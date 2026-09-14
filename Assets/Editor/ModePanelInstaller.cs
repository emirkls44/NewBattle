#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ModePanelInstaller
{
    [MenuItem("Tools/New Battle/Create Mode Panel")]
    private static void CreateModePanel()
    {
        NetworkMenuUI menuUi = UnityEngine.Object.FindFirstObjectByType<NetworkMenuUI>(
            FindObjectsInactive.Include
        );

        if (menuUi == null)
        {
            Debug.LogError("NetworkMenuUI bulunamadi. Game_UI nesnesinde Network Menu UI componenti olmali.");
            return;
        }

        Transform mainMenu = FindMainMenu(menuUi.transform);
        if (mainMenu == null)
        {
            Debug.LogError("MainMenu bulunamadi. Game_UI altinda MainMenu nesnesi olmali.");
            return;
        }

        Button playButton = FindButton(mainMenu, "PlayButton");
        if (playButton == null)
        {
            Debug.LogError("MainMenu altinda PlayButton bulunamadi.");
            return;
        }

        Transform existing = mainMenu.Find("ModePanel");
        if (existing != null)
        {
            BindReferences(menuUi, mainMenu.gameObject, playButton, existing.gameObject);
            Selection.activeGameObject = existing.gameObject;
            Debug.Log("ModePanel zaten mevcut ve referanslari yeniden baglandi.");
            return;
        }

        GameObject panelObject = new("ModePanel", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(panelObject, "Create Mode Panel");

        RectTransform panel = panelObject.GetComponent<RectTransform>();
        panel.SetParent(mainMenu, false);
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = new Vector2(0f, -55f);
        panel.sizeDelta = new Vector2(460f, 390f);

        CreateButton(panel, "BotsButton", new Vector2(0f, 105f), "BOTLARLA OYNA",
            new Color(0.95f, 0.62f, 0.18f, 1f), new Vector2(400f, 76f), 28f);
        CreateButton(panel, "EveryoneSoloButton", new Vector2(0f, 10f), "HERKES TEK",
            new Color(0.25f, 0.78f, 0.28f, 1f), new Vector2(400f, 76f), 28f);
        CreateButton(panel, "SquadButton", new Vector2(0f, -85f), "TAKIMLA OYNA",
            new Color(0.18f, 0.58f, 0.92f, 1f), new Vector2(400f, 76f), 28f);
        CreateButton(panel, "BackButton", new Vector2(0f, -170f), "GERI",
            new Color(0.28f, 0.32f, 0.38f, 1f), new Vector2(210f, 58f), 23f);

        BindReferences(menuUi, mainMenu.gameObject, playButton, panelObject);
        panelObject.SetActive(false);

        EditorUtility.SetDirty(menuUi);
        EditorSceneManager.MarkSceneDirty(menuUi.gameObject.scene);
        Selection.activeGameObject = panelObject;
        Debug.Log("ModePanel basariyla MainMenu altinda olusturuldu ve butonlar baglandi.");
    }

    private static Transform FindMainMenu(Transform startingPoint)
    {
        if (startingPoint.name == "MainMenu")
            return startingPoint;

        Transform direct = startingPoint.Find("MainMenu");
        if (direct != null)
            return direct;

        Transform[] allChildren = startingPoint.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in allChildren)
        {
            if (child.name == "MainMenu")
                return child;
        }

        return null;
    }

    private static Button FindButton(Transform parent, string buttonName)
    {
        Button[] buttons = parent.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button.name == buttonName)
                return button;
        }

        return null;
    }

    private static void BindReferences(
        NetworkMenuUI menuUi,
        GameObject mainMenu,
        Button playButton,
        GameObject modePanel)
    {
        SerializedObject serializedMenu = new(menuUi);
        serializedMenu.FindProperty("menuRoot").objectReferenceValue = mainMenu;
        serializedMenu.FindProperty("playButton").objectReferenceValue = playButton;
        serializedMenu.FindProperty("modePanel").objectReferenceValue = modePanel;
        serializedMenu.FindProperty("botsButton").objectReferenceValue = FindButton(modePanel.transform, "BotsButton");
        serializedMenu.FindProperty("everyoneSoloButton").objectReferenceValue = FindButton(modePanel.transform, "EveryoneSoloButton");
        serializedMenu.FindProperty("squadButton").objectReferenceValue = FindButton(modePanel.transform, "SquadButton");
        serializedMenu.FindProperty("backButton").objectReferenceValue = FindButton(modePanel.transform, "BackButton");
        serializedMenu.ApplyModifiedProperties();
    }

    private static Button CreateButton(
        RectTransform parent,
        string objectName,
        Vector2 position,
        string label,
        Color color,
        Vector2 size,
        float fontSize)
    {
        GameObject buttonObject = new(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.color = color;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        GameObject labelObject = new("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = labelObject.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        text.raycastTarget = false;
        return button;
    }
}
#endif
