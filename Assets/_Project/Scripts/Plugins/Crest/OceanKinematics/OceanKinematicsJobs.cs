using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Physics
{
    internal static class OceanKinematicsSimdMath
    {
        private const float HalfPi = 1.57079632679f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SinPolynomial(float radians, float qualityWeight)
        {
            float safeRadians = math.select(0f, radians, math.isfinite(radians));
            float x = safeRadians - math.floor((safeRadians + math.PI) * OceanKinematicsConstants.RcpTwoPi) * OceanKinematicsConstants.TwoPi;
            x = math.select(x, math.PI - x, x > HalfPi);
            x = math.select(x, -math.PI - x, x < -HalfPi);
            float x2 = x * x;
            float x4 = x2 * x2;
            float sin3 = x * (1f - (x2 * 0.16666666667f));
            float sin7 = x * (1f - (x2 * 0.16666666667f) + (x4 * 0.00833333333f) - (x4 * x2 * 0.00019841269f));
            float q = math.saturate(math.select(1f, qualityWeight, math.isfinite(qualityWeight)));
            return math.lerp(sin3, sin7, q * q * (3f - 2f * q));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CosPolynomial(float radians, float qualityWeight)
        {
            return SinPolynomial(radians + HalfPi, qualityWeight);
        }
    }

    public static unsafe class OceanKinematicsHashUtility
    {
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ResolveRequestHash(in OceanKinematicsSampleRequestDTO request)
        {
            if (request.RequestHash != 0u)
                return request.RequestHash;

            double3 requestedAup = math.select(double3.zero, request.RequestedAUP, math.isfinite(request.RequestedAUP));
            uint hash = FnvOffset;
            hash = Mix(hash, AsUInt64(requestedAup.x));
            hash = Mix(hash, AsUInt64(requestedAup.y));
            hash = Mix(hash, AsUInt64(requestedAup.z));
            return math.select(1u, hash, hash != 0u);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Mix(uint hash, ulong value)
        {
            hash = Mix(hash, (uint)value);
            return Mix(hash, (uint)(value >> 32));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Mix(uint hash, uint value)
        {
            hash ^= value & 0xFFu;
            hash *= FnvPrime;
            hash ^= (value >> 8) & 0xFFu;
            hash *= FnvPrime;
            hash ^= (value >> 16) & 0xFFu;
            hash *= FnvPrime;
            hash ^= (value >> 24) & 0xFFu;
            hash *= FnvPrime;
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong AsUInt64(double value)
        {
            return *(ulong*)&value;
        }
    }

    /// <summary>
    /// Deterministic emergency ocean wave generator for isolated kinematics stress tests.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct GenerateMockOceanWavesJob : IJobParallelForBatch
    {
        [ReadOnly, NoAlias] public NativeArray<OceanKinematicsSampleRequestDTO> Requests;
        [ReadOnly, NoAlias] public NativeArray<int> RequestCounter;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Results are written to the ParallelFor index. Queued requests are compacted so the packed request
        // index is the authoritative result slot; caller-provided ResultIndex is metadata only in this pass.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // [NoAlias] proves Results is a separate Vault buffer from Requests and RequestCounter. Rejected
        // writing back into the request lane because ocean sampling must keep request identity immutable for
        // cache and telemetry proof.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // Every write validates resultIndex against Results.Length before pointer access. Tail or invalid
        // requests return without writing, preserving the one-request-to-one-result contract.
        [WriteOnly, NativeDisableParallelForRestriction, NoAlias] public NativeArray<FluidSampleResultDTO> Results;
        public OceanKinematicsTuningDTO Tuning;
        public int RequestCount;

        public void Execute(int startIndex, int count)
        {
            if (!Requests.IsCreated || !Results.IsCreated)
                return;

            int requestCount = math.min(math.max(0, ResolveRequestCount()), Requests.Length);
            int endIndex = math.min(startIndex + count, requestCount);
            if ((uint)startIndex >= (uint)endIndex)
                return;

            for (int index = startIndex; index < endIndex; index++)
                ExecuteIndex(index);
        }

        private void ExecuteIndex(int index)
        {
            OceanKinematicsSampleRequestDTO request = Requests[index];
            int resultIndex = index;
            if ((uint)resultIndex >= (uint)Results.Length)
                return;

            FluidSampleResultDTO result = default;
            if (!IsFinite(request.RequestedAUP))
            {
                result.WaterHeight = SanitizeFinite(Tuning.OceanSurfaceY, 0f);
                result.SurfaceVelocity = float3.zero;
                WriteResult(resultIndex, result);
                return;
            }

            double3 rootAup = math.select(double3.zero, Tuning.OceanRootAUP, math.isfinite(Tuning.OceanRootAUP));
            double3 deltaAup = request.RequestedAUP - rootAup;
            float3 local = ToFiniteFloat3(deltaAup);
            float surfaceY = SanitizeFinite(Tuning.OceanSurfaceY, 0f);
            float depthCull = math.max(0f, SanitizeFinite(Tuning.DepthCullingThresholdMeters, OceanKinematicsConstants.DefaultDepthCullMeters));
            if (surfaceY - local.y > depthCull)
            {
                result.WaterHeight = surfaceY;
                result.SurfaceVelocity = float3.zero;
                WriteResult(resultIndex, result);
                return;
            }

            float quality = Sanitize01(Tuning.GlobalQualityWeight);
            int octaveLimit = math.clamp(Tuning.MaxOctaveLimit, 1, 4);
            int activeOctaves = math.clamp((int)math.lerp(1f, octaveLimit, quality), 1, octaveLimit);
            float amplitudeMultiplier = math.max(0f, SanitizeFinite(Tuning.WaveAmplitudeMultiplier, OceanKinematicsConstants.DefaultAmplitudeMultiplier));
            float time = SanitizeFinite(Tuning.TimeSeconds, 0f);
            float height = surfaceY;
            float3 velocity = float3.zero;

            for (int octave = 0; octave < activeOctaves; octave++)
            {
                float2 dir = ResolveMockDirection(octave);
                float frequency = 0.18f + octave * 0.071f;
                float amplitude = amplitudeMultiplier * (0.42f * math.rcp(1f + octave * 0.65f));
                float speed = 0.35f + octave * 0.17f;
                float phase = WrapPhase(((local.x * dir.x) + (local.z * dir.y)) * frequency + time * speed + octave * 1.113f);
                float waveSin = OceanKinematicsSimdMath.SinPolynomial(phase, quality);
                float waveCos = OceanKinematicsSimdMath.CosPolynomial(phase, quality);
                height += waveCos * amplitude;
                velocity.x += dir.x * waveSin * amplitude * speed;
                velocity.z += dir.y * waveSin * amplitude * speed;
            }

            result.WaterHeight = SanitizeFinite(height, surfaceY);
            result.SurfaceVelocity = SanitizeFinite(velocity);
            WriteResult(resultIndex, result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ResolveRequestCount()
        {
            if (RequestCounter.IsCreated && RequestCounter.Length > OceanKinematicsConstants.QueueCounterPacked)
                return RequestCounter[OceanKinematicsConstants.QueueCounterPacked];

            return RequestCount;
        }

        private void WriteResult(int resultIndex, FluidSampleResultDTO result)
        {
            FluidSampleResultDTO* resultsPtr = (FluidSampleResultDTO*)Results.GetUnsafePtr();
            ref FluidSampleResultDTO target = ref UnsafeUtility.AsRef<FluidSampleResultDTO>(resultsPtr + resultIndex);
            target = result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float2 ResolveMockDirection(int octave)
        {
            float2 value = default;
            int lane = octave & 3;
            if (lane == 0)
            {
                value.x = 0.9238795f;
                value.y = 0.3826834f;
            }
            else if (lane == 1)
            {
                value.x = -0.3826834f;
                value.y = 0.9238795f;
            }
            else if (lane == 2)
            {
                value.x = 0.70710677f;
                value.y = -0.70710677f;
            }
            else
            {
                value.x = -0.8660254f;
                value.y = -0.5f;
            }

            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float WrapPhase(float phase)
        {
            return phase - math.floor((phase + math.PI) * OceanKinematicsConstants.RcpTwoPi) * OceanKinematicsConstants.TwoPi;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(double3 value)
        {
            return math.all(math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ToFiniteFloat3(double3 value)
        {
            float3 result = default;
            result.x = (float)value.x;
            result.y = (float)value.y;
            result.z = (float)value.z;
            return SanitizeFinite(result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeFinite(float value, float fallback)
        {
            return math.select(fallback, value, math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SanitizeFinite(float3 value)
        {
            return math.select(float3.zero, value, math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Sanitize01(float value)
        {
            return math.saturate(math.select(1f, value, math.isfinite(value)));
        }
    }

    /// <summary>
    /// Burst analytical Gerstner wave evaluator for AUP ocean kinematics.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct EvaluateAnalyticalWavesJob : IJobParallelForBatch
    {
        [ReadOnly, NoAlias] public NativeArray<OceanKinematicsSampleRequestDTO> Requests;
        [ReadOnly, NoAlias] public NativeArray<GerstnerWaveDTO> Waves;
        [ReadOnly, NoAlias] public NativeArray<int> RequestCounter;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Results are written to the ParallelFor index. Queue drain compacts requests so each active index is
        // unique; caller-provided ResultIndex is metadata only in this pass.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Requests, Waves, RequestCounter, and Results are separate Vault buffers. [NoAlias] allows Burst to
        // assume the Gerstner input lanes cannot alias the output payload lane.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The code bounds resultIndex before the unsafe store and drops invalid rows. Rejected a secondary
        // compaction pass because it would add another scratch buffer and duplicate owner-side coalescing.
        [WriteOnly, NativeDisableParallelForRestriction, NoAlias] public NativeArray<FluidSampleResultDTO> Results;
        public OceanKinematicsTuningDTO Tuning;
        public int RequestCount;
        public int WaveCount;

        public void Execute(int startIndex, int count)
        {
            if (!Requests.IsCreated || !Results.IsCreated)
                return;

            int requestCount = math.min(math.max(0, ResolveRequestCount()), Requests.Length);
            int endIndex = math.min(startIndex + count, requestCount);
            if ((uint)startIndex >= (uint)endIndex)
                return;

            for (int index = startIndex; index < endIndex; index++)
                ExecuteIndex(index);
        }

        private void ExecuteIndex(int index)
        {
            OceanKinematicsSampleRequestDTO request = Requests[index];
            int resultIndex = index;
            if ((uint)resultIndex >= (uint)Results.Length)
                return;

            FluidSampleResultDTO result = default;
            float surfaceY = SanitizeFinite(Tuning.OceanSurfaceY, 0f);
            if (!IsFinite(request.RequestedAUP))
            {
                result.WaterHeight = surfaceY;
                result.SurfaceVelocity = float3.zero;
                WriteResult(resultIndex, result);
                return;
            }

            double3 rootAup = math.select(double3.zero, Tuning.OceanRootAUP, math.isfinite(Tuning.OceanRootAUP));
            double3 deltaAup = request.RequestedAUP - rootAup;
            float3 local = ToFiniteFloat3(deltaAup);
            float depthCull = math.max(0f, SanitizeFinite(Tuning.DepthCullingThresholdMeters, OceanKinematicsConstants.DefaultDepthCullMeters));
            if (surfaceY - local.y > depthCull)
            {
                result.WaterHeight = surfaceY;
                result.SurfaceVelocity = float3.zero;
                WriteResult(resultIndex, result);
                return;
            }

            int availableWaves = Waves.IsCreated ? math.min(math.max(0, WaveCount), Waves.Length) : 0;
            if (availableWaves <= 0)
            {
                result.WaterHeight = surfaceY;
                result.SurfaceVelocity = float3.zero;
                WriteResult(resultIndex, result);
                return;
            }

            float quality = Sanitize01(Tuning.GlobalQualityWeight);
            int maxOctaves = math.clamp(Tuning.MaxOctaveLimit, 1, availableWaves);
            int activeOctaves = math.clamp((int)math.lerp(1f, maxOctaves, quality), 1, maxOctaves);
            float amplitudeMultiplier = math.max(0f, SanitizeFinite(Tuning.WaveAmplitudeMultiplier, OceanKinematicsConstants.DefaultAmplitudeMultiplier));
            float time = SanitizeFinite(Tuning.TimeSeconds, 0f);
            float height = surfaceY;
            float3 velocity = float3.zero;

            for (int waveIndex = 0; waveIndex < activeOctaves; waveIndex++)
            {
                GerstnerWaveDTO wave = Waves[waveIndex];
                if ((wave.Flags & OceanKinematicsConstants.FlagActive) == 0u)
                    continue;

                float2 dir = NormalizeDirection(wave.DirectionXZ);
                float amplitude = math.max(0f, SanitizeFinite(wave.Amplitude, 0f)) * amplitudeMultiplier;
                float steepness = math.saturate(SanitizeFinite(wave.Steepness, 0f));
                float frequency = math.max(0.0001f, SanitizeFinite(wave.Frequency, 0.0001f));
                float phaseOffset = SanitizeFinite(wave.PhaseOffset, 0f);
                float phase = WrapPhase(((dir.x * local.x) + (dir.y * local.z)) * frequency - frequency * time + phaseOffset);
                float waveSin = OceanKinematicsSimdMath.SinPolynomial(phase, quality);
                float waveCos = OceanKinematicsSimdMath.CosPolynomial(phase, quality);
                float contribution = steepness * amplitude;
                height += contribution * waveCos;
                float velocityScale = contribution * frequency * waveSin;
                velocity.x += dir.x * velocityScale;
                velocity.z += dir.y * velocityScale;
            }

            result.WaterHeight = SanitizeFinite(height, surfaceY);
            result.SurfaceVelocity = SanitizeFinite(velocity);
            WriteResult(resultIndex, result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ResolveRequestCount()
        {
            if (RequestCounter.IsCreated && RequestCounter.Length > OceanKinematicsConstants.QueueCounterPacked)
                return RequestCounter[OceanKinematicsConstants.QueueCounterPacked];

            return RequestCount;
        }

        private void WriteResult(int resultIndex, FluidSampleResultDTO result)
        {
            FluidSampleResultDTO* resultsPtr = (FluidSampleResultDTO*)Results.GetUnsafePtr();
            ref FluidSampleResultDTO target = ref UnsafeUtility.AsRef<FluidSampleResultDTO>(resultsPtr + resultIndex);
            target = result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float2 NormalizeDirection(float2 value)
        {
            float lenSq = math.max(math.lengthsq(value), 0f);
            if (lenSq <= 0.000001f || !math.isfinite(lenSq))
            {
                float2 fallback = default;
                fallback.x = 1f;
                fallback.y = 0f;
                return fallback;
            }

            return value * math.rsqrt(math.max(lenSq, 0.0001f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float WrapPhase(float phase)
        {
            return phase - math.floor((phase + math.PI) * OceanKinematicsConstants.RcpTwoPi) * OceanKinematicsConstants.TwoPi;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(double3 value)
        {
            return math.all(math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ToFiniteFloat3(double3 value)
        {
            float3 result = default;
            result.x = (float)value.x;
            result.y = (float)value.y;
            result.z = (float)value.z;
            return SanitizeFinite(result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeFinite(float value, float fallback)
        {
            return math.select(fallback, value, math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SanitizeFinite(float3 value)
        {
            return math.select(float3.zero, value, math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Sanitize01(float value)
        {
            return math.saturate(math.select(1f, value, math.isfinite(value)));
        }
    }

    /// <summary>
    /// Single-consumer queue drain that spatially coalesces identical AUP requests before simulation jobs.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct DrainOceanSampleRequestQueueJob : IJob
    {
        [NoAlias] public NativeQueue<OceanKinematicsSampleRequestDTO> PendingRequests;
        [WriteOnly, NoAlias] public NativeArray<OceanKinematicsSampleRequestDTO> PackedRequests;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // QueueCounters is written by a serial IJob, not parallel workers. The lane is used for fixed counter
        // slots describing packed, dropped, duplicate, and depth-cull counts.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // QueueCounters is a separate Vault buffer from PendingRequests, PackedRequests, and the coalescing
        // hash map. [NoAlias] documents that no packed request payload overlaps the counter lane.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // SetCounter bounds every counter slot before writing. Rejected atomics because this is the single
        // queue-drain owner and serial writes preserve deterministic request order.
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> QueueCounters;
        [NoAlias] public NativeParallelHashMap<uint, int> CoalescingHashToIndex;
        public int MaxDrainCount;

        public void Execute()
        {
            if (!PendingRequests.IsCreated || !PackedRequests.IsCreated || !QueueCounters.IsCreated)
                return;

            ResetCounters();

            int capacity = PackedRequests.Length;
            int drainBudget = math.max(0, MaxDrainCount);
            int packed = 0;
            int dropped = 0;
            int duplicate = 0;
            int drained = 0;
            bool coalescingCleared = false;
            bool coalescingSaturated = false;

            while (drained < drainBudget && PendingRequests.TryDequeue(out OceanKinematicsSampleRequestDTO request))
            {
                drained++;
                uint hash = OceanKinematicsHashUtility.ResolveRequestHash(in request);
                request.RequestHash = hash;

                if (CoalescingHashToIndex.IsCreated && !coalescingSaturated)
                {
                    if (!coalescingCleared)
                    {
                        CoalescingHashToIndex.Clear();
                        coalescingCleared = true;
                    }

                    if (CoalescingHashToIndex.ContainsKey(hash))
                    {
                        duplicate++;
                        continue;
                    }
                }

                if (packed >= capacity)
                {
                    dropped++;
                    continue;
                }

                if (CoalescingHashToIndex.IsCreated && !coalescingSaturated)
                {
                    if (!CoalescingHashToIndex.TryAdd(hash, packed))
                        coalescingSaturated = true;
                }

                request.ResultIndex = packed;
                PackedRequests[packed] = request;
                packed++;
            }

            WriteCounter(OceanKinematicsConstants.QueueCounterPacked, packed);
            WriteCounter(OceanKinematicsConstants.QueueCounterDropped, dropped);
            WriteCounter(OceanKinematicsConstants.QueueCounterDuplicate, duplicate);
        }

        private void ResetCounters()
        {
            int count = math.min(QueueCounters.Length, OceanKinematicsConstants.QueueCounterCapacity);
            for (int i = 0; i < count; i++)
                QueueCounters[i] = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteCounter(int index, int value)
        {
            if ((uint)index < (uint)QueueCounters.Length)
                QueueCounters[index] = value;
        }
    }

    /// <summary>
    /// Immediate previous-frame water response for the Dear Lie GPU readback path.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct ResolveDearLieCachedResultsJob : IJobParallelForBatch
    {
        [ReadOnly, NoAlias] public NativeArray<OceanKinematicsSampleRequestDTO> Requests;
        [ReadOnly, NoAlias] public NativeArray<int> RequestCounter;
        [ReadOnly, NoAlias] public NativeArray<OceanCachedFluidSampleDTO> CachedResults;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Cached-result resolution writes to the ParallelFor index. The queue-drain owner compacts rows before
        // this job is scheduled, so each active lane owns a unique result slot.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // CachedResults is read-only and Results is a separate Vault output lane. [NoAlias] proves the Dear
        // Lie cache cannot alias the output surface while preserving previous-frame lookup speed.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The result index is bounded before the unsafe write. Misses write a sanitized fallback water result
        // to the same unique slot; invalid rows return without touching Results.
        [WriteOnly, NativeDisableParallelForRestriction, NoAlias] public NativeArray<FluidSampleResultDTO> Results;
        public OceanKinematicsTuningDTO Tuning;
        public int RequestCount;

        public void Execute(int startIndex, int count)
        {
            if (!Requests.IsCreated || !Results.IsCreated)
                return;

            int requestCount = math.min(math.max(0, ResolveRequestCount()), Requests.Length);
            int endIndex = math.min(startIndex + count, requestCount);
            if ((uint)startIndex >= (uint)endIndex)
                return;

            for (int index = startIndex; index < endIndex; index++)
                ExecuteIndex(index);
        }

        private void ExecuteIndex(int index)
        {
            OceanKinematicsSampleRequestDTO request = Requests[index];
            int resultIndex = index;
            if ((uint)resultIndex >= (uint)Results.Length)
                return;

            uint hash = OceanKinematicsHashUtility.ResolveRequestHash(in request);
            FluidSampleResultDTO result;
            bool hit = TryReadCachedResult(hash, out result);
            if (!hit)
            {
                result = default;
                result.WaterHeight = SanitizeFinite(Tuning.OceanSurfaceY, 0f);
                result.SurfaceVelocity = float3.zero;
            }

            WriteResult(resultIndex, result);
        }

        private bool TryReadCachedResult(uint hash, out FluidSampleResultDTO result)
        {
            result = default;
            if (!CachedResults.IsCreated || CachedResults.Length == 0 || hash == 0u)
                return false;

            uint slot = hash % unchecked((uint)CachedResults.Length);
            OceanCachedFluidSampleDTO cached = CachedResults[unchecked((int)slot)];
            if (cached.RequestHash != hash || (cached.Flags & OceanKinematicsConstants.FlagActive) == 0u)
                return false;

            result = cached.Result;
            return math.isfinite(result.WaterHeight) && math.all(math.isfinite(result.SurfaceVelocity));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ResolveRequestCount()
        {
            if (RequestCounter.IsCreated && RequestCounter.Length > OceanKinematicsConstants.QueueCounterPacked)
                return RequestCounter[OceanKinematicsConstants.QueueCounterPacked];

            return RequestCount;
        }

        private void WriteResult(int resultIndex, FluidSampleResultDTO result)
        {
            FluidSampleResultDTO* resultsPtr = (FluidSampleResultDTO*)Results.GetUnsafePtr();
            ref FluidSampleResultDTO target = ref UnsafeUtility.AsRef<FluidSampleResultDTO>(resultsPtr + resultIndex);
            target = result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeFinite(float value, float fallback)
        {
            return math.select(fallback, value, math.isfinite(value));
        }
    }

    /// <summary>
    /// Folds a completed GPU readback into the direct-mapped Dear Lie cache off the main thread.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct UpdateDearLieCacheFromReadbackJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<OceanKinematicsSampleRequestDTO> CompletedRequests;
        [ReadOnly, NoAlias] public NativeArray<float4> ReadbackSamples;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // CachedResults is the only written lane and is distinct from CompletedRequests and ReadbackSamples.
        // The job is serial because direct-mapped hash slots can collide; preserving request order avoids
        // parallel write races and matches the old last-writer-wins cache semantics.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // The owner checks completed Unity async readback staging and schedules this job. Hashing, finite
        // sanitation, and cache writes are moved into the dispatcher-owned job window.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The count is clamped against all three buffers before the loop. Rejected IJobParallelFor because
        // direct-mapped cache collisions would create nondeterministic writes to the same slot.
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<OceanCachedFluidSampleDTO> CachedResults;
        public int CompletedCount;

        public void Execute()
        {
            if (!CompletedRequests.IsCreated ||
                !ReadbackSamples.IsCreated ||
                !CachedResults.IsCreated ||
                CachedResults.Length == 0 ||
                CompletedCount <= 0)
            {
                return;
            }

            int count = math.min(CompletedCount, math.min(CompletedRequests.Length, ReadbackSamples.Length));
            for (int i = 0; i < count; i++)
            {
                OceanKinematicsSampleRequestDTO request = CompletedRequests[i];
                uint hash = OceanKinematicsHashUtility.ResolveRequestHash(in request);
                float4 sample = ReadbackSamples[i];

                FluidSampleResultDTO result = default;
                result.WaterHeight = math.select(0f, sample.x, math.isfinite(sample.x));
                bool velocityFinite = math.isfinite(sample.y) && math.isfinite(sample.z) && math.isfinite(sample.w);
                result.SurfaceVelocity = math.select(float3.zero, new float3(sample.y, sample.z, sample.w), velocityFinite);

                uint slot = hash % unchecked((uint)CachedResults.Length);
                OceanCachedFluidSampleDTO cached = default;
                cached.RequestHash = hash;
                cached.Result = result;
                cached.Flags = OceanKinematicsConstants.FlagActive | OceanKinematicsConstants.FlagAsyncCached;
                CachedResults[unchecked((int)slot)] = cached;
            }
        }
    }

    /// <summary>
    /// Serial post-simulation counter pass for depth culls and non-finite rows without parallel atomics.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct CountOceanSampleDepthCullsJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<OceanKinematicsSampleRequestDTO> Requests;
        [ReadOnly, NoAlias] public NativeArray<FluidSampleResultDTO> Results;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Depth-cull/hash counting is a serial IJob over fixed QueueCounters slots. No ParallelFor worker
        // writes this lane during the pass.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Requests, Results, and QueueCounters come from distinct Vault buffers, so [NoAlias] is valid. The
        // pass reads immutable request/result rows and writes only aggregate counter slots.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // All counter writes go through bounded SetCounter calls. Rejected main-thread result hashing because
        // telemetry must read O(1) counters after dispatcher completion.
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> QueueCounters;
        public OceanKinematicsTuningDTO Tuning;
        public int RequestCount;
        public int WaveCount;

        public void Execute()
        {
            if (!Requests.IsCreated || !QueueCounters.IsCreated)
                return;

            int count = math.min(math.max(0, ResolveRequestCount()), Requests.Length);
            int resultCount = Results.IsCreated ? math.min(count, Results.Length) : 0;
            float surfaceY = SanitizeFinite(Tuning.OceanSurfaceY, 0f);
            float depthCull = math.max(0f, SanitizeFinite(Tuning.DepthCullingThresholdMeters, OceanKinematicsConstants.DefaultDepthCullMeters));
            double3 rootAup = math.select(double3.zero, Tuning.OceanRootAUP, math.isfinite(Tuning.OceanRootAUP));
            int depthCulled = 0;
            int nonFinite = 0;
            int resultNonFinite = 0;
            uint resultHash = 2166136261u;

            for (int i = 0; i < count; i++)
            {
                OceanKinematicsSampleRequestDTO request = Requests[i];
                if (!math.all(math.isfinite(request.RequestedAUP)))
                {
                    nonFinite++;
                }
                else
                {
                    double3 delta = request.RequestedAUP - rootAup;
                    float localY = SanitizeFinite((float)delta.y, surfaceY);
                    if (surfaceY - localY > depthCull)
                        depthCulled++;
                }

                if (i < resultCount)
                {
                    FluidSampleResultDTO result = Results[i];
                    if (!math.isfinite(result.WaterHeight) || !math.all(math.isfinite(result.SurfaceVelocity)))
                        resultNonFinite++;

                    resultHash = Mix(resultHash, math.asuint(result.WaterHeight));
                    resultHash = Mix(resultHash, math.asuint(result.SurfaceVelocity.x));
                    resultHash = Mix(resultHash, math.asuint(result.SurfaceVelocity.y));
                    resultHash = Mix(resultHash, math.asuint(result.SurfaceVelocity.z));
                }
            }

            WriteCounter(OceanKinematicsConstants.QueueCounterDepthCulled, depthCulled);
            WriteCounter(OceanKinematicsConstants.QueueCounterActiveOctaves, ResolveActiveOctaves());
            WriteCounter(OceanKinematicsConstants.QueueCounterNonFinite, nonFinite);
            WriteCounter(OceanKinematicsConstants.QueueCounterResultHash, unchecked((int)resultHash));
            WriteCounter(OceanKinematicsConstants.QueueCounterResultNonFinite, resultNonFinite);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ResolveRequestCount()
        {
            if (QueueCounters.IsCreated && QueueCounters.Length > OceanKinematicsConstants.QueueCounterPacked)
                return QueueCounters[OceanKinematicsConstants.QueueCounterPacked];

            return RequestCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ResolveActiveOctaves()
        {
            if (WaveCount <= 0)
                return 0;

            int available = WaveCount;
            int maxOctaves = math.clamp(Tuning.MaxOctaveLimit, 1, available);
            float quality = math.saturate(math.select(1f, Tuning.GlobalQualityWeight, math.isfinite(Tuning.GlobalQualityWeight)));
            return math.clamp((int)math.lerp(1f, maxOctaves, quality), 1, maxOctaves);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteCounter(int index, int value)
        {
            if ((uint)index < (uint)QueueCounters.Length)
                QueueCounters[index] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeFinite(float value, float fallback)
        {
            return math.select(fallback, value, math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Mix(uint hash, uint value)
        {
            hash ^= value & 0xFFu;
            hash *= 16777619u;
            hash ^= (value >> 8) & 0xFFu;
            hash *= 16777619u;
            hash ^= (value >> 16) & 0xFFu;
            hash *= 16777619u;
            hash ^= (value >> 24) & 0xFFu;
            hash *= 16777619u;
            return hash;
        }
    }
}
