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
        private VaultBufferHandle<PathFunnelActivePath> _activePathsHandle;
        private VaultBufferHandle<ulong> _activePathCellMasksHandle;
        private VaultBufferHandle<PathFunnelInvalidation> _invalidationsHandle;
        private VaultBufferHandle<PathFunnelTelemetryEntry> _telemetryHandle;
        private VaultBufferHandle<PathFunnelRuntimeState> _runtimeStateHandle;
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
            _dataVault = GlobalRegistry.DataVault;
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

            int telemetryCursor = WriteTelemetry(telemetry, ref runtimeState);
            runtimeState.DumpRequested = 0;
            runtimeStateBuffer[0] = runtimeState;

            if (dumpRequested && !TryDumpBlackBox(telemetry))
            {
                runtimeState.TelemetryFlags = (ushort)(runtimeState.TelemetryFlags | PathFunnelTelemetryFlags.BlackBoxDumpFailed);
                PatchTelemetryFlags(telemetry, telemetryCursor, runtimeState.TelemetryFlags);
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
            for (int i = 0; i < safeCellCount; i++)
                SetPathCell(activePathCellMasks, pathIndex, corridorCells[i]);

            PathFunnelActivePath path = default;
            path.SectorHash = sectorHash;
            path.PathId = pathId;
            path.CorridorHash = corridorHash;
            path.CellCount = (ushort)math.min(safeCellCount, ushort.MaxValue);
            path.Flags = PathFunnelActivePathFlags.InUse;
            path.LastTouchedFrame = (uint)Time.frameCount;
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

            int activePathCapacity = math.clamp(_activePathCapacity, 1, MaxActivePathCapacity);
            int cellMaskCapacity = activePathCapacity * PathFunnelConstants.WfcCellMaskWordCount;
            int invalidationCapacity = math.clamp(_invalidationCapacity, 2, MaxInvalidationCapacity);

            if (!_activePathsHandle.IsCreated || _activePathsHandle.Length < activePathCapacity)
            {
                _activePathsHandle = vault.GetBufferHandle<PathFunnelActivePath>(
                    BufferID.PathFunnelActivePaths,
                    activePathCapacity,
                    SystemID.AIPathfinding,
                    NativeArrayOptions.ClearMemory);
            }

            if (!_activePathCellMasksHandle.IsCreated || _activePathCellMasksHandle.Length < cellMaskCapacity)
            {
                _activePathCellMasksHandle = vault.GetBufferHandle<ulong>(
                    BufferID.PathFunnelCellMasks,
                    cellMaskCapacity,
                    SystemID.AIPathfinding,
                    NativeArrayOptions.ClearMemory);
            }

            if (!_invalidationsHandle.IsCreated || _invalidationsHandle.Length < invalidationCapacity)
            {
                _invalidationsHandle = vault.GetBufferHandle<PathFunnelInvalidation>(
                    BufferID.PathFunnelInvalidations,
                    invalidationCapacity,
                    SystemID.AIPathfinding,
                    NativeArrayOptions.ClearMemory);
            }

            if (!_telemetryHandle.IsCreated || _telemetryHandle.Length < PathFunnelConstants.TelemetryFrames)
            {
                _telemetryHandle = vault.GetBufferHandle<PathFunnelTelemetryEntry>(
                    BufferID.PathFunnelTelemetryRing,
                    PathFunnelConstants.TelemetryFrames,
                    SystemID.AIPathfinding,
                    NativeArrayOptions.ClearMemory);
            }

            if (!_runtimeStateHandle.IsCreated || _runtimeStateHandle.Length < 1)
            {
                _runtimeStateHandle = vault.GetBufferHandle<PathFunnelRuntimeState>(
                    BufferID.PathFunnelRuntimeState,
                    1,
                    SystemID.AIPathfinding,
                    NativeArrayOptions.ClearMemory);
            }

            return _activePathsHandle.IsCreated &&
                   _activePathCellMasksHandle.IsCreated &&
                   _invalidationsHandle.IsCreated &&
                   _telemetryHandle.IsCreated &&
                   _runtimeStateHandle.IsCreated;
        }

        private void ClearVaultHandles()
        {
            _activePathsHandle = default;
            _activePathCellMasksHandle = default;
            _invalidationsHandle = default;
            _telemetryHandle = default;
            _runtimeStateHandle = default;
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

            IDataVault vault = _dataVault;
            activePaths = _activePathsHandle.Resolve(vault);
            activePathCellMasks = _activePathCellMasksHandle.Resolve(vault);
            invalidations = _invalidationsHandle.Resolve(vault);
            runtimeStateBuffer = _runtimeStateHandle.Resolve(vault);
            if (!activePaths.IsCreated ||
                !activePathCellMasks.IsCreated ||
                !invalidations.IsCreated ||
                !runtimeStateBuffer.IsCreated ||
                activePaths.Length <= 0 ||
                activePathCellMasks.Length < activePaths.Length * PathFunnelConstants.WfcCellMaskWordCount ||
                invalidations.Length <= 0 ||
                runtimeStateBuffer.Length <= 0)
            {
                return false;
            }

            InitializeRuntimeState(runtimeStateBuffer, PathFunnelConstants.TelemetryFrames);
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

            IDataVault vault = _dataVault;
            activePaths = _activePathsHandle.Resolve(vault);
            activePathCellMasks = _activePathCellMasksHandle.Resolve(vault);
            runtimeStateBuffer = _runtimeStateHandle.Resolve(vault);
            if (!activePaths.IsCreated ||
                !activePathCellMasks.IsCreated ||
                !runtimeStateBuffer.IsCreated ||
                activePaths.Length <= 0 ||
                activePathCellMasks.Length < activePaths.Length * PathFunnelConstants.WfcCellMaskWordCount ||
                runtimeStateBuffer.Length <= 0)
            {
                return false;
            }

            InitializeRuntimeState(runtimeStateBuffer, PathFunnelConstants.TelemetryFrames);
            return true;
        }

        private bool TryResolveRuntimeState(out NativeArray<PathFunnelRuntimeState> runtimeStateBuffer)
        {
            runtimeStateBuffer = default;
            if (!EnsureVaultBuffers())
                return false;

            runtimeStateBuffer = _runtimeStateHandle.Resolve(_dataVault);
            if (!runtimeStateBuffer.IsCreated || runtimeStateBuffer.Length <= 0)
                return false;

            InitializeRuntimeState(runtimeStateBuffer, PathFunnelConstants.TelemetryFrames);
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

            telemetry = _telemetryHandle.Resolve(_dataVault);
            runtimeStateBuffer = _runtimeStateHandle.Resolve(_dataVault);
            if (!telemetry.IsCreated ||
                !runtimeStateBuffer.IsCreated ||
                telemetry.Length < PathFunnelConstants.TelemetryFrames ||
                runtimeStateBuffer.Length <= 0)
            {
                return false;
            }

            InitializeRuntimeState(runtimeStateBuffer, telemetry.Length);
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

            activePaths = _activePathsHandle.Resolve(_dataVault);
            runtimeStateBuffer = _runtimeStateHandle.Resolve(_dataVault);
            if (!activePaths.IsCreated || !runtimeStateBuffer.IsCreated || activePaths.Length <= 0 || runtimeStateBuffer.Length <= 0)
                return false;

            InitializeRuntimeState(runtimeStateBuffer, PathFunnelConstants.TelemetryFrames);
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

            invalidations = _invalidationsHandle.Resolve(_dataVault);
            runtimeStateBuffer = _runtimeStateHandle.Resolve(_dataVault);
            if (!invalidations.IsCreated || !runtimeStateBuffer.IsCreated || invalidations.Length <= 0 || runtimeStateBuffer.Length <= 0)
                return false;

            InitializeRuntimeState(runtimeStateBuffer, PathFunnelConstants.TelemetryFrames);
            return true;
        }

        private void InitializeRuntimeState(NativeArray<PathFunnelRuntimeState> runtimeStateBuffer, int telemetryLength)
        {
            PathFunnelRuntimeState runtimeState = runtimeStateBuffer[0];
            if (runtimeState.BuffersReady == 0)
            {
                runtimeState = default;
                runtimeState.BuffersReady = 1;
            }

            runtimeState.ActivePathCount = math.clamp(runtimeState.ActivePathCount, 0, math.max(1, _activePathsHandle.Length));
            runtimeState.InvalidationReadCursor = ClampRingCursor(runtimeState.InvalidationReadCursor, math.max(1, _invalidationsHandle.Length));
            runtimeState.InvalidationWriteCursor = ClampRingCursor(runtimeState.InvalidationWriteCursor, math.max(1, _invalidationsHandle.Length));
            runtimeState.TelemetryCursor = ClampRingCursor(runtimeState.TelemetryCursor, math.max(1, telemetryLength));
            runtimeState.VaultGeneration = _runtimeStateHandle.GenerationID;
            runtimeStateBuffer[0] = runtimeState;
        }

        private bool TryResolveWfcGrid(out NativeArray<byte> wfcGridBitmasks)
        {
            wfcGridBitmasks = default;
            IDataVault vault = _dataVault;
            return vault != null && vault.TryGetBuffer(BufferID.WfcOutpostGrid, out wfcGridBitmasks);
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

            byte currentFlags = ResolveCurrentCellFlags(in signal, wfcGridBitmasks);
            bool wasOpen = (signal.PreviousFlags & PathFunnelConstants.WfcDoorOpenFlag) != 0;
            bool isOpen = (currentFlags & PathFunnelConstants.WfcDoorOpenFlag) != 0;
            if (!wasOpen || isOpen)
                return;

            InvalidatePathsThroughCell(
                signal.SectorHash,
                signal.CellIndex,
                signal.PreviousFlags,
                currentFlags,
                signal.Frame,
                activePaths,
                activePathCellMasks,
                invalidations,
                ref runtimeState);
        }

        private static byte ResolveCurrentCellFlags(in WfcOutpostStateChangedSignal signal, NativeArray<byte> wfcGridBitmasks)
        {
            if (wfcGridBitmasks.IsCreated && signal.CellIndex < wfcGridBitmasks.Length)
                return wfcGridBitmasks[signal.CellIndex];

            return signal.CurrentFlags;
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

        private static void SetPathCell(NativeArray<ulong> activePathCellMasks, int pathIndex, ushort cellIndex)
        {
            if (cellIndex >= PathFunnelConstants.WfcOutpostCellCount)
                return;

            int word = cellIndex >> 6;
            int wordIndex = PathMaskWordIndex(pathIndex, word);
            if (wordIndex < 0 || wordIndex >= activePathCellMasks.Length)
                return;

            activePathCellMasks[wordIndex] = activePathCellMasks[wordIndex] | (1UL << (cellIndex & 63));
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

            int telemetryCursor = ClampRingCursor(runtimeState.TelemetryCursor, telemetry.Length);
            telemetry[telemetryCursor] = new PathFunnelTelemetryEntry
            {
                Frame = (uint)Time.frameCount,
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
            runtimeState.TelemetryCursor = AdvanceRingCursor(telemetryCursor, telemetry.Length);
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

                int byteCount = UnsafeUtility.SizeOf<PathFunnelTelemetryEntry>() * telemetry.Length;
                void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                ReadOnlySpan<byte> telemetryBytes = new ReadOnlySpan<byte>(source, byteCount);
                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(telemetryBytes);
                    stream.Flush(true);
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
