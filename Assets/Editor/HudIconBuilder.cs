using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// HUD rozetlerini DUZENLENEBILIR sahne nesneleri olarak kurar.
    ///
    /// Neden bu arac var: HUD'un bir kismi calisma aninda koddan
    /// uretiliyordu, dolayisiyla Inspector'da gorunmuyor ve elle
    /// ayarlanamiyordu. Bu arac her parcayi gercek bir GameObject olarak
    /// sahneye koyuyor; sonrasinda font, renk, arka plan, konum - hepsi
    /// senin kontrolunde.
    ///
    /// Arac calisma aninda HICBIR SEY yapmiyor. Bir kez basiyorsun,
    /// sonra istedigini degistiriyorsun, arac bir daha karismiyor.
    /// Isin bitince dosyayi silebilirsin.
    /// </summary>
    public class HudIconBuilder : EditorWindow
    {
        private const string IconFolder = "Assets/GameArt/HudIcons";

        /// <summary>
        /// Ikonlar neden emoji degil de gorsel:
        ///
        /// TextMeshPro, emoji karakterlerini (kisi, kurukafa, kalkan) ancak emoji destekli
        /// bir font varligi varsa cizebiliyor. Normal bir metin fontunda o
        /// karakterler yok; TMP eksik glif yerine baska bir glif koyuyor ve
        /// ekranda anlamsiz isaretler cikiyor. Gorsel kullanmak bu sorunu
        /// tamamen ortadan kaldiriyor ve istedigin PNG ile degistirmene
        /// izin veriyor.
        /// </summary>
        private enum IconKind
        {
            Person,
            Skull,
            Cross,
            Shield
        }

        private Vector2 _scroll;

        [MenuItem("Tools/NewBattle/HUD Rozetleri", false, 21)]
        public static void Open()
        {
            GetWindow<HudIconBuilder>("HUD Rozetleri").minSize = new Vector2(440f, 420f);
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("1 - Ikonlari uret", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Dort beyaz ikon uretir: kisi, kurukafa, arti, kalkan.\n\n" +
                $"{IconFolder} klasorune PNG olarak kaydedilir. Begenmezsen " +
                "ayni isimli dosyalarin uzerine kendi gorsellerini yaz ya da " +
                "rozetlerdeki Sprite alanina baska bir gorsel surukle.",
                MessageType.None);

            if (GUILayout.Button("Ikonlari Uret", GUILayout.Height(30f)))
                BuildIcons();

            EditorGUILayout.Space(18f);
            EditorGUILayout.LabelField("2 - Rozetleri kur", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Sol ustteki KALAN / SKOR yazilarini kaldirir, yerlerine " +
                "saydam siyah kare zeminli ikon rozetleri koyar.\n\n" +
                "Alttaki can ve kalkan barlarindaki sayilari gizler, barlarin " +
                "soluna ikon koyar.\n\n" +
                "Hepsi normal sahne nesnesi: arka plan rengini, boyutu, " +
                "konumu, fontu Inspector'dan degistirebilirsin.",
                MessageType.None);

            if (GUILayout.Button("Rozetleri Kur", GUILayout.Height(34f)))
                BuildBadges();

            EditorGUILayout.Space(18f);
            EditorGUILayout.LabelField("3 - Font kurtarma", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Ekranda anlamsiz karakterler (Cince gibi) goruyorsan, script " +
                "ile uretilen font varliginin glif eslemesi bozulmus demektir.\n\n" +
                "Bu dugme butun yazilari TextMeshPro'nun kendi varsayilan " +
                "fontuna dondurur. Sonra istedigin fontu Unity'nin kendi " +
                "araciyla uret: Window > TextMeshPro > Font Asset Creator.",
                MessageType.Warning);

            if (GUILayout.Button("Yazilari Varsayilan Fonta Dondur"))
                ResetFonts();

            EditorGUILayout.Space(14f);

            if (GUILayout.Button("Durumu Yaz"))
                Report();

            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------------
        // Ikon uretimi
        // ------------------------------------------------------------------

        private static void BuildIcons()
        {
            Directory.CreateDirectory(IconFolder);

            foreach (IconKind kind in System.Enum.GetValues(typeof(IconKind)))
            {
                string path = $"{IconFolder}/{kind}.png";
                File.WriteAllBytes(path, Draw(kind).EncodeToPNG());
            }

            AssetDatabase.Refresh();

            // Sprite olarak iceri alinmali, yoksa Image bileseni kullanamaz.
            foreach (IconKind kind in System.Enum.GetValues(typeof(IconKind)))
            {
                string path = $"{IconFolder}/{kind}.png";

                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                    continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            Debug.Log($"4 ikon uretildi: {IconFolder}");
        }

        /// <summary>
        /// Ikonu piksel piksel cizer.
        ///
        /// Kenar yumusatma icin her piksel 2x2 orneklenip ortalamasi
        /// aliniyor; tek ornekle cizilen egriler kucuk boyutta merdiven
        /// basamagi gibi gorunuyor.
        /// </summary>
        private static Texture2D Draw(IconKind kind)
        {
            const int size = 128;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float coverage = 0f;

                    for (int sy = 0; sy < 2; sy++)
                    {
                        for (int sx = 0; sx < 2; sx++)
                        {
                            float u = (x + (sx + 0.5f) * 0.5f) / size;
                            float v = (y + (sy + 0.5f) * 0.5f) / size;

                            if (Inside(kind, u, 1f - v))
                                coverage += 0.25f;
                        }
                    }

                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(coverage * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>Verilen nokta ikonun icinde mi. u,v 0..1, sol ust kose 0,0.</summary>
        private static bool Inside(IconKind kind, float u, float v)
        {
            switch (kind)
            {
                case IconKind.Person:
                {
                    // Bas: daire. Govde: asagi dogru genisleyen yarim elips.
                    if (Circle(u, v, 0.5f, 0.28f, 0.17f))
                        return true;

                    return v > 0.5f && v < 0.9f &&
                           Ellipse(u, v, 0.5f, 0.95f, 0.30f, 0.45f);
                }

                case IconKind.Skull:
                {
                    // Kafatasi kubbesi + cene, uzerine iki carpi goz.
                    bool body = Ellipse(u, v, 0.5f, 0.44f, 0.33f, 0.34f) ||
                                (v > 0.6f && v < 0.84f && Mathf.Abs(u - 0.5f) < 0.17f);

                    if (!body)
                        return false;

                    // Gozler: iki carpi, govdeden oyuluyor.
                    if (Cross(u, v, 0.37f, 0.44f, 0.10f, 0.035f))
                        return false;

                    if (Cross(u, v, 0.63f, 0.44f, 0.10f, 0.035f))
                        return false;

                    return true;
                }

                case IconKind.Cross:
                {
                    const float arm = 0.34f;
                    const float thick = 0.13f;

                    return (Mathf.Abs(u - 0.5f) < thick && Mathf.Abs(v - 0.5f) < arm) ||
                           (Mathf.Abs(v - 0.5f) < thick && Mathf.Abs(u - 0.5f) < arm);
                }

                case IconKind.Shield:
                {
                    // Ust kenar duz, yanlar asagi dogru daralip ucta birlesiyor.
                    if (v < 0.18f || v > 0.9f)
                        return false;

                    float t = Mathf.InverseLerp(0.18f, 0.9f, v);
                    float halfWidth = Mathf.Lerp(0.32f, 0.02f, t * t);

                    return Mathf.Abs(u - 0.5f) < halfWidth;
                }
            }

            return false;
        }

        private static bool Circle(float u, float v, float cx, float cy, float r)
        {
            float dx = u - cx;
            float dy = v - cy;
            return dx * dx + dy * dy < r * r;
        }

        private static bool Ellipse(float u, float v, float cx, float cy, float rx, float ry)
        {
            float dx = (u - cx) / rx;
            float dy = (v - cy) / ry;
            return dx * dx + dy * dy < 1f;
        }

        private static bool Cross(float u, float v, float cx, float cy, float arm, float thick)
        {
            float dx = u - cx;
            float dy = v - cy;

            float a = (dx + dy) * 0.7071f;
            float b = (dx - dy) * 0.7071f;

            return (Mathf.Abs(a) < thick && Mathf.Abs(b) < arm) ||
                   (Mathf.Abs(b) < thick && Mathf.Abs(a) < arm);
        }

        // ------------------------------------------------------------------
        // Rozetler
        // ------------------------------------------------------------------

        private static void BuildBadges()
        {
            List<string> log = new() { "=== HUD ROZETLERI ===" };

            ClearStatusPrefixes(log);

            // Sol ust: kalan oyuncu ve skor
            AttachBadge("AliveCount", IconKind.Person, log);
            AttachBadge("KillCount", IconKind.Skull, log);

            // Alt orta: can ve kalkan
            HideValueText("HealthValue", log);
            HideValueText("ShieldValue", log);

            AttachBarIcon("HealthBar", IconKind.Cross, log);
            AttachBarIcon("ShieldBar", IconKind.Shield, log);

            MarkSceneDirty();
            Debug.Log(string.Join("\n", log));
        }

        /// <summary>
        /// "KALAN" ve "SKOR" kelimelerini kaldirir; geriye sadece sayi kalir.
        /// Kelimeleri kodda degil ayarda tuttugumuz icin tek satirlik is.
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
            log.Add("  KALAN / SKOR yazilari kaldirildi");
        }

        /// <summary>
        /// Yazinin soluna saydam siyah kare zeminli bir ikon rozeti koyar.
        ///
        /// Rozet, yazinin KARDESI olarak ekleniyor (cocugu degil): boylece
        /// yaziyi tasidiginda ya da sildiginde rozet etkilenmiyor, ikisini
        /// ayri ayri konumlandirabiliyorsun.
        /// </summary>
        private static void AttachBadge(string textObjectName, IconKind kind, List<string> log)
        {
            if (!TryFind(textObjectName, out RectTransform label))
            {
                log.Add($"  {textObjectName} bulunamadi.");
                return;
            }

            string badgeName = textObjectName + "Badge";

            if (TryFind(badgeName, out RectTransform _))
            {
                log.Add($"  {badgeName} zaten var, dokunulmadi.");
                return;
            }

            GameObject badge = new(badgeName, typeof(RectTransform), typeof(Image));
            RectTransform rect = badge.GetComponent<RectTransform>();
            rect.SetParent(label.parent, false);
            rect.anchorMin = label.anchorMin;
            rect.anchorMax = label.anchorMax;
            rect.pivot = label.pivot;
            rect.sizeDelta = new Vector2(46f, 46f);
            rect.anchoredPosition = label.anchoredPosition + new Vector2(2f, -2f);

            Image background = badge.GetComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.45f);
            background.raycastTarget = false;

            GameObject icon = new("Icon", typeof(RectTransform), typeof(Image));
            RectTransform iconRect = icon.GetComponent<RectTransform>();
            iconRect.SetParent(rect, false);
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(30f, 30f);

            Image iconImage = icon.GetComponent<Image>();
            iconImage.sprite = LoadIcon(kind);
            iconImage.color = Color.white;
            iconImage.raycastTarget = false;
            iconImage.preserveAspect = true;

            // Sayi rozetin sagina kayiyor, uzerine binmesin.
            label.anchoredPosition += new Vector2(54f, 0f);

            Undo.RegisterCreatedObjectUndo(badge, "HUD rozeti");
            log.Add($"  {badgeName} olusturuldu ({kind})");
        }

        /// <summary>Barin soluna ikon koyar. Arka plani yok: bar zaten kendi zeminini tasiyor.</summary>
        private static void AttachBarIcon(string barName, IconKind kind, List<string> log)
        {
            if (!TryFind(barName, out RectTransform bar))
            {
                log.Add($"  {barName} bulunamadi.");
                return;
            }

            string iconName = barName + "Icon";

            if (TryFind(iconName, out RectTransform _))
            {
                log.Add($"  {iconName} zaten var, dokunulmadi.");
                return;
            }

            GameObject icon = new(iconName, typeof(RectTransform), typeof(Image));
            RectTransform rect = icon.GetComponent<RectTransform>();
            rect.SetParent(bar.parent, false);
            rect.anchorMin = bar.anchorMin;
            rect.anchorMax = bar.anchorMax;
            rect.pivot = bar.pivot;

            float height = Mathf.Max(22f, bar.sizeDelta.y * 1.6f);
            rect.sizeDelta = new Vector2(height, height);
            rect.anchoredPosition = bar.anchoredPosition - new Vector2(bar.sizeDelta.x * 0.5f + 8f, 0f);

            Image image = icon.GetComponent<Image>();
            image.sprite = LoadIcon(kind);
            image.color = Color.white;
            image.raycastTarget = false;
            image.preserveAspect = true;

            Undo.RegisterCreatedObjectUndo(icon, "Bar ikonu");
            log.Add($"  {iconName} olusturuldu ({kind})");
        }

        /// <summary>
        /// Bardaki sayiyi gizler.
        ///
        /// Siliyor degil kapatiyoruz: LocalPlayerHUD bu yaziya referans
        /// tutuyor, silinirse her karede bos referans kontrolu yapmasi
        /// gerekirdi. Kapali nesneye yazmak zararsiz.
        /// </summary>
        private static void HideValueText(string objectName, List<string> log)
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

        private static Sprite LoadIcon(IconKind kind)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{IconFolder}/{kind}.png");
        }

        // ------------------------------------------------------------------
        // Font kurtarma
        // ------------------------------------------------------------------

        private static void ResetFonts()
        {
            TMP_FontAsset fallback = TMP_Settings.defaultFontAsset;

            if (fallback == null)
                fallback = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

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
                Undo.RecordObject(label, "Fontu sifirla");
                label.font = fallback;
                EditorUtility.SetDirty(label);
                changed++;
            }

            MarkSceneDirty();
            Debug.Log($"{changed} yazi '{fallback.name}' fontuna donduruldu.");
        }

        private static void Report()
        {
            List<string> log = new() { "=== HUD ROZET DURUMU ===" };

            foreach (IconKind kind in System.Enum.GetValues(typeof(IconKind)))
                log.Add($"  ikon {kind,-8}: {(LoadIcon(kind) != null ? "var" : "YOK")}");

            foreach (string name in new[]
                     {
                         "AliveCountBadge", "KillCountBadge",
                         "HealthBarIcon", "ShieldBarIcon",
                         "HealthValue", "ShieldValue"
                     })
            {
                if (TryFind(name, out RectTransform rect))
                    log.Add($"  {name,-18} var, acik: {rect.gameObject.activeSelf}");
                else
                    log.Add($"  {name,-18} yok");
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
