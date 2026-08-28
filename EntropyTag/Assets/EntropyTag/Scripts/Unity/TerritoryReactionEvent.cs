using EntropyTag.Domain;
using UnityEngine;

namespace EntropyTag.UnityAdapters
{
    public enum TerritoryReactionKind
    {
        TerritoryClaimed = 0,
        MistCreated = 1,
        MistClaimed = 2
    }

    public readonly struct TerritoryReactionEvent
    {
        public TerritoryReactionEvent(
            TerritoryReactionKind kind,
            TerritorySurface surface,
            Vector3 worldPoint,
            Vector3 worldNormal,
            TerritoryState previousState,
            TerritoryState currentState,
            ElementId appliedElement,
            int bankAward)
        {
            Kind = kind;
            Surface = surface;
            WorldPoint = worldPoint;
            WorldNormal = worldNormal;
            PreviousState = previousState;
            CurrentState = currentState;
            AppliedElement = appliedElement;
            BankAward = bankAward;
        }

        public TerritoryReactionKind Kind { get; }

        public TerritorySurface Surface { get; }

        public Vector3 WorldPoint { get; }

        public Vector3 WorldNormal { get; }

        public TerritoryState PreviousState { get; }

        public TerritoryState CurrentState { get; }

        public ElementId AppliedElement { get; }

        public int BankAward { get; }
    }
}
