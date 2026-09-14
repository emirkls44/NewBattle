using System.Collections;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MatchResultHUD : MonoBehaviour
{
    private const int PreviewLayer = 30;
    public static bool LocalGameplayStopped { get; private set; }
    private CameraFollow _spectatorCamera;
    private Transform _spectated;
    private Camera _backgroundCamera;
    private void Awake() { LocalGameplayStopped = false; }

    [Header("Hierarchy Referanslari")]
    [SerializeField] private GameObject resultRoot;
    [SerializeField] private Image panel;
    [SerializeField] private RawImage characterPreview;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI placementText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private Button returnButton;
    [SerializeField] private TextMeshProUGUI returnButtonText;

    [Header("Sonuc Ayarlari")]
    [SerializeField, Min(3f)] private float returnButtonDelay = 3f;
    [SerializeField] private Color backgroundColor = new(0.025f, 0.04f, 0.065f, 1f);
    [SerializeField] private Color victoryColor = new(0.35f, 0.95f, 0.25f, 1f);
    [SerializeField] private Color defeatColor = new(1f, 0.3f, 0.22f, 1f);

    private BattleMatchController _match;
    private PlayerRef _localPlayer = PlayerRef.None;
    private PlayerCombatStats _localStats;
    private HealthController _localHealth;
    private Vector3 _baseScale = Vector3.one;
    private bool _resultShown;
    private bool _returning;
    private int _capturedPlacement;
    private float _nextSearchTime;

    private RenderTexture _previewTexture;
    private GameObject _previewCharacter;
    private GameObject _previewCameraObject;
    private GameObject _previewLightObject;

    private void Start()
    {
        ResolveHierarchyReferences();

        if (!ReferencesAreValid())
        {
            Debug.LogError(
                "MatchResultHUD eksik. Game_UI uzerindeki Match Result HUD component menusunden " +
                "'Create Or Upgrade Match Result In Hierarchy' secenegini calistir."
            );
            return;
        }

        ConfigureResultCanvas();
        _baseScale = resultRoot.transform.localScale;
        returnButton.onClick.RemoveAllListeners();
        returnButton.onClick.AddListener(ReturnToMainMenu);
        returnButton.gameObject.SetActive(false);
        resultRoot.SetActive(false);
    }

    private void Update()
    {
        if (_resultShown)
            return;

        if (Time.unscaledTime >= _nextSearchTime)
        {
            FindRuntimeReferences();
            _nextSearchTime = Time.unscaledTime + 0.2f;
        }

        CapturePlacementWhenEliminated();
        if (IsNetworkObjectReady(_localHealth) && _localHealth.currentHealth <= 0f)
        {
            LocalGameplayStopped = true;
            _localHealth.Runner.ProvideInput = false;
            if (NetworkBootstrap.ActiveMode == BattleGameMode.Squad)
            {
                Transform teammate = FindLivingTeammate();
                if (teammate != null && !(IsNetworkObjectReady(_match) && _match.MatchEnded))
                {
                    if (_spectatorCamera == null)
                        _spectatorCamera = UnityEngine.Object.FindFirstObjectByType<CameraFollow>();
                    if (_spectatorCamera != null && _spectated != teammate)
                    {
                        _spectatorCamera.SetTarget(teammate);
                        _spectated = teammate;
                    }
                    HideLocalControls();
                    return;
                }
            }
            ShowResult();
            return;
        }

        if (!IsNetworkObjectReady(_match))
            return;

        if (_match.MatchEnded)
            ShowResult();
    }

    private Transform FindLivingTeammate()
    {
        if (!IsNetworkObjectReady(_localStats)) return null;
        foreach (var stats in UnityEngine.Object.FindObjectsByType<PlayerCombatStats>(FindObjectsSortMode.None))
        {
            if (stats == _localStats || !IsNetworkObjectReady(stats) || stats.Runner != _localStats.Runner) continue;
            var health = stats.GetComponent<HealthController>();
            if (_localStats.IsTeammate(stats) && IsNetworkObjectReady(health) && health.currentHealth > 0f)
                return stats.transform;
        }
        return null;
    }

    private void HideLocalControls()
    {
        foreach (var joystick in GetComponentsInChildren<VirtualJoystick>(true))
            joystick.gameObject.SetActive(false);
    }

    private void FindRuntimeReferences()
    {
        if (_match == null)
            _match = UnityEngine.Object.FindFirstObjectByType<BattleMatchController>();

        if (_localStats != null)
            return;

        PlayerCombatStats[] allStats = UnityEngine.Object.FindObjectsByType<PlayerCombatStats>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        foreach (PlayerCombatStats stats in allStats)
        {
            if (stats == null || stats.Object == null || !stats.HasInputAuthority)
                continue;

            _localPlayer = stats.Object.InputAuthority;
            _localStats = stats;
            _localHealth = stats.GetComponent<HealthController>();
            return;
        }
    }

    private void CapturePlacementWhenEliminated()
    {
        if (_capturedPlacement > 0 || !IsNetworkObjectReady(_localHealth))
            return;

        if (_localHealth.currentHealth > 0f)
            return;

        _capturedPlacement = CountAlivePlayers() + 1;
    }

    private static bool IsNetworkObjectReady(NetworkBehaviour behaviour)
    {
        return behaviour != null && behaviour.Object != null && behaviour.Object.IsValid &&
               behaviour.Runner != null && behaviour.Runner.IsRunning;
    }

    private static int CountAlivePlayers()
    {
        HealthController[] players = UnityEngine.Object.FindObjectsByType<HealthController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        int alive = 0;
        foreach (HealthController player in players)
        {
            if (IsNetworkObjectReady(player) && player.currentHealth > 0f)
                alive++;
        }

        return alive;
    }

    private void ShowResult()
    {
        if (!ReferencesAreValid())
            return;

        _resultShown = true;
        bool victory = IsNetworkObjectReady(_match) && _match.MatchEnded &&
            (_match.Winner == _localPlayer ||
             (NetworkBootstrap.ActiveMode == BattleGameMode.Squad && IsNetworkObjectReady(_localStats) &&
              _match.WinnerTeamId == _localStats.TeamId));
        LocalGameplayStopped = true;
        HideLocalControls();
        if (IsNetworkObjectReady(_localStats)) _localStats.Runner.ProvideInput = false;
        int kills = IsNetworkObjectReady(_localStats) ? _localStats.Kills : 0;
        int placement = victory ? 1 : Mathf.Max(2, _capturedPlacement);

        titleText.text = victory ? "ZAFER!" : "ELENDIN";
        titleText.color = victory ? victoryColor : defeatColor;
        placementText.text = $"SIRALAMA  #{placement}";
        scoreText.text = $"SKOR  {kills}";

        panel.color = new Color(backgroundColor.r, backgroundColor.g, backgroundColor.b, 1f);
        resultRoot.SetActive(true);
        resultRoot.transform.SetAsLastSibling();
        resultRoot.transform.localScale = _baseScale;
        returnButton.gameObject.SetActive(false);

        ConfigureResultCanvas();
        DisableGameplayCamera();
        var background = new GameObject("ResultDisplayCamera", typeof(Camera));
        _backgroundCamera = background.GetComponent<Camera>();
        _backgroundCamera.cullingMask = 0;
        _backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
        _backgroundCamera.backgroundColor = panel.color;
        _backgroundCamera.targetDisplay = 0;
        CreateCharacterPreview();
        StartCoroutine(RevealResult());
    }

    private IEnumerator RevealResult()
    {
        const float revealDuration = 0.32f;
        float elapsed = 0f;

        while (elapsed < revealDuration)
        {
            float progress = elapsed / revealDuration;
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            resultRoot.transform.localScale = Vector3.LerpUnclamped(_baseScale, _baseScale, eased);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        resultRoot.transform.localScale = _baseScale;
        float remaining = Mathf.Max(3f, returnButtonDelay) - revealDuration;
        if (remaining > 0f)
            yield return new WaitForSecondsRealtime(remaining);

        returnButton.gameObject.SetActive(true);
        returnButton.interactable = true;
    }

    private void DisableGameplayCamera()
    {
        Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        foreach (Camera camera in cameras)
        {
            if (camera != null && camera.CompareTag("MainCamera"))
                camera.enabled = false;
        }
    }

    private void CreateCharacterPreview()
    {
        if (_localStats == null || characterPreview == null)
            return;

        Animator sourceAnimator = _localStats.GetComponentInChildren<Animator>(true);
        if (sourceAnimator == null)
        {
            Debug.LogWarning("MatchResultHUD: Yerel karakterde Animator bulunamadi; onizleme bos kalacak.");
            return;
        }

        Vector3 stage = new(1000f, 1000f, 1000f);
        _previewCharacter = Instantiate(sourceAnimator.gameObject, stage, Quaternion.identity);
        _previewCharacter.name = "ResultCharacterPreview";
        SetLayerRecursively(_previewCharacter, PreviewLayer);

        MonoBehaviour[] scripts = _previewCharacter.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour script in scripts)
        {
            if (script != null)
                Destroy(script);
        }

        Renderer[] renderers = _previewCharacter.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Vector3 groundingOffset = stage - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        _previewCharacter.transform.position += groundingOffset;

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        _previewTexture = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32)
        {
            name = "MatchResultCharacterTexture",
            antiAliasing = 2
        };
        _previewTexture.Create();
        characterPreview.texture = _previewTexture;
        characterPreview.color = Color.white;

        _previewCameraObject = new GameObject("MatchResultPreviewCamera", typeof(Camera));
        Camera previewCamera = _previewCameraObject.GetComponent<Camera>();
        previewCamera.cullingMask = 1 << PreviewLayer;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        previewCamera.fieldOfView = 28f;
        previewCamera.nearClipPlane = 0.05f;
        previewCamera.targetTexture = _previewTexture;

        float height = Mathf.Max(1f, bounds.size.y);
        float distance = height / (2f * Mathf.Tan(previewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad));
        Vector3 lookTarget = bounds.center + Vector3.up * height * 0.03f;
        previewCamera.transform.position = lookTarget + new Vector3(0f, height * 0.03f, -distance * 1.25f);
        previewCamera.transform.LookAt(lookTarget);

        _previewLightObject = new GameObject("MatchResultPreviewLight", typeof(Light));
        Light previewLight = _previewLightObject.GetComponent<Light>();
        previewLight.type = LightType.Directional;
        previewLight.intensity = 1.35f;
        previewLight.cullingMask = 1 << PreviewLayer;
        previewLight.transform.rotation = Quaternion.Euler(38f, -32f, 0f);
    }

    private async void ReturnToMainMenu()
    {
        if (_returning)
            return;

        _returning = true;
        returnButton.interactable = false;
        if (returnButtonText != null)
            returnButtonText.text = "DONULUYOR...";

        int sceneIndex = SceneManager.GetActiveScene().buildIndex;
        NetworkRunner runner = _match != null ? _match.Runner : null;
        runner ??= UnityEngine.Object.FindFirstObjectByType<NetworkRunner>();

        if (runner != null && runner.IsRunning)
            await runner.Shutdown(true);

        SceneManager.LoadScene(sceneIndex);
    }

    private void ConfigureResultCanvas()
    {
        Canvas canvas = resultRoot.GetComponent<Canvas>();
        if (canvas == null)
            canvas = resultRoot.AddComponent<Canvas>();

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 2000;

        if (resultRoot.GetComponent<GraphicRaycaster>() == null)
            resultRoot.AddComponent<GraphicRaycaster>();
    }

    private void ResolveHierarchyReferences()
    {
        Transform root = transform.name == "MatchResult" ? transform : transform.Find("MatchResult");
        if (root == null)
            return;

        resultRoot ??= root.gameObject;
        panel ??= root.GetComponent<Image>();
        characterPreview ??= root.Find("CharacterPreview")?.GetComponent<RawImage>();
        titleText ??= root.Find("Title")?.GetComponent<TextMeshProUGUI>();
        placementText ??= root.Find("Placement")?.GetComponent<TextMeshProUGUI>();
        scoreText ??= root.Find("Score")?.GetComponent<TextMeshProUGUI>();
        returnButton ??= root.Find("ReturnButton")?.GetComponent<Button>();
        returnButtonText ??= root.Find("ReturnButton/Label")?.GetComponent<TextMeshProUGUI>();
    }

    private bool ReferencesAreValid()
    {
        return resultRoot != null && panel != null && characterPreview != null &&
               titleText != null && placementText != null && scoreText != null &&
               returnButton != null && returnButtonText != null;
    }

    private void OnDestroy()
    {
        LocalGameplayStopped = false;
        if (_backgroundCamera != null) Destroy(_backgroundCamera.gameObject);
        if (_previewCharacter != null) Destroy(_previewCharacter);
        if (_previewCameraObject != null) Destroy(_previewCameraObject);
        if (_previewLightObject != null) Destroy(_previewLightObject);

        if (_previewTexture != null)
        {
            _previewTexture.Release();
            Destroy(_previewTexture);
        }
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    [ContextMenu("Create Or Upgrade Match Result In Hierarchy")]
    private void CreateOrUpgradeMatchResultInHierarchy()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            Debug.LogWarning("MatchResult duzenlemek icin Play modunu kapat.");
            return;
        }

        Transform existing = transform.Find("MatchResult");
        if (existing == null)
        {
            resultRoot = new GameObject("MatchResult", typeof(RectTransform), typeof(Image));
            resultRoot.transform.SetParent(transform, false);
            UnityEditor.Undo.RegisterCreatedObjectUndo(resultRoot, "Create Match Result");
        }
        else
        {
            resultRoot = existing.gameObject;
            if (resultRoot.GetComponent<Image>() == null)
                resultRoot.AddComponent<Image>();
        }

        RectTransform root = resultRoot.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = Vector2.zero;
        root.sizeDelta = Vector2.zero;
        root.localScale = Vector3.one;

        panel = resultRoot.GetComponent<Image>();
        panel.color = new Color(backgroundColor.r, backgroundColor.g, backgroundColor.b, 1f);
        panel.raycastTarget = true;

        characterPreview = GetOrCreateRawImage(root, "CharacterPreview");
        SetRect(characterPreview.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -55f), new Vector2(570f, 610f));
        characterPreview.raycastTarget = false;

        titleText = GetOrCreateText(root, "Title", "ZAFER!", 64f);
        SetRect(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -105f), new Vector2(760f, 90f));

        Transform oldDetail = root.Find("Detail");
        if (root.Find("Placement") == null && oldDetail != null)
            oldDetail.name = "Placement";

        placementText = GetOrCreateText(root, "Placement", "SIRALAMA  #1", 31f);
        SetRect(placementText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -185f), new Vector2(620f, 55f));

        scoreText = GetOrCreateText(root, "Score", "SKOR  0", 28f);
        SetRect(scoreText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -235f), new Vector2(620f, 50f));

        returnButton = GetOrCreateButton(root, "ReturnButton", "ANA MENUYE DON");
        RectTransform buttonRect = returnButton.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 0f);
        buttonRect.anchorMax = new Vector2(1f, 0f);
        buttonRect.pivot = new Vector2(1f, 0f);
        buttonRect.anchoredPosition = new Vector2(-42f, 36f);
        buttonRect.sizeDelta = new Vector2(310f, 72f);
        returnButtonText = returnButton.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();

        resultRoot.SetActive(false);
        UnityEditor.EditorUtility.SetDirty(gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        UnityEditor.Selection.activeGameObject = resultRoot;
        Debug.Log("MatchResult tam ekran sonuc yapisina yukseltilip Hierarchy'ye yerlestirildi.");
#endif
    }

