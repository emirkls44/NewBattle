using NewBattle.Audio;
using System;
using System.Collections.Generic;
using NewBattle.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// MAĞAZA: lobi animasyonlari, reklam izleyip bedava para ve para
    /// paketleri (odeme sistemi gelene kadar YAKINDA).
    ///
    /// Bir animasyon kartina dokununca sagdaki karakter onu oynatir; alttaki
    /// buton duruma gore SATIN AL / SEÇ / SEÇİLİ / "Seviye 5 gerekli" olur.
    /// Parayla alinan her sey once onay penceresi sorar.
    ///
    /// Ana menudeki "magaza", "para_ekle" ve "para_paneli" butonlari acar.
    /// </summary>
    public class ShopScreen : MonoBehaviour
    {
        [SerializeField] private ShopCatalog catalog;
        [SerializeField] private LobbyAnimationLibrary library;
        [SerializeField] private MainMenuScreen menu;

        [Header("Liste")]
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private RectTransform listContent;
        [Tooltip("Her urun icin cogaltilan kart. Gorunumunu buradan duzenle.")]
        [SerializeField] private ShopItemCard cardTemplate;

        [Header("Ust")]
        [SerializeField] private TextMeshProUGUI coinText;
        [SerializeField] private Button closeButton;

        [Header("Karakter Onizleme")]
        [SerializeField] private MenuCharacterStage characterStage;
        [SerializeField] private RawImage characterView;
        [SerializeField] private AspectRatioFitter characterAspect;
        [SerializeField] private TextMeshProUGUI previewNameText;

        [Header("Islem Butonu")]
        [SerializeField] private Button actionButton;
        [SerializeField] private Image actionFace;
        [SerializeField] private Image actionLip;
        [SerializeField] private TextMeshProUGUI actionLabel;
        [SerializeField] private Color selectColor = new(0.36f, 0.77f, 0.22f, 1f);
        [SerializeField] private Color buyColor = new(1f, 0.78f, 0.1f, 1f);
        [SerializeField] private Color disabledColor = new(0.6f, 0.6f, 0.62f, 1f);

        [Tooltip("Doluysa buton renk yerine bu resimlerle gosterilir (sec / satin al / kapali).")]
        [SerializeField] private Sprite selectSprite;
        [SerializeField] private Sprite buySprite;
        [SerializeField] private Sprite disabledSprite;

        [Header("Onay Penceresi")]
        [SerializeField] private GameObject confirmRoot;
        [SerializeField] private TextMeshProUGUI confirmText;
        [SerializeField] private Button confirmYes;
        [SerializeField] private Button confirmNo;

        [Header("Varsayilan Ikonlar")]
        [SerializeField] private Sprite animationIcon;
        [SerializeField] private Sprite adIcon;
        [SerializeField] private Sprite coinPackIcon;

        [Tooltip("Kurulum aracinin yerlesim surumu.")]
        [SerializeField, HideInInspector] private int layoutVersion;

        private readonly List<ShopItemCard> _cards = new();
        private ShopItem _preview;
        private Action _pendingConfirm;
        private bool _initialized;
        private bool _opening;

        public bool IsOpen => gameObject.activeSelf;

        private bool HasPreviewArea => actionButton != null;

        private void Awake()
        {
            Initialize();

            // Duzenlerken gorunur birakilmis olabilir; oyun kapali baslar.
            if (!_opening)
                gameObject.SetActive(false);
        }

        private void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;

            if (closeButton != null)
                closeButton.onClick.AddListener(Close);
            if (confirmYes != null)
                confirmYes.onClick.AddListener(OnConfirmYes);
            if (confirmNo != null)
                confirmNo.onClick.AddListener(HideConfirm);
            if (cardTemplate != null)
                cardTemplate.gameObject.SetActive(false);

            if (actionButton != null)
            {
                actionButton.onClick.AddListener(OnActionPressed);
                // Renkleri bu script yonetiyor; Unity'nin kendi gecisi ust uste binip titretmesin.
                actionButton.transition = Selectable.Transition.None;
            }
        }

        public void Open() => Open(scrollToCoins: false);

        /// <summary>Para butonlarindan acilinca listenin sonuna (para paketlerine) kayar.</summary>
        public void OpenCoins() => Open(scrollToCoins: true);

        public void Open(bool scrollToCoins)
        {
            Initialize();

            _opening = true;
            gameObject.SetActive(true);
            _opening = false;
            transform.SetAsLastSibling();

            HideConfirm();
            BuildCards();
            PreviewDefault();
            Refresh();

            if (scroll != null)
            {
                Canvas.ForceUpdateCanvases();
                scroll.verticalNormalizedPosition = scrollToCoins ? 0f : 1f;
            }

            PlayerProgress.Changed -= Refresh;
            PlayerProgress.Changed += Refresh;
        }

        public void Close()
        {
            PlayerProgress.Changed -= Refresh;
            HideConfirm();

            // Onizlemeden secili animasyona don.
            LobbyCharacterAnimator animator = characterStage != null ? characterStage.LobbyAnimator : null;
            if (animator != null)
                animator.PlayCurrent();

            gameObject.SetActive(false);
        }

        private void OnDestroy() => PlayerProgress.Changed -= Refresh;

        private void LateUpdate()
        {
            // Menudeki karakter goruntusunun (RenderTexture) aynisi.
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
            foreach (ShopItemCard card in _cards)
            {
                if (card != null)
                    Destroy(card.gameObject);
            }

            _cards.Clear();

            if (catalog == null || cardTemplate == null || listContent == null)
                return;

            foreach (ShopItem item in catalog.BuildList(library))
            {
                ShopItemCard card = Instantiate(cardTemplate, listContent);
                card.name = $"Urun_{item.id}";
                card.Setup(item);
                card.gameObject.SetActive(true);
                ShopItem captured = item;
                card.Button.onClick.AddListener(() => OnItemPressed(captured));
                _cards.Add(card);
            }
        }

        /// <summary>Acilista secili animasyonu (yoksa ilk animasyonu) onizler.</summary>
        private void PreviewDefault()
        {
            _preview = null;
            if (!HasPreviewArea || library == null)
                return;

            LobbyAnimation current = library.Current;
            foreach (ShopItemCard card in _cards)
            {
                if (card.Item.kind != ShopItemKind.LobbyAnimation)
                    continue;

                if (_preview == null || (current != null && card.Item.animationId == current.id))
                    _preview = card.Item;
            }
        }

        private void Refresh()
        {
            if (coinText != null)
                coinText.text = PlayerProgress.Coins.ToString();

            foreach (ShopItemCard card in _cards)
            {
                if (card == null || card.Item == null)
                    continue;

                ShopItem item = card.Item;
                bool previewing = item == _preview;

                switch (item.kind)
                {
                    case ShopItemKind.LobbyAnimation:
                        BindAnimationCard(card, previewing);
                        break;

                    case ShopItemKind.MenuBackground:
                        BindBackgroundCard(card);
                        break;

                    case ShopItemKind.WatchAdCoins:
                    {
                        int left = Mathf.Max(0, item.dailyLimit - AdWatchesToday(item));
                        card.Bind(left > 0 ? $"+{item.coins}" : "YARIN", left > 0,
                            left > 0 ? ShopItemCard.State.Ad : ShopItemCard.State.Owned,
                            $"Reklam izle · Bugün {left}/{item.dailyLimit}", adIcon, false);
                        break;
                    }

                    default:
                        card.Bind(string.IsNullOrEmpty(item.priceLabel) ? "YAKINDA" : item.priceLabel, false,
                            ShopItemCard.State.ComingSoon, $"{item.coins} Para", coinPackIcon, false);
                        break;
                }
            }

            RefreshPreview();
        }

        private void BindAnimationCard(ShopItemCard card, bool previewing)
        {
            LobbyAnimation animation = FindAnimation(card.Item);
            if (animation == null)
            {
                card.Bind("-", false, ShopItemCard.State.Locked, "Bulunamadı", animationIcon, previewing);
                return;
            }

            if (library.IsUnlocked(animation))
            {
                bool selected = library.Current == animation;
                card.Bind(selected ? "SEÇİLİ" : "SENİN", false, ShopItemCard.State.Owned, "Lobi animasyonu", animationIcon, previewing);
            }
            else if (animation.unlock == LobbyUnlockType.Coins)
            {
                card.Bind(animation.requirement.ToString(), true, ShopItemCard.State.Buy, "Lobi animasyonu", animationIcon, previewing);
            }
            else
            {
                card.Bind(library.RequirementText(animation), false, ShopItemCard.State.Locked, "Lobi animasyonu", animationIcon, previewing);
            }
        }

        // ------------------------------------------------------------------
        // Ana menu arka planlari
        // ------------------------------------------------------------------

        private void BindBackgroundCard(ShopItemCard card)
        {
            MenuBackgroundLibrary backgrounds = MenuBackgroundLibrary.Instance;
            MenuBackground background = backgrounds != null ? backgrounds.Find(card.Item.backgroundId) : null;
            MenuBackground current = backgrounds != null ? backgrounds.Current : null;

            if (background == null)
            {
                // Varsayilan sari: herkesin, bedava.
                bool active = current == null;
                card.Bind(active ? "AKTİF" : "KULLAN", false, active ? ShopItemCard.State.Owned : ShopItemCard.State.Buy,
                    "Menü arka planı", animationIcon, false);
                return;
            }

            if (!backgrounds.IsOwned(background))
            {
                card.Bind(background.price.ToString(), true, ShopItemCard.State.Buy, "Menü arka planı", animationIcon, false);
                return;
            }

            bool selected = current == background;
            card.Bind(selected ? "AKTİF" : "KULLAN", false, selected ? ShopItemCard.State.Owned : ShopItemCard.State.Buy,
                selected ? "Dokun: kapat" : "Menü arka planı", animationIcon, false);
        }

        /// <summary>
        /// Alinmamissa satin al (onayla), alinmissa etkinlestir; etkinse kapat (sariya don).
        /// </summary>
        private void BackgroundPressed(ShopItem item)
        {
            MenuBackgroundLibrary backgrounds = MenuBackgroundLibrary.Instance;
            if (backgrounds == null)
                return;

            MenuBackground background = backgrounds.Find(item.backgroundId);
            MenuBackground current = backgrounds.Current;

            if (background == null || current == background)
            {
                bool changed = current != null;
                backgrounds.Select(null);
                Toast(changed ? "VARSAYILAN ARKA PLAN" : "ZATEN AKTİF");
                Refresh();
                return;
            }

            if (backgrounds.IsOwned(background))
            {
                backgrounds.Select(background);
                Toast("ARKA PLAN DEĞİŞTİ!");
                Refresh();
                return;
            }

            if (PlayerProgress.Coins < background.price)
            {
                Toast("PARA YETMİYOR!");
                Sfx.PlayUI("hata");
                return;
            }

            AskConfirm($"{background.displayName}\n{background.price} para karşılığında alınsın mı?", () =>
            {
                if (backgrounds.TryPurchase(background))
                {
                    backgrounds.Select(background);
                    Toast("SATIN ALINDI!");
                    Sfx.PlayUI("satin_al");
                }
                else
                {
                    Toast("PARA YETMİYOR!");
                    Sfx.PlayUI("hata");
                }

                Refresh();
            });
        }

        private void RefreshPreview()
        {
            LobbyAnimation animation = FindAnimation(_preview);

            if (previewNameText != null)
                previewNameText.text = animation != null ? animation.displayName : string.Empty;

            if (actionButton == null)
                return;

            actionButton.gameObject.SetActive(animation != null);
            if (animation == null)
                return;

            string label;
            Color color;
            bool interactable;

            if (library.Current == animation)
            {
                label = "SEÇİLİ";
                color = disabledColor;
                interactable = false;
            }
            else if (library.IsUnlocked(animation))
            {
                label = "SEÇ";
                color = selectColor;
                interactable = true;
            }
            else if (animation.unlock == LobbyUnlockType.Coins)
            {
                label = $"SATIN AL  {animation.requirement}";
                color = buyColor;
                interactable = true;
            }
            else
            {
                label = $"{library.RequirementText(animation)} GEREKLİ";
                color = disabledColor;
                interactable = false;
            }

            SetActionLook(label, color, interactable);
        }

        private void SetActionLook(string label, Color color, bool interactable)
        {
            if (actionLabel != null)
                actionLabel.text = label;
            Sprite sprite = color == selectColor ? selectSprite : color == buyColor ? buySprite : disabledSprite;
            if (actionFace != null && sprite != null)
            {
                actionFace.sprite = sprite;
                actionFace.color = Color.white;
            }
            else if (actionFace != null)
            {
                actionFace.color = color;
            }

            if (actionLip != null)
                actionLip.color = Color.Lerp(color, Color.black, 0.28f);

            actionButton.interactable = interactable;
        }

        private void OnItemPressed(ShopItem item)
        {
            switch (item.kind)
            {
                case ShopItemKind.LobbyAnimation:
                    if (HasPreviewArea)
                        PreviewAnimation(item);
                    else
                        BuyOrSelect(item);
                    break;

                case ShopItemKind.WatchAdCoins:
                    WatchAdForCoins(item);
                    break;

                case ShopItemKind.MenuBackground:
                    BackgroundPressed(item);
                    break;

                default:
                    // Gercek odeme (Google Play / App Store) henuz yok.
                    Toast("YAKINDA!");
                    break;
            }
        }

        private void PreviewAnimation(ShopItem item)
        {
            _preview = item;

            LobbyAnimation animation = FindAnimation(item);
            LobbyCharacterAnimator animator = characterStage != null ? characterStage.LobbyAnimator : null;
            if (animator != null && animation != null)
                animator.Play(animation);

            Refresh();
        }

        private void OnActionPressed()
        {
            if (_preview != null)
                BuyOrSelect(_preview);
        }

        private void BuyOrSelect(ShopItem item)
        {
            LobbyAnimation animation = FindAnimation(item);
            if (animation == null)
            {
                Toast("BU ÜRÜN BULUNAMADI");
                return;
            }

            if (library.IsUnlocked(animation))
            {
                if (library.Select(animation))
                    Toast("SEÇİLDİ!");
                return;
            }

            if (animation.unlock != LobbyUnlockType.Coins)
            {
                Toast($"{TurkishUpper(library.RequirementText(animation))} GEREKLİ!");
                return;
            }

            if (PlayerProgress.Coins < animation.requirement)
            {
                Toast("PARA YETMİYOR!");
                Sfx.PlayUI("hata");
                return;
            }

            AskConfirm($"{animation.displayName}\n{animation.requirement} para karşılığında alınsın mı?", () =>
            {
                if (library.TryPurchase(animation))
                {
                    library.Select(animation);
                    Toast("SATIN ALINDI!");
                    Sfx.PlayUI("satin_al");
                }
                else
                {
                    Toast("PARA YETMİYOR!");
                    Sfx.PlayUI("hata");
                }
            });
        }

        private void WatchAdForCoins(ShopItem item)
        {
            if (AdWatchesToday(item) >= item.dailyLimit)
            {
                Toast("YARIN TEKRAR GEL!");
                return;
            }

            if (RewardedAds.IsShowing)
                return;

            Toast("REKLAM İZLENİYOR...");
            RewardedAds.Show(this, watched =>
            {
                if (!watched)
                    return;

                PlayerPrefs.SetInt(AdKey(item), AdWatchesToday(item) + 1);
                PlayerProgress.AddCoins(item.coins);
                Toast($"+{item.coins} PARA!");
                Sfx.PlayUI("para");
            });
        }

        private LobbyAnimation FindAnimation(ShopItem item) =>
            item != null && item.kind == ShopItemKind.LobbyAnimation && library != null ? library.Find(item.animationId) : null;

        private void AskConfirm(string message, Action onYes)
        {
            if (confirmRoot == null)
            {
                onYes();
                return;
            }

            _pendingConfirm = onYes;
            if (confirmText != null)
                confirmText.text = message;

            confirmRoot.SetActive(true);
            confirmRoot.transform.SetAsLastSibling();
        }

        private void OnConfirmYes()
        {
            Action action = _pendingConfirm;
            HideConfirm();
            action?.Invoke();
        }

        private void HideConfirm()
        {
            _pendingConfirm = null;
            if (confirmRoot != null)
                confirmRoot.SetActive(false);
        }

        private void Toast(string message)
        {
            if (menu != null)
                menu.ShowToast(message);
        }

        /// <summary>"Seviye" -> "SEVİYE" (ToUpperInvariant noktali I'yi kaybeder).</summary>
        private static string TurkishUpper(string text) => text.Replace('i', 'İ').ToUpperInvariant();

        private static int AdWatchesToday(ShopItem item) => PlayerPrefs.GetInt(AdKey(item), 0);

        private static string AdKey(ShopItem item) => $"nb_shop_ad_{item.id}_{DateTime.Now:yyyyMMdd}";
    }
}
