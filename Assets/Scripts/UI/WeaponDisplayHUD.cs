using NewBattle.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// Minimap'in yanindaki yari saydam kutu: elindeki silahin (ya da yumrugun)
    /// resmi ve hemen altinda mermi sayisi. Mermi bitince silah gider, kutuda
    /// yumruk resmi gorunur. Kutunun cercevesi silahin nadirlik rengindedir.
    ///
    /// Resimler: icons[silah kimligi] (0 = yumruk). Kur, Assets/GameArt/HudIcons/Weapons
    /// altindaki weapon_&lt;id&gt;.png dosyalarini baglar (Tools/Icons/*.py uretir).
    /// </summary>
    public class WeaponDisplayHUD : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI ammoText;
        [Tooltip("Nadirlik rengine boyanan cerceve (bos olabilir).")]
        [SerializeField] private Image rarityFrame;

        [Tooltip("Sira = silah kimligi: 0 yumruk, 1 tufek, 2 airdrop, 3 tabanca, 4 hafif makineli, 5 pompali, 6 keskin nisanci.")]
        [SerializeField] private Sprite[] icons;

        [Header("Renkler")]
        [SerializeField] private Color ammoColor = Color.white;
        [SerializeField] private Color lowAmmoColor = new(1f, 0.45f, 0.4f, 1f);
        [Tooltip("Mermi azami mermisinin bu oraninin altindaysa kirmizi yazilir.")]
        [SerializeField, Range(0f, 1f)] private float lowAmmoFraction = 0.2f;
        [SerializeField] private Color fistFrameColor = new(1f, 1f, 1f, 0.35f);

        private PlayerLoadout _loadout;
        private float _nextSearchTime;
        private int _shownWeapon = -1;
        private int _shownAmmo = -1;
        private int _shownRarity = -1;
        private float _popTime = -10f;
        private Vector3 _iconBaseScale = Vector3.one;

        private void Awake()
        {
            if (icon != null)
                _iconBaseScale = icon.rectTransform.localScale;
        }

        private void Update()
        {
            if (!IsReady(_loadout) && Time.unscaledTime >= _nextSearchTime)
            {
                _loadout = FindLocalLoadout();
                _nextSearchTime = Time.unscaledTime + 0.25f;
            }

            bool ready = IsReady(_loadout);
            int weaponId = ready ? _loadout.WeaponId : PlayerLoadout.FistWeaponId;
            int ammo = ready ? _loadout.Ammo : 0;
            int rarity = ready ? _loadout.Rarity : 0;

            if (weaponId != _shownWeapon || rarity != _shownRarity)
            {
                if (_shownWeapon >= 0)
                    _popTime = Time.unscaledTime; // silah degisti: resim ziplasin
                _shownWeapon = weaponId;
                _shownRarity = rarity;
                ShowWeapon(weaponId, rarity);
                _shownAmmo = -1;
            }

            if (ammo != _shownAmmo)
            {
                _shownAmmo = ammo;
                ShowAmmo(weaponId, ammo, ready ? _loadout.MaxAmmo : 0);
            }

            AnimatePop();
        }

        private void ShowWeapon(int weaponId, int rarity)
        {
            if (icon != null)
            {
                Sprite sprite = icons != null && weaponId >= 0 && weaponId < icons.Length ? icons[weaponId] : null;
                if (sprite == null && icons != null && icons.Length > 0)
                    sprite = icons[0];

                icon.sprite = sprite;
                icon.enabled = sprite != null;
                icon.preserveAspect = true;
            }

            if (rarityFrame != null)
            {
                rarityFrame.color = weaponId == PlayerLoadout.FistWeaponId
                    ? fistFrameColor
                    : WeaponLootSettings.RarityColor(rarity);
            }
        }

        private void ShowAmmo(int weaponId, int ammo, int maxAmmo)
        {
            if (ammoText == null)
                return;

            bool fist = weaponId == PlayerLoadout.FistWeaponId;
            ammoText.gameObject.SetActive(!fist);
            if (fist)
                return;

            ammoText.text = ammo.ToString();
            ammoText.color = maxAmmo > 0 && ammo <= Mathf.CeilToInt(maxAmmo * lowAmmoFraction) ? lowAmmoColor : ammoColor;
        }

        private void AnimatePop()
        {
            if (icon == null)
                return;

            const float duration = 0.28f;
            float t = (Time.unscaledTime - _popTime) / duration;
            float bump = t is >= 0f and < 1f ? Mathf.Sin(t * Mathf.PI) * 0.18f : 0f;
            icon.rectTransform.localScale = _iconBaseScale * (1f + bump);
        }

        private static bool IsReady(PlayerLoadout loadout) =>
            loadout != null && loadout.Object != null && loadout.Object.IsValid;

        private static PlayerLoadout FindLocalLoadout()
        {
            foreach (PlayerLoadout candidate in FindObjectsByType<PlayerLoadout>(FindObjectsSortMode.None))
            {
                if (IsReady(candidate) && candidate.HasInputAuthority)
                    return candidate;
            }

            return null;
        }
    }
}
