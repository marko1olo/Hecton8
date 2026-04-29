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
    /// Authoring template for streamed harvestable organic/material nodes resolved by the indirect-flora entropy runtime.
    /// </summary>
    [CreateAssetMenu(
        fileName = "HarvestableTemplate",
        menuName = "Hecton8/Scavenging/Harvestable Template",
        order = 113)]
    public sealed class HarvestableTemplate : ScriptableObject
    {
        public enum MaterialClass : byte
        {
            None = 0,
            Kelp = 1,
            Coral = 2,
            TitaniumOutcrop = 3,
            Sargassum = 4
        }

        [Serializable]
        public struct LootAuthoringEntry
        {
            [Tooltip("Authored item asset granted by this harvest table entry.")]
            public ItemData item;

            [Min(1)]
            [Tooltip("Minimum stack count granted when the weighted entry resolves.")]
            public ushort minimumAmount;

            [Min(1)]
            [Tooltip("Maximum stack count granted when the weighted entry resolves.")]
            public ushort maximumAmount;

            [Range(1, 255)]
            [Tooltip("Relative weight consumed by the Burst weighted picker.")]
            public byte weight;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 24)]
        public struct LootRuntimeEntry
        {
            public int ItemHashId;
            public ushort MinimumAmount;
            public ushort MaximumAmount;
            public byte Weight;
            public byte Reserved0;
            public ushort Reserved1;
            public uint Reserved2;
            public uint Reserved3;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 24)]
        public struct RuntimeDescriptor
        {
            public int StableHashId;
            public float BaseHealth;
            public float ToolResistance;
            public int LootStartIndex;
            public byte LootCount;
            public byte MaterialClassId;
            public ushort Reserved0;
            public uint Reserved1;
        }

        [Header("Identity")]
        [SerializeField]
        [Tooltip("Stable template identifier used by persistence and runtime lookup.")]
        private string stableId = "harvestable.generic";

        [SerializeField]
        [Tooltip("Artist-facing label shown in authoring inspectors.")]
        private string displayName = "Generic Harvestable";

        [Header("Harvest")]
        [SerializeField, Min(0.1f)]
        [Tooltip("Base HP assigned to one flora/node instance before tool resistance is applied.")]
        private float baseHealth = 1f;

        [SerializeField, Min(0.01f)]
        [Tooltip("Damage divisor applied to incoming tool power. Higher values make the target harder to harvest.")]
        private float toolResistance = 1f;

        [SerializeField]
        [Tooltip("Material class consumed by the entropy runtime and Burst yield job.")]
        private MaterialClass materialClass = MaterialClass.Kelp;

        [Header("Loot")]
        [SerializeField]
        [Tooltip("Primary weighted loot table resolved by the Burst entropy yield job.")]
        private LootAuthoringEntry[] lootTable;

        /// <summary>Stable identifier used by runtime and persistence.</summary>
        public string StableId => stableId;

        /// <summary>Artist-facing label shown in inspectors.</summary>
        public string DisplayName => displayName;

        /// <summary>Configured material class consumed by the entropy runtime.</summary>
        public MaterialClass TemplateMaterialClass => materialClass;

        /// <summary>Builds the blittable runtime descriptor consumed by the organic entropy runtime.</summary>
        public RuntimeDescriptor BuildRuntimeDescriptor(int lootStartIndex)
        {
            return new RuntimeDescriptor
            {
                StableHashId = string.IsNullOrWhiteSpace(stableId) ? 0 : LocHash.Compute(stableId),
                BaseHealth = math.max(0.1f, baseHealth),
                ToolResistance = math.max(0.01f, toolResistance),
                LootStartIndex = math.max(0, lootStartIndex),
                LootCount = (byte)math.min(byte.MaxValue, lootTable != null ? lootTable.Length : 0),
                MaterialClassId = (byte)materialClass,
                Reserved0 = 0,
                Reserved1 = 0u
            };
        }

        /// <summary>
        /// Copies the authored loot table into caller-owned scratch storage without allocating runtime arrays.
        /// </summary>
        public int CopyLootTableNonAlloc(NativeList<LootRuntimeEntry> destination)
        {
            if (!destination.IsCreated || lootTable == null)
                return 0;

            int copiedCount = 0;
            int maxEntries = math.min(lootTable.Length, destination.Capacity - destination.Length);
            for (int i = 0; i < maxEntries; i++)
            {
                LootAuthoringEntry source = lootTable[i];
                if (source.item == null || string.IsNullOrWhiteSpace(source.item.PersistentId))
                    continue;

                destination.AddNoResize(new LootRuntimeEntry
                {
                    ItemHashId = LocHash.Compute(source.item.PersistentId),
                    MinimumAmount = (ushort)math.max(1, (int)source.minimumAmount),
                    MaximumAmount = (ushort)math.max(math.max(1, (int)source.minimumAmount), (int)source.maximumAmount),
                    Weight = (byte)math.max(1, (int)source.weight),
                    Reserved0 = 0,
                    Reserved1 = 0,
                    Reserved2 = 0u,
                    Reserved3 = 0u
                });
                copiedCount++;
            }

            return copiedCount;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(stableId) && !string.IsNullOrWhiteSpace(name))
                stableId = name;

            baseHealth = math.max(0.1f, baseHealth);
            toolResistance = math.max(0.01f, toolResistance);
        }
#endif
    }
}
