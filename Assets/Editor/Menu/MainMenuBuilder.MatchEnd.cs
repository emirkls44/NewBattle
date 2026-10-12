#if UNITY_EDITOR
using NewBattle.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NewBattle.EditorTools
{
    /// <summary>ASAMA A: Battlelands tarzi mac sonu ekraninin kurulumu.</summary>
    public static partial class MainMenuBuilder
    {
        private const string MatchEndName = "MacSonuEkrani";

        /// <summary>Yerlesim degisince artar; Kur eski kurulumlari yerinde duzeltir.</summary>
        private const int MatchEndLayoutVersion = 2;

        private static readonly Color ResultOrange = Hex("#FF9A1F");
        private static readonly Color ResultCard = new(0.23f, 0.15f, 0.40f, 0.92f);
        private static readonly Color ResultCardDim = new(0.29f, 0.20f, 0.47f, 0.85f);
        private static readonly Color ResultTrack = Hex("#3E2A66");
        private static readonly Color ResultSoftText = Hex("#D8D0EA");

        private static bool EnsureMatchEndScreen(Canvas canvas, bool rebuild)
        {
            MatchResultHUD hud = Object.FindFirstObjectByType<MatchResultHUD>(FindObjectsInactive.Include);
            if (hud == null)
                return false;

            MatchEndScreen existing = canvas.GetComponentInChildren<MatchEndScreen>(true);
            if (existing != null && !rebuild)
                return UpgradeMatchEndLayout(existing);

            if (existing != null)
                Undo.DestroyObjectImmediate(existing.gameObject);

            MatchEndScreen screen = BuildMatchEndScreen(canvas.transform);

            var serialized = new SerializedObject(hud);
            serialized.FindProperty("endScreen").objectReferenceValue = screen;
            serialized.ApplyModifiedProperties();
            return true;
        }

        private static MatchEndScreen BuildMatchEndScreen(Transform canvas)
        {
            RectTransform root = NewRect(MatchEndName, canvas);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Mac sonu ekrani");
            Stretch(root, 0f, 0f, 0f, 0f);

            // Her seyin ustunde (yukleme ekrani haric).
            Canvas screenCanvas = root.gameObject.AddComponent<Canvas>();
            screenCanvas.overrideSorting = true;
            screenCanvas.sortingOrder = 2000;
            root.gameObject.AddComponent<GraphicRaycaster>();

            RectTransform background = NewRect("Arkaplan", root);
            Stretch(background, 0f, 0f, 0f, 0f);
            AddImage(background, Sprite("bg_result"), Color.white, raycast: true).type = Image.Type.Simple;

            Vector2 topLeft = new(0f, 1f);

            TextMeshProUGUI title = AddText(root, "Baslik", "#17 Maç sona erdi!", 120f, ResultOrange, TextAlignmentOptions.Left, shadow: true);
            Place(title.rectTransform, topLeft, topLeft, new Vector2(130f, -36f), new Vector2(1400f, 140f));

            TextMeshProUGUI message = AddText(root, "Mesaj", "Dorian seni Tüfek ile vurdu!", 44f, Color.white, TextAlignmentOptions.Left, shadow: true);
            Place(message.rectTransform, topLeft, topLeft, new Vector2(150f, -168f), new Vector2(1400f, 64f));

            // Karakter (kartlarin arkasinda kalsin diye once).
            RectTransform character = NewRect("Karakter", root);
            Place(character, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(60f, 10f), new Vector2(560f, 700f));
            RawImage characterImage = character.gameObject.AddComponent<RawImage>();
            characterImage.color = new Color(1f, 1f, 1f, 0f);
            characterImage.raycastTarget = false;

            RectTransform kills = ResultCard_(root, "OldurmeKarti", new Vector2(120f, -20f), "icon_skull", "Savaşçılar yenildi", out TextMeshProUGUI killsText);
            RectTransform xp = ResultCard_(root, "XpKarti", new Vector2(335f, -20f), "icon_star", "Maç XP", out TextMeshProUGUI xpText);
            TextMeshProUGUI xpBadge = AddText(xp.Find("Ikon"), "XP", "XP", 34f, Hex("#5B3A93"), TextAlignmentOptions.Center);
            Stretch(xpBadge.rectTransform, 0f, 0f, 10f, 0f);
            RectTransform coins = ResultCard_(root, "ParaKarti", new Vector2(550f, -20f), "coin", "Para", out TextMeshProUGUI coinsText);
            coins.Find("Ikon").GetComponent<Image>().color = Color.white;

            // Battle Pass karti (simdilik bilgi; islevi sonra baglanacak).
            RectTransform pass = NewRect("BattlePassKarti", root);
            Place(pass, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-330f, 30f), new Vector2(230f, 290f));
            Image passImage = AddImage(pass, Sprite("ui_round"), ResultCardDim, raycast: true);
            passImage.pixelsPerUnitMultiplier = 1.4f;
            Button passButton = pass.gameObject.AddComponent<Button>();
            passButton.targetGraphic = passImage;
            pass.gameObject.AddComponent<UIPressScale>();
            AddIcon(pass, "Ikon", "battlepass_badge", Color.white, new Vector2(0.5f, 1f), new Vector2(0f, -78f), 110f);
            TextMeshProUGUI passTitle = AddText(pass, "Baslik", "Battle Pass\nGerekli", 34f, ResultOrange, TextAlignmentOptions.Center, shadow: true);
            passTitle.lineSpacing = -18f;
            Place(passTitle.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 74f), new Vector2(210f, 80f));
            TextMeshProUGUI passBody = AddText(pass, "Yazi", "Öldürme XP\nBonusu", 26f, Color.white, TextAlignmentOptions.Center);
            passBody.lineSpacing = -14f;
            Place(passBody.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(210f, 64f));
            AttachActions(new[] { passButton });
            SetActionId(new SerializedObject(passButton.GetComponent<MenuButton>()), "battle_pass_bonus");

            // Seviye bari (alt orta).
            Vector2 bottom = new(0.5f, 0f);
            TextMeshProUGUI level = AddText(root, "Seviye", "SEVİYE 1", 48f, ResultSoftText, TextAlignmentOptions.Left, shadow: true);
            TextMeshProUGUI numbers = AddText(root, "XpSayisi", "0 / 50", 40f, Color.white, TextAlignmentOptions.Right, shadow: true);

            RectTransform track = NewRect("XpBari", root);
            Place(track, bottom, new Vector2(0.5f, 0.5f), new Vector2(-40f, 96f), new Vector2(460f, 40f));
            AddImage(track, Sprite("ui_round"), ResultTrack).pixelsPerUnitMultiplier = 2.2f;
            RectTransform fill = NewRect("Dolum", track);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0.3f, 1f);
            fill.offsetMin = new Vector2(5f, 5f);
            fill.offsetMax = new Vector2(0f, -5f);
            AddImage(fill, Sprite("ui_round"), Yellow).pixelsPerUnitMultiplier = 2.8f;

            AddIcon(root, "Ok", "icon_play", ResultSoftText, bottom, new Vector2(214f, 96f), 34f);
            TextMeshProUGUI next = AddText(root, "SonrakiSeviye", "2", 72f, ResultSoftText, TextAlignmentOptions.Left, shadow: true);
            LayoutLevelTexts(level, numbers, next);

            TextMeshProUGUI levelUp = AddText(root, "SeviyeAtladin", "SEVİYE ATLADIN!", 72f, Yellow, TextAlignmentOptions.Center, shadow: true);
            Place(levelUp.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -290f), new Vector2(900f, 100f));
            levelUp.gameObject.SetActive(false);

            Button continueButton = AddButton(root, "DevamButonu", new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-110f, 60f), new Vector2(380f, 130f), Yellow, YellowLip);
            TextMeshProUGUI continueLabel = AddText(Face(continueButton), "Yazi", "DEVAM ET", 64f, Color.white, TextAlignmentOptions.Center, shadow: true);
            Stretch(continueLabel.rectTransform, 12f, 12f, 0f, 4f);

            MatchEndScreen screen = root.gameObject.AddComponent<MatchEndScreen>();
            var serializedScreen = new SerializedObject(screen);
            serializedScreen.FindProperty("screenCanvas").objectReferenceValue = screenCanvas;
            serializedScreen.FindProperty("titleText").objectReferenceValue = title;
            serializedScreen.FindProperty("messageText").objectReferenceValue = message;
            SerializedProperty cardList = serializedScreen.FindProperty("cards");
            cardList.arraySize = 3;
            cardList.GetArrayElementAtIndex(0).objectReferenceValue = kills;
            cardList.GetArrayElementAtIndex(1).objectReferenceValue = xp;
            cardList.GetArrayElementAtIndex(2).objectReferenceValue = coins;
            serializedScreen.FindProperty("killsText").objectReferenceValue = killsText;
            serializedScreen.FindProperty("xpText").objectReferenceValue = xpText;
            serializedScreen.FindProperty("coinsText").objectReferenceValue = coinsText;
            serializedScreen.FindProperty("characterImage").objectReferenceValue = characterImage;
            serializedScreen.FindProperty("levelText").objectReferenceValue = level;
            serializedScreen.FindProperty("xpNumbersText").objectReferenceValue = numbers;
            serializedScreen.FindProperty("nextLevelText").objectReferenceValue = next;
            serializedScreen.FindProperty("xpFill").objectReferenceValue = fill;
            serializedScreen.FindProperty("levelUpBanner").objectReferenceValue = levelUp.rectTransform;
            serializedScreen.FindProperty("continueButton").objectReferenceValue = continueButton;
            serializedScreen.FindProperty("layoutVersion").intValue = MatchEndLayoutVersion;
            serializedScreen.ApplyModifiedPropertiesWithoutUndo();

            screenCanvas.enabled = false;
            return screen;
        }

        /// <summary>
        /// Seviye yazisi barin sol yarisinin, XP sayilari sag yarisinin ustunde;
        /// birbirine binmezler. Cok buyuk sayilarda yazilar kuculur.
        /// </summary>
        private static void LayoutLevelTexts(TextMeshProUGUI level, TextMeshProUGUI numbers, TextMeshProUGUI next)
        {
            Vector2 bottom = new(0.5f, 0f);
            Place(level.rectTransform, bottom, new Vector2(0f, 0f), new Vector2(-270f, 122f), new Vector2(225f, 60f));
            Place(numbers.rectTransform, bottom, new Vector2(1f, 0f), new Vector2(190f, 122f), new Vector2(225f, 56f));
            Place(next.rectTransform, bottom, new Vector2(0f, 0.5f), new Vector2(240f, 100f), new Vector2(170f, 80f));

            level.fontSizeMin = 18f;
            numbers.fontSizeMin = 16f;
            next.fontSizeMin = 24f;
            foreach (TextMeshProUGUI text in new[] { level, numbers, next })
            {
                text.enableAutoSizing = true;
                text.overflowMode = TextOverflowModes.Ellipsis;
            }
        }

        /// <summary>Eski surumle kurulmus ekrani yerinde duzeltir. Duzelttiyse true.</summary>
        private static bool UpgradeMatchEndLayout(MatchEndScreen screen)
        {
            var serialized = new SerializedObject(screen);
            SerializedProperty version = serialized.FindProperty("layoutVersion");
            if (version.intValue >= MatchEndLayoutVersion)
                return false;

            Transform root = screen.transform;
            var level = root.Find("Seviye")?.GetComponent<TextMeshProUGUI>();
            var numbers = root.Find("XpSayisi")?.GetComponent<TextMeshProUGUI>();
            var next = root.Find("SonrakiSeviye")?.GetComponent<TextMeshProUGUI>();
            if (level != null && numbers != null && next != null)
            {
                Undo.RecordObjects(new Object[] { level, numbers, next, level.rectTransform, numbers.rectTransform, next.rectTransform }, "Mac sonu yerlesimi");
                LayoutLevelTexts(level, numbers, next);
            }

            MoveCard(root, "OldurmeKarti", 120f);
            MoveCard(root, "XpKarti", 335f);
            MoveCard(root, "ParaKarti", 550f);

            if (root.Find("Karakter") is RectTransform character)
            {
                Undo.RecordObject(character, "Mac sonu yerlesimi");
                character.anchoredPosition = new Vector2(60f, character.anchoredPosition.y);
            }

            version.intValue = MatchEndLayoutVersion;
            serialized.ApplyModifiedProperties();
            return true;
        }

        private static void MoveCard(Transform root, string name, float x)
        {
            if (!(root.Find(name) is RectTransform card))
                return;

            Undo.RecordObject(card, "Mac sonu yerlesimi");
            card.anchoredPosition = new Vector2(x, card.anchoredPosition.y);
        }

        /// <summary>Koyu mor kart: ikon, buyuk turuncu sayi, alt yazi.</summary>
        private static RectTransform ResultCard_(RectTransform root, string name, Vector2 position, string icon, string label,
            out TextMeshProUGUI value)
        {
            RectTransform card = NewRect(name, root);
            Place(card, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), position, new Vector2(200f, 290f));
            AddImage(card, Sprite("ui_round"), ResultCard).pixelsPerUnitMultiplier = 1.4f;

            AddIcon(card, "Ikon", icon, Color.white, new Vector2(0.5f, 1f), new Vector2(0f, -76f), 104f);

            value = AddText(card, "Sayi", "0", 96f, ResultOrange, TextAlignmentOptions.Center, shadow: true);
            value.enableAutoSizing = false;
            Place(value.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -22f), new Vector2(190f, 110f));

            TextMeshProUGUI caption = AddText(card, "Yazi", label, 30f, Color.white, TextAlignmentOptions.Center);
            caption.textWrappingMode = TextWrappingModes.Normal;
            Place(caption.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(186f, 70f));
            return card;
        }

        private static void SetActionId(SerializedObject action, string id)
        {
            action.FindProperty("actionId").stringValue = id;
            action.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
