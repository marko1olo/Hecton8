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
        public const uint FlagNanSanitized = 1u << 28;
        public const uint FlagFaultDumpRequested = 1u << 29;

        [FieldOffset(0)] public float LowFrequencyMotor01;
        [FieldOffset(4)] public float HighFrequencyMotor01;
        [FieldOffset(8)] public float DurationSeconds;
        [FieldOffset(12)] public uint PriorityFlags;
    }
}
