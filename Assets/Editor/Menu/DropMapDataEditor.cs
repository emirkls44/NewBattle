#if UNITY_EDITOR
using NewBattle.UI;
using UnityEditor;
using UnityEngine;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// Harita verisinin altinda haritayi ve bolgeleri gosterir.
    /// Bolgeye tikla: secilir. Surukle: tasinir. Sag alt kosedeki kareden
    /// surukle: boyutlanir. Bolge adlari ve sayisal degerler listeden duzenlenir.
    /// </summary>
    [CustomEditor(typeof(DropMapData))]
    public class DropMapDataEditor : Editor
    {
        private const float HandleSize = 12f;

        private int _selected = -1;
        private bool _resizing;
        private Vector2 _dragOffset;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Gerçek haritanın kuşbakışı resmi gelince: 'Map Texture'a sürükle, resmi çekerken kameranın " +
                "gördüğü kenar uzunluğunu 'World Size'a yaz.\n\n" +
                "Önizlemede bölgeye tıkla = seç, sürükle = taşı, sağ alt köşedeki kareden sürükle = boyutlandır.",
                MessageType.Info);

            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Önizleme", EditorStyles.boldLabel);
            DrawPreview();
        }

        private void DrawPreview()
        {
            var data = (DropMapData)target;
            float size = Mathf.Min(EditorGUIUtility.currentViewWidth - 40f, 520f);
            Rect area = GUILayoutUtility.GetRect(size, size, GUILayout.ExpandWidth(false));

            if (data.MapTexture != null)
                GUI.DrawTexture(area, data.MapTexture, ScaleMode.StretchToFill);
            else
                EditorGUI.DrawRect(area, new Color(0.15f, 0.35f, 0.5f));

            SerializedProperty regions = serializedObject.FindProperty("regions");

            for (int i = 0; i < regions.arraySize; i++)
            {
                SerializedProperty region = regions.GetArrayElementAtIndex(i);
                Rect normalized = region.FindPropertyRelative("area").rectValue;
                Rect screen = ToScreen(area, normalized);
                bool selected = i == _selected;

                Handles.DrawSolidRectangleWithOutline(screen,
                    selected ? new Color(1f, 0.85f, 0.1f, 0.25f) : new Color(1f, 1f, 1f, 0.08f),
                    selected ? new Color(1f, 0.85f, 0.1f, 1f) : new Color(1f, 1f, 1f, 0.8f));

                var style = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.white }, alignment = TextAnchor.UpperLeft };
                GUI.Label(new Rect(screen.x + 3f, screen.y + 2f, screen.width, 18f), region.FindPropertyRelative("name").stringValue, style);

                if (selected)
                    EditorGUI.DrawRect(HandleRect(screen), new Color(1f, 0.85f, 0.1f, 1f));
            }

            HandleMouse(area, regions);
        }

        private void HandleMouse(Rect area, SerializedProperty regions)
        {
            Event e = Event.current;
            if (!area.Contains(e.mousePosition) && e.type != EventType.MouseDrag && e.type != EventType.MouseUp)
                return;

            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0:
                {
                    _resizing = false;

                    if (_selected >= 0 && _selected < regions.arraySize)
                    {
                        Rect current = ToScreen(area, regions.GetArrayElementAtIndex(_selected).FindPropertyRelative("area").rectValue);
                        if (HandleRect(current).Contains(e.mousePosition))
                        {
                            _resizing = true;
                            e.Use();
                            return;
                        }
                    }

                    _selected = -1;
                    for (int i = regions.arraySize - 1; i >= 0; i--)
                    {
                        Rect screen = ToScreen(area, regions.GetArrayElementAtIndex(i).FindPropertyRelative("area").rectValue);
                        if (!screen.Contains(e.mousePosition))
                            continue;

                        _selected = i;
                        _dragOffset = e.mousePosition - screen.position;
                        break;
                    }

                    e.Use();
                    Repaint();
                    break;
                }

                case EventType.MouseDrag when _selected >= 0 && _selected < regions.arraySize:
                {
                    SerializedProperty property = regions.GetArrayElementAtIndex(_selected).FindPropertyRelative("area");
                    Rect screen = ToScreen(area, property.rectValue);

                    if (_resizing)
                    {
                        screen.xMax = Mathf.Max(screen.xMin + 8f, e.mousePosition.x);
                        screen.yMax = Mathf.Max(screen.yMin + 8f, e.mousePosition.y);
                    }
                    else
                    {
                        screen.position = e.mousePosition - _dragOffset;
                    }

                    property.rectValue = ToNormalized(area, screen);
                    serializedObject.ApplyModifiedProperties();
                    e.Use();
                    Repaint();
                    break;
                }

                case EventType.MouseUp:
                    _resizing = false;
                    break;
            }
        }

        private static Rect HandleRect(Rect screen) =>
            new(screen.xMax - HandleSize, screen.yMax - HandleSize, HandleSize, HandleSize);

        /// <summary>0-1 (sol alt orijin) -> onizleme pikseli (sol ust orijin).</summary>
        private static Rect ToScreen(Rect area, Rect normalized)
        {
            return new Rect(
                area.x + normalized.x * area.width,
                area.y + (1f - normalized.yMax) * area.height,
                normalized.width * area.width,
                normalized.height * area.height);
        }

        private static Rect ToNormalized(Rect area, Rect screen)
        {
            float x = Mathf.Clamp01((screen.x - area.x) / area.width);
            float w = Mathf.Clamp(screen.width / area.width, 0.01f, 1f - x);
            float yMax = Mathf.Clamp01(1f - (screen.y - area.y) / area.height);
            float h = Mathf.Clamp(screen.height / area.height, 0.01f, yMax);
            return new Rect(x, yMax - h, w, h);
        }
    }
}
#endif
