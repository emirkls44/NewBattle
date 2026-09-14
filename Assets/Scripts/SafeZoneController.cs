using Fusion;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(NetworkObject))]
public class SafeZoneController : NetworkBehaviour
{
    [Header("Alan Ayarlari")]
    [SerializeField, Min(1f)] private float initialRadius = 32f;
    [SerializeField, Min(1f)] private float finalRadius = 4f;
    [SerializeField, Min(0f)] private float waitBeforeShrink = 20f;
    [SerializeField, Min(1f)] private float shrinkDuration = 120f;
    [SerializeField, Range(0f, 1f)] private float finalCenterRandomness = 0.85f;

    [Header("Alan Disi Hasari")]
    [SerializeField, Min(0.1f)] private float damageInterval = 1f;
    [SerializeField, Min(0f)] private float damagePerInterval = 5f;

    [Header("Gorunum")]
    [SerializeField] private Color boundaryColor = new(0.2f, 0.65f, 1f, 0.9f);
    [SerializeField, Min(0.02f)] private float boundaryWidth = 0.22f;
    [SerializeField, Range(24, 160)] private int boundarySegments = 96;
    [SerializeField] private float boundaryHeight = 0.12f;

    [Header("Alan Disi Pusu")]
    [SerializeField] private Color outsideFogColor = new(0.12f, 0.28f, 0.42f, 0.22f);
    [SerializeField, Min(40f)] private float fogOuterRadius = 80f;
    [SerializeField] private float fogHeight = 0.08f;

    [Header("Ekran Tehlike Efekti")]
    [SerializeField] private Color dangerScreenColor = new(0.75f, 0.02f, 0.02f, 1f);
    [SerializeField, Range(0f, 0.3f)] private float dangerBaseAlpha = 0.035f;
    [SerializeField, Range(0f, 0.5f)] private float dangerPulseAlpha = 0.16f;
    [SerializeField, Min(0.1f)] private float heartbeatRate = 1.15f;

    [Networked] public Vector3 SafeCenter { get; private set; }
    [Networked] public float SafeRadius { get; private set; }
    [Networked] public Vector3 FinalCenter { get; private set; }
    [Networked] private TickTimer WaitTimer { get; set; }
    [Networked] private TickTimer ShrinkTimer { get; set; }
    [Networked] private TickTimer DamageTimer { get; set; }
    [Networked] private NetworkBool ShrinkStarted { get; set; }
    [Networked] private NetworkBool ShrinkFinished { get; set; }
    [Networked] private NetworkBool ZoneMatchInitialized { get; set; }

    private LineRenderer _boundary;
    private Material _boundaryMaterial;
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

    public bool IsWaitingForShrink => !ShrinkStarted;
    public bool IsShrinking => ShrinkStarted && !ShrinkFinished;
    public bool IsFinalZone => ShrinkFinished;

    public float RemainingPhaseSeconds
    {
        get
        {
            if (Runner == null)
                return 0f;

            TickTimer activeTimer = ShrinkStarted ? ShrinkTimer : WaitTimer;
            return Mathf.Max(0f, activeTimer.RemainingTime(Runner) ?? 0f);
        }
    }

    public override void Spawned()
    {
        _initialCenter = transform.position;
        CreateBoundary();
        CreateOutsideFog();
        CreateDangerOverlay();
        _lobbyCountdown = GetComponent<LobbyCountdownController>();
        _dropPhase = GetComponent<DropPhaseController>();

        if (!HasStateAuthority)
            return;

        float safeFinalRadius = Mathf.Clamp(finalRadius, 1f, initialRadius);
        float maximumOffset = Mathf.Max(0f, initialRadius - safeFinalRadius);
        Vector2 randomOffset = Random.insideUnitCircle * maximumOffset * finalCenterRandomness;

        SafeCenter = _initialCenter;
        SafeRadius = initialRadius;
        FinalCenter = _initialCenter + new Vector3(randomOffset.x, 0f, randomOffset.y);
        ZoneMatchInitialized = false;
        ShrinkStarted = false;
        ShrinkFinished = false;
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

            ZoneMatchInitialized = true;
            WaitTimer = TickTimer.CreateFromSeconds(Runner, waitBeforeShrink);
            DamageTimer = TickTimer.CreateFromSeconds(Runner, damageInterval);
        }

        UpdateShrinking();

