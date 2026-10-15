#if UNITY_EDITOR
using NewBattle.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// Malatya temasi: yukleme ekrani ve inis yeri secimi.
    ///   - Yukleme: ahsap kahvesi, donen kayisi, sirayla dolan kayisi dizisi, ipuclari.
    ///   - Inis yeri: eski kagit harita (solda), sagda kagit panel + geri sayim, kayisi ignesi.
    /// Unity derleyince eksikse bir kez kendiliginden uygulanir; sonra Hierarchy'den
    /// istedigin gibi degistirebilirsin (tekrar ezilmez).
    /// Geri donmek: git tag "safe-oncesi-yukleme-harita-temasi".
    /// </summary>
    public static partial class MainMenuBuilder
    {
        private const string PaperMapPath = "Assets/GameArt/DropMap/harita_kagit.png";
        private const string ApricotIconSpritePath = "Assets/GameArt/HudIcons/kayisi.png";

        private static readonly Color Wood = Hex("#7A4A26");
        private static readonly Color WoodDark = Hex("#4E2D14");
        private static readonly Color Cream = Hex("#FFE9C7");
        private static readonly Color ApricotOrange = Hex("#F2994A");
        private static readonly Color ApricotRust = Hex("#B5452B");
        private static readonly Color Parchment = Hex("#F3E2BE");
        private static readonly Color InkBrown = Hex("#4E2D14");

        [InitializeOnLoadMethod]
        private static void AutoApplyMalatyaScreensLater()
        {
            EditorApplication.delayCall += AutoApplyMalatyaScreens;
        }

        private static void AutoApplyMalatyaScreens()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AutoApplyMalatyaScreens;
                return;
            }

            var loading = AssetDatabase.LoadAssetAtPath<GameObject>(LoadingPrefabPath);
            bool loadingNeeded = loading != null && loading.GetComponent<LoadingThemeAnimator>() == null;

            // Taslaktaki gibi duz kayisi + daire dizisi (bir kez).
            if (!loadingNeeded && loading != null &&
                loading.transform.Find("DonenKayisi")?.GetComponent<Image>() is Image spinnerImage &&
                (spinnerImage.sprite == null || spinnerImage.sprite.name != "kayisi_duz"))
                UseFlatLoadingArt();
            DropSelectionScreen drop = Object.FindFirstObjectByType<DropSelectionScreen>(FindObjectsInactive.Include);
            bool dropNeeded = drop != null && drop.transform.Find("SagPanel") == null && drop.transform.Find("BilgiPaneli") == null;

            // Ilk surumde harita soldaydi; harita saga, panel sola (bir kez).
            if (drop != null && drop.transform.Find("SagPanel") is Transform oldPanel)
            {
                Undo.RecordObject(oldPanel.gameObject, "Panel adi");
                oldPanel.name = "BilgiPaneli";
                LayoutMalatyaDropScreen((RectTransform)drop.transform);
                EditorSceneManager.MarkSceneDirty(drop.gameObject.scene);
                EditorSceneManager.SaveScene(drop.gameObject.scene);
                Debug.Log("Inis yeri secimi: harita saga, bilgi paneli sola alindi.");
            }

            if (!loadingNeeded && !dropNeeded)
                return;

            if (!PrepareFont())
                return;
            PrepareMaterials();

            if (loadingNeeded)
                ApplyMalatyaLoading();
            if (dropNeeded)
                ApplyMalatyaDropScreen(drop);
        }

        [MenuItem("Tools/NewBattle/Menü/Malatya Yükleme + Harita (yeniden uygula)", false, 5)]
        private static void ApplyMalatyaScreensMenu()
        {
            if (!PrepareFont())
                return;
            PrepareMaterials();
            ApplyMalatyaLoading();
            DropSelectionScreen drop = Object.FindFirstObjectByType<DropSelectionScreen>(FindObjectsInactive.Include);
            if (drop != null)
                ApplyMalatyaDropScreen(drop);
        }

        // ------------------------------------------------------------------
        // Yukleme ekrani
        // ------------------------------------------------------------------

        private static void ApplyMalatyaLoading()
        {
            SetTexture(ApricotIconSpritePath, TextureImporterType.Sprite, 256);
            Sprite apricot = Sprite("kayisi_duz") ?? AssetDatabase.LoadAssetAtPath<Sprite>(ApricotIconSpritePath);
            Sprite dotSprite = Sprite("ui_circle") ?? apricot;

            GameObject root = PrefabUtility.LoadPrefabContents(LoadingPrefabPath);
            try
            {
                var rect = (RectTransform)root.transform;
                if (root.GetComponent<LoadingThemeAnimator>() != null)
                    Object.DestroyImmediate(root.GetComponent<LoadingThemeAnimator>());
                foreach (string old in new[] { "DonenKayisi", "YukleniyorYazisi", "KayisiDizisi", "Ipucu" })
                {
                    Transform existing = rect.Find(old);
                    if (existing != null)
                        Object.DestroyImmediate(existing.gameObject);
                }

                if (rect.Find("Arkaplan")?.GetComponent<Image>() is Image background)
                {
                    background.sprite = null;
                    background.color = Wood;
                }

                foreach (string hide in new[] { "KosanKarakter", "Gosterge" })
                    rect.Find(hide)?.gameObject.SetActive(false);

                Vector2 center = new(0.5f, 0.5f);

                RectTransform spinner = NewRect("DonenKayisi", rect);
                Place(spinner, center, center, new Vector2(0f, 110f), new Vector2(190f, 190f));
                Image spinnerImage = AddImage(spinner, apricot, Color.white);
                spinnerImage.type = Image.Type.Simple;
                spinnerImage.preserveAspect = true;
                UISpin spin = spinner.gameObject.AddComponent<UISpin>();
                var spinSerialized = new SerializedObject(spin);
                spinSerialized.FindProperty("degreesPerSecond").floatValue = -140f;
                spinSerialized.ApplyModifiedPropertiesWithoutUndo();

                TextMeshProUGUI title = AddText(rect, "YukleniyorYazisi", "Kayısılar toplanıyor…", 58f, Cream,
                    TextAlignmentOptions.Center, shadow: true);
                Place(title.rectTransform, center, center, new Vector2(0f, -40f), new Vector2(1200f, 80f));

                const int count = 7;
                RectTransform row = NewRect("KayisiDizisi", rect);
                Place(row, center, center, new Vector2(0f, -135f), new Vector2(count * 66f, 60f));
                var dots = new Image[count];
                for (int i = 0; i < count; i++)
                {
                    RectTransform dot = NewRect($"Kayisi{i + 1}", row);
                    Place(dot, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(33f + i * 66f, 0f), new Vector2(54f, 54f));
                    dots[i] = AddImage(dot, dotSprite, Color.white);
                    dots[i].type = Image.Type.Simple;
                    dots[i].preserveAspect = true;
                }

                RectTransform tipPanel = NewRect("Ipucu", rect);
                Place(tipPanel, center, center, new Vector2(0f, -235f), new Vector2(1000f, 74f));
                AddImage(tipPanel, Sprite("ui_round"), new Color(1f, 0.95f, 0.85f, 0.12f)).pixelsPerUnitMultiplier = 2f;
                TextMeshProUGUI tip = AddText(tipPanel, "Yazi", "İpucu: Kayısı atınca takım arkadaşların iyileşir", 34f,
                    Cream, TextAlignmentOptions.Center);
                Stretch(tip.rectTransform, 20f, 20f, 0f, 2f);

                if (rect.Find("Durum")?.GetComponent<TextMeshProUGUI>() is TextMeshProUGUI status)
                    status.color = new Color(Cream.r, Cream.g, Cream.b, 0.7f);

                LoadingThemeAnimator animator = root.AddComponent<LoadingThemeAnimator>();
                var serialized = new SerializedObject(animator);
                SerializedProperty apricots = serialized.FindProperty("apricots");
                apricots.arraySize = count;
                for (int i = 0; i < count; i++)
                    apricots.GetArrayElementAtIndex(i).objectReferenceValue = dots[i];
                serialized.FindProperty("tipText").objectReferenceValue = tip;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, LoadingPrefabPath);
                Debug.Log("Yukleme ekrani Malatya temasina gecti (YuklemeEkrani prefabi).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Yukleme: duz kayisi (taslaktaki) ve duz daireler.</summary>
        private static void UseFlatLoadingArt()
        {
            AssetDatabase.ImportAsset(ArtFolder + "/kayisi_duz.png");
            Sprite flat = Sprite("kayisi_duz");
            Sprite dot = Sprite("ui_circle");
            if (flat == null)
                return;

            GameObject root = PrefabUtility.LoadPrefabContents(LoadingPrefabPath);
            try
            {
                if (root.transform.Find("DonenKayisi") is Transform spinner)
                {
                    spinner.GetComponent<Image>().sprite = flat;
                    if (spinner.TryGetComponent(out UISpin oldSpin))
                        Object.DestroyImmediate(oldSpin);
                }

                if (root.transform.Find("KayisiDizisi") is Transform row && dot != null)
                {
                    foreach (Transform item in row)
                    {
                        if (item.TryGetComponent(out Image image))
                        {
                            image.sprite = dot;
                            image.preserveAspect = true;
                        }
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, LoadingPrefabPath);
                Debug.Log("Yukleme ekrani: duz kayisi ve daire dizisi (taslaktaki gibi).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ------------------------------------------------------------------
        // Inis yeri secimi
        // ------------------------------------------------------------------

        private static void ApplyMalatyaDropScreen(DropSelectionScreen screen)
        {
            var root = (RectTransform)screen.transform;
            Undo.SetCurrentGroupName("Malatya harita ekrani");

            // Eski kagit harita resmi.
            if (AssetImporter.GetAtPath(PaperMapPath) is TextureImporter mapImporter &&
                (mapImporter.mipmapEnabled || mapImporter.wrapMode != TextureWrapMode.Clamp))
            {
                mapImporter.mipmapEnabled = false;
                mapImporter.wrapMode = TextureWrapMode.Clamp;
                mapImporter.SaveAndReimport();
            }

            var paper = AssetDatabase.LoadAssetAtPath<Texture2D>(PaperMapPath);
            var data = AssetDatabase.LoadAssetAtPath<DropMapData>(DropMapDataPath);
            if (data != null && paper != null)
            {
                var dataSerialized = new SerializedObject(data);
                dataSerialized.FindProperty("mapTexture").objectReferenceValue = paper;
                dataSerialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
            }

            if (root.Find("Harita/Resim")?.GetComponent<RawImage>() is RawImage map && paper != null)
            {
                Undo.RecordObject(map, "Harita");
                map.texture = paper;
            }

            // Arka plan: ahsap kahvesi, bulutlar yok.
            if (root.Find("Arkaplan")?.GetComponent<Image>() is Image background)
            {
                Undo.RecordObject(background, "Arka plan");
                background.sprite = null;
                background.color = Wood;
            }

            foreach (string hide in new[] { "Arkaplan/Bulut1", "Arkaplan/Bulut2", "Harita/Resim/Bulut", "Harita/Resim/Bulut2" })
                Hide(root.Find(hide));

            // Bilgi paneli (kagit).
            RectTransform panel = NewRect("BilgiPaneli", root);
            Undo.RegisterCreatedObjectUndo(panel.gameObject, "Bilgi paneli");
            AddImage(panel, Sprite("ui_round"), Parchment).pixelsPerUnitMultiplier = 1.4f;
            panel.SetSiblingIndex(root.Find("Arkaplan") != null ? root.Find("Arkaplan").GetSiblingIndex() + 1 : 0);
            Paint(root.Find("Harita/Cerceve"), WoodDark);

            LayoutMalatyaDropScreen(root);

            if (root.Find("Pusula") is RectTransform compass)
            {
                Paint(compass, InkBrown);
                foreach (TextMeshProUGUI letter in compass.GetComponentsInChildren<TextMeshProUGUI>(true))
                    Tint(letter.transform, InkBrown);
            }

            // Yazilar kagit uzerinde koyu murekkep, geri sayim kayisi kirmizisi.
            SetText(root.Find("Baslik"), "İNİŞ NOKTANI SEÇ", InkBrown, 64f);
            SetText(root.Find("AltBaslik"), "Haritaya dokun, kayısı iğnesini oraya bırak", Wood, 36f);
            SetText(root.Find("ModYazisi"), null, InkBrown, 34f);
            if (root.Find("ModYazisi")?.GetComponent<TextMeshProUGUI>() is TextMeshProUGUI modeText)
                modeText.alignment = TextAlignmentOptions.Left;
            Paint(root.Find("ModIkonu"), InkBrown);
            Tint(root.Find("Bekleme"), Wood);
            Tint(root.Find("GeriSayim/Sayi"), ApricotRust);
            Paint(root.Find("GeriSayim"), new Color(ApricotOrange.r, ApricotOrange.g, ApricotOrange.b, 0.3f));
            Paint(root.Find("CiftXpDurumu"), new Color(Wood.r, Wood.g, Wood.b, 0.22f));
            Tint(root.Find("CiftXpDurumu/Yazi"), Wood);
            Paint(root.Find("CiftXpDurumu/Ikon"), Wood);

            // Kayisi ignesi, kizil halka.
            if (root.Find("Harita/Resim/Isaretci/Pin")?.GetComponent<Image>() is Image pin)
            {
                Sprite apricotPin = Sprite("pin_kayisi");
                if (apricotPin != null)
                {
                    Undo.RecordObject(pin, "Igne");
                    pin.sprite = apricotPin;
                    pin.preserveAspect = true;
                }
            }

            Paint(root.Find("Harita/Resim/Isaretci/Halka"), ApricotRust);

            // Oyuncu yuvalari ve cift XP renkleri (calisirken kod boyar).
            var serialized = new SerializedObject(screen);
            serialized.FindProperty("slotFilled").colorValue = new Color(Wood.r, Wood.g, Wood.b, 0.95f);
            serialized.FindProperty("slotEmpty").colorValue = new Color(Wood.r, Wood.g, Wood.b, 0.25f);
            serialized.FindProperty("slotIconFilled").colorValue = Cream;
            serialized.FindProperty("slotIconEmpty").colorValue = new Color(Wood.r, Wood.g, Wood.b, 0.55f);
            serialized.ApplyModifiedProperties();

            if (root.Find("Oyuncular") is Transform slots)
            {
                foreach (Transform slot in slots)
                {
                    Paint(slot, new Color(Wood.r, Wood.g, Wood.b, 0.25f));
                    Paint(slot.Find("Ikon"), new Color(Wood.r, Wood.g, Wood.b, 0.55f));
                }
            }

            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            EditorSceneManager.SaveScene(root.gameObject.scene);
            Debug.Log("Inis yeri secim ekrani Malatya temasina gecti (kagit harita, sag panel, kayisi ignesi).");
        }

        private static void Paint(Transform target, Color color)
        {
            if (target == null || !target.TryGetComponent(out Image image))
                return;

            Undo.RecordObject(image, "Renk");
            image.color = color;
            EditorUtility.SetDirty(image);
        }

        private static void Tint(Transform target, Color color)
        {
            if (target == null || !target.TryGetComponent(out TextMeshProUGUI text))
                return;

            Undo.RecordObject(text, "Yazi rengi");
            text.color = color;
            EditorUtility.SetDirty(text);
        }

        /// <summary>Harita sagda, bilgi paneli (baslik, mod, oyuncular, geri sayim) solda.</summary>
        private static void LayoutMalatyaDropScreen(RectTransform root)
        {
            if (root.Find("Harita") is RectTransform holder)
            {
                Undo.RecordObject(holder, "Harita yeri");
                holder.anchorMin = new Vector2(1f, 0.07f);
                holder.anchorMax = new Vector2(1f, 0.93f);
                holder.pivot = new Vector2(1f, 0.5f);
                holder.anchoredPosition = new Vector2(-80f, 0f);
            }

            if (root.Find("BilgiPaneli") is RectTransform panel)
            {
                Undo.RecordObject(panel, "Panel yeri");
                panel.anchorMin = new Vector2(0f, 0.07f);
                panel.anchorMax = new Vector2(0f, 0.93f);
                panel.pivot = new Vector2(0f, 0.5f);
                panel.anchoredPosition = new Vector2(40f, 0f);
                panel.sizeDelta = new Vector2(800f, 0f);
            }

            Vector2 topLeft = new(0f, 1f), center = new(0.5f, 0.5f);
            Move(root.Find("Baslik"), topLeft, topLeft, new Vector2(70f, -100f), new Vector2(760f, 84f));
            Move(root.Find("AltBaslik"), topLeft, topLeft, new Vector2(72f, -186f), new Vector2(740f, 54f));
            Move(root.Find("ModIkonu"), topLeft, center, new Vector2(110f, -282f), new Vector2(60f, 60f));
            Move(root.Find("ModYazisi"), topLeft, new Vector2(0f, 0.5f), new Vector2(152f, -282f), new Vector2(400f, 50f));
            Move(root.Find("CiftXpDurumu"), topLeft, topLeft, new Vector2(70f, -332f), new Vector2(470f, 74f));
            Move(root.Find("Oyuncular"), topLeft, topLeft, new Vector2(75f, -440f), null);
            Move(root.Find("Bekleme"), topLeft, new Vector2(0.5f, 1f), new Vector2(440f, -560f), new Vector2(720f, 120f));
            Move(root.Find("GeriSayim"), topLeft, center, new Vector2(440f, -800f), new Vector2(440f, 300f));

            // Pusula haritanin sag ust kosesinde.
            if (root.Find("Pusula") is RectTransform compass)
            {
                Undo.RecordObject(compass, "Pusula");
                compass.anchorMin = compass.anchorMax = Vector2.one;
                compass.anchoredPosition = new Vector2(-150f, -150f);
            }
        }

        private static void Move(Transform target, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2? size)
        {
            if (target is not RectTransform rect)
                return;

            Undo.RecordObject(rect, "Yerlestir");
            Vector2 keep = rect.rect.size;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = size ?? keep;
            rect.anchoredPosition = position;
        }

        private static void Hide(Transform target)
        {
            if (target == null || !target.gameObject.activeSelf)
                return;

            Undo.RecordObject(target.gameObject, "Gizle");
            target.gameObject.SetActive(false);
        }

        private static void SetText(Transform target, string value, Color color, float size)
        {
            if (target == null || !target.TryGetComponent(out TextMeshProUGUI text))
                return;

            Undo.RecordObject(text, "Yazi");
            if (value != null)
                text.text = value;
            text.color = color;
            text.fontSize = size;
            EditorUtility.SetDirty(text);
        }
    }
}
#endif
