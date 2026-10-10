#if UNITY_EDITOR
using NewBattle.Progression;
using UnityEditor;
using UnityEngine;

namespace NewBattle.EditorTools
{
    /// <summary>Default animasyonu elle kimlik yazmak yerine listeden secmek icin.</summary>
    [CustomEditor(typeof(LobbyAnimationLibrary))]
    public class LobbyAnimationLibraryEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Herkesin ilk gördüğü animasyon: 'Default Animasyon'. O her zaman açıktır.\n\n" +
                "Yeni animasyon: listeye satır ekle, klibi sürükle, açılma şartını seç. " +
                "Klip Humanoid olmalı (Mixamo klipleri olur). 'id' alanını sonradan değiştirme; " +
                "oyuncunun seçimi bu isimle kaydediliyor.",
                MessageType.Info);

            SerializedProperty defaultId = serializedObject.FindProperty("defaultAnimationId");
            SerializedProperty animations = serializedObject.FindProperty("animations");

            var ids = new string[animations.arraySize];
            var labels = new string[animations.arraySize];
            int selected = -1;

            for (int i = 0; i < animations.arraySize; i++)
            {
                SerializedProperty item = animations.GetArrayElementAtIndex(i);
                ids[i] = item.FindPropertyRelative("id").stringValue;
                labels[i] = $"{item.FindPropertyRelative("displayName").stringValue}  ({ids[i]})";
                if (ids[i] == defaultId.stringValue)
                    selected = i;
            }

            if (ids.Length > 0)
            {
                int picked = EditorGUILayout.Popup("Default Animasyon", Mathf.Max(0, selected), labels);
                if (picked != selected)
                    defaultId.stringValue = ids[picked];
            }
            else
            {
                EditorGUILayout.LabelField("Default Animasyon", "Liste boş");
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.PropertyField(animations, new GUIContent("Animasyonlar"), true);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
