using System.Collections;
using System.Collections.Generic;
using NewBattle.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// Birini oldurunce can barinin ustunde ziplayan kuru kafa rozeti; etrafindaki
    /// halka dolar. Seri oldurmede (kisa surede art arda):
    ///   1 oldurme: ortada tek rozet
    ///   2 oldurme: iki rozet yan yana
    ///   3 oldurme: ikisinin ortasina, biraz buyuk ve onde ucuncusu yapisir
    /// Bir sure oldurme olmazsa rozetler solup kaybolur.
    ///
    /// Rozet sablonu "RozetSablonu": gorunumunu Hierarchy'den duzenle.
    /// </summary>
    public class KillBadges : MonoBehaviour
    {
        [SerializeField] private RectTransform container;
        [SerializeField] private CanvasGroup group;
        [Tooltip("Her oldurme icin cogaltilan rozet (Halka adli cocugu dolar).")]
        [SerializeField] private RectTransform badgeTemplate;

        [Header("Yerlesim")]
        [Tooltip("Iki rozet yan yanayken merkezler arasi mesafe.")]
        [SerializeField] private float spacing = 118f;
        [Tooltip("Ucuncu rozet: ortada, bu kadar yukarida ve bu kadar buyuk.")]
        [SerializeField] private float thirdLift = 14f;
        [SerializeField] private float thirdScale = 1.18f;

        [Header("Zamanlama")]
        [Tooltip("Bu kadar saniye icinde gelen oldurmeler seri sayilir.")]
        [SerializeField, Min(1f)] private float streakWindow = 6f;
        [Tooltip("Son oldurmeden sonra rozetler bu kadar ekranda kalir (seri suresiyle ayni olmali; " +
                 "yoksa rozetler kaybolduktan sonra gelen oldurme seriyi bozar).")]
        [SerializeField, Min(0.5f)] private float holdSeconds = 6f;
        [SerializeField, Min(0.05f)] private float ringFillSeconds = 0.45f;

        private readonly List<RectTransform> _badges = new();
        private int _streak;
        private float _lastKillAt = -100f;
        private Coroutine _fade;

        private void Awake()
        {
            if (badgeTemplate != null)
                badgeTemplate.gameObject.SetActive(false);
            if (group != null)
                group.alpha = 0f;
        }

        private void OnEnable() => KillEvents.Killed += OnKilled;

        private void OnDisable() => KillEvents.Killed -= OnKilled;

        private void Update()
        {
            // Yerlerine yumusakca kaysinlar (ikinci gelince birinci sola kayar).
            for (int i = 0; i < _badges.Count; i++)
            {
                RectTransform badge = _badges[i];
                if (badge == null)
                    continue;

                (Vector2 target, _) = Slot(i, _badges.Count);
                badge.anchoredPosition = Vector2.Lerp(badge.anchoredPosition, target, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            }
        }

        private void OnKilled(KillInfo info)
        {
            if (!info.KillerIsLocal || badgeTemplate == null || container == null)
                return;

            float now = Time.unscaledTime;
            bool continuing = now - _lastKillAt <= streakWindow && _badges.Count > 0;
            _streak = continuing ? _streak + 1 : 1;
            _lastKillAt = now;

            if (!continuing)
                Clear();

            if (_fade != null)
                StopCoroutine(_fade);
            if (group != null)
                group.alpha = 1f;

            if (_badges.Count < 3)
            {
                RectTransform badge = Instantiate(badgeTemplate, container);
                badge.gameObject.SetActive(true);
                badge.name = $"Rozet{_badges.Count + 1}";
                _badges.Add(badge);

                (Vector2 position, float scale) = Slot(_badges.Count - 1, _badges.Count);
                // Ilk iki rozet ortadan, ucuncu ortada yerinde dogar.
                badge.anchoredPosition = _badges.Count == 3 ? position : Vector2.zero;
                StartCoroutine(PopIn(badge, scale));
            }
            else
            {
                // 4. ve sonrasi: ortadaki rozet yeniden ziplar.
                StartCoroutine(PopIn(_badges[2], thirdScale));
            }

            _fade = StartCoroutine(FadeAfterHold());
        }

        /// <summary>i. rozetin yeri ve olcegi (toplam count rozet varken).</summary>
        private (Vector2 position, float scale) Slot(int index, int count)
        {
            if (count <= 1)
                return (Vector2.zero, 1f);

            return index switch
            {
                0 => (new Vector2(-spacing * 0.5f, 0f), 1f),
                1 => (new Vector2(spacing * 0.5f, 0f), 1f),
                _ => (new Vector2(0f, thirdLift), thirdScale)
            };
        }

        private IEnumerator PopIn(RectTransform badge, float scale)
        {
            Vector3 baseScale = UIBaseScale.Get(badgeTemplate) * scale;
            badge.SetAsLastSibling(); // yenisi onde (ucuncu ikisinin ustune yapisir)

            Image ring = badge.Find("Halka")?.GetComponent<Image>();
            if (ring != null)
                ring.fillAmount = 0f;

            const float popSeconds = 0.32f;
            const float c1 = 1.9f;
            const float c3 = c1 + 1f;

            float total = Mathf.Max(popSeconds, ringFillSeconds);
            for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
            {
                if (badge == null)
                    yield break;

                float p = Mathf.Clamp01(t / popSeconds);
                float x = p - 1f;
                badge.localScale = baseScale * (1f + c3 * x * x * x + c1 * x * x);

                if (ring != null)
                {
                    float r = Mathf.Clamp01(t / ringFillSeconds);
                    ring.fillAmount = 1f - (1f - r) * (1f - r);
                }

                yield return null;
            }

            if (badge == null)
                yield break;

            badge.localScale = baseScale;
            if (ring != null)
                ring.fillAmount = 1f;
        }

        private IEnumerator FadeAfterHold()
        {
            yield return new WaitForSecondsRealtime(holdSeconds);

            for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime)
            {
                if (group != null)
                    group.alpha = 1f - t / 0.4f;
                yield return null;
            }

            if (group != null)
                group.alpha = 0f;
            Clear();
            _fade = null;
        }

        private void Clear()
        {
            foreach (RectTransform badge in _badges)
            {
                if (badge != null)
                    Destroy(badge.gameObject);
            }

            _badges.Clear();
        }
    }
}
