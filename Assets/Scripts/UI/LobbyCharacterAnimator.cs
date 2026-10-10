using NewBattle.Progression;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace NewBattle.UI
{
    /// <summary>
    /// Menudeki karakterin lobi animasyonunu oynatir.
    ///
    /// Animator Controller'a dokunmadan, Playables ile dogrudan klip oynatir:
    /// kutuphaneye yeni animasyon eklemek icin controller'da durum/gecis
    /// kurmaya gerek kalmaz. Animasyon degisince kisa bir gecisle karisir.
    /// </summary>
    public class LobbyCharacterAnimator : MonoBehaviour
    {
        [SerializeField] private LobbyAnimationLibrary library;
        [SerializeField] private Animator animator;
        [SerializeField, Min(0f)] private float crossFadeSeconds = 0.3f;

        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private readonly AnimationClipPlayable[] _slots = new AnimationClipPlayable[2];
        private int _active = -1;
        private float _fade = 1f;
        private LobbyAnimation _playing;
        private RuntimeAnimatorController _savedController;

        public LobbyAnimationLibrary Library => library;
        public LobbyAnimation Playing => _playing;

        private void OnEnable()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (animator == null || library == null)
                return;

            // Oyun ici controller menude gerekmiyor; kendi klibimizi oynatacagiz.
            _savedController = animator.runtimeAnimatorController;
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.enabled = true;

            _graph = PlayableGraph.Create("LobbyAnimation");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            _mixer = AnimationMixerPlayable.Create(_graph, 2);
            AnimationPlayableOutput.Create(_graph, "Lobby", animator).SetSourcePlayable(_mixer);
            _graph.Play();

            PlayerProgress.Changed += PlayCurrent;
            PlayCurrent();
        }

        private void OnDisable()
        {
            PlayerProgress.Changed -= PlayCurrent;

            if (_graph.IsValid())
                _graph.Destroy();

            _active = -1;
            _playing = null;

            if (animator != null)
            {
                animator.runtimeAnimatorController = _savedController;
                animator.enabled = false;
            }
        }

        private void Update()
        {
            if (!_graph.IsValid() || _active < 0)
                return;

            // Tek seferlik klipler (selamlama vb.) lobide surekli tekrar etsin.
            AnimationClipPlayable current = _slots[_active];
            AnimationClip clip = current.GetAnimationClip();
            if (clip != null && !clip.isLooping && current.GetTime() >= clip.length)
                current.SetTime(0d);

            if (_fade < 1f)
            {
                _fade = crossFadeSeconds > 0f ? Mathf.Min(1f, _fade + Time.deltaTime / crossFadeSeconds) : 1f;
                ApplyWeights();
            }
        }

        /// <summary>Oyuncunun secili (yoksa default) animasyonunu oynatir.</summary>
        public void PlayCurrent()
        {
            if (library != null)
                Play(library.Current);
        }

        /// <summary>
        /// Herhangi bir animasyonu oynatir; kilit kontrolu yapmaz. Secim ekraninda
        /// kilitli animasyonu "onizleme" olarak gostermek icin de kullanilir.
        /// </summary>
        public void Play(LobbyAnimation animation)
        {
            if (!_graph.IsValid() || animation == null || animation.clip == null || animation == _playing)
                return;

            int slot = _active < 0 ? 0 : 1 - _active;

            if (_slots[slot].IsValid())
            {
                _graph.Disconnect(_mixer, slot);
                _slots[slot].Destroy();
            }

            AnimationClipPlayable playable = AnimationClipPlayable.Create(_graph, animation.clip);
            playable.SetSpeed(animation.speed);
            playable.SetApplyFootIK(true);
            _graph.Connect(playable, 0, _mixer, slot);
            _slots[slot] = playable;

            bool first = _active < 0;
            _active = slot;
            _fade = first ? 1f : 0f;
            _playing = animation;
            ApplyWeights();
        }

        private void ApplyWeights()
        {
            int other = 1 - _active;
            _mixer.SetInputWeight(_active, _fade);
            _mixer.SetInputWeight(other, _slots[other].IsValid() ? 1f - _fade : 0f);
        }
    }
}
