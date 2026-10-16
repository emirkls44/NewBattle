#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// Assets/GameArt/MenuUI klasorune giren her PNG'yi otomatik olarak UI
    /// sprite'i yapar. Gorselleri kendi cizimlerinle degistirdiginde ayar
    /// yapmana gerek kalmaz.
    /// </summary>
    public class MenuArtImporter : AssetPostprocessor
    {
        public const string Folder = "Assets/GameArt/MenuUI/";

        public const string MapFolder = "Assets/GameArt/DropMap/";

        private void OnPreprocessTexture()
        {
            // Harita resmi RawImage ile gosterilir: sprite degil, duz doku.
            if (assetPath.StartsWith(MapFolder))
            {
                var map = (TextureImporter)assetImporter;
                map.textureType = TextureImporterType.Default;
                map.mipmapEnabled = false;
                map.wrapMode = TextureWrapMode.Clamp;
                map.maxTextureSize = 4096;
                map.textureCompression = TextureImporterCompression.CompressedHQ;
                return;
            }

            if (!assetPath.StartsWith(Folder) || !assetPath.EndsWith(".png"))
                return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            // Kose yaricapi 32 px; 9-slice ile her boyutta keskin kalir.
            if (assetPath.EndsWith("ui_round.png") || assetPath.EndsWith("ui_round_outline.png"))
                importer.spriteBorder = new Vector4(36f, 36f, 36f, 36f);

            // Sonbahar ortak parcalari (Tools/MenuArt/ortak_parcalar.py): sol, alt, sag, ust.
            string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            if (AutumnBorders.TryGetValue(file, out Vector4 border))
                importer.spriteBorder = border;
            else if (file.StartsWith("buton_"))
                importer.spriteBorder = new Vector4(56f, 66f, 56f, 56f);
        }

        private static readonly System.Collections.Generic.Dictionary<string, Vector4> AutumnBorders = new()
        {
            { "ahsap_panel", new Vector4(48f, 60f, 48f, 48f) },
            { "ahsap_baslik", new Vector4(36f, 36f, 36f, 36f) },
            { "parsomen", new Vector4(30f, 30f, 30f, 30f) },
            { "yazi_kutusu", new Vector4(24f, 24f, 24f, 24f) },
            { "cip_beyaz", new Vector4(26f, 30f, 26f, 26f) },
            { "kamuflaj_panel", new Vector4(42f, 42f, 42f, 42f) },
            { "kamuflaj_baslik", new Vector4(36f, 36f, 36f, 36f) },
        };
    }
}
#endif
