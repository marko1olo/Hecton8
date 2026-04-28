using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Authoring template that binds a flora family to its base meshes and optional VAT payloads.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ProceduralFamily_Flora",
        menuName = "Hecton8/World/Procedural Family Flora",
        order = 111)]
    public sealed class ProceduralFamily_Flora : ScriptableObject
    {
        [System.Serializable]
        public struct VatDescriptor
        {
            [Tooltip("Position VAT sampled per vertex in the flora shader.")]
            public Texture2D positionTexture;

            [Tooltip("Optional normal VAT paired with the position VAT.")]
            public Texture2D normalTexture;

            [Min(1)]
            [Tooltip("Frame count stored vertically in the VAT textures.")]
            public int frameCount;

            [Min(0f)]
            [Tooltip("Playback speed multiplier applied to the VAT phase.")]
            public float playbackSpeed;

            [Min(0f)]
            [Tooltip("Per-instance phase scale multiplied by the source instance identifier.")]
            public float instancePhaseScale;

            [Min(0.0001f)]
            [Tooltip("World-space scale applied to sampled VAT positions.")]
            public float positionScale;
        }

        [Header("Identity")]
        [SerializeField]
        [Tooltip("Stable family identifier used by placement and rendering systems.")]
        private string familyId = "flora.generic";

        [SerializeField]
        [Tooltip("Artist-facing display label for this flora family.")]
        private string familyLabel = "Generic Flora";

        [Header("Geometry")]
        [SerializeField]
        [Tooltip("Base meshes available to the runtime strip/impostor renderer for this family.")]
        private Mesh[] baseMeshes;

        [SerializeField]
        [Tooltip("Uniform size-variance range applied by the scatter/placement systems.")]
        private Vector2 sizeVariance = new Vector2(0.85f, 1.15f);

        [Header("VAT")]
        [SerializeField]
        [Tooltip("Optional VAT payload used when this flora family animates from authored textures instead of analytic sway only.")]
        private VatDescriptor vat;

        /// <summary>Stable family identifier used across procedural placement data.</summary>
        public string FamilyId => familyId;

        /// <summary>Artist-facing label shown in inspectors and authoring tools.</summary>
        public string FamilyLabel => familyLabel;

        /// <summary>Base meshes bound to this flora family.</summary>
        public Mesh[] BaseMeshes => baseMeshes;

        /// <summary>Uniform size-variance range applied to spawned instances.</summary>
        public Vector2 SizeVariance => sizeVariance;

        /// <summary>Optional VAT payload bound to this flora family.</summary>
        public VatDescriptor Vat => vat;

#if UNITY_EDITOR
        private void OnValidate()
        {
            sizeVariance.x = Mathf.Max(0.01f, sizeVariance.x);
            sizeVariance.y = Mathf.Max(sizeVariance.x, sizeVariance.y);
            vat.frameCount = Mathf.Max(1, vat.frameCount);
            vat.playbackSpeed = Mathf.Max(0f, vat.playbackSpeed);
            vat.instancePhaseScale = Mathf.Max(0f, vat.instancePhaseScale);
            vat.positionScale = Mathf.Max(0.0001f, vat.positionScale);
        }
#endif
    }
}
