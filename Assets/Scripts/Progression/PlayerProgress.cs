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

        public const int StartingCoins = 100;

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

        /// <summary>Bir sonraki seviye icin gereken XP.</summary>
        public static int XpForNextLevel(int level) => 50 + (Mathf.Max(1, level) - 1) * 20;

        /// <summary>Seviye 1 = Er I, 2 = Er II, 3 = Er III, 4 = Onbaşı I ...</summary>
        public static string RankName(int level)
        {
            int index = Mathf.Max(0, level - 1);
            int rank = Mathf.Min(index / RankTiers.Length, RankNames.Length - 1);
            string tier = RankTiers[index % RankTiers.Length];
            return $"{RankNames[rank]} {tier}";
        }

        public static void AddXp(int amount)
        {
            if (amount <= 0)
                return;

            int level = Level;
            int xp = Xp + amount;

            while (xp >= XpForNextLevel(level))
            {
                xp -= XpForNextLevel(level);
                level++;
            }

            PlayerPrefs.SetInt(LevelKey, level);
            PlayerPrefs.SetInt(XpKey, xp);
            Save();
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

            HashSet<string> owned = LoadOwned();
            if (!owned.Add(id))
                return;

            PlayerPrefs.SetString(OwnedKey, string.Join(",", owned));
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
            foreach (string key in new[] { LevelKey, XpKey, CoinsKey, WinsKey, OwnedKey, SelectedKey })
                PlayerPrefs.DeleteKey(key);

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
