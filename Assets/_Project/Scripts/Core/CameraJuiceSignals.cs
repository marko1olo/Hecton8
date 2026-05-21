using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Generated;
using CameraJuiceImpactSignal = Hecton8.Core.Contracts.Signals.CameraJuiceImpactSignal;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Typed signal camera impact lane. Producers publish impacts without referencing the camera runtime.
    /// </summary>
    public static class CameraJuiceSignals
    {
        private const int ImpactSignalCapacity = 128;
        private const int LowTierImpactSignalCapacity = 32;

        private static bool _signalLaneConfigured;

        /// <summary>Number of camera impact packets in the current typed-lane snapshot.</summary>
        public static int PendingImpactCount => SignalBus<CameraJuiceImpactSignal>.SnapshotCount;

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
            CameraJuiceImpactSignal signal = new CameraJuiceImpactSignal
            {
                Impact = impact,
                Direction = SanitizeDirection(direction),
                Severity = severity
            };
            SignalBus<CameraJuiceImpactSignal>.Push(in signal);
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
            if (!GlobalSignals.TryRuntimePositionToAup(runtimePosition, ref impact.PointAup))
                return;

            impact.Intensity = severity;
            impact.Force = severity;
            PublishImpact(in impact, new float3(direction.x, direction.y, direction.z));
        }

        /// <summary>Attempts to dequeue one camera impact packet.</summary>
        /// <param name="signal">Dequeued impact signal.</param>
        /// <returns>True when a packet was dequeued.</returns>
        public static bool TryDequeueImpact(out CameraJuiceImpactSignal signal)
        {
            return SignalBus<CameraJuiceImpactSignal>.TryConsumeFrame(out signal);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _signalLaneConfigured = false;
        }

        private static void EnsureInitialized()
        {
            if (_signalLaneConfigured)
                return;

            SignalBus<CameraJuiceImpactSignal>.Configure(
                ImpactSignalCapacity,
                maxFrameSignals: ImpactSignalCapacity,
                lowTierFrameSignals: LowTierImpactSignalCapacity,
                laneHash: H8Hashes.Signals.CameraJuiceImpactSignalHash);
            SignalBus<CameraJuiceImpactSignal>.EnsureInitialized();
            _signalLaneConfigured = true;
        }

        private static float3 SanitizeDirection(float3 direction)
        {
            if (!math.all(math.isfinite(direction)))
                return float3.zero;

            float lengthSq = math.lengthsq(direction);
            if (lengthSq <= 0.000001f)
                return float3.zero;

            return direction * math.rsqrt(math.max(lengthSq, 0.000001f));
        }
    }
}
