#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using NewBattle.Audio;
using UnityEditor;
using UnityEngine;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// Tools > NewBattle > Sesler: oyundaki her sesi ve muzigi tek pencerede
    /// dinle, degistir, seviyesini ayarla. Yeni bir klibi (wav / mp3 / ogg)
    /// Project'ten ilgili satira surukle-birak yeterli.
    /// </summary>
    public class SoundLibraryWindow : EditorWindow
    {
        private static readonly Dictionary<string, string> Descriptions = new()
        {
            { "menu_muzigi", "Ana menü müziği (döngü)" },
            { "buton", "Herhangi bir butona basınca" },
            { "kart", "Kart / pencere zıplarken" },
            { "para", "Para kazanınca" },
            { "satin_al", "Mağazada satın alınca" },
            { "hata", "Para yetmeyince / kilitliye basınca" },
            { "xp", "XP kazanınca, görev bitince" },
            { "odul", "Günlük ödül / hediye penceresi" },
            { "seviye_atla", "Seviye atlayınca" },
            { "geri_sayim", "Geri sayım bip (5-4-3-2-1)" },
            { "basla", "Maç başlarken korna" },
            { "ruzgar", "Serbest düşüşte rüzgâr (döngü)" },
            { "parasut", "Paraşüt açılınca" },
            { "inis", "Yere inince" },
            { "ates_tufek", "Tüfek atışı" },
            { "ates_agir", "Airdrop / ağır silah atışı" },
            { "ates_tabanca", "Tabanca atışı" },
            { "ates_pompali", "Pompalı atışı" },
            { "yumruk", "Yumruk" },
            { "isabet", "Senin mermin birine değince" },
            { "oldurme", "Birini öldürünce" },
            { "hasar_al", "Sen vurulunca" },
            { "olum", "Sen ölünce" },
            { "toplama", "Eşya toplayınca" },
            { "adim", "Ayak sesi" },
            { "alan_daraliyor", "Güvenli alan daralmaya başlayınca" },
            { "alan_kalp", "Alan dışındayken kalp atışı" },
            { "zafer", "Maç sonu: zafer" },
            { "elenme", "Maç sonu: elenme" },
        };

        private static readonly (SoundChannel channel, string title)[] Sections =
        {
            (SoundChannel.Music, "Müzik"),
            (SoundChannel.UI, "Arayüz ve Bildirim Sesleri"),
            (SoundChannel.Effects, "Oyun Sesleri"),
        };

        private SoundLibrary _library;
        private Vector2 _scroll;

        [MenuItem("Tools/NewBattle/Sesler", false, 35)]
        public static void Open()
        {
            GetWindow<SoundLibraryWindow>("Sesler").minSize = new Vector2(560f, 500f);
        }

        private void OnEnable()
        {
            _library = AssetDatabase.LoadAssetAtPath<SoundLibrary>("Assets/Resources/SesKutuphanesi.asset");
        }

        private void OnDisable() => StopPreview();

        private void OnGUI()
        {
            if (_library == null)
            {
                EditorGUILayout.HelpBox("Ses kütüphanesi yok. 'Tools > NewBattle > Menü > Kur' çalıştır.", MessageType.Warning);
                if (GUILayout.Button("Şimdi Oluştur"))
                {
                    MainMenuBuilder.EnsureSoundLibrary(out _library);
                    Repaint();
                }
                return;
            }

            EditorGUILayout.HelpBox(
                "Bir sesi değiştirmek için Project'teki ses dosyasını (wav / mp3 / ogg) ilgili satırdaki kutuya sürükle.\n" +
                "▶ ile dinle. Birden fazla klip vermek (her seferinde rastgele biri çalsın) için 'Gelişmiş' ile dosyayı aç.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Gelişmiş (kütüphane dosyası)"))
                {
                    Selection.activeObject = _library;
                    EditorGUIUtility.PingObject(_library);
                }

                if (GUILayout.Button("■ Durdur", GUILayout.Width(90f)))
                    StopPreview();
            }

            EditorGUI.BeginChangeCheck();
            float effects = EditorGUILayout.Slider("Genel efekt seviyesi", _library.effectsVolume, 0f, 1f);
            float music = EditorGUILayout.Slider("Genel müzik seviyesi", _library.musicVolume, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_library, "Ses seviyesi");
                _library.effectsVolume = effects;
                _library.musicVolume = music;
                EditorUtility.SetDirty(_library);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach ((SoundChannel channel, string title) in Sections)
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

                foreach (SoundEntry entry in _library.sounds)
                {
                    if (entry != null && entry.channel == channel)
                        DrawRow(entry);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawRow(SoundEntry entry)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(200f)))
                {
                    EditorGUILayout.LabelField(entry.id, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(Descriptions.TryGetValue(entry.id, out string text) ? text : "",
                        EditorStyles.miniLabel);
                }

                AudioClip current = entry.clips != null && entry.clips.Length > 0 ? entry.clips[0] : null;

                EditorGUI.BeginChangeCheck();
                var clip = (AudioClip)EditorGUILayout.ObjectField(current, typeof(AudioClip), false, GUILayout.MinWidth(140f));
                float volume = GUILayout.HorizontalSlider(entry.volume, 0f, 1f, GUILayout.Width(80f));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_library, "Ses degistir");
                    if (clip != current)
                    {
                        if (entry.clips == null || entry.clips.Length == 0)
                            entry.clips = new AudioClip[1];
                        entry.clips[0] = clip;
                    }

                    entry.volume = volume;
                    EditorUtility.SetDirty(_library);
                }

                GUILayout.Label($"{Mathf.RoundToInt(entry.volume * 100)}%", GUILayout.Width(36f));

                using (new EditorGUI.DisabledScope(current == null))
                {
                    if (GUILayout.Button("▶", GUILayout.Width(30f)))
                        PlayPreview(current);
                }

                if (entry.clips != null && entry.clips.Length > 1)
                    GUILayout.Label($"+{entry.clips.Length - 1}", GUILayout.Width(24f));
            }
        }

        // Unity'nin Inspector'daki ses onizlemesi (ic API, surume gore yansima ile).
        private static void PlayPreview(AudioClip clip)
        {
            StopPreview();
            Type util = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            MethodInfo play = util?.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public, null,
                new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);

            if (play != null)
                play.Invoke(null, new object[] { clip, 0, false });
            else
                Debug.LogWarning("Bu Unity surumunde onizleme bulunamadi; sesi Project'te secip Inspector'dan dinle.");
        }

        private static void StopPreview()
        {
            Type util = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            util?.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public)?.Invoke(null, null);
        }
    }
}
#endif
