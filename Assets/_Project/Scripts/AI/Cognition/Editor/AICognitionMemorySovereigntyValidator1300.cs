#if UNITY_EDITOR
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;

namespace Hecton8.AI.Cognition.Editor
{
    [InitializeOnLoad]
    public static class AICognitionMemorySovereigntyValidator1300
    {
        private const int StressPasses = 12;
        private const int AnxietySpikeCount = 2048;
        private const uint StressSeed = 0x13001300u;
        private const uint FailureHandle = 1u << 0;
        private const uint FailureLock = 1u << 1;
        private const uint FailureSchedule = 1u << 2;
        private const uint FailureDefrag = 1u << 3;
        private const uint FailureThread = 1u << 4;
        private const uint FailureReadback = 1u << 5;
        private static int s_workerDefragGate;

        static AICognitionMemorySovereigntyValidator1300()
        {
            AICognitionLayoutGuard1300.Validate();
        }

        [MenuItem("Hecton8/AI/Run Memory Sovereignty Validator 1300")]
        public static void RunMenu()
        {
            AICognitionLayoutGuard1300.Validate();
            if (!RunDefragRaceFuzzer(out uint failureFlags))
                throw new FatalArchitectureException("1300 AI cognition memory sovereignty validator failed.");

            H8Debug.Log("[1300] AI cognition memory sovereignty validator passed.");
        }

        public static bool RunDefragRaceFuzzer(out uint failureFlags)
        {
            failureFlags = 0u;
            int stopThread = 0;
            int workerIterations = 0;
            int workerFaulted = 0;
            Volatile.Write(ref s_workerDefragGate, 0);

            using GlobalDataVault vault = GlobalDataVault.Create(512, 64L * 1024L * 1024L);
            if (!UtilityAICognitionVault.TryAcquireHandles(vault, out UtilityAICognitionVaultHandles cognitionHandles) ||
                !UtilityAICognitionVault.TryAcquireAnxietyHandles(vault, out UtilityAIAnxietyVaultHandles anxietyHandles))
            {
                failureFlags |= FailureHandle;
                return false;
            }

            Thread worker = new Thread(
                () =>
                {
                    try
                    {
                        while (Volatile.Read(ref stopThread) == 0)
                        {
                            vault.TryGetBufferGeneration(UtilityAICognitionVaultBufferIds.States, out uint _);
                            vault.TryGetBufferGeneration(UtilityAICognitionVaultBufferIds.Aups, out uint _);
                            vault.TryGetBufferGeneration(UtilityAICognitionVaultBufferIds.TelemetryRing, out uint _);
                            vault.TryGetBufferGeneration(UtilityAIAnxietyVaultBufferIds.TelemetryRing, out uint _);
                            _ = vault.IsCompactionFenceActive;
                            _ = vault.ActiveBurstLockMask;
                            if (Volatile.Read(ref s_workerDefragGate) != 0)
                            {
                                vault.RequestEditorForceDefragmentation();
                                vault.FrostTickDefrag(1f / 120f, 0.125f, MemoryDefragPhase.PreSimulation, vault.ActiveBurstLockMask);
                            }

                            Interlocked.Increment(ref workerIterations);
                            Thread.Yield();
                        }
                    }
                    catch
                    {
                        Volatile.Write(ref workerFaulted, 1);
                    }
                });
            worker.IsBackground = true;
            worker.Name = "H8_1300_AICognitionVaultFuzzer";
            worker.Start();

            try
            {
                for (int pass = 0; pass < StressPasses; pass++)
                {
                    uint frame = (uint)(pass + 1);
                    if (!RunLockedCognitionPass(vault, in cognitionHandles, in anxietyHandles, frame, out uint passFailure))
                    {
                        failureFlags |= passFailure;
                        return false;
                    }

                    vault.RequestEditorForceDefragmentation();
                    vault.FrostTickDefrag(1f / 60f, pass * (1f / StressPasses), MemoryDefragPhase.PreSimulation, vault.ActiveBurstLockMask);
                    if (!TryValidateReadback(vault, in cognitionHandles, in anxietyHandles, out uint readbackFailure))
                    {
                        failureFlags |= readbackFailure;
                        return false;
                    }
                }

                bool relocated = vault.GenerateMockVaultRelocationForValidation(
                    StressSeed,
                    UtilityAICognitionConstants.MaxCreatures,
                    MemoryDefragPhase.PreSimulation,
                    vault.ActiveBurstLockMask);
                if (!relocated)
                {
                    failureFlags |= FailureDefrag;
                    return false;
                }

                if (!UtilityAICognitionVault.TryAcquireHandles(vault, out cognitionHandles) ||
                    !UtilityAICognitionVault.TryAcquireAnxietyHandles(vault, out anxietyHandles) ||
                    !TryValidateReadback(vault, in cognitionHandles, in anxietyHandles, out uint relocationReadbackFailure))
                {
                    failureFlags |= relocationReadbackFailure == 0u ? FailureReadback : relocationReadbackFailure;
                    return false;
                }
            }
            finally
            {
                Volatile.Write(ref s_workerDefragGate, 0);
                Volatile.Write(ref stopThread, 1);
                if (!worker.Join(1000))
                    failureFlags |= FailureThread;
            }

            if (Volatile.Read(ref workerFaulted) != 0 || workerIterations <= 0)
                failureFlags |= FailureThread;

            return failureFlags == 0u;
        }

