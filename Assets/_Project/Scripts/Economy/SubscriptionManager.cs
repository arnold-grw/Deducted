using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deducted.Economy
{
    /// <summary>
    /// Running subscriptions (CONTINUUM). Each costs ₵ per second, charged every second
    /// via BalanceManager.Charge(): no receipt lines, but totals for the final receipt.
    /// Time keeps running in menus: everything has a price.
    /// </summary>
    public class SubscriptionManager : MonoBehaviour
    {
        public static SubscriptionManager Instance { get; private set; }

        [Tooltip("Seconds between charges. Charges don't appear on the receipt.")]
        [SerializeField, Min(0.1f)] private float chargeIntervalSeconds = 1f;

        public class Subscription
        {
            public string id;
            public string name;
            public float costPerSecond;
            public float accrued;
        }

        private readonly List<Subscription> active = new List<Subscription>();
        private float billingTimer;

        public IReadOnlyList<Subscription> Active => active;
        public event Action SubscriptionsChanged;

        /// <summary>Total ₵ per second of all running subscriptions.</summary>
        public float TotalCostPerSecond
        {
            get
            {
                float sum = 0f;
                foreach (var s in active) sum += s.costPerSecond;
                return sum;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool IsActive(string id) => active.Exists(s => s.id == id);

        public void Subscribe(string id, string name, float costPerSecond)
        {
            if (IsActive(id)) return;
            active.Add(new Subscription { id = id, name = name, costPerSecond = costPerSecond });
            SubscriptionsChanged?.Invoke();
        }

        /// <summary>Stops a subscription and bills what has accrued so far.</summary>
        public void Cancel(string id)
        {
            int i = active.FindIndex(s => s.id == id);
            if (i < 0) return;
            Bill(active[i]);
            active.RemoveAt(i);
            SubscriptionsChanged?.Invoke();
        }

        private void Update()
        {
            if (active.Count == 0) return;

            foreach (var s in active)
                s.accrued += s.costPerSecond * Time.deltaTime;

            billingTimer += Time.deltaTime;
            if (billingTimer < chargeIntervalSeconds) return;
            billingTimer = 0f;

            foreach (var s in active)
                Bill(s);
        }

        private void Bill(Subscription s)
        {
            // Charge whole credits only; the remainder carries over to the next charge.
            int amount = Mathf.FloorToInt(s.accrued);
            if (amount <= 0 || BalanceManager.Instance == null) return;
            s.accrued -= amount;
            BalanceManager.Instance.Charge($"CONTINUUM {s.name}", -amount);
        }
    }
}
