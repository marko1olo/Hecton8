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
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct HazardSourceDTO
    {
        [FieldOffset(0)] public double3 AUP;
        [FieldOffset(24)] public float Intensity;
        [FieldOffset(28)] public float Radius;
        [FieldOffset(32)] public uint HazardTypeHash;
        [FieldOffset(36)] public uint _pad0;
        [FieldOffset(40)] private ulong _pad1;
        [FieldOffset(48)] private ulong _pad2;
        [FieldOffset(56)] private ulong _pad3;
    }

    /// <summary>
    /// Unmanaged constants edited by the thermodynamics tuner and read by Burst jobs.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ThermodynamicsHazardConstants
    {
        [FieldOffset(0)] public float BaseWaterTempCelsius;
        [FieldOffset(4)] public float HeatDiffusionRate;
        [FieldOffset(8)] public float RadiationDiffusionRate;
        [FieldOffset(12)] public float RadiationDecayCoefficient;
        [FieldOffset(16)] public float RockShieldingFactor;
        [FieldOffset(20)] public float VerticalHeatBias;
        [FieldOffset(24)] public float HeatDamageThresholdCelsius;
        [FieldOffset(28)] public float RadiationDamageThreshold;
    }

    /// <summary>
    /// Smooth local hazard sample returned by trilinear grid queries.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ThermodynamicsHazardSample
    {
        [FieldOffset(0)] public float TemperatureCelsius;
        [FieldOffset(4)] public float Radiation;
        [FieldOffset(8)] public float HeatDamage;
        [FieldOffset(12)] public float RadiationDamage;
        [FieldOffset(16)] public float3 LocalGridPosition;
        [FieldOffset(28)] public uint Flags;
    }

    /// <summary>
    /// Raw pointer surface for macro-grid buffers. Public readback exposes front pointers only;
    /// back pointers are owner-only and may be null.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public unsafe struct ThermodynamicsHazardGridPointers
    {
        [FieldOffset(0)] public float* TemperatureFront;
        [FieldOffset(8)] public float* TemperatureBack;
        [FieldOffset(16)] public float* RadiationFront;
        [FieldOffset(24)] public float* RadiationBack;
        [FieldOffset(32)] public int CellCount;
        [FieldOffset(36)] public int Resolution;
        [FieldOffset(40)] private ulong _pad0;
        [FieldOffset(48)] private ulong _pad1;
        [FieldOffset(56)] private ulong _pad2;
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
        [FieldOffset(41)] private byte _pad0;
        [FieldOffset(42)] private ushort _pad1;
        [FieldOffset(44)] private uint _pad2;
        [FieldOffset(48)] private ulong _pad3;
        [FieldOffset(56)] private ulong _pad4;
    }

    /// <summary>
    /// Local blind damage proof signal. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public partial struct ThermodynamicsMockDamageSignal : ISignal
    {
        [FieldOffset(0)] public double3 Aup;
        [FieldOffset(24)] public float3 Normal;
        [FieldOffset(36)] public float Damage;
        [FieldOffset(40)] public uint EntityId;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public ulong _pad0;
        [FieldOffset(56)] public ulong _pad1;
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
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ThermodynamicsHazardTelemetryEntry
    {
        [FieldOffset(0)] public float MaxGridTemperature;
        [FieldOffset(4)] public float MaxRadiationLevel;
        [FieldOffset(8)] public float DiffusionComputeTimeMs;
        [FieldOffset(12)] public float3 GridOrigin;
        [FieldOffset(24)] public uint Frame;
        [FieldOffset(28)] public uint GridVersion;
        [FieldOffset(32)] public uint SourceCount;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public uint ShiftSequence;
        [FieldOffset(44)] public uint NaNCellIndex;
        [FieldOffset(48)] public uint ActiveResolution;
        [FieldOffset(52)] public uint GridOriginHash;
        [FieldOffset(56)] public uint _pad0;
        [FieldOffset(60)] public uint _pad1;
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
        public const uint FlagPersistent = 1u << 0;
        public const uint FlagMock = 1u << 1;

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
        [FieldOffset(60)] public uint LastTouchedFrame;
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
        [FieldOffset(124)] public float SimulationTickDeltaSeconds;
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

    /// <summary>
    /// Pass-wide convergence control for abyssal heat diffusion. Size: 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct ThermalSolverConvergenceStateDTO
    {
        [FieldOffset(0)] public float MaxResidualFloat;
        [FieldOffset(4)] public float PreviousResidualFloat;
        [FieldOffset(8)] public float Omega;
        [FieldOffset(12)] public ushort IterationCount;
        [FieldOffset(14)] public ushort FaultFlags;
    }

    /// <summary>
    /// Cache-line isolated residual slot for per-worker map-reduce writes. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ThermalResidualSlot64
    {
        [FieldOffset(0)] public float MaxResidualFloat;
        [FieldOffset(4)] public uint FaultFlags;
        [FieldOffset(8)] private ulong _pad0;
        [FieldOffset(16)] private ulong _pad1;
        [FieldOffset(24)] private ulong _pad2;
        [FieldOffset(32)] private ulong _pad3;
        [FieldOffset(40)] private ulong _pad4;
        [FieldOffset(48)] private ulong _pad5;
        [FieldOffset(56)] private ulong _pad6;
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

        public static bool ValidateThermalSolverConvergenceLayout()
        {
            return UnsafeUtility.SizeOf<ThermalSolverConvergenceStateDTO>() == 16 &&
                   UnsafeUtility.SizeOf<ThermalResidualSlot64>() == 64 &&
                   Marshal.OffsetOf<ThermalSolverConvergenceStateDTO>(nameof(ThermalSolverConvergenceStateDTO.MaxResidualFloat)).ToInt32() == 0 &&
                   Marshal.OffsetOf<ThermalSolverConvergenceStateDTO>(nameof(ThermalSolverConvergenceStateDTO.PreviousResidualFloat)).ToInt32() == 4 &&
                   Marshal.OffsetOf<ThermalSolverConvergenceStateDTO>(nameof(ThermalSolverConvergenceStateDTO.Omega)).ToInt32() == 8 &&
                   Marshal.OffsetOf<ThermalSolverConvergenceStateDTO>(nameof(ThermalSolverConvergenceStateDTO.IterationCount)).ToInt32() == 12 &&
                   Marshal.OffsetOf<ThermalSolverConvergenceStateDTO>(nameof(ThermalSolverConvergenceStateDTO.FaultFlags)).ToInt32() == 14 &&
                   Marshal.OffsetOf<ThermalResidualSlot64>(nameof(ThermalResidualSlot64.MaxResidualFloat)).ToInt32() == 0 &&
                   Marshal.OffsetOf<ThermalResidualSlot64>(nameof(ThermalResidualSlot64.FaultFlags)).ToInt32() == 4;
        }
    }
}
