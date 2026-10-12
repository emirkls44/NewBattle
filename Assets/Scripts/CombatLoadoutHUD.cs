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

    [Header("Ikinci Silah Yuvasi (Kur ekler: CombatSlots/RifleSlot2)")]
    [SerializeField] private Button secondButton;
    [SerializeField] private Image secondBackground;
    [SerializeField] private TextMeshProUGUI secondText;

    [Header("Yuva Gorunumu")]
    [Tooltip("Secili olmayan yuva bu oranda kuculur.")]
    [SerializeField, Range(0.5f, 1f)] private float unselectedScale = 0.86f;
    [Tooltip("Nadirlik renginin ne kadar koyulastirilacagi (0 = oldugu gibi).")]
    [SerializeField, Range(0f, 0.9f)] private float selectedDarken = 0.18f;
    [SerializeField, Range(0f, 0.9f)] private float unselectedDarken = 0.55f;
    [SerializeField] private Color emptyAmmoColor = new(1f, 0.45f, 0.4f, 1f);

    [Header("Secim Renkleri")]
    [SerializeField] private Color selectedColor = new(0.28f, 0.78f, 0.25f, 0.96f);
    [SerializeField] private Color availableColor = new(0.16f, 0.18f, 0.21f, 0.92f);
    [SerializeField] private Color lockedColor = new(0.08f, 0.09f, 0.11f, 0.72f);

    private PlayerLoadout _loadout;
    private float _nextSearchTime;
    private Vector3 _rifleBaseScale = Vector3.one;
    private Vector3 _secondBaseScale = Vector3.one;

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
        rifleButton.onClick.AddListener(SelectFirstSlot);
        _rifleBaseScale = rifleButton.transform.localScale;

        if (secondButton != null)
        {
            secondButton.onClick.AddListener(SelectSecondSlot);
            _secondBaseScale = secondButton.transform.localScale;
        }

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
            rifleButton.onClick.RemoveListener(SelectFirstSlot);

        if (secondButton != null)
            secondButton.onClick.RemoveListener(SelectSecondSlot);
    }

    private void SelectFist()
    {
        _loadout?.SelectFist();
    }

    private void SelectFirstSlot() => _loadout?.SelectSlot(0);

    private void SelectSecondSlot() => _loadout?.SelectSlot(1);

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
            KeepOnOneLine(fistSlot.GetComponentInChildren<TextMeshProUGUI>(true));
        }

        if (rifleSlot != null)
        {
            rifleButton ??= rifleSlot.GetComponent<Button>();
            rifleBackground ??= rifleSlot.GetComponent<Image>();
            rifleText ??= rifleSlot.Find("Label")?.GetComponent<TextMeshProUGUI>();
            KeepOnOneLine(rifleText);
        }

        Transform secondSlot = root.Find("RifleSlot2");
        if (secondSlot != null)
        {
            secondButton ??= secondSlot.GetComponent<Button>();
            secondBackground ??= secondSlot.GetComponent<Image>();
            secondText ??= secondSlot.Find("Label")?.GetComponent<TextMeshProUGUI>();
        }

        // Yuvalarda iki satir var (silah adi + mermi): satir bolmeye izin ver.
        AllowTwoLines(rifleText);
        AllowTwoLines(secondText);
    }

    private static void AllowTwoLines(TextMeshProUGUI label)
    {
        if (label == null)
            return;

        label.richText = true;
        label.textWrappingMode = TextWrappingModes.NoWrap; // satir sadece \n ile
        label.enableAutoSizing = true;
    }

    /// <summary>
    /// Dar ekranli telefonlarda "YUMRUK" iki satira bolunup "YUMRU / K" oluyordu:
    /// yazi tek satirda kalsin, sigmazsa kuculsun.
    /// </summary>
    private static void KeepOnOneLine(TextMeshProUGUI label)
    {
        if (label == null)
            return;

        label.textWrappingMode = TextWrappingModes.NoWrap;
        if (!label.enableAutoSizing)
        {
            label.fontSizeMax = label.fontSize;
            label.fontSizeMin = Mathf.Min(12f, label.fontSize);
            label.enableAutoSizing = true;
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

        bool armed = loadoutReady && _loadout.HasRangedWeapon;

        // Silahsizken tek kutu: YUMRUK. Silah alininca iki yuva gorunur
        // (ikincisi bossa "BOS"); dokunulan yuvaya gecilir.
        SetActive(fistButton, !armed);
        SetActive(rifleButton, armed);
        SetActive(secondButton, armed);

        fistBackground.color = selectedColor;

        if (!armed)
            return;

        DrawSlot(0, rifleButton, rifleBackground, rifleText, _rifleBaseScale);
        DrawSlot(1, secondButton, secondBackground, secondText, _secondBaseScale);
    }

    private static void SetActive(Button button, bool active)
    {
        if (button != null && button.gameObject.activeSelf != active)
            button.gameObject.SetActive(active);
    }

    /// <summary>Bir yuvayi cizer: nadirlik renginde zemin, silah adi ve mermi.</summary>
    private void DrawSlot(int slot, Button button, Image background, TextMeshProUGUI text, Vector3 baseScale)
    {
        if (button == null || background == null || text == null)
            return;

        if (!_loadout.SlotHasWeapon(slot))
        {
            background.color = lockedColor;
            text.text = "<size=60%>BOŞ</size>";
            button.interactable = false;
            button.transform.localScale = baseScale * unselectedScale;
            return;
        }

        bool selected = _loadout.ActiveSlot == slot && _loadout.IsRifleSelected;
        int ammo = _loadout.GetSlotAmmo(slot);
        Color rarity = NewBattle.Gameplay.WeaponLootSettings.RarityColor(_loadout.GetSlotRarity(slot));

        Color fill = Color.Lerp(rarity, Color.black, selected ? selectedDarken : unselectedDarken);
        fill.a = selected ? 0.96f : 0.85f;
        background.color = fill;

        string ammoText = ammo > 0
            ? ammo.ToString()
            : $"<color=#{ColorUtility.ToHtmlStringRGB(emptyAmmoColor)}>0</color>";
        text.text = $"<size=46%>{SlotName(slot)}</size>\n{ammoText}";

        button.interactable = !selected && ammo > 0;
        button.transform.localScale = baseScale * (selected ? 1f : unselectedScale);
    }

    /// <summary>Yuvadaki silahin Turkce, buyuk harfli adi.</summary>
    private string SlotName(int slot)
    {
        NewBattle.Gameplay.WeaponDefinition weapon = _loadout.GetSlotWeapon(slot);
        string label = weapon != null ? NewBattle.Gameplay.KillEvents.WeaponDisplayName(weapon.label) : "SİLAH";
        return TurkishUpper(label);
    }

    /// <summary>"Keskin Nişancı" -> "KESKİN NİŞANCI" (kultur ayarina guvenmeden).</summary>
    private static string TurkishUpper(string value)
    {
        return value.Replace('i', 'İ').Replace('ı', 'I').ToUpperInvariant();
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
