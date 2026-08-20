using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EntropyTag.UnityAdapters
{
    public sealed class PlayerInputSource : MonoBehaviour
    {
        private const string GameplayMapName = "Gameplay";

        [SerializeField]
        private InputActionAsset actions;

        private InputActionMap gameplayMap;
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction aimAction;
        private InputAction fireAction;
        private InputAction jumpAction;
        private InputAction slideAction;
        private PlayerInputSettings settings = PlayerInputSettings.CreateDefault();

        public Vector2 Move => moveAction?.ReadValue<Vector2>() ?? Vector2.zero;

        public bool IsAiming => aimAction?.IsPressed() ?? false;

        public bool IsFiring => fireAction?.IsPressed() ?? false;

        public bool WasJumpPressedThisFrame => jumpAction?.WasPressedThisFrame() ?? false;

        public bool WasSlidePressedThisFrame => slideAction?.WasPressedThisFrame() ?? false;

        public bool LookUsesMouse => lookAction?.activeControl?.device is Mouse;

        public PlayerInputSettings Settings => settings;

        public void Configure(InputActionAsset inputActions, PlayerInputSettings inputSettings = null)
        {
            if (inputActions == null)
            {
                throw new ArgumentNullException(nameof(inputActions));
            }

            bool wasEnabled = isActiveAndEnabled;

            if (wasEnabled)
            {
                gameplayMap?.Disable();
            }

            actions = inputActions;
            settings = inputSettings?.Clone() ?? PlayerInputSettings.CreateDefault();
            BindActions();

            if (wasEnabled && gameplayMap != null)
            {
                gameplayMap.Enable();
            }
        }

        public void ApplySettings(PlayerInputSettings inputSettings)
        {
            settings = inputSettings?.Clone() ?? throw new ArgumentNullException(nameof(inputSettings));
        }

        public Vector2 ReadLookDelta(float deltaTime)
        {
            if (lookAction == null)
            {
                return Vector2.zero;
            }

            Vector2 rawLook = lookAction.ReadValue<Vector2>();
            bool usesMouse = lookAction.activeControl?.device is Mouse;
            float scale = usesMouse
                ? settings.MouseSensitivity
                : settings.GamepadSensitivity * Mathf.Max(0f, deltaTime);

            float verticalSign = settings.InvertVerticalLook ? 1f : -1f;
            return new Vector2(rawLook.x * scale, rawLook.y * scale * verticalSign);
        }

        public Vector2 ReadLookInput()
        {
            return lookAction?.ReadValue<Vector2>() ?? Vector2.zero;
        }

        public bool TryGetPointerPosition(out Vector2 screenPosition)
        {
            if (Mouse.current == null)
            {
                screenPosition = default;
                return false;
            }

            screenPosition = Mouse.current.position.ReadValue();
            return true;
        }

        private void Awake()
        {
            BindActions();
        }

        private void OnEnable()
        {
            gameplayMap?.Enable();
        }

        private void OnDisable()
        {
            gameplayMap?.Disable();
        }

        private void BindActions()
        {
            if (actions == null)
            {
                return;
            }

            gameplayMap = actions.FindActionMap(GameplayMapName);

            if (gameplayMap == null)
            {
                throw new InvalidOperationException($"Input actions do not define the required '{GameplayMapName}' map.");
            }

            moveAction = gameplayMap.FindAction("Move", true);
            lookAction = gameplayMap.FindAction("Look", true);
            aimAction = gameplayMap.FindAction("Aim", true);
            fireAction = gameplayMap.FindAction("Fire", true);
            jumpAction = gameplayMap.FindAction("Jump", true);
            slideAction = gameplayMap.FindAction("Slide", true);
        }
    }
}
