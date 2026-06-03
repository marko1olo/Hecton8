using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts;

namespace Hecton8.Core.Contracts.Signals
{
    /// <summary>
    /// Final PAL-facing haptic envelope. Size: 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct HapticPulseSignal : ISignal
    {
        public const uint LaneHash = 0x4850524Du; // HPRM
        public const uint PriorityCollision = 1u << 0;
        public const uint PriorityExplosion = 1u << 1;
        public const uint PriorityTool = 1u << 2;
        public const uint PriorityMask = PriorityCollision | PriorityExplosion | PriorityTool;
        public const int SourceHashShift = 3;
        public const uint SourceHashPayloadMask = 0x01FFFFFFu;
        public const uint SourceHashPackedMask = SourceHashPayloadMask << SourceHashShift;
        public const uint FlagNanSanitized = 1u << 28;
        public const uint FlagFaultDumpRequested = 1u << 29;
        public const uint FlagMask = FlagNanSanitized | FlagFaultDumpRequested;

        [FieldOffset(0)] public float LowFrequencyMotor01;
        [FieldOffset(4)] public float HighFrequencyMotor01;
        [FieldOffset(8)] public float DurationSeconds;
        [FieldOffset(12)] public uint PriorityFlags;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint PackPriorityAndSourceHash(uint priorityFlags, uint sourceHash)
        {
            return (priorityFlags & (PriorityMask | FlagMask)) | ((sourceHash & SourceHashPayloadMask) << SourceHashShift);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ExtractPriorityFlags(uint priorityFlags)
        {
            return priorityFlags & PriorityMask;
        }
    }
}
