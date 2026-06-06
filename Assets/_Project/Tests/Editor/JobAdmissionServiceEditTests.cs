using System;
using System.IO;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Core.Scheduling;
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
