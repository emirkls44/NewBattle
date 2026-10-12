using NewBattle.Audio;
using NewBattle.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// "HEDİYE KODU" penceresi (Ayarlar'dan acilir). Oyuncu senin belirledigin
    /// kodu yazar; gecerliyse odul (karakter, animasyon, para, XP) verilir.
    /// Kodlar: Tools > NewBattle > Hediye Kodlari.
    /// </summary>
    public class GiftCodePopup : MonoBehaviour
    {
        [SerializeField] private TMP_InputField input;
        [SerializeField] private Button redeemButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private TextMeshProUGUI hintText;
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
            if (input != null)
            {
                input.characterLimit = 20;
                input.onSubmit.AddListener(_ => Redeem());
                input.onValueChanged.AddListener(_ => SetHint("Hediye kodunu yaz", false));
            }

            if (redeemButton != null)
                redeemButton.onClick.AddListener(Redeem);
            if (cancelButton != null)
                cancelButton.onClick.AddListener(Close);
        }

        public void Open()
        {
            Initialize();

            _opening = true;
            gameObject.SetActive(true);
            _opening = false;
            transform.SetAsLastSibling();

            if (input != null)
            {
                input.text = string.Empty;
                input.ActivateInputField();
            }

            SetHint("Hediye kodunu yaz", false);
        }

        public void Close() => gameObject.SetActive(false);

        private void Redeem()
        {
            GiftCodes.Result result = GiftCodes.Redeem(input != null ? input.text : string.Empty, out RewardNotice notice);

            switch (result)
            {
                case GiftCodes.Result.Success:
                    Close();
                    if (menu != null && notice != null)
                        menu.ShowNotice(notice);
                    return;

                case GiftCodes.Result.AlreadyUsed:
                    SetHint("Bu kodu zaten kullandın", true);
                    break;

                case GiftCodes.Result.AlreadyOwned:
                    SetHint("Bu hediye zaten sende", true);
                    break;

                default:
                    SetHint("Kod geçersiz", true);
                    break;
            }

            Sfx.PlayUI("hata");
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
