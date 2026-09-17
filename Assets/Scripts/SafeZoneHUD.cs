using TMPro;
using UnityEngine;

public class SafeZoneHUD : MonoBehaviour
{
    [Header("Hierarchy Referanslari")]
    [SerializeField] private RectTransform zoneCounterRoot;
    [SerializeField] private CircleOutlineGraphic circleOutline;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Gorunum")]
    [SerializeField] private Color waitingColor = new(1f, 1f, 1f, 1f);
    [SerializeField] private Color shrinkingColor = new(1f, 0.24f, 0.42f, 1f);
    [SerializeField] private Color finalColor = new(1f, 0.68f, 0.1f, 1f);
    [SerializeField, Min(1f)] private float lastSecondsPulseScale = 1.12f;

    private SafeZoneController _safeZone;
    private DropPhaseController _dropPhase;
    private Vector3 _baseScale = Vector3.one;
    private float _nextSearchTime;

    private void Start()
    {
        ResolveHierarchyReferences();
        if (zoneCounterRoot != null)
            _baseScale = zoneCounterRoot.localScale;

        if (!ReferencesAreValid())
            Debug.LogError("SafeZoneHUD: ZoneCounter bulunamadi. Component menusunden 'Create Editable Zone Counter In Hierarchy' calistir.");
    }

    private void Update()
    {
        if (Time.unscaledTime >= _nextSearchTime)
        {
            _safeZone ??= UnityEngine.Object.FindFirstObjectByType<SafeZoneController>();
            _dropPhase ??= UnityEngine.Object.FindFirstObjectByType<DropPhaseController>();
            _nextSearchTime = Time.unscaledTime + 0.25f;
        }

        Refresh();
    }

    private void Refresh()
    {
        if (!ReferencesAreValid())
            return;

        bool safeZoneIsSpawned = _safeZone != null && _safeZone.Object != null &&
                                 _safeZone.Runner != null && _safeZone.Runner.IsRunning;
        bool gameplayIsRunning = _dropPhase != null && _dropPhase.Object != null &&
                                 _dropPhase.Runner != null && _dropPhase.Runner.IsRunning &&
                                 _dropPhase.GameplayStarted;

        zoneCounterRoot.gameObject.SetActive(safeZoneIsSpawned && gameplayIsRunning);
        if (!safeZoneIsSpawned || !gameplayIsRunning)
            return;

        if (_safeZone.IsFinalZone)
        {
            timerText.text = "!";
            statusText.text = "SON ALAN";
            circleOutline.color = finalColor;
            zoneCounterRoot.localScale = _baseScale;
            return;
        }

        float remaining = _safeZone.RemainingPhaseSeconds;
        timerText.text = Mathf.CeilToInt(remaining).ToString();
        string stage = $"{_safeZone.StageNumber}/{_safeZone.StageCount}";

        if (_safeZone.IsWaitingForShrink)
        {
            statusText.text = $"ALAN {stage} DARALACAK";
            circleOutline.color = waitingColor;
        }
        else
        {
            statusText.text = $"ALAN {stage} DARALIYOR";
            circleOutline.color = shrinkingColor;
        }

        if (remaining <= 5f)
        {
            float pulse = (Mathf.Sin(Time.unscaledTime * 10f) + 1f) * 0.5f;
            zoneCounterRoot.localScale = _baseScale * Mathf.Lerp(1f, lastSecondsPulseScale, pulse);
        }
        else
        {
            zoneCounterRoot.localScale = _baseScale;
        }
    }

    private void ResolveHierarchyReferences()
    {
        Transform root = transform.name == "ZoneCounter" ? transform : transform.Find("ZoneCounter");
        if (root == null)
            return;

        zoneCounterRoot ??= root.GetComponent<RectTransform>();
        circleOutline ??= root.Find("Circle")?.GetComponent<CircleOutlineGraphic>();
        timerText ??= root.Find("Circle/Timer")?.GetComponent<TextMeshProUGUI>();
        statusText ??= root.Find("Status")?.GetComponent<TextMeshProUGUI>();
    }

    private bool ReferencesAreValid()
    {
        return zoneCounterRoot != null && circleOutline != null && timerText != null && statusText != null;
    }

    [ContextMenu("Create Editable Zone Counter In Hierarchy")]
    private void CreateEditableZoneCounterInHierarchy()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            Debug.LogWarning("ZoneCounter olusturmak icin once Play modunu kapat.");
            return;
        }

        Transform existing = transform.Find("ZoneCounter");
        if (existing != null)
        {
            ResolveHierarchyReferences();
            UnityEditor.Selection.activeGameObject = existing.gameObject;
            return;
        }

        GameObject rootObject = CreateUiObject("ZoneCounter", transform);
        zoneCounterRoot = rootObject.GetComponent<RectTransform>();
        zoneCounterRoot.anchorMin = new Vector2(0.5f, 1f);
        zoneCounterRoot.anchorMax = new Vector2(0.5f, 1f);
        zoneCounterRoot.pivot = new Vector2(0.5f, 1f);
        zoneCounterRoot.anchoredPosition = new Vector2(0f, -22f);
        zoneCounterRoot.sizeDelta = new Vector2(220f, 112f);

        GameObject circleObject = CreateUiObject("Circle", zoneCounterRoot);
        RectTransform circleRect = circleObject.GetComponent<RectTransform>();
        circleRect.anchorMin = new Vector2(0.5f, 1f);
        circleRect.anchorMax = new Vector2(0.5f, 1f);
        circleRect.pivot = new Vector2(0.5f, 1f);
        circleRect.anchoredPosition = Vector2.zero;
        circleRect.sizeDelta = new Vector2(78f, 78f);

        circleOutline = circleObject.AddComponent<CircleOutlineGraphic>();
        circleOutline.color = waitingColor;
        circleOutline.SetThickness(9f);
        circleOutline.raycastTarget = false;

        timerText = CreateText(circleRect, "Timer", Vector2.zero, new Vector2(72f, 72f), "10", 32f);
        statusText = CreateText(zoneCounterRoot, "Status", new Vector2(0f, -88f), new Vector2(220f, 24f), "ALAN 1/5 DARALACAK", 15f);

        UnityEditor.Undo.RegisterCreatedObjectUndo(rootObject, "Create Editable Zone Counter");
        UnityEditor.EditorUtility.SetDirty(gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        UnityEditor.Selection.activeGameObject = rootObject;
#endif
    }

    private static GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject created = new(objectName, typeof(RectTransform));
        created.transform.SetParent(parent, false);
        return created;
    }

    private static TextMeshProUGUI CreateText(RectTransform parent, string objectName, Vector2 position, Vector2 size, string value, float fontSize)
    {
        GameObject textObject = CreateUiObject(objectName, parent);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }
}
