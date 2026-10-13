using NewBattle.Audio;
using System.Collections.Generic;
using NewBattle.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// "KARAKTER" ekrani, iki sekme:
    ///   - KARAKTERLER: oyuncunun karakteri (hediye karakterleri sadece sahibine gorunur).
    ///   - ANİMASYONLAR: lobide oynayacak animasyon.
    ///
    /// Bir karta dokunmak karakterde onu ONIZLER (kilitli olsa bile). Alttaki
    /// buton duruma gore SEÇ / SEÇİLİ / MAĞAZAYA GİT / "Seviye 5 gerekli" olur.
    /// Kapatinca menudeki karakter secili haline doner.
    ///
    /// Ana menudeki "karakter" eylem kimlikli buton bu ekrani acar.
    /// </summary>
    public class LobbyAnimationScreen : MonoBehaviour
    {
        private enum Tab
        {
            Characters,
            Animations
        }

        [SerializeField] private LobbyAnimationLibrary library;
        [SerializeField] private MenuCharacterStage characterStage;
        [SerializeField] private MainMenuScreen menu;

        [Header("Liste")]
        [SerializeField] private RectTransform listContent;
        [Tooltip("Her animasyon / karakter icin cogaltilan kart. Gorunumunu buradan duzenle.")]
        [SerializeField] private LobbyAnimationCard cardTemplate;

        [Header("Sekmeler")]
        [SerializeField] private Button charactersTab;
        [SerializeField] private Button animationsTab;
        [SerializeField] private TextMeshProUGUI subtitleText;
        [SerializeField] private Color tabOnColor = new(1f, 0.78f, 0.1f, 1f);
        [SerializeField] private Color tabOffColor = new(0.85f, 0.62f, 0.25f, 1f);

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
        private CharacterDefinition _previewCharacter;
        private Tab _tab = Tab.Characters;
        private bool _initialized;
        private bool _opening;

        public bool IsOpen => gameObject.activeSelf;

        private static CharacterLibrary Characters => CharacterLibrary.Instance;

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
            if (charactersTab != null)
                charactersTab.onClick.AddListener(() => SwitchTab(Tab.Characters));
            if (animationsTab != null)
                animationsTab.onClick.AddListener(() => SwitchTab(Tab.Animations));
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

            // Sekme yoksa (eski kurulum) sadece animasyonlar.
            if (charactersTab == null || Characters == null)
                _tab = Tab.Animations;

            SwitchTab(_tab);

            PlayerProgress.Changed -= Refresh;
            PlayerProgress.Changed += Refresh;
        }

        public void Close()
        {
            PlayerProgress.Changed -= Refresh;

            // Onizlemeden secili karaktere ve animasyona don.
            if (characterStage != null)
                characterStage.ShowSelectedCharacter();

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

        private void SwitchTab(Tab tab)
        {
            _tab = tab;

            if (charactersTab != null && charactersTab.targetGraphic != null)
                charactersTab.targetGraphic.color = tab == Tab.Characters ? tabOnColor : tabOffColor;
            if (animationsTab != null && animationsTab.targetGraphic != null)
                animationsTab.targetGraphic.color = tab == Tab.Animations ? tabOnColor : tabOffColor;
            if (subtitleText != null)
                subtitleText.text = tab == Tab.Characters ? "Karakterini seç" : "Lobi animasyonunu seç";

            _preview = library.Current;
            _previewCharacter = Characters != null ? Characters.Current : null;

            // Sekme degisince onizleme secili hale doner.
            if (characterStage != null)
                characterStage.ShowSelectedCharacter();
            LobbyCharacterAnimator animator = characterStage != null ? characterStage.LobbyAnimator : null;
            if (animator != null)
                animator.PlayCurrent();

            BuildCards();
            Refresh();
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

            if (_tab == Tab.Characters)
            {
                foreach (CharacterDefinition character in Characters.Characters)
                {
                    // Hediye karakterini sadece sahibi gorur.
                    if (!Characters.IsVisible(character))
                        continue;

                    LobbyAnimationCard card = CreateCard($"Karakter_{character.id}");
                    card.Setup(character);
                    CharacterDefinition captured = character;
                    card.Button.onClick.AddListener(() => OnCharacterPressed(captured));
                }

                return;
            }

            foreach (LobbyAnimation animation in library.Animations)
            {
                if (animation == null)
                    continue;

                LobbyAnimationCard card = CreateCard($"Kart_{animation.id}");
                card.Setup(animation);
                LobbyAnimation captured = animation;
                card.Button.onClick.AddListener(() => OnCardPressed(captured));
            }
        }

        private LobbyAnimationCard CreateCard(string objectName)
        {
            LobbyAnimationCard card = Instantiate(cardTemplate, listContent);
            card.name = objectName;
            card.gameObject.SetActive(true);
            _cards.Add(card);
            return card;
        }

        private void OnCharacterPressed(CharacterDefinition character)
        {
            _previewCharacter = character;

            if (characterStage != null)
                characterStage.ShowCharacter(character);

            if (!Characters.IsUnlocked(character) && menu != null)
            {
                Sfx.PlayUI("hata");
                menu.ShowToast(character.unlock == CharacterUnlockType.Coins ||
                               (character.unlock == CharacterUnlockType.LevelAndCoins && Characters.CanPurchase(character))
                    ? "ÖNCE SATIN ALMAN LAZIM!"
                    : $"{TurkishUpper(Characters.RequirementText(character))} GEREKLİ!");
            }

            Refresh();
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
                Sfx.PlayUI("hata");
                menu.ShowToast(animation.unlock == LobbyUnlockType.Coins
                    ? "ÖNCE SATIN ALMAN LAZIM!"
                    : $"{TurkishUpper(library.RequirementText(animation))} GEREKLİ!");
            }

            Refresh();
        }

        private void OnActionPressed()
        {
            if (_tab == Tab.Characters)
            {
                if (_previewCharacter == null)
                    return;

                if (Characters.IsUnlocked(_previewCharacter))
                {
                    Characters.Select(_previewCharacter);
                    Refresh();
                }
                else if (_previewCharacter.unlock == CharacterUnlockType.Coins && menu != null)
                {
                    menu.ShowToast("ÖNCE SATIN ALMAN LAZIM!");
                }

                return;
            }

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

            if (coinText != null)
                coinText.text = PlayerProgress.Coins.ToString();

            if (_tab == Tab.Characters)
            {
                CharacterDefinition selectedCharacter = Characters.Current;
                foreach (LobbyAnimationCard card in _cards)
                {
                    if (card != null && card.Character != null)
                        card.Bind(card.Character, Characters, card.Character == selectedCharacter, card.Character == _previewCharacter);
                }

                if (previewNameText != null)
                    previewNameText.text = _previewCharacter != null ? _previewCharacter.displayName : string.Empty;

                RefreshCharacterAction(selectedCharacter);
                return;
            }

            LobbyAnimation selected = library.Current;

            foreach (LobbyAnimationCard card in _cards)
            {
                if (card != null && card.Animation != null)
                    card.Bind(card.Animation, library, card.Animation == selected, card.Animation == _preview);
            }

            if (previewNameText != null)
                previewNameText.text = _preview != null ? _preview.displayName : string.Empty;

            RefreshActionButton(selected);
        }

        private void RefreshCharacterAction(CharacterDefinition selected)
        {
            if (actionButton == null || _previewCharacter == null)
                return;

            if (_previewCharacter == selected)
                SetAction("SEÇİLİ", disabledColor, false);
            else if (Characters.IsUnlocked(_previewCharacter))
                SetAction("SEÇ", selectColor, true);
            else if (_previewCharacter.unlock == CharacterUnlockType.Coins)
                SetAction($"{_previewCharacter.requirement} PARA", buyColor, true);
            else
                SetAction($"{TurkishUpper(Characters.RequirementText(_previewCharacter))} GEREKLİ", disabledColor, false);
        }

        private void RefreshActionButton(LobbyAnimation selected)
        {
            if (actionButton == null || _preview == null)
                return;

            if (_preview == selected)
                SetAction("SEÇİLİ", disabledColor, false);
            else if (library.IsUnlocked(_preview))
                SetAction("SEÇ", selectColor, true);
            else if (_preview.unlock == LobbyUnlockType.Coins)
                SetAction("MAĞAZAYA GİT", buyColor, true);
            else
                SetAction($"{TurkishUpper(library.RequirementText(_preview))} GEREKLİ", disabledColor, false);
        }

        private void SetAction(string label, Color color, bool interactable)
        {
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
