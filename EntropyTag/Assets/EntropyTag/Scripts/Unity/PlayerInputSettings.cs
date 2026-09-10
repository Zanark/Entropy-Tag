using System;
using UnityEngine;

namespace EntropyTag.UnityAdapters
{
    [Serializable]
    public sealed class PlayerInputSettings
    {
        public const float DefaultMouseSensitivity = 0.08f;
        public const float DefaultGamepadSensitivity = 120f;

        [SerializeField]
        private float mouseSensitivity = DefaultMouseSensitivity;

        [SerializeField]
        private float gamepadSensitivity = DefaultGamepadSensitivity;

        [SerializeField]
        private bool invertVerticalLook;

        public float MouseSensitivity => mouseSensitivity;

        public float GamepadSensitivity => gamepadSensitivity;

        public bool InvertVerticalLook => invertVerticalLook;

        public static PlayerInputSettings CreateDefault()
        {
            return new PlayerInputSettings();
        }

        public void Set(float newMouseSensitivity, float newGamepadSensitivity, bool newInvertVerticalLook)
        {
            mouseSensitivity = Mathf.Clamp(newMouseSensitivity, 0.01f, 1f);
            gamepadSensitivity = Mathf.Clamp(newGamepadSensitivity, 30f, 360f);
            invertVerticalLook = newInvertVerticalLook;
        }

        public PlayerInputSettings Clone()
        {
            var clone = new PlayerInputSettings();
            clone.Set(mouseSensitivity, gamepadSensitivity, invertVerticalLook);
            return clone;
        }

        public void Normalize()
        {
            Set(mouseSensitivity, gamepadSensitivity, invertVerticalLook);
        }
    }
}
