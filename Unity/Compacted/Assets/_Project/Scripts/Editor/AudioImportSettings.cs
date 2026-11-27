using UnityEditor;
using UnityEngine;

namespace Compacted.Editor
{
    /// <summary>
    /// Import settings for files under Assets/_Project/Audio:
    /// Ambience and Music stream from disk (long tracks); SFX and UI load into memory for instant playback.
    /// </summary>
    public sealed class AudioImportSettings : AssetPostprocessor
    {
        const string Root = "Assets/_Project/Audio/";

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Root)) return;
            var importer = (AudioImporter)assetImporter;
            var s = importer.defaultSampleSettings;
            bool longTrack = assetPath.StartsWith(Root + "Ambience/") || assetPath.StartsWith(Root + "Music/");
            if (longTrack)
            {
                s.loadType = AudioClipLoadType.Streaming;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.7f;
            }
            else
            {
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.ADPCM;
            }
            importer.defaultSampleSettings = s;
            importer.loadInBackground = longTrack;
        }

        [MenuItem("Compacted/Setup/Reimport Audio")]
        public static void ReimportAll()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/_Project/Audio" }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            Debug.Log("Audio reimported with Compacted import settings.");
        }
    }
}
