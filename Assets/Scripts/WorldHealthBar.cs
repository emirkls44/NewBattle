using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(HealthController))]
public class WorldHealthBar : MonoBehaviour
{
    [SerializeField] private Vector3 worldOffset = new(0f, 2.25f, 0f);
    [SerializeField] private Color healthColor = new(0.15f, 0.9f, 0.25f, 1f);
    [SerializeField] private Color shieldColor = new(0.15f, 0.65f, 1f, 1f);

    private HealthController _health;
    private Camera _camera;
    private Transform _canvasTransform;
    private RectTransform _healthFill;
    private RectTransform _shieldFill;
    private const float BarWidth = 100f;

    private void Start()
    {
        _health = GetComponent<HealthController>();

        // Yerel oyuncu kendi canini alt HUD'da gorur.
        // Kafa ustu bar sadece botlar ve diger ag oyunculari icindir.
        if (_health.HasInputAuthority)
        {
            enabled = false;
            return;
        }

        _camera = Camera.main;

        CreateBar();
        _health.OnHealthChanged += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        if (_health != null)
            _health.OnHealthChanged -= Refresh;
    }

    private void LateUpdate()
    {
        if (_canvasTransform == null)
            return;

        if (_camera == null)
            _camera = Camera.main;

        _canvasTransform.position = transform.position + worldOffset;

        if (_camera != null)
            _canvasTransform.rotation = _camera.transform.rotation;

        // Event kacirsa bile agdaki son can degeri ekrana yansir.
        Refresh();
    }

    private void CreateBar()
    {
        GameObject canvasObject = new("WorldHealthCanvas", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(transform, false);
        _canvasTransform = canvasObject.transform;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 20;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(110f, 18f);
        canvasRect.localScale = Vector3.one * 0.01f;

        CreateBackground(canvasRect, "HealthBackground", new Vector2(0f, -4.5f));
        _healthFill = CreateFill(canvasRect, "Health", new Vector2(0f, -4.5f), healthColor);

        CreateBackground(canvasRect, "ShieldBackground", new Vector2(0f, 4.5f));
        _shieldFill = CreateFill(canvasRect, "Shield", new Vector2(0f, 4.5f), shieldColor);
    }

    private static void CreateBackground(RectTransform parent, string objectName, Vector2 position)
    {
        Image image = CreateImage(parent, objectName, position);
        image.color = new Color(0f, 0f, 0f, 0.75f);
    }

    private static RectTransform CreateFill(RectTransform parent, string objectName, Vector2 position, Color color)
    {
        Image image = CreateImage(parent, objectName, position);
        image.color = color;
        return image.rectTransform;
    }

    private static Image CreateImage(RectTransform parent, string objectName, Vector2 position)
    {
        GameObject imageObject = new(objectName, typeof(RectTransform), typeof(Image));
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(100f, 6f);

        Image image = imageObject.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private void Refresh()
    {
        if (_health == null)
            return;

        if (_healthFill != null)
        {
            float healthRatio = _health.maxHealth > 0f
                ? Mathf.Clamp01(_health.currentHealth / _health.maxHealth)
                : 0f;
            SetBarWidth(_healthFill, healthRatio, -4.5f);
        }

        if (_shieldFill != null)
        {
            float shieldRatio = _health.maxShield > 0f
                ? Mathf.Clamp01(_health.currentShield / _health.maxShield)
                : 0f;
            SetBarWidth(_shieldFill, shieldRatio, 4.5f);
            _shieldFill.gameObject.SetActive(shieldRatio > 0f);
        }
    }

    private static void SetBarWidth(RectTransform bar, float ratio, float yPosition)
    {
        float width = BarWidth * ratio;
        bar.sizeDelta = new Vector2(width, 6f);
        bar.anchoredPosition = new Vector2(-BarWidth * 0.5f + width * 0.5f, yPosition);
    }
}
