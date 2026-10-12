using Fusion;
using NewBattle.Gameplay;
using UnityEngine;

/// <summary>
/// Yerdeki silah. Hangi silah oldugu ve nadirligi agda tasinir; host spawn'da
/// SilahAyarlari'ndaki olasiliklara gore secer (ya da dusurulen silah icin
/// Configure ile verilir). Model silaha gore degisir, altindaki parlama
/// nadirlik rengindedir.
///
/// Adi eski (tek silah varken "tufek" idi); prefab ve sahne referanslari
/// bozulmasin diye sinif adi korunuyor.
/// </summary>
public class RiflePickup : TimedLootPickup
{
    [Tooltip("Silah tanimi bulunamazsa verilecek mermi; normalde SilahAyarlari > Pickup Ammo Fraction kullanilir.")]
    [SerializeField] private int ammoAmount = 30;

    /// <summary>Silahin PlayerShooting dizisindeki indeksi (0 = henuz secilmedi).</summary>
    [Networked] public int WeaponId { get; private set; }

    [Networked] public int Rarity { get; private set; }

    /// <summary>Icindeki mermi; -1 = ayarlardaki orana gore.</summary>
    [Networked] private int StoredAmmo { get; set; }

    private MeshFilter _modelFilter;
    private int _shownWeaponId = -1;

    private void Awake()
    {
        // Halka/parlama nesneleri Spawned'da eklenir; model ondan once bulunmali.
        _modelFilter = GetComponentInChildren<MeshFilter>(true);
    }

    /// <summary>Host: spawn'dan once (onBeforeSpawned) ya da hemen sonra cagirir.</summary>
    public void Configure(int weaponId, int rarity, int ammo)
    {
        WeaponId = weaponId;
        Rarity = rarity;
        StoredAmmo = ammo;
    }

    public override void Spawned()
    {
        base.Spawned();

        if (HasStateAuthority && WeaponId <= PlayerLoadout.FistWeaponId)
        {
            WeaponLootSettings.Instance.RollGroundWeapon(out int weaponId, out int rarity);
            Configure(weaponId, rarity, -1);
        }

        ApplyModel();
    }

    public override void Render()
    {
        base.Render();
        ApplyModel();
    }

    protected override Color GlowColor
    {
        get
        {
            Color color = WeaponLootSettings.RarityColor(Rarity);
            color.a = WeaponLootSettings.Instance.glowAlpha;
            return color;
        }
    }

    protected override bool CanCollect(PlayerLoadout player) =>
        WeaponId > PlayerLoadout.FistWeaponId && player.CanAcceptWeapon(WeaponId, Rarity);

    /// <summary>Bot yapay zekasi: bu silah ona yarar mi (sadece mermi icin degil).</summary>
    public bool IsUpgradeFor(PlayerLoadout player) =>
        WeaponId > PlayerLoadout.FistWeaponId && player != null && player.IsUpgrade(WeaponId, Rarity);

    protected override void GiveTo(PlayerLoadout player)
    {
        player.GrantWeapon(WeaponId, Rarity, AmmoFor(player, WeaponId, StoredAmmo),
            out int droppedId, out int droppedRarity, out int droppedAmmo);

        // Iki yuva doluydu: eldeki silah bu esyanin yerine duser.
        if (droppedId > PlayerLoadout.FistWeaponId)
            SpawnDropped(Runner, transform.position, droppedId, droppedRarity, droppedAmmo);
    }

    private int AmmoFor(PlayerLoadout player, int weaponId, int stored)
    {
        if (stored >= 0)
            return stored;

        WeaponDefinition weapon = player.GetComponent<PlayerShooting>()?.GetWeapon(weaponId);
        if (weapon == null)
            return ammoAmount;

        return Mathf.Max(1, Mathf.CeilToInt(weapon.maxAmmo * WeaponLootSettings.Instance.pickupAmmoFraction));
    }

    /// <summary>Elden cikan silahi yere birakir (host). Airdrop sandigi da kullanir.</summary>
    public static void SpawnDropped(NetworkRunner runner, Vector3 position, int weaponId, int rarity, int ammo)
    {
        if (runner == null || !runner.IsServer)
            return;

        LootSpawner spawner = runner.GetComponent<LootSpawner>();
        if (spawner == null || !spawner.RiflePickupPrefab.IsValid)
            return;

        runner.Spawn(spawner.RiflePickupPrefab, position, Quaternion.identity, null,
            (_, spawned) =>
            {
                if (spawned.TryGetComponent(out RiflePickup pickup))
                    pickup.Configure(weaponId, rarity, ammo);
            });
    }

    /// <summary>Yerdeki modeli silahin elde tutulan modeliyle degistirir.</summary>
    private void ApplyModel()
    {
        if (_modelFilter == null || WeaponId == _shownWeaponId || WeaponId <= PlayerLoadout.FistWeaponId)
            return;

        WeaponDefinition weapon = FindWeaponDefinition(WeaponId);
        if (weapon == null)
            return; // oyuncu henuz yok; sonraki karede tekrar dene

        _shownWeaponId = WeaponId;
        if (weapon.heldMesh != null)
            _modelFilter.sharedMesh = weapon.heldMesh;
    }

    private static PlayerShooting _definitionSource;

    /// <summary>Silah tanimlari oyuncu prefabinda; sahnedeki herhangi bir oyuncudan okunur.</summary>
    private static WeaponDefinition FindWeaponDefinition(int weaponId)
    {
        if (_definitionSource == null)
            _definitionSource = FindFirstObjectByType<PlayerShooting>();
        return _definitionSource != null ? _definitionSource.GetWeapon(weaponId) : null;
    }
}
