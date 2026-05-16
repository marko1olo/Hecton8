using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.AI.Pathfinding
{
    /// <summary>
    /// Runtime owner for WFC door-driven path invalidation and funnel black-box telemetry.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PathFunnelNavmeshRuntime : MonoBehaviour, IFastTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const int DefaultActivePathCapacity = 128;
        private const int DefaultInvalidationCapacity = 64;
        private const SystemID PathFunnelSystemId = (SystemID)384;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_PATH_FUNNEL_NAVMESH_FIXER.bin";

        [Header("Path Funnel Runtime")]
        [SerializeField, Min(1), Tooltip("Maximum tracked active AI corridors for WFC door invalidation.")]
        private int _activePathCapacity = DefaultActivePathCapacity;

        [SerializeField, Min(1), Tooltip("Bounded invalidation ring capacity consumed by path owners.")]
        private int _invalidationCapacity = DefaultInvalidationCapacity;

        private NativeArray<PathFunnelActivePath> _activePaths;
        private NativeArray<ulong> _activePathCellMasks;
        private NativeArray<PathFunnelInvalidation> _invalidations;
        private NativeArray<PathFunnelTelemetryEntry> _telemetry;
        private NativeArray<byte> _wfcGridBitmasks;
        private IDataVault _dataVault;
        private bool _registeredFastTick;
        private bool _registeredLateFrame;
        private bool _registeredHotSwap;
        private int _activePathCount;
        private int _invalidationReadCursor;
        private int _invalidationWriteCursor;
        private int _telemetryCursor;
        private uint _pathInvalidationCount;
        private uint _lastPathId;
        private uint _lastCorridorHash;
        private ulong _lastSectorHash;
        private ushort _lastCellIndex;
        private ushort _invalidatedPathCount;
        private ushort _telemetryFlags;
        private bool _dumpRequested;

        /// <summary>Total WFC-driven path invalidations observed by this runtime.</summary>
        public uint PathInvalidationCount => _pathInvalidationCount;

        private void OnEnable()
        {
            EnsureNativeBuffers();
            _dataVault = GlobalRegistry.DataVault;
            RefreshWfcGridBuffer();
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

            ReleaseNativeBuffers();
        }

        /// <inheritdoc />
        public void FastTick(float deltaTime)
        {
            RefreshWfcGridBuffer();
            ReadOnlySpan<WfcOutpostStateChangedSignal> signals = SignalBus<WfcOutpostStateChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
                ProcessWfcStateSignal(in signals[i]);
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            WriteTelemetry();
            if (_dumpRequested)
            {
                _dumpRequested = false;
                DumpBlackBox();
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
            _wfcGridBitmasks = default;
            RefreshWfcGridBuffer();
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
            if (pathId == 0u || sectorHash == 0UL || !_activePaths.IsCreated || !_activePathCellMasks.IsCreated)
                return false;

            if (corridorCellCount > 0 && (!corridorCells.IsCreated || corridorCellCount > corridorCells.Length))
                return false;

            int pathIndex = FindPathIndex(pathId);
            if (pathIndex < 0)
            {
                if (_activePathCount >= _activePaths.Length)
                    return false;

                pathIndex = _activePathCount++;
            }

            ClearPathCellMask(pathIndex);
            int safeCellCount = math.min(corridorCellCount, PathFunnelConstants.WfcOutpostCellCount);
            for (int i = 0; i < safeCellCount; i++)
                SetPathCell(pathIndex, corridorCells[i]);

            PathFunnelActivePath path = default;
            path.SectorHash = sectorHash;
            path.PathId = pathId;
            path.CorridorHash = corridorHash;
            path.CellCount = (ushort)math.min(safeCellCount, ushort.MaxValue);
            path.Flags = PathFunnelActivePathFlags.InUse;
            path.LastTouchedFrame = (uint)Time.frameCount;
            _activePaths[pathIndex] = path;
            return true;
        }

        /// <summary>
        /// Removes one active path from WFC invalidation tracking.
        /// </summary>
        /// <param name="pathId">Stable path identifier.</param>
        public void UnregisterActivePath(uint pathId)
        {
            int pathIndex = FindPathIndex(pathId);
            if (pathIndex < 0)
                return;

            int lastIndex = _activePathCount - 1;
            if (pathIndex != lastIndex)
            {
                _activePaths[pathIndex] = _activePaths[lastIndex];
                CopyPathCellMask(lastIndex, pathIndex);
            }

            _activePaths[lastIndex] = default;
            ClearPathCellMask(lastIndex);
            _activePathCount = math.max(0, lastIndex);
        }

        /// <summary>
        /// Tests whether a tracked path was invalidated by a closed WFC door.
        /// </summary>
        /// <param name="pathId">Stable path identifier.</param>
        /// <returns>True when invalidated.</returns>
        public bool IsPathInvalidated(uint pathId)
        {
            int pathIndex = FindPathIndex(pathId);
            if (pathIndex < 0)
                return false;

            return (_activePaths[pathIndex].Flags & PathFunnelActivePathFlags.Invalidated) != 0;
        }

        /// <summary>
        /// Reads one queued invalidation event without allocation.
        /// </summary>
        /// <param name="invalidation">Copied invalidation payload.</param>
        /// <returns>True when one payload was available.</returns>
        public bool TryReadInvalidation(out PathFunnelInvalidation invalidation)
        {
            invalidation = default;
            if (!_invalidations.IsCreated || _invalidationReadCursor == _invalidationWriteCursor)
                return false;

            invalidation = _invalidations[_invalidationReadCursor];
            _invalidationReadCursor = (_invalidationReadCursor + 1) % _invalidations.Length;
            return true;
        }

        /// <summary>
        /// Requests a black-box dump during the next late-frame pass.
        /// </summary>
        public void RequestBlackBoxDump()
        {
            _dumpRequested = true;
        }

        private void EnsureNativeBuffers()
        {
            int activePathCapacity = math.max(1, _activePathCapacity);
            int invalidationCapacity = math.max(1, _invalidationCapacity);
            if (!_activePaths.IsCreated)
            {
                _activePaths = H8Memory.Allocate<PathFunnelActivePath>(
                    activePathCapacity,
                    PathFunnelSystemId,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<PathFunnelActivePath>[activePathCapacity] - active funnel path records - owner: PathFunnelNavmeshRuntime
            }

            if (!_activePathCellMasks.IsCreated)
            {
                _activePathCellMasks = H8Memory.Allocate<ulong>(
                    activePathCapacity * PathFunnelConstants.WfcCellMaskWordCount,
                    PathFunnelSystemId,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ulong>[activePathCapacity*8] - exact WFC corridor bitsets - owner: PathFunnelNavmeshRuntime
            }

            if (!_invalidations.IsCreated)
            {
                _invalidations = H8Memory.Allocate<PathFunnelInvalidation>(
                    invalidationCapacity,
                    PathFunnelSystemId,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<PathFunnelInvalidation>[invalidationCapacity] - bounded invalidation ring - owner: PathFunnelNavmeshRuntime
            }

            if (!_telemetry.IsCreated)
            {
                _telemetry = H8Memory.Allocate<PathFunnelTelemetryEntry>(
                    PathFunnelConstants.TelemetryFrames,
                    PathFunnelSystemId,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<PathFunnelTelemetryEntry>[300] - path funnel black-box ring - owner: PathFunnelNavmeshRuntime
            }
        }

        private void ReleaseNativeBuffers()
        {
            H8Memory.Release(ref _activePaths, PathFunnelSystemId);
            H8Memory.Release(ref _activePathCellMasks, PathFunnelSystemId);
            H8Memory.Release(ref _invalidations, PathFunnelSystemId);
            H8Memory.Release(ref _telemetry, PathFunnelSystemId);
            _wfcGridBitmasks = default;
            _dataVault = null;
            _activePathCount = 0;
            _invalidationReadCursor = 0;
            _invalidationWriteCursor = 0;
            _telemetryCursor = 0;
            _invalidatedPathCount = 0;
        }

        private void RefreshWfcGridBuffer()
        {
            IDataVault dataVault = _dataVault;
            if (dataVault == null)
            {
                _wfcGridBitmasks = default;
                return;
            }

            if (_wfcGridBitmasks.IsCreated)
                return;

            if (dataVault.TryGetBuffer(BufferID.WfcOutpostGrid, out NativeArray<byte> buffer))
                _wfcGridBitmasks = buffer;
        }

        private void ProcessWfcStateSignal(in WfcOutpostStateChangedSignal signal)
        {
            if (signal.SectorHash == 0UL || signal.CellIndex >= PathFunnelConstants.WfcOutpostCellCount)
                return;

            byte currentFlags = ResolveCurrentCellFlags(in signal);
            bool wasOpen = (signal.PreviousFlags & PathFunnelConstants.WfcDoorOpenFlag) != 0;
            bool isOpen = (currentFlags & PathFunnelConstants.WfcDoorOpenFlag) != 0;
            if (!wasOpen || isOpen)
                return;

            InvalidatePathsThroughCell(signal.SectorHash, signal.CellIndex, signal.PreviousFlags, currentFlags, signal.Frame);
        }

        private byte ResolveCurrentCellFlags(in WfcOutpostStateChangedSignal signal)
        {
            if (_wfcGridBitmasks.IsCreated && signal.CellIndex < _wfcGridBitmasks.Length)
                return _wfcGridBitmasks[signal.CellIndex];

            return signal.CurrentFlags;
        }

        private void InvalidatePathsThroughCell(ulong sectorHash, ushort cellIndex, byte previousFlags, byte currentFlags, uint frame)
        {
            int word = cellIndex >> 6;
            ulong bit = 1UL << (cellIndex & 63);
            for (int pathIndex = 0; pathIndex < _activePathCount; pathIndex++)
            {
                PathFunnelActivePath path = _activePaths[pathIndex];
                if ((path.Flags & PathFunnelActivePathFlags.InUse) == 0 ||
                    path.SectorHash != sectorHash)
                {
                    continue;
                }

                int wordIndex = PathMaskWordIndex(pathIndex, word);
                if (wordIndex < 0 || wordIndex >= _activePathCellMasks.Length ||
                    (_activePathCellMasks[wordIndex] & bit) == 0UL)
                {
                    continue;
                }

                if ((path.Flags & PathFunnelActivePathFlags.Invalidated) == 0)
                    _invalidatedPathCount++;

                path.Flags = (ushort)(path.Flags | PathFunnelActivePathFlags.Invalidated);
                path.InvalidatedFrame = frame;
                _activePaths[pathIndex] = path;
                _pathInvalidationCount++;
                _lastSectorHash = sectorHash;
                _lastPathId = path.PathId;
                _lastCorridorHash = path.CorridorHash;
                _lastCellIndex = cellIndex;
                EnqueueInvalidation(in path, cellIndex, previousFlags, currentFlags, frame);
            }
        }

        private void EnqueueInvalidation(in PathFunnelActivePath path, ushort cellIndex, byte previousFlags, byte currentFlags, uint frame)
        {
            if (!_invalidations.IsCreated || _invalidations.Length <= 0)
                return;

            int next = (_invalidationWriteCursor + 1) % _invalidations.Length;
            if (next == _invalidationReadCursor)
                _invalidationReadCursor = (_invalidationReadCursor + 1) % _invalidations.Length;

            _invalidations[_invalidationWriteCursor] = new PathFunnelInvalidation
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
            _invalidationWriteCursor = next;
        }

        private int FindPathIndex(uint pathId)
        {
            for (int i = 0; i < _activePathCount; i++)
            {
                PathFunnelActivePath path = _activePaths[i];
                if ((path.Flags & PathFunnelActivePathFlags.InUse) != 0 && path.PathId == pathId)
                    return i;
            }

            return -1;
        }

        private void SetPathCell(int pathIndex, ushort cellIndex)
        {
            if (cellIndex >= PathFunnelConstants.WfcOutpostCellCount)
                return;

            int word = cellIndex >> 6;
            int wordIndex = PathMaskWordIndex(pathIndex, word);
            if (wordIndex < 0 || wordIndex >= _activePathCellMasks.Length)
                return;

            _activePathCellMasks[wordIndex] = _activePathCellMasks[wordIndex] | (1UL << (cellIndex & 63));
        }

        private void ClearPathCellMask(int pathIndex)
        {
            for (int word = 0; word < PathFunnelConstants.WfcCellMaskWordCount; word++)
            {
                int wordIndex = PathMaskWordIndex(pathIndex, word);
                if (wordIndex >= 0 && wordIndex < _activePathCellMasks.Length)
                    _activePathCellMasks[wordIndex] = 0UL;
            }
        }

        private void CopyPathCellMask(int sourcePathIndex, int destinationPathIndex)
        {
            for (int word = 0; word < PathFunnelConstants.WfcCellMaskWordCount; word++)
            {
                int sourceWord = PathMaskWordIndex(sourcePathIndex, word);
                int destinationWord = PathMaskWordIndex(destinationPathIndex, word);
                if (sourceWord >= 0 &&
                    destinationWord >= 0 &&
                    sourceWord < _activePathCellMasks.Length &&
                    destinationWord < _activePathCellMasks.Length)
                {
                    _activePathCellMasks[destinationWord] = _activePathCellMasks[sourceWord];
                }
            }
        }

        private static int PathMaskWordIndex(int pathIndex, int word)
        {
            if (pathIndex < 0 || word < 0 || word >= PathFunnelConstants.WfcCellMaskWordCount)
                return -1;

            return (pathIndex * PathFunnelConstants.WfcCellMaskWordCount) + word;
        }

        private void WriteTelemetry()
        {
            if (!_telemetry.IsCreated || _telemetry.Length <= 0)
                return;

            _telemetry[_telemetryCursor] = new PathFunnelTelemetryEntry
            {
                Frame = (uint)Time.frameCount,
                PathInvalidationCount = _pathInvalidationCount,
                LastSectorHash = _lastSectorHash,
                LastPathId = _lastPathId,
                LastCorridorHash = _lastCorridorHash,
                LastCellIndex = _lastCellIndex,
                ActivePathCount = (ushort)math.min(_activePathCount, ushort.MaxValue),
                InvalidatedPathCount = _invalidatedPathCount,
                Flags = _telemetryFlags,
                Stress01 = 0f
            };
            _telemetryCursor = (_telemetryCursor + 1) % _telemetry.Length;
        }

        private unsafe void DumpBlackBox()
        {
            if (!_telemetry.IsCreated)
                return;

            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string path = Path.Combine(root, DumpRelativePath);
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            int byteCount = UnsafeUtility.SizeOf<PathFunnelTelemetryEntry>() * _telemetry.Length;
            byte[] bytes = new byte[byteCount];
            fixed (byte* destination = bytes)
            {
                void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_telemetry);
                UnsafeUtility.MemCpy(destination, source, byteCount);
            }

            File.WriteAllBytes(path, bytes);
        }
    }
}
