// ============================================================================
// HECTON-8 - PlayerInventory.cs
// Native SOA-backed inventory owner. Managed ItemData resolution is seam-only.
// ============================================================================

namespace Hecton8.Inventory
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using Hecton.Localization;
    using Hecton8.Audio;
    using Hecton8.Core;
    using Hecton8.Core.Contracts.Signals;
    using Hecton8.Gameplay;
    using Hecton8.Interaction;
    using Hecton8.Inventory.Algorithms;
    using Hecton8.Inventory.Corrosion;
    using Hecton8.Inventory.Corrosion.Contracts;
    using Hecton8.Items;
    using Hecton8.Modding;
    using Hecton8.Physics;
    using Hecton8.SaveSystem;
    using Hecton8.World;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using Unity.Mathematics;
    using Unity.Profiling;
    using UnityEngine;

    [DisallowMultipleComponent]
    public sealed class PlayerInventory : MonoBehaviour, ISaveable, ISlowTickable, ILateFrameTickable, IPhysicsImpactEventListener
    {
        private const ushort CraftingLockedMask = ItemRuntimeStateFlags.CraftingLocked;
        private const ushort RadioactiveItemStateMask = ItemRuntimeStateFlags.Radioactive;
        private const ushort BiologicalItemStateMask = ItemRuntimeStateFlags.Biological;
        internal const ushort DegradedItemStateMask = ItemRuntimeStateFlags.Degraded;
        private const ushort RustedItemStateMask = ItemRuntimeStateFlags.Rusted;
        private const ushort FlammableItemStateMask = ItemRuntimeStateFlags.Flammable;
        private const ushort BrokenItemStateMask = ItemRuntimeStateFlags.Broken;
        private const ushort DurabilityDecayEligibleMask = BiologicalItemStateMask | RustedItemStateMask | RadioactiveItemStateMask;
        private const ushort DefaultQualityMilli = 1000;
        internal const ushort DegradedQualityMilliThreshold = 250;
        private const byte DegradedDurabilityThreshold = DegradedQualityMilliThreshold / 10;
        private const float SlowTickIntervalSeconds = 0.5f;
        private const float OrganicDecayPerSecond = 0.00045f;
        private const float SubmergedOrganicDecayPerSecond = 0.00075f;
        private const float SubmergedMetalRustPerSecond = 0.00065f;
        private const float ThermalRunawayPerSecond = 0.65f;
        private const float ThermalRunawayCooldownPerSecond = 0.2f;
        private const float ThermalRunawayDamage = 50f;
        private const float ThermalRunawayAudioVolume = 0.72f;
        private const float PressureCrushDepthMeters = 2000f;
        private const float PressureCrushDurabilityPerSecond = 0.08f;
        private const float RadioactiveHalfLifeBaseSeconds = 1800f;
        private const float Ln2 = 0.6931471805599453f;
        private const float KineticDamageThresholdG = 50f;
        private const float InventoryLoadMinimumMovementMultiplier = 0.5f;
        private const float VolumeM3ToLiters = 1000f;
        private const float HeavyBulkTransferAudioThresholdKg = 50f;
        private const int InventoryBlackBoxCapacity = 300;
        private const int InventoryBlackBoxEntrySizeBytes = 64;
        private const string InventoryBlackBoxDumpRelativePath = "Docs/AgentLogs/Dump_INVENTORY_SOA_BLITTER.bin";
        private const float SalinityCorrosionFrostTickSeconds = 5f;
        private const float SalinityCorrosionDegradationRatePerFrostTick = 0.00325f;
        private const float EquipmentFailingThreshold01 = 0.2f;
        private const float EquipmentFailingResetThreshold01 = 0.25f;
        private const int SalinityCorrosionBlackBoxEntrySizeBytes = 32;
        private const string SalinityCorrosionBlackBoxDumpRelativePath = "Docs/AgentLogs/Dump_SALINITY_CORROSION_SYSTEM.bin";
        private const string BulkTransferValidationTempLabel = "BulkTransferValidationTemp";
        private const string BulkTransferFailureTempLabel = "BulkTransferFailureTemp";
        private const string BulkTransferCompactionResultTempLabel = "BulkTransferCompactionResultTemp";
        private const string BulkTransferCompactionHashTempLabel = "BulkTransferCompactionHashTemp";
        private const string BulkTransferCompactionCountTempLabel = "BulkTransferCompactionCountTemp";
        private const string BulkTransferCompactionConditionTempLabel = "BulkTransferCompactionConditionTemp";
        private const string BulkTransferCompactionStateTempLabel = "BulkTransferCompactionStateTemp";
        private const string BulkTransferCompactionGeneticsTempLabel = "BulkTransferCompactionGeneticsTemp";
        private const string BulkTransferCompactionQualityTempLabel = "BulkTransferCompactionQualityTemp";
        private const string BulkTransferCompactionDurabilityTempLabel = "BulkTransferCompactionDurabilityTemp";
        private const string BulkTransferCompactionTimestampTempLabel = "BulkTransferCompactionTimestampTemp";
        private const string BulkTransferCompactionMassTempLabel = "BulkTransferCompactionMassTemp";
        private const string BulkTransferCompactionVolumeTempLabel = "BulkTransferCompactionVolumeTemp";
        private const string BulkTransferCompactionRadiationTempLabel = "BulkTransferCompactionRadiationTemp";
        private const string NativeMemoryOwner = nameof(PlayerInventory);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;
        private const int InventoryShadowBufferBytes = 16 * 1024;
        private const uint Fnv1a32Offset = 2166136261u;
        private const uint Fnv1a32Prime = 16777619u;
        private const byte ItemGeneticsSupportedFlagsMask = (byte)(
            ItemGeneticFlags.Glow |
            ItemGeneticFlags.Toxic |
            ItemGeneticFlags.Edible |
            ItemGeneticFlags.Harvestable);
        private const ulong LegacyGlowGeneMask = (ulong)GeneticTraitProfile.GeneticTraitMask.Bioluminescent;
        private const ulong LegacyToxicGeneMask = (ulong)GeneticTraitProfile.GeneticTraitMask.Toxic;
        private const ulong LegacyEdibleGeneMask = (ulong)GeneticTraitProfile.GeneticTraitMask.Medicinal;
        private const ulong LegacyHarvestableGeneMask = (ulong)(
            GeneticTraitProfile.GeneticTraitMask.OxygenProducing |
            GeneticTraitProfile.GeneticTraitMask.FastGrowing |
            GeneticTraitProfile.GeneticTraitMask.Aquatic);
        private static readonly int _DepletedLeadHashId = LocHash.Compute("Data_DepletedLead");
        private static readonly uint _InventoryBulkTransferToolHash = unchecked((uint)LocHash.Compute("InventoryBulkTransfer"));
        private static readonly uint _HeavyThudTargetHash = unchecked((uint)LocHash.Compute("HeavyThud"));
        private static readonly uint _InventorySortToolHash = unchecked((uint)LocHash.Compute("InventorySort"));
        private static readonly uint _InventoryUiClickHash = unchecked((uint)LocHash.Compute("UI_Click"));
        private static readonly uint _InventoryDefragTimeMsHash = unchecked((uint)LocHash.Compute("InventoryDefragTimeMs"));
        private static readonly uint _InventoryDefragContextHash = unchecked((uint)LocHash.Compute("PlayerInventoryDefrag"));
        private static readonly uint _EquipmentCorrosionToolHash = unchecked((uint)LocHash.Compute("EquipmentCorrosion"));
        private static readonly uint _EquipmentBreakTargetHash = unchecked((uint)LocHash.Compute("EquipmentBreak"));
        private static readonly uint _EquipmentFailingMessageHash = unchecked((uint)LocHash.Compute("Equipment Failing"));
        private static readonly uint _EquipmentFailingContextHash = unchecked((uint)LocHash.Compute("SalinityCorrosion"));
        private static readonly uint _TitaniumScrapHashId = unchecked((uint)LocHash.Compute("Data_TitaniumScrap"));
        private static readonly uint _BrineFamilyLocHash = unchecked((uint)LocHash.Compute("biome.family.chemosynthetic_brine"));
        private static readonly uint _BrineFamilyDataHash = Hecton8.Data.H8DataHash.ComputeFnv1A32("biome.family.chemosynthetic_brine");
        private static readonly uint _BrineRiversLocHash = unchecked((uint)LocHash.Compute("Brine Rivers"));
        private static readonly uint _BrineRiversDataHash = Hecton8.Data.H8DataHash.ComputeFnv1A32("brine_rivers");
        private static readonly uint _ThermalBrineDataHash = Hecton8.Data.H8DataHash.ComputeFnv1A32("thermal_brine");
        private static readonly int _HectonEquipmentRust01Id = Shader.PropertyToID("_HectonEquipmentRust01");
        private static readonly ProfilerMarker _slowTickProfilerMarker = new ProfilerMarker("H8.Inventory.PlayerInventory.SlowTick");
        private static readonly ProfilerMarker _radioactiveHalfLifeProfilerMarker = new ProfilerMarker("H8.Inventory.PlayerInventory.RadioactiveHalfLife");
        private static readonly ProfilerMarker _reactiveChemistryProfilerMarker = new ProfilerMarker("H8.Inventory.PlayerInventory.ReactiveChemistry");
        private static readonly ProfilerMarker _defragProfilerMarker = new ProfilerMarker("H8.Inventory.PlayerInventory.DefragSort");

        [Flags]
        public enum ItemGeneticFlags : byte
        {
            None = 0,
            Glow = 1 << 0,
            Toxic = 1 << 1,
            Edible = 1 << 2,
            Harvestable = 1 << 3
        }

        [StructLayout(LayoutKind.Explicit, Size = InventoryBlackBoxEntrySizeBytes)]
        private struct InventoryTelemetryEntry
        {
            [FieldOffset(0)] public uint Frame;
            [FieldOffset(4)] public uint Version;
            [FieldOffset(8)] public float WeightKg;
            [FieldOffset(12)] public float VolumeLiters;
            [FieldOffset(16)] public float Load01;
            [FieldOffset(20)] public uint InventoryMaskLow;
            [FieldOffset(24)] public int OccupiedCells;
            [FieldOffset(28)] public int Flags;
            [FieldOffset(32)] public float MaxWeightKg;
            [FieldOffset(36)] public float MaxVolumeLiters;
            [FieldOffset(40)] public uint ShadowHash;
            [FieldOffset(44)] public int ShadowPayloadLength;
            [FieldOffset(48)] public float RadiationSv;
            [FieldOffset(52)] public int Columns;
            [FieldOffset(56)] public int Rows;
            [FieldOffset(60)] public int DefragTimeMicroseconds;
        }

        [StructLayout(LayoutKind.Explicit, Size = SalinityCorrosionBlackBoxEntrySizeBytes)]
        private struct SalinityCorrosionTelemetryEntry
        {
            [FieldOffset(0)] public uint Frame;
            [FieldOffset(4)] public uint InventoryVersion;
            [FieldOffset(8)] public float AverageEquipmentDurability01;
            [FieldOffset(12)] public float RustScalar01;
            [FieldOffset(16)] public float SalinityFactor;
            [FieldOffset(20)] public uint CurrentBiomeHash;
            [FieldOffset(24)] public uint InventoryMaskLow;
            [FieldOffset(28)] public int Flags;
        }

        [BurstCompile]
        private struct InventoryMassVolumeJob : IJob
        {
            [ReadOnly] public NativeArray<int>.ReadOnly AnchorHashIds;
            [ReadOnly] public NativeArray<ushort> StackCounts;
            [ReadOnly] public NativeArray<float> AnchorUnitMassKg;
            [ReadOnly] public NativeArray<float> AnchorUnitVolumeM3;
            [ReadOnly] public NativeArray<float> AnchorUnitRadiationSv;
            public NativeArray<float3> Totals;

            public void Execute()
            {
                int count = math.min(
                    math.min(math.min(AnchorHashIds.Length, StackCounts.Length), math.min(AnchorUnitMassKg.Length, AnchorUnitVolumeM3.Length)),
                    AnchorUnitRadiationSv.Length);

                float totalMassKg = 0f;
                float totalVolumeM3 = 0f;
                float totalRadiationSv = 0f;

                for (int anchorIndex = 0; anchorIndex < count; anchorIndex++)
                {
                    if (AnchorHashIds[anchorIndex] == 0)
                        continue;

                    int stackCount = math.max(1, (int)StackCounts[anchorIndex]);
                    totalMassKg += AnchorUnitMassKg[anchorIndex] * stackCount;
                    totalVolumeM3 += AnchorUnitVolumeM3[anchorIndex] * stackCount;
                    totalRadiationSv += AnchorUnitRadiationSv[anchorIndex] * stackCount;
                }

                Totals[0] = new float3(
                    math.max(0f, totalMassKg),
                    math.max(0f, totalVolumeM3),
                    math.max(0f, totalRadiationSv));
            }
        }

        private struct InventoryRadioactiveHalfLifeKernel
        {
            [ReadOnly] public NativeArray<int>.ReadOnly AnchorHashIds;
            [ReadOnly] public NativeArray<ushort> StackCounts;
            [ReadOnly] public NativeArray<float> AnchorUnitRadiationSv;
            public NativeArray<ushort> ItemStateFlags;
            public NativeArray<ushort> QualityMilli;
            public NativeArray<int> ConversionAnchorIndices;
            public NativeArray<int> Counters;
            public float DeltaSeconds;
            public float BaseHalfLifeSeconds;
            public ushort DefaultQuality;
            public ushort RadioactiveMask;
            public ushort DegradedMask;
            public ushort DegradedThreshold;

            public void Execute()
            {
                if (Counters.Length >= 2)
                {
                    Counters[0] = 0;
                    Counters[1] = 0;
                }

                int count = math.min(
                    math.min(math.min(AnchorHashIds.Length, StackCounts.Length), AnchorUnitRadiationSv.Length),
                    math.min(ItemStateFlags.Length, QualityMilli.Length));
                if (count <= 0 || !(DeltaSeconds > 0f))
                    return;

                int conversionCount = 0;
                int changed = 0;
                float safeBaseHalfLifeSeconds = math.max(1f, BaseHalfLifeSeconds);
                float inverseBaseHalfLifeSeconds = math.rcp(safeBaseHalfLifeSeconds);

                for (int anchorIndex = 0; anchorIndex < count; anchorIndex++)
                {
                    if (AnchorHashIds[anchorIndex] == 0 || StackCounts[anchorIndex] == 0)
                        continue;

                    float radiationSv = AnchorUnitRadiationSv[anchorIndex];
                    if (!(radiationSv > 0f))
                        continue;

                    ushort currentFlags = (ushort)(ItemStateFlags[anchorIndex] | RadioactiveMask);
                    ushort currentQualityMilli = QualityMilli[anchorIndex] > 0 ? QualityMilli[anchorIndex] : DefaultQuality;
                    float currentQuality = math.clamp(currentQualityMilli * 0.001f, 0f, 1f);
                    float radiationFactor = math.max(0.001f, radiationSv) * inverseBaseHalfLifeSeconds;
                    float decayFactor = ApproximateExpNegPositiveInput(Ln2 * radiationFactor * DeltaSeconds);
                    float nextQuality = math.clamp(currentQuality * decayFactor, 0f, 1f);
                    ushort nextQualityMilli = (ushort)math.clamp((int)math.round(nextQuality * 1000f), 0, 1000);

                    if (nextQualityMilli < DegradedThreshold)
                        currentFlags = (ushort)(currentFlags | DegradedMask);

                    if (nextQualityMilli <= 0)
                    {
                        currentFlags = (ushort)(currentFlags | DegradedMask);
                        if (conversionCount < ConversionAnchorIndices.Length)
                            ConversionAnchorIndices[conversionCount++] = anchorIndex;
                    }

                    if (currentFlags != ItemStateFlags[anchorIndex] || nextQualityMilli != currentQualityMilli)
                    {
                        ItemStateFlags[anchorIndex] = currentFlags;
                        QualityMilli[anchorIndex] = nextQualityMilli;
                        changed = 1;
                    }
                }

                if (Counters.Length >= 2)
                {
                    Counters[0] = conversionCount;
                    Counters[1] = changed;
                }
            }
        }

        private struct InventoryReactiveChemistryKernel
        {
            [ReadOnly] public NativeArray<int>.ReadOnly AnchorHashIds;
            [ReadOnly] public NativeArray<ushort> StackCounts;
            [ReadOnly] public NativeArray<ushort> CraftLockedCounts;
            [ReadOnly] public NativeArray<ushort> ItemStateFlags;
            public NativeArray<float> ThermalRunawayByAnchor;
            public NativeArray<int2> RunawayPairs;
            public NativeArray<int> Counters;
            public int Columns;
            public int Rows;
            public float DeltaSeconds;
            public float RunawayPerSecond;
            public float CooldownPerSecond;
            public ushort RadioactiveMask;
            public ushort FlammableMask;

            public void Execute()
            {
                if (Counters.Length >= 2)
                {
                    Counters[0] = 0;
                    Counters[1] = 0;
                }

                int slotCount = math.min(
                    math.min(math.min(AnchorHashIds.Length, StackCounts.Length), CraftLockedCounts.Length),
                    math.min(ItemStateFlags.Length, ThermalRunawayByAnchor.Length));
                int safeColumns = math.max(1, Columns);
                int safeRows = math.max(1, Rows);
                if (slotCount <= 0 || !(DeltaSeconds > 0f))
                    return;

                int pairCount = 0;
                int changed = 0;
                float heatDelta = math.max(0f, RunawayPerSecond) * DeltaSeconds;
                float cooldownDelta = math.max(0f, CooldownPerSecond) * DeltaSeconds;

                for (int anchorIndex = 0; anchorIndex < slotCount; anchorIndex++)
                {
                    if (!IsReactiveCandidate(anchorIndex, slotCount))
                    {
                        if (ThermalRunawayByAnchor[anchorIndex] > 0f)
                        {
                            ThermalRunawayByAnchor[anchorIndex] = math.max(0f, ThermalRunawayByAnchor[anchorIndex] - cooldownDelta);
                            changed = 1;
                        }

                        continue;
                    }

                    int adjacentAnchor = FindAdjacentReactivePartner(anchorIndex, slotCount, safeColumns, safeRows);
                    if (adjacentAnchor < 0)
                    {
                        if (ThermalRunawayByAnchor[anchorIndex] > 0f)
                        {
                            ThermalRunawayByAnchor[anchorIndex] = math.max(0f, ThermalRunawayByAnchor[anchorIndex] - cooldownDelta);
                            changed = 1;
                        }

                        continue;
                    }

                    float previousRunaway = ThermalRunawayByAnchor[anchorIndex];
                    float nextRunaway = previousRunaway + heatDelta;
                    float storedRunaway = math.min(1.25f, nextRunaway);
                    if (storedRunaway != previousRunaway)
                    {
                        ThermalRunawayByAnchor[anchorIndex] = storedRunaway;
                        changed = 1;
                    }

                    if (nextRunaway > 1f && anchorIndex < adjacentAnchor && pairCount < RunawayPairs.Length)
                        RunawayPairs[pairCount++] = new int2(anchorIndex, adjacentAnchor);
                }

                if (Counters.Length >= 2)
                {
                    Counters[0] = pairCount;
                    Counters[1] = changed;
                }
            }

            private bool IsReactiveCandidate(int anchorIndex, int slotCount)
            {
                return (uint)anchorIndex < (uint)slotCount &&
                       AnchorHashIds[anchorIndex] != 0 &&
                       StackCounts[anchorIndex] > 0 &&
                       CraftLockedCounts[anchorIndex] == 0 &&
                       ((ItemStateFlags[anchorIndex] & (RadioactiveMask | FlammableMask)) != 0);
            }

            private int FindAdjacentReactivePartner(int anchorIndex, int slotCount, int safeColumns, int safeRows)
            {
                if (anchorIndex < 0 || anchorIndex >= slotCount)
                    return -1;

                ushort flags = ItemStateFlags[anchorIndex];
                bool isRadioactive = (flags & RadioactiveMask) != 0;
                bool isFlammable = (flags & FlammableMask) != 0;
                if (!isRadioactive && !isFlammable)
                    return -1;

                int x = anchorIndex % safeColumns;
                int y = anchorIndex / safeColumns;
                int partner = FindReactivePartnerAt(x - 1, y, slotCount, safeColumns, safeRows, isRadioactive, isFlammable);
                if (partner >= 0)
                    return partner;

                partner = FindReactivePartnerAt(x + 1, y, slotCount, safeColumns, safeRows, isRadioactive, isFlammable);
                if (partner >= 0)
                    return partner;

                partner = FindReactivePartnerAt(x, y - 1, slotCount, safeColumns, safeRows, isRadioactive, isFlammable);
                if (partner >= 0)
                    return partner;

                return FindReactivePartnerAt(x, y + 1, slotCount, safeColumns, safeRows, isRadioactive, isFlammable);
            }

            private int FindReactivePartnerAt(
                int x,
                int y,
                int slotCount,
                int safeColumns,
                int safeRows,
                bool sourceRadioactive,
                bool sourceFlammable)
            {
                if (x < 0 || x >= safeColumns || y < 0 || y >= safeRows)
                    return -1;

                int candidateIndex = y * safeColumns + x;
                if (!IsReactiveCandidate(candidateIndex, slotCount))
                    return -1;

                ushort flags = ItemStateFlags[candidateIndex];
                bool candidateRadioactive = (flags & RadioactiveMask) != 0;
                bool candidateFlammable = (flags & FlammableMask) != 0;
                if ((sourceRadioactive && candidateFlammable) || (sourceFlammable && candidateRadioactive))
                    return candidateIndex;

                return -1;
            }
        }

        [StructLayout(LayoutKind.Sequential, Size = 12)]
        public struct CraftReservation
        {
            public int AnchorIndex;
            public int Quantity;
            public int ItemHashId;
        }

        public readonly struct ScavengeAttemptResult
        {
            public readonly int RequestedQuantity;
            public readonly int AddedQuantity;
            public readonly int RejectedQuantity;

            public bool AnyAdded => AddedQuantity > 0;
            public bool IsSuccess => AddedQuantity > 0 && RejectedQuantity == 0;

            internal ScavengeAttemptResult(int requestedQuantity, int addedQuantity)
            {
                RequestedQuantity = requestedQuantity;
                AddedQuantity = addedQuantity;
                RejectedQuantity = requestedQuantity - addedQuantity;
            }
        }

        public struct ItemPlacement
        {
            public int itemHashId;
            public int x;
            public int y;
            public ushort width;
            public ushort height;
            public ushort maxStack;
            public ushort stackCount;
            public ushort lockedCount;
            public ushort stateFlags;
            public byte geneticsMask;
            public ushort qualityMilli;
            public byte durability;
            public uint lastUpdateUnixSeconds;
            public float weight;
            public float unitVolumeM3;
            public float unitRadiationSv;
            public byte categoryId;
            public byte rarity;
            public bool stackable;

            public InventoryGrid.InventoryItemDescriptor Descriptor => new InventoryGrid.InventoryItemDescriptor(
                itemHashId,
                (byte)width,
                (byte)height,
                maxStack,
                weight,
                categoryId,
                rarity,
                stackable);
        }

        [Header("── Grid Settings ──────────────────")]
        [Tooltip("Inventory grid column count.")]
        [SerializeField] private int columns = 8;
        [Tooltip("Inventory grid row count.")]
        [SerializeField] private int rows = 6;
        [Tooltip("Hard transfer cap for carried container mass in kilograms.")]
        [SerializeField, Min(0f)] private float maxWeightKg = 200f;
        [Tooltip("Hard transfer cap for carried container volume in liters.")]
        [SerializeField, Min(0f)] private float maxVolumeLiters = 160f;

        [Header("── References ─────────────────────")]
        [Tooltip("Optional survival system weight sink.")]
        [SerializeField] private HectonSurvivalSystem survival;
        [Tooltip("Item catalog used for load-time and UI seam resolution.")]
        [SerializeField] private ItemCatalog itemCatalog;
        [Tooltip("Inventory radiation threshold in Sv before carried isotopes push trauma every SlowTick.")]
        [SerializeField, Min(0f)] private float radiationTraumaThresholdSv = 0.5f;

        private InventoryGrid _grid;
        private NativeArray<uint> _itemHashes;
        private NativeArray<ushort> _stackCounts;
        private NativeArray<float> _itemCondition;
        private NativeArray<float> _itemDurability;
        private NativeArray<ushort> _craftLockedCounts;
        private NativeArray<ushort> _anchorStateFlags;
        private NativeArray<ushort> _itemStateFlags;
        private NativeArray<byte> _itemGenetics;
        private NativeArray<ushort> _qualityMilli;
        private NativeArray<byte> _durabilities;
        private NativeArray<uint> _lastUpdateUnixSeconds;
        private NativeArray<ushort> _scavengeSimStackCounts;
        private NativeArray<byte> _simulationOccupiedCells;
        private NativeArray<float> _anchorUnitMassKg;
        private NativeArray<float> _anchorUnitVolumeM3;
        private NativeArray<float> _anchorUnitRadiationSv;
        private NativeArray<int> _massAnchorHashSnapshot;
        private NativeArray<ushort> _massStackCountSnapshot;
        private NativeArray<float> _massUnitMassSnapshot;
        private NativeArray<float> _massUnitVolumeSnapshot;
        private NativeArray<float> _massUnitRadiationSnapshot;
        private NativeArray<float3> _derivedMassVolumeScratch;
        private NativeArray<int> _radioactiveConversionAnchors;
        private NativeArray<int> _radioactiveHalfLifeCounters;
        private NativeArray<float> _thermalRunawayByAnchor;
        private NativeArray<int2> _thermalRunawayPairs;
        private NativeArray<int> _thermalRunawayCounters;
        private NativeArray<byte> _inventoryShadowBuffer;
        private NativeArray<InventoryTelemetryEntry> _inventoryBlackBox;
        private NativeArray<int> _salinityCorrosionJobResult;
        private NativeArray<uint> _salinityBrokenItemHashes;
        private NativeArray<SalinityCorrosionTelemetryEntry> _salinityCorrosionBlackBox;
        private NativeArray<int> _defragItemHashes;
        private NativeArray<ushort> _defragItemCounts;
        private NativeArray<byte> _defragCategories;
        private NativeArray<ushort> _defragMaxStacks;
        private NativeArray<byte> _defragRarities;
        private NativeArray<byte> _defragWidths;
        private NativeArray<byte> _defragHeights;
        private NativeArray<byte> _defragFlags;
        private NativeArray<ushort> _defragStateFlags;
        private NativeArray<byte> _defragGenetics;
        private NativeArray<ushort> _defragQualityMilli;
        private NativeArray<byte> _defragDurabilities;
        private NativeArray<uint> _defragLastUpdateUnixSeconds;
        private NativeArray<float> _defragUnitMassKg;
        private NativeArray<float> _defragUnitVolumeM3;
        private NativeArray<float> _defragUnitRadiationSv;
        private NativeArray<int> _defragResult;
        private ItemPlacement[] _sortBuffer;
        private JobHandle _massVolumeJobHandle;
        private bool _registeredSlowTick;
        private bool _registeredLateFrameTick;
        private bool _massVolumeJobScheduled;
        private bool _massCacheDirty = true;
        private int _massVolumeJobInventoryVersion;
        private ulong _playerImpactBodyId;
        private TraumaDispatcher _traumaDispatcher;
        private int _pressurizedContainerProtectionCount;
        private InventoryDTO _lastCommittedInventoryDto;
        private InventoryDTO _pendingInventoryDto;
        private uint _inventoryDirtyRevision = 1u;
        private uint _pendingInventorySaveRevision;
        private uint _inventoryShadowHash;
        private uint _lastCommittedInventoryShadowHash;
        private uint _pendingInventoryShadowHash;
        private int _inventoryShadowPayloadLength;
        private bool _isDirty = true;
        private bool _hasCommittedInventoryDto;
        private bool _hasPendingInventoryCommit;
        private bool _inventoryShadowValid;
        private bool _hasCommittedInventoryShadowHash;
        private bool _durabilitySnapshotDirty = true;
        private byte _coldDurabilityTickPhase;
        private int _inventoryBlackBoxCursor;
        private byte _inventoryBlackBoxDumped;
        private int _salinityCorrosionBlackBoxCursor;
        private byte _salinityCorrosionBlackBoxDumped;
        private byte _equipmentFailingHudLatched;
        private float _salinityCorrosionTickAccumulator;
        private float _currentSalinityFactor;
        private float _averageEquipmentDurability01 = 1f;
        private uint _currentSalinityBiomeHash;
        private uint _lastRepairTitaniumFrame;
        private int _lastInventorySortCommandFrame = -1;
        private int _lastDefragTimeMicroseconds;
        private float _currentWeightKg;
        private float _currentVolumeLiters;

        public float TotalWeight { get; private set; }
        public float TotalMassKg => _currentWeightKg;
        public ref readonly float CurrentWeightKg => ref _currentWeightKg;
        public float TotalVolumeM3 { get; private set; }
        public float CurrentVolumeLiters => _currentVolumeLiters;
        public float MaxWeightKg => math.max(0f, maxWeightKg);
        public float MaxVolumeLiters => math.max(0f, maxVolumeLiters);
        public float TotalRadiationSv { get; private set; }
        public float AverageEquipmentDurability01 => _averageEquipmentDurability01;
        public float CachedInventoryLoad01 { get; private set; }
        public float CachedMaxSwimSpeedMultiplier { get; private set; } = 1f;
        public ulong CurrentInventoryMask { get; private set; }
        public bool HasPressurizedContainerProtection => _pressurizedContainerProtectionCount > 0;
        public InventoryGrid Grid => _grid;
        public ItemCatalog ItemCatalog => itemCatalog;
        public int InventoryVersion { get; private set; }
        public event Action InventoryChanged;

        public int SavePriority => 20;
        public int LoadPriority => 20;

        /// <summary>
        /// Registers one active pressurized storage protector for this inventory.
        /// </summary>
        public void AddPressurizedContainerProtection()
        {
            if (_pressurizedContainerProtectionCount < int.MaxValue)
                _pressurizedContainerProtectionCount++;
        }

        /// <summary>
        /// Removes one active pressurized storage protector from this inventory.
        /// </summary>
        public void RemovePressurizedContainerProtection()
        {
            if (_pressurizedContainerProtectionCount > 0)
                _pressurizedContainerProtectionCount--;
        }

        internal static bool IsFaunaBaitItem(ItemData itemData)
        {
            if (itemData == null)
                return false;

            return itemData.category == ItemCategory.Organic ||
                   itemData.resourceFamily == ResourceFamily.Organic ||
                   itemData.isConsumable;
        }

        private void Awake()
        {
            _grid = new InventoryGrid(columns, rows);
            // COLD ALLOC: uint[columns * rows] - hash-only SOA mirror for zero-GC crafting/UI reads - owner: PlayerInventory
            _itemHashes = new NativeArray<uint>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: ushort[columns * rows] — anchor stack counts — owner: PlayerInventory
            _stackCounts = new NativeArray<ushort>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: float[columns * rows] - normalized item condition SOA mirror for FrostTick decay/UI reads - owner: PlayerInventory
            _itemCondition = new NativeArray<float>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: float[columns * rows] - salinity equipment durability SOA mirror mapped 1:1 with _itemHashes - owner: PlayerInventory
            _itemDurability = new NativeArray<float>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: ushort[columns * rows] — craft reservations per anchor — owner: PlayerInventory
            _craftLockedCounts = new NativeArray<ushort>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: ushort[columns * rows] — per-anchor state flags — owner: PlayerInventory
            _anchorStateFlags = new NativeArray<ushort>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: ushort[columns * rows] — persistent per-anchor item-state flags — owner: PlayerInventory
            _itemStateFlags = new NativeArray<ushort>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _itemGenetics = new NativeArray<byte>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: byte[columns * rows] - compressed per-anchor item genetics flags - owner: PlayerInventory
            // COLD ALLOC: ushort[columns * rows] — persistent per-anchor quality values (0-1000) — owner: PlayerInventory
            _qualityMilli = new NativeArray<ushort>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: uint[columns * rows] — persistent per-anchor last update timestamps — owner: PlayerInventory
            _durabilities = new NativeArray<byte>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: byte[columns * rows] - direct UI durability SOA mirror (0-100) - owner: PlayerInventory
            _lastUpdateUnixSeconds = new NativeArray<uint>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: ushort[columns * rows] — stack simulation scratch — owner: PlayerInventory
            _scavengeSimStackCounts = new NativeArray<ushort>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: byte[columns * rows] — occupancy simulation scratch — owner: PlayerInventory
            _simulationOccupiedCells = new NativeArray<byte>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: ItemPlacement[columns * rows] — placement snapshot buffer — owner: PlayerInventory
            _anchorUnitMassKg = new NativeArray<float>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: float[columns * rows] â€” per-anchor unit mass cache for Burst-derived carry totals â€” owner: PlayerInventory
            _anchorUnitVolumeM3 = new NativeArray<float>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: float[columns * rows] â€” per-anchor unit volume cache for Burst-derived carry totals â€” owner: PlayerInventory
            _anchorUnitRadiationSv = new NativeArray<float>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: float[columns * rows] — per-anchor inventory radiation cache for Burst half-life and trauma totals — owner: PlayerInventory
            _massAnchorHashSnapshot = new NativeArray<int>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: int[columns * rows] - SlowTick mass job hash snapshot - owner: PlayerInventory
            _massStackCountSnapshot = new NativeArray<ushort>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: ushort[columns * rows] - SlowTick mass job stack snapshot - owner: PlayerInventory
            _massUnitMassSnapshot = new NativeArray<float>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: float[columns * rows] - SlowTick mass job mass snapshot - owner: PlayerInventory
            _massUnitVolumeSnapshot = new NativeArray<float>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: float[columns * rows] - SlowTick mass job volume snapshot - owner: PlayerInventory
            _massUnitRadiationSnapshot = new NativeArray<float>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: float[columns * rows] - SlowTick mass job radiation snapshot - owner: PlayerInventory
            _derivedMassVolumeScratch = new NativeArray<float3>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: float3[1] - Burst-derived mass/volume/radiation totals scratch - owner: PlayerInventory
            _radioactiveConversionAnchors = new NativeArray<int>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: int[columns * rows] — radioactive half-life conversion anchor scratch — owner: PlayerInventory
            _radioactiveHalfLifeCounters = new NativeArray<int>(2, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: int[2] — radioactive half-life changed/conversion counters — owner: PlayerInventory
            _thermalRunawayByAnchor = new NativeArray<float>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: float[columns * rows] — reactive chemistry thermal runaway cache — owner: PlayerInventory
            _thermalRunawayPairs = new NativeArray<int2>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: int2[columns * rows] — reactive chemistry explosion pair scratch — owner: PlayerInventory
            _thermalRunawayCounters = new NativeArray<int>(2, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: int[2] — reactive chemistry pair/change counters — owner: PlayerInventory
            _inventoryShadowBuffer = new NativeArray<byte>(InventoryShadowBufferBytes, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: byte[16KB] - persistent inventory dehydration shadow payload - owner: PlayerInventory
            _inventoryBlackBox = new NativeArray<InventoryTelemetryEntry>(InventoryBlackBoxCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: InventoryTelemetryEntry[300] - fixed inventory black-box ring - owner: PlayerInventory
            _salinityCorrosionJobResult = new NativeArray<int>(InventoryCorrosionConstants.ResultRequiredLength, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: int[5] - salinity corrosion job summary - owner: PlayerInventory
            _salinityBrokenItemHashes = new NativeArray<uint>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: uint[columns * rows] - FrostTick break event hashes - owner: PlayerInventory
            _salinityCorrosionBlackBox = new NativeArray<SalinityCorrosionTelemetryEntry>(InventoryBlackBoxCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: SalinityCorrosionTelemetryEntry[300] - equipment corrosion black-box ring - owner: PlayerInventory
            _defragItemHashes = new NativeArray<int>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: int[columns * rows] - native defrag hash stream - owner: PlayerInventory
            _defragItemCounts = new NativeArray<ushort>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: ushort[columns * rows] - native defrag count stream - owner: PlayerInventory
            _defragCategories = new NativeArray<byte>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: byte[columns * rows] - native defrag category stream - owner: PlayerInventory
            _defragMaxStacks = new NativeArray<ushort>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: ushort[columns * rows] - native defrag max-stack stream - owner: PlayerInventory
            _defragRarities = new NativeArray<byte>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: byte[columns * rows] - native defrag rarity stream - owner: PlayerInventory
            _defragWidths = new NativeArray<byte>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: byte[columns * rows] - native defrag width stream - owner: PlayerInventory
            _defragHeights = new NativeArray<byte>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: byte[columns * rows] - native defrag height stream - owner: PlayerInventory
            _defragFlags = new NativeArray<byte>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: byte[columns * rows] - native defrag flags stream - owner: PlayerInventory
            _defragStateFlags = new NativeArray<ushort>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: ushort[columns * rows] - native defrag state stream - owner: PlayerInventory
            _defragGenetics = new NativeArray<byte>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: byte[columns * rows] - native defrag genetics stream - owner: PlayerInventory
            _defragQualityMilli = new NativeArray<ushort>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: ushort[columns * rows] - native defrag quality stream - owner: PlayerInventory
            _defragDurabilities = new NativeArray<byte>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: byte[columns * rows] - native defrag durability stream - owner: PlayerInventory
            _defragLastUpdateUnixSeconds = new NativeArray<uint>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: uint[columns * rows] - native defrag timestamp stream - owner: PlayerInventory
            _defragUnitMassKg = new NativeArray<float>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: float[columns * rows] - native defrag mass stream - owner: PlayerInventory
            _defragUnitVolumeM3 = new NativeArray<float>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: float[columns * rows] - native defrag volume stream - owner: PlayerInventory
            _defragUnitRadiationSv = new NativeArray<float>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: float[columns * rows] - native defrag radiation stream - owner: PlayerInventory
            _defragResult = new NativeArray<int>(InventoryDefragResultSlots.RequiredLength, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: int[4] - native defrag result scratch - owner: PlayerInventory
            RegisterNativeMemorySentinel();
            _sortBuffer = new ItemPlacement[columns * rows];
            TryGetComponent(out _traumaDispatcher);
        }

        private void OnEnable()
        {
            GlobalRegistry.Save?.Register(this);
            TryRegisterSlowTick();
            TryRegisterLateFrameTick();
            PhysicsEvents.Register(this);
            ResolvePlayerImpactBodyId();
        }

        private void OnDisable()
        {
            PhysicsEvents.Unregister(this);
            GlobalRegistry.Save?.Unregister(this);
            TryUnregisterSlowTick();
            TryUnregisterLateFrameTick();
            CompleteInventoryMassRecomputeJob(forceComplete: true);
        }

        private void OnDestroy()
        {
            TryUnregisterLateFrameTick();
            CompleteInventoryMassRecomputeJob(forceComplete: true);

            if (_grid != null)
            {
                _grid.Dispose(default);
                _grid = null;
            }

            DisposeNativeArray(ref _itemHashes);
            DisposeNativeArray(ref _stackCounts);
            DisposeNativeArray(ref _itemCondition);
            DisposeNativeArray(ref _itemDurability);
            DisposeNativeArray(ref _craftLockedCounts);
            DisposeNativeArray(ref _anchorStateFlags);
            DisposeNativeArray(ref _itemStateFlags);
            DisposeNativeArray(ref _itemGenetics);
            DisposeNativeArray(ref _qualityMilli);
            DisposeNativeArray(ref _durabilities);
            DisposeNativeArray(ref _lastUpdateUnixSeconds);
            DisposeNativeArray(ref _scavengeSimStackCounts);
            DisposeNativeArray(ref _simulationOccupiedCells);
            DisposeNativeArray(ref _anchorUnitMassKg);
            DisposeNativeArray(ref _anchorUnitVolumeM3);
            DisposeNativeArray(ref _anchorUnitRadiationSv);
            DisposeNativeArray(ref _massAnchorHashSnapshot);
            DisposeNativeArray(ref _massStackCountSnapshot);
            DisposeNativeArray(ref _massUnitMassSnapshot);
            DisposeNativeArray(ref _massUnitVolumeSnapshot);
            DisposeNativeArray(ref _massUnitRadiationSnapshot);
            DisposeNativeArray(ref _derivedMassVolumeScratch);
            DisposeNativeArray(ref _radioactiveConversionAnchors);
            DisposeNativeArray(ref _radioactiveHalfLifeCounters);
            DisposeNativeArray(ref _thermalRunawayByAnchor);
            DisposeNativeArray(ref _thermalRunawayPairs);
            DisposeNativeArray(ref _thermalRunawayCounters);
            DisposeNativeArray(ref _inventoryShadowBuffer);
            DisposeNativeArray(ref _inventoryBlackBox);
            DisposeNativeArray(ref _salinityCorrosionJobResult);
            DisposeNativeArray(ref _salinityBrokenItemHashes);
            DisposeNativeArray(ref _salinityCorrosionBlackBox);
            DisposeNativeArray(ref _defragItemHashes);
            DisposeNativeArray(ref _defragItemCounts);
            DisposeNativeArray(ref _defragCategories);
            DisposeNativeArray(ref _defragMaxStacks);
            DisposeNativeArray(ref _defragRarities);
            DisposeNativeArray(ref _defragWidths);
            DisposeNativeArray(ref _defragHeights);
            DisposeNativeArray(ref _defragFlags);
            DisposeNativeArray(ref _defragStateFlags);
            DisposeNativeArray(ref _defragGenetics);
            DisposeNativeArray(ref _defragQualityMilli);
            DisposeNativeArray(ref _defragDurabilities);
            DisposeNativeArray(ref _defragLastUpdateUnixSeconds);
            DisposeNativeArray(ref _defragUnitMassKg);
            DisposeNativeArray(ref _defragUnitVolumeM3);
            DisposeNativeArray(ref _defragUnitRadiationSv);
            DisposeNativeArray(ref _defragResult);

        }

        private void RegisterNativeMemorySentinel()
        {
            NativeMemorySentinel.RegisterNativeArray(_itemHashes, NativeMemoryOwner, nameof(_itemHashes), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_stackCounts, NativeMemoryOwner, nameof(_stackCounts), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_itemCondition, NativeMemoryOwner, nameof(_itemCondition), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_itemDurability, NativeMemoryOwner, nameof(_itemDurability), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_craftLockedCounts, NativeMemoryOwner, nameof(_craftLockedCounts), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_anchorStateFlags, NativeMemoryOwner, nameof(_anchorStateFlags), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_itemStateFlags, NativeMemoryOwner, nameof(_itemStateFlags), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_itemGenetics, NativeMemoryOwner, nameof(_itemGenetics), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_qualityMilli, NativeMemoryOwner, nameof(_qualityMilli), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_durabilities, NativeMemoryOwner, nameof(_durabilities), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_lastUpdateUnixSeconds, NativeMemoryOwner, nameof(_lastUpdateUnixSeconds), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_scavengeSimStackCounts, NativeMemoryOwner, nameof(_scavengeSimStackCounts), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_simulationOccupiedCells, NativeMemoryOwner, nameof(_simulationOccupiedCells), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_anchorUnitMassKg, NativeMemoryOwner, nameof(_anchorUnitMassKg), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_anchorUnitVolumeM3, NativeMemoryOwner, nameof(_anchorUnitVolumeM3), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_anchorUnitRadiationSv, NativeMemoryOwner, nameof(_anchorUnitRadiationSv), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_massAnchorHashSnapshot, NativeMemoryOwner, nameof(_massAnchorHashSnapshot), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_massStackCountSnapshot, NativeMemoryOwner, nameof(_massStackCountSnapshot), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_massUnitMassSnapshot, NativeMemoryOwner, nameof(_massUnitMassSnapshot), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_massUnitVolumeSnapshot, NativeMemoryOwner, nameof(_massUnitVolumeSnapshot), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_massUnitRadiationSnapshot, NativeMemoryOwner, nameof(_massUnitRadiationSnapshot), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_derivedMassVolumeScratch, NativeMemoryOwner, nameof(_derivedMassVolumeScratch), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_radioactiveConversionAnchors, NativeMemoryOwner, nameof(_radioactiveConversionAnchors), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_radioactiveHalfLifeCounters, NativeMemoryOwner, nameof(_radioactiveHalfLifeCounters), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_thermalRunawayByAnchor, NativeMemoryOwner, nameof(_thermalRunawayByAnchor), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_thermalRunawayPairs, NativeMemoryOwner, nameof(_thermalRunawayPairs), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_thermalRunawayCounters, NativeMemoryOwner, nameof(_thermalRunawayCounters), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_inventoryShadowBuffer, NativeMemoryOwner, nameof(_inventoryShadowBuffer), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_inventoryBlackBox, NativeMemoryOwner, nameof(_inventoryBlackBox), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_salinityCorrosionJobResult, NativeMemoryOwner, nameof(_salinityCorrosionJobResult), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_salinityBrokenItemHashes, NativeMemoryOwner, nameof(_salinityBrokenItemHashes), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_salinityCorrosionBlackBox, NativeMemoryOwner, nameof(_salinityCorrosionBlackBox), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_defragItemHashes, NativeMemoryOwner, nameof(_defragItemHashes), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_defragItemCounts, NativeMemoryOwner, nameof(_defragItemCounts), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_defragCategories, NativeMemoryOwner, nameof(_defragCategories), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_defragMaxStacks, NativeMemoryOwner, nameof(_defragMaxStacks), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_defragRarities, NativeMemoryOwner, nameof(_defragRarities), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_defragWidths, NativeMemoryOwner, nameof(_defragWidths), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_defragHeights, NativeMemoryOwner, nameof(_defragHeights), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_defragFlags, NativeMemoryOwner, nameof(_defragFlags), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_defragStateFlags, NativeMemoryOwner, nameof(_defragStateFlags), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_defragGenetics, NativeMemoryOwner, nameof(_defragGenetics), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_defragQualityMilli, NativeMemoryOwner, nameof(_defragQualityMilli), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_defragDurabilities, NativeMemoryOwner, nameof(_defragDurabilities), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_defragLastUpdateUnixSeconds, NativeMemoryOwner, nameof(_defragLastUpdateUnixSeconds), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_defragUnitMassKg, NativeMemoryOwner, nameof(_defragUnitMassKg), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_defragUnitVolumeM3, NativeMemoryOwner, nameof(_defragUnitVolumeM3), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_defragUnitRadiationSv, NativeMemoryOwner, nameof(_defragUnitRadiationSv), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_defragResult, NativeMemoryOwner, nameof(_defragResult), NativeMemoryLifetime);
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose(default);
            array = default;
        }

        private static void RegisterTempJobArray<T>(NativeArray<T> array, string label) where T : struct
        {
            NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob);
        }

        private static void DisposeTempJobArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }

        public void RemoveItemAt(int x, int y)
        {
            if (_grid == null || !_stackCounts.IsCreated)
                return;

            int anchorIndex = AnchorIndex(x, y);
            if (!_grid.TryGetAnchorDescriptor(anchorIndex, out InventoryGrid.InventoryItemDescriptor descriptor) || IsCraftLockedFlagSet(anchorIndex))
                return;

            int count = Mathf.Max(1, (int)_stackCounts[anchorIndex]);
            _grid.RemoveAnchorAt(anchorIndex);
            _stackCounts[anchorIndex] = 0;
            _craftLockedCounts[anchorIndex] = 0;
            _anchorStateFlags[anchorIndex] = 0;
            _itemStateFlags[anchorIndex] = 0;
            _itemGenetics[anchorIndex] = 0;
            _qualityMilli[anchorIndex] = 0;
            if (_itemDurability.IsCreated && (uint)anchorIndex < (uint)_itemDurability.Length)
                _itemDurability[anchorIndex] = 0f;
            if (_durabilities.IsCreated && (uint)anchorIndex < (uint)_durabilities.Length)
                _durabilities[anchorIndex] = 0;
            _durabilitySnapshotDirty = true;
            _lastUpdateUnixSeconds[anchorIndex] = 0;
            ClearAnchorPhysicalMetadata(anchorIndex);

            TotalWeight = Mathf.Max(0f, TotalWeight - descriptor.Weight * count);
            NotifyInventoryChanged();
        }

        public int RemoveOneItem(int anchorX, int anchorY)
        {
            return TryRemoveOneItemWithState(
                anchorX,
                anchorY,
                out int itemHashId,
                out _,
                out _,
                out _)
                ? itemHashId
                : 0;
        }

        public bool TryRemoveOneItemWithState(
            int anchorX,
            int anchorY,
            out int itemHashId,
            out ushort stateFlags,
            out ulong geneticsMask,
            out ushort qualityMilli)
        {
            itemHashId = 0;
            stateFlags = 0;
            geneticsMask = 0UL;
            qualityMilli = 0;
            if (_grid == null || !_stackCounts.IsCreated)
                return false;

            int anchorIndex = AnchorIndex(anchorX, anchorY);
            if (!_grid.TryGetAnchorDescriptor(anchorIndex, out InventoryGrid.InventoryItemDescriptor descriptor))
                return false;

            int count = Mathf.Max(1, (int)_stackCounts[anchorIndex]);
            int unlockedCount = Mathf.Max(0, count - GetReservedCraftCount(anchorIndex));
            if (unlockedCount <= 0)
                return false;

            itemHashId = descriptor.HashId;
            stateFlags = _itemStateFlags.IsCreated ? _itemStateFlags[anchorIndex] : (ushort)0;
            geneticsMask = _itemGenetics.IsCreated ? ExpandItemGenetics(_itemGenetics[anchorIndex]) : 0UL;
            qualityMilli = _qualityMilli.IsCreated && _qualityMilli[anchorIndex] > 0
                ? _qualityMilli[anchorIndex]
                : DefaultQualityMilli;

            if (count > 1)
            {
                _stackCounts[anchorIndex] = (ushort)(count - 1);
            }
            else
            {
                _grid.RemoveAnchorAt(anchorIndex);
                _stackCounts[anchorIndex] = 0;
                _craftLockedCounts[anchorIndex] = 0;
                _anchorStateFlags[anchorIndex] = 0;
                _itemStateFlags[anchorIndex] = 0;
                _itemGenetics[anchorIndex] = 0;
                _qualityMilli[anchorIndex] = 0;
                if (_itemDurability.IsCreated && (uint)anchorIndex < (uint)_itemDurability.Length)
                    _itemDurability[anchorIndex] = 0f;
                if (_durabilities.IsCreated && (uint)anchorIndex < (uint)_durabilities.Length)
                    _durabilities[anchorIndex] = 0;
                _lastUpdateUnixSeconds[anchorIndex] = 0;
                ClearAnchorPhysicalMetadata(anchorIndex);
            }

            TotalWeight = Mathf.Max(0f, TotalWeight - descriptor.Weight);
            NotifyInventoryChanged();
            return true;
        }

        public bool TryDropOneItemToWorldSignal(
            int anchorX,
            int anchorY,
            Vector3 runtimePosition,
            Vector3 initialImpulse,
            Transform interactor,
            out int droppedHashId)
        {
            droppedHashId = 0;
            if (!TryRemoveOneItemWithState(
                    anchorX,
                    anchorY,
                    out int itemHashId,
                    out _,
                    out ulong geneticsMask,
                    out ushort qualityMilli))
            {
                return false;
            }

            ItemData droppedItem = itemCatalog != null ? itemCatalog.FindByHash(itemHashId) : null;
            if (droppedItem == null)
            {
                TryAddItemWithState(itemHashId, geneticsMask, qualityMilli);
                return false;
            }

            PersistentWorldRegistry persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;
            if (persistentWorldRegistry == null ||
                !persistentWorldRegistry.TryRegisterDroppedItemWithState(droppedItem, 1, runtimePosition, geneticsMask, qualityMilli))
            {
                TryAddItemWithState(itemHashId, geneticsMask, qualityMilli);
                return false;
            }

            InventoryPhysicalDropRequestPayload payload = new InventoryPhysicalDropRequestPayload
            {
                RuntimePosition = runtimePosition,
                InitialImpulse = initialImpulse,
                GeneticsMask = geneticsMask,
                ItemHashId = unchecked((uint)itemHashId),
                Quantity = 1,
                QualityMilli = qualityMilli,
                Reserved = 0
            };
            HectonEventBus.Publish(in payload);

            bool hasInteractorPosition = interactor != null;
            ulong interactorEntityId = hasInteractorPosition ? EntityId.ToULong(interactor.GetEntityId()) : 0ul;
            Vector3 interactorPosition = hasInteractorPosition ? interactor.position : Vector3.zero;
            InteractionEvents.RaiseItemLost(droppedItem, 1, interactor);
            HectonEventBus.Publish(new ItemDiscardedEvent(
                droppedItem,
                1,
                interactorEntityId,
                interactorPosition,
                hasInteractorPosition));

            droppedHashId = itemHashId;
            return true;
        }

        public bool ConsumeOneItem(int anchorX, int anchorY)
        {
            if (_grid == null)
                return false;

            int anchorIndex = AnchorIndex(anchorX, anchorY);
            if (!_grid.TryGetAnchorDescriptor(anchorIndex, out InventoryGrid.InventoryItemDescriptor descriptor))
                return false;

            if (!TryGetRuntimeDescriptor(descriptor.HashId, out ItemCatalog.ItemRuntimeDescriptor runtimeDescriptor) ||
                !runtimeDescriptor.IsConsumable)
            {
                return false;
            }

            if (survival != null)
            {
                if (runtimeDescriptor.OxygenRestore > 0f)
                    survival.RefillOxygen(runtimeDescriptor.OxygenRestore);

                if (runtimeDescriptor.EnergyRestore > 0f)
                    survival.RechargeEnergy(runtimeDescriptor.EnergyRestore);

                if (runtimeDescriptor.IntegrityRestore > 0f)
                    survival.Repair(runtimeDescriptor.IntegrityRestore);

                if (runtimeDescriptor.HungerRestore > 0f)
                    survival.AddHunger(runtimeDescriptor.HungerRestore);

                if (runtimeDescriptor.ThirstRestore > 0f)
                    survival.AddThirst(runtimeDescriptor.ThirstRestore);

                if (HectonSurvivalSystem.ShouldApplyNutritionalToxicityOnConsume(descriptor.HashId))
                    survival.ApplyNutritionalToxicity();
            }

            RemoveOneItem(anchorX, anchorY);
            return true;
        }

        public int GetStackCount(int anchorX, int anchorY)
        {
            if (!_stackCounts.IsCreated)
                return 0;

            int index = AnchorIndex(anchorX, anchorY);
            return (uint)index < (uint)_stackCounts.Length ? _stackCounts[index] : 0;
        }

        public int GetItemHashAt(int x, int y)
        {
            return _grid == null ? 0 : _grid.GetCellHashId(x, y);
        }

        public int CountTotal(int itemHashId)
        {
            return CountQuantityByHash(itemHashId, false);
        }

        public int CountAvailableTotal(int itemHashId)
        {
            return CountQuantityByHash(itemHashId, true);
        }

        internal bool TryFindFirstAnchorByHash(int itemHashId, out int anchorIndex)
        {
            anchorIndex = -1;
            if (_grid == null || !_stackCounts.IsCreated || itemHashId == 0)
                return false;

            for (int i = 0; i < _stackCounts.Length; i++)
            {
                if (!_grid.HasAnchor(i) || _grid.GetAnchorHashId(i) != itemHashId)
                    continue;

                int stackCount = Mathf.Max(1, (int)_stackCounts[i]);
                if (GetReservedCraftCount(i) >= stackCount)
                    continue;

                anchorIndex = i;
                return true;
            }

            return false;
        }

        internal bool TryRemoveFirstMatchingItemByHash(int itemHashId)
        {
            if (!TryFindFirstAnchorByHash(itemHashId, out int anchorIndex) || _grid == null)
                return false;

            int anchorX = anchorIndex % _grid.Columns;
            int anchorY = anchorIndex / _grid.Columns;
            return RemoveOneItem(anchorX, anchorY) != 0;
        }

        internal bool TryConsumeFirstMatchingItemByHash(int itemHashId, out ushort stateFlags, out ushort qualityMilli)
        {
            return TryConsumeFirstMatchingItemByHash(itemHashId, out stateFlags, out qualityMilli, out _);
        }

        internal bool TryConsumeFirstMatchingItemByHash(int itemHashId, out ushort stateFlags, out ushort qualityMilli, out ulong geneticsMask)
        {
            stateFlags = 0;
            qualityMilli = 0;
            geneticsMask = 0UL;
            if (!TryFindFirstAnchorByHash(itemHashId, out int anchorIndex) || _grid == null)
                return false;

            stateFlags = _itemStateFlags.IsCreated ? _itemStateFlags[anchorIndex] : (ushort)0;
            geneticsMask = _itemGenetics.IsCreated ? ExpandItemGenetics(_itemGenetics[anchorIndex]) : 0UL;
            qualityMilli = _qualityMilli.IsCreated && _qualityMilli[anchorIndex] > 0
                ? _qualityMilli[anchorIndex]
                : DefaultQualityMilli;

            int anchorX = anchorIndex % _grid.Columns;
            int anchorY = anchorIndex / _grid.Columns;
            return RemoveOneItem(anchorX, anchorY) != 0;
        }

        public bool TryDrainItemConditionByHash(
            int itemHashId,
            float normalizedDrain,
            out int anchorIndex,
            out ushort qualityMilli)
        {
            anchorIndex = -1;
            qualityMilli = 0;
            if (itemHashId == 0 ||
                !math.isfinite(normalizedDrain) ||
                normalizedDrain <= 0f ||
                !TryFindFirstAnchorByHash(itemHashId, out anchorIndex))
            {
                return false;
            }

            return TryDrainItemConditionAtAnchorUnchecked(anchorIndex, normalizedDrain, out qualityMilli);
        }

        public bool TryDrainItemConditionAtAnchor(
            int anchorIndex,
            int itemHashId,
            float normalizedDrain,
            out ushort qualityMilli)
        {
            qualityMilli = 0;
            if (itemHashId == 0 ||
                !math.isfinite(normalizedDrain) ||
                normalizedDrain <= 0f ||
                _grid == null ||
                !_stackCounts.IsCreated ||
                (uint)anchorIndex >= (uint)_stackCounts.Length ||
                !_grid.HasAnchor(anchorIndex) ||
                _grid.GetAnchorHashId(anchorIndex) != itemHashId)
            {
                return false;
            }

            int stackCount = Mathf.Max(1, (int)_stackCounts[anchorIndex]);
            if (GetReservedCraftCount(anchorIndex) >= stackCount)
                return false;

            return TryDrainItemConditionAtAnchorUnchecked(anchorIndex, normalizedDrain, out qualityMilli);
        }

        private bool TryDrainItemConditionAtAnchorUnchecked(int anchorIndex, float normalizedDrain, out ushort qualityMilli)
        {
            qualityMilli = 0;
            if (!math.isfinite(normalizedDrain) ||
                normalizedDrain <= 0f ||
                !_qualityMilli.IsCreated ||
                !_durabilities.IsCreated ||
                !_itemStateFlags.IsCreated ||
                (uint)anchorIndex >= (uint)_qualityMilli.Length ||
                (uint)anchorIndex >= (uint)_itemStateFlags.Length)
            {
                return false;
            }

            ushort currentQualityMilli = _qualityMilli[anchorIndex] > 0
                ? _qualityMilli[anchorIndex]
                : DefaultQualityMilli;
            int drainMilli = math.clamp((int)math.ceil(normalizedDrain * DefaultQualityMilli), 1, DefaultQualityMilli);
            int nextQuality = math.max(0, currentQualityMilli - drainMilli);
            if (nextQuality == currentQualityMilli)
            {
                qualityMilli = currentQualityMilli;
                return false;
            }

            qualityMilli = (ushort)nextQuality;
            _qualityMilli[anchorIndex] = qualityMilli;
            if (_itemDurability.IsCreated && (uint)anchorIndex < (uint)_itemDurability.Length)
                _itemDurability[anchorIndex] = math.saturate(qualityMilli * 0.001f);
            if ((uint)anchorIndex < (uint)_durabilities.Length)
                _durabilities[anchorIndex] = (byte)math.clamp((qualityMilli + 5) / 10, 0, 100);

            if (qualityMilli < DegradedQualityMilliThreshold)
                _itemStateFlags[anchorIndex] |= DegradedItemStateMask;

            _durabilitySnapshotDirty = true;
            NotifyInventoryChanged();
            return true;
        }

        public void AddWeight(float amount)
        {
            TotalWeight = Mathf.Max(0f, TotalWeight + amount);
            RefreshDerivedMassAndSurvivalLoad();
        }

        public bool ContainsItem(int itemHashId)
        {
            return CountAnchorsByHash(itemHashId) > 0;
        }

        public bool TryAddItem(int itemHashId, int quantity = 1)
        {
            return CanAcceptQuantity(itemHashId, quantity) &&
                   TryAddItemInternal(itemHashId, quantity, out _);
        }

        /// <summary>
        /// Preflights whether the current grid can accept the requested item quantity without mutating inventory state.
        /// </summary>
        public bool CanAcceptItemQuantity(int itemHashId, int quantity = 1)
        {
            return CanAcceptQuantity(itemHashId, quantity);
        }

        /// <summary>
        /// Preflights a mixed set of item quantities against one shared grid simulation without mutating live inventory.
        /// </summary>
        public bool CanAcceptItemQuantityBatch(ReadOnlySpan<int> itemHashIds, ReadOnlySpan<int> quantities, int count)
        {
            return CanAcceptQuantityBatch(itemHashIds, quantities, count);
        }

        public bool TryAddItemWithGenetics(int itemHashId, uint geneticsMask, int quantity = 1)
        {
            return TryAddItemWithGenetics(itemHashId, (ulong)geneticsMask, quantity);
        }

        public bool TryAddItemWithGenetics(int itemHashId, ulong geneticsMask, int quantity = 1)
        {
            return TryAddItemWithStateInternal(itemHashId, quantity, geneticsMask, DefaultQualityMilli, out _);
        }

        public bool TryAddItemWithState(int itemHashId, uint geneticsMask, ushort qualityMilli, int quantity = 1)
        {
            return TryAddItemWithState(itemHashId, (ulong)geneticsMask, qualityMilli, quantity);
        }

        public bool TryAddItemWithState(int itemHashId, ulong geneticsMask, ushort qualityMilli, int quantity = 1)
        {
            return TryAddItemWithStateInternal(itemHashId, quantity, geneticsMask, qualityMilli, out _);
        }

        public void SlowTick()
        {
            using (_slowTickProfilerMarker.Auto())
            {
                DrainSalinityBiomeSignals();
                DrainRepairToolTitaniumSignals();
                ApplyInventoryEnvironmentalDegradation();
                ApplyInventorySalinityCorrosion();
                ApplyInventoryColdDurabilityDecay();
                ApplyInventoryRadioactiveHalfLife();
                ApplyInventoryReactiveChemistry();
                ApplyInventoryDepthPressureCrush();
                DispatchInventoryRadiationTrauma();
                if (_massCacheDirty)
                    ScheduleInventoryMassRecomputeJob();
            }
        }

        public void LateFrameTick()
        {
            ConsumeInventoryCommandSignals();
            CompleteInventoryMassRecomputeJob(forceComplete: false);
        }

        public bool TryCopyAvailableItemCountsNonAlloc(
            NativeParallelHashMap<int, int> destination,
            out int uniqueItemCount)
        {
            return TryCopyAvailableItemCountsNonAlloc(destination, out uniqueItemCount, out _);
        }

        public bool TryCopyAvailableItemCountsNonAlloc(
            NativeParallelHashMap<int, int> destination,
            out int uniqueItemCount,
            out ulong availableResourceMask)
        {
            uniqueItemCount = 0;
            availableResourceMask = 0UL;
            if (!destination.IsCreated || _grid == null || !_stackCounts.IsCreated)
                return false;

            destination.Clear();

            for (int anchorIndex = 0; anchorIndex < _stackCounts.Length; anchorIndex++)
            {
                if (!_grid.HasAnchor(anchorIndex))
                    continue;

                int itemHashId = _grid.GetAnchorHashId(anchorIndex);
                if (itemHashId == 0)
                    continue;

                int availableCount = math.max(0, math.max(1, (int)_stackCounts[anchorIndex]) - GetReservedCraftCount(anchorIndex));
                if (availableCount <= 0)
                    continue;

                availableResourceMask |= InventoryMaterialMask.ResolveBit(itemHashId);

                if (destination.TryGetValue(itemHashId, out int existingCount))
                {
                    destination[itemHashId] = existingCount + availableCount;
                    continue;
                }

                if (!destination.TryAdd(itemHashId, availableCount))
                {
                    destination.Clear();
                    uniqueItemCount = 0;
                    availableResourceMask = 0UL;
                    return false;
                }

                uniqueItemCount++;
            }

            return true;
        }

        public bool TryReserveQuantityForCraft(int itemHashId, int quantity, CraftReservation[] reservations, ref int reservationCount)
        {
            if (_grid == null || !_stackCounts.IsCreated || itemHashId == 0 || quantity <= 0 || reservations == null)
                return false;

            int startReservationCount = reservationCount;
            if (!TryReserveAvailableQuantityForCraft(itemHashId, quantity, reservations, ref reservationCount, out int reservedQuantity))
                return false;

            if (reservedQuantity >= quantity)
                return true;

            ReleaseCraftReservationsRange(reservations, startReservationCount, reservationCount);
            reservationCount = startReservationCount;
            return false;
        }

        /// <summary>
        /// Reserves up to <paramref name="maxQuantity"/> local inventory items for crafting in one inventory pass.
        /// </summary>
        /// <param name="itemHashId">Baked item hash to reserve.</param>
        /// <param name="maxQuantity">Maximum quantity to reserve from local inventory.</param>
        /// <param name="reservations">Caller-owned reservation output buffer.</param>
        /// <param name="reservationCount">Current reservation count, advanced by successful reservations.</param>
        /// <param name="reservedQuantity">Actual quantity reserved from local inventory.</param>
        /// <returns>False only when inputs are invalid or the reservation buffer cannot hold the result.</returns>
        public bool TryReserveAvailableQuantityForCraft(
            int itemHashId,
            int maxQuantity,
            CraftReservation[] reservations,
            ref int reservationCount,
            out int reservedQuantity)
        {
            reservedQuantity = 0;
            if (_grid == null || !_stackCounts.IsCreated || itemHashId == 0 || maxQuantity <= 0 || reservations == null)
                return false;

            int startReservationCount = reservationCount;
            int remaining = maxQuantity;
            for (int anchorIndex = 0; anchorIndex < _stackCounts.Length && remaining > 0; anchorIndex++)
            {
                if (!_grid.HasAnchor(anchorIndex) || _grid.GetAnchorHashId(anchorIndex) != itemHashId)
                    continue;

                int stackCount = math.max(1, (int)_stackCounts[anchorIndex]);
                int available = math.max(0, stackCount - GetReservedCraftCount(anchorIndex));
                if (available <= 0)
                    continue;

                if (reservationCount >= reservations.Length)
                {
                    ReleaseCraftReservationsRange(reservations, startReservationCount, reservationCount);
                    reservationCount = startReservationCount;
                    reservedQuantity = 0;
                    return false;
                }

                int take = math.min(available, remaining);
                _craftLockedCounts[anchorIndex] = (ushort)math.min(ushort.MaxValue, _craftLockedCounts[anchorIndex] + take);
                _anchorStateFlags[anchorIndex] |= CraftingLockedMask;
                reservations[reservationCount++] = new CraftReservation
                {
                    AnchorIndex = anchorIndex,
                    Quantity = take,
                    ItemHashId = itemHashId
                };
                remaining -= take;
                reservedQuantity += take;
            }

            return true;
        }

        public void ReleaseCraftReservations(CraftReservation[] reservations, int reservationCount)
        {
            ReleaseCraftReservationsRange(reservations, 0, reservationCount);
        }

        public bool CommitCraftReservations(CraftReservation[] reservations, int reservationCount)
        {
            if (reservations == null || reservationCount <= 0 || _grid == null || !_stackCounts.IsCreated)
                return true;

            for (int i = 0; i < reservationCount; i++)
            {
                if (!IsValidCraftReservation(in reservations[i]))
                {
                    ReleaseCraftReservations(reservations, reservationCount);
                    return false;
                }
            }

            float removedWeight = 0f;
            for (int i = 0; i < reservationCount; i++)
            {
                CraftReservation reservation = reservations[i];
                if (reservation.Quantity <= 0)
                    continue;

                int anchorIndex = reservation.AnchorIndex;
                if (!_grid.TryGetAnchorDescriptor(anchorIndex, out InventoryGrid.InventoryItemDescriptor descriptor))
                    continue;

                _craftLockedCounts[anchorIndex] = (ushort)Mathf.Max(0, _craftLockedCounts[anchorIndex] - reservation.Quantity);
                if (_craftLockedCounts[anchorIndex] == 0)
                    _anchorStateFlags[anchorIndex] = (ushort)(_anchorStateFlags[anchorIndex] & ~CraftingLockedMask);

                int stackCount = Mathf.Max(1, (int)_stackCounts[anchorIndex]);
                int remainingStack = stackCount - reservation.Quantity;
                if (remainingStack <= 0)
                {
                    _grid.RemoveAnchorAt(anchorIndex);
                    _stackCounts[anchorIndex] = 0;
                    _craftLockedCounts[anchorIndex] = 0;
                    _anchorStateFlags[anchorIndex] = 0;
                    _itemStateFlags[anchorIndex] = 0;
                    _itemGenetics[anchorIndex] = 0;
                    _qualityMilli[anchorIndex] = 0;
                    if (_itemDurability.IsCreated && (uint)anchorIndex < (uint)_itemDurability.Length)
                        _itemDurability[anchorIndex] = 0f;
                    if (_durabilities.IsCreated && (uint)anchorIndex < (uint)_durabilities.Length)
                        _durabilities[anchorIndex] = 0;
                    _lastUpdateUnixSeconds[anchorIndex] = 0;
                    ClearAnchorPhysicalMetadata(anchorIndex);
                }
                else
                {
                    _stackCounts[anchorIndex] = (ushort)remainingStack;
                }

                removedWeight += descriptor.Weight * reservation.Quantity;
                reservations[i] = default;
            }

            TotalWeight = Mathf.Max(0f, TotalWeight - removedWeight);
            NotifyInventoryChanged();
            return true;
        }

        public bool HasCraftReservations()
        {
            if (!_craftLockedCounts.IsCreated)
                return false;

            for (int i = 0; i < _craftLockedCounts.Length; i++)
            {
                if (IsCraftLockedFlagSet(i) && _craftLockedCounts[i] > 0)
                    return true;
            }

            return false;
        }

        public ScavengeAttemptResult ScavengeAttempt(int itemHashId, int quantity, Transform interactor)
        {
            return ScavengeAttempt(itemHashId, quantity, interactor, 0UL, DefaultQualityMilli);
        }

        public ScavengeAttemptResult ScavengeAttempt(int itemHashId, int quantity, Transform interactor, uint geneticsMask, ushort qualityMilli)
        {
            return ScavengeAttempt(itemHashId, quantity, interactor, (ulong)geneticsMask, qualityMilli);
        }

        public ScavengeAttemptResult ScavengeAttempt(int itemHashId, int quantity, Transform interactor, ulong geneticsMask, ushort qualityMilli)
        {
            if (itemHashId == 0 || quantity <= 0)
                return new ScavengeAttemptResult(Mathf.Max(0, quantity), 0);

            TryAddItemWithStateInternal(itemHashId, quantity, geneticsMask, qualityMilli, out int addedQuantity);
            return new ScavengeAttemptResult(quantity, addedQuantity);
        }

        public bool TryRemoveQuantity(int itemHashId, int quantity)
        {
            if (_grid == null || !_stackCounts.IsCreated || itemHashId == 0 || quantity <= 0)
                return false;

            if (CountAvailableTotal(itemHashId) < quantity)
                return false;

            int remaining = quantity;
            for (int anchorIndex = 0; anchorIndex < _stackCounts.Length && remaining > 0; anchorIndex++)
            {
                if (!_grid.HasAnchor(anchorIndex) || _grid.GetAnchorHashId(anchorIndex) != itemHashId)
                    continue;

                int stackCount = Mathf.Max(1, (int)_stackCounts[anchorIndex]);
                int available = Mathf.Max(0, stackCount - GetReservedCraftCount(anchorIndex));
                if (available <= 0)
                    continue;

                int take = Mathf.Min(available, remaining);
                if (!_grid.TryGetAnchorDescriptor(anchorIndex, out InventoryGrid.InventoryItemDescriptor descriptor))
                    continue;

                if (take >= stackCount && !IsCraftLockedFlagSet(anchorIndex))
                {
                    _grid.RemoveAnchorAt(anchorIndex);
                    _stackCounts[anchorIndex] = 0;
                    _craftLockedCounts[anchorIndex] = 0;
                    _anchorStateFlags[anchorIndex] = 0;
                    _itemStateFlags[anchorIndex] = 0;
                    _itemGenetics[anchorIndex] = 0;
                    _qualityMilli[anchorIndex] = 0;
                    if (_itemDurability.IsCreated && (uint)anchorIndex < (uint)_itemDurability.Length)
                        _itemDurability[anchorIndex] = 0f;
                    if (_durabilities.IsCreated && (uint)anchorIndex < (uint)_durabilities.Length)
                        _durabilities[anchorIndex] = 0;
                    _lastUpdateUnixSeconds[anchorIndex] = 0;
                    ClearAnchorPhysicalMetadata(anchorIndex);
                }
                else
                {
                    _stackCounts[anchorIndex] = (ushort)(stackCount - take);
                }

                TotalWeight -= descriptor.Weight * take;
                remaining -= take;
            }

            TotalWeight = Mathf.Max(0f, TotalWeight);
            NotifyInventoryChanged();
            return true;
        }

        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            if (_isDirty || !_inventoryShadowValid)
                RefreshInventoryShadowBufferFromRuntime();

            AttachInventoryShadowPayload(data);
            ref InventoryDTO dto = ref data.inventory;
            if (!_isDirty && _hasCommittedInventoryDto)
            {
                dto = _lastCommittedInventoryDto;
                _hasPendingInventoryCommit = false;
                return;
            }

            if (_hasCommittedInventoryShadowHash &&
                _inventoryShadowValid &&
                _inventoryShadowHash == _lastCommittedInventoryShadowHash &&
                _hasCommittedInventoryDto)
            {
                dto = _lastCommittedInventoryDto;
                _isDirty = false;
                _hasPendingInventoryCommit = false;
                return;
            }

            PopulateInventoryDtoFromRuntime(ref _pendingInventoryDto);
            dto = _pendingInventoryDto;
            _pendingInventorySaveRevision = _inventoryDirtyRevision;
            _pendingInventoryShadowHash = _inventoryShadowHash;
            _hasPendingInventoryCommit = true;
        }

        private void PopulateInventoryDtoFromRuntime(ref InventoryDTO dto)
        {
            dto.EnsureCapacity();
            if (_grid == null)
            {
                dto.gridColumns = columns;
                dto.gridRows = rows;
                dto.totalWeight = 0f;
                dto.cellCount = 0;
                dto.itemDurabilityRleLength = 0;
                return;
            }

            dto.gridColumns = _grid.Columns;
            dto.gridRows = _grid.Rows;
            dto.totalWeight = TotalWeight;

            int cellIndex = 0;
            for (int anchorIndex = 0; anchorIndex < _stackCounts.Length && cellIndex < InventoryDTO.MaxCells; anchorIndex++)
            {
                if (!_grid.HasAnchor(anchorIndex))
                    continue;

                int x = anchorIndex % _grid.Columns;
                int y = anchorIndex / _grid.Columns;
                dto.itemHashIds[cellIndex] = _grid.GetAnchorHashId(anchorIndex);
                dto.packedCellCoordinates[cellIndex] = InventoryDTO.PackCellCoordinate(x, y);
                dto.stackCounts[cellIndex] = _stackCounts[anchorIndex];
                dto.itemStateFlags[cellIndex] = _itemStateFlags[anchorIndex];
                dto.itemGeneticsWords[cellIndex] = _itemGenetics[anchorIndex];
                dto.qualityMilli[cellIndex] = _qualityMilli[anchorIndex] > 0 ? _qualityMilli[anchorIndex] : DefaultQualityMilli;
                dto.lastUpdateUnixSeconds[cellIndex] = _lastUpdateUnixSeconds[anchorIndex];
                dto.itemDurabilityRle[cellIndex] = _itemDurability.IsCreated && (uint)anchorIndex < (uint)_itemDurability.Length
                    ? QuantizeDurabilitySByte(_itemDurability[anchorIndex])
                    : QuantizeDurabilitySByte(dto.qualityMilli[cellIndex] * 0.001f);
                cellIndex++;
            }

            dto.cellCount = cellIndex;
            dto.itemDurabilityRleLength = EncodeItemDurabilityRle(ref dto);
        }

        private void RefreshInventoryShadowBufferFromRuntime()
        {
            if (!_inventoryShadowBuffer.IsCreated)
            {
                _inventoryShadowPayloadLength = 0;
                _inventoryShadowHash = 0u;
                _inventoryShadowValid = false;
                return;
            }

            PopulateInventoryDtoFromRuntime(ref _pendingInventoryDto);
            int offset = 0;
            uint hash = Fnv1a32Offset;
            int count = math.min(_pendingInventoryDto.cellCount, InventoryDTO.MaxCells);
            WriteInventoryShadowInt(ref offset, ref hash, count);

            WriteInventoryShadowInt(ref offset, ref hash, count);
            for (int i = 0; i < count; i++)
                WriteInventoryShadowInt(ref offset, ref hash, _pendingInventoryDto.itemHashIds[i]);

            WriteInventoryShadowInt(ref offset, ref hash, count);
            for (int i = 0; i < count; i++)
                WriteInventoryShadowUInt(ref offset, ref hash, _pendingInventoryDto.packedCellCoordinates[i]);

            WriteInventoryShadowInt(ref offset, ref hash, count);
            for (int i = 0; i < count; i++)
                WriteInventoryShadowUShort(ref offset, ref hash, _pendingInventoryDto.stackCounts[i]);

            WriteInventoryShadowInt(ref offset, ref hash, count);
            for (int i = 0; i < count; i++)
                WriteInventoryShadowUShort(ref offset, ref hash, _pendingInventoryDto.itemStateFlags[i]);

            WriteInventoryShadowInt(ref offset, ref hash, count);
            for (int i = 0; i < count; i++)
                WriteInventoryShadowByte(ref offset, ref hash, _pendingInventoryDto.itemGeneticsWords[i]);

            WriteInventoryShadowInt(ref offset, ref hash, count);
            for (int i = 0; i < count; i++)
                WriteInventoryShadowUShort(ref offset, ref hash, _pendingInventoryDto.qualityMilli[i]);

            WriteInventoryShadowInt(ref offset, ref hash, count);
            for (int i = 0; i < count; i++)
                WriteInventoryShadowUInt(ref offset, ref hash, _pendingInventoryDto.lastUpdateUnixSeconds[i]);

            int durabilityRleLength = math.clamp(
                _pendingInventoryDto.itemDurabilityRleLength,
                0,
                _pendingInventoryDto.itemDurabilityRle != null ? _pendingInventoryDto.itemDurabilityRle.Length : 0);
            WriteInventoryShadowInt(ref offset, ref hash, durabilityRleLength);
            for (int i = 0; i < durabilityRleLength; i++)
                WriteInventoryShadowByte(ref offset, ref hash, _pendingInventoryDto.itemDurabilityRle[i]);

            WriteInventoryShadowUInt(ref offset, ref hash, math.asuint(_pendingInventoryDto.totalWeight));
            WriteInventoryShadowInt(ref offset, ref hash, _pendingInventoryDto.gridColumns);
            WriteInventoryShadowInt(ref offset, ref hash, _pendingInventoryDto.gridRows);

            _inventoryShadowPayloadLength = offset;
            _inventoryShadowHash = hash;
            _inventoryShadowValid = true;
        }

        private void AttachInventoryShadowPayload(SaveData data)
        {
            if (data == null || !_inventoryShadowValid || !_inventoryShadowBuffer.IsCreated)
                return;

            data.inventoryShadowPayload = _inventoryShadowBuffer;
            data.inventoryShadowPayloadLength = _inventoryShadowPayloadLength;
            data.inventoryShadowPayloadHash = _inventoryShadowHash;
            data.hasInventoryShadowPayload = _inventoryShadowPayloadLength > 0;
        }

        private void CommitCurrentInventoryShadowHash()
        {
            RefreshInventoryShadowBufferFromRuntime();
            _lastCommittedInventoryShadowHash = _inventoryShadowHash;
            _hasCommittedInventoryShadowHash = _inventoryShadowValid;
        }

        private static void CopyInventoryDto(ref InventoryDTO destination, in InventoryDTO source)
        {
            destination.EnsureCapacity();
            destination.cellCount = math.clamp(source.cellCount, 0, InventoryDTO.MaxCells);
            destination.gridColumns = source.gridColumns;
            destination.gridRows = source.gridRows;
            destination.totalWeight = source.totalWeight;
            destination.itemDurabilityRleLength = math.clamp(
                source.itemDurabilityRleLength,
                0,
                math.min(
                    destination.itemDurabilityRle != null ? destination.itemDurabilityRle.Length : 0,
                    source.itemDurabilityRle != null ? source.itemDurabilityRle.Length : 0));

            for (int i = 0; i < InventoryDTO.MaxCells; i++)
            {
                bool active = i < destination.cellCount;
                destination.itemHashIds[i] = active && source.itemHashIds != null && i < source.itemHashIds.Length ? source.itemHashIds[i] : 0;
                destination.packedCellCoordinates[i] = active && source.packedCellCoordinates != null && i < source.packedCellCoordinates.Length ? source.packedCellCoordinates[i] : 0u;
                destination.stackCounts[i] = active && source.stackCounts != null && i < source.stackCounts.Length ? source.stackCounts[i] : (ushort)0;
                destination.itemStateFlags[i] = active && source.itemStateFlags != null && i < source.itemStateFlags.Length ? source.itemStateFlags[i] : (ushort)0;
                destination.itemGeneticsWords[i] = active && source.itemGeneticsWords != null && i < source.itemGeneticsWords.Length ? source.itemGeneticsWords[i] : (byte)0;
                destination.qualityMilli[i] = active && source.qualityMilli != null && i < source.qualityMilli.Length ? source.qualityMilli[i] : (ushort)0;
                destination.lastUpdateUnixSeconds[i] = active && source.lastUpdateUnixSeconds != null && i < source.lastUpdateUnixSeconds.Length ? source.lastUpdateUnixSeconds[i] : 0u;
            }

            for (int i = 0; i < InventoryDTO.MaxDurabilityRleBytes; i++)
            {
                bool active = i < destination.itemDurabilityRleLength;
                destination.itemDurabilityRle[i] = active && source.itemDurabilityRle != null && i < source.itemDurabilityRle.Length ? source.itemDurabilityRle[i] : (byte)0;
            }
        }

        private int EncodeItemDurabilityRle(ref InventoryDTO dto)
        {
            if (dto.itemDurabilityRle == null || dto.itemDurabilityRle.Length < 2)
                return 0;

            int count = math.clamp(dto.cellCount, 0, InventoryDTO.MaxCells);
            if (count <= 0)
                return 0;

            int write = 0;
            byte current = dto.itemDurabilityRle[0];
            int run = 1;
            for (int i = 1; i < count; i++)
            {
                byte next = dto.itemDurabilityRle[i];
                if (next == current && run < byte.MaxValue)
                {
                    run++;
                    continue;
                }

                if (!WriteDurabilityRlePair(dto.itemDurabilityRle, ref write, run, current))
                    return write;

                current = next;
                run = 1;
            }

            WriteDurabilityRlePair(dto.itemDurabilityRle, ref write, run, current);
            for (int i = write; i < dto.itemDurabilityRle.Length; i++)
                dto.itemDurabilityRle[i] = 0;

            return write;
        }

        private static bool WriteDurabilityRlePair(byte[] destination, ref int write, int run, byte quantized)
        {
            if (destination == null || write + 1 >= destination.Length)
                return false;

            destination[write++] = (byte)math.clamp(run, 1, byte.MaxValue);
            destination[write++] = quantized;
            return true;
        }

        private void ApplyLoadedDurability(int anchorIndex, InventoryDTO dto, int dtoIndex)
        {
            if (!_itemDurability.IsCreated || !_durabilities.IsCreated || !_qualityMilli.IsCreated)
                return;

            float durability01 = ResolveLoadedDurability01(dto, dtoIndex, _qualityMilli[anchorIndex]);
            _itemDurability[anchorIndex] = durability01;
            _durabilities[anchorIndex] = (byte)math.clamp((int)math.round(durability01 * 100f), 0, 100);
            _qualityMilli[anchorIndex] = (ushort)math.clamp((int)math.round(durability01 * 1000f), 0, 1000);
        }

        private static float ResolveLoadedDurability01(InventoryDTO dto, int index, ushort fallbackQualityMilli)
        {
            if (dto.itemDurabilityRle == null || dto.itemDurabilityRleLength <= 1 || index < 0)
                return math.saturate((fallbackQualityMilli > 0 ? fallbackQualityMilli : DefaultQualityMilli) * 0.001f);

            int limit = math.min(dto.itemDurabilityRleLength, dto.itemDurabilityRle.Length);
            int decoded = 0;
            for (int cursor = 0; cursor + 1 < limit;)
            {
                int run = dto.itemDurabilityRle[cursor++];
                byte encoded = dto.itemDurabilityRle[cursor++];
                if (run <= 0)
                    continue;

                if (index < decoded + run)
                    return DecodeDurabilitySByte(encoded);

                decoded += run;
            }

            return math.saturate((fallbackQualityMilli > 0 ? fallbackQualityMilli : DefaultQualityMilli) * 0.001f);
        }

        private static byte QuantizeDurabilitySByte(float durability01)
        {
            sbyte quantized = (sbyte)Mathf.Clamp(Mathf.RoundToInt(math.saturate(durability01) * 100f), 0, 100);
            return unchecked((byte)quantized);
        }

        private static float DecodeDurabilitySByte(byte encoded)
        {
            sbyte quantized = unchecked((sbyte)encoded);
            return math.saturate(math.clamp((int)quantized, 0, 100) * 0.01f);
        }

        private void WriteInventoryShadowInt(ref int offset, ref uint hash, int value)
        {
            WriteInventoryShadowUInt(ref offset, ref hash, unchecked((uint)value));
        }

        private void WriteInventoryShadowUShort(ref int offset, ref uint hash, ushort value)
        {
            WriteInventoryShadowByte(ref offset, ref hash, (byte)value);
            WriteInventoryShadowByte(ref offset, ref hash, (byte)(value >> 8));
        }

        private void WriteInventoryShadowUInt(ref int offset, ref uint hash, uint value)
        {
            WriteInventoryShadowByte(ref offset, ref hash, (byte)value);
            WriteInventoryShadowByte(ref offset, ref hash, (byte)(value >> 8));
            WriteInventoryShadowByte(ref offset, ref hash, (byte)(value >> 16));
            WriteInventoryShadowByte(ref offset, ref hash, (byte)(value >> 24));
        }

        private void WriteInventoryShadowByte(ref int offset, ref uint hash, byte value)
        {
            if ((uint)offset >= (uint)_inventoryShadowBuffer.Length)
                return;

            _inventoryShadowBuffer[offset] = value;
            offset++;
            hash ^= value;
            hash *= Fnv1a32Prime;
        }

        public void NotifyMappedInventoryWriteCommitted()
        {
            if (!_hasPendingInventoryCommit)
                return;

            if (_pendingInventorySaveRevision == _inventoryDirtyRevision)
            {
                CopyInventoryDto(ref _lastCommittedInventoryDto, in _pendingInventoryDto);
                _hasCommittedInventoryDto = true;
                _lastCommittedInventoryShadowHash = _pendingInventoryShadowHash;
                _hasCommittedInventoryShadowHash = _inventoryShadowValid;
                _isDirty = false;
            }

            _pendingInventorySaveRevision = 0u;
            _pendingInventoryShadowHash = 0u;
            _hasPendingInventoryCommit = false;
        }

        public void LoadFromSaveData(SaveData data)
        {
            if (data == null || itemCatalog == null || _grid == null)
                return;

            InventoryDTO dto = data.inventory;
            dto.EnsureCapacity();
            _grid.Clear();
            ClearNativeArray(_stackCounts);
            ClearCraftReservationState();
            ClearNativeArray(_itemStateFlags);
            ClearNativeArray(_itemGenetics);
            ClearNativeArray(_qualityMilli);
            ClearNativeArray(_itemDurability);
            ClearNativeArray(_durabilities);
            ClearNativeArray(_lastUpdateUnixSeconds);
            TotalWeight = 0f;

            if (dto.itemHashIds == null ||
                dto.packedCellCoordinates == null ||
                dto.stackCounts == null ||
                dto.cellCount <= 0)
            {
                PopulateInventoryDtoFromRuntime(ref _lastCommittedInventoryDto);
                _hasCommittedInventoryDto = true;
                _hasPendingInventoryCommit = false;
                _isDirty = false;
                CommitCurrentInventoryShadowHash();
                NotifyInventoryChanged(markDirty: false);
                return;
            }

            int count = Mathf.Min(dto.cellCount, dto.itemHashIds.Length, dto.packedCellCoordinates.Length, dto.stackCounts.Length);
            for (int i = 0; i < count; i++)
            {
                int itemHashId = dto.itemHashIds[i];
                if (itemHashId == 0)
                    continue;

                if (!TryBuildDescriptor(itemHashId, out InventoryGrid.InventoryItemDescriptor descriptor) ||
                    !TryGetRuntimeDescriptor(itemHashId, out ItemCatalog.ItemRuntimeDescriptor runtimeDescriptor))
                    continue;

                int cellX = InventoryDTO.UnpackCellX(dto.packedCellCoordinates[i]);
                int cellY = InventoryDTO.UnpackCellY(dto.packedCellCoordinates[i]);
                int loadedCount = dto.stackCounts[i] > 0 ? dto.stackCounts[i] : 1;

                if (_grid.CheckFit(cellX, cellY, descriptor.Width, descriptor.Height))
                {
                    _grid.PlaceAt(in descriptor, cellX, cellY);
                    int anchorIndex = AnchorIndex(cellX, cellY);
                    _stackCounts[anchorIndex] = (ushort)Mathf.Clamp(loadedCount, 1, ushort.MaxValue);
                    _itemStateFlags[anchorIndex] = ResolveLoadedItemStateFlags(dto, i, runtimeDescriptor.StateFlags);
                    _itemGenetics[anchorIndex] = ResolveLoadedGeneticsMask(dto, i);
                    _qualityMilli[anchorIndex] = ResolveLoadedQualityMilli(dto, i);
                    ApplyLoadedDurability(anchorIndex, dto, i);
                    _lastUpdateUnixSeconds[anchorIndex] = ResolveLoadedTimestamp(dto, i);
                    SetAnchorPhysicalMetadata(anchorIndex, runtimeDescriptor.MassKg, runtimeDescriptor.VolumeM3, runtimeDescriptor.RadiationSvPerSecond);
                    ApplyLoadedBiologicalDecay(anchorIndex);
                    TotalWeight += descriptor.Weight * loadedCount;
                    continue;
                }

                if (_grid.TryAddItem(in descriptor, out int px, out int py))
                {
                    int anchorIndex = AnchorIndex(px, py);
                    _stackCounts[anchorIndex] = (ushort)Mathf.Clamp(loadedCount, 1, ushort.MaxValue);
                    _itemStateFlags[anchorIndex] = ResolveLoadedItemStateFlags(dto, i, runtimeDescriptor.StateFlags);
                    _itemGenetics[anchorIndex] = ResolveLoadedGeneticsMask(dto, i);
                    _qualityMilli[anchorIndex] = ResolveLoadedQualityMilli(dto, i);
                    ApplyLoadedDurability(anchorIndex, dto, i);
                    _lastUpdateUnixSeconds[anchorIndex] = ResolveLoadedTimestamp(dto, i);
                    SetAnchorPhysicalMetadata(anchorIndex, runtimeDescriptor.MassKg, runtimeDescriptor.VolumeM3, runtimeDescriptor.RadiationSvPerSecond);
                    ApplyLoadedBiologicalDecay(anchorIndex);
                    TotalWeight += descriptor.Weight * loadedCount;
                }
            }

            PopulateInventoryDtoFromRuntime(ref _lastCommittedInventoryDto);
            _hasCommittedInventoryDto = true;
            _hasPendingInventoryCommit = false;
            _isDirty = false;
            CommitCurrentInventoryShadowHash();
            NotifyInventoryChanged(markDirty: false);
        }

        public void RequestSortInventory()
        {
            int frame = Mathf.Max(0, Time.frameCount);
            SignalBus<InventoryCommandSignal>.Push(new InventoryCommandSignal
            {
                InventoryHash = ResolveInventorySignalHash(),
                Frame = unchecked((uint)frame),
                Sequence = unchecked((uint)InventoryVersion),
                Command = InventoryCommandSignalCommands.Sort,
                Flags = 0
            });
            _lastInventorySortCommandFrame = frame;
            SortInventory();
        }

        public void SortInventory()
        {
            if (HasCraftReservations())
                return;

            int count = PopulateInventoryDefragBuffers();
            if (count <= 0)
                return;

            long startTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            using (_defragProfilerMarker.Auto())
            {
                JobHandle sortHandle = new InventoryDefragJob
                {
                    ItemHashes = _defragItemHashes,
                    ItemCounts = _defragItemCounts,
                    ItemCategories = _defragCategories,
                    MaxStackSizes = _defragMaxStacks,
                    ItemRarities = _defragRarities,
                    ItemWidths = _defragWidths,
                    ItemHeights = _defragHeights,
                    ItemFlags = _defragFlags,
                    ItemStateFlags = _defragStateFlags,
                    ItemGenetics = _defragGenetics,
                    QualityMilli = _defragQualityMilli,
                    Durabilities = _defragDurabilities,
                    LastUpdateUnixSeconds = _defragLastUpdateUnixSeconds,
                    UnitMassKg = _defragUnitMassKg,
                    UnitVolumeM3 = _defragUnitVolumeM3,
                    UnitRadiationSv = _defragUnitRadiationSv,
                    Result = _defragResult,
                    SlotCount = count
                }.Schedule();

                // COLD SYNC JOB: explicit user sort command; no Tick/SlowTick barrier.
                DispatcherJobSwap.TryComplete(ref sortHandle, forceComplete: true);
            }

            int sortedCount = _defragResult.IsCreated && _defragResult.Length > InventoryDefragResultSlots.OccupiedCount
                ? _defragResult[InventoryDefragResultSlots.OccupiedCount]
                : count;
            if (!TryApplyDefraggedNativeStream(sortedCount))
                return;

            _lastDefragTimeMicroseconds = ResolveElapsedMicroseconds(startTimestamp);
            GlobalTelemetryBus.PublishPerformanceWarning(
                _InventoryDefragTimeMsHash,
                _InventoryDefragContextHash,
                _lastDefragTimeMicroseconds * 0.001f);
            PublishInventorySortAcousticSignal();

            NotifyInventoryChanged(massDirty: false);
        }

        private void ConsumeInventoryCommandSignals()
        {
            ReadOnlySpan<InventoryCommandSignal> commands = SignalBus<InventoryCommandSignal>.GetFrameSnapshot();
            uint inventoryHash = ResolveInventorySignalHash();
            for (int index = 0; index < commands.Length; index++)
            {
                InventoryCommandSignal command = commands[index];
                if (command.Command != InventoryCommandSignalCommands.Sort)
                    continue;

                if (command.InventoryHash != 0u && command.InventoryHash != inventoryHash)
                    continue;

                int commandFrame = unchecked((int)command.Frame);
                if (commandFrame <= _lastInventorySortCommandFrame)
                    continue;

                _lastInventorySortCommandFrame = commandFrame;
                SortInventory();
                return;
            }
        }

        private int PopulateInventoryDefragBuffers()
        {
            if (_grid == null ||
                !_stackCounts.IsCreated ||
                !_defragItemHashes.IsCreated ||
                !_defragItemCounts.IsCreated)
            {
                return 0;
            }

            int count = 0;
            int capacity = math.min(_defragItemHashes.Length, _defragItemCounts.Length);
            int sourceCount = math.min(_grid.TotalCells, _stackCounts.Length);
            for (int anchorIndex = 0; anchorIndex < sourceCount && count < capacity; anchorIndex++)
            {
                if (!_grid.TryGetAnchorDescriptor(anchorIndex, out InventoryGrid.InventoryItemDescriptor descriptor))
                    continue;

                int hash = descriptor.HashId;
                ushort stackCount = (ushort)math.max(1, (int)_stackCounts[anchorIndex]);
                if (hash == 0 || stackCount == 0)
                    continue;

                _defragItemHashes[count] = hash;
                _defragItemCounts[count] = stackCount;
                _defragCategories[count] = descriptor.CategoryId;
                _defragMaxStacks[count] = descriptor.MaxStack;
                _defragRarities[count] = descriptor.Rarity;
                _defragWidths[count] = descriptor.Width;
                _defragHeights[count] = descriptor.Height;
                _defragFlags[count] = descriptor.Stackable ? (byte)0x01 : (byte)0x00;
                _defragStateFlags[count] = _itemStateFlags.IsCreated ? _itemStateFlags[anchorIndex] : (ushort)0;
                _defragGenetics[count] = _itemGenetics.IsCreated ? _itemGenetics[anchorIndex] : (byte)0;
                _defragQualityMilli[count] = _qualityMilli.IsCreated && _qualityMilli[anchorIndex] > 0
                    ? _qualityMilli[anchorIndex]
                    : DefaultQualityMilli;
                _defragDurabilities[count] = _durabilities.IsCreated ? _durabilities[anchorIndex] : (byte)100;
                _defragLastUpdateUnixSeconds[count] = _lastUpdateUnixSeconds.IsCreated ? _lastUpdateUnixSeconds[anchorIndex] : 0u;
                _defragUnitMassKg[count] = _anchorUnitMassKg.IsCreated ? _anchorUnitMassKg[anchorIndex] : descriptor.Weight;
                _defragUnitVolumeM3[count] = _anchorUnitVolumeM3.IsCreated ? _anchorUnitVolumeM3[anchorIndex] : 0f;
                _defragUnitRadiationSv[count] = _anchorUnitRadiationSv.IsCreated ? _anchorUnitRadiationSv[anchorIndex] : 0f;
                count++;
            }

            return count;
        }

        private bool TryApplyDefraggedNativeStream(int sortedCount)
        {
            if (_grid == null || sortedCount < 0 || !TryValidateDefragNativePlacement(sortedCount))
                return false;

            _grid.Clear();
            ClearNativeArray(_stackCounts);
            ClearCraftReservationState();
            ClearNativeArray(_itemStateFlags);
            ClearNativeArray(_itemGenetics);
            ClearNativeArray(_qualityMilli);
            ClearNativeArray(_itemDurability);
            ClearNativeArray(_durabilities);
            ClearNativeArray(_lastUpdateUnixSeconds);
            ClearNativeArray(_anchorUnitMassKg);
            ClearNativeArray(_anchorUnitVolumeM3);
            ClearNativeArray(_anchorUnitRadiationSv);
            TotalWeight = 0f;

            for (int index = 0; index < sortedCount; index++)
            {
                if (!TryBuildDefragDescriptor(index, out InventoryGrid.InventoryItemDescriptor descriptor))
                    continue;

                if (!_grid.TryAddItem(in descriptor, out int placedX, out int placedY))
                    return false;

                int anchorIndex = AnchorIndex(placedX, placedY);
                ushort stackCount = (ushort)math.max(1, (int)_defragItemCounts[index]);
                _stackCounts[anchorIndex] = stackCount;
                _itemStateFlags[anchorIndex] = _defragStateFlags[index];
                _itemGenetics[anchorIndex] = SanitizeItemGeneticsFlags(_defragGenetics[index]);
                _qualityMilli[anchorIndex] = _defragQualityMilli[index] > 0 ? _defragQualityMilli[index] : DefaultQualityMilli;
                _durabilities[anchorIndex] = _defragDurabilities[index] > 0
                    ? _defragDurabilities[index]
                    : (byte)math.clamp((_qualityMilli[anchorIndex] + 5) / 10, 0, 100);
                _itemDurability[anchorIndex] = math.saturate(_durabilities[anchorIndex] * 0.01f);
                _lastUpdateUnixSeconds[anchorIndex] = _defragLastUpdateUnixSeconds[index];
                SetAnchorPhysicalMetadata(
                    anchorIndex,
                    math.max(0f, _defragUnitMassKg[index]),
                    math.max(0f, _defragUnitVolumeM3[index]),
                    math.max(0f, _defragUnitRadiationSv[index]));
                TotalWeight += math.max(0f, _defragUnitMassKg[index]) * stackCount;
            }

            RefreshInventorySoAMirrorsAndMask();
            return true;
        }

        private bool TryValidateDefragNativePlacement(int sortedCount)
        {
            if (!_simulationOccupiedCells.IsCreated)
                return false;

            ClearNativeArray(_simulationOccupiedCells);
            for (int index = 0; index < sortedCount; index++)
            {
                if (!TryBuildDefragDescriptor(index, out InventoryGrid.InventoryItemDescriptor descriptor))
                    continue;

                if (!TryReservePlacementInSimulation(in descriptor))
                    return false;
            }

            return true;
        }

        private bool TryBuildDefragDescriptor(int index, out InventoryGrid.InventoryItemDescriptor descriptor)
        {
            descriptor = default;
            if (!_defragItemHashes.IsCreated ||
                !_defragItemCounts.IsCreated ||
                (uint)index >= (uint)_defragItemHashes.Length ||
                _defragItemHashes[index] == 0 ||
                _defragItemCounts[index] == 0)
            {
                return false;
            }

            byte width = (byte)math.max(1, _defragWidths[index]);
            byte height = (byte)math.max(1, _defragHeights[index]);
            ushort maxStack = _defragMaxStacks[index] == 0 ? (ushort)1 : _defragMaxStacks[index];
            descriptor = new InventoryGrid.InventoryItemDescriptor(
                _defragItemHashes[index],
                width,
                height,
                maxStack,
                math.max(0f, _defragUnitMassKg[index]),
                _defragCategories[index],
                _defragRarities[index],
                (_defragFlags[index] & 0x01) != 0);
            return descriptor.IsValid;
        }

        private static int ResolveElapsedMicroseconds(long startTimestamp)
        {
            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp;
            if (elapsedTicks <= 0)
                return 0;

            long microseconds = (elapsedTicks * 1000000L) / System.Diagnostics.Stopwatch.Frequency;
            if (microseconds <= 0L)
                return 0;

            return microseconds >= int.MaxValue ? int.MaxValue : (int)microseconds;
        }

        private void PublishInventorySortAcousticSignal()
        {
            GlobalSignals.Publish(new ToolAcousticSignal
            {
                ToolHash = _InventorySortToolHash,
                TargetHash = _InventoryUiClickHash,
                Progress01 = 1f,
                PitchScale = 1f,
                Intensity01 = 0.55f,
                Frame = (uint)Mathf.Max(0, Time.frameCount),
                State = 1,
                Flags = 0
            });
        }

        internal bool TryMoveOrSwapAnchor(int sourceAnchorX, int sourceAnchorY, int targetCellX, int targetCellY)
        {
            if (_grid == null ||
                !_stackCounts.IsCreated ||
                HasCraftReservations() ||
                (uint)sourceAnchorX >= (uint)_grid.Columns ||
                (uint)sourceAnchorY >= (uint)_grid.Rows ||
                (uint)targetCellX >= (uint)_grid.Columns ||
                (uint)targetCellY >= (uint)_grid.Rows)
            {
                return false;
            }

            int sourceAnchorIndex = _grid.GetCellAnchorIndex(sourceAnchorX, sourceAnchorY);
            if (sourceAnchorIndex < 0)
                return false;

            sourceAnchorX = sourceAnchorIndex % _grid.Columns;
            sourceAnchorY = sourceAnchorIndex / _grid.Columns;

            int targetAnchorIndex = _grid.GetCellAnchorIndex(targetCellX, targetCellY);
            int targetAnchorX = targetAnchorIndex >= 0 ? targetAnchorIndex % _grid.Columns : targetCellX;
            int targetAnchorY = targetAnchorIndex >= 0 ? targetAnchorIndex / _grid.Columns : targetCellY;
            if (sourceAnchorX == targetAnchorX && sourceAnchorY == targetAnchorY)
                return false;

            int destinationAnchorIndex = targetAnchorIndex >= 0
                ? targetAnchorIndex
                : (targetAnchorY * _grid.Columns) + targetAnchorX;
            if (!_grid.TryMoveOrSwapAnchor(sourceAnchorIndex, targetAnchorIndex, targetAnchorX, targetAnchorY))
                return false;

            MoveAnchorState(sourceAnchorIndex, destinationAnchorIndex, targetAnchorIndex >= 0);

            NotifyInventoryChanged(massDirty: false);
            return true;
        }

        public bool TryBulkTransferTo(
            PlayerInventory targetInventory,
            int sourceStartIndex,
            int targetStartIndex,
            int slotCount,
            out InventorySoAUtility.BulkTransferResult result)
        {
            result = InventorySoAUtility.BulkTransferResult.Failed(InventorySoAUtility.TransferFailureCode.InvalidInput);
            if (targetInventory == null ||
                targetInventory == this ||
                !IsValidBulkSlice(sourceStartIndex, slotCount) ||
                !targetInventory.IsValidBulkSlice(targetStartIndex, slotCount))
            {
                return false;
            }

            if (HasCraftReservations() || targetInventory.HasCraftReservations() || HasCraftLocksInSlice(sourceStartIndex, slotCount))
            {
                result = InventorySoAUtility.BulkTransferResult.Failed(InventorySoAUtility.TransferFailureCode.CraftLocked);
                return false;
            }

            PrepareBulkTransferCaches();
            targetInventory.PrepareBulkTransferCaches();

            if (!TryValidateBulkSourceFootprints(sourceStartIndex, slotCount, out bool hasSourceFootprint))
            {
                result = InventorySoAUtility.BulkTransferResult.Failed(
                    hasSourceFootprint
                        ? InventorySoAUtility.TransferFailureCode.PlacementRejected
                        : InventorySoAUtility.TransferFailureCode.SourceEmpty);
                return false;
            }

            if (!TryValidateBulkTransferPlacement(targetInventory, sourceStartIndex, targetStartIndex, slotCount))
            {
                result = InventorySoAUtility.BulkTransferResult.Failed(InventorySoAUtility.TransferFailureCode.PlacementRejected);
                return false;
            }

            if (!TryRunBulkTransferValidation(targetInventory, sourceStartIndex, targetStartIndex, slotCount, out result))
                return false;

            if (!TryPlaceBulkTransferSlice(targetInventory, sourceStartIndex, targetStartIndex, slotCount))
            {
                targetInventory.ClearBulkTransferSlice(targetStartIndex, slotCount);
                result = InventorySoAUtility.BulkTransferResult.Failed(InventorySoAUtility.TransferFailureCode.PlacementRejected);
                return false;
            }

            if (!TryCopyBulkTransferArraysTo(targetInventory, sourceStartIndex, targetStartIndex, slotCount))
            {
                targetInventory.ClearBulkTransferSlice(targetStartIndex, slotCount);
                result = InventorySoAUtility.BulkTransferResult.Failed(InventorySoAUtility.TransferFailureCode.CopyRejected);
                return false;
            }

            targetInventory.SyncBulkTransferPhysicalMetadata(targetStartIndex, slotCount);
            ClearBulkTransferSlice(sourceStartIndex, slotCount);
            TryCompactIdenticalHashesAfterBulkTransfer();
            targetInventory.TryCompactIdenticalHashesAfterBulkTransfer();
            NotifyInventoryChanged();
            targetInventory.NotifyInventoryChanged();
            PublishBulkTransferAudio(result.TransferWeightKg);
            return true;
        }

        public bool TryDropSliceToOcean(
            int sourceStartIndex,
            int slotCount,
            Vector3 runtimePosition,
            out InventorySoAUtility.BulkTransferResult result)
        {
            result = InventorySoAUtility.BulkTransferResult.Failed(InventorySoAUtility.TransferFailureCode.InvalidInput);
            if (!IsValidBulkSlice(sourceStartIndex, slotCount) || !IsFiniteRuntimePosition(runtimePosition))
                return false;

            if (HasCraftLocksInSlice(sourceStartIndex, slotCount))
            {
                result = InventorySoAUtility.BulkTransferResult.Failed(InventorySoAUtility.TransferFailureCode.CraftLocked);
                return false;
            }

            if (!TryValidateBulkSourceFootprints(sourceStartIndex, slotCount, out bool hasSourceFootprint))
            {
                result = InventorySoAUtility.BulkTransferResult.Failed(
                    hasSourceFootprint
                        ? InventorySoAUtility.TransferFailureCode.PlacementRejected
                        : InventorySoAUtility.TransferFailureCode.SourceEmpty);
                return false;
            }

            PrepareBulkTransferCaches();
            AbsoluteUniversePosition dropAup = AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
            int movedSlotCount = 0;
            int movedStackCount = 0;
            float transferWeightKg = 0f;
            float transferVolumeLiters = 0f;
            for (int offset = 0; offset < slotCount; offset++)
            {
                int sourceIndex = sourceStartIndex + offset;
                uint hash = _itemHashes[sourceIndex];
                ushort count = _stackCounts[sourceIndex];
                if (hash == 0u || count == 0)
                    continue;

                movedSlotCount++;
                movedStackCount += count;
                transferWeightKg += math.max(0f, _anchorUnitMassKg[sourceIndex]) * count;
                transferVolumeLiters += math.max(0f, _anchorUnitVolumeM3[sourceIndex]) * VolumeM3ToLiters * count;
                GlobalSignals.Publish(new DebrisSpawnSignal
                {
                    PositionAup = dropAup,
                    SpeciesHash = hash,
                    SourceEntityId = 0u,
                    Intensity01 = math.saturate(count * 0.02f),
                    DebrisKind = 4,
                    Flags = 0,
                    Quantity = count
                });
            }

            if (movedSlotCount == 0)
            {
                result = InventorySoAUtility.BulkTransferResult.Failed(InventorySoAUtility.TransferFailureCode.SourceEmpty);
                return false;
            }

            ClearBulkTransferSlice(sourceStartIndex, slotCount);
            TryCompactIdenticalHashesAfterBulkTransfer();
            NotifyInventoryChanged();
            result = new InventorySoAUtility.BulkTransferResult(
                InventorySoAUtility.TransferFailureCode.None,
                movedSlotCount,
                movedStackCount,
                transferWeightKg,
                transferVolumeLiters,
                _currentWeightKg,
                _currentVolumeLiters);
            PublishBulkTransferAudio(transferWeightKg);
            return true;
        }

        public bool TryCopyInventoryShadowPayload(NativeArray<byte> destination, out int payloadLength, out uint payloadHash)
        {
            if (_isDirty || !_inventoryShadowValid)
                RefreshInventoryShadowBufferFromRuntime();

            payloadLength = _inventoryShadowPayloadLength;
            payloadHash = _inventoryShadowHash;
            if (!_inventoryShadowBuffer.IsCreated ||
                !destination.IsCreated ||
                payloadLength <= 0 ||
                payloadLength > destination.Length)
            {
                return false;
            }

            return InventorySoAUtility.TryBulkCopySlice(_inventoryShadowBuffer, 0, destination, 0, payloadLength);
        }

        private void PrepareBulkTransferCaches()
        {
            CompleteInventoryMassRecomputeJob(forceComplete: true);
            RefreshInventorySoAMirrorsAndMask();
            MarkMassCacheDirty();
            RefreshDerivedMassAndSurvivalLoad();
        }

        private bool IsValidBulkSlice(int startIndex, int slotCount)
        {
            return _grid != null &&
                   _itemHashes.IsCreated &&
                   _stackCounts.IsCreated &&
                   startIndex >= 0 &&
                   slotCount > 0 &&
                   startIndex <= int.MaxValue - slotCount &&
                   startIndex + slotCount <= _itemHashes.Length &&
                   startIndex + slotCount <= _stackCounts.Length;
        }

        private bool HasCraftLocksInSlice(int startIndex, int slotCount)
        {
            if (!_craftLockedCounts.IsCreated || !_anchorStateFlags.IsCreated)
                return false;

            if (startIndex < 0 || slotCount <= 0 || startIndex > int.MaxValue - slotCount || startIndex + slotCount > _craftLockedCounts.Length)
                return true;

            for (int index = startIndex; index < startIndex + slotCount; index++)
            {
                if (_craftLockedCounts[index] > 0 || IsCraftLockedFlagSet(index))
                    return true;
            }

            return false;
        }

        private bool TryValidateBulkSourceFootprints(int startIndex, int slotCount, out bool hasSource)
        {
            hasSource = false;
            if (_grid == null || !_itemHashes.IsCreated || !_stackCounts.IsCreated)
                return false;

            if (!IsOccupiedCellRangeSelfContained(startIndex, slotCount, out bool hasOccupiedCell))
            {
                hasSource = hasOccupiedCell;
                return false;
            }

            int end = startIndex + slotCount;
            for (int index = startIndex; index < end; index++)
            {
                uint hash = _itemHashes[index];
                ushort count = _stackCounts[index];
                if (hash == 0u || count == 0)
                    continue;

                hasSource = true;
                if (!_grid.TryGetAnchorDescriptor(index, out InventoryGrid.InventoryItemDescriptor descriptor) ||
                    descriptor.HashId != unchecked((int)hash) ||
                    !IsAnchorFootprintContainedInSlice(index, descriptor.Width, descriptor.Height, startIndex, slotCount))
                {
                    return false;
                }
            }

            return hasSource;
        }

        private bool IsOccupiedCellRangeSelfContained(int startIndex, int slotCount, out bool hasOccupiedCell)
        {
            hasOccupiedCell = false;
            int end = startIndex + slotCount;
            for (int index = startIndex; index < end; index++)
            {
                if (!TryDecodeAnchorIndex(index, out int x, out int y))
                    return false;

                int anchorIndex = _grid.GetCellAnchorIndex(x, y);
                if (anchorIndex >= 0)
                {
                    hasOccupiedCell = true;
                    if (anchorIndex < startIndex || anchorIndex >= end)
                        return false;
                }
            }

            return true;
        }

        private bool IsBulkTargetSliceClear(int startIndex, int slotCount)
        {
            if (_grid == null || !_itemHashes.IsCreated || !_stackCounts.IsCreated)
                return false;

            int end = startIndex + slotCount;
            for (int index = startIndex; index < end; index++)
            {
                if (_itemHashes[index] != 0u || _stackCounts[index] != 0)
                    return false;

                if (!TryDecodeAnchorIndex(index, out int x, out int y) ||
                    _grid.GetCellAnchorIndex(x, y) >= 0)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsAnchorFootprintContainedInSlice(int anchorIndex, int width, int height, int sliceStartIndex, int slotCount)
        {
            if (!TryDecodeAnchorIndex(anchorIndex, out int anchorX, out int anchorY) ||
                sliceStartIndex < 0 ||
                slotCount <= 0 ||
                sliceStartIndex > int.MaxValue - slotCount ||
                width <= 0 ||
                height <= 0 ||
                anchorX + width > _grid.Columns ||
                anchorY + height > _grid.Rows)
            {
                return false;
            }

            int sliceEnd = sliceStartIndex + slotCount;
            for (int y = anchorY; y < anchorY + height; y++)
            {
                for (int x = anchorX; x < anchorX + width; x++)
                {
                    int cellIndex = AnchorIndex(x, y);
                    if (cellIndex < sliceStartIndex || cellIndex >= sliceEnd)
                        return false;
                }
            }

            return true;
        }

        private bool TryValidateBulkTransferPlacement(
            PlayerInventory targetInventory,
            int sourceStartIndex,
            int targetStartIndex,
            int slotCount)
        {
            if (targetInventory == null || targetInventory._grid == null)
                return false;

            if (!targetInventory.IsBulkTargetSliceClear(targetStartIndex, slotCount))
                return false;

            bool hasSource = false;
            for (int offset = 0; offset < slotCount; offset++)
            {
                int sourceIndex = sourceStartIndex + offset;
                uint hash = _itemHashes[sourceIndex];
                ushort count = _stackCounts[sourceIndex];
                if (hash == 0u || count == 0)
                    continue;

                hasSource = true;
                int targetIndex = targetStartIndex + offset;
                if (!_grid.TryGetAnchorDescriptor(sourceIndex, out InventoryGrid.InventoryItemDescriptor sourceDescriptor) ||
                    sourceDescriptor.HashId != unchecked((int)hash) ||
                    !IsAnchorFootprintContainedInSlice(sourceIndex, sourceDescriptor.Width, sourceDescriptor.Height, sourceStartIndex, slotCount) ||
                    !targetInventory.TryDecodeAnchorIndex(targetIndex, out int targetX, out int targetY) ||
                    !targetInventory.IsAnchorFootprintContainedInSlice(targetIndex, sourceDescriptor.Width, sourceDescriptor.Height, targetStartIndex, slotCount) ||
                    !targetInventory._grid.CheckFit(targetX, targetY, sourceDescriptor.Width, sourceDescriptor.Height))
                {
                    return false;
                }
            }

            return hasSource;
        }

        private bool TryRunBulkTransferValidation(
            PlayerInventory targetInventory,
            int sourceStartIndex,
            int targetStartIndex,
            int slotCount,
            out InventorySoAUtility.BulkTransferResult result)
        {
            result = InventorySoAUtility.BulkTransferResult.Failed(InventorySoAUtility.TransferFailureCode.InvalidInput);
            NativeArray<float4> validationResult = default;
            NativeArray<byte> failureCode = default;
            try
            {
                validationResult = new NativeArray<float4>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                failureCode = new NativeArray<byte>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                RegisterTempJobArray(validationResult, BulkTransferValidationTempLabel);
                RegisterTempJobArray(failureCode, BulkTransferFailureTempLabel);

                JobHandle validationHandle = new InventorySoAUtility.InventoryTransferValidationJob
                {
                    SourceHashes = _itemHashes,
                    SourceCounts = _stackCounts,
                    SourceUnitMassKg = _anchorUnitMassKg,
                    SourceUnitVolumeM3 = _anchorUnitVolumeM3,
                    TargetHashes = targetInventory._itemHashes,
                    TargetCounts = targetInventory._stackCounts,
                    Result = validationResult,
                    FailureCode = failureCode,
                    SourceStartIndex = sourceStartIndex,
                    TargetStartIndex = targetStartIndex,
                    SlotCount = slotCount,
                    TargetCurrentWeightKg = targetInventory._currentWeightKg,
                    TargetCurrentVolumeLiters = targetInventory._currentVolumeLiters,
                    TargetMaxWeightKg = targetInventory.MaxWeightKg,
                    TargetMaxVolumeLiters = targetInventory.MaxVolumeLiters
                }.Schedule();

                // COLD SYNC JOB: explicit inventory bulk transfer command; no Tick/SlowTick barrier.
                DispatcherJobSwap.TryComplete(ref validationHandle, forceComplete: true);

                float4 totals = validationResult[0];
                InventorySoAUtility.TransferFailureCode resolvedFailureCode = (InventorySoAUtility.TransferFailureCode)failureCode[0];
                result = new InventorySoAUtility.BulkTransferResult(
                    resolvedFailureCode,
                    (int)totals.w,
                    (int)totals.z,
                    totals.x,
                    totals.y,
                    targetInventory._currentWeightKg + totals.x,
                    targetInventory._currentVolumeLiters + totals.y);
                return result.Succeeded;
            }
            finally
            {
                DisposeTempJobArray(ref failureCode);
                DisposeTempJobArray(ref validationResult);
            }
        }

        private bool TryPlaceBulkTransferSlice(
            PlayerInventory targetInventory,
            int sourceStartIndex,
            int targetStartIndex,
            int slotCount)
        {
            for (int offset = 0; offset < slotCount; offset++)
            {
                int sourceIndex = sourceStartIndex + offset;
                uint hash = _itemHashes[sourceIndex];
                ushort count = _stackCounts[sourceIndex];
                if (hash == 0u || count == 0)
                    continue;

                int targetIndex = targetStartIndex + offset;
                if (!_grid.TryGetAnchorDescriptor(sourceIndex, out InventoryGrid.InventoryItemDescriptor descriptor) ||
                    descriptor.HashId != unchecked((int)hash) ||
                    !IsAnchorFootprintContainedInSlice(sourceIndex, descriptor.Width, descriptor.Height, sourceStartIndex, slotCount) ||
                    !targetInventory.TryDecodeAnchorIndex(targetIndex, out int targetX, out int targetY) ||
                    !targetInventory.IsAnchorFootprintContainedInSlice(targetIndex, descriptor.Width, descriptor.Height, targetStartIndex, slotCount) ||
                    !targetInventory._grid.PlaceAt(in descriptor, targetX, targetY))
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryCopyBulkTransferArraysTo(
            PlayerInventory targetInventory,
            int sourceStartIndex,
            int targetStartIndex,
            int slotCount)
        {
            return InventorySoAUtility.TryBulkCopySlice(_itemHashes, sourceStartIndex, targetInventory._itemHashes, targetStartIndex, slotCount) &&
                   InventorySoAUtility.TryBulkCopySlice(_stackCounts, sourceStartIndex, targetInventory._stackCounts, targetStartIndex, slotCount) &&
                   InventorySoAUtility.TryBulkCopySlice(_itemCondition, sourceStartIndex, targetInventory._itemCondition, targetStartIndex, slotCount) &&
                   InventorySoAUtility.TryBulkCopySlice(_itemStateFlags, sourceStartIndex, targetInventory._itemStateFlags, targetStartIndex, slotCount) &&
                   InventorySoAUtility.TryBulkCopySlice(_itemGenetics, sourceStartIndex, targetInventory._itemGenetics, targetStartIndex, slotCount) &&
                   InventorySoAUtility.TryBulkCopySlice(_qualityMilli, sourceStartIndex, targetInventory._qualityMilli, targetStartIndex, slotCount) &&
                   InventorySoAUtility.TryBulkCopySlice(_durabilities, sourceStartIndex, targetInventory._durabilities, targetStartIndex, slotCount) &&
                   InventorySoAUtility.TryBulkCopySlice(_lastUpdateUnixSeconds, sourceStartIndex, targetInventory._lastUpdateUnixSeconds, targetStartIndex, slotCount) &&
                   InventorySoAUtility.TryBulkCopySlice(_anchorUnitMassKg, sourceStartIndex, targetInventory._anchorUnitMassKg, targetStartIndex, slotCount) &&
                   InventorySoAUtility.TryBulkCopySlice(_anchorUnitVolumeM3, sourceStartIndex, targetInventory._anchorUnitVolumeM3, targetStartIndex, slotCount) &&
                   InventorySoAUtility.TryBulkCopySlice(_anchorUnitRadiationSv, sourceStartIndex, targetInventory._anchorUnitRadiationSv, targetStartIndex, slotCount);
        }

        private void SyncBulkTransferPhysicalMetadata(int startIndex, int slotCount)
        {
            int end = startIndex + slotCount;
            for (int index = startIndex; index < end && (uint)index < (uint)_itemHashes.Length; index++)
            {
                uint hash = _itemHashes[index];
                if (hash == 0u)
                    continue;

                SyncAnchorPhysicalMetadata(index, unchecked((int)hash));
            }
        }

        private void ClearBulkTransferSlice(int startIndex, int slotCount)
        {
            if (_grid != null)
            {
                int end = startIndex + slotCount;
                for (int index = startIndex; index < end; index++)
                {
                    if ((uint)index < (uint)_stackCounts.Length && _grid.HasAnchor(index))
                        _grid.RemoveAnchorAt(index);
                }
            }

            InventorySoAUtility.TryClearSlice(_itemHashes, startIndex, slotCount);
            InventorySoAUtility.TryClearSlice(_stackCounts, startIndex, slotCount);
            InventorySoAUtility.TryClearSlice(_itemCondition, startIndex, slotCount);
            InventorySoAUtility.TryClearSlice(_craftLockedCounts, startIndex, slotCount);
            InventorySoAUtility.TryClearSlice(_anchorStateFlags, startIndex, slotCount);
            InventorySoAUtility.TryClearSlice(_itemStateFlags, startIndex, slotCount);
            InventorySoAUtility.TryClearSlice(_itemGenetics, startIndex, slotCount);
            InventorySoAUtility.TryClearSlice(_qualityMilli, startIndex, slotCount);
            InventorySoAUtility.TryClearSlice(_durabilities, startIndex, slotCount);
            InventorySoAUtility.TryClearSlice(_lastUpdateUnixSeconds, startIndex, slotCount);
            InventorySoAUtility.TryClearSlice(_anchorUnitMassKg, startIndex, slotCount);
            InventorySoAUtility.TryClearSlice(_anchorUnitVolumeM3, startIndex, slotCount);
            InventorySoAUtility.TryClearSlice(_anchorUnitRadiationSv, startIndex, slotCount);
        }

        private bool TryCompactIdenticalHashesAfterBulkTransfer()
        {
            if (_grid == null ||
                !_itemHashes.IsCreated ||
                !_stackCounts.IsCreated ||
                !_itemCondition.IsCreated ||
                !_itemStateFlags.IsCreated ||
                !_itemGenetics.IsCreated ||
                !_qualityMilli.IsCreated ||
                !_durabilities.IsCreated ||
                !_lastUpdateUnixSeconds.IsCreated ||
                !_anchorUnitMassKg.IsCreated ||
                !_anchorUnitVolumeM3.IsCreated ||
                !_anchorUnitRadiationSv.IsCreated)
            {
                return false;
            }

            int compactionCapacity = ResolveBulkCompactionCapacity();
            if (compactionCapacity <= 0)
                return false;

            NativeArray<uint> itemHashes = default;
            NativeArray<ushort> itemCounts = default;
            NativeArray<float> itemCondition = default;
            NativeArray<ushort> itemStateFlags = default;
            NativeArray<byte> itemGenetics = default;
            NativeArray<ushort> qualityMilli = default;
            NativeArray<byte> durabilities = default;
            NativeArray<uint> lastUpdateUnixSeconds = default;
            NativeArray<float> unitMassKg = default;
            NativeArray<float> unitVolumeM3 = default;
            NativeArray<float> unitRadiationSv = default;
            NativeArray<int> resultCount = default;
            try
            {
                itemHashes = new NativeArray<uint>(compactionCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                itemCounts = new NativeArray<ushort>(compactionCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                itemCondition = new NativeArray<float>(compactionCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                itemStateFlags = new NativeArray<ushort>(compactionCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                itemGenetics = new NativeArray<byte>(compactionCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                qualityMilli = new NativeArray<ushort>(compactionCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                durabilities = new NativeArray<byte>(compactionCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                lastUpdateUnixSeconds = new NativeArray<uint>(compactionCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                unitMassKg = new NativeArray<float>(compactionCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                unitVolumeM3 = new NativeArray<float>(compactionCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                unitRadiationSv = new NativeArray<float>(compactionCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                resultCount = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                RegisterTempJobArray(itemHashes, BulkTransferCompactionHashTempLabel);
                RegisterTempJobArray(itemCounts, BulkTransferCompactionCountTempLabel);
                RegisterTempJobArray(itemCondition, BulkTransferCompactionConditionTempLabel);
                RegisterTempJobArray(itemStateFlags, BulkTransferCompactionStateTempLabel);
                RegisterTempJobArray(itemGenetics, BulkTransferCompactionGeneticsTempLabel);
                RegisterTempJobArray(qualityMilli, BulkTransferCompactionQualityTempLabel);
                RegisterTempJobArray(durabilities, BulkTransferCompactionDurabilityTempLabel);
                RegisterTempJobArray(lastUpdateUnixSeconds, BulkTransferCompactionTimestampTempLabel);
                RegisterTempJobArray(unitMassKg, BulkTransferCompactionMassTempLabel);
                RegisterTempJobArray(unitVolumeM3, BulkTransferCompactionVolumeTempLabel);
                RegisterTempJobArray(unitRadiationSv, BulkTransferCompactionRadiationTempLabel);
                RegisterTempJobArray(resultCount, BulkTransferCompactionResultTempLabel);

                if (!InventorySoAUtility.TryBulkCopySlice(_itemHashes, 0, itemHashes, 0, compactionCapacity) ||
                    !InventorySoAUtility.TryBulkCopySlice(_stackCounts, 0, itemCounts, 0, compactionCapacity) ||
                    !InventorySoAUtility.TryBulkCopySlice(_itemCondition, 0, itemCondition, 0, compactionCapacity) ||
                    !InventorySoAUtility.TryBulkCopySlice(_itemStateFlags, 0, itemStateFlags, 0, compactionCapacity) ||
                    !InventorySoAUtility.TryBulkCopySlice(_itemGenetics, 0, itemGenetics, 0, compactionCapacity) ||
                    !InventorySoAUtility.TryBulkCopySlice(_qualityMilli, 0, qualityMilli, 0, compactionCapacity) ||
                    !InventorySoAUtility.TryBulkCopySlice(_durabilities, 0, durabilities, 0, compactionCapacity) ||
                    !InventorySoAUtility.TryBulkCopySlice(_lastUpdateUnixSeconds, 0, lastUpdateUnixSeconds, 0, compactionCapacity) ||
                    !InventorySoAUtility.TryBulkCopySlice(_anchorUnitMassKg, 0, unitMassKg, 0, compactionCapacity) ||
                    !InventorySoAUtility.TryBulkCopySlice(_anchorUnitVolumeM3, 0, unitVolumeM3, 0, compactionCapacity) ||
                    !InventorySoAUtility.TryBulkCopySlice(_anchorUnitRadiationSv, 0, unitRadiationSv, 0, compactionCapacity))
                {
                    return false;
                }

                JobHandle compactionHandle = new InventorySoAUtility.InventoryCompactionJob
                {
                    ItemHashes = itemHashes,
                    ItemCounts = itemCounts,
                    ItemCondition = itemCondition,
                    ItemStateFlags = itemStateFlags,
                    ItemGenetics = itemGenetics,
                    QualityMilli = qualityMilli,
                    Durabilities = durabilities,
                    LastUpdateUnixSeconds = lastUpdateUnixSeconds,
                    UnitMassKg = unitMassKg,
                    UnitVolumeM3 = unitVolumeM3,
                    UnitRadiationSv = unitRadiationSv,
                    MaxStackCounts = _grid.AnchorMaxStacks,
                    ResultCount = resultCount
                }.Schedule();

                // COLD SYNC JOB: command-path inventory compaction after bulk transfer; no Tick/SlowTick barrier.
                DispatcherJobSwap.TryComplete(ref compactionHandle, forceComplete: true);
                return TryRepackCompactedSoA(
                    itemHashes,
                    itemCounts,
                    itemCondition,
                    itemStateFlags,
                    itemGenetics,
                    qualityMilli,
                    durabilities,
                    lastUpdateUnixSeconds,
                    unitMassKg,
                    unitVolumeM3,
                    unitRadiationSv,
                    resultCount[0]);
            }
            finally
            {
                DisposeTempJobArray(ref resultCount);
                DisposeTempJobArray(ref unitRadiationSv);
                DisposeTempJobArray(ref unitVolumeM3);
                DisposeTempJobArray(ref unitMassKg);
                DisposeTempJobArray(ref lastUpdateUnixSeconds);
                DisposeTempJobArray(ref durabilities);
                DisposeTempJobArray(ref qualityMilli);
                DisposeTempJobArray(ref itemGenetics);
                DisposeTempJobArray(ref itemStateFlags);
                DisposeTempJobArray(ref itemCondition);
                DisposeTempJobArray(ref itemCounts);
                DisposeTempJobArray(ref itemHashes);
            }
        }

        private int ResolveBulkCompactionCapacity()
        {
            int count = _itemHashes.Length;
            count = math.min(count, _stackCounts.Length);
            count = math.min(count, _itemCondition.Length);
            count = math.min(count, _itemStateFlags.Length);
            count = math.min(count, _itemGenetics.Length);
            count = math.min(count, _qualityMilli.Length);
            count = math.min(count, _durabilities.Length);
            count = math.min(count, _lastUpdateUnixSeconds.Length);
            count = math.min(count, _anchorUnitMassKg.Length);
            count = math.min(count, _anchorUnitVolumeM3.Length);
            return math.min(count, _anchorUnitRadiationSv.Length);
        }

        private bool TryRepackCompactedSoA(
            NativeArray<uint> itemHashes,
            NativeArray<ushort> itemCounts,
            NativeArray<float> itemCondition,
            NativeArray<ushort> itemStateFlags,
            NativeArray<byte> itemGenetics,
            NativeArray<ushort> qualityMilli,
            NativeArray<byte> durabilities,
            NativeArray<uint> lastUpdateUnixSeconds,
            NativeArray<float> unitMassKg,
            NativeArray<float> unitVolumeM3,
            NativeArray<float> unitRadiationSv,
            int compactedCount)
        {
            if (!TryBuildCompactedPlacements(
                    itemHashes,
                    itemCounts,
                    itemCondition,
                    itemStateFlags,
                    itemGenetics,
                    qualityMilli,
                    durabilities,
                    lastUpdateUnixSeconds,
                    unitMassKg,
                    unitVolumeM3,
                    unitRadiationSv,
                    compactedCount,
                    out int placementCount))
            {
                return false;
            }

            if (!CanApplyPlacementsFirstFit(_sortBuffer, placementCount))
                return false;

            return TryApplyPlacementsFirstFit(_sortBuffer, placementCount);
        }

        private bool TryBuildCompactedPlacements(
            NativeArray<uint> itemHashes,
            NativeArray<ushort> itemCounts,
            NativeArray<float> itemCondition,
            NativeArray<ushort> itemStateFlags,
            NativeArray<byte> itemGenetics,
            NativeArray<ushort> qualityMilli,
            NativeArray<byte> durabilities,
            NativeArray<uint> lastUpdateUnixSeconds,
            NativeArray<float> unitMassKg,
            NativeArray<float> unitVolumeM3,
            NativeArray<float> unitRadiationSv,
            int compactedCount,
            out int placementCount)
        {
            placementCount = 0;
            if (_sortBuffer == null ||
                compactedCount < 0 ||
                !itemHashes.IsCreated ||
                !itemCounts.IsCreated ||
                !itemCondition.IsCreated ||
                !itemStateFlags.IsCreated ||
                !itemGenetics.IsCreated ||
                !qualityMilli.IsCreated ||
                !durabilities.IsCreated ||
                !lastUpdateUnixSeconds.IsCreated ||
                !unitMassKg.IsCreated ||
                !unitVolumeM3.IsCreated ||
                !unitRadiationSv.IsCreated)
            {
                return false;
            }

            int count = math.min(compactedCount, itemHashes.Length);
            count = math.min(count, itemCounts.Length);
            count = math.min(count, itemCondition.Length);
            count = math.min(count, itemStateFlags.Length);
            count = math.min(count, itemGenetics.Length);
            count = math.min(count, qualityMilli.Length);
            count = math.min(count, durabilities.Length);
            count = math.min(count, lastUpdateUnixSeconds.Length);
            count = math.min(count, unitMassKg.Length);
            count = math.min(count, unitVolumeM3.Length);
            count = math.min(count, unitRadiationSv.Length);
            for (int index = 0; index < count && placementCount < _sortBuffer.Length; index++)
            {
                uint hash = itemHashes[index];
                ushort stackCount = itemCounts[index];
                if (hash == 0u || stackCount == 0)
                    continue;

                if (!TryBuildDescriptor(unchecked((int)hash), out InventoryGrid.InventoryItemDescriptor descriptor))
                    return false;

                _sortBuffer[placementCount++] = new ItemPlacement
                {
                    itemHashId = descriptor.HashId,
                    x = 0,
                    y = 0,
                    width = descriptor.Width,
                    height = descriptor.Height,
                    maxStack = descriptor.MaxStack,
                    stackCount = stackCount,
                    lockedCount = 0,
                    stateFlags = itemStateFlags[index],
                    geneticsMask = itemGenetics[index],
                    qualityMilli = qualityMilli[index] > 0 ? qualityMilli[index] : DefaultQualityMilli,
                    durability = durabilities[index],
                    lastUpdateUnixSeconds = lastUpdateUnixSeconds[index],
                    weight = math.max(0f, unitMassKg[index]),
                    unitVolumeM3 = math.max(0f, unitVolumeM3[index]),
                    unitRadiationSv = math.max(0f, unitRadiationSv[index]),
                    categoryId = descriptor.CategoryId,
                    rarity = descriptor.Rarity,
                    stackable = descriptor.Stackable
                };
            }

            return true;
        }

        private bool CanApplyPlacementsFirstFit(ItemPlacement[] placements, int placementCount)
        {
            if (_grid == null ||
                placements == null ||
                !_simulationOccupiedCells.IsCreated ||
                placementCount < 0 ||
                placementCount > placements.Length)
            {
                return false;
            }

            ClearNativeArray(_simulationOccupiedCells);
            for (int placementIndex = 0; placementIndex < placementCount; placementIndex++)
            {
                InventoryGrid.InventoryItemDescriptor descriptor = placements[placementIndex].Descriptor;
                if (!descriptor.IsValid || !TryReservePlacementInSimulation(in descriptor))
                    return false;
            }

            return true;
        }

        private bool TryApplyPlacementsFirstFit(ItemPlacement[] placements, int placementCount)
        {
            if (_grid == null || placements == null || !_stackCounts.IsCreated)
                return false;

            _grid.Clear();
            ClearNativeArray(_stackCounts);
            ClearNativeArray(_craftLockedCounts);
            ClearNativeArray(_anchorStateFlags);
            ClearNativeArray(_itemStateFlags);
            ClearNativeArray(_itemGenetics);
            ClearNativeArray(_qualityMilli);
            ClearNativeArray(_itemDurability);
            ClearNativeArray(_durabilities);
            ClearNativeArray(_lastUpdateUnixSeconds);
            ClearNativeArray(_anchorUnitMassKg);
            ClearNativeArray(_anchorUnitVolumeM3);
            ClearNativeArray(_anchorUnitRadiationSv);
            TotalWeight = 0f;

            for (int placementIndex = 0; placementIndex < placementCount; placementIndex++)
            {
                ItemPlacement placement = placements[placementIndex];
                InventoryGrid.InventoryItemDescriptor descriptor = placement.Descriptor;
                if (!descriptor.IsValid || !_grid.TryAddItem(in descriptor, out int placedX, out int placedY))
                    return false;

                int anchorIndex = AnchorIndex(placedX, placedY);
                _stackCounts[anchorIndex] = (ushort)math.max(1, placement.stackCount);
                _itemStateFlags[anchorIndex] = placement.stateFlags;
                _itemGenetics[anchorIndex] = SanitizeItemGeneticsFlags(placement.geneticsMask);
                _qualityMilli[anchorIndex] = placement.qualityMilli > 0 ? placement.qualityMilli : DefaultQualityMilli;
                _durabilities[anchorIndex] = placement.durability > 0
                    ? placement.durability
                    : (byte)math.clamp((_qualityMilli[anchorIndex] + 5) / 10, 0, 100);
                _itemDurability[anchorIndex] = math.saturate(_durabilities[anchorIndex] * 0.01f);
                _lastUpdateUnixSeconds[anchorIndex] = placement.lastUpdateUnixSeconds;
                if (placement.weight > 0f || placement.unitVolumeM3 > 0f || placement.unitRadiationSv > 0f)
                    SetAnchorPhysicalMetadata(anchorIndex, placement.weight, placement.unitVolumeM3, placement.unitRadiationSv);
                else
                    SyncAnchorPhysicalMetadata(anchorIndex, placement.itemHashId);
                TotalWeight += _anchorUnitMassKg[anchorIndex] * math.max(1, placement.stackCount);
            }

            RefreshInventorySoAMirrorsAndMask();
            return true;
        }

        private bool TryDecodeAnchorIndex(int anchorIndex, out int x, out int y)
        {
            x = 0;
            y = 0;
            if (_grid == null || anchorIndex < 0 || anchorIndex >= _grid.TotalCells)
                return false;

            x = anchorIndex % _grid.Columns;
            y = anchorIndex / _grid.Columns;
            return true;
        }

        private static bool IsFiniteRuntimePosition(Vector3 runtimePosition)
        {
            return math.all(math.isfinite(new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z)));
        }

        private void PublishBulkTransferAudio(float transferWeightKg)
        {
            if (transferWeightKg < HeavyBulkTransferAudioThresholdKg)
                return;

            float inverseTransferWeight = math.rcp(math.max(HeavyBulkTransferAudioThresholdKg, transferWeightKg));
            GlobalSignals.Publish(new ToolAcousticSignal
            {
                ToolHash = _InventoryBulkTransferToolHash,
                TargetHash = _HeavyThudTargetHash,
                Progress01 = 1f,
                PitchScale = math.lerp(0.65f, 0.95f, math.saturate(HeavyBulkTransferAudioThresholdKg * inverseTransferWeight)),
                Intensity01 = math.saturate(transferWeightKg * 0.01f),
                Frame = (uint)Mathf.Max(0, Time.frameCount),
                State = 2,
                Flags = 0
            });
        }

        private void MoveAnchorState(int sourceAnchorIndex, int destinationAnchorIndex, bool swappedWithExistingAnchor)
        {
            if (swappedWithExistingAnchor)
            {
                SwapAnchorState(_stackCounts, sourceAnchorIndex, destinationAnchorIndex);
                SwapAnchorState(_craftLockedCounts, sourceAnchorIndex, destinationAnchorIndex);
                SwapAnchorState(_anchorStateFlags, sourceAnchorIndex, destinationAnchorIndex);
                SwapAnchorState(_itemStateFlags, sourceAnchorIndex, destinationAnchorIndex);
                SwapAnchorState(_itemGenetics, sourceAnchorIndex, destinationAnchorIndex);
                SwapAnchorState(_qualityMilli, sourceAnchorIndex, destinationAnchorIndex);
                SwapAnchorState(_itemDurability, sourceAnchorIndex, destinationAnchorIndex);
                SwapAnchorState(_durabilities, sourceAnchorIndex, destinationAnchorIndex);
                SwapAnchorState(_lastUpdateUnixSeconds, sourceAnchorIndex, destinationAnchorIndex);
                SwapAnchorState(_anchorUnitMassKg, sourceAnchorIndex, destinationAnchorIndex);
                SwapAnchorState(_anchorUnitVolumeM3, sourceAnchorIndex, destinationAnchorIndex);
                SwapAnchorState(_anchorUnitRadiationSv, sourceAnchorIndex, destinationAnchorIndex);
                return;
            }

            MoveAnchorStateValue(_stackCounts, sourceAnchorIndex, destinationAnchorIndex);
            MoveAnchorStateValue(_craftLockedCounts, sourceAnchorIndex, destinationAnchorIndex);
            MoveAnchorStateValue(_anchorStateFlags, sourceAnchorIndex, destinationAnchorIndex);
            MoveAnchorStateValue(_itemStateFlags, sourceAnchorIndex, destinationAnchorIndex);
            MoveAnchorStateValue(_itemGenetics, sourceAnchorIndex, destinationAnchorIndex);
            MoveAnchorStateValue(_qualityMilli, sourceAnchorIndex, destinationAnchorIndex);
            MoveAnchorStateValue(_itemDurability, sourceAnchorIndex, destinationAnchorIndex);
            MoveAnchorStateValue(_durabilities, sourceAnchorIndex, destinationAnchorIndex);
            MoveAnchorStateValue(_lastUpdateUnixSeconds, sourceAnchorIndex, destinationAnchorIndex);
            MoveAnchorStateValue(_anchorUnitMassKg, sourceAnchorIndex, destinationAnchorIndex);
            MoveAnchorStateValue(_anchorUnitVolumeM3, sourceAnchorIndex, destinationAnchorIndex);
            MoveAnchorStateValue(_anchorUnitRadiationSv, sourceAnchorIndex, destinationAnchorIndex);
        }

        private static void SwapAnchorState<T>(NativeArray<T> values, int firstIndex, int secondIndex) where T : struct
        {
            if (!values.IsCreated || firstIndex == secondIndex)
                return;

            T temp = values[firstIndex];
            values[firstIndex] = values[secondIndex];
            values[secondIndex] = temp;
        }

        private static void MoveAnchorStateValue<T>(NativeArray<T> values, int sourceIndex, int destinationIndex) where T : struct
        {
            if (!values.IsCreated || sourceIndex == destinationIndex)
                return;

            values[destinationIndex] = values[sourceIndex];
            values[sourceIndex] = default;
        }

        public int GetPlacements(ItemPlacement[] buffer)
        {
            if (buffer == null || _grid == null || !_stackCounts.IsCreated)
                return 0;

            int count = 0;
            for (int anchorIndex = 0; anchorIndex < _stackCounts.Length && count < buffer.Length; anchorIndex++)
            {
                if (!_grid.TryGetAnchorDescriptor(anchorIndex, out InventoryGrid.InventoryItemDescriptor descriptor))
                    continue;

                buffer[count++] = new ItemPlacement
                {
                    itemHashId = descriptor.HashId,
                    x = anchorIndex % _grid.Columns,
                    y = anchorIndex / _grid.Columns,
                    width = descriptor.Width,
                    height = descriptor.Height,
                    maxStack = descriptor.MaxStack,
                    stackCount = (ushort)Mathf.Max(1, _stackCounts[anchorIndex]),
                    lockedCount = _craftLockedCounts[anchorIndex],
                    stateFlags = _itemStateFlags[anchorIndex],
                    geneticsMask = _itemGenetics[anchorIndex],
                    qualityMilli = _qualityMilli[anchorIndex] > 0 ? _qualityMilli[anchorIndex] : DefaultQualityMilli,
                    durability = _durabilities[anchorIndex],
                    lastUpdateUnixSeconds = _lastUpdateUnixSeconds[anchorIndex],
                    weight = descriptor.Weight,
                    unitVolumeM3 = _anchorUnitVolumeM3[anchorIndex],
                    unitRadiationSv = _anchorUnitRadiationSv[anchorIndex],
                    categoryId = descriptor.CategoryId,
                    rarity = descriptor.Rarity,
                    stackable = descriptor.Stackable
                };
            }

            return count;
        }

        public NativeArray<ushort>.ReadOnly GetStackCountsReadOnly()
        {
            return _stackCounts.IsCreated ? _stackCounts.AsReadOnly() : default;
        }

        public NativeArray<uint>.ReadOnly GetItemHashesReadOnly()
        {
            return _itemHashes.IsCreated ? _itemHashes.AsReadOnly() : default;
        }

        public NativeArray<ushort>.ReadOnly GetItemCountsReadOnly()
        {
            return GetStackCountsReadOnly();
        }

        public NativeArray<float>.ReadOnly GetItemConditionReadOnly()
        {
            return _itemCondition.IsCreated ? _itemCondition.AsReadOnly() : default;
        }

        public NativeArray<float>.ReadOnly GetItemDurabilityReadOnly()
        {
            return _itemDurability.IsCreated ? _itemDurability.AsReadOnly() : default;
        }

        public NativeArray<int>.ReadOnly GetItemIDsReadOnly()
        {
            return _grid != null ? _grid.AnchorHashIds : default;
        }

        public unsafe void* GetItemIDsUnsafeReadOnlyPtr(out int length)
        {
            if (_grid == null)
            {
                length = 0;
                return null;
            }

            return _grid.GetAnchorHashIdsUnsafeReadOnlyPtr(out length);
        }

        public unsafe void* GetItemHashesUnsafeReadOnlyPtr(out int length)
        {
            length = _itemHashes.IsCreated ? _itemHashes.Length : 0;
            return length > 0 ? NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_itemHashes) : null;
        }

        public unsafe void* GetItemCountsUnsafeReadOnlyPtr(out int length)
        {
            length = _stackCounts.IsCreated ? _stackCounts.Length : 0;
            return length > 0 ? NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_stackCounts) : null;
        }

        public unsafe void* GetItemConditionUnsafeReadOnlyPtr(out int length)
        {
            length = _itemCondition.IsCreated ? _itemCondition.Length : 0;
            return length > 0 ? NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_itemCondition) : null;
        }

        public NativeArray<ushort>.ReadOnly GetQuantitiesReadOnly()
        {
            return GetStackCountsReadOnly();
        }

        public NativeArray<byte>.ReadOnly GetDurabilitiesReadOnly()
        {
            SyncDurabilityBytesFromQuality();
            return _durabilities.IsCreated ? _durabilities.AsReadOnly() : default;
        }

        public bool TryGetInventorySoA(
            out NativeArray<int>.ReadOnly itemIDs,
            out NativeArray<ushort>.ReadOnly quantities,
            out NativeArray<byte>.ReadOnly durabilities)
        {
            itemIDs = GetItemIDsReadOnly();
            quantities = GetQuantitiesReadOnly();
            durabilities = GetDurabilitiesReadOnly();
            return _grid != null && _stackCounts.IsCreated && _durabilities.IsCreated;
        }

        public bool TryGetInventorySoA(
            out NativeArray<uint>.ReadOnly itemHashes,
            out NativeArray<ushort>.ReadOnly itemCounts,
            out NativeArray<float>.ReadOnly itemCondition,
            out ulong currentInventoryMask)
        {
            itemHashes = GetItemHashesReadOnly();
            itemCounts = GetItemCountsReadOnly();
            itemCondition = GetItemConditionReadOnly();
            currentInventoryMask = CurrentInventoryMask;
            return _itemHashes.IsCreated && _stackCounts.IsCreated && _itemCondition.IsCreated;
        }

        public NativeArray<ushort>.ReadOnly GetCraftLockedCountsReadOnly()
        {
            return _craftLockedCounts.IsCreated ? _craftLockedCounts.AsReadOnly() : default;
        }

        public NativeArray<ushort>.ReadOnly GetAnchorStateFlagsReadOnly()
        {
            return _anchorStateFlags.IsCreated ? _anchorStateFlags.AsReadOnly() : default;
        }

        private bool TryAddItemInternal(int itemHashId, int quantity, out int addedQuantity)
        {
            return TryAddItemWithStateInternal(itemHashId, quantity, 0UL, DefaultQualityMilli, out addedQuantity);
        }

        private bool TryAddItemWithStateInternal(int itemHashId, int quantity, ulong geneticsMask, ushort qualityMilli, out int addedQuantity)
        {
            addedQuantity = 0;
            if (_grid == null ||
                itemHashId == 0 ||
                quantity <= 0 ||
                !TryBuildDescriptor(itemHashId, out InventoryGrid.InventoryItemDescriptor descriptor) ||
                !TryGetRuntimeDescriptor(itemHashId, out ItemCatalog.ItemRuntimeDescriptor runtimeDescriptor))
            {
                return false;
            }

            uint timestampNow = ResolveCurrentUnixTimestamp();
            ushort resolvedQualityMilli = NormalizeQualityMilli(qualityMilli);
            byte compressedGenetics = CompressItemGenetics(geneticsMask);

            int requestedQuantity = quantity;
            if (!TryResolveCapacityLimitedQuantity(in runtimeDescriptor, requestedQuantity, out quantity))
            {
                InventoryEvents.NotifyInventoryFull(itemHashId);
                return false;
            }

            bool allAdded = quantity == requestedQuantity;
            int remainingQuantity = quantity;
            if (descriptor.Stackable)
            {
                int stackedQuantity = TryStackQuantityWithState(
                    descriptor.HashId,
                    descriptor.MaxStack,
                    runtimeDescriptor.StateFlags,
                    timestampNow,
                    compressedGenetics,
                    resolvedQualityMilli,
                    remainingQuantity);

                if (stackedQuantity > 0)
                {
                    TotalWeight += descriptor.Weight * stackedQuantity;
                    addedQuantity += stackedQuantity;
                    remainingQuantity -= stackedQuantity;
                }
            }

            while (remainingQuantity > 0)
            {
                int quantityForSlot = descriptor.Stackable
                    ? math.min(math.max(1, (int)descriptor.MaxStack), remainingQuantity)
                    : 1;
                if (_grid.TryAddItem(in descriptor, out int placedX, out int placedY))
                {
                    int anchorIndex = AnchorIndex(placedX, placedY);
                    _stackCounts[anchorIndex] = (ushort)quantityForSlot;
                    _itemStateFlags[anchorIndex] = runtimeDescriptor.StateFlags;
                    _itemGenetics[anchorIndex] = compressedGenetics;
                    _qualityMilli[anchorIndex] = resolvedQualityMilli;
                    if (_itemDurability.IsCreated && (uint)anchorIndex < (uint)_itemDurability.Length)
                        _itemDurability[anchorIndex] = math.saturate(resolvedQualityMilli * 0.001f);
                    _lastUpdateUnixSeconds[anchorIndex] = (runtimeDescriptor.StateFlags & BiologicalItemStateMask) != 0 ? timestampNow : 0u;
                    SetAnchorPhysicalMetadata(anchorIndex, runtimeDescriptor.MassKg, runtimeDescriptor.VolumeM3, runtimeDescriptor.RadiationSvPerSecond);
                    TotalWeight += descriptor.Weight * quantityForSlot;
                    addedQuantity += quantityForSlot;
                    remainingQuantity -= quantityForSlot;
                }
                else
                {
                    allAdded = false;
                    break;
                }
            }

            if (addedQuantity > 0)
            {
                NotifyInventoryChanged();
            }

            if (!allAdded)
                InventoryEvents.NotifyInventoryFull(itemHashId);

            return allAdded;
        }

        private int TryStackQuantityWithState(
            int itemHashId,
            int maxStack,
            ushort itemStateFlags,
            uint timestampNow,
            byte geneticsMask,
            ushort qualityMilli,
            int quantity)
        {
            if (_grid == null || !_stackCounts.IsCreated || itemHashId == 0 || maxStack <= 1 || quantity <= 0)
                return 0;

            int remainingQuantity = quantity;
            for (int anchorIndex = 0; anchorIndex < _stackCounts.Length; anchorIndex++)
            {
                if (!_grid.HasAnchor(anchorIndex) || _grid.GetAnchorHashId(anchorIndex) != itemHashId || IsCraftLockedFlagSet(anchorIndex))
                    continue;

                if ((_itemStateFlags.IsCreated && _itemStateFlags[anchorIndex] != itemStateFlags) ||
                    (_itemGenetics.IsCreated && _itemGenetics[anchorIndex] != geneticsMask) ||
                    (_qualityMilli.IsCreated && NormalizeQualityMilli(_qualityMilli[anchorIndex]) != qualityMilli))
                {
                    continue;
                }

                int stackCount = math.max(1, (int)_stackCounts[anchorIndex]);
                if (stackCount >= maxStack)
                    continue;

                ushort nextStackCount = InventorySoAUtility.ResolveStackInsert(
                    (ushort)math.min(stackCount, ushort.MaxValue),
                    (ushort)math.min(remainingQuantity, ushort.MaxValue),
                    (ushort)math.min(maxStack, ushort.MaxValue),
                    out ushort transfer);
                if (transfer == 0)
                    continue;

                _stackCounts[anchorIndex] = nextStackCount;
                _itemStateFlags[anchorIndex] = itemStateFlags;
                _itemGenetics[anchorIndex] = geneticsMask;
                _qualityMilli[anchorIndex] = qualityMilli;
                if (_itemDurability.IsCreated && (uint)anchorIndex < (uint)_itemDurability.Length)
                    _itemDurability[anchorIndex] = math.saturate(qualityMilli * 0.001f);
                if ((itemStateFlags & BiologicalItemStateMask) != 0 && _lastUpdateUnixSeconds[anchorIndex] == 0u)
                    _lastUpdateUnixSeconds[anchorIndex] = timestampNow;

                remainingQuantity -= transfer;
                if (remainingQuantity <= 0)
                    break;
            }

            return quantity - remainingQuantity;
        }

        private bool CanAcceptQuantity(int itemHashId, int quantity)
        {
            if (_grid == null ||
                itemHashId == 0 ||
                quantity <= 0 ||
                !_stackCounts.IsCreated ||
                !_scavengeSimStackCounts.IsCreated ||
                !_simulationOccupiedCells.IsCreated ||
                !TryBuildDescriptor(itemHashId, out InventoryGrid.InventoryItemDescriptor descriptor) ||
                !TryGetRuntimeDescriptor(itemHashId, out ItemCatalog.ItemRuntimeDescriptor runtimeDescriptor) ||
                !CanAcceptAdditionalPhysicalCapacity(in runtimeDescriptor, quantity))
            {
                return false;
            }

            CopyNativeArray(_stackCounts, _scavengeSimStackCounts);

            _grid.CopyOccupiedMask(_simulationOccupiedCells);

            int remaining = quantity;
            if (descriptor.Stackable)
            {
                for (int anchorIndex = 0; anchorIndex < _stackCounts.Length && remaining > 0; anchorIndex++)
                {
                    if (!_grid.HasAnchor(anchorIndex) || _grid.GetAnchorHashId(anchorIndex) != descriptor.HashId || IsCraftLockedFlagSet(anchorIndex))
                        continue;

                    int stackCount = math.max(1, (int)_scavengeSimStackCounts[anchorIndex]);
                    if (stackCount >= descriptor.MaxStack)
                        continue;

                    ushort nextStackCount = InventorySoAUtility.ResolveStackInsert(
                        (ushort)math.min(stackCount, ushort.MaxValue),
                        (ushort)math.min(remaining, ushort.MaxValue),
                        descriptor.MaxStack,
                        out ushort transfer);
                    if (transfer == 0)
                        continue;

                    _scavengeSimStackCounts[anchorIndex] = nextStackCount;
                    remaining -= transfer;
                }
            }

            while (remaining > 0)
            {
                if (!TryReservePlacementInSimulation(in descriptor))
                    return false;

                remaining -= descriptor.Stackable
                    ? math.min(math.max(1, (int)descriptor.MaxStack), remaining)
                    : 1;
            }

            return true;
        }

        private bool CanAcceptQuantityBatch(ReadOnlySpan<int> itemHashIds, ReadOnlySpan<int> quantities, int count)
        {
            if (_grid == null ||
                count < 0 ||
                itemHashIds.Length < count ||
                quantities.Length < count ||
                !_stackCounts.IsCreated ||
                !_scavengeSimStackCounts.IsCreated ||
                !_simulationOccupiedCells.IsCreated)
            {
                return false;
            }

            CopyNativeArray(_stackCounts, _scavengeSimStackCounts);
            _grid.CopyOccupiedMask(_simulationOccupiedCells);

            if (!TryResolveCurrentPhysicalTotals(out float currentWeightKg, out float currentVolumeLiters))
                return false;

            float additionalWeightKg = 0f;
            float additionalVolumeLiters = 0f;
            for (int groupIndex = 0; groupIndex < count; groupIndex++)
            {
                int itemHashId = itemHashIds[groupIndex];
                int remaining = quantities[groupIndex];
                if (itemHashId == 0 ||
                    remaining <= 0 ||
                    !TryBuildDescriptor(itemHashId, out InventoryGrid.InventoryItemDescriptor descriptor) ||
                    !TryGetRuntimeDescriptor(itemHashId, out ItemCatalog.ItemRuntimeDescriptor runtimeDescriptor) ||
                    !TryResolveAdditionalPhysicalDemand(in runtimeDescriptor, remaining, out float groupWeightKg, out float groupVolumeLiters))
                {
                    return false;
                }

                additionalWeightKg += groupWeightKg;
                additionalVolumeLiters += groupVolumeLiters;
                if (!math.isfinite(additionalWeightKg) ||
                    !math.isfinite(additionalVolumeLiters) ||
                    WouldExceedPhysicalCapacity(currentWeightKg, currentVolumeLiters, additionalWeightKg, additionalVolumeLiters))
                {
                    return false;
                }

                if (descriptor.Stackable)
                {
                    for (int anchorIndex = 0; anchorIndex < _stackCounts.Length && remaining > 0; anchorIndex++)
                    {
                        if (!_grid.HasAnchor(anchorIndex) || _grid.GetAnchorHashId(anchorIndex) != descriptor.HashId || IsCraftLockedFlagSet(anchorIndex))
                            continue;

                        int stackCount = math.max(1, (int)_scavengeSimStackCounts[anchorIndex]);
                        if (stackCount >= descriptor.MaxStack)
                            continue;

                        ushort nextStackCount = InventorySoAUtility.ResolveStackInsert(
                            (ushort)math.min(stackCount, ushort.MaxValue),
                            (ushort)math.min(remaining, ushort.MaxValue),
                            descriptor.MaxStack,
                            out ushort transfer);
                        if (transfer == 0)
                            continue;

                        _scavengeSimStackCounts[anchorIndex] = nextStackCount;
                        remaining -= transfer;
                    }
                }

                while (remaining > 0)
                {
                    if (!TryReservePlacementInSimulation(in descriptor))
                        return false;

                    remaining -= descriptor.Stackable
                        ? math.min(math.max(1, (int)descriptor.MaxStack), remaining)
                        : 1;
                }
            }

            return true;
        }

        private bool TryResolveCapacityLimitedQuantity(
            in ItemCatalog.ItemRuntimeDescriptor runtimeDescriptor,
            int requestedQuantity,
            out int allowedQuantity)
        {
            allowedQuantity = 0;
            if (requestedQuantity <= 0 ||
                !TryResolveCurrentPhysicalTotals(out float currentWeightKg, out float currentVolumeLiters) ||
                !TryResolveUnitPhysicalDemand(in runtimeDescriptor, out float unitMassKg, out float unitVolumeLiters))
            {
                return false;
            }

            allowedQuantity = requestedQuantity;
            allowedQuantity = ResolveCapacityLimitedQuantity(
                currentWeightKg,
                MaxWeightKg,
                unitMassKg,
                allowedQuantity);
            allowedQuantity = ResolveCapacityLimitedQuantity(
                currentVolumeLiters,
                MaxVolumeLiters,
                unitVolumeLiters,
                allowedQuantity);
            return allowedQuantity > 0;
        }

        private bool CanAcceptAdditionalPhysicalCapacity(in ItemCatalog.ItemRuntimeDescriptor runtimeDescriptor, int quantity)
        {
            return TryResolveCapacityLimitedQuantity(in runtimeDescriptor, quantity, out int allowedQuantity) &&
                   allowedQuantity == quantity;
        }

        private bool TryResolveAdditionalPhysicalDemand(
            in ItemCatalog.ItemRuntimeDescriptor runtimeDescriptor,
            int quantity,
            out float weightKg,
            out float volumeLiters)
        {
            weightKg = 0f;
            volumeLiters = 0f;
            if (quantity <= 0 || !TryResolveUnitPhysicalDemand(in runtimeDescriptor, out float unitMassKg, out float unitVolumeLiters))
                return false;

            float quantityFloat = quantity;
            weightKg = unitMassKg * quantityFloat;
            volumeLiters = unitVolumeLiters * quantityFloat;
            return math.isfinite(weightKg) && math.isfinite(volumeLiters);
        }

        private static bool TryResolveUnitPhysicalDemand(
            in ItemCatalog.ItemRuntimeDescriptor runtimeDescriptor,
            out float unitMassKg,
            out float unitVolumeLiters)
        {
            unitMassKg = math.max(0f, math.isfinite(runtimeDescriptor.MassKg) ? runtimeDescriptor.MassKg : 0f);
            float unitVolumeM3 = math.max(0f, math.isfinite(runtimeDescriptor.VolumeM3) ? runtimeDescriptor.VolumeM3 : 0f);
            unitVolumeLiters = unitVolumeM3 * VolumeM3ToLiters;
            return math.isfinite(unitMassKg) &&
                   math.isfinite(unitVolumeLiters) &&
                   unitMassKg > 0f &&
                   unitVolumeLiters > 0f;
        }

        private bool TryResolveCurrentPhysicalTotals(out float weightKg, out float volumeLiters)
        {
            weightKg = math.max(0f, math.isfinite(_currentWeightKg) ? _currentWeightKg : 0f);
            volumeLiters = math.max(0f, math.isfinite(_currentVolumeLiters) ? _currentVolumeLiters : 0f);
            if (_grid == null ||
                !_stackCounts.IsCreated ||
                !_anchorUnitMassKg.IsCreated ||
                !_anchorUnitVolumeM3.IsCreated)
            {
                return true;
            }

            NativeArray<int>.ReadOnly anchorHashIds = _grid.AnchorHashIds;
            int count = math.min(
                math.min(anchorHashIds.Length, _stackCounts.Length),
                math.min(_anchorUnitMassKg.Length, _anchorUnitVolumeM3.Length));
            float totalWeightKg = 0f;
            float totalVolumeM3 = 0f;
            for (int anchorIndex = 0; anchorIndex < count; anchorIndex++)
            {
                if (anchorHashIds[anchorIndex] == 0 || _stackCounts[anchorIndex] == 0)
                    continue;

                int stackCount = math.max(1, (int)_stackCounts[anchorIndex]);
                float unitMassKg = math.max(0f, math.isfinite(_anchorUnitMassKg[anchorIndex]) ? _anchorUnitMassKg[anchorIndex] : 0f);
                float unitVolumeM3 = math.max(0f, math.isfinite(_anchorUnitVolumeM3[anchorIndex]) ? _anchorUnitVolumeM3[anchorIndex] : 0f);
                totalWeightKg += unitMassKg * stackCount;
                totalVolumeM3 += unitVolumeM3 * stackCount;
            }

            if (!math.isfinite(totalWeightKg) || !math.isfinite(totalVolumeM3))
                return false;

            weightKg = math.max(0f, totalWeightKg);
            volumeLiters = math.max(0f, totalVolumeM3) * VolumeM3ToLiters;
            return math.isfinite(weightKg) && math.isfinite(volumeLiters);
        }

        private bool WouldExceedPhysicalCapacity(
            float currentWeightKg,
            float currentVolumeLiters,
            float additionalWeightKg,
            float additionalVolumeLiters)
        {
            float nextWeightKg = currentWeightKg + math.max(0f, additionalWeightKg);
            if (!math.isfinite(nextWeightKg) || nextWeightKg > MaxWeightKg)
                return true;

            float nextVolumeLiters = currentVolumeLiters + math.max(0f, additionalVolumeLiters);
            return !math.isfinite(nextVolumeLiters) || nextVolumeLiters > MaxVolumeLiters;
        }

        private static int ResolveCapacityLimitedQuantity(
            float currentValue,
            float maxValue,
            float unitValue,
            int requestedQuantity)
        {
            if (requestedQuantity <= 0)
                return 0;

            if (!math.isfinite(currentValue) || !math.isfinite(maxValue) || !math.isfinite(unitValue))
                return 0;

            if (unitValue <= 0f)
                return 0;

            float remaining = maxValue - currentValue;
            if (remaining <= 0f || !math.isfinite(remaining))
                return 0;

            float resolved = math.floor(remaining * math.rcp(math.max(0.0001f, unitValue)) + 0.0001f);
            if (!math.isfinite(resolved) || resolved <= 0f)
                return 0;

            return resolved >= requestedQuantity ? requestedQuantity : (int)resolved;
        }

        private bool TryReservePlacementInSimulation(in InventoryGrid.InventoryItemDescriptor descriptor)
        {
            int cols = _grid.Columns;
            int rows = _grid.Rows;
            int width = descriptor.Width;
            int height = descriptor.Height;
            if (width > cols || height > rows)
                return false;

            int maxX = cols - width;
            int maxY = rows - height;
            for (int y = 0; y <= maxY; y++)
            {
                for (int x = 0; x <= maxX; x++)
                {
                    if (_simulationOccupiedCells[AnchorIndex(x, y)] != 0 || !CheckFitInSimulation(x, y, width, height))
                        continue;

                    MarkOccupiedInSimulation(x, y, width, height);
                    return true;
                }
            }

            return false;
        }

        private bool CheckFitInSimulation(int startX, int startY, int width, int height)
        {
            int endX = startX + width;
            int endY = startY + height;
            for (int y = startY; y < endY; y++)
            {
                for (int x = startX; x < endX; x++)
                {
                    if (_simulationOccupiedCells[AnchorIndex(x, y)] != 0)
                        return false;
                }
            }

            return true;
        }

        private void MarkOccupiedInSimulation(int startX, int startY, int width, int height)
        {
            int endX = startX + width;
            int endY = startY + height;
            for (int y = startY; y < endY; y++)
            {
                for (int x = startX; x < endX; x++)
                    _simulationOccupiedCells[AnchorIndex(x, y)] = 1;
            }
        }

        private int AnchorIndex(int x, int y)
        {
            return y * _grid.Columns + x;
        }

        private bool IsCraftLockedFlagSet(int anchorIndex)
        {
            return _anchorStateFlags.IsCreated
                && (uint)anchorIndex < (uint)_anchorStateFlags.Length
                && (_anchorStateFlags[anchorIndex] & CraftingLockedMask) != 0;
        }

        private int GetReservedCraftCount(int anchorIndex)
        {
            if (!_craftLockedCounts.IsCreated || (uint)anchorIndex >= (uint)_craftLockedCounts.Length)
                return 0;

            return IsCraftLockedFlagSet(anchorIndex) ? _craftLockedCounts[anchorIndex] : 0;
        }

        private int CountAnchorsByHash(int itemHashId)
        {
            if (_grid == null || itemHashId == 0 || !_stackCounts.IsCreated)
                return 0;

            int count = 0;
            for (int i = 0; i < _stackCounts.Length; i++)
            {
                if (_grid.HasAnchor(i) && _grid.GetAnchorHashId(i) == itemHashId)
                    count++;
            }

            return count;
        }

        private int CountQuantityByHash(int itemHashId, bool availableOnly)
        {
            if (_grid == null || itemHashId == 0 || !_stackCounts.IsCreated)
                return 0;

            int total = 0;
            for (int anchorIndex = 0; anchorIndex < _stackCounts.Length; anchorIndex++)
            {
                if (!_grid.HasAnchor(anchorIndex) || _grid.GetAnchorHashId(anchorIndex) != itemHashId)
                    continue;

                int count = Mathf.Max(1, (int)_stackCounts[anchorIndex]);
                if (availableOnly)
                    count = Mathf.Max(0, count - GetReservedCraftCount(anchorIndex));

                total += count;
            }

            return total;
        }

        private bool TryBuildDescriptor(int itemHashId, out InventoryGrid.InventoryItemDescriptor descriptor)
        {
            descriptor = default;
            if (!TryGetRuntimeDescriptor(itemHashId, out ItemCatalog.ItemRuntimeDescriptor runtimeDescriptor))
                return false;

            descriptor = new InventoryGrid.InventoryItemDescriptor(
                runtimeDescriptor.HashId,
                runtimeDescriptor.Width,
                runtimeDescriptor.Height,
                runtimeDescriptor.MaxStack,
                runtimeDescriptor.Weight,
                runtimeDescriptor.CategoryId,
                0,
                runtimeDescriptor.Stackable);
            return descriptor.IsValid;
        }

        private bool TryApplyPlacements(ItemPlacement[] placements, int placementCount)
        {
            if (_grid == null || placements == null || !_stackCounts.IsCreated)
                return false;

            _grid.Clear();
            ClearNativeArray(_stackCounts);
            ClearNativeArray(_craftLockedCounts);
            ClearNativeArray(_anchorStateFlags);
            ClearNativeArray(_itemStateFlags);
            ClearNativeArray(_itemGenetics);
            ClearNativeArray(_qualityMilli);
            ClearNativeArray(_itemDurability);
            ClearNativeArray(_durabilities);
            ClearNativeArray(_lastUpdateUnixSeconds);
            ClearNativeArray(_anchorUnitMassKg);
            ClearNativeArray(_anchorUnitVolumeM3);
            ClearNativeArray(_anchorUnitRadiationSv);
            TotalWeight = 0f;

            for (int placementIndex = 0; placementIndex < placementCount; placementIndex++)
            {
                ItemPlacement placement = placements[placementIndex];
                InventoryGrid.InventoryItemDescriptor descriptor = placement.Descriptor;
                if (!descriptor.IsValid || !_grid.PlaceAt(in descriptor, placement.x, placement.y))
                    return false;

                int anchorIndex = AnchorIndex(placement.x, placement.y);
                _stackCounts[anchorIndex] = (ushort)Mathf.Max(1, placement.stackCount);
                if (_craftLockedCounts.IsCreated)
                    _craftLockedCounts[anchorIndex] = placement.lockedCount;
                if (_itemStateFlags.IsCreated)
                    _itemStateFlags[anchorIndex] = placement.stateFlags;
                if (_itemGenetics.IsCreated)
                    _itemGenetics[anchorIndex] = SanitizeItemGeneticsFlags(placement.geneticsMask);
                if (_qualityMilli.IsCreated)
                    _qualityMilli[anchorIndex] = placement.qualityMilli;
                if (_durabilities.IsCreated)
                    _durabilities[anchorIndex] = placement.durability > 0
                        ? placement.durability
                        : (byte)math.clamp((placement.qualityMilli + 5) / 10, 0, 100);
                if (_itemDurability.IsCreated && (uint)anchorIndex < (uint)_itemDurability.Length)
                    _itemDurability[anchorIndex] = _durabilities.IsCreated
                        ? math.saturate(_durabilities[anchorIndex] * 0.01f)
                        : math.saturate(placement.qualityMilli * 0.001f);
                if (_lastUpdateUnixSeconds.IsCreated)
                    _lastUpdateUnixSeconds[anchorIndex] = placement.lastUpdateUnixSeconds;
                if (placement.weight > 0f || placement.unitVolumeM3 > 0f || placement.unitRadiationSv > 0f)
                    SetAnchorPhysicalMetadata(anchorIndex, placement.weight, placement.unitVolumeM3, placement.unitRadiationSv);
                else
                    SyncAnchorPhysicalMetadata(anchorIndex, placement.itemHashId);
                TotalWeight += _anchorUnitMassKg[anchorIndex] * Mathf.Max(1, placement.stackCount);
            }

            return true;
        }

        private static bool TryFindPlacementIndex(ItemPlacement[] placements, int placementCount, int anchorX, int anchorY, out int placementIndex)
        {
            for (int i = 0; i < placementCount; i++)
            {
                if (placements[i].x == anchorX && placements[i].y == anchorY)
                {
                    placementIndex = i;
                    return true;
                }
            }

            placementIndex = -1;
            return false;
        }

        private void NotifyInventoryChanged(bool markDirty = true, bool massDirty = true)
        {
            RefreshInventorySoAMirrorsAndMask();

            if (markDirty)
            {
                MarkInventoryDirty();
                RefreshInventoryShadowBufferFromRuntime();
            }

            if (massDirty)
                MarkMassCacheDirty();

            if (_massCacheDirty)
                RefreshDerivedMassAndSurvivalLoad();

            _durabilitySnapshotDirty = true;
            PublishEncumbranceChanged();
            InventoryVersion++;
            InventoryEvents.NotifyInventoryChanged();
            SignalBus<InventoryChangedSignal>.Push(new InventoryChangedSignal
            {
                InventoryHash = ResolveInventorySignalHash(),
                Revision = unchecked((uint)InventoryVersion),
                Frame = (uint)Mathf.Max(0, Time.frameCount),
                OccupiedCells = _grid != null ? (ushort)math.clamp(_grid.OccupiedCells, 0, ushort.MaxValue) : (ushort)0,
                Flags = 0
            });
            InventoryChanged?.Invoke();
        }

        private void RefreshInventorySoAMirrorsAndMask()
        {
            if (_grid == null ||
                !_itemHashes.IsCreated ||
                !_stackCounts.IsCreated ||
                !_itemCondition.IsCreated ||
                !_itemDurability.IsCreated ||
                !_qualityMilli.IsCreated)
            {
                CurrentInventoryMask = 0UL;
                return;
            }

            ulong inventoryMask = 0UL;
            int count = math.min(
                math.min(_itemHashes.Length, _stackCounts.Length),
                math.min(math.min(_itemCondition.Length, _itemDurability.Length), _qualityMilli.Length));
            for (int anchorIndex = 0; anchorIndex < count; anchorIndex++)
            {
                if (!_grid.HasAnchor(anchorIndex))
                {
                    _itemHashes[anchorIndex] = 0u;
                    _stackCounts[anchorIndex] = 0;
                    _itemCondition[anchorIndex] = 0f;
                    _itemDurability[anchorIndex] = 0f;
                    continue;
                }

                int itemHashId = _grid.GetAnchorHashId(anchorIndex);
                ushort stackCount = _stackCounts[anchorIndex];
                if (itemHashId == 0)
                {
                    _itemHashes[anchorIndex] = 0u;
                    _stackCounts[anchorIndex] = 0;
                    _itemCondition[anchorIndex] = 0f;
                    _itemDurability[anchorIndex] = 0f;
                    continue;
                }

                if (stackCount == 0)
                {
                    stackCount = 1;
                    _stackCounts[anchorIndex] = 1;
                }

                _itemHashes[anchorIndex] = unchecked((uint)itemHashId);
                float condition01 = math.saturate((_qualityMilli[anchorIndex] > 0 ? _qualityMilli[anchorIndex] : DefaultQualityMilli) * 0.001f);
                _itemCondition[anchorIndex] = condition01;
                _itemDurability[anchorIndex] = condition01;
                if ((_itemStateFlags.IsCreated && (uint)anchorIndex < (uint)_itemStateFlags.Length && (_itemStateFlags[anchorIndex] & BrokenItemStateMask) != 0) == false)
                    inventoryMask |= InventoryMaterialMask.ResolveBit(itemHashId);
            }

            CurrentInventoryMask = inventoryMask;
        }

        private void MarkInventoryDirty()
        {
            _isDirty = true;
            unchecked
            {
                _inventoryDirtyRevision++;
                if (_inventoryDirtyRevision == 0u)
                    _inventoryDirtyRevision = 1u;
            }
        }

        private void MarkMassCacheDirty()
        {
            _massCacheDirty = true;
        }

        private void PublishEncumbranceChanged()
        {
            float carryCapacityKg = ResolveCarryCapacityKilograms();
            UIStateStore.WriteInventoryLoadState(TotalMassKg, carryCapacityKg, CachedInventoryLoad01, Time.unscaledTime);
            InventoryEvents.NotifyEncumbranceChanged(new EncumbranceChangedEvent(
                this,
                TotalMassKg,
                carryCapacityKg,
                CachedInventoryLoad01));
        }

        private uint ResolveInventorySignalHash()
        {
            return gameObject != null ? unchecked((uint)EntityId.ToULong(gameObject.GetEntityId())) : 0u;
        }

        private float ResolveCarryCapacityKilograms()
        {
            return survival != null && survival.Stats != null
                ? math.max(0.01f, survival.Stats.CarryCapacityKg)
                : 200f;
        }

        private void TryRegisterSlowTick()
        {
            if (_registeredSlowTick)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Player);
            _registeredSlowTick = GlobalRegistry.SlowTickables.Contains(this);
        }

        private void TryUnregisterSlowTick()
        {
            if (!_registeredSlowTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Player);
            _registeredSlowTick = false;
        }

        private void TryRegisterLateFrameTick()
        {
            if (_registeredLateFrameTick)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Player);
            _registeredLateFrameTick = SystemDispatcher.GetLateFrameLane(PriorityLayer.Player).Contains(this);
        }

        private void TryUnregisterLateFrameTick()
        {
            if (!_registeredLateFrameTick)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _registeredLateFrameTick = false;
        }

        private void DrainSalinityBiomeSignals()
        {
            ReadOnlySpan<BiomeChangedSignal> signals = SignalBus<BiomeChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                BiomeChangedSignal signal = signals[i];
                if (signal.CurrentBiomeHash == 0u)
                    continue;

                _currentSalinityBiomeHash = signal.CurrentBiomeHash;
                _currentSalinityFactor = ResolveSalinityFactor(signal.CurrentBiomeHash);
            }
        }

        private void DrainRepairToolTitaniumSignals()
        {
            ReadOnlySpan<ItemAcquiredSignal> signals = SignalBus<ItemAcquiredSignal>.GetFrameSnapshot();
            if (signals.Length == 0 || !TryResolveActiveRepairToolItemHash(out int repairToolItemHash))
                return;

            for (int i = 0; i < signals.Length; i++)
            {
                ItemAcquiredSignal signal = signals[i];
                if (signal.ItemHash != _TitaniumScrapHashId || signal.Frame <= _lastRepairTitaniumFrame)
                    continue;

                _lastRepairTitaniumFrame = signal.Frame;
                if (RestoreDurabilityForItemHash(repairToolItemHash))
                    return;
            }
        }

        private bool TryResolveActiveRepairToolItemHash(out int itemHashId)
        {
            itemHashId = 0;
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            PlayerToolManager toolManager = playerContext != null ? playerContext.ToolManager : null;
            PlayerTool currentTool = toolManager != null ? toolManager.CurrentTool : null;
            if (!(currentTool is RepairTool) || currentTool.ToolData == null || string.IsNullOrEmpty(currentTool.ToolData.PersistentId))
                return false;

            itemHashId = LocHash.Compute(currentTool.ToolData.PersistentId);
            return itemHashId != 0;
        }

        private bool RestoreDurabilityForItemHash(int itemHashId)
        {
            if (itemHashId == 0 ||
                _grid == null ||
                !_itemHashes.IsCreated ||
                !_stackCounts.IsCreated ||
                !_itemDurability.IsCreated ||
                !_durabilities.IsCreated ||
                !_qualityMilli.IsCreated ||
                !_itemStateFlags.IsCreated)
            {
                return false;
            }

            bool changed = false;
            int count = math.min(
                math.min(math.min(_itemHashes.Length, _stackCounts.Length), math.min(_itemDurability.Length, _durabilities.Length)),
                math.min(_qualityMilli.Length, _itemStateFlags.Length));
            for (int anchorIndex = 0; anchorIndex < count; anchorIndex++)
            {
                if (!_grid.HasAnchor(anchorIndex) ||
                    _stackCounts[anchorIndex] == 0 ||
                    _itemHashes[anchorIndex] != unchecked((uint)itemHashId))
                {
                    continue;
                }

                _itemDurability[anchorIndex] = 1f;
                _durabilities[anchorIndex] = 100;
                _qualityMilli[anchorIndex] = DefaultQualityMilli;
                _itemStateFlags[anchorIndex] = (ushort)(_itemStateFlags[anchorIndex] & ~(BrokenItemStateMask | DegradedItemStateMask | RustedItemStateMask));
                PublishItemDurabilityChanged(unchecked((uint)itemHashId), 1f, ItemDurabilityChangedSignal.ReasonRepair, (ushort)anchorIndex);
                changed = true;
            }

            if (!changed)
                return false;

            _averageEquipmentDurability01 = ResolveAverageEquipmentDurability();
            UpdateEquipmentRustShaderScalar();
            UpdateEquipmentFailingNotification();
            NotifyInventoryChanged(massDirty: false);
            return true;
        }

        private void ApplyInventorySalinityCorrosion()
        {
            _salinityCorrosionTickAccumulator += SlowTickIntervalSeconds;
            bool runFrostTick = _salinityCorrosionTickAccumulator >= SalinityCorrosionFrostTickSeconds;
            if (runFrostTick)
                _salinityCorrosionTickAccumulator = math.max(0f, _salinityCorrosionTickAccumulator - SalinityCorrosionFrostTickSeconds);

            if (!runFrostTick)
            {
                UpdateEquipmentRustShaderScalar();
                WriteSalinityCorrosionBlackBoxFrame(0);
                return;
            }

            if (!CanRunSalinityCorrosionJob())
            {
                _averageEquipmentDurability01 = ResolveAverageEquipmentDurability();
                UpdateEquipmentRustShaderScalar();
                WriteSalinityCorrosionBlackBoxFrame(1);
                return;
            }

            JobHandle salinityHandle = new ItemSalinityCorrosionJob
            {
                ItemHashes = _itemHashes.AsReadOnly(),
                StackCounts = _stackCounts,
                ItemDurability = _itemDurability,
                DurabilityBytes = _durabilities,
                QualityMilli = _qualityMilli,
                ItemStateFlags = _itemStateFlags,
                Result = _salinityCorrosionJobResult,
                BrokenItemHashes = _salinityBrokenItemHashes,
                CurrentInventoryMask = CurrentInventoryMask,
                SalinityFactor = _currentSalinityFactor,
                DegradationRate = SalinityCorrosionDegradationRatePerFrostTick,
                DegradedMask = DegradedItemStateMask,
                RustedMask = RustedItemStateMask,
                BrokenMask = BrokenItemStateMask,
                DegradedThresholdMilli = DegradedQualityMilliThreshold
            }.Schedule();

            DispatcherJobSwap.TryComplete(ref salinityHandle, forceComplete: true);

            int averageMilli = _salinityCorrosionJobResult[InventoryCorrosionConstants.ResultAverageDurabilityMilli];
            _averageEquipmentDurability01 = math.saturate(averageMilli * 0.001f);
            int changedCount = _salinityCorrosionJobResult[InventoryCorrosionConstants.ResultChangedCount];
            int brokenCount = _salinityCorrosionJobResult[InventoryCorrosionConstants.ResultBrokenCount];

            UpdateEquipmentRustShaderScalar();
            UpdateEquipmentFailingNotification();
            WriteSalinityCorrosionBlackBoxFrame(changedCount > 0 ? 2 : 0);

            if (brokenCount > 0)
                PublishBrokenEquipmentSignals(brokenCount);

            if (changedCount > 0)
            {
                PublishItemDurabilityChanged(0u, _averageEquipmentDurability01, ItemDurabilityChangedSignal.ReasonCorrosion, ushort.MaxValue);
                NotifyInventoryChanged(massDirty: false);
            }
        }

        private bool CanRunSalinityCorrosionJob()
        {
            return _itemHashes.IsCreated &&
                   _stackCounts.IsCreated &&
                   _itemDurability.IsCreated &&
                   _durabilities.IsCreated &&
                   _qualityMilli.IsCreated &&
                   _itemStateFlags.IsCreated &&
                   _salinityCorrosionJobResult.IsCreated &&
                   _salinityCorrosionJobResult.Length >= InventoryCorrosionConstants.ResultRequiredLength &&
                   _salinityBrokenItemHashes.IsCreated;
        }

        private void PublishBrokenEquipmentSignals(int brokenCount)
        {
            int count = math.min(brokenCount, _salinityBrokenItemHashes.Length);
            for (int i = 0; i < count; i++)
            {
                uint itemHash = _salinityBrokenItemHashes[i];
                if (itemHash == 0u)
                    continue;

                GlobalSignals.Publish(new ToolAcousticSignal
                {
                    ToolHash = _EquipmentCorrosionToolHash,
                    TargetHash = itemHash,
                    Progress01 = 1f,
                    PitchScale = 0.72f,
                    Intensity01 = 0.85f,
                    Frame = (uint)Mathf.Max(0, Time.frameCount),
                    State = 3,
                    Flags = 1
                });
                PublishItemDurabilityChanged(itemHash, 0f, ItemDurabilityChangedSignal.ReasonBreak, ushort.MaxValue);
            }
        }

        private void PublishItemDurabilityChanged(uint itemHash, float durability01, byte reason, ushort slotIndex)
        {
            GlobalSignals.Publish(new ItemDurabilityChangedSignal
            {
                InventoryHash = ResolveInventorySignalHash(),
                ItemHash = itemHash,
                Durability01 = math.saturate(durability01),
                AverageEquippedDurability01 = _averageEquipmentDurability01,
                Frame = (uint)Mathf.Max(0, Time.frameCount),
                SlotIndex = slotIndex,
                Reason = reason,
                Flags = 0,
                BiomeHash = _currentSalinityBiomeHash
            });
        }

        private void UpdateEquipmentFailingNotification()
        {
            if (_averageEquipmentDurability01 < EquipmentFailingThreshold01)
            {
                if (_equipmentFailingHudLatched != 0)
                    return;

                _equipmentFailingHudLatched = 1;
                GlobalSignals.Publish(new HUDNotificationSignal
                {
                    MessageHash = _EquipmentFailingMessageHash,
                    ContextHash = _EquipmentFailingContextHash,
                    SourceId = ResolveInventorySignalHash(),
                    Frame = (uint)Mathf.Max(0, Time.frameCount),
                    Severity = 2,
                    Flags = 0
                });
                return;
            }

            if (_averageEquipmentDurability01 >= EquipmentFailingResetThreshold01)
                _equipmentFailingHudLatched = 0;
        }

        private void UpdateEquipmentRustShaderScalar()
        {
            Shader.SetGlobalFloat(_HectonEquipmentRust01Id, math.saturate(1f - _averageEquipmentDurability01));
        }

        private float ResolveAverageEquipmentDurability()
        {
            if (_grid == null || !_itemHashes.IsCreated || !_stackCounts.IsCreated || !_itemDurability.IsCreated)
                return 1f;

            int count = math.min(math.min(_itemHashes.Length, _stackCounts.Length), _itemDurability.Length);
            float total = 0f;
            int equipped = 0;
            for (int anchorIndex = 0; anchorIndex < count; anchorIndex++)
            {
                uint hash = _itemHashes[anchorIndex];
                if (hash == 0u || _stackCounts[anchorIndex] == 0)
                    continue;

                ulong bit = InventoryMaterialMask.ResolveBit(hash);
                if ((CurrentInventoryMask & bit) == 0UL)
                    continue;

                total += math.saturate(_itemDurability[anchorIndex]);
                equipped++;
            }

            return equipped > 0 ? math.saturate(total / equipped) : 1f;
        }

        private void WriteSalinityCorrosionBlackBoxFrame(int flags)
        {
            if (!_salinityCorrosionBlackBox.IsCreated || _salinityCorrosionBlackBox.Length == 0)
                return;

            if (!math.isfinite(_averageEquipmentDurability01) || !math.isfinite(_currentSalinityFactor))
            {
                flags |= 0x40;
                DumpSalinityCorrosionBlackBoxOnce();
            }

            int index = _salinityCorrosionBlackBoxCursor % _salinityCorrosionBlackBox.Length;
            _salinityCorrosionBlackBox[index] = new SalinityCorrosionTelemetryEntry
            {
                Frame = (uint)Mathf.Max(0, Time.frameCount),
                InventoryVersion = unchecked((uint)InventoryVersion),
                AverageEquipmentDurability01 = _averageEquipmentDurability01,
                RustScalar01 = math.saturate(1f - _averageEquipmentDurability01),
                SalinityFactor = _currentSalinityFactor,
                CurrentBiomeHash = _currentSalinityBiomeHash,
                InventoryMaskLow = unchecked((uint)CurrentInventoryMask),
                Flags = flags
            };

            _salinityCorrosionBlackBoxCursor = (_salinityCorrosionBlackBoxCursor + 1) % _salinityCorrosionBlackBox.Length;
        }

        private void DumpSalinityCorrosionBlackBoxOnce()
        {
            if (_salinityCorrosionBlackBoxDumped != 0 || !_salinityCorrosionBlackBox.IsCreated)
                return;

            _salinityCorrosionBlackBoxDumped = 1;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", SalinityCorrosionBlackBoxDumpRelativePath));
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))
            {
                writer.Write(_salinityCorrosionBlackBox.Length);
                writer.Write(SalinityCorrosionBlackBoxEntrySizeBytes);
                for (int i = 0; i < _salinityCorrosionBlackBox.Length; i++)
                {
                    SalinityCorrosionTelemetryEntry entry = _salinityCorrosionBlackBox[i];
                    writer.Write(entry.Frame);
                    writer.Write(entry.InventoryVersion);
                    writer.Write(entry.AverageEquipmentDurability01);
                    writer.Write(entry.RustScalar01);
                    writer.Write(entry.SalinityFactor);
                    writer.Write(entry.CurrentBiomeHash);
                    writer.Write(entry.InventoryMaskLow);
                    writer.Write(entry.Flags);
                }
            }
        }

        private static float ResolveSalinityFactor(uint biomeHash)
        {
            if (biomeHash == 0u)
                return 0f;

            if (biomeHash == _BrineFamilyLocHash ||
                biomeHash == _BrineFamilyDataHash ||
                biomeHash == _BrineRiversLocHash ||
                biomeHash == _BrineRiversDataHash ||
                biomeHash == _ThermalBrineDataHash)
            {
                return 1f;
            }

            int folded = (int)(biomeHash & 0xFFu);
            return folded >= 0xD0 ? 0.55f : 0.18f;
        }

        private void ApplyInventoryEnvironmentalDegradation()
        {
            if (_grid == null ||
                !_stackCounts.IsCreated ||
                !_itemStateFlags.IsCreated ||
                !_qualityMilli.IsCreated)
            {
                return;
            }

            bool changed = false;
            bool isSubmerged = ResolveInventoryCarrierSubmergedState();
            float ambientTemperature = survival != null ? survival.EnvironmentTemperature : 2f;
            float temperatureFactor = math.max(0.35f, 1f + ((ambientTemperature - 4f) * 0.05f));
            uint nowTimestamp = ResolveCurrentUnixTimestamp();

            for (int anchorIndex = 0; anchorIndex < _stackCounts.Length; anchorIndex++)
            {
                if (!_grid.TryGetAnchorDescriptor(anchorIndex, out InventoryGrid.InventoryItemDescriptor descriptor) ||
                    !TryGetRuntimeDescriptor(descriptor.HashId, out ItemCatalog.ItemRuntimeDescriptor runtimeDescriptor))
                {
                    continue;
                }

                if (ApplyEnvironmentalDegradation(anchorIndex, in runtimeDescriptor, isSubmerged, temperatureFactor, nowTimestamp))
                    changed = true;
            }

            if (changed)
                NotifyInventoryChanged(massDirty: false);
        }

        private void RefreshDerivedMassAndSurvivalLoad()
        {
            if (_grid == null ||
                !_stackCounts.IsCreated ||
                !_anchorUnitMassKg.IsCreated ||
                !_anchorUnitVolumeM3.IsCreated ||
                !_anchorUnitRadiationSv.IsCreated ||
                !_derivedMassVolumeScratch.IsCreated)
            {
                ApplyDerivedMassTotals(float3.zero);
            }
            else
            {
                // ZERO-GC INLINE KERNEL: mutation seam refresh keeps public totals current before notifications.
                new InventoryMassVolumeJob
                {
                    AnchorHashIds = _grid.AnchorHashIds,
                    StackCounts = _stackCounts,
                    AnchorUnitMassKg = _anchorUnitMassKg,
                    AnchorUnitVolumeM3 = _anchorUnitVolumeM3,
                    AnchorUnitRadiationSv = _anchorUnitRadiationSv,
                    Totals = _derivedMassVolumeScratch
                }.Execute();

                ApplyDerivedMassTotals(_derivedMassVolumeScratch[0]);
            }

            _massCacheDirty = false;
        }

        private void ApplyDerivedMassTotals(float3 totals)
        {
            bool invalidTotals = !math.isfinite(totals.x) || !math.isfinite(totals.y) || !math.isfinite(totals.z);
            _currentWeightKg = math.max(0f, math.isfinite(totals.x) ? totals.x : 0f);
            TotalVolumeM3 = math.max(0f, math.isfinite(totals.y) ? totals.y : 0f);
            _currentVolumeLiters = TotalVolumeM3 * VolumeM3ToLiters;
            TotalRadiationSv = math.max(0f, math.isfinite(totals.z) ? totals.z : 0f);
            TotalWeight = _currentWeightKg;
            GlobalRegistry.PublishPlayerInventoryMassKg(_currentWeightKg);
            if (survival != null)
                survival.SetWeight(_currentWeightKg);

            float carryCapacityKg = ResolveCarryCapacityKilograms();
            float inverseCarryCapacityKg = math.rcp(carryCapacityKg);
            CachedInventoryLoad01 = math.saturate(_currentWeightKg * inverseCarryCapacityKg);
            CachedMaxSwimSpeedMultiplier = math.lerp(1f, InventoryLoadMinimumMovementMultiplier, CachedInventoryLoad01);

            WriteInventoryBlackBoxFrame(invalidTotals ? 1 : 0);
            if (invalidTotals)
                DumpInventoryBlackBoxOnce();
        }

        private void WriteInventoryBlackBoxFrame(int flags)
        {
            if (!_inventoryBlackBox.IsCreated || _inventoryBlackBox.Length == 0)
                return;

            int index = _inventoryBlackBoxCursor;
            if ((uint)index >= (uint)_inventoryBlackBox.Length)
                index = 0;

            _inventoryBlackBox[index] = new InventoryTelemetryEntry
            {
                Frame = (uint)Mathf.Max(0, Time.frameCount),
                Version = unchecked((uint)InventoryVersion),
                WeightKg = _currentWeightKg,
                VolumeLiters = _currentVolumeLiters,
                Load01 = CachedInventoryLoad01,
                InventoryMaskLow = unchecked((uint)CurrentInventoryMask),
                OccupiedCells = _grid != null ? _grid.OccupiedCells : 0,
                Flags = flags,
                MaxWeightKg = MaxWeightKg,
                MaxVolumeLiters = MaxVolumeLiters,
                ShadowHash = _inventoryShadowHash,
                ShadowPayloadLength = _inventoryShadowPayloadLength,
                RadiationSv = TotalRadiationSv,
                Columns = _grid != null ? _grid.Columns : columns,
                Rows = _grid != null ? _grid.Rows : rows,
                DefragTimeMicroseconds = _lastDefragTimeMicroseconds
            };

            _inventoryBlackBoxCursor = index + 1;
            if (_inventoryBlackBoxCursor >= _inventoryBlackBox.Length)
                _inventoryBlackBoxCursor = 0;
        }

        private void DumpInventoryBlackBoxOnce()
        {
            if (_inventoryBlackBoxDumped != 0 || !_inventoryBlackBox.IsCreated)
                return;

            _inventoryBlackBoxDumped = 1;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", InventoryBlackBoxDumpRelativePath));
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (FileStream stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(0x514D494Eu);
                writer.Write(InventoryBlackBoxCapacity);
                writer.Write(InventoryBlackBoxEntrySizeBytes);
                writer.Write(_inventoryBlackBoxCursor);
                for (int i = 0; i < _inventoryBlackBox.Length; i++)
                {
                    InventoryTelemetryEntry entry = _inventoryBlackBox[i];
                    writer.Write(entry.Frame);
                    writer.Write(entry.Version);
                    writer.Write(entry.WeightKg);
                    writer.Write(entry.VolumeLiters);
                    writer.Write(entry.Load01);
                    writer.Write(entry.InventoryMaskLow);
                    writer.Write(entry.OccupiedCells);
                    writer.Write(entry.Flags);
                    writer.Write(entry.MaxWeightKg);
                    writer.Write(entry.MaxVolumeLiters);
                    writer.Write(entry.ShadowHash);
                    writer.Write(entry.ShadowPayloadLength);
                    writer.Write(entry.RadiationSv);
                    writer.Write(entry.Columns);
                    writer.Write(entry.Rows);
                    writer.Write(entry.DefragTimeMicroseconds);
                }
            }
        }

        private void ScheduleInventoryMassRecomputeJob()
        {
            if (_massVolumeJobScheduled ||
                !_massCacheDirty ||
                !_derivedMassVolumeScratch.IsCreated)
            {
                return;
            }

            if (!TryBuildMassVolumeSnapshot())
                return;

            _massVolumeJobInventoryVersion = InventoryVersion;
            _massVolumeJobHandle = new InventoryMassVolumeJob
            {
                AnchorHashIds = _massAnchorHashSnapshot.AsReadOnly(),
                StackCounts = _massStackCountSnapshot,
                AnchorUnitMassKg = _massUnitMassSnapshot,
                AnchorUnitVolumeM3 = _massUnitVolumeSnapshot,
                AnchorUnitRadiationSv = _massUnitRadiationSnapshot,
                Totals = _derivedMassVolumeScratch
            }.Schedule();
            _massVolumeJobScheduled = true;
        }

        private bool TryBuildMassVolumeSnapshot()
        {
            if (_grid == null ||
                !_stackCounts.IsCreated ||
                !_anchorUnitMassKg.IsCreated ||
                !_anchorUnitVolumeM3.IsCreated ||
                !_anchorUnitRadiationSv.IsCreated ||
                !_massAnchorHashSnapshot.IsCreated ||
                !_massStackCountSnapshot.IsCreated ||
                !_massUnitMassSnapshot.IsCreated ||
                !_massUnitVolumeSnapshot.IsCreated ||
                !_massUnitRadiationSnapshot.IsCreated)
            {
                return false;
            }

            NativeArray<int>.ReadOnly anchorHashIds = _grid.AnchorHashIds;
            int count = math.min(
                math.min(math.min(anchorHashIds.Length, _stackCounts.Length), math.min(_anchorUnitMassKg.Length, _anchorUnitVolumeM3.Length)),
                math.min(_anchorUnitRadiationSv.Length, _massAnchorHashSnapshot.Length));
            count = math.min(
                count,
                math.min(math.min(_massStackCountSnapshot.Length, _massUnitMassSnapshot.Length), math.min(_massUnitVolumeSnapshot.Length, _massUnitRadiationSnapshot.Length)));
            if (count <= 0)
                return false;

            for (int anchorIndex = 0; anchorIndex < count; anchorIndex++)
            {
                _massAnchorHashSnapshot[anchorIndex] = anchorHashIds[anchorIndex];
                _massStackCountSnapshot[anchorIndex] = _stackCounts[anchorIndex];
                _massUnitMassSnapshot[anchorIndex] = _anchorUnitMassKg[anchorIndex];
                _massUnitVolumeSnapshot[anchorIndex] = _anchorUnitVolumeM3[anchorIndex];
                _massUnitRadiationSnapshot[anchorIndex] = _anchorUnitRadiationSv[anchorIndex];
            }

            return true;
        }

        private bool CompleteInventoryMassRecomputeJob(bool forceComplete)
        {
            if (!_massVolumeJobScheduled)
                return true;

            if (!DispatcherJobSwap.TryComplete(ref _massVolumeJobHandle, forceComplete))
                return false;

            _massVolumeJobScheduled = false;
            if (_massVolumeJobInventoryVersion == InventoryVersion &&
                _derivedMassVolumeScratch.IsCreated &&
                _derivedMassVolumeScratch.Length > 0)
            {
                ApplyDerivedMassTotals(_derivedMassVolumeScratch[0]);
                _massCacheDirty = false;
            }

            return true;
        }

        private bool ApplyEnvironmentalDegradation(
            int anchorIndex,
            in ItemCatalog.ItemRuntimeDescriptor runtimeDescriptor,
            bool isSubmerged,
            float temperatureFactor,
            uint nowTimestamp)
        {
            ushort currentQualityMilli = _qualityMilli[anchorIndex] > 0 ? _qualityMilli[anchorIndex] : DefaultQualityMilli;
            float currentQuality = math.clamp(currentQualityMilli * 0.001f, 0f, 1f);
            float decayPerSecond = 0f;

            if (ItemPhysicalMetadataUtility.IsOrganic(runtimeDescriptor.AudioMaterialId))
            {
                decayPerSecond = OrganicDecayPerSecond * temperatureFactor;
                if (isSubmerged)
                    decayPerSecond += SubmergedOrganicDecayPerSecond * math.max(0.5f, temperatureFactor);
            }
            else if (isSubmerged && ItemPhysicalMetadataUtility.IsMetal(runtimeDescriptor.AudioMaterialId))
            {
                decayPerSecond = SubmergedMetalRustPerSecond * math.max(0.75f, temperatureFactor);
                _itemStateFlags[anchorIndex] |= RustedItemStateMask;
            }

            if (!(decayPerSecond > 0f))
                return false;

            float nextQuality = math.clamp(currentQuality - (decayPerSecond * SlowTickIntervalSeconds), 0f, 1f);
            ushort nextQualityMilli = (ushort)math.clamp((int)math.round(nextQuality * 1000f), 0, 1000);
            bool changed = nextQualityMilli != currentQualityMilli;
            if (changed)
            {
                _qualityMilli[anchorIndex] = nextQualityMilli;
                if (nextQualityMilli < DegradedQualityMilliThreshold)
                    _itemStateFlags[anchorIndex] |= DegradedItemStateMask;
            }

            if (nowTimestamp != 0u)
                _lastUpdateUnixSeconds[anchorIndex] = nowTimestamp;

            return changed;
        }

        private void ApplyInventoryColdDurabilityDecay()
        {
            _coldDurabilityTickPhase ^= 1;
            if (_coldDurabilityTickPhase != 0 ||
                _grid == null ||
                !_stackCounts.IsCreated ||
                !_itemStateFlags.IsCreated ||
                !_anchorStateFlags.IsCreated ||
                !_qualityMilli.IsCreated ||
                !_durabilities.IsCreated)
            {
                return;
            }

            if (_durabilitySnapshotDirty)
                SyncDurabilityBytesFromQuality();

            int slotCount = math.min(
                math.min(math.min(_stackCounts.Length, _itemStateFlags.Length), _anchorStateFlags.Length),
                math.min(_qualityMilli.Length, _durabilities.Length));
            bool changed = false;
            for (int anchorIndex = 0; anchorIndex < slotCount; anchorIndex++)
            {
                if (_stackCounts[anchorIndex] == 0 || !_grid.HasAnchor(anchorIndex))
                    continue;

                ushort flags = _itemStateFlags[anchorIndex];
                if ((flags & DurabilityDecayEligibleMask) == 0)
                    continue;

                if ((_anchorStateFlags[anchorIndex] & CraftingLockedMask) != 0)
                    continue;

                byte durability = _durabilities[anchorIndex];
                if (durability == 0)
                    continue;

                byte nextDurability = (byte)(durability - 1);
                _durabilities[anchorIndex] = nextDurability;
                if (_itemDurability.IsCreated && (uint)anchorIndex < (uint)_itemDurability.Length)
                    _itemDurability[anchorIndex] = math.saturate(nextDurability * 0.01f);
                _qualityMilli[anchorIndex] = (ushort)(nextDurability * 10);
                if (nextDurability < DegradedDurabilityThreshold)
                    flags |= DegradedItemStateMask;

                if (nextDurability == 0)
                    flags |= (ushort)(BrokenItemStateMask | DegradedItemStateMask);

                _itemStateFlags[anchorIndex] = flags;
                changed = true;
            }

            if (changed)
                NotifyInventoryChanged(massDirty: false);
        }

        private void ApplyInventoryRadioactiveHalfLife()
        {
            if (_grid == null ||
                !_stackCounts.IsCreated ||
                !_itemStateFlags.IsCreated ||
                !_qualityMilli.IsCreated ||
                !_anchorUnitRadiationSv.IsCreated ||
                !_radioactiveConversionAnchors.IsCreated ||
                !_radioactiveHalfLifeCounters.IsCreated)
            {
                return;
            }

            // ZERO-GC INLINE KERNEL: bounded inventory SlowTick pass mutates only preallocated SOA state.
            using (_radioactiveHalfLifeProfilerMarker.Auto())
            {
                new InventoryRadioactiveHalfLifeKernel
                {
                    AnchorHashIds = _grid.AnchorHashIds,
                    StackCounts = _stackCounts,
                    AnchorUnitRadiationSv = _anchorUnitRadiationSv,
                    ItemStateFlags = _itemStateFlags,
                    QualityMilli = _qualityMilli,
                    ConversionAnchorIndices = _radioactiveConversionAnchors,
                    Counters = _radioactiveHalfLifeCounters,
                    DeltaSeconds = SlowTickIntervalSeconds,
                    BaseHalfLifeSeconds = RadioactiveHalfLifeBaseSeconds,
                    DefaultQuality = DefaultQualityMilli,
                    RadioactiveMask = RadioactiveItemStateMask,
                    DegradedMask = DegradedItemStateMask,
                    DegradedThreshold = DegradedQualityMilliThreshold
                }.Execute();
            }

            if (_radioactiveHalfLifeCounters.Length < 2 || _radioactiveHalfLifeCounters[1] == 0)
                return;

            int conversionCount = math.clamp(_radioactiveHalfLifeCounters[0], 0, _radioactiveConversionAnchors.Length);
            for (int i = 0; i < conversionCount; i++)
                TryConvertRadioactiveAnchorToDepletedLead(_radioactiveConversionAnchors[i]);

            NotifyInventoryChanged();
        }

        private void ApplyInventoryReactiveChemistry()
        {
            if (_grid == null ||
                !_stackCounts.IsCreated ||
                !_craftLockedCounts.IsCreated ||
                !_itemStateFlags.IsCreated ||
                !_thermalRunawayByAnchor.IsCreated ||
                !_thermalRunawayPairs.IsCreated ||
                !_thermalRunawayCounters.IsCreated)
            {
                return;
            }

            // ZERO-GC INLINE KERNEL: bounded SOA slot-adjacency pass mutates only preallocated thermal cache.
            using (_reactiveChemistryProfilerMarker.Auto())
            {
                new InventoryReactiveChemistryKernel
                {
                    AnchorHashIds = _grid.AnchorHashIds,
                    StackCounts = _stackCounts,
                    CraftLockedCounts = _craftLockedCounts,
                    ItemStateFlags = _itemStateFlags,
                    ThermalRunawayByAnchor = _thermalRunawayByAnchor,
                    RunawayPairs = _thermalRunawayPairs,
                    Counters = _thermalRunawayCounters,
                    Columns = columns,
                    Rows = rows,
                    DeltaSeconds = SlowTickIntervalSeconds,
                    RunawayPerSecond = ThermalRunawayPerSecond,
                    CooldownPerSecond = ThermalRunawayCooldownPerSecond,
                    RadioactiveMask = RadioactiveItemStateMask,
                    FlammableMask = FlammableItemStateMask
                }.Execute();
            }

            if (_thermalRunawayCounters.Length < 2)
                return;

            int pairCount = math.clamp(_thermalRunawayCounters[0], 0, _thermalRunawayPairs.Length);
            if (pairCount <= 0)
                return;

            int destroyedPairs = 0;
            for (int pairIndex = 0; pairIndex < pairCount; pairIndex++)
            {
                int2 pair = _thermalRunawayPairs[pairIndex];
                if (TryDestroyReactivePair(pair.x, pair.y))
                    destroyedPairs++;
            }

            if (destroyedPairs <= 0)
                return;

            DispatchInventoryThermalRunaway(destroyedPairs);
            NotifyInventoryChanged();
        }

        private bool TryDestroyReactivePair(int firstAnchorIndex, int secondAnchorIndex)
        {
            if (!IsReactiveAnchorStillValid(firstAnchorIndex) ||
                !IsReactiveAnchorStillValid(secondAnchorIndex))
            {
                return false;
            }

            int firstFlags = _itemStateFlags[firstAnchorIndex];
            int secondFlags = _itemStateFlags[secondAnchorIndex];
            bool firstRadioactive = (firstFlags & RadioactiveItemStateMask) != 0;
            bool firstFlammable = (firstFlags & FlammableItemStateMask) != 0;
            bool secondRadioactive = (secondFlags & RadioactiveItemStateMask) != 0;
            bool secondFlammable = (secondFlags & FlammableItemStateMask) != 0;
            if (!((firstRadioactive && secondFlammable) || (firstFlammable && secondRadioactive)))
                return false;

            bool destroyedSecond = DestroyInventoryAnchor(secondAnchorIndex);
            bool destroyedFirst = DestroyInventoryAnchor(firstAnchorIndex);
            return destroyedFirst | destroyedSecond;
        }

        private bool IsReactiveAnchorStillValid(int anchorIndex)
        {
            return _grid != null &&
                   _stackCounts.IsCreated &&
                   _itemStateFlags.IsCreated &&
                   (uint)anchorIndex < (uint)_stackCounts.Length &&
                   _grid.HasAnchor(anchorIndex) &&
                   _grid.GetAnchorHashId(anchorIndex) != 0 &&
                   _stackCounts[anchorIndex] > 0 &&
                   !IsCraftLockedFlagSet(anchorIndex);
        }

        private void DispatchInventoryThermalRunaway(int destroyedPairCount)
        {
            float damage = ThermalRunawayDamage * math.max(1, destroyedPairCount);
            if (survival != null)
                survival.TakeDamage(damage);

            global::Hecton8.Gameplay.HabitatDamageSignal signal = new global::Hecton8.Gameplay.HabitatDamageSignal
            {
                magnitude = damage,
                localPoint = float3.zero,
                damageType = (uint)(DamageTypeMask.Thermal | DamageTypeMask.Impact | DamageTypeMask.Radioactive),
                integrityDelta = byte.MaxValue,
                depth = ResolveInventoryCarrierDepthMeters(),
                sourceID = DamageSourceIds.InventoryRadiation
            };

            TraumaDispatcher dispatcher = ResolveTraumaDispatcher();
            if (dispatcher != null)
            {
                dispatcher.OnIntegrityChanged(1f, 0f, signal);
                dispatcher.OnTraumaThresholdCrossed(TraumaLevel.Critical);
            }

            if (GlobalRegistry.Audio is SpatialAudioManager spatialAudio)
                spatialAudio.QueueInventoryRunawayExplosion(transform.position, ThermalRunawayAudioVolume);
        }

        private void ApplyInventoryDepthPressureCrush()
        {
            if (_grid == null ||
                !_stackCounts.IsCreated ||
                !_itemStateFlags.IsCreated ||
                !_qualityMilli.IsCreated)
            {
                return;
            }

            float depthMeters = ResolveInventoryCarrierDepthMeters();
            if (!ShouldApplyDepthPressureCrush(depthMeters, ResolveInventoryPressurizedContainerProtection()))
                return;

            bool changed = false;
            float damageMilli = ResolveDepthPressureCrushDamageMilli(depthMeters);
            if (!(damageMilli > 0f))
                return;

            for (int anchorIndex = 0; anchorIndex < _stackCounts.Length; anchorIndex++)
            {
                if (!_grid.HasAnchor(anchorIndex) || IsCraftLockedFlagSet(anchorIndex))
                    continue;

                int itemHashId = _grid.GetAnchorHashId(anchorIndex);
                if (!TryGetRuntimeDescriptor(itemHashId, out ItemCatalog.ItemRuntimeDescriptor runtimeDescriptor) ||
                    !IsDepthPressureFragileItem(itemHashId, in runtimeDescriptor))
                {
                    continue;
                }

                if (ApplyPressureCrushDamageToAnchor(anchorIndex, damageMilli))
                    changed = true;
            }

            if (changed)
                NotifyInventoryChanged();
        }

        internal static bool ShouldApplyDepthPressureCrush(float depthMeters, bool hasPressurizedProtection)
        {
            return !hasPressurizedProtection && depthMeters > PressureCrushDepthMeters;
        }

        internal static float ResolveDepthPressureCrushDamageMilli(float depthMeters)
        {
            if (depthMeters <= PressureCrushDepthMeters)
                return 0f;

            float depthFactor = math.saturate((depthMeters - PressureCrushDepthMeters) * 0.001f);
            return PressureCrushDurabilityPerSecond * SlowTickIntervalSeconds * math.max(1f, depthFactor) * 1000f;
        }

        private bool ApplyPressureCrushDamageToAnchor(int anchorIndex, float damageMilli)
        {
            ushort currentQualityMilli = _qualityMilli[anchorIndex] > 0 ? _qualityMilli[anchorIndex] : DefaultQualityMilli;
            ushort nextQualityMilli = (ushort)math.clamp((int)math.round(currentQualityMilli - math.max(1f, damageMilli)), 0, 1000);
            if (nextQualityMilli <= 0)
                return DestroyInventoryAnchor(anchorIndex);

            if (nextQualityMilli == currentQualityMilli)
                return false;

            _qualityMilli[anchorIndex] = nextQualityMilli;
            if (nextQualityMilli < DegradedQualityMilliThreshold)
                _itemStateFlags[anchorIndex] |= DegradedItemStateMask;

            return true;
        }

        private void DispatchInventoryRadiationTrauma()
        {
            float threshold = ResolveInventoryRadiationThresholdSv();
            if (!(TotalRadiationSv > threshold))
                return;

            TraumaDispatcher dispatcher = ResolveTraumaDispatcher();
            if (dispatcher == null)
                return;

            float excess = TotalRadiationSv - threshold;
            float hazard01 = math.saturate(excess * math.rcp(math.max(0.01f, threshold)));
            if (hazard01 <= 0f)
                return;

            global::Hecton8.Gameplay.HabitatDamageSignal signal = new global::Hecton8.Gameplay.HabitatDamageSignal
            {
                magnitude = hazard01,
                localPoint = float3.zero,
                damageType = (uint)DamageTypeMask.Radioactive,
                integrityDelta = (byte)math.clamp((int)math.round(hazard01 * byte.MaxValue), 0, byte.MaxValue),
                depth = ResolveInventoryCarrierDepthMeters(),
                sourceID = DamageSourceIds.InventoryRadiation
            };

            dispatcher.OnClarityChanged(0f, hazard01, signal);
            dispatcher.OnTraumaThresholdCrossed(ResolveRadiationTraumaLevel(hazard01));
        }

        private bool TryConvertRadioactiveAnchorToDepletedLead(int anchorIndex)
        {
            if (_grid == null ||
                !_stackCounts.IsCreated ||
                !_itemStateFlags.IsCreated ||
                !_qualityMilli.IsCreated ||
                !_lastUpdateUnixSeconds.IsCreated ||
                (uint)anchorIndex >= (uint)_stackCounts.Length ||
                !_grid.TryGetAnchorDescriptor(anchorIndex, out InventoryGrid.InventoryItemDescriptor sourceDescriptor) ||
                !TryBuildDescriptor(_DepletedLeadHashId, out InventoryGrid.InventoryItemDescriptor depletedDescriptor) ||
                !TryGetRuntimeDescriptor(depletedDescriptor.HashId, out ItemCatalog.ItemRuntimeDescriptor depletedRuntimeDescriptor))
            {
                return false;
            }

            int stackCount = Mathf.Max(1, (int)_stackCounts[anchorIndex]);
            int anchorX = anchorIndex % columns;
            int anchorY = anchorIndex / columns;
            float sourceWeight = sourceDescriptor.Weight * stackCount;

            _grid.RemoveAnchorAt(anchorIndex);
            if (!_grid.PlaceAt(in depletedDescriptor, anchorX, anchorY))
            {
                _grid.PlaceAt(in sourceDescriptor, anchorX, anchorY);
                SyncAnchorPhysicalMetadata(anchorIndex, sourceDescriptor.HashId);
                return false;
            }

            ushort convertedStackCount = (ushort)Mathf.Clamp(stackCount, 1, depletedDescriptor.MaxStack);
            _stackCounts[anchorIndex] = convertedStackCount;
            _craftLockedCounts[anchorIndex] = 0;
            _anchorStateFlags[anchorIndex] = 0;
            _itemStateFlags[anchorIndex] = depletedRuntimeDescriptor.StateFlags;
            _itemGenetics[anchorIndex] = 0;
            _qualityMilli[anchorIndex] = DefaultQualityMilli;
            _lastUpdateUnixSeconds[anchorIndex] = 0u;
            SetAnchorPhysicalMetadata(
                anchorIndex,
                depletedRuntimeDescriptor.MassKg,
                depletedRuntimeDescriptor.VolumeM3,
                depletedRuntimeDescriptor.RadiationSvPerSecond);
            TotalWeight = Mathf.Max(0f, TotalWeight - sourceWeight + depletedDescriptor.Weight * convertedStackCount);
            return true;
        }

        private TraumaDispatcher ResolveTraumaDispatcher()
        {
            if (_traumaDispatcher != null)
                return _traumaDispatcher;

            if (survival != null)
                survival.TryGetComponent(out _traumaDispatcher);

            if (_traumaDispatcher == null)
                TryGetComponent(out _traumaDispatcher);

            return _traumaDispatcher;
        }

        private float ResolveInventoryRadiationThresholdSv()
        {
            if (survival != null && survival.Stats != null)
                return math.max(0.01f, survival.Stats.RadiationThreshold);

            return math.max(0.01f, radiationTraumaThresholdSv);
        }

        private static float ResolveInventoryCarrierDepthMeters()
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            HectonPlayerMovement movement = playerContext != null ? playerContext.PlayerMovement : null;
            return movement != null ? math.max(0f, movement.CurrentDepth) : 0f;
        }

        private static TraumaLevel ResolveRadiationTraumaLevel(float hazard01)
        {
            if (hazard01 >= 0.8f)
                return TraumaLevel.Catastrophic;

            if (hazard01 >= 0.55f)
                return TraumaLevel.Critical;

            if (hazard01 >= 0.3f)
                return TraumaLevel.Significant;

            return TraumaLevel.Minor;
        }

        private static bool ResolveInventoryCarrierSubmergedState()
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            HectonPlayerMovement movement = playerContext != null ? playerContext.PlayerMovement : null;
            return movement != null && movement.CurrentDepth > 0f;
        }

        private bool TryGetRuntimeDescriptor(int itemHashId, out ItemCatalog.ItemRuntimeDescriptor runtimeDescriptor)
        {
            runtimeDescriptor = default;
            return itemCatalog != null &&
                   itemHashId != 0 &&
                   itemCatalog.TryGetRuntimeDescriptor(itemHashId, out runtimeDescriptor);
        }

        private void ResolvePlayerImpactBodyId()
        {
            if (_playerImpactBodyId != 0ul)
                return;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            Rigidbody playerBody = playerContext != null ? playerContext.PlayerRigidbody : null;
            _playerImpactBodyId = playerBody != null ? EntityId.ToULong(playerBody.GetEntityId()) : 0ul;
        }

        void IPhysicsImpactEventListener.OnPhysicsImpact(in PhysicsImpactSignal impactSignal)
        {
            HandlePhysicsImpact(in impactSignal);
        }

        private void HandlePhysicsImpact(in PhysicsImpactSignal impactSignal)
        {
            ResolvePlayerImpactBodyId();
            if (_playerImpactBodyId == 0ul ||
                (impactSignal.PrimaryBodyId != _playerImpactBodyId && impactSignal.SecondaryBodyId != _playerImpactBodyId))
            {
                return;
            }

            float impactAccelerationG = EstimateImpactAccelerationInG(impactSignal);
            if (impactAccelerationG < KineticDamageThresholdG)
                return;

            ApplyKineticInventoryDamage();
        }

        private float EstimateImpactAccelerationInG(PhysicsImpactSignal impactSignal)
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            Rigidbody playerBody = playerContext != null ? playerContext.PlayerRigidbody : null;
            float playerMass = playerBody != null ? Mathf.Max(0.1f, playerBody.mass) : 80f;
            return math.max(0f, impactSignal.Force * math.rcp(playerMass * 9.81f));
        }

        private void ApplyKineticInventoryDamage()
        {
            if (_grid == null ||
                !_stackCounts.IsCreated ||
                !_itemStateFlags.IsCreated ||
                !_qualityMilli.IsCreated)
            {
                return;
            }

            bool changed = false;
            for (int anchorIndex = 0; anchorIndex < _stackCounts.Length; anchorIndex++)
            {
                if (!_grid.HasAnchor(anchorIndex))
                    continue;

                int itemHashId = _grid.GetAnchorHashId(anchorIndex);
                if (!TryGetRuntimeDescriptor(itemHashId, out ItemCatalog.ItemRuntimeDescriptor runtimeDescriptor) ||
                    !IsKineticFragileItem(itemHashId, in runtimeDescriptor))
                {
                    continue;
                }

                if (ApplyKineticDamageToAnchor(anchorIndex))
                    changed = true;
            }

            if (changed)
                NotifyInventoryChanged();
        }

        private bool IsKineticFragileItem(int itemHashId, in ItemCatalog.ItemRuntimeDescriptor runtimeDescriptor)
        {
            if (runtimeDescriptor.AudioMaterialId == (byte)ItemAudioMaterialId.Glass)
                return true;

            ItemData itemData = itemCatalog != null ? itemCatalog.FindByHash(itemHashId) : null;
            if (itemData != null)
            {
                if (itemData.resourceFamily == ResourceFamily.ElectronicsMetal ||
                    itemData.resourceFamily == ResourceFamily.Power)
                {
                    return true;
                }
            }

            return runtimeDescriptor.CategoryId == (byte)ItemCategory.Component ||
                   runtimeDescriptor.CategoryId == (byte)ItemCategory.Tool;
        }

        private bool IsDepthPressureFragileItem(int itemHashId, in ItemCatalog.ItemRuntimeDescriptor runtimeDescriptor)
        {
            if (IsDepthPressureFragileResource(runtimeDescriptor.AudioMaterialId, ResourceFamily.None))
                return true;

            ItemData itemData = itemCatalog != null ? itemCatalog.FindByHash(itemHashId) : null;
            return itemData != null && IsDepthPressureFragileResource(runtimeDescriptor.AudioMaterialId, itemData.resourceFamily);
        }

        internal static bool IsDepthPressureFragileResource(byte audioMaterialId, ResourceFamily resourceFamily)
        {
            return audioMaterialId == (byte)ItemAudioMaterialId.Glass ||
                   resourceFamily == ResourceFamily.ElectronicsMetal ||
                   resourceFamily == ResourceFamily.Power;
        }

        private bool ApplyKineticDamageToAnchor(int anchorIndex)
        {
            if (!_grid.TryGetAnchorDescriptor(anchorIndex, out InventoryGrid.InventoryItemDescriptor descriptor))
                return false;

            ushort currentQualityMilli = _qualityMilli[anchorIndex] > 0 ? _qualityMilli[anchorIndex] : DefaultQualityMilli;
            ushort nextQualityMilli = (ushort)(currentQualityMilli >> 1);

            if (nextQualityMilli <= 0)
            {
                int stackCount = Mathf.Max(1, (int)_stackCounts[anchorIndex]);
                _grid.RemoveAnchorAt(anchorIndex);
                _stackCounts[anchorIndex] = 0;
                _craftLockedCounts[anchorIndex] = 0;
                _anchorStateFlags[anchorIndex] = 0;
                _itemStateFlags[anchorIndex] = 0;
                _itemGenetics[anchorIndex] = 0;
                _qualityMilli[anchorIndex] = 0;
                if (_itemDurability.IsCreated && (uint)anchorIndex < (uint)_itemDurability.Length)
                    _itemDurability[anchorIndex] = 0f;
                if (_durabilities.IsCreated && (uint)anchorIndex < (uint)_durabilities.Length)
                    _durabilities[anchorIndex] = 0;
                _lastUpdateUnixSeconds[anchorIndex] = 0;
                ClearAnchorPhysicalMetadata(anchorIndex);
                TotalWeight = Mathf.Max(0f, TotalWeight - descriptor.Weight * stackCount);
                return true;
            }

            bool changed = nextQualityMilli != currentQualityMilli;
            if (!changed)
                return false;

            _qualityMilli[anchorIndex] = nextQualityMilli;
            if (_itemDurability.IsCreated && (uint)anchorIndex < (uint)_itemDurability.Length)
                _itemDurability[anchorIndex] = math.saturate(nextQualityMilli * 0.001f);
            if (_durabilities.IsCreated && (uint)anchorIndex < (uint)_durabilities.Length)
                _durabilities[anchorIndex] = (byte)math.clamp((nextQualityMilli + 5) / 10, 0, 100);
            if (nextQualityMilli < DegradedQualityMilliThreshold)
                _itemStateFlags[anchorIndex] |= DegradedItemStateMask;

            return true;
        }

        private bool DestroyInventoryAnchor(int anchorIndex)
        {
            if (_grid == null ||
                !_stackCounts.IsCreated ||
                !_craftLockedCounts.IsCreated ||
                !_anchorStateFlags.IsCreated ||
                !_itemStateFlags.IsCreated ||
                !_itemGenetics.IsCreated ||
                !_qualityMilli.IsCreated ||
                !_lastUpdateUnixSeconds.IsCreated ||
                !_grid.TryGetAnchorDescriptor(anchorIndex, out InventoryGrid.InventoryItemDescriptor descriptor))
            {
                return false;
            }

            int stackCount = Mathf.Max(1, (int)_stackCounts[anchorIndex]);
            // InventoryGrid.RemoveAnchorAt clears the SOA ItemHashID before trauma/audio dispatch can read the slot again.
            _grid.RemoveAnchorAt(anchorIndex);
            ClearDestroyedAnchorRuntimeState(anchorIndex);
            TotalWeight = Mathf.Max(0f, TotalWeight - descriptor.Weight * stackCount);
            return true;
        }

        private void ClearDestroyedAnchorRuntimeState(int anchorIndex)
        {
            _stackCounts[anchorIndex] = 0;
            _craftLockedCounts[anchorIndex] = 0;
            _anchorStateFlags[anchorIndex] = 0;
            _itemStateFlags[anchorIndex] = 0;
            _itemGenetics[anchorIndex] = 0;
            _qualityMilli[anchorIndex] = 0;
            if (_itemDurability.IsCreated && (uint)anchorIndex < (uint)_itemDurability.Length)
                _itemDurability[anchorIndex] = 0f;
            if (_durabilities.IsCreated && (uint)anchorIndex < (uint)_durabilities.Length)
                _durabilities[anchorIndex] = 0;
            _lastUpdateUnixSeconds[anchorIndex] = 0;
            if (_thermalRunawayByAnchor.IsCreated && (uint)anchorIndex < (uint)_thermalRunawayByAnchor.Length)
                _thermalRunawayByAnchor[anchorIndex] = 0f;
            ClearAnchorPhysicalMetadata(anchorIndex);
        }

        private bool ResolveInventoryPressurizedContainerProtection()
        {
            return HasPressurizedContainerProtection;
        }

        private void ClearCraftReservationState()
        {
            ClearNativeArray(_craftLockedCounts);
            ClearNativeArray(_anchorStateFlags);
        }

        private void SyncAnchorPhysicalMetadata(int anchorIndex, int itemHashId)
        {
            if (!TryGetRuntimeDescriptor(itemHashId, out ItemCatalog.ItemRuntimeDescriptor runtimeDescriptor))
            {
                ClearAnchorPhysicalMetadata(anchorIndex);
                return;
            }

            SetAnchorPhysicalMetadata(anchorIndex, runtimeDescriptor.MassKg, runtimeDescriptor.VolumeM3, runtimeDescriptor.RadiationSvPerSecond);
        }

        private void SetAnchorPhysicalMetadata(int anchorIndex, float massKg, float volumeM3, float radiationSv)
        {
            if (!_anchorUnitMassKg.IsCreated ||
                !_anchorUnitVolumeM3.IsCreated ||
                !_anchorUnitRadiationSv.IsCreated ||
                (uint)anchorIndex >= (uint)_anchorUnitMassKg.Length ||
                (uint)anchorIndex >= (uint)_anchorUnitVolumeM3.Length ||
                (uint)anchorIndex >= (uint)_anchorUnitRadiationSv.Length)
            {
                return;
            }

            _anchorUnitMassKg[anchorIndex] = Mathf.Max(0f, massKg);
            _anchorUnitVolumeM3[anchorIndex] = Mathf.Max(0f, volumeM3);
            _anchorUnitRadiationSv[anchorIndex] = Mathf.Max(0f, radiationSv);
        }

        private void ClearAnchorPhysicalMetadata(int anchorIndex)
        {
            if (!_anchorUnitMassKg.IsCreated ||
                !_anchorUnitVolumeM3.IsCreated ||
                !_anchorUnitRadiationSv.IsCreated ||
                (uint)anchorIndex >= (uint)_anchorUnitMassKg.Length ||
                (uint)anchorIndex >= (uint)_anchorUnitVolumeM3.Length ||
                (uint)anchorIndex >= (uint)_anchorUnitRadiationSv.Length)
            {
                return;
            }

            _anchorUnitMassKg[anchorIndex] = 0f;
            _anchorUnitVolumeM3[anchorIndex] = 0f;
            _anchorUnitRadiationSv[anchorIndex] = 0f;
            if (_thermalRunawayByAnchor.IsCreated && (uint)anchorIndex < (uint)_thermalRunawayByAnchor.Length)
                _thermalRunawayByAnchor[anchorIndex] = 0f;
        }

        private void SyncDurabilityBytesFromQuality()
        {
            if (!_durabilitySnapshotDirty ||
                _grid == null ||
                !_qualityMilli.IsCreated ||
                !_itemDurability.IsCreated ||
                !_durabilities.IsCreated)
            {
                return;
            }

            int count = math.min(math.min(_qualityMilli.Length, _itemDurability.Length), _durabilities.Length);
            for (int anchorIndex = 0; anchorIndex < count; anchorIndex++)
            {
                if (!_grid.HasAnchor(anchorIndex))
                {
                    _durabilities[anchorIndex] = 0;
                    _itemDurability[anchorIndex] = 0f;
                    continue;
                }

                ushort qualityMilli = _qualityMilli[anchorIndex] > 0 ? _qualityMilli[anchorIndex] : DefaultQualityMilli;
                float durability01 = math.saturate(_itemDurability[anchorIndex]);
                if (durability01 <= 0f && (_itemStateFlags.IsCreated == false || (uint)anchorIndex >= (uint)_itemStateFlags.Length || (_itemStateFlags[anchorIndex] & BrokenItemStateMask) == 0))
                {
                    durability01 = math.saturate(qualityMilli * 0.001f);
                    _itemDurability[anchorIndex] = durability01;
                }

                _durabilities[anchorIndex] = (byte)math.clamp((int)math.round(durability01 * 100f), 0, 100);
            }

            _durabilitySnapshotDirty = false;
        }

        private static uint ResolveCurrentUnixTimestamp()
        {
            long utcNowSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (utcNowSeconds <= 0L)
                return 0u;

            return utcNowSeconds >= uint.MaxValue ? uint.MaxValue : (uint)utcNowSeconds;
        }

        private static ushort ResolveLoadedQualityMilli(InventoryDTO dto, int index)
        {
            if (dto.qualityMilli == null || (uint)index >= (uint)dto.qualityMilli.Length)
                return DefaultQualityMilli;

            return dto.qualityMilli[index] > 0 ? dto.qualityMilli[index] : DefaultQualityMilli;
        }

        private static ushort NormalizeQualityMilli(ushort qualityMilli)
        {
            if (qualityMilli == 0)
                return DefaultQualityMilli;

            return (ushort)Mathf.Clamp((int)qualityMilli, 0, DefaultQualityMilli);
        }

        private static uint ResolveLoadedTimestamp(InventoryDTO dto, int index)
        {
            if (dto.lastUpdateUnixSeconds == null || (uint)index >= (uint)dto.lastUpdateUnixSeconds.Length)
                return 0u;

            return dto.lastUpdateUnixSeconds[index];
        }

        private static ushort ResolveLoadedItemStateFlags(InventoryDTO dto, int index, ushort fallbackFlags)
        {
            if (dto.itemStateFlags == null || (uint)index >= (uint)dto.itemStateFlags.Length)
                return fallbackFlags;

            ushort savedFlags = dto.itemStateFlags[index];
            return savedFlags != 0 ? savedFlags : fallbackFlags;
        }

        private static byte ResolveLoadedGeneticsMask(InventoryDTO dto, int index)
        {
            if (dto.itemGeneticsWords == null || (uint)index >= (uint)dto.itemGeneticsWords.Length)
                return 0;

            return SanitizeItemGeneticsFlags(dto.itemGeneticsWords[index]);
        }

        private static byte CompressItemGenetics(ulong geneticsMask)
        {
            byte flags = 0;
            if ((geneticsMask & LegacyGlowGeneMask) != 0UL)
                flags |= (byte)ItemGeneticFlags.Glow;
            if ((geneticsMask & LegacyToxicGeneMask) != 0UL)
                flags |= (byte)ItemGeneticFlags.Toxic;
            if ((geneticsMask & LegacyEdibleGeneMask) != 0UL)
                flags |= (byte)ItemGeneticFlags.Edible;
            if ((geneticsMask & LegacyHarvestableGeneMask) != 0UL)
                flags |= (byte)ItemGeneticFlags.Harvestable;

            return flags;
        }

        private static byte SanitizeItemGeneticsFlags(byte geneticsFlags)
        {
            return (byte)(geneticsFlags & ItemGeneticsSupportedFlagsMask);
        }

        private static ulong ExpandItemGenetics(byte geneticsFlags)
        {
            byte sanitizedFlags = SanitizeItemGeneticsFlags(geneticsFlags);
            ulong geneticsMask = 0UL;
            if ((sanitizedFlags & (byte)ItemGeneticFlags.Glow) != 0)
                geneticsMask |= LegacyGlowGeneMask;
            if ((sanitizedFlags & (byte)ItemGeneticFlags.Toxic) != 0)
                geneticsMask |= LegacyToxicGeneMask;
            if ((sanitizedFlags & (byte)ItemGeneticFlags.Edible) != 0)
                geneticsMask |= LegacyEdibleGeneMask;
            if ((sanitizedFlags & (byte)ItemGeneticFlags.Harvestable) != 0)
                geneticsMask |= LegacyHarvestableGeneMask;

            return geneticsMask;
        }

        private void ApplyLoadedBiologicalDecay(int anchorIndex)
        {
            if (!_itemStateFlags.IsCreated ||
                !_qualityMilli.IsCreated ||
                !_lastUpdateUnixSeconds.IsCreated ||
                (uint)anchorIndex >= (uint)_itemStateFlags.Length ||
                (_itemStateFlags[anchorIndex] & BiologicalItemStateMask) == 0)
            {
                return;
            }

            uint nowTimestamp = ResolveCurrentUnixTimestamp();
            uint lastTimestamp = _lastUpdateUnixSeconds[anchorIndex];
            if (lastTimestamp == 0u)
            {
                _lastUpdateUnixSeconds[anchorIndex] = nowTimestamp;
                if (_qualityMilli[anchorIndex] == 0)
                    _qualityMilli[anchorIndex] = DefaultQualityMilli;
                return;
            }

            float ambientTemperature = survival != null ? survival.EnvironmentTemperature : 2f;
            float tempFactor = ApproximateExpSigned((ambientTemperature - 4f) * 0.05f);
            uint elapsedSeconds = nowTimestamp >= lastTimestamp ? nowTimestamp - lastTimestamp : 0u;
            float currentQuality = math.clamp((_qualityMilli[anchorIndex] > 0 ? _qualityMilli[anchorIndex] : DefaultQualityMilli) * 0.001f, 0f, 1f);
            float decayedQuality = math.clamp(currentQuality - (elapsedSeconds * 0.001f * tempFactor), 0f, 1f);
            _qualityMilli[anchorIndex] = (ushort)math.clamp((int)math.round(decayedQuality * 1000f), 0, 1000);
            _lastUpdateUnixSeconds[anchorIndex] = nowTimestamp;
        }

        private void ReleaseCraftReservationsRange(CraftReservation[] reservations, int startIndex, int endExclusive)
        {
            if (reservations == null || !_craftLockedCounts.IsCreated || !_anchorStateFlags.IsCreated)
                return;

            int max = Mathf.Min(endExclusive, reservations.Length);
            for (int i = startIndex; i < max; i++)
            {
                CraftReservation reservation = reservations[i];
                int anchorIndex = reservation.AnchorIndex;
                if ((uint)anchorIndex < (uint)_craftLockedCounts.Length && reservation.Quantity > 0)
                {
                    _craftLockedCounts[anchorIndex] = (ushort)Mathf.Max(0, _craftLockedCounts[anchorIndex] - reservation.Quantity);
                    if (_craftLockedCounts[anchorIndex] == 0)
                        _anchorStateFlags[anchorIndex] = (ushort)(_anchorStateFlags[anchorIndex] & ~CraftingLockedMask);
                }

                reservations[i] = default;
            }
        }

        private static float ApproximateExpNegPositiveInput(float x)
        {
            x = math.max(0f, x);
            float x2 = x * x;
            return math.saturate(math.rcp(1f + x + (0.48f * x2) + (0.235f * x2 * x)));
        }

        private static float ApproximateExpSigned(float x)
        {
            return x < 0f
                ? ApproximateExpNegPositiveInput(-x)
                : math.rcp(ApproximateExpNegPositiveInput(math.min(x, 4f)));
        }

        private bool IsValidCraftReservation(in CraftReservation reservation)
        {
            if (_grid == null || !_stackCounts.IsCreated || reservation.Quantity <= 0 || (uint)reservation.AnchorIndex >= (uint)_stackCounts.Length)
                return false;

            if (!_grid.HasAnchor(reservation.AnchorIndex) || _grid.GetAnchorHashId(reservation.AnchorIndex) != reservation.ItemHashId)
                return false;

            if (GetReservedCraftCount(reservation.AnchorIndex) < reservation.Quantity)
                return false;

            return Mathf.Max(1, (int)_stackCounts[reservation.AnchorIndex]) >= reservation.Quantity;
        }

        private static unsafe void ClearNativeArray(NativeArray<ushort> array)
        {
            if (!array.IsCreated)
                return;

            void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(array);
            UnsafeUtility.MemClear(destinationPtr, array.Length * UnsafeUtility.SizeOf<ushort>());
        }

        private static unsafe void ClearNativeArray(NativeArray<uint> array)
        {
            if (!array.IsCreated)
                return;

            void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(array);
            UnsafeUtility.MemClear(destinationPtr, array.Length * UnsafeUtility.SizeOf<uint>());
        }

        private static unsafe void ClearNativeArray(NativeArray<byte> array)
        {
            if (!array.IsCreated)
                return;

            void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(array);
            UnsafeUtility.MemClear(destinationPtr, array.Length * UnsafeUtility.SizeOf<byte>());
        }

        private static unsafe void ClearNativeArray(NativeArray<float> array)
        {
            if (!array.IsCreated)
                return;

            void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(array);
            UnsafeUtility.MemClear(destinationPtr, array.Length * UnsafeUtility.SizeOf<float>());
        }

        private static unsafe void CopyNativeArray(NativeArray<ushort> source, NativeArray<ushort> destination)
        {
            if (!source.IsCreated || !destination.IsCreated)
                return;

            int copyLength = math.min(source.Length, destination.Length);
            if (copyLength <= 0)
                return;

            void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(source);
            void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(destination);
            int copyBytes = copyLength * UnsafeUtility.SizeOf<ushort>();
            int destinationBytes = destination.Length * UnsafeUtility.SizeOf<ushort>();
            if (!UnsafeMemoryCopyGuard.TryMemCpy(destinationPtr, destinationBytes, sourcePtr, copyBytes))
                UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(PlayerInventory));
        }
    }
}
