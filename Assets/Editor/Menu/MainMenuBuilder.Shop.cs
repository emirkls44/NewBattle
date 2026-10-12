#if UNITY_EDITOR
using NewBattle.Progression;
using NewBattle.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.EditorTools
{
    /// <summary>Ana menudeki "magaza" / para butonlarinin actigi MAĞAZA ekrani.</summary>
    public static partial class MainMenuBuilder
    {
        private const string ShopScreenName = "MagazaEkrani";
        private const string ShopCatalogFolder = "Assets/GameArt/Shop";
        private const string ShopCatalogPath = ShopCatalogFolder + "/MagazaUrunleri.asset";

        /// <summary>2: sagda karakter onizlemesi + islem butonu, kartlarda onizleme cercevesi.</summary>
        private const int ShopLayoutVersion = 2;

        private static ShopCatalog EnsureShopCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(ShopCatalogPath);
            if (catalog != null)
                return catalog;

            if (!AssetDatabase.IsValidFolder(ShopCatalogFolder))
                AssetDatabase.CreateFolder("Assets/GameArt", "Shop");

            catalog = ScriptableObject.CreateInstance<ShopCatalog>();
            AssetDatabase.CreateAsset(catalog, ShopCatalogPath);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        private static bool EnsureShopScreen(MainMenuScreen menu)
        {
            var serializedMenu = new SerializedObject(menu);
            SerializedProperty property = serializedMenu.FindProperty("shopScreen");
            if (property.objectReferenceValue is ShopScreen existing)
                return UpgradeShopLayout(existing, menu);

            ShopScreen screen = menu.GetComponentInChildren<ShopScreen>(true);
            if (screen == null)
                screen = BuildShopScreen((RectTransform)menu.transform, menu);

            UpgradeShopLayout(screen, menu);
            property.objectReferenceValue = screen;
            serializedMenu.ApplyModifiedProperties();
            return true;
        }

        /// <summary>
        /// Eski magazaya karakter onizlemesi ekler. Liste paneli ve kart
        /// boyutu sadece kurulumdaki haliyle duruyorsa (sen degistirmediysen)
        /// yer acmak icin daraltilir. Duzelttiyse true.
        /// </summary>
        private static bool UpgradeShopLayout(ShopScreen screen, MainMenuScreen menu)
        {
            var serialized = new SerializedObject(screen);
            SerializedProperty version = serialized.FindProperty("layoutVersion");
            if (version.intValue >= ShopLayoutVersion)
                return false;

            RectTransform root = (RectTransform)screen.transform;

            if (root.Find("ListePaneli") is RectTransform panel &&
                panel.offsetMin == new Vector2(50f, 40f) && panel.offsetMax == new Vector2(-50f, -180f))
            {
                Undo.RecordObject(panel, "Magaza yerlesimi");
                panel.offsetMax = new Vector2(-660f, -180f);

                GridLayoutGroup grid = panel.GetComponentInChildren<GridLayoutGroup>(true);
                if (grid != null && grid.constraintCount == 4 && grid.cellSize == new Vector2(380f, 440f))
                {
                    Undo.RecordObject(grid, "Magaza yerlesimi");
                    grid.constraintCount = 3;
                    grid.cellSize = new Vector2(350f, 420f);
                }
            }

            if (serialized.FindProperty("actionButton").objectReferenceValue == null)
            {
                const float column = 0.83f;
                RectTransform shadow = NewRect("KarakterGolgesi", root);
                Undo.RegisterCreatedObjectUndo(shadow.gameObject, "Magaza onizleme");
                Place(shadow, new Vector2(column, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 270f), new Vector2(300f, 80f));
                AddImage(shadow, Sprite("shadow_blob"), Color.white).type = Image.Type.Simple;

                RectTransform characterRect = NewRect("Karakter", root);
                Undo.RegisterCreatedObjectUndo(characterRect.gameObject, "Magaza onizleme");
                characterRect.anchorMin = new Vector2(column, 0f);
                characterRect.anchorMax = new Vector2(column, 1f);
                characterRect.pivot = new Vector2(0.5f, 0.5f);
                characterRect.offsetMin = new Vector2(-280f, 240f);
                characterRect.offsetMax = new Vector2(280f, -190f);
                RawImage view = characterRect.gameObject.AddComponent<RawImage>();
                view.color = new Color(1f, 1f, 1f, 0f);
                view.raycastTarget = false;
                AspectRatioFitter aspect = characterRect.gameObject.AddComponent<AspectRatioFitter>();
                aspect.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
                aspect.aspectRatio = 0.75f;

                TextMeshProUGUI previewName = AddText(root, "AnimasyonAdi", "Bekleme", 48f, Color.white, TextAlignmentOptions.Center, shadow: true);
                Undo.RegisterCreatedObjectUndo(previewName.gameObject, "Magaza onizleme");
                Place(previewName.rectTransform, new Vector2(column, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(600f, 64f));

                Button action = AddButton(root, "IslemButonu", new Vector2(column, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0f, 50f), new Vector2(460f, 120f), Green, GreenLip);
                Undo.RegisterCreatedObjectUndo(action.gameObject, "Magaza onizleme");
                TextMeshProUGUI actionLabel = AddText(Face(action), "Yazi", "SEÇ", 52f, Color.white, TextAlignmentOptions.Center, shadow: true);
                Stretch(actionLabel.rectTransform, 14f, 14f, 0f, 4f);

                // Onay penceresi her seyin ustunde kalsin.
                if (serialized.FindProperty("confirmRoot").objectReferenceValue is GameObject confirm)
                    confirm.transform.SetAsLastSibling();

                serialized.FindProperty("characterStage").objectReferenceValue = menu.GetComponentInChildren<MenuCharacterStage>(true);
                serialized.FindProperty("characterView").objectReferenceValue = view;
                serialized.FindProperty("characterAspect").objectReferenceValue = aspect;
                serialized.FindProperty("previewNameText").objectReferenceValue = previewName;
                serialized.FindProperty("actionButton").objectReferenceValue = action;
                serialized.FindProperty("actionFace").objectReferenceValue = Face(action).GetComponent<Image>();
                serialized.FindProperty("actionLip").objectReferenceValue = action.transform.Find("Alt").GetComponent<Image>();
                serialized.FindProperty("actionLabel").objectReferenceValue = actionLabel;
            }

            if (serialized.FindProperty("cardTemplate").objectReferenceValue is ShopItemCard template)
            {
                var serializedCard = new SerializedObject(template);
                SerializedProperty outlineProperty = serializedCard.FindProperty("previewOutline");
                if (outlineProperty.objectReferenceValue == null)
                {
                    RectTransform outline = NewRect("Cerceve", template.transform);
                    Undo.RegisterCreatedObjectUndo(outline.gameObject, "Magaza onizleme");
                    Stretch(outline, -7f, -7f, -7f, -7f);
                    AddImage(outline, Sprite("ui_round_outline"), Yellow).pixelsPerUnitMultiplier = 1.4f;
                    outline.gameObject.SetActive(false);
                    outlineProperty.objectReferenceValue = outline.gameObject;
                    serializedCard.ApplyModifiedProperties();
                }
            }

            version.intValue = ShopLayoutVersion;
            serialized.ApplyModifiedProperties();
            return true;
        }

        private static ShopScreen BuildShopScreen(RectTransform menuRoot, MainMenuScreen menu)
        {
            RectTransform root = NewRect(ShopScreenName, menuRoot);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Magaza");
            Stretch(root, 0f, 0f, 0f, 0f);
            root.SetAsLastSibling();

            RectTransform background = NewRect("Arkaplan", root);
            Stretch(background, 0f, 0f, 0f, 0f);
            AddImage(background, Sprite("bg_blue"), Color.white, raycast: true).type = Image.Type.Simple;

            Vector2 topLeft = new(0f, 1f);
            TextMeshProUGUI title = AddText(root, "Baslik", "MAĞAZA", 84f, Color.white, TextAlignmentOptions.Left, shadow: true);
            Place(title.rectTransform, topLeft, topLeft, new Vector2(60f, -26f), new Vector2(700f, 100f));
            TextMeshProUGUI subtitle = AddText(root, "AltBaslik", "Paranı harca, yeni şeyler aç!", 38f, Yellow, TextAlignmentOptions.Left, shadow: true);
            Place(subtitle.rectTransform, topLeft, topLeft, new Vector2(64f, -120f), new Vector2(900f, 50f));

            RectTransform wallet = NewRect("Para", root);
            Place(wallet, Vector2.one, Vector2.one, new Vector2(-170f, -36f), new Vector2(270f, 78f));
            AddImage(wallet, Sprite("ui_round"), Navy).pixelsPerUnitMultiplier = 1.4f;
            TextMeshProUGUI coins = AddText(wallet, "Miktar", "100", 44f, Color.white, TextAlignmentOptions.Right, shadow: true);
            Stretch(coins.rectTransform, 20f, 80f, 0f, 0f);
            AddIcon(wallet, "Coin", "coin", Color.white, new Vector2(1f, 0.5f), new Vector2(-18f, 0f), 92f);

            Button close = AddButton(root, "KapatButonu", Vector2.one, Vector2.one, new Vector2(-40f, -36f),
                new Vector2(100f, 100f), Orange, OrangeLip);
            AddIcon(Face(close), "Ikon", "icon_close", Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, 54f);

            // Kaydirilabilir urun listesi.
            RectTransform panel = NewRect("ListePaneli", root);
            Stretch(panel, 50f, 50f, 180f, 40f);
            AddImage(panel, Sprite("ui_round"), new Color(0f, 0f, 0f, 0.22f), raycast: true);

            RectTransform viewport = NewRect("Gorunum", panel);
            Stretch(viewport, 20f, 20f, 20f, 20f);
            viewport.gameObject.AddComponent<RectMask2D>();
            AddImage(viewport, null, new Color(1f, 1f, 1f, 0f), raycast: true);

            RectTransform content = NewRect("Icerik", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            GridLayoutGroup grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(380f, 440f);
            grid.spacing = new Vector2(28f, 28f);
            grid.padding = new RectOffset(8, 8, 8, 8);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;

            ShopItemCard template = BuildShopCardTemplate(content);

            (GameObject confirmRoot, TextMeshProUGUI confirmText, Button yes, Button no) = BuildShopConfirm(root);

            ShopScreen screen = root.gameObject.AddComponent<ShopScreen>();
            var serialized = new SerializedObject(screen);
            serialized.FindProperty("catalog").objectReferenceValue = EnsureShopCatalog();
            serialized.FindProperty("library").objectReferenceValue = LobbyAnimationSetup.EnsureLibrary();
            serialized.FindProperty("menu").objectReferenceValue = menu;
            serialized.FindProperty("scroll").objectReferenceValue = scroll;
            serialized.FindProperty("listContent").objectReferenceValue = content;
            serialized.FindProperty("cardTemplate").objectReferenceValue = template;
            serialized.FindProperty("coinText").objectReferenceValue = coins;
            serialized.FindProperty("closeButton").objectReferenceValue = close;
            serialized.FindProperty("confirmRoot").objectReferenceValue = confirmRoot;
            serialized.FindProperty("confirmText").objectReferenceValue = confirmText;
            serialized.FindProperty("confirmYes").objectReferenceValue = yes;
            serialized.FindProperty("confirmNo").objectReferenceValue = no;
            serialized.FindProperty("animationIcon").objectReferenceValue = Sprite("icon_star");
            serialized.FindProperty("adIcon").objectReferenceValue = Sprite("icon_play");
            serialized.FindProperty("coinPackIcon").objectReferenceValue = Sprite("coin");
            serialized.ApplyModifiedPropertiesWithoutUndo();

            root.gameObject.SetActive(false);
            return screen;
        }

        private static ShopItemCard BuildShopCardTemplate(RectTransform content)
        {
            RectTransform card = NewRect("KartSablonu", content);
            Image background = AddImage(card, Sprite("ui_round"), new Color(1f, 0.95f, 0.82f, 1f), raycast: true);
            background.pixelsPerUnitMultiplier = 1.4f;
            Button button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            card.gameObject.AddComponent<UIPressScale>();

            TextMeshProUGUI name = AddText(card, "Ad", "Nişan Al", 40f, BrownDark, TextAlignmentOptions.Center);
            Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(350f, 56f));

            Image icon = AddIcon(card, "Ikon", "icon_star", Color.white, new Vector2(0.5f, 0.5f), new Vector2(0f, 36f), 180f);

            TextMeshProUGUI note = AddText(card, "Not", "Lobi animasyonu", 26f, BrownText, TextAlignmentOptions.Center);
            Place(note.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 116f), new Vector2(350f, 40f));

            RectTransform price = NewRect("Fiyat", card);
            Place(price, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(330f, 88f));
            Image priceBackground = AddImage(price, Sprite("ui_round"), Green);
            priceBackground.pixelsPerUnitMultiplier = 1.8f;
            Image coin = AddIcon(price, "Coin", "coin", Color.white, new Vector2(0f, 0.5f), new Vector2(50f, 0f), 72f);
            TextMeshProUGUI priceText = AddText(price, "Yazi", "200", 50f, Color.white, TextAlignmentOptions.Center, shadow: true);
            Stretch(priceText.rectTransform, 70f, 16f, 0f, 3f);

            Image owned = AddIcon(card, "Sahip", "icon_check", Green, Vector2.one, new Vector2(-36f, -36f), 60f);
            owned.gameObject.SetActive(false);

            ShopItemCard component = card.gameObject.AddComponent<ShopItemCard>();
            var serialized = new SerializedObject(component);
            serialized.FindProperty("background").objectReferenceValue = background;
            serialized.FindProperty("icon").objectReferenceValue = icon;
            serialized.FindProperty("nameText").objectReferenceValue = name;
            serialized.FindProperty("priceBackground").objectReferenceValue = priceBackground;
            serialized.FindProperty("priceCoin").objectReferenceValue = coin;
            serialized.FindProperty("priceText").objectReferenceValue = priceText;
            serialized.FindProperty("ownedMark").objectReferenceValue = owned.gameObject;
            serialized.FindProperty("noteText").objectReferenceValue = note;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return component;
        }

        private static (GameObject, TextMeshProUGUI, Button, Button) BuildShopConfirm(RectTransform root)
        {
            RectTransform confirm = NewRect("SatinAlmaOnayi", root);
            Stretch(confirm, 0f, 0f, 0f, 0f);

            RectTransform dim = NewRect("Karartma", confirm);
            Stretch(dim, 0f, 0f, 0f, 0f);
            AddImage(dim, null, new Color(0f, 0f, 0f, 0.6f), raycast: true);

            RectTransform window = NewRect("Pencere", confirm);
            Place(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920f, 500f));
            AddImage(window, Sprite("ui_round"), ResultCard, raycast: true).pixelsPerUnitMultiplier = 1.2f;
            CardHeader(window, "SATIN AL", Orange, Color.white, 64f, shadow: true, inset: 0f, height: 100f);

            TextMeshProUGUI text = AddText(window, "Yazi", "Nişan Al\n200 para karşılığında alınsın mı?", 46f, Color.white, TextAlignmentOptions.Center, shadow: true);
            text.textWrappingMode = TextWrappingModes.Normal;
            Place(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(840f, 200f));

            Button no = AddButton(window, "VazgecButonu", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-200f, 44f), new Vector2(340f, 112f), Orange, OrangeLip);
            TextMeshProUGUI noLabel = AddText(Face(no), "Yazi", "VAZGEÇ", 50f, Color.white, TextAlignmentOptions.Center, shadow: true);
            Stretch(noLabel.rectTransform, 12f, 12f, 0f, 4f);

            Button yes = AddButton(window, "EvetButonu", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(200f, 44f), new Vector2(340f, 112f), Green, GreenLip);
            TextMeshProUGUI yesLabel = AddText(Face(yes), "Yazi", "SATIN AL", 50f, Color.white, TextAlignmentOptions.Center, shadow: true);
            Stretch(yesLabel.rectTransform, 12f, 12f, 0f, 4f);

            confirm.gameObject.SetActive(false);
            return (confirm.gameObject, text, yes, no);
        }
    }
}
#endif
