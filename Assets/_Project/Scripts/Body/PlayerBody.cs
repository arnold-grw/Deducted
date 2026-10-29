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
    /// Two layers: the owned state changes at once (money, shops), the felt state (GetState, Works,
    /// PartChanged, movement form) follows only when no dialogue / shop is open, so selling an ear
    /// in a shop is heard after leaving it. Losing an organic eye or ear plays the hurt animation.
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

        [Header("Sounds (always heard, even when deaf)")]
        [Tooltip("When the Collector takes a part. Both ears.")]
        [SerializeField] private AudioClip reclaimSound;
        [Tooltip("When an ear goes deaf (sold, reclaimed, subscription ended). Only on that ear; after leaving a shop.")]
        [SerializeField] private AudioClip earLostSound;
        [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

        [Header("Debug")]
        [Tooltip("Editor only: F1-F6 sell part, Shift+F1-F6 toggle subscription (on from organic or missing, off -> missing).")]
        [SerializeField] private bool debugKeys = true;

        private readonly Dictionary<BodyPartId, BodyPartState> states = new Dictionary<BodyPartId, BodyPartState>();
        private readonly Dictionary<BodyPartId, BodyPartState> felt = new Dictionary<BodyPartId, BodyPartState>();
        private bool changesPending;
        private float legBoostUntil = -1f;
        private bool legBoostWasActive;

        /// <summary>True while the legs work one step better (cigarette effect).</summary>
        public bool LegBoostActive => Time.time < legBoostUntil;
        public float LegBoostRemaining => Mathf.Max(0f, legBoostUntil - Time.time);

        /// <summary>Fired when the felt state of a part changes: (part, new state). Waits until dialogues close.</summary>
        public event Action<BodyPartId, BodyPartState> PartChanged;

        private void Reset()
        {
            playerAnimator = GetComponent<PlayerAnimator>();
        }

        private void Awake()
        {
            if (playerAnimator == null) playerAnimator = GetComponent<PlayerAnimator>();
            foreach (BodyPartId id in Enum.GetValues(typeof(BodyPartId)))
            {
                states[id] = BodyPartState.Organic;
                felt[id] = BodyPartState.Organic;
            }
        }

        private void Start()
        {
            UpdateMovementForm();
        }

        // Safe to call before Awake (other scripts may ask during their OnEnable).
        /// <summary>What the player feels right now (effects: vision, hearing, legs, visuals).</summary>
        public BodyPartState GetState(BodyPartId id) =>
            felt.TryGetValue(id, out var state) ? state : BodyPartState.Organic;
        /// <summary>What the player owns right now, even if a shop hasn't let it take effect yet.</summary>
        public BodyPartState GetOwnedState(BodyPartId id) =>
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

        /// <summary>
        /// Collector: takes an organic part by force and pays payoutFraction of its VITALIS price.
        /// Synthetic and missing parts can't be taken.
        /// </summary>
        public bool Reclaim(BodyPartId id, float payoutFraction)
        {
            if (states[id] != BodyPartState.Organic) return false;
            var config = GetConfig(id);
            int payout = Mathf.RoundToInt(config.sellPrice * payoutFraction);
            BalanceManager.Instance?.Apply($"COLLECTOR reclaim: {config.displayName}", payout);
            Deducted.Audio.UISound.Play(reclaimSound, soundVolume);
            SetState(id, BodyPartState.Missing);
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
            changesPending = true;
            if (!Deducted.NPC.DialogueSystem.IsOpen) ApplyPendingChanges();
        }

        // Owned -> felt for every part that changed; fires PartChanged, updates the legs, plays hurt.
        private void ApplyPendingChanges()
        {
            changesPending = false;
            bool hurt = false;
            bool legs = false;
            foreach (BodyPartId id in Enum.GetValues(typeof(BodyPartId)))
            {
                BodyPartState before = felt[id];
                BodyPartState now = states[id];
                if (before == now) continue;
                felt[id] = now;
                if (id == BodyPartId.LeftLeg || id == BodyPartId.RightLeg) legs = true;
                else if (before == BodyPartState.Organic) hurt = true; // an eye or ear was taken
                // Ear goes deaf (not when swapped for a synthetic one): sound on that ear only.
                if (now == BodyPartState.Missing && (id == BodyPartId.LeftEar || id == BodyPartId.RightEar))
                    Deducted.Audio.UISound.Play(earLostSound, soundVolume, id == BodyPartId.LeftEar ? -1f : 1f);
                PartChanged?.Invoke(id, now);
            }
            if (legs) UpdateMovementForm();
            if (hurt && playerAnimator != null) playerAnimator.Hurt();
        }

        /// <summary>
        /// Legs -> movement form:
        /// both organic = Normal, one synthetic = Injured, both synthetic = InjuredHeavy (limp),
        /// one missing = CrawlAllFours, both missing = CrawlLow.
        /// During a leg boost synthetic legs count one step better (Injured -> Normal, InjuredHeavy -> Injured),
        /// always from the current legs, so swapping a leg mid-boost gives the right result.
        /// </summary>
        public MovementForm GetLegForm()
        {
            BodyPartState l = felt[BodyPartId.LeftLeg];
            BodyPartState r = felt[BodyPartId.RightLeg];
            int missing = (l == BodyPartState.Missing ? 1 : 0) + (r == BodyPartState.Missing ? 1 : 0);
            int synthetic = (l == BodyPartState.Synthetic ? 1 : 0) + (r == BodyPartState.Synthetic ? 1 : 0);

            if (missing == 2) return MovementForm.CrawlLow;
            if (missing == 1) return MovementForm.CrawlAllFours;
            if (LegBoostActive) synthetic--;
            if (synthetic >= 2) return MovementForm.InjuredHeavy;
            if (synthetic == 1) return MovementForm.Injured;
            return MovementForm.Normal;
        }

        /// <summary>Legs work one step better for this many seconds. Calling again restarts the timer.</summary>
        public void BoostLegs(float seconds)
        {
            legBoostUntil = Time.time + seconds;
            legBoostWasActive = true;
            UpdateMovementForm();
        }

        /// <summary>Ends the leg boost right away (smoking interrupted by the Collector).</summary>
        public void CancelLegBoost()
        {
            legBoostUntil = -1f;
            legBoostWasActive = false;
            UpdateMovementForm();
        }

        private void UpdateMovementForm()
        {
            if (playerAnimator != null)
                playerAnimator.Form = GetLegForm();
        }

        private void Update()
        {
            // Purchases made in a dialogue / shop take effect once it's closed.
            if (changesPending && !Deducted.NPC.DialogueSystem.IsOpen) ApplyPendingChanges();

            // Back to the normal form when the boost runs out.
            if (legBoostWasActive && !LegBoostActive)
            {
                legBoostWasActive = false;
                UpdateMovementForm();
            }
#if UNITY_EDITOR
            if (!Deducted.UI.PauseMenu.BlocksInput) HandleDebugKeys();
#endif
        }

#if UNITY_EDITOR
        private void HandleDebugKeys()
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
