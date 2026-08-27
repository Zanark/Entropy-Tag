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
        public const string SelectedSandboxScenePath =
            "Assets/EntropyTag/Scenes/Tests/Sandbox_PlayerMovement.unity";
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

            CreateVariant(CameraAimExperimentMode.FreeAimContinuousFollow, SelectedSandboxScenePath);

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

        public static bool IsSandboxScene(string path)
        {
            return path == SelectedSandboxScenePath || IsCameraVariantScene(path);
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

            TerritorySurface territorySurface = CreateEnvironment(mode, out Transform spawnPoint);
            CreatePlayerRig(inputActions, mode, territorySurface, spawnPoint);
            EditorSceneManager.SaveScene(scene, scenePath);
        }

        private static void RegisterSandboxesForTests()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            scenes.RemoveAll(scene =>
                scene.path == LegacySandboxScenePath || IsSandboxScene(scene.path));

            scenes.Add(new EditorBuildSettingsScene(SelectedSandboxScenePath, true));

            foreach (string scenePath in CameraVariantScenePaths)
            {
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static TerritorySurface CreateEnvironment(
            CameraAimExperimentMode mode,
            out Transform spawnPoint)
        {
            new GameObject(CameraAimExperimentController.GetDisplayName(mode));

            TerritorySurface territorySurface = CreatePaintableBox(
                "Sandbox Floor",
                new Vector3(0f, -0.25f, 0f),
                new Vector3(40f, 0.5f, 40f),
                Quaternion.identity,
                false);

            CreatePaintableBox(
                "Camera Collision Wall",
                new Vector3(0f, 1.5f, -2.5f),
                new Vector3(8f, 3f, 0.5f));

            CreatePaintableBox(
                "Aim Target",
                new Vector3(0f, 1f, 8f),
                new Vector3(2f, 2f, 0.5f));

            CreateMovementGym();
            spawnPoint = CreateSpawnPoint();

            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            return territorySurface;
        }

        private static void CreatePlayerRig(
            InputActionAsset inputActions,
            CameraAimExperimentMode mode,
            TerritorySurface territorySurface,
            Transform spawnPoint)
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
            GameObject visual = new GameObject("Player Visual");
            visual.transform.SetParent(player.transform, false);

            torso.transform.SetParent(visual.transform, false);
            torso.transform.localPosition = new Vector3(0f, 1f, 0f);
            torso.transform.localScale = new Vector3(0.65f, 1.4f, 0.4f);
            UnityEngine.Object.DestroyImmediate(torso.GetComponent<Collider>());

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Player Head";
            head.transform.SetParent(visual.transform, false);
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
            Canvas aimCanvas = CreateAimPresentation(input, cameraRig, aimSolver, mode);

            GameObject muzzleObject = new GameObject("Test Projectile Muzzle");
            muzzleObject.transform.SetParent(player.transform, false);
            muzzleObject.transform.localPosition = new Vector3(0f, 1.2f, 0.6f);
            TestProjectileShooter shooter = player.AddComponent<TestProjectileShooter>();
            shooter.Configure(input, aimSolver, muzzleObject.transform, territorySurface);
            CreateTerritoryDebugPresentation(
                aimCanvas.transform,
                territorySurface,
                shooter,
                player.transform);

            ThirdPersonMotor motor = player.AddComponent<ThirdPersonMotor>();
            motor.Configure(input, playerCamera.transform, visual.transform);
            PlayerRespawnController respawn = player.AddComponent<PlayerRespawnController>();
            respawn.Configure(spawnPoint);
            player.AddComponent<PlayerSandboxDiagnostics>();

            cameraRig.Simulate(Vector2.zero, false);
            player.SetActive(true);
        }

        private static Canvas CreateAimPresentation(
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
            return canvas;
        }

        private static void CreateTerritoryDebugPresentation(
            Transform parent,
            TerritorySurface territorySurface,
            TestProjectileShooter shooter,
            Transform player)
        {
            GameObject labelObject = new GameObject(
                "Territory Debug Label",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            labelObject.transform.SetParent(parent, false);

            RectTransform rect = (RectTransform)labelObject.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-20f, -20f);
            rect.sizeDelta = new Vector2(430f, 220f);

            Text label = labelObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 20;
            label.color = new Color32(0, 180, 70, 255);
            label.alignment = TextAnchor.UpperRight;
            label.raycastTarget = false;
            Outline outline = labelObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            TerritoryDebugPresenter presenter = labelObject.AddComponent<TerritoryDebugPresenter>();
            presenter.Configure(territorySurface, shooter, player, label);
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

        private static void CreateMovementGym()
        {
            CreatePaintableBox("Climb Wall", new Vector3(8f, 2.5f, 2f), new Vector3(0.5f, 5f, 7f));
            CreatePaintableBox("Raised Platform", new Vector3(11f, 2.5f, 2f), new Vector3(5.5f, 0.5f, 7f));
            CreatePaintableBox(
                "Ramp",
                new Vector3(-7f, 0.75f, 7f),
                new Vector3(4f, 0.5f, 8f),
                Quaternion.Euler(-14f, 0f, 0f));
            CreatePaintableBox("Narrow Beam", new Vector3(-1f, 1.25f, -8f), new Vector3(1f, 0.5f, 10f));

            for (int index = 0; index < 4; index++)
            {
                float height = 0.4f + index * 0.4f;
                CreatePaintableBox(
                    $"Step {index + 1}",
                    new Vector3(3f + index * 1.2f, height * 0.5f, 9f),
                    new Vector3(1.2f, height, 3f));
            }

            CreatePaintableBox("Slide Tunnel Roof", new Vector3(-7f, 1.35f, -3f), new Vector3(6f, 0.3f, 4f));
            CreatePaintableBox("Slide Tunnel Left", new Vector3(-10.15f, 0.75f, -3f), new Vector3(0.3f, 1.5f, 4f));
            CreatePaintableBox("Slide Tunnel Right", new Vector3(-3.85f, 0.75f, -3f), new Vector3(0.3f, 1.5f, 4f));
        }

        private static Transform CreateSpawnPoint()
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "Spawn Area";
            marker.layer = LayerMask.NameToLayer("Ignore Raycast");
            marker.transform.SetPositionAndRotation(new Vector3(0f, 0.015f, 0f), Quaternion.identity);
            marker.transform.localScale = new Vector3(1.6f, 0.015f, 1.6f);
            UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.AddComponent<MangaStructureStyle>();

            GameObject labelObject = new GameObject("Spawn Area Label");
            labelObject.layer = LayerMask.NameToLayer("Ignore Raycast");
            labelObject.transform.SetPositionAndRotation(
                new Vector3(0f, 0.045f, 0f),
                Quaternion.Euler(90f, 0f, 0f));
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = "SPAWN";
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 48;
            label.characterSize = 0.04f;
            label.color = Color.black;

            GameObject point = new GameObject("Player Spawn Point");
            point.transform.SetPositionAndRotation(new Vector3(0f, 0.05f, 0f), Quaternion.identity);
            return point.transform;
        }

        private static TerritorySurface CreatePaintableBox(
            string name,
            Vector3 position,
            Vector3 scale,
            Quaternion? rotation = null,
            bool includeVerticalFaces = true)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetPositionAndRotation(position, rotation ?? Quaternion.identity);
            box.transform.localScale = scale;
            box.AddComponent<MangaStructureStyle>();
            Collider sourceCollider = box.GetComponent<Collider>();

            TerritorySurface top = CreatePaintableFace(
                box,
                sourceCollider,
                "Top",
                new Vector3(0f, 0.5f, 0f),
                Vector3.up,
                Vector3.right,
                scale.x,
                scale.z);

            if (includeVerticalFaces)
            {
                CreatePaintableFace(
                    box,
                    sourceCollider,
                    "Front",
                    new Vector3(0f, 0f, 0.5f),
                    Vector3.forward,
                    Vector3.right,
                    scale.x,
                    scale.y);
                CreatePaintableFace(
                    box,
                    sourceCollider,
                    "Back",
                    new Vector3(0f, 0f, -0.5f),
                    Vector3.back,
                    Vector3.left,
                    scale.x,
                    scale.y);
                CreatePaintableFace(
                    box,
                    sourceCollider,
                    "Right",
                    new Vector3(0.5f, 0f, 0f),
                    Vector3.right,
                    Vector3.back,
                    scale.z,
                    scale.y);
                CreatePaintableFace(
                    box,
                    sourceCollider,
                    "Left",
                    new Vector3(-0.5f, 0f, 0f),
                    Vector3.left,
                    Vector3.forward,
                    scale.z,
                    scale.y);
            }

            return top;
        }

        private static TerritorySurface CreatePaintableFace(
            GameObject source,
            Collider sourceCollider,
            string faceName,
            Vector3 localCenter,
            Vector3 localNormal,
            Vector3 localRight,
            float worldWidth,
            float worldHeight)
        {
            Vector3 worldNormal = source.transform.TransformDirection(localNormal).normalized;
            Vector3 worldRight = source.transform.TransformDirection(localRight).normalized;
            Vector3 worldUp = Vector3.Cross(worldNormal, worldRight).normalized;
            var face = new GameObject($"{source.name} {faceName} Territory");
            face.transform.SetPositionAndRotation(
                source.transform.TransformPoint(localCenter) + worldNormal * 0.003f,
                Quaternion.LookRotation(worldNormal, worldUp));
            face.transform.localScale = new Vector3(worldWidth, worldHeight, 1f);

            TerritorySurface surface = face.AddComponent<TerritorySurface>();
            int logicalWidth = Mathf.Clamp(Mathf.CeilToInt(worldWidth * 2f), 4, 64);
            int logicalHeight = Mathf.Clamp(Mathf.CeilToInt(worldHeight * 2f), 4, 64);
            int visualResolution =
                Mathf.Max(worldWidth, worldHeight) >= 20f ? 256 :
                Mathf.Max(worldWidth, worldHeight) >= 6f ? 128 :
                64;
            surface.Configure(
                sourceCollider,
                face.GetComponent<Renderer>(),
                logicalWidth,
                logicalHeight,
                visualResolution);
            return surface;
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
