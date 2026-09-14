using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform), typeof(Image))]
public class VirtualJoystick : MonoBehaviour, IDragHandler, IPointerUpHandler, IPointerDownHandler
{
    [SerializeField, Range(0.1f, 1f)] private float handleRange = 0.65f;

    [Header("Gorsel Stil")]
    [SerializeField] private bool applyBattleRoyaleStyle = true;
    [SerializeField, Range(0.2f, 0.7f)] private float handleSizeRatio = 0.38f;

    private RectTransform _backgroundRect;
    private RectTransform _handleRect;

    private static Texture2D _circleTexture;
    private static Sprite _circleSprite;

    public Vector2 InputVector { get; private set; }

    private void Awake()
    {
        _backgroundRect = (RectTransform)transform;

        if (transform.childCount == 0)
        {
            Debug.LogError($"{name}: VirtualJoystick needs a Handle child.");
            enabled = false;
            return;
        }

        _handleRect = transform.GetChild(0) as RectTransform;

        // Keep the handle centered even if its prefab/RectTransform anchors were set incorrectly.
        _handleRect.anchorMin = new Vector2(0.5f, 0.5f);
        _handleRect.anchorMax = new Vector2(0.5f, 0.5f);
        _handleRect.pivot = new Vector2(0.5f, 0.5f);
        _handleRect.localScale = Vector3.one;

        if (applyBattleRoyaleStyle)
            ApplyVisualStyle();

        ResetJoystick();
    }

    private void ApplyVisualStyle()
    {
        Sprite circle = GetCircleSprite();

        Image backgroundImage = GetComponent<Image>();
        backgroundImage.sprite = circle;
        backgroundImage.type = Image.Type.Simple;
        backgroundImage.preserveAspect = true;
        backgroundImage.raycastTarget = true;

        Image handleImage = _handleRect.GetComponent<Image>();
        if (handleImage == null)
            handleImage = _handleRect.gameObject.AddComponent<Image>();

        handleImage.sprite = circle;
        handleImage.type = Image.Type.Simple;
        handleImage.preserveAspect = true;
        handleImage.raycastTarget = false;

        float diameter = Mathf.Min(_backgroundRect.rect.width, _backgroundRect.rect.height);
        if (diameter <= 0f)
            diameter = Mathf.Min(_backgroundRect.sizeDelta.x, _backgroundRect.sizeDelta.y);

        float handleDiameter = Mathf.Max(1f, diameter * handleSizeRatio);
        _handleRect.sizeDelta = new Vector2(handleDiameter, handleDiameter);
    }

    private static Sprite GetCircleSprite()
    {
        if (_circleSprite != null)
            return _circleSprite;

        const int size = 128;
        _circleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "RuntimeJoystickCircle",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color32[] pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        float radius = center;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float normalizedDistance = Mathf.Sqrt(dx * dx + dy * dy) / radius;
                float alpha = 1f - Mathf.SmoothStep(0.94f, 1f, normalizedDistance);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        _circleTexture.SetPixels32(pixels);
        _circleTexture.Apply(false, true);

        _circleSprite = Sprite.Create(
            _circleTexture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f
        );
        _circleSprite.name = "RuntimeJoystickCircleSprite";
        return _circleSprite;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_backgroundRect == null || _handleRect == null)
        {
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _backgroundRect,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint))
        {
            return;
        }

        Rect rect = _backgroundRect.rect;
        Vector2 centeredPoint = localPoint - rect.center;
        Vector2 halfSize = rect.size * 0.5f;

        if (halfSize.x <= 0f || halfSize.y <= 0f)
        {
            return;
        }

        Vector2 normalizedInput = new(
            centeredPoint.x / halfSize.x,
            centeredPoint.y / halfSize.y
        );

        InputVector = Vector2.ClampMagnitude(normalizedInput, 1f);

        float movementRadius = Mathf.Min(halfSize.x, halfSize.y) * handleRange;
        _handleRect.anchoredPosition = InputVector * movementRadius;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        ResetJoystick();
    }

    private void OnDisable()
    {
        ResetJoystick();
    }

    private void ResetJoystick()
    {
        InputVector = Vector2.zero;

        if (_handleRect != null)
        {
            _handleRect.anchoredPosition = Vector2.zero;
        }
    }
}
