using UnityEngine;

public class ShotTracer : MonoBehaviour
{
    private const float Lifetime = 0.09f;
    private static Material _sharedMaterial;
    private float _remainingTime;
    private LineRenderer _line;

    public static void Show(Vector3 start, Vector3 end)
    {
        GameObject tracerObject = new("RifleTracer");
        LineRenderer line = tracerObject.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.startWidth = 0.065f;
        line.endWidth = 0.025f;
        line.numCapVertices = 2;
        line.sharedMaterial = GetMaterial();
        line.startColor = new Color(1f, 0.9f, 0.2f, 1f);
        line.endColor = new Color(1f, 0.45f, 0.05f, 0.2f);

        ShotTracer tracer = tracerObject.AddComponent<ShotTracer>();
        tracer._line = line;
        tracer._remainingTime = Lifetime;
    }

    private void Update()
    {
        _remainingTime -= Time.deltaTime;
        float alpha = Mathf.Clamp01(_remainingTime / Lifetime);

        if (_line != null)
        {
            Color start = _line.startColor;
            Color end = _line.endColor;
            start.a = alpha;
            end.a = alpha * 0.3f;
            _line.startColor = start;
            _line.endColor = end;
        }

        if (_remainingTime <= 0f)
            Destroy(gameObject);
    }

    private static Material GetMaterial()
    {
        if (_sharedMaterial != null)
            return _sharedMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        _sharedMaterial = new Material(shader)
        {
            name = "RuntimeShotTracerMaterial"
        };
        return _sharedMaterial;
    }
}
