using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// "Oyun modu" penceresi: SOLO / RASTGELE IKILI / ARKADAS IKILISI secimi
    /// ve CEVRIMICI OYNA. Ana menudeki OYNA butonu acar.
    ///
    /// Secim cihazda saklanir; oyuncu bir dahaki acilista ayni modu secili bulur.
    /// Kilitli secenek ("YAKINDA") secilemez.
    /// </summary>
    public class ModeSelectPanel : MonoBehaviour
    {
        private const string SelectedModeKey = "nb_selected_mode";

        [Serializable]
        public class ModeOption
        {
            public string id = "solo";
            [Tooltip("Ana menudeki OYNA butonunun ustunde yazan kisa ad.")]
            public string shortLabel = "SOLO";
            public BattleGameMode mode = BattleGameMode.Bots;
            [Tooltip("Isaretliyse secilemez, basinca 'YAKINDA' bildirimi cikar.")]
            public bool locked;

            [Header("Gorunum")]
            public Button button;
            public Image background;
            public TextMeshProUGUI label;
            public Image icon;
            public Image checkBox;
            public GameObject checkMark;
            public GameObject lockMark;
        }

        [SerializeField] private ModeOption[] options;

        [Header("Butonlar")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button closeButton;
        [Tooltip("Pencerenin disindaki karanlik alana basinca da kapansin.")]
        [SerializeField] private Button backdropButton;

        [Header("Renkler")]
        [SerializeField] private Color selectedColor = new(0.36f, 0.77f, 0.22f, 1f);
        [SerializeField] private Color normalColor = new(0.87f, 0.87f, 0.87f, 1f);
        [SerializeField] private Color selectedTextColor = Color.white;
        [SerializeField] private Color normalTextColor = new(0.33f, 0.33f, 0.36f, 1f);
        [SerializeField] private Color selectedBoxColor = Color.white;
        [SerializeField] private Color normalBoxColor = new(0.64f, 0.64f, 0.66f, 1f);

        [Header("Savas Ipuclari")]
        [SerializeField] private TextMeshProUGUI tipText;
        [SerializeField, TextArea(2, 4)] private string[] tips =
        {
            "Arkadaşlarını Arkadaş İkilisi'ne çağır ve onlarla oyna!",
            "Çalıların içine saklanınca rakipler seni göremez.",
            "Güvenli alan daralıyor: kırmızı çizginin dışında kalma!",
            "Havadan düşen sandıklarda en iyi silahlar var.",
            "Paraşütle inerken joystick ile yönünü değiştirebilirsin."
        };
        [SerializeField, Min(1f)] private float tipSeconds = 4f;

        [Header("Animasyon")]
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform window;
        [SerializeField, Min(0.01f)] private float openSeconds = 0.22f;

        /// <summary>CEVRIMICI OYNA'ya basildi.</summary>
        public event Action<ModeOption> PlayRequested;

        /// <summary>Secim degisti (OYNA butonundaki mod etiketi icin).</summary>
        public event Action<ModeOption> SelectionChanged;

        /// <summary>Kilitli secenege basildi.</summary>
        public event Action<ModeOption> LockedPressed;

        public ModeOption Selected { get; private set; }
        public bool IsOpen { get; private set; }

        private Coroutine _animation;
        private int _tipIndex;
        private float _nextTipTime;
        private bool _initialized;

        private void Awake()
        {
            Initialize();

            // Duzenlerken gorunur birakilmis olabilir (Ekran Duzenleyici);
            // oyun her zaman pencere kapali baslar.
            if (!IsOpen)
            {
                if (group != null)
                {
                    group.alpha = 0f;
                    group.blocksRaycasts = false;
                }

                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Butonlari baglar ve kayitli secimi yukler. Pencere kapali (inaktif)
        /// baslar, Awake ilk acilisa kadar calismaz; menu bu yuzden en basta
        /// bunu kendisi cagirir ki OYNA'daki mod etiketi dogru olsun.
        /// </summary>
        public void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;

            for (int i = 0; i < options.Length; i++)
            {
                ModeOption option = options[i];
                if (option.button != null)
                    option.button.onClick.AddListener(() => OnOptionPressed(option));
            }

            if (playButton != null)
                playButton.onClick.AddListener(() =>
                {
                    if (Selected != null)
                        PlayRequested?.Invoke(Selected);
                });

            if (closeButton != null)
                closeButton.onClick.AddListener(Close);
            if (backdropButton != null)
                backdropButton.onClick.AddListener(Close);

            string savedId = PlayerPrefs.GetString(SelectedModeKey, string.Empty);
            Select(Find(savedId) ?? FirstUnlocked(), save: false);
        }

        private void Update()
        {
            if (!IsOpen || tipText == null || tips == null || tips.Length == 0)
                return;

            if (Time.unscaledTime < _nextTipTime)
                return;

            _tipIndex = (_tipIndex + 1) % tips.Length;
            ShowTip();
        }

        public void Open()
        {
            gameObject.SetActive(true);
            IsOpen = true;
            transform.SetAsLastSibling();

            _tipIndex = UnityEngine.Random.Range(0, Mathf.Max(1, tips.Length));
            ShowTip();
            Animate(true);
        }

        public void Close()
        {
            if (!IsOpen)
                return;

            IsOpen = false;
            Animate(false);
        }

        /// <summary>Animasyonsuz kapatir (mac baslarken).</summary>
        public void Hide()
        {
            IsOpen = false;
            if (_animation != null)
                StopCoroutine(_animation);
            gameObject.SetActive(false);
        }

        private void OnOptionPressed(ModeOption option)
        {
            if (option.locked)
            {
                LockedPressed?.Invoke(option);
                return;
            }

            Select(option, save: true);
        }

        private void Select(ModeOption option, bool save)
        {
            if (option == null)
                return;

            Selected = option;

            foreach (ModeOption candidate in options)
                Paint(candidate, candidate == option);

            if (save)
            {
                PlayerPrefs.SetString(SelectedModeKey, option.id);
                PlayerPrefs.Save();
            }

            SelectionChanged?.Invoke(option);
        }

        private void Paint(ModeOption option, bool selected)
        {
            if (option.background != null)
                option.background.color = selected ? selectedColor : normalColor;
            if (option.label != null)
                option.label.color = selected ? selectedTextColor : normalTextColor;
            if (option.icon != null)
                option.icon.color = selected ? selectedTextColor : normalTextColor;
            if (option.checkBox != null)
                option.checkBox.color = selected ? selectedBoxColor : normalBoxColor;
            if (option.checkMark != null)
                option.checkMark.SetActive(selected);
            if (option.lockMark != null)
                option.lockMark.SetActive(option.locked);
        }

        private void ShowTip()
        {
            if (tipText != null && tips != null && tips.Length > 0)
                tipText.text = tips[Mathf.Clamp(_tipIndex, 0, tips.Length - 1)];

            _nextTipTime = Time.unscaledTime + tipSeconds;
        }

        private ModeOption Find(string id)
        {
            foreach (ModeOption option in options)
            {
                if (option.id == id && !option.locked)
                    return option;
            }

            return null;
        }

        private ModeOption FirstUnlocked()
        {
            foreach (ModeOption option in options)
            {
                if (!option.locked)
                    return option;
            }

            return options.Length > 0 ? options[0] : null;
        }

        private void Animate(bool opening)
        {
            if (_animation != null)
                StopCoroutine(_animation);

            _animation = StartCoroutine(AnimateRoutine(opening));
        }

        private IEnumerator AnimateRoutine(bool opening)
        {
            float from = opening ? 0f : 1f;
            float to = opening ? 1f : 0f;
            float duration = opening ? openSeconds : openSeconds * 0.7f;

            if (group != null)
                group.blocksRaycasts = opening;

            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                Apply(Mathf.Lerp(from, to, t / duration), opening);
                yield return null;
            }

            Apply(to, opening);
            _animation = null;

            if (!opening)
                gameObject.SetActive(false);
        }

        private void Apply(float progress, bool opening)
        {
            if (group != null)
                group.alpha = progress;

            if (window == null)
                return;

            // Acilirken hafif "zipla" (ease-out-back), kapanirken duz kucul.
            float scale = opening ? Mathf.LerpUnclamped(0.85f, 1f, EaseOutBack(progress)) : Mathf.Lerp(0.9f, 1f, progress);
            window.localScale = new Vector3(scale, scale, 1f);
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }
}
