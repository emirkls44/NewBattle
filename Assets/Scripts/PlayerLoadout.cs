using Fusion;
using NewBattle.Gameplay;
using UnityEngine;

/// <summary>
/// Oyuncunun iki silah yuvasi ve cephanesi.
///
/// Ag uzerinde her yuva icin uc sayi tasinir: silah kimligi (PlayerShooting'deki
/// dizinin indeksi; 0 = bos), nadirlik ve mermi. Ayrica secili yuva ve o an
/// kullanilan silah (WeaponId). Hasar, atis hizi gibi her sey tanimdan okunur.
///
/// Kurallar:
///   - Bos yuva varsa yerdeki silah oraya girer (elde yumruk varsa ona gecilir).
///   - Ayni silahtan daha nadiri alinirsa yuva yukselir; degilse sadece mermisi alinir.
///   - Iki yuva doluysa yalnizca ELDEKINDEN guclu bir silah alinir; eldeki yere duser.
///   - Mermi bitince diger silaha, o da bossa yumruga gecilir; mermi gelince geri doner.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerLoadout : NetworkBehaviour
{
    /// <summary>Yumruk her zaman 0 numaradir; silahsiz oyuncunun varsayilan hali.</summary>
    public const int FistWeaponId = 0;

    public const int SlotCount = 2;

    [Header("Gorsel")]
    [SerializeField] private GameObject rifleVisual;

    /// <summary>O an kullanilan silahin PlayerShooting dizisindeki indeksi (yumruk dahil).</summary>
    [Networked] public int WeaponId { get; private set; }

    /// <summary>Secili yuva (0 ya da 1).</summary>
    [Networked] public int ActiveSlot { get; private set; }

    [Networked, Capacity(SlotCount)] private NetworkArray<int> SlotWeapons => default;
    [Networked, Capacity(SlotCount)] private NetworkArray<int> SlotRarities => default;
    [Networked, Capacity(SlotCount)] private NetworkArray<int> SlotAmmo => default;

    private PlayerShooting _shooting;
    private PlayerController _playerController;
    private Animator _animator;
    private MeshFilter _heldMeshFilter;
    private int _shownHeldWeaponId = -1;
    private static readonly int HasWeaponHash = Animator.StringToHash("HasWeapon");

    #region Yuva bilgisi (HUD ve botlar okur)

    public int GetSlotWeaponId(int slot) => ValidSlot(slot) ? SlotWeapons[slot] : FistWeaponId;

    public int GetSlotRarity(int slot) => ValidSlot(slot) ? SlotRarities[slot] : 0;

    public int GetSlotAmmo(int slot) => ValidSlot(slot) ? SlotAmmo[slot] : 0;

    public bool SlotHasWeapon(int slot) => GetSlotWeaponId(slot) != FistWeaponId;

    public WeaponDefinition GetSlotWeapon(int slot) =>
        SlotHasWeapon(slot) ? Shooting?.GetWeapon(GetSlotWeaponId(slot)) : null;

    public int GetSlotMaxAmmo(int slot)
    {
        WeaponDefinition weapon = GetSlotWeapon(slot);
        return weapon != null ? weapon.maxAmmo : 0;
    }

    /// <summary>Eldeki silahin nadirligi (yumrukta 0).</summary>
    public int CurrentRarity => WeaponId != FistWeaponId ? GetSlotRarity(ActiveSlot) : 0;

    /// <summary>Eldeki silahin nadirlik hasar carpani.</summary>
    public float CurrentDamageMultiplier =>
        WeaponId != FistWeaponId ? WeaponLootSettings.DamageMultiplier(CurrentRarity) : 1f;

    #endregion

    #region Eski arayuz (HUD, pickup'lar ve botlar bunlari kullaniyor)

    /// <summary>En az bir menzilli silah tasiyor mu.</summary>
    public bool HasRangedWeapon => SlotHasWeapon(0) || SlotHasWeapon(1);
    public bool HasRifle => HasRangedWeapon;

    /// <summary>Secili yuvadaki silahin mermisi.</summary>
    public int Ammo => GetSlotAmmo(ActiveSlot);
    public int RifleAmmo => Ammo;
    public int MaxRifleAmmo => SlotHasWeapon(ActiveSlot) ? GetSlotMaxAmmo(ActiveSlot) : 120;
    public int SelectedSlot => WeaponId == FistWeaponId ? 0 : 1;
    public bool IsRifleSelected => WeaponId != FistWeaponId;

    /// <summary>Secili yuvadaki silahin tanimi (secili olmasa bile).</summary>
    public WeaponDefinition RangedWeapon => GetSlotWeapon(ActiveSlot);

    #endregion

    /// <summary>Elindeki silahin tanimi. Dizi disi bir kimlikte null doner.</summary>
    public WeaponDefinition CurrentWeapon => Shooting?.GetWeapon(WeaponId);

    private PlayerShooting Shooting
    {
        get
        {
            if (_shooting == null)
                _shooting = GetComponent<PlayerShooting>();
            return _shooting;
        }
    }

    private static bool ValidSlot(int slot) => slot >= 0 && slot < SlotCount;

    private void Awake()
    {
        _shooting = GetComponent<PlayerShooting>();
        _playerController = GetComponent<PlayerController>();
        _animator = GetComponentInChildren<Animator>();

        if (rifleVisual != null)
        {
            _heldMeshFilter = rifleVisual.GetComponentInChildren<MeshFilter>(true);
            rifleVisual.SetActive(false);
        }
    }

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                SlotWeapons.Set(i, FistWeaponId);
                SlotRarities.Set(i, 0);
                SlotAmmo.Set(i, 0);
            }

            ActiveSlot = 0;
            WeaponId = FistWeaponId;
        }

        ApplyVisualState();
    }

    public override void Render()
    {
        ApplyVisualState();
    }

    #region Secim

    /// <summary>HUD'dan yuva secimi (sadece yerel oyuncu).</summary>
    public void SelectSlot(int slot)
    {
        if (HasInputAuthority && ValidSlot(slot) && SlotHasWeapon(slot))
            Rpc_RequestSlot(slot);
    }

    /// <summary>Eski cagri yolu: silah varken yumruga donulmez, bos kalir.</summary>
    public void SelectFist() { }

    /// <summary>Eski cagri yolu: secili yuvayi ister.</summary>
    public void SelectRifle() => SelectSlot(ActiveSlot);

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void Rpc_RequestSlot(int slot)
    {
        // Sunucu da kontrol eder: degistirilmis bir istemci bos yuvayi secemesin.
        if (!ValidSlot(slot) || !SlotHasWeapon(slot) || SlotAmmo[slot] <= 0)
            return;

        ActiveSlot = slot;
        RecomputeWeapon();
    }

    #endregion

    #region Silah alma

    /// <summary>Bu silahi alabilir mi (bos yuva / yukseltme / mermi / daha guclu silah).</summary>
    public bool CanAcceptWeapon(int weaponId, int rarity)
    {
        WeaponDefinition incoming = Shooting?.GetWeapon(weaponId);
        if (incoming == null || incoming.isMelee)
            return false;

        int same = FindSlotWith(weaponId);
        if (same >= 0)
            return rarity > SlotRarities[same] || SlotAmmo[same] < incoming.maxAmmo;

        if (FindEmptySlot() >= 0)
            return true;

        return Score(weaponId, rarity) > Score(SlotWeapons[ActiveSlot], SlotRarities[ActiveSlot]);
    }

    /// <summary>Yerdeki silah, eldekinden mi daha iyi (bos yuva ya da yukseltme). Botlar okur.</summary>
    public bool IsUpgrade(int weaponId, int rarity)
    {
        int same = FindSlotWith(weaponId);
        if (same >= 0)
            return rarity > SlotRarities[same];

        return FindEmptySlot() >= 0 ||
               Score(weaponId, rarity) > Score(SlotWeapons[ActiveSlot], SlotRarities[ActiveSlot]);
    }

    /// <summary>
    /// Silahi verir. Iki yuva doluysa eldeki silah cikarilir; cikan silah
    /// droppedWeaponId (0 = hicbir sey dusmedi) ile doner, cagiran yere birakir.
    /// </summary>
    public void GrantWeapon(int weaponId, int rarity, int ammo,
        out int droppedWeaponId, out int droppedRarity, out int droppedAmmo)
    {
        droppedWeaponId = FistWeaponId;
        droppedRarity = 0;
        droppedAmmo = 0;

        if (!HasStateAuthority)
            return;

        WeaponDefinition incoming = Shooting?.GetWeapon(weaponId);
        if (incoming == null || incoming.isMelee)
            return;

        ammo = Mathf.Max(0, ammo);
        rarity = Mathf.Clamp(rarity, 0, (int)WeaponRarity.Efsanevi);

        int same = FindSlotWith(weaponId);
        if (same >= 0)
        {
            // Ayni silah: daha nadirse yukselt, mermiyi her durumda ekle.
            if (rarity > SlotRarities[same])
                SlotRarities.Set(same, rarity);
            SlotAmmo.Set(same, Mathf.Min(incoming.maxAmmo, SlotAmmo[same] + ammo));
        }
        else
        {
            int slot = FindEmptySlot();
            if (slot < 0)
            {
                slot = ActiveSlot;
                droppedWeaponId = SlotWeapons[slot];
                droppedRarity = SlotRarities[slot];
                droppedAmmo = SlotAmmo[slot];
            }

            SlotWeapons.Set(slot, weaponId);
            SlotRarities.Set(slot, rarity);
            SlotAmmo.Set(slot, Mathf.Min(incoming.maxAmmo, ammo));

            // Yumrukla geziyorsa (ya da yuva degistiyse) yeni silaha gec.
            if (WeaponId == FistWeaponId || slot == ActiveSlot)
                ActiveSlot = slot;
        }

        RecomputeWeapon();
    }

    /// <summary>Eski cagri yolu: Siradan tufek.</summary>
    public void GrantRifle(int ammo) => GrantWeapon(1, 0, ammo, out _, out _, out _);

    /// <summary>Eski cagri yolu. tier 0 = Siradan tufek, 1+ = airdrop silahi (ayarlardaki nadirlikte).</summary>
    public void GrantRifle(int ammo, int tier)
    {
        WeaponLootSettings settings = WeaponLootSettings.Instance;
        if (tier >= 1)
            GrantWeapon(settings.airdropWeaponId, (int)settings.airdropRarity, ammo, out _, out _, out _);
        else
            GrantRifle(ammo);
    }

    private int FindSlotWith(int weaponId)
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (SlotWeapons[i] == weaponId)
                return i;
        }

        return -1;
    }

    private int FindEmptySlot()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (SlotWeapons[i] == FistWeaponId)
                return i;
        }

        return -1;
    }

    /// <summary>Saniyedeki hasar x nadirlik: iki silahi karsilastirmanin olcusu.</summary>
    private float Score(int weaponId, int rarity)
    {
        WeaponDefinition weapon = Shooting?.GetWeapon(weaponId);
        if (weapon == null || weapon.isMelee)
            return 0f;

        float dps = weapon.damage * weapon.pellets / Mathf.Max(0.02f, weapon.fireInterval);
        return dps * WeaponLootSettings.DamageMultiplier(rarity);
    }

    #endregion

    #region Cephane

    /// <summary>Mermisi dolu olmayan bir silahi var mi.</summary>
    public bool CanTakeAmmo => AmmoTargetSlot() >= 0;

    /// <summary>Mermi kutusu: silahin azami mermisinin bu orani kadar ekler.</summary>
    public void AddAmmoPack(float fraction)
    {
        int slot = AmmoTargetSlot();
        if (!HasStateAuthority || slot < 0)
            return;

        int amount = Mathf.Max(1, Mathf.CeilToInt(GetSlotMaxAmmo(slot) * Mathf.Clamp01(fraction)));
        AddAmmoToSlot(slot, amount);
    }

    /// <summary>Eski cagri yolu: secili (ya da ihtiyaci olan) silaha mermi ekler.</summary>
    public void AddRifleAmmo(int amount)
    {
        int slot = AmmoTargetSlot();
        if (HasStateAuthority && slot >= 0)
            AddAmmoToSlot(slot, amount);
    }

    private void AddAmmoToSlot(int slot, int amount)
    {
        SlotAmmo.Set(slot, Mathf.Min(GetSlotMaxAmmo(slot), SlotAmmo[slot] + Mathf.Max(0, amount)));
        RecomputeWeapon();
    }

    /// <summary>Once secili yuva, dolu ise diger yuva; ikisi de doluysa -1.</summary>
    private int AmmoTargetSlot()
    {
        if (SlotHasWeapon(ActiveSlot) && GetSlotAmmo(ActiveSlot) < GetSlotMaxAmmo(ActiveSlot))
            return ActiveSlot;

        int other = 1 - ActiveSlot;
        if (SlotHasWeapon(other) && GetSlotAmmo(other) < GetSlotMaxAmmo(other))
            return other;

        return -1;
    }

    /// <summary>Atis icin mermi dusur. Sinirsiz cephaneli silahlarda hep true doner.</summary>
    public bool TryConsumeAmmo()
    {
        if (!HasStateAuthority)
            return false;

        WeaponDefinition weapon = CurrentWeapon;
        if (weapon == null)
            return false;

        if (weapon.infiniteAmmo || WeaponId == FistWeaponId)
            return true;

        int slot = ActiveSlot;
        if (SlotAmmo[slot] <= 0)
        {
            RecomputeWeapon();
            return false;
        }

        SlotAmmo.Set(slot, SlotAmmo[slot] - 1);
        if (SlotAmmo[slot] <= 0)
            RecomputeWeapon(); // bitti: diger silaha ya da yumruga gec
        return true;
    }

    #endregion

    /// <summary>
    /// Kullanilan silahi yuvalardan hesaplar: secili yuvada mermi varsa o,
    /// yoksa diger yuva, o da yoksa yumruk.
    /// </summary>
    private void RecomputeWeapon()
    {
        if (!HasStateAuthority)
            return;

        int active = ActiveSlot;
        if (SlotHasWeapon(active) && SlotAmmo[active] > 0)
        {
            WeaponId = SlotWeapons[active];
        }
        else
        {
            int other = 1 - active;
            if (SlotHasWeapon(other) && SlotAmmo[other] > 0)
            {
                ActiveSlot = other;
                WeaponId = SlotWeapons[other];
            }
            else
            {
                WeaponId = FistWeaponId;
            }
        }

        ApplyVisualState();
    }

    private void ApplyVisualState()
    {
        bool rangedSelected = WeaponId != FistWeaponId;

        if (rifleVisual != null && rifleVisual.activeSelf != rangedSelected)
            rifleVisual.SetActive(rangedSelected);

        if (rangedSelected)
            ApplyHeldMesh();

        if (_animator != null)
            _animator.SetBool(HasWeaponHash, rangedSelected);

        if (HasStateAuthority && _playerController != null)
            _playerController.hasWeapon = rangedSelected;
    }

    /// <summary>
    /// Eldeki silahin modelini tanimdaki heldMesh ile degistirir. Tek bir
    /// gorsel nesne tum silahlara hizmet eder; sadece mesh'i degisir.
    /// </summary>
    private void ApplyHeldMesh()
    {
        if (_heldMeshFilter == null || _shownHeldWeaponId == WeaponId)
            return;

        _shownHeldWeaponId = WeaponId;

        WeaponDefinition weapon = CurrentWeapon;
        if (weapon != null && weapon.heldMesh != null)
            _heldMeshFilter.sharedMesh = weapon.heldMesh;
    }
}
