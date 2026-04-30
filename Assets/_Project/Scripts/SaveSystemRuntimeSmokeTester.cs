using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Hecton8.SaveSystem;
using Hecton8.World;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Dev
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/Save System Runtime Smoke Tester")]
    public sealed class SaveSystemRuntimeSmokeTester : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SaveManager saveManager;

        [Header("Execution")]
        [SerializeField] private bool runOnStart = false;
        [SerializeField] private string slotName = "smoke_manual_slot";
        [SerializeField] private float startupDelay = 0.75f;
        [SerializeField] private float operationTimeout = 15f;
        [SerializeField] private float settleDelay = 0.15f;
        [SerializeField] private int savePasses = 3;
        [SerializeField] private bool cleanupBeforeRun = true;
        [SerializeField] private bool cleanupAfterRun = false;
        [SerializeField] private bool corruptPrimarySave = true;
        [SerializeField] private bool corruptPrimaryMetadata = false;
        [SerializeField] private int[] corruptBackupGenerations = { 1 };
        [SerializeField] private bool corruptBackupMetadata = false;
        [SerializeField] private bool verboseLogging = false;

        [Header("Indexed Sector Sub-Block")]
        [SerializeField] private string indexedSubBlockSlotName = "smoke_indexed_subblock_slot";
        [SerializeField] private bool cleanupIndexedSubBlockSlotAfterRun = true;

        // Inspector-only smoke diagnostics for save recovery validation.
#pragma warning disable CS0414
        [Header("Debug")]
        [SerializeField] private int _debugRunCount;
        [SerializeField] private string _debugLastPhase = "Idle";
        [SerializeField] private string _debugLastIssue = string.Empty;
        [SerializeField] private string _debugAuditBefore = string.Empty;
        [SerializeField] private string _debugAuditAfterCorruption = string.Empty;
        [SerializeField] private string _debugAuditAfterRepair = string.Empty;
        [SerializeField] private bool _debugLastPass;
        [SerializeField] private bool _debugLastLoadUsedBackup;
        [SerializeField] private int _debugLastLoadBackupGeneration;
        [SerializeField] private bool _debugLastLoadSelfRepaired;
        [SerializeField] private bool _debugIndexedSubBlockPass;
        [SerializeField] private string _debugIndexedSubBlockIssue = string.Empty;
        [SerializeField] private long _debugIndexedSubBlockSectorHash;
#pragma warning restore CS0414

        private bool _isRunning;

        private void Awake()
        {
            AutoResolve();
        }

        private void Start()
        {
            if (!runOnStart || _isRunning)
                return;

            StartCoroutine(RunSmokePass());
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            AutoResolve();
            if (string.IsNullOrWhiteSpace(slotName))
                slotName = "smoke_manual_slot";

            savePasses = Mathf.Clamp(savePasses, 1, 8);
        }
