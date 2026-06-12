using System;
using System.IO;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.AI.Cognition
{
    public static class UtilityAIAnxietyVaultBufferIds
    {
        public const BufferID Profiles = BufferID.UtilityAICognitionVault_AnxietyDecay_Profiles;
        public const BufferID Tuning = BufferID.UtilityAICognitionVault_AnxietyDecay_Tuning;
        public const BufferID Scratch = BufferID.UtilityAICognitionVault_AnxietyDecay_Scratch;
        public const BufferID TelemetryRing = BufferID.UtilityAICognitionVault_AnxietyDecay_TelemetryRing;
        public const BufferID TelemetryCursor = BufferID.UtilityAICognitionVault_AnxietyDecay_TelemetryCursor;
        public const BufferID ShelterSdf = BufferID.UtilityAICognitionVault_AnxietyDecay_ShelterSdf;
        public const BufferID ShelterHeader = BufferID.UtilityAICognitionVault_AnxietyDecay_ShelterHeader;
#if UNITY_EDITOR
        public const BufferID CsvScratch = BufferID.UtilityAICognitionVault_AnxietyDecay_CsvScratch;
#endif
    }

    public struct UtilityAIAnxietyVaultHandles
    {
        public VaultGenerationHandle<AnxietyProfileDTO> Profiles;
        public VaultGenerationHandle<AnxietyRuntimeTuningDTO> Tuning;
        public VaultGenerationHandle<AnxietyDecayScratchDTO> Scratch;
        public VaultGenerationHandle<AnxietyTelemetryEntry> TelemetryRing;
        public VaultGenerationHandle<int> TelemetryCursor;
        public VaultGenerationHandle<float> ShelterSdf;
        public VaultGenerationHandle<AnxietyShelterSdfHeaderDTO> ShelterHeader;
#if UNITY_EDITOR
        public VaultGenerationHandle<byte> CsvScratch;
#endif

        public bool IsCreated()
        {
            return IsHandleCreated(in Profiles) &&
                   IsHandleCreated(in Tuning) &&
                   IsHandleCreated(in Scratch) &&
                   IsHandleCreated(in TelemetryRing) &&
                   IsHandleCreated(in TelemetryCursor) &&
                   IsHandleCreated(in ShelterSdf) &&
                   IsHandleCreated(in ShelterHeader)
#if UNITY_EDITOR
                   && IsHandleCreated(in CsvScratch)
#endif
                   ;
        }

        private static bool IsHandleCreated<T>(in VaultGenerationHandle<T> handle)
            where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }
    }

    public ref struct UtilityAIAnxietyVaultBuffers
    {
        public NativeArray<AnxietyProfileDTO> Profiles;
        public NativeArray<AnxietyRuntimeTuningDTO> Tuning;
        public NativeArray<AnxietyDecayScratchDTO> Scratch;
        public NativeArray<AnxietyTelemetryEntry> TelemetryRing;
        public NativeArray<int> TelemetryCursor;
        public NativeArray<float> ShelterSdf;
        public NativeArray<AnxietyShelterSdfHeaderDTO> ShelterHeader;
#if UNITY_EDITOR
        public NativeArray<byte> CsvScratch;
#endif

        public bool IsCreated()
        {
            return Profiles.IsCreated &&
                   Tuning.IsCreated &&
                   Scratch.IsCreated &&
                   TelemetryRing.IsCreated &&
                   TelemetryCursor.IsCreated &&
                   ShelterSdf.IsCreated &&
                   ShelterHeader.IsCreated
#if UNITY_EDITOR
                   && CsvScratch.IsCreated
#endif
                   ;
        }
    }

    public static partial class UtilityAICognitionVault
    {
        private const uint AnxietyDumpEndianMarker = 0x01020304u;
        private const uint AnxietyDumpVersion = 1u;
        private const string AnxietyDumpFileName = "Dump_SHINOBU_312.bin";
        private const string AnxietyAgent1300DumpFileName = "Dump_1300_AICognition.bin";
        private const string AnxietyCsvFileName = "fauna_psychology_profiles.csv";
        private static readonly ulong AnxietyTuningProfileMutationGuardMask =
            AnxietyVaultMutationGuardBit(UtilityAIAnxietyVaultBufferIds.Tuning) |
            AnxietyVaultMutationGuardBit(UtilityAIAnxietyVaultBufferIds.Profiles);

        public static bool TryAcquireAnxietyHandles(IDataVault vault, out UtilityAIAnxietyVaultHandles handles)
        {
            handles = default;
            if (vault == null)
                return false;

            if (vault.IsAllocationLocked)
            {
                if (!TryReadExistingAnxietyHandles(vault, out handles))
                    return false;

                UtilityAIAnxietyVaultBuffers lockedBuffers;
                return TryResolveAnxietyViews(vault, ref handles, out lockedBuffers);
            }

            handles.Profiles = vault.EnsureGenerationHandle<AnxietyProfileDTO>(
                UtilityAIAnxietyVaultBufferIds.Profiles,
                AnxietyDecayConstants.MaxProfiles,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            handles.Tuning = vault.EnsureGenerationHandle<AnxietyRuntimeTuningDTO>(
                UtilityAIAnxietyVaultBufferIds.Tuning,
                1,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            handles.Scratch = vault.EnsureGenerationHandle<AnxietyDecayScratchDTO>(
                UtilityAIAnxietyVaultBufferIds.Scratch,
                UtilityAICognitionConstants.MaxCreatures,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            handles.TelemetryRing = vault.EnsureGenerationHandle<AnxietyTelemetryEntry>(
                UtilityAIAnxietyVaultBufferIds.TelemetryRing,
                AnxietyDecayConstants.TelemetryFrames,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            handles.TelemetryCursor = vault.EnsureGenerationHandle<int>(
                UtilityAIAnxietyVaultBufferIds.TelemetryCursor,
                1,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            handles.ShelterSdf = vault.EnsureGenerationHandle<float>(
                UtilityAIAnxietyVaultBufferIds.ShelterSdf,
                AnxietyDecayConstants.ShelterSdfVoxels,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            handles.ShelterHeader = vault.EnsureGenerationHandle<AnxietyShelterSdfHeaderDTO>(
                UtilityAIAnxietyVaultBufferIds.ShelterHeader,
                1,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
#if UNITY_EDITOR
            handles.CsvScratch = vault.EnsureGenerationHandle<byte>(
                UtilityAIAnxietyVaultBufferIds.CsvScratch,
                AnxietyDecayConstants.CsvScratchBytes,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
#endif

            if (!TryResolveAnxietyViews(vault, ref handles, out UtilityAIAnxietyVaultBuffers buffers))
                return false;

            EnsureAnxietyColdDefaults(buffers, true);
            return true;
        }

        public static bool TryResolveAnxietyViews(IDataVault vault, ref UtilityAIAnxietyVaultHandles handles, out UtilityAIAnxietyVaultBuffers buffers)
        {
            buffers = default;
            if (vault == null || !handles.IsCreated())
                return false;

            if (!TryOpenAnxietyVaultView(vault, in handles.Profiles, AnxietyDecayConstants.MaxProfiles, out buffers.Profiles) ||
                !TryOpenAnxietyVaultView(vault, in handles.Tuning, 1, out buffers.Tuning) ||
                !TryOpenAnxietyVaultView(vault, in handles.Scratch, UtilityAICognitionConstants.MaxCreatures, out buffers.Scratch) ||
                !TryOpenAnxietyVaultView(vault, in handles.TelemetryRing, AnxietyDecayConstants.TelemetryFrames, out buffers.TelemetryRing) ||
                !TryOpenAnxietyVaultView(vault, in handles.TelemetryCursor, 1, out buffers.TelemetryCursor) ||
                !TryOpenAnxietyVaultView(vault, in handles.ShelterSdf, AnxietyDecayConstants.ShelterSdfVoxels, out buffers.ShelterSdf) ||
                !TryOpenAnxietyVaultView(vault, in handles.ShelterHeader, 1, out buffers.ShelterHeader))
            {
                buffers = default;
                return false;
            }

#if UNITY_EDITOR
            if (!TryOpenAnxietyVaultView(vault, in handles.CsvScratch, AnxietyDecayConstants.CsvScratchBytes, out buffers.CsvScratch))
            {
                buffers = default;
                return false;
            }
#endif

            return buffers.IsCreated();
        }

        public static bool TryScheduleMockAnxietyEnvironment(
            in UtilityAICognitionVaultBuffers cognitionBuffers,
            in UtilityAIAnxietyVaultBuffers anxietyBuffers,
            uint frame,
            int spikeCount,
            JobHandle inputDependency,
            out JobHandle outputDependency)
        {
            outputDependency = inputDependency;
            if (!cognitionBuffers.IsCreated() || !anxietyBuffers.IsCreated())
                return false;

            int stateLength = math.min(cognitionBuffers.States.Length, cognitionBuffers.Aups.Length);
            if (stateLength <= 0)
                return false;

            GenerateMockAnxietySpikesJob spikesJob = new GenerateMockAnxietySpikesJob
            {
                States = cognitionBuffers.States,
                Aups = cognitionBuffers.Aups,
                Tuning = anxietyBuffers.Tuning,
                Frame = frame,
                SpikeCount = spikeCount
            };
            JobHandle spikeHandle = spikesJob.Schedule(stateLength, 64, inputDependency);

            GenerateMockShelterSdfJob shelterJob = new GenerateMockShelterSdfJob
            {
                ShelterSdf = anxietyBuffers.ShelterSdf,
                Header = anxietyBuffers.ShelterHeader
            };
            JobHandle shelterHandle = shelterJob.Schedule(anxietyBuffers.ShelterSdf.Length, 128, inputDependency);

            outputDependency = JobHandle.CombineDependencies(spikeHandle, shelterHandle);
            return true;
        }

        public static bool TryScheduleAnxietyFrostTick(
            in UtilityAICognitionVaultBuffers cognitionBuffers,
            in UtilityAIAnxietyVaultBuffers anxietyBuffers,
            uint frame,
            float deltaSeconds,
            float estimatedBurstMicroseconds,
            JobHandle inputDependency,
            out JobHandle outputDependency)
        {
            outputDependency = inputDependency;
            if (!cognitionBuffers.IsCreated() || !anxietyBuffers.IsCreated())
                return false;

            int scheduleLength = math.min(cognitionBuffers.States.Length, cognitionBuffers.Aups.Length);
            scheduleLength = math.min(scheduleLength, anxietyBuffers.Scratch.Length);
            if (scheduleLength <= 0)
                return false;

            CalculateAnxietyDecayJob decayJob = new CalculateAnxietyDecayJob
            {
                States = cognitionBuffers.States,
                Aups = cognitionBuffers.Aups,
                Profiles = anxietyBuffers.Profiles,
                Tuning = anxietyBuffers.Tuning,
                ShelterSdf = anxietyBuffers.ShelterSdf,
                ShelterHeader = anxietyBuffers.ShelterHeader,
                Scratch = anxietyBuffers.Scratch,
                Frame = frame,
                DeltaSeconds = deltaSeconds
            };
            JobHandle decayHandle = decayJob.Schedule(scheduleLength, 64, inputDependency);

            RecordAnxietyTelemetryJob telemetryJob = new RecordAnxietyTelemetryJob
            {
                Scratch = anxietyBuffers.Scratch,
                Tuning = anxietyBuffers.Tuning,
                TelemetryRing = anxietyBuffers.TelemetryRing,
                TelemetryCursor = anxietyBuffers.TelemetryCursor,
                Frame = frame,
                StateCount = scheduleLength,
                BurstMicroseconds = estimatedBurstMicroseconds
            };
            outputDependency = telemetryJob.Schedule(decayHandle);
            return true;
        }

        public static bool TryGetAnxietyTuning(IDataVault vault, ref UtilityAIAnxietyVaultHandles handles, out AnxietyRuntimeTuningDTO tuning)
        {
            tuning = default;
            if (vault == null ||
                handles.Tuning.BufferID == 0u ||
                handles.Tuning.Generation == 0u ||
                !vault.TryReadOnlyHandle(in handles.Tuning, out NativeArray<AnxietyRuntimeTuningDTO>.ReadOnly tuningBuffer) ||
                tuningBuffer.Length <= 0)
            {
                return false;
            }

            AnxietyRuntimeTuningDTO raw = tuningBuffer[0];
            tuning = AnxietyDecayJobMath.SanitizeTuning(in raw);
            return true;
        }

        public static bool TrySetAnxietyTuning(IDataVault vault, ref UtilityAIAnxietyVaultHandles handles, in AnxietyRuntimeTuningDTO tuning)
        {
            if (vault == null ||
                handles.Tuning.BufferID != (uint)UtilityAIAnxietyVaultBufferIds.Tuning ||
                handles.Tuning.Generation == 0u ||
                !vault.TryAcquireMutationGuard(AnxietyTuningProfileMutationGuardMask))
            {
                return false;
            }

            try
            {
                if (!TryOpenAnxietyVaultView(vault, in handles.Tuning, 1, out NativeArray<AnxietyRuntimeTuningDTO> tuningBuffer))
                    return false;

                bool profilesResolved = false;
                NativeArray<AnxietyProfileDTO> profiles = default;
                if (handles.Profiles.BufferID != 0u &&
                    handles.Profiles.Generation != 0u)
                {
                    if (handles.Profiles.BufferID != (uint)UtilityAIAnxietyVaultBufferIds.Profiles)
                        return false;

                    if (!TryOpenAnxietyVaultView(vault, in handles.Profiles, AnxietyDecayConstants.MaxProfiles, out profiles))
                        return false;

                    profilesResolved = true;
                }

                AnxietyRuntimeTuningDTO sanitized = AnxietyDecayJobMath.SanitizeTuning(in tuning);
                WriteAnxietyTuningDirect(tuningBuffer, in sanitized);
                if (profilesResolved && profiles.IsCreated && profiles.Length > 0)
                {
                    AnxietyProfileDTO profile = AnxietyDecayDefaults.BuildProfile();
                    profile.FearDecayRate = sanitized.BaseFearDecayRate;
                    profile.AggressionDecayRate = sanitized.BaseAggressionDecayRate;
                    profile.CalmingThreshold = sanitized.CalmingThreshold;
                    profiles[0] = profile;
                }

                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(AnxietyTuningProfileMutationGuardMask);
            }
        }

        public static bool TryReadLatestAnxietyTelemetry(in UtilityAIAnxietyVaultBuffers buffers, out AnxietyTelemetryEntry entry)
        {
            entry = default;
            if (!buffers.TelemetryRing.IsCreated || buffers.TelemetryRing.Length <= 0)
                return false;

            int cursor = buffers.TelemetryCursor.IsCreated && buffers.TelemetryCursor.Length > 0 ? buffers.TelemetryCursor[0] : 0;
            int index = math.clamp(cursor, 0, buffers.TelemetryRing.Length - 1);
            entry = buffers.TelemetryRing[index];
            return entry.Frame != 0u || entry.ActiveDecayCount != 0u;
        }

        public static bool TryPatchAnxietyTelemetryExecutionTimeAndDump(
            UtilityAIAnxietyVaultBuffers buffers,
            uint frame,
            float exactBurstMicroseconds,
            string projectRoot)
        {
            if (!buffers.TelemetryRing.IsCreated || buffers.TelemetryRing.Length <= 0)
                return false;

            int index = (int)(frame % AnxietyDecayConstants.TelemetryFrames) % buffers.TelemetryRing.Length;
            AnxietyTelemetryEntry entry = buffers.TelemetryRing[index];
            if (entry.Frame != frame)
                return false;

            float faultLimit = AnxietyDecayConstants.FaultMicroseconds;
            if (buffers.Tuning.IsCreated && buffers.Tuning.Length > 0)
                faultLimit = AnxietyDecayJobMath.SanitizePositive(buffers.Tuning[0].FaultMicroseconds, faultLimit);

            entry.BurstMicroseconds = AnxietyDecayJobMath.SanitizeNonNegative(exactBurstMicroseconds, entry.BurstMicroseconds);
            if (entry.BurstMicroseconds > faultLimit)
                entry.FaultFlags |= AnxietyDecayFlags.Fault;
            if (entry.NonFiniteCount > 0u)
                entry.FaultFlags |= AnxietyDecayFlags.NonFiniteInput;

            buffers.TelemetryRing[index] = entry;
            return entry.FaultFlags != 0u && TryDumpAnxietyBlackBox(in buffers, projectRoot, frame);
        }

        public static bool TryDumpAnxietyBlackBox(in UtilityAIAnxietyVaultBuffers buffers, string projectRoot, uint frame = 0u)
        {
            _ = projectRoot;
            _ = frame;
            return buffers.TelemetryRing.IsCreated && buffers.TelemetryRing.Length > 0;
        }

#if UNITY_EDITOR
        public static bool TryLoadPsychologyProfiles(IDataVault vault, ref UtilityAIAnxietyVaultHandles handles, string projectRoot)
        {
            if (!TryResolveAnxietyViews(vault, ref handles, out UtilityAIAnxietyVaultBuffers buffers) ||
                !buffers.CsvScratch.IsCreated ||
                !buffers.Profiles.IsCreated ||
                !buffers.Tuning.IsCreated)
            {
                return false;
            }

            string path = ResolvePsychologyCsvPath(projectRoot);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            int length = ReadFileIntoAnxietyScratch(path, buffers.CsvScratch);
            if (length <= 0)
                return false;

            uint csvHash = HashAnxietyBytes(buffers.CsvScratch, length);
            int profileCount = TryParsePsychologyProfiles(buffers.CsvScratch, length, buffers.Profiles, out _);
            if (profileCount <= 0)
                return false;

            AnxietyRuntimeTuningDTO raw = buffers.Tuning[0];
            AnxietyRuntimeTuningDTO tuning = AnxietyDecayJobMath.SanitizeTuning(in raw);
            AnxietyProfileDTO profile = buffers.Profiles[0];
            tuning.BaseFearDecayRate = profile.FearDecayRate;
            tuning.BaseAggressionDecayRate = profile.AggressionDecayRate;
            tuning.CalmingThreshold = profile.CalmingThreshold;
            tuning.ActiveProfileCount = (uint)profileCount;
            tuning.LastCsvHash = csvHash;
            tuning.CsvReloadVersion++;
            WriteAnxietyTuningDirect(buffers.Tuning, in tuning);
            return true;
        }

        public static bool TryPollPsychologyProfiles(IDataVault vault, ref UtilityAIAnxietyVaultHandles handles, string projectRoot)
        {
            if (!TryResolveAnxietyViews(vault, ref handles, out UtilityAIAnxietyVaultBuffers buffers) ||
                !buffers.CsvScratch.IsCreated ||
                !buffers.Tuning.IsCreated ||
                buffers.Tuning.Length <= 0)
            {
                return false;
            }

            string path = ResolvePsychologyCsvPath(projectRoot);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            int length = ReadFileIntoAnxietyScratch(path, buffers.CsvScratch);
            if (length <= 0)
                return false;

            uint csvHash = HashAnxietyBytes(buffers.CsvScratch, length);
            if (csvHash == buffers.Tuning[0].LastCsvHash)
                return false;

            return TryLoadPsychologyProfiles(vault, ref handles, projectRoot);
        }
#endif

        public static bool ValidateAnxietyLayouts()
        {
            return UnsafeUtility.SizeOf<AnxietyProfileDTO>() == 16 &&
                   UnsafeUtility.AlignOf<AnxietyProfileDTO>() == 4 &&
                   UnsafeUtility.SizeOf<AnxietyRuntimeTuningDTO>() == 64 &&
                   UnsafeUtility.SizeOf<AnxietyDecayScratchDTO>() == 64 &&
                   UnsafeUtility.SizeOf<AnxietyTelemetryEntry>() == 64 &&
                   UnsafeUtility.SizeOf<AnxietyShelterSdfHeaderDTO>() == 64;
        }

        public static bool TryRunAnxietySelfAudit(out uint failureMask)
        {
            failureMask = 0u;
            failureMask |= UnsafeUtility.SizeOf<AnxietyProfileDTO>() == 16 ? 0u : 1u << 0;
            failureMask |= UnsafeUtility.AlignOf<AnxietyProfileDTO>() == 4 ? 0u : 1u << 1;
            failureMask |= UnsafeUtility.SizeOf<AnxietyRuntimeTuningDTO>() == 64 ? 0u : 1u << 2;
            failureMask |= UnsafeUtility.SizeOf<AnxietyDecayScratchDTO>() == 64 ? 0u : 1u << 3;
            failureMask |= UnsafeUtility.SizeOf<AnxietyTelemetryEntry>() == 64 ? 0u : 1u << 4;
            failureMask |= UnsafeUtility.SizeOf<AnxietyShelterSdfHeaderDTO>() == 64 ? 0u : 1u << 5;
            failureMask |= AnxietyDecayConstants.TelemetryFrames == 300 ? 0u : 1u << 6;
            failureMask |= AnxietyDecayConstants.FaultMicroseconds <= 500f ? 0u : 1u << 7;
            return failureMask == 0u;
        }

        private static bool TryReadExistingAnxietyHandles(IDataVault vault, out UtilityAIAnxietyVaultHandles handles)
        {
            handles = default;
            bool resolved = vault.TryGetGenerationHandle<AnxietyProfileDTO>(UtilityAIAnxietyVaultBufferIds.Profiles, out handles.Profiles) &&
                   vault.TryGetGenerationHandle<AnxietyRuntimeTuningDTO>(UtilityAIAnxietyVaultBufferIds.Tuning, out handles.Tuning) &&
                   vault.TryGetGenerationHandle<AnxietyDecayScratchDTO>(UtilityAIAnxietyVaultBufferIds.Scratch, out handles.Scratch) &&
                   vault.TryGetGenerationHandle<AnxietyTelemetryEntry>(UtilityAIAnxietyVaultBufferIds.TelemetryRing, out handles.TelemetryRing) &&
                   vault.TryGetGenerationHandle<int>(UtilityAIAnxietyVaultBufferIds.TelemetryCursor, out handles.TelemetryCursor) &&
                   vault.TryGetGenerationHandle<float>(UtilityAIAnxietyVaultBufferIds.ShelterSdf, out handles.ShelterSdf) &&
                   vault.TryGetGenerationHandle<AnxietyShelterSdfHeaderDTO>(UtilityAIAnxietyVaultBufferIds.ShelterHeader, out handles.ShelterHeader);
#if UNITY_EDITOR
            resolved = resolved &&
                       vault.TryGetGenerationHandle<byte>(UtilityAIAnxietyVaultBufferIds.CsvScratch, out handles.CsvScratch);
#endif
            return resolved;
        }

        private static bool TryOpenAnxietyVaultView<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   handle.BufferID != 0u &&
                   handle.Generation != 0u &&
                   requiredLength >= 0 &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static ulong AnxietyVaultMutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private static void EnsureAnxietyColdDefaults(UtilityAIAnxietyVaultBuffers buffers, bool forceWrite)
        {
            if (buffers.Tuning.IsCreated && buffers.Tuning.Length > 0)
            {
                AnxietyRuntimeTuningDTO tuning = buffers.Tuning[0];
                bool invalid = forceWrite ||
                               (tuning.Flags & AnxietyDecayFlags.Active) == 0u ||
                               !math.isfinite(tuning.BaseFearDecayRate) ||
                               !math.isfinite(tuning.BaseAggressionDecayRate) ||
                               tuning.BaseFearDecayRate <= 0f ||
                               tuning.BaseAggressionDecayRate <= 0f;
                if (invalid)
                    WriteAnxietyTuningDirect(buffers.Tuning, AnxietyDecayDefaults.BuildTuning());
            }

            if (buffers.Profiles.IsCreated && buffers.Profiles.Length > 0)
            {
                AnxietyProfileDTO profile = buffers.Profiles[0];
                if (forceWrite || !math.isfinite(profile.FearDecayRate) || profile.FearDecayRate <= 0f)
                    buffers.Profiles[0] = AnxietyDecayDefaults.BuildProfile();
            }

            if (buffers.ShelterHeader.IsCreated && buffers.ShelterHeader.Length > 0)
            {
                AnxietyShelterSdfHeaderDTO header = buffers.ShelterHeader[0];
                if (forceWrite || header.Dimensions.x <= 0 || !math.isfinite(header.VoxelSizeMeters) || header.VoxelSizeMeters <= 0f)
                    buffers.ShelterHeader[0] = AnxietyDecayDefaults.BuildShelterHeader();
            }

            if (forceWrite && buffers.TelemetryCursor.IsCreated && buffers.TelemetryCursor.Length > 0)
                buffers.TelemetryCursor[0] = 0;

            if (forceWrite && buffers.TelemetryRing.IsCreated)
            {
                for (int i = 0; i < buffers.TelemetryRing.Length; i++)
                    buffers.TelemetryRing[i] = default;
            }
        }

        private static void WriteAnxietyTuningDirect(NativeArray<AnxietyRuntimeTuningDTO> tuningBuffer, in AnxietyRuntimeTuningDTO tuning)
        {
            if (!tuningBuffer.IsCreated || tuningBuffer.Length <= 0)
                return;

            tuningBuffer[0] = tuning;
        }

#if UNITY_EDITOR
        private static string ResolvePsychologyCsvPath(string projectRoot)
        {
            try
            {
                string root = string.IsNullOrEmpty(projectRoot) ? Directory.GetCurrentDirectory() : projectRoot;
                string sourceDataPath = Path.Combine(root, "Assets", "_SourceData", "AI", AnxietyCsvFileName);
                if (File.Exists(sourceDataPath))
                    return sourceDataPath;

                string dataPath = Path.Combine(root, "Data", "AI", AnxietyCsvFileName);
                if (File.Exists(dataPath))
                    return dataPath;

                string rootPath = Path.Combine(root, AnxietyCsvFileName);
                if (File.Exists(rootPath))
                    return rootPath;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }

            return null;
        }

        private static unsafe int ReadFileIntoAnxietyScratch(string path, NativeArray<byte> scratch)
        {
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    int total = 0;
                    int max = scratch.Length;
                    byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(scratch);
                    Span<byte> target = new Span<byte>(ptr, max);
                    while (total < max)
                    {
                        int read = stream.Read(target.Slice(total));
                        if (read <= 0)
                            break;

                        total += read;
                    }

                    return total;
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
            catch (NotSupportedException)
            {
                return 0;
            }
        }

        private static unsafe int TryParsePsychologyProfiles(
            NativeArray<byte> bytes,
            int length,
            NativeArray<AnxietyProfileDTO> profiles,
            out uint profileHash)
        {
            profileHash = 2166136261u;
            if (!bytes.IsCreated || !profiles.IsCreated || length <= 0)
                return 0;

            int count = math.min(length, bytes.Length);
            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(bytes);
            ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(ptr, count);
            int rowStart = 0;
            int written = 0;
            while (rowStart < span.Length && written < profiles.Length)
            {
                int rowEnd = rowStart;
                while (rowEnd < span.Length && span[rowEnd] != (byte)'\n')
                    rowEnd++;

                ReadOnlySpan<byte> row = TrimRow(span.Slice(rowStart, rowEnd - rowStart));
                rowStart = rowEnd + 1;
                if (row.Length <= 0 || row[0] == (byte)'#')
                    continue;

                int cursor = 0;
                ReadOnlySpan<byte> species = NextCsvToken(row, ref cursor);
                if (species.Length == 0 || EqualsAscii(species, "species") || EqualsAscii(species, "species_hash"))
                    continue;

                ReadOnlySpan<byte> fearToken = NextCsvToken(row, ref cursor);
                ReadOnlySpan<byte> aggressionToken = NextCsvToken(row, ref cursor);
                ReadOnlySpan<byte> thresholdToken = NextCsvToken(row, ref cursor);
                if (!AupPrecisionMath.TryParseFloat(fearToken, out float fearRate) ||
                    !AupPrecisionMath.TryParseFloat(aggressionToken, out float aggressionRate) ||
                    !AupPrecisionMath.TryParseFloat(thresholdToken, out float threshold))
                {
                    continue;
                }

                AnxietyProfileDTO profile = default;
                profile.FearDecayRate = AnxietyDecayJobMath.SanitizePositive(fearRate, AnxietyDecayConstants.DefaultFearDecayRate);
                profile.AggressionDecayRate = AnxietyDecayJobMath.SanitizePositive(aggressionRate, AnxietyDecayConstants.DefaultAggressionDecayRate);
                profile.CalmingThreshold = AnxietyDecayJobMath.SanitizePositive(threshold, AnxietyDecayConstants.DefaultCalmingThreshold);
                profiles[written++] = profile;

                uint speciesHash = AupPrecisionMath.HashFnv1A32(species);
                profileHash = UtilityAICognitionJobMath.Fnv(profileHash, speciesHash);
                profileHash = UtilityAICognitionJobMath.Fnv(profileHash, math.asuint(profile.FearDecayRate));
                profileHash = UtilityAICognitionJobMath.Fnv(profileHash, math.asuint(profile.AggressionDecayRate));
                profileHash = UtilityAICognitionJobMath.Fnv(profileHash, math.asuint(profile.CalmingThreshold));
            }

            return written;
        }

        private static uint HashAnxietyBytes(NativeArray<byte> bytes, int length)
        {
            if (!bytes.IsCreated || length <= 0)
                return 0u;

            int count = math.min(length, bytes.Length);
            uint hash = 2166136261u;
            for (int i = 0; i < count; i++)
                hash = UtilityAICognitionJobMath.Fnv(hash, bytes[i]);

            return hash == 0u ? 1u : hash;
        }

        private static ReadOnlySpan<byte> TrimRow(ReadOnlySpan<byte> row)
        {
            int start = 0;
            int end = row.Length;
            while (start < end && row[start] <= (byte)' ')
                start++;
            while (end > start && row[end - 1] <= (byte)' ')
                end--;

            return row.Slice(start, end - start);
        }

        private static ReadOnlySpan<byte> NextCsvToken(ReadOnlySpan<byte> row, ref int cursor)
        {
            int start = math.clamp(cursor, 0, row.Length);
            while (start < row.Length && (row[start] == (byte)' ' || row[start] == (byte)'\t'))
                start++;

            int end = start;
            while (end < row.Length && row[end] != (byte)',')
                end++;

            int trimmedEnd = end;
            while (trimmedEnd > start && (row[trimmedEnd - 1] == (byte)' ' || row[trimmedEnd - 1] == (byte)'\t' || row[trimmedEnd - 1] == (byte)'\r'))
                trimmedEnd--;

            cursor = end < row.Length ? end + 1 : row.Length;
            return row.Slice(start, trimmedEnd - start);
        }

        private static bool EqualsAscii(ReadOnlySpan<byte> bytes, string ascii)
        {
            if (bytes.Length != ascii.Length)
                return false;

            for (int i = 0; i < bytes.Length; i++)
            {
                byte a = bytes[i];
                byte b = (byte)ascii[i];
                if (a >= (byte)'A' && a <= (byte)'Z')
                    a = (byte)(a + 32);
                if (a != b)
                    return false;
            }

            return true;
        }
#endif

    }
}
