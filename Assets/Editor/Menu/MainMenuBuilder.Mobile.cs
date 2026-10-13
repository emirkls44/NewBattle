#if UNITY_EDITOR
using NewBattle.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// Telefon ekranina uyum:
    ///  - Ekran olcekleyicileri yukseklige esitlenir (20:9 telefonlarda dikeyde hep 1080 birim;
    ///    eskiden 0.5 oldugu icin menudeki alt kartlar kesiliyordu).
    ///  - Joystickler buyur, koselerden iceri/yukari alinir, yuvarlak ve saydam olur
    ///    (tek seferlik; sonra Hierarchy'den istedigin gibi tasiyabilirsin).
    /// </summary>
    public static partial class MainMenuBuilder
    {
        private const int JoystickLayoutVersion = 1;
        private const float JoystickSize = 300f;
        private static readonly Vector2 JoystickOffset = new(340f, 300f);

        private static bool EnsureHeightMatchedScalers()
        {
            bool changed = false;

            foreach (CanvasScaler scaler in Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                changed |= MatchHeight(scaler);

            const string loadingPrefab = "Assets/Prefabs/UI/YuklemeEkrani.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(loadingPrefab);
            if (prefab != null && prefab.TryGetComponent(out CanvasScaler prefabScaler) && MatchHeight(prefabScaler))
            {
                PrefabUtility.SavePrefabAsset(prefab);
                changed = true;
            }

            return changed;
        }

        private static bool MatchHeight(CanvasScaler scaler)
        {
            // Sadece eski varsayilan (0.5) degistirilir; elle baska deger verdiysen dokunulmaz.
            if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize ||
                !Mathf.Approximately(scaler.matchWidthOrHeight, 0.5f))
                return false;

            Undo.RecordObject(scaler, "Olcekleyici yukseklige esit");
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            EditorUtility.SetDirty(scaler);
            return true;
        }

        private const int FloatingJoystickVersion = 2;
        /// <summary>Dokunma alanlarinin ekran yuksekligindeki ust siniri (alt %60).</summary>
        private const float JoystickZoneTop = 0.6f;

        /// <summary>
        /// Yuzen joystick: ekran ortadan ikiye bolunur. Sol yarinin altina nereye
        /// dokunulursa hareket joystick'i, sag yarinin altina dokunulursa saldiri
        /// joystick'i parmagin altinda belirir. Alanlar gorunmez ve diger tum
        /// arayuzun ARKASINDA durur (butonlarin dokunusunu kapmaz). Tek seferlik.
        /// </summary>
        private static bool EnsureFloatingJoysticks()
        {
            bool changed = false;

            foreach (VirtualJoystick joystick in Object.FindObjectsByType<VirtualJoystick>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var serialized = new SerializedObject(joystick);
                SerializedProperty version = serialized.FindProperty("layoutVersion");
                SerializedProperty zoneProperty = serialized.FindProperty("touchZone");
                if (version == null || zoneProperty == null || version.intValue >= FloatingJoystickVersion)
                    continue;

                var rect = (RectTransform)joystick.transform;
                var parent = rect.parent as RectTransform;
                if (parent == null)
                    continue;

                bool right = rect.anchorMin.x > 0.5f;
                string zoneName = right ? "SaldiriAlani" : "HareketAlani";

                var zone = parent.Find(zoneName) as RectTransform;
                if (zone == null)
                {
                    zone = NewRect(zoneName, parent);
                    Undo.RegisterCreatedObjectUndo(zone.gameObject, "Joystick alani");
                    zone.anchorMin = new Vector2(right ? 0.5f : 0f, 0f);
                    zone.anchorMax = new Vector2(right ? 1f : 0.5f, JoystickZoneTop);
                    zone.offsetMin = Vector2.zero;
                    zone.offsetMax = Vector2.zero;
                    AddImage(zone, null, new Color(0f, 0f, 0f, 0f), raycast: true);
                    zone.SetAsFirstSibling(); // diger her seyin arkasinda
                }

                zoneProperty.objectReferenceValue = zone;
                version.intValue = FloatingJoystickVersion;
                serialized.ApplyModifiedProperties();
                changed = true;
            }

            return changed;
        }

        private static bool EnsureMobileJoysticks()
        {
            bool changed = false;

            foreach (VirtualJoystick joystick in Object.FindObjectsByType<VirtualJoystick>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var serialized = new SerializedObject(joystick);
                SerializedProperty version = serialized.FindProperty("layoutVersion");
                if (version == null || version.intValue >= JoystickLayoutVersion)
                    continue;

                var rect = (RectTransform)joystick.transform;
                Undo.RecordObject(rect, "Joystick yerlesimi");

                // Sol alt koseye bagliysa hareket, sag alta bagliysa ates joystick'i.
                bool right = rect.anchorMin.x > 0.5f;
                rect.anchorMin = rect.anchorMax = new Vector2(right ? 1f : 0f, 0f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(JoystickSize, JoystickSize);
                rect.anchoredPosition = new Vector2(right ? -JoystickOffset.x : JoystickOffset.x, JoystickOffset.y);

                serialized.FindProperty("applyBattleRoyaleStyle").boolValue = true;
                serialized.FindProperty("handleSizeRatio").floatValue = 0.42f;
                version.intValue = JoystickLayoutVersion;
                serialized.ApplyModifiedProperties();
                changed = true;
            }

            return changed;
        }
    }
}
#endif
