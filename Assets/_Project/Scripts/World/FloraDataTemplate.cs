using System;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Items;
using Hecton8.Scavenging;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Authored flora contract that links indirect-vegetation species selection to harvest and shader payload data.
    /// </summary>
    [CreateAssetMenu(
        fileName = "FloraDataTemplate_",
        menuName = "Hecton8/World/Flora Data Template",
        order = 112)]
    public sealed class FloraDataTemplate : ScriptableObject
    {
        [Flags]
        public enum VulnerabilityMask : uint
        {
            None = 0u,
            Cut = 1u << 0,
            Drill = 1u << 1,
            Grab = 1u << 2,
            Stun = 1u << 3,
            Burn = 1u << 4,
            PlasmaCut = Cut | Burn
        }

        public enum AudioMaterialId : byte
        {
            None = 0,
            Organic = 1,
            Brittle = 2,
            Metallic = 3
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 40)]
        public struct RuntimeDescriptor
        {
            public int StableHashId;
            public int LootHashId;
            public uint VulnerabilityMask;
            public uint AudioMaterialId;
            public float4 BioluminescenceColor;
            public float PulseFrequency;
            public int HarvestTemplateStableHashId;
            public uint Reserved0;
        }

        [Header("Identity")]
        [SerializeField]
        [Tooltip("Stable flora-template identifier used by persistence, analytics, and deterministic runtime selection.")]
        private string stableId = "flora.generic";

        [SerializeField]
        [Tooltip("Artist-facing label shown in tooling and diagnostics.")]
        private string displayName = "Generic Flora";

        [Header("Selection")]
        [SerializeField]
        [Tooltip("Indirect vegetation render family this template can bind to.")]
        private HectonVegetationInstanceType vegetationType = HectonVegetationInstanceType.Grass;

        [SerializeField]
        [Tooltip("Semantic subtype this template resolves from the streamed vegetation bridge.")]
        private HectonMapMagicVegetationBridge.VegetationSemanticType semanticType = HectonMapMagicVegetationBridge.VegetationSemanticType.OrganicGrass;

        [SerializeField]
        [Tooltip("Vertical biome layer this template targets inside the streamed flora selection lattice.")]
        private HectonMapMagicVegetationBridge.VegetationBiomeLayer biomeLayer = HectonMapMagicVegetationBridge.VegetationBiomeLayer.OrganicShelf;

        [Header("Harvest")]
        [SerializeField]
        [Tooltip("Authoritative harvest template used by the destruction runtime for health, resistance, and weighted loot yields.")]
        private HarvestableTemplate harvestTemplate;

        [SerializeField]
        [Tooltip("Primary authored item used to compute the stable loot hash id for economist/item routing.")]
        private ItemData lootItem;

        [SerializeField]
        [Tooltip("Fallback stable loot hash id used only when no item asset is authored.")]
        private int lootHashId;

        [SerializeField]
        [Tooltip("Capability mask required to harvest this flora family. CUT/PlasmaCut is live; other masks stage future tool integrations.")]
        private VulnerabilityMask vulnerabilityMask = VulnerabilityMask.Cut;

        [SerializeField]
        [Tooltip("Semantic audio-material id published for flora acoustics and impact routing.")]
        private AudioMaterialId audioMaterialId = AudioMaterialId.Organic;

        [Header("Bioluminescence")]
        [SerializeField]
        [Tooltip("Linear bioluminescent emission color. Alpha is used as emission intensity.")]
        private Color bioluminescenceColor = new Color(0.20f, 0.95f, 0.85f, 0.70f);

        [SerializeField, Min(0.05f)]
        [Tooltip("Pulse frequency in Hertz applied by the indirect vegetation shader.")]
        private float pulseFrequency = 0.85f;

        /// <summary>Stable flora-template identifier.</summary>
        public string StableId => stableId;

        /// <summary>Artist-facing display label.</summary>
        public string DisplayName => displayName;

        /// <summary>Vegetation render family used for deterministic instance selection.</summary>
        public HectonVegetationInstanceType VegetationType => vegetationType;

        /// <summary>Semantic subtype used for deterministic instance selection.</summary>
        public HectonMapMagicVegetationBridge.VegetationSemanticType SemanticType => semanticType;

        /// <summary>Biome layer used for deterministic instance selection.</summary>
        public HectonMapMagicVegetationBridge.VegetationBiomeLayer BiomeLayer => biomeLayer;

        /// <summary>Authoritative harvest template used by the destruction runtime.</summary>
        public HarvestableTemplate HarvestTemplate => harvestTemplate;

        /// <summary>Stable loot hash id mirrored into runtime descriptors and reports.</summary>
        public int LootHashId
        {
            get
            {
                if (lootItem != null && !string.IsNullOrWhiteSpace(lootItem.PersistentId))
                    return LocHash.Compute(lootItem.PersistentId);

                return lootHashId;
            }
        }

        /// <summary>Capability mask required to harvest this flora family.</summary>
        public uint ToolVulnerabilityMask => (uint)vulnerabilityMask;

        /// <summary>Semantic audio-material id used by runtime consumers.</summary>
        public byte AudioMaterialID => (byte)audioMaterialId;

        /// <summary>Authored bioluminescent emission color in linear space.</summary>
        public Color BioluminescenceColor => bioluminescenceColor.linear;

        /// <summary>Authored shader pulse frequency in Hertz.</summary>
        public float PulseFrequency => Mathf.Max(0.05f, pulseFrequency);

        /// <summary>
        /// Builds the blittable runtime descriptor copied into authoring/runtime caches without managed references.
        /// </summary>
        public RuntimeDescriptor BuildRuntimeDescriptor()
        {
            Color linearColor = bioluminescenceColor.linear;
            int harvestStableHashId = harvestTemplate != null && !string.IsNullOrWhiteSpace(harvestTemplate.StableId)
                ? LocHash.Compute(harvestTemplate.StableId)
                : 0;
            return new RuntimeDescriptor
            {
                StableHashId = string.IsNullOrWhiteSpace(stableId) ? 0 : LocHash.Compute(stableId),
                LootHashId = LootHashId,
                VulnerabilityMask = (uint)vulnerabilityMask,
                AudioMaterialId = (uint)audioMaterialId,
                BioluminescenceColor = new float4(linearColor.r, linearColor.g, linearColor.b, linearColor.a),
                PulseFrequency = Mathf.Max(0.05f, pulseFrequency),
                HarvestTemplateStableHashId = harvestStableHashId,
                Reserved0 = 0u
            };
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(stableId) && !string.IsNullOrWhiteSpace(name))
                stableId = name;

            pulseFrequency = Mathf.Max(0.05f, pulseFrequency);
        }
#endif
    }
}
