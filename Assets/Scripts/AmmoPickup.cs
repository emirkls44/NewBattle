using UnityEngine;

/// <summary>
/// Mermi kutusu: secili silaha (doluysa diger silaha) azami mermisinin
/// SilahAyarlari > Ammo Pack Fraction orani kadar mermi verir. Boylece pompaliya
/// da hafif makineliye de "ayni oranda" dolum gelir.
/// </summary>
public class AmmoPickup : TimedLootPickup
{
    protected override bool CanCollect(PlayerLoadout player) => player.CanTakeAmmo;

    protected override void GiveTo(PlayerLoadout player) =>
        player.AddAmmoPack(NewBattle.Gameplay.WeaponLootSettings.Instance.ammoPackFraction);
}
