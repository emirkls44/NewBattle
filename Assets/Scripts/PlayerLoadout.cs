using Fusion;
using NewBattle.Gameplay;
using UnityEngine;

/// <summary>
/// Oyuncunun elindeki silah ve cephanesi.
///
/// Ag uzerinde sadece iki sayi tasinir: WeaponId (PlayerShooting'deki silah
/// dizisinin indeksi) ve Ammo. Silahin hasari, atis hizi, menzili gibi her sey
/// tanimdan okunur - yeni silah eklemek bu sinifi degistirmeyi gerektirmez.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerLoadout : NetworkBehaviour
{
    /// <summary>Yumruk her zaman 0 numaradir; silahsiz oyuncunun varsayilan hali.</summary>
    public const int FistWeaponId = 0;

    [Header("Gorsel")]
    [SerializeField] private GameObject rifleVisual;

    /// <summary>Elindeki silahin PlayerShooting dizisindeki indeksi.</summary>
    [Networked] public int WeaponId { get; private set; }

    /// <summary>Elindeki silahin mermisi. Yumrukta anlamsizdir.</summary>
    [Networked] public int Ammo { get; private set; }

    /// <summary>Menzilli silahi var mi (yumruktan baska bir sey tasiyor mu).</summary>
    [Networked] public NetworkBool HasRangedWeapon { get; private set; }

    /// <summary>Menzilli silah alindiysa saklanan kimligi; yumruga gecince kaybolmaz.</summary>
    [Networked] private int StowedWeaponId { get; set; }

    private PlayerShooting _shooting;
    private PlayerController _playerController;
    private Animator _animator;
    private static readonly int HasWeaponHash = Animator.StringToHash("HasWeapon");

    #region Eski arayuz (HUD ve pickup'lar bunlari kullaniyor)

    public bool HasRifle => HasRangedWeapon;
    public int RifleAmmo => Ammo;
    public int SelectedSlot => WeaponId == FistWeaponId ? 0 : 1;
    public bool IsRifleSelected => WeaponId != FistWeaponId;
    /// <summary>
    /// Tasinan MENZILLI silahin mermi tavani. Elde yumruk varken de dogru cevabi
    /// vermeli: AmmoPickup "mermim dolu mu" diye buna bakiyor, secili silaha
    /// bakarsa yumruk eldeyken mermi kutusu toplanamaz hale gelir.
    /// </summary>
    public int MaxRifleAmmo
    {
        get
        {
            WeaponDefinition ranged = RangedWeapon;
            return ranged != null ? ranged.maxAmmo : 120;
        }
    }

    /// <summary>Tasinan menzilli silahin tanimi (secili olmasa bile).</summary>
    public WeaponDefinition RangedWeapon
    {
        get
        {
            if (!HasRangedWeapon)
                return null;

            if (_shooting == null)
                _shooting = GetComponent<PlayerShooting>();

            return _shooting != null ? _shooting.GetWeapon(StowedWeaponId) : null;
        }
    }

    #endregion

    /// <summary>Elindeki silahin tanimi. Dizi disi bir kimlikte null doner.</summary>
    public WeaponDefinition CurrentWeapon
    {
        get
        {
            if (_shooting == null)
                _shooting = GetComponent<PlayerShooting>();

            return _shooting != null ? _shooting.GetWeapon(WeaponId) : null;
        }
    }

    private void Awake()
    {
        _shooting = GetComponent<PlayerShooting>();
        _playerController = GetComponent<PlayerController>();
        _animator = GetComponentInChildren<Animator>();

        if (rifleVisual != null)
            rifleVisual.SetActive(false);
    }

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            WeaponId = FistWeaponId;
            StowedWeaponId = FistWeaponId;
            Ammo = 0;
            HasRangedWeapon = false;
        }

        ApplyVisualState();
    }

    public override void Render()
    {
        ApplyVisualState();
    }

    /// <summary>
    /// Yumruga gecmeyi ister.
    ///
    /// Menzilli silah tasiyorken bu istek KARSILIKSIZ kalir: silah alan
    /// oyuncu bir daha yumruga donemiyor. Boylece "silahi birakip yumrukla
    /// daha hizli kosayim" gibi bir oyun kirici tercih olusmuyor ve sag ust
    /// paneldeki silah gostergesi mac boyunca sabit kaliyor.
    /// </summary>
    public void SelectFist()
    {
        if (HasInputAuthority && !HasRangedWeapon)
            Rpc_RequestSlot(0);
    }

    public void SelectRifle()
    {
        if (HasInputAuthority && HasRangedWeapon)
            Rpc_RequestSlot(1);
    }

    /// <summary>Eski cagri yolu: varsayilan tufegi verir.</summary>
    public void GrantRifle(int ammo)
    {
        GrantWeapon(1, ammo);
    }

    /// <summary>
    /// Eski cagri yolu. tier 0 = normal tufek, 1 = airdrop silahi.
    /// Yeni kod dogrudan GrantWeapon kullanmali.
    /// </summary>
    public void GrantRifle(int ammo, int tier)
    {
        GrantWeapon(tier >= 1 ? 2 : 1, ammo);
    }

    /// <summary>
    /// Silahi eline verir. Elinde daha guclu bir silah varsa zayif olani almaz -
    /// airdrop silahini tasirken yerden normal tufek almak seni zayiflatmamali.
    /// </summary>
    public void GrantWeapon(int weaponId, int ammo)
    {
        if (!HasStateAuthority || _shooting == null)
            return;

        WeaponDefinition incoming = _shooting.GetWeapon(weaponId);
        if (incoming == null || incoming.isMelee)
            return;

        WeaponDefinition held = _shooting.GetWeapon(StowedWeaponId);
        bool upgrade = held == null || held.isMelee || IsBetter(incoming, held);

        if (upgrade)
        {
            StowedWeaponId = weaponId;
            WeaponId = weaponId;
            // Silah degisiminde mermi devralinir; oyuncu daha iyi silaha gecerken
            // elindeki cephaneyi kaybederse yukseltme ceza gibi hissedilir.
            Ammo = Mathf.Min(incoming.maxAmmo, Mathf.Max(0, Ammo) + Mathf.Max(0, ammo));
        }
        else
        {
            // Zayif silahi almiyoruz ama mermisi ise yarar.
            Ammo = Mathf.Min(held.maxAmmo, Ammo + Mathf.Max(0, ammo));
        }

        HasRangedWeapon = true;
        ApplyVisualState();
    }

    /// <summary>Saniyedeki hasar: iki silahi karsilastirmanin en durust olcusu.</summary>
    private static bool IsBetter(WeaponDefinition candidate, WeaponDefinition current)
    {
        float candidateDps = candidate.damage * candidate.pellets / Mathf.Max(0.02f, candidate.fireInterval);
        float currentDps = current.damage * current.pellets / Mathf.Max(0.02f, current.fireInterval);
        return candidateDps > currentDps;
    }

    public void AddRifleAmmo(int amount)
    {
        if (!HasStateAuthority || !HasRangedWeapon)
            return;

        Ammo = Mathf.Min(MaxRifleAmmo, Ammo + Mathf.Max(0, amount));
    }

    /// <summary>Atis icin mermi dusur. Sinirsiz cephaneli silahlarda hep true doner.</summary>
    public bool TryConsumeAmmo()
    {
        if (!HasStateAuthority)
            return false;

        WeaponDefinition weapon = CurrentWeapon;
        if (weapon == null)
            return false;

        if (weapon.infiniteAmmo)
            return true;

        if (Ammo <= 0)
            return false;

        Ammo--;
        return true;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void Rpc_RequestSlot(int slot)
    {
        // Yumruga donus kurali burada da uygulaniyor, sadece SelectFist'te
        // degil. Oradaki kontrol istemcide calisiyor; degistirilmis bir
        // istemci bu RPC'yi dogrudan gonderip kurali atlayabilirdi.
        if (slot == 0)
        {
            if (!HasRangedWeapon)
                WeaponId = FistWeaponId;
        }
        else if (HasRangedWeapon)
        {
            WeaponId = StowedWeaponId;
        }

        ApplyVisualState();
    }

    private void ApplyVisualState()
    {
        bool rangedSelected = WeaponId != FistWeaponId;

        if (rifleVisual != null && rifleVisual.activeSelf != rangedSelected)
            rifleVisual.SetActive(rangedSelected);

        if (_animator != null)
            _animator.SetBool(HasWeaponHash, rangedSelected);

        if (HasStateAuthority && _playerController != null)
            _playerController.hasWeapon = rangedSelected;
    }
}
