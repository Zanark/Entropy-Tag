using System;
using System.Collections.Generic;
using System.IO;
using EntropyTag.Presentation;
using EntropyTag.UnityAdapters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace EntropyTag.Editor
{
    public static class ProjectFoundationSetup
    {
        public const string BootstrapScenePath = "Assets/EntropyTag/Scenes/Bootstrap.unity";
        public const string FoundationTestScenePath = "Assets/EntropyTag/Scenes/Tests/Test_Foundation.unity";
        public const string InputActionsPath = "Assets/EntropyTag/Settings/Input/EntropyTagInputActions.inputactions";

        private static readonly string[] RequiredFolders =
        {
            "Assets/EntropyTag/Art/Characters",
            "Assets/EntropyTag/Art/Environments",
            "Assets/EntropyTag/Art/Props",
            "Assets/EntropyTag/Art/Textures",
            "Assets/EntropyTag/Audio/Music",
            "Assets/EntropyTag/Audio/SFX",
            "Assets/EntropyTag/Audio/Voice",
            "Assets/EntropyTag/Materials",
            "Assets/EntropyTag/Prefabs/Characters",
            "Assets/EntropyTag/Prefabs/Gameplay",
            "Assets/EntropyTag/Prefabs/UI",
            "Assets/EntropyTag/Prefabs/VFX",
            "Assets/EntropyTag/Scenes/FrontEnd",
            "Assets/EntropyTag/Scenes/Gameplay",
            "Assets/EntropyTag/Scenes/Tests",
            "Assets/EntropyTag/Scripts/Application",
            "Assets/EntropyTag/Scripts/Domain",
            "Assets/EntropyTag/Scripts/Editor",
            "Assets/EntropyTag/Scripts/Infrastructure",
            "Assets/EntropyTag/Scripts/Presentation",
            "Assets/EntropyTag/Scripts/Unity",
            "Assets/EntropyTag/Settings/Elements",
            "Assets/EntropyTag/Settings/Input",
            "Assets/EntropyTag/Settings/Maps",
            "Assets/EntropyTag/Settings/Tuning",
            "Assets/EntropyTag/Shaders/Includes",
            "Assets/EntropyTag/Shaders/Territory",
            "Assets/EntropyTag/Shaders/VFX",
            "Assets/EntropyTag/Tests/EditMode",
            "Assets/EntropyTag/Tests/PlayMode",
            "Assets/EntropyTag/UI",
            "Assets/EntropyTag/VFX",
            "Assets/Plugins",
            "Assets/ThirdParty"
        };

        [MenuItem("EntropyTag/Setup/Apply Project Foundation")]
        public static void Apply()
        {
            foreach (string folder in RequiredFolders)
            {
                EnsureFolder(folder);
            }

            AssetDatabase.Refresh();
            ValidateInputActions();
            CreateBootstrapScene();
            CreateFoundationTestScene();
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("EntropyTag project foundation applied successfully.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);

            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException($"Cannot create Unity folder '{path}'.");
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static void ValidateInputActions()
        {
            InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (inputActions == null)
            {
                throw new InvalidOperationException($"Required Input Action Asset was not imported: {InputActionsPath}");
            }
        }

        private static void CreateBootstrapScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapScenePath) != null)
            {
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject bootstrap = new GameObject("EntropyTag Bootstrap");
            bootstrap.AddComponent<ProjectBootstrap>();

            GameObject presentation = new GameObject("Presentation Root");
            presentation.AddComponent<PresentationRoot>();

            EditorSceneManager.SaveScene(scene, BootstrapScenePath);
        }

        private static void CreateFoundationTestScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(FoundationTestScenePath) != null)
            {
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Foundation Test Scene");
            EditorSceneManager.SaveScene(scene, FoundationTestScenePath);
        }

        private static void ConfigureBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(BootstrapScenePath, true)
            };

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PlayerSandboxSetup.SelectedSandboxScenePath) != null)
            {
                scenes.Add(new EditorBuildSettingsScene(PlayerSandboxSetup.SelectedSandboxScenePath, true));
            }

            foreach (string scenePath in PlayerSandboxSetup.CameraVariantScenePaths)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null)
                {
                    scenes.Add(new EditorBuildSettingsScene(scenePath, true));
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
