using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using NewBattle.Gameplay;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// TEK SEFERLIK kurulum araci: fontu hazirlar ve HUD'u videodaki
    /// yerlesime tasir.
    ///
    /// Calisma aninda hicbir sey yapmaz; sadece sahneye ve varliklara bir
    /// kez yazar. Boylece sonradan Inspector'dan yaptigin her degisiklik
    /// kalici olur - yerlesimi her karede zorlayan bir bilesen olsaydi
    /// senin ayarlarini surekli geri alirdi.
    ///
    /// Isin bitince bu dosyayi silebilirsin.
    /// </summary>
    public class BattlelandsHudSetup : EditorWindow
    {
        private const string FontSourcePath = "Assets/Fonts/TitanOne-Regular.ttf";
        private const string FontAssetPath = "Assets/Fonts/TitanOne SDF.asset";

        // ------------------------------------------------------------------
        // Videodan olculen yerlesim (ekran genisligi/yuksekliginin orani).
        // Oran olarak tutuluyor cunku sahnedeki canvas referans cozunurlugu
        // degisirse piksel degerleri yanlis olurdu.
        // ------------------------------------------------------------------
        private const float WeaponRightEdge = 0.8626f;  // silah panelinin sag kenari
        private const float WeaponWidth = 0.0699f;
        private const float WeaponHeight = 0.148f;

        private const float MinimapRightEdge = 0.990f;
        private const float MinimapWidth = 0.112f;
        private const float MinimapHeight = 0.156f;

        private const float TopMargin = 0.016f;

        private const float BarsWidth = 0.283f;
        private const float BarsBottom = 0.030f;

        private Vector2 _scroll;

        [MenuItem("Tools/NewBattle/HUD Kurulumu", false, 20)]
        public static void Open()
        {
            GetWindow<BattlelandsHudSetup>("HUD Kurulumu").minSize = new Vector2(430f, 380f);
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("1 - Font", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Assets/Fonts/TitanOne-Regular.ttf dosyasindan TextMeshPro font " +
                "varligi uretir ve sahnedeki tum yazilara uygular.\n\n" +
                "Dinamik mod kullaniliyor: Turkce karakterler (s, g, i, o, u, c) " +
                "kullanildiklari anda atlasa ekleniyor, onceden liste vermeye " +
                "gerek kalmiyor.",
                MessageType.None);

            if (GUILayout.Button("Fontu Hazirla ve Uygula", GUILayout.Height(32f)))
                SetupFont();

            EditorGUILayout.Space(18f);
            EditorGUILayout.LabelField("2 - HUD yerlesimi", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Silah panelini alt ortadan SAG USTE tasir, minimapin yanina " +
                "yerlestirir. Can ve kalkan barlarini alt ortaya hizalar.\n\n" +
                "Olculer videodan alindi. Uyguladiktan sonra Inspector'dan " +
                "diledigin gibi degistirebilirsin; bu arac bir daha karismaz.",
                MessageType.None);

            if (GUILayout.Button("Yerlesimi Uygula", GUILayout.Height(32f)))
                ApplyLayout();

            EditorGUILayout.Space(18f);
            EditorGUILayout.LabelField("3 - Duyuru yazisi", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "ScreenAnnouncer normalde kendi kendini olusturuyor, ama o " +
                "durumda Inspector'dan ayarlayamazsin. Bu dugme onu sahneye " +
                "kalici olarak ekler ve fontu atar.",
                MessageType.None);

            if (GUILayout.Button("Duyuru Nesnesini Sahneye Ekle"))
                AddAnnouncer();

            EditorGUILayout.Space(14f);

            if (GUILayout.Button("Durumu Yaz (degistirmez)"))
                Report();

            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------------
        // Font
        // ------------------------------------------------------------------

        /// <summary>
        /// TTF dosyasindan TMP font varligi uretir.
        ///
        /// Atlas'i DINAMIK kuruyoruz: statik atlas, olusturma aninda hangi
        /// karakterlerin gerekecegini bilmeyi gerektirir. Turkce metinde
        /// sonradan eklenen tek bir "s" harfi bile eksik kalirsa ekranda
        /// bos kare olarak cikar. Dinamik modda glif ilk kullanildiginda
        /// atlasa ekleniyor.
        /// </summary>
        private static void SetupFont()
        {
            Font source = AssetDatabase.LoadAssetAtPath<Font>(FontSourcePath);

            if (source == null)
            {
                Debug.LogError(
                    $"Font bulunamadi: {FontSourcePath}\n" +
                    "Dosya adi TitanOne-Regular.ttf olmali.");
                return;
            }

            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);

            if (fontAsset == null)
            {
                fontAsset = TMP_FontAsset.CreateFontAsset(
                    source,
                    90,                          // ornekleme boyutu
                    9,                           // atlas dolgusu
                    GlyphRenderMode.SDFAA,
                    1024, 1024,
                    AtlasPopulationMode.Dynamic);

                if (fontAsset == null)
                {
                    Debug.LogError("Font varligi uretilemedi. TTF bozuk olabilir.");
                    return;
                }

                fontAsset.name = "TitanOne SDF";
                AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

                // Materyal ve atlas dokusu, font varliginin ALT VARLIGI olarak
                // saklanmali; ayri dosya olurlarsa font tasindiginda kopuyorlar.
                if (fontAsset.material != null)
                {
                    fontAsset.material.name = fontAsset.name + " Material";
                    AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
                }

                if (fontAsset.atlasTextures != null)
                {
                    foreach (Texture2D atlas in fontAsset.atlasTextures)
                    {
                        if (atlas == null)
                            continue;

                        atlas.name = fontAsset.name + " Atlas";
                        AssetDatabase.AddObjectToAsset(atlas, fontAsset);
                    }
                }

                AssetDatabase.SaveAssets();
                Debug.Log($"Font varligi olusturuldu: {FontAssetPath}");
            }

            int changed = ApplyFontToScene(fontAsset);
            Debug.Log($"Font {changed} yaziya uygulandi.");
        }

        private static int ApplyFontToScene(TMP_FontAsset fontAsset)
        {
            int changed = 0;

            foreach (TextMeshProUGUI label in Object.FindObjectsByType<TextMeshProUGUI>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (label.font == fontAsset)
                    continue;

                Undo.RecordObject(label, "Font uygula");
                label.font = fontAsset;
                EditorUtility.SetDirty(label);
                changed++;
            }

            MarkSceneDirty();
            return changed;
        }

        // ------------------------------------------------------------------
        // Yerlesim
        // ------------------------------------------------------------------

        /// <summary>
        /// HUD parcalarini videodaki yerlerine tasir.
        ///
        /// Oranlar canvas'in referans cozunurlugu ile carpiliyor. Sabit
        /// piksel yazsaydik, referans cozunurlugu degistiginde ya da baska
        /// bir cihaz oraninda yerlesim kayardi.
        /// </summary>
        private static void ApplyLayout()
        {
            CanvasScaler scaler = Object.FindFirstObjectByType<CanvasScaler>(FindObjectsInactive.Include);

            Vector2 reference = scaler != null && scaler.referenceResolution.sqrMagnitude > 1f
                ? scaler.referenceResolution
                : new Vector2(1920f, 1080f);

            List<string> log = new()
            {
                "=== HUD YERLESIMI ===",
                $"  Referans cozunurluk: {reference.x:0} x {reference.y:0}"
            };

            // Silah paneli: alt ortadan sag uste
            if (TryFind("CombatSlots", out RectTransform weapon))
            {
                Undo.RecordObject(weapon, "HUD yerlesimi");
                AnchorTopRight(weapon);
                weapon.sizeDelta = new Vector2(WeaponWidth * reference.x, WeaponHeight * reference.y);
                weapon.anchoredPosition = new Vector2(
                    -(1f - WeaponRightEdge) * reference.x,
                    -TopMargin * reference.y);
                EditorUtility.SetDirty(weapon);
                log.Add($"  CombatSlots -> sag ust  {weapon.anchoredPosition}  {weapon.sizeDelta}");
            }
            else
            {
                log.Add("  CombatSlots bulunamadi.");
            }

            // Minimap: silahin sagina
            if (TryFind("Minimap", out RectTransform minimap))
            {
                Undo.RecordObject(minimap, "HUD yerlesimi");
                AnchorTopRight(minimap);
                minimap.sizeDelta = new Vector2(MinimapWidth * reference.x, MinimapHeight * reference.y);
                minimap.anchoredPosition = new Vector2(
                    -(1f - MinimapRightEdge) * reference.x,
                    -TopMargin * reference.y);
                EditorUtility.SetDirty(minimap);
                log.Add($"  Minimap -> sag ust  {minimap.anchoredPosition}  {minimap.sizeDelta}");
            }
            else
            {
                log.Add("  Minimap bulunamadi.");
            }

            // Can + kalkan: alt orta
            if (TryFind("PlayerStatusHUD", out RectTransform bars))
            {
                Undo.RecordObject(bars, "HUD yerlesimi");
                bars.anchorMin = new Vector2(0.5f, 0f);
                bars.anchorMax = new Vector2(0.5f, 0f);
                bars.pivot = new Vector2(0.5f, 0f);
                bars.sizeDelta = new Vector2(BarsWidth * reference.x, bars.sizeDelta.y);
                bars.anchoredPosition = new Vector2(0f, BarsBottom * reference.y);
                EditorUtility.SetDirty(bars);
                log.Add($"  PlayerStatusHUD -> alt orta  {bars.anchoredPosition}  {bars.sizeDelta}");
            }
            else
            {
                log.Add("  PlayerStatusHUD bulunamadi.");
            }

            MarkSceneDirty();
            Debug.Log(string.Join("\n", log));
        }

        private static void AnchorTopRight(RectTransform rect)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
        }

        private static bool TryFind(string name, out RectTransform rect)
        {
            rect = null;

            foreach (RectTransform candidate in Object.FindObjectsByType<RectTransform>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate.name != name)
                    continue;

                rect = candidate;
                return true;
            }

            return false;
        }

        // ------------------------------------------------------------------
        // Duyuru
        // ------------------------------------------------------------------

        private static void AddAnnouncer()
        {
            ScreenAnnouncer existing = Object.FindFirstObjectByType<ScreenAnnouncer>(
                FindObjectsInactive.Include);

            if (existing == null)
            {
                GameObject host = new("ScreenAnnouncer");
                existing = host.AddComponent<ScreenAnnouncer>();
                Undo.RegisterCreatedObjectUndo(host, "Duyuru nesnesi");
            }

            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);

            if (fontAsset != null)
            {
                SerializedObject so = new(existing);
                SerializedProperty fontProperty = so.FindProperty("font");

                if (fontProperty != null)
                {
                    fontProperty.objectReferenceValue = fontAsset;
                    so.ApplyModifiedProperties();
                }
            }

            MarkSceneDirty();
            Debug.Log(fontAsset != null
                ? "ScreenAnnouncer sahneye eklendi, font atandi."
                : "ScreenAnnouncer sahneye eklendi. Once fontu hazirla, sonra tekrar bas.");
        }

        private static void Report()
        {
            List<string> log = new() { "=== HUD DURUMU ===" };

            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            log.Add($"  Font varligi: {(fontAsset != null ? fontAsset.name : "YOK")}");

            int titan = 0, digerleri = 0;

            foreach (TextMeshProUGUI label in Object.FindObjectsByType<TextMeshProUGUI>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (fontAsset != null && label.font == fontAsset)
                    titan++;
                else
                    digerleri++;
            }

            log.Add($"  Titan One kullanan yazi: {titan}");
            log.Add($"  Baska font kullanan    : {digerleri}");

            foreach (string name in new[] { "CombatSlots", "Minimap", "PlayerStatusHUD" })
            {
                if (TryFind(name, out RectTransform rect))
                    log.Add($"  {name,-16} anchor {rect.anchorMin}  konum {rect.anchoredPosition}  boyut {rect.sizeDelta}");
                else
                    log.Add($"  {name,-16} BULUNAMADI");
            }

            log.Add($"  ScreenAnnouncer sahnede: " +
                    $"{Object.FindFirstObjectByType<ScreenAnnouncer>(FindObjectsInactive.Include) != null}");

            Debug.Log(string.Join("\n", log));
        }

        private static void MarkSceneDirty()
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
    }
}
