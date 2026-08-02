using Fusion;
using UnityEngine;

public abstract class WeaponBase : NetworkBehaviour, IWeapon
{
    [Header("Temel Silah Ayarlarý")]
    public float fireRate = 0.2f;
    public float weaponRange = 50f;
    public int maxAmmo = 90;
    public LayerMask hitLayerMask;

    [Networked] public int currentAmmo { get; set; }
    [Networked] protected TickTimer nextFireTimer { get; set; }
    [Networked, OnChangedRender(nameof(OnWeaponVisibilityChanged))]
    public NetworkBool isVisible { get; set; }

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            currentAmmo = maxAmmo / 3; // Baþlangýç mermisi
        }
    }

    // Ortak atýþ kontrolü: Mermi var mý? Süre doldu mu?
    public void Shoot(Vector3 firePoint, Vector2 aimDirection)
    {
        if (!HasStateAuthority || currentAmmo <= 0 || !nextFireTimer.ExpiredOrNotRunning(Runner)) return;

        currentAmmo--;
        nextFireTimer = TickTimer.CreateFromSeconds(Runner, fireRate);

        // Polimorfizm: Asýl atýþ mantýðý alt sýnýflarda iþlenir
        ExecuteFire(firePoint, aimDirection);

        InventoryManager.Instance?.UpdateAmmoUI(currentAmmo);
    }

    // Alt sýnýflarýn (Taarruz, Pompalý vb.) ezmek (override) zorunda olduðu metot
    protected abstract void ExecuteFire(Vector3 firePoint, Vector2 aimDirection);

    public void AddAmmo(int amount)
    {
        if (!HasStateAuthority) return;
        currentAmmo = Mathf.Min(currentAmmo + amount, maxAmmo);
        InventoryManager.Instance?.UpdateAmmoUI(currentAmmo);
    }

    public int GetCurrentAmmo() => currentAmmo;
    public bool IsWeaponVisible() => isVisible;

    public void SetWeaponVisibility(bool visibility)
    {
        if (HasStateAuthority) isVisible = visibility;
    }

    private void OnWeaponVisibilityChanged()
    {
        gameObject.SetActive(isVisible);
    }
}