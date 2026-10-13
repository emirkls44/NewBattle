using System;
using System.Collections.Generic;
using UnityEngine;

namespace NewBattle.Progression
{
    /// <summary>
    /// Oyuncunun kalici ilerlemesi: seviye, XP, para, galibiyet, sahip olunan
    /// ve secili lobi animasyonu.
    ///
    /// Simdilik cihazda (PlayerPrefs) tutulur. Ileride sunucuya tasindiginda
    /// sadece bu sinifin ici degisir; menu ve animasyon sistemi ayni kalir.
    /// </summary>
    public static class PlayerProgress
    {
        private const string LevelKey = "nb_level";
        private const string XpKey = "nb_xp";
        private const string CoinsKey = "nb_coins";
        private const string WinsKey = "nb_wins";
        private const string OwnedKey = "nb_owned_lobby_anims";
        private const string SelectedKey = "nb_selected_lobby_anim";
        private const string PlayerNameKey = "nb_player_name";
        private const string NameChosenKey = "nb_name_chosen";
        private const string NameChangesUsedKey = "nb_name_changes_used";
        private const string KillsKey = "nb_total_kills";
        private const string BestPlacementKey = "nb_best_placement";
        private const string MatchesKey = "nb_matches_played";
        private const string DoubleXpKey = "nb_double_xp_matches";

        public const int StartingCoins = 100;

        /// <summary>Isim degistirme hakki her bu kadar seviyede bir gelir (5, 10, 15...).</summary>
        public const int NameChangeLevelStep = 5;

        private static readonly string[] RankNames =
        {
            "Er", "Onbaşı", "Çavuş", "Başçavuş", "Asteğmen", "Teğmen",
            "Üsteğmen", "Yüzbaşı", "Binbaşı", "Yarbay", "Albay", "General"
        };

        private static readonly string[] RankTiers = { "I", "II", "III" };

        /// <summary>Herhangi bir deger degisince tetiklenir (menu kendini yeniler).</summary>
        public static event Action Changed;

        public static int Level => Mathf.Max(1, PlayerPrefs.GetInt(LevelKey, 1));
        public static int Xp => Mathf.Max(0, PlayerPrefs.GetInt(XpKey, 0));
        public static int Coins => Mathf.Max(0, PlayerPrefs.GetInt(CoinsKey, StartingCoins));
        public static int Wins => Mathf.Max(0, PlayerPrefs.GetInt(WinsKey, 0));
        public static int MatchesPlayed => Mathf.Max(0, PlayerPrefs.GetInt(MatchesKey, 0));

        /// <summary>Reklamla kazanilmis, henuz kullanilmamis cift XP maci sayisi.</summary>
        public static int DoubleXpMatches => Mathf.Max(0, PlayerPrefs.GetInt(DoubleXpKey, 0));

        /// <summary>Bos ise kutuphanedeki default animasyon kullanilir.</summary>
        public static string SelectedLobbyAnimation
        {
            get => PlayerPrefs.GetString(SelectedKey, string.Empty);
            set
            {
                PlayerPrefs.SetString(SelectedKey, value ?? string.Empty);
                Save();
            }
        }

        /// <summary>
        /// Oyuncunun adi. Ilk acilista "Savasci#1234" gibi rastgele bir ad
        /// uretilip kaydedilir. Macta diger oyunculara bu ad gorunur.
        /// </summary>
        public static string PlayerName
        {
            get
            {
                string saved = PlayerPrefs.GetString(PlayerNameKey, string.Empty);
                if (!string.IsNullOrEmpty(saved))
                    return saved;

                string created = $"Savaşçı#{UnityEngine.Random.Range(1000, 10000)}";
                PlayerPrefs.SetString(PlayerNameKey, created);
                PlayerPrefs.Save();
                return created;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    return;

                PlayerPrefs.SetString(PlayerNameKey, value.Trim());
                PlayerPrefs.SetInt(NameChosenKey, 1);
                Save();
            }
        }

        /// <summary>Oyuncu ilk acilista ismini sectiyse true (yoksa menu sorar).</summary>
        public static bool HasChosenName => PlayerPrefs.GetInt(NameChosenKey, 0) == 1;

        /// <summary>Kullanilmamis isim degistirme hakki (her 5 seviyede bir).</summary>
        public static int NameChangesAvailable =>
            Mathf.Max(0, Level / NameChangeLevelStep - PlayerPrefs.GetInt(NameChangesUsedKey, 0));

