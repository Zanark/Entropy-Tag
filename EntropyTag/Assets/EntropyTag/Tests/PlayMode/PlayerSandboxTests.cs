using System.Collections;
using System.Diagnostics;
using EntropyTag.Infrastructure;
using EntropyTag.Presentation;
using EntropyTag.UnityAdapters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace EntropyTag.Tests.PlayMode
{
    public sealed class PlayerSandboxTests
    {
        private const string SandboxScenePath =
            "Assets/EntropyTag/Scenes/Tests/CameraVariants/Sandbox_Camera_01_CenteredImmediate.unity";

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

            ThirdPersonMotor motor = Object.FindObjectOfType<ThirdPersonMotor>();
            ThirdPersonCameraRig cameraRig = Object.FindObjectOfType<ThirdPersonCameraRig>();
            ThirdPersonAimSolver aimSolver = Object.FindObjectOfType<ThirdPersonAimSolver>();
            AimReticlePresenter aimPresenter = Object.FindObjectOfType<AimReticlePresenter>();
            CameraAimExperimentController experiment = Object.FindObjectOfType<CameraAimExperimentController>();
            TestProjectileShooter shooter = Object.FindObjectOfType<TestProjectileShooter>();
            PlayerSandboxDiagnostics diagnostics = Object.FindObjectOfType<PlayerSandboxDiagnostics>();

            Assert.That(motor, Is.Not.Null);
            Assert.That(cameraRig, Is.Not.Null);
            Assert.That(aimSolver, Is.Not.Null);
            Assert.That(aimPresenter, Is.Not.Null);
            Assert.That(experiment, Is.Not.Null);
            Assert.That(experiment.Mode, Is.EqualTo(CameraAimExperimentMode.CenteredImmediate));
            Assert.That(shooter, Is.Not.Null);
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
        public IEnumerator CameraStudyScenesContainAllEightDistinctModes()
        {
            for (int index = 0; index < CameraVariantScenePaths.Length; index++)
            {
                yield return LoadSandbox(CameraVariantScenePaths[index]);

                CameraAimExperimentController experiment =
                    Object.FindObjectOfType<CameraAimExperimentController>();

                Assert.That(experiment, Is.Not.Null, CameraVariantScenePaths[index]);
                Assert.That((int)experiment.Mode, Is.EqualTo(index + 1), CameraVariantScenePaths[index]);
                Assert.That(Object.FindObjectOfType<TestProjectileShooter>(), Is.Not.Null);
                Assert.That(Object.FindObjectOfType<ThirdPersonMotor>(), Is.Not.Null);
            }
        }

        [UnityTest]
        public IEnumerator TestProjectileUsesTheSharedAimSolution()
        {
            yield return LoadSandbox();

            TestProjectileShooter shooter = Object.FindObjectOfType<TestProjectileShooter>();
            ThirdPersonAimSolver aimSolver = Object.FindObjectOfType<ThirdPersonAimSolver>();
            yield return null;

            Assert.That(aimSolver.Current.Direction.sqrMagnitude, Is.GreaterThan(0.99f));
            Assert.That(shooter.FireOnce(), Is.True);
            Assert.That(shooter.ActiveProjectileCount, Is.EqualTo(1));
            Assert.That(shooter.LastFiredVelocity.magnitude, Is.GreaterThan(0f));
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

            ThirdPersonMotor motor = Object.FindObjectOfType<ThirdPersonMotor>();
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
    }
}
