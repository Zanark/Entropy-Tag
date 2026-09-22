using UnityEngine;

namespace EntropyTag.UnityAdapters
{
    public interface IActorIntentSource
    {
        Vector2 Move { get; }
        bool IsFiring { get; }
        bool WasJumpPressedThisFrame { get; }
        bool WasSlidePressedThisFrame { get; }
    }

    public interface IAimSource
    {
        AimSolution Current { get; }
    }
}
