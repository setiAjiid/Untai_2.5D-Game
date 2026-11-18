using UnityEngine;
using UnityEngine.InputSystem;

namespace Untai.Core
{
    /// <summary>
    /// Pembaca input terpusat berbasis Input System (proyek memakai Input System saja).
    /// Keyboard: A/D atau panah = gerak, Space = lompat, Shift/J = benang, W/S = gulung/ulur.
    /// Gamepad: stik kiri = gerak, South = lompat, West/RT = benang.
    /// </summary>
    public static class InputReader
    {
        public static float Horizontal
        {
            get
            {
                float h = 0f;
                var k = Keyboard.current;
                if (k != null)
                {
                    if (k.aKey.isPressed || k.leftArrowKey.isPressed) h -= 1f;
                    if (k.dKey.isPressed || k.rightArrowKey.isPressed) h += 1f;
                }
                var g = Gamepad.current;
                if (g != null)
                {
                    float x = g.leftStick.x.ReadValue();
                    if (Mathf.Abs(x) > 0.2f) h = x;
                }
                return Mathf.Clamp(h, -1f, 1f);
            }
        }

        public static float Vertical
        {
            get
            {
                float v = 0f;
                var k = Keyboard.current;
                if (k != null)
                {
                    if (k.sKey.isPressed || k.downArrowKey.isPressed) v -= 1f;
                    if (k.wKey.isPressed || k.upArrowKey.isPressed) v += 1f;
                }
                var g = Gamepad.current;
                if (g != null)
                {
                    float y = g.leftStick.y.ReadValue();
                    if (Mathf.Abs(y) > 0.2f) v = y;
                }
                return Mathf.Clamp(v, -1f, 1f);
            }
        }

        public static bool JumpPressed
        {
            get
            {
                var k = Keyboard.current;
                var g = Gamepad.current;
                return (k != null && k.spaceKey.wasPressedThisFrame) ||
                       (g != null && g.buttonSouth.wasPressedThisFrame);
            }
        }

        public static bool JumpHeld
        {
            get
            {
                var k = Keyboard.current;
                var g = Gamepad.current;
                return (k != null && k.spaceKey.isPressed) ||
                       (g != null && g.buttonSouth.isPressed);
            }
        }

        public static bool ThreadHeld
        {
            get
            {
                var k = Keyboard.current;
                var g = Gamepad.current;
                return (k != null && (k.leftShiftKey.isPressed || k.jKey.isPressed)) ||
                       (g != null && (g.buttonWest.isPressed || g.rightTrigger.isPressed));
            }
        }

        /// <summary>Shift/J (gamepad: West/RT) ditekan sekali: kait atau lepas jangkar.</summary>
        public static bool ThreadPressed
        {
            get
            {
                var k = Keyboard.current;
                var g = Gamepad.current;
                return (k != null && (k.leftShiftKey.wasPressedThisFrame || k.jKey.wasPressedThisFrame)) ||
                       (g != null && (g.buttonWest.wasPressedThisFrame || g.rightTrigger.wasPressedThisFrame));
            }
        }

        /// <summary>Tahan F (gamepad: North) untuk menggulung untai dan menarik pasangan.</summary>
        public static bool ReelHeld
        {
            get
            {
                var k = Keyboard.current;
                var g = Gamepad.current;
                return (k != null && k.fKey.isPressed) || (g != null && g.buttonNorth.isPressed);
            }
        }

        public static bool KeyDown(Key key)
        {
            var k = Keyboard.current;
            return k != null && k[key].wasPressedThisFrame;
        }
    }
}
