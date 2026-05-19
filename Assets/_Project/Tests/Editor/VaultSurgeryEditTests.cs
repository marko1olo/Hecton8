using System;
using System.Buffers.Binary;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Tests.Editor
{
    public sealed class VaultSurgeryEditTests
    {
        [Test]
        public void VaultBufferHandle_GetElementAsRef_MutatesInPlace()
        {
            using (GlobalDataVault vault = GlobalDataVault.Create(32))
            {
                VaultBufferHandle<int> handle = vault.GetBufferHandle<int>(
                    BufferID.VaultEntityBucketMap,
                    4,
                    SystemID.CoreDataVault,
                    NativeArrayOptions.ClearMemory);

                ref int value = ref handle.GetElementAsRef(vault, 2);
                value = 77;

                Assert.IsTrue(vault.TryGetBuffer(BufferID.VaultEntityBucketMap, out NativeArray<int> buffer));
                Assert.AreEqual(77, buffer[2]);
            }
        }

        [Test]
        public void VaultBufferHandle_Tombstone_ClearsStableSlotAndAliveBit()
        {
            using (GlobalDataVault vault = GlobalDataVault.Create(32))
            {
                VaultBufferHandle<TestEntity64> handle = vault.GetBufferHandle<TestEntity64>(
                    BufferID.VaultHotEntityData,
                    2,
                    SystemID.CoreDataVault,
                    NativeArrayOptions.ClearMemory);

                ref TestEntity64 entity = ref handle.GetElementAsRef(vault, 1);
                entity.A = 10L;
                entity.B = 20L;
                entity.C = 30;
                ulong aliveMask = ulong.MaxValue;

                Assert.IsTrue(handle.TryTombstoneElement(vault, 1, ref aliveMask));
                ref readonly TestEntity64 cleared = ref handle.GetElementAsReadOnlyRef(vault, 1);
                Assert.AreEqual(0L, cleared.A);
                Assert.AreEqual(0L, cleared.B);
                Assert.AreEqual(0, cleared.C);
                Assert.AreEqual(0UL, aliveMask & (1UL << 1));
            }
        }

        [Test]
        public void TryAcquireSlice_ReturnsPrimaryWritableSlice()
        {
            using (GlobalDataVault vault = GlobalDataVault.Create(32))
            {
                Assert.IsTrue(vault.TryAcquireSlice<int>(
                    BufferID.VaultEntityBucketMap,
                    16,
                    4,
                    4,
                    SystemID.CoreDataVault,
                    out VaultBufferSlice<int> slice));

                ref int slot = ref slice.GetElementAsRef(1);
                slot = 1234;

                Assert.IsTrue(vault.TryGetBuffer(BufferID.VaultEntityBucketMap, out NativeArray<int> buffer));
                Assert.AreEqual(1234, buffer[5]);
            }
        }

        [Test]
        public void NativeArenaArray_GetElementAsRef_MutatesInPlace()
        {
            try
            {
                HectonArenaAllocator.Initialize(1024 * 1024);
                Assert.IsTrue(HectonArenaAllocator.TryAllocateNativeArenaArray<int>(
                    4,
                    true,
                    out NativeArenaArray<int> array));

                ref int slot = ref array.GetElementAsRef(2);
                slot = 44;

                ref readonly int readBack = ref array.GetElementAsReadOnlyRef(2);
                int value = readBack;
                Assert.AreEqual(44, value);
            }
            finally
            {
                HectonArenaAllocator.Shutdown();
            }
        }

        [Test]
        public void VaultRuntimeStructs_KeepExpectedNaturalSizes()
        {
            Assert.AreEqual(24, UnsafeUtility.SizeOf<VaultBufferHandle<byte>>());
            Assert.AreEqual(32, UnsafeUtility.SizeOf<VaultBufferSlice<byte>>());
            Assert.AreEqual(VaultBufferContract.LayoutConfigSizeBytes, UnsafeUtility.SizeOf<VaultMemoryLayoutConfig>());
            Assert.AreEqual(VaultBufferContract.HotEntitySizeBytes, UnsafeUtility.SizeOf<VaultHotEntityData>());
            Assert.AreEqual(VaultBufferContract.ColdEntitySizeBytes, UnsafeUtility.SizeOf<VaultColdEntityData>());
            Assert.AreEqual(VaultBufferContract.Aup64SizeBytes, UnsafeUtility.SizeOf<VaultAup64>());
            Assert.AreEqual(VaultBufferContract.TransformAliasSizeBytes, UnsafeUtility.SizeOf<VaultTransformAlias>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<VaultBufferContract>());
            Assert.AreEqual(32, UnsafeUtility.SizeOf<VaultRelocationRecord>());
            Assert.AreEqual(48, UnsafeUtility.SizeOf<VaultMemoryBlockSnapshot>());
            Assert.AreEqual(40, UnsafeUtility.SizeOf<BlockDescriptor>());
            Assert.AreEqual(48, UnsafeUtility.SizeOf<H8AllocationRecord>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<H8MemoryTelemetryEntry>());
            Assert.AreEqual(32, UnsafeUtility.SizeOf<HectonArenaAllocator.NativeArenaSlice<byte>>());
            Assert.AreEqual(0, UnsafeUtility.SizeOf<VaultHotEntityData>() & 7);
            Assert.AreEqual(0, UnsafeUtility.SizeOf<VaultColdEntityData>() & 7);
            Assert.AreEqual(0, UnsafeUtility.SizeOf<VaultAup64>() & 7);
            Assert.AreEqual(0, UnsafeUtility.SizeOf<VaultAupSectorLocal32>() & 7);
            Assert.AreEqual(0, UnsafeUtility.SizeOf<VaultSovereigntyTelemetryEntry>() & 7);
            Assert.AreEqual(0, UnsafeUtility.SizeOf<VaultMemoryAddressShiftRecord>() & 7);
            Assert.AreEqual(0, UnsafeUtility.SizeOf<VaultRelocationRecord>() & 7);
            Assert.AreEqual(0, UnsafeUtility.SizeOf<VaultMemoryBlockSnapshot>() & 7);
            Assert.AreEqual(0, UnsafeUtility.SizeOf<BlockDescriptor>() & 7);
            Assert.AreEqual(0, UnsafeUtility.SizeOf<H8AllocationRecord>() & 7);
            Assert.AreEqual(0, UnsafeUtility.SizeOf<H8MemoryTelemetryEntry>() & 7);
            Assert.AreEqual((int)BufferID.VaultMemoryLayoutConfig, VaultBufferContract.LayoutConfigBufferId);
            Assert.AreEqual((int)BufferID.VaultHotEntityData, VaultBufferContract.HotEntityBufferId);
            Assert.AreEqual((int)BufferID.VaultColdEntityData, VaultBufferContract.ColdEntityBufferId);
            Assert.AreEqual((int)BufferID.VaultAup64, VaultBufferContract.Aup64BufferId);
            Assert.AreEqual((int)BufferID.VaultEntityBucketMap, VaultBufferContract.EntityBucketMapBufferId);
            Assert.AreEqual((int)BufferID.VaultSharedTransformMatrices, VaultBufferContract.SharedTransformMatricesBufferId);
            Assert.AreEqual((int)BufferID.VaultSovereigntyTelemetryRing, VaultBufferContract.TelemetryRingBufferId);
            Assert.AreEqual((int)BufferID.AcousticEchoPendingTaps, VaultBufferContract.AcousticEchoPendingTapsBufferId);
            Assert.AreEqual((int)BufferID.VaultAupSectorLocal32, VaultBufferContract.AupSectorLocal32BufferId);
            Assert.AreEqual((int)BufferID.VaultSovereigntyActiveEntityCount, VaultBufferContract.ActiveEntityCountBufferId);
            Assert.AreEqual((int)BufferID.VaultMemoryProfileCsvScratch, VaultBufferContract.CsvScratchBufferId);
            Assert.AreEqual((int)BufferID.VaultMemoryAddressShiftRecords, VaultBufferContract.AddressShiftRecordsBufferId);
            Assert.AreEqual((int)BufferID.VaultMemoryAddressShiftCount, VaultBufferContract.AddressShiftCountBufferId);
            Assert.AreEqual(16, VaultBufferContract.OwnedBufferCount);
            Assert.AreEqual((int)BufferID.VaultMemoryLayoutConfig, VaultBufferContract.MinBufferId);
            Assert.AreEqual((int)BufferID.VaultMemoryAddressShiftCount, VaultBufferContract.MaxBufferId);
            Assert.IsTrue(VaultBufferContract.OwnsBufferId(BufferID.VaultMemoryLayoutConfig));
            Assert.IsTrue(VaultBufferContract.OwnsBufferId(BufferID.VaultSharedTransformMatrices));
            Assert.IsTrue(VaultBufferContract.OwnsBufferId(BufferID.VaultMemoryAddressShiftCount));
            Assert.IsFalse(VaultBufferContract.OwnsBufferId(BufferID.WristHudState));
            Assert.IsFalse(VaultBufferContract.OwnsBufferId(BufferID.FloraGenomeCsvScratch));
        }

        [Test]
        public void VaultPrimaryDtoOffsets_AreArm64Aligned()
        {
            Assert.AreEqual(0, OffsetOf<VaultBufferHandle<byte>>(nameof(VaultBufferHandle<byte>.ptr)));
            Assert.AreEqual(8, OffsetOf<VaultBufferHandle<byte>>(nameof(VaultBufferHandle<byte>.generation)));
            Assert.AreEqual(12, OffsetOf<VaultBufferHandle<byte>>(nameof(VaultBufferHandle<byte>.BufferId)));
            Assert.AreEqual(16, OffsetOf<VaultBufferHandle<byte>>(nameof(VaultBufferHandle<byte>.Length)));
            Assert.AreEqual(20, OffsetOf<VaultBufferHandle<byte>>(nameof(VaultBufferHandle<byte>.Stride)));
            Assert.AreEqual(0, OffsetOf<VaultBufferSlice<byte>>(nameof(VaultBufferSlice<byte>.Ptr)));
            Assert.AreEqual(8, OffsetOf<VaultBufferSlice<byte>>(nameof(VaultBufferSlice<byte>.Generation)));
            Assert.AreEqual(12, OffsetOf<VaultBufferSlice<byte>>(nameof(VaultBufferSlice<byte>.BufferId)));
            Assert.AreEqual(16, OffsetOf<VaultBufferSlice<byte>>(nameof(VaultBufferSlice<byte>.StartIndex)));
            Assert.AreEqual(20, OffsetOf<VaultBufferSlice<byte>>(nameof(VaultBufferSlice<byte>.Length)));
            Assert.AreEqual(24, OffsetOf<VaultBufferSlice<byte>>(nameof(VaultBufferSlice<byte>.Stride)));
            Assert.AreEqual(28, OffsetOf<VaultBufferSlice<byte>>(nameof(VaultBufferSlice<byte>.Flags)));

            Assert.AreEqual(VaultBufferContract.LayoutConfigArenaLimitOffset, OffsetOf<VaultMemoryLayoutConfig>(nameof(VaultMemoryLayoutConfig.ArenaLimitBytes)));
            Assert.AreEqual(VaultBufferContract.LayoutConfigBufferCapacityOffset, OffsetOf<VaultMemoryLayoutConfig>(nameof(VaultMemoryLayoutConfig.BufferCapacity)));
            Assert.AreEqual(VaultBufferContract.LayoutConfigSourceHashOffset, OffsetOf<VaultMemoryLayoutConfig>(nameof(VaultMemoryLayoutConfig.SourceHash)));
            Assert.AreEqual(VaultBufferContract.LayoutConfigScalabilityProfileOffset, OffsetOf<VaultMemoryLayoutConfig>(nameof(VaultMemoryLayoutConfig.ScalabilityProfile)));

            Assert.AreEqual(VaultBufferContract.AupSectorXOffset, OffsetOf<VaultAup64>(nameof(VaultAup64.SectorX)));
            Assert.AreEqual(VaultBufferContract.AupSectorYOffset, OffsetOf<VaultAup64>(nameof(VaultAup64.SectorY)));
            Assert.AreEqual(VaultBufferContract.AupSectorZOffset, OffsetOf<VaultAup64>(nameof(VaultAup64.SectorZ)));
            Assert.AreEqual(VaultBufferContract.AupLocalXOffset, OffsetOf<VaultAup64>(nameof(VaultAup64.LocalX)));
            Assert.AreEqual(VaultBufferContract.AupLocalYOffset, OffsetOf<VaultAup64>(nameof(VaultAup64.LocalY)));
            Assert.AreEqual(VaultBufferContract.AupLocalZOffset, OffsetOf<VaultAup64>(nameof(VaultAup64.LocalZ)));

            Assert.AreEqual(VaultBufferContract.HotRotationOffset, OffsetOf<VaultHotEntityData>(nameof(VaultHotEntityData.Rotation)));
            Assert.AreEqual(VaultBufferContract.HotLocalPositionOffset, OffsetOf<VaultHotEntityData>(nameof(VaultHotEntityData.LocalPosition)));
            Assert.AreEqual(VaultBufferContract.HotVelocityOffset, OffsetOf<VaultHotEntityData>(nameof(VaultHotEntityData.Velocity)));
            Assert.AreEqual(VaultBufferContract.HotEntityIdOffset, OffsetOf<VaultHotEntityData>(nameof(VaultHotEntityData.EntityId)));
            Assert.AreEqual(VaultBufferContract.HotSimulationBucketOffset, OffsetOf<VaultHotEntityData>(nameof(VaultHotEntityData.SimulationBucket)));

            Assert.AreEqual(VaultBufferContract.ColdDisplayNameHashOffset, OffsetOf<VaultColdEntityData>(nameof(VaultColdEntityData.DisplayNameHash)));
            Assert.AreEqual(VaultBufferContract.ColdFactionMaskOffset, OffsetOf<VaultColdEntityData>(nameof(VaultColdEntityData.FactionMask)));
            Assert.AreEqual(VaultBufferContract.ColdEntityIdOffset, OffsetOf<VaultColdEntityData>(nameof(VaultColdEntityData.EntityId)));
            Assert.AreEqual(VaultBufferContract.ColdFlagsOffset, OffsetOf<VaultColdEntityData>(nameof(VaultColdEntityData.Flags)));

            Assert.AreEqual(VaultBufferContract.TransformAliasMatrixPointerOffset, OffsetOf<VaultTransformAlias>(nameof(VaultTransformAlias.MatrixPointer)));
            Assert.AreEqual(VaultBufferContract.TransformAliasTransformHashOffset, OffsetOf<VaultTransformAlias>(nameof(VaultTransformAlias.TransformHash)));
            Assert.AreEqual(VaultBufferContract.TransformAliasEntityIdOffset, OffsetOf<VaultTransformAlias>(nameof(VaultTransformAlias.EntityId)));
            Assert.AreEqual(VaultBufferContract.TransformAliasFlagsOffset, OffsetOf<VaultTransformAlias>(nameof(VaultTransformAlias.Flags)));

            Assert.AreEqual(0, OffsetOf<VaultRelocationRecord>(nameof(VaultRelocationRecord.OldPointer)));
            Assert.AreEqual(8, OffsetOf<VaultRelocationRecord>(nameof(VaultRelocationRecord.NewPointer)));
            Assert.AreEqual(16, OffsetOf<VaultRelocationRecord>(nameof(VaultRelocationRecord.BufferId)));
            Assert.AreEqual(20, OffsetOf<VaultRelocationRecord>(nameof(VaultRelocationRecord.ByteLength)));
            Assert.AreEqual(24, OffsetOf<VaultRelocationRecord>(nameof(VaultRelocationRecord.Generation)));
            Assert.AreEqual(28, OffsetOf<VaultRelocationRecord>(nameof(VaultRelocationRecord.Flags)));
            Assert.AreEqual(0, OffsetOf<VaultMemoryBlockSnapshot>(nameof(VaultMemoryBlockSnapshot.OffsetBytes)));
            Assert.AreEqual(8, OffsetOf<VaultMemoryBlockSnapshot>(nameof(VaultMemoryBlockSnapshot.Bytes)));
            Assert.AreEqual(16, OffsetOf<VaultMemoryBlockSnapshot>(nameof(VaultMemoryBlockSnapshot.BufferKey)));
            Assert.AreEqual(24, OffsetOf<VaultMemoryBlockSnapshot>(nameof(VaultMemoryBlockSnapshot.Version)));
            Assert.AreEqual(28, OffsetOf<VaultMemoryBlockSnapshot>(nameof(VaultMemoryBlockSnapshot.Owner)));
            Assert.AreEqual(40, OffsetOf<VaultMemoryBlockSnapshot>(nameof(VaultMemoryBlockSnapshot.Reserved2)));
            Assert.AreEqual(0, OffsetOf<BlockDescriptor>(nameof(BlockDescriptor.BasePointer)));
            Assert.AreEqual(8, OffsetOf<BlockDescriptor>(nameof(BlockDescriptor.OffsetBytes)));
            Assert.AreEqual(16, OffsetOf<BlockDescriptor>(nameof(BlockDescriptor.Bytes)));
            Assert.AreEqual(24, OffsetOf<BlockDescriptor>(nameof(BlockDescriptor.OwnerKey)));
            Assert.AreEqual(32, OffsetOf<BlockDescriptor>(nameof(BlockDescriptor.Owner)));
            Assert.AreEqual(36, OffsetOf<BlockDescriptor>(nameof(BlockDescriptor.State)));
            Assert.AreEqual(0, OffsetOf<H8AllocationRecord>(nameof(H8AllocationRecord.Pointer)));
            Assert.AreEqual(8, OffsetOf<H8AllocationRecord>(nameof(H8AllocationRecord.Bytes)));
            Assert.AreEqual(16, OffsetOf<H8AllocationRecord>(nameof(H8AllocationRecord.Length)));
            Assert.AreEqual(36, OffsetOf<H8AllocationRecord>(nameof(H8AllocationRecord.Allocator)));
            Assert.AreEqual(40, OffsetOf<H8AllocationRecord>(nameof(H8AllocationRecord.Owner)));
            Assert.AreEqual(0, OffsetOf<H8MemoryTelemetryEntry>(nameof(H8MemoryTelemetryEntry.TotalBytes)));
            Assert.AreEqual(24, OffsetOf<H8MemoryTelemetryEntry>(nameof(H8MemoryTelemetryEntry.Sequence)));
            Assert.AreEqual(52, OffsetOf<H8MemoryTelemetryEntry>(nameof(H8MemoryTelemetryEntry.FatalLeakPreventedCount)));
            Assert.AreEqual(56, OffsetOf<H8MemoryTelemetryEntry>(nameof(H8MemoryTelemetryEntry.Owner)));
            Assert.AreEqual(60, OffsetOf<H8MemoryTelemetryEntry>(nameof(H8MemoryTelemetryEntry.Frame)));
        }

        [Test]
        public void VaultInternalRuntimeStructs_KeepExpectedNaturalOffsets()
        {
            Assembly memoryAssembly = typeof(GlobalDataVault).Assembly;

            AssertLayout(
                memoryAssembly.GetType("Hecton8.Core.Memory.VaultBufferMeta", true),
                48,
                ("OffsetBytes", 0),
                ("Bytes", 8),
                ("Length", 16),
                ("Stride", 20),
                ("Alignment", 24),
                ("BlockIndex", 28),
                ("Allocator", 32),
                ("Version", 36),
                ("Owner", 40),
                ("LastAliasRequester", 42),
                ("Reserved0", 44));

            AssertLayout(
                memoryAssembly.GetType("Hecton8.Core.Memory.VaultArenaBlock", true),
                32,
                ("OffsetBytes", 0),
                ("Bytes", 8),
                ("BufferKey", 16),
                ("H8BlockIndex", 20),
                ("Version", 24),
                ("State", 28),
                ("Reserved0", 29),
                ("Reserved1", 30));

            AssertLayout(
                memoryAssembly.GetType("Hecton8.Core.Memory.MemoryDefragTelemetryEntry", true),
                128,
                ("TotalFreeSpaceBytes", 0),
                ("LargestContiguousBlockBytes", 8),
                ("LastMovedBytes", 16),
                ("TotalMovedBytes", 24),
                ("PendingMassiveMoveBytes", 32),
                ("ActiveMutationGuardMask", 40),
                ("Sequence", 48),
                ("EmergencyOverflowCursorBytes", 76),
                ("HeapFragmentationRatio", 80),
                ("MemoryStarvationWarnings", 91),
                ("Reserved32", 92),
                ("ReservedLong0", 96),
                ("ReservedLong3", 120));

            AssertLayout(
                typeof(HectonArenaAllocator.NativeArenaSlice<byte>),
                32,
                ("Ptr", 0),
                ("Length", 8),
                ("Stride", 12),
                ("ByteCount", 16),
                ("FrameSequence", 20),
                ("_pad0", 24));

            AssertLayout(
                typeof(HectonArenaAllocator).GetNestedType("ArenaAllocation", BindingFlags.NonPublic),
                24,
                ("Ptr", 0),
                ("ByteCount", 8),
                ("ArenaIndex", 12),
                ("SlabIndex", 16),
                ("FrameSequence", 20));
        }

        [Test]
        public void VaultAupLocalOffsetResolver_UsesSectorMetersBeforeFloatDowncast()
        {
            using (NativeArray<VaultAup64> aups = new NativeArray<VaultAup64>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory))
            using (NativeArray<VaultHotEntityData> hot = new NativeArray<VaultHotEntityData>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory))
            {
                SetNativeArrayElement(
                    aups,
                    0,
                    new VaultAup64
                    {
                        SectorX = 2L,
                        SectorY = 4L,
                        SectorZ = -1L,
                        LocalX = 10.0d,
                        LocalY = 0.25d,
                        LocalZ = -3.0d
                    });

                VaultAupLocalOffsetResolverJob job = new VaultAupLocalOffsetResolverJob
                {
                    EntityAups = aups,
                    HotEntities = hot,
                    CameraAup = new VaultAup64
                    {
                        SectorX = 1L,
                        SectorY = 4L,
                        SectorZ = -2L,
                        LocalX = 2.0d,
                        LocalY = 0.125d,
                        LocalZ = 7.0d
                    },
                    ShiftFrameId = 17u
                };

                job.Execute(0);

                VaultHotEntityData resolved = hot[0];
                Assert.AreEqual(5008f, resolved.LocalPosition.x, 0.001f);
                Assert.AreEqual(0.125f, resolved.LocalPosition.y, 0.0001f);
                Assert.AreEqual(4990f, resolved.LocalPosition.z, 0.001f);
                Assert.AreEqual(17u, resolved.ShiftFrameId);
                Assert.AreNotEqual(8f, resolved.LocalPosition.x, "Sector delta must be scaled by the AUP sector size before float downcast.");
            }
        }

        [Test]
        public void VaultAupLocalOffsetResolver_ClampsExtremeSectorDeltaWithoutLongOverflow()
        {
            VaultAup64 entity = new VaultAup64
            {
                SectorX = long.MaxValue,
                SectorY = 0L,
                SectorZ = long.MinValue,
                LocalX = 0.0d,
                LocalY = 0.0d,
                LocalZ = 0.0d
            };

            VaultAup64 camera = new VaultAup64
            {
                SectorX = long.MinValue,
                SectorY = 0L,
                SectorZ = long.MaxValue,
                LocalX = 0.0d,
                LocalY = 0.0d,
                LocalZ = 0.0d
            };

            float3 local = VaultMemoryMath.ResolveCameraRelativeLocal(in entity, in camera);

            Assert.IsTrue(math.all(math.isfinite(local)));
            Assert.AreEqual((float)HectonPhysicsContract.AupMaxFloatSafeMeters, local.x, 1f);
            Assert.AreEqual(-(float)HectonPhysicsContract.AupMaxFloatSafeMeters, local.z, 1f);
        }

        private static void SetNativeArrayElement<T>(NativeArray<T> array, int index, T value)
            where T : struct
        {
            array[index] = value;
        }

        [Test]
        public void LegacyArchaeology_MissingFile_WritesMockConfig()
        {
            string root = Path.Combine(Path.GetTempPath(), "h8_vault_archaeology_missing");
            if (Directory.Exists(root))
                Directory.Delete(root, true);
            Directory.CreateDirectory(root);

            using (GlobalDataVault vault = GlobalDataVault.Create(32))
            {
                Assert.IsFalse(VaultLegacyBinaryArchaeology.TryBootstrapMemoryLayout(vault, root, 0, out VaultMemoryLayoutConfig config));
                Assert.AreEqual(0x4D4F434Bu, config.SourceHash);
                Assert.IsTrue(vault.TryGetBufferHandle(BufferID.VaultMemoryLayoutConfig, out VaultBufferHandle<VaultMemoryLayoutConfig> handle));
                ref readonly VaultMemoryLayoutConfig stored = ref handle.GetElementAsReadOnlyRef(vault, 0);
                Assert.AreEqual(0x4D4F434Bu, stored.SourceHash);
            }
        }

        [Test]
        public void LegacyArchaeology_LegacyHeader_UsesRawOffsets()
        {
            string root = Path.Combine(Path.GetTempPath(), "h8_vault_archaeology_legacy");
            if (Directory.Exists(root))
                Directory.Delete(root, true);
            string archive = Path.Combine(root, "Docs", "Archive", "Batch_006");
            Directory.CreateDirectory(archive);

            byte[] header = new byte[48];
            BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(0, 8), 0x4D454D4C41594F48UL);
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(8, 4), 7);
            BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(16, 8), 12345L);
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(24, 4), 256);
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(28, 4), 130);
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(32, 4), 145);
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(36, 4), 17);
            header[40] = 2;
            File.WriteAllBytes(Path.Combine(archive, "memory_layout_metrics.h8bin"), header);

            using (GlobalDataVault vault = GlobalDataVault.Create(32))
            {
                Assert.IsTrue(VaultLegacyBinaryArchaeology.TryBootstrapMemoryLayout(vault, root, 0, out VaultMemoryLayoutConfig config));
                Assert.AreEqual(0x4F53484Fu, config.SourceHash);
                Assert.AreEqual(7u, config.Version);
                Assert.AreEqual(12352L, config.ArenaLimitBytes);
                Assert.AreEqual(256, config.BufferCapacity);
                Assert.AreEqual(144, config.HotEntityCapacity);
                Assert.AreEqual(160, config.ColdEntityCapacity);
                Assert.AreEqual(17, config.BucketCapacity);
                Assert.AreEqual(2, config.ScalabilityProfile);
            }
        }

        [Test]
        public void LegacyArchaeology_CsvOverride_StreamsIntoVaultConfig()
        {
            string root = Path.Combine(Path.GetTempPath(), "h8_vault_archaeology_csv");
            if (Directory.Exists(root))
                Directory.Delete(root, true);
            Directory.CreateDirectory(root);

            string csv = Path.Combine(root, "memory_overrides.csv");
            File.WriteAllText(
                csv,
                "arena_limit_bytes,2097153\nbuffer_capacity,384\nhot_capacity,257\ncold_capacity,513\nbucket_capacity,32\nscalability_profile,3\n");

            using (GlobalDataVault vault = GlobalDataVault.Create(32))
            {
                VaultMemoryLayoutConfig initial = VaultMemoryMath.BuildMockConfig(0);
                VaultLegacyBinaryArchaeology.WriteMemoryLayoutConfig(vault, in initial);

                Assert.IsTrue(VaultLegacyBinaryArchaeology.TryApplyMemoryOverridesCsv(vault, csv));
                Assert.IsTrue(vault.TryGetBufferHandle(BufferID.VaultMemoryLayoutConfig, out VaultBufferHandle<VaultMemoryLayoutConfig> handle));

                ref readonly VaultMemoryLayoutConfig stored = ref handle.GetElementAsReadOnlyRef(vault, 0);
                Assert.AreEqual(0x4353564Fu, stored.SourceHash);
                Assert.AreEqual(2097168L, stored.ArenaLimitBytes);
                Assert.AreEqual(384, stored.BufferCapacity);
                Assert.AreEqual(272, stored.HotEntityCapacity);
                Assert.AreEqual(528, stored.ColdEntityCapacity);
                Assert.AreEqual(32, stored.BucketCapacity);
                Assert.AreEqual(3, stored.ScalabilityProfile);
            }
        }

        [StructLayout(LayoutKind.Sequential, Size = 64)]
        private struct TestEntity64
        {
            public long A;
            public long B;
            public int C;
        }

        private static int OffsetOf<T>(string fieldName)
        {
            return Marshal.OffsetOf<T>(fieldName).ToInt32();
        }

        private static void AssertLayout(Type type, int expectedSize, params (string Field, int Offset)[] offsets)
        {
            Assert.NotNull(type);
            Assert.AreEqual(expectedSize, Marshal.SizeOf(type));
            Assert.AreEqual(0, expectedSize & 7);

            for (int i = 0; i < offsets.Length; i++)
                Assert.AreEqual(offsets[i].Offset, Marshal.OffsetOf(type, offsets[i].Field).ToInt32(), offsets[i].Field);
        }
    }
}
