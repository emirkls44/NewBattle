using NewBattle.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NewBattle.ArtGen
{
    /// <summary>
    /// Faz 2 bilesenlerini projeye baglar:
    ///   - Oyuncu prefab'ina Presence / Visibility / Ring / Footprint ekler
    ///   - Gizlenme calilarini uretir ve sahneye bir spawner koyar
    ///
    /// Elle 4 bileseni prefab'a suruklemek yerine tek komut; ayrica idempotent,
    /// yani ikinci kez calistirmak kopya bilesen eklemez.
    /// </summary>
    public static class GameplaySetup
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/PlayerOnline.prefab";
        private const string GrassMeshFolder = "Grass";

        [MenuItem("Tools/NewBattle/Faz 2 Kurulumu", false, 10)]
        public static void Run()
        {
            ArtGenIO.EnsureFolders();
            ArtGenTags.EnsureTags();

            Material toonMaterial = ArtGenIO.GetOrCreateToonMaterial("ToonWorld");

            int playerChanges = SetupPlayerPrefab();
            int grassChanges = SetupGrassPatches(toonMaterial);
            int zoneChanges = SetupLootZones();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"Faz 2 kurulumu tamam. Oyuncu prefab'ina {playerChanges} bilesen eklendi, " +
                $"{grassChanges} cimen varligi hazirlandi, " +
                $"{zoneChanges} loot bolge yoneticisi kuruldu.\n" +
                "Sahneyi kaydetmeyi unutma (Ctrl+S).");
        }

        #region Oyuncu prefab

        private static int SetupPlayerPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);

            if (prefab == null)
            {
                Debug.LogError($"Faz 2: {PlayerPrefabPath} bulunamadi.");
                return 0;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            int added = 0;

            try
            {
                if (root.GetComponent<PlayerCombatStats>() == null)
                {
                    Debug.LogError(
                        "Faz 2: PlayerOnline uzerinde PlayerCombatStats yok. " +
                        "Takim/dusman ayrimi bu bilesene dayaniyor, kurulum yarim kalir.");
                }

                added += AddIfMissing<PlayerPresence>(root);
                added += AddIfMissing<FootprintTrail>(root);
                added += AddIfMissing<PlayerVisibility>(root);
                added += AddIfMissing<PlayerRingIndicator>(root);
                added += AddIfMissing<ParachuteDescent>(root);

                AssignParachuteVisual(root);
                EnsureTriggerProbe(root, ref added);

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return added;
        }

        /// <summary>
        /// Parasut mesh'i Loot ureticisinde zaten uretiliyor (Loot_Parachute).
        /// Burada sadece prefab'a bagliyoruz; mesh yoksa once loot uretimini iste.
        /// </summary>
        private static void AssignParachuteVisual(GameObject root)
        {
            ParachuteDescent descent = root.GetComponent<ParachuteDescent>();
            if (descent == null)
                return;

            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(
                $"{ArtGenIO.MeshFolder}/Loot/Loot_Parachute.asset");

            if (mesh == null)
            {
                Debug.LogWarning(
                    "Faz 2: Loot_Parachute mesh'i bulunamadi. Art Generator penceresinde " +
                    "'Loot Modellerini Uret' butonuna basip bu komutu tekrar calistir. " +
                    "Inis yine calisir, sadece parasut gorseli olmaz.");
                return;
            }

            SerializedObject serialized = new(descent);
            serialized.FindProperty("parachuteMesh").objectReferenceValue = mesh;
            serialized.FindProperty("parachuteMaterial").objectReferenceValue =
                ArtGenIO.GetOrCreateToonMaterial("ToonWorld");
            serialized.ApplyModifiedProperties();
        }

        /// <summary>
        /// LootZoneManager'i mevcut bir sahne NetworkObject'ine ekler.
        ///
        /// Yeni bir sahne NetworkObject'i olusturmak yerine DropPhaseController'in
        /// nesnesini kullaniyoruz: o zaten kayitli ve calisan bir ag nesnesi,
        /// boylece ayri bir kayit/ID sorunu cikmiyor.
        /// </summary>
        private static int SetupLootZones()
        {
            if (Object.FindFirstObjectByType<LootZoneManager>(FindObjectsInactive.Include) != null)
                return 0;

            DropPhaseController dropPhase =
                Object.FindFirstObjectByType<DropPhaseController>(FindObjectsInactive.Include);

            if (dropPhase == null)
            {
                Debug.LogError(
                    "Faz 2: Sahnede DropPhaseController yok, LootZoneManager eklenemedi. " +
                    "Bolgesel loot devre disi kalir, eski toplu spawn calismaya devam eder.");
                return 0;
            }

            Undo.AddComponent<LootZoneManager>(dropPhase.gameObject);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            return 1;
        }

        private static int AddIfMissing<T>(GameObject target) where T : Component
        {
            if (target.GetComponent<T>() != null)
                return 0;

            target.AddComponent<T>();
            return 1;
        }

        /// <summary>
        /// Trigger olaylarini garantiye alan kinematik Rigidbody.
        ///
        /// CharacterController zaten trigger callback'i uretir; ancak oyuncu bir state'te
        /// CharacterController ile, baska bir state'te dogrudan transform ile hareket
        /// ediyor (PlayerHide/PlayerMoveState farki). Transform ile itilen bir collider
        /// Rigidbody yoksa trigger'i tetiklemeyebiliyor - o durumda gizlenme sessizce
        /// olu kalirdi. Kinematik + yercekimsiz oldugu icin fizigi hic etkilemez.
        /// </summary>
        private static void EnsureTriggerProbe(GameObject root, ref int added)
        {
            Rigidbody body = root.GetComponent<Rigidbody>();

            if (body == null)
            {
                body = root.AddComponent<Rigidbody>();
                added++;
            }

            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
        }

        #endregion

        #region Cimen

        private static int SetupGrassPatches(Material toonMaterial)
        {
            Mesh[] meshes = new Mesh[4];

            meshes[0] = ArtGenIO.SaveMesh(PropFactory.BuildTallGrass(4101, 2.4f), "GrassPatch_0", GrassMeshFolder);
            meshes[1] = ArtGenIO.SaveMesh(PropFactory.BuildTallGrass(4102, 3.0f), "GrassPatch_1", GrassMeshFolder);
            meshes[2] = ArtGenIO.SaveMesh(PropFactory.BuildBush(4103, 1.9f), "GrassPatch_2", GrassMeshFolder);
            meshes[3] = ArtGenIO.SaveMesh(PropFactory.BuildBush(4104, 2.4f), "GrassPatch_3", GrassMeshFolder);

            GrassPatchSpawner spawner =
                Object.FindFirstObjectByType<GrassPatchSpawner>(FindObjectsInactive.Include);

            if (spawner == null)
            {
                GameObject host = new("GrassPatches");
                Undo.RegisterCreatedObjectUndo(host, "Cimen Alanlari");
                spawner = host.AddComponent<GrassPatchSpawner>();
            }

            SerializedObject serialized = new(spawner);

            SerializedProperty meshArray = serialized.FindProperty("patchMeshes");
            meshArray.arraySize = meshes.Length;
            for (int i = 0; i < meshes.Length; i++)
                meshArray.GetArrayElementAtIndex(i).objectReferenceValue = meshes[i];

            serialized.FindProperty("patchMaterial").objectReferenceValue = toonMaterial;
            serialized.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            return meshes.Length;
        }

        #endregion
    }
}
