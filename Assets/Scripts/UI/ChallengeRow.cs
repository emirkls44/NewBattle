using NewBattle.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// Mucadeleler ekranindaki tek gorev satiri: ad, ilerleme cubugu, odul.
    /// Tamamlaninca "TAMAMLANDI" gorunur. Ekran "SatirSablonu"nu cogaltir.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ChallengeRow : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI progressText;
        [Tooltip("Dolum cubugu. Genisligi anchorMax.x ile ayarlanir.")]
        [SerializeField] private RectTransform progressFill;
        [SerializeField] private TextMeshProUGUI rewardText;
        [Tooltip("Tamamlaninca gorunen 'TAMAMLANDI' etiketi.")]
        [SerializeField] private GameObject doneMark;

        [Header("Renkler")]
        [SerializeField] private Color activeColor = new(1f, 0.95f, 0.82f, 1f);
        [SerializeField] private Color doneColor = new(0.78f, 0.93f, 0.70f, 1f);

        public ChallengeStatus Status { get; private set; }
        public Button Button => GetComponent<Button>();

        public void Bind(ChallengeStatus status)
        {
            Status = status;
            ChallengeDef def = status.Def;

            if (titleText != null)
                titleText.text = def.title;
            if (progressText != null)
                progressText.text = $"{status.Progress} / {def.target}";
            if (progressFill != null)
                progressFill.anchorMax = new Vector2(Mathf.Clamp01(status.Progress / (float)Mathf.Max(1, def.target)), progressFill.anchorMax.y);
            if (rewardText != null)
                rewardText.text = $"+{def.xp} XP";
            if (doneMark != null)
                doneMark.SetActive(status.Done);
            if (background != null)
                background.color = status.Done ? doneColor : activeColor;
        }
    }
}
