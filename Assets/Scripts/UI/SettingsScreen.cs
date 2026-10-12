using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// AYARLAR penceresi: ses, kare hizi, kamera yakinligi ve isim degistirme.
    /// Ana menudeki "ayarlar" butonu acar. Degerler GameSettings'te saklanir.
    /// </summary>
    public class SettingsScreen : MonoBehaviour
    {
        [Header("Ses")]
        [SerializeField] private Button soundButton;
        [SerializeField] private Image soundFace;
        [SerializeField] private TextMeshProUGUI soundLabel;

        [Header("Kare Hizi")]
        [SerializeField] private Button frameRateButton;
        [SerializeField] private TextMeshProUGUI frameRateLabel;

        [Header("Kamera Yakinligi")]
        [SerializeField] private Button cameraFartherButton;
        [SerializeField] private Button cameraCloserButton;
        [SerializeField] private TextMeshProUGUI cameraLabel;
        [Tooltip("Oyuncu hic degistirmediyse gosterilen deger (CameraFollow'daki ile ayni olsun).")]
        [SerializeField] private float defaultCameraSize = 8.5f;

        [Header("Diger")]
        [SerializeField] private Button nameButton;
        [SerializeField] private NamePopup namePopup;
        [SerializeField] private TextMeshProUGUI versionText;
        [SerializeField] private Button closeButton;

        [Header("Renkler")]
        [SerializeField] private Color onColor = new(0.36f, 0.77f, 0.22f, 1f);
        [SerializeField] private Color offColor = new(0.6f, 0.6f, 0.62f, 1f);

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
                soundButton.onClick.AddListener(() => GameSettings.SoundOn = !GameSettings.SoundOn);
            if (frameRateButton != null)
                frameRateButton.onClick.AddListener(() => GameSettings.FrameRate = GameSettings.FrameRate == 60 ? 30 : 60);
            if (cameraCloserButton != null)
                cameraCloserButton.onClick.AddListener(() => ChangeCamera(-GameSettings.CameraStep));
            if (cameraFartherButton != null)
                cameraFartherButton.onClick.AddListener(() => ChangeCamera(GameSettings.CameraStep));
            if (nameButton != null)
                nameButton.onClick.AddListener(() =>
                {
                    if (namePopup != null)
                        namePopup.Open();
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

        private float CurrentCamera => GameSettings.HasCameraViewSize ? GameSettings.CameraViewSize : defaultCameraSize;

        private void ChangeCamera(float delta)
        {
            GameSettings.CameraViewSize = CurrentCamera + delta;
        }

        private void Refresh()
        {
            bool sound = GameSettings.SoundOn;
            if (soundLabel != null)
                soundLabel.text = sound ? "AÇIK" : "KAPALI";
            if (soundFace != null)
                soundFace.color = sound ? onColor : offColor;

            if (frameRateLabel != null)
                frameRateLabel.text = $"{GameSettings.FrameRate} FPS";

            float size = CurrentCamera;
            int level = GameSettings.CameraLevel(size);
            int maxLevel = GameSettings.CameraLevel(GameSettings.CameraMin);
            if (cameraLabel != null)
                cameraLabel.text = $"{level} / {maxLevel}";

            if (cameraCloserButton != null)
                cameraCloserButton.interactable = size > GameSettings.CameraMin + 0.01f;
            if (cameraFartherButton != null)
                cameraFartherButton.interactable = size < GameSettings.CameraMax - 0.01f;

            if (versionText != null)
                versionText.text = $"Sürüm {Application.version}";
        }
    }
}
