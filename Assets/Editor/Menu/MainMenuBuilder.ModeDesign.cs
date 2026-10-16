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
    /// Oyun modu penceresi = kullanicinin tasarim resmi birebir (tasarim_oyunmodu.png, 2000x859).
    /// Butonlar (Etkinlestir, modlar, OYNA, kapat) resmin ustunde gorunmez basma alanlari;
    /// secili modun tiki, cift XP yazisi ve ipucu canli. Derleyince bir kez uygulanir.
    /// </summary>
    public static partial class MainMenuBuilder
    {
        private const string ModeDesignSprite = "tasarim_oyunmodu";
        private const float ModeDesignWidth = 2000f, ModeDesignHeight = 859f;

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
            if (window != null && window.Find("Tasarim") == null)
                ApplyModeDesign(panel, (RectTransform)window);
        }

        private static void ApplyModeDesign(ModeSelectPanel panel, RectTransform window)
        {
            AssetDatabase.ImportAsset($"{ArtFolder}/{ModeDesignSprite}.png");
            Sprite design = Sprite(ModeDesignSprite);
            if (design == null)
                return;

            Undo.SetCurrentGroupName("Oyun modu tasarimi");
            Color ink = Hex("#5A3418");

            RectTransform layer = NewRect("Tasarim", window);
            Undo.RegisterCreatedObjectUndo(layer.gameObject, "Tasarim");
            Stretch(layer, 0f, 0f, 0f, 0f);
            Image image = AddImage(layer, design, Color.white, raycast: true);
            image.type = Image.Type.Simple;
            AspectRatioFitter fitter = layer.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = ModeDesignWidth / ModeDesignHeight;
            layer.SetAsFirstSibling();

            // Canli yazilar.
            ModeOverlay(window.Find("SolPanel/Aciklama"), layer, new Vector4(150, 202, 580, 258), ink, false);
            ModeOverlay(window.Find("SagPanel/Ipucu"), layer, new Vector4(1452, 312, 1890, 494), ink, true);

            // Mod satirlari: dikey duzen kapanir, her satir resimdeki tahtanin ustune.
            if (window.Find("OrtaPanel/Secenekler")?.GetComponent<VerticalLayoutGroup>() is VerticalLayoutGroup list)
            {
                Undo.RecordObject(list, "Duzen");
                list.enabled = false;
            }

            (string name, Vector4 row, Vector4 box)[] rows =
            {
                ("Secenek_solo", new Vector4(795, 318, 1198, 410), new Vector4(1118, 336, 1178, 394)),
                ("Secenek_rastgele_ikili", new Vector4(795, 428, 1198, 522), new Vector4(1116, 440, 1182, 500)),
                ("Secenek_arkadas_ikilisi", new Vector4(795, 535, 1198, 630), new Vector4(1118, 548, 1178, 606)),
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

            foreach ((string path, Vector4 rect) in new[]
                     {
                         ("SolPanel/EtkinlestirButonu", new Vector4(150, 568, 592, 730)),
                         ("SagPanel/KapatButonu", new Vector4(1795, 80, 1905, 170)),
                         ("CevrimiciOynaButonu", new Vector4(785, 640, 1205, 800)),
                     })
            {
                if (window.Find(path) is RectTransform hit)
                {
                    ModeMap(hit, layer, rect);
                    MakeInvisible(hit);
                }
            }

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

        private static void ModeMap(RectTransform rect, RectTransform layer, Vector4 pixels)
        {
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

        private static void ModeOverlay(Transform target, RectTransform layer, Vector4 pixels, Color color, bool wrap)
        {
            if (target is not RectTransform rect)
                return;

            ModeMap(rect, layer, pixels);
            if (rect.TryGetComponent(out TextMeshProUGUI text))
            {
                Undo.RecordObject(text, "Yazi");
                text.enabled = true;
                text.color = color;
                text.alignment = TextAlignmentOptions.Center;
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
