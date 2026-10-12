using System;
using UnityEngine;

namespace NewBattle.Progression
{
    /// <summary>
    /// Gunluk giris odulu: oyuncu her yeni gunde oyunu actiginda siradaki
    /// gunun XP'sini alir (1. gun, 2. gun ... 7. gun). Gun kacirmak seriyi
    /// bozmaz; liste bitince odul de biter. Rakamlar IlerlemeAyarlari'nda.
    /// </summary>
    public static class DailyLogin
    {
        private const string ClaimedKey = "nb_daily_claimed";
        private const string LastDateKey = "nb_daily_last_date";

        /// <summary>Kac gunun odulu alindi.</summary>
        public static int ClaimedDays => Mathf.Max(0, PlayerPrefs.GetInt(ClaimedKey, 0));

        public static int TotalDays => ProgressionSettings.Instance.dailyLoginXp?.Length ?? 0;

        /// <summary>Bugun alinabilecek odul var mi? Varsa kacinci gun (0'dan) ve XP'si.</summary>
        public static bool CanClaim(out int dayIndex, out int xp)
        {
            dayIndex = ClaimedDays;
            xp = 0;

            if (dayIndex >= TotalDays || PlayerPrefs.GetString(LastDateKey, string.Empty) == Today)
                return false;

            xp = ProgressionSettings.Instance.dailyLoginXp[dayIndex];
            return true;
        }

        /// <summary>Bugunun odulunu verir. Alinacak odul yoksa 0 dondurur.</summary>
        public static int Claim()
        {
            if (!CanClaim(out int dayIndex, out int xp))
                return 0;

            PlayerPrefs.SetInt(ClaimedKey, dayIndex + 1);
            PlayerPrefs.SetString(LastDateKey, Today);
            PlayerPrefs.Save();

            PlayerProgress.AddXp(xp);
            return xp;
        }

        /// <summary>Test icin: bugunun odulu tekrar alinabilsin.</summary>
        public static void AllowAgainToday()
        {
            PlayerPrefs.DeleteKey(LastDateKey);
            PlayerPrefs.Save();
        }

        /// <summary>Test icin: 1. gune doner.</summary>
        public static void Reset()
        {
            PlayerPrefs.DeleteKey(ClaimedKey);
            PlayerPrefs.DeleteKey(LastDateKey);
            PlayerPrefs.Save();
        }

        private static string Today => DateTime.Now.ToString("yyyy-MM-dd");
    }
}
