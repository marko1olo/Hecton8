#if UNITY_EDITOR || DEVELOPMENT_BUILD
// ============================================================================
// HECTON-8 - SaveRecoverySmokeTester.cs
// Dev-only runtime smoke for indexed-sector checksum/header failure and .sav.bak
// promotion through SaveManager.LoadGameAsync.
// ============================================================================

using System;
using System.IO;
using Hecton8.Core;
using Hecton8.SaveSystem;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Dev
{
    /// <summary>
    /// Creates isolated indexed saves, corrupts primary .sav recovery surfaces,
    /// then verifies SaveManager promotes slot.sav.bak to primary.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/Save Recovery Smoke Tester")]
    public sealed class SaveRecoverySmokeTester : MonoBehaviour
    {
        private const int FileHashBufferSize = 64 * 1024;
        private const ulong Fnv1A64Offset = 14695981039346656037UL;
        private const ulong Fnv1A64Prime = 1099511628211UL;

        [Header("References")]
        [SerializeField]
        [Tooltip("Save runtime owner. Auto-resolves from GlobalRegistry when empty.")]
        private SaveManager saveManager;

        [Header("Execution")]
        [SerializeField]
        [Tooltip("Runs the checksum/header failure .bak promotion smoke pass when Play Mode starts.")]
        private bool runOnStart;

        [SerializeField]
        [Tooltip("Isolated slot used for synthetic save recovery validation.")]
        private string recoverySlotName = "smoke_recovery_slot";

        [SerializeField, Min(0f)]
        [Tooltip("Realtime delay before starting the smoke pass.")]
        private float startupDelay = 0.75f;

        [SerializeField, Min(0.5f)]
        [Tooltip("Realtime timeout while waiting for SaveManager availability.")]
        private float operationTimeout = 15f;

        [SerializeField]
        [Tooltip("Deletes the smoke slot before the synthetic save is written.")]
        private bool cleanupBeforeRun = true;

        [SerializeField]
        [Tooltip("Deletes the smoke slot after recovery verification.")]
        private bool cleanupAfterRun = true;

        [SerializeField]
        [Tooltip("Also corrupts the save header magic so binary-container probe failures must recover from .sav.bak.")]
        private bool runHeaderMagicCorruptionPass = true;

#pragma warning disable CS0414
        [Header("Debug")]
        [SerializeField] private int _debugRunCount;
        [SerializeField] private string _debugLastPhase = "Idle";
        [SerializeField] private string _debugLastIssue = string.Empty;
        [SerializeField] private bool _debugRecoveryPass;
        [SerializeField] private long _debugCorruptedSectorHash;
        [SerializeField] private ulong _debugBackupHashBefore;
        [SerializeField] private ulong _debugPrimaryHashAfter;
        [SerializeField] private bool _debugCriticalRecoveryPromoted;
        [SerializeField] private bool _debugTempDeleted;
        [SerializeField] private int _debugScenarioPassCount;
        [SerializeField] private string _debugLastCorruptionMode = string.Empty;
#pragma warning restore CS0414

        private const int SyntheticChunkSizeMeters = 64;
        private const int SmokeRawPayloadCapacityBytes = 8 * 1024 * 1024;
        private const int SmokeCompressedPayloadCapacityBytes = 9 * 1024 * 1024;

        private bool _isRunning;

        private enum RecoveryCorruptionMode
        {
            ProtectedSectorChecksum = 0,
            HeaderMagic = 1
        }

        private void Awake()
        {
            AutoResolve();
        }

        private void Start()
        {
            if (runOnStart && !_isRunning)
                _ = RunRecoverySmokePassAsync();
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
            if (string.IsNullOrWhiteSpace(recoverySlotName))
                recoverySlotName = "smoke_recovery_slot";
        }
#endif

        [ContextMenu("Run Save Recovery Smoke Pass")]
        public void RunRecoveryFromContextMenu()
        {
            if (_isRunning)
                return;

            _ = RunRecoverySmokePassAsync();
        }

        private async Awaitable RunRecoverySmokePassAsync()
        {
            if (_isRunning)
                return;

            _isRunning = true;
            _debugRunCount++;
            _debugLastPhase = "Startup";
            _debugLastIssue = string.Empty;
            _debugRecoveryPass = false;
            _debugCorruptedSectorHash = 0L;
            _debugBackupHashBefore = 0UL;
            _debugPrimaryHashAfter = 0UL;
            _debugCriticalRecoveryPromoted = false;
            _debugTempDeleted = false;
            _debugScenarioPassCount = 0;
            _debugLastCorruptionMode = string.Empty;

            string currentSlot = string.IsNullOrWhiteSpace(recoverySlotName)
                ? "smoke_recovery_slot"
                : recoverySlotName.Trim();
            string headerMagicSlot = BuildHeaderMagicSlotName(currentSlot);

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
                    if (runHeaderMagicCorruptionPass)
                        saveManager.DeleteSave(headerMagicSlot);
                    await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: destroyCancellationToken);
                }

                if (!await RunRecoveryScenarioAsync(currentSlot, RecoveryCorruptionMode.ProtectedSectorChecksum))
                    return;

                if (runHeaderMagicCorruptionPass &&
                    !await RunRecoveryScenarioAsync(headerMagicSlot, RecoveryCorruptionMode.HeaderMagic))
                {
                    return;
                }

                _debugLastPhase = "Complete";
                _debugCriticalRecoveryPromoted = true;
                _debugRecoveryPass = true;
                Hecton8.Core.H8Debug.Log($"[SaveRecoverySmoke] PASS scenarios={_debugScenarioPassCount} slot={currentSlot}");
            }
            catch (OperationCanceledException)
            {
                FailRecovery("Save recovery smoke was cancelled.");
            }
            catch (Exception ex)
            {
                FailRecovery(ex.Message);
                Debug.LogException(ex);
            }
            finally
            {
                if (cleanupAfterRun && saveManager != null)
                {
                    _debugLastPhase = _debugRecoveryPass ? "CleanupAfter" : _debugLastPhase;
                    saveManager.DeleteSave(currentSlot);
                    if (runHeaderMagicCorruptionPass)
                        saveManager.DeleteSave(headerMagicSlot);
                }

                _isRunning = false;
            }
        }

        private async Awaitable<bool> RunRecoveryScenarioAsync(string slotName, RecoveryCorruptionMode corruptionMode)
        {
            string primaryAbsolutePath = HectonPersistentPathPolicy.CombineFile(SaveManager.GetPrimarySaveFilePath(slotName));
            string backupAbsolutePath = HectonPersistentPathPolicy.CombineFile(SaveManager.GetBackupSaveFilePath(slotName, 1));
            string tempAbsolutePath = HectonPersistentPathPolicy.CombineFile(SaveManager.GetTempSaveFilePath(slotName));

            _debugLastCorruptionMode = corruptionMode.ToString();
            _debugLastPhase = "WriteSyntheticIndexedSave";
            if (!TryWriteSyntheticIndexedSave(primaryAbsolutePath, slotName, out string writeError))
            {
                FailRecovery(writeError);
                return false;
            }

            File.Copy(primaryAbsolutePath, backupAbsolutePath, true);
            if (!TryComputeFileHash64(backupAbsolutePath, out ulong backupHashBefore, out string backupHashError))
            {
                FailRecovery(backupHashError);
                return false;
            }

            _debugBackupHashBefore = backupHashBefore;
            _debugLastPhase = $"CorruptPrimary:{corruptionMode}";
            if (!TryCorruptPrimarySave(primaryAbsolutePath, corruptionMode, out long sectorHash, out string corruptError))
            {
                FailRecovery(corruptError);
                return false;
            }

            _debugCorruptedSectorHash = sectorHash;
            if (!TryComputeFileHash64(primaryAbsolutePath, out ulong corruptedPrimaryHash, out string corruptedHashError))
            {
                FailRecovery(corruptedHashError);
                return false;
            }

            if (corruptedPrimaryHash == backupHashBefore)
            {
                FailRecovery($"Primary hash did not change after {corruptionMode} corruption.");
                return false;
            }

            _debugLastPhase = $"LoadAndPromoteBackup:{corruptionMode}";
            await saveManager.LoadGameAsync(slotName);
            if (!saveManager.LastOperationSucceeded)
            {
                FailRecovery($"Load failed after {corruptionMode} corruption: {saveManager.LastOperationError}");
                return false;
            }

            if (!saveManager.LastLoadUsedBackup || !saveManager.LastLoadSelfRepaired || saveManager.LastLoadBackupGeneration != 1)
            {
                FailRecovery(
                    $"Load did not report critical backup promotion after {corruptionMode}. usedBackup={saveManager.LastLoadUsedBackup} " +
                    $"selfRepaired={saveManager.LastLoadSelfRepaired} generation={saveManager.LastLoadBackupGeneration}");
                return false;
            }

            if (!TryComputeFileHash64(primaryAbsolutePath, out ulong primaryHashAfter, out string primaryHashError))
            {
                FailRecovery(primaryHashError);
                return false;
            }

            _debugPrimaryHashAfter = primaryHashAfter;
            _debugTempDeleted = !File.Exists(tempAbsolutePath);
            if (primaryHashAfter != backupHashBefore)
            {
                FailRecovery($"Promoted primary bytes do not match the validated .sav.bak source after {corruptionMode}.");
                return false;
            }

            if (!_debugTempDeleted)
            {
                FailRecovery($"Atomic promotion temp file still exists after {corruptionMode} recovery.");
                return false;
            }

            _debugScenarioPassCount++;
            Hecton8.Core.H8Debug.Log($"[SaveRecoverySmoke] PASS mode={corruptionMode} slot={slotName} sector=0x{sectorHash:X16}");
            return true;
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

            FailRecovery("SaveManager not found before save recovery smoke execution.");
            return false;
        }

        private async Awaitable DelayRealtimeAsync(float seconds)
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(0f, seconds);
            while (Time.realtimeSinceStartup < deadline)
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: destroyCancellationToken);
        }

        private static string BuildHeaderMagicSlotName(string slotName)
        {
            return $"{slotName}_header";
        }

        private static bool TryCorruptPrimarySave(
            string absolutePath,
            RecoveryCorruptionMode corruptionMode,
            out long sectorHash,
            out string error)
        {
            sectorHash = 0L;
            switch (corruptionMode)
            {
                case RecoveryCorruptionMode.ProtectedSectorChecksum:
                    return SaveBinaryStorage.TryCorruptFirstIndexedSectorBlockForSmoke(
                        absolutePath,
                        out sectorHash,
                        out error);
                case RecoveryCorruptionMode.HeaderMagic:
                    return TryCorruptSaveHeaderMagic(absolutePath, out error);
                default:
                    error = $"Unsupported recovery corruption mode {corruptionMode}.";
                    return false;
            }
        }

        private static bool TryCorruptSaveHeaderMagic(string absolutePath, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
            {
                error = "Header corruption target file is missing.";
                return false;
            }

            try
            {
                using (FileStream fileStream = new FileStream(absolutePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    if (fileStream.Length < sizeof(uint))
                    {
                        error = "Header corruption target is smaller than the magic prefix.";
                        return false;
                    }

                    Span<byte> firstByteBytes = stackalloc byte[1];
                    if (fileStream.Read(firstByteBytes) != firstByteBytes.Length)
                    {
                        error = "Header corruption target could not read the magic prefix.";
                        return false;
                    }

                    fileStream.Position = 0L;
                    fileStream.WriteByte((byte)(firstByteBytes[0] ^ 0x5A));
                    fileStream.Flush(true);
                    return true;
                }
            }
            catch (Exception ex)
            {
                error = $"Header magic corruption failed for '{absolutePath}': {ex.Message}";
                return false;
            }
        }

        private static bool TryWriteSyntheticIndexedSave(string absolutePath, string slotName, out string error)
        {
            error = string.Empty;
            SaveData data = SaveData.CreateNew(12.5d);
            SaveMetadata metadata = new SaveMetadata
            {
                SlotName = slotName,
                GameVersion = Application.version,
                Timestamp = DateTime.UtcNow.Ticks,
                PlayTimeSeconds = 12.5f,
                SceneName = "SaveRecoverySmoke",
                PlayerPosition = Vector3.zero
            };

            NativeArray<PersistentWorldDeltaRecord> persistentWorldDeltas = default;
            NativeArray<byte> rawBuffer = default;
            NativeArray<byte> compressedBuffer = default;
            try
            {
                persistentWorldDeltas = new NativeArray<PersistentWorldDeltaRecord>(1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                persistentWorldDeltas[0] = BuildSyntheticDeletedResourceNode();

                // COLD ALLOC: NativeArray<byte>[8388608] - synthetic save raw staging buffer for recovery smoke - owner: SaveRecoverySmokeTester
                rawBuffer = new NativeArray<byte>(SmokeRawPayloadCapacityBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                // COLD ALLOC: NativeArray<byte>[9437184] - synthetic save compressed staging buffer for recovery smoke - owner: SaveRecoverySmokeTester
                compressedBuffer = new NativeArray<byte>(SmokeCompressedPayloadCapacityBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

                return SaveBinaryStorage.TryWriteSaveFile(
                    absolutePath,
                    metadata,
                    data,
                    persistentWorldDeltas.AsReadOnly(),
                    default,
                    default,
                    default,
                    default,
                    rawBuffer,
                    compressedBuffer,
                    out _,
                    out _,
                    out error);
            }
            finally
            {
                if (compressedBuffer.IsCreated)
                    compressedBuffer.Dispose();
                if (rawBuffer.IsCreated)
                    rawBuffer.Dispose();
                if (persistentWorldDeltas.IsCreated)
                    persistentWorldDeltas.Dispose();
            }
        }

        private static PersistentWorldDeltaRecord BuildSyntheticDeletedResourceNode()
        {
            AbsoluteUniversePosition position = AbsoluteUniversePosition.FromAbsolutePosition(new double3(128d, -16d, 192d));
            int3 chunkId = AbsoluteUniversePosition.ResolveChunkId(in position, SyntheticChunkSizeMeters);
            PersistentWorldItemRecord record = new PersistentWorldItemRecord
            {
                Position = position,
                ChunkId = chunkId,
                ItemPersistentIdHash = 0x534D4F4B45524543UL,
                ItemPersistentId = default,
                Quantity = 0,
                Flags = PersistentWorldItemFlags.Deleted | PersistentWorldItemFlags.ResourceNodeDestroyed,
                InstanceUid = 0xFE000001u
            };

            return PersistentWorldDeltaRecord.CreateDeletedTombstone(in record, SyntheticChunkSizeMeters);
        }

        private static bool TryComputeFileHash64(string absolutePath, out ulong hash, out string error)
        {
            hash = Fnv1A64Offset;
            error = string.Empty;
            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
            {
                error = "Hash target file is missing.";
                return false;
            }

            try
            {
                using FileStream fileStream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (fileStream.Length <= 0L)
                {
                    error = "Hash target file is empty.";
                    return false;
                }

                byte[] buffer = new byte[FileHashBufferSize]; // COLD ALLOC: dev-only recovery smoke file hash buffer - owner: SaveRecoverySmokeTester
                int read;
                while ((read = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    for (int i = 0; i < read; i++)
                    {
                        hash ^= buffer[i];
                        hash *= Fnv1A64Prime;
                    }
                }

                if (hash == 0UL)
                    hash = 1UL;

                return true;
            }
            catch (Exception ex)
            {
                error = $"File hash failed for '{absolutePath}': {ex.Message}";
                return false;
            }
        }

        private void FailRecovery(string issue)
        {
            _debugRecoveryPass = false;
            _debugCriticalRecoveryPromoted = false;
            _debugLastIssue = string.IsNullOrEmpty(issue) ? "Unknown save recovery failure." : issue;
            _debugLastPhase = "Failed";
            Debug.LogWarning($"[SaveRecoverySmoke] FAIL {_debugLastIssue}");
        }

        private void AutoResolve()
        {
            if (saveManager == null)
                saveManager = GlobalRegistry.Save as SaveManager;
        }
    }
}
#endif
