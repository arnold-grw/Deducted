using System;
using Deducted.Economy;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Deducted.Body
{
    /// <summary>
    /// The player's name, which is the first thing they sell. Afterwards they are only
    /// a subject ID, freshly generated (from a GUID) every run.
    /// </summary>
    public class PlayerIdentity : MonoBehaviour
    {
        [Tooltip("Chosen by the player at the start (name entry comes later).")]
        [SerializeField] private string playerName = "NAMELESS";
        [Tooltip("₵ VITALIS pays for the name.")]
        [SerializeField] private int namePrice = 200;

        [Header("Debug")]
        [Tooltip("Editor only: F7 sells the name.")]
        [SerializeField] private bool debugKeys = true;

        /// <summary>Full GUID of this run.</summary>
        public string SubjectGuid { get; private set; }
        /// <summary>Short, readable form shown in game, e.g. "SUBJECT 7F3A-91C2".</summary>
        public string SubjectId { get; private set; }
        public string PlayerName => playerName;
        public bool NameSold { get; private set; }

        /// <summary>What the game calls the player right now: their name, or their ID once sold.</summary>
        public string DisplayName => NameSold ? SubjectId : playerName.ToUpperInvariant();

        public event Action IdentityChanged;

        private void Awake()
        {
            Guid guid = Guid.NewGuid();
            SubjectGuid = guid.ToString();
            string hex = guid.ToString("N").ToUpperInvariant();
            SubjectId = $"SUBJECT {hex.Substring(0, 4)}-{hex.Substring(4, 4)}";
        }

        /// <summary>Set the chosen name (before it is sold).</summary>
        public void SetName(string newName)
        {
            if (NameSold || string.IsNullOrWhiteSpace(newName)) return;
            playerName = newName.Trim();
            IdentityChanged?.Invoke();
        }

        /// <summary>VITALIS buys your name. From now on you are a subject ID.</summary>
        public bool SellName()
        {
            if (NameSold) return false;
            NameSold = true;
            BalanceManager.Instance?.Apply($"VITALIS purchase: name \"{playerName}\"", namePrice);
            IdentityChanged?.Invoke();
            return true;
        }

#if UNITY_EDITOR
        private void Update()
        {
            if (debugKeys && Keyboard.current != null && Keyboard.current.f7Key.wasPressedThisFrame)
                SellName();
        }
#endif
    }
}
