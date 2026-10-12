using System;
using System.Collections;
using System.Collections.Generic;
using NewBattle.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// Menudeki odul penceresi. Iki sekilde acilir:
    ///   - Gunluk giris: 7 gunluk sira, bugunun kutusu parlar, AL ile alinir.
    ///   - Bildirim: ilk oyun hediyesi, seviye odulu gibi tek bir odul.
    ///
    /// Gorunum "Menü > Kur" ile kurulur; yerlesimi Hierarchy'den degistirmek
    /// serbest. Oyun her zaman pencere kapali baslar.
    /// </summary>
    public class RewardPopup : MonoBehaviour
    {
        [Serializable]
        public class DaySlot
        {
            public Button button;
            public Image background;
            public TextMeshProUGUI dayText;
            public TextMeshProUGUI amountText;
            public GameObject check;
        }

        [Header("Pencere")]
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform window;
        [SerializeField, Min(0.01f)] private float openSeconds = 0.25f;

        [Header("Icerik")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI bodyText;
        [SerializeField] private Image icon;

        [Header("Ikonlar")]
        [SerializeField] private Sprite giftIcon;
        [SerializeField] private Sprite coinIcon;
        [SerializeField] private Sprite xpIcon;
        [SerializeField] private Sprite levelUpIcon;
        [SerializeField] private Sprite animationIcon;

        [Header("Gunluk Giris")]
        [SerializeField] private GameObject daysRoot;
        [SerializeField] private DaySlot[] days;
        [SerializeField] private string dailyTitle = "GÜNLÜK ÖDÜL";
        [SerializeField] private string dailyBodyFormat = "Bugün: +{0} XP";
        [SerializeField] private Color claimedColor = new(0.35f, 0.32f, 0.45f, 1f);
        [SerializeField] private Color todayColor = new(1f, 0.78f, 0.1f, 1f);
        [SerializeField] private Color futureColor = new(0.29f, 0.20f, 0.47f, 1f);

        [Header("Buton")]
        [SerializeField] private Button claimButton;
        [SerializeField] private TextMeshProUGUI claimLabel;
        [SerializeField] private string claimText = "AL";
        [SerializeField] private string okText = "TAMAM";

        public bool IsOpen { get; private set; }

        private Action _onClosed;
        private int _todayIndex = -1;
        private Coroutine _animation;
        private bool _wired;

        private void Awake()
        {
            Wire();

            // Duzenlerken gorunur birakilmis olabilir; oyun kapali baslar.
            if (!IsOpen)
            {
                if (group != null)
                {
                    group.alpha = 0f;
                    group.blocksRaycasts = false;
                }

                gameObject.SetActive(false);
            }
        }

        private void Wire()
        {
            if (_wired)
                return;

            _wired = true;

            if (claimButton != null)
                claimButton.onClick.AddListener(OnClaimPressed);

            if (days == null)
                return;

            for (int i = 0; i < days.Length; i++)
            {
                int index = i;
                if (days[i]?.button != null)
                    days[i].button.onClick.AddListener(() => OnDayPressed(index));
            }
        }

        /// <summary>Gunluk giris penceresi. onClaimed, AL'a basilip pencere kapaninca cagrilir.</summary>
        public void ShowDaily(int todayIndex, IReadOnlyList<int> amounts, Action onClaimed)
        {
            _todayIndex = todayIndex;
            int todayXp = amounts != null && todayIndex < amounts.Count ? amounts[todayIndex] : 0;

            SetText(titleText, dailyTitle);
            SetText(bodyText, string.Format(dailyBodyFormat, todayXp));
            SetText(claimLabel, claimText);

            if (icon != null)
                icon.gameObject.SetActive(false);

            if (daysRoot != null)
                daysRoot.SetActive(true);

            PaintDays(todayIndex, amounts);
            Open(onClaimed);
        }

        /// <summary>Tek bir odul bildirimi. onClosed, TAMAM'a basilip pencere kapaninca cagrilir.</summary>
        public void ShowNotice(RewardNotice notice, Action onClosed)
        {
            _todayIndex = -1;

            SetText(titleText, notice.title);
            SetText(bodyText, notice.body);
            SetText(claimLabel, notice.icon == RewardNoticeIcon.Gift ? claimText : okText);

            if (daysRoot != null)
                daysRoot.SetActive(false);

            if (icon != null)
            {
                Sprite sprite = IconFor(notice.icon);
                if (sprite != null)
                    icon.sprite = sprite;

                icon.gameObject.SetActive(true);
            }

            Open(onClosed);
        }

        private void PaintDays(int todayIndex, IReadOnlyList<int> amounts)
        {
            if (days == null)
                return;

            for (int i = 0; i < days.Length; i++)
            {
                DaySlot slot = days[i];
                if (slot == null)
                    continue;

                bool exists = amounts != null && i < amounts.Count;
                if (slot.button != null)
                    slot.button.gameObject.SetActive(exists);

                if (!exists)
                    continue;

                bool claimed = i < todayIndex;
                bool today = i == todayIndex;

                if (slot.background != null)
                    slot.background.color = claimed ? claimedColor : today ? todayColor : futureColor;

                SetText(slot.dayText, $"{i + 1}. GÜN");
                SetText(slot.amountText, $"+{amounts[i]}");

                if (slot.check != null)
                    slot.check.SetActive(claimed);
            }
        }

        private Sprite IconFor(RewardNoticeIcon type) => type switch
        {
            RewardNoticeIcon.Coins => coinIcon,
            RewardNoticeIcon.Xp => xpIcon,
            RewardNoticeIcon.LevelUp => levelUpIcon,
            RewardNoticeIcon.Animation => animationIcon,
            _ => giftIcon
        };

        private void OnDayPressed(int index)
        {
            if (index == _todayIndex)
                OnClaimPressed();
        }

        private void OnClaimPressed()
        {
            if (!IsOpen)
                return;

            IsOpen = false;
            Animate(false);
        }

        private void Open(Action onClosed)
        {
            Wire();
            _onClosed = onClosed;

            // Once "acik" isaretle: ilk acilista Awake bu SetActive sirasinda calisir.
            IsOpen = true;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Animate(true);
        }

        private void Animate(bool opening)
        {
            if (_animation != null)
                StopCoroutine(_animation);

            _animation = StartCoroutine(AnimateRoutine(opening));
        }

        private IEnumerator AnimateRoutine(bool opening)
        {
            float duration = opening ? openSeconds : openSeconds * 0.7f;

            if (group != null)
            {
                group.blocksRaycasts = opening;
                group.interactable = opening;
            }

            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                Apply(opening ? t / duration : 1f - t / duration, opening);
                yield return null;
            }

            Apply(opening ? 1f : 0f, opening);
            _animation = null;

            if (opening)
                yield break;

            Action closed = _onClosed;
            _onClosed = null;

            // Geri cagri yeni bir pencere acabilir; o zaman kapatma.
            closed?.Invoke();
            if (!IsOpen)
                gameObject.SetActive(false);
        }

        private void Apply(float progress, bool opening)
        {
            if (group != null)
                group.alpha = progress;

            if (window == null)
                return;

            // Acilirken ziplar, kapanirken duz kuculur; senin verdigin olcek temel alinir.
            float scale = opening ? Mathf.LerpUnclamped(0.6f, 1f, EaseOutBack(progress)) : Mathf.Lerp(0.85f, 1f, progress);
            window.localScale = UIBaseScale.Get(window) * scale;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        private static void SetText(TextMeshProUGUI target, string value)
        {
            if (target != null)
                target.text = value;
        }
    }
}
