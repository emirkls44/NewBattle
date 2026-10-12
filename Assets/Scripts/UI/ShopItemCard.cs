using NewBattle.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// Magazadaki tek urun karti. Ekran "KartSablonu" nesnesini her urun icin
    /// cogaltir; sablonun gorunumunu Hierarchy'den degistirirsen tum kartlar
    /// ona uyar.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ShopItemCard : MonoBehaviour
    {
        public enum State
        {
            Buy,
            Ad,
            Owned,
            Locked,
            ComingSoon
        }

        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI nameText;
        [Tooltip("Onizlenen kartin etrafindaki cerceve (istege bagli).")]
        [SerializeField] private GameObject previewOutline;

        [Header("Fiyat")]
        [SerializeField] private Image priceBackground;
        [SerializeField] private Image priceCoin;
        [SerializeField] private TextMeshProUGUI priceText;

        [Header("Durum")]
        [Tooltip("Sahip olunan urunde gorunen tik.")]
        [SerializeField] private GameObject ownedMark;
        [Tooltip("'Bugün 2/5' gibi ek bilgi.")]
        [SerializeField] private TextMeshProUGUI noteText;

        [Header("Renkler")]
        [SerializeField] private Color buyColor = new(0.36f, 0.77f, 0.22f, 1f);
        [SerializeField] private Color adColor = new(0.27f, 0.64f, 0.95f, 1f);
        [SerializeField] private Color doneColor = new(0.6f, 0.6f, 0.62f, 1f);
        [SerializeField] private Color lockedColor = new(0.45f, 0.40f, 0.36f, 1f);
        [SerializeField] private Color comingSoonColor = new(0.98f, 0.65f, 0.11f, 1f);

        public ShopItem Item { get; private set; }
        public Button Button => GetComponent<Button>();

        /// <summary>Kart olusturulurken hangi urunu gosterecegini bildirir.</summary>
        public void Setup(ShopItem item) => Item = item;

        public void Bind(string price, bool showCoin, State state, string note, Sprite fallbackIcon, bool previewing)
        {
            if (Item == null)
                return;

            if (nameText != null)
                nameText.text = Item.displayName;

            if (icon != null)
            {
                Sprite sprite = Item.icon != null ? Item.icon : fallbackIcon;
                if (sprite != null)
                    icon.sprite = sprite;
            }

            if (priceText != null)
                priceText.text = price;

            if (priceCoin != null)
                priceCoin.gameObject.SetActive(showCoin);

            if (priceBackground != null)
            {
                priceBackground.color = state switch
                {
                    State.Ad => adColor,
                    State.Owned => doneColor,
                    State.Locked => lockedColor,
                    State.ComingSoon => comingSoonColor,
                    _ => buyColor
                };
            }

            if (ownedMark != null)
                ownedMark.SetActive(state == State.Owned && Item.kind == ShopItemKind.LobbyAnimation);

            if (noteText != null)
            {
                noteText.text = note ?? string.Empty;
                noteText.gameObject.SetActive(!string.IsNullOrEmpty(note));
            }

            if (previewOutline != null)
                previewOutline.SetActive(previewing);
        }
    }
}
