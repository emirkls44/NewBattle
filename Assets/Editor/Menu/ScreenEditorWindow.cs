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
            GetWindow<ScreenEditorWindow>("Ekran Düzenleyici").minSize = new Vector2(360f, 590f);
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
                Row("Karakter (Lobi Animasyonu)", ShowCharacterScreen);
                Row("Ödül Penceresi (günlük / hediye)", ShowRewardPopup);
                Row("Mağaza", ShowShop);
                Row("Ayarlar", () => ShowChild<SettingsScreen>());
                Row("İsim Penceresi", () => ShowChild<NamePopup>());
                Row("Seviye Yolu", () => ShowChild<LevelRoadScreen>());
                Row("Profil (Liderlik)", () => ShowChild<ProfileScreen>());
                Row("Mücadeleler", () => ShowChild<ChallengesScreen>());
                Row("Hediye Kodu Penceresi", () => ShowChild<GiftCodePopup>());
                Row("Arkadaş İkilisi Penceresi", () => ShowChild<FriendRoomPopup>());
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

        private static void ShowCharacterScreen()
        {
            HideAll();
            LobbyAnimationScreen screen = Find<LobbyAnimationScreen>();
            if (screen == null)
                return;

            Undo.RecordObject(screen.gameObject, "Ekran goster");
            screen.gameObject.SetActive(true);
            Finish(screen.gameObject);
        }

        private static void ShowRewardPopup()
        {
            HideAll();
            RewardPopup popup = Find<RewardPopup>();
            if (popup == null)
                return;

            Undo.RecordObject(popup.gameObject, "Ekran goster");
            popup.gameObject.SetActive(true);
            if (popup.TryGetComponent(out CanvasGroup group))
            {
                Undo.RecordObject(group, "Ekran goster");
                group.alpha = 1f;
            }

            Finish(popup.gameObject);
        }

        private static void ShowShop()
        {
            HideAll();
            ShopScreen shop = Find<ShopScreen>();
            if (shop == null)
                return;

            Undo.RecordObject(shop.gameObject, "Ekran goster");
            shop.gameObject.SetActive(true);
            Finish(shop.gameObject);
        }

        /// <summary>Kendini Awake'te kapatan bir menu penceresini gorunur yapar.</summary>
        private static void ShowChild<T>() where T : Component
        {
            HideAll();
            T screen = Find<T>();
            if (screen == null)
                return;

            Undo.RecordObject(screen.gameObject, "Ekran goster");
            screen.gameObject.SetActive(true);
            Finish(screen.gameObject);
        }

        private static void HideChild<T>() where T : Component
        {
            T screen = FindFirstObjectByType<T>(FindObjectsInactive.Include);
            if (screen != null && screen.gameObject.activeSelf)
            {
                Undo.RecordObject(screen.gameObject, "Ekranlari gizle");
                screen.gameObject.SetActive(false);
            }
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

            LobbyAnimationScreen character = FindFirstObjectByType<LobbyAnimationScreen>(FindObjectsInactive.Include);
            if (character != null && character.gameObject.activeSelf)
            {
                Undo.RecordObject(character.gameObject, "Ekranlari gizle");
                character.gameObject.SetActive(false);
            }

            HideChild<SettingsScreen>();
            HideChild<NamePopup>();
            HideChild<LevelRoadScreen>();
            HideChild<ProfileScreen>();
            HideChild<ChallengesScreen>();
            HideChild<GiftCodePopup>();
            HideChild<FriendRoomPopup>();

            ShopScreen shop = FindFirstObjectByType<ShopScreen>(FindObjectsInactive.Include);
            if (shop != null && shop.gameObject.activeSelf)
            {
                Undo.RecordObject(shop.gameObject, "Ekranlari gizle");
                shop.gameObject.SetActive(false);
            }

            RewardPopup popup = FindFirstObjectByType<RewardPopup>(FindObjectsInactive.Include);
            if (popup != null && popup.gameObject.activeSelf)
            {
                Undo.RecordObject(popup.gameObject, "Ekranlari gizle");
                popup.gameObject.SetActive(false);
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
