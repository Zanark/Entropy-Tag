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

        private CharacterController controller;
        private Vector3 horizontalVelocity;
        private Vector3 externalVelocity;
        private float verticalVelocity;
        private float speedMultiplier = 1f;

        public Vector3 Velocity => horizontalVelocity + externalVelocity + Vector3.up * verticalVelocity;

        public bool IsGrounded => controller != null && controller.isGrounded;

        public void Configure(PlayerInputSource inputSource, Transform movementCamera)
        {
            input = inputSource;
            cameraTransform = movementCamera;
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

        public void Simulate(Vector2 moveInput, Transform movementCamera, float deltaTime)
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
            Vector3 desiredVelocity = desiredDirection * (maximumSpeed * speedMultiplier);
            float velocityChange = desiredVelocity.sqrMagnitude > horizontalVelocity.sqrMagnitude
                ? acceleration
                : deceleration;

            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, desiredVelocity, velocityChange * deltaTime);

            if (desiredDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion desiredRotation = Quaternion.LookRotation(desiredDirection, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredRotation, rotationSpeed * deltaTime);
            }

            if (controller.isGrounded && verticalVelocity < 0f)
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

            Simulate(input.Move, cameraTransform, Time.deltaTime);
        }

        private void EnsureController()
        {
            if (controller == null)
            {
                controller = GetComponent<CharacterController>();
            }
        }
    }
}
