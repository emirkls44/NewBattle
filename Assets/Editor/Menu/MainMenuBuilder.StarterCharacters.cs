#if UNITY_EDITOR
using NewBattle.Progression;
using NewBattle.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// Baslangic karakterleri ve ilk acilis akisi:
    ///   - Ahmet (erkek) ve Ayse (kiz): Humanoid ice aktarim, kendi dokulariyla materyal,
    ///     kutuphaneye "baslangic" olarak; secilen oyuncunun olur, digeri seviyeyle acilir.
    ///   - Eski takim elbiseli karakter kutuphaneden kalkar (prefab'daki modeli sadece
    ///     iskelet tasiyicisi; oyunda her zaman secili karaktere donusturulur).
    ///   - Isim penceresine ilk acilista tam ekran mor arka plan ("İSMİNİ GİR").
    ///   - "KARAKTERİNİ SEÇ" ekrani: solda Ahmet, sagda Ayse.
    /// </summary>
    public static partial class MainMenuBuilder
    {
        private const string OldCharacterId = "takim_elbiseli";
        private const int OtherStarterLevel = 5;

        private struct StarterSpec
        {
            public string Id;
            public string Name;
            public string Folder;
            public string File;
        }

        private static readonly StarterSpec[] StarterSpecs =
        {
            new() { Id = "ahmet", Name = "Ahmet", Folder = "Assets/Arts/Characters/Character_Ahmet", File = "Ahmet" },
            new() { Id = "ayse", Name = "Ayşe", Folder = "Assets/Arts/Characters/Character_Ayse", File = "Ayse" }
        };

        private static string EnsureStarterCharacters(CharacterLibrary library)
        {
            var added = new System.Collections.Generic.List<string>();

            foreach (StarterSpec spec in StarterSpecs)
            {
                string modelPath = $"{spec.Folder}/{spec.File}.fbx";

                // Var olan karakterde de: kemik eslestirmesi elle yapilmamissa yap.
                if (library.Find(spec.Id) != null)
                {
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(modelPath) != null && PrepareStarterModel(modelPath))
                        added.Add($"{spec.Name}: kemik eşleştirmesi düzeltildi");
                    continue;
                }

                if (AssetDatabase.LoadAssetAtPath<GameObject>(modelPath) == null)
                {
                    Debug.LogError($"Karakter modeli yok: {modelPath}");
                    continue;
                }

                PrepareStarterModel(modelPath);
                Avatar avatar = LoadAvatar(modelPath);
                if (avatar == null || !avatar.isHuman)
                {
                    Debug.LogError($"{spec.Name} modeli Humanoid olarak alinamadi: {modelPath} > Inspector > Rig sekmesini kontrol et.");
                    continue;
                }

                library.EditorSet(new CharacterDefinition
                {
                    id = spec.Id,
                    displayName = spec.Name,
                    model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath),
                    avatar = avatar,
                    material = EnsureCharacterMaterial($"{spec.Folder}/{spec.File}_Mat.mat", $"{spec.Folder}/{spec.File}_texture.png"),
                    starter = true,
                    unlock = CharacterUnlockType.Level,
                    requirement = OtherStarterLevel
                }, makeDefault: spec.Id == "ahmet");
                added.Add(spec.Name);
            }

            if (library.EditorRemove(OldCharacterId))
                added.Add("eski karakter kaldırıldı");

            if (added.Count > 0)
            {
                EditorUtility.SetDirty(library);
                AssetDatabase.SaveAssets();
            }

            return string.Join(", ", added);
        }

        /// <summary>
        /// Humanoid, animasyonsuz; FBX'e gomulu 8K doku/materyal ice aktarilmaz (kendi materyalimiz var).
        ///
        /// Kemik eslestirmesi ELLE yapilir: Unity'nin otomatik eslestirmesi Meshy SmartRig
        /// iskeletinde (headfront, Bone_0xx gibi fazladan kemikler, SmartRigArmature koku)
        /// karakteri yana donuk ve one egik gosteriyordu. Model dosyasi kusursuz T-pozda;
        /// referans poz dogrudan ondan alinir. Degisiklik yaptiysa true.
        /// </summary>
        private static bool PrepareStarterModel(string path)
        {
            if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
                return false;

            bool mapped = importer.humanDescription.human != null && importer.humanDescription.human.Length > 0;
            if (importer.animationType == ModelImporterAnimationType.Human && !importer.importAnimation &&
                importer.materialImportMode == ModelImporterMaterialImportMode.None && mapped)
                return false;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            HumanDescription description = importer.humanDescription;
            if (model != null && BuildSmartRigHuman(model, ref description, out int boneCount))
            {
                importer.humanDescription = description;
                Debug.Log($"{model.name}: Humanoid kemik eslestirmesi elle yapildi ({boneCount} kemik).");
            }

            importer.SaveAndReimport();

            Avatar avatar = LoadAvatar(path);
            Debug.Log($"{System.IO.Path.GetFileNameWithoutExtension(path)} avatari: " +
                      (avatar == null ? "YOK" : $"Humanoid={avatar.isHuman}, Gecerli={avatar.isValid}"));
            return true;
        }

        /// <summary>Unity Humanoid adi -> SmartRig/Mixamo kemik adi (onek haric).</summary>
        private static readonly (string human, string bone)[] SmartRigMap =
        {
            ("Hips", "Hips"), ("Spine", "Spine"), ("Chest", "Spine1"), ("UpperChest", "Spine2"),
            ("Neck", "Neck"), ("Head", "Head"),
            ("LeftShoulder", "LeftShoulder"), ("LeftUpperArm", "LeftArm"), ("LeftLowerArm", "LeftForeArm"), ("LeftHand", "LeftHand"),
            ("RightShoulder", "RightShoulder"), ("RightUpperArm", "RightArm"), ("RightLowerArm", "RightForeArm"), ("RightHand", "RightHand"),
            ("LeftUpperLeg", "LeftUpLeg"), ("LeftLowerLeg", "LeftLeg"), ("LeftFoot", "LeftFoot"), ("LeftToes", "LeftToeBase"),
            ("RightUpperLeg", "RightUpLeg"), ("RightLowerLeg", "RightLeg"), ("RightFoot", "RightFoot"), ("RightToes", "RightToeBase"),
            ("Left Thumb Proximal", "LeftHandThumb1"), ("Left Thumb Intermediate", "LeftHandThumb2"), ("Left Thumb Distal", "LeftHandThumb3"),
            ("Left Index Proximal", "LeftHandIndex1"), ("Left Index Intermediate", "LeftHandIndex2"), ("Left Index Distal", "LeftHandIndex3"),
            ("Left Middle Proximal", "LeftHandMiddle1"), ("Left Middle Intermediate", "LeftHandMiddle2"), ("Left Middle Distal", "LeftHandMiddle3"),
            ("Left Ring Proximal", "LeftHandRing1"), ("Left Ring Intermediate", "LeftHandRing2"), ("Left Ring Distal", "LeftHandRing3"),
            ("Left Little Proximal", "LeftHandPinky1"), ("Left Little Intermediate", "LeftHandPinky2"), ("Left Little Distal", "LeftHandPinky3"),
            ("Right Thumb Proximal", "RightHandThumb1"), ("Right Thumb Intermediate", "RightHandThumb2"), ("Right Thumb Distal", "RightHandThumb3"),
            ("Right Index Proximal", "RightHandIndex1"), ("Right Index Intermediate", "RightHandIndex2"), ("Right Index Distal", "RightHandIndex3"),
            ("Right Middle Proximal", "RightHandMiddle1"), ("Right Middle Intermediate", "RightHandMiddle2"), ("Right Middle Distal", "RightHandMiddle3"),
            ("Right Ring Proximal", "RightHandRing1"), ("Right Ring Intermediate", "RightHandRing2"), ("Right Ring Distal", "RightHandRing3"),
            ("Right Little Proximal", "RightHandPinky1"), ("Right Little Intermediate", "RightHandPinky2"), ("Right Little Distal", "RightHandPinky3"),
        };

        /// <summary>
        /// Model hiyerarsisinden HumanDescription kurar: eslestirme = SmartRigMap (bulunanlar),
        /// iskelet = modelin dinlenme (T) pozu. Zorunlu kemiklerden biri yoksa false.
        /// </summary>
        private static bool BuildSmartRigHuman(GameObject model, ref HumanDescription description, out int boneCount)
        {
            boneCount = 0;
            Transform[] transforms = model.GetComponentsInChildren<Transform>(true);

            Transform FindBone(string name)
            {
                foreach (Transform t in transforms)
                {
                    string plain = t.name;
                    int colon = plain.LastIndexOf(':');
                    if (colon >= 0)
                        plain = plain.Substring(colon + 1);
                    if (plain == name)
                        return t;
                }

                return null;
            }

            var human = new System.Collections.Generic.List<HumanBone>();
            foreach ((string humanName, string boneName) in SmartRigMap)
            {
                Transform bone = FindBone(boneName);
                if (bone == null)
                    continue;

                var entry = new HumanBone { humanName = humanName, boneName = bone.name };
                entry.limit.useDefaultValues = true;
                human.Add(entry);
            }

            string[] required =
            {
                "Hips", "Spine", "Head", "LeftUpperArm", "LeftLowerArm", "LeftHand", "RightUpperArm", "RightLowerArm",
                "RightHand", "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "RightUpperLeg", "RightLowerLeg", "RightFoot"
            };
            foreach (string name in required)
            {
                if (!human.Exists(h => h.humanName == name))
                {
                    Debug.LogError($"{model.name}: zorunlu kemik bulunamadi ({name}); otomatik eslestirme kullanilacak.");
                    return false;
                }
            }

            var skeleton = new SkeletonBone[transforms.Length];
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                skeleton[i] = new SkeletonBone
                {
                    name = t.name,
                    position = t.localPosition,
                    rotation = t.localRotation,
                    scale = t.localScale
                };
            }

            description.human = human.ToArray();
            description.skeleton = skeleton;
            description.hasTranslationDoF = false;
            boneCount = human.Count;
            return true;
        }

        /// <summary>Karakter materyalinin kopyasi + karakterin dokusu (melekle ayni tarz).</summary>
        private static Material EnsureCharacterMaterial(string materialPath, string texturePath)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material != null)
                return material;

            if (AssetImporter.GetAtPath(texturePath) is TextureImporter textureImporter && textureImporter.maxTextureSize > 2048)
            {
                textureImporter.maxTextureSize = 2048;
                textureImporter.SaveAndReimport();
            }

            if (!AssetDatabase.CopyAsset(DefaultCharacterMaterialPath, materialPath))
                return null;

            material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (material != null && texture != null)
            {
                if (material.HasProperty("_BaseMap"))
                    material.SetTexture("_BaseMap", texture);
                if (material.HasProperty("_MainTex"))
                    material.SetTexture("_MainTex", texture);
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        // ------------------------------------------------------------------
        // Ilk acilis: isim penceresi arka plani + karakter secim ekrani
        // ------------------------------------------------------------------

        private const string FirstLaunchBackground = "bg_purple";

        private static bool EnsureFirstLaunchFlow(MainMenuScreen menu)
        {
            bool changed = false;
            var root = (RectTransform)menu.transform;

            // Isim penceresi: ilk acilista tam ekran opak arka plan, baslik "İSMİNİ GİR".
            NamePopup namePopup = menu.GetComponentInChildren<NamePopup>(true);
            if (namePopup != null)
            {
                var serialized = new SerializedObject(namePopup);
                SerializedProperty background = serialized.FindProperty("firstTimeBackground");
                if (background != null && background.objectReferenceValue == null)
                {
                    RectTransform plate = NewRect("IlkAcilisArkaPlani", namePopup.transform);
                    Undo.RegisterCreatedObjectUndo(plate.gameObject, "Ilk acilis arka plani");
                    Stretch(plate, 0f, 0f, 0f, 0f);
                    plate.SetAsFirstSibling(); // karartma ve pencerenin arkasinda
                    Image image = AddImage(plate, Sprite(FirstLaunchBackground), Color.white, raycast: true);
                    image.type = Image.Type.Simple;
                    plate.gameObject.SetActive(false);
                    background.objectReferenceValue = plate.gameObject;
                    changed = true;
                }

                SerializedProperty title = serialized.FindProperty("firstTimeTitle");
                if (title != null && title.stringValue == "KULLANICI ADIN NE OLSUN?")
                {
                    title.stringValue = "İSMİNİ GİR";
                    changed = true;
                }

                serialized.ApplyModifiedProperties();
            }

            // Karakter secim ekrani.
            var menuSerialized = new SerializedObject(menu);
            SerializedProperty starterProperty = menuSerialized.FindProperty("starterScreen");
            if (starterProperty != null && starterProperty.objectReferenceValue == null)
            {
                StarterCharacterScreen screen = menu.GetComponentInChildren<StarterCharacterScreen>(true) ?? BuildStarterScreen(root);
                starterProperty.objectReferenceValue = screen;
                menuSerialized.ApplyModifiedProperties();
                changed = true;
            }

            return changed;
        }

        private static StarterCharacterScreen BuildStarterScreen(RectTransform menuRoot)
        {
            RectTransform root = NewRect("KarakterSecimEkrani", menuRoot);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Karakter secim ekrani");
            Stretch(root, 0f, 0f, 0f, 0f);

            RectTransform plate = NewRect("ArkaPlan", root);
            Stretch(plate, 0f, 0f, 0f, 0f);
            AddImage(plate, Sprite(FirstLaunchBackground), Color.white, raycast: true).type = Image.Type.Simple;

            TextMeshProUGUI title = AddText(root, "Baslik", "KARAKTERİNİ SEÇ", 86f, Color.white, TextAlignmentOptions.Center, shadow: true);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(1400f, 120f));

            StarterCharacterScreen.Slot left = BuildStarterSlot(root, "SolKart", -330f, "ahmet", "Ahmet");
            StarterCharacterScreen.Slot right = BuildStarterSlot(root, "SagKart", 330f, "ayse", "Ayşe");

            TextMeshProUGUI hint = AddText(root, "Ipucu", "Bir karaktere dokun", 34f, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Center);
            Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 168f), new Vector2(1200f, 50f));

            Vector2 bottom = new(0.5f, 0f);
            Button confirm = AddButton(root, "DevamButonu", bottom, bottom, new Vector2(0f, 40f), new Vector2(460f, 116f), Green, GreenLip);
            TextMeshProUGUI confirmLabel = AddText(Face(confirm), "Yazi", "DEVAM ET", 58f, Color.white, TextAlignmentOptions.Center, shadow: true);
            Stretch(confirmLabel.rectTransform, 12f, 12f, 0f, 4f);

            StarterCharacterScreen component = root.gameObject.AddComponent<StarterCharacterScreen>();
            var serialized = new SerializedObject(component);
            WriteSlot(serialized.FindProperty("left"), left);
            WriteSlot(serialized.FindProperty("right"), right);
            serialized.FindProperty("confirmButton").objectReferenceValue = confirm;
            serialized.FindProperty("hintText").objectReferenceValue = hint;
            serialized.FindProperty("animations").objectReferenceValue = LobbyAnimationSetup.EnsureLibrary();
            serialized.ApplyModifiedPropertiesWithoutUndo();

            root.gameObject.SetActive(false);
            return component;
        }

        /// <summary>Kart: cerceve + koyu zemin + 3B onizleme, altinda isim.</summary>
        private static StarterCharacterScreen.Slot BuildStarterSlot(RectTransform root, string name, float x, string id, string displayName)
        {
            Vector2 center = new(0.5f, 0.5f);
            RectTransform card = NewRect(name, root);
            Place(card, center, center, new Vector2(x, 10f), new Vector2(480f, 620f));

            Image background = AddImage(card, Sprite("ui_round"), new Color(0f, 0f, 0f, 0.28f), raycast: true);
            background.pixelsPerUnitMultiplier = 1.4f;
            Button button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            card.gameObject.AddComponent<UIPressScale>();

            RectTransform frameRect = NewRect("Cerceve", card);
            Stretch(frameRect, -6f, -6f, -6f, -6f);
            Image frame = AddImage(frameRect, Sprite("ui_round_outline"), new Color(1f, 1f, 1f, 0.25f));
            frame.pixelsPerUnitMultiplier = 1.4f;

            RectTransform previewRect = NewRect("Onizleme", card);
            Stretch(previewRect, 12f, 12f, 12f, 96f);
            RawImage preview = previewRect.gameObject.AddComponent<RawImage>();
            preview.raycastTarget = false;
            preview.color = new Color(1f, 1f, 1f, 0f); // doku gelene kadar gorunmesin

            TextMeshProUGUI nameText = AddText(card, "Isim", displayName, 64f, Color.white, TextAlignmentOptions.Center, shadow: true);
            Place(nameText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(440f, 80f));

            return new StarterCharacterScreen.Slot
            {
                characterId = id,
                card = button,
                frame = frame,
                preview = preview,
                nameText = nameText
            };
        }

        private static void WriteSlot(SerializedProperty property, StarterCharacterScreen.Slot slot)
        {
            property.FindPropertyRelative("characterId").stringValue = slot.characterId;
            property.FindPropertyRelative("card").objectReferenceValue = slot.card;
            property.FindPropertyRelative("frame").objectReferenceValue = slot.frame;
            property.FindPropertyRelative("preview").objectReferenceValue = slot.preview;
            property.FindPropertyRelative("nameText").objectReferenceValue = slot.nameText;
        }
    }
}
#endif
