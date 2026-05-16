using System.Runtime.InteropServices;
using Hecton8.World;

namespace Hecton8.Core.Contracts.Signals
{
    /// <summary>
    /// Re-entry VFX blackbox mirror for downstream diagnostics. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
    public struct ReentryVfxStateSignal : ISignal
    {
        public const byte FlagLowTier = 1 << 0;
        public const byte FlagWhiteout = 1 << 1;
        public const byte FlagHydrated = 1 << 2;
        public const byte FlagNaNGuard = 1 << 3;
        public const byte FlagSpatialAnchor = 1 << 4;

        public AbsoluteUniversePosition CapsuleAup;
        public float Heat01;
        public float Opacity01;
        public ushort Sequence;
        public ushort HydrationSequence;
        public byte Phase;
        public byte Flags;
        public byte QualityTier;
        public byte Reserved;
    }

    /// <summary>
    /// Visor-local external droplet request. Presentation-only; not a fluid simulation packet. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
    public struct VisorDropletSignal : ISignal
    {
        public const byte DropletKindMassiveSplash = 1;
        public const byte FlagExternalSplash = 1 << 0;

        public AbsoluteUniversePosition PositionAup;
        public float Intensity01;
        public float DurationSeconds;
        public uint SourceHash;
        public byte DropletKind;
        public byte Flags;
        public ushort Sequence;
    }

    /// <summary>
    /// Cold-start prewarm for orbital drop signal lanes.
    /// </summary>
    public static class PrologueReentrySignalLanes
    {
        /// <summary>
        /// Ensures the prologue signal lanes allocate before the whiteout moment.
        /// </summary>
        public static void Warm()
        {
            GlobalSignals.InitializeAllQueues();
        }
    }
}
