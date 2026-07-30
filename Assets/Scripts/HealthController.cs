using Fusion;
using UnityEngine;
using TMPro;


public class HealthController : NetworkBehaviour // DEÐÝÞÝKLÝK: Senkronizasyon için MonoBehaviour yerine NetworkBehaviour kullanýldý.
{
    private GameManager cachedGameManager;

    [Header("Can Ayarlarý")]
    public float maxHealth = 100f;
    [Networked] public float currentHealth { get; set; } // DEÐÝÞÝKLÝK: Can deðiþkeni aða senkronize edildi.

    [Header("Kalkan (Zýrh) Ayarlarý")]
    public float maxShield = 100f;
    [Networked] public float currentShield { get; set; } // DEÐÝÞÝKLÝK: Kalkan deðiþkeni aða senkronize edildi.

    [Tooltip("Mermiler kalkana kaç kat daha fazla hasar versin?")]
    public float shieldDamageMultiplier = 2f;

    [Header("HUD (Ekran) Ayarlarý")]
    public RectTransform healthBarRect;
    public TextMeshProUGUI healthText;

    public RectTransform shieldBarRect;
    public TextMeshProUGUI shieldText;

    [Header("Düþman Ölüm Ayarý")]
    public bool isEnemy = false;
    public NetworkPrefabRef lootBoxPrefab; // DEÐÝÞÝKLÝK: GameObject yerine Fusion'ýn að obje referansý.

    private float _maxHealthBarWidth;
    private float _maxShieldBarWidth;
    private ChangeDetector _changeDetector;
    private GameManager _gameManager; // DEÐÝÞÝKLÝK: Dinamik arama yerine referans önbelleðe alýndý.

    void Awake()
    {
        if (healthBarRect != null) _maxHealthBarWidth = healthBarRect.sizeDelta.x;
        if (shieldBarRect != null) _maxShieldBarWidth = shieldBarRect.sizeDelta.x;
    }

    public override void Spawned()
    {
        cachedGameManager = FindFirstObjectByType<GameManager>();
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        // Önbellekleme iþlemi sadece obje doðduðunda yapýlýr.
        GameManager gameManager = FindFirstObjectByType<GameManager>();

        if (HasStateAuthority)
        {
            currentHealth = maxHealth;
            currentShield = 0f;
        }

        UpdateHealthUI();
    }

    public void TakeDamage(float baseDamage)
    {
        // DEÐÝÞÝKLÝK: Gömülü sistemlerdeki donaným kesmesi (interrupt) mantýðý gibi, hasar verisini sadece yetkili (host/sunucu) iþler.
        if (!HasStateAuthority) return;

        float remainingHealthDamage = baseDamage;

        if (currentShield > 0)
        {
            float shieldDamage = remainingHealthDamage * shieldDamageMultiplier;

            if (shieldDamage <= currentShield)
            {
                currentShield -= shieldDamage;
                remainingHealthDamage = 0;
            }
            else
            {
                float leftoverShieldDamage = shieldDamage - currentShield;
                currentShield = 0;
                remainingHealthDamage = leftoverShieldDamage / shieldDamageMultiplier;
            }
        }

        if (remainingHealthDamage > 0)
        {
            currentHealth -= remainingHealthDamage;
        }

        if (currentHealth < 0) currentHealth = 0;

        if (currentHealth <= 0) Die();
    }

    public void Heal(float healAmount)
    {
        if (!HasStateAuthority) return;
        currentHealth = Mathf.Min(currentHealth + healAmount, maxHealth); // DEÐÝÞÝKLÝK: if bloðu yerine daha hýzlý olan Mathf.Min kullanýldý.
    }

    public void AddShield(float shieldAmount)
    {
        if (!HasStateAuthority) return;
        currentShield = Mathf.Min(currentShield + shieldAmount, maxShield);
    }

    // DEÐÝÞÝKLÝK: Aða baðlý deðiþkenler deðiþtiðinde UI güncellemesini sadece Render içinde yaparak iþlemciyi rahatlatýyoruz.
    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(currentHealth):
                case nameof(currentShield):
                    UpdateHealthUI();
                    break;
            }
        }
    }

    void UpdateHealthUI()
    {
        if (isEnemy) return;

        if (healthBarRect != null && maxHealth > 0)
        {
            float healthPercent = currentHealth / maxHealth;
            healthBarRect.sizeDelta = new Vector2(_maxHealthBarWidth * healthPercent, healthBarRect.sizeDelta.y);
        }

        if (healthText != null)
        {
            // DEÐÝÞÝKLÝK: String birleþtirme (allocation) silindi[cite: 3]. TMP'nin GC-Free SetText metodu kullanýldý.
            healthText.SetText("{0} / {1}", Mathf.RoundToInt(currentHealth), maxHealth);
        }

        if (shieldBarRect != null && maxShield > 0)
        {
            float shieldPercent = currentShield / maxShield;
            shieldBarRect.sizeDelta = new Vector2(_maxShieldBarWidth * shieldPercent, shieldBarRect.sizeDelta.y);

            bool hasShield = currentShield > 0;
            if (shieldBarRect.parent.gameObject.activeSelf != hasShield)
                shieldBarRect.parent.gameObject.SetActive(hasShield);
        }

        if (shieldText != null)
        {
            // DEÐÝÞÝKLÝK: GC oluþturmamasý için SetText ile formatlandý.
            shieldText.SetText("{0} / {1}", Mathf.RoundToInt(currentShield), maxShield);

            bool hasShield = currentShield > 0;
            if (shieldText.gameObject.activeSelf != hasShield)
                shieldText.gameObject.SetActive(hasShield);
        }
    }

    void Die()
    {
        if (isEnemy)
        {
            // DEÐÝÞÝKLÝK: Instantiate[cite: 3] yerine Runner.Spawn kullanýlarak obje að üzerinde yaratýldý.
            if (lootBoxPrefab.IsValid)
                Runner.Spawn(lootBoxPrefab, transform.position + new Vector3(0, 0.5f, 0), Quaternion.identity, Object.StateAuthority);

            if (_gameManager != null)
            {
                cachedGameManager.OnEnemyDied();
            }
        }
        else
        {
            if (_gameManager != null)
            {
                cachedGameManager.gameEnded = true;
            }
        }

        // DEÐÝÞÝKLÝK: Destroy() veya gameObject.SetActive(false)[cite: 3] yerine objeyi Fusion sisteminden temizliyoruz.
        Runner.Despawn(Object);
    }
}