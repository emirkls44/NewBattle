using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// Yukleme ekrani susu:
    ///   - Ortadaki duz kayisi saat yonunde (saga) doner.
    ///   - Daire dizisi sirayla kayisi turuncusuna dolar; bos olanlar yaprak yesili.
    ///     Istersen ziplama ve dalga da acilabilir (Pop Scale / Wave Height).
    ///   - Alttaki ipucu birkac saniyede bir degisir.
    /// Gercek ilerleme olcmez; baglanti durumu LoadingScreen'in Durum yazisinda.
    /// </summary>
    public class LoadingThemeAnimator : MonoBehaviour
    {
        [Header("Donen Kayisi")]
        [Tooltip("Bossa 'DonenKayisi' adli cocuk kullanilir.")]
        [SerializeField] private RectTransform spinner;
        [Tooltip("Saniyede kac derece saat yonunde (saga) donsun.")]
        [SerializeField] private float spinDegreesPerSecond = 120f;

        [Header("Kayisi Dizisi")]
        [SerializeField] private Image[] apricots;
        [SerializeField] private Color filledTint = new(0.949f, 0.6f, 0.29f, 1f);   // kayisi turuncusu
        [SerializeField] private Color emptyTint = new(0.361f, 0.478f, 0.278f, 1f); // yaprak yesili
        [Tooltip("Bir kayisinin dolma suresi (saniye).")]
        [SerializeField, Min(0.05f)] private float stepSeconds = 0.32f;
        [Tooltip("Dolan kayisinin ziplama buyuklugu.")]
        [SerializeField, Range(0f, 0.8f)] private float popScale;
        [Tooltip("Dizinin dalgalanma yuksekligi (piksel).")]
        [SerializeField, Range(0f, 20f)] private float waveHeight;

        [Header("Ipuclari")]
        [SerializeField] private TextMeshProUGUI tipText;
        [SerializeField, Min(1f)] private float tipSeconds = 3.5f;
        [SerializeField, TextArea] private string[] tips =
        {
            "İpucu: Kayısı atınca takım arkadaşların iyileşir",
            "İpucu: Fındık atınca rakipler neredeyse durur",
            "İpucu: Yere düşen arkadaşının yanında bekle, onu kaldır",
            "İpucu: Daralan alanın dışında canın azalır",
            "İpucu: Tedarik paketinde nadir silahlar çıkar",
            "İpucu: Çimenlerin içinde saklanabilirsin"
        };

        private float _startTime;
        private int _shownTip = -1;
        private Vector2[] _basePositions;
        private float[] _filledAt;

        private void Awake()
        {
            if (spinner == null)
                spinner = transform.Find("DonenKayisi") as RectTransform;

            // Donmeyi bu bilesen yapar; eski UISpin kapatilir (cift donmesin).
            if (spinner != null && spinner.TryGetComponent(out UISpin oldSpin))
                oldSpin.enabled = false;

            if (apricots != null)
            {
                _basePositions = new Vector2[apricots.Length];
                _filledAt = new float[apricots.Length];
                for (int i = 0; i < apricots.Length; i++)
                {
                    if (apricots[i] != null)
                        _basePositions[i] = apricots[i].rectTransform.anchoredPosition;
                    _filledAt[i] = -10f;
                }
            }
        }

        private void OnEnable()
        {
            _startTime = Time.unscaledTime;
            _shownTip = -1;
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            float elapsed = now - _startTime;

            if (spinner != null)
                spinner.localRotation = Quaternion.Euler(0f, 0f, -elapsed * spinDegreesPerSecond);

            UpdateApricots(now, elapsed);

            if (tipText != null && tips != null && tips.Length > 0)
            {
                int index = (int)(elapsed / tipSeconds) % tips.Length;
                if (index != _shownTip)
                {
                    _shownTip = index;
                    tipText.text = tips[index];
                }
            }
        }

        private void UpdateApricots(float now, float elapsed)
        {
            if (apricots == null || apricots.Length == 0 || _basePositions == null)
                return;

            // Dolar, kisa bir an dolu kalir, sonra bastan.
            int cycle = apricots.Length + 2;
            int filled = Mathf.Min(apricots.Length, (int)(elapsed / stepSeconds) % cycle);

            for (int i = 0; i < apricots.Length; i++)
            {
                Image apricot = apricots[i];
                if (apricot == null)
                    continue;

                bool isFilled = i < filled;
                if (isFilled && _filledAt[i] < 0f)
                    _filledAt[i] = now;
                else if (!isFilled)
                    _filledAt[i] = -10f;

                apricot.color = isFilled ? filledTint : emptyTint;

                // Dolunca ziplar: buyuyup yerine oturur.
                float since = _filledAt[i] >= 0f ? now - _filledAt[i] : 10f;
                float pop = since < 0.3f ? Mathf.Sin(since / 0.3f * Mathf.PI) * popScale : 0f;
                apricot.rectTransform.localScale = Vector3.one * (1f + pop);

                // Hafif dalga.
                float wave = Mathf.Sin(elapsed * 4f - i * 0.7f) * waveHeight;
                apricot.rectTransform.anchoredPosition = _basePositions[i] + new Vector2(0f, wave);
            }
        }
    }
}
