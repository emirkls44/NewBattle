using Fusion;
using NewBattle.Gameplay;
using UnityEngine;

/// <summary>
/// Yerdeki kayisi. Gercek oyuncular toplar (botlar kullanamadigi icin almaz);
/// tasima sinirina (KayisiAyarlari > maxCarry) ulasan alamaz.
/// </summary>
public class ApricotPickup : TimedLootPickup
{
    [Tooltip("Kayisi (iyilestirme) ya da findik (yavaslatma).")]
    [SerializeField] private ThrowableKind kind = ThrowableKind.Apricot;
    [SerializeField] private Color apricotGlow = new(1f, 0.55f, 0.15f, 0.35f);

    protected override Color GlowColor => apricotGlow;

    protected override bool CanCollect(PlayerLoadout player)
    {
        return player != null && player.Object != null &&
               player.Object.InputAuthority != PlayerRef.None &&
               player.CanTake(kind);
    }

    protected override void GiveTo(PlayerLoadout player)
    {
        ApricotSettings settings = ApricotSettings.Get(kind);
        player.AddThrowable(kind, settings != null ? settings.pickupAmount : 1);
    }
}
