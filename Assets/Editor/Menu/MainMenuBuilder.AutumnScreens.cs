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

            bool Needs(Component screen) => screen != null && (force || screen.transform.Find(AutumnLayerName) == null);
            if (!Needs(settings) && !Needs(profile))
                return;

            if (!PrepareFont())
                return;
            PrepareMaterials();

            if (Needs(settings))
                changed |= ApplySettingsDesign(settings);
            if (Needs(profile))
                changed |= ApplyProfileDesign(profile);

            if (changed)
            {
                Component any = settings != null ? settings : profile;
                EditorSceneManager.MarkSceneDirty(any.gameObject.scene);
                EditorSceneManager.SaveScene(any.gameObject.scene);
                Debug.Log("Sonbahar ekranlari: tasarim resimleri uygulandi (Profil, Ayarlar).");
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
    }
}
#endif
