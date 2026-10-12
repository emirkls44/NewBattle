#if UNITY_EDITOR
using System.Collections.Generic;
using NewBattle.Audio;
using NewBattle.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// Ses: Assets/Resources/SesKutuphanesi dosyasini olusturur ve
    /// Assets/GameArt/Audio'daki seslerle doldurur. Sadece EKSIK sesleri
    /// ekler; senin degistirdigin klip / seviye / mesafe ayarlarina dokunmaz.
    /// </summary>
    public static partial class MainMenuBuilder
    {
        private const string SoundLibraryPath = "Assets/Resources/SesKutuphanesi.asset";
        private const string AudioRoot = "Assets/GameArt/Audio";

        private readonly struct SoundDefault
        {
            public readonly string Id;
            public readonly SoundChannel Channel;
            public readonly float Volume;
            public readonly bool Spatial;
            public readonly float Full;
            public readonly float Max;
            public readonly float Jitter;
            public readonly float Interval;

            public SoundDefault(string id, SoundChannel channel, float volume, bool spatial = false, float full = 4f,
                float max = 30f, float jitter = 0.04f, float interval = 0.03f)
            {
                Id = id;
                Channel = channel;
                Volume = volume;
                Spatial = spatial;
                Full = full;
                Max = max;
                Jitter = jitter;
                Interval = interval;
            }
        }

        private static readonly SoundDefault[] SoundDefaults =
        {
            // Arayuz
            new("buton", SoundChannel.UI, 0.55f, jitter: 0.06f),
            new("kart", SoundChannel.UI, 0.5f, jitter: 0.08f, interval: 0.05f),
            new("para", SoundChannel.UI, 0.7f),
            new("satin_al", SoundChannel.UI, 0.8f, jitter: 0f),
            new("hata", SoundChannel.UI, 0.45f, jitter: 0f, interval: 0.2f),
            new("xp", SoundChannel.UI, 0.65f),
            new("odul", SoundChannel.UI, 0.8f, jitter: 0f),
            new("seviye_atla", SoundChannel.UI, 0.85f, jitter: 0f),

            // Lobi ve inis
            new("geri_sayim", SoundChannel.UI, 0.4f, jitter: 0f),
            new("basla", SoundChannel.UI, 0.7f, jitter: 0f),
            new("ruzgar", SoundChannel.Effects, 0.45f, jitter: 0f),
            new("parasut", SoundChannel.Effects, 0.7f, spatial: true, full: 4f, max: 30f),
            new("inis", SoundChannel.Effects, 0.8f, spatial: true, full: 4f, max: 25f),

            // Savas
            new("ates_tufek", SoundChannel.Effects, 0.7f, spatial: true, full: 5f, max: 40f, jitter: 0.06f, interval: 0.02f),
            new("ates_agir", SoundChannel.Effects, 0.8f, spatial: true, full: 6f, max: 45f, jitter: 0.05f, interval: 0.02f),
            new("ates_tabanca", SoundChannel.Effects, 0.65f, spatial: true, full: 5f, max: 35f, jitter: 0.06f, interval: 0.02f),
            new("ates_pompali", SoundChannel.Effects, 0.85f, spatial: true, full: 5f, max: 40f, jitter: 0.05f, interval: 0.02f),
            new("yumruk", SoundChannel.Effects, 0.7f, spatial: true, full: 3f, max: 18f, jitter: 0.08f),
            new("isabet", SoundChannel.UI, 0.5f, jitter: 0.08f, interval: 0.04f),
            new("oldurme", SoundChannel.UI, 0.8f, jitter: 0f),
            new("hasar_al", SoundChannel.UI, 0.6f, jitter: 0.08f, interval: 0.08f),
            new("olum", SoundChannel.UI, 0.8f, jitter: 0f),
            new("toplama", SoundChannel.UI, 0.65f),
            new("adim", SoundChannel.Effects, 0.25f, spatial: true, full: 2f, max: 12f, jitter: 0.15f, interval: 0.05f),

            // Alan ve sonuc
            new("alan_daraliyor", SoundChannel.UI, 0.45f, jitter: 0f, interval: 1f),
            new("alan_kalp", SoundChannel.UI, 0.6f, jitter: 0f, interval: 0.2f),
            new("zafer", SoundChannel.UI, 0.8f, jitter: 0f),
            new("elenme", SoundChannel.UI, 0.75f, jitter: 0f),

            // Muzik
            new("menu_muzigi", SoundChannel.Music, 0.8f, jitter: 0f, interval: 0f),
        };

        /// <summary>Ses kutuphanesini olusturur / eksik sesleri ekler. Kac ses eklendigini dondurur.</summary>
        public static int EnsureSoundLibrary(out SoundLibrary library)
        {
            if (AssetDatabase.IsValidFolder(AudioRoot))
                AssetDatabase.ImportAsset(AudioRoot, ImportAssetOptions.ImportRecursive);

            library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(SoundLibraryPath);
            if (library == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                    AssetDatabase.CreateFolder("Assets", "Resources");

                library = ScriptableObject.CreateInstance<SoundLibrary>();
                AssetDatabase.CreateAsset(library, SoundLibraryPath);
            }

            var existing = new HashSet<string>();
            foreach (SoundEntry entry in library.sounds)
            {
                if (entry != null)
                    existing.Add(entry.id);
            }

            int added = 0;
            foreach (SoundDefault preset in SoundDefaults)
            {
                if (existing.Contains(preset.Id))
                    continue;

                AudioClip clip = FindSoundClip(preset.Id);
                if (clip == null)
                    continue;

                library.sounds.Add(new SoundEntry
                {
                    id = preset.Id,
                    clips = new[] { clip },
                    channel = preset.Channel,
                    volume = preset.Volume,
                    pitchJitter = preset.Jitter,
                    spatial = preset.Spatial,
                    fullVolumeDistance = preset.Full,
                    maxDistance = preset.Max,
                    minInterval = preset.Interval
                });
                added++;
            }

            if (added > 0)
            {
                EditorUtility.SetDirty(library);
                AssetDatabase.SaveAssets();
            }

            return added;
        }

        private static AudioClip FindSoundClip(string id)
        {
            foreach (string folder in new[] { "Sfx", "Music" })
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioRoot}/{folder}/{id}.wav");
                if (clip != null)
                    return clip;
            }

            return null;
        }

        // ------------------------------------------------------------------
        // Ayarlar: MUZIK satiri
        // ------------------------------------------------------------------

        /// <summary>3: en uste MUZIK satiri; eski "SES" satiri "EFEKTLER" oldu.</summary>
        private const int SettingsMusicLayoutVersion = 3;

        private static bool UpgradeSettingsMusic(SettingsScreen screen)
        {
            var serialized = new SerializedObject(screen);
            SerializedProperty version = serialized.FindProperty("layoutVersion");
            if (version.intValue >= SettingsMusicLayoutVersion || serialized.FindProperty("musicButton").objectReferenceValue != null)
                return false;

            if (!(screen.transform.Find("Pencere") is RectTransform window))
                return false;

            // Diger satirlar (kurulumdaki yerlerindeyse) bir satir asagi kayar.
            const float rowStep = 130f;
            foreach (string name in new[] { "SesYazisi", "SesButonu", "KareHiziYazisi", "KareHiziButonu", "IsimYazisi", "IsimButonu" })
            {
                if (window.Find(name) is RectTransform row &&
                    (Mathf.Approximately(row.anchoredPosition.y, -200f) || Mathf.Approximately(row.anchoredPosition.y, -330f) ||
                     Mathf.Approximately(row.anchoredPosition.y, -460f)))
                {
                    Undo.RecordObject(row, "Ayarlar muzik");
                    row.anchoredPosition = new Vector2(row.anchoredPosition.x, row.anchoredPosition.y - rowStep);
                }
            }

            if (window.Find("SesYazisi")?.GetComponent<TextMeshProUGUI>() is { } effectsLabel && effectsLabel.text == "SES")
            {
                Undo.RecordObject(effectsLabel, "Ayarlar muzik");
                effectsLabel.text = "EFEKTLER";
            }

            if (Mathf.Approximately(window.sizeDelta.y, 730f))
            {
                Undo.RecordObject(window, "Ayarlar muzik");
                window.sizeDelta = new Vector2(window.sizeDelta.x, 860f);
            }

            SettingsRowLabel(window, "MuzikYazisi", "MÜZİK", -200f);
            Button music = SettingsButton(window, "MuzikButonu", "AÇIK", -200f, Green, GreenLip, out TextMeshProUGUI musicLabel);
            Undo.RegisterCreatedObjectUndo(window.Find("MuzikYazisi").gameObject, "Ayarlar muzik");
            Undo.RegisterCreatedObjectUndo(music.gameObject, "Ayarlar muzik");

            serialized.FindProperty("musicButton").objectReferenceValue = music;
            serialized.FindProperty("musicFace").objectReferenceValue = Face(music).GetComponent<Image>();
            serialized.FindProperty("musicLabel").objectReferenceValue = musicLabel;
            version.intValue = SettingsMusicLayoutVersion;
            serialized.ApplyModifiedProperties();
            return true;
        }
    }
}
#endif
