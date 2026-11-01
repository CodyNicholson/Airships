using UnityEditor;
using UnityEngine;

namespace Airships.Editor
{
    // Songs are stored as WAV from the music project, and ship compressed.
    public class MusicImport : AssetPostprocessor
    {
        const float Quality = 0.5f;

        void OnPreprocessAudio()
        {
            if (!assetPath.Replace('\\', '/').Contains("Resources/Music/")) return;
            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = Quality;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
        }
    }
}
