using System.Collections.Generic;
using UnityEngine;

namespace Deducted.Player
{
    /// <summary>
    /// Sounds driven by Animation Events (FBX > Animation tab > Events), set up per clip:
    /// - Function "Footstep": a step. Only while walking (not idle, crawling or dead).
    /// - Function "Sound": any sound, always plays (e.g. an interact clank).
    /// The event's String field picks a Sound Set by name (e.g. "mech"); empty = the default Clips.
    /// Random clip (never the same twice in a row), random pitch and volume.
    /// Can sit on the player or on a child (e.g. an AudioEmitter).
    /// Add WorldSound to the same AudioSource so missing ears apply.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class Footsteps : MonoBehaviour
    {
        [System.Serializable]
        public class SoundSet
        {
            [Tooltip("Typed into the String field of the Animation Event.")]
            public string name;
            public List<AudioClip> clips = new List<AudioClip>();
            public Vector2 volumeRange = new Vector2(0.8f, 1f);
            public Vector2 pitchRange = new Vector2(0.9f, 1.1f);
            [System.NonSerialized] public int lastClip = -1;
        }

        [Header("Default (event String empty)")]
        [SerializeField] private List<AudioClip> clips = new List<AudioClip>();
        [SerializeField] private Vector2 volumeRange = new Vector2(0.8f, 1f);
        [SerializeField] private Vector2 pitchRange = new Vector2(0.9f, 1.1f);

        [Header("Named sets (event String = name)")]
        [SerializeField] private List<SoundSet> soundSets = new List<SoundSet>();

        [Header("Events")]
        [Tooltip("Animator of the player model. Empty = first Animator under the player.")]
        [SerializeField] private Animator animator;
        [Tooltip("No footsteps below this speed (m/s), e.g. idle or dancing.")]
        [SerializeField] private float minSpeed = 0.05f;
        [Tooltip("Events from clips blended in below this weight are ignored (e.g. the old walk while switching forms).")]
        [SerializeField, Range(0f, 1f)] private float minClipWeight = 0.25f;
        [Tooltip("Minimum seconds between two footsteps, so two blended walk clips can't double up.")]
        [SerializeField] private float minInterval = 0.15f;

        private readonly SoundSet defaultSet = new SoundSet();
        private AudioSource source;
        private PlayerMovement movement;
        private PlayerAnimator playerAnimator;
        private float lastStepTime = float.MinValue;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            // Works on the player itself or on a child (e.g. an AudioEmitter).
            movement = GetComponentInParent<PlayerMovement>();
            playerAnimator = GetComponentInParent<PlayerAnimator>();
            if (animator == null)
            {
                Transform root = movement != null ? movement.transform : transform;
                animator = root.GetComponentInChildren<Animator>();
            }
            if (animator == null) Debug.LogWarning("Footsteps: no Animator found.", this);
            if (movement == null) Debug.LogWarning("Footsteps: no PlayerMovement in parents.", this);

            // Animation Events are sent to the Animator's GameObject: forward them to us.
            if (animator != null)
            {
                FootstepEvents relay = animator.GetComponent<FootstepEvents>();
                if (relay == null) relay = animator.gameObject.AddComponent<FootstepEvents>();
                relay.Target = this;
            }
        }

        private bool IsWalking =>
            movement != null && movement.Speed >= minSpeed
            && (playerAnimator == null || (!playerAnimator.IsCrawling && !playerAnimator.IsDead));

        /// <summary>"Footstep" Animation Event (through FootstepEvents).</summary>
        public void OnAnimationFootstep(AnimationEvent e)
        {
            if (!IsWalking || IsFaded(e)) return;
            if (Time.time - lastStepTime < minInterval) return;
            lastStepTime = Time.time;
            Play(e.stringParameter);
        }

        /// <summary>"Sound" Animation Event (through FootstepEvents).</summary>
        public void OnAnimationSound(AnimationEvent e)
        {
            if (IsFaded(e)) return;
            Play(e.stringParameter);
        }

        private bool IsFaded(AnimationEvent e) =>
            e.animatorClipInfo.clip != null && e.animatorClipInfo.weight < minClipWeight;

        private void Play(string setName)
        {
            SoundSet set = FindSet(setName);
            if (set == null || set.clips.Count == 0) return;

            int index = Random.Range(0, set.clips.Count);
            if (set.clips.Count > 1 && index == set.lastClip)
                index = (index + 1 + Random.Range(0, set.clips.Count - 1)) % set.clips.Count;
            set.lastClip = index;

            source.pitch = Random.Range(set.pitchRange.x, set.pitchRange.y);
            source.PlayOneShot(set.clips[index], Random.Range(set.volumeRange.x, set.volumeRange.y));
        }

        private SoundSet FindSet(string setName)
        {
            if (string.IsNullOrEmpty(setName))
            {
                defaultSet.clips = clips;
                defaultSet.volumeRange = volumeRange;
                defaultSet.pitchRange = pitchRange;
                return defaultSet;
            }
            foreach (SoundSet set in soundSets)
                if (string.Equals(set.name, setName, System.StringComparison.OrdinalIgnoreCase))
                    return set;
            Debug.LogWarning($"Footsteps: no Sound Set named \"{setName}\".", this);
            return null;
        }
    }
}
