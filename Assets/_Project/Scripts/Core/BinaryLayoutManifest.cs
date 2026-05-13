using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Construction;
using Hecton8.Core.Signals;
using Hecton8.SaveSystem;
using Hecton8.World;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Cold-boot binary layout verifier for structs used by memcpy, save paging, AUP, and native telemetry lanes.
    /// </summary>
    public static class BinaryLayoutManifest
    {
        public static readonly bool IsLittleEndian = BitConverter.IsLittleEndian;

        private const uint LayoutRuleHash = 0x424C5954u; // BLYT
        private const uint LayoutSystemHash = 0x424C534Eu; // BLSN
        private const uint EndiannessContextHash = 0x454E444Eu; // ENDN
        private const uint SizeContextHash = 0x53495A45u; // SIZE
        private const uint OffsetContextHash = 0x4F464653u; // OFFS
        private const uint BlittableContextHash = 0x424C4954u; // BLIT
        private const uint AttributeContextHash = 0x41545452u; // ATTR
        private const uint DumpMagic = 0x4838424Cu; // H8BL
        private const int DumpVersion = 1;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_BINARY_LAYOUT_SENTINEL.bin";

        private static bool _verified;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForSubsystemRegistration()
        {
            _verified = false;
        }

        /// <summary>
        /// Verifies all batch-owned binary layouts once during the bootstrap memory prewarm phase.
        /// </summary>
        public static void VerifyColdBoot()
        {
            if (_verified)
                return;

            if (!IsLittleEndian)
                Fail("ENDIANNESS", expected: 1, observed: 0, EndiannessContextHash);

            VerifyAupLayouts();
            VerifySaveLayouts();
            VerifyPersistentWorldLayouts();
            VerifySignalLayouts();
            VerifyRenderBlitLayouts();

            _verified = true;
        }

        private static void VerifyAupLayouts()
        {
            AssertSize<AbsoluteUniversePosition>(48);
            AssertOffset<AbsoluteUniversePosition>(nameof(AbsoluteUniversePosition.GridX), 0);
            AssertOffset<AbsoluteUniversePosition>(nameof(AbsoluteUniversePosition.GridY), 8);
            AssertOffset<AbsoluteUniversePosition>(nameof(AbsoluteUniversePosition.GridZ), 16);
            AssertOffset<AbsoluteUniversePosition>(nameof(AbsoluteUniversePosition.LocalX), 24);
            AssertOffset<AbsoluteUniversePosition>(nameof(AbsoluteUniversePosition.LocalY), 28);
            AssertOffset<AbsoluteUniversePosition>(nameof(AbsoluteUniversePosition.LocalZ), 32);

            AssertSize<AbsoluteUniversePositionBlit>(48);
            AssertOffset<AbsoluteUniversePositionBlit>(nameof(AbsoluteUniversePositionBlit.GridX), 0);
            AssertOffset<AbsoluteUniversePositionBlit>(nameof(AbsoluteUniversePositionBlit.Local), 24);
            AssertOffset<AbsoluteUniversePositionBlit>(nameof(AbsoluteUniversePositionBlit.Reserved1), 40);

            AssertSize<AbsoluteUniversePositionBlit128>(48);
            AssertOffset<AbsoluteUniversePositionBlit128>(nameof(AbsoluteUniversePositionBlit128.GridX), 0);
            AssertOffset<AbsoluteUniversePositionBlit128>(nameof(AbsoluteUniversePositionBlit128.Local), 24);
            AssertOffset<AbsoluteUniversePositionBlit128>(nameof(AbsoluteUniversePositionBlit128.Reserved), 40);
        }

        private static void VerifySaveLayouts()
        {
            AssertSize<SaveVoxelDeltaRun5>(5);
            AssertOffset<SaveVoxelDeltaRun5>(nameof(SaveVoxelDeltaRun5.StartIndex), 0);
            AssertOffset<SaveVoxelDeltaRun5>(nameof(SaveVoxelDeltaRun5.SdfValue), 2);
            AssertOffset<SaveVoxelDeltaRun5>(nameof(SaveVoxelDeltaRun5.RunLength), 3);

            AssertSize<SaveVoxelDeltaRun8>(8);
            AssertOffset<SaveVoxelDeltaRun8>(nameof(SaveVoxelDeltaRun8.StartIndex), 0);
            AssertOffset<SaveVoxelDeltaRun8>(nameof(SaveVoxelDeltaRun8.RunLength), 2);
            AssertOffset<SaveVoxelDeltaRun8>(nameof(SaveVoxelDeltaRun8.SdfValue), 4);
            AssertOffset<SaveVoxelDeltaRun8>(nameof(SaveVoxelDeltaRun8.MaterialId), 5);
            AssertOffset<SaveVoxelDeltaRun8>(nameof(SaveVoxelDeltaRun8.Flags), 6);

            AssertSize<PackedEntityState32>(4);
            AssertSize<PackedSuitUpgradeState64>(8);
            AssertSize<QuantizedLocalHalf3>(6);
            AssertOffset<QuantizedLocalHalf3>(nameof(QuantizedLocalHalf3.X), 0);
            AssertOffset<QuantizedLocalHalf3>(nameof(QuantizedLocalHalf3.Y), 2);
            AssertOffset<QuantizedLocalHalf3>(nameof(QuantizedLocalHalf3.Z), 4);

            AssertSize<QuantizedAupSectorHalf3>(18);
            AssertOffset<QuantizedAupSectorHalf3>(nameof(QuantizedAupSectorHalf3.SectorX), 0);
            AssertOffset<QuantizedAupSectorHalf3>(nameof(QuantizedAupSectorHalf3.LocalOffset), 12);

            AssertSize<StrictSaveFileHeader64>(64);
            AssertOffset<StrictSaveFileHeader64>(nameof(StrictSaveFileHeader64.Magic), 0);
            AssertOffset<StrictSaveFileHeader64>(nameof(StrictSaveFileHeader64.PlayTimeSeconds), 12);
            AssertOffset<StrictSaveFileHeader64>(nameof(StrictSaveFileHeader64.AupX), 20);
            AssertOffset<StrictSaveFileHeader64>(nameof(StrictSaveFileHeader64.Checksum), 44);

            AssertSize<SaveChunkHeader32>(32);
            AssertOffset<SaveChunkHeader32>(nameof(SaveChunkHeader32.ChunkKey), 0);
            AssertOffset<SaveChunkHeader32>(nameof(SaveChunkHeader32.PayloadLength), 12);
            AssertOffset<SaveChunkHeader32>(nameof(SaveChunkHeader32.PayloadHash64), 16);

            AssertSize<AbsoluteUniversePositionV7>(36);
            AssertSize<PayloadPrefixV7>(60);
            AssertSize<PayloadPrefixV8>(72);
        }

        private static void VerifyPersistentWorldLayouts()
        {
            AssertSize<PoolSlotData>(40);
            AssertOffset<PoolSlotData>(nameof(PoolSlotData.BoundGuid), 0);
            AssertOffset<PoolSlotData>(nameof(PoolSlotData.AupCell), 8);
            AssertOffset<PoolSlotData>(nameof(PoolSlotData.LocalOffset), 20);
            AssertOffset<PoolSlotData>(nameof(PoolSlotData.HydrationFrame), 32);

            AssertSize<EntityDataRecord>(64);
            AssertOffset<EntityDataRecord>(nameof(EntityDataRecord.Position), 0);
            AssertOffset<EntityDataRecord>(nameof(EntityDataRecord.Quantity), 48);
            AssertOffset<EntityDataRecord>(nameof(EntityDataRecord.Integrity01), 52);
            AssertOffset<EntityDataRecord>(nameof(EntityDataRecord.InventoryHash), 56);
            AssertOffset<EntityDataRecord>(nameof(EntityDataRecord.InstanceUid), 60);

            AssertSize<ResourceNodeTombstoneRecord>(80);
            AssertOffset<ResourceNodeTombstoneRecord>(nameof(ResourceNodeTombstoneRecord.TombstoneId), 0);
            AssertOffset<ResourceNodeTombstoneRecord>(nameof(ResourceNodeTombstoneRecord.Position), 16);
            AssertOffset<ResourceNodeTombstoneRecord>(nameof(ResourceNodeTombstoneRecord.ChunkId), 64);

            AssertSize<PersistentWorldItemRecord>(204);
            AssertOffset<PersistentWorldItemRecord>(nameof(PersistentWorldItemRecord.Position), 0);
            AssertOffset<PersistentWorldItemRecord>(nameof(PersistentWorldItemRecord.ChunkId), 48);
            AssertOffset<PersistentWorldItemRecord>(nameof(PersistentWorldItemRecord.ItemPersistentIdHash), 60);
            AssertOffset<PersistentWorldItemRecord>(nameof(PersistentWorldItemRecord.ItemPersistentId), 68);
            AssertOffset<PersistentWorldItemRecord>(nameof(PersistentWorldItemRecord.InstanceUid), 200);

            AssertSize<PersistentWorldCompactDeltaRecord>(16);
            AssertOffset<PersistentWorldCompactDeltaRecord>(nameof(PersistentWorldCompactDeltaRecord.PackedLocalPosition), 0);
            AssertOffset<PersistentWorldCompactDeltaRecord>(nameof(PersistentWorldCompactDeltaRecord.Quantity), 8);
            AssertOffset<PersistentWorldCompactDeltaRecord>(nameof(PersistentWorldCompactDeltaRecord.ChunkIndex), 12);
        }

        private static void VerifySignalLayouts()
        {
            AssertSize<ComplianceViolationSignal>(32);
            AssertOffset<ComplianceViolationSignal>(nameof(ComplianceViolationSignal.RuleHash), 0);
            AssertOffset<ComplianceViolationSignal>(nameof(ComplianceViolationSignal.SystemHash), 4);
            AssertOffset<ComplianceViolationSignal>(nameof(ComplianceViolationSignal.ContextHash), 8);
            AssertOffset<ComplianceViolationSignal>(nameof(ComplianceViolationSignal.Frame), 12);
            AssertOffset<ComplianceViolationSignal>(nameof(ComplianceViolationSignal.Severity), 16);
        }

        private static void VerifyRenderBlitLayouts()
        {
            AssertSize<HectonBlueprintPreviewBatch.BlueprintPreviewInstance>(64);
            AssertOffset<HectonBlueprintPreviewBatch.BlueprintPreviewInstance>(
                nameof(HectonBlueprintPreviewBatch.BlueprintPreviewInstance.Position),
                0);
            AssertOffset<HectonBlueprintPreviewBatch.BlueprintPreviewInstance>(
                nameof(HectonBlueprintPreviewBatch.BlueprintPreviewInstance.Rotation),
                12);
            AssertOffset<HectonBlueprintPreviewBatch.BlueprintPreviewInstance>(
                nameof(HectonBlueprintPreviewBatch.BlueprintPreviewInstance.RequirementMask),
                40);
        }

        private static void AssertSize<T>(int expected) where T : unmanaged
        {
            AssertBinarySafe<T>();
            int observed = UnsafeUtility.SizeOf<T>();
            UnityEngine.Debug.Assert(observed == expected, ResolveTypeName<T>());
            if (observed != expected)
                Fail(ResolveTypeName<T>(), expected, observed, CombineHash(SizeContextHash, ResolveTypeHash<T>()));
        }

        private static void AssertOffset<T>(string fieldName, int expected) where T : unmanaged
        {
            int observed = Marshal.OffsetOf(typeof(T), fieldName).ToInt32();
            UnityEngine.Debug.Assert(observed == expected, ResolveTypeName<T>());
            if (observed != expected)
                Fail(ResolveTypeName<T>(), expected, observed, CombineHash(OffsetContextHash, ComputeFnv1A32(fieldName)));
        }

        private static void AssertBinarySafe<T>() where T : unmanaged
        {
            if (!UnsafeUtility.IsBlittable<T>())
                Fail(ResolveTypeName<T>(), expected: 1, observed: 0, CombineHash(BlittableContextHash, ResolveTypeHash<T>()));

            if (!MemoryInquisitor.PrewarmBinaryBlittableSafety<T>())
                Fail(ResolveTypeName<T>(), expected: 1, observed: 0, CombineHash(AttributeContextHash, ResolveTypeHash<T>()));
        }

        private static void Fail(string structName, int expected, int observed, uint contextHash)
        {
            PublishComplianceViolation(contextHash);
            DumpFailure(structName, expected, observed, contextHash);
            throw new CriticalBootException("[BinaryLayoutManifest] Binary layout validation failed: " + structName);
        }

        private static void PublishComplianceViolation(uint contextHash)
        {
            ComplianceViolationSignal signal = new ComplianceViolationSignal
            {
                RuleHash = LayoutRuleHash,
                SystemHash = LayoutSystemHash,
                ContextHash = contextHash,
                Frame = (uint)Mathf.Max(0, Time.frameCount),
                Severity = 3,
                Flags = 1
            };
            GlobalSignals.Publish(in signal);
        }

        private static void DumpFailure(string structName, int expected, int observed, uint contextHash)
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", DumpRelativePath));
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(DumpMagic);
                writer.Write(DumpVersion);
                writer.Write(contextHash);
                writer.Write(expected);
                writer.Write(observed);
                writer.Write(structName ?? string.Empty);
            }
        }

        private static string ResolveTypeName<T>() where T : unmanaged
        {
            return typeof(T).FullName ?? typeof(T).Name;
        }

        private static uint ResolveTypeHash<T>() where T : unmanaged
        {
            return ComputeFnv1A32(ResolveTypeName<T>());
        }

        private static uint CombineHash(uint left, uint right)
        {
            return unchecked((left * 16777619u) ^ right);
        }

        private static uint ComputeFnv1A32(string value)
        {
            uint hash = 2166136261u;
            if (string.IsNullOrEmpty(value))
                return hash;

            for (int i = 0; i < value.Length; i++)
                hash = unchecked((hash ^ value[i]) * 16777619u);

            return hash;
        }
    }
}
