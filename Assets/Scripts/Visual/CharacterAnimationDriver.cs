using NewBattle.Gameplay;
using UnityEngine;

/// <summary>
/// Karakter animasyonunu GERCEK hareketten surer.
///
/// NEDEN: Eskiden animasyon state makinesinin yazdigi IsMoving bool'una bagliydi.
/// Uc sorunu vardi:
///   - Yon bilgisi yoktu. Nisan alirken govde nisana donuyor, kosu animasyonu hep
///     ileri oynuyordu; geri geri kacan oyuncu "moonwalk" yapiyordu.
///   - Hiz bilgisi yoktu. Calidaki yavas yuruyus ile kosu ayni klibi oynuyordu.
///   - IsMoving sadece yetkili tarafta yaziliyordu; uzak oyuncular calida kayiyordu.
///
/// Bu bilesen her istemcide ekrandaki konum degisiminden hizi olcer. Boylece
/// yerel oyuncu, uzak oyuncular ve botlar ayni kodla dogru animasyonu alir ve
/// aga tek bir bayt fazladan gitmez.
///
/// Yana kosu klibi olmadigi icin bacak/govde ayrimi prosedurel:
///   - Model (bacaklar) hareket yonune doner, ileri ya da geri kosu klibi oynar.
///   - Omurga kemikleri ters yone burulur; gogus, kollar ve silah nisanda kalir.
/// Silah soketi karakter kokune bagli oldugu icin bu burulma nisan dogrulugunu
/// etkilemez.
/// </summary>
[DisallowMultipleComponent]
public class CharacterAnimationDriver : MonoBehaviour
{
    [Header("Baglantilar")]
    [SerializeField] private Animator animator;
    [Tooltip("Bacak yonune dondurulen gorsel model. Bos birakilirsa Animator'un nesnesi.")]
    [SerializeField] private Transform model;
    [Tooltip("Ates edince geriye tepen silah gorseli (RifleVisual).")]
    [SerializeField] private Transform weaponKick;

    [Header("Hareket Olcumu")]
    [SerializeField, Min(0.01f)] private float velocitySmoothing = 0.08f;
    [Tooltip("Bu hizin altinda karakter duruyor sayilir.")]
    [SerializeField, Min(0.01f)] private float moveThreshold = 0.35f;
    [SerializeField, Min(1f)] private float maxTrackedSpeed = 9f;
    [Tooltip("Bir karede bundan fazla yer degistirme isinlanma sayilir, hiz olarak okunmaz.")]
    [SerializeField, Min(0.5f)] private float teleportDistance = 3f;

    [Header("Bacak / Govde Ayrimi")]
    [Tooltip("Bacaklarin govdeye gore en fazla ne kadar donebilecegi.")]
    [SerializeField, Range(0f, 90f)] private float maxLegYaw = 70f;
    [Tooltip("Hareket ile nisan arasindaki aci bunu gecince geri geri yurume baslar.")]
    [SerializeField, Range(90f, 170f)] private float backpedalEnterAngle = 115f;
    [Tooltip("Geri yurumeden ileri kosuya donus acisi. Giristen kucuk olmali ki titreme olmasin.")]
    [SerializeField, Range(45f, 150f)] private float backpedalExitAngle = 95f;
    [SerializeField, Min(0.01f)] private float legYawSmoothing = 0.07f;
    [Tooltip("Govde burulmasinin Spine / Chest / UpperChest kemiklerine dagilimi.")]
    [SerializeField] private Vector3 twistDistribution = new(0.3f, 0.35f, 0.35f);

    [Header("Oynatma")]
    [Tooltip("Kosu kliplerinin oynatma hizi carpani. Ayak kayiyorsa buradan ayarla.")]
    [SerializeField, Range(0.5f, 2f)] private float locomotionPlaybackScale = 1f;
    [SerializeField, Min(0.1f)] private float upperBodyBlendSpeed = 10f;

