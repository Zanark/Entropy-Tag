using EntropyTag.UnityAdapters;
using UnityEngine;

namespace EntropyTag.Presentation
{
    public sealed class ThirdPersonCameraRig : MonoBehaviour
    {
        [SerializeField]
        private Transform target;

        [SerializeField]
        private PlayerInputSource input;

        [SerializeField]
        private Camera controlledCamera;

        [SerializeField]
        private float pivotHeight = 1.5f;

        [SerializeField]
        private float distance = 4.5f;

        [SerializeField]
        private float minimumDistance = 0.25f;

        [SerializeField]
        private Vector3 aimShoulderOffset = new Vector3(0.6f, 0.1f, 0f);

        [SerializeField]
        private float collisionRadius = 0.2f;

        [SerializeField]
        private float collisionPadding = 0.05f;

        [SerializeField]
        private LayerMask collisionMask = ~0;

        [SerializeField]
        private float minimumPitch = -35f;

        [SerializeField]
        private float maximumPitch = 70f;

        private float yaw;
        private float pitch = 15f;
        private bool usesExternalLookControl;

        public Camera ControlledCamera => controlledCamera;

        public void Configure(Transform followTarget, PlayerInputSource inputSource, Camera cameraToControl)
        {
            target = followTarget;
            input = inputSource;
            controlledCamera = cameraToControl;
            yaw = followTarget != null ? followTarget.eulerAngles.y : 0f;
        }

        public void SetExternalLookControl(bool enabled)
        {
            usesExternalLookControl = enabled;
        }

        public void Simulate(Vector2 lookDelta, bool isAiming)
        {
            if (target == null || controlledCamera == null)
            {
                return;
            }

            yaw += lookDelta.x;
            pitch = Mathf.Clamp(pitch + lookDelta.y, minimumPitch, maximumPitch);

            Quaternion orbitRotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot = target.position + Vector3.up * pivotHeight;
            Vector3 shoulder = isAiming ? aimShoulderOffset : Vector3.zero;
            Vector3 desiredPosition = pivot + orbitRotation * (shoulder + Vector3.back * distance);
            Vector3 castDirection = desiredPosition - pivot;
            float castDistance = castDirection.magnitude;

            if (castDistance > 0f &&
                Physics.SphereCast(
                    pivot,
                    collisionRadius,
                    castDirection / castDistance,
                    out RaycastHit hit,
                    castDistance,
                    collisionMask,
                    QueryTriggerInteraction.Ignore))
            {
                float safeDistance = Mathf.Max(minimumDistance, hit.distance - collisionPadding);
                desiredPosition = pivot + castDirection.normalized * safeDistance;
            }

            controlledCamera.transform.SetPositionAndRotation(desiredPosition, orbitRotation);
        }

        private void LateUpdate()
        {
            if (usesExternalLookControl)
            {
                return;
            }

            Vector2 lookDelta = input != null ? input.ReadLookDelta(Time.unscaledDeltaTime) : Vector2.zero;
            bool isAiming = input != null && input.IsAiming;
            Simulate(lookDelta, isAiming);
        }
    }
}
