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
    /// Takim odasi = onaylanan sonbahar taslagi B: orman arka plani, ustte agactan sarkan ahsap
    /// tabelada oda kodu, solda mor "ODA AYARLARI" panosu (yesil secili cipler, altta HAZIR),
    /// sagda 2x2 parsomen takim kartlari (oyuncular ahsap plakalarda), sag altta turuncu BASLAT.
    /// Yerlesim taslagin 1280x720 olcusune gore oransal. Derleyince bir kez uygulanir.
    /// </summary>
    public static partial class MainMenuBuilder
    {
        [InitializeOnLoadMethod]
        private static void AutoApplyAutumnTeamRoomLater()
        {
            EditorApplication.delayCall += AutoApplyAutumnTeamRoom;
        }

        private static void AutoApplyAutumnTeamRoom()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AutoApplyAutumnTeamRoom;
                return;
            }

            TeamRoomScreen screen = Object.FindFirstObjectByType<TeamRoomScreen>(FindObjectsInactive.Include);
            if (screen != null && screen.transform.Find(AutumnStyleMarker) == null)
                ApplyAutumnTeamRoom(screen);
        }

        [MenuItem("Tools/NewBattle/Menü/Sonbahar Takım Odası (taslak B)", false, 9)]
        private static void ApplyAutumnTeamRoomItem()
        {
            TeamRoomScreen screen = Object.FindFirstObjectByType<TeamRoomScreen>(FindObjectsInactive.Include);
            if (screen != null)
                ApplyAutumnTeamRoom(screen);
        }

        /// <summary>Taslak piksellerine (varsayilan 1280x720) gore oransal yerlestirir.</summary>
        private static void Mock(Transform target, float x0, float y0, float x1, float y1, float w = 1280f, float h = 720f)
        {
            if (target is not RectTransform rect)
                return;

            Undo.RecordObject(rect, "Yerlestir");
            rect.anchorMin = new Vector2(x0 / w, 1f - y1 / h);
            rect.anchorMax = new Vector2(x1 / w, 1f - y0 / h);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static RectTransform EnsureChildImage(Transform parent, string name, string sprite, Color color, float multiplier)
        {
            RectTransform rect = parent.Find(name) as RectTransform;
            if (rect == null)
            {
                rect = NewRect(name, parent);
                Undo.RegisterCreatedObjectUndo(rect.gameObject, name);
                AddImage(rect, null, color);
            }

            Image image = rect.GetComponent<Image>();
            Undo.RecordObject(image, name);
            image.sprite = sprite != null ? Sprite(sprite) : null;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = multiplier;
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static void FaceSprite(Button button, string sprite, float multiplier = 2f)
        {
            if (button == null)
                return;

            Transform faceRect = button.transform.Find("Yuz");
            Image face = faceRect != null ? faceRect.GetComponent<Image>() : button.GetComponent<Image>();
            if (face == null)
                return;

            Undo.RecordObject(face, "Buton");
            face.sprite = Sprite(sprite);
            face.type = Image.Type.Sliced;
            face.pixelsPerUnitMultiplier = multiplier;
            face.color = Color.white;
            if (faceRect != null)
            {
                Undo.RecordObject(faceRect, "Buton");
                Stretch((RectTransform)faceRect, 0f, 0f, 0f, 0f);
            }

            HideButtonExtras(button.transform);
        }

        private static void ApplyAutumnTeamRoom(TeamRoomScreen screen)
        {
            if (!PrepareFont())
                return;
            PrepareMaterials();

            Undo.SetCurrentGroupName("Sonbahar takim odasi");
            Transform root = screen.transform;
            var serialized = new SerializedObject(screen);
            Color ink = AutumnInk;

            if (root.Find("Arkaplan")?.GetComponent<Image>() is Image background)
            {
                Undo.RecordObject(background, "Arka plan");
                background.sprite = Sprite("bg_sonbahar");
                background.color = Color.white;
            }

            if (root.Find("Karartma")?.GetComponent<Image>() is Image dim)
            {
                Undo.RecordObject(dim, "Karartma");
                dim.color = new Color(0.15f, 0.07f, 0.02f, 0.15f);
            }

            // Baslik
            if (root.Find("Baslik")?.GetComponent<TextMeshProUGUI>() is TextMeshProUGUI title)
            {
                Mock(title.transform, 34, 12, 420, 98);
                Paint(title, AutumnCream, shadow: true);
                title.alignment = TextAlignmentOptions.Left;
            }

            // Oda kodu: agactan sarkan ahsap tabela
            RectTransform sign = EnsureChildImage(root, "OdaTabelasi", "ahsap_panel", Color.white, 2.2f);
            Mock(sign, 430, 28, 700, 108);
            RectTransform ropeLeft = EnsureChildImage(root, "Ip_Sol", null, Hex("#5A3418"), 1f);
            Mock(ropeLeft, 464, 0, 468, 32);
            RectTransform ropeRight = EnsureChildImage(root, "Ip_Sag", null, Hex("#5A3418"), 1f);
            Mock(ropeRight, 662, 0, 666, 32);
            if (ScreenRef(serialized, "codeText") is Transform code && code.TryGetComponent(out TextMeshProUGUI codeText))
            {
                Mock(code, 440, 32, 690, 104);
                Paint(codeText, AutumnCream, shadow: true);
                codeText.alignment = TextAlignmentOptions.Center;
                codeText.lineSpacing = -20f;
                code.SetSiblingIndex(sign.GetSiblingIndex() + 1);
            }

            serialized.FindProperty("codeFormat").stringValue = "<size=38%>ODA KODU</size>\n{0}";

            RectTransform countBoard = EnsureChildImage(root, "OyuncuSayisiZemin", "ahsap_panel", Color.white, 2.4f);
            Mock(countBoard, 730, 34, 1010, 96);
            if (ScreenRef(serialized, "playerCountText") is Transform count && count.TryGetComponent(out TextMeshProUGUI countText))
            {
                Mock(count, 744, 38, 996, 90);
                Paint(countText, AutumnCream, shadow: true);
                countText.alignment = TextAlignmentOptions.Center;
                count.SetSiblingIndex(countBoard.GetSiblingIndex() + 1);
            }

            if (serialized.FindProperty("leaveButton").objectReferenceValue is Button leave)
            {
                Mock(leave.transform, 1070, 30, 1246, 94);
                FaceSprite(leave, "buton_kirmizi");
            }

            // Sol: mor ODA AYARLARI panosu
            if (root.Find("Ayarlar") is RectTransform settings)
            {
                Mock(settings, 40, 140, 420, 680);
                if (settings.GetComponent<Image>() is Image panel)
                {
                    Undo.RecordObject(panel, "Pano");
                    panel.sprite = Sprite("ui_round");
                    panel.type = Image.Type.Sliced;
                    panel.color = new Color(0.29f, 0.18f, 0.48f, 0.9f);
                }

                RectTransform frame = EnsureChildImage(settings, "Cerceve", "ui_round_outline", Hex("#6B4426"), 1f);
                Stretch(frame, -8f, -8f, -8f, -8f);
                frame.SetAsFirstSibling();

                RectTransform header = EnsureChildImage(settings, "AyarBaslik", "buton_turuncu", Color.white, 2f);
                Mock(header, -24, -30, 404, 36, 380, 540);
                TextMeshProUGUI headerText = header.GetComponentInChildren<TextMeshProUGUI>(true);
                if (headerText == null)
                {
                    headerText = AddText(header, "Yazi", "ODA AYARLARI", 46f, Color.white, TextAlignmentOptions.Center, shadow: true);
                    Undo.RegisterCreatedObjectUndo(headerText.gameObject, "Baslik");
                    Stretch(headerText.rectTransform, 20f, 20f, 4f, 12f);
                }

                AddCornerLeaves(settings);

                void Label(string name, float y0, float y1, Color color)
                {
                    if (settings.Find(name)?.GetComponent<TextMeshProUGUI>() is TextMeshProUGUI text)
                    {
                        Mock(text.transform, 24, y0, 356, y1, 380, 540);
                        Paint(text, color, shadow: color == Color.white);
                        text.alignment = TextAlignmentOptions.Left;
                    }
                }

                Label("OyuncuSayisiBaslik", 58, 92, Color.white);
                Label("TakimBoyuBaslik", 216, 250, Color.white);
                Label("KurucuNotu", 326, 356, Hex("#E6DDF5"));
                if (ScreenRef(serialized, "infoText") is Transform info && info.TryGetComponent(out TextMeshProUGUI infoText))
                {
                    Mock(info, 24, 360, 356, 420, 380, 540);
                    Paint(infoText, Color.white, shadow: false);
                    infoText.alignment = TextAlignmentOptions.Top;
                }

                StyleRoomChips(serialized, "sizeChoices", new[]
                {
                    new Vector4(24, 96, 184, 146), new Vector4(196, 96, 356, 146),
                    new Vector4(24, 154, 184, 204), new Vector4(196, 154, 356, 204),
                });
                StyleRoomChips(serialized, "teamSizeChoices", new[]
                {
                    new Vector4(24, 256, 184, 306), new Vector4(196, 256, 356, 306),
                });
            }

            serialized.FindProperty("selectedColor").colorValue = new Color(0.55f, 0.82f, 0.32f, 1f);
            serialized.FindProperty("normalColor").colorValue = new Color(0.93f, 0.8f, 0.6f, 1f);
            serialized.FindProperty("disabledColor").colorValue = new Color(0.62f, 0.56f, 0.5f, 0.6f);

            // HAZIR (kurucu degilse) solda panonun altinda; BASLAT (kurucu) sag altta.
            if (serialized.FindProperty("readyButton").objectReferenceValue is Button ready)
            {
                Mock(ready.transform, 110, 572, 350, 650);
                FaceSprite(ready, "buton_beyaz");
                ready.transform.SetAsLastSibling();
            }

            serialized.FindProperty("readyButtonColor").colorValue = new Color(0.5f, 0.8f, 0.28f, 1f);
            serialized.FindProperty("unreadyButtonColor").colorValue = new Color(0.96f, 0.56f, 0.22f, 1f);

            if (serialized.FindProperty("startButton").objectReferenceValue is Button start)
            {
                Mock(start.transform, 900, 598, 1240, 698);
                FaceSprite(start, "buton_turuncu");
            }

            // Durum: parsomen serit
            RectTransform statusBoard = EnsureChildImage(root, "DurumZemin", "parsomen", Color.white, 1.5f);
            Mock(statusBoard, 470, 612, 870, 678);
            if (ScreenRef(serialized, "statusText") is Transform status && status.TryGetComponent(out TextMeshProUGUI statusText))
            {
                Mock(status, 484, 616, 856, 674);
                Paint(statusText, ink, shadow: false);
                statusText.alignment = TextAlignmentOptions.Center;
                status.SetSiblingIndex(statusBoard.GetSiblingIndex() + 1);
            }

            // Sag: 2x2 takim kartlari
            if (ScreenRef(serialized, "teamsRoot") is RectTransform teams)
            {
                Mock(teams, 470, 140, 1240, 580);
                if (teams.GetComponent<GridLayoutGroup>() is GridLayoutGroup grid)
                {
                    Undo.RecordObject(grid, "Kartlar");
                    grid.cellSize = new Vector2(555f, 309f);
                    grid.spacing = new Vector2(42f, 42f);
                    grid.constraintCount = 2;
                    grid.childAlignment = TextAnchor.UpperLeft;
                }
            }

            if (serialized.FindProperty("cardTemplate").objectReferenceValue is TeamRoomCard card)
                StyleTeamCard(card);

            serialized.FindProperty("cardColor").colorValue = Color.white;
            serialized.FindProperty("myCardColor").colorValue = new Color(1f, 0.9f, 0.62f, 1f);
            serialized.FindProperty("hostNameColor").colorValue = new Color(1f, 0.85f, 0.35f, 1f);
            serialized.FindProperty("nameColor").colorValue = AutumnCream;
            serialized.FindProperty("emptyColor").colorValue = new Color(0.965f, 0.886f, 0.76f, 0.5f);
            serialized.FindProperty("readyOnColor").colorValue = new Color(0.55f, 0.85f, 0.3f, 1f);
            serialized.FindProperty("readyOffColor").colorValue = new Color(1f, 1f, 1f, 0.15f);
            serialized.ApplyModifiedProperties();

            if (root.Find(AutumnStyleMarker) == null)
            {
                RectTransform marker = NewRect(AutumnStyleMarker, root);
                Undo.RegisterCreatedObjectUndo(marker.gameObject, "Isaret");
            }

            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            EditorSceneManager.SaveScene(root.gameObject.scene);
            Debug.Log("Takim odasi: sonbahar taslagi B uygulandi.");
        }

        private static void StyleRoomChips(SerializedObject screen, string field, Vector4[] rects)
        {
            SerializedProperty list = screen.FindProperty(field);
            for (int i = 0; list != null && i < list.arraySize; i++)
            {
                SerializedProperty choice = list.GetArrayElementAtIndex(i);
                if (choice.FindPropertyRelative("button").objectReferenceValue is Button button && i < rects.Length)
                {
                    Vector4 r = rects[i];
                    Mock(button.transform, r.x, r.y, r.z, r.w, 380, 540);
                    HideButtonExtras(button.transform);
                }

                if (choice.FindPropertyRelative("face").objectReferenceValue is Image face)
                {
                    Undo.RecordObject(face, "Cip");
                    face.sprite = Sprite("cip_beyaz");
                    face.type = Image.Type.Sliced;
                    face.pixelsPerUnitMultiplier = 1.4f;
                    if (face.transform.name == "Yuz")
                    {
                        Undo.RecordObject(face.rectTransform, "Cip");
                        Stretch(face.rectTransform, 0f, 0f, 0f, 0f);
                    }
                }

                if (choice.FindPropertyRelative("label").objectReferenceValue is TextMeshProUGUI label)
                    Paint(label, AutumnInk, shadow: false);
            }
        }

        private static void StyleTeamCard(TeamRoomCard card)
        {
            var serialized = new SerializedObject(card);
            if (ScreenRef(serialized, "background")?.GetComponent<Image>() is Image background)
            {
                Undo.RecordObject(background, "Kart");
                background.sprite = Sprite("parsomen");
                background.type = Image.Type.Sliced;
                background.pixelsPerUnitMultiplier = 1.3f;
                background.color = Color.white;
            }

            RectTransform band = EnsureChildImage(card.transform, "BaslikZemin", "ahsap_baslik", Color.white, 2f);
            Undo.RecordObject(band, "Baslik");
            band.anchorMin = new Vector2(0f, 1f);
            band.anchorMax = Vector2.one;
            band.pivot = new Vector2(0.5f, 1f);
            band.offsetMin = new Vector2(-8f, -62f);
            band.offsetMax = new Vector2(8f, 10f);
            band.SetAsFirstSibling();

            if (ScreenRef(serialized, "title") is Transform titleRect && titleRect.TryGetComponent(out TextMeshProUGUI title))
            {
                Undo.RecordObject(titleRect, "Baslik");
                var rect = (RectTransform)titleRect;
                rect.offsetMin = new Vector2(12f, -60f);
                rect.offsetMax = new Vector2(-12f, 6f);
                Paint(title, AutumnCream, shadow: true);
            }

            SerializedProperty slots = serialized.FindProperty("slots");
            for (int i = 0; slots != null && i < slots.arraySize; i++)
            {
                if (slots.GetArrayElementAtIndex(i).FindPropertyRelative("root").objectReferenceValue is not GameObject slot)
                    continue;

                var rect = (RectTransform)slot.transform;
                Undo.RecordObject(rect, "Oyuncu");
                rect.offsetMin = new Vector2(16f, -78f - i * 56f - 50f);
                rect.offsetMax = new Vector2(-16f, -78f - i * 56f);
                if (slot.GetComponent<Image>() is Image plank)
                {
                    Undo.RecordObject(plank, "Oyuncu");
                    plank.sprite = Sprite("buton_ahsap");
                    plank.type = Image.Type.Sliced;
                    plank.pixelsPerUnitMultiplier = 2.6f;
                    plank.color = Color.white;
                }
            }
        }
    }
}
#endif
