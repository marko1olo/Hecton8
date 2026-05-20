using System.Runtime.InteropServices;
using System.Text;
using Hecton8.Gameplay;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Tests.Editor
{
    public sealed class ScannerDataMiningRouterEditTests
    {
        [Test]
        public void ScanResultDto_HasMandatedStrides()
        {
            Assert.AreEqual(48, UnsafeUtility.SizeOf<ScanResultDTO>());
            Assert.AreEqual(16, UnsafeUtility.SizeOf<ScannableEntityMetadataDTO>());
            Assert.AreEqual(32, UnsafeUtility.SizeOf<ScannerVfxDTO>());
            Assert.AreEqual(128, UnsafeUtility.SizeOf<ActiveScanStateDTO>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<ScanProgressDTO>());
            Assert.AreEqual(32, UnsafeUtility.SizeOf<ScannerLoreIndexDTO>());
            Assert.AreEqual(128, UnsafeUtility.SizeOf<ScannerEncyclopediaStateDTO>());
            Assert.AreEqual(0, Marshal.OffsetOf<ScannerSpatialEntityDTO>(nameof(ScannerSpatialEntityDTO.AUP)).ToInt32());
            Assert.AreEqual(24, Marshal.OffsetOf<ScannerSpatialEntityDTO>(nameof(ScannerSpatialEntityDTO.SectorHash)).ToInt32());
            Assert.AreEqual(32, Marshal.OffsetOf<ScannerSpatialEntityDTO>(nameof(ScannerSpatialEntityDTO.DepletionMask)).ToInt32());
            Assert.AreEqual(0, Marshal.OffsetOf<ActiveScanStateDTO>(nameof(ActiveScanStateDTO.TargetAUP)).ToInt32());
            Assert.AreEqual(24, Marshal.OffsetOf<ActiveScanStateDTO>(nameof(ActiveScanStateDTO.LastOriginAUP)).ToInt32());
            Assert.AreEqual(48, Marshal.OffsetOf<ActiveScanStateDTO>(nameof(ActiveScanStateDTO.SectorHash)).ToInt32());
            Assert.AreEqual(56, Marshal.OffsetOf<ActiveScanStateDTO>(nameof(ActiveScanStateDTO.DepletionMask)).ToInt32());
            Assert.IsTrue(ScannerDataMiningRouter.ValidateScanProgressLayout(
                out int scanProgressSize,
                out int targetHashOffset,
                out int progressOffset,
                out int scanRateOffset,
                out int flagsOffset,
                out int scannerAupOffset,
                out int completedHashOffset));
            Assert.AreEqual(64, scanProgressSize);
            Assert.AreEqual(0, targetHashOffset);
            Assert.AreEqual(4, progressOffset);
            Assert.AreEqual(8, scanRateOffset);
            Assert.AreEqual(12, flagsOffset);
            Assert.AreEqual(16, scannerAupOffset);
            Assert.AreEqual(44, completedHashOffset);
        }

        [Test]
        public void RaySphere_UsesAupDeltaBeforeFloatNarrowing()
        {
            double3 origin = new double3(1_000_000_000d, 64d, -1_000_000_000d);
            double3 center = origin + new double3(0d, 0d, 10d);

            bool hit = ScannerSpatialHash.TryRaySphere(
                origin,
                new float3(0f, 0f, 1f),
                center,
                1f,
                64f,
                out float distance,
                out float frontDot);

            Assert.IsTrue(hit);
            Assert.AreEqual(9f, distance, 0.001f);
            Assert.Greater(frontDot, 0.999f);
        }

        [Test]
        public void QueryJob_SelectsNearestForwardSphere()
        {
            using (NativeArray<ScannerSpatialEntityDTO> entities = new NativeArray<ScannerSpatialEntityDTO>(2, Allocator.TempJob))
            using (NativeArray<ScannableEntityMetadataDTO> metadata = new NativeArray<ScannableEntityMetadataDTO>(2, Allocator.TempJob))
            using (NativeArray<MockSdfOcclusionZoneDTO> zones = new NativeArray<MockSdfOcclusionZoneDTO>(1, Allocator.TempJob))
            using (NativeArray<int> bucketHeads = new NativeArray<int>(4, Allocator.TempJob))
            using (NativeArray<int> bucketNext = new NativeArray<int>(2, Allocator.TempJob))
            using (NativeArray<ScanResultDTO> results = new NativeArray<ScanResultDTO>(2, Allocator.TempJob))
            using (NativeArray<int> resultCount = new NativeArray<int>(1, Allocator.TempJob))
            using (NativeArray<ScannerQueryStatsDTO> stats = new NativeArray<ScannerQueryStatsDTO>(1, Allocator.TempJob))
            {
                double3 origin = double3.zero;
                SetNativeArrayElement(entities, 0, MakeEntity(0x100u, new double3(0d, 0d, 20d), 0u));
                SetNativeArrayElement(entities, 1, MakeEntity(0x200u, new double3(0d, 0d, 10d), 1u));
                SetNativeArrayElement(metadata, 0, MakeMetadata(0x100u));
                SetNativeArrayElement(metadata, 1, MakeMetadata(0x200u));
                ScannerSpatialHash.ClearBuckets(bucketHeads, bucketNext);
                ScannerSpatialHash.InsertBucket(bucketHeads, bucketNext, ScannerSpatialHash.CellKey(entities[0].AUP, 16f), 0);
                ScannerSpatialHash.InsertBucket(bucketHeads, bucketNext, ScannerSpatialHash.CellKey(entities[1].AUP, 16f), 1);

                new ScannerSpatialQueryJob
                {
                    Entities = entities,
                    Metadata = metadata,
                    OcclusionZones = zones,
                    BucketHeads = bucketHeads,
                    BucketNext = bucketNext,
                    Results = results,
                    ResultCount = resultCount,
                    QueryStats = stats,
                    Input = MakeInput(origin),
                    Settings = ScannerDataMiningRouter.CreateDefaultSettings(),
                    EntityCount = 2,
                    MetadataCount = 2,
                    OcclusionZoneCount = 0
                }.Run();

                Assert.AreEqual(1, resultCount[0]);
                Assert.AreEqual(0x200u, results[0].EntityHash);
                Assert.AreEqual(1, stats[0].BestEntityIndex);
            }
        }

        [Test]
        public void QueryJob_DearLieSdfDropsOccludedTarget()
        {
            using (NativeArray<ScannerSpatialEntityDTO> entities = new NativeArray<ScannerSpatialEntityDTO>(1, Allocator.TempJob))
            using (NativeArray<ScannableEntityMetadataDTO> metadata = new NativeArray<ScannableEntityMetadataDTO>(1, Allocator.TempJob))
            using (NativeArray<MockSdfOcclusionZoneDTO> zones = new NativeArray<MockSdfOcclusionZoneDTO>(1, Allocator.TempJob))
            using (NativeArray<int> bucketHeads = new NativeArray<int>(2, Allocator.TempJob))
            using (NativeArray<int> bucketNext = new NativeArray<int>(1, Allocator.TempJob))
            using (NativeArray<ScanResultDTO> results = new NativeArray<ScanResultDTO>(1, Allocator.TempJob))
            using (NativeArray<int> resultCount = new NativeArray<int>(1, Allocator.TempJob))
            using (NativeArray<ScannerQueryStatsDTO> stats = new NativeArray<ScannerQueryStatsDTO>(1, Allocator.TempJob))
            {
                SetNativeArrayElement(entities, 0, MakeEntity(0x300u, new double3(0d, 0d, 10d), 0u));
                SetNativeArrayElement(metadata, 0, MakeMetadata(0x300u));
                SetNativeArrayElement(
                    zones,
                    0,
                    new MockSdfOcclusionZoneDTO
                    {
                        CenterAUP = new double3(0d, 0d, 4.5d),
                        Radius = 1.2f,
                        Flags = 1u
                    });
                ScannerSpatialHash.ClearBuckets(bucketHeads, bucketNext);
                ScannerSpatialHash.InsertBucket(bucketHeads, bucketNext, ScannerSpatialHash.CellKey(entities[0].AUP, 16f), 0);

                ScannerSettingsDTO settings = ScannerDataMiningRouter.CreateDefaultSettings();
                settings.SdfMidpointClearance = 0f;
                new ScannerSpatialQueryJob
                {
                    Entities = entities,
                    Metadata = metadata,
                    OcclusionZones = zones,
                    BucketHeads = bucketHeads,
                    BucketNext = bucketNext,
                    Results = results,
                    ResultCount = resultCount,
                    QueryStats = stats,
                    Input = MakeInput(double3.zero),
                    Settings = settings,
                    EntityCount = 1,
                    MetadataCount = 1,
                    OcclusionZoneCount = 1
                }.Run();

                Assert.AreEqual(0, resultCount[0]);
                Assert.AreNotEqual(0u, stats[0].Flags & ScannerDataMiningRouter.QueryFlagOccluded);
            }
        }

        [Test]
        public void ActiveStateRef_MutatesAndMemClearsInPlace()
        {
            using (NativeArray<ActiveScanStateDTO> states = new NativeArray<ActiveScanStateDTO>(1, Allocator.TempJob))
            {
                unsafe
                {
                    ref ActiveScanStateDTO state = ref ScannerDataMiningRouter.GetActiveStateRef(states);
                    state.TargetHash = 77u;
                    state.Progress01 = 0.5f;
                    Assert.AreEqual(77u, states[0].TargetHash);
                    ScannerDataMiningRouter.ResetActiveState(ref state);
                    Assert.AreEqual(0u, states[0].TargetHash);
                    Assert.AreEqual(0f, states[0].Progress01);
                }
            }
        }

        [Test]
        public void CsvOverrideLine_UpdatesMetadataWithoutSplit()
        {
            using (NativeArray<ScannableEntityMetadataDTO> metadata = new NativeArray<ScannableEntityMetadataDTO>(1, Allocator.TempJob))
            {
                SetNativeArrayElement(metadata, 0, MakeMetadata(0xCAFEu));
                char[] csvLine = "0xCAFE,2.75".ToCharArray();
                bool applied = ScannerDataMiningRouter.TryApplyCsvOverrideLine(
                    new System.ReadOnlySpan<char>(csvLine),
                    metadata,
                    1,
                    out uint hash,
                    out float seconds);

                Assert.IsTrue(applied);
                Assert.AreEqual(0xCAFEu, hash);
                Assert.AreEqual(2.75f, seconds, 0.001f);
                Assert.AreEqual(2.75f, metadata[0].ScanDuration, 0.001f);
            }
        }

        [Test]
        public void LoreIndexCsvLine_HashesAsciiTokenIntoIndex()
        {
            using (NativeArray<ScannerLoreIndexDTO> loreIndex = new NativeArray<ScannerLoreIndexDTO>(16, Allocator.TempJob))
            {
                byte[] csvLine = Encoding.ASCII.GetBytes("moss_sample,130");
                bool applied = ScannerDataMiningRouter.TryApplyLoreIndexCsvLine(
                    csvLine,
                    loreIndex,
                    out uint hash,
                    out uint loreEntryIndex);

                Assert.IsTrue(applied);
                Assert.AreEqual(ScannerDataMiningRouter.ComputeFnv1a32Ascii(Encoding.ASCII.GetBytes("moss_sample")), hash);
                Assert.AreEqual(130u, loreEntryIndex);
                Assert.IsTrue(ScannerDataMiningRouter.TryFindLoreIndex(loreIndex, hash, out uint resolvedIndex));
                Assert.AreEqual(130u, resolvedIndex);
            }
        }

        [Test]
        public void CompletionJobs_SetUnmanagedUnlockBit()
        {
            using (NativeArray<ScanProgressDTO> progress = new NativeArray<ScanProgressDTO>(1, Allocator.TempJob))
            using (NativeArray<ScannerLoreIndexDTO> loreIndex = new NativeArray<ScannerLoreIndexDTO>(8, Allocator.TempJob))
            using (NativeArray<ScannerEncyclopediaStateDTO> state = new NativeArray<ScannerEncyclopediaStateDTO>(1, Allocator.TempJob))
            using (NativeArray<ScannerTelemetryEntry> telemetry = new NativeArray<ScannerTelemetryEntry>(4, Allocator.TempJob))
            {
                const uint targetHash = 0xCAFEu;
                Assert.IsTrue(ScannerDataMiningRouter.InsertLoreIndex(loreIndex, targetHash, 130u));
                progress[0] = new ScanProgressDTO
                {
                    TargetHashID = targetHash,
                    CurrentProgress01 = 0.99f,
                    ScanRate = 1f,
                    Flags = ScannerDataMiningRouter.ScanProgressFlagActive,
                    ScannerAUP = new double3(1d, 2d, 3d),
                    LastFrame = 4u,
                    CompletedHash = 0u
                };

                new UpdateScanProgressJob
                {
                    Progress = progress,
                    TargetHashID = targetHash,
                    ScannerAUP = new double3(1d, 2d, 3d),
                    ScanRate = 2f,
                    SimulationTickDelta = 0.01f,
                    Frame = 5u
                }.Run();
                new EvaluateScanCompletionJob
                {
                    Progress = progress,
                    LoreIndex = loreIndex,
                    EncyclopediaState = state,
                    Telemetry = telemetry,
                    Frame = 5u,
                    CompletionCount = 1u
                }.Run();

                ScannerEncyclopediaStateDTO mask = state[0];
                Assert.AreNotEqual(0UL, mask.Mask2 & (1UL << 2));
                Assert.AreEqual(targetHash, progress[0].CompletedHash);
                Assert.AreEqual(targetHash, telemetry[1].TargetHash);
            }
        }

        [Test]
        public void MockGenerationJob_FillsHashOnlyLoreIndex()
        {
            using (NativeArray<ScannerSpatialEntityDTO> entities = new NativeArray<ScannerSpatialEntityDTO>(4, Allocator.TempJob))
            using (NativeArray<ScannableEntityMetadataDTO> metadata = new NativeArray<ScannableEntityMetadataDTO>(4, Allocator.TempJob))
            using (NativeArray<ScannerLoreIndexDTO> loreIndex = new NativeArray<ScannerLoreIndexDTO>(16, Allocator.TempJob))
            using (NativeArray<int> bucketHeads = new NativeArray<int>(16, Allocator.TempJob))
            using (NativeArray<int> bucketNext = new NativeArray<int>(4, Allocator.TempJob))
            {
                new GenerateMockScannableTargetsJob
                {
                    BucketHeads = bucketHeads,
                    BucketNext = bucketNext,
                    Entities = entities,
                    Metadata = metadata,
                    LoreIndex = loreIndex,
                    OriginAUP = double3.zero,
                    Forward = new float3(0f, 0f, 1f),
                    Right = new float3(1f, 0f, 0f),
                    CellSizeMeters = 16f,
                    Count = 4
                }.Run();

                Assert.AreNotEqual(0u, entities[0].EntityHash);
                Assert.IsTrue(ScannerDataMiningRouter.TryFindLoreIndex(loreIndex, entities[0].EntityHash, out uint loreEntryIndex));
                Assert.AreEqual(0u, loreEntryIndex);
            }
        }

        [Test]
        public void QueryCadence_UsesContinuousQualityAndPressure()
        {
            ScannerSettingsDTO settings = ScannerDataMiningRouter.CreateDefaultSettings();
            int low = ScannerDataMiningRouter.ResolveQueryCadenceFrames(0.1f, 0f, in settings);
            int mid = ScannerDataMiningRouter.ResolveQueryCadenceFrames(0.5f, 0f, in settings);
            int ultra = ScannerDataMiningRouter.ResolveQueryCadenceFrames(1f, 0f, in settings);
            int pressured = ScannerDataMiningRouter.ResolveQueryCadenceFrames(1f, 1f, in settings);

            Assert.GreaterOrEqual(low, mid);
            Assert.GreaterOrEqual(mid, ultra);
            Assert.Greater(pressured, ultra);
        }

        private static ScannerSpatialEntityDTO MakeEntity(uint hash, double3 aup, uint metadataIndex)
        {
            return new ScannerSpatialEntityDTO
            {
                AUP = aup,
                EntityHash = hash,
                SphereRadius = 1f,
                MetadataIndex = metadataIndex,
                Flags = ScannerDataMiningRouter.MetadataFlagFauna,
                SectorHash = ScannerSpatialHash.HashSector64(aup, 64f),
                DepletionMask = 1UL,
                DepletionWordIndex = 0u,
                _pad0 = 0u
            };
        }

        private static ScannableEntityMetadataDTO MakeMetadata(uint hash)
        {
            return new ScannableEntityMetadataDTO
            {
                EntityHash = hash,
                ScanDuration = 1f,
                RequiredToolLevel = 1u,
                _pad0 = 0u
            };
        }

        private static void SetNativeArrayElement<T>(NativeArray<T> array, int index, T value)
            where T : struct
        {
            array[index] = value;
        }

        private static MockScannerInputSignal MakeInput(double3 origin)
        {
            return new MockScannerInputSignal
            {
                RayOriginAUP = origin,
                RayDirection = new float3(0f, 0f, 1f),
                MaxDistance = 64f,
                DeltaTime = 0.016f,
                BeamRadius = 0.1f,
                ToolHash = 0xABCDu,
                Frame = 1u,
                ToolLevel = 1u,
                Flags = 1u
            };
        }
    }
}
