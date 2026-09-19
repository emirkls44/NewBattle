using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// HUD rozetlerini DUZENLENEBILIR sahne nesnesi olarak kurar.
    ///
    /// Her rozet iki parca:
    ///   Rozet  -> Image   (arka plan; rengini, saydamligini, boyutunu sen ayarlarsin)
    ///     Icon -> TMP     (emoji; fontunu, boyutunu, rengini sen ayarlarsin)
    ///
    /// Arac calisma aninda HICBIR SEY yapmiyor; bir kez kuruyor, sonra
    /// karismiyor. Boylece Inspector'dan yaptigin her degisiklik kalici.
    /// Isin bitince bu dosyayi silebilirsin.
    /// </summary>
    public class HudIconBuilder : EditorWindow
    {
        private const string EmojiFontPath = "Assets/Fonts/Emoji SDF.asset";

        /// <summary>
        /// Emoji karakterleri kod noktasi olarak yaziliyor, kaynak dosyaya
        /// dogrudan gomulmuyor: proje ASCII tutuluyor ve bazi editorler
        /// yuzey disi karakterleri bozuyor.
        /// </summary>
        private const int PersonCodePoint = 0x1F464;   // kisi
        private const int SkullCodePoint = 0x1F480;    // kurukafa
        private const int ShieldCodePoint = 0x1F6E1;   // kalkan
        private const int PlusCodePoint = 0x2795;      // arti

        private Color _backgroundColor = new(0f, 0f, 0f, 0.45f);
        private float _badgeSize = 46f;
        private float _iconFontSize = 26f;
        private Vector2 _scroll;

        [MenuItem("Tools/NewBattle/HUD Rozetleri", false, 21)]
        public static void Open()
        {
            GetWindow<HudIconBuilder>("HUD Rozetleri").minSize = new Vector2(440f, 430f);
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Ortak ayarlar", EditorStyles.boldLabel);
            _backgroundColor = EditorGUILayout.ColorField("Arka plan rengi", _backgroundColor);
            _badgeSize = EditorGUILayout.Slider("Rozet boyutu", _badgeSize, 20f, 100f);
            _iconFontSize = EditorGUILayout.Slider("Emoji boyutu", _iconFontSize, 8f, 60f);

            EditorGUILayout.HelpBox(
                "Bunlar sadece KURULUM anindaki baslangic degerleri. Kurduktan " +
                "sonra her rozeti Inspector'dan ayri ayri degistirebilirsin.",
                MessageType.None);

            TMP_FontAsset emojiFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(EmojiFontPath);

            EditorGUILayout.Space(10f);

            if (emojiFont == null)
            {
                EditorGUILayout.HelpBox(
                    $"Emoji fontu yok: {EmojiFontPath}\n\n" +
                    "Rozetler yine de kurulur ama emoji yerine bos kare gorunur. " +
                    "Fontu uretmek icin: Window > TextMeshPro > Font Asset Creator, " +
                    "Source Font File = SegoeUIEmoji, Character Set = Custom Range, " +
                    "aralik = 1F464,1F480,1F6E1,2795",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox($"Emoji fontu bulundu: {emojiFont.name}", MessageType.Info);
            }

            if (GUILayout.Button("Emoji Fontunu Uret", GUILayout.Height(30f)))
                BuildEmojiFont();

            EditorGUILayout.Space(16f);
            EditorGUILayout.LabelField("Kendi ikonlarin", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Assets/GameArt/HudIcons klasorundeki PNG dosyalarini rozetlere " +
                "yerlestirir. Emoji yazisini Image bileseniyle degistirir.\n\n" +
                "Person -> kalan oyuncu,  Skull -> skor,\n" +
                "Health -> can bari,      Shield -> kalkan bari\n\n" +
                "Sonradan baska bir gorsel istersen Icon nesnesinin Source Image " +
                "alanina surukle, yeter.",
                MessageType.None);

            if (GUILayout.Button("Ikonlari Yerlestir", GUILayout.Height(34f)))
                PlaceCustomIcons();

            EditorGUILayout.Space(16f);
            EditorGUILayout.LabelField("Can ve kalkan barlari", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Barlardaki sayilari gizler, barlarin soluna emoji rozeti koyar.\n\n" +
                "Sayilar silinmiyor sadece kapatiliyor: LocalPlayerHUD onlara " +
                "referans tutuyor, silinirse her karede bos referans kontrolu " +
                "gerekirdi. Kapali nesneye yazmak zararsiz.",
                MessageType.None);

            if (GUILayout.Button("Can ve Kalkan Barini Kur", GUILayout.Height(34f)))
                BuildBars(emojiFont);

            EditorGUILayout.Space(16f);
            EditorGUILayout.LabelField("Sol ust rozetler", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "KALAN / SKOR kelimelerini kaldirir, yerlerine emoji rozeti koyar. " +
                "Sayilar rozetin sagina kayar.",
                MessageType.None);

            if (GUILayout.Button("Sol Ust Rozetleri Kur", GUILayout.Height(34f)))
                BuildTopBadges(emojiFont);

            EditorGUILayout.Space(16f);
            EditorGUILayout.LabelField("Minimap", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Minimap su an calisma aninda uretiliyor, o yuzden Hierarchy'de " +
                "gorunmuyor ve Play'e basmadan duzenleyemiyorsun.\n\n" +
                "Bu dugme minimapi gercek sahne nesnesi olarak kurar: cerceve, " +
                "harita, alan cemberleri, isaretciler - hepsi ayri ayri " +
                "Hierarchy'de durur ve Inspector'dan ayarlanir.",
                MessageType.None);

            if (GUILayout.Button("Minimapi Hiyerarsiye Kur", GUILayout.Height(34f)))
                BuildMinimap();

            EditorGUILayout.Space(16f);
            EditorGUILayout.LabelField("Kurtarma", EditorStyles.boldLabel);

            if (GUILayout.Button("Yazilari Varsayilan Fonta Dondur"))
                ResetFonts();

            if (GUILayout.Button("Kurulan Rozetleri Sil"))
                RemoveBadges();

            EditorGUILayout.Space(10f);

            if (GUILayout.Button("Durumu Yaz"))
                Report();

            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------------
        // Minimap
        // ------------------------------------------------------------------

        /// <summary>
        /// Minimapi sahne nesnesi olarak kurar ve MinimapSystem'e baglar.
        ///
        /// MinimapSystem, minimapRoot alani doluysa UI'i calisma aninda
        /// URETMIYOR; buradaki hazir nesneyi kullaniyor. Boylece cerceve,
        /// boyut, renkler Inspector'dan gorerek ayarlanabiliyor.
        ///
        /// Alt nesneler ADA gore bulunuyor, serilestirilmis referansla
        /// degil: bir parcayi silip yeniden olusturursan ad eslemesi
        /// calismaya devam eder, referans ise kopardi.
        /// </summary>
        private static void BuildMinimap()
        {
            MinimapSystem system = Object.FindFirstObjectByType<MinimapSystem>(
                FindObjectsInactive.Include);

            if (system == null)
            {
                Debug.LogError("Sahnede MinimapSystem yok.");
                return;
            }

            Canvas canvas = system.GetComponentInParent<Canvas>();

            if (canvas == null)
            {
                Debug.LogError("MinimapSystem bir Canvas altinda olmali.");
                return;
            }

            SerializedObject so = new(system);
            SerializedProperty rootProperty = so.FindProperty("minimapRoot");

            if (rootProperty == null)
            {
                Debug.LogError("MinimapSystem'de minimapRoot alani yok. Script guncel mi?");
                return;
            }

            if (rootProperty.objectReferenceValue != null)
            {
                Debug.Log("Minimap zaten sahnede kurulu. Yeniden kurmak icin " +
                          "once Hierarchy'den 'Minimap' nesnesini sil.");
                return;
            }

            float size = ReadFloat(so, "minimapSize", 220f);
            Vector2 corner = ReadVector(so, "cornerOffset", new Vector2(25f, 25f));
            float borderThickness = ReadFloat(so, "borderThickness", 3f);
            Color borderColor = ReadColor(so, "borderColor", Color.white);
            Color currentZoneColor = ReadColor(so, "currentZoneColor", new Color(1f, 0.24f, 0.42f, 0.95f));
            Color nextZoneColor = ReadColor(so, "nextZoneColor", new Color(1f, 1f, 1f, 0.95f));
            Color airdropColor = ReadColor(so, "airdropColor", new Color(1f, 0.85f, 0.15f, 1f));
            Color teammateColor = ReadColor(so, "teammateColor", new Color(0.25f, 0.66f, 1f, 1f));

            GameObject root = new("Minimap", typeof(RectTransform), typeof(Image), typeof(Mask));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.SetParent(canvas.transform, false);
            rootRect.anchorMin = Vector2.one;
            rootRect.anchorMax = Vector2.one;
            rootRect.pivot = Vector2.one;
            rootRect.anchoredPosition = new Vector2(-corner.x, -corner.y);
            rootRect.sizeDelta = new Vector2(size, size);

            Image mask = root.GetComponent<Image>();
            mask.color = Color.white;
            mask.raycastTarget = false;
            root.GetComponent<Mask>().showMaskGraphic = false;

            // Harita dokusu. Texture calisma aninda atanir; burada bos.
            GameObject mapObject = new("MapImage", typeof(RectTransform), typeof(RawImage));
            RectTransform mapRect = mapObject.GetComponent<RectTransform>();
            mapRect.SetParent(rootRect, false);
            Stretch(mapRect);
            RawImage mapImage = mapObject.GetComponent<RawImage>();
            mapImage.color = Color.white;
            mapImage.raycastTarget = false;

            CreateCircle("CurrentZone", rootRect, currentZoneColor, 4f, Vector2.zero);
            CreateCircle("NextZone", rootRect, nextZoneColor, 3f, Vector2.zero);

            RectTransform airdrop = CreateCircle("AirdropMarker", rootRect, airdropColor, 8f,
                new Vector2(16f, 16f));
            airdrop.gameObject.SetActive(false);

            CreateCircle("LocalPlayerMarker", rootRect, new Color(0.15f, 1f, 0.2f, 1f), 8f,
                new Vector2(16f, 16f));

            int teammateCount = Mathf.Max(0, ReadInt(so, "maxTeammateMarkers", 3));
            float teammateSize = ReadFloat(so, "teammateMarkerSize", 13f);

            for (int i = 0; i < teammateCount; i++)
            {
                RectTransform marker = CreateCircle($"Teammate_{i}", rootRect, teammateColor,
                    teammateSize * 0.5f, new Vector2(teammateSize, teammateSize));
                marker.gameObject.SetActive(false);
            }

            CreateBorder(rootRect, borderColor, borderThickness);

            rootProperty.objectReferenceValue = rootRect;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(system);

            Undo.RegisterCreatedObjectUndo(root, "Minimap kur");
            Selection.activeObject = root;
            MarkSceneDirty();

            Debug.Log(
                "Minimap Hierarchy'ye kuruldu ve MinimapSystem'e baglandi.\n" +
                "  Minimap            -> cerceve maskesi, boyut, konum\n" +
                "  Minimap/MapImage   -> harita goruntusu\n" +
                "  Minimap/CurrentZone, NextZone -> alan cemberleri\n" +
                "  Minimap/AirdropMarker, LocalPlayerMarker, Teammate_N\n" +
                "  Minimap/Border     -> dort kenar serit\n\n" +
                "Artik UI calisma aninda uretilmiyor; hepsini Inspector'dan ayarla.");
        }

        private static RectTransform CreateCircle(string name, RectTransform parent,
            Color color, float thickness, Vector2 size)
        {
            GameObject circle = new(name, typeof(RectTransform), typeof(CircleOutlineGraphic));
            RectTransform rect = circle.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

            if (size.sqrMagnitude > 0.01f)
                rect.sizeDelta = size;

            CircleOutlineGraphic graphic = circle.GetComponent<CircleOutlineGraphic>();
            graphic.raycastTarget = false;
            graphic.color = color;
            graphic.SetThickness(thickness);

            return rect;
        }

        /// <summary>
        /// Kare cerceve: dort ince serit.
        ///
        /// Tek bir 9-slice sprite yerine dort dikdortgen, cunku disaridan
        /// bir sprite varligina bagimli olmak istemiyoruz - eksik bir asset
        /// cerceveyi sessizce yok ederdi.
        /// </summary>
        private static void CreateBorder(RectTransform parent, Color color, float thickness)
        {
            GameObject borderRoot = new("Border", typeof(RectTransform));
            RectTransform rootRect = borderRoot.GetComponent<RectTransform>();
            rootRect.SetParent(parent, false);
            Stretch(rootRect);

            CreateEdge(rootRect, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, thickness), color);
            CreateEdge(rootRect, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, thickness), color);
            CreateEdge(rootRect, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(thickness, 0f), color);
            CreateEdge(rootRect, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(thickness, 0f), color);
        }

        private static void CreateEdge(RectTransform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Color color)
        {
            GameObject edge = new(name, typeof(RectTransform), typeof(Image));
            RectTransform rect = edge.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;

            Image image = edge.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static float ReadFloat(SerializedObject so, string field, float fallback)
        {
            SerializedProperty p = so.FindProperty(field);
            return p != null ? p.floatValue : fallback;
        }

        private static int ReadInt(SerializedObject so, string field, int fallback)
        {
            SerializedProperty p = so.FindProperty(field);
            return p != null ? p.intValue : fallback;
        }

        private static Color ReadColor(SerializedObject so, string field, Color fallback)
        {
            SerializedProperty p = so.FindProperty(field);
            return p != null ? p.colorValue : fallback;
        }

        private static Vector2 ReadVector(SerializedObject so, string field, Vector2 fallback)
        {
            SerializedProperty p = so.FindProperty(field);
            return p != null ? p.vector2Value : fallback;
        }

        // ------------------------------------------------------------------
        // Kendi ikonlarin
        // ------------------------------------------------------------------

        private const string IconFolder = "Assets/GameArt/HudIcons";

        /// <summary>Rozet adi -> o rozette duracak PNG dosyasinin adi.</summary>
        private static readonly (string Badge, string Icon)[] IconMap =
        {
            ("AliveCountBadge", "Person"),
            ("KillCountBadge", "Skull"),
            ("HealthBarIcon", "Health"),
            ("ShieldBarIcon", "Shield")
        };

        /// <summary>
        /// Rozetlerdeki emoji yazisini gorsele cevirir.
        ///
        /// Gorsel, fonta bagli olmadigi icin emoji yolundaki butun
        /// sorunlardan (eksik glif, bozuk atlas, lisans) kurtuluyoruz.
        /// Ustelik sonradan degistirmek tek surukleme.
        /// </summary>
        private static void PlaceCustomIcons()
        {
            List<string> log = new() { "=== IKONLAR YERLESTIRILIYOR ===" };

            foreach ((string badgeName, string iconName) in IconMap)
            {
                string path = $"{IconFolder}/{iconName}.png";

                Sprite sprite = EnsureSprite(path, log);

                if (sprite == null)
                    continue;

                if (!TryFind(badgeName, out RectTransform badge))
                {
                    log.Add($"  {badgeName} bulunamadi - once rozetleri kur.");
                    continue;
                }

                ReplaceIcon(badge, sprite, log);
            }

            MarkSceneDirty();
            Debug.Log(string.Join("\n", log));
        }

        /// <summary>
        /// PNG'nin Sprite olarak iceri alindigindan emin olur.
        ///
        /// Varsayilan iceri alma tipi "Default"; Image bileseni o haliyle
        /// dosyayi kabul etmiyor. Elle degistirmeyi unutmak, "gorsel neden
        /// gorunmuyor" diye saatler kaybettiren klasik bir tuzak.
        /// </summary>
        private static Sprite EnsureSprite(string path, List<string> log)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                log.Add($"  {path} bulunamadi.");
                return null;
            }

            if (importer.textureType != TextureImporterType.Sprite || !importer.alphaIsTransparency)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
                log.Add($"  {System.IO.Path.GetFileName(path)}: Sprite olarak ayarlandi");
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void ReplaceIcon(RectTransform badge, Sprite sprite, List<string> log)
        {
            Transform existing = badge.Find("Icon");

            if (existing != null)
                Undo.DestroyObjectImmediate(existing.gameObject);

            GameObject icon = new("Icon", typeof(RectTransform), typeof(Image));
            RectTransform rect = icon.GetComponent<RectTransform>();
            rect.SetParent(badge, false);

            // Arka plani doldur ama kenardan biraz bosluk birak; ikon
            // cercevenin tam dibine yapisirsa sikismis gorunuyor.
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(6f, 6f);
            rect.offsetMax = new Vector2(-6f, -6f);

            Image image = icon.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.raycastTarget = false;

            // Kare olmayan gorseller ezilmesin.
            image.preserveAspect = true;

            Undo.RegisterCreatedObjectUndo(icon, "Ikon yerlestir");
            log.Add($"  {badge.name}/Icon -> {sprite.name}");
        }

        // ------------------------------------------------------------------
        // Emoji fontu
        // ------------------------------------------------------------------

        /// <summary>
        /// SegoeUIEmoji.ttf dosyasindan sadece gereken dort emojiyi iceren
        /// bir TMP font varligi uretir ve rozetlere atar.
        ///
        /// Neden sadece dort karakter: Segoe UI Emoji binlerce glif tasiyor.
        /// Hepsini atlasa basmak yuz megabaytlik bir doku ve uzun bir
        /// uretim suresi demek. Bize dort tane lazim.
        ///
        /// Uretim sonrasi SONUC DOGRULANIYOR: her karakterin atlasa girip
        /// girmedigi tek tek kontrol edilip konsola yaziliyor. Onceki
        /// denemede font varligi sessizce bozuk uretilmis ve bunu ancak
        /// ekranda anlamsiz karakterler gorunce anlamistik.
        /// </summary>
        private static void BuildEmojiFont()
        {
            const string sourcePath = "Assets/Fonts/SegoeUIEmoji.ttf";

            Font source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);

            if (source == null)
            {
                Debug.LogError(
                    $"Font dosyasi bulunamadi: {sourcePath}\n" +
                    "Windows'taki C:/Windows/Fonts/seguiemj.ttf dosyasini " +
                    "Assets/Fonts/ icine SegoeUIEmoji.ttf adiyla kopyala.");
                return;
            }

            uint[] wanted =
            {
                PersonCodePoint, SkullCodePoint, ShieldCodePoint, PlusCodePoint
            };

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                source,
                90,                       // ornekleme boyutu
                9,                        // atlas dolgusu
                GlyphRenderMode.SDFAA,
                512, 512,
                AtlasPopulationMode.Dynamic);

            if (fontAsset == null)
            {
                Debug.LogError("Font varligi uretilemedi.");
                return;
            }

            fontAsset.name = "Emoji SDF";

            // Karakterleri simdi atlasa bas, sonra STATIK'e cevir. Dinamik
            // kalsaydi glifler calisma aninda eklenmeye calisilir ve cihazda
            // kaynak font bulunamazsa bos kare cikardi.
            fontAsset.TryAddCharacters(wanted, out uint[] missing);
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;

            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(EmojiFontPath) != null)
                AssetDatabase.DeleteAsset(EmojiFontPath);

            AssetDatabase.CreateAsset(fontAsset, EmojiFontPath);

            // Materyal ve atlas dokusu ALT VARLIK olmali; ayri dosya
            // olurlarsa font tasindiginda referanslari kopuyor.
            if (fontAsset.material != null)
            {
                fontAsset.material.name = "Emoji SDF Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            if (fontAsset.atlasTextures != null)
            {
                foreach (Texture2D atlas in fontAsset.atlasTextures)
                {
                    if (atlas == null)
                        continue;

                    atlas.name = "Emoji SDF Atlas";
                    AssetDatabase.AddObjectToAsset(atlas, fontAsset);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ---- Dogrulama ----
            List<string> log = new() { "=== EMOJI FONTU ===", $"  Kaydedildi: {EmojiFontPath}" };

            int ok = 0;

            foreach (uint code in wanted)
            {
                bool has = fontAsset.characterLookupTable.ContainsKey(code);

                if (has)
                    ok++;

                log.Add($"  U+{code:X4} {(has ? "VAR" : "YOK")}   {char.ConvertFromUtf32((int)code)}");
            }

            if (missing != null && missing.Length > 0)
                log.Add($"  Atlasa eklenemeyen: {missing.Length} karakter");

            log.Add(string.Empty);

            if (ok == wanted.Length)
            {
                int assigned = AssignEmojiFont(fontAsset);
                log.Add($"Dort emoji de hazir. {assigned} rozete atandi.");
            }
            else
            {
                log.Add(
                    $"{wanted.Length - ok} emoji uretilemedi. Segoe UI Emoji renkli " +
                    "katmanli bir font; bazi gliflerin duz cizgi hali olmayabiliyor. " +
                    "Bu durumda emoji yerine PNG ikon kullanmamiz gerekir - soyle, " +
                    "ikonlari cizip veririm.");
            }

            Debug.Log(string.Join("\n", log));
        }

        /// <summary>Kurulmus rozetlerin Icon yazilarina emoji fontunu atar.</summary>
        private static int AssignEmojiFont(TMP_FontAsset fontAsset)
        {
            int assigned = 0;

            foreach (TextMeshProUGUI label in Object.FindObjectsByType<TextMeshProUGUI>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (label.name != "Icon")
                    continue;

                Undo.RecordObject(label, "Emoji fontu ata");
                label.font = fontAsset;
                EditorUtility.SetDirty(label);
                assigned++;
            }

            MarkSceneDirty();
            return assigned;
        }

        // ------------------------------------------------------------------
        // Barlar
        // ------------------------------------------------------------------

        private void BuildBars(TMP_FontAsset emojiFont)
        {
            List<string> log = new() { "=== CAN VE KALKAN BARI ===" };

            HideValue("HealthValue", log);
            HideValue("ShieldValue", log);

            AttachBarBadge("HealthBar", PlusCodePoint, emojiFont, log);
            AttachBarBadge("ShieldBar", ShieldCodePoint, emojiFont, log);

            MarkSceneDirty();
            Debug.Log(string.Join("\n", log));
        }

        /// <summary>
        /// Barin SOL KENARININ disina rozet koyar.
        ///
        /// Konum, barin kendi olculerinden hesaplaniyor: barin sol kenari
        /// (merkez - yarim genislik) bulunup rozet onun biraz soluna
        /// aliniyor. Sabit bir sayi yazsaydik bar genisligi degistiginde
        /// rozet barin uzerine binerdi.
        /// </summary>
        private void AttachBarBadge(string barName, int codePoint, TMP_FontAsset emojiFont,
            List<string> log)
        {
            if (!TryFind(barName, out RectTransform bar))
            {
                log.Add($"  {barName} bulunamadi.");
                return;
            }

            string badgeName = barName + "Icon";

            if (TryFind(badgeName, out RectTransform _))
            {
                log.Add($"  {badgeName} zaten var, dokunulmadi.");
                return;
            }

            float leftEdge = bar.anchoredPosition.x - bar.sizeDelta.x * 0.5f;
            float size = Mathf.Max(_badgeSize * 0.75f, bar.sizeDelta.y * 1.4f);

            RectTransform badge = CreateBadge(
                badgeName, bar.parent, bar.anchorMin, bar.anchorMax, bar.pivot,
                new Vector2(size, size),
                new Vector2(leftEdge - size * 0.5f - 8f, bar.anchoredPosition.y),
                codePoint, emojiFont, size * 0.6f);

            log.Add($"  {badgeName} kuruldu  konum {badge.anchoredPosition}  boyut {size:0}");
        }

        // ------------------------------------------------------------------
        // Sol ust
        // ------------------------------------------------------------------

        private void BuildTopBadges(TMP_FontAsset emojiFont)
        {
            List<string> log = new() { "=== SOL UST ROZETLER ===" };

            ClearStatusPrefixes(log);

            AttachTopBadge("AliveCount", PersonCodePoint, emojiFont, log);
            AttachTopBadge("KillCount", SkullCodePoint, emojiFont, log);

            MarkSceneDirty();
            Debug.Log(string.Join("\n", log));
        }

        /// <summary>
        /// "KALAN" ve "SKOR" kelimelerini kaldirir; geriye sadece sayi kalir.
        /// Kelimeler kodda degil ayarda tutuldugu icin tek satirlik is.
        /// </summary>
        private static void ClearStatusPrefixes(List<string> log)
        {
            BattleStatusHUD status = Object.FindFirstObjectByType<BattleStatusHUD>(
                FindObjectsInactive.Include);

            if (status == null)
            {
                log.Add("  BattleStatusHUD bulunamadi.");
                return;
            }

            SerializedObject so = new(status);

            foreach (string field in new[] { "alivePrefix", "killsPrefix" })
            {
                SerializedProperty property = so.FindProperty(field);

                if (property != null)
                    property.stringValue = string.Empty;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(status);
            log.Add("  KALAN / SKOR kelimeleri kaldirildi");
        }

        private void AttachTopBadge(string labelName, int codePoint, TMP_FontAsset emojiFont,
            List<string> log)
        {
            if (!TryFind(labelName, out RectTransform label))
            {
                log.Add($"  {labelName} bulunamadi.");
                return;
            }

            string badgeName = labelName + "Badge";

            if (TryFind(badgeName, out RectTransform _))
            {
                log.Add($"  {badgeName} zaten var, dokunulmadi.");
                return;
            }

            // Rozet, yazinin KARDESI olarak ekleniyor (cocugu degil): ikisini
            // ayri ayri tasiyabilesin ve birini silmek digerini etkilemesin.
            RectTransform badge = CreateBadge(
                badgeName, label.parent, label.anchorMin, label.anchorMax, label.pivot,
                new Vector2(_badgeSize, _badgeSize),
                label.anchoredPosition,
                codePoint, emojiFont, _iconFontSize);

            // Sayi rozetin sagina kaysin, uzerine binmesin.
            Undo.RecordObject(label, "Sayiyi kaydir");
            label.anchoredPosition += new Vector2(_badgeSize + 10f, 0f);
            EditorUtility.SetDirty(label);

            log.Add($"  {badgeName} kuruldu  konum {badge.anchoredPosition}");
        }

        // ------------------------------------------------------------------
        // Ortak
        // ------------------------------------------------------------------

        /// <summary>
        /// Arka plan (Image) + emoji (TMP) ikilisini kurar.
        ///
        /// Ikisi ayri nesne: arka planin rengini degistirmek emojiyi,
        /// emojinin boyutunu degistirmek arka plani etkilemiyor. Tek
        /// nesnede birlestirseydik ikisini bagimsiz ayarlayamazdin.
        /// </summary>
        private RectTransform CreateBadge(string name, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 size, Vector2 position,
            int codePoint, TMP_FontAsset emojiFont, float fontSize)
        {
            GameObject badge = new(name, typeof(RectTransform), typeof(Image));
            RectTransform rect = badge.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            Image background = badge.GetComponent<Image>();
            background.color = _backgroundColor;
            background.raycastTarget = false;

            GameObject icon = new("Icon", typeof(RectTransform));
            RectTransform iconRect = icon.GetComponent<RectTransform>();
            iconRect.SetParent(rect, false);
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;

            TextMeshProUGUI label = icon.AddComponent<TextMeshProUGUI>();
            label.text = char.ConvertFromUtf32(codePoint);
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            label.enableWordWrapping = false;

            if (emojiFont != null)
                label.font = emojiFont;

            Undo.RegisterCreatedObjectUndo(badge, "HUD rozeti");
            return rect;
        }

        /// <summary>
        /// Bardaki sayiyi gizler.
        ///
        /// Silmiyoruz kapatiyoruz: LocalPlayerHUD bu yazilara referans
        /// tutuyor. Kapali bir nesneye yazmak zararsiz, ama silinmis bir
        /// referansa yazmak her karede kontrol gerektirirdi.
        /// </summary>
        private static void HideValue(string objectName, List<string> log)
        {
            if (!TryFind(objectName, out RectTransform rect))
            {
                log.Add($"  {objectName} bulunamadi.");
                return;
            }

            Undo.RecordObject(rect.gameObject, "Sayiyi gizle");
            rect.gameObject.SetActive(false);
            EditorUtility.SetDirty(rect.gameObject);
            log.Add($"  {objectName} gizlendi");
        }

        // ------------------------------------------------------------------
        // Kurtarma
        // ------------------------------------------------------------------

        private static void RemoveBadges()
        {
            int removed = 0;

            foreach (string name in new[]
                     {
                         "AliveCountBadge", "KillCountBadge",
                         "HealthBarIcon", "ShieldBarIcon"
                     })
            {
                if (!TryFind(name, out RectTransform rect))
                    continue;

                Undo.DestroyObjectImmediate(rect.gameObject);
                removed++;
            }

            foreach (string name in new[] { "HealthValue", "ShieldValue" })
            {
                if (TryFind(name, out RectTransform rect) && !rect.gameObject.activeSelf)
                {
                    Undo.RecordObject(rect.gameObject, "Sayiyi geri ac");
                    rect.gameObject.SetActive(true);
                }
            }

            MarkSceneDirty();
            Debug.Log($"{removed} rozet silindi, sayilar geri acildi. " +
                      "KALAN / SKOR kelimelerini istiyorsan Game_UI > Battle Status HUD " +
                      "icindeki Alive Prefix / Kills Prefix alanlarina elle yaz.");
        }

        private static void ResetFonts()
        {
            TMP_FontAsset fallback = TMP_Settings.defaultFontAsset
                                     ?? Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            if (fallback == null)
            {
                Debug.LogError(
                    "Varsayilan TMP fontu bulunamadi. Window > TextMeshPro > " +
                    "Import TMP Essential Resources calistirilmis mi?");
                return;
            }

            int changed = 0;

            foreach (TextMeshProUGUI label in Object.FindObjectsByType<TextMeshProUGUI>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                // Emoji rozetlerine dokunma: onlarin fontu bilincli olarak farkli.
                if (label.transform.parent != null && label.transform.parent.name.EndsWith("Badge"))
                    continue;

                if (label.transform.parent != null && label.transform.parent.name.EndsWith("Icon"))
                    continue;

                Undo.RecordObject(label, "Fontu sifirla");
                label.font = fallback;
                EditorUtility.SetDirty(label);
                changed++;
            }

            MarkSceneDirty();
            Debug.Log($"{changed} yazi '{fallback.name}' fontuna donduruldu " +
                      "(emoji rozetleri haric).");
        }

        private static void Report()
        {
            List<string> log = new() { "=== HUD ROZET DURUMU ===" };

            TMP_FontAsset emojiFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(EmojiFontPath);
            log.Add($"  Emoji fontu: {(emojiFont != null ? emojiFont.name : "YOK")}");

            foreach (string name in new[]
                     {
                         "AliveCountBadge", "KillCountBadge",
                         "HealthBarIcon", "ShieldBarIcon",
                         "HealthValue", "ShieldValue",
                         "HealthBar", "ShieldBar"
                     })
            {
                if (TryFind(name, out RectTransform rect))
                {
                    log.Add($"  {name,-17} var   acik: {rect.gameObject.activeSelf,-5} " +
                            $"konum {rect.anchoredPosition}  boyut {rect.sizeDelta}");
                }
                else
                {
                    log.Add($"  {name,-17} yok");
                }
            }

            Debug.Log(string.Join("\n", log));
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

        private static void MarkSceneDirty()
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
    }
}
