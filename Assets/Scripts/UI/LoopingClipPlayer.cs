using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace NewBattle.UI
{
    /// <summary>
    /// Bir Animator uzerinde tek bir klibi surekli tekrar oynatir (mac sonu
    /// ekranindaki karakterin zafer / yenilgi animasyonu). Tek seferlik
    /// klipler de bastan tekrar eder.
    /// </summary>
    public class LoopingClipPlayer : MonoBehaviour
    {
        private PlayableGraph _graph;
        private AnimationClipPlayable _playable;
        private AnimationClip _clip;

        public void Play(Animator animator, AnimationClip clip)
        {
            if (animator == null || clip == null)
                return;

            Stop();
            _clip = clip;

            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.enabled = true;

            _graph = PlayableGraph.Create("MacSonuAnimasyonu");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.UnscaledGameTime);
            _playable = AnimationClipPlayable.Create(_graph, clip);
            _playable.SetApplyFootIK(false); // ayak IK kapali: iri kafali/kisa bacakli karakterlerde (Ahmet) govdeyi one bukuyordu
            AnimationPlayableOutput.Create(_graph, "MacSonu", animator).SetSourcePlayable(_playable);
            _graph.Play();
        }

        private void Update()
        {
            if (!_graph.IsValid() || _clip == null || _clip.isLooping)
                return;

            if (_playable.GetTime() >= _clip.length)
                _playable.SetTime(0d);
        }

        private void OnDestroy() => Stop();

        private void Stop()
        {
            if (_graph.IsValid())
                _graph.Destroy();
        }
    }
}
