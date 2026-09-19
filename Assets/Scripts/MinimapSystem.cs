using UnityEngine;
using UnityEngine.UI;

public class MinimapSystem : MonoBehaviour
{
    [Header("Minimap Kamera")]
    [SerializeField] private int textureResolution = 512;
    [SerializeField] private float mapHeight = 50f;

    /// <summary>
    /// Haritanin bos kalan yerlerinin rengi (deniz / bosluk).
    ///
    /// Sabit yazilmis degildi, cunku minimapin genel tonunu belirleyen
    /// sey bu: harita dokusunun altinda kalan her piksel bu renk.
    /// Sahnedeki MapImage'in stop halindeki rengi de buna esitleniyor,
    /// boylece Play'e basmadan minimap nasil gorunecekse oyle gorunuyor.
    /// </summary>
    [Tooltip("Minimapin bos alan rengi. MapImage'in stop halindeki rengi de bu olmali.")]
    [SerializeField] private Color mapBackgroundColor = new(0.12f, 0.25f, 0.31f, 1f);

    [Tooltip("Minimapta gorunen dunya yaricapi. Kucuk deger = daha yakin.")]
    [SerializeField] private float visibleWorldRadius = 11f;

    [Header("Alanla Yakinlasma")]
    [Tooltip("Acikken minimap, guvenli alan daraldikca yakinlasir.")]
    [SerializeField] private bool zoomFollowsZone = true;

    [Tooltip("Guvenli alanin kac katini gostersin. 1 = tam alan, 1.35 = biraz disi da.")]
    [SerializeField, Range(1f, 2.5f)] private float zoomPadding = 1.35f;

    [Tooltip("En yakin gorunum yaricapi. Bunun altina inmez.")]
    [SerializeField, Min(3f)] private float minVisibleRadius = 9f;

    [Tooltip("En uzak gorunum yaricapi. Mac basinda bu kullanilir.")]
    [SerializeField, Min(5f)] private float maxVisibleRadius = 45f;

    [Tooltip("Yakinlasmanin yumusakligi. Buyudukce daha cabuk oturur.")]
    [SerializeField, Min(0.1f)] private float zoomSpeed = 1.4f;

    [Header("Tedarik ve Yon Oku")]
    [SerializeField] private Color airdropColor = new(1f, 0.85f, 0.15f, 1f);
    [SerializeField, Min(4f)] private float airdropMarkerSize = 16f;

    [Header("Minimap UI")]
    [SerializeField] private float minimapSize = 220f;

    [Header("Sahne Referansi")]
    /// <summary>
    /// Sahnede hazir kurulmus minimap nesnesi.
    ///
    /// Atanirsa UI calisma aninda URETILMEZ; buradaki nesne kullanilir.
    /// Boylece cerceveyi, boyutu, renkleri Inspector'dan gorerek
    /// ayarlayabiliyorsun - calisma aninda uretilen bir UI'i duzenlemek
    /// mumkun degil, cunku Play'e basmadan ortada nesne olmuyor.
    ///
    /// Bos birakilirsa eski davranis surer ve UI kendiliginden kurulur.
    /// </summary>
    [Tooltip("Sahnedeki hazir minimap. Atanirsa UI calisma aninda uretilmez.")]
    [SerializeField] private RectTransform minimapRoot;
    [SerializeField] private Vector2 cornerOffset = new(25f, 25f);

    [Tooltip("Acikken minimap kare, kapaliyken daire olur.")]
    [SerializeField] private bool squareMinimap = true;
    [SerializeField] private Color borderColor = Color.white;
    [SerializeField, Min(1f)] private float borderThickness = 3f;

    [Header("Guvenli Alan")]
    [Tooltip("Su anki alan siniri. Oyuncunun 'nereye kacmaliyim' sorusunu " +
             "cevaplayan cizgi bu.")]
    [SerializeField] private Color currentZoneColor = new(1f, 0.24f, 0.42f, 0.95f);
    [SerializeField, Min(1f)] private float currentZoneLineWidth = 4f;

