namespace Hecton8.Inventory
{
    using System.Threading;
    using Hecton8.Core;
    using Hecton8.Core.Contracts.Signals;
    using Hecton8.Core.Memory;
    using Hecton8.World;
    using Unity.Collections;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine;

    public struct SoaInventoryQueryXRaySnapshot
    {
        public uint Frame;
        public uint TargetHashID;
        public int FirstIndex;
        public uint QuantityTotal;
        public uint MatchCount;
        public int ActiveSlotCount;
        public int Capacity;
        public int TelemetryCursor;
        public int VaultSlotCapacity;
        public float EstimatedMicroseconds;
        public float GlobalQualityWeight;
        public uint Flags;
        public ulong InventoryMask;
    }

    public sealed partial class PlayerInventory
    {

        private static int s_x001DirectSignalPushDropCount_PlayerInventory_SoaQuery;

        private InventorySoaVaultHandles _soaQueryVaultHandles;
        private int _soaQueryVaultSlotCapacity;
        private int _soaQueryDumped;
        private int _soaQueryRequestsThisFrame;
        private int _soaMutationRequestsThisFrame;
        private int _soaSwapPopOpsThisFrame;
#if UNITY_EDITOR
        private int _soaDebugMutationHashBits;
        private int _soaDebugMutationDelta;
        private int _soaDebugMutationPending;
#endif

        private void InitializeSoaQueryEngine(int slotCapacity)
        {
            TryBindSoaQueryVault(_cachedDataVault, slotCapacity);
            PublishSoaQueryVaultSnapshotOwnerPhase();
        }

        private void DisposeSoaQueryEngine()
        {
            _soaQueryVaultHandles = default;
            _soaQueryVaultSlotCapacity = 0;
            _soaQueryDumped = 0;
            _soaQueryRequestsThisFrame = 0;
            _soaMutationRequestsThisFrame = 0;
            _soaSwapPopOpsThisFrame = 0;
#if UNITY_EDITOR
            _soaDebugMutationHashBits = 0;
            _soaDebugMutationDelta = 0;
            _soaDebugMutationPending = 0;
#endif
        }

        private void TryBindSoaQueryVault(IDataVault vault, int slotCapacity)
        {
            if (vault == null ||
                slotCapacity <= 0 ||
                !SoaInventoryQueryEngine.RuntimeLayoutValid())
            {
                _soaQueryVaultHandles = default;
                _soaQueryVaultSlotCapacity = 0;
                return;
            }

            int safeSlotCapacity = math.max(SoaInventoryQueryEngine.DefaultSlotCapacity, slotCapacity);
            _soaQueryVaultHandles = SoaInventoryQueryEngine.EnsureVaultBuffers(vault, safeSlotCapacity);
            VaultGenerationHandle<uint> itemHashLane = _soaQueryVaultHandles.ItemHashIDs.ToHandle<uint>();
            _soaQueryVaultSlotCapacity = itemHashLane.Generation != 0u ? safeSlotCapacity : 0;
        }

        public JobHandle ScheduleSoaInventoryQuery(
            NativeArray<uint> itemHashIds,
            NativeArray<int> quantities,
            NativeArray<int> activeSlotCount,
            NativeArray<InventorySoaQueryResultDTO> results,
            uint targetHashId,
            int resultIndex,
            JobHandle dependency = default)
        {
            return ScheduleSoaInventoryQuery(
                itemHashIds,
                SoaInventoryQueryEngine.AsUIntQuantityOwnerAlias(quantities),
                activeSlotCount,
                results,
                targetHashId,
                resultIndex,
                dependency);
        }

        public JobHandle ScheduleSoaInventoryQuery(
            NativeArray<uint> itemHashIds,
            NativeArray<uint> quantities,
            NativeArray<int> activeSlotCount,
            NativeArray<InventorySoaQueryResultDTO> results,
            uint targetHashId,
            int resultIndex,
            JobHandle dependency = default)
        {
            if (targetHashId != 0u &&
                itemHashIds.IsCreated &&
                quantities.IsCreated &&
                results.IsCreated)
            {
                AddSoaTelemetryCounter(ref _soaQueryRequestsThisFrame, 1);
            }

            return SoaInventoryQueryEngine.ScheduleQuery(
                itemHashIds,
                quantities,
                activeSlotCount,
                results,
                targetHashId,
                resultIndex,
                0,
                0,
                dependency);
        }

