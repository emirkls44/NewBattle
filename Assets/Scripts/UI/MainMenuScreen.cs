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
    /// "Tools > NewBattle > Menu > Kur" ile sahneye kurulur;
    /// sonrasinda yerlesimi, renkleri ve yazilari Inspector'dan degistirmek
    /// serbest - bu script sadece degerleri doldurur ve butonlari dinler.
    /// </summary>
    public class MainMenuScreen : MonoBehaviour
    {
        [Header("Butonlar")]
        [SerializeField] private Button playButton;
        [Tooltip("Eski kurulumdan kalan liste. 'Menü > Kur' bunlari MenuButton'a cevirip bosaltir.")]
        [SerializeField, HideInInspector] private Button[] comingSoonButtons;

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

        [Header("Oduller")]
        [Tooltip("Gunluk giris, ilk oyun hediyesi ve seviye odullerini gosteren pencere.")]
        [SerializeField] private RewardPopup rewardPopup;
        [Tooltip("Menu acildiktan kac saniye sonra odul penceresi gelsin.")]
        [SerializeField, Min(0f)] private float rewardDelay = 0.5f;

        [Header("Iki Kat XP")]
        [Tooltip("Cift XP etkinken 'Sonraki N maç çift XP!' yazan yazilar.")]
        [SerializeField] private TextMeshProUGUI[] doubleXpTexts;
        [SerializeField] private string doubleXpActiveFormat = "Sonraki {0} maç çift XP!";
        [Tooltip("Biriken cift XP maclarini gosteren kutucuklar (oyun modu penceresi).")]
        [SerializeField] private Image[] doubleXpSteps;
        [SerializeField] private Color doubleXpStepOn = new(1f, 0.78f, 0.1f, 1f);
        [SerializeField] private Color doubleXpStepOff = new(0.18f, 0.24f, 0.27f, 1f);

        [Header("Alt Ekranlar")]
        [Tooltip("'karakter' butonunun actigi lobi animasyonu secim ekrani.")]
        [SerializeField] private LobbyAnimationScreen characterScreen;
        [Tooltip("'magaza', 'para_ekle' ve 'para_paneli' butonlarinin actigi magaza.")]
        [SerializeField] private ShopScreen shopScreen;
        [Tooltip("'ayarlar' butonunun actigi pencere.")]
        [SerializeField] private SettingsScreen settingsScreen;
        [Tooltip("'isim_duzenle' (kalem) butonunun actigi pencere.")]
        [SerializeField] private NamePopup namePopup;
        [Tooltip("'sezon' karti ve 'seviye_yolu' (rutbe ikonu) butonlarinin actigi ekran.")]
        [SerializeField] private LevelRoadScreen levelRoadScreen;

        [Header("Yukleme Ekrani")]
        [Tooltip("Mac aranirken gosterilen ekran (Assets/Prefabs/UI/YuklemeEkrani).")]
        [SerializeField] private LoadingScreen loadingScreenPrefab;

        [Header("Mod")]
        [Tooltip("OYNA'ya basinca acilan oyun modu penceresi.")]
        [SerializeField] private ModeSelectPanel modePanel;
        [Tooltip("Pencere yoksa OYNA dogrudan bu modla baslar.")]
        [SerializeField] private BattleGameMode defaultMode = BattleGameMode.Bots;
        [SerializeField] private string defaultModeLabel = "SOLO";

        private NetworkBootstrap _bootstrap;
        private Coroutine _toastRoutine;
        private string[] _doubleXpIdleTexts;
        private readonly System.Collections.Generic.List<(string, System.Action)> _boundActions = new();
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

            // Islevi henuz baglanmamis butonlar (bkz. MenuButton) "YAKINDA!" der.
            MenuButton.Unhandled += OnUnhandledButton;

            // Baglanan butonlar.
            if (characterScreen != null)
                Bind("karakter", characterScreen.Open);

            if (shopScreen != null)
            {
                Bind("magaza", shopScreen.Open);
                Bind("para_ekle", shopScreen.OpenCoins);
                Bind("para_paneli", shopScreen.OpenCoins);
            }

            if (settingsScreen != null)
                Bind("ayarlar", settingsScreen.Open);

            if (namePopup != null)
                Bind("isim_duzenle", namePopup.Open);

            if (levelRoadScreen != null)
            {
                Bind("sezon", levelRoadScreen.Open);
                Bind("seviye_yolu", levelRoadScreen.Open);
            }

            if (rewardPopup != null)
                Bind("oduller", OpenDailyRewards);

            Bind("cift_xp", OnDoubleXpPressed);
            Bind("cift_xp_kart", OnDoubleXpPressed);
            CacheDoubleXpTexts();

            if (comingSoonButtons != null)
            {
                foreach (Button button in comingSoonButtons)
                {
                    if (button != null && button.GetComponent<MenuButton>() == null)
                        button.onClick.AddListener(() => ShowToast("YAKINDA!"));
                }
            }

            if (modePanel != null)
            {
                modePanel.Initialize();
                modePanel.PlayRequested += OnModePlayRequested;
                modePanel.SelectionChanged += OnModeSelectionChanged;
                modePanel.LockedPressed += OnLockedModePressed;
            }

            PlayerName = PlayerProgress.PlayerName;
            PlayerProgress.Changed += Refresh;
            Refresh();
            SetStatus(string.Empty);

            if (rewardPopup != null)
                StartCoroutine(ShowPendingRewards());
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
            MenuButton.Unhandled -= OnUnhandledButton;
            foreach ((string id, System.Action handler) in _boundActions)
                MenuButton.Unregister(id, handler);
            _boundActions.Clear();

            if (modePanel != null)
            {
                modePanel.PlayRequested -= OnModePlayRequested;
                modePanel.SelectionChanged -= OnModeSelectionChanged;
                modePanel.LockedPressed -= OnLockedModePressed;
            }

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

            PlayerName = PlayerProgress.PlayerName;
            SetText(playerNameText, PlayerName);
            SetText(coinText, PlayerProgress.Coins.ToString());
            SetText(seasonText, $"SEZON {seasonNumber}");
            SetText(levelText, $"SEVİYE {level}");
            SetText(rankText, PlayerProgress.RankName(level));
            SetText(xpText, $"{xp} / {xpForNextLevel}");
            SetText(nextLevelText, (level + 1).ToString());
            SetText(onlineText, $"{onlinePlayers} Online Savaşçılar");
            SetText(modeChipText, modePanel != null && modePanel.Selected != null
                ? modePanel.Selected.shortLabel
                : defaultModeLabel);

            if (xpFill != null)
            {
                float fill = Mathf.Clamp01(xp / (float)Mathf.Max(1, xpForNextLevel));
                xpFill.anchorMax = new Vector2(fill, xpFill.anchorMax.y);
            }

            RefreshDoubleXp();
        }

        // ------------------------------------------------------------------
        // Oduller
        // ------------------------------------------------------------------

        /// <summary>
        /// Once gunluk giris odulu (bugun alinmadiysa), sonra bekleyen
        /// bildirimler (ilk oyun hediyesi, seviye odulleri) sirayla gosterilir.
        /// </summary>
        private IEnumerator ShowPendingRewards()
        {
            yield return new WaitForSecondsRealtime(rewardDelay);

            while (isActiveAndEnabled)
            {
                // Oyuncu bu arada OYNA'ya basip mod penceresini actiysa bekle.
                if (modePanel != null && modePanel.IsOpen)
                {
                    yield return null;
                    continue;
                }

                bool closed = false;

                if (DailyLogin.CanClaim(out int day, out _))
                {
                    rewardPopup.ShowDaily(day, ProgressionSettings.Instance.dailyLoginXp, () =>
                    {
                        DailyLogin.Claim();
                        closed = true;
                    });
                }
                else
                {
                    RewardNotice notice = RewardInbox.TakeNext();
                    if (notice == null)
                        yield break;

                    rewardPopup.ShowNotice(notice, () => closed = true);
                }

                while (!closed)
                    yield return null;
            }
        }

        // ------------------------------------------------------------------
        // Iki Kat XP
        // ------------------------------------------------------------------

        /// <summary>Eylem kimligine islev baglar; menu kapanirken hepsi geri alinir.</summary>
        private void Bind(string actionId, System.Action handler)
        {
            MenuButton.Register(actionId, handler);
            _boundActions.Add((actionId, handler));
        }

        /// <summary>"Odul" butonu: bugun alinabiliyorsa al, yoksa 7 gunluk durumu goster.</summary>
        private void OpenDailyRewards()
        {
            if (rewardPopup == null || rewardPopup.IsOpen)
                return;

            int[] amounts = ProgressionSettings.Instance.dailyLoginXp;
            if (DailyLogin.CanClaim(out int day, out _))
                rewardPopup.ShowDaily(day, amounts, () => DailyLogin.Claim());
            else
                rewardPopup.ShowDailyStatus(DailyLogin.ClaimedDays, amounts, null);
        }

        private void OnDoubleXpPressed()
        {
            ProgressionSettings settings = ProgressionSettings.Instance;

            if (PlayerProgress.DoubleXpMatches >= settings.maxStoredDoubleXp)
            {
                ShowToast($"EN FAZLA {settings.maxStoredDoubleXp} MAÇ!");
                return;
            }

            if (RewardedAds.IsShowing)
                return;

            ShowToast("REKLAM İZLENİYOR...");
            RewardedAds.Show(this, watched =>
            {
                if (watched && PlayerProgress.AddDoubleXpMatches(settings.doubleXpMatchesPerAd))
                    ShowToast("ÇİFT XP ETKİN!");
            });
        }

        private void CacheDoubleXpTexts()
        {
            if (doubleXpTexts == null)
                return;

            _doubleXpIdleTexts = new string[doubleXpTexts.Length];
            for (int i = 0; i < doubleXpTexts.Length; i++)
                _doubleXpIdleTexts[i] = doubleXpTexts[i] != null ? doubleXpTexts[i].text : string.Empty;
        }

        private void RefreshDoubleXp()
        {
            int matches = PlayerProgress.DoubleXpMatches;

            if (doubleXpTexts != null && _doubleXpIdleTexts != null)
            {
                for (int i = 0; i < doubleXpTexts.Length; i++)
                {
                    if (doubleXpTexts[i] != null)
                        doubleXpTexts[i].text = matches > 0
                            ? string.Format(doubleXpActiveFormat, matches)
                            : _doubleXpIdleTexts[i];
                }
            }

            if (doubleXpSteps == null)
                return;

            for (int i = 0; i < doubleXpSteps.Length; i++)
            {
                if (doubleXpSteps[i] != null)
                    doubleXpSteps[i].color = i < matches ? doubleXpStepOn : doubleXpStepOff;
            }
        }

        public void StartMatch(BattleGameMode mode)
        {
            if (_bootstrap == null || _bootstrap.IsRunning)
                return;

            if (playButton != null)
                playButton.interactable = false;

            LoadingScreen.Show(loadingScreenPrefab, _bootstrap);
            _bootstrap.StartMatchmaking(mode);
        }

        public void ShowToast(string message)
        {
            if (toastGroup == null || toastText == null)
                return;

            toastText.text = message;
            toastGroup.transform.SetAsLastSibling(); // mod penceresinin de ustunde

            if (_toastRoutine != null)
                StopCoroutine(_toastRoutine);

            _toastRoutine = StartCoroutine(ToastRoutine());
        }

        private void OnPlayPressed()
        {
            if (modePanel != null)
                modePanel.Open();
            else
                StartMatch(defaultMode);
        }

        private void OnModePlayRequested(ModeSelectPanel.ModeOption option)
        {
            modePanel.Hide();
            StartMatch(option.mode);
        }

        private void OnModeSelectionChanged(ModeSelectPanel.ModeOption option)
        {
            SetText(modeChipText, option.shortLabel);
        }

        private void OnUnhandledButton(MenuButton button)
        {
            if (isActiveAndEnabled)
                ShowToast("YAKINDA!");
        }

        private void OnLockedModePressed(ModeSelectPanel.ModeOption option)
        {
            ShowToast("YAKINDA!");
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
