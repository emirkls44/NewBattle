using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NetworkMenuUI : MonoBehaviour
{
    [Header("Hierarchy Referanslari")]
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private Button playButton;
    [SerializeField] private TextMeshProUGUI statusText;

    private NetworkBootstrap _bootstrap;

    private void Start()
    {
        ResolveHierarchyReferences();
        _bootstrap = UnityEngine.Object.FindFirstObjectByType<NetworkBootstrap>(FindObjectsInactive.Include);

        if (!ReferencesAreValid())
        {
            Debug.LogError(
                "NetworkMenuUI: MainMenu bulunamadi. Play modunu kapatip component menusunden " +
                "'Create Editable Main Menu In Hierarchy' calistir."
            );
            return;
        }

        if (_bootstrap == null)
        {
            statusText.text = "NETWORK BOOTSTRAP BULUNAMADI";
            playButton.interactable = false;
            return;
        }

        playButton.onClick.AddListener(Play);
        _bootstrap.StatusChanged += SetStatus;
        _bootstrap.ConnectionCompleted += HandleConnectionCompleted;

        if (_bootstrap.IsRunning)
            menuRoot.SetActive(false);
        else
            SetStatus("HAZIR");
    }

    private void OnDestroy()
    {
        if (playButton != null)
            playButton.onClick.RemoveListener(Play);

        if (_bootstrap != null)
        {
            _bootstrap.StatusChanged -= SetStatus;
            _bootstrap.ConnectionCompleted -= HandleConnectionCompleted;
        }
    }

    private void Play()
    {
        if (_bootstrap == null)
            return;

        playButton.interactable = false;
        SetStatus("ESLESME ARANIYOR...");
        _bootstrap.StartMatchmaking();
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    private void HandleConnectionCompleted(bool success)
    {
        if (success)
        {
            menuRoot.SetActive(false);
            return;
        }

        playButton.interactable = true;
    }

    private void ResolveHierarchyReferences()
    {
        Transform root = transform.name == "MainMenu"
            ? transform
            : transform.Find("MainMenu");

        if (root == null)
            return;

        menuRoot ??= root.gameObject;
        playButton ??= root.Find("PlayButton")?.GetComponent<Button>();
        statusText ??= root.Find("Status")?.GetComponent<TextMeshProUGUI>();
    }

    private bool ReferencesAreValid()
    {
        return menuRoot != null && playButton != null && statusText != null;
    }

    [ContextMenu("Create Editable Main Menu In Hierarchy")]
    private void CreateEditableMainMenuInHierarchy()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            Debug.LogWarning("MainMenu olusturmak icin once Play modunu kapat.");
            return;
        }

        Transform existing = transform.Find("MainMenu");
        if (existing != null)
        {
            ResolveHierarchyReferences();
            UnityEditor.Selection.activeGameObject = existing.gameObject;
            return;
        }

        menuRoot = new GameObject("MainMenu", typeof(RectTransform), typeof(Image));
        RectTransform root = menuRoot.GetComponent<RectTransform>();
        root.SetParent(transform, false);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        Image background = menuRoot.GetComponent<Image>();
        background.color = new Color(0.055f, 0.16f, 0.25f, 1f);
        background.raycastTarget = true;

        CreateText(root, "Title", new Vector2(0f, 170f), new Vector2(900f, 120f), "NEW BATTLE", 72f);
        CreateText(root, "Subtitle", new Vector2(0f, 92f), new Vector2(700f, 45f), "SON HAYATTA KALAN KAZANIR", 22f);

        GameObject buttonObject = new("PlayButton", typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.SetParent(root, false);
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(0f, -30f);
        buttonRect.sizeDelta = new Vector2(360f, 92f);

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0.3f, 0.82f, 0.25f, 1f);

        playButton = buttonObject.GetComponent<Button>();
        playButton.targetGraphic = buttonImage;
        CreateText(buttonRect, "Label", Vector2.zero, new Vector2(340f, 80f), "OYNA", 38f);

        statusText = CreateText(root, "Status", new Vector2(0f, -118f), new Vector2(700f, 45f), "HAZIR", 19f);

        UnityEditor.Undo.RegisterCreatedObjectUndo(menuRoot, "Create Editable Main Menu");
        UnityEditor.EditorUtility.SetDirty(gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        UnityEditor.Selection.activeGameObject = menuRoot;
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
