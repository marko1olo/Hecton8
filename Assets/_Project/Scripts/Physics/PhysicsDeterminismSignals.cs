using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using System;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Physics
{
    // ---------------------------------------------------------------------------------------------------
    // REACHABILITY: NOTHING CALLS THIS TYPE. Verified 2026-07-29 by scoped scan of Assets/_Project.
    //
    // The only textual reference outside this file is Tests/Editor/KelpShaderScalability1427EditTests.cs
    // :4622, which reads this file as a STRING and asserts on the text of the KCC velocity publish helper.
    // It never invokes the type, so it does not make anything here reachable. No RuntimeInitializeOnLoad,
    // no reflection-by-string (scan over non-.cs assets found only docs/reports), and none is possible via
    // scene GUID, interface dispatch, or a registry slot - this is a static class with no instances.
    // Reachability was lost on purpose: the runtime migrated to CoreDeterminismSignals, recorded in
    // Docs/Archive/Batch012_Reentry_20260525_143323/Tasks/Status_X_005.md:978 ("after runtime had already
    // migrated to Core signal access"). ~44 files call CoreDeterminismSignals directly today.
    //
    // CONSEQUENCE - four half-wired lanes, because these members are the only reachable path to the
    // matching CoreDeterminismSignals members. Publishers below are live; drains below are not.
    //
    //   InputSignal - NO publisher AND no consumer. The only bus push is CoreDeterminismSignals.cs:88
    //     (TryPublish(in InputSignal)), reached only from CoreDeterminismSignals.TryPublishInput
    //     (cs:38), and the only caller of either is TryPublishInput/TryPublish in THIS file. That same
    //     unreachable path is the only writer of _latestInputSignal (cs:87), so
    //     CoreDeterminismSignals.TryGetLatestInput (cs:171) returns false forever and its live consumer
    //     ZeroGMovementRuntime.cs:579 can never see deterministic input. Do not confuse this with the
    //     input-override sidecar (CoreDeterminismSignals.TryPublishInputOverride, cs:58), which never
    //     touches the bus and IS live: published from LockstepStateValidator.cs:971, QA_WatchdogBot.cs
    //     :718, Shinobu38QaWatchdogRuntime.cs:2080, QAEnduranceWatchdogBot.cs:556,
    //     H8_HeadlessWorldDriver.cs:1590, and consumed at InputDispatcher.cs:3664.
    //   DesyncDetectedSignal - publishers PlayerKinematicsRuntime.cs:4070 and LockstepStateValidator.cs
    //     :1517. Only drain is CoreDeterminismSignals.cs:165, called only from TryDequeueDesyncDetected
    //     in this file. Every desync report published so far has been discarded unread.
    //   SyncFenceSignal - publishers PlayerKinematicsRuntime.cs:4023 and SomaticKinematicsRuntime.cs:1208
    //     (the latter pushes the bus directly, so it does not update the _latestSyncFenceSignal sidecar at
    //     CoreDeterminismSignals.cs:126). Only drains are CoreDeterminismSignals.cs:167 and cs:228,
    //     called only from TryDequeueSyncFence / TryGetLatestSyncFence in this file.
    //   StateCorrectionSignal - the mirror failure. It IS drained live at PlayerKinematicsRuntime.cs:3958,
    //     but the only publish path is CoreDeterminismSignals.cs:97, whose only caller is TryPublish(in
    //     StateCorrectionSignal) in this file. That drain loop can therefore never receive a correction.
    //   KccVelocitySignal is NOT affected - its sidecar readers are called through Core from ~9 live sites
    //     (PlayerKinematicsRuntime.cs:3628, FaunaBrain.cs:6101, and others). The KCC members here are
    //     redundant duplicates of a working Core path, not an orphaned lane.
    //
    // SEVERITY IS "SILENTLY DISCARDED", NOT "LEAK". An undrained lane does not grow without bound.
    // SystemDispatcher.cs:3036 calls SignalCorridorRuntime.FlushPostSimulation (SignalCorridorRuntime.cs
    // :24 -> GlobalSignals.RuntimeLifecycle.cs:357) every frame, and SignalBus<T>.FlushPostSimulation
    // (SignalBusRuntime.cs:890) zeroes _frameSnapshotCount and _legacyReadCursor before refilling, then
    // DropOldest()s anything past the frame limit. Ring enqueue failure also just drops
    // (SignalBusRuntime.cs:~703). Rings are capacity-bounded (SpscSignalRingBuffer.cs:264) at 128 / 16 / 32
    // for Input / Desync / SyncFence (GlobalSignals.State.cs:123-126). Because all three sit below
    // LaneOverflowFaultThreshold = 1024 (SignalBusRuntime.cs:388), the loud [LANE_OVERFLOW_FAULT] warning
    // can NEVER fire for them - the silence is structural, so cost is lost diagnostics, not RAM.
    //
    // WHAT WOULD HAVE TO CHANGE FOR THIS FILE TO MATTER: nothing that belongs in this file. Adding a
    // caller HERE is the wrong fix - new code calls CoreDeterminismSignals directly. The missing work is
    // an owner elsewhere: a drain for the desync/sync-fence lanes in a dispatcher phase ordered after the
    // flush at SystemDispatcher.cs:3036 (LockstepStateValidator is the natural owner, since it already
    // publishes desync), and a real InputSignal producer in InputDispatcher so ZeroGMovementRuntime.cs:579
    // and the StateCorrection publish path stop reading and writing dead lanes. Both need runtime proof.
    // Until one of those owners exists, this type stays as documentation of the gap. Do not delete it
    // without an owner decision, and do not read its existence as evidence the lanes are wired.
    // ---------------------------------------------------------------------------------------------------
    /// <summary>
    /// Compatibility facade for deterministic locomotion lanes. Core owns the sidecar state.
    /// UNREACHED as of 2026-07-29: this type has zero callers. Read the reachability block above before
    /// assuming any lane it names is wired, and before adding a call to it.
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

        [Obsolete("Use TryPublishKccVelocity(in AbsoluteUniversePosition,float3,uint,uint,byte,byte) so overflow/drop semantics stay visible at the producer.", true)]
        public static void PublishKccVelocity(in AbsoluteUniversePosition bodyAup, float3 velocity, uint frame, uint sourceId, byte flags = 0, byte qualityPressureQ8 = 0)
        {
            TryPublishKccVelocity(in bodyAup, velocity, frame, sourceId, flags, qualityPressureQ8);
        }

        public static bool TryPublishKccVelocity(in AbsoluteUniversePosition bodyAup, float3 velocity, uint frame, uint sourceId, byte flags = 0, byte qualityPressureQ8 = 0)
        {
            KccVelocitySignal signal = default;
            signal.BodyAup = bodyAup;
            signal.Velocity = math.select(velocity, float3.zero, !math.all(math.isfinite(velocity)));
            signal.PlanarSpeedSq = math.lengthsq(new float2(signal.Velocity.x, signal.Velocity.z));
            signal.Frame = frame;
            signal.SourceId = sourceId;
            signal.Flags = flags;
            signal.QualityPressureQ8 = qualityPressureQ8;
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
            return CoreDeterminismSignals.TryGetLatestKccVelocityFloat3(maxFrameAge, out velocity);
        }

        public static bool TryGetLatestKccVelocityVector(uint maxFrameAge, out Vector3 velocity)
        {
            return CoreDeterminismSignals.TryGetLatestKccVelocityVector(maxFrameAge, out velocity);
        }
    }
}
