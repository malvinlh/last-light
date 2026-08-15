using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace LastLight.Editor.Setup
{
    /// <summary>
    /// Imports TextMeshPro's essential resources if they are missing.
    /// </summary>
    /// <remarks>
    /// TMP ships its default font asset and shaders as a .unitypackage that Unity normally
    /// offers to import through a modal dialog on first use. A dialog is not an option in a
    /// headless workflow, and a project missing these resources builds fine but renders every
    /// label as nothing - a failure that only shows up in the finished executable.
    ///
    /// So it is done explicitly and verified by checking that TMP Settings actually landed on
    /// disk, rather than trusting the import call to have worked.
    /// </remarks>
    public static class TextMeshProSetup
    {
        private const string SettingsAssetPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        public static bool EssentialsPresent => File.Exists(SettingsAssetPath);

        [MenuItem("Last Light/Ensure TextMeshPro Essentials", priority = 20)]
        public static void EnsureEssentials()
        {
            if (EssentialsPresent)
            {
                Debug.Log("[LastLight] TMP essential resources already present.");
                return;
            }

            TMP_PackageResourceImporter.ImportResources(true, false, false);
            AssetDatabase.Refresh();

            Debug.Log(EssentialsPresent
                ? "[LastLight] TMP essential resources imported."
                : "[LastLight] TMP essential resources did NOT import - text would render blank.");
        }

        /// <summary>
        /// Entry point for `-executeMethod`; fails the run if the resources are still missing.
        /// </summary>
        /// <remarks>
        /// Importing a .unitypackage is asynchronous - the call returns long before the assets
        /// land - so this waits on the completion callback instead of checking immediately, with
        /// a wall-clock timeout so a callback that never arrives fails the run rather than
        /// hanging the build agent forever.
        /// </remarks>
        public static void EnsureEssentialsFromCLI()
        {
            if (EssentialsPresent)
            {
                Debug.Log("[LastLight] TMP essential resources already present.");
                EditorApplication.Exit(0);
                return;
            }

            double deadline = EditorApplication.timeSinceStartup + 180.0;

            void Finish()
            {
                AssetDatabase.Refresh();
                bool ok = EssentialsPresent;
                Debug.Log(ok
                    ? "[LastLight] TMP essential resources imported."
                    : "[LastLight] TMP essential resources did NOT import - text would render blank.");
                EditorApplication.Exit(ok ? 0 : 1);
            }

            AssetDatabase.importPackageCompleted += _ => Finish();
            AssetDatabase.importPackageFailed += (_, error) =>
            {
                Debug.LogError($"[LastLight] TMP package import failed: {error}");
                EditorApplication.Exit(1);
            };
            AssetDatabase.importPackageCancelled += _ =>
            {
                Debug.LogError("[LastLight] TMP package import was cancelled.");
                EditorApplication.Exit(1);
            };

            EditorApplication.update += () =>
            {
                // The importer can also finish without ever raising its callback in batch mode.
                if (EssentialsPresent) Finish();
                else if (EditorApplication.timeSinceStartup > deadline)
                {
                    Debug.LogError("[LastLight] Timed out waiting for the TMP package import.");
                    EditorApplication.Exit(1);
                }
            };

            TMP_PackageResourceImporter.ImportResources(true, false, false);
        }
    }
}
