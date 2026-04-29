using System;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Editor-authored template for baking a compound submarine collision rig from boxes and capsules.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Submarine/Submarine Compound Collider Authoring")]
    public sealed class SubmarineCompoundColliderAuthoring : MonoBehaviour
    {
        [Serializable]
        public struct BoxShape
        {
            [Tooltip("Optional debug label for this collider part.")]
            public string Name;

            [Tooltip("Local-space center offset in the submarine root frame.")]
            public Vector3 Center;

            [Tooltip("Local-space box dimensions in meters.")]
            public Vector3 Size;

            [Tooltip("Optional physic material applied to this generated collider.")]
            public PhysicMaterial Material;

            [Tooltip("Whether the generated collider should be a trigger.")]
            public bool IsTrigger;
        }

        [Serializable]
        public struct CapsuleShape
        {
            [Tooltip("Optional debug label for this collider part.")]
            public string Name;

            [Tooltip("Local-space center offset in the submarine root frame.")]
            public Vector3 Center;

            [Tooltip("Capsule radius in meters.")]
            public float Radius;

            [Tooltip("Capsule total height in meters.")]
            public float Height;

            [Tooltip("Capsule axis. 0 = X, 1 = Y, 2 = Z.")]
            [Range(0, 2)] public int Direction;

            [Tooltip("Optional physic material applied to this generated collider.")]
            public PhysicMaterial Material;

            [Tooltip("Whether the generated collider should be a trigger.")]
            public bool IsTrigger;
        }

        [Header("── Generation ───────────────────")]
        [Tooltip("Name used for the generated collider root beneath this submarine.")]
        [SerializeField] private string generatedRootName = "__CompoundColliders";

        [Tooltip("When enabled, the baker clears previously generated colliders before rebuilding.")]
        [SerializeField] private bool replaceExistingGeneratedColliders = true;

        [Header("── Box Segments ─────────────────")]
        [SerializeField] private BoxShape[] boxShapes = Array.Empty<BoxShape>();

        [Header("── Capsule Segments ─────────────")]
        [SerializeField] private CapsuleShape[] capsuleShapes = Array.Empty<CapsuleShape>();

        /// <summary>Name used for the generated collider root beneath this submarine.</summary>
        public string GeneratedRootName => string.IsNullOrWhiteSpace(generatedRootName) ? "__CompoundColliders" : generatedRootName;

        /// <summary>True when generated colliders should be cleared before rebuilding.</summary>
        public bool ReplaceExistingGeneratedColliders => replaceExistingGeneratedColliders;

        /// <summary>Authored box-shape definitions.</summary>
        public BoxShape[] BoxShapes => boxShapes;

        /// <summary>Authored capsule-shape definitions.</summary>
        public CapsuleShape[] CapsuleShapes => capsuleShapes;
    }
}
