#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// Assets/GameArt/Audio altina konan seslerin telefon icin ice aktarma
    /// ayarlari: efektler mono + ADPCM (kucuk, aninda calar), muzik
    /// Vorbis + akis (bellegi sisirmez). Yeni bir .wav koyunca otomatik uygulanir.
    /// </summary>
    public class AudioImportRules : AssetPostprocessor
    {
        private const string AudioFolder = "Assets/GameArt/Audio/";

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(AudioFolder))
                return;

            var importer = (AudioImporter)assetImporter;
            bool music = assetPath.StartsWith(AudioFolder + "Music/");

            importer.forceToMono = true;
            importer.loadInBackground = music;

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            if (music)
            {
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.55f;
            }
            else
            {
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.ADPCM;
            }

            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
        }
    }
}
#endif
