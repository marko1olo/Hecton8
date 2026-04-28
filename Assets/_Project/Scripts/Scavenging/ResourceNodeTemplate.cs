using System;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Items;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Scavenging
{
    /// <summary>
    /// Authoring template for harvestable resource-node families.
    /// Builds blittable descriptors and hash-based yield rows for SOA/runtime consumers.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ResourceNodeTemplate",
        menuName = "Hecton8/Scavenging/Resource Node Template",
        order = 112)]
    public sealed class ResourceNodeTemplate : ScriptableObject
    {
        public enum HarvestToolClass : byte
        {
            Any = 0,
            Knife = 1,
            Drill = 2,
            Laser = 3,
            Salvage = 4
        }

        [Serializable]
        public struct YieldAuthoringEntry
        {
            [Tooltip("Authored item asset granted by this harvest table entry.")]
            public ItemData item;

            [Min(1)]
            [Tooltip("Minimum stack count granted when the entry resolves.")]
            public ushort minimumAmount;

            [Min(1)]
            [Tooltip("Maximum stack count granted when the entry resolves.")]
            public ushort maximumAmount;

            [Range(1, 255)]
            [Tooltip("Relative weight consumed by the runtime weighted picker.")]
            public byte weight;
        }

        [Serializable]
        public struct RarityDropAuthoringEntry
        {
            [Range(0, 15)]
            [Tooltip("Rarity tier emitted into the runtime descriptor lane.")]
            public byte rarityTier;

            [Range(0f, 1f)]
            [Tooltip("Normalized probability for the rarity pass before weighted entry resolution.")]
            public float probability;

            [Tooltip("Hash-stable item granted by the rarity pass.")]
            public ItemData item;

            [Min(1)]
            [Tooltip("Quantity granted when the rarity drop resolves.")]
            public ushort amount;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 16)]
        public struct YieldRuntimeEntry
        {
            public int ItemHashId;
            public ushort MinimumAmount;
            public ushort MaximumAmount;
            public byte Weight;
            public byte Reserved0;
            public ushort Reserved1;
            public uint Reserved2;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 16)]
        public struct RarityDropRuntimeEntry
        {
            public int ItemHashId;
            public ushort Amount;
            public byte RarityTier;
            public byte ProbabilityByte;
            public uint Reserved0;
            public uint Reserved1;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 32)]
        public struct RuntimeDescriptor
        {
            public int StableHashId;
            public float ToolResistance;
            public float HarvestDurationSeconds;
            public int ValidLayerMask;
            public byte RequiredToolClass;
            public byte YieldCount;
            public byte RarityDropCount;
            public byte Reserved0;
            public float MinimumDensity;
            public float MaximumDensity;
            public float Reserved1;
        }

        [Header("Identity")]
        [SerializeField]
        [Tooltip("Stable string identifier used by persistence and procedural placement systems.")]
        private string stableId = "resource.node.generic";

        [SerializeField]
        [Tooltip("Artist-facing label shown in authoring inspectors.")]
        private string displayName = "Generic Resource Node";

        [Header("Harvest")]
        [SerializeField, Min(0.01f)]
        [Tooltip("Scalar resistance applied to tool damage validation before the node yields.")]
        private float toolResistance = 1f;

        [SerializeField, Min(0f)]
        [Tooltip("Base interaction duration for harvesting this node family.")]
        private float harvestDurationSeconds = 1.25f;

        [SerializeField]
        [Tooltip("Tool family required to extract this node. Logic tier consumes the byte enum only.")]
        private HarvestToolClass requiredToolClass = HarvestToolClass.Any;

        [Header("Scatter")]
        [SerializeField, Range(0f, 64f)]
        [Tooltip("Lower density bound exported to scatter/runtime placement lanes.")]
        private float minimumDensity = 0.5f;

        [SerializeField, Range(0f, 64f)]
        [Tooltip("Upper density bound exported to scatter/runtime placement lanes.")]
        private float maximumDensity = 2f;

        [SerializeField]
        [Tooltip("Terrain or biome layers allowed to host this node family.")]
        private LayerMask validLayers = ~0;

        [Header("Yield")]
        [SerializeField]
        [Tooltip("Primary weighted harvest table. Resolved to hash-based rows at runtime.")]
        private YieldAuthoringEntry[] harvestYield;

        [SerializeField]
        [Tooltip("Optional rarity table evaluated before the primary weighted harvest table.")]
        private RarityDropAuthoringEntry[] rarityDrops;

        /// <summary>Stable authored identifier used by persistence-facing systems.</summary>
        public string StableId => stableId;

        /// <summary>Artist-facing label for authoring tools.</summary>
        public string DisplayName => displayName;

        /// <summary>Builds the blittable runtime descriptor consumed by scatter/harvest SOA lanes.</summary>
        public RuntimeDescriptor BuildRuntimeDescriptor()
        {
            return new RuntimeDescriptor
            {
                StableHashId = string.IsNullOrWhiteSpace(stableId) ? 0 : LocHash.Compute(stableId),
                ToolResistance = math.max(0.01f, toolResistance),
                HarvestDurationSeconds = math.max(0f, harvestDurationSeconds),
                ValidLayerMask = validLayers.value,
                RequiredToolClass = (byte)requiredToolClass,
                YieldCount = (byte)math.min(byte.MaxValue, harvestYield != null ? harvestYield.Length : 0),
                RarityDropCount = (byte)math.min(byte.MaxValue, rarityDrops != null ? rarityDrops.Length : 0),
                Reserved0 = 0,
                MinimumDensity = math.max(0f, minimumDensity),
                MaximumDensity = math.max(math.max(0f, minimumDensity), maximumDensity),
                Reserved1 = 0f
            };
        }

        /// <summary>
        /// Copies the authored primary yield table into caller-owned scratch storage without allocating runtime arrays.
        /// </summary>
        public int CopyYieldTableNonAlloc(NativeList<YieldRuntimeEntry> destination)
        {
            if (!destination.IsCreated || harvestYield == null)
                return 0;

            int copiedCount = 0;
            int maxEntries = math.min(harvestYield.Length, destination.Capacity - destination.Length);
            for (int i = 0; i < maxEntries; i++)
            {
                YieldAuthoringEntry source = harvestYield[i];
                if (source.item == null || string.IsNullOrWhiteSpace(source.item.PersistentId))
                    continue;

                YieldRuntimeEntry runtimeEntry = new YieldRuntimeEntry
                {
                    ItemHashId = LocHash.Compute(source.item.PersistentId),
                    MinimumAmount = (ushort)math.max(1, (int)source.minimumAmount),
                    MaximumAmount = (ushort)math.max(math.max(1, (int)source.minimumAmount), (int)source.maximumAmount),
                    Weight = (byte)math.max(1, (int)source.weight),
                    Reserved0 = 0,
                    Reserved1 = 0,
                    Reserved2 = 0u
                };

                destination.AddNoResize(runtimeEntry);
                copiedCount++;
            }

            return copiedCount;
        }

        /// <summary>
        /// Copies the authored rarity table into caller-owned scratch storage without allocating runtime arrays.
        /// </summary>
        public int CopyRarityTableNonAlloc(NativeList<RarityDropRuntimeEntry> destination)
        {
            if (!destination.IsCreated || rarityDrops == null)
                return 0;

            int copiedCount = 0;
            int maxEntries = math.min(rarityDrops.Length, destination.Capacity - destination.Length);
            for (int i = 0; i < maxEntries; i++)
            {
                RarityDropAuthoringEntry source = rarityDrops[i];
                if (source.item == null || string.IsNullOrWhiteSpace(source.item.PersistentId))
                    continue;

                RarityDropRuntimeEntry runtimeEntry = new RarityDropRuntimeEntry
                {
                    ItemHashId = LocHash.Compute(source.item.PersistentId),
                    Amount = (ushort)math.max(1, (int)source.amount),
                    RarityTier = (byte)math.clamp((int)source.rarityTier, 0, 15),
                    ProbabilityByte = (byte)math.clamp(math.round(source.probability * 255f), 0f, 255f),
                    Reserved0 = 0u,
                    Reserved1 = 0u
                };

                destination.AddNoResize(runtimeEntry);
                copiedCount++;
            }

            return copiedCount;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(stableId) && !string.IsNullOrWhiteSpace(name))
                stableId = name;

            minimumDensity = math.max(0f, minimumDensity);
            maximumDensity = math.max(minimumDensity, maximumDensity);
            toolResistance = math.max(0.01f, toolResistance);
            harvestDurationSeconds = math.max(0f, harvestDurationSeconds);
        }
#endif
    }
}
