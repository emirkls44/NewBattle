using UnityEngine;
public class RiflePickup : TimedLootPickup
{
    [SerializeField] private int ammoAmount = 30;
    protected override bool CanCollect(PlayerLoadout player) => !player.HasRifle || player.RifleAmmo < player.MaxRifleAmmo;
    protected override void GiveTo(PlayerLoadout player) { player.GrantRifle(ammoAmount); }
}
