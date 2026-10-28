using UnityEngine;

namespace Deducted.Player
{
    /// <summary>
    /// Receives the player's Animation Events on the Animator's GameObject and forwards them:
    /// "Footstep" and "Sound" to Footsteps, "Effect" and "StopEffect" to AnimationEffects,
    /// "ShowProp" and "HideProp" to AnimationProps.
    /// Added automatically at runtime; no setup needed.
    /// </summary>
    public class FootstepEvents : MonoBehaviour
    {
        public Footsteps Target { get; set; }
        public AnimationEffects Effects { get; set; }
        public AnimationProps Props { get; set; }

        // Names must match the Function field of the Animation Event.
        private void Footstep(AnimationEvent e)
        {
            if (Target != null) Target.OnAnimationFootstep(e);
        }

        private void Sound(AnimationEvent e)
        {
            if (Target != null) Target.OnAnimationSound(e);
        }

        private void Effect(AnimationEvent e)
        {
            if (Effects != null) Effects.OnAnimationEffect(e);
        }

        private void StopEffect(AnimationEvent e)
        {
            if (Effects != null) Effects.Stop(e.stringParameter);
        }

        private void ShowProp(AnimationEvent e)
        {
            if (Props != null) Props.Show(e.stringParameter, e.floatParameter);
        }

        private void HideProp(AnimationEvent e)
        {
            if (Props != null) Props.Hide(e.stringParameter);
        }
    }
}
