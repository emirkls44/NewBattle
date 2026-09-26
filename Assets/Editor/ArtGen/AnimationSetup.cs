using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace NewBattle.ArtGen
{
    /// <summary>
    /// Karakterin animasyon altyapisini tek dugmeyle kurar:
    ///
    ///   1. Klip ayarlari: dongusu eksik yuruyus klipleri dongulu, dongulu
    ///      kalmis yumruk tek seferlik olur.
    ///   2. Ust govde maskesi: kollar, gogus ve kafa.
    ///   3. PlayerAnimator_Soft: iki katmanli yeni Animator.
    ///        Base Layer : hiza ve yone gore karisan yurume / kosu / geri yurume
    ///        UpperBody  : silah tutus, yumruk, rahat kollar (agirligi kod yonetir)
    ///   4. Oyuncu prefabi: Animator'a yeni kontrolcu, koke CharacterAnimationDriver.
    ///
    /// Eski PlayerAnimator_New silinmez; geri donmek istersen prefabdaki
    /// Animator'a onu tekrar atamak yeterli.
    ///
    /// Idempotent: tekrar calistirmak kontrolcuyu bastan kurar, kopya uretmez.
    /// </summary>
    public static class AnimationSetup
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/PlayerOnline.prefab";
        private const string AnimationFolder = "Assets/Arts/Character_V2_Animator";
        private const string MergedClipsPath = AnimationFolder + "/Meshy_AI_Tie_Guy_biped_Meshy_Merged_Animations.fbx";
        private const string RifleIdlePath = AnimationFolder + "/Meshy_AI_Tie_Guy_biped_Character_output@Rifle Aiming Idle.fbx";
        private const string PunchPath = AnimationFolder + "/Aj@Right Hook.fbx";
        private const string ControllerPath = AnimationFolder + "/PlayerAnimator_Soft.controller";
        private const string MaskPath = AnimationFolder + "/UpperBody.mask";

        private const string IdleClip = "Idle_11";
        private const string WalkClip = "Walking";
        private const string RunClip = "Running";
        private const string ArmedRunClip = "Run_and_Shoot";
        private const string BackpedalClip = "Walk_Backward_with_Bow_1";
        private const string DeathClip = "falling_down";
        private const string RifleIdleClip = "Rifle Aiming Idle";
        private const string PunchClip = "Right Hook";

        /// <summary>
        /// Oyuncunun kosu hizi (PlayerMoveState.moveSpeed). Blend esikleri buna
        /// gore: tam hizda kosu klibi, calidaki 3.5 m/s'de yuruyus ile kosu karisimi.
        /// </summary>
        private const float RunSpeed = 5f;
        private const float WalkSpeed = 2f;

        [MenuItem("Tools/NewBattle/Gorsel Yenileme/1 - Animasyonlari Kur", false, 40)]
        public static void Run()
        {
            List<string> log = new();

            if (!RunSilently(log))
            {
                Debug.LogError("Animasyon kurulumu yarida kaldi:\n" + string.Join("\n", log));
                return;
            }

            Debug.Log("Animasyon kurulumu tamam:\n" + string.Join("\n", log));
        }

        /// <summary>Hepsini Uygula komutu icin: sonucu log listesine yazar.</summary>
        public static bool RunSilently(List<string> log)
        {
            ConfigureClips(MergedClipsPath, new Dictionary<string, bool>
            {
                { IdleClip, true },
                { WalkClip, true },
                { RunClip, true },
                { ArmedRunClip, true },
                { BackpedalClip, true },
                { DeathClip, false }
            }, log);
            ConfigureClips(RifleIdlePath, new Dictionary<string, bool> { { RifleIdleClip, true } }, log);
            ConfigureClips(PunchPath, new Dictionary<string, bool> { { PunchClip, false } }, log);

            Dictionary<string, AnimationClip> clips = LoadClips(
                (MergedClipsPath, new[] { IdleClip, WalkClip, RunClip, ArmedRunClip, BackpedalClip, DeathClip }),
                (RifleIdlePath, new[] { RifleIdleClip }),
                (PunchPath, new[] { PunchClip }));

            string[] missing = new[]
                {
                    IdleClip, WalkClip, RunClip, ArmedRunClip, BackpedalClip, DeathClip, RifleIdleClip, PunchClip
                }
                .Where(name => !clips.ContainsKey(name))
                .ToArray();

            if (missing.Length > 0)
            {
                log.Add("HATA: Su klipler bulunamadi: " + string.Join(", ", missing));
                return false;
            }

            AvatarMask mask = BuildUpperBodyMask();
            log.Add($"Ust govde maskesi: {MaskPath}");

            AnimatorController controller = BuildController(clips, mask);
            log.Add($"Animator: {ControllerPath} (Base Layer + UpperBody)");

            if (!InstallIntoPrefab(controller, log))
                return false;

            AssetDatabase.SaveAssets();
            return true;
        }

        #region Klipler

        private static void ConfigureClips(string path, Dictionary<string, bool> loops, List<string> log)
        {
            if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
            {
                log.Add($"UYARI: {path} bulunamadi, klip ayarlari atlandi.");
                return;
            }

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
                clips = importer.defaultClipAnimations;

            List<string> changed = new();

            foreach (ModelImporterClipAnimation clip in clips)
            {
                if (!loops.TryGetValue(clip.name, out bool loop))
                    continue;

                bool dirty = false;

                if (clip.loopTime != loop)
                {
                    clip.loopTime = loop;
                    dirty = true;
                }

                // Loop Pose: klibin son karesini ilkine yumusakca baglar. Dongu
                // icin tasarlanmamis yuruyus kliplerinde adim basinda seken
                // "takilma"yi bu gideriyor.
                if (loop && !clip.loopPose)
                {
                    clip.loopPose = true;
                    dirty = true;
                }

                if (dirty)
                    changed.Add($"{clip.name} (dongu: {(loop ? "acik" : "kapali")})");
            }

            if (changed.Count == 0)
                return;

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            log.Add("Klip ayari: " + string.Join(", ", changed));
        }

        private static Dictionary<string, AnimationClip> LoadClips(params (string path, string[] names)[] sources)
        {
            Dictionary<string, AnimationClip> result = new();

            foreach ((string path, string[] names) in sources)
            {
                foreach (AnimationClip clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())
                {
                    if (clip.name.StartsWith("__preview__") || !names.Contains(clip.name))
                        continue;

                    result[clip.name] = clip;
                }
            }

            return result;
        }

        #endregion

        #region Maske

        private static AvatarMask BuildUpperBodyMask()
        {
            AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);

            if (mask == null)
            {
                mask = new AvatarMask { name = "UpperBody" };
                AssetDatabase.CreateAsset(mask, MaskPath);
            }

            AvatarMaskBodyPart[] upperParts =
            {
                AvatarMaskBodyPart.Body,
                AvatarMaskBodyPart.Head,
                AvatarMaskBodyPart.LeftArm,
                AvatarMaskBodyPart.RightArm,
                AvatarMaskBodyPart.LeftFingers,
                AvatarMaskBodyPart.RightFingers
            };

            for (AvatarMaskBodyPart part = 0; part < AvatarMaskBodyPart.LastBodyPart; part++)
                mask.SetHumanoidBodyPartActive(part, upperParts.Contains(part));

            EditorUtility.SetDirty(mask);
            return mask;
        }

        #endregion

        #region Animator

        private static AnimatorController BuildController(Dictionary<string, AnimationClip> clips, AvatarMask mask)
        {
            // Yarim kalmis eski bir kurulumun uzerine eklemek yerine bastan kur.
            AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            controller.AddParameter("MoveZ", AnimatorControllerParameterType.Float);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = "LocomotionSpeed",
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1f
            });
            // Kodun zaten yazdigi parametreler. IsMoving artik gecislerde
            // kullanilmiyor ama eksik olursa her karede uyari basilir.
            controller.AddParameter("HasWeapon", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Punch", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);

            BuildBaseLayer(controller, clips);
            BuildUpperLayer(controller, clips, mask);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void BuildBaseLayer(AnimatorController controller, Dictionary<string, AnimationClip> clips)
        {
            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            // Geri yurume klibi yavas bir yuruyus; tam hizda geri kacarken
            // bacaklar yetissin diye hizlandiriliyor.
            AnimatorState unarmed = AddLocomotion(controller, "Locomotion", new Vector3(300f, 0f, 0f),
                (clips[BackpedalClip], -RunSpeed, 2f),
                (clips[IdleClip], 0f, 1f),
                (clips[WalkClip], WalkSpeed, 1.15f),
                (clips[RunClip], RunSpeed, 1f));

            AnimatorState armed = AddLocomotion(controller, "ArmedLocomotion", new Vector3(300f, 120f, 0f),
                (clips[BackpedalClip], -RunSpeed, 2f),
                (clips[RifleIdleClip], 0f, 1f),
                (clips[ArmedRunClip], RunSpeed, 1f));

            machine.defaultState = unarmed;

            Connect(unarmed, armed, 0.15f, ("HasWeapon", true));
            Connect(armed, unarmed, 0.15f, ("HasWeapon", false));

            AnimatorState death = machine.AddState("Death", new Vector3(620f, 60f, 0f));
            death.motion = clips[DeathClip];
            death.writeDefaultValues = false;

            AnimatorStateTransition toDeath = machine.AddAnyStateTransition(death);
            toDeath.AddCondition(AnimatorConditionMode.If, 0f, "Death");
            toDeath.duration = 0.1f;
            toDeath.hasExitTime = false;
            toDeath.canTransitionToSelf = false;
        }

        private static AnimatorState AddLocomotion(AnimatorController controller, string name, Vector3 position,
            params (AnimationClip clip, float threshold, float timeScale)[] children)
        {
            AnimatorState state = controller.CreateBlendTreeInController(name, out BlendTree tree, 0);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            // CreateBlendTreeInController durumu rastgele bir yere koyuyor;
            // Animator penceresinde okunakli dursun.
            ChildAnimatorState[] states = machine.states;
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i].state == state)
                    states[i].position = position;
            }
            machine.states = states;

            tree.name = name;
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "MoveZ";
            tree.useAutomaticThresholds = false;

            foreach ((AnimationClip clip, float threshold, _) in children)
                tree.AddChild(clip, threshold);

            ChildMotion[] motions = tree.children;
            for (int i = 0; i < motions.Length; i++)
                motions[i].timeScale = children[i].timeScale;
            tree.children = motions;

            state.speedParameterActive = true;
            state.speedParameter = "LocomotionSpeed";
            state.writeDefaultValues = false;
            return state;
        }

        private static void BuildUpperLayer(AnimatorController controller, Dictionary<string, AnimationClip> clips,
            AvatarMask mask)
        {
            controller.AddLayer("UpperBody");

            // layers dizisi kopya doner: degistirip geri yazmak gerekiyor.
            AnimatorControllerLayer[] layers = controller.layers;
            AnimatorControllerLayer upper = layers[1];
            upper.avatarMask = mask;
            upper.blendingMode = AnimatorLayerBlendingMode.Override;
            // Agirligi CharacterAnimationDriver yonetir. 0 ile baslamak, surucu
            // olmayan bir sahnede (onizleme gibi) ust govdenin donuk kalmasini onler.
            upper.defaultWeight = 0f;
            layers[1] = upper;
            controller.layers = layers;

            AnimatorStateMachine machine = controller.layers[1].stateMachine;

            AnimatorState relaxed = AddState(machine, "Relaxed", clips[IdleClip], new Vector3(300f, 0f, 0f));
            AnimatorState rifleHold = AddState(machine, "RifleHold", clips[RifleIdleClip], new Vector3(300f, 120f, 0f));
            AnimatorState punch = AddState(machine, "Punch", clips[PunchClip], new Vector3(620f, 60f, 0f));

            machine.defaultState = relaxed;

            Connect(relaxed, rifleHold, 0.15f, ("HasWeapon", true));
            Connect(rifleHold, relaxed, 0.15f, ("HasWeapon", false));

            AnimatorStateTransition toPunch = machine.AddAnyStateTransition(punch);
            toPunch.AddCondition(AnimatorConditionMode.If, 0f, "Punch");
            toPunch.duration = 0.05f;
            toPunch.hasExitTime = false;
            toPunch.canTransitionToSelf = false;

            // Yumruk bitince elde ne varsa ona don. Kod ayrica bir sey soylemiyor.
            AddExit(punch, relaxed, ("HasWeapon", false));
            AddExit(punch, rifleHold, ("HasWeapon", true));
        }

        private static AnimatorState AddState(AnimatorStateMachine machine, string name, Motion motion,
            Vector3 position)
        {
            AnimatorState state = machine.AddState(name, position);
            state.motion = motion;

            // Write Defaults kapali: ust katmanin dokunmadigi kemikler alt
            // katmandan gelir, varsayilan poza geri yazilmaz.
            state.writeDefaultValues = false;
            return state;
        }

        private static void Connect(AnimatorState from, AnimatorState to, float duration,
            params (string parameter, bool value)[] conditions)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = duration;

            foreach ((string parameter, bool value) in conditions)
            {
                transition.AddCondition(
                    value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);
            }
        }

        private static void AddExit(AnimatorState from, AnimatorState to, (string parameter, bool value) condition)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = true;
            transition.exitTime = 0.8f;
            transition.duration = 0.15f;
            transition.AddCondition(
                condition.value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, condition.parameter);
        }

        #endregion

        #region Prefab

        private static bool InstallIntoPrefab(AnimatorController controller, List<string> log)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null)
            {
                log.Add($"HATA: {PlayerPrefabPath} bulunamadi.");
                return false;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

            try
            {
                Animator animator = root.GetComponentInChildren<Animator>(true);

                if (animator == null)
                {
                    log.Add("HATA: Oyuncu prefabinda Animator yok.");
                    return false;
                }

                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                // Ekranda olmayan karakterin kemiklerini yazma: mobilde 16 karakterin
                // cogu kamera disinda. State makinesi yine calisir, tetikler kacmaz.
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                CharacterAnimationDriver driver = root.GetComponent<CharacterAnimationDriver>();
                if (driver == null)
                    driver = root.AddComponent<CharacterAnimationDriver>();

                SerializedObject serialized = new(driver);
                serialized.FindProperty("animator").objectReferenceValue = animator;
                serialized.FindProperty("model").objectReferenceValue =
                    animator.transform != root.transform ? animator.transform : null;

                GameObject rifleVisual = FindRifleVisual(root);
                serialized.FindProperty("weaponKick").objectReferenceValue =
                    rifleVisual != null ? rifleVisual.transform : null;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                log.Add($"Prefab: {PlayerPrefabPath} -> yeni Animator + CharacterAnimationDriver" +
                        (rifleVisual == null ? " (UYARI: silah gorseli bulunamadi, geri tepme kapali)" : ""));
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        internal static GameObject FindRifleVisual(GameObject root)
        {
            PlayerLoadout loadout = root.GetComponent<PlayerLoadout>();
            if (loadout == null)
                return null;

            SerializedProperty property = new SerializedObject(loadout).FindProperty("rifleVisual");
            return property != null ? property.objectReferenceValue as GameObject : null;
        }

        #endregion
    }
}
