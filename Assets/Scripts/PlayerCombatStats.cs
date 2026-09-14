using Fusion;
using UnityEngine;

public class PlayerCombatStats : NetworkBehaviour
{
    [Networked] public int Kills { get; private set; }
    [Networked] public int TeamId { get; private set; }

    private static int _nextTrainingBotTeamId = 1000;

    public override void Spawned()
    {
        if (!HasStateAuthority)
            return;

        Kills = 0;

        if (Object.InputAuthority == PlayerRef.None)
        {
            // Test botlari birbirinin takim arkadasi sayilmasin.
            TeamId = _nextTrainingBotTeamId++;
            return;
        }

        if (NetworkBootstrap.ActiveMode == BattleGameMode.Squad)
        {
            // PlayerRef 1-4 = takim 0, 5-8 = takim 1, ...
            TeamId = Mathf.Max(0, (Object.InputAuthority.RawEncoded - 1) / 4);
            return;
        }

        // Herkes Tek modunda her gercek oyuncu kendi takimidir.
        TeamId = Object.InputAuthority.RawEncoded;
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
