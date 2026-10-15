#if UNITY_EDITOR
using System.IO;
using System.Reflection;
using Fusion;
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
    /// Kayisi (atilabilir iyilestirme) kurulumu. Var olanlara dokunmaz, eksikleri ekler:
    ///   - Doku ayarlari, materyal, 1 metreye olceklenmis model prefabi (KayisiModel)
    ///   - Yerdeki kayisi ag prefabi (ApricotPickup, can paketinden turetilir)
    ///   - Iyilestirme alani ag prefabi (ApricotHealZone)
    ///   - Resources/KayisiAyarlari (tum degerler Inspector'dan ayarlanir)
    ///   - Sahnedeki LootSpawner'a kayisi prefabi (haritada loot olarak cikar)
    ///   - Game_UI'da silah gostergesinin yaninda kayisi butonu (KayisiYuvasi)
    /// </summary>
    public static partial class MainMenuBuilder
    {
        private const string ApricotFolder = "Assets/GameArt/Imported/Props/Kayisi";
        private const string ApricotSettingsPath = "Assets/Resources/" + ApricotSettings.ResourcePath + ".asset";
        private const string ApricotMaterialPath = ApricotFolder + "/Kayisi_Mat.mat";
        private const string ApricotModelPath = ApricotFolder + "/KayisiModel.prefab";
        private const string ApricotIconPath = "Assets/GameArt/HudIcons/kayisi.png";
        private const string ApricotPickupPath = "Assets/Prefabs/Pickups/ApricotPickup.prefab";
        private const string ApricotZonePath = "Assets/Prefabs/Pickups/ApricotHealZone.prefab";
        private const string HealthPickupSource = "Assets/Prefabs/Pickups/HealthPickup.prefab";

        private static bool EnsureApricot(Canvas canvas, out string summary)
        {
            var parts = new System.Collections.Generic.List<string>();

            ConfigureApricotTextures();

            Material material = EnsureApricotMaterial(parts);
            GameObject model = EnsureApricotModel(material, parts);
            if (model == null)
            {
                summary = string.Empty;
                return false;
            }

            EnsureApricotPickup(model, parts);
            EnsureApricotZone(parts);
            EnsureApricotSettings(model, parts);
            EnsureApricotInLoot(parts);
            if (EnsureApricotSlot(canvas))
                parts.Add("sağ üstte kayısı butonu");

            summary = parts.Count > 0 ? "Kayısı: " + string.Join(", ", parts) : string.Empty;
            return parts.Count > 0;
        }

        private static void ConfigureApricotTextures()
        {
            SetTexture(ApricotFolder + "/Kayisi_BaseColor.png", TextureImporterType.Default, 512);
            SetTexture(ApricotFolder + "/Kayisi_Normal.png", TextureImporterType.NormalMap, 512);
            SetTexture(ApricotFolder + "/kayisi_siluet.png", TextureImporterType.Sprite, 128);
            SetTexture(ApricotFolder + "/duman.png", TextureImporterType.Sprite, 128);
            SetTexture(ApricotIconPath, TextureImporterType.Sprite, 256);
        }

        private static void SetTexture(string path, TextureImporterType type, int maxSize)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                return;

            if (importer.textureType == type && importer.maxTextureSize == maxSize)
                return;

            importer.textureType = type;
            importer.maxTextureSize = maxSize;
            if (type == TextureImporterType.Sprite)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
            }

            importer.SaveAndReimport();
        }

        private static Material EnsureApricotMaterial(System.Collections.Generic.List<string> parts)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(ApricotMaterialPath);
            if (material != null)
                return material;

            Shader shader = Shader.Find("NewBattle/SoftToon");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Lit");

            material = new Material(shader) { name = "Kayisi_Mat" };
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ApricotFolder + "/Kayisi_BaseColor.png");
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", Color.white);

            AssetDatabase.CreateAsset(material, ApricotMaterialPath);
            parts.Add("materyal");
            return material;
        }

        /// <summary>Model, ortalanmis ve en buyuk boyu 1 metre olacak sekilde prefab yapilir.</summary>
        private static GameObject EnsureApricotModel(Material material, System.Collections.Generic.List<string> parts)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(ApricotModelPath);
            if (existing != null)
                return existing;

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ApricotFolder + "/Kayisi.fbx");
            if (fbx == null)
            {
                Debug.LogError("Kayisi: " + ApricotFolder + "/Kayisi.fbx bulunamadi.");
                return null;
            }

            var root = new GameObject("KayisiModel");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.transform.SetParent(root.transform, false);

            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
                renderer.sharedMaterial = material;

            Bounds bounds = WorldBounds(renderers);
            float size = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (size > 0.0001f)
                instance.transform.localScale *= 1f / size;

            bounds = WorldBounds(renderers);
            instance.transform.localPosition -= bounds.center;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ApricotModelPath);
            Object.DestroyImmediate(root);
            parts.Add("model");
            return prefab;
        }

        private static Bounds WorldBounds(Renderer[] renderers)
        {
            Bounds bounds = default;
            bool first = true;
            foreach (Renderer renderer in renderers)
            {
                if (first)
                {
                    bounds = renderer.bounds;
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return bounds;
        }

        /// <summary>Can paketinin kopyasi: halka, parilti, ag ayarlari ayni; gorsel kayisi.</summary>
        private static void EnsureApricotPickup(GameObject model, System.Collections.Generic.List<string> parts)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ApricotPickupPath) != null)
                return;

            if (!AssetDatabase.CopyAsset(HealthPickupSource, ApricotPickupPath))
            {
                Debug.LogError("Kayisi: can paketi kopyalanamadi: " + HealthPickupSource);
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(ApricotPickupPath);
            try
            {
                RecoveryPickup old = contents.GetComponent<RecoveryPickup>();
                ApricotPickup pickup = contents.AddComponent<ApricotPickup>();
                if (old != null)
                {
                    var from = new SerializedObject(old);
                    var to = new SerializedObject(pickup);
                    foreach (string field in new[]
                             {
                                 "collectionSeconds", "collectionRadius", "ringWorldHeight", "ringGroundOffset",
                                 "progressColor", "ringColor", "blockedRingColor", "ringVisibleDistance", "glowRadius"
                             })
                    {
                        SerializedProperty property = from.FindProperty(field);
                        if (property != null)
                            to.CopyFromSerializedProperty(property);
                    }

                    to.ApplyModifiedPropertiesWithoutUndo();
                    Object.DestroyImmediate(old, true);
                }

                Transform modelSlot = FindDeep(contents.transform, "Model");
                if (modelSlot != null)
                {
                    foreach (MeshRenderer renderer in modelSlot.GetComponents<MeshRenderer>())
                        Object.DestroyImmediate(renderer, true);
                    foreach (MeshFilter filter in modelSlot.GetComponents<MeshFilter>())
                        Object.DestroyImmediate(filter, true);

                    var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, modelSlot);
                    visual.name = "Kayisi";
                    float parentScale = Mathf.Max(0.0001f, modelSlot.lossyScale.x);
                    visual.transform.localScale = Vector3.one * (0.6f / parentScale);
                    visual.transform.localPosition = Vector3.zero;
                    visual.transform.localRotation = Quaternion.Euler(-10f, 25f, 0f);
                }

                PrefabUtility.SaveAsPrefabAsset(contents, ApricotPickupPath);
                parts.Add("yerdeki kayısı prefabı");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void EnsureApricotZone(System.Collections.Generic.List<string> parts)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ApricotZonePath) != null)
                return;

            var zone = new GameObject("ApricotHealZone");
            zone.AddComponent<NetworkObject>();
            zone.AddComponent<ApricotHealZone>();
            PrefabUtility.SaveAsPrefabAsset(zone, ApricotZonePath);
            Object.DestroyImmediate(zone);
            parts.Add("iyileştirme alanı prefabı");
        }

        private static void EnsureApricotSettings(GameObject model, System.Collections.Generic.List<string> parts)
        {
            var settings = AssetDatabase.LoadAssetAtPath<ApricotSettings>(ApricotSettingsPath);
            bool created = false;
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<ApricotSettings>();
                Directory.CreateDirectory(Path.GetDirectoryName(ApricotSettingsPath));
                AssetDatabase.CreateAsset(settings, ApricotSettingsPath);
                created = true;
            }

            bool changed = created;
            if (settings.model == null) { settings.model = model; changed = true; }
            if (settings.icon == null) { settings.icon = AssetDatabase.LoadAssetAtPath<Sprite>(ApricotIconPath); changed = true; }
            if (settings.silhouette == null) { settings.silhouette = AssetDatabase.LoadAssetAtPath<Sprite>(ApricotFolder + "/kayisi_siluet.png"); changed = true; }
            if (settings.smoke == null) { settings.smoke = AssetDatabase.LoadAssetAtPath<Sprite>(ApricotFolder + "/duman.png"); changed = true; }

            string zoneGuid = AssetDatabase.AssetPathToGUID(ApricotZonePath);
            if (!settings.zonePrefab.IsValid && !string.IsNullOrEmpty(zoneGuid))
            {
                settings.zonePrefab = new NetworkPrefabRef(zoneGuid);
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                if (created)
                    parts.Add("Resources/KayisiAyarlari");
            }
        }

        private static void EnsureApricotInLoot(System.Collections.Generic.List<string> parts)
        {
            LootSpawner spawner = Object.FindFirstObjectByType<LootSpawner>(FindObjectsInactive.Include);
            string guid = AssetDatabase.AssetPathToGUID(ApricotPickupPath);
            if (spawner == null || string.IsNullOrEmpty(guid))
                return;

            FieldInfo field = typeof(LootSpawner).GetField("apricotPickupPrefab", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null || ((NetworkPrefabRef)field.GetValue(spawner)).IsValid)
                return;

            Undo.RecordObject(spawner, "Kayisi loot");
            field.SetValue(spawner, new NetworkPrefabRef(guid));
            EditorUtility.SetDirty(spawner);
            parts.Add("haritada loot olarak çıkıyor");
        }

        /// <summary>Silah gostergesinin solunda kayisi butonu (resim + sol altta sayi).</summary>
        private static bool EnsureApricotSlot(Canvas canvas)
        {
            if (canvas.GetComponentInChildren<ApricotSlotHUD>(true) != null)
                return false;

            var root = (RectTransform)canvas.transform;
            Vector2 topRight = new(1f, 1f);
            WeaponDisplayHUD weapon = canvas.GetComponentInChildren<WeaponDisplayHUD>(true);
            var weaponRect = weapon != null ? (RectTransform)weapon.transform : null;
            Vector2 size = new(140f, 140f);
            Vector2 position = weaponRect != null
                ? weaponRect.anchoredPosition - new Vector2(weaponRect.rect.width + 12f, 0f)
                : new Vector2(-445f, -25f);

            RectTransform slot = NewRect("KayisiYuvasi", weaponRect != null ? weaponRect.parent : root);
            Undo.RegisterCreatedObjectUndo(slot.gameObject, "Kayisi butonu");
            Place(slot, topRight, topRight, position, size);

            RectTransform background = NewRect("Zemin", slot);
            Stretch(background, 0f, 0f, 0f, 0f);
            Image backgroundImage = AddImage(background, Sprite("ui_round"), new Color(0f, 0f, 0f, 0.32f), raycast: true);
            backgroundImage.pixelsPerUnitMultiplier = 2.2f;

            RectTransform frameRect = NewRect("Cerceve", slot);
            Stretch(frameRect, 0f, 0f, 0f, 0f);
            Image frame = AddImage(frameRect, Sprite("ui_round_outline"), new Color(1f, 1f, 1f, 0.35f));
            frame.pixelsPerUnitMultiplier = 2.2f;

            RectTransform iconRect = NewRect("Resim", slot);
            Place(iconRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(4f, 6f), new Vector2(112f, 112f));
            Image icon = AddImage(iconRect, AssetDatabase.LoadAssetAtPath<Sprite>(ApricotIconPath), Color.white);
            icon.preserveAspect = true;

            TextMeshProUGUI count = AddText(slot, "Sayi", "1", 46f, Color.white, TextAlignmentOptions.BottomLeft, shadow: true);
            Place(count.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(10f, 2f), new Vector2(80f, 56f));

            Button button = slot.gameObject.AddComponent<Button>();
            button.targetGraphic = backgroundImage;
            button.transition = Selectable.Transition.None;

            CanvasGroup group = slot.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            ApricotSlotHUD hud = slot.gameObject.AddComponent<ApricotSlotHUD>();
            var serialized = new SerializedObject(hud);
            serialized.FindProperty("button").objectReferenceValue = button;
            serialized.FindProperty("icon").objectReferenceValue = icon;
            serialized.FindProperty("frame").objectReferenceValue = frame;
            serialized.FindProperty("countText").objectReferenceValue = count;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }
    }
}
#endif