    [Header("Geri Tepme ve Sarsilma")]
    [SerializeField, Range(0f, 25f)] private float fireKickDegrees = 8f;
    [SerializeField, Range(0f, 0.3f)] private float weaponKickDistance = 0.08f;
    [SerializeField, Min(1f)] private float kickRecovery = 16f;
    [SerializeField, Range(0f, 30f)] private float hitFlinchDegrees = 12f;
    [SerializeField, Min(1f)] private float flinchRecovery = 9f;

    private const string UpperBodyLayerName = "UpperBody";

    private static readonly int MoveZHash = Animator.StringToHash("MoveZ");
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int LocomotionSpeedHash = Animator.StringToHash("LocomotionSpeed");
    private static readonly int HasWeaponHash = Animator.StringToHash("HasWeapon");
    private static readonly int PunchStateHash = Animator.StringToHash("Punch");

    private HealthController _health;
    private ParachuteDescent _descent;
    private Renderer[] _bodyRenderers;

    private Quaternion _modelBaseRotation;
    private Vector3 _weaponBaseLocalPosition;

    private Transform[] _twistBones;
    private float[] _twistWeights;
    private Transform _kickBone;

    private bool _hasMoveZ;
    private bool _hasSpeed;
    private bool _hasLocomotionSpeed;
    private bool _hasWeaponParameter;
    private int _upperLayer = -1;

    private Vector3 _lastPosition;
    private bool _hasLastPosition;
    private Vector3 _velocity;
    private Vector3 _velocitySmoothVelocity;

    private float _legYaw;
    private float _legYawVelocity;
    private bool _backpedal;
    private float _upperWeight;

    private float _kick;
    private float _flinch;
    private float _flinchSide = 1f;

    /// <summary>Ates edildiginde govdeyi ve silahi kisaca geriye iter.</summary>
    public void PlayFireKick()
    {
        _kick = Mathf.Min(1.4f, _kick + 1f);
    }

    /// <summary>Hasar alindiginda govde kisaca geriye ve yana sarsilir.</summary>
    public void PlayHitFlinch()
    {
        _flinch = 1f;
        _flinchSide = Random.value < 0.5f ? -1f : 1f;
    }

    private void Awake()
    {
        _health = GetComponent<HealthController>();
        _descent = GetComponent<ParachuteDescent>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (model == null && animator != null)
            model = animator.transform;

        // Model kokun kendisiyse dondurmek ag konumunu bozar; o durumda sadece
        // animasyon parametrelerini yaz.
        if (model == transform)
            model = null;

        if (model != null)
        {
            _modelBaseRotation = model.localRotation;
            _bodyRenderers = model.GetComponentsInChildren<Renderer>(true);
        }

        if (weaponKick != null)
            _weaponBaseLocalPosition = weaponKick.localPosition;
    }

    private void Start()
    {
        CacheAnimatorLayout();
        CacheBones();
    }

