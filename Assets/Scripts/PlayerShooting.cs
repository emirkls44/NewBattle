using Fusion;
using NewBattle.Gameplay;
using UnityEngine;

/// <summary>
/// Oyuncunun atisi. Silahin ozellikleri buradaki <see cref="weapons"/> dizisinden
/// okunur; PlayerLoadout sadece hangi indeksin secili oldugunu tutar.
///
/// GECIKME TELAFISI: Menzilli atis <see cref="NetworkRunner.LagCompensation"/>
/// uzerinden yapilir. Bu, sunucunun "atis ani"ndaki dunyayi degil, ATES EDEN
/// OYUNCUNUN O ANDA EKRANINDA GORDUGU dunyayi kullanmasini saglar. Duz
/// Physics.Raycast ile 100 ms pingde hareketli hedefin onunu nisanlamak
/// gerekiyordu; mobil bir nisancida bu oyunu "vurmuyor" hissettiren sey.
///
/// Calismasi icin oyuncu prefabinda HitboxRoot + Hitbox bulunmali
/// (Tools > NewBattle > Isabet Kutularini Kur).
/// </summary>
[RequireComponent(typeof(PlayerController), typeof(PlayerLoadout))]
public class PlayerShooting : NetworkBehaviour
{
    [Header("Bilesenler")]
    public Transform firePoint;

    /// <summary>
    /// Merminin cikis noktasi. Nisan cizgisi de buradan baslar: cizgi ile mermi
    /// ayni noktadan ayni yone gitmezse oyuncu nisan aldigi yeri vuramaz.
    /// </summary>
    public Transform FirePoint => firePoint;

    [Header("Silahlar")]
    [Tooltip("Sira aga giden kimliktir. Yeni silahlari HER ZAMAN sona ekle; " +
             "ortadan silmek kalanlarin kimligini kaydirir.")]
    [SerializeField] private WeaponDefinition[] weapons;

    [Header("Isabet Katmanlari")]
    public LayerMask meleeHitLayerMask = ~0;
    public LayerMask rifleHitLayerMask = ~0;

    [Header("Yakin Dovus")]
    [Tooltip("Yumruk kuresinin karakterin onunde ne kadar ileri kuruldugu.")]
    public float meleeForwardOffset = 1f;

    private PlayerLoadout _loadout;
    private HealthController _ownHealth;
    private Animator _animator;
    private CharacterAnimationDriver _animationDriver;

    /// <summary>Bu karede kamera zaten sarsildi mi (saccma tekrarini engeller).</summary>
    private int _lastShakeFrame = -1;

    /// <summary>Bu karede kovan zaten firlatildi mi.</summary>
    private int _lastCasingFrame = -1;
    private readonly Collider[] _meleeHits = new Collider[16];
    private readonly RaycastHit[] _rifleHits = new RaycastHit[32];

    private static readonly int PunchHash = Animator.StringToHash("Punch");

    [Networked] private TickTimer FireCooldown { get; set; }

    /// <summary>Dizideki silah tanimi. Gecersiz indekste null doner.</summary>
    public WeaponDefinition GetWeapon(int weaponId)
    {
        EnsureWeapons();
        return weaponId >= 0 && weaponId < weapons.Length ? weapons[weaponId] : null;
    }

    public int WeaponCount
    {
        get
        {
            EnsureWeapons();
            return weapons.Length;
        }
    }

    private void EnsureWeapons()
    {
        // Prefab guncellenmemis bir projede de oyun calissin.
        if (weapons == null || weapons.Length == 0)
            weapons = WeaponDefinition.CreateDefaultSet();
    }

    private void Awake()
    {
        EnsureWeapons();
    }

    public override void Spawned()
    {
        _loadout = GetComponent<PlayerLoadout>();
        _ownHealth = GetComponent<HealthController>();
        _animator = GetComponentInChildren<Animator>();
        _animationDriver = GetComponent<CharacterAnimationDriver>();
    }

    public void ProcessShooting(Vector2 aimInput)
    {
        if (!HasStateAuthority || aimInput.sqrMagnitude <= 0.01f)
            return;

        WeaponDefinition weapon = _loadout != null ? _loadout.CurrentWeapon : null;
        if (weapon == null)
            return;

        if (!FireCooldown.ExpiredOrNotRunning(Runner))
            return;

        if (!_loadout.TryConsumeAmmo())
            return;

        FireCooldown = TickTimer.CreateFromSeconds(Runner, weapon.fireInterval);

        if (weapon.isMelee)
            ExecuteMelee(weapon);
        else
            FireRanged(weapon, aimInput);
    }

    #region Menzilli

    private void FireRanged(WeaponDefinition weapon, Vector2 aimInput)
    {
        Vector3 baseDirection = new Vector3(aimInput.x, 0f, aimInput.y).normalized;
        Vector3 origin = firePoint != null
            ? firePoint.position
            : transform.position + Vector3.up + baseDirection * 0.6f;

        for (int pellet = 0; pellet < Mathf.Max(1, weapon.pellets); pellet++)
        {
            Vector3 direction = ApplySpread(baseDirection, weapon.spreadAngle);
            Vector3 endPoint = origin + direction * weapon.range;
            bool foundHit = false;

            if (TryHit(weapon, origin, direction, out Vector3 point, out HealthController target))
            {
                endPoint = point;
                foundHit = true;

                if (target != null)
                    target.TakeDamage(weapon.damage, Object.InputAuthority);
            }

            Rpc_ShowShot(origin, endPoint, foundHit, weapon.tracerColor);
        }
    }

