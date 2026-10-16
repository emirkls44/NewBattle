using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// AYARLAR penceresi: muzik, efektler, kare hizi ve isim degistirme.
    /// Ana menudeki "ayarlar" butonu acar. Degerler GameSettings'te saklanir.
    /// </summary>
    public class SettingsScreen : MonoBehaviour
    {
        [Header("Muzik")]
        [SerializeField] private Button musicButton;
        [SerializeField] private Image musicFace;
        [SerializeField] private TextMeshProUGUI musicLabel;

        [Header("Efektler")]
        [SerializeField] private Button soundButton;
        [SerializeField] private Image soundFace;
        [SerializeField] private TextMeshProUGUI soundLabel;

        [Header("Kare Hizi")]
        [SerializeField] private Button frameRateButton;
        [SerializeField] private TextMeshProUGUI frameRateLabel;

        [Header("Diger")]
        [SerializeField] private Button nameButton;
        [SerializeField] private NamePopup namePopup;
        [SerializeField] private Button giftCodeButton;
        [SerializeField] private GiftCodePopup giftCodePopup;
        [SerializeField] private TextMeshProUGUI versionText;
        [SerializeField] private Button closeButton;

        [Header("Renkler")]
        [SerializeField] private Color onColor = new(0.36f, 0.77f, 0.22f, 1f);
        [SerializeField] private Color offColor = new(0.6f, 0.6f, 0.62f, 1f);

        [Tooltip("Doluysa acik / kapali dugme bu resimlerle gosterilir (renk boyanmaz).")]
        [SerializeField] private Sprite onSprite;
        [SerializeField] private Sprite offSprite;

        [Tooltip("Kurulum aracinin yerlesim surumu.")]
        [SerializeField, HideInInspector] private int layoutVersion;

        private bool _initialized;
        private bool _opening;

        private void Awake()
        {
            Initialize();

            // Duzenlerken gorunur birakilmis olabilir; oyun kapali baslar.
            if (!_opening)
                gameObject.SetActive(false);
        }

        private void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;

            if (soundButton != null)
                soundButton.onClick.AddListener(() => GameSettings.EffectsOn = !GameSettings.EffectsOn);
            if (musicButton != null)
                musicButton.onClick.AddListener(() => GameSettings.MusicOn = !GameSettings.MusicOn);
            if (frameRateButton != null)
                frameRateButton.onClick.AddListener(() => GameSettings.FrameRate = GameSettings.FrameRate == 60 ? 30 : 60);
            if (nameButton != null)
                nameButton.onClick.AddListener(() =>
                {
                    if (namePopup != null)
                        namePopup.Open();
                });
            if (giftCodeButton != null)
                giftCodeButton.onClick.AddListener(() =>
                {
                    if (giftCodePopup != null)
                        giftCodePopup.Open();
                });
            if (closeButton != null)
                closeButton.onClick.AddListener(Close);
        }

        public void Open()
        {
            Initialize();

            _opening = true;
            gameObject.SetActive(true);
            _opening = false;
            transform.SetAsLastSibling();

            GameSettings.Changed -= Refresh;
            GameSettings.Changed += Refresh;
            Refresh();
        }

        public void Close()
        {
            GameSettings.Changed -= Refresh;
            gameObject.SetActive(false);
        }

        private void OnDestroy() => GameSettings.Changed -= Refresh;

        private void PaintToggle(TextMeshProUGUI label, Image face, bool on)
        {
            if (label != null)
                label.text = on ? "AÇIK" : "KAPALI";
            if (face == null)
                return;

            if (onSprite != null && offSprite != null)
            {
                face.sprite = on ? onSprite : offSprite;
                face.color = Color.white;
            }
            else
            {
                face.color = on ? onColor : offColor;
            }
        }

        private void Refresh()
        {
            PaintToggle(soundLabel, soundFace, GameSettings.EffectsOn);
            PaintToggle(musicLabel, musicFace, GameSettings.MusicOn);

            if (frameRateLabel != null)
                frameRateLabel.text = $"{GameSettings.FrameRate} FPS";

            if (versionText != null)
                versionText.text = $"Sürüm {Application.version}";
        }
    }
}
