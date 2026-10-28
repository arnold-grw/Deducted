using System;
using System.Collections.Generic;
using Deducted.Economy;
using Deducted.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Deducted.Body
{
    /// <summary>
    /// The player's body: which parts are organic, synthetic (subscription) or missing.
    /// Selling pays out via BalanceManager, synthetic parts run via SubscriptionManager.
    /// Legs decide the movement form; eyes and ears are read by VisionOverlay and Hearing.
    /// </summary>
    public class PlayerBody : MonoBehaviour
    {
        [SerializeField] private PlayerAnimator playerAnimator;

        [SerializeField] private List<BodyPartConfig> parts = new List<BodyPartConfig>
        {
            new BodyPartConfig(BodyPartId.LeftEye, "Left Eye", 300, "Vision Premium (L)", 2f),
            new BodyPartConfig(BodyPartId.RightEye, "Right Eye", 300, "Vision Premium (R)", 2f),
            new BodyPartConfig(BodyPartId.LeftEar, "Left Ear", 150, "AudioLink Basic (L)", 1f),
            new BodyPartConfig(BodyPartId.RightEar, "Right Ear", 150, "AudioLink Basic (R)", 1f),
            new BodyPartConfig(BodyPartId.LeftLeg, "Left Leg", 400, "MotionCore Leg (L)", 3f),
            new BodyPartConfig(BodyPartId.RightLeg, "Right Leg", 400, "MotionCore Leg (R)", 3f),
        };

        [Header("Debug")]
        [Tooltip("Editor only: F1-F6 sell part, Shift+F1-F6 toggle subscription (on from organic or missing, off -> missing).")]
        [SerializeField] private bool debugKeys = true;

        private readonly Dictionary<BodyPartId, BodyPartState> states = new Dictionary<BodyPartId, BodyPartState>();

        /// <summary>Fired when any part changes state: (part, new state).</summary>
        public event Action<BodyPartId, BodyPartState> PartChanged;

        private void Reset()
        {
            playerAnimator = GetComponent<PlayerAnimator>();
        }

        private void Awake()
        {
            if (playerAnimator == null) playerAnimator = GetComponent<PlayerAnimator>();
            foreach (BodyPartId id in Enum.GetValues(typeof(BodyPartId)))
                states[id] = BodyPartState.Organic;
        }

        private void Start()
        {
            UpdateMovementForm();
        }

        // Safe to call before Awake (other scripts may ask during their OnEnable).
        public BodyPartState GetState(BodyPartId id) =>
            states.TryGetValue(id, out var state) ? state : BodyPartState.Organic;
        public bool Works(BodyPartId id) => GetState(id) != BodyPartState.Missing;
        public BodyPartConfig GetConfig(BodyPartId id) => parts.Find(p => p.id == id);

        /// <summary>VITALIS: sell an organic part for its price. It is gone afterwards.</summary>
        public bool Sell(BodyPartId id)
        {
            if (states[id] != BodyPartState.Organic) return false;
            var config = GetConfig(id);
            BalanceManager.Instance?.Apply($"VITALIS purchase: {config.displayName}", config.sellPrice);
            SetState(id, BodyPartState.Missing);
            return true;
        }

        /// <summary>
        /// CONTINUUM: switch to a synthetic part billed per second. Works any time:
        /// a missing part gets replaced, an organic part is traded in (VITALIS pays for it) and swapped.
        /// </summary>
        public bool Subscribe(BodyPartId id)
        {
            if (states[id] == BodyPartState.Synthetic) return false;
            var config = GetConfig(id);
            if (states[id] == BodyPartState.Organic)
                BalanceManager.Instance?.Apply($"VITALIS trade-in: {config.displayName}", config.sellPrice);
            SubscriptionManager.Instance?.Subscribe(id.ToString(), config.subscriptionName, config.subscriptionCostPerSecond);
            SetState(id, BodyPartState.Synthetic);
            return true;
        }

        /// <summary>Synthetic -> stop the subscription (part is missing again), otherwise subscribe.</summary>
        public bool ToggleSubscription(BodyPartId id) =>
            states[id] == BodyPartState.Synthetic ? CancelSubscription(id) : Subscribe(id);

        /// <summary>Stop the subscription; the synthetic part shuts off and the part is missing again.</summary>
        public bool CancelSubscription(BodyPartId id)
        {
            if (states[id] != BodyPartState.Synthetic) return false;
            SubscriptionManager.Instance?.Cancel(id.ToString());
            SetState(id, BodyPartState.Missing);
            return true;
        }

        private void SetState(BodyPartId id, BodyPartState state)
        {
            states[id] = state;
            PartChanged?.Invoke(id, state);
            if (id == BodyPartId.LeftLeg || id == BodyPartId.RightLeg)
                UpdateMovementForm();
        }

        /// <summary>
        /// Legs -> movement form:
        /// both organic = Normal, one synthetic = Injured, both synthetic = InjuredHeavy (limp),
        /// one missing = CrawlAllFours, both missing = CrawlLow.
        /// </summary>
        public MovementForm GetLegForm()
        {
            BodyPartState l = states[BodyPartId.LeftLeg];
            BodyPartState r = states[BodyPartId.RightLeg];
            int missing = (l == BodyPartState.Missing ? 1 : 0) + (r == BodyPartState.Missing ? 1 : 0);
            int synthetic = (l == BodyPartState.Synthetic ? 1 : 0) + (r == BodyPartState.Synthetic ? 1 : 0);

            if (missing == 2) return MovementForm.CrawlLow;
            if (missing == 1) return MovementForm.CrawlAllFours;
            if (synthetic == 2) return MovementForm.InjuredHeavy;
            if (synthetic == 1) return MovementForm.Injured;
            return MovementForm.Normal;
        }

        private void UpdateMovementForm()
        {
            if (playerAnimator != null)
                playerAnimator.Form = GetLegForm();
        }

#if UNITY_EDITOR
        private void Update()
        {
            if (!debugKeys) return;
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            Key[] keys = { Key.F1, Key.F2, Key.F3, Key.F4, Key.F5, Key.F6 };
            for (int i = 0; i < keys.Length; i++)
            {
                if (!kb[keys[i]].wasPressedThisFrame) continue;
                var id = (BodyPartId)i;
                if (kb.shiftKey.isPressed) ToggleSubscription(id);
                else Sell(id);
            }
        }
#endif
    }
}
