using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Signals;

namespace Hecton8.Core.Contracts
{
    /// <summary>
    /// Last committed dynamic-resolution runtime state, stored without managed payloads.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public struct DynamicResolutionRuntimeSnapshot
    {
        [FieldOffset(0)]
        public float CurrentRenderScale01;
        [FieldOffset(4)]
        public float TargetRenderScale01;
        [FieldOffset(8)]
        public float FrameTimeEwmaMs;
        [FieldOffset(12)]
        public byte PressureLevel;
        [FieldOffset(13)]
        public byte Flags;
        [FieldOffset(14)]
        public byte Reserved0;
        [FieldOffset(15)]
        public byte Reserved1;
        [FieldOffset(16)]
        public uint Frame;
        [FieldOffset(20)]
        public uint Sequence;
    }

    /// <summary>
    /// DataVault-backed STP render-scale state. One element is owned by the graphics scalability adapter.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ResolutionScaleState
    {
        [FieldOffset(0)]
        public float CurrentRenderScale01;
        [FieldOffset(4)]
        public float TargetRenderScale01;
        [FieldOffset(8)]
        public float SystemStress01;
        [FieldOffset(12)]
        public float SystemStressEwma01;
        [FieldOffset(16)]
        public float FrameTimeEwmaMs;
        [FieldOffset(20)]
        public float SharpenIntensity01;
        [FieldOffset(24)]
        public uint Frame;
        [FieldOffset(28)]
        public uint Sequence;
        [FieldOffset(32)]
        public byte HardwareTier;
        [FieldOffset(33)]
        public byte StpActive;
        [FieldOffset(34)]
        public byte Flags;
        [FieldOffset(35)]
        public byte AupLockFrames;
        [FieldOffset(36)]
        public int Reserved0;
        [FieldOffset(40)]
        public float VisualOverkill01;
        [FieldOffset(44)]
        public float DearLie01;
        [FieldOffset(48)]
        public uint VisualFeatureFlags;
        [FieldOffset(52)]
        public float GlobalQualityWeight01;
        [FieldOffset(56)]
        public int Reserved5;
        [FieldOffset(60)]
        public int Reserved6;
    }

    /// <summary>
    /// Flag bits packed into <see cref="ResolutionScaleState.Flags"/>.
    /// </summary>
    public static class ResolutionScaleStateFlags
    {
        public const byte LowTierEmergency = 1 << 0;
        public const byte FramePressure = 1 << 1;
        public const byte ThermalPressure = 1 << 2;
        public const byte AupLocked = 1 << 3;
        public const byte InvalidStateRecovered = 1 << 4;
    }

    /// <summary>
    /// SIMD-aligned dynamic-resolution hot state. Size: 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct DrsStateDTO
    {
        public float CurrentRenderScale;
        public float TargetRenderScale;
        public uint UpscalerTypeHash;
        public uint _pad0;
    }

    /// <summary>
    /// Mock quality-weight payload for blind SHI/Scalability Dictator integration tests. Size: 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public partial struct MockQualityWeightSignal : ISignal
    {
        public float GlobalQualityWeight;
        public float FrameTimeMs;
        public uint Flags;
        public uint _pad0;
    }
}
