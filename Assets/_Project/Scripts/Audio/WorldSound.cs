using UnityEngine;

namespace Deducted.Audio
{
    /// <summary>
    /// Spatial sound in the world, heard from the player's ears (not the far-away camera).
    /// Does distance falloff and left/right panning itself, so a missing ear can cut its side.
    /// Works on WebGL too (no mixer or audio filters needed).
    /// Put it next to an AudioSource; the AudioSource is forced to 2D, this script does the 3D.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class WorldSound : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float volume = 1f;
        [Tooltip("Full volume within this distance.")]
        [SerializeField, Min(0f)] private float minDistance = 2f;
        [Tooltip("Silent beyond this distance.")]
        [SerializeField, Min(0.1f)] private float maxDistance = 20f;
        [Tooltip("How far left/right a sound can pan. 1 = hard pan (the far ear hears nothing), lower = the far ear still hears it quietly.")]
        [SerializeField, Range(0f, 1f)] private float stereoWidth = 0.6f;

        private AudioSource source;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.spatialBlend = 0f;
        }

        private void LateUpdate()
        {
            Hearing hearing = Hearing.Instance;
            if (hearing == null)
            {
                source.volume = volume;
                source.panStereo = 0f;
                return;
            }

            Vector3 toSound = transform.position - hearing.Position;
            float distance = toSound.magnitude;

            float falloff = 1f - Mathf.InverseLerp(minDistance, maxDistance, distance);
            falloff *= falloff; // softer, more natural fade

            // -1 = fully left, +1 = fully right, as seen from the camera.
            float pan = distance > 0.01f ? Vector3.Dot(hearing.Right, toSound / distance) : 0f;
            // Never fully one-sided: the far ear always picks up some of it, just quieter.
            pan *= stereoWidth;

            // Equal-power panning gains for each ear.
            float angle = (pan + 1f) * Mathf.PI * 0.25f;
            float leftGain = Mathf.Cos(angle);
            float rightGain = Mathf.Sin(angle);

            bool left = hearing.LeftWorks;
            bool right = hearing.RightWorks;

            if (left && right)
            {
                // Both ears always hear it; the far ear just quieter. Closer than Min Distance
                // the sound surrounds you, so it narrows towards the centre.
                float closeness = minDistance > 0f ? Mathf.Clamp01(distance / minDistance) : 1f;
                source.panStereo = pan * Mathf.Lerp(0.5f, 1f, closeness);
                source.volume = volume * falloff;
            }
            else if (left)
            {
                // Only the left channel plays, at the loudness the left ear would get
                // (sounds on the right are quieter but not silent, see stereoWidth).
                source.panStereo = -1f;
                source.volume = volume * falloff * leftGain * Mathf.Sqrt(2f);
            }
            else if (right)
            {
                source.panStereo = 1f;
                source.volume = volume * falloff * rightGain * Mathf.Sqrt(2f);
            }
            else
            {
                source.volume = 0f;
            }
        }
    }
}
