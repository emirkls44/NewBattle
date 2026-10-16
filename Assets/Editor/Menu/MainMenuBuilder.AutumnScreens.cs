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
    /// Sonbahar ekranlari = kullanicinin tasarim resimleri birebir (2000x1116, tam ekran).
    ///   - Profil ve Ayarlar: resim ana menunun ustunde duran pencere (disi saydam, ici yari
    ///     saydam mor); butonlar resmin ustunde gorunmez basma alanlari, degisen yazilar canli.
    ///   - Seviye yolu: arka plan resmi, kartlar resimdeki kart resimleri (alinan / simdiki /
    ///     siradaki / gelecek), en ustte cerceve (kartlar dal kenarlarin altindan kayar).
    /// Resimler: Tools/MenuArt/ekranlar_hazirla.py. Derleyince kendiliginden uygulanir;
    /// surum (AutumnScreensVersion) artinca yeniden yerlesir.
    /// </summary>
    public static partial class MainMenuBuilder
    {
        private const int AutumnScreensVersion = 1;
        private static string AutumnLayerName => $"Tasarim_v{AutumnScreensVersion}";

        [InitializeOnLoadMethod]
        private static void AutoApplyAutumnScreensLater()
        {
            EditorApplication.delayCall += AutoApplyAutumnScreens;
        }

        private static void AutoApplyAutumnScreens()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AutoApplyAutumnScreens;
                return;
            }

            ApplyAutumnScreens(force: false);
        }

        [MenuItem("Tools/NewBattle/Menü/Sonbahar Ekranları (tasarım resimleri)", false, 7)]
        private static void ApplyAutumnScreensItem() => ApplyAutumnScreens(force: true);

        private static void ApplyAutumnScreens(bool force)
        {
            bool changed = false;
            SettingsScreen settings = Object.FindFirstObjectByType<SettingsScreen>(FindObjectsInactive.Include);
            ProfileScreen profile = Object.FindFirstObjectByType<ProfileScreen>(FindObjectsInactive.Include);
            LevelRoadScreen levelRoad = Object.FindFirstObjectByType<LevelRoadScreen>(FindObjectsInactive.Include);

            bool Needs(Component screen) => screen != null && (force || screen.transform.Find(AutumnLayerName) == null);
            if (!Needs(settings) && !Needs(profile) && !Needs(levelRoad))
                return;

            if (!PrepareFont())
                return;
            PrepareMaterials();

            if (Needs(settings))
                changed |= ApplySettingsDesign(settings);
            if (Needs(profile))
                changed |= ApplyProfileDesign(profile);
            if (Needs(levelRoad))
                changed |= ApplyLevelRoadDesign(levelRoad);

            if (changed)
            {
                Component any = settings != null ? settings : profile;
                EditorSceneManager.MarkSceneDirty(any.gameObject.scene);
                EditorSceneManager.SaveScene(any.gameObject.scene);
                Debug.Log("Sonbahar ekranlari: tasarim resimleri uygulandi.");
            }
        }

        // ------------------------------------------------------------------
        // Ortak: tasarim katmani
        // ------------------------------------------------------------------

        /// <summary>
        /// Ekranin kokune tam ekran tasarim resmi koyar (varsa eskisini yeniden kullanir) ve
        /// karartmayi saydam yapar (pencere disina basmak yine menuye gitmez).
        /// </summary>
        private static RectTransform EnsureAutumnLayer(RectTransform root, string spriteName)
        {
            SetSharpDesign($"{ArtFolder}/{spriteName}.png");
            Sprite design = Sprite(spriteName);
            if (design == null)
                return null;

            RectTransform layer = null;
            foreach (Transform child in root)
            {
                if (child.name.StartsWith("Tasarim"))
                    layer = (RectTransform)child;
            }

            if (layer == null)
            {
                layer = NewRect(AutumnLayerName, root);
                Undo.RegisterCreatedObjectUndo(layer.gameObject, "Tasarim");
                AddImage(layer, design, Color.white, raycast: true);
            }

            Undo.RecordObject(layer.gameObject, "Tasarim");
            layer.name = AutumnLayerName;
            Image image = layer.GetComponent<Image>();
            Undo.RecordObject(image, "Tasarim");
            image.sprite = design;
            image.type = Image.Type.Simple;
            image.color = Color.white;
            image.raycastTarget = true;
            image.enabled = true;
            Undo.RecordObject(layer, "Tam ekran");
            Stretch(layer, 0f, 0f, 0f, 0f);
            layer.localScale = Vector3.one;

            if (root.Find("Karartma") is Transform dim && dim.TryGetComponent(out Image dimImage))
            {
                Undo.RecordObject(dimImage, "Karartma");
                dimImage.color = new Color(0f, 0f, 0f, 0f);
                dimImage.raycastTarget = true;
                layer.SetSiblingIndex(dim.GetSiblingIndex() + 1);
            }
            else
            {
                layer.SetAsFirstSibling();
            }

            return layer;
        }

        /// <summary>Eski pencerenin kendi gorunumu kapanir (resimde var); tasarim katmani haric.</summary>
        private static void HideOldWindow(Transform window, RectTransform layer)
        {
            if (window == null)
                return;

            foreach (Graphic graphic in window.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic.transform.IsChildOf(layer))
                    continue;

                Undo.RecordObject(graphic, "Gizle");
                graphic.enabled = false;
            }
        }

        private static Transform ScreenRef(SerializedObject screen, string field) =>
            (screen.FindProperty(field)?.objectReferenceValue as Component)?.transform;

        /// <summary>Buton resmin ustunde gorunmez basma alani olur.</summary>
        private static void DesignHit(Transform target, RectTransform layer, Vector4 pixels)
        {
            if (target is not RectTransform rect)
                return;

            ModeMap(rect, layer, pixels);
            MakeInvisible(rect);
        }

        // ------------------------------------------------------------------
        // Ayarlar
        // ------------------------------------------------------------------

        private static bool ApplySettingsDesign(SettingsScreen screen)
        {
            var root = (RectTransform)screen.transform;
            RectTransform layer = EnsureAutumnLayer(root, "tasarim_ayarlar");
            if (layer == null)
                return false;

            Undo.SetCurrentGroupName("Sonbahar ayarlar");
            var serialized = new SerializedObject(screen);
            Transform window = root.Find("Pencere");
            Color label = Color.white;

            DesignHit(ScreenRef(serialized, "closeButton"), layer, new Vector4(1525, 18, 1630, 124));

            // Muzik / efektler: kendi resmi (acik yesil, kapali gri), yazi canli.
            Sprite on = Sprite("ayar_acik");
            Sprite off = Sprite("ayar_kapali");
            (string button, string face, string text, Vector4 rect)[] toggles =
            {
                ("musicButton", "musicFace", "musicLabel", new Vector4(1161, 185, 1572, 326)),
                ("soundButton", "soundFace", "soundLabel", new Vector4(1161, 319, 1572, 460)),
            };
            foreach ((string button, string face, string text, Vector4 rect) in toggles)
            {
                if (ScreenRef(serialized, button) is not RectTransform toggle)
                    continue;

                Transform caption = ScreenRef(serialized, text);
                DesignHit(toggle, layer, rect);
                Image own = toggle.GetComponent<Image>();
                Undo.RecordObject(own, "Dugme");
                own.sprite = on;
                own.type = Image.Type.Simple;
                own.color = Color.white;
                serialized.FindProperty(face).objectReferenceValue = own;

                float h = rect.w - rect.y;
                ModeOverlay(caption, layer, new Vector4(rect.x + 90, rect.y + h * 0.2f, rect.z - 90, rect.w - h * 0.24f),
                    label, false);
            }

            serialized.FindProperty("onSprite").objectReferenceValue = on;
            serialized.FindProperty("offSprite").objectReferenceValue = off;

            DesignHit(ScreenRef(serialized, "frameRateButton"), layer, new Vector4(1190, 463, 1545, 584));
            ModeOverlay(ScreenRef(serialized, "frameRateLabel"), layer, new Vector4(1250, 488, 1485, 560), label, false);
            DesignHit(ScreenRef(serialized, "nameButton"), layer, new Vector4(1187, 605, 1546, 719));
            DesignHit(ScreenRef(serialized, "giftCodeButton"), layer, new Vector4(1186, 733, 1546, 852));
            ModeOverlay(ScreenRef(serialized, "versionText"), layer, new Vector4(460, 984, 860, 1036), Hex("#E6DDF5"),
                false, TextAlignmentOptions.Left);
            serialized.ApplyModifiedProperties();

            HideOldWindow(window, layer);
            if (window != null && window.GetComponent<Graphic>() is Graphic windowGraphic)
            {
                Undo.RecordObject(windowGraphic, "Gizle");
                windowGraphic.enabled = false;
            }

            return true;
        }

        // ------------------------------------------------------------------
        // Profil
        // ------------------------------------------------------------------

        private static bool ApplyProfileDesign(ProfileScreen screen)
        {
            var root = (RectTransform)screen.transform;
            RectTransform layer = EnsureAutumnLayer(root, "tasarim_profil");
            if (layer == null)
                return false;

            Undo.SetCurrentGroupName("Sonbahar profil");
            var serialized = new SerializedObject(screen);
            Transform window = root.Find("Pencere");
            Color number = Hex("#F7941D");

            DesignHit(ScreenRef(serialized, "closeButton"), layer, new Vector4(1582, 128, 1676, 210));
            ModeOverlay(ScreenRef(serialized, "nameText"), layer, new Vector4(760, 276, 1240, 354), Hex("#F6B92B"), false);
            ModeOverlay(ScreenRef(serialized, "levelText"), layer, new Vector4(760, 358, 1240, 412), Color.white, false);

            (string field, Vector4 rect)[] stats =
            {
                ("matchesText", new Vector4(462, 452, 784, 560)),
                ("winsText", new Vector4(844, 452, 1166, 560)),
                ("winRateText", new Vector4(1226, 452, 1548, 560)),
                ("killsText", new Vector4(462, 702, 784, 810)),
                ("killsPerMatchText", new Vector4(844, 702, 1166, 810)),
                ("bestPlacementText", new Vector4(1226, 702, 1548, 810)),
            };
            foreach ((string field, Vector4 rect) in stats)
                ModeOverlay(ScreenRef(serialized, field), layer, rect, number, false);

            HideOldWindow(window, layer);
            return true;
        }

        // ------------------------------------------------------------------
        // Seviye yolu
        // ------------------------------------------------------------------

        /// <summary>Tasarim pikselini tuval birimine cevirir (tuval yuksekligi 1080 sabit).</summary>
        private const float DesignToCanvas = 1080f / 1116f;

        private static bool ApplyLevelRoadDesign(LevelRoadScreen screen)
        {
            var root = (RectTransform)screen.transform;
            if (root.Find("Arkaplan") is not RectTransform background)
                return false;

            SetSharpDesign($"{ArtFolder}/seviyeyolu_arkaplan.png");
            SetSharpDesign($"{ArtFolder}/seviyeyolu_cerceve.png");
            Undo.SetCurrentGroupName("Sonbahar seviye yolu");
            var serialized = new SerializedObject(screen);

            // Arka plan: resim; yerlesim bunun piksellerine gore.
            Image backgroundImage = background.GetComponent<Image>();
            Undo.RecordObject(backgroundImage, "Arka plan");
            backgroundImage.sprite = Sprite("seviyeyolu_arkaplan");
            backgroundImage.type = Image.Type.Simple;
            backgroundImage.color = Color.white;
            Undo.RecordObject(background, "Arka plan");
            Stretch(background, 0f, 0f, 0f, 0f);
            background.SetAsFirstSibling();

            if (root.Find("Baslik")?.GetComponent<Graphic>() is Graphic title)
            {
                Undo.RecordObject(title, "Gizle");
                title.enabled = false; // basligi resim yaziyor
            }

            DesignHit(ScreenRef(serialized, "closeButton"), background, new Vector4(1855, 40, 1962, 152));
            ModeOverlay(ScreenRef(serialized, "levelText"), background, new Vector4(58, 148, 430, 222), Hex("#FFC928"),
                false, TextAlignmentOptions.Left);
            ModeOverlay(ScreenRef(serialized, "rankText"), background, new Vector4(58, 222, 430, 276), Color.white,
                false, TextAlignmentOptions.Left);

            // XP cubugu: zemin resimde, dolum sari, yazi ustte.
            if (ScreenRef(serialized, "xpFill") is RectTransform fill && fill.parent is RectTransform track)
            {
                ModeMap(track, background, new Vector4(514, 174, 1311, 226));
                if (track.GetComponent<Image>() is Image trackImage)
                {
                    Undo.RecordObject(trackImage, "Gizle");
                    trackImage.enabled = false;
                }

                Undo.RecordObject(fill, "Dolum");
                fill.offsetMin = new Vector2(5f, 5f);
                fill.offsetMax = new Vector2(0f, -5f);
                if (fill.GetComponent<Image>() is Image fillImage)
                {
                    Undo.RecordObject(fillImage, "Dolum");
                    fillImage.sprite = Sprite("ui_round");
                    fillImage.type = Image.Type.Sliced;
                    fillImage.pixelsPerUnitMultiplier = 2.6f;
                    fillImage.color = Hex("#F6BE27");
                    fillImage.enabled = true;
                }
            }

            // Liste: resimdeki mor alan; kartlar resimdeki aralikla.
            Transform list = ScreenRef(serialized, "scroll");
            if (list is RectTransform panel)
            {
                ModeMap(panel, background, new Vector4(60, 350, 1940, 1036));
                Undo.SetTransformParent(panel, root, "Liste");
                if (panel.GetComponent<Image>() is Image panelImage)
                {
                    Undo.RecordObject(panelImage, "Gizle");
                    panelImage.color = new Color(1f, 1f, 1f, 0f);
                }

                if (panel.GetComponent<ScrollRect>()?.viewport is RectTransform viewport)
                {
                    Undo.RecordObject(viewport, "Gorunum");
                    Stretch(viewport, 0f, 0f, 0f, 0f);
                }
            }

            if (ScreenRef(serialized, "listContent") is RectTransform content &&
                content.GetComponent<GridLayoutGroup>() is GridLayoutGroup grid)
            {
                Undo.RecordObject(content, "Icerik");
                content.anchorMin = new Vector2(0f, 0f);
                content.anchorMax = new Vector2(0f, 1f);
                content.pivot = new Vector2(0f, 0.5f);
                content.offsetMin = new Vector2(content.offsetMin.x, 0f);
                content.offsetMax = new Vector2(content.offsetMax.x, 0f);

                Undo.RecordObject(grid, "Kartlar");
                grid.cellSize = new Vector2(338f, 634f) * DesignToCanvas;
                grid.spacing = Vector2.zero;
                int side = Mathf.RoundToInt(38 * DesignToCanvas);
                grid.padding = new RectOffset(side, side, Mathf.RoundToInt(26 * DesignToCanvas), 0);
                grid.childAlignment = TextAnchor.UpperLeft;
            }

            if (serialized.FindProperty("cardTemplate").objectReferenceValue is LevelRoadCard card)
                StyleLevelRoadCard(card);

            // Cerceve en ustte: kartlar dal kenarlarin ve yapraklarin altindan kayar.
            RectTransform frame = root.Find(AutumnLayerName) as RectTransform;
            if (frame == null)
            {
                frame = NewRect(AutumnLayerName, root);
                Undo.RegisterCreatedObjectUndo(frame.gameObject, "Cerceve");
                AddImage(frame, null, Color.white);
            }

            Undo.RecordObject(frame, "Cerceve");
            Stretch(frame, 0f, 0f, 0f, 0f);
            frame.localScale = Vector3.one;
            Image frameImage = frame.GetComponent<Image>();
            Undo.RecordObject(frameImage, "Cerceve");
            frameImage.sprite = Sprite("seviyeyolu_cerceve");
            frameImage.type = Image.Type.Simple;
            frameImage.color = Color.white;
            frameImage.raycastTarget = false;
            if (list != null)
                frame.SetSiblingIndex(list.GetSiblingIndex() + 1);
            else
                frame.SetAsLastSibling();

            return true;
        }

        /// <summary>Kart sablonu: resim kart (duruma gore degisir), yazilar ve ikon resimdeki yerlerde.</summary>
        private static void StyleLevelRoadCard(LevelRoadCard card)
        {
            var serialized = new SerializedObject(card);
            serialized.FindProperty("reachedSprite").objectReferenceValue = Sprite("yol_kart_alinan");
            serialized.FindProperty("currentSprite").objectReferenceValue = Sprite("yol_kart_simdiki");
            serialized.FindProperty("nextSprite").objectReferenceValue = Sprite("yol_kart_siradaki");
            serialized.FindProperty("futureSprite").objectReferenceValue = Sprite("yol_kart_gelecek");
            serialized.ApplyModifiedProperties();

            const float w = 338f, h = 634f;
            void Local(Transform target, Vector4 px)
            {
                if (target is not RectTransform rect)
                    return;

                Undo.RecordObject(rect, "Kart");
                rect.anchorMin = new Vector2(px.x / w, 1f - px.w / h);
                rect.anchorMax = new Vector2(px.z / w, 1f - px.y / h);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
            }

            void Text(string field, Vector4 px, Color color, bool wrap)
            {
                Transform target = ScreenRef(serialized, field);
                Local(target, px);
                if (target != null && target.TryGetComponent(out TextMeshProUGUI text))
                {
                    Undo.RecordObject(text, "Kart");
                    text.color = color;
                    text.fontSharedMaterial = _plainMaterial;
                    text.enableAutoSizing = true;
                    text.fontSizeMin = 10f;
                    text.fontSizeMax = wrap ? 44f : 56f;
                    text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
                    text.alignment = TextAlignmentOptions.Center;
                }
            }

            if (ScreenRef(serialized, "background")?.GetComponent<Image>() is Image back)
            {
                Undo.RecordObject(back, "Kart");
                back.sprite = Sprite("yol_kart_gelecek");
                back.type = Image.Type.Simple;
                back.color = Color.white;
            }

            Color ink = Hex("#4A2A12");
            Text("levelText", new Vector4(28, 46, 310, 106), ink, false);
            Text("rankText", new Vector4(40, 108, 298, 150), Hex("#5B3A1A"), false);
            Text("rewardText", new Vector4(26, 428, 312, 598), ink, true);
            Local(ScreenRef(serialized, "rewardIcon"), new Vector4(89, 209, 249, 369));

            if (ScreenRef(serialized, "reachedMark") is Transform mark)
            {
                Local(mark, new Vector4(258, 34, 322, 84));
                if (mark.TryGetComponent(out Image tick))
                {
                    Undo.RecordObject(tick, "Tik");
                    tick.sprite = Sprite("yol_tik");
                    tick.color = Color.white;
                    tick.preserveAspect = true;
                    tick.enabled = true;
                }
            }

            if (ScreenRef(serialized, "nextOutline") is Transform outline)
            {
                foreach (Graphic graphic in outline.GetComponentsInChildren<Graphic>(true))
                {
                    Undo.RecordObject(graphic, "Gizle");
                    graphic.enabled = false; // altin cerceve kart resminde
                }
            }
        }
    }
}

#endif
