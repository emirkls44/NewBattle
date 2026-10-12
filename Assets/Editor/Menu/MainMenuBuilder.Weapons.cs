#if UNITY_EDITOR
using System.Reflection;
using NewBattle.Gameplay;
using NewBattle.UI;
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
    ///  - HUD: minimap'in yaninda silah gostergesi (resim + mermi)
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

        private const string WeaponIconFolder = "Assets/GameArt/HudIcons/Weapons";

        /// <summary>
        /// Minimap'in soluna silah gostergesi (yari saydam kutu + silah/yumruk resmi + mermi).
        /// Eski sag ust silah kutulari (CombatSlots) gizlenir. Ikonlari baglar.
        /// </summary>
        private static bool EnsureWeaponDisplay(Canvas canvas)
        {
            bool changed = false;
            var root = (RectTransform)canvas.transform;

            WeaponDisplayHUD display = canvas.GetComponentInChildren<WeaponDisplayHUD>(true);
            if (display == null)
            {
                display = BuildWeaponDisplay(root);
                changed = true;
            }

            // Ikonlar sonradan uretilirse (Tools/Icons/*.py) Kur tekrar baglar.
            var serialized = new SerializedObject(display);
            SerializedProperty icons = serialized.FindProperty("icons");
            Sprite[] sprites = LoadWeaponIcons();
            bool iconsChanged = icons.arraySize != sprites.Length;
            for (int i = 0; !iconsChanged && i < sprites.Length; i++)
                iconsChanged = icons.GetArrayElementAtIndex(i).objectReferenceValue != sprites[i];

            if (iconsChanged)
            {
                icons.arraySize = sprites.Length;
                for (int i = 0; i < sprites.Length; i++)
                    icons.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
                serialized.ApplyModifiedProperties();
                changed = true;
            }

            // Eski sag ust silah kutulari: yerini yeni gosterge aldi.
            CombatLoadoutHUD oldHud = Object.FindFirstObjectByType<CombatLoadoutHUD>(FindObjectsInactive.Include);
            if (oldHud != null)
            {
                Transform slots = oldHud.transform.name == "CombatSlots" ? oldHud.transform : oldHud.transform.Find("CombatSlots");
                if (slots != null && slots.gameObject.activeSelf)
                {
                    Undo.RecordObject(slots.gameObject, "Eski silah kutulari");
                    slots.gameObject.SetActive(false);
                    changed = true;
                }

                if (oldHud.enabled)
                {
                    Undo.RecordObject(oldHud, "Eski silah kutulari");
                    oldHud.enabled = false;
                    changed = true;
                }
            }

            return changed;
        }

        private static WeaponDisplayHUD BuildWeaponDisplay(RectTransform canvas)
        {
            Vector2 topRight = new(1f, 1f);
            var minimap = canvas.Find("Minimap") as RectTransform;
            Vector2 position = minimap != null
                ? minimap.anchoredPosition - new Vector2(minimap.rect.width + 14f, 0f)
                : new Vector2(-259f, -25f);
            float height = minimap != null ? minimap.rect.height : 220f;

            RectTransform panel = NewRect("SilahGostergesi", canvas);
            Undo.RegisterCreatedObjectUndo(panel.gameObject, "Silah gostergesi");
            Place(panel, topRight, topRight, position, new Vector2(170f, height));

            RectTransform background = NewRect("Zemin", panel);
            Stretch(background, 0f, 0f, 0f, 0f);
            AddImage(background, Sprite("ui_round"), new Color(0f, 0f, 0f, 0.32f)).pixelsPerUnitMultiplier = 2.2f;

            RectTransform frameRect = NewRect("Cerceve", panel);
            Stretch(frameRect, 0f, 0f, 0f, 0f);
            Image frame = AddImage(frameRect, Sprite("ui_round_outline"), new Color(1f, 1f, 1f, 0.35f));
            frame.pixelsPerUnitMultiplier = 2.2f;

            RectTransform iconRect = NewRect("Resim", panel);
            Place(iconRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(154f, 150f));
            Image icon = AddImage(iconRect, null, Color.white);
            icon.type = Image.Type.Simple;
            icon.preserveAspect = true;

            TextMeshProUGUI ammo = AddText(panel, "Mermi", "30", 54f, Color.white, TextAlignmentOptions.Center, shadow: true);
            Place(ammo.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(160f, 64f));

            WeaponDisplayHUD component = panel.gameObject.AddComponent<WeaponDisplayHUD>();
            var serialized = new SerializedObject(component);
            serialized.FindProperty("icon").objectReferenceValue = icon;
            serialized.FindProperty("ammoText").objectReferenceValue = ammo;
            serialized.FindProperty("rarityFrame").objectReferenceValue = frame;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return component;
        }

        /// <summary>weapon_0..weapon_N.png (0 = yumruk); sprite olarak ice aktarir.</summary>
        private static Sprite[] LoadWeaponIcons()
        {
            var sprites = new System.Collections.Generic.List<Sprite>();
            for (int id = 0; id < WeaponCatalogSize; id++)
            {
                string path = $"{WeaponIconFolder}/weapon_{id}.png";
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                {
                    sprites.Add(null);
                    continue;
                }

                if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.SaveAndReimport();
                }

                sprites.Add(AssetDatabase.LoadAssetAtPath<Sprite>(path));
            }

            return sprites.ToArray();
        }
    }
}
#endif
