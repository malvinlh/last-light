using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LastLight.Editor.Build
{
    /// <summary>
    /// Produces the Windows build the submission ships.
    /// </summary>
    /// <remarks>
    /// Scripted rather than driven through the Build Settings dialog so the output is identical
    /// every time and can be produced from the command line. It takes its scene list from Build
    /// Settings rather than a hard-coded array, so the build cannot silently disagree with what
    /// the validator just checked.
    /// </remarks>
    public static class BuildScript
    {
        public const string OutputFolder = "Build/LastLight";
        public const string ExecutableName = "LastLight.exe";

        [MenuItem("Last Light/Build Windows Player", priority = 40)]
        public static void BuildWindows()
        {
            string path = Path.Combine(OutputFolder, ExecutableName);
            Directory.CreateDirectory(OutputFolder);

            var scenes = new List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled) scenes.Add(scene.path);
            }

            if (scenes.Count == 0)
            {
                Debug.LogError("[LastLight] No enabled scenes in Build Settings; refusing to build.");
                return;
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                // Release: no development flags, so the debug shortcuts compiled behind
                // DEVELOPMENT_BUILD are absent from what the reviewer runs.
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[LastLight] Build {summary.result}: {summary.totalErrors} error(s).");
                return;
            }

            Debug.Log($"[LastLight] Build succeeded: {path} " +
                      $"({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:F0}s, " +
                      $"{scenes.Count} scenes)");
        }

        /// <summary>Entry point for `-executeMethod`. Non-zero exit if the executable is missing.</summary>
        public static void BuildWindowsFromCLI()
        {
            try
            {
                BuildWindows();
                bool produced = File.Exists(Path.Combine(OutputFolder, ExecutableName));
                EditorApplication.Exit(produced ? 0 : 1);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[LastLight] Build failed: {exception}");
                EditorApplication.Exit(1);
            }
        }
    }
}
