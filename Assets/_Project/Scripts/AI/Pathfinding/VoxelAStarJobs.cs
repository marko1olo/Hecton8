using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.AI.Pathfinding
{
    /// <summary>
    /// Unmanaged binary min-heap for voxel A* open-set ownership.
    /// </summary>
    public unsafe ref struct NativeMinHeap
    {
        private const float TieEpsilon = 0.00001f;

        // Pointer view is valid only inside EvaluateVoxelPathJob.Execute; the job also carries the NativeArray fields so Unity owns lifetime and dependency safety.
        [NoAlias, NativeDisableUnsafePtrRestriction] private VoxelPathHeapNode* _heap;
        [NoAlias, NativeDisableUnsafePtrRestriction] private int* _heapPositions;
        [NoAlias, NativeDisableUnsafePtrRestriction] private VoxelPathNodeRecord* _nodes;
        private int _heapLength;
        private int _heapPositionsLength;
        private int _nodesLength;
        private uint _searchId;

        /// <summary>Number of live entries in the heap.</summary>
        public int Count;

        /// <summary>
        /// Creates a heap view over caller-owned memory.
        /// </summary>
        public NativeMinHeap(
            NativeArray<VoxelPathHeapNode> heap,
            NativeArray<int> heapPositions,
            NativeArray<VoxelPathNodeRecord> nodes,
            uint searchId,
            int count)
        {
            _heap = heap.IsCreated
                ? (VoxelPathHeapNode*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(heap)
                : null;
            _heapPositions = heapPositions.IsCreated
                ? (int*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(heapPositions)
                : null;
            _nodes = nodes.IsCreated
                ? (VoxelPathNodeRecord*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(nodes)
                : null;
            _heapLength = heap.IsCreated ? heap.Length : 0;
            _heapPositionsLength = heapPositions.IsCreated ? heapPositions.Length : 0;
            _nodesLength = nodes.IsCreated ? nodes.Length : 0;
            _searchId = searchId;
            Count = math.clamp(count, 0, _heapLength);
        }

        /// <summary>
        /// Pushes a new node or decreases its priority when it is already open.
        /// </summary>
        public bool TryPushOrDecrease(int nodeIndex, float fCost, float gCost, uint tieBreak)
        {
            if (_heap == null ||
                _heapPositions == null ||
                _nodes == null ||
                _heapLength <= 0 ||
                nodeIndex < 0 ||
                nodeIndex >= _nodesLength ||
                !math.isfinite(fCost) ||
                !math.isfinite(gCost))
            {
                return false;
            }

            int position = ResolveHeapPosition(nodeIndex);
            if (position >= 0 && position < Count && position < _heapLength && _heap[position].NodeIndex == nodeIndex)
            {
                VoxelPathHeapNode current = _heap[position];
                if (!IsLower(fCost, gCost, tieBreak, current))
                    return true;

                current.FCost = fCost;
                current.GCost = gCost;
                current.TieBreak = tieBreak;
                _heap[position] = current;
                SiftUp(position);
                return true;
            }

            if (Count >= _heapLength)
                return false;

            int insertIndex = Count;
            Count++;
            _heap[insertIndex] = new VoxelPathHeapNode
            {
                NodeIndex = nodeIndex,
                FCost = fCost,
                GCost = gCost,
                TieBreak = tieBreak
            };
            SetHeapPosition(nodeIndex, insertIndex);
            SiftUp(insertIndex);
            return true;
        }

        /// <summary>
        /// Pops the lowest-priority node from the heap.
        /// </summary>
        public bool TryPop(out VoxelPathHeapNode node)
        {
            node = default;
            if (_heap == null || _nodes == null || Count <= 0)
                return false;

            node = _heap[0];
            SetHeapPosition(node.NodeIndex, -1);

            Count--;
            if (Count > 0)
            {
                VoxelPathHeapNode moved = _heap[Count];
                _heap[0] = moved;
                SetHeapPosition(moved.NodeIndex, 0);
                SiftDown(0);
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsLower(float fCost, float gCost, uint tieBreak, in VoxelPathHeapNode other)
        {
            if (fCost < other.FCost - TieEpsilon)
                return true;
            if (fCost > other.FCost + TieEpsilon)
                return false;
            if (gCost < other.GCost - TieEpsilon)
                return true;
            if (gCost > other.GCost + TieEpsilon)
                return false;
            return tieBreak < other.TieBreak;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsLower(in VoxelPathHeapNode a, in VoxelPathHeapNode b)
        {
            return IsLower(a.FCost, a.GCost, a.TieBreak, in b);
        }

        private void SiftUp(int index)
        {
            while (index > 0)
            {
                int parent = (index - 1) >> 1;
                VoxelPathHeapNode childNode = _heap[index];
                VoxelPathHeapNode parentNode = _heap[parent];
                if (!IsLower(in childNode, in parentNode))
                    return;

                Swap(index, parent);
                index = parent;
            }
        }

        private void SiftDown(int index)
        {
            while (true)
            {
                int left = (index << 1) + 1;
                if (left >= Count)
                    return;

                int best = left;
                int right = left + 1;
                if (right < Count)
                {
                    VoxelPathHeapNode rightNode = _heap[right];
                    VoxelPathHeapNode leftNode = _heap[left];
                    if (IsLower(in rightNode, in leftNode))
                        best = right;
                }

                VoxelPathHeapNode bestNode = _heap[best];
                VoxelPathHeapNode currentNode = _heap[index];
                if (!IsLower(in bestNode, in currentNode))
                    return;

                Swap(index, best);
                index = best;
            }
        }

        private void Swap(int a, int b)
        {
            VoxelPathHeapNode temp = _heap[a];
            VoxelPathHeapNode bNode = _heap[b];
            _heap[a] = bNode;
            _heap[b] = temp;

            SetHeapPosition(bNode.NodeIndex, a);
            SetHeapPosition(temp.NodeIndex, b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ResolveHeapPosition(int nodeIndex)
        {
            if (_nodes == null || nodeIndex < 0 || nodeIndex >= _nodesLength)
                return -1;

            VoxelPathNodeRecord record = _nodes[nodeIndex];
            if (record.SearchId != _searchId)
                return -1;

            return record.HeapPosition;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetHeapPosition(int nodeIndex, int position)
        {
            if (_heapPositions != null && nodeIndex >= 0 && nodeIndex < _heapPositionsLength)
                _heapPositions[nodeIndex] = position;

            if (_nodes == null || nodeIndex < 0 || nodeIndex >= _nodesLength)
                return;

            VoxelPathNodeRecord record = _nodes[nodeIndex];
            if (record.SearchId != _searchId)
                return;

            record.HeapPosition = position;
            _nodes[nodeIndex] = record;
        }
    }

    /// <summary>
    /// Fills a deterministic emergency SDF cave volume for pathfinding bootstrap.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockPathingSDFJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<float> SdfDistances;
        [NoAlias] public NativeArray<VoxelSdfGridHeader> Header;
        public double3 OriginAUP;
        public int3 Dimensions;
        public float VoxelSizeMeters;
        public float MainTunnelRadiusMeters;
        public float ShaftRadiusMeters;
        public uint GridVersion;

        /// <inheritdoc />
        public void Execute(int index)
        {
            int total = SafeVolume(Dimensions);
            if (!SdfDistances.IsCreated || index < 0 || index >= SdfDistances.Length || index >= total)
                return;

            float voxelSize = math.max(VoxelAStarConstants.MinimumVoxelSizeMeters, FiniteOrFallback(VoxelSizeMeters, VoxelAStarConstants.DefaultVoxelSizeMeters));
            int3 dims = new int3(math.max(1, Dimensions.x), math.max(1, Dimensions.y), math.max(1, Dimensions.z));
            int3 c = IndexToCoord(index, dims);
            float3 p = (new float3(c.x, c.y, c.z) + 0.5f) * voxelSize;
            float3 extents = new float3(dims.x, dims.y, dims.z) * voxelSize;
            float t = dims.x > 1 ? (c.x + 0.5f) * math.rcp((float)dims.x) : 0f;

            float triA = TriangleWave(t * 3.0f);
            float triB = TriangleWave(t * 5.0f + 0.25f);
            float centerY = extents.y * math.lerp(0.34f, 0.66f, triA);
            float centerZ = extents.z * math.lerp(0.38f, 0.62f, triB);
            float radius = math.max(voxelSize, FiniteOrFallback(MainTunnelRadiusMeters, 5.5f));
            float tubeDeltaY = p.y - centerY;
            float tubeDeltaZ = p.z - centerZ;
            float tubeDistance = VoxelAStarConstants.FastLengthFromSq((tubeDeltaY * tubeDeltaY) + (tubeDeltaZ * tubeDeltaZ));
            float tunnelClearance = radius - tubeDistance;

            float midX = extents.x * 0.5f;
            float midZ = extents.z * 0.5f;
            float shaftRadius = math.max(voxelSize, FiniteOrFallback(ShaftRadiusMeters, radius * 0.72f));
            float shaftDeltaX = p.x - midX;
            float shaftDeltaZ = p.z - midZ;
            float shaftDistance = VoxelAStarConstants.FastLengthFromSq((shaftDeltaX * shaftDeltaX) + (shaftDeltaZ * shaftDeltaZ));
            float shaftClearance = shaftRadius - shaftDistance;

            float chamberScale = math.rcp(math.max(voxelSize, radius * 1.35f));
            float3 chamberDelta = (p - (extents * 0.5f)) * chamberScale;
            float chamberDistance = VoxelAStarConstants.FastLengthFromSq(math.lengthsq(chamberDelta));
            float chamberClearance = (1.18f - chamberDistance) * radius;

            float clearance = math.max(tunnelClearance, math.max(shaftClearance, chamberClearance));
            float boundaryShell = math.min(
                math.min(math.min(p.x, extents.x - p.x), math.min(p.y, extents.y - p.y)),
                math.min(p.z, extents.z - p.z));
            SdfDistances[index] = math.min(clearance, boundaryShell);

            if (index == 0 && Header.IsCreated && Header.Length > 0)
            {
                Header[0] = new VoxelSdfGridHeader
                {
                    OriginAUP = OriginAUP,
                    Dimensions = dims,
                    VoxelSizeMeters = voxelSize,
                    GridVersion = GridVersion == 0u ? 1u : GridVersion,
                    Flags = VoxelPathFlags.MockSdfGenerated,
                    SolidMarginMeters = voxelSize * 0.5f,
                    MaxDistanceMeters = math.max(radius, shaftRadius)
                };
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float TriangleWave(float x)
        {
            float f = math.frac(x);
            return 1f - math.abs((f * 2f) - 1f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SafeVolume(int3 dims)
        {
            int x = math.max(1, dims.x);
            int y = math.max(1, dims.y);
            int z = math.max(1, dims.z);
            return x * y * z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int3 IndexToCoord(int index, int3 dims)
        {
            int x = index % dims.x;
            int yz = index / dims.x;
            int y = yz % dims.y;
            int z = yz / dims.y;
            return new int3(x, y, z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FiniteOrFallback(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }
    }

    /// <summary>
    /// Time-sliced SDF A* evaluator. It never blocks for a complete route in one frame.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct EvaluateVoxelPathJob : IJob
    {
        private const byte NodeFlagOpen = 1;
        private const byte NodeFlagClosed = 2;

        [NoAlias] public NativeArray<PathRequestDTO> RequestRing;
        [NoAlias] public NativeArray<VoxelPathRingState> RingState;
        [NoAlias] public NativeArray<VoxelPathSolverState> SolverState;
        [ReadOnly, NoAlias] public NativeArray<float> SdfDistances;
        [ReadOnly, NoAlias] public NativeArray<VoxelSdfGridHeader> GridHeader;
        [NoAlias] public NativeArray<VoxelPathNodeRecord> Nodes;
        [NoAlias] public NativeArray<VoxelPathHeapNode> OpenHeap;
        [NoAlias] public NativeArray<int> HeapPositions;
        [NoAlias] public NativeArray<int> RawPath;
        [NoAlias] public NativeArray<int> ClosedDebug;
        [NoAlias] public NativeArray<PathResultDTO> Results;
        [NoAlias] public NativeArray<PathfindingTelemetryEntry> Telemetry;
        [ReadOnly, NoAlias] public NativeArray<VoxelAStarTuningDTO> Tuning;
        public uint Frame;
        public float GlobalQualityWeight;

        /// <inheritdoc />
        public void Execute()
        {
            if (!HasMinimumBuffers())
                return;

            VoxelPathRingState ring = RingState[0];
            VoxelPathSolverState state = SolverState[0];
            VoxelAStarTuningDTO tuning = SanitizeTuning(Tuning[0], GlobalQualityWeight);
            uint telemetryFlags = 0u;
            int expandedThisFrame = 0;
            bool successThisFrame = false;
            bool failedThisFrame = false;

            if (state.Active != 0 && state.Status == VoxelPathStatus.Searching)
                state.Flags &= ~(VoxelPathFlags.NodeBudgetYield | VoxelPathFlags.TimeSliceOverBudget);

            if (state.Active == 0 && state.Status != VoxelPathStatus.RawPathReady && state.Status != VoxelPathStatus.Smoothing)
            {
                if (!TryDequeueRequest(ref ring, out PathRequestDTO request))
                {
                    RingState[0] = ring;
                    WriteIdleTelemetry(in state, in ring, tuning);
                    return;
                }

                BeginSearch(request, ref state, in ring, in tuning);
            }

            if (state.Active == 0 || state.Status != VoxelPathStatus.Searching)
            {
                SolverState[0] = state;
                RingState[0] = ring;
                WriteIdleTelemetry(in state, in ring, tuning);
                return;
            }

            NativeMinHeap heap = new NativeMinHeap(OpenHeap, HeapPositions, Nodes, state.SearchId, state.OpenHeapCount);
            int budget = ResolveNodeBudget(in tuning);
            int guardBudget = math.max(1, budget);
            for (int i = 0; i < guardBudget; i++)
            {
                if (!heap.TryPop(out VoxelPathHeapNode heapNode))
                    break;

                int nodeIndex = heapNode.NodeIndex;
                if (!IsNodeIndexValid(nodeIndex, in state))
                    continue;

                VoxelPathNodeRecord node = Nodes[nodeIndex];
                if (node.SearchId != state.SearchId || (node.Flags & NodeFlagClosed) != 0)
                    continue;

                node.Flags = NodeFlagClosed;
                Nodes[nodeIndex] = node;
                AppendClosedDebug(nodeIndex, state.NodesExpandedTotal + expandedThisFrame);
                expandedThisFrame++;

                if (nodeIndex == state.GoalIndex)
                {
                    successThisFrame = true;
                    state.BestNodeIndex = nodeIndex;
                    break;
                }

                ExpandNeighbors(nodeIndex, in node, ref state, ref heap, in tuning);
            }

            state.OpenHeapCount = heap.Count;
            state.NodesExpandedLastFrame = expandedThisFrame;
            state.NodesExpandedTotal += expandedThisFrame;
            state.FrameUpdated = Frame == 0u ? state.FrameUpdated + 1u : Frame;

            if (successThisFrame)
            {
                FinishSearchWithRawPath(ref state, VoxelPathStatus.RawPathReady, ref telemetryFlags);
            }
            else if (state.OpenHeapCount <= 0)
            {
                telemetryFlags |= VoxelPathFlags.OpenSetExhausted;
                state.Flags |= VoxelPathFlags.OpenSetExhausted;
                if (state.BestNodeIndex >= 0 && state.BestNodeIndex != state.StartIndex)
                {
                    state.Flags |= VoxelPathFlags.PartialNearestFallback;
                    FinishSearchWithRawPath(ref state, VoxelPathStatus.RawPathReady, ref telemetryFlags);
                }
                else
                {
                    failedThisFrame = true;
                    FinishFailed(ref state, VoxelPathStatus.Failed, state.Flags | telemetryFlags, 0);
                }
            }
            else
            {
                state.Flags |= VoxelPathFlags.NodeBudgetYield;
            }

            if ((state.Flags & VoxelPathFlags.NodeBudgetYield) != 0)
                telemetryFlags |= VoxelPathFlags.NodeBudgetYield;
            if ((state.Flags & VoxelPathFlags.NaNDetected) != 0)
                telemetryFlags |= VoxelPathFlags.NaNDetected;
            if (state.HeuristicWeight > 1.0001f)
                telemetryFlags |= VoxelPathFlags.UsedWeightedHeuristic;

            if (state.Status == VoxelPathStatus.Searching)
                SolverState[0] = state;

            RingState[0] = ring;
            WriteTelemetry(in state, in ring, tuning, telemetryFlags, successThisFrame, failedThisFrame);
        }

        private bool HasMinimumBuffers()
        {
            return RequestRing.IsCreated &&
                   RingState.IsCreated &&
                   RingState.Length > 0 &&
                   SolverState.IsCreated &&
                   SolverState.Length > 0 &&
                   SdfDistances.IsCreated &&
                   GridHeader.IsCreated &&
                   GridHeader.Length > 0 &&
                   Nodes.IsCreated &&
                   OpenHeap.IsCreated &&
                   HeapPositions.IsCreated &&
                   RawPath.IsCreated &&
                   Results.IsCreated &&
                   Tuning.IsCreated &&
                   Tuning.Length > 0;
        }

        private bool TryDequeueRequest(ref VoxelPathRingState ring, out PathRequestDTO request)
        {
            request = default;
            if (!RequestRing.IsCreated || RequestRing.Length <= 0 || ring.Count <= 0)
                return false;

            int capacity = math.min(RequestRing.Length, math.max(1, ring.Capacity));
            int readCursor = ClampRingCursor(ring.ReadCursor, capacity);
            request = RequestRing[readCursor];
            ring.ReadCursor = AdvanceRingCursor(readCursor, capacity);
            ring.Count = math.max(0, ring.Count - 1);
            ring.ConsumedRequests++;
            return true;
        }

        private void BeginSearch(
            in PathRequestDTO request,
            ref VoxelPathSolverState state,
            in VoxelPathRingState ring,
            in VoxelAStarTuningDTO tuning)
        {
            VoxelSdfGridHeader header = GridHeader[0];
            state = default;
            state.Request = request;
            state.GridOriginAUP = header.OriginAUP;
            state.SearchId = NextSearchId(SolverState[0].SearchId);
            state.FrameStarted = Frame == 0u ? 1u : Frame;
            state.FrameUpdated = state.FrameStarted;
            state.ResultIndex = (ushort)ResolveResultIndex(request.RequesterEntityHash, ring.ConsumedRequests);
            state.HeuristicWeight = ResolveHeuristicWeight(in tuning);
            state.RequiredRadius = ResolveRequiredRadius(request.RequiredRadius);
            state.Dimensions = header.Dimensions;
            state.VoxelSizeMeters = header.VoxelSizeMeters;
            state.GridVersion = header.GridVersion;
            state.BestNodeIndex = -1;
            state.BestGoalDistanceSq = float.MaxValue;

            uint flags = 0u;
            if (!ValidateSearchInput(in request, in header, ref state, ref flags))
            {
                FinishFailed(ref state, VoxelPathStatus.InvalidInput, flags, 0);
                return;
            }

            NativeMinHeap heap = new NativeMinHeap(OpenHeap, HeapPositions, Nodes, state.SearchId, 0);
            float startHeuristic = HeuristicCost(IndexToCoord(state.StartIndex, state.Dimensions), IndexToCoord(state.GoalIndex, state.Dimensions), in state, in tuning);
            VoxelPathNodeRecord start = default;
            start.GCost = 0f;
            start.FCost = startHeuristic * state.HeuristicWeight;
            start.ParentIndex = -1;
            start.SearchId = state.SearchId;
            start.HeapPosition = -1;
            start.Flags = NodeFlagOpen;
            start.BestGoalDistanceSqBits = math.asuint(HeuristicDistanceSq(state.StartIndex, state.GoalIndex, state.Dimensions));
            Nodes[state.StartIndex] = start;
            if (!heap.TryPushOrDecrease(state.StartIndex, start.FCost, 0f, (uint)state.StartIndex))
            {
                FinishFailed(ref state, VoxelPathStatus.OutputOverflow, VoxelPathFlags.RawPathOverflow, 0);
                return;
            }

            state.OpenHeapCount = heap.Count;
            state.Active = 1;
            state.Status = VoxelPathStatus.Searching;
            state.BestNodeIndex = state.StartIndex;
            state.BestGoalDistanceSq = HeuristicDistanceSq(state.StartIndex, state.GoalIndex, state.Dimensions);
            state.Flags = flags | (state.HeuristicWeight > 1.0001f ? VoxelPathFlags.UsedWeightedHeuristic : 0u);
            if (ClosedDebug.IsCreated && ClosedDebug.Length > 0)
                ClosedDebug[0] = 0;

            WriteResult(in state, VoxelPathStatus.Searching);
        }

        private bool ValidateSearchInput(
            in PathRequestDTO request,
            in VoxelSdfGridHeader header,
            ref VoxelPathSolverState state,
            ref uint flags)
        {
            if (!math.all(math.isfinite(request.StartAUP)) || !math.all(math.isfinite(request.EndAUP)))
            {
                flags |= VoxelPathFlags.NonFiniteInput | VoxelPathFlags.NaNDetected;
                return false;
            }

            if (header.Dimensions.x <= 0 || header.Dimensions.y <= 0 || header.Dimensions.z <= 0)
            {
                flags |= VoxelPathFlags.SdfMissing;
                return false;
            }

            int total = SafeVolume(header.Dimensions);
            if (total <= 0 ||
                total > SdfDistances.Length ||
                total > Nodes.Length ||
                total > HeapPositions.Length ||
                OpenHeap.Length < total)
            {
                flags |= VoxelPathFlags.SdfMissing;
                return false;
            }

            float voxelSize = header.VoxelSizeMeters;
            if (!math.isfinite(voxelSize) || voxelSize < VoxelAStarConstants.MinimumVoxelSizeMeters)
            {
                flags |= VoxelPathFlags.SdfMissing;
                return false;
            }

            double3 startOffsetD = request.StartAUP - header.OriginAUP;
            double3 goalOffsetD = request.EndAUP - header.OriginAUP;
            if (!math.all(math.isfinite(startOffsetD)) || !math.all(math.isfinite(goalOffsetD)))
            {
                flags |= VoxelPathFlags.NonFiniteInput | VoxelPathFlags.NaNDetected;
                return false;
            }

            float3 startLocal = new float3((float)startOffsetD.x, (float)startOffsetD.y, (float)startOffsetD.z);
            float3 goalLocal = new float3((float)goalOffsetD.x, (float)goalOffsetD.y, (float)goalOffsetD.z);
            if (!math.all(math.isfinite(startLocal)) || !math.all(math.isfinite(goalLocal)))
            {
                flags |= VoxelPathFlags.NonFiniteInput | VoxelPathFlags.NaNDetected;
                return false;
            }

            if (!TryPointToIndex(startLocal, header.Dimensions, voxelSize, out int startIndex))
            {
                flags |= VoxelPathFlags.StartOutOfBounds;
                return false;
            }

            if (!TryPointToIndex(goalLocal, header.Dimensions, voxelSize, out int goalIndex))
            {
                flags |= VoxelPathFlags.GoalOutOfBounds;
                return false;
            }

            if (SdfDistances[startIndex] < state.RequiredRadius)
            {
                flags |= VoxelPathFlags.StartBlocked;
                return false;
            }

            if (SdfDistances[goalIndex] < state.RequiredRadius)
            {
                flags |= VoxelPathFlags.GoalBlocked;
                return false;
            }

            state.StartIndex = startIndex;
            state.GoalIndex = goalIndex;
            return true;
        }

        private void ExpandNeighbors(
            int nodeIndex,
            in VoxelPathNodeRecord node,
            ref VoxelPathSolverState state,
            ref NativeMinHeap heap,
            in VoxelAStarTuningDTO tuning)
        {
            int3 coord = IndexToCoord(nodeIndex, state.Dimensions);
            int3 goalCoord = IndexToCoord(state.GoalIndex, state.Dimensions);
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if ((dx | dy | dz) == 0)
                            continue;

                        int3 nextCoord = coord + new int3(dx, dy, dz);
                        if (!IsCoordValid(nextCoord, state.Dimensions))
                            continue;

                        int nextIndex = CoordToIndex(nextCoord, state.Dimensions);
                        if (nextIndex < 0 || nextIndex >= SdfDistances.Length || SdfDistances[nextIndex] < state.RequiredRadius)
                            continue;
                        if (!HasStepClearance(coord, nextCoord, in state, in tuning))
                            continue;

                        VoxelPathNodeRecord next = Nodes[nextIndex];
                        if (next.SearchId == state.SearchId && (next.Flags & NodeFlagClosed) != 0)
                            continue;

                        float movementCost = MovementCost(dx, dy, dz, state.VoxelSizeMeters, tuning.VerticalPenalty);
                        float candidateG = node.GCost + movementCost;
                        bool fresh = next.SearchId != state.SearchId;
                        if (!fresh && candidateG >= next.GCost)
                            continue;

                        float heuristic = HeuristicCost(nextCoord, goalCoord, in state, in tuning);
                        next.GCost = candidateG;
                        next.FCost = candidateG + (heuristic * state.HeuristicWeight);
                        next.ParentIndex = nodeIndex;
                        next.SearchId = state.SearchId;
                        if (fresh)
                            next.HeapPosition = -1;
                        next.Flags = NodeFlagOpen;
                        int3 goalDelta = nextCoord - goalCoord;
                        float distSq = math.lengthsq(new float3(goalDelta.x, goalDelta.y, goalDelta.z));
                        next.BestGoalDistanceSqBits = math.asuint(distSq);
                        Nodes[nextIndex] = next;

                        if (distSq < state.BestGoalDistanceSq)
                        {
                            state.BestGoalDistanceSq = distSq;
                            state.BestNodeIndex = nextIndex;
                        }

                        if (!heap.TryPushOrDecrease(nextIndex, next.FCost, next.GCost, (uint)nextIndex))
                            state.Flags |= VoxelPathFlags.RawPathOverflow;
                    }
                }
            }
        }

        private void FinishSearchWithRawPath(ref VoxelPathSolverState state, byte nextStatus, ref uint telemetryFlags)
        {
            int target = (state.Flags & VoxelPathFlags.PartialNearestFallback) != 0 ? state.BestNodeIndex : state.GoalIndex;
            if (!BuildRawPath(target, ref state))
            {
                FinishFailed(ref state, VoxelPathStatus.OutputOverflow, state.Flags | VoxelPathFlags.RawPathOverflow, 0);
                telemetryFlags |= VoxelPathFlags.RawPathOverflow;
                return;
            }

            state.Active = 0;
            state.OpenHeapCount = 0;
            state.Status = nextStatus;
            SolverState[0] = state;
            WriteResult(in state, state.Status);
        }

        private bool HasStepClearance(
            int3 from,
            int3 to,
            in VoxelPathSolverState state,
            in VoxelAStarTuningDTO tuning)
        {
            float q = Smooth01(tuning.GlobalQualityWeight);
            int samples = math.clamp((int)math.round(math.lerp(1f, 3f, q)), 1, 3);
            float3 a = (new float3(from.x, from.y, from.z) + 0.5f) * state.VoxelSizeMeters;
            float3 b = (new float3(to.x, to.y, to.z) + 0.5f) * state.VoxelSizeMeters;
            float invSampleCount = math.rcp((float)(samples + 1));
            for (int i = 1; i <= samples; i++)
            {
                float t = i * invSampleCount;
                float3 p = math.lerp(a, b, t);
                if (!TryPointToIndex(p, state.Dimensions, state.VoxelSizeMeters, out int sampleIndex))
                    return false;
                if (sampleIndex < 0 || sampleIndex >= SdfDistances.Length || SdfDistances[sampleIndex] < state.RequiredRadius)
                    return false;
            }

            return true;
        }

        private bool BuildRawPath(int targetIndex, ref VoxelPathSolverState state)
        {
            if (!RawPath.IsCreated || RawPath.Length <= 0 || !IsNodeIndexValid(targetIndex, in state))
                return false;

            int configuredLimit = Tuning[0].MaxRawPathNodes > 1 ? Tuning[0].MaxRawPathNodes : RawPath.Length;
            int limit = math.min(RawPath.Length, math.max(2, configuredLimit));
            int count = 0;
            int current = targetIndex;
            int guard = 0;
            while (current >= 0 && current < Nodes.Length && guard <= Nodes.Length)
            {
                if (count >= limit)
                {
                    state.Flags |= VoxelPathFlags.RawPathOverflow;
                    return false;
                }

                RawPath[count] = current;
                count++;
                if (current == state.StartIndex)
                    break;

                VoxelPathNodeRecord record = Nodes[current];
                if (record.SearchId != state.SearchId)
                    return false;

                current = record.ParentIndex;
                guard++;
            }

            if (count <= 0 || RawPath[count - 1] != state.StartIndex)
                return false;

            for (int i = 0; i < count >> 1; i++)
            {
                int other = count - 1 - i;
                int temp = RawPath[i];
                RawPath[i] = RawPath[other];
                RawPath[other] = temp;
            }

            state.RawPathCount = count;
            return true;
        }

        private void FinishFailed(ref VoxelPathSolverState state, byte status, uint flags, int rawPathCount)
        {
            state.Active = 0;
            state.OpenHeapCount = 0;
            state.RawPathCount = rawPathCount;
            state.WaypointCount = 0;
            state.Status = status;
            state.Flags |= flags;
            state.FrameUpdated = Frame == 0u ? state.FrameUpdated + 1u : Frame;
            SolverState[0] = state;
            WriteResult(in state, status);
        }

        private void WriteResult(in VoxelPathSolverState state, byte status)
        {
            if (!Results.IsCreated || Results.Length <= 0)
                return;

            int index = math.clamp((int)state.ResultIndex, 0, Results.Length - 1);
            float estimatedCost = 0f;
            if (IsNodeIndexValid(state.BestNodeIndex, in state))
            {
                VoxelPathNodeRecord best = Nodes[state.BestNodeIndex];
                estimatedCost = math.select(0f, best.GCost, math.isfinite(best.GCost));
            }

            Results[index] = new PathResultDTO
            {
                RequesterEntityHash = state.Request.RequesterEntityHash,
                RequestFlags = state.Request.Flags,
                ResultFlags = state.Flags,
                FrameCompleted = state.FrameUpdated,
                RawPathCount = state.RawPathCount,
                WaypointStart = 0,
                WaypointCount = state.WaypointCount,
                NodesExpandedTotal = state.NodesExpandedTotal,
                NodesExpandedLastFrame = state.NodesExpandedLastFrame,
                BestNodeIndex = state.BestNodeIndex,
                Status = status,
                SolverSlot = 0,
                ResultIndex = state.ResultIndex,
                RequiredRadius = state.RequiredRadius,
                HeuristicWeight = state.HeuristicWeight,
                QualityWeight = SanitizeQuality(GlobalQualityWeight),
                EstimatedCost = estimatedCost,
                SearchId = state.SearchId,
                StartAUP = state.Request.StartAUP,
                EndAUP = state.Request.EndAUP
            };
        }

        private void WriteIdleTelemetry(
            in VoxelPathSolverState state,
            in VoxelPathRingState ring,
            in VoxelAStarTuningDTO tuning)
        {
            WriteTelemetry(in state, in ring, tuning, 0u, false, false);
        }

        private void WriteTelemetry(
            in VoxelPathSolverState state,
            in VoxelPathRingState ring,
            in VoxelAStarTuningDTO tuning,
            uint flags,
            bool success,
            bool failed)
        {
            if (!Telemetry.IsCreated || Telemetry.Length <= 0)
                return;

            int length = math.min(Telemetry.Length, VoxelAStarConstants.TelemetryFrames);
            int cursor = (int)((Frame == 0u ? state.FrameUpdated : Frame) % (uint)math.max(1, length));
            Telemetry[cursor] = new PathfindingTelemetryEntry
            {
                Frame = Frame == 0u ? state.FrameUpdated : Frame,
                PendingRequests = (uint)math.max(0, ring.Count),
                AcceptedRequests = ring.AcceptedRequests,
                DroppedRequests = ring.DroppedRequests,
                SuccessfulPaths = success ? 1u : 0u,
                FailedPaths = failed ? 1u : 0u,
                NodesExpanded = (uint)math.max(0, state.NodesExpandedLastFrame),
                AverageNodesExpanded = ResolveAverageNodesExpanded(in state, Frame),
                BurstMicros = 0u,
                Flags = flags | state.Flags,
                SearchId = state.SearchId,
                RequesterEntityHash = state.Request.RequesterEntityHash,
                QualityWeight = tuning.GlobalQualityWeight,
                HeuristicWeight = state.HeuristicWeight,
                RawPathCount = (ushort)math.min(math.max(0, state.RawPathCount), ushort.MaxValue),
                WaypointCount = (ushort)math.min(math.max(0, state.WaypointCount), ushort.MaxValue)
            };
        }

        private void AppendClosedDebug(int nodeIndex, int expandedIndex)
        {
            if (!ClosedDebug.IsCreated || ClosedDebug.Length <= 1)
                return;

            int count = math.clamp(ClosedDebug[0], 0, ClosedDebug.Length - 1);
            if (expandedIndex == 0)
                count = 0;

            if (count + 1 >= ClosedDebug.Length)
                return;

            count++;
            ClosedDebug[count] = nodeIndex;
            ClosedDebug[0] = count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ResolveAverageNodesExpanded(in VoxelPathSolverState state, uint frame)
        {
            uint start = state.FrameStarted == 0u ? 1u : state.FrameStarted;
            uint current = frame == 0u ? (state.FrameUpdated > start ? state.FrameUpdated : start) : frame;
            uint frames = current >= start ? current - start + 1u : 1u;
            uint total = (uint)math.max(0, state.NodesExpandedTotal);
            return total / (frames == 0u ? 1u : frames);
        }

        private static VoxelAStarTuningDTO SanitizeTuning(VoxelAStarTuningDTO tuning, float globalQualityWeight)
        {
            VoxelAStarTuningDTO fallback = VoxelAStarTuningDTO.Default();
            tuning.GlobalQualityWeight = SanitizeQuality(math.min(SanitizeQuality(globalQualityWeight), SanitizeQuality(tuning.GlobalQualityWeight)));
            tuning.MinimumHeuristicWeight = FinitePositiveOrFallback(tuning.MinimumHeuristicWeight, fallback.MinimumHeuristicWeight);
            tuning.MaximumHeuristicWeight = math.max(tuning.MinimumHeuristicWeight, FinitePositiveOrFallback(tuning.MaximumHeuristicWeight, fallback.MaximumHeuristicWeight));
            tuning.SmoothingSampleSpacingMeters = FinitePositiveOrFallback(tuning.SmoothingSampleSpacingMeters, fallback.SmoothingSampleSpacingMeters);
            tuning.MinNodesExpandedPerFrame = math.max(1, tuning.MinNodesExpandedPerFrame);
            tuning.MaxNodesExpandedPerFrame = math.max(tuning.MinNodesExpandedPerFrame, tuning.MaxNodesExpandedPerFrame);
            tuning.MaxStringPullLookAhead = math.max(1, tuning.MaxStringPullLookAhead);
            tuning.MaxLineSamplesPerSegment = math.max(1, tuning.MaxLineSamplesPerSegment);
            tuning.MaxRawPathNodes = math.max(2, tuning.MaxRawPathNodes);
            tuning.MaxWaypoints = math.max(2, tuning.MaxWaypoints);
            tuning.TimeSliceBudgetMs = FinitePositiveOrFallback(tuning.TimeSliceBudgetMs, fallback.TimeSliceBudgetMs);
            tuning.VerticalPenalty = FinitePositiveOrFallback(tuning.VerticalPenalty, fallback.VerticalPenalty);
            return tuning;
        }

        private static int ResolveNodeBudget(in VoxelAStarTuningDTO tuning)
        {
            float q = Smooth01(tuning.GlobalQualityWeight);
            float budget = math.lerp(tuning.MinNodesExpandedPerFrame, tuning.MaxNodesExpandedPerFrame, q);
            return math.max(1, (int)math.round(budget));
        }

        private static float ResolveHeuristicWeight(in VoxelAStarTuningDTO tuning)
        {
            float q = Smooth01(tuning.GlobalQualityWeight);
            return math.lerp(tuning.MaximumHeuristicWeight, tuning.MinimumHeuristicWeight, q);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveRequiredRadius(float radius)
        {
            if (!math.isfinite(radius))
                return VoxelAStarConstants.MinimumRadiusMeters;
            return math.clamp(radius, VoxelAStarConstants.MinimumRadiusMeters, VoxelAStarConstants.MaximumRadiusMeters);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint NextSearchId(uint current)
        {
            uint next = current + 1u;
            return next == 0u ? 1u : next;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ResolveResultIndex(uint requesterHash, uint consumedRequests)
        {
            if (!Results.IsCreated || Results.Length <= 0)
                return 0;

            uint seed = requesterHash != 0u ? requesterHash : consumedRequests;
            int length = Results.Length;
            int start = (int)(seed % (uint)length);
            int oldestIndex = start;
            uint oldestFrame = uint.MaxValue;
            for (int offset = 0; offset < length; offset++)
            {
                int index = start + offset;
                if (index >= length)
                    index -= length;

                PathResultDTO candidate = Results[index];
                if (requesterHash != 0u && candidate.RequesterEntityHash == requesterHash)
                    return index;
                if (candidate.RequesterEntityHash == 0u || !IsTerminalResultStatus(candidate.Status))
                    return index;
                if (candidate.FrameCompleted < oldestFrame)
                {
                    oldestFrame = candidate.FrameCompleted;
                    oldestIndex = index;
                }
            }

            return oldestIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsTerminalResultStatus(byte status)
        {
            return status == VoxelPathStatus.Complete ||
                   status == VoxelPathStatus.Partial ||
                   status == VoxelPathStatus.Failed ||
                   status == VoxelPathStatus.InvalidInput ||
                   status == VoxelPathStatus.OutputOverflow;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float MovementCost(int dx, int dy, int dz, float voxelSize, float verticalPenalty)
        {
            float lengthSq = (dx * dx) + (dy * dy) + (dz * dz);
            float cost = VoxelAStarConstants.FastLengthFromSq(lengthSq) * voxelSize;
            float vertical = 1f + (math.max(1f, verticalPenalty) - 1f) * math.abs(dy);
            return cost * vertical;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float HeuristicCost(int3 a, int3 b, in VoxelPathSolverState state, in VoxelAStarTuningDTO tuning)
        {
            int3 gridDelta = a - b;
            float3 delta = new float3(gridDelta.x, gridDelta.y, gridDelta.z);
            delta.y *= math.max(1f, tuning.VerticalPenalty);
            return VoxelAStarConstants.FastLengthFromSq(math.lengthsq(delta)) * state.VoxelSizeMeters;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float HeuristicDistanceSq(int a, int b, int3 dims)
        {
            int3 delta = IndexToCoord(a, dims) - IndexToCoord(b, dims);
            return math.lengthsq(new float3(delta.x, delta.y, delta.z));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryPointToIndex(float3 local, int3 dims, float voxelSize, out int index)
        {
            index = -1;
            if (!math.all(math.isfinite(local)) || voxelSize < VoxelAStarConstants.MinimumVoxelSizeMeters)
                return false;

            float invVoxelSize = math.rcp(voxelSize);
            int3 coord = new int3(
                (int)math.floor(local.x * invVoxelSize),
                (int)math.floor(local.y * invVoxelSize),
                (int)math.floor(local.z * invVoxelSize));
            if (!IsCoordValid(coord, dims))
                return false;

            index = CoordToIndex(coord, dims);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SafeVolume(int3 dims)
        {
            if (dims.x <= 0 || dims.y <= 0 || dims.z <= 0)
                return 0;
            if (dims.x > 512 || dims.y > 512 || dims.z > 512)
                return 0;
            return dims.x * dims.y * dims.z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsNodeIndexValid(int index, in VoxelPathSolverState state)
        {
            int volume = SafeVolume(state.Dimensions);
            return index >= 0 && index < volume;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsCoordValid(int3 coord, int3 dims)
        {
            return coord.x >= 0 && coord.y >= 0 && coord.z >= 0 &&
                   coord.x < dims.x && coord.y < dims.y && coord.z < dims.z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CoordToIndex(int3 coord, int3 dims)
        {
            return coord.x + (coord.y * dims.x) + (coord.z * dims.x * dims.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int3 IndexToCoord(int index, int3 dims)
        {
            int x = index % dims.x;
            int yz = index / dims.x;
            int y = yz % dims.y;
            int z = yz / dims.y;
            return new int3(x, y, z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ClampRingCursor(int cursor, int length)
        {
            if (length <= 0)
                return 0;
            if (cursor < 0)
                return 0;
            return cursor < length ? cursor : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int AdvanceRingCursor(int cursor, int length)
        {
            if (length <= 1)
                return 0;
            int next = cursor + 1;
            return next < length ? next : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Smooth01(float value)
        {
            float q = SanitizeQuality(value);
            return q * q * (3f - (2f * q));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeQuality(float value)
        {
            return math.saturate(math.select(1f, value, math.isfinite(value)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FinitePositiveOrFallback(float value, float fallback)
        {
            return math.isfinite(value) && value > 0f ? value : fallback;
        }
    }

    /// <summary>
    /// SDF line-of-sight string-pulling pass over the raw voxel chain.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct SmoothPathStringPullingJob : IJob
    {
        [NoAlias] public NativeArray<VoxelPathSolverState> SolverState;
        [ReadOnly, NoAlias] public NativeArray<float> SdfDistances;
        [ReadOnly, NoAlias] public NativeArray<VoxelSdfGridHeader> GridHeader;
        [ReadOnly, NoAlias] public NativeArray<int> RawPath;
        [NoAlias] public NativeArray<VoxelPathWaypointDTO> Waypoints;
        [NoAlias] public NativeArray<PathResultDTO> Results;
        [NoAlias] public NativeArray<PathfindingTelemetryEntry> Telemetry;
        [ReadOnly, NoAlias] public NativeArray<VoxelAStarTuningDTO> Tuning;
        public uint Frame;
        public float GlobalQualityWeight;

        /// <inheritdoc />
        public void Execute()
        {
            if (!SolverState.IsCreated ||
                SolverState.Length <= 0 ||
                !GridHeader.IsCreated ||
                GridHeader.Length <= 0 ||
                !SdfDistances.IsCreated ||
                !RawPath.IsCreated ||
                !Waypoints.IsCreated ||
                !Results.IsCreated ||
                !Tuning.IsCreated ||
                Tuning.Length <= 0)
            {
                return;
            }

            VoxelPathSolverState state = SolverState[0];
            if (state.Status != VoxelPathStatus.RawPathReady || state.RawPathCount <= 0)
                return;

            VoxelAStarTuningDTO tuning = SanitizeTuning(Tuning[0], GlobalQualityWeight);
            VoxelSdfGridHeader header = GridHeader[0];
            int rawCount = math.min(state.RawPathCount, RawPath.Length);
            int resultIndex = Results.IsCreated && Results.Length > 0 ? math.clamp((int)state.ResultIndex, 0, Results.Length - 1) : 0;
            int segmentCapacity = ResolveWaypointSegmentCapacity(Waypoints.Length, Results.IsCreated ? Results.Length : 1);
            int waypointStart = Waypoints.Length > 0 ? math.min(Waypoints.Length - 1, resultIndex * segmentCapacity) : 0;
            int maxWaypoints = math.min(segmentCapacity, tuning.MaxWaypoints);
            uint flags = state.Flags;
            int waypointCount = 0;

            if (rawCount <= 0 || maxWaypoints <= 0)
            {
                flags |= VoxelPathFlags.WaypointOverflow;
                Finish(ref state, VoxelPathStatus.OutputOverflow, flags, waypointStart, waypointCount);
                return;
            }

            int current = 0;
            AppendWaypoint(ref waypointCount, waypointStart, RawPath[0], in header, ref flags, maxWaypoints);
            int guard = 0;
            while (current < rawCount - 1 && guard <= rawCount)
            {
                int farthest = math.min(rawCount - 1, current + math.max(1, tuning.MaxStringPullLookAhead));
                int accepted = current + 1;
                for (int candidate = farthest; candidate > current; candidate--)
                {
                    if (HasLineOfSight(RawPath[current], RawPath[candidate], in header, in state, in tuning))
                    {
                        accepted = candidate;
                        break;
                    }
                }

                AppendWaypoint(ref waypointCount, waypointStart, RawPath[accepted], in header, ref flags, maxWaypoints);
                current = accepted;
                guard++;
                if ((flags & VoxelPathFlags.WaypointOverflow) != 0)
                    break;
            }

            byte status = (flags & VoxelPathFlags.WaypointOverflow) != 0
                ? VoxelPathStatus.OutputOverflow
                : ((flags & VoxelPathFlags.PartialNearestFallback) != 0 ? VoxelPathStatus.Partial : VoxelPathStatus.Complete);
            Finish(ref state, status, flags, waypointStart, waypointCount);
        }

        private bool HasLineOfSight(
            int startIndex,
            int endIndex,
            in VoxelSdfGridHeader header,
            in VoxelPathSolverState state,
            in VoxelAStarTuningDTO tuning)
        {
            float3 a = NodeCenter(startIndex, header.Dimensions, header.VoxelSizeMeters);
            float3 b = NodeCenter(endIndex, header.Dimensions, header.VoxelSizeMeters);
            float distance = VoxelAStarConstants.FastLengthFromSq(math.lengthsq(b - a));
            if (!math.isfinite(distance))
                return false;

            float invSampleSpacing = math.rcp(math.max(VoxelAStarConstants.LineSampleEpsilon, tuning.SmoothingSampleSpacingMeters));
            int samples = math.clamp(
                (int)math.ceil(distance * invSampleSpacing),
                1,
                math.max(1, tuning.MaxLineSamplesPerSegment));
            float invSampleCount = math.rcp((float)(samples + 1));
            for (int i = 1; i <= samples; i++)
            {
                float t = i * invSampleCount;
                float3 p = math.lerp(a, b, t);
                if (!TryPointToIndex(p, header.Dimensions, header.VoxelSizeMeters, out int sampleIndex))
                    return false;
                if (sampleIndex < 0 || sampleIndex >= SdfDistances.Length || SdfDistances[sampleIndex] < state.RequiredRadius)
                    return false;
            }

            return true;
        }

        private void AppendWaypoint(
            ref int waypointCount,
            int waypointStart,
            int nodeIndex,
            in VoxelSdfGridHeader header,
            ref uint flags,
            int maxWaypoints)
        {
            if (waypointStart < 0 || waypointStart >= Waypoints.Length)
            {
                flags |= VoxelPathFlags.WaypointOverflow;
                return;
            }

            if (waypointCount > 0 && Waypoints[waypointStart + waypointCount - 1].NodeIndex == (uint)math.max(0, nodeIndex))
                return;

            if (waypointCount >= maxWaypoints || waypointStart + waypointCount >= Waypoints.Length)
            {
                flags |= VoxelPathFlags.WaypointOverflow;
                return;
            }

            float3 local = NodeCenter(nodeIndex, header.Dimensions, header.VoxelSizeMeters);
            double3 absolute = header.OriginAUP + new double3(local.x, local.y, local.z);
            if (!math.all(math.isfinite(absolute)))
            {
                flags |= VoxelPathFlags.NaNDetected;
                absolute = header.OriginAUP;
            }

            Waypoints[waypointStart + waypointCount] = new VoxelPathWaypointDTO
            {
                PositionAUP = absolute,
                NodeIndex = (uint)math.max(0, nodeIndex),
                Flags = flags
            };
            waypointCount++;
        }

        private void Finish(ref VoxelPathSolverState state, byte status, uint flags, int waypointStart, int waypointCount)
        {
            state.Status = status;
            state.Active = 0;
            state.WaypointCount = waypointCount;
            state.Flags = flags;
            state.FrameUpdated = Frame == 0u ? state.FrameUpdated + 1u : Frame;
            SolverState[0] = state;

            if (Results.IsCreated && Results.Length > 0)
            {
                int index = math.clamp((int)state.ResultIndex, 0, Results.Length - 1);
                PathResultDTO result = Results[index];
                result.ResultFlags = flags;
                result.FrameCompleted = state.FrameUpdated;
                result.WaypointStart = waypointStart;
                result.WaypointCount = waypointCount;
                result.RawPathCount = state.RawPathCount;
                result.NodesExpandedTotal = state.NodesExpandedTotal;
                result.NodesExpandedLastFrame = state.NodesExpandedLastFrame;
                result.BestNodeIndex = state.BestNodeIndex;
                result.Status = status;
                result.RequiredRadius = state.RequiredRadius;
                result.HeuristicWeight = state.HeuristicWeight;
                result.QualityWeight = SanitizeQuality(GlobalQualityWeight);
                result.SearchId = state.SearchId;
                result.StartAUP = state.Request.StartAUP;
                result.EndAUP = state.Request.EndAUP;
                Results[index] = result;
            }

            WriteTelemetry(in state, flags);
        }

        private static int ResolveWaypointSegmentCapacity(int waypointLength, int resultLength)
        {
            if (waypointLength <= 0)
                return 0;

            int safeResults = math.max(1, resultLength);
            return math.max(1, waypointLength / safeResults);
        }

        private void WriteTelemetry(in VoxelPathSolverState state, uint flags)
        {
            if (!Telemetry.IsCreated || Telemetry.Length <= 0)
                return;

            int length = math.min(Telemetry.Length, VoxelAStarConstants.TelemetryFrames);
            int cursor = (int)((Frame == 0u ? state.FrameUpdated : Frame) % (uint)math.max(1, length));
            Telemetry[cursor] = new PathfindingTelemetryEntry
            {
                Frame = Frame == 0u ? state.FrameUpdated : Frame,
                NodesExpanded = (uint)math.max(0, state.NodesExpandedLastFrame),
                AverageNodesExpanded = ResolveAverageNodesExpanded(in state, Frame),
                BurstMicros = 0u,
                Flags = flags,
                SearchId = state.SearchId,
                RequesterEntityHash = state.Request.RequesterEntityHash,
                QualityWeight = SanitizeQuality(GlobalQualityWeight),
                HeuristicWeight = state.HeuristicWeight,
                RawPathCount = (ushort)math.min(math.max(0, state.RawPathCount), ushort.MaxValue),
                WaypointCount = (ushort)math.min(math.max(0, state.WaypointCount), ushort.MaxValue)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ResolveAverageNodesExpanded(in VoxelPathSolverState state, uint frame)
        {
            uint start = state.FrameStarted == 0u ? 1u : state.FrameStarted;
            uint current = frame == 0u ? (state.FrameUpdated > start ? state.FrameUpdated : start) : frame;
            uint frames = current >= start ? current - start + 1u : 1u;
            uint total = (uint)math.max(0, state.NodesExpandedTotal);
            return total / (frames == 0u ? 1u : frames);
        }

        private static VoxelAStarTuningDTO SanitizeTuning(VoxelAStarTuningDTO tuning, float globalQualityWeight)
        {
            VoxelAStarTuningDTO fallback = VoxelAStarTuningDTO.Default();
            tuning.GlobalQualityWeight = SanitizeQuality(math.min(SanitizeQuality(globalQualityWeight), SanitizeQuality(tuning.GlobalQualityWeight)));
            tuning.SmoothingSampleSpacingMeters = FinitePositiveOrFallback(tuning.SmoothingSampleSpacingMeters, fallback.SmoothingSampleSpacingMeters);
            tuning.MaxStringPullLookAhead = math.max(1, tuning.MaxStringPullLookAhead);
            tuning.MaxLineSamplesPerSegment = math.max(1, tuning.MaxLineSamplesPerSegment);
            tuning.MaxWaypoints = math.max(2, tuning.MaxWaypoints);
            return tuning;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 NodeCenter(int index, int3 dims, float voxelSize)
        {
            int3 c = IndexToCoord(index, dims);
            return (new float3(c.x, c.y, c.z) + 0.5f) * voxelSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryPointToIndex(float3 local, int3 dims, float voxelSize, out int index)
        {
            index = -1;
            if (!math.all(math.isfinite(local)) || voxelSize < VoxelAStarConstants.MinimumVoxelSizeMeters)
                return false;

            float invVoxelSize = math.rcp(voxelSize);
            int3 coord = new int3(
                (int)math.floor(local.x * invVoxelSize),
                (int)math.floor(local.y * invVoxelSize),
                (int)math.floor(local.z * invVoxelSize));
            if (coord.x < 0 || coord.y < 0 || coord.z < 0 || coord.x >= dims.x || coord.y >= dims.y || coord.z >= dims.z)
                return false;

            index = coord.x + (coord.y * dims.x) + (coord.z * dims.x * dims.y);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int3 IndexToCoord(int index, int3 dims)
        {
            int x = index % dims.x;
            int yz = index / dims.x;
            int y = yz % dims.y;
            int z = yz / dims.y;
            return new int3(x, y, z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeQuality(float value)
        {
            return math.saturate(math.select(1f, value, math.isfinite(value)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FinitePositiveOrFallback(float value, float fallback)
        {
            return math.isfinite(value) && value > 0f ? value : fallback;
        }
    }

    /// <summary>
    /// Cold CSV parser for fauna pathing profiles. It writes into caller-owned native arrays.
    /// </summary>
#if UNITY_EDITOR
    public static class VoxelPathingProfileCsvParser
    {
        /// <summary>
        /// Parses `species,radius,max_nodes,heuristic_scale,lookahead,flags` rows without managed row objects.
        /// </summary>
        public static bool TryParse(
            ReadOnlySpan<byte> bytes,
            NativeArray<VoxelPathingProfileDTO> profiles,
            NativeArray<int> profileCount,
            out uint flags)
        {
            flags = 0u;
            if (!profileCount.IsCreated || profileCount.Length <= 0)
                return false;

            bool parsed = TryParse(bytes, profiles, out int written, out flags);
            profileCount[0] = written;
            return parsed;
        }

        /// <summary>
        /// Parses profile rows without requiring the caller to hold the profile-count writer lock.
        /// </summary>
        public static bool TryParse(
            ReadOnlySpan<byte> bytes,
            NativeArray<VoxelPathingProfileDTO> profiles,
            out int written,
            out uint flags)
        {
            flags = 0u;
            written = 0;
            if (!profiles.IsCreated || profiles.Length <= 0)
                return false;

            int cursor = 0;
            bool sawData = false;
            while (cursor < bytes.Length)
            {
                int lineStart = cursor;
                while (cursor < bytes.Length && bytes[cursor] != (byte)'\n' && bytes[cursor] != (byte)'\r')
                    cursor++;

                ReadOnlySpan<byte> line = TrimAscii(bytes.Slice(lineStart, cursor - lineStart));
                while (cursor < bytes.Length && (bytes[cursor] == (byte)'\n' || bytes[cursor] == (byte)'\r'))
                    cursor++;

                if (line.Length == 0 || line[0] == (byte)'#')
                    continue;

                if (!sawData && LooksLikeHeader(line))
                {
                    sawData = true;
                    continue;
                }

                sawData = true;
                if (written >= profiles.Length)
                {
                    flags |= VoxelPathFlags.CsvProfileOverflow;
                    break;
                }

                if (TryParseLine(line, out VoxelPathingProfileDTO profile))
                {
                    profiles[written] = profile;
                    written++;
                }
            }

            return written > 0 && (flags & VoxelPathFlags.CsvProfileOverflow) == 0;
        }

        /// <summary>
        /// Parses profile rows into caller-owned stack or managed-free staging memory.
        /// </summary>
        public static bool TryParse(
            ReadOnlySpan<byte> bytes,
            Span<VoxelPathingProfileDTO> profiles,
            out int written,
            out uint flags)
        {
            flags = 0u;
            written = 0;
            if (profiles.Length <= 0)
                return false;

            int cursor = 0;
            bool sawData = false;
            while (cursor < bytes.Length)
            {
                int lineStart = cursor;
                while (cursor < bytes.Length && bytes[cursor] != (byte)'\n' && bytes[cursor] != (byte)'\r')
                    cursor++;

                ReadOnlySpan<byte> line = TrimAscii(bytes.Slice(lineStart, cursor - lineStart));
                while (cursor < bytes.Length && (bytes[cursor] == (byte)'\n' || bytes[cursor] == (byte)'\r'))
                    cursor++;

                if (line.Length == 0 || line[0] == (byte)'#')
                    continue;

                if (!sawData && LooksLikeHeader(line))
                {
                    sawData = true;
                    continue;
                }

                sawData = true;
                if (written >= profiles.Length)
                {
                    flags |= VoxelPathFlags.CsvProfileOverflow;
                    break;
                }

                if (TryParseLine(line, out VoxelPathingProfileDTO profile))
                {
                    profiles[written] = profile;
                    written++;
                }
            }

            return written > 0 && (flags & VoxelPathFlags.CsvProfileOverflow) == 0;
        }

        private static bool TryParseLine(ReadOnlySpan<byte> line, out VoxelPathingProfileDTO profile)
        {
            profile = default;
            ReadOnlySpan<byte> species = NextToken(line, 0, out int cursor);
            if (species.Length == 0)
                return false;

            ReadOnlySpan<byte> radius = NextToken(line, cursor, out cursor);
            ReadOnlySpan<byte> maxNodes = NextToken(line, cursor, out cursor);
            ReadOnlySpan<byte> heuristicScale = NextToken(line, cursor, out cursor);
            ReadOnlySpan<byte> lookAhead = NextToken(line, cursor, out cursor);
            ReadOnlySpan<byte> flags = NextToken(line, cursor, out _);

            profile.SpeciesHash = ParseHashOrToken(species);
            profile.RequiredRadiusMeters = ClampFloat(ParseFloat(radius, 1f), VoxelAStarConstants.MinimumRadiusMeters, VoxelAStarConstants.MaximumRadiusMeters);
            profile.MaxNodesExpandedPerFrame = Math.Max(1, ParseInt(maxNodes, 256));
            profile.HeuristicWeightScale = ClampFloat(ParseFloat(heuristicScale, 1f), 0.25f, 4f);
            profile.MaxStringPullLookAhead = Math.Max(1, ParseInt(lookAhead, 16));
            profile.Flags = ParseUInt(flags, 0u);
            return profile.SpeciesHash != 0u;
        }

        private static ReadOnlySpan<byte> NextToken(ReadOnlySpan<byte> line, int start, out int next)
        {
            int i = Math.Min(Math.Max(0, start), line.Length);
            int tokenStart = i;
            while (i < line.Length && line[i] != (byte)',')
                i++;

            next = i < line.Length ? i + 1 : line.Length;
            return TrimAscii(line.Slice(tokenStart, i - tokenStart));
        }

        private static ReadOnlySpan<byte> TrimAscii(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && IsAsciiWhitespace(value[start]))
                start++;
            while (end >= start && IsAsciiWhitespace(value[end]))
                end--;
            return start <= end ? value.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static bool IsAsciiWhitespace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t' || value == (byte)'\r' || value == (byte)'\n';
        }

        private static bool LooksLikeHeader(ReadOnlySpan<byte> line)
        {
            ReadOnlySpan<byte> first = NextToken(line, 0, out _);
            return EqualsAscii(first, "species") || EqualsAscii(first, "species_hash");
        }

        private static uint ParseHashOrToken(ReadOnlySpan<byte> token)
        {
            if (TryParseUInt(token, out uint value))
                return value;

            uint hash = 2166136261u;
            for (int i = 0; i < token.Length; i++)
            {
                byte c = token[i];
                if (c >= (byte)'A' && c <= (byte)'Z')
                    c = (byte)(c + 32);
                hash ^= c;
                hash *= 16777619u;
            }

            return hash == 0u ? 1u : hash;
        }

        private static int ParseInt(ReadOnlySpan<byte> token, int fallback)
        {
            if (!TryParseUInt(token, out uint value))
                return fallback;
            return value > int.MaxValue ? int.MaxValue : (int)value;
        }

        private static uint ParseUInt(ReadOnlySpan<byte> token, uint fallback)
        {
            return TryParseUInt(token, out uint value) ? value : fallback;
        }

        private static bool TryParseUInt(ReadOnlySpan<byte> token, out uint value)
        {
            value = 0u;
            if (token.Length <= 0)
                return false;

            int i = 0;
            if (token.Length > 2 && token[0] == (byte)'0' && (token[1] == (byte)'x' || token[1] == (byte)'X'))
            {
                i = 2;
                for (; i < token.Length; i++)
                {
                    byte c = token[i];
                    uint digit;
                    if (c >= (byte)'0' && c <= (byte)'9')
                        digit = (uint)(c - (byte)'0');
                    else if (c >= (byte)'a' && c <= (byte)'f')
                        digit = (uint)(c - (byte)'a' + 10);
                    else if (c >= (byte)'A' && c <= (byte)'F')
                        digit = (uint)(c - (byte)'A' + 10);
                    else
                        return false;

                    value = (value << 4) | digit;
                }

                return i > 2;
            }

            for (; i < token.Length; i++)
            {
                byte c = token[i];
                if (c < (byte)'0' || c > (byte)'9')
                    return false;

                value = (value * 10u) + (uint)(c - (byte)'0');
            }

            return true;
        }

        private static float ParseFloat(ReadOnlySpan<byte> token, float fallback)
        {
            if (token.Length <= 0)
                return fallback;

            int i = 0;
            float sign = 1f;
            if (token[i] == (byte)'-')
            {
                sign = -1f;
                i++;
            }
            else if (token[i] == (byte)'+')
            {
                i++;
            }

            float value = 0f;
            bool any = false;
            while (i < token.Length && token[i] >= (byte)'0' && token[i] <= (byte)'9')
            {
                value = (value * 10f) + (token[i] - (byte)'0');
                i++;
                any = true;
            }

            if (i < token.Length && token[i] == (byte)'.')
            {
                i++;
                float scale = 0.1f;
                while (i < token.Length && token[i] >= (byte)'0' && token[i] <= (byte)'9')
                {
                    value += (token[i] - (byte)'0') * scale;
                    scale *= 0.1f;
                    i++;
                    any = true;
                }
            }

            if (!any)
                return fallback;

            return sign * value;
        }

        private static float ClampFloat(float value, float min, float max)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return min;
            if (value < min)
                return min;
            return value > max ? max : value;
        }

        private static bool EqualsAscii(ReadOnlySpan<byte> token, string text)
        {
            if (token.Length != text.Length)
                return false;

            for (int i = 0; i < token.Length; i++)
            {
                byte c = token[i];
                if (c >= (byte)'A' && c <= (byte)'Z')
                    c = (byte)(c + 32);
                if (c != (byte)text[i])
                    return false;
            }

            return true;
        }
    }
#endif
}
