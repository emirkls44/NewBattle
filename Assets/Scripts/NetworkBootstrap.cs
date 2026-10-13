using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum BattleGameMode
{
    Bots = 0,
    EveryoneSolo = 1,
    Squad = 2
}

/// <summary>
/// Oynanabilir bir harita. Her harita KENDI SAHNESINDE yasar.
///
/// Neden sahne basina harita: 4 harita tek sahnede dursa, oyuncu hangi haritaya
/// duserse dussun dortunu birden yuklemis olur - bellek, acilis suresi ve mobil
/// indirme boyutu dorde katlanir. Ayri sahne ile sadece oynanan harita yuklenir.
/// </summary>
[System.Serializable]
public struct BattleMap
{
    [Tooltip("Menude ve kayitlarda gorunen ad.")]
    public string displayName;

    [Tooltip("Build Settings'teki sahne indeksi. -1 = mevcut sahneyi kullan.")]
    public int sceneBuildIndex;

    [Tooltip("Bu haritada ayni anda kac kisi oynayabilir.")]
    [Min(2)] public int capacity;
}

[RequireComponent(typeof(NetworkRunner))]
[RequireComponent(typeof(NetworkSceneManagerDefault))]
public class NetworkBootstrap : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Session")]
    [SerializeField] private bool autoStart;
    [SerializeField] private bool useFixedSessionName;
    [SerializeField] private string sessionName = "NewBattlePrototype";
    [SerializeField, Range(2, 32)] private int maxPlayers = 16;
    [Tooltip("Botlarla Oyna modunda sen dahil kac kisi olacagi. 16 = sen + 15 bot.")]
    [SerializeField, Range(2, 32)] private int botMatchPlayers = 16;

    [Header("Haritalar")]
    [Tooltip("Bos birakilirsa mevcut sahne tek harita olarak kullanilir.")]
    [SerializeField] private BattleMap[] maps;

    [Header("Spawner")]
    [SerializeField] private NetworkPrefabRef playerPrefab;
    [SerializeField] private float spawnRadius = 8f;

    private readonly Dictionary<PlayerRef, NetworkObject> _spawnedPlayers = new();
    private NetworkRunner _runner;
    private InputManager _inputManager;
    private bool _startRequested;
    private bool _callbacksRegistered;

    public event Action<string> StatusChanged;
    public event Action<bool> ConnectionCompleted;
    public bool IsRunning => _runner != null && _runner.IsRunning;
    public BattleGameMode SelectedMode { get; private set; } = BattleGameMode.Bots;
    public static BattleGameMode ActiveMode { get; private set; } = BattleGameMode.Bots;

    /// <summary>Squad modunda (menude "Rastgele Ikili") bir takimdaki oyuncu sayisi.</summary>
    public const int SquadTeamSize = 2;

    /// <summary>Takim odasina en fazla kac gercek oyuncu girebilir (Photon oda boyu).</summary>
    public const int TeamRoomMaxPlayers = 16;

    /// <summary>
    /// Takimla Oyna odasinin 4 haneli kodu; normal macta null. Sahne yeniden
    /// yuklense de kalir (lobi ve harita ekrani okur).
    /// </summary>
    public static string FriendRoomCode { get; private set; }

    public static bool IsFriendRoom => !string.IsNullOrEmpty(FriendRoomCode);

    /// <summary>
    /// Takim odasi ayarlari (odayi kuranda/host'ta gecerli; lobi bunlari aga yazar).
    /// Oyuncu sayisi: 0 = katilan oyuncu kadar (bot yok), 4 / 8 / 16 = bos yerlere bot.
    /// </summary>
    public static int TeamRoomSizeChoice { get; set; }

    /// <summary>Takim odasinda bir takimdaki oyuncu sayisi (2 ya da 4).</summary>
    public static int TeamRoomTeamSize { get; set; } = SquadTeamSize;

    /// <summary>Takim odasina kac gercek oyuncu girebilir (secime gore).</summary>
    public static int TeamRoomHumanLimit => TeamRoomSizeChoice > 0
        ? Mathf.Min(TeamRoomSizeChoice, TeamRoomMaxPlayers)
        : TeamRoomMaxPlayers;

    /// <summary>Su anki macta takim boyu: takim odasi secimi, rastgele ikili 2, digerleri 1.</summary>
    public static int ActiveTeamSize => IsFriendRoom ? Mathf.Max(1, TeamRoomTeamSize)
        : ActiveMode == BattleGameMode.Squad ? SquadTeamSize : 1;

    /// <summary>Baglanti basarisiz olup menuye donulunce gosterilecek mesaj (bir kez).</summary>
    public static string PendingMenuMessage { get; set; }

    /// <summary>
    /// Takim odasinda kurucu ayrilinca: 2. oyuncu ayni ayarlarla yeni oda kurar, digerleri
    /// ona katilir. Menu yeniden yuklenince MainMenuScreen bunu baslatir.
    /// </summary>
    public struct RoomRejoin
    {
        public bool Create;
        public string Code;
        public int SizeChoice;
        public int TeamSize;
        public int Attempts;
        public float Delay;
    }

    public static RoomRejoin? PendingRejoin { get; set; }

    /// <summary>Katilma basarisiz olursa (yeni kurucu henuz odayi acmadiysa) kac kez daha denenecek.</summary>
    public static int RejoinAttemptsLeft { get; set; }

    /// <summary>Takim odasindan cikildi: sonraki menu normal baslasin.</summary>
    public static void ClearFriendRoom() => FriendRoomCode = null;

    public int MatchCapacity
    {
        get
        {
            // Bot macinda baska insan katilamaz (tek kisilik oturum); kapasite
            // dogrudan "kac botla oynuyorum" sorusunun cevabi. maxPlayers online
            // odalar icin ayri tutuluyor.
            if (SelectedMode == BattleGameMode.Bots)
                return Mathf.Clamp(botMatchPlayers, 2, 32);

            // Takim odasi: Photon odasi en fazla 16 gercek oyuncu alir; macin asil
            // kapasitesini kurucu "BASLAT"a basinca lobi belirler.
            if (IsFriendRoom)
                return TeamRoomMaxPlayers;

            // Harita kendi kapasitesini soyluyorsa o kazanir: kucuk bir harita
            // 32 kisiyi kaldiramaz, buyuk bir harita 8 kisiyle bos kalir.
            int baseCapacity = HasMapList && SelectedMapIndex < maps.Length && maps[SelectedMapIndex].capacity >= 2
                ? maps[SelectedMapIndex].capacity
                : maxPlayers;

            return SelectedMode == BattleGameMode.Squad
                ? Mathf.Clamp(baseCapacity / SquadTeamSize * SquadTeamSize, 8, 32)
                : Mathf.Clamp(baseCapacity, 2, 32);
        }
    }
    public NetworkPrefabRef PlayerPrefab => playerPrefab;
    public bool RosterLocked { get; set; }

    /// <summary>Su an secili harita indeksi (maps dizisinde).</summary>
    public int SelectedMapIndex { get; private set; }

    public static int ActiveMapIndex { get; private set; }

    public BattleMap[] Maps => maps;

    public bool HasMapList => maps != null && maps.Length > 0;

    public string SelectedMapName =>
        HasMapList && SelectedMapIndex < maps.Length && !string.IsNullOrEmpty(maps[SelectedMapIndex].displayName)
            ? maps[SelectedMapIndex].displayName
            : "Varsayilan Harita";

    /// <summary>Menuden harita secimi. StartMatchmaking'den ONCE cagrilmali.</summary>
    public void SelectMap(int mapIndex)
    {
        if (!HasMapList)
        {
            SelectedMapIndex = 0;
            return;
        }

        SelectedMapIndex = Mathf.Clamp(mapIndex, 0, maps.Length - 1);
    }

    private void Awake()
    {
        _runner = GetComponent<NetworkRunner>();
        _runner.AddCallbacks(this);
        _callbacksRegistered = true;
    }

    private void OnDestroy()
    {
        if (_callbacksRegistered && _runner != null)
        {
            _runner.RemoveCallbacks(this);
        }
    }

    private void Start()
    {
        if (autoStart)
            StartMatchmaking(BattleGameMode.Bots);
    }

    public void StartMatchmaking()
    {
        StartMatchmaking(BattleGameMode.EveryoneSolo);
    }

    public void StartMatchmaking(BattleGameMode mode) => StartSession(mode, null, false);

    /// <summary>
    /// Takimla Oyna: createRoom = true ise bu kodla gizli bir oda kurar
    /// (rastgele oyuncular giremez); false ise SADECE var olan odaya katilir.
    /// Odada takimlar secilir, herkes hazir olunca kurucu baslatir; bos yerlere bot.
    /// sizeChoice: 0 = katilan kadar, 4/8/16. teamSize: 2 ya da 4 (sadece kurucuda gecerli).
    /// </summary>
    public void StartFriendRoom(bool createRoom, string code, int sizeChoice = 0, int teamSize = SquadTeamSize)
    {
        if (createRoom)
        {
            TeamRoomSizeChoice = sizeChoice;
            TeamRoomTeamSize = Mathf.Max(1, teamSize);
        }

        StartSession(BattleGameMode.Squad, code, createRoom);
    }

    private async void StartSession(BattleGameMode mode, string friendCode, bool createFriendRoom)
    {
        if (_startRequested)
            return;

        FriendRoomCode = string.IsNullOrEmpty(friendCode) ? null : friendCode;

        SelectedMode = mode;
        ActiveMode = mode;
        ActiveMapIndex = SelectedMapIndex;
        _startRequested = true;
        _runner.ProvideInput = true;
        StatusChanged?.Invoke(IsFriendRoom
            ? createFriendRoom ? $"ODA KURULUYOR...  KOD: {FriendRoomCode}" : $"ODAYA KATILINIYOR...  KOD: {FriendRoomCode}"
            : mode == BattleGameMode.Bots ? "BOT MACI HAZIRLANIYOR..." : "ESLESME ARANIYOR...");

        // Harita listesi doluysa o haritanin sahnesini, degilse mevcut sahneyi
        // yukle. Boylece tek sahneli mevcut proje de calismaya devam eder.
        int sceneIndex = SceneManager.GetActiveScene().buildIndex;

        if (HasMapList && SelectedMapIndex < maps.Length && maps[SelectedMapIndex].sceneBuildIndex >= 0)
            sceneIndex = maps[SelectedMapIndex].sceneBuildIndex;

        NetworkSceneInfo sceneInfo = new();
        sceneInfo.AddSceneRef(SceneRef.FromIndex(sceneIndex), LoadSceneMode.Single);

        bool isBotMode = mode == BattleGameMode.Bots;

        // ODA DAGITIMI BURADA OLUYOR.
        //
        // SessionName bos + AutoHostOrClient + SessionProperties = Photon'un
        // rastgele eslestirmesi. Photon, bu ozelliklerin HEPSI tutan ve DOLU
        // OLMAYAN bir oda arar; bulursa oyuncuyu oraya koyar, bulamazsa yeni
        // oda acar.
        //
        // "map" ozelligini eklemek, istenen davranisi tam olarak verir:
        // Harita 1 odasi 32 kisiyle dolunca 33. oyuncu Harita 1'in ikinci
        // odasini acar; Harita 2 secen oyuncu hicbir zaman Harita 1 odasina
        // dusmez. Oyuncu sayisini elle bolmeye gerek yok, dagitim kendiliginden
        // olusur.
        bool matchmakingProperties = !isBotMode && !IsFriendRoom;
        Dictionary<string, SessionProperty> sessionProperties = !matchmakingProperties
            ? null
            : new Dictionary<string, SessionProperty>
            {
                { "mode", (int)mode },
                { "capacity", MatchCapacity },
                { "map", SelectedMapIndex },
                { "flow", 2 }
            };

        // Arkadas odasi: kuran Host olur; katilan Client olarak SADECE var olan
        // odaya baglanir (yanlis kodla yeni bos oda acilmasin). Oda gorunmez:
        // rastgele eslestirme onu bulamaz.
        GameMode gameMode = isBotMode ? GameMode.Single
            : IsFriendRoom ? createFriendRoom ? GameMode.Host : GameMode.Client
            : GameMode.AutoHostOrClient;

        string session = isBotMode ? null
            : IsFriendRoom ? $"NB_ARKADAS_{FriendRoomCode}_v2"
            : useFixedSessionName ? $"{sessionName}_{mode}_{SelectedMapIndex}_{MatchCapacity}_v2" : null;

        StartGameResult result = await _runner.StartGame(new StartGameArgs
        {
            GameMode = gameMode,
            SessionName = session,
            SessionProperties = sessionProperties,
            PlayerCount = isBotMode ? 1 : MatchCapacity,
            IsOpen = !isBotMode,
            IsVisible = !isBotMode && !IsFriendRoom,
            Scene = sceneInfo,
            SceneManager = GetComponent<NetworkSceneManagerDefault>()
        });

        if (!result.Ok)
        {
            _startRequested = false;

            // Yeni kurucunun odasi henuz acilmamis olabilir: biraz bekleyip tekrar dene.
            if (IsFriendRoom && !createFriendRoom && RejoinAttemptsLeft > 0)
            {
                PendingRejoin = new RoomRejoin
                {
                    Create = false,
                    Code = FriendRoomCode,
                    Attempts = RejoinAttemptsLeft - 1,
                    Delay = 1.5f
                };
                RejoinAttemptsLeft = 0;
                FriendRoomCode = null;
                PendingMenuMessage = "YENİ ODA BEKLENİYOR...";
                Invoke(nameof(ReloadMenu), 0.3f);
                return;
            }

            if (IsFriendRoom)
            {
                // Yanlis kod / oda dolu / kod cakismasi. Basarisiz runner tekrar
                // baslatilamaz: kisa bir mesajdan sonra menuyu temiz yeniden yukle.
                string message = createFriendRoom ? "ODA KURULAMADI, TEKRAR DENE" : "ODA BULUNAMADI";
                PendingMenuMessage = message;
                StatusChanged?.Invoke(message);
                ConnectionCompleted?.Invoke(false);
                FriendRoomCode = null;
                Invoke(nameof(ReloadMenu), 1.8f);
                return;
            }

            StatusChanged?.Invoke($"BAGLANTI HATASI: {result.ShutdownReason}");
            ConnectionCompleted?.Invoke(false);
            Debug.LogError(
                $"Photon connection failed: {result.ShutdownReason}\n" +
                $"Message: {result.ErrorMessage}\n" +
                $"Stack: {result.StackTrace}"
            );
        }
        else
        {
            RejoinAttemptsLeft = 0;
            StatusChanged?.Invoke("OYUNA BAGLANDI");
            ConnectionCompleted?.Invoke(true);
            Debug.Log($"Photon started successfully. Mode: {_runner.GameMode}");
        }
    }

    private void ReloadMenu()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"OnPlayerJoined received. Player: {player}, IsServer: {runner.IsServer}");

        if (!runner.IsServer)
        {
            return;
        }

        // Takim odasi: kurucunun sectigi oyuncu sayisi doluysa yeni gelen giremez.
        if (RosterLocked || (IsFriendRoom && _spawnedPlayers.Count >= TeamRoomHumanLimit))
        {
            runner.Disconnect(player);
            return;
        }

        if (!playerPrefab.IsValid)
        {
            Debug.LogError("PlayerOnline was not assigned or is not registered as a Fusion network prefab.");
            return;
        }

        if (_spawnedPlayers.ContainsKey(player))
        {
            return;
        }

        float angle = player.RawEncoded * 137.5f * Mathf.Deg2Rad;
        Vector3 spawnPosition = new(
            Mathf.Cos(angle) * spawnRadius,
            0f,
            Mathf.Sin(angle) * spawnRadius
        );

        NetworkObject playerObject = runner.Spawn(
            playerPrefab,
            spawnPosition,
            Quaternion.identity,
            player
        );

        var stats = playerObject.GetComponent<PlayerCombatStats>();
        if (stats != null) stats.AssignTeam(FindAvailableTeam(playerObject));
        _spawnedPlayers[player] = playerObject;
        runner.SetPlayerObject(player, playerObject);
    }

    public int FindAvailableTeam(NetworkObject excluded)
    {
        var counts = new Dictionary<int, int>();
        foreach (var stats in UnityEngine.Object.FindObjectsByType<PlayerCombatStats>(FindObjectsSortMode.None))
        {
            if (stats.Object == null || !stats.Object.IsValid || stats.Runner != _runner || stats.Object == excluded) continue;
            counts.TryGetValue(stats.TeamId, out int count);
            counts[stats.TeamId] = count + 1;
        }
        int teamSize = SelectedMode == BattleGameMode.Squad ? ActiveTeamSize : 1;
        int limit = Mathf.Max(MatchCapacity, TeamRoomMaxPlayers);
        for (int id = 0; id < limit; id++)
            if (!counts.TryGetValue(id, out int count) || count < teamSize) return id;
        return limit;
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer)
        {
            return;
        }

        if (_spawnedPlayers.Remove(player, out NetworkObject playerObject))
        {
            runner.Despawn(playerObject);
        }
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        if (_inputManager == null)
        {
            _inputManager = InputManager.Instance != null
                ? InputManager.Instance
                : FindFirstObjectByType<InputManager>();
        }

        NetworkInputData data = _inputManager != null
            ? _inputManager.GetNetworkInput()
            : default;

#if UNITY_EDITOR
        // Editor test fallback. Mobile builds continue to use the joysticks.
        if (data.JoystickInput.sqrMagnitude < 0.001f && Keyboard.current != null)
        {
            Vector2 keyboardInput = Vector2.zero;

            if (Keyboard.current.wKey.isPressed) keyboardInput.y += 1f;
            if (Keyboard.current.sKey.isPressed) keyboardInput.y -= 1f;
            if (Keyboard.current.dKey.isPressed) keyboardInput.x += 1f;
            if (Keyboard.current.aKey.isPressed) keyboardInput.x -= 1f;

            data.JoystickInput = Vector2.ClampMagnitude(keyboardInput, 1f);
        }
#endif

        input.Set(data);
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        _startRequested = false;
        StatusChanged?.Invoke($"BAGLANTI KAPANDI: {shutdownReason}");
    }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        _startRequested = false;
        StatusChanged?.Invoke($"SUNUCU BAGLANTISI KESILDI: {reason}");
        ConnectionCompleted?.Invoke(false);
    }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}
