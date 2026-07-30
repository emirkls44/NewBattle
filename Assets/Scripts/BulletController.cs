using Fusion;
using UnityEngine;

// Performans: Bullet.cs ve BulletController.cs birleþtirildi. Að destekli ve GC dostu yapýldý.
public class BulletController : NetworkBehaviour
{
    [Header("Mermi Ayarlarý")]
    // DEÐÝÞÝKLÝK: CS1503 hatasýný önlemek ve mobil iþlemcideki float hesaplama maliyetini düþürmek için int yapýldý.
    public int damage = 25;
    public float lifeTime = 3f;

    // PERFORMANS: GC alloc yaratan Destroy(gameObject, time) yerine Fusion að zamanlayýcýsý.
    [Networked] private TickTimer LifeTimer { get; set; }

    public override void Spawned()
    {
        // Zamanlayýcýyý yalnýzca yetkili sunucu baþlatýr.
        if (HasStateAuthority)
        {
            LifeTimer = TickTimer.CreateFromSeconds(Runner, lifeTime);
        }
    }

    public override void FixedUpdateNetwork()
    {
        // Merminin ömrü dolduðunda Object Pool'a (Despawn) geri gönderilir.
        if (HasStateAuthority && LifeTimer.Expired(Runner))
        {
            Runner.Despawn(Object);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // GÜVENLÝK VE AÐ: Çarpýþma ve hasar hesabý sadece sunucuda (Host) yapýlýr.
        if (!HasStateAuthority) return;

        if (other.gameObject.CompareTag("Bush")) return;
        if (other.CompareTag("Player") || other.CompareTag("Bullet")) return;

        HealthController targetHealth = other.GetComponent<HealthController>();
        if (targetHealth != null)
        {
            targetHealth.TakeDamage(damage);
        }
        else
        {
            EnemyController dusman = other.GetComponent<EnemyController>();
            if (dusman != null)
            {
                // DEÐÝÞÝKLÝK: HasarAl yerine standartlaþtýrýlmýþ TakeDamage kullanýldý.
                dusman.TakeDamage(damage);
            }
        }

        // DEÐÝÞÝKLÝK: Çarpýþma sonrasý Destroy() yerine bellek dostu að havuzlama metodu.
        Runner.Despawn(Object);
    }
}