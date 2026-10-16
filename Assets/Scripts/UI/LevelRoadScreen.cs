using System.Collections.Generic;
using NewBattle.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// SEVİYE YOLU: oyuncunun seviyesi, XP'si ve ilerideki seviyelerde neler
    /// kazanacagi (para, lobi animasyonu, rutbe). Menudeki sezon karti ve
    /// rutbe ikonu acar. Oduller IlerlemeAyarlari'ndan okunur.
    /// </summary>
    public class LevelRoadScreen : MonoBehaviour
    {
        [SerializeField] private LobbyAnimationLibrary library;
        [SerializeField] private MainMenuScreen menu;

        [Header("Ust")]
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI rankText;
        [SerializeField] private TextMeshProUGUI xpText;
        [Tooltip("Dolum cubugu. Genisligi anchorMax.x ile ayarlanir.")]
        [SerializeField] private RectTransform xpFill;
        [SerializeField] private Button closeButton;

        [Header("Liste")]
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private RectTransform listContent;
        [SerializeField] private LevelRoadCard cardTemplate;
        [Tooltip("Oyuncunun seviyesinden sonra kac seviye gosterilsin.")]
        [SerializeField, Min(3)] private int levelsAhead = 15;

        [Header("Ikonlar")]
        [SerializeField] private Sprite coinIcon;
        [SerializeField] private Sprite animationIcon;

        private readonly List<LevelRoadCard> _cards = new();
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
            if (cardTemplate != null)
                cardTemplate.gameObject.SetActive(false);
        }

        public void Open()
        {
            Initialize();

            _opening = true;
            gameObject.SetActive(true);
            _opening = false;
            transform.SetAsLastSibling();

            Build();

            PlayerProgress.Changed -= Build;
            PlayerProgress.Changed += Build;
        }

        public void Close()
        {
            PlayerProgress.Changed -= Build;
            gameObject.SetActive(false);
        }

        private void OnDestroy() => PlayerProgress.Changed -= Build;

        private void Build()
        {
            int level = PlayerProgress.Level;
            int xp = PlayerProgress.Xp;
            int needed = PlayerProgress.XpForNextLevel(level);

            SetText(levelText, $"SEVİYE {level}");
            SetText(rankText, PlayerProgress.RankName(level));
            SetText(xpText, $"{xp} / {needed} XP");
            if (xpFill != null)
                xpFill.anchorMax = new Vector2(Mathf.Clamp01(xp / (float)Mathf.Max(1, needed)), xpFill.anchorMax.y);

            foreach (LevelRoadCard card in _cards)
            {
                if (card != null)
                    Destroy(card.gameObject);
            }

            _cards.Clear();

            if (cardTemplate == null || listContent == null)
                return;

            int last = Mathf.Max(level + levelsAhead, 10);
            for (int target = 2; target <= last; target++)
            {
                LevelRoadCard card = Instantiate(cardTemplate, listContent);
                card.name = $"Seviye_{target}";
                card.gameObject.SetActive(true);

                LevelRoadCard.State state = target == level ? LevelRoadCard.State.Current
                    : target < level ? LevelRoadCard.State.Reached
                    : target == level + 1 ? LevelRoadCard.State.Next
                    : LevelRoadCard.State.Future;

                string reward = RewardText(target, out bool hasAnimation);
                card.Bind(target, PlayerProgress.RankName(target), reward, hasAnimation ? animationIcon : coinIcon, state);

                int captured = target;
                card.Button.onClick.AddListener(() => OnCardPressed(captured));
                _cards.Add(card);
            }

            // Siradaki seviye gorunsun.
            if (scroll != null && _cards.Count > 1)
            {
                Canvas.ForceUpdateCanvases();
                scroll.horizontalNormalizedPosition = Mathf.Clamp01((level - 2.5f) / (_cards.Count - 1));
            }
        }

        private string RewardText(int level, out bool hasAnimation)
        {
            var lines = new List<string>();
            LevelReward reward = ProgressionSettings.Instance.LevelRewardFor(level);
            hasAnimation = false;

            if (reward.coins > 0)
                lines.Add($"+{reward.coins} Para");

            if (!string.IsNullOrEmpty(reward.animationId))
            {
                lines.Add(string.IsNullOrEmpty(reward.animationLabel) ? "Animasyon" : reward.animationLabel);
                hasAnimation = true;
            }

            // Bu seviyede kilidi acilan lobi animasyonlari.
            if (library != null)
            {
                foreach (LobbyAnimation animation in library.Animations)
                {
                    if (animation != null && animation.unlock == LobbyUnlockType.Level && animation.requirement == level)
                    {
                        lines.Add(animation.displayName);
                        hasAnimation = true;
                    }
                }
            }

            return string.Join("\n", lines);
        }

        private void OnCardPressed(int target)
        {
            if (menu == null)
                return;

            int level = PlayerProgress.Level;
            if (target <= level)
                menu.ShowToast("BU ÖDÜLÜ ALDIN!");
            else
                menu.ShowToast($"{XpUntil(target)} XP KALDI");
        }

        private static long XpUntil(int target)
        {
            int level = PlayerProgress.Level;
            long total = PlayerProgress.XpForNextLevel(level) - PlayerProgress.Xp;
            for (int l = level + 1; l < target; l++)
                total += PlayerProgress.XpForNextLevel(l);
            return total;
        }

        private static void SetText(TextMeshProUGUI target, string value)
        {
            if (target != null)
                target.text = value;
        }
    }
}
