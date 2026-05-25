using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using System;
using Unity.Mathematics;
using UnityEngine;

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

        [Obsolete("Use TryPublishInput(in PlayerInputState,uint,byte) so overflow/drop semantics stay visible at the producer.", true)]
        public static void PublishInput(in PlayerInputState state, uint frame, byte flags = 0)
        {
            TryPublishInput(in state, frame, flags);
        }

        public static bool TryPublishInput(in PlayerInputState state, uint frame, byte flags = 0)
        {
            return CoreDeterminismSignals.TryPublishInput(in state, frame, flags);
        }

        [Obsolete("Use TryPublishInputOverride(in PlayerInputState,uint) so override publication is explicit.", true)]
        public static void PublishInputOverride(in PlayerInputState state, uint frame)
        {
            TryPublishInputOverride(in state, frame);
        }

        public static bool TryPublishInputOverride(in PlayerInputState state, uint frame)
        {
            return CoreDeterminismSignals.TryPublishInputOverride(in state, frame);
        }

        public static void ClearInputOverride()
        {
            CoreDeterminismSignals.ClearInputOverride();
        }

        [Obsolete("Use TryPublish(in InputSignal) so overflow/drop semantics stay visible at the producer.", true)]
        public static void Publish(in InputSignal signal)
        {
            TryPublish(in signal);
        }

        public static bool TryPublish(in InputSignal signal)
        {
            return CoreDeterminismSignals.TryPublish(in signal);
        }

        [Obsolete("Use TryPublish(in StateCorrectionSignal) so overflow/drop semantics stay visible at the producer.", true)]
        public static void Publish(in StateCorrectionSignal signal)
        {
            TryPublish(in signal);
        }

        public static bool TryPublish(in StateCorrectionSignal signal)
        {
            return CoreDeterminismSignals.TryPublish(in signal);
        }

        [Obsolete("Use TryPublish(in DesyncDetectedSignal) so overflow/drop semantics stay visible at the producer.", true)]
        public static void Publish(in DesyncDetectedSignal signal)
        {
            TryPublish(in signal);
        }

        public static bool TryPublish(in DesyncDetectedSignal signal)
        {
            return CoreDeterminismSignals.TryPublish(in signal);
        }

        [Obsolete("Use TryPublish(in SyncFenceSignal) so overflow/drop semantics stay visible at the producer.", true)]
        public static void Publish(in SyncFenceSignal signal)
        {
            TryPublish(in signal);
        }

        public static bool TryPublish(in SyncFenceSignal signal)
        {
            return CoreDeterminismSignals.TryPublish(in signal);
        }

        [Obsolete("Use TryPublishKccVelocity(in AbsoluteUniversePosition,float3,uint,uint,byte) so overflow/drop semantics stay visible at the producer.", true)]
        public static void PublishKccVelocity(in AbsoluteUniversePosition bodyAup, float3 velocity, uint frame, uint sourceId, byte flags = 0)
        {
            TryPublishKccVelocity(in bodyAup, velocity, frame, sourceId, flags);
        }

        public static bool TryPublishKccVelocity(in AbsoluteUniversePosition bodyAup, float3 velocity, uint frame, uint sourceId, byte flags = 0)
        {
            KccVelocitySignal signal = default;
            signal.BodyAup = bodyAup;
            signal.Velocity = math.select(velocity, float3.zero, !math.all(math.isfinite(velocity)));
            signal.PlanarSpeedSq = math.lengthsq(new float2(signal.Velocity.x, signal.Velocity.z));
            signal.Frame = frame;
            signal.SourceId = sourceId;
            signal.Flags = flags;
            return CoreDeterminismSignals.TryPublishKccVelocity(in signal);
        }

        [Obsolete("Use TryPublish(in KccVelocitySignal) so overflow/drop semantics stay visible at the producer.", true)]
        public static void Publish(in KccVelocitySignal signal)
        {
            TryPublish(in signal);
        }

        public static bool TryPublish(in KccVelocitySignal signal)
        {
            return CoreDeterminismSignals.TryPublish(in signal);
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

        public static bool TryGetLatestKccVelocityFloat3(uint maxFrameAge, out float3 velocity)
        {
            velocity = float3.zero;
            uint currentFrame = unchecked((uint)SystemDispatcher.CurrentFrameIndex);
            if (!CoreDeterminismSignals.TryGetLatestKccVelocity(out KccVelocitySignal signal) ||
                signal.Sequence == 0u ||
                !IsKccVelocityFresh(in signal, currentFrame, maxFrameAge) ||
                !math.all(math.isfinite(signal.Velocity)))
            {
                return false;
            }

            velocity = signal.Velocity;
            return true;
        }

        public static bool TryGetLatestKccVelocityVector(uint maxFrameAge, out Vector3 velocity)
        {
            velocity = Vector3.zero;
            if (!TryGetLatestKccVelocityFloat3(maxFrameAge, out float3 value))
                return false;

            velocity = new Vector3(value.x, value.y, value.z);
            return true;
        }

        private static bool IsKccVelocityFresh(in KccVelocitySignal signal, uint currentFrame, uint maxFrameAge)
        {
            uint signalFrame = signal.Frame != 0u ? signal.Frame : signal.Sequence;
            return currentFrame == 0u ||
                   signalFrame == 0u ||
                   (signalFrame <= currentFrame && currentFrame - signalFrame <= maxFrameAge);
        }
    }
}
