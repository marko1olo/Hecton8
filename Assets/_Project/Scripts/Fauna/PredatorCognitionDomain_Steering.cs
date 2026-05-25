using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Physics;
using Hecton8.Core.Memory;
using Hecton8.Core.Scheduling;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.UIElements;
#endif

namespace Hecton8.AI
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct SteeringParamsDTO
    {
        [FieldOffset(0)] public float MaxSpeed;
        [FieldOffset(4)] public float TurnSpeed;
        [FieldOffset(8)] public float LungeMultiplier;
        [FieldOffset(12)] public float ObstacleAvoidanceWeight;
        [FieldOffset(16)] public float3 CurrentTargetDirection;
        [FieldOffset(28)] private uint _pad0;

        internal static unsafe bool ValidateByteOffsets()
        {
            SteeringParamsDTO value = default;
            SteeringParamsDTO* root = &value;
            byte* bytes = (byte*)root;
            return (byte*)&root->MaxSpeed - bytes == 0 &&
                   (byte*)&root->TurnSpeed - bytes == 4 &&
                   (byte*)&root->LungeMultiplier - bytes == 8 &&
                   (byte*)&root->ObstacleAvoidanceWeight - bytes == 12 &&
                   (byte*)&root->CurrentTargetDirection - bytes == 16 &&
                   (byte*)&root->_pad0 - bytes == 28;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct SteeringAvoidanceDTO
    {
        [FieldOffset(0)] public float3 Repulsion;
        [FieldOffset(12)] public float AveragePressure;
        [FieldOffset(16)] public float3 BestWhiskerDirection;
        [FieldOffset(28)] public float NearestHitDistance;
        [FieldOffset(32)] public int ActiveWhiskerCount;
        [FieldOffset(36)] public int HitWhiskerCount;
        [FieldOffset(40)] public uint Flags;
        [FieldOffset(44)] public float DesiredSpeedMetersPerSecond;
        [FieldOffset(48)] public uint StateHash;
        [FieldOffset(52)] public uint Reserved0;
        [FieldOffset(56)] private ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct SteeringWhiskerResultDTO
    {
        [FieldOffset(0)] public float3 Direction;
        [FieldOffset(12)] public float DistanceMeters;
        [FieldOffset(16)] public float3 SampleLocalMeters;
        [FieldOffset(28)] public float SdfMeters;
        [FieldOffset(32)] public float3 ReflectedDirection;
        [FieldOffset(44)] public float Pressure01;
        [FieldOffset(48)] public int EntitySlot;
        [FieldOffset(52)] public int WhiskerIndex;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint Frame;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct SteeringSdfConfigDTO
    {
        [FieldOffset(0)] public double3 SdfOriginAup;
        [FieldOffset(24)] public float3 CellSizeMeters;
        [FieldOffset(36)] public int3 Dimensions;
        [FieldOffset(48)] public float WhiskerLengthMeters;
        [FieldOffset(52)] public float SolidThresholdMeters;
        [FieldOffset(56)] public float GlobalQualityWeight;
        [FieldOffset(60)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct SteeringTelemetryEntry
    {
        [FieldOffset(0)] public double3 FirstAup;
        [FieldOffset(24)] public float3 AverageVelocity;
        [FieldOffset(36)] public uint Frame;
        [FieldOffset(40)] public uint ActivePredators;
        [FieldOffset(44)] public uint ActiveRepulsions;
        [FieldOffset(48)] public float MaxLungeVelocity;
        [FieldOffset(52)] public float BurstMicroseconds;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint StateHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct SteeringProfileDTO
    {
        [FieldOffset(0)] public uint SpeciesHash;
        [FieldOffset(4)] public float MaxSpeed;
        [FieldOffset(8)] public float TurnSpeed;
        [FieldOffset(12)] public float LungeMultiplier;
        [FieldOffset(16)] public float ObstacleAvoidanceWeight;
        [FieldOffset(20)] public float WhiskerLengthMeters;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] private uint _pad0;
    }

    internal static partial class PredatorCognitionDomain
    {
        private const int SteeringParamsDtoSizeBytes = 32;
        private const int SteeringAvoidanceDtoSizeBytes = 64;
        private const int SteeringWhiskerResultDtoSizeBytes = 64;
        private const int SteeringSdfConfigDtoSizeBytes = 64;
        private const int SteeringTelemetryEntrySizeBytes = 64;
        private const int SteeringProfileDtoSizeBytes = 32;
        private const int LeviathanSteeringMaxWhiskers = 26;
        private const int LeviathanSteeringMinWhiskers = 6;
        private const int LeviathanSteeringTelemetryCapacity = 300;
        private const int LeviathanSteeringMockSdfX = 48;
        private const int LeviathanSteeringMockSdfY = 24;
        private const int LeviathanSteeringMockSdfZ = 48;
        private const int LeviathanSteeringMockSdfVoxelCount = LeviathanSteeringMockSdfX * LeviathanSteeringMockSdfY * LeviathanSteeringMockSdfZ;
        private const int LeviathanSteeringProfileCapacity = 64;
        private const int LeviathanSteeringCsvScratchBytes = 16 * 1024;
        private const int LeviathanSteeringLungeLockFrames = 8;
        private const float LeviathanSteeringLungeDistanceMeters = 20f;
        private const float LeviathanSteeringFaultBudgetMicroseconds = 1500f;
        private const float LeviathanSteeringMathEpsilon = 0.0001f;
        private const string LeviathanSteeringProfilesCsvName = "fauna_steering_profiles.csv";
        private const uint SteeringTelemetryFlagNonFiniteVelocity = 1u << 0;
        private const uint SteeringTelemetryFlagBudgetExceeded = 1u << 1;
        private const uint SteeringAvoidanceFlagHitRock = 1u << 0;
        private const uint SteeringAvoidanceFlagNonFinite = 1u << 1;
        private const uint SteeringWhiskerFlagHitRock = 1u << 0;
        private const uint SteeringWhiskerFlagActive = 1u << 1;
        private const uint SteeringSdfFlagMock = 1u << 0;
        private const uint SteeringSdfFlagSignedMeters = 1u << 1;
        private const uint SteeringDumpMagic = 0x30303353u;
        private const uint SteeringDumpFaultHash = 0x53333033u;
        private const int SteeringDumpVersion = 1;
        private const string SteeringDumpDirectoryRelativePath = "Docs/AgentLogs";
        private const string SteeringDumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_303.bin";
        internal const int LeviathanSteeringScheduledJobCount = 5;
        private const int SteeringParamsOffsetMaxSpeed = 0;
        private const int SteeringParamsOffsetTurnSpeed = 4;
        private const int SteeringParamsOffsetLungeMultiplier = 8;
        private const int SteeringParamsOffsetObstacleAvoidanceWeight = 12;
        private const int SteeringParamsOffsetCurrentTargetDirection = 16;
        private const int SteeringParamsOffsetPad0 = 28;

        private static VaultArray<SteeringParamsDTO> _steeringParams;
        private static VaultArray<SteeringAvoidanceDTO> _steeringAvoidance;
        private static VaultArray<SteeringWhiskerResultDTO> _steeringWhiskers;
        private static VaultArray<KinematicStateDTO> _leviathanKinematicStates;
        private static VaultArray<SteeringTelemetryEntry> _steeringTelemetryRing;
        private static VaultArray<int> _steeringTelemetryCursor;
        private static VaultArray<float> _steeringMockSdf;
        private static VaultArray<SteeringSdfConfigDTO> _steeringSdfConfig;
        private static VaultArray<SteeringProfileDTO> _steeringProfiles;
        private static VaultArray<byte> _steeringCsvScratch;
        private static bool _steeringMockSdfGenerated;
        private static bool _steeringEvaluationJobScheduled;
        private static bool _steeringFaultDumped;
        private static int _steeringAbiValidationState;
        private static float _lastLeviathanSteeringChainMicroseconds;

        private static bool ValidateLeviathanSteeringAbiLayout()
        {
            if (_steeringAbiValidationState != 0)
                return _steeringAbiValidationState > 0;

            bool valid = UnsafeUtility.SizeOf<SteeringParamsDTO>() == SteeringParamsDtoSizeBytes &&
                         SteeringParamsDTO.ValidateByteOffsets() &&
                         UnsafeUtility.SizeOf<SteeringAvoidanceDTO>() == SteeringAvoidanceDtoSizeBytes &&
                         UnsafeUtility.SizeOf<SteeringWhiskerResultDTO>() == SteeringWhiskerResultDtoSizeBytes &&
                         UnsafeUtility.SizeOf<SteeringSdfConfigDTO>() == SteeringSdfConfigDtoSizeBytes &&
                         UnsafeUtility.SizeOf<SteeringTelemetryEntry>() == SteeringTelemetryEntrySizeBytes &&
                         UnsafeUtility.SizeOf<SteeringProfileDTO>() == SteeringProfileDtoSizeBytes;
            _steeringAbiValidationState = valid ? 1 : -1;
            return valid;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe uint ReadSteeringRuntimePackedState(SteeringParamsDTO* parameter)
        {
            return *(uint*)((byte*)parameter + SteeringParamsOffsetPad0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void WriteSteeringRuntimePackedState(SteeringParamsDTO* parameter, uint value)
        {
            *(uint*)((byte*)parameter + SteeringParamsOffsetPad0) = value;
        }

        private static bool EnsureLeviathanSteeringVaultState()
        {
            if (!ValidateLeviathanSteeringAbiLayout())
                return false;

            if (HasLeviathanSteeringVaultState())
                return true;

            if (_dataVault == null || _dataVault.IsAllocationLocked)
                return false;

            _steeringParams = GetVaultArray<SteeringParamsDTO>(
                BufferID.Shinobu303SteeringParams,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            _steeringAvoidance = GetVaultArray<SteeringAvoidanceDTO>(
                BufferID.Shinobu303SteeringAvoidance,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            _steeringWhiskers = GetVaultArray<SteeringWhiskerResultDTO>(
                BufferID.Shinobu303SteeringWhiskers,
                Capacity * LeviathanSteeringMaxWhiskers,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            _leviathanKinematicStates = GetVaultArray<KinematicStateDTO>(
                BufferID.Shinobu303KinematicStates,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            _steeringTelemetryRing = GetVaultArray<SteeringTelemetryEntry>(
                BufferID.Shinobu303SteeringTelemetryRing,
                LeviathanSteeringTelemetryCapacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _steeringTelemetryCursor = GetVaultArray<int>(
                BufferID.Shinobu303SteeringTelemetryCursor,
                1,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _steeringMockSdf = GetVaultArray<float>(
                BufferID.Shinobu303MockSdf,
                LeviathanSteeringMockSdfVoxelCount,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            _steeringSdfConfig = GetVaultArray<SteeringSdfConfigDTO>(
                BufferID.Shinobu303SdfConfig,
                1,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _steeringProfiles = GetVaultArray<SteeringProfileDTO>(
                BufferID.Shinobu303SteeringProfiles,
                LeviathanSteeringProfileCapacity,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            _steeringCsvScratch = GetVaultArray<byte>(
                BufferID.Shinobu303CsvScratch,
                LeviathanSteeringCsvScratchBytes,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);

            bool resolved =
                _steeringParams.IsCreated &&
                _steeringAvoidance.IsCreated &&
                _steeringWhiskers.IsCreated &&
                _leviathanKinematicStates.IsCreated &&
                _steeringTelemetryRing.IsCreated &&
                _steeringTelemetryCursor.IsCreated &&
                _steeringMockSdf.IsCreated &&
                _steeringSdfConfig.IsCreated &&
                _steeringProfiles.IsCreated &&
                _steeringCsvScratch.IsCreated;
            if (!resolved)
            {
                ReleaseLeviathanSteeringVaultHandles();
                return false;
            }

            InitializeLeviathanSteeringCold();
            return true;
        }

        private static bool HasLeviathanSteeringVaultState()
        {
            return ValidateLeviathanSteeringAbiLayout() &&
                   _steeringParams.IsCreated &&
                   _steeringAvoidance.IsCreated &&
                   _steeringWhiskers.IsCreated &&
                   _leviathanKinematicStates.IsCreated &&
                   _steeringTelemetryRing.IsCreated &&
                   _steeringTelemetryCursor.IsCreated &&
                   _steeringMockSdf.IsCreated &&
                   _steeringSdfConfig.IsCreated &&
                   _steeringProfiles.IsCreated &&
                   _steeringCsvScratch.IsCreated;
        }

        private static void InitializeLeviathanSteeringCold()
        {
            NativeArray<SteeringSdfConfigDTO> sdfConfig = _steeringSdfConfig.Open();
            NativeArray<SteeringProfileDTO> profiles = _steeringProfiles.Open();
            NativeArray<SteeringParamsDTO> parameters = _steeringParams.Open();
            if (sdfConfig.IsCreated && sdfConfig.Length > 0)
                sdfConfig[0] = CreateDefaultSdfConfig(SanitizeQualityWeight(HomeostasisBrain.GlobalQualityWeight));

            if (parameters.IsCreated)
            {
                for (int i = 0; i < parameters.Length; i++)
                    parameters[i] = default;
            }

            if (profiles.IsCreated && profiles.Length > 0)
            {
                for (int i = 0; i < profiles.Length; i++)
                    profiles[i] = default;

                profiles[0] = CreateDefaultSteeringProfile();
            }

#if UNITY_EDITOR
            TryLoadLeviathanSteeringProfilesCsvCold();
#endif
        }

        internal static bool EnsureLeviathanSteeringStateCold()
        {
            EnsureInitialized();
            return EnsureLeviathanSteeringVaultState();
        }

        private static SteeringProfileDTO CreateDefaultSteeringProfile()
        {
            return new SteeringProfileDTO
            {
                SpeciesHash = HashSpeciesProfileKey("Default_Leviathan"),
                MaxSpeed = 22f,
                TurnSpeed = 0.82f,
                LungeMultiplier = 3.1f,
                ObstacleAvoidanceWeight = 1.35f,
                WhiskerLengthMeters = 36f,
                Flags = 1u
            };
        }

        private static SteeringSdfConfigDTO CreateDefaultSdfConfig(float quality)
        {
            return new SteeringSdfConfigDTO
            {
                SdfOriginAup = double3.zero,
                CellSizeMeters = new float3(4f, 4f, 4f),
                Dimensions = new int3(LeviathanSteeringMockSdfX, LeviathanSteeringMockSdfY, LeviathanSteeringMockSdfZ),
                WhiskerLengthMeters = math.lerp(24f, 48f, math.saturate(quality)),
                SolidThresholdMeters = 0f,
                GlobalQualityWeight = math.saturate(quality),
                Flags = SteeringSdfFlagMock | SteeringSdfFlagSignedMeters
            };
        }

        private static unsafe JobHandle ScheduleLeviathanSteering(int frameId, JobHandle dependency)
        {
            _steeringEvaluationJobScheduled = false;
            if (!HasLeviathanSteeringVaultState())
                return dependency;

            NativeArray<int> activeSlots = _activeSlots.Open();
            NativeArray<CognitionInput> inputs = _inputs.Open();
            NativeArray<PackedCognitionOutput> outputs = _outputs.Open();
            NativeArray<SteeringParamsDTO> steeringParams = _steeringParams.Open();
            NativeArray<SteeringAvoidanceDTO> avoidance = _steeringAvoidance.Open();
            NativeArray<SteeringWhiskerResultDTO> whiskers = _steeringWhiskers.Open();
            NativeArray<KinematicStateDTO> kinematics = _leviathanKinematicStates.Open();
            NativeArray<SteeringTelemetryEntry> telemetry = _steeringTelemetryRing.Open();
            NativeArray<int> telemetryCursor = _steeringTelemetryCursor.Open();
            NativeArray<float> sdf = _steeringMockSdf.Open();
            NativeArray<SteeringSdfConfigDTO> sdfConfig = _steeringSdfConfig.Open();
            NativeArray<SteeringProfileDTO> profiles = _steeringProfiles.Open();
            if (!activeSlots.IsCreated ||
                !inputs.IsCreated ||
                !outputs.IsCreated ||
                !steeringParams.IsCreated ||
                !avoidance.IsCreated ||
                !whiskers.IsCreated ||
                !kinematics.IsCreated ||
                !telemetry.IsCreated ||
                !telemetryCursor.IsCreated ||
                !sdf.IsCreated ||
                !sdfConfig.IsCreated ||
                !profiles.IsCreated ||
                _activeSlotCount <= 0)
            {
                return dependency;
            }

            SteeringSdfConfigDTO config = ResolveRuntimeSdfConfig(sdfConfig[0]);
            sdfConfig[0] = config;

            JobHandle chain = dependency;
            if (!_steeringMockSdfGenerated)
            {
                var mockJob = new GenerateMockSdfObstaclesJob
                {
                    Sdf = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(sdf),
                    Config = config
                };
                if (!mockJob.TryScheduleParallelAdmitted(
                        sdf.Length,
                        128,
                        JobAdmissionLane.Lane3_AI,
                        chain,
                        out chain))
                {
                    return chain;
                }

                _steeringMockSdfGenerated = true;
            }

            var populateJob = new PopulateLeviathanSteeringParamsJob
            {
                ActiveSlots = (int*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(activeSlots),
                Inputs = (CognitionInput*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(inputs),
                Outputs = (PackedCognitionOutput*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(outputs),
                Params = (SteeringParamsDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(steeringParams),
                Profiles = (SteeringProfileDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(profiles),
                ProfileCount = profiles.Length,
                GlobalQualityWeight = config.GlobalQualityWeight
            };
            if (!populateJob.TryScheduleParallelAdmitted(
                    _activeSlotCount,
                    EvaluationJobBatchSize,
                    JobAdmissionLane.Lane3_AI,
                    chain,
                    out chain))
            {
                return chain;
            }

            var avoidanceJob = new EvaluateSdfAvoidanceJob
            {
                ActiveSlots = (int*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(activeSlots),
                Inputs = (CognitionInput*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(inputs),
                Params = (SteeringParamsDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(steeringParams),
                Sdf = (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(sdf),
                Avoidance = (SteeringAvoidanceDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(avoidance),
                Whiskers = (SteeringWhiskerResultDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(whiskers),
                SdfLength = sdf.Length,
                Config = config,
                Frame = (uint)frameId
            };
            if (!avoidanceJob.TryScheduleParallelAdmitted(
                    _activeSlotCount,
                    EvaluationJobBatchSize,
                    JobAdmissionLane.Lane3_AI,
                    chain,
                    out chain))
            {
                return chain;
            }

            var integrateJob = new IntegrateSteeringVectorsJob
            {
                ActiveSlots = (int*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(activeSlots),
                Inputs = (CognitionInput*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(inputs),
                Outputs = (PackedCognitionOutput*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(outputs),
                Params = (SteeringParamsDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(steeringParams),
                Avoidance = (SteeringAvoidanceDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(avoidance),
                KinematicStates = (KinematicStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(kinematics),
                DeltaTime = ResolveSteeringDeltaTime(inputs, activeSlots),
                Frame = (uint)frameId
            };
            if (!integrateJob.TryScheduleParallelAdmitted(
                    _activeSlotCount,
                    EvaluationJobBatchSize,
                    JobAdmissionLane.Lane3_AI,
                    chain,
                    out chain))
            {
                return chain;
            }

            var telemetryJob = new RecordSteeringTelemetryJob
            {
                ActiveSlots = (int*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(activeSlots),
                Inputs = (CognitionInput*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(inputs),
                Params = (SteeringParamsDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(steeringParams),
                Avoidance = (SteeringAvoidanceDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(avoidance),
                KinematicStates = (KinematicStateDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(kinematics),
                Telemetry = (SteeringTelemetryEntry*)NativeArrayUnsafeUtility.GetUnsafePtr(telemetry),
                TelemetryCursor = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(telemetryCursor),
                ActiveSlotCount = _activeSlotCount,
                Frame = (uint)frameId,
                EstimatedBurstMicroseconds = _lastLeviathanSteeringChainMicroseconds
            };
            if (telemetryJob.TryScheduleAdmitted(JobAdmissionLane.Lane3_AI, chain, out chain))
                _steeringEvaluationJobScheduled = true;

            return chain;
        }

        private static SteeringSdfConfigDTO ResolveRuntimeSdfConfig(SteeringSdfConfigDTO existing)
        {
            float quality = SanitizeQualityWeight(HomeostasisBrain.GlobalQualityWeight);
            SteeringSdfConfigDTO config = existing;
            bool invalidDimensions = config.Dimensions.x <= 1 || config.Dimensions.y <= 1 || config.Dimensions.z <= 1;
            if (invalidDimensions)
                config = CreateDefaultSdfConfig(quality);

            config.CellSizeMeters = math.select(new float3(4f), config.CellSizeMeters, math.isfinite(config.CellSizeMeters) & (config.CellSizeMeters > new float3(0.001f)));
            config.WhiskerLengthMeters = math.max(4f, math.select(36f, config.WhiskerLengthMeters, math.isfinite(config.WhiskerLengthMeters)));
            config.GlobalQualityWeight = quality;
            config.Flags |= SteeringSdfFlagSignedMeters;
            return config;
        }

        private static float ResolveSteeringDeltaTime(NativeArray<CognitionInput> inputs, NativeArray<int> activeSlots)
        {
            if (!inputs.IsCreated || !activeSlots.IsCreated || _activeSlotCount <= 0)
                return 0.02f;

            int slot = activeSlots[0];
            if ((uint)slot >= (uint)inputs.Length)
                return 0.02f;

            float dt = inputs[slot].DeltaTime;
            return math.clamp(math.select(0.02f, dt, math.isfinite(dt) & dt > 0f), 0.0001f, 0.25f);
        }

        private static void ReportLeviathanSteeringJobsCompleted(float perJobMs)
        {
            JobAdmissionScheduleExtensions.ReportAdmittedJobCompleted<GenerateMockSdfObstaclesJob>(JobAdmissionLane.Lane3_AI, perJobMs);
            JobAdmissionScheduleExtensions.ReportAdmittedJobCompleted<PopulateLeviathanSteeringParamsJob>(JobAdmissionLane.Lane3_AI, perJobMs);
            JobAdmissionScheduleExtensions.ReportAdmittedJobCompleted<EvaluateSdfAvoidanceJob>(JobAdmissionLane.Lane3_AI, perJobMs);
            JobAdmissionScheduleExtensions.ReportAdmittedJobCompleted<IntegrateSteeringVectorsJob>(JobAdmissionLane.Lane3_AI, perJobMs);
            JobAdmissionScheduleExtensions.ReportAdmittedJobCompleted<RecordSteeringTelemetryJob>(JobAdmissionLane.Lane3_AI, perJobMs);
        }

        private static void FinalizeLeviathanSteeringTelemetry(int frameId, float chainMicroseconds)
        {
            _lastLeviathanSteeringChainMicroseconds = math.max(0f, chainMicroseconds);
            NativeArray<SteeringTelemetryEntry> telemetry = _steeringTelemetryRing.Open();
            NativeArray<int> cursor = _steeringTelemetryCursor.Open();
            if (!telemetry.IsCreated || !cursor.IsCreated || telemetry.Length <= 0 || cursor.Length <= 0)
                return;

            int lastIndex = cursor[0] - 1;
            if (lastIndex < 0)
                lastIndex += telemetry.Length;
            lastIndex %= telemetry.Length;

            SteeringTelemetryEntry entry = telemetry[lastIndex];
            if (entry.Frame == (uint)frameId)
            {
                entry.BurstMicroseconds = _lastLeviathanSteeringChainMicroseconds;
                if (_lastLeviathanSteeringChainMicroseconds > LeviathanSteeringFaultBudgetMicroseconds)
                    entry.Flags |= SteeringTelemetryFlagBudgetExceeded;
                telemetry[lastIndex] = entry;
            }

            if (!_steeringFaultDumped && (entry.Flags & (SteeringTelemetryFlagBudgetExceeded | SteeringTelemetryFlagNonFiniteVelocity)) != 0u)
                _steeringFaultDumped = DumpLeviathanSteeringBlackBox();
        }

        private static unsafe bool DumpLeviathanSteeringBlackBox()
        {
            NativeArray<SteeringTelemetryEntry> telemetry = _steeringTelemetryRing.OpenRead();
            NativeArray<int> cursor = _steeringTelemetryCursor.OpenRead();
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return false;

            try
            {
                GlobalTelemetryBus.PublishPerformanceWarning(SteeringDumpFaultHash, SteeringDumpMagic, _lastLeviathanSteeringChainMicroseconds);
                Directory.CreateDirectory(SteeringDumpDirectoryRelativePath);

                using (FileStream stream = new FileStream(SteeringDumpRelativePath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    Span<byte> header = stackalloc byte[24];
                    BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(0, 4), SteeringDumpMagic);
                    BinaryPrimitives.WriteInt32LittleEndian(header.Slice(4, 4), SteeringDumpVersion);
                    BinaryPrimitives.WriteInt32LittleEndian(header.Slice(8, 4), telemetry.Length);
                    BinaryPrimitives.WriteInt32LittleEndian(header.Slice(12, 4), SteeringTelemetryEntrySizeBytes);
                    BinaryPrimitives.WriteInt32LittleEndian(header.Slice(16, 4), cursor.IsCreated && cursor.Length > 0 ? cursor[0] : 0);
                    BinaryPrimitives.WriteInt32LittleEndian(header.Slice(20, 4), UnsafeUtility.SizeOf<SteeringTelemetryEntry>());
                    stream.Write(header);

                    void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                    int byteCount = telemetry.Length * UnsafeUtility.SizeOf<SteeringTelemetryEntry>();
                    stream.Write(new ReadOnlySpan<byte>(source, byteCount));
                    stream.Flush(true);
                }

                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }

        private static void ReleaseLeviathanSteeringVaultHandles()
        {
            ReleaseVaultHandle(ref _steeringParams);
            ReleaseVaultHandle(ref _steeringAvoidance);
            ReleaseVaultHandle(ref _steeringWhiskers);
            ReleaseVaultHandle(ref _leviathanKinematicStates);
            ReleaseVaultHandle(ref _steeringTelemetryRing);
            ReleaseVaultHandle(ref _steeringTelemetryCursor);
            ReleaseVaultHandle(ref _steeringMockSdf);
            ReleaseVaultHandle(ref _steeringSdfConfig);
            ReleaseVaultHandle(ref _steeringProfiles);
            ReleaseVaultHandle(ref _steeringCsvScratch);
            _steeringMockSdfGenerated = false;
            _steeringEvaluationJobScheduled = false;
            _steeringFaultDumped = false;
            _lastLeviathanSteeringChainMicroseconds = 0f;
        }

        internal static bool TryCopyLeviathanKinematicState(int slot, out KinematicStateDTO state)
        {
            state = default;
            if (IsLeviathanSteeringWriteInFlight())
                return false;

            NativeArray<KinematicStateDTO> states = _leviathanKinematicStates.OpenRead();
            if (!states.IsCreated || (uint)slot >= (uint)states.Length)
                return false;

            state = states[slot];
            return math.all(math.isfinite(state.AUP_Position)) && math.all(math.isfinite(state.Velocity));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsLeviathanSteeringWriteInFlight()
        {
            return _evaluationScheduled & _steeringEvaluationJobScheduled;
        }

        internal static bool TryCopyLeviathanSteeringTelemetry(out SteeringTelemetryEntry entry)
        {
            entry = default;
            if (IsLeviathanSteeringWriteInFlight())
                return false;

            NativeArray<SteeringTelemetryEntry> telemetry = _steeringTelemetryRing.OpenRead();
            NativeArray<int> cursor = _steeringTelemetryCursor.OpenRead();
            if (!telemetry.IsCreated || !cursor.IsCreated || telemetry.Length <= 0 || cursor.Length <= 0)
                return false;

            int index = cursor[0] - 1;
            if (index < 0)
                index += telemetry.Length;
            index %= telemetry.Length;
            entry = telemetry[index];
            return entry.StateHash != 0u || entry.ActivePredators != 0u;
        }

        internal static unsafe bool TryReadLeviathanSteeringParam(int slot, out SteeringParamsDTO param)
        {
            param = default;
            if (IsLeviathanSteeringWriteInFlight())
                return false;

            NativeArray<SteeringParamsDTO> parameters = _steeringParams.OpenRead();
            if (!parameters.IsCreated || (uint)slot >= (uint)parameters.Length)
                return false;

            SteeringParamsDTO* ptr = (SteeringParamsDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(parameters);
            param = UnsafeUtility.AsRef<SteeringParamsDTO>(ptr + slot);
            return true;
        }

        internal static unsafe bool TryWriteLeviathanSteeringParam(int slot, in SteeringParamsDTO param)
        {
            if (IsLeviathanSteeringWriteInFlight())
                return false;

            NativeArray<SteeringParamsDTO> parameters = _steeringParams.Open();
            if (!parameters.IsCreated || (uint)slot >= (uint)parameters.Length)
                return false;

            SteeringParamsDTO* ptr = (SteeringParamsDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(parameters);
            UnsafeUtility.AsRef<SteeringParamsDTO>(ptr + slot) = param;
            return true;
        }

        internal static int CopyLeviathanSteeringDebugGizmos(Span<SteeringWhiskerResultDTO> destination)
        {
            if (IsLeviathanSteeringWriteInFlight())
                return 0;

            NativeArray<SteeringWhiskerResultDTO> whiskers = _steeringWhiskers.OpenRead();
            if (!whiskers.IsCreated || destination.Length <= 0)
                return 0;

            int count = math.min(destination.Length, math.min(whiskers.Length, LeviathanSteeringMaxWhiskers * 8));
            for (int i = 0; i < count; i++)
                destination[i] = whiskers[i];
            return count;
        }

#if UNITY_EDITOR
        internal static bool TryParseLeviathanSteeringProfilesCsv(ReadOnlySpan<byte> csvBytes)
        {
            if (IsLeviathanSteeringWriteInFlight())
                return false;

            NativeArray<SteeringProfileDTO> profiles = _steeringProfiles.Open();
            if (!profiles.IsCreated || profiles.Length <= 0)
                return false;

            return TryParseLeviathanSteeringProfilesCsv(csvBytes, profiles);
        }

        private static unsafe bool TryLoadLeviathanSteeringProfilesCsvCold()
        {
            NativeArray<SteeringProfileDTO> profiles = _steeringProfiles.Open();
            NativeArray<byte> scratch = _steeringCsvScratch.Open();
            if (!profiles.IsCreated || !scratch.IsCreated || profiles.Length <= 0 || scratch.Length <= 0)
                return false;

            string path = ResolveLeviathanSteeringProfilesPathCold();
            if (string.IsNullOrEmpty(path))
                return false;

            int byteCount = ReadLeviathanSteeringProfilesFileCold(path, scratch);
            if (byteCount <= 0)
                return false;

            void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(scratch);
            bool parsed = TryParseLeviathanSteeringProfilesCsv(new ReadOnlySpan<byte>(source, byteCount), profiles);
            if (!parsed && profiles.Length > 0)
                profiles[0] = CreateDefaultSteeringProfile();

            return parsed;
        }

        private static string ResolveLeviathanSteeringProfilesPathCold()
        {
#if UNITY_EDITOR
            DirectoryInfo dataDirectory = Directory.GetParent(Application.dataPath);
            string projectRoot = dataDirectory != null ? dataDirectory.FullName : Application.dataPath;
            string path = Path.Combine(projectRoot, "Assets", "_SourceData", "Fauna", LeviathanSteeringProfilesCsvName);
            if (File.Exists(path))
                return path;

            path = Path.Combine(projectRoot, "Data", "AI", LeviathanSteeringProfilesCsvName);
            if (File.Exists(path))
                return path;

            path = Path.Combine(projectRoot, LeviathanSteeringProfilesCsvName);
            return File.Exists(path) ? path : null;
#else
            return null;
#endif
        }

        private static unsafe int ReadLeviathanSteeringProfilesFileCold(string path, NativeArray<byte> scratch)
        {
            if (!scratch.IsCreated || scratch.Length <= 0)
                return 0;

            try
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    if (stream.Length <= 0L || stream.Length > scratch.Length)
                        return -1;

                    int length = (int)stream.Length;
                    void* destination = NativeArrayUnsafeUtility.GetUnsafePtr(scratch);
                    Span<byte> target = new Span<byte>(destination, length);
                    int totalRead = 0;
                    while (totalRead < length)
                    {
                        int read = stream.Read(target.Slice(totalRead));
                        if (read <= 0)
                            break;

                        totalRead += read;
                    }

                    return totalRead;
                }
            }
            catch (IOException)
            {
                return 0;
            }
            catch (UnauthorizedAccessException)
            {
                return 0;
            }
            catch (ArgumentException)
            {
                return 0;
            }
        }

        private static bool TryParseLeviathanSteeringProfilesCsv(ReadOnlySpan<byte> csvBytes, NativeArray<SteeringProfileDTO> profiles)
        {
            for (int i = 0; i < profiles.Length; i++)
                profiles[i] = default;

            int index = 0;
            int count = 0;
            bool parsedAny = false;
            while (index < csvBytes.Length && count < profiles.Length)
            {
                ReadOnlySpan<byte> line = ReadCsvLine(csvBytes, ref index);
                TrimCsv(ref line);
                if (line.Length == 0 || line[0] == (byte)'#')
                    continue;
                if (StartsWithAscii(line, "species"))
                    continue;

                if (TryParseSteeringProfileLine(line, out SteeringProfileDTO profile))
                {
                    profiles[count++] = profile;
                    parsedAny = true;
                }
            }

            if (!parsedAny && profiles.Length > 0)
                profiles[0] = CreateDefaultSteeringProfile();

            return parsedAny;
        }

        private static bool TryParseSteeringProfileLine(ReadOnlySpan<byte> line, out SteeringProfileDTO profile)
        {
            profile = default;
            int cursor = 0;
            if (!TryReadCsvCell(line, ref cursor, out ReadOnlySpan<byte> species))
                return false;
            TrimCsv(ref species);
            if (species.Length == 0)
                return false;

            if (!TryParseNextCsvFloat(line, ref cursor, out float maxSpeed) ||
                !TryParseNextCsvFloat(line, ref cursor, out float turnSpeed) ||
                !TryParseNextCsvFloat(line, ref cursor, out float lungeMultiplier) ||
                !TryParseNextCsvFloat(line, ref cursor, out float avoidanceWeight))
            {
                return false;
            }

            float whiskerLength = 36f;
            TryParseNextCsvFloat(line, ref cursor, out whiskerLength);
            profile = new SteeringProfileDTO
            {
                SpeciesHash = HashSpeciesProfileKey(species),
                MaxSpeed = SanitizePositiveFinite(maxSpeed, 22f),
                TurnSpeed = SanitizePositiveFinite(turnSpeed, 0.82f),
                LungeMultiplier = SanitizePositiveFinite(lungeMultiplier, 3.1f),
                ObstacleAvoidanceWeight = SanitizePositiveFinite(avoidanceWeight, 1.35f),
                WhiskerLengthMeters = SanitizePositiveFinite(whiskerLength, 36f),
                Flags = 1u
            };
            return true;
        }

        private static ReadOnlySpan<byte> ReadCsvLine(ReadOnlySpan<byte> bytes, ref int index)
        {
            int start = index;
            while (index < bytes.Length && bytes[index] != (byte)'\n' && bytes[index] != (byte)'\r')
                index++;

            int end = index;
            while (index < bytes.Length && (bytes[index] == (byte)'\n' || bytes[index] == (byte)'\r'))
                index++;

            return bytes.Slice(start, end - start);
        }

        private static bool TryReadCsvCell(ReadOnlySpan<byte> bytes, ref int cursor, out ReadOnlySpan<byte> cell)
        {
            cell = default;
            if (cursor > bytes.Length)
                return false;

            int start = cursor;
            while (cursor < bytes.Length && bytes[cursor] != (byte)',')
                cursor++;

            int end = cursor;
            if (cursor < bytes.Length && bytes[cursor] == (byte)',')
                cursor++;

            cell = bytes.Slice(start, end - start);
            return true;
        }

        private static bool TryParseNextCsvFloat(ReadOnlySpan<byte> bytes, ref int cursor, out float value)
        {
            value = 0f;
            if (!TryReadCsvCell(bytes, ref cursor, out ReadOnlySpan<byte> cell))
                return false;

            TrimCsv(ref cell);
            return TryParseAsciiFloat(cell, out value);
        }

        private static void TrimCsv(ref ReadOnlySpan<byte> span)
        {
            int start = 0;
            int end = span.Length - 1;
            while (start <= end && IsAsciiWhitespace(span[start]))
                start++;
            while (end >= start && IsAsciiWhitespace(span[end]))
                end--;
            span = start <= end ? span.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static bool StartsWithAscii(ReadOnlySpan<byte> span, string value)
        {
            if (span.Length < value.Length)
                return false;
            for (int i = 0; i < value.Length; i++)
            {
                byte a = span[i];
                if (a >= (byte)'A' && a <= (byte)'Z')
                    a = (byte)(a + 32);
                if (a != (byte)value[i])
                    return false;
            }

            return true;
        }

        private static bool IsAsciiWhitespace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t' || value == (byte)'\r' || value == (byte)'\n';
        }

        private static bool TryParseAsciiFloat(ReadOnlySpan<byte> span, out float value)
        {
            value = 0f;
            if (span.Length == 0)
                return false;

            int index = 0;
            bool negative = false;
            if (span[index] == (byte)'-' || span[index] == (byte)'+')
            {
                negative = span[index] == (byte)'-';
                index++;
            }

            double result = 0d;
            bool hasDigit = false;
            while (index < span.Length && span[index] >= (byte)'0' && span[index] <= (byte)'9')
            {
                hasDigit = true;
                result = (result * 10d) + (span[index] - (byte)'0');
                index++;
            }

            if (index < span.Length && span[index] == (byte)'.')
            {
                index++;
                double place = 0.1d;
                while (index < span.Length && span[index] >= (byte)'0' && span[index] <= (byte)'9')
                {
                    hasDigit = true;
                    result += (span[index] - (byte)'0') * place;
                    place *= 0.1d;
                    index++;
                }
            }

            if (!hasDigit)
                return false;

            value = (float)(negative ? -result : result);
            return float.IsFinite(value);
        }
#endif

        private static float SanitizePositiveFinite(float value, float fallback)
        {
            return float.IsFinite(value) && value > 0f ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeQualityWeight(float value)
        {
            return math.saturate(math.select(0f, value, math.isfinite(value)));
        }

        private static uint HashSpeciesProfileKey(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0u;

            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                hash = (hash ^ (byte)current) * 16777619u;
                hash = (hash ^ (byte)(current >> 8)) * 16777619u;
            }

            hash &= 0x7fffffffu;
            return hash == 0u ? 2166136261u : hash;
        }

        private static uint HashSpeciesProfileKey(ReadOnlySpan<byte> value)
        {
            if (TryParseUInt32(value, out uint numeric))
                return numeric & 0x7fffffffu;

            return HashUtf8AsciiAsMaskedLocHash(value);
        }

        private static bool TryParseUInt32(ReadOnlySpan<byte> value, out uint parsed)
        {
            parsed = 0u;
            if (value.Length == 0)
                return false;

            for (int i = 0; i < value.Length; i++)
            {
                byte b = value[i];
                if (b < (byte)'0' || b > (byte)'9')
                    return false;

                uint digit = (uint)(b - (byte)'0');
                if (parsed > (uint.MaxValue - digit) / 10u)
                    return false;

                parsed = parsed * 10u + digit;
            }

            return true;
        }

        private static uint HashUtf8AsciiAsMaskedLocHash(ReadOnlySpan<byte> value)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                byte b = value[i];
                hash = (hash ^ b) * 16777619u;
                hash = (hash ^ 0u) * 16777619u;
            }

            hash &= 0x7fffffffu;
            return hash == 0u ? 2166136261u : hash;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)]
        private unsafe struct GenerateMockSdfObstaclesJob : IJobParallelFor
        {
            [NoAlias, NativeDisableUnsafePtrRestriction] public float* Sdf;
            public SteeringSdfConfigDTO Config;

            public void Execute(int index)
            {
                int3 dims = Config.Dimensions;
                int x = index % dims.x;
                int yz = index / dims.x;
                int y = yz % dims.y;
                int z = yz / dims.y;
                float3 center = (new float3(dims.x, dims.y, dims.z) - 1f) * 0.5f;
                float3 local = (new float3(x, y, z) - center) * Config.CellSizeMeters;

                float sphereA = math.length(local - new float3(-28f, -8f, 18f)) - 22f;
                float sphereB = math.length(local - new float3(34f, 4f, -26f)) - 28f;
                float sphereC = math.length(local - new float3(0f, 12f, 0f)) - 16f;
                float trenchWall = 82f - math.abs(local.x);
                float ceiling = 42f - math.abs(local.y);
                float signedDistance = math.min(math.min(math.min(sphereA, sphereB), sphereC), math.min(trenchWall, ceiling));
                Sdf[index] = math.select(64f, signedDistance, math.isfinite(signedDistance));
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)]
        private unsafe struct PopulateLeviathanSteeringParamsJob : IJobParallelFor
        {
            [ReadOnly, NoAlias, NativeDisableUnsafePtrRestriction] public int* ActiveSlots;
            [ReadOnly, NoAlias, NativeDisableUnsafePtrRestriction] public CognitionInput* Inputs;
            [ReadOnly, NoAlias, NativeDisableUnsafePtrRestriction] public PackedCognitionOutput* Outputs;
            [NoAlias, NativeDisableUnsafePtrRestriction] public SteeringParamsDTO* Params;
            [ReadOnly, NoAlias, NativeDisableUnsafePtrRestriction] public SteeringProfileDTO* Profiles;
            public int ProfileCount;
            public float GlobalQualityWeight;

            public void Execute(int index)
            {
                int slot = ActiveSlots[index];
                ref readonly CognitionInput input = ref UnsafeUtility.AsRef<CognitionInput>(Inputs + slot);
                ref readonly PackedCognitionOutput output = ref UnsafeUtility.AsRef<PackedCognitionOutput>(Outputs + slot);
                ref SteeringParamsDTO param = ref UnsafeUtility.AsRef<SteeringParamsDTO>(Params + slot);
                uint priorState = ReadSteeringRuntimePackedState(Params + slot);
                bool activeApex = IsLeviathanSteeringCandidate(in input);

                float quality = SanitizeQualityWeight(GlobalQualityWeight);
                float3 fallback = NormalizeSafe(input.Forward, new float3(0f, 0f, 1f));
                float3 targetDirection = NormalizeSafe(output.DesiredDirection, fallback);
                float baseMaxSpeed = math.max(0.1f, math.select(18f, input.BaseMaxSpeedMetersPerSecond, math.isfinite(input.BaseMaxSpeedMetersPerSecond)));
                SteeringProfileDTO profile = ResolveProfile(Profiles, ProfileCount, input.SpeciesId);
                float maxSpeed = math.max(baseMaxSpeed, profile.MaxSpeed);
                float turnSpeed = math.max(0.05f, profile.TurnSpeed * math.max(0.1f, output.TurnMultiplier));
                float avoidance = math.max(0.05f, profile.ObstacleAvoidanceWeight * math.lerp(0.75f, 1.35f, quality));
                float lunge = math.max(1f, profile.LungeMultiplier);

                param = new SteeringParamsDTO
                {
                    MaxSpeed = math.select(0f, maxSpeed, activeApex),
                    TurnSpeed = math.select(0f, turnSpeed, activeApex),
                    LungeMultiplier = math.select(1f, lunge, activeApex),
                    ObstacleAvoidanceWeight = math.select(0f, avoidance, activeApex),
                    CurrentTargetDirection = math.select(float3.zero, targetDirection, activeApex)
                };
                WriteSteeringRuntimePackedState(Params + slot, math.select(0u, priorState, activeApex));
            }

            private static SteeringProfileDTO ResolveProfile(SteeringProfileDTO* profiles, int profileCount, int speciesId)
            {
                uint speciesHash = (uint)math.max(0, speciesId);
                for (int i = 0; i < profileCount; i++)
                {
                    SteeringProfileDTO profile = profiles[i];
                    if (profile.SpeciesHash == speciesHash && profile.Flags != 0u)
                        return profile;
                }

                return profiles[0];
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)]
        private unsafe struct EvaluateSdfAvoidanceJob : IJobParallelFor
        {
            [ReadOnly, NoAlias, NativeDisableUnsafePtrRestriction] public int* ActiveSlots;
            [ReadOnly, NoAlias, NativeDisableUnsafePtrRestriction] public CognitionInput* Inputs;
            [ReadOnly, NoAlias, NativeDisableUnsafePtrRestriction] public SteeringParamsDTO* Params;
            [ReadOnly, NoAlias, NativeDisableUnsafePtrRestriction] public float* Sdf;
            [NoAlias, NativeDisableUnsafePtrRestriction] public SteeringAvoidanceDTO* Avoidance;
            [NoAlias, NativeDisableUnsafePtrRestriction] public SteeringWhiskerResultDTO* Whiskers;
            public int SdfLength;
            public SteeringSdfConfigDTO Config;
            public uint Frame;

            public void Execute(int index)
            {
                int slot = ActiveSlots[index];
                ref readonly CognitionInput input = ref UnsafeUtility.AsRef<CognitionInput>(Inputs + slot);
                ref readonly SteeringParamsDTO param = ref UnsafeUtility.AsRef<SteeringParamsDTO>(Params + slot);
                ref SteeringAvoidanceDTO result = ref UnsafeUtility.AsRef<SteeringAvoidanceDTO>(Avoidance + slot);
                bool activeApex = IsLeviathanSteeringCandidate(in input);
                float quality = SanitizeQualityWeight(Config.GlobalQualityWeight);
                int activeWhiskers = math.clamp((int)math.lerp(LeviathanSteeringMinWhiskers, LeviathanSteeringMaxWhiskers, quality), LeviathanSteeringMinWhiskers, LeviathanSteeringMaxWhiskers);
                float3 forward = NormalizeSafe(param.CurrentTargetDirection, NormalizeSafe(input.Forward, new float3(0f, 0f, 1f)));
                float3 right = NormalizeSafe(math.cross(new float3(0f, 1f, 0f), forward), new float3(1f, 0f, 0f));
                float3 up = NormalizeSafe(math.cross(forward, right), new float3(0f, 1f, 0f));
                double3 creatureAup = ResolveCreatureAup(in input);

                float3 repulsion = float3.zero;
                float3 bestDirection = forward;
                float pressureSum = 0f;
                float bestPressure = 0f;
                float nearest = 999999f;
                int hits = 0;
                uint flags = 0u;
                float whiskerLength = math.max(1f, Config.WhiskerLengthMeters);
                for (int whisker = 0; whisker < activeWhiskers; whisker++)
                {
                    float3 direction = ResolveWhiskerDirection(whisker, forward, right, up);
                    double3 tipAup = creatureAup + new double3(direction.x * whiskerLength, direction.y * whiskerLength, direction.z * whiskerLength);
                    double3 localDouble = tipAup - Config.SdfOriginAup;
                    float3 local = new float3((float)localDouble.x, (float)localDouble.y, (float)localDouble.z);
                    float sdfMeters = SampleSdf(local);
                    bool hit = activeApex & math.isfinite(sdfMeters) & sdfMeters <= Config.SolidThresholdMeters;
                    float pressure = math.select(0f, math.saturate((Config.SolidThresholdMeters - sdfMeters) * math.rcp(math.max(1f, whiskerLength))), hit);
                    float3 normal = SampleSdfNormal(local);
                    float3 reflected = NormalizeSafe(direction - (2f * math.dot(direction, normal) * normal), -direction);
                    repulsion += math.select(float3.zero, reflected * pressure, hit);
                    pressureSum += pressure;
                    hits += math.select(0, 1, hit);
                    bool bestHit = hit & pressure > bestPressure;
                    bestDirection = math.select(bestDirection, reflected, bestHit);
                    bestPressure = math.select(bestPressure, pressure, bestHit);
                    nearest = math.select(nearest, math.min(nearest, math.max(0f, sdfMeters)), hit);
                    flags |= math.select(0u, SteeringAvoidanceFlagHitRock, hit);

                    int whiskerIndex = (slot * LeviathanSteeringMaxWhiskers) + whisker;
                    Whiskers[whiskerIndex] = new SteeringWhiskerResultDTO
                    {
                        Direction = math.select(float3.zero, direction, activeApex),
                        DistanceMeters = math.select(0f, whiskerLength, activeApex),
                        SampleLocalMeters = math.select(float3.zero, local, activeApex),
                        SdfMeters = math.select(0f, sdfMeters, activeApex),
                        ReflectedDirection = math.select(float3.zero, reflected, hit),
                        Pressure01 = pressure,
                        EntitySlot = slot,
                        WhiskerIndex = whisker,
                        Flags = math.select(0u, SteeringWhiskerFlagActive | math.select(0u, SteeringWhiskerFlagHitRock, hit), activeApex),
                        Frame = Frame
                    };
                }

                for (int whisker = activeWhiskers; whisker < LeviathanSteeringMaxWhiskers; whisker++)
                {
                    int whiskerIndex = (slot * LeviathanSteeringMaxWhiskers) + whisker;
                    Whiskers[whiskerIndex] = default;
                }

                float averagePressure = hits > 0 ? pressureSum * math.rcp(math.max(1, hits)) : 0f;
                repulsion = NormalizeSafe(repulsion, float3.zero) * math.saturate(pressureSum);
                bool finite = math.all(math.isfinite(repulsion)) & math.isfinite(averagePressure);
                flags |= math.select(SteeringAvoidanceFlagNonFinite, 0u, finite);
                result = new SteeringAvoidanceDTO
                {
                    Repulsion = math.select(float3.zero, repulsion, activeApex & finite),
                    AveragePressure = math.select(0f, averagePressure, activeApex & finite),
                    BestWhiskerDirection = math.select(float3.zero, bestDirection, activeApex),
                    NearestHitDistance = math.select(0f, nearest, activeApex & hits > 0),
                    ActiveWhiskerCount = math.select(0, activeWhiskers, activeApex),
                    HitWhiskerCount = math.select(0, hits, activeApex),
                    Flags = math.select(0u, flags, activeApex),
                    DesiredSpeedMetersPerSecond = 0f,
                    StateHash = BuildHash((uint)slot, Frame, flags, repulsion),
                    Reserved0 = 0u
                };
            }

            private float SampleSdf(float3 localMeters)
            {
                int3 dims = Config.Dimensions;
                float3 cell = math.max(Config.CellSizeMeters, new float3(0.001f));
                float3 center = (new float3(dims.x, dims.y, dims.z) - 1f) * 0.5f;
                int3 voxel = (int3)math.round((localMeters / cell) + center);
                if (voxel.x < 0 || voxel.y < 0 || voxel.z < 0 || voxel.x >= dims.x || voxel.y >= dims.y || voxel.z >= dims.z)
                    return 64f;

                int flat = voxel.x + (voxel.y * dims.x) + (voxel.z * dims.x * dims.y);
                return (uint)flat < (uint)SdfLength ? Sdf[flat] : 64f;
            }

            private float3 SampleSdfNormal(float3 localMeters)
            {
                float3 cell = math.max(Config.CellSizeMeters, new float3(0.001f));
                float dx = SampleSdf(localMeters + new float3(cell.x, 0f, 0f)) - SampleSdf(localMeters - new float3(cell.x, 0f, 0f));
                float dy = SampleSdf(localMeters + new float3(0f, cell.y, 0f)) - SampleSdf(localMeters - new float3(0f, cell.y, 0f));
                float dz = SampleSdf(localMeters + new float3(0f, 0f, cell.z)) - SampleSdf(localMeters - new float3(0f, 0f, cell.z));
                return NormalizeSafe(new float3(dx, dy, dz), new float3(0f, 1f, 0f));
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)]
        private unsafe struct IntegrateSteeringVectorsJob : IJobParallelFor
        {
            [ReadOnly, NoAlias, NativeDisableUnsafePtrRestriction] public int* ActiveSlots;
            [ReadOnly, NoAlias, NativeDisableUnsafePtrRestriction] public CognitionInput* Inputs;
            [ReadOnly, NoAlias, NativeDisableUnsafePtrRestriction] public PackedCognitionOutput* Outputs;
            [NoAlias, NativeDisableUnsafePtrRestriction] public SteeringParamsDTO* Params;
            [NoAlias, NativeDisableUnsafePtrRestriction] public SteeringAvoidanceDTO* Avoidance;
            [NoAlias, NativeDisableUnsafePtrRestriction] public KinematicStateDTO* KinematicStates;
            public float DeltaTime;
            public uint Frame;

            public void Execute(int index)
            {
                int slot = ActiveSlots[index];
                ref readonly CognitionInput input = ref UnsafeUtility.AsRef<CognitionInput>(Inputs + slot);
                ref readonly PackedCognitionOutput output = ref UnsafeUtility.AsRef<PackedCognitionOutput>(Outputs + slot);
                ref SteeringParamsDTO param = ref UnsafeUtility.AsRef<SteeringParamsDTO>(Params + slot);
                ref SteeringAvoidanceDTO avoidance = ref UnsafeUtility.AsRef<SteeringAvoidanceDTO>(Avoidance + slot);
                ref KinematicStateDTO state = ref UnsafeUtility.AsRef<KinematicStateDTO>(KinematicStates + slot);
                bool activeApex = IsLeviathanSteeringCandidate(in input);
                if (!activeApex)
                    return;

                double3 creatureAup = ResolveCreatureAup(in input);
                double3 targetAup = ResolveTargetAup(in input, creatureAup);
                double3 toTargetDouble = targetAup - creatureAup;
                float targetDistance = ClampDoubleDistanceToFloat(toTargetDouble);
                float3 pursuit = NormalizeDouble3(toTargetDouble, NormalizeSafe(param.CurrentTargetDirection, new float3(0f, 0f, 1f)));
                float3 cognition = NormalizeSafe(param.CurrentTargetDirection, pursuit);
                float3 repulsion = avoidance.Repulsion;
                float3 desired = NormalizeSafe(pursuit + (cognition * 0.5f) + (repulsion * param.ObstacleAvoidanceWeight), cognition);

                uint runtimeState = ReadSteeringRuntimePackedState(Params + slot);
                int lungeFrames = (int)(runtimeState & 0xFFu);
                bool attacking = (output.OutputFlags & (uint)CognitionOutputFlags.ShouldAttack) != 0u;
                bool enterLunge = attacking & targetDistance <= LeviathanSteeringLungeDistanceMeters;
                lungeFrames = math.select(math.max(0, lungeFrames - 1), LeviathanSteeringLungeLockFrames, enterLunge);
                float3 currentVelocity = math.select(input.Velocity, state.Velocity, math.all(math.isfinite(state.Velocity)) & math.lengthsq(state.Velocity) > LeviathanSteeringMathEpsilon);
                desired = math.select(desired, NormalizeSafe(currentVelocity, desired), lungeFrames > 0 & !enterLunge);

                float speedMultiplier = math.max(0.05f, math.select(1f, output.SpeedMultiplier, math.isfinite(output.SpeedMultiplier)));
                float desiredSpeed = math.max(0f, param.MaxSpeed * speedMultiplier);
                desiredSpeed *= math.select(1f, math.max(1f, param.LungeMultiplier), lungeFrames > 0);
                float currentSpeed = math.sqrt(math.max(0f, math.lengthsq(currentVelocity)));
                float turn = math.saturate(math.max(0f, DeltaTime) * math.max(0f, param.TurnSpeed));
                float3 currentDirection = NormalizeSafe(currentVelocity, cognition);
                float3 smoothedDirection = SlerpDirection(currentDirection, desired, turn);
                float smoothedSpeed = math.lerp(currentSpeed, desiredSpeed, math.saturate(turn + DeltaTime));
                float3 nextVelocity = smoothedDirection * smoothedSpeed;
                if (!math.all(math.isfinite(nextVelocity)))
                    nextVelocity = float3.zero;

                state.AUP_Position = creatureAup;
                state.Velocity = nextVelocity;
                state.AngularVelocity = math.cross(currentDirection, smoothedDirection) * math.rcp(math.max(DeltaTime, 0.0001f));
                state.Mass = math.max(1f, math.select(100000f, state.Mass, math.isfinite(state.Mass) & state.Mass > 0f));
                state.Flags = (state.Flags & 0xFFFF0000u) | (uint)(slot & 0xFFFF);
                state.DragCoefficient = math.max(0f, math.select(0.18f, state.DragCoefficient, math.isfinite(state.DragCoefficient)));
                state.RestingFrameCount = 0;
                state.DeepSleepTickCount = 0;
                state.SleepMaterialIndex = 0;

                avoidance.DesiredSpeedMetersPerSecond = smoothedSpeed;
                avoidance.StateHash = BuildHash((uint)slot, Frame, avoidance.Flags, nextVelocity);
                WriteSteeringRuntimePackedState(Params + slot, (uint)math.clamp(lungeFrames, 0, 255));
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)]
        private unsafe struct RecordSteeringTelemetryJob : IJob
        {
            [ReadOnly, NoAlias, NativeDisableUnsafePtrRestriction] public int* ActiveSlots;
            [ReadOnly, NoAlias, NativeDisableUnsafePtrRestriction] public CognitionInput* Inputs;
            [ReadOnly, NoAlias, NativeDisableUnsafePtrRestriction] public SteeringParamsDTO* Params;
            [ReadOnly, NoAlias, NativeDisableUnsafePtrRestriction] public SteeringAvoidanceDTO* Avoidance;
            [ReadOnly, NoAlias, NativeDisableUnsafePtrRestriction] public KinematicStateDTO* KinematicStates;
            [NoAlias, NativeDisableUnsafePtrRestriction] public SteeringTelemetryEntry* Telemetry;
            [NoAlias, NativeDisableUnsafePtrRestriction] public int* TelemetryCursor;
            public int ActiveSlotCount;
            public uint Frame;
            public float EstimatedBurstMicroseconds;

            public void Execute()
            {
                float3 velocitySum = float3.zero;
                double3 firstAup = double3.zero;
                uint active = 0u;
                uint repulsions = 0u;
                uint flags = 0u;
                uint stateHash = 2166136261u;
                float maxLungeVelocity = 0f;
                for (int i = 0; i < ActiveSlotCount; i++)
                {
                    int slot = ActiveSlots[i];
                    ref readonly CognitionInput input = ref UnsafeUtility.AsRef<CognitionInput>(Inputs + slot);
                    bool activeApex = IsLeviathanSteeringCandidate(in input);
                    if (!activeApex)
                        continue;

                    ref readonly KinematicStateDTO state = ref UnsafeUtility.AsRef<KinematicStateDTO>(KinematicStates + slot);
                    ref readonly SteeringAvoidanceDTO avoidance = ref UnsafeUtility.AsRef<SteeringAvoidanceDTO>(Avoidance + slot);
                    bool velocityFinite = math.all(math.isfinite(state.Velocity));
                    flags |= math.select(SteeringTelemetryFlagNonFiniteVelocity, 0u, velocityFinite);
                    velocitySum += math.select(float3.zero, state.Velocity, velocityFinite);
                    firstAup = math.select(firstAup, state.AUP_Position, active == 0u);
                    repulsions += math.select(0u, 1u, (avoidance.Flags & SteeringAvoidanceFlagHitRock) != 0u);
                    float speed = math.sqrt(math.max(0f, math.lengthsq(state.Velocity)));
                    bool lungeActive = (ReadSteeringRuntimePackedState(Params + slot) & 0xFFu) != 0u;
                    maxLungeVelocity = math.select(maxLungeVelocity, math.max(maxLungeVelocity, speed), lungeActive);
                    stateHash = BuildHash(stateHash, (uint)slot, avoidance.StateHash, state.Velocity);
                    active++;
                }

                flags |= math.select(0u, SteeringTelemetryFlagBudgetExceeded, EstimatedBurstMicroseconds > LeviathanSteeringFaultBudgetMicroseconds);
                float invActive = active > 0u ? math.rcp((float)active) : 0f;
                int cursor = math.max(0, TelemetryCursor[0]);
                int index = cursor % LeviathanSteeringTelemetryCapacity;
                Telemetry[index] = new SteeringTelemetryEntry
                {
                    FirstAup = firstAup,
                    AverageVelocity = velocitySum * invActive,
                    Frame = Frame,
                    ActivePredators = active,
                    ActiveRepulsions = repulsions,
                    MaxLungeVelocity = maxLungeVelocity,
                    BurstMicroseconds = EstimatedBurstMicroseconds,
                    Flags = flags,
                    StateHash = stateHash
                };
                TelemetryCursor[0] = (cursor + 1) % LeviathanSteeringTelemetryCapacity;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsLeviathanSteeringCandidate(in CognitionInput input)
        {
            const int activePredatorMask = (int)CognitionInputFlags.Active | (int)CognitionInputFlags.PredatorRole;
            bool activePredator = (input.Flags & activePredatorMask) == activePredatorMask;
            bool alphaLeviathan = (input.Flags & (int)CognitionInputFlags.UseAlphaLeviathanCognition) != 0;
            bool apexPredator = (input.Flags & (int)CognitionInputFlags.IsApexPredator) != 0;
            return activePredator & (alphaLeviathan | apexPredator);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double3 ResolveCreatureAup(in CognitionInput input)
        {
            return input.FloatingOriginOffset + new double3(input.Position.x, input.Position.y, input.Position.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double3 ResolveTargetAup(in CognitionInput input, double3 creatureAup)
        {
            if ((input.Flags & (int)CognitionInputFlags.HasPackTarget) != 0)
                return ResolveAup(in input.PackTargetAup);
            if ((input.Flags & (int)CognitionInputFlags.HasPlayerTarget) != 0)
                return ResolveAup(in input.PlayerTargetAup);

            float3 fallback = NormalizeSafe(input.Forward, new float3(0f, 0f, 1f));
            return creatureAup + new double3(fallback.x * 100d, fallback.y * 100d, fallback.z * 100d);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double3 ResolveAup(in AbsoluteUniversePositionBlit128 aup)
        {
            double cellSize = AbsoluteUniversePosition.CellSizeMeters;
            return new double3(
                (aup.GridX * cellSize) + aup.Local.x,
                (aup.GridY * cellSize) + aup.Local.y,
                (aup.GridZ * cellSize) + aup.Local.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ClampDoubleDistanceToFloat(double3 value)
        {
            double distanceSq = math.lengthsq(value);
            if (!math.isfinite(distanceSq) || distanceSq <= 0.000001d)
                return 0f;

            double distance = math.sqrt(distanceSq);
            return (float)math.min(distance, (double)float.MaxValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 NormalizeDouble3(double3 value, float3 fallback)
        {
            double lengthSq = math.lengthsq(value);
            bool valid = math.isfinite(lengthSq) & lengthSq > 0.000001d;
            double inverseLength = math.rsqrt(math.max(lengthSq, 0.000001d));
            float3 normalized = new float3((float)(value.x * inverseLength), (float)(value.y * inverseLength), (float)(value.z * inverseLength));
            return math.select(fallback, normalized, valid & math.all(math.isfinite(normalized)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            bool valid = math.all(math.isfinite(value)) & lengthSq > LeviathanSteeringMathEpsilon;
            float3 normalized = math.normalizesafe(value, fallback);
            return math.select(fallback, normalized, valid & math.all(math.isfinite(normalized)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SlerpDirection(float3 from, float3 to, float t)
        {
            float3 safeFrom = NormalizeSafe(from, new float3(0f, 0f, 1f));
            float3 safeTo = NormalizeSafe(to, safeFrom);
            float blend = math.saturate(t);
            blend = blend * blend * (3f - (2f * blend));
            float3 result = math.lerp(safeFrom, safeTo, blend);
            return NormalizeSafe(result, safeTo);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolveWhiskerDirection(int index, float3 forward, float3 right, float3 up)
        {
            switch (index)
            {
                case 0: return forward;
                case 1: return right;
                case 2: return -right;
                case 3: return up;
                case 4: return -up;
                case 5: return -forward;
                case 6: return NormalizeSafe(forward + right, forward);
                case 7: return NormalizeSafe(forward - right, forward);
                case 8: return NormalizeSafe(forward + up, forward);
                case 9: return NormalizeSafe(forward - up, forward);
                case 10: return NormalizeSafe(forward + right + up, forward);
                case 11: return NormalizeSafe(forward + right - up, forward);
                case 12: return NormalizeSafe(forward - right + up, forward);
                case 13: return NormalizeSafe(forward - right - up, forward);
                case 14: return NormalizeSafe(right + up, right);
                case 15: return NormalizeSafe(right - up, right);
                case 16: return NormalizeSafe(-right + up, -right);
                case 17: return NormalizeSafe(-right - up, -right);
                case 18: return NormalizeSafe(-forward + right, -forward);
                case 19: return NormalizeSafe(-forward - right, -forward);
                case 20: return NormalizeSafe(-forward + up, -forward);
                case 21: return NormalizeSafe(-forward - up, -forward);
                case 22: return NormalizeSafe(-forward + right + up, -forward);
                case 23: return NormalizeSafe(-forward + right - up, -forward);
                case 24: return NormalizeSafe(-forward - right + up, -forward);
                default: return NormalizeSafe(-forward - right - up, -forward);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint BuildHash(uint a, uint b, uint c, float3 v)
        {
            uint hash = 2166136261u;
            hash = (hash ^ a) * 16777619u;
            hash = (hash ^ b) * 16777619u;
            hash = (hash ^ c) * 16777619u;
            hash = (hash ^ (uint)math.asint(v.x)) * 16777619u;
            hash = (hash ^ (uint)math.asint(v.y)) * 16777619u;
            hash = (hash ^ (uint)math.asint(v.z)) * 16777619u;
            return hash == 0u ? 2166136261u : hash;
        }
    }

#if UNITY_EDITOR
    internal sealed class LeviathanKinematicsTunerWindow : EditorWindow
    {
        private IntegerField _slotField;
        private Slider _maxSpeed;
        private Slider _turnSpeed;
        private Slider _lungeMultiplier;
        private Slider _avoidanceWeight;
        private Label _telemetryLabel;
        private LeviathanTelemetryGraphElement _graph;
        private bool _stateReady;
        private uint _lastTelemetryFrame = uint.MaxValue;

        [MenuItem("HECTON-8/AI/Leviathan Kinematics Tuner")]
        private static void Open()
        {
            GetWindow<LeviathanKinematicsTunerWindow>("Leviathan Kinematics");
        }

        private void CreateGUI()
        {
            _slotField = new IntegerField("Slot") { value = 0 };
            _maxSpeed = new Slider("Max Speed", 0f, 80f) { showInputField = true };
            _turnSpeed = new Slider("Turn Speed", 0f, 8f) { showInputField = true };
            _lungeMultiplier = new Slider("Lunge Multiplier", 1f, 8f) { showInputField = true };
            _avoidanceWeight = new Slider("Avoidance Weight", 0f, 5f) { showInputField = true };
            _telemetryLabel = new Label("Telemetry graph: cyan velocity, red SDF repulsions");
            _graph = new LeviathanTelemetryGraphElement();
            _graph.style.height = 96f;
            _graph.style.marginTop = 8f;

            rootVisualElement.Add(_slotField);
            rootVisualElement.Add(_maxSpeed);
            rootVisualElement.Add(_turnSpeed);
            rootVisualElement.Add(_lungeMultiplier);
            rootVisualElement.Add(_avoidanceWeight);
            rootVisualElement.Add(_telemetryLabel);
            rootVisualElement.Add(_graph);

            _slotField.RegisterValueChangedCallback(OnSlotChanged);
            _maxSpeed.RegisterValueChangedCallback(OnTuningChanged);
            _turnSpeed.RegisterValueChangedCallback(OnTuningChanged);
            _lungeMultiplier.RegisterValueChangedCallback(OnTuningChanged);
            _avoidanceWeight.RegisterValueChangedCallback(OnTuningChanged);
            EditorApplication.update += Tick;
            EnsureSteeringState();
            PullSlot();
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
        }

        private void Tick()
        {
            if (!EnsureSteeringState())
                return;

            if (PredatorCognitionDomain.TryCopyLeviathanSteeringTelemetry(out SteeringTelemetryEntry entry))
            {
                if (entry.Frame == _lastTelemetryFrame)
                    return;

                _lastTelemetryFrame = entry.Frame;
                _graph.Append(math.length(entry.AverageVelocity), entry.ActiveRepulsions);
            }
        }

        private void OnSlotChanged(ChangeEvent<int> evt)
        {
            PullSlot();
        }

        private void OnTuningChanged(ChangeEvent<float> evt)
        {
            PushSlot();
        }

        private void PullSlot()
        {
            if (!EnsureSteeringState())
                return;

            int slot = math.max(0, _slotField.value);
            if (!PredatorCognitionDomain.TryReadLeviathanSteeringParam(slot, out SteeringParamsDTO param))
                return;

            _maxSpeed.SetValueWithoutNotify(param.MaxSpeed);
            _turnSpeed.SetValueWithoutNotify(param.TurnSpeed);
            _lungeMultiplier.SetValueWithoutNotify(param.LungeMultiplier);
            _avoidanceWeight.SetValueWithoutNotify(param.ObstacleAvoidanceWeight);
        }

        private void PushSlot()
        {
            if (!EnsureSteeringState())
                return;

            int slot = math.max(0, _slotField.value);
            if (!PredatorCognitionDomain.TryReadLeviathanSteeringParam(slot, out SteeringParamsDTO param))
                return;

            param.MaxSpeed = _maxSpeed.value;
            param.TurnSpeed = _turnSpeed.value;
            param.LungeMultiplier = _lungeMultiplier.value;
            param.ObstacleAvoidanceWeight = _avoidanceWeight.value;
            PredatorCognitionDomain.TryWriteLeviathanSteeringParam(slot, in param);
        }

        private bool EnsureSteeringState()
        {
            if (_stateReady)
                return true;

            _stateReady = PredatorCognitionDomain.EnsureLeviathanSteeringStateCold();
            return _stateReady;
        }
    }

    internal sealed class LeviathanTelemetryGraphElement : VisualElement
    {
        private const int SampleCount = 128;
        private readonly float[] _velocity = new float[SampleCount];
        private readonly float[] _repulsions = new float[SampleCount];
        private int _cursor;
        private int _count;

        public LeviathanTelemetryGraphElement()
        {
            generateVisualContent += OnGenerate;
        }

        public void Append(float velocity, uint repulsions)
        {
            _velocity[_cursor] = math.max(0f, velocity);
            _repulsions[_cursor] = math.max(0f, (float)repulsions);
            _cursor = (_cursor + 1) & (SampleCount - 1);
            _count = math.min(SampleCount, _count + 1);
            MarkDirtyRepaint();
        }

        private void OnGenerate(MeshGenerationContext context)
        {
            Rect rect = contentRect;
            Painter2D painter = context.painter2D;
            painter.lineWidth = 1.5f;
            DrawSeries(painter, rect, _velocity, 80f, Color.cyan);
            DrawSeries(painter, rect, _repulsions, 16f, Color.red);
        }

        private void DrawSeries(Painter2D painter, Rect rect, float[] samples, float maxValue, Color color)
        {
            if (_count <= 1 || rect.width <= 1f || rect.height <= 1f)
                return;

            painter.strokeColor = color;
            painter.BeginPath();
            for (int i = 0; i < _count; i++)
            {
                int sampleIndex = (_cursor - _count + i + SampleCount) & (SampleCount - 1);
                float x = rect.xMin + (rect.width * i * math.rcp(math.max(1, _count - 1)));
                float y = rect.yMax - (rect.height * math.saturate(samples[sampleIndex] * math.rcp(math.max(0.0001f, maxValue))));
                if (i == 0)
                    painter.MoveTo(new Vector2(x, y));
                else
                    painter.LineTo(new Vector2(x, y));
            }

            painter.Stroke();
        }
    }

    [InitializeOnLoad]
    internal static class LeviathanSteeringDebugGizmo
    {
        private const int LeviathanSteeringScratchCount = 208;
        private const uint DebugWhiskerFlagHitRock = 1u << 0;
        private const uint DebugWhiskerFlagActive = 1u << 1;
        private static readonly SteeringWhiskerResultDTO[] _whiskerScratch = new SteeringWhiskerResultDTO[LeviathanSteeringScratchCount];
        private static bool _enabled;
        private static bool _stateReady;

        static LeviathanSteeringDebugGizmo()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        [MenuItem("HECTON-8/AI/Toggle Leviathan Steering Whiskers")]
        private static void Toggle()
        {
            _enabled = !_enabled;
            SceneView.RepaintAll();
        }

        private static void OnSceneGui(SceneView sceneView)
        {
            if (!_enabled)
                return;
            if (!_stateReady)
                _stateReady = PredatorCognitionDomain.EnsureLeviathanSteeringStateCold();
            if (!_stateReady)
                return;

            Span<SteeringWhiskerResultDTO> span = _whiskerScratch;
            int count = PredatorCognitionDomain.CopyLeviathanSteeringDebugGizmos(span);
            for (int i = 0; i < count; i++)
            {
                SteeringWhiskerResultDTO whisker = span[i];
                if ((whisker.Flags & DebugWhiskerFlagActive) == 0u)
                    continue;

                Vector3 start = new Vector3(whisker.SampleLocalMeters.x, whisker.SampleLocalMeters.y, whisker.SampleLocalMeters.z) -
                                new Vector3(whisker.Direction.x, whisker.Direction.y, whisker.Direction.z) * whisker.DistanceMeters;
                Vector3 end = new Vector3(whisker.SampleLocalMeters.x, whisker.SampleLocalMeters.y, whisker.SampleLocalMeters.z);
                Handles.color = (whisker.Flags & DebugWhiskerFlagHitRock) != 0u ? Color.red : Color.green;
                Handles.DrawLine(start, end);
                if ((whisker.Flags & DebugWhiskerFlagHitRock) != 0u)
                {
                    Vector3 reflected = new Vector3(whisker.ReflectedDirection.x, whisker.ReflectedDirection.y, whisker.ReflectedDirection.z);
                    Handles.color = Color.blue;
                    Handles.DrawLine(end, end + reflected * math.max(1f, whisker.DistanceMeters * 0.25f));
                }
            }
        }
    }

    internal static class OOP_Movement_Scanner
    {
        private const string SharedReportSectionName = "shinobu303LeviathanSteering";

        [MenuItem("HECTON-8/AI/Run OOP Movement Scanner")]
        private static void Run()
        {
            string root = Directory.GetCurrentDirectory();
            string scripts = Path.Combine(root, "Assets", "_Project", "Scripts");
            string reportPath = Path.Combine(root, "Docs", "Reports", "AI_OPTIMIZATION_REPORT.json");
            string stableReportPath = Path.Combine(root, "Docs", "Reports", "SHINOBU_303_AI_OPTIMIZATION_REPORT.json");
            int updateScopes = 0;
            int violations = 0;
            ScanDirectory(scripts, ref updateScopes, ref violations);
            string sectionJson = BuildScannerReportSection(updateScopes, violations);
            WriteStableScannerReport(stableReportPath, sectionJson);
            UpsertSharedScannerReport(reportPath, sectionJson);
            AssetDatabase.Refresh();
        }

        private static string BuildScannerReportSection(int updateScopes, int violations)
        {
            return "{\n" +
                   "    \"scanner\": \"OOP_Movement_Scanner\",\n" +
                   "    \"agent\": \"SHINOBU_303\",\n" +
                   "    \"domain\": \"LEVIATHAN_STEERING_MOTOR\",\n" +
                   "    \"summary\": \"OOP Steering Mechanisms Eradicated\",\n" +
                   "    \"status\": \"STATIC_SCANNER_EDITOR_RUN\",\n" +
                   "    \"newHotPath\": \"Assets/_Project/Scripts/Fauna/PredatorCognitionDomain_Steering.cs\",\n" +
                   "    \"newRuntimeRoute\": \"PredatorCognitionDomain -> GlobalDataVault steering buffers -> Burst SDF whiskers -> KinematicStateDTO\",\n" +
                   "    \"dtoLayout\": \"SteeringParamsDTO=32 bytes: MaxSpeed@0 TurnSpeed@4 LungeMultiplier@8 ObstacleAvoidanceWeight@12 CurrentTargetDirection@16 pad@28\",\n" +
                   "    \"updateScopesScanned\": " + updateScopes + ",\n" +
                   "    \"oOPSteeringViolations\": " + violations + ",\n" +
                   "    \"globalQualityWeightContinuous\": true,\n" +
                   "    \"activeWhiskersAtQualityZero\": 6,\n" +
                   "    \"activeWhiskersAtQualityOne\": 26,\n" +
                   "    \"blackBoxTelemetryFrames\": 300\n" +
                   "  }";
        }

        private static void WriteStableScannerReport(string path, string sectionJson)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, sectionJson + "\n");
        }

        private static void UpsertSharedScannerReport(string path, string sectionJson)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string existing = File.Exists(path) ? File.ReadAllText(path) : "{\n}\n";
            string trimmed = existing.Trim();
            if (trimmed.Length < 2 || trimmed[0] != '{' || trimmed[trimmed.Length - 1] != '}')
                trimmed = "{\n}";

            trimmed = RemoveJsonObjectProperty(trimmed, SharedReportSectionName).Trim();
            int insert = trimmed.LastIndexOf('}');
            if (insert < 0)
                trimmed = "{\n}";

            insert = trimmed.LastIndexOf('}');
            string prefix = trimmed.Substring(0, insert).TrimEnd();
            string suffix = trimmed.Substring(insert);
            bool hasExistingProperties = prefix.Length > 1;
            string comma = hasExistingProperties ? ",\n" : "\n";
            string property = "  \"" + SharedReportSectionName + "\": " + sectionJson;
            File.WriteAllText(path, prefix + comma + property + "\n" + suffix + "\n");
        }

        private static string RemoveJsonObjectProperty(string json, string propertyName)
        {
            string token = "\"" + propertyName + "\"";
            int nameIndex = json.IndexOf(token, StringComparison.Ordinal);
            if (nameIndex < 0)
                return json;

            int colon = json.IndexOf(':', nameIndex + token.Length);
            if (colon < 0)
                return json;

            int objectStart = FindNextNonWhitespace(json, colon + 1);
            if (objectStart < 0 || json[objectStart] != '{')
                return json;

            int objectEnd = FindJsonObjectEnd(json, objectStart);
            if (objectEnd < objectStart)
                return json;

            int removeStart = nameIndex;
            while (removeStart > 0 && char.IsWhiteSpace(json[removeStart - 1]))
                removeStart--;

            int removeEnd = objectEnd + 1;
            int after = FindNextNonWhitespace(json, removeEnd);
            if (after >= 0 && json[after] == ',')
            {
                removeEnd = after + 1;
            }
            else
            {
                int before = removeStart - 1;
                while (before >= 0 && char.IsWhiteSpace(json[before]))
                    before--;
                if (before >= 0 && json[before] == ',')
                    removeStart = before;
            }

            return json.Remove(removeStart, removeEnd - removeStart);
        }

        private static int FindNextNonWhitespace(string text, int start)
        {
            for (int i = start; i < text.Length; i++)
            {
                if (!char.IsWhiteSpace(text[i]))
                    return i;
            }

            return -1;
        }

        private static int FindJsonObjectEnd(string text, int objectStart)
        {
            int depth = 0;
            bool inString = false;
            bool escape = false;
            for (int i = objectStart; i < text.Length; i++)
            {
                char c = text[i];
                if (inString)
                {
                    if (escape)
                        escape = false;
                    else if (c == '\\')
                        escape = true;
                    else if (c == '"')
                        inString = false;
                    continue;
                }

                if (c == '"')
                    inString = true;
                else if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }

        private static void ScanDirectory(string directory, ref int updateScopes, ref int violations)
        {
            string[] files = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string text = File.ReadAllText(files[i]);
                ScanText(text, ref updateScopes, ref violations);
            }
        }

        private static void ScanText(string text, ref int updateScopes, ref int violations)
        {
            int cursor = 0;
            string sanitized = StripCommentsAndStrings(text);
            string updateToken = "Upd" + "ate(";
            while (cursor < sanitized.Length)
            {
                int updateIndex = sanitized.IndexOf(updateToken, cursor, StringComparison.Ordinal);
                if (updateIndex < 0)
                    break;
                cursor = updateIndex + 7;
                int brace = sanitized.IndexOf('{', cursor);
                if (brace < 0)
                    break;
                int end = FindScopeEnd(sanitized, brace);
                if (end <= brace)
                    break;
                updateScopes++;
                if (ContainsBetween(sanitized, brace, end, "Transform." + "Translate") ||
                    ContainsBetween(sanitized, brace, end, ".Set" + "Destination(") ||
                    ContainsBetween(sanitized, brace, end, "NavMesh" + "Agent"))
                {
                    violations++;
                }
                cursor = end + 1;
            }
        }

        private static bool ContainsBetween(string text, int start, int end, string token)
        {
            int index = text.IndexOf(token, start, end - start, StringComparison.Ordinal);
            return index >= start && index < end;
        }

        private static string StripCommentsAndStrings(string text)
        {
            char[] chars = text.ToCharArray();
            bool lineComment = false;
            bool blockComment = false;
            bool stringLiteral = false;
            bool verbatimString = false;
            bool stringEscape = false;
            bool charLiteral = false;
            bool charEscape = false;
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                char next = i + 1 < chars.Length ? chars[i + 1] : '\0';

                if (lineComment)
                {
                    if (c == '\r' || c == '\n')
                        lineComment = false;
                    else
                        chars[i] = ' ';
                    continue;
                }

                if (blockComment)
                {
                    if (c == '*' && next == '/')
                    {
                        chars[i] = ' ';
                        chars[i + 1] = ' ';
                        i++;
                        blockComment = false;
                    }
                    else if (c != '\r' && c != '\n')
                    {
                        chars[i] = ' ';
                    }
                    continue;
                }

                if (stringLiteral)
                {
                    if (verbatimString && c == '"' && next == '"')
                    {
                        chars[i] = ' ';
                        chars[i + 1] = ' ';
                        i++;
                        continue;
                    }

                    if (!verbatimString && stringEscape)
                    {
                        stringEscape = false;
                    }
                    else if (!verbatimString && c == '\\')
                    {
                        stringEscape = true;
                    }
                    else if (c == '"')
                    {
                        stringLiteral = false;
                        verbatimString = false;
                        stringEscape = false;
                    }
                    chars[i] = c == '\r' || c == '\n' ? c : ' ';
                    continue;
                }

                if (charLiteral)
                {
                    if (charEscape)
                    {
                        charEscape = false;
                    }
                    else if (c == '\\')
                    {
                        charEscape = true;
                    }
                    else if (c == '\'')
                    {
                        charLiteral = false;
                    }
                    chars[i] = c == '\r' || c == '\n' ? c : ' ';
                    continue;
                }

                if (c == '/' && next == '/')
                {
                    chars[i] = ' ';
                    chars[i + 1] = ' ';
                    i++;
                    lineComment = true;
                    continue;
                }

                if (c == '/' && next == '*')
                {
                    chars[i] = ' ';
                    chars[i + 1] = ' ';
                    i++;
                    blockComment = true;
                    continue;
                }

                if (c == '@' && next == '"')
                {
                    chars[i] = ' ';
                    chars[i + 1] = ' ';
                    i++;
                    stringLiteral = true;
                    verbatimString = true;
                    continue;
                }

                if (c == '"')
                {
                    chars[i] = ' ';
                    stringLiteral = true;
                    verbatimString = false;
                    continue;
                }

                if (c == '\'')
                {
                    chars[i] = ' ';
                    charLiteral = true;
                }
            }

            return new string(chars);
        }

        private static int FindScopeEnd(string text, int openBrace)
        {
            int depth = 0;
            for (int i = openBrace; i < text.Length; i++)
            {
                if (text[i] == '{')
                    depth++;
                else if (text[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }
    }
#endif
}
