// ============================================================================
// HECTON-8 - PlayerInventory.cs
// Native SOA-backed inventory owner. Managed ItemData resolution is seam-only.
// ============================================================================

namespace Hecton8.Inventory
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Hecton.Localization;
    using Hecton8.Audio;
    using Hecton8.Core;
    using Hecton8.Core.Contracts;
    using Hecton8.Core.Contracts.Signals;
    using Hecton8.Core.Memory;
    using Hecton8.Gameplay;
    using Hecton8.Interaction;
    using Hecton8.Inventory.Algorithms;
    using Hecton8.Inventory.Corrosion;
    using Hecton8.Inventory.Corrosion.Contracts;
    using Hecton8.Items;
    using Hecton8.SaveSystem;
    using Hecton8.World;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using Unity.Mathematics;
    using Unity.Profiling;
    using UnityEngine;

    [DisallowMultipleComponent]
    public partial class PlayerInventory : MonoBehaviour, ISaveable, ISlowTickable, ILateFrameTickable, IPhysicsImpactEventListener, IGlobalRegistryHotSwapListener, IMappedInventoryWriteCommitSink
    {
        private static int _signalPushDropCount;
        private const ushort CraftingLockedMask = ItemRuntimeStateFlags.CraftingLocked;
        private const ushort RadioactiveItemStateMask = ItemRuntimeStateFlags.Radioactive;
        private const ushort BiologicalItemStateMask = ItemRuntimeStateFlags.Biological;
        internal const ushort DegradedItemStateMask = ItemRuntimeStateFlags.Degraded;
        private const ushort RustedItemStateMask = ItemRuntimeStateFlags.Rusted;
        private const ushort FlammableItemStateMask = ItemRuntimeStateFlags.Flammable;
        private const ushort BrokenItemStateMask = ItemRuntimeStateFlags.Broken;
        private const ushort DurabilityDecayEligibleMask = BiologicalItemStateMask | RustedItemStateMask | RadioactiveItemStateMask;
        private const ushort DefaultQualityMilli = SaveData.InventoryDefaultQualityMilli;
        internal const ushort DegradedQualityMilliThreshold = 250;
        private const byte DegradedDurabilityThreshold = DegradedQualityMilliThreshold / 10;
        private const float SlowTickIntervalSeconds = 0.5f;
        private const float OrganicDecayPerSecond = 0.00045f;
        private const float SubmergedOrganicDecayPerSecond = 0.00075f;
        private const float SubmergedMetalRustPerSecond = 0.00065f;
        private const float ThermalRunawayPerSecond = 0.65f;
        private const float ThermalRunawayCooldownPerSecond = 0.2f;
        private const float ThermalRunawayDamage = 50f;
        private const float ThermalRunawayBurnDurationSeconds = 6f;
        private const float ThermalRunawayRadiationDurationSeconds = 10f;
        private const float ThermalRunawayRadiationDoseScale = 0.08f;
        private const byte ThermalRunawayRadiationDoseKind = 7;
        private const float ThermalRunawayAudioVolume = 0.72f;
        private const float PressureCrushDepthMeters = 2000f;
        private const float PressureCrushDurabilityPerSecond = 0.08f;
        private const float RadioactiveHalfLifeBaseSeconds = 1800f;
        private const float Ln2 = 0.6931471805599453f;
        private const float KineticDamageThresholdG = 50f;
        private const double KineticInventoryImpactRadiusMeters = 2.25;
        private const float PlayerEquivalentMassKg = 80f;
        private const float InventoryLoadMinimumMovementMultiplier = 0.5f;
        private const float VolumeM3ToLiters = 1000f;
        private const float HeavyBulkTransferAudioThresholdKg = 50f;
        private const int InventoryBlackBoxCapacity = 300;
        private const int InventoryBlackBoxEntrySizeBytes = 64;
        private const int InventoryBlackBoxDumpHeaderBytes = 32;
        private const uint InventoryBlackBoxDumpVersion = 1u;
        private const uint InventoryBlackBoxDumpMagic = 0x494E5638u;
        private const uint SalinityCorrosionBlackBoxDumpMagic = 0x53434F52u;
        private const string InventoryBlackBoxDumpRelativePath = "Docs/AgentLogs/Dump_INVENTORY_BLACKBOX.bin";
        private const int PendingScavengingItemSignalCapacity = ItemAcquiredSignal.ExpectedCapacity;
        private const int PendingInventoryCommandSignalCapacity = 16;
        private const float SalinityCorrosionFrostTickSeconds = 5f;
        private const float SalinityCorrosionDegradationRatePerFrostTick = 0.00325f;
        private const float EquipmentFailingThreshold01 = 0.2f;
        private const float EquipmentFailingResetThreshold01 = 0.25f;
        private const int SalinityCorrosionBlackBoxEntrySizeBytes = 64;
        private const string SalinityCorrosionBlackBoxDumpRelativePath = "Docs/AgentLogs/Dump_SALINITY_CORROSION_SYSTEM.bin";
        private const string NativeMemoryOwner = nameof(PlayerInventory);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;
        private const int InventoryShadowBufferBytes = SaveData.InventoryShadowPayloadMaxBytes;
        private const uint Fnv1a32Offset = SaveData.InventoryShadowPayloadHashSeed;
        private const uint Fnv1a32Prime = SaveData.InventoryShadowPayloadHashPrime;
        private const byte ItemGeneticsSupportedFlagsMask = SaveData.InventoryItemGeneticsSupportedFlagsMask;
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
        private static readonly uint _postSimulationSystemHash = Hecton8.Data.H8DataHash.ComputeFnv1A32("player_inventory_post_simulation");
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

        // Size is bound to the SAME constant the layout guard asserts against. It used to be a bare literal
        // 64 while ValidateInventoryMemorySovereigntyLayouts1317 checked it against
        // SalinityCorrosionBlackBoxEntrySizeBytes - two numbers that had to agree with nothing forcing them
        // to. That asymmetry (InventoryTelemetryEntry above was already bound to its constant) was the only
        // live way this struct could drift out from under the guard and fail-close the whole inventory:
        // editing the constant alone would not move the struct, and editing the struct alone would not move
        // the constant. Now one edit moves both.
        [System.Runtime.InteropServices.StructLayout(
            System.Runtime.InteropServices.LayoutKind.Explicit,
            Size = SalinityCorrosionBlackBoxEntrySizeBytes)]
        private struct SalinityCorrosionTelemetryEntry
        {
            [System.Runtime.InteropServices.FieldOffset(0)]
            public uint Frame;
            [System.Runtime.InteropServices.FieldOffset(4)]
            public uint InventoryVersion;
            [System.Runtime.InteropServices.FieldOffset(8)]
            public float AverageEquipmentDurability01;
            [System.Runtime.InteropServices.FieldOffset(12)]
            public float RustScalar01;
            [System.Runtime.InteropServices.FieldOffset(16)]
            public float SalinityFactor;
            [System.Runtime.InteropServices.FieldOffset(20)]
            public uint CurrentBiomeHash;
            [System.Runtime.InteropServices.FieldOffset(24)]
            public uint InventoryMaskLow;
            [System.Runtime.InteropServices.FieldOffset(28)]
            public int Flags;
            [System.Runtime.InteropServices.FieldOffset(32)]
            private byte _pad0;
            [System.Runtime.InteropServices.FieldOffset(33)]
            private byte _pad1;
            [System.Runtime.InteropServices.FieldOffset(34)]
            private byte _pad2;
            [System.Runtime.InteropServices.FieldOffset(35)]
            private byte _pad3;
            [System.Runtime.InteropServices.FieldOffset(36)]
            private byte _pad4;
            [System.Runtime.InteropServices.FieldOffset(37)]
            private byte _pad5;
            [System.Runtime.InteropServices.FieldOffset(38)]
            private byte _pad6;
            [System.Runtime.InteropServices.FieldOffset(39)]
            private byte _pad7;
            [System.Runtime.InteropServices.FieldOffset(40)]
            private byte _pad8;
            [System.Runtime.InteropServices.FieldOffset(41)]
            private byte _pad9;
            [System.Runtime.InteropServices.FieldOffset(42)]
            private byte _pad10;
            [System.Runtime.InteropServices.FieldOffset(43)]
            private byte _pad11;
            [System.Runtime.InteropServices.FieldOffset(44)]
            private byte _pad12;
            [System.Runtime.InteropServices.FieldOffset(45)]
            private byte _pad13;
            [System.Runtime.InteropServices.FieldOffset(46)]
            private byte _pad14;
            [System.Runtime.InteropServices.FieldOffset(47)]
            private byte _pad15;
            [System.Runtime.InteropServices.FieldOffset(48)]
            private byte _pad16;
            [System.Runtime.InteropServices.FieldOffset(49)]
            private byte _pad17;
            [System.Runtime.InteropServices.FieldOffset(50)]
            private byte _pad18;
            [System.Runtime.InteropServices.FieldOffset(51)]
            private byte _pad19;
            [System.Runtime.InteropServices.FieldOffset(52)]
            private byte _pad20;
            [System.Runtime.InteropServices.FieldOffset(53)]
            private byte _pad21;
            [System.Runtime.InteropServices.FieldOffset(54)]
            private byte _pad22;
            [System.Runtime.InteropServices.FieldOffset(55)]
            private byte _pad23;
            [System.Runtime.InteropServices.FieldOffset(56)]
            private byte _pad24;
            [System.Runtime.InteropServices.FieldOffset(57)]
            private byte _pad25;
            [System.Runtime.InteropServices.FieldOffset(58)]
            private byte _pad26;
            [System.Runtime.InteropServices.FieldOffset(59)]
            private byte _pad27;
            [System.Runtime.InteropServices.FieldOffset(60)]
            private byte _pad28;
            [System.Runtime.InteropServices.FieldOffset(61)]
            private byte _pad29;
            [System.Runtime.InteropServices.FieldOffset(62)]
            private byte _pad30;
            [System.Runtime.InteropServices.FieldOffset(63)]
            private byte _pad31;
        }

        private struct PendingInventoryCommand
        {
            public InventoryCommandSignal Command;
            public double3 DeferredDeathAup;
            public byte HasDeferredDeathAup;
        }

        private ref struct InventoryMassVolumeKernel
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
                    float active = math.select(1f, 0f, AnchorHashIds[anchorIndex] == 0);
                    int stackCount = math.max(1, (int)StackCounts[anchorIndex]);
                    float weightedStackCount = stackCount * active;
                    totalMassKg += AnchorUnitMassKg[anchorIndex] * weightedStackCount;
                    totalVolumeM3 += AnchorUnitVolumeM3[anchorIndex] * weightedStackCount;
                    totalRadiationSv += AnchorUnitRadiationSv[anchorIndex] * weightedStackCount;
                }

                float3 totals = default;
                totals.x = math.max(0f, totalMassKg);
                totals.y = math.max(0f, totalVolumeM3);
                totals.z = math.max(0f, totalRadiationSv);
                Totals[0] = totals;
            }
        }

        private ref struct InventoryRadioactiveHalfLifeKernel
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
                int conversionCapacity = ConversionAnchorIndices.Length;

                if (conversionCapacity > 0)
                {
                    for (int anchorIndex = 0; anchorIndex < count; anchorIndex++)
                    {
                        ushort originalFlags = ItemStateFlags[anchorIndex];
                        ushort originalQualityMilli = QualityMilli[anchorIndex];
                        float radiationSv = AnchorUnitRadiationSv[anchorIndex];
                        bool active = AnchorHashIds[anchorIndex] != 0 &
                                      StackCounts[anchorIndex] != 0 &
                                      radiationSv > 0f;
                        ushort effectiveQualityMilli = (ushort)math.select((int)DefaultQuality, (int)originalQualityMilli, originalQualityMilli > 0);
                        float currentQuality = math.clamp(effectiveQualityMilli * 0.001f, 0f, 1f);
                        float radiationFactor = math.max(0.001f, radiationSv) * inverseBaseHalfLifeSeconds;
                        float decayFactor = ApproximateExpNegPositiveInput(Ln2 * radiationFactor * DeltaSeconds);
                        float nextQuality = math.clamp(currentQuality * decayFactor, 0f, 1f);
                        ushort decayedQualityMilli = (ushort)math.clamp((int)math.round(nextQuality * 1000f), 0, 1000);
                        ushort nextQualityMilli = (ushort)math.select((int)originalQualityMilli, (int)decayedQualityMilli, active);
                        ushort radioactiveFlags = (ushort)(originalFlags | RadioactiveMask);
                        bool degraded = active & nextQualityMilli < DegradedThreshold;
                        bool depleted = active & nextQualityMilli == 0;
                        ushort degradedFlags = (ushort)(radioactiveFlags | DegradedMask);
                        ushort nextFlags = (ushort)math.select((int)radioactiveFlags, (int)degradedFlags, degraded | depleted);
                        nextFlags = (ushort)math.select((int)originalFlags, (int)nextFlags, active);
                        bool didChange = active & (nextFlags != originalFlags | nextQualityMilli != originalQualityMilli);
                        ItemStateFlags[anchorIndex] = (ushort)math.select((int)originalFlags, (int)nextFlags, didChange);
                        QualityMilli[anchorIndex] = (ushort)math.select((int)originalQualityMilli, (int)nextQualityMilli, didChange);

                        bool canWriteConversion = depleted & conversionCount < conversionCapacity;
                        int conversionIndex = math.min(conversionCount, conversionCapacity - 1);
                        int previousConversion = ConversionAnchorIndices[conversionIndex];
                        ConversionAnchorIndices[conversionIndex] = math.select(previousConversion, anchorIndex, canWriteConversion);
                        conversionCount += math.select(0, 1, canWriteConversion);
                        changed |= math.select(0, 1, didChange);
                    }
                }
                else
                {
                    for (int anchorIndex = 0; anchorIndex < count; anchorIndex++)
                    {
                        ushort originalFlags = ItemStateFlags[anchorIndex];
                        ushort originalQualityMilli = QualityMilli[anchorIndex];
                        float radiationSv = AnchorUnitRadiationSv[anchorIndex];
                        bool active = AnchorHashIds[anchorIndex] != 0 &
                                      StackCounts[anchorIndex] != 0 &
                                      radiationSv > 0f;
                        ushort effectiveQualityMilli = (ushort)math.select((int)DefaultQuality, (int)originalQualityMilli, originalQualityMilli > 0);
                        float currentQuality = math.clamp(effectiveQualityMilli * 0.001f, 0f, 1f);
                        float radiationFactor = math.max(0.001f, radiationSv) * inverseBaseHalfLifeSeconds;
                        float decayFactor = ApproximateExpNegPositiveInput(Ln2 * radiationFactor * DeltaSeconds);
                        float nextQuality = math.clamp(currentQuality * decayFactor, 0f, 1f);
                        ushort decayedQualityMilli = (ushort)math.clamp((int)math.round(nextQuality * 1000f), 0, 1000);
                        ushort nextQualityMilli = (ushort)math.select((int)originalQualityMilli, (int)decayedQualityMilli, active);
                        ushort radioactiveFlags = (ushort)(originalFlags | RadioactiveMask);
                        bool degraded = active & nextQualityMilli < DegradedThreshold;
                        bool depleted = active & nextQualityMilli == 0;
                        ushort degradedFlags = (ushort)(radioactiveFlags | DegradedMask);
                        ushort nextFlags = (ushort)math.select((int)radioactiveFlags, (int)degradedFlags, degraded | depleted);
                        nextFlags = (ushort)math.select((int)originalFlags, (int)nextFlags, active);
                        bool didChange = active & (nextFlags != originalFlags | nextQualityMilli != originalQualityMilli);
                        ItemStateFlags[anchorIndex] = (ushort)math.select((int)originalFlags, (int)nextFlags, didChange);
                        QualityMilli[anchorIndex] = (ushort)math.select((int)originalQualityMilli, (int)nextQualityMilli, didChange);
                        changed |= math.select(0, 1, didChange);
                    }
                }
                if (Counters.Length >= 2)
                {
                    Counters[0] = conversionCount;
                    Counters[1] = changed;
                }
            }
        }

        private ref struct InventoryReactiveChemistryKernel
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
                if (slotCount <= 0 || !(DeltaSeconds > 0f))
                    return;

                int pairCount = 0;
                int changed = 0;
                float heatDelta = math.max(0f, RunawayPerSecond) * DeltaSeconds;
                float cooldownDelta = math.max(0f, CooldownPerSecond) * DeltaSeconds;
                int runawayCapacity = RunawayPairs.Length;

                if (runawayCapacity > 0)
                {
                    for (int anchorIndex = 0; anchorIndex < slotCount; anchorIndex++)
                    {
                        bool active = IsReactiveCandidateBranchless(anchorIndex, slotCount);
                        int rightCandidate = math.min(anchorIndex + 1, slotCount - 1);
                        int downCandidate = math.min(anchorIndex + safeColumns, slotCount - 1);
                        bool rightInRow = (anchorIndex % safeColumns) < (safeColumns - 1);
                        bool hasRight = active &
                                        rightInRow &
                                        rightCandidate != anchorIndex &
                                        IsReactivePairBranchless(anchorIndex, rightCandidate, slotCount);
                        bool hasDown = active &
                                       downCandidate != anchorIndex &
                                       IsReactivePairBranchless(anchorIndex, downCandidate, slotCount);
                        bool hasPair = hasRight | hasDown;
                        int adjacentAnchor = math.select(downCandidate, rightCandidate, hasRight);
                        float previousRunaway = ThermalRunawayByAnchor[anchorIndex];
                        float heatedRunaway = previousRunaway + heatDelta;
                        float cooledRunaway = math.max(0f, previousRunaway - cooldownDelta);
                        float nextRunaway = math.select(cooledRunaway, heatedRunaway, hasPair);
                        float storedRunaway = math.min(1.25f, nextRunaway);
                        ThermalRunawayByAnchor[anchorIndex] = storedRunaway;
                        bool didChange = storedRunaway != previousRunaway;
                        bool canWritePair = hasPair & nextRunaway > 1f & pairCount < runawayCapacity;
                        int pairIndex = math.min(pairCount, runawayCapacity - 1);
                        int2 previousPair = RunawayPairs[pairIndex];
                        int2 nextPair = default;
                        nextPair.x = anchorIndex;
                        nextPair.y = adjacentAnchor;
                        RunawayPairs[pairIndex] = math.select(previousPair, nextPair, canWritePair);
                        pairCount += math.select(0, 1, canWritePair);
                        changed |= math.select(0, 1, didChange);
                    }
                }
                else
                {
                    for (int anchorIndex = 0; anchorIndex < slotCount; anchorIndex++)
                    {
                        float previousRunaway = ThermalRunawayByAnchor[anchorIndex];
                        float storedRunaway = math.max(0f, previousRunaway - cooldownDelta);
                        ThermalRunawayByAnchor[anchorIndex] = storedRunaway;
                        changed |= math.select(0, 1, storedRunaway != previousRunaway);
                    }
                }

                if (Counters.Length >= 2)
                {
                    Counters[0] = pairCount;
                    Counters[1] = changed;
                }
            }

            private bool IsReactiveCandidateBranchless(int anchorIndex, int slotCount)
            {
                return (uint)anchorIndex < (uint)slotCount &
                       AnchorHashIds[anchorIndex] != 0 &
                       StackCounts[anchorIndex] > 0 &
                       CraftLockedCounts[anchorIndex] == 0 &
                       ((ItemStateFlags[anchorIndex] & (RadioactiveMask | FlammableMask)) != 0);
            }

            private bool IsReactivePairBranchless(int sourceIndex, int candidateIndex, int slotCount)
            {
                bool inBounds = (uint)candidateIndex < (uint)slotCount;
                int safeCandidateIndex = math.min(math.max(candidateIndex, 0), math.max(0, slotCount - 1));
                ushort sourceFlags = ItemStateFlags[sourceIndex];
                ushort candidateFlags = ItemStateFlags[safeCandidateIndex];

                uint reactionMask = (uint)(RadioactiveMask | FlammableMask);
                uint reactionResult = Hecton8.PureLogic.Systems.BranchlessReactiveItemChemistryCalculator.Compute(sourceFlags, candidateFlags, reactionMask);

                bool candidateActive = inBounds &
                                       AnchorHashIds[safeCandidateIndex] != 0 &
                                       StackCounts[safeCandidateIndex] != 0 &
                                       CraftLockedCounts[safeCandidateIndex] == 0;

                return candidateActive & (reactionResult != 0);
            }
        }



        [StructLayout(LayoutKind.Explicit, Size = 16)]
        public struct CraftReservation
        {
            [FieldOffset(0)]
            public int AnchorIndex;

            [FieldOffset(4)]
            public int Quantity;

            [FieldOffset(8)]
            public int ItemHashId;

            [FieldOffset(12)]
            private byte _pad0;

            [FieldOffset(13)]
            private byte _pad1;

            [FieldOffset(14)]
            private byte _pad2;

            [FieldOffset(15)]
            private byte _pad3;
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

        public readonly struct ItemState
        {
            public readonly ulong GeneticsMask;
            public readonly ushort QualityMilli;
            public readonly ushort Flags;
            public readonly bool HasExplicitFlags;

            public ItemState(ulong geneticsMask, ushort qualityMilli, ushort flags)
            {
                GeneticsMask = geneticsMask;
                QualityMilli = qualityMilli;
                Flags = flags;
                HasExplicitFlags = true;
            }

            public ItemState(ulong geneticsMask, ushort qualityMilli)
            {
                GeneticsMask = geneticsMask;
                QualityMilli = qualityMilli;
                Flags = 0;
                HasExplicitFlags = false;
            }

            public ItemState(ulong geneticsMask)
            {
                GeneticsMask = geneticsMask;
                QualityMilli = DefaultQualityMilli;
                Flags = 0;
                HasExplicitFlags = false;
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
            public byte stackable;

            public InventoryGrid.InventoryItemDescriptor Descriptor => new InventoryGrid.InventoryItemDescriptor(
                itemHashId,
                (byte)width,
                (byte)height,
                maxStack,
                weight,
                categoryId,
                rarity,
                stackable != 0);
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
        private InventoryVaultLane<uint> _itemHashes;
        private InventoryVaultLane<ushort> _stackCounts;
        private InventoryVaultLane<float> _itemCondition;
        private InventoryVaultLane<float> _itemDurability;
        private InventoryVaultLane<ushort> _craftLockedCounts;
        private InventoryVaultLane<ushort> _anchorStateFlags;
        private InventoryVaultLane<ushort> _itemStateFlags;
        private InventoryVaultLane<byte> _itemGenetics;
        private InventoryVaultLane<ushort> _qualityMilli;
        private InventoryVaultLane<byte> _durabilities;
        private InventoryVaultLane<uint> _lastUpdateUnixSeconds;
        private InventoryVaultLane<ushort> _scavengeSimStackCounts;
        private InventoryVaultLane<byte> _simulationOccupiedCells;
        private InventoryVaultLane<float> _anchorUnitMassKg;
        private InventoryVaultLane<float> _anchorUnitVolumeM3;
        private InventoryVaultLane<float> _anchorUnitRadiationSv;
        private InventoryVaultLane<float3> _derivedMassVolumeScratch;
        private InventoryVaultLane<int> _radioactiveConversionAnchors;
        private InventoryVaultLane<int> _radioactiveHalfLifeCounters;
        private InventoryVaultLane<float> _thermalRunawayByAnchor;
        private InventoryVaultLane<int2> _thermalRunawayPairs;
        private InventoryVaultLane<int> _thermalRunawayCounters;
        private InventoryVaultLane<byte> _inventoryShadowBuffer;
        private InventoryVaultLane<InventoryTelemetryEntry> _inventoryBlackBox;
        private InventoryVaultLane<int> _salinityCorrosionJobResult;
        private InventoryVaultLane<uint> _salinityBrokenItemHashes;
        private InventoryVaultLane<SalinityCorrosionTelemetryEntry> _salinityCorrosionBlackBox;
        private InventoryVaultLane<int> _salinityChangedSlotsScratch;
        private InventoryVaultLane<float> _salinityNextDurabilityScratch;
        private InventoryVaultLane<byte> _salinityNextDurabilityBytesScratch;
        private InventoryVaultLane<ushort> _salinityNextQualityMilliScratch;
        private InventoryVaultLane<ushort> _salinityNextStateFlagsScratch;
        private InventoryVaultLane<int> _defragItemHashes;
        private InventoryVaultLane<ushort> _defragItemCounts;
        private InventoryVaultLane<byte> _defragCategories;
        private InventoryVaultLane<ushort> _defragMaxStacks;
        private InventoryVaultLane<byte> _defragRarities;
        private InventoryVaultLane<byte> _defragWidths;
        private InventoryVaultLane<byte> _defragHeights;
        private InventoryVaultLane<byte> _defragFlags;
        private InventoryVaultLane<ushort> _defragStateFlags;
        private InventoryVaultLane<byte> _defragGenetics;
        private InventoryVaultLane<ushort> _defragQualityMilli;
        private InventoryVaultLane<byte> _defragDurabilities;
        private InventoryVaultLane<uint> _defragLastUpdateUnixSeconds;
        private InventoryVaultLane<float> _defragUnitMassKg;
        private InventoryVaultLane<float> _defragUnitVolumeM3;
        private InventoryVaultLane<float> _defragUnitRadiationSv;
        private InventoryVaultLane<int> _defragResult;
        private ItemPlacement[] _sortBuffer;
        private ushort[] _bulkCompactionMaxStackBuffer;
        private ItemAcquiredSignal[] _pendingScavengingItemSignals;
        private PendingInventoryCommand[] _pendingInventoryCommands;
        private PostSimulationPhaseSystem _postSimulationPhase;
        private int _pendingScavengingItemSignalCount;
        private int _pendingInventoryCommandCount;
        private int _droppedInventoryCommandSignalCount;
        private int _lastScavengingItemSignalCaptureGeneration = -1;
        private bool _registeredPostSimulationDispatcher;
        private bool _registeredSlowTick;
        private bool _registeredLateFrameTick;
        private float _pendingEquipmentRustShaderScalar;
        private bool _hasPendingEquipmentRustShaderScalar;
        private bool _massCacheDirty = true;
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
        private IPersistentDroppedItemRegistry _cachedPersistentWorldRegistry;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private IAudioService _cachedAudioService;
        private ISaveService _cachedSaveService;
        private ISaveService _registeredSaveService;
        private IDataVault _cachedDataVault;
        private IPhysicsStateEventService _cachedPhysicsStateEvents;
        private bool _physicsImpactRegistered;
        private bool _hotSwapListenerRegistered;
        private bool _saveRegistered;
        private int _vaultBufferBase;
        private ulong _salinityCorrosionMutationGuardMask;

        // Guards ClearPlayerInventoryVaultBuffersCold against wiping a populated inventory on a re-arm.
        // Set only after a completed bind-and-clear; cleared by ReleasePlayerInventoryVaultBuffers.
        private bool _vaultBuffersInitialized;
        private int _boundVaultCellCount;

        private const SystemID PlayerInventoryVaultOwner = SystemID.GameplayPlayer;
        private const int PlayerInventoryVaultBufferBase = 410000;
        private const int PlayerInventoryVaultBufferStride = 64;
        private const int PlayerInventoryVaultBufferInstanceMask = 0x3ff;
        private const int ItemDurabilityVaultOrdinal = 3;
        private const int ItemStateFlagsVaultOrdinal = 6;
        private const int QualityMilliVaultOrdinal = 8;
        private const int DurabilitiesVaultOrdinal = 9;
        private const int SalinityCorrosionJobResultVaultOrdinal = 29;
        private const int SalinityBrokenItemHashesVaultOrdinal = 30;
        private const int SalinityChangedSlotsScratchVaultOrdinal = 49;
        private const int SalinityNextDurabilityScratchVaultOrdinal = 50;
        private const int SalinityNextDurabilityBytesScratchVaultOrdinal = 51;
        private const int SalinityNextQualityMilliScratchVaultOrdinal = 52;
        private const int SalinityNextStateFlagsScratchVaultOrdinal = 53;

        internal struct InventoryVaultLane<T> where T : struct
        {
            private IDataVault _vault;
            private IDataVault _writeLockVault;
            private SystemID _owner;
            private int _expectedLength;
            private BufferID _expectedBufferId;

            public VaultGenerationHandle<T> Handle;

            public int Length => TryReadOnly(out NativeArray<T>.ReadOnly buffer) ? buffer.Length : 0;
            public bool IsCreated => TryReadOnly(out NativeArray<T>.ReadOnly buffer) && buffer.IsCreated;
            public uint BufferId => Handle.BufferID;
            public uint Generation => Handle.Generation;

            public T this[int index]
            {
                get
                {
                    if (!TryReadOnly(out NativeArray<T>.ReadOnly buffer) || (uint)index >= (uint)buffer.Length)
                        return default;

                    return buffer[index];
                }
                set
                {
                    if (!TryAcquireWriteLock(out NativeArray<T> buffer))
                        return;

                    try
                    {
                        if ((uint)index < (uint)buffer.Length)
                            buffer[index] = value;
                    }
                    finally
                    {
                        ReleaseWriteLock();
                    }
                }
            }

            public bool Bind(
                IDataVault vault,
                BufferID bufferId,
                int expectedLength,
                SystemID owner,
                NativeArrayOptions options)
            {
                _vault = vault;
                _owner = owner;
                _expectedBufferId = bufferId;
                _expectedLength = expectedLength;
                _writeLockVault = null;
                Handle = default;

                if (vault == null || expectedLength <= 0 || owner == SystemID.Unknown || bufferId == BufferID.Unknown)
                    return false;

                Handle = vault.EnsureGenerationHandle<T>(bufferId, expectedLength, owner, options);
                return ValidateDescriptor() && TryResolve(out NativeArray<T> buffer) && buffer.Length >= expectedLength;
            }

            public void RebindVault(IDataVault vault)
            {
                _vault = vault;
            }

            public void Release()
            {
                IDataVault writeLockVault = _writeLockVault;
                if (writeLockVault != null && ValidateDescriptor())
                    writeLockVault.ReleaseWriteLock(in Handle, _owner);

                _writeLockVault = null;
                IDataVault vault = _vault;
                if (vault != null && Handle.BufferID != 0u)
                    vault.ReleaseBuffer(in Handle);

                this = default;
            }

            public bool TryResolve(out NativeArray<T> buffer)
            {
                buffer = default;
                IDataVault vault = _vault;
                if (vault == null || !ValidateDescriptor())
                    return false;

                return vault.TryResolveHandle(in Handle, out buffer) &&
                       buffer.IsCreated &&
                       buffer.Length >= _expectedLength;
            }

            public bool TryReadOnly(out NativeArray<T>.ReadOnly buffer)
            {
                buffer = default;
                IDataVault vault = _vault;
                if (vault == null || !ValidateDescriptor())
                    return false;

                return vault.TryReadOnlyHandle(in Handle, out buffer) &&
                       buffer.Length >= _expectedLength;
            }

            public bool TryAcquireWriteLock(out NativeArray<T> buffer)
            {
                buffer = default;
                IDataVault vault = _vault;
                if (vault == null || _writeLockVault != null || !ValidateDescriptor())
                    return false;

                if (!vault.TryAcquireWriteLock(in Handle, _owner, out buffer))
                    return false;

                bool ownershipTransferred = false;
                try
                {
                    if (buffer.IsCreated && buffer.Length >= _expectedLength)
                    {
                        _writeLockVault = vault;
                        ownershipTransferred = true;
                        return true;
                    }

                    buffer = default;
                    return false;
                }
                finally
                {
                    if (!ownershipTransferred)
                        vault.ReleaseWriteLock(in Handle, _owner);
                }
            }

            public bool ReleaseWriteLock()
            {
                IDataVault vault = _writeLockVault;
                if (vault == null)
                    return false;

                _writeLockVault = null;
                return vault != null && ValidateDescriptor() && vault.ReleaseWriteLock(in Handle, _owner);
            }

            public NativeArray<T> Resolve()
            {
                return TryResolve(out NativeArray<T> buffer) ? buffer : default;
            }

            public NativeArray<T>.ReadOnly AsReadOnly()
            {
                return TryReadOnly(out NativeArray<T>.ReadOnly buffer) ? buffer : default;
            }

            public static implicit operator NativeArray<T>(InventoryVaultLane<T> lane)
            {
                return lane.Resolve();
            }

            private bool ValidateDescriptor()
            {
                return Handle.BufferID == (uint)_expectedBufferId &&
                       Handle.SystemID == (uint)_owner &&
                       Handle.Generation != 0u &&
                       _expectedLength > 0;
            }

            /// <summary>
            /// COLD. Re-reads this lane's generation descriptor from the vault for the SAME buffer id,
            /// owner and length it was bound with, and keeps the new descriptor only if it resolves.
            /// Allocates nothing and grows nothing - <c>TryGetGenerationHandle</c> is the read-only
            /// counterpart of <c>EnsureGenerationHandle</c>.
            ///
            /// WHY THIS EXISTS. <see cref="Bind"/> caches <c>Handle.Generation</c> once and every later
            /// read compares that cached value against the vault's current <c>meta.Version</c>. The vault
            /// re-stamps <c>meta.Version</c> for EVERY live buffer whenever a new allocation splits an
            /// arena free block (<c>GlobalDataVault.RebuildMetadataBlockIndices</c> assigns
            /// <c>meta.Version = block.Version</c> for every occupied block), so a cached descriptor goes
            /// stale for reasons that have nothing to do with this lane and without the payload moving.
            /// Every other vault consumer in this project (CraftingSystem, the cognition vaults, the
            /// fluid engine) therefore re-reads the descriptor at point of use and caches nothing;
            /// PlayerInventory was the outlier that cached and never re-read, which is why binding 49
            /// lanes in one chain left only the last one resolvable.
            ///
            /// This is NOT a relaxation of any guard: identity is still checked (same buffer id, same
            /// owner, non-zero generation), the vault still enforces stride/alignment/type-hash inside
            /// <c>TryGetGenerationHandle</c>, the length floor is still enforced, and the previous
            /// descriptor is restored on any refusal so a failed refresh cannot leave the lane pointing
            /// at something it did not own.
            /// </summary>
            public bool TryRefreshHandle()
            {
                IDataVault vault = _vault;
                if (vault == null ||
                    _expectedLength <= 0 ||
                    _owner == SystemID.Unknown ||
                    _expectedBufferId == BufferID.Unknown)
                {
                    return false;
                }

                // Swapping the descriptor while a write lock is outstanding would orphan that lock:
                // ReleaseWriteLock passes the CURRENT handle back to the vault.
                if (_writeLockVault != null)
                    return ValidateDescriptor() &&
                           TryReadOnly(out NativeArray<T>.ReadOnly lockedBuffer) &&
                           lockedBuffer.Length >= _expectedLength;

                if (!vault.TryGetGenerationHandle(_expectedBufferId, out VaultGenerationHandle<T> refreshed))
                    return false;

                if (refreshed.BufferID != (uint)_expectedBufferId ||
                    refreshed.SystemID != (uint)_owner ||
                    refreshed.Generation == 0u)
                {
                    return false;
                }

                VaultGenerationHandle<T> previous = Handle;
                Handle = refreshed;
                if (TryReadOnly(out NativeArray<T>.ReadOnly buffer) && buffer.Length >= _expectedLength)
                    return true;

                Handle = previous;
                return false;
            }

            /// <summary>COLD diagnostics: the buffer id this lane was bound with, as a number.</summary>
            public uint ExpectedBufferIdValue => (uint)_expectedBufferId;

            /// <summary>COLD diagnostics: the element count this lane was bound with.</summary>
            public int ExpectedLength => _expectedLength;

            /// <summary>COLD diagnostics: whether the cached descriptor still passes identity checks.</summary>
            public bool DescriptorValid => ValidateDescriptor();

            /// <summary>
            /// COLD diagnostics only. The generation the vault reports for this lane's buffer id RIGHT
            /// NOW, read fresh so a stale cached generation cannot mask it. Zero means the vault will
            /// not hand out a descriptor for that id at all (never allocated, released, fenced, or a
            /// stride/type mismatch).
            /// </summary>
            public uint ProbeVaultGenerationCold()
            {
                IDataVault vault = _vault;
                if (vault == null || !vault.TryGetGenerationHandle(_expectedBufferId, out VaultGenerationHandle<T> current))
                    return 0u;

                return current.Generation;
            }

            /// <summary>
            /// COLD diagnostics only. The element count the vault reports for this lane's buffer id RIGHT
            /// NOW, resolved through a freshly read descriptor. -1 means the vault cannot resolve the
            /// buffer at all, which distinguishes "wrong length" from "not there".
            /// </summary>
            public int ProbeVaultLengthCold()
            {
                IDataVault vault = _vault;
                if (vault == null || !vault.TryGetGenerationHandle(_expectedBufferId, out VaultGenerationHandle<T> current))
                    return -1;

                return vault.TryReadOnlyHandle(in current, out NativeArray<T>.ReadOnly buffer) ? buffer.Length : -1;
            }
        }

        private BufferID ResolvePlayerInventoryVaultBufferId(int ordinal)
        {
            if (_vaultBufferBase == 0)
            {
                int instanceBucket = unchecked((int)UnityEngine.EntityId.ToULong(GetEntityId())) & PlayerInventoryVaultBufferInstanceMask;
                _vaultBufferBase = PlayerInventoryVaultBufferBase + (instanceBucket * PlayerInventoryVaultBufferStride);
            }

            return (BufferID)(_vaultBufferBase + ordinal);
        }

        private ulong ResolvePlayerInventoryVaultMutationGuardBit(int ordinal)
        {
            uint bufferId = unchecked((uint)(int)ResolvePlayerInventoryVaultBufferId(ordinal));
            return 1UL << unchecked((int)(bufferId & 63u));
        }

        private ulong BuildSalinityCorrosionMutationGuardMask()
        {
            return ResolvePlayerInventoryVaultMutationGuardBit(ItemDurabilityVaultOrdinal) |
                   ResolvePlayerInventoryVaultMutationGuardBit(ItemStateFlagsVaultOrdinal) |
                   ResolvePlayerInventoryVaultMutationGuardBit(QualityMilliVaultOrdinal) |
                   ResolvePlayerInventoryVaultMutationGuardBit(DurabilitiesVaultOrdinal) |
                   ResolvePlayerInventoryVaultMutationGuardBit(SalinityCorrosionJobResultVaultOrdinal) |
                   ResolvePlayerInventoryVaultMutationGuardBit(SalinityBrokenItemHashesVaultOrdinal) |
                   ResolvePlayerInventoryVaultMutationGuardBit(SalinityChangedSlotsScratchVaultOrdinal) |
                   ResolvePlayerInventoryVaultMutationGuardBit(SalinityNextDurabilityScratchVaultOrdinal) |
                   ResolvePlayerInventoryVaultMutationGuardBit(SalinityNextDurabilityBytesScratchVaultOrdinal) |
                   ResolvePlayerInventoryVaultMutationGuardBit(SalinityNextQualityMilliScratchVaultOrdinal) |
                   ResolvePlayerInventoryVaultMutationGuardBit(SalinityNextStateFlagsScratchVaultOrdinal);
        }

        private bool TryAcquireSalinityCorrosionMutationGuard(out IDataVault guardedVault, out ulong guardMask)
        {
            guardMask = _salinityCorrosionMutationGuardMask;
            guardedVault = _cachedDataVault;
            return guardedVault != null && guardMask != 0UL && guardedVault.TryAcquireMutationGuard(guardMask);
        }

        private static void ReleaseSalinityCorrosionMutationGuard(IDataVault guardedVault, ulong guardMask)
        {
            if (guardedVault != null && guardMask != 0UL)
                guardedVault.ReleaseMutationGuard(guardMask);
        }

        private bool BindPlayerInventoryVaultBuffers(int cellCount)
        {
            if (_cachedDataVault == null || cellCount <= 0)
                return false;

            _salinityCorrosionMutationGuardMask = 0UL;
            bool success =
                BindVaultLane(ref _itemHashes, 0, cellCount) &&
                BindVaultLane(ref _stackCounts, 1, cellCount) &&
                BindVaultLane(ref _itemCondition, 2, cellCount) &&
                BindVaultLane(ref _itemDurability, ItemDurabilityVaultOrdinal, cellCount) &&
                BindVaultLane(ref _craftLockedCounts, 4, cellCount) &&
                BindVaultLane(ref _anchorStateFlags, 5, cellCount) &&
                BindVaultLane(ref _itemStateFlags, ItemStateFlagsVaultOrdinal, cellCount) &&
                BindVaultLane(ref _itemGenetics, 7, cellCount) &&
                BindVaultLane(ref _qualityMilli, QualityMilliVaultOrdinal, cellCount) &&
                BindVaultLane(ref _durabilities, DurabilitiesVaultOrdinal, cellCount) &&
                BindVaultLane(ref _lastUpdateUnixSeconds, 10, cellCount) &&
                BindVaultLane(ref _scavengeSimStackCounts, 11, cellCount) &&
                BindVaultLane(ref _simulationOccupiedCells, 12, cellCount) &&
                BindVaultLane(ref _anchorUnitMassKg, 13, cellCount) &&
                BindVaultLane(ref _anchorUnitVolumeM3, 14, cellCount) &&
                BindVaultLane(ref _anchorUnitRadiationSv, 15, cellCount) &&
                BindVaultLane(ref _derivedMassVolumeScratch, 21, 1) &&
                BindVaultLane(ref _radioactiveConversionAnchors, 22, cellCount) &&
                BindVaultLane(ref _radioactiveHalfLifeCounters, 23, 2) &&
                BindVaultLane(ref _thermalRunawayByAnchor, 24, cellCount) &&
                BindVaultLane(ref _thermalRunawayPairs, 25, cellCount) &&
                BindVaultLane(ref _thermalRunawayCounters, 26, 2) &&
                BindVaultLane(ref _inventoryShadowBuffer, 27, InventoryShadowBufferBytes) &&
                BindVaultLane(ref _inventoryBlackBox, 28, InventoryBlackBoxCapacity) &&
                BindVaultLane(ref _salinityCorrosionJobResult, SalinityCorrosionJobResultVaultOrdinal, InventoryCorrosionConstants.ResultRequiredLength) &&
                BindVaultLane(ref _salinityBrokenItemHashes, SalinityBrokenItemHashesVaultOrdinal, cellCount) &&
                BindVaultLane(ref _salinityCorrosionBlackBox, 31, InventoryBlackBoxCapacity) &&
                BindVaultLane(ref _salinityChangedSlotsScratch, SalinityChangedSlotsScratchVaultOrdinal, cellCount) &&
                BindVaultLane(ref _salinityNextDurabilityScratch, SalinityNextDurabilityScratchVaultOrdinal, cellCount) &&
                BindVaultLane(ref _salinityNextDurabilityBytesScratch, SalinityNextDurabilityBytesScratchVaultOrdinal, cellCount) &&
                BindVaultLane(ref _salinityNextQualityMilliScratch, SalinityNextQualityMilliScratchVaultOrdinal, cellCount) &&
                BindVaultLane(ref _salinityNextStateFlagsScratch, SalinityNextStateFlagsScratchVaultOrdinal, cellCount) &&
                BindVaultLane(ref _defragItemHashes, 32, cellCount) &&
                BindVaultLane(ref _defragItemCounts, 33, cellCount) &&
                BindVaultLane(ref _defragCategories, 34, cellCount) &&
                BindVaultLane(ref _defragMaxStacks, 35, cellCount) &&
                BindVaultLane(ref _defragRarities, 36, cellCount) &&
                BindVaultLane(ref _defragWidths, 37, cellCount) &&
                BindVaultLane(ref _defragHeights, 38, cellCount) &&
                BindVaultLane(ref _defragFlags, 39, cellCount) &&
                BindVaultLane(ref _defragStateFlags, 40, cellCount) &&
                BindVaultLane(ref _defragGenetics, 41, cellCount) &&
                BindVaultLane(ref _defragQualityMilli, 42, cellCount) &&
                BindVaultLane(ref _defragDurabilities, 43, cellCount) &&
                BindVaultLane(ref _defragLastUpdateUnixSeconds, 44, cellCount) &&
                BindVaultLane(ref _defragUnitMassKg, 45, cellCount) &&
                BindVaultLane(ref _defragUnitVolumeM3, 46, cellCount) &&
                BindVaultLane(ref _defragUnitRadiationSv, 47, cellCount) &&
                BindVaultLane(ref _defragResult, 48, InventoryDefragResultSlots.RequiredLength);

            if (!success)
            {
                ReleasePlayerInventoryVaultBuffers();
            }
            else
            {
                // MANDATORY, not a tidy-up. Every BindVaultLane call above allocates a vault buffer, and a
                // vault allocation that splits an arena free block re-stamps meta.Version for every buffer
                // already live (GlobalDataVault.RebuildMetadataBlockIndices writes
                // meta.Version = block.Version for each occupied block). The descriptor each lane cached at
                // its own Bind is therefore stale the moment the NEXT lane binds, so by the time this chain
                // returns true only the last lane bound is still resolvable - every earlier one reads
                // IsCreated == false and Length == 0 even though its payload is intact and untouched.
                // Re-reading all 49 descriptors once here is what makes the chain's success mean anything.
                RefreshPlayerInventoryVaultHandlesCold();
                _salinityCorrosionMutationGuardMask = BuildSalinityCorrosionMutationGuardMask();
            }

            return success;
        }

        private bool BindVaultLane<T>(
            ref InventoryVaultLane<T> lane,
            int ordinal,
            int length,
            NativeArrayOptions options = NativeArrayOptions.UninitializedMemory) where T : struct
        {
            return lane.Bind(_cachedDataVault, ResolvePlayerInventoryVaultBufferId(ordinal), length, PlayerInventoryVaultOwner, options);
        }

        private void RebindPlayerInventoryVaultReferences(IDataVault vault)
        {
            _itemHashes.RebindVault(vault);
            _stackCounts.RebindVault(vault);
            _itemCondition.RebindVault(vault);
            _itemDurability.RebindVault(vault);
            _craftLockedCounts.RebindVault(vault);
            _anchorStateFlags.RebindVault(vault);
            _itemStateFlags.RebindVault(vault);
            _itemGenetics.RebindVault(vault);
            _qualityMilli.RebindVault(vault);
            _durabilities.RebindVault(vault);
            _lastUpdateUnixSeconds.RebindVault(vault);
            _scavengeSimStackCounts.RebindVault(vault);
            _simulationOccupiedCells.RebindVault(vault);
            _anchorUnitMassKg.RebindVault(vault);
            _anchorUnitVolumeM3.RebindVault(vault);
            _anchorUnitRadiationSv.RebindVault(vault);
            _derivedMassVolumeScratch.RebindVault(vault);
            _radioactiveConversionAnchors.RebindVault(vault);
            _radioactiveHalfLifeCounters.RebindVault(vault);
            _thermalRunawayByAnchor.RebindVault(vault);
            _thermalRunawayPairs.RebindVault(vault);
            _thermalRunawayCounters.RebindVault(vault);
            _inventoryShadowBuffer.RebindVault(vault);
            _inventoryBlackBox.RebindVault(vault);
            _salinityCorrosionJobResult.RebindVault(vault);
            _salinityBrokenItemHashes.RebindVault(vault);
            _salinityCorrosionBlackBox.RebindVault(vault);
            _salinityChangedSlotsScratch.RebindVault(vault);
            _salinityNextDurabilityScratch.RebindVault(vault);
            _salinityNextDurabilityBytesScratch.RebindVault(vault);
            _salinityNextQualityMilliScratch.RebindVault(vault);
            _salinityNextStateFlagsScratch.RebindVault(vault);
            _defragItemHashes.RebindVault(vault);
            _defragItemCounts.RebindVault(vault);
            _defragCategories.RebindVault(vault);
            _defragMaxStacks.RebindVault(vault);
            _defragRarities.RebindVault(vault);
            _defragWidths.RebindVault(vault);
            _defragHeights.RebindVault(vault);
            _defragFlags.RebindVault(vault);
            _defragStateFlags.RebindVault(vault);
            _defragGenetics.RebindVault(vault);
            _defragQualityMilli.RebindVault(vault);
            _defragDurabilities.RebindVault(vault);
            _defragLastUpdateUnixSeconds.RebindVault(vault);
            _defragUnitMassKg.RebindVault(vault);
            _defragUnitVolumeM3.RebindVault(vault);
            _defragUnitRadiationSv.RebindVault(vault);
            _defragResult.RebindVault(vault);

            // NO refresh sweep here on purpose. RebindVault only re-points each lane at the new IDataVault,
            // so every cached generation is now meaningless (it came from the OLD vault's metadata) and a
            // sweep IS required - but the only caller, OnGlobalRegistryServiceReplaced's DataVault case,
            // runs TryBindSoaQueryVault straight afterwards, and those allocations would immediately re-stale
            // anything refreshed here. The sweep therefore lives at the end of that case, after the last
            // allocation. If you add a second caller, give it the same treatment.
        }

        /// <summary>
        /// COLD. Re-reads the vault generation descriptor of every lane this component owns, for the same
        /// buffer id / owner / length each was bound with. Allocates nothing, grows nothing, moves nothing,
        /// and cannot retarget a lane at a buffer it does not own - see
        /// <c>InventoryVaultLane{T}.TryRefreshHandle</c>.
        ///
        /// Return value is deliberately void: a lane that cannot be refreshed is left exactly as it was and
        /// is caught downstream by the fail-closed checks (<see cref="AllocateSalinityCorrosionScratchCold"/>,
        /// <see cref="CanServiceItemAdds"/>). This method must never be the thing that decides storage is
        /// healthy - it only removes staleness as an explanation for a refusal.
        ///
        /// Ordering matters: call it AFTER the last allocation in a batch, never between allocations, or the
        /// next allocation invalidates what it just repaired.
        /// </summary>
        private void RefreshPlayerInventoryVaultHandlesCold()
        {
            _itemHashes.TryRefreshHandle();
            _stackCounts.TryRefreshHandle();
            _itemCondition.TryRefreshHandle();
            _itemDurability.TryRefreshHandle();
            _craftLockedCounts.TryRefreshHandle();
            _anchorStateFlags.TryRefreshHandle();
            _itemStateFlags.TryRefreshHandle();
            _itemGenetics.TryRefreshHandle();
            _qualityMilli.TryRefreshHandle();
            _durabilities.TryRefreshHandle();
            _lastUpdateUnixSeconds.TryRefreshHandle();
            _scavengeSimStackCounts.TryRefreshHandle();
            _simulationOccupiedCells.TryRefreshHandle();
            _anchorUnitMassKg.TryRefreshHandle();
            _anchorUnitVolumeM3.TryRefreshHandle();
            _anchorUnitRadiationSv.TryRefreshHandle();
            _derivedMassVolumeScratch.TryRefreshHandle();
            _radioactiveConversionAnchors.TryRefreshHandle();
            _radioactiveHalfLifeCounters.TryRefreshHandle();
            _thermalRunawayByAnchor.TryRefreshHandle();
            _thermalRunawayPairs.TryRefreshHandle();
            _thermalRunawayCounters.TryRefreshHandle();
            _inventoryShadowBuffer.TryRefreshHandle();
            _inventoryBlackBox.TryRefreshHandle();
            _salinityCorrosionJobResult.TryRefreshHandle();
            _salinityBrokenItemHashes.TryRefreshHandle();
            _salinityCorrosionBlackBox.TryRefreshHandle();
            _salinityChangedSlotsScratch.TryRefreshHandle();
            _salinityNextDurabilityScratch.TryRefreshHandle();
            _salinityNextDurabilityBytesScratch.TryRefreshHandle();
            _salinityNextQualityMilliScratch.TryRefreshHandle();
            _salinityNextStateFlagsScratch.TryRefreshHandle();
            _defragItemHashes.TryRefreshHandle();
            _defragItemCounts.TryRefreshHandle();
            _defragCategories.TryRefreshHandle();
            _defragMaxStacks.TryRefreshHandle();
            _defragRarities.TryRefreshHandle();
            _defragWidths.TryRefreshHandle();
            _defragHeights.TryRefreshHandle();
            _defragFlags.TryRefreshHandle();
            _defragStateFlags.TryRefreshHandle();
            _defragGenetics.TryRefreshHandle();
            _defragQualityMilli.TryRefreshHandle();
            _defragDurabilities.TryRefreshHandle();
            _defragLastUpdateUnixSeconds.TryRefreshHandle();
            _defragUnitMassKg.TryRefreshHandle();
            _defragUnitVolumeM3.TryRefreshHandle();
            _defragUnitRadiationSv.TryRefreshHandle();
            _defragResult.TryRefreshHandle();
        }

        private void ClearPlayerInventoryVaultBuffersCold()
        {
            ClearNativeArray(_itemHashes);
            ClearNativeArray(_stackCounts);
            ClearNativeArray(_itemCondition);
            ClearNativeArray(_itemDurability);
            ClearNativeArray(_craftLockedCounts);
            ClearNativeArray(_anchorStateFlags);
            ClearNativeArray(_itemStateFlags);
            ClearNativeArray(_itemGenetics);
            ClearNativeArray(_qualityMilli);
            ClearNativeArray(_durabilities);
            ClearNativeArray(_lastUpdateUnixSeconds);
            ClearNativeArray(_scavengeSimStackCounts);
            ClearNativeArray(_simulationOccupiedCells);
            ClearNativeArray(_anchorUnitMassKg);
            ClearNativeArray(_anchorUnitVolumeM3);
            ClearNativeArray(_anchorUnitRadiationSv);
            ClearNativeArray(_derivedMassVolumeScratch);
            ClearNativeArray(_radioactiveConversionAnchors);
            ClearNativeArray(_radioactiveHalfLifeCounters);
            ClearNativeArray(_thermalRunawayByAnchor);
            ClearNativeArray(_thermalRunawayPairs);
            ClearNativeArray(_thermalRunawayCounters);
            ClearNativeArray(_inventoryShadowBuffer);
            ClearNativeArray(_inventoryBlackBox);
            ClearNativeArray(_salinityCorrosionJobResult);
            ClearNativeArray(_salinityBrokenItemHashes);
            ClearNativeArray(_salinityCorrosionBlackBox);
            ClearNativeArray(_salinityChangedSlotsScratch);
            ClearNativeArray(_salinityNextDurabilityScratch);
            ClearNativeArray(_salinityNextDurabilityBytesScratch);
            ClearNativeArray(_salinityNextQualityMilliScratch);
            ClearNativeArray(_salinityNextStateFlagsScratch);
            ClearNativeArray(_defragItemHashes);
            ClearNativeArray(_defragItemCounts);
            ClearNativeArray(_defragCategories);
            ClearNativeArray(_defragMaxStacks);
            ClearNativeArray(_defragRarities);
            ClearNativeArray(_defragWidths);
            ClearNativeArray(_defragHeights);
            ClearNativeArray(_defragFlags);
            ClearNativeArray(_defragStateFlags);
            ClearNativeArray(_defragGenetics);
            ClearNativeArray(_defragQualityMilli);
            ClearNativeArray(_defragDurabilities);
            ClearNativeArray(_defragLastUpdateUnixSeconds);
            ClearNativeArray(_defragUnitMassKg);
            ClearNativeArray(_defragUnitVolumeM3);
            ClearNativeArray(_defragUnitRadiationSv);
            ClearNativeArray(_defragResult);
        }

        private void ReleasePlayerInventoryVaultBuffers()
        {
            _salinityCorrosionMutationGuardMask = 0UL;
            // Released lanes come back as UninitializedMemory on the next bind, so the next bind MUST clear.
            _vaultBuffersInitialized = false;
            _boundVaultCellCount = 0;
            _itemHashes.Release();
            _stackCounts.Release();
            _itemCondition.Release();
            _itemDurability.Release();
            _craftLockedCounts.Release();
            _anchorStateFlags.Release();
            _itemStateFlags.Release();
            _itemGenetics.Release();
            _qualityMilli.Release();
            _durabilities.Release();
            _lastUpdateUnixSeconds.Release();
            _scavengeSimStackCounts.Release();
            _simulationOccupiedCells.Release();
            _anchorUnitMassKg.Release();
            _anchorUnitVolumeM3.Release();
            _anchorUnitRadiationSv.Release();
            _derivedMassVolumeScratch.Release();
            _radioactiveConversionAnchors.Release();
            _radioactiveHalfLifeCounters.Release();
            _thermalRunawayByAnchor.Release();
            _thermalRunawayPairs.Release();
            _thermalRunawayCounters.Release();
            _inventoryShadowBuffer.Release();
            _inventoryBlackBox.Release();
            _salinityCorrosionJobResult.Release();
            _salinityBrokenItemHashes.Release();
            _salinityCorrosionBlackBox.Release();
            _salinityChangedSlotsScratch.Release();
            _salinityNextDurabilityScratch.Release();
            _salinityNextDurabilityBytesScratch.Release();
            _salinityNextQualityMilliScratch.Release();
            _salinityNextStateFlagsScratch.Release();
            _defragItemHashes.Release();
            _defragItemCounts.Release();
            _defragCategories.Release();
            _defragMaxStacks.Release();
            _defragRarities.Release();
            _defragWidths.Release();
            _defragHeights.Release();
            _defragFlags.Release();
            _defragStateFlags.Release();
            _defragGenetics.Release();
            _defragQualityMilli.Release();
            _defragDurabilities.Release();
            _defragLastUpdateUnixSeconds.Release();
            _defragUnitMassKg.Release();
            _defragUnitVolumeM3.Release();
            _defragUnitRadiationSv.Release();
            _defragResult.Release();
        }

        /// <summary>
        /// The per-lane verdict from the last <see cref="AllocateSalinityCorrosionScratchCold"/> run, or
        /// null when it passed. Cold-only, read by <see cref="AnnounceRuntimeStorageFailureOnce"/>.
        /// </summary>
        private string _salinityScratchFailureDetail;

        /// <summary>
        /// COLD. Validates the salinity-corrosion scratch lanes and, on refusal, names WHICH assertion
        /// failed with expected-vs-actual numbers.
        ///
        /// The name is historical: this method allocates nothing. Every lane it checks was already
        /// created by <see cref="BindPlayerInventoryVaultBuffers"/>; this is the fail-closed re-read that
        /// stands between a bad layout and 48 cells of vault corruption, and it stays fail-closed. It is
        /// deliberately the FIRST thing to re-read the lanes after the bind chain, which makes it the
        /// messenger for any staleness that hit the earlier lanes too - do not read a refusal here as
        /// "the salinity lanes specifically are broken" without checking the numbers it now prints.
        ///
        /// One handle refresh is attempted per failing lane before the refusal is recorded. That is not a
        /// weakening: a stale generation descriptor is a cached-identity problem, not a layout problem,
        /// the payload has not moved, and the same assertions must still pass afterwards or the lane is
        /// still reported. See <c>InventoryVaultLane{T}.TryRefreshHandle</c> for why a descriptor bound
        /// in this method's own call chain can already be stale by the time it is read.
        ///
        /// Naming the numbers is the point. A previous version of this guard returned one bare bool for
        /// fifteen conjuncts, so a refusal named the step and nothing else, and three separate theories
        /// about the cause (DTO layout drift, vault init order, grid allocation) each had to be disproved
        /// by other means before the real one could be reached. Do not collapse this back into a single
        /// boolean expression.
        /// </summary>
        private bool AllocateSalinityCorrosionScratchCold(int cellCount)
        {
            _salinityScratchFailureDetail = null;
            if (cellCount <= 0)
            {
                _salinityScratchFailureDetail = "cellCount=" + cellCount.ToString() +
                                                " expected>0 (columns*rows); no lane was checked";
                return false;
            }

            int failures = 0;
            int resultLength = InventoryCorrosionConstants.ResultRequiredLength;
            failures += ValidateSalinityScratchLaneCold(ref _salinityCorrosionJobResult, "jobResult<int>[ord29]", resultLength) ? 0 : 1;
            failures += ValidateSalinityScratchLaneCold(ref _salinityBrokenItemHashes, "brokenItemHashes<uint>[ord30]", cellCount) ? 0 : 1;
            failures += ValidateSalinityScratchLaneCold(ref _salinityChangedSlotsScratch, "changedSlots<int>[ord49]", cellCount) ? 0 : 1;
            failures += ValidateSalinityScratchLaneCold(ref _salinityNextDurabilityScratch, "nextDurability<float>[ord50]", cellCount) ? 0 : 1;
            failures += ValidateSalinityScratchLaneCold(ref _salinityNextDurabilityBytesScratch, "nextDurabilityBytes<byte>[ord51]", cellCount) ? 0 : 1;
            failures += ValidateSalinityScratchLaneCold(ref _salinityNextQualityMilliScratch, "nextQualityMilli<ushort>[ord52]", cellCount) ? 0 : 1;
            failures += ValidateSalinityScratchLaneCold(ref _salinityNextStateFlagsScratch, "nextStateFlags<ushort>[ord53]", cellCount) ? 0 : 1;

            if (failures == 0)
                return true;

            // 7 of 7 failing is a different diagnosis from 1 of 7: the first says the whole bind went
            // stale or the vault is refusing this owner wholesale, the second says one lane's length or
            // ordinal is wrong. Lead with the count so the reader does not have to infer it.
            _salinityScratchFailureDetail = failures.ToString() + " of 7 lanes refused; cellCount=" +
                                            cellCount.ToString() + " vaultBufferBase=" +
                                            _vaultBufferBase.ToString() + "; " +
                                            _salinityScratchFailureDetail;
            return false;
        }

        /// <summary>
        /// COLD. One lane's half of <see cref="AllocateSalinityCorrosionScratchCold"/>: the same
        /// <c>IsCreated</c> + length-floor assertions as before, one refresh attempt, then a numbered
        /// verdict appended to <see cref="_salinityScratchFailureDetail"/> on refusal.
        /// </summary>
        private bool ValidateSalinityScratchLaneCold<T>(
            ref InventoryVaultLane<T> lane,
            string laneName,
            int requiredLength) where T : struct
        {
            if (lane.IsCreated && lane.Length >= requiredLength)
                return true;

            if (lane.TryRefreshHandle() && lane.IsCreated && lane.Length >= requiredLength)
                return true;

            string detail =
                laneName +
                " bufferId=" + lane.ExpectedBufferIdValue.ToString() +
                " requiredLength=" + requiredLength.ToString() +
                " boundLength=" + lane.ExpectedLength.ToString() +
                " readableLength=" + lane.Length.ToString() +
                " isCreated=" + (lane.IsCreated ? "yes" : "no") +
                " descriptorValid=" + (lane.DescriptorValid ? "yes" : "no") +
                " cachedGeneration=" + lane.Generation.ToString() +
                " vaultGenerationNow=" + lane.ProbeVaultGenerationCold().ToString() +
                " vaultLengthNow=" + lane.ProbeVaultLengthCold().ToString() +
                " strideBytes=" + UnsafeUtility.SizeOf<T>().ToString();

            _salinityScratchFailureDetail = _salinityScratchFailureDetail == null
                ? detail
                : _salinityScratchFailureDetail + " | " + detail;
            return false;
        }

        private void ReleaseSalinityCorrosionScratchCold()
        {
            // Scratch lanes are owned by ReleasePlayerInventoryVaultBuffers.
        }

#if UNITY_EDITOR
        // The verdict is a property of the loaded assembly, not of any component instance: a struct's size
        // and field offsets cannot differ between two PlayerInventory objects in one domain. It was being
        // recomputed in EVERY Awake - 24 Marshal.OffsetOf reflection lookups plus 3 size intrinsics per
        // inventory - to re-derive a constant. Statics reset on domain reload, which is exactly the cache
        // lifetime this needs.
        private static bool _inventoryLayoutVerdictComputed;
        private static bool _inventoryLayoutVerdictPassed;

        private static bool ValidateInventoryMemorySovereigntyLayouts1317()
        {
            if (_inventoryLayoutVerdictComputed)
                return _inventoryLayoutVerdictPassed;

            _inventoryLayoutVerdictComputed = true;
            uint failures = 0u;
            ValidateLayoutSize<InventoryTelemetryEntry>(InventoryBlackBoxEntrySizeBytes, ref failures);
            ValidateOffset<InventoryTelemetryEntry>(nameof(InventoryTelemetryEntry.Frame), 0, ref failures);
            ValidateOffset<InventoryTelemetryEntry>(nameof(InventoryTelemetryEntry.Version), 4, ref failures);
            ValidateOffset<InventoryTelemetryEntry>(nameof(InventoryTelemetryEntry.WeightKg), 8, ref failures);
            ValidateOffset<InventoryTelemetryEntry>(nameof(InventoryTelemetryEntry.VolumeLiters), 12, ref failures);
            ValidateOffset<InventoryTelemetryEntry>(nameof(InventoryTelemetryEntry.Load01), 16, ref failures);
            ValidateOffset<InventoryTelemetryEntry>(nameof(InventoryTelemetryEntry.InventoryMaskLow), 20, ref failures);
            ValidateOffset<InventoryTelemetryEntry>(nameof(InventoryTelemetryEntry.OccupiedCells), 24, ref failures);
            ValidateOffset<InventoryTelemetryEntry>(nameof(InventoryTelemetryEntry.Flags), 28, ref failures);
            ValidateOffset<InventoryTelemetryEntry>(nameof(InventoryTelemetryEntry.MaxWeightKg), 32, ref failures);
            ValidateOffset<InventoryTelemetryEntry>(nameof(InventoryTelemetryEntry.MaxVolumeLiters), 36, ref failures);
            ValidateOffset<InventoryTelemetryEntry>(nameof(InventoryTelemetryEntry.ShadowHash), 40, ref failures);
            ValidateOffset<InventoryTelemetryEntry>(nameof(InventoryTelemetryEntry.ShadowPayloadLength), 44, ref failures);
            ValidateOffset<InventoryTelemetryEntry>(nameof(InventoryTelemetryEntry.RadiationSv), 48, ref failures);
            ValidateOffset<InventoryTelemetryEntry>(nameof(InventoryTelemetryEntry.Columns), 52, ref failures);
            ValidateOffset<InventoryTelemetryEntry>(nameof(InventoryTelemetryEntry.Rows), 56, ref failures);
            ValidateOffset<InventoryTelemetryEntry>(nameof(InventoryTelemetryEntry.DefragTimeMicroseconds), 60, ref failures);

            ValidateLayoutSize<SalinityCorrosionTelemetryEntry>(SalinityCorrosionBlackBoxEntrySizeBytes, ref failures);
            ValidateOffset<SalinityCorrosionTelemetryEntry>(nameof(SalinityCorrosionTelemetryEntry.Frame), 0, ref failures);
            ValidateOffset<SalinityCorrosionTelemetryEntry>(nameof(SalinityCorrosionTelemetryEntry.InventoryVersion), 4, ref failures);
            ValidateOffset<SalinityCorrosionTelemetryEntry>(nameof(SalinityCorrosionTelemetryEntry.AverageEquipmentDurability01), 8, ref failures);
            ValidateOffset<SalinityCorrosionTelemetryEntry>(nameof(SalinityCorrosionTelemetryEntry.RustScalar01), 12, ref failures);
            ValidateOffset<SalinityCorrosionTelemetryEntry>(nameof(SalinityCorrosionTelemetryEntry.SalinityFactor), 16, ref failures);
            ValidateOffset<SalinityCorrosionTelemetryEntry>(nameof(SalinityCorrosionTelemetryEntry.CurrentBiomeHash), 20, ref failures);
            ValidateOffset<SalinityCorrosionTelemetryEntry>(nameof(SalinityCorrosionTelemetryEntry.InventoryMaskLow), 24, ref failures);
            ValidateOffset<SalinityCorrosionTelemetryEntry>(nameof(SalinityCorrosionTelemetryEntry.Flags), 28, ref failures);

            ValidateLayoutSize<VaultGenerationHandle<uint>>(16, ref failures);

            _inventoryLayoutVerdictPassed = failures == 0u;
            if (_inventoryLayoutVerdictPassed)
            {
                // A PASSING guard used to emit absolutely nothing, and that silence is the whole reason this
                // branch became the standing suspect for every empty-inventory symptom in the project: an
                // unrun guard and a satisfied guard produced byte-identical logs, so "the DTO layout drifted"
                // could never be disconfirmed from a run - only re-argued. One line per domain reload retires
                // that question permanently and points the next investigation at the bind, not at the layout.
                Debug.Log(
                    "[PlayerInventory] DTO layout sovereignty PASSED: InventoryTelemetryEntry " +
                    InventoryBlackBoxEntrySizeBytes + "B, SalinityCorrosionTelemetryEntry " +
                    SalinityCorrosionBlackBoxEntrySizeBytes + "B, VaultGenerationHandle<uint> 16B, all 24 " +
                    "asserted field offsets in place. Awake did NOT bail here, so an empty inventory this " +
                    "session has a different cause - look at the GlobalDataVault lane bind " +
                    "(STORAGE UNAVAILABLE above) instead.");
            }

            return _inventoryLayoutVerdictPassed;
        }

        private static void ValidateLayoutSize<T>(int expectedSize, ref uint failures) where T : struct
        {
            int size = UnsafeUtility.SizeOf<T>();
            if (size == expectedSize && (size & 7) == 0)
                return;

            failures |= 1u;
            // Editor-only guard path; naming the type and both numbers is the whole point of the check.
            // Previously this set a bit and said nothing, so a layout drift silently disabled the entire
            // player inventory with no way to tell which struct moved.
            Debug.LogError(
                "[PlayerInventory] DTO layout size mismatch: " + typeof(T).Name +
                " is " + size + " bytes, expected " + expectedSize +
                ((size & 7) != 0 ? " (and is not 8-byte aligned)" : string.Empty) +
                ". ARM64/Burst/persistence/GPU boundaries require the authored layout; fix the struct or the "
                + "expected constant, do not relax the guard.");
        }

        private static void ValidateOffset<T>(string fieldName, int expectedOffset, ref uint failures) where T : struct
        {
            int offset = (int)Marshal.OffsetOf<T>(fieldName);
            if (offset == expectedOffset)
                return;

            failures |= 2u;
            Debug.LogError(
                "[PlayerInventory] DTO field offset mismatch: " + typeof(T).Name + "." + fieldName +
                " is at " + offset + ", expected " + expectedOffset +
                ". A field was reordered, resized, or lost its explicit padding.");
        }
#endif

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
        public int DroppedInventoryCommandSignalCount => _droppedInventoryCommandSignalCount;
        public InventoryGrid Grid => _grid;
        public ItemCatalog ItemCatalog => itemCatalog;
        public int InventoryVersion { get; private set; }

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
#if UNITY_EDITOR
            if (!ValidateInventoryMemorySovereigntyLayouts1317())
            {
                // Fail-closed is correct here - running with a drifted DTO layout would corrupt vault
                // buffers. Failing SILENTLY was not: this returned before _grid was built and before
                // BindPlayerInventoryVaultBuffers ran, so the inventory was dead with nothing logged, and
                // every downstream consumer just saw an empty inventory.
                //
                // THIS BRANCH IS NOT WHAT EMPTIED THE TOOL SLOTS. The earlier attribution of the four empty
                // slots to this guard was a hypothesis that had never been executed, and it is now
                // disconfirmed: all three asserted layouts were reproduced field-for-field outside Unity and
                // every one of the 27 assertions passes. Both telemetry entries are LayoutKind.Explicit with
                // hardcoded [FieldOffset] values IDENTICAL to the numbers the validator expects, and
                // VaultGenerationHandle<uint> is four uints under Size = 16 - measured 16 via the same
                // 'sizeof' opcode UnsafeUtility.SizeOf<T>() lowers to. An explicit-layout struct cannot drift
                // away from an expectation that is spelled with the same literals, so this guard cannot fail
                // as written and Awake reaches the grid construction below. The surviving suspect for an
                // empty inventory is the vault lane bind - see TryBindRuntimeStorageCold and its
                // STORAGE UNAVAILABLE line, which names _cachedDataVault when the GlobalDataVault service is
                // not registered yet at Awake time.
                enabled = false;
                Debug.LogError(
                    "[PlayerInventory] DISABLED at Awake: DTO layout validation failed (see the layout " +
                    "mismatch errors above). The inventory grid was never built and its vault lanes were " +
                    "never bound, so item grants, tool availability and any consumer reading inventory " +
                    "state will report empty for the rest of this session. Editor-only guard: a player " +
                    "build does not take this path.");
                return;
            }
#endif
            _grid = new InventoryGrid(columns, rows);

            // The lane bind and the cold scratch allocation live in TryBindRuntimeStorageCold so the SAME
            // sequence can be re-run later. Both of the bailouts that used to sit inline here were silent
            // `enabled = false; return;` - and because Awake disabling the component also means OnEnable
            // never runs, the hot-swap listener was never registered either, so nothing could ever drive a
            // retry. A vault refusal that was only ever momentary (raised compaction fence, arena or key
            // table briefly exhausted) therefore became permanent for the session, with no log line naming
            // it: every TryAddItem false and every CountAvailableTotal zero, which downstream reads as an
            // empty inventory rather than a broken one. TryBindRuntimeStorageCold now names the failing step
            // once, and TryRecoverRuntimeStorageCold lets the consumer that noticed re-arm it.
            if (!TryBindRuntimeStorageCold())
            {
                enabled = false;
                return;
            }

            if (_traumaDispatcher == null)
                TryGetComponent(out _traumaDispatcher);
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            TryRegisterSaveParticipant();
            TryRegisterPostSimulationDispatcher();
            TryRegisterSlowTick();
            TryRegisterLateFrameTick();
            TryRegisterPhysicsImpactListener();
        }

        private void OnDisable()
        {
            CaptureScavengingLootOracleSignals();
            ApplyDeferredScavengingLootOracleSignals();
            DropDeferredInventoryCommandSignals();
            TryUnregisterPhysicsImpactListener();
            TryUnregisterSaveParticipant();
            TryUnregisterSlowTick();
            TryUnregisterLateFrameTick();
            TryUnregisterPostSimulationDispatcher();
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            CaptureScavengingLootOracleSignals();
            ApplyDeferredScavengingLootOracleSignals();
            DropDeferredInventoryCommandSignals();
            TryUnregisterPhysicsImpactListener();
            TryUnregisterSaveParticipant();
            TryUnregisterSlowTick();
            TryUnregisterLateFrameTick();
            TryUnregisterPostSimulationDispatcher();
            TryUnregisterHotSwapListener();

            if (_grid != null)
            {
                _grid.Dispose(default);
                _grid = null;
            }

            ReleasePlayerInventoryVaultBuffers();
            ReleaseSalinityCorrosionScratchCold();
            DisposeSoaQueryEngine();

        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.PersistentWorldRegistry:
                    _cachedPersistentWorldRegistry = currentService as IPersistentDroppedItemRegistry;
                    break;
                case GlobalRegistryServiceSlot.Player:
                    CachePlayerRuntimeContext(currentService as IPlayerRuntimeContext);
                    break;
                case GlobalRegistryServiceSlot.Audio:
                    CacheAudioService(currentService as IAudioService);
                    break;
                case GlobalRegistryServiceSlot.Save:
                    TryUnregisterSaveParticipant();
                    _cachedSaveService = currentService as ISaveService;
                    TryRegisterSaveParticipant();
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    _cachedDataVault = currentService as IDataVault;
                    RebindPlayerInventoryVaultReferences(_cachedDataVault);
                    TryBindSoaQueryVault(_cachedDataVault, columns * rows);
                    // AFTER the last allocation in this case, not before it: TryBindSoaQueryVault above
                    // allocates vault buffers, and every vault allocation that splits an arena free block
                    // re-stamps meta.Version for all live buffers, staling the 49 descriptors the rebind just
                    // re-pointed. PublishSoaQueryVaultSnapshotOwnerPhase reads through those lanes, so the
                    // sweep has to land between the two.
                    RefreshPlayerInventoryVaultHandlesCold();
                    PublishSoaQueryVaultSnapshotOwnerPhase();
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregisterSlowTick();
                    TryUnregisterLateFrameTick();
                    TryUnregisterPostSimulationDispatcher();
                    TryRegisterPostSimulationDispatcher();
                    TryRegisterSlowTick();
                    TryRegisterLateFrameTick();
                    break;
                case GlobalRegistryServiceSlot.PhysicsStateManager:
                    RebindPhysicsStateEventService(currentService as IPhysicsStateEventService);
                    break;
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private void CacheRegistryServicesCold()
        {
            _cachedPersistentWorldRegistry = GlobalRegistry.PersistentDroppedItems;
            CachePlayerRuntimeContext(GlobalRegistry.Player);
            if (_traumaDispatcher == null)
                TryGetComponent(out _traumaDispatcher);
            CacheAudioService(GlobalRegistry.Audio);
            _cachedSaveService = GlobalRegistry.Save;
            _cachedDataVault = GlobalRegistry.DataVault;
            _cachedPhysicsStateEvents = GlobalRegistry.PhysicsStateEvents;
        }

        private void CacheAudioService(IAudioService audioService)
        {
            _cachedAudioService = IsAudioServiceUsable(audioService) ? audioService : null;
        }

        private IAudioService ResolveAudioService()
        {
            IAudioService audioService = _cachedAudioService;
            if (IsAudioServiceUsable(audioService))
                return audioService;

            _cachedAudioService = null;
            return null;
        }

        private static bool IsAudioServiceUsable(IAudioService audioService)
        {
            if (audioService == null || !audioService.IsAudioRuntimeReady)
                return false;

            if (audioService is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private void CachePlayerRuntimeContext(IPlayerRuntimeContext playerContext)
        {
            _cachedPlayerContext = playerContext;
            _traumaDispatcher = playerContext != null ? playerContext.TraumaDispatcher : null;
        }

        private void TryRegisterPhysicsImpactListener()
        {
            if (_physicsImpactRegistered)
                return;

            RebindPhysicsStateEventService(_cachedPhysicsStateEvents ?? GlobalRegistry.PhysicsStateEvents);
        }

        private void TryUnregisterPhysicsImpactListener()
        {
            if (!_physicsImpactRegistered)
            {
                _cachedPhysicsStateEvents = null;
                return;
            }

            _cachedPhysicsStateEvents?.UnregisterImpactListener(this);
            _cachedPhysicsStateEvents = null;
            _physicsImpactRegistered = false;
        }

        private void RebindPhysicsStateEventService(IPhysicsStateEventService physicsStateEvents)
        {
            if (ReferenceEquals(_cachedPhysicsStateEvents, physicsStateEvents) && _physicsImpactRegistered)
                return;

            if (_physicsImpactRegistered)
                _cachedPhysicsStateEvents?.UnregisterImpactListener(this);

            _cachedPhysicsStateEvents = physicsStateEvents;
            _physicsImpactRegistered = false;

            if (_cachedPhysicsStateEvents == null ||
                !isActiveAndEnabled ||
                !IsPhysicsStateEventServiceUsable(_cachedPhysicsStateEvents))
                return;

            _cachedPhysicsStateEvents.RegisterImpactListener(this);
            _physicsImpactRegistered = true;
        }

        private static bool IsPhysicsStateEventServiceUsable(IPhysicsStateEventService physicsStateEvents)
        {
            return physicsStateEvents != null && physicsStateEvents.IsInitialized;
        }

        private void TryRegisterSaveParticipant()
        {
            if (_saveRegistered || !Application.isPlaying || !isActiveAndEnabled)
                return;

            ISaveService saveService = _cachedSaveService;
            if (!IsSaveServiceUsable(saveService))
            {
                saveService = GlobalRegistry.Save;
                _cachedSaveService = saveService;
            }

            if (!IsSaveServiceUsable(saveService))
                return;

            saveService.Register(this);
            _registeredSaveService = saveService;
            _saveRegistered = true;
        }

        private static bool IsSaveServiceUsable(ISaveService saveService)
        {
            return saveService != null && saveService.IsInitialized;
        }

        private void TryUnregisterSaveParticipant()
        {
            if (!_saveRegistered && _registeredSaveService == null)
                return;

            ISaveService saveService = _registeredSaveService != null ? _registeredSaveService : _cachedSaveService;
            if (saveService != null)
                saveService.Unregister(this);

            _registeredSaveService = null;
            _saveRegistered = false;
        }

        private void RegisterNativeMemorySentinel()
        {
            // GlobalDataVault owns generation descriptor allocation telemetry.
        }

        private static unsafe void DisposeNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);
            System.Exception nativeSentinelCleanupException0 = null;

            try
            {
                NativeMemorySentinel.UnregisterPointer(trackedPointer);
            }
            catch (System.Exception nativeSentinelException0)
            {
                nativeSentinelCleanupException0 = nativeSentinelException0;
            }

            try
            {
                array.Dispose();
            }
            catch (System.Exception nativeSentinelException0)
            {
                if (nativeSentinelCleanupException0 == null)
                    nativeSentinelCleanupException0 = nativeSentinelException0;
            }
            finally
            {
                array = default;
            }

            if (nativeSentinelCleanupException0 != null)
                throw nativeSentinelCleanupException0;
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
                TryAddItemWithState(itemHashId, new ItemState(geneticsMask, qualityMilli));
                return false;
            }

            IPersistentDroppedItemRegistry persistentWorldRegistry = _cachedPersistentWorldRegistry;
            if (persistentWorldRegistry == null ||
                !persistentWorldRegistry.TryRegisterDroppedItemWithState(droppedItem, 1, runtimePosition, geneticsMask, qualityMilli))
            {
                TryAddItemWithState(itemHashId, new ItemState(geneticsMask, qualityMilli));
                return false;
            }

            bool hasInteractorPosition = interactor != null;
            ulong interactorEntityId = hasInteractorPosition ? EntityId.ToULong(interactor.GetEntityId()) : 0ul;
            Vector3 interactorPosition = hasInteractorPosition ? interactor.position : Vector3.zero;
            InteractionEvents.TryRaiseItemLost(droppedItem, 1, interactor);
            ItemLifecycleSignalRoute.TryPublishDiscarded(
                droppedItem,
                1,
                interactorEntityId,
                interactorPosition,
                hasInteractorPosition);

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
                runtimeDescriptor.IsConsumable == 0)
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

        // Refusal-reason bits for DescribeAddRefusalMask. Kept public because the only consumers are cold
        // callers that must PRINT the reason: a bare false from TryAddItem cannot distinguish "this inventory
        // can never store anything for the rest of the session" from "this one item does not fit right now",
        // and every caller that defers on a refusal has to make that distinction to be debuggable.
        public const uint AddRefusalComponentDisabled = 1u << 0;
        public const uint AddRefusalGridMissing = 1u << 1;
        public const uint AddRefusalStackLaneDead = 1u << 2;
        public const uint AddRefusalSimStackLaneDead = 1u << 3;
        public const uint AddRefusalSimOccupancyLaneDead = 1u << 4;
        public const uint AddRefusalCatalogMissing = 1u << 5;
        public const uint AddRefusalHashZero = 1u << 6;
        public const uint AddRefusalDescriptorMissing = 1u << 7;
        public const uint AddRefusalPhysicalCapacity = 1u << 8;
        public const uint AddRefusalGridPlacement = 1u << 9;

        /// <summary>
        /// COLD DIAGNOSTIC ONLY. Names which <see cref="TryAddItem"/> precondition is refusing a single-unit
        /// add; returns 0 when one unit would be accepted.
        ///
        /// Why this exists: <see cref="TryAddItem"/> is the strictest add overload in the class - it gates on
        /// <c>CanAcceptQuantity</c> first, which additionally requires the two PREFLIGHT SCRATCH lanes
        /// (<c>_scavengeSimStackCounts</c>, <c>_simulationOccupiedCells</c>) to be live, while
        /// <c>TryAddItemWithState</c>/<c>TryAddItemWithGenetics</c> skip that gate entirely. Those lanes, and
        /// <c>_stackCounts</c>, are vault-backed and bound exactly once in <c>Awake</c> via
        /// <c>BindPlayerInventoryVaultBuffers</c>. If that bind fails, <c>Awake</c> sets <c>enabled = false</c>
        /// and the loss is PERMANENT for the session: <c>Awake</c> never re-runs on
        /// <c>SetActive(true)</c>, <c>OnEnable</c> never runs so the hot-swap listener is never registered, and
        /// the <c>GlobalRegistryServiceSlot.DataVault</c> case only calls <c>RebindVault</c> - it never
        /// re-allocates. A caller that reads a bare <c>false</c> as "retry later" then waits forever.
        ///
        /// The refusal is honest and must NOT be worked around by switching to a laxer overload: a dead
        /// <c>_stackCounts</c> lane makes the indexer setter a silent no-op (see
        /// <c>InventoryVaultLane{T}</c>), so a laxer add would place the item in the grid, report success, and
        /// still leave <c>CountAvailableTotal</c> at zero - trading a visible refusal for an invisible lie.
        ///
        /// Runs the full physical-capacity fold; never call it from a tick, render, input or UI cadence.
        /// </summary>
        public uint DescribeAddRefusalMask(int itemHashId)
        {
            uint mask = 0u;
            if (!enabled)
                mask |= AddRefusalComponentDisabled;

            if (_grid == null)
                mask |= AddRefusalGridMissing;

            if (!_stackCounts.IsCreated)
                mask |= AddRefusalStackLaneDead;

            if (!_scavengeSimStackCounts.IsCreated)
                mask |= AddRefusalSimStackLaneDead;

            if (!_simulationOccupiedCells.IsCreated)
                mask |= AddRefusalSimOccupancyLaneDead;

            if (itemCatalog == null)
                mask |= AddRefusalCatalogMissing;

            if (itemHashId == 0)
                mask |= AddRefusalHashZero;
            else if (!TryGetRuntimeDescriptor(itemHashId, out ItemCatalog.ItemRuntimeDescriptor runtimeDescriptor))
                mask |= AddRefusalDescriptorMissing;
            else if (!CanAcceptAdditionalPhysicalCapacity(in runtimeDescriptor, 1))
                mask |= AddRefusalPhysicalCapacity;

            // Everything named above passed, so the only remaining refusal inside CanAcceptQuantity is the
            // simulated placement itself - the grid genuinely has no free footprint for this item.
            if (mask == 0u && !CanAcceptQuantity(itemHashId, 1))
                mask |= AddRefusalGridPlacement;

            return mask;
        }

        /// <summary>
        /// O(1) probe naming whether <see cref="TryAddItem"/> can do anything at all right now. These are
        /// exactly the item-independent preconditions <c>CanAcceptQuantity</c> gates on before it touches a
        /// single grid cell, plus the two that make a refusal indistinguishable from "the grid is full":
        /// <c>enabled</c> (<see cref="AddRefusalComponentDisabled"/>) and <c>itemCatalog</c>
        /// (<see cref="AddRefusalCatalogMissing"/>).
        ///
        /// Safe to call from a tick: no allocation and no fold over cells, unlike
        /// <see cref="DescribeAddRefusalMask"/>. Note that <c>IsCreated</c> on a vault lane re-resolves
        /// through the vault instead of reading a cached flag - that is the point. A raised
        /// <c>GlobalDataVault</c> compaction fence makes <c>TryResolveHandle</c> refuse, which flips these
        /// to false and later back to true with no callback firing anywhere, so the only honest way to know
        /// is to ask at the moment of use.
        /// </summary>
        /// <remarks>
        /// KNOWN RESIDUAL - do not "fix" this with a partial refresh. The three IsCreated checks below can
        /// go false long after a successful bind, because ANY other system allocating a vault buffer splits
        /// an arena free block and re-stamps meta.Version for every live buffer
        /// (GlobalDataVault.RebuildMetadataBlockIndices), staling all 49 of this component's cached
        /// descriptors at once. Refreshing only these three lanes here would make this method return true
        /// while the other 46 stay stale, and a write through a stale lane silently no-ops
        /// (InventoryVaultLane's indexer setter returns early when TryAcquireWriteLock refuses) - so
        /// TryAddItem would write the hash and drop the stack count, which is worse than refusing.
        /// Recovery must stay all-or-nothing: whoever notices the refusal calls
        /// <see cref="TryRecoverRuntimeStorageCold"/>, which re-arms every lane together.
        /// </remarks>
        internal bool CanServiceItemAdds()
        {
            return enabled &&
                   _grid != null &&
                   itemCatalog != null &&
                   _stackCounts.IsCreated &&
                   _scavengeSimStackCounts.IsCreated &&
                   _simulationOccupiedCells.IsCreated;
        }

        /// <summary>
        /// COLD. Ensures inventory storage is live, re-arming the vault bind if it is not, and returns
        /// whether <see cref="TryAddItem"/> can now do work. Cheap no-op when storage is already live, so a
        /// caller may treat this as "ensure storage" rather than "repair storage". Never call it from a
        /// per-frame path without a stride - it re-binds ~54 vault lanes.
        ///
        /// WHY THIS HAS TO BE DRIVEN FROM OUTSIDE. <c>Awake</c> binds these lanes exactly once and sets
        /// <c>enabled = false</c> on any refusal. That also guarantees <c>OnEnable</c> never runs, so
        /// <c>TryRegisterHotSwapListener</c> never runs, so the
        /// <c>GlobalRegistryServiceSlot.DataVault</c> notification that would drive a rebind can never
        /// reach this component - and the DataVault case only calls <c>RebindPlayerInventoryVaultReferences</c>
        /// anyway, which re-points at a vault without re-allocating anything. So a bind refusal that was
        /// only ever transient (raised compaction fence, key table or arena momentarily exhausted) became
        /// permanent for the session: every <c>TryAddItem</c> false, every <c>CountAvailableTotal</c> zero.
        /// Recovery therefore belongs to whoever noticed the refusal; <c>PlayerToolManager</c>'s starter
        /// grant is the first consumer to do so.
        ///
        /// Re-enabling the component is deliberate, not a side effect: it is what finally registers the save
        /// participant, the slow/late-frame ticks, the physics-impact listener and the hot-swap listener
        /// that the failed <c>Awake</c> skipped.
        ///
        /// A DTO layout-sovereignty failure is NOT recoverable and is refused without retrying. It is
        /// detectable here without a second latch: that branch is the only one that returns from
        /// <c>Awake</c> before <c>_grid</c> is constructed.
        /// </summary>
        internal bool TryRecoverRuntimeStorageCold()
        {
            if (CanServiceItemAdds())
                return true;

            // _grid == null means Awake has not completed its grid construction. Do not build the grid here:
            // InventoryGrid's constructor THROWS on allocation failure, and this method is reachable from a
            // dispatcher tick, where an escaping throw would amputate the caller's whole lane.
            //
            // This used to be documented as "Awake bailed on the editor-only DTO layout guard", i.e. as a
            // permanent unrecoverable state. It is not that in practice: the layout guard's assertions are
            // satisfied by construction (see the note in Awake) and cannot fail, so the reachable meaning of
            // _grid == null is that Awake has not RUN yet - a consumer polled this component before its
            // Awake, which is ordinary on the AddComponent bootstrap route. Returning false is still correct,
            // but the caller must treat it as "not ready yet" and keep its retry budget alive rather than
            // reading it as the unrecoverable layout verdict and giving up.
            if (_grid == null)
                return false;

            if (!TryBindRuntimeStorageCold())
                return false;

            if (!enabled)
                enabled = true;

            return CanServiceItemAdds();
        }

        /// <summary>
        /// Binds every vault-backed lane and allocates the managed cold scratch this inventory needs.
        /// Idempotent and re-entrant: the single owner of this sequence, called by <c>Awake</c> for the first
        /// bind and by <see cref="TryRecoverRuntimeStorageCold"/> for every re-arm after that.
        /// </summary>
        private bool TryBindRuntimeStorageCold()
        {
            if (_grid == null)
                return false;

            CacheRegistryServicesCold();
            int cellCount = columns * rows;
            if (cellCount <= 0)
            {
                AnnounceRuntimeStorageFailureOnce("grid sizing");
                return false;
            }

            if (!BindPlayerInventoryVaultBuffers(cellCount))
            {
                AnnounceRuntimeStorageFailureOnce("GlobalDataVault lane binding");
                return false;
            }

            if (!AllocateSalinityCorrosionScratchCold(cellCount))
            {
                ReleasePlayerInventoryVaultBuffers();
                AnnounceRuntimeStorageFailureOnce(
                    _salinityScratchFailureDetail == null
                        ? "salinity-corrosion scratch validation"
                        : "salinity-corrosion scratch validation [" + _salinityScratchFailureDetail + "]");
                return false;
            }

            // DO NOT make this unconditional again. The lanes are bound with
            // NativeArrayOptions.UninitializedMemory, so a FIRST bind must clear or the grid reads garbage
            // item hashes and stack counts. But this method is also the re-arm path for
            // TryRecoverRuntimeStorageCold, and a re-bind of buffers that already hold live data returns the
            // SAME pointers untouched (GlobalDataVault hands back the existing block whenever
            // existingMeta.Length >= requiredLength). Clearing on that path silently empties a populated
            // inventory with no log line and no save event - the player's items just cease to exist. So the
            // clear is skipped only when this component has itself already completed a bind-and-clear at this
            // exact cell count and has not released the lanes since; a release, a first bind, or a grid resize
            // all still clear.
            if (!_vaultBuffersInitialized || _boundVaultCellCount != cellCount)
            {
                ClearPlayerInventoryVaultBuffersCold();
                _vaultBuffersInitialized = true;
                _boundVaultCellCount = cellCount;
            }

            RegisterNativeMemorySentinel();
            if (_sortBuffer == null || _sortBuffer.Length != cellCount)
                _sortBuffer = new ItemPlacement[cellCount];
            // COLD ALLOC: ushort[cellCount] - bulk transfer merge-cap scratch - owner: PlayerInventory
            if (_bulkCompactionMaxStackBuffer == null || _bulkCompactionMaxStackBuffer.Length != cellCount)
                _bulkCompactionMaxStackBuffer = new ushort[cellCount];
            // COLD ALLOC: ItemAcquiredSignal[ItemAcquiredSignal.ExpectedCapacity] - late-frame to slow-tick scavenging ingress - owner: PlayerInventory
            if (_pendingScavengingItemSignals == null)
                _pendingScavengingItemSignals = new ItemAcquiredSignal[PendingScavengingItemSignalCapacity];
            // COLD ALLOC: PendingInventoryCommand[16] - late-frame to slow-tick command ingress - owner: PlayerInventory
            if (_pendingInventoryCommands == null)
                _pendingInventoryCommands = new PendingInventoryCommand[PendingInventoryCommandSignalCapacity];
            InitializeSoaQueryEngine(cellCount);

            // LAST STATEMENT ON PURPOSE - do not move this up and do not drop it as a duplicate of the sweep
            // inside BindPlayerInventoryVaultBuffers. That earlier sweep exists so the fail-closed scratch
            // guard and the clear above see live lanes; this one exists because InitializeSoaQueryEngine calls
            // SoaInventoryQueryEngine.EnsureVaultBuffers, which allocates FURTHER vault buffers and therefore
            // re-stales all 49 descriptors that the earlier sweep just repaired. Without this second sweep the
            // scratch guard passes, this method returns true, and CanServiceItemAdds still reports false at the
            // first add - storage that reports itself healthy and refuses every item, which is the exact
            // failure mode this whole path is supposed to make impossible.
            //
            // Anything added below this line that touches the vault needs the sweep moved after it.
            RefreshPlayerInventoryVaultHandlesCold();
            return true;
        }

        private bool _runtimeStorageFailureAnnounced;

        /// <summary>
        /// Names the failing storage step ONCE per component. Cold: string building and Unity object context
        /// are fine here, and the latch is what keeps a strided retry from turning this into log spam.
        ///
        /// <c>Awake</c> now has exactly ONE vault-bind bailout and it DOES route through here, because
        /// <see cref="TryBindRuntimeStorageCold"/> announces before every one of its three false returns
        /// (grid sizing, lane binding, scratch validation). So a bind failure is no longer invisible: this is
        /// the line to look for when the starter-tool grant presents as four assigned prefabs, four valid
        /// catalog entries and no inventory to grant into. Note the latch is per component and never reset,
        /// so a lane bind that fails, is announced, and is later repaired by
        /// <see cref="TryRecoverRuntimeStorageCold"/> leaves one stale error in the log with no paired
        /// recovery line - read this as "storage was dead at least once", not as "storage is dead now".
        /// </summary>
        private void AnnounceRuntimeStorageFailureOnce(string failedStep)
        {
            if (_runtimeStorageFailureAnnounced)
                return;

            _runtimeStorageFailureAnnounced = true;
            Hecton8.Core.H8Debug.LogError(
                "[PlayerInventory] STORAGE UNAVAILABLE - " + failedStep + " failed, so this inventory " +
                "cannot store or report ANY item: every TryAddItem returns false and every " +
                "CountAvailableTotal returns 0 until it binds. object='" + name +
                "' columns=" + columns.ToString() +
                " rows=" + rows.ToString() +
                " cells=" + (columns * rows).ToString() +
                " dataVault=" + (_cachedDataVault != null ? "present" : "NULL") +
                " itemCatalog=" + (itemCatalog != null ? itemCatalog.name : "NULL") +
                " vaultBufferBase=" + _vaultBufferBase.ToString() +
                " gridAllocated=" + (_grid != null ? "yes" : "no") + ".",
                this);
        }

        /// <summary>
        /// Preflights a mixed set of item quantities against one shared grid simulation without mutating live inventory.
        /// </summary>
        public bool CanAcceptItemQuantityBatch(ReadOnlySpan<int> itemHashIds, ReadOnlySpan<int> quantities, int count)
        {
            return CanAcceptQuantityBatch(itemHashIds, quantities, count);
        }

        /// <summary>
        /// Preflights stateful item insertion against one shared grid simulation without mutating live inventory.
        /// </summary>
        public bool CanAcceptItemWithStateBatch(
            ReadOnlySpan<int> itemHashIds,
            ReadOnlySpan<ulong> geneticsMasks,
            ReadOnlySpan<ushort> qualityMillis,
            int count)
        {
            return CanAcceptQuantityWithStateBatch(itemHashIds, geneticsMasks, qualityMillis, count);
        }

        public bool TryAddItemWithGenetics(int itemHashId, ulong geneticsMask, int quantity = 1)
        {

            return TryAddItemWithStateInternal(itemHashId, quantity, new ItemState(geneticsMask), out _);

        }

        public bool TryAddItemWithState(int itemHashId, in ItemState state, int quantity = 1)
        {
            return TryAddItemWithStateInternal(itemHashId, quantity, in state, out _);
        }

        public bool TryAddItemWithState(int itemHashId, in ItemState state, int quantity, out int addedQuantity)
        {
            return TryAddItemWithStateInternal(itemHashId, quantity, in state, out addedQuantity);
        }

        public void SlowTick()
        {
            using (_slowTickProfilerMarker.Auto())
            {
                ApplyDeferredScavengingLootOracleSignals();
                ConsumeDeferredInventoryCommandSignals();
                DrainSalinityBiomeSignals();
                DrainRepairToolTitaniumSignals();
                // L19 hop2 LIVE: skip inventory degradation/corrosion jobs under batch -
                // ApplyInventorySalinityCorrosion native AV (and sibling Burst job paths)
                // during WORLDDRIVER SlowTick.
                if (UnityEngine.Application.isBatchMode)
                {
                    if (_massCacheDirty)
                        RefreshDerivedMassAndSurvivalLoad();
                    return;
                }
                ApplyInventoryEnvironmentalDegradation();
                ApplyInventorySalinityCorrosion();
                ApplyInventoryColdDurabilityDecay();
                ApplyInventoryRadioactiveHalfLife();
                ApplyInventoryReactiveChemistry();
                ApplyInventoryDepthPressureCrush();
                DispatchInventoryRadiationTrauma();
                if (_massCacheDirty)
                    RefreshDerivedMassAndSurvivalLoad();
            }
        }

        public void LateFrameTick()
        {
            CaptureScavengingLootOracleSignals();
            CaptureInventoryCommandSignals();
            FlushEquipmentRustShaderScalar();
        }

        private void PostSimulationTick(in DispatcherTimingDTO timing)
        {
            WriteSoaQueryTelemetryOwnerPhase();
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


            TryAddItemWithStateInternal(itemHashId, quantity, new ItemState(geneticsMask, qualityMilli), out int addedQuantity);

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

            CaptureScavengingLootOracleSignals();
            ApplyDeferredScavengingLootOracleSignals();
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

            int payloadLength = math.clamp(_inventoryShadowPayloadLength, 0, math.min(_inventoryShadowBuffer.Length, InventoryShadowBufferBytes));
            if (payloadLength <= 0)
            {
                data.inventoryShadowPayloadLength = 0;
                data.inventoryShadowPayloadHash = 0u;
                data.hasInventoryShadowPayload = false;
                return;
            }

            if (data.inventoryShadowPayload == null || data.inventoryShadowPayload.Length < payloadLength)
                data.inventoryShadowPayload = new byte[payloadLength];

            for (int i = 0; i < payloadLength; i++)
                data.inventoryShadowPayload[i] = _inventoryShadowBuffer[i];

            data.inventoryShadowPayloadLength = payloadLength;
            data.inventoryShadowPayloadHash = _inventoryShadowHash;
            data.hasInventoryShadowPayload = true;
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
            int frame = SystemDispatcher.CurrentFrameIndex;
            SignalBus<InventoryCommandSignal>.TryPushTracked(new InventoryCommandSignal
            {
                InventoryHash = ResolveInventorySignalHash(),
                Frame = unchecked((uint)frame),
                Sequence = unchecked((uint)InventoryVersion),
                Command = InventoryCommandSignalCommands.Sort,
                Flags = 0
            }, ref _signalPushDropCount);
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
                InventoryDefragCommand sortCommand = new InventoryDefragCommand
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
                };
                sortCommand.Execute();
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

        private void ConsumeDeferredInventoryCommandSignals()
        {
            int count = _pendingInventoryCommandCount;
            if (count <= 0)
                return;

            PendingInventoryCommand[] commands = _pendingInventoryCommands;
            _pendingInventoryCommandCount = 0;
            if (commands == null)
            {
                RecordDroppedInventoryCommandSignals(count);
                return;
            }

            int safeCount = math.min(count, commands.Length);
            bool shouldSort = false;
            for (int index = 0; index < safeCount; index++)
            {
                PendingInventoryCommand pending = commands[index];
                commands[index] = default;
                InventoryCommandSignal command = pending.Command;

                if (command.Command == InventoryCommandSignalCommands.DropNonEquippedResources)
                {
                    TryApplyRespawnDropPenalty(
                        in command,
                        pending.HasDeferredDeathAup != 0,
                        in pending.DeferredDeathAup);
                    continue;
                }

                if (command.Command != InventoryCommandSignalCommands.Sort)
                    continue;

                int commandFrame = unchecked((int)command.Frame);
                if (commandFrame <= _lastInventorySortCommandFrame)
                    continue;

                _lastInventorySortCommandFrame = commandFrame;
                shouldSort = true;
            }

            if (count > safeCount)
                RecordDroppedInventoryCommandSignals(count - safeCount);

            if (shouldSort)
                SortInventory();
        }

        private bool TryApplyRespawnDropPenalty(in InventoryCommandSignal command)
        {
            double3 deferredDeathAup = default;
            return TryApplyRespawnDropPenalty(in command, false, in deferredDeathAup);
        }

        private bool TryApplyRespawnDropPenalty(
            in InventoryCommandSignal command,
            bool hasDeferredDeathAup,
            in double3 deferredDeathAup)
        {
            if (_grid == null || !_stackCounts.IsCreated)
            {
                PublishRespawnDropPenaltyResult(in command, 0);
                return false;
            }

            int sourceCells = math.min(_grid.TotalCells, _stackCounts.Length);
            int dropBudget = ResolveRespawnDropBudget(command.Flags);
            bool requiresRuleTable = (command.PayloadFlags & InventoryCommandSignalPayloadFlags.VaultPenaltyRules) != 0;
            bool hasRuleTable = TryResolveRespawnPenaltyRules(in command, out NativeArray<InventoryDeathPenaltyRuleDTO> rules, out int ruleCount);
            if (requiresRuleTable &&
                !hasRuleTable &&
                (command.PayloadFlags & InventoryCommandSignalPayloadFlags.FallbackWhenRuleTableMissing) == 0)
            {
                PublishRespawnDropPenaltyResult(in command, 0);
                return false;
            }

            double3 deathAup = deferredDeathAup;
            bool hasDeathAup = hasDeferredDeathAup || TryResolveRespawnDeathAup(in command, out deathAup);
            if (!hasDeathAup)
            {
                PublishRespawnDropPenaltyResult(in command, 0);
                return false;
            }

            bool dropped = false;
            int droppedCount = 0;

            for (int anchorIndex = 0; anchorIndex < sourceCells && dropBudget > 0; anchorIndex++)
            {
                if (!_grid.HasAnchor(anchorIndex) || IsCraftLockedFlagSet(anchorIndex))
                    continue;

                int itemHashId = _grid.GetAnchorHashId(anchorIndex);
                if (!TryGetRuntimeDescriptor(itemHashId, out ItemCatalog.ItemRuntimeDescriptor runtimeDescriptor))
                    continue;

                byte category = runtimeDescriptor.CategoryId;
                if (hasRuleTable)
                {
                    uint itemHash = unchecked((uint)itemHashId);
                    if (!TryFindRespawnPenaltyRule(itemHash, rules, ruleCount, out InventoryDeathPenaltyRuleDTO rule) ||
                        rule.DropOnDeath == 0 ||
                        ShouldRetainRespawnPenaltyItem(itemHash, rule.RetainIfEquipped, category))
                    {
                        continue;
                    }
                }
                else if (category == (byte)ItemCategory.Tool || category == (byte)ItemCategory.Equipment)
                {
                    continue;
                }

                int anchorX = anchorIndex % _grid.Columns;
                int anchorY = anchorIndex / _grid.Columns;
                float3 localOffset = ResolveRespawnLootScatterOffset(anchorIndex);
                bool didDrop = TryDropOneItemToDeathLootCacheSignal(anchorX, anchorY, deathAup, localOffset, in command, out _);

                if (!didDrop)
                    continue;

                dropBudget--;
                dropped = true;
                droppedCount++;
            }

            PublishRespawnDropPenaltyResult(in command, droppedCount);
            return dropped;
        }

        private bool TryResolveRespawnDeathAup(in InventoryCommandSignal command, out double3 deathAup)
        {
            deathAup = default;
            if (command.Sequence == 0u)
                return false;

            if ((command.PayloadFlags & InventoryCommandSignalPayloadFlags.RespawnDeathAupSideband) != 0 &&
                TryResolveRespawnDeathAupSideband(in command, out deathAup))
            {
                return true;
            }

            ReadOnlySpan<PlayerRespawnSignal> signals = SignalBus<PlayerRespawnSignal>.GetFrameSnapshot();
            uint inventoryHash = ResolveInventorySignalHash();
            for (int i = 0; i < signals.Length; i++)
            {
                PlayerRespawnSignal signal = signals[i];
                if (signal.Sequence != command.Sequence ||
                    (signal.PlayerHash != 0u && signal.PlayerHash != inventoryHash) ||
                    (signal.Flags & PlayerRespawnSignalFlags.InvalidDeathAup) != 0u ||
                    !math.all(math.isfinite(signal.DeathAUP)))
                {
                    continue;
                }

                deathAup = signal.DeathAUP;
                return true;
            }

            return false;
        }

        private bool TryResolveRespawnDeathAupSideband(in InventoryCommandSignal command, out double3 deathAup)
        {
            deathAup = default;
            ReadOnlySpan<InventoryRespawnDeathAupSignal> signals = SignalBus<InventoryRespawnDeathAupSignal>.GetFrameSnapshot();
            uint inventoryHash = ResolveInventorySignalHash();
            for (int i = 0; i < signals.Length; i++)
            {
                InventoryRespawnDeathAupSignal signal = signals[i];
                if (signal.Sequence != command.Sequence ||
                    (signal.InventoryHash != 0u && signal.InventoryHash != inventoryHash) ||
                    (signal.Flags & 0x80000000u) != 0u ||
                    !math.all(math.isfinite(signal.DeathAUP)))
                {
                    continue;
                }

                deathAup = signal.DeathAUP;
                return true;
            }

            return false;
        }

        private static float3 ResolveRespawnLootScatterOffset(int anchorIndex)
        {
            float angle = ((anchorIndex & 7) * 0.78539816339f) + 0.39269908169f;
            float radius = 0.35f + ((anchorIndex & 3) * 0.08f);
            MathLodApproximation.ApproxSinCosBhaskara(angle, out float sin, out float cos);
            float3 offset = default;
            offset.x = cos * radius;
            offset.y = 0.15f;
            offset.z = sin * radius;
            return offset;
        }

        private static void PublishRespawnDropPenaltyResult(in InventoryCommandSignal command, int droppedCount)
        {
            if (command.Sequence == 0u)
                return;

            InventoryRespawnPenaltyResultSignal result = default;
            result.InventoryHash = command.InventoryHash;
            result.Frame = command.Frame;
            result.Sequence = command.Sequence;
            result.DroppedCount = (uint)math.clamp(droppedCount, 0, 255);
            result.Flags = droppedCount > 0 ? 1u : 0u;
            SignalBus<InventoryRespawnPenaltyResultSignal>.TryPushTracked(in result, ref _signalPushDropCount);
        }

        private static int ResolveRespawnDropBudget(byte encodedMultiplier)
        {
            return math.clamp((int)math.ceil(math.max(1, encodedMultiplier) * (3f / 255f)), 1, 3);
        }

        private bool TryResolveRespawnPenaltyRules(
            in InventoryCommandSignal command,
            out NativeArray<InventoryDeathPenaltyRuleDTO> rules,
            out int ruleCount)
        {
            rules = default;
            ruleCount = 0;
            if ((command.PayloadFlags & InventoryCommandSignalPayloadFlags.VaultPenaltyRules) == 0 ||
                command.Payload0 == 0u ||
                command.Payload1 == 0u)
            {
                return false;
            }

            IDataVault vault = _cachedDataVault;
            BufferID rulesBufferId = (BufferID)command.Payload0;
            if (vault == null ||
                !vault.TryGetGenerationHandle<InventoryDeathPenaltyRuleDTO>(rulesBufferId, out VaultGenerationHandle<InventoryDeathPenaltyRuleDTO> rulesHandle) ||
                rulesHandle.BufferID != unchecked((uint)(int)rulesBufferId) ||
                !vault.TryReadHandle(in rulesHandle, out rules) ||
                !rules.IsCreated)
            {
                rules = default;
                return false;
            }

            int requestedCount = command.Payload1 > int.MaxValue ? int.MaxValue : (int)command.Payload1;
            ruleCount = math.min(math.max(0, requestedCount), rules.Length);
            return ruleCount > 0;
        }

        private static bool TryFindRespawnPenaltyRule(
            uint itemHash,
            NativeArray<InventoryDeathPenaltyRuleDTO> rules,
            int ruleCount,
            out InventoryDeathPenaltyRuleDTO rule)
        {
            rule = default;
            if (!rules.IsCreated || itemHash == 0u)
                return false;

            int count = math.min(math.max(0, ruleCount), rules.Length);
            for (int i = 0; i < count; i++)
            {
                InventoryDeathPenaltyRuleDTO candidate = rules[i];
                if (candidate.ItemHash != itemHash)
                    continue;

                rule = candidate;
                return true;
            }

            return false;
        }

        private bool ShouldRetainRespawnPenaltyItem(uint itemHash, byte retainIfEquipped, byte category)
        {
            if (retainIfEquipped == 0)
                return false;

            if (category == (byte)ItemCategory.Equipment)
                return true;

            return category == (byte)ItemCategory.Tool &&
                   TryResolveCurrentToolItemHash(out uint currentToolHash) &&
                   currentToolHash == itemHash;
        }

        private bool TryResolveCurrentToolItemHash(out uint itemHash)
        {
            itemHash = 0u;
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            PlayerToolManager toolManager = playerContext != null ? playerContext.ToolManager : null;
            PlayerTool currentTool = toolManager != null ? toolManager.CurrentTool : null;
            if (currentTool == null || currentTool.ToolData == null)
                return false;

            itemHash = unchecked((uint)ItemData.ResolvePersistentHashId(currentTool.ToolData));
            return itemHash != 0u;
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
                _defragFlags[count] = descriptor.Stackable != 0 ? (byte)0x01 : (byte)0x00;
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
            PublishSoaQueryVaultSnapshotOwnerPhase();
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
            return InventoryGrid.IsValidDescriptor(in descriptor);
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
            SignalBus<ToolAcousticSignal>.TryPushTracked(new ToolAcousticSignal
            {
                ToolHash = _InventorySortToolHash,
                TargetHash = _InventoryUiClickHash,
                Progress01 = 1f,
                PitchScale = 1f,
                Intensity01 = 0.55f,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                State = 1,
                Flags = 0
            }, ref _signalPushDropCount);
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
            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition dropAup))
                return false;

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
                SignalBus<DebrisSpawnSignal>.TryPushTracked(new DebrisSpawnSignal
                {
                    PositionAup = dropAup,
                    SpeciesHash = hash,
                    SourceEntityId = 0u,
                    Intensity01 = math.saturate(count * 0.02f),
                    DebrisKind = 4,
                    Flags = 0,
                    Quantity = count
                }, ref _signalPushDropCount);
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

            return TryBulkCopyLaneToNative(in _inventoryShadowBuffer, 0, destination, 0, payloadLength);
        }

        private void PrepareBulkTransferCaches()
        {
            RefreshInventorySoAMirrorsAndMask();
            PublishSoaQueryVaultSnapshotOwnerPhase();
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

            if (targetInventory == null ||
                !_itemHashes.TryResolve(out NativeArray<uint> sourceHashes) ||
                !_stackCounts.TryResolve(out NativeArray<ushort> sourceCounts) ||
                !_anchorUnitMassKg.TryResolve(out NativeArray<float> sourceUnitMassKg) ||
                !_anchorUnitVolumeM3.TryResolve(out NativeArray<float> sourceUnitVolumeM3) ||
                !targetInventory._itemHashes.TryResolve(out NativeArray<uint> targetHashes) ||
                !targetInventory._stackCounts.TryResolve(out NativeArray<ushort> targetCounts) ||
                sourceStartIndex < 0 ||
                targetStartIndex < 0 ||
                slotCount <= 0 ||
                sourceStartIndex + slotCount > sourceHashes.Length ||
                sourceStartIndex + slotCount > sourceCounts.Length ||
                sourceStartIndex + slotCount > sourceUnitMassKg.Length ||
                sourceStartIndex + slotCount > sourceUnitVolumeM3.Length ||
                targetStartIndex + slotCount > targetHashes.Length ||
                targetStartIndex + slotCount > targetCounts.Length)
            {
                return false;
            }

            float transferWeightKg = 0f;
            float transferVolumeLiters = 0f;
            int movedSlotCount = 0;
            int movedStackCount = 0;

            for (int offset = 0; offset < slotCount; offset++)
            {
                int sourceIndex = sourceStartIndex + offset;
                uint hash = sourceHashes[sourceIndex];
                ushort count = sourceCounts[sourceIndex];
                if (hash == 0u || count == 0)
                    continue;

                int targetIndex = targetStartIndex + offset;
                if (targetHashes[targetIndex] != 0u ||
                    targetCounts[targetIndex] != 0)
                {
                    result = InventorySoAUtility.BulkTransferResult.Failed(InventorySoAUtility.TransferFailureCode.TargetOccupied);
                    return false;
                }

                float unitMassKg = math.max(0f, sourceUnitMassKg[sourceIndex]);
                float unitVolumeLiters = math.max(0f, sourceUnitVolumeM3[sourceIndex]) * 1000f;
                transferWeightKg += unitMassKg * count;
                transferVolumeLiters += unitVolumeLiters * count;
                movedSlotCount++;
                movedStackCount += count;
            }

            if (movedSlotCount == 0)
            {
                result = InventorySoAUtility.BulkTransferResult.Failed(InventorySoAUtility.TransferFailureCode.SourceEmpty);
                return false;
            }

            float nextWeightKg = targetInventory._currentWeightKg + transferWeightKg;
            if (targetInventory.MaxWeightKg >= 0f && nextWeightKg > targetInventory.MaxWeightKg)
            {
                result = new InventorySoAUtility.BulkTransferResult(
                    InventorySoAUtility.TransferFailureCode.WeightLimit,
                    movedSlotCount,
                    movedStackCount,
                    transferWeightKg,
                    transferVolumeLiters,
                    nextWeightKg,
                    targetInventory._currentVolumeLiters + transferVolumeLiters);
                return false;
            }

            float nextVolumeLiters = targetInventory._currentVolumeLiters + transferVolumeLiters;
            if (targetInventory.MaxVolumeLiters >= 0f && nextVolumeLiters > targetInventory.MaxVolumeLiters)
            {
                result = new InventorySoAUtility.BulkTransferResult(
                    InventorySoAUtility.TransferFailureCode.VolumeLimit,
                    movedSlotCount,
                    movedStackCount,
                    transferWeightKg,
                    transferVolumeLiters,
                    nextWeightKg,
                    nextVolumeLiters);
                return false;
            }

            result = new InventorySoAUtility.BulkTransferResult(
                InventorySoAUtility.TransferFailureCode.None,
                movedSlotCount,
                movedStackCount,
                transferWeightKg,
                transferVolumeLiters,
                nextWeightKg,
                nextVolumeLiters);
            return true;
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
            return TryBulkCopyLaneToLane(in _itemHashes, sourceStartIndex, ref targetInventory._itemHashes, targetStartIndex, slotCount) &&
                   TryBulkCopyLaneToLane(in _stackCounts, sourceStartIndex, ref targetInventory._stackCounts, targetStartIndex, slotCount) &&
                   TryBulkCopyLaneToLane(in _itemCondition, sourceStartIndex, ref targetInventory._itemCondition, targetStartIndex, slotCount) &&
                   TryBulkCopyLaneToLane(in _itemStateFlags, sourceStartIndex, ref targetInventory._itemStateFlags, targetStartIndex, slotCount) &&
                   TryBulkCopyLaneToLane(in _itemGenetics, sourceStartIndex, ref targetInventory._itemGenetics, targetStartIndex, slotCount) &&
                   TryBulkCopyLaneToLane(in _qualityMilli, sourceStartIndex, ref targetInventory._qualityMilli, targetStartIndex, slotCount) &&
                   TryBulkCopyLaneToLane(in _durabilities, sourceStartIndex, ref targetInventory._durabilities, targetStartIndex, slotCount) &&
                   TryBulkCopyLaneToLane(in _lastUpdateUnixSeconds, sourceStartIndex, ref targetInventory._lastUpdateUnixSeconds, targetStartIndex, slotCount) &&
                   TryBulkCopyLaneToLane(in _anchorUnitMassKg, sourceStartIndex, ref targetInventory._anchorUnitMassKg, targetStartIndex, slotCount) &&
                   TryBulkCopyLaneToLane(in _anchorUnitVolumeM3, sourceStartIndex, ref targetInventory._anchorUnitVolumeM3, targetStartIndex, slotCount) &&
                   TryBulkCopyLaneToLane(in _anchorUnitRadiationSv, sourceStartIndex, ref targetInventory._anchorUnitRadiationSv, targetStartIndex, slotCount);
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

            TryClearLaneSlice(ref _itemHashes, startIndex, slotCount);
            TryClearLaneSlice(ref _stackCounts, startIndex, slotCount);
            TryClearLaneSlice(ref _itemCondition, startIndex, slotCount);
            TryClearLaneSlice(ref _craftLockedCounts, startIndex, slotCount);
            TryClearLaneSlice(ref _anchorStateFlags, startIndex, slotCount);
            TryClearLaneSlice(ref _itemStateFlags, startIndex, slotCount);
            TryClearLaneSlice(ref _itemGenetics, startIndex, slotCount);
            TryClearLaneSlice(ref _qualityMilli, startIndex, slotCount);
            TryClearLaneSlice(ref _durabilities, startIndex, slotCount);
            TryClearLaneSlice(ref _lastUpdateUnixSeconds, startIndex, slotCount);
            TryClearLaneSlice(ref _anchorUnitMassKg, startIndex, slotCount);
            TryClearLaneSlice(ref _anchorUnitVolumeM3, startIndex, slotCount);
            TryClearLaneSlice(ref _anchorUnitRadiationSv, startIndex, slotCount);
        }

        private bool TryCompactIdenticalHashesAfterBulkTransfer()
        {
            if (!TryBuildBulkCompactedPlacements(out int placementCount))
                return false;

            if (!CanApplyPlacementsFirstFit(_sortBuffer, placementCount))
                return false;

            return TryApplyPlacementsFirstFit(_sortBuffer, placementCount);
        }

        private bool TryBuildBulkCompactedPlacements(out int placementCount)
        {
            placementCount = 0;
            if (_grid == null ||
                _sortBuffer == null ||
                _bulkCompactionMaxStackBuffer == null ||
                !_itemHashes.TryReadOnly(out NativeArray<uint>.ReadOnly itemHashes) ||
                !_stackCounts.TryReadOnly(out NativeArray<ushort>.ReadOnly itemCounts) ||
                !_itemCondition.TryReadOnly(out NativeArray<float>.ReadOnly itemCondition) ||
                !_itemStateFlags.TryReadOnly(out NativeArray<ushort>.ReadOnly itemStateFlags) ||
                !_itemGenetics.TryReadOnly(out NativeArray<byte>.ReadOnly itemGenetics) ||
                !_qualityMilli.TryReadOnly(out NativeArray<ushort>.ReadOnly qualityMilli) ||
                !_durabilities.TryReadOnly(out NativeArray<byte>.ReadOnly durabilities) ||
                !_lastUpdateUnixSeconds.TryReadOnly(out NativeArray<uint>.ReadOnly lastUpdateUnixSeconds) ||
                !_anchorUnitMassKg.TryReadOnly(out NativeArray<float>.ReadOnly unitMassKg) ||
                !_anchorUnitVolumeM3.TryReadOnly(out NativeArray<float>.ReadOnly unitVolumeM3) ||
                !_anchorUnitRadiationSv.TryReadOnly(out NativeArray<float>.ReadOnly unitRadiationSv))
            {
                return false;
            }

            NativeArray<ushort>.ReadOnly maxStackCounts = _grid.AnchorMaxStacks;
            int count = itemHashes.Length;
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
            if (maxStackCounts.IsCreated)
                count = math.min(count, maxStackCounts.Length);

            int scratchCapacity = math.min(_sortBuffer.Length, _bulkCompactionMaxStackBuffer.Length);
            for (int index = 0; index < count && placementCount < scratchCapacity; index++)
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
                _bulkCompactionMaxStackBuffer[placementCount - 1] = ResolveBulkMergeMaxStack(maxStackCounts, index);
            }

            placementCount = CompactBulkTransferPlacements(_sortBuffer, _bulkCompactionMaxStackBuffer, placementCount);
            return true;
        }

        private static ushort ResolveBulkMergeMaxStack(NativeArray<ushort>.ReadOnly maxStackCounts, int index)
        {
            if (!maxStackCounts.IsCreated || (uint)index >= (uint)maxStackCounts.Length)
                return ushort.MaxValue;

            ushort maxStack = maxStackCounts[index];
            return maxStack == 0 ? (ushort)1 : maxStack;
        }

        private static int CompactBulkTransferPlacements(ItemPlacement[] placements, ushort[] mergeMaxStacks, int placementCount)
        {
            if (placements == null ||
                mergeMaxStacks == null ||
                placementCount <= 0)
            {
                return 0;
            }

            int count = math.min(placementCount, math.min(placements.Length, mergeMaxStacks.Length));
            for (int primary = 0; primary < count; primary++)
            {
                ItemPlacement primaryPlacement = placements[primary];
                int hash = primaryPlacement.itemHashId;
                ushort primaryCount = primaryPlacement.stackCount;
                ushort maxStack = mergeMaxStacks[primary];
                if (hash == 0 || primaryCount == 0 || maxStack <= 1)
                    continue;

                for (int candidate = primary + 1; candidate < count && primaryCount < maxStack; candidate++)
                {
                    ItemPlacement candidatePlacement = placements[candidate];
                    if (!CanMergeBulkTransferPlacement(in primaryPlacement, in candidatePlacement, mergeMaxStacks[candidate], hash))
                        continue;

                    ushort candidateCount = candidatePlacement.stackCount;
                    int capacity = math.max(0, maxStack - primaryCount);
                    int transfer = math.min(capacity, candidateCount);
                    if (transfer <= 0)
                        continue;

                    primaryCount = (ushort)(primaryCount + transfer);
                    candidateCount = (ushort)(candidateCount - transfer);
                    primaryPlacement.stackCount = primaryCount;
                    candidatePlacement.stackCount = candidateCount;
                    primaryPlacement.qualityMilli = (ushort)math.max((int)primaryPlacement.qualityMilli, (int)candidatePlacement.qualityMilli);
                    primaryPlacement.durability = (byte)math.max((int)primaryPlacement.durability, (int)candidatePlacement.durability);
                    primaryPlacement.lastUpdateUnixSeconds = math.max(primaryPlacement.lastUpdateUnixSeconds, candidatePlacement.lastUpdateUnixSeconds);
                    placements[primary] = primaryPlacement;

                    if (candidateCount == 0)
                    {
                        candidatePlacement = default;
                        mergeMaxStacks[candidate] = 0;
                    }
                    placements[candidate] = candidatePlacement;
                }
            }

            int writeIndex = 0;
            for (int readIndex = 0; readIndex < count; readIndex++)
            {
                ItemPlacement placement = placements[readIndex];
                if (placement.itemHashId == 0 || placement.stackCount == 0)
                    continue;

                if (writeIndex != readIndex)
                {
                    placements[writeIndex] = placement;
                    mergeMaxStacks[writeIndex] = mergeMaxStacks[readIndex];
                    placements[readIndex] = default;
                    mergeMaxStacks[readIndex] = 0;
                }

                writeIndex++;
            }

            for (int clearIndex = writeIndex; clearIndex < count; clearIndex++)
            {
                placements[clearIndex] = default;
                mergeMaxStacks[clearIndex] = 0;
            }

            return writeIndex;
        }

        private static bool CanMergeBulkTransferPlacement(in ItemPlacement primary, in ItemPlacement candidate, ushort candidateMergeMaxStack, int hash)
        {
            return candidate.itemHashId == hash &&
                   candidate.stackCount > 0 &&
                   candidateMergeMaxStack > 1 &&
                   candidate.stateFlags == primary.stateFlags &&
                   candidate.geneticsMask == primary.geneticsMask &&
                   candidate.qualityMilli == primary.qualityMilli;
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
                if (!InventoryGrid.IsValidDescriptor(in descriptor) || !TryReservePlacementInSimulation(in descriptor))
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
                if (!InventoryGrid.IsValidDescriptor(in descriptor) || !_grid.TryAddItem(in descriptor, out int placedX, out int placedY))
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
            PublishSoaQueryVaultSnapshotOwnerPhase();
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
            return math.isfinite(runtimePosition.x) &&
                   math.isfinite(runtimePosition.y) &&
                   math.isfinite(runtimePosition.z);
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition absoluteAup)
        {
            absoluteAup = default;
            if (!IsFiniteRuntimePosition(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            absoluteAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return absoluteAup.IsFinite();
        }

        private void PublishBulkTransferAudio(float transferWeightKg)
        {
            if (transferWeightKg < HeavyBulkTransferAudioThresholdKg)
                return;

            float inverseTransferWeight = math.rcp(math.max(HeavyBulkTransferAudioThresholdKg, transferWeightKg));
            SignalBus<ToolAcousticSignal>.TryPushTracked(new ToolAcousticSignal
            {
                ToolHash = _InventoryBulkTransferToolHash,
                TargetHash = _HeavyThudTargetHash,
                Progress01 = 1f,
                PitchScale = math.lerp(0.65f, 0.95f, math.saturate(HeavyBulkTransferAudioThresholdKg * inverseTransferWeight)),
                Intensity01 = math.saturate(transferWeightKg * 0.01f),
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                State = 2,
                Flags = 0
            }, ref _signalPushDropCount);
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

        public NativeArray<ushort>.ReadOnly GetQuantitiesReadOnly()
        {
            return GetStackCountsReadOnly();
        }

        public NativeArray<byte>.ReadOnly GetDurabilitiesReadOnly()
        {
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

            return TryAddItemWithStateInternal(itemHashId, quantity, new ItemState(0UL), out addedQuantity);

        }

        private bool TryAddItemWithStateInternal(
            int itemHashId,
            int quantity,
            in ItemState state,
            out int addedQuantity)
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
            ushort resolvedQualityMilli = NormalizeQualityMilli(state.QualityMilli);

            ushort resolvedStateFlags = state.HasExplicitFlags ? state.Flags : runtimeDescriptor.StateFlags;

            byte compressedGenetics = CompressItemGenetics(state.GeneticsMask);

            int requestedQuantity = quantity;
            if (!TryResolveCapacityLimitedQuantity(in runtimeDescriptor, requestedQuantity, out quantity))
            {
                InventoryEvents.TryNotifyInventoryFull(itemHashId);
                return false;
            }

            bool allAdded = quantity == requestedQuantity;
            int remainingQuantity = quantity;
            if (descriptor.Stackable != 0)
            {
                int stackedQuantity = TryStackQuantityWithState(
                    descriptor.HashId,
                    descriptor.MaxStack,
                    resolvedStateFlags,
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
                int quantityForSlot = descriptor.Stackable != 0
                    ? math.min(math.max(1, (int)descriptor.MaxStack), remainingQuantity)
                    : 1;
                if (_grid.TryAddItem(in descriptor, out int placedX, out int placedY))
                {
                    int anchorIndex = AnchorIndex(placedX, placedY);
                    _stackCounts[anchorIndex] = (ushort)quantityForSlot;
                    _itemStateFlags[anchorIndex] = resolvedStateFlags;
                    _itemGenetics[anchorIndex] = compressedGenetics;
                    _qualityMilli[anchorIndex] = resolvedQualityMilli;
                    if (_itemDurability.IsCreated && (uint)anchorIndex < (uint)_itemDurability.Length)
                        _itemDurability[anchorIndex] = math.saturate(resolvedQualityMilli * 0.001f);
                    _lastUpdateUnixSeconds[anchorIndex] = (resolvedStateFlags & BiologicalItemStateMask) != 0 ? timestampNow : 0u;
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
                InventoryEvents.TryNotifyInventoryFull(itemHashId);

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
            if (descriptor.Stackable != 0)
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

                remaining -= descriptor.Stackable != 0
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

                if (descriptor.Stackable != 0)
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

                    remaining -= descriptor.Stackable != 0
                        ? math.min(math.max(1, (int)descriptor.MaxStack), remaining)
                        : 1;
                }
            }

            return true;
        }

        private bool CanAcceptQuantityWithStateBatch(
            ReadOnlySpan<int> itemHashIds,
            ReadOnlySpan<ulong> geneticsMasks,
            ReadOnlySpan<ushort> qualityMillis,
            int count)
        {
            if (_grid == null ||
                count < 0 ||
                itemHashIds.Length < count ||
                geneticsMasks.Length < count ||
                qualityMillis.Length < count ||
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
                if (itemHashId == 0 ||
                    !TryBuildDescriptor(itemHashId, out InventoryGrid.InventoryItemDescriptor descriptor) ||
                    !TryGetRuntimeDescriptor(itemHashId, out ItemCatalog.ItemRuntimeDescriptor runtimeDescriptor) ||
                    !TryResolveAdditionalPhysicalDemand(in runtimeDescriptor, 1, out float groupWeightKg, out float groupVolumeLiters))
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

                int remaining = 1;
                ushort resolvedStateFlags = runtimeDescriptor.StateFlags;
                byte compressedGenetics = CompressItemGenetics(geneticsMasks[groupIndex]);
                ushort resolvedQualityMilli = NormalizeQualityMilli(qualityMillis[groupIndex]);
                if (descriptor.Stackable != 0)
                {
                    for (int anchorIndex = 0; anchorIndex < _stackCounts.Length && remaining > 0; anchorIndex++)
                    {
                        if (!_grid.HasAnchor(anchorIndex) ||
                            _grid.GetAnchorHashId(anchorIndex) != descriptor.HashId ||
                            IsCraftLockedFlagSet(anchorIndex) ||
                            !CanStackStatefulItemAt(anchorIndex, resolvedStateFlags, compressedGenetics, resolvedQualityMilli))
                        {
                            continue;
                        }

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

                    remaining -= descriptor.Stackable != 0
                        ? math.min(math.max(1, (int)descriptor.MaxStack), remaining)
                        : 1;
                }
            }

            return true;
        }

        private bool CanStackStatefulItemAt(
            int anchorIndex,
            ushort itemStateFlags,
            byte geneticsMask,
            ushort qualityMilli)
        {
            return (!_itemStateFlags.IsCreated || _itemStateFlags[anchorIndex] == itemStateFlags) &&
                   (!_itemGenetics.IsCreated || _itemGenetics[anchorIndex] == geneticsMask) &&
                   (!_qualityMilli.IsCreated || NormalizeQualityMilli(_qualityMilli[anchorIndex]) == qualityMilli);
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

            if (TryCountQuantityByHashSoa(itemHashId, availableOnly, out int soaTotal))
                return soaTotal;

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
                runtimeDescriptor.Stackable != 0);
            return InventoryGrid.IsValidDescriptor(in descriptor);
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
                if (!InventoryGrid.IsValidDescriptor(in descriptor) || !_grid.PlaceAt(in descriptor, placement.x, placement.y))
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
            _durabilitySnapshotDirty = true;
            RefreshInventorySoAMirrorsAndMask();
            SyncDurabilityBytesFromQuality();
            PublishSoaQueryVaultSnapshotOwnerPhase();

            if (markDirty)
            {
                MarkInventoryDirty();
                RefreshInventoryShadowBufferFromRuntime();
            }

            if (massDirty)
                MarkMassCacheDirty();

            if (_massCacheDirty)
                RefreshDerivedMassAndSurvivalLoad();

            PublishEncumbranceChanged();
            InventoryVersion++;
            InventoryEvents.TryNotifyInventoryChanged();
            SignalBus<InventoryChangedSignal>.TryPushTracked(new InventoryChangedSignal
            {
                InventoryHash = ResolveInventorySignalHash(),
                Revision = unchecked((uint)InventoryVersion),
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                OccupiedCells = _grid != null ? (ushort)math.clamp(_grid.OccupiedCells, 0, ushort.MaxValue) : (ushort)0,
                Flags = 0,
                TotalMassKg = math.isfinite(TotalMassKg) ? math.max(0f, TotalMassKg) : 0f,
                CarryCapacityKg = ResolveCarryCapacityKilograms(),
                Load01 = math.isfinite(CachedInventoryLoad01) ? math.saturate(CachedInventoryLoad01) : 0f
            }, ref _signalPushDropCount);
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
            UIStateStore.WriteInventoryLoadState(TotalMassKg, carryCapacityKg, CachedInventoryLoad01, (float)Hecton8.Core.SystemDispatcher.CurrentUnscaledTimeSeconds);
            InventoryEvents.TryNotifyEncumbranceChanged(new EncumbranceChangedEvent(
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

            _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Player);
        }

        private void TryRegisterPostSimulationDispatcher()
        {
            if (_registeredPostSimulationDispatcher)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (_postSimulationPhase == null)
                _postSimulationPhase = new PostSimulationPhaseSystem(this); // COLD ALLOC: IDispatcherSystem[1] - inventory SoA telemetry post-simulation bridge - owner: PlayerInventory

            _registeredPostSimulationDispatcher = GlobalRegistry.TryRegisterDispatcherSystem(_postSimulationPhase);
        }

        private void TryUnregisterPostSimulationDispatcher()
        {
            if (!_registeredPostSimulationDispatcher)
                return;

            GlobalRegistry.UnregisterDispatcherSystem(_postSimulationPhase);
            _registeredPostSimulationDispatcher = false;
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

            _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterLateFrameTick()
        {
            if (!_registeredLateFrameTick)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _registeredLateFrameTick = false;
        }

        private sealed class PostSimulationPhaseSystem : IDispatcherSystem
        {
            private readonly PlayerInventory _owner;

            public PostSimulationPhaseSystem(PlayerInventory owner)
            {
                _owner = owner;
            }

            public uint GetSystemIdHash() => _postSimulationSystemHash;

            public DispatcherPhase GetDispatcherPhase() => DispatcherPhase.PostSimulation;

            public byte GetBucketId() => byte.MaxValue;

            public int GetDependencyCount() => 0;

            public uint GetDependencyHash(int dependencyIndex) => 0u;

            public JobHandle ScheduleSimulation(
                in DispatcherTimingDTO timing,
                in DispatcherJobContext context,
                JobHandle dependsOn)
            {
                return dependsOn;
            }

            public void PostSimulationTick(in DispatcherTimingDTO timing)
            {
                _owner?.PostSimulationTick(in timing);
            }

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

        private void CaptureScavengingLootOracleSignals()
        {
            int snapshotGeneration = SignalBus<ItemAcquiredSignal>.SnapshotGeneration;
            if (_lastScavengingItemSignalCaptureGeneration == snapshotGeneration)
                return;

            _lastScavengingItemSignalCaptureGeneration = snapshotGeneration;
            ReadOnlySpan<ItemAcquiredSignal> signals = SignalBus<ItemAcquiredSignal>.GetFrameSnapshot();
            if (signals.Length == 0)
                return;

            ItemAcquiredSignal[] pending = _pendingScavengingItemSignals;
            if (pending == null)
            {
                SpillScavengingSignalsToWorldDrops(signals);
                return;
            }

            int writeIndex = _pendingScavengingItemSignalCount;
            for (int i = 0; i < signals.Length; i++)
            {
                ItemAcquiredSignal signal = signals[i];
                if (!IsPendingScavengingInventorySignal(in signal))
                    continue;

                if (TryMergePendingScavengingSignal(pending, ref writeIndex, in signal, out int overflowQuantity))
                {
                    if (overflowQuantity > 0 &&
                        !TryRegisterPendingScavengingWorldDrop(in signal, overflowQuantity))
                    {
                        InventoryEvents.TryNotifyInventoryFull(unchecked((int)signal.ItemHash));
                    }

                    continue;
                }

                if (writeIndex >= pending.Length)
                {
                    if (!TryRegisterPendingScavengingWorldDrop(in signal, signal.Quantity))
                        InventoryEvents.TryNotifyInventoryFull(unchecked((int)signal.ItemHash));
                    continue;
                }

                pending[writeIndex++] = signal;
            }

            _pendingScavengingItemSignalCount = writeIndex;
        }

        private void SpillScavengingSignalsToWorldDrops(ReadOnlySpan<ItemAcquiredSignal> signals)
        {
            for (int i = 0; i < signals.Length; i++)
            {
                ItemAcquiredSignal signal = signals[i];
                if (!IsPendingScavengingInventorySignal(in signal))
                    continue;

                if (!TryRegisterPendingScavengingWorldDrop(in signal, signal.Quantity))
                    InventoryEvents.TryNotifyInventoryFull(unchecked((int)signal.ItemHash));
            }
        }

        private static bool IsPendingScavengingInventorySignal(in ItemAcquiredSignal signal)
        {
            return signal.SourceKind == ItemAcquiredSignalSourceKinds.ScavengingLootOracle &&
                   signal.ItemHash != 0u &&
                   signal.Quantity != 0;
        }

        private static bool TryMergePendingScavengingSignal(
            ItemAcquiredSignal[] pending,
            ref int pendingCount,
            in ItemAcquiredSignal signal,
            out int overflowQuantity)
        {
            overflowQuantity = 0;
            for (int i = 0; i < pendingCount; i++)
            {
                if (pending[i].ItemHash != signal.ItemHash)
                    continue;

                if (pending[i].OreHash != signal.OreHash ||
                    pending[i].SourceKind != signal.SourceKind ||
                    pending[i].Flags != signal.Flags ||
                    !AreSamePendingScavengingSourcePosition(in pending[i].PositionAup, in signal.PositionAup))
                {
                    continue;
                }

                int mergedQuantity = pending[i].Quantity + signal.Quantity;
                pending[i].Quantity = (ushort)math.min(ushort.MaxValue, mergedQuantity);
                overflowQuantity = math.max(0, mergedQuantity - ushort.MaxValue);
                pending[i].Frame = pending[i].Frame >= signal.Frame ? pending[i].Frame : signal.Frame;
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool AreSamePendingScavengingSourcePosition(
            in AbsoluteUniversePosition left,
            in AbsoluteUniversePosition right)
        {
            return left.GridX == right.GridX &&
                   left.GridY == right.GridY &&
                   left.GridZ == right.GridZ &&
                   left.LocalX == right.LocalX &&
                   left.LocalY == right.LocalY &&
                   left.LocalZ == right.LocalZ;
        }

        private void ApplyDeferredScavengingLootOracleSignals()
        {
            int count = _pendingScavengingItemSignalCount;
            if (count <= 0)
                return;

            ItemAcquiredSignal[] pending = _pendingScavengingItemSignals;
            if (pending == null)
            {
                _pendingScavengingItemSignalCount = 0;
                return;
            }

            int safeCount = math.min(count, pending.Length);
            int retainedCount = 0;
            for (int i = 0; i < safeCount; i++)
            {
                ItemAcquiredSignal signal = pending[i];
                int requestedQuantity = signal.Quantity;
                if (signal.ItemHash == 0u || requestedQuantity <= 0)
                    continue;

                ItemState state = new ItemState(0UL, DefaultQualityMilli);
                TryAddItemWithStateInternal(
                    unchecked((int)signal.ItemHash),
                    requestedQuantity,

                    new ItemState(0UL, DefaultQualityMilli),

                    out int addedQuantity);

                int clampedAddedQuantity = math.clamp(addedQuantity, 0, requestedQuantity);
                PublishPendingScavengingLifecycleCollected(in signal, clampedAddedQuantity);
                int remainingQuantity = requestedQuantity - clampedAddedQuantity;
                if (remainingQuantity <= 0)
                    continue;

                if (TryRegisterPendingScavengingWorldDrop(in signal, remainingQuantity))
                    continue;

                signal.Quantity = (ushort)math.min(ushort.MaxValue, remainingQuantity);
                pending[retainedCount++] = signal;
            }

            for (int i = retainedCount; i < safeCount; i++)
                pending[i] = default;

            _pendingScavengingItemSignalCount = retainedCount;
        }

        private void PublishPendingScavengingLifecycleCollected(in ItemAcquiredSignal signal, int addedQuantity)
        {
            if (addedQuantity <= 0 || signal.ItemHash == 0u || itemCatalog == null)
                return;

            ItemData item = itemCatalog.FindByHash(unchecked((int)signal.ItemHash));
            if (item == null)
                return;

            bool hasRuntimePosition =
                signal.PositionAup.TryToRuntimeFloat3(out float3 runtimePosition) &&
                math.all(math.isfinite(runtimePosition));

            Vector3 signalPosition = hasRuntimePosition
                ? new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z)
                : Vector3.zero;

            ulong interactorEntityId = gameObject != null ? EntityId.ToULong(gameObject.GetEntityId()) : 0ul;
            ItemLifecycleSignalRoute.TryPublishCollected(
                item,
                addedQuantity,
                interactorEntityId,
                signalPosition,
                hasRuntimePosition);
        }

        private bool TryRegisterPendingScavengingWorldDrop(in ItemAcquiredSignal signal, int quantity)
        {
            if (quantity <= 0 || signal.ItemHash == 0u || itemCatalog == null)
                return false;

            IPersistentDroppedItemRegistry persistentWorldRegistry = _cachedPersistentWorldRegistry;
            if (persistentWorldRegistry == null)
            {
                persistentWorldRegistry = GlobalRegistry.PersistentDroppedItems;
                _cachedPersistentWorldRegistry = persistentWorldRegistry;
            }

            if (persistentWorldRegistry == null)
                return false;

            ItemData item = itemCatalog.FindByHash(unchecked((int)signal.ItemHash));
            if (item == null ||
                !signal.PositionAup.TryToRuntimeFloat3(out float3 runtimePosition) ||
                !math.all(math.isfinite(runtimePosition)))
            {
                return false;
            }

            return persistentWorldRegistry.TryRegisterDroppedItem(
                item,
                quantity,
                new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
        }

        private void CaptureInventoryCommandSignals()
        {
            ReadOnlySpan<InventoryCommandSignal> commands = SignalBus<InventoryCommandSignal>.GetFrameSnapshot();
            if (commands.Length == 0)
                return;

            PendingInventoryCommand[] pending = _pendingInventoryCommands;
            if (pending == null)
            {
                DropInventoryCommandSignals(commands, ResolveInventorySignalHash());
                return;
            }

            uint inventoryHash = ResolveInventorySignalHash();
            int writeIndex = _pendingInventoryCommandCount;
            for (int index = 0; index < commands.Length; index++)
            {
                InventoryCommandSignal command = commands[index];
                if (!IsPendingInventoryCommandForOwner(in command, inventoryHash))
                    continue;

                if (writeIndex >= pending.Length)
                {
                    DropInventoryCommandSignal(in command);
                    continue;
                }

                PendingInventoryCommand entry = default;
                entry.Command = command;
                if (command.Command == InventoryCommandSignalCommands.DropNonEquippedResources &&
                    TryResolveRespawnDeathAup(in command, out double3 deathAup))
                {
                    entry.DeferredDeathAup = deathAup;
                    entry.HasDeferredDeathAup = 1;
                }

                pending[writeIndex++] = entry;
            }

            _pendingInventoryCommandCount = writeIndex;
        }

        private void DropDeferredInventoryCommandSignals()
        {
            int count = _pendingInventoryCommandCount;
            if (count <= 0)
                return;

            _pendingInventoryCommandCount = 0;
            PendingInventoryCommand[] pending = _pendingInventoryCommands;
            if (pending == null)
            {
                RecordDroppedInventoryCommandSignals(count);
                return;
            }

            int safeCount = math.min(count, pending.Length);
            for (int index = 0; index < safeCount; index++)
            {
                InventoryCommandSignal command = pending[index].Command;
                pending[index] = default;
                if (command.Command == 0)
                    continue;

                DropInventoryCommandSignal(in command);
            }

            if (count > safeCount)
                RecordDroppedInventoryCommandSignals(count - safeCount);
        }

        private void DropInventoryCommandSignals(ReadOnlySpan<InventoryCommandSignal> commands, uint inventoryHash)
        {
            for (int index = 0; index < commands.Length; index++)
            {
                InventoryCommandSignal command = commands[index];
                if (!IsPendingInventoryCommandForOwner(in command, inventoryHash))
                    continue;

                DropInventoryCommandSignal(in command);
            }
        }

        private void DropInventoryCommandSignal(in InventoryCommandSignal command)
        {
            RecordDroppedInventoryCommandSignal();

            if (command.Command == InventoryCommandSignalCommands.DropNonEquippedResources)
                PublishRespawnDropPenaltyResult(in command, 0);
        }

        private void RecordDroppedInventoryCommandSignal()
        {
            if (_droppedInventoryCommandSignalCount < int.MaxValue)
                _droppedInventoryCommandSignalCount++;
        }

        private void RecordDroppedInventoryCommandSignals(int droppedCount)
        {
            if (droppedCount <= 0 || _droppedInventoryCommandSignalCount >= int.MaxValue)
                return;

            int remaining = int.MaxValue - _droppedInventoryCommandSignalCount;
            _droppedInventoryCommandSignalCount += math.min(droppedCount, remaining);
        }

        private static bool IsPendingInventoryCommandForOwner(in InventoryCommandSignal command, uint inventoryHash)
        {
            return (command.InventoryHash == 0u || command.InventoryHash == inventoryHash) &&
                   (command.Command == InventoryCommandSignalCommands.DropNonEquippedResources ||
                    command.Command == InventoryCommandSignalCommands.Sort);
        }

        private void DrainRepairToolTitaniumSignals()
        {
            ReadOnlySpan<ItemAcquiredSignal> signals = SignalBus<ItemAcquiredSignal>.GetFrameSnapshot();
            if (signals.Length == 0 || !TryResolveActiveRepairToolItemHash(out int repairToolItemHash))
                return;

            for (int i = 0; i < signals.Length; i++)
            {
                ItemAcquiredSignal signal = signals[i];
                if (signal.SourceKind == ItemAcquiredSignalSourceKinds.ScavengingLootOracle ||
                    signal.SourceKind == ItemAcquiredSignalSourceKinds.DroneMining ||
                    signal.ItemHash != _TitaniumScrapHashId ||
                    signal.Frame <= _lastRepairTitaniumFrame)
                    continue;

                _lastRepairTitaniumFrame = signal.Frame;
                if (RestoreDurabilityForItemHash(repairToolItemHash))
                    return;
            }
        }

        private bool TryResolveActiveRepairToolItemHash(out int itemHashId)
        {
            itemHashId = 0;
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            PlayerToolManager toolManager = playerContext != null ? playerContext.ToolManager : null;
            PlayerTool currentTool = toolManager != null ? toolManager.CurrentTool : null;
            if (!(currentTool is RepairTool) || currentTool.ToolData == null)
                return false;

            itemHashId = ItemData.ResolvePersistentHashId(currentTool.ToolData);
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
            // L19 hop2 LIVE: skip inventory salinity corrosion under batch -
            // native AV in ApplyInventorySalinityCorrosion during WORLDDRIVER SlowTick.
            if (UnityEngine.Application.isBatchMode)
                return;
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

            if (!TryAcquireSalinityCorrosionMutationGuard(out IDataVault salinityGuardVault, out ulong salinityGuardMask))
            {
                _averageEquipmentDurability01 = ResolveAverageEquipmentDurability();
                UpdateEquipmentRustShaderScalar();
                WriteSalinityCorrosionBlackBoxFrame(0x13);
                return;
            }

            int changedCount = 0;
            int brokenCount = 0;
            bool committedChanges = false;
            int salinityFrameFlags = 0;
            try
            {
                ExecuteSalinityCorrosionJobWithGuardHeld(
                    out changedCount,
                    out brokenCount,
                    out committedChanges,
                    out salinityFrameFlags);
            }
            finally
            {
                ReleaseSalinityCorrosionMutationGuard(salinityGuardVault, salinityGuardMask);
            }

            UpdateEquipmentRustShaderScalar();
            UpdateEquipmentFailingNotification();
            WriteSalinityCorrosionBlackBoxFrame(salinityFrameFlags);

            if (brokenCount > 0 && committedChanges)
                PublishBrokenEquipmentSignals(brokenCount);

            if (changedCount > 0 && committedChanges)
            {
                PublishItemDurabilityChanged(0u, _averageEquipmentDurability01, ItemDurabilityChangedSignal.ReasonCorrosion, ushort.MaxValue);
                NotifyInventoryChanged(massDirty: false);
            }
        }

        private bool CanRunSalinityCorrosionJob()
        {
            int cellCount = _grid != null ? _grid.Columns * _grid.Rows : columns * rows;
            return _itemHashes.IsCreated &&
                   _stackCounts.IsCreated &&
                   _itemDurability.IsCreated &&
                   _durabilities.IsCreated &&
                   _qualityMilli.IsCreated &&
                   _itemStateFlags.IsCreated &&
                   _salinityCorrosionJobResult.IsCreated &&
                   _salinityCorrosionJobResult.Length >= InventoryCorrosionConstants.ResultRequiredLength &&
                   _salinityBrokenItemHashes.IsCreated &&
                   _salinityBrokenItemHashes.Length >= cellCount &&
                   _salinityChangedSlotsScratch.IsCreated &&
                   _salinityChangedSlotsScratch.Length >= cellCount &&
                   _salinityNextDurabilityScratch.IsCreated &&
                   _salinityNextDurabilityScratch.Length >= cellCount &&
                   _salinityNextDurabilityBytesScratch.IsCreated &&
                   _salinityNextDurabilityBytesScratch.Length >= cellCount &&
                   _salinityNextQualityMilliScratch.IsCreated &&
                   _salinityNextQualityMilliScratch.Length >= cellCount &&
                   _salinityNextStateFlagsScratch.IsCreated &&
                   _salinityNextStateFlagsScratch.Length >= cellCount;
        }

        private bool TryResolveSalinityCorrosionScratchWithGuardHeld(
            out NativeArray<int> changedSlots,
            out NativeArray<float> nextDurability,
            out NativeArray<byte> nextDurabilityBytes,
            out NativeArray<ushort> nextQualityMilli,
            out NativeArray<ushort> nextStateFlags,
            out NativeArray<int> jobResult,
            out NativeArray<uint> brokenItemHashes)
        {
            changedSlots = default;
            nextDurability = default;
            nextDurabilityBytes = default;
            nextQualityMilli = default;
            nextStateFlags = default;
            jobResult = default;
            brokenItemHashes = default;

            return _salinityChangedSlotsScratch.TryResolve(out changedSlots) &&
                   _salinityNextDurabilityScratch.TryResolve(out nextDurability) &&
                   _salinityNextDurabilityBytesScratch.TryResolve(out nextDurabilityBytes) &&
                   _salinityNextQualityMilliScratch.TryResolve(out nextQualityMilli) &&
                   _salinityNextStateFlagsScratch.TryResolve(out nextStateFlags) &&
                   _salinityCorrosionJobResult.TryResolve(out jobResult) &&
                   _salinityBrokenItemHashes.TryResolve(out brokenItemHashes);
        }

        private bool TryCommitSalinityCorrosionScratchWithGuardHeld(
            int changedCount,
            NativeArray<int> changedSlots,
            NativeArray<float> nextDurability,
            NativeArray<byte> nextDurabilityBytes,
            NativeArray<ushort> nextQualityMilli,
            NativeArray<ushort> nextStateFlags)
        {
            int count = ResolveSalinityScratchChangeCountWithGuardHeld(
                changedCount,
                changedSlots,
                nextDurability,
                nextDurabilityBytes,
                nextQualityMilli,
                nextStateFlags);
            if (count <= 0)
                return false;

            if (!_itemDurability.TryResolve(out NativeArray<float> itemDurability) ||
                !_durabilities.TryResolve(out NativeArray<byte> durabilityBytes) ||
                !_qualityMilli.TryResolve(out NativeArray<ushort> qualityMilli) ||
                !_itemStateFlags.TryResolve(out NativeArray<ushort> itemStateFlags))
            {
                return false;
            }

            int committedRows = 0;
            for (int i = 0; i < count; i++)
            {
                int slot = changedSlots[i];
                if ((uint)slot >= (uint)itemDurability.Length ||
                    (uint)slot >= (uint)durabilityBytes.Length ||
                    (uint)slot >= (uint)qualityMilli.Length ||
                    (uint)slot >= (uint)itemStateFlags.Length)
                {
                    continue;
                }

                itemDurability[slot] = nextDurability[i];
                durabilityBytes[slot] = nextDurabilityBytes[i];
                qualityMilli[slot] = nextQualityMilli[i];
                itemStateFlags[slot] = nextStateFlags[i];
                committedRows++;
            }

            return committedRows > 0;
        }


        private void ExecuteSalinityCorrosionJobWithGuardHeld(
            out int changedCount,
            out int brokenCount,
            out bool committedChanges,
            out int salinityFrameFlags)
        {
            changedCount = 0;
            brokenCount = 0;
            committedChanges = false;
            salinityFrameFlags = 0;

            if (!TryResolveSalinityCorrosionScratchWithGuardHeld(
                    out NativeArray<int> changedSlots,
                    out NativeArray<float> nextDurability,
                    out NativeArray<byte> nextDurabilityBytes,
                    out NativeArray<ushort> nextQualityMilli,
                    out NativeArray<ushort> nextStateFlags,
                    out NativeArray<int> jobResult,
                    out NativeArray<uint> brokenItemHashes))
            {
                _averageEquipmentDurability01 = ResolveAverageEquipmentDurability();
                salinityFrameFlags = 0x14;
                return;
            }

            ItemSalinityCorrosionJob salinityJob = new ItemSalinityCorrosionJob
            {
                ItemHashes = _itemHashes.AsReadOnly(),
                StackCounts = _stackCounts.AsReadOnly(),
                ItemDurability = _itemDurability.AsReadOnly(),
                DurabilityBytes = _durabilities.AsReadOnly(),
                QualityMilli = _qualityMilli.AsReadOnly(),
                ItemStateFlags = _itemStateFlags.AsReadOnly(),
                ChangedSlots = changedSlots,
                NextItemDurability = nextDurability,
                NextDurabilityBytes = nextDurabilityBytes,
                NextQualityMilli = nextQualityMilli,
                NextItemStateFlags = nextStateFlags,
                Result = jobResult,
                BrokenItemHashes = brokenItemHashes,
                CurrentInventoryMask = CurrentInventoryMask,
                SalinityFactor = _currentSalinityFactor,
                DegradationRate = SalinityCorrosionDegradationRatePerFrostTick / SalinityCorrosionFrostTickSeconds,
                DegradedMask = DegradedItemStateMask,
                RustedMask = RustedItemStateMask,
                BrokenMask = BrokenItemStateMask,
                DegradedThresholdMilli = DegradedQualityMilliThreshold,
                ElapsedSeconds = SalinityCorrosionFrostTickSeconds
            };
            salinityJob.Execute();

            int averageMilli = jobResult[InventoryCorrosionConstants.ResultAverageDurabilityMilli];
            _averageEquipmentDurability01 = math.saturate(averageMilli * 0.001f);
            changedCount = jobResult[InventoryCorrosionConstants.ResultChangedCount];
            brokenCount = jobResult[InventoryCorrosionConstants.ResultBrokenCount];
            committedChanges = changedCount <= 0 ||
                TryCommitSalinityCorrosionScratchWithGuardHeld(
                    changedCount,
                    changedSlots,
                    nextDurability,
                    nextDurabilityBytes,
                    nextQualityMilli,
                    nextStateFlags);
            salinityFrameFlags = changedCount > 0 ? (committedChanges ? 2 : 0x12) : 0;
        }

        private static int ResolveSalinityScratchChangeCountWithGuardHeld(
            int changedCount,
            NativeArray<int> changedSlots,
            NativeArray<float> nextDurability,
            NativeArray<byte> nextDurabilityBytes,
            NativeArray<ushort> nextQualityMilli,
            NativeArray<ushort> nextStateFlags)
        {
            if (changedCount <= 0 ||
                !changedSlots.IsCreated ||
                !nextDurability.IsCreated ||
                !nextDurabilityBytes.IsCreated ||
                !nextQualityMilli.IsCreated ||
                !nextStateFlags.IsCreated)
            {
                return 0;
            }

            int capacity = changedSlots.Length;
            capacity = math.min(capacity, nextDurability.Length);
            capacity = math.min(capacity, nextDurabilityBytes.Length);
            capacity = math.min(capacity, nextQualityMilli.Length);
            capacity = math.min(capacity, nextStateFlags.Length);
            return math.min(changedCount, capacity);
        }

        private void PublishBrokenEquipmentSignals(int brokenCount)
        {
            if (!_salinityBrokenItemHashes.TryReadOnly(out NativeArray<uint>.ReadOnly brokenItemHashes))
                return;

            int count = brokenItemHashes.IsCreated
                ? math.min(brokenCount, brokenItemHashes.Length)
                : 0;
            for (int i = 0; i < count; i++)
            {
                uint itemHash = brokenItemHashes[i];
                if (itemHash == 0u)
                    continue;

                SignalBus<ToolAcousticSignal>.TryPushTracked(new ToolAcousticSignal
                {
                    ToolHash = _EquipmentCorrosionToolHash,
                    TargetHash = itemHash,
                    Progress01 = 1f,
                    PitchScale = 0.72f,
                    Intensity01 = 0.85f,
                    Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                    State = 3,
                    Flags = 1
                }, ref _signalPushDropCount);
                PublishItemDurabilityChanged(itemHash, 0f, ItemDurabilityChangedSignal.ReasonBreak, ushort.MaxValue);
            }
        }

        private void PublishItemDurabilityChanged(uint itemHash, float durability01, byte reason, ushort slotIndex)
        {
            SignalBus<ItemDurabilityChangedSignal>.TryPushTracked(new ItemDurabilityChangedSignal
            {
                InventoryHash = ResolveInventorySignalHash(),
                ItemHash = itemHash,
                Durability01 = math.saturate(durability01),
                AverageEquippedDurability01 = _averageEquipmentDurability01,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                SlotIndex = slotIndex,
                Reason = reason,
                Flags = 0,
                BiomeHash = _currentSalinityBiomeHash
            }, ref _signalPushDropCount);
        }

        private void UpdateEquipmentFailingNotification()
        {
            if (_averageEquipmentDurability01 < EquipmentFailingThreshold01)
            {
                if (_equipmentFailingHudLatched != 0)
                    return;

                _equipmentFailingHudLatched = 1;
                SignalBus<HUDNotificationSignal>.TryPushTracked(new HUDNotificationSignal
                {
                    MessageHash = _EquipmentFailingMessageHash,
                    ContextHash = _EquipmentFailingContextHash,
                    SourceId = ResolveInventorySignalHash(),
                    Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                    Severity = 2,
                    Flags = 0
                }, ref _signalPushDropCount);
                return;
            }

            if (_averageEquipmentDurability01 >= EquipmentFailingResetThreshold01)
                _equipmentFailingHudLatched = 0;
        }

        private void UpdateEquipmentRustShaderScalar()
        {
            _pendingEquipmentRustShaderScalar = math.saturate(1f - _averageEquipmentDurability01);
            _hasPendingEquipmentRustShaderScalar = true;
        }

        private void FlushEquipmentRustShaderScalar()
        {
            if (!_hasPendingEquipmentRustShaderScalar)
                return;

            _hasPendingEquipmentRustShaderScalar = false;
            Shader.SetGlobalFloat(_HectonEquipmentRust01Id, _pendingEquipmentRustShaderScalar);
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
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
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

        private unsafe void DumpSalinityCorrosionBlackBoxOnce()
        {
            if (_salinityCorrosionBlackBoxDumped != 0 || !_salinityCorrosionBlackBox.IsCreated)
                return;

            _salinityCorrosionBlackBoxDumped = 1;
            int count = _salinityCorrosionBlackBox.Length;
            if (count <= 0)
                return;

            int cursor = _salinityCorrosionBlackBoxCursor;
            if ((uint)cursor >= (uint)count)
                cursor = 0;

            int byteCount = InventoryBlackBoxDumpHeaderBytes + count * SalinityCorrosionBlackBoxEntrySizeBytes;
            NativeArray<byte> payload = default;
            try
            {
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(PlayerInventory),
                    "SalinityCorrosionBlackBoxDumpPayload",
                    NativeArrayOptions.ClearMemory);

                byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payload);
                int writeCursor = 0;
                WriteUInt32LittleEndian(destination, ref writeCursor, SalinityCorrosionBlackBoxDumpMagic);
                WriteUInt32LittleEndian(destination, ref writeCursor, InventoryBlackBoxDumpVersion);
                WriteUInt32LittleEndian(destination, ref writeCursor, unchecked((uint)count));
                WriteUInt32LittleEndian(destination, ref writeCursor, unchecked((uint)SalinityCorrosionBlackBoxEntrySizeBytes));
                WriteUInt32LittleEndian(destination, ref writeCursor, unchecked((uint)cursor));
                WriteUInt32LittleEndian(destination, ref writeCursor, unchecked((uint)InventoryVersion));
                WriteUInt32LittleEndian(destination, ref writeCursor, 0u);
                WriteUInt32LittleEndian(destination, ref writeCursor, 0u);

                for (int i = 0; i < count; i++)
                {
                    int index = cursor + i;
                    if (index >= count)
                        index -= count;

                    int rowEnd = writeCursor + SalinityCorrosionBlackBoxEntrySizeBytes;
                    WriteSalinityCorrosionTelemetryEntry(destination, ref writeCursor, _salinityCorrosionBlackBox[index]);
                    if (writeCursor > rowEnd)
                        return;

                    writeCursor = rowEnd;
                }

                NativeFaultDumpWriter.TryWriteAll(SalinityCorrosionBlackBoxDumpRelativePath, payload, writeCursor);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(PlayerInventory),
                    "SalinityCorrosionBlackBoxDumpPayload");
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
                // ZERO-GC INLINE KERNEL: owner-phase refresh keeps public totals current before notifications.
                InventoryMassVolumeKernel massVolumeKernel = default;
                massVolumeKernel.AnchorHashIds = _grid.AnchorHashIds;
                massVolumeKernel.StackCounts = _stackCounts;
                massVolumeKernel.AnchorUnitMassKg = _anchorUnitMassKg;
                massVolumeKernel.AnchorUnitVolumeM3 = _anchorUnitVolumeM3;
                massVolumeKernel.AnchorUnitRadiationSv = _anchorUnitRadiationSv;
                massVolumeKernel.Totals = _derivedMassVolumeScratch;
                massVolumeKernel.Execute();

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
            if (survival != null)
                survival.SetWeight(_currentWeightKg);

            float carryCapacityKg = ResolveCarryCapacityKilograms();
            CachedInventoryLoad01 = Hecton8.PureLogic.Systems.PlayerEffortLoadCalculator.ComputeLoad01(_currentWeightKg, carryCapacityKg);
            CachedMaxSwimSpeedMultiplier = Hecton8.PureLogic.Systems.PlayerEffortLoadCalculator.ComputeMovementMultiplier(
                _currentWeightKg,
                carryCapacityKg,
                InventoryLoadMinimumMovementMultiplier);

            WriteInventoryBlackBoxFrame(invalidTotals ? 1 : 0);
            if (invalidTotals)
                DumpInventoryBlackBoxOnce();
        }

        private void WriteInventoryBlackBoxFrame(int flags, uint faultBufferId = 0u, uint faultGeneration = 0u)
        {
            if (!_inventoryBlackBox.IsCreated || _inventoryBlackBox.Length == 0)
                return;

            int index = _inventoryBlackBoxCursor;
            if ((uint)index >= (uint)_inventoryBlackBox.Length)
                index = 0;

            _inventoryBlackBox[index] = new InventoryTelemetryEntry
            {
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                Version = faultGeneration != 0u ? faultGeneration : unchecked((uint)InventoryVersion),
                WeightKg = _currentWeightKg,
                VolumeLiters = _currentVolumeLiters,
                Load01 = CachedInventoryLoad01,
                InventoryMaskLow = faultBufferId != 0u ? faultBufferId : unchecked((uint)CurrentInventoryMask),
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

        private void WriteInventoryVaultFaultFrame<T>(in InventoryVaultLane<T> lane, int flags) where T : struct
        {
            WriteInventoryBlackBoxFrame(flags | 0x100, lane.BufferId, lane.Generation);
        }

        private unsafe void DumpInventoryBlackBoxOnce()
        {
            if (_inventoryBlackBoxDumped != 0 || !_inventoryBlackBox.IsCreated)
                return;

            _inventoryBlackBoxDumped = 1;
            int count = _inventoryBlackBox.Length;
            if (count <= 0)
                return;

            int cursor = _inventoryBlackBoxCursor;
            if ((uint)cursor >= (uint)count)
                cursor = 0;

            int byteCount = InventoryBlackBoxDumpHeaderBytes + count * InventoryBlackBoxEntrySizeBytes;
            NativeArray<byte> payload = default;
            try
            {
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(PlayerInventory),
                    "InventoryBlackBoxDumpPayload",
                    NativeArrayOptions.ClearMemory);

                byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payload);
                int writeCursor = 0;
                WriteUInt32LittleEndian(destination, ref writeCursor, InventoryBlackBoxDumpMagic);
                WriteUInt32LittleEndian(destination, ref writeCursor, InventoryBlackBoxDumpVersion);
                WriteUInt32LittleEndian(destination, ref writeCursor, unchecked((uint)count));
                WriteUInt32LittleEndian(destination, ref writeCursor, unchecked((uint)InventoryBlackBoxEntrySizeBytes));
                WriteUInt32LittleEndian(destination, ref writeCursor, unchecked((uint)cursor));
                WriteUInt32LittleEndian(destination, ref writeCursor, unchecked((uint)InventoryVersion));
                WriteUInt32LittleEndian(destination, ref writeCursor, 0u);
                WriteUInt32LittleEndian(destination, ref writeCursor, 0u);

                for (int i = 0; i < count; i++)
                {
                    int index = cursor + i;
                    if (index >= count)
                        index -= count;

                    int rowEnd = writeCursor + InventoryBlackBoxEntrySizeBytes;
                    WriteInventoryTelemetryEntry(destination, ref writeCursor, _inventoryBlackBox[index]);
                    if (writeCursor > rowEnd)
                        return;

                    writeCursor = rowEnd;
                }

                NativeFaultDumpWriter.TryWriteAll(BuildInventoryBlackBoxDumpRelativePath(DateTime.UtcNow.Ticks), payload, writeCursor);
                NativeFaultDumpWriter.TryWriteAll(InventoryBlackBoxDumpRelativePath, payload, writeCursor);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(PlayerInventory),
                    "InventoryBlackBoxDumpPayload");
            }
        }

        private static string BuildInventoryBlackBoxDumpRelativePath(long utcTicks)
        {
            return "Docs/AgentLogs/Dump_INVENTORY_BLACKBOX_" + utcTicks.ToString("X16") + ".bin";
        }

        private static unsafe void WriteSalinityCorrosionTelemetryEntry(byte* destination, ref int cursor, SalinityCorrosionTelemetryEntry entry)
        {
            WriteUInt32LittleEndian(destination, ref cursor, entry.Frame);
            WriteUInt32LittleEndian(destination, ref cursor, entry.InventoryVersion);
            WriteFloatLittleEndian(destination, ref cursor, entry.AverageEquipmentDurability01);
            WriteFloatLittleEndian(destination, ref cursor, entry.RustScalar01);
            WriteFloatLittleEndian(destination, ref cursor, entry.SalinityFactor);
            WriteUInt32LittleEndian(destination, ref cursor, entry.CurrentBiomeHash);
            WriteUInt32LittleEndian(destination, ref cursor, entry.InventoryMaskLow);
            WriteInt32LittleEndian(destination, ref cursor, entry.Flags);
        }

        private static unsafe void WriteInventoryTelemetryEntry(byte* destination, ref int cursor, InventoryTelemetryEntry entry)
        {
            WriteUInt32LittleEndian(destination, ref cursor, entry.Frame);
            WriteUInt32LittleEndian(destination, ref cursor, entry.Version);
            WriteFloatLittleEndian(destination, ref cursor, entry.WeightKg);
            WriteFloatLittleEndian(destination, ref cursor, entry.VolumeLiters);
            WriteFloatLittleEndian(destination, ref cursor, entry.Load01);
            WriteUInt32LittleEndian(destination, ref cursor, entry.InventoryMaskLow);
            WriteInt32LittleEndian(destination, ref cursor, entry.OccupiedCells);
            WriteInt32LittleEndian(destination, ref cursor, entry.Flags);
            WriteFloatLittleEndian(destination, ref cursor, entry.MaxWeightKg);
            WriteFloatLittleEndian(destination, ref cursor, entry.MaxVolumeLiters);
            WriteUInt32LittleEndian(destination, ref cursor, entry.ShadowHash);
            WriteInt32LittleEndian(destination, ref cursor, entry.ShadowPayloadLength);
            WriteFloatLittleEndian(destination, ref cursor, entry.RadiationSv);
            WriteInt32LittleEndian(destination, ref cursor, entry.Columns);
            WriteInt32LittleEndian(destination, ref cursor, entry.Rows);
            WriteInt32LittleEndian(destination, ref cursor, entry.DefragTimeMicroseconds);
        }

        private static unsafe void WriteFloatLittleEndian(byte* destination, ref int cursor, float value)
        {
            WriteUInt32LittleEndian(destination, ref cursor, math.asuint(value));
        }

        private static unsafe void WriteInt32LittleEndian(byte* destination, ref int cursor, int value)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(new Span<byte>(destination + cursor, sizeof(int)), value);
            cursor += sizeof(int);
        }

        private static unsafe void WriteUInt32LittleEndian(byte* destination, ref int cursor, uint value)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(new Span<byte>(destination + cursor, sizeof(uint)), value);
            cursor += sizeof(uint);
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
                InventoryRadioactiveHalfLifeKernel halfLifeKernel = default;
                halfLifeKernel.AnchorHashIds = _grid.AnchorHashIds;
                halfLifeKernel.StackCounts = _stackCounts;
                halfLifeKernel.AnchorUnitRadiationSv = _anchorUnitRadiationSv;
                halfLifeKernel.ItemStateFlags = _itemStateFlags;
                halfLifeKernel.QualityMilli = _qualityMilli;
                halfLifeKernel.ConversionAnchorIndices = _radioactiveConversionAnchors;
                halfLifeKernel.Counters = _radioactiveHalfLifeCounters;
                halfLifeKernel.DeltaSeconds = SlowTickIntervalSeconds;
                halfLifeKernel.BaseHalfLifeSeconds = RadioactiveHalfLifeBaseSeconds;
                halfLifeKernel.DefaultQuality = DefaultQualityMilli;
                halfLifeKernel.RadioactiveMask = RadioactiveItemStateMask;
                halfLifeKernel.DegradedMask = DegradedItemStateMask;
                halfLifeKernel.DegradedThreshold = DegradedQualityMilliThreshold;
                halfLifeKernel.Execute();
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
                InventoryReactiveChemistryKernel chemistryKernel = default;
                chemistryKernel.AnchorHashIds = _grid.AnchorHashIds;
                chemistryKernel.StackCounts = _stackCounts;
                chemistryKernel.CraftLockedCounts = _craftLockedCounts;
                chemistryKernel.ItemStateFlags = _itemStateFlags;
                chemistryKernel.ThermalRunawayByAnchor = _thermalRunawayByAnchor;
                chemistryKernel.RunawayPairs = _thermalRunawayPairs;
                chemistryKernel.Counters = _thermalRunawayCounters;
                chemistryKernel.Columns = columns;
                chemistryKernel.Rows = rows;
                chemistryKernel.DeltaSeconds = SlowTickIntervalSeconds;
                chemistryKernel.RunawayPerSecond = ThermalRunawayPerSecond;
                chemistryKernel.CooldownPerSecond = ThermalRunawayCooldownPerSecond;
                chemistryKernel.RadioactiveMask = RadioactiveItemStateMask;
                chemistryKernel.FlammableMask = FlammableItemStateMask;
                chemistryKernel.Execute();
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
            int targetId = ResolveInventoryPlayerCombatTargetId();
            QueueInventoryThermalRunawayStatus(targetId, damage);
            PublishInventoryThermalRunawayRadiationDose(damage);

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

            IAudioService audioService = ResolveAudioService();
            if (audioService is ISpatialAudioInventoryRunawaySink inventoryAudio)
                inventoryAudio.QueueInventoryRunawayExplosion(transform.position, ThermalRunawayAudioVolume);
        }

        private int ResolveInventoryPlayerCombatTargetId()
        {
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            HectonPlayerHealth playerHealth = playerContext != null ? playerContext.PlayerHealth : null;
            if (playerHealth != null)
                return CombatDamageRuntime.ResolveTargetId(playerHealth.gameObject);

            return survival != null ? CombatDamageRuntime.ResolveTargetId(survival.gameObject) : 0;
        }

        private void QueueInventoryThermalRunawayStatus(int targetId, float damage)
        {
            if (targetId == 0 || !CombatDamageRuntime.IsTargetRegistered(targetId))
                return;

            float severity01 = math.saturate(FiniteNonNegativeOrZero(damage) * 0.02f);
            if (severity01 <= 0.0001f)
                return;

            CombatDamageRuntime.TryQueueStatusEffect(
                targetId,
                CombatStatusBits.Burning64,
                ThermalRunawayBurnDurationSeconds * math.max(0.25f, severity01),
                DamageSourceIds.InventoryRadiation,
                severity01);
            CombatDamageRuntime.TryQueueStatusEffect(
                targetId,
                CombatStatusBits.Irradiated64,
                ThermalRunawayRadiationDurationSeconds * math.max(0.25f, severity01),
                DamageSourceIds.InventoryRadiation,
                severity01);
        }

        private void PublishInventoryThermalRunawayRadiationDose(float damage)
        {
            float severity01 = math.saturate(FiniteNonNegativeOrZero(damage) * 0.02f);
            if (severity01 <= 0.0001f || !TryResolveInventoryPlayerAup(out AbsoluteUniversePosition playerAup))
                return;

            RadiationDoseSignal signal = default;
            signal.PositionAup = playerAup;
            signal.Dose = FiniteNonNegativeOrZero(damage) * ThermalRunawayRadiationDoseScale;
            signal.Intensity01 = severity01;
            signal.SourceId = DamageSourceIds.InventoryRadiation;
            signal.DoseKind = ThermalRunawayRadiationDoseKind;
            signal.Flags = 1;
            SignalBus<RadiationDoseSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
        }

        private bool TryResolveInventoryPlayerAup(out AbsoluteUniversePosition playerAup)
        {
            playerAup = AbsoluteUniversePosition.Invalid();
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext != null)
            {
                if (playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                    snapshot.Aup.IsFinite())
                {
                    playerAup = snapshot.Aup;
                    return true;
                }

                return false;
            }

            return TryResolveAupFromRuntimeOrigin(transform.position, out playerAup);
        }

        private static float FiniteNonNegativeOrZero(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
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
            return _traumaDispatcher;
        }

        private float ResolveInventoryRadiationThresholdSv()
        {
            if (survival != null && survival.Stats != null)
                return math.max(0.01f, survival.Stats.RadiationThreshold);

            return math.max(0.01f, radiationTraumaThresholdSv);
        }

        private float ResolveInventoryCarrierDepthMeters()
        {
            if (TryResolveInventoryMovementRuntimeState(out PlayerMovementRuntimeState movementState))
                return math.max(0f, movementState.DepthMeters);

            return 0f;
        }

        private bool TryResolveInventoryMovementRuntimeState(out PlayerMovementRuntimeState movementState)
        {
            movementState = default;
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext == null ||
                !playerContext.IsInitialized ||
                !playerContext.TryGetMovementRuntimeState(out movementState) ||
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u ||
                !math.isfinite(movementState.DepthMeters))
            {
                movementState = default;
                return false;
            }

            return true;
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

        private bool ResolveInventoryCarrierSubmergedState()
        {
            if (TryResolveInventoryMovementRuntimeState(out PlayerMovementRuntimeState movementState))
                return (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.Underwater) != 0u ||
                       movementState.DepthMeters > 0f;

            return false;
        }

        private bool TryGetRuntimeDescriptor(int itemHashId, out ItemCatalog.ItemRuntimeDescriptor runtimeDescriptor)
        {
            runtimeDescriptor = default;
            return itemCatalog != null &&
                   itemHashId != 0 &&
                   itemCatalog.TryGetRuntimeDescriptor(itemHashId, out runtimeDescriptor);
        }

        void IPhysicsImpactEventListener.OnPhysicsImpact(in PhysicsImpactSignal impactSignal)
        {
            HandlePhysicsImpact(in impactSignal);
        }

        private void HandlePhysicsImpact(in PhysicsImpactSignal impactSignal)
        {
            if (!IsPlayerProximateImpact(in impactSignal))
                return;

            float impactAccelerationG = EstimateImpactAccelerationInG(impactSignal);
            if (impactAccelerationG < KineticDamageThresholdG)
                return;

            ApplyKineticInventoryDamage();
        }

        private float EstimateImpactAccelerationInG(PhysicsImpactSignal impactSignal)
        {
            return math.max(0f, impactSignal.Force * math.rcp(PlayerEquivalentMassKg * HectonPhysicsContract.GravityMetersPerSecondSquaredConst));
        }

        private bool IsPlayerProximateImpact(in PhysicsImpactSignal impactSignal)
        {
            if (!TryResolveInventoryPlayerAup(out AbsoluteUniversePosition playerAup))
                return false;

            AbsoluteUniversePosition impactAup = AbsoluteUniversePosition.FromAbsolutePosition(impactSignal.ResolvePointAupMeters());
            if (!impactAup.IsFinite())
                return false;

            double maxDistanceSq = KineticInventoryImpactRadiusMeters * KineticInventoryImpactRadiusMeters;
            return AbsoluteUniversePosition.DistanceSq(in playerAup, in impactAup) <= maxDistanceSq;
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

        private unsafe void ClearNativeArray<T>(InventoryVaultLane<T> lane) where T : struct
        {
            if (!lane.TryAcquireWriteLock(out NativeArray<T> array))
            {
                WriteInventoryVaultFaultFrame(in lane, 0x10);
                return;
            }

            try
            {
                if (!array.IsCreated)
                    return;

                void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(array);
                UnsafeUtility.MemClear(destinationPtr, array.Length * UnsafeUtility.SizeOf<T>());
            }
            finally
            {
                lane.ReleaseWriteLock();
            }
        }

        private bool TryBulkCopyLaneToNative<T>(
            in InventoryVaultLane<T> source,
            int sourceStartIndex,
            NativeArray<T> destination,
            int destinationStartIndex,
            int length) where T : struct
        {
            if (!source.TryResolve(out NativeArray<T> sourceArray))
            {
                WriteInventoryVaultFaultFrame(in source, 0x20);
                return false;
            }

            return InventorySoAUtility.TryBulkCopySlice(
                sourceArray,
                sourceStartIndex,
                destination,
                destinationStartIndex,
                length);
        }

        private bool TryBulkCopyLaneToLane<T>(
            in InventoryVaultLane<T> source,
            int sourceStartIndex,
            ref InventoryVaultLane<T> destination,
            int destinationStartIndex,
            int length) where T : struct
        {
            if (!source.TryResolve(out NativeArray<T> sourceArray))
            {
                WriteInventoryVaultFaultFrame(in source, 0x20);
                return false;
            }

            if (!destination.TryAcquireWriteLock(out NativeArray<T> destinationArray))
            {
                WriteInventoryVaultFaultFrame(in destination, 0x10);
                return false;
            }

            try
            {
                return InventorySoAUtility.TryBulkCopySlice(
                    sourceArray,
                    sourceStartIndex,
                    destinationArray,
                    destinationStartIndex,
                    length);
            }
            finally
            {
                destination.ReleaseWriteLock();
            }
        }

        private bool TryClearLaneSlice<T>(
            ref InventoryVaultLane<T> lane,
            int startIndex,
            int length) where T : struct
        {
            if (!lane.TryAcquireWriteLock(out NativeArray<T> array))
            {
                WriteInventoryVaultFaultFrame(in lane, 0x10);
                return false;
            }

            try
            {
                return InventorySoAUtility.TryClearSlice(array, startIndex, length);
            }
            finally
            {
                lane.ReleaseWriteLock();
            }
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

        private void CopyNativeArray(InventoryVaultLane<ushort> source, InventoryVaultLane<ushort> destination)
        {
            if (!source.TryResolve(out NativeArray<ushort> sourceArray))
            {
                WriteInventoryVaultFaultFrame(in source, 0x20);
                return;
            }

            if (!destination.TryAcquireWriteLock(out NativeArray<ushort> destinationArray))
            {
                WriteInventoryVaultFaultFrame(in destination, 0x10);
                return;
            }

            try
            {
                CopyNativeArray(sourceArray, destinationArray);
            }
            finally
            {
                destination.ReleaseWriteLock();
            }
        }

        private void SwapAnchorState<T>(InventoryVaultLane<T> lane, int firstIndex, int secondIndex) where T : struct
        {
            if (!lane.TryAcquireWriteLock(out NativeArray<T> values))
            {
                WriteInventoryVaultFaultFrame(in lane, 0x10);
                return;
            }

            try
            {
                if (!values.IsCreated ||
                    firstIndex == secondIndex ||
                    (uint)firstIndex >= (uint)values.Length ||
                    (uint)secondIndex >= (uint)values.Length)
                {
                    return;
                }

                T temp = values[firstIndex];
                values[firstIndex] = values[secondIndex];
                values[secondIndex] = temp;
            }
            finally
            {
                lane.ReleaseWriteLock();
            }
        }

        private void MoveAnchorStateValue<T>(InventoryVaultLane<T> lane, int sourceIndex, int destinationIndex) where T : struct
        {
            if (!lane.TryAcquireWriteLock(out NativeArray<T> values))
            {
                WriteInventoryVaultFaultFrame(in lane, 0x10);
                return;
            }

            try
            {
                if (!values.IsCreated ||
                    sourceIndex == destinationIndex ||
                    (uint)sourceIndex >= (uint)values.Length ||
                    (uint)destinationIndex >= (uint)values.Length)
                {
                    return;
                }

                values[destinationIndex] = values[sourceIndex];
                values[sourceIndex] = default;
            }
            finally
            {
                lane.ReleaseWriteLock();
            }
        }
    
        #region JulesLink_InventoryItemDefragmentationConsolidationCalculator
        private static void JulesLink_InventoryItemDefragmentationConsolidationCalculator() { _ = typeof(Hecton8.PureLogic.Systems.InventoryItemDefragmentationConsolidationCalculator); }
        #endregion
}
}
