using NewBattle.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// PROFİL: isim, seviye, rutbe ve istatistikler. Sunucu gelene kadar
    /// menudeki "liderlik" butonu bunu acar.
    /// </summary>
    public class ProfileScreen : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI levelText;

        [Header("Istatistikler")]
        [SerializeField] private TextMeshProUGUI matchesText;
        [SerializeField] private TextMeshProUGUI winsText;
        [SerializeField] private TextMeshProUGUI winRateText;
        [SerializeField] private TextMeshProUGUI killsText;
        [SerializeField] private TextMeshProUGUI killsPerMatchText;
        [SerializeField] private TextMeshProUGUI bestPlacementText;

        [SerializeField] private Button closeButton;

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
            if (closeButton != null)
                closeButton.onClick.AddListener(Close);
        }

        public void Open()
        {
            Initialize();

            _opening = true;
            gameObject.SetActive(true);
            _opening = false;
            transform.SetAsLastSibling();

            PlayerProgress.Changed -= Refresh;
            PlayerProgress.Changed += Refresh;
            Refresh();
        }

        public void Close()
        {
            PlayerProgress.Changed -= Refresh;
            gameObject.SetActive(false);
        }

        private void OnDestroy() => PlayerProgress.Changed -= Refresh;

        private void Refresh()
        {
            int matches = PlayerProgress.MatchesPlayed;
            int wins = PlayerProgress.Wins;
            int kills = PlayerProgress.TotalKills;
            int best = PlayerProgress.BestPlacement;
            int level = PlayerProgress.Level;

            Set(nameText, PlayerProgress.PlayerName);
            Set(levelText, $"SEVİYE {level} · {PlayerProgress.RankName(level)}");
            Set(matchesText, matches.ToString());
            Set(winsText, wins.ToString());
            Set(winRateText, matches > 0 ? $"%{Mathf.RoundToInt(100f * wins / matches)}" : "-");
            Set(killsText, kills.ToString());
            Set(killsPerMatchText, matches > 0 ? (kills / (float)matches).ToString("0.0") : "-");
            Set(bestPlacementText, best > 0 ? $"#{best}" : "-");
        }

        private static void Set(TextMeshProUGUI target, string value)
        {
            if (target != null)
                target.text = value;
        }
    }
}
