using UnityEditor;
using UnityEngine;

namespace NewBattle.ArtGen
{
    /// <summary>
    /// Uretilen nesnelerin ihtiyac duydugu tag ve layer'lari proje ayarlarina ekler.
    /// Elle eklemeyi unutmak, "gizlenme calismiyor" gibi sessiz hatalarin en yaygin sebebi.
    /// </summary>
    public static class ArtGenTags
    {
        public const string BushTag = "Bush";
        public const string PlayerTag = "Player";
        public const string EnemyTag = "Enemy";
        public const string LootTag = "Loot";

        private static readonly string[] RequiredTags = { BushTag, EnemyTag, LootTag };

        public static void EnsureTags()
        {
            SerializedObject tagManager = new(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty tags = tagManager.FindProperty("tags");

            foreach (string tag in RequiredTags)
            {
                if (HasTag(tags, tag))
                    continue;

                tags.InsertArrayElementAtIndex(tags.arraySize);
                tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
                Debug.Log($"ArtGen: '{tag}' tag'i projeye eklendi.");
            }

            tagManager.ApplyModifiedProperties();
        }

        private static bool HasTag(SerializedProperty tags, string tag)
        {
            for (int i = 0; i < tags.arraySize; i++)
            {
                if (tags.GetArrayElementAtIndex(i).stringValue == tag)
                    return true;
            }

            return false;
        }
    }
}
