using Fusion;
using NewBattle.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NewBattle.ArtGen
{
    /// <summary>
    /// Faz 3-5 kurulumu:
    ///   - Oyuncu prefab'ina nokta nokta nisan cizgisini ekler
    ///   - Airdrop sandigi prefab'ini uretir (NetworkObject + toplama + parasut)
    ///   - Sahneye AirdropController koyup prefab'i baglar
    ///
    /// Faz 2 kurulumu gibi idempotent: tekrar calistirmak kopya uretmez.
    /// </summary>
    public static class CombatSetup
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/PlayerOnline.prefab";
        private const string CratePrefabPath = ArtGenIO.PrefabFolder + "/Loot/AirdropCrateNetworked.prefab";

        [MenuItem("Tools/NewBattle/Faz 3-5 Kurulumu", false, 11)]
        public static void Run()
        {
            ArtGenIO.EnsureFolders();

            int playerChanges = SetupPlayerPrefab();
            NetworkObject crate = BuildCratePrefab();
            int controllerChanges = SetupAirdropController(crate);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"Faz 3-5 kurulumu tamam. Oyuncuya {playerChanges} bilesen eklendi, " +
                $"airdrop sandigi {(crate != null ? "hazir" : "URETILEMEDI")}, " +
                $"{controllerChanges} kontrolcu kuruldu.\n" +
                "Sahneyi kaydetmeyi unutma (Ctrl+S).");
        }

        #region Oyuncu

        private static int SetupPlayerPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null)
            {
                Debug.LogError($"Faz 3-5: {PlayerPrefabPath} bulunamadi.");
                return 0;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            int added = 0;

            try
            {
                if (root.GetComponent<AimLineIndicator>() == null)
                {
                    root.AddComponent<AimLineIndicator>();
                    added++;
                }

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return added;
        }

        #endregion

        #region Airdrop sandigi

        /// <summary>
        /// Sandik prefab'ini sifirdan kurar. Mesh'ler Art Generator'da uretilen
        /// Loot_AirdropCrate ve Loot_Parachute asset'leridir.
        /// </summary>
        private static NetworkObject BuildCratePrefab()
        {
            Mesh crateMesh = LoadLootMesh("Loot_AirdropCrate");
            Mesh parachuteMesh = LoadLootMesh("Loot_Parachute");

            if (crateMesh == null)
            {
                Debug.LogError(
                    "Faz 3-5: Loot_AirdropCrate mesh'i yok. Art Generator > " +
                    "'Loot Modellerini Uret' calistirip tekrar dene.");
                return null;
            }

            Material material = ArtGenIO.GetOrCreateToonMaterial("ToonWorld");

            GameObject root = new("AirdropCrateNetworked");

            try
            {
                root.AddComponent<NetworkObject>();
                root.AddComponent<NetworkTransform>();

                // Sandik kati bir nesne: oyuncu icinden gecemesin, ustune siper alabilsin.
                BoxCollider collider = root.AddComponent<BoxCollider>();
                collider.center = crateMesh.bounds.center;
                collider.size = crateMesh.bounds.size;

                AirdropCratePickup pickup = root.AddComponent<AirdropCratePickup>();

                ArtGenIO.CreateMeshObject("CrateVisual", crateMesh, material, root.transform);

                if (parachuteMesh != null)
                {
                    GameObject parachute = ArtGenIO.CreateMeshObject(
                        "ParachuteVisual", parachuteMesh, material, root.transform);
                    parachute.transform.localPosition = new Vector3(0f, 1.9f, 0f);

                    SerializedObject serialized = new(pickup);
                    serialized.FindProperty("parachuteVisual").objectReferenceValue = parachute.transform;

                    // Toplama suresi 3 saniye: normal loot'tan belirgin sekilde uzun,
                    // boylece sandigi acmak risk almayi gerektirir.
                    SerializedProperty collectionSeconds = serialized.FindProperty("collectionSeconds");
                    if (collectionSeconds != null)
                        collectionSeconds.floatValue = 3f;

                    SerializedProperty collectionRadius = serialized.FindProperty("collectionRadius");
                    if (collectionRadius != null)
                        collectionRadius.floatValue = 1.9f;

                    serialized.ApplyModifiedProperties();
                }

                ArtGenIO.EnsureFolder(ArtGenIO.PrefabFolder + "/Loot");
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, CratePrefabPath);
                return saved != null ? saved.GetComponent<NetworkObject>() : null;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Mesh LoadLootMesh(string meshName)
        {
            return AssetDatabase.LoadAssetAtPath<Mesh>($"{ArtGenIO.MeshFolder}/Loot/{meshName}.asset");
        }

        #endregion

        #region Kontrolcu

        private static int SetupAirdropController(NetworkObject crate)
        {
            AirdropController controller =
                Object.FindFirstObjectByType<AirdropController>(FindObjectsInactive.Include);

            int changes = 0;

            if (controller == null)
            {
                // LootZoneManager ile ayni mantik: mevcut ve kayitli bir sahne
                // NetworkObject'ine bindiriyoruz.
                DropPhaseController dropPhase =
                    Object.FindFirstObjectByType<DropPhaseController>(FindObjectsInactive.Include);

                if (dropPhase == null)
                {
                    Debug.LogError("Faz 3-5: Sahnede DropPhaseController yok, AirdropController eklenemedi.");
                    return 0;
                }

                controller = Undo.AddComponent<AirdropController>(dropPhase.gameObject);
                changes++;
            }

            if (crate != null)
            {
                SerializedObject serialized = new(controller);
                serialized.FindProperty("cratePrefab").objectReferenceValue = crate;
                serialized.ApplyModifiedProperties();
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            return changes;
        }

        #endregion
    }
}
