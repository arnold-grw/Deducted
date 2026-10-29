using System.Collections.Generic;
using Deducted.Body;
using Deducted.Economy;
using Deducted.NPC;
using Deducted.Player;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Deducted.Enemy
{
    /// <summary>
    /// The Collector. Balance positive: wanders between random NavMesh points and waits a random time at each.
    /// Balance negative: hunts the player (always knows where they are). Selling something in time calls it off.
    /// On reaching the player it takes the next organic part (Take Order: ears, eyes, legs) and pays
    /// Payout Fraction of its VITALIS price, then stands still (no turning) for Cooldown seconds.
    /// No organic parts left = the player dies.
    /// Animation: plays Idle / Walk / Reclaim clips on the model's Animator via Playables (no controller).
    /// Missing Idle / Reclaim clips = the walk clip's first frame.
    /// Needs a baked NavMesh (NavMeshSurface).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class Collector : MonoBehaviour
    {
        public enum State { Wander, Chase, Cooldown, Done }

        [Header("Wander")]
        [SerializeField] private float wanderSpeed = 1.2f;
        [Tooltip("Random wait (seconds) at each wander point: x = min, y = max.")]
        [SerializeField] private Vector2 waitTime = new Vector2(2f, 6f);
        [Tooltip("0 = anywhere on the NavMesh. Otherwise only points within this distance of the start position.")]
        [SerializeField, Min(0f)] private float wanderRadius = 0f;

        [Header("Chase")]
        [SerializeField] private float chaseSpeed = 2.8f;
        [Tooltip("Distance (m) at which it grabs the player.")]
        [SerializeField] private float reachDistance = 1.3f;

        [Header("Reclaim")]
        [Tooltip("Share of the VITALIS price paid per taken part.")]
        [SerializeField, Range(0f, 1f)] private float payoutFraction = 0.5f;
        [Tooltip("Seconds it stands still after taking a part.")]
        [SerializeField] private float cooldown = 5f;
        [SerializeField] private List<BodyPartId> takeOrder = new List<BodyPartId>
        {
            BodyPartId.LeftEar, BodyPartId.RightEar,
            BodyPartId.LeftEye, BodyPartId.RightEye,
            BodyPartId.LeftLeg, BodyPartId.RightLeg,
        };
        [Tooltip("Played from the Collector when it grabs the player (each part and the kill). Spatial: heard like its steps.")]
        [SerializeField] private AudioClip catchSound;
        [SerializeField, Range(0f, 1f)] private float catchVolume = 1f;
        [Tooltip("AudioSource with WorldSound for the catch sound. Empty = the one on the model (used by AnimationSounds).")]
        [SerializeField] private AudioSource catchSource;

        [Tooltip("Black screen when it takes a part: seconds fully black, then seconds to fade out.")]
        [SerializeField] private float blackoutHold = 0.15f;
        [SerializeField] private float blackoutFade = 1.2f;

        [Header("Animation")]
        [Tooltip("Animator on the model (child \"collector\"). Empty = first Animator in children.")]
        [SerializeField] private Animator animator;
        [Tooltip("Optional. Empty = first frame of the walk clip when standing.")]
        [SerializeField] private AnimationClip idleClip;
        [SerializeField] private AnimationClip walkClip;
        [Tooltip("Optional, plays once when it takes a part. Empty = it just stands (idle).")]
        [SerializeField] private AnimationClip reclaimClip;
        [Tooltip("Walking speed (m/s) at which the walk clip's feet match the ground. Feet slide backwards = raise it.")]
        [SerializeField] private float walkClipSpeed = 1.2f;
        [Tooltip("How fast idle and walk blend into each other.")]
        [SerializeField] private float blendSpeed = 6f;

        [Header("Debug")]
        [SerializeField] private bool logStates = false;

        private NavMeshAgent agent;
        private PlayerBody body;
        private PlayerAnimator playerAnimator;
        private Transform player;
        private Vector3 home;
        private State state = State.Wander;
        private float waitUntil = -1f;
        private float cooldownUntil;
        private Vector3 lastTargetPos;
        private bool warnedNoNavMesh;
        private NavMeshObstacle playerObstacle;
        private Deducted.UI.BodyOverview overview;

        // Whole-NavMesh sampling (wanderRadius 0).
        private Vector3[] navVerts;
        private int[] navIndices;
        private float[] navAreas;
        private float navTotalArea;

        // Animation.
        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private AnimationClipPlayable walkPlayable;
        private AnimationClipPlayable reclaimPlayable;
        private float walkBlend;
        private float reclaimEnd = -1f;
        private float reclaimWeight;

        public State CurrentState => state;

        private void Reset()
        {
            animator = GetComponentInChildren<Animator>();
        }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (catchSource == null) catchSource = GetComponentInChildren<AudioSource>();
            home = transform.position;
        }

        private void Start()
        {
            body = FindFirstObjectByType<PlayerBody>();
            playerAnimator = FindFirstObjectByType<PlayerAnimator>();
            PlayerMovement movement = FindFirstObjectByType<PlayerMovement>();
            player = movement != null ? movement.transform : body != null ? body.transform : null;
            if (player != null) playerObstacle = player.GetComponentInChildren<NavMeshObstacle>();
            overview = FindFirstObjectByType<Deducted.UI.BodyOverview>(FindObjectsInactive.Include);
            if (player == null) Debug.LogWarning("Collector: no player found.", this);

            if (wanderRadius <= 0f) CacheNavMesh();
            BuildGraph();
            StartWander();
        }

        private void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }

        private void Update()
        {
            if (Deducted.UI.PauseMenu.IsPaused) return; // frozen (time is stopped anyway)
            UpdateState();
            UpdateAnimation();
        }

        // ---------------------------------------------------------------- behaviour

        private bool Hunting =>
            player != null && BalanceManager.Instance != null && BalanceManager.Instance.IsNegative;

        private void UpdateState()
        {
            // While it hunts the player, the player's NavMeshObstacle (so it walks around them when wandering)
            // is off: otherwise the hole around the player can stop it short of them.
            if (playerObstacle != null)
            {
                bool hunting = state == State.Chase || state == State.Cooldown;
                if (playerObstacle.enabled == hunting) playerObstacle.enabled = !hunting;
            }

            if (state == State.Done) return;
            if (!agent.isOnNavMesh)
            {
                if (!warnedNoNavMesh) Debug.LogWarning("Collector is not on a NavMesh. Bake a NavMeshSurface.", this);
                warnedNoNavMesh = true;
                return;
            }
            if (playerAnimator != null && playerAnimator.IsDead)
            {
                SetState(State.Done);
                agent.isStopped = true;
                return;
            }

            switch (state)
            {
                case State.Cooldown:
                    if (Time.time < cooldownUntil) break;
                    if (Hunting) StartChase();
                    else StartWander();
                    break;

                case State.Chase:
                    if (!Hunting)
                    {
                        StartWander(); // sold something in time
                        break;
                    }
                    // Always knows where the player is; only re-path when they moved a bit.
                    if ((player.position - lastTargetPos).sqrMagnitude > 0.04f) SetTarget(player.position);
                    if (InReach()) ReachPlayer();
                    break;

                case State.Wander:
                    if (Hunting)
                    {
                        StartChase();
                        break;
                    }
                    if (agent.pathPending || agent.remainingDistance > agent.stoppingDistance + 0.05f) break;
                    if (waitUntil < 0f) waitUntil = Time.time + Random.Range(waitTime.x, waitTime.y);
                    else if (Time.time >= waitUntil) PickWanderPoint();
                    break;
            }
        }

        private void StartWander()
        {
            SetState(State.Wander);
            agent.isStopped = false;
            agent.speed = wanderSpeed;
            agent.stoppingDistance = 0.2f;
            PickWanderPoint();
        }

        private void StartChase()
        {
            SetState(State.Chase);
            agent.isStopped = false;
            agent.speed = chaseSpeed;
            agent.stoppingDistance = reachDistance * 0.8f;
            SetTarget(player.position);
        }

        private void SetTarget(Vector3 position)
        {
            lastTargetPos = position;
            agent.SetDestination(position);
        }

        private bool InReach()
        {
            Vector3 delta = player.position - transform.position;
            if (Mathf.Abs(delta.y) > 2f) return false; // other floor
            delta.y = 0f;
            return delta.magnitude <= reachDistance;
        }

        private void ReachPlayer()
        {
            if (DialogueSystem.Instance != null) DialogueSystem.Instance.ForceClose();
            if (overview != null && overview.IsOpen) overview.SetOpen(false); // out of the Tab menu too
            if (catchSource != null && catchSound != null) catchSource.PlayOneShot(catchSound, catchVolume);

            BodyPartId? part = NextPart();
            if (part == null)
            {
                // Nothing organic left to take.
                if (playerAnimator != null)
                {
                    playerAnimator.CancelAction();
                    playerAnimator.Die();
                }
                SetState(State.Done);
                agent.isStopped = true;
                return;
            }

            // Before the reclaim, so the hurt animation (lost eye / ear) isn't cancelled right away.
            if (playerAnimator != null) playerAnimator.CancelAction();
            body.Reclaim(part.Value, payoutFraction);
            PlayReclaim();
            Deducted.UI.ScreenFade.Flash(blackoutHold, blackoutFade);

            SetState(State.Cooldown);
            cooldownUntil = Time.time + cooldown;
            agent.isStopped = true;
            agent.ResetPath();
        }

        private BodyPartId? NextPart()
        {
            if (body == null) return null;
            foreach (BodyPartId id in takeOrder)
                if (body.GetOwnedState(id) == BodyPartState.Organic) return id;
            return null;
        }

        private void SetState(State next)
        {
            if (logStates && next != state) Debug.Log($"Collector: {state} -> {next}", this);
            state = next;
        }

        // ---------------------------------------------------------------- wander points

        private void PickWanderPoint()
        {
            waitUntil = -1f;
            var path = new NavMeshPath();
            for (int i = 0; i < 15; i++)
            {
                if (!TryRandomPoint(out Vector3 point)) continue;
                if (!agent.CalculatePath(point, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                agent.SetPath(path);
                return;
            }
            // Nothing reachable found: wait and try again.
            waitUntil = Time.time + waitTime.x;
        }

        private bool TryRandomPoint(out Vector3 point)
        {
            if (wanderRadius > 0f)
            {
                Vector3 guess = home + Random.insideUnitSphere * wanderRadius;
                if (NavMesh.SamplePosition(guess, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
                {
                    point = hit.position;
                    return true;
                }
                point = default;
                return false;
            }

            if (navTotalArea <= 0f) CacheNavMesh();
            if (navTotalArea <= 0f)
            {
                point = default;
                return false;
            }

            // Pick a triangle weighted by its area, then a random point inside it: even spread over the surface.
            float r = Random.value * navTotalArea;
            int lo = 0, hi = navAreas.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (navAreas[mid] < r) lo = mid + 1;
                else hi = mid;
            }
            Vector3 a = navVerts[navIndices[lo * 3]];
            Vector3 b = navVerts[navIndices[lo * 3 + 1]];
            Vector3 c = navVerts[navIndices[lo * 3 + 2]];
            float u = Random.value, v = Random.value;
            if (u + v > 1f)
            {
                u = 1f - u;
                v = 1f - v;
            }
            point = a + u * (b - a) + v * (c - a);
            return true;
        }

        private void CacheNavMesh()
        {
            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            navVerts = tri.vertices;
            navIndices = tri.indices;
            int count = navIndices.Length / 3;
            navAreas = new float[count]; // running total, for the weighted pick
            navTotalArea = 0f;
            for (int i = 0; i < count; i++)
            {
                Vector3 a = navVerts[navIndices[i * 3]];
                Vector3 b = navVerts[navIndices[i * 3 + 1]];
                Vector3 c = navVerts[navIndices[i * 3 + 2]];
                navTotalArea += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                navAreas[i] = navTotalArea;
            }
        }

        // ---------------------------------------------------------------- animation

        private void BuildGraph()
        {
            if (animator == null)
            {
                Debug.LogWarning("Collector: no Animator on the model.", this);
                return;
            }
            animator.applyRootMotion = false; // the NavMeshAgent moves it

            graph = PlayableGraph.Create("Collector");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            mixer = AnimationMixerPlayable.Create(graph, 3);
            AnimationPlayableOutput.Create(graph, "Animation", animator).SetSourcePlayable(mixer);

            if (idleClip != null) graph.Connect(AnimationClipPlayable.Create(graph, idleClip), 0, mixer, 0);
            else if (walkClip != null)
            {
                // No idle clip: stand in the walk clip's first frame.
                AnimationClipPlayable pose = AnimationClipPlayable.Create(graph, walkClip);
                pose.SetTime(0);
                pose.SetSpeed(0);
                graph.Connect(pose, 0, mixer, 0);
            }
            if (walkClip != null)
            {
                walkPlayable = AnimationClipPlayable.Create(graph, walkClip);
                graph.Connect(walkPlayable, 0, mixer, 1);
            }
            if (reclaimClip != null)
            {
                reclaimPlayable = AnimationClipPlayable.Create(graph, reclaimClip);
                reclaimPlayable.SetSpeed(0);
                graph.Connect(reclaimPlayable, 0, mixer, 2);
            }
            graph.Play();
        }

        private void PlayReclaim()
        {
            if (!reclaimPlayable.IsValid()) return;
            reclaimPlayable.SetTime(0);
            reclaimPlayable.SetSpeed(1);
            reclaimEnd = Time.time + reclaimClip.length;
        }

        private void UpdateAnimation()
        {
            if (!graph.IsValid()) return;

            float speed = agent.velocity.magnitude;
            float ratio = speed / Mathf.Max(walkClipSpeed, 0.01f);
            walkBlend = Mathf.MoveTowards(walkBlend, Mathf.Clamp01(ratio), blendSpeed * Time.deltaTime);
            // Standing = walk clip at speed 0, so its footstep events don't fire.
            if (walkPlayable.IsValid())
            {
                walkPlayable.SetSpeed(ratio);
                // Fully standing: start the next walk from the first frame again (matches the standing pose).
                if (walkBlend <= 0f) walkPlayable.SetTime(0);
            }

            bool reclaiming = reclaimPlayable.IsValid() && Time.time < reclaimEnd;
            if (!reclaiming && reclaimPlayable.IsValid()) reclaimPlayable.SetSpeed(0);
            reclaimWeight = Mathf.MoveTowards(reclaimWeight, reclaiming ? 1f : 0f, blendSpeed * Time.deltaTime);

            float rest = 1f - reclaimWeight;
            float walkWeight = rest * walkBlend;
            mixer.SetInputWeight(0, rest - walkWeight);
            mixer.SetInputWeight(1, walkClip != null ? walkWeight : 0f);
            mixer.SetInputWeight(2, reclaimWeight);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, reachDistance);
            if (wanderRadius > 0f)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(Application.isPlaying ? home : transform.position, wanderRadius);
            }
        }
    }
}
