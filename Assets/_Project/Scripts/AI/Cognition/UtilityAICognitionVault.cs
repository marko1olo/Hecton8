using System;
using System.IO;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.AI.Cognition
{
    public static class UtilityAICognitionVaultBufferIds
    {
        public const BufferID States = BufferID.UtilityAICognitionVault_States;
        public const BufferID Aups = BufferID.UtilityAICognitionVault_Aups;
        public const BufferID Targets = BufferID.UtilityAICognitionVault_Targets;
        public const BufferID TargetNext = BufferID.UtilityAICognitionVault_TargetNext;
        public const BufferID BucketHeads = BufferID.UtilityAICognitionVault_BucketHeads;
        public const BufferID Tuning = BufferID.UtilityAICognitionVault_Tuning;
        public const BufferID Outputs = BufferID.UtilityAICognitionVault_Outputs;
        public const BufferID TelemetryRing = BufferID.UtilityAICognitionVault_TelemetryRing;
        public const BufferID TelemetryCursor = BufferID.UtilityAICognitionVault_TelemetryCursor;
        public const BufferID Profiles = BufferID.UtilityAICognitionVault_Profiles;
#if UNITY_EDITOR
        public const BufferID CsvScratch = BufferID.UtilityAICognitionVault_CsvScratch;
#endif
    }

    public struct UtilityAICognitionVaultHandles
    {
        public VaultGenerationHandle<CognitionStateDTO> States;
        public VaultGenerationHandle<CognitionAupDTO> Aups;
        public VaultGenerationHandle<CognitionTargetCandidateDTO> Targets;
        public VaultGenerationHandle<int> TargetNext;
        public VaultGenerationHandle<int> BucketHeads;
        public VaultGenerationHandle<CognitionUtilityTuningDTO> Tuning;
        public VaultGenerationHandle<CognitionActionOutputDTO> Outputs;
        public VaultGenerationHandle<CognitionTelemetryEntry> TelemetryRing;
        public VaultGenerationHandle<int> TelemetryCursor;
        public VaultGenerationHandle<CognitionProfileDTO> Profiles;
#if UNITY_EDITOR
        public VaultGenerationHandle<byte> CsvScratch;
#endif

        public bool IsCreated()
        {
            return IsHandleCreated(in States) &&
                   IsHandleCreated(in Aups) &&
                   IsHandleCreated(in Targets) &&
                   IsHandleCreated(in TargetNext) &&
                   IsHandleCreated(in BucketHeads) &&
                   IsHandleCreated(in Tuning) &&
                   IsHandleCreated(in Outputs) &&
                   IsHandleCreated(in TelemetryRing) &&
                   IsHandleCreated(in TelemetryCursor) &&
                   IsHandleCreated(in Profiles)
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

    public ref struct UtilityAICognitionVaultBuffers
    {
        public NativeArray<CognitionStateDTO> States;
        public NativeArray<CognitionAupDTO> Aups;
        public NativeArray<CognitionTargetCandidateDTO> Targets;
        public NativeArray<int> TargetNext;
        public NativeArray<int> BucketHeads;
        public NativeArray<CognitionUtilityTuningDTO> Tuning;
        public NativeArray<CognitionActionOutputDTO> Outputs;
        public NativeArray<CognitionTelemetryEntry> TelemetryRing;
        public NativeArray<int> TelemetryCursor;
        public NativeArray<CognitionProfileDTO> Profiles;
#if UNITY_EDITOR
        public NativeArray<byte> CsvScratch;
#endif

        public bool IsCreated()
        {
            return States.IsCreated &&
                   Aups.IsCreated &&
                   Targets.IsCreated &&
                   TargetNext.IsCreated &&
                   BucketHeads.IsCreated &&
                   Tuning.IsCreated &&
                   Outputs.IsCreated &&
                   TelemetryRing.IsCreated &&
                   TelemetryCursor.IsCreated &&
                   Profiles.IsCreated
#if UNITY_EDITOR
                   && CsvScratch.IsCreated
#endif
                   ;
        }
    }

    public static partial class UtilityAICognitionVault
    {
        private const uint DumpMagic = 0x55333032u;
        private const uint DumpEndianMarker = 0x01020304u;
        private const uint DumpVersion = 1u;
        private const string DumpFileName = "Dump_SHINOBU_302.bin";
        private const string Agent1300DumpFileName = "Dump_1300_AICognition.bin";
        private static readonly ulong UtilityTuningMutationGuardMask =
            UtilityVaultMutationGuardBit(UtilityAICognitionVaultBufferIds.Tuning);
#if UNITY_EDITOR
        private const string CsvFileName = "fauna_cognition_profiles.csv";
#endif

        public static bool TryAcquireHandles(IDataVault vault, out UtilityAICognitionVaultHandles handles)
        {
            handles = default;
            if (vault == null)
                return false;

            if (vault.IsAllocationLocked)
            {
                if (!TryReadExistingHandles(vault, out handles))
                    return false;

                UtilityAICognitionVaultBuffers lockedBuffers;
                return TryResolveViews(vault, ref handles, out lockedBuffers);
            }

            handles.States = vault.EnsureGenerationHandle<CognitionStateDTO>(
                UtilityAICognitionVaultBufferIds.States,
                UtilityAICognitionConstants.MaxCreatures,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            handles.Aups = vault.EnsureGenerationHandle<CognitionAupDTO>(
                UtilityAICognitionVaultBufferIds.Aups,
                UtilityAICognitionConstants.MaxCreatures,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            handles.Targets = vault.EnsureGenerationHandle<CognitionTargetCandidateDTO>(
                UtilityAICognitionVaultBufferIds.Targets,
                UtilityAICognitionConstants.MaxTargets,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            handles.TargetNext = vault.EnsureGenerationHandle<int>(
                UtilityAICognitionVaultBufferIds.TargetNext,
                UtilityAICognitionConstants.MaxTargets,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            handles.BucketHeads = vault.EnsureGenerationHandle<int>(
                UtilityAICognitionVaultBufferIds.BucketHeads,
                UtilityAICognitionConstants.TargetBucketCount,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            handles.Tuning = vault.EnsureGenerationHandle<CognitionUtilityTuningDTO>(
                UtilityAICognitionVaultBufferIds.Tuning,
                1,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            handles.Outputs = vault.EnsureGenerationHandle<CognitionActionOutputDTO>(
                UtilityAICognitionVaultBufferIds.Outputs,
                UtilityAICognitionConstants.MaxCreatures,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            handles.TelemetryRing = vault.EnsureGenerationHandle<CognitionTelemetryEntry>(
                UtilityAICognitionVaultBufferIds.TelemetryRing,
                UtilityAICognitionConstants.TelemetryFrames,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            handles.TelemetryCursor = vault.EnsureGenerationHandle<int>(
                UtilityAICognitionVaultBufferIds.TelemetryCursor,
                1,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            handles.Profiles = vault.EnsureGenerationHandle<CognitionProfileDTO>(
                UtilityAICognitionVaultBufferIds.Profiles,
                UtilityAICognitionConstants.MaxProfiles,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
#if UNITY_EDITOR
            handles.CsvScratch = vault.EnsureGenerationHandle<byte>(
                UtilityAICognitionVaultBufferIds.CsvScratch,
                UtilityAICognitionConstants.CsvScratchBytes,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
#endif

            if (!TryResolveViews(vault, ref handles, out UtilityAICognitionVaultBuffers buffers))
                return false;

            EnsureColdDefaults(buffers);
            return true;
        }

        public static bool TryResolveViews(IDataVault vault, ref UtilityAICognitionVaultHandles handles, out UtilityAICognitionVaultBuffers buffers)
        {
            buffers = default;
            if (vault == null || !handles.IsCreated())
                return false;

            if (!TryOpenVaultView(vault, in handles.States, UtilityAICognitionConstants.MaxCreatures, out buffers.States) ||
                !TryOpenVaultView(vault, in handles.Aups, UtilityAICognitionConstants.MaxCreatures, out buffers.Aups) ||
                !TryOpenVaultView(vault, in handles.Targets, UtilityAICognitionConstants.MaxTargets, out buffers.Targets) ||
                !TryOpenVaultView(vault, in handles.TargetNext, UtilityAICognitionConstants.MaxTargets, out buffers.TargetNext) ||
                !TryOpenVaultView(vault, in handles.BucketHeads, UtilityAICognitionConstants.TargetBucketCount, out buffers.BucketHeads) ||
                !TryOpenVaultView(vault, in handles.Tuning, 1, out buffers.Tuning) ||
                !TryOpenVaultView(vault, in handles.Outputs, UtilityAICognitionConstants.MaxCreatures, out buffers.Outputs) ||
                !TryOpenVaultView(vault, in handles.TelemetryRing, UtilityAICognitionConstants.TelemetryFrames, out buffers.TelemetryRing) ||
                !TryOpenVaultView(vault, in handles.TelemetryCursor, 1, out buffers.TelemetryCursor) ||
                !TryOpenVaultView(vault, in handles.Profiles, UtilityAICognitionConstants.MaxProfiles, out buffers.Profiles))
            {
                buffers = default;
                return false;
            }

#if UNITY_EDITOR
            if (!TryOpenVaultView(vault, in handles.CsvScratch, UtilityAICognitionConstants.CsvScratchBytes, out buffers.CsvScratch))
            {
                buffers = default;
                return false;
            }
#endif

            return buffers.IsCreated();
        }

        public static bool TryScheduleMockData(
            in UtilityAICognitionVaultBuffers buffers,
            uint frame,
            JobHandle inputDependency,
            out JobHandle outputDependency)
        {
            outputDependency = inputDependency;
            if (!buffers.IsCreated())
                return false;

            int scheduleLength = math.max(buffers.States.Length, math.max(buffers.Aups.Length, buffers.Targets.Length));
            GenerateMockCognitionLoadJob mockJob = new GenerateMockCognitionLoadJob
            {
                States = buffers.States,
                Aups = buffers.Aups,
                Targets = buffers.Targets,
                Tuning = buffers.Tuning,
                Frame = frame
            };

            JobHandle mockHandle = mockJob.Schedule(scheduleLength, 64, inputDependency);
            BuildCognitionTargetBucketsJob bucketJob = new BuildCognitionTargetBucketsJob
            {
                Targets = buffers.Targets,
                Tuning = buffers.Tuning,
                BucketHeads = buffers.BucketHeads,
                TargetNext = buffers.TargetNext,
                TargetCount = math.min(buffers.Targets.Length, UtilityAICognitionConstants.MaxTargets)
            };
            outputDependency = bucketJob.Schedule(mockHandle);
            return true;
        }

        public static bool TryScheduleCognitionPass(
            in UtilityAICognitionVaultBuffers buffers,
            uint frame,
            float deltaSeconds,
            float estimatedBurstMicroseconds,
            NativeArray<CognitionMovementAcousticSignalDTO>.ReadOnly movementSignals,
            int movementSignalCount,
            NativeArray<CognitionCombatDamageSignalDTO>.ReadOnly damageSignals,
            int damageSignalCount,
            JobHandle inputDependency,
            out JobHandle outputDependency)
        {
            outputDependency = inputDependency;
            if (!buffers.IsCreated())
                return false;

            int scheduleLength = GetScheduleLength(in buffers);
            if (scheduleLength <= 0)
                return false;

            IntegrateCognitionSensoryInputJob sensoryJob = new IntegrateCognitionSensoryInputJob
            {
                States = buffers.States,
                Aups = buffers.Aups,
                MovementSignals = movementSignals,
                DamageSignals = damageSignals,
                Tuning = buffers.Tuning,
                MovementSignalCount = movementSignalCount,
                DamageSignalCount = damageSignalCount,
                DeltaSeconds = deltaSeconds
            };
            JobHandle sensoryHandle = sensoryJob.Schedule(scheduleLength, 64, inputDependency);

            BuildCognitionTargetBucketsJob bucketJob = new BuildCognitionTargetBucketsJob
            {
                Targets = buffers.Targets,
                Tuning = buffers.Tuning,
                BucketHeads = buffers.BucketHeads,
                TargetNext = buffers.TargetNext,
                TargetCount = math.min(buffers.Targets.Length, UtilityAICognitionConstants.MaxTargets)
            };
            JobHandle bucketHandle = bucketJob.Schedule(sensoryHandle);

            EvaluateUtilityCognitionJob evaluateJob = new EvaluateUtilityCognitionJob
            {
                States = buffers.States,
                Aups = buffers.Aups,
                Targets = buffers.Targets,
                BucketHeads = buffers.BucketHeads,
                TargetNext = buffers.TargetNext,
                Tuning = buffers.Tuning,
                Outputs = buffers.Outputs,
                Frame = frame,
                TargetCount = math.min(buffers.Targets.Length, UtilityAICognitionConstants.MaxTargets)
            };
            JobHandle evaluateHandle = evaluateJob.Schedule(scheduleLength, 64, bucketHandle);

            RecordCognitionTelemetryJob telemetryJob = new RecordCognitionTelemetryJob
            {
                States = buffers.States,
                Outputs = buffers.Outputs,
                Tuning = buffers.Tuning,
                TelemetryRing = buffers.TelemetryRing,
                TelemetryCursor = buffers.TelemetryCursor,
                Frame = frame,
                BurstMicroseconds = estimatedBurstMicroseconds
            };
            outputDependency = telemetryJob.Schedule(evaluateHandle);
            return true;
        }

        public static bool TryGetTuning(IDataVault vault, ref UtilityAICognitionVaultHandles handles, out CognitionUtilityTuningDTO tuning)
        {
            tuning = default;
            if (vault == null ||
                handles.Tuning.BufferID == 0u ||
                handles.Tuning.Generation == 0u ||
                !vault.TryReadOnlyHandle(in handles.Tuning, out NativeArray<CognitionUtilityTuningDTO>.ReadOnly tuningBuffer) ||
                tuningBuffer.Length <= 0)
            {
                return false;
            }

            CognitionUtilityTuningDTO rawTuning = tuningBuffer[0];
            tuning = UtilityAICognitionJobMath.SanitizeTuning(in rawTuning);
            return true;
        }

        public static bool TrySetTuning(IDataVault vault, ref UtilityAICognitionVaultHandles handles, in CognitionUtilityTuningDTO tuning)
        {
            if (vault == null ||
                handles.Tuning.BufferID != (uint)UtilityAICognitionVaultBufferIds.Tuning ||
                handles.Tuning.Generation == 0u ||
                !TryAcquireUtilityCognitionMutationGuard(vault, UtilityTuningMutationGuardMask))
            {
                return false;
            }

            try
            {
                if (!TryOpenVaultView(vault, in handles.Tuning, 1, out NativeArray<CognitionUtilityTuningDTO> tuningBuffer))
                    return false;

                if (!tuningBuffer.IsCreated || tuningBuffer.Length <= 0)
                    return false;

                CognitionUtilityTuningDTO sanitized = UtilityAICognitionJobMath.SanitizeTuning(in tuning);
                WriteTuningDirect(tuningBuffer, in sanitized);
                return true;
            }
            finally
            {
                ReleaseUtilityCognitionMutationGuard(vault, UtilityTuningMutationGuardMask);
            }
        }

#if UNITY_EDITOR
        public static bool TryLoadCsvProfiles(IDataVault vault, ref UtilityAICognitionVaultHandles handles, string projectRoot)
        {
            if (!TryResolveViews(vault, ref handles, out UtilityAICognitionVaultBuffers buffers) ||
                !buffers.CsvScratch.IsCreated ||
                !buffers.Profiles.IsCreated ||
                !buffers.Tuning.IsCreated)
            {
                return false;
            }

            string path = ResolveCsvPath(projectRoot);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            int length = ReadFileIntoNativeScratch(path, buffers.CsvScratch);
            if (length <= 0)
                return false;

            uint csvHash = HashBytes(buffers.CsvScratch, length);
            int profileCount = TryParseProfiles(buffers.CsvScratch, length, buffers.Profiles);
            if (profileCount <= 0)
                return false;

            CognitionUtilityTuningDTO rawTuning = buffers.Tuning[0];
            CognitionUtilityTuningDTO tuning = UtilityAICognitionJobMath.SanitizeTuning(in rawTuning);
            CognitionProfileDTO profile = buffers.Profiles[0];
            ApplyProfileToTuning(in profile, ref tuning);
            tuning.LastCsvHash = csvHash;
            tuning.CsvReloadVersion++;
            WriteTuningDirect(buffers.Tuning, in tuning);
            return true;
        }

        public static bool TryPollCsvProfiles(IDataVault vault, ref UtilityAICognitionVaultHandles handles, string projectRoot)
        {
            if (!TryResolveViews(vault, ref handles, out UtilityAICognitionVaultBuffers buffers) ||
                !buffers.CsvScratch.IsCreated ||
                !buffers.Tuning.IsCreated ||
                buffers.Tuning.Length <= 0)
            {
                return false;
            }

            string path = ResolveCsvPath(projectRoot);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            int length = ReadFileIntoNativeScratch(path, buffers.CsvScratch);
            if (length <= 0)
                return false;

            uint csvHash = HashBytes(buffers.CsvScratch, length);
            if (csvHash == buffers.Tuning[0].LastCsvHash)
                return false;

            int profileCount = TryParseProfiles(buffers.CsvScratch, length, buffers.Profiles);
            if (profileCount <= 0)
                return false;

            CognitionUtilityTuningDTO rawTuning = buffers.Tuning[0];
            CognitionUtilityTuningDTO tuning = UtilityAICognitionJobMath.SanitizeTuning(in rawTuning);
            CognitionProfileDTO profile = buffers.Profiles[0];
            ApplyProfileToTuning(in profile, ref tuning);
            tuning.LastCsvHash = csvHash;
            tuning.CsvReloadVersion++;
            WriteTuningDirect(buffers.Tuning, in tuning);
            return true;
        }
#endif

        public static bool TryPatchTelemetryExecutionTimeAndDump(
            UtilityAICognitionVaultBuffers buffers,
            uint frame,
            float exactBurstMicroseconds,
            string projectRoot)
        {
            if (!buffers.TelemetryRing.IsCreated || buffers.TelemetryRing.Length <= 0)
                return false;

            int index = (int)(frame % UtilityAICognitionConstants.TelemetryFrames) % buffers.TelemetryRing.Length;
            CognitionTelemetryEntry entry = buffers.TelemetryRing[index];
            if (entry.Frame != frame)
                return false;

            entry.BurstMicroseconds = UtilityAICognitionJobMath.SanitizeNonNegative(exactBurstMicroseconds, entry.BurstMicroseconds);
            float faultLimit = UtilityAICognitionConstants.FaultMicroseconds;
            if (buffers.Tuning.IsCreated && buffers.Tuning.Length > 0)
                faultLimit = UtilityAICognitionJobMath.SanitizePositive(buffers.Tuning[0].Runtime.w, faultLimit);

            if (entry.BurstMicroseconds > faultLimit)
                entry.FaultFlags |= UtilityAICognitionActionFlags.OverBudget;

            buffers.TelemetryRing[index] = entry;
            return entry.FaultFlags != 0u && TryDumpBlackBox(in buffers, projectRoot, frame);
        }

        public static bool TryDumpBlackBox(in UtilityAICognitionVaultBuffers buffers, string projectRoot, uint frame = 0u)
        {
            _ = projectRoot;
            _ = frame;
            return buffers.TelemetryRing.IsCreated && buffers.TelemetryRing.Length > 0;
        }

        public static bool TryReadLatestTelemetry(in UtilityAICognitionVaultBuffers buffers, out CognitionTelemetryEntry entry)
        {
            entry = default;
            if (!buffers.TelemetryRing.IsCreated || buffers.TelemetryRing.Length <= 0)
                return false;

            int cursor = buffers.TelemetryCursor.IsCreated && buffers.TelemetryCursor.Length > 0 ? buffers.TelemetryCursor[0] : 0;
            int index = math.clamp(cursor, 0, buffers.TelemetryRing.Length - 1);
            entry = buffers.TelemetryRing[index];
            return true;
        }

        public static bool ValidateLayouts()
        {
            return UnsafeUtility.SizeOf<CognitionStateDTO>() == UtilityAICognitionStateLayout.SizeBytes &&
                   UnsafeUtility.SizeOf<CognitionAupDTO>() == 32 &&
                   UnsafeUtility.SizeOf<CognitionTargetCandidateDTO>() == 64 &&
                   UnsafeUtility.SizeOf<CognitionUtilityTuningDTO>() == 128 &&
                   UnsafeUtility.SizeOf<CognitionActionOutputDTO>() == 64 &&
                   UnsafeUtility.SizeOf<CognitionProfileDTO>() == 96 &&
                   UnsafeUtility.SizeOf<CognitionMovementAcousticSignalDTO>() == 64 &&
                   UnsafeUtility.SizeOf<CognitionCombatDamageSignalDTO>() == 64 &&
                   UnsafeUtility.SizeOf<CognitionTelemetryEntry>() == 64 &&
                   UnsafeUtility.SizeOf<CognitionDumpHeaderDTO>() == 32;
        }

        public static void ReleaseOwnedHandles(IDataVault vault, ref UtilityAICognitionVaultHandles handles)
        {
            if (vault == null)
            {
                handles = default;
                return;
            }

            ReleaseVaultHandle(vault, ref handles.States);
            ReleaseVaultHandle(vault, ref handles.Aups);
            ReleaseVaultHandle(vault, ref handles.Targets);
            ReleaseVaultHandle(vault, ref handles.TargetNext);
            ReleaseVaultHandle(vault, ref handles.BucketHeads);
            ReleaseVaultHandle(vault, ref handles.Tuning);
            ReleaseVaultHandle(vault, ref handles.Outputs);
            ReleaseVaultHandle(vault, ref handles.TelemetryRing);
            ReleaseVaultHandle(vault, ref handles.TelemetryCursor);
            ReleaseVaultHandle(vault, ref handles.Profiles);
#if UNITY_EDITOR
            ReleaseVaultHandle(vault, ref handles.CsvScratch);
#endif
        }

        private static bool TryReadExistingHandles(IDataVault vault, out UtilityAICognitionVaultHandles handles)
        {
            handles = default;
            bool acquired =
                vault.TryGetGenerationHandle<CognitionStateDTO>(UtilityAICognitionVaultBufferIds.States, out handles.States) &&
                vault.TryGetGenerationHandle<CognitionAupDTO>(UtilityAICognitionVaultBufferIds.Aups, out handles.Aups) &&
                vault.TryGetGenerationHandle<CognitionTargetCandidateDTO>(UtilityAICognitionVaultBufferIds.Targets, out handles.Targets) &&
                vault.TryGetGenerationHandle<int>(UtilityAICognitionVaultBufferIds.TargetNext, out handles.TargetNext) &&
                vault.TryGetGenerationHandle<int>(UtilityAICognitionVaultBufferIds.BucketHeads, out handles.BucketHeads) &&
                vault.TryGetGenerationHandle<CognitionUtilityTuningDTO>(UtilityAICognitionVaultBufferIds.Tuning, out handles.Tuning) &&
                vault.TryGetGenerationHandle<CognitionActionOutputDTO>(UtilityAICognitionVaultBufferIds.Outputs, out handles.Outputs) &&
                vault.TryGetGenerationHandle<CognitionTelemetryEntry>(UtilityAICognitionVaultBufferIds.TelemetryRing, out handles.TelemetryRing) &&
                vault.TryGetGenerationHandle<int>(UtilityAICognitionVaultBufferIds.TelemetryCursor, out handles.TelemetryCursor) &&
                vault.TryGetGenerationHandle<CognitionProfileDTO>(UtilityAICognitionVaultBufferIds.Profiles, out handles.Profiles);
#if UNITY_EDITOR
            acquired &= vault.TryGetGenerationHandle<byte>(UtilityAICognitionVaultBufferIds.CsvScratch, out handles.CsvScratch);
#endif
            return acquired;
        }

        private static bool TryOpenVaultView<T>(
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

        private static bool TryAcquireUtilityCognitionMutationGuard(IDataVault vault, ulong guardMask)
        {
            return vault != null &&
                   guardMask != 0UL &&
                   !vault.IsCompactionFenceActive &&
                   vault.TryAcquireMutationGuard(guardMask);
        }

        private static void ReleaseUtilityCognitionMutationGuard(IDataVault vault, ulong guardMask)
        {
            if (guardMask != 0UL)
                vault?.ReleaseMutationGuard(guardMask);
        }

        private static ulong UtilityVaultMutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (handle.BufferID != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static int GetScheduleLength(in UtilityAICognitionVaultBuffers buffers)
        {
            if (!buffers.IsCreated())
                return 0;

            int length = math.min(buffers.States.Length, buffers.Aups.Length);
            length = math.min(length, buffers.Outputs.Length);
            return math.max(0, length);
        }

        private static void EnsureColdDefaults(UtilityAICognitionVaultBuffers buffers)
        {
            if (buffers.Tuning.IsCreated && buffers.Tuning.Length > 0)
            {
                CognitionUtilityTuningDTO tuning = buffers.Tuning[0];
                if (!math.isfinite(tuning.Runtime.x) || tuning.Runtime.x <= 0f)
                {
                    CognitionUtilityTuningDTO fallback = UtilityAICognitionDefaults.BuildTuning();
                    WriteTuningDirect(buffers.Tuning, in fallback);
                }
            }

            if (buffers.Profiles.IsCreated && buffers.Profiles.Length > 0 && buffers.Profiles[0].SpeciesHash == 0u)
                buffers.Profiles[0] = UtilityAICognitionDefaults.BuildFallbackProfile();
        }

        private static void ApplyProfileToTuning(in CognitionProfileDTO profile, ref CognitionUtilityTuningDTO tuning)
        {
            bool valid = profile.SpeciesHash != 0u;
            tuning.HungerPolynomial = math.select(tuning.HungerPolynomial, profile.HungerPolynomial, valid & math.all(math.isfinite(profile.HungerPolynomial)));
            tuning.FearPolynomial = math.select(tuning.FearPolynomial, profile.FearPolynomial, valid & math.all(math.isfinite(profile.FearPolynomial)));
            tuning.AggressionPolynomial = math.select(tuning.AggressionPolynomial, profile.AggressionPolynomial, valid & math.all(math.isfinite(profile.AggressionPolynomial)));
            tuning.ActionBiases.x = math.select(tuning.ActionBiases.x, profile.Weights.x, valid & math.isfinite(profile.Weights.x));
            tuning.ActionBiases.y = math.select(tuning.ActionBiases.y, profile.Weights.y, valid & math.isfinite(profile.Weights.y));
            tuning.ActionBiases.z = math.select(tuning.ActionBiases.z, profile.Weights.z, valid & math.isfinite(profile.Weights.z));
            tuning.ActionBiases.w = math.select(tuning.ActionBiases.w, profile.Weights.w, valid & math.isfinite(profile.Weights.w));
            tuning.DistanceMeters = math.select(tuning.DistanceMeters, profile.DistanceMeters, valid & math.all(math.isfinite(profile.DistanceMeters)));
            tuning = UtilityAICognitionJobMath.SanitizeTuning(in tuning);
        }

        private static void WriteTuningDirect(NativeArray<CognitionUtilityTuningDTO> tuningBuffer, in CognitionUtilityTuningDTO tuning)
        {
            if (!tuningBuffer.IsCreated || tuningBuffer.Length <= 0)
                return;

            tuningBuffer[0] = tuning;
        }

#if UNITY_EDITOR
        private static string ResolveCsvPath(string projectRoot)
        {
            try
            {
                string root = string.IsNullOrEmpty(projectRoot) ? Directory.GetCurrentDirectory() : projectRoot;
                string sourceDataPath = Path.Combine(root, "Assets", "_SourceData", "AI", CsvFileName);
                if (File.Exists(sourceDataPath))
                    return sourceDataPath;

                string dataPath = Path.Combine(root, "Data", "AI", CsvFileName);
                if (File.Exists(dataPath))
                    return dataPath;

                string rootPath = Path.Combine(root, CsvFileName);
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

        private static unsafe int ReadFileIntoNativeScratch(string path, NativeArray<byte> scratch)
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

        private static int TryParseProfiles(NativeArray<byte> bytes, int limit, NativeArray<CognitionProfileDTO> profiles)
        {
            int index = 0;
            int written = 0;
            while (index < limit && written < profiles.Length)
            {
                SkipWhitespaceAndLineBreaks(bytes, limit, ref index);
                if (index >= limit)
                    break;

                int lineStart = index;
                if (!TryParseTokenHash(bytes, limit, ref index, out uint speciesHash) || speciesHash == 0u)
                {
                    index = lineStart;
                    SkipLine(bytes, limit, ref index);
                    continue;
                }

                CognitionProfileDTO fallback = UtilityAICognitionDefaults.BuildFallbackProfile();
                CognitionProfileDTO profile = default;
                profile.SpeciesHash = speciesHash;
                profile.HungerPolynomial = ReadFloat4OrDefault(bytes, limit, ref index, fallback.HungerPolynomial);
                profile.FearPolynomial = ReadFloat4OrDefault(bytes, limit, ref index, fallback.FearPolynomial);
                profile.AggressionPolynomial = ReadFloat4OrDefault(bytes, limit, ref index, fallback.AggressionPolynomial);
                profile.Weights = ReadFloat4OrDefault(bytes, limit, ref index, fallback.Weights);
                profile.DistanceMeters = ReadFloat4OrDefault(bytes, limit, ref index, fallback.DistanceMeters);
                profile.Flags = UtilityAICognitionActionFlags.Active;
                profile.LastAppliedHash = HashProfile(in profile);
                profiles[written++] = profile;
                SkipLine(bytes, limit, ref index);
            }

            return written;
        }

        private static float4 ReadFloat4OrDefault(NativeArray<byte> bytes, int limit, ref int index, float4 fallback)
        {
            float x = TryParseFloatField(bytes, limit, ref index, out float parsedX) ? parsedX : fallback.x;
            float y = TryParseFloatField(bytes, limit, ref index, out float parsedY) ? parsedY : fallback.y;
            float z = TryParseFloatField(bytes, limit, ref index, out float parsedZ) ? parsedZ : fallback.z;
            float w = TryParseFloatField(bytes, limit, ref index, out float parsedW) ? parsedW : fallback.w;
            return new float4(x, y, z, w);
        }

        private static bool TryParseFloatField(NativeArray<byte> bytes, int limit, ref int index, out float value)
        {
            SkipSpaces(bytes, limit, ref index);
            if (index < limit && bytes[index] == (byte)',')
                index++;

            SkipSpaces(bytes, limit, ref index);
            return TryParseFloat(bytes, limit, ref index, out value);
        }

        private static bool TryParseFloat(NativeArray<byte> bytes, int limit, ref int index, out float value)
        {
            value = 0f;
            SkipSpaces(bytes, limit, ref index);
            if (index >= limit || bytes[index] == (byte)'\n' || bytes[index] == (byte)'\r')
                return false;

            float sign = 1f;
            if (bytes[index] == (byte)'-' || bytes[index] == (byte)'+')
            {
                sign = bytes[index] == (byte)'-' ? -1f : 1f;
                index++;
            }

            float integer = 0f;
            int digitCount = 0;
            while (index < limit && IsDigit(bytes[index]))
            {
                integer = (integer * 10f) + (bytes[index] - (byte)'0');
                index++;
                digitCount++;
            }

            float fraction = 0f;
            float place = 0.1f;
            if (index < limit && bytes[index] == (byte)'.')
            {
                index++;
                while (index < limit && IsDigit(bytes[index]))
                {
                    fraction += (bytes[index] - (byte)'0') * place;
                    place *= 0.1f;
                    index++;
                    digitCount++;
                }
            }

            value = (integer + fraction) * sign;
            return digitCount > 0 && math.isfinite(value);
        }

        private static bool TryParseTokenHash(NativeArray<byte> bytes, int limit, ref int index, out uint hash)
        {
            hash = 0u;
            SkipSpaces(bytes, limit, ref index);
            int start = index;
            while (index < limit && bytes[index] != (byte)',' && bytes[index] != (byte)'\n' && bytes[index] != (byte)'\r')
                index++;

            int end = index;
            while (start < end && bytes[start] <= (byte)' ')
                start++;
            while (end > start && bytes[end - 1] <= (byte)' ')
                end--;

            if (start >= end)
                return false;

            bool hex = end - start > 2 && bytes[start] == (byte)'0' && (bytes[start + 1] == (byte)'x' || bytes[start + 1] == (byte)'X');
            if (hex)
            {
                uint value = 0u;
                for (int i = start + 2; i < end; i++)
                {
                    byte c = bytes[i];
                    uint digit = c >= (byte)'0' && c <= (byte)'9'
                        ? (uint)(c - (byte)'0')
                        : c >= (byte)'a' && c <= (byte)'f'
                            ? (uint)(10 + c - (byte)'a')
                            : c >= (byte)'A' && c <= (byte)'F'
                                ? (uint)(10 + c - (byte)'A')
                                : 16u;
                    if (digit > 15u)
                        return false;
                    value = (value << 4) | digit;
                }

                hash = value;
                return true;
            }

            bool decimalOnly = true;
            uint decimalValue = 0u;
            for (int i = start; i < end; i++)
            {
                byte c = bytes[i];
                bool digit = IsDigit(c);
                decimalOnly &= digit;
                decimalValue = digit ? (decimalValue * 10u) + (uint)(c - (byte)'0') : decimalValue;
            }

            hash = decimalOnly ? decimalValue : HashAscii(bytes, start, end);
            return hash != 0u;
        }

        private static void SkipWhitespaceAndLineBreaks(NativeArray<byte> bytes, int limit, ref int index)
        {
            while (index < limit && bytes[index] <= (byte)' ')
                index++;
        }

        private static void SkipSpaces(NativeArray<byte> bytes, int limit, ref int index)
        {
            while (index < limit && (bytes[index] == (byte)' ' || bytes[index] == (byte)'\t'))
                index++;
        }

        private static void SkipLine(NativeArray<byte> bytes, int limit, ref int index)
        {
            while (index < limit && bytes[index] != (byte)'\n' && bytes[index] != (byte)'\r')
                index++;
            while (index < limit && (bytes[index] == (byte)'\n' || bytes[index] == (byte)'\r'))
                index++;
        }

        private static bool IsDigit(byte c)
        {
            return c >= (byte)'0' && c <= (byte)'9';
        }

        private static uint HashProfile(in CognitionProfileDTO profile)
        {
            uint hash = 2166136261u;
            hash = UtilityAICognitionJobMath.Fnv(hash, profile.SpeciesHash);
            hash = UtilityAICognitionJobMath.Fnv(hash, math.asuint(profile.HungerPolynomial.x));
            hash = UtilityAICognitionJobMath.Fnv(hash, math.asuint(profile.FearPolynomial.x));
            hash = UtilityAICognitionJobMath.Fnv(hash, math.asuint(profile.AggressionPolynomial.x));
            hash = UtilityAICognitionJobMath.Fnv(hash, math.asuint(profile.Weights.x));
            return hash;
        }

        private static uint HashBytes(NativeArray<byte> bytes, int length)
        {
            uint hash = 2166136261u;
            int count = math.min(length, bytes.Length);
            for (int i = 0; i < count; i++)
                hash = UtilityAICognitionJobMath.Fnv(hash, bytes[i]);
            return hash;
        }

        private static uint HashAscii(NativeArray<byte> bytes, int start, int end)
        {
            uint hash = 2166136261u;
            for (int i = start; i < end; i++)
            {
                byte c = bytes[i];
                if (c >= (byte)'A' && c <= (byte)'Z')
                    c = (byte)(c + 32);
                hash = UtilityAICognitionJobMath.Fnv(hash, c);
            }

            return hash;
        }
#endif

    }
}
