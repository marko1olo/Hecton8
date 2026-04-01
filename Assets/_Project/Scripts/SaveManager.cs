// ============================================================================
// HECTON-8 — SaveManager.cs
// Менеджер сохранений. Singleton, DontDestroyOnLoad.
//
// АРХИТЕКТУРА:
//   • Реестр ISaveable вместо FindObjectsByType (zero GC при save/load).
//   • CRC32 Checksums для проверки целостности (Master Grade).
//   • ES3 для сериализации (binary + zero-GC async).
//   • Unity 6 Awaitable API: BackgroundThreadAsync / MainThreadAsync.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Hecton8.SaveSystem
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-8000)]
    public sealed class SaveManager : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════
        //  CRC32 LOGIC (Zero-GC)
        // ══════════════════════════════════════════════════════════

        private const uint CrcPolynomial = 0xEDB88320;
        private static readonly uint[] _crcTable = GenerateCrcTable();

        private static uint[] GenerateCrcTable()
        {
            uint[] table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint entry = i;
                for (int j = 0; j < 8; j++)
                {
                    if ((entry & 1) == 1)
                        entry = (entry >> 1) ^ CrcPolynomial;
                    else
                        entry >>= 1;
                }
                table[i] = entry;
            }
            return table;
        }

        private static uint CalculateCRC32(byte[] data)
        {
            uint crc = 0xFFFFFFFF;
            if (data == null) return 0;
            for (int i = 0; i < data.Length; i++)
            {
                byte b = data[i];
                crc = (crc >> 8) ^ _crcTable[(crc ^ b) & 0xFF];
            }
            return ~crc;
        }

        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        private static SaveManager _instance;
        public static SaveManager Instance => _instance;
        public bool IsBusy => _isBusy;
        public float CurrentPlayTimeSeconds => _totalPlayTime + (Time.realtimeSinceStartup - _sessionStartTime);
        public bool LastOperationSucceeded { get; private set; }
        public string LastOperationError { get; private set; }
        public string LastOperationSlot { get; private set; }
        public bool LastLoadUsedBackup { get; private set; }
        public int LastLoadBackupGeneration { get; private set; }
        public bool LastLoadSelfRepaired { get; private set; }
        public bool LastLoadUsedLegacyCompression { get; private set; }

        private const int DefaultManualBackupGenerations = 3;
        private const int DefaultAutoBackupGenerations = 2;
        private const int DefaultQuickBackupGenerations = 2;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Settings ──────────────────────────────────")]
        [SerializeField] private string saveKeyPrefix = "save_";
        [SerializeField] private bool useCompression = true;

        [Header("── Backup Policy ─────────────────────────────")]
        [SerializeField] private int manualBackupGenerations = DefaultManualBackupGenerations;
        [SerializeField] private int autoBackupGenerations = DefaultAutoBackupGenerations;
        [SerializeField] private int quickBackupGenerations = DefaultQuickBackupGenerations;

        [Header("── Diagnostics ───────────────────────────────")]
        [SerializeField] private bool verboseLogging;
        [SerializeField] private int _debugRegisteredCount;

        private readonly List<ISaveable> _saveables = new List<ISaveable>(16);
        private bool _registryDirty;
        private float _sessionStartTime;
        private float _totalPlayTime;
        private bool _isBusy;

        private static readonly Comparison<ISaveable> SavePriorityCompare = (a, b) => a.SavePriority.CompareTo(b.SavePriority);
        private static readonly Comparison<ISaveable> LoadPriorityCompare = (a, b) => a.LoadPriority.CompareTo(b.LoadPriority);

        private ES3Settings _cachedSettings;

        private readonly struct SaveLoadCandidate
        {
            public readonly string SavePath;
            public readonly string MetadataPath;
            public readonly bool IsBackup;
            public readonly bool MetadataMatchesSave;
            public readonly int BackupGeneration;

            public SaveLoadCandidate(string savePath, string metadataPath, bool isBackup, bool metadataMatchesSave, int backupGeneration)
            {
                SavePath = savePath;
                MetadataPath = metadataPath;
                IsBackup = isBackup;
                MetadataMatchesSave = metadataMatchesSave;
                BackupGeneration = backupGeneration;
            }
        }

        private enum SaveSlotCategory
        {
            Manual = 0,
            Auto,
            Quick
        }

        private ES3Settings GetBaseSettings()
        {
            if (_cachedSettings == null)
            {
                _cachedSettings = new ES3Settings
                {
                    encryptionType = ES3.EncryptionType.None,
                    compressionType = useCompression ? ES3.CompressionType.Gzip : ES3.CompressionType.None
                };
            }
            return _cachedSettings;
        }

        private ES3Settings GetSlotSettings(string slotName)
        {
            return new ES3Settings(GetPrimarySaveFilePath(slotName), GetBaseSettings());
        }

        private ES3Settings GetPathSettings(string path)
        {
            return new ES3Settings(path, GetBaseSettings());
        }

        private int GetBackupRetentionCount(string slotName)
        {
            return GetBackupRetentionCountStatic(slotName);
        }

        private static int GetBackupRetentionCountStatic(string slotName)
        {
            SaveSlotCategory category = ClassifySlot(slotName);
            if (Instance != null)
            {
                switch (category)
                {
                    case SaveSlotCategory.Auto:
                        return Mathf.Clamp(Instance.autoBackupGenerations, 1, 8);
                    case SaveSlotCategory.Quick:
                        return Mathf.Clamp(Instance.quickBackupGenerations, 1, 8);
                    default:
                        return Mathf.Clamp(Instance.manualBackupGenerations, 1, 8);
                }
            }

            switch (category)
            {
                case SaveSlotCategory.Auto:
                    return DefaultAutoBackupGenerations;
                case SaveSlotCategory.Quick:
                    return DefaultQuickBackupGenerations;
                default:
                    return DefaultManualBackupGenerations;
            }
        }

        private static int GetMaxBackupGenerationCount()
        {
            if (Instance != null)
            {
                return Mathf.Clamp(
                    Mathf.Max(Instance.manualBackupGenerations, Mathf.Max(Instance.autoBackupGenerations, Instance.quickBackupGenerations)),
                    1,
                    8);
            }

            return Mathf.Max(DefaultManualBackupGenerations, Mathf.Max(DefaultAutoBackupGenerations, DefaultQuickBackupGenerations));
        }

        private static SaveSlotCategory ClassifySlot(string slotName)
        {
            if (string.IsNullOrEmpty(slotName))
                return SaveSlotCategory.Manual;

            if (slotName.IndexOf("auto", StringComparison.OrdinalIgnoreCase) >= 0)
                return SaveSlotCategory.Auto;

            if (slotName.IndexOf("quick", StringComparison.OrdinalIgnoreCase) >= 0)
                return SaveSlotCategory.Quick;

            return SaveSlotCategory.Manual;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            _sessionStartTime = Time.realtimeSinceStartup;
        }

        public void Register(ISaveable saveable)
        {
            if (saveable == null) return;
            if (!_saveables.Contains(saveable)) { _saveables.Add(saveable); _registryDirty = true; }
            _debugRegisteredCount = _saveables.Count;
        }

        public void Unregister(ISaveable saveable)
        {
            if (saveable == null) return;
            if (_saveables.Remove(saveable)) _registryDirty = true;
            _debugRegisteredCount = _saveables.Count;
        }

        private void SortRegistryIfDirty(Comparison<ISaveable> comparison)
        {
            if (!_registryDirty) return;
            _saveables.Sort(comparison);
            _registryDirty = false;
        }

        private static bool IsAlive(ISaveable saveable)
        {
            if (saveable == null) return false;
            if (saveable is UnityEngine.Object unityObj) return unityObj != null;
            return true;
        }

        // ══════════════════════════════════════════════════════════
        //  ASYNC SAVE/LOAD
        // ══════════════════════════════════════════════════════════

        public async Awaitable SaveGameAsync(string slotName)
        {
            LastOperationSucceeded = false;
            LastOperationError = string.Empty;
            LastOperationSlot = slotName;
            LastLoadUsedBackup = false;
            LastLoadBackupGeneration = 0;
            LastLoadSelfRepaired = false;
            LastLoadUsedLegacyCompression = false;

            if (_isBusy)
            {
                const string reason = "Save already in progress.";
                LastOperationError = reason;
                Debug.LogWarning($"[SaveManager] Ignored save request for '{slotName}': {reason}");
                SaveEvents.RaiseSaveFailed(slotName, reason);
                return;
            }

            if (string.IsNullOrEmpty(slotName))
            {
                const string reason = "Slot name is empty.";
                LastOperationError = reason;
                Debug.LogWarning($"[SaveManager] Ignored save request: {reason}");
                SaveEvents.RaiseSaveFailed(slotName, reason);
                return;
            }

            _isBusy = true;
            SaveEvents.RaiseSaveStarted(slotName);

            var totalTimer = Stopwatch.StartNew();
            float playTime = _totalPlayTime + (Time.realtimeSinceStartup - _sessionStartTime);
            SaveData data = SaveData.CreateNew(playTime);

            try
            {
                SortRegistryIfDirty(SavePriorityCompare);
                for (int i = 0; i < _saveables.Count; i++)
                {
                    if (IsAlive(_saveables[i])) _saveables[i].PopulateSaveData(data);
                }

                SaveMetadata metadata = new SaveMetadata
                {
                    SlotName = slotName,
                    GameVersion = Application.version,
                    Timestamp = DateTime.UtcNow.Ticks,
                    PlayTimeSeconds = playTime,
                    SceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                    PlayerPosition = data.playerStats.GetPosition()
                };

                string key = GetSaveKey(slotName);
                string primaryPath = GetPrimarySaveFilePath(slotName);
                string tempPath = GetTempSaveFilePath(slotName);
                string primaryMetadataPath = SaveMetadata.GetPrimaryMetadataPath(slotName);
                string tempMetadataPath = SaveMetadata.GetTempMetadataPath(slotName);
                int backupRetention = GetBackupRetentionCount(slotName);

                await Awaitable.BackgroundThreadAsync();

                DeleteFileIfExists(tempPath);
                DeleteFileIfExists(tempMetadataPath);

                ES3Settings tempSaveSettings = GetPathSettings(tempPath);
                ES3.Save(key, data, tempSaveSettings);

                byte[] bytes = ES3.Serialize(data, tempSaveSettings);
                metadata.Checksum = CalculateCRC32(bytes).ToString("X8");

                RotateBackupChain(primaryPath, generation => GetBackupSaveFilePath(slotName, generation), backupRetention);
                PromoteFile(tempPath, primaryPath);

                RotateBackupChain(primaryMetadataPath, generation => SaveMetadata.GetBackupMetadataPath(slotName, generation), backupRetention);
                metadata.Save(tempMetadataPath);
                PromoteFile(tempMetadataPath, primaryMetadataPath);

                await Awaitable.MainThreadAsync();
                SaveThumbnailSystem.CaptureThumbnail(slotName);
                SaveSlotIntegrityState savedIntegrity = backupRetention > 0
                    ? SaveSlotIntegrityState.HealthyWithBackup
                    : SaveSlotIntegrityState.Healthy;
                RecordSuccessfulSave(slotName, data.version, savedIntegrity);

                LastOperationSucceeded = true;
                Debug.Log($"[SaveManager] Saved '{slotName}' (CRC: {metadata.Checksum}) in {totalTimer.ElapsedMilliseconds}ms");
                SaveEvents.RaiseSaveCompleted(slotName);
            }
            catch (Exception ex)
            {
                RecordFailure(slotName, "save", ex.Message);
                LastOperationError = ex.Message;
                Debug.LogError($"[SaveManager] Save failed: {ex.Message}");
                SaveEvents.RaiseSaveFailed(slotName, ex.Message);
            }
            finally { _isBusy = false; }
        }

        public async Awaitable LoadGameAsync(string slotName)
        {
            LastOperationSucceeded = false;
            LastOperationError = string.Empty;
            LastOperationSlot = slotName;
            LastLoadUsedBackup = false;
            LastLoadBackupGeneration = 0;
            LastLoadSelfRepaired = false;
            LastLoadUsedLegacyCompression = false;

            if (_isBusy)
            {
                const string reason = "Load already in progress.";
                LastOperationError = reason;
                Debug.LogWarning($"[SaveManager] Ignored load request for '{slotName}': {reason}");
                SaveEvents.RaiseLoadFailed(slotName, reason);
                return;
            }

            if (string.IsNullOrEmpty(slotName))
            {
                const string reason = "Slot name is empty.";
                LastOperationError = reason;
                Debug.LogWarning($"[SaveManager] Ignored load request: {reason}");
                SaveEvents.RaiseLoadFailed(slotName, reason);
                return;
            }

            if (!SaveExists(slotName))
            {
                string reason = $"No primary or backup save found for '{slotName}'.";
                LastOperationError = reason;
                Debug.LogWarning($"[SaveManager] {reason}");
                SaveEvents.RaiseLoadFailed(slotName, reason);
                return;
            }

            _isBusy = true;
            SaveEvents.RaiseLoadStarted(slotName);
            var totalTimer = Stopwatch.StartNew();

            try
            {
                await Awaitable.BackgroundThreadAsync();
                SaveData data = null;
                SaveLoadCandidate loadedCandidate = default;
                Exception lastError = null;
                string key = GetSaveKey(slotName);
                List<SaveLoadCandidate> candidates = BuildLoadCandidates(slotName);
                ES3.CompressionType resolvedCompression = GetPreferredCompressionType();

                for (int i = 0; i < candidates.Count; i++)
                {
                    SaveLoadCandidate candidate = candidates[i];
                    if (TryLoadCandidateWithAnyCompression(
                        key,
                        candidate,
                        out SaveData candidateData,
                        out SaveMetadata candidateMetadata,
                        out ES3.CompressionType candidateCompression,
                        out string candidateError))
                    {
                        data = candidateData;
                        loadedCandidate = candidate;
                        resolvedCompression = candidateCompression;
                        break;
                    }

                    lastError = new Exception(candidateError);
                    string candidateLabel = candidate.IsBackup
                        ? $"backup g{candidate.BackupGeneration}"
                        : "primary";
                    Debug.LogWarning($"[SaveManager] Failed to load {candidateLabel} for '{slotName}': {candidateError}");
                }

                if (data == null)
                    throw lastError ?? new Exception("No load candidate could be restored.");

                await Awaitable.MainThreadAsync();

                if (SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary))
                {
                    Debug.Log($"[SaveManager] Migrated save '{slotName}' from v{originalVersion}: {summary}");
                }

                _totalPlayTime = data.totalPlayTime;
                _sessionStartTime = Time.realtimeSinceStartup;
                
                _registryDirty = true;
                SortRegistryIfDirty(LoadPriorityCompare);

                for (int i = 0; i < _saveables.Count; i++)
                {
                    if (IsAlive(_saveables[i])) _saveables[i].LoadFromSaveData(data);
                }

                string activeSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                Vector3 playerPosition = data.playerStats.GetPosition();
                bool repairedPrimaryArtifacts = false;

                if (ShouldSelfRepairSlot(slotName, loadedCandidate))
                {
                    await Awaitable.BackgroundThreadAsync();
                    repairedPrimaryArtifacts = SelfRepairPrimaryArtifacts(slotName, key, data, activeSceneName, playerPosition);
                    await Awaitable.MainThreadAsync();
                }

                string sourceLabel = loadedCandidate.IsBackup
                    ? $"backup g{loadedCandidate.BackupGeneration}"
                    : "primary";
                LastLoadUsedBackup = loadedCandidate.IsBackup;
                LastLoadBackupGeneration = loadedCandidate.BackupGeneration;
                LastLoadSelfRepaired = repairedPrimaryArtifacts;
                LastLoadUsedLegacyCompression = resolvedCompression != GetPreferredCompressionType();
                SaveSlotInfo postLoadInfo = BuildSaveSlotInfoInternal(slotName);
                SaveSlotIntegrityState postLoadIntegrity = postLoadInfo != null ? postLoadInfo.IntegrityState : SaveSlotIntegrityState.Empty;
                RecordSuccessfulLoad(slotName, data.version, postLoadIntegrity, LastLoadUsedBackup, LastLoadBackupGeneration, LastLoadUsedLegacyCompression, LastLoadSelfRepaired);
                LastOperationSucceeded = true;
                Debug.Log($"[SaveManager] Loaded '{slotName}' from {sourceLabel} in {totalTimer.ElapsedMilliseconds}ms" +
                          (repairedPrimaryArtifacts ? " and self-repaired primary artifacts." : "."));
                SaveEvents.RaiseLoadCompleted(slotName);
            }
            catch (Exception ex)
            {
                RecordFailure(slotName, "load", ex.Message);
                LastOperationError = ex.Message;
                Debug.LogError($"[SaveManager] Load failed: {ex.Message}");
                SaveEvents.RaiseLoadFailed(slotName, ex.Message);
            }
            finally { _isBusy = false; }
        }

        public bool SaveExists(string slotName)
        {
            if (string.IsNullOrEmpty(slotName))
                return false;

            if (FileExists(GetPrimarySaveFilePath(slotName)))
                return true;

            int backupRetention = GetBackupRetentionCountStatic(slotName);
            for (int generation = 1; generation <= backupRetention; generation++)
            {
                if (FileExists(GetBackupSaveFilePath(slotName, generation)))
                    return true;
            }

            return false;
        }

        public bool TryGetSaveMetadata(string slotName, out SaveMetadata metadata)
        {
            metadata = null;
            if (!TryGetSaveSlotInfo(slotName, out SaveSlotInfo info))
                return false;

            metadata = info.Metadata;
            return metadata != null && info.HasAnySaveData;
        }

        public void GetAvailableSaveSlots(List<SaveMetadata> results)
        {
            if (results == null)
                return;

            results.Clear();
            List<SaveSlotInfo> infos = new List<SaveSlotInfo>();
            GetAvailableSaveSlotInfos(infos);
            for (int i = 0; i < infos.Count; i++)
            {
                if (infos[i].Metadata != null)
                    results.Add(infos[i].Metadata);
            }
        }

        public bool TryGetSaveSlotInfo(string slotName, out SaveSlotInfo info)
        {
            info = null;
            if (string.IsNullOrEmpty(slotName))
                return false;

            info = BuildSaveSlotInfo(slotName);
            return info != null && info.HasAnySaveData;
        }

        public void GetAvailableSaveSlotInfos(List<SaveSlotInfo> results)
        {
            CollectAvailableSaveSlotInfos(results);
        }

        public bool TryRepairSaveSlot(string slotName, out SaveSlotRepairResult result)
        {
            return TryRepairSaveSlotInternal(slotName, out result);
        }

        public static bool TryRepairSaveSlotArtifacts(string slotName, out SaveSlotRepairResult result)
        {
            return TryRepairSaveSlotInternal(slotName, out result);
        }

        public static void CollectRepairResults(List<SaveSlotRepairResult> results)
        {
            if (results == null)
                return;

            results.Clear();

            List<SaveSlotInfo> slots = new List<SaveSlotInfo>();
            CollectAvailableSaveSlotInfos(slots);
            for (int i = 0; i < slots.Count; i++)
            {
                if (TryRepairSaveSlotInternal(slots[i].slotName, out SaveSlotRepairResult result))
                {
                    results.Add(result);
                }
            }
        }

        public bool TryAuditSaveSlot(string slotName, out SaveSlotAuditResult result)
        {
            return TryAuditSaveSlotInternal(slotName, out result);
        }

        public static bool TryAuditSaveSlotArtifacts(string slotName, out SaveSlotAuditResult result)
        {
            return TryAuditSaveSlotInternal(slotName, out result);
        }

        public static void CollectAuditResults(List<SaveSlotAuditResult> results)
        {
            if (results == null)
                return;

            results.Clear();

            List<SaveSlotInfo> slots = new List<SaveSlotInfo>();
            CollectAvailableSaveSlotInfos(slots);
            for (int i = 0; i < slots.Count; i++)
            {
                if (TryAuditSaveSlotInternal(slots[i].slotName, out SaveSlotAuditResult result))
                    results.Add(result);
            }
        }

        public static void CollectAvailableSaveSlotInfos(List<SaveSlotInfo> results)
        {
            if (results == null)
                return;

            results.Clear();

            string persistentPath = Application.persistentDataPath;
            if (!Directory.Exists(persistentPath))
                return;

            string[] files = Directory.GetFiles(persistentPath);
            HashSet<string> slotNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < files.Length; i++)
            {
                string fileName = Path.GetFileName(files[i]);
                if (TryExtractSlotName(fileName, out string slotName))
                    slotNames.Add(slotName);
            }

            SaveManager manager = Instance;
            foreach (string slotName in slotNames)
            {
                SaveSlotInfo info = manager != null ? manager.BuildSaveSlotInfo(slotName) : BuildSaveSlotInfoStatic(slotName);
                if (info != null && info.HasAnySaveData)
                    results.Add(info);
            }

            results.Sort((a, b) =>
            {
                long left = a != null && a.Metadata != null ? a.Metadata.Timestamp : a.LastWriteTicksUtc;
                long right = b != null && b.Metadata != null ? b.Metadata.Timestamp : b.LastWriteTicksUtc;
                return right.CompareTo(left);
            });
        }
        
        public void DeleteSave(string slotName)
        {
            if (string.IsNullOrEmpty(slotName))
                return;

            string[] artifactPaths = GetAllKnownArtifactPaths(slotName);
            for (int i = 0; i < artifactPaths.Length; i++)
            {
                DeleteFileIfExists(artifactPaths[i]);
            }

            DeleteFileIfExists(SaveSlotMaintenanceRecord.GetPath(slotName));
            SaveThumbnailSystem.DeleteThumbnail(slotName);
        }

        private string GetSaveKey(string slotName) => $"{saveKeyPrefix}{slotName}";
        public static string GetPrimarySaveFilePath(string slotName) => $"{slotName}.sav";
        public static string GetBackupSaveFilePath(string slotName) => GetBackupSaveFilePath(slotName, 1);
        public static string GetBackupSaveFilePath(string slotName, int generation)
        {
            if (generation <= 1)
                return $"{slotName}.sav.bak";

            return $"{slotName}.sav.bak{generation}";
        }
        public static string GetTempSaveFilePath(string slotName) => $"{slotName}.sav.tmp";

        private static bool FileExists(string path)
        {
            return !string.IsNullOrEmpty(path) && ES3.FileExists(path);
        }

        private static void DeleteFileIfExists(string path)
        {
            if (FileExists(path))
                ES3.DeleteFile(path);
        }

        public static string[] GetAllKnownArtifactPaths(string slotName)
        {
            List<string> paths = new List<string>(16)
            {
                GetPrimarySaveFilePath(slotName),
                GetTempSaveFilePath(slotName),
                SaveMetadata.GetPrimaryMetadataPath(slotName),
                SaveMetadata.GetTempMetadataPath(slotName),
                SaveSlotMaintenanceRecord.GetPath(slotName)
            };

            int maxGeneration = GetMaxBackupGenerationCount();
            for (int generation = 1; generation <= maxGeneration; generation++)
            {
                paths.Add(GetBackupSaveFilePath(slotName, generation));
                paths.Add(SaveMetadata.GetBackupMetadataPath(slotName, generation));
            }

            return paths.ToArray();
        }

        private static void RotateFile(string primaryPath, string backupPath)
        {
            DeleteFileIfExists(backupPath);
            if (FileExists(primaryPath))
                ES3.RenameFile(primaryPath, backupPath);
        }

        private static void RotateBackupChain(string primaryPath, Func<int, string> backupPathFactory, int retentionCount)
        {
            if (retentionCount <= 0)
            {
                DeleteFileIfExists(primaryPath);
                return;
            }

            for (int generation = retentionCount; generation >= 1; generation--)
            {
                string targetPath = backupPathFactory(generation);
                if (generation == retentionCount)
                    DeleteFileIfExists(targetPath);

                string sourcePath = generation == 1 ? primaryPath : backupPathFactory(generation - 1);
                if (FileExists(sourcePath))
                    ES3.RenameFile(sourcePath, targetPath);
            }

            int maxGeneration = GetMaxBackupGenerationCount();
            for (int generation = retentionCount + 1; generation <= maxGeneration; generation++)
            {
                DeleteFileIfExists(backupPathFactory(generation));
            }
        }

        private static void PromoteFile(string tempPath, string finalPath)
        {
            DeleteFileIfExists(finalPath);
            if (FileExists(tempPath))
                ES3.RenameFile(tempPath, finalPath);
        }

        private static bool TryExtractSlotName(string fileName, out string slotName)
        {
            slotName = null;
            if (string.IsNullOrEmpty(fileName))
                return false;

            if (fileName.EndsWith(".sav.tmp", StringComparison.OrdinalIgnoreCase))
                slotName = fileName.Substring(0, fileName.Length - ".sav.tmp".Length);
            else if (TryStripBackupSuffix(fileName, ".sav.bak", out slotName))
                return true;
            else if (fileName.EndsWith(".sav", StringComparison.OrdinalIgnoreCase))
                slotName = fileName.Substring(0, fileName.Length - ".sav".Length);
            else if (fileName.EndsWith(".meta.tmp", StringComparison.OrdinalIgnoreCase))
                slotName = fileName.Substring(0, fileName.Length - ".meta.tmp".Length);
            else if (TryStripBackupSuffix(fileName, ".meta.bak", out slotName))
                return true;
            else if (fileName.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                slotName = fileName.Substring(0, fileName.Length - ".meta".Length);
            else if (fileName.EndsWith(".diag", StringComparison.OrdinalIgnoreCase))
                slotName = fileName.Substring(0, fileName.Length - ".diag".Length);
            else if (fileName.EndsWith(".jpg.tmp", StringComparison.OrdinalIgnoreCase))
                slotName = fileName.Substring(0, fileName.Length - ".jpg.tmp".Length);
            else if (fileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                slotName = fileName.Substring(0, fileName.Length - ".jpg".Length);

            return !string.IsNullOrEmpty(slotName);
        }

        private static bool TryStripBackupSuffix(string fileName, string baseSuffix, out string slotName)
        {
            slotName = null;
            int suffixIndex = fileName.LastIndexOf(baseSuffix, StringComparison.OrdinalIgnoreCase);
            if (suffixIndex <= 0)
                return false;

            int digitsStart = suffixIndex + baseSuffix.Length;
            if (digitsStart < fileName.Length)
            {
                for (int i = digitsStart; i < fileName.Length; i++)
                {
                    if (!char.IsDigit(fileName[i]))
                        return false;
                }
            }

            slotName = fileName.Substring(0, suffixIndex);
            return !string.IsNullOrEmpty(slotName);
        }

        private static List<SaveLoadCandidate> BuildLoadCandidates(string slotName)
        {
            int backupRetention = GetBackupRetentionCountStatic(slotName);
            List<SaveLoadCandidate> candidates = new List<SaveLoadCandidate>(backupRetention + 1);

            string primarySavePath = GetPrimarySaveFilePath(slotName);
            string primaryMetadataPath = SaveMetadata.GetPrimaryMetadataPath(slotName);

            if (FileExists(primarySavePath))
            {
                string metadataPath = ResolveMetadataPath(slotName, 0);
                bool matches = string.Equals(metadataPath, primaryMetadataPath, StringComparison.OrdinalIgnoreCase);
                candidates.Add(new SaveLoadCandidate(primarySavePath, metadataPath, false, matches, 0));
            }

            for (int generation = 1; generation <= backupRetention; generation++)
            {
                string backupSavePath = GetBackupSaveFilePath(slotName, generation);
                if (!FileExists(backupSavePath))
                    continue;

                string expectedMetadataPath = SaveMetadata.GetBackupMetadataPath(slotName, generation);
                string metadataPath = ResolveMetadataPath(slotName, generation);
                bool matches = string.Equals(metadataPath, expectedMetadataPath, StringComparison.OrdinalIgnoreCase);
                candidates.Add(new SaveLoadCandidate(backupSavePath, metadataPath, true, matches, generation));
            }

            return candidates;
        }

        private static string ResolveMetadataPath(string slotName, int preferredGeneration)
        {
            if (preferredGeneration <= 0)
            {
                string primaryMetadataPath = SaveMetadata.GetPrimaryMetadataPath(slotName);
                if (SaveMetadata.Exists(primaryMetadataPath))
                    return primaryMetadataPath;
            }
            else
            {
                string preferredBackupMetadata = SaveMetadata.GetBackupMetadataPath(slotName, preferredGeneration);
                if (SaveMetadata.Exists(preferredBackupMetadata))
                    return preferredBackupMetadata;
            }

            string primaryPath = SaveMetadata.GetPrimaryMetadataPath(slotName);
            if (SaveMetadata.Exists(primaryPath))
                return primaryPath;

            int maxGeneration = GetMaxBackupGenerationCount();
            for (int generation = 1; generation <= maxGeneration; generation++)
            {
                string metadataPath = SaveMetadata.GetBackupMetadataPath(slotName, generation);
                if (SaveMetadata.Exists(metadataPath))
                    return metadataPath;
            }

            return null;
        }

        private static bool TryRepairSaveSlotInternal(string slotName, out SaveSlotRepairResult result)
        {
            result = new SaveSlotRepairResult
            {
                SlotName = slotName,
                Success = false,
                Message = "Repair not attempted."
            };

            SaveSlotInfo beforeInfo = BuildSaveSlotInfoInternal(slotName);
            if (beforeInfo == null || !beforeInfo.HasAnySaveData)
            {
                result.Message = "No save data found for this slot.";
                result.IntegrityBefore = SaveSlotIntegrityState.Empty;
                result.IntegrityAfter = SaveSlotIntegrityState.Empty;
                return false;
            }

            result.IntegrityBefore = beforeInfo.IntegrityState;

            string key = GetSaveKeyStatic(slotName);
            List<SaveLoadCandidate> candidates = BuildLoadCandidates(slotName);
            SaveData repairedData = null;
            SaveMetadata metadataSource = beforeInfo.Metadata;
            SaveLoadCandidate selectedCandidate = default;
            ES3.CompressionType selectedCompression = GetPreferredCompressionType();
            string errorMessage = string.Empty;

            for (int i = 0; i < candidates.Count; i++)
            {
                if (TryLoadCandidateWithAnyCompression(
                    key,
                    candidates[i],
                    out SaveData candidateData,
                    out SaveMetadata candidateMetadata,
                    out ES3.CompressionType candidateCompression,
                    out string candidateError))
                {
                    repairedData = candidateData;
                    metadataSource = candidateMetadata ?? beforeInfo.Metadata;
                    selectedCandidate = candidates[i];
                    selectedCompression = candidateCompression;
                    break;
                }

                errorMessage = candidateError;
            }

            if (repairedData == null)
            {
                result.Message = string.IsNullOrEmpty(errorMessage)
                    ? "No valid save candidate could be repaired."
                    : errorMessage;
                result.IntegrityAfter = beforeInfo.IntegrityState;
                return false;
            }

            ES3.CompressionType preferredCompression = GetPreferredCompressionType();
            bool shouldRewritePrimarySave = selectedCandidate.IsBackup
                || !FileExists(GetPrimarySaveFilePath(slotName))
                || selectedCompression != preferredCompression;

            bool shouldRewritePrimaryMetadata = shouldRewritePrimarySave
                || !SaveMetadata.Exists(SaveMetadata.GetPrimaryMetadataPath(slotName))
                || !selectedCandidate.MetadataMatchesSave
                || metadataSource == null
                || !string.Equals(selectedCandidate.MetadataPath, SaveMetadata.GetPrimaryMetadataPath(slotName), StringComparison.OrdinalIgnoreCase);

            bool changedAnything = RepairPrimaryArtifacts(
                slotName,
                key,
                repairedData,
                metadataSource,
                shouldRewritePrimarySave,
                shouldRewritePrimaryMetadata);

            SaveSlotInfo afterInfo = BuildSaveSlotInfoInternal(slotName);

            result.Success = true;
            result.ChangedAnything = changedAnything;
            result.UsedBackupSource = selectedCandidate.IsBackup;
            result.SourceBackupGeneration = selectedCandidate.IsBackup ? selectedCandidate.BackupGeneration : 0;
            result.UsedLegacyCompression = selectedCompression != preferredCompression;
            result.RewrotePrimarySave = shouldRewritePrimarySave;
            result.RewrotePrimaryMetadata = shouldRewritePrimaryMetadata;
            result.IntegrityAfter = afterInfo != null ? afterInfo.IntegrityState : beforeInfo.IntegrityState;
            result.Message = changedAnything
                ? "Slot repaired and normalized."
                : "Slot already healthy.";
            RecordRepairResult(result, repairedData != null ? repairedData.version : 0);
            return true;
        }

        private static bool TryAuditSaveSlotInternal(string slotName, out SaveSlotAuditResult result)
        {
            result = new SaveSlotAuditResult
            {
                SlotName = slotName,
                Success = false,
                Message = "Audit not attempted."
            };

            SaveSlotInfo info = BuildSaveSlotInfoInternal(slotName);
            if (info == null || !info.HasAnySaveData)
            {
                result.Message = "No save data found for this slot.";
                result.IntegrityState = SaveSlotIntegrityState.Empty;
                return false;
            }

            result.Success = true;
            result.IntegrityState = info.IntegrityState;

            string key = GetSaveKeyStatic(slotName);
            List<SaveLoadCandidate> candidates = BuildLoadCandidates(slotName);
            ES3.CompressionType preferredCompression = GetPreferredCompressionType();
            SaveLoadCandidate selectedCandidate = default;
            SaveData selectedData = null;
            ES3.CompressionType selectedCompression = preferredCompression;
            bool hasSelectedCandidate = false;

            for (int i = 0; i < candidates.Count; i++)
            {
                SaveLoadCandidate candidate = candidates[i];
                bool isBackup = candidate.IsBackup;

                if (isBackup)
                    result.HasBackupCandidate = true;
                else
                    result.HasPrimaryCandidate = true;

                if (TryLoadCandidateWithAnyCompression(
                    key,
                    candidate,
                    out SaveData candidateData,
                    out SaveMetadata _,
                    out ES3.CompressionType candidateCompression,
                    out string candidateError))
                {
                    if (isBackup)
                        result.BackupReadable = true;
                    else
                        result.PrimaryReadable = true;

                    if (!hasSelectedCandidate)
                    {
                        hasSelectedCandidate = true;
                        selectedCandidate = candidate;
                        selectedData = candidateData;
                        selectedCompression = candidateCompression;
                    }
                }
                else
                {
                    if (isBackup)
                        result.BackupError = candidateError;
                    else
                        result.PrimaryError = candidateError;
                }
            }

            result.SlotReadable = hasSelectedCandidate;
            if (!hasSelectedCandidate)
            {
                result.Message = "No readable save source found.";
                return true;
            }

            result.SelectedBackupSource = selectedCandidate.IsBackup;
            result.SelectedBackupGeneration = selectedCandidate.IsBackup ? selectedCandidate.BackupGeneration : 0;
            result.SelectedLegacyCompression = selectedCompression != preferredCompression;
            result.DetectedVersion = selectedData != null ? Mathf.Max(selectedData.version, 0) : 0;
            result.RequiresMigration = selectedData != null && selectedData.version != SaveData.CurrentVersion;
            result.RecommendedSource = selectedCandidate.IsBackup
                ? $"Backup g{selectedCandidate.BackupGeneration}"
                : "Primary";

            bool recommendedRepair = selectedCandidate.IsBackup
                || selectedCompression != preferredCompression
                || !SaveMetadata.Exists(SaveMetadata.GetPrimaryMetadataPath(slotName))
                || !selectedCandidate.MetadataMatchesSave
                || info.IntegrityState == SaveSlotIntegrityState.MissingMetadata
                || info.IntegrityState == SaveSlotIntegrityState.MetadataRecoveredFromBackup
                || info.IntegrityState == SaveSlotIntegrityState.MetadataSynthesized
                || info.IntegrityState == SaveSlotIntegrityState.CorruptedMetadata;

            result.RecommendedRepair = recommendedRepair;
            result.Message = BuildAuditMessage(result);
            RecordAuditResult(result);
            return true;
        }

        private static string BuildAuditMessage(SaveSlotAuditResult result)
        {
            if (result == null)
                return "Audit result is missing.";

            if (!result.SlotReadable)
                return "Slot exists, but no readable source was found.";

            string source = string.IsNullOrEmpty(result.RecommendedSource)
                ? (result.SelectedBackupSource ? "backup" : "primary")
                : result.RecommendedSource.ToLowerInvariant();
            string migration = result.RequiresMigration
                ? $"migration required from v{result.DetectedVersion}"
                : $"version v{result.DetectedVersion}";
            string compression = result.SelectedLegacyCompression ? ", legacy compression" : string.Empty;
            string repair = result.RecommendedRepair ? ", repair recommended" : ", no repair needed";
            return $"Readable from {source}, {migration}{compression}{repair}.";
        }

        private static bool ShouldSelfRepairSlot(string slotName, SaveLoadCandidate loadedCandidate)
        {
            if (loadedCandidate.IsBackup)
                return true;

            string primaryMetadataPath = SaveMetadata.GetPrimaryMetadataPath(slotName);
            if (!SaveMetadata.Exists(primaryMetadataPath))
                return true;

            return !loadedCandidate.MetadataMatchesSave;
        }

        private bool SelfRepairPrimaryArtifacts(string slotName, string key, SaveData data, string sceneName, Vector3 playerPosition)
        {
            SaveMetadata metadata = new SaveMetadata
            {
                SlotName = slotName,
                GameVersion = Application.version,
                Timestamp = DateTime.UtcNow.Ticks,
                PlayTimeSeconds = data.totalPlayTime,
                SceneName = string.IsNullOrEmpty(sceneName) ? "Unknown" : sceneName,
                PlayerPosition = playerPosition
            };

            return RepairPrimaryArtifacts(
                slotName,
                key,
                data,
                metadata,
                overwritePrimarySave: true,
                rewritePrimaryMetadata: true);
        }

        private void VerifyIntegrity(SaveData data, ES3Settings settings, string metadataPath, bool enforceChecksum)
        {
            if (data == null)
                throw new Exception("Save data is null.");

            if (!enforceChecksum || string.IsNullOrEmpty(metadataPath))
                return;

            SaveMetadata metadata = SaveMetadata.LoadFromPath(metadataPath);
            if (metadata == null || string.IsNullOrEmpty(metadata.Checksum))
                return;

            byte[] rawData = ES3.Serialize(data, settings);
            uint calculatedCrc = CalculateCRC32(rawData);
            if (uint.TryParse(metadata.Checksum, System.Globalization.NumberStyles.HexNumber, null, out uint expectedCrc) &&
                calculatedCrc != expectedCrc)
            {
                throw new Exception("CRC mismatch. Save data is corrupted.");
            }
        }

        private static bool TryLoadCandidateWithAnyCompression(
            string key,
            SaveLoadCandidate candidate,
            out SaveData data,
            out SaveMetadata metadata,
            out ES3.CompressionType resolvedCompression,
            out string errorMessage)
        {
            data = null;
            metadata = null;
            resolvedCompression = GetPreferredCompressionType();
            errorMessage = string.Empty;

            ES3.CompressionType preferredCompression = GetPreferredCompressionType();
            ES3.CompressionType fallbackCompression = preferredCompression == ES3.CompressionType.Gzip
                ? ES3.CompressionType.None
                : ES3.CompressionType.Gzip;

            if (TryLoadCandidateWithCompression(key, candidate, preferredCompression, out data, out metadata, out errorMessage))
            {
                resolvedCompression = preferredCompression;
                return true;
            }

            if (fallbackCompression != preferredCompression &&
                TryLoadCandidateWithCompression(key, candidate, fallbackCompression, out data, out metadata, out errorMessage))
            {
                resolvedCompression = fallbackCompression;
                return true;
            }

            return false;
        }

        private static bool TryLoadCandidateWithCompression(
            string key,
            SaveLoadCandidate candidate,
            ES3.CompressionType compressionType,
            out SaveData data,
            out SaveMetadata metadata,
            out string errorMessage)
        {
            data = null;
            metadata = null;
            errorMessage = string.Empty;

            try
            {
                ES3Settings settings = CreatePathSettings(candidate.SavePath, compressionType);
                data = ES3.Load<SaveData>(key, settings);
                metadata = !string.IsNullOrEmpty(candidate.MetadataPath)
                    ? SaveMetadata.LoadFromPath(candidate.MetadataPath)
                    : null;
                VerifyIntegrityStatic(data, settings, metadata, candidate.MetadataMatchesSave);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        private static void VerifyIntegrityStatic(SaveData data, ES3Settings settings, SaveMetadata metadata, bool enforceChecksum)
        {
            if (data == null)
                throw new Exception("Save data is null.");

            if (!enforceChecksum || metadata == null || string.IsNullOrEmpty(metadata.Checksum))
                return;

            byte[] rawData = ES3.Serialize(data, settings);
            uint calculatedCrc = CalculateCRC32(rawData);
            if (uint.TryParse(metadata.Checksum, System.Globalization.NumberStyles.HexNumber, null, out uint expectedCrc) &&
                calculatedCrc != expectedCrc)
            {
                throw new Exception("CRC mismatch. Save data is corrupted.");
            }
        }

        private static bool RepairPrimaryArtifacts(
            string slotName,
            string key,
            SaveData data,
            SaveMetadata metadataSource,
            bool overwritePrimarySave,
            bool rewritePrimaryMetadata)
        {
            string primarySavePath = GetPrimarySaveFilePath(slotName);
            string tempSavePath = GetTempSaveFilePath(slotName);
            string primaryMetadataPath = SaveMetadata.GetPrimaryMetadataPath(slotName);
            string tempMetadataPath = SaveMetadata.GetTempMetadataPath(slotName);

            bool changedAnything = false;
            DeleteFileIfExists(tempSavePath);
            DeleteFileIfExists(tempMetadataPath);

            ES3.CompressionType preferredCompression = GetPreferredCompressionType();

            if (overwritePrimarySave || !FileExists(primarySavePath))
            {
                ES3Settings tempSaveSettings = CreatePathSettings(tempSavePath, preferredCompression);
                ES3.Save(key, data, tempSaveSettings);
                PromoteFile(tempSavePath, primarySavePath);
                changedAnything = true;
            }

            if (rewritePrimaryMetadata || !SaveMetadata.Exists(primaryMetadataPath))
            {
                ES3Settings primarySettings = CreatePathSettings(primarySavePath, preferredCompression);
                byte[] primaryBytes = ES3.Serialize(data, primarySettings);
                string checksum = CalculateCRC32(primaryBytes).ToString("X8");

                SaveMetadata repairedMetadata = new SaveMetadata
                {
                    SlotName = slotName,
                    GameVersion = !string.IsNullOrEmpty(metadataSource?.GameVersion) ? metadataSource.GameVersion : Application.version,
                    Timestamp = DateTime.UtcNow.Ticks,
                    PlayTimeSeconds = data.totalPlayTime,
                    SceneName = !string.IsNullOrEmpty(metadataSource?.SceneName) ? metadataSource.SceneName : "Unknown",
                    PlayerPosition = data.playerStats.GetPosition(),
                    Checksum = checksum
                };

                repairedMetadata.Save(tempMetadataPath);
                PromoteFile(tempMetadataPath, primaryMetadataPath);
                changedAnything = true;
            }

            return changedAnything;
        }

        private static ES3.CompressionType GetPreferredCompressionType()
        {
            if (Instance != null)
                return Instance.useCompression ? ES3.CompressionType.Gzip : ES3.CompressionType.None;

            return ES3.CompressionType.Gzip;
        }

        private static ES3Settings CreatePathSettings(string path, ES3.CompressionType compressionType)
        {
            ES3Settings settings = new ES3Settings
            {
                encryptionType = ES3.EncryptionType.None,
                compressionType = compressionType
            };

            return new ES3Settings(path, settings);
        }

        private static string GetSaveKeyStatic(string slotName)
        {
            if (Instance != null)
                return Instance.GetSaveKey(slotName);

            return $"save_{slotName}";
        }

        private static SaveSlotMaintenanceRecord GetOrCreateMaintenanceRecord(string slotName)
        {
            SaveSlotMaintenanceRecord record = SaveSlotMaintenanceRecord.Load(slotName);
            if (record == null)
                record = SaveSlotMaintenanceRecord.Create(slotName);
            return record;
        }

        private static void RecordSuccessfulSave(string slotName, int dataVersion, SaveSlotIntegrityState integrityState)
        {
            SaveSlotMaintenanceRecord record = GetOrCreateMaintenanceRecord(slotName);
            record.LastSuccessfulSaveTicksUtc = DateTime.UtcNow.Ticks;
            record.SuccessfulSaveCount++;
            record.LastKnownSaveVersion = dataVersion;
            record.LastKnownIntegrityState = integrityState.ToString();
            record.LastFailureContext = string.Empty;
            record.LastFailureMessage = string.Empty;
            record.Save();
        }

        private static void RecordSuccessfulLoad(
            string slotName,
            int dataVersion,
            SaveSlotIntegrityState integrityState,
            bool usedBackup,
            int backupGeneration,
            bool usedLegacyCompression,
            bool selfRepaired)
        {
            SaveSlotMaintenanceRecord record = GetOrCreateMaintenanceRecord(slotName);
            record.LastSuccessfulLoadTicksUtc = DateTime.UtcNow.Ticks;
            record.SuccessfulLoadCount++;
            record.LastKnownSaveVersion = dataVersion;
            record.LastKnownIntegrityState = integrityState.ToString();
            record.LastLoadUsedBackup = usedBackup;
            record.LastLoadBackupGeneration = backupGeneration;
            record.LastLoadUsedLegacyCompression = usedLegacyCompression;
            record.LastLoadSelfRepaired = selfRepaired;
            record.LastFailureContext = string.Empty;
            record.LastFailureMessage = string.Empty;
            record.Save();
        }

        private static void RecordFailure(string slotName, string context, string message)
        {
            if (string.IsNullOrEmpty(slotName))
                return;

            SaveSlotMaintenanceRecord record = GetOrCreateMaintenanceRecord(slotName);
            record.LastFailureTicksUtc = DateTime.UtcNow.Ticks;
            record.FailureCount++;
            record.LastFailureContext = context ?? string.Empty;
            record.LastFailureMessage = message ?? string.Empty;
            record.Save();
        }

        private static void RecordAuditResult(SaveSlotAuditResult result)
        {
            if (result == null || string.IsNullOrEmpty(result.SlotName))
                return;

            SaveSlotMaintenanceRecord record = GetOrCreateMaintenanceRecord(result.SlotName);
            record.LastAuditTicksUtc = DateTime.UtcNow.Ticks;
            record.AuditCount++;
            record.LastAuditReadable = result.SlotReadable;
            record.LastAuditRecommendedRepair = result.RecommendedRepair;
            record.LastKnownSaveVersion = result.DetectedVersion;
            record.LastKnownIntegrityState = result.IntegrityState.ToString();
            record.LastLoadUsedBackup = result.SelectedBackupSource;
            record.LastLoadBackupGeneration = result.SelectedBackupSource ? Mathf.Max(1, result.SelectedBackupGeneration) : 0;
            record.LastLoadUsedLegacyCompression = result.SelectedLegacyCompression;
            record.LastAuditMessage = result.Message ?? string.Empty;
            record.Save();
        }

        private static void RecordRepairResult(SaveSlotRepairResult result, int dataVersion)
        {
            if (result == null || string.IsNullOrEmpty(result.SlotName))
                return;

            SaveSlotMaintenanceRecord record = GetOrCreateMaintenanceRecord(result.SlotName);
            record.LastRepairTicksUtc = DateTime.UtcNow.Ticks;
            record.RepairCount++;
            record.LastKnownSaveVersion = dataVersion;
            record.LastKnownIntegrityState = result.IntegrityAfter.ToString();
            record.LastLoadUsedBackup = result.UsedBackupSource;
            record.LastLoadBackupGeneration = result.UsedBackupSource ? Mathf.Max(1, result.SourceBackupGeneration) : 0;
            record.LastLoadUsedLegacyCompression = result.UsedLegacyCompression;
            record.LastRepairMessage = result.Message ?? string.Empty;
            record.Save();
        }

        private SaveSlotInfo BuildSaveSlotInfo(string slotName)
        {
            return BuildSaveSlotInfoInternal(slotName);
        }

        private static SaveSlotInfo BuildSaveSlotInfoStatic(string slotName)
        {
            return BuildSaveSlotInfoInternal(slotName);
        }

        private static SaveSlotInfo BuildSaveSlotInfoInternal(string slotName)
        {
            if (string.IsNullOrEmpty(slotName))
                return null;

            int backupRetention = GetBackupRetentionCountStatic(slotName);
            string primarySavePath = GetPrimarySaveFilePath(slotName);
            string primaryMetadataPath = SaveMetadata.GetPrimaryMetadataPath(slotName);

            bool hasPrimarySave = FileExists(primarySavePath);
            bool hasPrimaryMetadata = SaveMetadata.Exists(primaryMetadataPath);
            bool hasThumbnail = File.Exists(SaveThumbnailSystem.GetThumbnailPath(slotName));
            bool hasBackupSave = false;
            bool hasBackupMetadata = false;

            for (int generation = 1; generation <= backupRetention; generation++)
            {
                if (!hasBackupSave && FileExists(GetBackupSaveFilePath(slotName, generation)))
                    hasBackupSave = true;

                if (!hasBackupMetadata && SaveMetadata.Exists(SaveMetadata.GetBackupMetadataPath(slotName, generation)))
                    hasBackupMetadata = true;
            }

            if (!hasPrimarySave && !hasBackupSave)
                return null;

            SaveMetadata metadata = null;
            bool metadataRecoveredFromBackup = false;
            bool metadataSynthesized = false;
            bool metadataCorrupted = false;

            if (hasPrimaryMetadata)
            {
                metadata = SaveMetadata.LoadFromPath(primaryMetadataPath);
                if (metadata == null)
                    metadataCorrupted = true;
            }

            if (metadata == null && hasBackupMetadata)
            {
                for (int generation = 1; generation <= backupRetention; generation++)
                {
                    string backupMetadataPath = SaveMetadata.GetBackupMetadataPath(slotName, generation);
                    if (!SaveMetadata.Exists(backupMetadataPath))
                        continue;

                    metadata = SaveMetadata.LoadFromPath(backupMetadataPath);
                    metadataRecoveredFromBackup = metadata != null;
                    metadataCorrupted |= metadata == null;
                    if (metadata != null)
                        break;
                }
            }

            long lastWriteTicksUtc = 0L;
            long primaryBytes = GetPersistentFileSize(primarySavePath);
            long backupBytes = 0L;

            UpdateLastWrite(primarySavePath, ref lastWriteTicksUtc);
            UpdateLastWrite(primaryMetadataPath, ref lastWriteTicksUtc);
            UpdateLastWrite(Path.GetFileName(SaveThumbnailSystem.GetThumbnailPath(slotName)), ref lastWriteTicksUtc);

            for (int generation = 1; generation <= backupRetention; generation++)
            {
                string backupSavePath = GetBackupSaveFilePath(slotName, generation);
                string backupMetadataPath = SaveMetadata.GetBackupMetadataPath(slotName, generation);
                backupBytes += GetPersistentFileSize(backupSavePath);
                UpdateLastWrite(backupSavePath, ref lastWriteTicksUtc);
                UpdateLastWrite(backupMetadataPath, ref lastWriteTicksUtc);
            }

            if (metadata == null)
            {
                metadata = SaveMetadata.CreateFallback(slotName, lastWriteTicksUtc);
                metadataSynthesized = true;
            }

            SaveSlotIntegrityState integrityState;
            if (metadataCorrupted && metadataRecoveredFromBackup)
            {
                integrityState = SaveSlotIntegrityState.MetadataRecoveredFromBackup;
            }
            else if (metadataCorrupted && !metadataSynthesized)
            {
                integrityState = SaveSlotIntegrityState.CorruptedMetadata;
            }
            else if (hasPrimarySave && hasBackupSave && hasPrimaryMetadata)
            {
                integrityState = SaveSlotIntegrityState.HealthyWithBackup;
            }
            else if (hasPrimarySave && hasPrimaryMetadata)
            {
                integrityState = SaveSlotIntegrityState.Healthy;
            }
            else if (!hasPrimarySave && hasBackupSave)
            {
                integrityState = hasBackupMetadata
                    ? SaveSlotIntegrityState.BackupOnly
                    : SaveSlotIntegrityState.MetadataSynthesized;
            }
            else if (metadataRecoveredFromBackup)
            {
                integrityState = SaveSlotIntegrityState.MetadataRecoveredFromBackup;
            }
            else if (metadataSynthesized)
            {
                integrityState = SaveSlotIntegrityState.MetadataSynthesized;
            }
            else
            {
                integrityState = SaveSlotIntegrityState.MissingMetadata;
            }

            metadata.SlotName = slotName;

            return new SaveSlotInfo
            {
                SlotName = slotName,
                Metadata = metadata,
                IntegrityState = integrityState,
                HasPrimarySave = hasPrimarySave,
                HasBackupSave = hasBackupSave,
                HasPrimaryMetadata = hasPrimaryMetadata,
                HasBackupMetadata = hasBackupMetadata,
                HasThumbnail = hasThumbnail,
                MetadataRecoveredFromBackup = metadataRecoveredFromBackup,
                MetadataSynthesized = metadataSynthesized,
                LastWriteTicksUtc = lastWriteTicksUtc,
                PrimarySaveBytes = primaryBytes,
                BackupSaveBytes = backupBytes
            };
        }

        private static long GetPersistentFileSize(string relativeFileName)
        {
            if (string.IsNullOrEmpty(relativeFileName))
                return 0L;

            string fullPath = Path.Combine(Application.persistentDataPath, relativeFileName);
            if (!File.Exists(fullPath))
                return 0L;

            return new FileInfo(fullPath).Length;
        }

        private static void UpdateLastWrite(string relativeFileName, ref long lastWriteTicksUtc)
        {
            if (string.IsNullOrEmpty(relativeFileName))
                return;

            string fullPath = Path.Combine(Application.persistentDataPath, relativeFileName);
            if (!File.Exists(fullPath))
                return;

            long ticks = File.GetLastWriteTimeUtc(fullPath).Ticks;
            if (ticks > lastWriteTicksUtc)
                lastWriteTicksUtc = ticks;
        }
    }
}
