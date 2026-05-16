using System;
using System.Runtime.InteropServices;
using Unity.Collections;

namespace Hecton8.Core.Contracts
{
    /// <summary>
    /// Assembly marker for isolated core contract-only packages.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
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
    [StructLayout(LayoutKind.Sequential)]
    public struct HardwareThermalSnapshot
    {
        public byte Severity;
        public byte PreviousSeverity;
        public byte BatteryPercent;
        public byte BatteryStatus;
        public byte ThermalStatus;
        public byte Flags;
        public short TemperatureTenthsCelsius;
        public uint Sequence;
        public uint Frame;
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
    [StructLayout(LayoutKind.Sequential)]
    public struct DynamicResolutionRuntimeSnapshot
    {
        public float CurrentRenderScale01;
        public float TargetRenderScale01;
        public float FrameTimeEwmaMs;
        public byte PressureLevel;
        public byte Flags;
        public uint Frame;
        public uint Sequence;
    }

    /// <summary>
    /// DataVault-backed STP render-scale state. One element is owned by the graphics scalability adapter.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 64)]
    public struct ResolutionScaleState
    {
        public float CurrentRenderScale01;
        public float TargetRenderScale01;
        public float SystemStress01;
        public float SystemStressEwma01;
        public float FrameTimeEwmaMs;
        public float SharpenIntensity01;
        public uint Frame;
        public uint Sequence;
        public byte HardwareTier;
        public byte StpActive;
        public byte Flags;
        public byte AupLockFrames;
        public int Reserved0;
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
