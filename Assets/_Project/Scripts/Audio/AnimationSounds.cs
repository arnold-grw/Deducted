using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Deducted.Audio
{
    /// <summary>
    /// Sounds from Animation Events for NPCs and enemies (the player uses Footsteps).
    /// Put it on the GameObject with the Animator (e.g. the Collector's model), next to an AudioSource + WorldSound.
    /// - Function "Footstep": a step, only while the NavMeshAgent above is moving.
    /// - Function "Sound": any sound, always plays (e.g. a grab clank).
    /// The event's String field picks a Sound Set by name; empty = the default Clips.
    /// Random clip (never the same twice in a row), random pitch and volume.
    /// </summary>
    public class AnimationSounds : MonoBehaviour
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
        [Tooltip("Empty = AudioSource on this object or a child.")]
        [SerializeField] private AudioSource source;
        [Tooltip("No footsteps below this speed (m/s) of the NavMeshAgent in the parents.")]
        [SerializeField] private float minSpeed = 0.05f;
        [Tooltip("Events from clips blended in below this weight are ignored.")]
        [SerializeField, Range(0f, 1f)] private float minClipWeight = 0.25f;
        [Tooltip("Minimum seconds between two footsteps.")]
        [SerializeField] private float minInterval = 0.15f;

        private readonly SoundSet defaultSet = new SoundSet();
        private NavMeshAgent agent;
        private float lastStepTime = float.MinValue;

        private void Awake()
        {
            if (source == null) source = GetComponentInChildren<AudioSource>();
            if (source == null) Debug.LogWarning("AnimationSounds: no AudioSource found.", this);
            else
            {
                source.playOnAwake = false;
                source.loop = false;
            }
            agent = GetComponentInParent<NavMeshAgent>();
        }

        // Names must match the Function field of the Animation Event.
        private void Footstep(AnimationEvent e)
        {
            if (IsFaded(e)) return;
            if (agent != null && agent.velocity.magnitude < minSpeed) return;
            if (Time.time - lastStepTime < minInterval) return;
            lastStepTime = Time.time;
            Play(e.stringParameter);
        }

        private void Sound(AnimationEvent e)
        {
            if (IsFaded(e)) return;
            Play(e.stringParameter);
        }

        private bool IsFaded(AnimationEvent e) =>
            e.animatorClipInfo.clip != null && e.animatorClipInfo.weight < minClipWeight;

        /// <summary>Plays a sound set by name from code (empty = default Clips).</summary>
        public void Play(string setName)
        {
            if (source == null) return;
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
            Debug.LogWarning($"AnimationSounds: no Sound Set named \"{setName}\".", this);
            return null;
        }
    }
}
