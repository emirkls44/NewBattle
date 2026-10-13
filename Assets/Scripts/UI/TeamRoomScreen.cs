using System.Collections.Generic;
using Fusion;
using NewBattle.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// TAKIM ODASI ekrani (Takimla Oyna): harita secim ekranindan ONCE gorunur.
    ///   - Odadaki gercek oyuncular takim kartlarinda listelenir; karta basan oraya gecer.
    ///   - Kurucu oyuncu sayisini (katilan kadar / 4 / 8 / 16) ve takim boyunu (2 / 4) degistirebilir.
    ///   - Herkes HAZIR'a basinca kurucu BASLAT'a basar: harita secim ekrani acilir.
    ///   - AYRIL: odadan cikip ana menuye doner.
    /// Ag durumu LobbyCountdownController'da (takim/hazir PlayerCombatStats'ta).
    /// </summary>
    public class TeamRoomScreen : MonoBehaviour
    {
        [Header("Gorunurluk")]
        [Tooltip("Ekranin kendi Canvas'i. Gizlerken kapatilir; script calismaya devam eder.")]
        [SerializeField] private Canvas screenCanvas;

        [Header("Ust bilgi")]
        [SerializeField] private TextMeshProUGUI codeText;
        [SerializeField] private string codeFormat = "ODA KODU: {0}";
        [SerializeField] private TextMeshProUGUI playerCountText;
        [SerializeField] private TextMeshProUGUI infoText;

        [Header("Kurucu ayarlari")]
        [SerializeField] private FriendRoomPopup.Choice[] sizeChoices;
        [SerializeField] private FriendRoomPopup.Choice[] teamSizeChoices;
        [Tooltip("Kurucu olmayanlara 'Ayarlari oda kurucusu secer' yazar.")]
        [SerializeField] private TextMeshProUGUI settingsNote;

        [Header("Takimlar")]
        [SerializeField] private RectTransform teamsRoot;
        [Tooltip("Kopyalanan kart (pasif durur). Gorunumunu buradan duzenle.")]
        [SerializeField] private TeamRoomCard cardTemplate;

        [Header("Alt butonlar")]
        [SerializeField] private Button readyButton;
        [SerializeField] private Image readyFace;
        [SerializeField] private TextMeshProUGUI readyLabel;
        [SerializeField] private Button startButton;
        [SerializeField] private Button leaveButton;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Renkler")]
        [SerializeField] private Color selectedColor = new(0.36f, 0.77f, 0.22f, 1f);
        [SerializeField] private Color normalColor = new(0.32f, 0.24f, 0.52f, 1f);
        [SerializeField] private Color disabledColor = new(0.25f, 0.2f, 0.35f, 0.6f);
        [SerializeField] private Color cardColor = new(0.23f, 0.15f, 0.40f, 0.92f);
        [SerializeField] private Color myCardColor = new(0.20f, 0.45f, 0.22f, 0.95f);
        [SerializeField] private Color hostNameColor = new(1f, 0.82f, 0.25f, 1f);
        [SerializeField] private Color nameColor = Color.white;
        [SerializeField] private Color emptyColor = new(1f, 1f, 1f, 0.35f);
        [SerializeField] private Color readyOnColor = new(0.45f, 0.9f, 0.3f, 1f);
        [SerializeField] private Color readyOffColor = new(1f, 1f, 1f, 0.15f);
        [SerializeField] private Color readyButtonColor = new(0.36f, 0.77f, 0.22f, 1f);
        [SerializeField] private Color unreadyButtonColor = new(0.98f, 0.65f, 0.11f, 1f);

        [SerializeField, HideInInspector] private int layoutVersion;

        private readonly List<TeamRoomCard> _cards = new();
        private LobbyCountdownController _lobby;
        private NetworkRunner _runner;
        private PlayerCombatStats _local;
        private float _nextSearchTime;
        private bool _inRoom;
        private bool _leaving;

        // Kurucu ayrilirsa ne olacagi; oda acikken her karede guncellenir (herkeste ayni).
        private bool _wasHost;
        private bool _hasSuccessor;
        private bool _successorIsMe;
        private string _successorName;
        private int _nextCode;
        private int _sizeChoice;
        private int _teamSize;

        private void Awake()
        {
            if (screenCanvas == null)
                screenCanvas = GetComponent<Canvas>();

            if (cardTemplate != null)
                cardTemplate.gameObject.SetActive(false);

            if (sizeChoices != null)
                foreach (FriendRoomPopup.Choice choice in sizeChoices)
                    if (choice.button != null)
                        choice.button.onClick.AddListener(() => PickSize(choice.value));

            if (teamSizeChoices != null)
                foreach (FriendRoomPopup.Choice choice in teamSizeChoices)
                    if (choice.button != null)
                        choice.button.onClick.AddListener(() => PickTeamSize(choice.value));

            if (readyButton != null)
                readyButton.onClick.AddListener(ToggleReady);
            if (startButton != null)
                startButton.onClick.AddListener(StartRoom);
            if (leaveButton != null)
                leaveButton.onClick.AddListener(Leave);

            SetVisible(false);
        }

        private void Update()
        {
            Resolve();

            bool running = _runner != null && _runner.IsRunning;
            bool lobbyValid = _lobby != null && _lobby.Object != null && _lobby.Object.IsValid;
            bool show = NetworkBootstrap.IsFriendRoom && running && lobbyValid && !_lobby.RoomStarted;

            // Oda kapandi (kurucu cikti / oda dolu / baglanti koptu).
            if (_inRoom && !running && !_leaving)
            {
                _inRoom = false;
                RoomLost();
                return;
            }

            if (lobbyValid && _lobby.RoomStarted)
                _inRoom = false;
            else if (NetworkBootstrap.IsFriendRoom && running)
                _inRoom = true;

            SetVisible(show);
            if (!show)
                return;

            bool host = _lobby.HasStateAuthority;
            List<PlayerCombatStats> players = _lobby.RoomPlayers();
            RememberSuccessor(host, players);

            RefreshHeader(players.Count);
            RefreshSettings(host, players.Count);
            RefreshCards(players);
            RefreshButtons(host);
        }

        // ------------------------------------------------------------------
        // Gorunum
        // ------------------------------------------------------------------

        private void RefreshHeader(int humans)
        {
            if (codeText != null)
                codeText.text = string.Format(codeFormat, NetworkBootstrap.FriendRoomCode);

            if (playerCountText != null)
                playerCountText.text = _lobby.RoomSizeChoice > 0
                    ? $"{humans} / {_lobby.RoomSlotLimit} OYUNCU"
                    : $"{humans} OYUNCU";

            if (infoText != null)
                infoText.text = FriendRoomPopup.InfoFor(_lobby.RoomSizeChoice, _lobby.RoomTeamSize);
        }

        private void RefreshSettings(bool host, int humans)
        {
            int size = _lobby.RoomSizeChoice;
            int team = _lobby.RoomTeamSize;

            if (sizeChoices != null)
                foreach (FriendRoomPopup.Choice choice in sizeChoices)
                {
                    bool valid = choice.value == 0 ||
                                 (choice.value >= humans && choice.value / Mathf.Max(1, team) >= 2);
                    Paint(choice, choice.value == size, valid, host);
                }

            if (teamSizeChoices != null)
                foreach (FriendRoomPopup.Choice choice in teamSizeChoices)
                {
                    bool valid = size == 0 || size / Mathf.Max(1, choice.value) >= 2;
                    Paint(choice, choice.value == team, valid, host);
                }

            if (settingsNote != null)
                settingsNote.gameObject.SetActive(!host);
        }

        private void Paint(FriendRoomPopup.Choice choice, bool selected, bool valid, bool host)
        {
            if (choice.button != null)
                choice.button.interactable = host && valid;
            if (choice.face != null)
                choice.face.color = selected ? selectedColor : valid ? normalColor : disabledColor;
            if (choice.label != null)
                choice.label.alpha = valid || selected ? 1f : 0.45f;
        }

        private void RefreshCards(List<PlayerCombatStats> players)
        {
            if (teamsRoot == null || cardTemplate == null)
                return;

            int teamCount = _lobby.RoomTeamCount;
            int teamSize = Mathf.Max(1, _lobby.RoomTeamSize);
            PlayerRef roomHost = _lobby.RoomHost;
            int myTeam = _local != null ? _local.TeamId : -1;

            while (_cards.Count < teamCount)
            {
                TeamRoomCard card = Instantiate(cardTemplate, teamsRoot);
                card.name = $"Takim{_cards.Count + 1}";
                int index = _cards.Count;
                if (card.button != null)
                    card.button.onClick.AddListener(() => RequestTeam(index));
                _cards.Add(card);
            }

            for (int i = 0; i < _cards.Count; i++)
            {
                TeamRoomCard card = _cards[i];
                bool used = i < teamCount;
                if (card.gameObject.activeSelf != used)
                    card.gameObject.SetActive(used);
                if (!used)
                    continue;

                if (card.title != null)
                    card.title.text = $"TAKIM {i + 1}";
                if (card.background != null)
                    card.background.color = i == myTeam ? myCardColor : cardColor;

                int slot = 0;
                foreach (PlayerCombatStats player in players)
                {
                    if (player.TeamId != i || slot >= teamSize || card.slots == null || slot >= card.slots.Length)
                        continue;

                    bool isHost = player.Object.InputAuthority == roomHost;
                    FillSlot(card.slots[slot], player.NameOrDefault, isHost ? hostNameColor : nameColor,
                        isHost || player.RoomReady, isHost);
                    slot++;
                }

                for (; card.slots != null && slot < card.slots.Length; slot++)
                {
                    TeamRoomCard.Slot empty = card.slots[slot];
                    if (empty.root != null)
                        empty.root.SetActive(slot < teamSize);
                    if (slot < teamSize)
                        FillSlot(empty, _lobby.RoomSizeChoice > 0 ? "Boş (bot)" : "Boş", emptyColor, false, false);
                }
            }
        }

        private void FillSlot(TeamRoomCard.Slot slot, string name, Color color, bool ready, bool host)
        {
            if (slot.root != null && !slot.root.activeSelf)
                slot.root.SetActive(true);
            if (slot.nameText != null)
            {
                slot.nameText.text = name;
                slot.nameText.color = color;
            }
            if (slot.readyIcon != null)
                slot.readyIcon.color = ready ? readyOnColor : readyOffColor;
            if (slot.hostIcon != null)
                slot.hostIcon.SetActive(host);
        }

        private void RefreshButtons(bool host)
        {
            bool ready = _local != null && _local.RoomReady;

            if (readyButton != null)
                readyButton.gameObject.SetActive(!host);
            if (!host)
            {
                if (readyLabel != null)
                    readyLabel.text = ready ? "HAZIR DEĞİLİM" : "HAZIR";
                if (readyFace != null)
                    readyFace.color = ready ? unreadyButtonColor : readyButtonColor;
            }

            string reason = string.Empty;
            bool canStart = host && _lobby.CanStartRoom(out reason);
            if (startButton != null)
            {
                startButton.gameObject.SetActive(host);
                startButton.interactable = canStart;
            }

            if (statusText != null)
                statusText.text = host
                    ? canStart ? "Herkes hazır! BAŞLAT'a bas." : reason
                    : ready ? "Kurucunun başlatması bekleniyor..." : "Takımını seç ve HAZIR'a bas";
        }

        // ------------------------------------------------------------------
        // Eylemler
        // ------------------------------------------------------------------

        private void RequestTeam(int team)
        {
            if (_local == null || _lobby == null || _lobby.RoomStarted || _local.TeamId == team)
                return;

            _local.RPC_RequestTeam(team);
            Sfx.PlayUI("kart");
        }

        private void PickSize(int size)
        {
            if (_lobby != null && _lobby.SetRoomSizeChoice(size))
                Sfx.PlayUI("kart");
            else
                Sfx.PlayUI("hata");
        }

        private void PickTeamSize(int teamSize)
        {
            if (_lobby != null && _lobby.SetRoomTeamSize(teamSize))
                Sfx.PlayUI("kart");
            else
                Sfx.PlayUI("hata");
        }

        private void ToggleReady()
        {
            if (_local == null)
                return;

            _local.RPC_SetRoomReady(!_local.RoomReady);
            Sfx.PlayUI("kart");
        }

        private void StartRoom()
        {
            if (_lobby != null && _lobby.StartRoom())
                Sfx.PlayUI("basla");
            else
                Sfx.PlayUI("hata");
        }

        private async void Leave()
        {
            if (_leaving)
                return;

            _leaving = true;
            _inRoom = false;
            if (leaveButton != null)
                leaveButton.interactable = false;

            int sceneIndex = SceneManager.GetActiveScene().buildIndex;
            NetworkBootstrap.ClearFriendRoom();
            NetworkBootstrap.PendingRejoin = null;
            LoadingScreen.ShowLeaving(FindLoadingPrefab(), "ANA MENÜYE DÖNÜLÜYOR...");

            NetworkRunner runner = _runner;
            if (runner != null && runner.IsRunning)
            {
                try
                {
                    await runner.Shutdown(true);
                }
                catch (System.Exception exception)
                {
                    Debug.LogWarning($"Takim odasi: kapatirken hata: {exception.Message}");
                }
            }

            SceneManager.LoadScene(sceneIndex);
        }

        /// <summary>Kurucu ayrilirsa yeni kurucu = odaya kurucudan sonra ilk giren oyuncu.</summary>
        private void RememberSuccessor(bool host, List<PlayerCombatStats> players)
        {
            _wasHost = host;
            _nextCode = _lobby.NextRoomCode;
            _sizeChoice = _lobby.RoomSizeChoice;
            _teamSize = _lobby.RoomTeamSize;
            _hasSuccessor = false;
            _successorIsMe = false;
            _successorName = string.Empty;

            foreach (PlayerCombatStats player in players)
            {
                if (player.Object.InputAuthority == _lobby.RoomHost)
                    continue;

                _hasSuccessor = true;
                _successorIsMe = player.HasInputAuthority;
                _successorName = player.NameOrDefault;
                break;
            }
        }

        /// <summary>
        /// Oturum bitti ama biz cikmadik: kurucu ayrildi. 2. oyuncu ayni ayarlarla yeni
        /// oda kurar, digerleri ona katilir; 2. oyuncu yoksa oda kapanir.
        /// </summary>
        private void RoomLost()
        {
            NetworkBootstrap.ClearFriendRoom();

            if (!_wasHost && _hasSuccessor && _nextCode > 0)
            {
                string code = _nextCode.ToString();
                NetworkBootstrap.PendingRejoin = new NetworkBootstrap.RoomRejoin
                {
                    Create = _successorIsMe,
                    Code = code,
                    SizeChoice = _sizeChoice,
                    TeamSize = _teamSize,
                    Attempts = _successorIsMe ? 0 : 5,
                    Delay = _successorIsMe ? 0.5f : 3f
                };
                NetworkBootstrap.PendingMenuMessage = _successorIsMe
                    ? "KURUCU AYRILDI, YENİ KURUCU SENSİN"
                    : $"KURUCU AYRILDI, YENİ KURUCU: {_successorName}";
            }
            else
            {
                NetworkBootstrap.PendingMenuMessage = "ODA KAPANDI";
            }

            bool rejoin = NetworkBootstrap.PendingRejoin != null;
            LoadingScreen.ShowLeaving(FindLoadingPrefab(),
                rejoin ? NetworkBootstrap.PendingMenuMessage : "ODA KAPANDI", holdForRejoin: rejoin);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // ------------------------------------------------------------------
        // Yardimcilar
        // ------------------------------------------------------------------

        private void Resolve()
        {
            if (Time.unscaledTime < _nextSearchTime)
                return;

            _nextSearchTime = Time.unscaledTime + 0.25f;

            if (_lobby == null)
                _lobby = FindFirstObjectByType<LobbyCountdownController>();
            if (_runner == null)
                _runner = _lobby != null && _lobby.Runner != null ? _lobby.Runner : FindFirstObjectByType<NetworkRunner>();

            if (_local != null && _local.Object != null && _local.Object.IsValid)
                return;

            _local = null;
            foreach (PlayerCombatStats stats in FindObjectsByType<PlayerCombatStats>(FindObjectsSortMode.None))
            {
                if (stats.Object != null && stats.Object.IsValid && stats.HasInputAuthority)
                {
                    _local = stats;
                    break;
                }
            }
        }

        /// <summary>Mor yukleme ekrani prefabi (ana menude kayitli).</summary>
        private static LoadingScreen FindLoadingPrefab()
        {
            MainMenuScreen menu = FindFirstObjectByType<MainMenuScreen>(FindObjectsInactive.Include);
            return menu != null ? menu.LoadingScreenPrefab : null;
        }

        private void SetVisible(bool visible)
        {
            if (screenCanvas != null && screenCanvas.enabled != visible)
                screenCanvas.enabled = visible;
        }
    }
}
