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

        private void OnPreprocessTexture()
        {
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
            if (assetPath.EndsWith("ui_round.png"))
                importer.spriteBorder = new Vector4(36f, 36f, 36f, 36f);
        }
    }
}
#endif
