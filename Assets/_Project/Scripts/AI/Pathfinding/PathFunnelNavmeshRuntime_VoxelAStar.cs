using System;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.AI.Pathfinding
{
    public sealed partial class PathFunnelNavmeshRuntime
    {
        private const int MaxVoxelPathRequestCapacity = 1024;
        private const int MaxVoxelPathResultCapacity = 1024;
        private const int MaxVoxelPathRawPathCapacity = 8192;
        private const int MaxVoxelPathWaypointCapacity = 4096;
        private const int MaxVoxelPathGridDimension = 96;
        private const int MaxVoxelPathProfileCapacity = 256;
        private static readonly double StopwatchTicksToMicros = System.Diagnostics.Stopwatch.Frequency > 0L
            ? 1000000.0d / System.Diagnostics.Stopwatch.Frequency
            : 0.0d;
        [Header("Voxel SDF A* Runtime")]
        [SerializeField, Min(2), Tooltip("Bounded native request ring for SHINOBU_304 voxel route requests.")]
        private int _voxelPathRequestCapacity = VoxelAStarConstants.DefaultRequestCapacity;

        [SerializeField, Min(2), Tooltip("Bounded native result slots for SHINOBU_304 voxel route results.")]
        private int _voxelPathResultCapacity = VoxelAStarConstants.DefaultResultCapacity;

        [SerializeField, Min(2), Tooltip("Maximum raw voxel nodes retained for one route.")]
        private int _voxelPathRawPathCapacity = VoxelAStarConstants.DefaultRawPathCapacity;

        [SerializeField, Min(2), Tooltip("Maximum smoothed AUP waypoints retained for one route.")]
        private int _voxelPathWaypointCapacity = MaxVoxelPathWaypointCapacity;

        [SerializeField, Min(2), Tooltip("Mock SDF grid X dimension until the real cave bake publishes a snapshot.")]
        private int _voxelPathGridX = VoxelAStarConstants.DefaultGridX;

        [SerializeField, Min(2), Tooltip("Mock SDF grid Y dimension until the real cave bake publishes a snapshot.")]
        private int _voxelPathGridY = VoxelAStarConstants.DefaultGridY;

        [SerializeField, Min(2), Tooltip("Mock SDF grid Z dimension until the real cave bake publishes a snapshot.")]
        private int _voxelPathGridZ = VoxelAStarConstants.DefaultGridZ;

        [SerializeField, Min(1), Tooltip("Cold native profile slots for fauna pathing profile authoring.")]
        private int _voxelPathProfileCapacity = 64;

        private VaultGenerationHandle<PathRequestDTO> _voxelPathRequestsHandle;
        private VaultGenerationHandle<VoxelPathRingState> _voxelPathRingStateHandle;
        private VaultGenerationHandle<VoxelPathSolverState> _voxelPathSolverStateHandle;
        private VaultGenerationHandle<VoxelPathNodeRecord> _voxelPathNodesHandle;
        private VaultGenerationHandle<VoxelPathHeapNode> _voxelPathOpenHeapHandle;
        private VaultGenerationHandle<int> _voxelPathHeapPositionsHandle;
        private VaultGenerationHandle<int> _voxelPathRawPathHandle;
        private VaultGenerationHandle<VoxelPathWaypointDTO> _voxelPathWaypointsHandle;
        private VaultGenerationHandle<PathResultDTO> _voxelPathResultsHandle;
        private VaultGenerationHandle<PathfindingTelemetryEntry> _voxelPathTelemetryHandle;
        private VaultGenerationHandle<VoxelAStarTuningDTO> _voxelPathTuningHandle;
        private VaultGenerationHandle<float> _voxelPathMockSdfHandle;
        private VaultGenerationHandle<VoxelSdfGridHeader> _voxelPathSdfHeaderHandle;
        private VaultGenerationHandle<VoxelPathingProfileDTO> _voxelPathSpeciesProfilesHandle;
        private VaultGenerationHandle<int> _voxelPathSpeciesProfileCountHandle;
        private VaultGenerationHandle<int> _voxelPathClosedDebugHandle;
        private JobHandle _voxelAStarEvaluateHandle;
        private JobHandle _voxelAStarSmoothHandle;
        private static int _voxelAStarScheduledJobCount;
        private bool _voxelAStarEvaluateScheduled;
        private bool _voxelAStarSmoothScheduled;
        private uint _voxelAStarFrame;
        private uint _voxelAStarLastDumpFrame;
        private uint _voxelAStarLastDumpHash;
        private uint _voxelAStarMockGridVersion = 1u;
        private long _voxelAStarEvaluateScheduleTicks;
        private long _voxelAStarSmoothScheduleTicks;
        private bool _voxelAStarColdBootstrapped;

        /// <summary>
        /// Enqueues one voxel SDF path request into the native ring.
        /// </summary>
        public bool TryEnqueueVoxelPathRequest(in PathRequestDTO request)
        {
            if (!_voxelAStarColdBootstrapped ||
                IsVoxelAStarJobActive() ||
                !TryResolveVaultBuffer(in _voxelPathRequestsHandle, BufferID.ShinobuVoxelPathRequests, ResolveVoxelRequestCapacity(), out NativeArray<PathRequestDTO> requests) ||
                !TryResolveVaultBuffer(in _voxelPathRingStateHandle, BufferID.ShinobuVoxelPathRingState, 1, out NativeArray<VoxelPathRingState> ringStateBuffer))
            {
                return false;
            }

            VoxelPathRingState ring = ringStateBuffer[0];
            int capacity = math.min(requests.Length, ResolveVoxelRequestCapacity());
            if (capacity <= 0)
                return false;

            ring.Capacity = capacity;
            ring.ReadCursor = ClampRingCursor(ring.ReadCursor, capacity);
            ring.WriteCursor = ClampRingCursor(ring.WriteCursor, capacity);
            ring.Count = math.clamp(ring.Count, 0, capacity);
            if (ring.Count >= capacity)
            {
                ring.DroppedRequests++;
                ringStateBuffer[0] = ring;
                return false;
            }

            requests[ring.WriteCursor] = request;
            if (TryResolveVaultBuffer(in _voxelPathResultsHandle, BufferID.ShinobuVoxelPathResults, ResolveVoxelResultCapacity(), out NativeArray<PathResultDTO> results))
                InvalidateTerminalVoxelPathResults(in request, results, _voxelAStarFrame);

            ring.WriteCursor = AdvanceRingCursor(ring.WriteCursor, capacity);
            ring.Count++;
            ring.AcceptedRequests++;
            ringStateBuffer[0] = ring;
            return true;
        }

        /// <summary>
        /// Reads the latest result matching a requester hash without mutating runtime state.
        /// </summary>
        public bool TryReadVoxelPathResult(uint requesterEntityHash, out PathResultDTO result)
        {
            result = default;
            if (requesterEntityHash == 0u ||
                IsVoxelAStarJobActive() ||
                !TryReadVaultBuffer(in _voxelPathResultsHandle, BufferID.ShinobuVoxelPathResults, ResolveVoxelResultCapacity(), out NativeArray<PathResultDTO> results))
            {
                return false;
            }

            bool found = false;
            for (int i = 0; i < results.Length; i++)
            {
                PathResultDTO candidate = results[i];
                if (candidate.RequesterEntityHash != requesterEntityHash || !IsTerminalVoxelPathStatus(candidate.Status))
                    continue;

                if (!found || candidate.FrameCompleted >= result.FrameCompleted)
                {
                    result = candidate;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>
        /// Copies the latest terminal smoothed voxel path into caller-owned memory.
        /// </summary>
        public bool TryReadVoxelPathWaypoints(
            uint requesterEntityHash,
            Span<VoxelPathWaypointDTO> destination,
            out int waypointCount)
        {
            waypointCount = 0;
            if (!TryReadVoxelPathResult(requesterEntityHash, out PathResultDTO result))
                return false;

            int count = math.max(0, result.WaypointCount);
            waypointCount = count;
            if (count <= 0 || destination.Length < count)
                return false;
            if (IsVoxelAStarJobActive() ||
                !TryReadVaultBuffer(in _voxelPathWaypointsHandle, BufferID.ShinobuVoxelPathWaypoints, ResolveVoxelWaypointCapacity(), out NativeArray<VoxelPathWaypointDTO> waypoints))
            {
                waypointCount = 0;
                return false;
            }

            int start = result.WaypointStart;
            if (start < 0 || start > waypoints.Length - count)
            {
                waypointCount = 0;
                return false;
            }

            for (int i = 0; i < count; i++)
                destination[i] = waypoints[start + i];

            return true;
        }

        /// <summary>
        /// Pure ownership fence check for editor and consumer read gates.
        /// </summary>
        public bool IsVoxelAStarJobActive()
        {
            return _voxelAStarEvaluateScheduled || _voxelAStarSmoothScheduled;
        }

        public static bool IsAnyVoxelAStarJobActive()
        {
            return System.Threading.Volatile.Read(ref _voxelAStarScheduledJobCount) > 0;
        }

        private static void MarkVoxelAStarJobScheduled()
        {
            System.Threading.Interlocked.Increment(ref _voxelAStarScheduledJobCount);
        }

        private static void MarkVoxelAStarJobCompleted()
        {
            int remaining = System.Threading.Interlocked.Decrement(ref _voxelAStarScheduledJobCount);
            if (remaining < 0)
                System.Threading.Interlocked.Exchange(ref _voxelAStarScheduledJobCount, 0);
        }

        private static bool IsTerminalVoxelPathStatus(byte status)
        {
            return status == VoxelPathStatus.Complete ||
                   status == VoxelPathStatus.Partial ||
                   status == VoxelPathStatus.Failed ||
                   status == VoxelPathStatus.InvalidInput ||
                   status == VoxelPathStatus.OutputOverflow;
        }

        private static void InvalidateTerminalVoxelPathResults(
            in PathRequestDTO request,
            NativeArray<PathResultDTO> results,
            uint frame)
        {
            if (request.RequesterEntityHash == 0u || !results.IsCreated)
                return;

            uint safeFrame = frame == 0u ? 1u : frame;
            for (int i = 0; i < results.Length; i++)
            {
                PathResultDTO result = results[i];
                if (result.RequesterEntityHash != request.RequesterEntityHash ||
                    !IsTerminalVoxelPathStatus(result.Status))
                {
                    continue;
                }

                result.RequestFlags = request.Flags;
                result.ResultFlags = 0u;
                result.FrameCompleted = safeFrame;
                result.RawPathCount = 0;
                result.WaypointStart = 0;
                result.WaypointCount = 0;
                result.NodesExpandedTotal = 0;
                result.NodesExpandedLastFrame = 0;
                result.BestNodeIndex = -1;
                result.Status = VoxelPathStatus.Queued;
                result.SolverSlot = 0;
                result.ResultIndex = (ushort)math.min(i, ushort.MaxValue);
                result.RequiredRadius = SanitizeVoxelResultRadius(request.RequiredRadius);
                result.HeuristicWeight = 0f;
                result.QualityWeight = 0f;
                result.EstimatedCost = 0f;
                result.SearchId = 0u;
                result.StartAUP = request.StartAUP;
                result.EndAUP = request.EndAUP;
                results[i] = result;
            }
        }

        private static float SanitizeVoxelResultRadius(float radius)
        {
            if (!math.isfinite(radius))
                return VoxelAStarConstants.MinimumRadiusMeters;

            return math.clamp(radius, VoxelAStarConstants.MinimumRadiusMeters, VoxelAStarConstants.MaximumRadiusMeters);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Parses cold pathing-profile CSV bytes into the native profile table.
        /// </summary>
        public bool TryLoadVoxelPathingProfiles(ReadOnlySpan<byte> csvBytes, out uint flags)
        {
            flags = 0u;
            if (!_voxelAStarColdBootstrapped)
                return false;

            int stagingCapacity = ResolveVoxelProfileCapacity();
            Span<VoxelPathingProfileDTO> stagedProfiles = stackalloc VoxelPathingProfileDTO[stagingCapacity];
            bool parsed = VoxelPathingProfileCsvParser.TryParse(csvBytes, stagedProfiles, out int written, out flags);
            if (!parsed || written <= 0)
                return false;

            IDataVault vault = _dataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsOwnedVaultHandle(in _voxelPathSpeciesProfilesHandle, BufferID.ShinobuVoxelPathSpeciesProfiles) ||
                !IsOwnedVaultHandle(in _voxelPathSpeciesProfileCountHandle, BufferID.ShinobuVoxelPathSpeciesProfileCount))
            {
                return false;
            }

            if (!vault.TryAcquireWriteLock(in _voxelPathSpeciesProfileCountHandle, SystemID.AIPathfinding, out NativeArray<int> profileCount))
                return false;

            try
            {
                if (!profileCount.IsCreated || profileCount.Length < 1)
                {
                    return false;
                }

                profileCount[0] = 0;
            }
            finally
            {
                vault.ReleaseWriteLock(in _voxelPathSpeciesProfileCountHandle, SystemID.AIPathfinding);
            }

            if (!vault.TryAcquireWriteLock(in _voxelPathSpeciesProfilesHandle, SystemID.AIPathfinding, out NativeArray<VoxelPathingProfileDTO> profiles))
                return false;

            int copyCount;
            try
            {
                if (!profiles.IsCreated || profiles.Length <= 0)
                    return false;

                copyCount = math.min(written, profiles.Length);
                for (int i = 0; i < copyCount; i++)
                    profiles[i] = stagedProfiles[i];
                for (int i = copyCount; i < profiles.Length; i++)
                    profiles[i] = default;
            }
            finally
            {
                vault.ReleaseWriteLock(in _voxelPathSpeciesProfilesHandle, SystemID.AIPathfinding);
            }

            if (copyCount <= 0 ||
                !vault.TryAcquireWriteLock(in _voxelPathSpeciesProfileCountHandle, SystemID.AIPathfinding, out profileCount))
            {
                return false;
            }

            try
            {
                if (!profileCount.IsCreated || profileCount.Length < 1)
                    return false;

                profileCount[0] = copyCount;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _voxelPathSpeciesProfileCountHandle, SystemID.AIPathfinding);
            }
        }
#endif

        private bool EnsureVoxelAStarVaultBuffers()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            int requestCapacity = ResolveVoxelRequestCapacity();
            int resultCapacity = ResolveVoxelResultCapacity();
            int rawPathCapacity = ResolveVoxelRawPathCapacity();
            int waypointCapacity = ResolveVoxelWaypointCapacity();
            int nodeCapacity = ResolveVoxelGridCellCapacity();
            int profileCapacity = ResolveVoxelProfileCapacity();
            int closedDebugCapacity = math.max(2, nodeCapacity + 1);

            bool ready =
                EnsureVoxelAStarBuffer(vault, BufferID.ShinobuVoxelPathRequests, requestCapacity, NativeArrayOptions.UninitializedMemory, ref _voxelPathRequestsHandle, out _) &&
                EnsureVoxelAStarBuffer(vault, BufferID.ShinobuVoxelPathRingState, 1, NativeArrayOptions.ClearMemory, ref _voxelPathRingStateHandle, out _) &&
                EnsureVoxelAStarBuffer(vault, BufferID.ShinobuVoxelPathSolverState, 1, NativeArrayOptions.ClearMemory, ref _voxelPathSolverStateHandle, out _) &&
                EnsureVoxelAStarBuffer(vault, BufferID.ShinobuVoxelPathNodes, nodeCapacity, NativeArrayOptions.UninitializedMemory, ref _voxelPathNodesHandle, out _) &&
                EnsureVoxelAStarBuffer(vault, BufferID.ShinobuVoxelPathOpenHeap, nodeCapacity, NativeArrayOptions.UninitializedMemory, ref _voxelPathOpenHeapHandle, out _) &&
                EnsureVoxelAStarBuffer(vault, BufferID.ShinobuVoxelPathHeapPositions, nodeCapacity, NativeArrayOptions.UninitializedMemory, ref _voxelPathHeapPositionsHandle, out _) &&
                EnsureVoxelAStarBuffer(vault, BufferID.ShinobuVoxelPathRawPath, rawPathCapacity, NativeArrayOptions.UninitializedMemory, ref _voxelPathRawPathHandle, out _) &&
                EnsureVoxelAStarBuffer(vault, BufferID.ShinobuVoxelPathWaypoints, waypointCapacity, NativeArrayOptions.UninitializedMemory, ref _voxelPathWaypointsHandle, out _) &&
                EnsureVoxelAStarBuffer(vault, BufferID.ShinobuVoxelPathResults, resultCapacity, NativeArrayOptions.ClearMemory, ref _voxelPathResultsHandle, out _) &&
                EnsureVoxelAStarBuffer(vault, BufferID.ShinobuVoxelPathTelemetryRing, VoxelAStarConstants.TelemetryFrames, NativeArrayOptions.ClearMemory, ref _voxelPathTelemetryHandle, out _) &&
                EnsureVoxelAStarBuffer(vault, BufferID.ShinobuVoxelPathTuning, 1, NativeArrayOptions.ClearMemory, ref _voxelPathTuningHandle, out _) &&
                EnsureVoxelAStarBuffer(vault, BufferID.ShinobuVoxelPathMockSdf, nodeCapacity, NativeArrayOptions.UninitializedMemory, ref _voxelPathMockSdfHandle, out _) &&
                EnsureVoxelAStarBuffer(vault, BufferID.ShinobuVoxelPathSdfHeader, 1, NativeArrayOptions.ClearMemory, ref _voxelPathSdfHeaderHandle, out _) &&
                EnsureVoxelAStarBuffer(vault, BufferID.ShinobuVoxelPathSpeciesProfiles, profileCapacity, NativeArrayOptions.UninitializedMemory, ref _voxelPathSpeciesProfilesHandle, out _) &&
                EnsureVoxelAStarBuffer(vault, BufferID.ShinobuVoxelPathSpeciesProfileCount, 1, NativeArrayOptions.ClearMemory, ref _voxelPathSpeciesProfileCountHandle, out _) &&
                EnsureVoxelAStarBuffer(vault, BufferID.ShinobuVoxelPathClosedDebug, closedDebugCapacity, NativeArrayOptions.UninitializedMemory, ref _voxelPathClosedDebugHandle, out _);

            if (!ready)
                return false;

            return true;
        }

        private bool BootstrapVoxelAStarCold()
        {
            if (!EnsureVoxelAStarVaultBuffers())
                return false;

            InitializeVoxelAStarColdState(ResolveVoxelRequestCapacity());
            EnsureVoxelAStarMockSdfCold();
            _voxelAStarColdBootstrapped = true;
            return true;
        }

        private bool EnsureVoxelAStarViews(
            out NativeArray<PathRequestDTO> requests,
            out NativeArray<VoxelPathRingState> ringState,
            out NativeArray<VoxelPathSolverState> solverState,
            out NativeArray<VoxelPathNodeRecord> nodes,
            out NativeArray<VoxelPathHeapNode> openHeap,
            out NativeArray<int> heapPositions,
            out NativeArray<int> rawPath,
            out NativeArray<int> closedDebug,
            out NativeArray<VoxelPathWaypointDTO> waypoints,
            out NativeArray<PathResultDTO> results,
            out NativeArray<PathfindingTelemetryEntry> telemetry,
            out NativeArray<VoxelAStarTuningDTO> tuning,
            out NativeArray<float> sdf,
            out NativeArray<VoxelSdfGridHeader> header)
        {
            requests = default;
            ringState = default;
            solverState = default;
            nodes = default;
            openHeap = default;
            heapPositions = default;
            rawPath = default;
            closedDebug = default;
            waypoints = default;
            results = default;
            telemetry = default;
            tuning = default;
            sdf = default;
            header = default;

            if (!_voxelAStarColdBootstrapped)
                return false;

            return
                   TryResolveVaultBuffer(in _voxelPathRequestsHandle, BufferID.ShinobuVoxelPathRequests, ResolveVoxelRequestCapacity(), out requests) &&
                   TryResolveVaultBuffer(in _voxelPathRingStateHandle, BufferID.ShinobuVoxelPathRingState, 1, out ringState) &&
                   TryResolveVaultBuffer(in _voxelPathSolverStateHandle, BufferID.ShinobuVoxelPathSolverState, 1, out solverState) &&
                   TryResolveVaultBuffer(in _voxelPathNodesHandle, BufferID.ShinobuVoxelPathNodes, ResolveVoxelGridCellCapacity(), out nodes) &&
                   TryResolveVaultBuffer(in _voxelPathOpenHeapHandle, BufferID.ShinobuVoxelPathOpenHeap, ResolveVoxelGridCellCapacity(), out openHeap) &&
                   TryResolveVaultBuffer(in _voxelPathHeapPositionsHandle, BufferID.ShinobuVoxelPathHeapPositions, ResolveVoxelGridCellCapacity(), out heapPositions) &&
                   TryResolveVaultBuffer(in _voxelPathRawPathHandle, BufferID.ShinobuVoxelPathRawPath, ResolveVoxelRawPathCapacity(), out rawPath) &&
                   TryResolveVaultBuffer(in _voxelPathClosedDebugHandle, BufferID.ShinobuVoxelPathClosedDebug, ResolveVoxelGridCellCapacity() + 1, out closedDebug) &&
                   TryResolveVaultBuffer(in _voxelPathWaypointsHandle, BufferID.ShinobuVoxelPathWaypoints, ResolveVoxelWaypointCapacity(), out waypoints) &&
                   TryResolveVaultBuffer(in _voxelPathResultsHandle, BufferID.ShinobuVoxelPathResults, ResolveVoxelResultCapacity(), out results) &&
                   TryResolveVaultBuffer(in _voxelPathTelemetryHandle, BufferID.ShinobuVoxelPathTelemetryRing, VoxelAStarConstants.TelemetryFrames, out telemetry) &&
                   TryResolveVaultBuffer(in _voxelPathTuningHandle, BufferID.ShinobuVoxelPathTuning, 1, out tuning) &&
                   TryResolveVaultBuffer(in _voxelPathMockSdfHandle, BufferID.ShinobuVoxelPathMockSdf, ResolveVoxelGridCellCapacity(), out sdf) &&
                   TryResolveVaultBuffer(in _voxelPathSdfHeaderHandle, BufferID.ShinobuVoxelPathSdfHeader, 1, out header);
        }

        private void FastTickVoxelAStar(float deltaTime)
        {
            _ = deltaTime;
            if (!_voxelAStarColdBootstrapped)
                return;

            if (_voxelAStarEvaluateScheduled || _voxelAStarSmoothScheduled)
                return;

            if (!EnsureVoxelAStarViews(
                    out NativeArray<PathRequestDTO> requests,
                    out NativeArray<VoxelPathRingState> ringState,
                    out NativeArray<VoxelPathSolverState> solverState,
                    out NativeArray<VoxelPathNodeRecord> nodes,
                    out NativeArray<VoxelPathHeapNode> openHeap,
                    out NativeArray<int> heapPositions,
                    out NativeArray<int> rawPath,
                    out NativeArray<int> closedDebug,
                    out NativeArray<VoxelPathWaypointDTO> waypoints,
                    out NativeArray<PathResultDTO> results,
                    out NativeArray<PathfindingTelemetryEntry> telemetry,
                    out NativeArray<VoxelAStarTuningDTO> tuning,
                    out NativeArray<float> sdf,
                    out NativeArray<VoxelSdfGridHeader> header))
            {
                return;
            }

            _voxelAStarFrame = NextNonZeroFrame(_voxelAStarFrame);
            VoxelPathSolverState state = solverState[0];
            if (state.Status == VoxelPathStatus.RawPathReady)
            {
                state.Status = VoxelPathStatus.Smoothing;
                solverState[0] = state;
                SmoothPathStringPullingJob smoothJob = new SmoothPathStringPullingJob
                {
                    SolverState = solverState,
                    SdfDistances = sdf,
                    GridHeader = header,
                    RawPath = rawPath,
                    Waypoints = waypoints,
                    Results = results,
                    Telemetry = telemetry,
                    Tuning = tuning,
                    Frame = _voxelAStarFrame,
                    GlobalQualityWeight = ResolveVoxelAStarQualityWeight(tuning)
                };
                _voxelAStarSmoothScheduleTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                _voxelAStarSmoothHandle = smoothJob.Schedule();
                _voxelAStarSmoothScheduled = true;
                MarkVoxelAStarJobScheduled();
                H8Memory.RegisterActiveJob(SystemID.AIPathfinding, _voxelAStarSmoothHandle);
                JobHandle.ScheduleBatchedJobs();
                return;
            }

            VoxelPathRingState ring = ringState[0];
            if (state.Active == 0 && ring.Count <= 0)
                return;

            EvaluateVoxelPathJob evaluateJob = new EvaluateVoxelPathJob
            {
                RequestRing = requests,
                RingState = ringState,
                SolverState = solverState,
                SdfDistances = sdf,
                GridHeader = header,
                Nodes = nodes,
                OpenHeap = openHeap,
                HeapPositions = heapPositions,
                RawPath = rawPath,
                ClosedDebug = closedDebug,
                Results = results,
                Telemetry = telemetry,
                Tuning = tuning,
                Frame = _voxelAStarFrame,
                GlobalQualityWeight = ResolveVoxelAStarQualityWeight(tuning)
            };
            _voxelAStarEvaluateScheduleTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            _voxelAStarEvaluateHandle = evaluateJob.Schedule();
            _voxelAStarEvaluateScheduled = true;
            MarkVoxelAStarJobScheduled();
            H8Memory.RegisterActiveJob(SystemID.AIPathfinding, _voxelAStarEvaluateHandle);
            JobHandle.ScheduleBatchedJobs();
        }

        private void LateFrameTickVoxelAStar()
        {
            if (_voxelAStarEvaluateScheduled && _voxelAStarEvaluateHandle.IsCompleted)
            {
                _voxelAStarEvaluateScheduled = false;
                DispatcherJobFence.TryFinalizeCompleted(ref _voxelAStarEvaluateHandle);
                MarkVoxelAStarJobCompleted();
                PatchVoxelAStarTelemetryMicros(_voxelAStarEvaluateScheduleTicks);
                _voxelAStarEvaluateScheduleTicks = 0L;
            }

            if (_voxelAStarSmoothScheduled && _voxelAStarSmoothHandle.IsCompleted)
            {
                _voxelAStarSmoothScheduled = false;
                DispatcherJobFence.TryFinalizeCompleted(ref _voxelAStarSmoothHandle);
                MarkVoxelAStarJobCompleted();
                PatchVoxelAStarTelemetryMicros(_voxelAStarSmoothScheduleTicks);
                _voxelAStarSmoothScheduleTicks = 0L;
            }

            if (IsVoxelAStarJobActive())
                return;

            if (!TryResolveVaultBuffer(in _voxelPathTelemetryHandle, BufferID.ShinobuVoxelPathTelemetryRing, VoxelAStarConstants.TelemetryFrames, out NativeArray<PathfindingTelemetryEntry> telemetry) ||
                telemetry.Length <= 0)
            {
                return;
            }

            int cursor = (int)(_voxelAStarFrame % (uint)math.max(1, math.min(telemetry.Length, VoxelAStarConstants.TelemetryFrames)));
            PathfindingTelemetryEntry entry = telemetry[cursor];
            const uint faultMask = VoxelPathFlags.NaNDetected | VoxelPathFlags.TimeSliceOverBudget;
            if ((entry.Flags & faultMask) == 0u || entry.Frame == 0u || entry.Frame == _voxelAStarLastDumpFrame)
                return;

            if (TryDumpVoxelAStarBlackBox(telemetry))
                _voxelAStarLastDumpFrame = entry.Frame;
        }

        private void ForceCompleteVoxelAStarJobsForTeardown()
        {
            if (_voxelAStarEvaluateScheduled)
            {
                DispatcherJobFence.TryComplete(ref _voxelAStarEvaluateHandle, forceComplete: true);
                _voxelAStarEvaluateScheduled = false;
                MarkVoxelAStarJobCompleted();
                _voxelAStarEvaluateScheduleTicks = 0L;
            }

            if (_voxelAStarSmoothScheduled)
            {
                DispatcherJobFence.TryComplete(ref _voxelAStarSmoothHandle, forceComplete: true);
                _voxelAStarSmoothScheduled = false;
                MarkVoxelAStarJobCompleted();
                _voxelAStarSmoothScheduleTicks = 0L;
            }
        }

        private void InitializeVoxelAStarColdState(int requestCapacity)
        {
            if (TryResolveVaultBuffer(in _voxelPathRingStateHandle, BufferID.ShinobuVoxelPathRingState, 1, out NativeArray<VoxelPathRingState> ringStateBuffer))
            {
                VoxelPathRingState ring = ringStateBuffer[0];
                if (ring.Capacity <= 0)
                {
                    ring.ReadCursor = 0;
                    ring.WriteCursor = 0;
                    ring.Count = 0;
                }

                ring.Capacity = requestCapacity;
                ring.ReadCursor = ClampRingCursor(ring.ReadCursor, requestCapacity);
                ring.WriteCursor = ClampRingCursor(ring.WriteCursor, requestCapacity);
                ring.Count = math.clamp(ring.Count, 0, requestCapacity);
                ringStateBuffer[0] = ring;
            }

            if (TryResolveVaultBuffer(in _voxelPathTuningHandle, BufferID.ShinobuVoxelPathTuning, 1, out NativeArray<VoxelAStarTuningDTO> tuningBuffer))
            {
                VoxelAStarTuningDTO tuning = tuningBuffer[0];
                if (tuning.MinNodesExpandedPerFrame <= 0 ||
                    tuning.MaxNodesExpandedPerFrame <= 0 ||
                    tuning.MaxRawPathNodes <= 0 ||
                    tuning.MaxWaypoints <= 0)
                {
                    tuningBuffer[0] = VoxelAStarTuningDTO.Default();
                }
            }
        }

        private void EnsureVoxelAStarMockSdfCold()
        {
            if (!TryResolveVaultBuffer(in _voxelPathMockSdfHandle, BufferID.ShinobuVoxelPathMockSdf, ResolveVoxelGridCellCapacity(), out NativeArray<float> sdf) ||
                !TryResolveVaultBuffer(in _voxelPathSdfHeaderHandle, BufferID.ShinobuVoxelPathSdfHeader, 1, out NativeArray<VoxelSdfGridHeader> header))
            {
                return;
            }

            int3 dims = ResolveVoxelGridDimensions();
            VoxelSdfGridHeader current = header[0];
            if (current.Dimensions.x == dims.x &&
                current.Dimensions.y == dims.y &&
                current.Dimensions.z == dims.z &&
                current.GridVersion == _voxelAStarMockGridVersion &&
                (current.Flags & VoxelPathFlags.MockSdfGenerated) != 0)
            {
                return;
            }

            GenerateMockPathingSDFJob job = new GenerateMockPathingSDFJob
            {
                SdfDistances = sdf,
                Header = header,
                OriginAUP = double3.zero,
                Dimensions = dims,
                VoxelSizeMeters = VoxelAStarConstants.DefaultVoxelSizeMeters,
                MainTunnelRadiusMeters = 6f,
                ShaftRadiusMeters = 4f,
                GridVersion = _voxelAStarMockGridVersion
            };
            JobHandle handle = job.Schedule(sdf.Length, 128);
            H8Memory.RegisterActiveJob(SystemID.AIPathfinding, handle);
            DispatcherJobFence.TryComplete(ref handle, forceComplete: true); // COLD_BOOTSTRAP_SYNC: mock SDF must exist before first request admission.
        }

        private static bool EnsureVoxelAStarBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            ref VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0)
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
                options);
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

        private void ReleaseVoxelAStarVaultHandles(IDataVault vault)
        {
            if (vault == null)
                return;

            ReleaseVaultHandle(vault, BufferID.ShinobuVoxelPathRequests, ref _voxelPathRequestsHandle);
            ReleaseVaultHandle(vault, BufferID.ShinobuVoxelPathRingState, ref _voxelPathRingStateHandle);
            ReleaseVaultHandle(vault, BufferID.ShinobuVoxelPathSolverState, ref _voxelPathSolverStateHandle);
            ReleaseVaultHandle(vault, BufferID.ShinobuVoxelPathNodes, ref _voxelPathNodesHandle);
            ReleaseVaultHandle(vault, BufferID.ShinobuVoxelPathOpenHeap, ref _voxelPathOpenHeapHandle);
            ReleaseVaultHandle(vault, BufferID.ShinobuVoxelPathHeapPositions, ref _voxelPathHeapPositionsHandle);
            ReleaseVaultHandle(vault, BufferID.ShinobuVoxelPathRawPath, ref _voxelPathRawPathHandle);
            ReleaseVaultHandle(vault, BufferID.ShinobuVoxelPathWaypoints, ref _voxelPathWaypointsHandle);
            ReleaseVaultHandle(vault, BufferID.ShinobuVoxelPathResults, ref _voxelPathResultsHandle);
            ReleaseVaultHandle(vault, BufferID.ShinobuVoxelPathTelemetryRing, ref _voxelPathTelemetryHandle);
            ReleaseVaultHandle(vault, BufferID.ShinobuVoxelPathTuning, ref _voxelPathTuningHandle);
            ReleaseVaultHandle(vault, BufferID.ShinobuVoxelPathMockSdf, ref _voxelPathMockSdfHandle);
            ReleaseVaultHandle(vault, BufferID.ShinobuVoxelPathSdfHeader, ref _voxelPathSdfHeaderHandle);
            ReleaseVaultHandle(vault, BufferID.ShinobuVoxelPathSpeciesProfiles, ref _voxelPathSpeciesProfilesHandle);
            ReleaseVaultHandle(vault, BufferID.ShinobuVoxelPathSpeciesProfileCount, ref _voxelPathSpeciesProfileCountHandle);
            ReleaseVaultHandle(vault, BufferID.ShinobuVoxelPathClosedDebug, ref _voxelPathClosedDebugHandle);
        }

        private void ClearVoxelAStarVaultHandles()
        {
            _voxelPathRequestsHandle = default;
            _voxelPathRingStateHandle = default;
            _voxelPathSolverStateHandle = default;
            _voxelPathNodesHandle = default;
            _voxelPathOpenHeapHandle = default;
            _voxelPathHeapPositionsHandle = default;
            _voxelPathRawPathHandle = default;
            _voxelPathWaypointsHandle = default;
            _voxelPathResultsHandle = default;
            _voxelPathTelemetryHandle = default;
            _voxelPathTuningHandle = default;
            _voxelPathMockSdfHandle = default;
            _voxelPathSdfHeaderHandle = default;
            _voxelPathSpeciesProfilesHandle = default;
            _voxelPathSpeciesProfileCountHandle = default;
            _voxelPathClosedDebugHandle = default;
            _voxelAStarColdBootstrapped = false;
        }

        private int ResolveVoxelRequestCapacity()
        {
            return math.clamp(_voxelPathRequestCapacity, 2, MaxVoxelPathRequestCapacity);
        }

        private int ResolveVoxelResultCapacity()
        {
            return math.clamp(_voxelPathResultCapacity, 2, MaxVoxelPathResultCapacity);
        }

        private int ResolveVoxelRawPathCapacity()
        {
            return math.clamp(_voxelPathRawPathCapacity, 2, MaxVoxelPathRawPathCapacity);
        }

        private int ResolveVoxelWaypointCapacity()
        {
            return math.clamp(_voxelPathWaypointCapacity, 2, MaxVoxelPathWaypointCapacity);
        }

        private int ResolveVoxelProfileCapacity()
        {
            return math.clamp(_voxelPathProfileCapacity, 1, MaxVoxelPathProfileCapacity);
        }

        private int3 ResolveVoxelGridDimensions()
        {
            return new int3(
                math.clamp(_voxelPathGridX, 2, MaxVoxelPathGridDimension),
                math.clamp(_voxelPathGridY, 2, MaxVoxelPathGridDimension),
                math.clamp(_voxelPathGridZ, 2, MaxVoxelPathGridDimension));
        }

        private int ResolveVoxelGridCellCapacity()
        {
            int3 dims = ResolveVoxelGridDimensions();
            return dims.x * dims.y * dims.z;
        }

        private static uint NextNonZeroFrame(uint frame)
        {
            uint next = frame + 1u;
            return next == 0u ? 1u : next;
        }

        private static float ResolveVoxelAStarQualityWeight(NativeArray<VoxelAStarTuningDTO> tuning)
        {
            float global = MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config)
                ? MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, 1f)
                : MathLodApproximation.SaturateFinite(HomeostasisBrain.GlobalQualityWeight, 1f);
            if (tuning.IsCreated && tuning.Length > 0 && math.isfinite(tuning[0].GlobalQualityWeight))
                global = math.min(global, math.saturate(tuning[0].GlobalQualityWeight));
            return global;
        }

        private void PatchVoxelAStarTelemetryMicros(long scheduleTicks)
        {
            if (scheduleTicks <= 0L ||
                !TryResolveVaultBuffer(in _voxelPathTelemetryHandle, BufferID.ShinobuVoxelPathTelemetryRing, VoxelAStarConstants.TelemetryFrames, out NativeArray<PathfindingTelemetryEntry> telemetry) ||
                telemetry.Length <= 0)
            {
                return;
            }

            long elapsedTicks = Math.Max(0L, System.Diagnostics.Stopwatch.GetTimestamp() - scheduleTicks);
            double microsDouble = elapsedTicks * StopwatchTicksToMicros;
            uint micros = microsDouble >= uint.MaxValue ? uint.MaxValue : (uint)Math.Max(0.0d, microsDouble);
            int cursor = (int)(_voxelAStarFrame % (uint)math.max(1, math.min(telemetry.Length, VoxelAStarConstants.TelemetryFrames)));
            PathfindingTelemetryEntry entry = telemetry[cursor];
            entry.BurstMicros = micros;
            if (microsDouble > 1500.0d)
                entry.Flags |= VoxelPathFlags.TimeSliceOverBudget;
            telemetry[cursor] = entry;
        }

        private unsafe bool TryDumpVoxelAStarBlackBox(NativeArray<PathfindingTelemetryEntry> telemetry)
        {
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return false;

            int entryCount = math.min(telemetry.Length, VoxelAStarConstants.TelemetryFrames);
            int byteCount = UnsafeUtility.SizeOf<PathfindingTelemetryEntry>() * entryCount;
            if (byteCount <= 0)
                return false;

            byte* source = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
            uint hash = 2166136261u ^ (uint)byteCount ^ (uint)entryCount;
            for (int i = 0; i < byteCount; i++)
                hash = (hash ^ source[i]) * 16777619u;

            string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                Application.dataPath,
                "..",
                "Docs",
                "AgentLogs",
                "Dump_1403_VOXEL_ASTAR.bin"));

            if (!NativeFaultDumpWriter.TryWriteAll(path, new ReadOnlySpan<byte>(source, byteCount), byteCount))
                return false;

            _voxelAStarLastDumpHash = hash == 0u ? 2166136261u : hash;
            return true;
        }
    }
}