    [Header("Sonraki Alan")]
    [SerializeField] private Color nextZoneColor = new(1f, 1f, 1f, 0.95f);
    [SerializeField, Min(1f)] private float nextZoneLineWidth = 3f;

    private RenderTexture _renderTexture;
    private GameObject _cameraObject;
    private GameObject _uiRoot;
    private Texture2D _circleMaskTexture;
    private Sprite _circleMaskSprite;
    private DropPhaseController _dropPhase;
    private SafeZoneController _safeZone;
    private Transform _localPlayer;
    private RectTransform _nextZoneRect;
    private CircleOutlineGraphic _nextZoneGraphic;
    private RectTransform _currentZoneRect;
    private CircleOutlineGraphic _currentZoneGraphic;
    private float _nextSearchTime;

    [Header("Takim Isaretleri")]
    [SerializeField] private Color teammateColor = new(0.25f, 0.66f, 1f, 1f);
    [SerializeField, Min(0)] private int maxTeammateMarkers = 3;
    [SerializeField, Min(4f)] private float teammateMarkerSize = 13f;

    private RectTransform[] _teammateMarkers;
    private RectTransform _minimapRect;

    /// <summary>
    /// Su anda gosterilen dunya yaricapi.
    ///
    /// visibleWorldRadius sabit bir BASLANGIC degeri; asil kullanilan bu.
    /// Alan daraldikca kuculuyor, boylece gec oyunda minimap sadece
    /// catismanin gectigi yeri gosteriyor.
    /// </summary>
    private float _visibleRadius;

    private RectTransform _airdropMarker;
    private Transform _trackedCrate;
    private float _nextCrateSearchTime;

    private void Start()
    {
        CreateMinimapCamera();
        CreateMinimapUI();
        if (_uiRoot != null)
            _uiRoot.SetActive(false);
    }

    private void Update()
    {
        if (Time.unscaledTime >= _nextSearchTime)
        {
            _dropPhase ??= UnityEngine.Object.FindFirstObjectByType<DropPhaseController>();
            _safeZone ??= UnityEngine.Object.FindFirstObjectByType<SafeZoneController>();
            if (_localPlayer == null)
                FindLocalPlayer();
            _nextSearchTime = Time.unscaledTime + 0.25f;
        }

        bool dropPhaseIsSpawned = _dropPhase != null && _dropPhase.Object != null &&
                                  _dropPhase.Runner != null && _dropPhase.Runner.IsRunning;
        bool gameplayStarted = dropPhaseIsSpawned && _dropPhase.GameplayStarted;

        if (_uiRoot != null)
            _uiRoot.SetActive(gameplayStarted);

        if (gameplayStarted)
        {
            UpdateZoom();
            UpdateZoneMarkers();
            UpdateTeammateMarkers();
            UpdateAirdropMarker();
        }
    }

    /// <summary>
    /// Minimapi guvenli alana gore yakinlastirir.
    ///
    /// Sabit yaricapta minimap gec oyunda ise yaramaz hale geliyor: alan
    /// kucuk bir cembere inince haritanin tamami gosterilmeye devam ediyor
    /// ve oyuncu kendi cevresinde ne oldugunu goremiyor. Alanla birlikte
    /// kuculunce minimap her zaman catismanin gectigi yeri gosteriyor.
    /// </summary>
    private void UpdateZoom()
    {
        float target = visibleWorldRadius;

        bool zoneReady = _safeZone != null && _safeZone.Object != null &&
                         _safeZone.Runner != null && _safeZone.Runner.IsRunning;

        if (zoomFollowsZone && zoneReady && _safeZone.SafeRadius > 0f)
            target = _safeZone.SafeRadius * zoomPadding;

        target = Mathf.Clamp(target, minVisibleRadius, maxVisibleRadius);

        // Ussel yumusatma: dogrudan Lerp(a, b, speed * deltaTime) kullansaydik
        // gecis hizi kare hizina bagli olurdu - 30 fps'te baska, 120 fps'te
        // baska hizda yakinlasirdi. Bu bicim kare hizindan bagimsiz.
        _visibleRadius = Mathf.Lerp(_visibleRadius, target,
            1f - Mathf.Exp(-zoomSpeed * Time.deltaTime));

        if (_cameraObject != null && _cameraObject.TryGetComponent(out Camera minimapCamera))
            minimapCamera.orthographicSize = _visibleRadius;
    }

