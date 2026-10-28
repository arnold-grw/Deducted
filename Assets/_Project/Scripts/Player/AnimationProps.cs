using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Deducted.Player
{
    /// <summary>
    /// Props that appear during an animation (e.g. a cigarette under the head bone).
    /// Animation Event Function "ShowProp": String = prop name, Float = seconds visible (0 = the prop's Duration).
    /// Function "HideProp": String = prop name, hides it right away.
    /// Props start hidden (GameObject disabled). Can sit on the player or on a child.
    /// </summary>
    public class AnimationProps : MonoBehaviour
    {
        [System.Serializable]
        public class Prop
        {
            [Tooltip("Typed into the String field of the Animation Event.")]
            public string name;
            [Tooltip("Object in the scene, e.g. a small cylinder under mixamorig:Head.")]
            public GameObject target;
            [Tooltip("Seconds visible when the event's Float is 0.")]
            public float duration = 5f;
            [System.NonSerialized] public Coroutine hideRoutine;
        }

        [SerializeField] private List<Prop> props = new List<Prop>();
        [Tooltip("Animator of the player model. Empty = first Animator under the player.")]
        [SerializeField] private Animator animator;

        private void Awake()
        {
            foreach (Prop prop in props)
                if (prop.target != null) prop.target.SetActive(false);

            if (animator == null)
            {
                PlayerMovement movement = GetComponentInParent<PlayerMovement>();
                Transform root = movement != null ? movement.transform : transform;
                animator = root.GetComponentInChildren<Animator>();
            }
            if (animator == null)
            {
                Debug.LogWarning("AnimationProps: no Animator found.", this);
                return;
            }

            // Animation Events are sent to the Animator's GameObject: forward them to us.
            FootstepEvents relay = animator.GetComponent<FootstepEvents>();
            if (relay == null) relay = animator.gameObject.AddComponent<FootstepEvents>();
            relay.Props = this;
        }

        /// <summary>Show a prop, then hide it after seconds (0 = its Duration).</summary>
        public void Show(string propName, float seconds = 0f)
        {
            Prop prop = Find(propName);
            if (prop == null || prop.target == null) return;

            if (prop.hideRoutine != null) StopCoroutine(prop.hideRoutine);
            prop.target.SetActive(true);
            prop.hideRoutine = StartCoroutine(HideAfter(prop, seconds > 0f ? seconds : prop.duration));
        }

        public void Hide(string propName)
        {
            Prop prop = Find(propName);
            if (prop == null || prop.target == null) return;

            if (prop.hideRoutine != null) StopCoroutine(prop.hideRoutine);
            prop.hideRoutine = null;
            prop.target.SetActive(false);
        }

        private IEnumerator HideAfter(Prop prop, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            prop.target.SetActive(false);
            prop.hideRoutine = null;
        }

        private Prop Find(string propName)
        {
            foreach (Prop prop in props)
                if (string.Equals(prop.name, propName, System.StringComparison.OrdinalIgnoreCase))
                    return prop;
            Debug.LogWarning($"AnimationProps: no prop named \"{propName}\".", this);
            return null;
        }
    }
}
