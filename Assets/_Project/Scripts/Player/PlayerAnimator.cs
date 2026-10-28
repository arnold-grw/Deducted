using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Deducted.Player
{
    /// <summary>How the player gets around. Ordered by how injured they are.</summary>
    public enum MovementForm
    {
        Normal = 0,
        Injured = 1,
        InjuredHeavy = 2,
        CrawlAllFours = 3,
        CrawlLow = 4,
    }

    /// <summary>
    /// Drives the AC_Player Animator from PlayerMovement: picks the locomotion form,
    /// matches the clip playback speed to the actual walking speed, and plays
    /// interact / death on request.
    /// </summary>
    [RequireComponent(typeof(PlayerMovement))]
    public class PlayerAnimator : MonoBehaviour
    {
        [System.Serializable]
        public struct FormSettings
        {
            [Tooltip("Movement speed (m/s) at which the clip's feet match the ground. Raise it if feet slide backwards, lower it if they skate forwards.")]
            public float clipSpeed;
            [Tooltip("Multiplier on PlayerMovement's Move Speed while in this form.")]
            public float moveSpeedMultiplier;
            [Tooltip("Seconds to turn toward the move direction. Higher = slower, heavier turning.")]
            [Min(0.01f)] public float turnSmoothTime;

            public FormSettings(float clipSpeed, float moveSpeedMultiplier, float turnSmoothTime)
            {
                this.clipSpeed = clipSpeed;
                this.moveSpeedMultiplier = moveSpeedMultiplier;
                this.turnSmoothTime = turnSmoothTime;
            }
        }

        [SerializeField] private Animator animator;
        [SerializeField] private MovementForm form = MovementForm.Normal;

        [Header("Per form")]
        [SerializeField] private FormSettings normal = new FormSettings(1.5f, 1f, 0.1f);
        [SerializeField] private FormSettings injured = new FormSettings(1.0f, 0.65f, 0.18f);
        [SerializeField] private FormSettings injuredHeavy = new FormSettings(0.8f, 0.45f, 0.28f);
        [SerializeField] private FormSettings crawlAllFours = new FormSettings(0.8f, 0.35f, 0.4f);
        [SerializeField] private FormSettings crawlLow = new FormSettings(0.5f, 0.25f, 0.55f);

        [Header("Actions")]
        [Tooltip("Seconds the player can't move while interacting (the clip plays at 2x, so 3 s -> 1.5 s).")]
        [SerializeField] private float interactDuration = 1.5f;
        [Tooltip("Key that plays the smoke animation (works in builds too).")]
        [SerializeField] private Key smokeKey = Key.R;
        [Tooltip("Seconds the player can't move while smoking. 0 = length of the smoke clip.")]
        [SerializeField] private float smokeDuration = 0f;
        [SerializeField] private float blendDampTime = 0.1f;
        [Tooltip("How fast idle blends into dancing near a speaker.")]
        [SerializeField] private float danceBlendTime = 0.3f;

        [Header("Debug")]
        [Tooltip("Editor only: 1-5 = form, K = death.")]
        [SerializeField] private bool debugKeys = true;

        private static readonly int FormHash = Animator.StringToHash("Form");
        private static readonly int BlendHash = Animator.StringToHash("Blend");
        private static readonly int MotionSpeedHash = Animator.StringToHash("MotionSpeed");
        private static readonly int InteractHash = Animator.StringToHash("Interact");
        private static readonly int DeadHash = Animator.StringToHash("Dead");
        private static readonly int DanceHash = Animator.StringToHash("Dance");
        private static readonly int SmokeHash = Animator.StringToHash("Smoke");

        private PlayerMovement movement;
        private Coroutine actionRoutine;

        public MovementForm Form
        {
            get => form;
            set
            {
                if (!IsDead) form = value;
            }
        }

        /// <summary>Crawling forms have no idle or interact animation and die with death_low.</summary>
        public bool IsCrawling => form >= MovementForm.CrawlAllFours;

        public bool IsDead { get; private set; }
        /// <summary>True while an action (interact, smoke) locks movement.</summary>
        public bool IsInteracting => actionRoutine != null;

        // Components saved before turnSmoothTime existed load it as 0: fill in the defaults once.
        private void OnValidate()
        {
            if (normal.turnSmoothTime <= 0f) normal.turnSmoothTime = 0.1f;
            if (injured.turnSmoothTime <= 0f) injured.turnSmoothTime = 0.18f;
            if (injuredHeavy.turnSmoothTime <= 0f) injuredHeavy.turnSmoothTime = 0.28f;
            if (crawlAllFours.turnSmoothTime <= 0f) crawlAllFours.turnSmoothTime = 0.4f;
            if (crawlLow.turnSmoothTime <= 0f) crawlLow.turnSmoothTime = 0.55f;
        }

        private void Reset()
        {
            animator = GetComponentInChildren<Animator>();
        }

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            if (animator != null)
                animator.applyRootMotion = false;
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (debugKeys) HandleDebugKeys();
#endif
            Keyboard kb = Keyboard.current;
            if (kb != null && smokeKey != Key.None && kb[smokeKey].wasPressedThisFrame) Smoke();

            FormSettings settings = GetSettings(form);
            movement.SpeedMultiplier = settings.moveSpeedMultiplier;
            movement.TurnSmoothTimeOverride = Mathf.Max(settings.turnSmoothTime, 0.01f);

            if (animator == null) return;

            float speed = movement.Speed;
            float clipSpeed = Mathf.Max(settings.clipSpeed, 0.01f);
            float ratio = speed / clipSpeed;

            animator.SetInteger(FormHash, (int)form);
            animator.SetFloat(BlendHash, Mathf.Clamp01(ratio), blendDampTime, Time.deltaTime);

            // Standing forms blend idle -> walk, so idle keeps normal speed and walk speeds up past clipSpeed.
            // Crawling has no idle clip, so it pauses when the player stands still (also on interact).
            float motionSpeed = IsCrawling ? ratio : Mathf.Max(1f, ratio);
            animator.SetFloat(MotionSpeedHash, motionSpeed);

            // Dance instead of idling when standing near a speaker (not while crawling, dead or deaf).
            Deducted.Audio.Hearing hearing = Deducted.Audio.Hearing.Instance;
            bool canHear = hearing == null || hearing.LeftWorks || hearing.RightWorks;
            bool dance = !IsCrawling && !IsDead && canHear && Deducted.Audio.DanceZone.Contains(transform.position);
            animator.SetFloat(DanceHash, dance ? 1f : 0f, danceBlendTime, Time.deltaTime);
        }

        /// <summary>Play the interact animation and lock movement for its duration (called by game code, no key).</summary>
        public void Interact()
        {
            if (IsDead || IsInteracting) return;
            // While crawling there is no animation, only the movement lock.
            actionRoutine = StartCoroutine(ActionRoutine(IsCrawling ? 0 : InteractHash, interactDuration));
        }

        /// <summary>
        /// Light a cigarette: plays the smoke animation and locks movement like interact.
        /// Standing forms only (no crawling clip). Later this can cost money or give effects.
        /// </summary>
        public void Smoke()
        {
            if (IsDead || IsInteracting || IsCrawling || !movement.InputEnabled) return;
            float duration = smokeDuration > 0f ? smokeDuration : ClipLength("smoke", 3f) * 0.9f;
            actionRoutine = StartCoroutine(ActionRoutine(SmokeHash, duration));
        }

        /// <summary>Play the death animation and stop all input. No revives.</summary>
        public void Die()
        {
            if (IsDead) return;
            IsDead = true;
            movement.InputEnabled = false;
            if (animator != null) animator.SetBool(DeadHash, true);
        }

        // trigger 0 = no animation, only the movement lock.
        private IEnumerator ActionRoutine(int trigger, float duration)
        {
            movement.Lock(this);
            if (animator != null && trigger != 0) animator.SetTrigger(trigger);
            yield return new WaitForSeconds(duration);
            movement.Unlock(this);
            actionRoutine = null;
        }

        private float ClipLength(string clipName, float fallback)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return fallback;
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                if (clip != null && clip.name == clipName) return clip.length;
            return fallback;
        }

        private FormSettings GetSettings(MovementForm f)
        {
            switch (f)
            {
                case MovementForm.Injured: return injured;
                case MovementForm.InjuredHeavy: return injuredHeavy;
                case MovementForm.CrawlAllFours: return crawlAllFours;
                case MovementForm.CrawlLow: return crawlLow;
                default: return normal;
            }
        }

#if UNITY_EDITOR
        private void HandleDebugKeys()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame) Form = MovementForm.Normal;
            if (kb.digit2Key.wasPressedThisFrame) Form = MovementForm.Injured;
            if (kb.digit3Key.wasPressedThisFrame) Form = MovementForm.InjuredHeavy;
            if (kb.digit4Key.wasPressedThisFrame) Form = MovementForm.CrawlAllFours;
            if (kb.digit5Key.wasPressedThisFrame) Form = MovementForm.CrawlLow;
            if (kb.kKey.wasPressedThisFrame) Die();
        }
#endif
    }
}
