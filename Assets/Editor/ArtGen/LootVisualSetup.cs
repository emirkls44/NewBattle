using System.Collections.Generic;
using NewBattle.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NewBattle.ArtGen
{
    /// <summary>
    /// Yerdeki loot'larin ve eldeki silahin gorunusunu yeniler:
    ///
    ///   1. Loot modelleri yeni yumusak stille yeniden uretilir (mesh asset'leri
    ///      yerinde guncellenir, referanslar kopmaz).
    ///   2. Dort pickup prefabi temizlenir ve yeniden giydirilir:
    ///        - Kokteki Unity kure/kupu kaldirilir (eskiden modelin altinda gorunuyordu)
    ///        - Kok olcegi 1 yapilir. Eskiden kok (0.25, 0.15, 1), model (45, 76, 11)
    ///          olcekliydi; birlesince model ~10 kat buyuk gorunuyordu.
    ///        - Her pickup kendi modelini alir: can -> ilk yardim cantasi,
    ///          kalkan -> iksir, mermi -> mermi kumesi, silah -> tufek.
    ///        - Mermi kutusundaki yanlis script (RiflePickup) AmmoPickup olur.
    ///        - Turune gore renkli parilti.
    ///   3. Oyuncunun elindeki kutu seklindeki silah yer tutucusu gercek tufek
    ///      modeliyle degisir; airdrop silahi elde altin tufek olarak gorunur.
    ///   4. Sahnedeki LootZoneManager kapaliysa (bolge basina 0 esya) acilir.
    ///
    /// Idempotent: tekrar calistirmak ayni sonucu verir.
    /// </summary>
    public static class LootVisualSetup
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/PlayerOnline.prefab";
        private const string PickupFolder = "Assets/Prefabs/Pickups";
        private const string CratePrefabPath = "Assets/GameArt/Prefabs/Loot/AirdropCrateNetworked.prefab";
        private const string LootMaterialPath = ArtGenIO.MaterialFolder + "/ToonWorld.mat";

        /// <summary>
        /// Pickup kokunun zeminden yuksekligi. LootZoneManager ve TestLootPlacer
        /// loot'u zeminin 0.65 m ustune dogurur.
        /// </summary>
        private const float SpawnHeight = 0.65f;

        /// <summary>Modelin alt yuzunun zeminden yuksekligi: yere degmeden hafifce suzulur.</summary>
        private const float HoverHeight = 0.12f;

        /// <summary>Eldeki tufegin olcegi. Karakter 2 m; tufek ~1 m, Battlelands'teki gibi iri.</summary>
        private const float HeldWeaponScale = 1.2f;

        /// <summary>Loot kapaliysa acilacak bolge basina esya araligi.</summary>
        private static readonly Vector2Int DefaultLootPerZone = new(3, 6);

        private struct PickupSpec
        {
            public string File;
            public LootFactory.LootKind Kind;
            public float Scale;
            public Vector3 Tilt;
            public Color Glow;
        }

        private static readonly PickupSpec[] Pickups =
        {
            new()
            {
                File = "HealthPickup", Kind = LootFactory.LootKind.MedKit, Scale = 2f,
                Tilt = Vector3.zero, Glow = new Color(1f, 0.36f, 0.34f, 0.38f)
            },
            new()
            {
                File = "ShieldPickup", Kind = LootFactory.LootKind.SmallShield, Scale = 2.2f,
                Tilt = Vector3.zero, Glow = new Color(0.3f, 0.72f, 1f, 0.38f)
            },
            new()
            {
                File = "AmmoPickup", Kind = LootFactory.LootKind.AmmoPack, Scale = 2.4f,
                Tilt = Vector3.zero, Glow = new Color(1f, 0.8f, 0.25f, 0.36f)
            },
            // Silah yan yatirilir: ustten bakan kamerada silueti ancak boyle okunur.
            new()
            {
                File = "RiflePickup", Kind = LootFactory.LootKind.AssaultRifle, Scale = 1.5f,
                Tilt = new Vector3(0f, 0f, 90f), Glow = new Color(0.92f, 0.94f, 1f, 0.3f)
            }
        };

        private static readonly Color CrateGlow = new(1f, 0.82f, 0.3f, 0.45f);

        /// <summary>Script degisirken yeni bilesene tasinan ayarlar.</summary>
        private static readonly string[] CarriedPickupFields =
        {
            "collectionSeconds", "collectionRadius", "ringWorldHeight", "ringGroundOffset",
            "progressColor", "ringVisibleDistance", "ammoAmount"
        };

        [MenuItem("Tools/NewBattle/Gorsel Yenileme/2 - Loot Gorunumunu Yenile", false, 41)]
        public static void Run()
        {
            List<string> log = new();

            if (!RunSilently(log))
            {
                Debug.LogError("Loot yenileme yarida kaldi:\n" + string.Join("\n", log));
                return;
            }

            Debug.Log("Loot gorunumu yenilendi:\n" + string.Join("\n", log));
        }

        public static bool RunSilently(List<string> log)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(LootMaterialPath);
            if (material == null)
            {
                log.Add($"HATA: {LootMaterialPath} bulunamadi.");
                return false;
            }

            Dictionary<LootFactory.LootKind, Mesh> meshes = RegenerateMeshes(log);

            foreach (PickupSpec spec in Pickups)
                RebuildPickup(spec, meshes[spec.Kind], material, log);

            SetGlow(CratePrefabPath, CrateGlow, 1.5f, log);
            InstallHeldWeapon(meshes, material, log);
            EnableGroundLoot(log);

            AssetDatabase.SaveAssets();
            return true;
        }

        #region Modeller

        private static Dictionary<LootFactory.LootKind, Mesh> RegenerateMeshes(List<string> log)
        {
            Dictionary<LootFactory.LootKind, Mesh> meshes = new();
            int vertices = 0;

            foreach (LootFactory.LootKind kind in System.Enum.GetValues(typeof(LootFactory.LootKind)))
            {
                Mesh mesh = ArtGenIO.SaveMesh(LootFactory.Build(kind), $"Loot_{kind}", "Loot");
                meshes[kind] = mesh;
                vertices += mesh.vertexCount;
            }

            log.Add($"Loot modelleri yeniden uretildi: {meshes.Count} model, toplam {vertices} vertex.");
            return meshes;
        }

        #endregion

        #region Pickup prefablari

        private static void RebuildPickup(PickupSpec spec, Mesh mesh, Material material, List<string> log)
        {
            string path = $"{PickupFolder}/{spec.File}.prefab";

            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                log.Add($"UYARI: {path} bulunamadi, atlandi.");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            List<string> changes = new();

            try
            {
                if (spec.Kind == LootFactory.LootKind.AmmoPack && FixAmmoScript(root))
                    changes.Add("script RiflePickup -> AmmoPickup");

                if (RemovePlaceholder(root))
                    changes.Add("kokteki yer tutucu gorunum kaldirildi");

                if (root.transform.localScale != Vector3.one)
                {
                    changes.Add($"kok olcegi {root.transform.localScale} -> (1, 1, 1)");
                    root.transform.localScale = Vector3.one;
                }

                int removed = RemoveOldVisuals(root);
                if (removed > 0)
                    changes.Add($"{removed} eski gorsel silindi");

                BuildVisual(root, spec, mesh, material);
                changes.Add($"yeni model: {spec.Kind} x{spec.Scale}");

                TimedLootPickup pickup = root.GetComponent<TimedLootPickup>();
                if (pickup != null)
                    WriteGlow(pickup, spec.Glow, 0.55f * spec.Scale);

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            log.Add($"{spec.File}: " + string.Join(", ", changes));
        }

        private static bool FixAmmoScript(GameObject root)
        {
            RiflePickup wrong = root.GetComponent<RiflePickup>();
            if (wrong == null || root.GetComponent<AmmoPickup>() != null)
                return false;

            AmmoPickup fresh = root.AddComponent<AmmoPickup>();
            SerializedObject source = new(wrong);
            SerializedObject target = new(fresh);

            foreach (string field in CarriedPickupFields)
            {
                SerializedProperty from = source.FindProperty(field);
                if (from != null)
                    target.CopyFromSerializedProperty(from);
            }

            target.ApplyModifiedPropertiesWithoutUndo();

            // NetworkObject'in bilesen listesi prefab kaydedilince Fusion
            // tarafindan yeniden bake ediliyor.
            Object.DestroyImmediate(wrong, true);
            return true;
        }

        /// <summary>Kokteki Unity kure/kup yer tutucusunu kaldirir; collider kalir.</summary>
        private static bool RemovePlaceholder(GameObject root)
        {
            MeshRenderer renderer = root.GetComponent<MeshRenderer>();
            MeshFilter filter = root.GetComponent<MeshFilter>();

            if (renderer != null)
                Object.DestroyImmediate(renderer, true);

            if (filter != null)
                Object.DestroyImmediate(filter, true);

            return renderer != null || filter != null;
        }

        private static int RemoveOldVisuals(GameObject root)
        {
            List<GameObject> doomed = new();

            foreach (Transform child in root.transform)
            {
                bool oldLootModel = PrefabUtility.IsOutermostPrefabInstanceRoot(child.gameObject) &&
                                    child.GetComponentInChildren<LootBob>(true) != null;

                if (oldLootModel || child.name == "Visual")
                    doomed.Add(child.gameObject);
            }

            foreach (GameObject gameObject in doomed)
                Object.DestroyImmediate(gameObject, true);

            return doomed.Count;
        }

        /// <summary>
        /// Visual (donme + suzulme) -> Model (egim + olcek + mesh).
        ///
        /// Iki kat olmasinin sebebi: LootBob her karede kendi rotasyonunu yazar.
        /// Egim ayni nesnede olsaydi LootBob onu silerdi.
        /// </summary>
        private static void BuildVisual(GameObject root, PickupSpec spec, Mesh mesh, Material material)
        {
            GameObject visual = new("Visual") { layer = root.layer };
            visual.transform.SetParent(root.transform, false);

            LootBob bob = visual.AddComponent<LootBob>();
            SerializedObject bobSettings = new(bob);
            bobSettings.FindProperty("rotationSpeed").floatValue = 30f;
            bobSettings.FindProperty("bobHeight").floatValue = 0.05f;
            bobSettings.FindProperty("bobSpeed").floatValue = 1.6f;
            bobSettings.ApplyModifiedPropertiesWithoutUndo();

            GameObject model = new("Model") { layer = root.layer };
            model.transform.SetParent(visual.transform, false);
            model.AddComponent<MeshFilter>().sharedMesh = mesh;
            model.AddComponent<MeshRenderer>().sharedMaterial = material;

            Quaternion tilt = Quaternion.Euler(spec.Tilt);
            model.transform.localRotation = tilt;
            model.transform.localScale = Vector3.one * spec.Scale;

            // Egim ve olcek uygulanmis haliyle sinir kutusu: alt yuz zemine
            // HoverHeight kadar yaklassin, XZ merkezi donme ekseninde olsun.
            Bounds bounds = TransformedBounds(mesh, tilt, spec.Scale);
            float groundY = -SpawnHeight;
            model.transform.localPosition = new Vector3(
                -bounds.center.x,
                groundY + HoverHeight - bounds.min.y,
                -bounds.center.z);
        }

        private static Bounds TransformedBounds(Mesh mesh, Quaternion rotation, float scale)
        {
            Vector3[] vertices = mesh.vertices;
            Bounds bounds = new(rotation * (vertices[0] * scale), Vector3.zero);

            for (int i = 1; i < vertices.Length; i++)
                bounds.Encapsulate(rotation * (vertices[i] * scale));

            return bounds;
        }

        private static void SetGlow(string prefabPath, Color color, float radius, List<string> log)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
                return;

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                TimedLootPickup pickup = root.GetComponent<TimedLootPickup>();
                if (pickup == null)
                    return;

                WriteGlow(pickup, color, radius);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                log.Add($"{System.IO.Path.GetFileNameWithoutExtension(prefabPath)}: altin parilti");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void WriteGlow(TimedLootPickup pickup, Color color, float radius)
        {
            SerializedObject serialized = new(pickup);
            serialized.FindProperty("glowColor").colorValue = color;
            serialized.FindProperty("glowRadius").floatValue = radius;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        #endregion

        #region Eldeki silah

        private static void InstallHeldWeapon(Dictionary<LootFactory.LootKind, Mesh> meshes, Material material,
            List<string> log)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null)
            {
                log.Add($"UYARI: {PlayerPrefabPath} bulunamadi, eldeki silah atlandi.");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

            try
            {
                GameObject rifleVisual = AnimationSetup.FindRifleVisual(root);
                PlayerShooting shooting = root.GetComponent<PlayerShooting>();

                if (rifleVisual == null || shooting == null)
                {
                    log.Add("UYARI: Silah gorseli (PlayerLoadout.rifleVisual) bulunamadi, eldeki silah atlandi.");
                    return;
                }

                // Soket eski el kemiginden kalma ~33 derecelik yana yatiklik
                // tasiyordu. Kutu seklinde yer tutucuda fark edilmiyordu, gercek
                // bir tufekte silah yan yatmis gorunurdu.
                Transform socket = rifleVisual.transform.parent;
                if (socket != null && socket != root.transform)
                    socket.localRotation = Quaternion.identity;

                MeshFilter filter = rifleVisual.GetComponentInChildren<MeshFilter>(true);
                if (filter == null)
                {
                    log.Add("UYARI: Silah gorselinde MeshFilter yok, eldeki silah atlandi.");
                    return;
                }

                Mesh rifle = meshes[LootFactory.LootKind.AssaultRifle];
                filter.sharedMesh = rifle;

                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                if (renderer != null)
                    renderer.sharedMaterial = material;

                // Namlu ucu ates noktasina denk gelsin: nisan cizgisi, iz ve
                // kovan zaten oradan cikiyor.
                Transform meshTransform = filter.transform;
                meshTransform.localRotation = Quaternion.identity;
                meshTransform.localScale = Vector3.one * HeldWeaponScale;

                float muzzle = rifle.bounds.max.z * HeldWeaponScale;
                float firePointZ = shooting.FirePoint != null && shooting.FirePoint.parent == rifleVisual.transform
                    ? shooting.FirePoint.localPosition.z
                    : muzzle;
                meshTransform.localPosition = new Vector3(0f, 0f, firePointZ - muzzle);

                int assigned = AssignHeldMeshes(shooting, meshes);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);

                log.Add($"Eldeki silah: kutu yer tutucusu -> tufek modeli (x{HeldWeaponScale}), " +
                        $"{assigned} silaha elde gorunen model atandi.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Silah dizisi: 0 yumruk, 1 tufek, 2 airdrop tufegi.</summary>
        private static int AssignHeldMeshes(PlayerShooting shooting, Dictionary<LootFactory.LootKind, Mesh> meshes)
        {
            SerializedObject serialized = new(shooting);
            SerializedProperty weapons = serialized.FindProperty("weapons");

            if (weapons == null || !weapons.isArray)
                return 0;

            (int index, LootFactory.LootKind kind)[] mapping =
            {
                (1, LootFactory.LootKind.AssaultRifle),
                (2, LootFactory.LootKind.LegendaryRifle)
            };

            int assigned = 0;

            foreach ((int index, LootFactory.LootKind kind) in mapping)
            {
                if (index >= weapons.arraySize)
                    continue;

                SerializedProperty held = weapons.GetArrayElementAtIndex(index).FindPropertyRelative("heldMesh");
                if (held == null)
                    continue;

                held.objectReferenceValue = meshes[kind];
                assigned++;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return assigned;
        }

        #endregion

        #region Sahne

        private static void EnableGroundLoot(List<string> log)
        {
            LootZoneManager manager = Object.FindFirstObjectByType<LootZoneManager>(FindObjectsInactive.Include);

            if (manager == null)
            {
                log.Add("Sahnede LootZoneManager yok; yer loot'u ayari atlandi.");
                return;
            }

            SerializedObject serialized = new(manager);
            SerializedProperty perZone = serialized.FindProperty("lootPerZone");

            if (perZone == null)
            {
                log.Add("UYARI: LootZoneManager.lootPerZone bulunamadi; yer loot'u ayari atlandi.");
                return;
            }

            if (perZone.vector2IntValue.y > 0)
            {
                log.Add($"Yer loot'u zaten acik (bolge basina {perZone.vector2IntValue}); dokunulmadi.");
                return;
            }

            // Undo'lu yaz: begenmezsen Ctrl+Z ile geri alinir.
            perZone.vector2IntValue = DefaultLootPerZone;
            serialized.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);

            log.Add($"Yer loot'u acildi: bolge basina {DefaultLootPerZone.x}-{DefaultLootPerZone.y} esya. " +
                    "SAHNEYI KAYDETMEYI UNUTMA (Ctrl+S).");
        }

        #endregion
    }
}
