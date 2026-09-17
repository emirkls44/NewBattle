using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace NewBattle.ArtGen
{
    /// <summary>
    /// Oyuncu karakterini degistirmenin tamamini tek pencerede toplar:
    ///
    ///   1. Animasyon kliplerini ayarlar (dongu bayraklari)
    ///   2. Oyunun ihtiyaci olan Animator Controller'i sifirdan uretir
    ///   3. Modeli oyuncu prefabina takar ve boyunu ayarlar
    ///
    /// NEDEN BIR ARAC: Bu islerin hicbiri zor degil ama sirasi ve ayrintilari
    /// kolayca gozden kaciyor. En sik yapilan uc hata:
    ///   - Klipler dongusuz kaliyor: karakter bir adim atip donuyor
    ///   - Root motion acik kaliyor: animasyon karakteri surukluyor, ag konumuyla
    ///     kavga ediyor
    ///   - Animator parametreleri kodun bekledigi isimlerden farkli oluyor:
    ///     animasyon hic oynamiyor ama hata da vermiyor
    ///
    /// Kodun BEKLEDIGI parametreler (degistirilemez):
    ///   IsMoving (bool), HasWeapon (bool), Punch (trigger), Death (trigger)
    /// </summary>
    public class CharacterSwapWindow : EditorWindow
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/PlayerOnline.prefab";
        private const string ControllerFolder = "Assets/Characters/NewCharacter";

        /// <summary>Oyunun kullandigi animasyon yuvalari. Sira = arayuzdeki sira.</summary>
        private enum Slot
        {
            Idle,
            Run,
            ArmedIdle,
            ArmedRun,
            Punch,
            Death
        }

        private static readonly (Slot slot, string label, string help, bool loop)[] SlotInfo =
        {
            (Slot.Idle, "Bos elle bekleme", "Silahsiz dururken", true),
            (Slot.Run, "Bos elle kosma", "Silahsiz yururken", true),
            (Slot.ArmedIdle, "Silahla bekleme", "Silah elindeyken dururken", true),
            (Slot.ArmedRun, "Silahla kosma", "Silah elindeyken yururken", true),
            (Slot.Punch, "Yumruk", "Yakin dovus saldirisi", false),
            (Slot.Death, "Olum", "Can bitince", false)
        };

        /// <summary>Klip adinda aranacak anahtar kelimeler; ilk eslesen secilir.</summary>
        private static readonly Dictionary<Slot, string[]> AutoPickHints = new()
        {
            { Slot.Idle, new[] { "idle_5", "neutral idle", "idle" } },
            { Slot.Run, new[] { "run_fast_3_inplace", "running", "run", "walking" } },
            { Slot.ArmedIdle, new[] { "idlerifle", "rifle idle", "rifle_charge", "idle_11" } },
            { Slot.ArmedRun, new[] { "run_and_shoot", "walk_forward_while_shooting", "gunplay" } },
            { Slot.Punch, new[] { "right hook", "punch", "hook", "agree_gesture" } },
            { Slot.Death, new[] { "shot_in_the_back_and_fall", "falling_down", "death", "fall" } }
        };

        private GameObject _characterModel;
        private GameObject _animationSource;

        private readonly Dictionary<Slot, AnimationClip> _selection = new();
        private AnimationClip[] _availableClips = System.Array.Empty<AnimationClip>();
        private string[] _clipNames = System.Array.Empty<string>();

        private bool _setupClips = true;
        private bool _buildController = true;
        private bool _swapPrefab = true;
        private bool _matchHeight = true;

        private Vector2 _scroll;
        private string _status = "";

        [MenuItem("Tools/NewBattle/Karakteri Degistir", false, 13)]
        public static void Open()
        {
            CharacterSwapWindow window = GetWindow<CharacterSwapWindow>("Karakteri Degistir");
            window.minSize = new Vector2(440f, 560f);
            window.AutoDetect();
            window.Show();
        }

        #region Arayuz

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Karakter Degistirme", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Model ve animasyon dosyalarini sec, klipleri esle, tek dugmeye bas.\n" +
                "Islem oyuncu prefabini degistirir - once projeni yedekle veya commit at.",
                MessageType.Info);

            EditorGUILayout.Space(6f);
            DrawModelFields();

            EditorGUILayout.Space(8f);
            DrawClipMapping();

            EditorGUILayout.Space(8f);
            DrawOptions();

            EditorGUILayout.Space(10f);
            DrawRunButton();

            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.HelpBox(_status, MessageType.None);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawModelFields()
        {
            EditorGUILayout.LabelField("Dosyalar", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            _characterModel = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("Karakter modeli", "Iskeletli mesh'i tasiyan FBX."),
                _characterModel, typeof(GameObject), false);

            _animationSource = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("Animasyon dosyasi", "Animasyonlari tasiyan FBX."),
                _animationSource, typeof(GameObject), false);

            if (EditorGUI.EndChangeCheck())
                RefreshClipList();

            if (GUILayout.Button("Otomatik bul", GUILayout.Height(20f)))
                AutoDetect();

            ValidateRig(_characterModel, "Karakter modeli");
            ValidateRig(_animationSource, "Animasyon dosyasi");
        }

        /// <summary>
        /// Humanoid olmayan bir rig retarget edilemez; bu durumda animasyonlar
        /// sessizce oynamaz. Erken uyarmak, sonra saatlerce aramaktan iyidir.
        /// </summary>
        private void ValidateRig(GameObject model, string label)
        {
            if (model == null)
                return;

            string path = AssetDatabase.GetAssetPath(model);
            if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
                return;

            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                EditorGUILayout.HelpBox(
                    $"{label} Humanoid degil (su an: {importer.animationType}).\n" +
                    "Dosyayi sec > Inspector > Rig > Animation Type = Humanoid > Apply.",
                    MessageType.Error);
            }
        }

        private void DrawClipMapping()
        {
            EditorGUILayout.LabelField("Animasyon Eslestirme", EditorStyles.boldLabel);

            if (_availableClips.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "Hic klip bulunamadi. Animasyon dosyasini sectiginden ve " +
                    "Inspector > Animation > Import Animation'in acik oldugundan emin ol.",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField(
                $"{_availableClips.Length} klip bulundu.", EditorStyles.miniLabel);

            foreach ((Slot slot, string label, string help, bool loop) in SlotInfo)
            {
                _selection.TryGetValue(slot, out AnimationClip current);
                int index = System.Array.IndexOf(_availableClips, current);

                int picked = EditorGUILayout.Popup(
                    new GUIContent(label, help + (loop ? " (donguye alinir)" : "")),
                    index, _clipNames);

                if (picked >= 0 && picked < _availableClips.Length)
                    _selection[slot] = _availableClips[picked];
            }
        }

        private void DrawOptions()
        {
            EditorGUILayout.LabelField("Adimlar", EditorStyles.boldLabel);
            _setupClips = EditorGUILayout.Toggle("1. Klipleri ayarla (dongu)", _setupClips);
            _buildController = EditorGUILayout.Toggle("2. Animator olustur", _buildController);
            _swapPrefab = EditorGUILayout.Toggle("3. Prefaba tak", _swapPrefab);

            if (_swapPrefab)
            {
                EditorGUI.indentLevel++;
                _matchHeight = EditorGUILayout.Toggle("Boyu otomatik ayarla", _matchHeight);
                EditorGUI.indentLevel--;
            }
        }

        private void DrawRunButton()
        {
            bool ready = _characterModel != null && _animationSource != null &&
                         _availableClips.Length > 0;

            using (new EditorGUI.DisabledScope(!ready))
            {
                if (GUILayout.Button("KARAKTERI DEGISTIR", GUILayout.Height(38f)))
                    Run();
            }

            if (!ready)
                EditorGUILayout.LabelField("Once model ve animasyon dosyasini sec.", EditorStyles.miniLabel);
        }

        #endregion

        #region Bulma

        private void AutoDetect()
        {
            // Iskeletli mesh tasiyan ve animasyon tasiyan dosyalari ayirt ediyoruz:
            // animasyon dosyasinda klip var, karakter dosyasinda SkinnedMeshRenderer.
            foreach (string guid in AssetDatabase.FindAssets("t:Model"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/Characters"))
                    continue;

                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null)
                    continue;

                bool hasSkin = asset.GetComponentInChildren<SkinnedMeshRenderer>(true) != null;
                bool hasClips = LoadClips(asset).Length > 0;

                if (hasSkin && _characterModel == null)
                    _characterModel = asset;

                if (hasClips && _animationSource == null)
                    _animationSource = asset;
            }

            RefreshClipList();
            Repaint();
        }

        private static AnimationClip[] LoadClips(GameObject model)
        {
            if (model == null)
                return System.Array.Empty<AnimationClip>();

            string path = AssetDatabase.GetAssetPath(model);

            return AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                // Unity her modele gizli bir "__preview__" klibi koyar; o bizim degil.
                .Where(clip => !clip.name.StartsWith("__preview__"))
                .OrderBy(clip => clip.name)
                .ToArray();
        }

        /// <summary>
        /// Klip listesini tazeler. Sadece secili animasyon dosyasini degil,
        /// PROJEDEKI TUM humanoid kliplerini toplar - hepsi Humanoid oldugu icin
        /// eski Mixamo animasyonlari da yeni karaktere uyar ve yeni modelde
        /// olmayan bir hareket (ornegin yumruk) eskisinden odunc alinabilir.
        /// </summary>
        private void RefreshClipList()
        {
            List<AnimationClip> clips = new();

            if (_animationSource != null)
                clips.AddRange(LoadClips(_animationSource));

            foreach (string guid in AssetDatabase.FindAssets("t:Model"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
                    continue;

                if (importer.animationType != ModelImporterAnimationType.Human)
                    continue;

                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null || asset == _animationSource)
                    continue;

                clips.AddRange(LoadClips(asset));
            }

            _availableClips = clips.Distinct().ToArray();
            _clipNames = _availableClips
                .Select(clip => $"{clip.name}   ({System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(clip))})")
                .ToArray();

            AutoPick();
        }

        private void AutoPick()
        {
            foreach ((Slot slot, string[] hints) in AutoPickHints)
            {
                if (_selection.ContainsKey(slot) && _selection[slot] != null)
                    continue;

                AnimationClip match = null;

                // Ipuclari oncelik sirasinda: ilk tutan kazanir.
                foreach (string hint in hints)
                {
                    match = _availableClips.FirstOrDefault(
                        clip => clip.name.ToLowerInvariant().Contains(hint));

                    if (match != null)
                        break;
                }

                if (match != null)
                    _selection[slot] = match;
                else if (_availableClips.Length > 0)
                    _selection[slot] = _availableClips[0];
            }
        }

        #endregion

        #region Calistirma

        private void Run()
        {
            List<string> log = new();

            if (_setupClips)
            {
                log.Add(ConfigureClips());

                // SaveAndReimport elimizdeki AnimationClip nesnelerini gecersiz
                // kilar: Unity asset'i bastan yaratir, eski referanslar bos duser.
                // Animator'a bos motion baglamamak icin secimleri isimden tazeliyoruz.
                ReloadSelection();
            }

            AnimatorController controller = null;

            if (_buildController)
            {
                controller = BuildController(out string controllerLog);
                log.Add(controllerLog);
            }
            else
            {
                controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    $"{ControllerFolder}/PlayerAnimator_New.controller");
            }

            if (_swapPrefab)
                log.Add(SwapIntoPrefab(controller));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _status = string.Join("\n\n", log.Where(line => !string.IsNullOrEmpty(line)));
            Debug.Log("Karakter degistirme:\n" + _status);
        }

        /// <summary>
        /// Kliplerin dongu bayraklarini ayarlar.
        ///
        /// Unity klipleri varsayilan olarak DONGUSUZ getirir. Bekleme ve kosma
        /// animasyonlari dongusuz kalirsa karakter bir tur oynatip donar - en sik
        /// karsilasilan ve en cok kafa karistiran belirti budur.
        /// </summary>
        private string ConfigureClips()
        {
            // Hangi klip adi donguye alinacak: yuvalardan topluyoruz.
            HashSet<string> loopNames = new();

            foreach ((Slot slot, string _, string __, bool loop) in SlotInfo)
            {
                if (loop && _selection.TryGetValue(slot, out AnimationClip clip) && clip != null)
                    loopNames.Add(clip.name);
            }

            int changed = 0;
            HashSet<string> touchedFiles = new();

            foreach (AnimationClip clip in _selection.Values.Where(c => c != null).Distinct())
            {
                string path = AssetDatabase.GetAssetPath(clip);

                if (!touchedFiles.Add(path))
                    continue;

                if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
                    continue;

                ModelImporterClipAnimation[] clips = importer.clipAnimations;

                // clipAnimations bos ise Unity varsayilanlari kullaniyor demektir;
                // degistirebilmek icin once varsayilanlari kopyalamamiz gerekir.
                if (clips == null || clips.Length == 0)
                    clips = importer.defaultClipAnimations;

                bool dirty = false;

                for (int i = 0; i < clips.Length; i++)
                {
                    bool shouldLoop = loopNames.Contains(clips[i].name);

                    if (clips[i].loopTime == shouldLoop)
                        continue;

                    clips[i].loopTime = shouldLoop;
                    dirty = true;
                }

                if (!dirty)
                    continue;

                importer.clipAnimations = clips;
                importer.SaveAndReimport();
                changed++;
            }

            return $"1. Klipler: {changed} dosya guncellendi, " +
                   $"{loopNames.Count} klip donguye alindi.";
        }

        /// <summary>
        /// Secili klipleri (dosya yolu + klip adi) uzerinden yeniden yukler.
        /// Reimport sonrasi cagrilir.
        /// </summary>
        private void ReloadSelection()
        {
            // Once ne sectigimizi hatirla, sonra listeyi tazele, sonra ayni
            // isimleri yeni nesnelerde bul.
            Dictionary<Slot, (string path, string name)> wanted = new();

            foreach (KeyValuePair<Slot, AnimationClip> pair in _selection)
            {
                if (pair.Value == null)
                    continue;

                wanted[pair.Key] = (AssetDatabase.GetAssetPath(pair.Value), pair.Value.name);
            }

            _selection.Clear();
            RefreshClipList();

            foreach (KeyValuePair<Slot, (string path, string name)> pair in wanted)
            {
                AnimationClip match = _availableClips.FirstOrDefault(
                    clip => clip.name == pair.Value.name &&
                            AssetDatabase.GetAssetPath(clip) == pair.Value.path);

                if (match != null)
                    _selection[pair.Key] = match;
            }

            AutoPick();
        }

        #endregion

        #region Animator

        private AnimatorController BuildController(out string log)
        {
            EnsureFolder(ControllerFolder);
            string path = $"{ControllerFolder}/PlayerAnimator_New.controller";

            // Varsa sifirdan kuruyoruz: yarim kalmis eski bir kurulum uzerine
            // eklemek, izini surmesi zor cakismalar yaratir.
            AssetDatabase.DeleteAsset(path);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("HasWeapon", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Punch", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            AnimatorState idle = AddState(machine, "Idle", Slot.Idle, new Vector3(260f, 0f, 0f));
            AnimatorState run = AddState(machine, "Run", Slot.Run, new Vector3(260f, 80f, 0f));
            AnimatorState armedIdle = AddState(machine, "ArmedIdle", Slot.ArmedIdle, new Vector3(560f, 0f, 0f));
            AnimatorState armedRun = AddState(machine, "ArmedRun", Slot.ArmedRun, new Vector3(560f, 80f, 0f));
            AnimatorState punch = AddState(machine, "Punch", Slot.Punch, new Vector3(260f, 180f, 0f));
            AnimatorState death = AddState(machine, "Death", Slot.Death, new Vector3(560f, 260f, 0f));

            machine.defaultState = idle;

            // Dort yonlu gecisler. Her gecisin kosullari TEK BIR durumu tarif eder,
            // boylece iki gecis ayni anda gecerli olup yanip sonme yasanmaz.
            Connect(idle, run, ("IsMoving", true), ("HasWeapon", false));
            Connect(run, idle, ("IsMoving", false), ("HasWeapon", false));

            Connect(armedIdle, armedRun, ("IsMoving", true), ("HasWeapon", true));
            Connect(armedRun, armedIdle, ("IsMoving", false), ("HasWeapon", true));

            Connect(idle, armedIdle, ("HasWeapon", true));
            Connect(run, armedRun, ("HasWeapon", true));
            Connect(armedIdle, idle, ("HasWeapon", false));
            Connect(armedRun, run, ("HasWeapon", false));

            // Yumruk ve olum her durumdan tetiklenebilmeli.
            AnimatorStateTransition punchTransition = machine.AddAnyStateTransition(punch);
            punchTransition.AddCondition(AnimatorConditionMode.If, 0f, "Punch");
            punchTransition.duration = 0.05f;
            punchTransition.hasExitTime = false;
            // Yumruk oynarken tekrar tetiklenip basa sarmasin.
            punchTransition.canTransitionToSelf = false;

            // Yumruk bitince kendiliginden donsun; kod ayrica bir sey soylemiyor.
            AnimatorStateTransition punchExit = punch.AddTransition(idle);
            punchExit.hasExitTime = true;
            punchExit.exitTime = 0.8f;
            punchExit.duration = 0.15f;

            AnimatorStateTransition deathTransition = machine.AddAnyStateTransition(death);
            deathTransition.AddCondition(AnimatorConditionMode.If, 0f, "Death");
            deathTransition.duration = 0.1f;
            deathTransition.hasExitTime = false;
            deathTransition.canTransitionToSelf = false;

            EditorUtility.SetDirty(controller);

            log = $"2. Animator olusturuldu: {path}\n" +
                  "   Parametreler: IsMoving, HasWeapon, Punch, Death";

            return controller;
        }

        private AnimatorState AddState(AnimatorStateMachine machine, string name, Slot slot, Vector3 position)
        {
            AnimatorState state = machine.AddState(name, position);

            if (_selection.TryGetValue(slot, out AnimationClip clip) && clip != null)
            {
                state.motion = clip;
            }
            else
            {
                // Bos bir state sessizce "animasyon oynamiyor" olarak gorunur.
                // Sebebini burada soyluyoruz ki aranacak yer belli olsun.
                Debug.LogWarning(
                    $"Animator: '{name}' durumu icin klip secilmedi. " +
                    "Bu harekette karakter hareketsiz kalir.");
            }

            // Write Defaults kapali: bu state'in dokunmadigi ozellikler her
            // karede varsayilana geri yazilmaz. Tek katmanli bir kurulumda ikisi
            // de calisir ama kapali olan ileride ikinci bir katman eklendiginde
            // (ornegin ust govde nisan katmani) catisma cikarmaz.
            //
            // Root motion bu ayarla ILGILI DEGIL; o Animator uzerinde
            // applyRootMotion = false ile kapatiliyor (bkz. InstallNewModel).
            state.writeDefaultValues = false;

            return state;
        }

        private static void Connect(AnimatorState from, AnimatorState to,
            params (string parameter, bool value)[] conditions)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0.12f;

            foreach ((string parameter, bool value) in conditions)
            {
                transition.AddCondition(
                    value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);
            }
        }

        #endregion

        #region Prefab

        private string SwapIntoPrefab(AnimatorController controller)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null)
                return $"3. HATA: {PlayerPrefabPath} bulunamadi.";

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            string result;

            try
            {
                int removed = RemoveOldModel(root);
                GameObject model = InstallNewModel(root, controller, out float scale);

                if (model == null)
                {
                    result = "3. HATA: Yeni model prefaba eklenemedi.";
                }
                else
                {
                    WirePlayerController(root, model);
                    PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);

                    result =
                        $"3. Prefab guncellendi: {removed} eski model kaldirildi, " +
                        $"'{model.name}' takildi (olcek {scale:0.000}x).";
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return result;
        }

        /// <summary>
        /// Eski karakter modelini siler.
        ///
        /// Modeli, iskeletli mesh tasiyan cocuk olarak tespit ediyoruz. Isme gore
        /// aramak kirilgan olurdu; silah gorseli de bir mesh ama SkinnedMeshRenderer
        /// tasimadigi icin bu testten gecmez ve yerinde kalir.
        /// </summary>
        private static int RemoveOldModel(GameObject root)
        {
            List<GameObject> doomed = new();

            foreach (Transform child in root.transform)
            {
                bool isSkinned = child.GetComponentInChildren<SkinnedMeshRenderer>(true) != null;
                bool hasAnimator = child.GetComponentInChildren<Animator>(true) != null;

                if (isSkinned || hasAnimator)
                    doomed.Add(child.gameObject);
            }

            foreach (GameObject target in doomed)
                Object.DestroyImmediate(target);

            return doomed.Count;
        }

        private GameObject InstallNewModel(GameObject root, AnimatorController controller, out float scale)
        {
            scale = 1f;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(_characterModel, root.transform);
            if (instance == null)
                return null;

            // Prefab baglantisini kopariyoruz: model dosyasi yeniden import
            // edildiginde prefabdaki ayarlarimizin ezilmesini istemiyoruz.
            PrefabUtility.UnpackPrefabInstance(
                instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            instance.name = "CharacterModel";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            if (_matchHeight)
                scale = MatchHeightToController(root, instance);

            Animator animator = instance.GetComponentInChildren<Animator>(true);

            if (animator == null)
                animator = instance.AddComponent<Animator>();

            animator.avatar = LoadAvatar(_characterModel);
            animator.runtimeAnimatorController = controller;

            // Konumu ag katmani suruyor; animasyonun karakteri tasimasina izin yok.
            animator.applyRootMotion = false;

            // Uzak oyuncular ekran disindayken de animasyon islesin: aksi halde
            // kameraya girdiklerinde animasyon bir an ziplayarak duzeliyor.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            return instance;
        }

        private static Avatar LoadAvatar(GameObject model)
        {
            string path = AssetDatabase.GetAssetPath(model);

            return AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<Avatar>()
                .FirstOrDefault();
        }

        /// <summary>
        /// Modeli CharacterController'in boyuna gore olcekler.
        ///
        /// Carpisma hacmi ve gorsel boy birbirini tutmazsa oyun yalanci hissettirir:
        /// karakter duvara gorunurde uzakken takilir ya da icine girer.
        /// </summary>
        private static float MatchHeightToController(GameObject root, GameObject model)
        {
            CharacterController controller = root.GetComponent<CharacterController>();

            if (controller == null)
                return 1f;

            Bounds? bounds = null;

            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if (bounds.HasValue)
                {
                    Bounds current = bounds.Value;
                    current.Encapsulate(renderer.bounds);
                    bounds = current;
                }
                else
                {
                    bounds = renderer.bounds;
                }
            }

            if (!bounds.HasValue || bounds.Value.size.y < 0.001f)
                return 1f;

            float scale = controller.height / bounds.Value.size.y;
            model.transform.localScale *= scale;

            return scale;
        }

        private static void WirePlayerController(GameObject root, GameObject model)
        {
            PlayerController player = root.GetComponent<PlayerController>();

            if (player == null)
                return;

            SerializedObject serialized = new(player);
            SerializedProperty animProperty = serialized.FindProperty("anim");

            if (animProperty != null)
                animProperty.objectReferenceValue = model.GetComponentInChildren<Animator>(true);

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string[] parts = folder.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";

                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }

        #endregion
    }
}
