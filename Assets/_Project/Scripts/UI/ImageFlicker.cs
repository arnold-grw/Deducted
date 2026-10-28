using UnityEngine;
using UnityEngine.UI;

namespace Deducted.UI
{
    /// <summary>
    /// Fades a UI image in and out with a sine wave between a min and max opacity,
    /// e.g. the ads in a synthetic eye. Through the 1-bit dither the fade reads as a flicker.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public class ImageFlicker : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float minAlpha = 0.3f;
        [SerializeField, Range(0f, 1f)] private float maxAlpha = 1f;
        [Tooltip("Fade cycles per second.")]
        [SerializeField, Min(0f)] private float speed = 1.5f;
        [Tooltip("Shift in the cycle (0..1), so two ads don't pulse in sync.")]
        [SerializeField, Range(0f, 1f)] private float phase;

        private Graphic graphic;

        private void Awake()
        {
            graphic = GetComponent<Graphic>();
        }

        private void Update()
        {
            float wave = Mathf.Sin((Time.unscaledTime * speed + phase) * Mathf.PI * 2f) * 0.5f + 0.5f;
            Color c = graphic.color;
            c.a = Mathf.Lerp(minAlpha, maxAlpha, wave);
            graphic.color = c;
        }
    }
}
