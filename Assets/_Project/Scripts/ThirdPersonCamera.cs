using UnityEngine;
using UnityEngine.InputSystem;

namespace MagicWorld
{
    /// <summary>Камера от третьего лица: мышь или правый стик крутят вокруг героя, колесо — приближает.</summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 6.5f;
        public float height = 1.7f;
        public float mouseSensitivity = 0.12f;
        public float stickSensitivity = 140f;
        public float yaw = 20f;
        public float pitch = 18f;
        public LayerMask obstacles = ~0;

        void Start() { LockCursor(true); }

        void LockCursor(bool on)
        {
            Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !on;
        }

        void LateUpdate()
        {
            if (target == null) return;

            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) LockCursor(false);
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked) LockCursor(true);

            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                var d = mouse.delta.ReadValue();
                yaw += d.x * mouseSensitivity;
                pitch -= d.y * mouseSensitivity;
                distance = Mathf.Clamp(distance - mouse.scroll.ReadValue().y * 0.01f, 3f, 12f);
            }
            var gp = Gamepad.current;
            if (gp != null)
            {
                var s = gp.rightStick.ReadValue();
                yaw += s.x * stickSensitivity * Time.deltaTime;
                pitch -= s.y * stickSensitivity * 0.6f * Time.deltaTime;
            }
            pitch = Mathf.Clamp(pitch, -10f, 70f);

            Quaternion rot = Quaternion.Euler(pitch, yaw, 0);
            Vector3 focus = target.position + Vector3.up * height;
            Vector3 want = focus - rot * Vector3.forward * distance;

            // камера не заходит в землю и стены
            Vector3 dirBack = want - focus;
            if (Physics.SphereCast(focus, 0.25f, dirBack.normalized, out var hit, dirBack.magnitude, obstacles, QueryTriggerInteraction.Ignore)
                && hit.transform != target && !hit.transform.IsChildOf(target))
                want = focus + dirBack.normalized * Mathf.Max(0.8f, hit.distance - 0.1f);

            transform.position = Vector3.Lerp(transform.position, want, 1f - Mathf.Exp(-18f * Time.deltaTime));
            transform.rotation = Quaternion.LookRotation(focus - transform.position, Vector3.up);
        }
    }
}
