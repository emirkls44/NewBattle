using System.Collections.Generic;
using UnityEngine;

namespace NewBattle.Audio
{
    /// <summary>
    /// Kodun her yerinden tek satirla ses calmak icin:
    ///   Sfx.Play("ates_tufek", transform.position);  // dunyada: uzaktaysa kisik
    ///   Sfx.PlayUI("buton");                         // arayuz
    /// Sesin klibi, seviyesi ve mesafesi SesKutuphanesi dosyasindan gelir.
    /// </summary>
    public static class Sfx
    {
        public static void Play(string id, float volumeScale = 1f) =>
            AudioDirector.Instance?.Play(id, null, volumeScale);

        public static void Play(string id, Vector3 position, float volumeScale = 1f) =>
            AudioDirector.Instance?.Play(id, position, volumeScale);

        public static void PlayUI(string id) => AudioDirector.Instance?.Play(id, null, 1f, ui: true);

        /// <summary>
        /// Oyun olayinin sesi ama mesafesiz (alan sireni, isabet, toplama). Arayuz sesi
        /// gibi calar fakat mac sonu ekraninda (MuteWorld) susar.
        /// </summary>
        public static void PlayHud(string id)
        {
            if (!AudioDirector.WorldMuted)
                PlayUI(id);
        }

        /// <summary>
        /// Mac sonu ekraninda: arkadaki catisma, adim, ruzgar gibi oyun sesleri susar;
        /// arayuz sesleri ve muzik calmaya devam eder. Sahne yeniden yuklenince kalkar.
        /// </summary>
        public static void MuteWorld(bool muted) => AudioDirector.SetWorldMuted(muted);

        /// <summary>Donen bir ses baslatir (ruzgar gibi). Durdurmak icin donen numarayi sakla.</summary>
        public static int StartLoop(string id) => AudioDirector.Instance != null ? AudioDirector.Instance.StartLoop(id) : 0;

        public static void SetLoopVolume(int handle, float volumeScale) => AudioDirector.Instance?.SetLoopVolume(handle, volumeScale);

        public static void StopLoop(int handle) => AudioDirector.Instance?.StopLoop(handle);
    }

    public static class Music
    {
        public static void Play(string id) => AudioDirector.Instance?.PlayMusic(id);

        public static void Stop() => AudioDirector.Instance?.StopMusic();
    }

    /// <summary>
    /// Sesleri calan tek nesne. Ilk ses istendiginde kendiliginden olusur ve
    /// sahne degisse de yasar.
    ///
    /// Oyun ustten bakisli oldugu icin Unity'nin 3D sesi yerine mesafeyi ve
    /// sag/sol dagilimi KENDIMIZ hesapliyoruz: dinleyici kameranin degil
    /// oyuncunun yerinde kabul edilir (kamera 15+ metre yukarida).
    /// </summary>
    public class AudioDirector : MonoBehaviour
    {
        private const float MusicFadeSeconds = 0.8f;
        private const float PanDistance = 14f;

        private static AudioDirector _instance;
        private static bool _quitting;

        public static AudioDirector Instance
        {
            get
            {
                if (_instance != null || _quitting || !Application.isPlaying)
                    return _instance;

                if (SoundLibrary.Instance == null)
                    return null;

                var go = new GameObject("SesYoneticisi");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<AudioDirector>();
                return _instance;
            }
        }

        private SoundLibrary _library;
        private AudioSource[] _voices;
        private float[] _voiceBaseVolume;
        private readonly Dictionary<string, float> _lastPlayed = new();
        private readonly HashSet<string> _warned = new();

        private readonly Dictionary<int, (AudioSource source, float baseVolume)> _loops = new();
        private int _nextLoopHandle = 1;

        private AudioSource _music;
        private AudioSource _musicOut;
        private float _musicBaseVolume;
        private float _musicFade = 1f;
        private float _musicOutFade;
        private string _musicId;

        /// <summary>Oyun dunyasinin sesleri susturuldu mu (mac sonu ekrani).</summary>
        public static bool WorldMuted { get; private set; }

        public static void SetWorldMuted(bool muted)
        {
            WorldMuted = muted;
            if (_instance != null)
                _instance.ApplyWorldMute();
        }

