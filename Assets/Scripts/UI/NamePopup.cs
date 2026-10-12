using NewBattle.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// "İSMİNİ DEĞİŞTİR" penceresi. Menudeki kalem butonu ve AYARLAR'daki
    /// isim butonu acar. Isim 3-16 karakter olmali.
    /// </summary>
    public class NamePopup : MonoBehaviour
    {
        public const int MinLength = 3;
        public const int MaxLength = 16;

        [SerializeField] private TMP_InputField input;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private TextMeshProUGUI hintText;
        [SerializeField] private MainMenuScreen menu;

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

            if (input != null)
            {
                input.characterLimit = MaxLength;
                input.onValueChanged.AddListener(_ => RefreshHint());
                input.onSubmit.AddListener(_ => Save());
            }

            if (saveButton != null)
                saveButton.onClick.AddListener(Save);
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
                input.text = PlayerProgress.PlayerName;
                input.ActivateInputField(); // telefonda klavye acilir
            }

            RefreshHint();
        }

        public void Close() => gameObject.SetActive(false);

        private string Cleaned => input != null ? input.text.Trim() : string.Empty;

        private bool IsValid => Cleaned.Length >= MinLength && Cleaned.Length <= MaxLength;

        private void RefreshHint()
        {
            if (hintText != null)
                hintText.text = IsValid ? $"{Cleaned.Length} / {MaxLength}" : $"En az {MinLength}, en fazla {MaxLength} harf";

            if (saveButton != null)
                saveButton.interactable = IsValid;
        }

        private void Save()
        {
            if (!IsValid)
                return;

            PlayerProgress.PlayerName = Cleaned;
            Close();

            if (menu != null)
                menu.ShowToast("İSİM DEĞİŞTİ!");
        }
    }
}
