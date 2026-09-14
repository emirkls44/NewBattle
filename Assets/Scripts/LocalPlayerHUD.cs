using System.Collections;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LocalPlayerHUD : MonoBehaviour
{
    [Header("Hierarchy Referanslari")]
    [SerializeField] private Image healthFill;
    [SerializeField] private Image shieldFill;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI shieldText;

    [Header("Hasar Sayisi Efekti")]
    [SerializeField, Min(1f)] private float damagePulseScale = 1.35f;
    [SerializeField, Min(0.05f)] private float damagePulseDuration = 0.22f;

    private HealthController _health;
    private float _nextPlayerSearchTime;
    private float _previousHealth;
    private float _previousShield;
    private bool _previousValuesInitialized;
    private Vector3 _healthTextBaseScale = Vector3.one;
    private Vector3 _shieldTextBaseScale = Vector3.one;
    private Coroutine _healthPulseRoutine;
    private Coroutine _shieldPulseRoutine;

    private void Start()
    {
        ResolveHierarchyReferences();

        if (healthText != null)
            _healthTextBaseScale = healthText.rectTransform.localScale;

        if (shieldText != null)
            _shieldTextBaseScale = shieldText.rectTransform.localScale;

        if (!ReferencesAreValid())
        {
            Debug.LogError(
                "LocalPlayerHUD: Hierarchy HUD bulunamadi. " +
                "Component menusunden 'Create Editable HUD In Hierarchy' calistir."
            );
        }
    }

    private void Update()
    {
        if (_health == null && Time.unscaledTime >= _nextPlayerSearchTime)
        {
            FindLocalPlayer();
            _nextPlayerSearchTime = Time.unscaledTime + 0.25f;
        }

        Refresh();
    }

    private void OnDestroy()
    {
        Unbind();
    }

    private void FindLocalPlayer()
    {
        HealthController[] healthControllers = UnityEngine.Object.FindObjectsByType<HealthController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        foreach (HealthController candidate in healthControllers)
        {
            if (candidate == null || candidate.Object == null || !candidate.HasInputAuthority)
                continue;

            Bind(candidate);
            return;
        }
    }

    public void BindLocalPlayer(HealthController healthController)
    {
        if (healthController == null || healthController == _health)
            return;

        Unbind();
        _health = healthController;
        _previousHealth = _health.currentHealth;
        _previousShield = _health.currentShield;
        _previousValuesInitialized = true;
        _health.OnHealthChanged += Refresh;
        Refresh();
    }

    private void Bind(HealthController healthController)
    {
        BindLocalPlayer(healthController);
    }

    private void Unbind()
    {
        if (_health != null)
            _health.OnHealthChanged -= Refresh;

        _health = null;
        _previousValuesInitialized = false;
    }

    private void ResolveHierarchyReferences()
    {
        Transform root = transform.Find("PlayerStatusHUD");
        if (root == null)
            return;

        healthFill ??= root.Find("HealthBar/Fill")?.GetComponent<Image>();
        shieldFill ??= root.Find("ShieldBar/Fill")?.GetComponent<Image>();
        healthText ??= root.Find("HealthValue")?.GetComponent<TextMeshProUGUI>();
        shieldText ??= root.Find("ShieldValue")?.GetComponent<TextMeshProUGUI>();
    }

    private bool ReferencesAreValid()
    {
        return healthFill != null
            && shieldFill != null
            && healthText != null
            && shieldText != null;
    }

    private void Refresh()
    {
        if (!ReferencesAreValid())
            return;

        float healthRatio = 0f;
        float shieldRatio = 0f;
        int healthValue = 0;
        int shieldValue = 0;

        if (_health != null)
        {
            healthRatio = _health.maxHealth > 0f
                ? Mathf.Clamp01(_health.currentHealth / _health.maxHealth)
                : 0f;
            shieldRatio = _health.maxShield > 0f
                ? Mathf.Clamp01(_health.currentShield / _health.maxShield)
                : 0f;
            healthValue = Mathf.FloorToInt(Mathf.Max(0f, _health.currentHealth + 0.001f));
            shieldValue = Mathf.FloorToInt(Mathf.Max(0f, _health.currentShield + 0.001f));

            if (_previousValuesInitialized)
            {
                if (_health.currentHealth < _previousHealth - 0.001f)
                    PlayHealthDamagePulse();

                if (_health.currentShield < _previousShield - 0.001f)
                    PlayShieldDamagePulse();
            }

            _previousHealth = _health.currentHealth;
            _previousShield = _health.currentShield;
            _previousValuesInitialized = true;
        }

        SetBarRatio(healthFill, healthRatio);
        SetBarRatio(shieldFill, shieldRatio);
        healthText.text = healthValue.ToString();
        shieldText.text = shieldValue.ToString();
    }

    private static void SetBarRatio(Image fillImage, float ratio)
    {
        ratio = Mathf.Clamp01(ratio);

        // Bos sprite kullanildiginda Image.fillAmount goruntuyu kesmez.
        // Anchor genisligini degistirmek her UI Image tipinde guvenilir calisir.
        fillImage.gameObject.SetActive(ratio > 0.001f);

        if (ratio <= 0.001f)
            return;

        RectTransform fillRect = fillImage.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(ratio, 1f);
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);
    }

    private void PlayHealthDamagePulse()
    {
        if (_healthPulseRoutine != null)
            StopCoroutine(_healthPulseRoutine);

        healthText.rectTransform.localScale = _healthTextBaseScale;
        _healthPulseRoutine = StartCoroutine(
            PulseNumber(healthText.rectTransform, _healthTextBaseScale, true)
        );
    }

    private void PlayShieldDamagePulse()
    {
        if (_shieldPulseRoutine != null)
            StopCoroutine(_shieldPulseRoutine);

        shieldText.rectTransform.localScale = _shieldTextBaseScale;
        _shieldPulseRoutine = StartCoroutine(
            PulseNumber(shieldText.rectTransform, _shieldTextBaseScale, false)
        );
    }

    private IEnumerator PulseNumber(RectTransform target, Vector3 baseScale, bool isHealth)
    {
        float elapsed = 0f;

        while (elapsed < damagePulseDuration)
        {
            float progress = elapsed / damagePulseDuration;
            float heartbeat = Mathf.Sin(progress * Mathf.PI);
            float scaleMultiplier = Mathf.Lerp(1f, damagePulseScale, heartbeat);
            target.localScale = baseScale * scaleMultiplier;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        target.localScale = baseScale;

        if (isHealth)
            _healthPulseRoutine = null;
        else
            _shieldPulseRoutine = null;
    }

    [ContextMenu("Create Editable HUD In Hierarchy")]
    private void CreateEditableHudInHierarchy()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            Debug.LogWarning("HUD olusturmak icin once Play modunu kapat.");
            return;
        }

        Transform existing = transform.Find("PlayerStatusHUD");
        if (existing != null)
        {
            ResolveHierarchyReferences();
            UnityEditor.Selection.activeGameObject = existing.gameObject;
            Debug.Log("PlayerStatusHUD zaten Hierarchy'de bulunuyor.");
            return;
        }

        GameObject rootObject = CreateUiObject("PlayerStatusHUD", transform);
        RectTransform root = rootObject.GetComponent<RectTransform>();
        root.anchorMin = new Vector2(0.5f, 0f);
        root.anchorMax = new Vector2(0.5f, 0f);
        root.pivot = new Vector2(0.5f, 0f);
        root.anchoredPosition = new Vector2(0f, 24f);
        root.sizeDelta = new Vector2(576f, 76f);

        Image panel = rootObject.AddComponent<Image>();
        panel.color = new Color(0.055f, 0.065f, 0.075f, 0.72f);
        panel.raycastTarget = false;

        CreateEditableBar(
            root,
            "ShieldBar",
            new Vector2(24f, 50f),
            new Vector2(500f, 12f),
            new Color(0.12f, 0.65f, 1f, 1f),
            out shieldFill
        );

        CreateEditableBar(
            root,
            "HealthBar",
            new Vector2(24f, 23f),
            new Vector2(500f, 24f),
            new Color(0.35f, 0.95f, 0.25f, 1f),
            out healthFill
        );

        shieldText = CreateValueText(
            root,
            "ShieldValue",
            new Vector2(-262f, 50f),
            new Vector2(54f, 22f),
            15f,
            new Color(0.12f, 0.65f, 1f, 1f)
        );

        healthText = CreateValueText(
            root,
            "HealthValue",
            new Vector2(-262f, 23f),
            new Vector2(54f, 32f),
            20f,
            new Color(0.35f, 0.95f, 0.25f, 1f)
        );

        UnityEditor.Undo.RegisterCreatedObjectUndo(rootObject, "Create Editable Player HUD");
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

    private static void CreateEditableBar(
        RectTransform parent,
        string objectName,
        Vector2 position,
        Vector2 size,
        Color fillColor,
        out Image fillImage)
    {
        GameObject backgroundObject = CreateUiObject(objectName, parent);
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0.5f, 0f);
        backgroundRect.anchorMax = new Vector2(0.5f, 0f);
        backgroundRect.pivot = new Vector2(0.5f, 0.5f);
        backgroundRect.anchoredPosition = position;
        backgroundRect.sizeDelta = size;

        Image background = backgroundObject.AddComponent<Image>();
        background.color = new Color(0.025f, 0.03f, 0.035f, 0.82f);
        background.raycastTarget = false;

        GameObject fillObject = CreateUiObject("Fill", backgroundRect);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);

        fillImage = fillObject.AddComponent<Image>();
        fillImage.color = fillColor;
        fillImage.type = Image.Type.Simple;
        fillImage.raycastTarget = false;
    }

    private static TextMeshProUGUI CreateValueText(
        RectTransform parent,
        string objectName,
        Vector2 position,
        Vector2 size,
        float fontSize,
        Color color)
    {
        GameObject textObject = CreateUiObject(objectName, parent);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0f);
        textRect.anchorMax = new Vector2(0.5f, 0f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = position;
        textRect.sizeDelta = size;

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = "100";
        text.alignment = TextAlignmentOptions.Right;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }
}
