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

    public int MatchCapacity
    {
        get
        {
            // Harita kendi kapasitesini soyluyorsa o kazanir: kucuk bir harita
            // 32 kisiyi kaldiramaz, buyuk bir harita 8 kisiyle bos kalir.
            int baseCapacity = HasMapList && SelectedMapIndex < maps.Length && maps[SelectedMapIndex].capacity >= 2
                ? maps[SelectedMapIndex].capacity
                : maxPlayers;

            return SelectedMode == BattleGameMode.Squad
                ? Mathf.Clamp(baseCapacity / 4 * 4, 8, 32)
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

    public async void StartMatchmaking(BattleGameMode mode)
    {
        if (_startRequested)
            return;

        SelectedMode = mode;
        ActiveMode = mode;
        ActiveMapIndex = SelectedMapIndex;
        _startRequested = true;
        _runner.ProvideInput = true;
        StatusChanged?.Invoke(mode == BattleGameMode.Bots
            ? "BOT MACI HAZIRLANIYOR..."
            : "ESLESME ARANIYOR...");

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
        Dictionary<string, SessionProperty> sessionProperties = isBotMode
            ? null
            : new Dictionary<string, SessionProperty>
            {
                { "mode", (int)mode },
                { "capacity", MatchCapacity },
                { "map", SelectedMapIndex },
                { "flow", 2 }
            };

        StartGameResult result = await _runner.StartGame(new StartGameArgs
        {
            GameMode = isBotMode ? GameMode.Single : GameMode.AutoHostOrClient,
            SessionName = isBotMode
                ? null
                : useFixedSessionName ? $"{sessionName}_{mode}_{SelectedMapIndex}_{MatchCapacity}_v2" : null,
            SessionProperties = sessionProperties,
            PlayerCount = isBotMode ? 1 : MatchCapacity,
            IsOpen = !isBotMode,
            IsVisible = !isBotMode,
            Scene = sceneInfo,
            SceneManager = GetComponent<NetworkSceneManagerDefault>()
        });

        if (!result.Ok)
        {
            _startRequested = false;
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
            StatusChanged?.Invoke("OYUNA BAGLANDI");
            ConnectionCompleted?.Invoke(true);
            Debug.Log($"Photon started successfully. Mode: {_runner.GameMode}");
        }
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"OnPlayerJoined received. Player: {player}, IsServer: {runner.IsServer}");

        if (!runner.IsServer)
        {
            return;
        }

        if (RosterLocked)
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
        int teamSize = SelectedMode == BattleGameMode.Squad ? 4 : 1;
        for (int id = 0; id < MatchCapacity; id++)
            if (!counts.TryGetValue(id, out int count) || count < teamSize) return id;
        return MatchCapacity;
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