        /// <summary>Hak yoksa: bir sonraki hakkin gelecegi seviye.</summary>
        public static int NextNameChangeLevel =>
            (PlayerPrefs.GetInt(NameChangesUsedKey, 0) + 1) * NameChangeLevelStep;

        /// <summary>Ilk acilista isim secimi (hak harcamaz).</summary>
        public static void ChooseFirstName(string name) => PlayerName = name;

        /// <summary>Hakki varsa ismi degistirir ve bir hak harcar.</summary>
        public static bool TryChangeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || NameChangesAvailable <= 0)
                return false;

            PlayerPrefs.SetInt(NameChangesUsedKey, PlayerPrefs.GetInt(NameChangesUsedKey, 0) + 1);
            PlayerName = name;
            return true;
        }

        public static int TotalKills => Mathf.Max(0, PlayerPrefs.GetInt(KillsKey, 0));

        /// <summary>En iyi bitirme sirasi. 0 = henuz mac yok.</summary>
        public static int BestPlacement => Mathf.Max(0, PlayerPrefs.GetInt(BestPlacementKey, 0));

        /// <summary>Profil istatistikleri icin mac sonucunu kaydeder.</summary>
        public static void RecordMatchStats(int kills, int placement)
        {
            PlayerPrefs.SetInt(KillsKey, TotalKills + Mathf.Max(0, kills));
            if (placement > 0 && (BestPlacement == 0 || placement < BestPlacement))
                PlayerPrefs.SetInt(BestPlacementKey, placement);
            Save();
        }

        /// <summary>Bir sonraki seviye icin gereken XP (bkz. IlerlemeAyarlari).</summary>
        public static int XpForNextLevel(int level) => ProgressionSettings.Instance.XpForLevel(level);

        /// <summary>Seviye 1 = Er I, 2 = Er II, 3 = Er III, 4 = Onbaşı I ...</summary>
        public static string RankName(int level)
        {
            int index = Mathf.Max(0, level - 1);
            int rank = Mathf.Min(index / RankTiers.Length, RankNames.Length - 1);
            string tier = RankTiers[index % RankTiers.Length];
            return $"{RankNames[rank]} {tier}";
        }

        /// <summary>
        /// XP ekler. Seviye atlanirsa her yeni seviyenin odulu aninda verilir
        /// ve menude gosterilmek uzere RewardInbox'a yazilir.
        /// </summary>
        public static void AddXp(int amount)
        {
            if (amount <= 0)
                return;

            int startLevel = Level;
            int level = startLevel;
            long xp = (long)Xp + amount;

            while (xp >= XpForNextLevel(level))
            {
                xp -= XpForNextLevel(level);
                level++;
            }

            PlayerPrefs.SetInt(LevelKey, level);
            PlayerPrefs.SetInt(XpKey, (int)xp);

            for (int reached = startLevel + 1; reached <= level; reached++)
                GrantLevelReward(reached);

            Save();
        }

        private static void GrantLevelReward(int level)
        {
            LevelReward reward = ProgressionSettings.Instance.LevelRewardFor(level);
            var parts = new List<string>();

            if (reward.coins > 0)
            {
                PlayerPrefs.SetInt(CoinsKey, Coins + reward.coins);
                parts.Add($"+{reward.coins} Para");
            }

            if (!string.IsNullOrEmpty(reward.animationId) && !Owns(reward.animationId))
            {
                AddOwned(reward.animationId);
                parts.Add(string.IsNullOrEmpty(reward.animationLabel) ? "Yeni lobi animasyonu" : reward.animationLabel);
            }

            if (level % NameChangeLevelStep == 0)
                parts.Add("İsim değiştirme hakkı");

            RewardInbox.Add(new RewardNotice($"SEVİYE {level}!",
                parts.Count > 0 ? string.Join("\n", parts) : "Tebrikler!", RewardNoticeIcon.LevelUp));
        }

        /// <summary>
        /// Bir odulu verir (para, XP ya da animasyon) ve pencerede yazacak
        /// metni dondurur. Animasyona zaten sahipse yerine para verilir.
        /// </summary>
        public static string GrantReward(RewardEntry reward, out RewardNoticeIcon icon)
        {
            icon = RewardNoticeIcon.Gift;
            if (reward == null)
                return string.Empty;

            switch (reward.type)
            {
                case RewardType.Xp:
                    icon = RewardNoticeIcon.Xp;
                    AddXp(reward.amount);
                    return $"+{reward.amount} XP";

                case RewardType.LobbyAnimation when !string.IsNullOrEmpty(reward.animationId) && !Owns(reward.animationId):
                    icon = RewardNoticeIcon.Animation;
                    GrantOwnership(reward.animationId);
                    return string.IsNullOrEmpty(reward.label) ? "Yeni lobi animasyonu!" : $"{reward.label}\nlobi animasyonu";

                default:
                    int coins = reward.type == RewardType.Coins ? reward.amount : 200;
                    icon = RewardNoticeIcon.Coins;
                    AddCoins(coins);
                    return $"+{coins} Para";
            }
        }

        /// <summary>Oynanan mac sayisini bir artirir.</summary>
        public static void AddMatchPlayed()
        {
            PlayerPrefs.SetInt(MatchesKey, MatchesPlayed + 1);
            Save();
        }

        /// <summary>Cift XP maci ekler (reklam izlendi). Sinira takilirsa false.</summary>
        public static bool AddDoubleXpMatches(int amount)
        {
            int limit = ProgressionSettings.Instance.maxStoredDoubleXp;
            if (DoubleXpMatches >= limit)
                return false;

            PlayerPrefs.SetInt(DoubleXpKey, Mathf.Min(limit, DoubleXpMatches + Mathf.Max(1, amount)));
            Save();
            return true;
        }

        /// <summary>Bir cift XP macini kullanir. Hic yoksa false.</summary>
        public static bool ConsumeDoubleXpMatch()
        {
            if (DoubleXpMatches <= 0)
                return false;

            PlayerPrefs.SetInt(DoubleXpKey, DoubleXpMatches - 1);
            Save();
            return true;
        }

        public static void AddWin()
        {
            PlayerPrefs.SetInt(WinsKey, Wins + 1);
            Save();
        }

        public static void AddCoins(int amount)
        {
            if (amount <= 0)
                return;

            PlayerPrefs.SetInt(CoinsKey, Coins + amount);
            Save();
        }

        public static bool TrySpendCoins(int amount)
        {
            if (amount < 0 || Coins < amount)
                return false;

            PlayerPrefs.SetInt(CoinsKey, Coins - amount);
            Save();
            return true;
        }

        public static bool Owns(string id) => !string.IsNullOrEmpty(id) && LoadOwned().Contains(id);

        public static void GrantOwnership(string id)
        {
            if (string.IsNullOrEmpty(id))
                return;

            if (AddOwned(id))
                Save();
        }

        private static bool AddOwned(string id)
        {
            HashSet<string> owned = LoadOwned();
            if (!owned.Add(id))
                return false;

            PlayerPrefs.SetString(OwnedKey, string.Join(",", owned));
            return true;
        }

        /// <summary>Test icin: ilk mac hediyesi tekrar verilsin.</summary>
        public static void ResetMatchesPlayed()
        {
            PlayerPrefs.DeleteKey(MatchesKey);
            Save();
        }

        /// <summary>Test icin: degerleri dogrudan yazar.</summary>
        public static void SetValues(int level, int xp, int coins, int wins)
        {
            PlayerPrefs.SetInt(LevelKey, Mathf.Max(1, level));
            PlayerPrefs.SetInt(XpKey, Mathf.Max(0, xp));
            PlayerPrefs.SetInt(CoinsKey, Mathf.Max(0, coins));
            PlayerPrefs.SetInt(WinsKey, Mathf.Max(0, wins));
            Save();
        }

        /// <summary>Test icin: tum ilerlemeyi ilk acilis haline dondurur.</summary>
        public static void ResetAll()
        {
            foreach (string key in new[]
                     {
                         LevelKey, XpKey, CoinsKey, WinsKey, OwnedKey, SelectedKey, MatchesKey, DoubleXpKey,
                         PlayerNameKey, NameChosenKey, NameChangesUsedKey, KillsKey, BestPlacementKey
                     })
                PlayerPrefs.DeleteKey(key);

            // Ilk acilis karakter secimi de sifirlansin (Ahmet / Ayse ekrani tekrar gelsin).
            PlayerPrefs.DeleteKey(CharacterLibrary.StarterChosenKey);
            PlayerPrefs.DeleteKey("nb_selected_character");

            DailyLogin.Reset();
            Challenges.Reset();
            RewardInbox.Clear();
            Save();
        }

        private static HashSet<string> LoadOwned()
        {
            string raw = PlayerPrefs.GetString(OwnedKey, string.Empty);
            return new HashSet<string>(raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
        }

        private static void Save()
        {
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
