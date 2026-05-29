using System;
using System.Diagnostics;
using System.IO;
#if UNITY_STANDALONE || UNITY_EDITOR
using System.IO.MemoryMappedFiles;
#endif
using System.Runtime.CompilerServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Quest
{
    /// <summary>
    /// Vault-owned storage facade for the bitmask quest DAG.
    /// </summary>
    public static unsafe class QuestDagVault
    {
        private const SystemID VaultOwnerSystem = SystemID.QuestDag;

        /// <summary>
        /// Creates or resolves all persistent buffers required by the quest DAG.
        /// </summary>
        public static QuestDagBufferHandles EnsureBuffers(
            IDataVault vault,
            int nodeCapacity = QuestDagRuntimeConstants.DefaultNodeCapacity,
            int triggerCapacity = QuestDagRuntimeConstants.DefaultTriggerCapacity,
            int stateChunkCount = QuestDagRuntimeConstants.DefaultStateChunkCount,
            int itemLinkCapacity = QuestDagRuntimeConstants.DefaultItemLinkCapacity,
            int playerItemCapacity = QuestDagRuntimeConstants.DefaultPlayerItemCapacity,
            int factionCapacity = QuestDagRuntimeConstants.DefaultFactionCapacity)
        {
            QuestDagBufferHandles handles = default;
            if (vault == null || vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return handles;

            nodeCapacity = math.max(1, nodeCapacity);
            triggerCapacity = math.max(1, triggerCapacity);
            stateChunkCount = math.max(1, stateChunkCount);
            itemLinkCapacity = math.max(1, itemLinkCapacity);
            playerItemCapacity = math.max(1, playerItemCapacity);
            factionCapacity = math.max(1, factionCapacity);

            handles.NodeCapacity = nodeCapacity;
            handles.TriggerCapacity = triggerCapacity;
            handles.StateChunkCount = stateChunkCount;
            handles.ItemLinkCapacity = itemLinkCapacity;
            handles.PlayerItemCapacity = playerItemCapacity;
            handles.FactionCapacity = factionCapacity;

            handles.GlobalStateMasks = vault.EnsureGenerationHandle<ulong>(
                BufferID.QuestDagGlobalStateMasks,
                stateChunkCount,
                VaultOwnerSystem,
                NativeArrayOptions.ClearMemory);
            handles.OldStateMasks = vault.EnsureGenerationHandle<ulong>(
                BufferID.QuestDagOldStateMasks,
                stateChunkCount,
                VaultOwnerSystem,
                NativeArrayOptions.ClearMemory);
            handles.Nodes = vault.EnsureGenerationHandle<QuestNodeDTO>(
                BufferID.QuestDagNodes,
                nodeCapacity,
                VaultOwnerSystem,
                NativeArrayOptions.ClearMemory);
            handles.NodeRuntime = vault.EnsureGenerationHandle<QuestNodeRuntimeDTO>(
                BufferID.QuestDagNodeRuntime,
                nodeCapacity,
                VaultOwnerSystem,
                NativeArrayOptions.ClearMemory);
            handles.TriggerVolumes = vault.EnsureGenerationHandle<TriggerVolumeDTO>(
                BufferID.QuestDagTriggerVolumes,
                triggerCapacity,
                VaultOwnerSystem,
                NativeArrayOptions.ClearMemory);
            handles.RequiredItemHashes = vault.EnsureGenerationHandle<uint>(
                BufferID.QuestDagRequiredItemHashes,
                itemLinkCapacity,
                VaultOwnerSystem,
                NativeArrayOptions.ClearMemory);
            handles.RequiredItemQuantities = vault.EnsureGenerationHandle<int>(
                BufferID.QuestDagRequiredItemQuantities,
                itemLinkCapacity,
                VaultOwnerSystem,
                NativeArrayOptions.ClearMemory);
            handles.PlayerItemHashes = vault.EnsureGenerationHandle<uint>(
                BufferID.QuestDagPlayerItemHashes,
                playerItemCapacity,
                VaultOwnerSystem,
                NativeArrayOptions.ClearMemory);
            handles.PlayerItemQuantities = vault.EnsureGenerationHandle<int>(
                BufferID.QuestDagPlayerItemQuantities,
                playerItemCapacity,
                VaultOwnerSystem,
                NativeArrayOptions.ClearMemory);
            handles.FactionStandings = vault.EnsureGenerationHandle<float>(
                BufferID.QuestDagFactionStandings,
                factionCapacity,
                VaultOwnerSystem,
                NativeArrayOptions.ClearMemory);
            handles.TelemetryRing = vault.EnsureGenerationHandle<QuestDagTelemetryEntry>(
                BufferID.QuestDagTelemetryRing,
                QuestDagRuntimeConstants.TelemetryCapacity,
                VaultOwnerSystem,
                NativeArrayOptions.ClearMemory);
            handles.TelemetryCursor = vault.EnsureGenerationHandle<int>(
                BufferID.QuestDagTelemetryCursor,
                1,
                VaultOwnerSystem,
                NativeArrayOptions.ClearMemory);
            handles.Counters = vault.EnsureGenerationHandle<int>(
                BufferID.QuestDagCounters,
                QuestDagRuntimeConstants.CounterCount,
                VaultOwnerSystem,
                NativeArrayOptions.ClearMemory);
            handles.TriggerNodeIndices = vault.EnsureGenerationHandle<int>(
                BufferID.QuestDagTriggerNodeIndices,
                triggerCapacity,
                VaultOwnerSystem,
                NativeArrayOptions.ClearMemory);
            handles.NoTriggerNodeIndices = vault.EnsureGenerationHandle<int>(
                BufferID.QuestDagNoTriggerNodeIndices,
                nodeCapacity,
                VaultOwnerSystem,
                NativeArrayOptions.ClearMemory);
            handles.CsvMonitor = vault.EnsureGenerationHandle<long>(
                BufferID.QuestDagCsvMonitor,
                2,
                VaultOwnerSystem,
                NativeArrayOptions.ClearMemory);

            if (TryAcquireQuestDagWriteBuffer(
                    vault,
                    in handles.Counters,
                    BufferID.QuestDagCounters,
                    QuestDagRuntimeConstants.CounterCount,
                    out NativeArray<int> counters))
            {
                try
                {
                    counters[(int)QuestDagRuntimeConstants.CounterSlot.StateChunkCount] = stateChunkCount;
                }
                finally
                {
                    vault.ReleaseWriteLock(in handles.Counters, VaultOwnerSystem);
                }
            }

            return handles;
        }

        /// <summary>
        /// Resolves all handles to frame-local NativeArray views.
        /// </summary>
        public static bool TryResolveBuffers(
            IDataVault vault,
            ref QuestDagBufferHandles handles,
            out QuestDagBuffers buffers)
        {
            buffers = default;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (!TryResolveQuestDagBuffer(vault, in handles.GlobalStateMasks, BufferID.QuestDagGlobalStateMasks, handles.StateChunkCount, out buffers.GlobalStateMasks) ||
                !TryResolveQuestDagBuffer(vault, in handles.OldStateMasks, BufferID.QuestDagOldStateMasks, handles.StateChunkCount, out buffers.OldStateMasks) ||
                !TryResolveQuestDagBuffer(vault, in handles.Nodes, BufferID.QuestDagNodes, handles.NodeCapacity, out buffers.Nodes) ||
                !TryResolveQuestDagBuffer(vault, in handles.NodeRuntime, BufferID.QuestDagNodeRuntime, handles.NodeCapacity, out buffers.NodeRuntime) ||
                !TryResolveQuestDagBuffer(vault, in handles.TriggerVolumes, BufferID.QuestDagTriggerVolumes, handles.TriggerCapacity, out buffers.TriggerVolumes) ||
                !TryResolveQuestDagBuffer(vault, in handles.RequiredItemHashes, BufferID.QuestDagRequiredItemHashes, handles.ItemLinkCapacity, out buffers.RequiredItemHashes) ||
                !TryResolveQuestDagBuffer(vault, in handles.RequiredItemQuantities, BufferID.QuestDagRequiredItemQuantities, handles.ItemLinkCapacity, out buffers.RequiredItemQuantities) ||
                !TryResolveQuestDagBuffer(vault, in handles.PlayerItemHashes, BufferID.QuestDagPlayerItemHashes, handles.PlayerItemCapacity, out buffers.PlayerItemHashes) ||
                !TryResolveQuestDagBuffer(vault, in handles.PlayerItemQuantities, BufferID.QuestDagPlayerItemQuantities, handles.PlayerItemCapacity, out buffers.PlayerItemQuantities) ||
                !TryResolveQuestDagBuffer(vault, in handles.FactionStandings, BufferID.QuestDagFactionStandings, handles.FactionCapacity, out buffers.FactionStandings) ||
                !TryResolveQuestDagBuffer(vault, in handles.TelemetryRing, BufferID.QuestDagTelemetryRing, QuestDagRuntimeConstants.TelemetryCapacity, out buffers.TelemetryRing) ||
                !TryResolveQuestDagBuffer(vault, in handles.TelemetryCursor, BufferID.QuestDagTelemetryCursor, 1, out buffers.TelemetryCursor) ||
                !TryResolveQuestDagBuffer(vault, in handles.Counters, BufferID.QuestDagCounters, QuestDagRuntimeConstants.CounterCount, out buffers.Counters) ||
                !TryResolveQuestDagBuffer(vault, in handles.TriggerNodeIndices, BufferID.QuestDagTriggerNodeIndices, handles.TriggerCapacity, out buffers.TriggerNodeIndices) ||
                !TryResolveQuestDagBuffer(vault, in handles.NoTriggerNodeIndices, BufferID.QuestDagNoTriggerNodeIndices, handles.NodeCapacity, out buffers.NoTriggerNodeIndices) ||
                !TryResolveQuestDagBuffer(vault, in handles.CsvMonitor, BufferID.QuestDagCsvMonitor, 2, out buffers.CsvMonitor))
            {
                buffers = default;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Copies a state mask out through the read-only vault route.
        /// </summary>
        public static bool TryReadStateMask(
            IDataVault vault,
            ref QuestDagBufferHandles handles,
            int chunkIndex,
            out ulong mask)
        {
            mask = 0UL;
            if (vault == null ||
                (uint)chunkIndex >= (uint)handles.StateChunkCount ||
                !vault.TryReadOnlyHandle(in handles.GlobalStateMasks, out NativeArray<ulong>.ReadOnly masks) ||
                (uint)chunkIndex >= (uint)masks.Length)
            {
                return false;
            }

            mask = masks[chunkIndex];
            return true;
        }

        /// <summary>
        /// Copies the packed narrative state to a save-owned destination.
        /// </summary>
        public static bool TryCopySaveState(
            IDataVault vault,
            ref QuestDagBufferHandles handles,
            NativeArray<ulong> destination)
        {
            if (!destination.IsCreated ||
                !TryReadQuestDagBuffer(
                    vault,
                    in handles.GlobalStateMasks,
                    BufferID.QuestDagGlobalStateMasks,
                    handles.StateChunkCount,
                    out NativeArray<ulong>.ReadOnly globalStateMasks) ||
                !globalStateMasks.IsCreated)
            {
                return false;
            }

            int count = math.min(destination.Length, globalStateMasks.Length);
            if (count <= 0)
                return false;

            UnsafeUtility.MemCpy(
                destination.GetUnsafePtr(),
                globalStateMasks.GetUnsafeReadOnlyPtr(),
                (long)count * UnsafeUtility.SizeOf<ulong>());
            return true;
        }

        /// <summary>
        /// Releases QuestDag-owned descriptors. Call only after resolver jobs using these views are complete.
        /// </summary>
        public static void ReleaseBuffers(IDataVault vault, ref QuestDagBufferHandles handles)
        {
            ReleaseQuestDagVaultHandle(vault, ref handles.GlobalStateMasks, BufferID.QuestDagGlobalStateMasks);
            ReleaseQuestDagVaultHandle(vault, ref handles.OldStateMasks, BufferID.QuestDagOldStateMasks);
            ReleaseQuestDagVaultHandle(vault, ref handles.Nodes, BufferID.QuestDagNodes);
            ReleaseQuestDagVaultHandle(vault, ref handles.NodeRuntime, BufferID.QuestDagNodeRuntime);
            ReleaseQuestDagVaultHandle(vault, ref handles.TriggerVolumes, BufferID.QuestDagTriggerVolumes);
            ReleaseQuestDagVaultHandle(vault, ref handles.RequiredItemHashes, BufferID.QuestDagRequiredItemHashes);
            ReleaseQuestDagVaultHandle(vault, ref handles.RequiredItemQuantities, BufferID.QuestDagRequiredItemQuantities);
            ReleaseQuestDagVaultHandle(vault, ref handles.PlayerItemHashes, BufferID.QuestDagPlayerItemHashes);
            ReleaseQuestDagVaultHandle(vault, ref handles.PlayerItemQuantities, BufferID.QuestDagPlayerItemQuantities);
            ReleaseQuestDagVaultHandle(vault, ref handles.FactionStandings, BufferID.QuestDagFactionStandings);
            ReleaseQuestDagVaultHandle(vault, ref handles.TelemetryRing, BufferID.QuestDagTelemetryRing);
            ReleaseQuestDagVaultHandle(vault, ref handles.TelemetryCursor, BufferID.QuestDagTelemetryCursor);
            ReleaseQuestDagVaultHandle(vault, ref handles.Counters, BufferID.QuestDagCounters);
            ReleaseQuestDagVaultHandle(vault, ref handles.TriggerNodeIndices, BufferID.QuestDagTriggerNodeIndices);
            ReleaseQuestDagVaultHandle(vault, ref handles.NoTriggerNodeIndices, BufferID.QuestDagNoTriggerNodeIndices);
            ReleaseQuestDagVaultHandle(vault, ref handles.CsvMonitor, BufferID.QuestDagCsvMonitor);
            handles = default;
        }

        private static bool TryResolveQuestDagBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null || vault.IsCompactionFenceActive || requiredLength <= 0)
                return false;

            if (!IsQuestDagVaultHandle(in handle, bufferId) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        internal static bool TryAcquireQuestDagWriteBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null || vault.IsCompactionFenceActive || requiredLength <= 0)
                return false;

            if (!IsQuestDagVaultHandle(in handle, bufferId) ||
                !vault.TryAcquireWriteLock(in handle, VaultOwnerSystem, out buffer))
            {
                buffer = default;
                return false;
            }

            if (buffer.IsCreated && buffer.Length >= requiredLength)
                return true;

            vault.ReleaseWriteLock(in handle, VaultOwnerSystem);
            buffer = default;
            return false;
        }

        internal static bool TryReadQuestDagBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            if (vault == null || vault.IsCompactionFenceActive || requiredLength <= 0)
                return false;

            if (!IsQuestDagVaultHandle(in handle, bufferId) ||
                !vault.TryReadOnlyHandle(in handle, out buffer) ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        internal static bool TryWriteQuestDagValue<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            int index,
            T value)
            where T : struct
        {
            if ((uint)index >= (uint)requiredLength ||
                !TryAcquireQuestDagWriteBuffer(vault, in handle, bufferId, requiredLength, out NativeArray<T> values))
            {
                return false;
            }

            try
            {
                if ((uint)index >= (uint)values.Length)
                    return false;

                values[index] = value;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, VaultOwnerSystem);
            }
        }

        internal static bool TryReadQuestDagValue<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            int index,
            out T value)
            where T : struct
        {
            value = default;
            if ((uint)index >= (uint)requiredLength ||
                !TryReadQuestDagBuffer(vault, in handle, bufferId, requiredLength, out NativeArray<T>.ReadOnly values) ||
                (uint)index >= (uint)values.Length)
            {
                return false;
            }

            value = values[index];
            return true;
        }

        internal static bool TryReadQuestDagCounter(
            IDataVault vault,
            ref QuestDagBufferHandles handles,
            QuestDagRuntimeConstants.CounterSlot slot,
            out int value)
        {
            value = 0;
            int index = (int)slot;
            if (!TryReadQuestDagBuffer(
                    vault,
                    in handles.Counters,
                    BufferID.QuestDagCounters,
                    QuestDagRuntimeConstants.CounterCount,
                    out NativeArray<int>.ReadOnly counters) ||
                (uint)index >= (uint)counters.Length)
            {
                return false;
            }

            value = counters[index];
            return true;
        }

        internal static bool TryFindQuestDagNodeIndex(
            IDataVault vault,
            ref QuestDagBufferHandles handles,
            int nodeCount,
            uint nodeHash,
            out int nodeIndex)
        {
            nodeIndex = -1;
            if (!TryReadQuestDagBuffer(
                    vault,
                    in handles.Nodes,
                    BufferID.QuestDagNodes,
                    handles.NodeCapacity,
                    out NativeArray<QuestNodeDTO>.ReadOnly nodes))
            {
                return false;
            }

            nodeIndex = FindNodeIndex(nodes, nodeCount, nodeHash);
            return true;
        }

        private static bool IsQuestDagVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)VaultOwnerSystem &&
                   handle.Generation != 0u;
        }

        private static void ReleaseQuestDagVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            if (vault != null && IsQuestDagVaultHandle(in handle, bufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static int FindNodeIndex(NativeArray<QuestNodeDTO>.ReadOnly nodes, int nodeCount, uint nodeHash)
        {
            for (int i = 0; i < nodeCount; i++)
            {
                if (nodes[i].NodeHash == nodeHash)
                    return i;
            }

            return -1;
        }
    }

    /// <summary>
    /// Thin scheduler around pure quest DAG jobs. Persistent truth stays in GlobalDataVault.
    /// </summary>
    public sealed class QuestDagResolverService : IDisposable
    {
        private const string OwnerLabel = nameof(QuestDagResolverService);
        private const string SpatialHashLabel = "_triggerSpatialHash";
        private static readonly ulong ScheduledMutationGuardMask =
            VaultMutationGuardBit(BufferID.QuestDagGlobalStateMasks) |
            VaultMutationGuardBit(BufferID.QuestDagOldStateMasks) |
            VaultMutationGuardBit(BufferID.QuestDagNodes) |
            VaultMutationGuardBit(BufferID.QuestDagNodeRuntime) |
            VaultMutationGuardBit(BufferID.QuestDagTriggerVolumes) |
            VaultMutationGuardBit(BufferID.QuestDagRequiredItemHashes) |
            VaultMutationGuardBit(BufferID.QuestDagRequiredItemQuantities) |
            VaultMutationGuardBit(BufferID.QuestDagPlayerItemHashes) |
            VaultMutationGuardBit(BufferID.QuestDagPlayerItemQuantities) |
            VaultMutationGuardBit(BufferID.QuestDagFactionStandings) |
            VaultMutationGuardBit(BufferID.QuestDagTelemetryRing) |
            VaultMutationGuardBit(BufferID.QuestDagTelemetryCursor) |
            VaultMutationGuardBit(BufferID.QuestDagCounters) |
            VaultMutationGuardBit(BufferID.QuestDagTriggerNodeIndices) |
            VaultMutationGuardBit(BufferID.QuestDagNoTriggerNodeIndices) |
            VaultMutationGuardBit(BufferID.QuestDagCsvMonitor);
        private readonly IDataVault _vault;
        private QuestDagBufferHandles _handles;
        private NativeParallelMultiHashMap<int, int> _triggerSpatialHash;
        private JobHandle _scheduledHandle;
        private long _scheduledTimestamp;
        private uint _scheduledFrame;
        private float _tickDilationPressure01;
        private int _resolverCadenceFrames = 1;
        private bool _hasScheduled;
        private bool _scheduledBufferGuardHeld;
        private bool _disposed;
        private bool _spatialHashReady;
        private int _lastSpatialVersion = int.MinValue;
        private int _lastTriggerCount = -1;
        private int _pendingScheduleDropCount;
        private int _pendingSpatialHashRebuildCount;

        /// <summary>
        /// Constructs the resolver and allocates the transient spatial index.
        /// </summary>
        public QuestDagResolverService(
            IDataVault vault,
            int nodeCapacity = QuestDagRuntimeConstants.DefaultNodeCapacity,
            int triggerCapacity = QuestDagRuntimeConstants.DefaultTriggerCapacity)
        {
            _vault = vault;
            _handles = QuestDagVault.EnsureBuffers(vault, nodeCapacity, triggerCapacity);
            int spatialHashCapacity = math.max(
                1,
                triggerCapacity * QuestDagRuntimeConstants.SpatialHashCellsPerTriggerBudget);
            _triggerSpatialHash = new NativeParallelMultiHashMap<int, int>(
                spatialHashCapacity,
                Allocator.Persistent); // COLD ALLOC: NativeParallelMultiHashMap<int,int>[triggerCapacity*27] - expanded trigger-cell occupancy, quest truth remains in GlobalDataVault - owner: QuestDagResolverService
            NativeMemorySentinel.RegisterNativeParallelMultiHashMap(
                _triggerSpatialHash,
                OwnerLabel,
                SpatialHashLabel,
                NativeAllocationLifetime.Session);
            SignalBus<StateChangedSignal>.Configure(
                expectedCapacity: 256,
                maxFrameSignals: 512,
                lowTierFrameSignals: 64,
                laneHash: QuestDagRuntimeConstants.SignalSourceHash);
            SignalBus<StateChangedSignal>.EnsureInitialized();
        }

        /// <summary>Current vault handles, exposed for editor and save bridges.</summary>
        public ref QuestDagBufferHandles Handles => ref _handles;

        /// <summary>Current resolver cadence in frames, continuously derived from system pressure.</summary>
        public int ResolverCadenceFrames => _resolverCadenceFrames;

        /// <summary>Current smoothed resolver pressure.</summary>
        public float TickDilationPressure01 => _tickDilationPressure01;

        /// <summary>
        /// Schedules spatial hash rebuild and graph resolution.
        /// </summary>
        public JobHandle Schedule(
            double3 playerAup,
            ulong currentTimestamp,
            uint frame,
            float systemHealthIndex,
            JobHandle dependency)
        {
            if (_disposed || _vault == null || !_triggerSpatialHash.IsCreated)
                return dependency;

            if (_hasScheduled)
            {
                _pendingScheduleDropCount = _pendingScheduleDropCount == int.MaxValue ? int.MaxValue : _pendingScheduleDropCount + 1;
                return JobHandle.CombineDependencies(dependency, _scheduledHandle);
            }

            UpdateTickDilation(systemHealthIndex);
            if (_resolverCadenceFrames > 1 && (frame % (uint)_resolverCadenceFrames) != 0u)
                return dependency;

            if (!TryAcquireScheduledBufferGuard())
                return dependency;

            bool scheduled = false;
            try
            {
                if (!QuestDagVault.TryResolveBuffers(_vault, ref _handles, out QuestDagBuffers buffers))
                    return dependency;

                int nodeCount = ReadCounter(buffers.Counters, QuestDagRuntimeConstants.CounterSlot.NodeCount);
                int triggerCount = ReadCounter(buffers.Counters, QuestDagRuntimeConstants.CounterSlot.TriggerCount);
                int stateChunkCount = ReadCounter(buffers.Counters, QuestDagRuntimeConstants.CounterSlot.StateChunkCount);
                if (nodeCount <= 0 || stateChunkCount <= 0)
                    return dependency;

                int spatialVersion = ReadCounter(buffers.Counters, QuestDagRuntimeConstants.CounterSlot.SpatialHashVersion);
                bool rebuildSpatialHash = !_spatialHashReady ||
                                          spatialVersion != _lastSpatialVersion ||
                                          triggerCount != _lastTriggerCount;
                JobHandle buildHandle = dependency;
                if (rebuildSpatialHash)
                {
                    _triggerSpatialHash.Clear();

                    BuildQuestDagSpatialHashJob buildJob = new BuildQuestDagSpatialHashJob
                    {
                        TriggerVolumes = buffers.TriggerVolumes,
                        TriggerNodeIndices = buffers.TriggerNodeIndices,
                        SpatialHash = _triggerSpatialHash.AsParallelWriter(),
                        TriggerCount = math.min(triggerCount, buffers.TriggerVolumes.Length)
                    };

                    buildHandle = buildJob.Schedule(dependency);
                    _spatialHashReady = true;
                    _lastSpatialVersion = spatialVersion;
                    _lastTriggerCount = triggerCount;
                    _pendingSpatialHashRebuildCount = _pendingSpatialHashRebuildCount == int.MaxValue
                        ? int.MaxValue
                        : _pendingSpatialHashRebuildCount + 1;
                }

                GraphResolverJob resolverJob = new GraphResolverJob
                {
                    GlobalStateMasks = buffers.GlobalStateMasks,
                    OldStateMasks = buffers.OldStateMasks,
                    Nodes = buffers.Nodes,
                    NodeRuntime = buffers.NodeRuntime,
                    TriggerVolumes = buffers.TriggerVolumes,
                    RequiredItemHashes = buffers.RequiredItemHashes,
                    RequiredItemQuantities = buffers.RequiredItemQuantities,
                    PlayerItemHashes = buffers.PlayerItemHashes,
                    PlayerItemQuantities = buffers.PlayerItemQuantities,
                    FactionStandings = buffers.FactionStandings,
                    TelemetryRing = buffers.TelemetryRing,
                    TelemetryCursor = buffers.TelemetryCursor,
                    Counters = buffers.Counters,
                    NoTriggerNodeIndices = buffers.NoTriggerNodeIndices,
                    SpatialHash = _triggerSpatialHash,
                    StateChangedWriter = SignalBus<StateChangedSignal>.ParallelWriter,
                    StateChangedWriterBudget = SignalBus<StateChangedSignal>.ParallelWriterBudget,
                    PlayerAUP = playerAup,
                    CurrentTimestamp = currentTimestamp,
                    Frame = frame,
                    NodeCount = math.min(nodeCount, buffers.Nodes.Length),
                    TriggerCount = math.min(triggerCount, buffers.TriggerVolumes.Length),
                    PlayerItemCount = math.min(
                        ReadCounter(buffers.Counters, QuestDagRuntimeConstants.CounterSlot.PlayerItemCount),
                        buffers.PlayerItemHashes.Length),
                    NoTriggerNodeCount = math.min(
                        ReadCounter(buffers.Counters, QuestDagRuntimeConstants.CounterSlot.NoTriggerNodeCount),
                        buffers.NoTriggerNodeIndices.Length),
                    StateChunkCount = math.min(stateChunkCount, buffers.GlobalStateMasks.Length),
                    ResolverCadenceFrames = _resolverCadenceFrames
                };

                _scheduledTimestamp = Stopwatch.GetTimestamp();
                _scheduledFrame = frame;
                _scheduledHandle = resolverJob.Schedule(buildHandle);
                _hasScheduled = true;
                scheduled = true;
                return _scheduledHandle;
            }
            finally
            {
                if (!scheduled)
                    ReleaseScheduledBufferGuard();
            }
        }

        /// <summary>
        /// End-of-frame completion hook for telemetry patching and black-box dump on fixed-point lock.
        /// </summary>
        public void CompleteScheduled()
        {
            if (!_hasScheduled)
                return;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _scheduledHandle))
                return;

            _hasScheduled = false;
            ReleaseScheduledBufferGuard();

            PatchPendingScheduleDrops();
            PatchSpatialHashRebuildCount();

            if (!TryReadLastTelemetryCursor(out int telemetryCursor))
                return;

            PatchLastComputeTime(telemetryCursor);
            QuestDagTelemetryEntry last = ReadLastTelemetry(telemetryCursor);
            if ((last.Flags & (ushort)QuestDagTelemetryFlags.FixedPointLimitHit) != 0)
                DumpTelemetry(QuestDagRuntimeConstants.DeadlockDumpPath);
        }

        private void PatchPendingScheduleDrops()
        {
            if (_pendingScheduleDropCount == 0 || _vault == null)
                return;

            if (!_vault.TryAcquireWriteLock(in _handles.Counters, SystemID.QuestDag, out NativeArray<int> counters))
                return;

            try
            {
                int previous = ReadCounter(counters, QuestDagRuntimeConstants.CounterSlot.PendingScheduleDropCount);
                WriteCounter(
                    counters,
                    QuestDagRuntimeConstants.CounterSlot.PendingScheduleDropCount,
                    unchecked(previous + _pendingScheduleDropCount));
                _pendingScheduleDropCount = 0;
            }
            finally
            {
                _vault.ReleaseWriteLock(in _handles.Counters, SystemID.QuestDag);
            }
        }

        private void PatchSpatialHashRebuildCount()
        {
            if (_pendingSpatialHashRebuildCount == 0 || _vault == null)
                return;

            if (!_vault.TryAcquireWriteLock(in _handles.Counters, SystemID.QuestDag, out NativeArray<int> counters))
                return;

            try
            {
                int previous = ReadCounter(counters, QuestDagRuntimeConstants.CounterSlot.SpatialHashRebuildCount);
                WriteCounter(
                    counters,
                    QuestDagRuntimeConstants.CounterSlot.SpatialHashRebuildCount,
                    unchecked(previous + _pendingSpatialHashRebuildCount));
                _pendingSpatialHashRebuildCount = 0;
            }
            finally
            {
                _vault.ReleaseWriteLock(in _handles.Counters, SystemID.QuestDag);
            }
        }

        /// <summary>
        /// Applies the editor/debug forced completion path and emits the same state-change signal.
        /// </summary>
        public bool ForceCompleteNode(uint nodeHash, uint frame)
        {
            if (_hasScheduled)
                return false;

            return QuestDagDebugApi.ForceCompleteNode(_vault, ref _handles, nodeHash, frame);
        }

        /// <summary>
        /// Cold live-balance bridge for quest_logic_overrides.csv. Call from an editor tool or slow tick.
        /// </summary>
