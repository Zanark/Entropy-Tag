using System;
using System.Collections.Generic;
using EntropyTag.Domain;
using UnityEngine;

namespace EntropyTag.UnityAdapters
{
    public readonly struct BotPaintLocation
    {
        public BotPaintLocation(TerritorySurface surface, TerritoryCoordinate coordinate, Vector3 point, Vector3 navigationPoint)
        {
            Surface = surface;
            Coordinate = coordinate;
            Point = point;
            NavigationPoint = navigationPoint;
        }

        public TerritorySurface Surface { get; }
        public TerritoryCoordinate Coordinate { get; }
        public Vector3 Point { get; }
        public Vector3 NavigationPoint { get; }
        public TerritoryCell Cell => Surface.GetCell(Coordinate);
    }

    public sealed class BotTerritoryMap : MonoBehaviour
    {
        [SerializeField] private SandboxNavigation navigation;
        private readonly List<BotPaintLocation> locations = new List<BotPaintLocation>(2048);

        public int Count => locations.Count;
        public BotPaintLocation GetLocation(int index) => locations[index];

        public void Configure(SandboxNavigation arenaNavigation)
        {
            navigation = arenaNavigation != null ? arenaNavigation : throw new ArgumentNullException(nameof(arenaNavigation));
        }

        public void EnsureBuilt()
        {
            if (locations.Count != 0)
            {
                return;
            }

            if (navigation == null)
            {
                throw new InvalidOperationException($"{name}: bot territory map needs arena navigation.");
            }

            navigation.EnsureBuilt();
            var surfaces = new List<TerritorySurface>(TerritorySurfaceRegistry.Count);
            for (int index = 0; index < TerritorySurfaceRegistry.Count; index++)
            {
                TerritorySurface surface = TerritorySurfaceRegistry.GetSurface(index);
                if (surface.CountsForMatchScore)
                {
                    surfaces.Add(surface);
                }
            }

            surfaces.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
            foreach (TerritorySurface surface in surfaces)
            {
                for (int y = 1; y < surface.LogicalHeight; y += 2)
                {
                    for (int x = 1; x < surface.LogicalWidth; x += 2)
                    {
                        var coordinate = new TerritoryCoordinate(x, y);
                        Vector3 point = surface.GetWorldPoint(coordinate);
                        if (!Physics.Raycast(point + Vector3.up * 0.35f, Vector3.down, out RaycastHit hit, 0.7f,
                                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                            hit.collider != surface.SourceCollider ||
                            !navigation.TrySample(point, 0.7f, out Vector3 sampled) ||
                            Mathf.Abs(sampled.y - point.y) > 0.3f)
                        {
                            continue;
                        }

                        locations.Add(new BotPaintLocation(surface, coordinate, point, sampled));
                    }
                }
            }

            if (locations.Count == 0)
            {
                throw new InvalidOperationException($"{name}: no reachable scoring surfaces were found for bots.");
            }
        }
    }
}
