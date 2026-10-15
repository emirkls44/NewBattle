using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// Yukleme ekrani susu: kayisi dizisi sirayla dolup bosalir (ilerleme hissi) ve
    /// alttaki ipucu birkac saniyede bir degisir. Gercek ilerleme olcmez; baglanti
    /// durumu LoadingScreen'in Durum yazisinda.
    /// </summary>
    public class LoadingThemeAnimator : MonoBehaviour
    {
        [Header("Kayisi Dizisi")]
        [SerializeField] private Image[] apricots;
        [SerializeField] private Color filledColor = Color.white;
        [SerializeField] private Color emptyColor = new(0.36f, 0.48f, 0.28f, 0.55f);
        [Tooltip("Bir kayisinin dolma suresi (saniye).")]
        [SerializeField, Min(0.05f)] private float stepSeconds = 0.35f;

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

        private void OnEnable()
        {
            _startTime = Time.unscaledTime;
            _shownTip = -1;
        }

        private void Update()
        {
            float elapsed = Time.unscaledTime - _startTime;

            if (apricots != null && apricots.Length > 0)
            {
                // Dolar, kisa bir an dolu kalir, sonra bastan.
                int cycle = apricots.Length + 2;
                int filled = Mathf.Min(apricots.Length, (int)(elapsed / stepSeconds) % cycle);
                for (int i = 0; i < apricots.Length; i++)
                {
                    if (apricots[i] == null)
                        continue;

                    Color target = i < filled ? filledColor : emptyColor;
                    if (apricots[i].color != target)
                        apricots[i].color = target;
                }
            }

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
    }
}
