using NewBattle.Gameplay;
using NewBattle.Progression;
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
        [Tooltip("1080 piksel yukseklikte bir piksel suruklemenin dondurdugu derece " +
                 "(baska cozunurluklerde orantili; telefonda da ayni his).")]
        [SerializeField] private float dragDegreesPerPixel = 0.45f;
        [Tooltip("Donus parmagi ne kadar siki takip etsin (buyuk = daha dogrudan).")]
        [SerializeField, Min(1f)] private float followSharpness = 16f;
        [Tooltip("Birakinca savrulma ne kadar cabuk dursun (kucuk = daha kaygan, uzun doner).")]
        [SerializeField, Min(0.1f)] private float spinDamping = 2.2f;
        [SerializeField, Min(0f)] private float maxSpinSpeed = 900f;
        [Tooltip("Birakinca karakter bu hizla ilk yonune doner (0 = donmez).")]
        [SerializeField, Min(0f)] private float returnSpeed = 2.5f;
        [SerializeField, Min(0f)] private float returnDelay = 1.5f;

        private float _menuZoom = 1f;
        private CharacterDefinition _shownCharacter;
        private Vector2 _menuOffset;
        private RawImage _image;
        private RenderTexture _texture;
        private Quaternion _restRotation;
        private bool _dragging;
        private float _releasedAt;
        private float _pendingYaw;
        private float _spinVelocity;

        private void Awake()
        {
            _image = GetComponent<RawImage>();

            // Karakterin yuzu sahnenin gunesine bagli kalmasin: on taraftan aydinlat.
            if (stageCamera != null && stageCamera.GetComponent<ToonPreviewLight>() == null)
                stageCamera.gameObject.AddComponent<ToonPreviewLight>();

            if (turntable != null)
                _restRotation = turntable.localRotation;
        }

        public LobbyCharacterAnimator LobbyAnimator => lobbyAnimator;
        public Camera StageCamera => stageCamera;
        public Transform Turntable => turntable;

        private void OnEnable()
        {
            CharacterLibrary.CharacterSelectionChanged += ShowSelectedCharacter;
            ShowSelectedCharacter();

            if (lobbyAnimator != null)
                lobbyAnimator.enabled = true;
        }

        private void OnDisable()
        {
            CharacterLibrary.CharacterSelectionChanged -= ShowSelectedCharacter;

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

        /// <summary>Oyuncunun secili karakterini gosterir.</summary>
        public void ShowSelectedCharacter()
        {
            CharacterLibrary library = CharacterLibrary.Instance;
            if (library != null)
                ShowCharacter(library.Current);
        }

        /// <summary>
        /// Menudeki modeli verilen karakterle degistirir (karakter ekraninda
        /// onizleme). Lobi animasyonu yeni iskelete yeniden baglanir, kamera
        /// yeni boya gore cercevelenir.
        /// </summary>
        public void ShowCharacter(CharacterDefinition character)
        {
            CharacterLibrary library = CharacterLibrary.Instance;
            Animator animator = turntable != null ? turntable.GetComponentInChildren<Animator>(true) : null;
            if (library == null || character == null || animator == null)
                return;

            bool restart = lobbyAnimator != null && lobbyAnimator.isActiveAndEnabled;
            if (restart)
                lobbyAnimator.enabled = false;

            bool changed = CharacterModelSwap.Apply(animator, character, library.Default);

            // Karaktere ozel menu yakinligi (CharacterLibrary > Menu Zoom).
            _shownCharacter = character;
            Vector2 offset = new(character.menuOffsetX, character.menuOffsetY);
            if (!Mathf.Approximately(_menuZoom, character.menuZoom) || _menuOffset != offset)
            {
                _menuZoom = character.menuZoom;
                _menuOffset = offset;
                changed = true;
            }

            if (restart)
                lobbyAnimator.enabled = true;

            if (changed)
                FrameCharacter();
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
            if (turntable == null)
                return;

            // Menu Zoom / Offset Play'de Inspector'dan degisirse kadraj aninda yenilensin.
            if (_shownCharacter != null &&
                (!Mathf.Approximately(_menuZoom, _shownCharacter.menuZoom) ||
                 !Mathf.Approximately(_menuOffset.x, _shownCharacter.menuOffsetX) ||
                 !Mathf.Approximately(_menuOffset.y, _shownCharacter.menuOffsetY)))
            {
                _menuZoom = _shownCharacter.menuZoom;
                _menuOffset = new Vector2(_shownCharacter.menuOffsetX, _shownCharacter.menuOffsetY);
                FrameCharacter();
            }

            float dt = Time.unscaledDeltaTime;

            // Parmagi yumusakca takip et (titreme yerine akici donus).
            float step = _pendingYaw * (1f - Mathf.Exp(-followSharpness * dt));
            _pendingYaw -= step;

            if (_dragging)
            {
                // Parmak duruyorsa birakinca savrulmasin.
                _spinVelocity *= Mathf.Exp(-12f * dt);
            }
            else
            {
                // Birakildi: firlatildigi hizla donup yavasca dursun.
                step += _spinVelocity * dt;
                _spinVelocity *= Mathf.Exp(-spinDamping * dt);
                if (Mathf.Abs(_spinVelocity) < 4f)
                    _spinVelocity = 0f;
                else
                    _releasedAt = Time.unscaledTime; // donerken bekleme suresi baslamasin
            }

            if (Mathf.Abs(step) > 0.0001f)
                turntable.Rotate(0f, step, 0f, Space.Self);

            bool settled = !_dragging && _spinVelocity == 0f && Mathf.Abs(_pendingYaw) < 0.05f;
            if (!settled || returnSpeed <= 0f || Time.unscaledTime - _releasedAt < returnDelay)
                return;

            turntable.localRotation = Quaternion.Slerp(
                turntable.localRotation, _restRotation, 1f - Mathf.Exp(-returnSpeed * dt));
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragging = true;
            _spinVelocity = 0f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            float pixelScale = 1080f / Mathf.Max(1f, Screen.height);
            float degrees = -eventData.delta.x * dragDegreesPerPixel * pixelScale;
            _pendingYaw += degrees;

            float dt = Mathf.Max(0.001f, Time.unscaledDeltaTime);
            float sample = Mathf.Clamp(degrees / dt, -maxSpinSpeed, maxSpinSpeed);
            _spinVelocity = Mathf.Lerp(_spinVelocity, sample, 0.5f);
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

            // Karaktere ozel yakinlik: ayaklar yerinde kalir, kadraj tepeden daralir.
            float zoom = Mathf.Max(0.1f, _menuZoom);
            if (!Mathf.Approximately(zoom, 1f))
            {
                float zoomedHeight = bounds.size.y / zoom;
                bounds = new Bounds(
                    new Vector3(bounds.center.x, bounds.min.y + zoomedHeight * 0.5f, bounds.center.z),
                    new Vector3(bounds.size.x, zoomedHeight, bounds.size.z));
            }

            float height = bounds.size.y * (1f + framePadding * 2f);
            float halfFov = stageCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float distance = height * 0.5f / Mathf.Tan(halfFov) + bounds.extents.z;

            Vector3 forward = turntable.parent != null ? turntable.parent.forward : Vector3.forward;
            Quaternion look = Quaternion.LookRotation(-forward) * Quaternion.Euler(cameraPitch, 0f, 0f);

            // Karaktere ozel kaydirma: kamera ters yone kayar (karakter goruntude oteye gider).
            float visibleHeight = 2f * (distance - bounds.extents.z) * Mathf.Tan(halfFov);
            float visibleWidth = visibleHeight * Mathf.Max(0.1f, stageCamera.aspect);
            Vector3 shift = look * new Vector3(-_menuOffset.x * visibleWidth, -_menuOffset.y * visibleHeight, 0f);

            stageCamera.transform.SetPositionAndRotation(bounds.center + shift - look * Vector3.forward * distance, look);
            stageCamera.nearClipPlane = Mathf.Max(0.01f, distance * 0.1f);
            stageCamera.farClipPlane = distance * 4f;
        }
    }
}
