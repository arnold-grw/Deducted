using System.Collections.Generic;
using UnityEngine;

namespace Deducted.Interaction
{
    /// <summary>
    /// Anything the player can use with the interact key (Space): NPCs, terminals, gates...
    /// PlayerInteractor picks the closest one in front of the player. Subclasses implement Interact().
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        private static readonly List<Interactable> all = new List<Interactable>();
        public static IReadOnlyList<Interactable> All => all;

        /// <summary>Interact presses are ignored until after this frame (so closing a talk with Space doesn't reopen it).</summary>
        public static int BlockedUntilFrame { get; set; }

        [Tooltip("Text of the interact prompt, e.g. [Use Terminal]. Empty = the default of this kind " +
                 "([Talk] for NPCs, [Interact] otherwise).")]
        [SerializeField] private string prompt = "";

        /// <summary>Prompt shown while this is in reach.</summary>
        public string Prompt => string.IsNullOrEmpty(prompt) ? DefaultPrompt : prompt;
        protected virtual string DefaultPrompt => "[Interact]";

        public Vector3 Position => transform.position;
        public virtual bool CanInteract => isActiveAndEnabled;

        public abstract void Interact();

        protected virtual void OnEnable() => all.Add(this);
        protected virtual void OnDisable() => all.Remove(this);
    }
}
