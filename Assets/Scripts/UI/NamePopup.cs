using System;
using NewBattle.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// Isim penceresi, iki sekilde acilir:
    ///   - Ilk acilis: "KULLANICI ADIN NE OLSUN?" Vazgec yok; isim girilmeden kapanmaz.
    ///   - Degistirme (kalem / AYARLAR): sadece hak varsa. Hak her 5 seviyede
    ///     bir gelir (5, 10, 15...); yoksa pencere hangi seviyede gelecegini soyler.
    /// Isim 3-16 karakter olmali.
    /// </summary>
    public class NamePopup : MonoBehaviour
    {
        public const int MinLength = 3;
        public const int MaxLength = 16;

        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TMP_InputField input;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private TextMeshProUGUI cancelLabel;
        [SerializeField] private TextMeshProUGUI hintText;
        [SerializeField] private MainMenuScreen menu;
        [Tooltip("Ilk acilista acilan tam ekran opak arka plan (arkada oyun/menu gorunmesin).")]
        [SerializeField] private GameObject firstTimeBackground;

        [Header("Yazilar")]
        [SerializeField] private string firstTimeTitle = "İSMİNİ GİR";
        [SerializeField] private string changeTitle = "İSMİNİ DEĞİŞTİR";

        private bool _initialized;
        private bool _opening;
        private bool _firstTime;
        private bool _locked;
        private Action _onDone;

        public bool IsOpen => gameObject.activeSelf;

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

        /// <summary>Kalem / AYARLAR: isim degistirme (hak varsa).</summary>
        public void Open() => Show(firstTime: false, null);

        /// <summary>Ilk acilis: isim secilmeden kapanmaz. Bitince onDone cagrilir.</summary>
        public void OpenFirstTime(Action onDone) => Show(firstTime: true, onDone);

        private void Show(bool firstTime, Action onDone)
        {
            Initialize();
            _firstTime = firstTime;
            _onDone = onDone;
            _locked = !firstTime && PlayerProgress.NameChangesAvailable <= 0;

            _opening = true;
            gameObject.SetActive(true);
            _opening = false;
            transform.SetAsLastSibling();

            if (titleText != null)
                titleText.text = firstTime ? firstTimeTitle : changeTitle;

            if (firstTimeBackground != null)
                firstTimeBackground.SetActive(firstTime);

            if (cancelButton != null)
                cancelButton.gameObject.SetActive(!firstTime);
            if (cancelLabel != null)
                cancelLabel.text = _locked ? "TAMAM" : "VAZGEÇ";
            if (saveButton != null)
                saveButton.gameObject.SetActive(!_locked);

            if (input != null)
            {
                // Ilk acilista bos baslar; rastgele uretilmis "Savaşçı#1234" yazmasin.
                input.text = firstTime ? string.Empty : PlayerProgress.PlayerName;
                input.interactable = !_locked;
                if (!_locked)
                    input.ActivateInputField(); // telefonda klavye acilir
            }

            RefreshHint();
        }

        public void Close()
        {
            // Ilk acilista isim secmeden kapatilamaz.
            if (_firstTime && !PlayerProgress.HasChosenName)
                return;

            gameObject.SetActive(false);
            Action done = _onDone;
            _onDone = null;
            done?.Invoke();
        }

        private string Cleaned => input != null ? input.text.Trim() : string.Empty;

        private bool IsValid => Cleaned.Length >= MinLength && Cleaned.Length <= MaxLength && !Blocked;

        // Kufur / uygunsuz kelime (isimler diger oyunculara gorunur).
        private bool Blocked => NameFilter.IsBlocked(Cleaned);

        private void RefreshHint()
        {
            if (hintText != null)
            {
                if (_locked)
                    hintText.text = $"İsmini Seviye {Locative(PlayerProgress.NextNameChangeLevel)} değiştirebilirsin";
                else if (Blocked)
                    hintText.text = "Bu isim kullanılamaz, başka bir isim dene";
                else if (!IsValid)
                    hintText.text = $"En az {MinLength}, en fazla {MaxLength} harf";
                else if (_firstTime)
                    hintText.text = $"{Cleaned.Length} / {MaxLength}";
                else
                    hintText.text = $"{PlayerProgress.NameChangesAvailable} değiştirme hakkın var · {Cleaned.Length} / {MaxLength}";
            }

            if (saveButton != null)
                saveButton.interactable = !_locked && IsValid;
        }

        /// <summary>Sayiya Turkce bulunma eki: 5'te, 10'da, 20'de, 40'ta.</summary>
        private static string Locative(int number)
        {
            string[] units = { "", "'de", "'de", "'te", "'te", "'te", "'da", "'de", "'de", "'da" };
            string[] tens = { "", "'da", "'de", "'da", "'ta", "'de", "'ta", "'te", "'de", "'da" };

            string suffix = number % 10 != 0 ? units[number % 10]
                : number % 100 != 0 ? tens[number / 10 % 10]
                : "'de"; // yuz, bin
            return number + suffix;
        }

        private void Save()
        {
            if (_locked || !IsValid)
                return;

            if (_firstTime)
            {
                PlayerProgress.ChooseFirstName(Cleaned);
                Close();
                return;
            }

            if (Cleaned == PlayerProgress.PlayerName)
            {
                Close();
                return;
            }

            if (!PlayerProgress.TryChangeName(Cleaned))
                return;

            Close();
            if (menu != null)
                menu.ShowToast("İSİM DEĞİŞTİ!");
        }
    }
}
