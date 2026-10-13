#if UNITY_EDITOR
using NewBattle.UI;
using UnityEditor;
using UnityEngine;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// Ana menu: karakter ahsap bir sandigin ustunde durur (MenuCharacterStage > Sandik).
    /// Sandiga esyalarla ayni ToonLoot materyalini baglar ve menudeki eski duz
    /// golge resmini (OrtaAlan/KarakterGolgesi) gizler; golgeyi artik sandik verir.
    /// </summary>
    public static partial class MainMenuBuilder
    {
        private const string CrateMaterialPath = "Assets/GameArt/Materials/ToonLoot.mat";

        private static bool EnsureMenuCrate(MainMenuScreen screen)
        {
            bool changed = false;

            MenuCharacterStage stage = screen.GetComponentInChildren<MenuCharacterStage>(true);
            if (stage != null)
            {
                var serialized = new SerializedObject(stage);
                SerializedProperty material = serialized.FindProperty("crateMaterial");
                if (material != null && material.objectReferenceValue == null)
                {
                    material.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(CrateMaterialPath);
                    serialized.ApplyModifiedProperties();
                    changed = material.objectReferenceValue != null;
                }
            }

            Transform oldShadow = screen.transform.Find("OrtaAlan/KarakterGolgesi");
            if (oldShadow != null && oldShadow.gameObject.activeSelf)
            {
                Undo.RecordObject(oldShadow.gameObject, "Menu golgesi");
                oldShadow.gameObject.SetActive(false);
                changed = true;
            }

            return changed;
        }
    }
}
#endif