#endif

        [ContextMenu("Run Save System Runtime Smoke Pass")]
        public void RunFromContextMenu()
        {
            if (_isRunning)
                return;

            StartCoroutine(RunSmokePass());
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
            _debugLastPhase = "IndexedSubBlockStartup";
            _debugLastIssue = string.Empty;
            _debugIndexedSubBlockIssue = string.Empty;
            _debugIndexedSubBlockPass = false;
            _debugIndexedSubBlockSectorHash = 0L;

            string currentSlot = string.IsNullOrWhiteSpace(indexedSubBlockSlotName)
                ? "smoke_indexed_subblock_slot"
                : indexedSubBlockSlotName.Trim();

            try
            {
                if (startupDelay > 0f)
                    await DelayRealtimeAsync(startupDelay);

                _debugLastPhase = "IndexedSubBlockWaitForManager";
                if (!await WaitForSaveManagerAsync())
                    return;

                if (cleanupBeforeRun)
                {
                    _debugLastPhase = "IndexedSubBlockCleanupBefore";
                    saveManager.DeleteSave(currentSlot);
                    await Awaitable.NextFrameAsync(cancellationToken: destroyCancellationToken);
                }

                _debugLastPhase = "IndexedSubBlockSeedPrimary";
                await saveManager.SaveGameAsync(currentSlot);
                if (!saveManager.LastOperationSucceeded)
                {
                    FailIndexedSubBlock($"Primary seed save failed: {saveManager.LastOperationError}");
                    return;
                }

                _debugLastPhase = "IndexedSubBlockSeedBackup";
                await saveManager.SaveGameAsync(currentSlot);
                if (!saveManager.LastOperationSucceeded)
                {
                    FailIndexedSubBlock($"Backup seed save failed: {saveManager.LastOperationError}");
                    return;
                }

                string primaryRelativePath = SaveManager.GetPrimarySaveFilePath(currentSlot);
                string primaryAbsolutePath = Path.Combine(Application.persistentDataPath, primaryRelativePath);
                string indexedBackupAbsolutePath = $"{primaryAbsolutePath}.bak";
                if (!File.Exists(primaryAbsolutePath) || !File.Exists(indexedBackupAbsolutePath))
                {
                    FailIndexedSubBlock("Primary indexed save or .sav.bak mirror is missing after seed saves.");
                    return;
                }

                List<SaveBinaryStorage.IndexedSectorEntryInfo> sectorEntries = new List<SaveBinaryStorage.IndexedSectorEntryInfo>(16);
                if (!SaveBinaryStorage.TryReadIndexedPersistentWorldDirectory(primaryAbsolutePath, sectorEntries, out _, out string directoryError) ||
                    sectorEntries.Count <= 0)
                {
                    FailIndexedSubBlock($"No indexed persistent sectors available for sub-block fallback smoke: {directoryError}");
                    return;
                }

                if (!SaveBinaryStorage.TryCorruptFirstIndexedSectorProtectedBlockForSmoke(primaryAbsolutePath, out long sectorHash, out string corruptError))
                {
                    FailIndexedSubBlock(corruptError);
                    return;
                }

                _debugIndexedSubBlockSectorHash = sectorHash;

                if (!TryLoadIndexedSubBlockFallback(primaryAbsolutePath, sectorHash, out int restoredRecordCount, out string loadError))
                {
                    FailIndexedSubBlock($"Indexed sector fallback load failed: {loadError}");
                    return;
                }

                _debugLastPhase = "IndexedSubBlockComplete";
                _debugIndexedSubBlockPass = true;
                Debug.Log($"[SaveSmoke] Indexed sub-block fallback PASS slot={currentSlot} sector=0x{sectorHash:X16} records={restoredRecordCount}");
            }
            catch (OperationCanceledException)
            {
                FailIndexedSubBlock("Indexed sub-block fallback smoke was cancelled.");
            }
            finally
            {
                if (cleanupIndexedSubBlockSlotAfterRun && saveManager != null)
                {
                    _debugLastPhase = "IndexedSubBlockCleanupAfter";
                    saveManager.DeleteSave(currentSlot);
                }

                _isRunning = false;
            }
        }

        private static bool TryLoadIndexedSubBlockFallback(
            string primaryAbsolutePath,
            long sectorHash,
            out int restoredRecordCount,
            out string loadError)
        {
            restoredRecordCount = 0;
            loadError = string.Empty;
            NativeArray<long> requestedSectors = new NativeArray<long>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeList<PersistentWorldDeltaRecord> restoredRecords = new NativeList<PersistentWorldDeltaRecord>(16, Allocator.TempJob);
            try
            {
                requestedSectors[0] = sectorHash;
                if (!SaveBinaryStorage.TryLoadIndexedPersistentWorldSectors(primaryAbsolutePath, requestedSectors, restoredRecords, out loadError))
                    return false;

                restoredRecordCount = restoredRecords.Length;
                return true;
            }
            finally
            {
                if (requestedSectors.IsCreated)
                    requestedSectors.Dispose();
                if (restoredRecords.IsCreated)
                    restoredRecords.Dispose();
            }
        }