        public JobHandle ScheduleSoaInventoryQueryBatch(
            NativeArray<uint> itemHashIds,
            NativeArray<int> quantities,
            NativeArray<int> activeSlotCount,
            NativeArray<uint> targetHashIds,
            NativeArray<InventorySoaQueryResultDTO> results,
            float globalQualityWeight,
            JobHandle dependency = default)
        {
            return ScheduleSoaInventoryQueryBatch(
                itemHashIds,
                SoaInventoryQueryEngine.AsUIntQuantityOwnerAlias(quantities),
                activeSlotCount,
                targetHashIds,
                results,
                globalQualityWeight,
                dependency);
        }

        public JobHandle ScheduleSoaInventoryQueryBatch(
            NativeArray<uint> itemHashIds,
            NativeArray<uint> quantities,
            NativeArray<int> activeSlotCount,
            NativeArray<uint> targetHashIds,
            NativeArray<InventorySoaQueryResultDTO> results,
            float globalQualityWeight,
            JobHandle dependency = default)
        {
            if (itemHashIds.IsCreated &&
                quantities.IsCreated &&
                targetHashIds.IsCreated &&
                results.IsCreated)
            {
                int queryCount = math.min(targetHashIds.Length, results.Length);
                int admitted = SoaInventoryQueryEngine.ResolveQueryBatchSize(globalQualityWeight, queryCount);
                if (admitted > 0)
                    AddSoaTelemetryCounter(ref _soaQueryRequestsThisFrame, admitted);
            }

            return SoaInventoryQueryEngine.ScheduleQueryBatch(
                itemHashIds,
                quantities,
                activeSlotCount,
                targetHashIds,
                results,
                globalQualityWeight,
                dependency);
        }

        public JobHandle ScheduleSoaInventoryMutation(
            NativeArray<uint> itemHashIds,
            NativeArray<int> quantities,
            NativeArray<float> durabilities,
            NativeArray<int> activeSlotCount,
            NativeArray<InventorySoaMutationResultDTO> result,
            uint targetHashId,
            int quantityDelta,
            JobHandle dependency = default)
        {
            return ScheduleSoaInventoryMutation(
                itemHashIds,
                SoaInventoryQueryEngine.AsUIntQuantityOwnerAlias(quantities),
                durabilities,
                activeSlotCount,
                result,
                targetHashId,
                quantityDelta,
                dependency);
        }

        public JobHandle ScheduleSoaInventoryMutation(
            NativeArray<uint> itemHashIds,
            NativeArray<uint> quantities,
            NativeArray<float> durabilities,
            NativeArray<int> activeSlotCount,
            NativeArray<InventorySoaMutationResultDTO> result,
            uint targetHashId,
            int quantityDelta,
            JobHandle dependency = default)
        {
            if (targetHashId != 0u &&
                quantityDelta != 0 &&
                itemHashIds.IsCreated &&
                quantities.IsCreated &&
                activeSlotCount.IsCreated &&
                result.IsCreated)
            {
                AddSoaTelemetryCounter(ref _soaMutationRequestsThisFrame, 1);
            }

            return SoaInventoryQueryEngine.ScheduleMutation(
                itemHashIds,
                quantities,
                durabilities,
                activeSlotCount,
                result,
                targetHashId,
                quantityDelta,
                insertWhenMissing: 1u,
                removeWhenZero: 1u,
                initialDurability01: 1f,
                dependency);
        }

        public bool TryDropOneItemToWorldSignalAup(
            int anchorX,
            int anchorY,
            double3 sourceAup,
            float3 localDropOffsetMeters,
            float3 initialImpulse,
            Transform interactor,
            out int droppedHashId)
        {
            droppedHashId = 0;
            if (!SoaInventoryQueryEngine.TryResolveDropRuntimePosition(
                    sourceAup,
                    localDropOffsetMeters,
                    HectonFloatingOrigin.CurrentTotalOffsetDouble,
                    out float3 runtimeDropPosition))
            {
                return false;
            }

            return TryDropOneItemToWorldSignal(
                anchorX,
                anchorY,
                new Vector3(runtimeDropPosition.x, runtimeDropPosition.y, runtimeDropPosition.z),
                new Vector3(initialImpulse.x, initialImpulse.y, initialImpulse.z),
                interactor,
                out droppedHashId);
        }

