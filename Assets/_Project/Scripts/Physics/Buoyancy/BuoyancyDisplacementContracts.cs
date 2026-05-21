using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Physics
{
    public static class BuoyancyDisplacementConstants
    {
        public const int StateCapacity = 50000;
        public const int MockObjectCount = 50000;
        public const int FlowSampleCapacity = 4096;
        public const int MaterialVolumeCapacity = 2048;
        public const int MaterialSettlingProfileCapacity = 512;
        public const int SleepSdfCellCapacity = 65536;
        public const int CsvScratchBytes = 65536;
        public const int ForceQueueSoftCapacity = 8192;
        public const int TelemetryCapacity = 300;
        public const int TuningCapacity = 1;
        public const int CounterCapacity = 1;
        public const int StateBytes = 64;
        public const int TuningBytes = 128;
        public const int ForcePacketBytes = 128;
        public const int ForceDtoBytes = ForcePacketBytes;
        public const int FlowSampleBytes = 64;
        public const int TelemetryBytes = 64;
        public const int MaterialVolumeBytes = 32;
        public const int MaterialSettlingProfileBytes = 32;
        public const int CounterBytes = 64;
        public const int DebugForceBytes = 128;
        public const int BodyBindingBytes = 32;
        public const int SleepSdfConfigBytes = 64;
        public const int SleepTelemetryBytes = 64;
        public const int SimdBenchmarkCapacity = SimdVectorizationConstants.BenchmarkEntityCount;
        public const int SimdTelemetryCapacity = SimdVectorizationConstants.TelemetryCapacity;
        public const int SimdToleranceCapacity = SimdVectorizationConstants.ToleranceCapacity;
        public const int SimdHydrodynamicTuningCapacity = 1;
        public const float Epsilon = 0.0001f;
        public const float AuthoritativeQualityWeight = 1f;
        public const float DefaultWaterDensityKgPerM3 = 1025f;
        public const float DefaultGravityMetersPerSecondSq = 9.80665f;
        public const float DefaultLinearDragCoefficient = 3.25f;
        public const float DefaultQuadraticDragCoefficient = 0.92f;
        public const float DefaultSurfaceDampening = 0.78f;
        public const float DefaultFlowForceCoefficient = 0.35f;
        public const string DumpRelativePath = "Docs/AgentLogs/Dump_FLUID_DYNAMICS.bin";
        public const string AgentDumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_201_Buoyancy.bin";
        public const string SleepStateDumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_249.bin";
        public const string CsvRelativePath = "Data/Physics/item_volume_specs.csv";
        public const string MaterialSettlingProfilesCsvRelativePath = "Data/Physics/material_settling_profiles.csv";
        public const string SimdToleranceCsvRelativePath = "Data/Physics/simd_math_tolerances.csv";

        public const uint FlagActive = 1u << 0;
        public const uint FlagSleeping = 1u << 1;
        public const uint FlagSurfaceSnapped = 1u << 2;
        public const uint FlagSeafloorSleeping = 1u << 3;
        public const uint FlagEmergencyMock = 1u << 4;
        public const uint FlagEvaluated = 1u << 5;
        public const uint FlagForceQueued = 1u << 6;
        public const uint FlagStrideSkipped = 1u << 7;
        public const uint FlagOwnsGravityInPacket = 1u << 8;
        public const uint FlagForcePacketOverflow = 1u << 9;
        public const uint FlagSdfGrounded = 1u << 10;
        public const uint FlagDeepSleeping = 1u << 11;
        public const uint FlagStaticPromotionPending = 1u << 12;
        public const uint FlagWakeSignal = 1u << 13;
        public const uint FlagAmbientCurrentWake = 1u << 14;
        public const uint FlagNonFinite = 1u << 31;
        public const int SleepSdfConfigRestFrameOverrideShift = 16;
        public const uint SleepSdfConfigRestFrameOverrideMask = 0x00FF0000u;
    }

    public static class BuoyancyDisplacementBufferIds
    {
        public const BufferID States = BufferID.ShinobuBuoyancyStates;
        public const BufferID ForcePackets = BufferID.ShinobuBuoyancyForcePackets;
        public const BufferID FlowSamples = BufferID.ShinobuBuoyancyFlowSamples;
        public const BufferID Tuning = BufferID.ShinobuBuoyancyTuning;
        public const BufferID TelemetryRing = BufferID.ShinobuBuoyancyTelemetryRing;
        public const BufferID TelemetryCursor = BufferID.ShinobuBuoyancyTelemetryCursor;
        public const BufferID MaterialVolumes = BufferID.ShinobuBuoyancyMaterialVolumes;
        public const BufferID CsvScratch = BufferID.ShinobuBuoyancyCsvScratch;
        public const BufferID DebugForces = BufferID.ShinobuBuoyancyDebugForces;
        public const BufferID Counters = BufferID.ShinobuBuoyancyCounters;
        public const BufferID BodyBindings = BufferID.ShinobuBuoyancyBodyBindings;
        public const BufferID SimdLocalPositions = BufferID.ShinobuSimdLocalPositions;
        public const BufferID SimdVelocities = BufferID.ShinobuSimdVelocities;
        public const BufferID SimdDragCoefficients = BufferID.ShinobuSimdDragCoefficients;
        public const BufferID SimdOutputForces = BufferID.ShinobuSimdOutputForces;
        public const BufferID SimdTelemetryRing = BufferID.ShinobuSimdTelemetryRing;
        public const BufferID SimdTelemetryCursor = BufferID.ShinobuSimdTelemetryCursor;
        public const BufferID SimdMathTolerances = BufferID.ShinobuSimdMathTolerances;
        public const BufferID SimdVisibleIndexMask = BufferID.ShinobuSimdVisibleIndexMask;
        public const BufferID SimdVisibleIndices = BufferID.ShinobuSimdVisibleIndices;
        public const BufferID SimdVisibleCount = BufferID.ShinobuSimdVisibleCount;
        public const BufferID SimdHydrodynamicTuning = BufferID.ShinobuSimdHydrodynamicTuning;
        public const BufferID SleepSdfDensity = BufferID.ShinobuBuoyancySleepSdfDensity;
        public const BufferID SleepSdfConfig = BufferID.ShinobuBuoyancySleepSdfConfig;
        public const BufferID SleepTelemetryRing = BufferID.ShinobuBuoyancySleepTelemetryRing;
        public const BufferID SleepTelemetryCursor = BufferID.ShinobuBuoyancySleepTelemetryCursor;
        public const BufferID MaterialSettlingProfiles = BufferID.ShinobuBuoyancyMaterialSettlingProfiles;
    }

    [StructLayout(LayoutKind.Explicit, Size = BuoyancyDisplacementConstants.StateBytes)]
    public struct BuoyancyStateDTO
    {
        [FieldOffset(0)] public double3 CurrentAUP;
        [FieldOffset(24)] public float3 Velocity;
        [FieldOffset(36)] public float VolumeCubicMeters;
        [FieldOffset(40)] public float MassKg;
        [FieldOffset(44)] public uint EntityHashID;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public byte RestingFrameCount;
        [FieldOffset(53)] public byte DeepSleepTickCount;
        [FieldOffset(54)] public ushort MaterialSleepProfileIndex;
        /// <summary>Squared angular speed supplied by owner systems for sleep thresholding.</summary>
        [FieldOffset(56)] public float AngularSpeedSq;
        [FieldOffset(60)] public uint _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = BuoyancyDisplacementConstants.SleepSdfConfigBytes)]
    public struct BuoyancySleepSdfConfigDTO
    {
        [FieldOffset(0)] public double3 SdfOriginAUP;
        [FieldOffset(24)] public float CellSizeMeters;
        [FieldOffset(28)] public float DensityDecodeScale;
        [FieldOffset(32)] public float ContactEpsilonMeters;
        [FieldOffset(36)] public float AmbientStirThresholdSq;
        [FieldOffset(40)] public int Width;
        [FieldOffset(44)] public int Height;
        [FieldOffset(48)] public int Depth;
        [FieldOffset(52)] public int StrideY;
        [FieldOffset(56)] public int StrideZ;
        [FieldOffset(60)] public uint Flags;

        public static BuoyancySleepSdfConfigDTO Default()
        {
            BuoyancySleepSdfConfigDTO value = default;
            value.SdfOriginAUP = double3.zero;
            value.CellSizeMeters = 1f;
            value.DensityDecodeScale = 0.05f;
            value.ContactEpsilonMeters = 0.2f;
            value.AmbientStirThresholdSq = 0.25f;
            value.Width = 0;
            value.Height = 0;
            value.Depth = 0;
            value.StrideY = 0;
            value.StrideZ = 0;
            value.Flags = 0u;
            return value;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = BuoyancyDisplacementConstants.SleepTelemetryBytes)]
    public struct SleepStateTelemetryEntry
    {
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public int ActiveObjects;
        [FieldOffset(8)] public int SleepingObjects;
        [FieldOffset(12)] public int ForcedAwakeObjects;
        [FieldOffset(16)] public int StaticPromotionCandidates;
        [FieldOffset(20)] public int NonFiniteCount;
        [FieldOffset(24)] public float ComputeMicros;
        [FieldOffset(28)] public float GlobalQualityWeight;
        [FieldOffset(32)] public float SleepEnergyThreshold;
        [FieldOffset(36)] public int SdfGroundedObjects;
        [FieldOffset(40)] public int WakeRequestCount;
        [FieldOffset(44)] public int AmbientCurrentWakes;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public uint LastEntityHashID;
        [FieldOffset(56)] public float MaxEnergy;
        [FieldOffset(60)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = BuoyancyDisplacementConstants.MaterialSettlingProfileBytes)]
    public struct BuoyancyMaterialSettlingProfileDTO
    {
        [FieldOffset(0)] public uint MaterialHash;
        [FieldOffset(4)] public float LinearSleepSpeedSq;
        [FieldOffset(8)] public float AngularSleepSpeedSq;
        [FieldOffset(12)] public float ForceSleepThresholdSq;
        [FieldOffset(16)] public ushort RequiredRestFrames;
        [FieldOffset(18)] public ushort DeepSleepTicks;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = BuoyancyDisplacementConstants.TuningBytes)]
    public struct BuoyancyTuningDTO
    {
        [FieldOffset(0)] public double3 OceanSurfaceAUP;
        [FieldOffset(24)] public double3 SectorAUP;
        [FieldOffset(48)] public float WaterDensityKgPerM3;
        [FieldOffset(52)] public float GravityMetersPerSecondSq;
        [FieldOffset(56)] public float LinearDragCoefficient;
        [FieldOffset(60)] public float QuadraticDragCoefficient;
        [FieldOffset(64)] public float SurfaceDampening;
        [FieldOffset(68)] public float SleepSpeedSq;
        [FieldOffset(72)] public float SleepForceThreshold;
        [FieldOffset(76)] public float DensityDepthCoefficient;
        [FieldOffset(80)] public float SeafloorAUPY;
        [FieldOffset(84)] public float GlobalQualityWeight;
        [FieldOffset(88)] public float SimulationTickDelta;
        [FieldOffset(92)] public int ActiveStateCount;
        [FieldOffset(96)] public float FlowForceCoefficient;
        [FieldOffset(100)] public float SurfaceSnapDepthMeters;
        [FieldOffset(104)] public float MinFluidDensityKgPerM3;
        [FieldOffset(108)] public float MaxFluidDensityKgPerM3;
        [FieldOffset(112)] public int MockStateCount;
        [FieldOffset(116)] public uint FrameIndex;
        [FieldOffset(120)] public uint Flags;
        [FieldOffset(124)] public float ResolvedQualityWeight;

        public static BuoyancyTuningDTO Default()
        {
            BuoyancyTuningDTO value = default;
            value.OceanSurfaceAUP = double3.zero;
            value.SectorAUP = double3.zero;
            value.WaterDensityKgPerM3 = BuoyancyDisplacementConstants.DefaultWaterDensityKgPerM3;
            value.GravityMetersPerSecondSq = BuoyancyDisplacementConstants.DefaultGravityMetersPerSecondSq;
            value.LinearDragCoefficient = BuoyancyDisplacementConstants.DefaultLinearDragCoefficient;
            value.QuadraticDragCoefficient = BuoyancyDisplacementConstants.DefaultQuadraticDragCoefficient;
            value.SurfaceDampening = BuoyancyDisplacementConstants.DefaultSurfaceDampening;
            value.SleepSpeedSq = 0.0009f;
            value.SleepForceThreshold = 0.45f;
            value.DensityDepthCoefficient = 0.000045f;
            value.SeafloorAUPY = -10000f;
            value.GlobalQualityWeight = 1f;
            value.SimulationTickDelta = 0.02f;
            value.ActiveStateCount = 0;
            value.FlowForceCoefficient = BuoyancyDisplacementConstants.DefaultFlowForceCoefficient;
            value.SurfaceSnapDepthMeters = 0.18f;
            value.MinFluidDensityKgPerM3 = 900f;
            value.MaxFluidDensityKgPerM3 = 1160f;
            value.MockStateCount = BuoyancyDisplacementConstants.MockObjectCount;
            value.Flags = BuoyancyDisplacementConstants.FlagOwnsGravityInPacket;
            value.ResolvedQualityWeight = 1f;
            return value;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = BuoyancyDisplacementConstants.ForceDtoBytes)]
    public struct BuoyancyForcePacketDTO
    {
        [FieldOffset(0)] public double3 CurrentAUP;
        [FieldOffset(24)] public float3 NetForce;
        [FieldOffset(36)] public float3 BuoyantForce;
        [FieldOffset(48)] public float3 GravityForce;
        [FieldOffset(60)] public float3 DragForce;
        [FieldOffset(72)] public float3 FlowForce;
        [FieldOffset(84)] public float SubmergedFraction;
        [FieldOffset(88)] public float DepthMeters;
        [FieldOffset(92)] public float FluidDensityKgPerM3;
        [FieldOffset(96)] public uint EntityHashID;
        [FieldOffset(100)] public uint Flags;
        [FieldOffset(104)] public int StateIndex;
        [FieldOffset(108)] public uint FrameIndex;
        [FieldOffset(112)] public float3 DebugVelocity;
        [FieldOffset(124)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = BuoyancyDisplacementConstants.FlowSampleBytes)]
    public struct BuoyancyFlowSampleDTO
    {
        [FieldOffset(0)] public double3 SampleAUP;
        [FieldOffset(24)] public float3 FlowVelocity;
        [FieldOffset(36)] public float RadiusMeters;
        [FieldOffset(40)] public uint CellHash;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public ulong _pad0;
        [FieldOffset(56)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = BuoyancyDisplacementConstants.TelemetryBytes)]
    public struct BuoyancyTelemetryEntry
    {
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public int EvaluatedObjects;
        [FieldOffset(8)] public int SleepingObjects;
        [FieldOffset(12)] public int ForcePackets;
        [FieldOffset(16)] public float TotalBuoyantForce;
        [FieldOffset(20)] public float TotalDragForce;
        [FieldOffset(24)] public float ComputeMicros;
        [FieldOffset(28)] public float GlobalQualityWeight;
        [FieldOffset(32)] public uint Flags;
        [FieldOffset(36)] public int NonFiniteCount;
        [FieldOffset(40)] public uint LastEntityHashID;
        [FieldOffset(44)] public float MaxDepthMeters;
        [FieldOffset(48)] public float3 LastNetForce;
        [FieldOffset(60)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = BuoyancyDisplacementConstants.MaterialVolumeBytes)]
    public struct BuoyancyMaterialVolumeDTO
    {
        [FieldOffset(0)] public uint ItemHash;
        [FieldOffset(4)] public float MassKg;
        [FieldOffset(8)] public float VolumeCubicMeters;
        [FieldOffset(12)] public float HeightHintMeters;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public uint _pad0;
        [FieldOffset(24)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = BuoyancyDisplacementConstants.CounterBytes)]
    public struct BuoyancyCounterDTO
    {
        [FieldOffset(0)] public int EvaluatedObjects;
        [FieldOffset(4)] public int SleepingObjects;
        [FieldOffset(8)] public int ForcePackets;
        [FieldOffset(12)] public int NonFiniteCount;
        [FieldOffset(16)] public float TotalBuoyantForce;
        [FieldOffset(20)] public float TotalDragForce;
        [FieldOffset(24)] public float MaxDepthMeters;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public uint LastEntityHashID;
        [FieldOffset(36)] public float ComputeMicros;
        [FieldOffset(40)] public int ForcedAwakeObjects;
        [FieldOffset(44)] public int StaticPromotionCandidates;
        [FieldOffset(48)] public int AmbientCurrentWakes;
        [FieldOffset(52)] public int SdfGroundedObjects;
        [FieldOffset(56)] public ulong _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = BuoyancyDisplacementConstants.DebugForceBytes)]
    public struct BuoyancyDebugForceDTO
    {
        [FieldOffset(0)] public double3 CurrentAUP;
        [FieldOffset(24)] public float3 BuoyantForce;
        [FieldOffset(36)] public float3 GravityForce;
        [FieldOffset(48)] public float3 DragForce;
        [FieldOffset(60)] public float3 NetForce;
        [FieldOffset(72)] public float3 FlowForce;
        [FieldOffset(84)] public float SubmergedFraction;
        [FieldOffset(88)] public float DepthMeters;
        [FieldOffset(92)] public uint EntityHashID;
        [FieldOffset(96)] public uint Flags;
        [FieldOffset(100)] public int StateIndex;
        [FieldOffset(104)] public uint FrameIndex;
        [FieldOffset(108)] public float3 Velocity;
        [FieldOffset(120)] public float SleepScore;
        [FieldOffset(124)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = BuoyancyDisplacementConstants.BodyBindingBytes)]
    public struct BuoyancyBodyBindingDTO
    {
        [FieldOffset(0)] public uint EntityHashID;
        [FieldOffset(4)] public int StateIndex;
        [FieldOffset(8)] public int RigidbodyIndex;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public ulong _pad0;
        [FieldOffset(24)] public ulong _pad1;
    }

    public static class BuoyancyDisplacementLayout
    {
        private static readonly bool s_validateOnce = ValidateInternal();

        public static bool Validate()
        {
            return s_validateOnce;
        }

        private static bool ValidateInternal()
        {
            return UnsafeUtility.SizeOf<BuoyancyStateDTO>() == BuoyancyDisplacementConstants.StateBytes &&
                   UnsafeUtility.SizeOf<BuoyancyTuningDTO>() == BuoyancyDisplacementConstants.TuningBytes &&
                   UnsafeUtility.SizeOf<BuoyancyForcePacketDTO>() == BuoyancyDisplacementConstants.ForcePacketBytes &&
                   UnsafeUtility.SizeOf<BuoyancyFlowSampleDTO>() == BuoyancyDisplacementConstants.FlowSampleBytes &&
                   UnsafeUtility.SizeOf<BuoyancyTelemetryEntry>() == BuoyancyDisplacementConstants.TelemetryBytes &&
                   UnsafeUtility.SizeOf<BuoyancySleepSdfConfigDTO>() == BuoyancyDisplacementConstants.SleepSdfConfigBytes &&
                   UnsafeUtility.SizeOf<SleepStateTelemetryEntry>() == BuoyancyDisplacementConstants.SleepTelemetryBytes &&
                   UnsafeUtility.SizeOf<BuoyancyMaterialVolumeDTO>() == BuoyancyDisplacementConstants.MaterialVolumeBytes &&
                   UnsafeUtility.SizeOf<BuoyancyMaterialSettlingProfileDTO>() == BuoyancyDisplacementConstants.MaterialSettlingProfileBytes &&
                   UnsafeUtility.SizeOf<BuoyancyCounterDTO>() == BuoyancyDisplacementConstants.CounterBytes &&
                   UnsafeUtility.SizeOf<BuoyancyDebugForceDTO>() == BuoyancyDisplacementConstants.DebugForceBytes &&
                   UnsafeUtility.SizeOf<BuoyancyBodyBindingDTO>() == BuoyancyDisplacementConstants.BodyBindingBytes &&
                   ValidateStateOffsets() &&
                   ValidateTuningOffsets() &&
                   ValidateForcePacketOffsets() &&
                   ValidateFlowSampleOffsets() &&
                   ValidateTelemetryOffsets() &&
                   ValidateSleepSdfConfigOffsets() &&
                   ValidateSleepTelemetryOffsets() &&
                   ValidateMaterialVolumeOffsets() &&
                   ValidateMaterialSettlingOffsets() &&
                   ValidateCounterOffsets() &&
                   ValidateDebugForceOffsets() &&
                   ValidateBodyBindingOffsets();
        }

        private static bool ValidateStateOffsets()
        {
            return OffsetOf<BuoyancyStateDTO>(nameof(BuoyancyStateDTO.CurrentAUP)) == 0 &&
                   OffsetOf<BuoyancyStateDTO>(nameof(BuoyancyStateDTO.Velocity)) == 24 &&
                   OffsetOf<BuoyancyStateDTO>(nameof(BuoyancyStateDTO.VolumeCubicMeters)) == 36 &&
                   OffsetOf<BuoyancyStateDTO>(nameof(BuoyancyStateDTO.MassKg)) == 40 &&
                   OffsetOf<BuoyancyStateDTO>(nameof(BuoyancyStateDTO.EntityHashID)) == 44 &&
                   OffsetOf<BuoyancyStateDTO>(nameof(BuoyancyStateDTO.Flags)) == 48 &&
                   OffsetOf<BuoyancyStateDTO>(nameof(BuoyancyStateDTO.RestingFrameCount)) == 52 &&
                   OffsetOf<BuoyancyStateDTO>(nameof(BuoyancyStateDTO.DeepSleepTickCount)) == 53 &&
                   OffsetOf<BuoyancyStateDTO>(nameof(BuoyancyStateDTO.MaterialSleepProfileIndex)) == 54 &&
                   OffsetOf<BuoyancyStateDTO>(nameof(BuoyancyStateDTO.AngularSpeedSq)) == 56 &&
                   OffsetOf<BuoyancyStateDTO>(nameof(BuoyancyStateDTO._pad1)) == 60;
        }

        private static bool ValidateTuningOffsets()
        {
            return OffsetOf<BuoyancyTuningDTO>(nameof(BuoyancyTuningDTO.OceanSurfaceAUP)) == 0 &&
                   OffsetOf<BuoyancyTuningDTO>(nameof(BuoyancyTuningDTO.SectorAUP)) == 24 &&
                   OffsetOf<BuoyancyTuningDTO>(nameof(BuoyancyTuningDTO.WaterDensityKgPerM3)) == 48 &&
                   OffsetOf<BuoyancyTuningDTO>(nameof(BuoyancyTuningDTO.GravityMetersPerSecondSq)) == 52 &&
                   OffsetOf<BuoyancyTuningDTO>(nameof(BuoyancyTuningDTO.LinearDragCoefficient)) == 56 &&
                   OffsetOf<BuoyancyTuningDTO>(nameof(BuoyancyTuningDTO.QuadraticDragCoefficient)) == 60 &&
                   OffsetOf<BuoyancyTuningDTO>(nameof(BuoyancyTuningDTO.SurfaceDampening)) == 64 &&
                   OffsetOf<BuoyancyTuningDTO>(nameof(BuoyancyTuningDTO.SleepSpeedSq)) == 68 &&
                   OffsetOf<BuoyancyTuningDTO>(nameof(BuoyancyTuningDTO.SleepForceThreshold)) == 72 &&
                   OffsetOf<BuoyancyTuningDTO>(nameof(BuoyancyTuningDTO.DensityDepthCoefficient)) == 76 &&
                   OffsetOf<BuoyancyTuningDTO>(nameof(BuoyancyTuningDTO.SeafloorAUPY)) == 80 &&
                   OffsetOf<BuoyancyTuningDTO>(nameof(BuoyancyTuningDTO.GlobalQualityWeight)) == 84 &&
                   OffsetOf<BuoyancyTuningDTO>(nameof(BuoyancyTuningDTO.SimulationTickDelta)) == 88 &&
                   OffsetOf<BuoyancyTuningDTO>(nameof(BuoyancyTuningDTO.ActiveStateCount)) == 92 &&
                   OffsetOf<BuoyancyTuningDTO>(nameof(BuoyancyTuningDTO.FlowForceCoefficient)) == 96 &&
                   OffsetOf<BuoyancyTuningDTO>(nameof(BuoyancyTuningDTO.SurfaceSnapDepthMeters)) == 100 &&
                   OffsetOf<BuoyancyTuningDTO>(nameof(BuoyancyTuningDTO.MinFluidDensityKgPerM3)) == 104 &&
                   OffsetOf<BuoyancyTuningDTO>(nameof(BuoyancyTuningDTO.MaxFluidDensityKgPerM3)) == 108 &&
                   OffsetOf<BuoyancyTuningDTO>(nameof(BuoyancyTuningDTO.MockStateCount)) == 112 &&
                   OffsetOf<BuoyancyTuningDTO>(nameof(BuoyancyTuningDTO.FrameIndex)) == 116 &&
                   OffsetOf<BuoyancyTuningDTO>(nameof(BuoyancyTuningDTO.Flags)) == 120 &&
                   OffsetOf<BuoyancyTuningDTO>(nameof(BuoyancyTuningDTO.ResolvedQualityWeight)) == 124;
        }

        private static bool ValidateForcePacketOffsets()
        {
            return OffsetOf<BuoyancyForcePacketDTO>(nameof(BuoyancyForcePacketDTO.CurrentAUP)) == 0 &&
                   OffsetOf<BuoyancyForcePacketDTO>(nameof(BuoyancyForcePacketDTO.NetForce)) == 24 &&
                   OffsetOf<BuoyancyForcePacketDTO>(nameof(BuoyancyForcePacketDTO.BuoyantForce)) == 36 &&
                   OffsetOf<BuoyancyForcePacketDTO>(nameof(BuoyancyForcePacketDTO.GravityForce)) == 48 &&
                   OffsetOf<BuoyancyForcePacketDTO>(nameof(BuoyancyForcePacketDTO.DragForce)) == 60 &&
                   OffsetOf<BuoyancyForcePacketDTO>(nameof(BuoyancyForcePacketDTO.FlowForce)) == 72 &&
                   OffsetOf<BuoyancyForcePacketDTO>(nameof(BuoyancyForcePacketDTO.SubmergedFraction)) == 84 &&
                   OffsetOf<BuoyancyForcePacketDTO>(nameof(BuoyancyForcePacketDTO.DepthMeters)) == 88 &&
                   OffsetOf<BuoyancyForcePacketDTO>(nameof(BuoyancyForcePacketDTO.FluidDensityKgPerM3)) == 92 &&
                   OffsetOf<BuoyancyForcePacketDTO>(nameof(BuoyancyForcePacketDTO.EntityHashID)) == 96 &&
                   OffsetOf<BuoyancyForcePacketDTO>(nameof(BuoyancyForcePacketDTO.Flags)) == 100 &&
                   OffsetOf<BuoyancyForcePacketDTO>(nameof(BuoyancyForcePacketDTO.StateIndex)) == 104 &&
                   OffsetOf<BuoyancyForcePacketDTO>(nameof(BuoyancyForcePacketDTO.FrameIndex)) == 108 &&
                   OffsetOf<BuoyancyForcePacketDTO>(nameof(BuoyancyForcePacketDTO.DebugVelocity)) == 112 &&
                   OffsetOf<BuoyancyForcePacketDTO>(nameof(BuoyancyForcePacketDTO._pad0)) == 124;
        }

        private static bool ValidateFlowSampleOffsets()
        {
            return OffsetOf<BuoyancyFlowSampleDTO>(nameof(BuoyancyFlowSampleDTO.SampleAUP)) == 0 &&
                   OffsetOf<BuoyancyFlowSampleDTO>(nameof(BuoyancyFlowSampleDTO.FlowVelocity)) == 24 &&
                   OffsetOf<BuoyancyFlowSampleDTO>(nameof(BuoyancyFlowSampleDTO.RadiusMeters)) == 36 &&
                   OffsetOf<BuoyancyFlowSampleDTO>(nameof(BuoyancyFlowSampleDTO.CellHash)) == 40 &&
                   OffsetOf<BuoyancyFlowSampleDTO>(nameof(BuoyancyFlowSampleDTO.Flags)) == 44 &&
                   OffsetOf<BuoyancyFlowSampleDTO>(nameof(BuoyancyFlowSampleDTO._pad0)) == 48 &&
                   OffsetOf<BuoyancyFlowSampleDTO>(nameof(BuoyancyFlowSampleDTO._pad1)) == 56;
        }

        private static bool ValidateTelemetryOffsets()
        {
            return OffsetOf<BuoyancyTelemetryEntry>(nameof(BuoyancyTelemetryEntry.FrameIndex)) == 0 &&
                   OffsetOf<BuoyancyTelemetryEntry>(nameof(BuoyancyTelemetryEntry.EvaluatedObjects)) == 4 &&
                   OffsetOf<BuoyancyTelemetryEntry>(nameof(BuoyancyTelemetryEntry.SleepingObjects)) == 8 &&
                   OffsetOf<BuoyancyTelemetryEntry>(nameof(BuoyancyTelemetryEntry.ForcePackets)) == 12 &&
                   OffsetOf<BuoyancyTelemetryEntry>(nameof(BuoyancyTelemetryEntry.TotalBuoyantForce)) == 16 &&
                   OffsetOf<BuoyancyTelemetryEntry>(nameof(BuoyancyTelemetryEntry.TotalDragForce)) == 20 &&
                   OffsetOf<BuoyancyTelemetryEntry>(nameof(BuoyancyTelemetryEntry.ComputeMicros)) == 24 &&
                   OffsetOf<BuoyancyTelemetryEntry>(nameof(BuoyancyTelemetryEntry.GlobalQualityWeight)) == 28 &&
                   OffsetOf<BuoyancyTelemetryEntry>(nameof(BuoyancyTelemetryEntry.Flags)) == 32 &&
                   OffsetOf<BuoyancyTelemetryEntry>(nameof(BuoyancyTelemetryEntry.NonFiniteCount)) == 36 &&
                   OffsetOf<BuoyancyTelemetryEntry>(nameof(BuoyancyTelemetryEntry.LastEntityHashID)) == 40 &&
                   OffsetOf<BuoyancyTelemetryEntry>(nameof(BuoyancyTelemetryEntry.MaxDepthMeters)) == 44 &&
                   OffsetOf<BuoyancyTelemetryEntry>(nameof(BuoyancyTelemetryEntry.LastNetForce)) == 48 &&
                   OffsetOf<BuoyancyTelemetryEntry>(nameof(BuoyancyTelemetryEntry._pad0)) == 60;
        }

        private static bool ValidateSleepSdfConfigOffsets()
        {
            return OffsetOf<BuoyancySleepSdfConfigDTO>(nameof(BuoyancySleepSdfConfigDTO.SdfOriginAUP)) == 0 &&
                   OffsetOf<BuoyancySleepSdfConfigDTO>(nameof(BuoyancySleepSdfConfigDTO.CellSizeMeters)) == 24 &&
                   OffsetOf<BuoyancySleepSdfConfigDTO>(nameof(BuoyancySleepSdfConfigDTO.DensityDecodeScale)) == 28 &&
                   OffsetOf<BuoyancySleepSdfConfigDTO>(nameof(BuoyancySleepSdfConfigDTO.ContactEpsilonMeters)) == 32 &&
                   OffsetOf<BuoyancySleepSdfConfigDTO>(nameof(BuoyancySleepSdfConfigDTO.AmbientStirThresholdSq)) == 36 &&
                   OffsetOf<BuoyancySleepSdfConfigDTO>(nameof(BuoyancySleepSdfConfigDTO.Width)) == 40 &&
                   OffsetOf<BuoyancySleepSdfConfigDTO>(nameof(BuoyancySleepSdfConfigDTO.Height)) == 44 &&
                   OffsetOf<BuoyancySleepSdfConfigDTO>(nameof(BuoyancySleepSdfConfigDTO.Depth)) == 48 &&
                   OffsetOf<BuoyancySleepSdfConfigDTO>(nameof(BuoyancySleepSdfConfigDTO.StrideY)) == 52 &&
                   OffsetOf<BuoyancySleepSdfConfigDTO>(nameof(BuoyancySleepSdfConfigDTO.StrideZ)) == 56 &&
                   OffsetOf<BuoyancySleepSdfConfigDTO>(nameof(BuoyancySleepSdfConfigDTO.Flags)) == 60;
        }

        private static bool ValidateSleepTelemetryOffsets()
        {
            return OffsetOf<SleepStateTelemetryEntry>(nameof(SleepStateTelemetryEntry.FrameIndex)) == 0 &&
                   OffsetOf<SleepStateTelemetryEntry>(nameof(SleepStateTelemetryEntry.ActiveObjects)) == 4 &&
                   OffsetOf<SleepStateTelemetryEntry>(nameof(SleepStateTelemetryEntry.SleepingObjects)) == 8 &&
                   OffsetOf<SleepStateTelemetryEntry>(nameof(SleepStateTelemetryEntry.ForcedAwakeObjects)) == 12 &&
                   OffsetOf<SleepStateTelemetryEntry>(nameof(SleepStateTelemetryEntry.StaticPromotionCandidates)) == 16 &&
                   OffsetOf<SleepStateTelemetryEntry>(nameof(SleepStateTelemetryEntry.NonFiniteCount)) == 20 &&
                   OffsetOf<SleepStateTelemetryEntry>(nameof(SleepStateTelemetryEntry.ComputeMicros)) == 24 &&
                   OffsetOf<SleepStateTelemetryEntry>(nameof(SleepStateTelemetryEntry.GlobalQualityWeight)) == 28 &&
                   OffsetOf<SleepStateTelemetryEntry>(nameof(SleepStateTelemetryEntry.SleepEnergyThreshold)) == 32 &&
                   OffsetOf<SleepStateTelemetryEntry>(nameof(SleepStateTelemetryEntry.SdfGroundedObjects)) == 36 &&
                   OffsetOf<SleepStateTelemetryEntry>(nameof(SleepStateTelemetryEntry.WakeRequestCount)) == 40 &&
                   OffsetOf<SleepStateTelemetryEntry>(nameof(SleepStateTelemetryEntry.AmbientCurrentWakes)) == 44 &&
                   OffsetOf<SleepStateTelemetryEntry>(nameof(SleepStateTelemetryEntry.Flags)) == 48 &&
                   OffsetOf<SleepStateTelemetryEntry>(nameof(SleepStateTelemetryEntry.LastEntityHashID)) == 52 &&
                   OffsetOf<SleepStateTelemetryEntry>(nameof(SleepStateTelemetryEntry.MaxEnergy)) == 56 &&
                   OffsetOf<SleepStateTelemetryEntry>(nameof(SleepStateTelemetryEntry._pad0)) == 60;
        }

        private static bool ValidateMaterialVolumeOffsets()
        {
            return OffsetOf<BuoyancyMaterialVolumeDTO>(nameof(BuoyancyMaterialVolumeDTO.ItemHash)) == 0 &&
                   OffsetOf<BuoyancyMaterialVolumeDTO>(nameof(BuoyancyMaterialVolumeDTO.MassKg)) == 4 &&
                   OffsetOf<BuoyancyMaterialVolumeDTO>(nameof(BuoyancyMaterialVolumeDTO.VolumeCubicMeters)) == 8 &&
                   OffsetOf<BuoyancyMaterialVolumeDTO>(nameof(BuoyancyMaterialVolumeDTO.HeightHintMeters)) == 12 &&
                   OffsetOf<BuoyancyMaterialVolumeDTO>(nameof(BuoyancyMaterialVolumeDTO.Flags)) == 16 &&
                   OffsetOf<BuoyancyMaterialVolumeDTO>(nameof(BuoyancyMaterialVolumeDTO._pad0)) == 20 &&
                   OffsetOf<BuoyancyMaterialVolumeDTO>(nameof(BuoyancyMaterialVolumeDTO._pad1)) == 24;
        }

        private static bool ValidateMaterialSettlingOffsets()
        {
            return OffsetOf<BuoyancyMaterialSettlingProfileDTO>(nameof(BuoyancyMaterialSettlingProfileDTO.MaterialHash)) == 0 &&
                   OffsetOf<BuoyancyMaterialSettlingProfileDTO>(nameof(BuoyancyMaterialSettlingProfileDTO.LinearSleepSpeedSq)) == 4 &&
                   OffsetOf<BuoyancyMaterialSettlingProfileDTO>(nameof(BuoyancyMaterialSettlingProfileDTO.AngularSleepSpeedSq)) == 8 &&
                   OffsetOf<BuoyancyMaterialSettlingProfileDTO>(nameof(BuoyancyMaterialSettlingProfileDTO.ForceSleepThresholdSq)) == 12 &&
                   OffsetOf<BuoyancyMaterialSettlingProfileDTO>(nameof(BuoyancyMaterialSettlingProfileDTO.RequiredRestFrames)) == 16 &&
                   OffsetOf<BuoyancyMaterialSettlingProfileDTO>(nameof(BuoyancyMaterialSettlingProfileDTO.DeepSleepTicks)) == 18 &&
                   OffsetOf<BuoyancyMaterialSettlingProfileDTO>(nameof(BuoyancyMaterialSettlingProfileDTO.Flags)) == 20 &&
                   OffsetOf<BuoyancyMaterialSettlingProfileDTO>(nameof(BuoyancyMaterialSettlingProfileDTO._pad0)) == 24;
        }

        private static bool ValidateCounterOffsets()
        {
            return OffsetOf<BuoyancyCounterDTO>(nameof(BuoyancyCounterDTO.EvaluatedObjects)) == 0 &&
                   OffsetOf<BuoyancyCounterDTO>(nameof(BuoyancyCounterDTO.SleepingObjects)) == 4 &&
                   OffsetOf<BuoyancyCounterDTO>(nameof(BuoyancyCounterDTO.ForcePackets)) == 8 &&
                   OffsetOf<BuoyancyCounterDTO>(nameof(BuoyancyCounterDTO.NonFiniteCount)) == 12 &&
                   OffsetOf<BuoyancyCounterDTO>(nameof(BuoyancyCounterDTO.TotalBuoyantForce)) == 16 &&
                   OffsetOf<BuoyancyCounterDTO>(nameof(BuoyancyCounterDTO.TotalDragForce)) == 20 &&
                   OffsetOf<BuoyancyCounterDTO>(nameof(BuoyancyCounterDTO.MaxDepthMeters)) == 24 &&
                   OffsetOf<BuoyancyCounterDTO>(nameof(BuoyancyCounterDTO.Flags)) == 28 &&
                   OffsetOf<BuoyancyCounterDTO>(nameof(BuoyancyCounterDTO.LastEntityHashID)) == 32 &&
                   OffsetOf<BuoyancyCounterDTO>(nameof(BuoyancyCounterDTO.ComputeMicros)) == 36 &&
                   OffsetOf<BuoyancyCounterDTO>(nameof(BuoyancyCounterDTO.ForcedAwakeObjects)) == 40 &&
                   OffsetOf<BuoyancyCounterDTO>(nameof(BuoyancyCounterDTO.StaticPromotionCandidates)) == 44 &&
                   OffsetOf<BuoyancyCounterDTO>(nameof(BuoyancyCounterDTO.AmbientCurrentWakes)) == 48 &&
                   OffsetOf<BuoyancyCounterDTO>(nameof(BuoyancyCounterDTO.SdfGroundedObjects)) == 52 &&
                   OffsetOf<BuoyancyCounterDTO>(nameof(BuoyancyCounterDTO._pad2)) == 56;
        }

        private static bool ValidateDebugForceOffsets()
        {
            return OffsetOf<BuoyancyDebugForceDTO>(nameof(BuoyancyDebugForceDTO.CurrentAUP)) == 0 &&
                   OffsetOf<BuoyancyDebugForceDTO>(nameof(BuoyancyDebugForceDTO.BuoyantForce)) == 24 &&
                   OffsetOf<BuoyancyDebugForceDTO>(nameof(BuoyancyDebugForceDTO.GravityForce)) == 36 &&
                   OffsetOf<BuoyancyDebugForceDTO>(nameof(BuoyancyDebugForceDTO.DragForce)) == 48 &&
                   OffsetOf<BuoyancyDebugForceDTO>(nameof(BuoyancyDebugForceDTO.NetForce)) == 60 &&
                   OffsetOf<BuoyancyDebugForceDTO>(nameof(BuoyancyDebugForceDTO.FlowForce)) == 72 &&
                   OffsetOf<BuoyancyDebugForceDTO>(nameof(BuoyancyDebugForceDTO.SubmergedFraction)) == 84 &&
                   OffsetOf<BuoyancyDebugForceDTO>(nameof(BuoyancyDebugForceDTO.DepthMeters)) == 88 &&
                   OffsetOf<BuoyancyDebugForceDTO>(nameof(BuoyancyDebugForceDTO.EntityHashID)) == 92 &&
                   OffsetOf<BuoyancyDebugForceDTO>(nameof(BuoyancyDebugForceDTO.Flags)) == 96 &&
                   OffsetOf<BuoyancyDebugForceDTO>(nameof(BuoyancyDebugForceDTO.StateIndex)) == 100 &&
                   OffsetOf<BuoyancyDebugForceDTO>(nameof(BuoyancyDebugForceDTO.FrameIndex)) == 104 &&
                   OffsetOf<BuoyancyDebugForceDTO>(nameof(BuoyancyDebugForceDTO.Velocity)) == 108 &&
                   OffsetOf<BuoyancyDebugForceDTO>(nameof(BuoyancyDebugForceDTO.SleepScore)) == 120 &&
                   OffsetOf<BuoyancyDebugForceDTO>(nameof(BuoyancyDebugForceDTO._pad0)) == 124;
        }

        private static bool ValidateBodyBindingOffsets()
        {
            return OffsetOf<BuoyancyBodyBindingDTO>(nameof(BuoyancyBodyBindingDTO.EntityHashID)) == 0 &&
                   OffsetOf<BuoyancyBodyBindingDTO>(nameof(BuoyancyBodyBindingDTO.StateIndex)) == 4 &&
                   OffsetOf<BuoyancyBodyBindingDTO>(nameof(BuoyancyBodyBindingDTO.RigidbodyIndex)) == 8 &&
                   OffsetOf<BuoyancyBodyBindingDTO>(nameof(BuoyancyBodyBindingDTO.Flags)) == 12 &&
                   OffsetOf<BuoyancyBodyBindingDTO>(nameof(BuoyancyBodyBindingDTO._pad0)) == 16 &&
                   OffsetOf<BuoyancyBodyBindingDTO>(nameof(BuoyancyBodyBindingDTO._pad1)) == 24;
        }

        public static int OffsetOf<T>(string fieldName) where T : struct
        {
            Type type = typeof(T);
            if (type == typeof(BuoyancyStateDTO))
                return OffsetOfState(fieldName);
            if (type == typeof(BuoyancyTuningDTO))
                return OffsetOfTuning(fieldName);
            if (type == typeof(BuoyancyForcePacketDTO))
                return OffsetOfForcePacket(fieldName);
            if (type == typeof(BuoyancyFlowSampleDTO))
                return OffsetOfFlowSample(fieldName);
            if (type == typeof(BuoyancyTelemetryEntry))
                return OffsetOfTelemetry(fieldName);
            if (type == typeof(BuoyancySleepSdfConfigDTO))
                return OffsetOfSleepSdfConfig(fieldName);
            if (type == typeof(SleepStateTelemetryEntry))
                return OffsetOfSleepTelemetry(fieldName);
            if (type == typeof(BuoyancyMaterialVolumeDTO))
                return OffsetOfMaterialVolume(fieldName);
            if (type == typeof(BuoyancyMaterialSettlingProfileDTO))
                return OffsetOfMaterialSettling(fieldName);
            if (type == typeof(BuoyancyCounterDTO))
                return OffsetOfCounter(fieldName);
            if (type == typeof(BuoyancyDebugForceDTO))
                return OffsetOfDebugForce(fieldName);
            if (type == typeof(BuoyancyBodyBindingDTO))
                return OffsetOfBodyBinding(fieldName);

            return -1;
        }

        private static int OffsetOfState(string fieldName)
        {
            if (fieldName == nameof(BuoyancyStateDTO.CurrentAUP))
                return 0;
            if (fieldName == nameof(BuoyancyStateDTO.Velocity))
                return 24;
            if (fieldName == nameof(BuoyancyStateDTO.VolumeCubicMeters))
                return 36;
            if (fieldName == nameof(BuoyancyStateDTO.MassKg))
                return 40;
            if (fieldName == nameof(BuoyancyStateDTO.EntityHashID))
                return 44;
            if (fieldName == nameof(BuoyancyStateDTO.Flags))
                return 48;
            if (fieldName == nameof(BuoyancyStateDTO.RestingFrameCount))
                return 52;
            if (fieldName == nameof(BuoyancyStateDTO.DeepSleepTickCount))
                return 53;
            if (fieldName == nameof(BuoyancyStateDTO.MaterialSleepProfileIndex))
                return 54;
            if (fieldName == nameof(BuoyancyStateDTO.AngularSpeedSq))
                return 56;
            if (fieldName == nameof(BuoyancyStateDTO._pad1))
                return 60;

            return -1;
        }

        private static int OffsetOfTuning(string fieldName)
        {
            if (fieldName == nameof(BuoyancyTuningDTO.OceanSurfaceAUP)) return 0;
            if (fieldName == nameof(BuoyancyTuningDTO.SectorAUP)) return 24;
            if (fieldName == nameof(BuoyancyTuningDTO.WaterDensityKgPerM3)) return 48;
            if (fieldName == nameof(BuoyancyTuningDTO.GravityMetersPerSecondSq)) return 52;
            if (fieldName == nameof(BuoyancyTuningDTO.LinearDragCoefficient)) return 56;
            if (fieldName == nameof(BuoyancyTuningDTO.QuadraticDragCoefficient)) return 60;
            if (fieldName == nameof(BuoyancyTuningDTO.SurfaceDampening)) return 64;
            if (fieldName == nameof(BuoyancyTuningDTO.SleepSpeedSq)) return 68;
            if (fieldName == nameof(BuoyancyTuningDTO.SleepForceThreshold)) return 72;
            if (fieldName == nameof(BuoyancyTuningDTO.DensityDepthCoefficient)) return 76;
            if (fieldName == nameof(BuoyancyTuningDTO.SeafloorAUPY)) return 80;
            if (fieldName == nameof(BuoyancyTuningDTO.GlobalQualityWeight)) return 84;
            if (fieldName == nameof(BuoyancyTuningDTO.SimulationTickDelta)) return 88;
            if (fieldName == nameof(BuoyancyTuningDTO.ActiveStateCount)) return 92;
            if (fieldName == nameof(BuoyancyTuningDTO.FlowForceCoefficient)) return 96;
            if (fieldName == nameof(BuoyancyTuningDTO.SurfaceSnapDepthMeters)) return 100;
            if (fieldName == nameof(BuoyancyTuningDTO.MinFluidDensityKgPerM3)) return 104;
            if (fieldName == nameof(BuoyancyTuningDTO.MaxFluidDensityKgPerM3)) return 108;
            if (fieldName == nameof(BuoyancyTuningDTO.MockStateCount)) return 112;
            if (fieldName == nameof(BuoyancyTuningDTO.FrameIndex)) return 116;
            if (fieldName == nameof(BuoyancyTuningDTO.Flags)) return 120;
            if (fieldName == nameof(BuoyancyTuningDTO.ResolvedQualityWeight)) return 124;
            return -1;
        }

        private static int OffsetOfForcePacket(string fieldName)
        {
            if (fieldName == nameof(BuoyancyForcePacketDTO.CurrentAUP)) return 0;
            if (fieldName == nameof(BuoyancyForcePacketDTO.NetForce)) return 24;
            if (fieldName == nameof(BuoyancyForcePacketDTO.BuoyantForce)) return 36;
            if (fieldName == nameof(BuoyancyForcePacketDTO.GravityForce)) return 48;
            if (fieldName == nameof(BuoyancyForcePacketDTO.DragForce)) return 60;
            if (fieldName == nameof(BuoyancyForcePacketDTO.FlowForce)) return 72;
            if (fieldName == nameof(BuoyancyForcePacketDTO.SubmergedFraction)) return 84;
            if (fieldName == nameof(BuoyancyForcePacketDTO.DepthMeters)) return 88;
            if (fieldName == nameof(BuoyancyForcePacketDTO.FluidDensityKgPerM3)) return 92;
            if (fieldName == nameof(BuoyancyForcePacketDTO.EntityHashID)) return 96;
            if (fieldName == nameof(BuoyancyForcePacketDTO.Flags)) return 100;
            if (fieldName == nameof(BuoyancyForcePacketDTO.StateIndex)) return 104;
            if (fieldName == nameof(BuoyancyForcePacketDTO.FrameIndex)) return 108;
            if (fieldName == nameof(BuoyancyForcePacketDTO.DebugVelocity)) return 112;
            if (fieldName == nameof(BuoyancyForcePacketDTO._pad0)) return 124;
            return -1;
        }

        private static int OffsetOfFlowSample(string fieldName)
        {
            if (fieldName == nameof(BuoyancyFlowSampleDTO.SampleAUP)) return 0;
            if (fieldName == nameof(BuoyancyFlowSampleDTO.FlowVelocity)) return 24;
            if (fieldName == nameof(BuoyancyFlowSampleDTO.RadiusMeters)) return 36;
            if (fieldName == nameof(BuoyancyFlowSampleDTO.CellHash)) return 40;
            if (fieldName == nameof(BuoyancyFlowSampleDTO.Flags)) return 44;
            if (fieldName == nameof(BuoyancyFlowSampleDTO._pad0)) return 48;
            if (fieldName == nameof(BuoyancyFlowSampleDTO._pad1)) return 56;
            return -1;
        }

        private static int OffsetOfTelemetry(string fieldName)
        {
            if (fieldName == nameof(BuoyancyTelemetryEntry.FrameIndex)) return 0;
            if (fieldName == nameof(BuoyancyTelemetryEntry.EvaluatedObjects)) return 4;
            if (fieldName == nameof(BuoyancyTelemetryEntry.SleepingObjects)) return 8;
            if (fieldName == nameof(BuoyancyTelemetryEntry.ForcePackets)) return 12;
            if (fieldName == nameof(BuoyancyTelemetryEntry.TotalBuoyantForce)) return 16;
            if (fieldName == nameof(BuoyancyTelemetryEntry.TotalDragForce)) return 20;
            if (fieldName == nameof(BuoyancyTelemetryEntry.ComputeMicros)) return 24;
            if (fieldName == nameof(BuoyancyTelemetryEntry.GlobalQualityWeight)) return 28;
            if (fieldName == nameof(BuoyancyTelemetryEntry.Flags)) return 32;
            if (fieldName == nameof(BuoyancyTelemetryEntry.NonFiniteCount)) return 36;
            if (fieldName == nameof(BuoyancyTelemetryEntry.LastEntityHashID)) return 40;
            if (fieldName == nameof(BuoyancyTelemetryEntry.MaxDepthMeters)) return 44;
            if (fieldName == nameof(BuoyancyTelemetryEntry.LastNetForce)) return 48;
            if (fieldName == nameof(BuoyancyTelemetryEntry._pad0)) return 60;
            return -1;
        }

        private static int OffsetOfSleepSdfConfig(string fieldName)
        {
            if (fieldName == nameof(BuoyancySleepSdfConfigDTO.SdfOriginAUP)) return 0;
            if (fieldName == nameof(BuoyancySleepSdfConfigDTO.CellSizeMeters)) return 24;
            if (fieldName == nameof(BuoyancySleepSdfConfigDTO.DensityDecodeScale)) return 28;
            if (fieldName == nameof(BuoyancySleepSdfConfigDTO.ContactEpsilonMeters)) return 32;
            if (fieldName == nameof(BuoyancySleepSdfConfigDTO.AmbientStirThresholdSq)) return 36;
            if (fieldName == nameof(BuoyancySleepSdfConfigDTO.Width)) return 40;
            if (fieldName == nameof(BuoyancySleepSdfConfigDTO.Height)) return 44;
            if (fieldName == nameof(BuoyancySleepSdfConfigDTO.Depth)) return 48;
            if (fieldName == nameof(BuoyancySleepSdfConfigDTO.StrideY)) return 52;
            if (fieldName == nameof(BuoyancySleepSdfConfigDTO.StrideZ)) return 56;
            if (fieldName == nameof(BuoyancySleepSdfConfigDTO.Flags)) return 60;
            return -1;
        }

        private static int OffsetOfSleepTelemetry(string fieldName)
        {
            if (fieldName == nameof(SleepStateTelemetryEntry.FrameIndex)) return 0;
            if (fieldName == nameof(SleepStateTelemetryEntry.ActiveObjects)) return 4;
            if (fieldName == nameof(SleepStateTelemetryEntry.SleepingObjects)) return 8;
            if (fieldName == nameof(SleepStateTelemetryEntry.ForcedAwakeObjects)) return 12;
            if (fieldName == nameof(SleepStateTelemetryEntry.StaticPromotionCandidates)) return 16;
            if (fieldName == nameof(SleepStateTelemetryEntry.NonFiniteCount)) return 20;
            if (fieldName == nameof(SleepStateTelemetryEntry.ComputeMicros)) return 24;
            if (fieldName == nameof(SleepStateTelemetryEntry.GlobalQualityWeight)) return 28;
            if (fieldName == nameof(SleepStateTelemetryEntry.SleepEnergyThreshold)) return 32;
            if (fieldName == nameof(SleepStateTelemetryEntry.SdfGroundedObjects)) return 36;
            if (fieldName == nameof(SleepStateTelemetryEntry.WakeRequestCount)) return 40;
            if (fieldName == nameof(SleepStateTelemetryEntry.AmbientCurrentWakes)) return 44;
            if (fieldName == nameof(SleepStateTelemetryEntry.Flags)) return 48;
            if (fieldName == nameof(SleepStateTelemetryEntry.LastEntityHashID)) return 52;
            if (fieldName == nameof(SleepStateTelemetryEntry.MaxEnergy)) return 56;
            if (fieldName == nameof(SleepStateTelemetryEntry._pad0)) return 60;
            return -1;
        }

        private static int OffsetOfMaterialVolume(string fieldName)
        {
            if (fieldName == nameof(BuoyancyMaterialVolumeDTO.ItemHash)) return 0;
            if (fieldName == nameof(BuoyancyMaterialVolumeDTO.MassKg)) return 4;
            if (fieldName == nameof(BuoyancyMaterialVolumeDTO.VolumeCubicMeters)) return 8;
            if (fieldName == nameof(BuoyancyMaterialVolumeDTO.HeightHintMeters)) return 12;
            if (fieldName == nameof(BuoyancyMaterialVolumeDTO.Flags)) return 16;
            if (fieldName == nameof(BuoyancyMaterialVolumeDTO._pad0)) return 20;
            if (fieldName == nameof(BuoyancyMaterialVolumeDTO._pad1)) return 24;
            return -1;
        }

        private static int OffsetOfMaterialSettling(string fieldName)
        {
            if (fieldName == nameof(BuoyancyMaterialSettlingProfileDTO.MaterialHash)) return 0;
            if (fieldName == nameof(BuoyancyMaterialSettlingProfileDTO.LinearSleepSpeedSq)) return 4;
            if (fieldName == nameof(BuoyancyMaterialSettlingProfileDTO.AngularSleepSpeedSq)) return 8;
            if (fieldName == nameof(BuoyancyMaterialSettlingProfileDTO.ForceSleepThresholdSq)) return 12;
            if (fieldName == nameof(BuoyancyMaterialSettlingProfileDTO.RequiredRestFrames)) return 16;
            if (fieldName == nameof(BuoyancyMaterialSettlingProfileDTO.DeepSleepTicks)) return 18;
            if (fieldName == nameof(BuoyancyMaterialSettlingProfileDTO.Flags)) return 20;
            if (fieldName == nameof(BuoyancyMaterialSettlingProfileDTO._pad0)) return 24;
            return -1;
        }

        private static int OffsetOfCounter(string fieldName)
        {
            if (fieldName == nameof(BuoyancyCounterDTO.EvaluatedObjects)) return 0;
            if (fieldName == nameof(BuoyancyCounterDTO.SleepingObjects)) return 4;
            if (fieldName == nameof(BuoyancyCounterDTO.ForcePackets)) return 8;
            if (fieldName == nameof(BuoyancyCounterDTO.NonFiniteCount)) return 12;
            if (fieldName == nameof(BuoyancyCounterDTO.TotalBuoyantForce)) return 16;
            if (fieldName == nameof(BuoyancyCounterDTO.TotalDragForce)) return 20;
            if (fieldName == nameof(BuoyancyCounterDTO.MaxDepthMeters)) return 24;
            if (fieldName == nameof(BuoyancyCounterDTO.Flags)) return 28;
            if (fieldName == nameof(BuoyancyCounterDTO.LastEntityHashID)) return 32;
            if (fieldName == nameof(BuoyancyCounterDTO.ComputeMicros)) return 36;
            if (fieldName == nameof(BuoyancyCounterDTO.ForcedAwakeObjects)) return 40;
            if (fieldName == nameof(BuoyancyCounterDTO.StaticPromotionCandidates)) return 44;
            if (fieldName == nameof(BuoyancyCounterDTO.AmbientCurrentWakes)) return 48;
            if (fieldName == nameof(BuoyancyCounterDTO.SdfGroundedObjects)) return 52;
            if (fieldName == nameof(BuoyancyCounterDTO._pad2)) return 56;
            return -1;
        }

        private static int OffsetOfDebugForce(string fieldName)
        {
            if (fieldName == nameof(BuoyancyDebugForceDTO.CurrentAUP)) return 0;
            if (fieldName == nameof(BuoyancyDebugForceDTO.BuoyantForce)) return 24;
            if (fieldName == nameof(BuoyancyDebugForceDTO.GravityForce)) return 36;
            if (fieldName == nameof(BuoyancyDebugForceDTO.DragForce)) return 48;
            if (fieldName == nameof(BuoyancyDebugForceDTO.NetForce)) return 60;
            if (fieldName == nameof(BuoyancyDebugForceDTO.FlowForce)) return 72;
            if (fieldName == nameof(BuoyancyDebugForceDTO.SubmergedFraction)) return 84;
            if (fieldName == nameof(BuoyancyDebugForceDTO.DepthMeters)) return 88;
            if (fieldName == nameof(BuoyancyDebugForceDTO.EntityHashID)) return 92;
            if (fieldName == nameof(BuoyancyDebugForceDTO.Flags)) return 96;
            if (fieldName == nameof(BuoyancyDebugForceDTO.StateIndex)) return 100;
            if (fieldName == nameof(BuoyancyDebugForceDTO.FrameIndex)) return 104;
            if (fieldName == nameof(BuoyancyDebugForceDTO.Velocity)) return 108;
            if (fieldName == nameof(BuoyancyDebugForceDTO.SleepScore)) return 120;
            if (fieldName == nameof(BuoyancyDebugForceDTO._pad0)) return 124;
            return -1;
        }

        private static int OffsetOfBodyBinding(string fieldName)
        {
            if (fieldName == nameof(BuoyancyBodyBindingDTO.EntityHashID)) return 0;
            if (fieldName == nameof(BuoyancyBodyBindingDTO.StateIndex)) return 4;
            if (fieldName == nameof(BuoyancyBodyBindingDTO.RigidbodyIndex)) return 8;
            if (fieldName == nameof(BuoyancyBodyBindingDTO.Flags)) return 12;
            if (fieldName == nameof(BuoyancyBodyBindingDTO._pad0)) return 16;
            if (fieldName == nameof(BuoyancyBodyBindingDTO._pad1)) return 24;
            return -1;
        }
    }

    public static class BuoyancyMaterialVolumeCsvParser
    {
        private const byte Comma = (byte)',';
        private const byte CarriageReturn = (byte)'\r';
        private const byte LineFeed = (byte)'\n';
        private const byte Hash = (byte)'#';
        private const byte Space = (byte)' ';
        private const byte Tab = (byte)'\t';

        public static bool TryApply(ReadOnlySpan<byte> csv, NativeArray<BuoyancyMaterialVolumeDTO> table, out int rowsWritten)
        {
            rowsWritten = 0;
            if (csv.Length <= 0 || !table.IsCreated || table.Length <= 0)
                return false;

            ClearTable(table);
            int cursor = 0;
            while (cursor < csv.Length)
            {
                int lineStart = cursor;
                while (cursor < csv.Length && csv[cursor] != LineFeed)
                    cursor++;

                int lineEnd = cursor;
                if (cursor < csv.Length && csv[cursor] == LineFeed)
                    cursor++;
                if (lineEnd > lineStart && csv[lineEnd - 1] == CarriageReturn)
                    lineEnd--;

                if (TryParseLine(csv.Slice(lineStart, lineEnd - lineStart), out BuoyancyMaterialVolumeDTO row) &&
                    Insert(table, row))
                {
                    rowsWritten++;
                }
            }

            return rowsWritten > 0;
        }

        private static void ClearTable(NativeArray<BuoyancyMaterialVolumeDTO> table)
        {
            for (int i = 0; i < table.Length; i++)
                table[i] = default;
        }

        private static bool TryParseLine(ReadOnlySpan<byte> line, out BuoyancyMaterialVolumeDTO row)
        {
            row = default;
            line = Trim(line);
            if (line.Length <= 0 || line[0] == Hash)
                return false;

            int c0 = IndexOf(line, Comma, 0);
            if (c0 <= 0)
                return false;
            ReadOnlySpan<byte> key = Trim(line.Slice(0, c0));
            if (key.Length <= 0 || EqualsAscii(key, "item") || EqualsAscii(key, "name"))
                return false;

            int c1 = IndexOf(line, Comma, c0 + 1);
            if (c1 <= c0)
                return false;

            int c2 = IndexOf(line, Comma, c1 + 1);
            ReadOnlySpan<byte> massSpan = Trim(line.Slice(c0 + 1, c1 - c0 - 1));
            ReadOnlySpan<byte> volumeSpan = c2 > c1
                ? Trim(line.Slice(c1 + 1, c2 - c1 - 1))
                : Trim(line.Slice(c1 + 1));
            ReadOnlySpan<byte> heightSpan = c2 > c1 ? Trim(line.Slice(c2 + 1)) : ReadOnlySpan<byte>.Empty;

            if (!TryParseFloat(massSpan, out float massKg) ||
                !TryParseFloat(volumeSpan, out float volumeM3) ||
                !math.isfinite(massKg) ||
                !math.isfinite(volumeM3) ||
                massKg <= BuoyancyDisplacementConstants.Epsilon ||
                volumeM3 <= BuoyancyDisplacementConstants.Epsilon)
            {
                return false;
            }

            float heightHint = 0f;
            if (heightSpan.Length > 0 && TryParseFloat(heightSpan, out float parsedHeight) && math.isfinite(parsedHeight))
                heightHint = math.max(0f, parsedHeight);

            row.ItemHash = Fnv1A32(key);
            row.MassKg = massKg;
            row.VolumeCubicMeters = volumeM3;
            row.HeightHintMeters = heightHint;
            row.Flags = BuoyancyDisplacementConstants.FlagActive;
            return row.ItemHash != 0u;
        }

        private static bool Insert(NativeArray<BuoyancyMaterialVolumeDTO> table, BuoyancyMaterialVolumeDTO row)
        {
            int length = table.Length;
            int slot = (int)(row.ItemHash % (uint)length);
            for (int probe = 0; probe < length; probe++)
            {
                int index = (slot + probe) % length;
                BuoyancyMaterialVolumeDTO current = table[index];
                if (current.ItemHash == 0u || current.ItemHash == row.ItemHash)
                {
                    table[index] = row;
                    return true;
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && IsWhitespace(value[start]))
                start++;
            while (end >= start && IsWhitespace(value[end]))
                end--;
            return start > end ? ReadOnlySpan<byte>.Empty : value.Slice(start, end - start + 1);
        }

        private static int IndexOf(ReadOnlySpan<byte> value, byte target, int start)
        {
            for (int i = math.max(0, start); i < value.Length; i++)
            {
                if (value[i] == target)
                    return i;
            }

            return -1;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> span, out float value)
        {
            value = 0f;
            span = Trim(span);
            if (span.Length <= 0)
                return false;

            int index = 0;
            float sign = 1f;
            if (span[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }
            else if (span[index] == (byte)'+')
            {
                index++;
            }

            float integer = 0f;
            bool hasDigit = false;
            while (index < span.Length && span[index] >= (byte)'0' && span[index] <= (byte)'9')
            {
                integer = (integer * 10f) + (span[index] - (byte)'0');
                index++;
                hasDigit = true;
            }

            float fraction = 0f;
            float divisor = 1f;
            if (index < span.Length && span[index] == (byte)'.')
            {
                index++;
                while (index < span.Length && span[index] >= (byte)'0' && span[index] <= (byte)'9')
                {
                    fraction = (fraction * 10f) + (span[index] - (byte)'0');
                    divisor *= 10f;
                    index++;
                    hasDigit = true;
                }
            }

            if (!hasDigit)
                return false;

            value = sign * (integer + fraction * math.rcp(math.max(divisor, 1f)));
            return math.isfinite(value);
        }

        private static uint Fnv1A32(ReadOnlySpan<byte> span)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < span.Length; i++)
            {
                byte c = span[i];
                if (c >= (byte)'A' && c <= (byte)'Z')
                    c = (byte)(c + 32);
                hash ^= c;
                hash *= 16777619u;
            }

            return hash == 0u ? 1u : hash;
        }

        private static bool EqualsAscii(ReadOnlySpan<byte> span, string text)
        {
            if (span.Length != text.Length)
                return false;

            for (int i = 0; i < span.Length; i++)
            {
                byte a = span[i];
                if (a >= (byte)'A' && a <= (byte)'Z')
                    a = (byte)(a + 32);
                if (a != (byte)text[i])
                    return false;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsWhitespace(byte value)
        {
            return value == Space || value == Tab || value == CarriageReturn || value == LineFeed;
        }
    }

    public static class BuoyancyMaterialSettlingProfileCsvParser
    {
        private const byte Comma = (byte)',';
        private const byte CarriageReturn = (byte)'\r';
        private const byte LineFeed = (byte)'\n';
        private const byte Hash = (byte)'#';
        private const byte Space = (byte)' ';
        private const byte Tab = (byte)'\t';

        public static bool TryApply(ReadOnlySpan<byte> csv, NativeArray<BuoyancyMaterialSettlingProfileDTO> table, out int rowsWritten)
        {
            rowsWritten = 0;
            if (csv.Length <= 0 || !table.IsCreated || table.Length <= 0)
                return false;

            ClearTable(table);
            int cursor = 0;
            while (cursor < csv.Length)
            {
                int lineStart = cursor;
                while (cursor < csv.Length && csv[cursor] != LineFeed)
                    cursor++;

                int lineEnd = cursor;
                if (cursor < csv.Length && csv[cursor] == LineFeed)
                    cursor++;
                if (lineEnd > lineStart && csv[lineEnd - 1] == CarriageReturn)
                    lineEnd--;

                if (TryParseLine(csv.Slice(lineStart, lineEnd - lineStart), out BuoyancyMaterialSettlingProfileDTO row) &&
                    Insert(table, row))
                {
                    rowsWritten++;
                }
            }

            return rowsWritten > 0;
        }

        private static void ClearTable(NativeArray<BuoyancyMaterialSettlingProfileDTO> table)
        {
            for (int i = 0; i < table.Length; i++)
                table[i] = default;
        }

        private static bool TryParseLine(ReadOnlySpan<byte> line, out BuoyancyMaterialSettlingProfileDTO row)
        {
            row = default;
            line = Trim(line);
            if (line.Length <= 0 || line[0] == Hash)
                return false;

            int c0 = IndexOf(line, Comma, 0);
            if (c0 <= 0)
                return false;

            ReadOnlySpan<byte> key = Trim(line.Slice(0, c0));
            if (key.Length <= 0 || EqualsAscii(key, "material") || EqualsAscii(key, "name"))
                return false;

            int c1 = IndexOf(line, Comma, c0 + 1);
            int c2 = c1 > c0 ? IndexOf(line, Comma, c1 + 1) : -1;
            int c3 = c2 > c1 ? IndexOf(line, Comma, c2 + 1) : -1;
            int c4 = c3 > c2 ? IndexOf(line, Comma, c3 + 1) : -1;

            if (c1 <= c0 || c2 <= c1)
                return false;

            ReadOnlySpan<byte> linearSpan = Trim(line.Slice(c0 + 1, c1 - c0 - 1));
            ReadOnlySpan<byte> angularSpan = Trim(line.Slice(c1 + 1, c2 - c1 - 1));
            ReadOnlySpan<byte> forceSpan = c3 > c2
                ? Trim(line.Slice(c2 + 1, c3 - c2 - 1))
                : Trim(line.Slice(c2 + 1));
            ReadOnlySpan<byte> restSpan = c3 > c2
                ? (c4 > c3 ? Trim(line.Slice(c3 + 1, c4 - c3 - 1)) : Trim(line.Slice(c3 + 1)))
                : ReadOnlySpan<byte>.Empty;
            ReadOnlySpan<byte> deepSpan = c4 > c3 ? Trim(line.Slice(c4 + 1)) : ReadOnlySpan<byte>.Empty;

            if (!TryParseFloat(linearSpan, out float linearSleepSpeedSq) ||
                !TryParseFloat(angularSpan, out float angularSleepSpeedSq) ||
                !TryParseFloat(forceSpan, out float forceSleepThresholdSq) ||
                !math.isfinite(linearSleepSpeedSq) ||
                !math.isfinite(angularSleepSpeedSq) ||
                !math.isfinite(forceSleepThresholdSq))
            {
                return false;
            }

            ushort restFrames = 6;
            ushort deepTicks = 180;
            if (restSpan.Length > 0 && TryParseUnsigned(restSpan, out ushort parsedRest))
                restFrames = parsedRest;
            if (deepSpan.Length > 0 && TryParseUnsigned(deepSpan, out ushort parsedDeep))
                deepTicks = parsedDeep;

            row.MaterialHash = Fnv1A32(key);
            row.LinearSleepSpeedSq = math.max(0.000001f, linearSleepSpeedSq);
            row.AngularSleepSpeedSq = math.max(0.000001f, angularSleepSpeedSq);
            row.ForceSleepThresholdSq = math.max(0.000001f, forceSleepThresholdSq);
            row.RequiredRestFrames = (ushort)math.clamp((int)restFrames, 1, 255);
            row.DeepSleepTicks = (ushort)math.clamp((int)deepTicks, 1, ushort.MaxValue);
            row.Flags = BuoyancyDisplacementConstants.FlagActive;
            return row.MaterialHash != 0u;
        }

        private static bool Insert(NativeArray<BuoyancyMaterialSettlingProfileDTO> table, BuoyancyMaterialSettlingProfileDTO row)
        {
            int length = table.Length;
            int slot = (int)(row.MaterialHash % (uint)length);
            for (int probe = 0; probe < length; probe++)
            {
                int index = (slot + probe) % length;
                BuoyancyMaterialSettlingProfileDTO current = table[index];
                if (current.MaterialHash == 0u || current.MaterialHash == row.MaterialHash)
                {
                    table[index] = row;
                    return true;
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && IsWhitespace(value[start]))
                start++;
            while (end >= start && IsWhitespace(value[end]))
                end--;
            return start > end ? ReadOnlySpan<byte>.Empty : value.Slice(start, end - start + 1);
        }

        private static int IndexOf(ReadOnlySpan<byte> value, byte target, int start)
        {
            for (int i = math.max(0, start); i < value.Length; i++)
            {
                if (value[i] == target)
                    return i;
            }

            return -1;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> span, out float value)
        {
            value = 0f;
            span = Trim(span);
            if (span.Length <= 0)
                return false;

            int index = 0;
            float sign = 1f;
            if (span[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }
            else if (span[index] == (byte)'+')
            {
                index++;
            }

            float integer = 0f;
            bool hasDigit = false;
            while (index < span.Length && span[index] >= (byte)'0' && span[index] <= (byte)'9')
            {
                integer = (integer * 10f) + (span[index] - (byte)'0');
                index++;
                hasDigit = true;
            }

            float fraction = 0f;
            float divisor = 1f;
            if (index < span.Length && span[index] == (byte)'.')
            {
                index++;
                while (index < span.Length && span[index] >= (byte)'0' && span[index] <= (byte)'9')
                {
                    fraction = (fraction * 10f) + (span[index] - (byte)'0');
                    divisor *= 10f;
                    index++;
                    hasDigit = true;
                }
            }

            if (!hasDigit)
                return false;

            value = sign * (integer + fraction * math.rcp(math.max(divisor, 1f)));
            return math.isfinite(value);
        }

        private static bool TryParseUnsigned(ReadOnlySpan<byte> span, out ushort value)
        {
            value = 0;
            span = Trim(span);
            if (span.Length <= 0)
                return false;

            uint parsed = 0u;
            for (int i = 0; i < span.Length; i++)
            {
                byte c = span[i];
                if (c < (byte)'0' || c > (byte)'9')
                    return false;
                parsed = (parsed * 10u) + (uint)(c - (byte)'0');
                if (parsed > ushort.MaxValue)
                    parsed = ushort.MaxValue;
            }

            value = (ushort)parsed;
            return true;
        }

        private static uint Fnv1A32(ReadOnlySpan<byte> span)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < span.Length; i++)
            {
                byte c = span[i];
                if (c >= (byte)'A' && c <= (byte)'Z')
                    c = (byte)(c + 32);
                hash ^= c;
                hash *= 16777619u;
            }

            return hash == 0u ? 1u : hash;
        }

        private static bool EqualsAscii(ReadOnlySpan<byte> span, string text)
        {
            if (span.Length != text.Length)
                return false;

            for (int i = 0; i < span.Length; i++)
            {
                byte a = span[i];
                if (a >= (byte)'A' && a <= (byte)'Z')
                    a = (byte)(a + 32);
                if (a != (byte)text[i])
                    return false;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsWhitespace(byte value)
        {
            return value == Space || value == Tab || value == CarriageReturn || value == LineFeed;
        }
    }
}
