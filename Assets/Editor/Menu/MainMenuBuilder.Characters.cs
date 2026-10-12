#if UNITY_EDITOR
using System.Linq;
using NewBattle.Progression;
using NewBattle.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// Karakterler: karakter kutuphanesi (takim elbiseli + hediye melek),
    /// modellerin Humanoid ice aktarimi, karakter ekranindaki sekmeler,
    /// hediye kodu penceresi ve profildeki Oyuncu Kodu. Sadece eksikleri ekler.
    /// </summary>
    public static partial class MainMenuBuilder
    {
        private const string CharacterLibraryPath = "Assets/Resources/KarakterKutuphanesi.asset";
        private const string DefaultCharacterModelPath =
            "Assets/Arts/Characters/Character_V2/Meshy_AI_Tie_Guy_biped/Meshy_AI_Tie_Guy_biped_Character_output.fbx";
        private const string DefaultCharacterMaterialPath = "Assets/Arts/Characters/Character_Mat.mat";

        private const string AngelFolder = "Assets/Arts/Characters/Character_Angel/Meshy_AI_Little_Halo_Angel_biped";
        private const string AngelModelPath = AngelFolder + "/Meshy_AI_Little_Halo_Angel_biped_Character_output.fbx";
        private const string AngelTexturePath = AngelFolder + "/Meshy_AI_Little_Halo_Angel_biped_texture_0.png";
        private const string AngelMaterialPath = "Assets/Arts/Characters/Character_Angel/Angel_Mat.mat";

        /// <summary>Kutuphaneyi olusturur / eksik karakterleri ekler. Eklenenleri dondurur.</summary>
        public static string EnsureCharacterLibrary(out CharacterLibrary library)
        {
            var added = new System.Collections.Generic.List<string>();

            library = AssetDatabase.LoadAssetAtPath<CharacterLibrary>(CharacterLibraryPath);
            if (library == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                    AssetDatabase.CreateFolder("Assets", "Resources");

                library = ScriptableObject.CreateInstance<CharacterLibrary>();
                AssetDatabase.CreateAsset(library, CharacterLibraryPath);
            }

            if (library.Find("takim_elbiseli") == null)
            {
                library.EditorSet(new CharacterDefinition
                {
                    id = "takim_elbiseli",
                    displayName = "Takım Elbiseli",
                    model = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultCharacterModelPath),
                    avatar = LoadAvatar(DefaultCharacterModelPath),
                    material = AssetDatabase.LoadAssetAtPath<Material>(DefaultCharacterMaterialPath),
                    unlock = CharacterUnlockType.Free
                }, makeDefault: true);
                added.Add("Takım Elbiseli");
            }

            if (library.Find("melek") == null && AssetDatabase.LoadAssetAtPath<GameObject>(AngelModelPath) != null)
            {
                PrepareHumanoid(AngelModelPath);
                Avatar avatar = LoadAvatar(AngelModelPath);
                if (avatar == null || !avatar.isHuman)
                {
                    Debug.LogError("Melek modeli Humanoid olarak alinamadi: " + AngelModelPath +
                                   " > Inspector > Rig sekmesini kontrol et.");
                }
                else
                {
                    library.EditorSet(new CharacterDefinition
                    {
                        id = "melek",
                        displayName = "Küçük Hale Meleği",
                        model = AssetDatabase.LoadAssetAtPath<GameObject>(AngelModelPath),
                        avatar = avatar,
                        material = EnsureAngelMaterial(),
                        removeObjects = new[] { "Icosphere" },
                        unlock = CharacterUnlockType.Gift
                    }, makeDefault: false);
                    added.Add("Küçük Hale Meleği (hediye)");
                }
            }

            // Meshy'nin melek dosyasinda karakterin yaninda bir top (Icosphere) var.
            CharacterDefinition angel = library.Find("melek");
            if (angel != null && (angel.removeObjects == null || angel.removeObjects.Length == 0))
            {
                angel.removeObjects = new[] { "Icosphere" };
                added.Add("Melek: fazladan top kaldırıldı");
            }

            if (added.Count > 0)
            {
                EditorUtility.SetDirty(library);
                AssetDatabase.SaveAssets();
            }

            return string.Join(", ", added);
        }

        /// <summary>Modeli Humanoid yapar (oyunun tum animasyonlari ona uysun). Icindeki animasyon gerekmiyor.</summary>
        private static void PrepareHumanoid(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
                return;

            if (importer.animationType == ModelImporterAnimationType.Human && !importer.importAnimation)
                return;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;
            importer.SaveAndReimport();
        }

        private static Avatar LoadAvatar(string modelPath) =>
            AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();

        /// <summary>Ilk karakterin materyalinin kopyasi + meleginin dokusu (ayni gorunum tarzi).</summary>
        private static Material EnsureAngelMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(AngelMaterialPath);
            if (material != null)
                return material;

            if (!AssetDatabase.CopyAsset(DefaultCharacterMaterialPath, AngelMaterialPath))
                return null;

            material = AssetDatabase.LoadAssetAtPath<Material>(AngelMaterialPath);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AngelTexturePath);
            if (material != null && texture != null)
            {
                if (material.HasProperty("_BaseMap"))
                    material.SetTexture("_BaseMap", texture);
                if (material.HasProperty("_MainTex"))
                    material.SetTexture("_MainTex", texture);

                // Acik renkli (beyaz elbise) karakterde golge tarafi grilesmesin.
                if (material.HasProperty("_Ambient"))
                    material.SetFloat("_Ambient", 0.92f);
                if (material.HasProperty("_ShadeThreshold"))
                    material.SetFloat("_ShadeThreshold", -0.3f);
                if (material.HasProperty("_RimStrength"))
                    material.SetFloat("_RimStrength", 0.22f);
                if (material.HasProperty("_ShadowTint"))
                    material.SetColor("_ShadowTint", new Color(0.9f, 0.9f, 0.97f, 1f));
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        private const string GiftCodeCatalogPath = "Assets/Resources/HediyeKodlari.asset";

        /// <summary>Hediye kodlari dosyasini olusturur (ornek: melek kodu). Olusturduysa true.</summary>
        public static bool EnsureGiftCodeCatalog(out GiftCodeCatalog catalog)
        {
            catalog = AssetDatabase.LoadAssetAtPath<GiftCodeCatalog>(GiftCodeCatalogPath);
            if (catalog != null)
                return false;

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            catalog = ScriptableObject.CreateInstance<GiftCodeCatalog>();
            catalog.codes.Add(new GiftCode
            {
                code = "HALEMELEK",
                note = "Melek karakteri hediyesi (kodu istediğin gibi değiştir)",
                reward = GiftRewardType.Character,
                itemId = "melek"
            });
            AssetDatabase.CreateAsset(catalog, GiftCodeCatalogPath);
            AssetDatabase.SaveAssets();
            return true;
        }

        // ------------------------------------------------------------------
        // Karakter ekrani: KARAKTERLER / ANIMASYONLAR sekmeleri
        // ------------------------------------------------------------------

        private static bool EnsureCharacterTabs(MainMenuScreen menu)
        {
            LobbyAnimationScreen screen = menu.GetComponentInChildren<LobbyAnimationScreen>(true);
            if (screen == null)
                return false;

            var serialized = new SerializedObject(screen);
            SerializedProperty charactersTab = serialized.FindProperty("charactersTab");
            if (charactersTab.objectReferenceValue != null)
                return false;

            var root = (RectTransform)screen.transform;
            Vector2 topLeft = new(0f, 1f);

            Button characters = AddButton(root, "KarakterlerSekmesi", topLeft, topLeft, new Vector2(520f, -34f),
                new Vector2(300f, 84f), Yellow, YellowLip, lipHeight: 7f);
            TextMeshProUGUI charactersLabel = AddText(Face(characters), "Yazi", "KARAKTERLER", 38f, Color.white, TextAlignmentOptions.Center, shadow: true);
            Stretch(charactersLabel.rectTransform, 8f, 8f, 0f, 3f);
            Undo.RegisterCreatedObjectUndo(characters.gameObject, "Karakter sekmeleri");

            Button animations = AddButton(root, "AnimasyonlarSekmesi", topLeft, topLeft, new Vector2(840f, -34f),
                new Vector2(300f, 84f), Yellow, YellowLip, lipHeight: 7f);
            TextMeshProUGUI animationsLabel = AddText(Face(animations), "Yazi", "ANİMASYONLAR", 38f, Color.white, TextAlignmentOptions.Center, shadow: true);
            Stretch(animationsLabel.rectTransform, 8f, 8f, 0f, 3f);
            Undo.RegisterCreatedObjectUndo(animations.gameObject, "Karakter sekmeleri");

            charactersTab.objectReferenceValue = characters;
            serialized.FindProperty("animationsTab").objectReferenceValue = animations;
            serialized.FindProperty("subtitleText").objectReferenceValue = root.Find("AltBaslik")?.GetComponent<TextMeshProUGUI>();
            serialized.ApplyModifiedProperties();
            return true;
        }

        // ------------------------------------------------------------------
        // Hediye kodu: Ayarlar satiri + pencere, profilde Oyuncu Kodu
        // ------------------------------------------------------------------

        private static bool EnsureGiftCodeUi(MainMenuScreen menu)
        {
            bool changed = false;
            var root = (RectTransform)menu.transform;

            SettingsScreen settings = menu.GetComponentInChildren<SettingsScreen>(true);
            if (settings != null)
            {
                var serialized = new SerializedObject(settings);
                SerializedProperty button = serialized.FindProperty("giftCodeButton");
                if (button.objectReferenceValue == null && settings.transform.Find("Pencere") is RectTransform window)
                {
                    // Pencere bir satir uzar, yeni satir en alta.
                    Undo.RecordObject(window, "Hediye kodu");
                    window.sizeDelta += new Vector2(0f, 130f);
                    float y = -window.sizeDelta.y + 270f; // en alttaki satirin bir altina

                    SettingsRowLabel(window, "HediyeKoduYazisi", "HEDİYE KODU", y);
                    Button giftButton = SettingsButton(window, "HediyeKoduButonu", "GİR", y, Purple, PinkLip, out _);
                    Undo.RegisterCreatedObjectUndo(window.Find("HediyeKoduYazisi").gameObject, "Hediye kodu");
                    Undo.RegisterCreatedObjectUndo(giftButton.gameObject, "Hediye kodu");

                    GiftCodePopup popup = menu.GetComponentInChildren<GiftCodePopup>(true);
                    if (popup == null)
                        popup = BuildGiftCodePopup(root, menu);

                    button.objectReferenceValue = giftButton;
                    serialized.FindProperty("giftCodePopup").objectReferenceValue = popup;
                    serialized.ApplyModifiedProperties();
                    changed = true;
                }
            }

            // Kisiye ozel kod sistemi kaldirildi: profildeki "Oyuncu Kodu" satiri gereksiz.
            ProfileScreen profile = menu.GetComponentInChildren<ProfileScreen>(true);
            if (profile != null && profile.transform.Find("Pencere") is RectTransform profileWindow)
            {
                bool removed = false;
                foreach (string name in new[] { "OyuncuKodu", "KopyalaButonu" })
                {
                    Transform part = profileWindow.Find(name);
                    if (part == null)
                        continue;

                    Undo.DestroyObjectImmediate(part.gameObject);
                    removed = true;
                }

                if (removed && Mathf.Approximately(profileWindow.sizeDelta.y, 920f))
                {
                    Undo.RecordObject(profileWindow, "Oyuncu kodu");
                    profileWindow.sizeDelta = new Vector2(profileWindow.sizeDelta.x, 800f);
                }

                changed |= removed;
            }

            return changed;
        }

        private static GiftCodePopup BuildGiftCodePopup(RectTransform menuRoot, MainMenuScreen menu)
        {
            (RectTransform root, RectTransform window) = PopupShell(menuRoot, "HediyeKoduPenceresi", "HEDİYE KODU", new Vector2(960f, 560f));

            TMP_InputField input = CreateInputField(window, "KodKutusu", "Kodu yaz");
            TextMeshProUGUI hint = AddText(window, "Ipucu", "Hediye kodunu yaz", 32f, ResultSoftText, TextAlignmentOptions.Center);
            Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -45f), new Vector2(780f, 50f));

            (Button cancel, Button redeem) = PopupButtons(window, "VAZGEÇ", "KULLAN");

            GiftCodePopup popup = root.gameObject.AddComponent<GiftCodePopup>();
            var serialized = new SerializedObject(popup);
            serialized.FindProperty("input").objectReferenceValue = input;
            serialized.FindProperty("redeemButton").objectReferenceValue = redeem;
            serialized.FindProperty("cancelButton").objectReferenceValue = cancel;
            serialized.FindProperty("hintText").objectReferenceValue = hint;
            serialized.FindProperty("menu").objectReferenceValue = menu;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            root.gameObject.SetActive(false);
            return popup;
        }

        /// <summary>Krem renkli, ortali yazili tek satirlik giris kutusu.</summary>
        private static TMP_InputField CreateInputField(RectTransform parent, string name, string placeholderText)
        {
            GameObject inputObject = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
            inputObject.name = name;
            SetLayerRecursive(inputObject.transform, 5);
            var inputRect = (RectTransform)inputObject.transform;
            inputRect.SetParent(parent, false);
            Place(inputRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 50f), new Vector2(780f, 120f));

            Image inputImage = inputObject.GetComponent<Image>();
            inputImage.sprite = Sprite("ui_round");
            inputImage.type = Image.Type.Sliced;
            inputImage.color = new Color(1f, 0.97f, 0.9f, 1f);

            TMP_InputField input = inputObject.GetComponent<TMP_InputField>();
            input.lineType = TMP_InputField.LineType.SingleLine;
            foreach (TextMeshProUGUI text in inputObject.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                text.font = _font;
                text.fontSharedMaterial = _plainMaterial;
                text.fontSize = 56f;
                text.alignment = TextAlignmentOptions.Center;
                text.color = text == input.placeholder ? new Color(BrownText.r, BrownText.g, BrownText.b, 0.45f) : BrownDark;
            }

            if (input.placeholder is TextMeshProUGUI placeholder)
                placeholder.text = placeholderText;

            return input;
        }
    }
}
#endif
