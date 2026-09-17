using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NewBattle.ArtGen
{
    /// <summary>
    /// Prosedurel adayi geri alir ve sahneyi eski prototip arenasina dondurur.
    ///
    /// Degerler tahmin degil: git HEAD'deki PrototypeArena.unity'den okundu.
    /// Ada uretimi sirasinda WorldScaleSync bu alanlari 180 m'lik haritaya gore
    /// yeniden olceklemisti; burada birebir eski hallerine yaziliyorlar.
    /// </summary>
    public static class RestoreOldArena
    {
        // --- git HEAD: Assets/Scenes/PrototypeArena.unity ---
        private const float OldSafeZoneInitialRadius = 32f;
        private const float OldFogOuterRadius = 80f;
        private const float OldLootSpawnRadius = 28f;
        private const float OldPlayableMapRadius = 29f;
        private const float OldCameraFocusRadius = 23f;

        private const int OldAmmoCount = 8;
        private const int OldRifleCount = 5;
        private const int OldHealthCount = 6;
        private const int OldShieldCount = 14;

        /// <summary>SafeZoneController script'indeki varsayilan asama yaricaplari.</summary>
        private static readonly float[] OldStageRadii = { 24f, 16f, 10f, 6f, 3.5f };

        [MenuItem("Tools/NewBattle/Eski Arenaya Don", false, 20)]
        public static void Restore()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Eski Arenaya Don",
                "Sahnedeki uretilmis 'Island' nesnesi silinecek ve guvenli alan / loot / " +
                "inis fazi / kamera yaricaplari eski prototip degerlerine dondurulecek.\n\n" +
                "Karakterler ve loot modelleri silinmez.",
                "Geri Al", "Vazgec");

            if (!confirmed)
                return;

            int changes = 0;

            changes += RemoveGeneratedIsland();
            changes += RestoreSafeZone();
            changes += RestoreLootSpawner();
            changes += RestoreDropPhase();
            changes += RestoreCamera();
            changes += ReactivateLegacyBuilder();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log(
                $"RestoreOldArena: {changes} degisiklik uygulandi. " +
                "Sahneyi kaydet (Ctrl+S) ve Play'e bas."
            );
        }

        private static int RemoveGeneratedIsland()
        {
            GameObject island = GameObject.Find("Island");
            if (island == null)
                return 0;

            Undo.DestroyObjectImmediate(island);
            return 1;
        }

        /// <summary>
        /// Eski harita PrototypeIslandBuilder tarafindan Awake'te uretiliyordu.
        /// Ada uretimi sirasinda kapatilmis olabilir; tekrar aktif ediyoruz.
        /// </summary>
        private static int ReactivateLegacyBuilder()
        {
            PrototypeIslandBuilder builder =
                Object.FindFirstObjectByType<PrototypeIslandBuilder>(FindObjectsInactive.Include);

            if (builder == null)
            {
                Debug.LogWarning(
                    "RestoreOldArena: Sahnede PrototypeIslandBuilder bulunamadi. " +
                    "Eski harita 'ArenaBuilder' nesnesinde duruyordu - silinmis olabilir.");
                return 0;
            }

            if (builder.gameObject.activeSelf && builder.enabled)
                return 0;

            Undo.RecordObject(builder.gameObject, "Eski Arenaya Don");
            builder.gameObject.SetActive(true);
            builder.enabled = true;
            EditorUtility.SetDirty(builder);
            return 1;
        }

        private static int RestoreSafeZone()
        {
            SafeZoneController zone = Object.FindFirstObjectByType<SafeZoneController>(FindObjectsInactive.Include);
            if (zone == null)
                return 0;

            SerializedObject serialized = new(zone);
            serialized.FindProperty("initialRadius").floatValue = OldSafeZoneInitialRadius;

            SerializedProperty fog = serialized.FindProperty("fogOuterRadius");
            if (fog != null)
                fog.floatValue = OldFogOuterRadius;

            SerializedProperty stages = serialized.FindProperty("stages");
            for (int i = 0; i < stages.arraySize && i < OldStageRadii.Length; i++)
            {
                stages.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("targetRadius").floatValue = OldStageRadii[i];
            }

            serialized.ApplyModifiedProperties();
            return 1;
        }

        private static int RestoreLootSpawner()
        {
            LootSpawner spawner = Object.FindFirstObjectByType<LootSpawner>(FindObjectsInactive.Include);
            if (spawner == null)
                return 0;

            SerializedObject serialized = new(spawner);
            serialized.FindProperty("spawnRadius").floatValue = OldLootSpawnRadius;
            SetInt(serialized, "ammoCount", OldAmmoCount);
            SetInt(serialized, "rifleCount", OldRifleCount);
            SetInt(serialized, "healthCount", OldHealthCount);
            SetInt(serialized, "shieldCount", OldShieldCount);
            serialized.ApplyModifiedProperties();
            return 1;
        }

        private static void SetInt(SerializedObject serialized, string fieldName, int value)
        {
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property != null)
                property.intValue = value;
        }

        private static int RestoreDropPhase()
        {
            DropPhaseController drop = Object.FindFirstObjectByType<DropPhaseController>(FindObjectsInactive.Include);
            if (drop == null)
                return 0;

            SerializedObject serialized = new(drop);
            serialized.FindProperty("playableMapRadius").floatValue = OldPlayableMapRadius;
            serialized.ApplyModifiedProperties();
            return 1;
        }

        private static int RestoreCamera()
        {
            CameraFollow camera = Object.FindFirstObjectByType<CameraFollow>(FindObjectsInactive.Include);
            if (camera == null)
                return 0;

            Undo.RecordObject(camera, "Eski Arenaya Don");
            camera.cameraFocusRadius = OldCameraFocusRadius;
            EditorUtility.SetDirty(camera);
            return 1;
        }
    }
}
