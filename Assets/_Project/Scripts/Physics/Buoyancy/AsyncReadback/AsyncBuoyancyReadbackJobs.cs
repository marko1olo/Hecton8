using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Physics
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct GenerateMockAsyncReadbackJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<ReadbackRequestDTO> Requests;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // This job is an IJob, not an IJobParallelFor, so one worker owns the mock ring, completed
        // request lane, and counter lane for the whole Execute call. The write ranges are bounded by
        // safeCapacity, ring slot, and writable/readable counts before unsafe pointer stores occur.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // [NoAlias] records that Requests, MockRing, CompletedRequests, and Counters come from separate
        // Vault buffers. Rejected per-request NativeQueue writes because this mock readback path must stay
        // deterministic and free of atomic/queue contention.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // MockRing writes use [writeBase, writeBase + writable); CompletedRequests writes use [0, readable);
        // Counters writes only element zero after the data copy. No parallel worker can write the same row
        // because the job has serial ownership.
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<ReadbackRequestDTO> MockRing;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<ReadbackRequestDTO> CompletedRequests;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<AsyncReadbackCounterDTO> Counters;
        public int RequestCount;
        public int Capacity;
        public int RingSize;
        public int WriteSlot;
        public int LatencyFrames;
        public uint FrameIndex;
        public float TimeSeconds;

        public unsafe void Execute()
        {
            if (!Requests.IsCreated || !MockRing.IsCreated || !CompletedRequests.IsCreated || Capacity <= 0 || RingSize <= 0)
                return;

            int safeCapacity = math.min(Capacity, math.min(Requests.Length, CompletedRequests.Length));
            int count = math.clamp(RequestCount, 0, safeCapacity);
            int safeWriteSlot = math.clamp(WriteSlot, 0, RingSize - 1);
            int ringCapacity = math.min(MockRing.Length, safeCapacity * RingSize);
            int writeBase = safeWriteSlot * safeCapacity;
            int writable = math.max(0, math.min(count, ringCapacity - writeBase));
            ReadbackRequestDTO* ringPtr = (ReadbackRequestDTO*)MockRing.GetUnsafePtr();

            for (int i = 0; i < writable; i++)
            {
                ReadbackRequestDTO request = Requests[i];
                request.ResultHeight = AsyncBuoyancyReadbackMath.ResolveMockLocalHeight(
                    request.LocalXZ,
                    FrameIndex,
                    TimeSeconds);
                ref ReadbackRequestDTO ringRef = ref UnsafeUtility.AsRef<ReadbackRequestDTO>(ringPtr + writeBase + i);
                ringRef = request;
            }

            int completed = 0;
            int latency = math.max(1, LatencyFrames);
            if (FrameIndex >= (uint)latency)
            {
                int readSlot = safeWriteSlot - latency;
                while (readSlot < 0)
                    readSlot += RingSize;
                readSlot %= RingSize;
                int readBase = readSlot * safeCapacity;
                int readable = math.max(0, math.min(count, ringCapacity - readBase));
                ReadbackRequestDTO* completedPtr = (ReadbackRequestDTO*)CompletedRequests.GetUnsafePtr();
                for (int i = 0; i < readable; i++)
                {
                    ReadbackRequestDTO delayed = UnsafeUtility.AsRef<ReadbackRequestDTO>(ringPtr + readBase + i);
                    ref ReadbackRequestDTO completedRef = ref UnsafeUtility.AsRef<ReadbackRequestDTO>(completedPtr + i);
                    completedRef = delayed;
                }

                completed = readable;
            }

            if (Counters.IsCreated && Counters.Length > 0)
            {
                AsyncReadbackCounterDTO* counterPtr = (AsyncReadbackCounterDTO*)Counters.GetUnsafePtr();
                ref AsyncReadbackCounterDTO counter = ref UnsafeUtility.AsRef<AsyncReadbackCounterDTO>(counterPtr);
                counter.DispatchCount = count;
                counter.CompletedCount = completed;
                counter.LastLatencyFrames = latency;
                counter.FrameIndex = FrameIndex;
                counter.Flags |= AsyncBuoyancyReadbackConstants.FlagMockPath;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct ApplyDelayedBuoyancyReadbackJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<ReadbackRequestDTO> CompletedRequests;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Each ParallelFor lane writes ResolvedHeights[index] and ResultStates[index]. The only non-indexed
        // write is Counters[0], guarded by index == 0, so a single worker owns the aggregate update.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // ResolvedHeights, ResultStates, and Counters are disjoint Vault buffers. [NoAlias] documents the
        // ownership proof for Burst; the restriction is limited to pointer-based row access and the
        // single-lane counter update.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // Rejected a per-lane counter array because it would add a reduction pass for one max-stale sample.
        // The current design keeps row writes partitioned by index and keeps the counter write on lane zero.
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<ReadbackResolvedHeightDTO> ResolvedHeights;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<ReadbackResultStateDTO> ResultStates;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<AsyncReadbackCounterDTO> Counters;
        public double CameraAupY;
        public float FixedDeltaTime;
        public float SmoothingAlpha;
        public float DeadReckoningDecayRate;
        public int CompletedCount;
        public int MaxFreshAgeFrames;
        public uint FrameIndex;

        public unsafe void Execute(int index)
        {
            if (!CompletedRequests.IsCreated || !ResolvedHeights.IsCreated || !ResultStates.IsCreated)
                return;

            int stateCount = math.min(ResolvedHeights.Length, ResultStates.Length);
            if ((uint)index >= (uint)stateCount)
                return;

            bool hasFresh = index < math.min(math.max(0, CompletedCount), CompletedRequests.Length);
            float dt = math.max(0.0001f, FixedDeltaTime);
            float invDt = math.rcp(math.max(dt, 0.0001f));
            float alpha = math.saturate(SmoothingAlpha);
            float decay = math.saturate(DeadReckoningDecayRate);
            ReadbackResultStateDTO* statePtr = (ReadbackResultStateDTO*)ResultStates.GetUnsafePtr();
            ReadbackResolvedHeightDTO* resolvedPtr = (ReadbackResolvedHeightDTO*)ResolvedHeights.GetUnsafePtr();
            ref ReadbackResultStateDTO state = ref UnsafeUtility.AsRef<ReadbackResultStateDTO>(statePtr + index);
            ref ReadbackResolvedHeightDTO resolved = ref UnsafeUtility.AsRef<ReadbackResolvedHeightDTO>(resolvedPtr + index);

            uint flags = AsyncBuoyancyReadbackConstants.FlagActive;
            float localHeight;
            uint entityHash = state.EntityHash;
            if (hasFresh)
            {
                ReadbackRequestDTO request = CompletedRequests[index];
                entityHash = request.EntityHash;
                float observed = math.isfinite(request.ResultHeight) ? request.ResultHeight : state.LastLocalHeight;
                float previous = math.isfinite(state.SmoothedLocalHeight) ? state.SmoothedLocalHeight : observed;
                float predicted = previous + (state.VelocityY * dt);
                localHeight = math.lerp(predicted, observed, alpha);
                float velocity = (localHeight - previous) * invDt;

                state.PreviousLocalHeight = previous;
                state.LastLocalHeight = observed;
                state.SmoothedLocalHeight = localHeight;
                state.DeadReckonedLocalHeight = localHeight;
                state.VelocityY = math.isfinite(velocity) ? velocity : 0f;
                state.LastLocalX = request.LocalXZ.x;
                state.LastLocalZ = request.LocalXZ.y;
                state.EntityHash = entityHash;
                state.LastFrameIndex = FrameIndex;
                state.StaleFrames = 0;
                state.CameraAupY = CameraAupY;
                state.Flags = flags;
            }
            else
            {
                int stale = math.max(0, state.StaleFrames + 1);
                state.StaleFrames = stale;
                float predicted = state.SmoothedLocalHeight + (state.VelocityY * dt * math.min(stale, math.max(1, MaxFreshAgeFrames)));
                float staleFactor = math.saturate((float)math.max(0, stale - MaxFreshAgeFrames) * math.rcp(math.max(1f, MaxFreshAgeFrames)));
                localHeight = math.lerp(predicted, state.SmoothedLocalHeight, staleFactor * decay);
                state.DeadReckonedLocalHeight = localHeight;
                state.VelocityY *= math.lerp(1f, 0.65f, staleFactor);
                flags |= AsyncBuoyancyReadbackConstants.FlagStale;
                if (stale > MaxFreshAgeFrames)
                    flags |= AsyncBuoyancyReadbackConstants.FlagDeadReckoned;
                state.Flags = flags;
            }

            double heightAupY = CameraAupY + localHeight;
            bool finite = math.isfinite(localHeight) && math.isfinite(heightAupY);
            if (!finite)
            {
                localHeight = 0f;
                heightAupY = CameraAupY;
                flags |= AsyncBuoyancyReadbackConstants.FlagNonFinite;
            }

            state.LastHeightAupY = heightAupY;
            resolved.HeightAupY = heightAupY;
            resolved.LocalHeight = localHeight;
            resolved.VelocityY = state.VelocityY;
            resolved.EntityHash = entityHash;
            resolved.FrameIndex = FrameIndex;
            resolved.Flags = flags;

            if (Counters.IsCreated && Counters.Length > 0 && index == 0)
            {
                AsyncReadbackCounterDTO* counterPtr = (AsyncReadbackCounterDTO*)Counters.GetUnsafePtr();
                ref AsyncReadbackCounterDTO counter = ref UnsafeUtility.AsRef<AsyncReadbackCounterDTO>(counterPtr);
                counter.MaxStaleFrames = math.max(counter.MaxStaleFrames, state.StaleFrames);
                counter.LastEntityHash = entityHash;
                counter.LastLocalHeight = localHeight;
                counter.Flags |= flags;
            }
        }
    }

    public static class AsyncBuoyancyReadbackMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveSampleBudget(int minSampleCount, int maxSampleCount)
        {
            int safeMin = math.max(1, minSampleCount);
            int safeMax = math.max(safeMin, maxSampleCount);
            return safeMax;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveSmoothingAlpha()
        {
            return 0.52f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveMockLocalHeight(float2 localXz, uint frameIndex, float timeSeconds)
        {
            float coarse = TriangleSigned((localXz.x * 0.013671875f) + (frameIndex * 0.0078125f));
            float cross = TriangleSigned((localXz.y * 0.0107421875f) - (timeSeconds * 0.041666667f));
            float ripple = TriangleSigned(((localXz.x + localXz.y) * 0.03125f) + (frameIndex * 0.01953125f));
            return (coarse * 0.62f) + (cross * 0.31f) + (ripple * 0.18f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float TriangleSigned(float value)
        {
            float wrapped = value - math.floor(value);
            return (math.abs((wrapped * 2f) - 1f) * 2f) - 1f;
        }
    }
}
