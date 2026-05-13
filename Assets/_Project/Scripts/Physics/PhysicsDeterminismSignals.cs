using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Physics
{
    /// <summary>
    /// NativeQueue-backed signal lanes for deterministic locomotion authority.
    /// </summary>
    public static class PhysicsDeterminismSignals
    {
        private const int InputSignalCapacity = 128;
        private const int StateCorrectionSignalCapacity = 16;
        private const int DesyncDetectedSignalCapacity = 16;
        private const int SyncFenceSignalCapacity = 32;
        private const string NativeMemoryOwner = nameof(PhysicsDeterminismSignals);

        private static NativeQueue<InputSignal> _inputSignals;
        private static NativeQueue<StateCorrectionSignal> _stateCorrectionSignals;
        private static NativeQueue<DesyncDetectedSignal> _desyncDetectedSignals;
        private static NativeQueue<SyncFenceSignal> _syncFenceSignals;
        private static int _inputSignalCount;
        private static int _stateCorrectionSignalCount;
        private static int _desyncDetectedSignalCount;
        private static int _syncFenceSignalCount;
        private static bool _initialized;
        private static uint _inputSequence;
        private static uint _inputOverrideSequence;
        private static uint _syncFenceSequence;
        private static InputSignal _latestInputSignal;
        private static InputSignal _latestInputOverrideSignal;
        private static SyncFenceSignal _latestSyncFenceSignal;
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
            EnqueueBounded(ref _inputSignals, ref _inputSignalCount, InputSignalCapacity, in signal);
        }

        public static void Publish(in StateCorrectionSignal signal)
        {
            EnsureInitialized();
            EnqueueBounded(ref _stateCorrectionSignals, ref _stateCorrectionSignalCount, StateCorrectionSignalCapacity, in signal);
        }

        public static void Publish(in DesyncDetectedSignal signal)
        {
            EnsureInitialized();
            EnqueueBounded(ref _desyncDetectedSignals, ref _desyncDetectedSignalCount, DesyncDetectedSignalCapacity, in signal);
        }

        public static void Publish(in SyncFenceSignal signal)
        {
            EnsureInitialized();
            SyncFenceSignal sequenced = signal;
            sequenced.Sequence = NextSequence(ref _syncFenceSequence);
            _latestSyncFenceSignal = sequenced;
            EnqueueBounded(ref _syncFenceSignals, ref _syncFenceSignalCount, SyncFenceSignalCapacity, in sequenced);
        }

        public static bool TryDequeueInput(out InputSignal signal) => TryDequeue(ref _inputSignals, ref _inputSignalCount, out signal);

        public static bool TryDequeueStateCorrection(out StateCorrectionSignal signal) => TryDequeue(ref _stateCorrectionSignals, ref _stateCorrectionSignalCount, out signal);

        public static bool TryDequeueDesyncDetected(out DesyncDetectedSignal signal) => TryDequeue(ref _desyncDetectedSignals, ref _desyncDetectedSignalCount, out signal);

        public static bool TryDequeueSyncFence(out SyncFenceSignal signal) => TryDequeue(ref _syncFenceSignals, ref _syncFenceSignalCount, out signal);

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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            DisposeAllQueues();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void RegisterQuitHook()
        {
            Application.quitting -= DisposeAllQueues;
            Application.quitting += DisposeAllQueues;
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
                return;

            CreateQueue(ref _inputSignals, InputSignalCapacity, nameof(_inputSignals));
            CreateQueue(ref _stateCorrectionSignals, StateCorrectionSignalCapacity, nameof(_stateCorrectionSignals));
            CreateQueue(ref _desyncDetectedSignals, DesyncDetectedSignalCapacity, nameof(_desyncDetectedSignals));
            CreateQueue(ref _syncFenceSignals, SyncFenceSignalCapacity, nameof(_syncFenceSignals));
            _initialized = true;
        }

        private static void DisposeAllQueues()
        {
            DisposeQueue(ref _inputSignals, nameof(_inputSignals));
            DisposeQueue(ref _stateCorrectionSignals, nameof(_stateCorrectionSignals));
            DisposeQueue(ref _desyncDetectedSignals, nameof(_desyncDetectedSignals));
            DisposeQueue(ref _syncFenceSignals, nameof(_syncFenceSignals));
            _latestInputSignal = default;
            _latestInputOverrideSignal = default;
            _latestSyncFenceSignal = default;
            _inputSequence = 0u;
            _inputOverrideSequence = 0u;
            _syncFenceSequence = 0u;
            _inputSignalCount = 0;
            _stateCorrectionSignalCount = 0;
            _desyncDetectedSignalCount = 0;
            _syncFenceSignalCount = 0;
            _initialized = false;
        }

        private static void CreateQueue<T>(ref NativeQueue<T> queue, int expectedCapacity, string label)
            where T : unmanaged
        {
            if (queue.IsCreated)
                return;

            queue = new NativeQueue<T>(Allocator.Persistent); // COLD ALLOC: NativeQueue<T>[expectedCapacity] - determinism signal lane - owner: PhysicsDeterminismSignals
            NativeMemorySentinel.RegisterNativeQueue(
                queue,
                expectedCapacity,
                NativeMemoryOwner,
                label,
                NativeAllocationLifetime.Session);
            for (int i = 0; i < expectedCapacity; i++)
                queue.Enqueue(default);
            for (int i = 0; i < expectedCapacity; i++)
                queue.TryDequeue(out _);
        }

        private static void EnqueueBounded<T>(ref NativeQueue<T> queue, ref int queuedCount, int capacity, in T signal)
            where T : unmanaged
        {
            if (queuedCount >= capacity)
            {
                if (queue.TryDequeue(out _))
                    queuedCount--;
                else
                    queuedCount = 0;
            }

            queue.Enqueue(signal);
            queuedCount++;
        }

        private static bool TryDequeue<T>(ref NativeQueue<T> queue, ref int queuedCount, out T signal)
            where T : unmanaged
        {
            if (!queue.IsCreated)
            {
                signal = default;
                return false;
            }

            if (!queue.TryDequeue(out signal))
                return false;

            if (queuedCount > 0)
                queuedCount--;
            return true;
        }

        private static void DisposeQueue<T>(ref NativeQueue<T> queue, string label)
            where T : unmanaged
        {
            if (!queue.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeQueue(NativeMemoryOwner, label);
            queue.Dispose();
            queue = default;
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

    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 48)]
    public struct InputSignal
    {
        public float2 MoveDelta;
        public float2 LookDelta;
        public float VerticalDelta;
        public uint ActionsBitmask;
        public uint Frame;
        public uint Sequence;
        public byte Flags;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 128)]
    public struct StateCorrectionSignal
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

    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 32)]
    public struct DesyncDetectedSignal
    {
        public uint LocalHash;
        public uint AuthoritativeHash;
        public uint Frame;
        public uint SourceId;
        public uint LastFenceFrame;
        public byte Flags;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 128)]
    public struct SyncFenceSignal
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
}
