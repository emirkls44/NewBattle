using System;
using System.Collections;
using NewBattle.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// TAKIMLA OYNA penceresi (oyun modu ekraninin boyunda, ziplayarak acilir):
    ///   - ODA KUR: oyuncu sayisi (katilan kadar / 4 / 8 / 16) ve takim boyu (2 / 4)
    ///     secilir, 4 haneli kodlu gizli oda kurulur. Kodu arkadaslarina soyle.
    ///   - KODLA KATIL: arkadasinin kodunu yaz, onun odasina gir.
    /// Odada takimlar secilir, herkes hazir olunca kurucu baslatir (TeamRoomScreen).
    /// Kapatinca oyun modu ekranina geri donulur.
    /// </summary>
    public class FriendRoomPopup : MonoBehaviour
    {
        public const int CodeLength = 4;

        private const string SizePrefKey = "nb_room_size";
        private const string TeamPrefKey = "nb_room_team";

        [Serializable]
        public class Choice
        {
            [Tooltip("Oyuncu sayisi icin 0 = katilan oyuncu kadar; takim boyu icin 2 / 4.")]
            public int value;
            public Button button;
            public Image face;
            public TextMeshProUGUI label;
        }

        [SerializeField] private Button createButton;
        [SerializeField] private TMP_InputField codeInput;
        [SerializeField] private Button joinButton;
        [SerializeField] private TextMeshProUGUI hintText;
        [SerializeField] private Button closeButton;
        [SerializeField] private MainMenuScreen menu;

        [Header("Oda ayarlari")]
        [SerializeField] private Choice[] sizeChoices;
        [SerializeField] private Choice[] teamSizeChoices;
        [Tooltip("Altta: bos yerlere bot dogacagi bilgisi.")]
        [SerializeField] private TextMeshProUGUI infoText;

        [Header("Renkler")]
        [SerializeField] private Color selectedColor = new(0.36f, 0.77f, 0.22f, 1f);
        [SerializeField] private Color normalColor = new(0.32f, 0.24f, 0.52f, 1f);
        [SerializeField] private Color disabledColor = new(0.25f, 0.2f, 0.35f, 0.6f);
        [SerializeField] private Color errorColor = new(1f, 0.45f, 0.4f, 1f);

        [Header("Animasyon")]
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform window;
        [SerializeField, Min(0.01f)] private float openSeconds = 0.22f;

        [SerializeField, HideInInspector] private int layoutVersion;

        private Color _hintColor;
        private bool _initialized;
        private bool _opening;
        private Coroutine _animation;

        public static int SavedSizeChoice
        {
            get => PlayerPrefs.GetInt(SizePrefKey, 8);
            private set { PlayerPrefs.SetInt(SizePrefKey, value); PlayerPrefs.Save(); }
        }

        public static int SavedTeamSize
        {
            get => Mathf.Max(2, PlayerPrefs.GetInt(TeamPrefKey, 2));
            private set { PlayerPrefs.SetInt(TeamPrefKey, value); PlayerPrefs.Save(); }
        }

        private void Awake()
        {
            Initialize();

            if (!_opening)
                gameObject.SetActive(false);
        }

        private void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;

            if (hintText != null)
                _hintColor = hintText.color;

            if (codeInput != null)
            {
                codeInput.characterLimit = CodeLength;
                codeInput.contentType = TMP_InputField.ContentType.IntegerNumber;
                codeInput.onSubmit.AddListener(_ => Join());
                codeInput.onValueChanged.AddListener(_ => SetHint("Arkadaşının söylediği 4 haneli kodu yaz", false));
            }

            if (createButton != null)
                createButton.onClick.AddListener(Create);
            if (joinButton != null)
                joinButton.onClick.AddListener(Join);
            if (closeButton != null)
                closeButton.onClick.AddListener(CloseToModes);

            if (sizeChoices != null)
                foreach (Choice choice in sizeChoices)
                    if (choice.button != null)
                        choice.button.onClick.AddListener(() => PickSize(choice.value));

            if (teamSizeChoices != null)
                foreach (Choice choice in teamSizeChoices)
                    if (choice.button != null)
                        choice.button.onClick.AddListener(() => PickTeamSize(choice.value));
        }

        public void Open()
        {
            Initialize();

            _opening = true;
            gameObject.SetActive(true);
            _opening = false;
            transform.SetAsLastSibling();

            if (codeInput != null)
                codeInput.text = string.Empty;

            SetHint("Arkadaşlarınla takım kur, birlikte oyna!", false);
            Refresh();
            Animate(true);
        }

        /// <summary>Kapat ve oyun modu ekranina don.</summary>
        private void CloseToModes()
        {
            Close();
            if (menu != null)
                menu.ReopenModePanel();
        }

        public void Close()
        {
            if (!gameObject.activeSelf)
                return;

            Animate(false);
        }

        // ------------------------------------------------------------------
        // Ayarlar
        // ------------------------------------------------------------------

        /// <summary>En az iki takim olmali: 4 kisilik oda 4'lu takimla olmaz.</summary>
        private static bool Valid(int size, int teamSize) => size == 0 || size / Mathf.Max(1, teamSize) >= 2;

        private void PickSize(int size)
        {
            if (!Valid(size, SavedTeamSize))
            {
                SetHint("4 kişilik odada takımlar 2 kişilik olmalı", true);
                Sfx.PlayUI("hata");
                return;
            }

            SavedSizeChoice = size;
            Sfx.PlayUI("kart");
            Refresh();
        }

        private void PickTeamSize(int teamSize)
        {
            SavedTeamSize = teamSize;
            if (!Valid(SavedSizeChoice, teamSize))
                SavedSizeChoice = 8;

            Sfx.PlayUI("kart");
            Refresh();
        }

        private void Refresh()
        {
            int size = SavedSizeChoice;
            int team = SavedTeamSize;

            if (sizeChoices != null)
                foreach (Choice choice in sizeChoices)
                    Paint(choice, choice.value == size, Valid(choice.value, team));

            if (teamSizeChoices != null)
                foreach (Choice choice in teamSizeChoices)
                    Paint(choice, choice.value == team, true);

            if (infoText != null)
                infoText.text = InfoFor(size, team);
        }

        public static string InfoFor(int size, int teamSize)
        {
            if (size == 0)
                return $"Sadece odaya katılan gerçek oyuncular oynar, bot yok. Takımlar {teamSize} kişilik.";

            return $"Maç {size} kişilik, takımlar {teamSize} kişilik. Yeterli gerçek oyuncu yoksa " +
                   "boş yerlere bot doğar.";
        }

        private void Paint(Choice choice, bool selected, bool enabled)
        {
            if (choice.face != null)
                choice.face.color = !enabled ? disabledColor : selected ? selectedColor : normalColor;
            if (choice.label != null)
                choice.label.alpha = enabled ? 1f : 0.45f;
        }

        // ------------------------------------------------------------------
        // Oda kur / katil
        // ------------------------------------------------------------------

        private void Create()
        {
            string code = UnityEngine.Random.Range(1000, 10000).ToString();
            Close();
            if (menu != null)
                menu.StartFriendRoom(true, code, SavedSizeChoice, SavedTeamSize);
        }

        private void Join()
        {
            string code = codeInput != null ? codeInput.text.Trim() : string.Empty;
            if (code.Length != CodeLength)
            {
                SetHint($"Kod {CodeLength} haneli olmalı", true);
                Sfx.PlayUI("hata");
                return;
            }

            Close();
            if (menu != null)
                menu.StartFriendRoom(false, code);
        }

        private void SetHint(string text, bool error)
        {
            if (hintText == null)
                return;

            hintText.text = text;
            hintText.color = error ? errorColor : _hintColor;
        }

        // ------------------------------------------------------------------
        // Acilis / kapanis animasyonu (oyun modu ekraniyla ayni "zipla")
        // ------------------------------------------------------------------

        private void Animate(bool opening)
        {
            if (_animation != null)
                StopCoroutine(_animation);

            if (!isActiveAndEnabled)
            {
                gameObject.SetActive(opening);
                return;
            }

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

            float scale = opening ? Mathf.LerpUnclamped(0.85f, 1f, EaseOutBack(progress)) : Mathf.Lerp(0.9f, 1f, progress);
            window.localScale = UIBaseScale.Get(window) * scale;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }
}
