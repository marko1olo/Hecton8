using System;
using System.Runtime.InteropServices;
using Unity.Collections;

namespace Hecton8.Core.Contracts
{
    /// <summary>
    /// Assembly marker for isolated core contract-only packages.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct CoreContractsAssemblyMarker
    {
    }

    /// <summary>
    /// Coarse platform thermal state used by load-shedding systems.
    /// </summary>
    public enum HardwareThermalSeverity : byte
    {
        Cool = 0,
        Warm = 1,
        Throttling = 2,
        Critical = 3
    }

    /// <summary>
    /// Cached hardware thermal/battery snapshot. Values are written on FrostTick, never per-frame polled.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 20)]
    public struct HardwareThermalSnapshot
    {
        [FieldOffset(0)]
        public byte Severity;
        [FieldOffset(1)]
        public byte PreviousSeverity;
        [FieldOffset(2)]
        public byte BatteryPercent;
        [FieldOffset(3)]
        public byte BatteryStatus;
        [FieldOffset(4)]
        public byte ThermalStatus;
        [FieldOffset(5)]
        public byte Flags;
        [FieldOffset(6)]
        public short TemperatureTenthsCelsius;
        [FieldOffset(8)]
        public uint Sequence;
        [FieldOffset(12)]
        public uint Frame;
        [FieldOffset(16)]
        public uint ActionMask;
    }

    /// <summary>
    /// Registry-owned thermal watchdog service. Implementations must keep hot paths to cached reads.
    /// </summary>
    public interface IHardwareThermalService : IDisposable
    {
        byte CurrentSeverity { get; }
        byte BatteryPercent { get; }
        uint Sequence { get; }
        NativeArray<byte>.ReadOnly ThermalSeverity { get; }
        bool TryGetSnapshot(out HardwareThermalSnapshot snapshot);
        void ForceColdSample();
    }

    /// <summary>
    /// Last committed dynamic-resolution runtime state, stored without managed payloads.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 24)]
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
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]
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
        public int Reserved4;
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
    /// Registry-facing STP dynamic-resolution policy service.
    /// </summary>
    public interface IResolutionScalerService : IDisposable
    {
        float CurrentRenderScale01 { get; }
        float TargetRenderScale01 { get; }
        float SystemStress01 { get; }
        float SystemStressEwma01 { get; }
        float SharpenIntensity01 { get; }
        byte HardwareTier { get; }
        bool StpActive { get; }
        bool TryGetScaleState(out ResolutionScaleState state);
    }

    /// <summary>
    /// Registry-owned render-scale writer. Graphics policy systems push numeric overrides through this contract.
    /// </summary>
    public interface IDynamicResolutionRuntime : IDisposable
    {
        float CurrentRenderScale01 { get; }
        float TargetRenderScale01 { get; }
        bool IsSystemOverrideActive { get; }
        bool IsThermalOverrideActive { get; }
        bool TryGetSnapshot(out DynamicResolutionRuntimeSnapshot snapshot);
        void ApplySystemOverrideRenderScale(
            float currentScale01,
            float targetScale01,
            float frameTimeEwmaMs,
            byte pressureLevel,
            byte flags);
        void ClearSystemOverrideRenderScale();
    }
}
