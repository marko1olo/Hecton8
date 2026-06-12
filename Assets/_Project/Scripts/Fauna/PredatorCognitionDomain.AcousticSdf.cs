using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Core.Scheduling;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.AI
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct AcousticStimulusDTO
    {
        [FieldOffset(0)]
        public double3 EpicenterAUP;
        [FieldOffset(24)]
        public float InitialIntensity;
        [FieldOffset(28)]
        public uint SoundTypeHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    internal struct AcousticEvaluationResultDTO
    {
        [FieldOffset(0)]
        public double3 SourceAUP;
        [FieldOffset(24)]
        public float3 RuntimeSourcePosition;
        [FieldOffset(36)]
        public float3 Direction;
        [FieldOffset(48)]
        public float ReceivedIntensity;
        [FieldOffset(52)]
        public float RawInverseSquareIntensity;
        [FieldOffset(56)]
        public float OcclusionMultiplier;
        [FieldOffset(60)]
        public uint ListenerEntityHash;
        [FieldOffset(64)]
        public uint SoundTypeHash;
        [FieldOffset(68)]
        public ushort ListenerSlot;
        [FieldOffset(70)]
        public ushort SourceIndex;
        [FieldOffset(72)]
        public byte Flags;
        [FieldOffset(73)]
        public byte RaySteps;
        [FieldOffset(74)]
        public ushort Reserved0;
        [FieldOffset(76)]
        public uint Reserved1;
        [FieldOffset(80)]
        public ulong Reserved2;
        [FieldOffset(88)]
        public ulong Reserved3;
        [FieldOffset(96)]
        public ulong Reserved4;
        [FieldOffset(104)]
        public ulong Reserved5;
        [FieldOffset(112)]
        public ulong Reserved6;
        [FieldOffset(120)]
        public ulong Reserved7;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct SensoryTelemetryEntry
    {
        [FieldOffset(0)]
        public uint Frame;
        [FieldOffset(4)]
        public ushort ActivePredators;
        [FieldOffset(6)]
        public ushort StimulusCount;
        [FieldOffset(8)]
        public ushort HeardPredators;
        [FieldOffset(10)]
        public ushort OccludedEvaluations;
        [FieldOffset(12)]
        public float MaxReceivedIntensity;
        [FieldOffset(16)]
        public float MaxRawIntensity;
        [FieldOffset(20)]
        public float GlobalQualityWeight;
        [FieldOffset(24)]
        public float EstimatedMicroseconds;
        [FieldOffset(28)]
        public int RaySteps;
        [FieldOffset(32)]
        public uint FaultFlags;
        [FieldOffset(36)]
        public uint StateHash;
        [FieldOffset(40)]
        public float3 HottestSourceRuntime;
        [FieldOffset(52)]
        public uint HottestSoundTypeHash;
        [FieldOffset(56)]
        public uint Reserved0;
        [FieldOffset(60)]
        public uint Reserved1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct AcousticHearingProfileDTO
    {
        [FieldOffset(0)]
        public uint SpeciesHash;
        [FieldOffset(4)]
        public float HearingThreshold;
        [FieldOffset(8)]
        public float FearGain;
        [FieldOffset(12)]
        public float AggressionGain;
        [FieldOffset(16)]
        public float MaxDistanceSq;
        [FieldOffset(20)]
        public float MechanicalFearBias;
        [FieldOffset(24)]
        public float PreyAggressionBias;
        [FieldOffset(28)]
        public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct AcousticTuningDTO
    {
        [FieldOffset(0)]
        public float WaterAttenuationScalar;
        [FieldOffset(4)]
        public float RockOcclusionMultiplier;
        [FieldOffset(8)]
        public float MinReceivedThreshold;
        [FieldOffset(12)]
        public float MaxDistanceMeters;
        [FieldOffset(16)]
        public float GlobalQualityWeight;
        [FieldOffset(20)]
        public float RayStepScale;
        [FieldOffset(24)]
        public float FaultMicroseconds;
        [FieldOffset(28)]
        public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct AcousticCounter64DTO
    {
        [FieldOffset(0)]
        public int Value;
        [FieldOffset(4)]
        public int Capacity;
        [FieldOffset(8)]
        public uint Flags;
        [FieldOffset(12)]
        public uint Reserved0;
        [FieldOffset(16)]
        public ulong Reserved1;
        [FieldOffset(24)]
        public ulong Reserved2;
        [FieldOffset(32)]
        public ulong Reserved3;
        [FieldOffset(40)]
        public ulong Reserved4;
        [FieldOffset(48)]
        public ulong Reserved5;
        [FieldOffset(56)]
        public ulong Reserved6;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AcousticSensoryTelemetrySnapshot
    {
        [FieldOffset(0)]
        public uint Frame;
        [FieldOffset(4)]
        public int ActivePredators;
        [FieldOffset(8)]
        public int StimulusCount;
        [FieldOffset(12)]
        public int HeardPredators;
        [FieldOffset(16)]
        public int OccludedEvaluations;
        [FieldOffset(20)]
        public float MaxReceivedIntensity;
        [FieldOffset(24)]
        public float MaxRawIntensity;
        [FieldOffset(28)]
        public float GlobalQualityWeight;
        [FieldOffset(32)]
        public float EstimatedMicroseconds;
        [FieldOffset(36)]
        public int RaySteps;
        [FieldOffset(40)]
        public uint FaultFlags;
        [FieldOffset(44)]
        public uint StateHash;
        [FieldOffset(48)]
        public float3 HottestSourceRuntime;
        [FieldOffset(60)]
        public uint HottestSoundTypeHash;
    }

    public struct AcousticSensoryTuningSnapshot
    {
        public float WaterAttenuationScalar;
        public float RockOcclusionMultiplier;
        public float MinReceivedThreshold;
        public float MaxDistanceMeters;
        public float RayStepScale;
        public float FaultMicroseconds;
        public uint Flags;
    }

    public struct AcousticSensoryResultSnapshot
    {
        public int Slot;
        public uint ListenerEntityHash;
        public uint SoundTypeHash;
        public float ReceivedIntensity;
        public float RawInverseSquareIntensity;
        public float OcclusionMultiplier;
        public float3 Direction;
        public float3 RuntimeSourcePosition;
        public byte Flags;
    }

    public static class PredatorAcousticSensoryDiagnostics
    {
        public static bool TryReadLatestTelemetry(out AcousticSensoryTelemetrySnapshot snapshot)
        {
            return PredatorCognitionDomain.TryReadAcousticSdfTelemetry(out snapshot);
        }

        public static bool TryReadResult(int slot, out AcousticSensoryResultSnapshot snapshot)
        {
            return PredatorCognitionDomain.TryReadAcousticSdfResult(slot, out snapshot);
        }

        public static bool TryReadStimulus(int index, out AcousticStimulusDTO stimulus)
        {
            return PredatorCognitionDomain.TryReadAcousticSdfStimulus(index, out stimulus);
        }

        public static int ReadStimulusCount()
        {
            return PredatorCognitionDomain.ReadAcousticSdfStimulusCount();
        }

        public static bool TryReadTuning(out AcousticSensoryTuningSnapshot snapshot)
        {
            return PredatorCognitionDomain.TryReadAcousticSdfTuning(out snapshot);
        }

        public static bool TryWriteTuning(in AcousticSensoryTuningSnapshot snapshot)
        {
            return PredatorCognitionDomain.TryWriteAcousticSdfTuning(in snapshot);
        }
    }

    internal static partial class PredatorCognitionDomain
    {
        private const int AcousticStimulusCapacity = 128;
        private const int AcousticHearingProfileCapacity = 64;
        private const int AcousticCsvScratchBytes = 16 * 1024;
        private const int AcousticTelemetryCapacity = 300;
        private const int AcousticStimulusDtoSizeBytes = 32;
        private const int AcousticEvaluationResultDtoSizeBytes = 128;
        private const int SensoryTelemetryEntrySizeBytes = 64;
        private const int AcousticHearingProfileDtoSizeBytes = 32;
        private const int AcousticTuningDtoSizeBytes = 32;
        private const int AcousticCounter64DtoSizeBytes = 64;
        private const BufferID AcousticStimuliBufferId = BufferID.PredatorCognitionDomain_AcousticSdf_AcousticStimuliBufferId;
        private const BufferID AcousticStimulusCountBufferId = BufferID.PredatorCognitionDomain_AcousticSdf_AcousticStimulusCountBufferId;
        private const BufferID AcousticResultsBufferId = BufferID.PredatorCognitionDomain_AcousticSdf_AcousticResultsBufferId;
        private const BufferID AcousticTelemetryRingBufferId = BufferID.PredatorCognitionDomain_AcousticSdf_AcousticTelemetryRingBufferId;
        private const BufferID AcousticTelemetryCursorBufferId = BufferID.PredatorCognitionDomain_AcousticSdf_AcousticTelemetryCursorBufferId;
        private const BufferID AcousticHearingProfilesBufferId = BufferID.PredatorCognitionDomain_AcousticSdf_AcousticHearingProfilesBufferId;
        private const BufferID AcousticHearingProfileCountBufferId = BufferID.PredatorCognitionDomain_AcousticSdf_AcousticHearingProfileCountBufferId;
        private const BufferID AcousticTuningBufferId = BufferID.PredatorCognitionDomain_AcousticSdf_AcousticTuningBufferId;
        private const BufferID AcousticCsvScratchBufferId = BufferID.PredatorCognitionDomain_AcousticSdf_AcousticCsvScratchBufferId;
        private const uint AcousticSoundMovementHash = 0x4D4F5645u; // MOVE
        private const uint AcousticSoundDamageHash = 0x444D4747u; // DMGG
        private const uint AcousticSoundSonarHash = 0x534F4E52u; // SONR
        private const uint AcousticSoundMechanicalHash = 0x4D454348u; // MECH
        private const uint AcousticSoundPreyHash = 0x50524559u; // PREY
        private const uint AcousticSoundMockHash = 0x4D4F434Bu; // MOCK
        private const uint AcousticTuningFlagMockSignals = 1u;
        private const byte AcousticResultHeardFlag = 1;
        private const byte AcousticResultOccludedFlag = 1 << 1;
        private const byte AcousticResultMechanicalFearFlag = 1 << 2;
        private const byte AcousticResultPreyAggressionFlag = 1 << 3;
        private const uint AcousticFaultBudgetExceeded = 1u;
        private const uint AcousticFaultNonFinite = 1u << 1;
        private const uint AcousticFaultStimulusOverflow = 1u << 2;
        private const uint AcousticCounterFlagPendingRetry = 1u;
        private const uint AcousticCounterFlagStimulusOverflow = 1u << 1;
        private const uint AcousticCounterFlagInvalidIngress = 1u << 2;
        private const string AcousticProfilesCsvName = "fauna_hearing_profiles.csv";

        private static VaultArray<AcousticStimulusDTO> _acousticSdfStimuli;
        private static VaultArray<AcousticCounter64DTO> _acousticSdfStimulusCounter;
        private static VaultArray<AcousticEvaluationResultDTO> _acousticSdfResults;
        private static VaultArray<SensoryTelemetryEntry> _acousticSdfTelemetryRing;
        private static VaultArray<int> _acousticSdfTelemetryCursor;
        private static VaultArray<AcousticHearingProfileDTO> _acousticHearingProfiles;
        private static VaultArray<int> _acousticHearingProfileCount;
        private static VaultArray<AcousticTuningDTO> _acousticSdfTuning;
        private static VaultArray<byte> _acousticCsvScratch;
        private static bool _acousticSdfEvaluationJobScheduled;
        private static bool _acousticSdfDefaultsInitialized;
        private static bool _acousticProfilesLoadAttempted;
        private static bool _acousticSdfDumpPathInitialized;
        private static bool _acousticSdfFaultDumped;
        private static int _acousticSdfLastPreparedFrame = -1;
        private static int _acousticSdfLastDumpFrame = -AcousticTelemetryCapacity;
        private static float _acousticSdfLastChainMicroseconds;
        private static bool _acousticSdfPendingStimulusRetry;
        private static string _acousticSdfDumpPath;

        private static bool ValidateAcousticSdfAbiLayout()
        {
            return UnsafeUtility.SizeOf<AcousticStimulusDTO>() == AcousticStimulusDtoSizeBytes &&
                   UnsafeUtility.SizeOf<AcousticEvaluationResultDTO>() == AcousticEvaluationResultDtoSizeBytes &&
                   UnsafeUtility.SizeOf<SensoryTelemetryEntry>() == SensoryTelemetryEntrySizeBytes &&
                   UnsafeUtility.SizeOf<AcousticHearingProfileDTO>() == AcousticHearingProfileDtoSizeBytes &&
                   UnsafeUtility.SizeOf<AcousticTuningDTO>() == AcousticTuningDtoSizeBytes &&
                   UnsafeUtility.SizeOf<AcousticCounter64DTO>() == AcousticCounter64DtoSizeBytes &&
                   UnsafeUtility.AlignOf<AcousticCounter64DTO>() >= 8 &&
                   UnsafeUtility.AlignOf<AcousticEvaluationResultDTO>() >= 8 &&
                   UnsafeUtility.AlignOf<AcousticStimulusDTO>() == 8;
        }

        private static bool EnsureAcousticSdfVaultBuffers()
        {
            if (AreAcousticSdfVaultBuffersReady())
            {
                EnsureAcousticSdfDumpPathCold();
                return true;
            }

            IDataVault vault = _dataVault;
            if (vault == null || vault.IsAllocationLocked)
                return false;

            _dataVault = vault;
            _acousticSdfStimuli = GetVaultArray<AcousticStimulusDTO>(
                AcousticStimuliBufferId,
                AcousticStimulusCapacity,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            _acousticSdfStimulusCounter = GetVaultArray<AcousticCounter64DTO>(
                AcousticStimulusCountBufferId,
                1,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            _acousticSdfResults = GetVaultArray<AcousticEvaluationResultDTO>(
                AcousticResultsBufferId,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            _acousticSdfTelemetryRing = GetVaultArray<SensoryTelemetryEntry>(
                AcousticTelemetryRingBufferId,
                AcousticTelemetryCapacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _acousticSdfTelemetryCursor = GetVaultArray<int>(
                AcousticTelemetryCursorBufferId,
                1,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _acousticHearingProfiles = GetVaultArray<AcousticHearingProfileDTO>(
                AcousticHearingProfilesBufferId,
                AcousticHearingProfileCapacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _acousticHearingProfileCount = GetVaultArray<int>(
                AcousticHearingProfileCountBufferId,
                1,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _acousticSdfTuning = GetVaultArray<AcousticTuningDTO>(
                AcousticTuningBufferId,
                1,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            _acousticCsvScratch = GetVaultArray<byte>(
                AcousticCsvScratchBufferId,
                AcousticCsvScratchBytes,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);

            bool created = _acousticSdfStimuli.IsCreated &&
                           _acousticSdfStimulusCounter.IsCreated &&
                           _acousticSdfResults.IsCreated &&
                           _acousticSdfTelemetryRing.IsCreated &&
                           _acousticSdfTelemetryCursor.IsCreated &&
                           _acousticHearingProfiles.IsCreated &&
                           _acousticHearingProfileCount.IsCreated &&
                           _acousticSdfTuning.IsCreated &&
                           _acousticCsvScratch.IsCreated;
            if (created)
                InitializeAcousticSdfCold();

            return created;
        }

        private static bool AreAcousticSdfVaultBuffersReady()
        {
            return _acousticSdfStimuli.IsCreated &&
                   _acousticSdfStimulusCounter.IsCreated &&
                   _acousticSdfResults.IsCreated &&
                   _acousticSdfTelemetryRing.IsCreated &&
                   _acousticSdfTelemetryCursor.IsCreated &&
                   _acousticHearingProfiles.IsCreated &&
                   _acousticHearingProfileCount.IsCreated &&
                   _acousticSdfTuning.IsCreated &&
                   _acousticCsvScratch.IsCreated;
        }

        private static void InitializeAcousticSdfCold()
        {
            if (!_acousticSdfDefaultsInitialized)
            {
                NativeArray<AcousticCounter64DTO> count = _acousticSdfStimulusCounter.Open();
                NativeArray<int> profileCount = _acousticHearingProfileCount.Open();
                NativeArray<AcousticHearingProfileDTO> profiles = _acousticHearingProfiles.Open();
                NativeArray<AcousticTuningDTO> tuning = _acousticSdfTuning.Open();
                if (count.IsCreated)
                    count[0] = CreateAcousticCounter(0, AcousticStimulusCapacity);
                if (profileCount.IsCreated)
                    profileCount[0] = 1;
                if (profiles.IsCreated && profiles.Length > 0)
                    profiles[0] = CreateDefaultAcousticProfile();
                if (tuning.IsCreated && tuning.Length > 0)
                    tuning[0] = CreateDefaultAcousticTuning(ResolveAcousticGlobalQualityWeight());

                _acousticSdfDefaultsInitialized = true;
            }

            EnsureAcousticSdfDumpPathCold();
            if (!_acousticProfilesLoadAttempted)
            {
                _acousticProfilesLoadAttempted = true;
                TryLoadAcousticHearingProfilesCsvCold();
            }
        }

        private static AcousticHearingProfileDTO CreateDefaultAcousticProfile()
        {
            AcousticHearingProfileDTO profile = default;
            profile.SpeciesHash = 0u;
            profile.HearingThreshold = AcousticStimulusThreshold;
            profile.FearGain = 0.55f;
            profile.AggressionGain = 0.45f;
            profile.MaxDistanceSq = 2500f;
            profile.MechanicalFearBias = 1.35f;
            profile.PreyAggressionBias = 1.2f;
            profile.Flags = 0u;
            return profile;
        }

        private static AcousticTuningDTO CreateDefaultAcousticTuning(float qualityWeight)
        {
            AcousticTuningDTO tuning = default;
            tuning.WaterAttenuationScalar = 1f;
            tuning.RockOcclusionMultiplier = 0.18f;
            tuning.MinReceivedThreshold = AcousticStimulusThreshold;
            tuning.MaxDistanceMeters = 50f;
            tuning.GlobalQualityWeight = math.saturate(qualityWeight);
            tuning.RayStepScale = 1f;
            tuning.FaultMicroseconds = 1000f;
            tuning.Flags = 0u;
            return tuning;
        }

        private static AcousticCounter64DTO CreateAcousticCounter(int value, int capacity)
        {
            AcousticCounter64DTO counter = default;
            counter.Value = math.max(0, value);
            counter.Capacity = math.clamp(capacity, 0, AcousticStimulusCapacity);
            return counter;
        }

        private static bool TryReadAcousticPublishedVoxelSdfSnapshot(
            IDataVault vault,
            out NativeArray<byte>.ReadOnly encodedSdf,
            out int3 dimensions,
            out float3 runtimeOrigin,
            out float3 cellSize)
        {
            encodedSdf = default;
            dimensions = int3.zero;
            runtimeOrigin = float3.zero;
            cellSize = new float3(1f);
            if (vault == null ||
                !vault.TryGetGenerationHandle<VoxelSdfPayloadDescriptorDTO>(
                    BufferID.VoxelSdfPayloadDescriptor,
                    out VaultGenerationHandle<VoxelSdfPayloadDescriptorDTO> descriptorHandle) ||
                descriptorHandle.BufferID != unchecked((uint)(int)BufferID.VoxelSdfPayloadDescriptor) ||
                descriptorHandle.SystemID != (uint)SystemID.WorldStreaming ||
                descriptorHandle.Generation == 0u ||
                !vault.TryReadHandle(in descriptorHandle, out NativeArray<VoxelSdfPayloadDescriptorDTO> descriptors) ||
                !descriptors.IsCreated ||
                descriptors.Length <= 0)
            {
                return false;
            }

            VoxelSdfPayloadDescriptorDTO descriptor = descriptors[0];
            int expectedLength = ResolveAcousticVoxelSdfByteCount(descriptor.GridDimensions);
            if (expectedLength <= 0 ||
                descriptor.ByteCount != expectedLength ||
                descriptor.BufferId != unchecked((uint)(int)BufferID.VoxelSdfTexture3D) ||
                descriptor.OwnerSystemId != (uint)SystemID.WorldStreaming ||
                (descriptor.Flags & VoxelSdfPayloadDescriptorDTO.FlagValid) == 0u ||
                !vault.TryGetGenerationHandle<byte>(
                    BufferID.VoxelSdfTexture3D,
                    out VaultGenerationHandle<byte> sdfHandle) ||
                sdfHandle.BufferID != unchecked((uint)(int)BufferID.VoxelSdfTexture3D) ||
                sdfHandle.SystemID != (uint)SystemID.WorldStreaming ||
                sdfHandle.Generation == 0u ||
                sdfHandle.Generation != descriptor.BufferGeneration ||
                !vault.TryReadHandle(in sdfHandle, out NativeArray<byte> sdfBytes) ||
                !sdfBytes.IsCreated ||
                sdfBytes.Length < expectedLength)
            {
                return false;
            }

            float3 safeCellSize = math.max(math.abs(descriptor.VoxelCellSize), new float3(0.0001f));
            if (!math.all(math.isfinite(descriptor.VolumeOrigin)) ||
                !math.all(math.isfinite(safeCellSize)))
            {
                return false;
            }

            encodedSdf = sdfBytes.AsReadOnly();
            dimensions = descriptor.GridDimensions;
            runtimeOrigin = descriptor.VolumeOrigin;
            cellSize = safeCellSize;
            return true;
        }

        private static int ResolveAcousticVoxelSdfByteCount(int3 dimensions)
        {
            if (dimensions.x <= 0 || dimensions.y <= 0 || dimensions.z <= 0)
                return 0;

            long count = (long)dimensions.x * dimensions.y * dimensions.z;
            return count > 0L && count <= int.MaxValue ? (int)count : 0;
        }

        private static float ResolveAcousticGlobalQualityWeight()
        {
            return math.saturate(SignalBusRegistry.GlobalQualityWeight01);
        }

        private static void PrepareAcousticSdfSignals(int frameId)
        {
            if (_acousticSdfLastPreparedFrame == frameId)
                return;

            _acousticSdfLastPreparedFrame = frameId;
            if (!AreAcousticSdfVaultBuffersReady())
                return;

            NativeArray<AcousticStimulusDTO> stimuli = _acousticSdfStimuli.Open();
            NativeArray<AcousticCounter64DTO> counter = _acousticSdfStimulusCounter.Open();
            if (!stimuli.IsCreated || !counter.IsCreated || counter.Length <= 0)
                return;

            if (_acousticSdfPendingStimulusRetry)
            {
                AcousticCounter64DTO retryCounter = counter[0];
                if (retryCounter.Value > 0)
                {
                    retryCounter.Flags |= AcousticCounterFlagPendingRetry;
                    counter[0] = retryCounter;
                    return;
                }

                _acousticSdfPendingStimulusRetry = false;
            }

            int capacity = math.min(stimuli.Length, AcousticStimulusCapacity);
            counter[0] = CreateAcousticCounter(0, capacity);
            int combatQuota = math.max(1, capacity >> 1);
            int pingQuota = math.max(1, capacity >> 2);
            int dropped = 0;
            dropped += AppendCombatDamageAcousticSignals(stimuli, counter, combatQuota);
            dropped += AppendAcousticPingSignals(stimuli, counter, pingQuota);
            AcousticCounter64DTO current = counter[0];
            int movementQuota = math.max(0, capacity - math.max(0, current.Value));
            dropped += AppendMovementAcousticSignals(stimuli, counter, movementQuota);
            current = counter[0];
            if (dropped > 0)
            {
                current.Flags |= AcousticCounterFlagStimulusOverflow;
                current.Reserved0 = unchecked((uint)dropped);
                counter[0] = current;
            }
        }

        private static int AppendMovementAcousticSignals(
            NativeArray<AcousticStimulusDTO> stimuli,
            NativeArray<AcousticCounter64DTO> counter,
            int maxWrites)
        {
            ReadOnlySpan<MovementAcousticSignal> signals = SignalBus<MovementAcousticSignal>.GetFrameSnapshot();
            int start = math.max(0, signals.Length - AcousticStimulusCapacity);
            int written = 0;
            int dropped = 0;
            for (int i = start; i < signals.Length; i++)
            {
                MovementAcousticSignal signal = signals[i];
                if (!math.isfinite(signal.Volume) || !math.isfinite(signal.VelocitySq))
                {
                    MarkAcousticCounterFlag(counter, AcousticCounterFlagInvalidIngress);
                    continue;
                }

                double3 epicenterAup = signal.PositionAup.ToAbsoluteDouble3();
                if (!math.all(math.isfinite(epicenterAup)))
                {
                    MarkAcousticCounterFlag(counter, AcousticCounterFlagInvalidIngress);
                    continue;
                }

                float intensity = math.max(signal.Volume, FastLengthFromSq(math.max(0f, signal.VelocitySq)) * 0.04f);
                if (intensity <= 0f)
                    continue;

                uint sourceFold = signal.SourceId * 16777619u;
                if (!AppendAcousticStimulus(
                    stimuli,
                    counter,
                    epicenterAup,
                    intensity,
                    AcousticSoundMovementHash ^ sourceFold,
                    maxWrites,
                    ref written))
                {
                    dropped++;
                }
            }

            return dropped;
        }

        private static int AppendAcousticPingSignals(
            NativeArray<AcousticStimulusDTO> stimuli,
            NativeArray<AcousticCounter64DTO> counter,
            int maxWrites)
        {
            ReadOnlySpan<AcousticPingSignal> signals = SignalBus<AcousticPingSignal>.GetFrameSnapshot();
            int start = math.max(0, signals.Length - AcousticStimulusCapacity);
            int written = 0;
            int dropped = 0;
            for (int i = start; i < signals.Length; i++)
            {
                AcousticPingSignal signal = signals[i];
                if (!math.isfinite(signal.RadiusMeters) || !math.isfinite(signal.Intensity01))
                {
                    MarkAcousticCounterFlag(counter, AcousticCounterFlagInvalidIngress);
                    continue;
                }

                double3 epicenterAup = signal.PositionAup.ToAbsoluteDouble3();
                if (!math.all(math.isfinite(epicenterAup)))
                {
                    MarkAcousticCounterFlag(counter, AcousticCounterFlagInvalidIngress);
                    continue;
                }

                uint typeHash = ResolveAcousticPingSoundHash(in signal) ^ (signal.SourceId * 16777619u);
                float radiusBoost = math.max(0f, signal.RadiusMeters) * 0.0025f;
                float intensity = math.max(signal.Intensity01, radiusBoost);
                if (intensity <= 0f)
                    continue;

                if (!AppendAcousticStimulus(
                    stimuli,
                    counter,
                    epicenterAup,
                    intensity,
                    typeHash,
                    maxWrites,
                    ref written))
                {
                    dropped++;
                }
            }

            return dropped;
        }

        private static int AppendCombatDamageAcousticSignals(
            NativeArray<AcousticStimulusDTO> stimuli,
            NativeArray<AcousticCounter64DTO> counter,
            int maxWrites)
        {
            ReadOnlySpan<CombatDamageSignal> signals = SignalBus<CombatDamageSignal>.GetFrameSnapshot();
            int start = math.max(0, signals.Length - AcousticStimulusCapacity);
            int written = 0;
            int dropped = 0;
            for (int i = start; i < signals.Length; i++)
            {
                CombatDamageSignal signal = signals[i];
                if ((signal.Flags & CombatDamageSignal.VisualOnlyFlag) != 0)
                    continue;

                if (!math.isfinite(signal.Magnitude) || !math.all(math.isfinite(signal.ImpactAup)))
                {
                    MarkAcousticCounterFlag(counter, AcousticCounterFlagInvalidIngress);
                    continue;
                }

                float intensity = math.max(0f, signal.Magnitude) * 0.02f;
                if (intensity <= 0f)
                    continue;

                uint sourceFold = (signal.SourceHash ^ signal.TargetHash ^ signal.DamageType) * 16777619u;
                if (!AppendAcousticStimulus(
                    stimuli,
                    counter,
                    signal.ImpactAup,
                    intensity,
                    AcousticSoundDamageHash ^ sourceFold,
                    maxWrites,
                    ref written))
                {
                    dropped++;
                }
            }

            return dropped;
        }

        private static uint ResolveAcousticPingSoundHash(in AcousticPingSignal signal)
        {
            if ((signal.Flags & AcousticPingSignal.FlagActiveSonar) != 0 ||
                signal.Channel == AcousticPingSignal.ChannelActiveSonar)
            {
                return AcousticSoundSonarHash;
            }

            if ((signal.Flags & AcousticPingSignal.FlagJawSnap) != 0 ||
                signal.Channel == AcousticPingSignal.ChannelJawSnap)
            {
                return AcousticSoundPreyHash;
            }

            if (signal.Channel == AcousticPingSignal.ChannelMetalStress ||
                signal.Channel == AcousticPingSignal.ChannelGloveScrape ||
                (signal.Flags & AcousticPingSignal.FlagGloveScrape) != 0)
            {
                return AcousticSoundMechanicalHash;
            }

            return AcousticSoundMovementHash;
        }

        private static bool AppendAcousticStimulus(
            NativeArray<AcousticStimulusDTO> stimuli,
            NativeArray<AcousticCounter64DTO> counter,
            double3 epicenterAup,
            float intensity,
            uint soundTypeHash,
            int maxLaneWrites,
            ref int laneWrites)
        {
            if (!math.all(math.isfinite(epicenterAup)) ||
                !math.isfinite(intensity) ||
                intensity <= 0f)
            {
                return false;
            }

            AcousticCounter64DTO current = counter[0];
            int writeIndex = current.Value;
            int capacity = math.min(stimuli.Length, math.max(0, current.Capacity));
            if ((uint)writeIndex >= (uint)capacity || laneWrites >= math.max(0, maxLaneWrites))
                return false;

            AcousticStimulusDTO stimulus = default;
            stimulus.EpicenterAUP = epicenterAup;
            stimulus.InitialIntensity = intensity;
            stimulus.SoundTypeHash = soundTypeHash == 0u ? AcousticSoundMovementHash : soundTypeHash;
            stimuli[writeIndex] = stimulus;
            current.Value = writeIndex + 1;
            counter[0] = current;
            laneWrites++;
            return true;
        }

        private static void MarkAcousticCounterFlag(NativeArray<AcousticCounter64DTO> counter, uint flag)
        {
            if (!counter.IsCreated || counter.Length <= 0)
                return;

            AcousticCounter64DTO current = counter[0];
            current.Flags |= flag;
            counter[0] = current;
        }

        private static void MarkAcousticSdfPendingRetry(int frameId)
        {
            _acousticSdfPendingStimulusRetry = true;
            if (!_acousticSdfStimulusCounter.IsCreated)
                return;

            NativeArray<AcousticCounter64DTO> counter = _acousticSdfStimulusCounter.Open();
            if (!counter.IsCreated || counter.Length <= 0)
                return;

            AcousticCounter64DTO current = counter[0];
            if (current.Value <= 0)
                return;

            current.Flags |= AcousticCounterFlagPendingRetry;
            counter[0] = current;
        }

        private static void ClearAcousticSdfPendingRetryLatch()
        {
            _acousticSdfPendingStimulusRetry = false;
        }

        private static bool MarkAcousticSdfDueWhenStimuliPresent()
        {
            if (!_acousticSdfStimulusCounter.IsCreated)
                return false;

            NativeArray<AcousticCounter64DTO> counter = _acousticSdfStimulusCounter.OpenRead();
            bool hasRealStimuli = false;
            if (counter.IsCreated && counter.Length > 0)
            {
                AcousticCounter64DTO current = counter[0];
                hasRealStimuli = current.Value > 0;
            }

            if (!hasRealStimuli && !IsAcousticMockSignalModeEnabled())
                return false;

            bool hasDue = false;
            for (int i = 0; i < _activeSlotCount; i++)
            {
                int slot = _activeSlots[i];
                CognitionInput input = _inputs[slot];
                if ((input.Flags & (int)CognitionInputFlags.Active) == 0 ||
                    (input.Flags & (int)CognitionInputFlags.PredatorRole) == 0)
                {
                    continue;
                }

                _evaluationDueFlags[slot] = 1;
                hasDue = true;
            }

            return hasDue;
        }

        private static bool HasAcousticSdfWorkPending()
        {
            bool hasStimuli = false;
            if (_acousticSdfStimulusCounter.IsCreated)
            {
                NativeArray<AcousticCounter64DTO> counter = _acousticSdfStimulusCounter.OpenRead();
                if (counter.IsCreated && counter.Length > 0)
                    hasStimuli = counter[0].Value > 0;
            }

            return hasStimuli || IsAcousticMockSignalModeEnabled();
        }

        private static void RecordAcousticSdfIdleTelemetryFromCurrentTuning(int frameId)
        {
            if (!AreAcousticSdfVaultBuffersReady())
                return;

            float globalQualityWeight = ResolveAcousticGlobalQualityWeight();
            float rayStepScale = 1f;
            if (_acousticSdfTuning.IsCreated)
            {
                NativeArray<AcousticTuningDTO> tuningArray = _acousticSdfTuning.Open();
                if (tuningArray.IsCreated && tuningArray.Length > 0)
                {
                    AcousticTuningDTO tuning = tuningArray[0];
                    tuning = SanitizeAcousticTuning(in tuning);
                    tuning.GlobalQualityWeight = globalQualityWeight;
                    tuningArray[0] = tuning;
                    rayStepScale = tuning.RayStepScale;
                }
            }

            int raySteps = ResolveAcousticRaySteps(globalQualityWeight, rayStepScale);
            RecordAcousticSdfIdleTelemetryAndClearResults(
                frameId,
                _activeSlotCount,
                globalQualityWeight,
                raySteps);
        }

        private static unsafe JobHandle ScheduleAcousticSdfIntegration(int frameId, JobHandle dependency)
        {
            if (!AreAcousticSdfVaultBuffersReady())
                return dependency;

            NativeArray<AcousticStimulusDTO> stimuli = _acousticSdfStimuli.Open();
            NativeArray<AcousticCounter64DTO> stimulusCounter = _acousticSdfStimulusCounter.Open();
            NativeArray<AcousticEvaluationResultDTO> results = _acousticSdfResults.Open();
            NativeArray<SensoryTelemetryEntry> telemetryRing = _acousticSdfTelemetryRing.Open();
            NativeArray<int> telemetryCursor = _acousticSdfTelemetryCursor.Open();
            NativeArray<AcousticHearingProfileDTO> profiles = _acousticHearingProfiles.Open();
            NativeArray<int> profileCount = _acousticHearingProfileCount.Open();
            NativeArray<AcousticTuningDTO> tuningArray = _acousticSdfTuning.Open();
            if (!stimuli.IsCreated ||
                !stimulusCounter.IsCreated ||
                !results.IsCreated ||
                !telemetryRing.IsCreated ||
                !telemetryCursor.IsCreated ||
                !profiles.IsCreated ||
                !profileCount.IsCreated ||
                !tuningArray.IsCreated)
            {
                return dependency;
            }

            AcousticTuningDTO tuning = tuningArray[0];
            tuning.GlobalQualityWeight = ResolveAcousticGlobalQualityWeight();
            tuning = SanitizeAcousticTuning(in tuning);
            tuningArray[0] = tuning;
            AcousticCounter64DTO stagedCounter = stimulusCounter[0];
            int stagedCount = math.min(math.max(0, stagedCounter.Value), stimuli.Length);
            stagedCounter.Value = stagedCount;
            stagedCounter.Capacity = math.min(stimuli.Length, AcousticStimulusCapacity);
            stimulusCounter[0] = stagedCounter;
            int mockWriteCount = 0;
            if (stagedCount <= 0 &&
                (tuning.Flags & AcousticTuningFlagMockSignals) != 0u &&
                _activeSlotCount > 0)
            {
                mockWriteCount = math.min(math.min(stimuli.Length, ResolveAcousticMockCount(tuning.GlobalQualityWeight)), _activeSlotCount);
                stimulusCounter[0] = CreateAcousticCounter(mockWriteCount, mockWriteCount);
            }

            int raySteps = ResolveAcousticRaySteps(tuning.GlobalQualityWeight, tuning.RayStepScale);
            if (stagedCount <= 0 && mockWriteCount <= 0)
                return dependency;

            ClearAcousticSdfPendingRetryLatch();
            var mockJob = new GenerateMockAcousticSignalsJob
            {
                ActiveSlots = _activeSlots,
                Inputs = _inputs,
                Stimuli = stimuli,
                ActiveSlotCount = _activeSlotCount,
                MockCapacity = mockWriteCount,
                FrameId = frameId,
                GlobalQualityWeight = tuning.GlobalQualityWeight
            };
            JobHandle mockHandle = mockWriteCount > 0
                ? mockJob.Schedule(mockWriteCount, EvaluationJobBatchSize, dependency)
                : dependency;
            var attenuationJob = new CalculateAcousticAttenuationJob
            {
                ActiveSlots = _activeSlots,
                Inputs = _inputs,
                DueFlags = _evaluationDueFlags,
                Stimuli = stimuli,
                StimulusCounter = stimulusCounter,
                Profiles = profiles,
                ProfileCount = profileCount,
                Results = results,
                ActiveSlotCount = _activeSlotCount,
                Tuning = tuning,
                RaySteps = raySteps
            };
            JobHandle attenuationHandle = attenuationJob.Schedule(_activeSlotCount, EvaluationJobBatchSize, mockHandle);
            NativeArray<byte>.ReadOnly acousticSdfGrid = default;
            int3 acousticSdfDimensions = int3.zero;
            float3 acousticSdfOrigin = float3.zero;
            float3 acousticSdfCellSize = new float3(1f);
            TryReadAcousticPublishedVoxelSdfSnapshot(
                _dataVault,
                out acousticSdfGrid,
                out acousticSdfDimensions,
                out acousticSdfOrigin,
                out acousticSdfCellSize);
            var occlusionJob = new EvaluateAcousticOcclusionJob
            {
                ActiveSlots = _activeSlots,
                Inputs = _inputs,
                DueFlags = _evaluationDueFlags,
                Stimuli = stimuli,
                StimulusCounter = stimulusCounter,
                Profiles = profiles,
                ProfileCount = profileCount,
                Results = results,
                AcousticMemoryBank = _acousticMemoryBank,
                AcousticMemoryFloat4Bank = ResolveAcousticMemoryFloat4Bank(),
                CorePtr = _cores.GetUnsafePtr(),
                ControlPtr = _controls.GetUnsafePtr(),
                CoreLength = _cores.Length,
                ControlLength = _controls.Length,
                ActiveSlotCount = _activeSlotCount,
                Tuning = tuning,
                RaySteps = raySteps,
                ThreatVoxelGrid = acousticSdfGrid,
                ThreatVoxelDimensions = acousticSdfDimensions,
                ThreatVoxelOrigin = acousticSdfOrigin,
                ThreatVoxelCellSize = acousticSdfCellSize,
                ThreatVoxelSolidThreshold = SignedDistanceSolidThreshold,
                ThreatVoxelUsesSignedDistanceEncoding = 1
            };
            JobHandle occlusionHandle = occlusionJob.Schedule(_activeSlotCount, EvaluationJobBatchSize, attenuationHandle);
            var telemetryJob = new RecordAcousticTelemetryJob
            {
                ActiveSlots = _activeSlots,
                Results = results,
                StimulusCounter = stimulusCounter,
                TelemetryRing = telemetryRing,
                TelemetryCursor = telemetryCursor,
                ActiveSlotCount = _activeSlotCount,
                FrameId = frameId,
                GlobalQualityWeight = tuning.GlobalQualityWeight,
                RaySteps = raySteps,
                FaultMicroseconds = tuning.FaultMicroseconds
            };
            JobHandle telemetryHandle = telemetryJob.Schedule(occlusionHandle);
            _acousticSdfEvaluationJobScheduled = true;
            return telemetryHandle;
        }

        private static unsafe void RecordAcousticSdfIdleTelemetryAndClearResults(
            int frameId,
            int activeSlotCount,
            float globalQualityWeight,
            int raySteps)
        {
            if (!_acousticSdfResults.IsCreated ||
                !_acousticSdfTelemetryRing.IsCreated ||
                !_acousticSdfTelemetryCursor.IsCreated)
            {
                return;
            }

            _acousticSdfLastChainMicroseconds = 0f;
            NativeArray<AcousticEvaluationResultDTO> results = _acousticSdfResults.Open();
            NativeArray<SensoryTelemetryEntry> telemetry = _acousticSdfTelemetryRing.Open();
            NativeArray<int> cursor = _acousticSdfTelemetryCursor.Open();
            if (!results.IsCreated ||
                !telemetry.IsCreated ||
                !cursor.IsCreated ||
                telemetry.Length <= 0 ||
                cursor.Length <= 0)
            {
                return;
            }

            int stagedStimuli = 0;
            uint droppedStimuli = 0u;
            uint counterFlags = 0u;
            if (_acousticSdfStimulusCounter.IsCreated)
            {
                NativeArray<AcousticCounter64DTO> counter = _acousticSdfStimulusCounter.OpenRead();
                if (counter.IsCreated && counter.Length > 0)
                {
                    AcousticCounter64DTO current = counter[0];
                    stagedStimuli = math.max(0, current.Value);
                    droppedStimuli = current.Reserved0;
                    counterFlags = current.Flags;
                }
            }

            int safeActiveCount = _activeSlots.IsCreated
                ? math.min(math.max(0, activeSlotCount), _activeSlots.Length)
                : 0;
            if (safeActiveCount > 0)
            {
                for (int i = 0; i < safeActiveCount; i++)
                {
                    int slot = _activeSlots[i];
                    if ((uint)slot < (uint)results.Length)
                        results[slot] = default;
                }
            }

            SensoryTelemetryEntry entry = default;
            entry.Frame = (uint)math.max(0, frameId);
            entry.ActivePredators = (ushort)math.clamp(safeActiveCount, 0, ushort.MaxValue);
            entry.StimulusCount = (ushort)math.clamp(stagedStimuli, 0, ushort.MaxValue);
            entry.GlobalQualityWeight = math.saturate(globalQualityWeight);
            entry.RaySteps = raySteps;
            entry.Reserved0 = droppedStimuli;
            entry.Reserved1 = counterFlags;
            if (droppedStimuli > 0u)
                entry.FaultFlags |= AcousticFaultStimulusOverflow;
            if ((counterFlags & AcousticCounterFlagInvalidIngress) != 0u)
                entry.FaultFlags |= AcousticFaultNonFinite;
            entry.StateHash = unchecked((2166136261u ^ (uint)math.max(0, frameId)) * 16777619u);
            int writeIndex = cursor[0];
            if ((uint)writeIndex >= (uint)telemetry.Length)
                writeIndex = 0;
            telemetry[writeIndex] = entry;
            writeIndex++;
            if (writeIndex >= telemetry.Length)
                writeIndex = 0;
            cursor[0] = writeIndex;

            if ((entry.FaultFlags & (AcousticFaultBudgetExceeded | AcousticFaultNonFinite)) != 0u)
                TryDumpAcousticSdfBlackBox(frameId);
        }

        private static AcousticTuningDTO SanitizeAcousticTuning(in AcousticTuningDTO source)
        {
            AcousticTuningDTO tuning = source;
            tuning.WaterAttenuationScalar = SanitizeRange(tuning.WaterAttenuationScalar, 1f, 0.05f, 8f);
            tuning.RockOcclusionMultiplier = SanitizeRange(tuning.RockOcclusionMultiplier, 0.18f, 0.01f, 1f);
            tuning.MinReceivedThreshold = SanitizeRange(tuning.MinReceivedThreshold, AcousticStimulusThreshold, 0.0001f, 1f);
            tuning.MaxDistanceMeters = SanitizeRange(tuning.MaxDistanceMeters, 50f, 4f, 250f);
            tuning.GlobalQualityWeight = math.saturate(math.select(1f, tuning.GlobalQualityWeight, math.isfinite(tuning.GlobalQualityWeight)));
            tuning.RayStepScale = SanitizeRange(tuning.RayStepScale, 1f, 0.25f, 2f);
            tuning.FaultMicroseconds = SanitizeRange(tuning.FaultMicroseconds, 1000f, 100f, 10000f);
            return tuning;
        }

        private static float SanitizeRange(float value, float fallback, float min, float max)
        {
            return math.clamp(math.select(fallback, value, math.isfinite(value)), min, max);
        }

        private static int ResolveAcousticRaySteps(float qualityWeight, float rayStepScale)
        {
            float q = math.saturate(qualityWeight);
            float smooth = q * q * (3f - (2f * q));
            return math.clamp((int)math.round(math.lerp(1f, 8f, smooth) * math.max(0.25f, rayStepScale)), 1, 8);
        }

        private static int ResolveAcousticMockCount(float qualityWeight)
        {
            float q = math.saturate(qualityWeight);
            return math.clamp((int)math.round(math.lerp(1f, 8f, q)), 1, 8);
        }

        private static bool IsAcousticMockSignalModeEnabled()
        {
            if (!_acousticSdfTuning.IsCreated)
                return false;

            NativeArray<AcousticTuningDTO> tuningArray = _acousticSdfTuning.OpenRead();
            if (!tuningArray.IsCreated || tuningArray.Length <= 0)
                return false;

            AcousticTuningDTO tuning = tuningArray[0];
            return (tuning.Flags & AcousticTuningFlagMockSignals) != 0u;
        }

        private static void FinalizeAcousticSdfTelemetry(int frameId, float chainMicroseconds)
        {
            _acousticSdfLastChainMicroseconds = math.select(0f, chainMicroseconds, math.isfinite(chainMicroseconds));
            PatchLatestAcousticTelemetryChainMicroseconds(chainMicroseconds);
            if (!TryReadAcousticSdfTelemetry(out AcousticSensoryTelemetrySnapshot snapshot))
                return;

            if ((snapshot.FaultFlags & AcousticFaultBudgetExceeded) != 0u ||
                (snapshot.FaultFlags & AcousticFaultNonFinite) != 0u ||
                snapshot.EstimatedMicroseconds > 1000f ||
                chainMicroseconds > 1000f)
            {
                TryDumpAcousticSdfBlackBox(frameId);
            }
        }

        private static unsafe void TryDumpAcousticSdfBlackBox(int frameId)
        {
            if (_acousticSdfFaultDumped &&
                unchecked((uint)(frameId - _acousticSdfLastDumpFrame)) < AcousticTelemetryCapacity)
            {
                return;
            }

            NativeArray<SensoryTelemetryEntry> telemetry = _acousticSdfTelemetryRing.OpenRead();
            if (!telemetry.IsCreated)
                return;

            _acousticSdfFaultDumped = true;
            _acousticSdfLastDumpFrame = frameId;
        }

        private static void EnsureAcousticSdfDumpPathCold()
        {
            if (_acousticSdfDumpPathInitialized)
                return;

            try
            {
                _acousticSdfDumpPath = string.Empty;
                _acousticSdfDumpPathInitialized = true;
            }
            catch (Exception)
            {
                _acousticSdfDumpPath = string.Empty;
                _acousticSdfDumpPathInitialized = false;
            }
        }

        private static void PatchLatestAcousticTelemetryChainMicroseconds(float chainMicroseconds)
        {
            if (!_acousticSdfTelemetryRing.IsCreated || !_acousticSdfTelemetryCursor.IsCreated)
                return;

            NativeArray<SensoryTelemetryEntry> telemetry = _acousticSdfTelemetryRing.Open();
            NativeArray<int> cursor = _acousticSdfTelemetryCursor.Open();
            if (!telemetry.IsCreated || !cursor.IsCreated || telemetry.Length <= 0 || cursor.Length <= 0)
                return;

            int index = cursor[0] - 1;
            if (index < 0)
                index += telemetry.Length;
            if ((uint)index >= (uint)telemetry.Length)
                return;

            SensoryTelemetryEntry entry = telemetry[index];
            if (entry.Frame == 0u && entry.ActivePredators == 0)
                return;

            float safeChainMicroseconds = math.select(0f, chainMicroseconds, math.isfinite(chainMicroseconds));
            entry.EstimatedMicroseconds = math.max(entry.EstimatedMicroseconds, safeChainMicroseconds);
            if (!math.isfinite(chainMicroseconds))
                entry.FaultFlags |= AcousticFaultNonFinite;
            if (entry.EstimatedMicroseconds > 1000f)
                entry.FaultFlags |= AcousticFaultBudgetExceeded;
            telemetry[index] = entry;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteUInt32LE(Span<byte> destination, int offset, uint value)
        {
            destination[offset] = (byte)value;
            destination[offset + 1] = (byte)(value >> 8);
            destination[offset + 2] = (byte)(value >> 16);
            destination[offset + 3] = (byte)(value >> 24);
        }

        internal static bool TryReadAcousticSdfTelemetry(out AcousticSensoryTelemetrySnapshot snapshot)
        {
            snapshot = default;
            if (_evaluationScheduled ||
                !_acousticSdfTelemetryRing.IsCreated ||
                !_acousticSdfTelemetryCursor.IsCreated)
                return false;

            NativeArray<SensoryTelemetryEntry> telemetry = _acousticSdfTelemetryRing.OpenRead();
            NativeArray<int> cursor = _acousticSdfTelemetryCursor.OpenRead();
            if (!telemetry.IsCreated || !cursor.IsCreated || telemetry.Length <= 0)
                return false;

            int index = cursor[0] - 1;
            if (index < 0)
                index += telemetry.Length;
            if ((uint)index >= (uint)telemetry.Length)
                return false;

            SensoryTelemetryEntry entry = telemetry[index];
            snapshot.Frame = entry.Frame;
            snapshot.ActivePredators = entry.ActivePredators;
            snapshot.StimulusCount = entry.StimulusCount;
            snapshot.HeardPredators = entry.HeardPredators;
            snapshot.OccludedEvaluations = entry.OccludedEvaluations;
            snapshot.MaxReceivedIntensity = entry.MaxReceivedIntensity;
            snapshot.MaxRawIntensity = entry.MaxRawIntensity;
            snapshot.GlobalQualityWeight = entry.GlobalQualityWeight;
            snapshot.EstimatedMicroseconds = math.max(entry.EstimatedMicroseconds, _acousticSdfLastChainMicroseconds);
            snapshot.RaySteps = entry.RaySteps;
            snapshot.FaultFlags = entry.FaultFlags;
            snapshot.StateHash = entry.StateHash;
            snapshot.HottestSourceRuntime = entry.HottestSourceRuntime;
            snapshot.HottestSoundTypeHash = entry.HottestSoundTypeHash;
            return entry.Frame != 0u || entry.ActivePredators != 0;
        }

        internal static bool TryReadAcousticSdfResult(int slot, out AcousticSensoryResultSnapshot snapshot)
        {
            snapshot = default;
            if (_evaluationScheduled ||
                (uint)slot >= (uint)Capacity ||
                !_acousticSdfResults.IsCreated)
                return false;

            NativeArray<AcousticEvaluationResultDTO> results = _acousticSdfResults.OpenRead();
            if (!results.IsCreated || slot >= results.Length)
                return false;

            AcousticEvaluationResultDTO result = results[slot];
            snapshot.Slot = slot;
            snapshot.ListenerEntityHash = result.ListenerEntityHash;
            snapshot.SoundTypeHash = result.SoundTypeHash;
            snapshot.ReceivedIntensity = result.ReceivedIntensity;
            snapshot.RawInverseSquareIntensity = result.RawInverseSquareIntensity;
            snapshot.OcclusionMultiplier = result.OcclusionMultiplier;
            snapshot.Direction = result.Direction;
            snapshot.RuntimeSourcePosition = result.RuntimeSourcePosition;
            snapshot.Flags = result.Flags;
            return (result.Flags & AcousticResultHeardFlag) != 0;
        }

        internal static bool TryReadAcousticSdfStimulus(int index, out AcousticStimulusDTO stimulus)
        {
            stimulus = default;
            if (_evaluationScheduled ||
                !_acousticSdfStimuli.IsCreated ||
                !_acousticSdfStimulusCounter.IsCreated)
                return false;

            NativeArray<AcousticStimulusDTO> stimuli = _acousticSdfStimuli.OpenRead();
            NativeArray<AcousticCounter64DTO> counter = _acousticSdfStimulusCounter.OpenRead();
            if (!stimuli.IsCreated ||
                !counter.IsCreated ||
                counter.Length <= 0 ||
                index < 0 ||
                index >= stimuli.Length)
            {
                return false;
            }

            AcousticCounter64DTO current = counter[0];
            if (index >= current.Value)
                return false;

            stimulus = stimuli[index];
            return true;
        }

        internal static int ReadAcousticSdfStimulusCount()
        {
            if (_evaluationScheduled || !_acousticSdfStimulusCounter.IsCreated)
                return 0;

            NativeArray<AcousticCounter64DTO> counter = _acousticSdfStimulusCounter.OpenRead();
            if (!counter.IsCreated || counter.Length <= 0)
                return 0;

            AcousticCounter64DTO current = counter[0];
            return math.max(0, current.Value);
        }

        internal static bool TryReadAcousticSdfTuning(out AcousticSensoryTuningSnapshot snapshot)
        {
            snapshot = default;
            if (!_acousticSdfTuning.IsCreated)
                return false;

            NativeArray<AcousticTuningDTO> tuningArray = _acousticSdfTuning.OpenRead();
            if (!tuningArray.IsCreated || tuningArray.Length <= 0)
                return false;

            AcousticTuningDTO tuning = tuningArray[0];
            tuning = SanitizeAcousticTuning(in tuning);
            snapshot.WaterAttenuationScalar = tuning.WaterAttenuationScalar;
            snapshot.RockOcclusionMultiplier = tuning.RockOcclusionMultiplier;
            snapshot.MinReceivedThreshold = tuning.MinReceivedThreshold;
            snapshot.MaxDistanceMeters = tuning.MaxDistanceMeters;
            snapshot.RayStepScale = tuning.RayStepScale;
            snapshot.FaultMicroseconds = tuning.FaultMicroseconds;
            snapshot.Flags = tuning.Flags;
            return true;
        }

        internal static unsafe bool TryWriteAcousticSdfTuning(in AcousticSensoryTuningSnapshot snapshot)
        {
            if (_evaluationScheduled)
                return false;

            if (!EnsureAcousticSdfVaultBuffers())
                return false;

            NativeArray<AcousticTuningDTO> tuningArray = _acousticSdfTuning.Open();
            if (!tuningArray.IsCreated || tuningArray.Length <= 0)
                return false;

            void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(tuningArray);
            ref AcousticTuningDTO tuning = ref UnsafeUtility.AsRef<AcousticTuningDTO>(ptr);
            tuning.WaterAttenuationScalar = snapshot.WaterAttenuationScalar;
            tuning.RockOcclusionMultiplier = snapshot.RockOcclusionMultiplier;
            tuning.MinReceivedThreshold = snapshot.MinReceivedThreshold;
            tuning.MaxDistanceMeters = snapshot.MaxDistanceMeters;
            tuning.RayStepScale = snapshot.RayStepScale;
            tuning.FaultMicroseconds = snapshot.FaultMicroseconds;
            tuning.Flags = snapshot.Flags;
            tuning.GlobalQualityWeight = ResolveAcousticGlobalQualityWeight();
            AcousticTuningDTO sanitized = tuning;
            sanitized = SanitizeAcousticTuning(in sanitized);
            tuning = sanitized;
            return true;
        }

        private static unsafe bool TryLoadAcousticHearingProfilesCsvCold()
        {
#if !UNITY_EDITOR
            return false;
#else
            NativeArray<byte> scratch = _acousticCsvScratch.Open();
            NativeArray<AcousticHearingProfileDTO> profiles = _acousticHearingProfiles.Open();
            NativeArray<int> profileCount = _acousticHearingProfileCount.Open();
            if (!scratch.IsCreated ||
                !profiles.IsCreated ||
                !profileCount.IsCreated ||
                profileCount.Length <= 0)
            {
                return false;
            }

            string path = ResolveAcousticProfilesPath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    int byteCount = (int)math.min(stream.Length, scratch.Length);
                    void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(scratch);
                    Span<byte> destination = new Span<byte>(ptr, byteCount);
                    int read = stream.Read(destination);
                    int parsed = ParseAcousticProfilesCsv(new ReadOnlySpan<byte>(ptr, read), profiles);
                    if (parsed > 0)
                    {
                        profileCount[0] = parsed;
                        return true;
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }

            return false;
#endif
        }

        private static string ResolveAcousticProfilesPath()
        {
            string assets = Application.dataPath;
            string candidate = Path.Combine(assets, "_SourceData", "AI", AcousticProfilesCsvName);
            if (File.Exists(candidate))
                return candidate;

            candidate = Path.Combine(assets, "_Project", "Data", "AI", AcousticProfilesCsvName);
            if (File.Exists(candidate))
                return candidate;

            candidate = Path.Combine(assets, "..", "Docs", "Data", AcousticProfilesCsvName);
            return File.Exists(candidate) ? candidate : string.Empty;
        }

#if UNITY_EDITOR
        private static int ParseAcousticProfilesCsv(ReadOnlySpan<byte> bytes, NativeArray<AcousticHearingProfileDTO> profiles)
        {
            int cursor = 0;
            int row = 0;
            int count = 0;
            while (cursor < bytes.Length && count < profiles.Length)
            {
                int lineStart = cursor;
                while (cursor < bytes.Length && bytes[cursor] != '\n' && bytes[cursor] != '\r')
                    cursor++;

                ReadOnlySpan<byte> line = bytes.Slice(lineStart, cursor - lineStart);
                while (cursor < bytes.Length && (bytes[cursor] == '\n' || bytes[cursor] == '\r'))
                    cursor++;

                if (line.Length == 0)
                    continue;

                row++;
                if (row == 1 && LooksLikeAcousticProfileHeader(line))
                    continue;

                if (TryParseAcousticProfileLine(line, out AcousticHearingProfileDTO profile))
                    profiles[count++] = profile;
            }

            return count;
        }

        private static bool LooksLikeAcousticProfileHeader(ReadOnlySpan<byte> line)
        {
            return IndexOfAscii(line, (byte)'s') >= 0 &&
                   IndexOfAscii(line, (byte)'t') >= 0 &&
                   IndexOfAscii(line, (byte)',') >= 0;
        }

        private static bool TryParseAcousticProfileLine(ReadOnlySpan<byte> line, out AcousticHearingProfileDTO profile)
        {
            profile = CreateDefaultAcousticProfile();
            int column = 0;
            int cursor = 0;
            bool parsedSpecies = false;
            while (column < 8)
            {
                ReadOnlySpan<byte> token = ReadCsvToken(line, ref cursor);
                token = TrimAscii(token);
                if (column == 0)
                {
                    profile.SpeciesHash = Fnv1aLower(token);
                    parsedSpecies = token.Length > 0;
                }
                else if (column == 1 && TryParseFloatAscii(token, out float threshold))
                    profile.HearingThreshold = SanitizeRange(threshold, profile.HearingThreshold, 0.0001f, 1f);
                else if (column == 2 && TryParseFloatAscii(token, out float fearGain))
                    profile.FearGain = SanitizeRange(fearGain, profile.FearGain, 0f, 4f);
                else if (column == 3 && TryParseFloatAscii(token, out float aggressionGain))
                    profile.AggressionGain = SanitizeRange(aggressionGain, profile.AggressionGain, 0f, 4f);
                else if (column == 4 && TryParseFloatAscii(token, out float maxDistance))
                    profile.MaxDistanceSq = math.max(1f, maxDistance * maxDistance);
                else if (column == 5 && TryParseFloatAscii(token, out float mechanicalFear))
                    profile.MechanicalFearBias = SanitizeRange(mechanicalFear, profile.MechanicalFearBias, 0f, 4f);
                else if (column == 6 && TryParseFloatAscii(token, out float preyAggression))
                    profile.PreyAggressionBias = SanitizeRange(preyAggression, profile.PreyAggressionBias, 0f, 4f);
                else if (column == 7 && TryParseUIntAscii(token, out uint flags))
                    profile.Flags = flags;

                column++;
                if (cursor >= line.Length)
                    break;
            }

            return parsedSpecies;
        }

        private static ReadOnlySpan<byte> ReadCsvToken(ReadOnlySpan<byte> line, ref int cursor)
        {
            int start = cursor;
            while (cursor < line.Length && line[cursor] != ',')
                cursor++;

            ReadOnlySpan<byte> token = line.Slice(start, cursor - start);
            if (cursor < line.Length && line[cursor] == ',')
                cursor++;
            return token;
        }

        private static ReadOnlySpan<byte> TrimAscii(ReadOnlySpan<byte> token)
        {
            int start = 0;
            int end = token.Length - 1;
            while (start <= end && token[start] <= 32)
                start++;
            while (end >= start && token[end] <= 32)
                end--;
            return start > end ? ReadOnlySpan<byte>.Empty : token.Slice(start, end - start + 1);
        }

        private static int IndexOfAscii(ReadOnlySpan<byte> bytes, byte needle)
        {
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes[i];
                if (b >= (byte)'A' && b <= (byte)'Z')
                    b = (byte)(b + 32);
                if (b == needle)
                    return i;
            }

            return -1;
        }

        private static bool TryParseFloatAscii(ReadOnlySpan<byte> token, out float value)
        {
            value = 0f;
            if (token.Length == 0)
                return false;

            int i = 0;
            float sign = 1f;
            if (token[i] == '-')
            {
                sign = -1f;
                i++;
            }
            else if (token[i] == '+')
            {
                i++;
            }

            float whole = 0f;
            bool any = false;
            while (i < token.Length && token[i] >= '0' && token[i] <= '9')
            {
                whole = (whole * 10f) + (token[i] - '0');
                i++;
                any = true;
            }

            float fraction = 0f;
            float divisor = 1f;
            if (i < token.Length && token[i] == '.')
            {
                i++;
                while (i < token.Length && token[i] >= '0' && token[i] <= '9')
                {
                    fraction = (fraction * 10f) + (token[i] - '0');
                    divisor *= 10f;
                    i++;
                    any = true;
                }
            }

            value = sign * (whole + (fraction / divisor));
            return any && math.isfinite(value);
        }

        private static bool TryParseUIntAscii(ReadOnlySpan<byte> token, out uint value)
        {
            value = 0u;
            if (token.Length == 0)
                return false;

            uint parsed = 0u;
            for (int i = 0; i < token.Length; i++)
            {
                byte b = token[i];
                if (b < '0' || b > '9')
                    return false;
                parsed = (parsed * 10u) + (uint)(b - '0');
            }

            value = parsed;
            return true;
        }
#endif

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct GenerateMockAcousticSignalsJob : IJobParallelFor
        {
            [ReadOnly] [NoAlias] public NativeArray<int> ActiveSlots;
            [ReadOnly] [NoAlias] public NativeArray<CognitionInput> Inputs;
            [NoAlias] public NativeArray<AcousticStimulusDTO> Stimuli;
            public int ActiveSlotCount;
            public int MockCapacity;
            public int FrameId;
            public float GlobalQualityWeight;

            public void Execute(int mockIndex)
            {
                if (!Stimuli.IsCreated ||
                    !ActiveSlots.IsCreated ||
                    !Inputs.IsCreated ||
                    ActiveSlotCount <= 0 ||
                    MockCapacity <= 0 ||
                    (uint)mockIndex >= (uint)MockCapacity ||
                    (uint)mockIndex >= (uint)Stimuli.Length)
                {
                    return;
                }

                int activeIndex = mockIndex % math.max(1, ActiveSlotCount);
                if ((uint)activeIndex >= (uint)ActiveSlots.Length)
                {
                    Stimuli[mockIndex] = default;
                    return;
                }

                int slot = ActiveSlots[activeIndex];
                if ((uint)slot >= (uint)Inputs.Length)
                {
                    Stimuli[mockIndex] = default;
                    return;
                }

                CognitionInput input = Inputs[slot];
                if ((input.Flags & (int)CognitionInputFlags.Active) == 0 ||
                    (input.Flags & (int)CognitionInputFlags.PredatorRole) == 0)
                {
                    Stimuli[mockIndex] = default;
                    return;
                }

                float3 axis = ResolveMockAxis(mockIndex + FrameId);
                float range = math.lerp(10f, 38f, math.saturate(GlobalQualityWeight));
                AcousticStimulusDTO stimulus = default;
                stimulus.EpicenterAUP = input.FloatingOriginOffset + new double3(input.Position + (axis * range));
                stimulus.InitialIntensity = math.lerp(0.08f, 0.22f, math.saturate(GlobalQualityWeight));
                stimulus.SoundTypeHash = AcousticSoundMockHash ^ ((uint)slot * 2654435761u);
                Stimuli[mockIndex] = stimulus;
            }

            private static float3 ResolveMockAxis(int seed)
            {
                int selector = seed & 7;
                if (selector == 0)
                    return new float3(1f, 0f, 0f);
                if (selector == 1)
                    return new float3(-1f, 0f, 0f);
                if (selector == 2)
                    return new float3(0f, 0f, 1f);
                if (selector == 3)
                    return new float3(0f, 0f, -1f);
                if (selector == 4)
                    return NormalizeOrDominant(new float3(1f, 0.25f, 1f));
                if (selector == 5)
                    return NormalizeOrDominant(new float3(-1f, -0.15f, 1f));
                if (selector == 6)
                    return NormalizeOrDominant(new float3(1f, -0.1f, -1f));

                return NormalizeOrDominant(new float3(-1f, 0.2f, -1f));
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct CalculateAcousticAttenuationJob : IJobParallelFor
        {
            [ReadOnly] [NoAlias] public NativeArray<int> ActiveSlots;
            [ReadOnly] [NoAlias] public NativeArray<CognitionInput> Inputs;
            [ReadOnly] [NoAlias] public NativeArray<byte> DueFlags;
            [ReadOnly] [NoAlias] public NativeArray<AcousticStimulusDTO> Stimuli;
            [ReadOnly] [NoAlias] public NativeArray<AcousticCounter64DTO> StimulusCounter;
            [ReadOnly] [NoAlias] public NativeArray<AcousticHearingProfileDTO> Profiles;
            [ReadOnly] [NoAlias] public NativeArray<int> ProfileCount;
            [NoAlias] public NativeArray<AcousticEvaluationResultDTO> Results;
            public int ActiveSlotCount;
            public AcousticTuningDTO Tuning;
            public int RaySteps;

            public void Execute(int activeIndex)
            {
                if (!ActiveSlots.IsCreated ||
                    !Inputs.IsCreated ||
                    !DueFlags.IsCreated ||
                    !Results.IsCreated ||
                    (uint)activeIndex >= (uint)ActiveSlotCount ||
                    (uint)activeIndex >= (uint)ActiveSlots.Length)
                    return;

                int slot = ActiveSlots[activeIndex];
                if ((uint)slot >= (uint)Inputs.Length ||
                    (uint)slot >= (uint)DueFlags.Length ||
                    (uint)slot >= (uint)Results.Length)
                {
                    return;
                }

                CognitionInput input = Inputs[slot];
                if ((input.Flags & (int)CognitionInputFlags.Active) == 0 ||
                    DueFlags[slot] == 0)
                {
                    Results[slot] = default;
                    return;
                }

                AcousticHearingProfileDTO profile = ResolveProfile(input.SpeciesId, Profiles, ProfileCount);
                double3 listenerAup = input.FloatingOriginOffset + new double3(input.Position);
                float tuningMaxDistanceSq = math.max(1f, Tuning.MaxDistanceMeters * Tuning.MaxDistanceMeters);
                float maxDistanceSq = math.min(profile.MaxDistanceSq, tuningMaxDistanceSq);
                int count = 0;
                if (StimulusCounter.IsCreated && StimulusCounter.Length > 0)
                {
                    AcousticCounter64DTO stimulusCounter = StimulusCounter[0];
                    count = math.min(math.max(0, stimulusCounter.Value), Stimuli.Length);
                }

                float best = 0f;
                float bestDistanceSq = 0f;
                int bestIndex = -1;
                float3 bestDelta = float3.zero;
                for (int i = 0; i < count; i++)
                {
                    AcousticStimulusDTO stimulus = Stimuli[i];
                    double3 deltaD = stimulus.EpicenterAUP - listenerAup;
                    if (!math.all(math.isfinite(deltaD)))
                        continue;

                    float3 delta = new float3((float)deltaD.x, (float)deltaD.y, (float)deltaD.z);
                    float distanceSq = math.max(math.lengthsq(delta), 0.01f);
                    if (distanceSq > maxDistanceSq)
                        continue;

                    float received = stimulus.InitialIntensity * math.rcp(math.max(distanceSq * Tuning.WaterAttenuationScalar, 0.01f));
                    if (received > best)
                    {
                        best = received;
                        bestDistanceSq = distanceSq;
                        bestIndex = i;
                        bestDelta = delta;
                    }
                }

                AcousticEvaluationResultDTO result = default;
                result.ListenerSlot = (ushort)slot;
                result.ListenerEntityHash = HashListener(input.SpeciesId, slot);
                result.RaySteps = (byte)RaySteps;
                if (bestIndex >= 0)
                {
                    AcousticStimulusDTO stimulus = Stimuli[bestIndex];
                    result.SourceAUP = stimulus.EpicenterAUP;
                    result.RuntimeSourcePosition = input.Position + bestDelta;
                    result.Direction = NormalizeOrDominant(bestDelta);
                    result.ReceivedIntensity = best;
                    result.RawInverseSquareIntensity = best;
                    result.OcclusionMultiplier = 1f;
                    result.SoundTypeHash = stimulus.SoundTypeHash;
                    result.SourceIndex = (ushort)bestIndex;
                    result.Flags = best >= profile.HearingThreshold ? AcousticResultHeardFlag : (byte)0;
                    result.Reserved1 = (uint)math.asint(bestDistanceSq);
                }

                Results[slot] = result;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct EvaluateAcousticOcclusionJob : IJobParallelFor
        {
            [ReadOnly] [NoAlias] public NativeArray<int> ActiveSlots;
            [ReadOnly] [NoAlias] public NativeArray<CognitionInput> Inputs;
            [ReadOnly] [NoAlias] public NativeArray<byte> DueFlags;
            [ReadOnly] [NoAlias] public NativeArray<AcousticStimulusDTO> Stimuli;
            [ReadOnly] [NoAlias] public NativeArray<AcousticCounter64DTO> StimulusCounter;
            [ReadOnly] [NoAlias] public NativeArray<AcousticHearingProfileDTO> Profiles;
            [ReadOnly] [NoAlias] public NativeArray<int> ProfileCount;
            [NoAlias] public NativeArray<AcousticEvaluationResultDTO> Results;
            [NoAlias] public NativeArray<AcousticMemoryEntry> AcousticMemoryBank;
            [NoAlias] public NativeArray<float4> AcousticMemoryFloat4Bank;
            // SAFETY_JUSTIFICATION_PARAGRAPH_1: CorePtr and ControlPtr are raw bases for `_cores`
            // and `_controls`, both owned exclusively by PredatorCognitionDomain. ScheduleFrameEvaluation
            // wires this job after attenuation and before PredatorCognitionJob, so no sibling domain or
            // managed facade writes those rows during this mutation window.
            // SAFETY_JUSTIFICATION_PARAGRAPH_2: Rejected alternatives were native-array aliases for
            // `_cores`/`_controls`, a setter command buffer consumed by a later managed pass, and duplicate
            // acoustic patch arrays. The aliases widened Burst's alias surface, the setter buffer added
            // another gameplay truth route, and duplicate arrays required a second merge pass before
            // cognition. The raw owner-row pointer keeps one mutation route and one dependency edge.
            // SAFETY_JUSTIFICATION_PARAGRAPH_3: Pointers are never cached beyond the job struct, never
            // published through SignalBus or diagnostics, and never copied into DTOs. Dependency chaining
            // returns the occlusion handle to the dispatcher-owned graph before cognition reads the mutated
            // rows, preserving one owner, one route, one proof artifact.
            [NativeDisableUnsafePtrRestriction] [NoAlias] public void* CorePtr;
            [NativeDisableUnsafePtrRestriction] [NoAlias] public void* ControlPtr;
            public int CoreLength;
            public int ControlLength;
            public int ActiveSlotCount;
            public AcousticTuningDTO Tuning;
            public int RaySteps;
            [ReadOnly] [NoAlias] public NativeArray<byte>.ReadOnly ThreatVoxelGrid;
            public int3 ThreatVoxelDimensions;
            public float3 ThreatVoxelOrigin;
            public float3 ThreatVoxelCellSize;
            public byte ThreatVoxelSolidThreshold;
            public int ThreatVoxelUsesSignedDistanceEncoding;

            public void Execute(int activeIndex)
            {
                if (!ActiveSlots.IsCreated ||
                    !Inputs.IsCreated ||
                    !DueFlags.IsCreated ||
                    !Results.IsCreated ||
                    (uint)activeIndex >= (uint)ActiveSlotCount ||
                    (uint)activeIndex >= (uint)ActiveSlots.Length)
                    return;

                int slot = ActiveSlots[activeIndex];
                if ((uint)slot >= (uint)Inputs.Length ||
                    (uint)slot >= (uint)DueFlags.Length ||
                    (uint)slot >= (uint)Results.Length)
                {
                    return;
                }

                CognitionInput input = Inputs[slot];
                if ((input.Flags & (int)CognitionInputFlags.Active) == 0 ||
                    DueFlags[slot] == 0)
                {
                    Results[slot] = default;
                    return;
                }

                AcousticHearingProfileDTO profile = ResolveProfile(input.SpeciesId, Profiles, ProfileCount);
                double3 listenerAup = input.FloatingOriginOffset + new double3(input.Position);
                float tuningMaxDistanceSq = math.max(1f, Tuning.MaxDistanceMeters * Tuning.MaxDistanceMeters);
                float maxDistanceSq = math.min(profile.MaxDistanceSq, tuningMaxDistanceSq);
                int count = 0;
                if (StimulusCounter.IsCreated && StimulusCounter.Length > 0)
                {
                    AcousticCounter64DTO stimulusCounter = StimulusCounter[0];
                    count = math.min(math.max(0, stimulusCounter.Value), Stimuli.Length);
                }

                float bestFinal = 0f;
                float bestRaw = 0f;
                float bestOcclusion = 1f;
                int bestIndex = -1;
                float3 bestDelta = float3.zero;
                byte flags = 0;
                for (int i = 0; i < count; i++)
                {
                    AcousticStimulusDTO stimulus = Stimuli[i];
                    double3 deltaD = stimulus.EpicenterAUP - listenerAup;
                    if (!math.all(math.isfinite(deltaD)))
                        continue;

                    float3 delta = new float3((float)deltaD.x, (float)deltaD.y, (float)deltaD.z);
                    float distanceSq = math.max(math.lengthsq(delta), 0.01f);
                    if (distanceSq > maxDistanceSq)
                        continue;

                    float raw = stimulus.InitialIntensity * math.rcp(math.max(distanceSq * Tuning.WaterAttenuationScalar, 0.01f));
                    float hearingThreshold = math.max(profile.HearingThreshold, Tuning.MinReceivedThreshold);
                    if (raw < hearingThreshold)
                        continue;

                    float occlusion = EvaluateSdfOcclusion(input.Position, input.Position + delta, RaySteps, Tuning.RockOcclusionMultiplier);
                    float final = raw * occlusion;
                    if (final > bestFinal)
                    {
                        bestFinal = final;
                        bestRaw = raw;
                        bestOcclusion = occlusion;
                        bestIndex = i;
                        bestDelta = delta;
                        flags = (byte)math.select(0, AcousticResultOccludedFlag, occlusion < 0.999f);
                    }
                }

                AcousticEvaluationResultDTO result = default;
                result.ListenerSlot = (ushort)slot;
                result.ListenerEntityHash = HashListener(input.SpeciesId, slot);
                result.RaySteps = (byte)RaySteps;
                if (bestIndex >= 0)
                {
                    AcousticStimulusDTO stimulus = Stimuli[bestIndex];
                    result.SourceAUP = stimulus.EpicenterAUP;
                    result.RuntimeSourcePosition = input.Position + bestDelta;
                    result.Direction = NormalizeOrDominant(bestDelta);
                    result.ReceivedIntensity = bestFinal;
                    result.RawInverseSquareIntensity = bestRaw;
                    result.OcclusionMultiplier = bestOcclusion;
                    result.SoundTypeHash = stimulus.SoundTypeHash;
                    result.SourceIndex = (ushort)bestIndex;
                    if (bestFinal >= math.max(profile.HearingThreshold, Tuning.MinReceivedThreshold))
                    {
                        flags |= AcousticResultHeardFlag;
                        if (IsMechanicalFearSound(stimulus.SoundTypeHash))
                            flags |= AcousticResultMechanicalFearFlag;
                        if (IsPreyAggressionSound(stimulus.SoundTypeHash))
                            flags |= AcousticResultPreyAggressionFlag;

                        InjectCognition(slot, input, profile, in result, flags);
                    }

                    result.Flags = flags;
                }

                Results[slot] = result;
            }

            private void InjectCognition(
                int slot,
                in CognitionInput input,
                in AcousticHearingProfileDTO profile,
                in AcousticEvaluationResultDTO result,
                byte flags)
            {
                if (CorePtr == null || ControlPtr == null)
                    return;
                if ((uint)slot >= (uint)CoreLength || (uint)slot >= (uint)ControlLength)
                    return;

                byte* coreBytes = (byte*)CorePtr;
                byte* controlBytes = (byte*)ControlPtr;
                ref CognitionCore core = ref UnsafeUtility.AsRef<CognitionCore>(coreBytes + (slot * CognitionCoreSizeBytes));
                ref CognitionControl control = ref UnsafeUtility.AsRef<CognitionControl>(controlBytes + (slot * CognitionControlSizeBytes));
                UnpackDriveChannels(core.QuantizedDrives, out float hunger, out float aggression, out float fear, out float threatLevel);
                float scaled = math.saturate(result.ReceivedIntensity * 12f);
                if ((flags & AcousticResultMechanicalFearFlag) != 0)
                    fear = math.saturate(fear + (scaled * profile.FearGain * profile.MechanicalFearBias));
                else
                    fear = math.saturate(fear + (scaled * profile.FearGain * 0.35f));

                if ((flags & AcousticResultPreyAggressionFlag) != 0)
                    aggression = math.saturate(aggression + (scaled * profile.AggressionGain * profile.PreyAggressionBias));
                else
                    aggression = math.saturate(aggression + (scaled * profile.AggressionGain * 0.25f));

                threatLevel = math.saturate(math.max(threatLevel, scaled));
                core.QuantizedDrives = PackDriveChannels(hunger, aggression, fear, threatLevel);
                int acousticSlotIndex = core.AcousticMemoryHead;
                if ((uint)acousticSlotIndex >= (uint)AcousticMemorySlotsPerCreature)
                    acousticSlotIndex = 0;
                int acousticMemoryIndex = (slot * AcousticMemorySlotsPerCreature) + acousticSlotIndex;
                if ((uint)acousticMemoryIndex < (uint)AcousticMemoryBank.Length)
                {
                    AcousticMemoryEntry entry = default;
                    entry.WorldPosition = result.RuntimeSourcePosition;
                    entry.Timestamp = input.CurrentTime;
                    entry.Intensity = result.ReceivedIntensity;
                    entry.BucketCoord = ResolveAcousticBucketCoordinates(result.RuntimeSourcePosition, AcousticBucketCellSize);
                    entry.BucketHash = HashAcousticBucket(entry.BucketCoord);
                    AcousticMemoryBank[acousticMemoryIndex] = entry;
                    if (AcousticMemoryFloat4Bank.IsCreated && acousticMemoryIndex < AcousticMemoryFloat4Bank.Length)
                        AcousticMemoryFloat4Bank[acousticMemoryIndex] = new float4(result.RuntimeSourcePosition, input.CurrentTime);

                    int nextAcousticHead = acousticSlotIndex + 1;
                    core.AcousticMemoryHead = math.select(nextAcousticHead, 0, nextAcousticHead >= AcousticMemorySlotsPerCreature);
                }

                control.OverrideThreatPosition = result.RuntimeSourcePosition;
                control.OverrideUntilTime = math.max(control.OverrideUntilTime, input.CurrentTime + math.lerp(0.35f, 1.25f, scaled));
                control.Flags |= (int)CognitionControlFlags.HasOverrideThreatPosition;
            }

            private float EvaluateSdfOcclusion(float3 start, float3 end, int steps, float rockOcclusionMultiplier)
            {
                if (!ThreatVoxelGrid.IsCreated ||
                    ThreatVoxelDimensions.x <= 0 ||
                    ThreatVoxelDimensions.y <= 0 ||
                    ThreatVoxelDimensions.z <= 0)
                {
                    return 1f;
                }

                float occlusion = 1f;
                int safeSteps = math.clamp(steps, 1, 8);
                for (int i = 0; i < safeSteps; i++)
                {
                    float t = (i + 1f) / (safeSteps + 1f);
                    float3 samplePosition = math.lerp(start, end, t);
                    float sdf = SampleThreatVoxelSigned01(samplePosition);
                    if (sdf < 0f)
                        occlusion *= math.clamp(rockOcclusionMultiplier, 0.01f, 1f);
                    else
                        occlusion *= math.lerp(0.92f, 1f, math.saturate(sdf));
                }

                return math.saturate(occlusion);
            }

            private float SampleThreatVoxelSigned01(float3 worldPosition)
            {
                if (!math.all(math.isfinite(worldPosition)))
                    return 1f;

                int3 voxel = ResolveThreatVoxel(worldPosition);
                if (!IsVoxelInBounds(voxel))
                    return 1f;

                int flatIndex = FlattenThreatVoxelIndex(voxel, ThreatVoxelDimensions);
                if ((uint)flatIndex >= (uint)ThreatVoxelGrid.Length)
                    return 1f;

                byte sample = ThreatVoxelGrid[flatIndex];
                if (ThreatVoxelUsesSignedDistanceEncoding != 0)
                    return ((sample * QuantizedByteInvScale) - 0.5f) * 2f;

                return sample >= ThreatVoxelSolidThreshold ? -1f : 1f;
            }

            private static int FlattenThreatVoxelIndex(int3 voxel, int3 dimensions)
            {
                if (dimensions.x <= 0 || dimensions.y <= 0 || dimensions.z <= 0)
                    return -1;

                if (voxel.x < 0 ||
                    voxel.y < 0 ||
                    voxel.z < 0 ||
                    voxel.x >= dimensions.x ||
                    voxel.y >= dimensions.y ||
                    voxel.z >= dimensions.z)
                {
                    return -1;
                }

                long xyStride = (long)dimensions.x * dimensions.y;
                long index = voxel.x + ((long)voxel.y * dimensions.x) + ((long)voxel.z * xyStride);
                return xyStride > 0L && xyStride <= int.MaxValue && index >= 0L && index <= int.MaxValue ? (int)index : -1;
            }

            private int3 ResolveThreatVoxel(float3 worldPosition)
            {
                float3 invCellSize = math.rcp(math.max(ThreatVoxelCellSize, new float3(MathSafetyEpsilon)));
                float3 local = (worldPosition - ThreatVoxelOrigin) * invCellSize;
                return new int3((int)math.floor(local.x), (int)math.floor(local.y), (int)math.floor(local.z));
            }

            private bool IsVoxelInBounds(int3 voxel)
            {
                return voxel.x >= 0 &&
                       voxel.y >= 0 &&
                       voxel.z >= 0 &&
                       voxel.x < ThreatVoxelDimensions.x &&
                       voxel.y < ThreatVoxelDimensions.y &&
                       voxel.z < ThreatVoxelDimensions.z;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct RecordAcousticTelemetryJob : IJob
        {
            [ReadOnly] [NoAlias] public NativeArray<int> ActiveSlots;
            [ReadOnly] [NoAlias] public NativeArray<AcousticEvaluationResultDTO> Results;
            [ReadOnly] [NoAlias] public NativeArray<AcousticCounter64DTO> StimulusCounter;
            [NoAlias] public NativeArray<SensoryTelemetryEntry> TelemetryRing;
            [NoAlias] public NativeArray<int> TelemetryCursor;
            public int ActiveSlotCount;
            public int FrameId;
            public float GlobalQualityWeight;
            public int RaySteps;
            public float FaultMicroseconds;

            public void Execute()
            {
                if (!TelemetryRing.IsCreated ||
                    !TelemetryCursor.IsCreated ||
                    !ActiveSlots.IsCreated ||
                    !Results.IsCreated ||
                    TelemetryRing.Length <= 0 ||
                    TelemetryCursor.Length <= 0)
                {
                    return;
                }

                SensoryTelemetryEntry entry = default;
                entry.Frame = (uint)math.max(0, FrameId);
                int safeActiveCount = math.min(math.max(0, ActiveSlotCount), ActiveSlots.Length);
                entry.ActivePredators = (ushort)math.clamp(safeActiveCount, 0, ushort.MaxValue);
                int stimuli = 0;
                if (StimulusCounter.IsCreated && StimulusCounter.Length > 0)
                {
                    AcousticCounter64DTO stimulusCounter = StimulusCounter[0];
                    stimuli = math.max(0, stimulusCounter.Value);
                    entry.Reserved0 = stimulusCounter.Reserved0;
                    entry.Reserved1 = stimulusCounter.Flags;
                }

                entry.StimulusCount = (ushort)math.clamp(stimuli, 0, ushort.MaxValue);
                entry.GlobalQualityWeight = math.saturate(GlobalQualityWeight);
                entry.RaySteps = RaySteps;
                uint hash = 2166136261u;
                for (int i = 0; i < safeActiveCount; i++)
                {
                    int slot = ActiveSlots[i];
                    if ((uint)slot >= (uint)Results.Length)
                        continue;

                    AcousticEvaluationResultDTO result = Results[slot];
                    if ((result.Flags & AcousticResultHeardFlag) == 0)
                        continue;

                    entry.HeardPredators++;
                    if ((result.Flags & AcousticResultOccludedFlag) != 0)
                        entry.OccludedEvaluations++;
                    if (result.ReceivedIntensity > entry.MaxReceivedIntensity)
                    {
                        entry.MaxReceivedIntensity = result.ReceivedIntensity;
                        entry.MaxRawIntensity = result.RawInverseSquareIntensity;
                        entry.HottestSourceRuntime = result.RuntimeSourcePosition;
                        entry.HottestSoundTypeHash = result.SoundTypeHash;
                    }

                    hash = (hash ^ result.SoundTypeHash) * 16777619u;
                    hash = (hash ^ result.ListenerEntityHash) * 16777619u;
                }

                entry.EstimatedMicroseconds = EstimateMicroseconds(ActiveSlotCount, stimuli, RaySteps);
                entry.FaultFlags = entry.EstimatedMicroseconds > FaultMicroseconds ? AcousticFaultBudgetExceeded : 0u;
                if (entry.Reserved0 > 0u)
                    entry.FaultFlags |= AcousticFaultStimulusOverflow;
                if ((entry.Reserved1 & AcousticCounterFlagInvalidIngress) != 0u)
                    entry.FaultFlags |= AcousticFaultNonFinite;
                if (!math.isfinite(entry.MaxReceivedIntensity) || !math.all(math.isfinite(entry.HottestSourceRuntime)))
                    entry.FaultFlags |= AcousticFaultNonFinite;
                entry.StateHash = hash;
                int cursor = TelemetryCursor[0];
                if ((uint)cursor >= (uint)TelemetryRing.Length)
                    cursor = 0;
                TelemetryRing[cursor] = entry;
                cursor++;
                if (cursor >= TelemetryRing.Length)
                    cursor = 0;
                TelemetryCursor[0] = cursor;
            }

            private static float EstimateMicroseconds(int activeCount, int stimulusCount, int raySteps)
            {
                return math.max(1f, activeCount * math.max(1, stimulusCount) * math.max(1, raySteps) * 0.055f);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static AcousticHearingProfileDTO ResolveProfile(
            int speciesId,
            NativeArray<AcousticHearingProfileDTO> profiles,
            NativeArray<int> profileCount)
        {
            AcousticHearingProfileDTO fallback = CreateDefaultAcousticProfile();
            uint speciesHash = (uint)speciesId;
            int count = profileCount.IsCreated && profileCount.Length > 0
                ? math.min(math.max(0, profileCount[0]), profiles.Length)
                : 0;
            for (int i = 0; i < count; i++)
            {
                AcousticHearingProfileDTO profile = profiles[i];
                if (profile.SpeciesHash == 0u)
                    fallback = profile;
                if (profile.SpeciesHash == speciesHash)
                    return SanitizeProfile(in profile);
            }

            return SanitizeProfile(in fallback);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static AcousticHearingProfileDTO SanitizeProfile(in AcousticHearingProfileDTO source)
        {
            AcousticHearingProfileDTO profile = source;
            profile.HearingThreshold = math.clamp(math.select(AcousticStimulusThreshold, profile.HearingThreshold, math.isfinite(profile.HearingThreshold)), 0.0001f, 1f);
            profile.FearGain = math.clamp(math.select(0.55f, profile.FearGain, math.isfinite(profile.FearGain)), 0f, 4f);
            profile.AggressionGain = math.clamp(math.select(0.45f, profile.AggressionGain, math.isfinite(profile.AggressionGain)), 0f, 4f);
            profile.MaxDistanceSq = math.max(1f, math.select(2500f, profile.MaxDistanceSq, math.isfinite(profile.MaxDistanceSq)));
            profile.MechanicalFearBias = math.clamp(math.select(1.35f, profile.MechanicalFearBias, math.isfinite(profile.MechanicalFearBias)), 0f, 4f);
            profile.PreyAggressionBias = math.clamp(math.select(1.2f, profile.PreyAggressionBias, math.isfinite(profile.PreyAggressionBias)), 0f, 4f);
            return profile;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint HashListener(int speciesId, int slot)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)speciesId) * 16777619u;
                hash = (hash ^ (uint)slot) * 16777619u;
                return hash == 0u ? 1u : hash;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 NormalizeOrDominant(float3 delta)
        {
            float lengthSq = math.lengthsq(delta);
            if (lengthSq <= MathSafetyEpsilon)
                return new float3(0f, 0f, 1f);

            return delta * math.rsqrt(lengthSq);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsMechanicalFearSound(uint soundTypeHash)
        {
            return (soundTypeHash ^ AcousticSoundMechanicalHash) < 0x01000000u ||
                   (soundTypeHash ^ AcousticSoundSonarHash) < 0x01000000u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsPreyAggressionSound(uint soundTypeHash)
        {
            return (soundTypeHash ^ AcousticSoundPreyHash) < 0x01000000u ||
                   (soundTypeHash ^ AcousticSoundDamageHash) < 0x01000000u;
        }
    }
}