        private static bool RunLockedCognitionPass(
            GlobalDataVault vault,
            in UtilityAICognitionVaultHandles cognitionHandles,
            in UtilityAIAnxietyVaultHandles anxietyHandles,
            uint frame,
            out uint failureFlags)
        {
            failureFlags = 0u;
            UtilityAICognitionVaultBuffers cognitionBuffers = default;
            UtilityAIAnxietyVaultBuffers anxietyBuffers = default;
            bool statesLocked = false;
            bool aupsLocked = false;
            bool targetsLocked = false;
            bool targetNextLocked = false;
            bool bucketHeadsLocked = false;
            bool tuningLocked = false;
            bool outputsLocked = false;
            bool cognitionTelemetryLocked = false;
            bool cognitionTelemetryCursorLocked = false;
            bool profilesLocked = false;
            bool cognitionCsvScratchLocked = false;
            bool anxietyProfilesLocked = false;
            bool anxietyTuningLocked = false;
            bool anxietyScratchLocked = false;
            bool anxietyTelemetryLocked = false;
            bool anxietyTelemetryCursorLocked = false;
            bool shelterSdfLocked = false;
            bool shelterHeaderLocked = false;
            bool anxietyCsvScratchLocked = false;
            bool scheduledHandleActive = false;
            JobHandle scheduledHandle = default;

            try
            {
                if (!vault.TryAcquireWriteLock(in cognitionHandles.States, SystemID.AICognition, out cognitionBuffers.States))
                {
                    failureFlags |= FailureLock;
                    return false;
                }

                statesLocked = true;
                if (!vault.TryAcquireWriteLock(in cognitionHandles.Aups, SystemID.AICognition, out cognitionBuffers.Aups))
                {
                    failureFlags |= FailureLock;
                    return false;
                }

                aupsLocked = true;
                if (!vault.TryAcquireWriteLock(in cognitionHandles.Targets, SystemID.AICognition, out cognitionBuffers.Targets))
                {
                    failureFlags |= FailureLock;
                    return false;
                }

                targetsLocked = true;
                if (!vault.TryAcquireWriteLock(in cognitionHandles.TargetNext, SystemID.AICognition, out cognitionBuffers.TargetNext))
                {
                    failureFlags |= FailureLock;
                    return false;
                }

                targetNextLocked = true;
                if (!vault.TryAcquireWriteLock(in cognitionHandles.BucketHeads, SystemID.AICognition, out cognitionBuffers.BucketHeads))
                {
                    failureFlags |= FailureLock;
                    return false;
                }

                bucketHeadsLocked = true;
                if (!vault.TryAcquireWriteLock(in cognitionHandles.Tuning, SystemID.AICognition, out cognitionBuffers.Tuning))
                {
                    failureFlags |= FailureLock;
                    return false;
                }

                tuningLocked = true;
                if (!vault.TryAcquireWriteLock(in cognitionHandles.Outputs, SystemID.AICognition, out cognitionBuffers.Outputs))
                {
                    failureFlags |= FailureLock;
                    return false;
                }

                outputsLocked = true;
                if (!vault.TryAcquireWriteLock(in cognitionHandles.TelemetryRing, SystemID.AICognition, out cognitionBuffers.TelemetryRing))
                {
                    failureFlags |= FailureLock;
                    return false;
                }

                cognitionTelemetryLocked = true;
                if (!vault.TryAcquireWriteLock(in cognitionHandles.TelemetryCursor, SystemID.AICognition, out cognitionBuffers.TelemetryCursor))
                {
                    failureFlags |= FailureLock;
                    return false;
                }

                cognitionTelemetryCursorLocked = true;
                if (!vault.TryAcquireWriteLock(in cognitionHandles.Profiles, SystemID.AICognition, out cognitionBuffers.Profiles))
                {
                    failureFlags |= FailureLock;
                    return false;
                }

                profilesLocked = true;
                if (!vault.TryAcquireWriteLock(in cognitionHandles.CsvScratch, SystemID.AICognition, out cognitionBuffers.CsvScratch))
                {
                    failureFlags |= FailureLock;
                    return false;
                }

                cognitionCsvScratchLocked = true;
                if (!vault.TryAcquireWriteLock(in anxietyHandles.Profiles, SystemID.AICognition, out anxietyBuffers.Profiles))
                {
                    failureFlags |= FailureLock;
                    return false;
                }

                anxietyProfilesLocked = true;
                if (!vault.TryAcquireWriteLock(in anxietyHandles.Tuning, SystemID.AICognition, out anxietyBuffers.Tuning))
                {
                    failureFlags |= FailureLock;
                    return false;
                }

                anxietyTuningLocked = true;
                if (!vault.TryAcquireWriteLock(in anxietyHandles.Scratch, SystemID.AICognition, out anxietyBuffers.Scratch))
                {
                    failureFlags |= FailureLock;
                    return false;
                }

                anxietyScratchLocked = true;
                if (!vault.TryAcquireWriteLock(in anxietyHandles.TelemetryRing, SystemID.AICognition, out anxietyBuffers.TelemetryRing))
                {
                    failureFlags |= FailureLock;
                    return false;
                }

                anxietyTelemetryLocked = true;
                if (!vault.TryAcquireWriteLock(in anxietyHandles.TelemetryCursor, SystemID.AICognition, out anxietyBuffers.TelemetryCursor))
                {
                    failureFlags |= FailureLock;
                    return false;
                }

                anxietyTelemetryCursorLocked = true;
                if (!vault.TryAcquireWriteLock(in anxietyHandles.ShelterSdf, SystemID.AICognition, out anxietyBuffers.ShelterSdf))
                {
                    failureFlags |= FailureLock;
                    return false;
                }

                shelterSdfLocked = true;
                if (!vault.TryAcquireWriteLock(in anxietyHandles.ShelterHeader, SystemID.AICognition, out anxietyBuffers.ShelterHeader))
                {
                    failureFlags |= FailureLock;
                    return false;
                }

                shelterHeaderLocked = true;
                if (!vault.TryAcquireWriteLock(in anxietyHandles.CsvScratch, SystemID.AICognition, out anxietyBuffers.CsvScratch))
                {
                    failureFlags |= FailureLock;
                    return false;
                }

                anxietyCsvScratchLocked = true;

                if (!cognitionBuffers.IsCreated() || !anxietyBuffers.IsCreated())
                {
                    failureFlags |= FailureHandle;
                    return false;
                }

                Volatile.Write(ref s_workerDefragGate, 1);
                bool mockDataScheduled = UtilityAICognitionVault.TryScheduleMockData(in cognitionBuffers, frame, default, out scheduledHandle);
                scheduledHandleActive = true;
                if (!mockDataScheduled)
                {
                    failureFlags |= FailureSchedule;
                    return false;
                }

                if (!UtilityAICognitionVault.TryScheduleMockAnxietyEnvironment(in cognitionBuffers, in anxietyBuffers, frame, AnxietySpikeCount, scheduledHandle, out scheduledHandle))
                {
                    failureFlags |= FailureSchedule;
                    return false;
                }

                vault.RequestEditorForceDefragmentation();
                vault.FrostTickDefrag(1f / 60f, 0.25f, MemoryDefragPhase.PreSimulation, vault.ActiveBurstLockMask);

                NativeArray<CognitionMovementAcousticSignalDTO>.ReadOnly movementSignals = default;
                NativeArray<CognitionCombatDamageSignalDTO>.ReadOnly damageSignals = default;
                if (!UtilityAICognitionVault.TryScheduleCognitionPass(
                        in cognitionBuffers,
                        frame,
                        1f / 30f,
                        0f,
                        movementSignals,
                        0,
                        damageSignals,
                        0,
                        scheduledHandle,
                        out scheduledHandle))
                {
                    failureFlags |= FailureSchedule;
                    return false;
                }

                if (!UtilityAICognitionVault.TryScheduleAnxietyFrostTick(
                        in cognitionBuffers,
                        in anxietyBuffers,
                        frame,
                        1f / 30f,
                        0f,
                        scheduledHandle,
                        out scheduledHandle))
                {
                    failureFlags |= FailureSchedule;
                    return false;
                }

                vault.RequestEditorForceDefragmentation();
                vault.FrostTickDefrag(1f / 60f, 0.5f, MemoryDefragPhase.PreSimulation, vault.ActiveBurstLockMask);
                scheduledHandle.Complete();
                scheduledHandleActive = false;
                Volatile.Write(ref s_workerDefragGate, 0);

                if (cognitionBuffers.TelemetryRing.Length < UtilityAICognitionConstants.TelemetryFrames ||
                    anxietyBuffers.TelemetryRing.Length < AnxietyDecayConstants.TelemetryFrames ||
                    !math.isfinite(cognitionBuffers.Outputs[0].MaxUtility) ||
                    !math.isfinite(anxietyBuffers.Scratch[0].Fear01))
                {
                    failureFlags |= FailureReadback;
                    return false;
                }

                return true;
            }
            finally
            {
                Volatile.Write(ref s_workerDefragGate, 0);
                if (scheduledHandleActive)
                    scheduledHandle.Complete();
                if (anxietyCsvScratchLocked)
                    vault.ReleaseWriteLock(in anxietyHandles.CsvScratch, SystemID.AICognition);
                if (shelterHeaderLocked)
                    vault.ReleaseWriteLock(in anxietyHandles.ShelterHeader, SystemID.AICognition);
                if (shelterSdfLocked)
                    vault.ReleaseWriteLock(in anxietyHandles.ShelterSdf, SystemID.AICognition);
                if (anxietyTelemetryCursorLocked)
                    vault.ReleaseWriteLock(in anxietyHandles.TelemetryCursor, SystemID.AICognition);
                if (anxietyTelemetryLocked)
                    vault.ReleaseWriteLock(in anxietyHandles.TelemetryRing, SystemID.AICognition);
                if (anxietyScratchLocked)
                    vault.ReleaseWriteLock(in anxietyHandles.Scratch, SystemID.AICognition);
                if (anxietyTuningLocked)
                    vault.ReleaseWriteLock(in anxietyHandles.Tuning, SystemID.AICognition);
                if (anxietyProfilesLocked)
                    vault.ReleaseWriteLock(in anxietyHandles.Profiles, SystemID.AICognition);
                if (cognitionCsvScratchLocked)
                    vault.ReleaseWriteLock(in cognitionHandles.CsvScratch, SystemID.AICognition);
                if (profilesLocked)
                    vault.ReleaseWriteLock(in cognitionHandles.Profiles, SystemID.AICognition);
                if (cognitionTelemetryCursorLocked)
                    vault.ReleaseWriteLock(in cognitionHandles.TelemetryCursor, SystemID.AICognition);
                if (cognitionTelemetryLocked)
                    vault.ReleaseWriteLock(in cognitionHandles.TelemetryRing, SystemID.AICognition);
                if (outputsLocked)
                    vault.ReleaseWriteLock(in cognitionHandles.Outputs, SystemID.AICognition);
                if (tuningLocked)
                    vault.ReleaseWriteLock(in cognitionHandles.Tuning, SystemID.AICognition);
                if (bucketHeadsLocked)
                    vault.ReleaseWriteLock(in cognitionHandles.BucketHeads, SystemID.AICognition);
                if (targetNextLocked)
                    vault.ReleaseWriteLock(in cognitionHandles.TargetNext, SystemID.AICognition);
                if (targetsLocked)
                    vault.ReleaseWriteLock(in cognitionHandles.Targets, SystemID.AICognition);
                if (aupsLocked)
                    vault.ReleaseWriteLock(in cognitionHandles.Aups, SystemID.AICognition);
                if (statesLocked)
                    vault.ReleaseWriteLock(in cognitionHandles.States, SystemID.AICognition);
            }
        }

