using UnityEngine;
using UnityEngine.UI;

public class CircularMinimapGraphic : MaskableGraphic
{
    [SerializeField] private Texture minimapTexture;
    [SerializeField, Range(24, 128)] private int segments = 64;

    public override Texture mainTexture => minimapTexture != null
        ? minimapTexture
        : Texture2D.whiteTexture;

    public void SetTexture(Texture texture)
    {
        minimapTexture = texture;
        SetMaterialDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        Rect rect = GetPixelAdjustedRect();
        Vector2 center = rect.center;
        float radius = Mathf.Min(rect.width, rect.height) * 0.5f;

        vertexHelper.AddVert(center, color, new Vector2(0.5f, 0.5f));

        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.PI * 2f * i / segments;
            Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 position = center + direction * radius;
            Vector2 uv = direction * 0.5f + Vector2.one * 0.5f;
            vertexHelper.AddVert(position, color, uv);
        }

        for (int i = 0; i < segments; i++)
            vertexHelper.AddTriangle(0, i + 1, i + 2);
    }
}
