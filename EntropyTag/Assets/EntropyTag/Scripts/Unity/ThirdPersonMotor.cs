using System;
using UnityEngine;

namespace EntropyTag.UnityAdapters
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class ThirdPersonMotor : MonoBehaviour
    {
        [SerializeField]
        private PlayerInputSource input;

        [SerializeField]
        private Transform cameraTransform;

        [SerializeField]
        private Transform visualRoot;

        [SerializeField]
        private float maximumSpeed = 6f;

        [SerializeField]
        private float acceleration = 30f;

        [SerializeField]
        private float deceleration = 40f;

        [SerializeField]
        private float rotationSpeed = 720f;

        [SerializeField]
        private float gravity = 25f;

        [SerializeField]
        private float impulseDrag = 8f;

        [SerializeField]
        private float jumpHeight = 1.7f;

        [SerializeField]
        private float wallClimbSpeed = 3.5f;

        [SerializeField]
        private float wallProbeDistance = 0.35f;

        [SerializeField]
        private float slideSpeed = 11f;

        [SerializeField]
        private float slideDuration = 0.65f;

        [SerializeField]
        private float slidingHeight = 1f;

        private CharacterController controller;
        private Vector3 horizontalVelocity;
        private Vector3 externalVelocity;
        private float verticalVelocity;
        private float speedMultiplier = 1f;
        private float standingHeight;
        private Vector3 standingCenter;
        private float slideTimeRemaining;
        private Vector3 slideDirection;
        private Vector3 wallNormal;
        private readonly Collider[] clearanceHits = new Collider[8];

        public Vector3 Velocity => horizontalVelocity + externalVelocity + Vector3.up * verticalVelocity;

        public bool IsGrounded => controller != null && IsGroundedForActions();

        public bool IsWallClimbing { get; private set; }

        public bool IsSliding => slideTimeRemaining > 0f;

        public void Configure(PlayerInputSource inputSource, Transform movementCamera, Transform playerVisual = null)
        {
            input = inputSource;
            cameraTransform = movementCamera;
            visualRoot = playerVisual;
        }

        public void AddImpulse(Vector3 impulse)
        {
            externalVelocity += impulse;
        }

        public void SetSpeedMultiplier(float multiplier)
        {
            if (multiplier <= 0f || float.IsNaN(multiplier) || float.IsInfinity(multiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(multiplier), "Speed multiplier must be finite and greater than zero.");
            }

            speedMultiplier = multiplier;
        }

        public void Respawn(Vector3 position, Quaternion rotation)
        {
            EnsureController();
            controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            controller.enabled = true;
            horizontalVelocity = Vector3.zero;
            externalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            slideTimeRemaining = 0f;
            IsWallClimbing = false;
            controller.height = standingHeight;
            controller.center = standingCenter;

            if (visualRoot != null)
            {
                visualRoot.localPosition = Vector3.zero;
                visualRoot.localRotation = Quaternion.identity;
            }
        }

        public void Simulate(Vector2 moveInput, Transform movementCamera, float deltaTime)
        {
            Simulate(moveInput, movementCamera, deltaTime, false, false);
        }

        public void Simulate(
            Vector2 moveInput,
            Transform movementCamera,
            float deltaTime,
            bool jumpRequested,
            bool slideRequested)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            EnsureController();

            Vector3 forward = movementCamera != null ? movementCamera.forward : Vector3.forward;
            Vector3 right = movementCamera != null ? movementCamera.right : Vector3.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector2 clampedInput = Vector2.ClampMagnitude(moveInput, 1f);
            Vector3 desiredDirection = forward * clampedInput.y + right * clampedInput.x;
            desiredDirection = Vector3.ClampMagnitude(desiredDirection, 1f);
            UpdateWallClimb(desiredDirection);

            bool grounded = IsGroundedForActions();

            if (slideRequested && grounded && desiredDirection.sqrMagnitude > 0.01f)
            {
                BeginSlide(desiredDirection);
            }

            if (IsSliding)
            {
                slideTimeRemaining = Mathf.Max(0f, slideTimeRemaining - deltaTime);
                horizontalVelocity = Vector3.MoveTowards(
                    horizontalVelocity,
                    slideDirection * slideSpeed,
                    deceleration * 0.2f * deltaTime);
            }
            else
            {
                TryRestoreStandingHeight();
                Vector3 desiredVelocity = desiredDirection * (maximumSpeed * speedMultiplier);
                float velocityChange = desiredVelocity.sqrMagnitude > horizontalVelocity.sqrMagnitude
                    ? acceleration
                    : deceleration;

                horizontalVelocity =
                    Vector3.MoveTowards(horizontalVelocity, desiredVelocity, velocityChange * deltaTime);
            }

            if (desiredDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion desiredRotation = Quaternion.LookRotation(desiredDirection, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredRotation, rotationSpeed * deltaTime);
            }

            if (jumpRequested && IsWallClimbing)
            {
                IsWallClimbing = false;
                verticalVelocity = CalculateJumpVelocity();
                externalVelocity += wallNormal * 4f;
            }
            else if (jumpRequested && grounded && !IsSliding)
            {
                verticalVelocity = CalculateJumpVelocity();
            }
            else if (IsWallClimbing)
            {
                verticalVelocity = wallClimbSpeed;
            }
            else if (grounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }
            else
            {
                verticalVelocity -= gravity * deltaTime;
            }

            externalVelocity = Vector3.MoveTowards(externalVelocity, Vector3.zero, impulseDrag * deltaTime);
            controller.Move(Velocity * deltaTime);
        }

        private void Awake()
        {
            EnsureController();
        }

        private void Update()
        {
            if (input == null)
            {
                return;
            }

            Simulate(
                input.Move,
                cameraTransform,
                Time.deltaTime,
                input.WasJumpPressedThisFrame,
                input.WasSlidePressedThisFrame);
        }

        private void EnsureController()
        {
            if (controller == null)
            {
                controller = GetComponent<CharacterController>();
                standingHeight = controller.height;
                standingCenter = controller.center;
            }
        }

        private void UpdateWallClimb(Vector3 desiredDirection)
        {
            IsWallClimbing = false;

            if (desiredDirection.sqrMagnitude <= 0.01f)
            {
                return;
            }

            Vector3 origin = controller.bounds.center +
                             desiredDirection.normalized * (controller.radius + 0.02f);

            if (!Physics.Raycast(
                    origin,
                    desiredDirection,
                    out RaycastHit hit,
                    wallProbeDistance,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore) ||
                Mathf.Abs(hit.normal.y) > 0.25f)
            {
                return;
            }

            IsWallClimbing = true;
            wallNormal = hit.normal;
            horizontalVelocity = Vector3.ProjectOnPlane(horizontalVelocity, hit.normal);
        }

        private void BeginSlide(Vector3 desiredDirection)
        {
            slideDirection = desiredDirection.normalized;
            slideTimeRemaining = slideDuration;
            controller.height = slidingHeight;
            controller.center = Vector3.up * (slidingHeight * 0.5f);

            if (visualRoot != null)
            {
                visualRoot.localPosition = new Vector3(0f, -0.35f, 0.2f);
                visualRoot.localRotation = Quaternion.Euler(65f, 0f, 0f);
            }
        }

        private void TryRestoreStandingHeight()
        {
            if (Mathf.Approximately(controller.height, standingHeight) || !HasStandingClearance())
            {
                return;
            }

            controller.height = standingHeight;
            controller.center = standingCenter;

            if (visualRoot != null)
            {
                visualRoot.localPosition = Vector3.zero;
                visualRoot.localRotation = Quaternion.identity;
            }
        }

        private bool HasStandingClearance()
        {
            float radius = Mathf.Max(0.01f, controller.radius - controller.skinWidth);
            Vector3 center = transform.TransformPoint(standingCenter);
            float halfSegment = Mathf.Max(0f, standingHeight * 0.5f - radius);
            Vector3 bottom = center + Vector3.down * halfSegment;
            Vector3 top = center + Vector3.up * halfSegment;
            int hitCount = Physics.OverlapCapsuleNonAlloc(
                bottom,
                top,
                radius,
                clearanceHits,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            for (int index = 0; index < hitCount; index++)
            {
                if (clearanceHits[index] != null && clearanceHits[index] != controller)
                {
                    return false;
                }
            }

            return true;
        }

        private float CalculateJumpVelocity()
        {
            return Mathf.Sqrt(2f * gravity * jumpHeight);
        }

        private bool IsGroundedForActions()
        {
            if (controller.isGrounded)
            {
                return true;
            }

            float distance = controller.height * 0.5f + 0.15f;
            return Physics.Raycast(
                controller.bounds.center,
                Vector3.down,
                distance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
        }
    }
}
