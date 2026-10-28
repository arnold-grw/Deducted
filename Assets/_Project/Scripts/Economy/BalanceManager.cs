using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deducted.Economy
{
    /// <summary>
    /// The player's balance (₵), which is also their health. One-off transactions go through
    /// Apply() and land on the receipt; running costs (subscriptions) go through Charge(),
    /// which skips the receipt but adds up per item for the final receipt.
    /// One per scene, reachable via Instance.
    /// </summary>
    public class BalanceManager : MonoBehaviour
    {
        public static BalanceManager Instance { get; private set; }

        [SerializeField] private int startingBalance = 1000;

        private readonly List<Transaction> transactions = new List<Transaction>();
        private readonly Dictionary<string, int> runningTotals = new Dictionary<string, int>();

        public int Balance { get; private set; }
        public bool IsNegative => Balance < 0;
        public IReadOnlyList<Transaction> Transactions => transactions;
        /// <summary>Sum per running cost (e.g. each subscription) over the whole run, for the final receipt.</summary>
        public IReadOnlyDictionary<string, int> RunningTotals => runningTotals;

        /// <summary>Fired after every transaction, with the transaction.</summary>
        public event Action<Transaction> TransactionAdded;
        /// <summary>Fired whenever the balance changes, receipt line or not.</summary>
        public event Action<int> BalanceChanged;
        /// <summary>Fired once when the balance drops below zero (collectors come for you).</summary>
        public event Action BalanceWentNegative;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[BalanceManager] Second instance destroyed.", this);
                Destroy(this);
                return;
            }
            Instance = this;
            Balance = startingBalance;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Add (positive) or deduct (negative) money and log it.</summary>
        public Transaction Apply(string description, int amount)
        {
            bool wasNegative = IsNegative;
            Balance += amount;

            var t = new Transaction(Time.time, description, amount, Balance);
            transactions.Add(t);
            TransactionAdded?.Invoke(t);
            AfterChange(wasNegative);
            return t;
        }

        /// <summary>
        /// Running cost (e.g. a subscription tick): changes the balance without a receipt line,
        /// but is summed under its name for the final receipt.
        /// </summary>
        public void Charge(string item, int amount)
        {
            if (amount == 0) return;
            bool wasNegative = IsNegative;
            Balance += amount;
            runningTotals.TryGetValue(item, out int total);
            runningTotals[item] = total + amount;
            AfterChange(wasNegative);
        }

        private void AfterChange(bool wasNegative)
        {
            BalanceChanged?.Invoke(Balance);
            if (!wasNegative && IsNegative)
                BalanceWentNegative?.Invoke();
        }

        public bool CanAfford(int cost) => Balance >= cost;
    }
}
