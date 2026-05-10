using System.Runtime.CompilerServices;
using System.Threading;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Branch-only finite-value guard for physics and runtime-state ingress.
    /// </summary>
    public static class MathGuard
    {
        private const int InvalidNumberQueuePrewarmCapacity = 256;
        private const int MaxMainThreadDrainPerLateFrame = 32;
        private const float MinDirectionLengthSq = 0.000001f;
        private const float MinTransportSpeedMultiplier = 0.01f;

        private static NativeQueue<int> _invalidNumberQueue;
        private static int _initialized;
        private static int _queuedInvalidNumberCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Dispose();
            _initialized = 0;
            _queuedInvalidNumberCount = 0;
        }

        /// <summary>
        /// Cold-allocates the invalid-number queue before Burst jobs can request a writer.
        /// </summary>
        public static void Initialize()
        {
            if (_invalidNumberQueue.IsCreated && _initialized != 0)
                return;

            _invalidNumberQueue = new NativeQueue<int>(Allocator.Persistent); // COLD ALLOC: NativeQueue<int>[256] - Burst invalid-number error codes - owner: MathGuard
            NativeMemorySentinel.RegisterNativeQueue(
                _invalidNumberQueue,
                InvalidNumberQueuePrewarmCapacity,
                nameof(MathGuard),
                nameof(_invalidNumberQueue),
                NativeAllocationLifetime.Session);
            PrewarmQueue(ref _invalidNumberQueue, InvalidNumberQueuePrewarmCapacity);
            _initialized = 1;
        }

        /// <summary>
        /// Releases the invalid-number queue.
        /// </summary>
        public static void Dispose()
        {
            if (!_invalidNumberQueue.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeQueue(nameof(MathGuard), nameof(_invalidNumberQueue));
            _invalidNumberQueue.Dispose();
            _invalidNumberQueue = default;
            _initialized = 0;
            _queuedInvalidNumberCount = 0;
        }

        /// <summary>
        /// Returns a Burst-safe writer for invalid-number error codes.
        /// </summary>
        public static NativeQueue<int>.ParallelWriter AsParallelWriter()
        {
            Initialize();
            return _invalidNumberQueue.AsParallelWriter();
        }

        /// <summary>
        /// Checks a vector and enqueues the supplied error code if any component is NaN or infinity.
        /// </summary>
        /// <param name="value">Value to validate.</param>
        /// <param name="errorCode">Deterministic caller-owned error code.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Check(float3 value, int errorCode)
        {
            if (math.all(math.isfinite(value)))
                return;

            Initialize();
            int queuedCount = Interlocked.Increment(ref _queuedInvalidNumberCount);
            if (queuedCount > InvalidNumberQueuePrewarmCapacity)
            {
                Interlocked.Decrement(ref _queuedInvalidNumberCount);
                return;
            }

            _invalidNumberQueue.Enqueue(errorCode);
        }

        /// <summary>
        /// Burst-callable invalid-number check using a caller-supplied native queue writer.
        /// </summary>
        /// <param name="value">Value to validate.</param>
        /// <param name="errorCode">Deterministic caller-owned error code.</param>
        /// <param name="writer">Queue writer obtained from <see cref="AsParallelWriter"/> outside the job.</param>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Check(float3 value, int errorCode, NativeQueue<int>.ParallelWriter writer)
        {
            if (!math.all(math.isfinite(value)))
                writer.Enqueue(errorCode);
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 SanitizeFiniteOrZero(float3 value, int errorCode, NativeQueue<int>.ParallelWriter writer)
        {
            if (math.all(math.isfinite(value)))
                return value;

            writer.Enqueue(errorCode);
            return float3.zero;
        }

        /// <summary>
        /// Drains invalid-number error codes into the telemetry bus.
        /// </summary>
        /// <param name="maxDrainCount">Maximum codes to consume this frame.</param>
        public static int DrainInvalidNumberErrors(int maxDrainCount = MaxMainThreadDrainPerLateFrame)
        {
            if (!_invalidNumberQueue.IsCreated || maxDrainCount <= 0)
                return 0;

            int drainedCount = 0;
            while (drainedCount < maxDrainCount && _invalidNumberQueue.TryDequeue(out int errorCode))
            {
                if (Volatile.Read(ref _queuedInvalidNumberCount) > 0)
                    Interlocked.Decrement(ref _queuedInvalidNumberCount);

                GlobalTelemetryBus.PublishMathGuardInvalidNumber(errorCode);
                drainedCount++;
            }

            return drainedCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFinite(float value)
        {
            return math.isfinite(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFinite(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFinite(Vector3 value)
        {
            return IsFinite(new float3(value.x, value.y, value.z));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFinite(in AbsoluteUniversePosition value)
        {
            return math.isfinite(value.LocalX) &&
                   math.isfinite(value.LocalY) &&
                   math.isfinite(value.LocalZ);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SanitizeFinite(float value, float fallback = 0f)
        {
            return math.isfinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 SanitizeFinite(float3 value, float3 fallback = default)
        {
            return IsFinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 SanitizeFinite(Vector3 value, Vector3 fallback = default)
        {
            return IsFinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SanitizeNonNegative(float value, float fallback = 0f)
        {
            float finite = SanitizeFinite(value, fallback);
            return math.max(0f, finite);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sanitize01(float value, float fallback = 0f)
        {
            float finite = SanitizeFinite(value, fallback);
            return math.saturate(finite);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 SanitizeDirection(float3 value, float3 fallback)
        {
            if (IsFinite(value))
            {
                float lengthSq = math.lengthsq(value);
                if (lengthSq > MinDirectionLengthSq)
                    return value * math.rsqrt(lengthSq);
            }

            if (IsFinite(fallback))
            {
                float fallbackLengthSq = math.lengthsq(fallback);
                if (fallbackLengthSq > MinDirectionLengthSq)
                    return fallback * math.rsqrt(fallbackLengthSq);
            }

            return new float3(0f, 0f, 1f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AbsoluteUniversePosition SanitizeAup(
            in AbsoluteUniversePosition value,
            in AbsoluteUniversePosition fallback)
        {
            return IsFinite(in value) ? value : fallback;
        }

        public static PlayerMovementRuntimeState SanitizePlayerMovementRuntimeState(
            in PlayerMovementRuntimeState value,
            in PlayerMovementRuntimeState fallback)
        {
            PlayerMovementRuntimeState sanitized = value;
            sanitized.WorldPosition = SanitizeFinite(value.WorldPosition, fallback.WorldPosition);
            sanitized.PredictedWorldPosition = SanitizeFinite(value.PredictedWorldPosition, sanitized.WorldPosition);
            sanitized.PredictedAup = SanitizeAup(in value.PredictedAup, in fallback.PredictedAup);
            sanitized.Velocity = SanitizeFinite(value.Velocity, fallback.Velocity);
            sanitized.Forward = SanitizeDirection(value.Forward, fallback.Forward);
            sanitized.CameraForward = SanitizeDirection(value.CameraForward, sanitized.Forward);
            sanitized.DepthMeters = SanitizeNonNegative(value.DepthMeters, fallback.DepthMeters);
            sanitized.TransportSpeedMultiplier = math.max(
                MinTransportSpeedMultiplier,
                SanitizeFinite(value.TransportSpeedMultiplier, fallback.TransportSpeedMultiplier));
            sanitized.UnderwaterStressIntensity01 = Sanitize01(
                value.UnderwaterStressIntensity01,
                fallback.UnderwaterStressIntensity01);
            return sanitized;
        }

        private static void PrewarmQueue<T>(ref NativeQueue<T> queue, int capacity)
            where T : unmanaged
        {
            if (!queue.IsCreated || capacity <= 0)
                return;

            for (int i = 0; i < capacity; i++)
                queue.Enqueue(default);

            while (queue.TryDequeue(out _))
            {
            }
        }
    }
}
