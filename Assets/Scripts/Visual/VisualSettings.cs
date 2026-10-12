using System;
using UnityEngine;

/// <summary>
/// GORSEL AYARLAR: kamera yakinligi ve renkler tek yerde.
///
/// Hierarchy'deki "GorselAyarlar" nesnesini sec; Inspector'dan degistir.
/// Play'deyken de aninda uygulanir ve kalici olur (materyaller dosyadir).
/// </summary>
[ExecuteAlways]
public class VisualSettings : MonoBehaviour
{
    [Serializable]
    public class ColorGroup
    {
        [Tooltip("Ayarlanacak materyal (SoftToon).")]
        public Material material;

        [Tooltip("Vertex renklerini girildigi gibi (sRGB) goster. Kapaliysa renkler soluk/pembemsi gorunur.")]
        public bool vertexColorIsSRGB = true;

        [Tooltip("Renk canliligi. 1 = oldugu gibi, 0 = gri, 2 = cok canli.")]
        [Range(0f, 2f)] public float saturation = 1f;

        [Tooltip("Genel parlaklik. 1 = oldugu gibi.")]
        [Range(0.5f, 2f)] public float brightness = 1f;

        [Tooltip("Golgede kalan tarafin parlakligi. 1 = golge yok gibi.")]
        [Range(0f, 1f)] public float shadowBrightness = 0.65f;

        [Tooltip("Kenarlardaki beyaz parlama.")]
        [Range(0f, 1f)] public float rimStrength = 0.16f;

        public void Apply()
        {
            if (material == null)
                return;

            Set("_VertexColorSRGB", vertexColorIsSRGB ? 1f : 0f);
            Set("_Saturation", saturation);
            Set("_Brightness", brightness);
            Set("_Ambient", shadowBrightness);
            Set("_RimStrength", rimStrength);
        }

        /// <summary>Materyaldeki mevcut degerleri okur (kurulumda bir kez).</summary>
        public void ReadFrom(Material source)
        {
            material = source;
            if (source == null)
                return;

            vertexColorIsSRGB = Get("_VertexColorSRGB", 0f) > 0.5f;
            saturation = Get("_Saturation", 1f);
            brightness = Get("_Brightness", 1f);
            shadowBrightness = Get("_Ambient", 0.65f);
            rimStrength = Get("_RimStrength", 0.16f);
        }

        private void Set(string property, float value)
        {
            if (material.HasProperty(property) && !Mathf.Approximately(material.GetFloat(property), value))
                material.SetFloat(property, value);
        }

        private float Get(string property, float fallback) =>
            material != null && material.HasProperty(property) ? material.GetFloat(property) : fallback;
    }

    [Header("Kamera")]
    [Tooltip("Oyun kamerasinin yakinligi. KUCUK = YAKIN (karakterler buyuk), BUYUK = UZAK.")]
    [Range(4f, 20f)] public float cameraViewSize = 6f;

    [Header("Yerdeki Esyalar (ilk yardim, silah, mermi, kalkan)")]
    public ColorGroup loot = new();

    [Header("Harita (zemin, cimen, agac, kaya)")]
    public ColorGroup world = new();

    private void OnEnable() => Apply();

    private void OnValidate() => Apply();

    public void Apply()
    {
        CameraFollow follow = CameraFollow.Active != null ? CameraFollow.Active : FindFirstObjectByType<CameraFollow>();
        if (follow != null && !Mathf.Approximately(follow.ViewSize, cameraViewSize))
            follow.ViewSize = cameraViewSize;

        loot?.Apply();
        world?.Apply();
    }
}
