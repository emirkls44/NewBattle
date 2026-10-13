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

        [Header("Sandik (karakterin altindaki zemin)")]
        [Tooltip("Karakter ahsap bir sandigin ustunde durur (havada durmasin).")]
        [SerializeField] private bool showCrate = true;
        [Tooltip("Bos birakilirsa SoftToon ile olusturulur. Kur, ToonLoot materyalini baglar.")]
        [SerializeField] private Material crateMaterial;
        [Tooltip("Sandigin genisligi (karakter boyuna oran).")]
        [SerializeField, Range(0.3f, 1.4f)] private float crateWidth = 0.62f;
        [Tooltip("Sandigin yuksekligi (karakter boyuna oran).")]
        [SerializeField, Range(0.1f, 0.9f)] private float crateHeight = 0.3f;
        [Tooltip("Sandik hafif capraz dursun ki iki yuzu de gorunsun (derece).")]
        [SerializeField, Range(-45f, 45f)] private float crateYaw = 28f;
        [SerializeField] private Color plankLight = new(0.83f, 0.58f, 0.32f, 1f);
        [SerializeField] private Color plankDark = new(0.74f, 0.49f, 0.26f, 1f);
        [SerializeField] private Color frameColor = new(0.55f, 0.33f, 0.16f, 1f);
        [SerializeField] private Color gapColor = new(0.29f, 0.17f, 0.08f, 1f);
        [Tooltip("Ayaklarin altindaki yumusak golgenin koyulugu.")]
        [SerializeField, Range(0f, 1f)] private float footShadowAlpha = 0.38f;

        private Transform _crate;
        private MeshRenderer _crateRenderer;
        private MeshRenderer _footShadow;
        private Mesh _crateMesh;

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
        /// Karakterin altina ahsap sandik koyar: ust yuzu ayak hizasinda, boyu
        /// karakterle orantili. Sandik donmez; karakter ustunde doner.
        /// </summary>
        private bool PlaceCrate(float characterHeight, float feetY)
        {
            // Editorde (sag tik > Karakteri Cercevele) sahneye kalici sandik kaydedilmesin.
            if (!Application.isPlaying)
                return false;

            if (!showCrate || turntable == null || characterHeight <= 0.01f)
            {
                if (_crate != null)
                    _crate.gameObject.SetActive(false);
                return false;
            }

            Transform stage = turntable.parent != null ? turntable.parent : turntable;
            int layer = turntable.gameObject.layer;

            if (_crate == null)
            {
                _crateMesh = NewBattle.Visual.WoodenCrateMesh.Build(new NewBattle.Visual.WoodenCrateMesh.Palette
                {
                    PlankLight = plankLight,
                    PlankDark = plankDark,
                    Frame = frameColor,
                    Gap = gapColor
                });

                var crateObject = new GameObject("AhsapSandik") { layer = layer };
                crateObject.transform.SetParent(stage, false);
                crateObject.AddComponent<MeshFilter>().sharedMesh = _crateMesh;
                _crateRenderer = crateObject.AddComponent<MeshRenderer>();
                _crateRenderer.sharedMaterial = crateMaterial != null ? crateMaterial : CreateFallbackMaterial();
                _crateRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _crate = crateObject.transform;

                _footShadow = NewBattle.Gameplay.OverlayMeshLibrary.CreateOverlayObject(
                    "SandikGolgesi", NewBattle.Gameplay.OverlayMeshLibrary.SoftDisc, stage, Vector3.zero, 1f);
                _footShadow.gameObject.layer = layer;
                var block = new MaterialPropertyBlock();
                block.SetColor(NewBattle.Gameplay.OverlayMeshLibrary.BaseColorId, new Color(0.12f, 0.06f, 0.02f, footShadowAlpha));
                block.SetFloat(NewBattle.Gameplay.OverlayMeshLibrary.FillId, 1f);
                _footShadow.SetPropertyBlock(block);
            }

            _crate.gameObject.SetActive(true);

            // Sandigin ust yuzu = karakterin en alt noktasi (ayakkabilar).
            Vector3 feet = new(turntable.position.x, feetY, turntable.position.z);
            float width = crateWidth * characterHeight;
            float height = crateHeight * characterHeight;
            _crate.SetPositionAndRotation(feet, stage.rotation * Quaternion.Euler(0f, crateYaw, 0f));
            _crate.localScale = new Vector3(width, height, width);

            if (_footShadow != null)
            {
                _footShadow.transform.SetPositionAndRotation(feet + Vector3.up * 0.004f, stage.rotation);
                _footShadow.transform.localScale = Vector3.one * (width * 0.42f);
            }

            return true;
        }

        private void OnDestroy()
        {
            if (_crateMesh != null)
                Destroy(_crateMesh);
        }

        private static Material CreateFallbackMaterial()
        {
            Shader shader = Shader.Find("NewBattle/SoftToon");
            var material = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Lit"));
            if (material.HasProperty("_VertexColorSRGB"))
                material.SetFloat("_VertexColorSRGB", 1f);
            return material;
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

            // Sandik karakterin boyuna gore kurulur; kadraj sandigi da kapsar.
            if (PlaceCrate(bounds.size.y, bounds.min.y))
            {
                bounds.Encapsulate(_crateRenderer.bounds);
                if (_footShadow != null)
                    bounds.Encapsulate(_footShadow.bounds);
            }

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
