using System;
using System.IO;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Core.Scheduling;
using Hecton8.World;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class JobAdmissionServiceEditTests
    {
        private const uint WorldJobHash = 0xA1100001u;
        private const uint VfxJobHash = 0x46584656u;
        private const int AdmissionAttempts = 128;

        [Test]
        public void AupBarrier_DeniesNonCriticalJobsWithoutGc()
        {
            using (GlobalDataVault vault = GlobalDataVault.Create(64, GlobalDataVault.MinimumQualityArenaLimitBytes))
            using (BurstTokenBucketJobAdmissionService service = new BurstTokenBucketJobAdmissionService())
            {
                CapturingJobAdmissionTelemetrySink sink = new CapturingJobAdmissionTelemetrySink();
                service.Initialize(sink, vault);
                service.Refill(1f, 1f / 60f, previousFrameMissedBudget: false);
                service.SetAupBarrierActive(true);

                Assert.IsFalse(service.TryAdmitJob(JobAdmissionLane.Lane1_World, WorldJobHash, out _));
                sink.Reset();

                long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
                bool allDenied = true;
                for (int i = 0; i < AdmissionAttempts; i++)
                    allDenied &= !service.TryAdmitJob(JobAdmissionLane.Lane1_World, WorldJobHash, out _);

                long afterBytes = GC.GetAllocatedBytesForCurrentThread();

                Assert.IsTrue(allDenied);
                Assert.AreEqual(AdmissionAttempts, sink.DeniedCount);
                Assert.AreNotEqual(0, sink.LastDeniedReasonFlags & JobAdmissionTelemetryFlags.Denied);
                Assert.AreNotEqual(0, sink.LastDeniedReasonFlags & JobAdmissionTelemetryFlags.AupBarrier);
                Assert.AreEqual(0L, afterBytes - beforeBytes);
            }
        }

        [Test]
        public void GlobalQualityWeight_ContinuouslyScalesLaneBudgets()
        {
            float lowBudget = ResolveLane1BudgetAfterRefill(globalQualityWeight01: 0f);
            float highBudget = ResolveLane1BudgetAfterRefill(globalQualityWeight01: 1f);

            Assert.Greater(highBudget, lowBudget);
            Assert.GreaterOrEqual(lowBudget, 0f);
            Assert.Greater(highBudget, 0f);
        }

        [Test]
        public void VfxLane_DeniesWhenBudgetIsExhausted()
        {
            using (GlobalDataVault vault = GlobalDataVault.Create(64, GlobalDataVault.MinimumQualityArenaLimitBytes))
            using (BurstTokenBucketJobAdmissionService service = new BurstTokenBucketJobAdmissionService())
            {
                CapturingJobAdmissionTelemetrySink sink = new CapturingJobAdmissionTelemetrySink();
                service.Initialize(sink, vault);
                service.Refill(1f, 1f / 60f, previousFrameMissedBudget: false);

                bool denied = false;
                for (int i = 0; i < AdmissionAttempts; i++)
                {
                    if (!service.TryAdmitJob(JobAdmissionLane.Lane4_VFX, VfxJobHash, out _))
                    {
                        denied = true;
                        break;
                    }
                }

                Assert.IsTrue(denied);
                Assert.AreEqual(1, sink.DeniedCount);
                Assert.AreNotEqual(0, sink.LastDeniedReasonFlags & JobAdmissionTelemetryFlags.Denied);
                Assert.AreNotEqual(0, sink.LastDeniedReasonFlags & JobAdmissionTelemetryFlags.InsufficientBudget);
            }
        }

        [Test]
        public void JobAdmissionTelemetryFlags_DefinesStableByteReasonBits()
        {
            Assert.AreEqual((byte)(1 << 0), JobAdmissionTelemetryFlags.Admitted);
            Assert.AreEqual((byte)(1 << 1), JobAdmissionTelemetryFlags.Denied);
            Assert.AreEqual((byte)(1 << 2), JobAdmissionTelemetryFlags.AupBarrier);
            Assert.AreEqual((byte)(1 << 3), JobAdmissionTelemetryFlags.KillSwitch);
            Assert.AreEqual((byte)(1 << 4), JobAdmissionTelemetryFlags.InsufficientBudget);
            Assert.AreEqual((byte)(1 << 5), JobAdmissionTelemetryFlags.NonFinite);
        }

        [Test]
        public void BlackboxFlags_SourceDeclaresReasonBitsAndHashesFlags()
        {
            string source = File.ReadAllText(JobAdmissionServicePath());

            Assert.That(source, Does.Contain("JobAdmissionTelemetryFlags.Admitted"));
            Assert.That(source, Does.Contain("JobAdmissionTelemetryFlags.Denied"));
            Assert.That(source, Does.Contain("JobAdmissionTelemetryFlags.AupBarrier"));
            Assert.That(source, Does.Contain("JobAdmissionTelemetryFlags.KillSwitch"));
            Assert.That(source, Does.Contain("JobAdmissionTelemetryFlags.InsufficientBudget"));
            Assert.That(source, Does.Contain("JobAdmissionTelemetryFlags.NonFinite"));
            Assert.That(source, Does.Contain("ComputeBlackboxHash(uint jobHash, float estimatedCostMs, float remainingBudgetMs, byte flags)"));
            Assert.That(source, Does.Contain("hash = (hash ^ (uint)flags) * 16777619u;"));
            Assert.That(source, Does.Contain("entry.Flags = flags;"));
            Assert.That(source, Does.Contain("entry.StateHash = ComputeBlackboxHash(jobHash, entry.EstimatedCostMs, entry.RemainingBudgetMs, flags);"));
            Assert.That(source, Does.Contain("private const uint AdmissionBlackboxDumpVersion = 2u;"));
        }

        [Test]
        public void BlackboxDumpLayout_SourceKeepsFlagsByteInFixedSixtyFourByteEntry()
        {
            string source = File.ReadAllText(JobAdmissionServicePath());

            Assert.That(source, Does.Contain("private const int BlackboxEntrySizeBytes = 64;"));
            Assert.That(source, Does.Contain("[StructLayout(LayoutKind.Explicit, Size = BlackboxEntrySizeBytes)]"));
            Assert.That(source, Does.Contain("[FieldOffset(24)]"));
            Assert.That(source, Does.Contain("public byte Lane;"));
            Assert.That(source, Does.Contain("[FieldOffset(25)]"));
            Assert.That(source, Does.Contain("public byte Flags;"));
            Assert.That(source, Does.Contain("[FieldOffset(26)]"));
            Assert.That(source, Does.Contain("public ushort Reserved;"));
            Assert.That(source, Does.Contain("destination[cursor++] = entry.Flags;"));
            Assert.That(source, Does.Contain("while (cursor - entryStart < BlackboxEntrySizeBytes)"));
            Assert.That(source, Does.Not.Contain("public bool Flags"));
        }

        [Test]
        public void JobAdmissionTelemetryBridge_SourceForwardsReasonFlagsToCpuSignalAndCrashTelemetry()
        {
            string source = File.ReadAllText(JobAdmissionTelemetryBridgePath());

            Assert.That(source, Does.Contain("byte safeFlags = (byte)(reasonFlags | JobAdmissionTelemetryFlags.Denied);"));
            Assert.That(source, Does.Contain("Flags = safeFlags"));
            Assert.That(source, Does.Contain("signal.Flags);"));
            Assert.That(source, Does.Contain("JobAdmissionTelemetryFlags.NonFinite"));
            Assert.That(source, Does.Not.Contain("StarvedFlag"));
            Assert.That(source, Does.Not.Contain("NonFiniteFlag ="));
        }

        [Test]
        public void CrashTelemetryBuffer_SourcePacksJobAdmissionReasonFlags()
        {
            string source = File.ReadAllText(CrashTelemetryBufferPath());

            Assert.That(source, Does.Contain("return debt | ((uint)flags << 24);"));
            Assert.That(source, Does.Contain("byte nonFiniteFlags = (byte)(JobAdmissionTelemetryFlags.Denied | JobAdmissionTelemetryFlags.NonFinite);"));
            Assert.That(source, Does.Contain("WriteJobAdmissionTelemetry(lane, jobHash, 0f, value, 0, nonFiniteFlags);"));
            Assert.That(source, Does.Contain("using Hecton8.Core.Contracts;"));
        }

        [Test]
        public void WorldChunkResidency_SourceUsesSharedAdmissionHashAndBatchProfile()
        {
            string source = File.ReadAllText(WorldChunkResidencyManagerPath());

            Assert.That(source, Does.Contain("using Hecton8.Core.Scheduling;"));
            Assert.That(source, Does.Contain("uint jobHash = JobAdmissionHash<TJob>.Value;"));
            Assert.That(source, Does.Contain("int safeBatchCount = JobAdmissionScheduleExtensions.ResolveProfiledInnerloopBatchCount(jobHash, arrayLength, innerloopBatchCount);"));
            Assert.That(source, Does.Contain("handle = jobData.Schedule(arrayLength, safeBatchCount, dependsOn);"));
            Assert.That(source, Does.Not.Contain("WorldJobAdmissionHash"));
            Assert.That(source, Does.Not.Contain("ComputeJobAdmissionHash"));
            Assert.That(source, Does.Not.Contain("private static int ResolveInnerloopBatchCount"));
        }

        [Test]
        public void JobAdmissionScheduleExtensions_SourceOwnsProfiledBatchResolution()
        {
            string source = File.ReadAllText(JobAdmissionScheduleExtensionsPath());

            Assert.That(source, Does.Contain("int safeBatchCount = ResolveProfiledInnerloopBatchCount(jobHash, arrayLength, innerloopBatchCount);"));
            Assert.That(source, Does.Contain("public static int ResolveProfiledInnerloopBatchCount(uint jobHash, int elementCount, int innerloopBatchCount)"));
            Assert.That(source, Does.Contain("int maxBatch = innerloopBatchCount > 0 ? ResolveDefaultMaxBatch(innerloopBatchCount) : 4;"));
            Assert.That(source, Does.Contain("JobSchedulingProfileCatalog.TryResolveBatchBounds(jobHash"));
            Assert.That(source, Does.Contain("return ResolveInnerloopBatchCount(elementCount, minBatch, maxBatch);"));
            Assert.That(source, Does.Contain("private static int ResolveDefaultMaxBatch(int innerloopBatchCount)"));
            Assert.That(source, Does.Contain("innerloopBatchCount > int.MaxValue / 4"));
        }

        [Test]
        public void ProfiledBatchResolver_DefaultsAreBoundedWithoutProfile()
        {
            Assert.AreEqual(128, JobAdmissionScheduleExtensions.ResolveProfiledInnerloopBatchCount(0u, 1000, 32));
            Assert.AreEqual(4, JobAdmissionScheduleExtensions.ResolveProfiledInnerloopBatchCount(0u, 100, 0));
            Assert.Greater(JobAdmissionScheduleExtensions.ResolveProfiledInnerloopBatchCount(0u, 100, int.MaxValue), 0);
        }

        [Test]
        public void SystemDispatcher_SourceSyncsJobAdmissionSchedulerBridgeOnRefreshAndRebound()
        {
            string source = File.ReadAllText(SystemDispatcherPath());

            Assert.That(source, Does.Contain("private void RefreshJobAdmissionDependency()"));
            Assert.That(source, Does.Contain("case GlobalRegistryServiceSlot.JobAdmissionRuntime:"));
            Assert.That(source, Does.Contain("IJobAdmissionService previousAdmission = JobAdmissionSchedulerBridge.Service;"));
            Assert.That(source, Does.Contain("JobAdmissionSchedulerBridge.SetService(jobAdmission);"));
            Assert.That(source, Does.Contain("JobAdmissionSchedulerBridge.SetService(_jobAdmission);"));
            Assert.That(source, Does.Contain("_jobAdmission = null;"));
            Assert.That(source, Does.Contain("JobAdmissionSchedulerBridge.ClearService(previousAdmission);"));
        }

        [Test]
        public void SystemDispatcher_SourceReloadsJobSchedulingProfilesOnColdBootAndDataVaultRebound()
        {
            string source = File.ReadAllText(SystemDispatcherPath());

            Assert.GreaterOrEqual(CountOccurrences(source, "JobSchedulingProfileCatalog.LoadColdBootProfiles(_dataVault);"), 2);
            Assert.That(source, Does.Contain("case GlobalRegistryServiceSlot.DataVault:"));
            Assert.That(source, Does.Contain("VaultSovereigntyTelemetry.EnsureRing(_dataVault);"));
            Assert.That(source, Does.Contain("JobSchedulingProfileCatalog.LoadColdBootProfiles(_dataVault);"));
        }

        [Test]
        public void JobSchedulingProfiles_SourceKeepsWorldResidencyProfileOnSharedHashName()
        {
            string csv = File.ReadAllText(JobSchedulingProfilesPath());
            string typeName = typeof(RadiusBasedStreamingJob).FullName;

            Assert.AreEqual("Hecton8.World.RadiusBasedStreamingJob", typeName);
            Assert.That(csv, Does.Contain(typeName + ",64,256"));
            Assert.That(csv, Does.Not.Contain("WorldChunkResidencyManager+RadiusBasedStreamingJob"));
        }

        [Test]
        public void JobSchedulingProfiles_SourceKeepsFaunaSteeringParallelProfilesOnNestedNames()
        {
            string csv = File.ReadAllText(JobSchedulingProfilesPath());
            string steeringSource = File.ReadAllText(PredatorCognitionDomainSteeringPath());

            Assert.That(steeringSource, Does.Contain("private unsafe struct GenerateMockSdfObstaclesJob : IJobParallelFor"));
            Assert.That(steeringSource, Does.Contain("private unsafe struct PopulateLeviathanSteeringParamsJob : IJobParallelFor"));
            Assert.That(steeringSource, Does.Contain("private unsafe struct EvaluateSdfAvoidanceJob : IJobParallelFor"));
            Assert.That(steeringSource, Does.Contain("private unsafe struct IntegrateSteeringVectorsJob : IJobParallelFor"));
            Assert.That(steeringSource, Does.Contain("private unsafe struct RecordSteeringTelemetryJob : IJob"));

            Assert.That(csv, Does.Contain("Hecton8.AI.PredatorCognitionDomain+GenerateMockSdfObstaclesJob,128,512"));
            Assert.That(csv, Does.Contain("Hecton8.AI.PredatorCognitionDomain+PopulateLeviathanSteeringParamsJob,32,128"));
            Assert.That(csv, Does.Contain("Hecton8.AI.PredatorCognitionDomain+EvaluateSdfAvoidanceJob,32,128"));
            Assert.That(csv, Does.Contain("Hecton8.AI.PredatorCognitionDomain+IntegrateSteeringVectorsJob,32,128"));
            Assert.That(csv, Does.Not.Contain("RecordSteeringTelemetryJob"));
        }

        [Test]
        public void JobSchedulingProfileCatalog_SourceSkipsUtf8BomBeforeCommentHeader()
        {
            string source = File.ReadAllText(JobSchedulingProfileCatalogPath());

            Assert.That(source, Does.Contain("c == 0xEF && csvBytes[1] == 0xBB && csvBytes[2] == 0xBF"));
            Assert.That(source, Does.Contain("i += 2;"));
        }

        [Test]
        public void TelemetryDumpValidator_SourceDecodesJobAdmissionBlackboxAsDedicatedLayout()
        {
            string source = File.ReadAllText(TelemetryDumpValidatorWindowPath());

            Assert.That(source, Does.Contain("private const ulong JobAdmissionDumpMagic = 0x00384E4F54434548UL;"));
            Assert.That(source, Does.Contain("private const int JobAdmissionDumpHeaderBytes = 32;"));
            Assert.That(source, Does.Contain("private const int JobAdmissionDumpEntrySizeBytes = 64;"));
            Assert.That(source, Does.Contain("if (TryParseJobAdmissionDump(path, bytes, span))"));
            Assert.That(source, Does.Contain("private static bool IsJobAdmissionDumpPath(string path)"));
            Assert.That(source, Does.Contain("fileName.IndexOf(\"JobAdmission\", StringComparison.OrdinalIgnoreCase)"));
            Assert.That(source, Does.Contain("ReadU64(span, 0) != JobAdmissionDumpMagic"));
            Assert.That(source, Does.Contain("entrySize == JobAdmissionDumpEntrySizeBytes"));
            Assert.That(source, Does.Contain("reserved == 0u"));
            Assert.That(source, Does.Contain("JobAdmissionDumpHeaderBytes + (long)entryCount * entrySize <= span.Length"));
            Assert.That(source, Does.Contain("BuildInvalidJobAdmissionHeaderSummary("));
            Assert.That(source, Does.Contain("SetSummary(BuildInvalidJobAdmissionHeaderSummary(path, span.Length, 0u, 0, 0, 0, 0u, 0u));"));
            Assert.That(source, Does.Contain("invalid job-admission blackbox header"));
            Assert.That(source, Does.Contain("builder.Append(\" | reserved=0x\")"));
            Assert.That(source, Does.Contain("ComputeXxHash64(bytes, JobAdmissionDumpHeaderBytes, payloadBytes)"));
            Assert.That(source, Does.Contain("layout=job-admission-blackbox"));
            Assert.GreaterOrEqual(CountOccurrences(source, "math.min(nonEmptyEntryCount, MaxDisplayedFrames).ToString(CultureInfo.InvariantCulture)"), 2);
            Assert.That(source, Does.Contain("ResolveJobAdmissionFlagsLabel(version, flags)"));
            Assert.That(source, Does.Contain("\"legacy-starved\""));
            Assert.That(source, Does.Contain("\"budget\""));
            Assert.That(source, Does.Contain("ComputeJobAdmissionStateHash("));
            Assert.That(source, Does.Contain("hashOk="));
        }

        [Test]
        public void TelemetryDumpValidator_SourceDecodesSimulationBucketBlackboxAsDedicatedLayout()
        {
            string source = File.ReadAllText(TelemetryDumpValidatorWindowPath());

            Assert.That(source, Does.Contain("private const ulong SimulationBucketDumpMagic = 0x00384E4F54434548UL;"));
            Assert.That(source, Does.Contain("private const int SimulationBucketDumpHeaderBytes = 32;"));
            Assert.That(source, Does.Contain("private const int SimulationBucketDumpEntrySizeBytes = 64;"));
            Assert.That(source, Does.Contain("if (TryParseSimulationBucketDump(path, bytes, span))"));
            Assert.That(source, Does.Contain("private static bool IsSimulationBucketDumpPath(string path)"));
            Assert.That(source, Does.Contain("\"Dump_SIMULATION_BUCKET_DISTRIBUTOR.bin\""));
            Assert.That(source, Does.Contain("layout=simulation-bucket-blackbox"));
            Assert.That(source, Does.Contain("BuildInvalidSimulationBucketHeaderSummary("));
            Assert.That(source, Does.Contain("SetSummary(BuildInvalidSimulationBucketHeaderSummary(path, span.Length, 0u, 0, 0, 0, 0, 0u));"));
            Assert.That(source, Does.Contain("BuildSimulationBucketEntryLine("));
            Assert.That(source, Does.Contain("ComputeXxHash64(bytes, SimulationBucketDumpHeaderBytes, payloadBytes)"));
            Assert.That(source, Does.Contain("builder.Append(\" activeSlow=\")"));
            Assert.That(source, Does.Contain("ResolveSimulationBucketFlagsLabel(pacingFlags)"));
            Assert.That(source, Does.Contain("\"pre-sim-over-budget\""));
            Assert.That(source, Does.Contain("\"homeostasis-kill\""));
            Assert.That(source, Does.Contain("\"unknown=0x\""));
            Assert.That(source, Does.Contain("builder.Append(\" state=0x\")"));
        }

        [Test]
        public void TelemetryDumpValidator_SourceDecodesCrashTelemetryBufferAsDedicatedLayout()
        {
            string source = File.ReadAllText(TelemetryDumpValidatorWindowPath());

            Assert.That(source, Does.Contain("private const ulong CrashTelemetryDumpMagic = 0x00384E4F54434548UL;"));
            Assert.That(source, Does.Contain("private const int CrashTelemetryDumpHeaderBytes = 16;"));
            Assert.That(source, Does.Contain("private const int CrashTelemetryDumpEntrySizeBytes = 64;"));
            Assert.That(source, Does.Contain("if (TryParseCrashTelemetryDump(path, bytes, span))"));
            Assert.That(source, Does.Contain("private static bool IsCrashTelemetryDumpPath(string path)"));
            Assert.That(source, Does.Contain("\"Dump_CRASH_TELEMETRY_BUFFER.bin\""));
            Assert.That(source, Does.Contain("\"BLACKBOX_CRASH.bin\""));
            Assert.That(source, Does.Contain("layout=crash-telemetry-buffer"));
            Assert.That(source, Does.Contain("BuildInvalidCrashTelemetryHeaderSummary("));
            Assert.That(source, Does.Contain("SetSummary(BuildInvalidCrashTelemetryHeaderSummary(path, span.Length, 0u, 0u));"));
            Assert.That(source, Does.Contain("BuildCrashTelemetryEntryLine("));
            Assert.That(source, Does.Contain("ComputeXxHash64(bytes, CrashTelemetryDumpHeaderBytes, payloadBytes)"));
            Assert.That(source, Does.Contain("builder.Append(\" errors=0x\")"));
            Assert.That(source, Does.Contain("builder.Append(\" reason=0x\")"));
            Assert.That(source, Does.Contain("builder.Append(\" spike=\")"));
            Assert.That(source, Does.Contain("builder.Append(\" memFault=\")"));
        }

        private static float ResolveLane1BudgetAfterRefill(float globalQualityWeight01)
        {
            using (GlobalDataVault vault = GlobalDataVault.Create(64, GlobalDataVault.MinimumQualityArenaLimitBytes))
            using (BurstTokenBucketJobAdmissionService service = new BurstTokenBucketJobAdmissionService())
            {
                service.Initialize(new CapturingJobAdmissionTelemetrySink(), vault);
                service.Refill(globalQualityWeight01, 1f / 60f, previousFrameMissedBudget: false);
                return service.GetLaneBudgetMs(JobAdmissionLane.Lane1_World);
            }
        }

        private static string JobAdmissionServicePath()
        {
            return Path.Combine(
                Application.dataPath,
                "_Project",
                "Scripts",
                "Core",
                "Scheduling",
                "BurstTokenBucketJobAdmissionService.cs");
        }

        private static string JobAdmissionTelemetryBridgePath()
        {
            return Path.Combine(
                Application.dataPath,
                "_Project",
                "Scripts",
                "Core",
                "JobAdmissionTelemetryBridge.cs");
        }

        private static string CrashTelemetryBufferPath()
        {
            return Path.Combine(
                Application.dataPath,
                "_Project",
                "Scripts",
                "CrashTelemetryBuffer.cs");
        }

        private static int CountOccurrences(string source, string value)
        {
            int count = 0;
            int index = 0;
            while (index < source.Length)
            {
                int found = source.IndexOf(value, index, StringComparison.Ordinal);
                if (found < 0)
                    break;

                count++;
                index = found + value.Length;
            }

            return count;
        }

        private static string SystemDispatcherPath()
        {
            return Path.Combine(
                Application.dataPath,
                "_Project",
                "Scripts",
                "Core",
                "SystemDispatcher.cs");
        }

        private static string JobAdmissionScheduleExtensionsPath()
        {
            return Path.Combine(
                Application.dataPath,
                "_Project",
                "Scripts",
                "Core",
                "Scheduling",
                "JobAdmissionScheduleExtensions.cs");
        }

        private static string JobSchedulingProfileCatalogPath()
        {
            return Path.Combine(
                Application.dataPath,
                "_Project",
                "Scripts",
                "Core",
                "Scheduling",
                "JobSchedulingProfileCatalog.cs");
        }

        private static string WorldChunkResidencyManagerPath()
        {
            return Path.Combine(
                Application.dataPath,
                "_Project",
                "Scripts",
                "World",
                "WorldChunkResidencyManager.cs");
        }

        private static string PredatorCognitionDomainSteeringPath()
        {
            return Path.Combine(
                Application.dataPath,
                "_Project",
                "Scripts",
                "Fauna",
                "PredatorCognitionDomain_Steering.cs");
        }

        private static string TelemetryDumpValidatorWindowPath()
        {
            return Path.Combine(
                Application.dataPath,
                "_Project",
                "Scripts",
                "Editor",
                "TelemetryDumpValidatorWindow.cs");
        }

        private static string JobSchedulingProfilesPath()
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "Assets",
                "_SourceData",
                "Core",
                "Scheduling",
                "job_scheduling_profiles.csv"));
        }

        private sealed class CapturingJobAdmissionTelemetrySink : IJobAdmissionTelemetrySink
        {
            public int DeniedCount;
            public byte LastDeniedReasonFlags;

            public void Reset()
            {
                DeniedCount = 0;
                LastDeniedReasonFlags = 0;
            }

            public void ReportAdmissionDenied(
                JobAdmissionLane lane,
                uint jobHash,
                float estimatedCostMs,
                float remainingBudgetMs,
                int criticalDebtFrames,
                byte reasonFlags)
            {
                DeniedCount++;
                LastDeniedReasonFlags = reasonFlags;
            }

            public void ReportLaneState(
                JobAdmissionLane lane,
                float budgetMs,
                float refillMs,
                int criticalDebtFrames,
                uint killSwitchMask)
            {
            }

            public void ReportCostState(
                int slotIndex,
                uint jobHash,
                float ewmaCostMs,
                int costSlotCount,
                float overflowEwmaCostMs)
            {
            }

            public void ReportNonFiniteAdmissionState(
                JobAdmissionLane lane,
                uint jobHash,
                float value,
                int criticalDebtFrames)
            {
            }
        }
    }
}
