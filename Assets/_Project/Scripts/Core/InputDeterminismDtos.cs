using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Core
{
    [Flags]
    public enum InputBlockMaskFlags : uint
    {
        None = 0u,
        BlockMovement = 1u << 0,
        BlockLook = 1u << 1,
        BlockTools = 1u << 2,
        BlockDiscrete = 1u << 3
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public struct InputStateDTO
    {
        [FieldOffset(0)] public float2 LookDelta;
        [FieldOffset(8)] public float2 MoveAxis;
        [FieldOffset(16)] public uint ButtonMask;
        [FieldOffset(20)] private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct HapticCommandDTO
    {
        [FieldOffset(0)] public float LowFreqIntensity;
        [FieldOffset(4)] public float HighFreqIntensity;
        [FieldOffset(8)] public float DecayRate;
        [FieldOffset(12)] public uint MotorMask;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
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

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct InputTelemetryEntryDTO
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

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct MockCollisionSignal
    {
        [FieldOffset(0)] public float Magnitude01;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public uint SourceHash;
        [FieldOffset(12)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct MockToolEquipSignal
    {
        [FieldOffset(0)] public uint ToolHash;
        [FieldOffset(4)] public uint Slot;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct MockPlayerKinematicsSignal
    {
        [FieldOffset(0)] public double2 AupLocalCell;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] private ulong _pad0;
    }
}
