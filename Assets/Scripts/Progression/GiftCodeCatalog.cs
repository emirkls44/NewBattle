using System;
using System.Collections.Generic;
using UnityEngine;

namespace NewBattle.Progression
{
    public enum GiftRewardType
    {
        [Tooltip("Karakter (Karakter Kutuphanesi'ndeki kimlik).")]
        Character,
        [Tooltip("Lobi animasyonu (Lobi Animasyonlari'ndaki kimlik).")]
        LobbyAnimation,
        Coins,
        Xp
    }

    [Serializable]
    public class GiftCode
    {
        [Tooltip("Oyuncunun yazacagi kod. Buyuk/kucuk harf, bosluk ve tire fark etmez.")]
        public string code = "YENIKOD";
        [Tooltip("Sadece senin icin not (kime / ne icin verdin).")]
        public string note;
        [Tooltip("Kapaliysa kod artik calismaz.")]
        public bool active = true;

        [Header("Odul")]
        public GiftRewardType reward = GiftRewardType.Character;
        [Tooltip("Karakter / animasyon kimligi (ornek: melek).")]
        public string itemId;
        [Tooltip("Para / XP miktari.")]
        [Min(0)] public int amount;
    }

    /// <summary>
    /// Hediye kodlari: senin belirledigin kodu Ayarlar > Hediye Kodu'na yazan
    /// herkes odulu alir. Her kod bir cihazda bir kez kullanilir.
    ///
    /// Kodlar: Assets/Resources/HediyeKodlari (Tools > NewBattle > Hediye Kodlari).
    /// </summary>
    [CreateAssetMenu(menuName = "NewBattle/Hediye Kodlari", fileName = "HediyeKodlari")]
    public class GiftCodeCatalog : ScriptableObject
    {
        public const string ResourceName = "HediyeKodlari";

        public List<GiftCode> codes = new();

        private static GiftCodeCatalog _instance;

        public static GiftCodeCatalog Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Resources.Load<GiftCodeCatalog>(ResourceName);
                return _instance;
            }
        }

        public GiftCode Find(string typed)
        {
            string key = GiftCodes.Normalize(typed);
            if (key.Length == 0)
                return null;

            foreach (GiftCode code in codes)
            {
                if (code != null && GiftCodes.Normalize(code.code) == key)
                    return code;
            }

            return null;
        }
    }
}
