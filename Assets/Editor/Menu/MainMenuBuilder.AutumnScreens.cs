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
    ///   - Magaza ve Karakter: arka plan resmi, kartlar resimdeki kart resimleri, sagda 3D karakter
    ///     resimdeki yerde, islem butonu durumuna gore resim degistirir (sec / satin al / secili).
    ///   - Mac sonu: orman arka plani, uc kart ve DEVAM ET resimdeki parcalar (ziplayarak gelir),
    ///     baslik / mesaj / sayilar / seviye canli, 3D karakter resimdeki yerde.
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
            ShopScreen shop = Object.FindFirstObjectByType<ShopScreen>(FindObjectsInactive.Include);
            LobbyAnimationScreen character = Object.FindFirstObjectByType<LobbyAnimationScreen>(FindObjectsInactive.Include);
            MatchEndScreen matchEnd = Object.FindFirstObjectByType<MatchEndScreen>(FindObjectsInactive.Include);

            bool Needs(Component screen) => screen != null && (force || screen.transform.Find(AutumnLayerName) == null ||
                                                                (screen is MatchEndScreen && screen.transform.Find(MatchEndMarker) == null));
            if (!Needs(settings) && !Needs(profile) && !Needs(levelRoad) && !Needs(shop) && !Needs(character) &&
                !Needs(matchEnd))
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
            if (Needs(shop))
                changed |= ApplyShopDesign(shop);
            if (Needs(character))
                changed |= ApplyCharacterDesign(character);
            if (Needs(matchEnd))
                changed |= ApplyMatchEndDesign(matchEnd);

            if (changed)
            {
                Component any = settings != null ? settings : profile != null ? profile : levelRoad != null ? levelRoad
                    : shop != null ? shop : character != null ? character : matchEnd;
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
        // ------------------------------------------------------------------
        // Magaza ve Karakter (tam ekran, solda kart listesi, sagda 3D karakter)
        // ------------------------------------------------------------------

        /// <summary>Arka plan resmini koyar; yerlesimin katmani budur.</summary>
        private static RectTransform AutumnBackground(RectTransform root, string sprite)
        {
            if (root.Find("Arkaplan") is not RectTransform background)
                return null;

            SetSharpDesign($"{ArtFolder}/{sprite}.png");
            Image image = background.GetComponent<Image>();
            Undo.RecordObject(image, "Arka plan");
            image.sprite = Sprite(sprite);
            image.type = Image.Type.Simple;
            image.color = Color.white;
            image.preserveAspect = false;
            Undo.RecordObject(background, "Arka plan");
            Stretch(background, 0f, 0f, 0f, 0f);
            background.localScale = Vector3.one;
            background.SetAsFirstSibling();

            foreach (string name in new[] { "Baslik", "Para" })
            {
                if (root.Find(name) is not Transform old)
                    continue;

                foreach (Graphic graphic in old.GetComponentsInChildren<Graphic>(true))
                {
                    Undo.RecordObject(graphic, "Gizle");
                    graphic.enabled = false; // resimde var
                }
            }

            return background;
        }

        /// <summary>Isaret: ekran bu surumle uygulandi (gorunmez).</summary>
        private static RectTransform AutumnMarker(RectTransform root, Sprite overlay)
        {
            RectTransform marker = root.Find(AutumnLayerName) as RectTransform;
            if (marker == null)
            {
                marker = NewRect(AutumnLayerName, root);
                Undo.RegisterCreatedObjectUndo(marker.gameObject, "Tasarim");
            }

            Undo.RecordObject(marker, "Tasarim");
            Stretch(marker, 0f, 0f, 0f, 0f);
            marker.localScale = Vector3.one;
            Image image = marker.GetComponent<Image>();
            if (overlay != null)
            {
                if (image == null)
                    image = Undo.AddComponent<Image>(marker.gameObject);
                Undo.RecordObject(image, "Cerceve");
                image.sprite = overlay;
                image.type = Image.Type.Simple;
                image.color = Color.white;
                image.raycastTarget = false;
            }

            return marker;
        }

        /// <summary>Kaydirilan liste resimdeki alana; panelin kendi resimleri saydam.</summary>
        private static void AutumnList(SerializedObject screen, RectTransform background, Vector4 view,
            Vector2 cell, Vector2 spacing, Vector2 padding, int columns)
        {
            Transform list = ScreenRef(screen, "scroll");
            RectTransform content = ScreenRef(screen, "listContent") as RectTransform;
            if (list == null && content != null)
                list = content.GetComponentInParent<ScrollRect>(true)?.transform;
            if (list is not RectTransform panel)
                return;

            ModeMap(panel, background, view);
            Undo.SetTransformParent(panel, background.parent, "Liste");
            panel.SetSiblingIndex(background.GetSiblingIndex() + 1);
            foreach (Image image in panel.GetComponentsInChildren<Image>(true))
            {
                if (content != null && image.transform.IsChildOf(content))
                    continue;

                Undo.RecordObject(image, "Saydam");
                image.color = new Color(1f, 1f, 1f, 0f);
            }

            if (panel.GetComponent<ScrollRect>()?.viewport is RectTransform viewport)
            {
                Undo.RecordObject(viewport, "Gorunum");
                Stretch(viewport, 0f, 0f, 0f, 0f);
            }

            if (content != null && content.GetComponent<GridLayoutGroup>() is GridLayoutGroup grid)
            {
                Undo.RecordObject(grid, "Kartlar");
                grid.cellSize = cell * DesignToCanvas;
                grid.spacing = spacing * DesignToCanvas;
                grid.padding = new RectOffset(Mathf.RoundToInt(padding.x * DesignToCanvas), 0,
                    Mathf.RoundToInt(padding.y * DesignToCanvas), Mathf.RoundToInt(20 * DesignToCanvas));
                grid.childAlignment = TextAnchor.UpperLeft;
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = columns;
            }
        }

        /// <summary>Kart icindeki nesneyi kart resminin piksellerine gore yerlestirir.</summary>
        private static void CardPlace(Transform target, Vector2 size, Vector4 px)
        {
            if (target is not RectTransform rect)
                return;

            Undo.RecordObject(rect, "Kart");
            rect.anchorMin = new Vector2(px.x / size.x, 1f - px.w / size.y);
            rect.anchorMax = new Vector2(px.z / size.x, 1f - px.y / size.y);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void CardText(Transform target, Vector2 size, Vector4 px, Color color, float max,
            TextAlignmentOptions alignment, bool shadow = false)
        {
            CardPlace(target, size, px);
            if (target == null || !target.TryGetComponent(out TextMeshProUGUI text))
                return;

            Undo.RecordObject(text, "Kart");
            text.enabled = true;
            text.color = color;
            text.fontSharedMaterial = shadow ? _shadowMaterial : _plainMaterial;
            text.alignment = alignment;
            text.enableAutoSizing = true;
            text.fontSizeMin = 10f;
            text.fontSizeMax = max;
            text.textWrappingMode = TextWrappingModes.NoWrap;
        }

        private static void CardImage(Transform target, Sprite sprite, Color color)
        {
            if (target == null || !target.TryGetComponent(out Image image))
                return;

            Undo.RecordObject(image, "Kart");
            if (sprite != null)
                image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.color = color;
            image.preserveAspect = true;
            image.enabled = true;
        }

        /// <summary>Islem butonu: resimdeki tahta; durumuna gore resim degisir, yazi canli.</summary>
        private static void AutumnAction(SerializedObject screen, RectTransform background, Vector4 rect, Vector4 label,
            string prefix, Color labelColor)
        {
            if (ScreenRef(screen, "actionButton") is not RectTransform action)
                return;

            Transform caption = ScreenRef(screen, "actionLabel");
            DesignHit(action, background, rect);
            Image own = action.GetComponent<Image>();
            Undo.RecordObject(own, "Buton");
            own.sprite = Sprite($"{prefix}_kapali");
            own.type = Image.Type.Simple;
            own.color = Color.white;
            screen.FindProperty("actionFace").objectReferenceValue = own;
            screen.FindProperty("actionLip").objectReferenceValue = null;
            screen.FindProperty("selectSprite").objectReferenceValue = Sprite($"{prefix}_sec");
            screen.FindProperty("buySprite").objectReferenceValue = Sprite($"{prefix}_al");
            screen.FindProperty("disabledSprite").objectReferenceValue = Sprite($"{prefix}_kapali");
            ModeOverlay(caption, background, label, labelColor, false);
            if (caption != null && caption.TryGetComponent(out TextMeshProUGUI text))
                text.fontSharedMaterial = _shadowMaterial;
        }

        private static bool ApplyShopDesign(ShopScreen screen)
        {
            var root = (RectTransform)screen.transform;
            RectTransform background = AutumnBackground(root, "magaza_arkaplan");
            if (background == null)
                return false;

            Undo.SetCurrentGroupName("Sonbahar magaza");
            var serialized = new SerializedObject(screen);
            if (root.Find("AltBaslik")?.GetComponent<Graphic>() is Graphic subtitle)
            {
                Undo.RecordObject(subtitle, "Gizle");
                subtitle.enabled = false; // resimde var
            }

            DesignHit(ScreenRef(serialized, "closeButton"), background, new Vector4(1855, 35, 1962, 132));
            ModeOverlay(ScreenRef(serialized, "coinText"), background, new Vector4(1552, 48, 1750, 102), Hex("#F6E6C8"), false);

            AutumnList(serialized, background, new Vector4(60, 195, 1295, 1065),
                new Vector2(391, 458), new Vector2(3, 12), new Vector2(37, 2), 3);
            if (serialized.FindProperty("cardTemplate").objectReferenceValue is ShopItemCard card)
                StyleShopCard(card);

            ModeMap(root.Find("KarakterGolgesi"), background, new Vector4(1540, 762, 1770, 812));
            ModeMap(ScreenRef(serialized, "characterView"), background, new Vector4(1490, 234, 1815, 816));
            ModeOverlay(ScreenRef(serialized, "previewNameText"), background, new Vector4(1525, 888, 1790, 945),
                Hex("#F3DEC0"), false);
            AutumnAction(serialized, background, new Vector4(1395, 925, 1905, 1092), new Vector4(1500, 955, 1800, 1055),
                "magaza_buton", Hex("#F6E6C8"));
            serialized.ApplyModifiedProperties();

            AutumnMarker(root, null);
            return true;
        }

        private static void StyleShopCard(ShopItemCard card)
        {
            var serialized = new SerializedObject(card);
            var size = new Vector2(391, 458);
            Color ink = Hex("#4A2A12");

            CardImage(ScreenRef(serialized, "background"), Sprite("magaza_kart"), Color.white);
            if (card.GetComponent<Image>() is Image own)
                own.preserveAspect = false;

            if (ScreenRef(serialized, "previewOutline") is Transform outline)
            {
                CardPlace(outline, size, new Vector4(-45, -17, 393, 483));
                CardImage(outline, Sprite("magaza_kart_secili"), Color.white);
                if (outline.TryGetComponent(out Image outlineImage))
                    outlineImage.preserveAspect = false;
                outline.SetAsFirstSibling();
            }

            CardText(ScreenRef(serialized, "nameText"), size, new Vector4(23, 43, 316, 93), ink, 46f, TextAlignmentOptions.Center);
            CardText(ScreenRef(serialized, "noteText"), size, new Vector4(40, 296, 351, 336), Hex("#5B3A1A"), 30f,
                TextAlignmentOptions.Center);
            CardPlace(ScreenRef(serialized, "icon"), size, new Vector4(110, 111, 280, 281));
            CardImage(ScreenRef(serialized, "ownedMark"), Sprite("magaza_tik"), Color.white);
            CardPlace(ScreenRef(serialized, "ownedMark"), size, new Vector4(316, 30, 368, 76));

            if (ScreenRef(serialized, "priceBackground") is Transform price)
            {
                CardPlace(price, size, new Vector4(38, 348, 358, 433));
                if (price.TryGetComponent(out Image plank))
                {
                    Undo.RecordObject(plank, "Gizle");
                    plank.enabled = false; // tahta kart resminde
                }

                foreach (Transform child in price)
                {
                    if (child.TryGetComponent(out Image lip) && child != ScreenRef(serialized, "priceCoin"))
                    {
                        Undo.RecordObject(lip, "Gizle");
                        lip.enabled = false;
                    }
                }
            }

            CardText(ScreenRef(serialized, "priceText"), new Vector2(320, 85), new Vector4(70, 6, 300, 78), Hex("#F6E6C8"), 50f,
                TextAlignmentOptions.Center, shadow: true);
            CardPlace(ScreenRef(serialized, "priceCoin"), new Vector2(320, 85), new Vector4(14, 10, 78, 74));

            serialized.FindProperty("symbolColor").colorValue = new Color(0.85f, 0.75f, 0.62f, 1f);
            foreach (string field in new[] { "buyColor", "adColor", "doneColor", "lockedColor", "comingSoonColor" })
                serialized.FindProperty(field).colorValue = Color.white;
            serialized.ApplyModifiedProperties();
        }

        private static bool ApplyCharacterDesign(LobbyAnimationScreen screen)
        {
            var root = (RectTransform)screen.transform;
            RectTransform background = AutumnBackground(root, "karakter_arkaplan");
            if (background == null)
                return false;

            SetSharpDesign($"{ArtFolder}/karakter_cerceve.png");
            Undo.SetCurrentGroupName("Sonbahar karakter");
            var serialized = new SerializedObject(screen);
            Color ink = Hex("#5A3214");

            DesignHit(ScreenRef(serialized, "closeButton"), background, new Vector4(1858, 25, 1962, 128));
            ModeOverlay(ScreenRef(serialized, "coinText"), background, new Vector4(1556, 40, 1748, 100), Color.white, false);
            ModeOverlay(ScreenRef(serialized, "subtitleText"), background, new Vector4(55, 112, 560, 172), ink, false,
                TextAlignmentOptions.Left);

            // Sekmeler: turuncu tahta; secili olmayan koyulasir.
            foreach ((string field, Vector4 rect) in new[]
                     {
                         ("charactersTab", new Vector4(534, 22, 852, 92)),
                         ("animationsTab", new Vector4(866, 22, 1184, 92)),
                     })
            {
                if (ScreenRef(serialized, field) is not RectTransform tab)
                    continue;

                TextMeshProUGUI label = tab.GetComponentInChildren<TextMeshProUGUI>(true);
                DesignHit(tab, background, rect);
                Image own = tab.GetComponent<Image>();
                Undo.RecordObject(own, "Sekme");
                own.sprite = Sprite("karakter_sekme");
                own.type = Image.Type.Simple;
                own.color = Color.white;
                if (label != null)
                {
                    Undo.RecordObject(label.rectTransform, "Sekme");
                    Undo.SetTransformParent(label.rectTransform, tab, "Sekme");
                    Stretch(label.rectTransform, 16f, 16f, 6f, 10f);
                    label.rectTransform.localScale = Vector3.one;
                    Undo.RecordObject(label, "Sekme");
                    label.enabled = true;
                    label.color = Color.white;
                    label.fontSharedMaterial = _shadowMaterial;
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 10f;
                    label.fontSizeMax = 44f;
                }
            }

            serialized.FindProperty("tabOnColor").colorValue = Color.white;
            serialized.FindProperty("tabOffColor").colorValue = new Color(0.8f, 0.5f, 0.4f, 1f);

            AutumnList(serialized, background, new Vector4(70, 190, 830, 1068),
                new Vector2(376, 160), new Vector2(4, 6), new Vector2(0, 0), 2);
            if (serialized.FindProperty("cardTemplate").objectReferenceValue is LobbyAnimationCard card)
                StyleCharacterCard(card);

            ModeMap(root.Find("KarakterGolgesi"), background, new Vector4(1250, 838, 1470, 892));
            ModeMap(ScreenRef(serialized, "characterView"), background, new Vector4(1180, 135, 1540, 913));
            ModeOverlay(ScreenRef(serialized, "previewNameText"), background, new Vector4(1262, 902, 1460, 965), ink, false);
            AutumnAction(serialized, background, new Vector4(1462, 928, 1960, 1092), new Vector4(1560, 965, 1860, 1055),
                "karakter_buton", Color.white);
            serialized.ApplyModifiedProperties();

            // Panel cercevesi ve yapraklari kartlarin onunde.
            RectTransform frame = AutumnMarker(root, Sprite("karakter_cerceve"));
            if (ScreenRef(serialized, "listContent")?.GetComponentInParent<ScrollRect>(true) is ScrollRect list)
                frame.SetSiblingIndex(list.transform.GetSiblingIndex() + 1);
            return true;
        }

        private static void StyleCharacterCard(LobbyAnimationCard card)
        {
            var serialized = new SerializedObject(card);
            var size = new Vector2(376, 160);

            CardImage(ScreenRef(serialized, "background"), Sprite("karakter_kart"), Color.white);
            if (card.GetComponent<Image>() is Image own)
                own.preserveAspect = false;

            CardText(ScreenRef(serialized, "nameText"), size, new Vector4(113, 50, 348, 95), Hex("#4A2A12"), 44f,
                TextAlignmentOptions.Left);
            CardText(ScreenRef(serialized, "statusText"), size, new Vector4(113, 100, 308, 132), Hex("#5B3A1A"), 30f,
                TextAlignmentOptions.Left);
            CardPlace(ScreenRef(serialized, "icon"), size, new Vector4(33, 45, 98, 110));
            CardImage(ScreenRef(serialized, "selectedMark"), Sprite("karakter_tik"), Color.white);
            CardPlace(ScreenRef(serialized, "selectedMark"), size, new Vector4(316, 30, 358, 68));
            CardPlace(ScreenRef(serialized, "lockMark"), size, new Vector4(318, 30, 356, 68));

            serialized.FindProperty("unlockedColor").colorValue = Color.white;
            serialized.FindProperty("lockedColor").colorValue = new Color(0.82f, 0.78f, 0.72f, 1f);
            serialized.ApplyModifiedProperties();
        }
        // ------------------------------------------------------------------
        // Mac sonu
        // ------------------------------------------------------------------

        /// <summary>Mac sonu 2. surum: kullanicinin bos orman arka plani, kare karakter alani.</summary>
        private const string MatchEndMarker = "MacSonu_v2";

        private static bool ApplyMatchEndDesign(MatchEndScreen screen)
        {
            var root = (RectTransform)screen.transform;
            RectTransform background = AutumnBackground(root, "macsonu_arkaplan");
            if (background == null)
                return false;

            Undo.SetCurrentGroupName("Sonbahar mac sonu");
            var serialized = new SerializedObject(screen);

            ModeOverlay(ScreenRef(serialized, "titleText"), background, new Vector4(110, 12, 1340, 160), Hex("#F7941D"),
                false, TextAlignmentOptions.Left);
            serialized.FindProperty("defeatTitleColor").colorValue = Hex("#F7941D");
            serialized.FindProperty("victoryTitleColor").colorValue = Hex("#FFC928");
            ModeOverlay(ScreenRef(serialized, "messageText"), background, new Vector4(112, 316, 1000, 392), Color.white,
                false, TextAlignmentOptions.Left);

            // Kartlar: resimdeki kart (ikon ve alt yazi resimde), sayi canli; ziplayarak gelir.
            (string value, string sprite, Vector4 rect)[] cards =
            {
                ("killsText", "macsonu_kart1", new Vector4(12, 518, 232, 806)),
                ("xpText", "macsonu_kart2", new Vector4(312, 518, 532, 806)),
                ("coinsText", "macsonu_kart3", new Vector4(612, 518, 832, 806)),
            };
            foreach ((string value, string sprite, Vector4 rect) in cards)
            {
                Transform number = ScreenRef(serialized, value);
                if (number == null || number.parent is not RectTransform card)
                    continue;

                ModeMap(card, background, rect);
                if (card.GetComponent<Image>() is not Image face)
                    face = Undo.AddComponent<Image>(card.gameObject);
                Undo.RecordObject(face, "Kart");
                face.sprite = Sprite(sprite);
                face.type = Image.Type.Simple;
                face.color = Color.white;
                face.preserveAspect = false;

                foreach (Graphic graphic in card.GetComponentsInChildren<Graphic>(true))
                {
                    if (graphic == face || graphic.transform == number)
                        continue;

                    Undo.RecordObject(graphic, "Gizle");
                    graphic.enabled = false; // ikon ve alt yazi resimde
                }

                CardText(number, new Vector2(220, 288), new Vector4(18, 128, 202, 214), Hex("#F7941D"), 96f,
                    TextAlignmentOptions.Center, shadow: true);
            }

            if (ScreenRef(serialized, "doubleXpBadge") is RectTransform badge)
            {
                ModeMap(badge, background, new Vector4(440, 518, 552, 606));
                if (badge.GetComponent<Image>() is not Image leaf)
                    leaf = Undo.AddComponent<Image>(badge.gameObject);
                Undo.RecordObject(leaf, "2x");
                leaf.sprite = Sprite("macsonu_2x");
                leaf.type = Image.Type.Simple;
                leaf.color = Color.white;
                foreach (Graphic graphic in badge.GetComponentsInChildren<Graphic>(true))
                {
                    if (graphic == leaf)
                        continue;

                    Undo.RecordObject(graphic, "Gizle");
                    graphic.enabled = false; // "2x" resimde
                }

                badge.SetAsLastSibling();
            }

            // Karakter goruntusu kare doku (512x512) ve karakter dokunun ust yarisinda: alan kare ve
            // buyuk tutulur ki karakter resimdeki yerde (ayaklar ~885) ve boyda dursun.
            ModeMap(ScreenRef(serialized, "characterImage"), background, new Vector4(968, 326, 2023, 1381));

            // Seviye tahtasi: tahta resimde; yazilar ve dolum canli.
            ModeOverlay(ScreenRef(serialized, "levelText"), background, new Vector4(712, 938, 952, 996), Hex("#DCD3DA"),
                false, TextAlignmentOptions.Left);
            ModeOverlay(ScreenRef(serialized, "xpNumbersText"), background, new Vector4(960, 938, 1212, 996), Color.white,
                false, TextAlignmentOptions.Left);
            ModeOverlay(ScreenRef(serialized, "nextLevelText"), background, new Vector4(1248, 986, 1316, 1058), Hex("#DCD3DA"),
                false);
            if (ScreenRef(serialized, "xpFill") is RectTransform fill && fill.parent is RectTransform track)
            {
                ModeMap(track, background, new Vector4(716, 1004, 1205, 1044));
                if (track.GetComponent<Image>() is Image trackImage)
                {
                    Undo.RecordObject(trackImage, "Gizle");
                    trackImage.enabled = false;
                }

                Undo.RecordObject(fill, "Dolum");
                fill.offsetMin = new Vector2(3f, 3f);
                fill.offsetMax = new Vector2(0f, -3f);
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

            ModeOverlay(ScreenRef(serialized, "levelUpBanner"), background, new Vector4(650, 805, 1370, 885), Hex("#FFC928"), false);

            foreach (string name in new[] { "Ok", "BattlePassKarti" })
            {
                if (root.Find(name) is not Transform old)
                    continue;

                foreach (Graphic graphic in old.GetComponentsInChildren<Graphic>(true))
                {
                    Undo.RecordObject(graphic, "Gizle");
                    graphic.enabled = false; // resimde ok var, battle pass karti yok
                }
            }

            // DEVAM ET: resimdeki buton (ziplayarak gelir).
            if (ScreenRef(serialized, "continueButton") is RectTransform continueButton)
            {
                DesignHit(continueButton, background, new Vector4(1470, 900, 1940, 1095));
                Image own = continueButton.GetComponent<Image>();
                Undo.RecordObject(own, "Devam");
                own.sprite = Sprite("macsonu_devam");
                own.type = Image.Type.Simple;
                own.color = Color.white;
            }

            serialized.ApplyModifiedProperties();
            AutumnMarker(root, null);
            if (root.Find(MatchEndMarker) == null)
            {
                RectTransform marker = NewRect(MatchEndMarker, root);
                Undo.RegisterCreatedObjectUndo(marker.gameObject, "Isaret");
            }

            return true;
        }
    }
}
#endif
