using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Escape.EditorTools
{
    /// <summary>
    /// Standalone player build, for CI and local qualification.
    /// Batchmode: -executeMethod Escape.EditorTools.PlayerBuild.Build
    ///
    /// Exits non-zero when the build fails so the CI step fails with it.
    /// Output path may be overridden with ETE_PLAYER_OUTPUT; the default is
    /// under Builds/, which is gitignored.
    /// </summary>
    public static class PlayerBuild
    {
        public const string DefaultOutput = "Builds/StandaloneOSX/EscapeTheElites.app";

        [MenuItem("Tools/Escape the Elites/Build Standalone Player")]
        public static void Build()
        {
            var output = System.Environment.GetEnvironmentVariable("ETE_PLAYER_OUTPUT");
            if (string.IsNullOrEmpty(output)) output = DefaultOutput;
            output = Path.GetFullPath(output);
            Directory.CreateDirectory(Path.GetDirectoryName(output));

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[PlayerBuild] No enabled scenes in build settings.");
                Fail();
                return;
            }

            // Building rewrites ProjectSettings.asset and reorders (or
            // transiently empties) preloadedAssets, which dirties the repo for
            // no reason. Snapshot the authored order and put it back.
            var preloaded = PlayerSettings.GetPreloadedAssets();

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None
            });

            if (PlayerSettings.GetPreloadedAssets().Length != preloaded.Length ||
                !PlayerSettings.GetPreloadedAssets().SequenceEqual(preloaded))
            {
                PlayerSettings.SetPreloadedAssets(preloaded);
                AssetDatabase.SaveAssets();
                Debug.Log("[PlayerBuild] Restored the authored preloadedAssets order.");
            }

            var s = report.summary;
            Debug.Log($"[PlayerBuild] {s.result} — {s.totalSize} bytes, {s.totalTime}, " +
                      $"{scenes.Length} scenes, {s.totalErrors} errors, {s.totalWarnings} warnings");

            if (s.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[PlayerBuild] Build did not succeed: {s.result}");
                Fail();
            }
        }

        /// <summary>
        /// Fails the CI step. Only exits the process in batchmode — quitting
        /// the editor on a menu-item failure would discard the user's work.
        /// </summary>
        private static void Fail()
        {
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
