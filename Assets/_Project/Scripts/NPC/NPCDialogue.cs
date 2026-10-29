using System.Collections.Generic;
using Deducted.Interaction;
using UnityEngine;

namespace Deducted.NPC
{
    /// <summary>
    /// A talk with this NPC: Space starts it, the interaction camera moves to Camera Point,
    /// the pages (lines and shops, in any order) are shown at the bottom of the screen.
    /// D = next page, A = previous page (A on the first page leaves).
    /// </summary>
    public class NPCDialogue : Interactable
    {
        [SerializeField] private string speakerName = "VENDOR";
        [SerializeField] private List<DialoguePage> pages = new List<DialoguePage> { new DialoguePage("...") };

        // Old plain lines, turned into Line pages on load.
        [SerializeField, HideInInspector] private List<string> lines = new List<string>();

        [Header("Camera")]
        [Tooltip("Where the interaction camera sits during the talk (position + rotation, blue arrow = view).")]
        [SerializeField] private Transform cameraPoint;
        [SerializeField, Range(10f, 90f)] private float fieldOfView = 35f;

        public string SpeakerName => speakerName;
        public IReadOnlyList<DialoguePage> Pages
        {
            get
            {
                MigrateLines();
                return pages;
            }
        }
        public float FieldOfView => fieldOfView;
        public Transform CameraPoint => cameraPoint;

        protected override string DefaultPrompt => "[Talk]";

        public override bool CanInteract => base.CanInteract && Pages.Count > 0 && !DialogueSystem.IsOpen;

        private void OnValidate() => MigrateLines();

        private void MigrateLines()
        {
            if (lines == null || lines.Count == 0) return;
            // The default "..." page isn't content: replace it.
            if (pages.Count == 1 && pages[0].type == PageType.Line && pages[0].text == "...") pages.Clear();
            foreach (string line in lines) pages.Add(new DialoguePage(line));
            lines.Clear();
        }

        public override void Interact()
        {
            if (DialogueSystem.Instance == null)
            {
                Debug.LogWarning("NPCDialogue: no DialogueSystem in the scene (Deducted > Build Dialogue UI).", this);
                return;
            }
            DialogueSystem.Instance.Open(this);
        }

        /// <summary>Called when the player went past the last page with D (not when leaving with A).</summary>
        public virtual void OnDialogueFinished() { }

        private void OnDrawGizmosSelected()
        {
            if (cameraPoint == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(cameraPoint.position, 0.1f);
            Gizmos.DrawLine(cameraPoint.position, cameraPoint.position + cameraPoint.forward * 0.6f);
        }
    }
}
