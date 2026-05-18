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

    [StructLayout(LayoutKind.Sequential, Size = 24)]
    public struct InputStateDTO
    {
        public float2 LookDelta;
        public float2 MoveAxis;
        public uint ButtonMask;
        private uint _pad0;
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct HapticCommandDTO
    {
        public float LowFreqIntensity;
        public float HighFreqIntensity;
        public float DecayRate;
        public uint MotorMask;
    }

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
}
