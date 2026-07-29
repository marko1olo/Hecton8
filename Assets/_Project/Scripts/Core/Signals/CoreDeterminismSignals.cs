using Hecton8.Core.Contracts.Signals;
using System;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Core-owned signal sidecars for deterministic locomotion and lockstep lanes.
    /// </summary>
    public static class CoreDeterminismSignals
    {
        private static bool _initialized;
        private static uint _inputSequence;
        private static uint _inputOverrideSequence;
        private static uint _syncFenceSequence;
        private static uint _kccVelocitySequence;
        private static int s_x001CoreDeterminismSignalsSignalPushDropCount;
        private static InputSignal _latestInputSignal;
        private static InputSignal _latestInputOverrideSignal;
        // Latch for the one-shot input-override clock-skew report. int rather than bool so
        // Interlocked.Exchange can be used - the consume path can be reached from more than one caller.
        private static int _inputOverrideClockSkewReported;
        private static SyncFenceSignal _latestSyncFenceSignal;
        private static KccVelocitySignal _latestKccVelocitySignal;

        public const byte InputSignalFlagAutomationOverride = 1 << 0;
        public const byte StateCorrectionSignalFlagRuntimePositionValid = 1 << 0;
        public const byte StateCorrectionSignalFlagRotationValid = 1 << 1;
        public const byte StateCorrectionSignalFlagVelocityValid = 1 << 2;

        [Obsolete("Use TryPublishInput(in PlayerInputState,uint,byte) so overflow/drop semantics stay visible at the producer.", true)]
        public static void PublishInput(in PlayerInputState state, uint frame, byte flags = 0)
        {
            TryPublishInput(in state, frame, flags);
        }

        public static bool TryPublishInput(in PlayerInputState state, uint frame, byte flags = 0)
        {
            InputSignal signal = default;
            signal.MoveDelta = new float2(state.MoveDelta.x, state.MoveDelta.y);
            signal.LookDelta = new float2(state.LookDelta.x, state.LookDelta.y);
            signal.VerticalDelta = math.clamp(state.VerticalDelta, -1f, 1f);
            signal.ActionsBitmask = state.ActionsBitmask;
            signal.CurrentInputSchemeHash = state.CurrentInputSchemeHash;
            signal.Frame = frame;
            signal.Sequence = NextSequence(ref _inputSequence);
            signal.Flags = flags;
            return TryPublish(in signal);
        }

        [Obsolete("Use TryPublishInputOverride(in PlayerInputState,uint) so override publication is explicit.", true)]
        public static void PublishInputOverride(in PlayerInputState state, uint frame)
        {
            TryPublishInputOverride(in state, frame);
        }

        public static bool TryPublishInputOverride(in PlayerInputState state, uint frame)
        {
            InputSignal signal = default;
            signal.MoveDelta = new float2(state.MoveDelta.x, state.MoveDelta.y);
            signal.LookDelta = new float2(state.LookDelta.x, state.LookDelta.y);
            signal.VerticalDelta = math.clamp(state.VerticalDelta, -1f, 1f);
            signal.ActionsBitmask = state.ActionsBitmask;
            signal.CurrentInputSchemeHash = state.CurrentInputSchemeHash;
            signal.Frame = frame;
            signal.Sequence = NextSequence(ref _inputOverrideSequence);
            signal.Flags = InputSignalFlagAutomationOverride;
            _latestInputOverrideSignal = signal;
            return true;
        }

        public static void ClearInputOverride()
        {
            _latestInputOverrideSignal = default;
        }

        [Obsolete("Use TryPublish(in InputSignal) so overflow/drop semantics stay visible at the producer.", true)]
        public static void Publish(in InputSignal signal)
        {
            TryPublish(in signal);
        }

        public static bool TryPublish(in InputSignal signal)
        {
            EnsureInitialized();
            _latestInputSignal = signal;
            return SignalBus<InputSignal>.TryPushTracked(in signal, ref s_x001CoreDeterminismSignalsSignalPushDropCount);
        }

        [Obsolete("Use TryPublish(in StateCorrectionSignal) so overflow/drop semantics stay visible at the producer.", true)]
        public static void Publish(in StateCorrectionSignal signal)
        {
            TryPublish(in signal);
        }

        public static bool TryPublish(in StateCorrectionSignal signal)
        {
            EnsureInitialized();
            return SignalBus<StateCorrectionSignal>.TryPushTracked(in signal, ref s_x001CoreDeterminismSignalsSignalPushDropCount);
        }

        [Obsolete("Use TryPublish(in DesyncDetectedSignal) so overflow/drop semantics stay visible at the producer.", true)]
        public static void Publish(in DesyncDetectedSignal signal)
        {
            TryPublish(in signal);
        }

        public static bool TryPublish(in DesyncDetectedSignal signal)
        {
            EnsureInitialized();
            return SignalBus<DesyncDetectedSignal>.TryPushTracked(in signal, ref s_x001CoreDeterminismSignalsSignalPushDropCount);
        }

        [Obsolete("Use TryPublish(in SyncFenceSignal) so overflow/drop semantics stay visible at the producer.", true)]
        public static void Publish(in SyncFenceSignal signal)
        {
            TryPublish(in signal);
        }

        public static bool TryPublish(in SyncFenceSignal signal)
        {
            EnsureInitialized();
            SyncFenceSignal sequenced = signal;
            sequenced.Sequence = NextSequence(ref _syncFenceSequence);
            _latestSyncFenceSignal = sequenced;
            return SignalBus<SyncFenceSignal>.TryPushTracked(in sequenced, ref s_x001CoreDeterminismSignalsSignalPushDropCount);
        }

        [Obsolete("Use TryPublishKccVelocity(in KccVelocitySignal) so overflow/drop semantics stay visible at the producer.", true)]
        public static void PublishKccVelocity(in KccVelocitySignal signal)
        {
            TryPublishKccVelocity(in signal);
        }

        public static bool TryPublishKccVelocity(in KccVelocitySignal signal)
        {
            return TryPublish(in signal);
        }

        [Obsolete("Use TryPublish(in KccVelocitySignal) so overflow/drop semantics stay visible at the producer.", true)]
        public static void Publish(in KccVelocitySignal signal)
        {
            TryPublish(in signal);
        }

        public static bool TryPublish(in KccVelocitySignal signal)
        {
            EnsureInitialized();
            KccVelocitySignal sequenced = signal;
            sequenced.Sequence = NextSequence(ref _kccVelocitySequence);
            sequenced.Velocity = math.select(sequenced.Velocity, float3.zero, !math.all(math.isfinite(sequenced.Velocity)));
            sequenced.PlanarSpeedSq = math.select(
                math.lengthsq(new float2(sequenced.Velocity.x, sequenced.Velocity.z)),
                0.0f,
                !math.all(math.isfinite(sequenced.Velocity)));
            _latestKccVelocitySignal = sequenced;
            return SignalBus<KccVelocitySignal>.TryPushTracked(in sequenced, ref s_x001CoreDeterminismSignalsSignalPushDropCount);
        }

        public static bool TryDequeueInput(out InputSignal signal) => TryConsumeLane(out signal);

        public static bool TryDequeueStateCorrection(out StateCorrectionSignal signal) => TryConsumeLane(out signal);

        public static bool TryDequeueDesyncDetected(out DesyncDetectedSignal signal) => TryConsumeLane(out signal);

        public static bool TryDequeueSyncFence(out SyncFenceSignal signal) => TryConsumeLane(out signal);

        public static bool TryDequeueKccVelocity(out KccVelocitySignal signal) => TryConsumeLane(out signal);

        public static bool TryGetLatestInput(out InputSignal signal)
        {
            signal = _latestInputSignal;
            return signal.Sequence != 0u;
        }

        public static bool TryConsumeLatestInputOverride(uint frame, uint maxFrameAge, out InputSignal signal)
        {
            signal = _latestInputOverrideSignal;
            if (signal.Sequence == 0u)
                return false;

            if (frame < signal.Frame)
            {
                // Deliberately does NOT clear: a producer legitimately one frame ahead should be picked up on
                // the next poll rather than dropped. But that makes this branch a silent latch if the consumer
                // is reading a DIFFERENT clock than the producer stamps with - it then fires forever and no
                // override is ever applied. That is exactly what happened: producers publish
                // SystemDispatcher.CurrentFrameId (TimeSliceScheduler's boot-long counter) while
                // InputDispatcher consumed with CurrentFrameIndex (the dispatcher instance's own sequence,
                // reset to 0 on init), so 124 published overrides produced zero movement and the failure was
                // invisible. Per AGENTS.md, a system that can collapse silently must fail loudly instead.
                ReportInputOverrideClockSkewOnce(frame, signal.Frame);
                return false;
            }

            uint age = frame - signal.Frame;
            if (age > maxFrameAge)
            {
                _latestInputOverrideSignal = default;
                return false;
            }

            _latestInputOverrideSignal = default;
            return true;
        }

        /// <summary>
        /// One-shot diagnostic for producer/consumer frame-clock divergence on the input-override lane.
        /// Latched so a per-poll condition cannot spam the log or allocate per frame.
        /// </summary>
        private static void ReportInputOverrideClockSkewOnce(uint consumerFrame, uint producerFrame)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (System.Threading.Interlocked.Exchange(ref _inputOverrideClockSkewReported, 1) != 0)
                return;

            // COLD ALLOC: one-shot string - input-override clock skew report - owner: CoreDeterminismSignals
            UnityEngine.Debug.LogError(
                "[CoreDeterminismSignals] input-override clock skew: consumerFrame=" + consumerFrame +
                " < producerFrame=" + producerFrame +
                ". The consumer is reading a different frame counter than the producer stamps with, so no " +
                "synthetic input will ever be applied. Producers must publish SystemDispatcher.CurrentFrameId " +
                "and consumers must compare against SystemDispatcher.CurrentFrameId, not CurrentFrameIndex.");
#endif
        }

        public static bool TryGetLatestSyncFence(out SyncFenceSignal signal)
        {
            signal = _latestSyncFenceSignal;
            return signal.Sequence != 0u;
        }

        public static bool TryGetLatestKccVelocity(out KccVelocitySignal signal)
        {
            signal = _latestKccVelocitySignal;
            return signal.Sequence != 0u;
        }

        public static bool TryGetLatestKccVelocityFloat3(uint maxFrameAge, out float3 velocity)
        {
            velocity = float3.zero;
            uint currentFrame = SystemDispatcher.CurrentFrameId;
            if (!TryGetLatestKccVelocity(out KccVelocitySignal signal) ||
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ClearSidecars();
            _initialized = false;
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
                return;

            SignalCorridorRuntime.EnsureInitialized();
            SignalBus<InputSignal>.EnsureInitialized();
            SignalBus<StateCorrectionSignal>.EnsureInitialized();
            SignalBus<DesyncDetectedSignal>.EnsureInitialized();
            SignalBus<SyncFenceSignal>.EnsureInitialized();
            SignalBus<KccVelocitySignal>.EnsureInitialized();
            _initialized = true;
        }

        private static bool TryConsumeLane<T>(out T signal)
            where T : unmanaged, ISignal
        {
            if (!_initialized)
            {
                signal = default;
                return false;
            }

            return SignalBus<T>.TryConsumeFrame(out signal);
        }

        private static bool IsKccVelocityFresh(in KccVelocitySignal signal, uint currentFrame, uint maxFrameAge)
        {
            uint signalFrame = signal.Frame != 0u ? signal.Frame : signal.Sequence;
            return currentFrame == 0u ||
                   signalFrame == 0u ||
                   (signalFrame <= currentFrame && currentFrame - signalFrame <= maxFrameAge);
        }

        private static void ClearSidecars()
        {
            _latestInputSignal = default;
            _latestInputOverrideSignal = default;
            _latestSyncFenceSignal = default;
            _latestKccVelocitySignal = default;
            _inputSequence = 0u;
            _inputOverrideSequence = 0u;
            _syncFenceSequence = 0u;
            _kccVelocitySequence = 0u;
        }

        private static uint NextSequence(ref uint sequence)
        {
            uint next = sequence + 1u;
            if (next == 0u)
                next = 1u;

            sequence = next;
            return next;
        }
    }
}
