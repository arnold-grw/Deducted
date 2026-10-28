using System.Collections.Generic;
using UnityEngine;

namespace Deducted.Body
{
    /// <summary>
    /// Shows the body parts on the model: a Missing part's bone is scaled to 0, so everything below it
    /// collapses into that joint. Organic or Synthetic shows it again (e.g. buying a synthetic leg
    /// after losing it). Reads PlayerBody every frame. Put it on the player and drag the bones in.
    /// </summary>
    public class BodyVisuals : MonoBehaviour
    {
        [System.Serializable]
        public class PartBone
        {
            public BodyPartId part;
            [Tooltip("Bone from the model's hierarchy (e.g. mixamorig:RightLeg = shin, mixamorig:RightFoot = foot). " +
                     "It and everything below it gets scaled to 0.")]
            public Transform bone;
            [Tooltip("Testing: hide it even when the part works.")]
            public bool testHidden;
        }

        [SerializeField] private List<PartBone> parts = new List<PartBone>
        {
            new PartBone { part = BodyPartId.LeftLeg },
            new PartBone { part = BodyPartId.RightLeg },
        };
        [Tooltip("Empty = found on the player.")]
        [SerializeField] private PlayerBody body;

        private readonly Dictionary<PartBone, Vector3> originalScales = new Dictionary<PartBone, Vector3>();

        private void Awake()
        {
            if (body == null) body = GetComponentInParent<PlayerBody>();
            if (body == null) body = GetComponentInChildren<PlayerBody>();
            if (body == null) Debug.LogWarning("BodyVisuals: no PlayerBody found.", this);

            foreach (PartBone entry in parts)
            {
                if (entry.bone == null) Debug.LogWarning($"BodyVisuals: no bone set for {entry.part}.", this);
                else originalScales[entry] = entry.bone.localScale;
            }
        }

        // After the Animator, so the pose can't overwrite it.
        private void LateUpdate()
        {
            foreach (PartBone entry in parts)
            {
                if (entry.bone == null) continue;
                bool visible = !entry.testHidden && (body == null || body.Works(entry.part));
                entry.bone.localScale = visible ? originalScales[entry] : Vector3.zero;
            }
        }
    }
}
