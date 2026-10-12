using System;
using UnityEngine;

namespace NewBattle.Gameplay
{
    /// <summary>Bir olum: oldurme akisi, oldurme afisi ve sayaclar dinler.</summary>
    public struct KillInfo
    {
        public string KillerName;
        public string VictimName;
        /// <summary>Gosterilecek silah adi ("Tüfek"); alan olumunde bos.</summary>
        public string Weapon;
        /// <summary>Guvenli alanin disinda kalarak oldu.</summary>
        public bool ByZone;

        public bool KillerIsLocal;
        public bool VictimIsLocal;
        public bool KillerIsTeammate;
        public bool VictimIsTeammate;

        /// <summary>Bu olumden sonra hayatta kalan oyuncu sayisi.</summary>
        public int AliveAfter;
        public Vector3 VictimPosition;
    }

    /// <summary>
    /// Olum bildirimleri. Sunucu her olumde tum cihazlara bir RPC gonderir
    /// (HealthController.Rpc_AnnounceDeath); bu sinif onu arayuze dagitir.
    /// </summary>
    public static class KillEvents
    {
        public static event Action<KillInfo> Killed;

        /// <summary>Bu cihazin oyuncusu hasar aldi (miktar).</summary>
        public static event Action<float> LocalDamaged;

        public static void Raise(KillInfo info) => Killed?.Invoke(info);

        public static void RaiseLocalDamaged(float damage) => LocalDamaged?.Invoke(damage);

        /// <summary>Ag etiketindeki ASCII silah adini Turkce gosterime cevirir.</summary>
        public static string WeaponDisplayName(string label)
        {
            if (string.IsNullOrEmpty(label))
                return string.Empty;

            return label switch
            {
                "Tufek" => "Tüfek",
                "Airdrop Tufegi" => "Airdrop Tüfeği",
                "Pompali" => "Pompalı",
                _ => label
            };
        }
    }
}
