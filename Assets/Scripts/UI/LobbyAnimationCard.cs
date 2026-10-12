using NewBattle.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// Lobi animasyonu secim ekranindaki tek kart. Ekran, "KartSablonu"
    /// nesnesini her animasyon icin cogaltir; sablonun gorunumunu Hierarchy'den
    /// degistirebilirsin, tum kartlar ona uyar.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class LobbyAnimationCard : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private GameObject previewOutline;
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private GameObject lockMark;
        [SerializeField] private GameObject selectedMark;

        [Header("Renkler")]
        [SerializeField] private Color unlockedColor = new(1f, 0.95f, 0.82f, 1f);
        [SerializeField] private Color lockedColor = new(0.86f, 0.78f, 0.62f, 1f);

        public LobbyAnimation Animation { get; private set; }
        public Button Button => GetComponent<Button>();

        public void Bind(LobbyAnimation animation, LobbyAnimationLibrary library, bool selected, bool previewing)
        {
            Animation = animation;
            bool unlocked = library.IsUnlocked(animation);

            if (nameText != null)
                nameText.text = animation.displayName;

            if (statusText != null)
                statusText.text = selected ? "SEÇİLİ" : unlocked ? "Açık" : library.RequirementText(animation);

            if (icon != null && animation.icon != null)
                icon.sprite = animation.icon;

            if (background != null)
                background.color = unlocked ? unlockedColor : lockedColor;

            if (lockMark != null)
                lockMark.SetActive(!unlocked);

            if (selectedMark != null)
                selectedMark.SetActive(selected);

            if (previewOutline != null)
                previewOutline.SetActive(previewing);
        }
    }
}
