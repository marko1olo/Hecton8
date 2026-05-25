using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Signals;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts.Physics
{
    public static class SeaglideHydrodynamicsConstants
    {
        public const int StateCapacity = 1024;
        public const int RequestCapacity = 1024;
        public const int MockRequestCount = 1000;
        public const int FlowSampleCapacity = 64;
        public const int ForceQueueSoftCapacity = 1024;
        public const int TelemetryCapacity = 300;
        public const int TuningCapacity = 1;
        public const int CounterCapacity = 1;
        public const int CsvScratchBytes = 65536;
        public const int StateBytes = 64;
        public const int RequestBytes = 128;
        public const int RequestSignalBytes = 192;
        public const int ForcePacketBytes = 128;
        public const int ForceDtoBytes = ForcePacketBytes;
        public const int FlowSampleBytes = 64;
        public const int TuningBytes = 128;
        public const int CounterBytes = 64;
        public const int TelemetryBytes = 64;
        public const int BodyBindingBytes = 32;
        public const int VisualStateBytes = 64;
        public const int AudioSignalBytes = 64;
        public const int CavitationSignalBytes = 64;
        public const float Epsilon = 0.0001f;
        public const float DefaultWaterDensityKgPerM3 = 1025f;
        public const float DefaultMaxThrustN = 820f;
        public const float DefaultLinearDragCoefficient = 1.85f;
        public const float DefaultQuadraticDragCoefficient = 0.92f;
        public const float DefaultCrossSectionAreaM2 = 0.42f;
        public const float DefaultBaseMassKg = 82f;
        public const float DefaultAddedMassKg = 18f;
        public const float DefaultFlowForceCoefficient = 0.42f;
        public const float DefaultBatteryBaseDrainPerSecond = 0.018f;
        public const float DefaultBatteryLoadDrainPerNewton = 0.000015f;
        public const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_227.bin";
        public const string CsvRelativePath = "Data/Physics/seaglide_performance_profiles.csv";
        public const string LegacyCsvRelativePath = "Data/Physics/seaglide_vehicle_profiles.csv";
        public const uint PlayerBodyTargetHash = 0x504C5952u;
        public const uint SourceHash = 0x53323237u;

        public const uint FlagActive = 1u << 0;
        public const uint FlagPlayerControlled = 1u << 1;
        public const uint FlagEmergencyMock = 1u << 2;
        public const uint FlagForceQueued = 1u << 3;
        public const uint FlagMetabolismEvaluated = 1u << 4;
        public const uint FlagVisualOnly = 1u << 5;
        public const uint FlagRollbackExcluded = 1u << 6;
        public const uint FlagCavitationSignal = 1u << 7;
        public const uint FlagPacketOverflow = 1u << 8;
        public const uint FlagBudgetExceeded = 1u << 9;
        public const uint FlagTelemetryHeartbeat = 1u << 10;
        public const uint FlagCadenceSkipped = 1u << 11;
        public const uint FlagBodyBindingUnresolved = 1u << 12;
        public const uint FlagNonFinite = 1u << 31;
    }

    [StructLayout(LayoutKind.Explicit, Size = SeaglideHydrodynamicsConstants.RequestBytes)]
    public struct SeaglidePropulsionRequestDTO
    {
        [FieldOffset(0)] public double3 CurrentAUP;
        [FieldOffset(24)] public double3 PreviousAUP;
        [FieldOffset(48)] public float3 InputVector;
        [FieldOffset(60)] public float3 ForwardVector;
        [FieldOffset(72)] public float Throttle01;
        [FieldOffset(76)] public float DeltaTime;
        [FieldOffset(80)] public uint TargetEntityHash;
        [FieldOffset(84)] public uint RequestHash;
        [FieldOffset(88)] public uint Flags;
        [FieldOffset(92)] public uint FrameIndex;
        [FieldOffset(96)] public float BatteryLevel;
        [FieldOffset(100)] public float MaxThrustOverrideN;
        [FieldOffset(104)] public float3 SurfaceNormal;
        [FieldOffset(116)] public float CrossSectionAreaOverrideM2;
        [FieldOffset(120)] public float DragCoefficientOverride;
        [FieldOffset(124)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = SeaglideHydrodynamicsConstants.RequestSignalBytes)]
    public struct SeaglidePropulsionRequestSignal : ISignal
    {
        public const int ExpectedCapacity = 8;
        public const int MaxFrameSignals = 16;
        public const int LowTierFrameSignals = 4;
        public const uint LaneHash = 0xFED3F51Du;

        [FieldOffset(0)] public SeaglidePropulsionRequestDTO Request;
        [FieldOffset(128)] public float3 Velocity;
        [FieldOffset(140)] public float BatteryLevel;
        [FieldOffset(144)] public float MassKg;
        [FieldOffset(148)] public float AddedMassKg;
        [FieldOffset(152)] public uint TargetEntityHash;
        [FieldOffset(156)] public uint FrameIndex;
        [FieldOffset(160)] public uint Flags;
        [FieldOffset(164)] public uint _pad0;
        [FieldOffset(168)] public ulong _pad1;
        [FieldOffset(176)] public ulong _pad2;
        [FieldOffset(184)] public ulong _pad3;
    }
}
