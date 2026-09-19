using System;
using Fusion;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(NetworkObject))]
public class SafeZoneController : NetworkBehaviour
{
    [Serializable]
    private struct ZoneStage
    {
        [Min(1f)] public float targetRadius;
        [Min(0f)] public float waitSeconds;
        [Min(0.1f)] public float shrinkSeconds;

        public ZoneStage(float targetRadius, float waitSeconds, float shrinkSeconds)
        {
            this.targetRadius = targetRadius;
            this.waitSeconds = waitSeconds;
            this.shrinkSeconds = shrinkSeconds;
        }
    }

    private const int WaitingPhase = 0;
    private const int ShrinkingPhase = 1;
    private const int FinishedPhase = 2;

    private static readonly ZoneStage[] DefaultStages =
    {
        new(24f, 10f, 9f),
        new(16f, 10f, 8f),
        new(10f, 5f, 7f),
        new(6f, 5f, 6f),
        new(3.5f, 5f, 5f)
    };

    [Header("Alan Asamalari")]
    [SerializeField, Min(1f)] private float initialRadius = 32f;
    [SerializeField, Range(0f, 1f)] private float nextCenterRandomness = 0.85f;
    [SerializeField] private ZoneStage[] stages =
    {
        new(24f, 10f, 9f),
        new(16f, 10f, 8f),
        new(10f, 5f, 7f),
        new(6f, 5f, 6f),
        new(3.5f, 5f, 5f)
    };

    [Header("Alan Disi Hasari")]
    [SerializeField, Min(0.1f)] private float damageInterval = 1f;
    [SerializeField, Min(0f)] private float damagePerInterval = 5f;

    [Header("Mevcut Alan Gorunumu")]
    [Tooltip("Sinir seridinin rengi. Alan disi zaten pus ile kizarir; serit " +
             "bunun uzerine oturan daha parlak, daha doygun bir bant.")]
    [SerializeField] private Color boundaryColor = new(1f, 0.44f, 0.42f, 0.9f);
    [Tooltip("Seridin DUNYA birimi cinsinden genisligi. Karakter capi ~1 birim; " +
             "3 birim uzaktan secilebilen kalin bir bant verir.")]
    [SerializeField, Min(0.1f)] private float boundaryWidth = 3f;
    [Tooltip("Kenarlarin ne kadarinin silinerek yumusayacagi. 0 = keskin kenarli " +
             "bant, 0.5 = yarisi gecise ayrilmis yumusak bant.")]
    [SerializeField, Range(0f, 0.9f)] private float boundaryEdgeSoftness = 0.4f;
    [SerializeField, Range(24, 160)] private int boundarySegments = 96;
    [SerializeField] private float boundaryHeight = 0.15f;

    [Header("Sonraki Alan Gorunumu")]
    [SerializeField] private Color nextBoundaryColor = new(1f, 1f, 1f, 0.95f);
    [SerializeField, Min(0.02f)] private float nextBoundaryWidth = 0.4f;
    [SerializeField] private float nextBoundaryHeight = 0.14f;

    [Header("Alan Disi Pusu")]
    [Tooltip("Guvenli cemberin DISINDA kalan zeminin rengi. Zemine serilen yumusak " +
             "bir kirmizi ortu; hicbir nesnenin uzerine tirmanmaz.")]
    [SerializeField] private Color outsideFogColor = new(0.92f, 0.16f, 0.18f, 0.24f);
    [SerializeField, Min(40f)] private float fogOuterRadius = 120f;
    [SerializeField] private float fogHeight = 0.08f;

