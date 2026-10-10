#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NewBattle.Progression;
using NewBattle.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// Lobi animasyon kutuphanesini olusturur ve menudeki karaktere baglar.
    /// "1 - Ana Menuyu Kur" bunu kendisi de cagirir; bu menu ogesi, menu
    /// zaten kuruluysa sadece animasyon sistemini eklemek icindir.
    /// </summary>
    public static class LobbyAnimationSetup
    {
        public const string LibraryPath = "Assets/GameArt/LobbyAnimations/LobiAnimasyonlari.asset";

        private const string MergedClipsPath = "Assets/Arts/Character_V2_Animator/Meshy_AI_Tie_Guy_biped_Meshy_Merged_Animations.fbx";
        private const string RifleIdlePath = "Assets/Arts/Character_V2_Animator/Meshy_AI_Tie_Guy_biped_Character_output@Rifle Aiming Idle.fbx";

        [MenuItem("Tools/NewBattle/Menü/2 - Lobi Animasyonlarını Kur", false, 2)]
        public static void Setup()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Lobi Animasyonları", "Önce Play modunu kapat.", "Tamam");
                return;
            }

            LobbyAnimationLibrary library = EnsureLibrary();

            MenuCharacterStage stage = Object.FindFirstObjectByType<MenuCharacterStage>(FindObjectsInactive.Include);
            if (stage == null)
            {
                Debug.LogError("Lobi Animasyonları: önce 'Tools > NewBattle > Menü > 1 - Ana Menüyü Kur' çalıştır.");
                return;
            }

            Attach(stage, library);
            EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
            EditorSceneManager.SaveScene(stage.gameObject.scene);

            Selection.activeObject = library;
            EditorGUIUtility.PingObject(library);
            Debug.Log("Lobi animasyonları kuruldu. Default animasyonu seçili dosyadan değiştirebilirsin.");
        }

        public static LobbyAnimationLibrary EnsureLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<LobbyAnimationLibrary>(LibraryPath);
            if (library != null)
                return library;

            string folder = System.IO.Path.GetDirectoryName(LibraryPath)?.Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/GameArt", "LobbyAnimations");

            library = ScriptableObject.CreateInstance<LobbyAnimationLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);

            Dictionary<string, AnimationClip> merged = LoadClips(MergedClipsPath);
            Dictionary<string, AnimationClip> rifle = LoadClips(RifleIdlePath);

            var serialized = new SerializedObject(library);
            serialized.FindProperty("defaultAnimationId").stringValue = "bekleme";
            SerializedProperty list = serialized.FindProperty("animations");
            list.arraySize = 0;

            // Baslangic listesi. Hepsi projede zaten olan klipler; istedigin
            // gibi degistir, sil ya da Mixamo'dan yenilerini ekle.
            Add(list, "bekleme", "Bekleme", Get(merged, "Idle_11"), LobbyUnlockType.Free, 0);
            Add(list, "rahat", "Rahat Duruş", Get(merged, "Idle_5"), LobbyUnlockType.Level, 3);
            Add(list, "onay", "Onaylama", Get(merged, "Agree_Gesture"), LobbyUnlockType.Wins, 1);
            Add(list, "sasirma", "Etrafa Bakınma", Get(merged, "Look_Around_Dumbfounded"), LobbyUnlockType.Level, 5);
            Add(list, "nisan", "Nişan Al", Get(rifle, "Rifle Aiming Idle"), LobbyUnlockType.Coins, 200);
            Add(list, "havali", "Havalı Yürüyüş", Get(merged, "Proud_Strut"), LobbyUnlockType.Wins, 5);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return library;
        }

        /// <summary>Animasyon oynaticiyi karakterin donen nesnesine ekler ve baglar.</summary>
        public static void Attach(MenuCharacterStage stage, LobbyAnimationLibrary library)
        {
            var stageSerialized = new SerializedObject(stage);
            var turntable = stageSerialized.FindProperty("turntable").objectReferenceValue as Transform;
            if (turntable == null)
            {
                Debug.LogError("Lobi Animasyonları: MenuCharacterStage'in 'turntable' alanı boş.");
                return;
            }

            Animator animator = turntable.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                // Menu gizliyken calismasin; LobbyCharacterAnimator acar.
                Undo.RecordObject(animator, "Lobi animasyonu");
                animator.enabled = false;
            }

            LobbyCharacterAnimator player = turntable.GetComponent<LobbyCharacterAnimator>();
            if (player == null)
                player = Undo.AddComponent<LobbyCharacterAnimator>(turntable.gameObject);

            var playerSerialized = new SerializedObject(player);
            playerSerialized.FindProperty("library").objectReferenceValue = library;
            playerSerialized.FindProperty("animator").objectReferenceValue = animator;
            playerSerialized.ApplyModifiedProperties();
            player.enabled = false; // MenuCharacterStage menu gorunurken acar

            stageSerialized.FindProperty("lobbyAnimator").objectReferenceValue = player;
            stageSerialized.ApplyModifiedProperties();
        }

        private static void Add(SerializedProperty list, string id, string displayName, AnimationClip clip,
            LobbyUnlockType unlock, int requirement)
        {
            int index = list.arraySize;
            list.arraySize++;
            SerializedProperty item = list.GetArrayElementAtIndex(index);
            item.FindPropertyRelative("id").stringValue = id;
            item.FindPropertyRelative("displayName").stringValue = displayName;
            item.FindPropertyRelative("clip").objectReferenceValue = clip;
            item.FindPropertyRelative("unlock").enumValueIndex = (int)unlock;
            item.FindPropertyRelative("requirement").intValue = requirement;
            item.FindPropertyRelative("speed").floatValue = 1f;
            item.FindPropertyRelative("icon").objectReferenceValue = null;
        }

        private static Dictionary<string, AnimationClip> LoadClips(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__"))
                .GroupBy(clip => clip.name)
                .ToDictionary(group => group.Key, group => group.First());
        }

        private static AnimationClip Get(Dictionary<string, AnimationClip> clips, string name)
        {
            if (clips.TryGetValue(name, out AnimationClip clip))
                return clip;

            Debug.LogWarning($"Lobi Animasyonları: '{name}' klibi bulunamadı; listede boş bırakıldı.");
            return null;
        }
    }
}
#endif
