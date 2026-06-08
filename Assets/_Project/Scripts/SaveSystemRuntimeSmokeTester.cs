using System;
using System.Collections.Generic;
using System.IO;
using Hecton8.Core;
using Hecton8.SaveSystem;
using Hecton8.World;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Dev
{
    /// <summary>
    /// Development-only runtime verifier for indexed-sector protected sub-block recovery.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/Save System Runtime Smoke Tester")]
    public sealed class SaveSystemRuntimeSmokeTester : MonoBehaviour
    {
        private const string NativeMemoryOwner = nameof(SaveSystemRuntimeSmokeTester);
        private const string RequestedSectorScratchLabel = "indexedSubBlockSmokeRequestedSectors";
        private const string RestoredRecordsScratchLabel = "indexedSubBlockSmokeRestoredRecords";

        [Header("References")]
        [SerializeField]
        [Tooltip("Save runtime owner. Auto-resolves from GlobalRegistry when empty.")]
        private SaveManager saveManager;

        [Header("Execution")]
        [SerializeField]
        [Tooltip("Runs the indexed sub-block fallback smoke pass when Play Mode starts.")]
        private bool runOnStart;

        [SerializeField]
        [Tooltip("Temporary save slot used for sub-block fallback verification.")]
        private string indexedSubBlockSlotName = "smoke_indexed_subblock_slot";

        [SerializeField, Min(0f)]
        [Tooltip("Realtime delay before starting the smoke pass.")]
        private float startupDelay = 0.75f;

        [SerializeField, Min(0.5f)]
        [Tooltip("Realtime timeout while waiting for SaveManager availability.")]
        private float operationTimeout = 15f;

        [SerializeField]
        [Tooltip("Deletes the smoke slot before the test writes dummy indexed data.")]
        private bool cleanupBeforeRun = true;

        [SerializeField]
        [Tooltip("Deletes the smoke slot after the fallback check finishes.")]
        private bool cleanupAfterRun = true;

#pragma warning disable CS0414
        [Header("Debug")]
        [SerializeField]
        private int _debugRunCount;
        [SerializeField]
        private string _debugLastPhase = "Idle";
        [SerializeField]
        private string _debugLastIssue = string.Empty;
        [SerializeField]
        private bool _debugIndexedSubBlockPass;
        [SerializeField]
        private long _debugIndexedSubBlockSectorHash;
        [SerializeField]
        private int _debugRestoredRecordCount;
        [SerializeField]
        private bool _debugIndexedSubBlockBackupRecoveryReported;
#pragma warning restore CS0414

        private bool _isRunning;

        private void Awake()
        {
            AutoResolve();
        }

        private void Start()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (runOnStart && !_isRunning)
                _ = RunIndexedSubBlockFallbackSmokePassAsync();
#endif
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            AutoResolve();
            if (string.IsNullOrWhiteSpace(indexedSubBlockSlotName))
                indexedSubBlockSlotName = "smoke_indexed_subblock_slot";
        }
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Runs the indexed-sector protected sub-block corruption and backup fallback smoke pass.
        /// </summary>
        [ContextMenu("Run Indexed Sector Sub-Block Backup Smoke Pass")]
        public void RunIndexedSubBlockFallbackFromContextMenu()
        {
            if (_isRunning)
                return;

            _ = RunIndexedSubBlockFallbackSmokePassAsync();
        }

        private async Awaitable RunIndexedSubBlockFallbackSmokePassAsync()
        {
            if (_isRunning)
                return;

            _isRunning = true;
            _debugRunCount++;
            _debugLastPhase = "Startup";
            _debugLastIssue = string.Empty;
            _debugIndexedSubBlockPass = false;
            _debugIndexedSubBlockSectorHash = 0L;
            _debugRestoredRecordCount = 0;
            _debugIndexedSubBlockBackupRecoveryReported = false;

            string currentSlot = string.IsNullOrWhiteSpace(indexedSubBlockSlotName)
                ? "smoke_indexed_subblock_slot"
                : indexedSubBlockSlotName.Trim();

            try
            {
                if (startupDelay > 0f)
                    await DelayRealtimeAsync(startupDelay);

                _debugLastPhase = "WaitForManager";
                if (!await WaitForSaveManagerAsync())
                    return;

                if (cleanupBeforeRun)
                {
                    _debugLastPhase = "CleanupBefore";
                    saveManager.DeleteSave(currentSlot);
                    await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: destroyCancellationToken);
                }

                _debugLastPhase = "SeedPrimary";
                await saveManager.SaveGameAsync(currentSlot);
                if (!saveManager.LastOperationSucceeded)
                {
                    FailIndexedSubBlock($"Primary seed save failed: {saveManager.LastOperationError}");
                    return;
                }

                _debugLastPhase = "SeedBackup";
                await saveManager.SaveGameAsync(currentSlot);
                if (!saveManager.LastOperationSucceeded)
                {
                    FailIndexedSubBlock($"Backup seed save failed: {saveManager.LastOperationError}");
                    return;
                }

                string primaryAbsolutePath = HectonPersistentPathPolicy.CombineFile(SaveManager.GetPrimarySaveFilePath(currentSlot));
                string backupAbsolutePath = $"{primaryAbsolutePath}.bak";
                if (!File.Exists(primaryAbsolutePath) || !File.Exists(backupAbsolutePath))
                {
                    FailIndexedSubBlock("Primary indexed save or .sav.bak mirror is missing after dummy seed saves.");
                    return;
                }

                // COLD ALLOC: List<IndexedSectorEntryInfo>[16] — smoke-only sector directory readback — owner: SaveSystemRuntimeSmokeTester
                List<SaveBinaryStorage.IndexedSectorEntryInfo> sectorEntries = new List<SaveBinaryStorage.IndexedSectorEntryInfo>(16);
                if (!SaveBinaryStorage.TryReadIndexedPersistentWorldDirectory(
                        primaryAbsolutePath,
                        sectorEntries,
                        out int indexedChunkSizeMeters,
                        out string directoryError) ||
                    sectorEntries.Count <= 0)
                {
                    FailIndexedSubBlock($"No indexed persistent sectors available for sub-block fallback smoke: {directoryError}");
                    return;
                }

                _debugLastPhase = "CorruptPrimarySubBlock";
                if (!SaveBinaryStorage.TryCorruptFirstIndexedSectorBlockForSmoke(primaryAbsolutePath, out long sectorHash, out string corruptError))
                {
                    FailIndexedSubBlock(corruptError);
                    return;
                }

                _debugIndexedSubBlockSectorHash = sectorHash;
                _debugLastPhase = "LoadFallback";
                if (!TryLoadIndexedSubBlockFallback(
                        primaryAbsolutePath,
                        sectorHash,
                        indexedChunkSizeMeters,
                        out int restoredRecordCount,
                        out int repairedPrimaryRecordCount,
                        out _,
                        out int backupRecoveryHashCount,
                        out _,
                        out string loadError))
                {
                    FailIndexedSubBlock($"Indexed sector fallback load failed: {loadError}");
                    return;
                }

                _debugLastPhase = "Complete";
                _debugRestoredRecordCount = repairedPrimaryRecordCount;
                _debugIndexedSubBlockBackupRecoveryReported = true;
                _debugIndexedSubBlockPass = true;
                Hecton8.Core.H8Debug.Log($"[SaveSmoke] Indexed sub-block fallback PASS slot={currentSlot} sector=0x{sectorHash:X16} fallbackRecords={restoredRecordCount} repairedRecords={repairedPrimaryRecordCount} recoveredHashes={backupRecoveryHashCount}");
            }
            catch (OperationCanceledException)
            {
                FailIndexedSubBlock("Indexed sub-block fallback smoke was cancelled.");
            }
            finally
            {
                if (cleanupAfterRun && saveManager != null)
                {
                    _debugLastPhase = "CleanupAfter";
                    saveManager.DeleteSave(currentSlot);
                }

                _isRunning = false;
            }
        }

        private async Awaitable<bool> WaitForSaveManagerAsync()
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(0.5f, operationTimeout);
            while (Time.realtimeSinceStartup < deadline)
            {
                AutoResolve();
                if (saveManager != null)
                    return true;

                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: destroyCancellationToken);
            }

            FailIndexedSubBlock("SaveManager not found before indexed sub-block smoke execution.");
            return false;
        }

        private async Awaitable DelayRealtimeAsync(float seconds)
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(0f, seconds);
            while (Time.realtimeSinceStartup < deadline)
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: destroyCancellationToken);
        }

        private static bool TryLoadIndexedSubBlockFallback(
            string primaryAbsolutePath,
            long sectorHash,
            int indexedChunkSizeMeters,
            out int restoredRecordCount,
            out int repairedPrimaryRecordCount,
            out bool backupRecoveryReported,
            out int backupRecoveryHashCount,
            out bool backupRecoveryHashMatched,
            out string loadError)
        {
            restoredRecordCount = 0;
            repairedPrimaryRecordCount = 0;
            backupRecoveryReported = false;
            backupRecoveryHashCount = 0;
            backupRecoveryHashMatched = false;
            loadError = string.Empty;
            // COLD ALLOC: NativeArray<long>[1] - smoke-only requested sector hash scratch - owner: SaveSystemRuntimeSmokeTester
            NativeArray<long> requestedSectors = default;
            int requestedSectorsSentinelId = 0;
            // COLD ALLOC: NativeList<PersistentWorldDeltaRecord>[16+] - smoke-only restored records scratch - owner: SaveSystemRuntimeSmokeTester
            NativeList<PersistentWorldDeltaRecord> restoredRecords = default;
            int restoredRecordsSentinelId = 0;
            try
            {
                requestedSectors = AllocateTrackedTempJobArray<long>(
                    1,
                    RequestedSectorScratchLabel,
                    NativeArrayOptions.UninitializedMemory,
                    out requestedSectorsSentinelId);
                restoredRecords = AllocateTrackedTempJobList<PersistentWorldDeltaRecord>(
                    16,
                    RestoredRecordsScratchLabel,
                    out restoredRecordsSentinelId);

                requestedSectors[0] = sectorHash;
                if (!SaveBinaryStorage.TryLoadIndexedPersistentWorldSectors(primaryAbsolutePath, requestedSectors, restoredRecords, out loadError))
                    return false;

                restoredRecordCount = restoredRecords.Length;
                backupRecoveryReported = SaveBinaryStorage.ConsumeIndexedSectorBackupRecoveryFlag();
                if (backupRecoveryReported)
                {
                    long[] backupRecoveryScratch = new long[SaveBinaryStorage.IndexedSectorQuarantineHashCapacity];
                    backupRecoveryHashCount = SaveBinaryStorage.CopyAndClearIndexedSectorBackupRecoveryHashes(backupRecoveryScratch);
                    for (int i = 0; i < backupRecoveryHashCount; i++)
                    {
                        if (backupRecoveryScratch[i] == sectorHash)
                        {
                            backupRecoveryHashMatched = true;
                            break;
                        }
                    }
                }

                if (!backupRecoveryReported || backupRecoveryHashCount <= 0 || !backupRecoveryHashMatched)
                {
                    loadError = "Indexed sector fallback succeeded without backup-recovery telemetry.";
                    return false;
                }

                if (!SaveBinaryStorage.TryRestoreIndexedPersistentWorldSectorFromBackup(
                        primaryAbsolutePath,
                        sectorHash,
                        indexedChunkSizeMeters,
                        out string restoreError))
                {
                    loadError = $"Indexed sector fallback did not repair primary from backup: {restoreError}";
                    return false;
                }

                restoredRecords.Clear();
                if (!SaveBinaryStorage.TryLoadIndexedPersistentWorldSectors(primaryAbsolutePath, requestedSectors, restoredRecords, out loadError))
                    return false;

                repairedPrimaryRecordCount = restoredRecords.Length;
                bool unexpectedSecondRecovery = SaveBinaryStorage.ConsumeIndexedSectorBackupRecoveryFlag();
                if (unexpectedSecondRecovery)
                {
                    long[] unexpectedRecoveryScratch = new long[SaveBinaryStorage.IndexedSectorQuarantineHashCapacity];
                    _ = SaveBinaryStorage.CopyAndClearIndexedSectorBackupRecoveryHashes(unexpectedRecoveryScratch);
                    loadError = "Indexed sector primary reload still required backup recovery after repair.";
                    return false;
                }

                if (repairedPrimaryRecordCount != restoredRecordCount)
                {
                    loadError = $"Indexed sector repaired primary record count mismatch: fallback={restoredRecordCount} repaired={repairedPrimaryRecordCount}.";
                    return false;
                }

                return true;
            }
            finally
            {
                DisposeTrackedTempJobArray(ref requestedSectors, ref requestedSectorsSentinelId);
                DisposeTrackedTempJobList(ref restoredRecords, ref restoredRecordsSentinelId);
            }
        }

        private static NativeArray<T> AllocateTrackedTempJobArray<T>(
            int length,
            string label,
            NativeArrayOptions options,
            out int sentinelId)
            where T : struct
        {
            sentinelId = 0;
            NativeArray<T> array = new NativeArray<T>(length, Allocator.TempJob, options);
            try
            {
                sentinelId = NativeMemorySentinel.RegisterNativeArray(
                    array,
                    NativeMemoryOwner,
                    label,
                    NativeAllocationLifetime.TempJob);
                if (sentinelId > 0)
                    return array;
            }
            catch (Exception exception)
            {
                Exception cleanupException = null;
                try
                {
                    DisposeTrackedTempJobArray(ref array, ref sentinelId);
                }
                catch (Exception cleanupFault)
                {
                    cleanupException = cleanupFault;
                }

                if (cleanupException != null)
                    throw new AggregateException(
                        "Save system runtime TempJob NativeArray allocation failed and cleanup also failed.",
                        exception,
                        cleanupException);

                throw;
            }

            InvalidOperationException registrationException = new InvalidOperationException($"Native memory sentinel registration failed for {label}.");
            Exception registrationCleanupException = null;
            try
            {
                DisposeTrackedTempJobArray(ref array, ref sentinelId);
            }
            catch (Exception cleanupFault)
            {
                registrationCleanupException = cleanupFault;
            }

            if (registrationCleanupException != null)
                throw new AggregateException(
                    "Save system runtime TempJob NativeArray registration failed and cleanup also failed.",
                    registrationException,
                    registrationCleanupException);

            throw registrationException;
        }

        private static NativeList<T> AllocateTrackedTempJobList<T>(int capacity, string label, out int sentinelId)
            where T : unmanaged
        {
            sentinelId = 0;
            NativeList<T> list = new NativeList<T>(capacity, Allocator.TempJob);
            try
            {
                sentinelId = NativeMemorySentinel.RegisterNativeListInstance(
                    list,
                    NativeMemoryOwner,
                    label,
                    NativeAllocationLifetime.TempJob);
                if (sentinelId > 0)
                    return list;
            }
            catch (Exception exception)
            {
                Exception cleanupException = null;
                try
                {
                    DisposeTrackedTempJobList(ref list, ref sentinelId);
                }
                catch (Exception cleanupFault)
                {
                    cleanupException = cleanupFault;
                }

                if (cleanupException != null)
                    throw new AggregateException(
                        "Save system runtime TempJob NativeList allocation failed and cleanup also failed.",
                        exception,
                        cleanupException);

                throw;
            }

            InvalidOperationException registrationException = new InvalidOperationException($"Native memory sentinel registration failed for {label}.");
            Exception registrationCleanupException = null;
            try
            {
                DisposeTrackedTempJobList(ref list, ref sentinelId);
            }
            catch (Exception cleanupFault)
            {
                registrationCleanupException = cleanupFault;
            }

            if (registrationCleanupException != null)
                throw new AggregateException(
                    "Save system runtime TempJob NativeList registration failed and cleanup also failed.",
                    registrationException,
                    registrationCleanupException);

            throw registrationException;
        }

        private static void DisposeTrackedTempJobArray<T>(ref NativeArray<T> array, ref int sentinelId)
            where T : struct
        {
            Exception firstException = null;

            if (sentinelId > 0)
            {
                try
                {
                    NativeMemorySentinel.Unregister(sentinelId);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }
                finally
                {
                    sentinelId = 0;
                }
            }

            if (array.IsCreated)
            {
                try
                {
                    array.Dispose();
                }
                catch (Exception exception)
                {
                    if (firstException == null)
                        firstException = exception;
                }
                finally
                {
                    array = default;
                }
            }
            else
            {
                array = default;
            }

            if (firstException != null)
                throw firstException;
        }

        private static void DisposeTrackedTempJobList<T>(ref NativeList<T> list, ref int sentinelId)
            where T : unmanaged
        {
            Exception firstException = null;

            if (sentinelId > 0)
            {
                try
                {
                    NativeMemorySentinel.Unregister(sentinelId);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }
                finally
                {
                    sentinelId = 0;
                }
            }

            if (list.IsCreated)
            {
                try
                {
                    list.Dispose();
                }
                catch (Exception exception)
                {
                    if (firstException == null)
                        firstException = exception;
                }
                finally
                {
                    list = default;
                }
            }
            else
            {
                list = default;
            }

            if (firstException != null)
                throw firstException;
        }

        private void FailIndexedSubBlock(string issue)
        {
            _debugIndexedSubBlockPass = false;
            _debugLastIssue = string.IsNullOrEmpty(issue) ? "Unknown indexed sub-block failure." : issue;
            _debugLastPhase = "Failed";
            Hecton8.Core.H8Debug.LogWarning($"[SaveSmoke] Indexed sub-block fallback FAIL {_debugLastIssue}");
        }
#endif

        private void AutoResolve()
        {
            if (saveManager == null)
                saveManager = GlobalRegistry.Save as SaveManager;
        }
    }
}
