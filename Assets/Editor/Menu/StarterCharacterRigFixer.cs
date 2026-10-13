#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// Ahmet / Ayse modellerinin Humanoid kemik eslestirmesini Unity acilir acilmaz
    /// (ve her derlemeden sonra) kontrol eder; elle eslestirme yoksa uygular. Kur'u
    /// calistirmayi beklemez. Elle de: Tools > NewBattle > Karakter Iskeletlerini Onar.
    /// </summary>
    [InitializeOnLoad]
    internal static class StarterCharacterRigFixer
    {
        private static readonly string[] Models =
        {
            "Assets/Arts/Characters/Character_Ahmet/Ahmet.fbx",
            "Assets/Arts/Characters/Character_Ayse/Ayse.fbx"
        };

        static StarterCharacterRigFixer()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    FixAll(force: false);
            };
        }

        [MenuItem("Tools/NewBattle/Karakter Iskeletlerini Onar")]
        private static void FixFromMenu() => FixAll(force: true);

        private static void FixAll(bool force)
        {
            foreach (string path in Models)
            {
                if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
                    continue;

                bool mapped = importer.humanDescription.human != null && importer.humanDescription.human.Length > 0;
                if (mapped && !force)
                    continue;

                LogCurrentMapping(path);
                if (force && mapped)
                {
                    // Zorla: once eslestirmeyi bosalt ki yeniden kurulsun.
                    HumanDescription description = importer.humanDescription;
                    description.human = new HumanBone[0];
                    importer.humanDescription = description;
                }

                MainMenuBuilder.RepairStarterRig(path);
            }
        }

        /// <summary>Unity'nin su anki (otomatik) eslestirmesinden onemli kemikleri Console'a yazar.</summary>
        private static void LogCurrentMapping(string path)
        {
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            if (avatar == null)
            {
                Debug.Log($"[Iskelet] {path}: avatar yok.");
                return;
            }

            string[] keys = { "Hips", "Spine", "Chest", "UpperChest", "Neck", "Head", "LeftUpperArm", "LeftUpperLeg" };
            var lines = avatar.humanDescription.human
                .Where(h => keys.Contains(h.humanName))
                .Select(h => $"{h.humanName}={h.boneName}");
            Debug.Log($"[Iskelet] {System.IO.Path.GetFileNameWithoutExtension(path)} ONCEKI eslestirme " +
                      $"(Humanoid={avatar.isHuman}): " + string.Join(", ", lines));
        }
    }
}
#endif
