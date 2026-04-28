// ============================================================================
// HECTON-8 — SaveManager.cs
// Менеджер сохранений. Singleton, DontDestroyOnLoad.
//
// АРХИТЕКТУРА:
//   • Реестр ISaveable вместо FindObjectsByType (zero GC при save/load).
//   • XXHash3 checksums for header/payload integrity.
//   • Unity 6 Awaitable API: BackgroundThreadAsync / MainThreadAsync.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Modding;
using Hecton8.Quest;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Hecton8.SaveSystem
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-8000)]
    public sealed class SaveManager : MonoBehaviour, ISaveService
    {
        private const long MainThreadSnapshotBudgetMs = 50L;
        private static readonly long PreCompressionYieldBudgetTicks = Math.Max(1L, Stopwatch.Frequency / 500L);

        // ══════════════════════════════════════════════════════════
        //  SAVE STATE
        // ══════════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        private static SaveManager _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }
        public static SaveManager Instance => _instance;
        public bool IsInitialized => ReferenceEquals(_instance, this) && ReferenceEquals(GlobalRegistry.Save, this);
        public bool IsBusy => _isBusy;
        public float CurrentPlayTimeSeconds => (float)ResolveCurrentPlayTimeSeconds();
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

        [Header("── Backup Policy ─────────────────────────────")]
        [SerializeField] private int manualBackupGenerations = DefaultManualBackupGenerations;
        [SerializeField] private int autoBackupGenerations = DefaultAutoBackupGenerations;
        [SerializeField] private int quickBackupGenerations = DefaultQuickBackupGenerations;

        [Header("── Diagnostics ───────────────────────────────")]
        [SerializeField] private bool verboseLogging;
        [SerializeField] private int _debugRegisteredCount;

        private readonly List<ISaveable> _saveables = new List<ISaveable>(16);
        private bool _registryDirty;
        private double _sessionStartTime;
        private double _totalPlayTime;
        private bool _isBusy;

        private static readonly Comparison<ISaveable> SavePriorityCompare = (a, b) => a.SavePriority.CompareTo(b.SavePriority);
        private static readonly Comparison<ISaveable> LoadPriorityCompare = (a, b) => a.LoadPriority.CompareTo(b.LoadPriority);

        private NativeArray<byte> _savePayloadBuffer;
        private NativeArray<byte> _compressedSaveBuffer;

        private readonly struct SaveLoadCandidate
        {
            public readonly string SavePath;
            public readonly bool IsBackup;
            public readonly int BackupGeneration;

            public SaveLoadCandidate(string savePath, bool isBackup, int backupGeneration)
            {
                SavePath = savePath;
                IsBackup = isBackup;
                BackupGeneration = backupGeneration;
            }
        }

        private enum SaveSlotCategory
        {
            Manual = 0,
            Auto,
            Quick
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
                        return math.clamp(Instance.autoBackupGenerations, 1, 8);
                    case SaveSlotCategory.Quick:
                        return math.clamp(Instance.quickBackupGenerations, 1, 8);
                    default:
                        return math.clamp(Instance.manualBackupGenerations, 1, 8);
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
                return math.clamp(
                    math.max(Instance.manualBackupGenerations, math.max(Instance.autoBackupGenerations, Instance.quickBackupGenerations)),
                    1,
                    8);
            }

            return math.max(DefaultManualBackupGenerations, math.max(DefaultAutoBackupGenerations, DefaultQuickBackupGenerations));
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
            _sessionStartTime = Time.realtimeSinceStartupAsDouble;
            InitializeNativeBuffers();
            SaveBinaryStorage.WarmRuntime();
            GlobalRegistry.RegisterSaveService(this);
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(GlobalRegistry.Save, this))
                GlobalRegistry.UnregisterSaveService(this);

            if (_instance == this)
                _instance = null;

            if (_savePayloadBuffer.IsCreated)
                _savePayloadBuffer.Dispose();

            if (_compressedSaveBuffer.IsCreated)
                _compressedSaveBuffer.Dispose();
        }

        private void InitializeNativeBuffers()
        {
            if (!_savePayloadBuffer.IsCreated)
            {
                // COLD ALLOC: NativeArray<byte>[67108864] - raw binary save staging buffer for save payload assembly - owner: SaveManager
                _savePayloadBuffer = new NativeArray<byte>(SaveBinaryStorage.RawPayloadCapacityBytes, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }

            if (!_compressedSaveBuffer.IsCreated)
            {
                // COLD ALLOC: NativeArray<byte>[67378176] - worst-case LZ4 block-compressed save payload buffer for 64MB raw save budget - owner: SaveManager
                _compressedSaveBuffer = new NativeArray<byte>(SaveBinaryStorage.MaxCompressedPayloadBytes, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }
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
            var snapshotTimer = Stopwatch.StartNew();
            double playTime = ResolveCurrentPlayTimeSeconds();
            SaveData data = SaveData.CreateNew(playTime);
            PersistentWorldRegistry persistentWorldRegistry = PersistentWorldRegistry.Instance;
            NativeArray<PersistentWorldDeltaRecord> persistentWorldDeltaSnapshot = default;
            NativeArray<EcosystemSectorSaveRecord> ecosystemSectorSnapshot = default;
            NativeArray<uint> packedQuestStateSnapshot = default;
            NativeArray<byte> voxelDeltaSnapshot = default;

            try
            {
                SortRegistryIfDirty(SavePriorityCompare);
                for (int i = 0; i < _saveables.Count; i++)
                {
                    if (!IsAlive(_saveables[i]))
                        continue;

                    if (_saveables[i] is VoxelDeltaProcessor voxelDeltaProcessor)
                    {
                        if (voxelDeltaSnapshot.IsCreated)
                            voxelDeltaSnapshot.Dispose();

                        voxelDeltaSnapshot = voxelDeltaProcessor.CaptureNativeSnapshot(Allocator.Persistent);
                        continue;
                    }

                    _saveables[i].PopulateSaveData(data);
                }

                ModSaveStateStore.PopulateSaveData(data);
                Stopwatch divergenceSnapshotTimer = Stopwatch.StartNew();
                if (persistentWorldRegistry != null)
                {
                    persistentWorldRegistry.CaptureSaveSnapshot();
                    persistentWorldDeltaSnapshot = persistentWorldRegistry.GetSaveSnapshotArray();
                }

                EcosystemDirector ecosystemDirector = GlobalRegistry.EcosystemDirector as EcosystemDirector;
                if (ecosystemDirector != null)
                {
                    ecosystemDirector.CaptureSaveSnapshot();
                    ecosystemSectorSnapshot = ecosystemDirector.GetSaveSnapshotArray();
                }

                divergenceSnapshotTimer.Stop();
                QuestManager questManager = QuestManager.Instance;
                if (questManager != null)
                    packedQuestStateSnapshot = questManager.CapturePackedStateSnapshot(Allocator.Persistent);

                SaveMetadata metadata = new SaveMetadata
                {
                    SlotName = slotName,
                    GameVersion = Application.version,
                    Timestamp = DateTime.UtcNow.Ticks,
                    PlayTimeSeconds = (float)playTime,
                    SceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                    PlayerPosition = data.playerStats.GetPosition()
                };

                snapshotTimer.Stop();
                WarnIfSnapshotBudgetExceeded(slotName, snapshotTimer.ElapsedMilliseconds);

                string tempPath = GetTempSaveFilePath(slotName);
                if (divergenceSnapshotTimer.ElapsedTicks > PreCompressionYieldBudgetTicks)
                    await Awaitable.NextFrameAsync();

                await Awaitable.BackgroundThreadAsync();

                ExecuteVerifiedSavePipeline(
                    slotName,
                    tempPath,
                    GetPrimarySaveFilePath(slotName),
                    metadata,
                    data,
                    persistentWorldDeltaSnapshot,
                    ecosystemSectorSnapshot,
                    packedQuestStateSnapshot,
                    voxelDeltaSnapshot,
                    _savePayloadBuffer,
                    _compressedSaveBuffer);

                await Awaitable.MainThreadAsync();
                SaveThumbnailSystem.CaptureThumbnail(slotName);
                int backupRetention = GetBackupRetentionCount(slotName);
                SaveSlotIntegrityState savedIntegrity = backupRetention > 0
                    ? SaveSlotIntegrityState.HealthyWithBackup
                    : SaveSlotIntegrityState.Healthy;
                RecordSuccessfulSave(slotName, data.version, savedIntegrity);

                LastOperationSucceeded = true;
                Debug.Log($"[SaveManager] Saved '{slotName}' (XXH3-64: {metadata.Checksum}) in {totalTimer.ElapsedMilliseconds}ms");
                SaveEvents.RaiseSaveCompleted(slotName);
            }
            catch (Exception ex)
            {
                RecordFailure(slotName, "save", ex.Message);
                LastOperationError = ex.Message;
                Debug.LogError($"[SaveManager] Save failed: {ex.Message}");
                SaveEvents.RaiseSaveFailed(slotName, ex.Message);
            }
            finally
            {
                if (packedQuestStateSnapshot.IsCreated)
                    packedQuestStateSnapshot.Dispose();

                if (voxelDeltaSnapshot.IsCreated)
                    voxelDeltaSnapshot.Dispose();

                _isBusy = false;
            }
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void WarnIfSnapshotBudgetExceeded(string slotName, long snapshotElapsedMs)
        {
            if (snapshotElapsedMs <= MainThreadSnapshotBudgetMs)
                return;

            Debug.LogWarning(
                $"[SaveManager] Main-thread snapshot for '{slotName}' took {snapshotElapsedMs}ms. " +
                $"Budget is {MainThreadSnapshotBudgetMs}ms. Snapshot purity is pending verification.");
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
            NativeArray<byte> loadedVoxelDeltaSnapshot = default;

            try
            {
                await Awaitable.BackgroundThreadAsync();
                SaveData data = null;
                uint[] loadedQuestStateWords = null;
                PersistentWorldDeltaRecord[] loadedWorldDeltas = null;
                EcosystemSectorSaveRecord[] loadedEcosystemSectors = null;
                SaveMetadata loadedMetadata = null;
                SaveLoadCandidate loadedCandidate = default;
                Exception lastError = null;
                List<SaveLoadCandidate> candidates = BuildLoadCandidates(slotName);
                bool usedLegacyFormat = false;

                for (int i = 0; i < candidates.Count; i++)
                {
                    if (TryLoadCandidate(
                        slotName,
                        candidates[i],
                        out SaveData candidateData,
                        out uint[] candidateQuestStateWords,
                        out PersistentWorldDeltaRecord[] candidateWorldDeltas,
                        out EcosystemSectorSaveRecord[] candidateEcosystemSectors,
                        out NativeArray<byte> candidateVoxelDeltaSnapshot,
                        out SaveMetadata candidateMetadata,
                        out bool candidateUsedLegacyFormat,
                        out string candidateError))
                    {
                        data = candidateData;
                        loadedQuestStateWords = candidateQuestStateWords;
                        loadedWorldDeltas = candidateWorldDeltas;
                        loadedEcosystemSectors = candidateEcosystemSectors;
                        loadedVoxelDeltaSnapshot = candidateVoxelDeltaSnapshot;
                        loadedCandidate = candidates[i];
                        loadedMetadata = candidateMetadata;
                        usedLegacyFormat = candidateUsedLegacyFormat;
                        break;
                    }

                    lastError = new Exception(candidateError);
                    string candidateLabel = candidates[i].IsBackup
                        ? $"backup g{candidates[i].BackupGeneration}"
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
                _sessionStartTime = Time.realtimeSinceStartupAsDouble;
                ModSaveStateStore.LoadFromSaveData(data);
                QuestManager.StageLoadedPackedState(loadedQuestStateWords);
                
                _registryDirty = true;
                SortRegistryIfDirty(LoadPriorityCompare);

                VoxelDeltaProcessor voxelDeltaProcessor = null;
                for (int i = 0; i < _saveables.Count; i++)
                {
                    if (!IsAlive(_saveables[i]))
                        continue;

                    if (_saveables[i] is VoxelDeltaProcessor loadedVoxelDeltaProcessor)
                    {
                        voxelDeltaProcessor = loadedVoxelDeltaProcessor;
                        continue;
                    }

                    _saveables[i].LoadFromSaveData(data);
                }

                if (voxelDeltaProcessor != null && !voxelDeltaProcessor.TryLoadNativeSnapshot(loadedVoxelDeltaSnapshot, out string voxelLoadError))
                    throw new Exception(voxelLoadError);

                PersistentWorldRegistry.Instance?.RestoreFromLoadedRecords(loadedWorldDeltas);
                (GlobalRegistry.EcosystemDirector as EcosystemDirector)?.RestoreFromLoadedRecords(loadedEcosystemSectors);

                string activeSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                Vector3 playerPosition = data.playerStats.GetPosition();
                bool repairedPrimaryArtifacts = false;

                if (ShouldSelfRepairSlot(loadedCandidate, usedLegacyFormat))
                {
                    await Awaitable.BackgroundThreadAsync();
                    SaveMetadata repairMetadata = loadedMetadata ?? new SaveMetadata
                    {
                        SlotName = slotName,
                        GameVersion = Application.version,
                        Timestamp = DateTime.UtcNow.Ticks,
                        PlayTimeSeconds = (float)data.totalPlayTime,
                        SceneName = string.IsNullOrEmpty(activeSceneName) ? "Unknown" : activeSceneName,
                        PlayerPosition = playerPosition
                    };
                    repairedPrimaryArtifacts = SelfRepairPrimaryArtifacts(slotName, data, repairMetadata, loadedQuestStateWords, loadedWorldDeltas, loadedEcosystemSectors, loadedVoxelDeltaSnapshot);
                    await Awaitable.MainThreadAsync();
                }

                string sourceLabel = loadedCandidate.IsBackup
                    ? $"backup g{loadedCandidate.BackupGeneration}"
                    : "primary";
                LastLoadUsedBackup = loadedCandidate.IsBackup;
                LastLoadBackupGeneration = loadedCandidate.BackupGeneration;
                LastLoadSelfRepaired = repairedPrimaryArtifacts;
                LastLoadUsedLegacyCompression = usedLegacyFormat;
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
            finally
            {
                if (loadedVoxelDeltaSnapshot.IsCreated)
                    loadedVoxelDeltaSnapshot.Dispose();

                _isBusy = false;
            }
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

        public static string GetPrimarySaveFilePath(string slotName) => $"{slotName}.sav";
        public static string GetBackupSaveFilePath(string slotName) => GetBackupSaveFilePath(slotName, 1);
        public static string GetBackupSaveFilePath(string slotName, int generation)
        {
            if (generation <= 1)
                return $"{slotName}.sav.bak";

            return $"{slotName}.sav.bak{generation}";
        }
        public static string GetTempSaveFilePath(string slotName) => $"{slotName}.sav.tmp";
        private static string GetPersistentAbsolutePath(string relativePath) => Path.Combine(Application.persistentDataPath, relativePath);

        private static bool FileExists(string path)
        {
            return !string.IsNullOrEmpty(path) && File.Exists(GetPersistentAbsolutePath(path));
        }

        private static void DeleteFileIfExists(string path)
        {
            if (FileExists(path))
                File.Delete(GetPersistentAbsolutePath(path));
        }

        public static string[] GetAllKnownArtifactPaths(string slotName)
        {
            List<string> paths = new List<string>(12)
            {
                GetPrimarySaveFilePath(slotName),
                GetTempSaveFilePath(slotName),
                SaveSlotMaintenanceRecord.GetPath(slotName)
            };

            int maxGeneration = GetMaxBackupGenerationCount();
            for (int generation = 1; generation <= maxGeneration; generation++)
            {
                paths.Add(GetBackupSaveFilePath(slotName, generation));
            }

            return paths.ToArray();
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
                    File.Move(GetPersistentAbsolutePath(sourcePath), GetPersistentAbsolutePath(targetPath));
            }

            int maxGeneration = GetMaxBackupGenerationCount();
            for (int generation = retentionCount + 1; generation <= maxGeneration; generation++)
            {
                DeleteFileIfExists(backupPathFactory(generation));
            }
        }

        private static void CommitTempSaveToPrimary(string slotName, string tempPath, string finalPath)
        {
            if (!FileExists(tempPath))
                throw new FileNotFoundException("Verified temp save was not found during final rotation.", GetPersistentAbsolutePath(tempPath));

            // Step 5: rotate the previously committed primary into the backup chain before overwrite.
            RotateBackupChain(finalPath, generation => GetBackupSaveFilePath(slotName, generation), GetBackupRetentionCountStatic(slotName));

            // Step 6: promote the verified temp artifact to the authoritative primary slot.
            File.Move(GetPersistentAbsolutePath(tempPath), GetPersistentAbsolutePath(finalPath));

            // Step 7: primary must exist after promotion.
            if (!FileExists(finalPath))
                throw new IOException($"Primary save promotion failed for '{slotName}'.");

            // Step 8: temp must be fully consumed after promotion.
            if (FileExists(tempPath))
                throw new IOException($"Temp save cleanup failed for '{slotName}'.");
        }

        private static void ExecuteVerifiedSavePipeline(
            string slotName,
            string tempPath,
            string finalPath,
            SaveMetadata metadata,
            SaveData data,
            NativeArray<PersistentWorldDeltaRecord> persistentWorldItems,
            NativeArray<EcosystemSectorSaveRecord> ecosystemSectorStates,
            NativeArray<uint> packedQuestStateWords,
            NativeArray<byte> voxelDeltaSnapshot,
            NativeArray<byte> rawBuffer,
            NativeArray<byte> compressedBuffer)
        {
            // Step 1: clear any stale temp artifact from a previous interrupted transaction.
            DeleteFileIfExists(tempPath);

            // Step 2: resolve the absolute temp path used by the binary writer.
            string absoluteTempPath = GetPersistentAbsolutePath(tempPath);

            // Step 3: write the snapshot into .tmp using the binary container writer.
            if (!SaveBinaryStorage.TryWriteSaveFile(
                absoluteTempPath,
                    metadata,
                    data,
                    persistentWorldItems,
                    ecosystemSectorStates,
                    packedQuestStateWords,
                    voxelDeltaSnapshot,
                    rawBuffer,
                    compressedBuffer,
                    out string writeError))
            {
                throw new Exception(writeError);
            }

            // Step 4: the writer already re-reads metadata internally, but the pipeline still requires the temp artifact to exist here.
            if (!FileExists(tempPath))
                throw new FileNotFoundException("Verified temp save was not created by the binary writer.", absoluteTempPath);

            CommitTempSaveToPrimary(slotName, tempPath, finalPath);
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
            if (FileExists(primarySavePath))
                candidates.Add(new SaveLoadCandidate(primarySavePath, false, 0));

            for (int generation = 1; generation <= backupRetention; generation++)
            {
                string backupSavePath = GetBackupSaveFilePath(slotName, generation);
                if (!FileExists(backupSavePath))
                    continue;

                candidates.Add(new SaveLoadCandidate(backupSavePath, true, generation));
            }

            return candidates;
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

            List<SaveLoadCandidate> candidates = BuildLoadCandidates(slotName);
            SaveData repairedData = null;
            uint[] packedQuestStateWords = null;
            PersistentWorldDeltaRecord[] persistentWorldItems = null;
            EcosystemSectorSaveRecord[] ecosystemSectorStates = null;
            NativeArray<byte> voxelDeltaSnapshot = default;
            SaveMetadata metadataSource = beforeInfo.Metadata;
            SaveLoadCandidate selectedCandidate = default;
            bool usedLegacyFormat = false;
            string errorMessage = string.Empty;

            for (int i = 0; i < candidates.Count; i++)
            {
                if (TryLoadCandidate(
                    slotName,
                    candidates[i],
                    out SaveData candidateData,
                    out uint[] candidatePackedQuestStateWords,
                    out PersistentWorldDeltaRecord[] candidateWorldItems,
                    out EcosystemSectorSaveRecord[] candidateEcosystemSectorStates,
                    out NativeArray<byte> candidateVoxelDeltaSnapshot,
                    out SaveMetadata candidateMetadata,
                    out bool candidateUsedLegacyFormat,
                    out string candidateError))
                {
                    repairedData = candidateData;
                    packedQuestStateWords = candidatePackedQuestStateWords;
                    persistentWorldItems = candidateWorldItems;
                    ecosystemSectorStates = candidateEcosystemSectorStates;
                    voxelDeltaSnapshot = candidateVoxelDeltaSnapshot;
                    metadataSource = candidateMetadata ?? beforeInfo.Metadata;
                    selectedCandidate = candidates[i];
                    usedLegacyFormat = candidateUsedLegacyFormat;
                    break;
                }

                errorMessage = candidateError;
            }

            if (repairedData == null)
            {
                if (voxelDeltaSnapshot.IsCreated)
                    voxelDeltaSnapshot.Dispose();

                result.Message = string.IsNullOrEmpty(errorMessage)
                    ? "No valid save candidate could be repaired."
                    : errorMessage;
                result.IntegrityAfter = beforeInfo.IntegrityState;
                return false;
            }

            bool shouldRewritePrimarySave = selectedCandidate.IsBackup
                || !FileExists(GetPrimarySaveFilePath(slotName))
                || usedLegacyFormat;

            bool shouldRewritePrimaryMetadata = shouldRewritePrimarySave
                || metadataSource == null;

            bool changedAnything = RepairPrimaryArtifacts(
                slotName,
                repairedData,
                metadataSource,
                packedQuestStateWords,
                persistentWorldItems,
                ecosystemSectorStates,
                voxelDeltaSnapshot,
                shouldRewritePrimarySave);

            SaveSlotInfo afterInfo = BuildSaveSlotInfoInternal(slotName);

            result.Success = true;
            result.ChangedAnything = changedAnything;
            result.UsedBackupSource = selectedCandidate.IsBackup;
            result.SourceBackupGeneration = selectedCandidate.IsBackup ? selectedCandidate.BackupGeneration : 0;
            result.UsedLegacyCompression = usedLegacyFormat;
            result.RewrotePrimarySave = shouldRewritePrimarySave;
            result.RewrotePrimaryMetadata = shouldRewritePrimaryMetadata;
            result.IntegrityAfter = afterInfo != null ? afterInfo.IntegrityState : beforeInfo.IntegrityState;
            result.Message = changedAnything
                ? "Slot repaired and normalized."
                : "Slot already healthy.";
            RecordRepairResult(result, repairedData != null ? repairedData.version : 0);

            if (voxelDeltaSnapshot.IsCreated)
                voxelDeltaSnapshot.Dispose();

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

            List<SaveLoadCandidate> candidates = BuildLoadCandidates(slotName);
            SaveLoadCandidate selectedCandidate = default;
            SaveData selectedData = null;
            bool selectedLegacyFormat = false;
            bool hasSelectedCandidate = false;

            for (int i = 0; i < candidates.Count; i++)
            {
                SaveLoadCandidate candidate = candidates[i];
                bool isBackup = candidate.IsBackup;

                if (isBackup)
                    result.HasBackupCandidate = true;
                else
                    result.HasPrimaryCandidate = true;

                if (TryLoadCandidate(
                    slotName,
                    candidate,
                    out SaveData candidateData,
                    out _,
                    out _,
                    out _,
                    out NativeArray<byte> candidateVoxelDeltaSnapshot,
                    out SaveMetadata _,
                    out bool candidateLegacyFormat,
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
                        selectedLegacyFormat = candidateLegacyFormat;
                    }

                    if (candidateVoxelDeltaSnapshot.IsCreated)
                        candidateVoxelDeltaSnapshot.Dispose();
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
            result.SelectedLegacyCompression = selectedLegacyFormat;
            result.DetectedVersion = selectedData != null ? math.max(selectedData.version, 0) : 0;
            result.RequiresMigration = selectedData != null && selectedData.version != SaveData.CurrentVersion;
            result.RecommendedSource = selectedCandidate.IsBackup
                ? $"Backup g{selectedCandidate.BackupGeneration}"
                : "Primary";

            bool recommendedRepair = selectedCandidate.IsBackup
                || selectedLegacyFormat
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
            string compression = result.SelectedLegacyCompression ? ", legacy format" : string.Empty;
            string repair = result.RecommendedRepair ? ", repair recommended" : ", no repair needed";
            return $"Readable from {source}, {migration}{compression}{repair}.";
        }

        private static bool ShouldSelfRepairSlot(SaveLoadCandidate loadedCandidate, bool usedLegacyFormat)
        {
            if (loadedCandidate.IsBackup)
                return true;

            return usedLegacyFormat;
        }

        private bool SelfRepairPrimaryArtifacts(
            string slotName,
            SaveData data,
            SaveMetadata metadata,
            uint[] packedQuestStateWords,
            PersistentWorldDeltaRecord[] persistentWorldItems,
            EcosystemSectorSaveRecord[] ecosystemSectorStates,
            NativeArray<byte> voxelDeltaSnapshot)
        {
            return RepairPrimaryArtifacts(
                slotName,
                data,
                metadata,
                packedQuestStateWords,
                persistentWorldItems,
                ecosystemSectorStates,
                voxelDeltaSnapshot,
                overwritePrimarySave: true);
        }

        private static bool TryLoadCandidate(
            string slotName,
            SaveLoadCandidate candidate,
            out SaveData data,
            out uint[] packedQuestStateWords,
            out PersistentWorldDeltaRecord[] persistentWorldItems,
            out EcosystemSectorSaveRecord[] ecosystemSectorStates,
            out NativeArray<byte> voxelDeltaSnapshot,
            out SaveMetadata metadata,
            out bool usedLegacyFormat,
            out string errorMessage)
        {
            data = null;
            packedQuestStateWords = null;
            persistentWorldItems = null;
            ecosystemSectorStates = null;
            voxelDeltaSnapshot = default;
            metadata = null;
            usedLegacyFormat = false;
            errorMessage = string.Empty;

            string absolutePath = GetPersistentAbsolutePath(candidate.SavePath);
            if (SaveBinaryStorage.IsBinaryContainer(absolutePath))
            {
                return TryLoadBinaryCandidate(slotName, candidate, out data, out packedQuestStateWords, out persistentWorldItems, out ecosystemSectorStates, out voxelDeltaSnapshot, out metadata, out errorMessage);
            }

            errorMessage = $"Unsupported non-binary save artifact '{candidate.SavePath}'.";
            return false;
        }

        private static bool TryLoadBinaryCandidate(
            string slotName,
            SaveLoadCandidate candidate,
            out SaveData data,
            out uint[] packedQuestStateWords,
            out PersistentWorldDeltaRecord[] persistentWorldItems,
            out EcosystemSectorSaveRecord[] ecosystemSectorStates,
            out NativeArray<byte> voxelDeltaSnapshot,
            out SaveMetadata metadata,
            out string errorMessage)
        {
            data = null;
            packedQuestStateWords = null;
            persistentWorldItems = null;
            ecosystemSectorStates = null;
            voxelDeltaSnapshot = default;
            metadata = null;
            errorMessage = string.Empty;

            AcquireReadBuffer(out NativeArray<byte> readBuffer, out bool ownsReadBuffer);
            try
            {
                if (!SaveBinaryStorage.TryLoadSaveData(
                    GetPersistentAbsolutePath(candidate.SavePath),
                    slotName,
                    readBuffer,
                    out data,
                    out packedQuestStateWords,
                    out persistentWorldItems,
                    out ecosystemSectorStates,
                    out voxelDeltaSnapshot,
                    out metadata,
                    out _,
                    out errorMessage))
                {
                    if (voxelDeltaSnapshot.IsCreated)
                        voxelDeltaSnapshot.Dispose();

                    return false;
                }

                return true;
            }
            finally
            {
                ReleaseBuffer(readBuffer, ownsReadBuffer);
            }
        }

        private static bool TryReadCandidateMetadata(
            string slotName,
            SaveLoadCandidate candidate,
            out SaveMetadata metadata,
            out int detectedVersion,
            out bool usedLegacyFormat,
            out string errorMessage)
        {
            metadata = null;
            detectedVersion = 0;
            usedLegacyFormat = false;
            errorMessage = string.Empty;

            string absolutePath = GetPersistentAbsolutePath(candidate.SavePath);
            if (SaveBinaryStorage.IsBinaryContainer(absolutePath))
            {
                AcquireReadBuffer(out NativeArray<byte> readBuffer, out bool ownsReadBuffer);
                try
                {
                    return SaveBinaryStorage.TryReadMetadata(absolutePath, slotName, readBuffer, out metadata, out detectedVersion, out errorMessage);
                }
                finally
                {
                    ReleaseBuffer(readBuffer, ownsReadBuffer);
                }
            }

            errorMessage = $"Unsupported non-binary save artifact '{candidate.SavePath}'.";
            return false;
        }

        private static bool RepairPrimaryArtifacts(
            string slotName,
            SaveData data,
            SaveMetadata metadataSource,
            uint[] packedQuestStateWords,
            PersistentWorldDeltaRecord[] persistentWorldItems,
            EcosystemSectorSaveRecord[] ecosystemSectorStates,
            NativeArray<byte> voxelDeltaSnapshot,
            bool overwritePrimarySave)
        {
            string primarySavePath = GetPrimarySaveFilePath(slotName);
            string tempSavePath = GetTempSaveFilePath(slotName);

            bool changedAnything = false;
            if (overwritePrimarySave || !FileExists(primarySavePath))
            {
                SaveMetadata writeMetadata = CreateMetadataFromData(slotName, data, metadataSource);
                AcquireWriteBuffers(out NativeArray<byte> rawBuffer, out bool ownsRawBuffer, out NativeArray<byte> compressedBuffer, out bool ownsCompressedBuffer);
                NativeArray<PersistentWorldDeltaRecord> persistentWorldItemBuffer = default;
                NativeArray<EcosystemSectorSaveRecord> ecosystemSectorBuffer = default;
                NativeArray<uint> packedQuestStateBuffer = default;
                try
                {
                    if (persistentWorldItems != null && persistentWorldItems.Length > 0)
                    {
                        persistentWorldItemBuffer = new NativeArray<PersistentWorldDeltaRecord>(
                            persistentWorldItems.Length,
                            Allocator.Temp,
                            NativeArrayOptions.UninitializedMemory);
                        persistentWorldItemBuffer.CopyFrom(persistentWorldItems);
                    }

                    if (ecosystemSectorStates != null && ecosystemSectorStates.Length > 0)
                    {
                        ecosystemSectorBuffer = new NativeArray<EcosystemSectorSaveRecord>(
                            ecosystemSectorStates.Length,
                            Allocator.Temp,
                            NativeArrayOptions.UninitializedMemory);
                        ecosystemSectorBuffer.CopyFrom(ecosystemSectorStates);
                    }

                    if (packedQuestStateWords != null && packedQuestStateWords.Length > 0)
                    {
                        packedQuestStateBuffer = new NativeArray<uint>(
                            packedQuestStateWords.Length,
                            Allocator.Temp,
                            NativeArrayOptions.UninitializedMemory);
                        packedQuestStateBuffer.CopyFrom(packedQuestStateWords);
                    }

                    ExecuteVerifiedSavePipeline(
                        slotName,
                        tempSavePath,
                        primarySavePath,
                        writeMetadata,
                        data,
                        persistentWorldItemBuffer,
                        ecosystemSectorBuffer,
                        packedQuestStateBuffer,
                        voxelDeltaSnapshot,
                        rawBuffer,
                        compressedBuffer);
                }
                finally
                {
                    if (persistentWorldItemBuffer.IsCreated)
                        persistentWorldItemBuffer.Dispose();

                    if (ecosystemSectorBuffer.IsCreated)
                        ecosystemSectorBuffer.Dispose();

                    if (packedQuestStateBuffer.IsCreated)
                        packedQuestStateBuffer.Dispose();

                    ReleaseBuffer(rawBuffer, ownsRawBuffer);
                    ReleaseBuffer(compressedBuffer, ownsCompressedBuffer);
                }

                changedAnything = true;
            }

            return changedAnything;
        }

        private static SaveMetadata CreateMetadataFromData(string slotName, SaveData data, SaveMetadata source)
        {
            string sceneName = source != null && !string.IsNullOrEmpty(source.SceneName)
                ? source.SceneName
                : "Unknown";
            string gameVersion = source != null && !string.IsNullOrEmpty(source.GameVersion)
                ? source.GameVersion
                : Application.version;
            float playTimeSeconds = data != null ? (float)data.totalPlayTime : 0f;
            Vector3 playerPosition = data != null ? data.playerStats.GetPosition() : Vector3.zero;

            return new SaveMetadata
            {
                SlotName = slotName,
                GameVersion = gameVersion,
                Timestamp = DateTime.UtcNow.Ticks,
                PlayTimeSeconds = playTimeSeconds,
                SceneName = sceneName,
                PlayerPosition = playerPosition,
                Checksum = source != null ? source.Checksum : string.Empty
            };
        }

        private static void AcquireReadBuffer(out NativeArray<byte> buffer, out bool ownsBuffer)
        {
            SaveManager manager = Instance;
            if (manager != null && manager._savePayloadBuffer.IsCreated)
            {
                buffer = manager._savePayloadBuffer;
                ownsBuffer = false;
                return;
            }

            // COLD ALLOC: NativeArray<byte>[67108864] - fallback raw save read buffer when SaveManager instance is unavailable - owner: SaveManager
            buffer = new NativeArray<byte>(SaveBinaryStorage.RawPayloadCapacityBytes, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            ownsBuffer = true;
        }

        private double ResolveCurrentPlayTimeSeconds()
        {
            return _totalPlayTime + (Time.realtimeSinceStartupAsDouble - _sessionStartTime);
        }

        private static void AcquireWriteBuffers(
            out NativeArray<byte> rawBuffer,
            out bool ownsRawBuffer,
            out NativeArray<byte> compressedBuffer,
            out bool ownsCompressedBuffer)
        {
            SaveManager manager = Instance;
            if (manager != null && manager._savePayloadBuffer.IsCreated && manager._compressedSaveBuffer.IsCreated)
            {
                rawBuffer = manager._savePayloadBuffer;
                compressedBuffer = manager._compressedSaveBuffer;
                ownsRawBuffer = false;
                ownsCompressedBuffer = false;
                return;
            }

            // COLD ALLOC: NativeArray<byte>[67108864] - fallback raw save write buffer when SaveManager instance is unavailable - owner: SaveManager
            rawBuffer = new NativeArray<byte>(SaveBinaryStorage.RawPayloadCapacityBytes, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            // COLD ALLOC: NativeArray<byte>[67378176] - fallback compressed save write buffer when SaveManager instance is unavailable - owner: SaveManager
            compressedBuffer = new NativeArray<byte>(SaveBinaryStorage.MaxCompressedPayloadBytes, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            ownsRawBuffer = true;
            ownsCompressedBuffer = true;
        }

        private static void ReleaseBuffer(NativeArray<byte> buffer, bool ownsBuffer)
        {
            if (ownsBuffer && buffer.IsCreated)
                buffer.Dispose();
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
            record.LastLoadBackupGeneration = result.SelectedBackupSource ? math.max(1, result.SelectedBackupGeneration) : 0;
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
            record.LastLoadBackupGeneration = result.UsedBackupSource ? math.max(1, result.SourceBackupGeneration) : 0;
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

            bool hasPrimarySave = FileExists(primarySavePath);
            bool hasPrimaryMetadata = false;
            bool hasThumbnail = File.Exists(SaveThumbnailSystem.GetThumbnailPath(slotName));
            bool hasBackupSave = false;
            bool hasBackupMetadata = false;

            for (int generation = 1; generation <= backupRetention; generation++)
            {
                if (!hasBackupSave && FileExists(GetBackupSaveFilePath(slotName, generation)))
                    hasBackupSave = true;
            }

            if (!hasPrimarySave && !hasBackupSave)
                return null;

            SaveMetadata metadata = null;
            bool metadataRecoveredFromBackup = false;
            bool metadataSynthesized = false;
            bool metadataCorrupted = false;

            if (hasPrimarySave)
            {
                if (TryReadCandidateMetadata(
                    slotName,
                    new SaveLoadCandidate(primarySavePath, false, 0),
                    out SaveMetadata primaryMetadata,
                    out _,
                    out _,
                    out _))
                {
                    metadata = primaryMetadata;
                    hasPrimaryMetadata = primaryMetadata != null;
                }
                else
                {
                    metadataCorrupted = true;
                }
            }

            for (int generation = 1; generation <= backupRetention; generation++)
            {
                string backupSavePath = GetBackupSaveFilePath(slotName, generation);
                if (!FileExists(backupSavePath))
                    continue;

                if (TryReadCandidateMetadata(
                    slotName,
                    new SaveLoadCandidate(backupSavePath, true, generation),
                    out SaveMetadata backupMetadata,
                    out _,
                    out _,
                    out _))
                {
                    hasBackupMetadata = backupMetadata != null;
                    if (metadata == null && backupMetadata != null)
                    {
                        metadata = backupMetadata;
                        metadataRecoveredFromBackup = hasPrimarySave && !hasPrimaryMetadata;
                    }
                }
                else
                {
                    metadataCorrupted = true;
                }
            }

            long lastWriteTicksUtc = 0L;
            long primaryBytes = GetPersistentFileSize(primarySavePath);
            long backupBytes = 0L;

            UpdateLastWrite(primarySavePath, ref lastWriteTicksUtc);
            UpdateLastWrite(SaveSlotMaintenanceRecord.GetPath(slotName), ref lastWriteTicksUtc);
            UpdateLastWrite(Path.GetFileName(SaveThumbnailSystem.GetThumbnailPath(slotName)), ref lastWriteTicksUtc);

            for (int generation = 1; generation <= backupRetention; generation++)
            {
                string backupSavePath = GetBackupSaveFilePath(slotName, generation);
                backupBytes += GetPersistentFileSize(backupSavePath);
                UpdateLastWrite(backupSavePath, ref lastWriteTicksUtc);
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
            else if (hasPrimarySave && hasPrimaryMetadata && hasBackupSave && hasBackupMetadata)
            {
                integrityState = SaveSlotIntegrityState.HealthyWithBackup;
            }
            else if (hasPrimarySave && hasPrimaryMetadata)
            {
                integrityState = SaveSlotIntegrityState.Healthy;
            }
            else if (!hasPrimarySave && hasBackupSave && hasBackupMetadata)
            {
                integrityState = SaveSlotIntegrityState.BackupOnly;
            }
            else if (metadataCorrupted && !metadataSynthesized)
            {
                integrityState = SaveSlotIntegrityState.CorruptedMetadata;
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
