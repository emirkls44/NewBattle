using System.Collections.Generic;
using NewBattle.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// Inis sirasinda ekran efektleri: kenarlarin kararmasi (vinyet) ve
    /// serbest dususte akan hiz cizgileri. Sadece kendi oyuncumuzun inisine
    /// bakar.
    ///
    /// Serbest dususte oyun arayuzunu (joystick, can bari, minimap...) gizler,
    /// parasut acilinca geri getirir. Arayuz parcalarinin kendi kodlari
    /// calismaya devam etsin diye nesneleri kapatmaz ve saydamliklarina
    /// dokunmaz; her parcayi kendi alt tuvaline alip o tuvali kapatir.
    /// </summary>
    public class DescentOverlay : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Image vignette;
        [SerializeField] private RectTransform[] streaks;

        [Header("Ayarlar")]
        [SerializeField, Range(0f, 1f)] private float freefallVignette = 0.8f;
        [SerializeField, Range(0f, 1f)] private float parachuteVignette = 0.3f;
        [SerializeField, Range(0f, 1f)] private float streakAlpha = 0.55f;
        [Tooltip("Hiz cizgilerinin ekranda akma hizi.")]
        [SerializeField] private float streakSpeed = 2600f;
        [Tooltip("Hiz cizgilerinin akis yonu (asagi dusuyoruz: cizgiler yukari akar).")]
        [SerializeField] private Vector2 streakDirection = new(0.55f, 1f);

        [Header("Arayuz")]
        [Tooltip("Serbest dususte oyun arayuzunu gizle (Battlelands'teki gibi).")]
        [SerializeField] private bool hideHudDuringFreefall = true;

        private readonly List<Canvas> _hudCanvases = new();
        private bool _hudHidden;
        private float _nextHudScan;

        private Transform _cachedTarget;
        private ParachuteDescent _descent;
        private float _vignetteAlpha;
        private float _streakAlpha;

        private void Awake()
        {
            if (group != null)
                group.alpha = 0f;

            if (streaks != null)
            {
                foreach (RectTransform streak in streaks)
                {
                    if (streak != null)
                        ScatterStreak(streak, randomY: true);
                }
            }
        }

        private void Update()
        {
            ResolveDescent();

            float targetVignette = 0f;
            float targetStreaks = 0f;
            bool freefall = false;

            if (_descent != null && _descent.Object != null && _descent.Object.IsValid)
            {
                switch (_descent.Phase)
                {
                    case ParachuteDescent.DescentPhase.Freefall:
                        freefall = true;
                        targetVignette = freefallVignette;
                        targetStreaks = streakAlpha * (1f - Mathf.InverseLerp(0.7f, 1f, _descent.FreefallProgress));
                        break;
                    case ParachuteDescent.DescentPhase.Parachute:
                        targetVignette = parachuteVignette;
                        break;
                }
            }

            SetHudHidden(hideHudDuringFreefall && freefall);

            float blend = 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime);
            _vignetteAlpha = Mathf.Lerp(_vignetteAlpha, targetVignette, blend);
            _streakAlpha = Mathf.Lerp(_streakAlpha, targetStreaks, blend);

            bool visible = _vignetteAlpha > 0.005f || _streakAlpha > 0.005f;
            if (group != null)
                group.alpha = visible ? 1f : 0f;

            if (!visible)
                return;

            if (vignette != null)
                vignette.color = new Color(1f, 1f, 1f, _vignetteAlpha);

            MoveStreaks();
        }

        private void MoveStreaks()
        {
            if (streaks == null)
                return;

            var parent = (RectTransform)transform;
            Vector2 direction = streakDirection.normalized;
            float limit = parent.rect.height * 0.75f;

            foreach (RectTransform streak in streaks)
            {
                if (streak == null)
                    continue;

                streak.anchoredPosition += direction * (streakSpeed * Time.unscaledDeltaTime);
                if (streak.TryGetComponent(out Image image))
                    image.color = new Color(1f, 1f, 1f, _streakAlpha);

                if (streak.anchoredPosition.y > limit)
                    ScatterStreak(streak, randomY: false);
            }
        }

        private void ScatterStreak(RectTransform streak, bool randomY)
        {
            var parent = (RectTransform)transform;
            float width = Mathf.Max(1000f, parent.rect.width);
            float height = Mathf.Max(600f, parent.rect.height);
            Vector2 direction = streakDirection.normalized;

            float y = randomY ? Random.Range(-height * 0.75f, height * 0.75f) : -height * 0.75f;
            streak.anchoredPosition = new Vector2(Random.Range(-width * 0.6f, width * 0.6f), y);
            streak.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        private void OnDisable() => SetHudHidden(false);

        private void SetHudHidden(bool hidden)
        {
            if (hidden && Time.unscaledTime >= _nextHudScan)
            {
                // Minimap gibi bazi parcalar calisma aninda olusuyor; dusus
                // boyunca ara ara yeniden taranir.
                _nextHudScan = Time.unscaledTime + 1f;
                ScanHud();
            }

            if (hidden == _hudHidden)
                return;

            _hudHidden = hidden;
            foreach (Canvas canvas in _hudCanvases)
            {
                if (canvas != null)
                    canvas.enabled = !hidden;
            }
        }

        private void ScanHud()
        {
            Transform root = transform.parent;
            if (root == null)
                return;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child == transform || !(child is RectTransform))
                    continue;

                Canvas canvas = child.GetComponent<Canvas>();
                if (canvas != null && _hudCanvases.Contains(canvas))
                {
                    canvas.enabled = !_hudHidden;
                    continue;
                }

                // Kendi tuvali olan parcalar (harita secimi, bu efekt) ve menu /
                // mac sonu ekranlari oyun arayuzu degil.
                if (canvas != null || child.GetComponent<MainMenuScreen>() != null ||
                    child.GetComponentInChildren<MatchResultHUD>(true) != null)
                    continue;

                canvas = child.gameObject.AddComponent<Canvas>();
                if (child.GetComponent<GraphicRaycaster>() == null)
                    child.gameObject.AddComponent<GraphicRaycaster>(); // joystick dokunuslari icin sart
                canvas.enabled = !_hudHidden;
                _hudCanvases.Add(canvas);
            }
        }

        private void ResolveDescent()
        {
            CameraFollow follow = CameraFollow.Active;
            Transform target = follow != null ? follow.target : null;
            if (target == _cachedTarget)
                return;

            _cachedTarget = target;
            _descent = target != null ? target.GetComponent<ParachuteDescent>() : null;
        }
    }
}
