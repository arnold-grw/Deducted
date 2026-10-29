using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Deducted.NPC
{
    /// <summary>
    /// Plays one looping idle clip on the NPC, no Animator Controller needed.
    /// Pick any clip as Idle; empty = the default idle from male_npc.fbx.
    /// Put it on the NPC (the Animator can be on a child model).
    /// </summary>
    public class NPCIdle : MonoBehaviour
    {
        public const string DefaultIdlePath = "Assets/_Project/Models/NPC/Male/male_npc.fbx";

        [Tooltip("Empty = the default idle (male_npc).")]
        [SerializeField] private AnimationClip idle;
        [SerializeField] private float speed = 1f;
        [Tooltip("Start at a random point of the clip, so NPCs with the same idle don't move in sync.")]
        [SerializeField] private bool randomStart = true;
        [Tooltip("Empty = first Animator on this object or its children.")]
        [SerializeField] private Animator animator;
        [SerializeField, HideInInspector] private AnimationClip defaultIdle;

        private PlayableGraph graph;

#if UNITY_EDITOR
        // Remember the default clip in the scene, since game code can't load assets by path.
        private void OnValidate()
        {
            if (defaultIdle != null) return;
            foreach (Object asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(DefaultIdlePath))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    defaultIdle = clip;
                    break;
                }
            }
        }
#endif

        private void OnEnable()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            AnimationClip clip = idle != null ? idle : defaultIdle;
            if (animator == null || clip == null)
            {
                Debug.LogWarning("NPCIdle: needs an Animator and an idle clip.", this);
                return;
            }

            animator.applyRootMotion = false;
            AnimationClipPlayable playable = AnimationPlayableUtilities.PlayClip(animator, clip, out graph);
            playable.SetSpeed(speed);
            if (randomStart) playable.SetTime(Random.value * clip.length);
        }

        private void OnDisable()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
