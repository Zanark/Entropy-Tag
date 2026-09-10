using System.Collections;
using System.Diagnostics;
using EntropyTag.Domain;
using EntropyTag.Infrastructure;
using EntropyTag.Presentation;
using EntropyTag.UnityAdapters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace EntropyTag.Tests.PlayMode
{
    public sealed class PlayerSandboxTests
    {
        private const string SandboxScenePath =
            "Assets/EntropyTag/Scenes/Tests/Sandbox_PlayerMovement.unity";

        private static readonly string[] CameraVariantScenePaths =
        {
            "Assets/EntropyTag/Scenes/Tests/CameraVariants/Sandbox_Camera_01_CenteredImmediate.unity",
            "Assets/EntropyTag/Scenes/Tests/CameraVariants/Sandbox_Camera_02_CenteredSmooth.unity",
            "Assets/EntropyTag/Scenes/Tests/CameraVariants/Sandbox_Camera_03_CenteredShoulder.unity",
            "Assets/EntropyTag/Scenes/Tests/CameraVariants/Sandbox_Camera_04_FreeAimEdgeTurn.unity",
            "Assets/EntropyTag/Scenes/Tests/CameraVariants/Sandbox_Camera_05_FreeAimContinuousFollow.unity",
            "Assets/EntropyTag/Scenes/Tests/CameraVariants/Sandbox_Camera_06_ElasticTether.unity",
            "Assets/EntropyTag/Scenes/Tests/CameraVariants/Sandbox_Camera_07_SoftZoneRecenter.unity",
            "Assets/EntropyTag/Scenes/Tests/CameraVariants/Sandbox_Camera_08_CenteredCinematicSpring.unity"
        };

        [UnityTest]
        public IEnumerator SandboxProvidesMovementCameraAimAndDiagnostics()
        {
            yield return LoadSandbox();

            ThirdPersonMotor motor = FindHuman<ThirdPersonMotor>();
            ThirdPersonCameraRig cameraRig = Object.FindObjectOfType<ThirdPersonCameraRig>();
            ThirdPersonAimSolver aimSolver = Object.FindObjectOfType<ThirdPersonAimSolver>();
            AimReticlePresenter aimPresenter = Object.FindObjectOfType<AimReticlePresenter>();
            CameraAimExperimentController experiment = Object.FindObjectOfType<CameraAimExperimentController>();
            TestProjectileShooter shooter = FindHuman<TestProjectileShooter>();
            TerritorySurface territorySurface = GetFloorSurface();
            TerritoryDebugPresenter territoryPresenter = Object.FindObjectOfType<TerritoryDebugPresenter>();
            PlayerSandboxDiagnostics diagnostics = Object.FindObjectOfType<PlayerSandboxDiagnostics>();

            Assert.That(motor, Is.Not.Null);
            Assert.That(cameraRig, Is.Not.Null);
            Assert.That(aimSolver, Is.Not.Null);
            Assert.That(aimPresenter, Is.Not.Null);
            Assert.That(experiment, Is.Not.Null);
            Assert.That(experiment.Mode, Is.EqualTo(CameraAimExperimentMode.FreeAimContinuousFollow));
            Assert.That(shooter, Is.Not.Null);
            Assert.That(territorySurface, Is.Not.Null);
            Assert.That(territoryPresenter, Is.Not.Null);
            Assert.That(diagnostics, Is.Not.Null);

            Vector3 start = motor.transform.position;
            motor.Simulate(Vector2.up, cameraRig.ControlledCamera.transform, 0.1f);
            Assert.That(motor.transform.position.z, Is.GreaterThan(start.z));

            motor.AddImpulse(Vector3.right * 2f);
            motor.SetSpeedMultiplier(0.75f);
            motor.Simulate(Vector2.zero, cameraRig.ControlledCamera.transform, 0.1f);
            Assert.That(motor.transform.position.x, Is.GreaterThan(start.x));

            yield return null;
            Assert.That(diagnostics.IsRecording, Is.True);
        }

        [UnityTest]
        public IEnumerator TerritorySurfaceAlignsLogicalVisualCoverageAndBank()
        {
            yield return LoadSandbox();

            TerritorySurface surface = GetFloorSurface();
            Vector3 worldCenter = surface.transform.position;
            Assert.That(surface.TryWorldToCoordinate(worldCenter, out TerritoryCoordinate coordinate), Is.True);

            Color32 neutralColor = surface.GetVisualColor(coordinate);
            StampResult ice = surface.ApplyLogicalStamp(
                new[] { coordinate },
                ElementId.Ice,
                new TeamId(1));
            Color32 iceColor = surface.GetVisualColor(coordinate);

            Assert.That(ice.ChangedCells, Is.EqualTo(1));
            Assert.That(surface.GetCell(coordinate).State, Is.EqualTo(TerritoryState.Ice));
            Assert.That(iceColor, Is.Not.EqualTo(neutralColor));

            StampResult mist = surface.ApplyLogicalStamp(
                new[] { coordinate },
                ElementId.Fire,
                new TeamId(2));
            Color32 mistColor = surface.GetVisualColor(coordinate);

            Assert.That(surface.GetCell(coordinate).State, Is.EqualTo(TerritoryState.Mist));
            Assert.That(mist.BankAward, Is.EqualTo(2));
            Assert.That(surface.FireBank, Is.EqualTo(2));
            Assert.That(mistColor, Is.Not.EqualTo(iceColor));

            StampResult fire = surface.ApplyLogicalStamp(
                new[] { coordinate },
                ElementId.Fire,
                new TeamId(2));
            CoverageSnapshot coverage = surface.GetCoverage();

            Assert.That(fire.BankAward, Is.EqualTo(1));
            Assert.That(surface.GetCell(coordinate).State, Is.EqualTo(TerritoryState.Fire));
            Assert.That(surface.FireBank, Is.EqualTo(3));
            Assert.That(coverage.GetTeam(new TeamId(2)).OwnedCells, Is.EqualTo(1));
            Assert.That(coverage.GetTeam(new TeamId(1)).OwnedCells, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ElementPresentationUsesDistinctPatternsAndTerritoryMovementAffinity()
        {
            yield return LoadSandbox();

            TerritorySurface surface = GetFloorSurface();
            ElementReactionPresentationConfig config = surface.PresentationConfig;
            TerritoryMovementController territoryMovement =
                FindHuman<TerritoryMovementController>();
            ElementReactionFeedback feedback = Object.FindObjectOfType<ElementReactionFeedback>();
            ThirdPersonMotor motor = FindHuman<ThirdPersonMotor>();
            TestProjectileShooter shooter = FindHuman<TestProjectileShooter>();
            TerritoryDebugPresenter presenter = Object.FindObjectOfType<TerritoryDebugPresenter>();
            Text movementLabel =
                GameObject.Find("Territory Movement Status Label").GetComponent<Text>();
            Image movementBackground =
                GameObject.Find("Territory Movement Status Background").GetComponent<Image>();

            Assert.That(config, Is.Not.Null);
            Assert.That(config.Ice.Pattern, Is.EqualTo(TerritoryPattern.DiagonalStripes));
            Assert.That(config.Fire.Pattern, Is.EqualTo(TerritoryPattern.Dots));
            Assert.That(config.Mist.Pattern, Is.EqualTo(TerritoryPattern.Crosshatch));
            Assert.That(config.FriendlyMovementMultiplier, Is.EqualTo(1.4f).Within(0.001f));
            Assert.That(config.HostileMovementMultiplier, Is.EqualTo(0.7f).Within(0.001f));
            Assert.That(territoryMovement, Is.Not.Null);
            Assert.That(feedback, Is.Not.Null);

            territoryMovement.Simulate(TerritoryState.Ice);
            Assert.That(
                territoryMovement.CurrentEffect,
                Is.EqualTo(TerritoryMovementEffect.FriendlyBoost));
            Assert.That(motor.SpeedMultiplier, Is.EqualTo(1.4f).Within(0.001f));
            presenter.RefreshNow();
            Assert.That(movementLabel.text, Does.Contain("FRIENDLY BOOST x1.40"));
            Assert.That((Color32)movementLabel.color, Is.EqualTo(new Color32(30, 210, 255, 255)));
            Assert.That(movementBackground.enabled, Is.False);

            territoryMovement.Simulate(TerritoryState.Fire);
            Assert.That(
                territoryMovement.CurrentEffect,
                Is.EqualTo(TerritoryMovementEffect.HostileSlow));
            Assert.That(motor.SpeedMultiplier, Is.EqualTo(0.7f).Within(0.001f));
            presenter.RefreshNow();
            Assert.That(movementLabel.text, Does.Contain("HOSTILE SLOW x0.70"));
            Assert.That((Color32)movementLabel.color, Is.EqualTo(new Color32(30, 210, 255, 255)));
            Assert.That(movementBackground.enabled, Is.True);
            Assert.That(
                (Color32)movementBackground.color,
                Is.EqualTo(new Color32(255, 85, 20, 230)));
            Assert.That(movementBackground.rectTransform.sizeDelta.x, Is.LessThanOrEqualTo(340f));
            Assert.That(
                movementBackground.rectTransform.sizeDelta.x,
                Is.EqualTo(movementLabel.preferredWidth + 20f).Within(1f));

            territoryMovement.Simulate(TerritoryState.Mist);
            Assert.That(
                territoryMovement.CurrentEffect,
                Is.EqualTo(TerritoryMovementEffect.Normal));
            Assert.That(motor.SpeedMultiplier, Is.EqualTo(1f).Within(0.001f));
            presenter.RefreshNow();
            Assert.That(movementLabel.text, Does.Contain("Normal x1.00"));
            Assert.That(movementBackground.enabled, Is.False);

            shooter.SwitchElement();
            territoryMovement.Simulate(TerritoryState.Fire);
            Assert.That(
                territoryMovement.CurrentEffect,
                Is.EqualTo(TerritoryMovementEffect.FriendlyBoost));
            Assert.That(motor.SpeedMultiplier, Is.EqualTo(1.4f).Within(0.001f));
            presenter.RefreshNow();
            Assert.That((Color32)movementLabel.color, Is.EqualTo(new Color32(255, 85, 20, 255)));
            Assert.That(movementBackground.enabled, Is.False);

            territoryMovement.Simulate(TerritoryState.Ice);
            Assert.That(
                territoryMovement.CurrentEffect,
                Is.EqualTo(TerritoryMovementEffect.HostileSlow));
            Assert.That(motor.SpeedMultiplier, Is.EqualTo(0.7f).Within(0.001f));
            presenter.RefreshNow();
            Assert.That((Color32)movementLabel.color, Is.EqualTo(new Color32(255, 85, 20, 255)));
            Assert.That(movementBackground.enabled, Is.True);
            Assert.That(
                (Color32)movementBackground.color,
                Is.EqualTo(new Color32(30, 210, 255, 230)));
        }

        [UnityTest]
        public IEnumerator ContestedWorldStampPublishesAuthoritativeReactionFeedback()
        {
            yield return LoadSandbox();

            TerritorySurface surface = GetFloorSurface();
            ElementReactionFeedback feedback = Object.FindObjectOfType<ElementReactionFeedback>();
            Vector3 point = surface.transform.position;
            TerritoryReactionEvent? observed = null;
            System.Action<TerritoryReactionEvent> handler = reaction => observed = reaction;
            TerritorySurfaceRegistry.ReactionOccurred += handler;

            try
            {
                surface.ApplyWorldStamp(point, 0.25f, ElementId.Ice, new TeamId(1));
                observed = null;
                surface.ApplyWorldStamp(point, 0.25f, ElementId.Fire, new TeamId(2));
                yield return null;

                Assert.That(observed.HasValue, Is.True);
                Assert.That(observed.Value.Kind, Is.EqualTo(TerritoryReactionKind.MistCreated));
                Assert.That(observed.Value.Surface, Is.SameAs(surface));
                Assert.That(observed.Value.WorldPoint, Is.EqualTo(point));
                Assert.That(observed.Value.CurrentState, Is.EqualTo(TerritoryState.Mist));
                Assert.That(observed.Value.AppliedElement, Is.EqualTo(ElementId.Fire));
                Assert.That(observed.Value.BankAward, Is.GreaterThan(0));
                Assert.That(feedback.CreatedSlotCount, Is.EqualTo(6));
                Assert.That(feedback.ActiveFeedbackCount, Is.GreaterThan(0));
                Assert.That(feedback.HasAudioPlaceholder, Is.True);
            }
            finally
            {
                TerritorySurfaceRegistry.ReactionOccurred -= handler;
            }
        }

        [UnityTest]
        public IEnumerator SandboxStructuresUseSharedMangaShadingAndHudShowsFps()
        {
            yield return LoadSandbox();
            yield return null;

            MangaStructureStyle[] styles = Object.FindObjectsOfType<MangaStructureStyle>();
            Assert.That(styles.Length, Is.GreaterThan(10));
            Material baseMaterial = styles[0].GetComponent<MeshRenderer>().sharedMaterial;
            Material outlineMaterial =
                styles[0].OutlineObject.GetComponent<MeshRenderer>().sharedMaterial;

            for (int index = 0; index < styles.Length; index++)
            {
                Assert.That(styles[index].OutlineObject, Is.Not.Null);
                Assert.That(
                    styles[index].GetComponent<MeshRenderer>().sharedMaterial,
                    Is.SameAs(baseMaterial));
                Assert.That(
                    styles[index].OutlineObject.GetComponent<MeshRenderer>().sharedMaterial,
                    Is.SameAs(outlineMaterial));
            }

            Text territoryLabel = GameObject.Find("Territory Debug Label").GetComponent<Text>();
            Assert.That(territoryLabel.text, Does.StartWith("FPS: "));
            Assert.That(territoryLabel.text, Does.Contain("<color=#1ED2FF>Selected: Ice"));
            Assert.That(territoryLabel.text, Does.Contain("<color=#1ED2FF>Ice:"));
            Assert.That(territoryLabel.text, Does.Contain("<color=#FF5514>Fire:"));
            Assert.That(territoryLabel.text, Does.Contain("<color=#00B446>Standing on: Neutral</color>"));
            Assert.That((Color32)territoryLabel.color, Is.EqualTo(new Color32(0, 180, 70, 255)));
            Assert.That(territoryLabel.GetComponent<Outline>(), Is.Not.Null);
            Color32 baseColor = baseMaterial.GetColor("_BaseColor");
            Assert.That(baseColor, Is.EqualTo(new Color32(252, 252, 250, 255)));
            Assert.That(GameObject.Find("Spawn Area"), Is.Not.Null);
            TextMesh spawnLabel = GameObject.Find("Spawn Area Label").GetComponent<TextMesh>();
            Assert.That(spawnLabel, Is.Not.Null);
            Assert.That(spawnLabel.characterSize, Is.EqualTo(0.04f).Within(0.0001f));
            Assert.That(Vector3.Dot(spawnLabel.transform.forward, Vector3.down), Is.GreaterThan(0.99f));

            TestProjectileShooter shooter = FindHuman<TestProjectileShooter>();
            shooter.SwitchElement();
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(territoryLabel.text, Does.Contain("<color=#FF5514>Selected: Fire"));
        }

        [UnityTest]
        public IEnumerator PlayerRespawnsAtMarkedSpawnAfterFalling()
        {
            yield return LoadSandbox();

            PlayerRespawnController respawn = FindHuman<PlayerRespawnController>();
            ThirdPersonMotor motor = FindHuman<ThirdPersonMotor>();
            Assert.That(respawn, Is.Not.Null);
            Assert.That(respawn.SpawnPoint, Is.Not.Null);

            motor.Respawn(new Vector3(15f, -10f, 15f), Quaternion.Euler(0f, 90f, 0f));
            yield return null;

            Assert.That(respawn.RespawnCount, Is.EqualTo(1));
            Assert.That(
                Vector3.Distance(motor.transform.position, respawn.SpawnPoint.position),
                Is.LessThan(0.01f));
            Assert.That(motor.Velocity.magnitude, Is.LessThan(0.1f));
        }

        [UnityTest]
        public IEnumerator TerritoryResetAndPlayerSamplingUseLogicalFieldWithoutReadback()
        {
            yield return LoadSandbox();

            TerritorySurface surface = GetFloorSurface();
            ThirdPersonMotor motor = FindHuman<ThirdPersonMotor>();
            Vector3 worldCenter = surface.transform.position;
            surface.TryWorldToCoordinate(worldCenter, out TerritoryCoordinate coordinate);
            surface.ApplyLogicalStamp(new[] { coordinate }, ElementId.Ice, new TeamId(1));
            surface.FlushVisuals();

            Assert.That(
                TerritorySurfaceRegistry.TrySampleBelow(
                    motor.transform.position + Vector3.up * 0.25f,
                    2f,
                    out TerritorySurface sampledSurface,
                    out TerritoryCell sampledCell),
                Is.True);
            Assert.That(sampledSurface, Is.SameAs(surface));
            Assert.That(sampledCell.State, Is.EqualTo(TerritoryState.Ice));

            surface.ResetTerritory();

            Assert.That(surface.GetCell(coordinate), Is.EqualTo(TerritoryCell.Neutral));
            Assert.That(surface.GetCoverage().NeutralCells, Is.EqualTo(surface.LogicalWidth * surface.LogicalHeight));
            Assert.That(surface.IceBank, Is.Zero);
            Assert.That(surface.FireBank, Is.Zero);
            Assert.That(surface.GetVisualColor(coordinate), Is.EqualTo(new Color32(75, 75, 80, 0)));
        }

        [UnityTest]
        public IEnumerator ProjectileImpactStampsAuthoritativeTerritory()
        {
            yield return LoadSandbox();
            yield return null;

            TerritorySurface surface = GetFloorSurface();
            TestProjectileShooter shooter = FindHuman<TestProjectileShooter>();
            Collider floorCollider = surface.SourceCollider;
            Vector3 point = surface.transform.position;
            surface.TryWorldToCoordinate(point, out TerritoryCoordinate coordinate);

            Assert.That(shooter.FireOnce(), Is.True);
            shooter.HandleProjectileImpact(0, floorCollider, point, Vector3.up);

            Assert.That(surface.GetCell(coordinate).State, Is.EqualTo(TerritoryState.Ice));
            Assert.That(shooter.ActiveProjectileCount, Is.Zero);
            Assert.That(shooter.SplatCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RenderedFloorUvMatchesLogicalWorldCoordinates()
        {
            yield return LoadSandbox();

            TerritorySurface surface = GetFloorSurface();
            Vector3[] samplePoints =
            {
                new Vector3(-0.3f, -0.2f, 0f),
                new Vector3(0.25f, -0.3f, 0f),
                new Vector3(-0.2f, 0.3f, 0f),
                new Vector3(0.3f, 0.2f, 0f)
            };

            for (int index = 0; index < samplePoints.Length; index++)
            {
                Vector3 localPoint = samplePoints[index];
                Vector3 worldPoint = surface.transform.TransformPoint(localPoint);
                Assert.That(surface.TryWorldToCoordinate(worldPoint, out TerritoryCoordinate coordinate), Is.True);
                surface.ApplyLogicalStamp(new[] { coordinate }, ElementId.Fire, new TeamId(2));
                surface.FlushVisuals();

                Assert.That(
                    surface.GetRenderedColorAtWorldPoint(worldPoint),
                    Is.EqualTo(new Color32(255, 85, 20, 128)),
                    $"Rendered floor must show the logical cell at local point {localPoint}.");
            }
        }

        [UnityTest]
        public IEnumerator ProjectileImpactPaintsVerticalWallAtImpactLocation()
        {
            yield return LoadSandbox();
            yield return null;

            TerritorySurface wall =
                GameObject.Find("Camera Collision Wall Front Territory").GetComponent<TerritorySurface>();
            TestProjectileShooter shooter = FindHuman<TestProjectileShooter>();
            Vector3 point = wall.transform.position;

            Assert.That(
                TerritorySurfaceRegistry.TryGet(
                    wall.SourceCollider,
                    point,
                    wall.transform.forward,
                    out TerritorySurface resolved),
                Is.True);
            Assert.That(resolved, Is.SameAs(wall));
            Assert.That(wall.TryWorldToCoordinate(point, out TerritoryCoordinate coordinate), Is.True);
            Assert.That(shooter.FireOnce(), Is.True);

            shooter.HandleProjectileImpact(
                0,
                wall.SourceCollider,
                point,
                wall.transform.forward);
            wall.FlushVisuals();

            Assert.That(wall.GetCell(coordinate).State, Is.EqualTo(TerritoryState.Ice));
            Assert.That(
                wall.GetRenderedColorAtWorldPoint(point),
                Is.EqualTo(new Color32(30, 210, 255, 64)));
        }

        [UnityTest]
        public IEnumerator SustainedTerritoryStampingFitsProvisionalBudget()
        {
            yield return LoadSandbox();

            TerritorySurface surface = GetFloorSurface();
            Vector3 center = surface.transform.position;

            for (int index = 0; index < 32; index++)
            {
                surface.ApplyWorldStamp(center, 0.75f, ElementId.Ice, new TeamId(1));
                surface.ApplyWorldStamp(center, 0.75f, ElementId.Fire, new TeamId(2));
            }

            long allocatedBefore = System.GC.GetAllocatedBytesForCurrentThread();
            var stopwatch = Stopwatch.StartNew();
            const int StampCount = 600;

            for (int index = 0; index < StampCount; index++)
            {
                float localX = ((index * 17) % 61 - 30) / 64f;
                float localY = ((index * 29) % 61 - 30) / 64f;
                Vector3 point = surface.transform.TransformPoint(new Vector3(localX, localY, 0f));
                bool ice = (index & 1) == 0;
                surface.ApplyWorldStamp(
                    point,
                    0.75f,
                    ice ? ElementId.Ice : ElementId.Fire,
                    ice ? new TeamId(1) : new TeamId(2));
            }

            stopwatch.Stop();
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            double averageMilliseconds = stopwatch.Elapsed.TotalMilliseconds / StampCount;

            var uploadStopwatch = Stopwatch.StartNew();
            surface.FlushVisuals();
            uploadStopwatch.Stop();

            Assert.That(allocated, Is.LessThanOrEqualTo(1024L));
            Assert.That(averageMilliseconds, Is.LessThan(1d));
            Assert.That(uploadStopwatch.Elapsed.TotalMilliseconds, Is.LessThan(3d));
            Assert.That(surface.VisualTexture.width, Is.EqualTo(256));
            Assert.That(surface.LogicalWidth * surface.LogicalHeight, Is.EqualTo(4096));
            Assert.That(surface.EstimatedCpuBytes, Is.LessThanOrEqualTo(768 * 1024));
            Assert.That(surface.EstimatedGpuBytes, Is.EqualTo(256 * 256 * 4));
        }

        [UnityTest]
        public IEnumerator PlayerCanJumpAndSlide()
        {
            yield return LoadSandbox();
            yield return null;

            ThirdPersonMotor motor = FindHuman<ThirdPersonMotor>();
            ThirdPersonCameraRig cameraRig = Object.FindObjectOfType<ThirdPersonCameraRig>();
            CharacterController controller = motor.GetComponent<CharacterController>();

            motor.Simulate(Vector2.up, cameraRig.ControlledCamera.transform, 1f / 60f, false, true);
            Assert.That(motor.IsSliding, Is.True);
            Assert.That(controller.height, Is.LessThan(2f));

            yield return LoadSandbox();
            yield return null;

            motor = FindHuman<ThirdPersonMotor>();
            cameraRig = Object.FindObjectOfType<ThirdPersonCameraRig>();
            float startHeight = motor.transform.position.y;
            motor.Simulate(Vector2.zero, cameraRig.ControlledCamera.transform, 0.1f, true, false);

            Assert.That(motor.transform.position.y, Is.GreaterThan(startHeight));
            Assert.That(motor.Velocity.y, Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator PlayerClimbsWhenPushingIntoVerticalWall()
        {
            yield return LoadSandbox();

            ThirdPersonMotor motor = FindHuman<ThirdPersonMotor>();
            ThirdPersonCameraRig cameraRig = Object.FindObjectOfType<ThirdPersonCameraRig>();
            motor.transform.position = new Vector3(7.3f, 0f, 2f);
            Physics.SyncTransforms();
            float startHeight = motor.transform.position.y;

            motor.Simulate(Vector2.right, cameraRig.ControlledCamera.transform, 0.1f, false, false);

            Assert.That(motor.IsWallClimbing, Is.True);
            Assert.That(motor.transform.position.y, Is.GreaterThan(startHeight));
        }

        [UnityTest]
        public IEnumerator CameraStudyScenesContainAllEightDistinctModes()
        {
            for (int index = 0; index < CameraVariantScenePaths.Length; index++)
            {
                yield return LoadSandbox(CameraVariantScenePaths[index]);

                CameraAimExperimentController experiment =
                    Object.FindObjectOfType<CameraAimExperimentController>();

                Assert.That(experiment, Is.Not.Null, CameraVariantScenePaths[index]);
                Assert.That((int)experiment.Mode, Is.EqualTo(index + 1), CameraVariantScenePaths[index]);
                Assert.That(FindHuman<TestProjectileShooter>(), Is.Not.Null);
                Assert.That(FindHuman<ThirdPersonMotor>(), Is.Not.Null);
            }
        }

        [UnityTest]
        public IEnumerator TestProjectileUsesTheSharedAimSolution()
        {
            yield return LoadSandbox();

            TestProjectileShooter shooter = FindHuman<TestProjectileShooter>();
            ThirdPersonAimSolver aimSolver = Object.FindObjectOfType<ThirdPersonAimSolver>();
            yield return null;

            Assert.That(aimSolver.Current.Direction.sqrMagnitude, Is.GreaterThan(0.99f));
            Assert.That(shooter.FireOnce(), Is.True);
            Assert.That(shooter.ActiveProjectileCount, Is.EqualTo(1));
            Assert.That(shooter.LastFiredVelocity.magnitude, Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator ProjectileCreatesPersistentSplatWithoutCollapsingCamera()
        {
            yield return LoadSandbox();

            TestProjectileShooter shooter = FindHuman<TestProjectileShooter>();
            ThirdPersonAimSolver aimSolver = Object.FindObjectOfType<ThirdPersonAimSolver>();
            ThirdPersonCameraRig cameraRig = Object.FindObjectOfType<ThirdPersonCameraRig>();
            ThirdPersonMotor motor = FindHuman<ThirdPersonMotor>();
            aimSolver.SetViewportPoint(new Vector2(0.5f, 0.5f));
            yield return null;

            Assert.That(shooter.FireOnce(), Is.True);
            float minimumCameraDistance = float.MaxValue;
            float timeout = Time.time + 1f;

            while (shooter.SplatCount == 0 && Time.time < timeout)
            {
                Vector3 pivot = motor.transform.position + Vector3.up * 1.5f;
                minimumCameraDistance = Mathf.Min(
                    minimumCameraDistance,
                    Vector3.Distance(cameraRig.ControlledCamera.transform.position, pivot));
                yield return null;
            }

            Assert.That(shooter.SplatCount, Is.EqualTo(1));
            Assert.That(shooter.ActiveProjectileCount, Is.EqualTo(0));
            Assert.That(minimumCameraDistance, Is.GreaterThan(1f));
            Assert.That(GameObject.Find("Test Paint Splat 01").activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator CameraCollisionAndAimUseSandboxPhysics()
        {
            yield return LoadSandbox();

            ThirdPersonCameraRig cameraRig = Object.FindObjectOfType<ThirdPersonCameraRig>();
            ThirdPersonAimSolver aimSolver = Object.FindObjectOfType<ThirdPersonAimSolver>();

            cameraRig.Simulate(Vector2.zero, false);
            Physics.SyncTransforms();

            Assert.That(cameraRig.ControlledCamera.transform.position.z, Is.GreaterThan(-2.5f));

            AimSolution solution = aimSolver.Resolve();
            Assert.That(solution.Direction.sqrMagnitude, Is.GreaterThan(0.99f));
            Assert.That(solution.HasPhysicsContact, Is.True);
            Assert.That(Vector3.Distance(solution.Origin, solution.Point), Is.GreaterThan(0f));

            yield return null;
            Assert.That(
                Vector3.Distance(Object.FindObjectOfType<AimReticlePresenter>().LastPresentedPoint, aimSolver.Current.Point),
                Is.LessThan(0.001f));
        }

        [Test]
        public void PlayerInputSettingsPersistLocally()
        {
            var store = new PlayerSettingsStore();
            var settings = PlayerInputSettings.CreateDefault();
            settings.Set(0.2f, 180f, true);

            try
            {
                store.Save(settings);
                PlayerInputSettings loaded = store.Load();

                Assert.That(loaded.MouseSensitivity, Is.EqualTo(0.2f).Within(0.0001f));
                Assert.That(loaded.GamepadSensitivity, Is.EqualTo(180f).Within(0.0001f));
                Assert.That(loaded.InvertVerticalLook, Is.True);
            }
            finally
            {
                store.Clear();
            }
        }

        [UnityTest]
        public IEnumerator PlayerLoopHasNoRecurringAllocationAndFitsFrameBudget()
        {
            yield return LoadSandbox();

            ThirdPersonMotor motor = FindHuman<ThirdPersonMotor>();
            ThirdPersonCameraRig cameraRig = Object.FindObjectOfType<ThirdPersonCameraRig>();
            ThirdPersonAimSolver aimSolver = Object.FindObjectOfType<ThirdPersonAimSolver>();
            Transform cameraTransform = cameraRig.ControlledCamera.transform;

            for (int index = 0; index < 32; index++)
            {
                motor.Simulate(Vector2.zero, cameraTransform, 1f / 60f);
                cameraRig.Simulate(Vector2.zero, false);
                aimSolver.Resolve();
            }

            long allocatedBefore = System.GC.GetAllocatedBytesForCurrentThread();
            var stopwatch = Stopwatch.StartNew();

            const int SampleCount = 300;
            for (int index = 0; index < SampleCount; index++)
            {
                motor.Simulate(Vector2.zero, cameraTransform, 1f / 60f);
                cameraRig.Simulate(Vector2.zero, false);
                aimSolver.Resolve();
            }

            stopwatch.Stop();
            long allocatedBytes = System.GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            double averageMilliseconds = stopwatch.Elapsed.TotalMilliseconds / SampleCount;

            Assert.That(allocatedBytes, Is.EqualTo(0L), "The warm player loop must not allocate managed memory.");
            Assert.That(
                averageMilliseconds,
                Is.LessThan(1d),
                "The isolated player loop must remain comfortably below the 16.67 ms frame budget.");
        }

        private static IEnumerator LoadSandbox()
        {
            return LoadSandbox(SandboxScenePath);
        }

        private static IEnumerator LoadSandbox(string scenePath)
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);

            while (!load.isDone)
            {
                yield return null;
            }

            Physics.SyncTransforms();
        }

        private static TerritorySurface GetFloorSurface()
        {
            GameObject floor = GameObject.Find("Sandbox Floor Top Territory");
            Assert.That(floor, Is.Not.Null);
            return floor.GetComponent<TerritorySurface>();
        }

        private static T FindHuman<T>() where T : Component
        {
            return GameObject.Find("Player").GetComponent<T>();
        }
    }
}
