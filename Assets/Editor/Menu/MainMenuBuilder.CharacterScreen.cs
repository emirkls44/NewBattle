#if UNITY_EDITOR
using NewBattle.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.EditorTools
{
    /// <summary>Ana menudeki "karakter" butonunun actigi lobi animasyonu secim ekrani.</summary>
    public static partial class MainMenuBuilder
    {
        private const string CharacterScreenName = "KarakterEkrani";

        private static bool EnsureCharacterScreen(MainMenuScreen menu)
        {
            var serializedMenu = new SerializedObject(menu);
            SerializedProperty property = serializedMenu.FindProperty("characterScreen");
            if (property.objectReferenceValue != null)
                return false;

            LobbyAnimationScreen screen = BuildCharacterScreen((RectTransform)menu.transform, menu);
            property.objectReferenceValue = screen;
            serializedMenu.ApplyModifiedProperties();
            return true;
        }

        private static LobbyAnimationScreen BuildCharacterScreen(RectTransform menuRoot, MainMenuScreen menu)
        {
            RectTransform root = NewRect(CharacterScreenName, menuRoot);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Karakter ekrani");
            Stretch(root, 0f, 0f, 0f, 0f);
            root.SetAsLastSibling();

            RectTransform background = NewRect("Arkaplan", root);
            Stretch(background, 0f, 0f, 0f, 0f);
            AddImage(background, Sprite("bg_gold"), Color.white, raycast: true).type = Image.Type.Simple;

            Vector2 topLeft = new(0f, 1f);
            TextMeshProUGUI title = AddText(root, "Baslik", "KARAKTER", 84f, BrownDark, TextAlignmentOptions.Left);
            Place(title.rectTransform, topLeft, topLeft, new Vector2(60f, -26f), new Vector2(700f, 100f));
            TextMeshProUGUI subtitle = AddText(root, "AltBaslik", "Lobi animasyonunu seç", 38f, BrownText, TextAlignmentOptions.Left);
            Place(subtitle.rectTransform, topLeft, topLeft, new Vector2(64f, -120f), new Vector2(700f, 50f));

            // Para (sag ust) ve kapat.
            RectTransform wallet = NewRect("Para", root);
            Place(wallet, Vector2.one, Vector2.one, new Vector2(-170f, -36f), new Vector2(270f, 78f));
            AddImage(wallet, Sprite("ui_round"), Navy).pixelsPerUnitMultiplier = 1.4f;
            TextMeshProUGUI coins = AddText(wallet, "Miktar", "100", 44f, Color.white, TextAlignmentOptions.Right, shadow: true);
            Stretch(coins.rectTransform, 20f, 80f, 0f, 0f);
            AddIcon(wallet, "Coin", "coin", Color.white, new Vector2(1f, 0.5f), new Vector2(-18f, 0f), 92f);

            Button close = AddButton(root, "KapatButonu", Vector2.one, Vector2.one, new Vector2(-40f, -36f),
                new Vector2(100f, 100f), Orange, OrangeLip);
            AddIcon(Face(close), "Ikon", "icon_close", Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, 54f);

            // Sol: kaydirilabilir kart listesi.
            RectTransform panel = NewRect("ListePaneli", root);
            panel.anchorMin = new Vector2(0f, 0f);
            panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 0.5f);
            panel.offsetMin = new Vector2(50f, 50f);
            panel.offsetMax = new Vector2(50f + 770f, -180f);
            AddImage(panel, Sprite("ui_round"), CardEdge, raycast: true);
            RectTransform panelInner = NewRect("Ic", panel);
            Stretch(panelInner, 5f, 5f, 5f, 5f);
            AddImage(panelInner, Sprite("ui_round"), CardFill);

            RectTransform viewport = NewRect("Gorunum", panel);
            Stretch(viewport, 18f, 18f, 18f, 18f);
            viewport.gameObject.AddComponent<RectMask2D>();
            AddImage(viewport, null, new Color(1f, 1f, 1f, 0f), raycast: true); // kaydirma dokunuslari icin

            RectTransform content = NewRect("Icerik", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            GridLayoutGroup grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(355f, 130f);
            grid.spacing = new Vector2(14f, 14f);
            grid.padding = new RectOffset(4, 4, 4, 4);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;

            LobbyAnimationCard template = BuildCardTemplate(content);

            // Sag: karakter onizlemesi.
            RectTransform shadow = NewRect("KarakterGolgesi", root);
            Place(shadow, new Vector2(0.68f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 260f), new Vector2(340f, 90f));
            AddImage(shadow, Sprite("shadow_blob"), Color.white).type = Image.Type.Simple;

            RectTransform characterRect = NewRect("Karakter", root);
            characterRect.anchorMin = new Vector2(0.68f, 0f);
            characterRect.anchorMax = new Vector2(0.68f, 1f);
            characterRect.pivot = new Vector2(0.5f, 0.5f);
            characterRect.offsetMin = new Vector2(-300f, 230f);
            characterRect.offsetMax = new Vector2(300f, -160f);
            RawImage characterView = characterRect.gameObject.AddComponent<RawImage>();
            characterView.color = new Color(1f, 1f, 1f, 0f);
            characterView.raycastTarget = false;
            AspectRatioFitter aspect = characterRect.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
            aspect.aspectRatio = 0.75f;

            TextMeshProUGUI previewName = AddText(root, "AnimasyonAdi", "Bekleme", 48f, BrownDark, TextAlignmentOptions.Center);
            Place(previewName.rectTransform, new Vector2(0.68f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 180f), new Vector2(600f, 64f));

            // Sag alt: islem butonu.
            Button action = AddButton(root, "IslemButonu", new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-60f, 50f), new Vector2(440f, 120f), Green, GreenLip);
            TextMeshProUGUI actionLabel = AddText(Face(action), "Yazi", "SEÇ", 56f, Color.white, TextAlignmentOptions.Center, shadow: true);
            Stretch(actionLabel.rectTransform, 14f, 14f, 0f, 4f);

            LobbyAnimationScreen screen = root.gameObject.AddComponent<LobbyAnimationScreen>();
            var serialized = new SerializedObject(screen);
            serialized.FindProperty("library").objectReferenceValue = LobbyAnimationSetup.EnsureLibrary();
            serialized.FindProperty("characterStage").objectReferenceValue = menuRoot.GetComponentInChildren<MenuCharacterStage>(true);
            serialized.FindProperty("menu").objectReferenceValue = menu;
            serialized.FindProperty("listContent").objectReferenceValue = content;
            serialized.FindProperty("cardTemplate").objectReferenceValue = template;
            serialized.FindProperty("characterView").objectReferenceValue = characterView;
            serialized.FindProperty("characterAspect").objectReferenceValue = aspect;
            serialized.FindProperty("previewNameText").objectReferenceValue = previewName;
            serialized.FindProperty("coinText").objectReferenceValue = coins;
            serialized.FindProperty("closeButton").objectReferenceValue = close;
            serialized.FindProperty("actionButton").objectReferenceValue = action;
            serialized.FindProperty("actionFace").objectReferenceValue = Face(action).GetComponent<Image>();
            serialized.FindProperty("actionLabel").objectReferenceValue = actionLabel;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            root.gameObject.SetActive(false);
            return screen;
        }

        /// <summary>Kart sablonu: ekran bunu her animasyon icin cogaltir.</summary>
        private static LobbyAnimationCard BuildCardTemplate(RectTransform content)
        {
            RectTransform card = NewRect("KartSablonu", content);

            Image background = AddImage(card, Sprite("ui_round"), new Color(1f, 0.95f, 0.82f, 1f), raycast: true);
            background.pixelsPerUnitMultiplier = 1.6f;
            Button button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            card.gameObject.AddComponent<UIPressScale>();

            RectTransform outline = NewRect("Cerceve", card);
            Stretch(outline, -6f, -6f, -6f, -6f);
            AddImage(outline, Sprite("ui_round_outline"), Yellow).pixelsPerUnitMultiplier = 1.6f;
            outline.gameObject.SetActive(false);

            Image icon = AddIcon(card, "Ikon", "icon_star", new Color(BrownText.r, BrownText.g, BrownText.b, 0.85f), new Vector2(0f, 0.5f), new Vector2(60f, 0f), 72f);

            TextMeshProUGUI name = AddText(card, "Ad", "Bekleme", 36f, BrownDark, TextAlignmentOptions.Left);
            Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(110f, 18f), new Vector2(190f, 48f));

            TextMeshProUGUI status = AddText(card, "Durum", "Açık", 26f, BrownText, TextAlignmentOptions.Left);
            Place(status.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(110f, -26f), new Vector2(190f, 40f));

            Image lockIcon = AddIcon(card, "Kilit", "icon_lock", BrownDark, Vector2.one, new Vector2(-30f, -30f), 40f);
            Image check = AddIcon(card, "Tik", "icon_check", GreenLip, Vector2.one, new Vector2(-30f, -30f), 44f);
            check.gameObject.SetActive(false);

            LobbyAnimationCard component = card.gameObject.AddComponent<LobbyAnimationCard>();
            var serialized = new SerializedObject(component);
            serialized.FindProperty("background").objectReferenceValue = background;
            serialized.FindProperty("previewOutline").objectReferenceValue = outline.gameObject;
            serialized.FindProperty("icon").objectReferenceValue = icon;
            serialized.FindProperty("nameText").objectReferenceValue = name;
            serialized.FindProperty("statusText").objectReferenceValue = status;
            serialized.FindProperty("lockMark").objectReferenceValue = lockIcon.gameObject;
            serialized.FindProperty("selectedMark").objectReferenceValue = check.gameObject;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return component;
        }
    }
}
#endif
