#if UNITY_EDITOR
using System.Collections.Generic;
using NewBattle.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// ASAMA 1: Battlelands tarzi ana menuyu sahneye kurar.
    ///
    /// Tek seferlik bir kurulum aracidir: sahneye duzenlenebilir objeler yazar
    /// ve cekilir. Sonrasinda yerlesimi Inspector'dan degistirebilirsin; bu
    /// arac bir daha karismaz (tekrar calistirirsan sorar, sonra bastan kurar).
    ///
    /// Yaptiklari:
    ///   1. "TitanOne SDF" fontunun kaynagini, buyuk "İ" harfi eklenmis kopyaya
    ///      (NBDisplay-Regular.ttf) cevirir.
    ///   2. Eski menuyu (MainMenu + NetworkMenuUI + MainMenuFlow) kapatir;
    ///      silmez, "MainMenu_Eski" olarak gizli durur.
    ///   3. Game_UI altina "AnaMenu"yu, dunyaya "MenuKarakterSahnesi"ni kurar.
    /// </summary>
    public static partial class MainMenuBuilder
    {
        private const string FontAssetPath = "Assets/Fonts/TitanOne SDF.asset";
        private const string PatchedFontPath = "Assets/Fonts/NBDisplay-Regular.ttf";
        private const string ArtFolder = "Assets/GameArt/MenuUI";
        private const string MaterialFolder = ArtFolder + "/Materials";
        private const string CharacterPrefabPath = "Assets/Arts/Characters/Character_V2/Prefabs/Character_V2_Test.prefab";

        public const string RootName = "AnaMenu";
        private const string ModePanelName = "OyunModuEkrani";
        private const string LoadingPrefabPath = "Assets/Prefabs/UI/YuklemeEkrani.prefab";
        private const string StageName = "MenuKarakterSahnesi";
        private static readonly Vector3 StagePosition = new(5000f, -2000f, 5000f);

        private const float TopBarHeight = 150f;
        private const float BottomBarHeight = 170f;

        // Battlelands ekranlarindan alinan renkler.
        private static readonly Color BrownDark = Hex("#4E2E10");
        private static readonly Color BrownText = Hex("#5A3812");
        private static readonly Color Gold = Hex("#FFC83A");
        private static readonly Color CardFill = Hex("#F8C640");
        private static readonly Color CardEdge = Hex("#D9951A");
        private static readonly Color Orange = Hex("#F9A61C");
        private static readonly Color OrangeLip = Hex("#C9700C");
        private static readonly Color Blue = Hex("#45A2F2");
        private static readonly Color BlueLip = Hex("#2468B8");
        private static readonly Color Green = Hex("#5CC437");
        private static readonly Color GreenLip = Hex("#3B8D1F");
        private static readonly Color Yellow = Hex("#FFC61A");
        private static readonly Color YellowLip = Hex("#D88A06");
        private static readonly Color Navy = Hex("#2D2752");
        private static readonly Color Purple = Hex("#B53FD3");
        private static readonly Color PurpleEdge = Hex("#F05CC6");
        private static readonly Color PurpleHeader = Hex("#3A2C6E");
        private static readonly Color Teal = Hex("#22BFA0");
        private static readonly Color TealHeader = Hex("#179A80");
        private static readonly Color Pink = Hex("#FF72B6");
        private static readonly Color PinkLip = Hex("#C93A86");
        private static readonly Color PinkText = Hex("#B0236A");
        private static readonly Color Gray = Hex("#8E8E8E");

        private static TMP_FontAsset _font;
        private static Material _plainMaterial;
        private static Material _shadowMaterial;
        private static readonly List<Button> ComingSoon = new();

        [MenuItem("Tools/NewBattle/Menü/Kur (eksik parçaları ekler)", false, 1)]
        public static void Install() => Run(rebuild: false);

        [MenuItem("Tools/NewBattle/Menü/Baştan Kur (elle yapılan değişiklikler gider)", false, 2)]
        public static void Rebuild() => Run(rebuild: true);

        /// <summary>
        /// Menunun eksik parcalarini kurar. Var olan parcalara dokunmaz; yeni
        /// bir asama geldiginde sadece o asamanin parcasi eklenir ve senin
        /// Inspector'dan yaptigin ayarlar korunur.
        /// </summary>
        private static void Run(bool rebuild)
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Menü", "Önce Play modunu kapat.", "Tamam");
                return;
            }

            AssetDatabase.ImportAsset(ArtFolder, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);

            if (!PrepareFont())
                return;

            PrepareMaterials();

            Canvas canvas = FindGameCanvas();
            if (canvas == null)
            {
                Debug.LogError("Menü: Game_UI canvas'i bulunamadi.");
                return;
            }

            Transform root = FindMenuRoot(canvas, out int removedCopies);
            GameObject existingStage = GameObject.Find(StageName);

            if (rebuild && (root != null || existingStage != null))
            {
                bool confirmed = EditorUtility.DisplayDialog(
                    "Menü",
                    "Menü silinip baştan kurulsun mu?\n\nInspector'dan yaptığın değişiklikler gider.",
                    "Baştan Kur", "Vazgeç");

                if (!confirmed)
                    return;
            }

            Undo.SetCurrentGroupName("Menüyü Kur");
            int undoGroup = Undo.GetCurrentGroup();
            var added = new List<string>();
            if (removedCopies > 0)
                added.Add($"{removedCopies} fazla menü kopyası silindi");

            if (rebuild && root != null)
            {
                Undo.DestroyObjectImmediate(root.gameObject);
                root = null;
            }

            if (root == null)
            {
                if (existingStage != null)
                    Undo.DestroyObjectImmediate(existingStage);

                DisableLegacyMenu(canvas);
                (Camera stageCamera, Transform turntable) = BuildCharacterStage();
                root = BuildMenu(canvas.transform, stageCamera, turntable).transform;
                added.Add("Ana menü");
            }

            MainMenuScreen screen = root.GetComponent<MainMenuScreen>();
            MenuCharacterStage stage = root.GetComponentInChildren<MenuCharacterStage>(true);

            if (stage != null && stage.LobbyAnimator == null)
            {
                LobbyAnimationSetup.Attach(stage, LobbyAnimationSetup.EnsureLibrary());
                added.Add("Lobi animasyonları");
            }

            if (screen != null && MigrateComingSoonButtons(screen))
                added.Add("Buton eylemleri");

            if (screen != null && root.Find(ModePanelName) == null)
            {
                ComingSoon.Clear();
                ModeSelectPanel panel = BuildModePanel((RectTransform)root);
                var serialized = new SerializedObject(screen);
                serialized.FindProperty("modePanel").objectReferenceValue = panel;
                serialized.ApplyModifiedProperties();
                AttachActions(ComingSoon);
                added.Add("Oyun modu ekranı");
            }

            if (screen != null && EnsureLoadingScreen(screen, rebuild))
                added.Add("Yükleme ekranı");

            if (EnsureDropScreen(canvas, rebuild))
                added.Add("Harita seçme ekranı");

            if (RemoveDropBackButton(canvas))
                added.Add("Geri butonu kaldırıldı");

            if (EnsureWeaponHandIK())
                added.Add("Eller tüfeğe oturuyor (SagElTutamak / SolElTutamak)");

            int lootChanged = EnsureLootLook();
            if (lootChanged > 0)
                added.Add($"{lootChanged} eşya canlı renk + %35 büyük");

            if (EnsureMatchFeel(canvas, out string feel))
                added.Add("Maç içi his: " + feel);

            if (screen != null && EnsureMenuGround(screen))
                added.Add("Ana menü: karakterin altına zemin (Hierarchy > MenuKarakterSahnesi > KarakterZemini)");

            if (EnsureWeaponSettings())
                added.Add("Silah Ayarları (Resources/SilahAyarlari: nadirlik + olasılıklar)");

            if (EnsureWeaponCatalog())
                added.Add("Yeni silahlar: Tabanca, Hafif Makineli, Pompalı, Keskin Nişancı");

            if (EnsureWeaponDisplay(canvas))
                added.Add("Silah göstergesi (minimap yanında: resim + mermi)");

            if (EnsureHeightMatchedScalers())
                added.Add("Ekran ölçeği telefona uyumlu (yüksekliğe eşit)");

            if (EnsureMobileJoysticks())
                added.Add("Joystickler büyük, yuvarlak, saydam ve içeride");

            if (EnsureFloatingJoysticks())
                added.Add("Yüzen joystick: sol alt yarı hareket, sağ alt yarı saldırı");

            if (EnsureCloserCamera())
                added.Add("Kamera yaklaştırıldı (8.5 → 6)");

            if (EnsureVisualSettings())
                added.Add("Görsel Ayarlar (Hierarchy > GorselAyarlar: kamera ve renkler)");

            if (EnsureDescentCamera())
                added.Add("İniş kamerası");

            if (EnsureDescentOverlay(canvas, rebuild))
                added.Add("İniş efektleri");

            if (UpdateDescentTimings())
                added.Add("İniş süreleri (2.4 sn düşüş + 3.5 sn paraşüt)");

            if (UpdateDescentCameraPreset())
                added.Add("İniş kamerası ayarları");

            if (EnsureMatchEndScreen(canvas, rebuild))
                added.Add("Maç sonu ekranı (kuruldu / yerleşim düzeltildi)");

            if (EnsureScreenToasts(canvas))
                added.Add("YAKINDA bildirimleri");

            if (screen != null && EnsureCharacterScreen(screen))
                added.Add("Karakter ekranı (kuruldu / güncellendi)");

            if (screen != null && EnsureShopScreen(screen))
                added.Add("Mağaza (kuruldu / karakter önizlemesi eklendi)");

            if (screen != null && EnsureMenuScreens(screen, out string menuScreens))
                added.Add(menuScreens);

            string characters = EnsureCharacterLibrary(out _);
            if (characters.Length > 0)
                added.Add("Karakterler: " + characters);

            if (screen != null && EnsureCharacterTabs(screen))
                added.Add("Karakter ekranı sekmeleri");

            if (screen != null && EnsureGiftCodeUi(screen))
                added.Add("Hediye kodu (Ayarlar)");

            if (screen != null && EnsureFriendRoom(screen))
                added.Add("Arkadaş İkilisi (oda kur / kodla katıl)");

            if (EnsureGiftCodeCatalog(out _))
                added.Add("Hediye kodları (Assets/Resources/HediyeKodlari)");

            int sounds = EnsureSoundLibrary(out _);
            if (sounds > 0)
                added.Add($"{sounds} ses (Assets/Resources/SesKutuphanesi)");

            if (EnsureProgressionSettings(out _))
                added.Add("İlerleme ayarları (Assets/Resources/IlerlemeAyarlari)");

            if (screen != null && EnsureRewardPopup(screen))
                added.Add("Ödül penceresi");

            if (screen != null && EnsureDoubleXpRefs(screen))
                added.Add("İki Kat XP göstergeleri");

            if (EnsureDropDoubleXpRefs(canvas))
                added.Add("Harita ekranı çift XP durumu");

            if (EnsureMatchEndDoubleXpBadge(canvas))
                added.Add("Maç sonu 2x rozeti");

            int pressables = EnsurePressables(canvas);
            if (pressables > 0)
                added.Add($"{pressables} kart basılabilir yapıldı");

            if (SetFinalCountdownSeconds(5f))
                added.Add("Geri sayım 5 saniye");

            Undo.CollapseUndoOperations(undoGroup);

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            EditorSceneManager.SaveScene(canvas.gameObject.scene);
            Selection.activeGameObject = root.gameObject;

            string message = added.Count > 0
                ? "Eklendi: " + string.Join(", ", added) + ".\nSahne kaydedildi; Play'e basıp deneyebilirsin."
                : "Her şey zaten kurulu, eklenecek bir şey yok.";
            EditorUtility.DisplayDialog("Menü", message, "Tamam");
        }

        /// <summary>
        /// Eski kurulumdaki "YAKINDA" listesini MenuButton'lara cevirir: her
        /// buton kendi eylem kimligini alir, sonradan kodla baglanabilir.
        /// </summary>
        /// <summary>
        /// Menuyu adiyla degil bilesiniyle bulur. Eski bir hata yuzunden menu
        /// "Arkaplan" adini alip ikinci kez kurulmus olabilir: mod penceresi bagli
        /// (yoksa en son kurulan) kopya tutulur, digerleri silinir.
        /// </summary>
        private static Transform FindMenuRoot(Canvas canvas, out int removedCopies)
        {
            removedCopies = 0;
            MainMenuScreen[] screens = canvas.GetComponentsInChildren<MainMenuScreen>(true);
            if (screens.Length == 0)
                return null;

            MainMenuScreen keep = screens[screens.Length - 1];
            foreach (MainMenuScreen screen in screens)
            {
                if (new SerializedObject(screen).FindProperty("modePanel").objectReferenceValue != null)
                    keep = screen;
            }

            foreach (MainMenuScreen screen in screens)
            {
                if (screen == keep)
                    continue;

                Undo.DestroyObjectImmediate(screen.gameObject);
                removedCopies++;
            }

            if (keep.gameObject.name != RootName)
            {
                Undo.RecordObject(keep.gameObject, "Menu adini duzelt");
                keep.gameObject.name = RootName;
            }

            return keep.transform;
        }

        private static bool MigrateComingSoonButtons(MainMenuScreen screen)
        {
            var serialized = new SerializedObject(screen);
            SerializedProperty list = serialized.FindProperty("comingSoonButtons");
            if (list.arraySize == 0)
                return false;

            var buttons = new List<Button>();
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue is Button button)
                    buttons.Add(button);
            }

            AttachActions(buttons);
            list.arraySize = 0;
            serialized.ApplyModifiedProperties();
            return true;
        }

        private static void AttachActions(IEnumerable<Button> buttons)
        {
            foreach (Button button in buttons)
            {
                if (button == null)
                    continue;

                MenuButton action = button.GetComponent<MenuButton>();
                if (action == null)
                    action = Undo.AddComponent<MenuButton>(button.gameObject);

                var serialized = new SerializedObject(action);
                serialized.FindProperty("actionId").stringValue = ActionIdFor(button.gameObject.name);
                serialized.ApplyModifiedProperties();
            }
        }

        /// <summary>Buton nesnesinin adindan eylem kimligi.</summary>
        private static string ActionIdFor(string objectName)
        {
            return objectName switch
            {
                "AyarlarButonu" => "ayarlar",
                "ParaEkleButonu" => "para_ekle",
                "BattlePassButonu" => "battle_pass",
                "BattlePassKarti" => "battle_pass_kart",
                "MagazaButonu" => "magaza",
                "LiderlikButonu" => "liderlik",
                "KarakterButonu" => "karakter",
                "ArkadaslarButonu" => "arkadaslar",
                "EtkinliklerButonu" => "savas_etkinlikleri",
                "EtkinlikKarti" => "savas_etkinlikleri_kart",
                "KostumKarti" => "kostum",
                "EtkinlestirButonu" => "cift_xp",
                "IsimDuzenleButonu" => "isim_duzenle",
                _ => objectName.ToLowerInvariant()
            };
        }

        // ------------------------------------------------------------------
        // Hazirlik
        // ------------------------------------------------------------------

        private static bool PrepareFont()
        {
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (_font == null)
            {
                Debug.LogError($"Ana Menü: {FontAssetPath} bulunamadi.");
                return false;
            }

            AssetDatabase.ImportAsset(PatchedFontPath);
            Font patched = AssetDatabase.LoadAssetAtPath<Font>(PatchedFontPath);
            if (patched == null)
            {
                Debug.LogWarning($"Ana Menü: {PatchedFontPath} yok; 'İ' harfi kutucuk gorunebilir.");
                return true;
            }

            if (_font.sourceFontFile == patched)
                return true;

            var serialized = new SerializedObject(_font);
            serialized.FindProperty("m_SourceFontFile").objectReferenceValue = patched;
            serialized.FindProperty("m_SourceFontFileGUID").stringValue = AssetDatabase.AssetPathToGUID(PatchedFontPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // Dinamik atlasi bosalt: harfler yeni kaynaktan yeniden uretilsin.
            _font.ClearFontAssetData();
            EditorUtility.SetDirty(_font);
            AssetDatabase.SaveAssetIfDirty(_font);

            Debug.Log("Ana Menü: TitanOne SDF artik 'İ' harfi olan NBDisplay-Regular.ttf'i kullaniyor.");
            return true;
        }

        private static void PrepareMaterials()
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder(ArtFolder, "Materials");

            // Isimler fontla basliyor ki TMP'nin "Material Preset" listesinde cikssin.
            _plainMaterial = MakePreset("TitanOne SDF - Menu Duz", material =>
            {
                material.DisableKeyword("OUTLINE_ON");
                material.DisableKeyword("UNDERLAY_ON");
                material.SetFloat("_OutlineWidth", 0f);
            });

            _shadowMaterial = MakePreset("TitanOne SDF - Menu Golge", material =>
            {
                material.EnableKeyword("OUTLINE_ON");
                material.SetFloat("_OutlineWidth", 0.14f);
                material.SetColor("_OutlineColor", new Color(0.16f, 0.08f, 0.02f, 0.55f));
                material.EnableKeyword("UNDERLAY_ON");
                material.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.35f));
                material.SetFloat("_UnderlayOffsetX", 0f);
                material.SetFloat("_UnderlayOffsetY", -0.9f);
                material.SetFloat("_UnderlayDilate", 0.15f);
                material.SetFloat("_UnderlaySoftness", 0.15f);
            });
        }

        private static Material MakePreset(string name, System.Action<Material> setup)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(_font.material) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = _font.material.shader;
                material.CopyPropertiesFromMaterial(_font.material);
            }

            material.SetTexture(ShaderUtilities.ID_MainTex, _font.atlasTexture);
            setup(material);
            ShaderUtilities.UpdateShaderRatios(material);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        private static Canvas FindGameCanvas()
        {
            Component anchor = Object.FindFirstObjectByType<MainMenuFlow>(FindObjectsInactive.Include);
            anchor ??= Object.FindFirstObjectByType<NetworkMenuUI>(FindObjectsInactive.Include);
            anchor ??= Object.FindFirstObjectByType<MainMenuScreen>(FindObjectsInactive.Include);

            if (anchor != null)
            {
                Canvas canvas = anchor.GetComponentInParent<Canvas>(true);
                return canvas != null ? canvas.rootCanvas : null;
            }

            GameObject named = GameObject.Find("Game_UI");
            return named != null ? named.GetComponent<Canvas>() : null;
        }

        private static void DisableLegacyMenu(Canvas canvas)
        {
            foreach (NetworkMenuUI legacy in canvas.GetComponentsInChildren<NetworkMenuUI>(true))
            {
                Undo.RecordObject(legacy, "Eski menuyu kapat");
                legacy.enabled = false;
            }

            foreach (MainMenuFlow legacy in canvas.GetComponentsInChildren<MainMenuFlow>(true))
            {
                Undo.RecordObject(legacy, "Eski menuyu kapat");
                legacy.enabled = false;
            }

            foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true))
            {
                if (child.name != "MainMenu")
                    continue;

                Undo.RecordObject(child.gameObject, "Eski menuyu kapat");
                child.gameObject.name = "MainMenu_Eski";
                child.gameObject.SetActive(false);
            }
        }

        // ------------------------------------------------------------------
        // Karakter sahnesi (dunyada, gorunmeyen uzak bir noktada)
        // ------------------------------------------------------------------

        private static (Camera, Transform) BuildCharacterStage()
        {
            var stage = new GameObject(StageName);
            Undo.RegisterCreatedObjectUndo(stage, "Menu karakter sahnesi");
            stage.transform.position = StagePosition;

            Transform turntable = new GameObject("Doner").transform;
            turntable.SetParent(stage.transform, false);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
            if (prefab != null)
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, turntable);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
            }
            else
            {
                Debug.LogWarning($"Ana Menü: {CharacterPrefabPath} bulunamadi. 'Doner' altina kendi modelini koy.");
            }

            var cameraObject = new GameObject("MenuKamerasi", typeof(Camera));
            cameraObject.transform.SetParent(stage.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1f, 6f);
            cameraObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            Camera camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false; // MenuCharacterStage acar; aksi halde ekrana cizerdi
            camera.fieldOfView = 28f;
            camera.depth = -50f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.allowHDR = false;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 30f;

            var lightObject = new GameObject("MenuIsigi", typeof(Light));
            lightObject.transform.SetParent(stage.transform, false);
            lightObject.transform.localPosition = new Vector3(1.6f, 2.6f, 3.2f);
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Point;
            light.range = 15f;
            light.intensity = 3f;
            light.color = Hex("#FFF4E0");
            light.shadows = LightShadows.None;

            return (camera, turntable);
        }

        // ------------------------------------------------------------------
        // Menu arayuzu
        // ------------------------------------------------------------------

        private static GameObject BuildMenu(Transform canvas, Camera stageCamera, Transform turntable)
        {
            ComingSoon.Clear();

            RectTransform root = NewRect(RootName, canvas);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Ana menu");
            Stretch(root, 0f, 0f, 0f, 0f);
            root.SetAsLastSibling();

            Image background = AddImage(root, Sprite("bg_gold"), Color.white, raycast: true);
            background.type = Image.Type.Simple;

            var refs = new MenuRefs();

            BuildMiddle(root, refs, stageCamera, turntable);
            BuildTopBar(root, refs);
            BuildBottomBar(root, refs);

            refs.Status = AddText(root, "Durum", string.Empty, 30f, BrownText, TextAlignmentOptions.Right);
            Place(refs.Status.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-44f, BottomBarHeight + 14f), new Vector2(800f, 44f));

            BuildToast(root, refs);

            MainMenuScreen screen = root.gameObject.AddComponent<MainMenuScreen>();
            Bind(screen, refs);
            return root.gameObject;
        }

        private sealed class MenuRefs
        {
            public Button Play;
            public TextMeshProUGUI Status, PlayerName, Coins, Season, Level, Rank, Xp, NextLevel, Online, ModeChip, Toast;
            public RectTransform XpFill;
            public CanvasGroup ToastGroup;
        }

        private static void BuildTopBar(RectTransform root, MenuRefs refs)
        {
            RectTransform bar = NewRect("UstBar", root);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.offsetMin = new Vector2(0f, -TopBarHeight);
            bar.offsetMax = Vector2.zero;
            AddImage(bar, null, Color.white, raycast: true);

            RectTransform shade = NewRect("Golge", bar);
            shade.anchorMin = Vector2.zero;
            shade.anchorMax = new Vector2(1f, 0f);
            shade.pivot = new Vector2(0.5f, 1f);
            shade.sizeDelta = new Vector2(0f, 12f);
            AddImage(shade, null, new Color(0f, 0f, 0f, 0.10f));

            TextMeshProUGUI logo = AddText(bar, "Logo", "NEW BATTLE", 80f, Color.white, TextAlignmentOptions.Left, shadow: true);
            Place(logo.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 4f), new Vector2(470f, 110f));
            logo.enableVertexGradient = true;
            logo.colorGradient = new VertexGradient(Hex("#F7BA2E"), Hex("#F7BA2E"), Hex("#9A5310"), Hex("#9A5310"));

            AddIcon(bar, "OnlineIkon", "icon_people", Gray, new Vector2(0f, 0.5f), new Vector2(536f, 0f), 46f);
            refs.Online = AddText(bar, "OnlineSayac", "1 Online Savaşçılar", 30f, Gray, TextAlignmentOptions.Left);
            Place(refs.Online.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(572f, 0f), new Vector2(420f, 50f));

            Button settings = AddButton(bar, "AyarlarButonu", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-36f, 6f), new Vector2(104f, 104f), Orange, OrangeLip);
            AddIcon(Face(settings), "Ikon", "icon_gear", Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, 64f);
            ComingSoon.Add(settings);

            RectTransform wallet = NewRect("ParaPaneli", bar);
            Place(wallet, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-176f, 6f), new Vector2(330f, 80f));
            AddImage(wallet, Sprite("ui_round"), Navy).pixelsPerUnitMultiplier = 1.4f;

            Button addCoins = AddButton(wallet, "ParaEkleButonu", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(6f, 3f), new Vector2(70f, 66f), Green, GreenLip, lipHeight: 6f);
            AddIcon(Face(addCoins), "Ikon", "icon_plus", Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, 40f);
            ComingSoon.Add(addCoins);

            refs.Coins = AddText(wallet, "ParaMiktari", "100", 46f, Color.white, TextAlignmentOptions.Right, shadow: true);
            Stretch(refs.Coins.rectTransform, 90f, 76f, 0f, 0f);
            AddIcon(wallet, "Coin", "coin", Color.white, new Vector2(1f, 0.5f), new Vector2(-18f, 0f), 96f);
        }

        private static void BuildBottomBar(RectTransform root, MenuRefs refs)
        {
            RectTransform bar = NewRect("AltBar", root);
            bar.anchorMin = Vector2.zero;
            bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.offsetMin = Vector2.zero;
            bar.offsetMax = new Vector2(0f, BottomBarHeight);
            AddImage(bar, null, Color.white, raycast: true);

            RectTransform shade = NewRect("Golge", bar);
            shade.anchorMin = new Vector2(0f, 1f);
            shade.anchorMax = Vector2.one;
            shade.pivot = new Vector2(0.5f, 0f);
            shade.sizeDelta = new Vector2(0f, 12f);
            AddImage(shade, null, new Color(0f, 0f, 0f, 0.08f));

            SquareButton(bar, "BattlePassButonu", "icon_crown", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 6f), Orange, OrangeLip);
            SquareButton(bar, "MagazaButonu", "icon_cart", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(190f, 6f), Blue, BlueLip);
            SquareButton(bar, "LiderlikButonu", "icon_trophy", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(340f, 6f), Blue, BlueLip);

            SquareButton(bar, "KarakterButonu", "icon_head", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-80f, 6f), Green, GreenLip);
            Button friends = SquareButton(bar, "ArkadaslarButonu", "icon_friends", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(80f, 6f), Green, GreenLip);

            RectTransform badge = NewRect("Rozet", friends.transform);
            Place(badge, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(-8f, -8f), new Vector2(46f, 46f));
            AddImage(badge, Sprite("ui_circle"), Orange);
            TextMeshProUGUI badgeText = AddText(badge, "Sayi", "0", 28f, Color.white, TextAlignmentOptions.Center, shadow: true);
            Stretch(badgeText.rectTransform, 0f, 0f, 0f, 2f);

            // OYNA
            refs.Play = AddButton(bar, "OynaButonu", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-40f, 6f), new Vector2(380f, 132f), Yellow, YellowLip);
            RectTransform playFace = Face(refs.Play);
            AddIcon(playFace, "Ikon", "icon_play", Color.white, new Vector2(0f, 0.5f), new Vector2(66f, 0f), 64f);
            TextMeshProUGUI playLabel = AddText(playFace, "Yazi", "OYNA", 76f, Color.white, TextAlignmentOptions.Center, shadow: true);
            Stretch(playLabel.rectTransform, 96f, 16f, 0f, 4f);

            RectTransform chip = NewRect("ModEtiketi", refs.Play.transform);
            Place(chip, new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(18f, -14f), new Vector2(160f, 46f));
            AddImage(chip, Sprite("ui_round"), Orange).pixelsPerUnitMultiplier = 2.2f;
            AddIcon(chip, "Ikon", "icon_head", Color.white, new Vector2(0f, 0.5f), new Vector2(26f, 0f), 30f);
            refs.ModeChip = AddText(chip, "Yazi", "SOLO", 28f, Color.white, TextAlignmentOptions.Center, shadow: true);
            Stretch(refs.ModeChip.rectTransform, 44f, 8f, 0f, 2f);

            Button events = AddButton(bar, "EtkinliklerButonu", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-440f, 6f), new Vector2(330f, 132f), Orange, OrangeLip);
            RectTransform eventsFace = Face(events);
            AddIcon(eventsFace, "Ikon", "icon_gift", Color.white, new Vector2(0f, 0.5f), new Vector2(60f, 0f), 66f);
            TextMeshProUGUI eventsLabel = AddText(eventsFace, "Yazi", "SAVAŞ\nETKİNLİKLERİ", 38f, Color.white, TextAlignmentOptions.Center, shadow: true);
            eventsLabel.lineSpacing = -22f;
            Stretch(eventsLabel.rectTransform, 100f, 12f, 0f, 4f);
            ComingSoon.Add(events);
        }

        private static void BuildMiddle(RectTransform root, MenuRefs refs, Camera stageCamera, Transform turntable)
        {
            RectTransform middle = NewRect("OrtaAlan", root);
            Stretch(middle, 0f, 0f, TopBarHeight, BottomBarHeight);

            // --- Karakter (kartlarin arkasinda kalsin diye once) ---
            RectTransform shadow = NewRect("KarakterGolgesi", middle);
            Place(shadow, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 116f), new Vector2(340f, 90f));
            AddImage(shadow, Sprite("shadow_blob"), Color.white).type = Image.Type.Simple;

            RectTransform characterArea = NewRect("KarakterAlani", middle);
            characterArea.anchorMin = new Vector2(0.5f, 0f);
            characterArea.anchorMax = new Vector2(0.5f, 1f);
            characterArea.pivot = new Vector2(0.5f, 0.5f);
            characterArea.offsetMin = new Vector2(-310f, 80f);
            characterArea.offsetMax = new Vector2(310f, -10f);
            RawImage characterImage = characterArea.gameObject.AddComponent<RawImage>();
            characterImage.color = new Color(1f, 1f, 1f, 0f); // RenderTexture gelene kadar gorunmesin
            MenuCharacterStage stage = characterArea.gameObject.AddComponent<MenuCharacterStage>();
            var stageSerialized = new SerializedObject(stage);
            stageSerialized.FindProperty("stageCamera").objectReferenceValue = stageCamera;
            stageSerialized.FindProperty("turntable").objectReferenceValue = turntable;
            stageSerialized.ApplyModifiedPropertiesWithoutUndo();

            // --- Isim etiketi ---
            RectTransform nameTag = NewRect("IsimEtiketi", middle);
            Place(nameTag, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(600f, 64f));
            HorizontalLayoutGroup row = nameTag.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 14f;
            row.childAlignment = TextAnchor.MiddleCenter;
            // Genislikler cocuklarin "preferred" degerinden gelir: isim uzayip
            // kisaldikca ikon ve kalem butonu yanina kayar.
            row.childControlWidth = true;
            row.childControlHeight = false;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;

            Image rankIcon = AddIcon(nameTag, "RutbeIkon", "rank_bronze", Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, 56f);
            rankIcon.gameObject.AddComponent<LayoutElement>().preferredWidth = 56f;
            refs.PlayerName = AddText(nameTag, "Isim", "Savaşçı#0000", 40f, BrownText, TextAlignmentOptions.Center);
            refs.PlayerName.enableAutoSizing = false;
            refs.PlayerName.rectTransform.sizeDelta = new Vector2(300f, 60f);
            Button editName = AddButton(nameTag, "IsimDuzenleButonu", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(58f, 58f), Blue, BlueLip, lipHeight: 5f);
            editName.gameObject.AddComponent<LayoutElement>().preferredWidth = 58f;
            AddIcon(Face(editName), "Ikon", "icon_pencil", Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, 36f);
            ComingSoon.Add(editName);

            BuildSeasonCard(middle, refs);
            BuildChallengesCard(middle);
            BuildPromoCards(middle);
            BuildEventCard(middle);
            BuildDoubleXpCard(middle);
        }

        private static void BuildSeasonCard(RectTransform middle, MenuRefs refs)
        {
            RectTransform card = Card(middle, "SezonKarti", new Vector2(40f, -40f), new Vector2(470f, 330f));
            refs.Season = CardHeader(card, "SEZON 1", BrownDark, Gold, 54f, shadow: false);

            AddIcon(card, "RutbeIkon", "rank_bronze", Color.white, new Vector2(0f, 1f), new Vector2(105f, -198f), 150f);

            refs.Level = AddText(card, "Seviye", "SEVİYE 1", 52f, BrownText, TextAlignmentOptions.Left);
            Place(refs.Level.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(198f, -136f), new Vector2(255f, 60f));

            refs.Rank = AddText(card, "Rutbe", "Er I", 34f, BrownText, TextAlignmentOptions.Left);
            Place(refs.Rank.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(200f, -184f), new Vector2(255f, 44f));

            RectTransform barBack = NewRect("XpBari", card);
            Place(barBack, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(200f, -236f), new Vector2(196f, 32f));
            AddImage(barBack, Sprite("ui_round"), BrownDark).pixelsPerUnitMultiplier = 2.4f;

            refs.XpFill = NewRect("Dolum", barBack);
            refs.XpFill.anchorMin = Vector2.zero;
            refs.XpFill.anchorMax = new Vector2(0.3f, 1f);
            refs.XpFill.offsetMin = new Vector2(4f, 4f);
            refs.XpFill.offsetMax = new Vector2(0f, -4f);
            AddImage(refs.XpFill, Sprite("ui_round"), Hex("#FFE9A8")).pixelsPerUnitMultiplier = 3f;

            AddIcon(card, "Ok", "icon_play", BrownText, new Vector2(0f, 1f), new Vector2(414f, -236f), 26f);
            refs.NextLevel = AddText(card, "SonrakiSeviye", "2", 50f, BrownText, TextAlignmentOptions.Left);
            Place(refs.NextLevel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(430f, -232f), new Vector2(40f, 56f));

            refs.Xp = AddText(card, "XpYazisi", "0 / 50", 30f, BrownText, TextAlignmentOptions.Left);
            Place(refs.Xp.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(200f, -282f), new Vector2(220f, 40f));
        }

        private static void BuildChallengesCard(RectTransform middle)
        {
            RectTransform card = Card(middle, "MucadelelerKarti", new Vector2(40f, -390f), new Vector2(470f, 250f));
            CardHeader(card, "MÜCADELELER", BrownDark, Gold, 46f, shadow: false);

            AddIcon(card, "Kitap", "challenge_book", Color.white, new Vector2(0f, 1f), new Vector2(95f, -168f), 124f);

            TextMeshProUGUI daily = AddText(card, "Gunluk", "Günlük", 36f, BrownText, TextAlignmentOptions.Left);
            Place(daily.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(178f, -128f), new Vector2(180f, 46f));

            RectTransform box = NewRect("GunlukKutu", card);
            Place(box, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(412f, -128f), new Vector2(46f, 46f));
            AddImage(box, Sprite("ui_round"), new Color(BrownDark.r, BrownDark.g, BrownDark.b, 0.35f)).pixelsPerUnitMultiplier = 3f;

            TextMeshProUGUI weekly = AddText(card, "Haftalik", "Haftalık", 36f, BrownText, TextAlignmentOptions.Left);
            Place(weekly.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(178f, -192f), new Vector2(180f, 46f));

            RectTransform pill = NewRect("Yakinda", card);
            Place(pill, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(386f, -192f), new Vector2(130f, 40f));
            AddImage(pill, Sprite("ui_round"), BrownDark).pixelsPerUnitMultiplier = 2.6f;
            TextMeshProUGUI pillText = AddText(pill, "Yazi", "YAKINDA", 22f, Gold, TextAlignmentOptions.Center);
            Stretch(pillText.rectTransform, 6f, 6f, 0f, 2f);
        }

        private static void BuildPromoCards(RectTransform middle)
        {
            Button costume = AddButton(middle, "KostumKarti", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(530f, -30f), new Vector2(176f, 210f), Pink, PinkLip);
            AddIcon(Face(costume), "Ikon", "icon_gift", Color.white, new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), 112f);
            TextMeshProUGUI costumeLabel = AddText(costume.transform, "Etiket", "YENİ KOSTÜM", 30f, PinkText, TextAlignmentOptions.Center);
            Place(costumeLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(220f, 40f));
            ComingSoon.Add(costume);

            RectTransform pass = NewRect("BattlePassKarti", middle);
            Place(pass, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(538f, -300f), new Vector2(160f, 160f));
            Image passImage = AddImage(pass, Sprite("battlepass_badge"), Color.white, raycast: true);
            passImage.type = Image.Type.Simple;
            passImage.preserveAspect = true;
            Button passButton = pass.gameObject.AddComponent<Button>();
            passButton.targetGraphic = passImage;
            pass.gameObject.AddComponent<UIPressScale>();
            ComingSoon.Add(passButton);

            RectTransform pill = NewRect("Oduller", middle);
            Place(pill, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(530f, -468f), new Vector2(176f, 50f));
            AddImage(pill, Sprite("ui_round"), Navy).pixelsPerUnitMultiplier = 2.2f;
            AddIcon(pill, "Ikon", "icon_clock", Color.white, new Vector2(0f, 0.5f), new Vector2(28f, 0f), 30f);
            TextMeshProUGUI pillText = AddText(pill, "Yazi", "ÖDÜLLER", 26f, Color.white, TextAlignmentOptions.Center);
            Stretch(pillText.rectTransform, 46f, 10f, 0f, 2f);
        }

        private static void BuildEventCard(RectTransform middle)
        {
            RectTransform card = NewRect("EtkinlikKarti", middle);
            Place(card, Vector2.one, Vector2.one, new Vector2(-40f, -30f), new Vector2(470f, 330f));
            Image edge = AddImage(card, Sprite("ui_round"), PurpleEdge, raycast: true);
            Button button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = edge;
            card.gameObject.AddComponent<UIPressScale>();
            ComingSoon.Add(button);

            RectTransform inner = NewRect("Ic", card);
            Stretch(inner, 6f, 6f, 6f, 6f);
            AddImage(inner, Sprite("ui_round"), Purple);

            CardHeader(card, "SAVAŞ ETKİNLİKLERİ", PurpleHeader, Color.white, 44f, shadow: true, inset: 6f, height: 86f);

            AddIcon(card, "Kupa", "icon_trophy", Gold, Vector2.zero, new Vector2(118f, 122f), 172f);
            TextMeshProUGUI body = AddText(card, "Yazi", "Şimdi Savaş Etkinliklerini oyna!", 36f, Color.white, TextAlignmentOptions.Center, shadow: true);
            body.textWrappingMode = TextWrappingModes.Normal;
            Place(body.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-18f, 24f), new Vector2(250f, 200f));
        }

        private static void BuildDoubleXpCard(RectTransform middle)
        {
            RectTransform card = NewRect("CiftXpKarti", middle);
            Place(card, Vector2.one, Vector2.one, new Vector2(-40f, -380f), new Vector2(470f, 270f));
            AddImage(card, Sprite("ui_round"), Teal, raycast: true);
            CardHeader(card, "İKİ KAT XP", TealHeader, Color.white, 54f, shadow: true, inset: 0f, height: 84f);

            AddIcon(card, "Kafatasi", "icon_skull", new Color(1f, 1f, 1f, 0.30f), Vector2.zero, new Vector2(96f, 92f), 140f);

            TextMeshProUGUI body = AddText(card, "Yazi", "Bir reklam izle, çift XP al!", 30f, Color.white, TextAlignmentOptions.Center, shadow: true);
            body.textWrappingMode = TextWrappingModes.Normal;
            Place(body.rectTransform, Vector2.one, Vector2.one, new Vector2(-18f, -92f), new Vector2(290f, 80f));

            Button activate = AddButton(card, "EtkinlestirButonu", new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-30f, 28f), new Vector2(270f, 72f), Green, GreenLip, lipHeight: 7f);
            TextMeshProUGUI label = AddText(Face(activate), "Yazi", "Etkinleştir", 36f, Color.white, TextAlignmentOptions.Center, shadow: true);
            Stretch(label.rectTransform, 8f, 8f, 0f, 3f);
            ComingSoon.Add(activate);
        }

        private static void BuildToast(RectTransform root, MenuRefs refs)
        {
            RectTransform toast = NewRect("Bildirim", root);
            Place(toast, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(520f, 90f));
            AddImage(toast, Sprite("ui_round"), new Color(0f, 0f, 0f, 0.75f));
            refs.ToastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            refs.ToastGroup.alpha = 0f;
            refs.ToastGroup.blocksRaycasts = false;
            refs.ToastGroup.interactable = false;
            refs.Toast = AddText(toast, "Yazi", "YAKINDA!", 44f, Color.white, TextAlignmentOptions.Center);
            Stretch(refs.Toast.rectTransform, 16f, 16f, 0f, 4f);
        }

        private static void Bind(MainMenuScreen screen, MenuRefs refs)
        {
            var serialized = new SerializedObject(screen);
            serialized.FindProperty("playButton").objectReferenceValue = refs.Play;
            serialized.FindProperty("statusText").objectReferenceValue = refs.Status;
            serialized.FindProperty("playerNameText").objectReferenceValue = refs.PlayerName;
            serialized.FindProperty("coinText").objectReferenceValue = refs.Coins;
            serialized.FindProperty("seasonText").objectReferenceValue = refs.Season;
            serialized.FindProperty("levelText").objectReferenceValue = refs.Level;
            serialized.FindProperty("rankText").objectReferenceValue = refs.Rank;
            serialized.FindProperty("xpText").objectReferenceValue = refs.Xp;
            serialized.FindProperty("nextLevelText").objectReferenceValue = refs.NextLevel;
            serialized.FindProperty("onlineText").objectReferenceValue = refs.Online;
            serialized.FindProperty("modeChipText").objectReferenceValue = refs.ModeChip;
            serialized.FindProperty("xpFill").objectReferenceValue = refs.XpFill;
            serialized.FindProperty("toastGroup").objectReferenceValue = refs.ToastGroup;
            serialized.FindProperty("toastText").objectReferenceValue = refs.Toast;

            serialized.FindProperty("comingSoonButtons").arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AttachActions(ComingSoon);
        }

        // ------------------------------------------------------------------
        // ASAMA 2: Oyun modu penceresi
        // ------------------------------------------------------------------

        // Battlelands'teki gibi: ekrani enine kaplayan uc sutun. Orta sutun
        // yan sutunlardan biraz daha uzun; CEVRIMICI OYNA alt kenarina biner.
        private const float PanelBottom = 0.18f;
        private const float SidePanelTop = 0.853f;
        private const float CenterPanelTop = 0.91f;

        private static ModeSelectPanel BuildModePanel(RectTransform root)
        {
            RectTransform panelRoot = NewRect(ModePanelName, root);
            Undo.RegisterCreatedObjectUndo(panelRoot.gameObject, "Oyun modu ekrani");
            Stretch(panelRoot, 0f, 0f, 0f, 0f);
            CanvasGroup group = panelRoot.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            RectTransform backdrop = NewRect("Karartma", panelRoot);
            Stretch(backdrop, 0f, 0f, 0f, 0f);
            Image backdropImage = AddImage(backdrop, null, new Color(0f, 0f, 0f, 0.6f), raycast: true);
            Button backdropButton = backdrop.gameObject.AddComponent<Button>();
            backdropButton.targetGraphic = backdropImage;
            backdropButton.transition = Selectable.Transition.None;

            RectTransform window = NewRect("Pencere", panelRoot);
            Stretch(window, 0f, 0f, 0f, 0f);

            BuildDoubleXpColumn(window);
            List<ModeRow> rows = BuildModeColumn(window, out Button playButton);
            Button closeButton = BuildTipsColumn(window, out TextMeshProUGUI tipText);

            ModeSelectPanel panel = panelRoot.gameObject.AddComponent<ModeSelectPanel>();
            var serialized = new SerializedObject(panel);
            serialized.FindProperty("playButton").objectReferenceValue = playButton;
            serialized.FindProperty("closeButton").objectReferenceValue = closeButton;
            serialized.FindProperty("backdropButton").objectReferenceValue = backdropButton;
            serialized.FindProperty("tipText").objectReferenceValue = tipText;
            serialized.FindProperty("group").objectReferenceValue = group;
            serialized.FindProperty("window").objectReferenceValue = window;

            SerializedProperty options = serialized.FindProperty("options");
            options.arraySize = rows.Count;
            for (int i = 0; i < rows.Count; i++)
            {
                ModeRow row = rows[i];
                SerializedProperty item = options.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("id").stringValue = row.Id;
                item.FindPropertyRelative("shortLabel").stringValue = row.ShortLabel;
                item.FindPropertyRelative("mode").enumValueIndex = (int)row.Mode;
                item.FindPropertyRelative("locked").boolValue = row.Locked;
                item.FindPropertyRelative("button").objectReferenceValue = row.Button;
                item.FindPropertyRelative("background").objectReferenceValue = row.Background;
                item.FindPropertyRelative("label").objectReferenceValue = row.Label;
                item.FindPropertyRelative("icon").objectReferenceValue = row.Icon;
                item.FindPropertyRelative("checkBox").objectReferenceValue = row.CheckBox;
                item.FindPropertyRelative("checkMark").objectReferenceValue = row.CheckMark;
                item.FindPropertyRelative("lockMark").objectReferenceValue = row.LockMark;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            panelRoot.gameObject.SetActive(false);
            return panel;
        }

        private sealed class ModeRow
        {
            public string Id, ShortLabel;
            public BattleGameMode Mode;
            public bool Locked;
            public Button Button;
            public Image Background, Icon, CheckBox;
            public TextMeshProUGUI Label;
            public GameObject CheckMark, LockMark;
        }

        private static RectTransform Column(RectTransform window, string name, int index, float top)
        {
            RectTransform column = NewRect(name, window);
            column.anchorMin = new Vector2(index / 3f, PanelBottom);
            column.anchorMax = new Vector2((index + 1) / 3f, top);
            column.offsetMin = Vector2.zero;
            column.offsetMax = Vector2.zero;
            return column;
        }

        private static TextMeshProUGUI ColumnTitle(RectTransform column, string title, float size)
        {
            TextMeshProUGUI text = AddText(column, "Baslik", title, size, Color.white, TextAlignmentOptions.Center, shadow: true);
            text.rectTransform.anchorMin = new Vector2(0f, 1f);
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.pivot = new Vector2(0.5f, 1f);
            text.rectTransform.offsetMin = new Vector2(24f, -130f);
            text.rectTransform.offsetMax = new Vector2(-24f, -24f);
            return text;
        }

        private static void BuildDoubleXpColumn(RectTransform window)
        {
            RectTransform column = Column(window, "SolPanel", 0, SidePanelTop);
            AddImage(column, Sprite("bg_teal"), Color.white, raycast: true).type = Image.Type.Simple;
            ColumnTitle(column, "İKİ KAT XP", 78f);

            TextMeshProUGUI hint = AddText(column, "Aciklama", "Bir reklam izle. Çift XP al!", 36f, Color.white, TextAlignmentOptions.Center, shadow: true);
            Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(560f, 60f));

            Image skull = AddIcon(column, "Kafatasi", "icon_skull", new Color(0.92f, 0.96f, 0.95f, 1f), new Vector2(0.5f, 0.52f), Vector2.zero, 210f);
            TextMeshProUGUI twoX = AddText(skull.transform, "2x", "2x", 54f, Hex("#5E7C78"), TextAlignmentOptions.Center);
            Place(twoX.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 62f), new Vector2(120f, 60f));

            Button activate = AddButton(column, "EtkinlestirButonu", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 120f), new Vector2(380f, 104f), Green, GreenLip);
            TextMeshProUGUI label = AddText(Face(activate), "Yazi", "Etkinleştir", 52f, Color.white, TextAlignmentOptions.Center, shadow: true);
            Stretch(label.rectTransform, 12f, 12f, 0f, 4f);
            ComingSoon.Add(activate);

            RectTransform track = NewRect("Ilerleme", column);
            Place(track, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(400f, 34f));
            AddImage(track, Sprite("ui_round"), new Color(0f, 0f, 0f, 0.35f)).pixelsPerUnitMultiplier = 2.4f;
            for (int i = 0; i < 3; i++)
            {
                RectTransform slot = NewRect($"Adim{i + 1}", track);
                Place(slot, new Vector2(i / 2f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((1 - i) * 30f, 0f), new Vector2(54f, 54f));
                AddImage(slot, Sprite("ui_circle"), Hex("#2F3C44"));
                AddIcon(slot, "Ikon", "icon_skull", new Color(1f, 1f, 1f, 0.45f), new Vector2(0.5f, 0.5f), Vector2.zero, 32f);
            }
        }

        private static List<ModeRow> BuildModeColumn(RectTransform window, out Button playButton)
        {
            RectTransform column = Column(window, "OrtaPanel", 1, CenterPanelTop);
            AddImage(column, null, Hex("#FDB927"), raycast: true);

            RectTransform header = NewRect("Manzara", column);
            header.anchorMin = new Vector2(0f, 0.47f);
            header.anchorMax = Vector2.one;
            header.offsetMin = Vector2.zero;
            header.offsetMax = Vector2.zero;
            AddImage(header, Sprite("mode_header"), Color.white).type = Image.Type.Simple;
            ColumnTitle(header, "Oyun modu", 78f);

            RectTransform list = NewRect("Secenekler", column);
            list.anchorMin = new Vector2(0.5f, 0f);
            list.anchorMax = new Vector2(0.5f, 0.47f);
            list.offsetMin = new Vector2(-200f, 0f);
            list.offsetMax = new Vector2(200f, 0f);
            VerticalLayoutGroup layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 26, 0);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var rows = new List<ModeRow>
            {
                ModeOptionRow(list, "solo", "SOLO", "SOLO", "icon_person", BattleGameMode.Bots, locked: false),
                ModeOptionRow(list, "rastgele_ikili", "RASTGELE İKİLİ", "İKİLİ", "icon_friends", BattleGameMode.Squad, locked: false),
                ModeOptionRow(list, "arkadas_ikilisi", "ARKADAŞ İKİLİSİ", "ARKADAŞ", "icon_friends", BattleGameMode.Squad, locked: true)
            };

            // Orta panelin alt kenarina binen buyuk yesil buton.
            playButton = AddButton(window, "CevrimiciOynaButonu", new Vector2(0.5f, PanelBottom), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -6f), new Vector2(400f, 118f), Green, GreenLip, lipHeight: 10f);
            TextMeshProUGUI playLabel = AddText(Face(playButton), "Yazi", "ÇEVRİMİÇİ OYNA", 54f, Color.white, TextAlignmentOptions.Center, shadow: true);
            Stretch(playLabel.rectTransform, 16f, 16f, 0f, 4f);

            return rows;
        }

        private static ModeRow ModeOptionRow(RectTransform list, string id, string title, string shortLabel, string icon,
            BattleGameMode mode, bool locked)
        {
            RectTransform row = NewRect($"Secenek_{id}", list);
            row.sizeDelta = new Vector2(400f, 84f);

            Image background = AddImage(row, Sprite("ui_round"), Hex("#DEDEDE"), raycast: true);
            background.pixelsPerUnitMultiplier = 2.2f;
            Button button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.None; // renkleri ModeSelectPanel boyar
            row.gameObject.AddComponent<UIPressScale>();

            Image iconImage = AddIcon(row, "Ikon", icon, Color.white, new Vector2(0f, 0.5f), new Vector2(44f, 0f), 52f);

            TextMeshProUGUI label = AddText(row, "Yazi", title, 38f, Color.white, TextAlignmentOptions.Left);
            Stretch(label.rectTransform, 82f, 92f, 0f, 2f);

            RectTransform box = NewRect("Kutu", row);
            Place(box, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-46f, 0f), new Vector2(58f, 58f));
            Image boxImage = AddImage(box, Sprite("ui_round"), Color.white);
            boxImage.pixelsPerUnitMultiplier = 3f;

            Image check = AddIcon(box, "Tik", "icon_check", GreenLip, new Vector2(0.5f, 0.5f), Vector2.zero, 44f);
            Image lockIcon = AddIcon(box, "Kilit", "icon_lock", Hex("#55555C"), new Vector2(0.5f, 0.5f), Vector2.zero, 34f);
            lockIcon.gameObject.SetActive(locked);

            return new ModeRow
            {
                Id = id,
                ShortLabel = shortLabel,
                Mode = mode,
                Locked = locked,
                Button = button,
                Background = background,
                Icon = iconImage,
                Label = label,
                CheckBox = boxImage,
                CheckMark = check.gameObject,
                LockMark = lockIcon.gameObject
            };
        }

        private static Button BuildTipsColumn(RectTransform window, out TextMeshProUGUI tipText)
        {
            RectTransform column = Column(window, "SagPanel", 2, SidePanelTop);
            AddImage(column, Sprite("bg_blue"), Color.white, raycast: true).type = Image.Type.Simple;
            ColumnTitle(column, "Savaş İpuçları", 74f);

            tipText = AddText(column, "Ipucu", "Arkadaşlarını Arkadaş İkilisi'ne çağır ve onlarla oyna!", 40f, Color.white,
                TextAlignmentOptions.Top, shadow: true);
            tipText.textWrappingMode = TextWrappingModes.Normal;
            tipText.rectTransform.anchorMin = new Vector2(0f, 1f);
            tipText.rectTransform.anchorMax = Vector2.one;
            tipText.rectTransform.pivot = new Vector2(0.5f, 1f);
            tipText.rectTransform.offsetMin = new Vector2(48f, -330f);
            tipText.rectTransform.offsetMax = new Vector2(-48f, -140f);

            AddIcon(column, "Siluet", "icon_friends", new Color(1f, 1f, 1f, 0.6f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), 280f);

            RectTransform close = NewRect("KapatButonu", column);
            Place(close, Vector2.one, Vector2.one, new Vector2(-18f, -18f), new Vector2(78f, 78f));
            Image closeImage = AddImage(close, Sprite("icon_close"), Color.white, raycast: true);
            closeImage.type = Image.Type.Simple;
            closeImage.preserveAspect = true;
            Button closeButton = close.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = closeImage;
            close.gameObject.AddComponent<UIPressScale>();
            return closeButton;
        }

        // ------------------------------------------------------------------
        // ASAMA 3: Yukleme ekrani (prefab; sahne degisiminde yasar)
        // ------------------------------------------------------------------

        private static bool EnsureLoadingScreen(MainMenuScreen screen, bool rebuild)
        {
            var serialized = new SerializedObject(screen);
            SerializedProperty property = serialized.FindProperty("loadingScreenPrefab");
            if (property.objectReferenceValue != null && !rebuild)
                return false;

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(LoadingPrefabPath);
            LoadingScreen prefab = existing != null && !rebuild
                ? existing.GetComponent<LoadingScreen>()
                : BuildLoadingPrefab();

            property.objectReferenceValue = prefab;
            serialized.ApplyModifiedProperties();
            return true;
        }

        private static LoadingScreen BuildLoadingPrefab()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI"))
                AssetDatabase.CreateFolder("Assets/Prefabs", "UI");

            var go = new GameObject("YuklemeEkrani", typeof(RectTransform)) { layer = 5 };
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000; // her seyin ustunde

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f; // yatay telefonlar: dikeyde hep 1080 birim
            go.AddComponent<GraphicRaycaster>();
            CanvasGroup group = go.AddComponent<CanvasGroup>();
            group.alpha = 1f; // prefab duzenlenirken gorunsun; calisirken 0'dan acilir

            var root = (RectTransform)go.transform;

            RectTransform background = NewRect("Arkaplan", root);
            Stretch(background, 0f, 0f, 0f, 0f);
            AddImage(background, null, Hex("#221C3A"), raycast: true);

            RectTransform runner = NewRect("KosanKarakter", root);
            Place(runner, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170f, 170f));
            Image runnerImage = AddImage(runner, Sprite("run_00"), Color.white);
            runnerImage.type = Image.Type.Simple;
            runnerImage.preserveAspect = true;
            UIFlipbook flipbook = runner.gameObject.AddComponent<UIFlipbook>();
            var flipbookSerialized = new SerializedObject(flipbook);
            SerializedProperty frames = flipbookSerialized.FindProperty("frames");
            frames.arraySize = 8;
            for (int i = 0; i < 8; i++)
                frames.GetArrayElementAtIndex(i).objectReferenceValue = Sprite($"run_{i:00}");
            flipbookSerialized.ApplyModifiedPropertiesWithoutUndo();

            RectTransform spinner = NewRect("Gosterge", root);
            Place(spinner, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(64f, -64f), new Vector2(64f, 64f));
            AddImage(spinner, Sprite("spinner"), new Color(1f, 1f, 1f, 0.75f)).type = Image.Type.Simple;
            spinner.gameObject.AddComponent<UISpin>();

            TextMeshProUGUI status = AddText(root, "Durum", string.Empty, 28f, Hex("#8C86AC"), TextAlignmentOptions.Center);
            Place(status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1200f, 44f));

            LoadingScreen loading = go.AddComponent<LoadingScreen>();
            var loadingSerialized = new SerializedObject(loading);
            loadingSerialized.FindProperty("group").objectReferenceValue = group;
            loadingSerialized.FindProperty("statusText").objectReferenceValue = status;
            loadingSerialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, LoadingPrefabPath);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<LoadingScreen>();
        }

        // ------------------------------------------------------------------
        // Yapi taslari
        // ------------------------------------------------------------------

        /// <summary>Kenarlikli altin kart (sol sutundaki kartlar).</summary>
        private static RectTransform Card(RectTransform parent, string name, Vector2 position, Vector2 size)
        {
            RectTransform card = NewRect(name, parent);
            Place(card, new Vector2(0f, 1f), new Vector2(0f, 1f), position, size);
            AddImage(card, Sprite("ui_round"), CardEdge, raycast: true);

            RectTransform inner = NewRect("Ic", card);
            Stretch(inner, 5f, 5f, 5f, 5f);
            AddImage(inner, Sprite("ui_round"), CardFill);
            return card;
        }

        private static TextMeshProUGUI CardHeader(RectTransform card, string title, Color band, Color textColor,
            float fontSize, bool shadow, float inset = 5f, float height = 80f)
        {
            RectTransform header = NewRect("Baslik", card);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = Vector2.one;
            header.pivot = new Vector2(0.5f, 1f);
            header.offsetMin = new Vector2(inset, -inset - height);
            header.offsetMax = new Vector2(-inset, -inset);
            AddImage(header, Sprite("ui_round"), band);

            TextMeshProUGUI text = AddText(header, "Yazi", title, fontSize, textColor, TextAlignmentOptions.Center, shadow);
            Stretch(text.rectTransform, 12f, 12f, 0f, 4f);
            return text;
        }

        private static Button SquareButton(RectTransform parent, string name, string icon, Vector2 anchor, Vector2 pivot,
            Vector2 position, Color face, Color lip)
        {
            Button button = AddButton(parent, name, anchor, pivot, position, new Vector2(128f, 128f), face, lip);
            AddIcon(Face(button), "Ikon", icon, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, 80f);
            ComingSoon.Add(button);
            return button;
        }

        /// <summary>
        /// Battlelands tarzi "kabarik" buton: altta koyu bir dudak, ustte
        /// renkli yuz ve yuzun ust yarisinda hafif parlama.
        /// </summary>
        private static Button AddButton(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position,
            Vector2 size, Color face, Color lip, float lipHeight = 9f)
        {
            RectTransform root = NewRect(name, parent);
            Place(root, anchor, pivot, position, size);

            RectTransform lipRect = NewRect("Alt", root);
            Stretch(lipRect, 0f, 0f, 0f, 0f);
            lipRect.offsetMin = new Vector2(0f, -lipHeight);
            lipRect.offsetMax = new Vector2(0f, -lipHeight);
            AddImage(lipRect, Sprite("ui_round"), lip);

            RectTransform faceRect = NewRect("Yuz", root);
            Stretch(faceRect, 0f, 0f, 0f, 0f);
            Image faceImage = AddImage(faceRect, Sprite("ui_round"), face, raycast: true);

            RectTransform shine = NewRect("Parlama", faceRect);
            shine.anchorMin = new Vector2(0f, 0.52f);
            shine.anchorMax = Vector2.one;
            shine.offsetMin = new Vector2(7f, 0f);
            shine.offsetMax = new Vector2(-7f, -6f);
            AddImage(shine, Sprite("ui_round"), new Color(1f, 1f, 1f, 0.18f));

            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = faceImage;
            ColorBlock colors = button.colors;
            colors.pressedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
            colors.disabledColor = new Color(0.65f, 0.65f, 0.65f, 1f);
            button.colors = colors;
            root.gameObject.AddComponent<UIPressScale>();
            return button;
        }

        private static RectTransform Face(Button button) => (RectTransform)button.transform.Find("Yuz");

        private static Image AddIcon(Transform parent, string name, string sprite, Color color, Vector2 anchor,
            Vector2 position, float size)
        {
            RectTransform rect = NewRect(name, parent);
            Place(rect, anchor, new Vector2(0.5f, 0.5f), position, new Vector2(size, size));
            Image image = AddImage(rect, Sprite(sprite), color);
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            return image;
        }

        private static TextMeshProUGUI AddText(Transform parent, string name, string value, float size, Color color,
            TextAlignmentOptions alignment, bool shadow = false)
        {
            RectTransform rect = NewRect(name, parent);
            Stretch(rect, 0f, 0f, 0f, 0f);

            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = _font;
            text.fontSharedMaterial = shadow ? _shadowMaterial : _plainMaterial;
            text.text = value;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.enableAutoSizing = true;
            text.fontSizeMax = size;
            text.fontSizeMin = Mathf.Round(size * 0.55f);
            text.fontSize = size;
            text.raycastTarget = false;
            return text;
        }

        private static Image AddImage(RectTransform rect, Sprite sprite, Color color, bool raycast = false)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycast;
            image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            return image;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 }; // UI
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static Sprite Sprite(string file)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}/{file}.png");
            if (sprite == null)
                Debug.LogWarning($"Ana Menü: {ArtFolder}/{file}.png sprite olarak bulunamadi.");
            return sprite;
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color color);
            return color;
        }
    }
}
#endif
