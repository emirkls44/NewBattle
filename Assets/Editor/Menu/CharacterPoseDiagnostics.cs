#if UNITY_EDITOR
using System.Linq;
using System.Text;
using NewBattle.Gameplay;
using NewBattle.Progression;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// Tools > NewBattle > Karakter Pozu Teshis: varsayilan lobi animasyonunu karaktere
    /// iki yoldan uygular ve govde egimini olcer:
    ///   A) Temiz model (karakterin kendi FBX'i)
    ///   B) Menudeki yol (eski prefab modelinin uzerine CharacterModelSwap ile)
    /// Sonuclari Console'a yazar. Gecici nesneler hemen silinir.
    /// </summary>
    internal static class CharacterPoseDiagnostics
    {
        [MenuItem("Tools/NewBattle/Karakter Pozu Teshis")]
        private static void Run()
        {
            CharacterLibrary library = CharacterLibrary.Instance;
            var lobby = AssetDatabase.FindAssets("t:LobbyAnimationLibrary")
                .Select(g => AssetDatabase.LoadAssetAtPath<LobbyAnimationLibrary>(AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault(l => l != null);
            AnimationClip clip = lobby != null && lobby.Default != null ? lobby.Default.clip : null;
            var oldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Arts/Characters/Character_V2/Prefabs/Character_V2_Test.prefab");

            var log = new StringBuilder();
            log.AppendLine($"[Teshis] Klip: {(clip != null ? clip.name + (clip.isHumanMotion ? " (Humanoid)" : " (GENERIC!)") : "YOK")}");

            foreach (string id in new[] { "ahmet", "ayse", "melek" })
            {
                CharacterDefinition character = library != null ? library.Find(id) : null;
                if (character == null || character.model == null)
                    continue;

                Transform modelRoot = character.model.transform;
                log.AppendLine($"[Teshis] {id}: FBX koku rot={modelRoot.localEulerAngles} scale={modelRoot.localScale}; " +
                               "ust cocuklar: " + string.Join(", ", modelRoot.Cast<Transform>()
                                   .Select(c => $"{c.name}(rot {c.localEulerAngles}, scale {c.localScale})")));

                // A) Temiz model
                GameObject clean = Object.Instantiate(character.model);
                try
                {
                    Animator animator = clean.GetComponent<Animator>() ?? clean.AddComponent<Animator>();
                    animator.avatar = character.avatar;
                    log.AppendLine($"[Teshis] {id} A (temiz): {Measure(animator, clip)}");
                }
                finally
                {
                    Object.DestroyImmediate(clean);
                }

                // B) Menudeki yol: eski modelin uzerine donusturme
                if (oldPrefab == null)
                    continue;

                GameObject swapped = Object.Instantiate(oldPrefab);
                try
                {
                    Animator animator = swapped.GetComponentInChildren<Animator>(true);
                    log.AppendLine($"[Teshis] {id} B eski kok: rot={animator.transform.localEulerAngles} scale={animator.transform.localScale}");
                    CharacterModelSwap.Apply(animator, character, library.Default);
                    log.AppendLine($"[Teshis] {id} B (menudeki yol): {Measure(animator, clip)}");
                }
                finally
                {
                    Object.DestroyImmediate(swapped);
                }
            }

            Debug.Log(log.ToString());
        }

        /// <summary>Dinlenme pozunda ve klip uygulandiktan sonra govde egimi / yuzun baktigi yon.</summary>
        private static string Measure(Animator animator, AnimationClip clip)
        {
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                return "avatar Humanoid degil!";

            string rest = Pose(animator);
            if (clip == null)
                return "dinlenme: " + rest;

            clip.SampleAnimation(animator.gameObject, 0.5f);
            string sampled = Pose(animator);

            // Oyundaki gibi: PlayableGraph ile (menu LobbyCharacterAnimator boyle oynatir).
            string withIk = PlayLikeGame(animator, clip, footIk: true);
            string noIk = PlayLikeGame(animator, clip, footIk: false);
            return $"dinlenme: {rest} | klip: {sampled} | oyundaki gibi+ayak IK: {withIk} | oyundaki gibi IK yok: {noIk}";
        }

        private static string PlayLikeGame(Animator animator, AnimationClip clip, bool footIk)
        {
            animator.runtimeAnimatorController = null;
            animator.enabled = true;
            animator.Rebind();
            PlayableGraph graph = PlayableGraph.Create("Teshis");
            try
            {
                var playable = AnimationClipPlayable.Create(graph, clip);
                playable.SetApplyFootIK(footIk);
                AnimationPlayableOutput.Create(graph, "Teshis", animator).SetSourcePlayable(playable);
                graph.Evaluate(0f);
                graph.Evaluate(0.5f);
                return Pose(animator);
            }
            finally
            {
                graph.Destroy();
            }
        }

        private static string Pose(Animator animator)
        {
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform right = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if (hips == null || head == null || left == null || right == null)
                return "kemik eksik";

            Transform root = animator.transform;
            Vector3 up = root.InverseTransformDirection(head.position - hips.position).normalized;
            Vector3 across = root.InverseTransformDirection(left.position - right.position);
            Vector3 facing = Vector3.Cross(across, Vector3.up).normalized; // govdenin onu (yaklasik)
            float tilt = Vector3.Angle(up, Vector3.up);
            float yaw = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
            return $"egim {tilt:0}°, yuz yonu {yaw:0}° (0 = kameraya/+Z), yukseklik {Vector3.Distance(hips.position, head.position):0.00}";
        }
    }
}
#endif
