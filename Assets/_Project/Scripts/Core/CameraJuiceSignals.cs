using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Camera-owned impact signal payload. Wraps the core impact signal with an optional directional bias.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 80)]
    public struct CameraJuiceImpactSignal
    {
        [FieldOffset(0)] public ImpactSignal Impact;
        [FieldOffset(64)] public float3 Direction;
        [FieldOffset(76)] public float Severity;
    }

    /// <summary>
    /// NativeQueue-backed camera impact lane. Producers publish impacts without referencing the camera runtime.
    /// </summary>
    public static class CameraJuiceSignals
    {
        private const int ImpactSignalCapacity = 128;

        private static NativeQueue<CameraJuiceImpactSignal> _impactSignals;
        private static int _pendingImpactCount;

        /// <summary>Number of camera impact packets waiting for the camera presentation runtime.</summary>
        public static int PendingImpactCount => _pendingImpactCount;

        /// <summary>Prewarms the native impact lane before gameplay impacts arrive.</summary>
        public static void EnsurePrewarmed()
        {
            EnsureInitialized();
        }

        /// <summary>Queues one camera impact packet from an existing core impact signal.</summary>
        /// <param name="impact">Core impact payload.</param>
        /// <param name="direction">Optional world-space impact direction. Zero means unbiased.</param>
        public static void PublishImpact(in ImpactSignal impact, float3 direction)
        {
            float severity = math.saturate(impact.Intensity);
            if (severity <= 0f)
                return;

            EnsureInitialized();
            if (_pendingImpactCount >= ImpactSignalCapacity)
            {
                if (_impactSignals.TryDequeue(out _))
                    _pendingImpactCount = math.max(0, _pendingImpactCount - 1);
                else
                    _pendingImpactCount = 0;
            }

            _impactSignals.Enqueue(new CameraJuiceImpactSignal
            {
                Impact = impact,
                Direction = SanitizeDirection(direction),
                Severity = severity
            });
            _pendingImpactCount++;
        }

        /// <summary>Queues one camera impact packet from runtime position data.</summary>
        /// <param name="severity01">Normalized impact severity.</param>
        /// <param name="runtimePosition">Runtime-space impact position.</param>
        /// <param name="direction">Optional world-space impact direction. Zero means unbiased.</param>
        public static void PublishImpact(float severity01, Vector3 runtimePosition, Vector3 direction)
        {
            float severity = math.saturate(severity01);
            if (severity <= 0f)
                return;

            ImpactSignal impact = default;
            impact.PointAup = AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
            impact.Intensity = severity;
            impact.Force = severity;
            PublishImpact(in impact, new float3(direction.x, direction.y, direction.z));
        }

        /// <summary>Attempts to dequeue one camera impact packet.</summary>
        /// <param name="signal">Dequeued impact signal.</param>
        /// <returns>True when a packet was dequeued.</returns>
        public static bool TryDequeueImpact(out CameraJuiceImpactSignal signal)
        {
            if (!_impactSignals.IsCreated)
            {
                signal = default;
                return false;
            }

            if (!_impactSignals.TryDequeue(out signal))
                return false;

            _pendingImpactCount = math.max(0, _pendingImpactCount - 1);
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_impactSignals.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(CameraJuiceSignals), nameof(_impactSignals));
                _impactSignals.Dispose();
                _impactSignals = default;
            }

            _pendingImpactCount = 0;
        }

        private static void EnsureInitialized()
        {
            if (_impactSignals.IsCreated)
                return;

            _impactSignals = new NativeQueue<CameraJuiceImpactSignal>(Allocator.Persistent); // COLD ALLOC: NativeQueue<CameraJuiceImpactSignal>[128] - camera impact signal lane - owner: CameraJuiceSignals
            NativeMemorySentinel.RegisterNativeQueue(
                _impactSignals,
                ImpactSignalCapacity,
                nameof(CameraJuiceSignals),
                nameof(_impactSignals),
                NativeAllocationLifetime.Session);
            PrewarmQueue(ref _impactSignals, ImpactSignalCapacity);
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

        private static float3 SanitizeDirection(float3 direction)
        {
            if (!math.all(math.isfinite(direction)))
                return float3.zero;

            float lengthSq = math.lengthsq(direction);
            if (lengthSq <= 0.000001f)
                return float3.zero;

            return direction * math.rsqrt(lengthSq);
        }
    }
}
