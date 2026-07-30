using Fusion;
using UnityEngine;

public class PlayerShooting : NetworkBehaviour
{
    [Header("Silah Görseli")]
    public GameObject silahObjesi; // DÜZELTME: eldekiSilah ve silahObjesi karmaþasý giderildi.

    [Header("Silah ve Efektler")]
    public Transform firePoint;
    public GameObject bulletPrefab;
    public GameObject muzzleFlash;

    [Header("Atýþ Ayarlarý")]
    public float bulletSpeed = 20f;
    public float fireRate = 0.2f;
    public float yumrukMenzili = 1.5f;

    [Header("Cephane (Ammo) Sistemi")]
    [Networked] public int currentAmmo { get; set; }
    [Networked] private TickTimer nextFireTimer { get; set; }
    [Networked] private NetworkBool isFiring { get; set; }

    // PERFORMANS VE AÐ: Sahnede objeleri sürekli aç/kapat yapmak yerine að üzerinden Zero-GC senkronizasyon.
    [Networked, OnChangedRender(nameof(OnWeaponVisibilityChanged))]
    public NetworkBool IsWeaponVisible { get; set; }

    private PlayerController playerController;
    private ChangeDetector _changeDetector;

    // PERFORMANS: Sýk çaðrýlan bileþenler Update/Metot içinden çýkarýlýp önbelleðe (Cache) alýndý.
    private Animator _animator;

    // PERFORMANS: Yumruk fiziði için her seferinde array oluþturmayý (GC) engelleyen bellek bloðu.
    private Collider[] _hitColliders = new Collider[10];

    public override void Spawned()
    {
        playerController = GetComponent<PlayerController>();
        _animator = GetComponentInChildren<Animator>(); // DÜZELTME: GC önlendi
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        if (HasStateAuthority && EnvanterSistemi.instance != null)
            EnvanterSistemi.instance.RegisterLocalPlayer(this);

        if (muzzleFlash != null) muzzleFlash.SetActive(false);
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput<NetworkInputData>(out var input))
        {
            if (input.RightJoystickVector.sqrMagnitude > 0.01f)
            {
                if (nextFireTimer.ExpiredOrNotRunning(Runner))
                {
                    if (playerController != null && playerController.hasWeapon && currentAmmo > 0)
                    {
                        Shoot(input.RightJoystickVector);
                        nextFireTimer = TickTimer.CreateFromSeconds(Runner, fireRate);
                    }
                    else if (playerController != null && !playerController.hasWeapon)
                    {
                        YumrukAt();
                        nextFireTimer = TickTimer.CreateFromSeconds(Runner, 0.5f);
                    }
                }
            }
            else
            {
                if (HasStateAuthority) isFiring = false;
            }
        }
    }

    void YumrukAt()
    {
        if (_animator != null) _animator.SetTrigger("Punch");

        Vector3 vurusNoktasi = transform.position + transform.forward * 1f;

        // DÜZELTME: OverlapSphere yerine GC oluþturmayan NonAlloc varyantýna geçildi.
        int hitCount = Physics.OverlapSphereNonAlloc(vurusNoktasi, yumrukMenzili, _hitColliders);

        for (int i = 0; i < hitCount; i++)
        {
            if (_hitColliders[i].CompareTag("Enemy"))
            {
                EnemyController dusman = _hitColliders[i].GetComponent<EnemyController>();
                // dusman?.TakeDamage(25); // EnemyController düzenlendiðinde burayý aktif et
            }
        }
    }

    void Shoot(Vector2 aimInput)
    {
        if (HasStateAuthority)
        {
            currentAmmo--;
            isFiring = true;
        }

        if (bulletPrefab != null && firePoint != null)
        {
            Vector3 shootDirection = new Vector3(aimInput.x, 0, aimInput.y).normalized;
            Quaternion shootRotation = Quaternion.LookRotation(shootDirection);

            Runner.Spawn(bulletPrefab, firePoint.position, shootRotation, Object.InputAuthority, (runner, spawnedBullet) =>
            {
                Rigidbody rb = spawnedBullet.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = shootDirection * bulletSpeed;
                }
            });
        }
    }

    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(isFiring):
                    if (muzzleFlash != null) muzzleFlash.SetActive(isFiring);
                    break;
            }
        }
    }

    public void AddAmmo(int amount = 30)
    {
        if (HasStateAuthority) currentAmmo += amount;
    }

    // DÜZELTME: Sýnýf yapýsý onarýldý, iç içe geçmiþ metotlar ayrýþtýrýldý.
    public void SilahiBelindeSakla()
    {
        if (HasStateAuthority)
        {
            IsWeaponVisible = false;
        }
    }

    public void SilahiElineAl()
    {
        if (HasStateAuthority)
        {
            IsWeaponVisible = true;
        }
    }

    private void OnWeaponVisibilityChanged()
    {
        if (silahObjesi != null)
        {
            silahObjesi.SetActive(IsWeaponVisible);
        }

        // MÝMARÝ: Animasyon ve Controller güncellemeleri yalnýzca render/durum deðiþtiðinde çalýþýr.
        if (_animator != null) _animator.SetBool("HasWeapon", IsWeaponVisible);
        if (playerController != null) playerController.hasWeapon = IsWeaponVisible;
    }
}