using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NewBattle.ArtGen
{
    /// <summary>
    /// Kirmizi alan seridini, sonraki alanin beyaz cizgisi gibi ince ve keskin
    /// yapar. Alan disindaki kirmizi pus aynen kalir.
    ///
    /// Botlarin yapay zekasi, bot macinin 16 kisilik olmasi ve bot hizi kod
    /// tarafinda; onlar icin bir sey calistirmak gerekmiyor.
    /// </summary>
    public static class ZoneLineSetup
    {
        /// <summary>Sonraki alanin beyaz cizgisiyle ayni kalinlik.</summary>
        private const float BoundaryWidth = 1.6f;

        /// <summary>Kenarda sadece kenar yumusatma kadar gecis: keskin ama tirtikli degil.</summary>
        private const float BoundaryEdgeSoftness = 0.12f;

        private const float BoundaryAlpha = 0.95f;

        [MenuItem("Tools/NewBattle/Gorsel Yenileme/4 - Alan Cizgisini Keskinlestir", false, 43)]
        public static void Run()
        {
            List<string> log = new();
            SharpenZoneLine(log);
            Debug.Log("Alan cizgisi:\n" + string.Join("\n", log));
        }

        private static void SharpenZoneLine(List<string> log)
        {
            SafeZoneController zone = Object.FindFirstObjectByType<SafeZoneController>(FindObjectsInactive.Include);

            if (zone == null)
            {
                log.Add("UYARI: Sahnede SafeZoneController yok; alan cizgisi ayari atlandi.");
                return;
            }

            SerializedObject serialized = new(zone);
            SerializedProperty width = serialized.FindProperty("boundaryWidth");
            SerializedProperty softness = serialized.FindProperty("boundaryEdgeSoftness");
            SerializedProperty color = serialized.FindProperty("boundaryColor");

            float oldWidth = width.floatValue;
            float oldSoftness = softness.floatValue;

            width.floatValue = BoundaryWidth;
            softness.floatValue = BoundaryEdgeSoftness;

            // Renk ayni kalir, sadece biraz daha dolgun: ince serit soluk kalmasin.
            Color boundary = color.colorValue;
            boundary.a = BoundaryAlpha;
            color.colorValue = boundary;

            // Undo'lu yaz: begenmezsen Ctrl+Z ile geri alinir.
            serialized.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(zone.gameObject.scene);

            log.Add($"Alan cizgisi: genislik {oldWidth:0.##} -> {BoundaryWidth}, " +
                    $"yumusaklik {oldSoftness:0.##} -> {BoundaryEdgeSoftness}. Dis pus degismedi.");
            log.Add("SAHNEYI KAYDETMEYI UNUTMA (Ctrl+S).");
        }
    }
}
