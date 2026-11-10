using UnityEngine;
using UnityEngine.InputSystem;

namespace MagicWorld
{
    /// <summary>Бег, прыжок и спринт героя на обычной физике (Rigidbody). Клавиатура (WASD, Пробел, Shift) и геймпад.</summary>
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public class HeroController : MonoBehaviour
    {
        public Transform model;
        public Transform cameraYaw;          // камера, относительно которой бежим
        public float walkSpeed = 6f;
        public float sprintSpeed = 10f;
        public float jumpHeight = 1.6f;
        public float gravity = -25f;
        public float waterY = 1.2f;          // ниже этой высоты герой упал в воду
        public float worldHalfSize = 96f;    // невидимые стены по краю карты

        public bool InputEnabled { get; set; } = true;

        Rigidbody rb;
        Vector3 wishDir;
        float wishSpeed;
        bool jumpQueued;
        bool grounded;
        Vector3 lastSafePos;
        float safeTimer;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            // без трения о стены, чтобы герой не «прилипал»
            var mat = new PhysicsMaterial("HeroNoFriction")
            {
                dynamicFriction = 0f, staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum
            };
            GetComponent<CapsuleCollider>().sharedMaterial = mat;
            lastSafePos = transform.position;
        }

        void Update()
        {
            Vector2 move = Vector2.zero;
            bool sprint = false;

            if (InputEnabled)
            {
                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1;
                    if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1;
                    if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1;
                    if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1;
                    if (kb.spaceKey.wasPressedThisFrame) jumpQueued = true;
                    sprint |= kb.leftShiftKey.isPressed;
                }
                var gp = Gamepad.current;
                if (gp != null)
                {
                    move += gp.leftStick.ReadValue();
                    if (gp.buttonSouth.wasPressedThisFrame) jumpQueued = true;
                    sprint |= gp.leftShoulder.isPressed || gp.leftTrigger.isPressed;
                }
            }
            move = Vector2.ClampMagnitude(move, 1f);

            // направление относительно взгляда камеры
            float yaw = cameraYaw != null ? cameraYaw.eulerAngles.y : 0f;
            wishDir = Quaternion.Euler(0, yaw, 0) * new Vector3(move.x, 0, move.y);
            wishSpeed = sprint ? sprintSpeed : walkSpeed;

            // поворот модели по ходу движения
            if (model != null && wishDir.sqrMagnitude > 0.01f)
            {
                var target = Quaternion.LookRotation(wishDir, Vector3.up);
                model.rotation = Quaternion.Slerp(model.rotation, target, 1f - Mathf.Exp(-14f * Time.deltaTime));
            }
        }

        void FixedUpdate()
        {
            grounded = CheckGround();
            var v = rb.linearVelocity;

            v.x = wishDir.x * wishSpeed;
            v.z = wishDir.z * wishSpeed;

            if (grounded && v.y <= 0.01f) v.y = -2f;                  // прижимаем к земле на спусках
            else v.y += gravity * Time.fixedDeltaTime;
            if (jumpQueued && grounded) v.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpQueued = false;
            v.y = Mathf.Max(v.y, -30f);
            rb.linearVelocity = v;

            // невидимые стены
            var p = rb.position;
            float cx = Mathf.Clamp(p.x, -worldHalfSize, worldHalfSize);
            float cz = Mathf.Clamp(p.z, -worldHalfSize, worldHalfSize);
            if (cx != p.x || cz != p.z) rb.position = new Vector3(cx, p.y, cz);

            // запоминаем безопасное место; упал в воду — вернулись на берег
            if (grounded && p.y > waterY + 0.6f)
            {
                safeTimer += Time.fixedDeltaTime;
                if (safeTimer > 0.4f) { lastSafePos = p; safeTimer = 0f; }
            }
            if (p.y < waterY - 0.2f) Respawn();
        }

        bool CheckGround()
        {
            var origin = rb.position + Vector3.up * 0.5f;
            var hits = Physics.SphereCastAll(origin, 0.3f, Vector3.down, 0.35f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
                if (!h.collider.transform.IsChildOf(transform) && Vector3.Angle(h.normal, Vector3.up) < 60f) return true;
            return false;
        }

        public void Respawn()
        {
            Debug.LogWarning($"[Hero] упал в воду или провалился: pos={rb.position}, возврат в {lastSafePos}");
            Teleport(lastSafePos + Vector3.up * 0.3f, false);
        }

        public void Teleport(Vector3 pos, bool markSafe = true)
        {
            rb.position = pos;
            transform.position = pos;
            rb.linearVelocity = Vector3.zero;
            if (markSafe) lastSafePos = pos;
        }
    }
}
