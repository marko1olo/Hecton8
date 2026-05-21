using System;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Physics
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

    public static class SeaglideHydrodynamicsBufferIds
    {
        public const BufferID States = BufferID.ShinobuSeaglideStates;
        public const BufferID Requests = BufferID.ShinobuSeaglideRequests;
        public const BufferID ForcePackets = BufferID.ShinobuSeaglideForcePackets;
        public const BufferID FlowSamples = BufferID.ShinobuSeaglideFlowSamples;
        public const BufferID Tuning = BufferID.ShinobuSeaglideTuning;
        public const BufferID TelemetryRing = BufferID.ShinobuSeaglideTelemetryRing;
        public const BufferID TelemetryCursor = BufferID.ShinobuSeaglideTelemetryCursor;
        public const BufferID Counters = BufferID.ShinobuSeaglideCounters;
        public const BufferID BodyBindings = BufferID.ShinobuSeaglideBodyBindings;
        public const BufferID VisualStates = BufferID.ShinobuSeaglideVisualStates;
        public const BufferID AudioSignals = BufferID.ShinobuSeaglideAudioSignals;
        public const BufferID CavitationSignals = BufferID.ShinobuSeaglideCavitationSignals;
        public const BufferID CsvScratch = BufferID.ShinobuSeaglideCsvScratch;
    }

    [StructLayout(LayoutKind.Explicit, Size = SeaglideHydrodynamicsConstants.StateBytes)]
    public struct SeaglideStateDTO
    {
        [FieldOffset(0)] public double3 CurrentAUP;
        [FieldOffset(24)] public float3 Velocity;
        [FieldOffset(36)] public float BatteryLevel;
        [FieldOffset(40)] public uint ActiveFlags;
        [FieldOffset(44)] public uint TargetEntityHash;
        [FieldOffset(48)] public float MassKg;
        [FieldOffset(52)] public float AddedMassKg;
        [FieldOffset(56)] public uint FrameIndex;
        [FieldOffset(60)] public uint _pad0;
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

    [StructLayout(LayoutKind.Explicit, Size = SeaglideHydrodynamicsConstants.ForceDtoBytes)]
    public struct SeaglideForcePacketDTO
    {
        [FieldOffset(0)] public double3 CurrentAUP;
        [FieldOffset(24)] public float3 NetForce;
        [FieldOffset(36)] public float3 ThrustForce;
        [FieldOffset(48)] public float3 DragForce;
        [FieldOffset(60)] public float3 FlowForce;
        [FieldOffset(72)] public float3 RelativeVelocity;
        [FieldOffset(84)] public uint TargetEntityHash;
        [FieldOffset(88)] public uint Flags;
        [FieldOffset(92)] public int StateIndex;
        [FieldOffset(96)] public uint FrameIndex;
        [FieldOffset(100)] public float ForceMagnitude;
        [FieldOffset(104)] public float BatteryLevel;
        [FieldOffset(108)] public float MassKg;
        [FieldOffset(112)] public float AddedMassKg;
        [FieldOffset(116)] public float Throttle01;
        [FieldOffset(120)] public float CurrentSpeed;
        [FieldOffset(124)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = SeaglideHydrodynamicsConstants.FlowSampleBytes)]
    public struct SeaglideFlowSampleDTO
    {
        [FieldOffset(0)] public double3 SampleAUP;
        [FieldOffset(24)] public float3 FlowVelocity;
        [FieldOffset(36)] public float CellSizeMeters;
        [FieldOffset(40)] public uint CellHash;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public ulong _pad0;
        [FieldOffset(56)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = SeaglideHydrodynamicsConstants.TuningBytes)]
    public struct SeaglideTuningDTO
    {
        [FieldOffset(0)] public double3 SectorAUP;
        [FieldOffset(24)] public float WaterDensityKgPerM3;
        [FieldOffset(28)] public float MaxThrustN;
        [FieldOffset(32)] public float LinearDragCoefficient;
        [FieldOffset(36)] public float QuadraticDragCoefficient;
        [FieldOffset(40)] public float CrossSectionAreaM2;
        [FieldOffset(44)] public float AddedMassKg;
        [FieldOffset(48)] public float BaseMassKg;
        [FieldOffset(52)] public float FlowForceCoefficient;
        [FieldOffset(56)] public float BatteryBaseDrainPerSecond;
        [FieldOffset(60)] public float BatteryLoadDrainPerNewton;
        [FieldOffset(64)] public float BatteryCadenceSeconds;
        [FieldOffset(68)] public float AudioDopplerScale;
        [FieldOffset(72)] public float CavitationSpeedStart;
        [FieldOffset(76)] public float CavitationSpeedFull;
        [FieldOffset(80)] public float GlobalQualityWeight;
        [FieldOffset(84)] public float SimulationTickDelta;
        [FieldOffset(88)] public int ActiveRequestCount;
        [FieldOffset(92)] public int MockRequestCount;
        [FieldOffset(96)] public uint FrameIndex;
        [FieldOffset(100)] public uint Flags;
        [FieldOffset(104)] public int CurrentGridResolution;
        [FieldOffset(108)] public int FlowSampleCount;
        [FieldOffset(112)] public float MinimumCadenceSeconds;
        [FieldOffset(116)] public float MaximumCadenceSeconds;
        [FieldOffset(120)] public uint ProfileHash;
        [FieldOffset(124)] public float ResolvedQualityWeight;

        public static SeaglideTuningDTO Default()
        {
            SeaglideTuningDTO value = default;
            value.SectorAUP = double3.zero;
            value.WaterDensityKgPerM3 = SeaglideHydrodynamicsConstants.DefaultWaterDensityKgPerM3;
            value.MaxThrustN = SeaglideHydrodynamicsConstants.DefaultMaxThrustN;
            value.LinearDragCoefficient = SeaglideHydrodynamicsConstants.DefaultLinearDragCoefficient;
            value.QuadraticDragCoefficient = SeaglideHydrodynamicsConstants.DefaultQuadraticDragCoefficient;
            value.CrossSectionAreaM2 = SeaglideHydrodynamicsConstants.DefaultCrossSectionAreaM2;
            value.AddedMassKg = SeaglideHydrodynamicsConstants.DefaultAddedMassKg;
            value.BaseMassKg = SeaglideHydrodynamicsConstants.DefaultBaseMassKg;
            value.FlowForceCoefficient = SeaglideHydrodynamicsConstants.DefaultFlowForceCoefficient;
            value.BatteryBaseDrainPerSecond = SeaglideHydrodynamicsConstants.DefaultBatteryBaseDrainPerSecond;
            value.BatteryLoadDrainPerNewton = SeaglideHydrodynamicsConstants.DefaultBatteryLoadDrainPerNewton;
            value.BatteryCadenceSeconds = 0.2f;
            value.AudioDopplerScale = 1f;
            value.CavitationSpeedStart = 7.5f;
            value.CavitationSpeedFull = 14f;
            value.GlobalQualityWeight = 1f;
            value.SimulationTickDelta = 0.02f;
            value.ActiveRequestCount = 0;
            value.MockRequestCount = SeaglideHydrodynamicsConstants.MockRequestCount;
            value.CurrentGridResolution = 2;
            value.FlowSampleCount = 0;
            value.MinimumCadenceSeconds = 0.02f;
            value.MaximumCadenceSeconds = 0.12f;
            value.ProfileHash = SeaglideHydrodynamicsConstants.SourceHash;
            value.ResolvedQualityWeight = 1f;
            return value;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = SeaglideHydrodynamicsConstants.CounterBytes)]
    public struct SeaglideCounterDTO
    {
        [FieldOffset(0)] public int EvaluatedRequests;
        [FieldOffset(4)] public int ForcePackets;
        [FieldOffset(8)] public int NonFiniteCount;
        [FieldOffset(12)] public int MetabolismTicks;
        [FieldOffset(16)] public float TotalThrustForce;
        [FieldOffset(20)] public float TotalDragForce;
        [FieldOffset(24)] public float TotalFlowForce;
        [FieldOffset(28)] public float MaxForceMagnitude;
        [FieldOffset(32)] public float ComputeMicros;
        [FieldOffset(36)] public float GlobalQualityWeight;
        [FieldOffset(40)] public uint Flags;
        [FieldOffset(44)] public uint LastTargetEntityHash;
        [FieldOffset(48)] public ulong _pad0;
        [FieldOffset(56)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = SeaglideHydrodynamicsConstants.TelemetryBytes)]
    public struct SeaglideTelemetryEntry
    {
        [FieldOffset(0)] public ulong FrameAndRequestCountPacked;
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public int EvaluatedRequests;
        [FieldOffset(8)] public int ForcePackets;
        [FieldOffset(12)] public int NonFiniteCount;
        [FieldOffset(16)] public float TotalThrustForce;
        [FieldOffset(20)] public float TotalDragForce;
        [FieldOffset(24)] public float TotalFlowForce;
        [FieldOffset(28)] public float MaxForceMagnitude;
        [FieldOffset(32)] public float ComputeMicros;
        [FieldOffset(36)] public float GlobalQualityWeight;
        [FieldOffset(40)] public uint Flags;
        [FieldOffset(44)] public uint LastTargetEntityHash;
        [FieldOffset(48)] public float3 LastFlowForce;
        [FieldOffset(60)] public float LastBatteryLevel;
    }

    [StructLayout(LayoutKind.Explicit, Size = SeaglideHydrodynamicsConstants.BodyBindingBytes)]
    public struct SeaglideBodyBindingDTO
    {
        [FieldOffset(0)] public uint TargetEntityHash;
        [FieldOffset(4)] public int StateIndex;
        [FieldOffset(8)] public int RigidbodyIndex;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public ulong _pad0;
        [FieldOffset(24)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = SeaglideHydrodynamicsConstants.VisualStateBytes)]
    public struct SeaglideVisualStateDTO
    {
        [FieldOffset(0)] public double3 CurrentAUP;
        [FieldOffset(24)] public float3 WakeDirection;
        [FieldOffset(36)] public float WakeIntensity01;
        [FieldOffset(40)] public float Cavitation01;
        [FieldOffset(44)] public float BrakeCloud01;
        [FieldOffset(48)] public uint SourceHash;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = SeaglideHydrodynamicsConstants.AudioSignalBytes)]
    public struct SeaglideAudioSignalDTO
    {
        [FieldOffset(0)] public double3 CurrentAUP;
        [FieldOffset(24)] public float DopplerSpeedMetersPerSecond;
        [FieldOffset(28)] public float PitchScalar;
        [FieldOffset(32)] public float VolumeScalar;
        [FieldOffset(36)] public float Cavitation01;
        [FieldOffset(40)] public uint SourceHash;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public uint TargetEntityHash;
        [FieldOffset(52)] public uint FrameIndex;
        [FieldOffset(56)] public ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = SeaglideHydrodynamicsConstants.CavitationSignalBytes)]
    public struct SeaglideCavitationVfxSignalDTO
    {
        [FieldOffset(0)] public double3 CurrentAUP;
        [FieldOffset(24)] public float3 Direction;
        [FieldOffset(36)] public float Intensity01;
        [FieldOffset(40)] public float RadiusMeters;
        [FieldOffset(44)] public uint SourceHash;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public uint FrameIndex;
        [FieldOffset(56)] public ulong _pad0;
    }

    public static class SeaglideHydrodynamicsLayout
    {
        private static readonly bool s_validateOnce = ValidateInternal();

        public static bool Validate()
        {
            return s_validateOnce;
        }

        private static bool ValidateInternal()
        {
            return UnsafeUtility.SizeOf<SeaglideStateDTO>() == SeaglideHydrodynamicsConstants.StateBytes &&
                   UnsafeUtility.SizeOf<SeaglidePropulsionRequestDTO>() == SeaglideHydrodynamicsConstants.RequestBytes &&
                   UnsafeUtility.SizeOf<SeaglidePropulsionRequestSignal>() == SeaglideHydrodynamicsConstants.RequestSignalBytes &&
                   UnsafeUtility.SizeOf<SeaglideForcePacketDTO>() == SeaglideHydrodynamicsConstants.ForcePacketBytes &&
                   UnsafeUtility.SizeOf<SeaglideFlowSampleDTO>() == SeaglideHydrodynamicsConstants.FlowSampleBytes &&
                   UnsafeUtility.SizeOf<SeaglideTuningDTO>() == SeaglideHydrodynamicsConstants.TuningBytes &&
                   UnsafeUtility.SizeOf<SeaglideCounterDTO>() == SeaglideHydrodynamicsConstants.CounterBytes &&
                   UnsafeUtility.SizeOf<SeaglideTelemetryEntry>() == SeaglideHydrodynamicsConstants.TelemetryBytes &&
                   UnsafeUtility.SizeOf<SeaglideBodyBindingDTO>() == SeaglideHydrodynamicsConstants.BodyBindingBytes &&
                   UnsafeUtility.SizeOf<SeaglideVisualStateDTO>() == SeaglideHydrodynamicsConstants.VisualStateBytes &&
                   UnsafeUtility.SizeOf<SeaglideAudioSignalDTO>() == SeaglideHydrodynamicsConstants.AudioSignalBytes &&
                   UnsafeUtility.SizeOf<SeaglideCavitationVfxSignalDTO>() == SeaglideHydrodynamicsConstants.CavitationSignalBytes &&
                   UnsafeUtility.AlignOf<SeaglideStateDTO>() == 8 &&
                   UnsafeUtility.AlignOf<SeaglidePropulsionRequestDTO>() == 8 &&
                   UnsafeUtility.AlignOf<SeaglidePropulsionRequestSignal>() == 8 &&
                   UnsafeUtility.AlignOf<SeaglideForcePacketDTO>() == 8 &&
                   UnsafeUtility.AlignOf<SeaglideFlowSampleDTO>() == 8 &&
                   UnsafeUtility.AlignOf<SeaglideTuningDTO>() == 8 &&
                   UnsafeUtility.AlignOf<SeaglideCounterDTO>() == 8 &&
                   UnsafeUtility.AlignOf<SeaglideTelemetryEntry>() == 8 &&
                   UnsafeUtility.AlignOf<SeaglideBodyBindingDTO>() == 8 &&
                   UnsafeUtility.AlignOf<SeaglideVisualStateDTO>() == 8 &&
                   UnsafeUtility.AlignOf<SeaglideAudioSignalDTO>() == 8 &&
                   UnsafeUtility.AlignOf<SeaglideCavitationVfxSignalDTO>() == 8 &&
                   OffsetOf<SeaglideStateDTO>(nameof(SeaglideStateDTO.CurrentAUP)) == 0 &&
                   OffsetOf<SeaglideStateDTO>(nameof(SeaglideStateDTO.Velocity)) == 24 &&
                   OffsetOf<SeaglideStateDTO>(nameof(SeaglideStateDTO.BatteryLevel)) == 36 &&
                   OffsetOf<SeaglideStateDTO>(nameof(SeaglideStateDTO.ActiveFlags)) == 40 &&
                   OffsetOf<SeaglidePropulsionRequestDTO>(nameof(SeaglidePropulsionRequestDTO.CurrentAUP)) == 0 &&
                   OffsetOf<SeaglidePropulsionRequestDTO>(nameof(SeaglidePropulsionRequestDTO.PreviousAUP)) == 24 &&
                   OffsetOf<SeaglidePropulsionRequestDTO>(nameof(SeaglidePropulsionRequestDTO.InputVector)) == 48 &&
                   OffsetOf<SeaglidePropulsionRequestDTO>(nameof(SeaglidePropulsionRequestDTO.ForwardVector)) == 60 &&
                   OffsetOf<SeaglidePropulsionRequestDTO>(nameof(SeaglidePropulsionRequestDTO.Throttle01)) == 72 &&
                   OffsetOf<SeaglidePropulsionRequestDTO>(nameof(SeaglidePropulsionRequestDTO.TargetEntityHash)) == 80 &&
                   OffsetOf<SeaglidePropulsionRequestDTO>(nameof(SeaglidePropulsionRequestDTO.SurfaceNormal)) == 104 &&
                   OffsetOf<SeaglidePropulsionRequestDTO>(nameof(SeaglidePropulsionRequestDTO._pad0)) == 124 &&
                   OffsetOf<SeaglidePropulsionRequestSignal>(nameof(SeaglidePropulsionRequestSignal.Request)) == 0 &&
                   OffsetOf<SeaglidePropulsionRequestSignal>(nameof(SeaglidePropulsionRequestSignal.Velocity)) == 128 &&
                   OffsetOf<SeaglidePropulsionRequestSignal>(nameof(SeaglidePropulsionRequestSignal.TargetEntityHash)) == 152 &&
                   OffsetOf<SeaglideForcePacketDTO>(nameof(SeaglideForcePacketDTO.TargetEntityHash)) == 84 &&
                   OffsetOf<SeaglideForcePacketDTO>(nameof(SeaglideForcePacketDTO.NetForce)) == 24 &&
                   OffsetOfTelemetry(nameof(SeaglideTelemetryEntry.LastFlowForce)) == 48 &&
                   OffsetOfTelemetry(nameof(SeaglideTelemetryEntry.LastBatteryLevel)) == 60 &&
                   OffsetOf<SeaglideVisualStateDTO>(nameof(SeaglideVisualStateDTO.Flags)) == 52 &&
                   OffsetOf<SeaglideAudioSignalDTO>(nameof(SeaglideAudioSignalDTO.DopplerSpeedMetersPerSecond)) == 24 &&
                   OffsetOf<SeaglideAudioSignalDTO>(nameof(SeaglideAudioSignalDTO.TargetEntityHash)) == 48 &&
                   OffsetOf<SeaglideAudioSignalDTO>(nameof(SeaglideAudioSignalDTO.FrameIndex)) == 52;
        }

        private static int OffsetOf<T>(string fieldName) where T : struct
        {
            Type type = typeof(T);
            if (type == typeof(SeaglideStateDTO))
                return OffsetOfState(fieldName);
            if (type == typeof(SeaglidePropulsionRequestDTO))
                return OffsetOfRequest(fieldName);
            if (type == typeof(SeaglidePropulsionRequestSignal))
                return OffsetOfRequestSignal(fieldName);
            if (type == typeof(SeaglideForcePacketDTO))
                return OffsetOfForcePacket(fieldName);
            if (type == typeof(SeaglideVisualStateDTO))
                return OffsetOfVisualState(fieldName);
            if (type == typeof(SeaglideAudioSignalDTO))
                return OffsetOfAudioSignal(fieldName);

            return -1;
        }

        private static int OffsetOfRequest(string fieldName)
        {
            if (fieldName == nameof(SeaglidePropulsionRequestDTO.CurrentAUP)) return 0;
            if (fieldName == nameof(SeaglidePropulsionRequestDTO.PreviousAUP)) return 24;
            if (fieldName == nameof(SeaglidePropulsionRequestDTO.InputVector)) return 48;
            if (fieldName == nameof(SeaglidePropulsionRequestDTO.ForwardVector)) return 60;
            if (fieldName == nameof(SeaglidePropulsionRequestDTO.Throttle01)) return 72;
            if (fieldName == nameof(SeaglidePropulsionRequestDTO.DeltaTime)) return 76;
            if (fieldName == nameof(SeaglidePropulsionRequestDTO.TargetEntityHash)) return 80;
            if (fieldName == nameof(SeaglidePropulsionRequestDTO.RequestHash)) return 84;
            if (fieldName == nameof(SeaglidePropulsionRequestDTO.Flags)) return 88;
            if (fieldName == nameof(SeaglidePropulsionRequestDTO.FrameIndex)) return 92;
            if (fieldName == nameof(SeaglidePropulsionRequestDTO.BatteryLevel)) return 96;
            if (fieldName == nameof(SeaglidePropulsionRequestDTO.MaxThrustOverrideN)) return 100;
            if (fieldName == nameof(SeaglidePropulsionRequestDTO.SurfaceNormal)) return 104;
            if (fieldName == nameof(SeaglidePropulsionRequestDTO.CrossSectionAreaOverrideM2)) return 116;
            if (fieldName == nameof(SeaglidePropulsionRequestDTO.DragCoefficientOverride)) return 120;
            if (fieldName == nameof(SeaglidePropulsionRequestDTO._pad0)) return 124;
            return -1;
        }

        private static int OffsetOfRequestSignal(string fieldName)
        {
            if (fieldName == nameof(SeaglidePropulsionRequestSignal.Request)) return 0;
            if (fieldName == nameof(SeaglidePropulsionRequestSignal.Velocity)) return 128;
            if (fieldName == nameof(SeaglidePropulsionRequestSignal.BatteryLevel)) return 140;
            if (fieldName == nameof(SeaglidePropulsionRequestSignal.MassKg)) return 144;
            if (fieldName == nameof(SeaglidePropulsionRequestSignal.AddedMassKg)) return 148;
            if (fieldName == nameof(SeaglidePropulsionRequestSignal.TargetEntityHash)) return 152;
            if (fieldName == nameof(SeaglidePropulsionRequestSignal.FrameIndex)) return 156;
            if (fieldName == nameof(SeaglidePropulsionRequestSignal.Flags)) return 160;
            if (fieldName == nameof(SeaglidePropulsionRequestSignal._pad0)) return 164;
            if (fieldName == nameof(SeaglidePropulsionRequestSignal._pad1)) return 168;
            if (fieldName == nameof(SeaglidePropulsionRequestSignal._pad2)) return 176;
            if (fieldName == nameof(SeaglidePropulsionRequestSignal._pad3)) return 184;
            return -1;
        }

        private static int OffsetOfState(string fieldName)
        {
            if (fieldName == nameof(SeaglideStateDTO.CurrentAUP)) return 0;
            if (fieldName == nameof(SeaglideStateDTO.Velocity)) return 24;
            if (fieldName == nameof(SeaglideStateDTO.BatteryLevel)) return 36;
            if (fieldName == nameof(SeaglideStateDTO.ActiveFlags)) return 40;
            if (fieldName == nameof(SeaglideStateDTO.TargetEntityHash)) return 44;
            if (fieldName == nameof(SeaglideStateDTO.MassKg)) return 48;
            if (fieldName == nameof(SeaglideStateDTO.AddedMassKg)) return 52;
            if (fieldName == nameof(SeaglideStateDTO.FrameIndex)) return 56;
            if (fieldName == nameof(SeaglideStateDTO._pad0)) return 60;
            return -1;
        }

        private static int OffsetOfForcePacket(string fieldName)
        {
            if (fieldName == nameof(SeaglideForcePacketDTO.CurrentAUP)) return 0;
            if (fieldName == nameof(SeaglideForcePacketDTO.NetForce)) return 24;
            if (fieldName == nameof(SeaglideForcePacketDTO.ThrustForce)) return 36;
            if (fieldName == nameof(SeaglideForcePacketDTO.DragForce)) return 48;
            if (fieldName == nameof(SeaglideForcePacketDTO.FlowForce)) return 60;
            if (fieldName == nameof(SeaglideForcePacketDTO.RelativeVelocity)) return 72;
            if (fieldName == nameof(SeaglideForcePacketDTO.TargetEntityHash)) return 84;
            if (fieldName == nameof(SeaglideForcePacketDTO.Flags)) return 88;
            if (fieldName == nameof(SeaglideForcePacketDTO.StateIndex)) return 92;
            if (fieldName == nameof(SeaglideForcePacketDTO.FrameIndex)) return 96;
            if (fieldName == nameof(SeaglideForcePacketDTO.ForceMagnitude)) return 100;
            if (fieldName == nameof(SeaglideForcePacketDTO.BatteryLevel)) return 104;
            if (fieldName == nameof(SeaglideForcePacketDTO.MassKg)) return 108;
            if (fieldName == nameof(SeaglideForcePacketDTO.AddedMassKg)) return 112;
            if (fieldName == nameof(SeaglideForcePacketDTO.Throttle01)) return 116;
            if (fieldName == nameof(SeaglideForcePacketDTO.CurrentSpeed)) return 120;
            if (fieldName == nameof(SeaglideForcePacketDTO._pad0)) return 124;
            return -1;
        }

        private static int OffsetOfVisualState(string fieldName)
        {
            if (fieldName == nameof(SeaglideVisualStateDTO.CurrentAUP)) return 0;
            if (fieldName == nameof(SeaglideVisualStateDTO.WakeDirection)) return 24;
            if (fieldName == nameof(SeaglideVisualStateDTO.WakeIntensity01)) return 36;
            if (fieldName == nameof(SeaglideVisualStateDTO.Cavitation01)) return 40;
            if (fieldName == nameof(SeaglideVisualStateDTO.BrakeCloud01)) return 44;
            if (fieldName == nameof(SeaglideVisualStateDTO.SourceHash)) return 48;
            if (fieldName == nameof(SeaglideVisualStateDTO.Flags)) return 52;
            if (fieldName == nameof(SeaglideVisualStateDTO._pad0)) return 56;
            return -1;
        }

        private static int OffsetOfAudioSignal(string fieldName)
        {
            if (fieldName == nameof(SeaglideAudioSignalDTO.CurrentAUP)) return 0;
            if (fieldName == nameof(SeaglideAudioSignalDTO.DopplerSpeedMetersPerSecond)) return 24;
            if (fieldName == nameof(SeaglideAudioSignalDTO.PitchScalar)) return 28;
            if (fieldName == nameof(SeaglideAudioSignalDTO.VolumeScalar)) return 32;
            if (fieldName == nameof(SeaglideAudioSignalDTO.Cavitation01)) return 36;
            if (fieldName == nameof(SeaglideAudioSignalDTO.SourceHash)) return 40;
            if (fieldName == nameof(SeaglideAudioSignalDTO.Flags)) return 44;
            if (fieldName == nameof(SeaglideAudioSignalDTO.TargetEntityHash)) return 48;
            if (fieldName == nameof(SeaglideAudioSignalDTO.FrameIndex)) return 52;
            if (fieldName == nameof(SeaglideAudioSignalDTO._pad0)) return 56;
            return -1;
        }

        private static int OffsetOfTelemetry(string fieldName)
        {
            if (fieldName == nameof(SeaglideTelemetryEntry.FrameIndex)) return 0;
            if (fieldName == nameof(SeaglideTelemetryEntry.EvaluatedRequests)) return 4;
            if (fieldName == nameof(SeaglideTelemetryEntry.ForcePackets)) return 8;
            if (fieldName == nameof(SeaglideTelemetryEntry.NonFiniteCount)) return 12;
            if (fieldName == nameof(SeaglideTelemetryEntry.TotalThrustForce)) return 16;
            if (fieldName == nameof(SeaglideTelemetryEntry.TotalDragForce)) return 20;
            if (fieldName == nameof(SeaglideTelemetryEntry.TotalFlowForce)) return 24;
            if (fieldName == nameof(SeaglideTelemetryEntry.MaxForceMagnitude)) return 28;
            if (fieldName == nameof(SeaglideTelemetryEntry.ComputeMicros)) return 32;
            if (fieldName == nameof(SeaglideTelemetryEntry.GlobalQualityWeight)) return 36;
            if (fieldName == nameof(SeaglideTelemetryEntry.Flags)) return 40;
            if (fieldName == nameof(SeaglideTelemetryEntry.LastTargetEntityHash)) return 44;
            if (fieldName == nameof(SeaglideTelemetryEntry.LastFlowForce)) return 48;
            if (fieldName == nameof(SeaglideTelemetryEntry.LastBatteryLevel)) return 60;
            return -1;
        }
    }
}
