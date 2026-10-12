using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Bu kamera cizerken SoftToon karakterleri sahnenin gunesi yerine kameranin
/// tarafindan (hafif soldan-yukaridan) gelen bir isikla aydinlatir.
///
/// Menudeki ve mac sonundaki karakter onizlemesinde yuz hep aydinlik olsun
/// diye var: gunes karakterin arkasindan vurunca yuz golgede kaliyordu.
/// Diger kameralar (oyun kamerasi) etkilenmez.
/// </summary>
[RequireComponent(typeof(Camera))]
public class ToonPreviewLight : MonoBehaviour
{
    private static readonly int DirectionId = Shader.PropertyToID("_ToonPreviewLightDir");
    private static readonly int ColorId = Shader.PropertyToID("_ToonPreviewLightColor");

    [Tooltip("Isigin kameraya gore yonu (derece): x = yukaridan, y = soldan.")]
    [SerializeField] private Vector2 angles = new(28f, 30f);
    [SerializeField] private Color color = new(1.05f, 1.02f, 0.98f, 1f);

    private Camera _camera;

    private void Awake() => _camera = GetComponent<Camera>();

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBegin;
        RenderPipelineManager.endCameraRendering += OnEnd;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBegin;
        RenderPipelineManager.endCameraRendering -= OnEnd;
        Shader.SetGlobalVector(DirectionId, Vector4.zero);
    }

    private void OnBegin(ScriptableRenderContext context, Camera rendering)
    {
        if (rendering != _camera)
            return;

        // Yuzeyden isiga dogru: kameranin arkasindan, biraz yukaridan ve soldan.
        Transform view = _camera.transform;
        // (Pozitif aci: -forward'i saga eksende yukari, yukari eksende sola cevirir.)
        Vector3 toLight = Quaternion.AngleAxis(angles.y, view.up) *
                          Quaternion.AngleAxis(angles.x, view.right) * -view.forward;

        Shader.SetGlobalVector(DirectionId, new Vector4(toLight.x, toLight.y, toLight.z, 1f));
        Shader.SetGlobalVector(ColorId, color);
    }

    private void OnEnd(ScriptableRenderContext context, Camera rendering)
    {
        if (rendering == _camera)
            Shader.SetGlobalVector(DirectionId, Vector4.zero);
    }
}
