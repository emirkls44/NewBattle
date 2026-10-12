using System;
using System.Collections.Generic;
using UnityEngine;

namespace NewBattle.Progression
{
    public enum RewardNoticeIcon
    {
        Gift,
        Coins,
        Xp,
        LevelUp,
        Animation
    }

    /// <summary>Menude gosterilecek bir odul bildirimi ("SEVİYE 3!  +250 Altın").</summary>
    [Serializable]
    public class RewardNotice
    {
        public string title;
        public string body;
        public RewardNoticeIcon icon;

        public RewardNotice(string title, string body, RewardNoticeIcon icon)
        {
            this.title = title;
            this.body = body;
            this.icon = icon;
        }
    }

    /// <summary>
    /// Kazanilmis ama oyuncuya henuz gosterilmemis oduller. Odul aninda
    /// verilir (para, XP); burada sadece "sana bunu verdik" penceresi bekler.
    /// Mac icinde kazanilanlar menuye donunce sirayla gosterilir.
    /// </summary>
    public static class RewardInbox
    {
        private const string Key = "nb_reward_inbox";

        [Serializable]
        private class Store
        {
            public List<RewardNotice> items = new();
        }

        public static int Count => Load().items.Count;

        public static void Add(RewardNotice notice)
        {
            if (notice == null)
                return;

            Store store = Load();
            store.items.Add(notice);
            Save(store);
        }

        /// <summary>Siradaki bildirimi alir ve listeden cikarir. Yoksa null.</summary>
        public static RewardNotice TakeNext()
        {
            Store store = Load();
            if (store.items.Count == 0)
                return null;

            RewardNotice next = store.items[0];
            store.items.RemoveAt(0);
            Save(store);
            return next;
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }

        private static Store Load()
        {
            string json = PlayerPrefs.GetString(Key, string.Empty);
            if (string.IsNullOrEmpty(json))
                return new Store();

            try
            {
                return JsonUtility.FromJson<Store>(json) ?? new Store();
            }
            catch (ArgumentException)
            {
                return new Store();
            }
        }

        private static void Save(Store store)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(store));
            PlayerPrefs.Save();
        }
    }
}
