using UnityEngine;

namespace Deducted.UI
{
    /// <summary>
    /// Renders this canvas through the game camera instead of on top of the screen,
    /// so the 1-bit full-screen pass (pixelation + dithering) also applies to it.
    /// Keeps the canvas just past the camera's near clip plane, which SecurityCamera
    /// changes with the zoom, so nothing in the world ends up in front of it.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(100)] // after SecurityCamera has set the clip planes
    [RequireComponent(typeof(Canvas))]
    public class DitheredCanvas : MonoBehaviour
    {
        [Tooltip("Empty = Camera.main.")]
        [SerializeField] private Camera targetCamera;
        [Tooltip("How far past the near clip plane the canvas sits.")]
        [SerializeField, Min(0.001f)] private float offset = 0.05f;
        [Tooltip("Minimum height of the canvas in the world (m). TextMeshPro text disappears when the canvas gets " +
                 "too small (close camera with a narrow FOV, e.g. the interaction camera), so it is pushed further " +
                 "away until it is at least this tall. Things closer to the camera than that can cover the UI.")]
        [SerializeField, Min(0f)] private float minWorldHeight = 0.5f;

        private Canvas canvas;

        /// <summary>For canvases made from code (e.g. ScreenFade): 0 = always right at the near clip plane.</summary>
        public float MinWorldHeight
        {
            get => minWorldHeight;
            set => minWorldHeight = Mathf.Max(0f, value);
        }

        private void OnEnable()
        {
            canvas = GetComponent<Canvas>();
        }

        private void LateUpdate()
        {
            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null || canvas == null) return;

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            float distance = cam.nearClipPlane + offset;
            if (!cam.orthographic && minWorldHeight > 0f)
            {
                // Height of the view at distance d: 2 * d * tan(fov / 2).
                float tan = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                distance = Mathf.Max(distance, minWorldHeight / (2f * tan));
            }
            canvas.planeDistance = distance;
        }
    }
}
