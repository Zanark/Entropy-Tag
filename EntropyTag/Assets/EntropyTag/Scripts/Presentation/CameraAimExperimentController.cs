using System;
using EntropyTag.UnityAdapters;
using UnityEngine;

namespace EntropyTag.Presentation
{
    public enum CameraAimExperimentMode
    {
        CenteredImmediate = 1,
        CenteredSmooth = 2,
        CenteredShoulder = 3,
        FreeAimEdgeTurn = 4,
        FreeAimContinuousFollow = 5,
        ElasticTether = 6,
        SoftZoneRecenter = 7,
        CenteredCinematicSpring = 8
    }

    [DefaultExecutionOrder(-100)]
    public sealed class CameraAimExperimentController : MonoBehaviour
    {
        private static readonly Vector2 ViewportCenter = new Vector2(0.5f, 0.5f);

        [SerializeField]
        private CameraAimExperimentMode mode;

        [SerializeField]
        private PlayerInputSource input;

        [SerializeField]
        private ThirdPersonCameraRig cameraRig;

        [SerializeField]
        private ThirdPersonAimSolver aimSolver;

        [SerializeField]
        private RectTransform reticle;

        [SerializeField]
        private float cameraSpeed = 1f;

        [SerializeField]
        private float cameraSmoothTime;

        [SerializeField]
        private float reticleSpeed = 850f;

        [SerializeField]
        private float reticleSmoothTime = 0.05f;

        [SerializeField]
        [Range(0.05f, 0.45f)]
        private float softZoneHalfExtent = 0.25f;

        [SerializeField]
        private float tetherRadius = 220f;

        [SerializeField]
        private float tetherCameraStrength = 8f;

        [SerializeField]
        private bool useShoulderWhileAiming;

        private Vector2 targetReticlePosition;
        private Vector2 reticlePosition;
        private Vector2 reticleVelocity;
        private Vector2 smoothedLook;
        private Vector2 lookVelocity;
        private bool initialized;

        public CameraAimExperimentMode Mode => mode;

        public Vector2 ReticleViewportPoint { get; private set; } = ViewportCenter;

        public void Configure(
            CameraAimExperimentMode experimentMode,
            PlayerInputSource inputSource,
            ThirdPersonCameraRig orbitCamera,
            ThirdPersonAimSolver solver,
            RectTransform reticleTransform)
        {
            mode = experimentMode;
            input = inputSource;
            cameraRig = orbitCamera;
            aimSolver = solver;
            reticle = reticleTransform;
            ApplyPreset(experimentMode);
            cameraRig.SetExternalLookControl(true);
            SetAimViewport(ViewportCenter);
        }

        public static string GetDisplayName(CameraAimExperimentMode experimentMode)
        {
            switch (experimentMode)
            {
                case CameraAimExperimentMode.CenteredImmediate:
                    return "01 - Centered Immediate";
                case CameraAimExperimentMode.CenteredSmooth:
                    return "02 - Centered Smooth";
                case CameraAimExperimentMode.CenteredShoulder:
                    return "03 - Centered Shoulder Aim";
                case CameraAimExperimentMode.FreeAimEdgeTurn:
                    return "04 - Free Aim Edge Turn";
                case CameraAimExperimentMode.FreeAimContinuousFollow:
                    return "05 - Free Aim Continuous Follow";
                case CameraAimExperimentMode.ElasticTether:
                    return "06 - Elastic Tether";
                case CameraAimExperimentMode.SoftZoneRecenter:
                    return "07 - Soft Zone Recenter";
                case CameraAimExperimentMode.CenteredCinematicSpring:
                    return "08 - Centered Cinematic Spring";
                default:
                    throw new ArgumentOutOfRangeException(nameof(experimentMode), experimentMode, null);
            }
        }

