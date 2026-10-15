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
    /// Atilabilir esyalar (kayisi: iyilestirme, findik: yavaslatma). Var olanlara dokunmaz,
    /// eksikleri ekler; her biri icin:
    ///   - Doku ayarlari, materyal, 1 metreye olceklenmis model prefabi (KayisiModel / FindikModel)
    ///   - Yerdeki ag prefabi (ApricotPickup bileseniyle, can paketinden turetilir)
    ///   - Resources/KayisiAyarlari, Resources/FindikAyarlari (Inspector'dan ayarlanir)
    ///   - Sahnedeki LootSpawner'a loot kaydi
    ///   - Game_UI'da silah gostergesinin yaninda buton (KayisiYuvasi / FindikYuvasi)
    /// Iki esya ayni alan prefabini (ApricotHealZone) kullanir; etki ayardan gelir.
    /// </summary>
    public static partial class MainMenuBuilder
    {
        private const string ApricotFolder = "Assets/GameArt/Imported/Props/Kayisi";
        private const string ApricotZonePath = "Assets/Prefabs/Pickups/ApricotHealZone.prefab";
        private const string ThrowableSmokePath = ApricotFolder + "/duman.png";
        private const string HealthPickupSource = "Assets/Prefabs/Pickups/HealthPickup.prefab";

        private sealed class ThrowableSpec
        {
            public ThrowableKind Kind;
            public string Label;
            public string Folder;
            public string File;
            public string Silhouette;
            public string IconPath;
            public string SettingsPath;
            public string PickupPath;
            public string SlotName;
            public Color Glow;
            public System.Action<ApricotSettings> Defaults;

            public string Fbx => $"{Folder}/{File}.fbx";
            public string BaseColor => $"{Folder}/{File}_BaseColor.png";
            public string Normal => $"{Folder}/{File}_Normal.png";
            public string MaterialPath => $"{Folder}/{File}_Mat.mat";
            public string ModelPath => $"{Folder}/{File}Model.prefab";
        }

        private static readonly ThrowableSpec[] Throwables =
        {
            new()
            {
                Kind = ThrowableKind.Apricot, Label = "Kayısı", Folder = ApricotFolder, File = "Kayisi",
                Silhouette = ApricotFolder + "/kayisi_siluet.png", IconPath = "Assets/GameArt/HudIcons/kayisi.png",
                SettingsPath = "Assets/Resources/" + ApricotSettings.ResourcePath + ".asset",
                PickupPath = "Assets/Prefabs/Pickups/ApricotPickup.prefab", SlotName = "KayisiYuvasi",
                Glow = new Color(1f, 0.55f, 0.15f, 0.35f),
                Defaults = s => { s.kind = ThrowableKind.Apricot; s.effect = ThrowEffect.HealTeam; }
            },
            new()
            {
                Kind = ThrowableKind.Hazelnut, Label = "Fındık", Folder = "Assets/GameArt/Imported/Props/Findik", File = "Findik",
                Silhouette = "Assets/GameArt/Imported/Props/Findik/findik_siluet.png", IconPath = "Assets/GameArt/HudIcons/findik.png",
                SettingsPath = "Assets/Resources/" + ApricotSettings.HazelnutResourcePath + ".asset",
                PickupPath = "Assets/Prefabs/Pickups/HazelnutPickup.prefab", SlotName = "FindikYuvasi",
                Glow = new Color(0.62f, 0.4f, 0.2f, 0.35f),
                Defaults = s =>
                {
                    s.kind = ThrowableKind.Hazelnut;
                    s.effect = ThrowEffect.SlowEnemies;
                    s.slowMultiplier = 0.15f;
                    s.healPerSecond = 0f;
                    s.duration = 5f;
                    s.ringColor = new Color(0.7f, 0.45f, 0.2f, 0.95f);
                    s.fillColor = new Color(0.55f, 0.34f, 0.15f, 0.28f);
                    s.smokeColor = new Color(0.62f, 0.44f, 0.26f, 0.42f);
                    s.silhouetteColor = new Color(0.82f, 0.6f, 0.38f, 0.9f);
                }
            }
        };

        /// <summary>
        /// Kur'a basmayi unutma derdi olmasin: Unity derledikten sonra eksik bir atilabilir
        /// esya (yeni eklenen model, ayar dosyasi, loot kaydi ya da sag ustteki buton) varsa
        /// kendiliginden kurulur ve sahne kaydedilir.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void AutoInstallThrowablesLater()
        {
            EditorApplication.delayCall += AutoInstallThrowables;
        }

        private static void AutoInstallThrowables()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AutoInstallThrowables;
                return;
            }

            Canvas canvas = FindGameCanvas();
            if (canvas == null || !ThrowablesNeedInstall(canvas))
                return;

            if (!PrepareFont())
                return;
            PrepareMaterials();

            if (EnsureApricot(canvas, out string summary))
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(canvas.gameObject.scene);
                Debug.Log("Atilabilir esyalar otomatik kuruldu: " + summary);
            }
        }

        private static bool ThrowablesNeedInstall(Canvas canvas)
        {
            LootSpawner spawner = Object.FindFirstObjectByType<LootSpawner>(FindObjectsInactive.Include);
            ApricotSlotHUD[] slots = canvas.GetComponentsInChildren<ApricotSlotHUD>(true);

            foreach (ThrowableSpec spec in Throwables)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(spec.Fbx) == null)
                    continue;

                if (AssetDatabase.LoadAssetAtPath<ApricotSettings>(spec.SettingsPath) == null ||
                    AssetDatabase.LoadAssetAtPath<GameObject>(spec.PickupPath) == null)
                    return true;

                if (spawner != null)
                {
                    string fieldName = spec.Kind == ThrowableKind.Hazelnut ? "hazelnutPickupPrefab" : "apricotPickupPrefab";
                    FieldInfo field = typeof(LootSpawner).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                    if (field != null && !((NetworkPrefabRef)field.GetValue(spawner)).IsValid)
                        return true;
                }

                bool hasSlot = false;
                foreach (ApricotSlotHUD slot in slots)
                    hasSlot |= new SerializedObject(slot).FindProperty("kind").enumValueIndex == (int)spec.Kind;
                if (!hasSlot)
                    return true;
            }

            return false;
        }

        private static bool EnsureApricot(Canvas canvas, out string summary)
        {
            var all = new System.Collections.Generic.List<string>();
            EnsureApricotZone(all);

            for (int i = 0; i < Throwables.Length; i++)
            {
                ThrowableSpec spec = Throwables[i];
                if (AssetDatabase.LoadAssetAtPath<GameObject>(spec.Fbx) == null)
                    continue;

                var parts = new System.Collections.Generic.List<string>();
                ConfigureThrowableTextures(spec);
                Material material = EnsureThrowableMaterial(spec, parts);
                GameObject model = EnsureThrowableModel(spec, material, parts);
                if (model == null)
                    continue;

                EnsureThrowablePickup(spec, model, parts);
                EnsureThrowableSettings(spec, model, parts);
                EnsureThrowableInLoot(spec, parts);
                if (EnsureThrowableSlot(canvas, spec, i))
                    parts.Add("sağ üstte buton");

                if (parts.Count > 0)
                    all.Add(spec.Label + ": " + string.Join(", ", parts));
            }

            summary = string.Join(" · ", all);
            return all.Count > 0;
        }

        private static void ConfigureThrowableTextures(ThrowableSpec spec)
        {
            SetTexture(spec.BaseColor, TextureImporterType.Default, 512);
            SetTexture(spec.Normal, TextureImporterType.NormalMap, 512);
            SetTexture(spec.Silhouette, TextureImporterType.Sprite, 128);
            SetTexture(ThrowableSmokePath, TextureImporterType.Sprite, 128);
            SetTexture(spec.IconPath, TextureImporterType.Sprite, 256);
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

        private static Material EnsureThrowableMaterial(ThrowableSpec spec, System.Collections.Generic.List<string> parts)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(spec.MaterialPath);
            if (material != null)
                return material;

            Shader shader = Shader.Find("NewBattle/SoftToon");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Lit");

            material = new Material(shader) { name = spec.File + "_Mat" };
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(spec.BaseColor);
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", Color.white);

            AssetDatabase.CreateAsset(material, spec.MaterialPath);
            parts.Add("materyal");
            return material;
        }

        /// <summary>
        /// Model, ortalanmis ve en buyuk boyu 1 metre olacak sekilde prefab yapilir. Kayisi.fbx
        /// degistirilirse (yeni model) prefab ayni yerde (ayni kimlikle) yeniden kurulur;
        /// loot, elde ve havada gorunen kayisi kendiliginden yenisi olur.
        /// </summary>
        private static GameObject EnsureThrowableModel(ThrowableSpec spec, Material material, System.Collections.Generic.List<string> parts)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(spec.ModelPath);
            if (existing != null && ApricotModelIsCurrent(existing))
                return existing;

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(spec.Fbx);
            if (fbx == null)
            {
                Debug.LogError(spec.File + ": " + spec.Fbx + " bulunamadi.");
                return existing;
            }

            GameObject root = existing != null ? PrefabUtility.LoadPrefabContents(spec.ModelPath) : new GameObject(spec.File + "Model");
            var oldChildren = new System.Collections.Generic.List<GameObject>();
            foreach (Transform child in root.transform)
                oldChildren.Add(child.gameObject);
            foreach (GameObject child in oldChildren)
                Object.DestroyImmediate(child);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.transform.SetParent(root.transform, false);
            instance.transform.localPosition = Vector3.zero;

            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
                renderer.sharedMaterial = material;

            Bounds bounds = LocalMeshBounds(root.transform);
            float size = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (size > 0.0001f)
                instance.transform.localScale *= 1f / size;

            bounds = LocalMeshBounds(root.transform);
            instance.transform.localPosition -= bounds.center;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, spec.ModelPath);
            if (existing != null)
                PrefabUtility.UnloadPrefabContents(root);
            else
                Object.DestroyImmediate(root);

            parts.Add(existing != null ? "yeni model" : "model");
            return prefab;
        }

        /// <summary>Prefab gecerli mesh'i gosteriyor ve 1 metreye olceklenmis, ortalanmis mi.</summary>
        private static bool ApricotModelIsCurrent(GameObject prefab)
        {
            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                    return false;
            }

            Bounds bounds = LocalMeshBounds(prefab.transform);
            float size = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            return Mathf.Abs(size - 1f) < 0.05f && bounds.center.magnitude < 0.05f;
        }

        /// <summary>Mesh kutularinin koke gore birlesimi (prefab dosyasinda da calisir).</summary>
        private static Bounds LocalMeshBounds(Transform root)
        {
            Bounds result = default;
            bool first = true;
            Matrix4x4 toRoot = root.worldToLocalMatrix;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                    continue;

                Bounds mesh = filter.sharedMesh.bounds;
                Matrix4x4 matrix = toRoot * filter.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mesh.center + Vector3.Scale(mesh.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    Vector3 point = matrix.MultiplyPoint3x4(corner);
                    if (first)
                    {
                        result = new Bounds(point, Vector3.zero);
                        first = false;
                    }
                    else
                    {
                        result.Encapsulate(point);
                    }
                }
            }

            return result;
        }

        /// <summary>Can paketinin kopyasi: halka, parilti, ag ayarlari ayni; gorsel kayisi.</summary>
        private static void EnsureThrowablePickup(ThrowableSpec spec, GameObject model, System.Collections.Generic.List<string> parts)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(spec.PickupPath) != null)
                return;

            if (!AssetDatabase.CopyAsset(HealthPickupSource, spec.PickupPath))
            {
                Debug.LogError(spec.File + ": can paketi kopyalanamadi: " + HealthPickupSource);
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(spec.PickupPath);
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

                var pickupSettings = new SerializedObject(pickup);
                pickupSettings.FindProperty("kind").enumValueIndex = (int)spec.Kind;
                pickupSettings.FindProperty("apricotGlow").colorValue = spec.Glow;
                pickupSettings.ApplyModifiedPropertiesWithoutUndo();

                Transform modelSlot = FindDeep(contents.transform, "Model");
                if (modelSlot != null)
                {
                    foreach (MeshRenderer renderer in modelSlot.GetComponents<MeshRenderer>())
                        Object.DestroyImmediate(renderer, true);
                    foreach (MeshFilter filter in modelSlot.GetComponents<MeshFilter>())
                        Object.DestroyImmediate(filter, true);

                    var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, modelSlot);
                    visual.name = spec.File;
                    float parentScale = Mathf.Max(0.0001f, modelSlot.lossyScale.x);
                    visual.transform.localScale = Vector3.one * (0.6f / parentScale);
                    visual.transform.localPosition = Vector3.zero;
                    visual.transform.localRotation = Quaternion.Euler(-10f, 25f, 0f);
                }

                PrefabUtility.SaveAsPrefabAsset(contents, spec.PickupPath);
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

        private static void EnsureThrowableSettings(ThrowableSpec spec, GameObject model, System.Collections.Generic.List<string> parts)
        {
            var settings = AssetDatabase.LoadAssetAtPath<ApricotSettings>(spec.SettingsPath);
            bool created = false;
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<ApricotSettings>();
                spec.Defaults?.Invoke(settings);
                Directory.CreateDirectory(Path.GetDirectoryName(spec.SettingsPath));
                AssetDatabase.CreateAsset(settings, spec.SettingsPath);
                created = true;
            }

            bool changed = created;
            if (settings.kind != spec.Kind) { settings.kind = spec.Kind; changed = true; }
            if (settings.model == null) { settings.model = model; changed = true; }
            if (settings.icon == null) { settings.icon = AssetDatabase.LoadAssetAtPath<Sprite>(spec.IconPath); changed = true; }
            if (settings.silhouette == null) { settings.silhouette = AssetDatabase.LoadAssetAtPath<Sprite>(spec.Silhouette); changed = true; }
            if (settings.smoke == null) { settings.smoke = AssetDatabase.LoadAssetAtPath<Sprite>(ThrowableSmokePath); changed = true; }

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
                    parts.Add(Path.GetFileNameWithoutExtension(spec.SettingsPath) + " ayar dosyası");
            }
        }

        private static void EnsureThrowableInLoot(ThrowableSpec spec, System.Collections.Generic.List<string> parts)
        {
            LootSpawner spawner = Object.FindFirstObjectByType<LootSpawner>(FindObjectsInactive.Include);
            string guid = AssetDatabase.AssetPathToGUID(spec.PickupPath);
            if (spawner == null || string.IsNullOrEmpty(guid))
                return;

            string fieldName = spec.Kind == ThrowableKind.Hazelnut ? "hazelnutPickupPrefab" : "apricotPickupPrefab";
            FieldInfo field = typeof(LootSpawner).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null || ((NetworkPrefabRef)field.GetValue(spawner)).IsValid)
                return;

            Undo.RecordObject(spawner, spec.File + " loot");
            field.SetValue(spawner, new NetworkPrefabRef(guid));
            EditorUtility.SetDirty(spawner);
            parts.Add("haritada loot olarak çıkıyor");
        }

        /// <summary>Silah gostergesinin solunda kayisi butonu (resim + sol altta sayi).</summary>
        private static bool EnsureThrowableSlot(Canvas canvas, ThrowableSpec spec, int order)
        {
            foreach (ApricotSlotHUD existing in canvas.GetComponentsInChildren<ApricotSlotHUD>(true))
            {
                if (new SerializedObject(existing).FindProperty("kind").enumValueIndex == (int)spec.Kind)
                    return false;
            }

            var root = (RectTransform)canvas.transform;
            Vector2 topRight = new(1f, 1f);
            WeaponDisplayHUD weapon = canvas.GetComponentInChildren<WeaponDisplayHUD>(true);
            var weaponRect = weapon != null ? (RectTransform)weapon.transform : null;
            Vector2 size = new(140f, 140f);
            float offset = order * (size.x + 12f);
            Vector2 position = weaponRect != null
                ? weaponRect.anchoredPosition - new Vector2(weaponRect.rect.width + 12f + offset, 0f)
                : new Vector2(-445f - offset, -25f);

            RectTransform slot = NewRect(spec.SlotName, weaponRect != null ? weaponRect.parent : root);
            Undo.RegisterCreatedObjectUndo(slot.gameObject, spec.SlotName);
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
            Image icon = AddImage(iconRect, AssetDatabase.LoadAssetAtPath<Sprite>(spec.IconPath), Color.white);
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
            serialized.FindProperty("kind").enumValueIndex = (int)spec.Kind;
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
