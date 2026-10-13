using Fusion;
using UnityEngine;

public class PlayerCombatStats : NetworkBehaviour
{
    [Networked] public int Kills { get; private set; }
    [Networked] public int TeamId { get; private set; }

    /// <summary>Oyuncunun adi (oldurme mesaji, ileride oldurme akisi icin).</summary>
    [Networked] public NetworkString<_16> DisplayName { get; private set; }

    /// <summary>Oyuncunun sectigi karakter (bos = varsayilan). Herkes bu modeli gorur.</summary>
    [Networked] public NetworkString<_16> CharacterId { get; private set; }

    /// <summary>Takim odasinda "HAZIR"a basti mi (kurucu herkes hazir olunca baslatir).</summary>
    [Networked] public NetworkBool RoomReady { get; private set; }

    private string _appliedCharacter;

    private static int _nextTrainingBotTeamId = 1000;
    private static int _nextBotNameIndex;

    private static readonly string[] BotNames =
    {
        "Dorian", "Kartal", "Poyraz", "Duman", "Panter", "Atmaca", "Şimşek", "Tayfun",
        "Kuzgun", "Bulut", "Fırtına", "Kaplan", "Yıldız", "Efsane", "Zeytin", "Kurt",
        "Alaz", "Boran", "Toprak", "Ateş", "Kılıç", "Rüzgar", "Çakal", "Volkan",
        "Gölge", "Kobra", "Tilki", "Ayaz", "Kara", "Doruk", "Yaman", "Asena"
    };

    public override void Spawned()
    {
        // Gercek oyuncu kendi adini sunucuya bildirir.
        if (HasInputAuthority)
        {
            RPC_SetDisplayName(NewBattle.Progression.PlayerProgress.PlayerName);

            NewBattle.Progression.CharacterLibrary characters = NewBattle.Progression.CharacterLibrary.Instance;
            if (characters != null && characters.Current != null)
                RPC_SetCharacter(characters.Current.id);
        }

        ApplyCharacter();

        if (!HasStateAuthority)
            return;

        Kills = 0;

        if (Object.InputAuthority == PlayerRef.None)
        {
            // Test botlari birbirinin takim arkadasi sayilmasin.
            TeamId = _nextTrainingBotTeamId++;
            DisplayName = BotNames[_nextBotNameIndex++ % BotNames.Length];

            // Botlar da Ahmet ya da Ayse olarak gelir (hepsi ayni gorunmesin).
            NewBattle.Progression.CharacterDefinition botCharacter =
                NewBattle.Progression.CharacterLibrary.Instance != null
                    ? NewBattle.Progression.CharacterLibrary.Instance.RandomStarter()
                    : null;
            if (botCharacter != null)
                CharacterId = botCharacter.id;
            return;
        }

        if (NetworkBootstrap.ActiveMode == BattleGameMode.Squad)
        {
            // Ikili: PlayerRef 1-2 = takim 0, 3-4 = takim 1, ... (sunucu sonra
            // NetworkBootstrap.FindAvailableTeam ile kesinlestirir)
            TeamId = Mathf.Max(0, (Object.InputAuthority.RawEncoded - 1) / NetworkBootstrap.ActiveTeamSize);
            return;
        }

        // Herkes Tek modunda her gercek oyuncu kendi takimidir.
        TeamId = Object.InputAuthority.RawEncoded;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return;

        DisplayName = displayName.Length > 16 ? displayName.Substring(0, 16) : displayName;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetCharacter(string characterId)
    {
        if (string.IsNullOrEmpty(characterId) || characterId.Length > 16)
            return;

        CharacterId = characterId;
    }

    /// <summary>Takim odasi: hazir / hazir degil.</summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetRoomReady(NetworkBool ready)
    {
        var lobby = FindFirstObjectByType<LobbyCountdownController>();
        if (lobby != null && lobby.RoomStarted)
            return;

        RoomReady = ready;
    }

    /// <summary>Takim odasi: baska takima gecme istegi (yer varsa sunucu gecirir).</summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_RequestTeam(int team)
    {
        var lobby = FindFirstObjectByType<LobbyCountdownController>();
        if (lobby != null)
            lobby.HandleTeamRequest(this, team);
    }

    public override void Render() => ApplyCharacter();

    /// <summary>Ag uzerinden gelen karakter degistiyse modeli degistirir (her cihazda).</summary>
    private void ApplyCharacter()
    {
        string id = CharacterId.ToString();
        if (id == _appliedCharacter)
            return;

        _appliedCharacter = id;

        NewBattle.Progression.CharacterLibrary characters = NewBattle.Progression.CharacterLibrary.Instance;
        if (characters == null)
            return;

        NewBattle.Progression.CharacterDefinition character = characters.Find(id) ?? characters.Default;
        NewBattle.Gameplay.CharacterModelSwap.Apply(GetComponentInChildren<Animator>(true), character, characters.Default);
    }

    /// <summary>Gosterilecek ad; bos ise "Oyuncu".</summary>
    public string NameOrDefault
    {
        get
        {
            string value = DisplayName.ToString();
            return string.IsNullOrEmpty(value) ? "Oyuncu" : value;
        }
    }

    public void AssignTeam(int teamId)
    {
        if (HasStateAuthority) TeamId = teamId;
    }

    public void AddKill()
    {
        if (HasStateAuthority)
            Kills++;
    }

    public bool IsTeammate(PlayerCombatStats other)
    {
        return other != null && TeamId == other.TeamId;
    }
}