        private void ApplyWorldMute()
        {
            if (WorldMuted)
            {
                // Su an calan atis/adim seslerini de kes.
                foreach (AudioSource voice in _voices)
                {
                    if (voice != null)
                        voice.Stop();
                }
            }

            foreach (var loop in _loops.Values)
            {
                if (loop.source != null)
                    loop.source.mute = WorldMuted;
            }
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            // Menuye donuldu (sahne yeniden yuklendi): oyun sesleri geri gelsin.
            if (WorldMuted)
                SetWorldMuted(false);
        }

        private void Awake()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            _library = SoundLibrary.Instance;

            int count = _library != null ? _library.maxVoices : 16;
            _voices = new AudioSource[count];
            _voiceBaseVolume = new float[count];
            for (int i = 0; i < count; i++)
                _voices[i] = CreateSource($"Ses{i + 1}", loop: false);

            _music = CreateSource("Muzik", loop: true);
            _musicOut = CreateSource("MuzikGecis", loop: true);

            // Sahnede dinleyici yoksa ses hic duyulmaz.
            if (FindFirstObjectByType<AudioListener>() == null)
                gameObject.AddComponent<AudioListener>();

            GameSettings.Changed += ApplyVolumes;
        }

        private void OnDestroy()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            GameSettings.Changed -= ApplyVolumes;
            if (_instance == this)
                _instance = null;
        }

        private void OnApplicationQuit() => _quitting = true;

        private AudioSource CreateSource(string name, bool loop)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
        }

        // ------------------------------------------------------------------
        // Efektler
        // ------------------------------------------------------------------

        public void Play(string id, Vector3? position, float volumeScale, bool ui = false)
        {
            SoundEntry entry = Lookup(id);
            if (entry == null || entry.clips == null || entry.clips.Length == 0)
                return;

            // Mac sonu ekraninda sadece arayuz sesleri.
            if (WorldMuted && !ui && entry.channel != SoundChannel.UI)
                return;

            float now = Time.unscaledTime;
            if (_lastPlayed.TryGetValue(id, out float last) && now - last < entry.minInterval)
                return;

            float volume = entry.volume * volumeScale * ChannelVolume(entry.channel);
            float pan = 0f;

            if (entry.spatial && position.HasValue)
            {
                if (!Attenuate(entry, position.Value, out float distanceGain, out pan))
                    return;
                volume *= distanceGain;
            }

            if (volume <= 0.005f)
                return;

            AudioClip clip = entry.clips[Random.Range(0, entry.clips.Length)];
            if (clip == null)
                return;

            int voice = PickVoice(volume);
            if (voice < 0)
                return;

            _lastPlayed[id] = now;
            AudioSource source = _voices[voice];
            source.clip = clip;
            source.volume = volume;
            source.pitch = 1f + Random.Range(-entry.pitchJitter, entry.pitchJitter);
            source.panStereo = pan;
            _voiceBaseVolume[voice] = volume;
            source.Play();
        }

        /// <summary>
        /// Bos ses kanali; yoksa en kisik calani (o da yenisinden yuksekse
        /// hic) feda eder. Boylece 15 botun atisinda yakindakiler duyulur.
        /// </summary>
        private int PickVoice(float volume)
        {
            int quietest = -1;
            float quietestVolume = float.MaxValue;

            for (int i = 0; i < _voices.Length; i++)
            {
                if (!_voices[i].isPlaying)
                    return i;

                if (_voiceBaseVolume[i] < quietestVolume)
                {
                    quietestVolume = _voiceBaseVolume[i];
                    quietest = i;
                }
            }

            return quietestVolume <= volume ? quietest : -1;
        }

        /// <summary>Oyuncuya uzakliga gore ses seviyesi ve sag/sol dagilimi.</summary>
        private static bool Attenuate(SoundEntry entry, Vector3 position, out float gain, out float pan)
        {
            gain = 1f;
            pan = 0f;

            if (!TryGetListenerPoint(out Vector3 listener))
                return true;

            Vector3 offset = position - listener;
            offset.y = 0f;
            float distance = offset.magnitude;

            if (distance >= entry.maxDistance)
                return false;

            float range = Mathf.Max(0.01f, entry.maxDistance - entry.fullVolumeDistance);
            float t = Mathf.Clamp01((distance - entry.fullVolumeDistance) / range);
            gain = (1f - t) * (1f - t);
            pan = Mathf.Clamp(offset.x / PanDistance, -0.75f, 0.75f);
            return gain > 0.01f;
        }

        private static bool TryGetListenerPoint(out Vector3 point)
        {
            CameraFollow follow = CameraFollow.Active;
            if (follow != null && follow.target != null)
            {
                point = follow.target.position;
                return true;
            }

            Camera main = Camera.main;
            point = main != null ? main.transform.position : Vector3.zero;
            return main != null;
        }

        // ------------------------------------------------------------------
        // Donen sesler (ruzgar)
        // ------------------------------------------------------------------

        public int StartLoop(string id)
        {
            SoundEntry entry = Lookup(id);
            if (entry == null || entry.clips == null || entry.clips.Length == 0 || entry.clips[0] == null)
                return 0;

            AudioSource source = CreateSource("Dongu_" + id, loop: true);
            source.clip = entry.clips[0];
            source.volume = entry.volume * ChannelVolume(entry.channel);
            source.mute = WorldMuted;
            source.Play();

            int handle = _nextLoopHandle++;
            _loops[handle] = (source, entry.volume);
            return handle;
        }

        public void SetLoopVolume(int handle, float volumeScale)
        {
            if (!_loops.TryGetValue(handle, out var loop) || loop.source == null)
                return;

            loop.source.volume = loop.baseVolume * Mathf.Clamp01(volumeScale) * ChannelVolume(SoundChannel.Effects);
        }

        public void StopLoop(int handle)
        {
            if (!_loops.TryGetValue(handle, out var loop))
                return;

            _loops.Remove(handle);
            if (loop.source != null)
                Destroy(loop.source.gameObject);
        }

        // ------------------------------------------------------------------
        // Muzik
        // ------------------------------------------------------------------

        public void PlayMusic(string id)
        {
            if (_musicId == id && _music.isPlaying)
                return;

            SoundEntry entry = Lookup(id);
            if (entry == null || entry.clips == null || entry.clips.Length == 0 || entry.clips[0] == null)
                return;

            // Calani yavasca kis, yenisini yavasca ac.
            (_music, _musicOut) = (_musicOut, _music);
            _musicOutFade = _musicOut.isPlaying ? 1f : 0f;

            _musicId = id;
            _musicBaseVolume = entry.volume;
            _music.clip = entry.clips[0];
            _music.time = 0f;
            _musicFade = 0f;
            _music.volume = 0f;
            _music.Play();
        }

        public void StopMusic()
        {
            if (_musicId == null)
                return;

            _musicId = null;
            (_music, _musicOut) = (_musicOut, _music);
            _musicOutFade = _musicOut.isPlaying ? 1f : 0f;
            _music.Stop();
        }

        private void Update()
        {
            float step = Time.unscaledDeltaTime / MusicFadeSeconds;

            if (_musicId != null)
                _musicFade = Mathf.MoveTowards(_musicFade, 1f, step);

            if (_musicOut.isPlaying)
            {
                _musicOutFade = Mathf.MoveTowards(_musicOutFade, 0f, step);
                if (_musicOutFade <= 0f)
                    _musicOut.Stop();
            }

            float musicVolume = ChannelVolume(SoundChannel.Music);
            _music.volume = _musicBaseVolume * _musicFade * musicVolume;
            _musicOut.volume = _musicBaseVolume * _musicOutFade * musicVolume;
        }

        private void ApplyVolumes()
        {
            float effects = ChannelVolume(SoundChannel.Effects);
            foreach (var loop in _loops.Values)
            {
                if (loop.source != null)
                    loop.source.volume = loop.baseVolume * effects;
            }

            if (effects <= 0f)
            {
                foreach (AudioSource voice in _voices)
                    voice.Stop();
            }
        }

        private float ChannelVolume(SoundChannel channel)
        {
            if (channel == SoundChannel.Music)
                return GameSettings.MusicOn ? (_library != null ? _library.musicVolume : 0.55f) : 0f;

            return GameSettings.EffectsOn ? (_library != null ? _library.effectsVolume : 1f) : 0f;
        }

        private SoundEntry Lookup(string id)
        {
            SoundEntry entry = _library != null ? _library.Find(id) : null;
            if (entry == null && _warned.Add(id))
                Debug.LogWarning($"Ses bulunamadi: '{id}'. SesKutuphanesi'ne ekle (Menü > Kur eksikleri ekler).");
            return entry;
        }
    }
}
