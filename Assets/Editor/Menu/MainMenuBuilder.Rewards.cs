#if UNITY_EDITOR
using System.Collections.Generic;
using NewBattle.Progression;
using NewBattle.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// XP yapisi: ilerleme ayarlari dosyasi, menudeki odul penceresi
    /// (gunluk giris / ilk oyun hediyesi / seviye odulu) ve IKI KAT XP
    /// gostergeleri. Sadece eksik olani ekler.
    /// </summary>
    public static partial class MainMenuBuilder
    {
        private const string RewardPopupName = "OdulPenceresi";
        private const string ProgressionSettingsPath = "Assets/Resources/IlerlemeAyarlari.asset";
        private const int DaySlotCount = 7;

        /// <summary>Ilerleme ayarlari dosyasini olusturur (yoksa). Olusturduysa true.</summary>
        public static bool EnsureProgressionSettings(out ProgressionSettings settings)
        {
            settings = AssetDatabase.LoadAssetAtPath<ProgressionSettings>(ProgressionSettingsPath);
            if (settings != null)
                return false;

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            settings = ScriptableObject.CreateInstance<ProgressionSettings>();
            AssetDatabase.CreateAsset(settings, ProgressionSettingsPath);
            AssetDatabase.SaveAssets();
            return true;
        }

        private static bool EnsureRewardPopup(MainMenuScreen screen)
        {
            var serialized = new SerializedObject(screen);
            SerializedProperty property = serialized.FindProperty("rewardPopup");
            if (property.objectReferenceValue != null)
                return false;

            RewardPopup popup = screen.GetComponentInChildren<RewardPopup>(true);
            if (popup == null)
                popup = BuildRewardPopup((RectTransform)screen.transform);

            property.objectReferenceValue = popup;
            serialized.ApplyModifiedProperties();
            return true;
        }

        private static RewardPopup BuildRewardPopup(RectTransform menuRoot)
        {
            RectTransform root = NewRect(RewardPopupName, menuRoot);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Odul penceresi");
            Stretch(root, 0f, 0f, 0f, 0f);
            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();

            RectTransform dim = NewRect("Karartma", root);
            Stretch(dim, 0f, 0f, 0f, 0f);
            AddImage(dim, null, new Color(0f, 0f, 0f, 0.7f), raycast: true);

            RectTransform window = NewRect("Pencere", root);
            Place(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(1240f, 720f));
            AddImage(window, Sprite("ui_round"), ResultCard, raycast: true).pixelsPerUnitMultiplier = 1.2f;

            TextMeshProUGUI title = CardHeader(window, "GÜNLÜK ÖDÜL", Orange, Color.white, 76f, shadow: true, inset: 0f, height: 120f);

            Image icon = AddIcon(window, "Ikon", "icon_gift", Color.white, new Vector2(0.5f, 0.5f), new Vector2(0f, 50f), 250f);
            icon.gameObject.SetActive(false);

            TextMeshProUGUI body = AddText(window, "Yazi", "Bugün: +50 XP", 60f, Color.white, TextAlignmentOptions.Center, shadow: true);
            body.textWrappingMode = TextWrappingModes.Normal;
            body.lineSpacing = -10f;
            Place(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -125f), new Vector2(1100f, 140f));

            // 7 gunluk sira.
            const float slotWidth = 140f;
            const float slotSpacing = 18f;
            RectTransform daysRoot = NewRect("Gunler", window);
            float rowWidth = DaySlotCount * slotWidth + (DaySlotCount - 1) * slotSpacing;
            Place(daysRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 75f), new Vector2(rowWidth, 200f));

            var slots = new List<(Button button, Image background, TextMeshProUGUI day, TextMeshProUGUI amount, GameObject check)>();
            for (int i = 0; i < DaySlotCount; i++)
            {
                RectTransform slot = NewRect($"Gun{i + 1}", daysRoot);
                Place(slot, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(i * (slotWidth + slotSpacing), 0f), new Vector2(slotWidth, 200f));
                Image background = AddImage(slot, Sprite("ui_round"), ResultCardDim, raycast: true);
                background.pixelsPerUnitMultiplier = 1.6f;
                Button button = slot.gameObject.AddComponent<Button>();
                button.targetGraphic = background;
                slot.gameObject.AddComponent<UIPressScale>();

                TextMeshProUGUI day = AddText(slot, "Gun", $"{i + 1}. GÜN", 28f, Color.white, TextAlignmentOptions.Center, shadow: true);
                Place(day.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(130f, 40f));

                AddIcon(slot, "Ikon", "icon_star", Color.white, new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), 70f);

                TextMeshProUGUI amount = AddText(slot, "Miktar", "+50", 36f, Color.white, TextAlignmentOptions.Center, shadow: true);
                Place(amount.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(130f, 48f));

                Image check = AddIcon(slot, "Alindi", "icon_check", Green, new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), 96f);
                check.gameObject.SetActive(false);

                slots.Add((button, background, day, amount, check.gameObject));
            }

            Button claim = AddButton(window, "AlButonu", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 46f), new Vector2(400f, 124f), Green, GreenLip);
            TextMeshProUGUI claimLabel = AddText(Face(claim), "Yazi", "AL", 66f, Color.white, TextAlignmentOptions.Center, shadow: true);
            Stretch(claimLabel.rectTransform, 12f, 12f, 0f, 4f);

            RewardPopup popup = root.gameObject.AddComponent<RewardPopup>();
            var serialized = new SerializedObject(popup);
            serialized.FindProperty("group").objectReferenceValue = group;
            serialized.FindProperty("window").objectReferenceValue = window;
            serialized.FindProperty("titleText").objectReferenceValue = title;
            serialized.FindProperty("bodyText").objectReferenceValue = body;
            serialized.FindProperty("icon").objectReferenceValue = icon;
            serialized.FindProperty("giftIcon").objectReferenceValue = Sprite("icon_gift");
            serialized.FindProperty("coinIcon").objectReferenceValue = Sprite("coin");
            serialized.FindProperty("xpIcon").objectReferenceValue = Sprite("icon_star");
            serialized.FindProperty("levelUpIcon").objectReferenceValue = Sprite("icon_crown");
            serialized.FindProperty("animationIcon").objectReferenceValue = Sprite("icon_person");
            serialized.FindProperty("daysRoot").objectReferenceValue = daysRoot.gameObject;
            serialized.FindProperty("futureColor").colorValue = ResultCardDim;
            serialized.FindProperty("claimButton").objectReferenceValue = claim;
            serialized.FindProperty("claimLabel").objectReferenceValue = claimLabel;

            SerializedProperty dayList = serialized.FindProperty("days");
            dayList.arraySize = slots.Count;
            for (int i = 0; i < slots.Count; i++)
            {
                SerializedProperty element = dayList.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("button").objectReferenceValue = slots[i].button;
                element.FindPropertyRelative("background").objectReferenceValue = slots[i].background;
                element.FindPropertyRelative("dayText").objectReferenceValue = slots[i].day;
                element.FindPropertyRelative("amountText").objectReferenceValue = slots[i].amount;
                element.FindPropertyRelative("check").objectReferenceValue = slots[i].check;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

            group.alpha = 0f;
            group.blocksRaycasts = false;
            root.gameObject.SetActive(false);
            return popup;
        }

        /// <summary>Menudeki ve oyun modu penceresindeki IKI KAT XP yazilarini baglar.</summary>
        private static bool EnsureDoubleXpRefs(MainMenuScreen screen)
        {
            var serialized = new SerializedObject(screen);
            SerializedProperty texts = serialized.FindProperty("doubleXpTexts");
            SerializedProperty steps = serialized.FindProperty("doubleXpSteps");
            if (texts.arraySize > 0 || steps.arraySize > 0)
                return false;

            var foundTexts = new List<TextMeshProUGUI>();
            var foundSteps = new List<Image>();

            if (screen.transform.Find("OrtaAlan/CiftXpKarti/Yazi")?.GetComponent<TextMeshProUGUI>() is { } cardText)
                foundTexts.Add(cardText);

            var panel = serialized.FindProperty("modePanel").objectReferenceValue as ModeSelectPanel;
            Transform side = panel != null ? FindDeep(panel.transform, "SolPanel") : null;
            if (side != null)
            {
                if (side.Find("Aciklama")?.GetComponent<TextMeshProUGUI>() is { } hint)
                    foundTexts.Add(hint);

                for (int i = 1; i <= 3; i++)
                {
                    if (side.Find($"Ilerleme/Adim{i}")?.GetComponent<Image>() is { } step)
                        foundSteps.Add(step);
                }
            }

            if (foundTexts.Count == 0 && foundSteps.Count == 0)
                return false;

            texts.arraySize = foundTexts.Count;
            for (int i = 0; i < foundTexts.Count; i++)
                texts.GetArrayElementAtIndex(i).objectReferenceValue = foundTexts[i];

            steps.arraySize = foundSteps.Count;
            for (int i = 0; i < foundSteps.Count; i++)
                steps.GetArrayElementAtIndex(i).objectReferenceValue = foundSteps[i];

            serialized.ApplyModifiedProperties();
            return true;
        }

        /// <summary>Harita secim ekranindaki "Çift XP etkin değil" yazisini baglar.</summary>
        private static bool EnsureDropDoubleXpRefs(Canvas canvas)
        {
            DropSelectionScreen drop = canvas.GetComponentInChildren<DropSelectionScreen>(true);
            if (drop == null)
                return false;

            var serialized = new SerializedObject(drop);
            SerializedProperty text = serialized.FindProperty("doubleXpText");
            if (text.objectReferenceValue != null)
                return false;

            Transform status = drop.transform.Find("CiftXpDurumu");
            if (status == null)
                return false;

            text.objectReferenceValue = status.Find("Yazi")?.GetComponent<TextMeshProUGUI>();
            serialized.FindProperty("doubleXpIcon").objectReferenceValue = status.Find("Ikon")?.GetComponent<Image>();
            serialized.ApplyModifiedProperties();
            return text.objectReferenceValue != null;
        }

        /// <summary>Mac sonu XP kartina "2x" rozeti ekler.</summary>
        private static bool EnsureMatchEndDoubleXpBadge(Canvas canvas)
        {
            MatchEndScreen screen = canvas.GetComponentInChildren<MatchEndScreen>(true);
            if (screen == null)
                return false;

            var serialized = new SerializedObject(screen);
            SerializedProperty badge = serialized.FindProperty("doubleXpBadge");
            if (badge.objectReferenceValue != null)
                return false;

            Transform card = screen.transform.Find("XpKarti");
            if (card == null)
                return false;

            RectTransform rect = NewRect("CiftXpRozeti", card);
            Undo.RegisterCreatedObjectUndo(rect.gameObject, "2x rozeti");
            Place(rect, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(-14f, -14f), new Vector2(100f, 64f));
            rect.localRotation = Quaternion.Euler(0f, 0f, -12f);
            AddImage(rect, Sprite("ui_round"), Green).pixelsPerUnitMultiplier = 2.4f;
            TextMeshProUGUI label = AddText(rect, "Yazi", "2x", 44f, Color.white, TextAlignmentOptions.Center, shadow: true);
            Stretch(label.rectTransform, 4f, 4f, 0f, 3f);
            rect.gameObject.SetActive(false);

            badge.objectReferenceValue = rect;
            serialized.ApplyModifiedProperties();
            return true;
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                    return child;

                Transform found = FindDeep(child, name);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
#endif
