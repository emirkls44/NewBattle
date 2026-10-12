using UnityEngine;

namespace NewBattle.Progression
{
    /// <summary>Bir macin sonucu (maç sonu ekrani doldurur).</summary>
    public struct MatchOutcome
    {
        public bool Victory;
        public int Placement;
        public int Kills;
        public int TotalPlayers;
    }

    /// <summary>Bir mactan kazanilanlar ve oyuncunun once/sonra durumu.</summary>
    public struct MatchRewardResult
    {
        public int Xp;
        public int Coins;
        public bool WinAdded;

        public int LevelBefore;
        public int XpBefore;
        public int LevelAfter;
        public int XpAfter;

        public bool LeveledUp => LevelAfter > LevelBefore;
    }

    /// <summary>
    /// Mac odullerinin kurallari. Rakamlari degistirmek icin sadece bu
    /// dosyadaki sabitlerle oyna.
    /// </summary>
    public static class MatchRewards
    {
        public const int ParticipationXp = 5;
        public const int XpPerKill = 10;
        public const int MaxPlacementXp = 20;
        public const int VictoryXp = 30;

        public const int CoinsPerKill = 5;
        public const int TopThreeCoins = 10;
        public const int VictoryCoins = 25;

        public static MatchRewardResult Calculate(MatchOutcome outcome)
        {
            int total = Mathf.Max(2, outcome.TotalPlayers);
            int placement = Mathf.Clamp(outcome.Placement, 1, total);

            // 1. sira tam bonusu alir, sonuncu hic almaz; arasi dogrusal.
            float placementShare = 1f - (placement - 1f) / (total - 1f);
            int placementXp = Mathf.RoundToInt(MaxPlacementXp * placementShare);

            int xp = ParticipationXp + outcome.Kills * XpPerKill + placementXp;
            int coins = outcome.Kills * CoinsPerKill;

            if (placement <= 3)
                coins += TopThreeCoins;

            if (outcome.Victory)
            {
                xp += VictoryXp;
                coins += VictoryCoins;
            }

            return new MatchRewardResult { Xp = xp, Coins = coins, WinAdded = outcome.Victory };
        }

        /// <summary>Odulleri hesaplar ve oyuncu ilerlemesine ekler.</summary>
        public static MatchRewardResult Apply(MatchOutcome outcome)
        {
            MatchRewardResult result = Calculate(outcome);
            result.LevelBefore = PlayerProgress.Level;
            result.XpBefore = PlayerProgress.Xp;

            PlayerProgress.AddXp(result.Xp);
            PlayerProgress.AddCoins(result.Coins);
            if (result.WinAdded)
                PlayerProgress.AddWin();

            result.LevelAfter = PlayerProgress.Level;
            result.XpAfter = PlayerProgress.Xp;
            return result;
        }
    }
}
