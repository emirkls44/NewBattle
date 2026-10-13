using Fusion;
using NewBattle.Gameplay;
using UnityEngine;

/// <summary>
/// Yerdeki silah. Hangi silah oldugu ve nadirligi agda tasinir; host spawn'da
/// SilahAyarlari'ndaki olasiliklara gore secer. Model silaha gore degisir,
/// namlusu yukari dik durur ve LootBob ile kendi etrafinda yavasca doner;
/// altindaki parlama nadirlik rengindedir.
///
/// Halkasi dolunca oyuncunun elindeki silah yok olur, bu silah kendi mermisiyle
/// gelir (PlayerLoadout.GrantWeapon).
///
/// Adi eski (tek silah varken "tufek" idi); prefab ve sahne referanslari
/// bozulmasin diye sinif adi korunuyor.
/// </summary>
public class RiflePickup : TimedLootPickup
{
    [Tooltip("Silah tanimi bulunamazsa verilecek mermi; normalde SilahAyarlari > Pickup Ammo Fraction kullanilir.")]
    [SerializeField] private int ammoAmount = 30;

    [Header("Yerdeki Duruş")]
    [Tooltip("0 = silah dik durur (kabzasi asagida, yandan gorunur) ve kendi etrafinda doner. " +
             "Arttikca namlu yukari kalkar; 90 = namlu tam yukari (ustten bakan kamerada ince gorunur).")]
    [SerializeField, Range(0f, 90f)] private float standAngle = 0f;
    [Tooltip("Silahin en alt noktasi zeminden bu kadar yukarida durur.")]
    [SerializeField, Min(0f)] private float groundClearance = 0.12f;

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

    /// <summary>Bot yapay zekasi: bu silah elindekinden iyi mi.</summary>
    public bool IsUpgradeFor(PlayerLoadout player) =>
        WeaponId > PlayerLoadout.FistWeaponId && player != null && player.IsUpgrade(WeaponId, Rarity);

    protected override void GiveTo(PlayerLoadout player)
    {
        player.GrantWeapon(WeaponId, Rarity, AmmoFor(player));
    }

    private int AmmoFor(PlayerLoadout player)
    {
        if (StoredAmmo >= 0)
            return StoredAmmo;

        WeaponDefinition weapon = player.GetComponent<PlayerShooting>()?.GetWeapon(WeaponId);
        if (weapon == null)
            return ammoAmount;

        return Mathf.Max(1, Mathf.CeilToInt(weapon.maxAmmo * WeaponLootSettings.Instance.pickupAmmoFraction));
    }

    /// <summary>Yerdeki modeli silahin modeliyle degistirir ve namlusu yukari dik durdurur.</summary>
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

        StandUpright(_modelFilter.transform, _modelFilter.sharedMesh);
    }

    /// <summary>
    /// Silah modellerinde namlu +Z yonunde. Model, namlu yukari bakacak sekilde
    /// (standAngle) cevrilir ve merkezi dondurme eksenine (ustteki LootBob) oturtulur;
    /// boylece silah kendi etrafinda doner. Uzun silahlar yere gomulmesin diye
    /// en alt nokta zeminden groundClearance kadar yukarida tutulur.
    /// </summary>
    private void StandUpright(Transform model, Mesh mesh)
    {
        if (model == null || mesh == null || model.parent == null)
            return;

        Quaternion rotation = Quaternion.Euler(-standAngle, 0f, 0f);
        model.localRotation = rotation;

        Vector3 scale = model.localScale;
        Bounds bounds = mesh.bounds;
        Vector3 center = rotation * Vector3.Scale(bounds.center, scale);
        Vector3 rotatedExtents = RotatedExtents(rotation, Vector3.Scale(bounds.extents, scale));

        // Model merkezi dondurme ekseninde (x, z = 0).
        Vector3 position = -center;

        // Zemin: pickup kokunun RingGroundOffset kadar altinda (kok olcegi 1).
        Transform pivot = model.parent;
        float pivotHeight = pivot.position.y - transform.position.y;
        float groundLocal = RingGroundOffset - pivotHeight;
        float bottom = position.y - rotatedExtents.y;
        float minBottom = groundLocal + groundClearance;
        if (bottom < minBottom)
            position.y += minBottom - bottom;

        model.localPosition = position;
    }

    private static Vector3 RotatedExtents(Quaternion rotation, Vector3 extents)
    {
        Matrix4x4 m = Matrix4x4.Rotate(rotation);
        return new Vector3(
            Mathf.Abs(m.m00) * extents.x + Mathf.Abs(m.m01) * extents.y + Mathf.Abs(m.m02) * extents.z,
            Mathf.Abs(m.m10) * extents.x + Mathf.Abs(m.m11) * extents.y + Mathf.Abs(m.m12) * extents.z,
            Mathf.Abs(m.m20) * extents.x + Mathf.Abs(m.m21) * extents.y + Mathf.Abs(m.m22) * extents.z);
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
