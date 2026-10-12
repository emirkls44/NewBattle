using System;
using System.Collections.Generic;
using UnityEngine;

namespace NewBattle.Progression
{
    /// <summary>Ekranda gosterilen bir gorevin durumu.</summary>
    public struct ChallengeStatus
    {
        public ChallengeDef Def;
        public int Progress;
        public bool Done;
    }

    /// <summary>
    /// Gunluk ve haftalik gorevler. Her gun (haftalikta her pazartesi)
    /// havuzdan birkac gorev secilir; maclar ilerletir, tamamlanan gorevin
    /// XP'si aninda verilir ve menude "GÖREV TAMAMLANDI!" penceresi cikar.
    ///
    /// Secim tarihe gore sabittir: ayni gun oyunu kapatip acmak gorevleri
    /// degistirmez. Gorevler IlerlemeAyarlari'nda.
    /// </summary>
    public static class Challenges
    {
        private const string DailyKey = "nb_challenges_daily";
        private const string WeeklyKey = "nb_challenges_weekly";

        [Serializable]
        private class Entry
        {
            public string id;
            public int progress;
            public bool done;
        }

        [Serializable]
        private class Store
        {
            public string period;
            public List<Entry> entries = new();
        }

        public static List<ChallengeStatus> Daily => Statuses(daily: true);
        public static List<ChallengeStatus> Weekly => Statuses(daily: false);

        /// <summary>Bir sonraki yenilenmeye kalan sure.</summary>
        public static TimeSpan TimeUntilReset(bool daily)
        {
            DateTime now = DateTime.Now;
            DateTime next = daily ? now.Date.AddDays(1) : WeekStart(now).AddDays(7);
            return next - now;
        }

        /// <summary>Bir mac bitince cagrilir: gorevleri ilerletir, tamamlananlarin XP'sini verir.</summary>
        public static void RecordMatch(MatchOutcome outcome)
        {
            Advance(daily: true, outcome);
            Advance(daily: false, outcome);
        }

        /// <summary>Test icin: gorevleri sifirlar.</summary>
        public static void Reset()
        {
            PlayerPrefs.DeleteKey(DailyKey);
            PlayerPrefs.DeleteKey(WeeklyKey);
            PlayerPrefs.Save();
        }

        private static void Advance(bool daily, MatchOutcome outcome)
        {
            Store store = Load(daily);
            ChallengeDef[] pool = Pool(daily);

            foreach (Entry entry in store.entries)
            {
                ChallengeDef def = Find(pool, entry.id);
                if (def == null || entry.done)
                    continue;

                int amount = def.goal switch
                {
                    ChallengeGoal.PlayMatches => 1,
                    ChallengeGoal.Kills => Mathf.Max(0, outcome.Kills),
                    ChallengeGoal.Wins => outcome.Victory ? 1 : 0,
                    ChallengeGoal.TopThree => outcome.Placement is > 0 and <= 3 ? 1 : 0,
                    ChallengeGoal.TopTen => outcome.Placement is > 0 and <= 10 ? 1 : 0,
                    _ => 0
                };

                if (amount <= 0)
                    continue;

                entry.progress = Mathf.Min(def.target, entry.progress + amount);
                if (entry.progress < def.target)
                    continue;

                entry.done = true;
                Save(daily, store);

                PlayerProgress.AddXp(def.xp);
                RewardInbox.Add(new RewardNotice("GÖREV TAMAMLANDI!", $"{def.title}\n+{def.xp} XP", RewardNoticeIcon.Xp));
            }

            Save(daily, store);
        }

        private static List<ChallengeStatus> Statuses(bool daily)
        {
            Store store = Load(daily);
            ChallengeDef[] pool = Pool(daily);
            var result = new List<ChallengeStatus>();

            foreach (Entry entry in store.entries)
            {
                ChallengeDef def = Find(pool, entry.id);
                if (def != null)
                    result.Add(new ChallengeStatus { Def = def, Progress = entry.progress, Done = entry.done });
            }

            return result;
        }

        /// <summary>Kayitli donem bittiyse yeni gorevleri secip dondurur.</summary>
        private static Store Load(bool daily)
        {
            string period = PeriodId(daily);
            Store store = null;

            string json = PlayerPrefs.GetString(daily ? DailyKey : WeeklyKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    store = JsonUtility.FromJson<Store>(json);
                }
                catch (ArgumentException)
                {
                    store = null;
                }
            }

            if (store != null && store.period == period && store.entries.Count > 0)
                return store;

            store = new Store { period = period };
            foreach (ChallengeDef def in Pick(daily, period))
                store.entries.Add(new Entry { id = def.id });

            Save(daily, store);
            return store;
        }

        private static IEnumerable<ChallengeDef> Pick(bool daily, string period)
        {
            ProgressionSettings settings = ProgressionSettings.Instance;
            var pool = new List<ChallengeDef>();
            foreach (ChallengeDef def in Pool(daily))
            {
                if (def != null && !string.IsNullOrEmpty(def.id))
                    pool.Add(def);
            }

            int count = Mathf.Min(daily ? settings.dailyChallengeCount : settings.weeklyChallengeCount, pool.Count);

            // Tarihten uretilen tohum: ayni gun hep ayni gorevler.
            var random = new System.Random(int.Parse(period) * (daily ? 1 : 7));
            for (int i = 0; i < count; i++)
            {
                int pick = random.Next(i, pool.Count);
                (pool[i], pool[pick]) = (pool[pick], pool[i]);
                yield return pool[i];
            }
        }

        private static ChallengeDef[] Pool(bool daily)
        {
            ProgressionSettings settings = ProgressionSettings.Instance;
            return (daily ? settings.dailyChallenges : settings.weeklyChallenges) ?? Array.Empty<ChallengeDef>();
        }

        private static ChallengeDef Find(ChallengeDef[] pool, string id)
        {
            foreach (ChallengeDef def in pool)
            {
                if (def != null && def.id == id)
                    return def;
            }

            return null;
        }

        private static string PeriodId(bool daily)
        {
            DateTime day = daily ? DateTime.Now.Date : WeekStart(DateTime.Now);
            return day.ToString("yyyyMMdd");
        }

        private static DateTime WeekStart(DateTime time)
        {
            int sinceMonday = ((int)time.DayOfWeek + 6) % 7;
            return time.Date.AddDays(-sinceMonday);
        }

        private static void Save(bool daily, Store store)
        {
            PlayerPrefs.SetString(daily ? DailyKey : WeeklyKey, JsonUtility.ToJson(store));
            PlayerPrefs.Save();
        }
    }
}
