using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class LobbyCountdownController : NetworkBehaviour
{
    [SerializeField, Min(1f)] private float waitSeconds = 20f;
    [SerializeField, Min(1f)] private float finalCountdownSeconds = 3f;
    [Networked] public int ConnectedPlayers { get; private set; }
    [Networked] public int BotPlayers { get; private set; }
    [Networked] public int TotalContestants { get; private set; }
    [Networked] public int Capacity { get; private set; }
    [Networked] public NetworkBool CountdownRunning { get; private set; }
    [Networked] public NetworkBool RosterFinalized { get; private set; }
    [Networked] public NetworkBool MatchStarted { get; private set; }
    [Networked] private TickTimer CountdownTimer { get; set; }
    private NetworkBootstrap _bootstrap;
    private TrainingBotSpawner _spawner;
    public int MaximumPlayers => Object != null && Object.IsValid ? Capacity : 16;
    public float RemainingSeconds => Object != null && Object.IsValid && Runner != null && CountdownRunning
        ? Mathf.Max(0f, CountdownTimer.RemainingTime(Runner) ?? 0f) : 0f;

    public override void Spawned()
    {
        _bootstrap = Runner.GetComponent<NetworkBootstrap>();
        _spawner = Runner.GetComponent<TrainingBotSpawner>();
        if (!HasStateAuthority) return;
        Capacity = _bootstrap != null ? _bootstrap.MatchCapacity : 16;
        CountdownRunning = false;
        RosterFinalized = false;
        MatchStarted = false;
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
        if (!CountdownRunning)
        {
            CountdownRunning = true;
            CountdownTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(1f, waitSeconds));
        }
        if (!RosterFinalized)
        {
            if (humans < Capacity && !CountdownTimer.Expired(Runner)) return;
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
}
