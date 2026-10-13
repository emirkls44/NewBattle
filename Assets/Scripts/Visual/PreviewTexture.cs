using UnityEngine;

/// <summary>
/// Karakter onizlemeleri (menu, karakter secimi, mac sonu) icin RenderTexture.
///
/// Kenar yumusatma (MSAA) ornek sayisini cihazin destekledigi degere indirir:
/// bazi telefon GPU'lari (Mali) 2x'i desteklemiyor ve 2x istenen onizleme
/// telefonda bos kaliyordu (editorde gorunuyordu).
/// </summary>
public static class PreviewTexture
{
    public static RenderTexture Create(int width, int height, string name, int msaa = 4, int depth = 24)
    {
        var descriptor = new RenderTextureDescriptor(Mathf.Max(16, width), Mathf.Max(16, height), RenderTextureFormat.ARGB32, depth)
        {
            msaaSamples = 1,
            useMipMap = false,
            autoGenerateMips = false,
            sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear
        };

        // 4 -> 2 -> 1: desteklenen ilk deger (0 donerse desteklenmiyor).
        for (int samples = Mathf.ClosestPowerOfTwo(Mathf.Clamp(msaa, 1, 8)); samples > 1; samples /= 2)
        {
            descriptor.msaaSamples = samples;
            if (SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor) == samples)
                break;
            descriptor.msaaSamples = 1;
        }

        var texture = new RenderTexture(descriptor) { name = name };
        texture.Create();
        return texture;
    }
}
