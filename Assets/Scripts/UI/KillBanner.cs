using System.Collections;
using NewBattle.Gameplay;
using NewBattle.Progression;
using TMPro;
using UnityEngine;

namespace NewBattle.UI
{
    /// <summary>
    /// Sen birini oldurunce ekranin ustunde ziplayan afis: "ÖLDÜRDÜN!",
    /// altinda oldurulenin adi ve yukari suzulen "+15 XP". Kisa surede art
    /// arda oldurmelerde "ÇİFTE ÖLDÜRME!", "ÜÇLÜ ÖLDÜRME!", "DURDURULAMAZ!".
    /// </summary>
    public class KillBanner : MonoBehaviour
    {
        [SerializeField] private RectTransform banner;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI victimText;
        [SerializeField] private RectTransform xpRect;
        [SerializeField] private TextMeshProUGUI xpText;

        [Header("Seri")]
        [Tooltip("Bu kadar saniye icinde gelen oldurmeler seri sayilir.")]
        [SerializeField, Min(1f)] private float streakWindow = 8f;
        [SerializeField] private string[] streakTitles =
        {
            "ÖLDÜRDÜN!", "ÇİFTE ÖLDÜRME!", "ÜÇLÜ ÖLDÜRME!", "DURDURULAMAZ!"
        };
        [SerializeField] private Color[] streakColors =
        {
            new(1f, 0.85f, 0.2f, 1f), new(1f, 0.6f, 0.1f, 1f), new(1f, 0.35f, 0.15f, 1f), new(0.95f, 0.25f, 0.85f, 1f)
        };

        [Header("Zamanlama")]
        [SerializeField, Min(0.2f)] private float holdSeconds = 1.6f;
        [SerializeField, Min(0.05f)] private float fadeSeconds = 0.35f;
        [Tooltip("+XP yazisinin yukari suzulme mesafesi.")]
        [SerializeField] private float xpRise = 70f;

        private int _streak;
        private float _lastKillAt = -100f;
        private Coroutine _routine;
        private Vector2 _xpBasePosition;

        private void Awake()
        {
            if (xpRect != null)
                _xpBasePosition = xpRect.anchoredPosition;

            if (group != null)
                group.alpha = 0f;
        }

        private void OnEnable() => KillEvents.Killed += OnKilled;

        private void OnDisable() => KillEvents.Killed -= OnKilled;

        private void OnKilled(KillInfo info)
        {
            if (!info.KillerIsLocal)
                return;

            float now = Time.unscaledTime;
            _streak = now - _lastKillAt <= streakWindow ? _streak + 1 : 1;
            _lastKillAt = now;

            int index = Mathf.Clamp(_streak - 1, 0, streakTitles.Length - 1);
            if (titleText != null)
            {
                titleText.text = streakTitles[index];
                if (streakColors != null && streakColors.Length > 0)
                    titleText.color = streakColors[Mathf.Clamp(index, 0, streakColors.Length - 1)];
            }

            if (victimText != null)
                victimText.text = info.VictimName;

            if (xpText != null)
            {
                ProgressionSettings settings = ProgressionSettings.Instance;
                int xp = settings.xpPerKill * (PlayerProgress.DoubleXpMatches > 0 ? 2 : 1);
                xpText.text = $"+{xp} XP";
            }

            CameraFollow.Shake(0.25f);

            if (_routine != null)
                StopCoroutine(_routine);
            _routine = StartCoroutine(Play());
        }

        private IEnumerator Play()
        {
            Vector3 baseScale = banner != null ? UIBaseScale.Get(banner) : Vector3.one;
            const float popSeconds = 0.28f;
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;

            if (group != null)
                group.alpha = 1f;

            // Zipla (seride her seferinde biraz daha buyuk).
            float extra = 1f + 0.08f * Mathf.Min(_streak - 1, 3);
            for (float t = 0f; t < popSeconds; t += Time.unscaledDeltaTime)
            {
                float x = t / popSeconds - 1f;
                float scale = (1f + c3 * x * x * x + c1 * x * x) * extra;
                if (banner != null)
                    banner.localScale = baseScale * scale;
                MoveXp(t / (holdSeconds + popSeconds));
                yield return null;
            }

            if (banner != null)
                banner.localScale = baseScale * extra;

            for (float t = 0f; t < holdSeconds; t += Time.unscaledDeltaTime)
            {
                MoveXp((t + popSeconds) / (holdSeconds + popSeconds));
                yield return null;
            }

            for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
            {
                if (group != null)
                    group.alpha = 1f - t / fadeSeconds;
                yield return null;
            }

            if (group != null)
                group.alpha = 0f;
            if (banner != null)
                banner.localScale = baseScale;
            _routine = null;
        }

        private void MoveXp(float progress)
        {
            if (xpRect == null)
                return;

            float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(progress), 2f);
            xpRect.anchoredPosition = _xpBasePosition + Vector2.up * (xpRise * eased);
        }
    }
}
