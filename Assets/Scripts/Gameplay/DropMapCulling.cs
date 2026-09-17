using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NewBattle.Gameplay
{
    /// <summary>
    /// Inis secim haritasinda sadece arazi gorunsun diye, o kameranin render'i
    /// sirasinda oyuncu / bot / loot gorsellerini gecici olarak kapatir.
    ///
    /// Neden layer kullanmadik: oyuncuyu ayri bir layer'a tasimak, silahlarin
    /// hitLayerMask ayarlarina bagli olarak atisin calismamasina yol acabilir.
    /// Render callback'i ile filtrelemek hicbir fizik ayarina dokunmaz.
    ///
    /// Maliyeti sadece bu kamera aktifken odenir; inis fazi bitince kamera kapanir.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class DropMapCulling : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float refreshInterval = 0.5f;

        [Tooltip("Guvenli alan gorselleri de gizlensin mi. Inis haritasinda acik, " +
                 "minimapta kapali olmali - minimapta alan cemberi gerekli.")]
        [SerializeField] private bool hideSafeZone = true;

        /// <summary>Minimap gibi alan gorsellerini gormesi gereken kameralar icin.</summary>
        public void SetHideSafeZone(bool hide)
        {
            hideSafeZone = hide;
        }

        private Camera _camera;
        private float _nextRefresh;

        private readonly List<Renderer> _targets = new();
        private readonly List<Canvas> _canvasTargets = new();
        private readonly List<Renderer> _suppressed = new();
        private readonly List<Canvas> _suppressedCanvases = new();

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += HandleEndCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= HandleEndCameraRendering;

            Restore();
        }

        private void HandleBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera != _camera)
                return;

            RefreshTargets();

            foreach (Renderer renderer in _targets)
            {
                if (renderer == null || !renderer.enabled)
                    continue;

                renderer.enabled = false;
                _suppressed.Add(renderer);
            }

            foreach (Canvas canvas in _canvasTargets)
            {
                if (canvas == null || !canvas.enabled)
                    continue;

                canvas.enabled = false;
                _suppressedCanvases.Add(canvas);
            }
        }

        private void HandleEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera != _camera)
                return;

            Restore();
        }

        private void Restore()
        {
            foreach (Renderer renderer in _suppressed)
            {
                if (renderer != null)
                    renderer.enabled = true;
            }

            foreach (Canvas canvas in _suppressedCanvases)
            {
                if (canvas != null)
                    canvas.enabled = true;
            }

            _suppressed.Clear();
            _suppressedCanvases.Clear();
        }

        /// <summary>
        /// Gizlenecek nesneleri periyodik olarak topla. Her karede taramak gereksiz;
        /// inis fazinda sahneye yeni nesne nadiren girer.
        /// </summary>
        private void RefreshTargets()
        {
            if (Time.unscaledTime < _nextRefresh)
                return;

            _nextRefresh = Time.unscaledTime + refreshInterval;

            _targets.Clear();
            _canvasTargets.Clear();

            CollectFrom(Object.FindObjectsByType<PlayerLoadout>(FindObjectsSortMode.None));
            CollectFrom(Object.FindObjectsByType<TrainingBot>(FindObjectsSortMode.None));
            CollectFrom(Object.FindObjectsByType<TimedLootPickup>(FindObjectsSortMode.None));

            // Alan cemberi, kirmizi pus ve duvar da gizlenir: oyuncu inecegi yeri
            // secerken haritanin kendisini gormeli, uzerine serilmis kirmizi bir
            // ortuyu degil.
            if (hideSafeZone)
                CollectFrom(Object.FindObjectsByType<SafeZoneController>(FindObjectsSortMode.None));
        }

        private void CollectFrom<T>(T[] sources) where T : Component
        {
            foreach (T source in sources)
            {
                if (source == null)
                    continue;

                _targets.AddRange(source.GetComponentsInChildren<Renderer>(true));
                _canvasTargets.AddRange(source.GetComponentsInChildren<Canvas>(true));
            }
        }
    }
}
