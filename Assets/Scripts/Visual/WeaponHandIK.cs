using UnityEngine;

/// <summary>
/// Elleri tufege oturtur: sag el kabzaya, sol el on tutamaga.
///
/// Tufek karakterin eline bagli degil; govdede sabit bir yuvada (WeaponSocket)
/// duruyor. Animasyondaki eller ise ondan bagimsiz oynuyordu ve karakter
/// silahi tutmuyor gibi gorunuyordu. Bu bilesen her karede, animasyon ve
/// govde burulmasi (CharacterAnimationDriver) uygulandiktan SONRA kollari
/// iki kemikli IK ile tutamak noktalarina uzatir.
///
/// Tutamak noktalari RifleVisual'in altindaki "SagElTutamak" ve "SolElTutamak"
/// nesneleri: Hierarchy'den tasiyarak ellerin yerini ayarlayabilirsin.
/// Humanoid her karakterde (melek dahil) calisir.
/// </summary>
[DefaultExecutionOrder(500)]
public class WeaponHandIK : MonoBehaviour
{
    [Tooltip("Tufek gorseli (aktifken eller ona tutunur).")]
    [SerializeField] private GameObject weapon;
    [SerializeField] private Transform rightGrip;
    [SerializeField] private Transform leftGrip;

    [Tooltip("0 = animasyondaki el, 1 = tam tutamakta.")]
    [SerializeField, Range(0f, 1f)] private float weight = 1f;
    [Tooltip("Silah alinip birakilirken gecis hizi.")]
    [SerializeField, Min(0.1f)] private float blendSpeed = 8f;
    [Tooltip("Isaretliyse eller tutamagin yonunu de alir (tutamagi dondurerek el acisini ayarla).")]
    [SerializeField] private bool matchHandRotation;

    private Animator _animator;
    private HealthController _health;
    private NewBattle.Gameplay.ParachuteDescent _descent;
    private Transform _rightUpper, _rightLower, _rightHand;
    private Transform _leftUpper, _leftLower, _leftHand;
    private float _current;

    private void Awake()
    {
        _animator = GetComponentInChildren<Animator>(true);
        _health = GetComponent<HealthController>();
        _descent = GetComponent<NewBattle.Gameplay.ParachuteDescent>();
        CacheBones();
    }

    private void OnEnable() => NewBattle.Gameplay.CharacterModelSwap.Swapped += OnModelSwapped;

    private void OnDisable() => NewBattle.Gameplay.CharacterModelSwap.Swapped -= OnModelSwapped;

    private void OnModelSwapped(Animator swapped)
    {
        if (swapped == _animator)
            CacheBones();
    }

    private void CacheBones()
    {
        if (_animator == null || !_animator.isHuman)
            return;

        _rightUpper = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        _rightLower = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        _rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
        _leftUpper = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        _leftLower = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        _leftHand = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
    }

    private void LateUpdate()
    {
        bool holding = weapon != null && weapon.activeInHierarchy &&
                       (_health == null || _health.IsStanding) &&
                       (_descent == null || !_descent.IsDescending);

        _current = Mathf.MoveTowards(_current, holding ? weight : 0f, blendSpeed * Time.deltaTime);
        if (_current <= 0.001f)
            return;

        Solve(_rightUpper, _rightLower, _rightHand, rightGrip);
        Solve(_leftUpper, _leftLower, _leftHand, leftGrip);
    }

    /// <summary>
    /// Iki kemikli IK: once dirsegi, el ile omuz arasi mesafe hedefe esit
    /// olacak kadar bukeriz; sonra omzu, el hedefe bakacak sekilde cevireriz.
    /// Dirsek animasyondaki bukulme duzleminde kalir, kol ters donmez.
    /// </summary>
    private void Solve(Transform upper, Transform lower, Transform hand, Transform grip)
    {
        if (upper == null || lower == null || hand == null || grip == null)
            return;

        Vector3 a = upper.position;
        Vector3 b = lower.position;
        Vector3 c = hand.position;
        Vector3 target = Vector3.Lerp(c, grip.position, _current);

        float upperLength = Vector3.Distance(a, b);
        float lowerLength = Vector3.Distance(b, c);
        float reach = Mathf.Clamp(Vector3.Distance(a, target), 0.01f, upperLength + lowerLength - 0.001f);

        // Dirsek acisi (kosinus teoremi).
        Vector3 bendAxis = Vector3.Cross(a - b, c - b);
        if (bendAxis.sqrMagnitude < 1e-8f)
            bendAxis = Vector3.Cross(a - b, transform.up);
        bendAxis.Normalize();

        float current = Vector3.Angle(a - b, c - b);
        float desired = Mathf.Acos(Mathf.Clamp(
            (upperLength * upperLength + lowerLength * lowerLength - reach * reach) / (2f * upperLength * lowerLength),
            -1f, 1f)) * Mathf.Rad2Deg;

        Quaternion handRotation = hand.rotation;
        lower.rotation = Quaternion.AngleAxis(desired - current, bendAxis) * lower.rotation;

        // Omuzdan ele giden cizgiyi hedefe cevir.
        upper.rotation = Quaternion.FromToRotation(hand.position - a, target - a) * upper.rotation;

        if (matchHandRotation)
            hand.rotation = Quaternion.Slerp(handRotation, grip.rotation, _current);
    }
}
