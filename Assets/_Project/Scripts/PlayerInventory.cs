// ============================================================================
// HECTON-8 - PlayerInventory.cs
// Native SOA-backed inventory owner. Managed ItemData resolution is seam-only.
// ============================================================================

namespace Hecton8.Inventory
{
    using System;
    using System.Runtime.InteropServices;
    using Hecton.Localization;
    using Hecton8.Audio;
    using Hecton8.Core;
    using Hecton8.Gameplay;
    using Hecton8.Interaction;
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
        private const ushort DefaultQualityMilli = 1000;
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
        private const string RadixSortBufferMismatchLog = "[PlayerInventory] Critical radix sort buffer mismatch. Sorting bypassed.";
        private const string RadixSortEntriesTempLabel = "RadixSortEntriesTemp";
        private const string RadixSortScratchTempLabel = "RadixSortScratchTemp";
        private const string RadixSortCountsTempLabel = "RadixSortCountsTemp";
        private const string NativeMemoryOwner = nameof(PlayerInventory);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;
        private const int InventoryShadowBufferBytes = 16 * 1024;
        private const uint Fnv1a32Offset = 2166136261u;
        private const uint Fnv1a32Prime = 16777619u;
        internal const ushort DegradedQualityMilliThreshold = 250;
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
        private static readonly ProfilerMarker _slowTickProfilerMarker = new ProfilerMarker("H8.Inventory.PlayerInventory.SlowTick");
        private static readonly ProfilerMarker _radioactiveHalfLifeProfilerMarker = new ProfilerMarker("H8.Inventory.PlayerInventory.RadioactiveHalfLife");
        private static readonly ProfilerMarker _reactiveChemistryProfilerMarker = new ProfilerMarker("H8.Inventory.PlayerInventory.ReactiveChemistry");

