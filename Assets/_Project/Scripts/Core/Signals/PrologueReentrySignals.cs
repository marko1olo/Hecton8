using System.Runtime.InteropServices;
using Hecton8.World;

namespace Hecton8.Core.Signals
{
    /// <summary>
    /// Re-entry VFX blackbox mirror for downstream diagnostics. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ReentryVfxStateSignal : ISignal
    {
        public const byte FlagLowTier = 1 << 0;
        public const byte FlagWhiteout = 1 << 1;
        public const byte FlagHydrated = 1 << 2;
        public const byte FlagNaNGuard = 1 << 3;
        public const byte FlagSpatialAnchor = 1 << 4;

        [FieldOffset(0)] public AbsoluteUniversePosition CapsuleAup;
        [FieldOffset(48)] public float Heat01;
        [FieldOffset(52)] public float Opacity01;
        [FieldOffset(56)] public ushort Sequence;
        [FieldOffset(58)] public ushort HydrationSequence;
        [FieldOffset(60)] public byte Phase;
        [FieldOffset(61)] public byte Flags;
        [FieldOffset(62)] public byte QualityTier;
        [FieldOffset(63)] public byte Reserved;
    }

    /// <summary>
    /// Visor-local external droplet request. Presentation-only; not a fluid simulation packet. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VisorDropletSignal : ISignal
    {
        public const byte DropletKindMassiveSplash = 1;
        public const byte FlagExternalSplash = 1 << 0;

        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public float Intensity01;
        [FieldOffset(52)] public float DurationSeconds;
        [FieldOffset(56)] public uint SourceHash;
        [FieldOffset(60)] public byte DropletKind;
        [FieldOffset(61)] public byte Flags;
        [FieldOffset(62)] public ushort Sequence;
    }

    /// <summary>
    /// Cold-start prewarm for orbital drop signal lanes.
    /// </summary>
    public static class PrologueReentrySignalLanes
    {
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;

        /// <summary>
        /// Ensures the prologue signal lanes allocate before the whiteout moment.
        /// </summary>
        public static void Warm()
        {
            SignalBus<AtmosphericReentrySignal>.Configure(32, laneHash: ComputeStableSignalLaneHash(nameof(AtmosphericReentrySignal)));
            SignalBus<AtmosphericReentrySignal>.EnsureInitialized();
            SignalBus<PrologueCompleteSignal>.Configure(8, laneHash: ComputeStableSignalLaneHash(nameof(PrologueCompleteSignal)));
            SignalBus<PrologueCompleteSignal>.EnsureInitialized();
            SignalBus<ManualOverridePulledSignal>.Configure(8, laneHash: ComputeStableSignalLaneHash(nameof(ManualOverridePulledSignal)));
            SignalBus<ManualOverridePulledSignal>.EnsureInitialized();
            SignalBus<ReentryVfxStateSignal>.Configure(4, 16, 4, ComputeStableSignalLaneHash(nameof(ReentryVfxStateSignal)));
            SignalBus<ReentryVfxStateSignal>.EnsureInitialized();
            SignalBus<VisorDropletSignal>.Configure(8, 32, 8, ComputeStableSignalLaneHash(nameof(VisorDropletSignal)));
            SignalBus<VisorDropletSignal>.EnsureInitialized();
        }

        private static uint ComputeStableSignalLaneHash(string label)
        {
            uint hash = FnvOffset;
            if (!string.IsNullOrEmpty(label))
            {
                for (int i = 0; i < label.Length; i++)
                {
                    hash ^= label[i];
                    hash *= FnvPrime;
                }
            }

            return hash == 0u ? 1u : hash;
        }
    }
}
