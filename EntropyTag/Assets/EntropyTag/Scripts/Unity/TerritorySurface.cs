using System;
using System.Collections.Generic;
using EntropyTag.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace EntropyTag.UnityAdapters
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class TerritorySurface : MonoBehaviour
    {
        private static readonly Color32 NeutralColor = new Color32(75, 75, 80, 0);
        private const int EstimatedLogicalCellAndVisitBytes = 36;
        private const int BytesPerVisualPixel = 4;

        [SerializeField]
        private Collider sourceCollider;

        [SerializeField]
        private Renderer targetRenderer;

        [SerializeField]
        private ElementReactionPresentationConfig presentationConfig;

        [SerializeField]
        private int logicalWidth = 64;

        [SerializeField]
        private int logicalHeight = 64;

        [SerializeField]
        private int visualResolution = 256;

        private readonly List<TerritoryCoordinate> stampCoordinates =
            new List<TerritoryCoordinate>(128);
        private DomainRulesConfig config;
        private TerritoryField field;
        private Texture2D visualTexture;
        private Material runtimeMaterial;
        private Mesh runtimeMesh;
        private Color32[] visualPixels;
        private bool visualDirty;
        private int iceBank;
        private int fireBank;

        public Collider SourceCollider => sourceCollider;

        public ElementReactionPresentationConfig PresentationConfig => presentationConfig;

        public int LogicalWidth => logicalWidth;

        public int LogicalHeight => logicalHeight;

        public int VisualResolution => visualResolution;

        public int IceBank => iceBank;

        public int FireBank => fireBank;

        public int IceMistCreatedCells { get; private set; }

        public int FireMistCreatedCells { get; private set; }

        public int IceMistClaimedCells { get; private set; }

        public int FireMistClaimedCells { get; private set; }

        public bool CountsForMatchScore => Vector3.Dot(transform.forward, Vector3.up) >= 0.5f;

        public Texture2D VisualTexture => visualTexture;

        public int EstimatedCpuBytes =>
            logicalWidth * logicalHeight * EstimatedLogicalCellAndVisitBytes +
            visualResolution * visualResolution * BytesPerVisualPixel * 2 +
            stampCoordinates.Capacity * 8;

        public int EstimatedGpuBytes =>
            visualResolution * visualResolution * BytesPerVisualPixel;

        public void Configure(
            Collider colliderToUse,
            Renderer rendererToUse,
            ElementReactionPresentationConfig reactions,
            int width,
            int height,
            int resolution)
        {
            if (width <= 0 || height <= 0 || resolution <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(width),
                    "Logical and visual territory dimensions must be greater than zero.");
            }

            sourceCollider = colliderToUse != null
                ? colliderToUse
                : throw new ArgumentNullException(nameof(colliderToUse));
            targetRenderer = rendererToUse != null
                ? rendererToUse
                : throw new ArgumentNullException(nameof(rendererToUse));
            presentationConfig = reactions != null
                ? reactions
                : throw new ArgumentNullException(nameof(reactions));
            logicalWidth = width;
            logicalHeight = height;
            visualResolution = resolution;

            if (UnityEngine.Application.isPlaying)
            {
                Initialize();
            }
        }

        public bool MatchesImpact(Collider collider, Vector3 worldPoint, Vector3 worldNormal)
        {
            return collider == sourceCollider &&
                   Vector3.Dot(transform.forward, worldNormal.normalized) >= 0.75f &&
                   TryWorldToCoordinate(worldPoint, out _);
        }

        public StampResult ApplyWorldStamp(
            Vector3 worldPoint,
            float radius,
            ElementId element,
            TeamId applyingTeam,
            Vector3 worldNormal = default,
            CircularArenaBoundary? activeBoundary = null)
        {
            EnsureInitialized();

            if (radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), "Stamp radius must be finite and positive.");
            }

            if (!TryWorldToCoordinate(worldPoint, out TerritoryCoordinate center))
            {
                return default;
            }

            TerritoryState previousState = field.GetCell(center).State;
            float worldWidth = Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.x));
            float worldHeight = Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.y));
            int radiusX = Mathf.Max(1, Mathf.CeilToInt(radius / worldWidth * logicalWidth));
            int radiusY = Mathf.Max(1, Mathf.CeilToInt(radius / worldHeight * logicalHeight));
            stampCoordinates.Clear();

            for (int y = center.Y - radiusY; y <= center.Y + radiusY; y++)
            {
                if (y < 0 || y >= logicalHeight)
                {
                    continue;
                }

                for (int x = center.X - radiusX; x <= center.X + radiusX; x++)
                {
                    if (x < 0 || x >= logicalWidth)
                    {
                        continue;
                    }

                    float normalizedX = (x - center.X) / (float)radiusX;
                    float normalizedY = (y - center.Y) / (float)radiusY;

                    if (normalizedX * normalizedX + normalizedY * normalizedY <= 1f)
                    {
                        var coordinate = new TerritoryCoordinate(x, y);
                        if (activeBoundary.HasValue)
                        {
                            Vector3 cellPoint = GetWorldPoint(coordinate);
                            if (!activeBoundary.Value.Contains(cellPoint.x, cellPoint.z))
                            {
                                continue;
                            }
                        }

                        stampCoordinates.Add(coordinate);
                    }
                }
            }

            StampResult result = ApplyLogicalStamp(stampCoordinates, element, applyingTeam);
            TerritoryState currentState = field.GetCell(center).State;

            if (previousState != currentState)
            {
                TerritorySurfaceRegistry.PublishReaction(
                    new TerritoryReactionEvent(
                        GetReactionKind(previousState, currentState),
                        this,
                        worldPoint,
                        worldNormal.sqrMagnitude > 0.0001f
                            ? worldNormal.normalized
                            : transform.forward,
                        previousState,
                        currentState,
                        element,
                        result.BankAward));
            }

            return result;
        }

        public StampResult ApplyLogicalStamp(
            IReadOnlyList<TerritoryCoordinate> coordinates,
            ElementId element,
            TeamId applyingTeam)
        {
            EnsureInitialized();
            StampResult result = field.ApplyStamp(coordinates, element, applyingTeam);

            if (applyingTeam == config.IceTeam.Id)
            {
                iceBank += result.BankAward;
                IceMistCreatedCells += result.MistCreatedCells;
                IceMistClaimedCells += result.MistClaimedCells;
            }
            else if (applyingTeam == config.FireTeam.Id)
            {
                fireBank += result.BankAward;
                FireMistCreatedCells += result.MistCreatedCells;
                FireMistClaimedCells += result.MistClaimedCells;
            }
            else
            {
                throw new ArgumentException($"Unknown territory team '{applyingTeam}'.", nameof(applyingTeam));
            }

            for (int index = 0; index < coordinates.Count; index++)
            {
                TerritoryCoordinate coordinate = coordinates[index];

                if (coordinate.X >= 0 &&
                    coordinate.X < logicalWidth &&
                    coordinate.Y >= 0 &&
                    coordinate.Y < logicalHeight)
                {
                    WriteVisualCell(coordinate, field.GetCell(coordinate).State);
                }
            }

            visualDirty = true;
            return result;
        }

        public bool TryWorldToCoordinate(Vector3 worldPoint, out TerritoryCoordinate coordinate)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);

            if (local.x < -0.5001f ||
                local.x > 0.5001f ||
                local.y < -0.5001f ||
                local.y > 0.5001f ||
                Mathf.Abs(local.z) > 0.05f)
            {
                coordinate = default;
                return false;
            }

            int x = Mathf.Clamp(Mathf.FloorToInt((local.x + 0.5f) * logicalWidth), 0, logicalWidth - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt((local.y + 0.5f) * logicalHeight), 0, logicalHeight - 1);
            coordinate = new TerritoryCoordinate(x, y);
            return true;
        }

        public bool TrySampleWorldPoint(Vector3 worldPoint, out TerritoryCell cell)
        {
            EnsureInitialized();

            if (!TryWorldToCoordinate(worldPoint, out TerritoryCoordinate coordinate))
            {
                cell = default;
                return false;
            }

            cell = field.GetCell(coordinate);
            return true;
        }

        public TerritoryCell GetCell(TerritoryCoordinate coordinate)
        {
            EnsureInitialized();
            return field.GetCell(coordinate);
        }

        public Vector3 GetWorldPoint(TerritoryCoordinate coordinate)
        {
            if (coordinate.X < 0 || coordinate.X >= logicalWidth ||
                coordinate.Y < 0 || coordinate.Y >= logicalHeight)
            {
                throw new ArgumentOutOfRangeException(nameof(coordinate), "Cell is outside this territory face.");
            }

            return transform.TransformPoint(new Vector3(
                (coordinate.X + 0.5f) / logicalWidth - 0.5f,
                (coordinate.Y + 0.5f) / logicalHeight - 0.5f,
                0f));
        }

        public void CountActiveCells(
            CircularArenaBoundary boundary,
            ref int total,
            ref int neutral,
            ref int mist,
            ref int ice,
            ref int fire)
        {
            if (!CountsForMatchScore)
            {
                return;
            }

            EnsureInitialized();
            Matrix4x4 localToWorld = transform.localToWorldMatrix;
            for (int y = 0; y < logicalHeight; y++)
            {
                for (int x = 0; x < logicalWidth; x++)
                {
                    Vector3 point = localToWorld.MultiplyPoint3x4(new Vector3(
                        (x + 0.5f) / logicalWidth - 0.5f,
                        (y + 0.5f) / logicalHeight - 0.5f,
                        0f));
                    if (!boundary.Contains(point.x, point.z))
                    {
                        continue;
                    }

                    total++;
                    TerritoryCell cell = field.GetCell(new TerritoryCoordinate(x, y));
                    switch (cell.State)
                    {
                        case TerritoryState.Neutral:
                            neutral++;
                            break;
                        case TerritoryState.Mist:
                            mist++;
                            break;
                        case TerritoryState.Ice:
                            ice++;
                            break;
                        case TerritoryState.Fire:
                            fire++;
                            break;
                        default:
                            throw new InvalidOperationException($"Unsupported scoring state {cell.State}.");
                    }
                }
            }
        }

        public CoverageSnapshot GetCoverage()
        {
            EnsureInitialized();
            return TerritoryCoverageCalculator.Calculate(
                field,
                new[] { config.IceTeam.Id, config.FireTeam.Id });
        }

        public Color32 GetVisualColor(TerritoryCoordinate coordinate)
        {
            EnsureInitialized();
            int x = Mathf.Clamp(
                Mathf.FloorToInt((coordinate.X + 0.5f) / logicalWidth * visualResolution),
                0,
                visualResolution - 1);
            int y = Mathf.Clamp(
                Mathf.FloorToInt((coordinate.Y + 0.5f) / logicalHeight * visualResolution),
                0,
                visualResolution - 1);
            return visualPixels[y * visualResolution + x];
        }

        public Color32 GetRenderedColorAtWorldPoint(Vector3 worldPoint)
        {
            EnsureInitialized();
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            int x = Mathf.Clamp(
                Mathf.FloorToInt((local.x + 0.5f) * visualResolution),
                0,
                visualResolution - 1);
            int y = Mathf.Clamp(
                Mathf.FloorToInt((local.y + 0.5f) * visualResolution),
                0,
                visualResolution - 1);
            return visualTexture.GetPixel(x, y);
        }

        public void FlushVisuals()
        {
            if (!visualDirty || visualTexture == null)
            {
                return;
            }

            visualTexture.SetPixels32(visualPixels);
            visualTexture.Apply(false, false);
            visualDirty = false;
        }

        public void ResetTerritory()
        {
            EnsureInitialized();
            field.Reset();
            iceBank = 0;
            fireBank = 0;
            IceMistCreatedCells = 0;
            FireMistCreatedCells = 0;
            IceMistClaimedCells = 0;
            FireMistClaimedCells = 0;

            for (int index = 0; index < visualPixels.Length; index++)
            {
                visualPixels[index] = NeutralColor;
            }

            visualDirty = true;
            FlushVisuals();
        }

        private void Awake()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<Renderer>();
            }

            Initialize();
        }

        private void OnEnable()
        {
            TerritorySurfaceRegistry.Register(this);
        }

        private void OnDisable()
        {
            TerritorySurfaceRegistry.Unregister(this);
        }

        private void LateUpdate()
        {
            FlushVisuals();
        }

        private void OnDestroy()
        {
            if (visualTexture != null)
            {
                Destroy(visualTexture);
            }

            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
            }

            if (runtimeMesh != null)
            {
                Destroy(runtimeMesh);
            }
        }

        private void Initialize()
        {
            if (field != null &&
                field.Width == logicalWidth &&
                field.Height == logicalHeight &&
                visualTexture != null &&
                visualTexture.width == visualResolution)
            {
                return;
            }

            if (sourceCollider == null)
            {
                throw new InvalidOperationException($"{name} requires a source collider.");
            }

            if (presentationConfig == null)
            {
                throw new InvalidOperationException($"{name} requires element reaction presentation configuration.");
            }

            config = DomainRulesConfig.CreateFirstSlice();
            field = new TerritoryField(logicalWidth, logicalHeight, config.Resolver);
            visualPixels = new Color32[visualResolution * visualResolution];
            CreateRuntimeMesh();

            if (visualTexture != null)
            {
                Destroy(visualTexture);
            }

            visualTexture = new Texture2D(
                visualResolution,
                visualResolution,
                TextureFormat.RGBA32,
                false,
                true)
            {
                name = $"{name} Territory Mask",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
            }

            Shader shader = presentationConfig.TerritoryShader != null
                ? presentationConfig.TerritoryShader
                : Shader.Find("EntropyTag/TerritoryOverlay");

            if (shader == null)
            {
                throw new InvalidOperationException("No supported territory shader is available.");
            }

            runtimeMaterial = new Material(shader)
            {
                name = $"{name} Territory Material",
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = visualTexture
            };

            if (runtimeMaterial.HasProperty("_BaseMap"))
            {
                runtimeMaterial.SetTexture("_BaseMap", visualTexture);
            }

            if (runtimeMaterial.HasProperty("_Cull"))
            {
                runtimeMaterial.SetFloat("_Cull", (float)CullMode.Off);
            }

            targetRenderer.sharedMaterial = runtimeMaterial;
            ResetTerritory();
        }

        private void CreateRuntimeMesh()
        {
            if (runtimeMesh != null)
            {
                Destroy(runtimeMesh);
            }

            runtimeMesh = new Mesh
            {
                name = $"{name} Territory Quad",
                hideFlags = HideFlags.HideAndDontSave,
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f),
                    new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f),
                    new Vector3(0.5f, 0.5f, 0f)
                },
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f)
                },
                triangles = new[] { 0, 1, 2, 2, 1, 3 },
                normals = new[]
                {
                    Vector3.forward,
                    Vector3.forward,
                    Vector3.forward,
                    Vector3.forward
                }
            };
            runtimeMesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = runtimeMesh;
        }

        private void EnsureInitialized()
        {
            if (field == null || visualTexture == null)
            {
                Initialize();
            }
        }

        private void WriteVisualCell(TerritoryCoordinate coordinate, TerritoryState state)
        {
            int startX = coordinate.X * visualResolution / logicalWidth;
            int endX = (coordinate.X + 1) * visualResolution / logicalWidth;
            int startY = coordinate.Y * visualResolution / logicalHeight;
            int endY = (coordinate.Y + 1) * visualResolution / logicalHeight;
            Color32 color = GetStateColor(state);

            for (int y = startY; y < endY; y++)
            {
                int row = y * visualResolution;

                for (int x = startX; x < endX; x++)
                {
                    visualPixels[row + x] = color;
                }
            }
        }

        private Color32 GetStateColor(TerritoryState state)
        {
            switch (state)
            {
                case TerritoryState.Neutral:
                    return NeutralColor;
                case TerritoryState.Ice:
                    return WithPatternTag(
                        presentationConfig.GetTerritoryColor(state),
                        GetPatternTag(presentationConfig.Ice.Pattern));
                case TerritoryState.Fire:
                    return WithPatternTag(
                        presentationConfig.GetTerritoryColor(state),
                        GetPatternTag(presentationConfig.Fire.Pattern));
                case TerritoryState.Mist:
                    return WithPatternTag(
                        presentationConfig.GetTerritoryColor(state),
                        GetPatternTag(presentationConfig.Mist.Pattern));
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        private static Color32 WithPatternTag(Color32 color, byte patternTag)
        {
            color.a = patternTag;
            return color;
        }

        private static byte GetPatternTag(TerritoryPattern pattern)
        {
            return (byte)((int)pattern * 64);
        }

        private static TerritoryReactionKind GetReactionKind(
            TerritoryState previousState,
            TerritoryState currentState)
        {
            if (currentState == TerritoryState.Mist)
            {
                return TerritoryReactionKind.MistCreated;
            }

            return previousState == TerritoryState.Mist
                ? TerritoryReactionKind.MistClaimed
                : TerritoryReactionKind.TerritoryClaimed;
        }
    }
}