        [Flags]
        public enum ItemGeneticFlags : byte
        {
            None = 0,
            Glow = 1 << 0,
            Toxic = 1 << 1,
            Edible = 1 << 2,
            Harvestable = 1 << 3
        }

        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private struct InventorySortEntry
        {
            public ulong PackedKey;
            public int OriginalIndex;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct InventoryRadixSortJob : IJob
        {
            private const int ByteBucketCount = 256;
            private const int PackedKeyPassCount = 6;

            public NativeArray<InventorySortEntry> Entries;
            public NativeArray<InventorySortEntry> Scratch;
            public NativeArray<int> Counts;
            public int Count;

            public void Execute()
            {
                if (Count <= 1)
                    return;

                NativeArray<InventorySortEntry> source = Entries;
                NativeArray<InventorySortEntry> destination = Scratch;
                bool sourceIsEntries = true;

                for (int pass = 0; pass < PackedKeyPassCount; pass++)
                {
                    for (int bucket = 0; bucket < ByteBucketCount; bucket++)
                        Counts[bucket] = 0;

                    int shift = pass * 8;
                    for (int index = 0; index < Count; index++)
                    {
                        int bucket = (int)((source[index].PackedKey >> shift) & 0xFFuL);
                        Counts[bucket]++;
                    }

                    int runningOffset = 0;
                    for (int bucket = 0; bucket < ByteBucketCount; bucket++)
                    {
                        int bucketCount = Counts[bucket];
                        Counts[bucket] = runningOffset;
                        runningOffset += bucketCount;
                    }

                    for (int index = 0; index < Count; index++)
                    {
                        InventorySortEntry entry = source[index];
                        int bucket = (int)((entry.PackedKey >> shift) & 0xFFuL);
                        int writeIndex = Counts[bucket];
                        destination[writeIndex] = entry;
                        Counts[bucket] = writeIndex + 1;
                    }

                    NativeArray<InventorySortEntry> swap = source;
                    source = destination;
                    destination = swap;
                    sourceIsEntries = !sourceIsEntries;
                }

                if (!sourceIsEntries)
                {
                    for (int index = 0; index < Count; index++)
                        Entries[index] = source[index];
                }
            }
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

                for (int anchorIndex = 0; anchorIndex < count; anchorIndex++)
                {
                    if (AnchorHashIds[anchorIndex] == 0 || StackCounts[anchorIndex] == 0)
                        continue;

                    float radiationSv = AnchorUnitRadiationSv[anchorIndex];
                    if (!(radiationSv > 0f))
                        continue;

                    ushort currentFlags = (ushort)(ItemStateFlags[anchorIndex] | RadioactiveMask);
                    ushort currentQualityMilli = QualityMilli[anchorIndex] > 0 ? QualityMilli[anchorIndex] : DefaultQuality;
                    float currentQuality = math.clamp(currentQualityMilli / 1000f, 0f, 1f);
                    float halfLifeSeconds = safeBaseHalfLifeSeconds / math.max(0.001f, radiationSv);
                    float decayFactor = ApproximateExpNegPositiveInput((Ln2 / halfLifeSeconds) * DeltaSeconds);
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
            public uint lastUpdateUnixSeconds;
            public float weight;
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

        [Header("── References ─────────────────────")]
        [Tooltip("Optional survival system weight sink.")]
        [SerializeField] private HectonSurvivalSystem survival;
        [Tooltip("Item catalog used for load-time and UI seam resolution.")]
        [SerializeField] private ItemCatalog itemCatalog;
        [Tooltip("Inventory radiation threshold in Sv before carried isotopes push trauma every SlowTick.")]
        [SerializeField, Min(0f)] private float radiationTraumaThresholdSv = 0.5f;

        private InventoryGrid _grid;
        private NativeArray<ushort> _stackCounts;
        private NativeArray<ushort> _craftLockedCounts;
        private NativeArray<ushort> _anchorStateFlags;
        private NativeArray<ushort> _itemStateFlags;
        private NativeArray<byte> _itemGenetics;
        private NativeArray<ushort> _qualityMilli;
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
        private ItemPlacement[] _sortBuffer;
        private ItemPlacement[] _sortedPlacements;
        private JobHandle _massVolumeJobHandle;
        private HectonPlayerMovement _movementLoadSink;
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

        public float TotalWeight { get; private set; }
        public float TotalMassKg { get; private set; }
        public float TotalVolumeM3 { get; private set; }
        public float TotalRadiationSv { get; private set; }
        public float CachedInventoryLoad01 { get; private set; }
        public float CachedMaxSwimSpeedMultiplier { get; private set; } = 1f;
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
            // COLD ALLOC: ushort[columns * rows] — anchor stack counts — owner: PlayerInventory
            _stackCounts = new NativeArray<ushort>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory);
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
            RegisterNativeMemorySentinel();
            _sortBuffer = new ItemPlacement[columns * rows];
            // COLD ALLOC: ItemPlacement[columns * rows] — placement reorder buffer — owner: PlayerInventory
            _sortedPlacements = new ItemPlacement[columns * rows];
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

            DisposeNativeArray(ref _stackCounts);
            DisposeNativeArray(ref _craftLockedCounts);
            DisposeNativeArray(ref _anchorStateFlags);
            DisposeNativeArray(ref _itemStateFlags);
            DisposeNativeArray(ref _itemGenetics);
            DisposeNativeArray(ref _qualityMilli);
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

        }

        private void RegisterNativeMemorySentinel()
        {
            NativeMemorySentinel.RegisterNativeArray(_stackCounts, NativeMemoryOwner, nameof(_stackCounts), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_craftLockedCounts, NativeMemoryOwner, nameof(_craftLockedCounts), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_anchorStateFlags, NativeMemoryOwner, nameof(_anchorStateFlags), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_itemStateFlags, NativeMemoryOwner, nameof(_itemStateFlags), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_itemGenetics, NativeMemoryOwner, nameof(_itemGenetics), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_qualityMilli, NativeMemoryOwner, nameof(_qualityMilli), NativeMemoryLifetime);
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
                _lastUpdateUnixSeconds[anchorIndex] = 0;
                ClearAnchorPhysicalMetadata(anchorIndex);
            }

            TotalWeight = Mathf.Max(0f, TotalWeight - descriptor.Weight);
            NotifyInventoryChanged();
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
                ApplyInventoryEnvironmentalDegradation();
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
            CompleteInventoryMassRecomputeJob(forceComplete: false);
        }

