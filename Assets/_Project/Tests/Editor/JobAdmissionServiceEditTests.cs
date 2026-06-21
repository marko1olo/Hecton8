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
            Assert.That(source, Does.Contain("instance.WriteJobAdmissionTelemetry(in args);"));
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

        [Test]
        public void TelemetryDumpValidator_SourceDecodesTerrainStreamingBlackboxAsDedicatedLayout()
        {
            string source = File.ReadAllText(TelemetryDumpValidatorWindowPath());

            Assert.That(source, Does.Contain("private const ulong TerrainStreamingDumpMagic = 0x00384E4F54434548UL;"));
            Assert.That(source, Does.Contain("private const int TerrainStreamingLegacyPagerDumpHeaderBytes = 24;"));
            Assert.That(source, Does.Contain("private const int TerrainStreamingPagerDumpHeaderBytes = 32;"));
            Assert.That(source, Does.Contain("private const int TerrainStreamingDumpEntrySizeBytes = 64;"));
            Assert.That(source, Does.Contain("private const uint TerrainStreamingPagerDumpVersion = 1305u;"));
            Assert.That(source, Does.Contain("private const uint TerrainStreamingPagerDumpLayoutHash = 0x44504354u;"));
            Assert.That(source, Does.Contain("private const int WorldChunkResidencyDumpHeaderBytes = 32;"));
            Assert.That(source, Does.Contain("private const uint WorldChunkResidencyDumpVersion = 1u;"));
            Assert.That(source, Does.Contain("private const uint WorldChunkResidencyDumpLayoutHash = 0x44524357u;"));
            Assert.That(source, Does.Contain("if (TryParseTerrainStreamingDump(path, bytes, span))"));
            Assert.That(source, Does.Contain("if (!IsTerrainStreamingDumpPath(path))"));
            Assert.That(source, Does.Contain("private static bool IsTerrainStreamingDumpPath(string path)"));
            Assert.That(source, Does.Contain("\"Dump_1305_Streaming.bin\""));
            Assert.That(source, Does.Contain("\"Dump_1305_TerrainChunkPager.bin\""));
            Assert.That(source, Does.Contain("\"Dump_1305_WorldChunkResidency.bin\""));
            Assert.That(source, Does.Contain("\"Dump_1305_WorldChunkResidency_Backpressure.bin\""));
            Assert.That(source, Does.Contain("\"Dump_1305_WorldChunkResidency_HLOD.bin\""));
            Assert.That(source, Does.Contain("bool pagerFile = IsTerrainStreamingPagerDumpFileName(fileName);"));
            Assert.That(source, Does.Contain("bool rawResidencyFile = IsWorldChunkResidencyDumpFileName(fileName);"));
            Assert.That(source, Does.Contain("if (span.Length >= TerrainStreamingLegacyPagerDumpHeaderBytes &&"));
            Assert.That(source, Does.Contain("layoutHash == WorldChunkResidencyDumpLayoutHash"));
            Assert.That(source, Does.Contain("if (span.Length < WorldChunkResidencyDumpHeaderBytes)"));
            Assert.That(source, Does.Contain("if (pagerFile || legacyFile)"));
            Assert.That(source, Does.Contain("bool requiresLayoutHash = pagerFile;"));
            Assert.That(source, Does.Contain("ParseTerrainStreamingPagerDump(path, bytes, span, headerBytes, requiresLayoutHash)"));
            Assert.That(source, Does.Contain("layoutHash == TerrainStreamingPagerDumpLayoutHash && reserved == 0u"));
            Assert.That(source, Does.Contain("if (legacyFile &&"));
            Assert.That(source, Does.Contain("ReadU64(span, 0) == TerrainStreamingDumpMagic"));
            Assert.That(source, Does.Contain("span.Length % TerrainStreamingDumpEntrySizeBytes == 0"));
            Assert.That(source, Does.Contain("layout=terrain-chunk-pager-blackbox"));
            Assert.That(source, Does.Contain("layout=world-chunk-residency-blackbox"));
            Assert.That(source, Does.Contain("BuildInvalidTerrainStreamingHeaderSummary("));
            Assert.That(source, Does.Contain("BuildInvalidWorldChunkResidencyHeaderSummary("));
            Assert.That(source, Does.Contain("ParseWorldChunkResidencyHeaderDump("));
            Assert.That(source, Does.Contain("WorldChunkResidencyDumpHeaderBytes + (long)entryCount * entrySize <= span.Length"));
            Assert.That(source, Does.Contain("BuildTerrainStreamingPagerEntryLine("));
            Assert.That(source, Does.Contain("BuildWorldChunkResidencyEntryLine("));
            Assert.That(source, Does.Contain("ResolveTerrainStreamingPagerFaultLabels(flags)"));
            Assert.That(source, Does.Contain("\"missing-file\""));
            Assert.That(source, Does.Contain("\"capacity\""));
            Assert.That(source, Does.Contain("\"addressables-fault\""));
            Assert.That(source, Does.Contain("\"hydration-copy-spike\""));
            Assert.That(source, Does.Contain("ReadF64(entry, 0)"));
            Assert.That(source, Does.Contain("ReadI64(entry, 0)"));
            Assert.That(source, Does.Contain("ComputeXxHash64(bytes, headerBytes, payloadBytes)"));
            Assert.That(source, Does.Contain("ComputeXxHash64(bytes, WorldChunkResidencyDumpHeaderBytes, payloadBytes)"));
            Assert.That(source, Does.Contain("ComputeXxHash64(bytes, 0, span.Length)"));
        }

        [Test]
        public void TelemetryDumpValidator_SourceDecodesGpuScatterDirectorBlackboxAsDedicatedLayout()
        {
            string validatorSource = File.ReadAllText(TelemetryDumpValidatorWindowPath());
            string scatterSource = File.ReadAllText(GpuScatterDirectorPath());

            Assert.That(scatterSource, Does.Contain("private const uint ScatterTelemetryDumpMagic = 0x47505344u;"));
            Assert.That(scatterSource, Does.Contain("private const uint ScatterTelemetryDumpVersion = 1u;"));
            Assert.That(scatterSource, Does.Contain("private const int ScatterTelemetryDumpHeaderBytes = 32;"));
            Assert.That(scatterSource, Does.Contain("private const string ScatterTelemetryDumpPath = \"Docs/AgentLogs/Dump_GPU_SCATTER_DIRECTOR.bin\";"));
            Assert.That(scatterSource, Does.Contain("[StructLayout(LayoutKind.Explicit, Size = 64)]"));
            Assert.That(scatterSource, Does.Contain("private struct ScatterTelemetryEntry"));
            Assert.That(scatterSource, Does.Contain("WriteUInt32LittleEndian(target, 0, ScatterTelemetryDumpMagic);"));
            Assert.That(scatterSource, Does.Contain("WriteInt32LittleEndian(target, 12, count);"));
            Assert.That(scatterSource, Does.Contain("WriteInt32LittleEndian(target, 16, entrySize);"));
            Assert.That(scatterSource, Does.Contain("WriteUInt32LittleEndian(target, 20, ScatterTelemetryHashSeed);"));
            Assert.That(scatterSource, Does.Contain("WriteUInt32LittleEndian(target, 24, ScatterTelemetryInvalidStateFlag);"));

            Assert.That(validatorSource, Does.Contain("private const uint GpuScatterDumpMagic = 0x47505344u;"));
            Assert.That(validatorSource, Does.Contain("private const int GpuScatterDumpHeaderBytes = 32;"));
            Assert.That(validatorSource, Does.Contain("private const int GpuScatterDumpEntrySizeBytes = 64;"));
            Assert.That(validatorSource, Does.Contain("private const string GpuScatterDumpFileName = \"Dump_GPU_SCATTER_DIRECTOR.bin\";"));
            Assert.That(validatorSource, Does.Contain("if (TryParseGpuScatterDump(path, bytes, span))"));
            Assert.That(validatorSource, Does.Contain("private bool TryParseGpuScatterDump(string path, byte[] bytes, ReadOnlySpan<byte> span)"));
            Assert.That(validatorSource, Does.Contain("ReadU32(span, 0) != GpuScatterDumpMagic"));
            Assert.That(validatorSource, Does.Contain("hashSeed == GpuScatterTelemetryHashSeed"));
            Assert.That(validatorSource, Does.Contain("invalidStateFlag == GpuScatterInvalidStateFlag"));
            Assert.That(validatorSource, Does.Contain("reserved == 0u"));
            Assert.That(validatorSource, Does.Contain("layout=gpu-scatter-director-blackbox"));
            Assert.That(validatorSource, Does.Contain("BuildInvalidGpuScatterHeaderSummary("));
            Assert.That(validatorSource, Does.Contain("BuildGpuScatterEntryLine("));
            Assert.That(validatorSource, Does.Contain("ResolveGpuScatterFlagsLabel(flags)"));
            Assert.That(validatorSource, Does.Contain("\"missing-dependency\""));
            Assert.That(validatorSource, Does.Contain("\"invalid-state\""));
            Assert.That(validatorSource, Does.Contain("\"origin-shift\""));
            Assert.That(validatorSource, Does.Contain("ComputeGpuScatterStateHash("));
            Assert.That(validatorSource, Does.Contain("hashOk="));
            Assert.That(validatorSource, Does.Contain("ComputeXxHash64(bytes, GpuScatterDumpHeaderBytes, payloadBytes)"));
        }

        [Test]
        public void TelemetryDumpValidator_SourceDecodesGpuScatterLodManagerBlackboxAsDedicatedLayout()
        {
            string validatorSource = File.ReadAllText(TelemetryDumpValidatorWindowPath());
            string lodSource = File.ReadAllText(GpuScatterLodManagerPath());

            Assert.That(lodSource, Does.Contain("private const uint BlackBoxMagic = 0x47534C4Du;"));
            Assert.That(lodSource, Does.Contain("private const uint BlackBoxVersion = 2u;"));
            Assert.That(lodSource, Does.Contain("private const int BlackBoxHeaderBytes = 20;"));
            Assert.That(lodSource, Does.Contain("private const int ScatterBlackBoxEntryStrideBytes = 64;"));
            Assert.That(lodSource, Does.Contain("const string path = \"Docs/AgentLogs/Dump_GPU_SCATTER_LOD_MANAGER.bin\";"));
            Assert.That(lodSource, Does.Contain("[StructLayout(LayoutKind.Explicit, Size = ScatterBlackBoxEntryStrideBytes)]"));
            Assert.That(lodSource, Does.Contain("private struct ScatterBlackBoxEntry"));
            Assert.That(lodSource, Does.Contain("WriteUInt32LittleEndian(destination, 0, BlackBoxMagic);"));
            Assert.That(lodSource, Does.Contain("WriteUInt32LittleEndian(destination, 8, reason);"));
            Assert.That(lodSource, Does.Contain("WriteInt32LittleEndian(destination, 12, blackBoxLength);"));
            Assert.That(lodSource, Does.Contain("WriteInt32LittleEndian(destination, 16, _blackBoxCursor);"));

            Assert.That(validatorSource, Does.Contain("private const uint GpuScatterLodDumpMagic = 0x47534C4Du;"));
            Assert.That(validatorSource, Does.Contain("private const uint GpuScatterLodDumpVersion = 2u;"));
            Assert.That(validatorSource, Does.Contain("private const int GpuScatterLodDumpHeaderBytes = 20;"));
            Assert.That(validatorSource, Does.Contain("private const int GpuScatterLodDumpEntrySizeBytes = 64;"));
            Assert.That(validatorSource, Does.Contain("private const string GpuScatterLodDumpFileName = \"Dump_GPU_SCATTER_LOD_MANAGER.bin\";"));
            Assert.That(validatorSource, Does.Contain("if (TryParseGpuScatterLodDump(path, bytes, span))"));
            Assert.That(validatorSource, Does.Contain("private bool TryParseGpuScatterLodDump(string path, byte[] bytes, ReadOnlySpan<byte> span)"));
            Assert.That(validatorSource, Does.Contain("ReadU32(span, 0) != GpuScatterLodDumpMagic"));
            Assert.That(validatorSource, Does.Contain("layout=gpu-scatter-lod-blackbox"));
            Assert.That(validatorSource, Does.Contain("BuildInvalidGpuScatterLodHeaderSummary("));
            Assert.That(validatorSource, Does.Contain("BuildGpuScatterLodEntryLine("));
            Assert.That(validatorSource, Does.Contain("ResolveGpuScatterLodReasonLabel(reason)"));
            Assert.That(validatorSource, Does.Contain("ResolveGpuScatterLodFlagsLabel(flags)"));
            Assert.That(validatorSource, Does.Contain("\"nonfinite-matrix\""));
            Assert.That(validatorSource, Does.Contain("\"nonfinite-metadata\""));
            Assert.That(validatorSource, Does.Contain("\"nonfinite-auxiliary-lane\""));
            Assert.That(validatorSource, Does.Contain("\"nonfinite-aup\""));
            Assert.That(validatorSource, Does.Contain("\"abi-layout\""));
            Assert.That(validatorSource, Does.Contain("\"invalid-material-variant\""));
            Assert.That(validatorSource, Does.Contain("ComputeXxHash64(bytes, GpuScatterLodDumpHeaderBytes, payloadBytes)"));
        }

        [Test]
        public void TelemetryDumpValidator_SourceDecodesVegetationMemoryBlackboxAsDedicatedLayout()
        {
            string validatorSource = File.ReadAllText(TelemetryDumpValidatorWindowPath());
            string runtimeSource = File.ReadAllText(VegetationMemorySovereigntyRuntimePath());
            string contractsSource = File.ReadAllText(VegetationMemorySovereigntyContractsPath());

            Assert.That(contractsSource, Does.Contain("public const ulong DumpMagic = 0x313331365F564547UL;"));
            Assert.That(contractsSource, Does.Contain("public const int DumpVersion = 1;"));
            Assert.That(contractsSource, Does.Contain("public const string DumpRelativePath = \"Docs/AgentLogs/Dump_1316_Vegetation.bin\";"));
            Assert.That(contractsSource, Does.Contain("[StructLayout(LayoutKind.Explicit, Size = VegetationMemorySovereigntyConstants.TelemetryEntryStrideBytes)]"));
            Assert.That(contractsSource, Does.Contain("[FieldOffset(0)] public ulong StateHash;"));
            Assert.That(contractsSource, Does.Contain("[FieldOffset(40)] public ushort FailureCode;"));
            Assert.That(contractsSource, Does.Contain("[FieldOffset(42)] public ushort Phase;"));
            Assert.That(contractsSource, Does.Contain("[FieldOffset(44)] public uint Flags;"));
            Assert.That(runtimeSource, Does.Contain("private const int VegetationMemoryDumpHeaderBytes = 24;"));
            Assert.That(runtimeSource, Does.Contain("WriteUInt64LittleEndian(header, 0, VegetationMemorySovereigntyConstants.DumpMagic);"));
            Assert.That(runtimeSource, Does.Contain("WriteInt32LittleEndian(header, 8, VegetationMemorySovereigntyConstants.DumpVersion);"));
            Assert.That(runtimeSource, Does.Contain("WriteInt32LittleEndian(header, 12, VegetationMemorySovereigntyConstants.TelemetryFrameCount);"));
            Assert.That(runtimeSource, Does.Contain("WriteInt32LittleEndian(header, 16, rowBytes);"));
            Assert.That(runtimeSource, Does.Contain("WriteInt32LittleEndian(header, 20, cursorBuffer.Length > 0 ? cursorBuffer[0] : 0);"));
            Assert.That(runtimeSource, Does.Contain("HashVegetationMemoryTelemetry(entry)"));
            Assert.That(runtimeSource, Does.Contain("hash *= 1099511628211UL;"));

            Assert.That(validatorSource, Does.Contain("private const ulong VegetationMemoryDumpMagic = 0x313331365F564547UL;"));
            Assert.That(validatorSource, Does.Contain("private const int VegetationMemoryDumpHeaderBytes = 24;"));
            Assert.That(validatorSource, Does.Contain("private const int VegetationMemoryDumpEntrySizeBytes = 64;"));
            Assert.That(validatorSource, Does.Contain("private const string VegetationMemoryDumpFileName = \"Dump_1316_Vegetation.bin\";"));
            Assert.That(validatorSource, Does.Contain("if (TryParseVegetationMemoryDump(path, bytes, span))"));
            Assert.That(validatorSource, Does.Contain("private bool TryParseVegetationMemoryDump(string path, byte[] bytes, ReadOnlySpan<byte> span)"));
            Assert.That(validatorSource, Does.Contain("ReadU64(span, 0) != VegetationMemoryDumpMagic"));
            Assert.That(validatorSource, Does.Contain("layout=vegetation-memory-blackbox"));
            Assert.That(validatorSource, Does.Contain("BuildInvalidVegetationMemoryHeaderSummary("));
            Assert.That(validatorSource, Does.Contain("BuildVegetationMemoryEntryLine("));
            Assert.That(validatorSource, Does.Contain("ResolveVegetationMemoryCodeLabel(failureCode)"));
            Assert.That(validatorSource, Does.Contain("ResolveVegetationMemoryPhaseLabel(phase)"));
            Assert.That(validatorSource, Does.Contain("ResolveVegetationMemoryFlagsLabel(flags)"));
            Assert.That(validatorSource, Does.Contain("\"nan-detected\""));
            Assert.That(validatorSource, Does.Contain("\"compaction-fence-active\""));
            Assert.That(validatorSource, Does.Contain("\"write-lock-contention\""));
            Assert.That(validatorSource, Does.Contain("ComputeVegetationMemoryStateHash("));
            Assert.That(validatorSource, Does.Contain("hashOk="));
            Assert.That(validatorSource, Does.Contain("ComputeXxHash64(bytes, VegetationMemoryDumpHeaderBytes, payloadBytes)"));
        }

        [Test]
        public void TelemetryDumpValidator_SourceDecodesGlobalShaderDispatcherBlackboxAsDedicatedLayout()
        {
            string validatorSource = File.ReadAllText(TelemetryDumpValidatorWindowPath());
            string dispatcherSource = File.ReadAllText(GlobalShaderDispatcherPath());

            Assert.That(dispatcherSource, Does.Contain("private const uint TelemetryDumpMagic = 0x47534844u;"));
            Assert.That(dispatcherSource, Does.Contain("private const uint TelemetryDumpVersion = 1u;"));
            Assert.That(dispatcherSource, Does.Contain("private const int TelemetryDumpHeaderBytes = 32;"));
            Assert.That(dispatcherSource, Does.Contain("private const int TelemetryDumpEntryBytes = 16;"));
            Assert.That(dispatcherSource, Does.Contain("private const string TelemetryDumpPath = \"Docs/AgentLogs/Dump_GLOBAL_SHADER_DISPATCHER.bin\";"));
            Assert.That(dispatcherSource, Does.Contain("WriteUInt32LittleEndian(target, 0, TelemetryDumpMagic);"));
            Assert.That(dispatcherSource, Does.Contain("WriteUInt32LittleEndian(target, 8, reasonFlags);"));
            Assert.That(dispatcherSource, Does.Contain("WriteInt32LittleEndian(target, 12, telemetryCursor);"));
            Assert.That(dispatcherSource, Does.Contain("WriteInt32LittleEndian(target, 16, count);"));
            Assert.That(dispatcherSource, Does.Contain("WriteInt32LittleEndian(target, 20, TelemetryDumpEntryBytes);"));
            Assert.That(dispatcherSource, Does.Contain("WriteUInt32LittleEndian(target, 24, (uint)RequiredShaderGlobalSlots);"));
            Assert.That(dispatcherSource, Does.Contain("float4 telemetryEntry = new float4(frame, dispatchMicroseconds, keywordCount, flags);"));

            Assert.That(validatorSource, Does.Contain("private const uint GlobalShaderDispatcherDumpMagic = 0x47534844u;"));
            Assert.That(validatorSource, Does.Contain("private const int GlobalShaderDispatcherDumpHeaderBytes = 32;"));
            Assert.That(validatorSource, Does.Contain("private const int GlobalShaderDispatcherDumpEntrySizeBytes = 16;"));
            Assert.That(validatorSource, Does.Contain("private const string GlobalShaderDispatcherDumpFileName = \"Dump_GLOBAL_SHADER_DISPATCHER.bin\";"));
            Assert.That(validatorSource, Does.Contain("if (TryParseGlobalShaderDispatcherDump(path, bytes, span))"));
            Assert.That(validatorSource, Does.Contain("private bool TryParseGlobalShaderDispatcherDump(string path, byte[] bytes, ReadOnlySpan<byte> span)"));
            Assert.That(validatorSource, Does.Contain("ReadU32(span, 0) != GlobalShaderDispatcherDumpMagic"));
            Assert.That(validatorSource, Does.Contain("layout=global-shader-dispatcher-blackbox"));
            Assert.That(validatorSource, Does.Contain("BuildInvalidGlobalShaderDispatcherHeaderSummary("));
            Assert.That(validatorSource, Does.Contain("BuildGlobalShaderDispatcherEntryLine("));
            Assert.That(validatorSource, Does.Contain("ResolveGlobalShaderDispatcherReasonLabel(reasonFlags)"));
            Assert.That(validatorSource, Does.Contain("\"layout-fault\""));
            Assert.That(validatorSource, Does.Contain("\"dispatch-over-budget\""));
            Assert.That(validatorSource, Does.Contain("\"vault-unavailable\""));
            Assert.That(validatorSource, Does.Contain("FloatToUIntOrZero(flagsFloat)"));
            Assert.That(validatorSource, Does.Contain("ComputeXxHash64(bytes, GlobalShaderDispatcherDumpHeaderBytes, payloadBytes)"));
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

        private static string GpuScatterDirectorPath()
        {
            return Path.Combine(
                Application.dataPath,
                "_Project",
                "Scripts",
                "World",
                "GPUScatterDirector.cs");
        }

        private static string GpuScatterLodManagerPath()
        {
            return Path.Combine(
                Application.dataPath,
                "_Project",
                "Scripts",
                "Rendering",
                "Scatter",
                "GpuScatterLodManager.cs");
        }

        private static string VegetationMemorySovereigntyRuntimePath()
        {
            return Path.Combine(
                Application.dataPath,
                "_Project",
                "Scripts",
                "World",
                "VegetationMemorySovereigntyRuntime.cs");
        }

        private static string VegetationMemorySovereigntyContractsPath()
        {
            return Path.Combine(
                Application.dataPath,
                "_Project",
                "Scripts",
                "World",
                "VegetationMemorySovereigntyContracts.cs");
        }

        private static string GlobalShaderDispatcherPath()
        {
            return Path.Combine(
                Application.dataPath,
                "_Project",
                "Scripts",
                "Rendering",
                "GlobalShaderDispatcher.cs");
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