    private Vector3 ApplySpread(Vector3 direction, float spreadAngle)
    {
        if (spreadAngle <= 0f)
            return direction;

        // Yayilma sunucuda uretiliyor: istemci kendi sapmasini secemez,
        // yoksa "hep tam isabet" eden bir hile yazmak serbest kalirdi.
        float angle = Random.Range(-spreadAngle, spreadAngle);
        return Quaternion.Euler(0f, angle, 0f) * direction;
    }

    /// <summary>
    /// Once gecikme telafili sorgu denenir. Sahnede isabet kutusu yoksa
    /// (prefab henuz guncellenmemisse) duz fizik sorgusuna duseriz.
    /// </summary>
    private bool TryHit(WeaponDefinition weapon, Vector3 origin, Vector3 direction,
        out Vector3 point, out HealthController target)
    {
        point = origin + direction * weapon.range;
        target = null;

        if (Runner.LagCompensation != null && Object.InputAuthority != PlayerRef.None)
        {
            bool compensated = Runner.LagCompensation.Raycast(
                origin,
                direction,
                weapon.range,
                Object.InputAuthority,
                out LagCompensatedHit hit,
                rifleHitLayerMask,
                HitOptions.IncludePhysX | HitOptions.SubtickAccuracy);

            if (compensated)
            {
                HealthController hitHealth = hit.Hitbox != null
                    ? hit.Hitbox.Root.GetComponentInParent<HealthController>()
                    : (hit.Collider != null ? hit.Collider.GetComponentInParent<HealthController>() : null);

                if (hitHealth == _ownHealth)
                    return false;

                point = hit.Point;
                target = hitHealth;
                return true;
            }

            return false;
        }

        return RawRaycast(weapon, origin, direction, out point, out target);
    }

    private bool RawRaycast(WeaponDefinition weapon, Vector3 origin, Vector3 direction,
        out Vector3 point, out HealthController target)
    {
        point = origin + direction * weapon.range;
        target = null;

        int hitCount = Physics.RaycastNonAlloc(
            origin, direction, _rifleHits, weapon.range,
            rifleHitLayerMask, QueryTriggerInteraction.Ignore);

        float nearestDistance = float.MaxValue;
        RaycastHit nearestHit = default;
        bool foundHit = false;

        for (int i = 0; i < hitCount; i++)
        {
            if (_rifleHits[i].collider.GetComponentInParent<HealthController>() == _ownHealth)
                continue;

            if (_rifleHits[i].distance >= nearestDistance)
                continue;

            nearestDistance = _rifleHits[i].distance;
            nearestHit = _rifleHits[i];
            foundHit = true;
        }

        if (!foundHit)
            return false;

        point = nearestHit.point;
        target = nearestHit.collider.GetComponentInParent<HealthController>();
        return true;
    }

    #endregion

    #region Yakin dovus

    private void ExecuteMelee(WeaponDefinition weapon)
    {
        Rpc_PlayMeleeAnimation();

        Vector3 hitCenter = transform.position + Vector3.up + transform.forward * meleeForwardOffset;
        int hitCount = Physics.OverlapSphereNonAlloc(
            hitCenter, weapon.range, _meleeHits,
            meleeHitLayerMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            HealthController targetHealth = _meleeHits[i].GetComponentInParent<HealthController>();
            if (targetHealth == null || targetHealth == _ownHealth)
                continue;

            targetHealth.TakeDamage(weapon.damage, Object.InputAuthority);
            break;
        }
    }

    #endregion

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_PlayMeleeAnimation()
    {
        if (_animator != null)
            _animator.SetTrigger(PunchHash);

        ShakeOwnCamera(0.12f);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_ShowShot(Vector3 origin, Vector3 endPoint, bool hitSomething, Color tracerColor)
    {
        ShotTracer.Show(origin, endPoint, hitSomething, tracerColor);

        WeaponDefinition weapon = _loadout != null ? _loadout.CurrentWeapon : null;
        ShakeOwnCamera(weapon != null ? weapon.shakeStrength : 0.15f);

        // Kovan HERKESTE gorunmeli - dusmanin ates ettigini kovanindan da
        // anlayabilmelisin. O yuzden sarsintinin aksine yetki kontrolu yok,
        // sadece saccma tekrarini engelleyen kare kontrolu var.
        if (_lastCasingFrame != Time.frameCount)
        {
            _lastCasingFrame = Time.frameCount;

            Vector3 aim = endPoint - origin;
            ShellCasingPool.Eject(origin, aim, transform.position.y);

            // Geri tepme de kare basina bir kez: saccmanin her tanesi govdeyi
            // ayri ayri itseydi pompali tek atista karakteri devirirdi.
            if (_animationDriver != null)
                _animationDriver.PlayFireKick();
        }
    }

    /// <summary>
    /// Sadece SILAHI TUTAN oyuncunun kendi kamerasini sarsar.
    ///
    /// Iki koruma var:
    ///   - HasInputAuthority: bu mermiyi ben attiysam sarsilirim. Olmasaydi
    ///     haritadaki herkesin her atisi benim ekranimi titretirdi.
    ///   - Kare kontrolu: saccma atan silahlar (pompali gibi) tek atista
    ///     birden fazla Rpc_ShowShot gonderiyor. Her biri sarsinti eklerse
    ///     tek bir pompali atisi ekrani okunmaz hale getirir.
    /// </summary>
    private void ShakeOwnCamera(float strength)
    {
        if (!HasInputAuthority || strength <= 0f)
            return;

        if (_lastShakeFrame == Time.frameCount)
            return;

        _lastShakeFrame = Time.frameCount;
        CameraFollow.Shake(strength);
    }
}
