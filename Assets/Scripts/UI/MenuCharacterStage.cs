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
        [Tooltip("KAPALI (onerilen): kamera, zemin ve karakter sahnede nereye koyduysan Play'de de " +
                 "orada durur (ne goruyorsan o). ACIK: kamera her acilista karakter+zemine gore " +
                 "kendini yeniden konumlar (asagidaki Pitch/Fov/Offset/Zoom ile).")]
        [SerializeField] private bool autoFrame;
        [Tooltip("Kamera karaktere hafif yukaridan baksin (derece). Sadece Auto Frame acikken.")]
        [SerializeField, Range(-10f, 60f)] private float cameraPitch = 18f;
        [Tooltip("Kameranin gorus acisi (derece). Buyuk = perspektif daha belirgin (zemin derin gorunur), " +
                 "kucuk = daha duz/telefoto. Kamera, kadraj sigacak kadar otomatik uzaklasir.")]
        [SerializeField, Range(10f, 70f)] private float cameraFov = 30f;
        [Tooltip("Karakteri ve zemini ekranda yukari (+) / asagi (-) kaydirir (kadraj yuksekliginin orani).")]
        [SerializeField, Range(-0.5f, 0.5f)] private float frameOffsetY = 0f;
        [Tooltip("1 = her sey tam sigar. Buyuk = yaklas, kucuk = uzaklas.")]
        [SerializeField, Range(0.5f, 2.5f)] private float frameZoom = 1f;

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

        [Header("Zemin (karakterin bastigi yer)")]
        [Tooltip("Sahnedeki zemin objesi (MenuKarakterSahnesi/KarakterZemini). Hierarchy'den tasi, olcekle, " +
                 "dondur; rengini materyalinden (Assets/GameArt/Materials/MenuZemin) degistir. Bos = zemin yok.")]
        [SerializeField] private Renderer ground;
        [Tooltip("Acikken Play'de karakterin ayaklari zeminin ust yuzeyine otomatik oturtulur " +
                 "(karakter zeminle birlikte hareket eder). Kapaliyken karakteri sen yerlestirirsin.")]
        [SerializeField] private bool plantFeet;
        [Tooltip("Ayaklarin zemine ne kadar gomulecegi (metre). Eksi = biraz havada.")]
        [SerializeField, Range(-0.2f, 0.2f)] private float footSink = 0.01f;
        [Tooltip("Acikken zeminin tamami kadraja sigar; kapaliyken sadece ayaklarin altinda " +
                 "'Ground In Frame' kadar zemin gorunur.")]
        [SerializeField] private bool frameWholeGround = true;
        [Tooltip("Kadrajda ayaklarin altinda gorunen zemin payi (karakter boyuna oran).")]
        [SerializeField, Range(0f, 0.6f)] private float groundInFrame = 0.1f;

        [SerializeField, HideInInspector] private int groundVersion;

        [Header("Ayak Golgesi")]
        [SerializeField] private bool footShadow = true;
        [Tooltip("Rengin alfasi golgenin koyulugudur.")]
        [SerializeField] private Color footShadowColor = new(0.1f, 0.06f, 0.02f, 0.38f);
        [Tooltip("Golgenin yaricapi (karakter boyuna oran).")]
        [SerializeField, Range(0.05f, 1.5f)] private float footShadowSize = 0.3f;

        private MeshRenderer _footShadow;
        private MaterialPropertyBlock _shadowBlock;

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

        [System.NonSerialized] private Mesh _bakedMesh;

        /// <summary>
        /// Modelin su anki pozundaki gercek en alt nokta (ayakkabi tabani). Iskeletli
        /// modelde koseler o anki poza gore hesaplanir (BakeMesh); digerlerinde sinir kutusu.
        /// </summary>
        private float LowestPoint(Renderer[] renderers, float fallback)
        {
            float lowest = float.MaxValue;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;

                if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
                {
                    // ??= degil: Unity objesinde "sahte null" ??= ile yakalanmaz.
                    if (_bakedMesh == null)
                        _bakedMesh = new Mesh { name = "AyakOlcumu", hideFlags = HideFlags.HideAndDontSave };
                    skinned.BakeMesh(_bakedMesh, true);
                    Transform t = skinned.transform;
                    Matrix4x4 toWorld = Matrix4x4.TRS(t.position, t.rotation, Vector3.one);
                    foreach (Vector3 vertex in _bakedMesh.vertices)
                        lowest = Mathf.Min(lowest, toWorld.MultiplyPoint3x4(vertex).y);
                }
                else if (renderer is MeshRenderer)
                {
                    lowest = Mathf.Min(lowest, renderer.bounds.min.y);
                }
            }

            return lowest < float.MaxValue ? lowest : fallback;
        }

        /// <summary>
        /// Ayaklarin altina, zeminin ustune yumusak golge (sadece oyun calisirken;
        /// editorde sahneye kalici nesne kaydedilmesin).
        /// </summary>
        private void PlaceFootShadow(float characterHeight)
        {
            if (!Application.isPlaying)
                return;

            if (!footShadow || ground == null)
            {
                if (_footShadow != null)
                    _footShadow.gameObject.SetActive(false);
                return;
            }

            if (_footShadow == null)
            {
                Transform parent = ground.transform.parent != null ? ground.transform.parent : ground.transform;
                _footShadow = NewBattle.Gameplay.OverlayMeshLibrary.CreateOverlayObject(
                    "AyakGolgesi", NewBattle.Gameplay.OverlayMeshLibrary.SoftDisc, parent, Vector3.zero, 1f);
                _footShadow.gameObject.layer = ground.gameObject.layer;
                _shadowBlock = new MaterialPropertyBlock();
            }

            _footShadow.gameObject.SetActive(true);
            Vector3 feet = turntable.position;
            _footShadow.transform.SetPositionAndRotation(
                new Vector3(feet.x, ground.bounds.max.y + 0.004f, feet.z), Quaternion.identity);
            _footShadow.transform.localScale = Vector3.one * Mathf.Max(0.01f, footShadowSize * characterHeight);

            _shadowBlock.SetColor(NewBattle.Gameplay.OverlayMeshLibrary.BaseColorId, footShadowColor);
            _shadowBlock.SetFloat(NewBattle.Gameplay.OverlayMeshLibrary.FillId, 1f);
            _footShadow.SetPropertyBlock(_shadowBlock);
        }

        private void OnDestroy()
        {
            if (_bakedMesh != null)
                DestroyImmediate(_bakedMesh);
        }

        private void OnValidate()
        {
            // Inspector'da golge/zemin ayari degisince oyun calisirken hemen gor.
            if (Application.isPlaying && isActiveAndEnabled)
                FrameCharacter();
        }

        /// <summary>
        /// Kamerayi karakterin boyuna gore yerlestirir. Model degisse ya da
        /// olcegi farkli olsa bile karakter cerceveye tam oturur.
        /// </summary>
        /// <summary>
        /// Sag tik > "Kadraji otomatik ayarla": Auto Frame kapaliyken de bir kez otomatik
        /// kadraj + ayak oturtma yapar (sonra elle ince ayar). Edit modunda sahneye yazilir.
        /// </summary>
        [ContextMenu("Kadraji otomatik ayarla (bir kez)")]
        public void AutoFrameOnce()
        {
            bool frame = autoFrame;
            bool plant = plantFeet;
            autoFrame = true;
            plantFeet = true;
            try
            {
                FrameCharacter();
            }
            finally
            {
                autoFrame = frame;
                plantFeet = plant;
            }
        }

        public Camera StageCamera => stageCamera;
        public Transform Turntable => turntable;

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

            // Ayaklari zemine oturt, golgeyi koy; kadraj ayaklarin altinda biraz zemin gostersin.
            if (ground != null && ground.gameObject.activeInHierarchy)
            {
                if (plantFeet)
                {
                    // Sinir kutusu (bounds) animasyonlu modelde ayaklardan asagi tasar;
                    // ona gore oturtunca ayaklar zemine gomuluyordu. Gercek en alt nokta.
                    float feetY = LowestPoint(renderers, bounds.min.y);
                    float delta = ground.bounds.max.y - footSink - feetY;
                    Animator animator = turntable.GetComponentInChildren<Animator>(true);
                    Transform model = animator != null ? animator.transform : turntable;
                    model.position += Vector3.up * delta;
                    bounds.center += Vector3.up * delta;
                }

                PlaceFootShadow(bounds.size.y);

                if (frameWholeGround)
                {
                    bounds.Encapsulate(ground.bounds);
                }
                else
                {
                    float below = groundInFrame * bounds.size.y;
                    bounds.Encapsulate(new Vector3(bounds.center.x, bounds.min.y - below, bounds.center.z));
                }
            }

            // Elle yerlestirme: kameraya dokunma (sahnede nasilsa oyle kalsin).
            if (!autoFrame)
                return;

            stageCamera.fieldOfView = cameraFov;

            Vector3 forward = turntable.parent != null ? turntable.parent.forward : Vector3.forward;
            Quaternion look = Quaternion.LookRotation(-forward) * Quaternion.Euler(cameraPitch, 0f, 0f);
            Vector3 viewForward = look * Vector3.forward;
            Vector3 viewUp = look * Vector3.up;
            Vector3 viewRight = look * Vector3.right;

            // Kadraj: kutunun 8 kosesi kameranin gozunden sigacak kadar uzaklas, sonra
            // icerigi dikeyde/yatayda ortala. Egik bakista (zemin one uzaniyor) da kesilmez.
            float tanV = Mathf.Tan(stageCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) / (1f + framePadding * 2f);
            float tanH = tanV * Mathf.Max(0.1f, stageCamera.aspect);
            Vector3 center = bounds.center;
            Vector3 ext = bounds.extents;

            float distance = 0.1f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 v = new Vector3((i & 1) == 0 ? -ext.x : ext.x, (i & 2) == 0 ? -ext.y : ext.y, (i & 4) == 0 ? -ext.z : ext.z);
                float x = Vector3.Dot(v, viewRight);
                float y = Vector3.Dot(v, viewUp);
                float z = Vector3.Dot(v, viewForward);
                distance = Mathf.Max(distance, Mathf.Abs(y) / tanV - z, Mathf.Abs(x) / tanH - z);
            }

            // Ortalama: koselerin ekrandaki en alt/ust ve sol/sag noktalarinin ortasi.
            float minY = float.MaxValue, maxY = float.MinValue, minX = float.MaxValue, maxX = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                Vector3 v = new Vector3((i & 1) == 0 ? -ext.x : ext.x, (i & 2) == 0 ? -ext.y : ext.y, (i & 4) == 0 ? -ext.z : ext.z);
                float depth = Mathf.Max(0.01f, distance + Vector3.Dot(v, viewForward));
                float sy = Vector3.Dot(v, viewUp) / depth;
                float sx = Vector3.Dot(v, viewRight) / depth;
                minY = Mathf.Min(minY, sy); maxY = Mathf.Max(maxY, sy);
                minX = Mathf.Min(minX, sx); maxX = Mathf.Max(maxX, sx);
            }

            Vector3 shift = viewUp * ((minY + maxY) * 0.5f * distance) + viewRight * ((minX + maxX) * 0.5f * distance);

            // Elle ince ayar: yakinlastirma ve dikey kaydirma.
            distance /= Mathf.Max(0.1f, frameZoom);
            float visibleHeight = 2f * distance * Mathf.Tan(stageCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            shift -= viewUp * (frameOffsetY * visibleHeight);
            stageCamera.transform.SetPositionAndRotation(center + shift - viewForward * distance, look);
            stageCamera.nearClipPlane = Mathf.Max(0.01f, distance * 0.05f);
            stageCamera.farClipPlane = distance * 4f + ext.magnitude * 2f;
        }
    }
}
