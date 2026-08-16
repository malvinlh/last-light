using UnityEditor;
using UnityEngine;

namespace LastLight.Editor.Setup
{
    /// <summary>
    /// Applies import settings to the generated music.
    /// </summary>
    /// <remarks>
    /// The WAVs are committed because they are the generator's output and a reviewer should be able
    /// to regenerate and diff them, but shipping uncompressed 60-second loops in the player would
    /// waste about 8 MB for no benefit. Vorbis at a middling quality is inaudible on ambient pads.
    ///
    /// Streaming rather than decompress-on-load: these are long, quiet, and started once per scene,
    /// so there is nothing to gain from holding them in memory.
    /// </remarks>
    public static class MusicImportSettings
    {
        public const string AudioFolder = "Assets/_Project/Audio";

        [MenuItem("Last Light/Import Audio Settings", priority = 22)]
        public static void ApplyAll()
        {
            int touched = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null) continue;

                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.45f;

                // Preloading moved onto the per-platform sample settings in Unity 6; the importer
                // level property still exists but is obsolete and fails the build as an error.
                settings.preloadAudioData = false;

                importer.defaultSampleSettings = settings;
                importer.forceToMono = true;
                importer.loadInBackground = true;

                importer.SaveAndReimport();
                touched++;
            }

            Debug.Log($"[LastLight] Audio import settings applied to {touched} clip(s).");
        }

        public static void ApplyAllFromCLI()
        {
            ApplyAll();
            EditorApplication.Exit(0);
        }
    }
}