        private void ApplyPreset(CameraAimExperimentMode experimentMode)
        {
            cameraSpeed = 1f;
            cameraSmoothTime = 0f;
            reticleSpeed = 850f;
            reticleSmoothTime = 0.05f;
            softZoneHalfExtent = 0.25f;
            tetherRadius = 220f;
            tetherCameraStrength = 8f;
            useShoulderWhileAiming = false;

            switch (experimentMode)
            {
                case CameraAimExperimentMode.CenteredImmediate:
                    break;
                case CameraAimExperimentMode.CenteredSmooth:
                    cameraSmoothTime = 0.055f;
                    break;
                case CameraAimExperimentMode.CenteredShoulder:
                    cameraSmoothTime = 0.025f;
                    useShoulderWhileAiming = true;
                    break;
                case CameraAimExperimentMode.FreeAimEdgeTurn:
                    cameraSpeed = 150f;
                    reticleSmoothTime = 0.07f;
                    softZoneHalfExtent = 0.26f;
                    break;
                case CameraAimExperimentMode.FreeAimContinuousFollow:
                    cameraSpeed = 0.55f;
                    cameraSmoothTime = 0.035f;
                    reticleSmoothTime = 0.055f;
                    break;
                case CameraAimExperimentMode.ElasticTether:
                    cameraSpeed = 0.8f;
                    cameraSmoothTime = 0.045f;
                    tetherRadius = 210f;
                    tetherCameraStrength = 7f;
                    break;
                case CameraAimExperimentMode.SoftZoneRecenter:
                    cameraSpeed = 165f;
                    cameraSmoothTime = 0.045f;
                    reticleSmoothTime = 0.04f;
                    softZoneHalfExtent = 0.18f;
                    break;
                case CameraAimExperimentMode.CenteredCinematicSpring:
                    cameraSpeed = 0.85f;
                    cameraSmoothTime = 0.11f;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(experimentMode), experimentMode, null);
            }
        }

        private void OnEnable()
        {
            bool usesFreeReticle = UsesFreeReticle(mode);
            Cursor.lockState = usesFreeReticle ? CursorLockMode.Confined : CursorLockMode.Locked;
            Cursor.visible = false;
            initialized = false;
        }

