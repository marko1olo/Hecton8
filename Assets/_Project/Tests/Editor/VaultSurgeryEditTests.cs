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
        public void VaultGenerationHandle_TryResolveHandle_MutatesInPlace()
        {
            using (GlobalDataVault vault = GlobalDataVault.Create(32))
            {
                VaultGenerationHandle<int> handle = vault.EnsureGenerationHandle<int>(
                    BufferID.VaultEntityBucketMap,
                    4,
                    SystemID.CoreDataVault,
                    NativeArrayOptions.ClearMemory);

                Assert.IsTrue(vault.TryResolveHandle(in handle, out NativeArray<int> buffer));
                buffer[2] = 77;

                Assert.IsTrue(vault.TryReadHandle(in handle, out NativeArray<int> readback));
                Assert.AreEqual(77, readback[2]);
            }
        }

        [Test]
        public void VaultGenerationHandle_ReleaseBuffer_InvalidatesOldDescriptor()
        {
            using (GlobalDataVault vault = GlobalDataVault.Create(32))
            {
                VaultGenerationHandle<TestEntity64> handle = vault.EnsureGenerationHandle<TestEntity64>(
                    BufferID.VaultHotEntityData,
                    2,
                    SystemID.CoreDataVault,
                    NativeArrayOptions.ClearMemory);

                Assert.IsTrue(vault.TryResolveHandle(in handle, out NativeArray<TestEntity64> buffer));
                buffer[1] = new TestEntity64 { A = 10L, B = 20L, C = 30 };

                Assert.IsTrue(vault.ReleaseBuffer(in handle));
                Assert.IsFalse(vault.TryReadHandle(in handle, out _));
            }
        }

        [Test]
        public void AlignmentTelemetry_DisposeDetachesOwnerAndStaleVaultFailsClosed()
        {
            string dumpPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Docs",
                "AgentLogs",
                "Dump_SHINOBU_204.bin");
            byte[] previousDump = File.Exists(dumpPath) ? File.ReadAllBytes(dumpPath) : null;
            GlobalDataVault vault = null;
            try
            {
                vault = GlobalDataVault.Create(32);
                Assert.IsTrue(Arm64AlignmentTelemetry.TryRecordFault(
                    vault,
                    BufferID.VaultHotEntityData,
                    0xA11A64UL,
                    8u,
                    11u,
                    AlignmentTelemetryFlags.MisalignedEightByteField,
                    new double3(1.0d, 2.0d, 3.0d)));
                Assert.IsTrue(Arm64AlignmentTelemetry.TryGetNewestFault(vault, out AlignmentTelemetryEntry newest));
                Assert.AreEqual(11u, newest.Frame);

                vault.Dispose();

                Assert.IsFalse(Arm64AlignmentTelemetry.TryRecordFault(
                    vault,
                    BufferID.VaultHotEntityData,
                    0xA11A65UL,
                    16u,
                    12u,
                    AlignmentTelemetryFlags.DynamicCastFault,
                    new double3(4.0d, 5.0d, 6.0d)));
                Assert.IsFalse(vault.TryAcquireMutationGuard(1UL));
                Assert.IsFalse(vault.TryGetGenerationHandle<AlignmentTelemetryEntry>(
                    BufferID.Arm64AlignmentTelemetryRing,
                    out _));
                Assert.IsNull(ReadPrivateStaticField<IDataVault>(typeof(Arm64AlignmentTelemetry), "_ringVault"));
                vault = null;

                using (GlobalDataVault replacement = GlobalDataVault.Create(32))
                {
                    Assert.IsTrue(Arm64AlignmentTelemetry.TryRecordFault(
                        replacement,
                        BufferID.VaultColdEntityData,
                        0xA11A66UL,
                        24u,
                        13u,
                        AlignmentTelemetryFlags.InvalidStride,
                        new double3(7.0d, 8.0d, 9.0d)));
                    Assert.IsTrue(Arm64AlignmentTelemetry.TryGetNewestFault(replacement, out AlignmentTelemetryEntry replacementNewest));
                    Assert.AreEqual(13u, replacementNewest.Frame);
                }
            }
            finally
            {
                if (vault != null)
                    vault.Dispose();
                RestoreFileBytes(dumpPath, previousDump);
            }
        }

        [Test]
        public void GlobalRegistryDataVaultUnregister_DetachesAlignmentTelemetryBeforeServiceClear()
        {
            string registry = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "Core",
                "GlobalRegistry.cs"));
            string unregister = ExtractMethodBlock(registry, "public static void UnregisterDataVault(IDataVault instance)");

            StringAssert.Contains("Arm64AlignmentTelemetry.ReleaseOwnedBuffers(instance);", unregister);
            StringAssert.Contains("BulkheadContainmentIntentBus.UnbindDataVault(instance);", unregister);
            Assert.Less(
                unregister.IndexOf("Arm64AlignmentTelemetry.ReleaseOwnedBuffers(instance);", StringComparison.Ordinal),
                unregister.IndexOf("UnregisterService(ref _dataVault, instance);", StringComparison.Ordinal));
            Assert.Less(
                unregister.IndexOf("BulkheadContainmentIntentBus.UnbindDataVault(instance);", StringComparison.Ordinal),
                unregister.IndexOf("UnregisterService(ref _dataVault, instance);", StringComparison.Ordinal));
        }

        [Test]
        public void GlobalRegistryStaticReset_ClearsBulkheadIntentBusCache()
        {
            string registry = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "Core",
                "GlobalRegistry.cs"));
            string reset = ExtractMethodBlock(registry, "private static void ResetStaticState()");

            StringAssert.Contains("BulkheadContainmentIntentBus.UnbindDataVault(null);", reset);
        }

        [Test]
        public void BulkheadIntentBus_UnbindClearsOnlyMatchingOwnerAndFailsClosed()
        {
            BulkheadContainmentIntentBus.UnbindDataVault(null);
            using (GlobalDataVault owner = GlobalDataVault.Create(32))
            using (GlobalDataVault unrelated = GlobalDataVault.Create(32))
            {
                owner.EnsureGenerationHandle<BulkheadContainmentIntentDTO>(
                    BufferID.Shinobu220BulkheadIntentRing,
                    BulkheadContainmentIntentBus.IntentCapacity,
                    SystemID.CoreDataVault,
                    NativeArrayOptions.ClearMemory);
                owner.EnsureGenerationHandle<BulkheadContainmentIntentControlDTO>(
                    BufferID.Shinobu220BulkheadIntentControl,
                    1,
                    SystemID.CoreDataVault,
                    NativeArrayOptions.ClearMemory);

                BulkheadContainmentIntentBus.BindDataVault(owner);
                Assert.AreSame(owner, ReadPrivateStaticField<IDataVault>(
                    typeof(BulkheadContainmentIntentBus),
                    "s_cachedVault"));

                BulkheadContainmentIntentBus.UnbindDataVault(unrelated);
                Assert.AreSame(owner, ReadPrivateStaticField<IDataVault>(
                    typeof(BulkheadContainmentIntentBus),
                    "s_cachedVault"));

                BulkheadContainmentIntentBus.UnbindDataVault(owner);
                Assert.IsNull(ReadPrivateStaticField<IDataVault>(
                    typeof(BulkheadContainmentIntentBus),
                    "s_cachedVault"));
                Assert.IsFalse(BulkheadContainmentIntentBus.TryWriteAirlockBulkheadIntent(
                    0xB011u,
                    true,
                    new double3(1.0d, 2.0d, 3.0d),
                    new float3(0f, 0f, 1f),
                    2.5f,
                    3.0f,
                    1.0f,
                    0u,
                    42u));
            }
        }

        [Test]
        public void AlignmentTelemetry_InterruptedInitializationReleasesOldVaultOnReplacement()
        {
            string dumpPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Docs",
                "AgentLogs",
                "Dump_SHINOBU_204.bin");
            byte[] previousDump = File.Exists(dumpPath) ? File.ReadAllBytes(dumpPath) : null;
            GlobalDataVault interruptedVault = null;
            try
            {
                interruptedVault = GlobalDataVault.Create(32);
                ulong telemetryGuardMask = MutationGuardBit(BufferID.Arm64AlignmentTelemetryRing) |
                                           MutationGuardBit(BufferID.Arm64AlignmentTelemetryCursor);
                Assert.IsTrue(interruptedVault.TryAcquireMutationGuard(telemetryGuardMask));
                try
                {
                    Assert.IsFalse(Arm64AlignmentTelemetry.TryRecordFault(
                        interruptedVault,
                        BufferID.VaultHotEntityData,
                        0xA11A67UL,
                        32u,
                        14u,
                        AlignmentTelemetryFlags.DynamicCastFault,
                        new double3(10.0d, 11.0d, 12.0d)));
                    Assert.IsTrue(interruptedVault.TryGetGenerationHandle<AlignmentTelemetryEntry>(
                        BufferID.Arm64AlignmentTelemetryRing,
                        out _));
                }
                finally
                {
                    interruptedVault.ReleaseMutationGuard(telemetryGuardMask);
                }

                using (GlobalDataVault replacement = GlobalDataVault.Create(32))
                {
                    Assert.IsTrue(Arm64AlignmentTelemetry.TryRecordFault(
                        replacement,
                        BufferID.VaultColdEntityData,
                        0xA11A68UL,
                        40u,
                        15u,
                        AlignmentTelemetryFlags.InvalidStride,
                        new double3(13.0d, 14.0d, 15.0d)));
                }

                Assert.IsFalse(interruptedVault.TryGetGenerationHandle<AlignmentTelemetryEntry>(
                    BufferID.Arm64AlignmentTelemetryRing,
                    out _));
            }
            finally
            {
                if (interruptedVault != null)
                    interruptedVault.Dispose();
                RestoreFileBytes(dumpPath, previousDump);
            }
        }

        [Test]
        public void TryAcquireSliceHandle_ReturnsPrimaryWritableSlice()
        {
            using (GlobalDataVault vault = GlobalDataVault.Create(32))
            {
                Assert.IsTrue(vault.TryAcquireSliceHandle<int>(
                    BufferID.VaultEntityBucketMap,
                    16,
                    4,
                    4,
                    SystemID.CoreDataVault,
                    out VaultSliceHandle<int> slice));

                Assert.IsTrue(vault.TryResolveSlice(in slice, out NativeArray<int> sliceView));
                sliceView[1] = 1234;

                Assert.IsTrue(vault.TryGetGenerationHandle(BufferID.VaultEntityBucketMap, out VaultGenerationHandle<int> handle));
                Assert.IsTrue(vault.TryReadHandle(in handle, out NativeArray<int> buffer));
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
            Assert.AreEqual(16, UnsafeUtility.SizeOf<VaultGenerationHandle<byte>>());
            Assert.AreEqual(32, UnsafeUtility.SizeOf<VaultSliceHandle<byte>>());
            Assert.AreEqual(VaultBufferContract.LayoutConfigSizeBytes, UnsafeUtility.SizeOf<VaultMemoryLayoutConfig>());
            Assert.AreEqual(VaultBufferContract.HotEntitySizeBytes, UnsafeUtility.SizeOf<VaultHotEntityData>());
            Assert.AreEqual(VaultBufferContract.ColdEntitySizeBytes, UnsafeUtility.SizeOf<VaultColdEntityData>());
            Assert.AreEqual(VaultBufferContract.Aup64SizeBytes, UnsafeUtility.SizeOf<VaultAup64>());
            Assert.AreEqual(VaultBufferContract.TransformAliasSizeBytes, UnsafeUtility.SizeOf<VaultTransformAlias>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<VaultBufferContract>());
            Assert.AreEqual(32, UnsafeUtility.SizeOf<VaultRelocationRecord>());
            Assert.AreEqual(48, UnsafeUtility.SizeOf<VaultMemoryBlockSnapshot>());
            Type blockDescriptorType = typeof(H8Memory).Assembly.GetType("Hecton8.Core.Memory.BlockDescriptor", true);
            Type allocationRecordType = typeof(H8Memory).Assembly.GetType("Hecton8.Core.Memory.H8AllocationRecord", true);
            Assert.AreEqual(40, Marshal.SizeOf(blockDescriptorType));
            Assert.AreEqual(48, Marshal.SizeOf(allocationRecordType));
            Assert.AreEqual(64, UnsafeUtility.SizeOf<H8MemoryTelemetryEntry>());
            StructLayoutAttribute arenaSliceLayout = typeof(HectonArenaAllocator.NativeArenaSlice<byte>).StructLayoutAttribute;
            Assert.IsNotNull(arenaSliceLayout);
            Assert.AreEqual(32, arenaSliceLayout.Size);
            Assert.AreEqual(0, UnsafeUtility.SizeOf<VaultHotEntityData>() & 7);
            Assert.AreEqual(0, UnsafeUtility.SizeOf<VaultColdEntityData>() & 7);
            Assert.AreEqual(0, UnsafeUtility.SizeOf<VaultAup64>() & 7);
            Assert.AreEqual(0, UnsafeUtility.SizeOf<VaultAupSectorLocal32>() & 7);
            Assert.AreEqual(0, UnsafeUtility.SizeOf<VaultSovereigntyTelemetryEntry>() & 7);
            Assert.AreEqual(0, UnsafeUtility.SizeOf<VaultMemoryAddressShiftRecord>() & 7);
            Assert.AreEqual(0, UnsafeUtility.SizeOf<VaultRelocationRecord>() & 7);
            Assert.AreEqual(0, UnsafeUtility.SizeOf<VaultMemoryBlockSnapshot>() & 7);
            Assert.AreEqual(0, Marshal.SizeOf(blockDescriptorType) & 7);
            Assert.AreEqual(0, Marshal.SizeOf(allocationRecordType) & 7);
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
            Assert.AreEqual(0, OffsetOf<VaultGenerationHandle<byte>>(nameof(VaultGenerationHandle<byte>.BufferID)));
            Assert.AreEqual(4, OffsetOf<VaultGenerationHandle<byte>>(nameof(VaultGenerationHandle<byte>.SystemID)));
            Assert.AreEqual(8, OffsetOf<VaultGenerationHandle<byte>>(nameof(VaultGenerationHandle<byte>.Generation)));
            Assert.AreEqual(12, OffsetOf<VaultGenerationHandle<byte>>(nameof(VaultGenerationHandle<byte>.Flags)));
            Assert.AreEqual(0, OffsetOf<VaultSliceHandle<byte>>(nameof(VaultSliceHandle<byte>.BufferID)));
            Assert.AreEqual(4, OffsetOf<VaultSliceHandle<byte>>(nameof(VaultSliceHandle<byte>.SystemID)));
            Assert.AreEqual(8, OffsetOf<VaultSliceHandle<byte>>(nameof(VaultSliceHandle<byte>.Generation)));
            Assert.AreEqual(12, OffsetOf<VaultSliceHandle<byte>>(nameof(VaultSliceHandle<byte>.HandleFlags)));
            Assert.AreEqual(16, OffsetOf<VaultSliceHandle<byte>>(nameof(VaultSliceHandle<byte>.StartIndex)));
            Assert.AreEqual(20, OffsetOf<VaultSliceHandle<byte>>(nameof(VaultSliceHandle<byte>.Length)));
            Assert.AreEqual(24, OffsetOf<VaultSliceHandle<byte>>(nameof(VaultSliceHandle<byte>.Flags)));
            Assert.AreEqual(28, OffsetOf<VaultSliceHandle<byte>>(nameof(VaultSliceHandle<byte>.Reserved0)));

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

            Assert.AreEqual(VaultBufferContract.TransformAliasMatrixBufferIdOffset, OffsetOf<VaultTransformAlias>(nameof(VaultTransformAlias.MatrixBufferId)));
            Assert.AreEqual(VaultBufferContract.TransformAliasMatrixOffsetBytesOffset, OffsetOf<VaultTransformAlias>(nameof(VaultTransformAlias.MatrixOffsetBytes)));
            Assert.AreEqual(VaultBufferContract.TransformAliasMatrixGenerationOffset, OffsetOf<VaultTransformAlias>(nameof(VaultTransformAlias.MatrixGeneration)));
            Assert.AreEqual(VaultBufferContract.TransformAliasTransformHashOffset, OffsetOf<VaultTransformAlias>(nameof(VaultTransformAlias.TransformHash)));
            Assert.AreEqual(VaultBufferContract.TransformAliasEntityIdOffset, OffsetOf<VaultTransformAlias>(nameof(VaultTransformAlias.EntityId)));
            Assert.AreEqual(VaultBufferContract.TransformAliasFlagsOffset, OffsetOf<VaultTransformAlias>(nameof(VaultTransformAlias.Flags)));

            Assert.AreEqual(0, OffsetOf<VaultRelocationRecord>(nameof(VaultRelocationRecord.OldOffsetBytes)));
            Assert.AreEqual(8, OffsetOf<VaultRelocationRecord>(nameof(VaultRelocationRecord.NewOffsetBytes)));
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
            Type blockDescriptorType = typeof(H8Memory).Assembly.GetType("Hecton8.Core.Memory.BlockDescriptor", true);
            Type allocationRecordType = typeof(H8Memory).Assembly.GetType("Hecton8.Core.Memory.H8AllocationRecord", true);
            Assert.AreEqual(0, OffsetOf(blockDescriptorType, "BasePointer"));
            Assert.AreEqual(8, OffsetOf(blockDescriptorType, "OffsetBytes"));
            Assert.AreEqual(16, OffsetOf(blockDescriptorType, "Bytes"));
            Assert.AreEqual(24, OffsetOf(blockDescriptorType, "OwnerKey"));
            Assert.AreEqual(28, OffsetOf(blockDescriptorType, "Generation"));
            Assert.AreEqual(32, OffsetOf(blockDescriptorType, "Owner"));
            Assert.AreEqual(34, OffsetOf(blockDescriptorType, "Flags"));
            Assert.AreEqual(36, OffsetOf(blockDescriptorType, "Reserved2"));
            Assert.AreEqual(38, OffsetOf(blockDescriptorType, "State"));
            Assert.AreEqual(39, OffsetOf(blockDescriptorType, "Reserved"));
            Assert.AreEqual(0, OffsetOf(allocationRecordType, "Pointer"));
            Assert.AreEqual(8, OffsetOf(allocationRecordType, "Bytes"));
            Assert.AreEqual(16, OffsetOf(allocationRecordType, "Length"));
            Assert.AreEqual(36, OffsetOf(allocationRecordType, "Allocator"));
            Assert.AreEqual(40, OffsetOf(allocationRecordType, "Owner"));
            Assert.AreEqual(0, OffsetOf<H8MemoryTelemetryEntry>(nameof(H8MemoryTelemetryEntry.TotalBytes)));
            Assert.AreEqual(24, OffsetOf<H8MemoryTelemetryEntry>(nameof(H8MemoryTelemetryEntry.Sequence)));
            Assert.AreEqual(52, OffsetOf<H8MemoryTelemetryEntry>(nameof(H8MemoryTelemetryEntry.FatalLeakPreventedCount)));
            Assert.AreEqual(56, OffsetOf<H8MemoryTelemetryEntry>(nameof(H8MemoryTelemetryEntry.Frame)));
            Assert.AreEqual(60, OffsetOf<H8MemoryTelemetryEntry>(nameof(H8MemoryTelemetryEntry.Owner)));
            Assert.AreEqual(62, OffsetOf<H8MemoryTelemetryEntry>(nameof(H8MemoryTelemetryEntry.Flags)));
        }

        [Test]
        public void VaultInternalRuntimeStructs_KeepExpectedNaturalOffsets()
        {
            global::System.Reflection.Assembly memoryAssembly = typeof(GlobalDataVault).Assembly;

            AssertLayout(
                memoryAssembly.GetType("Hecton8.Core.Memory.VaultBufferMeta", true),
                64,
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
                ("ActiveWriterSystemID", 44),
                ("TypeHash", 48),
                ("RefCount", 52),
                ("Flags", 56),
                ("BufferKey", 60));

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
                ("ReservedCursorBytes", 76),
                ("HeapFragmentationRatio", 80),
                ("MemoryStarvationWarnings", 91),
                ("GenerationMismatchCount", 92),
                ("ResolutionTicks", 96),
                ("ResolvedHandleCount", 104),
                ("LastFaultBufferID", 112),
                ("LastFaultHandleGeneration", 116),
                ("LastFaultMetaGeneration", 120),
                ("Reserved32", 124));

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
                32,
                ("Ptr", 0),
                ("ByteCount", 8),
                ("ArenaIndex", 12),
                ("SlabIndex", 16),
                ("FrameSequence", 20),
                ("_pad0", 24));
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

                VaultAup64 cameraAup = new VaultAup64
                {
                    SectorX = 1L,
                    SectorY = 4L,
                    SectorZ = -2L,
                    LocalX = 2.0d,
                    LocalY = 0.125d,
                    LocalZ = 7.0d
                };
                VaultHotEntityData hotRow = default;
                VaultAup64 entityAup = aups[0];
                hotRow.LocalPosition = VaultMemoryMath.ResolveCameraRelativeLocal(in entityAup, in cameraAup);
                hotRow.ShiftFrameId = 17u;
                hotRow.SimulationBucket = VaultMemoryMath.ResolveSimulationBucket(in entityAup);
                SetNativeArrayElement(hot, 0, hotRow);

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
                Assert.IsTrue(vault.TryGetGenerationHandle(BufferID.VaultMemoryLayoutConfig, out VaultGenerationHandle<VaultMemoryLayoutConfig> handle));
                Assert.IsTrue(vault.TryReadHandle(in handle, out NativeArray<VaultMemoryLayoutConfig> storedBuffer));
                VaultMemoryLayoutConfig stored = storedBuffer[0];
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
                Assert.IsTrue(vault.TryGetGenerationHandle(BufferID.VaultMemoryLayoutConfig, out VaultGenerationHandle<VaultMemoryLayoutConfig> handle));

                Assert.IsTrue(vault.TryReadHandle(in handle, out NativeArray<VaultMemoryLayoutConfig> storedBuffer));
                VaultMemoryLayoutConfig stored = storedBuffer[0];
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

        private static int OffsetOf(Type type, string fieldName)
        {
            return Marshal.OffsetOf(type, fieldName).ToInt32();
        }

        private static T ReadPrivateStaticField<T>(Type type, string fieldName)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(field, fieldName);
            return (T)field.GetValue(null);
        }

        private static void RestoreFileBytes(string path, byte[] bytes)
        {
            if (bytes == null)
            {
                if (File.Exists(path))
                    File.Delete(path);
                return;
            }

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllBytes(path, bytes);
        }

        private static string ExtractMethodBlock(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, signature);
            int brace = source.IndexOf('{', start);
            Assert.GreaterOrEqual(brace, 0, signature);

            int depth = 0;
            for (int i = brace; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(start, i - start + 1);
                }
            }

            Assert.Fail(signature);
            return string.Empty;
        }

        private static ulong MutationGuardBit(BufferID bufferId)
        {
            int bitIndex = unchecked((int)((uint)(int)bufferId & 63u));
            return 1UL << bitIndex;
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
