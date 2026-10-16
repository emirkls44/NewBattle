#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using NewBattle.UI;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// Onaylanan sonbahar taslaklari: acilir pencereler ve kucuk paneller ahsap pano, koyu ahsap
    /// baslik, parsomen ic kutular, yaprak bicimli butonlar ve kose yapraklariyla.
    /// (Satin alma onayi, isim, hediye kodu, mucadeleler, gunluk odul, arkadaslarla oyna, izleme
    /// paneli, YAKINDA bildirimi.) Yerlesim ayni kalir, yalniz gorunum degisir.
    /// Parcalar: Tools/MenuArt/ortak_parcalar.py. Derleyince her pencereye bir kez uygulanir.
    /// </summary>
    public static partial class MainMenuBuilder
    {
        private const string AutumnStyleMarker = "SonbaharStil_v1";

        private static readonly Color AutumnInk = new(0.29f, 0.165f, 0.07f, 1f);
        private static readonly Color AutumnCream = new(0.965f, 0.886f, 0.76f, 1f);

        [InitializeOnLoadMethod]
        private static void AutoApplyAutumnPopupsLater()
        {
            EditorApplication.delayCall += AutoApplyAutumnPopups;
        }

        private static void AutoApplyAutumnPopups()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AutoApplyAutumnPopups;
                return;
            }

            ApplyAutumnPopups(force: false);
        }

        [MenuItem("Tools/NewBattle/Menü/Sonbahar Pencereleri (taslak stili)", false, 8)]
        private static void ApplyAutumnPopupsItem() => ApplyAutumnPopups(force: true);

        private static void ApplyAutumnPopups(bool force)
        {
            var roots = new List<Transform>();
            void Add(Component component)
            {
                if (component != null)
                    roots.Add(component.transform);
            }

            Add(Object.FindFirstObjectByType<NamePopup>(FindObjectsInactive.Include));
            Add(Object.FindFirstObjectByType<GiftCodePopup>(FindObjectsInactive.Include));
            Add(Object.FindFirstObjectByType<RewardPopup>(FindObjectsInactive.Include));
            Add(Object.FindFirstObjectByType<FriendRoomPopup>(FindObjectsInactive.Include));
            Add(Object.FindFirstObjectByType<ChallengesScreen>(FindObjectsInactive.Include));
            Add(Object.FindFirstObjectByType<SpectatorPanel>(FindObjectsInactive.Include));

            ShopScreen shop = Object.FindFirstObjectByType<ShopScreen>(FindObjectsInactive.Include);
            if (shop != null && new SerializedObject(shop).FindProperty("confirmRoot").objectReferenceValue is GameObject confirm)
                roots.Add(confirm.transform);

            MainMenuScreen menu = Object.FindFirstObjectByType<MainMenuScreen>(FindObjectsInactive.Include);
            if (menu != null && new SerializedObject(menu).FindProperty("toastGroup").objectReferenceValue is CanvasGroup toast)
                roots.Add(toast.transform);

            roots.RemoveAll(root => !force && root.Find(AutumnStyleMarker) != null);
            if (roots.Count == 0)
                return;

            if (!PrepareFont())
                return;
            PrepareMaterials();

            foreach (Transform root in roots)
            {
                Undo.SetCurrentGroupName("Sonbahar pencere stili");
                StyleAutumnWindow(root);
                if (root.Find(AutumnStyleMarker) == null)
                {
                    RectTransform marker = NewRect(AutumnStyleMarker, root);
                    Undo.RegisterCreatedObjectUndo(marker.gameObject, "Isaret");
                }

                EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            }

            EditorSceneManager.SaveOpenScenes();
            Debug.Log($"Sonbahar pencere stili uygulandi ({roots.Count} pencere).");
        }

        // ------------------------------------------------------------------

        private static void StyleAutumnWindow(Transform root)
        {
            var skip = new HashSet<Image>();
            var parchments = new List<Transform>();

            // Calisirken boyanan parcalar (gun kutulari, secim cipleri, gorev satirlari) once.
            if (root.GetComponent<RewardPopup>() is RewardPopup reward)
                StyleRewardDays(reward, skip, parchments);
            if (root.GetComponent<FriendRoomPopup>() is FriendRoomPopup room)
                StyleRoomChoices(room, skip);
            if (root.GetComponent<ChallengesScreen>() is ChallengesScreen challenges)
                StyleChallengeRow(challenges, skip, parchments);

            if (root.Find("Karartma")?.GetComponent<Image>() is Image dim)
            {
                Undo.RecordObject(dim, "Karartma");
                dim.color = new Color(0.18f, 0.08f, 0.02f, 0.6f);
            }

            // Pencere: ahsap pano; baslik koyu ahsap.
            Transform window = root.Find("Pencere");
            Image windowImage = window != null ? window.GetComponent<Image>() : null;
            if (windowImage == null && root.GetComponent<Image>() is Image own && own.color.a > 0.3f && IsShadeColor(own.color))
            {
                window = root;
                windowImage = own; // YAKINDA bildirimi
            }

            if (windowImage != null)
            {
                Board(windowImage, "ahsap_panel", 1.2f);
                skip.Add(windowImage);
                if (window.Find("Baslik") is Transform header && header.GetComponent<Image>() is Image headerImage)
                {
                    Board(headerImage, "ahsap_baslik", 1.2f);
                    skip.Add(headerImage);
                    foreach (TextMeshProUGUI title in header.GetComponentsInChildren<TextMeshProUGUI>(true))
                        Paint(title, AutumnCream, shadow: true);
                }

                if (window != root)
                    AddCornerLeaves((RectTransform)window);
            }

            // Butonlar: yaprak bicimli (rengi eski rengine gore), kapat yuvarlak ahsap.
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                Transform faceRect = button.transform.Find("Yuz");
                Image face = faceRect != null ? faceRect.GetComponent<Image>() : null;
                if (face == null)
                    face = button.GetComponent<Image>();
                if (face == null || skip.Contains(face) || button.GetComponent<TMP_InputField>() != null)
                    continue;

                skip.Add(face);
                if (button.GetComponent<Image>() is Image rootImage)
                    skip.Add(rootImage);

                if (button.name.Contains("Kapat"))
                {
                    Undo.RecordObject(face, "Kapat");
                    face.sprite = Sprite("yuvarlak_ahsap");
                    face.type = Image.Type.Simple;
                    face.color = Color.white;
                    face.preserveAspect = true;
                    HideButtonExtras(button.transform);
                    continue;
                }

                string kind = button.name.Contains("Onceki") || button.name.Contains("Sonraki") ? "ahsap" : ButtonKind(face.color);
                Undo.RecordObject(face, "Buton");
                face.sprite = Sprite($"buton_{kind}");
                face.type = Image.Type.Sliced;
                face.pixelsPerUnitMultiplier = 2f;
                face.color = Color.white;
                if (face.transform != button.transform)
                {
                    Undo.RecordObject(face.rectTransform, "Buton");
                    Stretch(face.rectTransform, 0f, 0f, 0f, 0f);
                }

                HideButtonExtras(button.transform);
            }

            // Giris kutulari: krem kutu.
            foreach (TMP_InputField input in root.GetComponentsInChildren<TMP_InputField>(true))
            {
                if (input.GetComponent<Image>() is not Image box)
                    continue;

                Board(box, "yazi_kutusu", 1.5f);
                skip.Add(box);
                foreach (TextMeshProUGUI text in input.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    Undo.RecordObject(text, "Yazi");
                    text.color = text == input.placeholder ? new Color(AutumnInk.r, AutumnInk.g, AutumnInk.b, 0.45f) : AutumnInk;
                }
            }

            // Ic kutular: mor / koyu olanlar parsomen, dolum cubuklarinin zemini koyu ahsap.
            foreach (Image image in root.GetComponentsInChildren<Image>(true))
            {
                if (skip.Contains(image) || image.GetComponentInParent<Button>(true) != null ||
                    image.transform.name.StartsWith("Yaprak_") || !IsShadeColor(image.color) || image.color.a < 0.2f)
                    continue;

                if (image.transform.Find("Dolum") != null)
                {
                    Undo.RecordObject(image, "Cubuk");
                    image.color = new Color(0.23f, 0.13f, 0.06f, 1f);
                    continue;
                }

                if (image.sprite == null || image.sprite.name != "ui_round")
                    continue;

                if (image.transform.parent == root || image.name == "Zemin")
                {
                    Board(image, "ahsap_panel", 1.6f); // izleme paneli zemini
                    continue;
                }

                Board(image, "parsomen", 1.5f);
                parchments.Add(image.transform);
            }

            // Parsomen ustundeki yazilar koyu kahve; ahsap ustundeki soluk mor yazilar kreme.
            foreach (Transform paper in parchments)
            {
                foreach (TextMeshProUGUI text in paper.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    if (text.GetComponentInParent<Button>(true) is Button owner && owner.transform.IsChildOf(paper) &&
                        owner.transform != paper)
                        continue;

                    Paint(text, AutumnInk, shadow: false);
                }
            }

            foreach (TextMeshProUGUI text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                Color.RGBToHSV(text.color, out float h, out float s, out float v);
                if (s < 0.2f && v > 0.8f && v < 0.95f && h > 0.6f && h < 0.85f)
                {
                    Undo.RecordObject(text, "Yazi");
                    text.color = new Color(0.92f, 0.84f, 0.69f, text.color.a); // soluk mor -> krem
                }
            }
        }

        private static void Board(Image image, string sprite, float multiplier)
        {
            Undo.RecordObject(image, "Sonbahar");
            image.sprite = Sprite(sprite);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = multiplier;
            image.color = Color.white;
            image.enabled = true;
        }

        private static void Paint(TextMeshProUGUI text, Color color, bool shadow)
        {
            Undo.RecordObject(text, "Yazi");
            text.color = new Color(color.r, color.g, color.b, text.color.a);
            text.fontSharedMaterial = shadow ? _shadowMaterial : _plainMaterial;
        }

        /// <summary>Mor / lacivert / yari saydam siyah (eski tema) mi?</summary>
        private static bool IsShadeColor(Color color)
        {
            Color.RGBToHSV(color, out float h, out float s, out float v);
            bool purple = h > 0.62f && h < 0.86f && v < 0.65f;
            bool black = v < 0.12f && color.a < 0.95f;
            return purple || black;
        }

        private static string ButtonKind(Color color)
        {
            Color.RGBToHSV(color, out float h, out float s, out float v);
            if (s < 0.25f)
                return "ahsap";
            if (h < 0.03f || h > 0.94f)
                return "kirmizi";
            if (h < 0.17f)
                return "turuncu";
            if (h < 0.47f)
                return "yesil";
            if (h < 0.68f)
                return "mavi";
            return "ahsap";
        }

        /// <summary>Eski butonun alt kenari ve parlamasi kapanir (yeni resimde var).</summary>
        private static void HideButtonExtras(Transform button)
        {
            foreach (string name in new[] { "Alt", "Yuz/Parlama", "Parlama" })
            {
                if (button.Find(name)?.GetComponent<Image>() is Image extra)
                {
                    Undo.RecordObject(extra, "Gizle");
                    extra.enabled = false;
                }
            }
        }

        /// <summary>Pencerenin uc kosesine yaprak kumesi (arkada, tiklamayi engellemez).</summary>
        private static void AddCornerLeaves(RectTransform window)
        {
            (string name, Vector2 anchor, Vector2 offset, Vector3 scale, float angle)[] corners =
            {
                ("Yaprak_SolUst", new Vector2(0f, 1f), new Vector2(18f, -14f), new Vector3(1f, 1f, 1f), 8f),
                ("Yaprak_SagUst", new Vector2(1f, 1f), new Vector2(-30f, -10f), new Vector3(-1f, 1f, 1f), -6f),
                ("Yaprak_SagAlt", new Vector2(1f, 0f), new Vector2(-16f, 22f), new Vector3(-1f, -1f, 1f), 10f),
            };

            foreach ((string name, Vector2 anchor, Vector2 offset, Vector3 scale, float angle) in corners)
            {
                RectTransform leaf = window.Find(name) as RectTransform;
                if (leaf == null)
                {
                    leaf = NewRect(name, window);
                    Undo.RegisterCreatedObjectUndo(leaf.gameObject, name);
                    AddImage(leaf, Sprite("yaprak_kume"), Color.white).type = Image.Type.Simple;
                }

                Undo.RecordObject(leaf, name);
                leaf.anchorMin = leaf.anchorMax = anchor;
                leaf.pivot = new Vector2(0.3f, 0.7f);
                leaf.anchoredPosition = offset;
                leaf.sizeDelta = new Vector2(190f, 157f);
                leaf.localScale = scale;
                leaf.localEulerAngles = new Vector3(0f, 0f, angle);
                if (leaf.GetComponent<Image>() is Image image)
                    image.raycastTarget = false;
                leaf.SetSiblingIndex(0);
            }
        }

        private static void StyleRewardDays(RewardPopup popup, HashSet<Image> skip, List<Transform> parchments)
        {
            var serialized = new SerializedObject(popup);
            SerializedProperty days = serialized.FindProperty("days");
            for (int i = 0; days != null && i < days.arraySize; i++)
            {
                SerializedProperty slot = days.GetArrayElementAtIndex(i);
                if (slot.FindPropertyRelative("background").objectReferenceValue is not Image background)
                    continue;

                Board(background, "parsomen", 1.6f);
                skip.Add(background);
                parchments.Add(background.transform);
                if (slot.FindPropertyRelative("button").objectReferenceValue is Button button &&
                    button.GetComponent<Image>() is Image buttonImage)
                    skip.Add(buttonImage);
            }

            serialized.FindProperty("claimedColor").colorValue = new Color(0.66f, 0.55f, 0.42f, 1f);
            serialized.FindProperty("todayColor").colorValue = new Color(1f, 0.66f, 0.32f, 1f);
            serialized.FindProperty("futureColor").colorValue = Color.white;
            serialized.ApplyModifiedProperties();
        }

        private static void StyleRoomChoices(FriendRoomPopup popup, HashSet<Image> skip)
        {
            var serialized = new SerializedObject(popup);
            foreach (string field in new[] { "sizeChoices", "teamSizeChoices" })
            {
                SerializedProperty list = serialized.FindProperty(field);
                for (int i = 0; list != null && i < list.arraySize; i++)
                {
                    SerializedProperty choice = list.GetArrayElementAtIndex(i);
                    if (choice.FindPropertyRelative("face").objectReferenceValue is Image face)
                    {
                        Undo.RecordObject(face, "Secim");
                        face.sprite = Sprite("cip_beyaz");
                        face.type = Image.Type.Sliced;
                        face.pixelsPerUnitMultiplier = 1.4f;
                        skip.Add(face);
                        if (face.transform.parent != null)
                            HideButtonExtras(face.transform.parent);
                        if (face.transform.parent != null && face.transform.name == "Yuz")
                        {
                            Undo.RecordObject(face.rectTransform, "Secim");
                            Stretch(face.rectTransform, 0f, 0f, 0f, 0f);
                        }
                    }

                    if (choice.FindPropertyRelative("label").objectReferenceValue is TextMeshProUGUI label)
                        Paint(label, AutumnInk, shadow: false);
                    if (choice.FindPropertyRelative("button").objectReferenceValue is Button button &&
                        button.GetComponent<Image>() is Image own)
                        skip.Add(own);
                }
            }

            serialized.FindProperty("selectedColor").colorValue = new Color(0.55f, 0.82f, 0.32f, 1f);
            serialized.FindProperty("normalColor").colorValue = new Color(0.93f, 0.8f, 0.6f, 1f);
            serialized.FindProperty("disabledColor").colorValue = new Color(0.62f, 0.56f, 0.5f, 0.6f);
            serialized.ApplyModifiedProperties();
        }

        private static void StyleChallengeRow(ChallengesScreen screen, HashSet<Image> skip, List<Transform> parchments)
        {
            var serialized = new SerializedObject(screen);
            if (serialized.FindProperty("rowTemplate").objectReferenceValue is not ChallengeRow row)
                return;

            var rowSerialized = new SerializedObject(row);
            if (rowSerialized.FindProperty("background").objectReferenceValue is Image background)
            {
                Board(background, "parsomen", 1.5f);
                skip.Add(background);
            }

            if (rowSerialized.FindProperty("progressFill").objectReferenceValue is RectTransform fill)
            {
                if (fill.GetComponent<Image>() is Image fillImage)
                {
                    Undo.RecordObject(fillImage, "Dolum");
                    fillImage.color = Hex("#7DBA3A");
                    skip.Add(fillImage);
                }

                if (fill.parent != null && fill.parent.GetComponent<Image>() is Image track)
                {
                    Undo.RecordObject(track, "Cubuk");
                    track.color = new Color(0.23f, 0.13f, 0.06f, 1f);
                    skip.Add(track);
                }
            }

            rowSerialized.FindProperty("activeColor").colorValue = Color.white;
            rowSerialized.FindProperty("doneColor").colorValue = new Color(0.86f, 0.95f, 0.76f, 1f);
            rowSerialized.ApplyModifiedProperties();
        }
    }
}
#endif
