using UnityEngine;
using UnityEngine.InputSystem;

namespace MagicWorld
{
    /// <summary>Бег, прыжок и спринт героя. Клавиатура (WASD, Пробел, Shift) и геймпад (левый стик, A, LB/LT).</summary>
    [RequireComponent(typeof(CharacterController))]
    public class HeroController : MonoBehaviour
    {
        public Transform model;
        public Transform cameraYaw;          // камера, относительно которой бежим
        public float walkSpeed = 6f;
        public float sprintSpeed = 10f;
        public float jumpHeight = 1.6f;
        public float gravity = -25f;
        public float waterY = 1.2f;          // ниже этой высоты герой падает в воду
        public float worldHalfSize = 96f;    // невидимые стены по краю карты

        public bool InputEnabled { get; set; } = true;

        CharacterController cc;
        Vector3 velocity;
        Vector3 lastSafePos;
        float safeTimer;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            lastSafePos = transform.position;
        }

        void Update()
        {
            Vector2 move = Vector2.zero;
            bool jump = false, sprint = false;

            if (InputEnabled)
            {
                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1;
                    if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1;
                    if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1;
                    if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1;
                    jump |= kb.spaceKey.wasPressedThisFrame;
                    sprint |= kb.leftShiftKey.isPressed;
                }
                var gp = Gamepad.current;
                if (gp != null)
                {
                    move += gp.leftStick.ReadValue();
                    jump |= gp.buttonSouth.wasPressedThisFrame;
                    sprint |= gp.leftShoulder.isPressed || gp.leftTrigger.isPressed;
                }
            }
            move = Vector2.ClampMagnitude(move, 1f);

            // направление относительно взгляда камеры
            float yaw = cameraYaw != null ? cameraYaw.eulerAngles.y : 0f;
            Quaternion yawRot = Quaternion.Euler(0, yaw, 0);
            Vector3 dir = yawRot * new Vector3(move.x, 0, move.y);

            float speed = sprint ? sprintSpeed : walkSpeed;
            Vector3 horizontal = dir * speed;

            if (cc.isGrounded && velocity.y < 0) velocity.y = -2f;
            if (jump && cc.isGrounded) velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            velocity.y += gravity * Time.deltaTime;

            cc.Move((horizontal + Vector3.up * velocity.y) * Time.deltaTime);

            // невидимые стены
            var p = transform.position;
            float cx = Mathf.Clamp(p.x, -worldHalfSize, worldHalfSize);
            float cz = Mathf.Clamp(p.z, -worldHalfSize, worldHalfSize);
            if (cx != p.x || cz != p.z)
            {
                cc.enabled = false;
                transform.position = new Vector3(cx, p.y, cz);
                cc.enabled = true;
            }

            // поворот модели по ходу движения
            if (model != null && dir.sqrMagnitude > 0.01f)
            {
                var target = Quaternion.LookRotation(dir, Vector3.up);
                model.rotation = Quaternion.Slerp(model.rotation, target, 1f - Mathf.Exp(-14f * Time.deltaTime));
            }

            // запоминаем безопасное место; упал в воду — вернулись на берег
            if (cc.isGrounded && transform.position.y > waterY + 0.6f)
            {
                safeTimer += Time.deltaTime;
                if (safeTimer > 0.4f) { lastSafePos = transform.position; safeTimer = 0f; }
            }
            if (transform.position.y < waterY - 0.2f) Respawn();
        }

        public void Respawn()
        {
            cc.enabled = false;
            transform.position = lastSafePos + Vector3.up * 0.3f;
            cc.enabled = true;
            velocity = Vector3.zero;
        }

        public void Teleport(Vector3 pos)
        {
            cc.enabled = false;
            transform.position = pos;
            cc.enabled = true;
            lastSafePos = pos;
            velocity = Vector3.zero;
        }
    }
}
