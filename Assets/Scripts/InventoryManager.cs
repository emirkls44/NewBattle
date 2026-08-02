using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("Slot UI")]
    public Image fistSlot;
    public Image weaponSlot;

    [Header("Ammo UI")]
    public TextMeshProUGUI ammoText;

    [Header("Colors")]
    public Color activeColor = new Color(1f, 1f, 1f, 1f);
    public Color inactiveColor = new Color(1f, 1f, 1f, 0.3f);
    public Color hiddenColor = new Color(1f, 1f, 1f, 0f);

    private bool _hasWeapon = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        fistSlot.color = activeColor;
        weaponSlot.color = hiddenColor;
        if (ammoText != null) ammoText.gameObject.SetActive(false);
    }

    public void OnWeaponPickedUp(int currentAmmo)
    {
        _hasWeapon = true;
        SwitchToWeapon(null);
        UpdateAmmoUI(currentAmmo);
    }

    public void UpdateAmmoUI(int currentAmmo)
    {
        if (ammoText != null)
        {
            ammoText.SetText("{0}", currentAmmo);
        }
    }

    public void SwitchToFist(PlayerShooting localPlayer)
    {
        fistSlot.color = activeColor;
        weaponSlot.color = _hasWeapon ? inactiveColor : hiddenColor;

        if (ammoText != null) ammoText.gameObject.SetActive(false);

        if (localPlayer != null) localPlayer.HolsterWeapon();
    }

    public void SwitchToWeapon(PlayerShooting localPlayer)
    {
        if (!_hasWeapon) return;

        fistSlot.color = inactiveColor;
        weaponSlot.color = activeColor;

        if (ammoText != null) ammoText.gameObject.SetActive(true);

        if (localPlayer != null) localPlayer.DrawWeapon();
    }
}