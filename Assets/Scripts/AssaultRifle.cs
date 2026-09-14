using Fusion;
using UnityEngine;

public class AssaultRifle : WeaponBase
{
    [Header("Assault Rifle Ayarlarý")]
    public int damage = 15;

    private TracerPool _tracerPool;

    public override void Spawned()
    {
        base.Spawned();
        // Mimari Düzeltme: Prefab referans kopmasýný önlemek için dinamik arama
        _tracerPool = UnityEngine.Object.FindFirstObjectByType<TracerPool>();
    }

    protected override void ExecuteFire(Vector3 firePoint, Vector2 aimDirection)
    {
        Vector3 shootDirection = new Vector3(aimDirection.x, 0, aimDirection.y).normalized;
        Vector3 targetPosition = firePoint + (shootDirection * weaponRange);

        bool hit = Runner.LagCompensation.Raycast(
            firePoint,
            shootDirection,
            weaponRange,
            Object.InputAuthority,
            out var hitInfo,
            hitLayerMask,
            HitOptions.IncludePhysX
        );

        if (hit && hitInfo.Hitbox != null)
        {
            targetPosition = hitInfo.Point;

            if (hitInfo.Hitbox.TryGetComponent<HealthController>(out var targetHealth))
            {
                targetHealth.TakeDamage(damage);
            }
        }

        Rpc_PlayShootEffects(targetPosition, firePoint);
    }

    [Rpc(RpcSources.InputAuthority | RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_PlayShootEffects(Vector3 hitPoint, Vector3 firePoint)
    {
        if (_tracerPool != null)
        {
            Vector3 direction = (hitPoint - firePoint).normalized;
            Quaternion rotation = direction != Vector3.zero ? Quaternion.LookRotation(direction) : Quaternion.identity;
            _tracerPool.GetTracer(firePoint, rotation);
        }
    }
}