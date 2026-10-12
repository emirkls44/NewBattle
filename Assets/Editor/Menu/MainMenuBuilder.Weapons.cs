#if UNITY_EDITOR
using System.Reflection;
using NewBattle.Gameplay;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// Silah cesitliligi:
    ///  - Assets/Resources/SilahAyarlari (nadirlikler, yerde cikma olasiliklari, mermi oranlari)
    ///  - PlayerOnline > PlayerShooting > Weapons: Tabanca, Hafif Makineli, Pompali, Keskin Nisanci
    ///    (eksikse SONA eklenir; sira aga giden kimliktir)
    ///  - HUD: ikinci silah yuvasi (CombatSlots/RifleSlot2)
    /// </summary>
    public static partial class MainMenuBuilder
    {
        private const string WeaponSettingsPath = "Assets/Resources/SilahAyarlari.asset";
        private const int WeaponCatalogSize = 7;

        /// <summary>Yeni silahlarin elde/yerde gorunen modelleri (sira = silah kimligi 3..6).</summary>
        private static readonly string[] NewWeaponMeshes =
        {
            "Assets/GameArt/Meshes/Loot/Loot_Pistol.asset",
            "Assets/GameArt/Meshes/Loot/Loot_Smg.asset",
            "Assets/GameArt/Meshes/Loot/Loot_Shotgun.asset",
            "Assets/GameArt/Meshes/Loot/Loot_Sniper.asset"
        };

        private static bool EnsureWeaponSettings()
        {
            if (AssetDatabase.LoadAssetAtPath<WeaponLootSettings>(WeaponSettingsPath) != null)
                return false;

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<WeaponLootSettings>(), WeaponSettingsPath);
            AssetDatabase.SaveAssets();
            return true;
        }

        private static bool EnsureWeaponCatalog()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null)
                return false;

            FieldInfo field = typeof(PlayerShooting).GetField("weapons", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                return false;

            GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            bool changed = false;
            try
            {
                PlayerShooting shooting = contents.GetComponent<PlayerShooting>();
                if (shooting == null)
                    return false;

                var current = field.GetValue(shooting) as WeaponDefinition[];
                WeaponDefinition[] defaults = WeaponDefinition.CreateDefaultSet();
                int existing = current?.Length ?? 0;
                if (existing >= WeaponCatalogSize)
                    return false;

                var merged = new WeaponDefinition[WeaponCatalogSize];
                for (int i = 0; i < WeaponCatalogSize; i++)
                {
                    if (i < existing)
                    {
                        merged[i] = current[i];
                        continue;
                    }

                    merged[i] = defaults[i];
                    int meshIndex = i - 3;
                    if (meshIndex >= 0 && meshIndex < NewWeaponMeshes.Length)
                        merged[i].heldMesh = AssetDatabase.LoadAssetAtPath<Mesh>(NewWeaponMeshes[meshIndex]);
                }

                field.SetValue(shooting, merged);
                EditorUtility.SetDirty(shooting);
                PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
                changed = true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            return changed;
        }

        /// <summary>Sag ustteki silah kutusunun soluna ikinci yuvayi ekler.</summary>
        private static bool EnsureSecondWeaponSlot()
        {
            CombatLoadoutHUD hud = Object.FindFirstObjectByType<CombatLoadoutHUD>(FindObjectsInactive.Include);
            if (hud == null)
                return false;

            var serialized = new SerializedObject(hud);
            SerializedProperty secondButton = serialized.FindProperty("secondButton");
            if (secondButton == null || secondButton.objectReferenceValue != null)
                return false;

            Transform slots = hud.transform.name == "CombatSlots" ? hud.transform : hud.transform.Find("CombatSlots");
            Transform first = slots != null ? slots.Find("RifleSlot") : null;
            if (first == null)
                return false;

            Transform second = slots.Find("RifleSlot2");
            if (second == null)
            {
                second = Object.Instantiate(first.gameObject, slots).transform;
                second.name = "RifleSlot2";
                Undo.RegisterCreatedObjectUndo(second.gameObject, "Ikinci silah yuvasi");

                var firstRect = (RectTransform)first;
                var secondRect = (RectTransform)second;
                float width = firstRect.rect.width * Mathf.Abs(firstRect.localScale.x);
                secondRect.anchoredPosition = firstRect.anchoredPosition - new Vector2(width + 16f, 0f);
            }

            secondButton.objectReferenceValue = second.GetComponent<Button>();
            serialized.FindProperty("secondBackground").objectReferenceValue = second.GetComponent<Image>();
            serialized.FindProperty("secondText").objectReferenceValue = second.Find("Label")?.GetComponent<TextMeshProUGUI>();
            serialized.ApplyModifiedProperties();
            return true;
        }
    }
}
#endif
