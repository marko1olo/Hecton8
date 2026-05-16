using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Physics
{
    /// <summary>
    /// NativeQueue-backed signal lanes for deterministic locomotion authority.
    /// </summary>
    public static class PhysicsDeterminismSignals
    {
        private static bool _initialized;
        private static uint _inputSequence;
        private static uint _inputOverrideSequence;
        private static uint _syncFenceSequence;
        private static uint _kccVelocitySequence;
        private static InputSignal _latestInputSignal;
        private static InputSignal _latestInputOverrideSignal;
        private static SyncFenceSignal _latestSyncFenceSignal;
        private static KccVelocitySignal _latestKccVelocitySignal;
        public const byte InputSignalFlagAutomationOverride = 1 << 0;
        public const byte StateCorrectionSignalFlagRuntimePositionValid = 1 << 0;
        public const byte StateCorrectionSignalFlagRotationValid = 1 << 1;
        public const byte StateCorrectionSignalFlagVelocityValid = 1 << 2;

        public static void PublishInput(in PlayerInputState state, uint frame, byte flags = 0)
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
            Publish(in signal);
        }

        public static void PublishInputOverride(in PlayerInputState state, uint frame)
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
        }

        public static void ClearInputOverride()
        {
            _latestInputOverrideSignal = default;
        }

        public static void Publish(in InputSignal signal)
        {
            EnsureInitialized();
            _latestInputSignal = signal;
            SignalBus<InputSignal>.Push(in signal);
        }

        public static void Publish(in StateCorrectionSignal signal)
        {
            EnsureInitialized();
            SignalBus<StateCorrectionSignal>.Push(in signal);
        }

        public static void Publish(in DesyncDetectedSignal signal)
        {
            EnsureInitialized();
            SignalBus<DesyncDetectedSignal>.Push(in signal);
        }

        public static void Publish(in SyncFenceSignal signal)
        {
            EnsureInitialized();
            SyncFenceSignal sequenced = signal;
            sequenced.Sequence = NextSequence(ref _syncFenceSequence);
            _latestSyncFenceSignal = sequenced;
            SignalBus<SyncFenceSignal>.Push(in sequenced);
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
            Publish(in signal);
        }

        public static void Publish(in KccVelocitySignal signal)
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
            SignalBus<KccVelocitySignal>.Push(in sequenced);
        }

        public static bool TryDequeueInput(out InputSignal signal) => TryReadLane(out signal);

        public static bool TryDequeueStateCorrection(out StateCorrectionSignal signal) => TryReadLane(out signal);

        public static bool TryDequeueDesyncDetected(out DesyncDetectedSignal signal) => TryReadLane(out signal);

        public static bool TryDequeueSyncFence(out SyncFenceSignal signal) => TryReadLane(out signal);

        public static bool TryDequeueKccVelocity(out KccVelocitySignal signal) => TryReadLane(out signal);

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
                return false;

            uint age = frame - signal.Frame;
            if (age > maxFrameAge)
            {
                _latestInputOverrideSignal = default;
                return false;
            }

            _latestInputOverrideSignal = default;
            return true;
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

            GlobalSignals.InitializeAllQueues();
            SignalBus<InputSignal>.EnsureInitialized();
            SignalBus<StateCorrectionSignal>.EnsureInitialized();
            SignalBus<DesyncDetectedSignal>.EnsureInitialized();
            SignalBus<SyncFenceSignal>.EnsureInitialized();
            SignalBus<KccVelocitySignal>.EnsureInitialized();
            _initialized = true;
        }

        private static bool TryReadLane<T>(out T signal)
            where T : unmanaged, ISignal
        {
            EnsureInitialized();
            return SignalBus<T>.TryReadFrame(out signal);
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

namespace Hecton8.Core.Contracts.Signals
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]
    public struct InputSignal : ISignal
    {
        public float2 MoveDelta;
        public float2 LookDelta;
        public float VerticalDelta;
        public uint ActionsBitmask;
        public uint CurrentInputSchemeHash;
        public uint Frame;
        public uint Sequence;
        public byte Flags;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 128)]
    public struct StateCorrectionSignal : ISignal
    {
        public AbsoluteUniversePosition PositionAup;
        public float3 RuntimePosition;
        public float3 Velocity;
        public quaternion Rotation;
        public uint AuthoritativeHash;
        public uint ExpectedLocalHash;
        public uint Frame;
        public uint SourceId;
        public uint Sequence;
        public byte Flags;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]
    public struct DesyncDetectedSignal : ISignal
    {
        public uint LocalHash;
        public uint AuthoritativeHash;
        public uint Frame;
        public uint SourceId;
        public uint LastFenceFrame;
        public byte Flags;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 128)]
    public struct SyncFenceSignal : ISignal
    {
        public AbsoluteUniversePosition PositionAup;
        public float3 RuntimePosition;
        public float3 Velocity;
        public quaternion Rotation;
        public uint StateHash;
        public uint Frame;
        public uint SourceId;
        public uint Sequence;
        public byte Flags;
    }

    /// <summary>
    /// Decoupled player KCC velocity lane consumed by presentation systems such as lower-body IK.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 80)]
    public struct KccVelocitySignal : ISignal
    {
        public const byte FlagLowTier = 1 << 0;
        public const byte FlagMovementAuthorityExternal = 1 << 1;

        public AbsoluteUniversePosition BodyAup;
        public float3 Velocity;
        public float PlanarSpeedSq;
        public uint Frame;
        public uint SourceId;
        public uint Sequence;
        public byte Flags;
    }
}
