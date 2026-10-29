using Deducted.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Deducted.Interaction
{
    /// <summary>
    /// Finds the closest Interactable in a cone in front of the player and uses it with the interact key.
    /// Shows the prompt box with the thing's prompt text ([Talk], [Interact]...) while something is in reach.
    /// Put it on the player.
    /// Does nothing while movement is locked (Tab menu, dialogue, smoking, death).
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Key interactKey = Key.Space;
        [Tooltip("How far in front of the player things can be used (m).")]
        [SerializeField] private float radius = 1.8f;
        [Tooltip("Width of the cone in front of the player (degrees). 360 = all around.")]
        [SerializeField, Range(10f, 360f)] private float angle = 140f;
        [Tooltip("Optional: shown while something is in reach (the InteractPrompt box).")]
        [SerializeField] private GameObject prompt;
        [Tooltip("Optional: gets the prompt text of the thing in reach.")]
        [SerializeField] private TMP_Text promptText;

        private PlayerMovement movement;

        /// <summary>What Space would use right now, or null.</summary>
        public Interactable Current { get; private set; }

        private void Awake()
        {
            movement = GetComponentInParent<PlayerMovement>();
        }

        private void Update()
        {
            bool canAct = movement == null || movement.InputEnabled;
            Current = canAct ? FindBest() : null;

            if (prompt != null && prompt.activeSelf != (Current != null))
                prompt.SetActive(Current != null);
            if (promptText != null && Current != null && promptText.text != Current.Prompt)
                promptText.text = Current.Prompt;

            Keyboard kb = Keyboard.current;
            if (Current != null && kb != null && kb[interactKey].wasPressedThisFrame && Time.frameCount > Interactable.BlockedUntilFrame
                && !Deducted.UI.PauseMenu.BlocksInput)
                Current.Interact();
        }

        private Interactable FindBest()
        {
            Vector3 origin = transform.position;
            Vector3 forward = transform.forward;
            forward.y = 0f;

            Interactable best = null;
            float bestDistance = float.MaxValue;
            foreach (Interactable candidate in Interactable.All)
            {
                if (candidate == null || !candidate.CanInteract) continue;

                Vector3 to = candidate.Position - origin;
                to.y = 0f;
                float distance = to.magnitude;
                if (distance > radius || distance >= bestDistance) continue;
                if (distance > 0.01f && Vector3.Angle(forward, to) > angle * 0.5f) continue;

                best = candidate;
                bestDistance = distance;
            }
            return best;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, radius);
            Vector3 left = Quaternion.Euler(0f, -angle * 0.5f, 0f) * transform.forward;
            Vector3 right = Quaternion.Euler(0f, angle * 0.5f, 0f) * transform.forward;
            Gizmos.DrawLine(transform.position, transform.position + left * radius);
            Gizmos.DrawLine(transform.position, transform.position + right * radius);
        }
    }
}