        private bool TryDropOneItemToDeathLootCacheSignal(
            int anchorX,
            int anchorY,
            double3 sourceAup,
            float3 localDropOffsetMeters,
            in InventoryCommandSignal command,
            out int droppedHashId)
        {
            droppedHashId = 0;
            if (!math.all(math.isfinite(sourceAup)) ||
                !math.all(math.isfinite(localDropOffsetMeters)))
            {
                return false;
            }

            AbsoluteUniversePosition dropAup = AbsoluteUniversePosition.FromAbsolutePosition(
                sourceAup + new double3(localDropOffsetMeters.x, localDropOffsetMeters.y, localDropOffsetMeters.z));
            if (!dropAup.IsFinite())
                return false;

            if (!TryRemoveOneItemWithState(
                    anchorX,
                    anchorY,
                    out int itemHashId,
                    out ushort stateFlags,
                    out ulong geneticsMask,
                    out ushort qualityMilli))
            {
                return false;
            }

            InventoryDeathLootCacheSignal signal = default;
            signal.PositionAup = dropAup;
            signal.GeneticsMask = geneticsMask;
            signal.InventoryHash = command.InventoryHash;
            signal.ItemHash = unchecked((uint)itemHashId);
            signal.Sequence = command.Sequence;
            signal.Frame = command.Frame;
            signal.Quantity = 1;
            signal.QualityMilli = qualityMilli > 0 ? qualityMilli : DefaultQualityMilli;
            signal.Flags = 1u;
            signal.StateFlags = stateFlags;
            if (!SignalBus<InventoryDeathLootCacheSignal>.TryPushTracked(in signal, ref s_x001DirectSignalPushDropCount_PlayerInventory_SoaQuery))
            {
                TryAddItemWithState(itemHashId, new ItemState(geneticsMask, qualityMilli, stateFlags));
                return false;
            }

            droppedHashId = itemHashId;
            return true;
        }

        public bool TryReadLatestSoaQueryTelemetry(out InventorySoaTelemetryEntry entry)
        {
            entry = default;
            if (!TryReadSoaQueryVaultBuffers(out InventorySoaVaultBuffers buffers) ||
                !buffers.TelemetryRing.IsCreated ||
                !buffers.TelemetryCursor.IsCreated ||
                buffers.TelemetryRing.Length == 0)
            {
                return false;
            }

            int cursor = buffers.TelemetryCursor[0];
            if (cursor <= 0)
                return false;

            int slot = (cursor - 1) % buffers.TelemetryRing.Length;
            entry = buffers.TelemetryRing[slot];
            return true;
        }

        public bool TryReadSoaInventoryXRay(out SoaInventoryQueryXRaySnapshot snapshot)
        {
            snapshot = default;
            if (!TryReadSoaQueryVaultBuffers(out InventorySoaVaultBuffers buffers) ||
                !TryReadLatestSoaQueryTelemetry(out InventorySoaTelemetryEntry entry))
            {
                return false;
            }

            snapshot = new SoaInventoryQueryXRaySnapshot
            {
                Frame = entry.Frame,
                TargetHashID = entry.TargetHashID,
                FirstIndex = entry.FirstIndex,
                QuantityTotal = entry.QuantityTotal,
                MatchCount = entry.MatchCount,
                ActiveSlotCount = entry.ActiveSlotCount,
                Capacity = entry.Capacity,
                TelemetryCursor = buffers.TelemetryCursor.IsCreated ? buffers.TelemetryCursor[0] : 0,
                VaultSlotCapacity = _soaQueryVaultSlotCapacity,
                EstimatedMicroseconds = entry.EstimatedMicroseconds,
                GlobalQualityWeight = entry.GlobalQualityWeight,
                Flags = entry.Flags,
                InventoryMask = entry.Reserved0
            };
            return true;
        }

