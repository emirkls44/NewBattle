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

            EditorGUILayout.Space(18f);
            EditorGUILayout.LabelField("4 - Hasar sayisi", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Hasar sayilari da kendi kendine olusuyor, o yuzden Inspector'dan " +
                "ayarlanamiyor. Bu dugme sahneye kalici ekler; font, boyut, renk, " +
                "kontur ve suzulme hizini oradan degistirirsin.\n\n" +
                "Play modunda degistirdiginde sonuc aninda gorunur.",
                MessageType.None);

            if (GUILayout.Button("Hasar Sayisini Sahneye Ekle"))
                AddDamageNumbers();

            EditorGUILayout.Space(18f);
            EditorGUILayout.LabelField("5 - Test silahi", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Haritanin merkezine bir silah birakir. Loot prefablari ag " +
                "nesnesi oldugu icin sahneye elle surukleyemiyoruz; bu bilesen " +
                "maci baslatinca host tarafinda doguruyor.\n\n" +
                "Konumu ve neyin birakilacagini Inspector'dan degistirebilirsin.",
                MessageType.None);

            if (GUILayout.Button("Haritaya Test Silahi Koy", GUILayout.Height(32f)))
                AddTestLoot();

            EditorGUILayout.Space(18f);
            EditorGUILayout.LabelField("6 - Harita olculeri", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Zemini OLCER ve alan/kamera/loot ayarlarini ona gore yeniden " +
                "hesaplar.\n\n" +
                "Su an oyun 160x160 bir haritaya gore ayarli ama senin haritan " +
                "80x80. Oyuncu merkezden 76.8 birim yuruyebiliyor, zemin ise " +
                "40'ta bitiyor - yani haritanin kenarindan cikip bosluga " +
                "yurunebiliyor. Alan yaricapi da (102.4) haritanin tamamindan " +
                "buyuk, bu yuzden ilk asamalar hicbir sey yapmiyor.",
                MessageType.Warning);

            if (GUILayout.Button("Alani ve Sinirlari Haritaya Uyarla", GUILayout.Height(34f)))
                RetuneToMap();

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

        /// <summary>
        /// Hasar sayisi havuzunu sahneye kalici olarak ekler.
        ///
        /// Sahnede olmasi sart degil - yoksa kendini olusturuyor - ama o
        /// durumda Inspector'da gorunmedigi icin font ve renk ayarlanamiyor.
        /// </summary>
        private static void AddDamageNumbers()
        {
            DamageNumberPopup existing = Object.FindFirstObjectByType<DamageNumberPopup>(
                FindObjectsInactive.Include);

            if (existing == null)
            {
                GameObject host = new("DamageNumberPool");
                existing = host.AddComponent<DamageNumberPopup>();
                Undo.RegisterCreatedObjectUndo(host, "Hasar sayisi havuzu");
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

            Selection.activeObject = existing.gameObject;
            MarkSceneDirty();

            Debug.Log(fontAsset != null
                ? "DamageNumberPool sahneye eklendi, Titan One atandi. Inspector'dan ayarla."
                : "DamageNumberPool sahneye eklendi. Once fontu hazirla, sonra tekrar bas.");
        }

        /// <summary>
        /// Test loot birakiciyi sahneye ekler.
        ///
        /// Yeni bir nesne yaratmak yerine MEVCUT bir ag nesnesinin uzerine
        /// takiliyor (SafeZone). Sebebi: NetworkBehaviour'un calismasi icin
        /// ayni nesnede kayitli bir NetworkObject gerekiyor. Sifirdan bir
        /// sahne ag nesnesi kurmak, Fusion'un sahne nesnesi kaydina elle
        /// mudahale etmek demek olurdu; hazir ve calisan bir taneye
        /// eklemek hem kisa hem guvenli.
        /// </summary>
        private static void AddTestLoot()
        {
            if (Object.FindFirstObjectByType<TestLootPlacer>(FindObjectsInactive.Include) != null)
            {
                Debug.Log("TestLootPlacer zaten sahnede. Inspector'dan ayarlayabilirsin.");
                return;
            }

            LootZoneManager host = Object.FindFirstObjectByType<LootZoneManager>(
                FindObjectsInactive.Include);

            if (host == null)
            {
                Debug.LogError(
                    "SafeZone (LootZoneManager tasiyan nesne) bulunamadi. " +
                    "TestLootPlacer'in calismak icin kayitli bir NetworkObject'e " +
                    "ihtiyaci var; onu elle bir ag nesnesine ekle.");
                return;
            }

            Undo.AddComponent<TestLootPlacer>(host.gameObject);
            Selection.activeObject = host.gameObject;
            MarkSceneDirty();

            Debug.Log(
                $"TestLootPlacer '{host.gameObject.name}' nesnesine eklendi. " +
                "Liste bos oldugu icin haritanin merkezine (0,0) bir silah birakacak.");
        }

        /// <summary>
        /// Zemini olcup butun mesafe ayarlarini ona gore yeniden hesaplar.
        ///
        /// Neden olcuyoruz: harita elle kuruldugu icin boyutu kodun bilmedigi
        /// bir sey. Sabit bir sayi yazsaydik harita her degistiginde bu arac
        /// da yanlis olurdu. Zemin renderer'larinin sinirlarini olcmek, harita
        /// nasil kurulmus olursa olsun dogru cevabi veriyor.
        /// </summary>
        private static void RetuneToMap()
        {
            if (!TryMeasureGround(out float halfExtent, out string mapName))
            {
                Debug.LogError(
                    "Zemin olculemedi. Sahnede zemin nesneleri (CityGround, " +
                    "ForestGround, SandGround vb.) bulunamadi.");
                return;
            }

            List<string> log = new()
            {
                "=== HARITA OLCULERINE UYARLAMA ===",
                $"  Olculen zemin : {halfExtent * 2f:0.#} x {halfExtent * 2f:0.#} birim ({mapName})",
                $"  Yari genislik : {halfExtent:0.#}",
                string.Empty
            };

            // Oyuncu kenara kadar gidebilsin ama disina cikmasin: kucuk bir
            // pay birakiyoruz, yoksa kenarda duran oyuncunun yarisi bosluga
            // tasiyor.
            float playable = halfExtent * 0.96f;

            // Alan koseleri de kapsamali. Kare bir haritada merkezden koseye
            // uzaklik yari genisligin kok2 katidir; biraz uzerine cikiyoruz.
            float initialRadius = halfExtent * 1.48f;

            if (TrySet<DropPhaseController>("playableMapExtent", playable, log))
                log.Add($"    oyuncu siniri  -> {playable:0.#}");

            if (TrySet<LootSpawner>("spawnExtent", halfExtent * 0.92f, log))
                log.Add($"    loot dagilimi  -> {halfExtent * 0.92f:0.#}");

            if (TrySet<NewBattle.Gameplay.LootZoneManager>("mapRadiusOverride", halfExtent, log))
                log.Add($"    loot bolgeleri -> {halfExtent:0.#}");

            CameraFollow camera = Object.FindFirstObjectByType<CameraFollow>(FindObjectsInactive.Include);

            if (camera != null)
            {
                Undo.RecordObject(camera, "Harita olculeri");
                camera.cameraFocusRadius = playable;
                EditorUtility.SetDirty(camera);
                log.Add($"    kamera siniri  -> {playable:0.#}");
            }

            RetuneZone(initialRadius, halfExtent, log);

            MarkSceneDirty();
            Debug.Log(string.Join("\n", log));
        }

        private static bool TryMeasureGround(out float halfExtent, out string mapName)
        {
            halfExtent = 0f;
            mapName = "-";

            Bounds bounds = default;
            bool any = false;

            foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                // Zemin: genis, yassi ve dunya orijinine yakin yukseklikte.
                // Agaclar, binalar ve karakterler bu filtreye takilmiyor.
                Bounds candidate = renderer.bounds;

                if (candidate.size.x < 10f || candidate.size.z < 10f)
                    continue;

                if (Mathf.Abs(candidate.center.y) > 5f)
                    continue;

                if (!any)
                {
                    bounds = candidate;
                    mapName = renderer.name;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(candidate);
                }
            }

            if (!any)
                return false;

            halfExtent = Mathf.Max(bounds.size.x, bounds.size.z) * 0.5f;
            return halfExtent > 1f;
        }

        /// <summary>
        /// Alan yaricaplarini yeni harita olcusune gore olcekler.
        ///
        /// Asama yaricaplari mutlak deger olarak saklaniyor. Oranlarini
        /// koruyarak olcekliyoruz: asamalarin birbirine gore temposu
        /// (genis -> orta -> dar) el yapimi bir denge, onu bozmak istemiyoruz.
        /// </summary>
        private static void RetuneZone(float initialRadius, float halfExtent, List<string> log)
        {
            SafeZoneController zone = Object.FindFirstObjectByType<SafeZoneController>(
                FindObjectsInactive.Include);

            if (zone == null)
            {
                log.Add("    SafeZoneController bulunamadi.");
                return;
            }

            SerializedObject so = new(zone);

            SerializedProperty initial = so.FindProperty("initialRadius");
            float previousInitial = initial != null ? initial.floatValue : 0f;

            if (initial != null)
            {
                initial.floatValue = initialRadius;
                log.Add($"    alan yaricapi  -> {initialRadius:0.#}  (onceki {previousInitial:0.#})");
            }

            SerializedProperty fog = so.FindProperty("fogOuterRadius");

            if (fog != null)
                fog.floatValue = initialRadius * 2.1f;

            // Videoda alan disi PEMBE kapli, mavi degil.
            SerializedProperty fogColor = so.FindProperty("outsideFogColor");

            if (fogColor != null)
            {
                fogColor.colorValue = new Color(1f, 0.33f, 0.36f, 0.30f);
                log.Add("    alan disi rengi-> pembe (videodaki gibi)");
            }

            SerializedProperty stages = so.FindProperty("stages");

            if (stages != null && stages.isArray && previousInitial > 0.01f)
            {
                float scale = initialRadius / previousInitial;

                for (int i = 0; i < stages.arraySize; i++)
                {
                    SerializedProperty target =
                        stages.GetArrayElementAtIndex(i).FindPropertyRelative("targetRadius");

                    if (target != null)
                        target.floatValue *= scale;
                }

                log.Add($"    {stages.arraySize} asama {scale:0.##} katsayisiyla olceklendi");
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(zone);
        }

        private static bool TrySet<T>(string fieldName, float value, List<string> log)
            where T : Component
        {
            T component = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);

            if (component == null)
            {
                log.Add($"    {typeof(T).Name} bulunamadi.");
                return false;
            }

            SerializedObject so = new(component);
            SerializedProperty property = so.FindProperty(fieldName);

            if (property == null)
            {
                log.Add($"    {typeof(T).Name}.{fieldName} alani yok.");
                return false;
            }

            property.floatValue = value;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(component);
            return true;
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
