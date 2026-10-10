#if UNITY_EDITOR
using NewBattle.Gameplay;
using NewBattle.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NewBattle.EditorTools
{
    /// <summary>ASAMA 6-7: Ucus (serbest dusus) ve parasut inisi kurulumu.</summary>
    public static partial class MainMenuBuilder
    {
        private const string DescentOverlayName = "InisEfekti";
        private const string PlayerPrefabPath = "Assets/Prefabs/PlayerOnline.prefab";
        private const int StreakCount = 10;

        /// <summary>Oyun kamerasina inis kamerasini ekler.</summary>
        private static bool EnsureDescentCamera()
        {
            CameraFollow follow = Object.FindFirstObjectByType<CameraFollow>(FindObjectsInactive.Include);
            if (follow == null || follow.GetComponent<DescentCameraRig>() != null)
                return false;

            DescentCameraRig rig = Undo.AddComponent<DescentCameraRig>(follow.gameObject);
            var serialized = new SerializedObject(rig);
            serialized.FindProperty("skySprite").objectReferenceValue = Sprite("sky_dive");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        /// <summary>Vinyet ve hiz cizgilerini Game_UI altina kurar.</summary>
        private static bool EnsureDescentOverlay(Canvas canvas, bool rebuild)
        {
            DescentOverlay existing = canvas.GetComponentInChildren<DescentOverlay>(true);
            if (existing != null && !rebuild)
                return false;

            if (existing != null)
                Undo.DestroyObjectImmediate(existing.gameObject);

            RectTransform root = NewRect(DescentOverlayName, canvas.transform);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Inis efekti");
            Stretch(root, 0f, 0f, 0f, 0f);

            // HUD'un ustunde, harita secim ekraninin (900) altinda.
            Canvas overlayCanvas = root.gameObject.AddComponent<Canvas>();
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = 800;
            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            RectTransform streakRoot = NewRect("HizCizgileri", root);
            Stretch(streakRoot, 0f, 0f, 0f, 0f);
            var streaks = new RectTransform[StreakCount];
            for (int i = 0; i < StreakCount; i++)
            {
                RectTransform streak = NewRect($"Cizgi{i + 1:00}", streakRoot);
                Place(streak, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(i % 3 == 0 ? 520f : 360f, i % 2 == 0 ? 10f : 7f));
                AddImage(streak, Sprite("streak"), new Color(1f, 1f, 1f, 0f)).type = Image.Type.Simple;
                streaks[i] = streak;
            }

            RectTransform vignette = NewRect("Vinyet", root);
            Stretch(vignette, 0f, 0f, 0f, 0f);
            Image vignetteImage = AddImage(vignette, Sprite("vignette"), new Color(1f, 1f, 1f, 0f));
            vignetteImage.type = Image.Type.Simple;

            DescentOverlay overlay = root.gameObject.AddComponent<DescentOverlay>();
            var serialized = new SerializedObject(overlay);
            serialized.FindProperty("group").objectReferenceValue = group;
            serialized.FindProperty("vignette").objectReferenceValue = vignetteImage;
            SerializedProperty list = serialized.FindProperty("streaks");
            list.arraySize = StreakCount;
            for (int i = 0; i < StreakCount; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = streaks[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        /// <summary>
        /// Inis surelerini Battlelands hissine cekiyor: daha yuksekten, daha uzun
        /// serbest dusus ve rahat bir parasut. Sadece eski varsayilanlar
        /// duruyorsa degistirir; senin ayarladigin degerlere dokunmaz.
        /// </summary>
        private static bool UpdateDescentTimings()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null)
                return false;

            GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            bool changed = false;

            try
            {
                ParachuteDescent descent = contents.GetComponent<ParachuteDescent>();
                if (descent == null)
                    return false;

                var serialized = new SerializedObject(descent);
                changed |= ReplaceIfDefault(serialized, "freefallSeconds", 0.8f, 2.4f);
                changed |= ReplaceIfDefault(serialized, "parachuteSeconds", 2f, 3.5f);
                changed |= ReplaceIfDefault(serialized, "startAltitude", 48f, 70f);
                changed |= ReplaceIfDefault(serialized, "freefallAltitudeShare", 0.68f, 0.72f);

                if (changed)
                {
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            return changed;
        }

        private static bool ReplaceIfDefault(SerializedObject serialized, string property, float oldValue, float newValue)
        {
            SerializedProperty field = serialized.FindProperty(property);
            if (field == null || !Mathf.Approximately(field.floatValue, oldValue))
                return false;

            field.floatValue = newValue;
            return true;
        }
    }
}
#endif
