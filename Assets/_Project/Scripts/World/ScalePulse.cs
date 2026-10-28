using UnityEngine;

namespace Deducted.World
{
    /// <summary>
    /// Makes an object grow and shrink with a sine wave, e.g. a speaker pumping to the beat.
    /// Scale per axis = original scale * (1 + amount * sine).
    /// </summary>
    public class ScalePulse : MonoBehaviour
    {
        [Tooltip("How much each axis grows/shrinks, as a fraction of its scale. 0.05 = ±5 %, 0 = axis stays still.")]
        [SerializeField] private Vector3 amount = new Vector3(0.05f, 0.05f, 0.05f);
        [Tooltip("Pulses per minute. Match the music's BPM to pump on the beat.")]
        [SerializeField, Min(0f)] private float beatsPerMinute = 128f;
        [Tooltip("Only grow (0..1) instead of growing and shrinking (-1..1). Feels more like a kick drum.")]
        [SerializeField] private bool onlyGrow = true;

        private Vector3 baseScale;

        private void Awake()
        {
            baseScale = transform.localScale;
        }

        private void OnDisable()
        {
            transform.localScale = baseScale;
        }

        private void Update()
        {
            float wave = Mathf.Sin(Time.time * beatsPerMinute / 60f * Mathf.PI * 2f);
            if (onlyGrow) wave = wave * 0.5f + 0.5f;

            transform.localScale = new Vector3(
                baseScale.x * (1f + amount.x * wave),
                baseScale.y * (1f + amount.y * wave),
                baseScale.z * (1f + amount.z * wave));
        }
    }
}
