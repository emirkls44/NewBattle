using UnityEngine;
using UnityEngine.UI;

public class CircleOutlineGraphic : MaskableGraphic
{
    [SerializeField, Range(24, 128)] private int segments = 64;
    [SerializeField] private float thickness = 6f;

    public void SetThickness(float value)
    {
        thickness = Mathf.Max(0f, value);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        Rect rect = GetPixelAdjustedRect();
        Vector2 center = rect.center;
        float outerRadius = Mathf.Min(rect.width, rect.height) * 0.5f;
        float innerRadius = Mathf.Max(0f, outerRadius - thickness);

        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.PI * 2f * i / segments;
            Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));

            vertexHelper.AddVert(center + direction * outerRadius, color, Vector2.zero);
            vertexHelper.AddVert(center + direction * innerRadius, color, Vector2.zero);
        }

        for (int i = 0; i < segments; i++)
        {
            int index = i * 2;
            vertexHelper.AddTriangle(index, index + 2, index + 1);
            vertexHelper.AddTriangle(index + 2, index + 3, index + 1);
        }
    }
}
