#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using NewBattle.UI;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// Sonbahar ana menu = kullanicinin tasarim resmi birebir (tasarim_sonbahar.png).
    ///   - Resim ana menunun gorunumudur, ekrani tam kaplar (eski menu hic gorunmez).
    ///   - Butonlar resimdeki buton resimlerinin tam ustunde gorunmez basma alanlari.
    ///   - Degisen yazilar (para, seviye, XP, isim, mod, cift XP) resimden silindi;
    ///     yerine oyunun gercek degerleri ayni yere yazilir. Logonun altindaki gri cubuk ve
    ///     online sayaci yok.
    ///   - Arka plan ayri: "ArkaPlan" nesnesi (kullanicinin orman resmi, Inspector'dan Source Image
    ///     degistirilir). Her buton / kart / logo kendi nesnesinde kendi resmiyle (AnaMenu/*.png,
    ///     Tools/MenuArt/anamenu_katman.py); yazilari icinde, Hierarchy'de birlikte tasinir.
    /// Koordinatlar tasarim resminin piksel olcusunde (2000x1120, tam resim).
    /// Unity derleyince bir kez kendiliginden uygulanir (resim olcusu degisince yeniden yerlesir). Geri: tag "safe-oncesi-sonbahar-menu".
    /// </summary>
    public static partial class MainMenuBuilder
    {
        private const string DesignSprite = "tasarim_sonbahar";
        private const string DesignBackground = "bg_sonbahar";
        private const string PieceFolder = "AnaMenu";
        private const string MenuBackgroundSprite = "anamenu_arkaplan";
        private const string MenuBackgroundName = "ArkaPlan";
        private const float DesignLeft = 0f, DesignWidth = 2000f, DesignHeight = 1120f;

        // Parca resmi -> nesnesi (null: yeni nesne) ve resimdeki dikdortgeni (anamenu_katman.py yazar).
        private static readonly (string sprite, string target, Vector4 rect)[] MenuPieces =
        {
            ("logo", null, new Vector4(0, 10, 543, 142)),
            ("para", "ParaPaneli", new Vector4(1399, 26, 1762, 147)),
            ("ayarlar", "AyarlarButonu", new Vector4(1769, 20, 1887, 151)),
            ("sezon", "SezonKarti", new Vector4(43, 194, 378, 448)),
            ("kostum", "KostumKarti", new Vector4(52, 474, 202, 633)),
            ("odul_tac", "BattlePassKarti", new Vector4(221, 463, 385, 607)),
            ("odul_cip", "Oduller", new Vector4(228, 602, 363, 645)),
            ("mucadeleler", "MucadelelerKarti", new Vector4(47, 638, 443, 872)),
            ("etkinlik_karti", "EtkinlikKarti", new Vector4(1530, 295, 1860, 503)),
            ("cift_xp", "CiftXpKarti", new Vector4(1491, 536, 1879, 764)),
            ("battle_pass", "BattlePassButonu", new Vector4(45, 919, 184, 1056)),
            ("magaza", "MagazaButonu", new Vector4(205, 916, 342, 1072)),
            ("liderlik", "LiderlikButonu", new Vector4(349, 931, 482, 1071)),
            ("isim", null, new Vector4(713, 819, 1216, 922)),
            ("karakter", "KarakterButonu", new Vector4(811, 920, 955, 1078)),
            ("arkadaslar", "ArkadaslarButonu", new Vector4(968, 920, 1113, 1078)),
            ("etkinlikler", "EtkinliklerButonu", new Vector4(1129, 876, 1503, 1087)),
            ("oyna", "OynaButonu", new Vector4(1483, 893, 1900, 1107)),
        };

        private static readonly (string name, Vector4 rect)[] DesignHits =
        {
            ("AyarlarButonu", new Vector4(1772, 38, 1880, 142)),
            ("ParaPaneli", new Vector4(1400, 30, 1762, 140)),
            ("SezonKarti", new Vector4(52, 200, 368, 424)),
            ("KostumKarti", new Vector4(62, 476, 196, 632)),
            ("BattlePassKarti", new Vector4(226, 470, 362, 600)),
            ("Oduller", new Vector4(228, 604, 362, 642)),
            ("MucadelelerKarti", new Vector4(52, 662, 424, 866)),
            ("EtkinlikKarti", new Vector4(1558, 298, 1838, 494)),
            ("CiftXpKarti", new Vector4(1500, 536, 1878, 756)),
            ("BattlePassButonu", new Vector4(36, 926, 188, 1068)),
            ("MagazaButonu", new Vector4(200, 926, 338, 1068)),
            ("LiderlikButonu", new Vector4(346, 926, 486, 1068)),
            ("KarakterButonu", new Vector4(812, 922, 958, 1076)),
            ("ArkadaslarButonu", new Vector4(968, 922, 1112, 1076)),
            ("EtkinliklerButonu", new Vector4(1128, 912, 1484, 1080)),
            ("OynaButonu", new Vector4(1488, 896, 1902, 1080)),
            ("RutbeIkon", new Vector4(742, 844, 794, 898)),
            ("IsimDuzenleButonu", new Vector4(1130, 838, 1192, 900)),
        };

        [InitializeOnLoadMethod]
        private static void AutoApplyAutumnMenuLater()
        {
            EditorApplication.delayCall += AutoApplyAutumnMenu;
        }

        private static void AutoApplyAutumnMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AutoApplyAutumnMenu;
                return;
            }

            Canvas canvas = FindGameCanvas();
            MainMenuScreen screen = canvas != null ? canvas.GetComponentInChildren<MainMenuScreen>(true) : null;
            if (screen == null)
                return;

            if (!AutumnMenuCurrent(screen.transform))
                ApplyAutumnMenu(screen.transform);
            else if (screen.transform.Find("Tasarim") is Transform layer &&
                     layer.TryGetComponent(out Image design) && design.enabled)
                ApplyMenuPieces(screen.transform);
        }

        /// <summary>
        /// Arka plan ayri nesne olur ("ArkaPlan", en altta, tam ekran; Inspector'dan degisir). Tasarim
        /// resmi kapanir; her buton / kart / logo kendi nesnesinde kendi resmini gosterir, degisen yazilar
        /// ait olduklari parcanin icine alinir (Hierarchy'de parcayla birlikte tasinir).
        /// Bir kez yapilir (tasarim resmi acikken); sonra kullanicinin yerlestirmesine dokunulmaz.
        /// </summary>
        private static void ApplyMenuPieces(Transform root)
        {
            SetSharpDesign($"{ArtFolder}/{MenuBackgroundSprite}.png");
            Sprite forest = Sprite(MenuBackgroundSprite);
            if (forest == null || root.Find("Tasarim") is not RectTransform layer)
                return;

            Undo.SetCurrentGroupName("Ana menu parcalari");
            var screenRefs = new SerializedObject(root.GetComponent<MainMenuScreen>());
            Transform Ref(string field) => (screenRefs.FindProperty(field)?.objectReferenceValue as Component)?.transform;

            var pieces = new System.Collections.Generic.Dictionary<string, (RectTransform rect, Vector4 pixels)>();
            foreach ((string sprite, string target, Vector4 rect) in MenuPieces)
            {
                Sprite image = Sprite($"{PieceFolder}/{sprite}");
                if (image == null)
                    continue;

                RectTransform piece = target != null ? FindDeep(root, target) as RectTransform : null;
                if (piece == null)
                {
                    string name = target ?? (sprite == "logo" ? "Logo" : "IsimTahtasi");
                    piece = layer.Find(name) as RectTransform;
                    if (piece == null)
                    {
                        piece = NewRect(name, layer);
                        Undo.RegisterCreatedObjectUndo(piece.gameObject, name);
                        AddImage(piece, image, Color.white);
                    }
                }

                MapTo(piece, layer, rect);
                Image own = piece.GetComponent<Image>();
                if (own == null)
                    own = Undo.AddComponent<Image>(piece.gameObject);
                Undo.RecordObject(own, "Parca resmi");
                own.sprite = image;
                own.type = Image.Type.Simple;
                own.preserveAspect = false;
                own.color = Color.white;
                own.enabled = true;
                own.raycastTarget = target != null;
                piece.SetAsLastSibling();
                pieces[sprite] = (piece, rect);
            }

            // Degisen yazilar parcalarinin icine (yerleri resimde ayni kalir).
            Transform modeChip = Ref("modeChipText");
            Transform doubleXpText = null;
            foreach (Transform child in layer)
                if (child.name == "Yazi" && child != modeChip)
                    doubleXpText = child;
            Transform xpFill = Ref("xpFill");
            (Transform child, string piece)[] contents =
            {
                (Ref("coinText"), "para"),
                (Ref("levelText"), "sezon"), (Ref("rankText"), "sezon"), (Ref("nextLevelText"), "sezon"),
                (Ref("xpText"), "sezon"), (xpFill != null ? xpFill.parent : null, "sezon"),
                (Ref("playerNameText"), "isim"), (FindDeep(root, "RutbeIkon"), "isim"),
                (FindDeep(root, "IsimDuzenleButonu"), "isim"),
                (modeChip, "oyna"),
                (doubleXpText, "cift_xp"),
            };
            foreach ((Transform child, string piece) in contents)
            {
                if (child is not RectTransform rect || rect.parent != layer || !pieces.TryGetValue(piece, out var owner))
                    continue;

                // Katmandaki yeri (resim pikseli) parcaya gore yeniden hesaplanir.
                float x0 = rect.anchorMin.x * DesignWidth, x1 = rect.anchorMax.x * DesignWidth;
                float y0 = (1f - rect.anchorMax.y) * DesignHeight, y1 = (1f - rect.anchorMin.y) * DesignHeight;
                Vector4 box = owner.pixels;
                float width = box.z - box.x, height = box.w - box.y;
                Undo.SetTransformParent(rect, owner.rect, "Parcaya al");
                Undo.RecordObject(rect, "Yerlestir");
                rect.anchorMin = new Vector2((x0 - box.x) / width, 1f - (y1 - box.y) / height);
                rect.anchorMax = new Vector2((x1 - box.x) / width, 1f - (y0 - box.y) / height);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.SetAsLastSibling();
            }

            // Tasarim resmi kapanir (arka plan ayri, parcalar kendi resimlerinde).
            Image layerImage = layer.GetComponent<Image>();
            Undo.RecordObject(layerImage, "Resim");
            layerImage.enabled = false;
            layerImage.raycastTarget = false;

            if (root.Find(MenuBackgroundName) is not RectTransform background)
            {
                background = NewRect(MenuBackgroundName, (RectTransform)root);
                Undo.RegisterCreatedObjectUndo(background.gameObject, MenuBackgroundName);
                AddImage(background, forest, Color.white);
                Stretch(background, 0f, 0f, 0f, 0f);
            }

            if (background.GetComponent<Image>() is Image backgroundImage)
            {
                Undo.RecordObject(backgroundImage, "Arka plan");
                if (backgroundImage.sprite == null)
                    backgroundImage.sprite = forest;
                backgroundImage.type = Image.Type.Simple;
                backgroundImage.preserveAspect = false;
                backgroundImage.raycastTarget = false;
                backgroundImage.enabled = true;
            }

            layer.SetAsFirstSibling();
            background.SetAsFirstSibling();
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            EditorSceneManager.SaveScene(root.gameObject.scene);
            Debug.Log("Ana menu: arka plan \"ArkaPlan\" (Inspector > Image > Source Image); butonlar / kartlar ayri nesneler.");
        }

        /// <summary>Tasarim katmani var ve guncel resim olcusuyle tam ekran mi?</summary>
        private static bool AutumnMenuCurrent(Transform root)
        {
            AspectRatioFitter fitter = root.Find("Tasarim")?.GetComponent<AspectRatioFitter>();
            return fitter != null && fitter.aspectMode == AspectRatioFitter.AspectMode.None &&
                   Mathf.Abs(fitter.aspectRatio - DesignWidth / DesignHeight) < 0.001f;
        }

        [MenuItem("Tools/NewBattle/Menü/Sonbahar Ana Menü (tasarım resmi)", false, 6)]
        private static void ApplyAutumnMenuItem()
        {
            Canvas canvas = FindGameCanvas();
            MainMenuScreen screen = canvas != null ? canvas.GetComponentInChildren<MainMenuScreen>(true) : null;
            if (screen != null)
                ApplyAutumnMenu(screen.transform);
        }

        private static void ApplyAutumnMenu(Transform root)
        {
            SetSharpDesign($"{ArtFolder}/{DesignSprite}.png");
            AssetDatabase.ImportAsset($"{ArtFolder}/{DesignBackground}.png");
            Sprite design = Sprite(DesignSprite);
            Sprite background = Sprite(DesignBackground);
            if (design == null)
                return;

            Undo.SetCurrentGroupName("Sonbahar tasarim menusu");
            var rootRect = (RectTransform)root;

            if (background != null && root.GetComponent<Image>() is Image menuBackground)
            {
                Undo.RecordObject(menuBackground, "Arka plan");
                menuBackground.sprite = background;
                menuBackground.type = Image.Type.Simple;
                menuBackground.color = Color.white;
            }

            // Tasarim resmi: ekrani tam kaplar (oran farki kucuk; basma alanlari resimle birlikte uzar).
            // Varsa ayni katman yeni resme gore yeniden yerlesir.
            RectTransform layer = root.Find("Tasarim") as RectTransform;
            if (layer == null)
            {
                layer = NewRect("Tasarim", rootRect);
                Undo.RegisterCreatedObjectUndo(layer.gameObject, "Tasarim");
                AddImage(layer, design, Color.white).type = Image.Type.Simple;
                layer.gameObject.AddComponent<AspectRatioFitter>();
            }

            Image layerImage = layer.GetComponent<Image>();
            Undo.RecordObject(layerImage, "Resim");
            layerImage.sprite = design;
            layerImage.color = Color.white;
            AspectRatioFitter fitter = layer.GetComponent<AspectRatioFitter>();
            Undo.RecordObject(fitter, "Oran");
            fitter.aspectMode = AspectRatioFitter.AspectMode.None;
            fitter.aspectRatio = DesignWidth / DesignHeight;
            Undo.RecordObject(layer, "Tam ekran");
            Stretch(layer, 0f, 0f, 0f, 0f);
            layer.localScale = Vector3.one;
            layer.SetAsFirstSibling();

            Color cream = Hex("#F6E2C2");
            Color ink = Hex("#4E2D14");

            // Degisen yazilar once disari alinir (gizlenen kartlarin icinde kalmasinlar).
            // Ekranin baglantilarindan bulunur: onceki uygulamada tasarim katmanina tasinmis olabilirler.
            var screenRefs = new SerializedObject(root.GetComponent<MainMenuScreen>());
            Transform Ref(string field) => (screenRefs.FindProperty(field)?.objectReferenceValue as Component)?.transform;
            Transform doubleXp = FindDeep(root, "CiftXpKarti");
            Transform modeChip = Ref("modeChipText");

            Overlay(Ref("coinText"), layer, new Vector4(1572, 58, 1672, 120), cream, TextAlignmentOptions.Center);
            Overlay(Ref("levelText"), layer, new Vector4(184, 276, 342, 318), cream, TextAlignmentOptions.Left);
            Overlay(Ref("rankText"), layer, new Vector4(184, 316, 300, 342), cream, TextAlignmentOptions.Left);
            Overlay(Ref("nextLevelText"), layer, new Vector4(302, 338, 342, 374), cream, TextAlignmentOptions.Center);
            Overlay(Ref("xpText"), layer, new Vector4(184, 376, 300, 404), cream, TextAlignmentOptions.Left);
            Transform xpBar = Ref("xpFill")?.parent;
            if (xpBar is RectTransform xpRect)
            {
                MapTo(xpRect, layer, new Vector4(184, 346, 298, 368));
                PaintImage(xpRect, Hex("#3B2210"));
                PaintImage(xpRect.Find("Dolum"), cream);
            }

            Overlay(Ref("playerNameText"), layer, new Vector4(806, 846, 1120, 898), ink, TextAlignmentOptions.Center);
            Overlay(modeChip, layer, new Vector4(1578, 902, 1662, 938), cream, TextAlignmentOptions.Center);
            Transform doubleXpText = doubleXp?.Find("Yazi");
            if (doubleXpText == null)
            {
                foreach (Transform child in layer)
                    if (child.name == "Yazi" && child != modeChip)
                        doubleXpText = child;
            }
            Overlay(doubleXpText, layer, new Vector4(1636, 612, 1842, 676), Color.white, TextAlignmentOptions.Center);

            // Logonun altindaki gri cubuk / online sayaci istenmiyor: yazi kapanir.
            if (Ref("onlineText") is Transform online && online.TryGetComponent(out TextMeshProUGUI onlineLabel))
            {
                Undo.RecordObject(onlineLabel, "Gizle");
                onlineLabel.enabled = false;
            }

            // Butonlar: resmin ustunde gorunmez basma alanlari.
            foreach ((string name, Vector4 rect) in DesignHits)
            {
                if (FindDeep(root, name) is not RectTransform hit)
                    continue;

                MapTo(hit, layer, rect);
                MakeInvisible(hit);
            }

            // Ust / alt barin kendi gorunumu (logo, seritler) resimde var.
            foreach (string hide in new[] { "UstBar", "AltBar" })
            {
                Transform target = FindDeep(root, hide);
                if (target != null && target.gameObject.activeSelf)
                {
                    Undo.RecordObject(target.gameObject, "Gizle");
                    target.gameObject.SetActive(false);
                }
            }

            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            EditorSceneManager.SaveScene(root.gameObject.scene);
            Debug.Log("Sonbahar ana menu: tasarim resmi birebir uygulandi (butonlar resmin ustunde, yazilar canli).");
            ApplyMenuPieces(root);
        }

        /// <summary>Resim pikselindeki dikdortgene yerlestirir (tasarim katmanina gore).</summary>
        private static void MapTo(RectTransform rect, RectTransform layer, Vector4 pixels)
        {
            Undo.SetTransformParent(rect, layer, "Tasi");
            Undo.RecordObject(rect, "Yerlestir");
            rect.anchorMin = new Vector2((pixels.x - DesignLeft) / DesignWidth, 1f - pixels.w / DesignHeight);
            rect.anchorMax = new Vector2((pixels.z - DesignLeft) / DesignWidth, 1f - pixels.y / DesignHeight);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            if (!rect.gameObject.activeSelf)
                rect.gameObject.SetActive(true);
        }

        /// <summary>Canli yaziyi resmin uygun yerine koyar, kutuya sigacak kadar buyutur.</summary>
        private static void Overlay(Transform target, RectTransform layer, Vector4 pixels, Color color,
            TextAlignmentOptions alignment)
        {
            if (target is not RectTransform rect)
                return;

            MapTo(rect, layer, pixels);
            if (rect.TryGetComponent(out TextMeshProUGUI text))
            {
                Undo.RecordObject(text, "Yazi");
                text.enabled = true;
                text.color = color;
                text.alignment = alignment;
                text.enableAutoSizing = true;
                text.fontSizeMin = 10f;
                text.fontSizeMax = 72f;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                EditorUtility.SetDirty(text);
            }
        }

        /// <summary>
        /// Kendi gorunumu kapanir: kokteki resim saydam kalir (basmayi yakalar), icindeki tum
        /// resim ve yazilar kapatilir (resimde zaten cizili).
        /// </summary>
        private static void MakeInvisible(RectTransform hit)
        {
            Image own = hit.GetComponent<Image>();
            if (own == null)
            {
                own = Undo.AddComponent<Image>(hit.gameObject);
                own.sprite = null;
            }

            Undo.RecordObject(own, "Gorunmez");
            own.color = new Color(1f, 1f, 1f, 0f);
            own.raycastTarget = true;
            if (hit.TryGetComponent(out Button button))
            {
                Undo.RecordObject(button, "Buton");
                button.targetGraphic = own;
                button.transition = Selectable.Transition.None;
            }

            foreach (Graphic graphic in hit.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic == own)
                    continue;

                Undo.RecordObject(graphic, "Gizle");
                graphic.enabled = false;
            }
        }

        private static void PaintImage(Transform target, Color color)
        {
            if (target == null || !target.TryGetComponent(out Image image))
                return;

            Undo.RecordObject(image, "Renk");
            image.color = color;
            image.enabled = true;
            EditorUtility.SetDirty(image);
        }
    }
}
#endif
