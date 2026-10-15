#if UNITY_EDITOR
using System.IO;
using NewBattle.Progression;
using UnityEditor;
using UnityEngine;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// Magazadaki ana menu arka planlari:
    ///   - Assets/GameArt/MenuBackgrounds icindeki resimler Sprite olarak ice aktarilir.
    ///   - Assets/Resources/ArkaPlanKutuphanesi yoksa hazir arka planlarla olusturulur
    ///     (varsa dokunulmaz; Inspector'dan yaptigin ayarlar kalir).
    /// </summary>
    public class MenuBackgroundSetup : AssetPostprocessor
    {
        private const string Folder = "Assets/GameArt/MenuBackgrounds";
        private const string LibraryPath = "Assets/Resources/" + MenuBackgroundLibrary.ResourcePath + ".asset";
        private const string DefaultImagePath = "Assets/GameArt/MenuUI/bg_gold.png";

        private static readonly (string id, string name, int price)[] Defaults =
        {
            ("gun_batimi", "Gün Batımı", 500),
            ("orman", "Orman", 500),
            ("col", "Çöl", 500),
            ("okyanus", "Okyanus", 600),
            ("gece", "Yıldızlı Gece", 750),
            ("karli_daglar", "Karlı Dağlar", 750),
        };

        private void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(Folder + "/"))
                return;

            ConfigureImporter((TextureImporter)assetImporter);
        }

        private static bool ConfigureImporter(TextureImporter importer)
        {
            bool changed = importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled ||
                           importer.maxTextureSize != 2048;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Compressed;
            return changed;
        }

        // Resimler bu betikten sonra ice aktarilabilir: aktarim bitince tekrar dene.
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (string path in imported)
            {
                if (path.Replace('\\', '/').StartsWith(Folder + "/"))
                {
                    EditorApplication.delayCall -= EnsureLibrary;
                    EditorApplication.delayCall += EnsureLibrary;
                    return;
                }
            }
        }

        [InitializeOnLoadMethod]
        private static void EnsureLibraryLater()
        {
            EditorApplication.delayCall += EnsureLibrary;
        }

        [MenuItem("Tools/NewBattle/Menü/Arka Plan Kütüphanesini Kur", false, 120)]
        private static void EnsureLibrary()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (AssetDatabase.LoadAssetAtPath<MenuBackgroundLibrary>(LibraryPath) != null)
                return;

            var library = ScriptableObject.CreateInstance<MenuBackgroundLibrary>();
            library.defaultImage = LoadSprite(DefaultImagePath);

            foreach ((string id, string name, int price) in Defaults)
            {
                string path = $"{Folder}/arkaplan_{id}.png";
                if (!File.Exists(path))
                    continue;

                Sprite image = LoadSprite(path);
                if (image == null)
                {
                    // Resim henuz ice aktarilmadi; aktarim bitince (OnPostprocessAllAssets) yeniden denenir.
                    Object.DestroyImmediate(library);
                    return;
                }

                library.backgrounds.Add(new MenuBackground
                {
                    id = id,
                    displayName = name,
                    image = image,
                    price = price
                });
            }

            Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));
            AssetDatabase.CreateAsset(library, LibraryPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Arka plan kutuphanesi olusturuldu: {LibraryPath} ({library.backgrounds.Count} arka plan)");
        }

        /// <summary>Resim henuz Sprite olarak alinmadiysa ayarlayip yeniden alir.</summary>
        private static Sprite LoadSprite(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter importer &&
                importer.textureType != TextureImporterType.Sprite &&
                path.StartsWith(Folder))
            {
                ConfigureImporter(importer);
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
#endif
