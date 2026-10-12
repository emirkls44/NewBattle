using System;
using System.Collections.Generic;
using UnityEngine;

namespace NewBattle.Audio
{
    public enum SoundChannel
    {
        [Tooltip("Oyun sesleri (atis, adim, inis...). Ayarlar > Efektler.")]
        Effects,
        [Tooltip("Arayuz sesleri (buton, odul...). Ayarlar > Efektler.")]
        UI,
        [Tooltip("Muzik. Ayarlar > Muzik.")]
        Music
    }

    [Serializable]
    public class SoundEntry
    {
        [Tooltip("Kodda kullanilan ad (ornek: ates_tufek). Degistirme.")]
        public string id = "yeni_ses";
        [Tooltip("Birden fazla klip varsa her seferinde biri rastgele calar.")]
        public AudioClip[] clips;
        public SoundChannel channel = SoundChannel.Effects;
        [Range(0f, 1f)] public float volume = 0.8f;
        [Tooltip("Her calista perde bu kadar oynar (0.05 = %5). Ayni ses tekrar tekrar ayni duyulmasin.")]
        [Range(0f, 0.3f)] public float pitchJitter = 0.05f;

        [Header("Mesafe (dunyadaki sesler)")]
        [Tooltip("Isaretliyse oyuncuya uzakliga gore kisilir ve sag/sol kulaga dagilir.")]
        public bool spatial;
        [Tooltip("Bu mesafeye kadar tam ses.")]
        [Min(0f)] public float fullVolumeDistance = 4f;
        [Tooltip("Bu mesafeden sonra hic duyulmaz.")]
        [Min(0.1f)] public float maxDistance = 30f;

        [Tooltip("Ayni ses bu kadar saniye icinde tekrar istenirse calmaz (ust uste binmesin).")]
        [Min(0f)] public float minInterval = 0.03f;
    }

    /// <summary>
    /// Oyunun tum sesleri tek dosyada: Assets/Resources/SesKutuphanesi.asset
    /// (Menü > Kur olusturur). Bir sesi degistirmek icin o satirin klibini
    /// degistirmen yeterli; ses seviyesi, mesafe ve perde de buradan.
    /// </summary>
    [CreateAssetMenu(menuName = "NewBattle/Ses Kutuphanesi", fileName = "SesKutuphanesi")]
    public class SoundLibrary : ScriptableObject
    {
        public const string ResourceName = "SesKutuphanesi";

        [Tooltip("Ayni anda en fazla kac efekt calsin. Fazlasi en eski/en kisik olanin yerine gecer.")]
        [Range(4, 32)] public int maxVoices = 16;
        [Tooltip("Genel efekt ses seviyesi.")]
        [Range(0f, 1f)] public float effectsVolume = 1f;
        [Tooltip("Genel muzik ses seviyesi.")]
        [Range(0f, 1f)] public float musicVolume = 0.55f;

        public List<SoundEntry> sounds = new();

        private Dictionary<string, SoundEntry> _lookup;
        private static SoundLibrary _instance;

        public static SoundLibrary Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Resources.Load<SoundLibrary>(ResourceName);
                return _instance;
            }
        }

        public SoundEntry Find(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            if (_lookup == null || _lookup.Count != sounds.Count)
            {
                _lookup = new Dictionary<string, SoundEntry>();
                foreach (SoundEntry entry in sounds)
                {
                    if (entry != null && !string.IsNullOrEmpty(entry.id))
                        _lookup[entry.id] = entry;
                }
            }

            return _lookup.TryGetValue(id, out SoundEntry found) ? found : null;
        }

        private void OnValidate() => _lookup = null;
    }
}
