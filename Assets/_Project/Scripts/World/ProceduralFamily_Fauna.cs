using System.Runtime.InteropServices;
using Hecton8.Systems.AI;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Authoring template for procedural fauna families.
    /// Exports a blittable runtime descriptor for SOA/boid lanes without carrying managed authoring state into simulation.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ProceduralFamily_Fauna",
        menuName = "Hecton8/World/Procedural Family Fauna",
        order = 113)]
    public sealed class ProceduralFamily_Fauna : ScriptableObject
    {
        [System.Serializable]
        public struct VatDescriptor
        {
            [Tooltip("Position VAT sampled by the fauna shader for this family.")]
            public Texture2D positionTexture;

            [Tooltip("Optional normal VAT paired with the position VAT texture.")]
            public Texture2D normalTexture;

            [Min(1)]
            [Tooltip("Frame count stored in the VAT textures.")]
            public int frameCount;

            [Min(0f)]
            [Tooltip("Playback speed multiplier applied to the VAT timeline.")]
            public float playbackSpeed;

            [Tooltip("Per-family VAT phase offset payload consumed by the runtime descriptor.")]
            public Vector4 phaseOffsetScale;
        }

        [StructLayout(LayoutKind.Explicit, Size = 56)]
        public struct RuntimeDescriptor
        {
            [FieldOffset(0)]
            public int FamilyHashId;
            [FieldOffset(4)]
            public float MinimumScale;
            [FieldOffset(8)]
            public float MaximumScale;
            [FieldOffset(12)]
            public float VatPlaybackSpeed;
            [FieldOffset(16)]
            public float4 VatPhaseOffsetScale;
            [FieldOffset(32)]
            public float4 VatPositionScaleBias;
            [FieldOffset(48)]
            public int ThreatClass;
            [FieldOffset(52)]
            public int Reserved0;
        }

        [Header("Identity")]
        [SerializeField]
        [Tooltip("Stable family identifier used by spawn tables, sector paging and fauna registries.")]
        private string familyId = "fauna.family.generic";

        [SerializeField]
        [Tooltip("Artist-facing label shown in tooling and authoring inspectors.")]
        private string familyLabel = "Generic Fauna";

        [Header("Scale")]
        [SerializeField]
        [Tooltip("Uniform scale variance applied by procedural fauna spawners.")]
        private Vector2 scaleVariance = new Vector2(0.85f, 1.15f);

        [Header("VAT")]
        [SerializeField]
        [Tooltip("Optional VAT payload bound to this fauna family.")]
        private VatDescriptor vat;

        [SerializeField]
        [Tooltip("World-space VAT position scale/bias payload: xy = scale, zw = bias.")]
        private Vector4 vatPositionScaleBias = new Vector4(1f, 1f, 0f, 0f);

        [Header("Threat")]
        [SerializeField]
        [Tooltip("Encounter/boid threat class exported into runtime spawn descriptors.")]
        private EncounterThreatClass boidThreatClass = EncounterThreatClass.Drone;

        /// <summary>Stable family identifier used across procedural fauna systems.</summary>
        public string FamilyId => familyId;

        /// <summary>Artist-facing family label for tooling.</summary>
        public string FamilyLabel => familyLabel;

        /// <summary>Configured scale variance range for spawned fauna in this family.</summary>
        public Vector2 ScaleVariance => scaleVariance;

        /// <summary>Optional VAT payload bound to this fauna family.</summary>
        public VatDescriptor Vat => vat;

        /// <summary>Threat class exported into boid/encounter runtime descriptors.</summary>
        internal EncounterThreatClass BoidThreatClass => boidThreatClass;

        /// <summary>Builds the blittable runtime descriptor consumed by fauna SOA lanes.</summary>
        public RuntimeDescriptor BuildRuntimeDescriptor()
        {
            return new RuntimeDescriptor
            {
                FamilyHashId = string.IsNullOrWhiteSpace(familyId) ? 0 : Hecton.Localization.LocHash.Compute(familyId),
                MinimumScale = math.max(0.01f, scaleVariance.x),
                MaximumScale = math.max(math.max(0.01f, scaleVariance.x), scaleVariance.y),
                VatPlaybackSpeed = math.max(0f, vat.playbackSpeed),
                VatPhaseOffsetScale = new float4(
                    vat.phaseOffsetScale.x,
                    vat.phaseOffsetScale.y,
                    vat.phaseOffsetScale.z,
                    vat.phaseOffsetScale.w),
                VatPositionScaleBias = new float4(
                    vatPositionScaleBias.x,
                    vatPositionScaleBias.y,
                    vatPositionScaleBias.z,
                    vatPositionScaleBias.w),
                ThreatClass = (int)boidThreatClass,
                Reserved0 = 0
            };
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(familyId) && !string.IsNullOrWhiteSpace(name))
                familyId = name;

            scaleVariance.x = Mathf.Max(0.01f, scaleVariance.x);
            scaleVariance.y = Mathf.Max(scaleVariance.x, scaleVariance.y);
            vat.frameCount = Mathf.Max(1, vat.frameCount);
            vat.playbackSpeed = Mathf.Max(0f, vat.playbackSpeed);
        }
#endif
    }
}
