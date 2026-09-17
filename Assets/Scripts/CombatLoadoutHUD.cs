using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CombatLoadoutHUD : MonoBehaviour
{
    [Header("Hierarchy Referanslari")]
    [SerializeField] private Button fistButton;
    [SerializeField] private Button rifleButton;
    [SerializeField] private Image fistBackground;
    [SerializeField] private Image rifleBackground;
    [SerializeField] private TextMeshProUGUI rifleText;

    [Header("Secim Renkleri")]
    [SerializeField] private Color selectedColor = new(0.28f, 0.78f, 0.25f, 0.96f);
    [SerializeField] private Color availableColor = new(0.16f, 0.18f, 0.21f, 0.92f);
    [SerializeField] private Color lockedColor = new(0.08f, 0.09f, 0.11f, 0.72f);

    private PlayerLoadout _loadout;
    private float _nextSearchTime;

    private void Start()
    {
        ResolveHierarchyReferences();

        if (!ReferencesAreValid())
        {
            Debug.LogError(
                "CombatLoadoutHUD: CombatSlots bulunamadi. Play modunu kapatip " +
                "component menusunden 'Create Editable Combat Slots In Hierarchy' calistir."
            );
            return;
        }

        fistButton.onClick.AddListener(SelectFist);
        rifleButton.onClick.AddListener(SelectRifle);
        Refresh();
    }

    private void Update()
    {
        if (_loadout == null && Time.unscaledTime >= _nextSearchTime)
        {
            FindLocalLoadout();
            _nextSearchTime = Time.unscaledTime + 0.25f;
        }

        Refresh();
    }

    private void OnDestroy()
    {
        if (fistButton != null)
            fistButton.onClick.RemoveListener(SelectFist);

        if (rifleButton != null)
            rifleButton.onClick.RemoveListener(SelectRifle);
    }

    private void SelectFist()
    {
        _loadout?.SelectFist();
    }

    private void SelectRifle()
    {
        _loadout?.SelectRifle();
    }

    private void FindLocalLoadout()
    {
        PlayerLoadout[] loadouts = UnityEngine.Object.FindObjectsByType<PlayerLoadout>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        foreach (PlayerLoadout candidate in loadouts)
        {
            if (candidate == null || candidate.Object == null || !candidate.HasInputAuthority)
                continue;

            _loadout = candidate;
            return;
        }
    }

    private void ResolveHierarchyReferences()
    {
        Transform root = transform.name == "CombatSlots"
            ? transform
            : transform.Find("CombatSlots");

        if (root == null)
            return;

        Transform fistSlot = root.Find("FistSlot");
        Transform rifleSlot = root.Find("RifleSlot");

        if (fistSlot != null)
        {
            fistButton ??= fistSlot.GetComponent<Button>();
            fistBackground ??= fistSlot.GetComponent<Image>();
        }

        if (rifleSlot != null)
        {
            rifleButton ??= rifleSlot.GetComponent<Button>();
            rifleBackground ??= rifleSlot.GetComponent<Image>();
            rifleText ??= rifleSlot.Find("Label")?.GetComponent<TextMeshProUGUI>();
        }
    }

    private bool ReferencesAreValid()
    {
        return fistButton != null
            && rifleButton != null
            && fistBackground != null
            && rifleBackground != null
            && rifleText != null;
    }

    private void Refresh()
    {
        if (!ReferencesAreValid())
            return;

        // Networked alanlar sadece nesne spawn'liyken okunabilir; bu HUD ise
        // oyuncudan bagimsiz calisan bir MonoBehaviour.
        bool loadoutReady = _loadout != null
            && _loadout.Object != null
            && _loadout.Object.IsValid
            && _loadout.Runner != null;

        bool hasRifle = loadoutReady && _loadout.HasRifle;
        int selectedSlot = loadoutReady ? _loadout.SelectedSlot : 0;

        fistBackground.color = selectedSlot == 0 ? selectedColor : availableColor;
        rifleBackground.color = !hasRifle
            ? lockedColor
            : selectedSlot == 1 ? selectedColor : availableColor;

        rifleButton.interactable = hasRifle;
        rifleText.text = hasRifle ? $"SILAH\n{_loadout.RifleAmmo}" : "SILAH\nKILITLI";
    }

    /// <summary>Elindeki silahin adi; cok silahli sistemde hangi silah oldugunu gosterir.</summary>
    private string WeaponLabel()
    {
        NewBattle.Gameplay.WeaponDefinition weapon = _loadout.CurrentWeapon;
        return weapon != null && !string.IsNullOrEmpty(weapon.label)
            ? weapon.label.ToUpperInvariant()
            : "SILAH";
    }

    [ContextMenu("Create Editable Combat Slots In Hierarchy")]
    private void CreateEditableCombatSlotsInHierarchy()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            Debug.LogWarning("CombatSlots olusturmak icin once Play modunu kapat.");
            return;
        }

        Transform existing = transform.Find("CombatSlots");
        if (existing != null)
        {
            ResolveHierarchyReferences();
            UnityEditor.Selection.activeGameObject = existing.gameObject;
            Debug.Log("CombatSlots zaten Hierarchy'de bulunuyor.");
            return;
        }

        GameObject rootObject = CreateUiObject("CombatSlots", transform);
        RectTransform root = rootObject.GetComponent<RectTransform>();
        root.anchorMin = new Vector2(0.5f, 0f);
        root.anchorMax = new Vector2(0.5f, 0f);
        root.pivot = new Vector2(0.5f, 0f);
        root.anchoredPosition = new Vector2(0f, 108f);
        root.sizeDelta = new Vector2(204f, 72f);

        CreateSlot(root, "FistSlot", new Vector2(-51f, 36f), "YUMRUK", out fistButton, out fistBackground, out _);
        CreateSlot(root, "RifleSlot", new Vector2(51f, 36f), "SILAH\nKILITLI", out rifleButton, out rifleBackground, out rifleText);

        UnityEditor.Undo.RegisterCreatedObjectUndo(rootObject, "Create Editable Combat Slots");
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

    private void CreateSlot(RectTransform parent, string objectName, Vector2 position, string label,
        out Button button, out Image background, out TextMeshProUGUI text)
    {
        GameObject slotObject = new(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform rect = slotObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(96f, 72f);

        background = slotObject.GetComponent<Image>();
        background.color = availableColor;

        button = slotObject.GetComponent<Button>();
        button.targetGraphic = background;

        GameObject textObject = CreateUiObject("Label", rect);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(4f, 4f);
        textRect.offsetMax = new Vector2(-4f, -4f);

        text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 18f;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        text.raycastTarget = false;
    }
}