        private static bool TryValidateReadback(
            GlobalDataVault vault,
            in UtilityAICognitionVaultHandles cognitionHandles,
            in UtilityAIAnxietyVaultHandles anxietyHandles,
            out uint failureFlags)
        {
            failureFlags = 0u;
            if (!vault.TryReadOnlyHandle(in cognitionHandles.States, out NativeArray<CognitionStateDTO>.ReadOnly states) ||
                !vault.TryReadOnlyHandle(in cognitionHandles.Aups, out NativeArray<CognitionAupDTO>.ReadOnly aups) ||
                !vault.TryReadOnlyHandle(in cognitionHandles.TelemetryRing, out NativeArray<CognitionTelemetryEntry>.ReadOnly cognitionTelemetry) ||
                !vault.TryReadOnlyHandle(in anxietyHandles.TelemetryRing, out NativeArray<AnxietyTelemetryEntry>.ReadOnly anxietyTelemetry) ||
                states.Length < UtilityAICognitionConstants.MaxCreatures ||
                aups.Length < UtilityAICognitionConstants.MaxCreatures ||
                cognitionTelemetry.Length < UtilityAICognitionConstants.TelemetryFrames ||
                anxietyTelemetry.Length < AnxietyDecayConstants.TelemetryFrames)
            {
                failureFlags |= FailureReadback;
                return false;
            }

            CognitionAupDTO sampleAup = aups[0];
            CognitionTelemetryEntry cognitionEntry = cognitionTelemetry[0];
            AnxietyTelemetryEntry anxietyEntry = anxietyTelemetry[0];
            if (!math.isfinite(sampleAup.AUP.x) ||
                !math.isfinite(sampleAup.AUP.y) ||
                !math.isfinite(sampleAup.AUP.z) ||
                !math.isfinite(cognitionEntry.AverageFear01) ||
                !math.isfinite(anxietyEntry.AverageFear01))
            {
                failureFlags |= FailureReadback;
                return false;
            }

            return true;
        }
    }
}
#endif
