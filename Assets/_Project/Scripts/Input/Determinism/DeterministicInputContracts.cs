using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Input.Determinism
{
    internal static class DeterministicInputContractLayout
    {
        public const int InputStateStrideBytes = 32;
        public const int HapticCommandStrideBytes = 16;
        public const int InputProfileStrideBytes = 64;
        public const int DeterministicTelemetryEntryStrideBytes = 64;
        public const int DeterministicMockCollisionSignalStrideBytes = 16;
        public const int DeterministicMockToolEquipSignalStrideBytes = 16;
        public const int DeterministicMockPlayerKinematicsSignalStrideBytes = 32;
        public const int StateContractStrideBytes = 32;
    }

    [Flags]
    public enum DeterministicInputContractFlags : ushort
    {
        None = 0,
        AutomationOverride = 1 << 0,
        DelayApplied = 1 << 1,
        NonFiniteSanitized = 1 << 2
    }

    [Flags]
    public enum InputBlockMaskFlags : uint
    {
        None = 0u,
        BlockMovement = 1u << 0,
        BlockLook = 1u << 1,
        BlockTools = 1u << 2,
        BlockDiscrete = 1u << 3
    }

    /// <summary>
    /// Authoritative unmanaged input frame for lockstep, replay, and rollback.
    /// Layout: 0 float2 LookDelta, 8 float2 MoveAxis, 16 uint ButtonMask, 20 uint padding.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = DeterministicInputContractLayout.InputStateStrideBytes)]
    public struct InputStateDTO
    {
        [FieldOffset(0)] public float2 LookDelta;
        [FieldOffset(8)] public float2 MoveAxis;
        [FieldOffset(16)] public uint ButtonMask;
        [FieldOffset(20)] private uint _pad0;
        [FieldOffset(24)] private ulong _pad1;
    }

    /// <summary>
    /// Sixteen-byte haptic command consumed by math-only decay evaluators.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = DeterministicInputContractLayout.HapticCommandStrideBytes)]
    public struct HapticCommandDTO
    {
        [FieldOffset(0)] public float LowFreqIntensity;
        [FieldOffset(4)] public float HighFreqIntensity;
        [FieldOffset(8)] public float DecayRate;
        [FieldOffset(12)] public uint MotorMask;
    }

    /// <summary>
    /// Vault-resident deterministic input tuning values. Designers overwrite this through CSV or editor tooling.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = DeterministicInputContractLayout.InputProfileStrideBytes)]
    public struct InputProfileDTO
    {
        [FieldOffset(0)] public float InnerDeadzone;
        [FieldOffset(4)] public float OuterDeadzone;
        [FieldOffset(8)] public float MoveExponent;
        [FieldOffset(12)] public float MouseSensitivity;
        [FieldOffset(16)] public float MouseAcceleration;
        [FieldOffset(20)] public float HapticPowerScale;
        [FieldOffset(24)] public float HapticDispatchIntervalSeconds;
        [FieldOffset(28)] public float HapticThermalAmplitudeScale;
        [FieldOffset(32)] public uint Flags;
        [FieldOffset(36)] private uint _pad0;
        [FieldOffset(40)] private ulong _pad1;
        [FieldOffset(48)] private ulong _pad2;
        [FieldOffset(56)] private ulong _pad3;
    }

    /// <summary>
    /// Three hundred frame black-box record for input latency and haptic load postmortems.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = DeterministicInputContractLayout.DeterministicTelemetryEntryStrideBytes)]
    public struct DeterministicInputTelemetryEntryDTO
    {
        [FieldOffset(0)] public double InputSystemTimeSeconds;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint Sequence;
        [FieldOffset(16)] public uint ButtonMask;
        [FieldOffset(20)] public uint CurrentInputSchemeHash;
        [FieldOffset(24)] public uint PollingTimeMicroseconds;
        [FieldOffset(28)] public uint BufferedInputsConsumed;
        [FieldOffset(32)] public ushort HapticCommandsActive;
        [FieldOffset(34)] public ushort Flags;
        [FieldOffset(36)] private uint _pad0;
        [FieldOffset(40)] private ulong _pad1;
        [FieldOffset(48)] private ulong _pad2;
        [FieldOffset(56)] private ulong _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = DeterministicInputContractLayout.DeterministicMockCollisionSignalStrideBytes)]
    public struct DeterministicMockCollisionSignal
    {
        [FieldOffset(0)] public float Magnitude01;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public uint SourceHash;
        [FieldOffset(12)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = DeterministicInputContractLayout.DeterministicMockToolEquipSignalStrideBytes)]
    public struct DeterministicMockToolEquipSignal
    {
        [FieldOffset(0)] public uint ToolHash;
        [FieldOffset(4)] public uint Slot;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = DeterministicInputContractLayout.DeterministicMockPlayerKinematicsSignalStrideBytes)]
    public struct DeterministicMockPlayerKinematicsSignal
    {
        [FieldOffset(0)] public double2 AupLocalCell;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] private ulong _pad0;
    }

    /// <summary>
    /// Cross-assembly ABI for the standardized 60 Hz input tick.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = DeterministicInputContractLayout.StateContractStrideBytes)]
    public struct DeterministicInputStateContract
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint Sequence;
        [FieldOffset(8)] public short MoveX;
        [FieldOffset(10)] public short MoveY;
        [FieldOffset(12)] public short LookX;
        [FieldOffset(14)] public short LookY;
        [FieldOffset(16)] public short Vertical;
        [FieldOffset(18)] public ushort Flags;
        [FieldOffset(20)] public uint ButtonsBitmask;
        [FieldOffset(24)] private ulong _pad0;
    }
}
