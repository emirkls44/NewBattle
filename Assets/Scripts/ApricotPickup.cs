using Fusion;
using NewBattle.Gameplay;
using UnityEngine;

/// <summary>
/// Yerdeki kayisi. Gercek oyuncular toplar (botlar kullanamadigi icin almaz);
/// tasima sinirina (KayisiAyarlari > maxCarry) ulasan alamaz.
/// </summary>
public class ApricotPickup : TimedLootPickup
{
    [SerializeField] private Color apricotGlow = new(1f, 0.55f, 0.15f, 0.35f);

    protected override Color GlowColor => apricotGlow;

    protected override bool CanCollect(PlayerLoadout player)
    {
        return player != null && player.Object != null &&
               player.Object.InputAuthority != PlayerRef.None &&
               player.CanTakeApricot;
    }

    protected override void GiveTo(PlayerLoadout player)
    {
        ApricotSettings settings = ApricotSettings.Instance;
        player.AddApricots(settings != null ? settings.pickupAmount : 1);
    }
}