        private void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void LateUpdate()
        {
            if (input == null || cameraRig == null || aimSolver == null || reticle == null)
            {
                return;
            }

            EnsureInitialized();

            switch (mode)
            {
                case CameraAimExperimentMode.CenteredImmediate:
                case CameraAimExperimentMode.CenteredSmooth:
                case CameraAimExperimentMode.CenteredShoulder:
                case CameraAimExperimentMode.CenteredCinematicSpring:
                    UpdateCenteredMode();
                    break;
                case CameraAimExperimentMode.FreeAimEdgeTurn:
                    UpdateFreeAimEdgeTurn(false);
                    break;
                case CameraAimExperimentMode.FreeAimContinuousFollow:
                    UpdateFreeAimContinuousFollow();
                    break;
                case CameraAimExperimentMode.ElasticTether:
                    UpdateElasticTether();
                    break;
                case CameraAimExperimentMode.SoftZoneRecenter:
                    UpdateFreeAimEdgeTurn(true);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void UpdateCenteredMode()
        {
            SetReticlePosition(ScreenCenter());
            Vector2 targetLook = input.ReadLookDelta(Time.unscaledDeltaTime) * cameraSpeed;
            Vector2 look = SmoothLook(targetLook);
            cameraRig.Simulate(look, useShoulderWhileAiming && input.IsAiming);
            SetAimViewport(ViewportCenter);
        }

        private void UpdateFreeAimContinuousFollow()
        {
            UpdatePointerReticle();
            Vector2 targetLook = input.ReadLookDelta(Time.unscaledDeltaTime) * cameraSpeed;
            cameraRig.Simulate(SmoothLook(targetLook), false);
            SetAimViewport(ScreenToViewport(reticlePosition));
        }

        private void UpdateFreeAimEdgeTurn(bool recenterWhileTurning)
        {
            UpdatePointerReticle();
            Vector2 viewport = ScreenToViewport(reticlePosition);
            Vector2 edgeTurn = CalculateEdgeTurn(viewport, softZoneHalfExtent);
            Vector2 targetLook = edgeTurn * (cameraSpeed * Time.unscaledDeltaTime);
            cameraRig.Simulate(SmoothLook(targetLook), false);

            if (recenterWhileTurning && edgeTurn.sqrMagnitude > 0f)
            {
                targetReticlePosition = Vector2.MoveTowards(
                    targetReticlePosition,
                    ScreenCenter(),
                    reticleSpeed * 0.35f * Time.unscaledDeltaTime);
            }

            SetAimViewport(viewport);
        }

        private void UpdateElasticTether()
        {
            Vector2 rawLook = input.ReadLookInput();
            float inputScale = input.LookUsesMouse ? 1f : Time.unscaledDeltaTime * 45f;
            targetReticlePosition += rawLook * inputScale;

            Vector2 center = ScreenCenter();
            Vector2 offset = Vector2.ClampMagnitude(targetReticlePosition - center, tetherRadius);
            targetReticlePosition = center + offset;
            SetReticlePosition(Vector2.SmoothDamp(
                reticlePosition,
                targetReticlePosition,
                ref reticleVelocity,
                reticleSmoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime));

            Vector2 normalizedOffset = tetherRadius > 0f ? offset / tetherRadius : Vector2.zero;
            Vector2 targetLook = new Vector2(normalizedOffset.x, -normalizedOffset.y) *
                                 (tetherCameraStrength * cameraSpeed * Time.unscaledDeltaTime);
            cameraRig.Simulate(SmoothLook(targetLook), false);
            SetAimViewport(ScreenToViewport(reticlePosition));
        }

        private void UpdatePointerReticle()
        {
            if (input.LookUsesMouse && input.TryGetPointerPosition(out Vector2 pointerPosition))
            {
                targetReticlePosition = pointerPosition;
            }
            else
            {
                targetReticlePosition +=
                    Vector2.ClampMagnitude(input.ReadLookInput(), 1f) * (reticleSpeed * Time.unscaledDeltaTime);
            }

            targetReticlePosition.x = Mathf.Clamp(targetReticlePosition.x, 12f, Screen.width - 12f);
            targetReticlePosition.y = Mathf.Clamp(targetReticlePosition.y, 12f, Screen.height - 12f);
            SetReticlePosition(Vector2.SmoothDamp(
                reticlePosition,
                targetReticlePosition,
                ref reticleVelocity,
                reticleSmoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime));
        }

        private Vector2 SmoothLook(Vector2 targetLook)
        {
            if (cameraSmoothTime <= 0f)
            {
                smoothedLook = targetLook;
                return smoothedLook;
            }

            smoothedLook = Vector2.SmoothDamp(
                smoothedLook,
                targetLook,
                ref lookVelocity,
                cameraSmoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime);
            return smoothedLook;
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            targetReticlePosition = ScreenCenter();
            reticlePosition = targetReticlePosition;
            SetReticlePosition(reticlePosition);
            initialized = true;
        }

        private void SetReticlePosition(Vector2 screenPosition)
        {
            reticlePosition = screenPosition;
            reticle.position = screenPosition;
        }

        private void SetAimViewport(Vector2 viewport)
        {
            ReticleViewportPoint = new Vector2(Mathf.Clamp01(viewport.x), Mathf.Clamp01(viewport.y));
            aimSolver.SetViewportPoint(ReticleViewportPoint);
        }

        private static Vector2 ScreenCenter()
        {
            return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        private static Vector2 ScreenToViewport(Vector2 screenPosition)
        {
            return new Vector2(
                screenPosition.x / Mathf.Max(1f, Screen.width),
                screenPosition.y / Mathf.Max(1f, Screen.height));
        }

        private static bool UsesFreeReticle(CameraAimExperimentMode experimentMode)
        {
            return experimentMode == CameraAimExperimentMode.FreeAimEdgeTurn ||
                   experimentMode == CameraAimExperimentMode.FreeAimContinuousFollow ||
                   experimentMode == CameraAimExperimentMode.ElasticTether ||
                   experimentMode == CameraAimExperimentMode.SoftZoneRecenter;
        }

        private static Vector2 CalculateEdgeTurn(Vector2 viewportPoint, float innerHalfExtent)
        {
            return new Vector2(
                CalculateAxisTurn(viewportPoint.x, innerHalfExtent),
                -CalculateAxisTurn(viewportPoint.y, innerHalfExtent));
        }

        private static float CalculateAxisTurn(float position, float innerHalfExtent)
        {
            float offset = position - 0.5f;
            float magnitude = Mathf.Abs(offset);

            if (magnitude <= innerHalfExtent)
            {
                return 0f;
            }

            return Mathf.Sign(offset) *
                   Mathf.Clamp01((magnitude - innerHalfExtent) / (0.5f - innerHalfExtent));
        }
    }
}
