using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Input.Determinism
{
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
    [StructLayout(LayoutKind.Sequential, Size = 24)]
    public struct InputStateDTO
    {
        public float2 LookDelta;
        public float2 MoveAxis;
        public uint ButtonMask;
        private uint _pad0;
    }

    /// <summary>
    /// Sixteen-byte haptic command consumed by math-only decay evaluators.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct HapticCommandDTO
    {
        public float LowFreqIntensity;
        public float HighFreqIntensity;
        public float DecayRate;
        public uint MotorMask;
    }

    /// <summary>
    /// Vault-resident deterministic input tuning values. Designers overwrite this through CSV or editor tooling.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 40)]
    public struct InputProfileDTO
    {
        public float InnerDeadzone;
        public float OuterDeadzone;
        public float MoveExponent;
        public float MouseSensitivity;
        public float MouseAcceleration;
        public float HapticPowerScale;
        public float HapticDispatchIntervalSeconds;
        public float HapticThermalAmplitudeScale;
        public uint Flags;
        private uint _pad0;
    }

    /// <summary>
    /// Three hundred frame black-box record for input latency and haptic load postmortems.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 40)]
    public struct InputTelemetryEntryDTO
    {
        public double InputSystemTimeSeconds;
        public uint Frame;
        public uint Sequence;
        public uint ButtonMask;
        public uint CurrentInputSchemeHash;
        public uint PollingTimeMicroseconds;
        public uint BufferedInputsConsumed;
        public ushort HapticCommandsActive;
        public ushort Flags;
        private uint _pad0;
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct MockCollisionSignal
    {
        public float Magnitude01;
        public uint Frame;
        public uint SourceHash;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct MockToolEquipSignal
    {
        public uint ToolHash;
        public uint Slot;
        public uint Frame;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential, Size = 24)]
    public struct MockPlayerKinematicsSignal
    {
        public double2 AupLocalCell;
        public uint Frame;
        public uint Flags;
    }

    /// <summary>
    /// Cross-assembly ABI for the standardized 60 Hz input tick.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 24)]
    public struct DeterministicInputStateContract
    {
        public uint Frame;
        public uint Sequence;
        public short MoveX;
        public short MoveY;
        public short LookX;
        public short LookY;
        public short Vertical;
        public ushort Flags;
        public uint ButtonsBitmask;
    }
}