    [Header("Zemin Takibi")]
    [Tooltip("Cember ve duvar arazinin yuksekligini takip eder. Bu olmadan " +
             "yukseltili haritada cizgi tepelerin altinda kalir.")]
    [SerializeField] private bool followGround = true;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField, Min(10f)] private float groundSampleFrom = 300f;

    [Header("Ekran Tehlike Efekti")]
    [SerializeField] private Color dangerScreenColor = new(0.75f, 0.02f, 0.02f, 1f);
    [SerializeField, Range(0f, 0.3f)] private float dangerBaseAlpha = 0.035f;
    [SerializeField, Range(0f, 0.5f)] private float dangerPulseAlpha = 0.16f;
    [SerializeField, Min(0.1f)] private float heartbeatRate = 1.15f;

    [Networked] public Vector3 SafeCenter { get; private set; }
    [Networked] public float SafeRadius { get; private set; }
    [Networked] public Vector3 NextCenter { get; private set; }
    [Networked] public float NextRadius { get; private set; }
    [Networked] public int CurrentStageIndex { get; private set; }
    [Networked] private int PhaseState { get; set; }
    [Networked] private Vector3 ShrinkStartCenter { get; set; }
    [Networked] private float ShrinkStartRadius { get; set; }
    [Networked] private float ActiveShrinkDuration { get; set; }
    [Networked] private TickTimer WaitTimer { get; set; }
    [Networked] private TickTimer ShrinkTimer { get; set; }
    [Networked] private TickTimer DamageTimer { get; set; }
    [Networked] private NetworkBool ZoneMatchInitialized { get; set; }

    /// <summary>
    /// Alan gorselleri (cember, pus, duvar). Inis secim haritasi bunlari
    /// gizleyebilsin diye disariya aciliyor: oyuncu inecegi yeri secerken
    /// haritayi temiz gormeli.
    /// </summary>
    public Transform VisualRoot => transform;

    /// <summary>Kalin sinir seridi: zemine serilen bir halka mesh.</summary>
    private Mesh _bandMesh;
    private Vector3[] _bandVertices;
    private Color[] _bandColors;
    private Transform _bandTransform;
    private Material _bandMaterial;
    private LineRenderer _nextBoundary;

    /// <summary>Serit kesitindeki halka sayisi: sonen ic kenar, iki cekirdek, sonen dis kenar.</summary>
    private const int BandRings = 4;
    /// <summary>Cember uzerindeki her segmentin zemin yuksekligi.</summary>
    private float[] _groundHeights;
    private float _nextGroundSampleTime;
    /// <summary>Dikey isin icin tek seferlik tampon; her karede dizi ayirmayalim.</summary>
    private readonly RaycastHit[] _groundHits = new RaycastHit[16];
    /// <summary>Son basarili zemin olcumu; isin hicbir seye carpmazsa buna doneriz.</summary>
    private float _lastGroundHeight;

    /// <summary>Zemin disindaki noktalar icin sabit dayanak yuksekligi.</summary>
    private float _referenceGroundHeight;

    private bool _hasReferenceGround;
    private Material _nextBoundaryMaterial;
    private Mesh _fogMesh;
    private Vector3[] _fogVertices;
    private Transform _fogTransform;
    private Material _fogMaterial;
    private Image _dangerOverlay;
    private GameObject _dangerCanvasObject;
    private Transform _localPlayer;
    private float _nextLocalPlayerSearchTime;
    private Vector3 _initialCenter;
    private LobbyCountdownController _lobbyCountdown;
    private DropPhaseController _dropPhase;

    public bool IsWaitingForShrink => ZoneMatchInitialized && PhaseState == WaitingPhase;
    public bool IsShrinking => ZoneMatchInitialized && PhaseState == ShrinkingPhase;
    public bool IsFinalZone => ZoneMatchInitialized && PhaseState == FinishedPhase;
    public bool HasNextZone => ZoneMatchInitialized && PhaseState != FinishedPhase && NextRadius > 0f;
    public int StageNumber => Mathf.Clamp(CurrentStageIndex + 1, 1, StageCount);
    public int StageCount => GetConfiguredStageCount();

    public float RemainingPhaseSeconds
    {
        get
        {
            if (Runner == null || !ZoneMatchInitialized)
                return 0f;

            TickTimer activeTimer = PhaseState == ShrinkingPhase ? ShrinkTimer : WaitTimer;
            return Mathf.Max(0f, activeTimer.RemainingTime(Runner) ?? 0f);
        }
    }

    public override void Spawned()
    {
        _initialCenter = transform.position;
        CreateBoundaries();
        CreateOutsideFog();
        CreateDangerOverlay();
        _lobbyCountdown = GetComponent<LobbyCountdownController>();
        _dropPhase = GetComponent<DropPhaseController>();

        if (!HasStateAuthority)
            return;

        SafeCenter = _initialCenter;
        SafeRadius = Mathf.Max(1f, initialRadius);
        NextCenter = SafeCenter;
        NextRadius = SafeRadius;
        CurrentStageIndex = 0;
        PhaseState = WaitingPhase;
        ZoneMatchInitialized = false;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        if (!ZoneMatchInitialized)
        {
            if (_lobbyCountdown != null && !_lobbyCountdown.MatchStarted)
                return;

            if (_dropPhase != null && !_dropPhase.GameplayStarted)
                return;

            BeginZoneMatch();
        }

        UpdateZonePhase();

        if (DamageTimer.ExpiredOrNotRunning(Runner))
        {
            DamagePlayersOutsideZone();
            DamageTimer = TickTimer.CreateFromSeconds(Runner, damageInterval);
        }
    }

    public override void Render()
    {
        SampleGroundHeights();
        DrawBoundaries();
        DrawOutsideFog();
        UpdateDangerOverlay();
    }

    /// <summary>
    /// Cember uzerindeki noktalarin zemin yuksekligini olcer.
    ///
    /// NEDEN: Cizgi ve pus eskiden SABIT bir dunya yuksekliginde (y = 0.12)
    /// ciziliyordu. Duz prototip arenada dogruydu; yukseltili bir haritada alan
    /// cizgisi tepelerin ICINDE kalir ve oyuncu daralan alani hic goremeden
    /// canini kaybeder.
    ///
    /// Isin atmak bedava degil, o yuzden saniyede ~12 kez ornekliyoruz ve
    /// sonucu iki cizgi, pus ve duvar arasinda paylasiyoruz.
    /// </summary>
    private void SampleGroundHeights()
    {
        int count = boundarySegments + 1;

        if (_groundHeights == null || _groundHeights.Length != count)
        {
            _groundHeights = new float[count];
            _nextGroundSampleTime = 0f;
        }

        if (!followGround)
        {
            for (int i = 0; i < count; i++)
                _groundHeights[i] = 0f;

            return;
        }

        if (Time.time < _nextGroundSampleTime)
            return;

        _nextGroundSampleTime = Time.time + 0.08f;

        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / boundarySegments;
            Vector3 origin = new(
                SafeCenter.x + Mathf.Cos(angle) * SafeRadius,
                groundSampleFrom,
                SafeCenter.z + Mathf.Sin(angle) * SafeRadius);


            // Olcum basarisizsa onceki degeri koruyoruz: harita disina tasan
            // bir segmentte cizgiyi y=0'a dusurmek, cizgiyi arazinin icinde
            // birakir.
            if (TrySampleGround(origin.x, origin.z, out float groundY))
                _groundHeights[i] = groundY;
        }
    }

    /// <summary>
    /// Tek bir noktanin ZEMIN yuksekligi.
    ///
    /// NEDEN en ALTTAKI temas: asagi dogru atilan bir isin yalniz zemine
    /// degil, zeminin uzerinde duran her seye carpar - agac tepesi, kaya,
    /// cadir brandasi. Ilk temasi kabul edersek cemberin o segmenti agacin
    /// tepesine tirmanir; alan cizgisinin nesnelerin uzerinden gecmesinin
    /// sebebi tam olarak buydu. Dikey bir isinda zemin her zaman en alttaki
    /// temastir, cunku diger her sey onun ustunde durur.
    ///
    /// Bu yontem haritadaki nesneleri ayri bir layer'a tasimayi gerektirmez:
    /// kullanici disaridan hazir bir arazi paketi attiginda da calisir.
    /// </summary>
    private bool TrySampleGround(float x, float z, out float height)
    {
        // Isin bosa giderse SABIT referans yuksekligi donuyor, son basarili
        // orneklemeyi degil.
        //
        // Neden onemli: alan cemberi haritadan buyuk oldugu icin cember
        // uzerindeki noktalarin bir kismi zeminin disinda kaliyor. Orada
        // "son bulunan yukseklik" donulurse, cember harita kenarina girip
        // ciktikca bant o anki son degeri miras aliyor ve yukseklik
        // zipliyor - bant ekranda kopuk kopuk gorunuyor.
        height = _referenceGroundHeight;

        int hitCount = Physics.RaycastNonAlloc(
            new Vector3(x, groundSampleFrom, z),
            Vector3.down,
            _groundHits,
            groundSampleFrom * 2f,
            groundMask,
            QueryTriggerInteraction.Ignore);

        if (hitCount <= 0)
            return false;

        float lowest = float.MaxValue;
        for (int i = 0; i < hitCount; i++)
            lowest = Mathf.Min(lowest, _groundHits[i].point.y);

        height = lowest;
        _lastGroundHeight = lowest;

        // Referans, haritada bulunan ILK gecerli yukseklik. Sonraki
        // orneklemelerle guncellenmiyor; guncellenseydi zemin disindaki
        // noktalar yine kayan bir degere baglanirdi.
        if (!_hasReferenceGround)
        {
            _referenceGroundHeight = lowest;
            _hasReferenceGround = true;
        }

        return true;
    }

    private float GroundHeightAt(int segment)
    {
        if (_groundHeights == null || _groundHeights.Length == 0)
            return 0f;

        return _groundHeights[Mathf.Clamp(segment, 0, _groundHeights.Length - 1)];
    }

    /// <summary>Tek nokta icin zemin yuksekligi; sonraki alan cemberi gibi ayri merkezler icin.</summary>
    private float SampleGroundAt(float x, float z)
    {
        if (!followGround)
            return 0f;

        TrySampleGround(x, z, out float groundY);
        return groundY;
    }

    private void BeginZoneMatch()
    {
        ZoneMatchInitialized = true;
        CurrentStageIndex = 0;
        SafeCenter = _initialCenter;
        SafeRadius = Mathf.Max(1f, initialRadius);
        DamageTimer = TickTimer.CreateFromSeconds(Runner, damageInterval);
        PrepareCurrentStage();
    }

    private void PrepareCurrentStage()
    {
        if (CurrentStageIndex >= GetConfiguredStageCount())
        {
            PhaseState = FinishedPhase;
            NextCenter = SafeCenter;
            NextRadius = SafeRadius;
            return;
        }

        ZoneStage stage = GetStage(CurrentStageIndex);
        float targetRadius = Mathf.Clamp(stage.targetRadius, 1f, SafeRadius);
        float maximumCenterOffset = Mathf.Max(0f, SafeRadius - targetRadius);
        Vector2 offset = UnityEngine.Random.insideUnitCircle * maximumCenterOffset * nextCenterRandomness;

        NextCenter = SafeCenter + new Vector3(offset.x, 0f, offset.y);
        NextRadius = targetRadius;
        PhaseState = WaitingPhase;
        WaitTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(0f, stage.waitSeconds));
    }

    private void UpdateZonePhase()
    {
        if (PhaseState == FinishedPhase)
            return;

        ZoneStage stage = GetStage(CurrentStageIndex);

        if (PhaseState == WaitingPhase)
        {
            if (!WaitTimer.ExpiredOrNotRunning(Runner))
                return;

            ShrinkStartCenter = SafeCenter;
            ShrinkStartRadius = SafeRadius;
            ActiveShrinkDuration = Mathf.Max(0.1f, stage.shrinkSeconds);
            ShrinkTimer = TickTimer.CreateFromSeconds(Runner, ActiveShrinkDuration);
            PhaseState = ShrinkingPhase;
        }

        float remaining = ShrinkTimer.RemainingTime(Runner) ?? 0f;
        float progress = 1f - Mathf.Clamp01(remaining / Mathf.Max(0.1f, ActiveShrinkDuration));
        SafeCenter = Vector3.Lerp(ShrinkStartCenter, NextCenter, progress);
        SafeRadius = Mathf.Lerp(ShrinkStartRadius, NextRadius, progress);

        if (!ShrinkTimer.ExpiredOrNotRunning(Runner))
            return;

        SafeCenter = NextCenter;
        SafeRadius = NextRadius;
        CurrentStageIndex++;
        PrepareCurrentStage();
    }

    private int GetConfiguredStageCount()
    {
        return stages != null && stages.Length > 0 ? stages.Length : DefaultStages.Length;
    }

    private ZoneStage GetStage(int index)
    {
        ZoneStage[] source = stages != null && stages.Length > 0 ? stages : DefaultStages;
        return source[Mathf.Clamp(index, 0, source.Length - 1)];
    }

    private void DamagePlayersOutsideZone()
    {
        float radiusSquared = SafeRadius * SafeRadius;
        var registry = NewBattle.Gameplay.PlayerRegistry.All;

        for (int i = 0; i < registry.Count; i++)
        {
            HealthController player = registry[i] != null ? registry[i].Health : null;

            if (player == null || !player.HasStateAuthority || player.currentHealth <= 0f)
                continue;

            Vector3 offset = player.transform.position - SafeCenter;
            offset.y = 0f;

            if (offset.sqrMagnitude > radiusSquared)
                player.TakeDamage(damagePerInterval);
        }
    }

    private void CreateBoundaries()
    {
        CreateBoundaryBand();
        _nextBoundary = CreateBoundaryRenderer("NextSafeZoneBoundary", nextBoundaryColor, nextBoundaryWidth, out _nextBoundaryMaterial);

        SampleGroundHeights();
        DrawBoundaries();
    }

    private LineRenderer CreateBoundaryRenderer(string objectName, Color color, float width, out Material material)
    {
        GameObject boundaryObject = new(objectName);
        boundaryObject.transform.SetParent(transform, false);

        LineRenderer line = boundaryObject.AddComponent<LineRenderer>();
        line.loop = true;
        line.useWorldSpace = true;
        line.positionCount = boundarySegments;
        line.startWidth = width;
        line.endWidth = width;
        line.numCornerVertices = 2;
        line.numCapVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        material = new Material(shader) { name = $"Runtime{objectName}Material" };
        ConfigureTransparentMaterial(material, color);
        line.sharedMaterial = material;
        line.startColor = color;
        line.endColor = color;
        return line;
    }

    /// <summary>
    /// Sinir seridini kurar.
    ///
    /// NEDEN LineRenderer degil de mesh: LineRenderer seridi varsayilan
    /// olarak kameraya dondurur (billboard). Ince bir cizgide fark edilmez
    /// ama kalinlastirdiginda serit zeminden kalkip ekrana dik duran bir
    /// perdeye donusur - istedigimiz ise zemine serilmis bir bant. Mesh ile
    /// serit gercekten yatay durur ve arazinin yuksekligini takip eder.
    /// </summary>
    private void CreateBoundaryBand()
    {
        GameObject bandObject = new("SafeZoneBoundaryBand");
        bandObject.transform.SetParent(transform, false);
        _bandTransform = bandObject.transform;

        MeshFilter filter = bandObject.AddComponent<MeshFilter>();
        MeshRenderer renderer = bandObject.AddComponent<MeshRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        _bandMesh = new Mesh { name = "RuntimeZoneBandMesh" };
        _bandMesh.MarkDynamic();
        filter.sharedMesh = _bandMesh;

        Shader shader = Shader.Find("NewBattle/ZoneBand");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");

        _bandMaterial = new Material(shader) { name = "RuntimeZoneBandMaterial" };

        // Pustan SONRA cizilsin. Ikisi de saydam ve neredeyse ayni yukseklikte;
        // ayni siradaysalar kameranin acisina gore sirayi degistirip
        // titresirler. Serit ustte kalmali, zaten pusun uzerine oturuyor.
        _bandMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 10;

        if (_bandMaterial.HasProperty("_BaseColor"))
            _bandMaterial.SetColor("_BaseColor", boundaryColor);

        renderer.sharedMaterial = _bandMaterial;

        BuildBandTopology();
    }

    /// <summary>
    /// Seridin ucgenleri. Her segmentte dort halka noktasi var:
    /// sonen ic kenar, dolu cekirdegin iki yakasi, sonen dis kenar.
    /// Aradaki uc quad her karede sadece konum guncellemesi aliyor.
    /// </summary>
    private void BuildBandTopology()
    {
        int ringCount = boundarySegments + 1;

        _bandVertices = new Vector3[ringCount * BandRings];
        _bandColors = new Color[ringCount * BandRings];

        // Kenar saydamligi: dista 0, cekirdekte 1. Kose rengi shader'da
        // carpan oldugu icin materyalin kendi alfasi da korunuyor.
        for (int i = 0; i < ringCount; i++)
        {
            int baseIndex = i * BandRings;
            _bandColors[baseIndex] = new Color(1f, 1f, 1f, 0f);
            _bandColors[baseIndex + 1] = Color.white;
            _bandColors[baseIndex + 2] = Color.white;
            _bandColors[baseIndex + 3] = new Color(1f, 1f, 1f, 0f);
        }

        int[] triangles = new int[boundarySegments * 3 * 6];
        int write = 0;

        for (int i = 0; i < boundarySegments; i++)
        {
            int current = i * BandRings;
            int next = (i + 1) * BandRings;

            for (int ring = 0; ring < BandRings - 1; ring++)
            {
                triangles[write++] = current + ring;
                triangles[write++] = next + ring;
                triangles[write++] = current + ring + 1;

                triangles[write++] = current + ring + 1;
                triangles[write++] = next + ring;
                triangles[write++] = next + ring + 1;
            }
        }

        _bandMesh.Clear();
        _bandMesh.vertices = _bandVertices;
        _bandMesh.colors = _bandColors;
        _bandMesh.triangles = triangles;
    }

    private void DrawBoundaryBand()
    {
        if (_bandMesh == null || _bandTransform == null || SafeRadius <= 0f)
            return;

        int ringCount = boundarySegments + 1;

        if (_bandVertices == null || _bandVertices.Length != ringCount * BandRings)
            BuildBandTopology();

        float half = boundaryWidth * 0.5f;
        float core = half * (1f - Mathf.Clamp01(boundaryEdgeSoftness));

        // Serit guvenli yaricapin iki yanina esit yayiliyor: oyuncu bandin
        // uzerine bastiginda hala guvende, hasar cizgisi bandin orta ekseni.
        float[] radii =
        {
            Mathf.Max(0f, SafeRadius - half),
            Mathf.Max(0f, SafeRadius - core),
            SafeRadius + core,
            SafeRadius + half
        };

        for (int i = 0; i < ringCount; i++)
        {
            float angle = i * Mathf.PI * 2f / boundarySegments;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            // Dort halka da ayni zemin yuksekligini kullaniyor. Serit birkac
            // birim genis; bu genislikte ayri ayri isin atmak dort kat maliyet
            // karsiliginda birkac santimlik kazanc demek olurdu.
            float y = GroundHeightAt(i) + boundaryHeight;
            int baseIndex = i * BandRings;

            for (int ring = 0; ring < BandRings; ring++)
            {
                _bandVertices[baseIndex + ring] =
                    new Vector3(cos * radii[ring], y, sin * radii[ring]);
            }
        }

        _bandTransform.position = new Vector3(SafeCenter.x, 0f, SafeCenter.z);
        _bandMesh.vertices = _bandVertices;
        _bandMesh.RecalculateBounds();
    }

    private void DrawBoundaries()
    {
        DrawBoundaryBand();

        if (_nextBoundary != null)
        {
            _nextBoundary.enabled = HasNextZone;
            if (HasNextZone)
                DrawCircle(_nextBoundary, NextCenter, NextRadius, nextBoundaryHeight, false);
        }
    }

    /// <summary>
    /// useCachedGround: mevcut alan cemberi onceden olculmus yukseklikleri kullanir.
    /// Sonraki alan cemberi baska bir merkez/yaricapta oldugu icin kendi olcumunu
    /// yapar.
    /// </summary>
    private void DrawCircle(LineRenderer line, Vector3 center, float radius, float height,
        bool useCachedGround)
    {
        if (line == null || radius <= 0f)
            return;

        if (line.positionCount != boundarySegments)
            line.positionCount = boundarySegments;

        for (int i = 0; i < boundarySegments; i++)
        {
            float angle = i * Mathf.PI * 2f / boundarySegments;
            float x = center.x + Mathf.Cos(angle) * radius;
            float z = center.z + Mathf.Sin(angle) * radius;
            float groundY = useCachedGround ? GroundHeightAt(i) : SampleGroundAt(x, z);

            line.SetPosition(i, new Vector3(x, groundY + height, z));
        }
    }

    private void CreateOutsideFog()
    {
        GameObject fogObject = new("OutsideZoneFog");
        fogObject.transform.SetParent(transform, false);
        _fogTransform = fogObject.transform;

        MeshFilter meshFilter = fogObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = fogObject.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        _fogMesh = new Mesh { name = "RuntimeOutsideZoneFogMesh" };
        _fogMesh.MarkDynamic();
        meshFilter.sharedMesh = _fogMesh;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        _fogMaterial = new Material(shader) { name = "RuntimeOutsideZoneFogMaterial" };
        ConfigureTransparentMaterial(_fogMaterial, outsideFogColor);
        meshRenderer.sharedMaterial = _fogMaterial;

        BuildFogTriangles();
        DrawOutsideFog();
    }

    private void BuildFogTriangles()
    {
        int[] triangles = new int[boundarySegments * 6];

        for (int i = 0; i < boundarySegments; i++)
        {
            int vertex = i * 2;
            int triangle = i * 6;
            triangles[triangle] = vertex;
            triangles[triangle + 1] = vertex + 1;
            triangles[triangle + 2] = vertex + 2;
            triangles[triangle + 3] = vertex + 2;
            triangles[triangle + 4] = vertex + 1;
            triangles[triangle + 5] = vertex + 3;
        }

        _fogMesh.Clear();
        _fogVertices = new Vector3[(boundarySegments + 1) * 2];
        _fogMesh.vertices = _fogVertices;
        _fogMesh.triangles = triangles;
    }

    private void DrawOutsideFog()
    {
        if (_fogMesh == null || _fogTransform == null || SafeRadius <= 0f)
            return;

        int requiredVertexCount = (boundarySegments + 1) * 2;
        if (_fogMesh.vertexCount != requiredVertexCount)
            BuildFogTriangles();

        float outerRadius = Mathf.Max(fogOuterRadius, SafeRadius + 1f);
        for (int i = 0; i <= boundarySegments; i++)
        {
            float angle = i * Mathf.PI * 2f / boundarySegments;
            float x = Mathf.Cos(angle);
            float z = Mathf.Sin(angle);

            // Pus da zemine oturur; sabit yukseklikte birakirsak tepelerin
            // icinde kaybolur, cukurlarda havada asili durur.
            float groundY = GroundHeightAt(i) + fogHeight;

            _fogVertices[i * 2] = new Vector3(x * SafeRadius, groundY, z * SafeRadius);
            _fogVertices[i * 2 + 1] = new Vector3(x * outerRadius, groundY, z * outerRadius);
        }

        _fogTransform.position = new Vector3(SafeCenter.x, 0f, SafeCenter.z);
        _fogMesh.vertices = _fogVertices;
        _fogMesh.RecalculateBounds();
    }

    private static void ConfigureTransparentMaterial(Material material, Color color)
    {
        material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private void CreateDangerOverlay()
    {
        _dangerCanvasObject = new GameObject("OutsideZoneDangerOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _dangerCanvasObject.transform.SetParent(transform, false);

        Canvas canvas = _dangerCanvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;

        CanvasScaler scaler = _dangerCanvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject imageObject = new("RedPulse", typeof(RectTransform), typeof(Image));
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.SetParent(_dangerCanvasObject.transform, false);
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;

        _dangerOverlay = imageObject.GetComponent<Image>();
        _dangerOverlay.raycastTarget = false;
        _dangerOverlay.color = Color.clear;
        _dangerCanvasObject.GetComponent<GraphicRaycaster>().enabled = false;
    }

    private void UpdateDangerOverlay()
    {
        if (_dangerOverlay == null)
            return;

        if (_localPlayer == null && Time.unscaledTime >= _nextLocalPlayerSearchTime)
        {
            FindLocalPlayer();
            _nextLocalPlayerSearchTime = Time.unscaledTime + 0.25f;
        }

        HealthController localHealth = _localPlayer != null ? _localPlayer.GetComponent<HealthController>() : null;
        if (MatchResultHUD.LocalGameplayStopped || localHealth == null || localHealth.Object == null ||
            !localHealth.Object.IsValid || localHealth.currentHealth <= 0f)
        {
            _dangerOverlay.color = Color.clear;
            return;
        }

        Vector3 offset = _localPlayer.position - SafeCenter;
        offset.y = 0f;
        bool isOutside = SafeRadius > 0f && offset.sqrMagnitude > SafeRadius * SafeRadius;

        float alpha = 0f;
        if (isOutside)
        {
            float phase = Mathf.Repeat(Time.unscaledTime * heartbeatRate, 1f);
            float firstBeat = Mathf.Exp(-Mathf.Pow((phase - 0.12f) / 0.055f, 2f));
            float secondBeat = 0.62f * Mathf.Exp(-Mathf.Pow((phase - 0.29f) / 0.07f, 2f));
            alpha = dangerBaseAlpha + dangerPulseAlpha * Mathf.Max(firstBeat, secondBeat);
        }

        _dangerOverlay.color = new Color(dangerScreenColor.r, dangerScreenColor.g, dangerScreenColor.b, alpha);
    }

    private void FindLocalPlayer()
    {
        HealthController[] players = UnityEngine.Object.FindObjectsByType<HealthController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (HealthController player in players)
        {
            if (player == null || player.Object == null || !player.HasInputAuthority)
                continue;

            _localPlayer = player.transform;
            return;
        }
    }

    [ContextMenu("Use Recommended Zone Stages")]
    private void UseRecommendedZoneStages()
    {
        stages = (ZoneStage[])DefaultStages.Clone();
    }

    private void OnDestroy()
    {
        if (_bandMaterial != null) Destroy(_bandMaterial);
        if (_bandMesh != null) Destroy(_bandMesh);
        if (_nextBoundaryMaterial != null) Destroy(_nextBoundaryMaterial);
        if (_fogMaterial != null) Destroy(_fogMaterial);
        if (_fogMesh != null) Destroy(_fogMesh);
        if (_dangerCanvasObject != null) Destroy(_dangerCanvasObject);
    }
}
