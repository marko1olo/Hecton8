using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Signals;
using Unity.Mathematics;

namespace Hecton8.Thermodynamics
{
    /// <summary>
    /// Packed unmanaged hazard source. Layout: double3 AUP 24B, intensity 4B, radius 4B,
    /// hazard hash 4B, reserved/pad 4B. Total: 40 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 40)]
    public struct HazardSourceDTO
    {
        public double3 AUP;
        public float Intensity;
        public float Radius;
        public uint HazardTypeHash;
        public uint _pad0;
    }

    /// <summary>
    /// Unmanaged constants edited by the thermodynamics tuner and read by Burst jobs.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct ThermodynamicsHazardConstants
    {
        public float BaseWaterTempCelsius;
        public float HeatDiffusionRate;
        public float RadiationDiffusionRate;
        public float RadiationDecayCoefficient;
        public float RockShieldingFactor;
        public float VerticalHeatBias;
        public float HeatDamageThresholdCelsius;
        public float RadiationDamageThreshold;
    }

    /// <summary>
    /// Smooth local hazard sample returned by trilinear grid queries.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct ThermodynamicsHazardSample
    {
        public float TemperatureCelsius;
        public float Radiation;
        public float HeatDamage;
        public float RadiationDamage;
        public float3 LocalGridPosition;
        public uint Flags;
    }

    /// <summary>
    /// Raw pointer surface for macro-grid buffers. Public readback exposes front pointers only;
    /// back pointers are owner-only and may be null.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct ThermodynamicsHazardGridPointers
    {
        public float* TemperatureFront;
        public float* TemperatureBack;
        public float* RadiationFront;
        public float* RadiationBack;
        public int CellCount;
        public int Resolution;
    }

    /// <summary>
    /// Environment lane payload for hot-cell updraft consumers. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public partial struct ThermalUpdraftSignal : ISignal
    {
        [FieldOffset(0)] public double3 Aup;
        [FieldOffset(24)] public float TemperatureCelsius;
        [FieldOffset(28)] public float Intensity01;
        [FieldOffset(32)] public uint CellIndex;
        [FieldOffset(36)] public uint Frame;
        [FieldOffset(40)] public byte Flags;
    }

    /// <summary>
    /// Local blind damage proof signal. Size: 48 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 48)]
    public partial struct MockDamageSignal : ISignal
    {
        public double3 Aup;
        public float3 Normal;
        public float Damage;
        public uint EntityId;
        public uint Flags;
    }

    /// <summary>
    /// ARM64-safe local staging DTO converted to CombatDamageSignal only at publish. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct ThermodynamicsCombatDamageSignal
    {
        public float3 WorldPoint;
        public float3 Direction;
        public float Magnitude;
        public uint DamageType;
        public uint TargetHash;
        public uint SourceHash;
        public uint Frame;
        public ushort SourceId;
        public ushort TargetId;
        public byte Channel;
        public byte Flags;
        public byte IntegrityDelta;
        public byte _pad0;
        public uint _pad1;
        public uint _pad2;
        public uint _pad3;
    }

    /// <summary>
    /// One black-box frame for the thermodynamics hazard grid. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct ThermodynamicsHazardTelemetryEntry
    {
        public float MaxGridTemperature;
        public float MaxRadiationLevel;
        public float DiffusionComputeTimeMs;
        public float3 GridOrigin;
        public uint Frame;
        public uint GridVersion;
        public uint SourceCount;
        public uint Flags;
        public uint ShiftSequence;
        public uint NaNCellIndex;
        public uint ActiveResolution;
        public uint GridOriginHash;
        public uint _pad0;
        public uint _pad1;
    }
}
