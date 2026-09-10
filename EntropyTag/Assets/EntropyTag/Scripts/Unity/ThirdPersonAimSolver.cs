using UnityEngine;

namespace EntropyTag.UnityAdapters
{
    public sealed class ThirdPersonAimSolver : MonoBehaviour, IAimSource
    {
        [SerializeField]
        private Camera aimCamera;

        [SerializeField]
        private LayerMask collisionMask = ~0;

        [SerializeField]
        private float maximumDistance = 100f;

        [SerializeField]
        private bool drawDebugRay = true;

        private Vector2 viewportPoint = new Vector2(0.5f, 0.5f);

        public AimSolution Current { get; private set; }

        public void Configure(Camera cameraToUse, LayerMask mask)
        {
            aimCamera = cameraToUse;
            collisionMask = mask;
        }

        public void SetViewportPoint(Vector2 normalizedViewportPoint)
        {
            viewportPoint = new Vector2(
                Mathf.Clamp01(normalizedViewportPoint.x),
                Mathf.Clamp01(normalizedViewportPoint.y));
        }

        public AimSolution Resolve()
        {
            if (aimCamera == null)
            {
                return default;
            }

            Ray ray = aimCamera.ViewportPointToRay(viewportPoint);

            if (Physics.Raycast(ray, out RaycastHit hit, maximumDistance, collisionMask, QueryTriggerInteraction.Ignore))
            {
                return new AimSolution(ray.origin, ray.direction, hit.point, hit.collider);
            }

            return new AimSolution(ray.origin, ray.direction, ray.GetPoint(maximumDistance), null);
        }

        private void LateUpdate()
        {
            Current = Resolve();

            if (drawDebugRay && Current.Direction.sqrMagnitude > 0f)
            {
                Color color = Current.HasPhysicsContact ? Color.green : Color.yellow;
                Debug.DrawLine(Current.Origin, Current.Point, color);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (Current.Direction.sqrMagnitude <= 0f)
            {
                return;
            }

            Gizmos.color = Current.HasPhysicsContact ? Color.green : Color.yellow;
            Gizmos.DrawLine(Current.Origin, Current.Point);
            Gizmos.DrawWireSphere(Current.Point, 0.1f);
        }
    }
}