        public bool TryReadFastFailInventorySoA(
            out NativeArray<uint>.ReadOnly itemHashIds,
            out NativeArray<uint>.ReadOnly quantities,
            out int activeSlotCount,
            out ulong currentInventoryMask)
        {
            itemHashIds = default;
            quantities = default;
            activeSlotCount = 0;
            currentInventoryMask = CurrentInventoryMask;
            if (_cachedDataVault == null || _soaQueryVaultSlotCapacity <= 0)
                return false;

            if (!SoaInventoryQueryEngine.TryReadVaultBuffers(_cachedDataVault, in _soaQueryVaultHandles, out InventorySoaVaultBuffers buffers) ||
                !buffers.ItemHashIDs.IsCreated ||
                !buffers.Quantities.IsCreated ||
                !buffers.ActiveSlotCount.IsCreated ||
                buffers.ActiveSlotCount.Length == 0)
            {
                return false;
            }

            NativeArray<uint> quantityView = SoaInventoryQueryEngine.AsUIntQuantityOwnerAlias(buffers.Quantities);
            if (!quantityView.IsCreated)
                return false;

            int capacity = math.min(buffers.ItemHashIDs.Length, quantityView.Length);
            int active = math.clamp(buffers.ActiveSlotCount[0], 0, capacity);

            itemHashIds = buffers.ItemHashIDs.AsReadOnly();
            quantities = quantityView.AsReadOnly();
            activeSlotCount = active;
            return true;
        }

        public bool TryDumpSoaQueryTelemetry()
        {
            if (!TryResolveSoaQueryVaultBuffers(out InventorySoaVaultBuffers buffers))
                return false;

            int cursor = buffers.TelemetryCursor.IsCreated && buffers.TelemetryCursor.Length > 0
                ? buffers.TelemetryCursor[0]
                : 0;
            return SoaInventoryQueryEngine.TryDumpTelemetry(buffers.TelemetryRing, cursor);
        }

        private bool TryResolveSoaQueryVaultBuffers(out InventorySoaVaultBuffers buffers)
        {
            buffers = default;
            return _cachedDataVault != null &&
                   _soaQueryVaultSlotCapacity > 0 &&
                   SoaInventoryQueryEngine.TryResolveVaultBuffers(_cachedDataVault, ref _soaQueryVaultHandles, out buffers);
        }

        private bool TryReadSoaQueryVaultBuffers(out InventorySoaVaultBuffers buffers)
        {
            buffers = default;
            return _cachedDataVault != null &&
                   _soaQueryVaultSlotCapacity > 0 &&
                   SoaInventoryQueryEngine.TryReadVaultBuffers(_cachedDataVault, in _soaQueryVaultHandles, out buffers);
        }

        private void PublishSoaQueryVaultSnapshotOwnerPhase()
        {
            // L19 hop2: resolve vault lanes before bulk read — same stale-_basePtr hazard as
            // TryCountQuantityByHashSoa after world-load vault rebirth.
            if (!TryResolveSoaQueryVaultBuffers(out InventorySoaVaultBuffers buffers) ||
                !_itemHashes.TryResolve(out NativeArray<uint> itemHashes) ||
                !_stackCounts.TryResolve(out NativeArray<ushort> stackCounts) ||
                !_itemDurability.TryResolve(out NativeArray<float> itemDurability))
            {
                return;
            }

            int sourceCount = math.min(itemHashes.Length, math.min(stackCounts.Length, itemDurability.Length));
            int capacity = math.min(sourceCount, math.min(buffers.ItemHashIDs.Length, math.min(buffers.Quantities.Length, buffers.Durabilities.Length)));
            int active = 0;
            for (int anchorIndex = 0; anchorIndex < sourceCount && active < capacity; anchorIndex++)
            {
                if (_grid == null || !_grid.HasAnchor(anchorIndex))
                    continue;

                uint itemHash = itemHashes[anchorIndex];
                if (itemHash == 0u)
                    continue;

                int availableQuantity = math.max(0, math.max(1, (int)stackCounts[anchorIndex]) - GetReservedCraftCount(anchorIndex));
                if (availableQuantity <= 0)
                    continue;

                buffers.ItemHashIDs[active] = itemHash;
                buffers.Quantities[active] = availableQuantity;
                buffers.Durabilities[active] = math.saturate(itemDurability[anchorIndex]);
                active++;
            }

            if (buffers.ActiveSlotCount.IsCreated && buffers.ActiveSlotCount.Length > 0)
                buffers.ActiveSlotCount[0] = active;
        }


