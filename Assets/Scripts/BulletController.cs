using Fusion;
using UnityEngine;

public class BulletController : NetworkBehaviour
{
    [Header("Mermi Ayarlarý")]
    public int damage = 25;
    public float lifeTime = 3f;

    [Networked] private TickTimer LifeTimer { get; set; }

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            LifeTimer = TickTimer.CreateFromSeconds(Runner, lifeTime);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority && LifeTimer.Expired(Runner))
        {
            Runner.Despawn(Object);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!HasStateAuthority) return;

        // Optimizasyon: Katman (Layer) bazlý çarpýþma kullanmýyorsan tag kontrolleri 
        // hýzlý bir filtrelemedir ancak LayerMask kullanmak fizik motorunda çok daha ucuzdur.
        if (other.gameObject.CompareTag("Bush") || other.CompareTag("Player") || other.CompareTag("Bullet")) return;

        // MÝMARÝ DÜZELTME: Objenin ne olduðu önemsiz. Üzerinde HealthController varsa hasarý alýr.
        if (other.TryGetComponent<HealthController>(out var targetHealth))
        {
            targetHealth.TakeDamage(damage);
        }

        Runner.Despawn(Object);
    }
}