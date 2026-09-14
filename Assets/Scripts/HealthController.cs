using System;
using Fusion;
using UnityEngine;

public class HealthController : NetworkBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    [Networked] public float currentHealth { get; set; }

    [Header("Shield Settings")]
    public float maxShield = 100f;
    [Networked] public float currentShield { get; set; }
    [Range(0f, 1f)] public float maxShieldDamageReduction = 1f;
    [Min(0f)] public float shieldDrainMultiplier = 1f;

    public event Action OnDeath;
    public event Action OnHealthChanged;

    private ChangeDetector _changeDetector;

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        if (HasStateAuthority)
        {
            currentHealth = maxHealth;
            currentShield = 0f;
        }
    }

    public void TakeDamage(float baseDamage)
    {
        TakeDamage(baseDamage, PlayerRef.None);
    }

    public void TakeDamage(float baseDamage, PlayerRef attacker)
    {
        if (!HasStateAuthority || currentHealth <= 0f || baseDamage <= 0f)
            return;

        if (IsFriendlyFire(attacker))
            return;

        bool shieldWasHit = currentShield > 0f;
        float healthDamage = baseDamage;

        if (currentShield > 0f)
        {
            // Kalkan doluluk orani hasar azaltma oranini belirler.
            // 100 kalkan = tam azaltma, 50 kalkan = yari azaltma.
            float shieldRatio = maxShield > 0f
                ? Mathf.Clamp01(currentShield / maxShield)
                : 0f;
            float damageReduction = shieldRatio * maxShieldDamageReduction;

            healthDamage = baseDamage * (1f - damageReduction);
            currentShield = Mathf.Max(
                0f,
                currentShield - baseDamage * shieldDrainMultiplier
            );
        }

        if (healthDamage > 0f)
            currentHealth = Mathf.Max(0f, currentHealth - healthDamage);

        Rpc_ShowDamageNumber(baseDamage, shieldWasHit);

        if (currentHealth <= 0f)
        {
            AwardKill(attacker);
            Die();
        }
    }

    private bool IsFriendlyFire(PlayerRef attacker)
    {
        if (attacker == PlayerRef.None)
            return false;

        PlayerCombatStats ownStats = GetComponent<PlayerCombatStats>();
        if (ownStats == null)
            return false;

        PlayerCombatStats[] allStats = UnityEngine.Object.FindObjectsByType<PlayerCombatStats>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        foreach (PlayerCombatStats attackerStats in allStats)
        {
            if (attackerStats == null ||
                attackerStats.Object == null ||
                attackerStats.Object.InputAuthority != attacker)
                continue;

            return ownStats.IsTeammate(attackerStats);
        }

        return false;
    }

    private void AwardKill(PlayerRef attacker)
    {
        if (attacker == PlayerRef.None || (Object != null && Object.InputAuthority == attacker))
            return;

        PlayerCombatStats[] allStats = UnityEngine.Object.FindObjectsByType<PlayerCombatStats>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        foreach (PlayerCombatStats stats in allStats)
        {
            if (stats == null || stats.Object == null || stats.Object.InputAuthority != attacker)
                continue;

            stats.AddKill();
            return;
        }
    }

    public void Heal(float healAmount)
    {
        if (!HasStateAuthority || healAmount <= 0f)
            return;

        currentHealth = Mathf.Min(currentHealth + healAmount, maxHealth);
    }

    public void AddShield(float shieldAmount)
    {
        if (!HasStateAuthority || shieldAmount <= 0f)
            return;

        currentShield = Mathf.Min(currentShield + shieldAmount, maxShield);
    }

    public override void Render()
    {
        if (_changeDetector == null)
            return;

        foreach (var change in _changeDetector.DetectChanges(this))
        {
            if (change == nameof(currentHealth) || change == nameof(currentShield))
                OnHealthChanged?.Invoke();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_ShowDamageNumber(float damage, bool shieldHit)
    {
        DamageNumberPopup.Show(transform.position, damage, shieldHit);
    }

    private void Die()
    {
        OnDeath?.Invoke();
    }
}