#if UNITY_EDITOR
    private static RawImage GetOrCreateRawImage(RectTransform parent, string objectName)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
            return existing.GetComponent<RawImage>() ?? existing.gameObject.AddComponent<RawImage>();

        GameObject created = new(objectName, typeof(RectTransform), typeof(RawImage));
        created.transform.SetParent(parent, false);
        return created.GetComponent<RawImage>();
    }

    private static TextMeshProUGUI GetOrCreateText(RectTransform parent, string objectName, string value, float fontSize)
    {
        Transform existing = parent.Find(objectName);
        TextMeshProUGUI text;

        if (existing != null)
            text = existing.GetComponent<TextMeshProUGUI>() ?? existing.gameObject.AddComponent<TextMeshProUGUI>();
        else
        {
            GameObject created = new(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            created.transform.SetParent(parent, false);
            text = created.GetComponent<TextMeshProUGUI>();
        }

        text.text = value;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static Button GetOrCreateButton(RectTransform parent, string objectName, string label)
    {
        Transform existing = parent.Find(objectName);
        GameObject buttonObject;

        if (existing != null)
            buttonObject = existing.gameObject;
        else
        {
            buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
        }

        Image image = buttonObject.GetComponent<Image>() ?? buttonObject.AddComponent<Image>();
        image.color = new Color(0.25f, 0.82f, 0.3f, 1f);
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>() ?? buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        TextMeshProUGUI text = GetOrCreateText(buttonObject.GetComponent<RectTransform>(), "Label", label, 23f);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        return button;
    }

    private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
#endif
}
