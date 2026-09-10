using System.Collections.Generic;
using System;
using EntropyTag.Application;
using EntropyTag.Domain;
using UnityEngine;

namespace EntropyTag.UnityAdapters
{
    public static class TerritorySurfaceRegistry
    {
        private static readonly List<TerritorySurface> Surfaces = new List<TerritorySurface>();
        private static readonly TeamId IceTeam = new TeamId(1);
        private static readonly TeamId FireTeam = new TeamId(2);

        public static int Count => Surfaces.Count;

        public static TerritorySurface GetSurface(int index) => Surfaces[index];

        public static event Action<TerritoryReactionEvent> ReactionOccurred;

        public static int IceBank
        {
            get
            {
                int total = 0;

                for (int index = 0; index < Surfaces.Count; index++)
                {
                    total += Surfaces[index].IceBank;
                }

                return total;
            }
        }

        public static int FireBank
        {
            get
            {
                int total = 0;

                for (int index = 0; index < Surfaces.Count; index++)
                {
                    total += Surfaces[index].FireBank;
                }

                return total;
            }
        }

        public static void Register(TerritorySurface surface)
        {
            if (surface != null && !Surfaces.Contains(surface))
            {
                Surfaces.Add(surface);
            }
        }

        public static void Unregister(TerritorySurface surface)
        {
            Surfaces.Remove(surface);
        }

        public static bool TryGet(
            Collider collider,
            Vector3 worldPoint,
            Vector3 worldNormal,
            out TerritorySurface surface)
        {
            for (int index = 0; index < Surfaces.Count; index++)
            {
                TerritorySurface candidate = Surfaces[index];

                if (candidate.MatchesImpact(collider, worldPoint, worldNormal))
                {
                    surface = candidate;
                    return true;
                }
            }

            surface = null;
            return false;
        }

        public static bool TrySampleBelow(
            Vector3 worldPosition,
            float maximumDistance,
            out TerritorySurface surface,
            out TerritoryCell cell)
        {
            if (Physics.Raycast(
                    worldPosition,
                    Vector3.down,
                    out RaycastHit hit,
                    maximumDistance,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore) &&
                TryGet(hit.collider, hit.point, hit.normal, out surface) &&
                surface.TrySampleWorldPoint(hit.point, out cell))
            {
                return true;
            }

            surface = null;
            cell = default;
            return false;
        }

        public static CoverageSnapshot GetCoverage()
        {
            int totalCells = 0;
            int neutralCells = 0;
            int mistCells = 0;
            int iceCells = 0;
            int fireCells = 0;

            for (int index = 0; index < Surfaces.Count; index++)
            {
                CoverageSnapshot coverage = Surfaces[index].GetCoverage();
                totalCells += coverage.TotalCells;
                neutralCells += coverage.NeutralCells;
                mistCells += coverage.MistCells;
                iceCells += coverage.GetTeam(IceTeam).OwnedCells;
                fireCells += coverage.GetTeam(FireTeam).OwnedCells;
            }

            return new CoverageSnapshot(
                totalCells,
                neutralCells,
                mistCells,
                new[]
                {
                    new TeamCoverage(IceTeam, iceCells, totalCells),
                    new TeamCoverage(FireTeam, fireCells, totalCells)
                });
        }

        public static void ResetAll()
        {
            for (int index = 0; index < Surfaces.Count; index++)
            {
                Surfaces[index].ResetTerritory();
            }
        }

        public static MatchScoreSnapshot GetMatchScore(CircularArenaBoundary boundary)
        {
            int total = 0;
            int neutral = 0;
            int mist = 0;
            int ice = 0;
            int fire = 0;
            int iceBank = 0;
            int fireBank = 0;
            int iceMistCreated = 0;
            int fireMistCreated = 0;
            int iceMistClaimed = 0;
            int fireMistClaimed = 0;

            for (int index = 0; index < Surfaces.Count; index++)
            {
                TerritorySurface surface = Surfaces[index];
                surface.CountActiveCells(boundary, ref total, ref neutral, ref mist, ref ice, ref fire);
                iceBank += surface.IceBank;
                fireBank += surface.FireBank;
                iceMistCreated += surface.IceMistCreatedCells;
                fireMistCreated += surface.FireMistCreatedCells;
                iceMistClaimed += surface.IceMistClaimedCells;
                fireMistClaimed += surface.FireMistClaimedCells;
            }

            var coverage = new CoverageSnapshot(total, neutral, mist, new[]
            {
                new TeamCoverage(IceTeam, ice, total),
                new TeamCoverage(FireTeam, fire, total)
            });
            return new MatchScoreSnapshot(
                coverage, iceBank, fireBank,
                iceMistCreated, fireMistCreated, iceMistClaimed, fireMistClaimed);
        }

        public static void PublishReaction(TerritoryReactionEvent reaction)
        {
            ReactionOccurred?.Invoke(reaction);
        }
    }
}
