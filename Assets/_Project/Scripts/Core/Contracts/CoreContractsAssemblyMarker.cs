using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts
{
    /// <summary>
    /// Assembly marker for isolated core contract-only packages.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 1)]
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
    [StructLayout(LayoutKind.Explicit, Size = 24)]
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
        [FieldOffset(20)]
        public uint Reserved0;
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

    /// <summary>
    /// Presentation-only bridge for procedural character animation consumers.
    /// Implemented by the Animation domain; gameplay callers cache this interface instead of depending on a concrete MonoBehaviour type.
    /// </summary>
    public interface IKineticCharacterPresentationSink
    {
        void SubmitSwimPresentation(
            float waveForward,
            float waveLateral,
            float crestReach,
            float descentTuck,
            float leanWeight,
            float immersionDepth,
            float breathingPhase,
            float activeToolWeight);

        void SubmitToolPose(float4x4 localToCameraMatrix, float weight01, uint toolHash);
        void SubmitDamageImpulse(float3 localImpulse, float weight01);
    }
}

namespace Hecton8.Inventory
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct InventorySlotDTO
    {
        [FieldOffset(0)]
        public uint ItemHashID;
        [FieldOffset(4)]
        public uint Quantity;
        [FieldOffset(8)]
        public ulong ContainerAUPHash;
        [FieldOffset(16)]
        public uint ConditionFlags;
        [FieldOffset(20)]
        public uint ReservedLock;
        [FieldOffset(24)]
        private ulong _pad0;
    }
}
