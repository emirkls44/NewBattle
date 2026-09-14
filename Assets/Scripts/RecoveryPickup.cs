using UnityEngine;
public enum RecoveryType { Health, Shield }
public class RecoveryPickup : TimedLootPickup
{
    [SerializeField] private RecoveryType recoveryType = RecoveryType.Health;
    [SerializeField, Min(1f)] private float amount = 25f;
    [SerializeField, Min(0f)] private float fullTolerance = 0.01f;
    protected override bool CanCollect(PlayerLoadout player)
    {
        var health = player.GetComponent<HealthController>();
        return recoveryType == RecoveryType.Health
            ? health.currentHealth < health.maxHealth - fullTolerance
            : health.currentShield < health.maxShield - fullTolerance;
    }
    protected override void GiveTo(PlayerLoadout player)
    {
        var health = player.GetComponent<HealthController>();
        if (recoveryType == RecoveryType.Health) health.Heal(amount);
        else health.AddShield(amount);
    }
}
