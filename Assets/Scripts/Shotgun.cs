using Fusion;
using UnityEngine;

public class Shotgun : WeaponBase
{
    [Header("Shotgun Ayarlarý")]
    public int damagePerPellet = 8;
    public int pelletCount = 5;
    public float spreadAngle = 15f;

    public TracerPool tracerPool;

    protected override void ExecuteFire(Vector3 firePoint, Vector2 aimDirection)
    {
        Vector3 baseDirection = new Vector3(aimDirection.x, 0, aimDirection.y).normalized;

        for (int i = 0; i < pelletCount; i++)
        {
            // Saçmalar için rastgele yayýlým açýsý hesaplama
            float randomAngle = Random.Range(-spreadAngle, spreadAngle);
            Quaternion spreadRotation = Quaternion.Euler(0, randomAngle, 0);
            Vector3 shootDirection = spreadRotation * baseDirection;

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
                    targetHealth.TakeDamage(damagePerPellet);
                }
            }

            Rpc_PlayShootEffects(targetPosition, firePoint);
        }
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