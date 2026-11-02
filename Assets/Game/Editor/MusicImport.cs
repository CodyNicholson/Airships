using UnityEditor;
using UnityEngine;

namespace Airships.Editor
{
    // Songs and sound effects are stored as WAV from the music project, and ship compressed.
    // Effects decompress when loaded, so they can start with no delay in the middle of a fight.
    public class MusicImport : AssetPostprocessor
    {
        const float Quality = 0.5f;
        const float SfxQuality = 0.7f;

        void OnPreprocessAudio()
        {
            var path = assetPath.Replace('\\', '/');
            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;
            if (path.Contains("Resources/Sfx/"))
            {
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = SfxQuality;
                settings.preloadAudioData = true;
                importer.defaultSampleSettings = settings;
                return;
            }
            if (!path.Contains("Resources/Music/")) return;
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = Quality;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
        }
    }
}
