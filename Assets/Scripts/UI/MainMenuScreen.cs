using System.Collections;
using NewBattle.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// Ana menu ekrani (Battlelands tarzi).
    ///
    /// Eski NetworkMenuUI + MainMenuFlow ikilisinin yerini alir. Gorunum
    /// "Tools > NewBattle > Menu > 1 - Ana Menuyu Kur" ile sahneye kurulur;
    /// sonrasinda yerlesimi, renkleri ve yazilari Inspector'dan degistirmek
    /// serbest - bu script sadece degerleri doldurur ve butonlari dinler.
    /// </summary>
    public class MainMenuScreen : MonoBehaviour
    {
        private const string PlayerNameKey = "nb_player_name";

        [Header("Butonlar")]
        [SerializeField] private Button playButton;
        [Tooltip("Henuz islevi olmayan butonlar. Basinca 'YAKINDA' bildirimi cikar.")]
        [SerializeField] private Button[] comingSoonButtons;

        [Header("Yazilar")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private TextMeshProUGUI coinText;
        [SerializeField] private TextMeshProUGUI seasonText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI rankText;
        [SerializeField] private TextMeshProUGUI xpText;
        [SerializeField] private TextMeshProUGUI nextLevelText;
        [SerializeField] private TextMeshProUGUI onlineText;
        [SerializeField] private TextMeshProUGUI modeChipText;

        [Header("XP Bari")]
        [Tooltip("Dolum cubugu. Genisligi anchorMax.x ile ayarlanir.")]
        [SerializeField] private RectTransform xpFill;

        [Header("Bildirim")]
        [SerializeField] private CanvasGroup toastGroup;
        [SerializeField] private TextMeshProUGUI toastText;

        [Header("Gecici degerler")]
        [Tooltip("Seviye, XP, para ve galibiyet PlayerProgress'ten gelir. " +
                 "Test icin: Tools > NewBattle > Oyuncu Profili (Test).")]
        [SerializeField, Min(1)] private int seasonNumber = 1;
        [SerializeField, Min(0)] private int onlinePlayers = 1;

        [Header("Mod")]
        [Tooltip("OYNA'ya basilinca baslayacak mod. Asama 2'de mod secim ekrani gelecek.")]
        [SerializeField] private BattleGameMode defaultMode = BattleGameMode.Bots;
        [SerializeField] private string defaultModeLabel = "SOLO";

        private NetworkBootstrap _bootstrap;
        private Coroutine _toastRoutine;
        private float _nextBootstrapCheck;

        public string PlayerName { get; private set; }

        private void Awake()
        {
            _bootstrap = FindBootstrap();

            // Fusion maci baslatirken sahneyi yeniden yukler. Yeni yuklenen
            // sahnede menu tekrar acilmasin.
            if (_bootstrap != null && _bootstrap.IsRunning)
                gameObject.SetActive(false);
        }

        private void Start()
        {
            if (_bootstrap == null)
            {
                Debug.LogError("MainMenuScreen: NetworkBootstrap bulunamadi.");
                SetStatus("NETWORK BOOTSTRAP BULUNAMADI");
                if (playButton != null)
                    playButton.interactable = false;
                return;
            }

            _bootstrap.StatusChanged += SetStatus;
            _bootstrap.ConnectionCompleted += HandleConnectionCompleted;

            if (playButton != null)
                playButton.onClick.AddListener(OnPlayPressed);

            if (comingSoonButtons != null)
            {
                foreach (Button button in comingSoonButtons)
                {
                    if (button != null)
                        button.onClick.AddListener(() => ShowToast("YAKINDA!"));
                }
            }

            PlayerName = LoadOrCreatePlayerName();
            PlayerProgress.Changed += Refresh;
            Refresh();
            SetStatus(string.Empty);
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextBootstrapCheck)
                return;

            _nextBootstrapCheck = Time.unscaledTime + 0.25f;

            // Mac baslarken Fusion sahneyi yeniden yukler; yeni sahnedeki menu
            // ilk anda calisan oturumu goremeyebilir. Oturum calisiyorsa menu
            // ekranda kalmamali.
            NetworkBootstrap running = FindBootstrap();
            if (running != null && running.IsRunning)
                gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            PlayerProgress.Changed -= Refresh;

            if (_bootstrap == null)
                return;

            _bootstrap.StatusChanged -= SetStatus;
            _bootstrap.ConnectionCompleted -= HandleConnectionCompleted;
        }

        /// <summary>Profil degerlerini ekrana yazar.</summary>
        public void Refresh()
        {
            int level = PlayerProgress.Level;
            int xp = PlayerProgress.Xp;
            int xpForNextLevel = PlayerProgress.XpForNextLevel(level);

            SetText(playerNameText, PlayerName);
            SetText(coinText, PlayerProgress.Coins.ToString());
            SetText(seasonText, $"SEZON {seasonNumber}");
            SetText(levelText, $"SEVİYE {level}");
            SetText(rankText, PlayerProgress.RankName(level));
            SetText(xpText, $"{xp} / {xpForNextLevel}");
            SetText(nextLevelText, (level + 1).ToString());
            SetText(onlineText, $"{onlinePlayers} Online Savaşçılar");
            SetText(modeChipText, defaultModeLabel);

            if (xpFill != null)
            {
                float fill = Mathf.Clamp01(xp / (float)Mathf.Max(1, xpForNextLevel));
                xpFill.anchorMax = new Vector2(fill, xpFill.anchorMax.y);
            }
        }

        public void StartMatch(BattleGameMode mode)
        {
            if (_bootstrap == null || _bootstrap.IsRunning)
                return;

            if (playButton != null)
                playButton.interactable = false;

            _bootstrap.StartMatchmaking(mode);
        }

        public void ShowToast(string message)
        {
            if (toastGroup == null || toastText == null)
                return;

            toastText.text = message;

            if (_toastRoutine != null)
                StopCoroutine(_toastRoutine);

            _toastRoutine = StartCoroutine(ToastRoutine());
        }

        private void OnPlayPressed()
        {
            StartMatch(defaultMode);
        }

        private void HandleConnectionCompleted(bool success)
        {
            if (success)
            {
                gameObject.SetActive(false);
                return;
            }

            if (playButton != null)
                playButton.interactable = true;
        }

        private void SetStatus(string message)
        {
            SetText(statusText, message);
        }

        private IEnumerator ToastRoutine()
        {
            const float fade = 0.15f;
            const float hold = 1.2f;

            for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
            {
                toastGroup.alpha = t / fade;
                yield return null;
            }

            toastGroup.alpha = 1f;
            yield return new WaitForSecondsRealtime(hold);

            for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
            {
                toastGroup.alpha = 1f - t / fade;
                yield return null;
            }

            toastGroup.alpha = 0f;
            _toastRoutine = null;
        }

        private static string LoadOrCreatePlayerName()
        {
            string saved = PlayerPrefs.GetString(PlayerNameKey, string.Empty);
            if (!string.IsNullOrEmpty(saved))
                return saved;

            string created = $"Savaşçı#{Random.Range(1000, 10000)}";
            PlayerPrefs.SetString(PlayerNameKey, created);
            PlayerPrefs.Save();
            return created;
        }

        /// <summary>
        /// Calisan bir bootstrap varsa onu tercih eder: mac baslarken sahne
        /// yeniden yuklenince sahnede ikinci, calismayan bir kopya olabilir.
        /// </summary>
        private static NetworkBootstrap FindBootstrap()
        {
            NetworkBootstrap[] all = FindObjectsByType<NetworkBootstrap>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            NetworkBootstrap fallback = null;
            foreach (NetworkBootstrap candidate in all)
            {
                if (candidate.IsRunning)
                    return candidate;

                fallback ??= candidate;
            }

            return fallback;
        }

        private static void SetText(TextMeshProUGUI target, string value)
        {
            if (target != null)
                target.text = value;
        }
    }
}
