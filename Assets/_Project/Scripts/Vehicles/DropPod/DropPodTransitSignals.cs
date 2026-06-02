namespace Hecton8.Vehicles.DropPod
{
    using System.Runtime.InteropServices;
    using System.Threading;
    using Hecton8.Core.Contracts.Signals;
    using Unity.Mathematics;

    public enum DropPodCommandId : uint
    {
        None = 0u,
        LockHatch = 1u,
        UnlockHatch = 2u,
        StrapIn = 3u,
        IgniteEngines = 4u,
        AbortTransit = 5u,
        ToggleAuxPower = 6u,
        SeatTransitStarted = 7u,
        SeatTransitCompleted = 8u,
        DashboardToggle = 9u,
    }

    public enum DropPodStatusId : uint
    {
        None = 0u,
        Idle = 1u,
        AirlockMoving = 2u,
        AirlockSealed = 3u,
        AirlockOpen = 4u,
        SeatTransitArmed = 5u,
        SeatTransitActive = 6u,
        Seated = 7u,
        EngineIgnitionArmed = 8u,
        FailClosed = 9u,
        SeatBlockedAirlockOpen = 10u,
    }

    public static class DropPodSignalFlags
    {
        public const byte None = 0;
        public const byte PhysicalHand = 1 << 0;
        public const byte PlayerFallback = 1 << 1;
        public const byte FailClosed = 1 << 2;
        public const byte VisualOnly = 1 << 3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct DropPodCommandSignal : ISignal
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint CommandId;
        [FieldOffset(8)] public uint SourceId;
        [FieldOffset(12)] public byte Flags;
        [FieldOffset(13)] public byte QualityByte;
        [FieldOffset(14)] public ushort Sequence;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct DropPodStatusSignal : ISignal
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint StatusId;
        [FieldOffset(8)] public uint SourceId;
        [FieldOffset(12)] public byte Flags;
        [FieldOffset(13)] public byte QualityByte;
        [FieldOffset(14)] public ushort Sequence;
    }

    public static class DropPodSignalLaneBootstrap
    {
        private const int CommandCapacity = 32;
        private const int CommandMaxPerFrame = 16;
        private const int CommandLowTierPerFrame = 4;
        private const int StatusCapacity = 32;
        private const int StatusMaxPerFrame = 16;
        private const int StatusLowTierPerFrame = 4;
        private const uint CommandLaneHash = 0x4450434Du; // DPCM
        private const uint StatusLaneHash = 0x44505354u; // DPST
        private const int SequenceBits = 16;
        private const long SequenceMask = ushort.MaxValue;
        private static long s_signalSequenceState;
        private static bool s_configured;

        public static void EnsureConfigured()
        {
            if (s_configured &&
                SignalBus<DropPodCommandSignal>.HasNativeStorage &&
                SignalBus<DropPodStatusSignal>.HasNativeStorage)
            {
                return;
            }

            SignalBus<DropPodCommandSignal>.Configure(CommandCapacity, CommandMaxPerFrame, CommandLowTierPerFrame, CommandLaneHash);
            SignalBus<DropPodStatusSignal>.Configure(StatusCapacity, StatusMaxPerFrame, StatusLowTierPerFrame, StatusLaneHash);
            SignalBus<DropPodCommandSignal>.EnsureInitialized();
            SignalBus<DropPodStatusSignal>.EnsureInitialized();
            s_configured = true;
        }

        public static byte EncodeQualityByte(float quality01)
        {
            float sanitized = DropPodSplineMath.SanitizeUnit01(quality01);
            return (byte)math.clamp((int)math.round(sanitized * byte.MaxValue), 0, byte.MaxValue);
        }

        public static ushort NextSequence(uint frame)
        {
            EnsureConfigured();
            long frameBits = (long)frame << SequenceBits;
            while (true)
            {
                long observed = Interlocked.Read(ref s_signalSequenceState);
                uint observedFrame = (uint)(observed >> SequenceBits);
                int observedSequence = observedFrame == frame ? (int)(observed & SequenceMask) : 0;
                int nextSequence = observedSequence >= ushort.MaxValue ? ushort.MaxValue : observedSequence + 1;
                long nextState = frameBits | (uint)nextSequence;
                if (Interlocked.CompareExchange(ref s_signalSequenceState, nextState, observed) == observed)
                    return (ushort)nextSequence;
            }
        }

        public static bool IsNewerSignal(uint frame, ushort sequence, uint lastFrame, ushort lastSequence)
        {
            if (frame > lastFrame)
                return true;

            if (frame < lastFrame)
                return false;

            return lastSequence == 0 || sequence > lastSequence;
        }
    }
}
