using NewBattle.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// Vurulunca ekranin kenarlari kisa bir an kirmizi yanar (hasar ne kadar
    /// buyukse o kadar guclu). Canin azken kenarlar yavasca nabiz gibi atar.
    /// Guvenli alan disindaki kirmizi efektten ayri, onunla cakismaz.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class DamageVignette : MonoBehaviour
    {
        [SerializeField] private Color color = new(0.9f, 0.05f, 0.05f, 1f);
        [Tooltip("Tek bir vurusun en fazla parlaklik payi.")]
        [SerializeField, Range(0f, 1f)] private float hitAlpha = 0.55f;
        [Tooltip("Bu hasar (ve ustu) tam parlaklik verir.")]
        [SerializeField, Min(1f)] private float fullHitDamage = 30f;
        [SerializeField, Min(0.1f)] private float fadeSpeed = 2.5f;

        [Header("Can Azken")]
        [Tooltip("Can bu oranin altindayken kenarlar nabiz gibi atar.")]
        [SerializeField, Range(0f, 1f)] private float lowHealthRatio = 0.3f;
        [SerializeField, Range(0f, 1f)] private float lowHealthAlpha = 0.35f;
        [SerializeField, Min(0.1f)] private float pulsePerSecond = 1.4f;

        private Image _image;
        private float _hit;
        private HealthController _local;
        private float _nextSearch;

        private void Awake()
        {
            _image = GetComponent<Image>();
            _image.raycastTarget = false;
            Paint(0f);
        }

        private void OnEnable() => KillEvents.LocalDamaged += OnLocalDamaged;

        private void OnDisable() => KillEvents.LocalDamaged -= OnLocalDamaged;

        private void OnLocalDamaged(float damage)
        {
            _hit = Mathf.Max(_hit, Mathf.Clamp01(damage / fullHitDamage) * hitAlpha + 0.12f);
        }

        private void Update()
        {
            _hit = Mathf.MoveTowards(_hit, 0f, fadeSpeed * Time.unscaledDeltaTime);

            float low = 0f;
            HealthController local = FindLocal();
            if (local != null && local.currentHealth > 0f && local.maxHealth > 0f)
            {
                float ratio = local.currentHealth / local.maxHealth;
                if (ratio < lowHealthRatio)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulsePerSecond * Mathf.PI * 2f);
                    low = lowHealthAlpha * (1f - ratio / lowHealthRatio * 0.5f) * (0.55f + 0.45f * pulse);
                }
            }

            Paint(Mathf.Max(_hit, low));
        }

        private void Paint(float alpha)
        {
            if (_image != null)
                _image.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
        }

        private HealthController FindLocal()
        {
            if (_local != null && _local.Object != null && _local.Object.IsValid)
                return _local;

            if (Time.unscaledTime < _nextSearch)
                return null;

            _nextSearch = Time.unscaledTime + 0.5f;
            _local = null;
            foreach (HealthController health in FindObjectsByType<HealthController>(FindObjectsSortMode.None))
            {
                if (health.Object != null && health.Object.IsValid && health.HasInputAuthority)
                {
                    _local = health;
                    break;
                }
            }

            return _local;
        }
    }
}
