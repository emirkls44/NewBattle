using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// Menudeki karakteri ayri bir kamerayla RenderTexture'a cizip bu
    /// RawImage'da gosterir. Parmakla saga sola surukleyince karakter doner.
    ///
    /// Karakter dunyada uzak bir noktada durur (MenuKarakterSahnesi); oyun
    /// kamerasi oraya hic bakmaz. Bu component kapaninca (menu gizlenince)
    /// kamera da kapanir, yani macta hicbir maliyeti yok.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class MenuCharacterStage : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        [Header("Sahne Referanslari")]
        [SerializeField] private Camera stageCamera;
        [Tooltip("Karakterin bagli oldugu donen nesne.")]
        [SerializeField] private Transform turntable;
        [Tooltip("Menu gorunurken acilir, gizlenince kapanir (macta maliyeti olmasin).")]
        [SerializeField] private LobbyCharacterAnimator lobbyAnimator;

        [Header("Goruntu")]
        [Tooltip("RenderTexture'in uzun kenari (piksel).")]
        [SerializeField, Range(256, 2048)] private int resolution = 1024;
        [Tooltip("Karakterin alt ve ust kenarinda birakilacak bosluk (yukseklik orani).")]
        [SerializeField, Range(0f, 0.3f)] private float framePadding = 0.06f;
        [Tooltip("Kamera karaktere hafif yukaridan baksin (derece).")]
        [SerializeField, Range(-10f, 30f)] private float cameraPitch = 6f;

        [Header("Dondurme")]
        [SerializeField] private float dragDegreesPerPixel = 0.45f;
        [Tooltip("Birakinca karakter bu hizla ilk yonune doner (0 = donmez).")]
        [SerializeField, Min(0f)] private float returnSpeed = 2.5f;
        [SerializeField, Min(0f)] private float returnDelay = 1.5f;

        private RawImage _image;
        private RenderTexture _texture;
        private Quaternion _restRotation;
        private bool _dragging;
        private float _releasedAt;

        private void Awake()
        {
            _image = GetComponent<RawImage>();

            if (turntable != null)
                _restRotation = turntable.localRotation;
        }

        public LobbyCharacterAnimator LobbyAnimator => lobbyAnimator;

        private void OnEnable()
        {
            if (lobbyAnimator != null)
                lobbyAnimator.enabled = true;
        }

        private void OnDisable()
        {
            if (lobbyAnimator != null)
                lobbyAnimator.enabled = false;

            if (stageCamera != null)
            {
                stageCamera.enabled = false;
                stageCamera.targetTexture = null;
            }

            if (_image != null)
                _image.texture = null;

            ReleaseTexture();
        }

        private void LateUpdate()
        {
            // RenderTexture, RawImage'in boyutuna gore kurulur. Ilk karede
            // canvas olcusu henuz hesaplanmamis olabilir, ekran donebilir;
            // bu yuzden her karede kontrol edip gerekirse yeniden kurulur.
            EnsureTexture();
        }

        private void EnsureTexture()
        {
            if (stageCamera == null)
                return;

            Rect rect = ((RectTransform)transform).rect;
            if (rect.width < 2f || rect.height < 2f)
                return;

            float aspect = rect.width / rect.height;
            if (_texture != null && Mathf.Abs(_texture.width / (float)_texture.height - aspect) < 0.01f)
                return;

            ReleaseTexture();

            int height = aspect <= 1f ? resolution : Mathf.RoundToInt(resolution / aspect);
            int width = Mathf.RoundToInt(height * aspect);

            _texture = new RenderTexture(Mathf.Max(64, width), Mathf.Max(64, height), 24, RenderTextureFormat.ARGB32)
            {
                name = "MenuCharacterStage",
                antiAliasing = 4
            };
            _texture.Create();

            stageCamera.clearFlags = CameraClearFlags.SolidColor;
            stageCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            stageCamera.targetTexture = _texture;
            stageCamera.aspect = _texture.width / (float)_texture.height;
            stageCamera.enabled = true;

            _image.texture = _texture;
            _image.color = Color.white;

            FrameCharacter();
        }

        private void ReleaseTexture()
        {
            if (_texture == null)
                return;

            if (stageCamera != null)
                stageCamera.targetTexture = null;

            _texture.Release();
            Destroy(_texture);
            _texture = null;
        }

        private void Update()
        {
            if (turntable == null || _dragging || returnSpeed <= 0f)
                return;

            if (Time.unscaledTime - _releasedAt < returnDelay)
                return;

            turntable.localRotation = Quaternion.Slerp(
                turntable.localRotation, _restRotation, 1f - Mathf.Exp(-returnSpeed * Time.unscaledDeltaTime));
        }

        public void OnBeginDrag(PointerEventData eventData) => _dragging = true;

        public void OnDrag(PointerEventData eventData)
        {
            if (turntable != null)
                turntable.Rotate(0f, -eventData.delta.x * dragDegreesPerPixel, 0f, Space.Self);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _dragging = false;
            _releasedAt = Time.unscaledTime;
        }

        /// <summary>
        /// Kamerayi karakterin boyuna gore yerlestirir. Model degisse ya da
        /// olcegi farkli olsa bile karakter cerceveye tam oturur.
        /// </summary>
        [ContextMenu("Karakteri Cercevele")]
        public void FrameCharacter()
        {
            if (stageCamera == null || turntable == null)
                return;

            Renderer[] renderers = turntable.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            float height = bounds.size.y * (1f + framePadding * 2f);
            float halfFov = stageCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float distance = height * 0.5f / Mathf.Tan(halfFov) + bounds.extents.z;

            Vector3 forward = turntable.parent != null ? turntable.parent.forward : Vector3.forward;
            Quaternion look = Quaternion.LookRotation(-forward) * Quaternion.Euler(cameraPitch, 0f, 0f);

            stageCamera.transform.SetPositionAndRotation(bounds.center - look * Vector3.forward * distance, look);
            stageCamera.nearClipPlane = Mathf.Max(0.01f, distance * 0.1f);
            stageCamera.farClipPlane = distance * 4f;
        }
    }
}
