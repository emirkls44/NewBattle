using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// Seviye Yolu'ndaki tek seviye karti. Ekran "KartSablonu"nu her seviye
    /// icin cogaltir; sablonu Hierarchy'den duzenlersen tum kartlar uyar.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class LevelRoadCard : MonoBehaviour
    {
        public enum State
        {
            Reached,
            Next,
            Future
        }

        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI rankText;
        [SerializeField] private Image rewardIcon;
        [SerializeField] private TextMeshProUGUI rewardText;
        [SerializeField] private GameObject reachedMark;
        [SerializeField] private GameObject nextOutline;

        [Header("Renkler")]
        [SerializeField] private Color reachedColor = new(0.55f, 0.50f, 0.62f, 1f);
        [SerializeField] private Color nextColor = new(1f, 0.95f, 0.82f, 1f);
        [SerializeField] private Color futureColor = new(0.86f, 0.80f, 0.68f, 1f);

        public int Level { get; private set; }
        public Button Button => GetComponent<Button>();

        public void Bind(int level, string rank, string reward, Sprite icon, State state)
        {
            Level = level;

            if (levelText != null)
                levelText.text = $"SEVİYE {level}";
            if (rankText != null)
                rankText.text = rank;
            if (rewardText != null)
                rewardText.text = reward;
            if (rewardIcon != null && icon != null)
                rewardIcon.sprite = icon;

            if (background != null)
                background.color = state switch
                {
                    State.Reached => reachedColor,
                    State.Next => nextColor,
                    _ => futureColor
                };

            if (reachedMark != null)
                reachedMark.SetActive(state == State.Reached);
            if (nextOutline != null)
                nextOutline.SetActive(state == State.Next);
        }
    }
}
