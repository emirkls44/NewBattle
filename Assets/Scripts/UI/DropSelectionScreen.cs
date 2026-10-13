using NewBattle.Audio;
using System.Collections;
using NewBattle.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// ASAMA 4-5: "AKSIYONA HAZIRLAN" harita secim ekrani.
    ///
    /// Oyuncu kare haritaya dokunarak inis noktasini secer; isaretci oraya
    /// konur ve bolgenin adi (Mezarlik, Ciftlik...) gorunur. Sol tarafta
    /// odadaki oyuncu ikonlari dolar. Once "Oyuncular bekleniyor...", oda
    /// dolunca buyuk geri sayim (5-4-3-2-1) gelir, sayim bitince ekran kapanir
    /// ve inis baslar.
    ///
    /// Bilerek "Geri" butonu yok: bu ekrana gelen oyuncu maca girmis sayilir.
    /// Oda dolarken cikip girmeler eslestirmeyi ve bot doldurmayi bozuyordu.
    ///
    /// Secim agda eski yolla gider: PlayerController.RequestDropPosition.
    /// Sunucu noktayi gercek zemine ve guvenli alana oturtur.
    /// </summary>
    public class DropSelectionScreen : MonoBehaviour
    {
        [Header("Gorunurluk")]
        [Tooltip("Ekranin kendi Canvas'i. Gizlerken kapatilir; script calismaya devam eder.")]
        [SerializeField] private Canvas screenCanvas;

        [Header("Harita")]
        [SerializeField] private DropMapData mapData;
        [SerializeField] private RawImage mapImage;
        [SerializeField] private RectTransform marker;
        [SerializeField] private RectTransform markerPin;
        [SerializeField] private GameObject regionLabel;
        [SerializeField] private TextMeshProUGUI regionLabelText;

        [Header("Mod")]
        [SerializeField] private Image modeIcon;
        [SerializeField] private TextMeshProUGUI modeText;
        [SerializeField] private Sprite soloIcon;
        [SerializeField] private Sprite duoIcon;

        [Header("Oyuncular")]
        [Tooltip("Oyuncu ikonlarinin ebeveyni (her cocuk bir yuva).")]
        [SerializeField] private RectTransform slotsRoot;
        [SerializeField] private Color slotFilled = new(0.20f, 0.30f, 0.40f, 0.95f);
        [SerializeField] private Color slotEmpty = new(0.10f, 0.22f, 0.32f, 0.55f);
        [SerializeField] private Color slotIconFilled = new(0.88f, 0.90f, 0.93f, 1f);
        [SerializeField] private Color slotIconEmpty = new(0.45f, 0.55f, 0.65f, 0.6f);

        [Header("Bekleme ve Geri Sayim")]
        [SerializeField] private TextMeshProUGUI waitingText;
        [SerializeField] private string waitingFormat = "Oyuncular bekleniyor...\n{0}";
        [Tooltip("Takim odasinda (kurucu baslattiktan sonra): {0} = kalan saniye.")]
        [SerializeField] private string teamRoomFormat = "İniş noktanı seç!\n{0}";
        [SerializeField] private RectTransform countdownGroup;
        [SerializeField] private TextMeshProUGUI countdownText;

        [Header("Bildirim")]
        [Tooltip("Islevi henuz baglanmamis butonlara basinca 'YAKINDA!' gosterir.")]
        [SerializeField] private ToastView toast;

        [Header("Cift XP Durumu")]
        [Tooltip("Reklamla cift XP kazanilmissa bu yazi 'ÇİFT XP ETKİN!' olur.")]
        [SerializeField] private TextMeshProUGUI doubleXpText;
        [SerializeField] private Image doubleXpIcon;
        [SerializeField] private string doubleXpActiveLabel = "ÇİFT XP ETKİN!";
        [SerializeField] private Color doubleXpActiveColor = new(1f, 0.78f, 0.1f, 1f);

        private string _doubleXpIdleLabel;
        private Color _doubleXpIdleColor;
        private Color _doubleXpIdleIconColor;

        private DropPhaseController _dropPhase;
        private LobbyCountdownController _lobby;
        private PlayerController _localPlayer;
        private float _nextSearchTime;
        private int _lastCountdown = -1;
        private Coroutine _markerPop;
        private Coroutine _countdownPop;

        private void Awake()
        {
            if (screenCanvas == null)
                screenCanvas = GetComponent<Canvas>();

            if (mapImage != null && mapData != null && mapData.MapTexture != null)
                mapImage.texture = mapData.MapTexture;

            if (marker != null)
                marker.gameObject.SetActive(false);

            if (doubleXpText != null)
            {
                _doubleXpIdleLabel = doubleXpText.text;
                _doubleXpIdleColor = doubleXpText.color;
            }

            if (doubleXpIcon != null)
                _doubleXpIdleIconColor = doubleXpIcon.color;

            SetVisible(false);
            MenuButton.Unhandled += OnUnhandledButton;
        }

        private void OnDestroy() => MenuButton.Unhandled -= OnUnhandledButton;

        private void OnUnhandledButton(MenuButton button)
        {
            if (toast != null && screenCanvas != null && screenCanvas.enabled)
                toast.Show("YAKINDA!");
        }

        private void Update()
        {
            ResolveRuntimeReferences();

            bool ready = _dropPhase != null && _dropPhase.Object != null && _dropPhase.Object.IsValid &&
                         _dropPhase.Runner != null && _dropPhase.Runner.IsRunning;
            bool lobbyReady = _lobby != null && _lobby.Object != null && _lobby.Object.IsValid;
            // Takim odasinda kurucu BASLAT'a basana kadar takim odasi ekrani gorunur.
            bool show = ready && lobbyReady && !_dropPhase.GameplayStarted &&
                        (!NetworkBootstrap.IsFriendRoom || _lobby.RoomStarted);

            SetVisible(show);
            if (!show)
                return;

            RefreshMode();
            RefreshDoubleXp();
            RefreshSlots();
            RefreshCountdown();
        }

        // ------------------------------------------------------------------
        // Harita secimi
        // ------------------------------------------------------------------

        /// <summary>Haritaya dokunulan ekran noktasindan inis noktasi secer.</summary>
        public void SelectAt(Vector2 screenPosition, Camera eventCamera)
        {
            if (_dropPhase == null || _dropPhase.Object == null || _dropPhase.GameplayStarted || mapImage == null)
                return;

            RectTransform mapRect = mapImage.rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(mapRect, screenPosition, eventCamera, out Vector2 local))
                return;

            Rect rect = mapRect.rect;
            Vector2 normalized = new(
                Mathf.Clamp01(Mathf.InverseLerp(rect.xMin, rect.xMax, local.x)),
                Mathf.Clamp01(Mathf.InverseLerp(rect.yMin, rect.yMax, local.y)));

            ResolveRuntimeReferences(force: true);
            if (_localPlayer == null)
                return;

            Vector3 world = mapData != null
                ? mapData.NormalizedToWorld(normalized, _dropPhase.MapExtent)
                : new Vector3((normalized.x - 0.5f) * 2f * _dropPhase.MapExtent, 0f, (normalized.y - 0.5f) * 2f * _dropPhase.MapExtent);

            _localPlayer.RequestDropPosition(world);
            PlaceMarker(normalized);
        }

        private void PlaceMarker(Vector2 normalized)
        {
            if (marker == null)
                return;

            marker.anchorMin = normalized;
            marker.anchorMax = normalized;
            marker.anchoredPosition = Vector2.zero;
            marker.gameObject.SetActive(true);

            DropMapData.Region region = mapData != null ? mapData.FindRegion(normalized) : null;
            if (regionLabel != null)
                regionLabel.SetActive(region != null);
            if (regionLabelText != null && region != null)
                regionLabelText.text = region.name;

            if (_markerPop != null)
                StopCoroutine(_markerPop);
            _markerPop = StartCoroutine(Pop(markerPin != null ? markerPin : marker, 1.35f, 0.25f));
            Sfx.PlayUI("kart");
        }

        // ------------------------------------------------------------------
        // Sol taraf
        // ------------------------------------------------------------------

        private void RefreshMode()
        {
            bool duo = NetworkBootstrap.ActiveMode == BattleGameMode.Squad;

            if (modeText != null)
                modeText.text = NetworkBootstrap.IsFriendRoom
                    ? $"{NetworkBootstrap.ActiveTeamSize} Kişilik Takım"
                    : duo ? "İkili" : "Solo";

            if (modeIcon != null)
            {
                Sprite icon = duo ? duoIcon : soloIcon;
                if (icon != null)
                    modeIcon.sprite = icon;
            }
        }

        private void RefreshDoubleXp()
        {
            bool active = PlayerProgress.DoubleXpMatches > 0;

            if (doubleXpText != null)
            {
                doubleXpText.text = active ? doubleXpActiveLabel : _doubleXpIdleLabel;
                doubleXpText.color = active ? doubleXpActiveColor : _doubleXpIdleColor;
            }

            if (doubleXpIcon != null)
                doubleXpIcon.color = active ? doubleXpActiveColor : _doubleXpIdleIconColor;
        }

        private void RefreshSlots()
        {
            if (slotsRoot == null)
                return;

            int capacity = Mathf.Max(1, _lobby.MaximumPlayers);
            int filled = _lobby.RosterFinalized
                ? _lobby.TotalContestants
                : _lobby.ConnectedPlayers;

            for (int i = 0; i < slotsRoot.childCount; i++)
            {
                Transform slot = slotsRoot.GetChild(i);
                bool used = i < capacity;
                if (slot.gameObject.activeSelf != used)
                    slot.gameObject.SetActive(used);

                if (!used)
                    continue;

                bool taken = i < filled;
                if (slot.TryGetComponent(out Image background))
                    background.color = taken ? slotFilled : slotEmpty;

                if (slot.childCount > 0 && slot.GetChild(0).TryGetComponent(out Image icon))
                    icon.color = taken ? slotIconFilled : slotIconEmpty;
            }
        }

        private void RefreshCountdown()
        {
            bool finalCountdown = _lobby.RosterFinalized;

            if (waitingText != null)
            {
                waitingText.gameObject.SetActive(!finalCountdown);
                if (!finalCountdown)
                {
                    string seconds = _lobby.CountdownRunning
                        ? Mathf.CeilToInt(_lobby.RemainingSeconds).ToString()
                        : string.Empty;
                    waitingText.text = NetworkBootstrap.IsFriendRoom
                        ? string.Format(teamRoomFormat, seconds)
                        : string.Format(waitingFormat, seconds);
                }
            }

            if (countdownGroup != null)
                countdownGroup.gameObject.SetActive(finalCountdown);

            if (!finalCountdown || countdownText == null)
            {
                _lastCountdown = -1;
                return;
            }

            int value = _lobby.MatchStarted ? 0 : Mathf.Max(1, Mathf.CeilToInt(_lobby.RemainingSeconds));
            if (value == _lastCountdown)
                return;

            _lastCountdown = value;
            countdownText.text = value.ToString();
            Sfx.PlayUI(value > 0 ? "geri_sayim" : "basla");

            if (_countdownPop != null)
                StopCoroutine(_countdownPop);
            _countdownPop = StartCoroutine(Pop(countdownGroup, 1.4f, 0.3f));
        }

        // ------------------------------------------------------------------
        // Yardimcilar
        // ------------------------------------------------------------------

        private void SetVisible(bool visible)
        {
            if (screenCanvas != null && screenCanvas.enabled != visible)
                screenCanvas.enabled = visible;
        }

        private void ResolveRuntimeReferences(bool force = false)
        {
            if (!force && Time.unscaledTime < _nextSearchTime)
                return;

            _nextSearchTime = Time.unscaledTime + 0.25f;

            if (_dropPhase == null)
                _dropPhase = FindFirstObjectByType<DropPhaseController>();
            if (_lobby == null)
                _lobby = FindFirstObjectByType<LobbyCountdownController>();

            if (_localPlayer != null)
                return;

            foreach (PlayerController player in FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (player != null && player.Object != null && player.HasInputAuthority)
                {
                    _localPlayer = player;
                    break;
                }
            }
        }

        /// <summary>Kisa bir "zipla" animasyonu: buyuk baslar, normale oturur.</summary>
        private static IEnumerator Pop(Transform target, float startScale, float seconds)
        {
            if (target == null)
                yield break;

            // Senin ayarladigin olcek etrafinda ziplar.
            Vector3 baseScale = UIBaseScale.Get(target);

            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = 1f - Mathf.Pow(1f - t / seconds, 3f);
                target.localScale = baseScale * Mathf.Lerp(startScale, 1f, k);
                yield return null;
            }

            target.localScale = baseScale;
        }
    }
}
