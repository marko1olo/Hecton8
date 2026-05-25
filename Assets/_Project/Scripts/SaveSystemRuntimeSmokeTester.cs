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
                if (!SaveBinaryStorage.TryReadIndexedPersistentWorldDirectory(primaryAbsolutePath, sectorEntries, out _, out string directoryError) ||
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
                if (!TryLoadIndexedSubBlockFallback(primaryAbsolutePath, sectorHash, out int restoredRecordCount, out string loadError))
                {
                    FailIndexedSubBlock($"Indexed sector fallback load failed: {loadError}");
                    return;
                }

                _debugLastPhase = "Complete";
                _debugRestoredRecordCount = restoredRecordCount;
                _debugIndexedSubBlockPass = true;
                Hecton8.Core.H8Debug.Log($"[SaveSmoke] Indexed sub-block fallback PASS slot={currentSlot} sector=0x{sectorHash:X16} records={restoredRecordCount}");
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
