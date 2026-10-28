using Deducted.Body;
using UnityEngine;

namespace Deducted.Audio
{
    /// <summary>
    /// The player's ears. WorldSound sources ask this which side can hear.
    /// A missing ear mutes that side for all world sounds, like a broken headphone.
    /// Synthetic ears hear normally (ads come later). Non-spatial sounds (menu, ambience)
    /// are plain AudioSources and are not affected.
    /// </summary>
    public class Hearing : MonoBehaviour
    {
        public static Hearing Instance { get; private set; }

        [SerializeField] private PlayerBody body;
        [Tooltip("Where the ears are. Empty = this transform.")]
        [SerializeField] private Transform head;
        [Tooltip("Left/right come from this camera's view, so a sound on the right of the screen is heard on the right. Empty = Camera.main.")]
        [SerializeField] private Camera viewCamera;

        /// <summary>Position of the ears in the world.</summary>
        public Vector3 Position => head != null ? head.position : transform.position;

        /// <summary>The ears' "right" direction: the camera's right, flattened to the ground.</summary>
        public Vector3 Right
        {
            get
            {
                Camera cam = viewCamera != null ? viewCamera : Camera.main;
                if (cam == null) return transform.right;
                Vector3 right = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up);
                return right.sqrMagnitude > 0.0001f ? right.normalized : transform.right;
            }
        }
        public bool LeftWorks => body == null || body.Works(BodyPartId.LeftEar);
        public bool RightWorks => body == null || body.Works(BodyPartId.RightEar);

        private void Awake()
        {
            Instance = this;
            if (body == null) body = GetComponent<PlayerBody>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
