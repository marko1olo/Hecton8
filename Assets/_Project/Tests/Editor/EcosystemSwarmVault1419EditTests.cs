using System;
using Hecton8.AI.Ecosystem;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.World;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Tests.Editor
{
    public sealed class EcosystemSwarmVault1419EditTests
    {
        private const int EntityCount = 5000;
        private const int QueryCount = 500;
        private const int QueryResultCapacity = 64;
        private const int BucketRangeCapacity = 8192;
        private const uint Frame = 1419u;
        private const float CellSizeMeters = 10f;
        private const uint EntityFlagActive = 1u << 0;
        private const uint EntityFlagHydrated = 1u << 2;

        private struct HarnessHandles
        {
            public VaultGenerationHandle<SpatialGridEntryDTO> Entries;
            public VaultGenerationHandle<SpatialGridBucketRangeDTO> Ranges;
            public VaultGenerationHandle<AmbientEntityAupDTO> Aups;
            public VaultGenerationHandle<SpatialGridTelemetryEntry> Telemetry;
            public VaultGenerationHandle<int> TelemetryCursor;
        }

        [Test]
        public void EcosystemSwarmDtos_AreExplicitAndArm64Aligned()
        {
            EcosystemTelemetryEntry ecosystemTelemetry = default;
            SpatialGridTelemetryEntry spatialTelemetry = default;
            SpatialGridEntryDTO gridEntry = default;
            SpatialGridBucketRangeDTO range = default;

            Assert.AreEqual(64, UnsafeUtility.SizeOf<EcosystemTelemetryEntry>());
            Assert.AreEqual(0, UnsafeUtility.SizeOf<EcosystemTelemetryEntry>() & 7);
            Assert.AreEqual(56, ByteOffset(ref ecosystemTelemetry, ref ecosystemTelemetry.Pad0));
            Assert.AreEqual(60, ByteOffset(ref ecosystemTelemetry, ref ecosystemTelemetry.CsvLoadedCount));
            Assert.AreEqual(62, ByteOffset(ref ecosystemTelemetry, ref ecosystemTelemetry.ProfileLoadedCount));

            Assert.AreEqual(64, UnsafeUtility.SizeOf<SpatialGridTelemetryEntry>());
            Assert.AreEqual(0, UnsafeUtility.SizeOf<SpatialGridTelemetryEntry>() & 7);
            Assert.AreEqual(40, ByteOffset(ref spatialTelemetry, ref spatialTelemetry.StateHash));
            Assert.AreEqual(56, ByteOffset(ref spatialTelemetry, ref spatialTelemetry.InvalidInputCount));
            Assert.AreEqual(60, ByteOffset(ref spatialTelemetry, ref spatialTelemetry.Pad1));

            Assert.AreEqual(16, UnsafeUtility.SizeOf<SpatialGridEntryDTO>());
            Assert.AreEqual(0, ByteOffset(ref gridEntry, ref gridEntry.EntityHashID));
            Assert.AreEqual(0, ByteOffset(ref gridEntry, ref gridEntry.EntityRowIndex));
            Assert.AreEqual(8, ByteOffset(ref gridEntry, ref gridEntry.CellFingerprint));

            Assert.AreEqual(32, UnsafeUtility.SizeOf<SpatialGridBucketRangeDTO>());
            Assert.AreEqual(0, UnsafeUtility.SizeOf<SpatialGridBucketRangeDTO>() & 7);
            Assert.AreEqual(12, ByteOffset(ref range, ref range.StartIndex));
            Assert.AreEqual(24, ByteOffset(ref range, ref range.Pad0));
        }

        [Test]
        public void MockSwarmSpatialQueryStress_IsDeterministicAndZeroManagedGc()
        {
            using (GlobalDataVault vault = GlobalDataVault.Create(64, GlobalDataVault.MinimumQualityArenaLimitBytes))
            using (NativeArray<uint> results = new NativeArray<uint>(QueryResultCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory))
            {
                HarnessHandles handles = CreateHandles(vault);
                SeedSpatialGrid(vault, in handles);
                SpatialHashQuery query = CreateQuery(in handles);

                int warmupCount = query.CollectEntitiesInRadius(vault, double3.zero, CellSizeMeters, results);
                Assert.AreEqual(QueryResultCapacity, warmupCount);

                long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
                uint hash = 2166136261u;
                int totalHits = 0;
                for (int i = 0; i < QueryCount; i++)
                {
                    int hitCount = query.CollectEntitiesInRadius(vault, double3.zero, CellSizeMeters, results);
                    totalHits += hitCount;
                    hash = Mix(hash, (uint)hitCount);
                    hash = Mix(hash, results[0]);
                    hash = Mix(hash, results[hitCount - 1]);
                }

                long afterBytes = GC.GetAllocatedBytesForCurrentThread();
                Assert.AreEqual(QueryCount * QueryResultCapacity, totalHits);
                Assert.AreNotEqual(0u, hash);
                Assert.AreEqual(0L, afterBytes - beforeBytes);
            }
        }

        [Test]
        public void VaultSpatialLocks_FailClosedWithoutManagedGc_WhenAlreadyHeld()
        {
            using (GlobalDataVault vault = GlobalDataVault.Create(64, GlobalDataVault.MinimumQualityArenaLimitBytes))
            {
                HarnessHandles handles = CreateHandles(vault);
                Assert.IsTrue(vault.TryAcquireWriteLock(in handles.Entries, SystemID.AIEcology, out NativeArray<SpatialGridEntryDTO> first));
                Assert.IsTrue(first.IsCreated);
                try
                {
                    long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
                    bool allFailedClosed = true;
                    for (int i = 0; i < QueryCount; i++)
                    {
                        allFailedClosed &= !vault.TryAcquireWriteLock(in handles.Entries, SystemID.AIEcology, out NativeArray<SpatialGridEntryDTO> blocked);
                        allFailedClosed &= !blocked.IsCreated;
                    }

                    long afterBytes = GC.GetAllocatedBytesForCurrentThread();
                    Assert.IsTrue(allFailedClosed);
                    Assert.AreEqual(0L, afterBytes - beforeBytes);
                }
                finally
                {
                    vault.ReleaseWriteLock(in handles.Entries, SystemID.AIEcology);
                }
            }
        }

        [Test]
        public void InvalidSpatialQuery_FailsClosedToEmptyResult()
        {
            using (GlobalDataVault vault = GlobalDataVault.Create(64, GlobalDataVault.MinimumQualityArenaLimitBytes))
            using (NativeArray<uint> results = new NativeArray<uint>(QueryResultCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory))
            {
                HarnessHandles handles = CreateHandles(vault);
                SpatialHashQuery query = default;
                query.TelemetryHandle = handles.Telemetry;
                query.TelemetryCursorHandle = handles.TelemetryCursor;
                query.Frame = Frame;
                query.CellSizeMeters = CellSizeMeters;

                int count = query.CollectEntitiesInRadius(vault, double3.zero, CellSizeMeters, results);
                Assert.AreEqual(0, count);
            }
        }

        private static HarnessHandles CreateHandles(GlobalDataVault vault)
        {
            return new HarnessHandles
            {
                Entries = vault.EnsureGenerationHandle<SpatialGridEntryDTO>(
                    BufferID.ShinobuSpatialGridEntries,
                    EntityCount,
                    SystemID.AIEcology,
                    NativeArrayOptions.ClearMemory),
                Ranges = vault.EnsureGenerationHandle<SpatialGridBucketRangeDTO>(
                    BufferID.ShinobuSpatialGridBucketRanges,
                    BucketRangeCapacity,
                    SystemID.AIEcology,
                    NativeArrayOptions.ClearMemory),
                Aups = vault.EnsureGenerationHandle<AmbientEntityAupDTO>(
                    BufferID.ShinobuAmbientAupSnapshot,
                    EntityCount,
                    SystemID.AIEcology,
                    NativeArrayOptions.ClearMemory),
                Telemetry = vault.EnsureGenerationHandle<SpatialGridTelemetryEntry>(
                    BufferID.ShinobuSpatialGridTelemetryRing,
                    ShinobuSpatialGridConstants.TelemetryCapacity,
                    SystemID.AIEcology,
                    NativeArrayOptions.ClearMemory),
                TelemetryCursor = vault.EnsureGenerationHandle<int>(
                    BufferID.ShinobuSpatialGridTelemetryCursor,
                    1,
                    SystemID.AIEcology,
                    NativeArrayOptions.ClearMemory)
            };
        }

        private static void SeedSpatialGrid(GlobalDataVault vault, in HarnessHandles handles)
        {
            bool entriesLocked = false;
            bool rangesLocked = false;
            bool aupsLocked = false;
            try
            {
                entriesLocked = vault.TryAcquireWriteLock(in handles.Entries, SystemID.AIEcology, out NativeArray<SpatialGridEntryDTO> entries);
                rangesLocked = vault.TryAcquireWriteLock(in handles.Ranges, SystemID.AIEcology, out NativeArray<SpatialGridBucketRangeDTO> ranges);
                aupsLocked = vault.TryAcquireWriteLock(in handles.Aups, SystemID.AIEcology, out NativeArray<AmbientEntityAupDTO> aups);
                Assert.IsTrue(entriesLocked && rangesLocked && aupsLocked);

                SpatialGridCell64 cell = ShinobuSpatialGridMath.QuantizeCell(double3.zero, CellSizeMeters);
                uint2 fingerprint = ShinobuSpatialGridMath.FingerprintCell(
                    in cell,
                    ShinobuSpatialGridConstants.DefaultHashMultiplierX,
                    ShinobuSpatialGridConstants.DefaultHashMultiplierY,
                    ShinobuSpatialGridConstants.DefaultHashMultiplierZ);
                uint hash = ShinobuSpatialGridMath.HashCellFromFingerprint(fingerprint);
                int bucketSlot = (int)(hash & (uint)(BucketRangeCapacity - 1));
                ranges[bucketSlot] = new SpatialGridBucketRangeDTO
                {
                    CellHash = hash,
                    CellFingerprintX = fingerprint.x,
                    CellFingerprintY = fingerprint.y,
                    StartIndex = 0,
                    Count = EntityCount,
                    Flags = Frame
                };

                for (int i = 0; i < EntityCount; i++)
                {
                    entries[i] = new SpatialGridEntryDTO
                    {
                        EntityRowIndex = (uint)i,
                        CellHash = hash,
                        CellFingerprint = fingerprint
                    };

                    double x = ((i & 31) - 16) * 0.125d;
                    double z = (((i >> 5) & 31) - 16) * 0.125d;
                    aups[i] = new AmbientEntityAupDTO
                    {
                        PositionAup = AbsoluteUniversePosition.FromAbsolutePosition(new double3(x, 0d, z)),
                        Flags = EntityFlagActive | EntityFlagHydrated,
                        StableSeed = (uint)(i + 1)
                    };
                }
            }
            finally
            {
                if (aupsLocked) vault.ReleaseWriteLock(in handles.Aups, SystemID.AIEcology);
                if (rangesLocked) vault.ReleaseWriteLock(in handles.Ranges, SystemID.AIEcology);
                if (entriesLocked) vault.ReleaseWriteLock(in handles.Entries, SystemID.AIEcology);
            }
        }

        private static SpatialHashQuery CreateQuery(in HarnessHandles handles)
        {
            return new SpatialHashQuery
            {
                CenterAbsolute = double3.zero,
                EntriesHandle = handles.Entries,
                BucketRangesHandle = handles.Ranges,
                AupSnapshotHandle = handles.Aups,
                TelemetryHandle = handles.Telemetry,
                TelemetryCursorHandle = handles.TelemetryCursor,
                EntryCount = EntityCount,
                BucketMask = BucketRangeCapacity - 1,
                Frame = Frame,
                CellSizeMeters = CellSizeMeters,
                HashMultiplierX = ShinobuSpatialGridConstants.DefaultHashMultiplierX,
                HashMultiplierY = ShinobuSpatialGridConstants.DefaultHashMultiplierY,
                HashMultiplierZ = ShinobuSpatialGridConstants.DefaultHashMultiplierZ,
                MaxResults = QueryResultCapacity,
                MaxProbeCount = 1
            };
        }

        private static uint Mix(uint hash, uint value)
        {
            hash ^= value;
            return hash * 16777619u;
        }

        private static unsafe int ByteOffset<TStruct, TField>(ref TStruct owner, ref TField field)
            where TStruct : struct
            where TField : struct
        {
            return (int)((byte*)UnsafeUtility.AddressOf(ref field) - (byte*)UnsafeUtility.AddressOf(ref owner));
        }
    }
}
