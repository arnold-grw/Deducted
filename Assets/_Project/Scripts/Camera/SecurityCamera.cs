using UnityEngine;

namespace Deducted.CameraSystem
{
    /// <summary>
    /// Signalis-style camera: sits far away at a fixed angle with a narrow field of view,
    /// which flattens perspective into an almost-2D look. Can follow a target or stay
    /// fixed on a point (for security-cam angles later).
    /// Runs in edit mode so you can frame the shot without pressing Play.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class SecurityCamera : MonoBehaviour
    {
        [Header("Focus")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1f, 0f);
        [Tooltip("Off = look at Fixed Focus Point instead of following the target.")]
        [SerializeField] private bool followTarget = true;
        [SerializeField] private Vector3 fixedFocusPoint;
        [Tooltip("Follow smoothing in seconds. 0 = locked to the target.")]
        [SerializeField] private float followSmoothTime = 0.2f;

        [Header("Angle")]
        [Tooltip("Looking down angle. 90 = top-down.")]
        [SerializeField, Range(5f, 90f)] private float pitch = 35f;
        [Tooltip("Rotation around the vertical axis. 45 = classic isometric diagonal.")]
        [SerializeField] private float yaw = 45f;

        [Header("Lens")]
        [Tooltip("Lower = flatter, more 2D. Distance is adjusted so framing stays the same.")]
        [SerializeField, Range(1f, 60f)] private float fieldOfView = 12f;
        [Tooltip("Zoom: how much of the screen height the player fills. 0.2 = a fifth of the screen, 1 = full screen.")]
        [SerializeField, Range(0.02f, 1f)] private float playerScreenSize = 0.2f;
        [Tooltip("Height of the player in world units (the default capsule is 2).")]
        [SerializeField, Min(0.1f)] private float playerHeight = 2f;
        [Tooltip("Fully flat orthographic projection instead of a long lens.")]
        [SerializeField] private bool orthographic;
        [Tooltip("Set near/far clip planes from the distance, for better depth precision.")]
        [SerializeField] private bool autoClipPlanes = true;

        /// <summary>World units visible vertically at the focus point.</summary>
        public float ViewHeight => playerHeight / playerScreenSize;

        /// <summary>Zoom level as screen fraction the player fills (0.02..1).</summary>
        public float PlayerScreenSize
        {
            get => playerScreenSize;
            set => playerScreenSize = Mathf.Clamp(value, 0.02f, 1f);
        }

        private Camera cam;
        private Vector3 smoothedFocus;
        private Vector3 focusVelocity;
        private bool initialised;

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        /// <summary>Distance from the focus point to the camera.</summary>
        public float Distance
        {
            get
            {
                if (orthographic) return ViewHeight * 4f;
                return ViewHeight * 0.5f / Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
            }
        }

        private void OnEnable()
        {
            cam = GetComponent<Camera>();
            initialised = false;
        }

        // Apply Inspector changes immediately in the editor, even when nothing else repaints.
        private void OnValidate()
        {
            if (!isActiveAndEnabled || Application.isPlaying) return;
            cam = GetComponent<Camera>();
            Apply(GetDesiredFocus());
        }

        private void LateUpdate()
        {
            Vector3 focus = GetDesiredFocus();

            if (!initialised || !Application.isPlaying || followSmoothTime <= 0f)
            {
                smoothedFocus = focus;
                focusVelocity = Vector3.zero;
                initialised = true;
            }
            else
            {
                smoothedFocus = Vector3.SmoothDamp(smoothedFocus, focus, ref focusVelocity, followSmoothTime);
            }

            Apply(smoothedFocus);
        }

        /// <summary>Jump straight to the focus without smoothing (e.g. after a camera cut).</summary>
        public void SnapToTarget()
        {
            initialised = false;
        }

        private Vector3 GetDesiredFocus()
        {
            if (followTarget && target != null)
                return target.position + targetOffset;
            return fixedFocusPoint;
        }

        private void Apply(Vector3 focus)
        {
            if (cam == null) cam = GetComponent<Camera>();

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            float distance = Distance;
            transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);

            cam.orthographic = orthographic;
            if (orthographic)
                cam.orthographicSize = ViewHeight * 0.5f;
            else
                cam.fieldOfView = fieldOfView;

            if (autoClipPlanes)
            {
                cam.nearClipPlane = Mathf.Max(0.3f, distance - ViewHeight * 2f);
                cam.farClipPlane = distance * 2f + 50f;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.white;
            Vector3 focus = Application.isPlaying ? smoothedFocus : GetDesiredFocus();
            Gizmos.DrawWireSphere(focus, 0.3f);
            Gizmos.DrawLine(transform.position, focus);
        }
    }
}
