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
        }

        public void Close() => gameObject.SetActive(false);

        private void Create()
        {
            string code = Random.Range(1000, 10000).ToString();
            Close();
            if (menu != null)
                menu.StartFriendRoom(true, code);
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
