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

        /// <summary>Takim arkadasi hala oynarken maçtan cikildi (sira o anki sira).</summary>
        public bool InProgress;
    }

    /// <summary>Bir mactan kazanilanlar ve oyuncunun once/sonra durumu.</summary>
    public struct MatchRewardResult
    {
        public int Xp;
        public int Coins;
        public bool WinAdded;

        /// <summary>Reklamla kazanilan cift XP bu macta kullanildi (Xp zaten iki kati).</summary>
        public bool DoubleXp;

        /// <summary>Ilk mac hediyesi verildiyse true (pencere menude acilir).</summary>
        public bool FirstGameGift;

        public int LevelBefore;
        public int XpBefore;
        public int LevelAfter;
        public int XpAfter;

        public bool LeveledUp => LevelAfter > LevelBefore;
    }

    /// <summary>
    /// Mac odullerinin kurallari. Rakamlar Assets/Resources/IlerlemeAyarlari
    /// dosyasinda; buradan degil oradan degistir.
    /// </summary>
    public static class MatchRewards
    {
        public static MatchRewardResult Calculate(MatchOutcome outcome)
        {
            ProgressionSettings settings = ProgressionSettings.Instance;
            int total = Mathf.Max(2, outcome.TotalPlayers);
            int placement = Mathf.Clamp(outcome.Placement, 1, total);

            // 1. sira tam bonusu alir, sonuncu hic almaz; arasi dogrusal.
            float placementShare = 1f - (placement - 1f) / (total - 1f);
            int placementXp = Mathf.RoundToInt(settings.maxPlacementXp * placementShare);

            int xp = settings.participationXp + Mathf.Max(0, outcome.Kills) * settings.xpPerKill + placementXp;
            int coins = Mathf.Max(0, outcome.Kills) * settings.coinsPerKill;

            if (placement <= 3)
                coins += settings.topThreeCoins;

            if (outcome.Victory)
            {
                xp += settings.victoryXp;
                coins += settings.victoryCoins;
            }

            bool doubleXp = PlayerProgress.DoubleXpMatches > 0;
            if (doubleXp)
                xp *= 2;

            return new MatchRewardResult { Xp = xp, Coins = coins, WinAdded = outcome.Victory, DoubleXp = doubleXp };
        }

        /// <summary>Odulleri hesaplar ve oyuncu ilerlemesine ekler.</summary>
        public static MatchRewardResult Apply(MatchOutcome outcome)
        {
            MatchRewardResult result = Calculate(outcome);
            result.LevelBefore = PlayerProgress.Level;
            result.XpBefore = PlayerProgress.Xp;

            if (result.DoubleXp)
                PlayerProgress.ConsumeDoubleXpMatch();

            PlayerProgress.AddXp(result.Xp);
            PlayerProgress.AddCoins(result.Coins);
            if (result.WinAdded)
                PlayerProgress.AddWin();

            result.LevelAfter = PlayerProgress.Level;
            result.XpAfter = PlayerProgress.Xp;

            // Ilk macin hediyesi: XP bari animasyonu bozulmasin diye seviye
            // degerleri okunduktan SONRA verilir; penceresi menude acilir.
            if (PlayerProgress.MatchesPlayed == 0)
            {
                RewardEntry gift = ProgressionSettings.Instance.PickFirstGameGift();
                if (gift != null)
                {
                    string body = PlayerProgress.GrantReward(gift, out RewardNoticeIcon _);
                    RewardInbox.Add(new RewardNotice("İLK OYUN HEDİYESİ!", body, RewardNoticeIcon.Gift));
                    result.FirstGameGift = true;
                }
            }

            PlayerProgress.AddMatchPlayed();
            PlayerProgress.RecordMatchStats(outcome.Kills, outcome.Victory ? 1 : outcome.Placement);

            // Gorevler: tamamlananin XP'si verilir, penceresi menude acilir.
            Challenges.RecordMatch(outcome);
            return result;
        }
    }
}
