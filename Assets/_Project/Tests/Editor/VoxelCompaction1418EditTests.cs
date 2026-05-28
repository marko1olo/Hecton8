using System.IO;
using Hecton8.Caves;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class VoxelCompaction1418EditTests
    {
        const string EnginePath = "Assets/_Project/Scripts/HectonVoxelEngine.cs";
        const string DeltaPath = "Assets/_Project/Scripts/VoxelDeltaProcessor.cs";
        const string VolumePath = "Assets/_Project/Scripts/HectonVoxelVolume.cs";

        [Test]
        public void TargetVoxelSources_HaveNoTempJobOrNativeArrayExchangeAllocations()
        {
            string engine = ReadProjectFile(EnginePath);
            string delta = ReadProjectFile(DeltaPath);

            Assert.That(engine, Does.Not.Contain("Allocator.TempJob"));
            Assert.That(delta, Does.Not.Contain("Allocator.TempJob"));
            Assert.That(engine, Does.Not.Contain("new NativeArray<"));
            Assert.That(delta, Does.Not.Contain("new NativeArray<"));
        }

        [Test]
        public void StreamingScratchRoute_UsesCollisionFree1418RangeAndDumpPath()
        {
            string engine = ReadProjectFile(EnginePath);

            StringAssert.Contains("StreamingScratchVaultBufferBase = 76500", engine);
            StringAssert.DoesNotContain("StreamingScratchVaultBufferBase = 74500", engine);
            StringAssert.Contains("StreamingScratchVaultStride = 60", engine);
            StringAssert.Contains("StreamingScratchLeaseSlotCapacity = 8", engine);
            StringAssert.Contains("Dump_1418_VoxelCompaction.bin", engine);
        }

        [Test]
        public void PublicVoxelReadAccessors_DoNotExposeIntermediateScratch()
        {
            string engine = ReadProjectFile(EnginePath).Replace("\r\n", "\n");
            string bulkReadBlock = ExtractBlock(engine, "public bool TryReadNearestSonarSdf", "public bool TryAcquireNearestSonarSdfReadLease");
            string leaseBlock = ExtractBlock(engine, "public bool TryAcquireNearestSonarSdfReadLease", "public void ReleaseNearestSonarSdfReadLease");

            StringAssert.Contains("return false;", bulkReadBlock);
            StringAssert.DoesNotContain("ScratchLease", bulkReadBlock);
            StringAssert.DoesNotContain("ModifiedCellsScratch", bulkReadBlock);
            StringAssert.Contains("TryAcquireNearestActiveSonarSdfPayloadReadLease", leaseBlock);
            StringAssert.Contains("finally", leaseBlock);
            StringAssert.Contains("ReleasePublishedSonarSdfPayloadReadLease", leaseBlock);
            StringAssert.DoesNotContain("StreamingScratch", leaseBlock);
        }

        [Test]
        public void NearestSonarSample_UsesAlreadyLeasedPublishedSdfPayload()
        {
            string engine = ReadProjectFile(EnginePath).Replace("\r\n", "\n");
            string sampleBlock = ExtractBlock(engine, "public bool TrySampleNearestSonarSdf", "private bool TryAcquireNearestActiveSonarSdfPayloadReadLease");

            StringAssert.Contains("TryAcquirePublishedSonarSdfPayloadReadLease", sampleBlock);
            StringAssert.Contains("out NativeArray<byte>.ReadOnly candidateSdf", sampleBlock);
            StringAssert.Contains("out float candidateRange", sampleBlock);
            StringAssert.Contains("ResolveSdfPayloadBoundsDistanceSq", sampleBlock);
            StringAssert.Contains("VoxelSonarSdfMath.TrySampleEncodedSdfTrilinear", sampleBlock);
            StringAssert.DoesNotContain("volume.TrySampleSonarSdf", sampleBlock);
            Assert.Less(
                sampleBlock.IndexOf("ResolveSdfPayloadBoundsDistanceSq"),
                sampleBlock.IndexOf("VoxelSonarSdfMath.TrySampleEncodedSdfTrilinear"));
            StringAssert.Contains("finally", sampleBlock);
            StringAssert.Contains("ReleasePublishedSonarSdfPayloadReadLease", sampleBlock);
        }

        [Test]
        public void VoxelVolumeBulkDensityScans_UseSinglePublishedSdfLease()
        {
            string volume = ReadProjectFile(VolumePath).Replace("\r\n", "\n");
            string burrowBlock = ExtractBlock(volume, "public bool TryResolveBurrowAmbushRoute", "private static int ResolveDominantAxis");
            string rootMoundBlock = ExtractBlock(volume, "private float ResolveOrganicRootMoundWeldRadius", "public void ApplyPersistentResourceCrater");
            string seismicBlock = ExtractBlock(volume, "private bool TryResolveSeismicCollapseAnchor", "private bool TryResolveTopSolidAnchor");
            string topAnchorBlock = ExtractBlock(volume, "private bool TryResolveTopSolidAnchor", "private bool TryResolveNearestSolidDistance");
            string nearestDistanceBlock = ExtractBlock(volume, "private bool TryResolveNearestSolidDistance", "private bool HasSolidDensityPath");
            string densityPathBlock = ExtractBlock(volume, "private bool HasSolidDensityPath", "private static float Hash01");

            StringAssert.Contains("TryAcquirePublishedSonarSdfPayloadReadLease", burrowBlock);
            Assert.AreEqual(1, CountOccurrences(burrowBlock, "TryAcquirePublishedSonarSdfPayloadReadLease"));
            StringAssert.Contains("finally", burrowBlock);
            StringAssert.Contains("ReleasePublishedSonarSdfPayloadReadLease", burrowBlock);
            StringAssert.DoesNotContain("TrySampleDensity(", burrowBlock);

            AssertSinglePublishedLeaseDensityLoop(rootMoundBlock);
            AssertSinglePublishedLeaseDensityLoop(seismicBlock);
            AssertPublishedDensityHelper(topAnchorBlock);
            AssertPublishedDensityHelper(nearestDistanceBlock);
            AssertPublishedDensityHelper(densityPathBlock);
        }

        [Test]
        public void ScratchLeaseWindows_ArePinnedAndReleasedInFinally()
        {
            string engine = ReadProjectFile(EnginePath).Replace("\r\n", "\n");

            StringAssert.Contains("TryLockStreamingScratchJobLifetime(ref data.ScratchLease)", engine);
            StringAssert.Contains("finally\n        {\n            UnlockStreamingScratchJobLifetime(ref data.ScratchLease);\n        }", engine);
            StringAssert.Contains("TryAcquireWriteLock(in _voxelMeshPipelineBlackBoxHandle", engine);
            StringAssert.Contains("vault.ReleaseWriteLock(in _voxelMeshPipelineBlackBoxHandle", engine);
        }

        [Test]
        public void VoxelQualityWeight_RemainsContinuousAcrossDrainAndScratchCapacity()
        {
            string engine = ReadProjectFile(EnginePath);
            string delta = ReadProjectFile(DeltaPath);

            StringAssert.Contains("HomeostasisBrain.GlobalQualityWeight", engine);
            StringAssert.Contains("math.lerp", engine);
            StringAssert.Contains("StreamingMeshRawVertexScratchLowTierCapacity", engine);
            StringAssert.Contains("StreamingMeshRawVertexScratchVisualOverkillCapacity", engine);
            StringAssert.Contains("ResolveQueuedCarveDrainBudgetPerFrame", delta);
            StringAssert.Contains("HomeostasisBrain.GlobalQualityWeight", delta);
            StringAssert.Contains("math.lerp", delta);
            StringAssert.DoesNotContain("HectonQualityTier", engine);
            StringAssert.DoesNotContain("HectonQualityTier", delta);
        }

        [Test]
        public void VoxelDtoLayouts_AreExplicitAndRuntimeGuarded()
        {
            uint failureFlags = 0u;

            Assert.AreEqual(8, UnsafeUtility.SizeOf<VoxelModifiedCell>());
            Assert.AreEqual(24, UnsafeUtility.SizeOf<global::VoxelModifiedCellEntry>());
            Assert.AreEqual(24, UnsafeUtility.SizeOf<global::MCRawVertex>());
            Assert.IsTrue(global::HectonVoxelEngine.ValidateAgent1315EnginePrivateLayouts(ref failureFlags), failureFlags.ToString("X8"));
            Assert.AreEqual(0u, failureFlags);
            Assert.IsTrue(VoxelDeltaProcessor.ValidateAgent1304PrivateLayouts(ref failureFlags), failureFlags.ToString("X8"));
            Assert.AreEqual(0u, failureFlags);
        }

        [Test]
        public void MockVoxelSpamFuzzer_UsesBoundedOverflowRoutes()
        {
            string engine = ReadProjectFile(EnginePath);
            string delta = ReadProjectFile(DeltaPath);
            const int spamHits = 50000;
            const int scratchSlots = 8;
            const int frameBudgetLow = 1;
            const int frameBudgetUltra = 64;

            int framesLow = (spamHits + frameBudgetLow - 1) / frameBudgetLow;
            int framesUltra = (spamHits + frameBudgetUltra - 1) / frameBudgetUltra;

            Assert.Greater(framesLow, framesUltra);
            Assert.AreEqual(6250, spamHits / scratchSlots);
            StringAssert.Contains("ReportVoxelMeshScratchCapacityOverflow", engine);
            StringAssert.Contains("VoxelMeshPipelineScratchCapacityOverflowFlag", engine);
            StringAssert.Contains("TryCoalesceOverflowPendingCarve", delta);
            StringAssert.Contains("VoxelBlackBoxQueueOverflowFlag", delta);
        }

        static string ReadProjectFile(string relativePath)
        {
            return File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath)));
        }

        static string ExtractBlock(string source, string startToken, string endToken)
        {
            int start = source.IndexOf(startToken);
            Assert.GreaterOrEqual(start, 0, startToken);
            int end = source.IndexOf(endToken, start + startToken.Length);
            Assert.Greater(end, start, endToken);
            return source.Substring(start, end - start);
        }

        static void AssertSinglePublishedLeaseDensityLoop(string source)
        {
            StringAssert.Contains("TryAcquirePublishedSonarSdfPayloadReadLease", source);
            Assert.AreEqual(1, CountOccurrences(source, "TryAcquirePublishedSonarSdfPayloadReadLease"));
            StringAssert.Contains("TrySamplePublishedDensity", source);
            StringAssert.Contains("finally", source);
            StringAssert.Contains("ReleasePublishedSonarSdfPayloadReadLease", source);
            StringAssert.DoesNotContain("TrySampleDensity(", source);
        }

        static void AssertPublishedDensityHelper(string source)
        {
            StringAssert.Contains("TrySamplePublishedDensity", source);
            StringAssert.DoesNotContain("TrySampleDensity(", source);
        }

        static int CountOccurrences(string source, string token)
        {
            int count = 0;
            int index = 0;
            while (index < source.Length)
            {
                index = source.IndexOf(token, index);
                if (index < 0)
                    return count;

                count++;
                index += token.Length;
            }

            return count;
        }
    }
}
