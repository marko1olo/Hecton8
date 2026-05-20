using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Unity.Mathematics;

namespace Hecton8.Physics
{
    /// <summary>
    /// Compatibility facade for deterministic locomotion lanes. Core owns the sidecar state.
    /// </summary>
    public static class PhysicsDeterminismSignals
    {
        public const byte InputSignalFlagAutomationOverride = CoreDeterminismSignals.InputSignalFlagAutomationOverride;
        public const byte StateCorrectionSignalFlagRuntimePositionValid = CoreDeterminismSignals.StateCorrectionSignalFlagRuntimePositionValid;
        public const byte StateCorrectionSignalFlagRotationValid = CoreDeterminismSignals.StateCorrectionSignalFlagRotationValid;
        public const byte StateCorrectionSignalFlagVelocityValid = CoreDeterminismSignals.StateCorrectionSignalFlagVelocityValid;

        public static void PublishInput(in PlayerInputState state, uint frame, byte flags = 0)
        {
            CoreDeterminismSignals.PublishInput(in state, frame, flags);
        }

        public static void PublishInputOverride(in PlayerInputState state, uint frame)
        {
            CoreDeterminismSignals.PublishInputOverride(in state, frame);
        }

        public static void ClearInputOverride()
        {
            CoreDeterminismSignals.ClearInputOverride();
        }

        public static void Publish(in InputSignal signal)
        {
            CoreDeterminismSignals.Publish(in signal);
        }

        public static void Publish(in StateCorrectionSignal signal)
        {
            CoreDeterminismSignals.Publish(in signal);
        }

        public static void Publish(in DesyncDetectedSignal signal)
        {
            CoreDeterminismSignals.Publish(in signal);
        }

        public static void Publish(in SyncFenceSignal signal)
        {
            CoreDeterminismSignals.Publish(in signal);
        }

        public static void PublishKccVelocity(in AbsoluteUniversePosition bodyAup, float3 velocity, uint frame, uint sourceId, byte flags = 0)
        {
            KccVelocitySignal signal = default;
            signal.BodyAup = bodyAup;
            signal.Velocity = math.select(velocity, float3.zero, !math.all(math.isfinite(velocity)));
            signal.PlanarSpeedSq = math.lengthsq(new float2(signal.Velocity.x, signal.Velocity.z));
            signal.Frame = frame;
            signal.SourceId = sourceId;
            signal.Flags = flags;
            CoreDeterminismSignals.PublishKccVelocity(in signal);
        }

        public static void Publish(in KccVelocitySignal signal)
        {
            CoreDeterminismSignals.Publish(in signal);
        }

        public static bool TryDequeueInput(out InputSignal signal) => CoreDeterminismSignals.TryDequeueInput(out signal);

        public static bool TryDequeueStateCorrection(out StateCorrectionSignal signal) => CoreDeterminismSignals.TryDequeueStateCorrection(out signal);

        public static bool TryDequeueDesyncDetected(out DesyncDetectedSignal signal) => CoreDeterminismSignals.TryDequeueDesyncDetected(out signal);

        public static bool TryDequeueSyncFence(out SyncFenceSignal signal) => CoreDeterminismSignals.TryDequeueSyncFence(out signal);

        public static bool TryDequeueKccVelocity(out KccVelocitySignal signal) => CoreDeterminismSignals.TryDequeueKccVelocity(out signal);

        public static bool TryGetLatestInput(out InputSignal signal) => CoreDeterminismSignals.TryGetLatestInput(out signal);

        public static bool TryConsumeLatestInputOverride(uint frame, uint maxFrameAge, out InputSignal signal) =>
            CoreDeterminismSignals.TryConsumeLatestInputOverride(frame, maxFrameAge, out signal);

        public static bool TryGetLatestSyncFence(out SyncFenceSignal signal) => CoreDeterminismSignals.TryGetLatestSyncFence(out signal);

        public static bool TryGetLatestKccVelocity(out KccVelocitySignal signal) => CoreDeterminismSignals.TryGetLatestKccVelocity(out signal);
    }
}
