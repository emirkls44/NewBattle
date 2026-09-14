using TMPro;
using UnityEngine;

public class BattleStatusHUD : MonoBehaviour
{
    [Header("Hierarchy Referanslari")]
    [SerializeField] private TextMeshProUGUI aliveText;
    [SerializeField] private TextMeshProUGUI killsText;

    [Header("Yazilar")]
    [SerializeField] private string alivePrefix = "KALAN";
    [SerializeField] private string killsPrefix = "SKOR";

    private PlayerCombatStats _localStats;
    private float _nextRefreshTime;

    private void Start()
    {
        ResolveHierarchyReferences();

        if (!ReferencesAreValid())
        {
            Debug.LogError(
                "BattleStatusHUD: BattleStatus bulunamadi. Play modunu kapatip component menusunden " +
                "'Create Editable Battle Status In Hierarchy' calistir."
            );
        }
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextRefreshTime)
            return;

        _nextRefreshTime = Time.unscaledTime + 0.2f;

        if (_localStats == null)
            FindLocalStats();

        Refresh();
    }

    private void FindLocalStats()
    {
        PlayerCombatStats[] allStats = UnityEngine.Object.FindObjectsByType<PlayerCombatStats>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        foreach (PlayerCombatStats stats in allStats)
        {
            if (stats == null || stats.Object == null || !stats.HasInputAuthority)
                continue;

            _localStats = stats;
            return;
        }
    }

    private void Refresh()
    {
        if (!ReferencesAreValid())
            return;

        HealthController[] players = UnityEngine.Object.FindObjectsByType<HealthController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        int aliveCount = 0;
        foreach (HealthController player in players)
        {
            if (player != null && player.Object != null && player.currentHealth > 0f)
                aliveCount++;
        }

        int kills = _localStats != null ? _localStats.Kills : 0;
        aliveText.text = $"{alivePrefix}  {aliveCount}";
        killsText.text = $"{killsPrefix}  {kills}";
    }

    private void ResolveHierarchyReferences()
    {
        Transform root = transform.name == "BattleStatus"
            ? transform
            : transform.Find("BattleStatus");

        if (root == null)
            return;

        aliveText ??= root.Find("AliveCount")?.GetComponent<TextMeshProUGUI>();
        killsText ??= root.Find("KillCount")?.GetComponent<TextMeshProUGUI>();
    }

    private bool ReferencesAreValid()
    {
        return aliveText != null && killsText != null;
    }

    [ContextMenu("Create Editable Battle Status In Hierarchy")]
    private void CreateEditableBattleStatusInHierarchy()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            Debug.LogWarning("BattleStatus olusturmak icin once Play modunu kapat.");
            return;
        }

        Transform existing = transform.Find("BattleStatus");
        if (existing != null)
        {
            ResolveHierarchyReferences();
            UnityEditor.Selection.activeGameObject = existing.gameObject;
            Debug.Log("BattleStatus zaten Hierarchy'de bulunuyor.");
            return;
        }

        GameObject rootObject = CreateUiObject("BattleStatus", transform);
        RectTransform root = rootObject.GetComponent<RectTransform>();
        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.anchoredPosition = new Vector2(18f, -18f);
        root.sizeDelta = new Vector2(245f, 42f);

        aliveText = CreateText(root, "AliveCount", new Vector2(0f, 0f), new Vector2(120f, 42f), "KALAN  6");
        killsText = CreateText(root, "KillCount", new Vector2(125f, 0f), new Vector2(120f, 42f), "SKOR  0");

        UnityEditor.Undo.RegisterCreatedObjectUndo(rootObject, "Create Editable Battle Status");
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

    private static TextMeshProUGUI CreateText(
        RectTransform parent,
        string objectName,
        Vector2 position,
        Vector2 size,
        string value)
    {
        GameObject textObject = CreateUiObject(objectName, parent);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.fontSize = 20f;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }
}
