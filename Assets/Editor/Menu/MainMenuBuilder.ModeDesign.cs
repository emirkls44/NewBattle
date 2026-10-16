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
    /// Oyun modu penceresi = kullanicinin tasarim resmi birebir (tasarim_oyunmodu.png, 2000x1116,
    /// tam ekran: ustte logo, oyuncu adi, para ve ayarlar da resimde).
    /// Butonlar (Etkinlestir, modlar, OYNA, kapat, para, ayarlar) resmin ustunde gorunmez basma
    /// alanlari; secili modun tiki, oyuncu adi, para, cift XP yazisi ve ipucu canli.
    /// Derleyince kendiliginden uygulanir (resim degisince yeni koordinatlarla yeniden).
    /// </summary>
    public static partial class MainMenuBuilder
    {
        private const string ModeDesignSprite = "tasarim_oyunmodu";
        private const float ModeDesignWidth = 2000f, ModeDesignHeight = 1116f;

        [InitializeOnLoadMethod]
        private static void AutoApplyModeDesignLater()
        {
            EditorApplication.delayCall += AutoApplyModeDesign;
        }

        private static void AutoApplyModeDesign()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AutoApplyModeDesign;
                return;
            }

            ModeSelectPanel panel = Object.FindFirstObjectByType<ModeSelectPanel>(FindObjectsInactive.Include);
            Transform window = panel != null ? panel.transform.Find("Pencere") : null;
            if (window == null)
                return;

            AspectRatioFitter fitter = window.Find("Tasarim")?.GetComponent<AspectRatioFitter>();
            bool current = fitter != null && Mathf.Abs(fitter.aspectRatio - ModeDesignWidth / ModeDesignHeight) < 0.001f;
            if (!current)
            {
                if (!PrepareFont())
                    return;
                PrepareMaterials();
                ApplyModeDesign(panel, (RectTransform)window);
            }
        }

        private static void ApplyModeDesign(ModeSelectPanel panel, RectTransform window)
        {
            AssetDatabase.ImportAsset($"{ArtFolder}/{ModeDesignSprite}.png", ImportAssetOptions.ForceUpdate);
            Sprite design = Sprite(ModeDesignSprite);
            if (design == null)
                return;

            Undo.SetCurrentGroupName("Oyun modu tasarimi");
            Color ink = Hex("#5A3418");

            RectTransform layer = window.Find("Tasarim") as RectTransform;
            if (layer == null)
            {
                layer = NewRect("Tasarim", window);
                Undo.RegisterCreatedObjectUndo(layer.gameObject, "Tasarim");
                Stretch(layer, 0f, 0f, 0f, 0f);
                AddImage(layer, design, Color.white, raycast: true).type = Image.Type.Simple;
                layer.gameObject.AddComponent<AspectRatioFitter>();
            }

            Image layerImage = layer.GetComponent<Image>();
            Undo.RecordObject(layerImage, "Resim");
            layerImage.sprite = design;
            AspectRatioFitter fitter = layer.GetComponent<AspectRatioFitter>();
            Undo.RecordObject(fitter, "Oran");
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = ModeDesignWidth / ModeDesignHeight;
            layer.SetAsFirstSibling();

            // Canli yazilar.
            ModeOverlay(FindDeep(window, "Aciklama"), layer, new Vector4(146, 338, 590, 404), ink, false);
            ModeOverlay(FindDeep(window, "Ipucu"), layer, new Vector4(1452, 440, 1898, 636), ink, true);
            ModeOverlay(EnsureLabel(layer, "OyuncuAdi", ProgressLabel.Value.PlayerName), layer,
                new Vector4(846, 20, 1210, 72), ink, false, TextAlignmentOptions.Left);
            ModeOverlay(EnsureLabel(layer, "ParaMiktari", ProgressLabel.Value.Coins), layer,
                new Vector4(1612, 60, 1732, 110), Hex("#FFE9C7"), false);

            // Mod satirlari: dikey duzen kapanir, her satir resimdeki tahtanin ustune.
            if (FindDeep(window, "Secenekler")?.GetComponent<VerticalLayoutGroup>() is VerticalLayoutGroup list)
            {
                Undo.RecordObject(list, "Duzen");
                list.enabled = false;
            }

            (string name, Vector4 row, Vector4 box)[] rows =
            {
                ("Secenek_solo", new Vector4(800, 448, 1200, 540), new Vector4(1122, 466, 1180, 522)),
                ("Secenek_rastgele_ikili", new Vector4(800, 558, 1200, 648), new Vector4(1120, 574, 1182, 630)),
                ("Secenek_arkadas_ikilisi", new Vector4(800, 666, 1200, 756), new Vector4(1120, 684, 1180, 738)),
            };

            foreach ((string name, Vector4 row, Vector4 box) in rows)
            {
                if (FindDeep(window, name) is not RectTransform option)
                    continue;

                ModeMap(option, layer, row);
                MakeInvisible(option);

                // Kutu resimdeki kutunun ustune; tik ve kilit gorunur (kahverengi).
                if (option.Find("Kutu") is RectTransform checkBox)
                {
                    Undo.RecordObject(checkBox, "Kutu");
                    float w = row.z - row.x, h = row.w - row.y;
                    checkBox.anchorMin = new Vector2((box.x - row.x) / w, 1f - (box.w - row.y) / h);
                    checkBox.anchorMax = new Vector2((box.z - row.x) / w, 1f - (box.y - row.y) / h);
                    checkBox.offsetMin = checkBox.offsetMax = Vector2.zero;
                    checkBox.localScale = Vector3.one;

                    foreach (string mark in new[] { "Tik", "Kilit" })
                    {
                        if (checkBox.Find(mark) is not RectTransform markRect)
                            continue;

                        Undo.RecordObject(markRect, "Isaret");
                        markRect.anchorMin = new Vector2(0.08f, 0.08f);
                        markRect.anchorMax = new Vector2(0.92f, 0.92f);
                        markRect.offsetMin = markRect.offsetMax = Vector2.zero;
                        if (markRect.TryGetComponent(out Image markImage))
                        {
                            Undo.RecordObject(markImage, "Isaret");
                            markImage.enabled = true;
                            markImage.color = ink;
                            markImage.preserveAspect = true;
                        }
                    }
                }
            }

            foreach ((string name, Vector4 rect) in new[]
                     {
                         ("EtkinlestirButonu", new Vector4(150, 700, 580, 852)),
                         ("KapatButonu", new Vector4(1826, 204, 1922, 296)),
                         ("CevrimiciOynaButonu", new Vector4(785, 768, 1210, 945)),
                     })
            {
                if (FindDeep(window, name) is RectTransform hit)
                {
                    ModeMap(hit, layer, rect);
                    MakeInvisible(hit);
                }
            }

            // Resimdeki para ve ayarlar: ana menudekiyle ayni islevler.
            ModeMap(EnsureActionHit(layer, "ParaButonu", "para_paneli"), layer, new Vector4(1450, 20, 1812, 132));
            ModeMap(EnsureActionHit(layer, "AyarlarButonuTasarim", "ayarlar"), layer, new Vector4(1818, 22, 1938, 142));

            // Eski pencerenin kendi gorunumu (paneller, basliklar, manzara) resimde var.
            foreach (Graphic graphic in window.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic.transform.IsChildOf(layer) || graphic.transform == window)
                    continue;

                Undo.RecordObject(graphic, "Gizle");
                graphic.enabled = false;
            }

            if (window.GetComponent<Graphic>() is Graphic windowGraphic)
            {
                Undo.RecordObject(windowGraphic, "Gizle");
                windowGraphic.enabled = false;
            }

            // Secili satirin rengi artik resimde; panel rengi boyamasin.
            var serialized = new SerializedObject(panel);
            serialized.FindProperty("selectedColor").colorValue = new Color(1f, 1f, 1f, 0f);
            serialized.FindProperty("normalColor").colorValue = new Color(1f, 1f, 1f, 0f);
            serialized.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(window.gameObject.scene);
            EditorSceneManager.SaveScene(window.gameObject.scene);
            Debug.Log("Oyun modu penceresi: tasarim resmi birebir uygulandi.");
        }

        private static Transform EnsureLabel(RectTransform layer, string name, ProgressLabel.Value value)
        {
            if (layer.Find(name) is Transform existing)
                return existing;

            TextMeshProUGUI text = AddText(layer, name, string.Empty, 48f, Color.white, TextAlignmentOptions.Center, shadow: false);
            Undo.RegisterCreatedObjectUndo(text.gameObject, name);
            text.raycastTarget = false;
            ProgressLabel label = text.gameObject.AddComponent<ProgressLabel>();
            var serialized = new SerializedObject(label);
            serialized.FindProperty("value").enumValueIndex = (int)value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return text.transform;
        }

        /// <summary>Gorunmez buton; basinca MenuButton islevini calistirir.</summary>
        private static RectTransform EnsureActionHit(RectTransform layer, string name, string action)
        {
            if (layer.Find(name) is RectTransform existing)
                return existing;

            RectTransform rect = NewRect(name, layer);
            Undo.RegisterCreatedObjectUndo(rect.gameObject, name);
            Image image = AddImage(rect, null, new Color(1f, 1f, 1f, 0f), raycast: true);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            MenuButton menuButton = rect.gameObject.AddComponent<MenuButton>();
            var serialized = new SerializedObject(menuButton);
            serialized.FindProperty("actionId").stringValue = action;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            rect.gameObject.AddComponent<UIPressScale>();
            return rect;
        }

        private static void ModeMap(Transform target, RectTransform layer, Vector4 pixels)
        {
            if (target is not RectTransform rect)
                return;

            Undo.SetTransformParent(rect, layer, "Tasi");
            Undo.RecordObject(rect, "Yerlestir");
            rect.anchorMin = new Vector2(pixels.x / ModeDesignWidth, 1f - pixels.w / ModeDesignHeight);
            rect.anchorMax = new Vector2(pixels.z / ModeDesignWidth, 1f - pixels.y / ModeDesignHeight);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            if (!rect.gameObject.activeSelf)
                rect.gameObject.SetActive(true);
        }

        private static void ModeOverlay(Transform target, RectTransform layer, Vector4 pixels, Color color, bool wrap,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            if (target is not RectTransform rect)
                return;

            ModeMap(rect, layer, pixels);
            if (rect.TryGetComponent(out TextMeshProUGUI text))
            {
                Undo.RecordObject(text, "Yazi");
                text.enabled = true;
                text.color = color;
                text.alignment = alignment;
                text.enableAutoSizing = true;
                text.fontSizeMin = 10f;
                text.fontSizeMax = wrap ? 54f : 64f;
                text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
                EditorUtility.SetDirty(text);
            }
        }
    }
}
#endif