#if UNITY_EDITOR
        public bool TryApplyCsvOverrides(string path, out int appliedRows)
        {
            appliedRows = 0;
            if (_hasScheduled)
                return false;

            return QuestDagCsvOverrideIngestor.TryApplyOverridesFromFile(_vault, ref _handles, path, out appliedRows);
        }
#endif

        /// <summary>
        /// Marks the transient spatial hash dirty after cold trigger data mutation.
        /// </summary>
        public void InvalidateSpatialHash()
        {
            _spatialHashReady = false;
            if (_hasScheduled)
                return;

            if (_vault == null ||
                !_vault.TryAcquireWriteLock(in _handles.Counters, SystemID.QuestDag, out NativeArray<int> counters))
            {
                return;
            }

            try
            {
                int version = ReadCounter(counters, QuestDagRuntimeConstants.CounterSlot.SpatialHashVersion);
                WriteCounter(counters, QuestDagRuntimeConstants.CounterSlot.SpatialHashVersion, version + 1);
            }
            finally
            {
                _vault.ReleaseWriteLock(in _handles.Counters, SystemID.QuestDag);
            }
        }

        /// <summary>
        /// Direct mask ref access required by the CS1612 purge.
        /// </summary>
        public bool TryReadStateMask(int chunkIndex, out ulong mask)
        {
            return QuestDagVault.TryReadStateMask(_vault, ref _handles, chunkIndex, out mask);
        }

        /// <summary>
        /// Copies packed state to an externally owned save buffer.
        /// </summary>
        public bool TryCopySaveState(NativeArray<ulong> destination)
        {
            return QuestDagVault.TryCopySaveState(_vault, ref _handles, destination);
        }

        /// <summary>
        /// Writes the 300-frame black box to disk. Cold fault path only.
        /// </summary>
        public void DumpTelemetry(string relativePath)
        {
            if (!QuestDagVault.TryReadQuestDagBuffer(
                    _vault,
                    in _handles.TelemetryRing,
                    BufferID.QuestDagTelemetryRing,
                    QuestDagRuntimeConstants.TelemetryCapacity,
                    out NativeArray<QuestDagTelemetryEntry>.ReadOnly telemetryRing) ||
                !telemetryRing.IsCreated)
            {
                return;
            }

            int cursor = 0;
            if (QuestDagVault.TryReadQuestDagBuffer(
                    _vault,
                    in _handles.TelemetryCursor,
                    BufferID.QuestDagTelemetryCursor,
                    1,
                    out NativeArray<int>.ReadOnly telemetryCursor) &&
                telemetryCursor.IsCreated)
            {
                cursor = telemetryCursor[0];
            }

            QuestDagTelemetryDump.Write(relativePath, telemetryRing, cursor);
            if (string.Equals(relativePath, QuestDagRuntimeConstants.DeadlockDumpPath, StringComparison.Ordinal))
                QuestDagTelemetryDump.Write(QuestDagRuntimeConstants.DeadlockH8DumpPath, telemetryRing, cursor);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (!_disposed && _hasScheduled)
            {
                DispatcherJobFence.TryComplete(ref _scheduledHandle, forceComplete: true);
                _hasScheduled = false;
                ReleaseScheduledBufferGuard();
            }

            JobHandle disposeHandle = Dispose(default);
            DispatcherJobFence.TryComplete(ref disposeHandle, forceComplete: true);
        }

        /// <summary>
        /// Fences active resolver work before releasing native scratch and Vault handles.
        /// </summary>
        public JobHandle Dispose(JobHandle dependency)
        {
            if (_disposed)
                return dependency;

            JobHandle disposeDependency = dependency;
            if (_hasScheduled)
            {
                disposeDependency = JobHandle.CombineDependencies(disposeDependency, _scheduledHandle);
                _hasScheduled = false;
            }

            if (_triggerSpatialHash.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeParallelMultiHashMap(OwnerLabel, SpatialHashLabel);
                disposeDependency = _triggerSpatialHash.Dispose(disposeDependency);
                _triggerSpatialHash = default;
            }

            DispatcherJobFence.TryComplete(ref disposeDependency, forceComplete: true);
            ReleaseScheduledBufferGuard();
            QuestDagVault.ReleaseBuffers(_vault, ref _handles);

            _disposed = true;
            return disposeDependency;
        }

        private bool TryAcquireScheduledBufferGuard()
        {
            IDataVault vault = _vault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            ReleaseScheduledBufferGuard();
            if (!vault.TryAcquireMutationGuard(ScheduledMutationGuardMask))
                return false;

            _scheduledBufferGuardHeld = true;
            return true;
        }

        private void ReleaseScheduledBufferGuard()
        {
            if (!_scheduledBufferGuardHeld)
                return;

            _scheduledBufferGuardHeld = false;
            _vault?.ReleaseMutationGuard(ScheduledMutationGuardMask);
        }

        private static ulong VaultMutationGuardBit(BufferID bufferId)
        {
            int bitIndex = unchecked((int)((uint)(int)bufferId & 63u));
            return 1UL << bitIndex;
        }

        private static int ReadCounter(NativeArray<int> counters, QuestDagRuntimeConstants.CounterSlot slot)
        {
            int index = (int)slot;
            return counters.IsCreated && (uint)index < (uint)counters.Length ? counters[index] : 0;
        }

        private static void WriteCounter(NativeArray<int> counters, QuestDagRuntimeConstants.CounterSlot slot, int value)
        {
            int index = (int)slot;
            if (counters.IsCreated && (uint)index < (uint)counters.Length)
                counters[index] = value;
        }

        private void UpdateTickDilation(float systemHealthIndex)
        {
            float targetPressure01 = math.isfinite(systemHealthIndex) ? math.saturate(systemHealthIndex) : 1f;
            _tickDilationPressure01 = math.lerp(_tickDilationPressure01, targetPressure01, 0.025f);
            float cadence01 = SmoothStep01(_tickDilationPressure01);
            _resolverCadenceFrames = 1 + (int)math.round(cadence01 * (QuestDagRuntimeConstants.ToasterTickModulo - 1));
        }

        private static float SmoothStep01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - 2f * t);
        }

        private bool TryReadLastTelemetryCursor(out int telemetryCursor)
        {
            telemetryCursor = 0;
            if (_vault == null ||
                !_vault.TryReadOnlyHandle(in _handles.TelemetryCursor, out NativeArray<int>.ReadOnly cursorView) ||
                cursorView.Length <= 0)
            {
                return false;
            }

            telemetryCursor = cursorView[0] - 1;
            return true;
        }

        private void PatchLastComputeTime(int telemetryCursor)
        {
            if (_vault == null ||
                !_vault.TryAcquireWriteLock(in _handles.TelemetryRing, SystemID.QuestDag, out NativeArray<QuestDagTelemetryEntry> telemetryRing))
            {
                return;
            }

            try
            {
                if (!telemetryRing.IsCreated || telemetryRing.Length <= 0)
                    return;

                int cursor = telemetryCursor;
                if (cursor < 0)
                    cursor += telemetryRing.Length;

                int index = cursor % telemetryRing.Length;
                QuestDagTelemetryEntry entry = telemetryRing[index];
                if (entry.Frame != _scheduledFrame)
                    return;

                long elapsed = Stopwatch.GetTimestamp() - _scheduledTimestamp;
                entry.ResolverComputeTimeMs = elapsed * 1000.0d / Stopwatch.Frequency;
                telemetryRing[index] = entry;
            }
            finally
            {
                _vault.ReleaseWriteLock(in _handles.TelemetryRing, SystemID.QuestDag);
            }
        }

        private QuestDagTelemetryEntry ReadLastTelemetry(int telemetryCursor)
        {
            if (_vault == null ||
                !_vault.TryReadOnlyHandle(in _handles.TelemetryRing, out NativeArray<QuestDagTelemetryEntry>.ReadOnly telemetryRing) ||
                telemetryRing.Length <= 0)
            {
                return default;
            }

            int cursor = telemetryCursor;
            if (cursor < 0)
                cursor += telemetryRing.Length;

            return telemetryRing[cursor % telemetryRing.Length];
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct BuildQuestDagSpatialHashJob : IJob
    {
        [ReadOnly] [NoAlias] public NativeArray<TriggerVolumeDTO> TriggerVolumes;
        [ReadOnly] [NoAlias] public NativeArray<int> TriggerNodeIndices;
        [NoAlias] public NativeParallelMultiHashMap<int, int>.ParallelWriter SpatialHash;
        public int TriggerCount;

        public void Execute()
        {
            int count = math.min(TriggerCount, math.min(TriggerVolumes.Length, TriggerNodeIndices.Length));
            for (int i = 0; i < count; i++)
            {
                TriggerVolumeDTO trigger = TriggerVolumes[i];
                if (trigger.RequiredNodeHash == 0u ||
                    !QuestDagSpatialHash.IsAupCellRangeSafe(trigger.AUP) ||
                    !math.isfinite(trigger.Radius))
                {
                    continue;
                }

                int nodeIndex = TriggerNodeIndices[i];
                if (nodeIndex < 0)
                    continue;

                double radius = math.min(
                    math.max((double)trigger.Radius, 0.0d),
                    QuestDagRuntimeConstants.SpatialCellSizeMeters *
                    QuestDagRuntimeConstants.SpatialHashMaxInsertedCellRadius);
                int3 centerCell = QuestDagSpatialHash.AupToCell(trigger.AUP);
                int3 minCell = QuestDagSpatialHash.AupToCell(trigger.AUP - new double3(radius));
                int3 maxCell = QuestDagSpatialHash.AupToCell(trigger.AUP + new double3(radius));
                minCell = math.max(minCell, centerCell - QuestDagRuntimeConstants.SpatialHashMaxInsertedCellRadius);
                maxCell = math.min(maxCell, centerCell + QuestDagRuntimeConstants.SpatialHashMaxInsertedCellRadius);

                for (int z = minCell.z; z <= maxCell.z; z++)
                {
                    for (int y = minCell.y; y <= maxCell.y; y++)
                    {
                        for (int x = minCell.x; x <= maxCell.x; x++)
                            SpatialHash.Add(QuestDagSpatialHash.HashCell(new int3(x, y, z)), nodeIndex);
                    }
                }
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct GraphResolverJob : IJob
    {
        [NoAlias] public NativeArray<ulong> GlobalStateMasks;
        [NoAlias] public NativeArray<ulong> OldStateMasks;
        [ReadOnly] [NoAlias] public NativeArray<QuestNodeDTO> Nodes;
        [ReadOnly] [NoAlias] public NativeArray<QuestNodeRuntimeDTO> NodeRuntime;
        [ReadOnly] [NoAlias] public NativeArray<TriggerVolumeDTO> TriggerVolumes;
        [ReadOnly] [NoAlias] public NativeArray<uint> RequiredItemHashes;
        [ReadOnly] [NoAlias] public NativeArray<int> RequiredItemQuantities;
        [ReadOnly] [NoAlias] public NativeArray<uint> PlayerItemHashes;
        [ReadOnly] [NoAlias] public NativeArray<int> PlayerItemQuantities;
        [NoAlias] public NativeArray<float> FactionStandings;
        [NoAlias] public NativeArray<QuestDagTelemetryEntry> TelemetryRing;
        [NoAlias] public NativeArray<int> TelemetryCursor;
        [NoAlias] public NativeArray<int> Counters;
        [ReadOnly] [NoAlias] public NativeArray<int> NoTriggerNodeIndices;
        [ReadOnly] [NoAlias] public NativeParallelMultiHashMap<int, int> SpatialHash;
        [NoAlias] public global::Hecton8.Core.MpscSignalRingBuffer<StateChangedSignal>.ParallelWriter StateChangedWriter;
        [NativeDisableParallelForRestriction] public NativeArray<int> StateChangedWriterBudget;
        public double3 PlayerAUP;
        public ulong CurrentTimestamp;
        public uint Frame;
        public int NodeCount;
        public int TriggerCount;
        public int PlayerItemCount;
        public int NoTriggerNodeCount;
        public int StateChunkCount;
        public int ResolverCadenceFrames;

        public void Execute()
        {
            int nodeCount = math.min(NodeCount, math.min(Nodes.Length, NodeRuntime.Length));
            int stateChunkCount = math.min(StateChunkCount, math.min(GlobalStateMasks.Length, OldStateMasks.Length));
            if (nodeCount <= 0 || stateChunkCount <= 0)
                return;

            NodeCount = nodeCount;
            StateChunkCount = stateChunkCount;
            TriggerCount = math.min(TriggerCount, TriggerVolumes.Length);
            PlayerItemCount = math.min(PlayerItemCount, math.min(PlayerItemHashes.Length, PlayerItemQuantities.Length));
            NoTriggerNodeCount = math.min(NoTriggerNodeCount, NoTriggerNodeIndices.Length);

            bool invalidAup = !QuestDagSpatialHash.IsAupCellRangeSafe(PlayerAUP);
            bool changed = false;
            ushort flags = 0;
            ushort iterations = 0;
            int evaluatedNodes = 0;
            int spatialCandidateCount = 0;
            ulong bitsFlipped = 0UL;
            uint deadlockNode = 0u;

            for (int iteration = 0; iteration < QuestDagRuntimeConstants.MaxFixedPointIterations; iteration++)
            {
                iterations = (ushort)(iteration + 1);
                bool passChanged = false;

                bool noTriggerBudgeted = false;
                if (!invalidAup)
                    passChanged |= EvaluateSpatialCandidates(ref evaluatedNodes, ref spatialCandidateCount, ref bitsFlipped, ref deadlockNode);

                if (NoTriggerNodeCount > 0)
                    passChanged |= EvaluateNoTriggerNodes(ref evaluatedNodes, ref bitsFlipped, ref deadlockNode, ref noTriggerBudgeted);

                changed |= passChanged;
                if (noTriggerBudgeted)
                    flags |= (ushort)QuestDagTelemetryFlags.NoTriggerBudgeted;
                if (!passChanged)
                    break;
            }

            if (invalidAup)
                flags |= (ushort)QuestDagTelemetryFlags.InvalidAup;
            if (changed && iterations >= QuestDagRuntimeConstants.MaxFixedPointIterations)
                flags |= (ushort)QuestDagTelemetryFlags.FixedPointLimitHit;
            if (ResolverCadenceFrames > 1)
                flags |= (ushort)QuestDagTelemetryFlags.ToasterDilated;

            EmitStateChanges(stateChunkCount);
            uint playerCellHash = invalidAup ? 0u : unchecked((uint)QuestDagSpatialHash.HashAupToCell(PlayerAUP));
            uint stateHash = ComputeStateHash(stateChunkCount);
            WriteTelemetry(evaluatedNodes, spatialCandidateCount, bitsFlipped, iterations, flags, deadlockNode, playerCellHash, stateHash);
        }

        private bool EvaluateSpatialCandidates(
            ref int evaluatedNodes,
            ref int spatialCandidateCount,
            ref ulong bitsFlipped,
            ref uint deadlockNode)
        {
            bool changed = false;
            int3 playerCell = QuestDagSpatialHash.AupToCell(PlayerAUP);
            for (int z = -1; z <= 1; z++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        int key = QuestDagSpatialHash.HashCell(playerCell + new int3(x, y, z));
                        NativeParallelMultiHashMapIterator<int> iterator;
                        int nodeIndex;
                        if (!SpatialHash.TryGetFirstValue(key, out nodeIndex, out iterator))
                            continue;

                        do
                        {
                            spatialCandidateCount++;
                            if ((uint)nodeIndex >= (uint)NodeCount)
                                continue;

                            changed |= TryResolveNode(nodeIndex, ref evaluatedNodes, ref bitsFlipped, ref deadlockNode);
                        }
                        while (SpatialHash.TryGetNextValue(out nodeIndex, ref iterator));
                    }
                }
            }

            return changed;
        }

        private bool EvaluateNoTriggerNodes(
            ref int evaluatedNodes,
            ref ulong bitsFlipped,
            ref uint deadlockNode,
            ref bool budgeted)
        {
            bool changed = false;
            int count = math.min(NoTriggerNodeCount, NoTriggerNodeIndices.Length);
            int budget = math.min(count, QuestDagRuntimeConstants.NoTriggerNodesPerPassBudget);
            int cursor = ReadCounter(QuestDagRuntimeConstants.CounterSlot.NoTriggerCursor);
            if ((uint)cursor >= (uint)count)
                cursor = 0;

            for (int offset = 0; offset < budget; offset++)
            {
                int i = cursor + offset;
                if (i >= count)
                    i -= count;

                int nodeIndex = NoTriggerNodeIndices[i];
                if ((uint)nodeIndex >= (uint)NodeCount)
                    continue;

                changed |= TryResolveNode(nodeIndex, ref evaluatedNodes, ref bitsFlipped, ref deadlockNode);
            }

            if (budget < count)
            {
                budgeted = true;
                WriteCounter(QuestDagRuntimeConstants.CounterSlot.NoTriggerCursor, (cursor + budget) % count);
            }
            else
            {
                WriteCounter(QuestDagRuntimeConstants.CounterSlot.NoTriggerCursor, 0);
            }

            return changed;
        }

        private bool TryResolveNode(
            int nodeIndex,
            ref int evaluatedNodes,
            ref ulong bitsFlipped,
            ref uint deadlockNode)
        {
            QuestNodeDTO node = Nodes[nodeIndex];
            QuestNodeRuntimeDTO runtime = NodeRuntime[nodeIndex];
            evaluatedNodes++;

            if (node.NodeHash == 0u ||
                (uint)runtime.StateChunk >= (uint)StateChunkCount ||
                IsCompleted(runtime.StateChunk, node.CompletionMask))
            {
                return false;
            }

            ulong stateMask = GlobalStateMasks[runtime.StateChunk];
            if ((stateMask & node.PrerequisiteMask) != node.PrerequisiteMask)
                return false;

            if ((runtime.Flags & (ushort)QuestDagNodeFlags.RequiresTimestamp) != 0 &&
                runtime.TargetTimestamp != 0UL &&
                CurrentTimestamp < runtime.TargetTimestamp)
            {
                return false;
            }

            if ((runtime.Flags & (ushort)QuestDagNodeFlags.RequiresTrigger) != 0 &&
                !IsPlayerInsideRequiredTrigger(runtime.TriggerIndex, node.NodeHash))
            {
                return false;
            }

            if ((runtime.Flags & (ushort)QuestDagNodeFlags.RequiresInventory) != 0 &&
                !HasRequiredItems(runtime.RequiredItemStart, runtime.RequiredItemCount))
            {
                return false;
            }

            if ((runtime.Flags & (ushort)QuestDagNodeFlags.RequiresFactionThreshold) != 0 &&
                !HasFactionStanding(runtime.FactionId, runtime.ReputationThreshold))
            {
                return false;
            }

            bool flipped = AtomicOrMask(GlobalStateMasks, runtime.StateChunk, node.CompletionMask);
            if (!flipped)
                return false;

            bitsFlipped |= node.CompletionMask;
            deadlockNode = node.NodeHash;

            if ((runtime.Flags & (ushort)QuestDagNodeFlags.AppliesFactionDelta) != 0)
                AtomicAddFloat(FactionStandings, runtime.FactionId, runtime.ReputationDelta);

            return true;
        }

        private bool IsCompleted(int stateChunk, ulong completionMask)
        {
            return completionMask == 0UL ||
                   (GlobalStateMasks[stateChunk] & completionMask) == completionMask;
        }

        private bool IsPlayerInsideRequiredTrigger(int triggerIndex, uint nodeHash)
        {
            if ((uint)triggerIndex >= (uint)TriggerCount || (uint)triggerIndex >= (uint)TriggerVolumes.Length)
                return false;

            TriggerVolumeDTO trigger = TriggerVolumes[triggerIndex];
            if (trigger.RequiredNodeHash != 0u && trigger.RequiredNodeHash != nodeHash)
                return false;

            double3 deltaDouble = PlayerAUP - trigger.AUP;
            if (!math.all(math.isfinite(deltaDouble)))
                return false;

            float radius = math.max(0f, trigger.Radius);
            float3 delta = (float3)deltaDouble;
            return math.lengthsq(delta) <= radius * radius;
        }

        private bool HasRequiredItems(int start, int count)
        {
            if (count <= 0)
                return true;

            int end = start + count;
            if (start < 0 ||
                end < start ||
                end > RequiredItemHashes.Length ||
                end > RequiredItemQuantities.Length)
            {
                return false;
            }

            for (int i = start; i < end; i++)
            {
                uint requiredHash = RequiredItemHashes[i];
                int requiredQuantity = RequiredItemQuantities[i];
                if (requiredHash == 0u || requiredQuantity <= 0)
                    continue;

                bool matched = false;
                for (int p = 0; p < PlayerItemCount; p++)
                {
                    if (PlayerItemHashes[p] != requiredHash)
                        continue;

                    matched = PlayerItemQuantities[p] >= requiredQuantity;
                    break;
                }

                if (!matched)
                    return false;
            }

            return true;
        }

        private bool HasFactionStanding(ushort factionId, float threshold)
        {
            if ((uint)factionId >= (uint)FactionStandings.Length)
                return false;

            float standing = FactionStandings[factionId];
            return math.isfinite(standing) && standing > threshold;
        }

        private void EmitStateChanges(int stateChunkCount)
        {
            ushort sequence = 0;
            for (int i = 0; i < stateChunkCount; i++)
            {
                ulong oldMask = OldStateMasks[i];
                ulong newMask = GlobalStateMasks[i];
                ulong flipped = oldMask ^ newMask;
                if (flipped == 0UL)
                    continue;

                SignalBus<StateChangedSignal>.TryEnqueueBounded(StateChangedWriter, StateChangedWriterBudget, new StateChangedSignal
                {
                    FlippedMask = flipped,
                    NewMask = newMask,
                    Frame = Frame,
                    ChunkIndex = i,
                    Flags = 0,
                    Sequence = sequence++,
                    SourceHash = QuestDagRuntimeConstants.SignalSourceHash
                });
                OldStateMasks[i] = newMask;
            }
        }

        private void WriteTelemetry(
            int evaluatedNodes,
            int spatialCandidateCount,
            ulong bitsFlipped,
            ushort iterations,
            ushort flags,
            uint deadlockNode,
            uint playerCellHash,
            uint stateHash)
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length <= 0 || !TelemetryCursor.IsCreated)
                return;

            int cursor = TelemetryCursor[0];
            int index = cursor % TelemetryRing.Length;
            TelemetryRing[index] = new QuestDagTelemetryEntry
            {
                ResolverComputeTimeMs = 0d,
                BitsFlipped = bitsFlipped,
                Frame = Frame,
                ActiveNodesEvaluated = evaluatedNodes,
                Iterations = iterations,
                Flags = flags,
                DeadlockNodeHash = deadlockNode,
                PlayerCellHash = playerCellHash,
                StateHash = stateHash,
                SpatialCandidateCount = spatialCandidateCount,
                _pad0 = 0u
            };
            TelemetryCursor[0] = (cursor + 1) % TelemetryRing.Length;

            WriteCounter(QuestDagRuntimeConstants.CounterSlot.LastBitsFlippedLow, unchecked((int)(bitsFlipped & 0xFFFFFFFFUL)));
            WriteCounter(QuestDagRuntimeConstants.CounterSlot.LastBitsFlippedHigh, unchecked((int)(bitsFlipped >> 32)));
            WriteCounter(QuestDagRuntimeConstants.CounterSlot.LastEvaluatedNodes, evaluatedNodes);
            WriteCounter(QuestDagRuntimeConstants.CounterSlot.LastIterations, iterations);
            if ((flags & (ushort)QuestDagTelemetryFlags.FixedPointLimitHit) != 0)
                WriteCounter(QuestDagRuntimeConstants.CounterSlot.LastDeadlockFrame, unchecked((int)Frame));
        }

        private uint ComputeStateHash(int stateChunkCount)
        {
            unchecked
            {
                uint hash = 2166136261u;
                int count = math.min(stateChunkCount, GlobalStateMasks.Length);
                for (int i = 0; i < count; i++)
                {
                    ulong value = GlobalStateMasks[i];
                    hash = (hash ^ (uint)value) * 16777619u;
                    hash = (hash ^ (uint)(value >> 32)) * 16777619u;
                }

                return hash;
            }
        }

        private int ReadCounter(QuestDagRuntimeConstants.CounterSlot slot)
        {
            int index = (int)slot;
            return Counters.IsCreated && (uint)index < (uint)Counters.Length ? Counters[index] : 0;
        }

        private void WriteCounter(QuestDagRuntimeConstants.CounterSlot slot, int value)
        {
            int index = (int)slot;
            if (Counters.IsCreated && (uint)index < (uint)Counters.Length)
                Counters[index] = value;
        }

        private static bool AtomicOrMask(NativeArray<ulong> masks, int index, ulong mask)
        {
            if (mask == 0UL || (uint)index >= (uint)masks.Length)
                return false;

            ulong* basePtr = (ulong*)masks.GetUnsafePtr();
            long* target = (long*)(basePtr + index);
            long oldValue = UnsafeUtility.AsRef<long>(target);
            long desired;
            do
            {
                desired = oldValue | unchecked((long)mask);
                if (desired == oldValue)
                    return false;

                long observed = Interlocked.CompareExchange(ref UnsafeUtility.AsRef<long>(target), desired, oldValue);
                if (observed == oldValue)
                    return true;

                oldValue = observed;
            }
            while (true);
        }

        private static void AtomicAddFloat(NativeArray<float> values, ushort index, float delta)
        {
            if (!math.isfinite(delta) || (uint)index >= (uint)values.Length)
                return;

            float* basePtr = (float*)values.GetUnsafePtr();
            int* target = (int*)(basePtr + index);
            int oldBits = UnsafeUtility.AsRef<int>(target);
            do
            {
                float oldValue = math.asfloat((uint)oldBits);
                float newValue = oldValue + delta;
                if (!math.isfinite(newValue))
                    return;

                int newBits = unchecked((int)math.asuint(newValue));
                int observed = Interlocked.CompareExchange(ref UnsafeUtility.AsRef<int>(target), newBits, oldBits);
                if (observed == oldBits)
                    return;

                oldBits = observed;
            }
            while (true);
        }
    }

    /// <summary>
    /// Deterministic 100 m AUP grid for narrative trigger culling.
    /// </summary>
    public static class QuestDagSpatialHash
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int HashAupToCell(double3 aup)
        {
            return HashCell(AupToCell(aup));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 AupToCell(double3 aup)
        {
            double invCell = 1.0d / QuestDagRuntimeConstants.SpatialCellSizeMeters;
            double3 cell = math.clamp(
                math.floor(aup * invCell),
                new double3(int.MinValue + 1.0d),
                new double3(int.MaxValue - 1.0d));
            return new int3((int)cell.x, (int)cell.y, (int)cell.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAupCellRangeSafe(double3 aup)
        {
            if (!math.all(math.isfinite(aup)))
                return false;

            double invCell = 1.0d / QuestDagRuntimeConstants.SpatialCellSizeMeters;
            double3 cell = math.floor(aup * invCell);
            return math.all(cell >= new double3(int.MinValue + 1.0d)) &&
                   math.all(cell <= new double3(int.MaxValue - 1.0d));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int HashCell(int3 cell)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)cell.x) * 16777619u;
                hash = (hash ^ (uint)cell.y) * 16777619u;
                hash = (hash ^ (uint)cell.z) * 16777619u;
                return (int)(hash & 0x7FFFFFFFu);
            }
        }
    }

    /// <summary>
    /// Editor/debug bridge over vault state. No gameplay ownership lives here.
    /// </summary>
    public static class QuestDagDebugApi
    {
        private static int s_x001QuestDagResolverRuntimeSignalPushDropCount;

        public static bool ForceCompleteNode(
            IDataVault vault,
            ref QuestDagBufferHandles handles,
            uint nodeHash,
            uint frame)
        {
            if (nodeHash == 0u ||
                !QuestDagVault.TryReadQuestDagCounter(
                    vault,
                    ref handles,
                    QuestDagRuntimeConstants.CounterSlot.NodeCount,
                    out int nodeCount))
            {
                return false;
            }

            nodeCount = math.min(nodeCount, handles.NodeCapacity);
            for (int i = 0; i < nodeCount; i++)
            {
                if (!QuestDagVault.TryReadQuestDagValue(
                        vault,
                        in handles.Nodes,
                        BufferID.QuestDagNodes,
                        handles.NodeCapacity,
                        i,
                        out QuestNodeDTO node))
                {
                    return false;
                }

                if (node.NodeHash != nodeHash)
                    continue;

                if (!QuestDagVault.TryReadQuestDagValue(
                        vault,
                        in handles.NodeRuntime,
                        BufferID.QuestDagNodeRuntime,
                        handles.NodeCapacity,
                        i,
                        out QuestNodeRuntimeDTO runtime) ||
                    (uint)runtime.StateChunk >= (uint)handles.StateChunkCount)
                {
                    return false;
                }

                if (!vault.TryAcquireWriteLock(
                        in handles.GlobalStateMasks,
                        SystemID.QuestDag,
                        out NativeArray<ulong> globalStateMasks))
                {
                    return false;
                }

                ulong oldMask;
                ulong newMask;
                try
                {
                    if ((uint)runtime.StateChunk >= (uint)globalStateMasks.Length)
                        return false;

                    oldMask = globalStateMasks[runtime.StateChunk];
                    newMask = oldMask | node.CompletionMask;
                    if (newMask == oldMask)
                        return false;

                    globalStateMasks[runtime.StateChunk] = newMask;
                }
                finally
                {
                    vault.ReleaseWriteLock(in handles.GlobalStateMasks, SystemID.QuestDag);
                }

                PatchOldStateMask(vault, ref handles, runtime.StateChunk, newMask);

                SignalBus<StateChangedSignal>.TryPushTracked(new StateChangedSignal
                {
                    FlippedMask = oldMask ^ newMask,
                    NewMask = newMask,
                    Frame = frame,
                    ChunkIndex = runtime.StateChunk,
                    Flags = 1,
                    Sequence = 0,
                    SourceHash = QuestDagRuntimeConstants.SignalSourceHash
                }, ref s_x001QuestDagResolverRuntimeSignalPushDropCount);
                return true;
            }

            return false;
        }

        private static void PatchOldStateMask(
            IDataVault vault,
            ref QuestDagBufferHandles handles,
            int stateChunk,
            ulong newMask)
        {
            if (vault == null ||
                !vault.TryAcquireWriteLock(
                    in handles.OldStateMasks,
                    SystemID.QuestDag,
                    out NativeArray<ulong> oldStateMasks))
            {
                return;
            }

            try
            {
                if ((uint)stateChunk < (uint)oldStateMasks.Length)
                    oldStateMasks[stateChunk] = newMask;
            }
            finally
            {
                vault.ReleaseWriteLock(in handles.OldStateMasks, SystemID.QuestDag);
            }
        }

        private static int ReadCounter(NativeArray<int> counters, QuestDagRuntimeConstants.CounterSlot slot)
        {
            int index = (int)slot;
            return counters.IsCreated && (uint)index < (uint)counters.Length ? counters[index] : 0;
        }
    }

    /// <summary>
    /// Cold black-box dump writer. File IO is only invoked on fixed-point lock or manual diagnostics.
    /// </summary>
    public static unsafe class QuestDagTelemetryDump
    {
        public const long DumpMagic = 0x5144414748443801L; // H8HDAGQ little-endian marker.
        public const int DumpHeaderBytes = 32;

        public static void Write(string relativePath, NativeArray<QuestDagTelemetryEntry>.ReadOnly telemetry, int cursor)
        {
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return;

            int entrySize = UnsafeUtility.SizeOf<QuestDagTelemetryEntry>();
            int payloadBytes = telemetry.Length * entrySize;
            int bytes = DumpHeaderBytes + payloadBytes;
            string fullPath = Path.GetFullPath(relativePath);
            try
            {
                string directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.WriteThrough))
                {
#if UNITY_STANDALONE || UNITY_EDITOR
                    stream.SetLength(bytes);
                    using (MemoryMappedFile mappedFile = MemoryMappedFile.CreateFromFile(stream, null, bytes, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, false))
                    using (MemoryMappedViewAccessor accessor = mappedFile.CreateViewAccessor(0L, bytes, MemoryMappedFileAccess.Write))
                    {
                        byte* destination = null;
                        accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref destination);
                        try
                        {
                            WriteHeader(destination, telemetry.Length, entrySize, cursor);
                            byte* payload = destination + DumpHeaderBytes;
                            byte* source = (byte*)telemetry.GetUnsafeReadOnlyPtr();
                            int normalizedCursor = cursor % telemetry.Length;
                            if (normalizedCursor < 0)
                                normalizedCursor += telemetry.Length;

                            for (int i = 0; i < telemetry.Length; i++)
                            {
                                int sourceIndex = normalizedCursor + i;
                                if (sourceIndex >= telemetry.Length)
                                    sourceIndex -= telemetry.Length;

                                UnsafeUtility.MemCpy(
                                    payload + (i * entrySize),
                                    source + (sourceIndex * entrySize),
                                    entrySize);
                            }
                        }
                        finally
                        {
                            accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                        }
                    }
#else
                    WriteStreamPayload(stream, telemetry, entrySize, cursor);
#endif
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void WriteStreamPayload(FileStream stream, NativeArray<QuestDagTelemetryEntry>.ReadOnly telemetry, int entrySize, int cursor)
        {
            Span<byte> header = stackalloc byte[DumpHeaderBytes];
            fixed (byte* headerPtr = header)
                WriteHeader(headerPtr, telemetry.Length, entrySize, cursor);

            stream.Write(header);
            byte* source = (byte*)telemetry.GetUnsafeReadOnlyPtr();
            int normalizedCursor = cursor % telemetry.Length;
            if (normalizedCursor < 0)
                normalizedCursor += telemetry.Length;

            for (int i = 0; i < telemetry.Length; i++)
            {
                int sourceIndex = normalizedCursor + i;
                if (sourceIndex >= telemetry.Length)
                    sourceIndex -= telemetry.Length;

                stream.Write(new ReadOnlySpan<byte>(source + (sourceIndex * entrySize), entrySize));
            }
        }

        private static void WriteHeader(byte* destination, int entryCount, int entrySize, int cursor)
        {
            long* header64 = (long*)destination;
            header64[0] = DumpMagic;
            int* header32 = (int*)(destination + sizeof(long));
            header32[0] = entryCount;
            header32[1] = entrySize;
            header32[2] = cursor;
            header32[3] = DumpHeaderBytes;
            header32[4] = QuestDagRuntimeConstants.MaxFixedPointIterations;
            header32[5] = QuestDagRuntimeConstants.SpatialCellSizeMeters;
        }
    }
}
