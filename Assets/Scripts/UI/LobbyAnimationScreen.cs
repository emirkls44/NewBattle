using System.Collections.Generic;
using NewBattle.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// "KARAKTER" ekrani: oyuncunun lobide oynayacak animasyonunu secer.
    ///
    /// Bir karta dokunmak karakterde o animasyonu ONIZLER (kilitli olsa bile).
    /// Alttaki buton duruma gore SEC / SATIN AL / KILITLI / SECILI olur.
    /// Kapatinca karakter secili animasyonuna doner.
    ///
    /// Ana menudeki "karakter" eylem kimlikli buton bu ekrani acar.
    /// </summary>
    public class LobbyAnimationScreen : MonoBehaviour
    {
        [SerializeField] private LobbyAnimationLibrary library;
        [SerializeField] private MenuCharacterStage characterStage;
        [SerializeField] private MainMenuScreen menu;

        [Header("Liste")]
        [SerializeField] private RectTransform listContent;
        [Tooltip("Her animasyon icin cogaltilan kart. Gorunumunu buradan duzenle.")]
        [SerializeField] private LobbyAnimationCard cardTemplate;

        [Header("Karakter")]
        [Tooltip("Menudeki karakter goruntusunun aynisini gosterir.")]
        [SerializeField] private RawImage characterView;
        [SerializeField] private AspectRatioFitter characterAspect;
        [SerializeField] private TextMeshProUGUI previewNameText;

        [Header("Ust")]
        [SerializeField] private TextMeshProUGUI coinText;
        [SerializeField] private Button closeButton;

        [Header("Islem Butonu")]
        [SerializeField] private Button actionButton;
        [SerializeField] private Image actionFace;
        [Tooltip("Butonun alt kenari; yuzle birlikte renk degistirir.")]
        [SerializeField] private Image actionLip;
        [SerializeField] private TextMeshProUGUI actionLabel;
        [SerializeField] private Color selectColor = new(0.36f, 0.77f, 0.22f, 1f);
        [SerializeField] private Color buyColor = new(1f, 0.78f, 0.1f, 1f);
        [SerializeField] private Color disabledColor = new(0.6f, 0.6f, 0.62f, 1f);

        private readonly List<LobbyAnimationCard> _cards = new();
        private LobbyAnimation _preview;
        private bool _initialized;

        public bool IsOpen => gameObject.activeSelf;

        private void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;

            if (closeButton != null)
                closeButton.onClick.AddListener(Close);
            if (actionButton != null)
            {
                actionButton.onClick.AddListener(OnActionPressed);
                // Renkleri bu script yonetiyor; Unity'nin kendi gecisi ust uste binip titretmesin.
                actionButton.transition = Selectable.Transition.None;
            }
            if (cardTemplate != null)
                cardTemplate.gameObject.SetActive(false);
        }

        private void Awake()
        {
            Initialize();

            // Duzenlerken gorunur birakilmis olabilir; oyun kapali baslar.
            if (!_opening)
                gameObject.SetActive(false);
        }

        private bool _opening;

        public void Open()
        {
            Initialize();
            if (library == null)
            {
                Debug.LogError("LobbyAnimationScreen: animasyon kutuphanesi atanmamis.");
                return;
            }

            _opening = true;
            gameObject.SetActive(true);
            _opening = false;
            transform.SetAsLastSibling();

            _preview = library.Current;
            BuildCards();
            Refresh();

            PlayerProgress.Changed -= Refresh;
            PlayerProgress.Changed += Refresh;
        }

        public void Close()
        {
            PlayerProgress.Changed -= Refresh;

            // Onizlemeden secili animasyona don.
            LobbyCharacterAnimator animator = characterStage != null ? characterStage.LobbyAnimator : null;
            if (animator != null)
                animator.PlayCurrent();

            gameObject.SetActive(false);
        }

        private void OnDestroy() => PlayerProgress.Changed -= Refresh;

        private void LateUpdate()
        {
            // Menudeki karakter goruntusu (RenderTexture) ekran boyutu degisince
            // yeniden olusabiliyor; her karede ayni dokuyu goster.
            if (characterView == null || characterStage == null)
                return;

            Texture texture = characterStage.GetComponent<RawImage>().texture;
            if (characterView.texture != texture)
                characterView.texture = texture;

            characterView.color = texture != null ? Color.white : new Color(1f, 1f, 1f, 0f);

            if (characterAspect != null && texture != null && texture.height > 0)
                characterAspect.aspectRatio = texture.width / (float)texture.height;
        }

        private void BuildCards()
        {
            foreach (LobbyAnimationCard card in _cards)
            {
                if (card != null)
                    Destroy(card.gameObject);
            }

            _cards.Clear();

            if (cardTemplate == null || listContent == null)
                return;

            foreach (LobbyAnimation animation in library.Animations)
            {
                if (animation == null)
                    continue;

                LobbyAnimationCard card = Instantiate(cardTemplate, listContent);
                card.name = $"Kart_{animation.id}";
                card.Setup(animation);
                card.gameObject.SetActive(true);
                LobbyAnimation captured = animation;
                card.Button.onClick.AddListener(() => OnCardPressed(captured));
                _cards.Add(card);
            }
        }

        private void OnCardPressed(LobbyAnimation animation)
        {
            _preview = animation;

            // Kilitli olsa da onizlenir; oyuncu neyi alacagini gorsun.
            LobbyCharacterAnimator animator = characterStage != null ? characterStage.LobbyAnimator : null;
            if (animator != null)
                animator.Play(animation);

            if (!library.IsUnlocked(animation) && menu != null)
            {
                menu.ShowToast(animation.unlock == LobbyUnlockType.Coins
                    ? "ÖNCE SATIN ALMAN LAZIM!"
                    : $"{TurkishUpper(library.RequirementText(animation))} GEREKLİ!");
            }

            Refresh();
        }

        private void OnActionPressed()
        {
            if (_preview == null)
                return;

            if (library.IsUnlocked(_preview))
            {
                library.Select(_preview);
                return;
            }

            // Parayla acilanlar magazadan alinir.
            if (_preview.unlock == LobbyUnlockType.Coins && !MenuButton.TryInvoke("magaza") && menu != null)
                menu.ShowToast("ÖNCE SATIN ALMAN LAZIM!");
        }

        /// <summary>"Seviye" -> "SEVİYE" (ToUpperInvariant noktali I'yi kaybeder).</summary>
        private static string TurkishUpper(string text) => text.Replace('i', 'İ').ToUpperInvariant();

        private void Refresh()
        {
            if (library == null)
                return;

            LobbyAnimation selected = library.Current;

            foreach (LobbyAnimationCard card in _cards)
            {
                if (card != null && card.Animation != null)
                    card.Bind(card.Animation, library, card.Animation == selected, card.Animation == _preview);
            }

            if (coinText != null)
                coinText.text = PlayerProgress.Coins.ToString();

            if (previewNameText != null)
                previewNameText.text = _preview != null ? _preview.displayName : string.Empty;

            RefreshActionButton(selected);
        }

        private void RefreshActionButton(LobbyAnimation selected)
        {
            if (actionButton == null || _preview == null)
                return;

            string label;
            Color color;
            bool interactable;

            if (_preview == selected)
            {
                label = "SEÇİLİ";
                color = disabledColor;
                interactable = false;
            }
            else if (library.IsUnlocked(_preview))
            {
                label = "SEÇ";
                color = selectColor;
                interactable = true;
            }
            else if (_preview.unlock == LobbyUnlockType.Coins)
            {
                label = "MAĞAZAYA GİT";
                color = buyColor;
                interactable = true;
            }
            else
            {
                label = $"{TurkishUpper(library.RequirementText(_preview))} GEREKLİ";
                color = disabledColor;
                interactable = false;
            }

            if (actionLabel != null)
                actionLabel.text = label;
            if (actionFace != null)
                actionFace.color = color;
            if (actionLip != null)
                actionLip.color = Color.Lerp(color, Color.black, 0.28f);
            actionButton.interactable = interactable;
        }
    }
}
