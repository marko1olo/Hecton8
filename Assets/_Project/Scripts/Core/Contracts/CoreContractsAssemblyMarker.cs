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
