using UnityEngine;

namespace Deducted.UI
{
    /// <summary>Hides this UI (via a CanvasGroup) while an NPC talk is open, e.g. the HUD balance.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class HideDuringDialogue : MonoBehaviour
    {
        private CanvasGroup group;

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
        }

        private void LateUpdate()
        {
            group.alpha = Deducted.NPC.DialogueSystem.IsOpen ? 0f : 1f;
        }
    }
}
