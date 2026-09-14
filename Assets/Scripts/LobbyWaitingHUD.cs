using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyWaitingHUD : MonoBehaviour
{
    [Header("Hierarchy Referanslari")]
    [SerializeField] private GameObject lobbyRoot;
    [SerializeField] private TextMeshProUGUI playerCountText;
    [SerializeField] private TextMeshProUGUI countdownText;

    private LobbyCountdownController _lobby;
    private float _nextSearchTime;

    private void Start()
    {
        ResolveHierarchyReferences();

        if (!ReferencesAreValid())
        {
            Debug.LogError(
                "LobbyWaitingHUD: WaitingLobby bulunamadi. Play modunu kapatip component menusunden " +
                "'Create Editable Waiting Lobby In Hierarchy' calistir."
            );
            return;
        }

        lobbyRoot.SetActive(false);
    }

    private void Update()
    {
        if (_lobby == null && Time.unscaledTime >= _nextSearchTime)
        {
            _lobby = UnityEngine.Object.FindFirstObjectByType<LobbyCountdownController>();
            _nextSearchTime = Time.unscaledTime + 0.25f;
        }

        if (_lobby == null || _lobby.Object == null || _lobby.Runner == null || !_lobby.Runner.IsRunning)
            return;

        if (_lobby.MatchStarted)
        {
            lobbyRoot.SetActive(false);
            return;
        }

        lobbyRoot.SetActive(true);
        playerCountText.text = $"{_lobby.ConnectedPlayers} / {_lobby.MaximumPlayers} OYUNCU";
        countdownText.text = _lobby.CountdownRunning
            ? $"MAC {Mathf.CeilToInt(_lobby.RemainingSeconds)} SANIYE ICINDE BASLIYOR"
            : "OYUNCULAR BEKLENIYOR";
    }

    private void ResolveHierarchyReferences()
    {
        Transform root = transform.name == "WaitingLobby"
            ? transform
            : transform.Find("WaitingLobby");

        if (root == null)
            return;

        lobbyRoot ??= root.gameObject;
        playerCountText ??= root.Find("PlayerCount")?.GetComponent<TextMeshProUGUI>();
        countdownText ??= root.Find("Countdown")?.GetComponent<TextMeshProUGUI>();
    }

    private bool ReferencesAreValid()
    {
        return lobbyRoot != null && playerCountText != null && countdownText != null;
    }

    [ContextMenu("Create Editable Waiting Lobby In Hierarchy")]
    private void CreateEditableWaitingLobbyInHierarchy()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            Debug.LogWarning("WaitingLobby olusturmak icin once Play modunu kapat.");
            return;
        }

        Transform existing = transform.Find("WaitingLobby");
        if (existing != null)
        {
            ResolveHierarchyReferences();
            UnityEditor.Selection.activeGameObject = existing.gameObject;
            return;
        }

        lobbyRoot = new GameObject("WaitingLobby", typeof(RectTransform), typeof(Image));
        RectTransform root = lobbyRoot.GetComponent<RectTransform>();
        root.SetParent(transform, false);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        Image background = lobbyRoot.GetComponent<Image>();
        background.color = new Color(0.045f, 0.12f, 0.18f, 0.96f);
        background.raycastTarget = true;

        CreateText(root, "Title", new Vector2(0f, 145f), new Vector2(900f, 80f), "SAVAS ALANI HAZIRLANIYOR", 42f);
        playerCountText = CreateText(root, "PlayerCount", new Vector2(0f, 48f), new Vector2(600f, 72f), "1 / 32 OYUNCU", 36f);
        countdownText = CreateText(root, "Countdown", new Vector2(0f, -32f), new Vector2(800f, 55f), "OYUNCULAR BEKLENIYOR", 23f);
        CreateText(root, "Hint", new Vector2(0f, -145f), new Vector2(900f, 45f), "SON HAYATTA KALAN KAZANIR", 18f);

        lobbyRoot.SetActive(false);
        UnityEditor.Undo.RegisterCreatedObjectUndo(lobbyRoot, "Create Editable Waiting Lobby");
        UnityEditor.EditorUtility.SetDirty(gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        UnityEditor.Selection.activeGameObject = lobbyRoot;
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
