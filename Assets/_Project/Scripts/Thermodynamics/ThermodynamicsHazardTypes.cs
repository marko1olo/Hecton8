using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Signals;
using Unity.Collections.LowLevel.Unsafe;
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
    public partial struct ThermodynamicsMockDamageSignal : ISignal
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
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ThermodynamicsCombatDamageSignal
    {
        [FieldOffset(0)] public double3 ImpactAup;
        [FieldOffset(24)] public float3 Direction;
        [FieldOffset(36)] public float Magnitude;
        [FieldOffset(40)] public uint DamageType;
        [FieldOffset(44)] public uint TargetHash;
        [FieldOffset(48)] public uint SourceHash;
        [FieldOffset(52)] public uint Frame;
        [FieldOffset(56)] public ushort SourceId;
        [FieldOffset(58)] public ushort TargetId;
        [FieldOffset(60)] public byte Channel;
        [FieldOffset(61)] public byte Flags;
        [FieldOffset(62)] public byte IntegrityDelta;
        [FieldOffset(63)] public byte _pad0;
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

    /// <summary>
    /// ABYSSAL thermodynamics cell. Exactly one 16-byte SIMD lane, four cells per 64-byte cache fetch.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct ThermalCellDTO
    {
        [FieldOffset(0)] public float TemperatureCelsius;
        [FieldOffset(4)] public float ThermalConductivity;
        [FieldOffset(8)] public float ConvectionVelocityY;
        [FieldOffset(12)] public uint Flags;
    }

    /// <summary>
    /// Packed heat producer. Sources are data, never trigger colliders.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct HeatSourceDTO
    {
        [FieldOffset(0)] public double3 Aup;
        [FieldOffset(24)] public float IntensityCelsiusPerSecond;
        [FieldOffset(28)] public float RadiusMeters;
        [FieldOffset(32)] public float FalloffExponent;
        [FieldOffset(36)] public uint ProfileHash;
        [FieldOffset(40)] public uint SourceId;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public float ConductivityOverride;
        [FieldOffset(52)] public float ConvectionGain;
        [FieldOffset(56)] public float Phase01;
        [FieldOffset(60)] public uint _pad0;
    }

    /// <summary>
    /// Vault-backed tuning block. Editor writes fields directly; Burst jobs read the copied value.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct ThermalGridTuningDTO
    {
        [FieldOffset(0)] public double3 GridOriginAup;
        [FieldOffset(24)] public float CellSizeMeters;
        [FieldOffset(28)] public float AmbientTemperatureCelsius;
        [FieldOffset(32)] public float WaterThermalConductivity;
        [FieldOffset(36)] public float ConvectionSpeed;
        [FieldOffset(40)] public float GlobalQualityWeight;
        [FieldOffset(44)] public int JacobiIterations;
        [FieldOffset(48)] public int3 GridResolution;
        [FieldOffset(60)] public int ActiveCellCount;
        [FieldOffset(64)] public float DissipationPerStep;
        [FieldOffset(68)] public float MaxStableTemperatureCelsius;
        [FieldOffset(72)] public float HullInsulationConductivity;
        [FieldOffset(76)] public float MockVolcanoIntensity;
        [FieldOffset(80)] public float MockVolcanoRadiusMeters;
        [FieldOffset(84)] public int MockVolcanoCount;
        [FieldOffset(88)] public float ShiftThresholdMeters;
        [FieldOffset(92)] public float ThermalDamageThresholdCelsius;
        [FieldOffset(96)] public uint Frame;
        [FieldOffset(100)] public uint StateHash;
        [FieldOffset(104)] public uint Flags;
        [FieldOffset(108)] public uint LastShiftSequence;
        [FieldOffset(112)] public float SubmarineHalfExtentX;
        [FieldOffset(116)] public float SubmarineHalfExtentY;
        [FieldOffset(120)] public float SubmarineHalfExtentZ;
        [FieldOffset(124)] public float _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ThermalSampleResultDTO
    {
        [FieldOffset(0)] public float TemperatureCelsius;
        [FieldOffset(4)] public float ConvectionVelocityY;
        [FieldOffset(8)] public uint CellIndex;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public float3 LocalGridPosition;
        [FieldOffset(28)] public float Conductivity;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct HeatSourceProfileDTO
    {
        [FieldOffset(0)] public uint NameHash;
        [FieldOffset(4)] public float IntensityCelsiusPerSecond;
        [FieldOffset(8)] public float RadiusMeters;
        [FieldOffset(12)] public float FalloffExponent;
        [FieldOffset(16)] public float ConvectionGain;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public uint _pad0;
        [FieldOffset(28)] public uint _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ThermalTelemetryEntry
    {
        [FieldOffset(0)] public float MaxTemperatureCelsius;
        [FieldOffset(4)] public float EnergyBefore;
        [FieldOffset(8)] public float EnergyAfter;
        [FieldOffset(12)] public float SolverMicroseconds;
        [FieldOffset(16)] public double3 GridOriginAup;
        [FieldOffset(40)] public uint Frame;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public uint ActiveSourceCount;
        [FieldOffset(52)] public uint JacobiIterations;
        [FieldOffset(56)] public uint NaNCellIndex;
        [FieldOffset(60)] public uint ActiveResolution;
    }

    public static class ThermalCellLayoutValidator
    {
        public static bool ValidateThermalCellLayout()
        {
            return UnsafeUtility.SizeOf<ThermalCellDTO>() == 16 &&
                   Marshal.OffsetOf<ThermalCellDTO>(nameof(ThermalCellDTO.TemperatureCelsius)).ToInt32() == 0 &&
                   Marshal.OffsetOf<ThermalCellDTO>(nameof(ThermalCellDTO.ThermalConductivity)).ToInt32() == 4 &&
                   Marshal.OffsetOf<ThermalCellDTO>(nameof(ThermalCellDTO.ConvectionVelocityY)).ToInt32() == 8 &&
                   Marshal.OffsetOf<ThermalCellDTO>(nameof(ThermalCellDTO.Flags)).ToInt32() == 12;
        }
    }
}
