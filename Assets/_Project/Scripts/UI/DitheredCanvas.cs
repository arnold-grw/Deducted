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

        private Canvas canvas;

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
            canvas.planeDistance = cam.nearClipPlane + offset;
        }
    }
}
