using System;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.AI.Pathfinding
{
    /// <summary>
    /// Runtime bridge for WFC door-driven path invalidation and funnel black-box telemetry.
    /// Persistent state lives in GlobalDataVault buffers; this component only caches generation-checked handles.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class PathFunnelNavmeshRuntime : MonoBehaviour, IColdTickable, IFastTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const int DefaultActivePathCapacity = 128;
        private const int DefaultInvalidationCapacity = 64;
        private const int MaxActivePathCapacity = 4096;
        private const int MaxInvalidationCapacity = 4096;
        private const string BlackBoxDumpRelativePath = "Docs/AgentLogs/Dump_1403_PATH_FUNNEL.bin";
        private const string BlackBoxDumpPayloadLabel = "pathFunnelTelemetryDumpPayload";
        private static readonly ulong FastTickMutationGuardMask =
            PathFunnelMutationGuardBit(BufferID.PathFunnelActivePaths) |
            PathFunnelMutationGuardBit(BufferID.PathFunnelCellMasks) |
            PathFunnelMutationGuardBit(BufferID.PathFunnelInvalidations) |
            PathFunnelMutationGuardBit(BufferID.PathFunnelRuntimeState);
        private static readonly ulong ActivePathMutationGuardMask =
            PathFunnelMutationGuardBit(BufferID.PathFunnelActivePaths) |
            PathFunnelMutationGuardBit(BufferID.PathFunnelCellMasks) |
            PathFunnelMutationGuardBit(BufferID.PathFunnelRuntimeState);
        private static readonly ulong InvalidationMutationGuardMask =
            PathFunnelMutationGuardBit(BufferID.PathFunnelInvalidations) |
            PathFunnelMutationGuardBit(BufferID.PathFunnelRuntimeState);
        private static readonly ulong RuntimeStateMutationGuardMask =
            PathFunnelMutationGuardBit(BufferID.PathFunnelRuntimeState);
        private static readonly ulong TelemetryMutationGuardMask =
            PathFunnelMutationGuardBit(BufferID.PathFunnelTelemetryRing) |
            PathFunnelMutationGuardBit(BufferID.PathFunnelRuntimeState);

        [Header("Path Funnel Runtime")]
        [SerializeField, Min(1), Tooltip("Maximum tracked active AI corridors for WFC door invalidation.")]
        private int _activePathCapacity = DefaultActivePathCapacity;

        [SerializeField, Min(2), Tooltip("Bounded invalidation ring capacity consumed by path owners.")]
        private int _invalidationCapacity = DefaultInvalidationCapacity;

        private IDataVault _dataVault;
        private VaultGenerationHandle<PathFunnelActivePath> _activePathsHandle;
        private VaultGenerationHandle<ulong> _activePathCellMasksHandle;
        private VaultGenerationHandle<PathFunnelInvalidation> _invalidationsHandle;
        private VaultGenerationHandle<PathFunnelTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<PathFunnelRuntimeState> _runtimeStateHandle;
        private VaultGenerationHandle<byte> _wfcGridHandle;
        private uint _lastBlackBoxDumpHash;
        private bool _registeredColdTick;
        private bool _registeredFastTick;
        private bool _registeredLateFrame;
        private bool _registeredHotSwap;
        private bool _pathFunnelColdBootstrapped;

        /// <summary>Total WFC-driven path invalidations observed by this runtime.</summary>
        public uint PathInvalidationCount
        {
            get
            {
                if (!TryReadRuntimeState(out NativeArray<PathFunnelRuntimeState> runtimeState))
                    return 0u;

                return runtimeState[0].PathInvalidationCount;
            }
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            RebindDataVaultForLifecycle(GlobalRegistry.DataVault);
            BootstrapPathFunnelCold();
            BootstrapVoxelAStarCold();
            TryRegisterDispatcherTicks();
            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void OnDisable()
        {
            TryUnregisterDispatcherTicks();

            if (_registeredHotSwap)
            {
                GlobalRegistry.UnregisterHotSwapListener(this);
                _registeredHotSwap = false;
            }

            ForceCompleteVoxelAStarJobsForTeardown();
            ReleaseVoxelAStarVaultHandles(_dataVault);
            ReleaseVaultHandles(_dataVault);
            ClearVoxelAStarVaultHandles();
            ClearVaultHandles();
            _dataVault = null;
        }

        /// <inheritdoc />
        public void FastTick(float deltaTime)
        {
            if (TryAcquirePathFunnelMutationGuard(FastTickMutationGuardMask, out IDataVault guardVault))
            {
                try
                {
                    if (EnsureMutationViews(
                            out NativeArray<PathFunnelActivePath> activePaths,
                            out NativeArray<ulong> activePathCellMasks,
                            out NativeArray<PathFunnelInvalidation> invalidations,
                            out NativeArray<PathFunnelRuntimeState> runtimeStateBuffer))
                    {
                        PathFunnelRuntimeState runtimeState = runtimeStateBuffer[0];
                        AdvanceRuntimeFrame(ref runtimeState);
                        NativeArray<byte> wfcGridBitmasks = default;
                        TryResolveWfcGrid(out wfcGridBitmasks);

                        ReadOnlySpan<WfcOutpostStateChangedSignal> signals = SignalBus<WfcOutpostStateChangedSignal>.GetFrameSnapshot();
                        for (int i = 0; i < signals.Length; i++)
                        {
                            ProcessWfcStateSignal(
                                in signals[i],
                                wfcGridBitmasks,
                                activePaths,
                                activePathCellMasks,
                                invalidations,
                                ref runtimeState);
                        }

                        runtimeStateBuffer[0] = runtimeState;
                    }
                }
                finally
                {
                    ReleasePathFunnelMutationGuard(guardVault, FastTickMutationGuardMask);
                }
            }

            FastTickVoxelAStar(deltaTime);
        }

        /// <inheritdoc />
        public void ColdTick()
        {
            if (!Application.isPlaying)
                return;

            if (_dataVault == null)
                RebindDataVaultForLifecycle(GlobalRegistry.DataVault);

            BootstrapPathFunnelCold();
            BootstrapVoxelAStarCold();
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            bool dumpRequested = false;
            bool dumpStaged = false;
            int dumpByteCount = 0;
            int dumpTelemetryCursor = -1;
            ushort dumpTelemetryFlags = 0;
            NativeArray<byte> dumpPayload = default;

            if (TryAcquirePathFunnelMutationGuard(TelemetryMutationGuardMask, out IDataVault guardVault))
            {
                try
                {
                    if (TryResolveTelemetryViews(
                            out NativeArray<PathFunnelTelemetryEntry> telemetry,
                            out NativeArray<PathFunnelRuntimeState> runtimeStateBuffer))
                    {
                        PathFunnelRuntimeState runtimeState = runtimeStateBuffer[0];
                        dumpRequested = runtimeState.DumpRequested != 0;
                        if (dumpRequested)
                        {
                            runtimeState.TelemetryFlags = (ushort)(runtimeState.TelemetryFlags & ~PathFunnelTelemetryFlags.BlackBoxDumpFailed);
                        }

                        dumpTelemetryFlags = runtimeState.TelemetryFlags;
                        dumpTelemetryCursor = WriteTelemetry(telemetry, ref runtimeState);
                        runtimeState.DumpRequested = 0;
                        runtimeState.TelemetryFlags = (ushort)(runtimeState.TelemetryFlags & ~PathFunnelTelemetryFlags.TransientFrameMask);
                        runtimeStateBuffer[0] = runtimeState;

                        if (dumpRequested)
                        {
                            dumpStaged = TryStageBlackBoxDump(telemetry, out dumpPayload, out dumpByteCount);
                            if (!dumpStaged)
                            {
                                runtimeState.TelemetryFlags = (ushort)(runtimeState.TelemetryFlags | PathFunnelTelemetryFlags.BlackBoxDumpFailed);
                                PatchTelemetryFlags(
                                    telemetry,
                                    dumpTelemetryCursor,
                                    (ushort)(dumpTelemetryFlags | PathFunnelTelemetryFlags.BlackBoxDumpFailed));
                                runtimeStateBuffer[0] = runtimeState;
                            }
                        }
                    }
                }
                finally
                {
                    ReleasePathFunnelMutationGuard(guardVault, TelemetryMutationGuardMask);
                }
            }

            try
            {
                if (dumpRequested &&
                    dumpStaged &&
                    !TryDumpBlackBox(dumpPayload, dumpByteCount))
                {
                    TryPatchBlackBoxDumpFailure(dumpTelemetryCursor, dumpTelemetryFlags);
                }
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref dumpPayload,
                    nameof(PathFunnelNavmeshRuntime),
                    BlackBoxDumpPayloadLabel);
            }

            LateFrameTickVoxelAStar();
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    _registeredColdTick = false;
                    _registeredFastTick = false;
                    _registeredLateFrame = false;
                    if (currentService != null && isActiveAndEnabled)
                        TryRegisterDispatcherTicks();
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    RebindDataVaultForLifecycle(currentService is IDataVault currentVault ? currentVault : null);
                    BootstrapPathFunnelCold();
                    BootstrapVoxelAStarCold();
                    break;
            }
        }

        private void RebindDataVaultForLifecycle(IDataVault currentVault)
        {
            if (ReferenceEquals(_dataVault, currentVault))
                return;

            ForceCompleteVoxelAStarJobsForTeardown();
            ReleaseVoxelAStarVaultHandles(_dataVault);
            ReleaseVaultHandles(_dataVault);
            ClearVoxelAStarVaultHandles();
            ClearVaultHandles();
            _dataVault = currentVault;
        }

        private void TryRegisterDispatcherTicks()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredColdTick)
                _registeredColdTick = GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Environment);

            if (!_registeredFastTick)
                _registeredFastTick = GlobalRegistry.TryRegisterFastTickable(this, PriorityLayer.Environment);

            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterDispatcherTicks()
        {
            if (_registeredFastTick)
            {
                GlobalRegistry.UnregisterFastTickable(this, PriorityLayer.Environment);
                _registeredFastTick = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }

            if (_registeredColdTick)
            {
                GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Environment);
                _registeredColdTick = false;
            }
        }

        /// <summary>
        /// Registers or replaces one active path corridor. Corridor cells are packed into an exact 500-bit WFC mask.
        /// </summary>
        /// <param name="pathId">Stable path identifier owned by the caller.</param>
        /// <param name="sectorHash">WFC outpost sector hash.</param>
        /// <param name="corridorCells">Cell indices the path passes through.</param>
        /// <param name="corridorCellCount">Number of valid entries in <paramref name="corridorCells"/>.</param>
        /// <param name="corridorHash">Caller-computed corridor hash for telemetry.</param>
        /// <returns>True when the path was tracked.</returns>
        public bool RegisterActivePath(
            uint pathId,
            ulong sectorHash,
            NativeArray<ushort> corridorCells,
            int corridorCellCount,
            uint corridorHash)
        {
            if (pathId == 0u ||
                sectorHash == 0UL ||
                corridorCellCount < 0)
            {
                return false;
            }

            if (corridorCellCount > 0 && (!corridorCells.IsCreated || corridorCellCount > corridorCells.Length))
                return false;

            if (!TryAcquirePathFunnelMutationGuard(ActivePathMutationGuardMask, out IDataVault guardVault))
                return false;

            try
            {
                if (!EnsureActivePathMutationViews(
                        out NativeArray<PathFunnelActivePath> activePaths,
                        out NativeArray<ulong> activePathCellMasks,
                        out NativeArray<PathFunnelRuntimeState> runtimeStateBuffer))
                {
                    return false;
                }

                PathFunnelRuntimeState runtimeState = runtimeStateBuffer[0];
                int activePathCount = math.clamp(runtimeState.ActivePathCount, 0, activePaths.Length);
                int pathIndex = FindPathIndex(activePaths, activePathCount, pathId);
                if (pathIndex < 0)
                {
                    if (activePathCount >= activePaths.Length)
                        return false;

                    pathIndex = activePathCount;
                    activePathCount++;
                }
                else
                {
                    DecrementInvalidatedCountIfNeeded(activePaths[pathIndex], ref runtimeState);
                }

                ClearPathCellMask(activePathCellMasks, pathIndex);
                int safeCellCount = math.min(corridorCellCount, PathFunnelConstants.WfcOutpostCellCount);
                int validCellCount = 0;
                for (int i = 0; i < safeCellCount; i++)
                {
                    if (SetPathCell(activePathCellMasks, pathIndex, corridorCells[i]))
                        validCellCount++;
                }

                PathFunnelActivePath path = default;
                path.SectorHash = sectorHash;
                path.PathId = pathId;
                path.CorridorHash = corridorHash;
                path.CellCount = (ushort)math.min(validCellCount, ushort.MaxValue);
                path.Flags = PathFunnelActivePathFlags.InUse;
                path.LastTouchedFrame = ResolveCurrentRuntimeFrame(in runtimeState);
                activePaths[pathIndex] = path;

                runtimeState.ActivePathCount = activePathCount;
                runtimeStateBuffer[0] = runtimeState;
                return true;
            }
            finally
            {
                ReleasePathFunnelMutationGuard(guardVault, ActivePathMutationGuardMask);
            }
        }

        /// <summary>
        /// Removes one active path from WFC invalidation tracking.
        /// </summary>
        /// <param name="pathId">Stable path identifier.</param>
        public void UnregisterActivePath(uint pathId)
        {
            if (!TryAcquirePathFunnelMutationGuard(ActivePathMutationGuardMask, out IDataVault guardVault))
                return;

            try
            {
                if (!EnsureActivePathMutationViews(
                        out NativeArray<PathFunnelActivePath> activePaths,
                        out NativeArray<ulong> activePathCellMasks,
                        out NativeArray<PathFunnelRuntimeState> runtimeStateBuffer))
                {
                    return;
                }

                PathFunnelRuntimeState runtimeState = runtimeStateBuffer[0];
                int activePathCount = math.clamp(runtimeState.ActivePathCount, 0, activePaths.Length);
                int pathIndex = FindPathIndex(activePaths, activePathCount, pathId);
                if (pathIndex < 0)
                    return;

                int lastIndex = activePathCount - 1;
                DecrementInvalidatedCountIfNeeded(activePaths[pathIndex], ref runtimeState);
                if (pathIndex != lastIndex)
                {
                    activePaths[pathIndex] = activePaths[lastIndex];
                    CopyPathCellMask(activePathCellMasks, lastIndex, pathIndex);
                }

                activePaths[lastIndex] = default;
                ClearPathCellMask(activePathCellMasks, lastIndex);
                runtimeState.ActivePathCount = math.max(0, lastIndex);
                runtimeStateBuffer[0] = runtimeState;
            }
            finally
            {
                ReleasePathFunnelMutationGuard(guardVault, ActivePathMutationGuardMask);
            }
        }

        /// <summary>
        /// Tests whether a tracked path was invalidated by a closed WFC door.
        /// </summary>
        /// <param name="pathId">Stable path identifier.</param>
        /// <returns>True when invalidated.</returns>
        public bool IsPathInvalidated(uint pathId)
        {
            if (!TryReadActivePathViews(
                    out NativeArray<PathFunnelActivePath> activePaths,
                    out NativeArray<PathFunnelRuntimeState> runtimeStateBuffer))
            {
                return false;
            }

            int activePathCount = math.clamp(runtimeStateBuffer[0].ActivePathCount, 0, activePaths.Length);
            int pathIndex = FindPathIndex(activePaths, activePathCount, pathId);
            return pathIndex >= 0 && (activePaths[pathIndex].Flags & PathFunnelActivePathFlags.Invalidated) != 0;
        }

        /// <summary>
        /// Reads one queued invalidation event without allocation.
        /// </summary>
        /// <param name="invalidation">Copied invalidation payload.</param>
        /// <returns>True when one payload was available.</returns>
        public bool TryDequeueInvalidation(out PathFunnelInvalidation invalidation)
        {
            invalidation = default;
            if (!TryAcquirePathFunnelMutationGuard(InvalidationMutationGuardMask, out IDataVault guardVault))
            {
                return false;
            }

            try
            {
                if (!EnsureInvalidationViews(
                        out NativeArray<PathFunnelInvalidation> invalidations,
                        out NativeArray<PathFunnelRuntimeState> runtimeStateBuffer))
                {
                    return false;
                }

                PathFunnelRuntimeState runtimeState = runtimeStateBuffer[0];
                int readCursor = ClampRingCursor(runtimeState.InvalidationReadCursor, invalidations.Length);
                int writeCursor = ClampRingCursor(runtimeState.InvalidationWriteCursor, invalidations.Length);
                if (readCursor == writeCursor)
                    return false;

                invalidation = invalidations[readCursor];
                runtimeState.InvalidationReadCursor = AdvanceRingCursor(readCursor, invalidations.Length);
                runtimeStateBuffer[0] = runtimeState;
                return true;
            }
            finally
            {
                ReleasePathFunnelMutationGuard(guardVault, InvalidationMutationGuardMask);
            }
        }

        /// <summary>
        /// Requests a black-box dump during the next late-frame pass.
        /// </summary>
        public void RequestBlackBoxDump()
        {
            if (!TryAcquirePathFunnelMutationGuard(RuntimeStateMutationGuardMask, out IDataVault guardVault))
                return;

            try
            {
                if (!EnsureRuntimeState(out NativeArray<PathFunnelRuntimeState> runtimeStateBuffer))
                    return;

                PathFunnelRuntimeState runtimeState = runtimeStateBuffer[0];
                runtimeState.DumpRequested = 1;
                runtimeStateBuffer[0] = runtimeState;
            }
            finally
            {
                ReleasePathFunnelMutationGuard(guardVault, RuntimeStateMutationGuardMask);
            }
        }

        private bool OpenOrAcquireVaultBuffersForOwnerRoute()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            int activePathCapacity = ResolveActivePathCapacity();
            int cellMaskCapacity = activePathCapacity * PathFunnelConstants.WfcCellMaskWordCount;
            int invalidationCapacity = ResolveInvalidationCapacity();

            return EnsureVaultBuffer(
                       vault,
                       BufferID.PathFunnelActivePaths,
                       activePathCapacity,
                       ref _activePathsHandle,
                       out _) &&
                   EnsureVaultBuffer(
                       vault,
                       BufferID.PathFunnelCellMasks,
                       cellMaskCapacity,
                       ref _activePathCellMasksHandle,
                       out _) &&
                   EnsureVaultBuffer(
                       vault,
                       BufferID.PathFunnelInvalidations,
                       invalidationCapacity,
                       ref _invalidationsHandle,
                       out _) &&
                   EnsureVaultBuffer(
                       vault,
                       BufferID.PathFunnelTelemetryRing,
                       PathFunnelConstants.TelemetryFrames,
                       ref _telemetryHandle,
                       out _) &&
                   EnsureVaultBuffer(
                       vault,
                       BufferID.PathFunnelRuntimeState,
                       1,
                       ref _runtimeStateHandle,
                       out _);
        }

        private bool BootstrapPathFunnelCold()
        {
            if (!OpenOrAcquireVaultBuffersForOwnerRoute())
            {
                _pathFunnelColdBootstrapped = false;
                _wfcGridHandle = default;
                return false;
            }

            _pathFunnelColdBootstrapped = true;
            RefreshWfcGridHandleCold();
            if (TryResolveVaultBuffer(in _runtimeStateHandle, BufferID.PathFunnelRuntimeState, 1, out NativeArray<PathFunnelRuntimeState> runtimeStateBuffer))
            {
                InitializeRuntimeState(
                    runtimeStateBuffer,
                    PathFunnelConstants.TelemetryFrames,
                    ResolveActivePathLengthForState(),
                    ResolveInvalidationLengthForState());
            }

            return true;
        }

        private void RefreshWfcGridHandleCold()
        {
            _wfcGridHandle = default;
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return;

            if (!vault.TryGetGenerationHandle<byte>(BufferID.WfcOutpostGrid, out VaultGenerationHandle<byte> handle) ||
                !IsVaultHandleForBuffer(in handle, BufferID.WfcOutpostGrid, SystemID.CoreDataVault))
            {
                _wfcGridHandle = default;
                return;
            }

            _wfcGridHandle = handle;
        }

        private void ClearVaultHandles()
        {
            _activePathsHandle = default;
            _activePathCellMasksHandle = default;
            _invalidationsHandle = default;
            _telemetryHandle = default;
            _runtimeStateHandle = default;
            _wfcGridHandle = default;
            _pathFunnelColdBootstrapped = false;
        }

        private int ResolveActivePathCapacity()
        {
            return math.clamp(_activePathCapacity, 1, MaxActivePathCapacity);
        }

        private int ResolveInvalidationCapacity()
        {
            return math.clamp(_invalidationCapacity, 2, MaxInvalidationCapacity);
        }

        private int ResolveActivePathLengthForState()
        {
            return TryResolveVaultBuffer(
                    in _activePathsHandle,
                    BufferID.PathFunnelActivePaths,
                    ResolveActivePathCapacity(),
                    out NativeArray<PathFunnelActivePath> activePaths)
                ? activePaths.Length
                : ResolveActivePathCapacity();
        }

        private int ResolveInvalidationLengthForState()
        {
            return TryResolveVaultBuffer(
                    in _invalidationsHandle,
                    BufferID.PathFunnelInvalidations,
                    ResolveInvalidationCapacity(),
                    out NativeArray<PathFunnelInvalidation> invalidations)
                ? invalidations.Length
                : ResolveInvalidationCapacity();
        }

        private static bool IsVaultHandleForBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            SystemID expectedSystemId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)expectedBufferId) &&
                   handle.SystemID == (uint)expectedSystemId &&
                   handle.Generation != 0u;
        }

        private bool TryResolveVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   IsOwnedVaultHandle(in handle, expectedBufferId) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private bool TryReadVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   IsOwnedVaultHandle(in handle, expectedBufferId) &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private bool TryReadExternalVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            SystemID expectedSystemId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   IsVaultHandleForBuffer(in handle, expectedBufferId, expectedSystemId) &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool EnsureVaultBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            ref VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null)
                return false;

            if (IsOwnedVaultHandle(in handle, bufferId) &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            VaultGenerationHandle<T> acquired = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.AIPathfinding,
                NativeArrayOptions.ClearMemory);
            if (!IsOwnedVaultHandle(in acquired, bufferId) ||
                !vault.TryResolveHandle(in acquired, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                if (IsOwnedVaultHandle(in acquired, bufferId))
                    vault.ReleaseBuffer(in acquired);

                return false;
            }

            handle = acquired;
            return true;
        }

        private void ReleaseVaultHandles(IDataVault vault)
        {
            if (vault == null)
                return;

            ReleaseVaultHandle(vault, BufferID.PathFunnelActivePaths, ref _activePathsHandle);
            ReleaseVaultHandle(vault, BufferID.PathFunnelCellMasks, ref _activePathCellMasksHandle);
            ReleaseVaultHandle(vault, BufferID.PathFunnelInvalidations, ref _invalidationsHandle);
            ReleaseVaultHandle(vault, BufferID.PathFunnelTelemetryRing, ref _telemetryHandle);
            ReleaseVaultHandle(vault, BufferID.PathFunnelRuntimeState, ref _runtimeStateHandle);
        }

        private static void ReleaseVaultHandle<T>(
            IDataVault vault,
            BufferID expectedBufferId,
            ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (IsOwnedVaultHandle(in handle, expectedBufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool IsOwnedVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId) where T : struct
        {
            return IsVaultHandleForBuffer(in handle, expectedBufferId, SystemID.AIPathfinding);
        }

        private bool TryAcquirePathFunnelMutationGuard(ulong mask, out IDataVault guardVault)
        {
            guardVault = null;
            IDataVault vault = _dataVault;
            if (vault == null ||
                mask == 0UL ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireMutationGuard(mask))
            {
                return false;
            }

            guardVault = vault;
            return true;
        }

        private static void ReleasePathFunnelMutationGuard(IDataVault guardVault, ulong mask)
        {
            guardVault?.ReleaseMutationGuard(mask);
        }

        private static ulong PathFunnelMutationGuardBit(BufferID bufferId)
        {
            return 1UL << ((int)bufferId & 31);
        }

        private bool EnsureMutationViews(
            out NativeArray<PathFunnelActivePath> activePaths,
            out NativeArray<ulong> activePathCellMasks,
            out NativeArray<PathFunnelInvalidation> invalidations,
            out NativeArray<PathFunnelRuntimeState> runtimeStateBuffer)
        {
            activePaths = default;
            activePathCellMasks = default;
            invalidations = default;
            runtimeStateBuffer = default;

            if (!_pathFunnelColdBootstrapped)
                return false;

            if (!TryResolveVaultBuffer(in _activePathsHandle, BufferID.PathFunnelActivePaths, ResolveActivePathCapacity(), out activePaths) ||
                !TryResolveVaultBuffer(
                    in _activePathCellMasksHandle,
                    BufferID.PathFunnelCellMasks,
                    ResolveActivePathCapacity() * PathFunnelConstants.WfcCellMaskWordCount,
                    out activePathCellMasks) ||
                !TryResolveVaultBuffer(in _invalidationsHandle, BufferID.PathFunnelInvalidations, ResolveInvalidationCapacity(), out invalidations) ||
                !TryResolveVaultBuffer(in _runtimeStateHandle, BufferID.PathFunnelRuntimeState, 1, out runtimeStateBuffer) ||
                activePathCellMasks.Length < activePaths.Length * PathFunnelConstants.WfcCellMaskWordCount ||
                invalidations.Length <= 0)
            {
                return false;
            }

            InitializeRuntimeState(runtimeStateBuffer, PathFunnelConstants.TelemetryFrames, activePaths.Length, invalidations.Length);
            return true;
        }

        private bool EnsureActivePathMutationViews(
            out NativeArray<PathFunnelActivePath> activePaths,
            out NativeArray<ulong> activePathCellMasks,
            out NativeArray<PathFunnelRuntimeState> runtimeStateBuffer)
        {
            activePaths = default;
            activePathCellMasks = default;
            runtimeStateBuffer = default;

            if (!_pathFunnelColdBootstrapped)
                return false;

            if (!TryResolveVaultBuffer(in _activePathsHandle, BufferID.PathFunnelActivePaths, ResolveActivePathCapacity(), out activePaths) ||
                !TryResolveVaultBuffer(
                    in _activePathCellMasksHandle,
                    BufferID.PathFunnelCellMasks,
                    ResolveActivePathCapacity() * PathFunnelConstants.WfcCellMaskWordCount,
                    out activePathCellMasks) ||
                !TryResolveVaultBuffer(in _runtimeStateHandle, BufferID.PathFunnelRuntimeState, 1, out runtimeStateBuffer) ||
                activePathCellMasks.Length < activePaths.Length * PathFunnelConstants.WfcCellMaskWordCount)
            {
                return false;
            }

            InitializeRuntimeState(runtimeStateBuffer, PathFunnelConstants.TelemetryFrames, activePaths.Length, ResolveInvalidationLengthForState());
            return true;
        }

        private bool TryReadRuntimeState(out NativeArray<PathFunnelRuntimeState> runtimeStateBuffer)
        {
            runtimeStateBuffer = default;
            return TryReadVaultBuffer(in _runtimeStateHandle, BufferID.PathFunnelRuntimeState, 1, out runtimeStateBuffer);
        }

        private bool EnsureRuntimeState(out NativeArray<PathFunnelRuntimeState> runtimeStateBuffer)
        {
            runtimeStateBuffer = default;
            if (!_pathFunnelColdBootstrapped)
                return false;

            if (!TryResolveVaultBuffer(in _runtimeStateHandle, BufferID.PathFunnelRuntimeState, 1, out runtimeStateBuffer))
                return false;

            InitializeRuntimeState(runtimeStateBuffer, PathFunnelConstants.TelemetryFrames, ResolveActivePathLengthForState(), ResolveInvalidationLengthForState());
            return true;
        }

        private bool TryResolveTelemetryViews(
            out NativeArray<PathFunnelTelemetryEntry> telemetry,
            out NativeArray<PathFunnelRuntimeState> runtimeStateBuffer)
        {
            telemetry = default;
            runtimeStateBuffer = default;
            if (!_pathFunnelColdBootstrapped)
                return false;

            if (!TryResolveVaultBuffer(in _telemetryHandle, BufferID.PathFunnelTelemetryRing, PathFunnelConstants.TelemetryFrames, out telemetry) ||
                !TryResolveVaultBuffer(in _runtimeStateHandle, BufferID.PathFunnelRuntimeState, 1, out runtimeStateBuffer))
            {
                return false;
            }

            InitializeRuntimeState(runtimeStateBuffer, ResolveTelemetryRingLength(telemetry), ResolveActivePathLengthForState(), ResolveInvalidationLengthForState());
            return true;
        }

        private bool TryReadActivePathViews(
            out NativeArray<PathFunnelActivePath> activePaths,
            out NativeArray<PathFunnelRuntimeState> runtimeStateBuffer)
        {
            activePaths = default;
            runtimeStateBuffer = default;
            if (!TryReadVaultBuffer(in _activePathsHandle, BufferID.PathFunnelActivePaths, ResolveActivePathCapacity(), out activePaths) ||
                !TryReadVaultBuffer(in _runtimeStateHandle, BufferID.PathFunnelRuntimeState, 1, out runtimeStateBuffer))
                return false;
            return true;
        }

        private bool EnsureInvalidationViews(
            out NativeArray<PathFunnelInvalidation> invalidations,
            out NativeArray<PathFunnelRuntimeState> runtimeStateBuffer)
        {
            invalidations = default;
            runtimeStateBuffer = default;
            if (!_pathFunnelColdBootstrapped)
                return false;

            if (!TryResolveVaultBuffer(in _invalidationsHandle, BufferID.PathFunnelInvalidations, ResolveInvalidationCapacity(), out invalidations) ||
                !TryResolveVaultBuffer(in _runtimeStateHandle, BufferID.PathFunnelRuntimeState, 1, out runtimeStateBuffer))
                return false;

            InitializeRuntimeState(runtimeStateBuffer, PathFunnelConstants.TelemetryFrames, ResolveActivePathLengthForState(), invalidations.Length);
            return true;
        }

        private void InitializeRuntimeState(
            NativeArray<PathFunnelRuntimeState> runtimeStateBuffer,
            int telemetryLength,
            int activePathLength,
            int invalidationLength)
        {
            PathFunnelRuntimeState runtimeState = runtimeStateBuffer[0];
            if (runtimeState.BuffersReady == 0)
            {
                runtimeState = default;
                runtimeState.BuffersReady = 1;
            }

            runtimeState.ActivePathCount = math.clamp(runtimeState.ActivePathCount, 0, math.max(1, activePathLength));
            runtimeState.InvalidationReadCursor = ClampRingCursor(runtimeState.InvalidationReadCursor, math.max(1, invalidationLength));
            runtimeState.InvalidationWriteCursor = ClampRingCursor(runtimeState.InvalidationWriteCursor, math.max(1, invalidationLength));
            runtimeState.TelemetryCursor = ClampRingCursor(runtimeState.TelemetryCursor, math.max(1, telemetryLength));
            runtimeState.VaultGeneration = _runtimeStateHandle.Generation;
            runtimeStateBuffer[0] = runtimeState;
        }

        private static uint AdvanceRuntimeFrame(ref PathFunnelRuntimeState runtimeState)
        {
            uint next = runtimeState.FrameCounter + 1u;
            if (next == 0u)
                next = 1u;

            runtimeState.FrameCounter = next;
            return next;
        }

        private static uint ResolveCurrentRuntimeFrame(in PathFunnelRuntimeState runtimeState)
        {
            return runtimeState.FrameCounter != 0u ? runtimeState.FrameCounter : 1u;
        }

        private bool TryResolveWfcGrid(out NativeArray<byte> wfcGridBitmasks)
        {
            wfcGridBitmasks = default;
            return TryReadExternalVaultBuffer(
                in _wfcGridHandle,
                BufferID.WfcOutpostGrid,
                SystemID.CoreDataVault,
                1,
                out wfcGridBitmasks);
        }

        private void ProcessWfcStateSignal(
            in WfcOutpostStateChangedSignal signal,
            NativeArray<byte> wfcGridBitmasks,
            NativeArray<PathFunnelActivePath> activePaths,
            NativeArray<ulong> activePathCellMasks,
            NativeArray<PathFunnelInvalidation> invalidations,
            ref PathFunnelRuntimeState runtimeState)
        {
            if (signal.SectorHash == 0UL || signal.CellIndex >= PathFunnelConstants.WfcOutpostCellCount)
                return;

            byte previousFlags = (byte)(signal.PreviousFlags & PathFunnelConstants.WfcMutableFlagMask);
            byte currentFlags = ResolveCurrentCellFlags(in signal, wfcGridBitmasks, ref runtimeState);
            bool wasOpen = (previousFlags & PathFunnelConstants.WfcDoorOpenFlag) != 0;
            bool isOpen = (currentFlags & PathFunnelConstants.WfcDoorOpenFlag) != 0;
            if (!wasOpen || isOpen)
                return;

            InvalidatePathsThroughCell(
                signal.SectorHash,
                signal.CellIndex,
                previousFlags,
                currentFlags,
                signal.Frame,
                activePaths,
                activePathCellMasks,
                invalidations,
                ref runtimeState);
        }

        private static byte ResolveCurrentCellFlags(
            in WfcOutpostStateChangedSignal signal,
            NativeArray<byte> wfcGridBitmasks,
            ref PathFunnelRuntimeState runtimeState)
        {
            byte signalFlags = (byte)(signal.CurrentFlags & PathFunnelConstants.WfcMutableFlagMask);
            if (wfcGridBitmasks.IsCreated && signal.CellIndex < wfcGridBitmasks.Length)
            {
                byte vaultFlags = (byte)(wfcGridBitmasks[signal.CellIndex] & PathFunnelConstants.WfcMutableFlagMask);
                if (vaultFlags != signalFlags)
                {
                    runtimeState.TelemetryFlags = (ushort)(
                        runtimeState.TelemetryFlags |
                        PathFunnelTelemetryFlags.WfcVaultSignalMismatch);
                }
            }

            return signalFlags;
        }

        private static void InvalidatePathsThroughCell(
            ulong sectorHash,
            ushort cellIndex,
            byte previousFlags,
            byte currentFlags,
            uint frame,
            NativeArray<PathFunnelActivePath> activePaths,
            NativeArray<ulong> activePathCellMasks,
            NativeArray<PathFunnelInvalidation> invalidations,
            ref PathFunnelRuntimeState runtimeState)
        {
            int word = cellIndex >> 6;
            ulong bit = 1UL << (cellIndex & 63);
            int activePathCount = math.clamp(runtimeState.ActivePathCount, 0, activePaths.Length);
            for (int pathIndex = 0; pathIndex < activePathCount; pathIndex++)
            {
                PathFunnelActivePath path = activePaths[pathIndex];
                if ((path.Flags & PathFunnelActivePathFlags.InUse) == 0 ||
                    path.SectorHash != sectorHash)
                {
                    continue;
                }

                int wordIndex = PathMaskWordIndex(pathIndex, word);
                if (wordIndex < 0 || wordIndex >= activePathCellMasks.Length ||
                    (activePathCellMasks[wordIndex] & bit) == 0UL)
                {
                    continue;
                }

                if ((path.Flags & PathFunnelActivePathFlags.Invalidated) != 0)
                    continue;

                if (runtimeState.InvalidatedPathCount < ushort.MaxValue)
                    runtimeState.InvalidatedPathCount++;

                path.Flags = (ushort)(path.Flags | PathFunnelActivePathFlags.Invalidated);
                path.InvalidatedFrame = frame;
                activePaths[pathIndex] = path;
                runtimeState.PathInvalidationCount++;
                runtimeState.LastSectorHash = sectorHash;
                runtimeState.LastPathId = path.PathId;
                runtimeState.LastCorridorHash = path.CorridorHash;
                runtimeState.LastCellIndex = cellIndex;
                EnqueueInvalidation(in path, cellIndex, previousFlags, currentFlags, frame, invalidations, ref runtimeState);
            }
        }

        private static void EnqueueInvalidation(
            in PathFunnelActivePath path,
            ushort cellIndex,
            byte previousFlags,
            byte currentFlags,
            uint frame,
            NativeArray<PathFunnelInvalidation> invalidations,
            ref PathFunnelRuntimeState runtimeState)
        {
            if (!invalidations.IsCreated || invalidations.Length <= 0)
                return;

            int readCursor = ClampRingCursor(runtimeState.InvalidationReadCursor, invalidations.Length);
            int writeCursor = ClampRingCursor(runtimeState.InvalidationWriteCursor, invalidations.Length);
            int next = AdvanceRingCursor(writeCursor, invalidations.Length);
            if (next == readCursor)
                readCursor = AdvanceRingCursor(readCursor, invalidations.Length);

            invalidations[writeCursor] = new PathFunnelInvalidation
            {
                SectorHash = path.SectorHash,
                PathId = path.PathId,
                CorridorHash = path.CorridorHash,
                Frame = frame,
                CellIndex = cellIndex,
                Flags = path.Flags,
                PreviousCellFlags = previousFlags,
                CurrentCellFlags = currentFlags
            };
            runtimeState.InvalidationReadCursor = readCursor;
            runtimeState.InvalidationWriteCursor = next;
        }

        private static int FindPathIndex(NativeArray<PathFunnelActivePath> activePaths, int activePathCount, uint pathId)
        {
            for (int i = 0; i < activePathCount; i++)
            {
                PathFunnelActivePath path = activePaths[i];
                if ((path.Flags & PathFunnelActivePathFlags.InUse) != 0 && path.PathId == pathId)
                    return i;
            }

            return -1;
        }

        private static bool SetPathCell(NativeArray<ulong> activePathCellMasks, int pathIndex, ushort cellIndex)
        {
            if (cellIndex >= PathFunnelConstants.WfcOutpostCellCount)
                return false;

            int word = cellIndex >> 6;
            int wordIndex = PathMaskWordIndex(pathIndex, word);
            if (wordIndex < 0 || wordIndex >= activePathCellMasks.Length)
                return false;

            ulong bit = 1UL << (cellIndex & 63);
            ulong wordValue = activePathCellMasks[wordIndex];
            if ((wordValue & bit) != 0UL)
                return false;

            activePathCellMasks[wordIndex] = wordValue | bit;
            return true;
        }

        private static void ClearPathCellMask(NativeArray<ulong> activePathCellMasks, int pathIndex)
        {
            for (int word = 0; word < PathFunnelConstants.WfcCellMaskWordCount; word++)
            {
                int wordIndex = PathMaskWordIndex(pathIndex, word);
                if (wordIndex >= 0 && wordIndex < activePathCellMasks.Length)
                    activePathCellMasks[wordIndex] = 0UL;
            }
        }

        private static void CopyPathCellMask(NativeArray<ulong> activePathCellMasks, int sourcePathIndex, int destinationPathIndex)
        {
            for (int word = 0; word < PathFunnelConstants.WfcCellMaskWordCount; word++)
            {
                int sourceWord = PathMaskWordIndex(sourcePathIndex, word);
                int destinationWord = PathMaskWordIndex(destinationPathIndex, word);
                if (sourceWord >= 0 &&
                    destinationWord >= 0 &&
                    sourceWord < activePathCellMasks.Length &&
                    destinationWord < activePathCellMasks.Length)
                {
                    activePathCellMasks[destinationWord] = activePathCellMasks[sourceWord];
                }
            }
        }

        private static int PathMaskWordIndex(int pathIndex, int word)
        {
            if (pathIndex < 0 || word < 0 || word >= PathFunnelConstants.WfcCellMaskWordCount)
                return -1;

            return (pathIndex * PathFunnelConstants.WfcCellMaskWordCount) + word;
        }

        private static int ClampRingCursor(int cursor, int length)
        {
            if (length <= 0)
                return 0;

            if (cursor < 0)
                return 0;

            return cursor < length ? cursor : 0;
        }

        private static int AdvanceRingCursor(int cursor, int length)
        {
            if (length <= 1)
                return 0;

            int next = cursor + 1;
            return next < length ? next : 0;
        }

        private static void DecrementInvalidatedCountIfNeeded(
            in PathFunnelActivePath path,
            ref PathFunnelRuntimeState runtimeState)
        {
            if ((path.Flags & PathFunnelActivePathFlags.Invalidated) == 0 ||
                runtimeState.InvalidatedPathCount == 0)
            {
                return;
            }

            runtimeState.InvalidatedPathCount--;
        }

        private static int WriteTelemetry(NativeArray<PathFunnelTelemetryEntry> telemetry, ref PathFunnelRuntimeState runtimeState)
        {
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return -1;

            int telemetryLength = ResolveTelemetryRingLength(telemetry);
            int telemetryCursor = ClampRingCursor(runtimeState.TelemetryCursor, telemetryLength);
            telemetry[telemetryCursor] = new PathFunnelTelemetryEntry
            {
                Frame = ResolveCurrentRuntimeFrame(in runtimeState),
                PathInvalidationCount = runtimeState.PathInvalidationCount,
                LastSectorHash = runtimeState.LastSectorHash,
                LastPathId = runtimeState.LastPathId,
                LastCorridorHash = runtimeState.LastCorridorHash,
                LastCellIndex = runtimeState.LastCellIndex,
                ActivePathCount = (ushort)math.min(math.max(0, runtimeState.ActivePathCount), ushort.MaxValue),
                InvalidatedPathCount = runtimeState.InvalidatedPathCount,
                Flags = runtimeState.TelemetryFlags,
                Stress01 = 0f
            };
            runtimeState.TelemetryCursor = AdvanceRingCursor(telemetryCursor, telemetryLength);
            return telemetryCursor;
        }

        private static void PatchTelemetryFlags(NativeArray<PathFunnelTelemetryEntry> telemetry, int telemetryCursor, ushort flags)
        {
            if (!telemetry.IsCreated || telemetryCursor < 0 || telemetryCursor >= telemetry.Length)
                return;

            PathFunnelTelemetryEntry entry = telemetry[telemetryCursor];
            entry.Flags = flags;
            telemetry[telemetryCursor] = entry;
        }

        private bool TryPatchBlackBoxDumpFailure(int telemetryCursor, ushort writtenTelemetryFlags)
        {
            if (!TryAcquirePathFunnelMutationGuard(TelemetryMutationGuardMask, out IDataVault guardVault))
                return false;

            try
            {
                if (!TryResolveTelemetryViews(
                        out NativeArray<PathFunnelTelemetryEntry> telemetry,
                        out NativeArray<PathFunnelRuntimeState> runtimeStateBuffer))
                {
                    return false;
                }

                PathFunnelRuntimeState runtimeState = runtimeStateBuffer[0];
                runtimeState.TelemetryFlags = (ushort)(runtimeState.TelemetryFlags | PathFunnelTelemetryFlags.BlackBoxDumpFailed);
                runtimeStateBuffer[0] = runtimeState;
                PatchTelemetryFlags(
                    telemetry,
                    telemetryCursor,
                    (ushort)(writtenTelemetryFlags | PathFunnelTelemetryFlags.BlackBoxDumpFailed));
                return true;
            }
            finally
            {
                ReleasePathFunnelMutationGuard(guardVault, TelemetryMutationGuardMask);
            }
        }

        private static unsafe bool TryStageBlackBoxDump(
            NativeArray<PathFunnelTelemetryEntry> telemetry,
            out NativeArray<byte> payload,
            out int byteCount)
        {
            payload = default;
            byteCount = 0;
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return false;

            int telemetryLength = ResolveTelemetryRingLength(telemetry);
            int entryBytes = UnsafeUtility.SizeOf<PathFunnelTelemetryEntry>();
            byteCount = telemetryLength * entryBytes;
            if (telemetryLength <= 0 || byteCount <= 0)
                return false;

            try
            {
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(PathFunnelNavmeshRuntime),
                    BlackBoxDumpPayloadLabel,
                    NativeArrayOptions.UninitializedMemory);

                void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                void* destination = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payload);
                if (!UnsafeMemoryCopyGuard.SafeCopy(destination, payload.Length, source, byteCount))
                {
                    NativeFaultDumpWriter.DisposeTransientPayload(
                        ref payload,
                        nameof(PathFunnelNavmeshRuntime),
                        BlackBoxDumpPayloadLabel);
                    byteCount = 0;
                    return false;
                }

                return true;
            }
            catch
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(PathFunnelNavmeshRuntime),
                    BlackBoxDumpPayloadLabel);
                byteCount = 0;
                return false;
            }
        }

        private bool TryDumpBlackBox(NativeArray<byte> payload, int byteCount)
        {
            if (!payload.IsCreated || byteCount <= 0 || byteCount > payload.Length)
                return false;

            uint hash = 2166136261u ^ (uint)byteCount;
            for (int i = 0; i < byteCount; i++)
                hash = (hash ^ payload[i]) * 16777619u;

            _lastBlackBoxDumpHash = hash == 0u ? 2166136261u : hash;
            return NativeFaultDumpWriter.TryWriteAll(BlackBoxDumpRelativePath, payload, byteCount);
        }

        private static int ResolveTelemetryRingLength(NativeArray<PathFunnelTelemetryEntry> telemetry)
        {
            return telemetry.IsCreated
                ? math.min(telemetry.Length, PathFunnelConstants.TelemetryFrames)
                : 0;
        }
    }
}
