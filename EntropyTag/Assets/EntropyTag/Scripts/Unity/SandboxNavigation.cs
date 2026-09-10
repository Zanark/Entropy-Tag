using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace EntropyTag.UnityAdapters
{
    [DefaultExecutionOrder(-300)]
    [DisallowMultipleComponent]
    public sealed class SandboxNavigation : MonoBehaviour
    {
        private const float AgentRadius = 0.45f;
        private const float AgentHeight = 2f;
        private const float AgentClimb = 0.3f;
        private const float AgentSlope = 45f;
        private const float EndpointSampleDistance = 0.75f;
        private const float MaximumVerticalSampleDistance = 0.6f;

        [SerializeField]
        private Bounds buildBounds = new Bounds(
            new Vector3(0f, 4f, 0f),
            new Vector3(44f, 16f, 44f));

        private NavMeshData data;
        private NavMeshDataInstance instance;
        private NavMeshQueryFilter queryFilter;
        private Vector3[] cornerScratch = new Vector3[33];
        private bool buildAttempted;

        public bool IsReady => data != null && instance.valid;

        public int SourceCount { get; private set; }

        public void EnsureBuilt()
        {
            if (IsReady)
            {
                return;
            }

            if (!isActiveAndEnabled)
            {
                throw new InvalidOperationException("Sandbox navigation must be active before it can be built.");
            }

            if (data != null)
            {
                RegisterOwnedData();
                return;
            }

            if (buildAttempted)
            {
                throw new InvalidOperationException(
                    "Sandbox navigation previously failed to build. Reload the scene after correcting its geometry.");
            }

            buildAttempted = true;
            ValidateBounds();
            Physics.SyncTransforms();

            List<NavMeshBuildSource> sources = CollectArenaSources();
            SourceCount = sources.Count;

            if (SourceCount == 0)
            {
                throw new InvalidOperationException(
                    "Sandbox navigation requires enabled, static TerritorySurface source colliders in its scene.");
            }

            if (NavMesh.GetSettingsCount() == 0)
            {
                throw new InvalidOperationException("Sandbox navigation requires a configured NavMesh agent type.");
            }

            NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = AgentRadius;
            settings.agentHeight = AgentHeight;
            settings.agentClimb = AgentClimb;
            settings.agentSlope = AgentSlope;
            settings.overrideVoxelSize = true;
            settings.voxelSize = 0.1f;
            settings.overrideTileSize = true;
            settings.tileSize = 256;
            queryFilter = new NavMeshQueryFilter
            {
                agentTypeID = settings.agentTypeID,
                areaMask = NavMesh.AllAreas
            };

            try
            {
                data = NavMeshBuilder.BuildNavMeshData(
                    settings, sources, buildBounds, Vector3.zero, Quaternion.identity);

                if (data == null)
                {
                    throw new InvalidOperationException("Sandbox navigation did not produce NavMesh data.");
                }

                data.name = $"{gameObject.name} Runtime NavMesh";
                int previousIndexCount = NavMesh.CalculateTriangulation().indices.Length;
                RegisterOwnedData();

                if (NavMesh.CalculateTriangulation().indices.Length <= previousIndexCount)
                {
                    throw new InvalidOperationException(
                        "Sandbox navigation did not produce any walkable polygons for the player-sized agent.");
                }
            }
            catch
            {
                ReleaseOwnedData();
                throw;
            }
        }

        /// <summary>
        /// Samples a feet/ground position, with at most 0.6 metres of vertical displacement.
        /// A wider search radius never permits snapping to a remote floor.
        /// </summary>
        public bool TrySample(Vector3 desired, float maxDistance, out Vector3 point)
        {
            point = default;
            ValidatePoint(desired, nameof(desired));

            if (!IsFinite(maxDistance) || maxDistance <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxDistance), "Sampling distance must be finite and greater than zero.");
            }

            EnsureBuilt();

            if (!NavMesh.SamplePosition(desired, out NavMeshHit hit, maxDistance, queryFilter) ||
                Mathf.Abs(hit.position.y - desired.y) > MaximumVerticalSampleDistance ||
                !buildBounds.Contains(hit.position))
            {
                return false;
            }

            point = hit.position;
            return true;
        }

        /// <summary>
        /// Computes route guidance only; callers move their CharacterController themselves.
        /// Endpoints are feet positions sampled within 0.75 metres. Partial paths and buffers
        /// too small for the entire route return false with count zero.
        /// </summary>
        public bool TryPath(
            Vector3 from,
            Vector3 to,
            NavMeshPath reusablePath,
            Vector3[] corners,
            out int count)
        {
            count = 0;
            ValidatePoint(from, nameof(from));
            ValidatePoint(to, nameof(to));

            if (reusablePath == null)
            {
                throw new ArgumentNullException(nameof(reusablePath));
            }

            if (corners == null)
            {
                throw new ArgumentNullException(nameof(corners));
            }

            if (corners.Length < 2)
            {
                throw new ArgumentException("A path buffer must hold at least two corners.", nameof(corners));
            }

            reusablePath.ClearCorners();

            if (!TrySample(from, EndpointSampleDistance, out Vector3 start) ||
                !TrySample(to, EndpointSampleDistance, out Vector3 goal))
            {
                return false;
            }

            if (!NavMesh.CalculatePath(start, goal, queryFilter, reusablePath) ||
                reusablePath.status != NavMeshPathStatus.PathComplete)
            {
                reusablePath.ClearCorners();
                return false;
            }

            // One spare slot distinguishes an exact-fit path from a truncated result.
            if (cornerScratch.Length <= corners.Length)
            {
                cornerScratch = new Vector3[corners.Length + 1];
            }

            int cornerCount = reusablePath.GetCornersNonAlloc(cornerScratch);

            if (cornerCount == 0 || cornerCount > corners.Length)
            {
                reusablePath.ClearCorners();
                return false;
            }

            Array.Copy(cornerScratch, corners, cornerCount);
            count = cornerCount;
            return true;
        }

        private void Start()
        {
            EnsureBuilt();
        }

        private void OnEnable()
        {
            if (data != null)
            {
                RegisterOwnedData();
            }
        }

        private void OnDisable()
        {
            if (instance.valid)
            {
                instance.Remove();
            }
        }

        private void OnDestroy()
        {
            ReleaseOwnedData();
        }

        private List<NavMeshBuildSource> CollectArenaSources()
        {
            var arenaColliders = new HashSet<Collider>();
            TerritorySurface[] surfaces = FindObjectsByType<TerritorySurface>(FindObjectsSortMode.None);

            for (int index = 0; index < surfaces.Length; index++)
            {
                TerritorySurface surface = surfaces[index];
                Collider collider = surface.SourceCollider;

                if (surface.isActiveAndEnabled &&
                    surface.gameObject.scene == gameObject.scene &&
                    collider != null &&
                    collider.gameObject.scene == gameObject.scene &&
                    collider.enabled &&
                    collider.gameObject.activeInHierarchy &&
                    !collider.isTrigger &&
                    !(collider is CharacterController) &&
                    collider.attachedRigidbody == null &&
                    collider.GetComponentInParent<ThirdPersonMotor>() == null)
                {
                    arenaColliders.Add(collider);
                }
            }

            var sources = new List<NavMeshBuildSource>(arenaColliders.Count);
            NavMeshBuilder.CollectSources(
                buildBounds,
                ~0,
                NavMeshCollectGeometry.PhysicsColliders,
                0,
                new List<NavMeshBuildMarkup>(),
                sources);

            for (int index = sources.Count - 1; index >= 0; index--)
            {
                if (!(sources[index].component is Collider collider) || !arenaColliders.Contains(collider))
                {
                    sources.RemoveAt(index);
                }
            }

            return sources;
        }

        private void RegisterOwnedData()
        {
            if (instance.valid)
            {
                return;
            }

            instance = NavMesh.AddNavMeshData(data);

            if (!instance.valid)
            {
                throw new InvalidOperationException("Sandbox navigation could not register its NavMesh data.");
            }

            instance.owner = this;
        }

        private void ReleaseOwnedData()
        {
            if (instance.valid)
            {
                instance.Remove();
            }

            if (data != null)
            {
                Destroy(data);
                data = null;
            }
        }

        private void ValidateBounds()
        {
            ValidatePoint(buildBounds.center, nameof(buildBounds));
            ValidatePoint(buildBounds.size, nameof(buildBounds));

            if (buildBounds.size.x <= 0f || buildBounds.size.y <= 0f || buildBounds.size.z <= 0f)
            {
                throw new ArgumentException("Navigation build bounds must have positive size.", nameof(buildBounds));
            }
        }

        private static void ValidatePoint(Vector3 point, string argumentName)
        {
            if (!IsFinite(point.x) || !IsFinite(point.y) || !IsFinite(point.z))
            {
                throw new ArgumentException("Navigation positions must have finite coordinates.", argumentName);
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