        private bool TryCountQuantityByHashSoa(int itemHashId, bool availableOnly, out int total)
        {
            total = 0;
            // L19 hop2 LIVE: ACCESS_VIOLATION in EqualMask4 / NativeArray`1.get_Item during
            // EnsureToolGranted -> CountQuantityByHash. InventoryVaultLane implicit conversion
            // and indexer read _basePtr without TryRefreshHandle. After vault rebirth/relocate
            // (world load) that pointer is dangling → native Crash!!!.
            // Always resolve via TryResolve so generation is refreshed before SIMD/scalar reads.
            if (!_itemHashes.TryResolve(out NativeArray<uint> itemHashes) ||
                !_stackCounts.TryResolve(out NativeArray<ushort> stackCounts) ||
                itemHashes.Length == 0 ||
                itemHashes.Length != stackCounts.Length)
            {
                return false;
            }

            uint targetHashId = unchecked((uint)itemHashId);
            int capacity = itemHashes.Length;
            int vectorEnd = capacity & ~3;
            bool found = false;
            for (int i = 0; i < vectorEnd; i += 4)
            {
                int mask = SoaInventoryQueryEngine.EqualMask4(itemHashes, i, targetHashId);
                while (mask != 0)
                {
                    int lane = math.tzcnt(mask);
                    int anchorIndex = i + lane;
                    AccumulateSoaStack(itemHashes, stackCounts, anchorIndex, availableOnly, ref total, ref found);
                    mask &= mask - 1;
                }
            }

            for (int anchorIndex = vectorEnd; anchorIndex < capacity; anchorIndex++)
            {
                if (itemHashes[anchorIndex] == targetHashId)
                    AccumulateSoaStack(itemHashes, stackCounts, anchorIndex, availableOnly, ref total, ref found);
            }

            return found;
        }

        private void AccumulateSoaStack(
            NativeArray<uint> itemHashes,
            NativeArray<ushort> stackCounts,
            int anchorIndex,
            bool availableOnly,
            ref int total,
            ref bool found)
        {
            if (_grid != null && !_grid.HasAnchor(anchorIndex))
                return;

            // Use resolved NativeArrays — never re-enter vault lane indexers (stale _basePtr).
            if ((uint)anchorIndex >= (uint)stackCounts.Length)
                return;

            int count = math.max(1, (int)stackCounts[anchorIndex]);
            if (availableOnly)
                count = math.max(0, count - GetReservedCraftCount(anchorIndex));

            if (count <= 0)
                return;

            found = true;
            total = total > int.MaxValue - count ? int.MaxValue : total + count;
        }


        private void WriteSoaQueryTelemetryOwnerPhase()
        {
            if (!TryResolveSoaQueryVaultBuffers(out InventorySoaVaultBuffers buffers))
                return;

            float quality = InventoryRoutingNetwork.ResolveCurrentGlobalQualityWeight();
#if UNITY_EDITOR
            bool hasDebugMutation = TryApplyPendingSoaXRayMutationOwnerPhase(buffers, out InventorySoaMutationResultDTO debugMutation);
#else
            bool hasDebugMutation = false;
            InventorySoaMutationResultDTO debugMutation = default;
#endif
            int capacity = buffers.ItemHashIDs.IsCreated ? buffers.ItemHashIDs.Length : 0;
            int active = buffers.ActiveSlotCount.IsCreated && buffers.ActiveSlotCount.Length > 0
                ? math.clamp(buffers.ActiveSlotCount[0], 0, capacity)
                : 0;
            int queryRequests = Interlocked.Exchange(ref _soaQueryRequestsThisFrame, 0);
            int mutationRequests = Interlocked.Exchange(ref _soaMutationRequestsThisFrame, 0);
            int swapPopOps = Interlocked.Exchange(ref _soaSwapPopOpsThisFrame, 0);
            uint flags = SoaInventoryQueryEngine.ResultOwnerPhaseFrame;
            if (!SoaInventoryQueryEngine.RuntimeLayoutValid() || !math.isfinite(quality))
                flags |= SoaInventoryQueryEngine.ResultNaNFault;

            InventorySoaTelemetryEntry entry = new InventorySoaTelemetryEntry
            {
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                TargetHashID = hasDebugMutation ? debugMutation.TargetHashID : 0u,
                FirstIndex = hasDebugMutation ? debugMutation.SlotIndex : -1,
                QuantityTotal = hasDebugMutation ? debugMutation.NewQuantity : (uint)math.max(0, active),
                MatchCount = (uint)math.max(0, queryRequests),
                ActiveSlotCount = active,
                Capacity = capacity,
                EstimatedMicroseconds = SoaInventoryQueryEngine.EstimateFrameQueryMicroseconds(active, queryRequests, quality),
                GlobalQualityWeight = quality,
                Flags = flags | (hasDebugMutation ? debugMutation.Flags : 0u),
                MutationIndex = swapPopOps,
                MutationDelta = mutationRequests,
                LayoutHash = SoaInventoryQueryEngine.LayoutHash,
                Reserved0 = CurrentInventoryMask
            };

            WriteSoaTelemetryEntry(buffers, in entry);
            if (((flags & SoaInventoryQueryEngine.ResultNaNFault) != 0u || entry.EstimatedMicroseconds > 200f) && _soaQueryDumped == 0)
                _soaQueryDumped = TryDumpSoaQueryTelemetry() ? 1 : 0;
        }

