using System;
using System.IO;
using System.Linq;
using EntropyTag.UnityAdapters;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace EntropyTag.Editor
{
    public static class EntropyTagBuildCommand
    {
        public static void BuildWindowsDevelopment()
        {
            ProjectFoundationSetup.Apply();

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && !PlayerSandboxSetup.IsSandboxScene(scene.path))
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                throw new InvalidOperationException("No enabled scenes are configured for the build.");
            }

            if (!scenes.Contains(ProjectBootstrap.FirstSliceScenePath))
            {
                throw new InvalidOperationException(
                    "The match arena is missing. Run EntropyTag/Setup/Create Match Flow Scenes before building.");
            }

            string projectRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            string outputPath = Path.Combine(projectRoot, "Builds", "Windows", "EntropyTag.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"EntropyTag development build failed with result {report.summary.result}.");
            }

            Debug.Log($"EntropyTag development build created at {outputPath}.");
        }
    }
}
