using UnityEngine;
using UnityEngine.InputSystem;

namespace BlackFriday
{
    /// <summary>
    /// Klavye/fare + gamepad girişleri (Input System).
    /// Fare/Klavye: WASD hareket, fare bakış, Boşluk zıpla, SAĞ TIK nişan/strafe, SOL TIK fırlat/it, E al-bırak, Shift koş, V kamera (TPS/FPS), F it, Esc menü.
    /// Gamepad: Sol çubuk hareket, sağ çubuk bakış, A zıpla, LT nişan, RT fırlat/it, X al-bırak, L3 koş, R3 kamera, B it, Start menü.
    /// </summary>
    public static class BFInput
    {
        public static bool Blocked; // pause menüsü vb. açıkken oyun girişini kapat
        public static bool SimulateAim; // test/otomasyon için: sağ tık basılı say
        public static bool SimulateJump; // test/otomasyon için: bir kerelik zıplama

        public static Vector2 Move()
        {
            if (Blocked) return Vector2.zero;
            Vector2 v = Vector2.zero;
            var k = Keyboard.current;
            if (k != null)
            {
                if (k.wKey.isPressed || k.upArrowKey.isPressed) v.y += 1;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) v.y -= 1;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) v.x += 1;
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) v.x -= 1;
            }
            var g = Gamepad.current;
            if (g != null)
            {
                var s = g.leftStick.ReadValue();
                if (s.sqrMagnitude > 0.04f) v += s;
            }
            return Vector2.ClampMagnitude(v, 1f);
        }

        /// <summary>Bu karede bakış değişimi (derece).</summary>
        public static Vector2 Look()
        {
            if (Blocked) return Vector2.zero;
            Vector2 d = Vector2.zero;
            float sens = GameSettings.MouseSensitivity;
            var m = Mouse.current;
            if (m != null && Cursor.lockState == CursorLockMode.Locked) d += m.delta.ReadValue() * 0.08f * sens;
            var g = Gamepad.current;
            if (g != null)
            {
                var s = g.rightStick.ReadValue();
                if (s.sqrMagnitude > 0.02f) d += s * 160f * sens * Time.deltaTime;
            }
            if (GameSettings.InvertY) d.y = -d.y;
            return d;
        }

        public static bool Aim()
        {
            if (Blocked) return false;
            var m = Mouse.current; var g = Gamepad.current;
            return SimulateAim || (m != null && m.rightButton.isPressed) || (g != null && g.leftTrigger.isPressed);
        }

        /// <summary>Sol tık / RT: elde ürün varsa fırlat, yoksa it.</summary>
        public static bool Primary()
        {
            if (Blocked) return false;
            var m = Mouse.current; var g = Gamepad.current;
            return (m != null && m.leftButton.wasPressedThisFrame && Cursor.lockState == CursorLockMode.Locked) ||
                   (g != null && g.rightTrigger.wasPressedThisFrame);
        }

        public static bool Interact()
        {
            if (Blocked) return false;
            var k = Keyboard.current; var g = Gamepad.current;
            return (k != null && k.eKey.wasPressedThisFrame) || (g != null && g.buttonWest.wasPressedThisFrame);
        }

        public static bool JumpPressed()
        {
            if (SimulateJump) { SimulateJump = false; return true; }
            if (Blocked) return false;
            var k = Keyboard.current; var g = Gamepad.current;
            return (k != null && k.spaceKey.wasPressedThisFrame) || (g != null && g.buttonSouth.wasPressedThisFrame);
        }

        public static bool Sprint()
        {
            if (Blocked) return false;
            var k = Keyboard.current; var g = Gamepad.current;
            return (k != null && k.leftShiftKey.isPressed) || (g != null && g.leftStickButton.isPressed);
        }

        public static bool Shove()
        {
            if (Blocked) return false;
            var k = Keyboard.current; var g = Gamepad.current;
            return (k != null && k.fKey.wasPressedThisFrame) || (g != null && g.buttonEast.wasPressedThisFrame);
        }

        public static bool ToggleCamera()
        {
            if (Blocked) return false;
            var k = Keyboard.current; var g = Gamepad.current;
            return (k != null && k.vKey.wasPressedThisFrame) || (g != null && g.rightStickButton.wasPressedThisFrame);
        }

        public static bool Pause()
        {
            var k = Keyboard.current; var g = Gamepad.current;
            return (k != null && k.escapeKey.wasPressedThisFrame) || (g != null && g.startButton.wasPressedThisFrame);
        }

        public static bool Confirm()
        {
            var k = Keyboard.current; var g = Gamepad.current;
            return (k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame)) ||
                   (g != null && g.buttonNorth.wasPressedThisFrame);
        }

        public static float Zoom()
        {
            if (Blocked) return 0;
            var m = Mouse.current; float z = 0;
            if (m != null) z -= m.scroll.ReadValue().y * 0.002f;
            return z;
        }
    }
}
