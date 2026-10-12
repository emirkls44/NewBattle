#if UNITY_EDITOR
using NewBattle.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// Ana menu butonlarinin actigi pencereler: isim degistirme, ayarlar,
    /// seviye yolu. Ayrica karaktere dokunmak karakter ekranini acar.
    /// Sadece eksik olani ekler.
    /// </summary>
    public static partial class MainMenuBuilder
    {
        private static bool EnsureMenuScreens(MainMenuScreen menu, out string summary)
        {
            var added = new System.Collections.Generic.List<string>();
            var serialized = new SerializedObject(menu);
            var root = (RectTransform)menu.transform;

            SerializedProperty nameProperty = serialized.FindProperty("namePopup");
            if (nameProperty.objectReferenceValue == null)
            {
                nameProperty.objectReferenceValue = Existing<NamePopup>(menu) ?? (Object)BuildNamePopup(root, menu);
                added.Add("İsim penceresi");
            }

            SerializedProperty settingsProperty = serialized.FindProperty("settingsScreen");
            if (settingsProperty.objectReferenceValue is SettingsScreen existingSettings && UpgradeSettingsLayout(existingSettings))
                added.Add("Ayarlar: kamera yakınlığı kaldırıldı");

            if (settingsProperty.objectReferenceValue == null)
            {
                settingsProperty.objectReferenceValue = Existing<SettingsScreen>(menu) ??
                                                       (Object)BuildSettingsScreen(root, (NamePopup)nameProperty.objectReferenceValue);
                added.Add("Ayarlar");
            }

            if (settingsProperty.objectReferenceValue is SettingsScreen musicSettings && UpgradeSettingsMusic(musicSettings))
                added.Add("Ayarlar: müzik satırı");

            SerializedProperty roadProperty = serialized.FindProperty("levelRoadScreen");
            if (roadProperty.objectReferenceValue == null)
            {
                roadProperty.objectReferenceValue = Existing<LevelRoadScreen>(menu) ?? (Object)BuildLevelRoadScreen(root, menu);
                added.Add("Seviye Yolu");
            }

            SerializedProperty profileProperty = serialized.FindProperty("profileScreen");
            if (profileProperty.objectReferenceValue == null)
            {
                profileProperty.objectReferenceValue = Existing<ProfileScreen>(menu) ?? (Object)BuildProfileScreen(root);
                added.Add("Profil");
            }

            SerializedProperty challengesProperty = serialized.FindProperty("challengesScreen");
            if (challengesProperty.objectReferenceValue == null)
            {
                challengesProperty.objectReferenceValue = Existing<ChallengesScreen>(menu) ?? (Object)BuildChallengesScreen(root, menu);
                added.Add("Mücadeleler");
            }

            if (EnsureChallengeCardRefs(menu, serialized))
                added.Add("Mücadeleler kartı sayaçları");

            serialized.ApplyModifiedProperties();

            if (nameProperty.objectReferenceValue is NamePopup namePopup && EnsureNamePopupRefs(namePopup))
                added.Add("İsim penceresi (ilk açılış)");

            if (EnsureCharacterTap(menu))
                added.Add("Karaktere dokununca karakter ekranı");

            summary = string.Join(", ", added);
            return added.Count > 0;
        }

        /// <summary>Menudeki karaktere dokununca karakter ekrani acilsin (surukleyerek dondurme bozulmaz).</summary>
        private static bool EnsureCharacterTap(MainMenuScreen menu)
        {
            MenuCharacterStage stage = menu.GetComponentInChildren<MenuCharacterStage>(true);
            if (stage == null || stage.GetComponent<MenuButton>() != null)
                return false;

            Graphic graphic = stage.GetComponent<Graphic>();
            if (graphic == null)
                return false;

            Undo.RecordObject(graphic, "Karaktere dokunma");
            graphic.raycastTarget = true;

            if (!stage.TryGetComponent(out Button button))
                button = Undo.AddComponent<Button>(stage.gameObject);
            button.transition = Selectable.Transition.None;
            button.targetGraphic = graphic;

            MenuButton action = Undo.AddComponent<MenuButton>(stage.gameObject);
            SetActionId(new SerializedObject(action), "karakter");
            return true;
        }

        // ------------------------------------------------------------------
        // Isim penceresi
        // ------------------------------------------------------------------

        private static NamePopup BuildNamePopup(RectTransform menuRoot, MainMenuScreen menu)
        {
            (RectTransform root, RectTransform window) = PopupShell(menuRoot, "IsimPenceresi", "İSMİNİ DEĞİŞTİR", new Vector2(960f, 560f));

            GameObject inputObject = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
            inputObject.name = "IsimKutusu";
            SetLayerRecursive(inputObject.transform, 5);
            var inputRect = (RectTransform)inputObject.transform;
            inputRect.SetParent(window, false);
            Place(inputRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 50f), new Vector2(780f, 120f));

            Image inputImage = inputObject.GetComponent<Image>();
            inputImage.sprite = Sprite("ui_round");
            inputImage.type = Image.Type.Sliced;
            inputImage.color = new Color(1f, 0.97f, 0.9f, 1f);

            TMP_InputField input = inputObject.GetComponent<TMP_InputField>();
            input.characterLimit = NamePopup.MaxLength;
            input.lineType = TMP_InputField.LineType.SingleLine;
            foreach (TextMeshProUGUI text in inputObject.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                text.font = _font;
                text.fontSharedMaterial = _plainMaterial;
                text.fontSize = 56f;
                text.alignment = TextAlignmentOptions.Center;
                text.color = text == input.placeholder ? new Color(BrownText.r, BrownText.g, BrownText.b, 0.45f) : BrownDark;
            }

            if (input.placeholder is TextMeshProUGUI placeholder)
                placeholder.text = "İsmini yaz";

            TextMeshProUGUI hint = AddText(window, "Ipucu", "En az 3, en fazla 16 harf", 32f, ResultSoftText, TextAlignmentOptions.Center);
            Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -45f), new Vector2(780f, 50f));

            (Button cancel, Button save) = PopupButtons(window, "VAZGEÇ", "KAYDET");

            NamePopup popup = root.gameObject.AddComponent<NamePopup>();
            var serialized = new SerializedObject(popup);
            serialized.FindProperty("input").objectReferenceValue = input;
            serialized.FindProperty("saveButton").objectReferenceValue = save;
            serialized.FindProperty("cancelButton").objectReferenceValue = cancel;
            serialized.FindProperty("hintText").objectReferenceValue = hint;
            serialized.FindProperty("menu").objectReferenceValue = menu;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            root.gameObject.SetActive(false);
            return popup;
        }

        // ------------------------------------------------------------------
        // Ayarlar
        // ------------------------------------------------------------------

        private static SettingsScreen BuildSettingsScreen(RectTransform menuRoot, NamePopup namePopup)
        {
            (RectTransform root, RectTransform window) = PopupShell(menuRoot, "AyarlarEkrani", "AYARLAR", new Vector2(1160f, 730f));

            Button close = AddButton(window, "KapatButonu", Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(-20f, -20f),
                new Vector2(100f, 100f), Orange, OrangeLip);
            AddIcon(Face(close), "Ikon", "icon_close", Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, 54f);

            SettingsRowLabel(window, "SesYazisi", "SES", -200f);
            Button sound = SettingsButton(window, "SesButonu", "AÇIK", -200f, Green, GreenLip, out TextMeshProUGUI soundLabel);

            SettingsRowLabel(window, "KareHiziYazisi", "KARE HIZI", -330f);
            Button frameRate = SettingsButton(window, "KareHiziButonu", "60 FPS", -330f, Blue, BlueLip, out TextMeshProUGUI frameRateLabel);

            SettingsRowLabel(window, "IsimYazisi", "İSİM", -460f);
            Button nameButton = SettingsButton(window, "IsimButonu", "DEĞİŞTİR", -460f, Blue, BlueLip, out _);

            TextMeshProUGUI version = AddText(window, "Surum", "Sürüm 0.1", 30f, ResultSoftText, TextAlignmentOptions.Left);
            Place(version.rectTransform, Vector2.zero, Vector2.zero, new Vector2(60f, 36f), new Vector2(500f, 44f));

            SettingsScreen screen = root.gameObject.AddComponent<SettingsScreen>();
            var serialized = new SerializedObject(screen);
            serialized.FindProperty("soundButton").objectReferenceValue = sound;
            serialized.FindProperty("soundFace").objectReferenceValue = Face(sound).GetComponent<Image>();
            serialized.FindProperty("soundLabel").objectReferenceValue = soundLabel;
            serialized.FindProperty("frameRateButton").objectReferenceValue = frameRate;
            serialized.FindProperty("frameRateLabel").objectReferenceValue = frameRateLabel;
            serialized.FindProperty("nameButton").objectReferenceValue = nameButton;
            serialized.FindProperty("namePopup").objectReferenceValue = namePopup;
            serialized.FindProperty("versionText").objectReferenceValue = version;
            serialized.FindProperty("closeButton").objectReferenceValue = close;
            serialized.FindProperty("layoutVersion").intValue = SettingsLayoutVersion;

            serialized.ApplyModifiedPropertiesWithoutUndo();

            root.gameObject.SetActive(false);
            return screen;
        }

        /// <summary>2: kamera yakinligi satiri kaldirildi (oyuncu degistiremez).</summary>
        private const int SettingsLayoutVersion = 2;

        private static bool UpgradeSettingsLayout(SettingsScreen screen)
        {
            var serialized = new SerializedObject(screen);
            SerializedProperty version = serialized.FindProperty("layoutVersion");
            if (version.intValue >= SettingsLayoutVersion)
                return false;

            Transform window = screen.transform.Find("Pencere");
            if (window != null)
            {
                foreach (string name in new[] { "KameraYazisi", "UzaklastirButonu", "KameraDegeri", "YaklastirButonu" })
                {
                    Transform row = window.Find(name);
                    if (row != null)
                        Undo.DestroyObjectImmediate(row.gameObject);
                }

                // Isim satiri bosalan yere kayar (sen tasimadiysan).
                foreach (string name in new[] { "IsimYazisi", "IsimButonu" })
                {
                    if (window.Find(name) is RectTransform row && Mathf.Approximately(row.anchoredPosition.y, -590f))
                    {
                        Undo.RecordObject(row, "Ayarlar yerlesimi");
                        row.anchoredPosition = new Vector2(row.anchoredPosition.x, -460f);
                    }
                }

                if (window is RectTransform windowRect && windowRect.sizeDelta == new Vector2(1160f, 860f))
                {
                    Undo.RecordObject(windowRect, "Ayarlar yerlesimi");
                    windowRect.sizeDelta = new Vector2(1160f, 730f);
                }
            }

            version.intValue = SettingsLayoutVersion;
            serialized.ApplyModifiedProperties();
            return true;
        }

        private static void SettingsRowLabel(RectTransform window, string name, string text, float y)
        {
            TextMeshProUGUI label = AddText(window, name, text, 50f, Color.white, TextAlignmentOptions.Left, shadow: true);
            Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(70f, y), new Vector2(560f, 80f));
        }

        private static Button SettingsButton(RectTransform window, string name, string text, float y, Color face, Color lip,
            out TextMeshProUGUI label)
        {
            Button button = AddButton(window, name, Vector2.one, new Vector2(1f, 0.5f), new Vector2(-60f, y),
                new Vector2(336f, 96f), face, lip, lipHeight: 7f);
            label = AddText(Face(button), "Yazi", text, 44f, Color.white, TextAlignmentOptions.Center, shadow: true);
            Stretch(label.rectTransform, 10f, 10f, 0f, 3f);
            return button;
        }

        // ------------------------------------------------------------------
        // Seviye Yolu
        // ------------------------------------------------------------------

        private static LevelRoadScreen BuildLevelRoadScreen(RectTransform menuRoot, MainMenuScreen menu)
        {
            RectTransform root = NewRect("SeviyeYoluEkrani", menuRoot);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Seviye yolu");
            Stretch(root, 0f, 0f, 0f, 0f);

            RectTransform background = NewRect("Arkaplan", root);
            Stretch(background, 0f, 0f, 0f, 0f);
            AddImage(background, Sprite("bg_purple"), Color.white, raycast: true).type = Image.Type.Simple;

            Vector2 topLeft = new(0f, 1f);
            TextMeshProUGUI title = AddText(root, "Baslik", "SEVİYE YOLU", 84f, Color.white, TextAlignmentOptions.Left, shadow: true);
            Place(title.rectTransform, topLeft, topLeft, new Vector2(60f, -26f), new Vector2(900f, 100f));

            Button close = AddButton(root, "KapatButonu", Vector2.one, Vector2.one, new Vector2(-40f, -36f),
                new Vector2(100f, 100f), Orange, OrangeLip);
            AddIcon(Face(close), "Ikon", "icon_close", Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, 54f);

            TextMeshProUGUI level = AddText(root, "Seviye", "SEVİYE 1", 60f, Yellow, TextAlignmentOptions.Left, shadow: true);
            Place(level.rectTransform, topLeft, new Vector2(0f, 0.5f), new Vector2(64f, -170f), new Vector2(420f, 74f));
            TextMeshProUGUI rank = AddText(root, "Rutbe", "Er I", 40f, Color.white, TextAlignmentOptions.Left, shadow: true);
            Place(rank.rectTransform, topLeft, new Vector2(0f, 0.5f), new Vector2(64f, -232f), new Vector2(420f, 54f));

            RectTransform track = NewRect("XpBari", root);
            Place(track, topLeft, new Vector2(0f, 0.5f), new Vector2(500f, -190f), new Vector2(760f, 50f));
            AddImage(track, Sprite("ui_round"), ResultTrack).pixelsPerUnitMultiplier = 2.2f;
            RectTransform fill = NewRect("Dolum", track);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0.3f, 1f);
            fill.offsetMin = new Vector2(5f, 5f);
            fill.offsetMax = new Vector2(0f, -5f);
            AddImage(fill, Sprite("ui_round"), Yellow).pixelsPerUnitMultiplier = 2.8f;
            TextMeshProUGUI xp = AddText(track, "XpSayisi", "0 / 200 XP", 32f, Color.white, TextAlignmentOptions.Center, shadow: true);
            Stretch(xp.rectTransform, 0f, 0f, 0f, 2f);

            RectTransform panel = NewRect("ListePaneli", root);
            Stretch(panel, 40f, 40f, 290f, 50f);
            AddImage(panel, Sprite("ui_round"), new Color(0f, 0f, 0f, 0.25f), raycast: true);

            RectTransform viewport = NewRect("Gorunum", panel);
            Stretch(viewport, 20f, 20f, 20f, 20f);
            viewport.gameObject.AddComponent<RectMask2D>();
            AddImage(viewport, null, new Color(1f, 1f, 1f, 0f), raycast: true);

            RectTransform content = NewRect("Icerik", viewport);
            Place(content, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 600f));
            GridLayoutGroup grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(300f, 580f);
            grid.spacing = new Vector2(24f, 0f);
            grid.padding = new RectOffset(10, 10, 10, 10);
            grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
            grid.constraintCount = 1;
            grid.startAxis = GridLayoutGroup.Axis.Vertical;
            content.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;

            LevelRoadCard template = BuildLevelRoadCard(content);

            LevelRoadScreen screen = root.gameObject.AddComponent<LevelRoadScreen>();
            var serialized = new SerializedObject(screen);
            serialized.FindProperty("library").objectReferenceValue = LobbyAnimationSetup.EnsureLibrary();
            serialized.FindProperty("menu").objectReferenceValue = menu;
            serialized.FindProperty("levelText").objectReferenceValue = level;
            serialized.FindProperty("rankText").objectReferenceValue = rank;
            serialized.FindProperty("xpText").objectReferenceValue = xp;
            serialized.FindProperty("xpFill").objectReferenceValue = fill;
            serialized.FindProperty("closeButton").objectReferenceValue = close;
            serialized.FindProperty("scroll").objectReferenceValue = scroll;
            serialized.FindProperty("listContent").objectReferenceValue = content;
            serialized.FindProperty("cardTemplate").objectReferenceValue = template;
            serialized.FindProperty("coinIcon").objectReferenceValue = Sprite("coin");
            serialized.FindProperty("animationIcon").objectReferenceValue = Sprite("icon_star");
            serialized.ApplyModifiedPropertiesWithoutUndo();

            root.gameObject.SetActive(false);
            return screen;
        }

        private static LevelRoadCard BuildLevelRoadCard(RectTransform content)
        {
            RectTransform card = NewRect("KartSablonu", content);
            Image background = AddImage(card, Sprite("ui_round"), new Color(1f, 0.95f, 0.82f, 1f), raycast: true);
            background.pixelsPerUnitMultiplier = 1.4f;
            Button button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            card.gameObject.AddComponent<UIPressScale>();

            RectTransform outline = NewRect("Cerceve", card);
            Stretch(outline, -7f, -7f, -7f, -7f);
            AddImage(outline, Sprite("ui_round_outline"), Yellow).pixelsPerUnitMultiplier = 1.4f;
            outline.gameObject.SetActive(false);

            TextMeshProUGUI level = AddText(card, "Seviye", "SEVİYE 2", 44f, BrownDark, TextAlignmentOptions.Center);
            Place(level.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(280f, 60f));
            TextMeshProUGUI rank = AddText(card, "Rutbe", "Er II", 30f, BrownText, TextAlignmentOptions.Center);
            Place(rank.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(280f, 44f));

            Image icon = AddIcon(card, "Ikon", "coin", Color.white, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), 150f);

            TextMeshProUGUI reward = AddText(card, "Odul", "+100 Para", 36f, BrownDark, TextAlignmentOptions.Center);
            reward.textWrappingMode = TextWrappingModes.Normal;
            reward.lineSpacing = -10f;
            Place(reward.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(280f, 160f));

            Image check = AddIcon(card, "Alindi", "icon_check", Green, Vector2.one, new Vector2(-36f, -36f), 60f);
            check.gameObject.SetActive(false);

            LevelRoadCard component = card.gameObject.AddComponent<LevelRoadCard>();
            var serialized = new SerializedObject(component);
            serialized.FindProperty("background").objectReferenceValue = background;
            serialized.FindProperty("levelText").objectReferenceValue = level;
            serialized.FindProperty("rankText").objectReferenceValue = rank;
            serialized.FindProperty("rewardIcon").objectReferenceValue = icon;
            serialized.FindProperty("rewardText").objectReferenceValue = reward;
            serialized.FindProperty("reachedMark").objectReferenceValue = check.gameObject;
            serialized.FindProperty("nextOutline").objectReferenceValue = outline.gameObject;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return component;
        }

        // ------------------------------------------------------------------
        // Isim penceresi: baslik ve vazgec yazisi (ilk acilis modu icin)
        // ------------------------------------------------------------------

        private static bool EnsureNamePopupRefs(NamePopup popup)
        {
            var serialized = new SerializedObject(popup);
            SerializedProperty title = serialized.FindProperty("titleText");
            SerializedProperty cancelLabel = serialized.FindProperty("cancelLabel");
            if (title.objectReferenceValue != null && cancelLabel.objectReferenceValue != null)
                return false;

            if (title.objectReferenceValue == null)
                title.objectReferenceValue = popup.transform.Find("Pencere/Baslik/Yazi")?.GetComponent<TextMeshProUGUI>();

            if (cancelLabel.objectReferenceValue == null && serialized.FindProperty("cancelButton").objectReferenceValue is Button cancel)
                cancelLabel.objectReferenceValue = cancel.transform.Find("Yuz/Yazi")?.GetComponent<TextMeshProUGUI>();

            serialized.ApplyModifiedProperties();
            return true;
        }

        // ------------------------------------------------------------------
        // Profil
        // ------------------------------------------------------------------

        private static ProfileScreen BuildProfileScreen(RectTransform menuRoot)
        {
            (RectTransform root, RectTransform window) = PopupShell(menuRoot, "ProfilEkrani", "PROFİL", new Vector2(1240f, 800f));
            Button close = PopupCloseButton(window);

            TextMeshProUGUI name = AddText(window, "Isim", "Savaşçı", 64f, Yellow, TextAlignmentOptions.Center, shadow: true);
            Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(1000f, 80f));
            TextMeshProUGUI level = AddText(window, "Seviye", "SEVİYE 1 · Er I", 40f, ResultSoftText, TextAlignmentOptions.Center);
            Place(level.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -240f), new Vector2(1000f, 56f));

            TextMeshProUGUI StatBox(string objectName, string label, float x, float y)
            {
                RectTransform box = NewRect(objectName, window);
                Place(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(340f, 200f));
                AddImage(box, Sprite("ui_round"), ResultCardDim).pixelsPerUnitMultiplier = 1.6f;
                TextMeshProUGUI value = AddText(box, "Deger", "0", 76f, ResultOrange, TextAlignmentOptions.Center, shadow: true);
                Place(value.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(320f, 100f));
                TextMeshProUGUI caption = AddText(box, "Yazi", label, 32f, Color.white, TextAlignmentOptions.Center);
                Place(caption.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(320f, 50f));
                return value;
            }

            TextMeshProUGUI matches = StatBox("Mac", "Maç", -370f, 10f);
            TextMeshProUGUI wins = StatBox("Galibiyet", "Galibiyet", 0f, 10f);
            TextMeshProUGUI winRate = StatBox("KazanmaOrani", "Kazanma Oranı", 370f, 10f);
            TextMeshProUGUI kills = StatBox("Oldurme", "Öldürme", -370f, -230f);
            TextMeshProUGUI killsPerMatch = StatBox("MacBasiOldurme", "Maç Başı Öldürme", 0f, -230f);
            TextMeshProUGUI best = StatBox("EnIyiSira", "En İyi Sıra", 370f, -230f);

            ProfileScreen screen = root.gameObject.AddComponent<ProfileScreen>();
            var serialized = new SerializedObject(screen);
            serialized.FindProperty("nameText").objectReferenceValue = name;
            serialized.FindProperty("levelText").objectReferenceValue = level;
            serialized.FindProperty("matchesText").objectReferenceValue = matches;
            serialized.FindProperty("winsText").objectReferenceValue = wins;
            serialized.FindProperty("winRateText").objectReferenceValue = winRate;
            serialized.FindProperty("killsText").objectReferenceValue = kills;
            serialized.FindProperty("killsPerMatchText").objectReferenceValue = killsPerMatch;
            serialized.FindProperty("bestPlacementText").objectReferenceValue = best;
            serialized.FindProperty("closeButton").objectReferenceValue = close;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            root.gameObject.SetActive(false);
            return screen;
        }

        // ------------------------------------------------------------------
        // Mucadeleler
        // ------------------------------------------------------------------

        private static ChallengesScreen BuildChallengesScreen(RectTransform menuRoot, MainMenuScreen menu)
        {
            (RectTransform root, RectTransform window) = PopupShell(menuRoot, "MucadelelerEkrani", "MÜCADELELER", new Vector2(1640f, 880f));
            Button close = PopupCloseButton(window);

            RectTransform Column(string prefix, string title, float x, out TextMeshProUGUI timer)
            {
                TextMeshProUGUI heading = AddText(window, prefix + "Baslik", title, 56f, Yellow, TextAlignmentOptions.Center, shadow: true);
                Place(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(x, -170f), new Vector2(740f, 70f));
                timer = AddText(window, prefix + "Sure", "Yenilenme: 5 sa 20 dk", 30f, ResultSoftText, TextAlignmentOptions.Center);
                Place(timer.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(x, -222f), new Vector2(740f, 44f));

                RectTransform list = NewRect(prefix + "Liste", window);
                Place(list, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(x, -260f), new Vector2(740f, 580f));
                GridLayoutGroup grid = list.gameObject.AddComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2(740f, 170f);
                grid.spacing = new Vector2(0f, 20f);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 1;
                return list;
            }

            RectTransform daily = Column("Gunluk", "GÜNLÜK", -400f, out TextMeshProUGUI dailyTimer);
            RectTransform weekly = Column("Haftalik", "HAFTALIK", 400f, out TextMeshProUGUI weeklyTimer);
            ChallengeRow template = BuildChallengeRow(daily);

            ChallengesScreen screen = root.gameObject.AddComponent<ChallengesScreen>();
            var serialized = new SerializedObject(screen);
            serialized.FindProperty("dailyList").objectReferenceValue = daily;
            serialized.FindProperty("weeklyList").objectReferenceValue = weekly;
            serialized.FindProperty("rowTemplate").objectReferenceValue = template;
            serialized.FindProperty("dailyTimerText").objectReferenceValue = dailyTimer;
            serialized.FindProperty("weeklyTimerText").objectReferenceValue = weeklyTimer;
            serialized.FindProperty("closeButton").objectReferenceValue = close;
            serialized.FindProperty("menu").objectReferenceValue = menu;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            root.gameObject.SetActive(false);
            return screen;
        }

        private static ChallengeRow BuildChallengeRow(RectTransform list)
        {
            RectTransform row = NewRect("SatirSablonu", list);
            Image background = AddImage(row, Sprite("ui_round"), new Color(1f, 0.95f, 0.82f, 1f), raycast: true);
            background.pixelsPerUnitMultiplier = 1.6f;
            Button button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            row.gameObject.AddComponent<UIPressScale>();

            Vector2 topLeft = new(0f, 1f);
            TextMeshProUGUI title = AddText(row, "Ad", "3 maç oyna", 40f, BrownDark, TextAlignmentOptions.Left);
            Place(title.rectTransform, topLeft, topLeft, new Vector2(28f, -18f), new Vector2(480f, 56f));

            TextMeshProUGUI reward = AddText(row, "Odul", "+80 XP", 40f, GreenLip, TextAlignmentOptions.Right);
            Place(reward.rectTransform, Vector2.one, Vector2.one, new Vector2(-28f, -18f), new Vector2(200f, 56f));

            RectTransform track = NewRect("Cubuk", row);
            Place(track, Vector2.zero, Vector2.zero, new Vector2(28f, 30f), new Vector2(460f, 40f));
            AddImage(track, Sprite("ui_round"), ResultTrack).pixelsPerUnitMultiplier = 2.2f;
            RectTransform fill = NewRect("Dolum", track);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0.3f, 1f);
            fill.offsetMin = new Vector2(4f, 4f);
            fill.offsetMax = new Vector2(0f, -4f);
            AddImage(fill, Sprite("ui_round"), Yellow).pixelsPerUnitMultiplier = 2.8f;

            TextMeshProUGUI progress = AddText(row, "Ilerleme", "1 / 3", 34f, BrownText, TextAlignmentOptions.Left);
            Place(progress.rectTransform, Vector2.zero, new Vector2(0f, 0.5f), new Vector2(508f, 50f), new Vector2(200f, 50f));

            RectTransform done = NewRect("Tamamlandi", row);
            Place(done, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(250f, 60f));
            AddImage(done, Sprite("ui_round"), Green).pixelsPerUnitMultiplier = 2.4f;
            TextMeshProUGUI doneText = AddText(done, "Yazi", "TAMAMLANDI", 30f, Color.white, TextAlignmentOptions.Center, shadow: true);
            Stretch(doneText.rectTransform, 8f, 8f, 0f, 3f);
            done.gameObject.SetActive(false);

            ChallengeRow component = row.gameObject.AddComponent<ChallengeRow>();
            var serialized = new SerializedObject(component);
            serialized.FindProperty("background").objectReferenceValue = background;
            serialized.FindProperty("titleText").objectReferenceValue = title;
            serialized.FindProperty("progressText").objectReferenceValue = progress;
            serialized.FindProperty("progressFill").objectReferenceValue = fill;
            serialized.FindProperty("rewardText").objectReferenceValue = reward;
            serialized.FindProperty("doneMark").objectReferenceValue = done.gameObject;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return component;
        }

        /// <summary>Menudeki MÜCADELELER kartinin yazilarini ve kutularini baglar.</summary>
        private static bool EnsureChallengeCardRefs(MainMenuScreen menu, SerializedObject serialized)
        {
            SerializedProperty dailyText = serialized.FindProperty("challengeDailyText");
            if (dailyText.objectReferenceValue != null)
                return false;

            Transform card = menu.transform.Find("OrtaAlan/MucadelelerKarti");
            if (card == null)
                return false;

            dailyText.objectReferenceValue = card.Find("Gunluk")?.GetComponent<TextMeshProUGUI>();
            serialized.FindProperty("challengeWeeklyText").objectReferenceValue = card.Find("Haftalik")?.GetComponent<TextMeshProUGUI>();
            serialized.FindProperty("challengeDailyBox").objectReferenceValue = card.Find("GunlukKutu")?.GetComponent<Image>();
            Transform weeklyBox = card.Find("HaftalıkKutu");
            if (weeklyBox == null)
                weeklyBox = card.Find("HaftalikKutu");
            serialized.FindProperty("challengeWeeklyBox").objectReferenceValue = weeklyBox != null ? weeklyBox.GetComponent<Image>() : null;
            Transform comingSoon = card.Find("Yakinda");
            serialized.FindProperty("challengeComingSoon").objectReferenceValue = comingSoon != null ? comingSoon.gameObject : null;
            return true;
        }

        /// <summary>Pencerenin sag ust kosesinde turuncu kapat butonu.</summary>
        private static Button PopupCloseButton(RectTransform window)
        {
            Button close = AddButton(window, "KapatButonu", Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(-20f, -20f),
                new Vector2(100f, 100f), Orange, OrangeLip);
            AddIcon(Face(close), "Ikon", "icon_close", Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, 54f);
            return close;
        }

        // ------------------------------------------------------------------
        // Ortak pencere parcalari
        // ------------------------------------------------------------------

        /// <summary>Karartma + ortada baslikli pencere.</summary>
        private static (RectTransform root, RectTransform window) PopupShell(RectTransform menuRoot, string name, string title, Vector2 size)
        {
            RectTransform root = NewRect(name, menuRoot);
            Undo.RegisterCreatedObjectUndo(root.gameObject, name);
            Stretch(root, 0f, 0f, 0f, 0f);

            RectTransform dim = NewRect("Karartma", root);
            Stretch(dim, 0f, 0f, 0f, 0f);
            AddImage(dim, null, new Color(0f, 0f, 0f, 0.65f), raycast: true);

            RectTransform window = NewRect("Pencere", root);
            Place(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            AddImage(window, Sprite("ui_round"), ResultCard, raycast: true).pixelsPerUnitMultiplier = 1.2f;
            CardHeader(window, title, Orange, Color.white, 64f, shadow: true, inset: 0f, height: 110f);
            return (root, window);
        }

        /// <summary>Pencerenin altinda iki buton: solda vazgec (turuncu), sagda onay (yesil).</summary>
        private static (Button no, Button yes) PopupButtons(RectTransform window, string noText, string yesText)
        {
            Button no = AddButton(window, "VazgecButonu", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-200f, 44f), new Vector2(340f, 112f), Orange, OrangeLip);
            TextMeshProUGUI noLabel = AddText(Face(no), "Yazi", noText, 50f, Color.white, TextAlignmentOptions.Center, shadow: true);
            Stretch(noLabel.rectTransform, 12f, 12f, 0f, 4f);

            Button yes = AddButton(window, "OnayButonu", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(200f, 44f), new Vector2(340f, 112f), Green, GreenLip);
            TextMeshProUGUI yesLabel = AddText(Face(yes), "Yazi", yesText, 50f, Color.white, TextAlignmentOptions.Center, shadow: true);
            Stretch(yesLabel.rectTransform, 12f, 12f, 0f, 4f);
            return (no, yes);
        }

        /// <summary>Menude zaten varsa onu dondurur, yoksa gercek null (?? ile guvenli).</summary>
        private static Object Existing<T>(MainMenuScreen menu) where T : Component
        {
            T found = menu.GetComponentInChildren<T>(true);
            return found != null ? found : null;
        }

        private static void SetLayerRecursive(Transform target, int layer)
        {
            target.gameObject.layer = layer;
            foreach (Transform child in target)
                SetLayerRecursive(child, layer);
        }
    }
}
#endif
