using System;
using System.Collections.Generic;
using EntropyTag.Presentation;
using EntropyTag.UnityAdapters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EntropyTag.Editor
{
    public static class PlayerSandboxSetup
    {
        public const string CameraVariantFolder = "Assets/EntropyTag/Scenes/Tests/CameraVariants";
        public const string LegacySandboxScenePath = "Assets/EntropyTag/Scenes/Tests/Sandbox_PlayerMotor.unity";

        public static readonly string[] CameraVariantScenePaths =
        {
            $"{CameraVariantFolder}/Sandbox_Camera_01_CenteredImmediate.unity",
            $"{CameraVariantFolder}/Sandbox_Camera_02_CenteredSmooth.unity",
            $"{CameraVariantFolder}/Sandbox_Camera_03_CenteredShoulder.unity",
            $"{CameraVariantFolder}/Sandbox_Camera_04_FreeAimEdgeTurn.unity",
            $"{CameraVariantFolder}/Sandbox_Camera_05_FreeAimContinuousFollow.unity",
            $"{CameraVariantFolder}/Sandbox_Camera_06_ElasticTether.unity",
            $"{CameraVariantFolder}/Sandbox_Camera_07_SoftZoneRecenter.unity",
            $"{CameraVariantFolder}/Sandbox_Camera_08_CenteredCinematicSpring.unity"
        };

        [MenuItem("EntropyTag/Setup/Create Camera Comparison Sandboxes")]
        public static void Create()
        {
            EnsureFolder(CameraVariantFolder);

            Array modes = Enum.GetValues(typeof(CameraAimExperimentMode));
            if (modes.Length != CameraVariantScenePaths.Length)
            {
                throw new InvalidOperationException(
                    "Camera experiment modes and scene paths must have matching counts.");
            }

            for (int index = 0; index < modes.Length; index++)
            {
                CreateVariant((CameraAimExperimentMode)modes.GetValue(index), CameraVariantScenePaths[index]);
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(LegacySandboxScenePath) != null)
            {
                AssetDatabase.DeleteAsset(LegacySandboxScenePath);
            }

            RegisterSandboxesForTests();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Created {CameraVariantScenePaths.Length} EntropyTag camera comparison sandboxes.");
        }

        public static bool IsCameraVariantScene(string path)
        {
            return !string.IsNullOrWhiteSpace(path) &&
                   path.StartsWith(CameraVariantFolder, StringComparison.Ordinal);
        }

        private static void CreateVariant(CameraAimExperimentMode mode, string scenePath)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            InputActionAsset inputActions =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(ProjectFoundationSetup.InputActionsPath);

            if (inputActions == null)
            {
                throw new InvalidOperationException(
                    $"Required Input Action Asset was not imported: {ProjectFoundationSetup.InputActionsPath}");
            }

            CreateEnvironment(mode);
            CreatePlayerRig(inputActions, mode);
            EditorSceneManager.SaveScene(scene, scenePath);
        }

        private static void RegisterSandboxesForTests()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            scenes.RemoveAll(scene =>
                scene.path == LegacySandboxScenePath || IsCameraVariantScene(scene.path));

            foreach (string scenePath in CameraVariantScenePaths)
            {
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void CreateEnvironment(CameraAimExperimentMode mode)
        {
            new GameObject(CameraAimExperimentController.GetDisplayName(mode));

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Sandbox Floor";
            floor.transform.SetPositionAndRotation(new Vector3(0f, -0.25f, 0f), Quaternion.identity);
            floor.transform.localScale = new Vector3(20f, 0.5f, 20f);

            GameObject rearWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rearWall.name = "Camera Collision Wall";
            rearWall.transform.SetPositionAndRotation(new Vector3(0f, 1.5f, -2.5f), Quaternion.identity);
            rearWall.transform.localScale = new Vector3(8f, 3f, 0.5f);

            GameObject aimTarget = GameObject.CreatePrimitive(PrimitiveType.Cube);
            aimTarget.name = "Aim Target";
            aimTarget.transform.SetPositionAndRotation(new Vector3(0f, 1f, 8f), Quaternion.identity);
            aimTarget.transform.localScale = new Vector3(2f, 2f, 0.5f);

            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        }

        private static void CreatePlayerRig(InputActionAsset inputActions, CameraAimExperimentMode mode)
        {
            GameObject player = new GameObject("Player");
            player.SetActive(false);
            player.transform.position = Vector3.zero;

            CharacterController controller = player.AddComponent<CharacterController>();
            controller.center = new Vector3(0f, 1f, 0f);
            controller.height = 2f;
            controller.radius = 0.4f;

            GameObject torso = GameObject.CreatePrimitive(PrimitiveType.Cube);
            torso.name = "Player Torso";
            torso.transform.SetParent(player.transform, false);
            torso.transform.localPosition = new Vector3(0f, 1f, 0f);
            torso.transform.localScale = new Vector3(0.65f, 1.4f, 0.4f);
            UnityEngine.Object.DestroyImmediate(torso.GetComponent<Collider>());

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Player Head";
            head.transform.SetParent(player.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.9f, 0f);
            head.transform.localScale = Vector3.one * 0.55f;
            UnityEngine.Object.DestroyImmediate(head.GetComponent<Collider>());

            PlayerInputSource input = player.AddComponent<PlayerInputSource>();
            input.Configure(inputActions);

            GameObject cameraObject = new GameObject("Player Camera");
            Camera playerCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();

            ThirdPersonCameraRig cameraRig = cameraObject.AddComponent<ThirdPersonCameraRig>();
            cameraRig.Configure(player.transform, input, playerCamera);

            ThirdPersonAimSolver aimSolver = cameraObject.AddComponent<ThirdPersonAimSolver>();
            aimSolver.Configure(playerCamera, Physics.DefaultRaycastLayers);
            CreateAimPresentation(input, cameraRig, aimSolver, mode);

            GameObject muzzleObject = new GameObject("Test Projectile Muzzle");
            muzzleObject.transform.SetParent(player.transform, false);
            muzzleObject.transform.localPosition = new Vector3(0f, 1.2f, 0.6f);
            TestProjectileShooter shooter = player.AddComponent<TestProjectileShooter>();
            shooter.Configure(input, aimSolver, muzzleObject.transform);

            ThirdPersonMotor motor = player.AddComponent<ThirdPersonMotor>();
            motor.Configure(input, playerCamera.transform);
            player.AddComponent<PlayerSandboxDiagnostics>();

            cameraRig.Simulate(Vector2.zero, false);
            player.SetActive(true);
        }

        private static void CreateAimPresentation(
            PlayerInputSource input,
            ThirdPersonCameraRig cameraRig,
            ThirdPersonAimSolver aimSolver,
            CameraAimExperimentMode mode)
        {
            GameObject canvasObject = new GameObject("Aim Canvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject reticleObject = new GameObject("Experiment Reticle", typeof(RectTransform));
            reticleObject.transform.SetParent(canvasObject.transform, false);
            RectTransform reticle = (RectTransform)reticleObject.transform;
            reticle.anchorMin = new Vector2(0.5f, 0.5f);
            reticle.anchorMax = new Vector2(0.5f, 0.5f);
            reticle.pivot = new Vector2(0.5f, 0.5f);
            reticle.anchoredPosition = Vector2.zero;
            reticle.sizeDelta = new Vector2(24f, 24f);

            Image horizontal = CreateReticleBar(reticle, "Reticle Horizontal", new Vector2(24f, 3f));
            Image vertical = CreateReticleBar(reticle, "Reticle Vertical", new Vector2(3f, 24f));

            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "Aim Contact Marker";
            marker.transform.localScale = Vector3.one * 0.15f;
            UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.SetActive(false);

            AimReticlePresenter presenter = canvasObject.AddComponent<AimReticlePresenter>();
            presenter.Configure(aimSolver, new Graphic[] { horizontal, vertical }, marker.transform);

            CameraAimExperimentController experiment = canvasObject.AddComponent<CameraAimExperimentController>();
            experiment.Configure(mode, input, cameraRig, aimSolver, reticle);
            CreateVariantLabel(canvasObject.transform, mode);
        }

        private static void CreateVariantLabel(Transform parent, CameraAimExperimentMode mode)
        {
            GameObject labelObject = new GameObject("Variant Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(parent, false);

            RectTransform rect = (RectTransform)labelObject.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(20f, -20f);
            rect.sizeDelta = new Vector2(700f, 50f);

            Text label = labelObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 24;
            label.color = Color.white;
            label.alignment = TextAnchor.UpperLeft;
            label.raycastTarget = false;
            label.text = CameraAimExperimentController.GetDisplayName(mode);
        }

        private static Image CreateReticleBar(Transform parent, string name, Vector2 size)
        {
            GameObject bar = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bar.transform.SetParent(parent, false);
            RectTransform rectTransform = (RectTransform)bar.transform;
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = size;

            Image image = bar.GetComponent<Image>();
            image.color = Color.yellow;
            image.raycastTarget = false;
            return image;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = System.IO.Path.GetFileName(path);

            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException($"Cannot create Unity folder '{path}'.");
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