    private void CacheAnimatorLayout()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == MoveZHash) _hasMoveZ = true;
            else if (parameter.nameHash == SpeedHash) _hasSpeed = true;
            else if (parameter.nameHash == LocomotionSpeedHash) _hasLocomotionSpeed = true;
            else if (parameter.nameHash == HasWeaponHash) _hasWeaponParameter = true;
        }

        _upperLayer = animator.GetLayerIndex(UpperBodyLayerName);
    }

    private void CacheBones()
    {
        if (animator == null || !animator.isHuman)
            return;

        Transform spine = animator.GetBoneTransform(HumanBodyBones.Spine);
        Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        Transform upperChest = animator.GetBoneTransform(HumanBodyBones.UpperChest);

        // Riglerin hepsinde uc omurga kemigi yok. Olmayanin payini digerlerine
        // dagit ki toplam burulma her zaman tam olsun.
        Transform[] bones = { spine, chest, upperChest };
        float[] weights = { twistDistribution.x, twistDistribution.y, twistDistribution.z };

        int count = 0;
        float total = 0f;

        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i] == null)
                continue;

            count++;
            total += Mathf.Max(0f, weights[i]);
        }

        _twistBones = new Transform[count];
        _twistWeights = new float[count];

        for (int i = 0, index = 0; i < bones.Length; i++)
        {
            if (bones[i] == null)
                continue;

            _twistBones[index] = bones[i];
            _twistWeights[index] = total > 0f ? Mathf.Max(0f, weights[i]) / total : 1f / count;
            index++;
        }

        _kickBone = upperChest != null ? upperChest : chest != null ? chest : spine;
    }

    private void LateUpdate()
    {
        if (animator == null)
            return;

        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
            return;

        MeasureVelocity(deltaTime);

        bool dead = IsDead();
        bool frozen = dead || IsDescending();
        float speed = frozen ? 0f : _velocity.magnitude;
        bool moving = speed > moveThreshold;

        float targetLegYaw = 0f;
        float signedForwardSpeed = 0f;

        if (moving)
        {
            Vector3 facing = transform.forward;
            facing.y = 0f;

            float angle = Vector3.SignedAngle(facing, _velocity, Vector3.up);
            float absoluteAngle = Mathf.Abs(angle);

            // Histerezis: tam sinirda iki mod arasinda gidip gelmesin.
            if (_backpedal)
            {
                if (absoluteAngle < backpedalExitAngle)
                    _backpedal = false;
            }
            else if (absoluteAngle > backpedalEnterAngle)
            {
                _backpedal = true;
            }

            // Geri yururken model nisana bakar, bacaklar hareketin TERSINE doner:
            // geri yurume klibi modelin arkasina dogru ilerler.
            float legAngle = _backpedal ? Mathf.DeltaAngle(180f, angle) : angle;

            targetLegYaw = Mathf.Clamp(legAngle, -maxLegYaw, maxLegYaw);

            // Bacaklarin yetisemedigi aci kadar hiz kaybolur; ayak kaymasi azalir.
            float unreachable = (legAngle - targetLegYaw) * Mathf.Deg2Rad;
            signedForwardSpeed = speed * Mathf.Cos(unreachable) * (_backpedal ? -1f : 1f);
        }
        else
        {
            _backpedal = false;
        }

        _legYaw = Mathf.SmoothDampAngle(_legYaw, targetLegYaw, ref _legYawVelocity, legYawSmoothing);

        WriteParameters(signedForwardSpeed, speed, moving, deltaTime);
        UpdateUpperBody(dead, moving, deltaTime);
        ApplyModelYaw();

        _kick *= Mathf.Exp(-kickRecovery * deltaTime);
        _flinch *= Mathf.Exp(-flinchRecovery * deltaTime);

        if (dead)
            _kick = _flinch = 0f;

        ApplyBoneOffsets();
        ApplyWeaponKick();
    }

    private void MeasureVelocity(float deltaTime)
    {
        Vector3 position = transform.position;

        if (!_hasLastPosition)
        {
            _lastPosition = position;
            _hasLastPosition = true;
        }

        Vector3 delta = position - _lastPosition;
        delta.y = 0f;
        _lastPosition = position;

        // Inis noktasina yerlestirme gibi isinlanmalar kosu olarak okunmasin.
        Vector3 rawVelocity = delta.magnitude > teleportDistance
            ? Vector3.zero
            : Vector3.ClampMagnitude(delta / deltaTime, maxTrackedSpeed);

        _velocity = Vector3.SmoothDamp(_velocity, rawVelocity, ref _velocitySmoothVelocity,
            velocitySmoothing, Mathf.Infinity, deltaTime);
    }

    private void WriteParameters(float signedForwardSpeed, float speed, bool moving, float deltaTime)
    {
        if (_hasMoveZ)
            animator.SetFloat(MoveZHash, signedForwardSpeed, 0.06f, deltaTime);

        if (_hasSpeed)
            animator.SetFloat(SpeedHash, speed, 0.06f, deltaTime);

        if (_hasLocomotionSpeed)
            animator.SetFloat(LocomotionSpeedHash, moving ? locomotionPlaybackScale : 1f);
    }

    /// <summary>
    /// Ust govde katmani: silah tutus, yumruk, geri yururken rahat kollar.
    ///
    /// Katman agirligi koddan yonetiliyor. Bos elle ileri kosarken agirlik 0'dir
    /// ve kosu klibinin kendi kol sallanmasi gorunur; silah alinca, yumruk
    /// atilinca ya da geri yurunurken ust govde bu katmana gecer.
    /// </summary>
    private void UpdateUpperBody(bool dead, bool moving, float deltaTime)
    {
        if (_upperLayer < 0)
            return;

        bool armed = _hasWeaponParameter && animator.GetBool(HasWeaponHash);
        bool punching = IsUpperState(PunchStateHash);

        float target;

        if (dead)
            target = 0f;
        else if (armed || punching)
            target = 1f;
        else
            target = _backpedal && moving ? 1f : 0f;

        // Yumruga hizli gir, yavas cik: vurus kacmasin, cikis sert gorunmesin.
        float rate = target > _upperWeight ? upperBodyBlendSpeed * 1.6f : upperBodyBlendSpeed * 0.6f;
        _upperWeight = Mathf.MoveTowards(_upperWeight, target, rate * deltaTime);
        animator.SetLayerWeight(_upperLayer, _upperWeight);
    }

    private bool IsUpperState(int stateHash)
    {
        if (animator.GetCurrentAnimatorStateInfo(_upperLayer).shortNameHash == stateHash)
            return true;

        return animator.IsInTransition(_upperLayer) &&
               animator.GetNextAnimatorStateInfo(_upperLayer).shortNameHash == stateHash;
    }

    private void ApplyModelYaw()
    {
        if (model == null)
            return;

        model.localRotation = Quaternion.AngleAxis(_legYaw, Vector3.up) * _modelBaseRotation;
    }

    /// <summary>
    /// Animator pozu yazdiktan SONRA kemiklere ek donus uygular. Animator her
    /// karede pozu bastan yazdigi icin ekler birikmez.
    /// </summary>
    private void ApplyBoneOffsets()
    {
        if (_twistBones == null || !IsBodyVisible())
            return;

        // Burulma: gogus nisana, bacaklar harekete baksin.
        float twist = -_legYaw;

        if (Mathf.Abs(twist) > 0.01f)
        {
            for (int i = 0; i < _twistBones.Length; i++)
            {
                Transform bone = _twistBones[i];
                bone.rotation = Quaternion.AngleAxis(twist * _twistWeights[i], Vector3.up) * bone.rotation;
            }
        }

        if (_kickBone == null)
            return;

        float leanBack = fireKickDegrees * Mathf.Min(1f, _kick) + hitFlinchDegrees * 0.55f * _flinch;
        float leanSide = hitFlinchDegrees * 0.5f * _flinch * _flinchSide;

        if (leanBack > 0.01f)
            _kickBone.rotation = Quaternion.AngleAxis(-leanBack, transform.right) * _kickBone.rotation;

        if (Mathf.Abs(leanSide) > 0.01f)
            _kickBone.rotation = Quaternion.AngleAxis(leanSide, transform.forward) * _kickBone.rotation;
    }

    private void ApplyWeaponKick()
    {
        if (weaponKick == null)
            return;

        weaponKick.localPosition = _weaponBaseLocalPosition +
                                   Vector3.back * (weaponKickDistance * Mathf.Min(1f, _kick));
    }

    /// <summary>
    /// Ekranda olmayan karakterin kemiklerine dokunma. Animator culling ile
    /// gorunmeyen karakterin pozunu yazmayi birakir; o durumda burulma her karede
    /// ustune eklenip birikirdi.
    /// </summary>
    private bool IsBodyVisible()
    {
        if (_bodyRenderers == null)
            return false;

        for (int i = 0; i < _bodyRenderers.Length; i++)
        {
            if (_bodyRenderers[i] != null && _bodyRenderers[i].isVisible)
                return true;
        }

        return false;
    }

    private bool IsDead()
    {
        return _health != null && _health.Object != null && _health.Object.IsValid &&
               _health.currentHealth <= 0f;
    }

    private bool IsDescending()
    {
        return _descent != null && _descent.Object != null && _descent.Object.IsValid &&
               _descent.IsDescending;
    }
}
