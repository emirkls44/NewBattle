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

            ScreenAnnouncer announcer = Object.FindFirstObjectByType<ScreenAnnouncer>(FindObjectsInactive.Include);
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
