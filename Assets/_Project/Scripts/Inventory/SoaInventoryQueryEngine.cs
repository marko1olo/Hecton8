namespace Hecton8.Inventory
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Reflection;
    using System.Threading;
    using Hecton8.Core;
    using Hecton8.Core.Memory;
    using Unity.Burst;
    using Unity.Burst.CompilerServices;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using Unity.Mathematics;

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct InventorySoaQueryResultDTO
    {
        [FieldOffset(0)] public uint TargetHashID;
        [FieldOffset(4)] public int FirstIndex;
        [FieldOffset(8)] public uint QuantityTotal;
        [FieldOffset(12)] public uint MatchCount;
        [FieldOffset(16)] public int ActiveSlotCount;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] private byte _pad0;
        [FieldOffset(25)] private byte _pad1;
        [FieldOffset(26)] private byte _pad2;
        [FieldOffset(27)] private byte _pad3;
        [FieldOffset(28)] private byte _pad4;
        [FieldOffset(29)] private byte _pad5;
        [FieldOffset(30)] private byte _pad6;
        [FieldOffset(31)] private byte _pad7;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct InventorySoaMutationResultDTO
    {
        [FieldOffset(0)] public uint TargetHashID;
        [FieldOffset(4)] public int SlotIndex;
        [FieldOffset(8)] public uint PreviousQuantity;
        [FieldOffset(12)] public uint NewQuantity;
        [FieldOffset(16)] public int ActiveBefore;
        [FieldOffset(20)] public int ActiveAfter;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Status;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct InventoryCapacityProfileDTO
    {
        [FieldOffset(0)] public uint ProfileHash;
        [FieldOffset(4)] public int SlotCapacity;
        [FieldOffset(8)] public int MinQueryBatch;
        [FieldOffset(12)] public int MaxQueryBatch;
        [FieldOffset(16)] public float TelemetryCadenceSeconds;
        [FieldOffset(20)] public float DropImpulseScale;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct InventorySoaTelemetryEntry
    {
        [FieldOffset(0)] public ulong LayoutHash;
        [FieldOffset(8)] public ulong Reserved0;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public uint TargetHashID;
        [FieldOffset(24)] public int FirstIndex;
        [FieldOffset(28)] public uint QuantityTotal;
        [FieldOffset(32)] public uint MatchCount;
        [FieldOffset(36)] public int ActiveSlotCount;
        [FieldOffset(40)] public int Capacity;
        [FieldOffset(44)] public float EstimatedMicroseconds;
        [FieldOffset(48)] public float GlobalQualityWeight;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public int MutationIndex;
        [FieldOffset(60)] public int MutationDelta;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public struct InventorySoaVaultLane
    {
        [FieldOffset(0)]
        public uint BufferID;

        [FieldOffset(4)]
        public uint SystemID;

        [FieldOffset(8)]
        public uint Generation;

        [FieldOffset(12)]
        public uint Flags;

        [FieldOffset(16)]
        public uint ExpectedBufferID;

        [FieldOffset(20)]
        public int Length;

        public void SetHandle<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            BufferID = handle.BufferID;
            SystemID = handle.SystemID;
            Generation = handle.Generation;
            Flags = handle.Flags;
        }

        public VaultGenerationHandle<T> ToHandle<T>() where T : struct
        {
            VaultGenerationHandle<T> handle = default;
            handle.BufferID = BufferID;
            handle.SystemID = SystemID;
            handle.Generation = Generation;
            handle.Flags = Flags;
            return handle;
        }
    }

    public struct InventorySoaVaultHandles
    {
        public InventorySoaVaultLane ItemHashIDs;
        public InventorySoaVaultLane Quantities;
        public InventorySoaVaultLane Durabilities;
        public InventorySoaVaultLane ActiveSlotCount;
        public InventorySoaVaultLane TelemetryRing;
        public InventorySoaVaultLane TelemetryCursor;
        public InventorySoaVaultLane CapacityProfiles;
    }

    public ref struct InventorySoaVaultBuffers
    {
        public NativeArray<uint> ItemHashIDs;
        public NativeArray<int> Quantities;
        public NativeArray<float> Durabilities;
        public NativeArray<int> ActiveSlotCount;
        public NativeArray<InventorySoaTelemetryEntry> TelemetryRing;
        public NativeArray<int> TelemetryCursor;
        public NativeArray<InventoryCapacityProfileDTO> CapacityProfiles;
    }

    public static unsafe partial class SoaInventoryQueryEngine
    {
        public const int TelemetryCapacity = 300;
        public const int DefaultSlotCapacity = 512;
        public const int CapacityProfileCapacity = 64;

        /// <summary>
        /// Sentinel for <see cref="ResolveScanWindow"/>: scan every slot from the start offset to the
        /// end of the shortest bound lane. A requested count of zero means "the active region is
        /// empty", never "scan everything".
        /// </summary>
        public const int ScanToBufferEnd = -1;

        public const int QueryResultDtoSizeBytes = 32;
        public const int MutationResultDtoSizeBytes = 32;
        public const int CapacityProfileDtoSizeBytes = 32;
        public const int TelemetryEntrySizeBytes = 64;
        public const uint DumpMagic = 0x534F4131u; // SOA1
        public const uint DumpVersion = 1u;
        public const ulong LayoutHash = 0x5348494E4F425533UL; // SHINOBU3
        public const string DumpPath = "Docs/AgentLogs/Dump_SHINOBU_316.bin";
        private const double FloatCastClampMeters = 3.4028234663852886e38d;

        public const uint ResultFound = 1u << 0;
        public const uint ResultInserted = 1u << 1;
        public const uint ResultRemoved = 1u << 2;
        public const uint ResultAtomicConflict = 1u << 3;
        public const uint ResultInvalidInput = 1u << 4;
        public const uint ResultSwapPop = 1u << 5;
        public const uint ResultSse2 = 1u << 6;
        public const uint ResultOwnerPhaseFrame = 1u << 7;
        public const uint ResultNaNFault = 1u << 8;
        public const uint ResultQuantityUIntView = 1u << 9;
        public const uint ResultAvx2 = 1u << 10;
        public const uint ResultNeon = 1u << 11;

        public static bool RuntimeLayoutValid()
        {
            return UnsafeUtility.SizeOf<InventorySoaQueryResultDTO>() == QueryResultDtoSizeBytes &&
                   OffsetOf<InventorySoaQueryResultDTO>(nameof(InventorySoaQueryResultDTO.TargetHashID)) == 0 &&
                   OffsetOf<InventorySoaQueryResultDTO>(nameof(InventorySoaQueryResultDTO.FirstIndex)) == 4 &&
                   OffsetOf<InventorySoaQueryResultDTO>(nameof(InventorySoaQueryResultDTO.QuantityTotal)) == 8 &&
                   OffsetOf<InventorySoaQueryResultDTO>(nameof(InventorySoaQueryResultDTO.MatchCount)) == 12 &&
                   OffsetOf<InventorySoaQueryResultDTO>(nameof(InventorySoaQueryResultDTO.ActiveSlotCount)) == 16 &&
                   OffsetOf<InventorySoaQueryResultDTO>(nameof(InventorySoaQueryResultDTO.Flags)) == 20 &&
                   UnsafeUtility.SizeOf<InventorySoaMutationResultDTO>() == MutationResultDtoSizeBytes &&
                   UnsafeUtility.SizeOf<InventoryCapacityProfileDTO>() == CapacityProfileDtoSizeBytes &&
                   UnsafeUtility.SizeOf<InventorySoaTelemetryEntry>() == TelemetryEntrySizeBytes &&
                   OffsetOf<InventorySoaTelemetryEntry>(nameof(InventorySoaTelemetryEntry.LayoutHash)) == 0 &&
                   OffsetOf<InventorySoaTelemetryEntry>(nameof(InventorySoaTelemetryEntry.Reserved0)) == 8 &&
                   OffsetOf<InventorySoaTelemetryEntry>(nameof(InventorySoaTelemetryEntry.Frame)) == 16 &&
                   OffsetOf<InventorySoaTelemetryEntry>(nameof(InventorySoaTelemetryEntry.TargetHashID)) == 20 &&
                   OffsetOf<InventorySoaTelemetryEntry>(nameof(InventorySoaTelemetryEntry.FirstIndex)) == 24 &&
                   OffsetOf<InventorySoaTelemetryEntry>(nameof(InventorySoaTelemetryEntry.QuantityTotal)) == 28 &&
                   OffsetOf<InventorySoaTelemetryEntry>(nameof(InventorySoaTelemetryEntry.MatchCount)) == 32 &&
                   OffsetOf<InventorySoaTelemetryEntry>(nameof(InventorySoaTelemetryEntry.ActiveSlotCount)) == 36 &&
                   OffsetOf<InventorySoaTelemetryEntry>(nameof(InventorySoaTelemetryEntry.Capacity)) == 40 &&
                   OffsetOf<InventorySoaTelemetryEntry>(nameof(InventorySoaTelemetryEntry.EstimatedMicroseconds)) == 44 &&
                   OffsetOf<InventorySoaTelemetryEntry>(nameof(InventorySoaTelemetryEntry.GlobalQualityWeight)) == 48 &&
                   OffsetOf<InventorySoaTelemetryEntry>(nameof(InventorySoaTelemetryEntry.Flags)) == 52 &&
                   OffsetOf<InventorySoaTelemetryEntry>(nameof(InventorySoaTelemetryEntry.MutationIndex)) == 56 &&
                   OffsetOf<InventorySoaTelemetryEntry>(nameof(InventorySoaTelemetryEntry.MutationDelta)) == 60;
        }

        private static int OffsetOf<T>(string fieldName) where T : struct
        {
            FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field != null ? UnsafeUtility.GetFieldOffset(field) : -1;
        }

        public static InventorySoaVaultHandles EnsureVaultBuffers(
            IDataVault vault,
            int slotCapacity,
            int profileCapacity = CapacityProfileCapacity)
        {
            InventorySoaVaultHandles handles = default;
            if (vault == null || !RuntimeLayoutValid())
                return handles;

            int safeSlotCapacity = math.max(DefaultSlotCapacity, slotCapacity);
            int safeProfileCapacity = math.max(1, profileCapacity);
            handles.ItemHashIDs = AcquireLane<uint>(
                vault,
                BufferID.ShinobuInventoryHashes,
                safeSlotCapacity,
                SystemID.GameplayPlayer,
                NativeArrayOptions.UninitializedMemory);
            handles.Quantities = AcquireLane<int>(
                vault,
                BufferID.ShinobuInventoryQuantities,
                safeSlotCapacity,
                SystemID.GameplayPlayer,
                NativeArrayOptions.UninitializedMemory);
            handles.Durabilities = AcquireLane<float>(
                vault,
                BufferID.ShinobuInventoryDurabilities,
                safeSlotCapacity,
                SystemID.GameplayPlayer,
                NativeArrayOptions.UninitializedMemory);
            handles.ActiveSlotCount = AcquireLane<int>(
                vault,
                BufferID.ShinobuInventoryActiveSlotCount,
                1,
                SystemID.GameplayPlayer,
                NativeArrayOptions.ClearMemory);
            handles.TelemetryRing = AcquireLane<InventorySoaTelemetryEntry>(
                vault,
                BufferID.ShinobuInventorySoaTelemetry,
                TelemetryCapacity,
                SystemID.GameplayPlayer,
                NativeArrayOptions.ClearMemory);
            handles.TelemetryCursor = AcquireLane<int>(
                vault,
                BufferID.ShinobuInventorySoaTelemetryCursor,
                1,
                SystemID.GameplayPlayer,
                NativeArrayOptions.ClearMemory);
            handles.CapacityProfiles = AcquireLane<InventoryCapacityProfileDTO>(
                vault,
                BufferID.ShinobuInventorySoaCapacityProfiles,
                safeProfileCapacity,
                SystemID.GameplayPlayer,
                NativeArrayOptions.ClearMemory);
            return handles;
        }

        public static bool TryResolveVaultBuffers(
            IDataVault vault,
            ref InventorySoaVaultHandles handles,
            out InventorySoaVaultBuffers buffers)
        {
            buffers = default;
            if (vault == null)
                return false;

            buffers.ItemHashIDs = OpenLane<uint>(vault, in handles.ItemHashIDs);
            buffers.Quantities = OpenLane<int>(vault, in handles.Quantities);
            buffers.Durabilities = OpenLane<float>(vault, in handles.Durabilities);
            buffers.ActiveSlotCount = OpenLane<int>(vault, in handles.ActiveSlotCount);
            buffers.TelemetryRing = OpenLane<InventorySoaTelemetryEntry>(vault, in handles.TelemetryRing);
            buffers.TelemetryCursor = OpenLane<int>(vault, in handles.TelemetryCursor);
            buffers.CapacityProfiles = OpenLane<InventoryCapacityProfileDTO>(vault, in handles.CapacityProfiles);
            return buffers.ItemHashIDs.IsCreated &&
                   buffers.Quantities.IsCreated &&
                   buffers.Durabilities.IsCreated &&
                   buffers.ActiveSlotCount.IsCreated &&
                   buffers.TelemetryRing.IsCreated &&
                   buffers.TelemetryCursor.IsCreated &&
                   buffers.CapacityProfiles.IsCreated;
        }

        public static bool TryReadVaultBuffers(
            IDataVault vault,
            in InventorySoaVaultHandles handles,
            out InventorySoaVaultBuffers buffers)
        {
            buffers = default;
            if (vault == null)
                return false;

            buffers.ItemHashIDs = ReadLane<uint>(vault, in handles.ItemHashIDs);
            buffers.Quantities = ReadLane<int>(vault, in handles.Quantities);
            buffers.Durabilities = ReadLane<float>(vault, in handles.Durabilities);
            buffers.ActiveSlotCount = ReadLane<int>(vault, in handles.ActiveSlotCount);
            buffers.TelemetryRing = ReadLane<InventorySoaTelemetryEntry>(vault, in handles.TelemetryRing);
            buffers.TelemetryCursor = ReadLane<int>(vault, in handles.TelemetryCursor);
            buffers.CapacityProfiles = ReadLane<InventoryCapacityProfileDTO>(vault, in handles.CapacityProfiles);
            return buffers.ItemHashIDs.IsCreated &&
                   buffers.Quantities.IsCreated &&
                   buffers.ActiveSlotCount.IsCreated;
        }

        internal static NativeArray<uint> AsUIntQuantityOwnerAlias(NativeArray<int> quantities)
        {
            if (!quantities.IsCreated)
                return default;

            return quantities.Reinterpret<uint>(UnsafeUtility.SizeOf<int>());
        }

        [System.Obsolete("Use AsUIntQuantityOwnerAlias; legacy mutable wrapper retained for compatibility.", false)]
        public static NativeArray<uint> AsUIntQuantityView(NativeArray<int> quantities)
        {
            return AsUIntQuantityOwnerAlias(quantities);
        }

        public static JobHandle ScheduleMockInventory(
            NativeArray<uint> itemHashIds,
            NativeArray<int> quantities,
            NativeArray<float> durabilities,
            NativeArray<int> activeSlotCount,
            int requestedCount,
            uint seed,
            JobHandle dependency = default)
        {
            return ScheduleMockInventory(
                itemHashIds,
                AsUIntQuantityOwnerAlias(quantities),
                durabilities,
                activeSlotCount,
                requestedCount,
                seed,
                dependency);
        }

        public static JobHandle ScheduleMockInventory(
            NativeArray<uint> itemHashIds,
            NativeArray<uint> quantities,
            NativeArray<float> durabilities,
            NativeArray<int> activeSlotCount,
            int requestedCount,
            uint seed,
            JobHandle dependency = default)
        {
            if (!itemHashIds.IsCreated || !quantities.IsCreated)
                return dependency;

            int count = math.clamp(requestedCount, 0, math.min(itemHashIds.Length, quantities.Length));
            if (count <= 0)
                return dependency;

            JobHandle countHandle = activeSlotCount.IsCreated && activeSlotCount.Length > 0
                ? new InitializeSoaActiveCountJob { ActiveSlotCount = activeSlotCount, ActiveCount = count }.Schedule(dependency)
                : dependency;

            return new GenerateMockSoaInventoryJob
            {
                ItemHashIDs = itemHashIds,
                Quantities = quantities,
                Durabilities = durabilities,
                Seed = seed
            }.Schedule(count, 128, countHandle);
        }

        public static JobHandle ScheduleQuery(
            NativeArray<uint> itemHashIds,
            NativeArray<int> quantities,
            NativeArray<int> activeSlotCount,
            NativeArray<InventorySoaQueryResultDTO> results,
            uint targetHashId,
            int resultIndex,
            int slotStart,
            int requestedSlotCount,
            JobHandle dependency = default)
        {
            return ScheduleQuery(
                itemHashIds,
                AsUIntQuantityOwnerAlias(quantities),
                activeSlotCount,
                results,
                targetHashId,
                resultIndex,
                slotStart,
                requestedSlotCount,
                dependency);
        }

        public static JobHandle ScheduleQuery(
            NativeArray<uint> itemHashIds,
            NativeArray<uint> quantities,
            NativeArray<int> activeSlotCount,
            NativeArray<InventorySoaQueryResultDTO> results,
            uint targetHashId,
            int resultIndex,
            int slotStart,
            int requestedSlotCount,
            JobHandle dependency = default)
        {
            if (!itemHashIds.IsCreated || !quantities.IsCreated || !results.IsCreated)
                return dependency;

            return new QueryInventoryHashJob
            {
                ItemHashIDs = itemHashIds,
                Quantities = quantities,
                ActiveSlotCount = activeSlotCount,
                Results = results,
                TargetHashID = targetHashId,
                ResultIndex = resultIndex,
                SlotStart = slotStart,
                RequestedSlotCount = requestedSlotCount
            }.Schedule(dependency);
        }

        public static JobHandle ScheduleQueryBatch(
            NativeArray<uint> itemHashIds,
            NativeArray<int> quantities,
            NativeArray<int> activeSlotCount,
            NativeArray<uint> targetHashIds,
            NativeArray<InventorySoaQueryResultDTO> results,
            float globalQualityWeight,
            JobHandle dependency = default)
        {
            return ScheduleQueryBatch(
                itemHashIds,
                AsUIntQuantityOwnerAlias(quantities),
                activeSlotCount,
                targetHashIds,
                results,
                globalQualityWeight,
                dependency);
        }

        public static JobHandle ScheduleQueryBatch(
            NativeArray<uint> itemHashIds,
            NativeArray<uint> quantities,
            NativeArray<int> activeSlotCount,
            NativeArray<uint> targetHashIds,
            NativeArray<InventorySoaQueryResultDTO> results,
            float globalQualityWeight,
            JobHandle dependency = default)
        {
            if (!itemHashIds.IsCreated || !quantities.IsCreated || !targetHashIds.IsCreated || !results.IsCreated)
                return dependency;

            int queryCount = math.min(targetHashIds.Length, results.Length);
            int batchCount = ResolveQueryBatchSize(globalQualityWeight, queryCount);
            if (batchCount <= 0)
                return dependency;

            return new QueryInventoryHashBatchJob
            {
                ItemHashIDs = itemHashIds,
                Quantities = quantities,
                ActiveSlotCount = activeSlotCount,
                TargetHashIDs = targetHashIds,
                Results = results,
                QueryCount = batchCount
            }.Schedule(batchCount, 32, dependency);
        }

        public static JobHandle ScheduleMutation(
            NativeArray<uint> itemHashIds,
            NativeArray<int> quantities,
            NativeArray<float> durabilities,
            NativeArray<int> activeSlotCount,
            NativeArray<InventorySoaMutationResultDTO> result,
            uint targetHashId,
            int quantityDelta,
            uint insertWhenMissing,
            uint removeWhenZero,
            float initialDurability01,
            JobHandle dependency = default)
        {
            return ScheduleMutation(
                itemHashIds,
                AsUIntQuantityOwnerAlias(quantities),
                durabilities,
                activeSlotCount,
                result,
                targetHashId,
                quantityDelta,
                insertWhenMissing,
                removeWhenZero,
                initialDurability01,
                dependency);
        }

        public static JobHandle ScheduleMutation(
            NativeArray<uint> itemHashIds,
            NativeArray<uint> quantities,
            NativeArray<float> durabilities,
            NativeArray<int> activeSlotCount,
            NativeArray<InventorySoaMutationResultDTO> result,
            uint targetHashId,
            int quantityDelta,
            uint insertWhenMissing,
            uint removeWhenZero,
            float initialDurability01,
            JobHandle dependency = default)
        {
            if (!itemHashIds.IsCreated || !quantities.IsCreated || !activeSlotCount.IsCreated || !result.IsCreated)
                return dependency;

            return new MutateInventoryQuantityJob
            {
                ItemHashIDs = itemHashIds,
                Quantities = quantities,
                Durabilities = durabilities,
                ActiveSlotCount = activeSlotCount,
                Result = result,
                TargetHashID = targetHashId,
                QuantityDelta = quantityDelta,
                InsertWhenMissing = insertWhenMissing,
                RemoveWhenZero = removeWhenZero,
                InitialDurability01 = initialDurability01
            }.Schedule(dependency);
        }

        public static bool TryApplyMutationOwnerPhase(
            NativeArray<uint> itemHashIds,
            NativeArray<uint> quantities,
            NativeArray<float> durabilities,
            NativeArray<int> activeSlotCount,
            uint targetHashId,
            int quantityDelta,
            uint insertWhenMissing,
            uint removeWhenZero,
            float initialDurability01,
            out InventorySoaMutationResultDTO mutation)
        {
            mutation = default;
            mutation.TargetHashID = targetHashId;
            mutation.SlotIndex = -1;
            if (!itemHashIds.IsCreated ||
                !quantities.IsCreated ||
                !activeSlotCount.IsCreated ||
                activeSlotCount.Length == 0 ||
                targetHashId == 0u ||
                itemHashIds.Length != quantities.Length)
            {
                mutation.Flags = ResultInvalidInput;
                mutation.Status = 1u;
                return false;
            }

            int activeBefore = AtomicReadActiveCount(activeSlotCount, itemHashIds.Length);
            ScanHashQuantity(
                itemHashIds,
                quantities,
                0,
                activeBefore,
                targetHashId,
                out int index,
                out _,
                out _,
                out uint queryFlags);

            mutation.Flags = queryFlags & (ResultAvx2 | ResultSse2 | ResultNeon | ResultQuantityUIntView);
            mutation.ActiveBefore = activeBefore;
            if (index < 0)
            {
                if (quantityDelta <= 0 || insertWhenMissing == 0u)
                {
                    mutation.Status = 2u;
                    return false;
                }

                index = AtomicReserveSlot(activeSlotCount, itemHashIds.Length);
                if (index < 0)
                {
                    mutation.Status = 3u;
                    mutation.Flags |= ResultInvalidInput;
                    return false;
                }

                itemHashIds[index] = targetHashId;
                quantities[index] = 0u;
                if (durabilities.IsCreated && index < durabilities.Length)
                    durabilities[index] = math.saturate(initialDurability01);
                mutation.Flags |= ResultInserted;
            }

            uint next = AtomicApplyQuantityDelta(quantities, index, quantityDelta, out uint previous, out uint conflict);
            mutation.SlotIndex = index;
            mutation.PreviousQuantity = previous;
            mutation.NewQuantity = next;
            mutation.Flags |= conflict | ResultFound;
            if (next == 0u && removeWhenZero != 0u)
                SwapAndPopOwnerPhase(itemHashIds, quantities, durabilities, activeSlotCount, ref mutation, index);

            mutation.ActiveAfter = AtomicReadActiveCount(activeSlotCount, itemHashIds.Length);
            return mutation.Status == 0u;
        }

        public static int ResolveQueryBatchSize(float globalQualityWeight, int queryCount)
        {
            if (queryCount <= 0)
                return 0;

            return InventoryRoutingNetwork.ResolveTimeSliceBatchSize(
                InventoryRoutingNetwork.SanitizeQualityWeight(globalQualityWeight),
                queryCount,
                minSlots: 1,
                maxSlots: math.max(1, queryCount));
        }

        public static float EstimateQueryMicroseconds(int activeSlots, int matchCount, float globalQualityWeight)
        {
            float safeQuality = InventoryRoutingNetwork.SanitizeQualityWeight(globalQualityWeight);
            float scannedLanes = math.max(1f, activeSlots * 0.25f);
            float matchCost = math.max(0f, matchCount) * 0.012f;
            return scannedLanes * math.lerp(0.006f, 0.011f, safeQuality) + matchCost;
        }

        public static float EstimateFrameQueryMicroseconds(int activeSlots, int queryCount, float globalQualityWeight)
        {
            if (activeSlots <= 0 || queryCount <= 0)
                return 0f;

            float safeQuality = InventoryRoutingNetwork.SanitizeQualityWeight(globalQualityWeight);
            float scannedLanesPerQuery = math.max(1f, activeSlots * 0.25f);
            float admittedQueries = math.max(1f, queryCount);
            float laneCost = math.lerp(0.006f, 0.011f, safeQuality);
            return scannedLanesPerQuery * admittedQueries * laneCost;
        }

        public static bool TryResolveDropRuntimePosition(
            double3 sourceAup,
            float3 localOffsetMeters,
            double3 committedOriginAup,
            out float3 runtimePosition)
        {
            runtimePosition = default;
            double3 dropAup = sourceAup + new double3(localOffsetMeters.x, localOffsetMeters.y, localOffsetMeters.z);
            double3 runtime = dropAup - committedOriginAup;
            if (!math.all(math.isfinite(dropAup)) || !math.all(math.isfinite(runtime)))
                return false;

            runtimePosition = ClampAupDeltaToFloat3(runtime);
            return math.all(math.isfinite(runtimePosition));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ClampAupDeltaToFloat3(double3 deltaAup)
        {
            float3 result = default;
            result.x = (float)math.clamp(deltaAup.x, -FloatCastClampMeters, FloatCastClampMeters);
            result.y = (float)math.clamp(deltaAup.y, -FloatCastClampMeters, FloatCastClampMeters);
            result.z = (float)math.clamp(deltaAup.z, -FloatCastClampMeters, FloatCastClampMeters);
            return result;
        }

        public static bool TryParseCapacityProfiles(
            ReadOnlySpan<byte> csvUtf8,
            NativeArray<InventoryCapacityProfileDTO> profiles,
            out int acceptedRows,
            out int rejectedRows)
        {
            acceptedRows = 0;
            rejectedRows = 0;
            if (!profiles.IsCreated || profiles.Length == 0 || csvUtf8.Length == 0)
                return false;

            int cursor = 0;
            while (acceptedRows < profiles.Length && TryReadLine(csvUtf8, ref cursor, out ReadOnlySpan<byte> line))
            {
                line = Trim(line);
                if (line.Length == 0 || line[0] == (byte)'#')
                    continue;

                if (IsCapacityProfileHeader(line))
                    continue;

                if (TryParseCapacityProfileLine(line, out InventoryCapacityProfileDTO profile))
                {
                    profiles[acceptedRows++] = profile;
                }
                else
                {
                    rejectedRows++;
                }
            }

            return acceptedRows > 0;
        }

        public static bool TryDumpTelemetry(
            NativeArray<InventorySoaTelemetryEntry> telemetryRing,
            int cursor,
            string relativePath = DumpPath)
        {
            if (!telemetryRing.IsCreated || telemetryRing.Length == 0)
                return false;

            try
            {
                int telemetryBytes = telemetryRing.Length * TelemetryEntrySizeBytes;
                int byteCount = TelemetryDumpHeaderBytes + telemetryBytes;
                NativeArray<byte> payload = default;
                try
                {
                    payload = NativeFaultDumpWriter.CreateTransientPayload(
                        byteCount,
                        nameof(SoaInventoryQueryEngine),
                        "InventorySoaTelemetryDumpPayload");
                    byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                    WriteUInt32LittleEndian(target, 0, DumpMagic);
                    WriteUInt32LittleEndian(target, 4, DumpVersion);
                    WriteInt32LittleEndian(target, 8, cursor);
                    WriteInt32LittleEndian(target, 12, telemetryRing.Length);
                    WriteInt32LittleEndian(target, 16, TelemetryEntrySizeBytes);
                    WriteUInt32LittleEndian(target, 20, unchecked((uint)LayoutHash));

                    void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetryRing);
                    UnsafeUtility.MemCpy(target + TelemetryDumpHeaderBytes, source, telemetryBytes);
                    return NativeFaultDumpWriter.TryWriteAll(relativePath, payload, byteCount);
                }
                finally
                {
                    NativeFaultDumpWriter.DisposeTransientPayload(
                        ref payload,
                        nameof(SoaInventoryQueryEngine),
                        "InventorySoaTelemetryDumpPayload");
                }
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }

        private const int TelemetryDumpHeaderBytes = 24;

        private static void WriteInt32LittleEndian(byte* target, int offset, int value)
        {
            WriteUInt32LittleEndian(target, offset, unchecked((uint)value));
        }

        private static void WriteUInt32LittleEndian(byte* target, int offset, uint value)
        {
            target[offset] = (byte)value;
            target[offset + 1] = (byte)(value >> 8);
            target[offset + 2] = (byte)(value >> 16);
            target[offset + 3] = (byte)(value >> 24);
        }

        private static InventorySoaVaultLane AcquireLane<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            SystemID owner,
            NativeArrayOptions options) where T : struct
        {
            if (vault == null || requiredLength <= 0)
                return default;

            VaultGenerationHandle<T> handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                owner,
                options);
            uint expectedBufferId = unchecked((uint)(int)bufferId);
            if (handle.BufferID != expectedBufferId || handle.Generation == 0u)
                return default;

            InventorySoaVaultLane lane = default;
            lane.SetHandle(in handle);
            lane.ExpectedBufferID = expectedBufferId;
            lane.Length = requiredLength;
            return lane;
        }

        private static NativeArray<T> OpenLane<T>(
            IDataVault vault,
            in InventorySoaVaultLane lane) where T : struct
        {
            VaultGenerationHandle<T> handle = lane.ToHandle<T>();
            if (vault == null ||
                lane.ExpectedBufferID == 0u ||
                lane.BufferID != lane.ExpectedBufferID ||
                lane.Generation == 0u ||
                lane.Length <= 0 ||
                !vault.TryResolveHandle(in handle, out NativeArray<T> buffer) ||
                !buffer.IsCreated ||
                buffer.Length < lane.Length)
            {
                return default;
            }

            return buffer;
        }

        private static NativeArray<T> ReadLane<T>(
            IDataVault vault,
            in InventorySoaVaultLane lane) where T : struct
        {
            VaultGenerationHandle<T> handle = lane.ToHandle<T>();
            if (vault == null ||
                lane.ExpectedBufferID == 0u ||
                lane.BufferID != lane.ExpectedBufferID ||
                lane.Generation == 0u ||
                lane.Length <= 0 ||
                !vault.TryReadHandle(in handle, out NativeArray<T> buffer) ||
                !buffer.IsCreated ||
                buffer.Length < lane.Length)
            {
                return default;
            }

            return buffer;
        }

        /// <summary>
        /// Resolves the half-open slot window [<paramref name="scanStart"/>, <paramref name="scanEnd"/>)
        /// a hash scan is allowed to touch. Every call site feeds an ACTIVE SLOT COUNT here, so
        /// <paramref name="requestedSlotCount"/> == 0 means the inventory holds nothing and the window
        /// must be empty. Only <see cref="ScanToBufferEnd"/> (any negative value) opens the window to
        /// the end of the shortest bound lane; the SOA tail past the active region is uninitialized
        /// vault memory and matching a stale hash there routes a pickup into a slot no consumer reads.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ResolveScanWindow(
            int hashLaneLength,
            int quantityLaneLength,
            int slotStart,
            int requestedSlotCount,
            out int scanStart,
            out int scanEnd)
        {
            int laneLength = math.min(math.max(0, hashLaneLength), math.max(0, quantityLaneLength));
            scanStart = math.clamp(slotStart, 0, laneLength);
            int available = laneLength - scanStart;
            int count = requestedSlotCount < 0
                ? available
                : math.min(requestedSlotCount, available);
            scanEnd = scanStart + math.max(0, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ScanHashQuantity(
            NativeArray<uint> itemHashIds,
            NativeArray<uint> quantities,
            int slotStart,
            int requestedSlotCount,
            uint targetHashId,
            out int firstIndex,
            out uint quantityTotal,
            out uint matchCount,
            out uint flags)
        {
            firstIndex = -1;
            quantityTotal = 0u;
            matchCount = 0u;
            flags = (X86.Avx2.IsAvx2Supported ? ResultAvx2 : X86.Sse2.IsSse2Supported ? ResultSse2 : Arm.Neon.IsNeonSupported ? ResultNeon : 0u) |
                    ResultQuantityUIntView;
            if (!itemHashIds.IsCreated || !quantities.IsCreated || targetHashId == 0u)
            {
                flags |= ResultInvalidInput;
                return;
            }

            ResolveScanWindow(
                itemHashIds.Length,
                quantities.Length,
                slotStart,
                requestedSlotCount,
                out int start,
                out int end);
            int cursor = start;
            if (X86.Avx2.IsAvx2Supported)
            {
                int vector8End = start + ((end - start) & ~7);
                for (int i = start; i < vector8End; i += 8)
                {
                    int mask = EqualMask8(itemHashIds, i, targetHashId);
                    while (mask != 0)
                    {
                        int lane = math.tzcnt(mask);
                        int index = i + lane;
                        uint quantity = math.max(1u, quantities[index]);
                        quantityTotal = SaturatingAdd(quantityTotal, quantity);
                        matchCount++;
                        if (firstIndex < 0)
                            firstIndex = index;
                        mask &= mask - 1;
                    }
                }

                cursor = vector8End;
            }

            int vectorEnd = cursor + ((end - cursor) & ~3);
            for (int i = cursor; i < vectorEnd; i += 4)
            {
                int mask = EqualMask4(itemHashIds, i, targetHashId);
                while (mask != 0)
                {
                    int lane = math.tzcnt(mask);
                    int index = i + lane;
                    uint quantity = math.max(1u, quantities[index]);
                    quantityTotal = SaturatingAdd(quantityTotal, quantity);
                    matchCount++;
                    if (firstIndex < 0)
                        firstIndex = index;
                    mask &= mask - 1;
                }
            }

            for (int i = vectorEnd; i < end; i++)
            {
                if (itemHashIds[i] != targetHashId)
                    continue;

                uint quantity = math.max(1u, quantities[i]);
                quantityTotal = SaturatingAdd(quantityTotal, quantity);
                matchCount++;
                if (firstIndex < 0)
                    firstIndex = i;
            }

            if (matchCount != 0u)
                flags |= ResultFound;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [IgnoreWarning(1305)]
        internal static int EqualMask4(NativeArray<uint> itemHashIds, int index, uint targetHashId)
        {
            if (X86.Sse2.IsSse2Supported)
            {
                v128 target = X86.Sse2.set1_epi32(unchecked((int)targetHashId));
                v128 keys = new v128(
                    unchecked((int)itemHashIds[index]),
                    unchecked((int)itemHashIds[index + 1]),
                    unchecked((int)itemHashIds[index + 2]),
                    unchecked((int)itemHashIds[index + 3]));
                return CollapseLaneMask4(X86.Sse2.movemask_epi8(X86.Sse2.cmpeq_epi32(keys, target)));
            }

            if (Arm.Neon.IsNeonSupported)
            {
                v128 target = new v128(targetHashId);
                v128 keys = new v128(
                    itemHashIds[index],
                    itemHashIds[index + 1],
                    itemHashIds[index + 2],
                    itemHashIds[index + 3]);
                v128 equals = Arm.Neon.vceqq_u32(keys, target);
                int mask = 0;
                mask |= Arm.Neon.vgetq_lane_u32(equals, 0) != 0u ? 1 : 0;
                mask |= Arm.Neon.vgetq_lane_u32(equals, 1) != 0u ? 2 : 0;
                mask |= Arm.Neon.vgetq_lane_u32(equals, 2) != 0u ? 4 : 0;
                mask |= Arm.Neon.vgetq_lane_u32(equals, 3) != 0u ? 8 : 0;
                return mask;
            }

            uint4 values = new uint4(
                itemHashIds[index],
                itemHashIds[index + 1],
                itemHashIds[index + 2],
                itemHashIds[index + 3]);
            return math.bitmask(values == new uint4(targetHashId)) & 0xF;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [IgnoreWarning(1305)]
        internal static int EqualMask8(NativeArray<uint> itemHashIds, int index, uint targetHashId)
        {
            if (X86.Avx2.IsAvx2Supported)
            {
                v256 target = X86.Avx.mm256_set1_epi32(unchecked((int)targetHashId));
                v256 keys = new v256(
                    unchecked((int)itemHashIds[index]),
                    unchecked((int)itemHashIds[index + 1]),
                    unchecked((int)itemHashIds[index + 2]),
                    unchecked((int)itemHashIds[index + 3]),
                    unchecked((int)itemHashIds[index + 4]),
                    unchecked((int)itemHashIds[index + 5]),
                    unchecked((int)itemHashIds[index + 6]),
                    unchecked((int)itemHashIds[index + 7]));
                return CollapseLaneMask8(X86.Avx2.mm256_movemask_epi8(X86.Avx2.mm256_cmpeq_epi32(keys, target)));
            }

            return EqualMask4(itemHashIds, index, targetHashId) |
                   (EqualMask4(itemHashIds, index + 4, targetHashId) << 4);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint SaturatingAdd(uint left, uint right)
        {
            uint sum = left + right;
            return sum < left ? uint.MaxValue : sum;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int CollapseLaneMask4(int byteMask)
        {
            int mask = 0;
            mask |= (byteMask & 0x000F) != 0 ? 1 : 0;
            mask |= (byteMask & 0x00F0) != 0 ? 2 : 0;
            mask |= (byteMask & 0x0F00) != 0 ? 4 : 0;
            mask |= (byteMask & 0xF000) != 0 ? 8 : 0;
            return mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int CollapseLaneMask8(int byteMask)
        {
            int mask = 0;
            mask |= (byteMask & 0x0000000F) != 0 ? 1 : 0;
            mask |= (byteMask & 0x000000F0) != 0 ? 2 : 0;
            mask |= (byteMask & 0x00000F00) != 0 ? 4 : 0;
            mask |= (byteMask & 0x0000F000) != 0 ? 8 : 0;
            mask |= (byteMask & 0x000F0000) != 0 ? 16 : 0;
            mask |= (byteMask & 0x00F00000) != 0 ? 32 : 0;
            mask |= (byteMask & 0x0F000000) != 0 ? 64 : 0;
            mask |= (byteMask & unchecked((int)0xF0000000)) != 0 ? 128 : 0;
            return mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ResolveActiveSlotCount(NativeArray<uint> itemHashIds, NativeArray<int> activeSlotCount)
        {
            if (!itemHashIds.IsCreated)
                return 0;

            if (!activeSlotCount.IsCreated || activeSlotCount.Length == 0)
                return itemHashIds.Length;

            return math.clamp(activeSlotCount[0], 0, itemHashIds.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ClampDelta(uint current, int delta)
        {
            long raw = (long)math.min(current, 0x7FFFFFFFu) + delta;
            if (raw <= 0L)
                return 0u;
            if (raw >= int.MaxValue)
                return int.MaxValue;
            return (uint)raw;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint AtomicApplyQuantityDelta(NativeArray<uint> quantities, int index, int delta, out uint previousQuantity, out uint conflict)
        {
            previousQuantity = 0u;
            conflict = 0u;
            uint* quantityPtr = (uint*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(quantities);
            ref int quantityRef = ref UnsafeUtility.AsRef<int>((int*)(quantityPtr + index));
            for (int attempt = 0; attempt < 16; attempt++)
            {
                int observed = Interlocked.CompareExchange(ref quantityRef, 0, 0);
                uint current = unchecked((uint)math.max(0, observed));
                uint next = ClampDelta(current, delta);
                int nextInt = next > int.MaxValue ? int.MaxValue : unchecked((int)next);
                if (Interlocked.CompareExchange(ref quantityRef, nextInt, observed) == observed)
                {
                    previousQuantity = current;
                    return next;
                }
            }

            conflict = ResultAtomicConflict;
            int finalObserved = Interlocked.CompareExchange(ref quantityRef, 0, 0);
            previousQuantity = unchecked((uint)math.max(0, finalObserved));
            return previousQuantity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int AtomicReserveSlot(NativeArray<int> activeSlotCount, int capacity)
        {
            if (!activeSlotCount.IsCreated || activeSlotCount.Length == 0 || capacity <= 0)
                return -1;

            int* countPtr = (int*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(activeSlotCount);
            ref int countRef = ref UnsafeUtility.AsRef<int>(countPtr);
            while (true)
            {
                int observed = Interlocked.CompareExchange(ref countRef, 0, 0);
                if ((uint)observed >= (uint)capacity)
                    return -1;

                if (Interlocked.CompareExchange(ref countRef, observed + 1, observed) == observed)
                    return observed;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int AtomicReadActiveCount(NativeArray<int> activeSlotCount, int capacity)
        {
            if (!activeSlotCount.IsCreated || activeSlotCount.Length == 0)
                return capacity;

            int* countPtr = (int*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(activeSlotCount);
            int observed = Interlocked.CompareExchange(ref UnsafeUtility.AsRef<int>(countPtr), 0, 0);
            return math.clamp(observed, 0, capacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int AtomicDecrementActiveCount(NativeArray<int> activeSlotCount)
        {
            int* countPtr = (int*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(activeSlotCount);
            return Interlocked.Decrement(ref UnsafeUtility.AsRef<int>(countPtr));
        }

        private static void SwapAndPopOwnerPhase(
            NativeArray<uint> itemHashIds,
            NativeArray<uint> quantities,
            NativeArray<float> durabilities,
            NativeArray<int> activeSlotCount,
            ref InventorySoaMutationResultDTO mutation,
            int index)
        {
            int newCount = AtomicDecrementActiveCount(activeSlotCount);
            int lastIndex = newCount;
            if ((uint)lastIndex >= (uint)itemHashIds.Length || index < 0)
            {
                mutation.Flags |= ResultInvalidInput;
                mutation.Status = 4u;
                return;
            }

            if (index != lastIndex)
            {
                itemHashIds[index] = itemHashIds[lastIndex];
                quantities[index] = quantities[lastIndex];
                if (durabilities.IsCreated && index < durabilities.Length && lastIndex < durabilities.Length)
                    durabilities[index] = durabilities[lastIndex];
                mutation.Flags |= ResultSwapPop;
            }

            itemHashIds[lastIndex] = 0u;
            quantities[lastIndex] = 0u;
            if (durabilities.IsCreated && lastIndex < durabilities.Length)
                durabilities[lastIndex] = 0f;
            mutation.Flags |= ResultRemoved;
        }

        private static bool TryReadLine(ReadOnlySpan<byte> bytes, ref int cursor, out ReadOnlySpan<byte> line)
        {
            line = default;
            if (cursor >= bytes.Length)
                return false;

            int start = cursor;
            while (cursor < bytes.Length && bytes[cursor] != (byte)'\n')
                cursor++;

            int end = cursor;
            if (cursor < bytes.Length && bytes[cursor] == (byte)'\n')
                cursor++;

            if (end > start && bytes[end - 1] == (byte)'\r')
                end--;

            line = bytes.Slice(start, end - start);
            return true;
        }

        private static bool TryParseCapacityProfileLine(ReadOnlySpan<byte> line, out InventoryCapacityProfileDTO profile)
        {
            profile = default;
            int cursor = 0;
            if (!TryReadToken(line, ref cursor, out ReadOnlySpan<byte> profileToken) ||
                !TryReadToken(line, ref cursor, out ReadOnlySpan<byte> capacityToken) ||
                !TryReadToken(line, ref cursor, out ReadOnlySpan<byte> minBatchToken) ||
                !TryReadToken(line, ref cursor, out ReadOnlySpan<byte> maxBatchToken))
            {
                return false;
            }

            TryReadToken(line, ref cursor, out ReadOnlySpan<byte> cadenceToken);
            TryReadToken(line, ref cursor, out ReadOnlySpan<byte> impulseToken);
            TryReadToken(line, ref cursor, out ReadOnlySpan<byte> flagsToken);

            uint profileHash = TryParseUInt(profileToken, out uint parsedHash)
                ? parsedHash
                : HashLowerAscii(profileToken);

            if (profileHash == 0u ||
                !TryParseInt(capacityToken, out int capacity) ||
                !TryParseInt(minBatchToken, out int minBatch) ||
                !TryParseInt(maxBatchToken, out int maxBatch))
            {
                return false;
            }

            float cadence = TryParseFloat(cadenceToken, out float parsedCadence) ? parsedCadence : 0.25f;
            float impulse = TryParseFloat(impulseToken, out float parsedImpulse) ? parsedImpulse : 1f;
            uint flags = TryParseUInt(flagsToken, out uint parsedFlags) ? parsedFlags : 0u;
            int safeCapacity = math.max(1, capacity);
            int safeMin = math.clamp(minBatch, 1, safeCapacity);
            int safeMax = math.clamp(math.max(maxBatch, safeMin), safeMin, safeCapacity);

            profile = new InventoryCapacityProfileDTO
            {
                ProfileHash = profileHash,
                SlotCapacity = safeCapacity,
                MinQueryBatch = safeMin,
                MaxQueryBatch = safeMax,
                TelemetryCadenceSeconds = math.max(0.016f, InventoryRoutingNetwork.SanitizeNonNegativeFinite(cadence)),
                DropImpulseScale = math.max(0f, math.isfinite(impulse) ? impulse : 1f),
                Flags = flags
            };
            return true;
        }

        private static bool TryReadToken(ReadOnlySpan<byte> line, ref int cursor, out ReadOnlySpan<byte> token)
        {
            token = default;
            if (cursor >= line.Length)
                return false;

            int start = cursor;
            while (cursor < line.Length && line[cursor] != (byte)',')
                cursor++;

            int end = cursor;
            if (cursor < line.Length && line[cursor] == (byte)',')
                cursor++;

            token = Trim(line.Slice(start, end - start));
            return true;
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && IsWhitespace(value[start]))
                start++;
            while (end >= start && IsWhitespace(value[end]))
                end--;
            return start <= end ? value.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static bool IsWhitespace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t' || value == (byte)'\r' || value == (byte)'\n';
        }

        private static bool IsCapacityProfileHeader(ReadOnlySpan<byte> line)
        {
            return line.Length >= 7 &&
                   ToLowerAscii(line[0]) == (byte)'p' &&
                   ToLowerAscii(line[1]) == (byte)'r' &&
                   ToLowerAscii(line[2]) == (byte)'o' &&
                   ToLowerAscii(line[3]) == (byte)'f' &&
                   ToLowerAscii(line[4]) == (byte)'i' &&
                   ToLowerAscii(line[5]) == (byte)'l' &&
                   ToLowerAscii(line[6]) == (byte)'e';
        }

        private static bool TryParseInt(ReadOnlySpan<byte> value, out int result)
        {
            result = 0;
            if (!TryParseUInt(value, out uint unsigned) || unsigned > int.MaxValue)
                return false;

            result = (int)unsigned;
            return true;
        }

        private static bool TryParseUInt(ReadOnlySpan<byte> value, out uint result)
        {
            result = 0u;
            value = Trim(value);
            if (value.Length == 0)
                return false;

            int cursor = 0;
            bool hex = value.Length > 2 &&
                       value[0] == (byte)'0' &&
                       (value[1] == (byte)'x' || value[1] == (byte)'X');
            if (hex)
                cursor = 2;

            for (; cursor < value.Length; cursor++)
            {
                byte b = value[cursor];
                uint digit;
                if (hex)
                {
                    if (b >= (byte)'0' && b <= (byte)'9')
                        digit = (uint)(b - (byte)'0');
                    else if (b >= (byte)'a' && b <= (byte)'f')
                        digit = (uint)(10 + b - (byte)'a');
                    else if (b >= (byte)'A' && b <= (byte)'F')
                        digit = (uint)(10 + b - (byte)'A');
                    else
                        return false;

                    result = (result << 4) | digit;
                }
                else
                {
                    if (b < (byte)'0' || b > (byte)'9')
                        return false;

                    result = result * 10u + (uint)(b - (byte)'0');
                }
            }

            return true;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> value, out float result)
        {
            result = 0f;
            value = Trim(value);
            if (value.Length == 0)
                return false;

            bool negative = false;
            int cursor = 0;
            if (value[0] == (byte)'-')
            {
                negative = true;
                cursor = 1;
            }

            double accumulator = 0d;
            double fractionScale = 0.1d;
            bool afterDecimal = false;
            bool sawDigit = false;
            for (; cursor < value.Length; cursor++)
            {
                byte b = value[cursor];
                if (b == (byte)'.')
                {
                    if (afterDecimal)
                        return false;
                    afterDecimal = true;
                    continue;
                }

                if (b < (byte)'0' || b > (byte)'9')
                    return false;

                sawDigit = true;
                int digit = b - (byte)'0';
                if (afterDecimal)
                {
                    accumulator += digit * fractionScale;
                    fractionScale *= 0.1d;
                }
                else
                {
                    accumulator = accumulator * 10d + digit;
                }
            }

            if (!sawDigit)
                return false;

            result = (float)(negative ? -accumulator : accumulator);
            return math.isfinite(result);
        }

        public static uint HashLowerAscii(ReadOnlySpan<byte> value)
        {
            uint hash = InventoryRoutingNetwork.FnvOffset;
            for (int i = 0; i < value.Length; i++)
            {
                byte b = value[i];
                if (b >= (byte)'A' && b <= (byte)'Z')
                    b = (byte)(b + 32);
                hash ^= b;
                hash *= InventoryRoutingNetwork.FnvPrime;
            }

            return hash == 0u ? 1u : hash;
        }

        private static byte ToLowerAscii(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z' ? (byte)(value + 32) : value;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct InitializeSoaActiveCountJob : IJob
        {
            [NoAlias] public NativeArray<int> ActiveSlotCount;
            public int ActiveCount;

            public void Execute()
            {
                if (ActiveSlotCount.IsCreated && ActiveSlotCount.Length > 0)
                    ActiveSlotCount[0] = math.max(0, ActiveCount);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        public struct GenerateMockSoaInventoryJob : IJobParallelFor
        {
            [NoAlias] public NativeArray<uint> ItemHashIDs;
            [NoAlias] public NativeArray<uint> Quantities;
            [NoAlias] public NativeArray<float> Durabilities;
            public uint Seed;

            public void Execute(int index)
            {
                uint state = Hash32((uint)index ^ Seed);
                ItemHashIDs[index] = 0x80000000u | ((state & 0x0000FFFFu) + 1u);
                Quantities[index] = 1u + ((state >> 17) & 0x3Fu);
                if (Durabilities.IsCreated && index < Durabilities.Length)
                    Durabilities[index] = (Hash32(state + 0x9E3779B9u) % 1001u) * 0.001f;
            }

            private static uint Hash32(uint value)
            {
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return value;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        public struct QueryInventoryHashJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<uint> ItemHashIDs;
            [ReadOnly, NoAlias] public NativeArray<uint> Quantities;
            [ReadOnly, NoAlias] public NativeArray<int> ActiveSlotCount;
            [NoAlias] public NativeArray<InventorySoaQueryResultDTO> Results;
            public uint TargetHashID;
            public int ResultIndex;
            public int SlotStart;
            public int RequestedSlotCount;

            public void Execute()
            {
                if (!Results.IsCreated || (uint)ResultIndex >= (uint)Results.Length)
                    return;

                int active = ResolveActiveSlotCount(ItemHashIDs, ActiveSlotCount);
                int availableFromStart = active - math.clamp(SlotStart, 0, active);
                int requested = RequestedSlotCount > 0
                    ? math.min(RequestedSlotCount, availableFromStart)
                    : availableFromStart;
                ScanHashQuantity(
                    ItemHashIDs,
                    Quantities,
                    SlotStart,
                    requested,
                    TargetHashID,
                    out int firstIndex,
                    out uint quantityTotal,
                    out uint matchCount,
                    out uint flags);

                InventorySoaQueryResultDTO result = default;
                result.TargetHashID = TargetHashID;
                result.FirstIndex = firstIndex;
                result.QuantityTotal = quantityTotal;
                result.MatchCount = matchCount;
                result.ActiveSlotCount = active;
                result.Flags = flags;
                Results[ResultIndex] = result;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        public struct QueryInventoryHashBatchJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<uint> ItemHashIDs;
            [ReadOnly, NoAlias] public NativeArray<uint> Quantities;
            [ReadOnly, NoAlias] public NativeArray<int> ActiveSlotCount;
            [ReadOnly, NoAlias] public NativeArray<uint> TargetHashIDs;
            [NoAlias] public NativeArray<InventorySoaQueryResultDTO> Results;
            public int QueryCount;

            public void Execute(int index)
            {
                if ((uint)index >= (uint)QueryCount || (uint)index >= (uint)Results.Length)
                    return;

                uint targetHash = TargetHashIDs[index];
                int active = ResolveActiveSlotCount(ItemHashIDs, ActiveSlotCount);
                ScanHashQuantity(
                    ItemHashIDs,
                    Quantities,
                    0,
                    active,
                    targetHash,
                    out int firstIndex,
                    out uint quantityTotal,
                    out uint matchCount,
                    out uint flags);

                InventorySoaQueryResultDTO result = default;
                result.TargetHashID = targetHash;
                result.FirstIndex = firstIndex;
                result.QuantityTotal = quantityTotal;
                result.MatchCount = matchCount;
                result.ActiveSlotCount = active;
                result.Flags = flags;
                Results[index] = result;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        public struct MutateInventoryQuantityJob : IJob
        {
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<uint> ItemHashIDs;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<uint> Quantities;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<float> Durabilities;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> ActiveSlotCount;
            [NoAlias] public NativeArray<InventorySoaMutationResultDTO> Result;
            public uint TargetHashID;
            public int QuantityDelta;
            public uint InsertWhenMissing;
            public uint RemoveWhenZero;
            public float InitialDurability01;

            public void Execute()
            {
                InventorySoaMutationResultDTO mutation = default;
                mutation.TargetHashID = TargetHashID;
                mutation.SlotIndex = -1;
                if (!ItemHashIDs.IsCreated ||
                    !Quantities.IsCreated ||
                    !ActiveSlotCount.IsCreated ||
                    ActiveSlotCount.Length == 0 ||
                    TargetHashID == 0u ||
                    ItemHashIDs.Length != Quantities.Length)
                {
                    mutation.Flags = ResultInvalidInput;
                    WriteResult(mutation);
                    return;
                }

                int activeBefore = AtomicReadActiveCount(ActiveSlotCount, ItemHashIDs.Length);
                ScanHashQuantity(
                    ItemHashIDs,
                    Quantities,
                    0,
                    activeBefore,
                    TargetHashID,
                    out int index,
                    out _,
                    out _,
                    out uint queryFlags);

                mutation.Flags = queryFlags & (ResultAvx2 | ResultSse2 | ResultNeon | ResultQuantityUIntView);
                mutation.ActiveBefore = activeBefore;
                if (index < 0)
                {
                    if (QuantityDelta <= 0 || InsertWhenMissing == 0u)
                    {
                        mutation.Status = 1u;
                        WriteResult(mutation);
                        return;
                    }

                    index = AtomicReserveSlot(ActiveSlotCount, ItemHashIDs.Length);
                    if (index < 0)
                    {
                        mutation.Status = 2u;
                        mutation.Flags |= ResultInvalidInput;
                        WriteResult(mutation);
                        return;
                    }

                    ItemHashIDs[index] = TargetHashID;
                    Quantities[index] = 0u;
                    if (Durabilities.IsCreated && index < Durabilities.Length)
                        Durabilities[index] = math.saturate(InitialDurability01);
                    mutation.Flags |= ResultInserted;
                }

                uint next = AtomicApplyQuantityDelta(Quantities, index, QuantityDelta, out uint previous, out uint conflict);
                mutation.SlotIndex = index;
                mutation.PreviousQuantity = previous;
                mutation.NewQuantity = next;
                mutation.Flags |= conflict | ResultFound;
                if (next == 0u && RemoveWhenZero != 0u)
                {
                    SwapAndPop(ref mutation, index);
                }

                mutation.ActiveAfter = AtomicReadActiveCount(ActiveSlotCount, ItemHashIDs.Length);
                WriteResult(mutation);
            }

            private void SwapAndPop(ref InventorySoaMutationResultDTO mutation, int index)
            {
                int newCount = AtomicDecrementActiveCount(ActiveSlotCount);
                int lastIndex = newCount;
                if ((uint)lastIndex >= (uint)ItemHashIDs.Length || index < 0)
                {
                    mutation.Flags |= ResultInvalidInput;
                    return;
                }

                if (index != lastIndex)
                {
                    ItemHashIDs[index] = ItemHashIDs[lastIndex];
                    Quantities[index] = Quantities[lastIndex];
                    if (Durabilities.IsCreated && index < Durabilities.Length && lastIndex < Durabilities.Length)
                        Durabilities[index] = Durabilities[lastIndex];
                    mutation.Flags |= ResultSwapPop;
                }

                ItemHashIDs[lastIndex] = 0u;
                Quantities[lastIndex] = 0u;
                if (Durabilities.IsCreated && lastIndex < Durabilities.Length)
                    Durabilities[lastIndex] = 0f;
                mutation.Flags |= ResultRemoved;
            }

            private void WriteResult(in InventorySoaMutationResultDTO mutation)
            {
                if (Result.IsCreated && Result.Length > 0)
                    Result[0] = mutation;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        public struct SwapAndPopDefragmentJob : IJob
        {
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<uint> ItemHashIDs;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<uint> Quantities;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<float> Durabilities;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> ActiveSlotCount;
            [NoAlias] public NativeArray<InventorySoaMutationResultDTO> Result;
            public int RemoveIndex;

            public void Execute()
            {
                InventorySoaMutationResultDTO result = default;
                result.SlotIndex = RemoveIndex;
                if (!ItemHashIDs.IsCreated ||
                    !Quantities.IsCreated ||
                    !ActiveSlotCount.IsCreated ||
                    ActiveSlotCount.Length == 0 ||
                    ItemHashIDs.Length != Quantities.Length)
                {
                    result.Flags = ResultInvalidInput;
                    Write(result);
                    return;
                }

                int activeBefore = AtomicReadActiveCount(ActiveSlotCount, ItemHashIDs.Length);
                result.ActiveBefore = activeBefore;
                if ((uint)RemoveIndex >= (uint)activeBefore)
                {
                    result.Flags = ResultInvalidInput;
                    Write(result);
                    return;
                }

                int lastIndex = AtomicDecrementActiveCount(ActiveSlotCount);
                result.TargetHashID = ItemHashIDs[RemoveIndex];
                result.PreviousQuantity = Quantities[RemoveIndex];
                if (RemoveIndex != lastIndex)
                {
                    ItemHashIDs[RemoveIndex] = ItemHashIDs[lastIndex];
                    Quantities[RemoveIndex] = Quantities[lastIndex];
                    if (Durabilities.IsCreated && RemoveIndex < Durabilities.Length && lastIndex < Durabilities.Length)
                        Durabilities[RemoveIndex] = Durabilities[lastIndex];
                    result.Flags |= ResultSwapPop;
                }

                ItemHashIDs[lastIndex] = 0u;
                Quantities[lastIndex] = 0u;
                if (Durabilities.IsCreated && lastIndex < Durabilities.Length)
                    Durabilities[lastIndex] = 0f;

                result.ActiveAfter = AtomicReadActiveCount(ActiveSlotCount, ItemHashIDs.Length);
                result.Flags |= ResultRemoved;
                Write(result);
            }

            private void Write(in InventorySoaMutationResultDTO result)
            {
                if (Result.IsCreated && Result.Length > 0)
                    Result[0] = result;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        public struct RecordSoaInventoryTelemetryJob : IJob
        {
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<InventorySoaTelemetryEntry> TelemetryRing;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> TelemetryCursor;
            public InventorySoaTelemetryEntry Entry;

            public void Execute()
            {
                if (!TelemetryRing.IsCreated || TelemetryRing.Length == 0)
                    return;

                int cursor = 0;
                if (TelemetryCursor.IsCreated && TelemetryCursor.Length > 0)
                {
                    int* cursorPtr = (int*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(TelemetryCursor);
                    cursor = Interlocked.Increment(ref UnsafeUtility.AsRef<int>(cursorPtr)) - 1;
                }

                int slot = math.abs(cursor % TelemetryRing.Length);
                TelemetryRing[slot] = Entry;
            }
        }
    }
}
