using Fusion;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class BattleMatchController : NetworkBehaviour
{
    [SerializeField, Min(1f)] private float minimumMatchDuration = 5f;
    [SerializeField, Min(0.1f)] private float aliveCheckInterval = 0.25f;

    [Networked] public NetworkBool MatchEnded { get; private set; }
    [Networked] public int WinnerTeamId { get; private set; }
    [Networked] public PlayerRef Winner { get; private set; }
    [Networked] public int AliveCount { get; private set; }
    [Networked] private NetworkBool HadMultipleContestants { get; set; }
    [Networked] private TickTimer MinimumMatchTimer { get; set; }
    [Networked] private TickTimer AliveCheckTimer { get; set; }

    public override void Spawned()
    {
        if (!HasStateAuthority)
            return;

        MatchEnded = false;
        Winner = PlayerRef.None;
        WinnerTeamId = -1;
        AliveCount = 0;
        HadMultipleContestants = false;
        MinimumMatchTimer = TickTimer.CreateFromSeconds(Runner, minimumMatchDuration);
        AliveCheckTimer = TickTimer.CreateFromSeconds(Runner, aliveCheckInterval);
    }

    public override void FixedUpdateNetwork()
    {
        var lobby = GetComponent<LobbyCountdownController>();
        if (lobby == null || lobby.Object == null || !lobby.Object.IsValid || !lobby.MatchStarted) return;
        if (!HasStateAuthority || MatchEnded || !AliveCheckTimer.ExpiredOrNotRunning(Runner))
            return;

        AliveCheckTimer = TickTimer.CreateFromSeconds(Runner, aliveCheckInterval);

        HealthController[] players = UnityEngine.Object.FindObjectsByType<HealthController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        var livingTeams = new HashSet<int>();
        int alive = 0;
        HealthController lastAlive = null;

        foreach (HealthController player in players)
        {
            if (player == null || player.Object == null || !player.Object.IsValid || player.Runner != Runner || player.currentHealth <= 0f)
                continue;

            var stats = player.GetComponent<PlayerCombatStats>();
            if (stats != null) livingTeams.Add(stats.TeamId);
            alive++;
            lastAlive = player;
        }

        AliveCount = alive;

        bool squad = NetworkBootstrap.ActiveMode == BattleGameMode.Squad;
        int contenders = squad ? livingTeams.Count : alive;
        if (contenders >= 2 || lobby.TotalContestants >= 2)
            HadMultipleContestants = true;

        if (!HadMultipleContestants || !MinimumMatchTimer.Expired(Runner) || contenders > 1)
            return;

        Winner = alive == 1 && lastAlive != null
            ? lastAlive.Object.InputAuthority
            : PlayerRef.None;
        if (lastAlive != null)
        {
            var stats = lastAlive.GetComponent<PlayerCombatStats>();
            if (stats != null) WinnerTeamId = stats.TeamId;
        }
        MatchEnded = true;
    }
}
