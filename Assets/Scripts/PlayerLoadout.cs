using Fusion;
using NewBattle.Gameplay;
using UnityEngine;

/// <summary>
/// Oyuncunun elindeki TEK silah ve cephanesi.
///
/// Ag uzerinde uc sayi tasinir: WeaponId (PlayerShooting'deki silah dizisinin
/// indeksi; 0 = yumruk), nadirlik ve mermi. Hasar, atis hizi gibi her sey
/// tanimdan okunur.
///
/// Kurallar:
///   - Yerdeki silahin halkasi dolunca elindeki silah YOK OLUR, yenisi kendi
///     mermisiyle gelir (eski mermi aktarilmaz).
///   - Mermi bitince silah gider, yumruga gecilir.
///   - Mermi kutusu sadece elinde silah varken ve mermi dolu degilken alinir.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerLoadout : NetworkBehaviour
{
    /// <summary>Yumruk her zaman 0 numaradir; silahsiz oyuncunun varsayilan hali.</summary>
    public const int FistWeaponId = 0;

    [Header("Gorsel")]
    [SerializeField] private GameObject rifleVisual;

    /// <summary>Elindeki silahin PlayerShooting dizisindeki indeksi (0 = yumruk).</summary>
    [Networked] public int WeaponId { get; private set; }

    /// <summary>Elindeki silahin nadirligi (WeaponRarity; yumrukta 0).</summary>
    [Networked] public int Rarity { get; private set; }

    /// <summary>Elindeki silahin mermisi. Yumrukta anlamsizdir.</summary>
    [Networked] public int Ammo { get; private set; }

    /// <summary>Cantadaki kayisi sayisi (atilabilir iyilestirme).</summary>
    [Networked] public int ApricotCount { get; private set; }

    /// <summary>Cantadaki findik sayisi (atilabilir yavaslatma).</summary>
    [Networked] public int HazelnutCount { get; private set; }

    /// <summary>Eldeki atilabilir: 0 yok (silah), 1 + ThrowableKind.</summary>
    [Networked] private int EquippedSlot { get; set; }

    // Joystick birakilinca atilir: son gecerli nisan burada tutulur.
    [Networked] private Vector2 ThrowAim { get; set; }
    [Networked] private TickTimer ThrowCooldown { get; set; }

    private GameObject _heldModel;
    private int _heldModelSlot;
    private Transform _heldHand;

    private PlayerShooting _shooting;
    private PlayerController _playerController;
    private Animator _animator;
    private MeshFilter _heldMeshFilter;
    private int _shownHeldWeaponId = -1;
    private static readonly int HasWeaponHash = Animator.StringToHash("HasWeapon");

    /// <summary>Elinde menzilli bir silah var mi.</summary>
    public bool HasRangedWeapon => WeaponId != FistWeaponId;

    /// <summary>Elindeki silahin tanimi. Dizi disi bir kimlikte null doner.</summary>
    public WeaponDefinition CurrentWeapon => Shooting?.GetWeapon(WeaponId);

    /// <summary>Elindeki silahin azami mermisi (yumrukta 0).</summary>
    public int MaxAmmo
    {
        get
        {
            WeaponDefinition weapon = HasRangedWeapon ? CurrentWeapon : null;
            return weapon != null ? weapon.maxAmmo : 0;
        }
    }

    /// <summary>Elindeki silahin nadirlik hasar carpani.</summary>
    public float CurrentDamageMultiplier =>
        HasRangedWeapon ? WeaponLootSettings.DamageMultiplier(Rarity) : 1f;

    #region Eski arayuz (HUD, pickup'lar ve botlar bunlari kullaniyor)

    public bool HasRifle => HasRangedWeapon;
    public int RifleAmmo => Ammo;
    public int MaxRifleAmmo => HasRangedWeapon ? MaxAmmo : 120;
    public int SelectedSlot => HasRangedWeapon ? 1 : 0;
    public bool IsRifleSelected => HasRangedWeapon;
    public WeaponDefinition RangedWeapon => HasRangedWeapon ? CurrentWeapon : null;

    /// <summary>Tek silah var; secim yok (eski HUD butonlari icin bos).</summary>
    public void SelectFist() { }

    public void SelectRifle() { }

    #endregion

    private PlayerShooting Shooting
    {
        get
        {
            if (_shooting == null)
                _shooting = GetComponent<PlayerShooting>();
            return _shooting;
        }
    }

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
            ClearWeapon();

        ApplyVisualState();
    }

    public override void Render()
    {
        ApplyVisualState();
    }

    #region Silah alma

    /// <summary>Bu silahi alabilir mi. Gecerli her menzilli silah alinir (elindekinin yerine).</summary>
    public bool CanAcceptWeapon(int weaponId, int rarity)
    {
        WeaponDefinition incoming = Shooting?.GetWeapon(weaponId);
        return incoming != null && !incoming.isMelee;
    }

    /// <summary>
    /// Bot yapay zekasi: bu silah elindekinden daha mi iyi. Silahsizken her silah;
    /// silahliyken saniyedeki hasari (nadirlik dahil) daha yuksek olan.
    /// </summary>
    public bool IsUpgrade(int weaponId, int rarity)
    {
        if (!CanAcceptWeapon(weaponId, rarity))
            return false;

        return !HasRangedWeapon || Ammo <= 0 || Score(weaponId, rarity) > Score(WeaponId, Rarity);
    }

    /// <summary>Silahi eline verir; elindeki silah ve mermisi yok olur.</summary>
    public void GrantWeapon(int weaponId, int rarity, int ammo)
    {
        if (!HasStateAuthority)
            return;

        WeaponDefinition incoming = Shooting?.GetWeapon(weaponId);
        if (incoming == null || incoming.isMelee)
            return;

        WeaponId = weaponId;
        Rarity = Mathf.Clamp(rarity, 0, (int)WeaponRarity.Efsanevi);
        Ammo = Mathf.Clamp(ammo, 1, incoming.maxAmmo);
        ApplyVisualState();
    }

    /// <summary>Eski cagri yolu: Siradan tufek.</summary>
    public void GrantRifle(int ammo) => GrantWeapon(1, 0, ammo);

    /// <summary>Eski cagri yolu. tier 0 = Siradan tufek, 1+ = airdrop silahi (ayarlardaki nadirlikte).</summary>
    public void GrantRifle(int ammo, int tier)
    {
        WeaponLootSettings settings = WeaponLootSettings.Instance;
        if (tier >= 1)
            GrantWeapon(settings.airdropWeaponId, (int)settings.airdropRarity, ammo);
        else
            GrantRifle(ammo);
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

    /// <summary>Host: yere dusen oyuncunun silahi elinden gider (yumruga doner).</summary>
    public void DropWeapon()
    {
        if (HasStateAuthority)
            ClearWeapon();
    }

    private void ClearWeapon()
    {
        WeaponId = FistWeaponId;
        Rarity = 0;
        Ammo = 0;
    }

    #endregion

    #region Cephane

    /// <summary>Elinde silah var ve mermisi dolu degil.</summary>
    public bool CanTakeAmmo => HasRangedWeapon && Ammo < MaxAmmo;

    /// <summary>Mermi kutusu: silahin azami mermisinin bu orani kadar ekler.</summary>
    public void AddAmmoPack(float fraction)
    {
        if (!HasStateAuthority || !CanTakeAmmo)
            return;

        AddRifleAmmo(Mathf.Max(1, Mathf.CeilToInt(MaxAmmo * Mathf.Clamp01(fraction))));
    }

    public void AddRifleAmmo(int amount)
    {
        if (!HasStateAuthority || !HasRangedWeapon)
            return;

        Ammo = Mathf.Min(MaxAmmo, Ammo + Mathf.Max(0, amount));
    }

    /// <summary>Atis icin mermi dusur. Yumrukta ve sinirsiz cephaneli silahlarda hep true doner.</summary>
    public bool TryConsumeAmmo()
    {
        if (!HasStateAuthority)
            return false;

        WeaponDefinition weapon = CurrentWeapon;
        if (weapon == null)
            return false;

        if (weapon.infiniteAmmo || !HasRangedWeapon)
            return true;

        if (Ammo <= 0)
        {
            ClearWeapon();
            ApplyVisualState();
            return false;
        }

        Ammo--;

        // Son mermi atildi: silah gider, yumruga gecilir.
        if (Ammo <= 0)
        {
            ClearWeapon();
            ApplyVisualState();
        }

        return true;
    }

    #endregion

    #region Atilabilir (kayisi, findik)

    /// <summary>Elinde atilabilir var (silah yerine): saldiri joystick'i atis yapar.</summary>
    public bool ThrowableEquipped => EquippedSlot > 0;

    /// <summary>Eldeki atilabilirin turu (ThrowableEquipped degilse anlamsiz).</summary>
    public ThrowableKind EquippedKind => (ThrowableKind)Mathf.Max(0, EquippedSlot - 1);

    public bool IsEquipped(ThrowableKind kind) => EquippedSlot == (int)kind + 1;

    public int CountOf(ThrowableKind kind) => kind == ThrowableKind.Hazelnut ? HazelnutCount : ApricotCount;

    private void SetCount(ThrowableKind kind, int value)
    {
        if (kind == ThrowableKind.Hazelnut)
            HazelnutCount = value;
        else
            ApricotCount = value;
    }

    public bool CanTake(ThrowableKind kind)
    {
        ApricotSettings settings = ApricotSettings.Get(kind);
        return settings != null && CountOf(kind) < settings.maxCarry;
    }

    /// <summary>Host: yerden kayisi / findik alindi.</summary>
    public void AddThrowable(ThrowableKind kind, int amount)
    {
        ApricotSettings settings = ApricotSettings.Get(kind);
        if (!HasStateAuthority || settings == null)
            return;

        SetCount(kind, Mathf.Clamp(CountOf(kind) + Mathf.Max(0, amount), 0, settings.maxCarry));
    }

    /// <summary>Host: yere dusunce atilabilir elden birakilir (cantada kalir).</summary>
    public void UnequipThrowable()
    {
        if (HasStateAuthority)
        {
            EquippedSlot = 0;
            ThrowAim = default;
        }
    }

    /// <summary>Yerel oyuncu: sag ustteki kayisi / findik butonu (null = silaha don).</summary>
    public void RequestEquip(ThrowableKind? kind)
    {
        if (HasInputAuthority)
            RPC_Equip(kind.HasValue ? (int)kind.Value + 1 : 0);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_Equip(int slot)
    {
        bool valid = slot > 0 && CountOf((ThrowableKind)(slot - 1)) > 0;
        EquippedSlot = valid ? slot : 0;
        ThrowAim = default;
    }

    /// <summary>
    /// Host, her tick: atilabilir eldeyken saldiri joystick'i. Cekilirken nisan alinir;
    /// parmak kalkinca (girdi tam sifir) atilir. Joystick yavasca ortaya geri
    /// getirilirse atis iptal olur.
    /// </summary>
    public void ProcessThrow(Vector2 aim)
    {
        if (!HasStateAuthority)
            return;

        if (!ThrowableEquipped || CountOf(EquippedKind) <= 0)
        {
            ThrowAim = default;
            return;
        }

        float strength = aim.sqrMagnitude;
        if (strength > 0.04f)
        {
            ThrowAim = aim;
            return;
        }

        bool released = strength < 0.000001f;
        bool armed = ThrowAim.sqrMagnitude > 0.04f;
        Vector2 throwAim = ThrowAim;
        ThrowAim = default;

        if (released && armed && ThrowCooldown.ExpiredOrNotRunning(Runner))
            Throw(EquippedKind, throwAim);
    }

    private void Throw(ThrowableKind kind, Vector2 aim)
    {
        ApricotSettings settings = ApricotSettings.Get(kind);
        if (settings == null || !settings.zonePrefab.IsValid)
            return;

        Vector3 origin = transform.position;
        Vector3 target = settings.ResolveLanding(origin, aim, transform);
        int teamId = TryGetComponent(out PlayerCombatStats stats) ? stats.TeamId : -1;
        NetworkId thrower = Object.Id;

        int left = CountOf(kind) - 1;
        SetCount(kind, Mathf.Max(0, left));
        if (left <= 0)
            EquippedSlot = 0;

        ThrowCooldown = TickTimer.CreateFromSeconds(Runner, settings.throwCooldown);

        Runner.Spawn(settings.zonePrefab, target, Quaternion.identity, null, (runner, spawned) =>
        {
            if (spawned.TryGetComponent(out ApricotHealZone zone))
                zone.Init(runner, kind, origin + Vector3.up * 1.3f, target, teamId, thrower);
        });
    }

    /// <summary>Atilabilir eldeyken sag elde onun modeli (silah gizli).</summary>
    private void ApplyHeldModel()
    {
        if (!ThrowableEquipped)
        {
            if (_heldModel != null && _heldModel.activeSelf)
                _heldModel.SetActive(false);
            return;
        }

        if (_heldHand == null)
        {
            Animator animator = GetComponentInChildren<Animator>();
            _heldHand = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
        }

        ApricotSettings settings = ApricotSettings.Get(EquippedKind);
        if (_heldHand == null || settings == null || settings.model == null)
            return;

        if (_heldModel != null && _heldModelSlot != EquippedSlot)
        {
            Destroy(_heldModel);
            _heldModel = null;
        }

        if (_heldModel == null)
        {
            _heldModel = Instantiate(settings.model);
            _heldModel.name = "EldekiAtilabilir";
            _heldModel.transform.localScale = Vector3.one * settings.modelSize;
            _heldModelSlot = EquippedSlot;
        }

        if (!_heldModel.activeSelf)
            _heldModel.SetActive(true);

        // Elin olcegi rigden rige degisir; ebeveyn yapmadan dunyada takip edilir.
        _heldModel.transform.SetPositionAndRotation(
            _heldHand.position + _heldHand.rotation * settings.heldOffset,
            _heldHand.rotation);
    }

    private void LateUpdate()
    {
        if (Object != null && Object.IsValid && _heldModel != null && _heldModel.activeSelf)
            ApplyHeldModel();
    }

    private void OnDestroy()
    {
        if (_heldModel != null)
            Destroy(_heldModel);
    }

    #endregion

    private void ApplyVisualState()
    {
        ApplyHeldModel();
        bool rangedSelected = WeaponId != FistWeaponId && !ThrowableEquipped;

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
