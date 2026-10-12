using System.Collections;
using System.Collections.Generic;
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

    [Header("Izleme")]
    [Tooltip("Olunce (ikili modda) altta cikan izleme paneli. Bossa sahnede aranir.")]
    [SerializeField] private NewBattle.UI.SpectatorPanel spectatorPanel;

    [Header("Yeni Mac Sonu Ekrani")]
    [Tooltip("Doluysa sonuc Battlelands tarzi yeni ekranda gosterilir (Tools > NewBattle > Menu > Kur).")]
    [SerializeField] private NewBattle.UI.MatchEndScreen endScreen;

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

    // Olen oyuncu 1.25 sn sonra agdan silinir (PlayerDeadState); ondan sonra da
    // izleme ve mac sonu ekrani calissin diye gereken bilgiler burada tutulur.
    private bool _localDead;
    private int _deathKills;
    private int _localTeamId = int.MinValue;
    private GameObject _characterHolder;
    private GameObject _characterSnapshot;
    private PlayerCombatStats _spectatedStats;
    private bool _leftSpectating;
    private bool _spectatorWired;
    private readonly List<PlayerCombatStats> _spectateTargets = new();

    private RenderTexture _previewTexture;
    private GameObject _previewCharacter;
    private GameObject _previewCameraObject;
    private GameObject _previewLightObject;

    private void Start()
    {
        ResolveHierarchyReferences();

        if (endScreen != null)
        {
            // Yeni ekran kullaniliyor; eski panel gizli kalsin.
            if (resultRoot != null)
                resultRoot.SetActive(false);
            return;
        }

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
        if (!_localDead && IsNetworkObjectReady(_localHealth) && _localHealth.currentHealth <= 0f)
            OnLocalPlayerDied();

        if (_localDead)
        {
            bool matchOver = IsNetworkObjectReady(_match) && _match.MatchEnded;
            if (!matchOver && !_leftSpectating && UpdateSpectating())
                return;

            HideSpectatorPanel();
            ShowResult();
            return;
        }

        if (!IsNetworkObjectReady(_match))
            return;

        if (_match.MatchEnded)
            ShowResult();
    }

    private void OnLocalPlayerDied()
    {
        _localDead = true;
        LocalGameplayStopped = true;
        _localHealth.Runner.ProvideInput = false;
        if (IsNetworkObjectReady(_localStats))
        {
            _localTeamId = _localStats.TeamId;
            // Mac sonu ekrani bunu kullanir: ikilide o ana kadar karakter agdan silinmis olur.
            _deathKills = _localStats.Kills;
        }

        // Mac sonu ekrani bu kopyayi gosterir: ikilide arkadasini izlerken senin
        // karakterin agdan silindigi icin onizleme bos kaliyordu.
        SnapshotLocalCharacter();
    }

    /// <summary>
    /// Ikili modda takim arkadasin hayattaysa izlemeye devam eder. Takim tamamen
    /// elendiyse (ya da solo moddaysa) false doner ve mac sonu ekrani acilir.
    /// </summary>
    private bool UpdateSpectating()
    {
        if (NetworkBootstrap.ActiveMode != BattleGameMode.Squad || _localTeamId == int.MinValue)
            return false;

        CollectSpectateTargets();
        if (_spectateTargets.Count == 0 || !IsTeammate(_spectateTargets[0]))
            return false; // takimda hayatta kimse kalmadi

        if (!_spectateTargets.Contains(_spectatedStats))
            SetSpectated(_spectateTargets[0]); // ilk kez ya da izlenen oldu: takim arkadasina don
        else
            RefreshSpectatorPanel();

        HideLocalControls();
        return true;
    }

    /// <summary>Hayattaki oyuncular; once takim arkadaslari, sonra rakipler (isme gore).</summary>
    private void CollectSpectateTargets()
    {
        _spectateTargets.Clear();
        foreach (PlayerCombatStats stats in UnityEngine.Object.FindObjectsByType<PlayerCombatStats>(FindObjectsSortMode.None))
        {
            if (stats == null || stats == _localStats || !IsNetworkObjectReady(stats))
                continue;

            HealthController health = stats.GetComponent<HealthController>();
            if (IsNetworkObjectReady(health) && health.currentHealth > 0f)
                _spectateTargets.Add(stats);
        }

        _spectateTargets.Sort((a, b) =>
        {
            int team = IsTeammate(b).CompareTo(IsTeammate(a));
            return team != 0 ? team : string.CompareOrdinal(a.NameOrDefault, b.NameOrDefault);
        });
    }

    private bool IsTeammate(PlayerCombatStats stats) => stats != null && stats.TeamId == _localTeamId;

    private void StepSpectated(int direction)
    {
        CollectSpectateTargets();
        if (_spectateTargets.Count == 0)
            return;

        int index = _spectateTargets.IndexOf(_spectatedStats);
        int count = _spectateTargets.Count;
        int next = index < 0 ? 0 : ((index + direction) % count + count) % count;
        SetSpectated(_spectateTargets[next]);
    }

    private void SetSpectated(PlayerCombatStats target)
    {
        _spectatedStats = target;

        if (_spectatorCamera == null)
            _spectatorCamera = UnityEngine.Object.FindFirstObjectByType<CameraFollow>();
        if (_spectatorCamera != null && target != null && _spectated != target.transform)
        {
            _spectatorCamera.SetTarget(target.transform);
            _spectated = target.transform;
        }

        RefreshSpectatorPanel();
    }

    private void RefreshSpectatorPanel()
    {
        NewBattle.UI.SpectatorPanel panel = ResolveSpectatorPanel();
        if (panel == null || _spectatedStats == null)
            return;

        panel.Show(_spectatedStats.NameOrDefault, IsTeammate(_spectatedStats), _spectateTargets.Count > 1);
    }

    private NewBattle.UI.SpectatorPanel ResolveSpectatorPanel()
    {
        if (spectatorPanel == null)
            spectatorPanel = UnityEngine.Object.FindFirstObjectByType<NewBattle.UI.SpectatorPanel>(FindObjectsInactive.Include);

        if (spectatorPanel != null && !_spectatorWired)
        {
            _spectatorWired = true;
            spectatorPanel.StepRequested += StepSpectated;
            spectatorPanel.LeaveRequested += () => _leftSpectating = true;
        }

        return spectatorPanel;
    }

    private void HideSpectatorPanel()
    {
        if (spectatorPanel != null)
            spectatorPanel.Hide();
    }

    /// <summary>
    /// Olum aninda karakterin gorunur kismini gizli bir kopya olarak saklar
    /// (kopya aktif olmayan bir tasiyicinin altinda: calismaz, gorunmez).
    /// </summary>
    private void SnapshotLocalCharacter()
    {
        if (_characterSnapshot != null || _localStats == null)
            return;

        Animator source = _localStats.GetComponentInChildren<Animator>(true);
        if (source == null)
            return;

        _characterHolder = new GameObject("MacSonuKarakterKopyasi");
        _characterHolder.SetActive(false);
        _characterSnapshot = Instantiate(source.gameObject, _characterHolder.transform);
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
        if (endScreen != null)
        {
            ShowEndScreen();
            return;
        }

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
        int kills = IsNetworkObjectReady(_localStats) ? _localStats.Kills : _deathKills;
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
        CreateCharacterPreview(characterPreview);
        StartCoroutine(RevealResult());
    }

    /// <summary>Sonucu yeni mac sonu ekraninda gosterir; oduller orada verilir.</summary>
    private void ShowEndScreen()
    {
        _resultShown = true;
        bool victory = IsVictory();
        LocalGameplayStopped = true;
        HideLocalControls();
        if (IsNetworkObjectReady(_localStats)) _localStats.Runner.ProvideInput = false;

        int kills = IsNetworkObjectReady(_localStats) ? _localStats.Kills : _deathKills;
        int placement = victory ? 1 : Mathf.Max(2, _capturedPlacement);

        LobbyCountdownController lobby = UnityEngine.Object.FindFirstObjectByType<LobbyCountdownController>();
        int total = lobby != null && lobby.Object != null && lobby.Object.IsValid && lobby.TotalContestants > 0
            ? lobby.TotalContestants
            : Mathf.Max(placement, CountAlivePlayers() + 1);

        var outcome = new NewBattle.Progression.MatchOutcome
        {
            Victory = victory,
            Placement = placement,
            Kills = kills,
            TotalPlayers = Mathf.Max(total, placement)
        };

        DisableGameplayCamera();
        var background = new GameObject("ResultDisplayCamera", typeof(Camera));
        _backgroundCamera = background.GetComponent<Camera>();
        _backgroundCamera.cullingMask = 0;
        _backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
        _backgroundCamera.backgroundColor = Color.black;
        _backgroundCamera.targetDisplay = 0;

        CreateCharacterPreview(endScreen.CharacterImage, endScreen.ResultClip(victory));
        endScreen.Show(outcome, BuildEndMessage(victory), ReturnToMainMenu);
    }

    private bool IsVictory()
    {
        return IsNetworkObjectReady(_match) && _match.MatchEnded &&
               (_match.Winner == _localPlayer ||
                (NetworkBootstrap.ActiveMode == BattleGameMode.Squad && IsNetworkObjectReady(_localStats) &&
                 _match.WinnerTeamId == _localStats.TeamId));
    }

    /// <summary>"Dorian seni Tufek ile vurdu!" gibi olum mesaji.</summary>
    private string BuildEndMessage(bool victory)
    {
        if (victory)
            return NetworkBootstrap.ActiveMode == BattleGameMode.Squad
                ? "Takımınla son hayatta kalanlar sizsiniz!"
                : "Son hayatta kalan sensin!";

        if (!IsNetworkObjectReady(_localHealth) || _localHealth.currentHealth > 0f)
            return "Maç sona erdi.";

        if (_localHealth.LastDeathCause == HealthController.DeathCause.Zone)
            return "Güvenli alanın dışında kaldın!";

        string killer = "Bir rakip";
        if (_localHealth.KillerObject.IsValid &&
            _localHealth.Runner.TryFindObject(_localHealth.KillerObject, out NetworkObject killerObject) &&
            killerObject != null &&
            killerObject.TryGetComponent(out PlayerCombatStats killerStats))
        {
            killer = killerStats.NameOrDefault;
        }

        string weapon = _localHealth.KillerWeapon.ToString();
        if (string.IsNullOrEmpty(weapon))
            return $"{killer} seni vurdu!";

        if (weapon == "Yumruk")
            return $"{killer} seni yumrukla devirdi!";

        return $"{killer} seni {weapon} ile vurdu!";
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

    private void CreateCharacterPreview(RawImage target, AnimationClip resultClip = null)
    {
        if (target == null)
            return;

        // Oyuncu hala sahnedeyse onu, agdan silindiyse olum anindaki kopyayi kullan.
        Animator sourceAnimator = _localStats != null ? _localStats.GetComponentInChildren<Animator>(true) : null;
        GameObject source = sourceAnimator != null ? sourceAnimator.gameObject : _characterSnapshot;
        if (source == null)
        {
            Debug.LogWarning("MatchResultHUD: Yerel karakter bulunamadi; onizleme bos kalacak.");
            return;
        }

        Vector3 stage = new(1000f, 1000f, 1000f);
        _previewCharacter = Instantiate(source, stage, Quaternion.identity);
        _previewCharacter.SetActive(true);
        _previewCharacter.name = "ResultCharacterPreview";
        SetLayerRecursively(_previewCharacter, PreviewLayer);

        MonoBehaviour[] scripts = _previewCharacter.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour script in scripts)
        {
            if (script != null)
                Destroy(script);
        }

        // Kazanma / elenme animasyonu.
        if (resultClip != null && _previewCharacter.TryGetComponent(out Animator previewAnimator))
            _previewCharacter.AddComponent<NewBattle.UI.LoopingClipPlayer>().Play(previewAnimator, resultClip);

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
        target.texture = _previewTexture;
        target.color = Color.white;

        _previewCameraObject = new GameObject("MatchResultPreviewCamera", typeof(Camera));
        Camera previewCamera = _previewCameraObject.GetComponent<Camera>();
        previewCamera.cullingMask = 1 << PreviewLayer;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        previewCamera.fieldOfView = 28f;
        previewCamera.nearClipPlane = 0.05f;
        previewCamera.targetTexture = _previewTexture;
        _previewCameraObject.AddComponent<ToonPreviewLight>(); // yuz hep aydinlik

        float height = Mathf.Max(1f, bounds.size.y);
        float distance = height / (2f * Mathf.Tan(previewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad));
        Vector3 lookTarget = bounds.center + Vector3.up * height * 0.03f;
        // Karakter +Z'ye (one) bakiyor: kamera da onune (+Z) gecer. Eskiden
        // -Z'deydi ve karakter sirtini donuyordu.
        previewCamera.transform.position = lookTarget + new Vector3(0f, height * 0.03f, distance * 1.25f);
        previewCamera.transform.LookAt(lookTarget);

        _previewLightObject = new GameObject("MatchResultPreviewLight", typeof(Light));
        Light previewLight = _previewLightObject.GetComponent<Light>();
        previewLight.type = LightType.Directional;
        previewLight.intensity = 1.35f;
        previewLight.cullingMask = 1 << PreviewLayer;
        // Isik da kameranin tarafindan, hafif soldan-yukaridan karakterin yuzune vurur.
        previewLight.transform.rotation = Quaternion.Euler(38f, 148f, 0f);
    }

    private async void ReturnToMainMenu()
    {
        if (_returning)
            return;

        _returning = true;
        if (returnButton != null)
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
        if (_characterHolder != null) Destroy(_characterHolder);
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
