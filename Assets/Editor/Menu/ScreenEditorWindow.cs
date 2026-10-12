#if UNITY_EDITOR
using NewBattle.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// Oyun disinda gizli duran ekranlari (oyun modu, harita secimi, mac sonu,
    /// yukleme) duzenlemek icin gorunur yapar ve Hierarchy'de secer. Boylece
    /// her parcayi Game/Scene gorunumunde surukleyerek konumlandirabilirsin.
    ///
    /// Gorunur birakmak sorun degil: ekranlar oyun baslarken kendilerini
    /// zaten gizler. Yine de "Hepsini Gizle" ile duzenli birakabilirsin.
    /// </summary>
    public class ScreenEditorWindow : EditorWindow
    {
        private const string LoadingPrefabPath = "Assets/Prefabs/UI/YuklemeEkrani.prefab";

        [MenuItem("Tools/NewBattle/Ekran Düzenleyici", false, 30)]
        public static void Open()
        {
            GetWindow<ScreenEditorWindow>("Ekran Düzenleyici").minSize = new Vector2(360f, 330f);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Bir ekranı göster → Game görünümünde gör, Hierarchy'de parçalarını seçip taşı.\n" +
                "Ekranlar oyun başlarken kendilerini gizler; görünür bırakman sorun değil.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                Row("Ana Menü", ShowMainMenu);
                Row("Oyun Modu Penceresi", ShowModePanel);
                Row("Harita Seçimi (Aksiyona Hazırlan)", ShowDropScreen);
                Row("Maç Sonu", ShowMatchEnd);
                Row("Yükleme Ekranı (prefab)", OpenLoadingPrefab);

                EditorGUILayout.Space(10f);
                if (GUILayout.Button("Hepsini Gizle (oyundaki başlangıç hali)", GUILayout.Height(28f)))
                    HideAll();
            }

            if (EditorApplication.isPlaying)
                EditorGUILayout.HelpBox("Play modundayken düzenleme yapılmaz; değişiklikler kaydedilmez.", MessageType.Warning);
        }

        private static void Row(string label, System.Action show)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label);
                if (GUILayout.Button("Göster", GUILayout.Width(80f)))
                    show();
            }
        }

        private static void ShowMainMenu()
        {
            HideAll();
            MainMenuScreen menu = Find<MainMenuScreen>();
            if (menu != null)
                Finish(menu.gameObject);
        }

        private static void ShowModePanel()
        {
            HideAll();
            ModeSelectPanel panel = Find<ModeSelectPanel>();
            if (panel == null)
                return;

            Undo.RecordObject(panel.gameObject, "Ekran goster");
            panel.gameObject.SetActive(true);
            if (panel.TryGetComponent(out CanvasGroup group))
            {
                Undo.RecordObject(group, "Ekran goster");
                group.alpha = 1f;
            }

            Finish(panel.gameObject);
        }

        private static void ShowDropScreen()
        {
            HideAll();
            DropSelectionScreen screen = Find<DropSelectionScreen>();
            if (screen != null)
                Finish(SetCanvas(screen, true));
        }

        private static void ShowMatchEnd()
        {
            HideAll();
            MatchEndScreen screen = Find<MatchEndScreen>();
            if (screen != null)
                Finish(SetCanvas(screen, true));
        }

        private static void OpenLoadingPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LoadingPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("Yükleme ekranı yok: 'Tools > NewBattle > Menü > Kur' çalıştır.");
                return;
            }

            // Eski kurulumda prefab saydam (alpha 0) kaydedilmisti; duzenlerken gorunsun.
            GameObject contents = PrefabUtility.LoadPrefabContents(LoadingPrefabPath);
            try
            {
                if (contents.TryGetComponent(out CanvasGroup group) && group.alpha < 1f)
                {
                    group.alpha = 1f;
                    PrefabUtility.SaveAsPrefabAsset(contents, LoadingPrefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            AssetDatabase.OpenAsset(prefab);
        }

        /// <summary>Ekranlari oyundaki baslangic haline dondurur.</summary>
        private static void HideAll()
        {
            ModeSelectPanel panel = Find<ModeSelectPanel>();
            if (panel != null && panel.gameObject.activeSelf)
            {
                Undo.RecordObject(panel.gameObject, "Ekranlari gizle");
                panel.gameObject.SetActive(false);
            }

            DropSelectionScreen drop = Find<DropSelectionScreen>();
            if (drop != null)
                SetCanvas(drop, false);

            MatchEndScreen matchEnd = Find<MatchEndScreen>();
            if (matchEnd != null)
                SetCanvas(matchEnd, false);

            MarkDirty();
        }

        private static GameObject SetCanvas(Component screen, bool visible)
        {
            if (screen.TryGetComponent(out Canvas canvas) && canvas.enabled != visible)
            {
                Undo.RecordObject(canvas, visible ? "Ekran goster" : "Ekranlari gizle");
                canvas.enabled = visible;
            }

            return screen.gameObject;
        }

        private static void Finish(GameObject target)
        {
            Selection.activeGameObject = target;
            EditorGUIUtility.PingObject(target);
            MarkDirty();
        }

        private static void MarkDirty()
        {
            if (!EditorApplication.isPlaying)
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        private static T Find<T>() where T : Object
        {
            T found = FindFirstObjectByType<T>(FindObjectsInactive.Include);
            if (found == null)
                Debug.LogWarning($"{typeof(T).Name} sahnede yok: 'Tools > NewBattle > Menü > Kur' çalıştır.");
            return found;
        }
    }
}
#endif
