using UnityEngine;
using UnityEngine.InputSystem;

namespace Deducted.Player
{
    /// <summary>
    /// Camera-relative WASD movement for the player bean.
    /// Moves via CharacterController, turns smoothly toward the move direction,
    /// and exposes speed values for PlayerAnimator.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Input")]
        [Tooltip("WASD / arrow keys / left stick by default. Rebind in the Inspector.")]
        [SerializeField] private InputAction moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 4f;
        [Tooltip("How fast the bean reaches full speed (units/s²).")]
        [SerializeField] private float acceleration = 25f;
        [Tooltip("How fast the bean stops (units/s²).")]
        [SerializeField] private float deceleration = 30f;

        [Header("Rotation")]
        [Tooltip("Smoothing time for turning toward the move direction. Lower = snappier.")]
        [SerializeField] private float turnSmoothTime = 0.1f;

        [Header("Gravity")]
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float groundedStickForce = -2f;

        [Header("Camera")]
        [Tooltip("Camera the input is relative to. Empty = Camera.main (the active fixed camera).")]
        [SerializeField] private Camera referenceCamera;
        [Tooltip("On a camera cut, keep walking the same way until the input changes (classic fixed-camera behaviour).")]
        [SerializeField] private bool holdDirectionOnCameraCut = true;

        /// <summary>Horizontal velocity in world space (units/s).</summary>
        public Vector3 Velocity => horizontalVelocity;
        /// <summary>Current horizontal speed (units/s).</summary>
        public float Speed => horizontalVelocity.magnitude;
        /// <summary>Speed normalised 0..1 against moveSpeed — handy for blend trees.</summary>
        public float Speed01 => moveSpeed > 0f ? Mathf.Clamp01(Speed / moveSpeed) : 0f;
        public bool IsGrounded => controller.isGrounded;
        /// <summary>
        /// False while input is frozen: either switched off (death) or locked by someone (menu, actions).
        /// Set false to switch it off for good; use Lock/Unlock for temporary freezes so they can overlap.
        /// </summary>
        public bool InputEnabled
        {
            get => inputSwitchedOn && inputLocks.Count == 0;
            set => inputSwitchedOn = value;
        }
        private bool inputSwitchedOn = true;
        private readonly System.Collections.Generic.HashSet<object> inputLocks = new System.Collections.Generic.HashSet<object>();

        /// <summary>Freeze input until the same owner calls Unlock. Several owners can lock at once.</summary>
        public void Lock(object owner) => inputLocks.Add(owner);
        public void Unlock(object owner) => inputLocks.Remove(owner);
        /// <summary>Scales Move Speed (set by PlayerAnimator per movement form, e.g. crawling).</summary>
        public float SpeedMultiplier { get; set; } = 1f;
        /// <summary>Overrides Turn Smooth Time when set (PlayerAnimator sets it per movement form).</summary>
        public float? TurnSmoothTimeOverride { get; set; }

        private CharacterController controller;
        private Vector3 horizontalVelocity;
        private float verticalVelocity;
        private float turnVelocity;

        // Camera basis currently used for input, so direction survives camera cuts.
        private Camera basisCamera;
        private Vector3 basisForward = Vector3.forward;
        private Vector3 basisRight = Vector3.right;
        private Vector2 inputAtCut;
        private bool holdingCutBasis;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();

            if (moveAction.bindings.Count == 0)
            {
                moveAction.AddCompositeBinding("2DVector")
                    .With("Up", "<Keyboard>/w")
                    .With("Down", "<Keyboard>/s")
                    .With("Left", "<Keyboard>/a")
                    .With("Right", "<Keyboard>/d");
                moveAction.AddCompositeBinding("2DVector")
                    .With("Up", "<Keyboard>/upArrow")
                    .With("Down", "<Keyboard>/downArrow")
                    .With("Left", "<Keyboard>/leftArrow")
                    .With("Right", "<Keyboard>/rightArrow");
                moveAction.AddBinding("<Gamepad>/leftStick");
            }
        }

        private void OnEnable() => moveAction.Enable();
        private void OnDisable() => moveAction.Disable();

        private void Update()
        {
            Vector2 input = InputEnabled ? moveAction.ReadValue<Vector2>() : Vector2.zero;
            input = Vector2.ClampMagnitude(input, 1f);

            UpdateCameraBasis(input);

            Vector3 moveDir = basisForward * input.y + basisRight * input.x;
            Vector3 targetVelocity = moveDir * moveSpeed * SpeedMultiplier;

            float rate = targetVelocity.sqrMagnitude > 0.0001f ? acceleration : deceleration;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, rate * Time.deltaTime);

            if (moveDir.sqrMagnitude > 0.0001f)
            {
                float targetAngle = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
                float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnVelocity, TurnSmoothTimeOverride ?? turnSmoothTime);
                transform.rotation = Quaternion.Euler(0f, angle, 0f);
            }

            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = groundedStickForce;
            verticalVelocity += gravity * Time.deltaTime;

            Vector3 motion = horizontalVelocity;
            motion.y = verticalVelocity;
            controller.Move(motion * Time.deltaTime);
        }

        private void UpdateCameraBasis(Vector2 input)
        {
            Camera cam = referenceCamera != null ? referenceCamera : Camera.main;
            if (cam == null) return;

            if (cam != basisCamera)
            {
                bool firstCamera = basisCamera == null;
                basisCamera = cam;

                if (!firstCamera && holdDirectionOnCameraCut && input.sqrMagnitude > 0.01f)
                {
                    // Keep the old basis until the player lets go or changes direction.
                    holdingCutBasis = true;
                    inputAtCut = input;
                    return;
                }
            }

            if (holdingCutBasis)
            {
                bool released = input.sqrMagnitude < 0.01f;
                bool changed = Vector2.Angle(input, inputAtCut) > 30f;
                if (!released && !changed) return;
                holdingCutBasis = false;
            }

            Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            // Straight-down cameras have no horizontal forward; fall back to their up vector.
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.ProjectOnPlane(cam.transform.up, Vector3.up);

            basisForward = forward.normalized;
            basisRight = Vector3.Cross(Vector3.up, basisForward);
        }
    }
}
