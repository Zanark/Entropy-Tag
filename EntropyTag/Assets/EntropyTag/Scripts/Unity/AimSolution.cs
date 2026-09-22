using UnityEngine;

namespace EntropyTag.UnityAdapters
{
    public readonly struct AimSolution
    {
        public AimSolution(Vector3 origin, Vector3 direction, Vector3 point, Collider hitCollider)
        {
            Origin = origin;
            Direction = direction;
            Point = point;
            HitCollider = hitCollider;
        }

        public Vector3 Origin { get; }

        public Vector3 Direction { get; }

        public Vector3 Point { get; }

        public Collider HitCollider { get; }

        public bool HasPhysicsContact => HitCollider != null;
    }
}
