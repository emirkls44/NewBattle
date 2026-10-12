using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace NewBattle.Progression
{
    public enum ShopItemKind
    {
        [Tooltip("Lobi animasyonu; fiyati animasyon kutuphanesindeki 'Coins' sartidir.")]
        LobbyAnimation,
        [Tooltip("Reklam izleyerek ucretsiz para (gunluk sinirli).")]
        WatchAdCoins,
        [Tooltip("Gercek parayla para paketi (odeme sistemi gelene kadar YAKINDA).")]
        CoinPack
    }

    [Serializable]
    public class ShopItem
    {
        public string id = "yeni_urun";
        public ShopItemKind kind = ShopItemKind.LobbyAnimation;
        public string displayName = "Yeni Ürün";
        [Tooltip("Bossa ture gore varsayilan ikon kullanilir.")]
        public Sprite icon;

        [Tooltip("LobbyAnimation: kutuphanedeki animasyonun kimligi.")]
        public string animationId;

        [Tooltip("WatchAdCoins / CoinPack: verilecek para.")]
        [Min(0)] public int coins;

        [Tooltip("CoinPack: kartta yazan fiyat (ornek: 29,99 TL).")]
        public string priceLabel;

        [Tooltip("WatchAdCoins: gunde en fazla kac kez.")]
        [Min(1)] public int dailyLimit = 5;
    }

    /// <summary>
    /// Magazada satilanlar. Dosya: Assets/GameArt/Shop/MagazaUrunleri.asset
    /// (Menü > Kur olusturur). Sira, magazadaki siradir; Inspector'dan urun
    /// ekleyip cikarabilir, fiyat ve adlari degistirebilirsin.
    /// </summary>
    [CreateAssetMenu(menuName = "NewBattle/Magaza Urunleri", fileName = "MagazaUrunleri")]
    public class ShopCatalog : ScriptableObject
    {
        [Tooltip("Isaretliyse kutuphanedeki TUM lobi animasyonlari (listede olmayanlar) magazanin basina eklenir. " +
                 "Parayla acilanlar satin alinir; seviye/galibiyetle acilanlar sartini gosterir.")]
        [FormerlySerializedAs("autoAddCoinAnimations")]
        public bool autoAddAnimations = true;

        public List<ShopItem> items = new()
        {
            new ShopItem { id = "reklam_para", kind = ShopItemKind.WatchAdCoins, displayName = "Bedava Para", coins = 50, dailyLimit = 5 },
            new ShopItem { id = "paket_kucuk", kind = ShopItemKind.CoinPack, displayName = "Bir Avuç Para", coins = 500, priceLabel = "29,99 TL" },
            new ShopItem { id = "paket_orta", kind = ShopItemKind.CoinPack, displayName = "Para Kesesi", coins = 1200, priceLabel = "59,99 TL" },
            new ShopItem { id = "paket_buyuk", kind = ShopItemKind.CoinPack, displayName = "Para Sandığı", coins = 3000, priceLabel = "129,99 TL" }
        };

        /// <summary>Magazada gosterilecek tum urunler (otomatik eklenen animasyonlar basta).</summary>
        public List<ShopItem> BuildList(LobbyAnimationLibrary library)
        {
            var result = new List<ShopItem>();
            var listed = new HashSet<string>();

            foreach (ShopItem item in items)
            {
                if (item != null && item.kind == ShopItemKind.LobbyAnimation && !string.IsNullOrEmpty(item.animationId))
                    listed.Add(item.animationId);
            }

            if (autoAddAnimations && library != null)
            {
                foreach (LobbyAnimation animation in library.Animations)
                {
                    if (animation == null || listed.Contains(animation.id))
                        continue;

                    result.Add(new ShopItem
                    {
                        id = "anim_" + animation.id,
                        kind = ShopItemKind.LobbyAnimation,
                        displayName = animation.displayName,
                        icon = animation.icon,
                        animationId = animation.id
                    });
                }
            }

            foreach (ShopItem item in items)
            {
                if (item != null)
                    result.Add(item);
            }

            return result;
        }
    }
}