    /// <summary>
    /// Tedarik paketini minimapta sari nokta olarak gosterir.
    ///
    /// Paket gorunum alaninin disindaysa nokta minimapin KENARINA yapisiyor.
    /// Boylece paket cok uzaktayken bile hangi yonde oldugu belli oluyor;
    /// gizlenseydi oyuncu paketin varligindan haberdar olamazdi.
    /// </summary>
    private void UpdateAirdropMarker()
    {
        if (_airdropMarker == null || _localPlayer == null)
            return;

        if (_trackedCrate == null && Time.unscaledTime >= _nextCrateSearchTime)
        {
            _nextCrateSearchTime = Time.unscaledTime + 0.5f;

            NewBattle.Gameplay.AirdropCratePickup crate =
                UnityEngine.Object.FindFirstObjectByType<NewBattle.Gameplay.AirdropCratePickup>(
                    FindObjectsInactive.Exclude);

            _trackedCrate = crate != null ? crate.transform : null;
        }

        if (_trackedCrate == null)
        {
            _airdropMarker.gameObject.SetActive(false);
            return;
        }

        _airdropMarker.gameObject.SetActive(true);

        float pixelsPerWorldUnit = minimapSize / (_visibleRadius * 2f);
        Vector3 offset = _trackedCrate.position - _localPlayer.position;
        Vector2 pixels = new Vector2(offset.x, offset.z) * pixelsPerWorldUnit;

        float limit = minimapSize * 0.5f - airdropMarkerSize * 0.5f;
        _airdropMarker.anchoredPosition = new Vector2(
            Mathf.Clamp(pixels.x, -limit, limit),
            Mathf.Clamp(pixels.y, -limit, limit));
    }


    /// <summary>
    /// Takim arkadaslarini minimapta mavi nokta olarak gosterir.
    ///
    /// Dusmanlar bilincli olarak gosterilmiyor: minimap bir istihbarat araci degil,
    /// yon bulma araci. Referans oyunda da minimapta sadece kendi takimin gorunur.
    /// </summary>
    private void UpdateTeammateMarkers()
    {
        if (_teammateMarkers == null || _localPlayer == null)
            return;

        float pixelsPerWorldUnit = minimapSize / (_visibleRadius * 2f);
        int used = 0;

        foreach (NewBattle.Gameplay.PlayerPresence presence in NewBattle.Gameplay.PlayerPresence.All)
        {
            if (used >= _teammateMarkers.Length)
                break;

            if (presence == null || presence.Object == null || !presence.Object.IsValid)
                continue;

            PlayerCombatStats stats = presence.GetComponent<PlayerCombatStats>();

            if (NewBattle.Gameplay.LocalPlayerContext.GetRelation(stats)
                != NewBattle.Gameplay.PlayerRelation.Teammate)
                continue;

            Vector3 offset = presence.transform.position - _localPlayer.position;

            // Minimap kapsaminin disindaki takim arkadasi cizilmez; kenara
            // yapistirmak yanlis mesafe hissi verir.
            if (new Vector2(offset.x, offset.z).magnitude > _visibleRadius)
                continue;

            _teammateMarkers[used].gameObject.SetActive(true);
            _teammateMarkers[used].anchoredPosition = new Vector2(offset.x, offset.z) * pixelsPerWorldUnit;
            used++;
        }

        for (int i = used; i < _teammateMarkers.Length; i++)
            _teammateMarkers[i].gameObject.SetActive(false);
    }

