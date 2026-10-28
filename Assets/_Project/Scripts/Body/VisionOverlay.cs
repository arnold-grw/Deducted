using UnityEngine;
using UnityEngine.UI;

namespace Deducted.Body
{
    /// <summary>
    /// Full-screen images that show what the eyes can (not) see.
    /// Put the Images on a Screen Space - Overlay canvas, stretched to the full screen.
    /// - one eye missing: that eye's "missing" image (e.g. black half)
    /// - one eye synthetic: that eye's ad image (e.g. "VISION PREMIUM - BUY NOW")
    /// - both eyes missing: the blind image (black with a small dim circle in the middle)
    /// </summary>
    public class VisionOverlay : MonoBehaviour
    {
        [SerializeField] private PlayerBody body;

        [Header("Left eye")]
        [SerializeField] private Image leftMissing;
        [SerializeField] private Image leftSynthetic;

        [Header("Right eye")]
        [SerializeField] private Image rightMissing;
        [SerializeField] private Image rightSynthetic;

        [Header("Both eyes missing")]
        [SerializeField] private Image blind;

        private void OnEnable()
        {
            if (body == null) body = FindAnyObjectByType<PlayerBody>();
            if (body != null) body.PartChanged += OnPartChanged;
            Refresh();
        }

        private void OnDisable()
        {
            if (body != null) body.PartChanged -= OnPartChanged;
        }

        private void OnPartChanged(BodyPartId id, BodyPartState state)
        {
            if (id == BodyPartId.LeftEye || id == BodyPartId.RightEye)
                Refresh();
        }

        public void Refresh()
        {
            BodyPartState l = body != null ? body.GetState(BodyPartId.LeftEye) : BodyPartState.Organic;
            BodyPartState r = body != null ? body.GetState(BodyPartId.RightEye) : BodyPartState.Organic;
            bool blindNow = l == BodyPartState.Missing && r == BodyPartState.Missing;

            // When fully blind, the blind image replaces the two half images.
            Show(leftMissing, l == BodyPartState.Missing && !blindNow);
            Show(rightMissing, r == BodyPartState.Missing && !blindNow);
            Show(leftSynthetic, l == BodyPartState.Synthetic);
            Show(rightSynthetic, r == BodyPartState.Synthetic);
            Show(blind, blindNow);
        }

        private static void Show(Image image, bool visible)
        {
            if (image != null) image.enabled = visible;
        }
    }
}
