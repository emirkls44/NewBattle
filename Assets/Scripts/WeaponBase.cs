using Fusion;
using UnityEngine;

public abstract class WeaponBase : NetworkBehaviour, IWeapon
{
    [Header("Temel Silah Ayarlarý")]
    public float fireRate = 0.2f;
    public float weaponRange = 50f;
    public int maxAmmo = 90;
    public LayerMask hitLayerMask;

    // MÝMARÝ MÜDAHALE: Mermi deðiþimi að üzerinden Render döngüsünde dinlenir.
    [Networked, OnChangedRender(nameof(OnAmmoChanged))]
    public int currentAmmo { get; set; }

    [Networked] protected TickTimer nextFireTimer { get; set; }

    [Networked, OnChangedRender(nameof(OnWeaponVisibilityChanged))]
    public NetworkBool isVisible { get; set; }

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            currentAmmo = maxAmmo / 3;
        }
    }

    public void Shoot(Vector3 firePoint, Vector2 aimDirection)
    {
        if (!HasStateAuthority || currentAmmo <= 0 || !nextFireTimer.ExpiredOrNotRunning(Runner)) return;

        currentAmmo--;
        nextFireTimer = TickTimer.CreateFromSeconds(Runner, fireRate);

        ExecuteFire(firePoint, aimDirection);
        // HATA GÝDERÝLDÝ: UI çaðrýsý buradan silindi. Sunucu UI bilmez.
    }

    protected abstract void ExecuteFire(Vector3 firePoint, Vector2 aimDirection);

    public void AddAmmo(int amount)
    {
        if (!HasStateAuthority) return;
        currentAmmo = Mathf.Min(currentAmmo + amount, maxAmmo);
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

    // OBSERVER PATTERN: Deðiþim tüm istemcilere ulaþtýðýnda UI güncellenir.
    private void OnAmmoChanged()
    {
        // Sadece silahý tutan (Girdi yetkisi olan) ve silahý aktif olan kiþinin UI'ý güncellenir.
        if (HasInputAuthority && isVisible)
        {
            InventoryManager.Instance?.UpdateAmmoUI(currentAmmo);
        }
    }
}