using System;
using System.Collections.Generic;
using UnityEngine;

namespace NewBattle.Progression
{
    [Serializable]
    public class MenuBackground
    {
        [Tooltip("Degistirme: oyuncularin satin aldiklari bu kimlikle kayitli.")]
        public string id = "yeni_arkaplan";
        public string displayName = "Yeni Arka Plan";
        [Tooltip("Ana menunun arkasinda gorunen resim (yatay; ekrani kaplayacak sekilde kirpilir).")]
        public Sprite image;
        [Tooltip("Oyun parasiyla fiyati. 0 = herkese bedava.")]
        [Min(0)] public int price = 500;
    }

    /// <summary>
    /// Magazada satilan ana menu arka planlari. Dosya: Assets/Resources/ArkaPlanKutuphanesi.
    /// Yeni resim eklemek icin listeye satir ekle, resmi ve fiyati sec; magazaya kendiliginden gelir.
    /// Varsayilan (bedava) arka plan sahnedeki sari arka plandir; oyuncu istedigi an ona doner.
    /// </summary>
    [CreateAssetMenu(menuName = "NewBattle/Arka Plan Kutuphanesi", fileName = "ArkaPlanKutuphanesi")]
    public class MenuBackgroundLibrary : ScriptableObject
    {
        public const string ResourcePath = "ArkaPlanKutuphanesi";
        private const string SelectedKey = "nb_menu_background";

        [Tooltip("Magazada varsayilan (sari) arka planin adi.")]
        public string defaultName = "Sarı (Varsayılan)";
        [Tooltip("Magazada varsayilan arka planin kart resmi.")]
        public Sprite defaultImage;

        public List<MenuBackground> backgrounds = new();

        /// <summary>Secili arka plan degisti (ana menu yeniden cizer).</summary>
        public static event Action Changed;

        private static MenuBackgroundLibrary _instance;

        public static MenuBackgroundLibrary Instance =>
            _instance != null ? _instance : _instance = Resources.Load<MenuBackgroundLibrary>(ResourcePath);

        public MenuBackground Find(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            foreach (MenuBackground background in backgrounds)
                if (background != null && background.id == id)
                    return background;
            return null;
        }

        /// <summary>Ana menude kullanilan arka plan; null = varsayilan sari.</summary>
        public MenuBackground Current
        {
            get
            {
                MenuBackground selected = Find(PlayerPrefs.GetString(SelectedKey, string.Empty));
                return selected != null && IsOwned(selected) ? selected : null;
            }
        }

        public bool IsOwned(MenuBackground background) =>
            background != null && (background.price <= 0 || PlayerProgress.Owns(OwnershipId(background)));

        public bool TryPurchase(MenuBackground background)
        {
            if (background == null || IsOwned(background) || !PlayerProgress.TrySpendCoins(background.price))
                return false;

            PlayerProgress.GrantOwnership(OwnershipId(background));
            return true;
        }

        /// <summary>Arka plani etkinlestirir; null verilirse varsayilan sariya doner.</summary>
        public void Select(MenuBackground background)
        {
            if (background != null && !IsOwned(background))
                return;

            PlayerPrefs.SetString(SelectedKey, background != null ? background.id : string.Empty);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        private static string OwnershipId(MenuBackground background) => "arkaplan_" + background.id;
    }
}
