using Fusion;
using UnityEngine;
using System;

public class HealthController : NetworkBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    [Networked] public float currentHealth { get; set; }

    [Header("Shield Settings")]
    public float maxShield = 100f;
    [Networked] public float currentShield { get; set; }
    public float shieldDamageMultiplier = 2f;

    // Arayüz ve diðer sistemleri haberdar etmek için Event yapýsý
    public event Action OnDeath;
    public event Action OnHealthChanged; // UI bu eventi dinleyecek

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
        if (!HasStateAuthority || currentHealth <= 0) return;

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
                remainingHealthDamage = (shieldDamage - currentShield) / shieldDamageMultiplier;
                currentShield = 0;
            }
        }

        if (remainingHealthDamage > 0)
        {
            currentHealth -= remainingHealthDamage;
        }

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
    }

    public void Heal(float healAmount)
    {
        if (!HasStateAuthority) return;
        currentHealth = Mathf.Min(currentHealth + healAmount, maxHealth);
    }

    public void AddShield(float shieldAmount)
    {
        if (!HasStateAuthority) return;
        currentShield = Mathf.Min(currentShield + shieldAmount, maxShield);
    }

    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(currentHealth):
                case nameof(currentShield):
                    OnHealthChanged?.Invoke(); // Veri deðiþtiðinde sinyal gönder
                    break;
            }
        }
    }

    private void Die()
    {
        OnDeath?.Invoke(); // Ölüm sinyali gönder
        // NOT: Runner.Despawn(Object) iþlemi bu eventi dinleyen ana kontrolcü (Örn: EnemyController) tarafýndan yapýlmalýdýr.
    }
}