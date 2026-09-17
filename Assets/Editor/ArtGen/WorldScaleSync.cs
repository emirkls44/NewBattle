using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NewBattle.ArtGen
{
    /// <summary>
    /// Ada yaricapi degistiginde ona bagli butun oyun sistemlerini tek seferde gunceller.
    ///
    /// Bu sistemler birbirinden habersiz sabitler tutuyor; biri guncellenip digeri
    /// unutulursa ortaya sessiz hatalar cikiyor (guvenli alan haritanin disinda kapaniyor,
    /// loot denize dusuyor, kamera haritanin kenarina kilitleniyor). Tek dugmede
    /// hepsini birden ayarlamak bu sinifin tek isi.
    /// </summary>
    public static class WorldScaleSync
    {
        public static void Apply(float playableRadius)
        {
            int updated = 0;

            updated += SyncSafeZone(playableRadius);
            updated += SyncLootSpawner(playableRadius);
            updated += SyncDropPhase(playableRadius);
            updated += SyncCamera(playableRadius);

            if (updated == 0)
            {
                Debug.LogWarning(
                    "WorldScaleSync: Sahnede guncellenecek bilesen bulunamadi. " +
                    "Dogru sahne acik mi (Assets/Scenes/PrototypeArena.unity)?");
                return;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"WorldScaleSync: {updated} bilesen {playableRadius:0} m yaricapa gore guncellendi.");
        }

        private static int SyncSafeZone(float playableRadius)
        {
            SafeZoneController zone = Object.FindFirstObjectByType<SafeZoneController>(FindObjectsInactive.Include);
            if (zone == null)
                return 0;

            SerializedObject serialized = new(zone);

            SerializedProperty initialRadius = serialized.FindProperty("initialRadius");
            float previousRadius = initialRadius.floatValue;
            float newRadius = playableRadius * 0.94f;
            initialRadius.floatValue = newRadius;

            // Asamalarin birbirine orani oyun temposunu belirliyor; oranlari koruyup
            // hepsini yeni yaricapa olcekliyoruz.
            float scale = previousRadius > 0.01f ? newRadius / previousRadius : 1f;
            SerializedProperty stages = serialized.FindProperty("stages");

            for (int i = 0; i < stages.arraySize; i++)
            {
                SerializedProperty target = stages.GetArrayElementAtIndex(i).FindPropertyRelative("targetRadius");
                target.floatValue = Mathf.Max(3f, target.floatValue * scale);
            }

            SerializedProperty fogOuterRadius = serialized.FindProperty("fogOuterRadius");
            if (fogOuterRadius != null)
                fogOuterRadius.floatValue = playableRadius * 2.2f;

            serialized.ApplyModifiedProperties();
            return 1;
        }

        private static int SyncLootSpawner(float playableRadius)
        {
            LootSpawner spawner = Object.FindFirstObjectByType<LootSpawner>(FindObjectsInactive.Include);
            if (spawner == null)
                return 0;

            SerializedObject serialized = new(spawner);
            serialized.FindProperty("spawnRadius").floatValue = playableRadius * 0.92f;

            // Harita buyudukce ayni loot sayisi seyrek kalir: alanla orantili olarak artir.
            float areaScale = Mathf.Pow(playableRadius / 28f, 2f);
            ScaleCount(serialized, "rifleCount", areaScale, 32);
            ScaleCount(serialized, "healthCount", areaScale, 32);
            ScaleCount(serialized, "shieldCount", areaScale, 32);
            ScaleCount(serialized, "ammoCount", areaScale, 32);

            serialized.ApplyModifiedProperties();
            return 1;
        }

        private static void ScaleCount(SerializedObject serialized, string fieldName, float scale, int maximum)
        {
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
                return;

            property.intValue = Mathf.Clamp(Mathf.RoundToInt(property.intValue * Mathf.Sqrt(scale)), 1, maximum);
        }

        private static int SyncDropPhase(float playableRadius)
        {
            DropPhaseController drop = Object.FindFirstObjectByType<DropPhaseController>(FindObjectsInactive.Include);
            if (drop == null)
                return 0;

            SerializedObject serialized = new(drop);
            serialized.FindProperty("playableMapRadius").floatValue = playableRadius * 0.95f;
            serialized.ApplyModifiedProperties();
            return 1;
        }

        private static int SyncCamera(float playableRadius)
        {
            CameraFollow camera = Object.FindFirstObjectByType<CameraFollow>(FindObjectsInactive.Include);
            if (camera == null)
                return 0;

            Undo.RecordObject(camera, "World Scale Sync");
            camera.cameraFocusRadius = playableRadius;
            camera.mapCenter = Vector2.zero;
            EditorUtility.SetDirty(camera);
            return 1;
        }
    }
}
