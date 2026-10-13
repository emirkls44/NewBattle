using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Ekran joystick'i. Dokunma alani (touchZone) atanmissa "yuzen" joystick olur:
/// alanin icinde nereye dokunulursa joystick parmagin altinda belirir, birakinca
/// dinlenme yerine soluk olarak doner. Kur: sol yarinin alti hareket, sag yarinin
/// alti saldiri (HareketAlani / SaldiriAlani).
/// </summary>
[RequireComponent(typeof(RectTransform), typeof(Image))]
public class VirtualJoystick : MonoBehaviour, IDragHandler, IPointerUpHandler, IPointerDownHandler,
    IInitializePotentialDragHandler
{
    [SerializeField, Range(0.1f, 1f)] private float handleRange = 0.65f;

    [Header("Yuzen Joystick")]
    [Tooltip("Bu alanin icinde nereye dokunulursa joystick oraya gelir. Bossa joystick sabit durur.")]
    [SerializeField] private RectTransform touchZone;
    [Tooltip("Dokunulmazken joystick'in saydamligi (dinlenme yerinde).")]
    [SerializeField, Range(0f, 1f)] private float idleAlpha = 0.45f;

    [Header("Gorsel Stil")]
    [Tooltip("Acikken arka plan ve top yuvarlak cizilir; asagidaki renkler kullanilir.")]
    [SerializeField] private bool applyBattleRoyaleStyle = true;
    [SerializeField, Range(0.2f, 0.7f)] private float handleSizeRatio = 0.42f;
    [Tooltip("Arka plan dairesi: ici bu rengin yarisi kadar, kenar halkasi tam bu renk.")]
    [SerializeField] private Color backgroundColor = new(1f, 1f, 1f, 0.32f);
    [SerializeField] private Color handleColor = new(1f, 1f, 1f, 0.55f);

    [SerializeField, HideInInspector] private int layoutVersion;

    private RectTransform _backgroundRect;
    private RectTransform _handleRect;
    private CanvasGroup _group;
    private Vector2 _restPosition;
    private bool _touching;

    public bool IsFloating => touchZone != null;

    private static Texture2D _circleTexture;
    private static Sprite _circleSprite;
    private static Sprite _ringSprite;

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

        _restPosition = _backgroundRect.anchoredPosition;

        if (IsFloating)
        {
            // Dokunuslari alan alir; joystick'in kendisi isini engellemesin.
            if (TryGetComponent(out Image background))
                background.raycastTarget = false;

            _group = GetComponent<CanvasGroup>();
            if (_group == null)
                _group = gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.alpha = idleAlpha;

            FloatingJoystickZone zone = touchZone.GetComponent<FloatingJoystickZone>();
            if (zone == null)
                zone = touchZone.gameObject.AddComponent<FloatingJoystickZone>();
            zone.Bind(this);
        }

        ResetJoystick();
    }

    /// <summary>Alana dokunuldu: joystick parmagin altina gelir.</summary>
    public void BeginTouch(PointerEventData eventData)
    {
        if (!isActiveAndEnabled)
            return;

        _touching = true;
        MoveCenterTo(eventData);
        if (_group != null)
            _group.alpha = 1f;
        OnDrag(eventData);
    }

    /// <summary>Parmak kalkti: girdi sifirlanir, joystick dinlenme yerine doner.</summary>
    public void EndTouch()
    {
        _touching = false;
        ResetJoystick();
    }

    private void MoveCenterTo(PointerEventData eventData)
    {
        var parent = _backgroundRect.parent as RectTransform;
        if (parent == null)
            return;

        // Joystick ekrandan tasmasin: merkezi kenarlardan yaricap kadar iceride tut.
        Vector3 scale = _backgroundRect.lossyScale;
        float radius = _backgroundRect.rect.width * 0.5f * Mathf.Abs(scale.x);
        Camera eventCamera = eventData.pressEventCamera;
        if (eventCamera != null)
        {
            // Kamerali canvas'ta yaricap dunya biriminde; ekrana cevir.
            Vector3 a = eventCamera.WorldToScreenPoint(_backgroundRect.position);
            Vector3 b = eventCamera.WorldToScreenPoint(_backgroundRect.position + _backgroundRect.right * radius);
            radius = Vector3.Distance(a, b);
        }

        Vector2 screen = eventData.position;
        screen.x = Mathf.Clamp(screen.x, radius, Mathf.Max(radius, Screen.width - radius));
        screen.y = Mathf.Clamp(screen.y, radius, Mathf.Max(radius, Screen.height - radius));

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, screen, eventCamera, out Vector3 world))
            _backgroundRect.position = world;
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        // Joystick ilk milimetreden tepki versin (surukleme esigi beklemesin).
        eventData.useDragThreshold = false;
    }

    private void ApplyVisualStyle()
    {
        Sprite circle = GetCircleSprite();

        Image backgroundImage = GetComponent<Image>();
        backgroundImage.sprite = GetRingSprite();
        backgroundImage.type = Image.Type.Simple;
        backgroundImage.preserveAspect = true;
        backgroundImage.raycastTarget = true;
        backgroundImage.color = backgroundColor;

        // Saydam daire altinda golge camur gibi gorunuyor.
        if (TryGetComponent(out Shadow shadow))
            shadow.enabled = false;

        Image handleImage = _handleRect.GetComponent<Image>();
        if (handleImage == null)
            handleImage = _handleRect.gameObject.AddComponent<Image>();

        handleImage.sprite = circle;
        handleImage.type = Image.Type.Simple;
        handleImage.preserveAspect = true;
        handleImage.raycastTarget = false;
        handleImage.color = handleColor;

        if (_handleRect.TryGetComponent(out Shadow handleShadow))
            handleShadow.enabled = false;

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

    /// <summary>Ici yari saydam, kenari belirgin halka (Battlelands joystick'i gibi).</summary>
    private static Sprite GetRingSprite()
    {
        if (_ringSprite != null)
            return _ringSprite;

        const int size = 256;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "RuntimeJoystickRing",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color32[] pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / center;

                float outerEdge = 1f - Mathf.SmoothStep(0.97f, 1f, d);
                float ring = Mathf.SmoothStep(0.88f, 0.91f, d);
                float alpha = outerEdge * Mathf.Lerp(0.5f, 1f, ring);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        _ringSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        _ringSprite.name = "RuntimeJoystickRingSprite";
        return _ringSprite;
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
        _touching = false;
        ResetJoystick();
    }

    private void ResetJoystick()
    {
        InputVector = Vector2.zero;

        if (_handleRect != null)
        {
            _handleRect.anchoredPosition = Vector2.zero;
        }

        if (IsFloating && _backgroundRect != null && !_touching)
        {
            _backgroundRect.anchoredPosition = _restPosition;
            if (_group != null)
                _group.alpha = idleAlpha;
        }
    }
}
