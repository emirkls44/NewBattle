using Fusion;
using UnityEngine;

public class AssaultRifle : WeaponBase
{
    [Header("Assault Rifle Ayarlarý")]
    public int damage = 15;

    // TracerPool referansý Inspector'dan atanmalý veya Object.FindFirstObjectByType ile bulunmalýdýr.
    public TracerPool tracerPool;

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
        if (tracerPool != null)
        {
            Vector3 direction = (hitPoint - firePoint).normalized;
            Quaternion rotation = direction != Vector3.zero ? Quaternion.LookRotation(direction) : Quaternion.identity;
            tracerPool.GetTracer(firePoint, rotation);
        }
    }
}