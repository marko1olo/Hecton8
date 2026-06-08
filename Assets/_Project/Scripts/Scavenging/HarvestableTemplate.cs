using System;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Items;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Scavenging
{
    internal static class HarvestableTemplateLayout
    {
        public const int LootRuntimeEntryStrideBytes = 32;
        public const int RuntimeDescriptorStrideBytes = 32;
    }

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

        [StructLayout(LayoutKind.Explicit, Size = HarvestableTemplateLayout.LootRuntimeEntryStrideBytes)]
        public struct LootRuntimeEntry
        {
            [FieldOffset(0)] public int ItemHashId;
            [FieldOffset(4)] public ushort MinimumAmount;
            [FieldOffset(6)] public ushort MaximumAmount;
            [FieldOffset(8)] public byte Weight;
            [FieldOffset(9)] private byte _pad0;
            [FieldOffset(10)] private byte _pad1;
            [FieldOffset(11)] private byte _pad2;
            [FieldOffset(12)] private byte _pad3;
            [FieldOffset(13)] private byte _pad4;
            [FieldOffset(14)] private byte _pad5;
            [FieldOffset(15)] private byte _pad6;
            [FieldOffset(16)] private byte _pad7;
            [FieldOffset(17)] private byte _pad8;
            [FieldOffset(18)] private byte _pad9;
            [FieldOffset(19)] private byte _pad10;
            [FieldOffset(20)] private byte _pad11;
            [FieldOffset(21)] private byte _pad12;
            [FieldOffset(22)] private byte _pad13;
            [FieldOffset(23)] private byte _pad14;
            [FieldOffset(24)] private byte _pad15;
            [FieldOffset(25)] private byte _pad16;
            [FieldOffset(26)] private byte _pad17;
            [FieldOffset(27)] private byte _pad18;
            [FieldOffset(28)] private byte _pad19;
            [FieldOffset(29)] private byte _pad20;
            [FieldOffset(30)] private byte _pad21;
            [FieldOffset(31)] private byte _pad22;
        }

        [StructLayout(LayoutKind.Explicit, Size = HarvestableTemplateLayout.RuntimeDescriptorStrideBytes)]
        public struct RuntimeDescriptor
        {
            [FieldOffset(0)] public int StableHashId;
            [FieldOffset(4)] public float BaseHealth;
            [FieldOffset(8)] public float ToolResistance;
            [FieldOffset(12)] public int LootStartIndex;
            [FieldOffset(16)] public byte LootCount;
            [FieldOffset(17)] public byte MaterialClassId;
            [FieldOffset(18)] private byte _pad0;
            [FieldOffset(19)] private byte _pad1;
            [FieldOffset(20)] private byte _pad2;
            [FieldOffset(21)] private byte _pad3;
            [FieldOffset(22)] private byte _pad4;
            [FieldOffset(23)] private byte _pad5;
            [FieldOffset(24)] private byte _pad6;
            [FieldOffset(25)] private byte _pad7;
            [FieldOffset(26)] private byte _pad8;
            [FieldOffset(27)] private byte _pad9;
            [FieldOffset(28)] private byte _pad10;
            [FieldOffset(29)] private byte _pad11;
            [FieldOffset(30)] private byte _pad12;
            [FieldOffset(31)] private byte _pad13;
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

        [SerializeField, HideInInspector] private int validationInvalidLootEntryCount;
        [SerializeField, HideInInspector] private int validationFirstInvalidLootEntryIndex = -1;
        [SerializeField, HideInInspector] private int validationDuplicateLootItemHashCount;
        [SerializeField, HideInInspector] private int validationFirstDuplicateLootItemHashIndex = -1;
        [SerializeField, HideInInspector] private int validationRuntimeLootEntryCount;

        /// <summary>Stable identifier used by runtime and persistence.</summary>
        public string StableId => stableId;

        /// <summary>Artist-facing label shown in inspectors.</summary>
        public string DisplayName => displayName;

        /// <summary>Configured material class consumed by the entropy runtime.</summary>
        public MaterialClass TemplateMaterialClass => materialClass;

        public int ValidationInvalidLootEntryCount => validationInvalidLootEntryCount;
        public int ValidationFirstInvalidLootEntryIndex => validationFirstInvalidLootEntryIndex;
        public int ValidationDuplicateLootItemHashCount => validationDuplicateLootItemHashCount;
        public int ValidationFirstDuplicateLootItemHashIndex => validationFirstDuplicateLootItemHashIndex;
        public int ValidationRuntimeLootEntryCount => validationRuntimeLootEntryCount;
        public bool HasValidationErrors => validationInvalidLootEntryCount > 0 || validationDuplicateLootItemHashCount > 0;

        /// <summary>Builds the blittable runtime descriptor consumed by the organic entropy runtime.</summary>
        public RuntimeDescriptor BuildRuntimeDescriptor(int lootStartIndex)
        {
            return new RuntimeDescriptor
            {
                StableHashId = string.IsNullOrWhiteSpace(stableId) ? 0 : LocHash.Compute(stableId),
                BaseHealth = math.max(0.1f, baseHealth),
                ToolResistance = math.max(0.01f, toolResistance),
                LootStartIndex = math.max(0, lootStartIndex),
                LootCount = (byte)CountRuntimeLootEntries(byte.MaxValue),
                MaterialClassId = (byte)materialClass
            };
        }

        /// <summary>Counts valid runtime loot entries without allocating caller scratch.</summary>
        public int CountRuntimeLootEntries(int maxCount)
        {
            return CountValidLootEntries(maxCount);
        }

        /// <summary>
        /// Copies the authored loot table into caller-owned scratch storage without allocating runtime arrays.
        /// </summary>
        public int CopyLootTableNonAlloc(NativeList<LootRuntimeEntry> destination)
        {
            if (!destination.IsCreated || lootTable == null)
                return 0;

            int copiedCount = 0;
            int remainingCapacity = destination.Capacity - destination.Length;
            if (remainingCapacity <= 0)
                return 0;

            for (int i = 0; i < lootTable.Length && copiedCount < remainingCapacity; i++)
            {
                LootAuthoringEntry source = lootTable[i];
                if (!IsRuntimeLootSlotValid(i))
                    continue;

                destination.AddNoResize(new LootRuntimeEntry
                {
                    ItemHashId = ItemData.ResolvePersistentHashId(source.item),
                    MinimumAmount = source.minimumAmount,
                    MaximumAmount = source.maximumAmount,
                    Weight = source.weight
                });
                copiedCount++;
            }

            return copiedCount;
        }

        private int CountValidLootEntries(int maxCount)
        {
            if (lootTable == null || maxCount <= 0)
                return 0;

            int count = 0;
            int scanCount = math.min(lootTable.Length, maxCount);
            for (int i = 0; i < scanCount; i++)
            {
                if (IsRuntimeLootSlotValid(i))
                    count++;
            }

            return count;
        }

        public void RefreshValidationState()
        {
            RebuildValidationState();
        }

        private void OnEnable()
        {
            RebuildValidationState();
        }

        private void RebuildValidationState()
        {
            validationInvalidLootEntryCount = 0;
            validationFirstInvalidLootEntryIndex = -1;
            validationDuplicateLootItemHashCount = 0;
            validationFirstDuplicateLootItemHashIndex = -1;
            validationRuntimeLootEntryCount = 0;

            if (lootTable == null)
                return;

            for (int i = 0; i < lootTable.Length; i++)
            {
                if (!IsValidLootEntry(in lootTable[i]))
                {
                    validationInvalidLootEntryCount++;
                    if (validationFirstInvalidLootEntryIndex < 0)
                        validationFirstInvalidLootEntryIndex = i;
                    continue;
                }

                if (HasDuplicateLootEntryBefore(i))
                {
                    validationDuplicateLootItemHashCount++;
                    if (validationFirstDuplicateLootItemHashIndex < 0)
                        validationFirstDuplicateLootItemHashIndex = i;
                    continue;
                }

                validationRuntimeLootEntryCount++;
            }
        }

        private bool IsRuntimeLootSlotValid(int index)
        {
            return lootTable != null &&
                   (uint)index < (uint)lootTable.Length &&
                   IsValidLootEntry(in lootTable[index]) &&
                   !HasDuplicateLootEntryBefore(index);
        }

        private bool HasDuplicateLootEntryBefore(int index)
        {
            if (lootTable == null || (uint)index >= (uint)lootTable.Length)
                return false;

            LootAuthoringEntry current = lootTable[index];
            if (!IsValidLootEntry(in current))
                return false;

            int currentHash = ItemData.ResolvePersistentHashId(current.item);
            for (int i = 0; i < index; i++)
            {
                LootAuthoringEntry previous = lootTable[i];
                if (IsValidLootEntry(in previous) && ItemData.ResolvePersistentHashId(previous.item) == currentHash)
                    return true;
            }

            return false;
        }

        private static bool IsValidLootEntry(in LootAuthoringEntry entry)
        {
            return entry.item != null &&
                   !string.IsNullOrWhiteSpace(entry.item.PersistentId) &&
                   entry.minimumAmount > 0 &&
                   entry.maximumAmount >= entry.minimumAmount &&
                   entry.weight > 0;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(stableId) && !string.IsNullOrWhiteSpace(name))
                stableId = name;

            baseHealth = math.max(0.1f, baseHealth);
            toolResistance = math.max(0.01f, toolResistance);
            RebuildValidationState();
        }
#endif
    }
}
