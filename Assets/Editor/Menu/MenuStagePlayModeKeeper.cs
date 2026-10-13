#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using NewBattle.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// Ana menu karakter sahnesinde PLAY sirasinda yapilan ayarlar, Play durunca
    /// kaybolmasin: cikista yakalanir, edit moduna donunce sahneye yazilir.
    ///
    /// Korunanlar:
    ///   - MenuCharacterStage'in Inspector'da gorunen sayi/renk/ac-kapa ayarlari
    ///     (nesne baglantilari ve gizli alanlar degil)
    ///   - Zemin objesinin (KarakterZemini) konumu, acisi ve boyutu
    /// Materyal (MenuZemin) zaten bir dosya; Play'de degisse de kalir.
    /// Sonra sahneyi kaydet (Ctrl+S).
    /// </summary>
    [InitializeOnLoad]
    internal static class MenuStagePlayModeKeeper
    {
        private const string Key = "NB_MenuStagePlayModeKeeper";

        static MenuStagePlayModeKeeper()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode)
                Capture();
            else if (change == PlayModeStateChange.EnteredEditMode)
                Apply();
        }

        private static void Capture()
        {
            MenuCharacterStage stage = Object.FindFirstObjectByType<MenuCharacterStage>(FindObjectsInactive.Include);
            if (stage == null)
            {
                SessionState.EraseString(Key);
                return;
            }

            var lines = new List<string>();
            var serialized = new SerializedObject(stage);
            SerializedProperty property = serialized.GetIterator();
            if (property.NextVisible(true))
            {
                do
                {
                    string value = Encode(property);
                    if (value != null)
                        lines.Add($"p|{property.propertyPath}|{value}");
                }
                while (property.NextVisible(false));
            }

            if (serialized.FindProperty("ground").objectReferenceValue is Renderer ground)
            {
                Transform t = ground.transform;
                lines.Add("g|pos|" + V3(t.localPosition));
                lines.Add("g|rot|" + V3(t.localEulerAngles));
                lines.Add("g|scale|" + V3(t.localScale));
            }

            SessionState.SetString(Key, string.Join("\n", lines));
        }

        private static void Apply()
        {
            string data = SessionState.GetString(Key, string.Empty);
            SessionState.EraseString(Key);
            if (string.IsNullOrEmpty(data))
                return;

            MenuCharacterStage stage = Object.FindFirstObjectByType<MenuCharacterStage>(FindObjectsInactive.Include);
            if (stage == null)
                return;

            var serialized = new SerializedObject(stage);
            var ground = serialized.FindProperty("ground").objectReferenceValue as Renderer;
            bool groundChanged = false;

            foreach (string line in data.Split('\n'))
            {
                string[] parts = line.Split('|');
                if (parts.Length != 3)
                    continue;

                if (parts[0] == "p")
                {
                    SerializedProperty property = serialized.FindProperty(parts[1]);
                    if (property != null)
                        Decode(property, parts[2]);
                }
                else if (parts[0] == "g" && ground != null)
                {
                    Transform t = ground.transform;
                    Vector3 value = ParseV3(parts[2]);
                    Vector3 current = parts[1] switch
                    {
                        "pos" => t.localPosition,
                        "rot" => t.localEulerAngles,
                        _ => t.localScale
                    };

                    if ((current - value).sqrMagnitude < 1e-10f)
                        continue;

                    if (!groundChanged)
                        Undo.RecordObject(t, "Menu sahnesi (Play ayarlari)");
                    groundChanged = true;

                    switch (parts[1])
                    {
                        case "pos": t.localPosition = value; break;
                        case "rot": t.localEulerAngles = value; break;
                        default: t.localScale = value; break;
                    }
                }
            }

            bool stageChanged = serialized.ApplyModifiedProperties();
            if (!stageChanged && !groundChanged)
                return;

            EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
            Debug.Log("Menü karakter sahnesi: Play'de yaptığın ayarlar korundu (zemin + MenuCharacterStage). " +
                      "Sahneyi kaydetmeyi unutma (Ctrl+S).");
        }

        private static string Encode(SerializedProperty property)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            return property.propertyType switch
            {
                SerializedPropertyType.Float => "f:" + property.floatValue.ToString("R", c),
                SerializedPropertyType.Integer => "i:" + property.intValue.ToString(c),
                SerializedPropertyType.Boolean => "b:" + (property.boolValue ? "1" : "0"),
                SerializedPropertyType.Enum => "e:" + property.enumValueIndex.ToString(c),
                SerializedPropertyType.Color => "c:" + C(property.colorValue),
                SerializedPropertyType.Vector2 => "v2:" + V3(property.vector2Value),
                SerializedPropertyType.Vector3 => "v3:" + V3(property.vector3Value),
                _ => null
            };
        }

        private static void Decode(SerializedProperty property, string encoded)
        {
            int colon = encoded.IndexOf(':');
            if (colon < 0)
                return;

            string kind = encoded.Substring(0, colon);
            string value = encoded.Substring(colon + 1);
            CultureInfo c = CultureInfo.InvariantCulture;

            switch (kind)
            {
                case "f": property.floatValue = float.Parse(value, c); break;
                case "i": property.intValue = int.Parse(value, c); break;
                case "b": property.boolValue = value == "1"; break;
                case "e": property.enumValueIndex = int.Parse(value, c); break;
                case "c":
                    string[] rgba = value.Split(',');
                    property.colorValue = new Color(float.Parse(rgba[0], c), float.Parse(rgba[1], c),
                        float.Parse(rgba[2], c), float.Parse(rgba[3], c));
                    break;
                case "v2": property.vector2Value = ParseV3(value); break;
                case "v3": property.vector3Value = ParseV3(value); break;
            }
        }

        private static string V3(Vector3 v)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            return $"{v.x.ToString("R", c)},{v.y.ToString("R", c)},{v.z.ToString("R", c)}";
        }

        private static string C(Color v)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            return $"{v.r.ToString("R", c)},{v.g.ToString("R", c)},{v.b.ToString("R", c)},{v.a.ToString("R", c)}";
        }

        private static Vector3 ParseV3(string value)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            string[] p = value.Split(',');
            return new Vector3(float.Parse(p[0], c), float.Parse(p[1], c), p.Length > 2 ? float.Parse(p[2], c) : 0f);
        }
    }
}
#endif