        public bool TryCopyAvailableItemCountsNonAlloc(
            NativeParallelHashMap<int, int> destination,
            out int uniqueItemCount)
        {
            uniqueItemCount = 0;
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

                int availableCount = Mathf.Max(0, Mathf.Max(1, (int)_stackCounts[anchorIndex]) - GetReservedCraftCount(anchorIndex));
                if (availableCount <= 0)
                    continue;

                if (destination.TryGetValue(itemHashId, out int existingCount))
                {
                    destination[itemHashId] = existingCount + availableCount;
                    continue;
                }

                if (!destination.TryAdd(itemHashId, availableCount))
                {
                    destination.Clear();
                    uniqueItemCount = 0;
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
                cellIndex++;
            }

            dto.cellCount = cellIndex;
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

        public void SortInventory()
        {
            if (HasCraftReservations())
                return;

            int count = GetPlacements(_sortBuffer);
            if (count <= 0)
                return;

            if (!TryValidateRadixSortBuffers(count))
                return;

            NativeArray<InventorySortEntry> sortEntries = default;
            NativeArray<InventorySortEntry> sortScratch = default;
            NativeArray<int> sortCounts = default;
            try
            {
                sortEntries = new NativeArray<InventorySortEntry>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                sortScratch = new NativeArray<InventorySortEntry>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                sortCounts = new NativeArray<int>(256, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                RegisterTempJobArray(sortEntries, RadixSortEntriesTempLabel);
                RegisterTempJobArray(sortScratch, RadixSortScratchTempLabel);
                RegisterTempJobArray(sortCounts, RadixSortCountsTempLabel);

                for (int i = 0; i < count; i++)
                    sortEntries[i] = BuildInventorySortEntry(in _sortBuffer[i], i);

                JobHandle sortHandle = new InventoryRadixSortJob
                {
                    Entries = sortEntries,
                    Scratch = sortScratch,
                    Counts = sortCounts,
                    Count = count
                }.Schedule();

                // COLD SYNC JOB: explicit user sort command; no Tick/SlowTick barrier.
                DispatcherJobSwap.TryComplete(ref sortHandle, forceComplete: true);

                for (int i = 0; i < count; i++)
                    _sortedPlacements[i] = _sortBuffer[sortEntries[i].OriginalIndex];
            }
            finally
            {
                DisposeTempJobArray(ref sortCounts);
                DisposeTempJobArray(ref sortScratch);
                DisposeTempJobArray(ref sortEntries);
            }

            _grid.Clear();
            ClearNativeArray(_stackCounts);
            ClearCraftReservationState();
            ClearNativeArray(_itemStateFlags);
            ClearNativeArray(_itemGenetics);
            ClearNativeArray(_qualityMilli);
            ClearNativeArray(_lastUpdateUnixSeconds);
            ClearNativeArray(_anchorUnitMassKg);
            ClearNativeArray(_anchorUnitVolumeM3);
            ClearNativeArray(_anchorUnitRadiationSv);
            TotalWeight = 0f;

            for (int i = 0; i < count; i++)
            {
                ItemPlacement placement = _sortedPlacements[i];
                InventoryGrid.InventoryItemDescriptor descriptor = placement.Descriptor;
                if (_grid.TryAddItem(in descriptor, out int px, out int py))
                {
                    int anchorIndex = AnchorIndex(px, py);
                    _stackCounts[anchorIndex] = placement.stackCount;
                    _itemStateFlags[anchorIndex] = placement.stateFlags;
                    _itemGenetics[anchorIndex] = SanitizeItemGeneticsFlags(placement.geneticsMask);
                    _qualityMilli[anchorIndex] = placement.qualityMilli;
                    _lastUpdateUnixSeconds[anchorIndex] = placement.lastUpdateUnixSeconds;
                    SyncAnchorPhysicalMetadata(anchorIndex, placement.itemHashId);
                    TotalWeight += placement.weight * placement.stackCount;
                }
            }

            NotifyInventoryChanged(massDirty: false);
        }

        private bool TryValidateRadixSortBuffers(int itemCount)
        {
            if (_sortBuffer == null ||
                _sortedPlacements == null ||
                itemCount > _sortBuffer.Length ||
                itemCount > _sortedPlacements.Length)
            {
                return false;
            }

            bool lengthMismatch = _sortBuffer.Length != _sortedPlacements.Length;
            if (!lengthMismatch)
                return true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError(RadixSortBufferMismatchLog);
#endif
            return false;
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
                    lastUpdateUnixSeconds = _lastUpdateUnixSeconds[anchorIndex],
                    weight = descriptor.Weight,
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

            bool allAdded = true;
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

                int transfer = math.min(maxStack - stackCount, remainingQuantity);
                _stackCounts[anchorIndex] = (ushort)(stackCount + transfer);
                _itemStateFlags[anchorIndex] = itemStateFlags;
                _itemGenetics[anchorIndex] = geneticsMask;
                _qualityMilli[anchorIndex] = qualityMilli;
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
                !TryBuildDescriptor(itemHashId, out InventoryGrid.InventoryItemDescriptor descriptor))
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

                    int stackCapacity = descriptor.MaxStack - stackCount;
                    int transfer = math.min(stackCapacity, remaining);
                    _scavengeSimStackCounts[anchorIndex] = (ushort)(stackCount + transfer);
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

        private static ulong PackInventorySortKey(byte categoryId, byte rarity, uint hashId)
        {
            return ((ulong)categoryId << 40)
                | ((ulong)rarity << 32)
                | hashId;
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
                if (_lastUpdateUnixSeconds.IsCreated)
                    _lastUpdateUnixSeconds[anchorIndex] = placement.lastUpdateUnixSeconds;
                SyncAnchorPhysicalMetadata(anchorIndex, placement.itemHashId);
                TotalWeight += placement.weight * Mathf.Max(1, placement.stackCount);
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

        private static InventorySortEntry BuildInventorySortEntry(in ItemPlacement placement, int originalIndex)
        {
            if (placement.itemHashId == 0)
            {
                return new InventorySortEntry
                {
                    PackedKey = PackInventorySortKey(byte.MaxValue, byte.MaxValue, uint.MaxValue),
                    OriginalIndex = originalIndex
                };
            }

            return new InventorySortEntry
            {
                PackedKey = PackInventorySortKey(placement.categoryId, placement.rarity, unchecked((uint)placement.itemHashId)),
                OriginalIndex = originalIndex
            };
        }

        private void NotifyInventoryChanged(bool markDirty = true, bool massDirty = true)
        {
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
            InventoryEvents.NotifyInventoryChanged();
            InventoryChanged?.Invoke();
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
            TotalMassKg = math.max(0f, totals.x);
            TotalVolumeM3 = math.max(0f, totals.y);
            TotalRadiationSv = math.max(0f, totals.z);
            TotalWeight = TotalMassKg;
            if (survival != null)
                survival.SetWeight(TotalMassKg);

            float carryCapacityKg = ResolveCarryCapacityKilograms();
            CachedInventoryLoad01 = math.saturate(TotalMassKg / carryCapacityKg);
            CachedMaxSwimSpeedMultiplier = math.lerp(1f, InventoryLoadMinimumMovementMultiplier, CachedInventoryLoad01);
            HectonPlayerMovement movement = TryResolveMovementLoadSink();
            if (movement != null)
                movement.ApplyRuntimeInventoryMassLoad(TotalMassKg, carryCapacityKg, CachedMaxSwimSpeedMultiplier, CachedInventoryLoad01);
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

        private HectonPlayerMovement TryResolveMovementLoadSink()
        {
            if (_movementLoadSink != null)
                return _movementLoadSink;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null)
                _movementLoadSink = playerContext.PlayerMovement;

            if (_movementLoadSink == null)
                TryGetComponent(out _movementLoadSink);

            return _movementLoadSink;
        }

        private bool ApplyEnvironmentalDegradation(
            int anchorIndex,
            in ItemCatalog.ItemRuntimeDescriptor runtimeDescriptor,
            bool isSubmerged,
            float temperatureFactor,
            uint nowTimestamp)
        {
            ushort currentQualityMilli = _qualityMilli[anchorIndex] > 0 ? _qualityMilli[anchorIndex] : DefaultQualityMilli;
            float currentQuality = math.clamp(currentQualityMilli / 1000f, 0f, 1f);
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

            DamageSignal signal = new DamageSignal
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

            float depthFactor = math.saturate((depthMeters - PressureCrushDepthMeters) / 1000f);
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
            float hazard01 = math.saturate(excess / math.max(0.01f, threshold));
            if (hazard01 <= 0f)
                return;

            DamageSignal signal = new DamageSignal
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
            return Mathf.Max(0f, impactSignal.Force / (playerMass * 9.81f));
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
            ushort nextQualityMilli = (ushort)(currentQualityMilli / 2);
            if (nextQualityMilli == currentQualityMilli && currentQualityMilli > 0)
                nextQualityMilli = (ushort)Mathf.Max(0, currentQualityMilli - 1);

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
                _lastUpdateUnixSeconds[anchorIndex] = 0;
                ClearAnchorPhysicalMetadata(anchorIndex);
                TotalWeight = Mathf.Max(0f, TotalWeight - descriptor.Weight * stackCount);
                return true;
            }

            bool changed = nextQualityMilli != currentQualityMilli;
            if (!changed)
                return false;

            _qualityMilli[anchorIndex] = nextQualityMilli;
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
            float currentQuality = math.clamp((_qualityMilli[anchorIndex] > 0 ? _qualityMilli[anchorIndex] : DefaultQualityMilli) / 1000f, 0f, 1f);
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
            return math.saturate(1f / (1f + x + (0.48f * x2) + (0.235f * x2 * x)));
        }

        private static float ApproximateExpSigned(float x)
        {
            return x < 0f
                ? ApproximateExpNegPositiveInput(-x)
                : 1f / ApproximateExpNegPositiveInput(math.min(x, 4f));
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
