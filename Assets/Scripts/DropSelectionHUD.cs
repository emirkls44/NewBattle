using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DropSelectionHUD : MonoBehaviour
{
    [Header("Hierarchy Referanslari")]
    [SerializeField] private GameObject dropRoot;
    [SerializeField] private RawImage mapImage;
    [SerializeField] private RectTransform selectionMarker;
    [SerializeField] private TextMeshProUGUI playerCountText;
    [SerializeField] private TextMeshProUGUI countdownText;

    [Header("Harita Kamerasi")]
    [SerializeField, Range(256, 1024)] private int textureResolution = 512;
    [SerializeField] private float cameraHeight = 60f;
    [SerializeField, Min(5f)] private float fallbackMapRadius = 29f;

    private DropPhaseController _dropPhase;
    private LobbyCountdownController _lobby;
    private NetworkBootstrap _bootstrap;
    private PlayerController _localPlayer;
    private RenderTexture _mapTexture;
    private GameObject _mapCameraObject;
    private float _nextSearchTime;

    private void Start()
    {
        ResolveHierarchyReferences();

        if (!ReferencesAreValid())
        {
            Debug.LogError(
                "DropSelectionHUD: DropSelection bulunamadi. Play modunu kapatip component menusunden " +
                "'Create Editable Drop Selection In Hierarchy' calistir."
            );
            return;
        }

        CreateMapCamera();
        dropRoot.SetActive(false);
    }

    private void Update()
    {
        ResolveRuntimeReferences();

        bool dropPhaseIsSpawned = _dropPhase != null &&
                                  _dropPhase.Object != null &&
                                  _dropPhase.Runner != null &&
                                  _dropPhase.Runner.IsRunning;

        bool sessionIsRunning = _bootstrap != null && _bootstrap.IsRunning;
        bool gameplayStarted = dropPhaseIsSpawned && _dropPhase.GameplayStarted;
        bool shouldShow = sessionIsRunning && !gameplayStarted;

        if (dropRoot != null)
            dropRoot.SetActive(shouldShow);

        if (!shouldShow)
            return;

        if (!dropPhaseIsSpawned)
        {
            playerCountText.text = "ODA HAZIRLANIYOR";
            countdownText.text = "BAGLANTI BEKLENIYOR";
            return;
        }

        bool lobbyIsSpawned = _lobby != null &&
                              _lobby.Object != null &&
                              _lobby.Runner != null &&
                              _lobby.Runner.IsRunning;

        if (!lobbyIsSpawned)
        {
            playerCountText.text = "ODA HAZIRLANIYOR";
            countdownText.text = "BAGLANTI BEKLENIYOR";
            return;
        }

        playerCountText.text = _lobby.RosterFinalized
            ? $"{_lobby.ConnectedPlayers} OYUNCU + {_lobby.BotPlayers} BOT / {_lobby.MaximumPlayers}"
            : $"{_lobby.ConnectedPlayers} / {_lobby.MaximumPlayers} OYUNCU";

        if (!_lobby.MatchStarted)
        {
            countdownText.text = _lobby.CountdownRunning
                ? (_lobby.RosterFinalized
                    ? $"INIS {Mathf.CeilToInt(_lobby.RemainingSeconds)} SANIYE ICINDE"
                    : $"OYUNCULAR BEKLENIYOR: {Mathf.CeilToInt(_lobby.RemainingSeconds)} sn")
                : "OYUNCULAR BEKLENIYOR";
            return;
        }

        countdownText.text = "INIS BASLIYOR";
    }

    private void OnDestroy()
    {
        if (_mapTexture != null)
        {
            _mapTexture.Release();
            Destroy(_mapTexture);
        }

        if (_mapCameraObject != null)
            Destroy(_mapCameraObject);
    }

    public void SelectDropPoint(Vector2 screenPosition, Camera eventCamera)
    {
        if (_dropPhase == null ||
            _dropPhase.Object == null ||
            _dropPhase.Runner == null ||
            !_dropPhase.Runner.IsRunning ||
            _dropPhase.GameplayStarted ||
            mapImage == null)
            return;

        RectTransform mapRect = mapImage.rectTransform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                mapRect,
                screenPosition,
                eventCamera,
                out Vector2 localPoint))
            return;

        Rect rect = mapRect.rect;
        Vector2 normalized = new(
            Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x),
            Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y)
        );

        Vector2 circlePoint = (normalized - Vector2.one * 0.5f) * 2f;
        circlePoint = Vector2.ClampMagnitude(circlePoint, 0.96f);
        normalized = circlePoint * 0.5f + Vector2.one * 0.5f;

        float radius = _dropPhase != null ? _dropPhase.PlayableMapRadius : fallbackMapRadius;
        Vector3 worldPoint = new(circlePoint.x * radius, 0f, circlePoint.y * radius);

        if (_localPlayer == null)
            ResolveRuntimeReferences(true);

        if (_localPlayer == null)
            return;

        _localPlayer.RequestDropPosition(worldPoint);
        selectionMarker.anchorMin = normalized;
        selectionMarker.anchorMax = normalized;
        selectionMarker.anchoredPosition = Vector2.zero;
        selectionMarker.gameObject.SetActive(true);
    }

    private void ResolveRuntimeReferences(bool force = false)
    {
        if (!force && Time.unscaledTime < _nextSearchTime)
            return;

        _nextSearchTime = Time.unscaledTime + 0.25f;

        _dropPhase ??= UnityEngine.Object.FindFirstObjectByType<DropPhaseController>();
        _lobby ??= UnityEngine.Object.FindFirstObjectByType<LobbyCountdownController>();
        _bootstrap ??= UnityEngine.Object.FindFirstObjectByType<NetworkBootstrap>(FindObjectsInactive.Include);

        if (_localPlayer == null)
        {
            PlayerController[] players = UnityEngine.Object.FindObjectsByType<PlayerController>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );

            foreach (PlayerController player in players)
            {
                if (player != null && player.Object != null && player.HasInputAuthority)
                {
                    _localPlayer = player;
                    break;
                }
            }
        }
    }

    private void CreateMapCamera()
    {
        _mapTexture = new RenderTexture(textureResolution, textureResolution, 16)
        {
            name = "RuntimeDropMapTexture",
            antiAliasing = 2
        };
        _mapTexture.Create();

        _mapCameraObject = new GameObject("DropSelectionCamera");
        Camera mapCamera = _mapCameraObject.AddComponent<Camera>();
        mapCamera.orthographic = true;
        mapCamera.orthographicSize = _dropPhase != null
            ? _dropPhase.PlayableMapRadius
            : fallbackMapRadius;
        mapCamera.clearFlags = CameraClearFlags.SolidColor;
        mapCamera.backgroundColor = new Color(0.08f, 0.25f, 0.30f, 1f);
        mapCamera.targetTexture = _mapTexture;
        mapCamera.nearClipPlane = 0.1f;
        mapCamera.farClipPlane = cameraHeight + 30f;
        _mapCameraObject.transform.SetPositionAndRotation(
            new Vector3(0f, cameraHeight, 0f),
            Quaternion.Euler(90f, 0f, 0f)
        );

        mapImage.texture = _mapTexture;
    }

    private void ResolveHierarchyReferences()
    {
        Transform root = null;

        if (transform.name == "DropSelection")
            root = transform;
        else if (dropRoot != null)
            root = dropRoot.transform;
        else
            root = transform.Find("DropSelection");

        if (root == null)
        {
            Transform[] children = GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children)
            {
                if (child.name != "DropSelection")
                    continue;

                root = child;
                break;
            }
        }

        if (root == null)
            return;

        dropRoot ??= root.gameObject;
        mapImage ??= root.Find("Map")?.GetComponent<RawImage>();
        selectionMarker ??= root.Find("Map/SelectionMarker")?.GetComponent<RectTransform>();
        playerCountText ??= root.Find("PlayerCount")?.GetComponent<TextMeshProUGUI>();
        countdownText ??= root.Find("Countdown")?.GetComponent<TextMeshProUGUI>();

        DropMapClickArea clickArea = mapImage != null
            ? mapImage.GetComponent<DropMapClickArea>()
            : null;
        if (clickArea != null)
            clickArea.SetOwner(this);
    }

    private bool ReferencesAreValid()
    {
        return dropRoot != null &&
               mapImage != null &&
               selectionMarker != null &&
               playerCountText != null &&
               countdownText != null;
    }

    [ContextMenu("Create Editable Drop Selection In Hierarchy")]
    private void CreateEditableDropSelectionInHierarchy()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            Debug.LogWarning("DropSelection olusturmak icin once Play modunu kapat.");
            return;
        }

        ResolveHierarchyReferences();

        Transform existing = dropRoot != null
            ? dropRoot.transform
            : transform.name == "DropSelection"
                ? transform
                : transform.Find("DropSelection");

        if (existing != null)
        {
            Transform existingPlayerCount = existing.Find("PlayerCount");
            playerCountText = existingPlayerCount != null
                ? existingPlayerCount.GetComponent<TextMeshProUGUI>()
                : null;

            if (playerCountText == null)
            {
                playerCountText = CreateText(
                    existing.GetComponent<RectTransform>(),
                    "PlayerCount",
                    new Vector2(-360f, 30f),
                    new Vector2(520f, 55f),
                    "1 / 32 OYUNCU",
                    27f
                );
                UnityEditor.EditorUtility.SetDirty(gameObject);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
                Debug.Log("PlayerCount basariyla DropSelection altinda olusturuldu.");
            }
            else
            {
                Debug.Log("PlayerCount zaten mevcut; Hierarchy'de secildi.");
            }

            UnityEditor.Selection.activeGameObject = playerCountText.gameObject;
            return;
        }

        dropRoot = new GameObject("DropSelection", typeof(RectTransform), typeof(Image));
        RectTransform root = dropRoot.GetComponent<RectTransform>();
        root.SetParent(transform, false);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        Image background = dropRoot.GetComponent<Image>();
        background.color = new Color(0.03f, 0.10f, 0.15f, 0.97f);
        background.raycastTarget = true;

        CreateText(root, "Title", new Vector2(-360f, 165f), new Vector2(500f, 80f), "SAVASA HAZIRLAN", 42f);
        CreateText(root, "Hint", new Vector2(-360f, 100f), new Vector2(520f, 65f), "HARITADA INECEGIN NOKTAYI SEC", 23f);
        playerCountText = CreateText(root, "PlayerCount", new Vector2(-360f, 30f), new Vector2(520f, 55f), "1 / 32 OYUNCU", 27f);
        countdownText = CreateText(root, "Countdown", new Vector2(-360f, -35f), new Vector2(520f, 70f), "OYUNCULAR BEKLENIYOR", 26f);

        GameObject mapObject = new("Map", typeof(RectTransform), typeof(RawImage), typeof(DropMapClickArea));
        RectTransform mapRect = mapObject.GetComponent<RectTransform>();
        mapRect.SetParent(root, false);
        mapRect.anchorMin = new Vector2(0.72f, 0.5f);
        mapRect.anchorMax = new Vector2(0.72f, 0.5f);
        mapRect.pivot = new Vector2(0.5f, 0.5f);
        mapRect.sizeDelta = new Vector2(520f, 520f);
        mapImage = mapObject.GetComponent<RawImage>();
        mapImage.color = Color.white;
        mapImage.raycastTarget = true;
        mapObject.GetComponent<DropMapClickArea>().SetOwner(this);

        GameObject markerObject = new("SelectionMarker", typeof(RectTransform), typeof(Image));
        selectionMarker = markerObject.GetComponent<RectTransform>();
        selectionMarker.SetParent(mapRect, false);
        selectionMarker.anchorMin = new Vector2(0.5f, 0.5f);
        selectionMarker.anchorMax = new Vector2(0.5f, 0.5f);
        selectionMarker.pivot = new Vector2(0.5f, 0.5f);
        selectionMarker.sizeDelta = new Vector2(28f, 28f);
        Image markerImage = markerObject.GetComponent<Image>();
        markerImage.color = new Color(0.2f, 1f, 0.25f, 1f);
        markerImage.raycastTarget = false;
        markerObject.SetActive(false);

        dropRoot.SetActive(false);
        UnityEditor.Undo.RegisterCreatedObjectUndo(dropRoot, "Create Editable Drop Selection");
        UnityEditor.EditorUtility.SetDirty(gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        UnityEditor.Selection.activeGameObject = dropRoot;
#endif
    }

    private static TextMeshProUGUI CreateText(RectTransform parent, string objectName, Vector2 position,
        Vector2 size, string value, float fontSize)
    {
        GameObject textObject = new(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }
}
