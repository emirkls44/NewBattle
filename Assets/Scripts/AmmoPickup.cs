using UnityEngine;
public class AmmoPickup : TimedLootPickup
{
    [SerializeField, Min(1)] private int ammoAmount = 20;
    protected override bool CanCollect(PlayerLoadout player) => player.HasRifle && player.RifleAmmo < player.MaxRifleAmmo;
    protected override void GiveTo(PlayerLoadout player) { player.AddRifleAmmo(ammoAmount); }
}
