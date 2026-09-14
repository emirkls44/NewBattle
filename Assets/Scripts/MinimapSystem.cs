using UnityEngine;
using UnityEngine.UI;

public class MinimapSystem : MonoBehaviour
{
    [Header("Minimap Kamera")]
    [SerializeField] private int textureResolution = 512;
    [SerializeField] private float mapHeight = 50f;
    [SerializeField] private float visibleWorldRadius = 16f;

    [Header("Minimap UI")]
    [SerializeField] private float minimapSize = 220f;
    [SerializeField] private Vector2 cornerOffset = new(25f, 25f);

    private RenderTexture _renderTexture;
    private GameObject _cameraObject;
    private GameObject _uiRoot;
    private Texture2D _circleMaskTexture;
    private Sprite _circleMaskSprite;
    private DropPhaseController _dropPhase;
    private float _nextPhaseSearchTime;

    private void Start()
    {
        CreateMinimapCamera();
        CreateMinimapUI();

        if (_uiRoot != null)
            _uiRoot.SetActive(false);
    }

    private void Update()
    {
        if (_dropPhase == null && Time.unscaledTime >= _nextPhaseSearchTime)
        {
            _dropPhase = UnityEngine.Object.FindFirstObjectByType<DropPhaseController>();
            _nextPhaseSearchTime = Time.unscaledTime + 0.25f;
        }

        bool dropPhaseIsSpawned = _dropPhase != null &&
                                  _dropPhase.Object != null &&
                                  _dropPhase.Runner != null &&
                                  _dropPhase.Runner.IsRunning;

        if (_uiRoot != null)
            _uiRoot.SetActive(dropPhaseIsSpawned && _dropPhase.GameplayStarted);
    }

    private void OnDestroy()
    {
        if (_renderTexture != null)
        {
            _renderTexture.Release();
            Destroy(_renderTexture);
        }

        if (_cameraObject != null)
            Destroy(_cameraObject);

        if (_uiRoot != null)
            Destroy(_uiRoot);

        if (_circleMaskSprite != null)
            Destroy(_circleMaskSprite);

        if (_circleMaskTexture != null)
            Destroy(_circleMaskTexture);
    }

    private void CreateMinimapCamera()
    {
        _renderTexture = new RenderTexture(textureResolution, textureResolution, 16)
        {
            name = "RuntimeMinimapTexture",
            antiAliasing = 2
        };
        _renderTexture.Create();

        _cameraObject = new GameObject("MinimapCamera");
        Camera minimapCamera = _cameraObject.AddComponent<Camera>();
        minimapCamera.orthographic = true;
        minimapCamera.orthographicSize = visibleWorldRadius;
        minimapCamera.clearFlags = CameraClearFlags.SolidColor;
        minimapCamera.backgroundColor = new Color(0.12f, 0.25f, 0.31f, 1f);
        minimapCamera.targetTexture = _renderTexture;
        minimapCamera.nearClipPlane = 0.1f;
        minimapCamera.farClipPlane = mapHeight + 20f;

        MinimapCameraFollow follow = _cameraObject.AddComponent<MinimapCameraFollow>();
        follow.mapHeight = mapHeight;

        _cameraObject.transform.SetPositionAndRotation(
            new Vector3(0f, mapHeight, 0f),
            Quaternion.Euler(90f, 0f, 0f)
        );
    }

    private void CreateMinimapUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("MinimapSystem bir Canvas veya Canvas altinda bulunmali.");
            return;
        }

        _uiRoot = new GameObject("Minimap", typeof(RectTransform), typeof(Image), typeof(Mask));
        RectTransform minimapRect = _uiRoot.GetComponent<RectTransform>();
        minimapRect.SetParent(canvas.transform, false);
        minimapRect.anchorMin = Vector2.one;
        minimapRect.anchorMax = Vector2.one;
        minimapRect.pivot = Vector2.one;
        minimapRect.anchoredPosition = new Vector2(-cornerOffset.x, -cornerOffset.y);
        minimapRect.sizeDelta = new Vector2(minimapSize, minimapSize);

        Image maskImage = _uiRoot.GetComponent<Image>();
        maskImage.sprite = CreateCircleMaskSprite();
        maskImage.color = Color.white;
        maskImage.raycastTarget = false;

        Mask mask = _uiRoot.GetComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject mapImageObject = new("MapImage", typeof(RectTransform), typeof(RawImage));
        RectTransform mapImageRect = mapImageObject.GetComponent<RectTransform>();
        mapImageRect.SetParent(minimapRect, false);
        Stretch(mapImageRect);

        RawImage mapImage = mapImageObject.GetComponent<RawImage>();
        mapImage.texture = _renderTexture;
        mapImage.color = Color.white;
        mapImage.raycastTarget = false;

        GameObject borderObject = new("Border", typeof(RectTransform), typeof(CircleOutlineGraphic));
        RectTransform borderRect = borderObject.GetComponent<RectTransform>();
        borderRect.SetParent(minimapRect, false);
        Stretch(borderRect);

        CircleOutlineGraphic border = borderObject.GetComponent<CircleOutlineGraphic>();
        border.raycastTarget = false;
        border.color = Color.white;
        border.SetThickness(6f);

        GameObject markerObject = new("LocalPlayerMarker", typeof(RectTransform), typeof(CircleOutlineGraphic));
        RectTransform markerRect = markerObject.GetComponent<RectTransform>();
        markerRect.SetParent(minimapRect, false);
        markerRect.anchorMin = new Vector2(0.5f, 0.5f);
        markerRect.anchorMax = new Vector2(0.5f, 0.5f);
        markerRect.pivot = new Vector2(0.5f, 0.5f);
        markerRect.anchoredPosition = Vector2.zero;
        markerRect.sizeDelta = new Vector2(16f, 16f);

        CircleOutlineGraphic marker = markerObject.GetComponent<CircleOutlineGraphic>();
        marker.raycastTarget = false;
        marker.color = new Color(0.15f, 1f, 0.2f, 1f);
        marker.SetThickness(8f);
    }

    private static void Stretch(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private Sprite CreateCircleMaskSprite()
    {
        const int size = 128;
        _circleMaskTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "RuntimeMinimapCircleMask",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color32[] pixels = new Color32[size * size];
        Vector2 center = new((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radiusSquared = center.x * center.x;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 offset = new Vector2(x, y) - center;
                byte alpha = offset.sqrMagnitude <= radiusSquared ? (byte)255 : (byte)0;
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        _circleMaskTexture.SetPixels32(pixels);
        _circleMaskTexture.Apply(false, true);

        _circleMaskSprite = Sprite.Create(
            _circleMaskTexture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f
        );
        _circleMaskSprite.name = "RuntimeMinimapCircleMaskSprite";
        return _circleMaskSprite;
    }
}