    /// <summary>
    /// Hem SU ANKI hem SONRAKI alani cizer.
    ///
    /// Onceki surum sadece sonraki alani gosteriyordu. Oyuncunun asil ihtiyaci
    /// olan bilgi ise su anki sinirin nerede oldugu: alan disinda kalinca can
    /// gidiyor ama haritada bunu gosteren hicbir sey yoktu.
    /// </summary>
    private void UpdateZoneMarkers()
    {
        bool zoneIsSpawned = _safeZone != null && _safeZone.Object != null &&
                             _safeZone.Runner != null && _safeZone.Runner.IsRunning &&
                             _localPlayer != null;

        float pixelsPerWorldUnit = minimapSize / (_visibleRadius * 2f);

        if (_currentZoneRect != null)
        {
            bool showCurrent = zoneIsSpawned && _safeZone.SafeRadius > 0f;
            _currentZoneRect.gameObject.SetActive(showCurrent);

            if (showCurrent)
            {
                Vector3 offset = _safeZone.SafeCenter - _localPlayer.position;
                _currentZoneRect.anchoredPosition =
                    new Vector2(offset.x, offset.z) * pixelsPerWorldUnit;
                float diameter = _safeZone.SafeRadius * 2f * pixelsPerWorldUnit;
                _currentZoneRect.sizeDelta = new Vector2(diameter, diameter);
            }
        }

        if (_nextZoneRect == null)
            return;

        bool showNext = zoneIsSpawned && _safeZone.HasNextZone;
        _nextZoneRect.gameObject.SetActive(showNext);
        if (!showNext)
            return;

        Vector3 nextOffset = _safeZone.NextCenter - _localPlayer.position;
        _nextZoneRect.anchoredPosition = new Vector2(nextOffset.x, nextOffset.z) * pixelsPerWorldUnit;
        float nextDiameter = _safeZone.NextRadius * 2f * pixelsPerWorldUnit;
        _nextZoneRect.sizeDelta = new Vector2(nextDiameter, nextDiameter);
    }

    private void FindLocalPlayer()
    {
        HealthController[] players = UnityEngine.Object.FindObjectsByType<HealthController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (HealthController player in players)
        {
            if (player != null && player.Object != null && player.HasInputAuthority)
            {
                _localPlayer = player.transform;
                return;
            }
        }
    }

    private void OnDestroy()
    {
        if (_renderTexture != null) { _renderTexture.Release(); Destroy(_renderTexture); }
        if (_cameraObject != null) Destroy(_cameraObject);
        if (_uiRoot != null) Destroy(_uiRoot);
        if (_circleMaskSprite != null) Destroy(_circleMaskSprite);
        if (_circleMaskTexture != null) Destroy(_circleMaskTexture);
    }

