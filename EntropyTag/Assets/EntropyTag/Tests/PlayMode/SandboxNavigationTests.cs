using System;
using System.Collections;
using System.Collections.Generic;
using EntropyTag.UnityAdapters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EntropyTag.Tests.PlayMode
{
    public sealed class SandboxNavigationTests
    {
        private const string SandboxScenePath =
            "Assets/EntropyTag/Scenes/Tests/Sandbox_PlayerMovement.unity";

        private SandboxNavigation navigation;

        [UnitySetUp]
        public IEnumerator LoadSandbox()
        {
            yield return ReloadSandbox();
        }

        [UnityTearDown]
        public IEnumerator UnloadSandbox()
        {
            Scene sandbox = SceneManager.GetSceneByPath(SandboxScenePath);

            if (sandbox.IsValid() && sandbox.isLoaded)
            {
                yield return UnloadIntoEmptyScene(sandbox);
            }
        }

        [Test]
        public void NavigationBuildsOnceFromDistinctStaticArenaColliders()
        {
            Assert.That(navigation.IsReady, Is.True);
            Assert.That(navigation.SourceCount, Is.GreaterThan(0));

            var authoredColliders = new HashSet<Collider>();
            TerritorySurface[] surfaces = Object.FindObjectsByType<TerritorySurface>(FindObjectsSortMode.None);

            for (int index = 0; index < surfaces.Length; index++)
            {
                Collider collider = surfaces[index].SourceCollider;

                if (surfaces[index].isActiveAndEnabled &&
                    collider != null &&
                    collider.enabled &&
                    !collider.isTrigger &&
                    collider.attachedRigidbody == null &&
                    !(collider is CharacterController) &&
                    collider.GetComponentInParent<ThirdPersonMotor>() == null)
                {
                    authoredColliders.Add(collider);
                }
            }

            Assert.That(navigation.SourceCount, Is.EqualTo(authoredColliders.Count));
            Assert.That(surfaces.Length, Is.GreaterThan(navigation.SourceCount),
                "Paint overlays must not become additional navigation geometry.");

            NavMeshData[] originalData = Resources.FindObjectsOfTypeAll<NavMeshData>();
            int originalIndices = NavMesh.CalculateTriangulation().indices.Length;

            for (int index = 0; index < 5; index++)
            {
                navigation.EnsureBuilt();
            }

            Assert.That(NavMesh.CalculateTriangulation().indices.Length, Is.EqualTo(originalIndices));
            CollectionAssert.AreEquivalent(originalData, Resources.FindObjectsOfTypeAll<NavMeshData>());

            navigation.enabled = false;
            Assert.That(navigation.IsReady, Is.False);
            navigation.enabled = true;
            Assert.That(navigation.IsReady, Is.True);
            CollectionAssert.AreEquivalent(originalData, Resources.FindObjectsOfTypeAll<NavMeshData>());
            Assert.That(NavMesh.CalculateTriangulation().indices.Length, Is.EqualTo(originalIndices));
        }

        [Test]
        public void CompleteRouteGoesAroundCameraCollisionWall()
        {
            var path = new NavMeshPath();
            var corners = new Vector3[32];
            Vector3 from = new Vector3(0f, 0f, -6f);
            Vector3 to = Vector3.zero;

            Assert.That(navigation.TryPath(from, to, path, corners, out int count), Is.True);
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
            Assert.That(count, Is.GreaterThan(2));
            Assert.That(Vector3.Distance(corners[0], from), Is.LessThanOrEqualTo(0.75f));
            Assert.That(Vector3.Distance(corners[count - 1], to), Is.LessThanOrEqualTo(0.75f));

            Collider wall = GameObject.Find("Camera Collision Wall").GetComponent<Collider>();
            Bounds clearanceBounds = wall.bounds;
            clearanceBounds.Expand(new Vector3(0.7f, 0f, 0.7f));
            bool passedWallEnd = false;

            for (int index = 0; index < count; index++)
            {
                passedWallEnd |= Mathf.Abs(corners[index].x - wall.bounds.center.x) >
                                 wall.bounds.extents.x + 0.35f;
                Assert.That(corners[index].y, Is.LessThan(0.6f));

                if (index == 0)
                {
                    continue;
                }

                Vector3 segment = corners[index] - corners[index - 1];
                bool intersects = clearanceBounds.IntersectRay(
                    new Ray(corners[index - 1], segment.normalized), out float distance);
                Assert.That(intersects && distance <= segment.magnitude, Is.False,
                    "Route segments must leave room for the player's collision radius.");
            }

            Assert.That(passedWallEnd, Is.True);
        }

        [Test]
        public void ClearFloorSamplesAndExactFitRoutesAreReachable()
        {
            Vector3[] clearPoints =
            {
                new Vector3(-14f, 0f, -12f),
                new Vector3(14f, 0f, -12f),
                new Vector3(14f, 0f, 14f),
                new Vector3(-14f, 0f, 14f)
            };
            var path = new NavMeshPath();
            var corners = new Vector3[32];

            for (int index = 0; index < clearPoints.Length; index++)
            {
                Vector3 point = clearPoints[index];
                Assert.That(navigation.TrySample(point, 0.5f, out Vector3 sampled), Is.True);
                Assert.That(Vector3.Distance(sampled, point), Is.LessThanOrEqualTo(0.5f));
                Assert.That(navigation.TryPath(
                    point, clearPoints[(index + 1) % clearPoints.Length], path, corners, out int count), Is.True);
                Assert.That(count, Is.GreaterThanOrEqualTo(2));
            }

            Assert.That(navigation.TryPath(
                new Vector3(12f, 0f, -12f),
                new Vector3(14f, 0f, -12f),
                path,
                new Vector3[2],
                out int exactFitCount), Is.True);
            Assert.That(exactFitCount, Is.EqualTo(2));
        }

        [Test]
        public void UnreachableAndDifferentFloorGoalsDoNotProduceRoutes()
        {
            var path = new NavMeshPath();
            var corners = new Vector3[32];
            Vector3 distant = new Vector3(1000f, 0f, 1000f);

            Assert.That(navigation.TrySample(distant, 2f, out _), Is.False);
            Assert.That(navigation.TryPath(Vector3.zero, distant, path, corners, out int count), Is.False);
            Assert.That(count, Is.Zero);
            Assert.That(navigation.TrySample(new Vector3(14f, 5f, -12f), 10f, out _), Is.False,
                "A wide search must not snap an airborne or upper-floor target down to ground level.");
            Assert.That(navigation.TrySample(new Vector3(-7f, 0f, -3f), 0.4f, out _), Is.False,
                "The slide tunnel does not provide standing-height clearance.");

            Vector3 platform = new Vector3(11f, 2.75f, 2f);
            Assert.That(navigation.TrySample(platform, 0.4f, out _), Is.True);
            Assert.That(navigation.TryPath(Vector3.zero, platform, path, corners, out count), Is.False,
                "A disconnected platform must not return a partial path as a usable route.");
            Assert.That(count, Is.Zero);
        }

        [Test]
        public void TruncatedPathsAndInvalidArgumentsAreRejected()
        {
            var path = new NavMeshPath();
            var corners = new Vector3[32];
            Vector3 from = new Vector3(0f, 0f, -6f);

            Assert.That(navigation.TryPath(from, Vector3.zero, path, corners, out int completeCount), Is.True);
            Assert.That(completeCount, Is.GreaterThan(2));
            Assert.That(navigation.TryPath(
                from, Vector3.zero, path, new Vector3[2], out int truncatedCount), Is.False);
            Assert.That(truncatedCount, Is.Zero);
            Assert.That(navigation.TryPath(from, Vector3.zero, path, corners, out _), Is.True,
                "A reusable path must recover after a rejected route.");

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                navigation.TrySample(Vector3.zero, 0f, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                navigation.TrySample(Vector3.zero, -1f, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                navigation.TrySample(Vector3.zero, float.PositiveInfinity, out _));
            Assert.Throws<ArgumentException>(() =>
                navigation.TrySample(new Vector3(float.NaN, 0f, 0f), 1f, out _));
            Assert.Throws<ArgumentException>(() =>
                navigation.TryPath(from, new Vector3(0f, float.PositiveInfinity, 0f), path, corners, out _));
            Assert.Throws<ArgumentNullException>(() =>
                navigation.TryPath(from, Vector3.zero, null, corners, out _));
            Assert.Throws<ArgumentNullException>(() =>
                navigation.TryPath(from, Vector3.zero, path, null, out _));
            Assert.Throws<ArgumentException>(() =>
                navigation.TryPath(from, Vector3.zero, path, new Vector3[1], out _));
        }

        [UnityTest]
        public IEnumerator ReloadAndTeardownDoNotAccumulateOrRemoveUnownedNavigation()
        {
            int expectedIndices = NavMesh.CalculateTriangulation().indices.Length;
            int expectedDataCount = Resources.FindObjectsOfTypeAll<NavMeshData>().Length;

            for (int index = 0; index < 2; index++)
            {
                yield return ReloadSandbox();
                Assert.That(NavMesh.CalculateTriangulation().indices.Length, Is.EqualTo(expectedIndices));
                Assert.That(Resources.FindObjectsOfTypeAll<NavMeshData>().Length, Is.EqualTo(expectedDataCount));
            }

            Vector3 independentPosition = new Vector3(100f, 0f, 0f);
            NavMeshData independentData = BuildIndependentFloor(independentPosition);
            NavMeshDataInstance independentInstance = default;

            try
            {
                independentInstance = NavMesh.AddNavMeshData(independentData);
                Assert.That(independentInstance.valid, Is.True);
                int independentIndices = NavMesh.CalculateTriangulation().indices.Length - expectedIndices;
                Assert.That(independentIndices, Is.GreaterThan(0));

                yield return UnloadIntoEmptyScene(navigation.gameObject.scene);
                Assert.That(navigation == null, Is.True);
                Assert.That(NavMesh.SamplePosition(Vector3.zero, out _, 0.5f, NavMesh.AllAreas), Is.False);
                Assert.That(independentInstance.valid, Is.True);
                Assert.That(NavMesh.SamplePosition(
                    independentPosition, out _, 0.5f, NavMesh.AllAreas), Is.True);
                Assert.That(NavMesh.CalculateTriangulation().indices.Length, Is.EqualTo(independentIndices));
                Assert.That(Resources.FindObjectsOfTypeAll<NavMeshData>().Length, Is.EqualTo(expectedDataCount),
                    "The destroyed scene's runtime data must be released, leaving only the independent replacement.");
            }
            finally
            {
                if (independentInstance.valid)
                {
                    independentInstance.Remove();
                }

                Object.Destroy(independentData);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator MissingArenaGeometryFailsExplicitly()
        {
            yield return UnloadIntoEmptyScene(navigation.gameObject.scene);
            var emptyObject = new GameObject("Navigation Without Arena");
            SandboxNavigation emptyNavigation = emptyObject.AddComponent<SandboxNavigation>();

            try
            {
                Assert.Throws<InvalidOperationException>(() => emptyNavigation.EnsureBuilt());
                Assert.That(emptyNavigation.IsReady, Is.False);
                Assert.That(emptyNavigation.SourceCount, Is.Zero);
            }
            finally
            {
                emptyNavigation.enabled = false;
                Object.Destroy(emptyObject);
            }

            yield return null;
        }

        private IEnumerator ReloadSandbox()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(SandboxScenePath, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;
            Physics.SyncTransforms();

            SandboxNavigation[] adapters =
                Object.FindObjectsByType<SandboxNavigation>(FindObjectsSortMode.None);
            Assert.That(adapters, Has.Length.EqualTo(1), "The sandbox must author one shared navigation adapter.");
            navigation = adapters[0];
            navigation.EnsureBuilt();
            Assert.That(navigation.IsReady, Is.True);
        }

        private static IEnumerator UnloadIntoEmptyScene(Scene scene)
        {
            Scene empty = SceneManager.CreateScene("SandboxNavigationTests_Empty");
            SceneManager.SetActiveScene(empty);
            AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
            Assert.That(unload, Is.Not.Null);
            yield return unload;
            yield return null;
        }

        private static NavMeshData BuildIndependentFloor(Vector3 position)
        {
            var sources = new List<NavMeshBuildSource>
            {
                new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Box,
                    size = new Vector3(8f, 0.5f, 8f),
                    transform = Matrix4x4.TRS(
                        position + Vector3.down * 0.25f, Quaternion.identity, Vector3.one),
                    area = 0
                }
            };
            NavMeshData result = NavMeshBuilder.BuildNavMeshData(
                NavMesh.GetSettingsByIndex(0),
                sources,
                new Bounds(position, new Vector3(10f, 8f, 10f)),
                Vector3.zero,
                Quaternion.identity);
            Assert.That(result, Is.Not.Null);
            result.name = "Independent Navigation Test Floor";
            return result;
        }
    }
}
