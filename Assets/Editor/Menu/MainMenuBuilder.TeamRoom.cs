#if UNITY_EDITOR
using NewBattle.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// TAKIMLA OYNA: oyun modu ekranindaki adlar, oda kurma penceresi (oyuncu sayisi +
    /// takim boyu + kodla katil) ve maca girince acilan TAKIM ODASI ekrani.
    /// </summary>
    public static partial class MainMenuBuilder
    {
        private const int TeamPopupLayoutVersion = 1;
        private const string TeamRoomScreenName = "TakimOdasiEkrani";

        private static readonly int[] RoomSizes = { 0, 4, 8, 16 };
        private static readonly string[] RoomSizeLabels = { "KATILAN KADAR", "4", "8", "16" };
        private static readonly int[] RoomTeamSizes = { 2, 4 };
        private static readonly string[] RoomTeamLabels = { "2'Lİ TAKIM", "4'LÜ TAKIM" };
        private static readonly Color RoomChoiceFace = Hex("#52417F");
        private static readonly Color RoomChoiceLip = Hex("#2E2350");

        // ------------------------------------------------------------------
        // Oyun modu ekrani + Takimla Oyna penceresi
        // ------------------------------------------------------------------

        private static bool EnsureFriendRoom(MainMenuScreen menu)
        {
            bool changed = false;
            var serializedMenu = new SerializedObject(menu);

            if (serializedMenu.FindProperty("modePanel").objectReferenceValue is ModeSelectPanel panel && RenameTeamMode(panel))
                changed = true;

            SerializedProperty popupProperty = serializedMenu.FindProperty("friendRoomPopup");
            var popup = popupProperty.objectReferenceValue as FriendRoomPopup;
            if (popup == null)
                popup = menu.GetComponentInChildren<FriendRoomPopup>(true);

            if (popup != null && new SerializedObject(popup).FindProperty("layoutVersion").intValue >= TeamPopupLayoutVersion)
            {
                if (popupProperty.objectReferenceValue != popup)
                {
                    popupProperty.objectReferenceValue = popup;
                    serializedMenu.ApplyModifiedProperties();
                    changed = true;
                }

                return changed;
            }

            // Eski "Arkadas Ikilisi" penceresi: yenisiyle degistir.
            if (popup != null)
                Undo.DestroyObjectImmediate(popup.gameObject);

            popupProperty.objectReferenceValue = BuildTeamPopup((RectTransform)menu.transform, menu);
            serializedMenu.ApplyModifiedProperties();
            return true;
        }

        /// <summary>ARKADAŞ İKİLİSİ -> TAKIMLA OYNA, ÇEVRİMİÇİ OYNA -> OYNA (bir kez; elle degistirdiysen dokunmaz).</summary>
        private static bool RenameTeamMode(ModeSelectPanel panel)
        {
            bool changed = false;
            var serialized = new SerializedObject(panel);

            SerializedProperty options = serialized.FindProperty("options");
            for (int i = 0; i < options.arraySize; i++)
            {
                SerializedProperty option = options.GetArrayElementAtIndex(i);
                if (option.FindPropertyRelative("id").stringValue != "arkadas_ikilisi")
                    continue;

                SerializedProperty locked = option.FindPropertyRelative("locked");
                if (locked.boolValue)
                {
                    locked.boolValue = false;
                    changed = true;
                }

                SerializedProperty shortLabel = option.FindPropertyRelative("shortLabel");
                if (shortLabel.stringValue == "ARKADAŞ")
                {
                    shortLabel.stringValue = "TAKIM";
                    changed = true;
                }

                if (option.FindPropertyRelative("label").objectReferenceValue is TextMeshProUGUI label &&
                    label.text == "ARKADAŞ İKİLİSİ")
                {
                    Undo.RecordObject(label, "Takimla Oyna");
                    label.text = "TAKIMLA OYNA";
                    changed = true;
                }
            }

            if (serialized.FindProperty("playButton").objectReferenceValue is Button play)
            {
                TextMeshProUGUI playLabel = play.transform.Find("Yuz/Yazi")?.GetComponent<TextMeshProUGUI>();
                if (playLabel != null && playLabel.text == "ÇEVRİMİÇİ OYNA")
                {
                    Undo.RecordObject(playLabel, "Oyna");
                    playLabel.text = "OYNA";
                    changed = true;
                }
            }

            SerializedProperty tips = serialized.FindProperty("tips");
            for (int i = 0; tips != null && i < tips.arraySize; i++)
            {
                SerializedProperty tip = tips.GetArrayElementAtIndex(i);
                if (!tip.stringValue.Contains("Arkadaş İkilisi"))
                    continue;

                tip.stringValue = "Arkadaşlarını Takımla Oyna'ya çağır, birlikte oyna!";
                changed = true;
            }

            serialized.ApplyModifiedProperties();
            return changed;
        }

        private static FriendRoomPopup BuildTeamPopup(RectTransform menuRoot, MainMenuScreen menu)
        {
            (RectTransform root, RectTransform window) = PopupShell(menuRoot, "TakimlaOynaPenceresi", "TAKIMLA OYNA", Vector2.zero);
            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();

            // Oyun modu ekraniyla ayni buyuklukte.
            window.anchorMin = new Vector2(0.03f, 0.13f);
            window.anchorMax = new Vector2(0.97f, 0.92f);
            window.offsetMin = Vector2.zero;
            window.offsetMax = Vector2.zero;

            Button close = PopupCloseButton(window);

            TextMeshProUGUI hint = AddText(window, "Ipucu", "Arkadaşlarınla takım kur, birlikte oyna!", 34f, ResultSoftText, TextAlignmentOptions.Center);
            Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(1400f, 50f));

            Vector2 topLeft = new(0f, 1f);

            // Sol: oda kur.
            RectTransform left = NewRect("OdaKur", window);
            left.anchorMin = Vector2.zero;
            left.anchorMax = new Vector2(0.6f, 1f);
            left.offsetMin = new Vector2(50f, 36f);
            left.offsetMax = new Vector2(-20f, -190f);

            TextMeshProUGUI sizeTitle = AddText(left, "OyuncuSayisiBaslik", "OYUNCU SAYISI", 38f, Gold, TextAlignmentOptions.Left, shadow: true);
            Place(sizeTitle.rectTransform, topLeft, topLeft, new Vector2(0f, 0f), new Vector2(700f, 50f));

            var popupObject = root.gameObject.AddComponent<FriendRoomPopup>();
            var serialized = new SerializedObject(popupObject);

            SerializedProperty sizes = serialized.FindProperty("sizeChoices");
            sizes.arraySize = RoomSizes.Length;
            float x = 0f;
            for (int i = 0; i < RoomSizes.Length; i++)
            {
                float width = i == 0 ? 330f : 150f;
                RoomChoiceButton(left, $"Sayi_{RoomSizeLabels[i]}", RoomSizeLabels[i], topLeft, new Vector2(x, -60f),
                    new Vector2(width, 96f), sizes.GetArrayElementAtIndex(i), RoomSizes[i]);
                x += width + 20f;
            }

            TextMeshProUGUI teamTitle = AddText(left, "TakimBoyuBaslik", "TAKIM BOYU", 38f, Gold, TextAlignmentOptions.Left, shadow: true);
            Place(teamTitle.rectTransform, topLeft, topLeft, new Vector2(0f, -180f), new Vector2(700f, 50f));

            SerializedProperty teams = serialized.FindProperty("teamSizeChoices");
            teams.arraySize = RoomTeamSizes.Length;
            for (int i = 0; i < RoomTeamSizes.Length; i++)
                RoomChoiceButton(left, $"Takim_{RoomTeamSizes[i]}", RoomTeamLabels[i], topLeft, new Vector2(i * 320f, -240f),
                    new Vector2(300f, 96f), teams.GetArrayElementAtIndex(i), RoomTeamSizes[i]);

            TextMeshProUGUI info = AddText(left, "Bilgi", FriendRoomPopup.InfoFor(8, 2), 30f, Color.white, TextAlignmentOptions.TopLeft);
            info.textWrappingMode = TextWrappingModes.Normal;
            Place(info.rectTransform, topLeft, topLeft, new Vector2(0f, -360f), new Vector2(980f, 96f));

            Button create = AddButton(left, "OdaKurButonu", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f),
                new Vector2(440f, 124f), Green, GreenLip, lipHeight: 10f);
            TextMeshProUGUI createLabel = AddText(Face(create), "Yazi", "ODA KUR", 60f, Color.white, TextAlignmentOptions.Center, shadow: true);
            Stretch(createLabel.rectTransform, 12f, 12f, 0f, 4f);

            // Ayrac
            RectTransform divider = NewRect("Ayrac", window);
            divider.anchorMin = new Vector2(0.61f, 0.08f);
            divider.anchorMax = new Vector2(0.61f, 0.74f);
            divider.sizeDelta = new Vector2(4f, 0f);
            AddImage(divider, null, new Color(1f, 1f, 1f, 0.18f));

            // Sag: kodla katil.
            RectTransform right = NewRect("KodlaKatil", window);
            right.anchorMin = new Vector2(0.62f, 0f);
            right.anchorMax = Vector2.one;
            right.offsetMin = new Vector2(20f, 36f);
            right.offsetMax = new Vector2(-40f, -190f);

            Vector2 top = new(0.5f, 1f);
            TextMeshProUGUI joinTitle = AddText(right, "Baslik", "KODLA KATIL", 38f, Gold, TextAlignmentOptions.Center, shadow: true);
            Place(joinTitle.rectTransform, top, top, Vector2.zero, new Vector2(600f, 50f));
            TextMeshProUGUI joinHint = AddText(right, "Aciklama", "Arkadaşının söylediği kodu yaz", 30f, Color.white, TextAlignmentOptions.Center);
            Place(joinHint.rectTransform, top, top, new Vector2(0f, -60f), new Vector2(600f, 44f));

            TMP_InputField input = CreateInputField(right, "KodKutusu", "Kod");
            Place((RectTransform)input.transform, top, top, new Vector2(0f, -150f), new Vector2(380f, 120f));

            Button join = AddButton(right, "KatilButonu", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f),
                new Vector2(340f, 124f), Blue, BlueLip, lipHeight: 10f);
            TextMeshProUGUI joinLabel = AddText(Face(join), "Yazi", "KATIL", 56f, Color.white, TextAlignmentOptions.Center, shadow: true);
            Stretch(joinLabel.rectTransform, 12f, 12f, 0f, 4f);

            serialized.FindProperty("createButton").objectReferenceValue = create;
            serialized.FindProperty("codeInput").objectReferenceValue = input;
            serialized.FindProperty("joinButton").objectReferenceValue = join;
            serialized.FindProperty("hintText").objectReferenceValue = hint;
            serialized.FindProperty("closeButton").objectReferenceValue = close;
            serialized.FindProperty("menu").objectReferenceValue = menu;
            serialized.FindProperty("infoText").objectReferenceValue = info;
            serialized.FindProperty("group").objectReferenceValue = group;
            serialized.FindProperty("window").objectReferenceValue = window;
            serialized.FindProperty("layoutVersion").intValue = TeamPopupLayoutVersion;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            root.gameObject.SetActive(false);
            return popupObject;
        }

        /// <summary>Secenek butonu (mor; secilince yesil) ve Choice kaydi.</summary>
        private static Button RoomChoiceButton(RectTransform parent, string name, string text, Vector2 anchor, Vector2 position,
            Vector2 size, SerializedProperty item, int value)
        {
            Button button = AddButton(parent, name, anchor, anchor, position, size, RoomChoiceFace, RoomChoiceLip, lipHeight: 6f);
            TextMeshProUGUI label = AddText(Face(button), "Yazi", text, 40f, Color.white, TextAlignmentOptions.Center, shadow: true);
            Stretch(label.rectTransform, 10f, 10f, 0f, 4f);

            item.FindPropertyRelative("value").intValue = value;
            item.FindPropertyRelative("button").objectReferenceValue = button;
            item.FindPropertyRelative("face").objectReferenceValue = Face(button).GetComponent<Image>();
            item.FindPropertyRelative("label").objectReferenceValue = label;
            return button;
        }

        // ------------------------------------------------------------------
        // TAKIM ODASI ekrani (macta, harita seciminden once)
        // ------------------------------------------------------------------

        private static bool EnsureTeamRoomScreen(Canvas canvas, bool rebuild)
        {
            TeamRoomScreen existing = canvas.GetComponentInChildren<TeamRoomScreen>(true);
            if (existing != null && !rebuild)
                return false;

            if (existing != null)
                Undo.DestroyObjectImmediate(existing.gameObject);

            BuildTeamRoomScreen(canvas.transform);
            return true;
        }

        private static void BuildTeamRoomScreen(Transform canvas)
        {
            RectTransform root = NewRect(TeamRoomScreenName, canvas);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Takim odasi ekrani");
            Stretch(root, 0f, 0f, 0f, 0f);

            // Harita secim ekraninin (900) ustunde, yukleme ekraninin altinda.
            Canvas screenCanvas = root.gameObject.AddComponent<Canvas>();
            screenCanvas.overrideSorting = true;
            screenCanvas.sortingOrder = 905;
            root.gameObject.AddComponent<GraphicRaycaster>();

            RectTransform background = NewRect("Arkaplan", root);
            Stretch(background, 0f, 0f, 0f, 0f);
            AddImage(background, Sprite("bg_ocean"), Color.white, raycast: true).type = Image.Type.Simple;
            RectTransform dim = NewRect("Karartma", root);
            Stretch(dim, 0f, 0f, 0f, 0f);
            AddImage(dim, null, new Color(0.08f, 0.05f, 0.18f, 0.55f));

            Vector2 topLeft = new(0f, 1f);
            Vector2 top = new(0.5f, 1f);
            Vector2 topRight = Vector2.one;

            // Ust cubuk
            Button leave = AddButton(root, "AyrilButonu", topLeft, topLeft, new Vector2(30f, -24f), new Vector2(240f, 100f), ExitRed, ExitRedLip);
            TextMeshProUGUI leaveLabel = AddText(Face(leave), "Yazi", "AYRIL", 46f, Color.white, TextAlignmentOptions.Center, shadow: true);
            Stretch(leaveLabel.rectTransform, 10f, 10f, 0f, 4f);

            TextMeshProUGUI title = AddText(root, "Baslik", "TAKIM ODASI", 64f, Color.white, TextAlignmentOptions.Left, shadow: true);
            Place(title.rectTransform, topLeft, topLeft, new Vector2(300f, -24f), new Vector2(520f, 100f));

            TextMeshProUGUI code = AddText(root, "OdaKodu", "ODA KODU: 1234", 76f, Gold, TextAlignmentOptions.Center, shadow: true);
            Place(code.rectTransform, top, top, new Vector2(0f, -20f), new Vector2(720f, 110f));

            TextMeshProUGUI count = AddText(root, "OyuncuSayisi", "1 / 8 OYUNCU", 50f, Color.white, TextAlignmentOptions.Right, shadow: true);
            Place(count.rectTransform, topRight, topRight, new Vector2(-40f, -30f), new Vector2(440f, 90f));

            // Sol: kurucu ayarlari
            RectTransform settings = NewRect("Ayarlar", root);
            settings.anchorMin = new Vector2(0f, 0.14f);
            settings.anchorMax = new Vector2(0.29f, 0.86f);
            settings.offsetMin = new Vector2(30f, 0f);
            settings.offsetMax = new Vector2(-10f, 0f);
            AddImage(settings, Sprite("ui_round"), ResultCard, raycast: true).pixelsPerUnitMultiplier = 1.2f;

            var screen = root.gameObject.AddComponent<TeamRoomScreen>();
            var serialized = new SerializedObject(screen);

            TextMeshProUGUI sizeTitle = AddText(settings, "OyuncuSayisiBaslik", "OYUNCU SAYISI", 38f, Gold, TextAlignmentOptions.Center, shadow: true);
            Place(sizeTitle.rectTransform, top, top, new Vector2(0f, -20f), new Vector2(480f, 50f));

            SerializedProperty sizes = serialized.FindProperty("sizeChoices");
            sizes.arraySize = RoomSizes.Length;
            for (int i = 0; i < RoomSizes.Length; i++)
            {
                Vector2 position = new(i % 2 == 0 ? -125f : 125f, i < 2 ? -140f : -255f);
                RoomChoiceButton(settings, $"Sayi_{RoomSizeLabels[i]}", RoomSizeLabels[i], top, position,
                    new Vector2(230f, 96f), sizes.GetArrayElementAtIndex(i), RoomSizes[i]);
            }

            TextMeshProUGUI teamTitle = AddText(settings, "TakimBoyuBaslik", "TAKIM BOYU", 38f, Gold, TextAlignmentOptions.Center, shadow: true);
            Place(teamTitle.rectTransform, top, top, new Vector2(0f, -375f), new Vector2(480f, 50f));

            SerializedProperty teamSizes = serialized.FindProperty("teamSizeChoices");
            teamSizes.arraySize = RoomTeamSizes.Length;
            for (int i = 0; i < RoomTeamSizes.Length; i++)
                RoomChoiceButton(settings, $"Takim_{RoomTeamSizes[i]}", RoomTeamLabels[i], top,
                    new Vector2(i == 0 ? -125f : 125f, -440f), new Vector2(230f, 96f), teamSizes.GetArrayElementAtIndex(i), RoomTeamSizes[i]);

            TextMeshProUGUI note = AddText(settings, "KurucuNotu", "Ayarları oda kurucusu seçer", 28f, ResultSoftText, TextAlignmentOptions.Center);
            Place(note.rectTransform, top, top, new Vector2(0f, -555f), new Vector2(480f, 40f));

            TextMeshProUGUI info = AddText(settings, "Bilgi", FriendRoomPopup.InfoFor(8, 2), 28f, Color.white, TextAlignmentOptions.Bottom);
            info.textWrappingMode = TextWrappingModes.Normal;
            info.rectTransform.anchorMin = Vector2.zero;
            info.rectTransform.anchorMax = new Vector2(1f, 0f);
            info.rectTransform.pivot = new Vector2(0.5f, 0f);
            info.rectTransform.offsetMin = new Vector2(24f, 20f);
            info.rectTransform.offsetMax = new Vector2(-24f, 150f);

            // Sag: takim kartlari
            RectTransform teams = NewRect("Takimlar", root);
            teams.anchorMin = new Vector2(0.3f, 0.14f);
            teams.anchorMax = new Vector2(1f, 0.86f);
            teams.offsetMin = new Vector2(10f, 0f);
            teams.offsetMax = new Vector2(-30f, 0f);
            GridLayoutGroup grid = teams.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(310f, 340f);
            grid.spacing = new Vector2(18f, 18f);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;

            TeamRoomCard template = BuildTeamCardTemplate(teams);

            // Alt: durum + HAZIR / BASLAT
            TextMeshProUGUI status = AddText(root, "Durum", "Takımını seç ve HAZIR'a bas", 38f, Color.white, TextAlignmentOptions.Left, shadow: true);
            status.rectTransform.anchorMin = Vector2.zero;
            status.rectTransform.anchorMax = new Vector2(0.62f, 0.13f);
            status.rectTransform.offsetMin = new Vector2(40f, 10f);
            status.rectTransform.offsetMax = new Vector2(0f, -10f);

            Vector2 bottomRight = new(1f, 0f);
            Button ready = AddButton(root, "HazirButonu", bottomRight, bottomRight, new Vector2(-40f, 24f), new Vector2(460f, 110f), Green, GreenLip, lipHeight: 10f);
            TextMeshProUGUI readyLabel = AddText(Face(ready), "Yazi", "HAZIR", 56f, Color.white, TextAlignmentOptions.Center, shadow: true);
            Stretch(readyLabel.rectTransform, 12f, 12f, 0f, 4f);

            Button start = AddButton(root, "BaslatButonu", bottomRight, bottomRight, new Vector2(-40f, 24f), new Vector2(460f, 110f), Yellow, YellowLip, lipHeight: 10f);
            TextMeshProUGUI startLabel = AddText(Face(start), "Yazi", "BAŞLAT", 60f, Color.white, TextAlignmentOptions.Center, shadow: true);
            Stretch(startLabel.rectTransform, 12f, 12f, 0f, 4f);

            serialized.FindProperty("screenCanvas").objectReferenceValue = screenCanvas;
            serialized.FindProperty("codeText").objectReferenceValue = code;
            serialized.FindProperty("playerCountText").objectReferenceValue = count;
            serialized.FindProperty("infoText").objectReferenceValue = info;
            serialized.FindProperty("settingsNote").objectReferenceValue = note;
            serialized.FindProperty("teamsRoot").objectReferenceValue = teams;
            serialized.FindProperty("cardTemplate").objectReferenceValue = template;
            serialized.FindProperty("readyButton").objectReferenceValue = ready;
            serialized.FindProperty("readyFace").objectReferenceValue = Face(ready).GetComponent<Image>();
            serialized.FindProperty("readyLabel").objectReferenceValue = readyLabel;
            serialized.FindProperty("startButton").objectReferenceValue = start;
            serialized.FindProperty("leaveButton").objectReferenceValue = leave;
            serialized.FindProperty("statusText").objectReferenceValue = status;
            serialized.FindProperty("layoutVersion").intValue = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            screenCanvas.enabled = false;
        }

        private static TeamRoomCard BuildTeamCardTemplate(RectTransform teams)
        {
            RectTransform card = NewRect("TakimSablonu", teams);
            card.sizeDelta = new Vector2(310f, 340f);
            Image background = AddImage(card, Sprite("ui_round"), ResultCard, raycast: true);
            background.pixelsPerUnitMultiplier = 1.4f;
            Button button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.None; // renkleri TeamRoomScreen boyar
            card.gameObject.AddComponent<UIPressScale>();

            TextMeshProUGUI title = AddText(card, "Baslik", "TAKIM 1", 40f, Gold, TextAlignmentOptions.Center, shadow: true);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = Vector2.one;
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.offsetMin = new Vector2(12f, -64f);
            title.rectTransform.offsetMax = new Vector2(-12f, -6f);

            TeamRoomCard view = card.gameObject.AddComponent<TeamRoomCard>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("button").objectReferenceValue = button;
            serialized.FindProperty("background").objectReferenceValue = background;
            serialized.FindProperty("title").objectReferenceValue = title;

            SerializedProperty slots = serialized.FindProperty("slots");
            slots.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                RectTransform slot = NewRect($"Oyuncu{i + 1}", card);
                slot.anchorMin = new Vector2(0f, 1f);
                slot.anchorMax = Vector2.one;
                slot.pivot = new Vector2(0.5f, 1f);
                slot.offsetMin = new Vector2(14f, -70f - i * 66f - 58f);
                slot.offsetMax = new Vector2(-14f, -70f - i * 66f);
                AddImage(slot, Sprite("ui_round"), new Color(0f, 0f, 0f, 0.28f)).pixelsPerUnitMultiplier = 2.4f;

                Image crown = AddIcon(slot, "Kurucu", "icon_crown", Gold, new Vector2(0f, 0.5f), new Vector2(28f, 0f), 34f);
                TextMeshProUGUI name = AddText(slot, "Ad", "Boş", 32f, Color.white, TextAlignmentOptions.Left);
                Stretch(name.rectTransform, 52f, 52f, 0f, 0f);
                Image check = AddIcon(slot, "Hazir", "icon_check", new Color(1f, 1f, 1f, 0.15f), new Vector2(1f, 0.5f), new Vector2(-28f, 0f), 38f);

                SerializedProperty item = slots.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("root").objectReferenceValue = slot.gameObject;
                item.FindPropertyRelative("nameText").objectReferenceValue = name;
                item.FindPropertyRelative("readyIcon").objectReferenceValue = check;
                item.FindPropertyRelative("hostIcon").objectReferenceValue = crown.gameObject;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            card.gameObject.SetActive(false);
            return view;
        }
    }
}
#endif