        private static void WriteSoaTelemetryEntry(InventorySoaVaultBuffers buffers, in InventorySoaTelemetryEntry entry)
        {
            if (!buffers.TelemetryRing.IsCreated ||
                !buffers.TelemetryCursor.IsCreated ||
                buffers.TelemetryRing.Length == 0 ||
                buffers.TelemetryCursor.Length == 0)
            {
                return;
            }

            int cursor = buffers.TelemetryCursor[0];
            int slot = cursor % buffers.TelemetryRing.Length;
            buffers.TelemetryRing[slot] = entry;
            buffers.TelemetryCursor[0] = cursor == int.MaxValue ? 0 : cursor + 1;
        }

#if UNITY_EDITOR
        public bool TryInjectSoaVaultItemForXRay(uint targetHashId, int quantityDelta)
        {
            if (!Application.isPlaying || targetHashId == 0u || quantityDelta == 0 || _soaQueryVaultSlotCapacity <= 0)
                return false;

            Interlocked.Exchange(ref _soaDebugMutationHashBits, unchecked((int)targetHashId));
            Interlocked.Exchange(ref _soaDebugMutationDelta, quantityDelta);
            Interlocked.Exchange(ref _soaDebugMutationPending, 1);
            return true;
        }

        private bool TryApplyPendingSoaXRayMutationOwnerPhase(
            InventorySoaVaultBuffers buffers,
            out InventorySoaMutationResultDTO mutation)
        {
            mutation = default;
            if (Interlocked.Exchange(ref _soaDebugMutationPending, 0) == 0)
                return false;

            uint targetHashId = unchecked((uint)Interlocked.CompareExchange(ref _soaDebugMutationHashBits, 0, 0));
            int quantityDelta = Interlocked.CompareExchange(ref _soaDebugMutationDelta, 0, 0);
            if (targetHashId == 0u || quantityDelta == 0)
                return false;

            NativeArray<uint> quantityView = SoaInventoryQueryEngine.AsUIntQuantityOwnerAlias(buffers.Quantities);
            if (!quantityView.IsCreated)
                return false;

            bool accepted = SoaInventoryQueryEngine.TryApplyMutationOwnerPhase(
                buffers.ItemHashIDs,
                quantityView,
                buffers.Durabilities,
                buffers.ActiveSlotCount,
                targetHashId,
                quantityDelta,
                insertWhenMissing: 1u,
                removeWhenZero: 1u,
                initialDurability01: 1f,
                out mutation);
            if (accepted)
            {
                AddSoaTelemetryCounter(ref _soaMutationRequestsThisFrame, 1);
                if ((mutation.Flags & SoaInventoryQueryEngine.ResultRemoved) != 0u)
                    AddSoaTelemetryCounter(ref _soaSwapPopOpsThisFrame, 1);
            }

            return accepted;
        }
#endif

        private static void AddSoaTelemetryCounter(ref int counter, int delta)
        {
            if (delta <= 0)
                return;

            for (int attempt = 0; attempt < 8; attempt++)
            {
                int observed = Interlocked.CompareExchange(ref counter, 0, 0);
                int next = observed > int.MaxValue - delta ? int.MaxValue : observed + delta;
                if (Interlocked.CompareExchange(ref counter, next, observed) == observed)
                    return;
            }

            Interlocked.Exchange(ref counter, int.MaxValue);
        }
    }
}
