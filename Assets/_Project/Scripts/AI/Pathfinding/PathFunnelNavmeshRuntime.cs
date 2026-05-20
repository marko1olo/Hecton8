using System;
using System.IO;
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
    public sealed class PathFunnelNavmeshRuntime : MonoBehaviour, IFastTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const int DefaultActivePathCapacity = 128;
        private const int DefaultInvalidationCapacity = 64;
        private const int MaxActivePathCapacity = 4096;
        private const int MaxInvalidationCapacity = 4096;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_PATH_FUNNEL_NAVMESH_FIXER.bin";

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
        private bool _registeredFastTick;
        private bool _registeredLateFrame;
        private bool _registeredHotSwap;

        /// <summary>Total WFC-driven path invalidations observed by this runtime.</summary>
        public uint PathInvalidationCount
        {
            get
            {
                if (!TryResolveRuntimeState(out NativeArray<PathFunnelRuntimeState> runtimeState))
                    return 0u;

                return runtimeState[0].PathInvalidationCount;
            }
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            if (_dataVault == null && GlobalDataVault.TryGetLatestCreated(out GlobalDataVault latestVault))
                _dataVault = latestVault;
            EnsureVaultBuffers();
            _registeredFastTick = GlobalRegistry.TryRegisterFastTickable(this, PriorityLayer.Environment);
            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void OnDisable()
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

            if (_registeredHotSwap)
            {
                GlobalRegistry.UnregisterHotSwapListener(this);
                _registeredHotSwap = false;
            }

            ReleaseVaultHandles(_dataVault);
            ClearVaultHandles();
            _dataVault = null;
        }

        /// <inheritdoc />
        public void FastTick(float deltaTime)
        {
            if (!TryResolveMutationViews(
                    out NativeArray<PathFunnelActivePath> activePaths,
                    out NativeArray<ulong> activePathCellMasks,
                    out NativeArray<PathFunnelInvalidation> invalidations,
                    out NativeArray<PathFunnelRuntimeState> runtimeStateBuffer))
            {
                return;
            }

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

        /// <inheritdoc />
        public void LateFrameTick()
        {
            if (!TryResolveTelemetryViews(
                    out NativeArray<PathFunnelTelemetryEntry> telemetry,
                    out NativeArray<PathFunnelRuntimeState> runtimeStateBuffer))
            {
                return;
            }

            PathFunnelRuntimeState runtimeState = runtimeStateBuffer[0];
            bool dumpRequested = runtimeState.DumpRequested != 0;
            if (dumpRequested)
            {
                runtimeState.TelemetryFlags = (ushort)(runtimeState.TelemetryFlags & ~PathFunnelTelemetryFlags.BlackBoxDumpFailed);
            }

            ushort writtenTelemetryFlags = runtimeState.TelemetryFlags;
            int telemetryCursor = WriteTelemetry(telemetry, ref runtimeState);
            runtimeState.DumpRequested = 0;
            runtimeState.TelemetryFlags = (ushort)(runtimeState.TelemetryFlags & ~PathFunnelTelemetryFlags.TransientFrameMask);
            runtimeStateBuffer[0] = runtimeState;

            if (dumpRequested && !TryDumpBlackBox(telemetry))
            {
                runtimeState.TelemetryFlags = (ushort)(runtimeState.TelemetryFlags | PathFunnelTelemetryFlags.BlackBoxDumpFailed);
                ushort patchedTelemetryFlags = (ushort)(writtenTelemetryFlags | PathFunnelTelemetryFlags.BlackBoxDumpFailed);
                PatchTelemetryFlags(telemetry, telemetryCursor, patchedTelemetryFlags);
                runtimeStateBuffer[0] = runtimeState;
            }
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            ReleaseVaultHandles(_dataVault);
            _dataVault = currentService as IDataVault;
            ClearVaultHandles();
            EnsureVaultBuffers();
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
                corridorCellCount < 0 ||
                !TryResolveActivePathMutationViews(
                    out NativeArray<PathFunnelActivePath> activePaths,
                    out NativeArray<ulong> activePathCellMasks,
                    out NativeArray<PathFunnelRuntimeState> runtimeStateBuffer))
            {
                return false;
            }

            if (corridorCellCount > 0 && (!corridorCells.IsCreated || corridorCellCount > corridorCells.Length))
                return false;

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

        /// <summary>
        /// Removes one active path from WFC invalidation tracking.
        /// </summary>
        /// <param name="pathId">Stable path identifier.</param>
        public void UnregisterActivePath(uint pathId)
        {
            if (!TryResolveActivePathMutationViews(
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

        /// <summary>
        /// Tests whether a tracked path was invalidated by a closed WFC door.
        /// </summary>
        /// <param name="pathId">Stable path identifier.</param>
        /// <returns>True when invalidated.</returns>
        public bool IsPathInvalidated(uint pathId)
        {
            if (!TryResolveActivePathViews(
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
        public bool TryReadInvalidation(out PathFunnelInvalidation invalidation)
        {
            invalidation = default;
            if (!TryResolveInvalidationViews(
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

        /// <summary>
        /// Requests a black-box dump during the next late-frame pass.
        /// </summary>
        public void RequestBlackBoxDump()
        {
            if (!TryResolveRuntimeState(out NativeArray<PathFunnelRuntimeState> runtimeStateBuffer))
                return;

            PathFunnelRuntimeState runtimeState = runtimeStateBuffer[0];
            runtimeState.DumpRequested = 1;
            runtimeStateBuffer[0] = runtimeState;
        }

        private bool EnsureVaultBuffers()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            int activePathCapacity = ResolveActivePathCapacity();
            int cellMaskCapacity = activePathCapacity * PathFunnelConstants.WfcCellMaskWordCount;
            int invalidationCapacity = ResolveInvalidationCapacity();

            return TryResolveOrAcquireVaultBuffer(
                       vault,
                       BufferID.PathFunnelActivePaths,
                       activePathCapacity,
                       ref _activePathsHandle,
                       out _) &&
                   TryResolveOrAcquireVaultBuffer(
                       vault,
                       BufferID.PathFunnelCellMasks,
                       cellMaskCapacity,
                       ref _activePathCellMasksHandle,
                       out _) &&
                   TryResolveOrAcquireVaultBuffer(
                       vault,
                       BufferID.PathFunnelInvalidations,
                       invalidationCapacity,
                       ref _invalidationsHandle,
                       out _) &&
                   TryResolveOrAcquireVaultBuffer(
                       vault,
                       BufferID.PathFunnelTelemetryRing,
                       PathFunnelConstants.TelemetryFrames,
                       ref _telemetryHandle,
                       out _) &&
                   TryResolveOrAcquireVaultBuffer(
                       vault,
                       BufferID.PathFunnelRuntimeState,
                       1,
                       ref _runtimeStateHandle,
                       out _);
        }

        private void ClearVaultHandles()
        {
            _activePathsHandle = default;
            _activePathCellMasksHandle = default;
            _invalidationsHandle = default;
            _telemetryHandle = default;
            _runtimeStateHandle = default;
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
            return TryResolveVaultBuffer(in _activePathsHandle, ResolveActivePathCapacity(), out NativeArray<PathFunnelActivePath> activePaths)
                ? activePaths.Length
                : ResolveActivePathCapacity();
        }

        private int ResolveInvalidationLengthForState()
        {
            return TryResolveVaultBuffer(in _invalidationsHandle, ResolveInvalidationCapacity(), out NativeArray<PathFunnelInvalidation> invalidations)
                ? invalidations.Length
                : ResolveInvalidationCapacity();
        }

        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private bool TryResolveVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   IsVaultHandleCreated(in handle) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool TryResolveOrAcquireVaultBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            ref VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null)
                return false;

            if (IsVaultHandleCreated(in handle) &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            VaultGenerationHandle<T> acquired = vault.GetGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.AIPathfinding,
                NativeArrayOptions.ClearMemory);
            if (!IsVaultHandleCreated(in acquired) ||
                !vault.TryResolveHandle(in acquired, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                return false;
            }

            handle = acquired;
            return true;
        }

        private void ReleaseVaultHandles(IDataVault vault)
        {
            if (vault == null)
                return;

            ReleaseVaultHandle(vault, ref _activePathsHandle);
            ReleaseVaultHandle(vault, ref _activePathCellMasksHandle);
            ReleaseVaultHandle(vault, ref _invalidationsHandle);
            ReleaseVaultHandle(vault, ref _telemetryHandle);
            ReleaseVaultHandle(vault, ref _runtimeStateHandle);
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (!IsVaultHandleCreated(in handle))
                return;

            vault.ReleaseBuffer(in handle);
            handle = default;
        }

        private bool TryResolveMutationViews(
            out NativeArray<PathFunnelActivePath> activePaths,
            out NativeArray<ulong> activePathCellMasks,
            out NativeArray<PathFunnelInvalidation> invalidations,
            out NativeArray<PathFunnelRuntimeState> runtimeStateBuffer)
        {
            activePaths = default;
            activePathCellMasks = default;
            invalidations = default;
            runtimeStateBuffer = default;

            if (!EnsureVaultBuffers())
                return false;

            if (!TryResolveVaultBuffer(in _activePathsHandle, ResolveActivePathCapacity(), out activePaths) ||
                !TryResolveVaultBuffer(
                    in _activePathCellMasksHandle,
                    ResolveActivePathCapacity() * PathFunnelConstants.WfcCellMaskWordCount,
                    out activePathCellMasks) ||
                !TryResolveVaultBuffer(in _invalidationsHandle, ResolveInvalidationCapacity(), out invalidations) ||
                !TryResolveVaultBuffer(in _runtimeStateHandle, 1, out runtimeStateBuffer) ||
                activePathCellMasks.Length < activePaths.Length * PathFunnelConstants.WfcCellMaskWordCount ||
                invalidations.Length <= 0)
            {
                return false;
            }

            InitializeRuntimeState(runtimeStateBuffer, PathFunnelConstants.TelemetryFrames, activePaths.Length, invalidations.Length);
            return true;
        }

        private bool TryResolveActivePathMutationViews(
            out NativeArray<PathFunnelActivePath> activePaths,
            out NativeArray<ulong> activePathCellMasks,
            out NativeArray<PathFunnelRuntimeState> runtimeStateBuffer)
        {
            activePaths = default;
            activePathCellMasks = default;
            runtimeStateBuffer = default;

            if (!EnsureVaultBuffers())
                return false;

            if (!TryResolveVaultBuffer(in _activePathsHandle, ResolveActivePathCapacity(), out activePaths) ||
                !TryResolveVaultBuffer(
                    in _activePathCellMasksHandle,
                    ResolveActivePathCapacity() * PathFunnelConstants.WfcCellMaskWordCount,
                    out activePathCellMasks) ||
                !TryResolveVaultBuffer(in _runtimeStateHandle, 1, out runtimeStateBuffer) ||
                activePathCellMasks.Length < activePaths.Length * PathFunnelConstants.WfcCellMaskWordCount)
            {
                return false;
            }

            InitializeRuntimeState(runtimeStateBuffer, PathFunnelConstants.TelemetryFrames, activePaths.Length, ResolveInvalidationLengthForState());
            return true;
        }

        private bool TryResolveRuntimeState(out NativeArray<PathFunnelRuntimeState> runtimeStateBuffer)
        {
            runtimeStateBuffer = default;
            if (!EnsureVaultBuffers())
                return false;

            if (!TryResolveVaultBuffer(in _runtimeStateHandle, 1, out runtimeStateBuffer))
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
            if (!EnsureVaultBuffers())
                return false;

            if (!TryResolveVaultBuffer(in _telemetryHandle, PathFunnelConstants.TelemetryFrames, out telemetry) ||
                !TryResolveVaultBuffer(in _runtimeStateHandle, 1, out runtimeStateBuffer))
            {
                return false;
            }

            InitializeRuntimeState(runtimeStateBuffer, ResolveTelemetryRingLength(telemetry), ResolveActivePathLengthForState(), ResolveInvalidationLengthForState());
            return true;
        }

        private bool TryResolveActivePathViews(
            out NativeArray<PathFunnelActivePath> activePaths,
            out NativeArray<PathFunnelRuntimeState> runtimeStateBuffer)
        {
            activePaths = default;
            runtimeStateBuffer = default;
            if (!EnsureVaultBuffers())
                return false;

            if (!TryResolveVaultBuffer(in _activePathsHandle, ResolveActivePathCapacity(), out activePaths) ||
                !TryResolveVaultBuffer(in _runtimeStateHandle, 1, out runtimeStateBuffer))
                return false;

            InitializeRuntimeState(runtimeStateBuffer, PathFunnelConstants.TelemetryFrames, activePaths.Length, ResolveInvalidationLengthForState());
            return true;
        }

        private bool TryResolveInvalidationViews(
            out NativeArray<PathFunnelInvalidation> invalidations,
            out NativeArray<PathFunnelRuntimeState> runtimeStateBuffer)
        {
            invalidations = default;
            runtimeStateBuffer = default;
            if (!EnsureVaultBuffers())
                return false;

            if (!TryResolveVaultBuffer(in _invalidationsHandle, ResolveInvalidationCapacity(), out invalidations) ||
                !TryResolveVaultBuffer(in _runtimeStateHandle, 1, out runtimeStateBuffer))
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
            IDataVault vault = _dataVault;
            if (vault == null ||
                !vault.TryGetGenerationHandle<byte>(BufferID.WfcOutpostGrid, out VaultGenerationHandle<byte> handle) ||
                !vault.TryResolveHandle(in handle, out wfcGridBitmasks))
            {
                return false;
            }

            return wfcGridBitmasks.IsCreated;
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

        private static unsafe bool TryDumpBlackBox(NativeArray<PathFunnelTelemetryEntry> telemetry)
        {
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return false;

            try
            {
                string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string path = Path.Combine(root, DumpRelativePath);
                string directory = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(directory))
                    return false;

                Directory.CreateDirectory(directory);

                int byteCount = UnsafeUtility.SizeOf<PathFunnelTelemetryEntry>() * ResolveTelemetryRingLength(telemetry);
                void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                ReadOnlySpan<byte> telemetryBytes = new ReadOnlySpan<byte>(source, byteCount);
                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(telemetryBytes);
                    stream.Flush(true);
                }

                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static int ResolveTelemetryRingLength(NativeArray<PathFunnelTelemetryEntry> telemetry)
        {
            return telemetry.IsCreated
                ? math.min(telemetry.Length, PathFunnelConstants.TelemetryFrames)
                : 0;
        }
    }
}
