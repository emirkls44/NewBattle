#if UNITY_EDITOR
using NewBattle.Gameplay;
using NewBattle.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// Mac ici his: sol ustte oldurme akisi, oldurunce ziplayan afis + XP,
    /// vurulunca kirmizi kenar efekti ve "SON 3 OYUNCU!" duyurusunun fontu.
    /// Hepsi Game_UI altinda; Hierarchy'den tasiyabilirsin.
    /// </summary>
    public static partial class MainMenuBuilder
    {
        private static bool EnsureMatchFeel(Canvas canvas, out string summary)
        {
            var added = new System.Collections.Generic.List<string>();
            var root = (RectTransform)canvas.transform;

            if (canvas.GetComponentInChildren<KillFeed>(true) == null)
            {
                BuildKillFeed(root);
                added.Add("Öldürme akışı (sol üst)");
            }

            // "ÖLDÜRDÜN!" yazisi yerine can barinin ustunde kuru kafa rozetleri.
            KillBanner oldBanner = canvas.GetComponentInChildren<KillBanner>(true);
            if (oldBanner != null)
            {
                Undo.DestroyObjectImmediate(oldBanner.gameObject);
                added.Add("ÖLDÜRDÜN yazısı kaldırıldı");
            }

            if (canvas.GetComponentInChildren<KillBadges>(true) == null)
            {
                BuildKillBadges(root);
                added.Add("Öldürme rozetleri (can barının üstü)");
            }

            if (canvas.GetComponentInChildren<DamageVignette>(true) == null)
            {
                BuildDamageVignette(root);
                added.Add("Hasar efekti");
            }

            SpectatorPanel existingSpectator = canvas.GetComponentInChildren<SpectatorPanel>(true);
            if (existingSpectator != null && UpgradeSpectatorPanel(existingSpectator))
                added.Add("İzleme paneli: sağ altta MAÇTAN ÇIK");

            if (existingSpectator == null)
            {
                SpectatorPanel panel = BuildSpectatorPanel(root);
                MatchResultHUD resultHud = Object.FindFirstObjectByType<MatchResultHUD>(FindObjectsInactive.Include);
                if (resultHud != null)
                {
                    var serializedHud = new SerializedObject(resultHud);
                    serializedHud.FindProperty("spectatorPanel").objectReferenceValue = panel;
                    serializedHud.ApplyModifiedProperties();
                }

                added.Add("İzleme paneli (ölünce kimi izlediğin + oklarla geçiş)");
            }

            ScreenAnnouncer announcer =Object.FindFirstObjectByType<ScreenAnnouncer>(FindObjectsInactive.Include);
            if (announcer != null)
            {
                var serialized = new SerializedObject(announcer);
                SerializedProperty font = serialized.FindProperty("font");
                if (font != null && font.objectReferenceValue == null)
                {
                    font.objectReferenceValue = _font;
                    serialized.ApplyModifiedProperties();
                    added.Add("Ekran duyurusu yazı tipi");
                }
            }

            summary = string.Join(", ", added);
            return added.Count > 0;
        }

        private static void BuildKillFeed(RectTransform canvas)
        {
            Vector2 topLeft = new(0f, 1f);
            RectTransform feed = NewRect("OldurmeAkisi", canvas);
            Undo.RegisterCreatedObjectUndo(feed.gameObject, "Oldurme akisi");
            Place(feed, topLeft, topLeft, new Vector2(40f, -96f), new Vector2(640f, 260f));

            RectTransform list = NewRect("Liste", feed);
            Stretch(list, 0f, 0f, 0f, 0f);
            VerticalLayoutGroup layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            RectTransform row = NewRect("SatirSablonu", list);
            row.sizeDelta = new Vector2(600f, 52f);
            AddImage(row, Sprite("ui_round"), new Color(0f, 0f, 0f, 0.55f)).pixelsPerUnitMultiplier = 2.4f;
            TextMeshProUGUI text = AddText(row, "Yazi", "emir  <size=75%>Tüfek</size>  Dorian", 32f, Color.white, TextAlignmentOptions.Left, shadow: true);
            Stretch(text.rectTransform, 18f, 12f, 0f, 2f);
            text.richText = true;

            KillFeed component = feed.gameObject.AddComponent<KillFeed>();
            var serialized = new SerializedObject(component);
            serialized.FindProperty("list").objectReferenceValue = list;
            serialized.FindProperty("rowTemplate").objectReferenceValue = row;
            serialized.FindProperty("aliveCounter").objectReferenceValue = canvas.Find("BattleStatus/AliveCount") as RectTransform;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private const int SpectatorLayoutVersion = 1;
        private const float SpectatorPlateHeight = 190f;
        private static readonly Color ExitRed = Hex("#E8453C");
        private static readonly Color ExitRedLip = Hex("#A82820");

        /// <summary>
        /// Olunce: alt ortada "İZLENİYOR" etiketi + izlenen oyuncunun adi, iki yanda
        /// oklar (oyuncular arasinda gecis); sag altta kirmizi MAÇTAN ÇIK (mac
        /// istatistikleri ekranina gec). Kok tum ekrani kaplar, cocuklar kenarlara baglidir.
        /// </summary>
        private static SpectatorPanel BuildSpectatorPanel(RectTransform canvas)
        {
            RectTransform root = NewRect("IzlemePaneli", canvas);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Izleme paneli");
            Stretch(root, 0f, 0f, 0f, 0f);
            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            Vector2 bottom = new(0.5f, 0f);
            Vector2 center = new(0.5f, 0.5f);

            RectTransform plate = NewRect("Zemin", root);
            Place(plate, bottom, center, new Vector2(0f, SpectatorPlateHeight), new Vector2(600f, 140f));
            AddImage(plate, Sprite("ui_round"), new Color(0f, 0f, 0f, 0.6f)).pixelsPerUnitMultiplier = 2f;

            TextMeshProUGUI tag = AddText(plate, "Etiket", "İZLENİYOR · TAKIM ARKADAŞIN", 28f, new Color(0.45f, 0.9f, 1f, 1f), TextAlignmentOptions.Center);
            Place(tag.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(560f, 40f));

            TextMeshProUGUI playerName = AddText(plate, "Isim", "Savaşçı", 56f, Color.white, TextAlignmentOptions.Center, shadow: true);
            Place(playerName.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(560f, 76f));

            Button previous = AddButton(root, "OncekiButonu", bottom, center,
                new Vector2(-385f, SpectatorPlateHeight + 4f), new Vector2(120f, 120f), Blue, BlueLip);
            AddIcon(Face(previous), "Ok", "icon_play", Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, 58f)
                .rectTransform.localEulerAngles = new Vector3(0f, 0f, 180f);

            Button next = AddButton(root, "SonrakiButonu", bottom, center,
                new Vector2(385f, SpectatorPlateHeight + 4f), new Vector2(120f, 120f), Blue, BlueLip);
            AddIcon(Face(next), "Ok", "icon_play", Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, 58f);

            Button exit = BuildSpectatorExitButton(root);

            SpectatorPanel component = root.gameObject.AddComponent<SpectatorPanel>();
            var serialized = new SerializedObject(component);
            serialized.FindProperty("group").objectReferenceValue = group;
            serialized.FindProperty("nameText").objectReferenceValue = playerName;
            serialized.FindProperty("tagText").objectReferenceValue = tag;
            serialized.FindProperty("previousButton").objectReferenceValue = previous;
            serialized.FindProperty("nextButton").objectReferenceValue = next;
            serialized.FindProperty("leaveButton").objectReferenceValue = exit;
            serialized.FindProperty("layoutVersion").intValue = SpectatorLayoutVersion;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return component;
        }

        /// <summary>Sag altta (olunce gizlenen ates joystick'inin yeri) kirmizi MAÇTAN ÇIK.</summary>
        private static Button BuildSpectatorExitButton(RectTransform root)
        {
            Vector2 bottomRight = new(1f, 0f);
            Button exit = AddButton(root, "MactanCikButonu", bottomRight, bottomRight,
                new Vector2(-60f, 70f), new Vector2(360f, 120f), ExitRed, ExitRedLip);
            AddIcon(Face(exit), "Ikon", "icon_close", Color.white, new Vector2(0f, 0.5f), new Vector2(56f, 0f), 50f);
            TextMeshProUGUI label = AddText(Face(exit), "Yazi", "MAÇTAN ÇIK", 46f, Color.white, TextAlignmentOptions.Center, shadow: true);
            Stretch(label.rectTransform, 92f, 16f, 0f, 4f);
            return exit;
        }

        /// <summary>
        /// Eski izleme paneli (alt ortada tek parca, kucuk AYRIL): koku tum ekrana yayar,
        /// plaka ve oklari alta baglar, AYRIL yerine sag altta MAÇTAN ÇIK koyar. Tek seferlik.
        /// </summary>
        private static bool UpgradeSpectatorPanel(SpectatorPanel panel)
        {
            var serialized = new SerializedObject(panel);
            SerializedProperty version = serialized.FindProperty("layoutVersion");
            if (version == null || version.intValue >= SpectatorLayoutVersion)
                return false;

            var root = (RectTransform)panel.transform;
            Undo.RecordObject(root, "Izleme paneli");
            float oldCenterY = root.anchoredPosition.y;
            Stretch(root, 0f, 0f, 0f, 0f);

            foreach (string child in new[] { "Zemin", "OncekiButonu", "SonrakiButonu" })
            {
                if (root.Find(child) is not RectTransform rect)
                    continue;

                Undo.RecordObject(rect, "Izleme paneli");
                Vector2 position = rect.anchoredPosition;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(position.x, oldCenterY + position.y);
            }

            Transform oldLeave = root.Find("AyrilButonu");
            if (oldLeave != null)
                Undo.DestroyObjectImmediate(oldLeave.gameObject);

            serialized.FindProperty("leaveButton").objectReferenceValue = BuildSpectatorExitButton(root);
            version.intValue = SpectatorLayoutVersion;
            serialized.ApplyModifiedProperties();
            return true;
        }

        private static void BuildKillBadges(RectTransform canvas)
        {
            Vector2 bottom = new(0.5f, 0f);
            RectTransform root = NewRect("OldurmeRozetleri", canvas);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Oldurme rozetleri");
            Place(root, bottom, new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(400f, 160f));
            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            RectTransform badge = NewRect("RozetSablonu", root);
            Place(badge, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(112f, 112f));

            RectTransform backing = NewRect("Zemin", badge);
            Stretch(backing, 6f, 6f, 6f, 6f);
            AddImage(backing, Sprite("ui_circle"), new Color(0.12f, 0.08f, 0.2f, 0.88f)).type = Image.Type.Simple;

            RectTransform track = NewRect("HalkaIzi", badge);
            Stretch(track, 0f, 0f, 0f, 0f);
            AddImage(track, Sprite("ring"), new Color(1f, 1f, 1f, 0.22f)).type = Image.Type.Simple;

            RectTransform ringRect = NewRect("Halka", badge);
            Stretch(ringRect, 0f, 0f, 0f, 0f);
            Image ring = AddImage(ringRect, Sprite("ring"), Yellow);
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = true;
            ring.fillAmount = 1f;

            AddIcon(badge, "KuruKafa", "icon_skull", Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, 60f);

            KillBadges component = root.gameObject.AddComponent<KillBadges>();
            var serialized = new SerializedObject(component);
            serialized.FindProperty("container").objectReferenceValue = root;
            serialized.FindProperty("group").objectReferenceValue = group;
            serialized.FindProperty("badgeTemplate").objectReferenceValue = badge;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildKillBanner(RectTransform canvas)
        {
            Vector2 top = new(0.5f, 1f);
            RectTransform root = NewRect("OldurmeAfisi", canvas);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Oldurme afisi");
            Place(root, top, new Vector2(0.5f, 0.5f), new Vector2(0f, -240f), new Vector2(900f, 240f));
            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            TextMeshProUGUI title = AddText(root, "Baslik", "ÖLDÜRDÜN!", 100f, Yellow, TextAlignmentOptions.Center, shadow: true);
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 45f), new Vector2(900f, 125f));

            TextMeshProUGUI victim = AddText(root, "Kurban", "Dorian", 42f, Color.white, TextAlignmentOptions.Center, shadow: true);
            Place(victim.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -32f), new Vector2(800f, 56f));

            TextMeshProUGUI xp = AddText(root, "Xp", "+15 XP", 52f, new Color(0.45f, 1f, 0.4f, 1f), TextAlignmentOptions.Center, shadow: true);
            Place(xp.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -95f), new Vector2(400f, 66f));

            KillBanner component = root.gameObject.AddComponent<KillBanner>();
            var serialized = new SerializedObject(component);
            serialized.FindProperty("banner").objectReferenceValue = root;
            serialized.FindProperty("group").objectReferenceValue = group;
            serialized.FindProperty("titleText").objectReferenceValue = title;
            serialized.FindProperty("victimText").objectReferenceValue = victim;
            serialized.FindProperty("xpRect").objectReferenceValue = xp.rectTransform;
            serialized.FindProperty("xpText").objectReferenceValue = xp;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildDamageVignette(RectTransform canvas)
        {
            RectTransform root = NewRect("HasarEfekti", canvas);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Hasar efekti");
            Stretch(root, 0f, 0f, 0f, 0f);
            root.SetAsFirstSibling(); // joystick ve HUD'un altinda, oyunun ustunde

            Image image = AddImage(root, Sprite("vignette_white"), new Color(0.9f, 0.05f, 0.05f, 0f));
            image.type = Image.Type.Simple;
            root.gameObject.AddComponent<DamageVignette>();
        }
    }
}
#endif