        if (DamageTimer.ExpiredOrNotRunning(Runner))
        {
            DamagePlayersOutsideZone();
            DamageTimer = TickTimer.CreateFromSeconds(Runner, damageInterval);
        }
    }

    public override void Render()
    {
        DrawBoundary();
        DrawOutsideFog();
        UpdateDangerOverlay();
    }

    private void UpdateShrinking()
    {
        if (ShrinkFinished)
            return;

        if (!ShrinkStarted)
        {
            if (!WaitTimer.Expired(Runner))
                return;

            ShrinkStarted = true;
            ShrinkTimer = TickTimer.CreateFromSeconds(Runner, shrinkDuration);
        }

        float remaining = ShrinkTimer.RemainingTime(Runner) ?? 0f;
        float progress = 1f - Mathf.Clamp01(remaining / shrinkDuration);

        SafeCenter = Vector3.Lerp(_initialCenter, FinalCenter, progress);
        SafeRadius = Mathf.Lerp(initialRadius, finalRadius, progress);

        if (!ShrinkTimer.Expired(Runner))
            return;

        SafeCenter = FinalCenter;
        SafeRadius = finalRadius;
        ShrinkFinished = true;
    }

    private void DamagePlayersOutsideZone()
    {
        HealthController[] players = UnityEngine.Object.FindObjectsByType<HealthController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        float radiusSquared = SafeRadius * SafeRadius;

        foreach (HealthController player in players)
        {
            if (player == null || !player.HasStateAuthority || player.currentHealth <= 0f)
                continue;

            Vector3 offset = player.transform.position - SafeCenter;
            offset.y = 0f;

            if (offset.sqrMagnitude > radiusSquared)
                player.TakeDamage(damagePerInterval);
        }
    }

    private void CreateBoundary()
    {
        if (_boundary != null)
            return;

        GameObject boundaryObject = new("SafeZoneBoundary");
        boundaryObject.transform.SetParent(transform, false);

        _boundary = boundaryObject.AddComponent<LineRenderer>();
        _boundary.loop = true;
        _boundary.useWorldSpace = true;
        _boundary.positionCount = boundarySegments;
        _boundary.startWidth = boundaryWidth;
        _boundary.endWidth = boundaryWidth;
        _boundary.numCornerVertices = 2;
        _boundary.numCapVertices = 2;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        _boundaryMaterial = new Material(shader)
        {
            name = "RuntimeSafeZoneMaterial",
            color = boundaryColor
        };

        _boundary.sharedMaterial = _boundaryMaterial;
        _boundary.startColor = boundaryColor;
        _boundary.endColor = boundaryColor;
        DrawBoundary();
    }

    private void DrawBoundary()
    {
        if (_boundary == null || SafeRadius <= 0f)
            return;

        if (_boundary.positionCount != boundarySegments)
            _boundary.positionCount = boundarySegments;

        for (int i = 0; i < boundarySegments; i++)
        {
            float angle = i * Mathf.PI * 2f / boundarySegments;
            Vector3 point = SafeCenter + new Vector3(
                Mathf.Cos(angle) * SafeRadius,
                boundaryHeight,
                Mathf.Sin(angle) * SafeRadius
            );
            _boundary.SetPosition(i, point);
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

            _fogVertices[i * 2] = new Vector3(x * SafeRadius, fogHeight, z * SafeRadius);
            _fogVertices[i * 2 + 1] = new Vector3(x * outerRadius, fogHeight, z * outerRadius);
        }

        _fogTransform.position = new Vector3(SafeCenter.x, transform.position.y, SafeCenter.z);
        _fogMesh.vertices = _fogVertices;
        _fogMesh.RecalculateBounds();
    }

    private static void ConfigureTransparentMaterial(Material material, Color color)
    {
        material.color = color;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);

        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);

        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);

        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", 0f);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private void CreateDangerOverlay()
    {
        _dangerCanvasObject = new GameObject(
            "OutsideZoneDangerOverlay",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );

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
        _dangerOverlay.color = new Color(dangerScreenColor.r, dangerScreenColor.g, dangerScreenColor.b, 0f);

        GraphicRaycaster raycaster = _dangerCanvasObject.GetComponent<GraphicRaycaster>();
        raycaster.enabled = false;
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

        var localHealth = _localPlayer != null ? _localPlayer.GetComponent<HealthController>() : null;
        if (MatchResultHUD.LocalGameplayStopped || localHealth == null ||
            localHealth.Object == null || !localHealth.Object.IsValid || localHealth.currentHealth <= 0f)
        {
            _dangerOverlay.color = Color.clear;
            return;
        }
        bool isOutside = false;
        if (_localPlayer != null && SafeRadius > 0f)
        {
            Vector3 offset = _localPlayer.position - SafeCenter;
            offset.y = 0f;
            isOutside = offset.sqrMagnitude > SafeRadius * SafeRadius;
        }

        float alpha = 0f;
        if (isOutside)
        {
            float phase = Mathf.Repeat(Time.unscaledTime * heartbeatRate, 1f);
            float firstBeat = Mathf.Exp(-Mathf.Pow((phase - 0.12f) / 0.055f, 2f));
            float secondBeat = 0.62f * Mathf.Exp(-Mathf.Pow((phase - 0.29f) / 0.07f, 2f));
            alpha = dangerBaseAlpha + dangerPulseAlpha * Mathf.Max(firstBeat, secondBeat);
        }

        _dangerOverlay.color = new Color(
            dangerScreenColor.r,
            dangerScreenColor.g,
            dangerScreenColor.b,
            alpha
        );
    }

    private void FindLocalPlayer()
    {
        HealthController[] players = UnityEngine.Object.FindObjectsByType<HealthController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        foreach (HealthController player in players)
        {
            if (player == null || player.Object == null || !player.HasInputAuthority)
                continue;

            _localPlayer = player.transform;
            return;
        }
    }

    private void OnDestroy()
    {
        if (_boundaryMaterial != null)
            Destroy(_boundaryMaterial);

        if (_fogMaterial != null)
            Destroy(_fogMaterial);

        if (_fogMesh != null)
            Destroy(_fogMesh);

        if (_dangerCanvasObject != null)
            Destroy(_dangerCanvasObject);
    }
}