#endif

        private IEnumerator RunSmokePass()
        {
            if (_isRunning)
                yield break;

            _isRunning = true;
            _debugRunCount++;
            _debugLastPhase = "Startup";
            _debugLastIssue = string.Empty;
            _debugAuditBefore = string.Empty;
            _debugAuditAfterCorruption = string.Empty;
            _debugAuditAfterRepair = string.Empty;
            _debugLastPass = false;
            _debugLastLoadUsedBackup = false;
            _debugLastLoadBackupGeneration = 0;
            _debugLastLoadSelfRepaired = false;

            if (startupDelay > 0f)
                yield return new WaitForSecondsRealtime(startupDelay);

            _debugLastPhase = "WaitForManager";
            yield return WaitForSaveManager();
            if (!_isRunning)
                yield break;

            string currentSlot = string.IsNullOrWhiteSpace(slotName) ? "smoke_manual_slot" : slotName.Trim();
            int effectiveSavePasses = Mathf.Max(GetRequiredSavePassCount(), Mathf.Clamp(savePasses, 1, 8));

            try
            {
                if (cleanupBeforeRun)
                {
                    _debugLastPhase = "CleanupBefore";
                    LogVerbose($"Deleting existing artifacts for '{currentSlot}'.");
                    saveManager.DeleteSave(currentSlot);
                    yield return null;
                }

                _debugLastPhase = "SeedSaves";
                for (int passIndex = 0; passIndex < effectiveSavePasses; passIndex++)
                {
                    _debugLastPhase = $"SavePass{passIndex + 1}";
                    LogVerbose($"Creating save pass {passIndex + 1}/{effectiveSavePasses}.");
                    _ = saveManager.SaveGameAsync(currentSlot);
                    yield return WaitForManager(currentSlot, $"Save pass {passIndex + 1}");
                    if (!_isRunning)
                        yield break;

                    if (!saveManager.LastOperationSucceeded)
                    {
                        Fail($"Save pass {passIndex + 1} failed: {saveManager.LastOperationError}");
                        yield break;
                    }

                    if (settleDelay > 0f)
                        yield return new WaitForSecondsRealtime(settleDelay);
                }

                _debugLastPhase = "AuditBeforeCorruption";
                if (!SaveManager.TryAuditSaveSlotArtifacts(currentSlot, out SaveSlotAuditResult auditBefore))
                {
                    Fail("Initial audit failed.");
                    yield break;
                }

                _debugAuditBefore = auditBefore.Message ?? string.Empty;
                if (!auditBefore.SlotReadable)
                {
                    Fail("Initial audit reported unreadable slot.");
                    yield break;
                }

                _debugLastPhase = "CorruptArtifacts";
                if (corruptPrimarySave)
                    CorruptRelativePath(SaveManager.GetPrimarySaveFilePath(currentSlot));

                if (corruptPrimaryMetadata)
                    CorruptRelativePath(SaveMetadata.GetPrimaryMetadataPath(currentSlot));

                if (corruptBackupGenerations != null)
                {
                    for (int i = 0; i < corruptBackupGenerations.Length; i++)
                    {
                        int generation = corruptBackupGenerations[i];
                        if (generation <= 0)
                            continue;

                        CorruptRelativePath(SaveManager.GetBackupSaveFilePath(currentSlot, generation));
                        if (corruptBackupMetadata)
                            CorruptRelativePath(SaveMetadata.GetBackupMetadataPath(currentSlot, generation));
                    }
                }

                yield return null;

                _debugLastPhase = "AuditAfterCorruption";
                if (!SaveManager.TryAuditSaveSlotArtifacts(currentSlot, out SaveSlotAuditResult auditAfterCorruption))
                {
                    Fail("Audit after corruption failed.");
                    yield break;
                }

                _debugAuditAfterCorruption = auditAfterCorruption.Message ?? string.Empty;
                int expectedBackupGeneration = DetermineExpectedBackupGeneration(currentSlot);
                if (corruptPrimarySave && expectedBackupGeneration <= 0)
                {
                    Fail("No readable backup generation remained after corruption.");
                    yield break;
                }

                _debugLastPhase = "LoadRecovery";
                _ = saveManager.LoadGameAsync(currentSlot);
                yield return WaitForManager(currentSlot, "Load recovery");
                if (!_isRunning)
                    yield break;

                if (!saveManager.LastOperationSucceeded)
                {
                    Fail($"Load recovery failed: {saveManager.LastOperationError}");
                    yield break;
                }

                _debugLastLoadUsedBackup = saveManager.LastLoadUsedBackup;
                _debugLastLoadBackupGeneration = saveManager.LastLoadBackupGeneration;
                _debugLastLoadSelfRepaired = saveManager.LastLoadSelfRepaired;

                if (corruptPrimarySave)
                {
                    if (!saveManager.LastLoadUsedBackup)
                    {
                        Fail("Expected backup recovery, but load reported primary.");
                        yield break;
                    }

                    if (expectedBackupGeneration > 0 && saveManager.LastLoadBackupGeneration != expectedBackupGeneration)
                    {
                        Fail($"Expected backup g{expectedBackupGeneration}, got g{saveManager.LastLoadBackupGeneration}.");
                        yield break;
                    }

                    if (!saveManager.LastLoadSelfRepaired)
                    {
                        Fail("Expected self-repair after backup recovery.");
                        yield break;
                    }
                }

                _debugLastPhase = "AuditAfterRepair";
                if (!SaveManager.TryAuditSaveSlotArtifacts(currentSlot, out SaveSlotAuditResult auditAfterRepair))
                {
                    Fail("Audit after repair failed.");
                    yield break;
                }

                _debugAuditAfterRepair = auditAfterRepair.Message ?? string.Empty;
                if (!auditAfterRepair.SlotReadable)
                {
                    Fail("Slot is unreadable after recovery.");
                    yield break;
                }

                if (auditAfterRepair.SelectedBackupSource)
                {
                    Fail($"Post-repair audit still prefers backup g{auditAfterRepair.SelectedBackupGeneration}.");
                    yield break;
                }

                SaveSlotMaintenanceRecord maintenance = SaveSlotMaintenanceRecord.Load(currentSlot);
                if (maintenance == null)
                {
                    Fail("Maintenance record was not written.");
                    yield break;
                }

                if (corruptPrimarySave)
                {
                    if (!maintenance.LastLoadUsedBackup)
                    {
                        Fail("Maintenance record did not store backup usage.");
                        yield break;
                    }

                    if (maintenance.LastLoadBackupGeneration != expectedBackupGeneration)
                    {
                        Fail($"Maintenance record stored backup g{maintenance.LastLoadBackupGeneration}, expected g{expectedBackupGeneration}.");
                        yield break;
                    }

                    if (!maintenance.LastLoadSelfRepaired)
                    {
                        Fail("Maintenance record did not store self-repair.");
                        yield break;
                    }
                }

                _debugLastPhase = "Complete";
                _debugLastPass = true;
                Debug.Log(
                    $"[SaveSmoke] PASS slot={currentSlot} " +
                    $"backup={_debugLastLoadUsedBackup} generation={_debugLastLoadBackupGeneration} selfRepair={_debugLastLoadSelfRepaired}");
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

        private IEnumerator WaitForManager(string currentSlot, string label)
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(0.25f, operationTimeout);
            while (Time.realtimeSinceStartup < deadline)
            {
                if (saveManager == null)
                {
                    Fail($"{label} aborted because SaveManager disappeared.");
                    yield break;
                }

                if (!saveManager.IsBusy)
                    yield break;

                yield return null;
            }

            Fail($"{label} timed out for '{currentSlot}'.");
        }

        private IEnumerator WaitForSaveManager()
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(0.5f, operationTimeout);
            while (Time.realtimeSinceStartup < deadline)
            {
                AutoResolve();
                if (saveManager != null)
                    yield break;

                yield return null;
            }

            Fail("SaveManager not found before smoke execution.");
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private async Awaitable<bool> WaitForSaveManagerAsync()
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(0.5f, operationTimeout);
            while (Time.realtimeSinceStartup < deadline)
            {
                AutoResolve();
                if (saveManager != null)
                    return true;

                await Awaitable.NextFrameAsync(cancellationToken: destroyCancellationToken);
            }

            FailIndexedSubBlock("SaveManager not found before indexed sub-block smoke execution.");
            return false;
        }

        private async Awaitable DelayRealtimeAsync(float seconds)
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(0f, seconds);
            while (Time.realtimeSinceStartup < deadline)
                await Awaitable.NextFrameAsync(cancellationToken: destroyCancellationToken);
        }
