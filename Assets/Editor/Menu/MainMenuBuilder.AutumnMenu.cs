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
    ///   - Resim ana menunun gorunumudur (oran korunur, kenarlarda bg_sonbahar).
    ///   - Butonlar resimdeki buton resimlerinin tam ustunde gorunmez basma alanlari.
    ///   - Degisen yazilar (para, seviye, XP, isim, mod, online, cift XP) resimden silindi;
    ///     yerine oyunun gercek degerleri ayni yere yazilir.
    /// Koordinatlar tasarim resminin piksel olcusunde (2000x1120 kaynak, soldan 16 px kirpik).
    /// Unity derleyince bir kez kendiliginden uygulanir. Geri: tag "safe-oncesi-sonbahar-menu".
    /// </summary>
    public static partial class MainMenuBuilder
    {
        private const string DesignSprite = "tasarim_sonbahar";
        private const string DesignBackground = "bg_sonbahar";
        private const float DesignLeft = 16f, DesignWidth = 1884f, DesignHeight = 1120f;

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
            if (screen != null && screen.transform.Find("Tasarim") == null)
                ApplyAutumnMenu(screen.transform);
        }

        [MenuItem("Tools/NewBattle/Menü/Sonbahar Ana Menü (tasarım resmi)", false, 6)]
        private static void ApplyAutumnMenuItem()
        {
            Canvas canvas = FindGameCanvas();
            MainMenuScreen screen = canvas != null ? canvas.GetComponentInChildren<MainMenuScreen>(true) : null;
            if (screen != null && screen.transform.Find("Tasarim") == null)
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

            // Tasarim resmi: oran korunur, ekrana sigar (genis telefonda kenarlarda arka plan).
            RectTransform layer = NewRect("Tasarim", rootRect);
            Undo.RegisterCreatedObjectUndo(layer.gameObject, "Tasarim");
            Stretch(layer, 0f, 0f, 0f, 0f);
            Image layerImage = AddImage(layer, design, Color.white);
            layerImage.type = Image.Type.Simple;
            AspectRatioFitter fitter = layer.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = DesignWidth / DesignHeight;
            layer.SetAsFirstSibling();

            Color cream = Hex("#F6E2C2");
            Color ink = Hex("#4E2D14");

            // Degisen yazilar once disari alinir (gizlenen kartlarin icinde kalmasinlar).
            Transform season = FindDeep(root, "SezonKarti");
            Transform wallet = FindDeep(root, "ParaPaneli");
            Transform nameTag = FindDeep(root, "IsimEtiketi");
            Transform play = FindDeep(root, "OynaButonu");
            Transform doubleXp = FindDeep(root, "CiftXpKarti");

            Overlay(wallet?.Find("ParaMiktari"), layer, new Vector4(1572, 58, 1672, 120), cream, TextAlignmentOptions.Center);
            Overlay(season?.Find("Seviye"), layer, new Vector4(184, 276, 342, 318), cream, TextAlignmentOptions.Left);
            Overlay(season?.Find("Rutbe"), layer, new Vector4(184, 316, 300, 342), cream, TextAlignmentOptions.Left);
            Overlay(season?.Find("SonrakiSeviye"), layer, new Vector4(302, 338, 342, 374), cream, TextAlignmentOptions.Center);
            Overlay(season?.Find("XpYazisi"), layer, new Vector4(184, 376, 300, 404), cream, TextAlignmentOptions.Left);
            Transform xpBar = season?.Find("XpBari");
            if (xpBar is RectTransform xpRect)
            {
                MapTo(xpRect, layer, new Vector4(184, 346, 298, 368));
                PaintImage(xpRect, Hex("#3B2210"));
                PaintImage(xpRect.Find("Dolum"), cream);
            }

            Overlay(nameTag?.Find("Isim"), layer, new Vector4(806, 846, 1120, 898), ink, TextAlignmentOptions.Center);
            Overlay(play?.Find("ModEtiketi/Yazi"), layer, new Vector4(1578, 902, 1662, 938), cream, TextAlignmentOptions.Center);
            Overlay(FindDeep(root, "OnlineSayac"), layer, new Vector4(70, 116, 640, 162), Hex("#5A3A20"), TextAlignmentOptions.Left);
            Overlay(doubleXp?.Find("Yazi"), layer, new Vector4(1636, 612, 1842, 676), Color.white, TextAlignmentOptions.Center);

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
