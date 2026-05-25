// ============================================================================
// HECTON-8 - BaseAtmosphereLogisticsTypes.cs
// CSR gas logistics state for base-interior atmosphere diffusion.
// ============================================================================

using System.Runtime.InteropServices;
using Hecton8.Core.Memory;
using Hecton8.Core.Memory.Layout;
using Unity.Mathematics;

namespace Hecton8.Atmosphere
{
    internal static partial class AtmosphereLogisticsLayout
    {
        public const int CellStrideBytes = 32;
        public const int NodeStrideBytes = 32;
        public const int ConnectionStrideBytes = 16;
        public const int ConsumerStrideBytes = 64;
        public const int ToxicSourceStrideBytes = 64;
        public const int VentStrideBytes = 64;
        public const int TuningStrideBytes = 32;
        public const int TelemetryEntryStrideBytes = 64;
        public const int GraphCountersStrideBytes = 32;
        public const int GasRemainderStrideBytes = 16;
        public const int DeltaLaneStrideBytes = 64;
        public const int ShaderPayloadStrideBytes = 16;
        public const int GasProfileStrideBytes = 32;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = AtmosphereLogisticsLayout.CellStrideBytes)]
    public struct AtmosphereCellDTO
    {
        [FieldOffset(0)] public uint NodeHash;
        [FieldOffset(4)] public float Oxygen01;
        [FieldOffset(8)] public float CarbonDioxide01;
        [FieldOffset(12)] public float Nitrogen01;
        [FieldOffset(16)] public float Toxin01;
        [FieldOffset(20)] public float Temperature;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint _pad0;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = AtmosphereLogisticsLayout.NodeStrideBytes)]
    public struct AtmosphereNodeDTO
    {
        [FieldOffset(0)] public double3 Aup;
        [FieldOffset(24)] public uint NodeHash;
        [FieldOffset(28)] public uint Flags;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = AtmosphereLogisticsLayout.ConnectionStrideBytes)]
    public struct AtmosphereConnectionDTO
    {
        [FieldOffset(0)] public int FromNode;
        [FieldOffset(4)] public int ToNode;
        [FieldOffset(8)] public float Conductance;
        [FieldOffset(12)] public uint Flags;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = AtmosphereLogisticsLayout.ConsumerStrideBytes)]
    public struct AtmosphereConsumerDTO
    {
        [FieldOffset(0)] public double3 Aup;
        [FieldOffset(24)] public float OxygenPerSecond01;
        [FieldOffset(28)] public float CarbonDioxidePerSecond01;
        [FieldOffset(32)] public float RadiusMeters;
        [FieldOffset(36)] public float HeatPerSecond;
        [FieldOffset(40)] public uint EntityHash;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public uint LastNodeHash;
        [FieldOffset(52)] public int LastNodeIndex;
        [FieldOffset(56)] public uint _pad0;
        [FieldOffset(60)] public uint _pad1;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = AtmosphereLogisticsLayout.ToxicSourceStrideBytes)]
    public struct AtmosphereToxicSourceDTO
    {
        [FieldOffset(0)] public double3 Aup;
        [FieldOffset(24)] public float ToxinPerSecond01;
        [FieldOffset(28)] public float CarbonDioxidePerSecond01;
        [FieldOffset(32)] public float OxygenDrainPerSecond01;
        [FieldOffset(36)] public float HeatPerSecond;
        [FieldOffset(40)] public uint SourceHash;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public int LastNodeIndex;
        [FieldOffset(52)] public float RadiusMeters;
        [FieldOffset(56)] public uint _pad0;
        [FieldOffset(60)] public uint _pad1;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = AtmosphereLogisticsLayout.VentStrideBytes)]
    public struct AtmosphereVentDTO
    {
        [FieldOffset(0)] public double3 Aup;
        [FieldOffset(24)] public float RadiusMeters;
        [FieldOffset(28)] public float LeakOxygenPerSecond01;
        [FieldOffset(32)] public float LeakNitrogenPerSecond01;
        [FieldOffset(36)] public float ToxinIngressPerSecond01;
        [FieldOffset(40)] public uint VentHash;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public int LastNodeIndex;
        [FieldOffset(52)] public float LastDistanceMeters;
        [FieldOffset(56)] public uint _pad0;
        [FieldOffset(60)] public uint _pad1;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = AtmosphereLogisticsLayout.TuningStrideBytes)]
    public struct AtmosphereTuningDTO
    {
        [FieldOffset(0)] public float BaseDiffusionRate;
        [FieldOffset(4)] public float InhalationMultiplier;
        [FieldOffset(8)] public float ToxinDissipationSpeed;
        [FieldOffset(12)] public float GlobalQualityWeight;
        [FieldOffset(16)] public float CellSizeMeters;
        [FieldOffset(20)] public float AmbientTemperatureCelsius;
        [FieldOffset(24)] public float LeakDrainMultiplier;
        [FieldOffset(28)] public uint Flags;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = AtmosphereLogisticsLayout.TelemetryEntryStrideBytes)]
    public struct AtmosphereTelemetryEntry
    {
        [FieldOffset(0)] public ulong StateHash;
        [FieldOffset(8)] public float AverageOxygen01;
        [FieldOffset(12)] public float MaxCarbonDioxide01;
        [FieldOffset(16)] public float AverageNitrogen01;
        [FieldOffset(20)] public float MaxToxin01;
        [FieldOffset(24)] public float AverageTemperature;
        [FieldOffset(28)] public int FrameIndex;
        [FieldOffset(32)] public int NodeCount;
        [FieldOffset(36)] public int EdgeCount;
        [FieldOffset(40)] public int ConsumerCount;
        [FieldOffset(44)] public int SourceCount;
        [FieldOffset(48)] public int SolverMicros;
        [FieldOffset(52)] public int JacobiIterations;
        [FieldOffset(56)] public uint FaultFlags;
        [FieldOffset(60)] public uint TotalGasUnits;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = AtmosphereLogisticsLayout.GraphCountersStrideBytes)]
    public struct AtmosphereGraphCountersDTO
    {
        [FieldOffset(0)] public int NodeCount;
        [FieldOffset(4)] public int ConnectionCount;
        [FieldOffset(8)] public int CsrEdgeCount;
        [FieldOffset(12)] public int ConsumerCount;
        [FieldOffset(16)] public int SourceCount;
        [FieldOffset(20)] public int VentCount;
        [FieldOffset(24)] public int TelemetryCursor;
        [FieldOffset(28)] public uint Flags;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = AtmosphereLogisticsLayout.GasRemainderStrideBytes)]
    public struct AtmosphereGasRemainderDTO
    {
        [FieldOffset(0)] public float Oxygen;
        [FieldOffset(4)] public float CarbonDioxide;
        [FieldOffset(8)] public float Nitrogen;
        [FieldOffset(12)] public float Toxin;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = AtmosphereLogisticsLayout.DeltaLaneStrideBytes)]
    public struct AtmosphereDeltaLane64
    {
        [FieldOffset(0)] public int Units;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public ulong _pad0;
        [FieldOffset(16)] public ulong _pad1;
        [FieldOffset(24)] public ulong _pad2;
        [FieldOffset(32)] public ulong _pad3;
        [FieldOffset(40)] public ulong _pad4;
        [FieldOffset(48)] public ulong _pad5;
        [FieldOffset(56)] public ulong _pad6;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = AtmosphereLogisticsLayout.ShaderPayloadStrideBytes)]
    public struct AtmosphereShaderPayloadDTO
    {
        [FieldOffset(0)] public float Oxygen01;
        [FieldOffset(4)] public float CarbonDioxide01;
        [FieldOffset(8)] public float Toxin01;
        [FieldOffset(12)] public float Flow01;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = AtmosphereLogisticsLayout.GasProfileStrideBytes)]
    public struct AtmosphereGasProfileDTO
    {
        [FieldOffset(0)] public uint ProfileHash;
        [FieldOffset(4)] public float Oxygen01;
        [FieldOffset(8)] public float CarbonDioxide01;
        [FieldOffset(12)] public float Nitrogen01;
        [FieldOffset(16)] public float Toxin01;
        [FieldOffset(20)] public float Temperature;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint _pad0;
    }

    public static class AtmosphereLogisticsConstants
    {
        public const int MaxMockNodes = 1000;
        public const int MaxMockConnections = 2500;
        public const int MaxCsrEdges = MaxMockConnections * 2;
        public const int MaxConsumers = 128;
        public const int MaxToxicSources = 128;
        public const int MaxVents = 64;
        public const int TelemetryRingCapacity = 300;
        public const int CsvScratchBytes = 16384;
        public const int MaxProfiles = 64;
        public const int GasUnitScale = 1000000;
        public const float DefaultOxygen01 = 0.2095f;
        public const float DefaultCarbonDioxide01 = 0.00042f;
        public const float DefaultNitrogen01 = 0.79008f;
        public const float DefaultTemperatureCelsius = 20f;
        public const float MinimumCellSizeMeters = 0.25f;
        public const float MaximumCellSizeMeters = 12f;
        public const uint GraphInitializedFlag = 1u << 16;
        public const uint GraphDirtyFlag = 1u << 17;
        public const uint DumpRequestedFlag = 1u << 18;
        public const uint MockTopologyFlag = 1u << 19;
        public const uint RuntimeFaultFlag = 1u << 31;
    }

    public static class AtmosphereLogisticsBufferIds
    {
        public const uint CellsFrontValue = 71500u;
        public const uint CellsBackValue = 71501u;
        public const BufferID CellsFront = BufferID.AtmosphereLogisticsCellsFront;
        public const BufferID CellsBack = BufferID.AtmosphereLogisticsCellsBack;
        public const BufferID Nodes = BufferID.AtmosphereLogisticsNodes;
        public const BufferID Connections = BufferID.AtmosphereLogisticsConnections;
        public const BufferID EdgeOffsets = BufferID.AtmosphereLogisticsEdgeOffsets;
        public const BufferID EdgeDestinations = BufferID.AtmosphereLogisticsEdgeDestinations;
        public const BufferID EdgeConductance = BufferID.AtmosphereLogisticsEdgeConductance;
        public const BufferID EdgeWriteCursor = BufferID.AtmosphereLogisticsEdgeWriteCursor;
        public const BufferID Consumers = BufferID.AtmosphereLogisticsConsumers;
        public const BufferID ToxicSources = BufferID.AtmosphereLogisticsToxicSources;
        public const BufferID Vents = BufferID.AtmosphereLogisticsVents;
        public const BufferID Counters = BufferID.AtmosphereLogisticsCounters;
        public const BufferID Tuning = BufferID.AtmosphereLogisticsTuning;
        public const BufferID TelemetryRing = BufferID.AtmosphereLogisticsTelemetryRing;
        public const BufferID OxygenDeltaUnits = BufferID.AtmosphereLogisticsOxygenDeltaUnits;
        public const BufferID CarbonDioxideDeltaUnits = BufferID.AtmosphereLogisticsCarbonDioxideDeltaUnits;
        public const BufferID NitrogenDeltaUnits = BufferID.AtmosphereLogisticsNitrogenDeltaUnits;
        public const BufferID ToxinDeltaUnits = BufferID.AtmosphereLogisticsToxinDeltaUnits;
        public const BufferID TemperatureDeltaMilli = BufferID.AtmosphereLogisticsTemperatureDeltaMilli;
        public const BufferID GasRemainders = BufferID.AtmosphereLogisticsGasRemainders;
        public const BufferID ShaderPayload = BufferID.AtmosphereLogisticsShaderPayload;
        public const BufferID CsvScratch = BufferID.AtmosphereLogisticsCsvScratch;
        public const BufferID Profiles = BufferID.AtmosphereLogisticsProfiles;
    }

    public static class AtmosphereCellFlags
    {
        public const uint Walkable = 1u << 0;
        public const uint Sealed = 1u << 1;
        public const uint Vent = 1u << 2;
        public const uint Breached = 1u << 3;
        public const uint ReactorLeak = 1u << 4;
        public const uint Fault = 1u << 31;
    }

    public static class AtmosphereFaultFlags
    {
        public const uint None = 0u;
        public const uint LayoutFault = 1u << 0;
        public const uint EmptyGraph = 1u << 1;
        public const uint NonFiniteGas = 1u << 2;
        public const uint BufferAlias = 1u << 3;
        public const uint CsrOverflow = 1u << 4;
        public const uint SourceOverflow = 1u << 5;
        public const uint CsvMalformed = 1u << 6;
        public const uint NaNDetected = 1u << 7;
    }
}
