using System.Collections.Generic;
using UnityEngine;

namespace Deducted.Audio
{
    /// <summary>
    /// Area around a music source where the player dances instead of idling.
    /// Put it on the speaker. PlayerAnimator checks all zones every frame.
    /// </summary>
    public class DanceZone : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float radius = 2f;

        private static readonly List<DanceZone> zones = new List<DanceZone>();

        private void OnEnable() => zones.Add(this);
        private void OnDisable() => zones.Remove(this);

        /// <summary>True if the position is inside any active dance zone (ignoring height).</summary>
        public static bool Contains(Vector3 position)
        {
            foreach (var zone in zones)
            {
                Vector3 offset = position - zone.transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude <= zone.radius * zone.radius)
                    return true;
            }
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
