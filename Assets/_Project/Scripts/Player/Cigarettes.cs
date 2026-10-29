using System;
using Deducted.Body;
using UnityEngine;

namespace Deducted.Player
{
    /// <summary>
    /// The player's cigarettes. Smoking (R, see PlayerAnimator) uses one; none left = no smoking.
    /// Smoking makes the legs work one step better for a while (PlayerBody.BoostLegs):
    /// one synthetic leg walks like organic, two synthetic like one. Smoking again restarts the timer.
    /// PlayerAnimator adds this automatically if it's missing.
    /// </summary>
    public class Cigarettes : MonoBehaviour
    {
        [SerializeField, Min(0)] private int count = 10;
        [Tooltip("Seconds the leg effect lasts after smoking.")]
        [SerializeField, Min(0f)] private float effectDuration = 10f;
        [Tooltip("Empty = found on the player.")]
        [SerializeField] private PlayerBody body;

        public int Count => count;
        public bool HasAny => count > 0;

        /// <summary>Fired with the new count when it changes.</summary>
        public event Action<int> CountChanged;

        private void Awake()
        {
            if (body == null) body = GetComponentInParent<PlayerBody>();
            if (body == null) body = GetComponentInChildren<PlayerBody>();
        }

        /// <summary>Take one out of the pack. False when there are none left.</summary>
        public bool TryUse()
        {
            if (count <= 0) return false;
            count--;
            CountChanged?.Invoke(count);
            return true;
        }

        public void Add(int amount)
        {
            count = Mathf.Max(0, count + amount);
            CountChanged?.Invoke(count);
        }

        /// <summary>
        /// The effect of a smoked cigarette. Called when smoking starts; extraSeconds = length of the smoke
        /// animation, so the effect still lasts Effect Duration after it.
        /// </summary>
        public void ApplyEffect(float extraSeconds = 0f)
        {
            if (body != null) body.BoostLegs(effectDuration + extraSeconds);
        }

        /// <summary>Stops the effect early (smoking interrupted). The cigarette stays used.</summary>
        public void CancelEffect()
        {
            if (body != null) body.CancelLegBoost();
        }
    }
}
