using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.AtlasSignal;
using Hecton8.Caves;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory.Layout;
using Hecton8.Economy;
using Hecton8.Gameplay;
using Hecton8.Gameplay.Atlas6Liability;
using Hecton8.Inventory;
using Hecton8.Narrative;
using Hecton8.SaveSystem;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Tests.Editor
{
    public sealed unsafe class HazardZoneRuntimeSaveEditTests
    {
        private const int BinaryPayloadScratchBytes = 1024 * 1024;

        [Test]
        public void SaveEventPayload_IsExplicitTwentyFourBytes()
        {
            StructLayoutAttribute layout = typeof(SaveEventPayload).StructLayoutAttribute;
            Assert.IsNotNull(layout);
            Assert.AreEqual(LayoutKind.Explicit, layout.Value);
            Assert.AreEqual(24, UnsafeUtility.SizeOf<SaveEventPayload>());
            Assert.AreEqual(0, UnsafeUtility.SizeOf<SaveEventPayload>() & 7);
            Assert.AreEqual(0, (int)Marshal.OffsetOf<SaveEventPayload>(nameof(SaveEventPayload.TimestampTicks)));
            Assert.AreEqual(8, (int)Marshal.OffsetOf<SaveEventPayload>(nameof(SaveEventPayload.SlotHash)));
            Assert.AreEqual(12, (int)Marshal.OffsetOf<SaveEventPayload>(nameof(SaveEventPayload.MessageHash)));
            Assert.AreEqual(16, (int)Marshal.OffsetOf<SaveEventPayload>(nameof(SaveEventPayload.MessageSlot)));
            Assert.AreEqual(20, (int)Marshal.OffsetOf<SaveEventPayload>(nameof(SaveEventPayload.Type)));
            Assert.AreEqual(21, (int)Marshal.OffsetOf<SaveEventPayload>(nameof(SaveEventPayload._pad0)));
            Assert.AreEqual(22, (int)Marshal.OffsetOf<SaveEventPayload>(nameof(SaveEventPayload._pad1)));
            Assert.AreEqual(23, (int)Marshal.OffsetOf<SaveEventPayload>(nameof(SaveEventPayload._pad2)));
        }

        [Test]
        public void SaveManagerBackupRecoveryAndLoadFailure_ReachHudNotificationLink()
        {
            string saveManager = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/SaveManager.cs"));
            string hudLink = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/UI/HUDSaveNotificationLink.cs"));
            string mainMenu = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/MainMenuController.cs"));

            int recordLoadIndex = saveManager.IndexOf(
                "RecordSuccessfulLoad(slotName",
                StringComparison.Ordinal);
            Assert.GreaterOrEqual(recordLoadIndex, 0, saveManager);

            int backupGateIndex = saveManager.IndexOf(
                "if (LastLoadUsedBackup)",
                recordLoadIndex,
                StringComparison.Ordinal);
            Assert.Greater(backupGateIndex, recordLoadIndex, saveManager);

            int emergencyRaiseIndex = saveManager.IndexOf(
                "SaveEvents.TryRaiseEmergencyBackupRestoreRequested(SaveEvents.ComputeSlotHash(slotName));",
                backupGateIndex,
                StringComparison.Ordinal);
            Assert.Greater(emergencyRaiseIndex, backupGateIndex, saveManager);

            int recoveredSignalIndex = saveManager.IndexOf(
                "PublishSaveRecoveredNotification(slotName);",
                emergencyRaiseIndex,
                StringComparison.Ordinal);
            Assert.Greater(recoveredSignalIndex, emergencyRaiseIndex, saveManager);

            StringAssert.Contains("LoadFailedKeyHash", hudLink);
            StringAssert.Contains("BackupRestoreKeyHash", hudLink);
            StringAssert.Contains("LocalizationKeys.ERROR_LOAD_FAILED_TITLE", hudLink);
            StringAssert.Contains("LocalizationKeys.WARNING_BACKUP_USED_TITLE", hudLink);
            StringAssert.Contains("TryResolveNotificationSystem(out HUDNotification targetNotification)", hudLink);
            StringAssert.Contains("TryGetComponent(out resolved)", hudLink);
            StringAssert.Contains("HUDNotification.TryGetActive(out resolved)", hudLink);
            StringAssert.Contains("resolved.isActiveAndEnabled", hudLink);

            int loadFailedCaseIndex = hudLink.IndexOf(
                "case SaveEventType.LoadFailed:",
                StringComparison.Ordinal);
            Assert.GreaterOrEqual(loadFailedCaseIndex, 0, hudLink);

            int criticalIndex = hudLink.IndexOf(
                "targetNotification.ShowCritical(in _messageBuffer);",
                loadFailedCaseIndex,
                StringComparison.Ordinal);
            Assert.Greater(criticalIndex, loadFailedCaseIndex, hudLink);

            int backupCaseIndex = hudLink.IndexOf(
                "case SaveEventType.EmergencyBackupRestoreRequested:",
                StringComparison.Ordinal);
            Assert.Greater(backupCaseIndex, criticalIndex, hudLink);

            int warningIndex = hudLink.IndexOf(
                "targetNotification.ShowWarning(in _messageBuffer);",
                backupCaseIndex,
                StringComparison.Ordinal);
            Assert.Greater(warningIndex, backupCaseIndex, hudLink);

            int backupMessageIndex = hudLink.IndexOf(
                "\"BACKUP RESTORE ACTIVE\".AsSpan()",
                StringComparison.Ordinal);
            Assert.Greater(backupMessageIndex, warningIndex, hudLink);

            StringAssert.Contains("private uint _pendingBackupRestoreSlotHash;", mainMenu);
            StringAssert.Contains("case SaveEventType.EmergencyBackupRestoreRequested:", mainMenu);
            StringAssert.Contains("OnEmergencyBackupRestoreRequested(in payload);", mainMenu);
            StringAssert.Contains("_pendingBackupRestoreSlotHash = 0u;", mainMenu);
            StringAssert.Contains("_pendingBackupRestoreSlotHash = payload.SlotHash;", mainMenu);
            StringAssert.Contains("backupRestoreRaisedForSlot || (_saveManager != null && _saveManager.LastLoadUsedBackup)", mainMenu);
        }

        [Test]
        public void SaveDeltaCompression_SuitUpgradeMaskMatchesRuntimeResolver()
        {
            const ulong supportedMask = SaveData.SuitUpgradeSupportedMask;
            const ulong malformedMask = supportedMask | (1UL << 63) | (1UL << 32);

            Assert.AreEqual(SuitUpgradeResolver.SupportedMask, supportedMask);
            Assert.AreEqual(supportedMask, SaveDeltaCompression.SupportedSuitUpgradeMask);
            PackedSuitUpgradeState64 packed = SaveDeltaCompression.PackSuitUpgrades64(malformedMask);
            Assert.AreEqual(supportedMask, packed.Value);
            Assert.AreEqual(supportedMask, SaveDeltaCompression.UnpackSuitUpgrades64(new PackedSuitUpgradeState64(malformedMask)));
        }

        [Test]
        public void SaveManagerWfcDirtySignalDrain_UsesOwnerScratchInsteadOfLargeStackalloc()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/SaveManager.cs"));

            StringAssert.Contains("private readonly ulong[] _wfcDirtySectorScratch", source);
            StringAssert.Contains("private readonly ushort[] _wfcDirtyCellIndexScratch", source);
            StringAssert.Contains("private readonly byte[] _wfcDirtyCellFlagScratch", source);
            StringAssert.Contains("_wfcDirtySectorScratch.AsSpan(0, signals.Length)", source);
            StringAssert.Contains("out bool writeOverflow", source);
            int overflowIndex = source.IndexOf("if (writeOverflow)", StringComparison.Ordinal);
            Assert.GreaterOrEqual(overflowIndex, 0, source);
            int acquireIndex = source.IndexOf("if (!TryAcquireWfcOutpostGridWrite", overflowIndex, StringComparison.Ordinal);
            Assert.Greater(acquireIndex, overflowIndex, source);
            string overflowBlock = source.Substring(overflowIndex, acquireIndex - overflowIndex);
            StringAssert.Contains("RecordWfcOutpostEventBlackBox", overflowBlock);
            StringAssert.Contains("PublishWfcWriteFailureWarning();", overflowBlock);
            StringAssert.Contains("continue;", overflowBlock);
            Assert.IsFalse(source.Contains("stackalloc ulong[MaxWfcDirtySectorStackEntries]"));
            Assert.IsFalse(source.Contains("stackalloc ushort[MaxWfcDirtySectorStackEntries]"));
            Assert.IsFalse(source.Contains("stackalloc byte[MaxWfcDirtySectorStackEntries]"));
        }

        [Test]
        public void SaveSidecarStorage_RespectsConfiguredPersistentRoot()
        {
            string previousRoot = Hecton8.Core.HectonPersistentPathPolicy.RootPath;
            string tempRoot = Path.Combine(Path.GetTempPath(), "H8SidecarRoot_" + Guid.NewGuid().ToString("N"));
            string relativePath = Path.Combine("nested", "slot.meta");
            string expectedPath = Path.Combine(tempRoot, relativePath);

            try
            {
                Directory.CreateDirectory(tempRoot);
                SaveSidecarStorage.SetPersistentDataPathRoot(tempRoot);

                SaveMetadata metadata = SaveMetadata.CreateFallback("slot", 123456789L);
                metadata.GameVersion = "root-override-test";
                metadata.SceneName = "RootOverrideScene";
                metadata.PlayTimeSeconds = 42.5f;
                metadata.Checksum = "abc123";
                metadata.WorldSeed = 8192;
                metadata.WorldGenerationVersionId = 7;

                Assert.IsTrue(SaveSidecarStorage.SaveMetadata(metadata, relativePath, out string saveError), saveError);
                Assert.IsTrue(File.Exists(expectedPath), expectedPath);
                Assert.IsTrue(SaveSidecarStorage.LoadMetadata(relativePath, out SaveMetadata loaded, out string loadError), loadError);
                Assert.AreEqual(metadata.SlotName, loaded.SlotName);
                Assert.AreEqual(metadata.GameVersion, loaded.GameVersion);
                Assert.AreEqual(metadata.SceneName, loaded.SceneName);
                Assert.AreEqual(metadata.PlayTimeSeconds, loaded.PlayTimeSeconds);
                Assert.AreEqual(metadata.Checksum, loaded.Checksum);
                Assert.AreEqual(metadata.WorldSeed, loaded.WorldSeed);
                Assert.AreEqual(metadata.WorldGenerationVersionId, loaded.WorldGenerationVersionId);
            }
            finally
            {
                SaveSidecarStorage.SetPersistentDataPathRoot(previousRoot);
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, true);
            }
        }

        [Test]
        public void SaveManagerLoadCandidate_FallsBackToVoxelDtoWhenNativeSnapshotValidationFails()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/SaveManager.cs"));

            int candidateIndex = source.IndexOf(
                "private static bool TryLoadBinaryCandidate(",
                StringComparison.Ordinal);
            Assert.GreaterOrEqual(candidateIndex, 0, source);

            int validationIndex = source.IndexOf(
                "!VoxelDeltaProcessor.TryValidateNativeSnapshotForLoad",
                candidateIndex,
                StringComparison.Ordinal);
            Assert.GreaterOrEqual(validationIndex, 0, source);

            int dtoFallbackGateIndex = source.IndexOf(
                "HasLoadableVoxelDeltaDtoFallback(data)",
                validationIndex,
                StringComparison.Ordinal);
            Assert.Greater(dtoFallbackGateIndex, validationIndex, source);

            int fallbackWarningIndex = source.IndexOf(
                "falling back to binary voxel payload",
                dtoFallbackGateIndex,
                StringComparison.Ordinal);
            Assert.Greater(fallbackWarningIndex, dtoFallbackGateIndex, source);

            int disposeIndex = source.IndexOf(
                "DisposeTransientNativeArray(ref loadedVoxelDeltaSnapshot",
                fallbackWarningIndex,
                StringComparison.Ordinal);
            Assert.Greater(disposeIndex, fallbackWarningIndex, source);

            int clearNativeSnapshotIndex = source.IndexOf(
                "voxelDeltaSnapshot = default;",
                disposeIndex,
                StringComparison.Ordinal);
            Assert.Greater(clearNativeSnapshotIndex, disposeIndex, source);

            int fallbackSuccessIndex = source.IndexOf(
                "return true;",
                clearNativeSnapshotIndex,
                StringComparison.Ordinal);
            Assert.Greater(fallbackSuccessIndex, clearNativeSnapshotIndex, source);

            int nativeRejectIndex = source.IndexOf(
                "errorMessage = fallbackReason;",
                fallbackSuccessIndex,
                StringComparison.Ordinal);

            Assert.Greater(nativeRejectIndex, fallbackSuccessIndex, source);
        }

        [Test]
        public void SaveManagerLoad_FailsWhenVoxelDtoFallbackCannotLoad()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/SaveManager.cs"));

            int voxelLoadBranchIndex = source.IndexOf(
                "if (voxelDeltaProcessor != null)",
                StringComparison.Ordinal);
            Assert.GreaterOrEqual(voxelLoadBranchIndex, 0, source);

            int stageIntegrityIndex = source.IndexOf(
                "StageIntegrityPayload(",
                voxelLoadBranchIndex,
                StringComparison.Ordinal);
            Assert.Greater(stageIntegrityIndex, voxelLoadBranchIndex, source);

            int rejectedSnapshotFlagIndex = source.LastIndexOf(
                "bool loadedVoxelDeltaSnapshotRejectedForLoad = false;",
                voxelLoadBranchIndex,
                StringComparison.Ordinal);
            Assert.GreaterOrEqual(rejectedSnapshotFlagIndex, 0, source);
            Assert.Less(rejectedSnapshotFlagIndex, voxelLoadBranchIndex, source);

            int rejectedSnapshotMarkIndex = source.IndexOf(
                "loadedVoxelDeltaSnapshotRejectedForLoad = true;",
                rejectedSnapshotFlagIndex,
                stageIntegrityIndex - rejectedSnapshotFlagIndex,
                StringComparison.Ordinal);
            Assert.Greater(rejectedSnapshotMarkIndex, rejectedSnapshotFlagIndex, source);

            int rejectedSnapshotDisposeIndex = source.IndexOf(
                "if (loadedVoxelDeltaSnapshotRejectedForLoad && loadedVoxelDeltaSnapshot.IsCreated)",
                rejectedSnapshotMarkIndex,
                stageIntegrityIndex - rejectedSnapshotMarkIndex,
                StringComparison.Ordinal);
            Assert.Greater(rejectedSnapshotDisposeIndex, rejectedSnapshotMarkIndex, source);

            int fallbackTryIndex = source.IndexOf(
                "!voxelDeltaProcessor.TryLoadFromSaveData(data, out string voxelFallbackError)",
                voxelLoadBranchIndex,
                stageIntegrityIndex - voxelLoadBranchIndex,
                StringComparison.Ordinal);
            Assert.Greater(fallbackTryIndex, voxelLoadBranchIndex, source);

            int loadFailureIndex = source.IndexOf(
                "Voxel delta binary payload load failed.",
                fallbackTryIndex,
                stageIntegrityIndex - fallbackTryIndex,
                StringComparison.Ordinal);
            Assert.Greater(loadFailureIndex, fallbackTryIndex, source);

            int recordFailureIndex = source.IndexOf(
                "RecordFailure(slotName, \"load\", loadFailure);",
                loadFailureIndex,
                stageIntegrityIndex - loadFailureIndex,
                StringComparison.Ordinal);
            Assert.Greater(recordFailureIndex, loadFailureIndex, source);

            int returnIndex = source.IndexOf(
                "return;",
                recordFailureIndex,
                stageIntegrityIndex - recordFailureIndex,
                StringComparison.Ordinal);
            Assert.Greater(returnIndex, recordFailureIndex, source);

            int silentLoadIndex = source.IndexOf(
                "voxelDeltaProcessor.LoadFromSaveData(data);",
                voxelLoadBranchIndex,
                stageIntegrityIndex - voxelLoadBranchIndex,
                StringComparison.Ordinal);
            Assert.AreEqual(-1, silentLoadIndex, source);
        }

        [Test]
        public void SaveManagerLoad_FailsWhenVoxelPayloadHasNoProcessor()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/SaveManager.cs"));

            int helperIndex = source.IndexOf(
                "private static bool HasVoxelDeltaPayloadForLoad(SaveData data, NativeArray<byte> loadedVoxelDeltaSnapshot)",
                StringComparison.Ordinal);
            Assert.GreaterOrEqual(helperIndex, 0, source);

            int loadStartIndex = source.IndexOf(
                "public async Awaitable LoadGameAsync(string slotName)",
                helperIndex,
                StringComparison.Ordinal);
            Assert.Greater(loadStartIndex, helperIndex, source);

            string helperBody = source.Substring(helperIndex, loadStartIndex - helperIndex);
            StringAssert.Contains("loadedVoxelDeltaSnapshot.IsCreated && loadedVoxelDeltaSnapshot.Length > 0", helperBody);
            StringAssert.Contains("HasVoxelDeltaDtoPayloadForLoad(data)", helperBody);
            StringAssert.Contains("private static bool HasLoadableVoxelDeltaDtoFallback(SaveData data)", helperBody);
            StringAssert.Contains("voxelDeltaPersistence.chunkCount > 0", helperBody);
            StringAssert.Contains("voxelDeltaPersistence.totalCellCount > 0", helperBody);
            StringAssert.Contains("VoxelDeltaProcessor.TryValidateSaveDataForLoad(data, out _)", helperBody);
            Assert.IsFalse(helperBody.Contains("voxelDeltaPersistence.carvingOperationCount > 0"), helperBody);

            int voxelLoadBranchIndex = source.IndexOf(
                "if (voxelDeltaProcessor != null)",
                loadStartIndex,
                StringComparison.Ordinal);
            Assert.Greater(voxelLoadBranchIndex, loadStartIndex, source);

            int stageIntegrityIndex = source.IndexOf(
                "StageIntegrityPayload(",
                voxelLoadBranchIndex,
                StringComparison.Ordinal);
            Assert.Greater(stageIntegrityIndex, voxelLoadBranchIndex, source);

            int missingProcessorBranchIndex = source.IndexOf(
                "else if (HasVoxelDeltaPayloadForLoad(data, loadedVoxelDeltaSnapshot))",
                voxelLoadBranchIndex,
                stageIntegrityIndex - voxelLoadBranchIndex,
                StringComparison.Ordinal);
            Assert.Greater(missingProcessorBranchIndex, voxelLoadBranchIndex, source);

            int failureMessageIndex = source.IndexOf(
                "Voxel delta payload exists, but no VoxelDeltaProcessor is registered for load.",
                missingProcessorBranchIndex,
                stageIntegrityIndex - missingProcessorBranchIndex,
                StringComparison.Ordinal);
            Assert.Greater(failureMessageIndex, missingProcessorBranchIndex, source);

            int recordFailureIndex = source.IndexOf(
                "RecordFailure(slotName, \"load\", loadFailure);",
                failureMessageIndex,
                stageIntegrityIndex - failureMessageIndex,
                StringComparison.Ordinal);
            Assert.Greater(recordFailureIndex, failureMessageIndex, source);

            int returnIndex = source.IndexOf(
                "return;",
                recordFailureIndex,
                stageIntegrityIndex - recordFailureIndex,
                StringComparison.Ordinal);
            Assert.Greater(returnIndex, recordFailureIndex, source);
        }

        [Test]
        public void VoxelDeltaProcessorBinaryLoad_FailsClosedOnStoreFailures()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/VoxelDeltaProcessor.cs"));

            int tryLoadIndex = source.IndexOf(
                "public bool TryLoadFromSaveData(SaveData data, out string error)",
                StringComparison.Ordinal);
            Assert.GreaterOrEqual(tryLoadIndex, 0, source);

            int upfrontValidationIndex = source.IndexOf(
                "!TryValidateSaveDataForLoad(data, out string validationError)",
                tryLoadIndex,
                StringComparison.Ordinal);
            Assert.Greater(upfrontValidationIndex, tryLoadIndex, source);

            int validateHelperIndex = source.IndexOf(
                "internal static bool TryValidateSaveDataForLoad(SaveData data, out string error)",
                tryLoadIndex,
                StringComparison.Ordinal);
            Assert.Greater(validateHelperIndex, tryLoadIndex, source);

            int failHelperDeclarationIndex = source.IndexOf(
                "private bool FailLoadedVoxelDeltaState(string message, out string error)",
                validateHelperIndex,
                StringComparison.Ordinal);
            Assert.Greater(failHelperDeclarationIndex, validateHelperIndex, source);

            string validateHelperBody = source.Substring(validateHelperIndex, failHelperDeclarationIndex - validateHelperIndex);
            StringAssert.Contains("Voxel delta binary payload has cells without chunks.", validateHelperBody);
            StringAssert.Contains("Voxel delta chunk count exceeds available binary payload chunks.", validateHelperBody);
            StringAssert.Contains("Voxel delta binary payload chunk coordinate is outside the supported range.", validateHelperBody);
            StringAssert.Contains("IsSupportedVoxelDeltaChunkCoordinate(chunk.chunkX)", validateHelperBody);
            StringAssert.Contains("Voxel delta binary payload chunk has invalid voxel size.", validateHelperBody);
            StringAssert.Contains("Voxel delta binary payload chunk has unsupported storage flags.", validateHelperBody);
            StringAssert.Contains("legacyDirtyMaskScratch ??= new uint[ChunkDirtyMaskWordCount]", validateHelperBody);
            StringAssert.Contains("TryComputeLocalCellIndex(absoluteCell, chunkCoord, out uint localIndex)", validateHelperBody);
            StringAssert.Contains("appliedLegacyCellCount++", validateHelperBody);
            StringAssert.Contains("Voxel delta binary payload total cell count mismatch.", validateHelperBody);

            int compactedStoreFailureIndex = source.IndexOf(
                "Voxel delta compacted chunk store failed while loading binary payload.",
                tryLoadIndex,
                StringComparison.Ordinal);
            Assert.Greater(compactedStoreFailureIndex, tryLoadIndex, source);

            int dirtyPoolFailureIndex = source.IndexOf(
                "Voxel delta dirty chunk pool exhausted while loading binary payload.",
                compactedStoreFailureIndex,
                StringComparison.Ordinal);
            Assert.Greater(dirtyPoolFailureIndex, compactedStoreFailureIndex, source);

            int chunkCountFailureIndex = source.IndexOf(
                "Voxel delta chunk count exceeds available binary payload chunks.",
                tryLoadIndex,
                dirtyPoolFailureIndex - tryLoadIndex,
                StringComparison.Ordinal);
            Assert.Greater(chunkCountFailureIndex, tryLoadIndex, source);

            int dirtyStoreFailureIndex = source.IndexOf(
                "Voxel delta dirty chunk store failed while loading binary payload.",
                dirtyPoolFailureIndex,
                StringComparison.Ordinal);
            Assert.Greater(dirtyStoreFailureIndex, dirtyPoolFailureIndex, source);

            int legacyCellFailureIndex = source.IndexOf(
                "Voxel delta legacy cell store failed while loading binary payload.",
                dirtyPoolFailureIndex,
                StringComparison.Ordinal);
            Assert.Greater(legacyCellFailureIndex, dirtyPoolFailureIndex, source);

            int totalCountFailureIndex = source.IndexOf(
                "Voxel delta binary payload total cell count mismatch.",
                legacyCellFailureIndex,
                StringComparison.Ordinal);
            Assert.Greater(totalCountFailureIndex, legacyCellFailureIndex, source);

            int clearIndex = source.IndexOf(
                "ClearLoadedVoxelDeltaStateAfterFailedLoad();",
                failHelperDeclarationIndex,
                StringComparison.Ordinal);
            Assert.Greater(clearIndex, failHelperDeclarationIndex, source);

            int blackBoxIndex = source.IndexOf(
                "WriteBlackBoxSample(0UL, VoxelBlackBoxQueueOverflowFlag);",
                compactedStoreFailureIndex,
                StringComparison.Ordinal);
            Assert.Greater(blackBoxIndex, compactedStoreFailureIndex, source);

            int wrapperIndex = source.IndexOf(
                "public void LoadFromSaveData(SaveData data)",
                StringComparison.Ordinal);
            Assert.GreaterOrEqual(wrapperIndex, 0, source);

            int wrapperTryIndex = source.IndexOf(
                "TryLoadFromSaveData(data, out string error)",
                wrapperIndex,
                tryLoadIndex - wrapperIndex,
                StringComparison.Ordinal);
            Assert.Greater(wrapperTryIndex, wrapperIndex, source);
        }

        [Test]
        public void VoxelDeltaProcessorBinaryLoad_RejectsOutOfRangeChunkCoordinates()
        {
            SaveData data = SaveData.CreateNew(42.0);
            data.voxelDeltaPersistence.EnsureCapacity(1);
            data.voxelDeltaPersistence.chunkCount = 1;
            data.voxelDeltaPersistence.totalCellCount = 0;
            data.voxelDeltaPersistence.chunks[0] = new VoxelDeltaChunkDTO
            {
                chunkX = (long)int.MaxValue + 1L,
                chunkY = 0,
                chunkZ = 0,
                voxelSize = 0.25f,
                storageFlags = VoxelDeltaChunkDTO.StorageDense,
                dirtyMaskWords = Array.Empty<uint>(),
                sdfValueBits = Array.Empty<ushort>(),
                materialIds = Array.Empty<byte>(),
                cellFlags = Array.Empty<byte>(),
                cells = Array.Empty<VoxelDeltaCellDTO>()
            };

            bool valid = VoxelDeltaProcessor.TryValidateSaveDataForLoad(data, out string error);

            Assert.IsFalse(valid);
            StringAssert.Contains("Voxel delta binary payload chunk coordinate is outside the supported range.", error);
        }

        [Test]
        public void VoxelDeltaProcessorNativeSnapshotLoad_FailsClosedAfterStateMutation()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/VoxelDeltaProcessor.cs"));

            int tryLoadIndex = source.IndexOf(
                "public unsafe bool TryLoadNativeSnapshot(NativeArray<byte> snapshot, out string error)",
                StringComparison.Ordinal);
            Assert.GreaterOrEqual(tryLoadIndex, 0, source);

            int validationGateIndex = source.IndexOf(
                "if (!TryValidateNativeSnapshotForLoad(snapshot, out error))",
                tryLoadIndex,
                StringComparison.Ordinal);
            Assert.Greater(validationGateIndex, tryLoadIndex, source);

            int clearIndex = source.IndexOf(
                "DisposeChunkStates();",
                validationGateIndex,
                StringComparison.Ordinal);
            Assert.Greater(clearIndex, validationGateIndex, source);

            int staticValidatorIndex = source.IndexOf(
                "private static unsafe bool TryValidateNativeSnapshotForLoadUnsafe(NativeArray<byte> snapshot, out string error)",
                clearIndex,
                StringComparison.Ordinal);
            Assert.Greater(staticValidatorIndex, clearIndex, source);

            string loadBodyAfterClear = source.Substring(clearIndex, staticValidatorIndex - clearIndex);
            StringAssert.Contains("return FailLoadedVoxelDeltaState(", loadBodyAfterClear);
            StringAssert.Contains("!math.isfinite(chunkHeader.VoxelSize)", loadBodyAfterClear);
            StringAssert.Contains("Voxel delta dirty chunk store failed while loading dense payload.", loadBodyAfterClear);
            StringAssert.Contains("Voxel delta dense dirty-mask count does not match the chunk header.", loadBodyAfterClear);
            StringAssert.Contains("Voxel delta snapshot dirty-cell count does not match the header.", loadBodyAfterClear);
            Assert.AreEqual(-1, loadBodyAfterClear.IndexOf("return false;", StringComparison.Ordinal), source);

            int nextMethodIndex = source.IndexOf(
                "private unsafe bool TryLoadSparseRlePayload(",
                staticValidatorIndex,
                StringComparison.Ordinal);
            Assert.Greater(nextMethodIndex, staticValidatorIndex, source);

            string validatorBody = source.Substring(staticValidatorIndex, nextMethodIndex - staticValidatorIndex);
            Assert.AreEqual(-1, validatorBody.IndexOf("FailLoadedVoxelDeltaState(", StringComparison.Ordinal), source);
            StringAssert.Contains("!math.isfinite(chunkHeader.VoxelSize)", validatorBody);
            StringAssert.Contains("CountNativeSnapshotDirtyMaskBits(snapshotPtr + cursor)", validatorBody);
            StringAssert.Contains("error = \"Voxel delta dense dirty-mask count does not match the chunk header.\";", validatorBody);
            StringAssert.Contains("error = \"Voxel delta snapshot contains unread trailing bytes.\";", validatorBody);
            StringAssert.Contains("error = \"Voxel delta snapshot dirty-cell count does not match the header.\";", validatorBody);
        }

        [Test]
        public void VoxelDeltaProcessorDataVaultRollback_ReleasesFailedNativeSnapshotScratch()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/VoxelDeltaProcessor.cs"));

            int rollbackIndex = source.IndexOf(
                "private void RestoreDataVaultAfterFailedRebind(",
                StringComparison.Ordinal);
            Assert.GreaterOrEqual(rollbackIndex, 0, source);

            int setOldVaultIndex = source.IndexOf(
                "_dataVault = oldVault;",
                rollbackIndex,
                StringComparison.Ordinal);
            Assert.Greater(setOldVaultIndex, rollbackIndex, source);

            int disposeNativeScratchIndex = source.IndexOf(
                "DisposeNativeSnapshotScratchBuffer(failedVault);",
                rollbackIndex,
                setOldVaultIndex - rollbackIndex,
                StringComparison.Ordinal);
            Assert.Greater(disposeNativeScratchIndex, rollbackIndex, source);

            int ensureNativeScratchIndex = source.IndexOf(
                "EnsureNativeSnapshotScratchBuffer();",
                setOldVaultIndex,
                StringComparison.Ordinal);
            Assert.Greater(ensureNativeScratchIndex, setOldVaultIndex, source);
        }

        [Test]
        public void SaveManagerSave_PopulatesVoxelDtoAfterBorrowingNativeSnapshot()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/SaveManager.cs"));

            int voxelBranchIndex = source.IndexOf(
                "if (saveable is VoxelDeltaProcessor voxelDeltaProcessor)",
                StringComparison.Ordinal);
            Assert.GreaterOrEqual(voxelBranchIndex, 0, source);

            int nativeOwnerIndex = source.IndexOf(
                "borrowedVoxelDeltaSnapshotOwner = voxelDeltaProcessor;",
                voxelBranchIndex,
                StringComparison.Ordinal);
            Assert.Greater(nativeOwnerIndex, voxelBranchIndex, source);

            int populateIndex = source.IndexOf(
                "saveable.PopulateSaveData(data);",
                nativeOwnerIndex,
                StringComparison.Ordinal);
            Assert.Greater(populateIndex, nativeOwnerIndex, source);

            int skippedPopulateIndex = source.IndexOf(
                "continue;",
                nativeOwnerIndex,
                populateIndex - nativeOwnerIndex,
                StringComparison.Ordinal);
            Assert.AreEqual(-1, skippedPopulateIndex, source);
        }

        [Test]
        public void PlayerInventoryBlackBoxDump_UsesSystemTimestampPathNotBatchId()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/PlayerInventory.cs"));

            StringAssert.Contains("BuildInventoryBlackBoxDumpRelativePath(DateTime.UtcNow.Ticks)", source);
            StringAssert.Contains("Docs/AgentLogs/Dump_INVENTORY_BLACKBOX_", source);
            StringAssert.Contains("Docs/AgentLogs/Dump_INVENTORY_BLACKBOX.bin", source);
            StringAssert.DoesNotContain("Dump_1317_Inventory.bin", source);
        }

        [Test]
        public void AsyncWriteManagerFlushQueue_FallsBackInsteadOfDroppingRequests()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/SaveBinaryStorage.cs"));

            StringAssert.Contains("bool flushImmediately = false;", source);
            StringAssert.Contains("flushImmediately = true;", source);
            StringAssert.Contains("if (flushImmediately)", source);
            StringAssert.Contains("ThrottleFlush(byteCount);", source);
            StringAssert.Contains("if (!TryFlushPath(absolutePath))", source);
            StringAssert.Contains("Flush queue is full and immediate flush failed.", source);
            StringAssert.Contains("private static bool TryFlushPath(string absolutePath)", source);
            StringAssert.Contains("_ = TryFlushPath(request.AbsolutePath);", source);

            int queueFullIndex = source.IndexOf("if (s_flushCount == DiskFlushQueueCapacity)", StringComparison.Ordinal);
            Assert.GreaterOrEqual(queueFullIndex, 0, source);
            int enqueueIndex = source.IndexOf("s_flushQueue[s_flushWriteIndex]", queueFullIndex, StringComparison.Ordinal);
            Assert.Greater(enqueueIndex, queueFullIndex, source);
            string queueFullBlock = source.Substring(queueFullIndex, enqueueIndex - queueFullIndex);
            StringAssert.Contains("flushImmediately = true;", queueFullBlock);
            StringAssert.DoesNotContain("s_flushReadIndex = (s_flushReadIndex + 1) % DiskFlushQueueCapacity;", queueFullBlock);
            StringAssert.DoesNotContain("s_flushCount--;", queueFullBlock);
        }

        [Test]
        public void IndexedSectorEntityStateWriteSchedulingFailure_DefersScratchDisposal()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/SaveBinaryStorage.cs"));

            int errorIndex = source.IndexOf("Sector entity-state write job scheduling failed", StringComparison.Ordinal);
            Assert.GreaterOrEqual(errorIndex, 0, source);
            int blockStart = source.LastIndexOf("JobHandle scheduledHandle = default;", errorIndex, StringComparison.Ordinal);
            Assert.GreaterOrEqual(blockStart, 0, source);
            int blockEnd = source.IndexOf("return true;", errorIndex, StringComparison.Ordinal);
            Assert.Greater(blockEnd, errorIndex, source);
            string schedulingBlock = source.Substring(blockStart, blockEnd - blockStart);

            StringAssert.Contains("scheduledHandle = buildHandle;", schedulingBlock);
            StringAssert.Contains("scheduledHandle = sortHandle;", schedulingBlock);
            StringAssert.Contains("scheduledHandle = extractHandle;", schedulingBlock);
            StringAssert.Contains("scheduledHandle = compactHandle;", schedulingBlock);
            StringAssert.Contains("scheduledHandle = compressHandle;", schedulingBlock);
            StringAssert.Contains("writeHandle.Handle = scheduledHandle;", schedulingBlock);
            StringAssert.Contains("DisposeIndexedSectorEntityStateOverrideWriteDeferred(ref writeHandle, default);", schedulingBlock);
            StringAssert.Contains("JobHandle.ScheduleBatchedJobs();", schedulingBlock);
            StringAssert.DoesNotContain("writeHandle.Dispose();", schedulingBlock);
        }

        [Test]
        public void HazardZoneRuntimeDTO_IsExplicitEightBytes()
        {
            StructLayoutAttribute layout = typeof(HazardZoneRuntimeDTO).StructLayoutAttribute;
            Assert.IsNotNull(layout);
            Assert.AreEqual(LayoutKind.Explicit, layout.Value);
            Assert.AreEqual(8, UnsafeUtility.SizeOf<HazardZoneRuntimeDTO>());
            Assert.AreEqual(0, UnsafeUtility.SizeOf<HazardZoneRuntimeDTO>() & 7);
            Assert.AreEqual(0, (int)Marshal.OffsetOf<HazardZoneRuntimeDTO>(nameof(HazardZoneRuntimeDTO.toxicityDose)));
            Assert.AreEqual(4, (int)Marshal.OffsetOf<HazardZoneRuntimeDTO>(nameof(HazardZoneRuntimeDTO.toxicityPulseAccumulatorSeconds)));
        }

        [Test]
        public void HabitatFloodStateDTO_IsExplicitThirtyTwoBytes()
        {
            StructLayoutAttribute layout = typeof(HabitatFloodStateDTO).StructLayoutAttribute;
            Assert.IsNotNull(layout);
            Assert.AreEqual(LayoutKind.Explicit, layout.Value);
            Assert.IsTrue(Attribute.IsDefined(typeof(HabitatFloodStateDTO), typeof(BinaryBlittableSafeAttribute)));
            Assert.AreEqual(32, UnsafeUtility.SizeOf<HabitatFloodStateDTO>());
            Assert.AreEqual(0, UnsafeUtility.SizeOf<HabitatFloodStateDTO>() & 7);
            Assert.AreEqual(0, (int)Marshal.OffsetOf<HabitatFloodStateDTO>(nameof(HabitatFloodStateDTO.moduleHashId)));
            Assert.AreEqual(4, (int)Marshal.OffsetOf<HabitatFloodStateDTO>(nameof(HabitatFloodStateDTO.integrity)));
            Assert.AreEqual(8, (int)Marshal.OffsetOf<HabitatFloodStateDTO>(nameof(HabitatFloodStateDTO.repairIntegrityCap)));
            Assert.AreEqual(12, (int)Marshal.OffsetOf<HabitatFloodStateDTO>(nameof(HabitatFloodStateDTO.airReserveNormalized)));
            Assert.AreEqual(16, (int)Marshal.OffsetOf<HabitatFloodStateDTO>(nameof(HabitatFloodStateDTO.co2Normalized)));
            Assert.AreEqual(20, (int)Marshal.OffsetOf<HabitatFloodStateDTO>(nameof(HabitatFloodStateDTO.floodedReefFloodSeconds)));
            Assert.AreEqual(24, (int)Marshal.OffsetOf<HabitatFloodStateDTO>(nameof(HabitatFloodStateDTO.flags)));
            Assert.AreEqual(25, (int)Marshal.OffsetOf<HabitatFloodStateDTO>(nameof(HabitatFloodStateDTO.failureMode)));
            Assert.AreEqual(26, (int)Marshal.OffsetOf<HabitatFloodStateDTO>(nameof(HabitatFloodStateDTO.health)));
            Assert.AreEqual(27, (int)Marshal.OffsetOf<HabitatFloodStateDTO>(nameof(HabitatFloodStateDTO.reserved0)));
        }

        [Test]
        public void ModuleBlitDTO_IsExplicitSixtyFourBytes()
        {
            StructLayoutAttribute layout = typeof(ModuleBlitDTO).StructLayoutAttribute;
            Assert.IsNotNull(layout);
            Assert.AreEqual(LayoutKind.Explicit, layout.Value);
            Assert.IsTrue(Attribute.IsDefined(typeof(ModuleBlitDTO), typeof(BinaryBlittableSafeAttribute)));
            Assert.AreEqual(64, UnsafeUtility.SizeOf<ModuleBlitDTO>());
            Assert.AreEqual(0, UnsafeUtility.SizeOf<ModuleBlitDTO>() & 15);
            Assert.AreEqual(0, (int)Marshal.OffsetOf<ModuleBlitDTO>(nameof(ModuleBlitDTO.prefabHashId)));
            Assert.AreEqual(4, (int)Marshal.OffsetOf<ModuleBlitDTO>(nameof(ModuleBlitDTO.moduleHashId)));
            Assert.AreEqual(8, (int)Marshal.OffsetOf<ModuleBlitDTO>(nameof(ModuleBlitDTO.aupGridX)));
            Assert.AreEqual(16, (int)Marshal.OffsetOf<ModuleBlitDTO>(nameof(ModuleBlitDTO.aupGridY)));
            Assert.AreEqual(24, (int)Marshal.OffsetOf<ModuleBlitDTO>(nameof(ModuleBlitDTO.aupGridZ)));
            Assert.AreEqual(32, (int)Marshal.OffsetOf<ModuleBlitDTO>(nameof(ModuleBlitDTO.aupLocalX)));
            Assert.AreEqual(36, (int)Marshal.OffsetOf<ModuleBlitDTO>(nameof(ModuleBlitDTO.aupLocalY)));
            Assert.AreEqual(40, (int)Marshal.OffsetOf<ModuleBlitDTO>(nameof(ModuleBlitDTO.aupLocalZ)));
            Assert.AreEqual(44, (int)Marshal.OffsetOf<ModuleBlitDTO>(nameof(ModuleBlitDTO.rotX)));
            Assert.AreEqual(48, (int)Marshal.OffsetOf<ModuleBlitDTO>(nameof(ModuleBlitDTO.rotY)));
            Assert.AreEqual(52, (int)Marshal.OffsetOf<ModuleBlitDTO>(nameof(ModuleBlitDTO.rotZ)));
            Assert.AreEqual(56, (int)Marshal.OffsetOf<ModuleBlitDTO>(nameof(ModuleBlitDTO.rotW)));
            Assert.AreEqual(60, (int)Marshal.OffsetOf<ModuleBlitDTO>(nameof(ModuleBlitDTO.health)));
            Assert.AreEqual(61, (int)Marshal.OffsetOf<ModuleBlitDTO>(nameof(ModuleBlitDTO.flags)));
            Assert.AreEqual(62, (int)Marshal.OffsetOf<ModuleBlitDTO>(nameof(ModuleBlitDTO.failureMode)));
            Assert.AreEqual(63, (int)Marshal.OffsetOf<ModuleBlitDTO>(nameof(ModuleBlitDTO.reserved)));
        }

        [Test]
        public void ModuleGraphEdgeDTO_IsExplicitSixteenBytes()
        {
            StructLayoutAttribute layout = typeof(ModuleGraphEdgeDTO).StructLayoutAttribute;
            Assert.IsNotNull(layout);
            Assert.AreEqual(LayoutKind.Explicit, layout.Value);
            Assert.IsTrue(Attribute.IsDefined(typeof(ModuleGraphEdgeDTO), typeof(BinaryBlittableSafeAttribute)));
            Assert.AreEqual(16, UnsafeUtility.SizeOf<ModuleGraphEdgeDTO>());
            Assert.AreEqual(0, UnsafeUtility.SizeOf<ModuleGraphEdgeDTO>() & 7);
            Assert.AreEqual(0, (int)Marshal.OffsetOf<ModuleGraphEdgeDTO>(nameof(ModuleGraphEdgeDTO.sourceNodeIndex)));
            Assert.AreEqual(4, (int)Marshal.OffsetOf<ModuleGraphEdgeDTO>(nameof(ModuleGraphEdgeDTO.destinationNodeIndex)));
        }

        [Test]
        public void HabitatFloodStateDTO_FromModuleSanitizesNonFiniteMirrorScalars()
        {
            ModuleDTO module = new ModuleDTO
            {
                integrity = float.NaN,
                repairIntegrityCap = float.NegativeInfinity,
                airReserveNormalized = 2f,
                co2Normalized = float.PositiveInfinity,
                floodedReefFloodSeconds = -10f,
                isFlooded = true,
                interiorReefInfestationActive = true,
                failureMode = 2,
                health = 128
            };

            HabitatFloodStateDTO dto = HabitatFloodStateDTO.FromModule(in module, 123456);

            Assert.AreEqual(123456, dto.moduleHashId);
            Assert.AreEqual(0f, dto.integrity);
            Assert.AreEqual(0f, dto.repairIntegrityCap);
            Assert.AreEqual(1f, dto.airReserveNormalized);
            Assert.AreEqual(0f, dto.co2Normalized);
            Assert.AreEqual(0f, dto.floodedReefFloodSeconds);
            Assert.AreEqual((byte)(HabitatFloodStateDTO.FlagFlooded | HabitatFloodStateDTO.FlagInfested), dto.flags);
            Assert.AreEqual((byte)2, dto.failureMode);
            Assert.AreEqual((byte)128, dto.health);
        }

        [Test]
        public void HabitatFloodStateDTO_SanitizeRepairsLoadedMirrorScalars()
        {
            HabitatFloodStateDTO dto = new HabitatFloodStateDTO
            {
                moduleHashId = 987654,
                integrity = float.PositiveInfinity,
                repairIntegrityCap = -4f,
                airReserveNormalized = -2f,
                co2Normalized = float.NaN,
                floodedReefFloodSeconds = float.NegativeInfinity,
                flags = 0xFF,
                failureMode = 9,
                health = 77,
                reserved0 = 12
            };

            HabitatFloodStateDTO sanitized = HabitatFloodStateDTO.Sanitize(in dto);

            Assert.AreEqual(987654, sanitized.moduleHashId);
            Assert.AreEqual(0f, sanitized.integrity);
            Assert.AreEqual(0f, sanitized.repairIntegrityCap);
            Assert.AreEqual(0f, sanitized.airReserveNormalized);
            Assert.AreEqual(0f, sanitized.co2Normalized);
            Assert.AreEqual(0f, sanitized.floodedReefFloodSeconds);
            Assert.AreEqual((byte)(HabitatFloodStateDTO.FlagFlooded | HabitatFloodStateDTO.FlagInfested), sanitized.flags);
            Assert.AreEqual(SaveData.ModuleFailureModeNone, sanitized.failureMode);
            Assert.AreEqual(77, sanitized.health);
            Assert.AreEqual(0, sanitized.reserved0);
        }

        [Test]
        public void ModuleDTO_SanitizeForPersistenceRepairsConstructionScalarsWithoutMutatingSourceArrays()
        {
            ModuleDTO module = new ModuleDTO
            {
                prefabId = " ",
                slottedToolItemId = "\t",
                pipeInFlightItemId = null,
                pipeInFlightAmount = -3,
                pipeTransitProgress = float.NaN,
                pipeExportTimerSeconds = float.PositiveInfinity,
                drillBufferedItemId = " ",
                drillBufferedAmount = -2,
                drillCycleTimerSeconds = -10f,
                sorterBufferedSlotCount = 1,
                sorterBufferedItemIds = new[] { " ", "sorter.kept" },
                sorterBufferedQuantities = new[] { -5, 9 },
                posX = float.NaN,
                posY = 12f,
                posZ = float.NegativeInfinity,
                rotX = 0f,
                rotY = 0f,
                rotZ = 0f,
                rotW = 0f,
                integrity = float.NaN,
                repairIntegrityCap = -4f,
                airReserveNormalized = 3f,
                co2Normalized = -2f,
                failureMode = 9,
                floodedReefFloodSeconds = float.PositiveInfinity,
                cultivationSlotCount = 1,
                cultivationSeedItemIds = new[] { "\t", "seed.kept" },
                cultivationGeneticsMasks = new[] { 0xFFFFUL, 0x2UL },
                cultivationGrowth01 = new[] { float.NaN, 0.5f },
                cultivationQuality01 = new[] { 2f, 0.5f }
            };

            ModuleDTO sanitized = ModuleDTO.SanitizeForPersistence(in module);

            Assert.AreEqual(string.Empty, sanitized.prefabId);
            Assert.AreEqual(string.Empty, sanitized.slottedToolItemId);
            Assert.AreEqual(string.Empty, sanitized.pipeInFlightItemId);
            Assert.AreEqual(0, sanitized.pipeInFlightAmount);
            Assert.AreEqual(0f, sanitized.pipeTransitProgress);
            Assert.AreEqual(0f, sanitized.pipeExportTimerSeconds);
            Assert.AreEqual(string.Empty, sanitized.drillBufferedItemId);
            Assert.AreEqual(0, sanitized.drillBufferedAmount);
            Assert.AreEqual(0f, sanitized.drillCycleTimerSeconds);
            Assert.AreEqual(string.Empty, sanitized.sorterBufferedItemIds[0]);
            Assert.AreEqual(0, sanitized.sorterBufferedQuantities[0]);
            Assert.AreEqual(0f, sanitized.posX);
            Assert.AreEqual(12f, sanitized.posY);
            Assert.AreEqual(0f, sanitized.posZ);
            Assert.AreEqual(0f, sanitized.rotX);
            Assert.AreEqual(0f, sanitized.rotY);
            Assert.AreEqual(0f, sanitized.rotZ);
            Assert.AreEqual(1f, sanitized.rotW);
            Assert.AreEqual(0f, sanitized.integrity);
            Assert.AreEqual(0f, sanitized.repairIntegrityCap);
            Assert.AreEqual(1f, sanitized.airReserveNormalized);
            Assert.AreEqual(0f, sanitized.co2Normalized);
            Assert.AreEqual(SaveData.ModuleFailureModeNone, sanitized.failureMode);
            Assert.AreEqual(0f, sanitized.floodedReefFloodSeconds);
            Assert.AreEqual(string.Empty, sanitized.cultivationSeedItemIds[0]);
            Assert.AreEqual(ModuleDTO.CultivationGeneticsSupportedMask, sanitized.cultivationGeneticsMasks[0]);
            Assert.AreEqual(0f, sanitized.cultivationGrowth01[0]);
            Assert.AreEqual(1f, sanitized.cultivationQuality01[0]);
            Assert.AreEqual(" ", module.sorterBufferedItemIds[0]);
            Assert.AreEqual(-5, module.sorterBufferedQuantities[0]);
            Assert.AreEqual(9, module.failureMode);
            Assert.AreEqual("\t", module.cultivationSeedItemIds[0]);
            Assert.AreEqual(0xFFFFUL, module.cultivationGeneticsMasks[0]);
            Assert.IsTrue(float.IsNaN(module.cultivationGrowth01[0]));
            Assert.AreEqual(2f, module.cultivationQuality01[0]);
        }

        [Test]
        public void ModuleDTO_PersistenceEqualsTracksSerializedIdentityAndStateFields()
        {
            ModuleDTO module = CreatePersistenceSampleModule();
            ModuleDTO same = CreatePersistenceSampleModule();
            Assert.IsTrue(ModuleDTO.PersistenceEquals(in module, in same));

            ModuleDTO changed = module;
            changed.prefabId = "module.changed";
            AssertModulePersistenceDifference(in module, in changed, nameof(ModuleDTO.prefabId));

            changed = module;
            changed.slottedToolItemId = "tool.changed";
            AssertModulePersistenceDifference(in module, in changed, nameof(ModuleDTO.slottedToolItemId));

            changed = module;
            changed.pipeInFlightItemId = "pipe.changed";
            AssertModulePersistenceDifference(in module, in changed, nameof(ModuleDTO.pipeInFlightItemId));

            changed = module;
            changed.drillBufferedItemId = "drill.changed";
            AssertModulePersistenceDifference(in module, in changed, nameof(ModuleDTO.drillBufferedItemId));

            changed = module;
            changed.isFlooded = !module.isFlooded;
            AssertModulePersistenceDifference(in module, in changed, nameof(ModuleDTO.isFlooded));

            changed = module;
            changed.health = 201;
            AssertModulePersistenceDifference(in module, in changed, nameof(ModuleDTO.health));

            changed = module;
            changed.interiorReefInfestationActive = !module.interiorReefInfestationActive;
            AssertModulePersistenceDifference(in module, in changed, nameof(ModuleDTO.interiorReefInfestationActive));

            changed = CreatePersistenceSampleModule();
            changed.sorterBufferedItemIds[1] = "sorter.changed";
            AssertModulePersistenceDifference(in module, in changed, nameof(ModuleDTO.sorterBufferedItemIds));

            changed = CreatePersistenceSampleModule();
            changed.sorterBufferedQuantities[1] = 9;
            AssertModulePersistenceDifference(in module, in changed, nameof(ModuleDTO.sorterBufferedQuantities));

            changed = CreatePersistenceSampleModule();
            changed.cultivationSeedItemIds[1] = "seed.changed";
            AssertModulePersistenceDifference(in module, in changed, nameof(ModuleDTO.cultivationSeedItemIds));

            changed = CreatePersistenceSampleModule();
            changed.cultivationGeneticsMasks[1] = 0xFFFFu;
            AssertModulePersistenceDifference(in module, in changed, nameof(ModuleDTO.cultivationGeneticsMasks));

            changed = CreatePersistenceSampleModule();
            changed.cultivationGrowth01[1] = 0.91f;
            AssertModulePersistenceDifference(in module, in changed, nameof(ModuleDTO.cultivationGrowth01));

            changed = CreatePersistenceSampleModule();
            changed.cultivationQuality01[1] = 0.23f;
            AssertModulePersistenceDifference(in module, in changed, nameof(ModuleDTO.cultivationQuality01));
        }

        [Test]
        public void HazardZoneTelemetryEntry_IsExplicitSixtyFourBytes()
        {
            StructLayoutAttribute layout = typeof(HazardZoneTelemetryEntry).StructLayoutAttribute;
            Assert.IsNotNull(layout);
            Assert.AreEqual(LayoutKind.Explicit, layout.Value);
            Assert.IsTrue(Attribute.IsDefined(typeof(HazardZoneTelemetryEntry), typeof(BinaryBlittableSafeAttribute)));
            Assert.AreEqual(64, UnsafeUtility.SizeOf<HazardZoneTelemetryEntry>());
            Assert.AreEqual(0, UnsafeUtility.SizeOf<HazardZoneTelemetryEntry>() & 7);
            Assert.AreEqual(0, (int)Marshal.OffsetOf<HazardZoneTelemetryEntry>(nameof(HazardZoneTelemetryEntry.PackedOwner)));
            Assert.AreEqual(8, (int)Marshal.OffsetOf<HazardZoneTelemetryEntry>(nameof(HazardZoneTelemetryEntry.FrameIndex)));
            Assert.AreEqual(12, (int)Marshal.OffsetOf<HazardZoneTelemetryEntry>(nameof(HazardZoneTelemetryEntry.Sequence)));
            Assert.AreEqual(16, (int)Marshal.OffsetOf<HazardZoneTelemetryEntry>(nameof(HazardZoneTelemetryEntry.StateHash)));
            Assert.AreEqual(20, (int)Marshal.OffsetOf<HazardZoneTelemetryEntry>(nameof(HazardZoneTelemetryEntry.Flags)));
            Assert.AreEqual(24, (int)Marshal.OffsetOf<HazardZoneTelemetryEntry>(nameof(HazardZoneTelemetryEntry.ActiveZoneCount)));
            Assert.AreEqual(28, (int)Marshal.OffsetOf<HazardZoneTelemetryEntry>(nameof(HazardZoneTelemetryEntry.PendingMutationCount)));
            Assert.AreEqual(32, (int)Marshal.OffsetOf<HazardZoneTelemetryEntry>(nameof(HazardZoneTelemetryEntry.PublishedExposureMask)));
            Assert.AreEqual(36, (int)Marshal.OffsetOf<HazardZoneTelemetryEntry>(nameof(HazardZoneTelemetryEntry.BufferGeneration)));
            Assert.AreEqual(40, (int)Marshal.OffsetOf<HazardZoneTelemetryEntry>(nameof(HazardZoneTelemetryEntry.ToxicityDose)));
            Assert.AreEqual(44, (int)Marshal.OffsetOf<HazardZoneTelemetryEntry>(nameof(HazardZoneTelemetryEntry.ToxicityPulseAccumulatorSeconds)));
            Assert.AreEqual(48, (int)Marshal.OffsetOf<HazardZoneTelemetryEntry>(nameof(HazardZoneTelemetryEntry.PlayerToxicity)));
            Assert.AreEqual(52, (int)Marshal.OffsetOf<HazardZoneTelemetryEntry>(nameof(HazardZoneTelemetryEntry.VehicleToxicity)));
            Assert.AreEqual(56, (int)Marshal.OffsetOf<HazardZoneTelemetryEntry>(nameof(HazardZoneTelemetryEntry.PlayerRadiation)));
            Assert.AreEqual(60, (int)Marshal.OffsetOf<HazardZoneTelemetryEntry>(nameof(HazardZoneTelemetryEntry.VehicleRadiation)));
        }

        [Test]
        public void TelemetryDumpValidator_RecognizesHazardZoneBlackBoxHeader()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Editor/TelemetryDumpValidatorWindow.cs"));

            StringAssert.Contains("private const uint HazardZoneDumpMagic = 0x4838485Au", source);
            StringAssert.Contains("private const int HazardZoneDumpHeaderBytes = 24", source);
            StringAssert.Contains("layoutName = \"hazard-zone\"", source);
            StringAssert.Contains("builder.Append(\" | writeIndex=\")", source);
            StringAssert.Contains("BuildHazardZoneEntryLine", source);
            StringAssert.Contains("ResolveReadableDumpPath", source);
            StringAssert.Contains("ResolveSourceEntryIndex", source);
            StringAssert.Contains("CountHazardTelemetryEntriesWithPayload", source);
            StringAssert.Contains("ComputeHazardZoneTelemetryStateHash", source);
            StringAssert.Contains("builder.Append(\" hashOk=\")", source);
            StringAssert.Contains("builder.Append(\" slot=\")", source);
            StringAssert.Contains("ReadU32(entry, 8)", source);
            StringAssert.Contains("ReadF32(entry, 40)", source);
        }

        [Test]
        public void HazardZoneManager_ClampsExposureToBoundedFiniteRange()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Gameplay/HazardZoneManager.cs"));

            StringAssert.Contains("private static float ClampExposure(float value)", source);
            StringAssert.Contains("private const float HazardIntensityHardCap = 1000f", source);
            StringAssert.Contains("math.isfinite(value) ? math.clamp(value, 0f, HazardIntensityHardCap) : 0f", source);
            StringAssert.Contains("data.Intensity = ClampExposure(intensity)", source);
            StringAssert.Contains("return ClampExposure(SumHazardIntensityLinear(", source);
            StringAssert.Contains("_lastExposureJobResultNonFinite = HasNonFiniteExposureJobResult(in result)", source);
            StringAssert.Contains("HasNonFiniteExposureJobResult", source);
            StringAssert.Contains("!math.isfinite(result.PlayerToxicity)", source);
            StringAssert.Contains("flags |= TelemetryFlagNonFinite", source);
            StringAssert.Contains("if (!Application.isPlaying || intensity <= 0f)", source);
            StringAssert.Contains("UnregisterZone(id, type);", source);
            StringAssert.Contains("if (id == 0)", source);
            StringAssert.Contains("if (id <= 0)", source);

            MethodInfo clampExposure = typeof(HazardZoneManager).GetMethod(
                "ClampExposure",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(clampExposure);
            Assert.AreEqual(0f, (float)clampExposure.Invoke(null, new object[] { float.NaN }));
            Assert.AreEqual(0f, (float)clampExposure.Invoke(null, new object[] { -4f }));
            Assert.AreEqual(0.5f, (float)clampExposure.Invoke(null, new object[] { 0.5f }));
            Assert.AreEqual(128f, (float)clampExposure.Invoke(null, new object[] { 128f }));
            Assert.AreEqual(1000f, (float)clampExposure.Invoke(null, new object[] { 5000f }));
        }

        [Test]
        public void HectonHazardManager_InvalidRadiationFacadeInputDoesNotConsumeTrackingSlot()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Gameplay/HectonHazardManager.cs"));

            StringAssert.Contains("IsValidRadiationFacadeSourceInput", source);
            StringAssert.Contains("_ = UntrackRadiationFacadeId(id)", source);
            StringAssert.Contains("RadiationHazardGrid.UnregisterSource(id)", source);
            StringAssert.Contains("math.isfinite(intensity)", source);
            StringAssert.Contains("intensity > 0f", source);
            StringAssert.Contains("math.isfinite(radius)", source);
            StringAssert.Contains("radius > 0f", source);
            StringAssert.Contains("RuntimeInitializeLoadType.SubsystemRegistration", source);
            StringAssert.Contains("System.Array.Clear(_radiationFacadeIds, 0, _radiationFacadeIds.Length)", source);
            StringAssert.Contains("_radiationFacadeIdCount = 0", source);
            StringAssert.Contains("if (!TrackRadiationFacadeId(id, out bool addedFacadeId))", source);
            StringAssert.Contains("Unregister(id, type)", source);
            StringAssert.Contains("zoneManager.UnregisterZone(id)", source);
            StringAssert.Contains("if (id == 0)", source);
        }

        [Test]
        public void HectonHazardSource_InvalidPayloadUnregistersInsteadOfPublishing()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Gameplay/HectonHazardSource.cs"));

            StringAssert.Contains("TryResolveValidRuntimeSource", source);
            StringAssert.Contains("TryUnregisterAuthority();", source);
            StringAssert.Contains("IsValidHazardSourcePayload", source);
            StringAssert.Contains("IsFiniteRuntimePosition", source);
            StringAssert.Contains("!TryResolveAupFromRuntimeOrigin(position, out AbsoluteUniversePosition sourceAup)", source);
            StringAssert.Contains("RadiationHazardGrid.RegisterSource(_instanceID, in sourceAup, intensity, radius)", source);
            StringAssert.Contains("_registeredRadiationSource = true", source);
            StringAssert.Contains("thermodynamics.TryInjectTransientHeatSource(position, radius, intensity", source);
            StringAssert.Contains("math.isfinite(intensity)", source);
            StringAssert.Contains("intensity > 0f", source);
            StringAssert.Contains("math.isfinite(radius)", source);
            StringAssert.Contains("radius > 0f", source);
        }

        [Test]
        public void EnvironmentalHazard_InvalidPayloadDoesNotMarkRadiationSourceRegistered()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Gameplay/EnvironmentalHazard.cs"));

            StringAssert.Contains("TryResolveValidHazardSourcePayload", source);
            StringAssert.Contains("TryUnregisterRadiationSource();", source);
            StringAssert.Contains("!TryResolveAupFromRuntimeOrigin(position, out AbsoluteUniversePosition sourceAup)", source);
            StringAssert.Contains("RadiationHazardGrid.RegisterSource(_radiationSourceId, in sourceAup, safeIntensity, safeRadius)", source);
            StringAssert.Contains("_radiationSourceRegistered = true", source);
            StringAssert.Contains("thermodynamics.TryInjectTransientHeatSource(position, safeRadius, safeIntensity", source);
            StringAssert.Contains("!math.isfinite(baseDamagePerSecond)", source);
            StringAssert.Contains("!math.isfinite(hazardRadius)", source);
            StringAssert.Contains("!math.isfinite(damageInterval)", source);
        }

        [Test]
        public void RadiationHazard_InvalidSourceUnregistersBeforeGridPublish()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Gameplay/RadiationHazard.cs"));

            StringAssert.Contains("TryResolveValidRadiationSource", source);
            StringAssert.Contains("RadiationHazardGrid.UnregisterSource(_sourceId);", source);
            StringAssert.Contains("RadiationHazardGrid.RegisterSource(_sourceId, in sourceAup, safeIntensity, safeRadius)", source);
            StringAssert.Contains("RuntimeOriginRoute.CurrentRuntimeOriginAup()", source);
            StringAssert.Contains("math.isfinite(runtimePosition.x)", source);
            StringAssert.Contains("math.isfinite(intensity)", source);
            StringAssert.Contains("intensity <= 0f", source);
            StringAssert.Contains("math.isfinite(radiusMeters)", source);
            StringAssert.Contains("math.isfinite(radiationBuildupRate)", source);
            StringAssert.Contains("math.isfinite(radiationRadiusMeters)", source);
        }

        [Test]
        public void HabitatIntegrityManager_ToxicityHazardTracksRegistrationResult()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Gameplay/HabitatIntegrityManager.cs"));

            StringAssert.Contains("!math.isfinite(intensity)", source);
            StringAssert.Contains("if (HectonHazardManager.Register(", source);
            StringAssert.Contains("_toxicityHazardRegistered = true", source);
            StringAssert.Contains("ClearToxicityHazard();", source);
            StringAssert.Contains("HectonHazardManager.Unregister(_toxicityHazardId)", source);
        }

        [Test]
        public void RadioisotopeThermalGenerator_RadiationPublishUsesResolvedAupOrUnregisters()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Power/Generators/RadioisotopeThermalGenerator.cs"));

            StringAssert.Contains("!TryResolveAupFromRuntimeOrigin(position, out AbsoluteUniversePosition positionAup)", source);
            StringAssert.Contains("RadiationHazardGrid.UnregisterSource(_sourceId);", source);
            StringAssert.Contains("math.isfinite(radiationIntensity) && radiationIntensity > 0f", source);
            StringAssert.Contains("RadiationHazardGrid.RegisterSource(", source);
            StringAssert.Contains("in positionAup", source);
            StringAssert.Contains("math.isfinite(heatDelta)", source);
            StringAssert.Contains("math.select(0f, baseOutputWatts, math.isfinite(baseOutputWatts))", source);
            StringAssert.Contains("math.select(MinimumRadiationRadiusMeters, radiationRadiusMeters, math.isfinite(radiationRadiusMeters))", source);
        }

        [Test]
        public void ResourceDistributionDirector_MeteorRadiationFailsClosedOnInvalidAupOrTuning()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/World/ResourceDistributionDirector.cs"));

            StringAssert.Contains("RegisterMeteoriteRadiationHazard", source);
            StringAssert.Contains("ResolveMeteorRadiationHazardZoneId(stableSeed)", source);
            StringAssert.Contains("!math.isfinite(meteoriteRadiationIntensity)", source);
            StringAssert.Contains("!math.isfinite(meteoriteRadiationRadiusMeters)", source);
            StringAssert.Contains("!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition radiationAup)", source);
            StringAssert.Contains("RadiationHazardGrid.UnregisterSource(zoneId);", source);
            StringAssert.Contains("_debugLastMeteorHazardZoneId = 0", source);
            StringAssert.Contains("RadiationHazardGrid.RegisterSource(", source);
            StringAssert.Contains("in radiationAup", source);
            StringAssert.Contains("ResolveFiniteSaturate(meteoriteRadiationIntensity, DefaultMeteoriteRadiationIntensity)", source);
            StringAssert.Contains("ResolveFiniteAtLeast(meteoriteRadiationRadiusMeters, DefaultMeteoriteRadiationRadiusMeters, 4f)", source);
        }

        [Test]
        public void FloraInteractionManager_SporeHazardsSkipNonFiniteRuntimePayload()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/World/FloraInteractionManager.cs"));

            StringAssert.Contains("!IsFiniteVector3(playerPositionWS)", source);
            StringAssert.Contains("!float.IsFinite(detectionRadius)", source);
            StringAssert.Contains("!IsFiniteVector3(instancePositionWS)", source);
            StringAssert.Contains("!math.isfinite(distanceSq) || distanceSq > detectionRadiusSq", source);
            StringAssert.Contains("!math.isfinite(exposure01) || exposure01 <= bestExposure01", source);
            StringAssert.Contains("!IsFiniteVector3(positionWS) || !float.IsFinite(intensity01)", source);
            StringAssert.Contains("!float.IsFinite(burst.Intensity)", source);
            StringAssert.Contains("!IsFiniteVector3(burst.PositionWS)", source);
            StringAssert.Contains("!math.isfinite(exposure01) || exposure01 <= strongestExposure", source);
            StringAssert.Contains("SanitizeSporeHazardAuthoringValues();", source);
            StringAssert.Contains("private void SanitizeSporeHazardAuthoringValues()", source);
            StringAssert.Contains("_toxicSporeHazardRadius = ClampFinite(_toxicSporeHazardRadius, 3f, 1f, 8f);", source);
            StringAssert.Contains("_toxicSporeHazardIntensity = ClampFinite(_toxicSporeHazardIntensity, 0.78f, 0f, 1f);", source);
            StringAssert.Contains("_defensiveSporeBurstRadius = ClampFinite(_defensiveSporeBurstRadius, 7f, 1f, 20f);", source);
            StringAssert.Contains("_defensiveSporeHazardIntensity = ClampFinite(_defensiveSporeHazardIntensity, 1.15f, 0f, 2f);", source);
            StringAssert.Contains("float toxicSporeDetectionRadius = ClampFinite(_toxicSporeDetectionRadius, 4.5f, 1f, 8f);", source);
            StringAssert.DoesNotContain("Mathf.Max(1f, _toxicSporeDetectionRadius)", source);
        }

        [Test]
        public void ChemicalInfluenceGrid_QueueApisRejectNonFiniteRuntimePayload()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs"));

            StringAssert.Contains("TryResolveChemicalQueueInput(worldPosition, intensity, out float clampedIntensity)", source);
            StringAssert.Contains("!IsFiniteRuntimePosition(worldPosition)", source);
            StringAssert.Contains("!math.isfinite(intensity)", source);
            StringAssert.Contains("intensity <= 0f", source);
            StringAssert.Contains("private static bool IsFiniteRuntimePosition(Vector3 runtimePosition)", source);
            StringAssert.Contains("!math.isfinite(radiusMeters)", source);
            StringAssert.Contains("radiusMeters <= 0f", source);
            StringAssert.Contains("breadcrumbRadiusMeters = FiniteAtLeast(breadcrumbRadiusMeters, 28f, 1f);", source);
            StringAssert.Contains("maximumChannelIntensity = FiniteAtLeast(maximumChannelIntensity, DefaultMaximumChannelIntensity, 0.1f);", source);
            StringAssert.Contains("tuning.BaseDiffusionRate = FiniteAtLeast(baseDiffusionRate, 0.18f, 0.001f);", source);
            StringAssert.Contains("tuning.MaxChannelIntensity = FiniteAtLeast(maximumChannelIntensity, DefaultMaximumChannelIntensity, 0.1f);", source);
            StringAssert.Contains("float safeMaximumChannelIntensity = FiniteAtLeast(maximumChannelIntensity, DefaultMaximumChannelIntensity, 0.1f);", source);
            StringAssert.DoesNotContain("math.max(0.1f, maximumChannelIntensity)", source);
            StringAssert.DoesNotContain("MaxChannelIntensity = math.max(0.1f, tuning.MaxChannelIntensity)", source);
            StringAssert.DoesNotContain("float safeMax = math.max(0.1f, MaxChannelIntensity)", source);
            StringAssert.DoesNotContain("math.rcp(math.max(0.1f, MaxChannelIntensity))", source);
            StringAssert.Contains("private static float FiniteAtLeast(float value, float fallback, float minimum)", source);
            StringAssert.Contains("float safeFallback = math.select(minimum, fallback, math.isfinite(fallback));", source);
            StringAssert.Contains("math.select(safeFallback, value, math.isfinite(value))", source);

            int transientIndex = source.IndexOf("private static void RegisterChemicalTransient", StringComparison.Ordinal);
            Assert.GreaterOrEqual(transientIndex, 0);
            string transientBody = source.Substring(transientIndex, Math.Min(600, source.Length - transientIndex));
            StringAssert.Contains("!IsFiniteRuntimePosition(worldPosition)", transientBody);
            StringAssert.Contains("!math.isfinite(intensity)", transientBody);
            StringAssert.Contains("WorldSpatialHashGrid.RegisterTransientEvent", transientBody);
        }

        [Test]
        public void ThermalUpdraftVolume_SanitizesAuthoringValuesBeforeRuntimePreset()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/ThermalUpdraftVolume.cs"));

            StringAssert.Contains("SanitizeAuthoringValues();", source);
            StringAssert.Contains("private void SanitizeAuthoringValues()", source);
            StringAssert.Contains("float.IsFinite(updraftStrength)", source);
            StringAssert.Contains("float.IsFinite(swirlBias)", source);
            StringAssert.Contains("float.IsFinite(heatIntensity)", source);
            StringAssert.Contains("float.IsFinite(hazardRadiusScale)", source);
            StringAssert.Contains("ApplyPreset();", source);
            StringAssert.Contains("Vector3 position = transform.position;", source);
            StringAssert.Contains("!IsFiniteVector3(position)", source);
            StringAssert.Contains("!float.IsFinite(heatIntensity)", source);
            StringAssert.Contains("float influenceRadius = _currentVolume != null", source);
            StringAssert.Contains("float safeInfluenceRadius = FiniteAtLeast(influenceRadius, 1f, 1f);", source);
            StringAssert.Contains("float radius = safeInfluenceRadius * FiniteAtLeast(hazardRadiusScale, 1.1f, 0.1f);", source);
            StringAssert.Contains("if (!float.IsFinite(radius) || radius <= 0f)", source);
            StringAssert.Contains("bool injected = thermodynamics != null", source);
            StringAssert.Contains("thermodynamics.TryInjectTransientHeatSource(", source);
            StringAssert.Contains("position,", source);
            StringAssert.Contains("if (injected)", source);
            StringAssert.Contains("HectonHazardManager.Unregister(_hazardSourceId);", source);
            StringAssert.Contains("private static float FiniteAtLeast(float value, float fallback, float minimum)", source);
        }

        [Test]
        public void ThermalGeyser_SanitizesFiniteIngressBeforeVolcanicDirectorSubmit()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/ThermalGeyser.cs"));

            StringAssert.Contains("float safeGlobalIntensity = ClampFinite(globalIntensity, 1f, 0.1f, MaxGlobalIntensity);", source);
            StringAssert.Contains("_quietDuration = ClampFinite(config.quietDuration, DefaultQuietDurationSeconds, 0.5f, MaxQuietDurationSeconds);", source);
            StringAssert.Contains("_eruptionRadius = ClampFinite(config.eruptionRadius, DefaultEruptionRadiusMeters, 0.5f, MaxEruptionRadiusMeters);", source);
            StringAssert.Contains("_cavitationRadius = math.max(_eruptionRadius, ClampFinite(config.cavitationRadius, DefaultCavitationRadiusMeters, 0.5f, MaxCavitationRadiusMeters));", source);
            StringAssert.Contains("_updraftStrength = ClampFinite(config.updraftStrength, DefaultUpdraftStrength, 0f, MaxUpdraftStrength) * safeGlobalIntensity;", source);
            StringAssert.Contains("private const float MaxGlobalIntensity = 1.25f;", source);
            StringAssert.Contains("private const float MaxRuntimeUpdraftStrength = MaxUpdraftStrength * MaxGlobalIntensity;", source);
            StringAssert.Contains("float safeDt = FiniteAtLeast(dt, 0f, 0f);", source);
            StringAssert.Contains("if (!math.isfinite(fdt) || fdt <= 0f)", source);
            StringAssert.Contains("if (!IsFiniteVector3(origin))", source);
            StringAssert.Contains("float safeImpulse = ClampFinite(mineralEjectionImpulse, DefaultMineralEjectionImpulse, 0.1f, MaxMineralEjectionImpulse);", source);
            StringAssert.Contains("float safeEruptionRadius = ClampFinite(_eruptionRadius, DefaultEruptionRadiusMeters, 0.5f, MaxEruptionRadiusMeters);", source);
            StringAssert.Contains("float safeUpdraftStrength = ClampFinite(_updraftStrength, 0f, 0f, MaxRuntimeUpdraftStrength);", source);
            StringAssert.Contains("float safePhaseTimer = math.select(0f, _phaseTimer, math.isfinite(_phaseTimer));", source);
            StringAssert.Contains("safeUpdraftStrength * active01", source);
            StringAssert.Contains("private static float FiniteAtLeast(float value, float fallback, float minimum)", source);
            StringAssert.Contains("private static float ClampFinite(float value, float fallback, float minimum, float maximum)", source);
            StringAssert.DoesNotContain("_updraftStrength = Mathf.Max(0f, config.updraftStrength * Mathf.Max(0.1f, globalIntensity))", source);
            StringAssert.DoesNotContain("float safeDt = Mathf.Max(0f, dt)", source);
        }

        [Test]
        public void WorldCaveDirector_SanitizesThermalGeyserPlacementInputs()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/WorldCaveDirector.cs"));

            StringAssert.Contains("float safeGeyserIntensity = ResolveFiniteClamp(dressingConfig.globalIntensity, 1f, 0f, 1.25f);", source);
            StringAssert.Contains("int geyserCount = Mathf.Clamp(Mathf.RoundToInt(maxGeyserCount * safeGeyserIntensity), 0, maxGeyserCount);", source);
            StringAssert.Contains("geyser.Configure(geyserConfig, safeGeyserIntensity);", source);
            StringAssert.Contains("!IsFiniteBounds(bounds)", source);
            StringAssert.Contains("float minX = bounds.min.x + margin;", source);
            StringAssert.Contains("if (maxX < minX)", source);
            StringAssert.Contains("private static bool IsFiniteBounds(Bounds bounds)", source);
            StringAssert.Contains("private static float ResolveFiniteClamp(float value, float fallback, float minimum, float maximum)", source);
            StringAssert.DoesNotContain("Mathf.Clamp01(dressingConfig.globalIntensity)", source);
            StringAssert.DoesNotContain("geyser.Configure(geyserConfig, dressingConfig.globalIntensity)", source);
        }

        [Test]
        public void VolcanicUpdraftDirector_SanitizesSettingsAndAuthoredVentIngress()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/World/VolcanicUpdraftDirector.cs"));

            StringAssert.Contains("settings.MaxThrust = ClampFinite(settings.MaxThrust", source);
            StringAssert.Contains("settings.HeatOutput = ClampFinite(settings.HeatOutput", source);
            StringAssert.Contains("settings.DebrisCommandIntensity = SaturateFinite(settings.DebrisCommandIntensity", source);
            StringAssert.Contains("settings.MaxThrust = ClampFinite(editorMaxThrust, 24f, 0.01f, 240f);", source);
            StringAssert.Contains("settings.HeatOutput = ClampFinite(editorHeatOutput, 1f, 0f, 25f);", source);
            StringAssert.Contains("private static float ClampFinite(float value, float fallback, float min, float max)", source);
            StringAssert.Contains("float safeFallback = math.select(min, fallback, math.isfinite(fallback));", source);
            StringAssert.Contains("math.select(safeFallback, value, math.isfinite(value))", source);
            StringAssert.Contains("settings.VentCount > (uint)VolcanicUpdraftVault.MaxVents", source);

            int upsertIndex = source.IndexOf("public bool TryUpsertAuthoredVent", StringComparison.Ordinal);
            Assert.GreaterOrEqual(upsertIndex, 0);
            string upsertBody = source.Substring(upsertIndex, Math.Min(1600, source.Length - upsertIndex));
            StringAssert.Contains("!math.all(math.isfinite(aup))", upsertBody);
            StringAssert.Contains("float safeRadius = VolcanicUpdraftVault.SafePositive(radius, settings.CylinderRadius);", upsertBody);
            StringAssert.Contains("float safeThrust = math.max(0f, math.select(0f, thrustPower, math.isfinite(thrustPower)));", upsertBody);
            StringAssert.Contains("float safeHeatOutput = ClampFinite(heatOutput, settings.HeatOutput, 0f, 25f);", upsertBody);
            StringAssert.Contains("vent.Radius = safeRadius;", upsertBody);
            StringAssert.Contains("vent.ThrustPower = safeThrust;", upsertBody);
            StringAssert.Contains("vent.EruptionTimer = SaturateFinite(timer01, 0f);", upsertBody);
            StringAssert.Contains("vent.Radius = VolcanicUpdraftVault.SafePositive(", source);
            StringAssert.Contains("float readThrustPower = ReadFloatLittleEndian(record, 40);", source);
            StringAssert.Contains("vent.ThrustPower = math.max(0f, math.select(0f, readThrustPower, math.isfinite(readThrustPower)));", source);
            StringAssert.Contains("!math.all(math.isfinite(new float3(x, y, z)))", source);
            StringAssert.Contains("vent.ThrustPower = math.max(0f, math.select(0f, thrust, math.isfinite(thrust)));", source);
        }

        [Test]
        public void HazardVolumeData_IsExplicitSixtyFourBytes()
        {
            StructLayoutAttribute layout = typeof(HazardVolumeData).StructLayoutAttribute;
            Assert.IsNotNull(layout);
            Assert.AreEqual(LayoutKind.Explicit, layout.Value);
            Assert.IsTrue(Attribute.IsDefined(typeof(HazardVolumeData), typeof(BinaryBlittableSafeAttribute)));
            Assert.AreEqual(64, UnsafeUtility.SizeOf<HazardVolumeData>());
            Assert.AreEqual(0, UnsafeUtility.SizeOf<HazardVolumeData>() & 7);
            Assert.AreEqual(0, (int)Marshal.OffsetOf<HazardVolumeData>(nameof(HazardVolumeData.AbsoluteUniversePosition)));
            Assert.AreEqual(24, (int)Marshal.OffsetOf<HazardVolumeData>(nameof(HazardVolumeData.Radius)));
            Assert.AreEqual(28, (int)Marshal.OffsetOf<HazardVolumeData>(nameof(HazardVolumeData.InvRadius)));
            Assert.AreEqual(32, (int)Marshal.OffsetOf<HazardVolumeData>(nameof(HazardVolumeData.InvRadiusSqr)));
            Assert.AreEqual(36, (int)Marshal.OffsetOf<HazardVolumeData>(nameof(HazardVolumeData.Intensity)));
            Assert.AreEqual(40, (int)Marshal.OffsetOf<HazardVolumeData>(nameof(HazardVolumeData.VisorGlitchBias)));
            Assert.AreEqual(44, (int)Marshal.OffsetOf<HazardVolumeData>(nameof(HazardVolumeData.CurveLutOffset)));
            Assert.AreEqual(48, (int)Marshal.OffsetOf<HazardVolumeData>(nameof(HazardVolumeData.Type)));
            Assert.AreEqual(52, (int)Marshal.OffsetOf<HazardVolumeData>(nameof(HazardVolumeData.RequiresToxicMudBroadphase)));
            Assert.AreEqual(53, (int)Marshal.OffsetOf<HazardVolumeData>(nameof(HazardVolumeData.PlayerToxicMudBroadphase)));
            Assert.AreEqual(54, (int)Marshal.OffsetOf<HazardVolumeData>(nameof(HazardVolumeData.VehicleToxicMudBroadphase)));
        }

        [Test]
        public void HazardExposureJobResult_IsExplicitOneHundredTwentyEightBytes()
        {
            StructLayoutAttribute layout = typeof(HazardExposureJobResult).StructLayoutAttribute;
            Assert.IsNotNull(layout);
            Assert.AreEqual(LayoutKind.Explicit, layout.Value);
            Assert.IsTrue(Attribute.IsDefined(typeof(HazardExposureJobResult), typeof(BinaryBlittableSafeAttribute)));
            Assert.AreEqual(128, UnsafeUtility.SizeOf<HazardExposureJobResult>());
            Assert.AreEqual(0, UnsafeUtility.SizeOf<HazardExposureJobResult>() & 15);
            Assert.AreEqual(0, (int)Marshal.OffsetOf<HazardExposureJobResult>(nameof(HazardExposureJobResult.PlayerRadiation)));
            Assert.AreEqual(8, (int)Marshal.OffsetOf<HazardExposureJobResult>(nameof(HazardExposureJobResult.PlayerToxicity)));
            Assert.AreEqual(32, (int)Marshal.OffsetOf<HazardExposureJobResult>(nameof(HazardExposureJobResult.VehicleRadiation)));
            Assert.AreEqual(40, (int)Marshal.OffsetOf<HazardExposureJobResult>(nameof(HazardExposureJobResult.VehicleToxicity)));
            Assert.AreEqual(64, (int)Marshal.OffsetOf<HazardExposureJobResult>(nameof(HazardExposureJobResult.PlayerExposureMask)));
            Assert.AreEqual(65, (int)Marshal.OffsetOf<HazardExposureJobResult>(nameof(HazardExposureJobResult.VehicleExposureMask)));
        }

        [Test]
        public void HazardZoneRuntime_RoundTripsThroughBinaryPayload()
        {
            SaveData data = SaveData.CreateNew(42.0);
            data.hazardZones.toxicityDose = 12.25f;
            data.hazardZones.toxicityPulseAccumulatorSeconds = 0.25f;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(SaveData.CurrentVersion, restored.version);
                Assert.AreEqual(12.25f, restored.hazardZones.toxicityDose);
                Assert.AreEqual(0.25f, restored.hazardZones.toxicityPulseAccumulatorSeconds);
                Assert.IsTrue(BitConverter.IsLittleEndian);
                Assert.AreEqual(1, CountLittleEndianFloatPair(payload, bytesWritten, 12.25f, 0.25f));
                Assert.AreEqual(0, CountLittleEndianFloatPair(payload, bytesWritten, 0.25f, 12.25f));
            }
        }

        [Test]
        public void AtlasSignalRuntime_SanitizesInvalidPulseTimerAndRevealStageThroughBinaryPayload()
        {
            SaveData data = SaveData.CreateNew(42.0);
            data.atlasSignalDetected = true;
            data.atlasSignalPulseTimer = float.PositiveInfinity;
            data.atlasSignalRevealStage = 99;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.IsTrue(restored.atlasSignalDetected);
                Assert.AreEqual(0f, restored.atlasSignalPulseTimer);
                Assert.AreEqual(4, restored.atlasSignalRevealStage);
            }
        }

        [Test]
        public void Atlas6LiabilityRuntime_RoundTripsThroughBinaryPayload()
        {
            const uint workerHashA = 0xABCDEF01u;
            const uint workerHashB = 0x12345678u;

            SaveData data = SaveData.CreateNew(42.0);
            data.atlas6LiabilitySectorXenonOmegaYield = 321.5f;
            data.atlas6LiabilityHasDisasterEvidence = true;
            data.atlas6LiabilityRecoveredWorkerTagCount = 2;
            data.atlas6LiabilityRecoveredWorkerTagHashes[0] = workerHashA;
            data.atlas6LiabilityRecoveredWorkerTagHashes[1] = workerHashB;
            data.atlas6LiabilityCorporateHostilityIndex = 31.25f;
            data.atlas6LiabilityCorporateCreditBalance = 4700f;
            data.atlas6LiabilityExtractionCarrierState = 3;
            data.atlas6LiabilityBiomatterExposureLevel = 44.5f;
            data.atlas6LiabilityHaldaneLockoutActive = true;
            data.atlas6LiabilityPressureSealIntegrity = 0.25f;
            data.atlas6LiabilityBulkheadLocked = true;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(SaveData.CurrentVersion, restored.version);
                Assert.AreEqual(321.5f, restored.atlas6LiabilitySectorXenonOmegaYield);
                Assert.IsTrue(restored.atlas6LiabilityHasDisasterEvidence);
                Assert.AreEqual(2, restored.atlas6LiabilityRecoveredWorkerTagCount);
                Assert.AreEqual(SaveData.MaxAtlas6LiabilityWorkerTags, restored.atlas6LiabilityRecoveredWorkerTagHashes.Length);
                Assert.AreEqual(workerHashA, restored.atlas6LiabilityRecoveredWorkerTagHashes[0]);
                Assert.AreEqual(workerHashB, restored.atlas6LiabilityRecoveredWorkerTagHashes[1]);
                Assert.AreEqual(0u, restored.atlas6LiabilityRecoveredWorkerTagHashes[2]);
                Assert.AreEqual(31.25f, restored.atlas6LiabilityCorporateHostilityIndex);
                Assert.AreEqual(4700f, restored.atlas6LiabilityCorporateCreditBalance);
                Assert.AreEqual(3, restored.atlas6LiabilityExtractionCarrierState);
                Assert.AreEqual(44.5f, restored.atlas6LiabilityBiomatterExposureLevel);
                Assert.IsTrue(restored.atlas6LiabilityHaldaneLockoutActive);
                Assert.AreEqual(0.25f, restored.atlas6LiabilityPressureSealIntegrity);
                Assert.IsTrue(restored.atlas6LiabilityBulkheadLocked);
            }
        }

        [Test]
        public void VoxelDeltaRuntime_RoundTripsUniformChunkThroughBinaryPayload()
        {
            SaveData data = SaveData.CreateNew(42.0);
            data.voxelDeltaPersistence.EnsureCapacity(1);
            data.voxelDeltaPersistence.chunkCount = 1;
            data.voxelDeltaPersistence.totalCellCount = VoxelDeltaChunkDTO.CellCount;
            data.voxelDeltaPersistence.chunks[0] = new VoxelDeltaChunkDTO
            {
                chunkX = -2,
                chunkY = 3,
                chunkZ = 4,
                voxelSize = 0.5f,
                cellCount = VoxelDeltaChunkDTO.CellCount,
                storageFlags = VoxelDeltaChunkDTO.StorageUniformSdfRle,
                uniformSdfValueBits = 0x1234,
                dirtyMaskWords = Array.Empty<uint>(),
                sdfValueBits = Array.Empty<ushort>(),
                materialIds = Array.Empty<byte>(),
                cellFlags = Array.Empty<byte>(),
                cells = Array.Empty<VoxelDeltaCellDTO>()
            };

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(SaveData.CurrentVersion, restored.version);
                Assert.AreEqual(1, restored.voxelDeltaPersistence.chunkCount);
                Assert.AreEqual(VoxelDeltaChunkDTO.CellCount, restored.voxelDeltaPersistence.totalCellCount);
                VoxelDeltaChunkDTO chunk = restored.voxelDeltaPersistence.chunks[0];
                Assert.AreEqual(-2L, chunk.chunkX);
                Assert.AreEqual(3L, chunk.chunkY);
                Assert.AreEqual(4L, chunk.chunkZ);
                Assert.AreEqual(0.5f, chunk.voxelSize);
                Assert.AreEqual(VoxelDeltaChunkDTO.CellCount, chunk.cellCount);
                Assert.AreEqual(VoxelDeltaChunkDTO.StorageUniformSdfRle, chunk.storageFlags);
                Assert.AreEqual((ushort)0x1234, chunk.uniformSdfValueBits);
                Assert.AreEqual(0, chunk.dirtyMaskWords.Length);
                Assert.AreEqual(0, chunk.sdfValueBits.Length);
                Assert.AreEqual(0, chunk.materialIds.Length);
                Assert.AreEqual(0, chunk.cellFlags.Length);
                Assert.AreEqual(0, chunk.cells.Length);
            }
        }

        [Test]
        public void VoxelDeltaRuntime_RoundTripsDenseChunkWithMissingFlagsThroughBinaryPayload()
        {
            const int dirtyCellIndex = 42;
            const int dirtyWordIndex = dirtyCellIndex / 32;
            const uint dirtyCellBit = 1u << (dirtyCellIndex & 31);

            SaveData data = SaveData.CreateNew(42.0);
            uint[] dirtyMaskWords = new uint[VoxelDeltaChunkDTO.DirtyMaskWordCount];
            ushort[] sdfValueBits = new ushort[VoxelDeltaChunkDTO.CellCount];
            byte[] materialIds = new byte[VoxelDeltaChunkDTO.CellCount];
            dirtyMaskWords[dirtyWordIndex] = dirtyCellBit;
            sdfValueBits[dirtyCellIndex] = 0x4321;
            materialIds[dirtyCellIndex] = 7;

            data.voxelDeltaPersistence.EnsureCapacity(1);
            data.voxelDeltaPersistence.chunkCount = 1;
            data.voxelDeltaPersistence.totalCellCount = 1;
            data.voxelDeltaPersistence.chunks[0] = new VoxelDeltaChunkDTO
            {
                chunkX = 11,
                chunkY = -12,
                chunkZ = 13,
                voxelSize = 0.25f,
                cellCount = 1,
                storageFlags = VoxelDeltaChunkDTO.StorageDense,
                dirtyMaskWords = dirtyMaskWords,
                sdfValueBits = sdfValueBits,
                materialIds = materialIds,
                cellFlags = Array.Empty<byte>(),
                cells = Array.Empty<VoxelDeltaCellDTO>()
            };

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(1, restored.voxelDeltaPersistence.chunkCount);
                Assert.AreEqual(1, restored.voxelDeltaPersistence.totalCellCount);
                VoxelDeltaChunkDTO chunk = restored.voxelDeltaPersistence.chunks[0];
                Assert.AreEqual(11L, chunk.chunkX);
                Assert.AreEqual(-12L, chunk.chunkY);
                Assert.AreEqual(13L, chunk.chunkZ);
                Assert.AreEqual(0.25f, chunk.voxelSize);
                Assert.AreEqual(1, chunk.cellCount);
                Assert.AreEqual(VoxelDeltaChunkDTO.StorageDense, chunk.storageFlags);
                Assert.AreEqual(VoxelDeltaChunkDTO.DirtyMaskWordCount, chunk.dirtyMaskWords.Length);
                Assert.AreEqual(dirtyCellBit, chunk.dirtyMaskWords[dirtyWordIndex]);
                Assert.AreEqual(VoxelDeltaChunkDTO.CellCount, chunk.sdfValueBits.Length);
                Assert.AreEqual((ushort)0x4321, chunk.sdfValueBits[dirtyCellIndex]);
                Assert.AreEqual(VoxelDeltaChunkDTO.CellCount, chunk.materialIds.Length);
                Assert.AreEqual((byte)7, chunk.materialIds[dirtyCellIndex]);
                Assert.AreEqual(VoxelDeltaChunkDTO.CellCount, chunk.cellFlags.Length);
                Assert.AreEqual((byte)0, chunk.cellFlags[dirtyCellIndex]);
                Assert.AreEqual(0, chunk.cells.Length);
            }
        }

        [Test]
        public void VoxelDeltaRuntime_WriteSanitizesMalformedDenseCellFlags()
        {
            const int dirtyCellIndex = 42;
            const int dirtyWordIndex = dirtyCellIndex / 32;
            const uint dirtyCellBit = 1u << (dirtyCellIndex & 31);
            const byte malformedFlags = VoxelDeltaChunkDTO.SupportedCellFlags | 0x80;

            SaveData data = SaveData.CreateNew(42.0);
            uint[] dirtyMaskWords = new uint[VoxelDeltaChunkDTO.DirtyMaskWordCount];
            ushort[] sdfValueBits = new ushort[VoxelDeltaChunkDTO.CellCount];
            byte[] materialIds = new byte[VoxelDeltaChunkDTO.CellCount];
            byte[] cellFlags = new byte[VoxelDeltaChunkDTO.CellCount];
            dirtyMaskWords[dirtyWordIndex] = dirtyCellBit;
            sdfValueBits[dirtyCellIndex] = 0x4321;
            materialIds[dirtyCellIndex] = 7;
            cellFlags[dirtyCellIndex] = malformedFlags;

            data.voxelDeltaPersistence.EnsureCapacity(1);
            data.voxelDeltaPersistence.chunkCount = 1;
            data.voxelDeltaPersistence.totalCellCount = 1;
            data.voxelDeltaPersistence.chunks[0] = new VoxelDeltaChunkDTO
            {
                chunkX = 11,
                chunkY = -12,
                chunkZ = 13,
                voxelSize = 0.25f,
                cellCount = 1,
                storageFlags = VoxelDeltaChunkDTO.StorageDense,
                dirtyMaskWords = dirtyMaskWords,
                sdfValueBits = sdfValueBits,
                materialIds = materialIds,
                cellFlags = cellFlags,
                cells = Array.Empty<VoxelDeltaCellDTO>()
            };

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                VoxelDeltaChunkDTO chunk = restored.voxelDeltaPersistence.chunks[0];
                Assert.AreEqual(VoxelDeltaChunkDTO.SupportedCellFlags, chunk.cellFlags[dirtyCellIndex]);
            }
        }

        [Test]
        public void VoxelDeltaRuntime_WriteSanitizesMalformedLegacyCellFlags()
        {
            const long universeKey = 0x1122334455667788L;
            const byte malformedFlags = VoxelDeltaChunkDTO.SupportedCellFlags | 0x80;

            SaveData data = SaveData.CreateNew(42.0);
            data.voxelDeltaPersistence.EnsureCapacity(1);
            data.voxelDeltaPersistence.chunkCount = 1;
            data.voxelDeltaPersistence.totalCellCount = 1;
            data.voxelDeltaPersistence.chunks[0] = new VoxelDeltaChunkDTO
            {
                chunkX = -21,
                chunkY = 22,
                chunkZ = -23,
                voxelSize = 0.5f,
                cellCount = 1,
                storageFlags = VoxelDeltaChunkDTO.StorageDense,
                dirtyMaskWords = Array.Empty<uint>(),
                sdfValueBits = Array.Empty<ushort>(),
                materialIds = Array.Empty<byte>(),
                cellFlags = Array.Empty<byte>(),
                cells = new[]
                {
                    new VoxelDeltaCellDTO
                    {
                        universeKey = unchecked((ulong)universeKey),
                        sdfValue = -0.25f,
                        materialId = 9,
                        flags = malformedFlags,
                        metadata = 0x3344,
                        reserved = 0x55667788u
                    }
                }
            };

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
                Assert.AreEqual(malformedFlags, data.voxelDeltaPersistence.chunks[0].cells[0].flags);

                byte[] cleanCellMarker = new byte[24];
                int markerOffset = 0;
                WritePayloadLong(cleanCellMarker, ref markerOffset, universeKey);
                WritePayloadFloat(cleanCellMarker, ref markerOffset, -0.25f);
                WritePayloadByte(cleanCellMarker, ref markerOffset, 9);
                WritePayloadByte(cleanCellMarker, ref markerOffset, VoxelDeltaChunkDTO.SupportedCellFlags);
                WritePayloadUShort(cleanCellMarker, ref markerOffset, 0x3344);
                WritePayloadUInt(cleanCellMarker, ref markerOffset, 0x55667788u);
                WritePayloadUInt(cleanCellMarker, ref markerOffset, 0);
                Assert.AreEqual(cleanCellMarker.Length, markerOffset);

                int cellOffset = FindLittleEndianByteSequenceOffset(payload, bytesWritten, cleanCellMarker);
                Assert.GreaterOrEqual(cellOffset, 0);
                Assert.AreEqual(VoxelDeltaChunkDTO.SupportedCellFlags, payload[cellOffset + 13]);
                payload[cellOffset + 13] = malformedFlags;

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                VoxelDeltaChunkDTO chunk = restored.voxelDeltaPersistence.chunks[0];
                Assert.AreEqual(1, chunk.cellCount);
                Assert.AreEqual(1, chunk.cells.Length);
                VoxelDeltaCellDTO cell = chunk.cells[0];
                Assert.AreEqual(unchecked((ulong)universeKey), cell.universeKey);
                Assert.AreEqual(-0.25f, cell.sdfValue);
                Assert.AreEqual((byte)9, cell.materialId);
                Assert.AreEqual(VoxelDeltaChunkDTO.SupportedCellFlags, cell.flags);
                Assert.AreEqual((ushort)0x3344, cell.metadata);
                Assert.AreEqual(0x55667788u, cell.reserved);
            }
        }

        [Test]
        public void VoxelDeltaRuntime_ReadsV76DenseChunkWithoutCellFlags()
        {
            const int dirtyCellIndex = 42;
            const int dirtyWordIndex = dirtyCellIndex / 32;
            const uint dirtyCellBit = 1u << (dirtyCellIndex & 31);

            SaveData data = SaveData.CreateNew(42.0);
            uint[] dirtyMaskWords = new uint[VoxelDeltaChunkDTO.DirtyMaskWordCount];
            ushort[] sdfValueBits = new ushort[VoxelDeltaChunkDTO.CellCount];
            byte[] materialIds = new byte[VoxelDeltaChunkDTO.CellCount];
            dirtyMaskWords[dirtyWordIndex] = dirtyCellBit;
            sdfValueBits[dirtyCellIndex] = 0x4321;
            materialIds[dirtyCellIndex] = 7;

            data.voxelDeltaPersistence.EnsureCapacity(1);
            data.voxelDeltaPersistence.chunkCount = 1;
            data.voxelDeltaPersistence.totalCellCount = 1;
            data.voxelDeltaPersistence.chunks[0] = new VoxelDeltaChunkDTO
            {
                chunkX = 11,
                chunkY = -12,
                chunkZ = 13,
                voxelSize = 0.25f,
                cellCount = 1,
                storageFlags = VoxelDeltaChunkDTO.StorageDense,
                dirtyMaskWords = dirtyMaskWords,
                sdfValueBits = sdfValueBits,
                materialIds = materialIds,
                cellFlags = new byte[VoxelDeltaChunkDTO.CellCount],
                cells = Array.Empty<VoxelDeltaCellDTO>()
            };

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                PatchPayloadInt(payload, 0, SaveData.VoxelDeltaPersistenceVersion);
                byte[] marker = BuildVoxelDeltaDenseChunkHeaderMarker(
                    1,
                    1,
                    11,
                    -12,
                    13,
                    0.25f,
                    VoxelDeltaChunkDTO.DirtyMaskWordCount);
                int markerOffset = FindLittleEndianByteSequenceOffset(payload, bytesWritten, marker);
                Assert.GreaterOrEqual(markerOffset, 0);

                int dirtyMaskValuesOffset = markerOffset + marker.Length;
                int sdfCountOffset = dirtyMaskValuesOffset + (VoxelDeltaChunkDTO.DirtyMaskWordCount * sizeof(uint));
                int sdfValuesOffset = sdfCountOffset + sizeof(int);
                int materialCountOffset = sdfValuesOffset + (VoxelDeltaChunkDTO.CellCount * sizeof(ushort));
                int materialValuesOffset = materialCountOffset + sizeof(int);
                int cellFlagsCountOffset = materialValuesOffset + VoxelDeltaChunkDTO.CellCount;
                int cellFlagsPayloadBytes = sizeof(int) + VoxelDeltaChunkDTO.CellCount;
                byte[] legacyPayload = new byte[bytesWritten - cellFlagsPayloadBytes];
                int legacyBytesWritten = RemovePayloadRange(
                    payload,
                    cellFlagsCountOffset,
                    cellFlagsPayloadBytes,
                    bytesWritten,
                    legacyPayload);

                fixed (byte* legacyPayloadPtr = legacyPayload)
                {
                    bool read = SaveBinaryPayloadCodec.TryRead(
                        legacyPayloadPtr,
                        legacyBytesWritten,
                        out SaveData restored,
                        out int bytesRead,
                        out string readError);

                    Assert.IsTrue(read, readError);
                    Assert.AreEqual(legacyBytesWritten, bytesRead);
                    Assert.AreEqual(SaveData.VoxelDeltaPersistenceVersion, restored.version);
                    Assert.AreEqual(1, restored.voxelDeltaPersistence.chunkCount);
                    VoxelDeltaChunkDTO chunk = restored.voxelDeltaPersistence.chunks[0];
                    Assert.AreEqual(1, chunk.cellCount);
                    Assert.AreEqual(dirtyCellBit, chunk.dirtyMaskWords[dirtyWordIndex]);
                    Assert.AreEqual((ushort)0x4321, chunk.sdfValueBits[dirtyCellIndex]);
                    Assert.AreEqual((byte)7, chunk.materialIds[dirtyCellIndex]);
                    Assert.AreEqual(VoxelDeltaChunkDTO.CellCount, chunk.cellFlags.Length);
                    Assert.AreEqual((byte)0, chunk.cellFlags[dirtyCellIndex]);
                }

                byte[] legacyNoTailPayload = new byte[legacyBytesWritten - sizeof(int)];
                int legacyNoTailBytesWritten = RemovePayloadRange(
                    legacyPayload,
                    legacyBytesWritten - sizeof(int),
                    sizeof(int),
                    legacyBytesWritten,
                    legacyNoTailPayload);

                fixed (byte* legacyNoTailPayloadPtr = legacyNoTailPayload)
                {
                    bool read = SaveBinaryPayloadCodec.TryRead(
                        legacyNoTailPayloadPtr,
                        legacyNoTailBytesWritten,
                        out SaveData restored,
                        out int bytesRead,
                        out string readError);

                    Assert.IsTrue(read, readError);
                    Assert.AreEqual(legacyNoTailBytesWritten, bytesRead);
                    Assert.AreEqual(SaveData.VoxelDeltaPersistenceVersion, restored.version);
                    Assert.AreEqual(1, restored.voxelDeltaPersistence.chunkCount);
                    Assert.AreEqual(0, restored.voxelDeltaPersistence.carvingOperationCount);
                    Assert.AreEqual(0, restored.voxelDeltaPersistence.carvingOperations.Length);
                    VoxelDeltaChunkDTO chunk = restored.voxelDeltaPersistence.chunks[0];
                    Assert.AreEqual(1, chunk.cellCount);
                    Assert.AreEqual(dirtyCellBit, chunk.dirtyMaskWords[dirtyWordIndex]);
                    Assert.AreEqual((ushort)0x4321, chunk.sdfValueBits[dirtyCellIndex]);
                    Assert.AreEqual((byte)7, chunk.materialIds[dirtyCellIndex]);
                    Assert.AreEqual(VoxelDeltaChunkDTO.CellCount, chunk.cellFlags.Length);
                    Assert.AreEqual((byte)0, chunk.cellFlags[dirtyCellIndex]);
                }
            }
        }

        [Test]
        public void VoxelDeltaRuntime_ReadsV76DenseChunkWithoutCellFlagsWhenCarvingCountMatchesCellCount()
        {
            const int dirtyCellIndex = 42;
            const int dirtyWordIndex = dirtyCellIndex / 32;
            const uint dirtyCellBit = 1u << (dirtyCellIndex & 31);
            const int sampledOperationIndex = 1234;

            SaveData data = SaveData.CreateNew(42.0);
            uint[] dirtyMaskWords = new uint[VoxelDeltaChunkDTO.DirtyMaskWordCount];
            ushort[] sdfValueBits = new ushort[VoxelDeltaChunkDTO.CellCount];
            byte[] materialIds = new byte[VoxelDeltaChunkDTO.CellCount];
            VoxelCarvingOperationDTO[] carvingOperations = new VoxelCarvingOperationDTO[VoxelDeltaChunkDTO.CellCount];
            dirtyMaskWords[dirtyWordIndex] = dirtyCellBit;
            sdfValueBits[dirtyCellIndex] = 0x4321;
            materialIds[dirtyCellIndex] = 7;
            for (int i = 0; i < carvingOperations.Length; i++)
            {
                carvingOperations[i] = new VoxelCarvingOperationDTO
                {
                    localPosition = new Unity.Mathematics.float3(i, i + 1, i + 2),
                    radius = 1f + (i % 8),
                    operation = (i & 1) == 0 ? VoxelCarvingOperationKind.Subtract : VoxelCarvingOperationKind.Add,
                    materialId = (byte)(i & 0xFF),
                    flags = (ushort)(i & 0xFFFF),
                    sequence = (uint)i
                };
            }

            data.voxelDeltaPersistence.EnsureCapacity(1);
            data.voxelDeltaPersistence.chunkCount = 1;
            data.voxelDeltaPersistence.totalCellCount = 1;
            data.voxelDeltaPersistence.carvingOperationCount = carvingOperations.Length;
            data.voxelDeltaPersistence.carvingOperations = carvingOperations;
            data.voxelDeltaPersistence.chunks[0] = new VoxelDeltaChunkDTO
            {
                chunkX = 11,
                chunkY = -12,
                chunkZ = 13,
                voxelSize = 0.25f,
                cellCount = 1,
                storageFlags = VoxelDeltaChunkDTO.StorageDense,
                dirtyMaskWords = dirtyMaskWords,
                sdfValueBits = sdfValueBits,
                materialIds = materialIds,
                cellFlags = new byte[VoxelDeltaChunkDTO.CellCount],
                cells = Array.Empty<VoxelDeltaCellDTO>()
            };

            byte[] payload = new byte[BinaryPayloadScratchBytes * 2];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                PatchPayloadInt(payload, 0, SaveData.VoxelDeltaPersistenceVersion);
                byte[] marker = BuildVoxelDeltaDenseChunkHeaderMarker(
                    1,
                    1,
                    11,
                    -12,
                    13,
                    0.25f,
                    VoxelDeltaChunkDTO.DirtyMaskWordCount);
                int markerOffset = FindLittleEndianByteSequenceOffset(payload, bytesWritten, marker);
                Assert.GreaterOrEqual(markerOffset, 0);

                int dirtyMaskValuesOffset = markerOffset + marker.Length;
                int sdfCountOffset = dirtyMaskValuesOffset + (VoxelDeltaChunkDTO.DirtyMaskWordCount * sizeof(uint));
                int sdfValuesOffset = sdfCountOffset + sizeof(int);
                int materialCountOffset = sdfValuesOffset + (VoxelDeltaChunkDTO.CellCount * sizeof(ushort));
                int materialValuesOffset = materialCountOffset + sizeof(int);
                int cellFlagsCountOffset = materialValuesOffset + VoxelDeltaChunkDTO.CellCount;
                int cellFlagsPayloadBytes = sizeof(int) + VoxelDeltaChunkDTO.CellCount;
                byte[] legacyPayload = new byte[bytesWritten - cellFlagsPayloadBytes];
                int legacyBytesWritten = RemovePayloadRange(
                    payload,
                    cellFlagsCountOffset,
                    cellFlagsPayloadBytes,
                    bytesWritten,
                    legacyPayload);

                fixed (byte* legacyPayloadPtr = legacyPayload)
                {
                    bool read = SaveBinaryPayloadCodec.TryRead(
                        legacyPayloadPtr,
                        legacyBytesWritten,
                        out SaveData restored,
                        out int bytesRead,
                        out string readError);

                    Assert.IsTrue(read, readError);
                    Assert.AreEqual(legacyBytesWritten, bytesRead);
                    Assert.AreEqual(SaveData.VoxelDeltaPersistenceVersion, restored.version);
                    Assert.AreEqual(1, restored.voxelDeltaPersistence.chunkCount);
                    VoxelDeltaChunkDTO chunk = restored.voxelDeltaPersistence.chunks[0];
                    Assert.AreEqual(1, chunk.cellCount);
                    Assert.AreEqual(dirtyCellBit, chunk.dirtyMaskWords[dirtyWordIndex]);
                    Assert.AreEqual((ushort)0x4321, chunk.sdfValueBits[dirtyCellIndex]);
                    Assert.AreEqual((byte)7, chunk.materialIds[dirtyCellIndex]);
                    Assert.AreEqual(VoxelDeltaChunkDTO.CellCount, chunk.cellFlags.Length);
                    Assert.AreEqual((byte)0, chunk.cellFlags[dirtyCellIndex]);
                    Assert.AreEqual(VoxelDeltaChunkDTO.CellCount, restored.voxelDeltaPersistence.carvingOperationCount);
                    Assert.AreEqual(VoxelDeltaChunkDTO.CellCount, restored.voxelDeltaPersistence.carvingOperations.Length);
                    VoxelCarvingOperationDTO operation = restored.voxelDeltaPersistence.carvingOperations[sampledOperationIndex];
                    Assert.AreEqual((float)sampledOperationIndex, operation.localPosition.x);
                    Assert.AreEqual((float)(sampledOperationIndex + 1), operation.localPosition.y);
                    Assert.AreEqual((float)(sampledOperationIndex + 2), operation.localPosition.z);
                    Assert.AreEqual(1f + (sampledOperationIndex % 8), operation.radius);
                    Assert.AreEqual(VoxelCarvingOperationKind.Subtract, operation.operation);
                    Assert.AreEqual((byte)(sampledOperationIndex & 0xFF), operation.materialId);
                    Assert.AreEqual((ushort)(sampledOperationIndex & 0xFFFF), operation.flags);
                    Assert.AreEqual((uint)sampledOperationIndex, operation.sequence);
                }
            }
        }

        [Test]
        public void VoxelDeltaRuntime_ReadsV76DenseChunkWithLegacyCellFlags()
        {
            const int dirtyCellIndex = 42;
            const int dirtyWordIndex = dirtyCellIndex / 32;
            const uint dirtyCellBit = 1u << (dirtyCellIndex & 31);

            SaveData data = SaveData.CreateNew(42.0);
            uint[] dirtyMaskWords = new uint[VoxelDeltaChunkDTO.DirtyMaskWordCount];
            ushort[] sdfValueBits = new ushort[VoxelDeltaChunkDTO.CellCount];
            byte[] materialIds = new byte[VoxelDeltaChunkDTO.CellCount];
            byte[] cellFlags = new byte[VoxelDeltaChunkDTO.CellCount];
            dirtyMaskWords[dirtyWordIndex] = dirtyCellBit;
            sdfValueBits[dirtyCellIndex] = 0x4321;
            materialIds[dirtyCellIndex] = 7;
            cellFlags[dirtyCellIndex] = VoxelDeltaChunkDTO.CellFlagReplace;

            data.voxelDeltaPersistence.EnsureCapacity(1);
            data.voxelDeltaPersistence.chunkCount = 1;
            data.voxelDeltaPersistence.totalCellCount = 1;
            data.voxelDeltaPersistence.chunks[0] = new VoxelDeltaChunkDTO
            {
                chunkX = 11,
                chunkY = -12,
                chunkZ = 13,
                voxelSize = 0.25f,
                cellCount = 1,
                storageFlags = VoxelDeltaChunkDTO.StorageDense,
                dirtyMaskWords = dirtyMaskWords,
                sdfValueBits = sdfValueBits,
                materialIds = materialIds,
                cellFlags = cellFlags,
                cells = Array.Empty<VoxelDeltaCellDTO>()
            };

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                PatchPayloadInt(payload, 0, SaveData.VoxelDeltaPersistenceVersion);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(SaveData.VoxelDeltaPersistenceVersion, restored.version);
                Assert.AreEqual(1, restored.voxelDeltaPersistence.chunkCount);
                VoxelDeltaChunkDTO chunk = restored.voxelDeltaPersistence.chunks[0];
                Assert.AreEqual(1, chunk.cellCount);
                Assert.AreEqual(dirtyCellBit, chunk.dirtyMaskWords[dirtyWordIndex]);
                Assert.AreEqual((ushort)0x4321, chunk.sdfValueBits[dirtyCellIndex]);
                Assert.AreEqual((byte)7, chunk.materialIds[dirtyCellIndex]);
                Assert.AreEqual(VoxelDeltaChunkDTO.CellCount, chunk.cellFlags.Length);
                Assert.AreEqual(VoxelDeltaChunkDTO.CellFlagReplace, chunk.cellFlags[dirtyCellIndex]);
            }
        }

        [Test]
        public void VoxelDeltaRuntime_ReadRejectsTamperedTotalCellCount()
        {
            const int dirtyCellIndex = 42;
            const int dirtyWordIndex = dirtyCellIndex / 32;
            const uint dirtyCellBit = 1u << (dirtyCellIndex & 31);

            SaveData data = SaveData.CreateNew(42.0);
            uint[] dirtyMaskWords = new uint[VoxelDeltaChunkDTO.DirtyMaskWordCount];
            ushort[] sdfValueBits = new ushort[VoxelDeltaChunkDTO.CellCount];
            byte[] materialIds = new byte[VoxelDeltaChunkDTO.CellCount];
            dirtyMaskWords[dirtyWordIndex] = dirtyCellBit;
            sdfValueBits[dirtyCellIndex] = 0x4321;
            materialIds[dirtyCellIndex] = 7;

            data.voxelDeltaPersistence.EnsureCapacity(1);
            data.voxelDeltaPersistence.chunkCount = 1;
            data.voxelDeltaPersistence.totalCellCount = 1;
            data.voxelDeltaPersistence.chunks[0] = new VoxelDeltaChunkDTO
            {
                chunkX = 11,
                chunkY = -12,
                chunkZ = 13,
                voxelSize = 0.25f,
                cellCount = 1,
                storageFlags = VoxelDeltaChunkDTO.StorageDense,
                dirtyMaskWords = dirtyMaskWords,
                sdfValueBits = sdfValueBits,
                materialIds = materialIds,
                cellFlags = Array.Empty<byte>(),
                cells = Array.Empty<VoxelDeltaCellDTO>()
            };

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                byte[] marker = BuildVoxelDeltaDenseChunkHeaderMarker(
                    1,
                    1,
                    11,
                    -12,
                    13,
                    0.25f,
                    VoxelDeltaChunkDTO.DirtyMaskWordCount);
                int markerOffset = FindLittleEndianByteSequenceOffset(payload, bytesWritten, marker);
                Assert.GreaterOrEqual(markerOffset, 0);
                PatchPayloadInt(payload, markerOffset + sizeof(int), 2);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out _,
                    out string readError);

                Assert.IsFalse(read);
                Assert.IsNull(restored);
                StringAssert.Contains("Voxel delta total cell count does not match", readError);
            }
        }

        [Test]
        public void VoxelDeltaRuntime_ReadRejectsIncompleteDenseChunkPayload()
        {
            const int dirtyCellIndex = 42;
            const int dirtyWordIndex = dirtyCellIndex / 32;
            const uint dirtyCellBit = 1u << (dirtyCellIndex & 31);

            SaveData data = SaveData.CreateNew(42.0);
            uint[] dirtyMaskWords = new uint[VoxelDeltaChunkDTO.DirtyMaskWordCount];
            ushort[] sdfValueBits = new ushort[VoxelDeltaChunkDTO.CellCount];
            byte[] materialIds = new byte[VoxelDeltaChunkDTO.CellCount];
            dirtyMaskWords[dirtyWordIndex] = dirtyCellBit;
            sdfValueBits[dirtyCellIndex] = 0x4321;
            materialIds[dirtyCellIndex] = 7;

            data.voxelDeltaPersistence.EnsureCapacity(1);
            data.voxelDeltaPersistence.chunkCount = 1;
            data.voxelDeltaPersistence.totalCellCount = 1;
            data.voxelDeltaPersistence.chunks[0] = new VoxelDeltaChunkDTO
            {
                chunkX = 11,
                chunkY = -12,
                chunkZ = 13,
                voxelSize = 0.25f,
                cellCount = 1,
                storageFlags = VoxelDeltaChunkDTO.StorageDense,
                dirtyMaskWords = dirtyMaskWords,
                sdfValueBits = sdfValueBits,
                materialIds = materialIds,
                cellFlags = Array.Empty<byte>(),
                cells = Array.Empty<VoxelDeltaCellDTO>()
            };

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                byte[] marker = BuildVoxelDeltaDenseChunkHeaderMarker(
                    1,
                    1,
                    11,
                    -12,
                    13,
                    0.25f,
                    VoxelDeltaChunkDTO.DirtyMaskWordCount);
                int markerOffset = FindLittleEndianByteSequenceOffset(payload, bytesWritten, marker);
                Assert.GreaterOrEqual(markerOffset, 0);

                int dirtyMaskCountOffset = markerOffset + marker.Length - sizeof(int);
                int dirtyMaskValuesOffset = markerOffset + marker.Length;
                int dirtyMaskValuesByteCount = VoxelDeltaChunkDTO.DirtyMaskWordCount * sizeof(uint);
                PatchPayloadInt(payload, dirtyMaskCountOffset, 0);
                byte[] malformedPayload = new byte[bytesWritten - dirtyMaskValuesByteCount];
                int malformedBytesWritten = RemovePayloadRange(
                    payload,
                    dirtyMaskValuesOffset,
                    dirtyMaskValuesByteCount,
                    bytesWritten,
                    malformedPayload);

                fixed (byte* malformedPayloadPtr = malformedPayload)
                {
                    bool read = SaveBinaryPayloadCodec.TryRead(
                        malformedPayloadPtr,
                        malformedBytesWritten,
                        out SaveData restored,
                        out _,
                        out string readError);

                    Assert.IsFalse(read);
                    Assert.IsNull(restored);
                    StringAssert.Contains("Voxel delta dense storage arrays are incomplete", readError);
                }
            }
        }

        [Test]
        public void VoxelDeltaRuntime_RoundTripsCarvingOperationsThroughBinaryPayload()
        {
            SaveData data = SaveData.CreateNew(42.0);
            data.voxelDeltaPersistence.carvingOperationCount = 1;
            data.voxelDeltaPersistence.carvingOperations = new[]
            {
                new VoxelCarvingOperationDTO
                {
                    localPosition = new Unity.Mathematics.float3(1f, 2f, 3f),
                    radius = 4.5f,
                    operation = VoxelCarvingOperationKind.Add,
                    materialId = 9,
                    flags = 0x1234,
                    sequence = 77u
                }
            };

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(0, restored.voxelDeltaPersistence.chunkCount);
                Assert.AreEqual(0, restored.voxelDeltaPersistence.totalCellCount);
                Assert.AreEqual(1, restored.voxelDeltaPersistence.carvingOperationCount);
                Assert.AreEqual(1, restored.voxelDeltaPersistence.carvingOperations.Length);
                VoxelCarvingOperationDTO operation = restored.voxelDeltaPersistence.carvingOperations[0];
                Assert.AreEqual(1f, operation.localPosition.x);
                Assert.AreEqual(2f, operation.localPosition.y);
                Assert.AreEqual(3f, operation.localPosition.z);
                Assert.AreEqual(4.5f, operation.radius);
                Assert.AreEqual(VoxelCarvingOperationKind.Add, operation.operation);
                Assert.AreEqual((byte)9, operation.materialId);
                Assert.AreEqual((ushort)0x1234, operation.flags);
                Assert.AreEqual(77u, operation.sequence);
            }
        }

        [Test]
        public void VoxelDeltaRuntime_ReadsLegacyPayloadWithoutCarvingOperationTail()
        {
            const int dirtyCellIndex = 42;
            const int dirtyWordIndex = dirtyCellIndex / 32;
            const uint dirtyCellBit = 1u << (dirtyCellIndex & 31);

            SaveData data = SaveData.CreateNew(42.0);
            uint[] dirtyMaskWords = new uint[VoxelDeltaChunkDTO.DirtyMaskWordCount];
            ushort[] sdfValueBits = new ushort[VoxelDeltaChunkDTO.CellCount];
            byte[] materialIds = new byte[VoxelDeltaChunkDTO.CellCount];
            byte[] cellFlags = new byte[VoxelDeltaChunkDTO.CellCount];
            dirtyMaskWords[dirtyWordIndex] = dirtyCellBit;
            sdfValueBits[dirtyCellIndex] = 0x4321;
            materialIds[dirtyCellIndex] = 7;
            cellFlags[dirtyCellIndex] = VoxelDeltaChunkDTO.CellFlagAdditive;

            data.voxelDeltaPersistence.EnsureCapacity(1);
            data.voxelDeltaPersistence.chunkCount = 1;
            data.voxelDeltaPersistence.totalCellCount = 1;
            data.voxelDeltaPersistence.chunks[0] = new VoxelDeltaChunkDTO
            {
                chunkX = 11,
                chunkY = -12,
                chunkZ = 13,
                voxelSize = 0.25f,
                cellCount = 1,
                storageFlags = VoxelDeltaChunkDTO.StorageDense,
                dirtyMaskWords = dirtyMaskWords,
                sdfValueBits = sdfValueBits,
                materialIds = materialIds,
                cellFlags = cellFlags,
                cells = Array.Empty<VoxelDeltaCellDTO>()
            };

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                PatchPayloadInt(payload, 0, SaveData.VoxelDeltaPersistenceVersion);
                byte[] legacyPayload = new byte[bytesWritten - sizeof(int)];
                int legacyBytesWritten = RemovePayloadRange(
                    payload,
                    bytesWritten - sizeof(int),
                    sizeof(int),
                    bytesWritten,
                    legacyPayload);

                fixed (byte* legacyPayloadPtr = legacyPayload)
                {
                    bool read = SaveBinaryPayloadCodec.TryRead(
                        legacyPayloadPtr,
                        legacyBytesWritten,
                        out SaveData restored,
                        out int bytesRead,
                        out string readError);

                    Assert.IsTrue(read, readError);
                    Assert.AreEqual(legacyBytesWritten, bytesRead);
                    Assert.AreEqual(SaveData.VoxelDeltaPersistenceVersion, restored.version);
                    Assert.AreEqual(1, restored.voxelDeltaPersistence.chunkCount);
                    Assert.AreEqual(1, restored.voxelDeltaPersistence.totalCellCount);
                    Assert.AreEqual(0, restored.voxelDeltaPersistence.carvingOperationCount);
                    Assert.AreEqual(0, restored.voxelDeltaPersistence.carvingOperations.Length);
                    VoxelDeltaChunkDTO chunk = restored.voxelDeltaPersistence.chunks[0];
                    Assert.AreEqual(dirtyCellBit, chunk.dirtyMaskWords[dirtyWordIndex]);
                    Assert.AreEqual((ushort)0x4321, chunk.sdfValueBits[dirtyCellIndex]);
                    Assert.AreEqual((byte)7, chunk.materialIds[dirtyCellIndex]);
                    Assert.AreEqual(VoxelDeltaChunkDTO.CellFlagAdditive, chunk.cellFlags[dirtyCellIndex]);
                }
            }
        }

        [Test]
        public void VoxelDeltaRuntime_ReadRejectsCurrentPayloadWithoutCarvingOperationTail()
        {
            SaveData data = SaveData.CreateNew(42.0);
            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                byte[] truncatedPayload = new byte[bytesWritten - sizeof(int)];
                int truncatedBytesWritten = RemovePayloadRange(
                    payload,
                    bytesWritten - sizeof(int),
                    sizeof(int),
                    bytesWritten,
                    truncatedPayload);

                fixed (byte* truncatedPayloadPtr = truncatedPayload)
                {
                    bool read = SaveBinaryPayloadCodec.TryRead(
                        truncatedPayloadPtr,
                        truncatedBytesWritten,
                        out SaveData restored,
                        out _,
                        out string readError);

                    Assert.IsFalse(read);
                    Assert.IsNull(restored);
                    StringAssert.Contains("Voxel carving operation payload is missing.", readError);
                }
            }
        }

        [Test]
        public void VoxelDeltaRuntime_WriteSanitizesMalformedCarvingOperations()
        {
            SaveData data = SaveData.CreateNew(42.0);
            data.voxelDeltaPersistence.carvingOperationCount = 1;
            data.voxelDeltaPersistence.carvingOperations = new[]
            {
                new VoxelCarvingOperationDTO
                {
                    localPosition = new Unity.Mathematics.float3(float.NaN, float.PositiveInfinity, 3f),
                    radius = float.NegativeInfinity,
                    operation = (VoxelCarvingOperationKind)99,
                    materialId = 9,
                    flags = 0x1234,
                    sequence = 77u
                }
            };

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.AreEqual(0, CountLittleEndianFloat(payload, bytesWritten, float.NaN));
                Assert.AreEqual(0, CountLittleEndianFloat(payload, bytesWritten, float.PositiveInfinity));
                Assert.AreEqual(0, CountLittleEndianFloat(payload, bytesWritten, float.NegativeInfinity));

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(1, restored.voxelDeltaPersistence.carvingOperationCount);
                VoxelCarvingOperationDTO operation = restored.voxelDeltaPersistence.carvingOperations[0];
                Assert.AreEqual(0f, operation.localPosition.x);
                Assert.AreEqual(0f, operation.localPosition.y);
                Assert.AreEqual(3f, operation.localPosition.z);
                Assert.AreEqual(0f, operation.radius);
                Assert.AreEqual(VoxelCarvingOperationKind.Subtract, operation.operation);
                Assert.AreEqual((byte)9, operation.materialId);
                Assert.AreEqual((ushort)0x1234, operation.flags);
                Assert.AreEqual(77u, operation.sequence);
            }
        }

        [Test]
        public void VoxelDeltaNativeSnapshot_ReadRejectsMixedStorageFlags()
        {
            byte[] snapshot = BuildAlignedVoxelDeltaNativeSnapshot(
                storageFlags: 3,
                voxelSize: 0.25f,
                dirtyCellCount: 0,
                payloadByteLength: 0,
                appendTrailingBytes: false);

            AssertVoxelDeltaNativeSnapshotRejected(
                snapshot,
                "Voxel delta snapshot storage flags are outside the supported range");
        }

        [Test]
        public void VoxelDeltaNativeSnapshot_ReadRejectsInvalidDeltaRleChunkHeader()
        {
            byte[] snapshot = BuildAlignedVoxelDeltaNativeSnapshot(
                storageFlags: 0,
                voxelSize: -1f,
                dirtyCellCount: 0,
                payloadByteLength: 0,
                appendTrailingBytes: true);

            AssertVoxelDeltaNativeSnapshotRejected(
                snapshot,
                "Voxel delta chunk header contains invalid values");
        }

        [Test]
        public void VoxelDeltaNativeSnapshot_ReadRejectsNonFiniteVoxelSize()
        {
            byte[] snapshot = BuildAlignedVoxelDeltaNativeSnapshot(
                storageFlags: VoxelDeltaChunkDTO.StorageUniformSdfRle,
                voxelSize: float.NaN,
                dirtyCellCount: VoxelDeltaChunkDTO.CellCount,
                payloadByteLength: VoxelDeltaChunkDTO.UniformSdfRlePayloadBytes,
                appendTrailingBytes: false,
                writeValidPayloadHash: true);

            AssertVoxelDeltaNativeSnapshotRejected(
                snapshot,
                "Voxel delta chunk header contains invalid values");
        }

        [Test]
        public void VoxelDeltaNativeSnapshot_ReadRejectsDeltaRlePayloadHashMismatch()
        {
            byte[] snapshot = BuildAlignedVoxelDeltaNativeSnapshot(
                storageFlags: 0,
                voxelSize: 0.25f,
                dirtyCellCount: 0,
                payloadByteLength: 1,
                appendTrailingBytes: false);

            AssertVoxelDeltaNativeSnapshotRejected(
                snapshot,
                "Voxel delta delta-RLE payload hash mismatch");
        }

        [Test]
        public void VoxelDeltaNativeSnapshot_ReadRejectsDenseDirtyMaskCountMismatch()
        {
            int densePayloadBytes =
                (VoxelDeltaChunkDTO.DirtyMaskWordCount * sizeof(uint)) +
                (VoxelDeltaChunkDTO.CellCount * sizeof(ushort)) +
                VoxelDeltaChunkDTO.CellCount +
                VoxelDeltaChunkDTO.CellCount;
            byte[] snapshot = BuildAlignedVoxelDeltaNativeSnapshot(
                storageFlags: 0,
                voxelSize: 0.25f,
                dirtyCellCount: 1,
                payloadByteLength: densePayloadBytes,
                appendTrailingBytes: false,
                writeValidPayloadHash: true);

            AssertVoxelDeltaNativeSnapshotRejected(
                snapshot,
                "Voxel delta dense dirty-mask count does not match the chunk header");
        }

        [Test]
        public void VoxelDeltaNativeSnapshot_ReadRejectsSparseRlePayloadBoundsBeforeDecode()
        {
            byte[] snapshot = BuildLegacyRleVoxelDeltaNativeSnapshot(
                storageFlags: 2,
                voxelSize: 0.25f,
                dirtyCellCount: 1,
                declaredPayloadByteLength: 1024);

            AssertVoxelDeltaNativeSnapshotRejected(
                snapshot,
                "Voxel delta sparse RLE payload exceeds the snapshot bounds");
        }

        [Test]
        public void BinaryPayloadRuntime_ReadRejectsNonCanonicalBooleanBytes()
        {
            const uint workerHashA = 0xABCDEF01u;
            const uint workerHashB = 0x12345678u;

            SaveData data = SaveData.CreateNew(42.0);
            data.atlas6LiabilitySectorXenonOmegaYield = 321.5f;
            data.atlas6LiabilityHasDisasterEvidence = true;
            data.atlas6LiabilityRecoveredWorkerTagCount = 2;
            data.atlas6LiabilityRecoveredWorkerTagHashes[0] = workerHashA;
            data.atlas6LiabilityRecoveredWorkerTagHashes[1] = workerHashB;
            data.atlas6LiabilityCorporateHostilityIndex = 31.25f;
            data.atlas6LiabilityCorporateCreditBalance = 4700f;
            data.atlas6LiabilityExtractionCarrierState = 3;
            data.atlas6LiabilityBiomatterExposureLevel = 44.5f;
            data.atlas6LiabilityHaldaneLockoutActive = true;
            data.atlas6LiabilityPressureSealIntegrity = 0.25f;
            data.atlas6LiabilityBulkheadLocked = true;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            int bytesWritten;
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
            }

            byte[] atlas6Marker = BuildAtlas6LiabilityMarker(
                321.5f,
                true,
                new[] { workerHashA, workerHashB },
                31.25f,
                4700f,
                3,
                44.5f,
                true,
                0.25f,
                true);
            int atlas6Offset = FindLittleEndianByteSequenceOffset(payload, bytesWritten, atlas6Marker);
            Assert.GreaterOrEqual(atlas6Offset, 0);

            const int hasDisasterEvidenceOffsetInsideAtlas6Marker = sizeof(float);
            payload[atlas6Offset + hasDisasterEvidenceOffsetInsideAtlas6Marker] = 2;

            fixed (byte* payloadPtr = payload)
            {
                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out _,
                    out string readError);

                Assert.IsFalse(read);
                Assert.IsNull(restored);
                StringAssert.Contains("Boolean flag byte is outside canonical 0/1 range.", readError);
            }
        }

        [Test]
        public void Atlas6LiabilityRuntime_PreV75BinaryPayloadReadsDefaultsWithoutAtlas6Bytes()
        {
            const float hazardDoseMarker = 12.25f;
            const float hazardPulseMarker = 0.25f;
            const uint workerHashA = 0xABCDEF01u;
            const uint workerHashB = 0x12345678u;

            SaveData data = SaveData.CreateNew(42.0);
            data.hazardZones.toxicityDose = hazardDoseMarker;
            data.hazardZones.toxicityPulseAccumulatorSeconds = hazardPulseMarker;
            data.atlas6LiabilitySectorXenonOmegaYield = 321.5f;
            data.atlas6LiabilityHasDisasterEvidence = true;
            data.atlas6LiabilityRecoveredWorkerTagCount = 2;
            data.atlas6LiabilityRecoveredWorkerTagHashes[0] = workerHashA;
            data.atlas6LiabilityRecoveredWorkerTagHashes[1] = workerHashB;
            data.atlas6LiabilityCorporateHostilityIndex = 31.25f;
            data.atlas6LiabilityCorporateCreditBalance = 4700f;
            data.atlas6LiabilityExtractionCarrierState = 3;
            data.atlas6LiabilityBiomatterExposureLevel = 44.5f;
            data.atlas6LiabilityHaldaneLockoutActive = true;
            data.atlas6LiabilityPressureSealIntegrity = 0.25f;
            data.atlas6LiabilityBulkheadLocked = true;

            byte[] currentPayload = new byte[BinaryPayloadScratchBytes];
            int currentBytesWritten;
            fixed (byte* currentPayloadPtr = currentPayload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    currentPayloadPtr,
                    currentPayload.Length,
                    out currentBytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(currentBytesWritten, 0);
            }

            byte[] withoutVoxelPayload = RemoveCurrentDefaultVoxelDeltaPayload(
                currentPayload,
                currentBytesWritten,
                out int withoutVoxelBytesWritten);
            byte[] atlas6Marker = BuildAtlas6LiabilityMarker(
                321.5f,
                true,
                new[] { workerHashA, workerHashB },
                31.25f,
                4700f,
                3,
                44.5f,
                true,
                0.25f,
                true);
            int atlas6Offset = FindLittleEndianByteSequenceOffset(
                withoutVoxelPayload,
                withoutVoxelBytesWritten,
                atlas6Marker);
            Assert.GreaterOrEqual(atlas6Offset, 0);

            int legacyBytesWritten = withoutVoxelBytesWritten - atlas6Marker.Length;
            byte[] legacyPayload = new byte[legacyBytesWritten];
            RemovePayloadRange(
                withoutVoxelPayload,
                atlas6Offset,
                atlas6Marker.Length,
                withoutVoxelBytesWritten,
                legacyPayload);
            Buffer.BlockCopy(
                BitConverter.GetBytes(SaveData.Atlas6LiabilityPersistenceVersion - 1),
                0,
                legacyPayload,
                0,
                sizeof(int));

            fixed (byte* legacyPayloadPtr = legacyPayload)
            {
                bool read = SaveBinaryPayloadCodec.TryRead(
                    legacyPayloadPtr,
                    legacyPayload.Length,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(legacyBytesWritten, bytesRead);
                Assert.AreEqual(SaveData.Atlas6LiabilityPersistenceVersion - 1, restored.version);
                Assert.AreEqual(hazardDoseMarker, restored.hazardZones.toxicityDose);
                Assert.AreEqual(hazardPulseMarker, restored.hazardZones.toxicityPulseAccumulatorSeconds);
                Assert.AreEqual(0f, restored.atlas6LiabilitySectorXenonOmegaYield);
                Assert.IsFalse(restored.atlas6LiabilityHasDisasterEvidence);
                Assert.AreEqual(0, restored.atlas6LiabilityRecoveredWorkerTagCount);
                Assert.AreEqual(5000f, restored.atlas6LiabilityCorporateCreditBalance);
                Assert.AreEqual(1f, restored.atlas6LiabilityPressureSealIntegrity);
            }
        }

        [Test]
        public void HazardZoneRuntime_PreV74BinaryPayloadReadsWithoutHazardBytes()
        {
            const float hazardDoseMarker = 12.25f;
            const float hazardPulseMarker = 0.25f;
            const uint workerHashA = 0xABCDEF01u;
            const uint workerHashB = 0x12345678u;

            SaveData data = SaveData.CreateNew(42.0);
            data.hazardZones.toxicityDose = hazardDoseMarker;
            data.hazardZones.toxicityPulseAccumulatorSeconds = hazardPulseMarker;
            data.atlas6LiabilitySectorXenonOmegaYield = 321.5f;
            data.atlas6LiabilityHasDisasterEvidence = true;
            data.atlas6LiabilityRecoveredWorkerTagCount = 2;
            data.atlas6LiabilityRecoveredWorkerTagHashes[0] = workerHashA;
            data.atlas6LiabilityRecoveredWorkerTagHashes[1] = workerHashB;
            data.atlas6LiabilityCorporateHostilityIndex = 31.25f;
            data.atlas6LiabilityCorporateCreditBalance = 4700f;
            data.atlas6LiabilityExtractionCarrierState = 3;
            data.atlas6LiabilityBiomatterExposureLevel = 44.5f;
            data.atlas6LiabilityHaldaneLockoutActive = true;
            data.atlas6LiabilityPressureSealIntegrity = 0.25f;
            data.atlas6LiabilityBulkheadLocked = true;

            byte[] currentPayload = new byte[BinaryPayloadScratchBytes];
            int currentBytesWritten;
            fixed (byte* currentPayloadPtr = currentPayload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    currentPayloadPtr,
                    currentPayload.Length,
                    out currentBytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(currentBytesWritten, 0);
            }

            byte[] withoutVoxelPayload = RemoveCurrentDefaultVoxelDeltaPayload(
                currentPayload,
                currentBytesWritten,
                out int withoutVoxelBytesWritten);
            int hazardBytes = sizeof(float) * 2;
            int hazardOffset = FindLittleEndianFloatPairOffset(
                withoutVoxelPayload,
                withoutVoxelBytesWritten,
                hazardDoseMarker,
                hazardPulseMarker);
            Assert.GreaterOrEqual(hazardOffset, sizeof(int));
            Assert.AreEqual(1, CountLittleEndianFloatPair(
                withoutVoxelPayload,
                withoutVoxelBytesWritten,
                hazardDoseMarker,
                hazardPulseMarker));

            byte[] withoutHazardPayload = new byte[withoutVoxelBytesWritten - hazardBytes];
            int withoutHazardBytesWritten = RemovePayloadRange(
                withoutVoxelPayload,
                hazardOffset,
                hazardBytes,
                withoutVoxelBytesWritten,
                withoutHazardPayload);

            byte[] atlas6Marker = BuildAtlas6LiabilityMarker(
                321.5f,
                true,
                new[] { workerHashA, workerHashB },
                31.25f,
                4700f,
                3,
                44.5f,
                true,
                0.25f,
                true);
            int atlas6Offset = FindLittleEndianByteSequenceOffset(
                withoutHazardPayload,
                withoutHazardBytesWritten,
                atlas6Marker);
            Assert.GreaterOrEqual(atlas6Offset, 0);

            int legacyBytesWritten = withoutHazardBytesWritten - atlas6Marker.Length;
            byte[] legacyPayload = new byte[legacyBytesWritten];
            RemovePayloadRange(
                withoutHazardPayload,
                atlas6Offset,
                atlas6Marker.Length,
                withoutHazardBytesWritten,
                legacyPayload);
            Buffer.BlockCopy(
                BitConverter.GetBytes(SaveData.HazardZoneRuntimePersistenceVersion - 1),
                0,
                legacyPayload,
                0,
                sizeof(int));

            fixed (byte* legacyPayloadPtr = legacyPayload)
            {
                bool read = SaveBinaryPayloadCodec.TryRead(
                    legacyPayloadPtr,
                    legacyPayload.Length,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(legacyBytesWritten, bytesRead);
                Assert.AreEqual(SaveData.HazardZoneRuntimePersistenceVersion - 1, restored.version);
                Assert.AreEqual(0f, restored.hazardZones.toxicityDose);
                Assert.AreEqual(0f, restored.hazardZones.toxicityPulseAccumulatorSeconds);
                Assert.AreEqual(0f, restored.atlas6LiabilitySectorXenonOmegaYield);
                Assert.IsFalse(restored.atlas6LiabilityHasDisasterEvidence);
                Assert.AreEqual(0, restored.atlas6LiabilityRecoveredWorkerTagCount);
                Assert.AreEqual(SaveData.MaxAtlas6LiabilityWorkerTags, restored.atlas6LiabilityRecoveredWorkerTagHashes.Length);
                Assert.AreEqual(5000f, restored.atlas6LiabilityCorporateCreditBalance);
                Assert.AreEqual(1f, restored.atlas6LiabilityPressureSealIntegrity);
            }
        }

        [Test]
        public void HazardZoneRuntime_WriteClampsOutOfRangeValues()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.hazardZones.toxicityDose = 128f;
            data.hazardZones.toxicityPulseAccumulatorSeconds = 3f;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(SaveData.HazardZoneMaxPersistedToxicityDose, restored.hazardZones.toxicityDose);
                Assert.AreEqual(
                    SaveData.HazardZoneMaxPersistedToxicityPulseSeconds,
                    restored.hazardZones.toxicityPulseAccumulatorSeconds);
            }
        }

        [Test]
        public void HazardZoneRuntime_WriteClearsInactivePulseBelowDamageThreshold()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.hazardZones.toxicityDose = SaveData.HazardZoneToxicityDamageDoseThreshold * 0.5f;
            data.hazardZones.toxicityPulseAccumulatorSeconds = 0.25f;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(data.hazardZones.toxicityDose, restored.hazardZones.toxicityDose);
                Assert.AreEqual(0f, restored.hazardZones.toxicityPulseAccumulatorSeconds);
            }
        }

        [Test]
        public void HazardZoneRuntime_ReadClampsNonFiniteFileValues()
        {
            const float hazardDoseMarker = 12.25f;
            const float hazardPulseMarker = 0.25f;

            SaveData data = SaveData.CreateNew(0.0);
            data.hazardZones.toxicityDose = hazardDoseMarker;
            data.hazardZones.toxicityPulseAccumulatorSeconds = hazardPulseMarker;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            int bytesWritten;
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
            }

            int hazardOffset = FindLittleEndianFloatPairOffset(
                payload,
                bytesWritten,
                hazardDoseMarker,
                hazardPulseMarker);
            Assert.GreaterOrEqual(hazardOffset, sizeof(int));
            Buffer.BlockCopy(BitConverter.GetBytes(float.NaN), 0, payload, hazardOffset, sizeof(float));
            Buffer.BlockCopy(BitConverter.GetBytes(3f), 0, payload, hazardOffset + sizeof(float), sizeof(float));

            fixed (byte* payloadPtr = payload)
            {
                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(0f, restored.hazardZones.toxicityDose);
                Assert.AreEqual(0f, restored.hazardZones.toxicityPulseAccumulatorSeconds);
            }
        }

        [Test]
        public void RadiationGridRlePersistenceBounds_MatchRuntimeWorstCase()
        {
            Assert.AreEqual(32, SaveData.RadiationGridResolution);
            Assert.AreEqual(
                SaveData.RadiationGridResolution * SaveData.RadiationGridResolution * SaveData.RadiationGridResolution,
                SaveData.RadiationGridCellCount);
            Assert.AreEqual(sizeof(ushort) + sizeof(byte) + sizeof(ushort), SaveData.RadiationGridRlePacketSizeBytes);
            Assert.AreEqual(163840, SaveData.RadiationGridRleMaxBytes);
            Assert.AreEqual(SaveData.RadiationGridResolution, RadiationHazardGrid.GridResolution);
            Assert.AreEqual(SaveData.RadiationGridCellCount, RadiationHazardGrid.GridCellCount);
            Assert.AreEqual(SaveData.RadiationGridRlePacketSizeBytes, RadiationHazardGrid.RlePacketSizeBytes);
            Assert.AreEqual(SaveData.RadiationGridRleMaxBytes, RadiationHazardGrid.MaxRlePayloadBytes);
            Assert.AreEqual(4f, SaveData.RadiationGridDefaultCellSizeMeters);
            Assert.AreEqual(0.5f, SaveData.RadiationGridMinCellSizeMeters);
            Assert.AreEqual(1000f, SaveData.RadiationGridMaxCellSizeMeters);

            SaveData data = SaveData.CreateNew(0.0);
            Assert.IsNotNull(data.radiationGridRle);
            Assert.AreEqual(SaveData.RadiationGridRleMaxBytes, data.radiationGridRle.Length);
            Assert.IsFalse(RadiationHazardGrid.HasPersistedRadiationGridPayload(null, SaveData.RadiationGridRleMaxBytes));
            Assert.IsFalse(RadiationHazardGrid.HasPersistedRadiationGridPayload(
                new byte[SaveData.RadiationGridRlePacketSizeBytes - 1],
                SaveData.RadiationGridRlePacketSizeBytes - 1));
            Assert.IsTrue(RadiationHazardGrid.HasPersistedRadiationGridPayload(
                new byte[SaveData.RadiationGridRlePacketSizeBytes],
                SaveData.RadiationGridRlePacketSizeBytes));
        }

        [Test]
        public void RadiationGridLoad_ClearsTransientStateWithoutDroppingRegisteredSources()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs"));

            Assert.IsFalse(source.Contains("ClearRadiationSourcesForLoad"));
            StringAssert.Contains("ClearGrid(_gridRead);", source);
            StringAssert.Contains("ClearGrid(_gridWrite);", source);
            StringAssert.Contains("ClearGrid(_gridSource);", source);
            StringAssert.Contains("RepairRadiationSourceCountFromBuffer();", source);
            StringAssert.Contains("private void RepairRadiationSourceCountFromBuffer()", source);
            StringAssert.Contains("_hasGridOrigin = false;", source);
            StringAssert.Contains("RestoreGridOriginFromActiveSourceOrDefault();", source);
            StringAssert.Contains("TryResolveFirstActiveRadiationSourceOrigin", source);
            StringAssert.Contains("_lastExternalIntensity01 = 0f;", source);
            StringAssert.Contains("_lastSourceSignalDrainFrame = -1;", source);
            StringAssert.Contains("_lastExternalDoseSignalDrainFrame = -1;", source);
        }

        [Test]
        public void RadiationGridSourceRegister_FailsClosedOnInvalidAup()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs"));

            int vectorOverloadIndex = source.IndexOf(
                "public static void RegisterSource(int sourceId, Vector3 runtimePosition",
                StringComparison.Ordinal);
            int aupOverloadIndex = source.IndexOf(
                "public static void RegisterSource(int sourceId, in AbsoluteUniversePosition sourceAup",
                StringComparison.Ordinal);
            int unregisterIndex = source.IndexOf(
                "public static void UnregisterSource(int sourceId)",
                aupOverloadIndex,
                StringComparison.Ordinal);
            Assert.GreaterOrEqual(vectorOverloadIndex, 0);
            Assert.Greater(aupOverloadIndex, vectorOverloadIndex);
            Assert.Greater(unregisterIndex, aupOverloadIndex);

            string vectorOverload = source.Substring(vectorOverloadIndex, aupOverloadIndex - vectorOverloadIndex);
            StringAssert.Contains("!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition sourceAup)", vectorOverload);
            StringAssert.Contains("UnregisterSource(sourceId);", vectorOverload);
            StringAssert.Contains("RegisterSource(sourceId, in sourceAup, intensity, radiusMeters);", vectorOverload);

            string aupOverload = source.Substring(aupOverloadIndex, unregisterIndex - aupOverloadIndex);
            StringAssert.Contains("!AbsoluteUniversePosition.IsFinite(in sourceAup)", aupOverload);
            StringAssert.Contains("!math.isfinite(radiusMeters)", aupOverload);
            StringAssert.Contains("radiusMeters <= 0f", aupOverload);
            StringAssert.Contains("UnregisterSource(sourceId);", aupOverload);
            StringAssert.Contains("float safeRadius = math.max(0.5f, radiusMeters);", aupOverload);
            StringAssert.DoesNotContain(": DefaultSourceRadiusMeters", aupOverload);
        }

        [Test]
        public void SaveBinaryPayloadCodec_RejectsFutureSaveDataVersion()
        {
            byte[] payload = BitConverter.GetBytes(SaveData.CurrentVersion + 1);

            fixed (byte* payloadPtr = payload)
            {
                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    payload.Length,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsFalse(read);
                Assert.IsNull(restored);
                Assert.AreEqual(0, bytesRead);
                StringAssert.Contains("Unsupported save data version", readError);
            }
        }

        [Test]
        public void SaveBinaryPayloadCodec_RejectsWritingFutureSaveDataVersion()
        {
            SaveData data = SaveData.CreateNew(0.0);
            int futureVersion = SaveData.CurrentVersion + 1;
            data.version = futureVersion;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsFalse(wrote);
                Assert.AreEqual(0, bytesWritten);
                Assert.AreEqual(futureVersion, data.version);
                StringAssert.Contains("Unsupported save data version", writeError);
            }
        }

        [Test]
        public void SaveBinaryPayloadCodec_RejectsWritingUnmigratedOlderSaveDataVersion()
        {
            SaveData data = SaveData.CreateNew(0.0);
            int legacyVersion = SaveData.HazardZoneRuntimePersistenceVersion - 1;
            data.version = legacyVersion;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsFalse(wrote);
                Assert.AreEqual(0, bytesWritten);
                Assert.AreEqual(legacyVersion, data.version);
                StringAssert.Contains("must be migrated before writing", writeError);
            }
        }

        [Test]
        public void SaveRootTime_WriteSanitizesNonFiniteSessionValues()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.totalPlayTime = double.NaN;
            data.firstHourSessionTime = float.PositiveInfinity;
            data.corporatePendingOrderIds.Add("order.a");
            data.corporatePendingOrderTimers.Add(float.NegativeInfinity);

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(0d, restored.totalPlayTime);
                Assert.AreEqual(0f, restored.firstHourSessionTime);
                Assert.AreEqual(1, restored.corporatePendingOrderTimers.Count);
                Assert.AreEqual(0f, restored.corporatePendingOrderTimers[0]);
            }
        }

        [Test]
        public void SaveRootRuntime_WriteSanitizesMalformedNarrativeAndLodValues()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.narrativeDepthTier = -4;
            data.LODQualityPreset = 99;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(0, restored.narrativeDepthTier);
                Assert.AreEqual(1, restored.LODQualityPreset);
            }
        }

        [Test]
        public void SaveRootRuntime_WriteCanonicalizesNullOperationalLogStrings()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.scanLog.EnsureCapacity();
            data.scanLog.entryCount = 1;
            data.scanLog.entries[0] = new ScanEntryDTO { title = "Scan Title" };
            data.scanLog.recentCount = 1;
            data.scanLog.recentEntryIds[0] = null;

            data.barter.EnsureCapacity();
            data.barter.stateCount = 1;
            data.barter.offerStates[0] = new BarterOfferStateDTO
            {
                offerId = null,
                executionCount = -7
            };
            data.barter.recentTransactionCount = 1;
            data.barter.recentTransactions[0] = new BarterTransactionDTO
            {
                offerName = "Recovered Offer"
            };

            data.fieldOperations.EnsureCapacity();
            data.fieldOperations.recentCount = 1;
            data.fieldOperations.recentEntries[0] = new FieldOperationEntryDTO
            {
                title = "Field Title"
            };

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(string.Empty, restored.scanLog.entries[0].id);
                Assert.AreEqual("Scan Title", restored.scanLog.entries[0].title);
                Assert.AreEqual(string.Empty, restored.scanLog.entries[0].category);
                Assert.AreEqual(string.Empty, restored.scanLog.entries[0].summary);
                Assert.AreEqual(0, restored.scanLog.recentCount);
                Assert.AreEqual(string.Empty, restored.barter.offerStates[0].offerId);
                Assert.AreEqual(0, restored.barter.offerStates[0].executionCount);
                Assert.AreEqual(string.Empty, restored.barter.recentTransactions[0].offerId);
                Assert.AreEqual("Recovered Offer", restored.barter.recentTransactions[0].offerName);
                Assert.AreEqual(string.Empty, restored.barter.recentTransactions[0].channelName);
                Assert.AreEqual(string.Empty, restored.barter.recentTransactions[0].costSummary);
                Assert.AreEqual(string.Empty, restored.barter.recentTransactions[0].rewardSummary);
                Assert.AreEqual(string.Empty, restored.fieldOperations.recentEntries[0].source);
                Assert.AreEqual("Field Title", restored.fieldOperations.recentEntries[0].title);
                Assert.AreEqual(string.Empty, restored.fieldOperations.recentEntries[0].summary);
                Assert.AreEqual(string.Empty, restored.fieldOperations.recentEntries[0].severity);
            }
        }

        [Test]
        public void SaveRootRuntime_WriteCompactsBlankIdOnlyArrays()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.narrativeDiscoveryCount = 4;
            data.narrativeDiscoveryIds[0] = "narrative.alpha";
            data.narrativeDiscoveryIds[1] = null;
            data.narrativeDiscoveryIds[2] = " ";
            data.narrativeDiscoveryIds[3] = "narrative.beta";

            data.worldState.EnsureCapacity();
            data.worldState.depletedCount = 4;
            data.worldState.depletedNodeIds[0] = "node.alpha";
            data.worldState.depletedNodeIds[1] = null;
            data.worldState.depletedNodeIds[2] = "\t";
            data.worldState.depletedNodeIds[3] = "node.beta";

            data.scanLog.EnsureCapacity();
            data.scanLog.recentCount = 4;
            data.scanLog.recentEntryIds[0] = "scan.alpha";
            data.scanLog.recentEntryIds[1] = null;
            data.scanLog.recentEntryIds[2] = " ";
            data.scanLog.recentEntryIds[3] = "scan.beta";

            data.achievements.EnsureCapacity();
            data.achievements.unlockedCount = 4;
            data.achievements.unlockedIds[0] = "achievement.alpha";
            data.achievements.unlockedIds[1] = null;
            data.achievements.unlockedIds[2] = " ";
            data.achievements.unlockedIds[3] = "achievement.beta";

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(2, restored.narrativeDiscoveryCount);
                Assert.AreEqual("narrative.alpha", restored.narrativeDiscoveryIds[0]);
                Assert.AreEqual("narrative.beta", restored.narrativeDiscoveryIds[1]);
                Assert.AreEqual(2, restored.worldState.depletedCount);
                Assert.AreEqual("node.alpha", restored.worldState.depletedNodeIds[0]);
                Assert.AreEqual("node.beta", restored.worldState.depletedNodeIds[1]);
                Assert.AreEqual(2, restored.scanLog.recentCount);
                Assert.AreEqual("scan.alpha", restored.scanLog.recentEntryIds[0]);
                Assert.AreEqual("scan.beta", restored.scanLog.recentEntryIds[1]);
                Assert.AreEqual(2, restored.achievements.unlockedCount);
                Assert.AreEqual("achievement.alpha", restored.achievements.unlockedIds[0]);
                Assert.AreEqual("achievement.beta", restored.achievements.unlockedIds[1]);
            }
        }

        [Test]
        public void SaveRootRuntime_ReadClampsMalformedNarrativeDiscoveryCount()
        {
            const string narrativeId = "narrative.read";
            const int narrativeDepthTier = 3;

            SaveData data = SaveData.CreateNew(0.0);
            data.narrativeDiscoveryCount = 1;
            data.narrativeDiscoveryIds[0] = narrativeId;
            data.narrativeDepthTier = narrativeDepthTier;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            int bytesWritten;
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
            }

            byte[] marker = BuildNarrativeDiscoveryRootMarker(-1, narrativeId, narrativeDepthTier);
            int narrativeOffset = FindLittleEndianByteSequenceOffset(payload, bytesWritten, marker);
            Assert.GreaterOrEqual(narrativeOffset, 0);
            PatchPayloadInt(payload, narrativeOffset + sizeof(int), SaveData.MaxNarrativeDiscoveries + 10);

            fixed (byte* payloadPtr = payload)
            {
                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(1, restoredData.narrativeDiscoveryCount);
                Assert.AreEqual(SaveData.MaxNarrativeDiscoveries, restoredData.narrativeDiscoveryIds.Length);
                Assert.AreEqual(narrativeId, restoredData.narrativeDiscoveryIds[0]);
                Assert.AreEqual(narrativeDepthTier, restoredData.narrativeDepthTier);
            }
        }

        [Test]
        public void SaveRootRuntime_ReadTrimsMismatchedCorporatePendingPairs()
        {
            const string receivedId = "corp.received.read";
            const string orderA = "corp.pending.a";
            const string orderB = "corp.pending.b";
            const float timerA = 4.5f;
            const float timerB = 8.25f;
            const float firstHourSessionTime = 12.75f;

            SaveData data = SaveData.CreateNew(0.0);
            data.corporateReceivedOrderIds.Add(receivedId);
            data.corporatePendingOrderIds.Add(orderA);
            data.corporatePendingOrderIds.Add(orderB);
            data.corporatePendingOrderTimers.Add(timerA);
            data.corporatePendingOrderTimers.Add(timerB);
            data.firstHourSessionTime = firstHourSessionTime;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            int bytesWritten;
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
            }

            byte[] marker = BuildCorporatePendingRootMarker(receivedId, orderA, orderB, timerA, timerB, firstHourSessionTime);
            int corporateOffset = FindLittleEndianByteSequenceOffset(payload, bytesWritten, marker);
            Assert.GreaterOrEqual(corporateOffset, 0);

            int receivedListBytes = EncodedStringArraySingleEntryBytes(receivedId);
            int pendingIdsOffset = receivedListBytes;
            int secondPendingIdOffset = pendingIdsOffset + sizeof(int) + EncodedStringBytes(orderA);
            int secondPendingIdBytes = EncodedStringBytes(orderB);
            PatchPayloadInt(payload, corporateOffset + pendingIdsOffset, 1);

            byte[] trimmedPayload = new byte[bytesWritten - secondPendingIdBytes];
            int trimmedBytesWritten = RemovePayloadRange(
                payload,
                corporateOffset + secondPendingIdOffset,
                secondPendingIdBytes,
                bytesWritten,
                trimmedPayload);

            fixed (byte* payloadPtr = trimmedPayload)
            {
                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    trimmedBytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(trimmedBytesWritten, bytesRead);
                Assert.AreEqual(1, restoredData.corporatePendingOrderIds.Count);
                Assert.AreEqual(1, restoredData.corporatePendingOrderTimers.Count);
                Assert.AreEqual(orderA, restoredData.corporatePendingOrderIds[0]);
                Assert.AreEqual(timerA, restoredData.corporatePendingOrderTimers[0]);
                Assert.AreEqual(firstHourSessionTime, restoredData.firstHourSessionTime);
            }
        }

        [Test]
        public void SaveRootRuntime_WriteNormalizesStaleLastDiscoveredBiomeId()
        {
            SaveData data = SaveData.CreateNew(0.0);
            const int discoveredBiomeId = 7;
            const int staleLastBiomeId = 99;
            data.discoveredBiomeIds.Clear();
            data.discoveredBiomeIds.Add(discoveredBiomeId);
            BiomeDiscoveryBitMask.Pack(data.discoveredBiomeIds, data.discoveredBiomeBitWords);
            data.lastDiscoveredBiomeId = staleLastBiomeId;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
                byte[] normalizedMarker = BuildDiscoveredBiomeRootMarker(
                    discoveredBiomeId,
                    data.discoveredBiomeBitWords,
                    discoveredBiomeId);
                byte[] staleMarker = BuildDiscoveredBiomeRootMarker(
                    discoveredBiomeId,
                    data.discoveredBiomeBitWords,
                    staleLastBiomeId);
                Assert.GreaterOrEqual(FindLittleEndianByteSequenceOffset(payload, bytesWritten, normalizedMarker), 0);
                Assert.AreEqual(-1, FindLittleEndianByteSequenceOffset(payload, bytesWritten, staleMarker));

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(discoveredBiomeId, restored.lastDiscoveredBiomeId);
            }
        }

        [Test]
        public void SaveRootRuntime_WriteFiltersInvalidLegacyDiscoveredBiomeIds()
        {
            SaveData data = SaveData.CreateNew(0.0);
            const int discoveredBiomeId = 10;
            data.discoveredBiomeIds.Clear();
            data.discoveredBiomeIds.Add(-1);
            data.discoveredBiomeIds.Add(discoveredBiomeId);
            data.discoveredBiomeIds.Add(BiomeDiscoveryBitMask.MaxBiomeId + 1);
            BiomeDiscoveryBitMask.Pack(data.discoveredBiomeIds, data.discoveredBiomeBitWords);
            data.lastDiscoveredBiomeId = discoveredBiomeId;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(1, restored.discoveredBiomeIds.Count);
                Assert.IsTrue(restored.discoveredBiomeIds.Contains(discoveredBiomeId));
                Assert.AreEqual(discoveredBiomeId, restored.lastDiscoveredBiomeId);
            }
        }

        [Test]
        public void SaveRootRuntime_ReadNormalizesStaleLastDiscoveredBiomeId()
        {
            SaveData data = SaveData.CreateNew(0.0);
            const int discoveredBiomeId = 8;
            const int staleLastBiomeId = 100;
            data.discoveredBiomeIds.Clear();
            data.discoveredBiomeIds.Add(discoveredBiomeId);
            BiomeDiscoveryBitMask.Pack(data.discoveredBiomeIds, data.discoveredBiomeBitWords);
            data.lastDiscoveredBiomeId = discoveredBiomeId;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
                byte[] marker = BuildDiscoveredBiomeRootMarker(
                    discoveredBiomeId,
                    data.discoveredBiomeBitWords,
                    discoveredBiomeId);
                int biomeRootOffset = FindLittleEndianByteSequenceOffset(payload, bytesWritten, marker);
                Assert.GreaterOrEqual(biomeRootOffset, 0);
                PatchPayloadInt(payload, biomeRootOffset + marker.Length - sizeof(int), staleLastBiomeId);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(discoveredBiomeId, restored.lastDiscoveredBiomeId);
            }
        }

        [Test]
        public void SaveRootRuntime_WriteSanitizesMalformedDiscoveredBiomeIds()
        {
            SaveData data = SaveData.CreateNew(0.0);
            const int discoveredBiomeId = 10;
            const int malformedBiomeId = BiomeDiscoveryBitMask.MaxBiomeId + 1;
            data.discoveredBiomeIds.Clear();
            data.discoveredBiomeIds.Add(malformedBiomeId);
            data.discoveredBiomeIds.Add(discoveredBiomeId);
            BiomeDiscoveryBitMask.Pack(data.discoveredBiomeIds, data.discoveredBiomeBitWords);
            data.lastDiscoveredBiomeId = discoveredBiomeId;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
                byte[] marker = BuildDiscoveredBiomeRootMarker(
                    discoveredBiomeId,
                    data.discoveredBiomeBitWords,
                    discoveredBiomeId);
                Assert.GreaterOrEqual(FindLittleEndianByteSequenceOffset(payload, bytesWritten, marker), 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(1, restored.discoveredBiomeIds.Count);
                Assert.IsTrue(restored.discoveredBiomeIds.Contains(discoveredBiomeId));
                Assert.IsFalse(restored.discoveredBiomeIds.Contains(malformedBiomeId));
                Assert.AreEqual(discoveredBiomeId, restored.lastDiscoveredBiomeId);
            }
        }

        [Test]
        public void SaveRootRuntime_ReadSanitizesMalformedDiscoveredBiomeIds()
        {
            SaveData data = SaveData.CreateNew(0.0);
            const int discoveredBiomeId = 11;
            const int malformedBiomeId = BiomeDiscoveryBitMask.MinBiomeId - 1;
            data.discoveredBiomeIds.Clear();
            data.discoveredBiomeIds.Add(discoveredBiomeId);
            BiomeDiscoveryBitMask.Pack(data.discoveredBiomeIds, data.discoveredBiomeBitWords);
            data.lastDiscoveredBiomeId = discoveredBiomeId;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
                byte[] marker = BuildDiscoveredBiomeRootMarker(
                    discoveredBiomeId,
                    data.discoveredBiomeBitWords,
                    discoveredBiomeId);
                int biomeRootOffset = FindLittleEndianByteSequenceOffset(payload, bytesWritten, marker);
                Assert.GreaterOrEqual(biomeRootOffset, 0);
                byte[] malformedPayload = InsertPayloadInt(
                    payload,
                    bytesWritten,
                    biomeRootOffset + sizeof(int) + sizeof(int),
                    malformedBiomeId);
                PatchPayloadInt(malformedPayload, biomeRootOffset, 2);

                fixed (byte* malformedPayloadPtr = malformedPayload)
                {
                    bool read = SaveBinaryPayloadCodec.TryRead(
                        malformedPayloadPtr,
                        malformedPayload.Length,
                        out SaveData restored,
                        out int bytesRead,
                        out string readError);

                    Assert.IsTrue(read, readError);
                    Assert.AreEqual(malformedPayload.Length, bytesRead);
                    Assert.AreEqual(1, restored.discoveredBiomeIds.Count);
                    Assert.IsTrue(restored.discoveredBiomeIds.Contains(discoveredBiomeId));
                    Assert.IsFalse(restored.discoveredBiomeIds.Contains(malformedBiomeId));
                    Assert.AreEqual(discoveredBiomeId, restored.lastDiscoveredBiomeId);
                }
            }
        }

        [Test]
        public void SaveRootRuntime_WriteSanitizesMalformedDiscoveredBiomeBitWords()
        {
            SaveData data = SaveData.CreateNew(0.0);
            const int discoveredBiomeId = 65;
            const long malformedHighBit = 1L << 50;
            data.discoveredBiomeIds.Clear();
            data.discoveredBiomeIds.Add(discoveredBiomeId);
            BiomeDiscoveryBitMask.Pack(data.discoveredBiomeIds, data.discoveredBiomeBitWords);
            data.discoveredBiomeBitWords[1] |= malformedHighBit;
            data.lastDiscoveredBiomeId = discoveredBiomeId;
            long[] sanitizedWords = (long[])data.discoveredBiomeBitWords.Clone();
            BiomeDiscoveryBitMask.SanitizeWords(sanitizedWords);

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
                byte[] marker = BuildDiscoveredBiomeRootMarker(
                    discoveredBiomeId,
                    sanitizedWords,
                    discoveredBiomeId);
                Assert.GreaterOrEqual(FindLittleEndianByteSequenceOffset(payload, bytesWritten, marker), 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(sanitizedWords[1], restored.discoveredBiomeBitWords[1]);
                Assert.IsTrue(BiomeDiscoveryBitMask.Contains(restored.discoveredBiomeBitWords, discoveredBiomeId));
                Assert.AreEqual(discoveredBiomeId, restored.lastDiscoveredBiomeId);
            }
        }

        [Test]
        public void SaveRootRuntime_ReadSanitizesMalformedDiscoveredBiomeBitWords()
        {
            SaveData data = SaveData.CreateNew(0.0);
            const int discoveredBiomeId = 66;
            const long malformedHighBit = 1L << 50;
            data.discoveredBiomeIds.Clear();
            data.discoveredBiomeIds.Add(discoveredBiomeId);
            BiomeDiscoveryBitMask.Pack(data.discoveredBiomeIds, data.discoveredBiomeBitWords);
            data.lastDiscoveredBiomeId = discoveredBiomeId;
            long[] malformedWords = (long[])data.discoveredBiomeBitWords.Clone();
            malformedWords[1] = malformedHighBit;
            long[] expectedWords = (long[])data.discoveredBiomeBitWords.Clone();

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
                byte[] marker = BuildDiscoveredBiomeRootMarker(
                    discoveredBiomeId,
                    data.discoveredBiomeBitWords,
                    discoveredBiomeId);
                int biomeRootOffset = FindLittleEndianByteSequenceOffset(payload, bytesWritten, marker);
                Assert.GreaterOrEqual(biomeRootOffset, 0);
                int secondWordOffset = biomeRootOffset + (sizeof(int) * 3) + sizeof(long);
                PatchPayloadLong(payload, secondWordOffset, malformedWords[1]);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(expectedWords[1], restored.discoveredBiomeBitWords[1]);
                Assert.IsTrue(BiomeDiscoveryBitMask.Contains(restored.discoveredBiomeBitWords, discoveredBiomeId));
                Assert.AreEqual(discoveredBiomeId, restored.lastDiscoveredBiomeId);
            }
        }

        [Test]
        public void SaveRootRuntime_WriteSanitizesMalformedIndustrialLoreUnlockWords()
        {
            SaveData data = SaveData.CreateNew(0.0);
            const long validWord = (1L << 49) | (1L << 17) | 1L;
            const long malformedHighBit = 1L << 60;
            long malformedWord = validWord | malformedHighBit;
            data.industrialLoreUnlockWords[0] = malformedWord;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
                Assert.AreEqual(0, CountLittleEndianLong(payload, bytesWritten, malformedWord));
                Assert.GreaterOrEqual(CountLittleEndianLong(payload, bytesWritten, validWord), 1);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(validWord, restored.industrialLoreUnlockWords[0]);
            }
        }

        [Test]
        public void SaveRootRuntime_ReadSanitizesMalformedIndustrialLoreUnlockWords()
        {
            SaveData data = SaveData.CreateNew(0.0);
            const long validWord = (1L << 49) | (1L << 17) | 1L;
            const long malformedHighBit = 1L << 60;
            long malformedWord = validWord | malformedHighBit;
            data.industrialLoreUnlockWords[0] = validWord;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
                byte[] marker = BuildIndustrialLoreRootMarker(validWord);
                int industrialLoreOffset = FindLittleEndianByteSequenceOffset(payload, bytesWritten, marker);
                Assert.GreaterOrEqual(industrialLoreOffset, 0);
                PatchPayloadLong(payload, industrialLoreOffset + sizeof(int), malformedWord);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(validWord, restored.industrialLoreUnlockWords[0]);
            }
        }

        [Test]
        public void SaveRootRuntime_WriteSanitizesMalformedSuitUpgradeMask()
        {
            SaveData data = SaveData.CreateNew(0.0);
            const ulong validMask = SaveData.SuitUpgradeSupportedMask;
            const ulong malformedMask = validMask | (1UL << 63) | (1UL << 32);
            data.suitUpgradeMask = malformedMask;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
                Assert.AreEqual(0, CountLittleEndianLong(payload, bytesWritten, unchecked((long)malformedMask)));
                Assert.GreaterOrEqual(CountLittleEndianLong(payload, bytesWritten, unchecked((long)validMask)), 1);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(validMask, restored.suitUpgradeMask);
            }
        }

        [Test]
        public void SaveRootRuntime_ReadSanitizesMalformedSuitUpgradeMask()
        {
            SaveData data = SaveData.CreateNew(0.0);
            const ulong validMask = SaveData.SuitUpgradeSupportedMask;
            const ulong malformedMask = validMask | (1UL << 63) | (1UL << 32);
            data.atlasSignalDetected = true;
            data.atlasSignalPulseTimer = 123.25f;
            data.atlasSignalRevealStage = 4;
            data.suitUpgradeMask = validMask;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
                byte[] marker = BuildSuitUpgradeRootMarker(true, 123.25f, 4, validMask);
                int suitUpgradeOffset = FindLittleEndianByteSequenceOffset(payload, bytesWritten, marker);
                Assert.GreaterOrEqual(suitUpgradeOffset, 0);
                PatchPayloadLong(payload, suitUpgradeOffset + sizeof(byte) + sizeof(float) + sizeof(int), unchecked((long)malformedMask));

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(validMask, restored.suitUpgradeMask);
            }
        }

        [Test]
        public void SaveRootRuntime_ReadCanonicalizesMissingRootCollectionsAndPackedWords()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.toolDurabilityMap = null;
            data.toolBrokenMap = null;
            data.CustomModData = null;
            data.discoveredBiomeIds = null;
            data.discoveredBiomeBitWords = new[] { 1L };
            data.lastDiscoveredBiomeId = BiomeDiscoveryBitMask.MinBiomeId;
            data.narrativeDiscoveryCount = 4;
            data.narrativeDiscoveryIds = null;
            data.audioLogDiscoveredIds = null;
            data.audioLogDiscoveryBitWords = null;
            data.audioLogEncryptedFragmentCount = 3;
            data.audioLogEncryptedFragmentHashes = null;
            data.audioLogEncryptedFragmentBits = null;
            data.industrialLoreUnlockWords = null;
            data.questActiveIds = null;
            data.questCompletedIds = null;
            data.suitInstalledUpgradeIds = null;
            data.suitUnlockedBlueprintIds = null;
            data.suitBrokenUpgradeIds = null;
            data.playerExpressionProfileId = null;
            data.corporateReceivedOrderIds = null;
            data.corporatePendingOrderIds = null;
            data.corporatePendingOrderTimers = null;
            data.missionActiveIds = null;
            data.missionCompletedIds = null;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.IsNotNull(restored.toolDurabilityMap);
                Assert.IsNotNull(restored.toolBrokenMap);
                Assert.IsNotNull(restored.CustomModData);
                Assert.IsNotNull(restored.discoveredBiomeIds);
                Assert.AreEqual(BiomeDiscoveryBitMask.WordCount, restored.discoveredBiomeBitWords.Length);
                Assert.AreEqual(1L, restored.discoveredBiomeBitWords[0]);
                Assert.AreEqual(BiomeDiscoveryBitMask.MinBiomeId, restored.lastDiscoveredBiomeId);
                Assert.AreEqual(SaveData.MaxNarrativeDiscoveries, restored.narrativeDiscoveryIds.Length);
                Assert.AreEqual(0, restored.narrativeDiscoveryCount);
                Assert.IsNotNull(restored.audioLogDiscoveredIds);
                Assert.AreEqual(AudioLogDiscoveryBitMask.WordCount, restored.audioLogDiscoveryBitWords.Length);
                Assert.AreEqual(SaveData.MaxEncryptedAudioLogFragments, restored.audioLogEncryptedFragmentHashes.Length);
                Assert.AreEqual(SaveData.MaxEncryptedAudioLogFragments, restored.audioLogEncryptedFragmentBits.Length);
                Assert.AreEqual(0, restored.audioLogEncryptedFragmentCount);
                Assert.AreEqual(IndustrialLoreBitMask.WordCount, restored.industrialLoreUnlockWords.Length);
                Assert.IsNotNull(restored.questActiveIds);
                Assert.IsNotNull(restored.questCompletedIds);
                Assert.IsNotNull(restored.suitInstalledUpgradeIds);
                Assert.IsNotNull(restored.suitUnlockedBlueprintIds);
                Assert.IsNotNull(restored.suitBrokenUpgradeIds);
                Assert.AreEqual(string.Empty, restored.playerExpressionProfileId);
                Assert.IsNotNull(restored.corporateReceivedOrderIds);
                Assert.IsNotNull(restored.corporatePendingOrderIds);
                Assert.IsNotNull(restored.corporatePendingOrderTimers);
                Assert.IsNotNull(restored.missionActiveIds);
                Assert.IsNotNull(restored.missionCompletedIds);
            }
        }

        [Test]
        public void SaveRootRuntime_WriteSkipsBlankRootStringListIds()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.timestamp = null;
            data.narrativeDiscoveryCount = 1;
            data.narrativeDiscoveryIds[0] = null;
            data.audioLogDiscoveredIds.Add(null);
            data.questActiveIds.Add(null);
            data.suitInstalledUpgradeIds.Add(null);
            data.playerExpressionProfileId = " ";
            data.corporateReceivedOrderIds.Add(null);
            data.missionActiveIds.Add(null);
            data.CustomModData["custom.null"] = null;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.IsFalse(string.IsNullOrWhiteSpace(restored.timestamp));
                Assert.AreEqual(0, restored.narrativeDiscoveryCount);
                Assert.AreEqual(0, restored.audioLogDiscoveredIds.Count);
                Assert.AreEqual(0, restored.questActiveIds.Count);
                Assert.AreEqual(0, restored.suitInstalledUpgradeIds.Count);
                Assert.AreEqual(string.Empty, restored.playerExpressionProfileId);
                Assert.AreEqual(0, restored.corporateReceivedOrderIds.Count);
                Assert.AreEqual(0, restored.missionActiveIds.Count);
                Assert.AreEqual(string.Empty, restored.CustomModData["custom.null"]);
            }
        }

        [Test]
        public void SaveRootRuntime_ReadRepairsBlankPlayerExpressionProfileId()
        {
            const string profileId = "profile.read.blank.probe";

            SaveData data = SaveData.CreateNew(0.0);
            data.playerExpressionProfileId = profileId;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                byte[] profileMarker = BuildPayloadString(profileId);
                int profileOffset = FindLittleEndianByteSequenceOffset(payload, bytesWritten, profileMarker);
                Assert.GreaterOrEqual(profileOffset, 0);

                for (int i = 0; i < profileId.Length; i++)
                {
                    int characterOffset = profileOffset + sizeof(int) + (i * sizeof(char));
                    payload[characterOffset] = 0x20;
                    payload[characterOffset + 1] = 0x00;
                }

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(string.Empty, restored.playerExpressionProfileId);
            }
        }

        [Test]
        public void SaveRootRuntime_WriteSkipsBlankDictionaryKeys()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.toolDurabilityMap[string.Empty] = 99f;
            data.toolDurabilityMap[" "] = 88f;
            data.toolDurabilityMap["tool.valid.key"] = 7.5f;
            data.toolBrokenMap[string.Empty] = true;
            data.toolBrokenMap[" "] = true;
            data.toolBrokenMap["tool.valid.key"] = false;
            data.CustomModData[string.Empty] = "discard";
            data.CustomModData[" "] = "discard";
            data.CustomModData["custom.valid.key"] = "keep";

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.IsFalse(restored.toolDurabilityMap.ContainsKey(string.Empty));
                Assert.IsFalse(restored.toolDurabilityMap.ContainsKey(" "));
                Assert.IsFalse(restored.toolBrokenMap.ContainsKey(string.Empty));
                Assert.IsFalse(restored.toolBrokenMap.ContainsKey(" "));
                Assert.IsFalse(restored.CustomModData.ContainsKey(string.Empty));
                Assert.IsFalse(restored.CustomModData.ContainsKey(" "));
                Assert.AreEqual(7.5f, restored.toolDurabilityMap["tool.valid.key"]);
                Assert.IsFalse(restored.toolBrokenMap["tool.valid.key"]);
                Assert.AreEqual("keep", restored.CustomModData["custom.valid.key"]);
            }
        }

        [Test]
        public void SaveRootRuntime_ReadSkipsBlankDictionaryKeys()
        {
            SaveData data = SaveData.CreateNew(0.0);
            const string durabilityKey = "tool.empty.key.read.probe";
            data.toolDurabilityMap[durabilityKey] = 17.25f;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                byte[] keyMarker = BuildPayloadString(durabilityKey);
                int keyOffset = FindLittleEndianByteSequenceOffset(payload, bytesWritten, keyMarker);
                Assert.GreaterOrEqual(keyOffset, 0);

                byte[] whitespacePayload = new byte[bytesWritten];
                Buffer.BlockCopy(payload, 0, whitespacePayload, 0, bytesWritten);
                for (int i = 0; i < durabilityKey.Length; i++)
                {
                    int characterOffset = keyOffset + sizeof(int) + (i * sizeof(char));
                    whitespacePayload[characterOffset] = 0x20;
                    whitespacePayload[characterOffset + 1] = 0x00;
                }

                fixed (byte* whitespacePtr = whitespacePayload)
                {
                    bool read = SaveBinaryPayloadCodec.TryRead(
                        whitespacePtr,
                        bytesWritten,
                        out SaveData restored,
                        out int bytesRead,
                        out string readError);

                    Assert.IsTrue(read, readError);
                    Assert.AreEqual(bytesWritten, bytesRead);
                    Assert.IsFalse(restored.toolDurabilityMap.ContainsKey(durabilityKey));
                    Assert.AreEqual(0, restored.toolDurabilityMap.Count);
                }

                PatchPayloadInt(payload, keyOffset, 0);

                int keyPayloadByteCount = durabilityKey.Length * sizeof(char);
                byte[] malformedPayload = new byte[bytesWritten - keyPayloadByteCount];
                int malformedBytesWritten = RemovePayloadRange(
                    payload,
                    keyOffset + sizeof(int),
                    keyPayloadByteCount,
                    bytesWritten,
                    malformedPayload);

                fixed (byte* malformedPtr = malformedPayload)
                {
                    bool read = SaveBinaryPayloadCodec.TryRead(
                        malformedPtr,
                        malformedBytesWritten,
                        out SaveData restored,
                        out int bytesRead,
                        out string readError);

                    Assert.IsTrue(read, readError);
                    Assert.AreEqual(malformedBytesWritten, bytesRead);
                    Assert.IsFalse(restored.toolDurabilityMap.ContainsKey(string.Empty));
                    Assert.IsFalse(restored.toolDurabilityMap.ContainsKey(durabilityKey));
                    Assert.AreEqual(0, restored.toolDurabilityMap.Count);
                }
            }
        }

        [Test]
        public void SaveRootRuntime_ReadRejectsTrailingBytes()
        {
            SaveData data = SaveData.CreateNew(0.0);

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
                payload[bytesWritten] = 0x5A;

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten + 1,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsFalse(read);
                Assert.IsNull(restored);
                Assert.AreEqual(bytesWritten, bytesRead);
                StringAssert.Contains("trailing unread bytes", readError);
            }
        }

        [Test]
        public void SaveRootRuntime_ReadClearsEncryptedAudioLogFragmentTail()
        {
            const uint activeHash = 0xAABBCCDDu;
            const uint staleHash = 0x11223344u;
            const uint activeBits = 0x0000FFFFu;
            const uint staleBits = 0x00FF00FFu;

            SaveData data = SaveData.CreateNew(0.0);
            data.audioLogEncryptedFragmentCount = 2;
            data.audioLogEncryptedFragmentHashes[0] = activeHash;
            data.audioLogEncryptedFragmentHashes[1] = staleHash;
            data.audioLogEncryptedFragmentBits[0] = activeBits;
            data.audioLogEncryptedFragmentBits[1] = staleBits;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                byte[] marker = BuildEncryptedAudioLogFragmentsMarker(
                    activeHash,
                    staleHash,
                    activeBits,
                    staleBits);
                int fragmentOffset = FindLittleEndianByteSequenceOffset(payload, bytesWritten, marker);
                Assert.GreaterOrEqual(fragmentOffset, 0);
                PatchPayloadInt(payload, fragmentOffset, 1);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(1, restored.audioLogEncryptedFragmentCount);
                Assert.AreEqual(activeHash, restored.audioLogEncryptedFragmentHashes[0]);
                Assert.AreEqual(activeBits, restored.audioLogEncryptedFragmentBits[0]);
                Assert.AreEqual(0u, restored.audioLogEncryptedFragmentHashes[1]);
                Assert.AreEqual(0u, restored.audioLogEncryptedFragmentBits[1]);
            }
        }

        [Test]
        public void SaveRootRuntime_PreV73BinaryPayloadRepairsLegacyRootDefaults()
        {
            const int legacyVersion = 72;
            const int discoveredBiomeId = 7;
            const float hazardDoseMarker = 12.25f;
            const float hazardPulseMarker = 0.25f;
            const uint workerHashA = 0xABCDEF01u;
            const uint workerHashB = 0x12345678u;

            SaveData data = SaveData.CreateNew(42.0);
            data.timestamp = "   ";
            data.discoveredBiomeIds.Clear();
            data.discoveredBiomeIds.Add(discoveredBiomeId);
            Array.Clear(
                data.discoveredBiomeBitWords,
                0,
                data.discoveredBiomeBitWords.Length);
            data.atlasSignalDetected = true;
            data.narrativeDepthTier = 3;
            data.atlasSignalRevealStage = 0;
            data.DynamicResolutionEnabled = false;
            data.firstHourSessionTime = 13.5f;
            data.firstHourMilestones = 5;
            data.firstHourGuidanceFlags = 3;
            data.endingChoice = (int)EndingChoice.Leave;
            data.endingComplete = true;
            data.endingConditionMet = false;
            data.hazardZones.toxicityDose = hazardDoseMarker;
            data.hazardZones.toxicityPulseAccumulatorSeconds = hazardPulseMarker;
            data.atlas6LiabilitySectorXenonOmegaYield = 321.5f;
            data.atlas6LiabilityHasDisasterEvidence = true;
            data.atlas6LiabilityRecoveredWorkerTagCount = 2;
            data.atlas6LiabilityRecoveredWorkerTagHashes[0] = workerHashA;
            data.atlas6LiabilityRecoveredWorkerTagHashes[1] = workerHashB;
            data.atlas6LiabilityCorporateHostilityIndex = 31.25f;
            data.atlas6LiabilityCorporateCreditBalance = 4700f;
            data.atlas6LiabilityExtractionCarrierState = 3;
            data.atlas6LiabilityBiomatterExposureLevel = 44.5f;
            data.atlas6LiabilityHaldaneLockoutActive = true;
            data.atlas6LiabilityPressureSealIntegrity = 0.25f;
            data.atlas6LiabilityBulkheadLocked = true;

            byte[] currentPayload = new byte[BinaryPayloadScratchBytes];
            int currentBytesWritten;
            fixed (byte* currentPayloadPtr = currentPayload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    currentPayloadPtr,
                    currentPayload.Length,
                    out currentBytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(currentBytesWritten, 0);
            }

            byte[] completedEndingMarker = BuildEndingRootMarker(13.5f, 5, 3, (int)EndingChoice.Leave, true, true);
            int completedEndingOffset = FindLittleEndianByteSequenceOffset(
                currentPayload,
                currentBytesWritten,
                completedEndingMarker);
            Assert.GreaterOrEqual(completedEndingOffset, 0);
            currentPayload[completedEndingOffset + completedEndingMarker.Length - 1] = 0;

            byte[] withoutVoxelPayload = RemoveCurrentDefaultVoxelDeltaPayload(
                currentPayload,
                currentBytesWritten,
                out int withoutVoxelBytesWritten);
            int contractHashBytes = sizeof(ulong) * 2;
            byte[] withoutContractPayload = new byte[withoutVoxelBytesWritten - contractHashBytes];
            int withoutContractBytesWritten = RemovePayloadRange(
                withoutVoxelPayload,
                sizeof(int),
                contractHashBytes,
                withoutVoxelBytesWritten,
                withoutContractPayload);
            PatchPayloadInt(withoutContractPayload, 0, legacyVersion);

            int hazardBytes = sizeof(float) * 2;
            int hazardOffset = FindLittleEndianFloatPairOffset(
                withoutContractPayload,
                withoutContractBytesWritten,
                hazardDoseMarker,
                hazardPulseMarker);
            Assert.GreaterOrEqual(hazardOffset, sizeof(int));
            byte[] withoutHazardPayload = new byte[withoutContractBytesWritten - hazardBytes];
            int withoutHazardBytesWritten = RemovePayloadRange(
                withoutContractPayload,
                hazardOffset,
                hazardBytes,
                withoutContractBytesWritten,
                withoutHazardPayload);

            byte[] atlas6Marker = BuildAtlas6LiabilityMarker(
                321.5f,
                true,
                new[] { workerHashA, workerHashB },
                31.25f,
                4700f,
                3,
                44.5f,
                true,
                0.25f,
                true);
            int atlas6Offset = FindLittleEndianByteSequenceOffset(
                withoutHazardPayload,
                withoutHazardBytesWritten,
                atlas6Marker);
            Assert.GreaterOrEqual(atlas6Offset, 0);

            int legacyBytesWritten = withoutHazardBytesWritten - atlas6Marker.Length;
            byte[] legacyPayload = new byte[legacyBytesWritten];
            RemovePayloadRange(
                withoutHazardPayload,
                atlas6Offset,
                atlas6Marker.Length,
                withoutHazardBytesWritten,
                legacyPayload);

            fixed (byte* legacyPayloadPtr = legacyPayload)
            {
                bool read = SaveBinaryPayloadCodec.TryRead(
                    legacyPayloadPtr,
                    legacyPayload.Length,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(legacyBytesWritten, bytesRead);
                Assert.AreEqual(legacyVersion, restored.version);
                Assert.AreEqual(HectonContractVersion.HashLo, restored.contractVersionHashLo);
                Assert.AreEqual(HectonContractVersion.HashHi, restored.contractVersionHashHi);
                Assert.IsFalse(string.IsNullOrWhiteSpace(restored.timestamp));
                Assert.IsTrue(restored.discoveredBiomeIds.Contains(discoveredBiomeId));
                Assert.IsTrue(BiomeDiscoveryBitMask.Contains(
                    restored.discoveredBiomeBitWords,
                    discoveredBiomeId));
                Assert.AreEqual((int)EndingChoice.Leave, restored.endingChoice);
                Assert.IsTrue(restored.endingComplete);
                Assert.IsTrue(restored.endingConditionMet);
                Assert.AreEqual(4, restored.atlasSignalRevealStage);
                Assert.IsTrue(restored.DynamicResolutionEnabled);
            }
        }

        [Test]
        public void SaveRootRuntime_WriteSanitizesFirstHourAndEndingScalars()
        {
            SaveData data = SaveData.CreateNew(0.0);
            int knownMilestones = (1 << (int)FirstHourMilestone.Orientation) |
                                  (1 << (int)FirstHourMilestone.HumCloser);
            int knownGuidance = (1 << 1) | (1 << 10);
            data.firstHourMilestones = knownMilestones | (1 << 20);
            data.firstHourGuidanceFlags = knownGuidance | (1 << 20);
            data.endingChoice = 99;
            data.endingComplete = true;
            data.endingConditionMet = false;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(knownMilestones, restored.firstHourMilestones);
                Assert.AreEqual(knownGuidance, restored.firstHourGuidanceFlags);
                Assert.AreEqual(0, restored.endingChoice);
                Assert.IsFalse(restored.endingComplete);
                Assert.IsFalse(restored.endingConditionMet);
            }

            SaveData completedData = SaveData.CreateNew(0.0);
            completedData.endingChoice = (int)EndingChoice.Leave;
            completedData.endingComplete = true;
            completedData.endingConditionMet = false;

            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    completedData,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual((int)EndingChoice.Leave, restored.endingChoice);
                Assert.IsTrue(restored.endingComplete);
                Assert.IsTrue(restored.endingConditionMet);
            }

            SaveData incompleteChoiceData = SaveData.CreateNew(0.0);
            incompleteChoiceData.endingChoice = (int)EndingChoice.Amplify;
            incompleteChoiceData.endingComplete = false;
            incompleteChoiceData.endingConditionMet = true;

            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    incompleteChoiceData,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual((int)EndingChoice.None, restored.endingChoice);
                Assert.IsFalse(restored.endingComplete);
                Assert.IsTrue(restored.endingConditionMet);
            }
        }

        [Test]
        public void SaveRootRuntime_WriteClearsNegativeFirstHourMasks()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.firstHourMilestones = -1;
            data.firstHourGuidanceFlags = -1;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(0, restored.firstHourMilestones);
                Assert.AreEqual(0, restored.firstHourGuidanceFlags);
            }
        }

        [Test]
        public void ToolDurabilityRuntime_WriteSanitizesNonFiniteDurabilityValues()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.toolDurabilityMap["tool.nan"] = float.NaN;
            data.toolDurabilityMap["tool.negative"] = -12.5f;
            data.toolDurabilityMap["tool.ok"] = 42.5f;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(0f, restored.toolDurabilityMap["tool.nan"]);
                Assert.AreEqual(0f, restored.toolDurabilityMap["tool.negative"]);
                Assert.AreEqual(42.5f, restored.toolDurabilityMap["tool.ok"]);
            }
        }

        [Test]
        public void SaveDataMigration_DoesNotDowngradeFutureSaveDataVersion()
        {
            SaveData data = SaveData.CreateNew(0.0);
            int futureVersion = SaveData.CurrentVersion + 1;
            data.version = futureVersion;
            data.hazardZones.toxicityDose = 12f;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsFalse(changed);
            Assert.AreEqual(futureVersion, originalVersion);
            Assert.AreEqual(futureVersion, data.version);
            Assert.AreEqual(12f, data.hazardZones.toxicityDose);
            StringAssert.Contains("unsupported future save data version", summary);
        }

        [Test]
        public void SaveRootRuntimeMigration_CurrentRepairsNonFiniteSessionValues()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.totalPlayTime = double.NegativeInfinity;
            data.firstHourSessionTime = float.NaN;
            data.corporatePendingOrderIds.Add("order.a");
            data.corporatePendingOrderIds.Add("order.b");
            data.corporatePendingOrderTimers.Add(-1f);
            data.corporatePendingOrderTimers.Add(float.PositiveInfinity);

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(0d, data.totalPlayTime);
            Assert.AreEqual(0f, data.firstHourSessionTime);
            Assert.AreEqual(2, data.corporatePendingOrderTimers.Count);
            Assert.AreEqual(0f, data.corporatePendingOrderTimers[0]);
            Assert.AreEqual(0f, data.corporatePendingOrderTimers[1]);
            StringAssert.Contains("total play time repaired", summary);
            StringAssert.Contains("first hour session time repaired", summary);
            StringAssert.Contains("corporate order timers repaired", summary);
        }

        [Test]
        public void SaveRootRuntimeMigration_CurrentCompactsCorporatePendingOrdersWithTimers()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.corporatePendingOrderIds.Clear();
            data.corporatePendingOrderTimers.Clear();
            data.corporatePendingOrderIds.Add("order.alpha");
            data.corporatePendingOrderTimers.Add(1.25f);
            data.corporatePendingOrderIds.Add(" ");
            data.corporatePendingOrderTimers.Add(99f);
            data.corporatePendingOrderIds.Add("order.beta");
            data.corporatePendingOrderTimers.Add(2.5f);

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(2, data.corporatePendingOrderIds.Count);
            Assert.AreEqual(2, data.corporatePendingOrderTimers.Count);
            Assert.AreEqual("order.alpha", data.corporatePendingOrderIds[0]);
            Assert.AreEqual(1.25f, data.corporatePendingOrderTimers[0]);
            Assert.AreEqual("order.beta", data.corporatePendingOrderIds[1]);
            Assert.AreEqual(2.5f, data.corporatePendingOrderTimers[1]);
            StringAssert.Contains("corporate pending order ids repaired", summary);
        }

        [Test]
        public void SaveRootRuntimeMigration_CurrentRepairsMalformedNarrativeAndLodValues()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.narrativeDepthTier = -2;
            data.LODQualityPreset = -1;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(0, data.narrativeDepthTier);
            Assert.AreEqual(1, data.LODQualityPreset);
            StringAssert.Contains("narrative depth tier repaired", summary);
            StringAssert.Contains("lod quality preset repaired", summary);
        }

        [Test]
        public void SaveRootRuntimeMigration_CurrentCanonicalizesNullOperationalLogStrings()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.scanLog.EnsureCapacity();
            data.scanLog.entryCount = 1;
            data.scanLog.entries[0] = new ScanEntryDTO { title = "Scan Title" };
            data.scanLog.recentCount = 1;
            data.scanLog.recentEntryIds[0] = null;

            data.barter.EnsureCapacity();
            data.barter.stateCount = 1;
            data.barter.offerStates[0] = new BarterOfferStateDTO
            {
                offerId = null,
                executionCount = -7
            };
            data.barter.recentTransactionCount = 1;
            data.barter.recentTransactions[0] = new BarterTransactionDTO
            {
                offerName = "Recovered Offer"
            };

            data.fieldOperations.EnsureCapacity();
            data.fieldOperations.recentCount = 1;
            data.fieldOperations.recentEntries[0] = new FieldOperationEntryDTO
            {
                title = "Field Title"
            };

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(string.Empty, data.scanLog.entries[0].id);
            Assert.AreEqual("Scan Title", data.scanLog.entries[0].title);
            Assert.AreEqual(string.Empty, data.scanLog.entries[0].category);
            Assert.AreEqual(string.Empty, data.scanLog.entries[0].summary);
            Assert.AreEqual(0, data.scanLog.recentCount);
            Assert.AreEqual(string.Empty, data.scanLog.recentEntryIds[0]);
            Assert.AreEqual(string.Empty, data.barter.offerStates[0].offerId);
            Assert.AreEqual(0, data.barter.offerStates[0].executionCount);
            Assert.AreEqual(string.Empty, data.barter.recentTransactions[0].offerId);
            Assert.AreEqual("Recovered Offer", data.barter.recentTransactions[0].offerName);
            Assert.AreEqual(string.Empty, data.barter.recentTransactions[0].channelName);
            Assert.AreEqual(string.Empty, data.barter.recentTransactions[0].costSummary);
            Assert.AreEqual(string.Empty, data.barter.recentTransactions[0].rewardSummary);
            Assert.AreEqual(string.Empty, data.fieldOperations.recentEntries[0].source);
            Assert.AreEqual("Field Title", data.fieldOperations.recentEntries[0].title);
            Assert.AreEqual(string.Empty, data.fieldOperations.recentEntries[0].summary);
            Assert.AreEqual(string.Empty, data.fieldOperations.recentEntries[0].severity);
            StringAssert.Contains("scan log entries repaired", summary);
            StringAssert.Contains("scan log recent ids repaired", summary);
            StringAssert.Contains("barter offer states repaired", summary);
            StringAssert.Contains("barter transactions repaired", summary);
            StringAssert.Contains("field log entries repaired", summary);
        }

        [Test]
        public void SaveRootRuntimeMigration_CurrentCompactsBlankIdOnlyArrays()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.narrativeDiscoveryCount = 4;
            data.narrativeDiscoveryIds[0] = "narrative.alpha";
            data.narrativeDiscoveryIds[1] = null;
            data.narrativeDiscoveryIds[2] = " ";
            data.narrativeDiscoveryIds[3] = "narrative.beta";

            data.worldState.EnsureCapacity();
            data.worldState.depletedCount = 4;
            data.worldState.depletedNodeIds[0] = "node.alpha";
            data.worldState.depletedNodeIds[1] = null;
            data.worldState.depletedNodeIds[2] = "\t";
            data.worldState.depletedNodeIds[3] = "node.beta";

            data.scanLog.EnsureCapacity();
            data.scanLog.recentCount = 4;
            data.scanLog.recentEntryIds[0] = "scan.alpha";
            data.scanLog.recentEntryIds[1] = null;
            data.scanLog.recentEntryIds[2] = " ";
            data.scanLog.recentEntryIds[3] = "scan.beta";

            data.achievements.EnsureCapacity();
            data.achievements.unlockedCount = 4;
            data.achievements.unlockedIds[0] = "achievement.alpha";
            data.achievements.unlockedIds[1] = null;
            data.achievements.unlockedIds[2] = " ";
            data.achievements.unlockedIds[3] = "achievement.beta";

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(2, data.narrativeDiscoveryCount);
            Assert.AreEqual("narrative.alpha", data.narrativeDiscoveryIds[0]);
            Assert.AreEqual("narrative.beta", data.narrativeDiscoveryIds[1]);
            Assert.AreEqual(string.Empty, data.narrativeDiscoveryIds[2]);
            Assert.AreEqual(2, data.worldState.depletedCount);
            Assert.AreEqual("node.alpha", data.worldState.depletedNodeIds[0]);
            Assert.AreEqual("node.beta", data.worldState.depletedNodeIds[1]);
            Assert.AreEqual(string.Empty, data.worldState.depletedNodeIds[2]);
            Assert.AreEqual(2, data.scanLog.recentCount);
            Assert.AreEqual("scan.alpha", data.scanLog.recentEntryIds[0]);
            Assert.AreEqual("scan.beta", data.scanLog.recentEntryIds[1]);
            Assert.AreEqual(string.Empty, data.scanLog.recentEntryIds[2]);
            Assert.AreEqual(2, data.achievements.unlockedCount);
            Assert.AreEqual("achievement.alpha", data.achievements.unlockedIds[0]);
            Assert.AreEqual("achievement.beta", data.achievements.unlockedIds[1]);
            Assert.AreEqual(string.Empty, data.achievements.unlockedIds[2]);
            StringAssert.Contains("narrative discovery ids repaired", summary);
            StringAssert.Contains("world state depleted ids repaired", summary);
            StringAssert.Contains("scan log recent ids repaired", summary);
            StringAssert.Contains("achievement unlocked ids repaired", summary);
        }

        [Test]
        public void SaveRootRuntime_ReadCompactsBlankRootStringLists()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.audioLogDiscoveredIds.Clear();
            data.audioLogDiscoveredIds.Add("audio.alpha");
            data.audioLogDiscoveredIds.Add(null);
            data.audioLogDiscoveredIds.Add(" ");
            data.audioLogDiscoveredIds.Add("audio.beta");

            data.questActiveIds.Clear();
            data.questActiveIds.Add("quest.alpha");
            data.questActiveIds.Add(string.Empty);
            data.questActiveIds.Add("quest.beta");

            data.questCompletedIds.Clear();
            data.questCompletedIds.Add("quest.done.alpha");
            data.questCompletedIds.Add(" ");
            data.questCompletedIds.Add("quest.done.beta");

            data.suitInstalledUpgradeIds.Clear();
            data.suitInstalledUpgradeIds.Add("upgrade.alpha");
            data.suitInstalledUpgradeIds.Add("\t");
            data.suitInstalledUpgradeIds.Add("upgrade.beta");

            data.suitUnlockedBlueprintIds.Clear();
            data.suitUnlockedBlueprintIds.Add("blueprint.alpha");
            data.suitUnlockedBlueprintIds.Add(null);
            data.suitUnlockedBlueprintIds.Add("blueprint.beta");

            data.suitBrokenUpgradeIds.Clear();
            data.suitBrokenUpgradeIds.Add("broken.alpha");
            data.suitBrokenUpgradeIds.Add(string.Empty);
            data.suitBrokenUpgradeIds.Add("broken.beta");

            data.corporateReceivedOrderIds.Clear();
            data.corporateReceivedOrderIds.Add("corp.alpha");
            data.corporateReceivedOrderIds.Add(" ");
            data.corporateReceivedOrderIds.Add("corp.beta");

            data.corporatePendingOrderIds.Clear();
            data.corporatePendingOrderTimers.Clear();
            data.corporatePendingOrderIds.Add("pending.alpha");
            data.corporatePendingOrderTimers.Add(1f);
            data.corporatePendingOrderIds.Add(" ");
            data.corporatePendingOrderTimers.Add(2f);
            data.corporatePendingOrderIds.Add("pending.beta");
            data.corporatePendingOrderTimers.Add(float.NaN);

            data.missionActiveIds.Clear();
            data.missionActiveIds.Add("mission.alpha");
            data.missionActiveIds.Add(null);
            data.missionActiveIds.Add("mission.beta");

            data.missionCompletedIds.Clear();
            data.missionCompletedIds.Add("mission.done.alpha");
            data.missionCompletedIds.Add(" ");
            data.missionCompletedIds.Add("mission.done.beta");

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                CollectionAssert.AreEqual(
                    new[] { "audio.alpha", "audio.beta" },
                    restored.audioLogDiscoveredIds);
                CollectionAssert.AreEqual(
                    new[] { "quest.alpha", "quest.beta" },
                    restored.questActiveIds);
                CollectionAssert.AreEqual(
                    new[] { "quest.done.alpha", "quest.done.beta" },
                    restored.questCompletedIds);
                CollectionAssert.AreEqual(
                    new[] { "upgrade.alpha", "upgrade.beta" },
                    restored.suitInstalledUpgradeIds);
                CollectionAssert.AreEqual(
                    new[] { "blueprint.alpha", "blueprint.beta" },
                    restored.suitUnlockedBlueprintIds);
                CollectionAssert.AreEqual(
                    new[] { "broken.alpha", "broken.beta" },
                    restored.suitBrokenUpgradeIds);
                CollectionAssert.AreEqual(
                    new[] { "corp.alpha", "corp.beta" },
                    restored.corporateReceivedOrderIds);
                CollectionAssert.AreEqual(
                    new[] { "pending.alpha", "pending.beta" },
                    restored.corporatePendingOrderIds);
                CollectionAssert.AreEqual(
                    new[] { 1f, 0f },
                    restored.corporatePendingOrderTimers);
                CollectionAssert.AreEqual(
                    new[] { "mission.alpha", "mission.beta" },
                    restored.missionActiveIds);
                CollectionAssert.AreEqual(
                    new[] { "mission.done.alpha", "mission.done.beta" },
                    restored.missionCompletedIds);
            }
        }

        [Test]
        public void SaveRootRuntimeMigration_CurrentClearsEncryptedAudioLogFragmentTail()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.audioLogEncryptedFragmentCount = 1;
            data.audioLogEncryptedFragmentHashes[0] = 0xAABBCCDDu;
            data.audioLogEncryptedFragmentHashes[1] = 0x11223344u;
            data.audioLogEncryptedFragmentBits[0] = 0x0000FFFFu;
            data.audioLogEncryptedFragmentBits[1] = 0x00FF00FFu;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(1, data.audioLogEncryptedFragmentCount);
            Assert.AreEqual(0xAABBCCDDu, data.audioLogEncryptedFragmentHashes[0]);
            Assert.AreEqual(0x0000FFFFu, data.audioLogEncryptedFragmentBits[0]);
            Assert.AreEqual(0u, data.audioLogEncryptedFragmentHashes[1]);
            Assert.AreEqual(0u, data.audioLogEncryptedFragmentBits[1]);
            StringAssert.Contains("encrypted audio-log fragment tail cleared", summary);
        }

        [Test]
        public void SaveRootRuntimeMigration_CurrentRepairsVoxelDeltaDenseCountAndMissingFlags()
        {
            const int dirtyCellIndex = 17;
            const int dirtyWordIndex = dirtyCellIndex / 32;
            const uint dirtyCellBit = 1u << (dirtyCellIndex & 31);

            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.voxelDeltaPersistence.EnsureCapacity(1);
            data.voxelDeltaPersistence.chunkCount = 1;
            data.voxelDeltaPersistence.totalCellCount = 5;

            uint[] dirtyMaskWords = new uint[VoxelDeltaChunkDTO.DirtyMaskWordCount];
            ushort[] sdfValueBits = new ushort[VoxelDeltaChunkDTO.CellCount];
            byte[] materialIds = new byte[VoxelDeltaChunkDTO.CellCount];
            byte[] cellFlags = new byte[VoxelDeltaChunkDTO.CellCount];
            dirtyMaskWords[dirtyWordIndex] = dirtyCellBit;
            sdfValueBits[dirtyCellIndex] = 0x2345;
            materialIds[dirtyCellIndex] = 4;
            cellFlags[dirtyCellIndex] = VoxelDeltaChunkDTO.SupportedCellFlags | 0x80;
            data.voxelDeltaPersistence.chunks[0] = new VoxelDeltaChunkDTO
            {
                chunkX = 3,
                chunkY = 4,
                chunkZ = 5,
                voxelSize = 0.25f,
                cellCount = 5,
                storageFlags = (byte)(VoxelDeltaChunkDTO.StorageDense | 0x80),
                reservedStorage = 7,
                dirtyMaskWords = dirtyMaskWords,
                sdfValueBits = sdfValueBits,
                materialIds = materialIds,
                cellFlags = cellFlags,
                cells = new VoxelDeltaCellDTO[5]
            };

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(1, data.voxelDeltaPersistence.chunkCount);
            Assert.AreEqual(1, data.voxelDeltaPersistence.totalCellCount);
            VoxelDeltaChunkDTO chunk = data.voxelDeltaPersistence.chunks[0];
            Assert.AreEqual(1, chunk.cellCount);
            Assert.AreEqual(VoxelDeltaChunkDTO.StorageDense, chunk.storageFlags);
            Assert.AreEqual((byte)0, chunk.reservedStorage);
            Assert.AreEqual(VoxelDeltaChunkDTO.CellCount, chunk.cellFlags.Length);
            Assert.AreEqual(dirtyCellBit, chunk.dirtyMaskWords[dirtyWordIndex]);
            Assert.AreEqual((ushort)0x2345, chunk.sdfValueBits[dirtyCellIndex]);
            Assert.AreEqual((byte)4, chunk.materialIds[dirtyCellIndex]);
            Assert.AreEqual(VoxelDeltaChunkDTO.SupportedCellFlags, chunk.cellFlags[dirtyCellIndex]);
            Assert.AreEqual(0, chunk.cells.Length);
            StringAssert.Contains("voxel delta total count repaired", summary);
            StringAssert.Contains("voxel delta cell flags repaired", summary);
            StringAssert.Contains("voxel delta storage flags repaired", summary);
        }

        [Test]
        public void SaveRootRuntimeMigration_CurrentRepairsVoxelDeltaLegacyCellFlagsAndCount()
        {
            const byte malformedFlags = VoxelDeltaChunkDTO.SupportedCellFlags | 0x80;

            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.voxelDeltaPersistence.EnsureCapacity(1);
            data.voxelDeltaPersistence.chunkCount = 1;
            data.voxelDeltaPersistence.totalCellCount = VoxelDeltaChunkDTO.CellCount + 1;

            VoxelDeltaCellDTO[] cells = new VoxelDeltaCellDTO[VoxelDeltaChunkDTO.CellCount + 1];
            cells[0] = new VoxelDeltaCellDTO
            {
                universeKey = 0x0102030405060708UL,
                sdfValue = 0.125f,
                materialId = 3,
                flags = malformedFlags
            };

            data.voxelDeltaPersistence.chunks[0] = new VoxelDeltaChunkDTO
            {
                chunkX = -7,
                chunkY = 8,
                chunkZ = -9,
                voxelSize = 0.25f,
                cellCount = VoxelDeltaChunkDTO.CellCount + 1,
                storageFlags = (byte)(VoxelDeltaChunkDTO.StorageDense | 0x40),
                reservedStorage = 12,
                dirtyMaskWords = Array.Empty<uint>(),
                sdfValueBits = Array.Empty<ushort>(),
                materialIds = Array.Empty<byte>(),
                cellFlags = Array.Empty<byte>(),
                cells = cells
            };

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(1, data.voxelDeltaPersistence.chunkCount);
            Assert.AreEqual(VoxelDeltaChunkDTO.CellCount, data.voxelDeltaPersistence.totalCellCount);
            VoxelDeltaChunkDTO chunk = data.voxelDeltaPersistence.chunks[0];
            Assert.AreEqual(VoxelDeltaChunkDTO.CellCount, chunk.cellCount);
            Assert.AreEqual(VoxelDeltaChunkDTO.StorageDense, chunk.storageFlags);
            Assert.AreEqual((byte)0, chunk.reservedStorage);
            Assert.AreEqual(VoxelDeltaChunkDTO.SupportedCellFlags, chunk.cells[0].flags);
            StringAssert.Contains("voxel delta total count repaired", summary);
            StringAssert.Contains("voxel delta cell flags repaired", summary);
            StringAssert.Contains("voxel delta storage flags repaired", summary);
        }

        [Test]
        public void SaveRootRuntimeMigration_CurrentClampsVoxelDeltaChunkCountAndTotalCellOverflow()
        {
            const int excessiveChunkCount = 65537;
            const int expectedChunkCount = 65536;

            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.voxelDeltaPersistence.chunks = new VoxelDeltaChunkDTO[excessiveChunkCount];
            data.voxelDeltaPersistence.chunkCount = excessiveChunkCount;
            data.voxelDeltaPersistence.totalCellCount = -1;
            for (int i = 0; i < excessiveChunkCount; i++)
            {
                data.voxelDeltaPersistence.chunks[i] = new VoxelDeltaChunkDTO
                {
                    chunkX = i,
                    chunkY = 0,
                    chunkZ = 0,
                    voxelSize = 0.25f,
                    cellCount = VoxelDeltaChunkDTO.CellCount,
                    storageFlags = VoxelDeltaChunkDTO.StorageUniformSdfRle,
                    uniformSdfValueBits = 0x3C00,
                    dirtyMaskWords = Array.Empty<uint>(),
                    sdfValueBits = Array.Empty<ushort>(),
                    materialIds = Array.Empty<byte>(),
                    cellFlags = Array.Empty<byte>(),
                    cells = Array.Empty<VoxelDeltaCellDTO>()
                };
            }

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(expectedChunkCount, data.voxelDeltaPersistence.chunkCount);
            Assert.AreEqual(int.MaxValue, data.voxelDeltaPersistence.totalCellCount);
            StringAssert.Contains("voxel delta chunk count clamped", summary);
            StringAssert.Contains("voxel delta total count repaired", summary);
        }

        [Test]
        public void SaveRootRuntimeMigration_CurrentRepairsVoxelDeltaCarvingOperations()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.voxelDeltaPersistence.chunks = null;
            data.voxelDeltaPersistence.chunkCount = 4;
            data.voxelDeltaPersistence.totalCellCount = 123;
            data.voxelDeltaPersistence.carvingOperationCount = 3;
            data.voxelDeltaPersistence.carvingOperations = new[]
            {
                new VoxelCarvingOperationDTO
                {
                    localPosition = new Unity.Mathematics.float3(float.NaN, 2f, float.PositiveInfinity),
                    radius = float.NaN,
                    operation = (VoxelCarvingOperationKind)99,
                    materialId = 4,
                    flags = 0x1200,
                    sequence = 9u
                },
                new VoxelCarvingOperationDTO
                {
                    localPosition = new Unity.Mathematics.float3(1f, 2f, 3f),
                    radius = 5f,
                    operation = VoxelCarvingOperationKind.Add,
                    materialId = 7,
                    flags = 0x3400,
                    sequence = 10u
                }
            };

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(0, data.voxelDeltaPersistence.chunkCount);
            Assert.AreEqual(0, data.voxelDeltaPersistence.totalCellCount);
            Assert.AreEqual(0, data.voxelDeltaPersistence.chunks.Length);
            Assert.AreEqual(2, data.voxelDeltaPersistence.carvingOperationCount);
            VoxelCarvingOperationDTO repaired = data.voxelDeltaPersistence.carvingOperations[0];
            Assert.AreEqual(0f, repaired.localPosition.x);
            Assert.AreEqual(2f, repaired.localPosition.y);
            Assert.AreEqual(0f, repaired.localPosition.z);
            Assert.AreEqual(0f, repaired.radius);
            Assert.AreEqual(VoxelCarvingOperationKind.Subtract, repaired.operation);
            Assert.AreEqual((byte)4, repaired.materialId);
            Assert.AreEqual((ushort)0x1200, repaired.flags);
            Assert.AreEqual(9u, repaired.sequence);
            VoxelCarvingOperationDTO valid = data.voxelDeltaPersistence.carvingOperations[1];
            Assert.AreEqual(1f, valid.localPosition.x);
            Assert.AreEqual(2f, valid.localPosition.y);
            Assert.AreEqual(3f, valid.localPosition.z);
            Assert.AreEqual(5f, valid.radius);
            Assert.AreEqual(VoxelCarvingOperationKind.Add, valid.operation);
            StringAssert.Contains("voxel delta chunks created", summary);
            StringAssert.Contains("voxel carving operation count clamped", summary);
            StringAssert.Contains("voxel carving operations repaired", summary);
        }

        [Test]
        public void SaveRootRuntimeMigration_CurrentNormalizesStaleLastDiscoveredBiomeId()
        {
            SaveData data = SaveData.CreateNew(0.0);
            const int discoveredBiomeId = 9;
            data.version = SaveData.CurrentVersion;
            data.discoveredBiomeIds.Clear();
            data.discoveredBiomeIds.Add(discoveredBiomeId);
            BiomeDiscoveryBitMask.Pack(data.discoveredBiomeIds, data.discoveredBiomeBitWords);
            data.lastDiscoveredBiomeId = 101;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(discoveredBiomeId, data.lastDiscoveredBiomeId);
            StringAssert.Contains("last discovered biome repaired", summary);
        }

        [Test]
        public void SaveRootRuntimeMigration_CurrentRepairsMalformedDiscoveredBiomeIds()
        {
            SaveData data = SaveData.CreateNew(0.0);
            const int discoveredBiomeId = 12;
            const int malformedBiomeId = BiomeDiscoveryBitMask.MaxBiomeId + 1;
            data.version = SaveData.CurrentVersion;
            data.discoveredBiomeIds.Clear();
            data.discoveredBiomeIds.Add(malformedBiomeId);
            data.discoveredBiomeIds.Add(discoveredBiomeId);
            Array.Clear(data.discoveredBiomeBitWords, 0, data.discoveredBiomeBitWords.Length);
            data.lastDiscoveredBiomeId = discoveredBiomeId;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(1, data.discoveredBiomeIds.Count);
            Assert.IsTrue(data.discoveredBiomeIds.Contains(discoveredBiomeId));
            Assert.IsFalse(data.discoveredBiomeIds.Contains(malformedBiomeId));
            Assert.IsTrue(BiomeDiscoveryBitMask.Contains(data.discoveredBiomeBitWords, discoveredBiomeId));
            Assert.AreEqual(discoveredBiomeId, data.lastDiscoveredBiomeId);
            StringAssert.Contains("discovered biome set repaired", summary);
        }

        [Test]
        public void SaveRootRuntimeMigration_CurrentRepairsMalformedDiscoveredBiomeBitWords()
        {
            SaveData data = SaveData.CreateNew(0.0);
            const int discoveredBiomeId = 67;
            const long malformedHighBit = 1L << 50;
            data.version = SaveData.CurrentVersion;
            data.discoveredBiomeIds.Clear();
            data.discoveredBiomeIds.Add(discoveredBiomeId);
            BiomeDiscoveryBitMask.Pack(data.discoveredBiomeIds, data.discoveredBiomeBitWords);
            long expectedWord = data.discoveredBiomeBitWords[1];
            data.discoveredBiomeBitWords[1] = malformedHighBit;
            data.lastDiscoveredBiomeId = discoveredBiomeId;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.IsTrue(BiomeDiscoveryBitMask.Contains(data.discoveredBiomeBitWords, discoveredBiomeId));
            Assert.AreEqual(expectedWord, data.discoveredBiomeBitWords[1]);
            Assert.AreEqual(0L, data.discoveredBiomeBitWords[1] & malformedHighBit);
            Assert.AreEqual(discoveredBiomeId, data.lastDiscoveredBiomeId);
            StringAssert.Contains("discovered biome bit words repaired", summary);
        }

        [Test]
        public void SaveRootRuntimeMigration_CurrentRepairsMalformedIndustrialLoreUnlockWords()
        {
            SaveData data = SaveData.CreateNew(0.0);
            const long validWord = (1L << 49) | (1L << 17) | 1L;
            const long malformedHighBit = 1L << 60;
            data.version = SaveData.CurrentVersion;
            data.industrialLoreUnlockWords = new[] { validWord | malformedHighBit, -1L };

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(IndustrialLoreBitMask.WordCount, data.industrialLoreUnlockWords.Length);
            Assert.AreEqual(validWord, data.industrialLoreUnlockWords[0]);
            StringAssert.Contains("industrial lore bit words repaired", summary);
        }

        [Test]
        public void SaveRootRuntimeMigration_PreV73RepairsLegacyRootDefaults()
        {
            const int legacyVersion = 72;
            const int discoveredBiomeId = 7;

            SaveData data = SaveData.CreateNew(0.0);
            data.version = legacyVersion;
            data.timestamp = "   ";
            data.discoveredBiomeIds.Clear();
            data.discoveredBiomeIds.Add(discoveredBiomeId);
            Array.Clear(
                data.discoveredBiomeBitWords,
                0,
                data.discoveredBiomeBitWords.Length);
            data.atlasSignalDetected = true;
            data.narrativeDepthTier = 3;
            data.atlasSignalRevealStage = 0;
            data.DynamicResolutionEnabled = false;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(legacyVersion, originalVersion);
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.IsFalse(string.IsNullOrWhiteSpace(data.timestamp));
            Assert.IsTrue(BiomeDiscoveryBitMask.Contains(data.discoveredBiomeBitWords, discoveredBiomeId));
            Assert.AreEqual(2, data.atlasSignalRevealStage);
            Assert.IsTrue(data.DynamicResolutionEnabled);
            StringAssert.Contains("timestamp repaired", summary);
            StringAssert.Contains("discovered biome set packed", summary);
            StringAssert.Contains("atlas reveal stage repaired", summary);
            StringAssert.Contains("dynamic resolution default repaired", summary);
            StringAssert.Contains($"version upgraded from {legacyVersion} to {SaveData.CurrentVersion}", summary);
        }

        [Test]
        public void SaveRootRuntimeMigration_CurrentPrunesBlankRootStringListIds()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.timestamp = null;
            data.narrativeDiscoveryCount = 1;
            data.narrativeDiscoveryIds[0] = null;
            data.audioLogDiscoveredIds.Add(null);
            data.questActiveIds.Add(null);
            data.questCompletedIds.Add(null);
            data.suitInstalledUpgradeIds.Add(null);
            data.suitUnlockedBlueprintIds.Add(null);
            data.suitBrokenUpgradeIds.Add(null);
            data.playerExpressionProfileId = " ";
            data.corporateReceivedOrderIds.Add(null);
            data.corporatePendingOrderIds.Add(null);
            data.corporatePendingOrderTimers.Add(1f);
            data.missionActiveIds.Add(null);
            data.missionCompletedIds.Add(null);
            data.CustomModData["custom.null"] = null;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.IsFalse(string.IsNullOrWhiteSpace(data.timestamp));
            Assert.AreEqual(0, data.narrativeDiscoveryCount);
            Assert.AreEqual(string.Empty, data.narrativeDiscoveryIds[0]);
            Assert.AreEqual(0, data.audioLogDiscoveredIds.Count);
            Assert.AreEqual(0, data.questActiveIds.Count);
            Assert.AreEqual(0, data.questCompletedIds.Count);
            Assert.AreEqual(0, data.suitInstalledUpgradeIds.Count);
            Assert.AreEqual(0, data.suitUnlockedBlueprintIds.Count);
            Assert.AreEqual(0, data.suitBrokenUpgradeIds.Count);
            Assert.AreEqual(string.Empty, data.playerExpressionProfileId);
            Assert.AreEqual(0, data.corporateReceivedOrderIds.Count);
            Assert.AreEqual(0, data.corporatePendingOrderIds.Count);
            Assert.AreEqual(0, data.corporatePendingOrderTimers.Count);
            Assert.AreEqual(0, data.missionActiveIds.Count);
            Assert.AreEqual(0, data.missionCompletedIds.Count);
            Assert.AreEqual(string.Empty, data.CustomModData["custom.null"]);
            StringAssert.Contains("narrative discovery ids repaired", summary);
            StringAssert.Contains("audioLog ids repaired", summary);
            StringAssert.Contains("player expression profile repaired", summary);
            StringAssert.Contains("custom mod data values repaired", summary);
            StringAssert.Contains("mission active ids repaired", summary);
        }

        [Test]
        public void SaveRootRuntimeMigration_CurrentRemovesBlankDictionaryKeys()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.toolDurabilityMap[string.Empty] = 99f;
            data.toolDurabilityMap[" "] = 88f;
            data.toolDurabilityMap["tool.valid.key"] = 6.25f;
            data.toolBrokenMap[string.Empty] = true;
            data.toolBrokenMap[" "] = true;
            data.toolBrokenMap["tool.valid.key"] = false;
            data.CustomModData[string.Empty] = "discard";
            data.CustomModData[" "] = "discard";
            data.CustomModData["custom.valid.key"] = "keep";
            data.CustomModData["custom.null"] = null;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.IsFalse(data.toolDurabilityMap.ContainsKey(string.Empty));
            Assert.IsFalse(data.toolDurabilityMap.ContainsKey(" "));
            Assert.IsFalse(data.toolBrokenMap.ContainsKey(string.Empty));
            Assert.IsFalse(data.toolBrokenMap.ContainsKey(" "));
            Assert.IsFalse(data.CustomModData.ContainsKey(string.Empty));
            Assert.IsFalse(data.CustomModData.ContainsKey(" "));
            Assert.AreEqual(6.25f, data.toolDurabilityMap["tool.valid.key"]);
            Assert.IsFalse(data.toolBrokenMap["tool.valid.key"]);
            Assert.AreEqual("keep", data.CustomModData["custom.valid.key"]);
            Assert.AreEqual(string.Empty, data.CustomModData["custom.null"]);
            StringAssert.Contains("tool durability keys repaired", summary);
            StringAssert.Contains("tool broken keys repaired", summary);
            StringAssert.Contains("custom mod data keys repaired", summary);
            StringAssert.Contains("custom mod data values repaired", summary);
        }

        [Test]
        public void SaveRootRuntimeMigration_CurrentSanitizesFirstHourAndEndingScalars()
        {
            SaveData data = SaveData.CreateNew(0.0);
            int knownMilestones = (1 << (int)FirstHourMilestone.Orientation) |
                                  (1 << (int)FirstHourMilestone.HumCloser);
            int knownGuidance = (1 << 1) | (1 << 10);
            data.version = SaveData.CurrentVersion;
            data.firstHourMilestones = knownMilestones | (1 << 20);
            data.firstHourGuidanceFlags = knownGuidance | (1 << 20);
            data.endingChoice = 99;
            data.endingComplete = true;
            data.endingConditionMet = false;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(knownMilestones, data.firstHourMilestones);
            Assert.AreEqual(knownGuidance, data.firstHourGuidanceFlags);
            Assert.AreEqual(0, data.endingChoice);
            Assert.IsFalse(data.endingComplete);
            Assert.IsFalse(data.endingConditionMet);
            StringAssert.Contains("first hour milestones repaired", summary);
            StringAssert.Contains("first hour guidance flags repaired", summary);
            StringAssert.Contains("ending choice repaired", summary);
            StringAssert.Contains("ending completion repaired", summary);

            SaveData completedData = SaveData.CreateNew(0.0);
            completedData.version = SaveData.CurrentVersion;
            completedData.endingChoice = (int)EndingChoice.Leave;
            completedData.endingComplete = true;
            completedData.endingConditionMet = false;

            changed = SaveDataMigration.MigrateInPlace(completedData, out originalVersion, out summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual((int)EndingChoice.Leave, completedData.endingChoice);
            Assert.IsTrue(completedData.endingComplete);
            Assert.IsTrue(completedData.endingConditionMet);
            StringAssert.Contains("ending condition repaired", summary);

            SaveData incompleteChoiceData = SaveData.CreateNew(0.0);
            incompleteChoiceData.version = SaveData.CurrentVersion;
            incompleteChoiceData.endingChoice = (int)EndingChoice.Amplify;
            incompleteChoiceData.endingComplete = false;
            incompleteChoiceData.endingConditionMet = true;

            changed = SaveDataMigration.MigrateInPlace(incompleteChoiceData, out originalVersion, out summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual((int)EndingChoice.None, incompleteChoiceData.endingChoice);
            Assert.IsFalse(incompleteChoiceData.endingComplete);
            Assert.IsTrue(incompleteChoiceData.endingConditionMet);
            StringAssert.Contains("ending choice repaired", summary);
        }

        [Test]
        public void SaveRootRuntimeMigration_CurrentRepairsMalformedSuitUpgradeMask()
        {
            SaveData data = SaveData.CreateNew(0.0);
            const ulong validMask = SaveData.SuitUpgradeSupportedMask;
            data.version = SaveData.CurrentVersion;
            data.suitUpgradeMask = validMask | (1UL << 63) | (1UL << 32);

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(validMask, data.suitUpgradeMask);
            StringAssert.Contains("suit upgrade mask repaired", summary);
        }

        [Test]
        public void SaveRootRuntimeMigration_CurrentClearsNegativeFirstHourMasks()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.firstHourMilestones = -1;
            data.firstHourGuidanceFlags = -1;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(0, data.firstHourMilestones);
            Assert.AreEqual(0, data.firstHourGuidanceFlags);
            StringAssert.Contains("first hour milestones repaired", summary);
            StringAssert.Contains("first hour guidance flags repaired", summary);
        }

        [Test]
        public void ToolDurabilityMigration_CurrentRepairsNonFiniteDurabilityValues()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.toolDurabilityMap["tool.inf"] = float.PositiveInfinity;
            data.toolDurabilityMap["tool.negative"] = -0.25f;
            data.toolDurabilityMap["tool.ok"] = 13.75f;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(0f, data.toolDurabilityMap["tool.inf"]);
            Assert.AreEqual(0f, data.toolDurabilityMap["tool.negative"]);
            Assert.AreEqual(13.75f, data.toolDurabilityMap["tool.ok"]);
            StringAssert.Contains("tool durability values repaired", summary);
        }

        [Test]
        public void Atlas6LiabilityMigration_PreV75DefaultsUnpersistedState()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.Atlas6LiabilityPersistenceVersion - 1;
            data.atlas6LiabilitySectorXenonOmegaYield = 900f;
            data.atlas6LiabilityHasDisasterEvidence = true;
            data.atlas6LiabilityRecoveredWorkerTagCount = 9;
            data.atlas6LiabilityRecoveredWorkerTagHashes = new[] { 0xABCDEF01u, 0x12345678u };
            data.atlas6LiabilityCorporateHostilityIndex = 99f;
            data.atlas6LiabilityCorporateCreditBalance = 1f;
            data.atlas6LiabilityExtractionCarrierState = 3;
            data.atlas6LiabilityBiomatterExposureLevel = 44f;
            data.atlas6LiabilityHaldaneLockoutActive = true;
            data.atlas6LiabilityPressureSealIntegrity = 0.25f;
            data.atlas6LiabilityBulkheadLocked = true;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.Atlas6LiabilityPersistenceVersion - 1, originalVersion);
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(0f, data.atlas6LiabilitySectorXenonOmegaYield);
            Assert.IsFalse(data.atlas6LiabilityHasDisasterEvidence);
            Assert.AreEqual(0, data.atlas6LiabilityRecoveredWorkerTagCount);
            Assert.AreEqual(SaveData.MaxAtlas6LiabilityWorkerTags, data.atlas6LiabilityRecoveredWorkerTagHashes.Length);
            Assert.AreEqual(0u, data.atlas6LiabilityRecoveredWorkerTagHashes[0]);
            Assert.AreEqual(0f, data.atlas6LiabilityCorporateHostilityIndex);
            Assert.AreEqual(5000f, data.atlas6LiabilityCorporateCreditBalance);
            Assert.AreEqual(0, data.atlas6LiabilityExtractionCarrierState);
            Assert.AreEqual(0f, data.atlas6LiabilityBiomatterExposureLevel);
            Assert.IsFalse(data.atlas6LiabilityHaldaneLockoutActive);
            Assert.AreEqual(1f, data.atlas6LiabilityPressureSealIntegrity);
            Assert.IsFalse(data.atlas6LiabilityBulkheadLocked);
            StringAssert.Contains("atlas6 liability state defaulted", summary);
        }

        [Test]
        public void Atlas6LiabilityMigration_CurrentRepairsInvalidValuesAndWorkerTail()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.atlas6LiabilitySectorXenonOmegaYield = float.PositiveInfinity;
            data.atlas6LiabilityHasDisasterEvidence = true;
            data.atlas6LiabilityRecoveredWorkerTagCount = 1;
            data.atlas6LiabilityRecoveredWorkerTagHashes = new uint[4];
            data.atlas6LiabilityRecoveredWorkerTagHashes[0] = 0xABCDEF01u;
            data.atlas6LiabilityRecoveredWorkerTagHashes[2] = 0x12345678u;
            data.atlas6LiabilityCorporateHostilityIndex = float.NaN;
            data.atlas6LiabilityCorporateCreditBalance = -50f;
            data.atlas6LiabilityExtractionCarrierState = 99;
            data.atlas6LiabilityBiomatterExposureLevel = 150f;
            data.atlas6LiabilityPressureSealIntegrity = float.NaN;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(0f, data.atlas6LiabilitySectorXenonOmegaYield);
            Assert.AreEqual(1, data.atlas6LiabilityRecoveredWorkerTagCount);
            Assert.AreEqual(SaveData.MaxAtlas6LiabilityWorkerTags, data.atlas6LiabilityRecoveredWorkerTagHashes.Length);
            Assert.AreEqual(0xABCDEF01u, data.atlas6LiabilityRecoveredWorkerTagHashes[0]);
            Assert.AreEqual(0u, data.atlas6LiabilityRecoveredWorkerTagHashes[2]);
            Assert.AreEqual(0f, data.atlas6LiabilityCorporateHostilityIndex);
            Assert.AreEqual(0f, data.atlas6LiabilityCorporateCreditBalance);
            Assert.AreEqual(0, data.atlas6LiabilityExtractionCarrierState);
            Assert.AreEqual(SaveData.Atlas6LiabilityMaxBiomatterExposure, data.atlas6LiabilityBiomatterExposureLevel);
            Assert.AreEqual(1f, data.atlas6LiabilityPressureSealIntegrity);
            StringAssert.Contains("atlas6 liability values repaired", summary);
            StringAssert.Contains("atlas6 liability worker-tag tail cleared", summary);
        }

        [Test]
        public void Atlas6DirectiveMigration_CurrentRepairsInvalidStatusAndBarterCount()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.atlas6PlayerStatus = 99;
            data.atlas6BarterCount = -7;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(0, data.atlas6PlayerStatus);
            Assert.AreEqual(0, data.atlas6BarterCount);
            StringAssert.Contains("atlas6 directive state repaired", summary);
        }

        [Test]
        public void AtlasSignalMigration_CurrentRepairsInvalidPulseTimerAndRevealStage()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.atlasSignalPulseTimer = float.NaN;
            data.atlasSignalRevealStage = 99;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(0f, data.atlasSignalPulseTimer);
            Assert.AreEqual(4, data.atlasSignalRevealStage);
            StringAssert.Contains("atlas signal pulse timer repaired", summary);
            StringAssert.Contains("atlas reveal stage repaired", summary);
        }

        [Test]
        public void Atlas6PlayerStatusSanitizers_CoverAtlas6PlayerStatusEnum()
        {
            Array enumValues = Enum.GetValues(typeof(Atlas6PlayerStatus));
            int[] playerStatuses = new int[enumValues.Length];
            int maxPlayerStatus = 0;
            for (int i = 0; i < enumValues.Length; i++)
            {
                int statusValue = Convert.ToInt32(enumValues.GetValue(i));
                playerStatuses[i] = statusValue;
                maxPlayerStatus = Math.Max(maxPlayerStatus, statusValue);
            }

            Array.Sort(playerStatuses);
            for (int i = 0; i < playerStatuses.Length; i++)
                Assert.AreEqual(i, playerStatuses[i], "Atlas6PlayerStatus must remain a compact persisted enum.");

            MethodInfo codecSanitizer = typeof(SaveBinaryPayloadCodec).GetMethod(
                "SanitizeAtlas6PlayerStatus",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(codecSanitizer);

            MethodInfo migrationSanitizer = typeof(SaveDataMigration).GetMethod(
                "IsKnownAtlas6PlayerStatus",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(migrationSanitizer);

            for (int status = 0; status <= maxPlayerStatus; status++)
            {
                Assert.AreEqual(status, (int)codecSanitizer.Invoke(null, new object[] { status }));
                Assert.IsTrue((bool)migrationSanitizer.Invoke(null, new object[] { status }));
            }

            int invalidStatus = maxPlayerStatus + 1;
            Assert.AreEqual(0, (int)codecSanitizer.Invoke(null, new object[] { invalidStatus }));
            Assert.IsFalse((bool)migrationSanitizer.Invoke(null, new object[] { invalidStatus }));
        }

        [Test]
        public void Atlas6LiabilityCarrierStateSanitizers_CoverExtractionCarrierStateEnum()
        {
            Array enumValues = Enum.GetValues(typeof(ExtractionCarrierState));
            int[] carrierStates = new int[enumValues.Length];
            int maxCarrierState = 0;
            for (int i = 0; i < enumValues.Length; i++)
            {
                int stateValue = Convert.ToInt32(enumValues.GetValue(i));
                carrierStates[i] = stateValue;
                maxCarrierState = Math.Max(maxCarrierState, stateValue);
            }

            Array.Sort(carrierStates);
            for (int i = 0; i < carrierStates.Length; i++)
                Assert.AreEqual(i, carrierStates[i], "ExtractionCarrierState must remain a compact persisted enum.");

            MethodInfo codecSanitizer = typeof(SaveBinaryPayloadCodec).GetMethod(
                "SanitizeAtlas6LiabilityCarrierState",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(codecSanitizer);

            MethodInfo migrationSanitizer = typeof(SaveDataMigration).GetMethod(
                "IsKnownAtlas6CarrierState",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(migrationSanitizer);

            for (int state = 0; state <= maxCarrierState; state++)
            {
                Assert.AreEqual(state, (int)codecSanitizer.Invoke(null, new object[] { state }));
                Assert.IsTrue((bool)migrationSanitizer.Invoke(null, new object[] { state }));
            }

            int invalidState = maxCarrierState + 1;
            Assert.AreEqual(0, (int)codecSanitizer.Invoke(null, new object[] { invalidState }));
            Assert.IsFalse((bool)migrationSanitizer.Invoke(null, new object[] { invalidState }));
        }

        [Test]
        public void RadiationGridRuntime_WriteClampsOversizedRlePayloadToPersistedMaximum()
        {
            SaveData data = SaveData.CreateNew(0.0);
            int oversizedLength = SaveData.RadiationGridRleMaxBytes + 16;
            data.radiationGridRle = new byte[oversizedLength];
            data.radiationGridRleLength = oversizedLength;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(SaveData.RadiationGridRleMaxBytes, restored.radiationGridRleLength);
                Assert.AreEqual(SaveData.RadiationGridRleMaxBytes, restored.radiationGridRle.Length);
            }
        }

        [Test]
        public void RadiationGridRuntime_WriteClampsNonFiniteValues()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.radiationDose = float.NaN;
            data.radiationGridOriginX = double.PositiveInfinity;
            data.radiationGridOriginY = double.NaN;
            data.radiationGridOriginZ = double.NegativeInfinity;
            data.radiationGridCellSizeMeters = float.NaN;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(0f, restored.radiationDose);
                Assert.AreEqual(0d, restored.radiationGridOriginX);
                Assert.AreEqual(0d, restored.radiationGridOriginY);
                Assert.AreEqual(0d, restored.radiationGridOriginZ);
                Assert.AreEqual(4f, restored.radiationGridCellSizeMeters);
                Assert.AreEqual(SaveData.RadiationGridRleMaxBytes, restored.radiationGridRle.Length);
            }
        }

        [Test]
        public void RadiationGridRuntime_ReadClampsNonFiniteFileValues()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.radiationDose = 7.25f;
            data.radiationGridOriginX = 101.25d;
            data.radiationGridOriginY = -202.5d;
            data.radiationGridOriginZ = 303.75d;
            data.radiationGridCellSizeMeters = 6.5f;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            int bytesWritten;
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
            }

            byte[] marker = BuildLittleEndianRadiationGridHeader(7.25f, 101.25d, -202.5d, 303.75d, 6.5f);
            byte[] replacement = BuildLittleEndianRadiationGridHeader(
                float.NaN,
                double.PositiveInfinity,
                double.NaN,
                double.NegativeInfinity,
                float.NaN);
            int radiationOffset = FindLittleEndianByteSequenceOffset(payload, bytesWritten, marker);
            Assert.GreaterOrEqual(radiationOffset, sizeof(int));
            Buffer.BlockCopy(replacement, 0, payload, radiationOffset, replacement.Length);

            fixed (byte* payloadPtr = payload)
            {
                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(0f, restored.radiationDose);
                Assert.AreEqual(0d, restored.radiationGridOriginX);
                Assert.AreEqual(0d, restored.radiationGridOriginY);
                Assert.AreEqual(0d, restored.radiationGridOriginZ);
                Assert.AreEqual(4f, restored.radiationGridCellSizeMeters);
            }
        }

        [Test]
        public void RadiationGridRuntimeMigration_PreV68DropsUnpersistedGrid()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.RadiationGridPersistenceVersion - 1;
            data.radiationDose = 7.25f;
            data.radiationGridOriginX = 101.25d;
            data.radiationGridOriginY = -202.5d;
            data.radiationGridOriginZ = 303.75d;
            data.radiationGridCellSizeMeters = 6.5f;
            data.radiationGridRleLength = SaveData.RadiationGridRlePacketSizeBytes;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.RadiationGridPersistenceVersion - 1, originalVersion);
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(0f, data.radiationDose);
            Assert.AreEqual(0d, data.radiationGridOriginX);
            Assert.AreEqual(0d, data.radiationGridOriginY);
            Assert.AreEqual(0d, data.radiationGridOriginZ);
            Assert.AreEqual(4f, data.radiationGridCellSizeMeters);
            Assert.AreEqual(0, data.radiationGridRleLength);
            Assert.IsNotNull(data.radiationGridRle);
            Assert.AreEqual(SaveData.RadiationGridRleMaxBytes, data.radiationGridRle.Length);
            StringAssert.Contains("radiation grid state repaired", summary);
        }

        [Test]
        public void RadiationGridRuntimeMigration_V68ClampsNonFiniteAndOversizedPayload()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.RadiationGridPersistenceVersion;
            data.radiationDose = float.NaN;
            data.radiationGridOriginX = double.PositiveInfinity;
            data.radiationGridOriginY = double.NaN;
            data.radiationGridOriginZ = double.NegativeInfinity;
            data.radiationGridCellSizeMeters = float.NaN;
            data.radiationGridRle = new byte[SaveData.RadiationGridRleMaxBytes + 16];
            data.radiationGridRleLength = SaveData.RadiationGridRleMaxBytes + 16;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.RadiationGridPersistenceVersion, originalVersion);
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(0f, data.radiationDose);
            Assert.AreEqual(0d, data.radiationGridOriginX);
            Assert.AreEqual(0d, data.radiationGridOriginY);
            Assert.AreEqual(0d, data.radiationGridOriginZ);
            Assert.AreEqual(4f, data.radiationGridCellSizeMeters);
            Assert.AreEqual(SaveData.RadiationGridRleMaxBytes, data.radiationGridRleLength);
            Assert.IsNotNull(data.radiationGridRle);
            Assert.AreEqual(SaveData.RadiationGridRleMaxBytes, data.radiationGridRle.Length);
            StringAssert.Contains("radiation grid state repaired", summary);
        }

        [Test]
        public void RadiationGridRuntimeMigration_CurrentRepairsMissingPayloadBuffer()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.radiationGridRle = null;
            data.radiationGridRleLength = SaveData.RadiationGridRlePacketSizeBytes;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(0, data.radiationGridRleLength);
            Assert.IsNotNull(data.radiationGridRle);
            Assert.AreEqual(SaveData.RadiationGridRleMaxBytes, data.radiationGridRle.Length);
            StringAssert.Contains("radiation grid state repaired", summary);
        }

        [Test]
        public void ResourceScarcityRuntime_WriteClampsNegativeCollectedCounts()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.resourceScarcity.EnsureCapacity();
            data.resourceScarcity.entryCount = 2;
            data.resourceScarcity.itemHashIds[0] = 101;
            data.resourceScarcity.itemHashIds[1] = 202;
            data.resourceScarcity.itemIds[0] = "CopperOre";
            data.resourceScarcity.itemIds[1] = "Quartz";
            data.resourceScarcity.collectedCounts[0] = -5;
            data.resourceScarcity.collectedCounts[1] = 7;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(2, restored.resourceScarcity.entryCount);
                Assert.AreEqual(ResourceScarcityDTO.MaxTrackedResources, restored.resourceScarcity.itemHashIds.Length);
                Assert.AreEqual(ResourceScarcityDTO.MaxTrackedResources, restored.resourceScarcity.itemIds.Length);
                Assert.AreEqual(ResourceScarcityDTO.MaxTrackedResources, restored.resourceScarcity.collectedCounts.Length);
                Assert.AreEqual(101, restored.resourceScarcity.itemHashIds[0]);
                Assert.AreEqual(202, restored.resourceScarcity.itemHashIds[1]);
                Assert.AreEqual(0, restored.resourceScarcity.collectedCounts[0]);
                Assert.AreEqual(7, restored.resourceScarcity.collectedCounts[1]);
            }
        }

        [Test]
        public void ResourceScarcityRuntime_WritePreservesIdOnlyEntries()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.resourceScarcity.entryCount = 2;
            data.resourceScarcity.itemHashIds = null;
            data.resourceScarcity.itemIds = new[] { "CopperOre", "Quartz" };
            data.resourceScarcity.collectedCounts = new[] { 4, 5 };

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(2, restored.resourceScarcity.entryCount);
                Assert.AreEqual(ResourceScarcityDTO.MaxTrackedResources, restored.resourceScarcity.itemHashIds.Length);
                Assert.AreEqual(ResourceScarcityDTO.MaxTrackedResources, restored.resourceScarcity.itemIds.Length);
                Assert.AreEqual(ResourceScarcityDTO.MaxTrackedResources, restored.resourceScarcity.collectedCounts.Length);
                Assert.AreEqual(LocHash.Compute("CopperOre"), restored.resourceScarcity.itemHashIds[0]);
                Assert.AreEqual(LocHash.Compute("Quartz"), restored.resourceScarcity.itemHashIds[1]);
                Assert.AreEqual(4, restored.resourceScarcity.collectedCounts[0]);
                Assert.AreEqual(5, restored.resourceScarcity.collectedCounts[1]);
            }
        }

        [Test]
        public void ResourceScarcityRuntime_WritePreservesHashOnlyEntries()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.resourceScarcity.entryCount = 2;
            data.resourceScarcity.itemHashIds = new[] { 303, 404 };
            data.resourceScarcity.itemIds = null;
            data.resourceScarcity.collectedCounts = new[] { 6, 8 };

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(2, restored.resourceScarcity.entryCount);
                Assert.AreEqual(ResourceScarcityDTO.MaxTrackedResources, restored.resourceScarcity.itemHashIds.Length);
                Assert.AreEqual(ResourceScarcityDTO.MaxTrackedResources, restored.resourceScarcity.itemIds.Length);
                Assert.AreEqual(ResourceScarcityDTO.MaxTrackedResources, restored.resourceScarcity.collectedCounts.Length);
                Assert.AreEqual(303, restored.resourceScarcity.itemHashIds[0]);
                Assert.AreEqual(404, restored.resourceScarcity.itemHashIds[1]);
                Assert.IsNull(restored.resourceScarcity.itemIds[0]);
                Assert.IsNull(restored.resourceScarcity.itemIds[1]);
                Assert.AreEqual(6, restored.resourceScarcity.collectedCounts[0]);
                Assert.AreEqual(8, restored.resourceScarcity.collectedCounts[1]);
            }
        }

        [Test]
        public void ResourceScarcityRuntimeMigration_PreV60PreservesIdOnlyEntriesAndRepairsCounts()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = 59;
            data.resourceScarcity.entryCount = 2;
            data.resourceScarcity.itemHashIds = null;
            data.resourceScarcity.itemIds = new[] { "CopperOre", "Quartz" };
            data.resourceScarcity.collectedCounts = new[] { -4, 3 };

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(59, originalVersion);
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(2, data.resourceScarcity.entryCount);
            Assert.AreEqual(ResourceScarcityDTO.MaxTrackedResources, data.resourceScarcity.itemHashIds.Length);
            Assert.AreEqual(ResourceScarcityDTO.MaxTrackedResources, data.resourceScarcity.itemIds.Length);
            Assert.AreEqual(ResourceScarcityDTO.MaxTrackedResources, data.resourceScarcity.collectedCounts.Length);
            Assert.AreEqual(LocHash.Compute("CopperOre"), data.resourceScarcity.itemHashIds[0]);
            Assert.AreEqual(LocHash.Compute("Quartz"), data.resourceScarcity.itemHashIds[1]);
            Assert.AreEqual(0, data.resourceScarcity.collectedCounts[0]);
            Assert.AreEqual(3, data.resourceScarcity.collectedCounts[1]);
            StringAssert.Contains("resource scarcity capacity repaired", summary);
            StringAssert.Contains("resource scarcity hash repaired", summary);
            StringAssert.Contains("resource scarcity collected counts repaired", summary);
        }

        [Test]
        public void ResourceScarcityRuntime_SaturatingAddPreventsCollectedCountOverflow()
        {
            Assert.AreEqual(5, ResourceScarcityDirector.SaturatingAddCollectedUnits(-12, 5));
            Assert.AreEqual(0, ResourceScarcityDirector.SaturatingAddCollectedUnits(-12, 0));
            Assert.AreEqual(9, ResourceScarcityDirector.SaturatingAddCollectedUnits(4, 5));
            Assert.AreEqual(int.MaxValue, ResourceScarcityDirector.SaturatingAddCollectedUnits(int.MaxValue - 2, 5));
            Assert.AreEqual(5L, ResourceScarcityDirector.SaturatingAddPositiveUnits(-12L, 5));
            Assert.AreEqual(12L, ResourceScarcityDirector.SaturatingAddPositiveUnits(12L, 0));
            Assert.AreEqual(long.MaxValue, ResourceScarcityDirector.SaturatingAddPositiveUnits(long.MaxValue - 2L, 5));
        }

        [Test]
        public void ResourceScarcityRuntime_SaturatingInflatedAmountGuardsOverflow()
        {
            Assert.AreEqual(0, ResourceScarcityDirector.SaturatingInflatedAmountAtLeastBase(-4, 3f));
            Assert.AreEqual(4, ResourceScarcityDirector.SaturatingInflatedAmountAtLeastBase(4, float.NaN));
            Assert.AreEqual(4, ResourceScarcityDirector.SaturatingInflatedAmountAtLeastBase(4, 0.5f));
            Assert.AreEqual(10, ResourceScarcityDirector.SaturatingInflatedAmountAtLeastBase(4, 2.5f));
            Assert.AreEqual(int.MaxValue, ResourceScarcityDirector.SaturatingInflatedAmountAtLeastBase(int.MaxValue - 1, 4f));
        }

        [Test]
        public void InventoryRuntime_WriteSanitizesMalformedInventoryState()
        {
            Assert.AreEqual(
                (byte)(
                    PlayerInventory.ItemGeneticFlags.Glow |
                    PlayerInventory.ItemGeneticFlags.Toxic |
                    PlayerInventory.ItemGeneticFlags.Edible |
                    PlayerInventory.ItemGeneticFlags.Harvestable),
                SaveData.InventoryItemGeneticsSupportedFlagsMask);
            Assert.AreEqual(1000, SaveData.InventoryDefaultQualityMilli);

            SaveData data = SaveData.CreateNew(0.0);
            data.inventory.EnsureCapacity();
            data.inventory.cellCount = 1;
            data.inventory.itemHashIds[0] = 12345;
            data.inventory.packedCellCoordinates[0] = InventoryDTO.PackCellCoordinate(2, 3);
            data.inventory.stackCounts[0] = 0;
            data.inventory.itemGeneticsWords[0] = 0xFF;
            data.inventory.qualityMilli[0] = 2000;
            data.inventory.totalWeight = float.NaN;
            data.inventory.gridColumns = -5;
            data.inventory.gridRows = InventoryDTO.MaxCells + 100;
            data.inventory.itemDurabilityRle = new byte[InventoryDTO.MaxDurabilityRleBytes + 16];
            data.inventory.itemDurabilityRleLength = data.inventory.itemDurabilityRle.Length;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(1, restored.inventory.cellCount);
                Assert.AreEqual(1, restored.inventory.stackCounts[0]);
                Assert.AreEqual(SaveData.InventoryDefaultQualityMilli, restored.inventory.qualityMilli[0]);
                Assert.AreEqual(SaveData.InventoryItemGeneticsSupportedFlagsMask, restored.inventory.itemGeneticsWords[0]);
                Assert.AreEqual(0f, restored.inventory.totalWeight);
                Assert.AreEqual(0, restored.inventory.gridColumns);
                Assert.AreEqual(InventoryDTO.MaxCells, restored.inventory.gridRows);
                Assert.AreEqual(InventoryDTO.MaxDurabilityRleBytes, restored.inventory.itemDurabilityRleLength);
                Assert.AreEqual(1, restored.inventoryShadow.cellCount);
                Assert.AreEqual(0, restored.inventoryShadow.payloadLength);
                Assert.AreEqual(0u, restored.inventoryShadow.payloadHash);
                Assert.AreEqual(0, restored.inventoryShadow.gridColumns);
                Assert.AreEqual(InventoryDTO.MaxCells, restored.inventoryShadow.gridRows);
                Assert.AreEqual(0f, restored.inventoryShadow.totalWeight);
                Assert.AreEqual(0, restored.inventoryShadow.flags);
                Assert.AreEqual(InventoryShadowDTO.SchemaVersion, restored.inventoryShadow.schemaVersion);
            }
        }

        [Test]
        public void InventoryRuntime_ShadowPayloadConstantsStaySharedWithSaveData()
        {
            FieldInfo bufferBytes = typeof(PlayerInventory).GetField(
                "InventoryShadowBufferBytes",
                BindingFlags.Static | BindingFlags.NonPublic);
            FieldInfo hashSeed = typeof(PlayerInventory).GetField(
                "Fnv1a32Offset",
                BindingFlags.Static | BindingFlags.NonPublic);
            FieldInfo hashPrime = typeof(PlayerInventory).GetField(
                "Fnv1a32Prime",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.IsNotNull(bufferBytes);
            Assert.IsNotNull(hashSeed);
            Assert.IsNotNull(hashPrime);
            Assert.AreEqual(SaveData.InventoryShadowPayloadMaxBytes, (int)bufferBytes.GetRawConstantValue());
            Assert.AreEqual(SaveData.InventoryShadowPayloadHashSeed, (uint)hashSeed.GetRawConstantValue());
            Assert.AreEqual(SaveData.InventoryShadowPayloadHashPrime, (uint)hashPrime.GetRawConstantValue());
        }

        [Test]
        public void InventoryRuntime_WriteFallsBackFromOversizedShadowPayload()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.inventory.EnsureCapacity();
            data.inventory.cellCount = 1;
            data.inventory.itemHashIds[0] = 12345;
            data.inventory.packedCellCoordinates[0] = InventoryDTO.PackCellCoordinate(4, 5);
            data.inventory.stackCounts[0] = 2;
            data.inventory.qualityMilli[0] = SaveData.InventoryDefaultQualityMilli;
            data.inventory.gridColumns = 10;
            data.inventory.gridRows = 8;

            data.hasInventoryShadowPayload = true;
            data.inventoryShadowPayload = new byte[SaveData.InventoryShadowPayloadMaxBytes + 1];
            data.inventoryShadowPayloadLength = data.inventoryShadowPayload.Length;
            data.inventoryShadowPayloadHash = 0xA5A5A5A5u;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(1, restored.inventory.cellCount);
                Assert.AreEqual(12345, restored.inventory.itemHashIds[0]);
                Assert.AreEqual(2, restored.inventory.stackCounts[0]);
                Assert.AreEqual(0, restored.inventoryShadow.payloadLength);
                Assert.AreEqual(0u, restored.inventoryShadow.payloadHash);
                Assert.AreEqual(0, restored.inventoryShadow.flags);
                Assert.AreEqual(1, restored.inventoryShadow.cellCount);
                Assert.AreEqual(10, restored.inventoryShadow.gridColumns);
                Assert.AreEqual(8, restored.inventoryShadow.gridRows);
            }
        }

        [Test]
        public void InventoryRuntime_WriteFallsBackFromTruncatedShadowPayload()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.inventory.EnsureCapacity();
            data.inventory.cellCount = 1;
            data.inventory.itemHashIds[0] = 67890;
            data.inventory.packedCellCoordinates[0] = InventoryDTO.PackCellCoordinate(3, 4);
            data.inventory.stackCounts[0] = 4;
            data.inventory.qualityMilli[0] = SaveData.InventoryDefaultQualityMilli;
            data.inventory.gridColumns = 9;
            data.inventory.gridRows = 7;

            data.hasInventoryShadowPayload = true;
            data.inventoryShadowPayload = new byte[1];
            data.inventoryShadowPayloadLength = SaveData.InventoryShadowPayloadMaxBytes;
            data.inventoryShadowPayloadHash = 0xC0FFEEu;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(1, restored.inventory.cellCount);
                Assert.AreEqual(67890, restored.inventory.itemHashIds[0]);
                Assert.AreEqual(4, restored.inventory.stackCounts[0]);
                Assert.AreEqual(0, restored.inventoryShadow.payloadLength);
                Assert.AreEqual(0u, restored.inventoryShadow.payloadHash);
                Assert.AreEqual(0, restored.inventoryShadow.flags);
                Assert.AreEqual(1, restored.inventoryShadow.cellCount);
                Assert.AreEqual(9, restored.inventoryShadow.gridColumns);
                Assert.AreEqual(7, restored.inventoryShadow.gridRows);
            }
        }

        [Test]
        public void InventoryShadowPayloadBudget_CoversWorstCaseInventoryDto()
        {
            long worstCaseBytes =
                sizeof(int) +
                EncodedStructArrayBytes<int>(InventoryDTO.MaxCells) +
                EncodedStructArrayBytes<uint>(InventoryDTO.MaxCells) +
                EncodedStructArrayBytes<ushort>(InventoryDTO.MaxCells) +
                EncodedStructArrayBytes<ushort>(InventoryDTO.MaxCells) +
                EncodedStructArrayBytes<byte>(InventoryDTO.MaxCells) +
                EncodedStructArrayBytes<ushort>(InventoryDTO.MaxCells) +
                EncodedStructArrayBytes<uint>(InventoryDTO.MaxCells) +
                EncodedStructArrayBytes<byte>(InventoryDTO.MaxDurabilityRleBytes) +
                sizeof(float) +
                sizeof(int) +
                sizeof(int);

            Assert.LessOrEqual(worstCaseBytes, SaveData.InventoryShadowPayloadMaxBytes);
            Assert.AreEqual(16 * 1024, SaveData.InventoryShadowPayloadMaxBytes);
        }

        [Test]
        public void InventoryShadowBuilder_DoesNotMutateInventoryArrays()
        {
            InventoryDTO inventory = default;
            inventory.EnsureCapacity();
            inventory.cellCount = 1;
            inventory.itemHashIds[0] = 123;
            inventory.packedCellCoordinates[0] = InventoryDTO.PackCellCoordinate(1, 1);
            inventory.stackCounts[0] = 0;
            inventory.qualityMilli[0] = 2000;
            inventory.itemGeneticsWords[0] = 0xF0;
            inventory.totalWeight = float.NaN;
            inventory.gridColumns = -1;
            inventory.gridRows = InventoryDTO.MaxCells + 1;

            InventoryShadowDTO shadow = SaveDataInventorySanitizer.BuildInventoryShadow(
                in inventory,
                12,
                0x12345678u,
                true);

            Assert.AreEqual(1, shadow.cellCount);
            Assert.AreEqual(0f, shadow.totalWeight);
            Assert.AreEqual(0, shadow.gridColumns);
            Assert.AreEqual(InventoryDTO.MaxCells, shadow.gridRows);
            Assert.AreEqual(12, shadow.payloadLength);
            Assert.AreEqual(0x12345678u, shadow.payloadHash);
            Assert.AreEqual(0, inventory.stackCounts[0]);
            Assert.AreEqual(2000, inventory.qualityMilli[0]);
            Assert.AreEqual(0xF0, inventory.itemGeneticsWords[0]);
            Assert.IsTrue(float.IsNaN(inventory.totalWeight));
            Assert.AreEqual(-1, inventory.gridColumns);
            Assert.AreEqual(InventoryDTO.MaxCells + 1, inventory.gridRows);
        }

        [Test]
        public void InventoryShadowPayloadValidation_RejectsRawMalformedPayloadEvenWhenHashMatches()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.inventory.EnsureCapacity();
            data.inventory.cellCount = 1;
            data.inventory.itemHashIds[0] = 454545;
            data.inventory.packedCellCoordinates[0] = InventoryDTO.PackCellCoordinate(2, 2);
            data.inventory.stackCounts[0] = 0;
            data.inventory.itemGeneticsWords[0] = 0xFF;
            data.inventory.qualityMilli[0] = 2000;
            data.inventory.totalWeight = float.NaN;
            data.inventory.gridColumns = -5;
            data.inventory.gridRows = InventoryDTO.MaxCells + 100;
            data.hasInventoryShadowPayload = true;
            data.inventoryShadowPayload = new byte[SaveData.InventoryShadowPayloadMaxBytes];
            data.inventoryShadowPayloadLength = WriteInventoryShadowPayload(in data.inventory, data.inventoryShadowPayload);
            data.inventoryShadowPayloadHash = SaveDataInventorySanitizer.ComputeInventoryShadowPayloadHash(
                data.inventoryShadowPayload,
                data.inventoryShadowPayloadLength);

            int payloadLength = SaveDataInventorySanitizer.ResolveInventoryShadowPayloadLength(data);

            Assert.AreEqual(0, payloadLength);
            Assert.AreNotEqual(
                data.inventoryShadowPayloadHash,
                SaveDataInventorySanitizer.ComputeInventoryShadowPayloadHash(in data.inventory));
        }

        [Test]
        public void InventoryRuntimeMigration_CurrentRepairsMalformedInventoryState()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.inventory.cellCount = 4;
            data.inventory.itemHashIds = new[] { 101, 202 };
            data.inventory.packedCellCoordinates = new[]
            {
                InventoryDTO.PackCellCoordinate(1, 1),
                InventoryDTO.PackCellCoordinate(2, 2)
            };
            data.inventory.stackCounts = new ushort[] { 0, 3 };
            data.inventory.itemStateFlags = new ushort[] { 0, 0 };
            data.inventory.itemGeneticsWords = new byte[] { 0xFF, 0x10 };
            data.inventory.qualityMilli = new ushort[] { 0, 2000 };
            data.inventory.lastUpdateUnixSeconds = new uint[] { 0u, 123u };
            data.inventory.itemDurabilityRle = new byte[] { 11, 22 };
            data.inventory.itemDurabilityRleLength = 99;
            data.inventory.totalWeight = float.NegativeInfinity;
            data.inventory.gridColumns = -1;
            data.inventory.gridRows = InventoryDTO.MaxCells + 1;
            data.inventoryShadow.cellCount = 99;
            data.inventoryShadow.payloadLength = int.MaxValue;
            data.inventoryShadow.payloadHash = 0x12345678u;
            data.inventoryShadow.gridColumns = 99;
            data.inventoryShadow.gridRows = -99;
            data.inventoryShadow.totalWeight = float.NaN;
            data.inventoryShadow.flags = InventoryShadowDTO.FlagHasPayload;
            data.inventoryShadow.schemaVersion = 0;
            data.inventoryShadow.reserved0 = 123;
            data.hasInventoryShadowPayload = true;
            data.inventoryShadowPayload = new byte[1];
            data.inventoryShadowPayloadLength = SaveData.InventoryShadowPayloadMaxBytes;
            data.inventoryShadowPayloadHash = 0x87654321u;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(2, data.inventory.cellCount);
            Assert.AreEqual(InventoryDTO.MaxCells, data.inventory.itemHashIds.Length);
            Assert.AreEqual(InventoryDTO.MaxCells, data.inventory.stackCounts.Length);
            Assert.AreEqual(InventoryDTO.MaxDurabilityRleBytes, data.inventory.itemDurabilityRle.Length);
            Assert.AreEqual(1, data.inventory.stackCounts[0]);
            Assert.AreEqual(3, data.inventory.stackCounts[1]);
            Assert.AreEqual(SaveData.InventoryDefaultQualityMilli, data.inventory.qualityMilli[0]);
            Assert.AreEqual(SaveData.InventoryDefaultQualityMilli, data.inventory.qualityMilli[1]);
            Assert.AreEqual(SaveData.InventoryItemGeneticsSupportedFlagsMask, data.inventory.itemGeneticsWords[0]);
            Assert.AreEqual(0x00, data.inventory.itemGeneticsWords[1]);
            Assert.AreEqual(2, data.inventory.itemDurabilityRleLength);
            Assert.AreEqual(0f, data.inventory.totalWeight);
            Assert.AreEqual(0, data.inventory.gridColumns);
            Assert.AreEqual(InventoryDTO.MaxCells, data.inventory.gridRows);
            Assert.AreEqual(2, data.inventoryShadow.cellCount);
            Assert.AreEqual(0, data.inventoryShadow.payloadLength);
            Assert.AreEqual(0u, data.inventoryShadow.payloadHash);
            Assert.AreEqual(0, data.inventoryShadow.gridColumns);
            Assert.AreEqual(InventoryDTO.MaxCells, data.inventoryShadow.gridRows);
            Assert.AreEqual(0f, data.inventoryShadow.totalWeight);
            Assert.AreEqual(0, data.inventoryShadow.flags);
            Assert.AreEqual(InventoryShadowDTO.SchemaVersion, data.inventoryShadow.schemaVersion);
            Assert.AreEqual(0, data.inventoryShadow.reserved0);
            Assert.IsFalse(data.hasInventoryShadowPayload);
            Assert.AreEqual(0, data.inventoryShadowPayloadLength);
            Assert.AreEqual(0u, data.inventoryShadowPayloadHash);
            StringAssert.Contains("inventory state repaired", summary);
            StringAssert.Contains("inventory shadow repaired", summary);
        }

        [Test]
        public void InventoryRuntimeMigration_CurrentPreservesHashValidShadowPayloadMetadata()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.inventory.EnsureCapacity();
            data.inventory.cellCount = 1;
            data.inventory.itemHashIds[0] = 31337;
            data.inventory.packedCellCoordinates[0] = InventoryDTO.PackCellCoordinate(1, 2);
            data.inventory.stackCounts[0] = 2;
            data.inventory.qualityMilli[0] = SaveData.InventoryDefaultQualityMilli;
            data.inventory.gridColumns = 4;
            data.inventory.gridRows = 3;
            data.hasInventoryShadowPayload = true;
            data.inventoryShadowPayload = new byte[SaveData.InventoryShadowPayloadMaxBytes];
            data.inventoryShadowPayloadLength = WriteInventoryShadowPayload(in data.inventory, data.inventoryShadowPayload);
            data.inventoryShadowPayloadHash = SaveDataInventorySanitizer.ComputeInventoryShadowPayloadHash(
                data.inventoryShadowPayload,
                data.inventoryShadowPayloadLength);
            Assert.AreEqual(
                data.inventoryShadowPayloadLength,
                SaveDataInventorySanitizer.ComputeInventoryShadowPayloadLength(in data.inventory));
            Assert.AreEqual(
                data.inventoryShadowPayloadHash,
                SaveDataInventorySanitizer.ComputeInventoryShadowPayloadHash(in data.inventory));

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(1, data.inventoryShadow.cellCount);
            Assert.AreEqual(data.inventoryShadowPayloadLength, data.inventoryShadow.payloadLength);
            Assert.AreEqual(data.inventoryShadowPayloadHash, data.inventoryShadow.payloadHash);
            Assert.AreEqual(4, data.inventoryShadow.gridColumns);
            Assert.AreEqual(3, data.inventoryShadow.gridRows);
            Assert.AreEqual(InventoryShadowDTO.FlagHasPayload, data.inventoryShadow.flags);
            Assert.AreEqual(InventoryShadowDTO.SchemaVersion, data.inventoryShadow.schemaVersion);
            StringAssert.Contains("inventory shadow repaired", summary);
        }

        [Test]
        public void InventoryRuntimeMigration_CurrentPreservesValidPersistedShadowMetadata()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.inventory.EnsureCapacity();
            data.inventory.cellCount = 1;
            data.inventory.itemHashIds[0] = 31339;
            data.inventory.packedCellCoordinates[0] = InventoryDTO.PackCellCoordinate(3, 2);
            data.inventory.stackCounts[0] = 4;
            data.inventory.qualityMilli[0] = SaveData.InventoryDefaultQualityMilli;
            data.inventory.gridColumns = 6;
            data.inventory.gridRows = 5;

            byte[] shadowPayload = new byte[SaveData.InventoryShadowPayloadMaxBytes];
            int shadowPayloadLength = WriteInventoryShadowPayload(in data.inventory, shadowPayload);
            uint shadowPayloadHash = SaveDataInventorySanitizer.ComputeInventoryShadowPayloadHash(
                shadowPayload,
                shadowPayloadLength);
            data.inventoryShadow = SaveDataInventorySanitizer.BuildInventoryShadow(
                in data.inventory,
                shadowPayloadLength,
                shadowPayloadHash,
                true);

            SaveDataMigration.MigrateInPlace(data, out int originalVersion, out _);

            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(1, data.inventoryShadow.cellCount);
            Assert.AreEqual(shadowPayloadLength, data.inventoryShadow.payloadLength);
            Assert.AreEqual(shadowPayloadHash, data.inventoryShadow.payloadHash);
            Assert.AreEqual(InventoryShadowDTO.FlagHasPayload, data.inventoryShadow.flags);
            Assert.IsFalse(data.hasInventoryShadowPayload);
            Assert.AreEqual(0, data.inventoryShadowPayloadLength);
            Assert.AreEqual(0u, data.inventoryShadowPayloadHash);
        }

        [Test]
        public void InventoryRuntime_WritePreservesHashValidShadowPayloadMirror()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.inventory.EnsureCapacity();
            data.inventory.cellCount = 1;
            data.inventory.itemHashIds[0] = 515151;
            data.inventory.packedCellCoordinates[0] = InventoryDTO.PackCellCoordinate(2, 1);
            data.inventory.stackCounts[0] = 3;
            data.inventory.itemGeneticsWords[0] = 0x05;
            data.inventory.qualityMilli[0] = SaveData.InventoryDefaultQualityMilli;
            data.inventory.lastUpdateUnixSeconds[0] = 123u;
            data.inventory.gridColumns = 7;
            data.inventory.gridRows = 6;
            data.inventory.totalWeight = 2.5f;
            data.hasInventoryShadowPayload = true;
            data.inventoryShadowPayload = new byte[SaveData.InventoryShadowPayloadMaxBytes];
            data.inventoryShadowPayloadLength = WriteInventoryShadowPayload(in data.inventory, data.inventoryShadowPayload);
            data.inventoryShadowPayloadHash = SaveDataInventorySanitizer.ComputeInventoryShadowPayloadHash(
                data.inventoryShadowPayload,
                data.inventoryShadowPayloadLength);

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(1, restored.inventory.cellCount);
                Assert.AreEqual(515151, restored.inventory.itemHashIds[0]);
                Assert.AreEqual(3, restored.inventory.stackCounts[0]);
                Assert.AreEqual(data.inventoryShadowPayloadLength, restored.inventoryShadow.payloadLength);
                Assert.AreEqual(data.inventoryShadowPayloadHash, restored.inventoryShadow.payloadHash);
                Assert.AreEqual(InventoryShadowDTO.FlagHasPayload, restored.inventoryShadow.flags);
                Assert.AreEqual(7, restored.inventoryShadow.gridColumns);
                Assert.AreEqual(6, restored.inventoryShadow.gridRows);
                Assert.AreEqual(2.5f, restored.inventoryShadow.totalWeight);
            }
        }

        [Test]
        public void InventoryRuntime_WritePreservesValidPersistedShadowMirrorWithoutTransientPayload()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.inventory.EnsureCapacity();
            data.inventory.cellCount = 1;
            data.inventory.itemHashIds[0] = 525252;
            data.inventory.packedCellCoordinates[0] = InventoryDTO.PackCellCoordinate(3, 1);
            data.inventory.stackCounts[0] = 2;
            data.inventory.qualityMilli[0] = SaveData.InventoryDefaultQualityMilli;
            data.inventory.gridColumns = 5;
            data.inventory.gridRows = 4;

            byte[] shadowPayload = new byte[SaveData.InventoryShadowPayloadMaxBytes];
            int shadowPayloadLength = WriteInventoryShadowPayload(in data.inventory, shadowPayload);
            uint shadowPayloadHash = SaveDataInventorySanitizer.ComputeInventoryShadowPayloadHash(
                shadowPayload,
                shadowPayloadLength);
            data.inventoryShadow = SaveDataInventorySanitizer.BuildInventoryShadow(
                in data.inventory,
                shadowPayloadLength,
                shadowPayloadHash,
                true);

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(1, restored.inventory.cellCount);
                Assert.AreEqual(525252, restored.inventory.itemHashIds[0]);
                Assert.AreEqual(shadowPayloadLength, restored.inventoryShadow.payloadLength);
                Assert.AreEqual(shadowPayloadHash, restored.inventoryShadow.payloadHash);
                Assert.AreEqual(InventoryShadowDTO.FlagHasPayload, restored.inventoryShadow.flags);
                Assert.AreEqual(5, restored.inventoryShadow.gridColumns);
                Assert.AreEqual(4, restored.inventoryShadow.gridRows);
            }
        }

        [Test]
        public void InventoryRuntime_ReadDropsShadowPayloadMirrorWhenBiologicalDecayMutatesInventory()
        {
            const ushort biologicalItemStateMask = 1 << 6;
            SaveData data = SaveData.CreateNew(0.0);
            data.playerStats.environmentTemperature = 4f;
            data.inventory.EnsureCapacity();
            data.inventory.cellCount = 1;
            data.inventory.itemHashIds[0] = 616161;
            data.inventory.packedCellCoordinates[0] = InventoryDTO.PackCellCoordinate(2, 2);
            data.inventory.stackCounts[0] = 1;
            data.inventory.itemStateFlags[0] = biologicalItemStateMask;
            data.inventory.qualityMilli[0] = SaveData.InventoryDefaultQualityMilli;
            data.inventory.lastUpdateUnixSeconds[0] = 1u;
            data.inventory.gridColumns = 4;
            data.inventory.gridRows = 4;
            data.hasInventoryShadowPayload = true;
            data.inventoryShadowPayload = new byte[SaveData.InventoryShadowPayloadMaxBytes];
            data.inventoryShadowPayloadLength = WriteInventoryShadowPayload(in data.inventory, data.inventoryShadowPayload);
            data.inventoryShadowPayloadHash = SaveDataInventorySanitizer.ComputeInventoryShadowPayloadHash(
                data.inventoryShadowPayload,
                data.inventoryShadowPayloadLength);

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(1, restored.inventory.cellCount);
                Assert.AreEqual(616161, restored.inventory.itemHashIds[0]);
                Assert.Less(restored.inventory.qualityMilli[0], SaveData.InventoryDefaultQualityMilli);
                Assert.AreNotEqual(1u, restored.inventory.lastUpdateUnixSeconds[0]);
                Assert.AreEqual(0, restored.inventoryShadow.payloadLength);
                Assert.AreEqual(0u, restored.inventoryShadow.payloadHash);
                Assert.AreEqual(0, restored.inventoryShadow.flags);
                Assert.AreEqual(4, restored.inventoryShadow.gridColumns);
                Assert.AreEqual(4, restored.inventoryShadow.gridRows);
            }
        }

        [Test]
        public void InventoryRuntime_WritePreservesValidShadowDtoMetadataWithoutTransientPayload()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.inventory.EnsureCapacity();
            data.inventory.cellCount = 1;
            data.inventory.itemHashIds[0] = 616161;
            data.inventory.packedCellCoordinates[0] = InventoryDTO.PackCellCoordinate(5, 4);
            data.inventory.stackCounts[0] = 6;
            data.inventory.qualityMilli[0] = SaveData.InventoryDefaultQualityMilli;
            data.inventory.gridColumns = 9;
            data.inventory.gridRows = 8;
            data.inventory.totalWeight = 3.75f;

            byte[] shadowPayload = new byte[SaveData.InventoryShadowPayloadMaxBytes];
            int shadowPayloadLength = WriteInventoryShadowPayload(in data.inventory, shadowPayload);
            uint shadowPayloadHash = SaveDataInventorySanitizer.ComputeInventoryShadowPayloadHash(
                shadowPayload,
                shadowPayloadLength);
            data.inventoryShadow = SaveDataInventorySanitizer.BuildInventoryShadow(
                in data.inventory,
                shadowPayloadLength,
                shadowPayloadHash,
                true);
            data.hasInventoryShadowPayload = false;
            data.inventoryShadowPayloadLength = 0;
            data.inventoryShadowPayloadHash = 0u;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(616161, restored.inventory.itemHashIds[0]);
                Assert.AreEqual(6, restored.inventory.stackCounts[0]);
                Assert.AreEqual(shadowPayloadLength, restored.inventoryShadow.payloadLength);
                Assert.AreEqual(shadowPayloadHash, restored.inventoryShadow.payloadHash);
                Assert.AreEqual(InventoryShadowDTO.FlagHasPayload, restored.inventoryShadow.flags);
                Assert.AreEqual(9, restored.inventoryShadow.gridColumns);
                Assert.AreEqual(8, restored.inventoryShadow.gridRows);
                Assert.AreEqual(3.75f, restored.inventoryShadow.totalWeight);
            }
        }

        [Test]
        public void InventoryRuntime_WriteRejectsHashMismatchedShadowPayload()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.inventory.EnsureCapacity();
            data.inventory.cellCount = 1;
            data.inventory.itemHashIds[0] = 424242;
            data.inventory.packedCellCoordinates[0] = InventoryDTO.PackCellCoordinate(6, 7);
            data.inventory.stackCounts[0] = 5;
            data.inventory.qualityMilli[0] = SaveData.InventoryDefaultQualityMilli;
            data.inventory.gridColumns = 12;
            data.inventory.gridRows = 9;
            data.hasInventoryShadowPayload = true;
            data.inventoryShadowPayload = new byte[64];
            data.inventoryShadowPayloadLength = data.inventoryShadowPayload.Length;
            data.inventoryShadowPayloadHash = 0xBADC0DEu;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(1, restored.inventory.cellCount);
                Assert.AreEqual(424242, restored.inventory.itemHashIds[0]);
                Assert.AreEqual(5, restored.inventory.stackCounts[0]);
                Assert.AreEqual(0, restored.inventoryShadow.payloadLength);
                Assert.AreEqual(0u, restored.inventoryShadow.payloadHash);
                Assert.AreEqual(0, restored.inventoryShadow.flags);
                Assert.AreEqual(1, restored.inventoryShadow.cellCount);
                Assert.AreEqual(12, restored.inventoryShadow.gridColumns);
                Assert.AreEqual(9, restored.inventoryShadow.gridRows);
            }
        }

        [Test]
        public void InventoryRuntime_WriteRejectsHashValidShadowPayloadFromDifferentInventory()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.inventory.EnsureCapacity();
            data.inventory.cellCount = 1;
            data.inventory.itemHashIds[0] = 11111;
            data.inventory.packedCellCoordinates[0] = InventoryDTO.PackCellCoordinate(1, 1);
            data.inventory.stackCounts[0] = 2;
            data.inventory.qualityMilli[0] = SaveData.InventoryDefaultQualityMilli;
            data.inventory.gridColumns = 4;
            data.inventory.gridRows = 4;

            InventoryDTO payloadInventory = default;
            payloadInventory.EnsureCapacity();
            payloadInventory.cellCount = 1;
            payloadInventory.itemHashIds[0] = 99999;
            payloadInventory.packedCellCoordinates[0] = InventoryDTO.PackCellCoordinate(8, 8);
            payloadInventory.stackCounts[0] = 7;
            payloadInventory.qualityMilli[0] = SaveData.InventoryDefaultQualityMilli;
            payloadInventory.gridColumns = 8;
            payloadInventory.gridRows = 8;

            data.hasInventoryShadowPayload = true;
            data.inventoryShadowPayload = new byte[SaveData.InventoryShadowPayloadMaxBytes];
            data.inventoryShadowPayloadLength = WriteInventoryShadowPayload(in payloadInventory, data.inventoryShadowPayload);
            data.inventoryShadowPayloadHash = SaveDataInventorySanitizer.ComputeInventoryShadowPayloadHash(
                data.inventoryShadowPayload,
                data.inventoryShadowPayloadLength);
            Assert.AreEqual(
                data.inventoryShadowPayloadHash,
                SaveDataInventorySanitizer.ComputeInventoryShadowPayloadHash(in payloadInventory));
            Assert.AreNotEqual(
                data.inventoryShadowPayloadHash,
                SaveDataInventorySanitizer.ComputeInventoryShadowPayloadHash(in data.inventory));

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(1, restored.inventory.cellCount);
                Assert.AreEqual(11111, restored.inventory.itemHashIds[0]);
                Assert.AreEqual(2, restored.inventory.stackCounts[0]);
                Assert.AreEqual(0, restored.inventoryShadow.payloadLength);
                Assert.AreEqual(0u, restored.inventoryShadow.payloadHash);
                Assert.AreEqual(0, restored.inventoryShadow.flags);
                Assert.AreEqual(4, restored.inventoryShadow.gridColumns);
                Assert.AreEqual(4, restored.inventoryShadow.gridRows);
            }
        }

        [Test]
        public void InventoryRuntimeMigration_CurrentDiscardsHashMismatchedShadowPayloadMetadata()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.inventory.EnsureCapacity();
            data.inventory.cellCount = 1;
            data.inventory.itemHashIds[0] = 31338;
            data.inventory.packedCellCoordinates[0] = InventoryDTO.PackCellCoordinate(2, 3);
            data.inventory.stackCounts[0] = 2;
            data.inventory.qualityMilli[0] = SaveData.InventoryDefaultQualityMilli;
            data.inventory.gridColumns = 5;
            data.inventory.gridRows = 4;
            data.inventoryShadow.payloadLength = 64;
            data.inventoryShadow.payloadHash = 0xBADC0DEu;
            data.inventoryShadow.flags = InventoryShadowDTO.FlagHasPayload;
            data.hasInventoryShadowPayload = true;
            data.inventoryShadowPayload = new byte[64];
            data.inventoryShadowPayloadLength = data.inventoryShadowPayload.Length;
            data.inventoryShadowPayloadHash = 0xBADC0DEu;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(1, data.inventoryShadow.cellCount);
            Assert.AreEqual(0, data.inventoryShadow.payloadLength);
            Assert.AreEqual(0u, data.inventoryShadow.payloadHash);
            Assert.AreEqual(0, data.inventoryShadow.flags);
            Assert.AreEqual(0, data.inventoryShadowPayloadLength);
            Assert.AreEqual(0u, data.inventoryShadowPayloadHash);
            Assert.IsFalse(data.hasInventoryShadowPayload);
            StringAssert.Contains("inventory shadow repaired", summary);
        }

        [Test]
        public void InventoryRuntimeMigration_CurrentDropsShadowPayloadWhenInventoryWasRepaired()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.inventory.EnsureCapacity();
            data.inventory.cellCount = 1;
            data.inventory.itemHashIds[0] = 31337;
            data.inventory.packedCellCoordinates[0] = InventoryDTO.PackCellCoordinate(1, 2);
            data.inventory.stackCounts[0] = 0;
            data.inventory.qualityMilli[0] = SaveData.InventoryDefaultQualityMilli;
            data.inventory.gridColumns = 4;
            data.inventory.gridRows = 3;
            data.hasInventoryShadowPayload = true;
            data.inventoryShadowPayload = new byte[64];
            data.inventoryShadowPayloadLength = data.inventoryShadowPayload.Length;
            data.inventoryShadowPayloadHash = 0xBADC0DEu;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(1, data.inventory.stackCounts[0]);
            Assert.AreEqual(0, data.inventoryShadow.payloadLength);
            Assert.AreEqual(0u, data.inventoryShadow.payloadHash);
            Assert.AreEqual(0, data.inventoryShadow.flags);
            Assert.AreEqual(0, data.inventoryShadowPayloadLength);
            Assert.AreEqual(0u, data.inventoryShadowPayloadHash);
            Assert.IsFalse(data.hasInventoryShadowPayload);
            StringAssert.Contains("inventory state repaired", summary);
            StringAssert.Contains("inventory shadow repaired", summary);
        }

        [Test]
        public void ConstructionRuntime_FloodMirrorsIgnoreInactiveBlitRecords()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.construction.EnsureCapacity();
            data.construction.moduleCount = 1;
            data.construction.moduleBlitCount = 0;
            data.construction.moduleBlitRecords[0] = new ModuleBlitDTO
            {
                moduleHashId = 777
            };
            data.construction.modules[0] = new ModuleDTO
            {
                integrity = 8f,
                repairIntegrityCap = 10f,
                airReserveNormalized = 0.75f,
                co2Normalized = 0.25f,
                isFlooded = true,
                health = 200
            };

            data.RefreshFirstHourDtoMirrors();

            Assert.AreEqual(1, data.construction.habitatFloodStateCount);
            Assert.AreEqual(0, data.construction.habitatFloodStates[0].moduleHashId);
            Assert.AreEqual(8f, data.construction.habitatFloodStates[0].integrity);
            Assert.AreEqual(HabitatFloodStateDTO.FlagFlooded, data.construction.habitatFloodStates[0].flags);

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(1, restored.construction.habitatFloodStateCount);
                Assert.AreEqual(0, restored.construction.habitatFloodStates[0].moduleHashId);
                Assert.AreEqual(8f, restored.construction.habitatFloodStates[0].integrity);
                Assert.AreEqual(HabitatFloodStateDTO.FlagFlooded, restored.construction.habitatFloodStates[0].flags);
            }
        }

        [Test]
        public void ConstructionRuntime_FloodMirrorsFallBackToGraphNodeHashWhenBlitInactive()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.construction.EnsureCapacity();
            data.construction.moduleCount = 1;
            data.construction.graphNodeCount = 1;
            data.construction.graphNodes[0] = new ModuleGraphNodeDTO
            {
                moduleHashId = 1234,
                rotW = 1f
            };
            data.construction.moduleBlitCount = 0;
            data.construction.moduleBlitRecords[0] = new ModuleBlitDTO
            {
                moduleHashId = 777
            };
            data.construction.modules[0] = new ModuleDTO
            {
                integrity = 9f,
                repairIntegrityCap = 10f,
                airReserveNormalized = 0.6f,
                co2Normalized = 0.1f,
                health = 199
            };

            data.RefreshFirstHourDtoMirrors();

            Assert.AreEqual(1, data.construction.habitatFloodStateCount);
            Assert.AreEqual(1234, data.construction.habitatFloodStates[0].moduleHashId);
            Assert.AreEqual(9f, data.construction.habitatFloodStates[0].integrity);

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(1, restored.construction.habitatFloodStateCount);
                Assert.AreEqual(1234, restored.construction.habitatFloodStates[0].moduleHashId);
                Assert.AreEqual(9f, restored.construction.habitatFloodStates[0].integrity);
            }
        }

        [Test]
        public void ConstructionRuntime_FloodMirrorsPreferActiveBlitHashOverGraphNodeHash()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.construction.EnsureCapacity();
            data.construction.moduleCount = 1;
            data.construction.graphNodeCount = 1;
            data.construction.graphNodes[0] = new ModuleGraphNodeDTO
            {
                moduleHashId = 1234,
                rotW = 1f
            };
            data.construction.moduleBlitCount = 1;
            data.construction.moduleBlitRecords[0] = new ModuleBlitDTO
            {
                moduleHashId = 777
            };
            data.construction.modules[0] = new ModuleDTO
            {
                integrity = 10f,
                repairIntegrityCap = 10f,
                airReserveNormalized = 0.5f,
                co2Normalized = 0.1f,
                health = 190
            };

            data.RefreshFirstHourDtoMirrors();

            Assert.AreEqual(1, data.construction.habitatFloodStateCount);
            Assert.AreEqual(777, data.construction.habitatFloodStates[0].moduleHashId);
            Assert.AreEqual(10f, data.construction.habitatFloodStates[0].integrity);
        }

        [Test]
        public void ConstructionRuntime_FloodMirrorsFallBackToGraphNodeHashWhenActiveBlitHashMissing()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.construction.EnsureCapacity();
            data.construction.moduleCount = 1;
            data.construction.graphNodeCount = 1;
            data.construction.graphNodes[0] = new ModuleGraphNodeDTO
            {
                moduleHashId = 4321,
                rotW = 1f
            };
            data.construction.moduleBlitCount = 1;
            data.construction.moduleBlitRecords[0] = new ModuleBlitDTO
            {
                moduleHashId = 0
            };
            data.construction.modules[0] = new ModuleDTO
            {
                integrity = 11f,
                repairIntegrityCap = 12f,
                airReserveNormalized = 0.7f,
                co2Normalized = 0.2f,
                health = 188
            };

            data.RefreshFirstHourDtoMirrors();

            Assert.AreEqual(1, data.construction.habitatFloodStateCount);
            Assert.AreEqual(4321, data.construction.habitatFloodStates[0].moduleHashId);
            Assert.AreEqual(11f, data.construction.habitatFloodStates[0].integrity);

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(1, restored.construction.habitatFloodStateCount);
                Assert.AreEqual(4321, restored.construction.habitatFloodStates[0].moduleHashId);
                Assert.AreEqual(11f, restored.construction.habitatFloodStates[0].integrity);
            }
        }

        [Test]
        public void ConstructionRuntimeMigration_CurrentUsesGraphNodeHashForFloodMirrorWhenBlitInactive()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.construction.EnsureCapacity();
            data.construction.moduleCount = 1;
            data.construction.graphNodeCount = 1;
            data.construction.graphNodes[0] = new ModuleGraphNodeDTO
            {
                moduleHashId = 1234,
                rotW = 1f
            };
            data.construction.moduleBlitCount = 0;
            data.construction.moduleBlitRecords[0] = new ModuleBlitDTO
            {
                moduleHashId = 777
            };
            data.construction.modules[0] = new ModuleDTO
            {
                integrity = 9f,
                repairIntegrityCap = 10f,
                airReserveNormalized = 0.6f,
                co2Normalized = 0.1f,
                health = 199
            };
            data.construction.habitatFloodStateCount = 1;
            data.construction.habitatFloodStates[0] = new HabitatFloodStateDTO
            {
                moduleHashId = 777,
                integrity = 1f
            };

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(1, data.construction.habitatFloodStateCount);
            Assert.AreEqual(1234, data.construction.habitatFloodStates[0].moduleHashId);
            Assert.AreEqual(9f, data.construction.habitatFloodStates[0].integrity);
            StringAssert.Contains("construction flood mirrors refreshed", summary);
        }

        [Test]
        public void ConstructionRuntimeMigration_CurrentUsesGraphNodeHashWhenActiveBlitHashMissing()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.construction.EnsureCapacity();
            data.construction.moduleCount = 1;
            data.construction.graphNodeCount = 1;
            data.construction.graphNodes[0] = new ModuleGraphNodeDTO
            {
                moduleHashId = 4321,
                rotW = 1f
            };
            data.construction.moduleBlitCount = 1;
            data.construction.moduleBlitRecords[0] = new ModuleBlitDTO
            {
                moduleHashId = 0
            };
            data.construction.modules[0] = new ModuleDTO
            {
                integrity = 11f,
                repairIntegrityCap = 12f,
                airReserveNormalized = 0.7f,
                co2Normalized = 0.2f,
                health = 188
            };
            data.construction.habitatFloodStateCount = 1;
            data.construction.habitatFloodStates[0] = new HabitatFloodStateDTO
            {
                moduleHashId = 0,
                integrity = 1f
            };

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(1, data.construction.habitatFloodStateCount);
            Assert.AreEqual(4321, data.construction.habitatFloodStates[0].moduleHashId);
            Assert.AreEqual(11f, data.construction.habitatFloodStates[0].integrity);
            StringAssert.Contains("construction flood mirrors refreshed", summary);
        }

        [Test]
        public void ConstructionRuntimeMigration_CurrentRefreshesFloodMirrorFromActiveModules()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.construction.EnsureCapacity();
            data.construction.moduleCount = 1;
            data.construction.moduleBlitCount = 0;
            data.construction.moduleBlitRecords[0] = new ModuleBlitDTO
            {
                moduleHashId = 888
            };
            data.construction.modules[0] = new ModuleDTO
            {
                integrity = 42f,
                repairIntegrityCap = 55f,
                airReserveNormalized = 0.8f,
                co2Normalized = 0.2f,
                isFlooded = true,
                health = 144
            };
            data.construction.habitatFloodStateCount = 1;
            data.construction.habitatFloodStates[0] = new HabitatFloodStateDTO
            {
                moduleHashId = 888,
                integrity = 1f,
                repairIntegrityCap = 2f,
                airReserveNormalized = 0.1f,
                co2Normalized = 0.9f,
                flags = 0,
                health = 1
            };

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(1, data.construction.habitatFloodStateCount);
            HabitatFloodStateDTO restored = data.construction.habitatFloodStates[0];
            Assert.AreEqual(0, restored.moduleHashId);
            Assert.AreEqual(42f, restored.integrity);
            Assert.AreEqual(55f, restored.repairIntegrityCap);
            Assert.AreEqual(0.8f, restored.airReserveNormalized);
            Assert.AreEqual(0.2f, restored.co2Normalized);
            Assert.AreEqual(HabitatFloodStateDTO.FlagFlooded, restored.flags);
            Assert.AreEqual(144, restored.health);
            StringAssert.Contains("construction flood mirrors refreshed", summary);
        }

        [Test]
        public void ConstructionRuntimeMigration_CurrentRepairsMalformedFloodStateMirror()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.construction.EnsureCapacity();
            data.construction.moduleCount = 1;
            data.construction.graphNodeCount = 1;
            data.construction.graphNodes[0] = new ModuleGraphNodeDTO
            {
                moduleHashId = 444,
                rotW = 1f
            };
            data.construction.modules[0] = new ModuleDTO
            {
                integrity = 0f,
                repairIntegrityCap = 0f,
                airReserveNormalized = 1f,
                co2Normalized = 0f,
                rotW = 1f,
                floodedReefFloodSeconds = 0f,
                isFlooded = true,
                interiorReefInfestationActive = true,
                failureMode = SaveData.ModuleFailureModeNone,
                health = 201
            };
            data.construction.habitatFloodStateCount = 1;
            data.construction.habitatFloodStates[0] = new HabitatFloodStateDTO
            {
                moduleHashId = 444,
                integrity = float.NaN,
                repairIntegrityCap = float.NegativeInfinity,
                airReserveNormalized = 3f,
                co2Normalized = -2f,
                floodedReefFloodSeconds = float.PositiveInfinity,
                flags = 0xFF,
                failureMode = 6,
                health = 201,
                reserved0 = 99
            };

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            HabitatFloodStateDTO restored = data.construction.habitatFloodStates[0];
            Assert.AreEqual(444, restored.moduleHashId);
            Assert.AreEqual(0f, restored.integrity);
            Assert.AreEqual(0f, restored.repairIntegrityCap);
            Assert.AreEqual(1f, restored.airReserveNormalized);
            Assert.AreEqual(0f, restored.co2Normalized);
            Assert.AreEqual(0f, restored.floodedReefFloodSeconds);
            Assert.AreEqual((byte)(HabitatFloodStateDTO.FlagFlooded | HabitatFloodStateDTO.FlagInfested), restored.flags);
            Assert.AreEqual(SaveData.ModuleFailureModeNone, restored.failureMode);
            Assert.AreEqual(201, restored.health);
            Assert.AreEqual(0, restored.reserved0);
            StringAssert.Contains("construction flood states repaired", summary);
        }

        [Test]
        public void ConstructionRuntimeMigration_CurrentClampsFloodMirrorCountToActiveModules()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.construction.EnsureCapacity();
            data.construction.moduleCount = 0;
            data.construction.habitatFloodStateCount = 1;
            data.construction.habitatFloodStates[0] = new HabitatFloodStateDTO
            {
                moduleHashId = 444,
                integrity = 5f
            };

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(0, data.construction.habitatFloodStateCount);
            StringAssert.Contains("construction flood count clamped", summary);
        }

        [Test]
        public void ConstructionRuntimeMigration_CurrentRepairsMalformedModuleState()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.construction.EnsureCapacity();
            data.construction.moduleCount = 1;
            data.construction.modules[0] = new ModuleDTO
            {
                prefabId = " ",
                slottedToolItemId = "\t",
                pipeInFlightItemId = null,
                pipeInFlightAmount = -1,
                pipeTransitProgress = float.PositiveInfinity,
                pipeExportTimerSeconds = -5f,
                drillBufferedItemId = " ",
                drillBufferedAmount = -7,
                drillCycleTimerSeconds = float.NaN,
                sorterBufferedSlotCount = 1,
                sorterBufferedItemIds = new[] { " ", "CopperOre" },
                sorterBufferedQuantities = new[] { -3, 8 },
                posX = float.NaN,
                posY = 2f,
                posZ = float.PositiveInfinity,
                rotX = 0f,
                rotY = 0f,
                rotZ = 0f,
                rotW = 0f,
                integrity = float.NegativeInfinity,
                repairIntegrityCap = float.NaN,
                airReserveNormalized = 4f,
                co2Normalized = -4f,
                failureMode = 9,
                floodedReefFloodSeconds = float.PositiveInfinity,
                cultivationSlotCount = 1,
                cultivationSeedItemIds = new[] { "\t", "CreepvineSeed" },
                cultivationGeneticsMasks = new[] { ModuleDTO.CultivationGeneticsSupportedMask | (1UL << 32), 0UL },
                cultivationGrowth01 = new[] { -2f, 0.5f },
                cultivationQuality01 = new[] { float.NaN, 0.5f }
            };

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            ModuleDTO restored = data.construction.modules[0];
            Assert.AreEqual(string.Empty, restored.prefabId);
            Assert.AreEqual(string.Empty, restored.slottedToolItemId);
            Assert.AreEqual(string.Empty, restored.pipeInFlightItemId);
            Assert.AreEqual(0, restored.pipeInFlightAmount);
            Assert.AreEqual(0f, restored.pipeTransitProgress);
            Assert.AreEqual(0f, restored.pipeExportTimerSeconds);
            Assert.AreEqual(string.Empty, restored.drillBufferedItemId);
            Assert.AreEqual(0, restored.drillBufferedAmount);
            Assert.AreEqual(0f, restored.drillCycleTimerSeconds);
            Assert.AreEqual(string.Empty, restored.sorterBufferedItemIds[0]);
            Assert.AreEqual(0, restored.sorterBufferedQuantities[0]);
            Assert.AreEqual(0f, restored.posX);
            Assert.AreEqual(2f, restored.posY);
            Assert.AreEqual(0f, restored.posZ);
            Assert.AreEqual(0f, restored.rotX);
            Assert.AreEqual(0f, restored.rotY);
            Assert.AreEqual(0f, restored.rotZ);
            Assert.AreEqual(1f, restored.rotW);
            Assert.AreEqual(0f, restored.integrity);
            Assert.AreEqual(0f, restored.repairIntegrityCap);
            Assert.AreEqual(1f, restored.airReserveNormalized);
            Assert.AreEqual(0f, restored.co2Normalized);
            Assert.AreEqual(SaveData.ModuleFailureModeNone, restored.failureMode);
            Assert.AreEqual(0f, restored.floodedReefFloodSeconds);
            Assert.AreEqual(string.Empty, restored.cultivationSeedItemIds[0]);
            Assert.AreEqual(ModuleDTO.CultivationGeneticsSupportedMask, restored.cultivationGeneticsMasks[0]);
            Assert.AreEqual(0f, restored.cultivationGrowth01[0]);
            Assert.AreEqual(0f, restored.cultivationQuality01[0]);
            StringAssert.Contains("construction module state repaired", summary);
        }

        [Test]
        public void ConstructionRuntimeMigration_CurrentRepairsOnlyModuleStringState()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.construction.EnsureCapacity();
            data.construction.moduleCount = 1;

            ModuleDTO module = new ModuleDTO
            {
                prefabId = " ",
                slottedToolItemId = "\t",
                pipeInFlightItemId = null,
                drillBufferedItemId = " ",
                sorterBufferedSlotCount = 1,
                cultivationSlotCount = 1,
                rotW = 1f
            };
            module.EnsureNestedArrayCapacity();
            module.sorterBufferedItemIds[0] = " ";
            module.sorterBufferedQuantities[0] = 2;
            module.cultivationSeedItemIds[0] = "\t";
            module.cultivationGeneticsMasks[0] = 7UL;
            module.cultivationGrowth01[0] = 0.5f;
            module.cultivationQuality01[0] = 0.75f;
            data.construction.modules[0] = module;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            ModuleDTO restored = data.construction.modules[0];
            Assert.AreEqual(string.Empty, restored.prefabId);
            Assert.AreEqual(string.Empty, restored.slottedToolItemId);
            Assert.AreEqual(string.Empty, restored.pipeInFlightItemId);
            Assert.AreEqual(string.Empty, restored.drillBufferedItemId);
            Assert.AreEqual(string.Empty, restored.sorterBufferedItemIds[0]);
            Assert.AreEqual(2, restored.sorterBufferedQuantities[0]);
            Assert.AreEqual(string.Empty, restored.cultivationSeedItemIds[0]);
            Assert.AreEqual(7UL, restored.cultivationGeneticsMasks[0]);
            Assert.AreEqual(0.5f, restored.cultivationGrowth01[0]);
            Assert.AreEqual(0.75f, restored.cultivationQuality01[0]);
            StringAssert.Contains("construction module state repaired", summary);
        }

        [Test]
        public void ConstructionRuntimeMigration_CurrentRepairsModuleNestedArrayCapacity()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.construction.EnsureCapacity();
            data.construction.moduleCount = 1;
            data.construction.modules[0] = new ModuleDTO
            {
                prefabId = "HabitatLocker",
                sorterBufferedSlotCount = 2,
                sorterBufferedItemIds = new[] { "CopperOre", "Quartz" },
                sorterBufferedQuantities = new[] { 2, 3 },
                rotW = 1f,
                integrity = 100f,
                repairIntegrityCap = 100f,
                cultivationSlotCount = 1,
                cultivationSeedItemIds = new[] { "CreepvineSeed" },
                cultivationGeneticsMasks = new[] { 9UL },
                cultivationGrowth01 = new[] { 0.5f },
                cultivationQuality01 = new[] { 0.75f }
            };

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            ModuleDTO restored = data.construction.modules[0];
            Assert.AreEqual(ModuleDTO.MaxSorterBufferedSlots, restored.sorterBufferedItemIds.Length);
            Assert.AreEqual(ModuleDTO.MaxSorterBufferedSlots, restored.sorterBufferedQuantities.Length);
            Assert.AreEqual(ModuleDTO.MaxCultivationSlots, restored.cultivationSeedItemIds.Length);
            Assert.AreEqual(ModuleDTO.MaxCultivationSlots, restored.cultivationGeneticsMasks.Length);
            Assert.AreEqual(ModuleDTO.MaxCultivationSlots, restored.cultivationGrowth01.Length);
            Assert.AreEqual(ModuleDTO.MaxCultivationSlots, restored.cultivationQuality01.Length);
            Assert.AreEqual("CopperOre", restored.sorterBufferedItemIds[0]);
            Assert.AreEqual("Quartz", restored.sorterBufferedItemIds[1]);
            Assert.AreEqual(2, restored.sorterBufferedQuantities[0]);
            Assert.AreEqual(3, restored.sorterBufferedQuantities[1]);
            Assert.AreEqual("CreepvineSeed", restored.cultivationSeedItemIds[0]);
            Assert.AreEqual(9UL, restored.cultivationGeneticsMasks[0]);
            Assert.AreEqual(0.5f, restored.cultivationGrowth01[0]);
            Assert.AreEqual(0.75f, restored.cultivationQuality01[0]);
            StringAssert.Contains("construction module state repaired", summary);
        }

        [Test]
        public void ConstructionRuntimeMigration_CurrentTrimsOversizedModuleNestedArrays()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.construction.EnsureCapacity();
            data.construction.moduleCount = 1;

            string[] sorterItemIds = new string[ModuleDTO.MaxSorterBufferedSlots + 1];
            int[] sorterQuantities = new int[ModuleDTO.MaxSorterBufferedSlots + 1];
            string[] seedItemIds = new string[ModuleDTO.MaxCultivationSlots + 1];
            ulong[] geneticsMasks = new ulong[ModuleDTO.MaxCultivationSlots + 1];
            float[] growthValues = new float[ModuleDTO.MaxCultivationSlots + 1];
            float[] qualityValues = new float[ModuleDTO.MaxCultivationSlots + 1];
            sorterItemIds[0] = "CopperOre";
            sorterItemIds[1] = "Quartz";
            sorterQuantities[0] = 2;
            sorterQuantities[1] = 3;
            sorterQuantities[ModuleDTO.MaxSorterBufferedSlots] = 999;
            seedItemIds[0] = "CreepvineSeed";
            geneticsMasks[0] = 9UL;
            growthValues[0] = 0.5f;
            qualityValues[0] = 0.75f;
            growthValues[ModuleDTO.MaxCultivationSlots] = 1f;
            qualityValues[ModuleDTO.MaxCultivationSlots] = 1f;

            data.construction.modules[0] = new ModuleDTO
            {
                prefabId = "HabitatLocker",
                sorterBufferedSlotCount = 2,
                sorterBufferedItemIds = sorterItemIds,
                sorterBufferedQuantities = sorterQuantities,
                rotW = 1f,
                integrity = 100f,
                repairIntegrityCap = 100f,
                cultivationSlotCount = 1,
                cultivationSeedItemIds = seedItemIds,
                cultivationGeneticsMasks = geneticsMasks,
                cultivationGrowth01 = growthValues,
                cultivationQuality01 = qualityValues
            };

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            ModuleDTO restored = data.construction.modules[0];
            Assert.AreEqual(ModuleDTO.MaxSorterBufferedSlots, restored.sorterBufferedItemIds.Length);
            Assert.AreEqual(ModuleDTO.MaxSorterBufferedSlots, restored.sorterBufferedQuantities.Length);
            Assert.AreEqual(ModuleDTO.MaxCultivationSlots, restored.cultivationSeedItemIds.Length);
            Assert.AreEqual(ModuleDTO.MaxCultivationSlots, restored.cultivationGeneticsMasks.Length);
            Assert.AreEqual(ModuleDTO.MaxCultivationSlots, restored.cultivationGrowth01.Length);
            Assert.AreEqual(ModuleDTO.MaxCultivationSlots, restored.cultivationQuality01.Length);
            Assert.AreEqual("CopperOre", restored.sorterBufferedItemIds[0]);
            Assert.AreEqual("Quartz", restored.sorterBufferedItemIds[1]);
            Assert.AreEqual(2, restored.sorterBufferedQuantities[0]);
            Assert.AreEqual(3, restored.sorterBufferedQuantities[1]);
            Assert.AreEqual("CreepvineSeed", restored.cultivationSeedItemIds[0]);
            Assert.AreEqual(9UL, restored.cultivationGeneticsMasks[0]);
            Assert.AreEqual(0.5f, restored.cultivationGrowth01[0]);
            Assert.AreEqual(0.75f, restored.cultivationQuality01[0]);
            StringAssert.Contains("construction module state repaired", summary);
        }

        [Test]
        public void ConstructionRuntime_WriteSanitizesMalformedModuleState()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.construction.EnsureCapacity();
            data.construction.moduleCount = 1;
            data.construction.modules[0] = new ModuleDTO
            {
                prefabId = " ",
                slottedToolItemId = "\t",
                pipeInFlightItemId = null,
                pipeInFlightAmount = -5,
                pipeTransitProgress = float.NaN,
                pipeExportTimerSeconds = float.PositiveInfinity,
                drillBufferedItemId = " ",
                drillBufferedAmount = -9,
                drillCycleTimerSeconds = -1f,
                sorterBufferedSlotCount = 1,
                sorterBufferedItemIds = new[] { " ", string.Empty },
                sorterBufferedQuantities = new[] { -4, 2 },
                posX = float.NegativeInfinity,
                posY = 5f,
                posZ = float.NaN,
                rotX = 0f,
                rotY = 0f,
                rotZ = 0f,
                rotW = 0f,
                integrity = float.PositiveInfinity,
                repairIntegrityCap = float.NaN,
                airReserveNormalized = -1f,
                co2Normalized = 2f,
                failureMode = 9,
                floodedReefFloodSeconds = -5f,
                cultivationSlotCount = 1,
                cultivationSeedItemIds = new[] { "\t", string.Empty },
                cultivationGeneticsMasks = new[] { ModuleDTO.CultivationGeneticsSupportedMask | (1UL << 32), 0UL },
                cultivationGrowth01 = new[] { float.PositiveInfinity, 0.5f },
                cultivationQuality01 = new[] { -1f, 0.5f }
            };

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(1, restoredData.construction.moduleCount);
                ModuleDTO restored = restoredData.construction.modules[0];
                Assert.AreEqual(string.Empty, restored.prefabId);
                Assert.AreEqual(string.Empty, restored.slottedToolItemId);
                Assert.AreEqual(string.Empty, restored.pipeInFlightItemId);
                Assert.AreEqual(0, restored.pipeInFlightAmount);
                Assert.AreEqual(0f, restored.pipeTransitProgress);
                Assert.AreEqual(0f, restored.pipeExportTimerSeconds);
                Assert.AreEqual(string.Empty, restored.drillBufferedItemId);
                Assert.AreEqual(0, restored.drillBufferedAmount);
                Assert.AreEqual(0f, restored.drillCycleTimerSeconds);
                Assert.AreEqual(string.Empty, restored.sorterBufferedItemIds[0]);
                Assert.AreEqual(0, restored.sorterBufferedQuantities[0]);
                Assert.AreEqual(0f, restored.posX);
                Assert.AreEqual(5f, restored.posY);
                Assert.AreEqual(0f, restored.posZ);
                Assert.AreEqual(0f, restored.rotX);
                Assert.AreEqual(0f, restored.rotY);
                Assert.AreEqual(0f, restored.rotZ);
                Assert.AreEqual(1f, restored.rotW);
                Assert.AreEqual(0f, restored.integrity);
                Assert.AreEqual(0f, restored.repairIntegrityCap);
                Assert.AreEqual(0f, restored.airReserveNormalized);
                Assert.AreEqual(1f, restored.co2Normalized);
                Assert.AreEqual(SaveData.ModuleFailureModeNone, restored.failureMode);
                Assert.AreEqual(0f, restored.floodedReefFloodSeconds);
                Assert.AreEqual(string.Empty, restored.cultivationSeedItemIds[0]);
                Assert.AreEqual(ModuleDTO.CultivationGeneticsSupportedMask, restored.cultivationGeneticsMasks[0]);
                Assert.AreEqual(0f, restored.cultivationGrowth01[0]);
                Assert.AreEqual(0f, restored.cultivationQuality01[0]);
            }
        }

        [Test]
        public void ConstructionRuntime_WriteSanitizesMalformedGraphAndBlitRecords()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.construction.EnsureCapacity();
            data.construction.graphNodeCount = 1;
            data.construction.graphNodes[0] = new ModuleGraphNodeDTO
            {
                prefabId = null,
                moduleHashId = 42,
                aupGridX = -12,
                aupGridY = 3,
                aupGridZ = 99,
                aupLocalX = float.NaN,
                aupLocalY = 2f,
                aupLocalZ = float.PositiveInfinity,
                rotX = 0f,
                rotY = 0f,
                rotZ = 0f,
                rotW = 0f
            };
            data.construction.moduleBlitCount = 1;
            data.construction.moduleBlitRecords[0] = new ModuleBlitDTO
            {
                prefabHashId = 313,
                moduleHashId = 42,
                aupGridX = -12,
                aupGridY = 3,
                aupGridZ = 99,
                aupLocalX = float.NegativeInfinity,
                aupLocalY = 4f,
                aupLocalZ = float.NaN,
                rotX = float.PositiveInfinity,
                rotY = 0f,
                rotZ = 0f,
                rotW = 1f,
                health = 201,
                flags = 0xFF,
                failureMode = 99,
                reserved = 7
            };

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(1, restoredData.construction.graphNodeCount);
                ModuleGraphNodeDTO graphNode = restoredData.construction.graphNodes[0];
                Assert.AreEqual(string.Empty, graphNode.prefabId);
                Assert.AreEqual(42, graphNode.moduleHashId);
                Assert.AreEqual(-12, graphNode.aupGridX);
                Assert.AreEqual(3, graphNode.aupGridY);
                Assert.AreEqual(99, graphNode.aupGridZ);
                Assert.AreEqual(0f, graphNode.aupLocalX);
                Assert.AreEqual(2f, graphNode.aupLocalY);
                Assert.AreEqual(0f, graphNode.aupLocalZ);
                Assert.AreEqual(0f, graphNode.rotX);
                Assert.AreEqual(0f, graphNode.rotY);
                Assert.AreEqual(0f, graphNode.rotZ);
                Assert.AreEqual(1f, graphNode.rotW);

                Assert.AreEqual(1, restoredData.construction.moduleBlitCount);
                ModuleBlitDTO blit = restoredData.construction.moduleBlitRecords[0];
                Assert.AreEqual(313, blit.prefabHashId);
                Assert.AreEqual(42, blit.moduleHashId);
                Assert.AreEqual(-12, blit.aupGridX);
                Assert.AreEqual(3, blit.aupGridY);
                Assert.AreEqual(99, blit.aupGridZ);
                Assert.AreEqual(0f, blit.aupLocalX);
                Assert.AreEqual(4f, blit.aupLocalY);
                Assert.AreEqual(0f, blit.aupLocalZ);
                Assert.AreEqual(0f, blit.rotX);
                Assert.AreEqual(0f, blit.rotY);
                Assert.AreEqual(0f, blit.rotZ);
                Assert.AreEqual(1f, blit.rotW);
                Assert.AreEqual(201, blit.health);
                Assert.AreEqual((byte)(ModuleBlitDTO.FlagFlooded | ModuleBlitDTO.FlagInteriorReef), blit.flags);
                Assert.AreEqual(0, blit.failureMode);
                Assert.AreEqual(0, blit.reserved);
            }
        }

        [Test]
        public void ConstructionRuntimeMigration_CurrentRepairsMalformedGraphAndBlitRecords()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.construction.EnsureCapacity();
            data.construction.graphNodeCount = 1;
            data.construction.graphNodes[0] = new ModuleGraphNodeDTO
            {
                prefabId = null,
                moduleHashId = 42,
                aupLocalX = float.NaN,
                aupLocalY = 2f,
                aupLocalZ = float.PositiveInfinity,
                rotX = 0f,
                rotY = 0f,
                rotZ = 0f,
                rotW = 0f
            };
            data.construction.moduleBlitCount = 1;
            data.construction.moduleBlitRecords[0] = new ModuleBlitDTO
            {
                prefabHashId = 313,
                moduleHashId = 42,
                aupLocalX = float.NegativeInfinity,
                aupLocalY = 4f,
                aupLocalZ = float.NaN,
                rotX = float.PositiveInfinity,
                rotY = 0f,
                rotZ = 0f,
                rotW = 1f,
                flags = 0xFF,
                failureMode = 99,
                reserved = 7
            };

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            ModuleGraphNodeDTO graphNode = data.construction.graphNodes[0];
            Assert.AreEqual(string.Empty, graphNode.prefabId);
            Assert.AreEqual(0f, graphNode.aupLocalX);
            Assert.AreEqual(2f, graphNode.aupLocalY);
            Assert.AreEqual(0f, graphNode.aupLocalZ);
            Assert.AreEqual(0f, graphNode.rotX);
            Assert.AreEqual(0f, graphNode.rotY);
            Assert.AreEqual(0f, graphNode.rotZ);
            Assert.AreEqual(1f, graphNode.rotW);

            ModuleBlitDTO blit = data.construction.moduleBlitRecords[0];
            Assert.AreEqual(0f, blit.aupLocalX);
            Assert.AreEqual(4f, blit.aupLocalY);
            Assert.AreEqual(0f, blit.aupLocalZ);
            Assert.AreEqual(0f, blit.rotX);
            Assert.AreEqual(0f, blit.rotY);
            Assert.AreEqual(0f, blit.rotZ);
            Assert.AreEqual(1f, blit.rotW);
            Assert.AreEqual((byte)(ModuleBlitDTO.FlagFlooded | ModuleBlitDTO.FlagInteriorReef), blit.flags);
            Assert.AreEqual(0, blit.failureMode);
            Assert.AreEqual(0, blit.reserved);
            StringAssert.Contains("construction graph nodes repaired", summary);
            StringAssert.Contains("construction blit records repaired", summary);
        }

        [Test]
        public void ConstructionRuntime_WriteCompactsMalformedGraphEdges()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.construction.EnsureCapacity();
            data.construction.moduleCount = 2;
            data.construction.graphNodeCount = 2;
            data.construction.graphNodes[0] = new ModuleGraphNodeDTO { prefabId = "A", moduleHashId = 1, rotW = 1f };
            data.construction.graphNodes[1] = new ModuleGraphNodeDTO { prefabId = "B", moduleHashId = 2, rotW = 1f };
            data.construction.graphEdgeCount = 3;
            data.construction.graphEdges[0] = new ModuleGraphEdgeDTO
            {
                sourceNodeIndex = 1,
                destinationNodeIndex = 0
            };
            data.construction.graphEdges[1] = new ModuleGraphEdgeDTO
            {
                sourceNodeIndex = 0,
                destinationNodeIndex = 0
            };
            data.construction.graphEdges[2] = new ModuleGraphEdgeDTO
            {
                sourceNodeIndex = 0,
                destinationNodeIndex = 3
            };

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(1, restoredData.construction.graphEdgeCount);
                Assert.AreEqual(0, restoredData.construction.graphEdges[0].sourceNodeIndex);
                Assert.AreEqual(1, restoredData.construction.graphEdges[0].destinationNodeIndex);
            }
        }

        [Test]
        public void ConstructionRuntime_ReadSanitizerCompactsMalformedGraphEdges()
        {
            ConstructionDTO construction = ConstructionDTO.CreatePreallocated();
            construction.moduleCount = 2;
            construction.graphNodeCount = 2;
            construction.graphNodes[0] = new ModuleGraphNodeDTO { prefabId = "A", moduleHashId = 1, rotW = 1f };
            construction.graphNodes[1] = new ModuleGraphNodeDTO { prefabId = "B", moduleHashId = 2, rotW = 1f };
            construction.graphEdgeCount = 3;
            construction.graphEdges[0] = new ModuleGraphEdgeDTO
            {
                sourceNodeIndex = 1,
                destinationNodeIndex = 0
            };
            construction.graphEdges[1] = new ModuleGraphEdgeDTO
            {
                sourceNodeIndex = 0,
                destinationNodeIndex = 0
            };
            construction.graphEdges[2] = new ModuleGraphEdgeDTO
            {
                sourceNodeIndex = 0,
                destinationNodeIndex = 9
            };

            MethodInfo sanitizer = typeof(SaveBinaryPayloadCodec).GetMethod(
                "SanitizeConstructionGraphEdgesAfterRead",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(sanitizer);

            object[] args = { construction };
            sanitizer.Invoke(null, args);
            ConstructionDTO sanitized = (ConstructionDTO)args[0];

            Assert.AreEqual(2, sanitized.graphNodeCount);
            Assert.AreEqual(1, sanitized.graphEdgeCount);
            Assert.AreEqual(0, sanitized.graphEdges[0].sourceNodeIndex);
            Assert.AreEqual(1, sanitized.graphEdges[0].destinationNodeIndex);
        }

        [Test]
        public void ConstructionRuntimeMigration_CurrentCompactsMalformedGraphEdges()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.construction.EnsureCapacity();
            data.construction.moduleCount = 2;
            data.construction.graphNodeCount = 2;
            data.construction.graphEdgeCount = 3;
            data.construction.graphEdges[0] = new ModuleGraphEdgeDTO
            {
                sourceNodeIndex = 1,
                destinationNodeIndex = 0
            };
            data.construction.graphEdges[1] = new ModuleGraphEdgeDTO
            {
                sourceNodeIndex = 0,
                destinationNodeIndex = 0
            };
            data.construction.graphEdges[2] = new ModuleGraphEdgeDTO
            {
                sourceNodeIndex = -1,
                destinationNodeIndex = 1
            };

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(1, data.construction.graphEdgeCount);
            Assert.AreEqual(0, data.construction.graphEdges[0].sourceNodeIndex);
            Assert.AreEqual(1, data.construction.graphEdges[0].destinationNodeIndex);
            StringAssert.Contains("construction graph edges repaired", summary);
        }

        [Test]
        public void ConstructionRuntime_WriteDropsDuplicateGraphEdges()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.construction.EnsureCapacity();
            data.construction.moduleCount = 3;
            data.construction.graphNodeCount = 3;
            data.construction.graphNodes[0] = new ModuleGraphNodeDTO { prefabId = "A", moduleHashId = 1, rotW = 1f };
            data.construction.graphNodes[1] = new ModuleGraphNodeDTO { prefabId = "B", moduleHashId = 2, rotW = 1f };
            data.construction.graphNodes[2] = new ModuleGraphNodeDTO { prefabId = "C", moduleHashId = 3, rotW = 1f };
            data.construction.graphEdgeCount = 4;
            data.construction.graphEdges[0] = new ModuleGraphEdgeDTO
            {
                sourceNodeIndex = 1,
                destinationNodeIndex = 0
            };
            data.construction.graphEdges[1] = new ModuleGraphEdgeDTO
            {
                sourceNodeIndex = 0,
                destinationNodeIndex = 1
            };
            data.construction.graphEdges[2] = new ModuleGraphEdgeDTO
            {
                sourceNodeIndex = 2,
                destinationNodeIndex = 1
            };
            data.construction.graphEdges[3] = new ModuleGraphEdgeDTO
            {
                sourceNodeIndex = 1,
                destinationNodeIndex = 2
            };

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(2, restoredData.construction.graphEdgeCount);
                Assert.AreEqual(0, restoredData.construction.graphEdges[0].sourceNodeIndex);
                Assert.AreEqual(1, restoredData.construction.graphEdges[0].destinationNodeIndex);
                Assert.AreEqual(1, restoredData.construction.graphEdges[1].sourceNodeIndex);
                Assert.AreEqual(2, restoredData.construction.graphEdges[1].destinationNodeIndex);
            }
        }

        [Test]
        public void ConstructionRuntime_ReadSanitizerDropsDuplicateGraphEdges()
        {
            ConstructionDTO construction = ConstructionDTO.CreatePreallocated();
            construction.moduleCount = 3;
            construction.graphNodeCount = 3;
            construction.graphNodes[0] = new ModuleGraphNodeDTO { prefabId = "A", moduleHashId = 1, rotW = 1f };
            construction.graphNodes[1] = new ModuleGraphNodeDTO { prefabId = "B", moduleHashId = 2, rotW = 1f };
            construction.graphNodes[2] = new ModuleGraphNodeDTO { prefabId = "C", moduleHashId = 3, rotW = 1f };
            construction.graphEdgeCount = 4;
            construction.graphEdges[0] = new ModuleGraphEdgeDTO
            {
                sourceNodeIndex = 1,
                destinationNodeIndex = 0
            };
            construction.graphEdges[1] = new ModuleGraphEdgeDTO
            {
                sourceNodeIndex = 0,
                destinationNodeIndex = 1
            };
            construction.graphEdges[2] = new ModuleGraphEdgeDTO
            {
                sourceNodeIndex = 1,
                destinationNodeIndex = 2
            };
            construction.graphEdges[3] = new ModuleGraphEdgeDTO
            {
                sourceNodeIndex = 2,
                destinationNodeIndex = 1
            };

            MethodInfo sanitizer = typeof(SaveBinaryPayloadCodec).GetMethod(
                "SanitizeConstructionGraphEdgesAfterRead",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(sanitizer);

            object[] args = { construction };
            sanitizer.Invoke(null, args);
            ConstructionDTO sanitized = (ConstructionDTO)args[0];

            Assert.AreEqual(3, sanitized.graphNodeCount);
            Assert.AreEqual(2, sanitized.graphEdgeCount);
            Assert.AreEqual(0, sanitized.graphEdges[0].sourceNodeIndex);
            Assert.AreEqual(1, sanitized.graphEdges[0].destinationNodeIndex);
            Assert.AreEqual(1, sanitized.graphEdges[1].sourceNodeIndex);
            Assert.AreEqual(2, sanitized.graphEdges[1].destinationNodeIndex);
        }

        [Test]
        public void ConstructionRuntime_ReadSanitizerClampsGraphAndBlitCountsToActiveModules()
        {
            ConstructionDTO construction = ConstructionDTO.CreatePreallocated();
            construction.moduleCount = 1;
            construction.graphNodeCount = 2;
            construction.graphNodes[0] = new ModuleGraphNodeDTO { prefabId = "A", moduleHashId = 1, rotW = 1f };
            construction.graphNodes[1] = new ModuleGraphNodeDTO { prefabId = "Stale", moduleHashId = 2, rotW = 1f };
            construction.graphEdgeCount = 1;
            construction.graphEdges[0] = new ModuleGraphEdgeDTO
            {
                sourceNodeIndex = 0,
                destinationNodeIndex = 1
            };
            construction.moduleBlitCount = 2;
            construction.moduleBlitRecords[0] = new ModuleBlitDTO { moduleHashId = 1, rotW = 1f };
            construction.moduleBlitRecords[1] = new ModuleBlitDTO { moduleHashId = 2, rotW = 1f };

            MethodInfo sanitizer = typeof(SaveBinaryPayloadCodec).GetMethod(
                "SanitizeConstructionGraphEdgesAfterRead",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(sanitizer);

            object[] args = { construction };
            sanitizer.Invoke(null, args);
            ConstructionDTO sanitized = (ConstructionDTO)args[0];

            Assert.AreEqual(1, sanitized.moduleCount);
            Assert.AreEqual(1, sanitized.graphNodeCount);
            Assert.AreEqual(0, sanitized.graphEdgeCount);
            Assert.AreEqual(1, sanitized.moduleBlitCount);
            Assert.AreEqual(1, sanitized.moduleBlitRecords[0].moduleHashId);
        }

        [Test]
        public void ConstructionRuntime_ReadEnsuresExactArrayCapacityAfterShortPayload()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.construction.EnsureCapacity();
            data.construction.moduleCount = 1;
            data.construction.modules[0] = new ModuleDTO
            {
                prefabId = "module.prefab",
                instanceId = "module.instance",
                rotW = 1f,
                integrity = 100f,
                repairIntegrityCap = 100f,
                health = 100
            };
            data.construction.graphNodeCount = 1;
            data.construction.graphNodes[0] = new ModuleGraphNodeDTO
            {
                prefabId = "module.prefab",
                instanceId = "module.instance",
                moduleHashId = 1,
                rotW = 1f
            };
            data.construction.graphEdgeCount = 0;
            data.construction.moduleBlitCount = 1;
            data.construction.moduleBlitRecords[0] = new ModuleBlitDTO
            {
                prefabHashId = 1,
                moduleHashId = 1,
                rotW = 1f,
                health = 100
            };

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(1, restoredData.construction.moduleCount);
                Assert.AreEqual(1, restoredData.construction.graphNodeCount);
                Assert.AreEqual(0, restoredData.construction.graphEdgeCount);
                Assert.AreEqual(1, restoredData.construction.moduleBlitCount);
                Assert.AreEqual(ConstructionDTO.MaxModules, restoredData.construction.modules.Length);
                Assert.AreEqual(ConstructionDTO.MaxModules, restoredData.construction.graphNodes.Length);
                Assert.AreEqual(ConstructionDTO.MaxGraphEdges, restoredData.construction.graphEdges.Length);
                Assert.AreEqual(ConstructionDTO.MaxModules, restoredData.construction.moduleBlitRecords.Length);
                Assert.AreEqual(ConstructionDTO.MaxModules, restoredData.construction.habitatFloodStates.Length);
            }
        }

        [Test]
        public void ConstructionRuntimeMigration_CurrentDropsDuplicateGraphEdges()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.construction.EnsureCapacity();
            data.construction.moduleCount = 3;
            data.construction.graphNodeCount = 3;
            data.construction.graphEdgeCount = 4;
            data.construction.graphEdges[0] = new ModuleGraphEdgeDTO
            {
                sourceNodeIndex = 1,
                destinationNodeIndex = 0
            };
            data.construction.graphEdges[1] = new ModuleGraphEdgeDTO
            {
                sourceNodeIndex = 0,
                destinationNodeIndex = 1
            };
            data.construction.graphEdges[2] = new ModuleGraphEdgeDTO
            {
                sourceNodeIndex = 2,
                destinationNodeIndex = 1
            };
            data.construction.graphEdges[3] = new ModuleGraphEdgeDTO
            {
                sourceNodeIndex = 1,
                destinationNodeIndex = 2
            };

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(2, data.construction.graphEdgeCount);
            Assert.AreEqual(0, data.construction.graphEdges[0].sourceNodeIndex);
            Assert.AreEqual(1, data.construction.graphEdges[0].destinationNodeIndex);
            Assert.AreEqual(1, data.construction.graphEdges[1].sourceNodeIndex);
            Assert.AreEqual(2, data.construction.graphEdges[1].destinationNodeIndex);
            StringAssert.Contains("construction graph edges repaired", summary);
        }

        [Test]
        public void ProceduralWorldRuntimeMigration_CurrentRepairsMalformedFaunaStates()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.proceduralWorldState.EnsureCapacity();
            data.proceduralWorldState.faunaStateCount = 1;
            data.proceduralWorldState.faunaStates[0] = new ProceduralFaunaStateDTO
            {
                runtimeKey = 11,
                cooldownUntilPlayTime = float.NaN,
                flags = 0xFF
            };
            data.proceduralWorldState.hibernatedFaunaCount = 1;
            data.proceduralWorldState.hibernatedFaunaStates[0] = new HibernatedFaunaStateDTO
            {
                speciesId = 123,
                biomeIndex = 2,
                creatureTypeIndex = 3,
                health = float.PositiveInfinity,
                position = new Hecton8.World.AbsoluteUniversePositionBlit128
                {
                    GridX = 7,
                    GridY = 8,
                    GridZ = 9,
                    Local = new Unity.Mathematics.float4(float.NaN, 4f, float.NegativeInfinity, 123f),
                    Reserved = 77UL
                },
                rotationX = 0f,
                rotationY = 0f,
                rotationZ = 0f,
                rotationW = 0f,
                linearVelocityX = float.NaN,
                linearVelocityY = 2f,
                linearVelocityZ = float.PositiveInfinity,
                angularVelocityX = float.NegativeInfinity,
                angularVelocityY = 3f,
                angularVelocityZ = float.NaN,
                uniqueInstanceUid = 44u,
                flags = 0xFF
            };

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            ProceduralFaunaStateDTO fauna = data.proceduralWorldState.faunaStates[0];
            Assert.AreEqual(11, fauna.runtimeKey);
            Assert.AreEqual(0f, fauna.cooldownUntilPlayTime);
            Assert.AreEqual((byte)(ProceduralFaunaStateDTO.FlagLargeThreatZone | ProceduralFaunaStateDTO.FlagBlocked), fauna.flags);

            HibernatedFaunaStateDTO hibernated = data.proceduralWorldState.hibernatedFaunaStates[0];
            Assert.AreEqual(123, hibernated.speciesId);
            Assert.AreEqual(2, hibernated.biomeIndex);
            Assert.AreEqual(3, hibernated.creatureTypeIndex);
            Assert.AreEqual(0f, hibernated.health);
            Assert.AreEqual(7, hibernated.position.GridX);
            Assert.AreEqual(8, hibernated.position.GridY);
            Assert.AreEqual(9, hibernated.position.GridZ);
            Assert.AreEqual(0f, hibernated.position.Local.x);
            Assert.AreEqual(4f, hibernated.position.Local.y);
            Assert.AreEqual(0f, hibernated.position.Local.z);
            Assert.AreEqual(0f, hibernated.position.Local.w);
            Assert.AreEqual(0UL, hibernated.position.Reserved);
            Assert.AreEqual(0f, hibernated.rotationX);
            Assert.AreEqual(0f, hibernated.rotationY);
            Assert.AreEqual(0f, hibernated.rotationZ);
            Assert.AreEqual(1f, hibernated.rotationW);
            Assert.AreEqual(0f, hibernated.linearVelocityX);
            Assert.AreEqual(2f, hibernated.linearVelocityY);
            Assert.AreEqual(0f, hibernated.linearVelocityZ);
            Assert.AreEqual(0f, hibernated.angularVelocityX);
            Assert.AreEqual(3f, hibernated.angularVelocityY);
            Assert.AreEqual(0f, hibernated.angularVelocityZ);
            Assert.AreEqual(44u, hibernated.uniqueInstanceUid);
            Assert.AreEqual(HibernatedFaunaStateDTO.FlagLargeThreat, hibernated.flags);
            StringAssert.Contains("procedural fauna states repaired", summary);
            StringAssert.Contains("hibernated fauna states repaired", summary);
        }

        [Test]
        public void ProceduralWorldRuntime_WriteSanitizesMalformedFaunaStates()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.proceduralWorldState.EnsureCapacity();
            data.proceduralWorldState.faunaStateCount = 1;
            data.proceduralWorldState.faunaStates[0] = new ProceduralFaunaStateDTO
            {
                runtimeKey = 22,
                cooldownUntilPlayTime = float.PositiveInfinity,
                flags = 0xFF
            };
            data.proceduralWorldState.hibernatedFaunaCount = 1;
            data.proceduralWorldState.hibernatedFaunaStates[0] = new HibernatedFaunaStateDTO
            {
                speciesId = 321,
                biomeIndex = 4,
                creatureTypeIndex = 5,
                health = float.NaN,
                position = new Hecton8.World.AbsoluteUniversePositionBlit128
                {
                    GridX = -7,
                    GridY = -8,
                    GridZ = -9,
                    Local = new Unity.Mathematics.float4(float.PositiveInfinity, 6f, float.NaN, 99f),
                    Reserved = 66UL
                },
                rotationX = float.NaN,
                rotationY = 0f,
                rotationZ = 0f,
                rotationW = 1f,
                linearVelocityX = float.NegativeInfinity,
                linearVelocityY = 8f,
                linearVelocityZ = float.NaN,
                angularVelocityX = float.PositiveInfinity,
                angularVelocityY = 9f,
                angularVelocityZ = float.NaN,
                uniqueInstanceUid = 55u,
                flags = 0xFE
            };

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                ProceduralFaunaStateDTO fauna = restoredData.proceduralWorldState.faunaStates[0];
                Assert.AreEqual(22, fauna.runtimeKey);
                Assert.AreEqual(0f, fauna.cooldownUntilPlayTime);
                Assert.AreEqual((byte)(ProceduralFaunaStateDTO.FlagLargeThreatZone | ProceduralFaunaStateDTO.FlagBlocked), fauna.flags);

                HibernatedFaunaStateDTO hibernated = restoredData.proceduralWorldState.hibernatedFaunaStates[0];
                Assert.AreEqual(321, hibernated.speciesId);
                Assert.AreEqual(4, hibernated.biomeIndex);
                Assert.AreEqual(5, hibernated.creatureTypeIndex);
                Assert.AreEqual(0f, hibernated.health);
                Assert.AreEqual(-7, hibernated.position.GridX);
                Assert.AreEqual(-8, hibernated.position.GridY);
                Assert.AreEqual(-9, hibernated.position.GridZ);
                Assert.AreEqual(0f, hibernated.position.Local.x);
                Assert.AreEqual(6f, hibernated.position.Local.y);
                Assert.AreEqual(0f, hibernated.position.Local.z);
                Assert.AreEqual(0f, hibernated.position.Local.w);
                Assert.AreEqual(0UL, hibernated.position.Reserved);
                Assert.AreEqual(0f, hibernated.rotationX);
                Assert.AreEqual(0f, hibernated.rotationY);
                Assert.AreEqual(0f, hibernated.rotationZ);
                Assert.AreEqual(1f, hibernated.rotationW);
                Assert.AreEqual(0f, hibernated.linearVelocityX);
                Assert.AreEqual(8f, hibernated.linearVelocityY);
                Assert.AreEqual(0f, hibernated.linearVelocityZ);
                Assert.AreEqual(0f, hibernated.angularVelocityX);
                Assert.AreEqual(9f, hibernated.angularVelocityY);
                Assert.AreEqual(0f, hibernated.angularVelocityZ);
                Assert.AreEqual(55u, hibernated.uniqueInstanceUid);
                Assert.AreEqual(0, hibernated.flags);
            }
        }

        [Test]
        public void ProceduralWorldRuntime_ReadClampsMalformedCounts()
        {
            const long suppressedKey = 0x0102030405060708L;
            const long faunaRuntimeKey = 0x1020304050607080L;

            SaveData data = SaveData.CreateNew(0.0);
            data.proceduralWorldState.EnsureCapacity();
            data.proceduralWorldState.suppressedPlacementCount = 1;
            data.proceduralWorldState.suppressedPlacementKeys[0] = suppressedKey;
            data.proceduralWorldState.faunaStateCount = 1;
            data.proceduralWorldState.faunaStates[0] = new ProceduralFaunaStateDTO
            {
                runtimeKey = faunaRuntimeKey,
                cooldownUntilPlayTime = 12.5f,
                flags = ProceduralFaunaStateDTO.FlagBlocked
            };

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            int bytesWritten;
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
            }

            byte[] marker = BuildProceduralWorldFaunaHeaderMarker(suppressedKey, faunaRuntimeKey, 12.5f);
            int proceduralWorldOffset = FindLittleEndianByteSequenceOffset(payload, bytesWritten, marker);
            Assert.GreaterOrEqual(proceduralWorldOffset, 0);

            int faunaCountOffset = sizeof(int) + (int)EncodedStructArrayBytes<long>(1);
            PatchPayloadInt(payload, proceduralWorldOffset, ProceduralWorldStateDTO.MaxSuppressedPlacements + 10);
            PatchPayloadInt(payload, proceduralWorldOffset + faunaCountOffset, ProceduralWorldStateDTO.MaxFaunaStates + 10);

            fixed (byte* payloadPtr = payload)
            {
                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(1, restoredData.proceduralWorldState.suppressedPlacementCount);
                Assert.AreEqual(1, restoredData.proceduralWorldState.faunaStateCount);
                Assert.AreEqual(
                    ProceduralWorldStateDTO.MaxSuppressedPlacements,
                    restoredData.proceduralWorldState.suppressedPlacementKeys.Length);
                Assert.AreEqual(
                    ProceduralWorldStateDTO.MaxFaunaStates,
                    restoredData.proceduralWorldState.faunaStates.Length);
                Assert.AreEqual(suppressedKey, restoredData.proceduralWorldState.suppressedPlacementKeys[0]);
                Assert.AreEqual(faunaRuntimeKey, restoredData.proceduralWorldState.faunaStates[0].runtimeKey);
            }
        }

        [Test]
        public void ConstructionRuntime_WriteClampsGraphAndBlitCountsToActiveModules()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.construction.EnsureCapacity();
            data.construction.moduleCount = 1;
            data.construction.graphNodeCount = 2;
            data.construction.graphNodes[0] = new ModuleGraphNodeDTO { prefabId = "A", moduleHashId = 1, rotW = 1f };
            data.construction.graphNodes[1] = new ModuleGraphNodeDTO { prefabId = "Stale", moduleHashId = 2, rotW = 1f };
            data.construction.graphEdgeCount = 1;
            data.construction.graphEdges[0] = new ModuleGraphEdgeDTO
            {
                sourceNodeIndex = 0,
                destinationNodeIndex = 1
            };
            data.construction.moduleBlitCount = 2;
            data.construction.moduleBlitRecords[0] = new ModuleBlitDTO { moduleHashId = 1, rotW = 1f };
            data.construction.moduleBlitRecords[1] = new ModuleBlitDTO { moduleHashId = 2, rotW = 1f };

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(1, restoredData.construction.moduleCount);
                Assert.AreEqual(1, restoredData.construction.graphNodeCount);
                Assert.AreEqual(0, restoredData.construction.graphEdgeCount);
                Assert.AreEqual(1, restoredData.construction.moduleBlitCount);
                Assert.AreEqual(1, restoredData.construction.moduleBlitRecords[0].moduleHashId);
            }
        }

        [Test]
        public void ConstructionRuntime_ReadClampsFirstHourFloodMirrorCountToActiveModules()
        {
            const string sentinelPrefabId = "FIRST_HOUR_FLOOD_CLAMP_SENTINEL";
            SaveData data = SaveData.CreateNew(0.0);
            data.construction.EnsureCapacity();
            data.construction.moduleCount = 1;
            data.construction.modules[0] = new ModuleDTO
            {
                prefabId = sentinelPrefabId,
                integrity = 15f,
                repairIntegrityCap = 20f,
                airReserveNormalized = 0.9f,
                co2Normalized = 0.1f,
                health = 177
            };
            data.construction.graphNodeCount = 1;
            data.construction.graphNodes[0] = new ModuleGraphNodeDTO
            {
                prefabId = sentinelPrefabId + ".node",
                moduleHashId = 12345,
                rotW = 1f
            };

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            int bytesWritten;
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
            }

            byte[] marker = BuildConstructionModuleHeaderMarker(sentinelPrefabId);
            int constructionModuleCountOffset = FindLittleEndianByteSequenceOffset(payload, bytesWritten, marker);
            Assert.GreaterOrEqual(constructionModuleCountOffset, 0);
            PatchLittleEndianIntAtOffset(payload, constructionModuleCountOffset, 0);

            fixed (byte* payloadPtr = payload)
            {
                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(0, restoredData.construction.moduleCount);
                Assert.AreEqual(0, restoredData.construction.graphNodeCount);
                Assert.AreEqual(0, restoredData.construction.habitatFloodStateCount);
            }
        }

        [Test]
        public void ConstructionRuntime_ReadRegeneratesMissingFirstHourFloodMirrorsFromModules()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.construction.EnsureCapacity();
            data.construction.moduleCount = 1;
            data.construction.modules[0] = new ModuleDTO
            {
                integrity = 17f,
                repairIntegrityCap = 23f,
                airReserveNormalized = 0.85f,
                co2Normalized = 0.15f,
                floodedReefFloodSeconds = 3f,
                isFlooded = true,
                failureMode = 2,
                health = 155
            };
            data.construction.graphNodeCount = 1;
            data.construction.graphNodes[0] = new ModuleGraphNodeDTO
            {
                moduleHashId = 6789,
                rotW = 1f
            };

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            int bytesWritten;
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
            }

            byte[] marker = BuildFirstHourFloodStateMarker(
                6789,
                17f,
                23f,
                0.85f,
                0.15f,
                3f,
                HabitatFloodStateDTO.FlagFlooded,
                2,
                155);
            int floodCountOffset = FindLittleEndianByteSequenceOffset(payload, bytesWritten, marker);
            Assert.GreaterOrEqual(floodCountOffset, 0);
            PatchLittleEndianIntAtOffset(payload, floodCountOffset, 0);
            int shortenedBytesWritten = bytesWritten - 32;
            Assert.Greater(shortenedBytesWritten, 0);

            fixed (byte* payloadPtr = payload)
            {
                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    shortenedBytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(shortenedBytesWritten, bytesRead);
                Assert.AreEqual(1, restoredData.construction.moduleCount);
                Assert.AreEqual(1, restoredData.construction.habitatFloodStateCount);
                HabitatFloodStateDTO restored = restoredData.construction.habitatFloodStates[0];
                Assert.AreEqual(6789, restored.moduleHashId);
                Assert.AreEqual(17f, restored.integrity);
                Assert.AreEqual(23f, restored.repairIntegrityCap);
                Assert.AreEqual(0.85f, restored.airReserveNormalized);
                Assert.AreEqual(0.15f, restored.co2Normalized);
                Assert.AreEqual(3f, restored.floodedReefFloodSeconds);
                Assert.AreEqual(HabitatFloodStateDTO.FlagFlooded, restored.flags);
                Assert.AreEqual(2, restored.failureMode);
                Assert.AreEqual(155, restored.health);
            }
        }

        [Test]
        public void ConstructionRuntime_ReadRebuildsCorruptFirstHourFloodMirrorFromModules()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.construction.EnsureCapacity();
            data.construction.moduleCount = 1;
            data.construction.modules[0] = new ModuleDTO
            {
                integrity = 19f,
                repairIntegrityCap = 29f,
                airReserveNormalized = 0.61f,
                co2Normalized = 0.24f,
                floodedReefFloodSeconds = 4.5f,
                isFlooded = true,
                interiorReefInfestationActive = true,
                failureMode = 1,
                health = 199
            };
            data.construction.graphNodeCount = 1;
            data.construction.graphNodes[0] = new ModuleGraphNodeDTO
            {
                moduleHashId = 2468,
                rotW = 1f
            };

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            int bytesWritten;
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
            }

            byte[] expectedMarker = BuildFirstHourFloodStateMarker(
                2468,
                19f,
                29f,
                0.61f,
                0.24f,
                4.5f,
                HabitatFloodStateDTO.FlagFlooded | HabitatFloodStateDTO.FlagInfested,
                1,
                199);
            int floodCountOffset = FindLittleEndianByteSequenceOffset(payload, bytesWritten, expectedMarker);
            Assert.GreaterOrEqual(floodCountOffset, 0);

            byte[] corruptMarker = BuildFirstHourFloodStateMarker(
                1357,
                99f,
                88f,
                0.02f,
                0.97f,
                123f,
                HabitatFloodStateDTO.FlagInfested,
                3,
                22);
            Buffer.BlockCopy(corruptMarker, 0, payload, floodCountOffset, corruptMarker.Length);

            fixed (byte* payloadPtr = payload)
            {
                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(1, restoredData.construction.moduleCount);
                Assert.AreEqual(1, restoredData.construction.habitatFloodStateCount);
                HabitatFloodStateDTO restored = restoredData.construction.habitatFloodStates[0];
                Assert.AreEqual(2468, restored.moduleHashId);
                Assert.AreEqual(19f, restored.integrity);
                Assert.AreEqual(29f, restored.repairIntegrityCap);
                Assert.AreEqual(0.61f, restored.airReserveNormalized);
                Assert.AreEqual(0.24f, restored.co2Normalized);
                Assert.AreEqual(4.5f, restored.floodedReefFloodSeconds);
                Assert.AreEqual(HabitatFloodStateDTO.FlagFlooded | HabitatFloodStateDTO.FlagInfested, restored.flags);
                Assert.AreEqual(1, restored.failureMode);
                Assert.AreEqual(199, restored.health);
            }
        }

        [Test]
        public void ConstructionRuntimeMigration_CurrentClampsGraphAndBlitCountsToActiveModules()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.construction.EnsureCapacity();
            data.construction.moduleCount = 1;
            data.construction.graphNodeCount = 2;
            data.construction.graphNodes[0] = new ModuleGraphNodeDTO { prefabId = "A", moduleHashId = 1, rotW = 1f };
            data.construction.graphNodes[1] = new ModuleGraphNodeDTO { prefabId = "Stale", moduleHashId = 2, rotW = 1f };
            data.construction.graphEdgeCount = 1;
            data.construction.graphEdges[0] = new ModuleGraphEdgeDTO
            {
                sourceNodeIndex = 0,
                destinationNodeIndex = 1
            };
            data.construction.moduleBlitCount = 2;
            data.construction.moduleBlitRecords[0] = new ModuleBlitDTO { moduleHashId = 1, rotW = 1f };
            data.construction.moduleBlitRecords[1] = new ModuleBlitDTO { moduleHashId = 2, rotW = 1f };

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(1, data.construction.moduleCount);
            Assert.AreEqual(1, data.construction.graphNodeCount);
            Assert.AreEqual(0, data.construction.graphEdgeCount);
            Assert.AreEqual(1, data.construction.moduleBlitCount);
            StringAssert.Contains("construction graph node count clamped", summary);
            StringAssert.Contains("construction blit count clamped", summary);
            StringAssert.Contains("construction graph edges repaired", summary);
        }

        [Test]
        public void ProceduralWorldRuntimeMigration_CurrentRepairsMalformedGeologyStates()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.proceduralWorldState.EnsureCapacity();
            data.proceduralWorldState.geologySeamStateCount = 1;
            data.proceduralWorldState.geologySeamStates[0] = new ProceduralGeologySeamStateDTO
            {
                runtimeKey = 77,
                chunkX = 1,
                chunkZ = 2,
                absoluteTerrainHeight = float.NaN,
                absoluteSeamHeight = 12f,
                seamBlendRadius = float.NegativeInfinity,
                terrainBlendWeight = 2f,
                caveBlendWeight = float.NaN,
                absolutePositionX = float.PositiveInfinity,
                absolutePositionY = 3f,
                absolutePositionZ = float.NaN,
                absoluteVoxelCenterX = 4f,
                absoluteVoxelCenterY = float.PositiveInfinity,
                absoluteVoxelCenterZ = -5f
            };
            data.proceduralWorldState.geologyCaveEntranceCount = 1;
            data.proceduralWorldState.geologyCaveEntrances[0] = new ProceduralGeologyCaveEntranceDTO
            {
                runtimeKey = 88,
                surfacePositionX = float.NaN,
                surfacePositionY = 5f,
                surfacePositionZ = float.PositiveInfinity,
                inwardDirectionX = 0f,
                inwardDirectionY = 0f,
                inwardDirectionZ = 0f,
                radius = float.NaN,
                funnelLength = -5f,
                innerRadius = float.PositiveInfinity
            };

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            ProceduralGeologySeamStateDTO seam = data.proceduralWorldState.geologySeamStates[0];
            Assert.AreEqual(77, seam.runtimeKey);
            Assert.AreEqual(1, seam.chunkX);
            Assert.AreEqual(2, seam.chunkZ);
            Assert.AreEqual(0f, seam.absoluteTerrainHeight);
            Assert.AreEqual(12f, seam.absoluteSeamHeight);
            Assert.AreEqual(0f, seam.seamBlendRadius);
            Assert.AreEqual(1f, seam.terrainBlendWeight);
            Assert.AreEqual(0f, seam.caveBlendWeight);
            Assert.AreEqual(0f, seam.absolutePositionX);
            Assert.AreEqual(3f, seam.absolutePositionY);
            Assert.AreEqual(0f, seam.absolutePositionZ);
            Assert.AreEqual(4f, seam.absoluteVoxelCenterX);
            Assert.AreEqual(0f, seam.absoluteVoxelCenterY);
            Assert.AreEqual(-5f, seam.absoluteVoxelCenterZ);

            ProceduralGeologyCaveEntranceDTO entrance = data.proceduralWorldState.geologyCaveEntrances[0];
            Assert.AreEqual(88, entrance.runtimeKey);
            Assert.AreEqual(0f, entrance.surfacePositionX);
            Assert.AreEqual(5f, entrance.surfacePositionY);
            Assert.AreEqual(0f, entrance.surfacePositionZ);
            Assert.AreEqual(0f, entrance.inwardDirectionX);
            Assert.AreEqual(0f, entrance.inwardDirectionY);
            Assert.AreEqual(1f, entrance.inwardDirectionZ);
            Assert.AreEqual(0f, entrance.radius);
            Assert.AreEqual(0f, entrance.funnelLength);
            Assert.AreEqual(0f, entrance.innerRadius);
            StringAssert.Contains("procedural geology seam states repaired", summary);
            StringAssert.Contains("procedural geology cave entrances repaired", summary);
        }

        [Test]
        public void ProceduralWorldRuntime_WriteSanitizesMalformedGeologyStates()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.proceduralWorldState.EnsureCapacity();
            data.proceduralWorldState.geologySeamStateCount = 1;
            data.proceduralWorldState.geologySeamStates[0] = new ProceduralGeologySeamStateDTO
            {
                runtimeKey = 177,
                chunkX = -3,
                chunkZ = 4,
                absoluteTerrainHeight = float.PositiveInfinity,
                absoluteSeamHeight = float.NaN,
                seamBlendRadius = -9f,
                terrainBlendWeight = -1f,
                caveBlendWeight = 3f,
                absolutePositionX = 10f,
                absolutePositionY = float.NegativeInfinity,
                absolutePositionZ = 12f,
                absoluteVoxelCenterX = float.NaN,
                absoluteVoxelCenterY = 14f,
                absoluteVoxelCenterZ = float.PositiveInfinity
            };
            data.proceduralWorldState.geologyCaveEntranceCount = 1;
            data.proceduralWorldState.geologyCaveEntrances[0] = new ProceduralGeologyCaveEntranceDTO
            {
                runtimeKey = 188,
                surfacePositionX = 1f,
                surfacePositionY = float.NaN,
                surfacePositionZ = 3f,
                inwardDirectionX = 3f,
                inwardDirectionY = 0f,
                inwardDirectionZ = 4f,
                radius = float.PositiveInfinity,
                funnelLength = float.NaN,
                innerRadius = -1f
            };

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                ProceduralGeologySeamStateDTO seam = restoredData.proceduralWorldState.geologySeamStates[0];
                Assert.AreEqual(177, seam.runtimeKey);
                Assert.AreEqual(-3, seam.chunkX);
                Assert.AreEqual(4, seam.chunkZ);
                Assert.AreEqual(0f, seam.absoluteTerrainHeight);
                Assert.AreEqual(0f, seam.absoluteSeamHeight);
                Assert.AreEqual(0f, seam.seamBlendRadius);
                Assert.AreEqual(0f, seam.terrainBlendWeight);
                Assert.AreEqual(1f, seam.caveBlendWeight);
                Assert.AreEqual(10f, seam.absolutePositionX);
                Assert.AreEqual(0f, seam.absolutePositionY);
                Assert.AreEqual(12f, seam.absolutePositionZ);
                Assert.AreEqual(0f, seam.absoluteVoxelCenterX);
                Assert.AreEqual(14f, seam.absoluteVoxelCenterY);
                Assert.AreEqual(0f, seam.absoluteVoxelCenterZ);

                ProceduralGeologyCaveEntranceDTO entrance = restoredData.proceduralWorldState.geologyCaveEntrances[0];
                Assert.AreEqual(188, entrance.runtimeKey);
                Assert.AreEqual(1f, entrance.surfacePositionX);
                Assert.AreEqual(0f, entrance.surfacePositionY);
                Assert.AreEqual(3f, entrance.surfacePositionZ);
                Assert.AreEqual(0.6f, entrance.inwardDirectionX, 0.0001f);
                Assert.AreEqual(0f, entrance.inwardDirectionY);
                Assert.AreEqual(0.8f, entrance.inwardDirectionZ, 0.0001f);
                Assert.AreEqual(0f, entrance.radius);
                Assert.AreEqual(0f, entrance.funnelLength);
                Assert.AreEqual(0f, entrance.innerRadius);
            }
        }

        [Test]
        public void DataArchaeologyRuntimeMmfPersistenceWritesThroughCreateNewTemp()
        {
            string source = File.ReadAllText(Path.Combine("Assets", "_Project", "Scripts", "Gameplay", "DataArchaeologyRuntime.cs"));

            StringAssert.Contains("TryDeleteMmfTempFileNoThrow(tempPath);", source);
            StringAssert.Contains("new FileStream(tempPath, FileMode.CreateNew", source);
            StringAssert.Contains("stream.Flush(true);", source);
            StringAssert.Contains("File.Replace(tempPath, path, null, true);", source);
            StringAssert.DoesNotContain("new FileStream(tempPath, FileMode.Create, FileAccess.Write", source);
            Assert.Less(
                source.IndexOf("new FileStream(tempPath, FileMode.CreateNew", StringComparison.Ordinal),
                source.IndexOf("PromoteMmfTempFileCold(tempPath, path);", StringComparison.Ordinal));
        }

        [Test]
        public void DataArchaeologyRuntimeMigration_CurrentRepairsMalformedPartialProgress()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.dataArchaeologyPartialScanCount = 1;
            data.dataArchaeologyPartialScanHashes[0] = 0x1234ABCDu;
            data.dataArchaeologyPartialScanProgressPermille[0] = 50000;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(1, data.dataArchaeologyPartialScanCount);
            Assert.AreEqual(0x1234ABCDu, data.dataArchaeologyPartialScanHashes[0]);
            Assert.AreEqual(999, data.dataArchaeologyPartialScanProgressPermille[0]);
            StringAssert.Contains("data archaeology partial progress repaired", summary);
        }

        [Test]
        public void DataArchaeologyRuntime_WriteSanitizesMalformedPartialProgress()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.dataArchaeologyPartialScanCount = 1;
            data.dataArchaeologyPartialScanHashes[0] = 0xCAFEBABEu;
            data.dataArchaeologyPartialScanProgressPermille[0] = 50000;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(1, restoredData.dataArchaeologyPartialScanCount);
                Assert.AreEqual(0xCAFEBABEu, restoredData.dataArchaeologyPartialScanHashes[0]);
                Assert.AreEqual(999, restoredData.dataArchaeologyPartialScanProgressPermille[0]);
            }
        }

        [Test]
        public void DataArchaeologyRuntime_ReadSanitizesMalformedPartialProgress()
        {
            const uint partialHash = 0xDEADBEEFu;
            const ushort validProgress = 123;

            SaveData data = SaveData.CreateNew(0.0);
            data.dataArchaeologyPartialScanCount = 1;
            data.dataArchaeologyPartialScanHashes[0] = partialHash;
            data.dataArchaeologyPartialScanProgressPermille[0] = validProgress;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            int bytesWritten;
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
            }

            byte[] marker = BuildDataArchaeologyPartialScanMarker(partialHash, validProgress);
            int markerOffset = FindLittleEndianByteSequenceOffset(payload, bytesWritten, marker);
            Assert.GreaterOrEqual(markerOffset, 0);

            int progressOffset = markerOffset + marker.Length - sizeof(ushort);
            WritePayloadUShort(payload, ref progressOffset, 50000);

            fixed (byte* payloadPtr = payload)
            {
                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(1, restoredData.dataArchaeologyPartialScanCount);
                Assert.AreEqual(partialHash, restoredData.dataArchaeologyPartialScanHashes[0]);
                Assert.AreEqual(999, restoredData.dataArchaeologyPartialScanProgressPermille[0]);
            }
        }

        [Test]
        public void DataArchaeologyRuntimeMigration_CurrentRepairsMalformedScanStateValue()
        {
            const int scanKey = 0x5EADBEEF;

            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.dataArchaeologyScanStateCount = 1;
            data.dataArchaeologyScanStateKeys[0] = scanKey;
            data.dataArchaeologyScanStateValues[0] = 250;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(1, data.dataArchaeologyScanStateCount);
            Assert.AreEqual(scanKey, data.dataArchaeologyScanStateKeys[0]);
            Assert.AreEqual(0, data.dataArchaeologyScanStateValues[0]);
            StringAssert.Contains("data archaeology scan-state values repaired", summary);
        }

        [Test]
        public void DataArchaeologyRuntime_WriteSanitizesMalformedScanStateValue()
        {
            const int scanKey = 0x5EADBEE1;

            SaveData data = SaveData.CreateNew(0.0);
            data.dataArchaeologyScanStateCount = 1;
            data.dataArchaeologyScanStateKeys[0] = scanKey;
            data.dataArchaeologyScanStateValues[0] = 250;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(1, restoredData.dataArchaeologyScanStateCount);
                Assert.AreEqual(scanKey, restoredData.dataArchaeologyScanStateKeys[0]);
                Assert.AreEqual(0, restoredData.dataArchaeologyScanStateValues[0]);
            }
        }

        [Test]
        public void DataArchaeologyRuntime_ReadSanitizesMalformedScanStateValue()
        {
            const int scanKey = 0x5EADBEE2;
            const byte validState = 2;

            SaveData data = SaveData.CreateNew(0.0);
            data.dataArchaeologyScanStateCount = 1;
            data.dataArchaeologyScanStateKeys[0] = scanKey;
            data.dataArchaeologyScanStateValues[0] = validState;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            int bytesWritten;
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
            }

            byte[] marker = BuildDataArchaeologyScanStateMarker(scanKey, validState);
            int markerOffset = FindLittleEndianByteSequenceOffset(payload, bytesWritten, marker);
            Assert.GreaterOrEqual(markerOffset, 0);

            int stateOffset = markerOffset + marker.Length - sizeof(byte);
            WritePayloadByte(payload, ref stateOffset, 250);

            fixed (byte* payloadPtr = payload)
            {
                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(1, restoredData.dataArchaeologyScanStateCount);
                Assert.AreEqual(scanKey, restoredData.dataArchaeologyScanStateKeys[0]);
                Assert.AreEqual(0, restoredData.dataArchaeologyScanStateValues[0]);
            }
        }

        [Test]
        public void ProceduralLoreRuntimeMigration_CurrentRepairsMalformedPlacementPosition()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.proceduralLore.EnsureCapacity();
            data.proceduralLore.activeCount = 1;
            data.proceduralLore.activePlacements[0] = new ProceduralLorePlacementDTO
            {
                discoveryId = null,
                logId = null,
                chunkKey = 42L,
                posX = float.NaN,
                posY = 2f,
                posZ = float.PositiveInfinity
            };

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            ProceduralLorePlacementDTO placement = data.proceduralLore.activePlacements[0];
            Assert.AreEqual(string.Empty, placement.discoveryId);
            Assert.AreEqual(string.Empty, placement.logId);
            Assert.AreEqual(42L, placement.chunkKey);
            Assert.AreEqual(0f, placement.posX);
            Assert.AreEqual(2f, placement.posY);
            Assert.AreEqual(0f, placement.posZ);
            StringAssert.Contains("procedural lore placements repaired", summary);
        }

        [Test]
        public void ProceduralLoreRuntime_WriteSanitizesMalformedPlacementPosition()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.proceduralLore.EnsureCapacity();
            data.proceduralLore.activeCount = 1;
            data.proceduralLore.activePlacements[0] = new ProceduralLorePlacementDTO
            {
                discoveryId = null,
                logId = null,
                chunkKey = 84L,
                posX = float.PositiveInfinity,
                posY = 5f,
                posZ = float.NaN
            };

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
                Assert.AreEqual(0, CountLittleEndianFloat(payload, bytesWritten, float.NaN));
                Assert.AreEqual(0, CountLittleEndianFloat(payload, bytesWritten, float.PositiveInfinity));

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(1, restoredData.proceduralLore.activeCount);
                ProceduralLorePlacementDTO placement = restoredData.proceduralLore.activePlacements[0];
                Assert.AreEqual(string.Empty, placement.discoveryId);
                Assert.AreEqual(string.Empty, placement.logId);
                Assert.AreEqual(84L, placement.chunkKey);
                Assert.AreEqual(0f, placement.posX);
                Assert.AreEqual(5f, placement.posY);
                Assert.AreEqual(0f, placement.posZ);
            }
        }

        [Test]
        public void ProceduralLoreRuntimeMigration_CurrentRepairsMalformedSourceIndex()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.proceduralLore.EnsureCapacity();
            data.proceduralLore.nextSourceIndex = -3;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(0, data.proceduralLore.nextSourceIndex);
            StringAssert.Contains("procedural lore source index repaired", summary);
        }

        [Test]
        public void ProceduralLoreRuntime_WriteSanitizesMalformedSourceIndex()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.proceduralLore.EnsureCapacity();
            data.proceduralLore.nextSourceIndex = -5;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(0, restoredData.proceduralLore.nextSourceIndex);
            }
        }

        [Test]
        public void AchievementRuntimeMigration_CurrentRepairsMalformedScalars()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.achievements.EnsureCapacity();
            data.achievements.swamDistanceMeters = float.PositiveInfinity;
            data.achievements.craftedItemCount = -2;
            data.achievements.discoveredBiomeCount = -3;
            data.achievements.unlockedCount = 1;
            data.achievements.unlockedIds[0] = null;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(0f, data.achievements.swamDistanceMeters);
            Assert.AreEqual(0, data.achievements.craftedItemCount);
            Assert.AreEqual(0, data.achievements.discoveredBiomeCount);
            Assert.AreEqual(0, data.achievements.unlockedCount);
            Assert.AreEqual(string.Empty, data.achievements.unlockedIds[0]);
            StringAssert.Contains("achievement swim distance repaired", summary);
            StringAssert.Contains("achievement crafted count repaired", summary);
            StringAssert.Contains("achievement biome count repaired", summary);
            StringAssert.Contains("achievement unlocked ids repaired", summary);
        }

        [Test]
        public void AchievementRuntime_WriteSanitizesMalformedScalars()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.achievements.EnsureCapacity();
            data.achievements.swamDistanceMeters = float.NaN;
            data.achievements.craftedItemCount = -5;
            data.achievements.discoveredBiomeCount = 6;
            data.achievements.unlockedCount = 1;
            data.achievements.unlockedIds[0] = null;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
                Assert.AreEqual(0, CountLittleEndianFloat(payload, bytesWritten, float.NaN));

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(0f, restoredData.achievements.swamDistanceMeters);
                Assert.AreEqual(0, restoredData.achievements.craftedItemCount);
                Assert.AreEqual(6, restoredData.achievements.discoveredBiomeCount);
                Assert.AreEqual(0, restoredData.achievements.unlockedCount);
            }
        }

        [Test]
        public void RunModifiersRuntimeMigration_CurrentRepairsInconsistentFlags()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.runModifiers = new RunModifiersDTO
            {
                isPermadeath = false,
                isDailySeed = false,
                runMarkedDead = true,
                dailySeedId = "daily.stale"
            };

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.IsFalse(data.runModifiers.runMarkedDead);
            Assert.AreEqual(string.Empty, data.runModifiers.dailySeedId);
            StringAssert.Contains("run modifiers dead-run flag repaired", summary);
            StringAssert.Contains("run modifiers daily-seed id cleared", summary);
        }

        [Test]
        public void RunModifiersRuntimeMigration_CurrentRepairsBlankDailySeedId()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.runModifiers = new RunModifiersDTO
            {
                isDailySeed = true,
                dailySeedId = " "
            };

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(string.Empty, data.runModifiers.dailySeedId);
            StringAssert.Contains("run modifiers daily-seed id repaired", summary);
        }

        [Test]
        public void RunModifiersRuntime_WriteSanitizesInconsistentFlags()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.runModifiers = new RunModifiersDTO
            {
                isPermadeath = false,
                isDailySeed = false,
                runMarkedDead = true,
                dailySeedId = "daily.write"
            };

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.IsFalse(restoredData.runModifiers.runMarkedDead);
                Assert.AreEqual(string.Empty, restoredData.runModifiers.dailySeedId);
            }
        }

        [Test]
        public void RunModifiersRuntime_WriteSanitizesBlankDailySeedId()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.runModifiers = new RunModifiersDTO
            {
                isDailySeed = true,
                dailySeedId = " "
            };

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(string.Empty, restoredData.runModifiers.dailySeedId);
            }
        }

        [Test]
        public void WorldStateRuntime_ReadClampsMalformedCounts()
        {
            const string depletedNodeId = "node.world.count";
            const long pickupChunkKey = 0x1020304050607080L;
            const long pickupWord = 0x0102030405060708L;

            SaveData data = SaveData.CreateNew(0.0);
            data.worldState.EnsureCapacity();
            data.worldState.depletedCount = 1;
            data.worldState.depletedNodeIds[0] = depletedNodeId;
            data.worldState.depletedPickupChunkCount = 1;
            data.worldState.depletedPickupChunkKeys[0] = pickupChunkKey;
            data.worldState.depletedPickupChunkWordStarts[0] = 0;
            data.worldState.depletedPickupChunkWordCounts[0] = 1;
            data.worldState.depletedPickupWordCount = 1;
            data.worldState.depletedPickupWords[0] = pickupWord;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            int bytesWritten;
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
            }

            byte[] marker = BuildWorldStateMarker(depletedNodeId, pickupChunkKey, pickupWord);
            int worldOffset = FindLittleEndianByteSequenceOffset(payload, bytesWritten, marker);
            Assert.GreaterOrEqual(worldOffset, 0);

            int pickupChunkCountOffset = sizeof(int) + EncodedStringArraySingleEntryBytes(depletedNodeId);
            int pickupWordCountOffset = pickupChunkCountOffset
                + sizeof(int)
                + (int)EncodedStructArrayBytes<long>(1)
                + (int)EncodedStructArrayBytes<int>(1)
                + (int)EncodedStructArrayBytes<int>(1);
            PatchPayloadInt(payload, worldOffset, WorldStateDTO.MaxNodes + 10);
            PatchPayloadInt(payload, worldOffset + pickupChunkCountOffset, WorldStateDTO.MaxPickupChunks + 10);
            PatchPayloadInt(payload, worldOffset + pickupWordCountOffset, WorldStateDTO.MaxPickupWords + 10);

            fixed (byte* payloadPtr = payload)
            {
                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(1, restoredData.worldState.depletedCount);
                Assert.AreEqual(1, restoredData.worldState.depletedPickupChunkCount);
                Assert.AreEqual(1, restoredData.worldState.depletedPickupWordCount);
                Assert.AreEqual(WorldStateDTO.MaxNodes, restoredData.worldState.depletedNodeIds.Length);
                Assert.AreEqual(WorldStateDTO.MaxPickupChunks, restoredData.worldState.depletedPickupChunkKeys.Length);
                Assert.AreEqual(WorldStateDTO.MaxPickupChunks, restoredData.worldState.depletedPickupChunkWordStarts.Length);
                Assert.AreEqual(WorldStateDTO.MaxPickupChunks, restoredData.worldState.depletedPickupChunkWordCounts.Length);
                Assert.AreEqual(WorldStateDTO.MaxPickupWords, restoredData.worldState.depletedPickupWords.Length);
                Assert.AreEqual(depletedNodeId, restoredData.worldState.depletedNodeIds[0]);
                Assert.AreEqual(pickupChunkKey, restoredData.worldState.depletedPickupChunkKeys[0]);
                Assert.AreEqual(pickupWord, restoredData.worldState.depletedPickupWords[0]);
            }
        }

        [Test]
        public void WorldStateRuntime_WriteCanonicalizesNullDepletedNodeIds()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.worldState.EnsureCapacity();
            data.worldState.depletedCount = 1;
            data.worldState.depletedNodeIds[0] = null;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(0, restoredData.worldState.depletedCount);
            }
        }

        [Test]
        public void WorldStateRuntimeMigration_CurrentRepairsNullDepletedNodeIds()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.worldState.EnsureCapacity();
            data.worldState.depletedCount = 1;
            data.worldState.depletedNodeIds[0] = null;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(0, data.worldState.depletedCount);
            Assert.AreEqual(string.Empty, data.worldState.depletedNodeIds[0]);
            StringAssert.Contains("world state depleted ids repaired", summary);
        }

        [Test]
        public void BarterRuntimeMigration_CurrentRepairsMalformedOfferState()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.barter.EnsureCapacity();
            data.barter.stateCount = 1;
            data.barter.offerStates[0] = new BarterOfferStateDTO
            {
                offerId = "offer.sample",
                executionCount = -4
            };

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(1, data.barter.stateCount);
            Assert.AreEqual("offer.sample", data.barter.offerStates[0].offerId);
            Assert.AreEqual(0, data.barter.offerStates[0].executionCount);
            StringAssert.Contains("barter offer states repaired", summary);
        }

        [Test]
        public void BarterRuntime_WriteSanitizesMalformedOfferState()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.barter.EnsureCapacity();
            data.barter.stateCount = 1;
            data.barter.offerStates[0] = new BarterOfferStateDTO
            {
                offerId = "offer.write",
                executionCount = -7
            };

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(1, restoredData.barter.stateCount);
                Assert.AreEqual("offer.write", restoredData.barter.offerStates[0].offerId);
                Assert.AreEqual(0, restoredData.barter.offerStates[0].executionCount);
            }
        }

        [Test]
        public void BarterRuntime_ReadClampsMalformedOuterCounts()
        {
            const string offerId = "offer.read";

            SaveData data = SaveData.CreateNew(0.0);
            data.barter.EnsureCapacity();
            data.barter.stateCount = 1;
            data.barter.offerStates[0] = new BarterOfferStateDTO
            {
                offerId = offerId,
                executionCount = 2
            };

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            int bytesWritten;
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
            }

            byte[] marker = BuildBarterSingleOfferMarker(offerId, 2);
            int barterOffset = FindLittleEndianByteSequenceOffset(payload, bytesWritten, marker);
            Assert.GreaterOrEqual(barterOffset, 0);

            int recentTransactionCountOffset = sizeof(int) + EncodedBarterOfferStateArraySingleEntryBytes(offerId);
            PatchPayloadInt(payload, barterOffset, BarterDTO.MaxOffers + 10);
            PatchPayloadInt(payload, barterOffset + recentTransactionCountOffset, BarterDTO.MaxRecentTransactions + 10);

            fixed (byte* payloadPtr = payload)
            {
                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(1, restoredData.barter.stateCount);
                Assert.AreEqual(0, restoredData.barter.recentTransactionCount);
                Assert.AreEqual(BarterDTO.MaxOffers, restoredData.barter.offerStates.Length);
                Assert.AreEqual(BarterDTO.MaxRecentTransactions, restoredData.barter.recentTransactions.Length);
                Assert.AreEqual(offerId, restoredData.barter.offerStates[0].offerId);
                Assert.AreEqual(2, restoredData.barter.offerStates[0].executionCount);
            }
        }

        [Test]
        public void ExplorationMapRuntime_ReadCanonicalizesMalformedCurrentHeader()
        {
            const int malformedByteCount = 7;
            int validByteCount = SaveBinaryStorage.AlignExplorationMortonByteCount(1);
            int expectedAlignedByteCount = SaveBinaryStorage.AlignExplorationMortonByteCount(malformedByteCount);

            SaveData data = SaveData.CreateNew(0.0);
            data.explorationMap.EnsureCapacity();
            data.explorationMap.exploredChunkCount = 9;
            data.explorationMap.exploredMortonByteCount = validByteCount;
            data.explorationMap.exploredMortonMaskBytes[0] = 0xC3;
            data.explorationMap.discoveredSectorByteCount = validByteCount;
            data.explorationMap.discoveredSectorMaskBytes[0] = 0x3C;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            int bytesWritten;
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
            }

            byte[] marker = BuildCurrentExplorationMapHeaderMarker(9, validByteCount);
            int explorationOffset = FindLittleEndianByteSequenceOffset(payload, bytesWritten, marker);
            Assert.GreaterOrEqual(explorationOffset, 0);

            PatchPayloadInt(payload, explorationOffset, ExplorationMapDTO.MaxExploredChunks + 99);
            PatchPayloadInt(payload, explorationOffset + sizeof(int), -4);
            PatchPayloadInt(payload, explorationOffset + sizeof(int) * 2, -5);
            PatchPayloadInt(payload, explorationOffset + sizeof(int) * 3, -6);
            PatchPayloadInt(payload, explorationOffset + sizeof(int) * 4, 0);
            PatchPayloadInt(payload, explorationOffset + sizeof(int) * 5, malformedByteCount);

            int cartographyOffset = explorationOffset + (sizeof(int) * 7) + validByteCount;
            PatchPayloadInt(payload, cartographyOffset, -7);
            PatchPayloadInt(payload, cartographyOffset + sizeof(int), -8);
            PatchPayloadInt(payload, cartographyOffset + sizeof(int) * 2, -9);
            PatchPayloadInt(payload, cartographyOffset + sizeof(int) * 3, malformedByteCount);

            fixed (byte* payloadPtr = payload)
            {
                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(ExplorationMapDTO.MaxExploredChunks, restoredData.explorationMap.exploredChunkCount);
                Assert.AreEqual(ExplorationMapDTO.DenseChunkSizeMeters, restoredData.explorationMap.chunkSizeMeters);
                Assert.AreEqual(ExplorationMapDTO.MortonMaskAxisBits, restoredData.explorationMap.mortonMaskAxisBits);
                Assert.AreEqual(ExplorationMapDTO.MortonMaskOriginOffset, restoredData.explorationMap.mortonMaskOriginOffset);
                Assert.AreEqual(SaveBinaryStorage.ExplorationMortonBuildSalt32, restoredData.explorationMap.mortonBuildSalt);
                Assert.AreEqual(expectedAlignedByteCount, restoredData.explorationMap.exploredMortonByteCount);
                Assert.AreEqual(ExplorationMapDTO.CartographyCellSizeMeters, restoredData.explorationMap.cartographyCellSizeMeters);
                Assert.AreEqual(ExplorationMapDTO.CartographyMaskAxisBits, restoredData.explorationMap.cartographyMaskAxisBits);
                Assert.AreEqual(ExplorationMapDTO.CartographyMaskOriginOffset, restoredData.explorationMap.cartographyMaskOriginOffset);
                Assert.AreEqual(expectedAlignedByteCount, restoredData.explorationMap.discoveredSectorByteCount);
            }
        }

        [Test]
        public void PdaMarkerRuntimeMigration_CurrentRepairsMalformedEntryPosition()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.pdaMarkers.EnsureCapacity();
            data.pdaMarkers.markerCount = 1;
            data.pdaMarkers.nextSequence = 1;
            data.pdaMarkers.entries[0] = new PDAMarkerEntryDTO
            {
                markerId = "marker.sample",
                title = "Marker Sample",
                iconType = 2,
                posX = float.NaN,
                posY = 2f,
                posZ = float.PositiveInfinity,
                visibleOnHud = true,
                positionEncodingVersion = PDAMarkerEntryDTO.AupPositionEncodingVersion,
                aupGridX = 7L,
                aupGridY = 8L,
                aupGridZ = 9L,
                aupLocalX = float.NegativeInfinity,
                aupLocalY = 3f,
                aupLocalZ = float.NaN
            };

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            PDAMarkerEntryDTO entry = data.pdaMarkers.entries[0];
            Assert.AreEqual("marker.sample", entry.markerId);
            Assert.AreEqual("Marker Sample", entry.title);
            Assert.AreEqual(2, entry.iconType);
            Assert.AreEqual(0f, entry.posX);
            Assert.AreEqual(2f, entry.posY);
            Assert.AreEqual(0f, entry.posZ);
            Assert.IsTrue(entry.visibleOnHud);
            Assert.AreEqual(PDAMarkerEntryDTO.AupPositionEncodingVersion, entry.positionEncodingVersion);
            Assert.AreEqual(7L, entry.aupGridX);
            Assert.AreEqual(8L, entry.aupGridY);
            Assert.AreEqual(9L, entry.aupGridZ);
            Assert.AreEqual(0f, entry.aupLocalX);
            Assert.AreEqual(3f, entry.aupLocalY);
            Assert.AreEqual(0f, entry.aupLocalZ);
            StringAssert.Contains("pda marker entries repaired", summary);
        }

        [Test]
        public void PdaMarkerRuntimeMigration_CurrentRepairsMalformedSequence()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.pdaMarkers.EnsureCapacity();
            data.pdaMarkers.markerCount = 1;
            data.pdaMarkers.nextSequence = 0;
            data.pdaMarkers.entries[0] = new PDAMarkerEntryDTO
            {
                markerId = "marker.sequence",
                title = "Marker Sequence"
            };

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(1, data.pdaMarkers.markerCount);
            Assert.AreEqual(2, data.pdaMarkers.nextSequence);
            StringAssert.Contains("pda marker sequence repaired", summary);
        }

        [Test]
        public void PdaMarkerRuntime_WriteSanitizesMalformedEntryPosition()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.pdaMarkers.EnsureCapacity();
            data.pdaMarkers.markerCount = 1;
            data.pdaMarkers.nextSequence = 2;
            data.pdaMarkers.entries[0] = new PDAMarkerEntryDTO
            {
                markerId = "marker.write",
                title = "Marker Write",
                iconType = 3,
                posX = float.PositiveInfinity,
                posY = 5f,
                posZ = float.NaN,
                visibleOnHud = true,
                positionEncodingVersion = PDAMarkerEntryDTO.AupPositionEncodingVersion,
                aupGridX = 11L,
                aupGridY = 12L,
                aupGridZ = 13L,
                aupLocalX = float.NaN,
                aupLocalY = 6f,
                aupLocalZ = float.NegativeInfinity
            };

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
                Assert.AreEqual(0, CountLittleEndianFloat(payload, bytesWritten, float.NaN));
                Assert.AreEqual(0, CountLittleEndianFloat(payload, bytesWritten, float.PositiveInfinity));
                Assert.AreEqual(0, CountLittleEndianFloat(payload, bytesWritten, float.NegativeInfinity));

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(1, restoredData.pdaMarkers.markerCount);
                PDAMarkerEntryDTO entry = restoredData.pdaMarkers.entries[0];
                Assert.AreEqual("marker.write", entry.markerId);
                Assert.AreEqual("Marker Write", entry.title);
                Assert.AreEqual(3, entry.iconType);
                Assert.AreEqual(0f, entry.posX);
                Assert.AreEqual(5f, entry.posY);
                Assert.AreEqual(0f, entry.posZ);
                Assert.IsTrue(entry.visibleOnHud);
                Assert.AreEqual(PDAMarkerEntryDTO.AupPositionEncodingVersion, entry.positionEncodingVersion);
                Assert.AreEqual(11L, entry.aupGridX);
                Assert.AreEqual(12L, entry.aupGridY);
                Assert.AreEqual(13L, entry.aupGridZ);
                Assert.AreEqual(0f, entry.aupLocalX);
                Assert.AreEqual(6f, entry.aupLocalY);
                Assert.AreEqual(0f, entry.aupLocalZ);
            }
        }

        [Test]
        public void PdaMarkerRuntime_WriteSanitizesMalformedSequence()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.pdaMarkers.EnsureCapacity();
            data.pdaMarkers.markerCount = 1;
            data.pdaMarkers.nextSequence = -5;
            data.pdaMarkers.entries[0] = new PDAMarkerEntryDTO
            {
                markerId = "marker.write.sequence",
                title = "Marker Write Sequence"
            };

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(1, restoredData.pdaMarkers.markerCount);
                Assert.AreEqual(1, restoredData.pdaMarkers.nextSequence);
                Assert.AreEqual("marker.write.sequence", restoredData.pdaMarkers.entries[0].markerId);
            }
        }

        [Test]
        public void PdaAdvisoryRuntimeMigration_CurrentRepairsMalformedValues()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.pdaAdvisories.issuedFlags = 123;
            data.pdaAdvisories.oxygenDeathCount = -1;
            data.pdaAdvisories.inventoryFullAttemptCount = -2;
            data.pdaAdvisories.pressureDeathCount = -3;
            data.pdaAdvisories.baseEmergencyCount = -4;
            data.pdaAdvisories.staleAirIncidentCount = -5;
            data.pdaAdvisories.coldStressIncidentCount = -6;
            data.pdaAdvisories.heatStressIncidentCount = -7;
            data.pdaAdvisories.deepExposureSeconds = float.PositiveInfinity;
            data.pdaAdvisories.coldStressExposureSeconds = float.NegativeInfinity;
            data.pdaAdvisories.heatStressExposureSeconds = float.NaN;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(123, data.pdaAdvisories.issuedFlags);
            Assert.AreEqual(0, data.pdaAdvisories.oxygenDeathCount);
            Assert.AreEqual(0, data.pdaAdvisories.inventoryFullAttemptCount);
            Assert.AreEqual(0, data.pdaAdvisories.pressureDeathCount);
            Assert.AreEqual(0, data.pdaAdvisories.baseEmergencyCount);
            Assert.AreEqual(0, data.pdaAdvisories.staleAirIncidentCount);
            Assert.AreEqual(0, data.pdaAdvisories.coldStressIncidentCount);
            Assert.AreEqual(0, data.pdaAdvisories.heatStressIncidentCount);
            Assert.AreEqual(0f, data.pdaAdvisories.deepExposureSeconds);
            Assert.AreEqual(0f, data.pdaAdvisories.coldStressExposureSeconds);
            Assert.AreEqual(0f, data.pdaAdvisories.heatStressExposureSeconds);
            StringAssert.Contains("pda advisory oxygen-death count repaired", summary);
            StringAssert.Contains("pda advisory deep-exposure time repaired", summary);
            StringAssert.Contains("pda advisory cold-stress exposure repaired", summary);
            StringAssert.Contains("pda advisory heat-stress exposure repaired", summary);
        }

        [Test]
        public void PdaAdvisoryRuntime_WriteSanitizesMalformedValues()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.pdaAdvisories.issuedFlags = 456;
            data.pdaAdvisories.oxygenDeathCount = 2;
            data.pdaAdvisories.inventoryFullAttemptCount = -1;
            data.pdaAdvisories.pressureDeathCount = -2;
            data.pdaAdvisories.baseEmergencyCount = 3;
            data.pdaAdvisories.staleAirIncidentCount = -4;
            data.pdaAdvisories.coldStressIncidentCount = 5;
            data.pdaAdvisories.heatStressIncidentCount = -6;
            data.pdaAdvisories.deepExposureSeconds = float.NaN;
            data.pdaAdvisories.coldStressExposureSeconds = float.PositiveInfinity;
            data.pdaAdvisories.heatStressExposureSeconds = float.NegativeInfinity;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
                Assert.AreEqual(0, CountLittleEndianFloat(payload, bytesWritten, float.NaN));
                Assert.AreEqual(0, CountLittleEndianFloat(payload, bytesWritten, float.PositiveInfinity));
                Assert.AreEqual(0, CountLittleEndianFloat(payload, bytesWritten, float.NegativeInfinity));

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(456, restoredData.pdaAdvisories.issuedFlags);
                Assert.AreEqual(2, restoredData.pdaAdvisories.oxygenDeathCount);
                Assert.AreEqual(0, restoredData.pdaAdvisories.inventoryFullAttemptCount);
                Assert.AreEqual(0, restoredData.pdaAdvisories.pressureDeathCount);
                Assert.AreEqual(3, restoredData.pdaAdvisories.baseEmergencyCount);
                Assert.AreEqual(0, restoredData.pdaAdvisories.staleAirIncidentCount);
                Assert.AreEqual(5, restoredData.pdaAdvisories.coldStressIncidentCount);
                Assert.AreEqual(0, restoredData.pdaAdvisories.heatStressIncidentCount);
                Assert.AreEqual(0f, restoredData.pdaAdvisories.deepExposureSeconds);
                Assert.AreEqual(0f, restoredData.pdaAdvisories.coldStressExposureSeconds);
                Assert.AreEqual(0f, restoredData.pdaAdvisories.heatStressExposureSeconds);
            }
        }

        [Test]
        public void EnvironmentalStrainRuntimeMigration_CurrentRepairsMalformedValues()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.environmentalStrain.microplasticStrain = float.NaN;
            data.environmentalStrain.generalPollution = float.NegativeInfinity;
            data.environmentalStrain.recycledPlasticItemCount = -3;
            data.environmentalStrain.discardedItemCount = -4;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(0f, data.environmentalStrain.microplasticStrain);
            Assert.AreEqual(0f, data.environmentalStrain.generalPollution);
            Assert.AreEqual(0, data.environmentalStrain.recycledPlasticItemCount);
            Assert.AreEqual(0, data.environmentalStrain.discardedItemCount);
            StringAssert.Contains("environmental strain values clamped", summary);
        }

        [Test]
        public void EnvironmentalStrainRuntime_WriteSanitizesMalformedValues()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.environmentalStrain.microplasticStrain = float.PositiveInfinity;
            data.environmentalStrain.generalPollution = float.NaN;
            data.environmentalStrain.recycledPlasticItemCount = -5;
            data.environmentalStrain.discardedItemCount = 7;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(0f, restoredData.environmentalStrain.microplasticStrain);
                Assert.AreEqual(0f, restoredData.environmentalStrain.generalPollution);
                Assert.AreEqual(0, restoredData.environmentalStrain.recycledPlasticItemCount);
                Assert.AreEqual(7, restoredData.environmentalStrain.discardedItemCount);
            }
        }

        [Test]
        public void EcosystemRuntimeMigration_CurrentRepairsMalformedInfectedSeverity()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.ecosystemState.EnsureCapacity();
            data.ecosystemState.infectedZoneCount = 3;
            data.ecosystemState.infectedChunkKeys[0] = 10L;
            data.ecosystemState.infectedChunkKeys[1] = 11L;
            data.ecosystemState.infectedChunkKeys[2] = 12L;
            data.ecosystemState.infectedSeverities[0] = float.NaN;
            data.ecosystemState.infectedSeverities[1] = 2f;
            data.ecosystemState.infectedSeverities[2] = -1f;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(3, data.ecosystemState.infectedZoneCount);
            Assert.AreEqual(10L, data.ecosystemState.infectedChunkKeys[0]);
            Assert.AreEqual(11L, data.ecosystemState.infectedChunkKeys[1]);
            Assert.AreEqual(12L, data.ecosystemState.infectedChunkKeys[2]);
            Assert.AreEqual(0f, data.ecosystemState.infectedSeverities[0]);
            Assert.AreEqual(1f, data.ecosystemState.infectedSeverities[1]);
            Assert.AreEqual(0f, data.ecosystemState.infectedSeverities[2]);
            StringAssert.Contains("ecosystem infected severity repaired", summary);
        }

        [Test]
        public void EcosystemRuntimeMigration_CurrentRepairsMalformedGenerationVersion()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.ecosystemState.worldGenerationVersionId = -2;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(0, data.ecosystemState.worldGenerationVersionId);
            StringAssert.Contains("world generation version clamped", summary);
        }

        [Test]
        public void EcosystemRuntime_WriteSanitizesMalformedInfectedSeverity()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.ecosystemState.EnsureCapacity();
            data.ecosystemState.infectedZoneCount = 3;
            data.ecosystemState.infectedChunkKeys[0] = 20L;
            data.ecosystemState.infectedChunkKeys[1] = 21L;
            data.ecosystemState.infectedChunkKeys[2] = 22L;
            data.ecosystemState.infectedSeverities[0] = float.PositiveInfinity;
            data.ecosystemState.infectedSeverities[1] = 0.5f;
            data.ecosystemState.infectedSeverities[2] = float.NaN;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(3, restoredData.ecosystemState.infectedZoneCount);
                Assert.AreEqual(20L, restoredData.ecosystemState.infectedChunkKeys[0]);
                Assert.AreEqual(21L, restoredData.ecosystemState.infectedChunkKeys[1]);
                Assert.AreEqual(22L, restoredData.ecosystemState.infectedChunkKeys[2]);
                Assert.AreEqual(0f, restoredData.ecosystemState.infectedSeverities[0]);
                Assert.AreEqual(0.5f, restoredData.ecosystemState.infectedSeverities[1]);
                Assert.AreEqual(0f, restoredData.ecosystemState.infectedSeverities[2]);
            }
        }

        [Test]
        public void EcosystemRuntime_WriteSanitizesMalformedGenerationVersion()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.ecosystemState.worldGenerationVersionId = -5;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(0, restoredData.ecosystemState.worldGenerationVersionId);
            }
        }

        [Test]
        public void ExternalScavengerRuntimeMigration_CurrentCompactsInvalidSites()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.externalScavengerSites = new[]
            {
                new ExternalScavengerSiteDTO
                {
                    chunkX = 1,
                    chunkY = 2,
                    chunkZ = 3,
                    offsetX = 4,
                    offsetY = 5,
                    offsetZ = 6,
                    quantizedRadius = 7,
                    remainingTime = 11.5f,
                    seed = 123u
                },
                new ExternalScavengerSiteDTO
                {
                    chunkX = 99,
                    remainingTime = float.NaN
                },
                new ExternalScavengerSiteDTO
                {
                    chunkX = 100,
                    remainingTime = 0f
                }
            };

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(1, data.externalScavengerSites.Length);
            Assert.AreEqual(1, data.externalScavengerSites[0].chunkX);
            Assert.AreEqual(2, data.externalScavengerSites[0].chunkY);
            Assert.AreEqual(3, data.externalScavengerSites[0].chunkZ);
            Assert.AreEqual(11.5f, data.externalScavengerSites[0].remainingTime);
            Assert.AreEqual(123u, data.externalScavengerSites[0].seed);
            StringAssert.Contains("external scavenger sites repaired", summary);
        }

        [Test]
        public void ExternalScavengerRuntime_WriteCompactsInvalidSites()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.externalScavengerSites = new[]
            {
                new ExternalScavengerSiteDTO
                {
                    chunkX = 10,
                    chunkY = 20,
                    chunkZ = 30,
                    offsetX = 1,
                    offsetY = 2,
                    offsetZ = 3,
                    quantizedRadius = 4,
                    remainingTime = 15.25f,
                    seed = 456u
                },
                new ExternalScavengerSiteDTO
                {
                    chunkX = 98,
                    remainingTime = float.PositiveInfinity
                },
                new ExternalScavengerSiteDTO
                {
                    chunkX = 97,
                    remainingTime = -1f
                }
            };

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(1, restoredData.externalScavengerSites.Length);
                Assert.AreEqual(10, restoredData.externalScavengerSites[0].chunkX);
                Assert.AreEqual(20, restoredData.externalScavengerSites[0].chunkY);
                Assert.AreEqual(30, restoredData.externalScavengerSites[0].chunkZ);
                Assert.AreEqual(15.25f, restoredData.externalScavengerSites[0].remainingTime);
                Assert.AreEqual(456u, restoredData.externalScavengerSites[0].seed);
            }
        }

        [Test]
        public void RtgDecayRuntimeMigration_CurrentRepairsMalformedRecords()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.rtgDecayCount = 3;
            data.rtgDecaySourceIds = new[] { -7, 101 };
            data.rtgStartTimesSeconds = new[] { double.NaN, 45.5d };
            data.rtgDecayFlags = new byte[] { 0xFF, 0x05 };

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(2, data.rtgDecayCount);
            Assert.AreEqual(SaveData.MaxRtgDecayRecords, data.rtgDecaySourceIds.Length);
            Assert.AreEqual(SaveData.MaxRtgDecayRecords, data.rtgStartTimesSeconds.Length);
            Assert.AreEqual(SaveData.MaxRtgDecayRecords, data.rtgDecayFlags.Length);
            Assert.AreEqual(0, data.rtgDecaySourceIds[0]);
            Assert.AreEqual(101, data.rtgDecaySourceIds[1]);
            Assert.AreEqual(0d, data.rtgStartTimesSeconds[0]);
            Assert.AreEqual(45.5d, data.rtgStartTimesSeconds[1]);
            Assert.AreEqual(SaveData.RtgDecayPersistedFlagMask, data.rtgDecayFlags[0]);
            Assert.AreEqual(0x05, data.rtgDecayFlags[1]);
            StringAssert.Contains("rtg decay count clamped", summary);
            StringAssert.Contains("rtg decay arrays repaired", summary);
            StringAssert.Contains("rtg decay records repaired", summary);
        }

        [Test]
        public void RtgDecayRuntimeMigration_PreservesRecordsWhenOptionalColumnsAreShort()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.rtgDecayCount = 2;
            data.rtgDecaySourceIds = new[] { 303, 404 };
            data.rtgStartTimesSeconds = new[] { 12.5d };
            data.rtgDecayFlags = new byte[] { 0x05 };

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(2, data.rtgDecayCount);
            Assert.AreEqual(303, data.rtgDecaySourceIds[0]);
            Assert.AreEqual(404, data.rtgDecaySourceIds[1]);
            Assert.AreEqual(12.5d, data.rtgStartTimesSeconds[0]);
            Assert.AreEqual(0d, data.rtgStartTimesSeconds[1]);
            Assert.AreEqual(0x05, data.rtgDecayFlags[0]);
            Assert.AreEqual(0, data.rtgDecayFlags[1]);
            StringAssert.Contains("rtg decay arrays repaired", summary);
            StringAssert.Contains("rtg decay partial records defaulted", summary);
        }

        [Test]
        public void RtgDecayRuntime_WriteSanitizesMalformedRecords()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.EnsureRtgDecayCapacity();
            data.rtgDecayCount = 2;
            data.rtgDecaySourceIds[0] = -5;
            data.rtgDecaySourceIds[1] = 202;
            data.rtgStartTimesSeconds[0] = double.NegativeInfinity;
            data.rtgStartTimesSeconds[1] = 88.25d;
            data.rtgDecayFlags[0] = 0xF0;
            data.rtgDecayFlags[1] = 0xFF;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(2, restoredData.rtgDecayCount);
                Assert.AreEqual(SaveData.MaxRtgDecayRecords, restoredData.rtgDecaySourceIds.Length);
                Assert.AreEqual(SaveData.MaxRtgDecayRecords, restoredData.rtgStartTimesSeconds.Length);
                Assert.AreEqual(SaveData.MaxRtgDecayRecords, restoredData.rtgDecayFlags.Length);
                Assert.AreEqual(0, restoredData.rtgDecaySourceIds[0]);
                Assert.AreEqual(202, restoredData.rtgDecaySourceIds[1]);
                Assert.AreEqual(0d, restoredData.rtgStartTimesSeconds[0]);
                Assert.AreEqual(88.25d, restoredData.rtgStartTimesSeconds[1]);
                Assert.AreEqual(0, restoredData.rtgDecayFlags[0]);
                Assert.AreEqual(SaveData.RtgDecayPersistedFlagMask, restoredData.rtgDecayFlags[1]);
            }
        }

        [Test]
        public void RtgDecayRuntime_WritePreservesRecordsWhenOptionalColumnsAreShort()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.rtgDecayCount = 2;
            data.rtgDecaySourceIds = new[] { 505, 606 };
            data.rtgStartTimesSeconds = new[] { 99.75d };
            data.rtgDecayFlags = new byte[] { 0x0D };

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(2, restoredData.rtgDecayCount);
                Assert.AreEqual(SaveData.MaxRtgDecayRecords, restoredData.rtgDecaySourceIds.Length);
                Assert.AreEqual(SaveData.MaxRtgDecayRecords, restoredData.rtgStartTimesSeconds.Length);
                Assert.AreEqual(SaveData.MaxRtgDecayRecords, restoredData.rtgDecayFlags.Length);
                Assert.AreEqual(505, restoredData.rtgDecaySourceIds[0]);
                Assert.AreEqual(606, restoredData.rtgDecaySourceIds[1]);
                Assert.AreEqual(99.75d, restoredData.rtgStartTimesSeconds[0]);
                Assert.AreEqual(0d, restoredData.rtgStartTimesSeconds[1]);
                Assert.AreEqual(0x0D, restoredData.rtgDecayFlags[0]);
                Assert.AreEqual(0, restoredData.rtgDecayFlags[1]);
            }
        }

        [Test]
        public void ExplorationMapRuntime_WriteSanitizesMalformedDenseMetadataAndShortMasks()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.explorationMap.exploredChunkCount = ExplorationMapDTO.MaxExploredChunks + 5;
            data.explorationMap.chunkSizeMeters = -1;
            data.explorationMap.mortonMaskAxisBits = 99;
            data.explorationMap.mortonMaskOriginOffset = -3;
            data.explorationMap.mortonBuildSalt = 123u;
            data.explorationMap.exploredMortonByteCount = 1;
            data.explorationMap.exploredMortonMaskBytes = new byte[] { 0x5A };
            data.explorationMap.cartographyCellSizeMeters = -10;
            data.explorationMap.cartographyMaskAxisBits = 42;
            data.explorationMap.cartographyMaskOriginOffset = -7;
            data.explorationMap.discoveredSectorByteCount = 1;
            data.explorationMap.discoveredSectorMaskBytes = new byte[] { 0xA5 };

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                ExplorationMapDTO map = restoredData.explorationMap;
                Assert.AreEqual(ExplorationMapDTO.MaxExploredChunks, map.exploredChunkCount);
                Assert.AreEqual(ExplorationMapDTO.DenseChunkSizeMeters, map.chunkSizeMeters);
                Assert.AreEqual(ExplorationMapDTO.MortonMaskAxisBits, map.mortonMaskAxisBits);
                Assert.AreEqual(ExplorationMapDTO.MortonMaskOriginOffset, map.mortonMaskOriginOffset);
                Assert.AreEqual(SaveBinaryStorage.ExplorationMortonBuildSalt32, map.mortonBuildSalt);
                Assert.AreEqual(SaveBinaryStorage.AlignExplorationMortonByteCount(1), map.exploredMortonByteCount);
                Assert.AreEqual(ExplorationMapDTO.MortonMaskByteCount, map.exploredMortonMaskBytes.Length);
                Assert.AreEqual(0x5A, map.exploredMortonMaskBytes[0]);
                Assert.AreEqual(0, map.exploredMortonMaskBytes[1]);
                Assert.AreEqual(ExplorationMapDTO.CartographyCellSizeMeters, map.cartographyCellSizeMeters);
                Assert.AreEqual(ExplorationMapDTO.CartographyMaskAxisBits, map.cartographyMaskAxisBits);
                Assert.AreEqual(ExplorationMapDTO.CartographyMaskOriginOffset, map.cartographyMaskOriginOffset);
                Assert.AreEqual(SaveBinaryStorage.AlignExplorationMortonByteCount(1), map.discoveredSectorByteCount);
                Assert.AreEqual(ExplorationMapDTO.CartographyMaskByteCount, map.discoveredSectorMaskBytes.Length);
                Assert.AreEqual(0xA5, map.discoveredSectorMaskBytes[0]);
                Assert.AreEqual(0, map.discoveredSectorMaskBytes[1]);
            }
        }

        [Test]
        public void BeaconNetworkRuntimeMigration_CurrentRepairsMalformedBeaconEntries()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.beaconNetwork.EnsureCapacity();
            data.beaconNetwork.activeCount = 1;
            data.beaconNetwork.nextSequence = 0;
            data.beaconNetwork.entries[0] = new BeaconEntryDTO
            {
                id = "beacon-alpha",
                label = "Alpha",
                posX = float.NaN,
                posY = 2f,
                posZ = float.PositiveInfinity,
                rotX = 0f,
                rotY = 0f,
                rotZ = 0f,
                rotW = 0f,
                colorR = -1f,
                colorG = 0.25f,
                colorB = float.PositiveInfinity,
                colorA = 0f,
                lightRange = float.NaN
            };

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(1, data.beaconNetwork.activeCount);
            Assert.AreEqual(2, data.beaconNetwork.nextSequence);
            BeaconEntryDTO entry = data.beaconNetwork.entries[0];
            Assert.AreEqual("beacon-alpha", entry.id);
            Assert.AreEqual("Alpha", entry.label);
            Assert.AreEqual(0f, entry.posX);
            Assert.AreEqual(2f, entry.posY);
            Assert.AreEqual(0f, entry.posZ);
            Assert.AreEqual(0f, entry.rotX);
            Assert.AreEqual(0f, entry.rotY);
            Assert.AreEqual(0f, entry.rotZ);
            Assert.AreEqual(1f, entry.rotW);
            Assert.AreEqual(0f, entry.colorR);
            Assert.AreEqual(0.25f, entry.colorG);
            Assert.AreEqual(1f, entry.colorB);
            Assert.AreEqual(1f, entry.colorA);
            Assert.AreEqual(BeaconEntryDTO.DefaultLightRange, entry.lightRange);
            StringAssert.Contains("beacon entries repaired", summary);
            StringAssert.Contains("beacon sequence repaired", summary);
        }

        [Test]
        public void BeaconNetworkRuntime_WriteSanitizesMalformedBeaconEntries()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.beaconNetwork.EnsureCapacity();
            data.beaconNetwork.activeCount = 1;
            data.beaconNetwork.nextSequence = 0;
            data.beaconNetwork.entries[0] = new BeaconEntryDTO
            {
                id = "beacon-beta",
                label = "Beta",
                posX = float.PositiveInfinity,
                posY = 5f,
                posZ = float.NaN,
                rotX = 0f,
                rotY = 3f,
                rotZ = 0f,
                rotW = 4f,
                colorR = 2f,
                colorG = -2f,
                colorB = 0.5f,
                colorA = float.NegativeInfinity,
                lightRange = -2f
            };

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(1, restoredData.beaconNetwork.activeCount);
                Assert.AreEqual(1, restoredData.beaconNetwork.nextSequence);
                BeaconEntryDTO entry = restoredData.beaconNetwork.entries[0];
                Assert.AreEqual("beacon-beta", entry.id);
                Assert.AreEqual("Beta", entry.label);
                Assert.AreEqual(0f, entry.posX);
                Assert.AreEqual(5f, entry.posY);
                Assert.AreEqual(0f, entry.posZ);
                Assert.AreEqual(0f, entry.rotX);
                Assert.AreEqual(0.6f, entry.rotY, 0.0001f);
                Assert.AreEqual(0f, entry.rotZ);
                Assert.AreEqual(0.8f, entry.rotW, 0.0001f);
                Assert.AreEqual(1f, entry.colorR);
                Assert.AreEqual(0f, entry.colorG);
                Assert.AreEqual(0.5f, entry.colorB);
                Assert.AreEqual(1f, entry.colorA);
                Assert.AreEqual(BeaconEntryDTO.DefaultLightRange, entry.lightRange);
            }
        }

        [Test]
        public void BeaconNetworkRuntime_ReadClampsMalformedOuterCount()
        {
            const int nextSequence = 7;
            BeaconEntryDTO expectedEntry = new BeaconEntryDTO
            {
                id = "beacon-read",
                label = "Read Beacon",
                posX = 1f,
                posY = 2f,
                posZ = 3f,
                rotX = 0f,
                rotY = 0f,
                rotZ = 0f,
                rotW = 1f,
                colorR = 0.25f,
                colorG = 0.5f,
                colorB = 0.75f,
                colorA = 1f,
                lightRange = 8f
            };

            SaveData data = SaveData.CreateNew(0.0);
            data.beaconNetwork.EnsureCapacity();
            data.beaconNetwork.activeCount = 1;
            data.beaconNetwork.nextSequence = nextSequence;
            data.beaconNetwork.entries[0] = expectedEntry;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            int bytesWritten;
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
            }

            byte[] marker = BuildBeaconNetworkSingleEntryMarker(expectedEntry, nextSequence);
            int beaconOffset = FindLittleEndianByteSequenceOffset(payload, bytesWritten, marker);
            Assert.GreaterOrEqual(beaconOffset, 0);
            PatchPayloadInt(payload, beaconOffset, BeaconNetworkDTO.MaxEntries + 10);

            fixed (byte* payloadPtr = payload)
            {
                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(1, restoredData.beaconNetwork.activeCount);
                Assert.AreEqual(nextSequence, restoredData.beaconNetwork.nextSequence);
                Assert.AreEqual(BeaconNetworkDTO.MaxEntries, restoredData.beaconNetwork.entries.Length);
                BeaconEntryDTO entry = restoredData.beaconNetwork.entries[0];
                Assert.AreEqual(expectedEntry.id, entry.id);
                Assert.AreEqual(expectedEntry.label, entry.label);
                Assert.AreEqual(expectedEntry.posX, entry.posX);
                Assert.AreEqual(expectedEntry.posY, entry.posY);
                Assert.AreEqual(expectedEntry.posZ, entry.posZ);
                Assert.AreEqual(expectedEntry.rotW, entry.rotW);
                Assert.AreEqual(expectedEntry.lightRange, entry.lightRange);
            }
        }

        [Test]
        public void PdaLogbookRuntimeMigration_CurrentRepairsMalformedEntryTime()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.pdaLogbook.EnsureCapacity();
            data.pdaLogbook.entryCount = 2;
            data.pdaLogbook.nextSequence = 0;
            data.pdaLogbook.entries[0] = new PDALogbookEntryDTO
            {
                sequence = -5,
                dayIndex = -2,
                dayTimeHours = float.PositiveInfinity,
                playTimeSeconds = float.NaN,
                titleHash = 11,
                messageHash = 12,
                originHash = 13
            };
            data.pdaLogbook.entries[1] = new PDALogbookEntryDTO
            {
                sequence = 7,
                dayIndex = 3,
                dayTimeHours = 31f,
                playTimeSeconds = -4f,
                titleHash = 21,
                messageHash = 22,
                originHash = 23
            };

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(2, data.pdaLogbook.entryCount);
            Assert.AreEqual(3, data.pdaLogbook.nextSequence);
            Assert.AreEqual(0, data.pdaLogbook.entries[0].sequence);
            Assert.AreEqual(0, data.pdaLogbook.entries[0].dayIndex);
            Assert.AreEqual(0f, data.pdaLogbook.entries[0].dayTimeHours);
            Assert.AreEqual(0f, data.pdaLogbook.entries[0].playTimeSeconds);
            Assert.AreEqual(11, data.pdaLogbook.entries[0].titleHash);
            Assert.AreEqual(12, data.pdaLogbook.entries[0].messageHash);
            Assert.AreEqual(13, data.pdaLogbook.entries[0].originHash);
            Assert.AreEqual(7, data.pdaLogbook.entries[1].sequence);
            Assert.AreEqual(3, data.pdaLogbook.entries[1].dayIndex);
            Assert.AreEqual(24f, data.pdaLogbook.entries[1].dayTimeHours);
            Assert.AreEqual(0f, data.pdaLogbook.entries[1].playTimeSeconds);
            StringAssert.Contains("pda logbook sequence repaired", summary);
            StringAssert.Contains("pda logbook entries repaired", summary);
        }

        [Test]
        public void PdaLogbookRuntimeMigration_CurrentConvertsLegacyEntryStrings()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.pdaLogbook.EnsureCapacity();
            data.pdaLogbook.entryCount = 2;
            data.pdaLogbook.nextSequence = 3;
            data.pdaLogbook.entries[0] = new PDALogbookEntryDTO
            {
                sequence = 1,
                dayIndex = 1,
                dayTimeHours = 5f,
                playTimeSeconds = 10f,
                title = "pda.log.title.alpha",
                message = "pda.log.message.alpha",
                originKey = "pda.log.origin.alpha"
            };
            data.pdaLogbook.entries[1] = new PDALogbookEntryDTO
            {
                sequence = 2,
                dayIndex = 1,
                dayTimeHours = 6f,
                playTimeSeconds = 20f,
                titleHash = 101,
                messageHash = 102,
                originHash = 103,
                title = "pda.log.title.stale",
                message = "pda.log.message.stale",
                originKey = "pda.log.origin.stale"
            };

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(LocHash.Compute("pda.log.title.alpha"), data.pdaLogbook.entries[0].titleHash);
            Assert.AreEqual(LocHash.Compute("pda.log.message.alpha"), data.pdaLogbook.entries[0].messageHash);
            Assert.AreEqual(LocHash.Compute("pda.log.origin.alpha"), data.pdaLogbook.entries[0].originHash);
            Assert.AreEqual(101, data.pdaLogbook.entries[1].titleHash);
            Assert.AreEqual(102, data.pdaLogbook.entries[1].messageHash);
            Assert.AreEqual(103, data.pdaLogbook.entries[1].originHash);
            Assert.AreEqual(string.Empty, data.pdaLogbook.entries[0].title);
            Assert.AreEqual(string.Empty, data.pdaLogbook.entries[0].message);
            Assert.AreEqual(string.Empty, data.pdaLogbook.entries[0].originKey);
            Assert.AreEqual(string.Empty, data.pdaLogbook.entries[1].title);
            Assert.AreEqual(string.Empty, data.pdaLogbook.entries[1].message);
            Assert.AreEqual(string.Empty, data.pdaLogbook.entries[1].originKey);
            StringAssert.Contains("pda logbook entries repaired", summary);
        }

        [Test]
        public void PdaLogbookRuntimeMigration_CurrentConvertsLegacySeenOriginKeys()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.pdaLogbook.seenOriginCount = 4;
            data.pdaLogbook.seenOriginHashes = null;
            data.pdaLogbook.seenOriginKeys = new[]
            {
                "origin.alpha",
                null,
                string.Empty,
                "origin.beta"
            };

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(2, data.pdaLogbook.seenOriginCount);
            Assert.AreEqual(LocHash.Compute("origin.alpha"), data.pdaLogbook.seenOriginHashes[0]);
            Assert.AreEqual(LocHash.Compute("origin.beta"), data.pdaLogbook.seenOriginHashes[1]);
            Assert.AreEqual("origin.alpha", data.pdaLogbook.seenOriginKeys[0]);
            Assert.AreEqual("origin.beta", data.pdaLogbook.seenOriginKeys[1]);
            Assert.AreEqual(string.Empty, data.pdaLogbook.seenOriginKeys[2]);
            Assert.AreEqual(string.Empty, data.pdaLogbook.seenOriginKeys[3]);
            StringAssert.Contains("pda logbook capacity repaired", summary);
            StringAssert.Contains("pda logbook seen origins repaired", summary);
        }

        [Test]
        public void PdaLogbookRuntime_WriteSanitizesMalformedEntryTime()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.pdaLogbook.EnsureCapacity();
            data.pdaLogbook.entryCount = 1;
            data.pdaLogbook.nextSequence = -1;
            data.pdaLogbook.entries[0] = new PDALogbookEntryDTO
            {
                sequence = -2,
                dayIndex = -3,
                dayTimeHours = float.NegativeInfinity,
                playTimeSeconds = float.PositiveInfinity,
                titleHash = 31,
                messageHash = 32,
                originHash = 33
            };

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
                Assert.AreEqual(0, CountLittleEndianFloat(payload, bytesWritten, float.PositiveInfinity));
                Assert.AreEqual(0, CountLittleEndianFloat(payload, bytesWritten, float.NegativeInfinity));

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(1, restoredData.pdaLogbook.entryCount);
                Assert.AreEqual(1, restoredData.pdaLogbook.nextSequence);
                PDALogbookEntryDTO entry = restoredData.pdaLogbook.entries[0];
                Assert.AreEqual(0, entry.sequence);
                Assert.AreEqual(0, entry.dayIndex);
                Assert.AreEqual(0f, entry.dayTimeHours);
                Assert.AreEqual(0f, entry.playTimeSeconds);
                Assert.AreEqual(31, entry.titleHash);
                Assert.AreEqual(32, entry.messageHash);
                Assert.AreEqual(33, entry.originHash);
            }
        }

        [Test]
        public void PlayerStatsRuntime_ReadClampsNonFiniteResourceFileValues()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.playerStats.oxygen = 91.25f;
            data.playerStats.energy = 82.5f;
            data.playerStats.integrity = 73.75f;
            data.playerStats.weight = 14.5f;
            data.playerStats.hunger = 64.25f;
            data.playerStats.thirst = 55.5f;
            data.playerStats.currentLifeLowestOxygenNormalized = 0.21f;
            data.playerStats.currentLifeLowestEnergyNormalized = 0.32f;
            data.playerStats.currentLifeLowestIntegrityNormalized = 0.43f;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            int bytesWritten;
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
            }

            Assert.AreEqual(
                1,
                PatchLittleEndianFloatSequence(
                    payload,
                    bytesWritten,
                    new[] { 91.25f, 82.5f, 73.75f, 14.5f, 64.25f, 55.5f },
                    new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, float.NaN, float.PositiveInfinity, float.NegativeInfinity }));
            Assert.AreEqual(
                1,
                PatchLittleEndianFloatSequence(
                    payload,
                    bytesWritten,
                    new[] { 0.21f, 0.32f, 0.43f },
                    new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity }));

            fixed (byte* payloadPtr = payload)
            {
                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(0f, restored.playerStats.oxygen);
                Assert.AreEqual(0f, restored.playerStats.energy);
                Assert.AreEqual(0f, restored.playerStats.integrity);
                Assert.AreEqual(0f, restored.playerStats.weight);
                Assert.AreEqual(0f, restored.playerStats.hunger);
                Assert.AreEqual(0f, restored.playerStats.thirst);
                Assert.AreEqual(1f, restored.playerStats.currentLifeLowestOxygenNormalized);
                Assert.AreEqual(1f, restored.playerStats.currentLifeLowestEnergyNormalized);
                Assert.AreEqual(1f, restored.playerStats.currentLifeLowestIntegrityNormalized);
            }
        }

        [Test]
        public void PlayerStatsRuntime_WriteSanitizesMalformedInjuryFlags()
        {
            const byte malformedFlags = 0xFF;

            SaveData data = SaveData.CreateNew(0.0);
            data.playerStats.injuryFlags = malformedFlags;
            data.playerStats.bleedingSecondsRemaining = 12f;
            data.playerStats.bleedingDamagePerSecond = 3f;
            data.playerStats.bleedingSeverity01 = 0.8f;
            data.playerStats.fractureSecondsRemaining = 18f;
            data.playerStats.fracturePenalty01 = 0.6f;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
                Assert.AreEqual(malformedFlags, data.playerStats.injuryFlags);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(SaveData.PlayerInjurySupportedFlagMask, restored.playerStats.injuryFlags);
                Assert.AreEqual(12f, restored.playerStats.bleedingSecondsRemaining);
                Assert.AreEqual(3f, restored.playerStats.bleedingDamagePerSecond);
                Assert.AreEqual(0.8f, restored.playerStats.bleedingSeverity01);
                Assert.AreEqual(18f, restored.playerStats.fractureSecondsRemaining);
                Assert.AreEqual(0.6f, restored.playerStats.fracturePenalty01);
            }
        }

        [Test]
        public void PlayerStatsRuntime_WriteClearsInactiveInjuryPayload()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.playerStats.injuryFlags = 0;
            data.playerStats.bleedingSecondsRemaining = 12f;
            data.playerStats.bleedingDamagePerSecond = 3f;
            data.playerStats.bleedingSeverity01 = 0.8f;
            data.playerStats.fractureSecondsRemaining = 18f;
            data.playerStats.fracturePenalty01 = 0.6f;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(0, restored.playerStats.injuryFlags);
                Assert.AreEqual(0f, restored.playerStats.bleedingSecondsRemaining);
                Assert.AreEqual(0f, restored.playerStats.bleedingDamagePerSecond);
                Assert.AreEqual(0f, restored.playerStats.bleedingSeverity01);
                Assert.AreEqual(0f, restored.playerStats.fractureSecondsRemaining);
                Assert.AreEqual(0f, restored.playerStats.fracturePenalty01);
            }
        }

        [Test]
        public void PlayerStatsRuntime_WriteSanitizesActiveLastDeathPayload()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.playerStats.hasLastDeathRecord = true;
            data.playerStats.lastDeathCause = 0xFF;
            data.playerStats.lastDeathPosX = float.NaN;
            data.playerStats.lastDeathPosY = 2f;
            data.playerStats.lastDeathPosZ = float.PositiveInfinity;
            data.playerStats.lastDeathLifeDurationSeconds = double.NaN;
            data.playerStats.lastDeathPeakDepthMeters = double.PositiveInfinity;
            data.playerStats.lastDeathLowestOxygenNormalized = float.NaN;
            data.playerStats.lastDeathLowestEnergyNormalized = float.PositiveInfinity;
            data.playerStats.lastDeathLowestIntegrityNormalized = float.NegativeInfinity;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.IsTrue(restored.playerStats.hasLastDeathRecord);
                Assert.AreEqual(0, restored.playerStats.lastDeathCause);
                Assert.AreEqual(0f, restored.playerStats.lastDeathPosX);
                Assert.AreEqual(0f, restored.playerStats.lastDeathPosY);
                Assert.AreEqual(0f, restored.playerStats.lastDeathPosZ);
                Assert.AreEqual(0d, restored.playerStats.lastDeathLifeDurationSeconds);
                Assert.AreEqual(0d, restored.playerStats.lastDeathPeakDepthMeters);
                Assert.AreEqual(1f, restored.playerStats.lastDeathLowestOxygenNormalized);
                Assert.AreEqual(1f, restored.playerStats.lastDeathLowestEnergyNormalized);
                Assert.AreEqual(1f, restored.playerStats.lastDeathLowestIntegrityNormalized);
            }
        }

        [Test]
        public void PlayerStatsRuntime_WriteClearsInactiveLastDeathPayload()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.playerStats.hasLastDeathRecord = false;
            data.playerStats.lastDeathCause = SaveData.PlayerLastDeathCauseMaxKnown;
            data.playerStats.lastDeathPosX = 1f;
            data.playerStats.lastDeathPosY = 2f;
            data.playerStats.lastDeathPosZ = 3f;
            data.playerStats.lastDeathLifeDurationSeconds = 12d;
            data.playerStats.lastDeathPeakDepthMeters = 34d;
            data.playerStats.lastDeathLowestOxygenNormalized = 0.2f;
            data.playerStats.lastDeathLowestEnergyNormalized = 0.3f;
            data.playerStats.lastDeathLowestIntegrityNormalized = 0.4f;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.IsFalse(restored.playerStats.hasLastDeathRecord);
                Assert.AreEqual(0, restored.playerStats.lastDeathCause);
                Assert.AreEqual(0f, restored.playerStats.lastDeathPosX);
                Assert.AreEqual(0f, restored.playerStats.lastDeathPosY);
                Assert.AreEqual(0f, restored.playerStats.lastDeathPosZ);
                Assert.AreEqual(0d, restored.playerStats.lastDeathLifeDurationSeconds);
                Assert.AreEqual(0d, restored.playerStats.lastDeathPeakDepthMeters);
                Assert.AreEqual(0f, restored.playerStats.lastDeathLowestOxygenNormalized);
                Assert.AreEqual(0f, restored.playerStats.lastDeathLowestEnergyNormalized);
                Assert.AreEqual(0f, restored.playerStats.lastDeathLowestIntegrityNormalized);
            }
        }

        [Test]
        public void HectonSurvivalSystemRuntime_PopulateSaveDataUsesCurrentInjuryMask()
        {
            const float bleedingSeverity = 0.42f;
            const float fracturePenalty = 0.37f;

            UnityEngine.GameObject gameObject = new UnityEngine.GameObject("HectonSurvivalSaveInjuryMaskTest");
            gameObject.SetActive(false);

            bool previousIgnoreFailingMessages = UnityEngine.TestTools.LogAssert.ignoreFailingMessages;
            HectonSurvivalSystem survival = null;
            try
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
                survival = gameObject.AddComponent<HectonSurvivalSystem>();
            }
            finally
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }

            try
            {
                SetPrivateInstanceField(
                    survival,
                    "_cachedCombatStatusMask",
                    CombatStatusBits.Bleeding64 | CombatStatusBits.Fractured64);
                SetPrivateInstanceField(survival, "_hasCachedCombatStatusMask", true);
                SetPrivateInstanceField(survival, "_bleedingSeverity01", bleedingSeverity);
                SetPrivateInstanceField(survival, "_fracturePenalty01", fracturePenalty);

                SaveData data = SaveData.CreateNew(0.0);
                survival.PopulateSaveData(data);

                Assert.AreEqual(SaveData.PlayerInjurySupportedFlagMask, data.playerStats.injuryFlags);
                Assert.AreEqual(bleedingSeverity, data.playerStats.bleedingSeverity01, 0.0001f);
                Assert.AreEqual(fracturePenalty, data.playerStats.fracturePenalty01, 0.0001f);

                SetPrivateInstanceField(survival, "_bleedingSeverity01", float.NaN);
                SetPrivateInstanceField(survival, "_fracturePenalty01", float.PositiveInfinity);
                SetPrivateInstanceField(survival, "oxygen", float.NaN);
                SetPrivateInstanceField(survival, "energy", float.PositiveInfinity);
                SetPrivateInstanceField(survival, "integrity", float.NegativeInfinity);
                SetPrivateInstanceField(survival, "weight", float.NaN);
                SetPrivateInstanceField(survival, "hunger", float.PositiveInfinity);
                SetPrivateInstanceField(survival, "thirst", float.NegativeInfinity);
                SetPrivateInstanceField(survival, "_currentLifeDurationSeconds", double.NaN);
                SetPrivateInstanceField(survival, "_currentLifePeakDepthMeters", double.PositiveInfinity);
                SetPrivateInstanceField(survival, "_currentLifeLowestOxygenNormalized", float.NaN);
                SetPrivateInstanceField(survival, "_currentLifeLowestEnergyNormalized", float.PositiveInfinity);
                SetPrivateInstanceField(survival, "_currentLifeLowestIntegrityNormalized", float.NegativeInfinity);

                SaveData nonFinite = SaveData.CreateNew(0.0);
                survival.PopulateSaveData(nonFinite);

                Assert.AreEqual(SaveData.PlayerInjurySupportedFlagMask, nonFinite.playerStats.injuryFlags);
                Assert.AreEqual(0f, nonFinite.playerStats.oxygen);
                Assert.AreEqual(0f, nonFinite.playerStats.energy);
                Assert.AreEqual(0f, nonFinite.playerStats.integrity);
                Assert.AreEqual(0f, nonFinite.playerStats.weight);
                Assert.AreEqual(0f, nonFinite.playerStats.hunger);
                Assert.AreEqual(0f, nonFinite.playerStats.thirst);
                Assert.AreEqual(0d, nonFinite.playerStats.currentLifeDurationSeconds);
                Assert.AreEqual(0d, nonFinite.playerStats.currentLifePeakDepthMeters);
                Assert.AreEqual(1f, nonFinite.playerStats.currentLifeLowestOxygenNormalized);
                Assert.AreEqual(1f, nonFinite.playerStats.currentLifeLowestEnergyNormalized);
                Assert.AreEqual(1f, nonFinite.playerStats.currentLifeLowestIntegrityNormalized);
                Assert.AreEqual(0f, nonFinite.playerStats.bleedingSeverity01);
                Assert.AreEqual(0f, nonFinite.playerStats.fracturePenalty01);

                SetPrivateInstanceField(survival, "_cachedCombatStatusMask", 0UL);
                SetPrivateInstanceField(survival, "_hasCachedCombatStatusMask", false);

                SaveData cleared = SaveData.CreateNew(0.0);
                survival.PopulateSaveData(cleared);

                Assert.AreEqual(0, cleared.playerStats.injuryFlags);
                Assert.AreEqual(0f, cleared.playerStats.bleedingSeverity01);
                Assert.AreEqual(0f, cleared.playerStats.fracturePenalty01);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void HectonSurvivalSystemRuntime_LoadFromSaveDataSanitizesInjuryPayload()
        {
            UnityEngine.GameObject gameObject = new UnityEngine.GameObject("HectonSurvivalLoadInjuryMaskTest");
            gameObject.SetActive(false);
            global::SurvivalStats stats = UnityEngine.ScriptableObject.CreateInstance<global::SurvivalStats>();

            bool previousIgnoreFailingMessages = UnityEngine.TestTools.LogAssert.ignoreFailingMessages;
            HectonSurvivalSystem survival = null;
            try
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
                survival = gameObject.AddComponent<HectonSurvivalSystem>();
            }
            finally
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }

            try
            {
                SetPrivateInstanceField(survival, "stats", stats);

                SaveData data = SaveData.CreateNew(0.0);
                data.playerStats.oxygen = float.NaN;
                data.playerStats.energy = float.PositiveInfinity;
                data.playerStats.integrity = float.NegativeInfinity;
                data.playerStats.hunger = float.NaN;
                data.playerStats.thirst = float.PositiveInfinity;
                data.playerStats.injuryFlags = SaveData.PlayerInjurySupportedFlagMask;
                data.playerStats.bleedingSeverity01 = float.NaN;
                data.playerStats.fracturePenalty01 = float.PositiveInfinity;
                data.playerStats.hasLastDeathRecord = false;
                data.playerStats.lastDeathCause = SaveData.PlayerLastDeathCauseMaxKnown;
                data.playerStats.lastDeathPosX = 1f;
                data.playerStats.lastDeathPosY = 2f;
                data.playerStats.lastDeathPosZ = 3f;
                data.playerStats.lastDeathLifeDurationSeconds = 12d;
                data.playerStats.lastDeathPeakDepthMeters = 34d;
                data.playerStats.lastDeathLowestOxygenNormalized = 0.2f;
                data.playerStats.lastDeathLowestEnergyNormalized = 0.3f;
                data.playerStats.lastDeathLowestIntegrityNormalized = 0.4f;

                survival.LoadFromSaveData(data);

                Assert.AreEqual(0f, survival.Oxygen);
                Assert.AreEqual(0f, survival.Energy);
                Assert.AreEqual(0f, survival.Integrity);
                Assert.AreEqual(0f, survival.Hunger);
                Assert.AreEqual(0f, survival.Thirst);
                Assert.IsFalse(survival.HasLastDeathRecord);
                Assert.AreEqual(0f, GetPrivateInstanceField<float>(survival, "_bleedingSeverity01"));
                Assert.AreEqual(0f, GetPrivateInstanceField<float>(survival, "_fracturePenalty01"));

                SaveData restored = SaveData.CreateNew(0.0);
                survival.PopulateSaveData(restored);

                Assert.AreEqual(SaveData.PlayerInjurySupportedFlagMask, restored.playerStats.injuryFlags);
                Assert.AreEqual(0f, restored.playerStats.bleedingSeverity01);
                Assert.AreEqual(0f, restored.playerStats.fracturePenalty01);
                Assert.IsFalse(restored.playerStats.hasLastDeathRecord);
                Assert.AreEqual(0, restored.playerStats.lastDeathCause);
                Assert.AreEqual(0f, restored.playerStats.lastDeathPosX);
                Assert.AreEqual(0f, restored.playerStats.lastDeathPosY);
                Assert.AreEqual(0f, restored.playerStats.lastDeathPosZ);
                Assert.AreEqual(0d, restored.playerStats.lastDeathLifeDurationSeconds);
                Assert.AreEqual(0d, restored.playerStats.lastDeathPeakDepthMeters);
                Assert.AreEqual(0f, restored.playerStats.lastDeathLowestOxygenNormalized);
                Assert.AreEqual(0f, restored.playerStats.lastDeathLowestEnergyNormalized);
                Assert.AreEqual(0f, restored.playerStats.lastDeathLowestIntegrityNormalized);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stats);
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void PlayerStatsRuntime_ReadClampsNonFiniteKinematicFileValues()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.playerStats.posX = 1234.25f;
            data.playerStats.posY = 2345.5f;
            data.playerStats.posZ = 3456.75f;
            data.playerStats.rotX = 0.5f;
            data.playerStats.rotY = 0.5f;
            data.playerStats.rotZ = 0.5f;
            data.playerStats.rotW = 0.5f;
            data.playerStats.velX = 4.5f;
            data.playerStats.velY = 5.5f;
            data.playerStats.velZ = 6.5f;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            int bytesWritten;
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
            }

            Assert.AreEqual(
                2,
                PatchLittleEndianFloatSequence(
                    payload,
                    bytesWritten,
                    new[] { 1234.25f, 2345.5f, 3456.75f, 0.5f, 0.5f, 0.5f, 0.5f, 4.5f, 5.5f, 6.5f },
                    new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, 0f, 0f, 0f, 0f, float.NaN, float.PositiveInfinity, float.NegativeInfinity }));

            fixed (byte* payloadPtr = payload)
            {
                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(0f, restored.playerStats.posX);
                Assert.AreEqual(0f, restored.playerStats.posY);
                Assert.AreEqual(0f, restored.playerStats.posZ);
                Assert.AreEqual(0f, restored.playerStats.rotX);
                Assert.AreEqual(0f, restored.playerStats.rotY);
                Assert.AreEqual(0f, restored.playerStats.rotZ);
                Assert.AreEqual(1f, restored.playerStats.rotW);
                Assert.AreEqual(0f, restored.playerStats.velX);
                Assert.AreEqual(0f, restored.playerStats.velY);
                Assert.AreEqual(0f, restored.playerStats.velZ);
                Assert.AreEqual(0f, restored.playerKinematicState.posX);
                Assert.AreEqual(0f, restored.playerKinematicState.posY);
                Assert.AreEqual(0f, restored.playerKinematicState.posZ);
                Assert.AreEqual(0f, restored.playerKinematicState.rotX);
                Assert.AreEqual(0f, restored.playerKinematicState.rotY);
                Assert.AreEqual(0f, restored.playerKinematicState.rotZ);
                Assert.AreEqual(1f, restored.playerKinematicState.rotW);
                Assert.AreEqual(0f, restored.playerKinematicState.velX);
                Assert.AreEqual(0f, restored.playerKinematicState.velY);
                Assert.AreEqual(0f, restored.playerKinematicState.velZ);
            }
        }

        [Test]
        public void PlayerStatsRuntimeMigration_CurrentClampsNonFiniteResourceValues()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.playerStats.oxygen = float.NaN;
            data.playerStats.energy = float.PositiveInfinity;
            data.playerStats.integrity = float.NegativeInfinity;
            data.playerStats.weight = float.NaN;
            data.playerStats.hunger = float.PositiveInfinity;
            data.playerStats.thirst = float.NegativeInfinity;
            data.playerStats.currentLifeLowestOxygenNormalized = float.NaN;
            data.playerStats.currentLifeLowestEnergyNormalized = float.PositiveInfinity;
            data.playerStats.currentLifeLowestIntegrityNormalized = float.NegativeInfinity;
            data.playerStats.nitrogenBuildUp = SaveData.PlayerStatsNitrogenBuildUpHardCap * 2f;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(0f, data.playerStats.oxygen);
            Assert.AreEqual(0f, data.playerStats.energy);
            Assert.AreEqual(0f, data.playerStats.integrity);
            Assert.AreEqual(0f, data.playerStats.weight);
            Assert.AreEqual(0f, data.playerStats.hunger);
            Assert.AreEqual(0f, data.playerStats.thirst);
            Assert.AreEqual(1f, data.playerStats.currentLifeLowestOxygenNormalized);
            Assert.AreEqual(1f, data.playerStats.currentLifeLowestEnergyNormalized);
            Assert.AreEqual(1f, data.playerStats.currentLifeLowestIntegrityNormalized);
            Assert.AreEqual(SaveData.PlayerStatsNitrogenBuildUpHardCap, data.playerStats.nitrogenBuildUp);
            StringAssert.Contains("player survival state repaired", summary);
        }

        [Test]
        public void PlayerStatsRuntimeMigration_CurrentRepairsMalformedInjuryFlags()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.playerStats.injuryFlags = 0xFF;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(SaveData.PlayerInjurySupportedFlagMask, data.playerStats.injuryFlags);
            StringAssert.Contains("player survival state repaired", summary);
        }

        [Test]
        public void PlayerStatsRuntimeMigration_CurrentClearsInactiveInjuryPayload()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.playerStats.injuryFlags = 0;
            data.playerStats.bleedingSecondsRemaining = 12f;
            data.playerStats.bleedingDamagePerSecond = 3f;
            data.playerStats.bleedingSeverity01 = 0.8f;
            data.playerStats.fractureSecondsRemaining = 18f;
            data.playerStats.fracturePenalty01 = 0.6f;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(0, data.playerStats.injuryFlags);
            Assert.AreEqual(0f, data.playerStats.bleedingSecondsRemaining);
            Assert.AreEqual(0f, data.playerStats.bleedingDamagePerSecond);
            Assert.AreEqual(0f, data.playerStats.bleedingSeverity01);
            Assert.AreEqual(0f, data.playerStats.fractureSecondsRemaining);
            Assert.AreEqual(0f, data.playerStats.fracturePenalty01);
            StringAssert.Contains("player survival state repaired", summary);
        }

        [Test]
        public void PlayerStatsRuntimeMigration_CurrentClearsInactiveLastDeathPayload()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.playerStats.hasLastDeathRecord = false;
            data.playerStats.lastDeathCause = SaveData.PlayerLastDeathCauseMaxKnown;
            data.playerStats.lastDeathPosX = 1f;
            data.playerStats.lastDeathPosY = 2f;
            data.playerStats.lastDeathPosZ = 3f;
            data.playerStats.lastDeathLifeDurationSeconds = 12d;
            data.playerStats.lastDeathPeakDepthMeters = 34d;
            data.playerStats.lastDeathLowestOxygenNormalized = 0.2f;
            data.playerStats.lastDeathLowestEnergyNormalized = 0.3f;
            data.playerStats.lastDeathLowestIntegrityNormalized = 0.4f;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.IsFalse(data.playerStats.hasLastDeathRecord);
            Assert.AreEqual(0, data.playerStats.lastDeathCause);
            Assert.AreEqual(0f, data.playerStats.lastDeathPosX);
            Assert.AreEqual(0f, data.playerStats.lastDeathPosY);
            Assert.AreEqual(0f, data.playerStats.lastDeathPosZ);
            Assert.AreEqual(0d, data.playerStats.lastDeathLifeDurationSeconds);
            Assert.AreEqual(0d, data.playerStats.lastDeathPeakDepthMeters);
            Assert.AreEqual(0f, data.playerStats.lastDeathLowestOxygenNormalized);
            Assert.AreEqual(0f, data.playerStats.lastDeathLowestEnergyNormalized);
            Assert.AreEqual(0f, data.playerStats.lastDeathLowestIntegrityNormalized);
            StringAssert.Contains("player survival state repaired", summary);
        }

        [Test]
        public void PlayerKinematicRuntime_RefreshFirstHourMirrorsSanitizesStatsMirror()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.playerStats.posX = float.NaN;
            data.playerStats.posY = 12f;
            data.playerStats.posZ = float.PositiveInfinity;
            data.playerStats.rotX = 0f;
            data.playerStats.rotY = 0f;
            data.playerStats.rotZ = 0f;
            data.playerStats.rotW = 0f;
            data.playerStats.velX = SaveData.PlayerKinematicVelocityHardCapMetersPerSecond * 2f;
            data.playerStats.velY = 0f;
            data.playerStats.velZ = 0f;
            data.playerStats.injuryFlags = 0;
            data.playerStats.bleedingSeverity01 = 0.8f;
            data.playerStats.fracturePenalty01 = 0.6f;

            data.RefreshFirstHourDtoMirrors();

            Assert.AreEqual(0f, data.playerStats.posX);
            Assert.AreEqual(0f, data.playerStats.posY);
            Assert.AreEqual(0f, data.playerStats.posZ);
            Assert.AreEqual(0f, data.playerStats.rotX);
            Assert.AreEqual(0f, data.playerStats.rotY);
            Assert.AreEqual(0f, data.playerStats.rotZ);
            Assert.AreEqual(1f, data.playerStats.rotW);
            Assert.AreEqual(
                SaveData.PlayerKinematicVelocityHardCapMetersPerSecond,
                data.playerStats.velX,
                0.0001f);
            Assert.AreEqual(0f, data.playerStats.velY);
            Assert.AreEqual(0f, data.playerStats.velZ);
            Assert.AreEqual(0f, data.playerStats.bleedingSeverity01);
            Assert.AreEqual(0f, data.playerStats.fracturePenalty01);
            Assert.AreEqual(0f, data.playerKinematicState.posX);
            Assert.AreEqual(0f, data.playerKinematicState.posY);
            Assert.AreEqual(0f, data.playerKinematicState.posZ);
            Assert.AreEqual(0f, data.playerKinematicState.rotX);
            Assert.AreEqual(0f, data.playerKinematicState.rotY);
            Assert.AreEqual(0f, data.playerKinematicState.rotZ);
            Assert.AreEqual(1f, data.playerKinematicState.rotW);
            Assert.AreEqual(
                SaveData.PlayerKinematicVelocityHardCapMetersPerSecond,
                data.playerKinematicState.velX,
                0.0001f);
            Assert.AreEqual(0f, data.playerKinematicState.velY);
            Assert.AreEqual(0f, data.playerKinematicState.velZ);
            Assert.AreEqual(1, data.playerKinematicState.flags);
        }

        [Test]
        public void PlayerKinematicRuntimeMigration_CurrentUsesDedicatedKinematicState()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.playerStats.posX = 100f;
            data.playerStats.posY = 200f;
            data.playerStats.posZ = 300f;
            data.playerKinematicState.posX = 12.25f;
            data.playerKinematicState.posY = 23.5f;
            data.playerKinematicState.posZ = 34.75f;
            data.playerKinematicState.rotX = 0f;
            data.playerKinematicState.rotY = 0f;
            data.playerKinematicState.rotZ = 0f;
            data.playerKinematicState.rotW = 0f;
            data.playerKinematicState.velX = 160f;
            data.playerKinematicState.velY = 0f;
            data.playerKinematicState.velZ = 0f;
            data.playerKinematicState.flags = 7;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(12.25f, data.playerKinematicState.posX);
            Assert.AreEqual(23.5f, data.playerKinematicState.posY);
            Assert.AreEqual(34.75f, data.playerKinematicState.posZ);
            Assert.AreEqual(0f, data.playerKinematicState.rotX);
            Assert.AreEqual(0f, data.playerKinematicState.rotY);
            Assert.AreEqual(0f, data.playerKinematicState.rotZ);
            Assert.AreEqual(1f, data.playerKinematicState.rotW);
            Assert.AreEqual(SaveData.PlayerKinematicVelocityHardCapMetersPerSecond, data.playerKinematicState.velX, 0.0001f);
            Assert.AreEqual(0f, data.playerKinematicState.velY);
            Assert.AreEqual(0f, data.playerKinematicState.velZ);
            Assert.AreEqual(7, data.playerKinematicState.flags);
            Assert.AreEqual(data.playerKinematicState.posX, data.playerStats.posX);
            Assert.AreEqual(data.playerKinematicState.posY, data.playerStats.posY);
            Assert.AreEqual(data.playerKinematicState.posZ, data.playerStats.posZ);
            Assert.AreEqual(data.playerKinematicState.rotW, data.playerStats.rotW);
            Assert.AreEqual(data.playerKinematicState.velX, data.playerStats.velX, 0.0001f);
            StringAssert.Contains("player survival state repaired", summary);
        }

        [Test]
        public void PlayerKinematicRuntimeMigration_PreV72CopiesLegacyStatsToKinematicState()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.FirstHourDtoLockPersistenceVersion - 1;
            data.playerStats.posX = 44.25f;
            data.playerStats.posY = 55.5f;
            data.playerStats.posZ = 66.75f;
            data.playerStats.rotX = 0.5f;
            data.playerStats.rotY = 0.5f;
            data.playerStats.rotZ = 0.5f;
            data.playerStats.rotW = 0.5f;
            data.playerStats.velX = 6f;
            data.playerStats.velY = 7f;
            data.playerStats.velZ = 8f;
            data.playerKinematicState.posX = 999f;
            data.playerKinematicState.posY = 999f;
            data.playerKinematicState.posZ = 999f;
            data.playerKinematicState.flags = 99;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.FirstHourDtoLockPersistenceVersion - 1, originalVersion);
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(44.25f, data.playerKinematicState.posX);
            Assert.AreEqual(55.5f, data.playerKinematicState.posY);
            Assert.AreEqual(66.75f, data.playerKinematicState.posZ);
            Assert.AreEqual(0.5f, data.playerKinematicState.rotX);
            Assert.AreEqual(0.5f, data.playerKinematicState.rotY);
            Assert.AreEqual(0.5f, data.playerKinematicState.rotZ);
            Assert.AreEqual(0.5f, data.playerKinematicState.rotW);
            Assert.AreEqual(6f, data.playerKinematicState.velX);
            Assert.AreEqual(7f, data.playerKinematicState.velY);
            Assert.AreEqual(8f, data.playerKinematicState.velZ);
            Assert.AreEqual(1, data.playerKinematicState.flags);
            Assert.AreEqual(data.playerKinematicState.posX, data.playerStats.posX);
            Assert.AreEqual(data.playerKinematicState.posY, data.playerStats.posY);
            Assert.AreEqual(data.playerKinematicState.posZ, data.playerStats.posZ);
            StringAssert.Contains("player survival state repaired", summary);
        }

        [Test]
        public void HazardZoneRuntimeMigration_PreV74DropsUnpersistedToxicity()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.HazardZoneRuntimePersistenceVersion - 1;
            data.hazardZones.toxicityDose = 32f;
            data.hazardZones.toxicityPulseAccumulatorSeconds = 0.25f;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.HazardZoneRuntimePersistenceVersion - 1, originalVersion);
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(0f, data.hazardZones.toxicityDose);
            Assert.AreEqual(0f, data.hazardZones.toxicityPulseAccumulatorSeconds);
            StringAssert.Contains("hazard zone toxicity state repaired", summary);
        }

        [Test]
        public void HazardZoneRuntimeMigration_V74ClampsNonFiniteAndOutOfRangeValues()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.HazardZoneRuntimePersistenceVersion;
            data.hazardZones.toxicityDose = float.NaN;
            data.hazardZones.toxicityPulseAccumulatorSeconds = 3f;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.HazardZoneRuntimePersistenceVersion, originalVersion);
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(0f, data.hazardZones.toxicityDose);
            Assert.AreEqual(0f, data.hazardZones.toxicityPulseAccumulatorSeconds);
            StringAssert.Contains("hazard zone toxicity state repaired", summary);
        }

        [Test]
        public void HazardZoneRuntimeMigration_V74ClearsInactivePulseBelowDamageThreshold()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.HazardZoneRuntimePersistenceVersion;
            data.hazardZones.toxicityDose = SaveData.HazardZoneToxicityDamageDoseThreshold * 0.5f;
            data.hazardZones.toxicityPulseAccumulatorSeconds = 0.25f;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.HazardZoneRuntimePersistenceVersion, originalVersion);
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(SaveData.HazardZoneToxicityDamageDoseThreshold * 0.5f, data.hazardZones.toxicityDose);
            Assert.AreEqual(0f, data.hazardZones.toxicityPulseAccumulatorSeconds);
            StringAssert.Contains("hazard zone toxicity state repaired", summary);
        }

        private static void SetPrivateInstanceField<TValue>(object target, string fieldName, TValue value)
        {
            Assert.IsNotNull(target);
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(target, value);
        }

        private static TValue GetPrivateInstanceField<TValue>(object target, string fieldName)
        {
            Assert.IsNotNull(target);
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            return (TValue)field.GetValue(target);
        }

        private static long EncodedStructArrayBytes<T>(int count) where T : unmanaged
        {
            return sizeof(int) + (long)Math.Clamp(count, 0, int.MaxValue) * UnsafeUtility.SizeOf<T>();
        }

        private static int EncodedStringArraySingleEntryBytes(string value)
        {
            Assert.IsNotNull(value);
            return sizeof(int) + sizeof(int) + (value.Length * sizeof(char));
        }

        private static int EncodedStringBytes(string value)
        {
            Assert.IsNotNull(value);
            return sizeof(int) + (value.Length * sizeof(char));
        }

        private static int EncodedBarterOfferStateArraySingleEntryBytes(string offerId)
        {
            return sizeof(int) + EncodedStringBytes(offerId) + sizeof(int);
        }

        private static byte[] BuildBarterSingleOfferMarker(string offerId, int executionCount)
        {
            Assert.IsTrue(BitConverter.IsLittleEndian);
            Assert.IsNotNull(offerId);
            byte[] bytes = new byte[
                sizeof(int) +
                EncodedBarterOfferStateArraySingleEntryBytes(offerId) +
                sizeof(int) +
                sizeof(int)];
            int offset = 0;
            WritePayloadInt(bytes, ref offset, 1);
            WritePayloadInt(bytes, ref offset, 1);
            WritePayloadString(bytes, ref offset, offerId);
            WritePayloadInt(bytes, ref offset, executionCount);
            WritePayloadInt(bytes, ref offset, 0);
            WritePayloadInt(bytes, ref offset, 0);
            Assert.AreEqual(bytes.Length, offset);
            return bytes;
        }

        private static byte[] BuildDataArchaeologyPartialScanMarker(uint hash, ushort progressPermille)
        {
            Assert.IsTrue(BitConverter.IsLittleEndian);
            byte[] bytes = new byte[sizeof(int) + sizeof(int) + sizeof(uint) + sizeof(int) + sizeof(ushort)];
            int offset = 0;
            WritePayloadInt(bytes, ref offset, 1);
            WritePayloadInt(bytes, ref offset, 1);
            WritePayloadUInt(bytes, ref offset, hash);
            WritePayloadInt(bytes, ref offset, 1);
            WritePayloadUShort(bytes, ref offset, progressPermille);
            Assert.AreEqual(bytes.Length, offset);
            return bytes;
        }

        private static byte[] BuildDataArchaeologyScanStateMarker(int key, byte state)
        {
            Assert.IsTrue(BitConverter.IsLittleEndian);
            byte[] bytes = new byte[sizeof(int) + sizeof(int) + sizeof(int) + sizeof(int) + sizeof(byte)];
            int offset = 0;
            WritePayloadInt(bytes, ref offset, 1);
            WritePayloadInt(bytes, ref offset, 1);
            WritePayloadInt(bytes, ref offset, key);
            WritePayloadInt(bytes, ref offset, 1);
            WritePayloadByte(bytes, ref offset, state);
            Assert.AreEqual(bytes.Length, offset);
            return bytes;
        }

        private static byte[] BuildBeaconNetworkSingleEntryMarker(BeaconEntryDTO entry, int nextSequence)
        {
            Assert.IsTrue(BitConverter.IsLittleEndian);
            Assert.IsNotNull(entry.id);
            Assert.IsNotNull(entry.label);
            byte[] bytes = new byte[
                (sizeof(int) * 3) +
                EncodedStringBytes(entry.id) +
                EncodedStringBytes(entry.label) +
                (sizeof(float) * 12)];
            int offset = 0;
            WritePayloadInt(bytes, ref offset, 1);
            WritePayloadInt(bytes, ref offset, nextSequence);
            WritePayloadInt(bytes, ref offset, 1);
            WritePayloadString(bytes, ref offset, entry.id);
            WritePayloadString(bytes, ref offset, entry.label);
            WritePayloadFloat(bytes, ref offset, entry.posX);
            WritePayloadFloat(bytes, ref offset, entry.posY);
            WritePayloadFloat(bytes, ref offset, entry.posZ);
            WritePayloadFloat(bytes, ref offset, entry.rotX);
            WritePayloadFloat(bytes, ref offset, entry.rotY);
            WritePayloadFloat(bytes, ref offset, entry.rotZ);
            WritePayloadFloat(bytes, ref offset, entry.rotW);
            WritePayloadFloat(bytes, ref offset, entry.colorR);
            WritePayloadFloat(bytes, ref offset, entry.colorG);
            WritePayloadFloat(bytes, ref offset, entry.colorB);
            WritePayloadFloat(bytes, ref offset, entry.colorA);
            WritePayloadFloat(bytes, ref offset, entry.lightRange);
            Assert.AreEqual(bytes.Length, offset);
            return bytes;
        }

        private static byte[] BuildNarrativeDiscoveryRootMarker(
            int lastDiscoveredBiomeId,
            string narrativeId,
            int narrativeDepthTier)
        {
            Assert.IsTrue(BitConverter.IsLittleEndian);
            Assert.IsNotNull(narrativeId);
            byte[] bytes = new byte[
                sizeof(int) +
                sizeof(int) +
                EncodedStringArraySingleEntryBytes(narrativeId) +
                sizeof(int)];
            int offset = 0;
            WritePayloadInt(bytes, ref offset, lastDiscoveredBiomeId);
            WritePayloadInt(bytes, ref offset, 1);
            WritePayloadInt(bytes, ref offset, 1);
            WritePayloadString(bytes, ref offset, narrativeId);
            WritePayloadInt(bytes, ref offset, narrativeDepthTier);
            Assert.AreEqual(bytes.Length, offset);
            return bytes;
        }

        private static byte[] BuildCorporatePendingRootMarker(
            string receivedId,
            string orderA,
            string orderB,
            float timerA,
            float timerB,
            float firstHourSessionTime)
        {
            Assert.IsTrue(BitConverter.IsLittleEndian);
            Assert.IsNotNull(receivedId);
            Assert.IsNotNull(orderA);
            Assert.IsNotNull(orderB);
            byte[] bytes = new byte[
                EncodedStringArraySingleEntryBytes(receivedId) +
                sizeof(int) +
                EncodedStringBytes(orderA) +
                EncodedStringBytes(orderB) +
                sizeof(int) +
                (sizeof(float) * 2) +
                sizeof(float)];
            int offset = 0;
            WritePayloadInt(bytes, ref offset, 1);
            WritePayloadString(bytes, ref offset, receivedId);
            WritePayloadInt(bytes, ref offset, 2);
            WritePayloadString(bytes, ref offset, orderA);
            WritePayloadString(bytes, ref offset, orderB);
            WritePayloadInt(bytes, ref offset, 2);
            WritePayloadFloat(bytes, ref offset, timerA);
            WritePayloadFloat(bytes, ref offset, timerB);
            WritePayloadFloat(bytes, ref offset, firstHourSessionTime);
            Assert.AreEqual(bytes.Length, offset);
            return bytes;
        }

        private static byte[] BuildDiscoveredBiomeRootMarker(
            int legacyBiomeId,
            long[] discoveredBiomeBitWords,
            int lastDiscoveredBiomeId)
        {
            Assert.IsTrue(BitConverter.IsLittleEndian);
            Assert.IsNotNull(discoveredBiomeBitWords);
            Assert.GreaterOrEqual(discoveredBiomeBitWords.Length, BiomeDiscoveryBitMask.WordCount);
            byte[] bytes = new byte[(sizeof(int) * 4) + (sizeof(long) * BiomeDiscoveryBitMask.WordCount)];
            int offset = 0;
            WritePayloadInt(bytes, ref offset, 1);
            WritePayloadInt(bytes, ref offset, legacyBiomeId);
            WritePayloadInt(bytes, ref offset, BiomeDiscoveryBitMask.WordCount);
            for (int i = 0; i < BiomeDiscoveryBitMask.WordCount; i++)
                WritePayloadLong(bytes, ref offset, discoveredBiomeBitWords[i]);

            WritePayloadInt(bytes, ref offset, lastDiscoveredBiomeId);
            Assert.AreEqual(bytes.Length, offset);
            return bytes;
        }

        private static byte[] BuildIndustrialLoreRootMarker(long word)
        {
            Assert.IsTrue(BitConverter.IsLittleEndian);
            byte[] bytes = new byte[sizeof(int) + sizeof(long)];
            int offset = 0;
            WritePayloadInt(bytes, ref offset, IndustrialLoreBitMask.WordCount);
            WritePayloadLong(bytes, ref offset, word);
            Assert.AreEqual(bytes.Length, offset);
            return bytes;
        }

        private static byte[] BuildSuitUpgradeRootMarker(
            bool atlasSignalDetected,
            float atlasSignalPulseTimer,
            int atlasSignalRevealStage,
            ulong suitUpgradeMask)
        {
            Assert.IsTrue(BitConverter.IsLittleEndian);
            byte[] bytes = new byte[sizeof(byte) + sizeof(float) + sizeof(int) + sizeof(long)];
            int offset = 0;
            WritePayloadBool(bytes, ref offset, atlasSignalDetected);
            WritePayloadFloat(bytes, ref offset, atlasSignalPulseTimer);
            WritePayloadInt(bytes, ref offset, atlasSignalRevealStage);
            WritePayloadLong(bytes, ref offset, unchecked((long)suitUpgradeMask));
            Assert.AreEqual(bytes.Length, offset);
            return bytes;
        }

        private static ModuleDTO CreatePersistenceSampleModule()
        {
            return new ModuleDTO
            {
                prefabId = "module.sample",
                slottedToolItemId = "tool.sample",
                pipeInFlightItemId = "pipe.sample",
                pipeInFlightAmount = 3,
                pipeTransitProgress = 0.4f,
                pipeExportTimerSeconds = 1.5f,
                drillBufferedItemId = "drill.sample",
                drillBufferedAmount = 2,
                drillCycleTimerSeconds = 4.5f,
                sorterBufferedSlotCount = 2,
                sorterBufferedItemIds = new[] { "sorter.a", "sorter.b", "unused.sorter" },
                sorterBufferedQuantities = new[] { 1, 2, 99 },
                posX = 10f,
                posY = 20f,
                posZ = 30f,
                rotX = 0f,
                rotY = 0f,
                rotZ = 0f,
                rotW = 1f,
                integrity = 88f,
                repairIntegrityCap = 100f,
                airReserveNormalized = 0.7f,
                co2Normalized = 0.2f,
                isFlooded = true,
                failureMode = SaveData.ModuleFailureModeNone,
                health = 199,
                floodedReefFloodSeconds = 12f,
                interiorReefInfestationActive = true,
                cultivationSlotCount = 2,
                cultivationSeedItemIds = new[] { "seed.a", "seed.b", "unused.seed" },
                cultivationGeneticsMasks = new[] { 0x1UL, 0x2UL, 0x99UL },
                cultivationGrowth01 = new[] { 0.25f, 0.5f, 0.99f },
                cultivationQuality01 = new[] { 0.75f, 0.8f, 0.01f }
            };
        }

        private static void AssertModulePersistenceDifference(
            in ModuleDTO left,
            in ModuleDTO right,
            string fieldName)
        {
            Assert.IsFalse(
                ModuleDTO.PersistenceEquals(in left, in right),
                fieldName + " must participate in ModuleDTO persistence equality.");
        }

        private static int WriteInventoryShadowPayload(in InventoryDTO inventory, byte[] destination)
        {
            Assert.IsTrue(BitConverter.IsLittleEndian);
            Assert.IsNotNull(destination);
            int offset = 0;
            int count = Math.Clamp(inventory.cellCount, 0, InventoryDTO.MaxCells);

            WritePayloadInt(destination, ref offset, count);
            WritePayloadInt(destination, ref offset, count);
            for (int i = 0; i < count; i++)
                WritePayloadInt(destination, ref offset, inventory.itemHashIds[i]);

            WritePayloadInt(destination, ref offset, count);
            for (int i = 0; i < count; i++)
                WritePayloadUInt(destination, ref offset, inventory.packedCellCoordinates[i]);

            WritePayloadInt(destination, ref offset, count);
            for (int i = 0; i < count; i++)
                WritePayloadUShort(destination, ref offset, inventory.stackCounts[i]);

            WritePayloadInt(destination, ref offset, count);
            for (int i = 0; i < count; i++)
                WritePayloadUShort(destination, ref offset, inventory.itemStateFlags[i]);

            WritePayloadInt(destination, ref offset, count);
            for (int i = 0; i < count; i++)
                WritePayloadByte(destination, ref offset, inventory.itemGeneticsWords[i]);

            WritePayloadInt(destination, ref offset, count);
            for (int i = 0; i < count; i++)
                WritePayloadUShort(destination, ref offset, inventory.qualityMilli[i]);

            WritePayloadInt(destination, ref offset, count);
            for (int i = 0; i < count; i++)
                WritePayloadUInt(destination, ref offset, inventory.lastUpdateUnixSeconds[i]);

            int durabilityRleLength = Math.Clamp(
                inventory.itemDurabilityRleLength,
                0,
                inventory.itemDurabilityRle != null
                    ? Math.Min(inventory.itemDurabilityRle.Length, InventoryDTO.MaxDurabilityRleBytes)
                    : 0);
            WritePayloadInt(destination, ref offset, durabilityRleLength);
            for (int i = 0; i < durabilityRleLength; i++)
                WritePayloadByte(destination, ref offset, inventory.itemDurabilityRle[i]);

            WritePayloadFloat(destination, ref offset, inventory.totalWeight);
            WritePayloadInt(destination, ref offset, inventory.gridColumns);
            WritePayloadInt(destination, ref offset, inventory.gridRows);
            return offset;
        }

        private static void WritePayloadInt(byte[] destination, ref int offset, int value)
        {
            WritePayloadBytes(destination, ref offset, BitConverter.GetBytes(value), sizeof(int));
        }

        private static void PatchPayloadInt(byte[] destination, int offset, int value)
        {
            Assert.IsNotNull(destination);
            Assert.GreaterOrEqual(offset, 0);
            Assert.LessOrEqual(offset + sizeof(int), destination.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(value), 0, destination, offset, sizeof(int));
        }

        private static void PatchPayloadLong(byte[] destination, int offset, long value)
        {
            Assert.IsNotNull(destination);
            Assert.GreaterOrEqual(offset, 0);
            Assert.LessOrEqual(offset + sizeof(long), destination.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(value), 0, destination, offset, sizeof(long));
        }

        private static byte[] InsertPayloadInt(byte[] source, int bytesWritten, int offset, int value)
        {
            Assert.IsNotNull(source);
            Assert.GreaterOrEqual(bytesWritten, 0);
            Assert.LessOrEqual(bytesWritten, source.Length);
            Assert.GreaterOrEqual(offset, 0);
            Assert.LessOrEqual(offset, bytesWritten);
            byte[] destination = new byte[bytesWritten + sizeof(int)];
            Buffer.BlockCopy(source, 0, destination, 0, offset);
            Buffer.BlockCopy(BitConverter.GetBytes(value), 0, destination, offset, sizeof(int));
            Buffer.BlockCopy(
                source,
                offset,
                destination,
                offset + sizeof(int),
                bytesWritten - offset);
            return destination;
        }

        private static void WritePayloadUInt(byte[] destination, ref int offset, uint value)
        {
            WritePayloadBytes(destination, ref offset, BitConverter.GetBytes(value), sizeof(uint));
        }

        private static void WritePayloadUShort(byte[] destination, ref int offset, ushort value)
        {
            WritePayloadBytes(destination, ref offset, BitConverter.GetBytes(value), sizeof(ushort));
        }

        private static void WritePayloadFloat(byte[] destination, ref int offset, float value)
        {
            WritePayloadBytes(destination, ref offset, BitConverter.GetBytes(value), sizeof(float));
        }

        private static void WritePayloadLong(byte[] destination, ref int offset, long value)
        {
            WritePayloadBytes(destination, ref offset, BitConverter.GetBytes(value), sizeof(long));
        }

        private static void WritePayloadByte(byte[] destination, ref int offset, byte value)
        {
            Assert.LessOrEqual(offset + sizeof(byte), destination.Length);
            destination[offset] = value;
            offset += sizeof(byte);
        }

        private static void WritePayloadBool(byte[] destination, ref int offset, bool value)
        {
            WritePayloadByte(destination, ref offset, value ? (byte)1 : (byte)0);
        }

        private static void WritePayloadBytes(byte[] destination, ref int offset, byte[] source, int byteCount)
        {
            Assert.LessOrEqual(offset + byteCount, destination.Length);
            Buffer.BlockCopy(source, 0, destination, offset, byteCount);
            offset += byteCount;
        }

        private static int CountLittleEndianFloatPair(byte[] payload, int bytesWritten, float first, float second)
        {
            Assert.IsTrue(BitConverter.IsLittleEndian);
            byte[] firstBytes = BitConverter.GetBytes(first);
            byte[] secondBytes = BitConverter.GetBytes(second);
            int safeLength = Math.Clamp(bytesWritten, 0, payload != null ? payload.Length : 0);
            int count = 0;
            for (int i = 0; i <= safeLength - sizeof(float) * 2; i++)
            {
                if (payload[i] == firstBytes[0] &&
                    payload[i + 1] == firstBytes[1] &&
                    payload[i + 2] == firstBytes[2] &&
                    payload[i + 3] == firstBytes[3] &&
                    payload[i + 4] == secondBytes[0] &&
                    payload[i + 5] == secondBytes[1] &&
                    payload[i + 6] == secondBytes[2] &&
                    payload[i + 7] == secondBytes[3])
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountLittleEndianFloat(byte[] payload, int bytesWritten, float value)
        {
            Assert.IsTrue(BitConverter.IsLittleEndian);
            byte[] marker = BitConverter.GetBytes(value);
            int safeLength = Math.Clamp(bytesWritten, 0, payload != null ? payload.Length : 0);
            int count = 0;
            for (int i = 0; i <= safeLength - marker.Length; i++)
            {
                if (ByteSequenceMatches(payload, i, marker))
                    count++;
            }

            return count;
        }

        private static int CountLittleEndianLong(byte[] payload, int bytesWritten, long value)
        {
            Assert.IsTrue(BitConverter.IsLittleEndian);
            byte[] marker = BitConverter.GetBytes(value);
            int safeLength = Math.Clamp(bytesWritten, 0, payload != null ? payload.Length : 0);
            int count = 0;
            for (int i = 0; i <= safeLength - marker.Length; i++)
            {
                if (ByteSequenceMatches(payload, i, marker))
                    count++;
            }

            return count;
        }

        private static int FindLittleEndianFloatPairOffset(byte[] payload, int bytesWritten, float first, float second)
        {
            Assert.IsTrue(BitConverter.IsLittleEndian);
            byte[] firstBytes = BitConverter.GetBytes(first);
            byte[] secondBytes = BitConverter.GetBytes(second);
            int safeLength = Math.Clamp(bytesWritten, 0, payload != null ? payload.Length : 0);
            for (int i = 0; i <= safeLength - sizeof(float) * 2; i++)
            {
                if (payload[i] == firstBytes[0] &&
                    payload[i + 1] == firstBytes[1] &&
                    payload[i + 2] == firstBytes[2] &&
                    payload[i + 3] == firstBytes[3] &&
                    payload[i + 4] == secondBytes[0] &&
                    payload[i + 5] == secondBytes[1] &&
                    payload[i + 6] == secondBytes[2] &&
                    payload[i + 7] == secondBytes[3])
                {
                    return i;
                }
            }

            return -1;
        }

        private static int FindLittleEndianByteSequenceOffset(byte[] payload, int bytesWritten, byte[] marker)
        {
            Assert.IsTrue(BitConverter.IsLittleEndian);
            Assert.IsNotNull(marker);
            int safeLength = Math.Clamp(bytesWritten, 0, payload != null ? payload.Length : 0);
            for (int i = 0; i <= safeLength - marker.Length; i++)
            {
                if (ByteSequenceMatches(payload, i, marker))
                    return i;
            }

            return -1;
        }

        private static int PatchLittleEndianFloatSequence(
            byte[] payload,
            int bytesWritten,
            float[] marker,
            float[] replacement)
        {
            Assert.IsNotNull(marker);
            Assert.IsNotNull(replacement);
            Assert.AreEqual(marker.Length, replacement.Length);

            byte[] markerBytes = BuildLittleEndianFloatBytes(marker);
            byte[] replacementBytes = BuildLittleEndianFloatBytes(replacement);
            int safeLength = Math.Clamp(bytesWritten, 0, payload != null ? payload.Length : 0);
            int patchedCount = 0;

            for (int i = 0; i <= safeLength - markerBytes.Length; i++)
            {
                if (!ByteSequenceMatches(payload, i, markerBytes))
                    continue;

                Buffer.BlockCopy(replacementBytes, 0, payload, i, replacementBytes.Length);
                patchedCount++;
                i += markerBytes.Length - 1;
            }

            return patchedCount;
        }

        private static byte[] BuildLittleEndianFloatBytes(float[] values)
        {
            Assert.IsTrue(BitConverter.IsLittleEndian);
            byte[] bytes = new byte[values.Length * sizeof(float)];
            for (int i = 0; i < values.Length; i++)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(values[i]), 0, bytes, i * sizeof(float), sizeof(float));
            }

            return bytes;
        }

        private static byte[] BuildCurrentExplorationMapHeaderMarker(int exploredChunkCount, int maskByteCount)
        {
            Assert.IsTrue(BitConverter.IsLittleEndian);
            byte[] bytes = new byte[sizeof(int) * 7];
            int offset = 0;
            WritePayloadInt(bytes, ref offset, exploredChunkCount);
            WritePayloadInt(bytes, ref offset, ExplorationMapDTO.DenseChunkSizeMeters);
            WritePayloadInt(bytes, ref offset, ExplorationMapDTO.MortonMaskAxisBits);
            WritePayloadInt(bytes, ref offset, ExplorationMapDTO.MortonMaskOriginOffset);
            WritePayloadUInt(bytes, ref offset, SaveBinaryStorage.ExplorationMortonBuildSalt32);
            WritePayloadInt(bytes, ref offset, maskByteCount);
            WritePayloadInt(bytes, ref offset, maskByteCount);
            return bytes;
        }

        private static byte[] BuildWorldStateMarker(string depletedNodeId, long pickupChunkKey, long pickupWord)
        {
            Assert.IsTrue(BitConverter.IsLittleEndian);
            Assert.IsNotNull(depletedNodeId);
            byte[] bytes = new byte[
                sizeof(int) +
                EncodedStringArraySingleEntryBytes(depletedNodeId) +
                sizeof(int) +
                (int)EncodedStructArrayBytes<long>(1) +
                (int)EncodedStructArrayBytes<int>(1) +
                (int)EncodedStructArrayBytes<int>(1) +
                sizeof(int) +
                (int)EncodedStructArrayBytes<long>(1)];
            int offset = 0;
            WritePayloadInt(bytes, ref offset, 1);
            WritePayloadInt(bytes, ref offset, 1);
            WritePayloadString(bytes, ref offset, depletedNodeId);
            WritePayloadInt(bytes, ref offset, 1);
            WritePayloadInt(bytes, ref offset, 1);
            WritePayloadLong(bytes, ref offset, pickupChunkKey);
            WritePayloadInt(bytes, ref offset, 1);
            WritePayloadInt(bytes, ref offset, 0);
            WritePayloadInt(bytes, ref offset, 1);
            WritePayloadInt(bytes, ref offset, 1);
            WritePayloadInt(bytes, ref offset, 1);
            WritePayloadLong(bytes, ref offset, pickupWord);
            Assert.AreEqual(bytes.Length, offset);
            return bytes;
        }

        private static byte[] BuildEncryptedAudioLogFragmentsMarker(
            uint activeHash,
            uint staleHash,
            uint activeBits,
            uint staleBits)
        {
            Assert.IsTrue(BitConverter.IsLittleEndian);
            byte[] bytes = new byte[sizeof(int) * 3 + sizeof(uint) * 4];
            int offset = 0;
            WritePayloadInt(bytes, ref offset, 2);
            WritePayloadInt(bytes, ref offset, 2);
            WritePayloadUInt(bytes, ref offset, activeHash);
            WritePayloadUInt(bytes, ref offset, staleHash);
            WritePayloadInt(bytes, ref offset, 2);
            WritePayloadUInt(bytes, ref offset, activeBits);
            WritePayloadUInt(bytes, ref offset, staleBits);
            Assert.AreEqual(bytes.Length, offset);
            return bytes;
        }

        private static byte[] BuildVoxelDeltaDenseChunkHeaderMarker(
            int chunkCount,
            int totalCellCount,
            long chunkX,
            long chunkY,
            long chunkZ,
            float voxelSize,
            int dirtyMaskWordCount)
        {
            Assert.IsTrue(BitConverter.IsLittleEndian);
            byte[] bytes = new byte[(sizeof(int) * 3) + (sizeof(long) * 3) + sizeof(float) + sizeof(byte) + sizeof(ushort)];
            int offset = 0;
            WritePayloadInt(bytes, ref offset, chunkCount);
            WritePayloadInt(bytes, ref offset, totalCellCount);
            WritePayloadLong(bytes, ref offset, chunkX);
            WritePayloadLong(bytes, ref offset, chunkY);
            WritePayloadLong(bytes, ref offset, chunkZ);
            WritePayloadFloat(bytes, ref offset, voxelSize);
            WritePayloadByte(bytes, ref offset, VoxelDeltaChunkDTO.StorageDense);
            WritePayloadUShort(bytes, ref offset, 0);
            WritePayloadInt(bytes, ref offset, dirtyMaskWordCount);
            Assert.AreEqual(bytes.Length, offset);
            return bytes;
        }

        private static byte[] BuildAlignedVoxelDeltaNativeSnapshot(
            byte storageFlags,
            float voxelSize,
            int dirtyCellCount,
            int payloadByteLength,
            bool appendTrailingBytes,
            bool writeValidPayloadHash = false)
        {
            Assert.IsTrue(BitConverter.IsLittleEndian);
            const int snapshotMagic = unchecked((int)0x48584435);
            const int headerBytes = 16;
            const int chunkHeaderBytes = 40;
            int safePayloadByteLength = Math.Max(0, payloadByteLength);
            byte[] bytes = new byte[headerBytes + chunkHeaderBytes + safePayloadByteLength + (appendTrailingBytes ? sizeof(uint) : 0)];
            int offset = 0;
            WritePayloadInt(bytes, ref offset, snapshotMagic);
            WritePayloadInt(bytes, ref offset, 1);
            WritePayloadInt(bytes, ref offset, Math.Max(0, dirtyCellCount));
            WritePayloadInt(bytes, ref offset, 0);
            WritePayloadInt(bytes, ref offset, 0);
            WritePayloadInt(bytes, ref offset, 0);
            WritePayloadInt(bytes, ref offset, 0);
            WritePayloadFloat(bytes, ref offset, voxelSize);
            WritePayloadInt(bytes, ref offset, dirtyCellCount);
            WritePayloadByte(bytes, ref offset, storageFlags);
            WritePayloadByte(bytes, ref offset, 0);
            WritePayloadUShort(bytes, ref offset, 0);
            WritePayloadInt(bytes, ref offset, safePayloadByteLength);
            int payloadHashOffset = offset;
            WritePayloadUInt(bytes, ref offset, 0);
            WritePayloadUInt(bytes, ref offset, 0);
            WritePayloadUInt(bytes, ref offset, 0);
            offset += safePayloadByteLength;
            if (writeValidPayloadHash)
            {
                fixed (byte* bytesPtr = bytes)
                {
                    byte* payloadPtr = bytesPtr + headerBytes + chunkHeaderBytes;
                    ulong payloadHash = SaveBinaryStorage.Hash64(payloadPtr, safePayloadByteLength);
                    PatchPayloadLong(bytes, payloadHashOffset, unchecked((long)payloadHash));
                }
            }

            if (appendTrailingBytes)
                WritePayloadUInt(bytes, ref offset, 0xDEADC0DEu);

            Assert.AreEqual(bytes.Length, offset);
            return bytes;
        }

        private static byte[] BuildLegacyRleVoxelDeltaNativeSnapshot(
            byte storageFlags,
            float voxelSize,
            int dirtyCellCount,
            int declaredPayloadByteLength)
        {
            Assert.IsTrue(BitConverter.IsLittleEndian);
            const int snapshotMagic = unchecked((int)0x48584433);
            const int headerBytes = 12;
            const int chunkHeaderBytes = 28;
            byte[] bytes = new byte[headerBytes + chunkHeaderBytes];
            int offset = 0;
            WritePayloadInt(bytes, ref offset, snapshotMagic);
            WritePayloadInt(bytes, ref offset, 1);
            WritePayloadInt(bytes, ref offset, Math.Max(0, dirtyCellCount));
            WritePayloadInt(bytes, ref offset, 0);
            WritePayloadInt(bytes, ref offset, 0);
            WritePayloadInt(bytes, ref offset, 0);
            WritePayloadFloat(bytes, ref offset, voxelSize);
            WritePayloadInt(bytes, ref offset, dirtyCellCount);
            WritePayloadByte(bytes, ref offset, storageFlags);
            WritePayloadByte(bytes, ref offset, 0);
            WritePayloadUShort(bytes, ref offset, 0);
            WritePayloadInt(bytes, ref offset, declaredPayloadByteLength);
            Assert.AreEqual(bytes.Length, offset);
            return bytes;
        }

        private static void AssertVoxelDeltaNativeSnapshotRejected(byte[] snapshotBytes, string expectedError)
        {
            Assert.IsNotNull(snapshotBytes);
            UnityEngine.GameObject gameObject = new UnityEngine.GameObject("VoxelDeltaProcessorNativeSnapshotTest");
            Unity.Collections.NativeArray<byte> snapshot = new Unity.Collections.NativeArray<byte>(
                snapshotBytes.Length,
                Unity.Collections.Allocator.Temp,
                Unity.Collections.NativeArrayOptions.UninitializedMemory);
            try
            {
                for (int i = 0; i < snapshotBytes.Length; i++)
                    snapshot[i] = snapshotBytes[i];

                VoxelDeltaProcessor processor = gameObject.AddComponent<VoxelDeltaProcessor>();
                bool loaded = processor.TryLoadNativeSnapshot(snapshot, out string error);

                Assert.IsFalse(loaded);
                StringAssert.Contains(expectedError, error);
            }
            finally
            {
                if (snapshot.IsCreated)
                    snapshot.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static byte[] BuildProceduralWorldFaunaHeaderMarker(
            long suppressedKey,
            long faunaRuntimeKey,
            float cooldownUntilPlayTime)
        {
            Assert.IsTrue(BitConverter.IsLittleEndian);
            byte[] bytes = new byte[
                sizeof(int) +
                (int)EncodedStructArrayBytes<long>(1) +
                sizeof(int) +
                sizeof(int) +
                sizeof(long) +
                sizeof(float) +
                sizeof(byte) +
                sizeof(byte) +
                sizeof(byte) +
                sizeof(byte)];
            int offset = 0;
            WritePayloadInt(bytes, ref offset, 1);
            WritePayloadInt(bytes, ref offset, 1);
            WritePayloadLong(bytes, ref offset, suppressedKey);
            WritePayloadInt(bytes, ref offset, 1);
            WritePayloadInt(bytes, ref offset, 1);
            WritePayloadLong(bytes, ref offset, faunaRuntimeKey);
            WritePayloadFloat(bytes, ref offset, cooldownUntilPlayTime);
            WritePayloadBool(bytes, ref offset, false);
            WritePayloadBool(bytes, ref offset, true);
            WritePayloadByte(bytes, ref offset, 0);
            WritePayloadByte(bytes, ref offset, 0);
            Assert.AreEqual(bytes.Length, offset);
            return bytes;
        }

        private static void WritePayloadString(byte[] destination, ref int offset, string value)
        {
            Assert.IsNotNull(value);
            WritePayloadInt(destination, ref offset, value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                WritePayloadByte(destination, ref offset, (byte)character);
                WritePayloadByte(destination, ref offset, (byte)(character >> 8));
            }
        }

        private static byte[] BuildPayloadString(string value)
        {
            Assert.IsNotNull(value);
            byte[] bytes = new byte[sizeof(int) + (value.Length * sizeof(char))];
            int offset = 0;
            WritePayloadString(bytes, ref offset, value);
            Assert.AreEqual(bytes.Length, offset);
            return bytes;
        }

        private static byte[] BuildConstructionModuleHeaderMarker(string prefabId)
        {
            Assert.IsTrue(BitConverter.IsLittleEndian);
            Assert.IsNotNull(prefabId);
            byte[] bytes = new byte[(sizeof(int) * 3) + (prefabId.Length * sizeof(char))];
            int offset = 0;
            WritePayloadInt(bytes, ref offset, 1);
            WritePayloadInt(bytes, ref offset, 1);
            WritePayloadInt(bytes, ref offset, prefabId.Length);
            for (int i = 0; i < prefabId.Length; i++)
            {
                char value = prefabId[i];
                bytes[offset++] = (byte)value;
                bytes[offset++] = (byte)(value >> 8);
            }

            return bytes;
        }

        private static byte[] BuildFirstHourFloodStateMarker(
            int moduleHashId,
            float integrity,
            float repairIntegrityCap,
            float airReserveNormalized,
            float co2Normalized,
            float floodedReefFloodSeconds,
            byte flags,
            byte failureMode,
            byte health)
        {
            Assert.IsTrue(BitConverter.IsLittleEndian);
            byte[] bytes = new byte[sizeof(int) + 32];
            int offset = 0;
            WritePayloadInt(bytes, ref offset, 1);
            WritePayloadInt(bytes, ref offset, moduleHashId);
            WritePayloadFloat(bytes, ref offset, integrity);
            WritePayloadFloat(bytes, ref offset, repairIntegrityCap);
            WritePayloadFloat(bytes, ref offset, airReserveNormalized);
            WritePayloadFloat(bytes, ref offset, co2Normalized);
            WritePayloadFloat(bytes, ref offset, floodedReefFloodSeconds);
            WritePayloadByte(bytes, ref offset, flags);
            WritePayloadByte(bytes, ref offset, failureMode);
            WritePayloadByte(bytes, ref offset, health);
            WritePayloadByte(bytes, ref offset, 0);
            WritePayloadInt(bytes, ref offset, 0);
            return bytes;
        }

        private static byte[] BuildAtlas6LiabilityMarker(
            float sectorXenonOmegaYield,
            bool hasDisasterEvidence,
            uint[] workerTagHashes,
            float corporateHostilityIndex,
            float corporateCreditBalance,
            int extractionCarrierState,
            float biomatterExposureLevel,
            bool haldaneLockoutActive,
            float pressureSealIntegrity,
            bool bulkheadLocked)
        {
            Assert.IsTrue(BitConverter.IsLittleEndian);
            Assert.IsNotNull(workerTagHashes);
            byte[] bytes = new byte[
                (sizeof(float) * 5) +
                (sizeof(byte) * 3) +
                (sizeof(int) * 2) +
                (sizeof(uint) * workerTagHashes.Length)];
            int offset = 0;
            WritePayloadFloat(bytes, ref offset, sectorXenonOmegaYield);
            WritePayloadBool(bytes, ref offset, hasDisasterEvidence);
            WritePayloadInt(bytes, ref offset, workerTagHashes.Length);
            for (int i = 0; i < workerTagHashes.Length; i++)
                WritePayloadUInt(bytes, ref offset, workerTagHashes[i]);
            WritePayloadFloat(bytes, ref offset, corporateHostilityIndex);
            WritePayloadFloat(bytes, ref offset, corporateCreditBalance);
            WritePayloadInt(bytes, ref offset, extractionCarrierState);
            WritePayloadFloat(bytes, ref offset, biomatterExposureLevel);
            WritePayloadBool(bytes, ref offset, haldaneLockoutActive);
            WritePayloadFloat(bytes, ref offset, pressureSealIntegrity);
            WritePayloadBool(bytes, ref offset, bulkheadLocked);
            Assert.AreEqual(bytes.Length, offset);
            return bytes;
        }

        private static byte[] BuildEndingRootMarker(
            float firstHourSessionTime,
            int firstHourMilestones,
            int firstHourGuidanceFlags,
            int endingChoice,
            bool endingComplete,
            bool endingConditionMet)
        {
            Assert.IsTrue(BitConverter.IsLittleEndian);
            byte[] bytes = new byte[sizeof(float) + (sizeof(int) * 3) + (sizeof(byte) * 2)];
            int offset = 0;
            WritePayloadFloat(bytes, ref offset, firstHourSessionTime);
            WritePayloadInt(bytes, ref offset, firstHourMilestones);
            WritePayloadInt(bytes, ref offset, firstHourGuidanceFlags);
            WritePayloadInt(bytes, ref offset, endingChoice);
            WritePayloadBool(bytes, ref offset, endingComplete);
            WritePayloadBool(bytes, ref offset, endingConditionMet);
            Assert.AreEqual(bytes.Length, offset);
            return bytes;
        }

        private static byte[] RemoveCurrentDefaultVoxelDeltaPayload(
            byte[] source,
            int sourceLength,
            out int bytesWritten)
        {
            const int defaultVoxelDeltaPayloadBytes = sizeof(int) * 3;
            Assert.IsNotNull(source);
            Assert.GreaterOrEqual(sourceLength, defaultVoxelDeltaPayloadBytes);
            for (int i = sourceLength - defaultVoxelDeltaPayloadBytes; i < sourceLength; i++)
                Assert.AreEqual(0, source[i]);

            byte[] destination = new byte[sourceLength - defaultVoxelDeltaPayloadBytes];
            bytesWritten = RemovePayloadRange(
                source,
                sourceLength - defaultVoxelDeltaPayloadBytes,
                defaultVoxelDeltaPayloadBytes,
                sourceLength,
                destination);
            return destination;
        }

        private static int RemovePayloadRange(
            byte[] source,
            int offset,
            int byteCount,
            int sourceLength,
            byte[] destination)
        {
            Assert.IsNotNull(source);
            Assert.IsNotNull(destination);
            Assert.GreaterOrEqual(offset, 0);
            Assert.GreaterOrEqual(byteCount, 0);
            Assert.LessOrEqual(offset + byteCount, sourceLength);
            int destinationLength = sourceLength - byteCount;
            Assert.LessOrEqual(destinationLength, destination.Length);
            Buffer.BlockCopy(source, 0, destination, 0, offset);
            Buffer.BlockCopy(
                source,
                offset + byteCount,
                destination,
                offset,
                sourceLength - offset - byteCount);
            return destinationLength;
        }

        private static void PatchLittleEndianIntAtOffset(byte[] payload, int offset, int value)
        {
            Assert.IsTrue(BitConverter.IsLittleEndian);
            Assert.IsNotNull(payload);
            Assert.GreaterOrEqual(offset, 0);
            Assert.LessOrEqual(offset + sizeof(int), payload.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(value), 0, payload, offset, sizeof(int));
        }

        private static byte[] BuildLittleEndianRadiationGridHeader(
            float dose,
            double originX,
            double originY,
            double originZ,
            float cellSizeMeters)
        {
            Assert.IsTrue(BitConverter.IsLittleEndian);
            byte[] bytes = new byte[sizeof(float) + (sizeof(double) * 3) + sizeof(float)];
            int cursor = 0;
            Buffer.BlockCopy(BitConverter.GetBytes(dose), 0, bytes, cursor, sizeof(float));
            cursor += sizeof(float);
            Buffer.BlockCopy(BitConverter.GetBytes(originX), 0, bytes, cursor, sizeof(double));
            cursor += sizeof(double);
            Buffer.BlockCopy(BitConverter.GetBytes(originY), 0, bytes, cursor, sizeof(double));
            cursor += sizeof(double);
            Buffer.BlockCopy(BitConverter.GetBytes(originZ), 0, bytes, cursor, sizeof(double));
            cursor += sizeof(double);
            Buffer.BlockCopy(BitConverter.GetBytes(cellSizeMeters), 0, bytes, cursor, sizeof(float));
            return bytes;
        }

        private static bool ByteSequenceMatches(byte[] payload, int offset, byte[] marker)
        {
            if (payload == null || marker == null || offset < 0 || offset + marker.Length > payload.Length)
                return false;

            for (int i = 0; i < marker.Length; i++)
            {
                if (payload[offset + i] != marker[i])
                    return false;
            }

            return true;
        }
    }
}
