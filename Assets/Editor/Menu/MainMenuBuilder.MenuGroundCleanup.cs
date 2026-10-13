#if UNITY_EDITOR
using NewBattle.UI;
using UnityEditor;
using UnityEngine;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// Ana menu zemin/sandik denemesi kaldirildi: karakter yine ayaginin altindaki
    /// yumusak golgeyle (OrtaAlan/KarakterGolgesi) durur. Sahnede kalan zemin objesini
    /// siler, golgeyi geri acar, kamera acisini ve karakterin yuksekligini eski haline
    /// dondurur. Sadece zemin objesi bulunursa calisir (tek seferlik).
    /// </summary>
    public static partial class MainMenuBuilder
    {
        private const float OriginalCameraPitch = 6f;

        private static bool RemoveMenuGround(MainMenuScreen screen)
        {
            GameObject stageRoot = GameObject.Find(StageName);
            if (stageRoot == null)
                return false;

            bool found = false;
            for (int i = stageRoot.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = stageRoot.transform.GetChild(i);
                if (child.name != "KarakterZemini" && child.name != "Zemin")
                    continue;

                Undo.DestroyObjectImmediate(child.gameObject);
                found = true;
            }

            if (!found)
                return false;

            // Ayagin altindaki golge geri gelsin.
            Transform shadow = screen.transform.Find("OrtaAlan/KarakterGolgesi");
            if (shadow != null && !shadow.gameObject.activeSelf)
            {
                Undo.RecordObject(shadow.gameObject, "Menu golgesi");
                shadow.gameObject.SetActive(true);
            }

            MenuCharacterStage stage = screen.GetComponentInChildren<MenuCharacterStage>(true);
            if (stage != null)
            {
                var serialized = new SerializedObject(stage);
                SerializedProperty pitch = serialized.FindProperty("cameraPitch");
                if (pitch != null)
                    pitch.floatValue = OriginalCameraPitch;

                // Zemine oturtma karakterin modelini yukari/asagi kaydirmisti: geri al.
                var turntable = serialized.FindProperty("turntable").objectReferenceValue as Transform;
                Animator model = turntable != null ? turntable.GetComponentInChildren<Animator>(true) : null;
                if (model != null && model.transform != turntable && model.transform.localPosition != Vector3.zero)
                {
                    Undo.RecordObject(model.transform, "Menu karakteri");
                    model.transform.localPosition = Vector3.zero;
                }

                serialized.ApplyModifiedProperties();
            }

            return true;
        }
    }
}
#endif