#endif

        private int GetRequiredSavePassCount()
        {
            int highestBackupGeneration = 0;
            if (corruptBackupGenerations != null)
            {
                for (int i = 0; i < corruptBackupGenerations.Length; i++)
                    highestBackupGeneration = Mathf.Max(highestBackupGeneration, corruptBackupGenerations[i]);
            }

            if (!corruptPrimarySave)
                return Mathf.Max(1, highestBackupGeneration + 1);

            return Mathf.Max(2, highestBackupGeneration + 2);
        }

        private int DetermineExpectedBackupGeneration(string currentSlot)
        {
            for (int generation = 1; generation <= 8; generation++)
            {
                string backupPath = SaveManager.GetBackupSaveFilePath(currentSlot, generation);
                if (!File.Exists(Path.Combine(Application.persistentDataPath, backupPath)))
                    continue;

                if (IsGenerationMarkedCorrupted(generation))
                    continue;

                return generation;
            }

            return 0;
        }

        private bool IsGenerationMarkedCorrupted(int generation)
        {
            if (corruptBackupGenerations == null)
                return false;

            for (int i = 0; i < corruptBackupGenerations.Length; i++)
            {
                if (corruptBackupGenerations[i] == generation)
                    return true;
            }

            return false;
        }

        private void CorruptRelativePath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath) || !File.Exists(Path.Combine(Application.persistentDataPath, relativePath)))
            {
                LogVerbose($"Skip corruption, path missing: {relativePath}");
                return;
            }

            string absolutePath = Path.Combine(Application.persistentDataPath, relativePath);
            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(absolutePath, $"SAVE_SMOKE_CORRUPTED::{relativePath}::{Time.frameCount}");
            LogVerbose($"Corrupted {absolutePath}");
        }

        private void AutoResolve()
        {
            if (saveManager == null)
                saveManager = Hecton8.Core.GlobalRegistry.SaveRuntime != null ? Hecton8.Core.GlobalRegistry.SaveRuntime : Hecton8.Core.GlobalRegistry.SaveRuntime;
        }

        private void LogVerbose(string message)
        {
            if (verboseLogging)
                Debug.Log($"[SaveSmoke] {message}");
        }

        private void Fail(string issue)
        {
            _debugLastPass = false;
            _debugLastIssue = string.IsNullOrEmpty(issue) ? "Unknown failure." : issue;
            _debugLastPhase = "Failed";
            _isRunning = false;
            Debug.LogWarning($"[SaveSmoke] FAIL {_debugLastIssue}");
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void FailIndexedSubBlock(string issue)
        {
            _debugIndexedSubBlockPass = false;
            _debugIndexedSubBlockIssue = string.IsNullOrEmpty(issue) ? "Unknown indexed sub-block failure." : issue;
            _debugLastIssue = _debugIndexedSubBlockIssue;
            _debugLastPhase = "IndexedSubBlockFailed";
            Debug.LogWarning($"[SaveSmoke] Indexed sub-block fallback FAIL {_debugIndexedSubBlockIssue}");
        }
#endif
    }
}
