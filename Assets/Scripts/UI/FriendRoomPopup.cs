using NewBattle.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// ARKADAŞ İKİLİSİ penceresi:
    ///   - ODA KUR: 4 haneli kod uretir, gizli oda acar. Kodu arkadasina soyle.
    ///   - KODLA KATIL: arkadasinin kodunu yaz, onun odasina gir.
    ///   - GERÇEK OYUNCULAR (parti): aciksa arkadasin gelince oda rastgele
    ///     eslestirmeye acilir; "Rastgele Ikili" arayanlar da girer. Kapaliysa
    ///     sadece ikiniz + botlar. Secimi odayi kuran yapar, hatirlanir.
    /// Ikiniz ayni takimda olursunuz; kalan yerler botla dolar.
    /// Mod penceresinde Arkadas Ikilisi secilip CEVRIMICI OYNA'ya basilinca acilir.
    /// </summary>
    public class FriendRoomPopup : MonoBehaviour
    {
        public const int CodeLength = 4;

        [SerializeField] private Button createButton;
        [SerializeField] private TMP_InputField codeInput;
        [SerializeField] private Button joinButton;
        [SerializeField] private TextMeshProUGUI hintText;
        [SerializeField] private Button closeButton;

        [Header("Parti: gercek oyuncularla eslesme")]
        [SerializeField] private Button matchmakingButton;
        [SerializeField] private TextMeshProUGUI matchmakingLabel;
        [SerializeField] private Image matchmakingFace;
        [SerializeField] private string matchmakingOnText = "GERÇEK OYUNCULARLA EŞLEŞ: AÇIK";
        [SerializeField] private string matchmakingOffText = "SADECE BOTLARLA OYNA";
        [SerializeField] private Color matchmakingOnColor = new(0.36f, 0.77f, 0.22f, 1f);
        [SerializeField] private Color matchmakingOffColor = new(0.55f, 0.55f, 0.6f, 1f);

        private const string MatchmakingPrefKey = "nb_party_matchmaking";
        [SerializeField] private MainMenuScreen menu;
        [SerializeField] private Color errorColor = new(1f, 0.45f, 0.4f, 1f);

        private Color _hintColor;
        private bool _initialized;
        private bool _opening;

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
                closeButton.onClick.AddListener(Close);
            if (matchmakingButton != null)
                matchmakingButton.onClick.AddListener(ToggleMatchmaking);
        }

        /// <summary>Odayi kuran gercek oyuncularla eslesmek istiyor mu (varsayilan: evet).</summary>
        private static bool OpenToMatchmaking
        {
            get => PlayerPrefs.GetInt(MatchmakingPrefKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(MatchmakingPrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        private void ToggleMatchmaking()
        {
            OpenToMatchmaking = !OpenToMatchmaking;
            RefreshMatchmaking();
            SetHint(OpenToMatchmaking
                ? "Arkadaşın gelince gerçek oyuncular da katılabilir"
                : "Sadece ikiniz ve botlar oynayacak", false);
        }

        private void RefreshMatchmaking()
        {
            bool on = OpenToMatchmaking;
            if (matchmakingLabel != null)
                matchmakingLabel.text = on ? matchmakingOnText : matchmakingOffText;
            if (matchmakingFace != null)
                matchmakingFace.color = on ? matchmakingOnColor : matchmakingOffColor;
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

            SetHint("Arkadaşınla aynı takımda oyna!", false);
            RefreshMatchmaking();
        }

        public void Close() => gameObject.SetActive(false);

        private void Create()
        {
            string code = Random.Range(1000, 10000).ToString();
            Close();
            if (menu != null)
                menu.StartFriendRoom(true, code, OpenToMatchmaking);
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
    }
}
