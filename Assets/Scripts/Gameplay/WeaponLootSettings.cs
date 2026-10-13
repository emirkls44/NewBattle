using System;
using UnityEngine;

namespace NewBattle.Gameplay
{
    /// <summary>Silah nadirligi. Sayisal deger aga gider; sirayi degistirme.</summary>
    public enum WeaponRarity
    {
        Siradan = 0,
        SiraDisi = 1,
        Nadir = 2,
        Epik = 3,
        Efsanevi = 4
    }

    /// <summary>
    /// Silah nadirlikleri ve yerde cikan silahlarin olasiliklari.
    /// Assets/Resources/SilahAyarlari.asset — Inspector'dan ayarla (Kur olusturur).
    ///
    /// Nadirlik silahin hasarini carpar ve yerdeki parlamanin rengini belirler:
    /// gri Siradan, yesil Sira Disi, mavi Nadir, mor Epik, altin Efsanevi.
    /// </summary>
    [CreateAssetMenu(fileName = "SilahAyarlari", menuName = "NewBattle/Silah Ayarlari")]
    public class WeaponLootSettings : ScriptableObject
    {
        public const string ResourceName = "SilahAyarlari";

        [Serializable]
        public class RarityDef
        {
            public string name = "Sıradan";
            public Color color = Color.white;
            [Tooltip("Hasar carpani. 1.25 = %25 fazla hasar.")]
            [Min(0.1f)] public float damageMultiplier = 1f;
            [Tooltip("Yerde bu nadirligin cikma agirligi (digerlerine gore).")]
            [Min(0f)] public float weight = 1f;
        }

        [Serializable]
        public class WeaponDrop
        {
            [Tooltip("Sadece Inspector'da okumak icin.")]
            public string label = "Silah";
            [Tooltip("PlayerOnline > PlayerShooting > Weapons dizisindeki sira (0 yumruk, 1 tufek, 2 airdrop, 3 tabanca, 4 hafif makineli, 5 pompali, 6 keskin nisanci).")]
            [Min(1)] public int weaponId = 1;
            [Tooltip("Yerde bu silahin cikma agirligi (digerlerine gore).")]
            [Min(0f)] public float weight = 1f;
            public WeaponRarity minRarity = WeaponRarity.Siradan;
            public WeaponRarity maxRarity = WeaponRarity.Efsanevi;
        }

        [Header("Nadirlikler (sira: Sıradan, Sıra Dışı, Nadir, Epik, Efsanevi)")]
        public RarityDef[] rarities =
        {
            new() { name = "Sıradan", color = new Color(0.86f, 0.88f, 0.92f, 1f), damageMultiplier = 1f, weight = 45f },
            new() { name = "Sıra Dışı", color = new Color(0.38f, 0.86f, 0.3f, 1f), damageMultiplier = 1.08f, weight = 30f },
            new() { name = "Nadir", color = new Color(0.25f, 0.62f, 1f, 1f), damageMultiplier = 1.16f, weight = 15f },
            new() { name = "Epik", color = new Color(0.72f, 0.38f, 1f, 1f), damageMultiplier = 1.25f, weight = 8f },
            new() { name = "Efsanevi", color = new Color(1f, 0.74f, 0.16f, 1f), damageMultiplier = 1.35f, weight = 2f }
        };

        [Header("Yerde Cikan Silahlar")]
        public WeaponDrop[] groundWeapons =
        {
            new() { label = "Tabanca", weaponId = 3, weight = 30f, maxRarity = WeaponRarity.Epik },
            new() { label = "Tüfek", weaponId = 1, weight = 25f },
            new() { label = "Hafif Makineli", weaponId = 4, weight = 20f },
            new() { label = "Pompalı", weaponId = 5, weight = 15f },
            new() { label = "Keskin Nişancı", weaponId = 6, weight = 10f, minRarity = WeaponRarity.SiraDisi }
        };

        [Header("Cephane")]
        [Tooltip("Yerden alinan silahin icinden cikan mermi (silahin azami mermisinin orani).")]
        [Range(0.05f, 1f)] public float pickupAmmoFraction = 0.4f;
        [Tooltip("Mermi kutusunun verdigi mermi (silahin azami mermisinin orani).")]
        [Range(0.05f, 1f)] public float ammoPackFraction = 0.3f;

        [Header("Airdrop")]
        [Min(1)] public int airdropWeaponId = 2;
        public WeaponRarity airdropRarity = WeaponRarity.Efsanevi;

        [Header("Gorsel")]
        [Tooltip("Yerdeki silahin altindaki nadirlik parlamasinin opakligi.")]
        [Range(0f, 1f)] public float glowAlpha = 0.3f;

        private static WeaponLootSettings _instance;

        public static WeaponLootSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<WeaponLootSettings>(ResourceName);
                    if (_instance == null)
                        _instance = CreateInstance<WeaponLootSettings>(); // varsayilan degerlerle
                }

                return _instance;
            }
        }

        public RarityDef Rarity(int rarity)
        {
            if (rarities == null || rarities.Length == 0)
                return new RarityDef();

            return rarities[Mathf.Clamp(rarity, 0, rarities.Length - 1)];
        }

        public static string RarityName(int rarity) => Instance.Rarity(rarity).name;

        public static Color RarityColor(int rarity) => Instance.Rarity(rarity).color;

        public static float DamageMultiplier(int rarity) => Instance.Rarity(rarity).damageMultiplier;

        /// <summary>Yerde cikacak silah ve nadirligi (sadece host cagirir).</summary>
        public void RollGroundWeapon(out int weaponId, out int rarity)
        {
            WeaponDrop drop = PickWeighted(groundWeapons, d => d.weight);
            weaponId = drop != null ? drop.weaponId : 1;

            int min = drop != null ? (int)drop.minRarity : 0;
            int max = drop != null ? (int)drop.maxRarity : (int)WeaponRarity.Efsanevi;
            if (max < min)
                (min, max) = (max, min);

            float total = 0f;
            for (int i = min; i <= max && i < rarities.Length; i++)
                total += rarities[i].weight;

            rarity = min;
            if (total <= 0f)
                return;

            float roll = UnityEngine.Random.value * total;
            for (int i = min; i <= max && i < rarities.Length; i++)
            {
                roll -= rarities[i].weight;
                if (roll <= 0f)
                {
                    rarity = i;
                    return;
                }
            }

            rarity = Mathf.Min(max, rarities.Length - 1);
        }

        private static T PickWeighted<T>(T[] items, Func<T, float> weight) where T : class
        {
            if (items == null || items.Length == 0)
                return null;

            float total = 0f;
            foreach (T item in items)
                total += Mathf.Max(0f, weight(item));

            if (total <= 0f)
                return items[0];

            float roll = UnityEngine.Random.value * total;
            foreach (T item in items)
            {
                roll -= Mathf.Max(0f, weight(item));
                if (roll <= 0f)
                    return item;
            }

            return items[items.Length - 1];
        }
    }
}
