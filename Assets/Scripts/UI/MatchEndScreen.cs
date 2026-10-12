using System;
using System.Collections;
using NewBattle.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// Battlelands tarzi mac sonu ekrani: "#17 Mac sona erdi!", olum mesaji,
    /// oldurme / XP / para kartlari, ortada karakter, dolan seviye bari ve
    /// DEVAM ET.
    ///
    /// Gosterildigi anda maç odullerini (XP, para, galibiyet) BIR KEZ
    /// oyuncunun ilerlemesine ekler. Mac mantigi (kim kazandi, kacinci oldun)
    /// MatchResultHUD'da; bu ekran sadece sonucu gosterir.
    /// </summary>
    public class MatchEndScreen : MonoBehaviour
    {
        [Header("Gorunurluk")]
        [SerializeField] private Canvas screenCanvas;

        [Header("Ust")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Color victoryTitleColor = new(1f, 0.82f, 0.18f, 1f);
        [SerializeField] private Color defeatTitleColor = new(1f, 0.6f, 0.12f, 1f);

        [Header("Kartlar")]
        [SerializeField] private RectTransform[] cards;
        [SerializeField] private TextMeshProUGUI killsText;
        [SerializeField] private TextMeshProUGUI xpText;
        [SerializeField] private TextMeshProUGUI coinsText;
        [Tooltip("Reklamla kazanilan cift XP bu macta kullanildiysa XP kartinda gorunen '2x' rozeti.")]
        [SerializeField] private RectTransform doubleXpBadge;

        [Header("Karakter")]
        [SerializeField] private RawImage characterImage;
        [Tooltip("Kazaninca karakterin oynayacagi animasyon (Humanoid klip; Mixamo 'Victory' gibi).")]
        [SerializeField] private AnimationClip victoryClip;
        [Tooltip("Elenince karakterin oynayacagi animasyon (Mixamo 'Defeated' gibi).")]
        [SerializeField] private AnimationClip defeatClip;

        [Header("Seviye")]
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI xpNumbersText;
        [SerializeField] private TextMeshProUGUI nextLevelText;
        [Tooltip("Dolum cubugu. Genisligi anchorMax.x ile ayarlanir.")]
        [SerializeField] private RectTransform xpFill;
        [SerializeField] private RectTransform levelUpBanner;

        [Header("Devam")]
        [SerializeField] private Button continueButton;

        [Header("Bildirim")]
        [Tooltip("Islevi henuz baglanmamis butonlara basinca 'YAKINDA!' gosterir.")]
        [SerializeField] private ToastView toast;

        [Tooltip("Kurulum aracinin yerlesim surumu (eski kurulumlari duzeltmek icin).")]
        [SerializeField, HideInInspector] private int layoutVersion;

        [Header("Zamanlama")]
        [SerializeField, Min(0f)] private float cardDelay = 0.15f;
        [SerializeField, Min(0.05f)] private float countSeconds = 0.6f;
        [SerializeField, Min(0.05f)] private float barSecondsPerLevel = 0.9f;

        private Action _onContinue;
        private bool _shown;

        public RawImage CharacterImage => characterImage;

        /// <summary>Sonuca gore karakterin oynayacagi klip (bossa duruyor).</summary>
        public AnimationClip ResultClip(bool victory) => victory ? victoryClip : defeatClip;

        private void Awake()
        {
            if (screenCanvas == null)
                screenCanvas = GetComponent<Canvas>();

            if (screenCanvas != null)
                screenCanvas.enabled = false;

            if (continueButton != null)
                continueButton.onClick.AddListener(OnContinuePressed);
        }

        /// <summary>
        /// Sonucu gosterir ve odulleri verir. Ayni macta ikinci kez cagrilirsa
        /// hicbir sey yapmaz (oduller iki kez verilmesin).
        /// </summary>
        public void Show(MatchOutcome outcome, string message, Action onContinue)
        {
            if (_shown)
                return;

            _shown = true;
            _onContinue = onContinue;

            MatchRewardResult rewards = MatchRewards.Apply(outcome);

            if (titleText != null)
            {
                titleText.text = outcome.Victory
                    ? "#1 Zafer!"
                    : $"#{Mathf.Max(1, outcome.Placement)} Maç sona erdi!";
                titleText.color = outcome.Victory ? victoryTitleColor : defeatTitleColor;
            }

            if (messageText != null)
                messageText.text = message;

            SetLevelDisplay(rewards.LevelBefore, rewards.XpBefore);

            if (levelUpBanner != null)
                levelUpBanner.gameObject.SetActive(false);

            if (doubleXpBadge != null)
            {
                UIBaseScale.Get(doubleXpBadge);
                doubleXpBadge.localScale = Vector3.zero;
                doubleXpBadge.gameObject.SetActive(rewards.DoubleXp);
            }

            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(true);
                continueButton.interactable = false;

                // Animasyonlar bitene kadar gizli (sifir boyut); sonunda bir kez
                // ziplayarak cikar. Asil olcek once hatirlaniyor.
                Transform continueTransform = continueButton.transform;
                UIBaseScale.Get(continueTransform);
                continueTransform.localScale = Vector3.zero;
            }

            foreach (RectTransform card in cards)
            {
                if (card == null)
                    continue;

                UIBaseScale.Get(card); // asil olcegi sifirlamadan once hatirla
                card.localScale = Vector3.zero;
            }

            SetText(killsText, "0");
            SetText(xpText, "0");
            SetText(coinsText, "0");

            if (screenCanvas != null)
                screenCanvas.enabled = true;

            MenuButton.Unhandled -= OnUnhandledButton;
            MenuButton.Unhandled += OnUnhandledButton;

            transform.SetAsLastSibling();
            StartCoroutine(Reveal(outcome, rewards));
        }

        private IEnumerator Reveal(MatchOutcome outcome, MatchRewardResult rewards)
        {
            int[] values = { outcome.Kills, rewards.Xp, rewards.Coins };
            TextMeshProUGUI[] targets = { killsText, xpText, coinsText };

            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] != null)
                    StartCoroutine(Pop(cards[i]));

                if (i < values.Length)
                    StartCoroutine(CountUp(targets[i], values[i]));

                yield return new WaitForSecondsRealtime(cardDelay);
            }

            yield return new WaitForSecondsRealtime(countSeconds);

            if (rewards.DoubleXp && doubleXpBadge != null)
            {
                StartCoroutine(Pop(doubleXpBadge));
                yield return new WaitForSecondsRealtime(0.3f);
            }

            yield return AnimateXpBar(rewards);

            if (continueButton != null)
            {
                continueButton.interactable = true;
                StartCoroutine(Pop((RectTransform)continueButton.transform));
            }
        }

        private IEnumerator AnimateXpBar(MatchRewardResult rewards)
        {
            for (int level = rewards.LevelBefore; level <= rewards.LevelAfter; level++)
            {
                int needed = PlayerProgress.XpForNextLevel(level);
                int from = level == rewards.LevelBefore ? rewards.XpBefore : 0;
                int to = level == rewards.LevelAfter ? rewards.XpAfter : needed;

                for (float t = 0f; t < barSecondsPerLevel; t += Time.unscaledDeltaTime)
                {
                    float eased = 1f - Mathf.Pow(1f - t / barSecondsPerLevel, 3f);
                    SetLevelDisplay(level, Mathf.RoundToInt(Mathf.Lerp(from, to, eased)));
                    yield return null;
                }

                SetLevelDisplay(level, to);

                if (level < rewards.LevelAfter)
                {
                    // Seviye atlandi: bar basa doner, afis ziplar.
                    if (levelUpBanner != null)
                    {
                        levelUpBanner.gameObject.SetActive(true);
                        StartCoroutine(Pop(levelUpBanner));
                    }

                    yield return new WaitForSecondsRealtime(0.35f);
                }
            }
        }

        private void SetLevelDisplay(int level, int xp)
        {
            int needed = PlayerProgress.XpForNextLevel(level);
            SetText(levelText, $"SEVİYE {level}");
            SetText(xpNumbersText, $"{xp} / {needed}");
            SetText(nextLevelText, (level + 1).ToString());

            if (xpFill != null)
                xpFill.anchorMax = new Vector2(Mathf.Clamp01(xp / (float)Mathf.Max(1, needed)), xpFill.anchorMax.y);
        }

        /// <summary>Test icin: ekrani kapatir ve tekrar gosterilebilir hale getirir.</summary>
        public void HideAndReset()
        {
            MenuButton.Unhandled -= OnUnhandledButton;
            StopAllCoroutines();
            _shown = false;
            if (screenCanvas != null)
                screenCanvas.enabled = false;
        }

        private void OnDestroy() => MenuButton.Unhandled -= OnUnhandledButton;

        private void OnUnhandledButton(MenuButton button)
        {
            if (toast != null && screenCanvas != null && screenCanvas.enabled)
                toast.Show("YAKINDA!");
        }

        private void OnContinuePressed()
        {
            if (continueButton != null)
                continueButton.interactable = false;

            _onContinue?.Invoke();
        }

        private IEnumerator CountUp(TextMeshProUGUI target, int value)
        {
            if (target == null)
                yield break;

            for (float t = 0f; t < countSeconds; t += Time.unscaledDeltaTime)
            {
                target.text = Mathf.RoundToInt(Mathf.Lerp(0f, value, t / countSeconds)).ToString();
                yield return null;
            }

            target.text = value.ToString();
        }

        /// <summary>Sifirdan biraz tasarak tam boyuna "ziplar".</summary>
        private static IEnumerator Pop(RectTransform target)
        {
            const float seconds = 0.35f;
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;

            // Senin ayarladigin olcek etrafinda ziplar.
            Vector3 baseScale = UIBaseScale.Get(target);

            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float x = t / seconds - 1f;
                target.localScale = baseScale * (1f + c3 * x * x * x + c1 * x * x);
                yield return null;
            }

            target.localScale = baseScale;
        }

        private static void SetText(TextMeshProUGUI target, string value)
        {
            if (target != null)
                target.text = value;
        }
    }
}
