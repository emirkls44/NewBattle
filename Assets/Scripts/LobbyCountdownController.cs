using System.Collections.Generic;
using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class LobbyCountdownController : NetworkBehaviour
{
    [SerializeField, Min(1f)] private float waitSeconds = 20f;
    [SerializeField, Min(1f)] private float finalCountdownSeconds = 3f;
    [Tooltip("Takim odasi: kurucu BASLAT'a bastiktan sonra herkesin inis noktasi secmesi icin sure.")]
    [SerializeField, Min(1f)] private float teamRoomPickSeconds = 12f;
    [Networked] public int ConnectedPlayers { get; private set; }
    [Networked] public int BotPlayers { get; private set; }
    [Networked] public int TotalContestants { get; private set; }
    [Networked] public int Capacity { get; private set; }
    [Networked] public NetworkBool CountdownRunning { get; private set; }
    [Networked] public NetworkBool RosterFinalized { get; private set; }
    [Networked] public NetworkBool MatchStarted { get; private set; }

    // ------------------------------------------------------------------
    // Takim odasi (Takimla Oyna): takim secimi, hazir, kurucu baslatir.
    // ------------------------------------------------------------------

    /// <summary>Kurucu BASLAT'a basti: oda kapanir, harita secim ekrani acilir.</summary>
    [Networked] public NetworkBool RoomStarted { get; private set; }

    /// <summary>0 = katilan oyuncu kadar, 4 / 8 / 16 = bos yerlere bot.</summary>
    [Networked] public int RoomSizeChoice { get; private set; }

    /// <summary>Bir takimdaki oyuncu sayisi (2 ya da 4).</summary>
    [Networked] public int RoomTeamSize { get; private set; }

    /// <summary>Odayi kuran oyuncu.</summary>
    [Networked] public PlayerRef RoomHost { get; private set; }

    [Networked] private TickTimer CountdownTimer { get; set; }
    private NetworkBootstrap _bootstrap;
    private TrainingBotSpawner _spawner;
    public int MaximumPlayers => Object != null && Object.IsValid ? Capacity : 16;
    public float RemainingSeconds => Object != null && Object.IsValid && Runner != null && CountdownRunning
        ? Mathf.Max(0f, CountdownTimer.RemainingTime(Runner) ?? 0f) : 0f;

    /// <summary>Odadaki gercek oyuncu yeri (secime gore).</summary>
    public int RoomSlotLimit => RoomSizeChoice > 0
        ? Mathf.Min(RoomSizeChoice, NetworkBootstrap.TeamRoomMaxPlayers)
        : NetworkBootstrap.TeamRoomMaxPlayers;

    /// <summary>Takim odasinda gosterilen takim sayisi.</summary>
    public int RoomTeamCount => Mathf.Max(1, Mathf.CeilToInt(RoomSlotLimit / (float)Mathf.Max(1, RoomTeamSize)));

    public override void Spawned()
    {
        _bootstrap = Runner.GetComponent<NetworkBootstrap>();
        _spawner = Runner.GetComponent<TrainingBotSpawner>();
        if (!HasStateAuthority) return;
        Capacity = _bootstrap != null ? _bootstrap.MatchCapacity : 16;
        CountdownRunning = false;
        RosterFinalized = false;
        MatchStarted = false;
        RoomStarted = false;
        RoomSizeChoice = NetworkBootstrap.TeamRoomSizeChoice;
        RoomTeamSize = Mathf.Max(1, NetworkBootstrap.TeamRoomTeamSize);
        RoomHost = Runner.LocalPlayer;
        CountdownTimer = default;
        ConnectedPlayers = BotPlayers = TotalContestants = 0;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || MatchStarted) return;
        if (_bootstrap == null || _spawner == null) return;
        int humans = 0;
        foreach (var player in Runner.ActivePlayers)
        {
            // Wait for both connection and player prefab creation.
            if (Runner.TryGetPlayerObject(player, out var obj) && obj != null) humans++;
        }
        ConnectedPlayers = humans;
        if (humans == 0) return;
        bool friendRoom = NetworkBootstrap.IsFriendRoom;

        // Takim odasi: kurucu baslatana kadar bekle (sure yok). Baslatinca herkes
        // inis noktasini secsin diye kisa bir sure, sonra bos yerlere bot ve son sayim.
        if (friendRoom && !RosterFinalized)
        {
            if (!RoomStarted)
            {
                CountdownRunning = false;
                return;
            }

            if (!CountdownRunning)
            {
                CountdownRunning = true;
                CountdownTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(1f, teamRoomPickSeconds));
            }

            if (!CountdownTimer.Expired(Runner)) return;
        }
        else if (!CountdownRunning)
        {
            CountdownRunning = true;
            CountdownTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(1f, waitSeconds));
        }

        if (!RosterFinalized)
        {
            if (!friendRoom && humans < Capacity && !CountdownTimer.Expired(Runner)) return;
            _bootstrap.RosterLocked = true;
            if (Runner.SessionInfo.IsValid)
            {
                Runner.SessionInfo.IsOpen = false;
                Runner.SessionInfo.IsVisible = false;
            }
            if (!_spawner.FillToCapacity(Capacity, out int bots, out int total)) return;
            BotPlayers = bots;
            TotalContestants = total;
            RosterFinalized = true;
            CountdownTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(1f, finalCountdownSeconds));
            return;
        }
        // Replace a disconnected participant during the final countdown as well.
        if (!_spawner.FillToCapacity(Capacity, out int currentBots, out int currentTotal)) return;
        BotPlayers = currentBots;
        TotalContestants = currentTotal;
        if (!CountdownTimer.Expired(Runner)) return;
        MatchStarted = true;
        CountdownRunning = false;
    }

    // ------------------------------------------------------------------
    // Takim odasi islemleri (sadece kurucu / sunucu)
    // ------------------------------------------------------------------

    /// <summary>Odadaki gercek oyuncular (bot yok).</summary>
    public List<PlayerCombatStats> RoomPlayers()
    {
        var list = new List<PlayerCombatStats>();
        if (Runner == null) return list;
        foreach (var stats in FindObjectsByType<PlayerCombatStats>(FindObjectsSortMode.None))
        {
            if (stats.Object == null || !stats.Object.IsValid || stats.Runner != Runner) continue;
            if (stats.Object.InputAuthority == PlayerRef.None) continue;
            list.Add(stats);
        }
        list.Sort((a, b) => a.Object.InputAuthority.RawEncoded.CompareTo(b.Object.InputAuthority.RawEncoded));
        return list;
    }

    private int CountInTeam(int team, PlayerCombatStats except)
    {
        int count = 0;
        foreach (var stats in RoomPlayers())
            if (stats != except && stats.TeamId == team) count++;
        return count;
    }

    /// <summary>Kurucu oyuncu sayisini degistirir. Odadakilerden az secilemez.</summary>
    public bool SetRoomSizeChoice(int choice)
    {
        if (!HasStateAuthority || RoomStarted) return false;
        if (choice > 0 && choice < ConnectedPlayers) return false;
        if (choice > 0 && choice / Mathf.Max(1, RoomTeamSize) < 2) return false; // en az iki takim
        RoomSizeChoice = choice;
        NetworkBootstrap.TeamRoomSizeChoice = choice;
        Rebalance();
        return true;
    }

    /// <summary>Kurucu takim boyunu degistirir; dolu takimlardan tasanlar bos takima gecer.</summary>
    public bool SetRoomTeamSize(int size)
    {
        if (!HasStateAuthority || RoomStarted || size < 1) return false;
        if (RoomSizeChoice > 0 && RoomSizeChoice / size < 2) return false;
        RoomTeamSize = size;
        NetworkBootstrap.TeamRoomTeamSize = size;
        Rebalance();
        return true;
    }

    /// <summary>Oyuncu baska takima gecmek istiyor (PlayerCombatStats RPC'sinden).</summary>
    public void HandleTeamRequest(PlayerCombatStats player, int team)
    {
        if (!HasStateAuthority || RoomStarted || player == null) return;
        if (team < 0 || team >= RoomTeamCount || team == player.TeamId) return;
        if (CountInTeam(team, player) >= RoomTeamSize) return;
        player.AssignTeam(team);
    }

    /// <summary>Takim boyu / sayisi kuculunce sigmayanlari bos takimlara dagitir.</summary>
    private void Rebalance()
    {
        var counts = new Dictionary<int, int>();
        foreach (var stats in RoomPlayers())
        {
            counts.TryGetValue(stats.TeamId, out int count);
            if (stats.TeamId < RoomTeamCount && count < RoomTeamSize)
            {
                counts[stats.TeamId] = count + 1;
                continue;
            }

            for (int team = 0; team < RoomTeamCount; team++)
            {
                counts.TryGetValue(team, out int used);
                if (used >= RoomTeamSize) continue;
                stats.AssignTeam(team);
                counts[team] = used + 1;
                break;
            }
        }
    }

    /// <summary>Baslatilabilir mi; degilse neden (ekranda gosterilir).</summary>
    public bool CanStartRoom(out string reason)
    {
        reason = string.Empty;
        if (RoomStarted) return false;

        List<PlayerCombatStats> players = RoomPlayers();
        var teams = new HashSet<int>();
        int notReady = 0;
        foreach (var stats in players)
        {
            teams.Add(stats.TeamId);
            if (stats.Object.InputAuthority != RoomHost && !stats.RoomReady) notReady++;
        }

        if (notReady > 0)
        {
            reason = notReady == 1 ? "1 oyuncu henüz hazır değil" : $"{notReady} oyuncu henüz hazır değil";
            return false;
        }

        if (RoomSizeChoice == 0 && teams.Count < 2)
        {
            reason = players.Count < 2
                ? "Katılan oyuncu kadar: en az 2 oyuncu gerekli"
                : "En az 2 takımda oyuncu olmalı";
            return false;
        }

        return true;
    }

    /// <summary>Kurucu BASLAT'a basti.</summary>
    public bool StartRoom()
    {
        if (!HasStateAuthority || !CanStartRoom(out _)) return false;
        int humans = RoomPlayers().Count;
        Capacity = RoomSizeChoice > 0 ? Mathf.Max(RoomSizeChoice, humans) : humans;
        RoomStarted = true;
        _bootstrap.RosterLocked = true;
        if (Runner.SessionInfo.IsValid)
        {
            Runner.SessionInfo.IsOpen = false;
            Runner.SessionInfo.IsVisible = false;
        }
        return true;
    }
}
