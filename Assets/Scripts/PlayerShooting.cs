using Fusion;
using UnityEngine;

[RequireComponent(typeof(PlayerController), typeof(PlayerLoadout))]
public class PlayerShooting : NetworkBehaviour
{
    [Header("Bilesenler")]
    public Transform firePoint;

    [Header("Yakin Dovus")]
    public float meleeRange = 1.5f;
    public float meleeForwardOffset = 1f;
    public float meleeCooldownSeconds = 0.5f;
    public float meleeDamage = 25f;
    public LayerMask meleeHitLayerMask = ~0;

    [Header("Tufek")]
    public float rifleRange = 35f;
    public float rifleFireInterval = 0.18f;
    public float rifleDamage = 15f;
    public LayerMask rifleHitLayerMask = ~0;

    private PlayerLoadout _loadout;
    private HealthController _ownHealth;
    private Animator _animator;
    private readonly Collider[] _meleeHits = new Collider[16];
    private readonly RaycastHit[] _rifleHits = new RaycastHit[32];

    private static readonly int PunchHash = Animator.StringToHash("Punch");

    [Networked] private TickTimer MeleeCooldown { get; set; }
    [Networked] private TickTimer RifleCooldown { get; set; }

    public override void Spawned()
    {
        _loadout = GetComponent<PlayerLoadout>();
        _ownHealth = GetComponent<HealthController>();
        _animator = GetComponentInChildren<Animator>();
    }

    public void ProcessShooting(Vector2 aimInput)
    {
        if (!HasStateAuthority || aimInput.sqrMagnitude <= 0.01f)
            return;

        if (_loadout != null && _loadout.IsRifleSelected)
        {
            TryFireRifle(aimInput);
            return;
        }

        if (MeleeCooldown.ExpiredOrNotRunning(Runner))
        {
            ExecuteMelee();
            MeleeCooldown = TickTimer.CreateFromSeconds(Runner, meleeCooldownSeconds);
        }
    }

    private void TryFireRifle(Vector2 aimInput)
    {
        if (!RifleCooldown.ExpiredOrNotRunning(Runner))
            return;

        if (!_loadout.TryConsumeRifleAmmo())
            return;

        RifleCooldown = TickTimer.CreateFromSeconds(Runner, rifleFireInterval);

        Vector3 direction = new Vector3(aimInput.x, 0f, aimInput.y).normalized;
        Vector3 origin = firePoint != null
            ? firePoint.position
            : transform.position + Vector3.up + direction * 0.6f;
        Vector3 endPoint = origin + direction * rifleRange;

        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction,
            _rifleHits,
            rifleRange,
            rifleHitLayerMask,
            QueryTriggerInteraction.Ignore
        );

        float nearestDistance = float.MaxValue;
        RaycastHit nearestHit = default;
        bool foundHit = false;

        for (int i = 0; i < hitCount; i++)
        {
            HealthController hitHealth = _rifleHits[i].collider.GetComponentInParent<HealthController>();
            if (hitHealth == _ownHealth)
                continue;

            if (_rifleHits[i].distance >= nearestDistance)
                continue;

            nearestDistance = _rifleHits[i].distance;
            nearestHit = _rifleHits[i];
            foundHit = true;
        }

        if (foundHit)
        {
            endPoint = nearestHit.point;
            HealthController targetHealth = nearestHit.collider.GetComponentInParent<HealthController>();
            if (targetHealth != null)
                targetHealth.TakeDamage(rifleDamage, Object.InputAuthority);
        }

        Rpc_ShowRifleShot(origin, endPoint);
    }

    private void ExecuteMelee()
    {
        Rpc_PlayMeleeAnimation();

        Vector3 hitCenter = transform.position + Vector3.up + transform.forward * meleeForwardOffset;
        int hitCount = Physics.OverlapSphereNonAlloc(
            hitCenter,
            meleeRange,
            _meleeHits,
            meleeHitLayerMask,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hitCount; i++)
        {
            HealthController targetHealth = _meleeHits[i].GetComponentInParent<HealthController>();
            if (targetHealth == null || targetHealth == _ownHealth)
                continue;

            targetHealth.TakeDamage(meleeDamage, Object.InputAuthority);
            break;
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_PlayMeleeAnimation()
    {
        if (_animator != null)
            _animator.SetTrigger(PunchHash);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_ShowRifleShot(Vector3 origin, Vector3 endPoint)
    {
        ShotTracer.Show(origin, endPoint);
    }

    // Eski InventoryManager arayuzu ile uyumluluk.
    public void HolsterWeapon()
    {
        _loadout?.SelectFist();
    }

    // Eski InventoryManager arayuzu ile uyumluluk.
    public void DrawWeapon()
    {
        _loadout?.SelectRifle();
    }
}