    /// <summary>
    /// Kare minimapin cercevesi: dort ince serit.
    ///
    /// Tek bir 9-slice sprite yerine dort dikdortgen kullaniyoruz cunku proje
    /// calisma aninda UI kuruyor ve disaridan sprite asset'ine bagimli olmak
    /// istemiyoruz - eksik bir asset sessizce cerceveyi yok ederdi.
    /// </summary>
    private void CreateSquareBorder(RectTransform parent)
    {
        GameObject borderRoot = new("Border", typeof(RectTransform));
        RectTransform rootRect = borderRoot.GetComponent<RectTransform>();
        rootRect.SetParent(parent, false);
        Stretch(rootRect);

        CreateBorderEdge(rootRect, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, borderThickness));
        CreateBorderEdge(rootRect, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, borderThickness));
        CreateBorderEdge(rootRect, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(borderThickness, 0f));
        CreateBorderEdge(rootRect, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(borderThickness, 0f));
    }

    private void CreateBorderEdge(RectTransform parent, string edgeName,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
    {
        GameObject edge = new(edgeName, typeof(RectTransform), typeof(Image));
        RectTransform rect = edge.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;

        Image image = edge.GetComponent<Image>();
        image.color = borderColor;
        image.raycastTarget = false;
    }

    private void CreateMinimapCamera()
    {
        _renderTexture = new RenderTexture(textureResolution, textureResolution, 16)
        {
            name = "RuntimeMinimapTexture",
            antiAliasing = 2
        };
        _renderTexture.Create();

        _cameraObject = new GameObject("MinimapCamera");
        Camera minimapCamera = _cameraObject.AddComponent<Camera>();
        minimapCamera.orthographic = true;
        _visibleRadius = visibleWorldRadius;
        minimapCamera.orthographicSize = _visibleRadius;
        minimapCamera.clearFlags = CameraClearFlags.SolidColor;
        minimapCamera.backgroundColor = mapBackgroundColor;
        minimapCamera.targetTexture = _renderTexture;
        minimapCamera.nearClipPlane = 0.1f;
        minimapCamera.farClipPlane = mapHeight + 20f;

        // Minimap sadece araziyi gostersin. Filtre olmadan minimap butun dusmanlarin
        // yerini ele verirdi - cimende gizlenen oyuncu bile minimapta gorunurdu.
        // Oyuncu isaretleri asagida ayrica, iliskiye gore ciziliyor.
        NewBattle.Gameplay.DropMapCulling culling =
            _cameraObject.AddComponent<NewBattle.Gameplay.DropMapCulling>();

        // Minimapta alan cemberi GEREKLI; sadece oyuncu/bot/loot gizlenir.
        culling.SetHideSafeZone(false);

        MinimapCameraFollow follow = _cameraObject.AddComponent<MinimapCameraFollow>();
        follow.mapHeight = mapHeight;
        _cameraObject.transform.SetPositionAndRotation(new Vector3(0f, mapHeight, 0f), Quaternion.Euler(90f, 0f, 0f));
    }

    private void CreateMinimapUI()
    {
        if (minimapRoot != null)
        {
            AdoptSceneMinimap();
            return;
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("MinimapSystem bir Canvas veya Canvas altinda bulunmali.");
            return;
        }

        _uiRoot = new GameObject("Minimap", typeof(RectTransform), typeof(Image), typeof(Mask));
        RectTransform minimapRect = _uiRoot.GetComponent<RectTransform>();
        minimapRect.SetParent(canvas.transform, false);
        minimapRect.anchorMin = Vector2.one;
        minimapRect.anchorMax = Vector2.one;
        minimapRect.pivot = Vector2.one;
        minimapRect.anchoredPosition = new Vector2(-cornerOffset.x, -cornerOffset.y);
        minimapRect.sizeDelta = new Vector2(minimapSize, minimapSize);

        Image maskImage = _uiRoot.GetComponent<Image>();

        // Kare minimapta maske sprite'i gerekmiyor: duz beyaz bir dikdortgen
        // zaten kareyi keser. Dairesel sprite ancak daire istendiginde uretilir.
        maskImage.sprite = squareMinimap ? null : CreateCircleMaskSprite();
        maskImage.color = Color.white;
        maskImage.raycastTarget = false;
        _uiRoot.GetComponent<Mask>().showMaskGraphic = false;

        GameObject mapImageObject = new("MapImage", typeof(RectTransform), typeof(RawImage));
        RectTransform mapImageRect = mapImageObject.GetComponent<RectTransform>();
        mapImageRect.SetParent(minimapRect, false);
        Stretch(mapImageRect);
        RawImage mapImage = mapImageObject.GetComponent<RawImage>();
        mapImage.texture = _renderTexture;
        mapImage.color = Color.white;
        mapImage.raycastTarget = false;

        GameObject currentZoneObject = new("CurrentZone", typeof(RectTransform), typeof(CircleOutlineGraphic));
        _currentZoneRect = currentZoneObject.GetComponent<RectTransform>();
        _currentZoneRect.SetParent(minimapRect, false);
        _currentZoneRect.anchorMin = new Vector2(0.5f, 0.5f);
        _currentZoneRect.anchorMax = new Vector2(0.5f, 0.5f);
        _currentZoneRect.pivot = new Vector2(0.5f, 0.5f);
        _currentZoneGraphic = currentZoneObject.GetComponent<CircleOutlineGraphic>();
        _currentZoneGraphic.raycastTarget = false;
        _currentZoneGraphic.color = currentZoneColor;
        _currentZoneGraphic.SetThickness(currentZoneLineWidth);

        GameObject nextZoneObject = new("NextZone", typeof(RectTransform), typeof(CircleOutlineGraphic));
        _nextZoneRect = nextZoneObject.GetComponent<RectTransform>();
        _nextZoneRect.SetParent(minimapRect, false);
        _nextZoneRect.anchorMin = new Vector2(0.5f, 0.5f);
        _nextZoneRect.anchorMax = new Vector2(0.5f, 0.5f);
        _nextZoneRect.pivot = new Vector2(0.5f, 0.5f);
        _nextZoneGraphic = nextZoneObject.GetComponent<CircleOutlineGraphic>();
        _nextZoneGraphic.raycastTarget = false;
        _nextZoneGraphic.color = nextZoneColor;
        _nextZoneGraphic.SetThickness(nextZoneLineWidth);

        if (squareMinimap)
        {
            CreateSquareBorder(minimapRect);
        }
        else
        {
            GameObject borderObject = new("Border", typeof(RectTransform), typeof(CircleOutlineGraphic));
            RectTransform borderRect = borderObject.GetComponent<RectTransform>();
            borderRect.SetParent(minimapRect, false);
            Stretch(borderRect);
            CircleOutlineGraphic border = borderObject.GetComponent<CircleOutlineGraphic>();
            border.raycastTarget = false;
            border.color = borderColor;
            border.SetThickness(6f);
        }

        GameObject markerObject = new("LocalPlayerMarker", typeof(RectTransform), typeof(CircleOutlineGraphic));
        RectTransform markerRect = markerObject.GetComponent<RectTransform>();
        markerRect.SetParent(minimapRect, false);
        markerRect.anchorMin = new Vector2(0.5f, 0.5f);
        markerRect.anchorMax = new Vector2(0.5f, 0.5f);
        markerRect.pivot = new Vector2(0.5f, 0.5f);
        markerRect.anchoredPosition = Vector2.zero;
        markerRect.sizeDelta = new Vector2(16f, 16f);
        CircleOutlineGraphic marker = markerObject.GetComponent<CircleOutlineGraphic>();
        marker.raycastTarget = false;
        marker.color = new Color(0.15f, 1f, 0.2f, 1f);
        marker.SetThickness(8f);

        CreateAirdropMarker(minimapRect);

        _minimapRect = minimapRect;
        CreateTeammateMarkers(minimapRect);
    }

    /// <summary>
    /// Tedarik paketi isaretcisi: dolu sari nokta.
    ///
    /// CircleOutlineGraphic'in kalinligi yaricapa esitlenince cember dolu
    /// bir daireye donusuyor; ayri bir sprite'a bagimli olmadan dolu nokta
    /// elde ediyoruz.
    /// </summary>
    private void CreateAirdropMarker(RectTransform parent)
    {
        GameObject markerObject = new("AirdropMarker", typeof(RectTransform), typeof(CircleOutlineGraphic));
        _airdropMarker = markerObject.GetComponent<RectTransform>();
        _airdropMarker.SetParent(parent, false);
        _airdropMarker.anchorMin = new Vector2(0.5f, 0.5f);
        _airdropMarker.anchorMax = new Vector2(0.5f, 0.5f);
        _airdropMarker.pivot = new Vector2(0.5f, 0.5f);
        _airdropMarker.sizeDelta = new Vector2(airdropMarkerSize, airdropMarkerSize);

        CircleOutlineGraphic graphic = markerObject.GetComponent<CircleOutlineGraphic>();
        graphic.raycastTarget = false;
        graphic.color = airdropColor;
        graphic.SetThickness(airdropMarkerSize * 0.5f);

        markerObject.SetActive(false);
    }


    /// <summary>
    /// Sahnede hazir duran minimap nesnesini kullanir.
    ///
    /// Alt nesneleri ADA gore buluyor. Tek tek serilestirilmis alan
    /// yerine ad kullanmanin sebebi: sen sahneyi duzenlerken bir nesneyi
    /// silip yeniden olusturursan serilestirilmis referans kopardi ve
    /// minimap sessizce calismaz olurdu. Ad eslemesi buna dayanikli.
    ///
    /// Bulunamayan parca icin uyari yazilir; sessizce eksik kalmasi,
    /// "neden alan cemberi gorunmuyor" diye aranmaya yol acardi.
    /// </summary>
    private void AdoptSceneMinimap()
    {
        _uiRoot = minimapRoot.gameObject;
        _minimapRect = minimapRoot;

        // Piksel hesaplari bu degere dayaniyor; sen nesneyi Inspector'dan
        // buyutup kucultunce isaretciler de onunla birlikte olceklensin
        // diye gercek genisligi okuyoruz.
        if (minimapRoot.sizeDelta.x > 1f)
            minimapSize = minimapRoot.sizeDelta.x;

        RawImage mapImage = FindChild<RawImage>("MapImage");

        if (mapImage != null)
        {
            mapImage.texture = _renderTexture;

            // Sahnedeki rengi, doku yokken minimapin bos gorunmemesi icin
            // arka plan rengine boyanmis durumda. Doku gelince o renk
            // dokuyu da boyardi; bu yuzden beyaza cekiyoruz.
            mapImage.color = Color.white;
        }
        else
            Debug.LogWarning("MinimapSystem: 'MapImage' bulunamadi, harita gorunmeyecek.");

        _currentZoneGraphic = FindChild<CircleOutlineGraphic>("CurrentZone");
        _currentZoneRect = _currentZoneGraphic != null ? _currentZoneGraphic.rectTransform : null;

        _nextZoneGraphic = FindChild<CircleOutlineGraphic>("NextZone");
        _nextZoneRect = _nextZoneGraphic != null ? _nextZoneGraphic.rectTransform : null;

        Graphic airdrop = FindChild<Graphic>("AirdropMarker");
        _airdropMarker = airdrop != null ? airdrop.rectTransform : null;

        AdoptTeammateMarkers();
    }

    private T FindChild<T>(string childName) where T : Component
    {
        Transform child = minimapRoot.Find(childName);
        return child != null ? child.GetComponent<T>() : null;
    }

    /// <summary>
    /// Takim isaretcilerini sahneden toplar: Teammate_0, Teammate_1, ...
    /// Kac tane koydugun onemli degil, bulundugu kadari kullanilir.
    /// </summary>
    private void AdoptTeammateMarkers()
    {
        System.Collections.Generic.List<RectTransform> found = new();

        for (int i = 0; i < maxTeammateMarkers; i++)
        {
            Transform child = minimapRoot.Find($"Teammate_{i}");

            if (child == null)
                break;

            found.Add(child as RectTransform);
            child.gameObject.SetActive(false);
        }

        _teammateMarkers = found.ToArray();
    }

    private void CreateTeammateMarkers(RectTransform parent)
    {
        _teammateMarkers = new RectTransform[maxTeammateMarkers];

        for (int i = 0; i < maxTeammateMarkers; i++)
        {
            GameObject markerObject = new($"Teammate_{i}", typeof(RectTransform), typeof(CircleOutlineGraphic));
            RectTransform rect = markerObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(teammateMarkerSize, teammateMarkerSize);

            CircleOutlineGraphic graphic = markerObject.GetComponent<CircleOutlineGraphic>();
            graphic.raycastTarget = false;
            graphic.color = teammateColor;
            graphic.SetThickness(teammateMarkerSize * 0.5f);

            markerObject.SetActive(false);
            _teammateMarkers[i] = rect;
        }
    }

    private static void Stretch(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private Sprite CreateCircleMaskSprite()
    {
        const int size = 128;
        _circleMaskTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "RuntimeMinimapCircleMask",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color32[] pixels = new Color32[size * size];
        Vector2 center = new((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radiusSquared = center.x * center.x;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 offset = new Vector2(x, y) - center;
                byte alpha = offset.sqrMagnitude <= radiusSquared ? (byte)255 : (byte)0;
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        _circleMaskTexture.SetPixels32(pixels);
        _circleMaskTexture.Apply(false, true);
        _circleMaskSprite = Sprite.Create(_circleMaskTexture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        _circleMaskSprite.name = "RuntimeMinimapCircleMaskSprite";
        return _circleMaskSprite;
    }
}
