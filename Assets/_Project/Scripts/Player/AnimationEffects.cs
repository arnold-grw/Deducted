using System.Collections.Generic;
using UnityEngine;

namespace Deducted.Player
{
    /// <summary>
    /// Particle effects started from Animation Events (FBX > Animation tab > Events):
    /// Function "Effect" plays, "StopEffect" stops; the event's String field is the effect name.
    /// Put the particle object where it should appear (e.g. FX_SmokePuff under the head bone at the mouth)
    /// and drag it into Particles. A prefab from the Project window also works: it is spawned under
    /// Attach To (or this object) at start.
    /// Can sit on the player or on a child.
    /// </summary>
    public class AnimationEffects : MonoBehaviour
    {
        [System.Serializable]
        public class Effect
        {
            [Tooltip("Typed into the String field of the Animation Event.")]
            public string name;
            public ParticleSystem particles;
            [Tooltip("Only for prefabs from the Project window: where to spawn them. Empty = this object.")]
            public Transform attachTo;
        }

        [SerializeField] private List<Effect> effects = new List<Effect>();
        [Tooltip("Animator of the player model. Empty = first Animator under the player.")]
        [SerializeField] private Animator animator;
        [Tooltip("Events from clips blended in below this weight are ignored.")]
        [SerializeField, Range(0f, 1f)] private float minClipWeight = 0.25f;

        private void Awake()
        {
            foreach (Effect effect in effects)
            {
                // A prefab asset isn't in the scene yet: spawn a copy.
                if (effect.particles != null && !effect.particles.gameObject.scene.IsValid())
                {
                    Transform parent = effect.attachTo != null ? effect.attachTo : transform;
                    effect.particles = Instantiate(effect.particles, parent, false);
                }
            }

            if (animator == null)
            {
                PlayerMovement movement = GetComponentInParent<PlayerMovement>();
                Transform root = movement != null ? movement.transform : transform;
                animator = root.GetComponentInChildren<Animator>();
            }
            if (animator == null)
            {
                Debug.LogWarning("AnimationEffects: no Animator found.", this);
                return;
            }

            // Animation Events are sent to the Animator's GameObject: forward them to us.
            FootstepEvents relay = animator.GetComponent<FootstepEvents>();
            if (relay == null) relay = animator.gameObject.AddComponent<FootstepEvents>();
            relay.Effects = this;
        }

        /// <summary>"Effect" Animation Event (through FootstepEvents).</summary>
        public void OnAnimationEffect(AnimationEvent e)
        {
            if (e.animatorClipInfo.clip != null && e.animatorClipInfo.weight < minClipWeight) return;
            Play(e.stringParameter);
        }

        public void Play(string effectName)
        {
            ParticleSystem particles = Find(effectName);
            if (particles != null) particles.Play(true);
        }

        public void Stop(string effectName)
        {
            ParticleSystem particles = Find(effectName);
            if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private ParticleSystem Find(string effectName)
        {
            foreach (Effect effect in effects)
                if (string.Equals(effect.name, effectName, System.StringComparison.OrdinalIgnoreCase))
                    return effect.particles;
            Debug.LogWarning($"AnimationEffects: no effect named \"{effectName}\".", this);
            return null;
        }
    }
}
