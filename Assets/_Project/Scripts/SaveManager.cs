// ============================================================================
// HECTON-8 — SaveManager.cs
// Save persistence service. Runtime owner is injected through GlobalRegistry.
//
// ARHITEKTURA:
//   • Reestr ISaveable cherez explicit registration (zero GC pri save/load).
//   • XXHash3 checksums for header/payload integrity.
//   • Unity 6 Awaitable API: BackgroundThreadAsync / MainThreadAsync.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Bootstrap;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Modding;
using Hecton8.Quest;
using Hecton8.UI;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Hecton8.SaveSystem
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-8000)]
    public sealed class SaveManager : MonoBehaviour, ISaveService, IUpdatable, ISlowTickable, ILateFrameTickable, IServiceHeartbeat, IServiceShutdown
    {
        private const long MainThreadSnapshotBudgetMs = 50L;
        private static readonly long PreCompressionYieldBudgetTicks = Math.Max(1L, Stopwatch.Frequency / 500L);
        private static readonly long LoadApplyFrameBudgetTicks = HydrationScheduler.FrameBudgetTicks;
        private const float SafeAupSnapGroundPaddingMeters = 0.28f;
        private const float SafeAupSnapMinimumLiftMeters = 0.35f;
        private const string CriticalSectorCorruptionMessage = "CRITICAL ERROR: LOCALIZED DATA CORRUPTION. TERRAIN RE-INITIALIZED.";
        private const string GeologicalAnomalyDetectedMessage = "UNSTABLE REALITY";
        private const int MaxRegisteredSaveables = 256;
        private const int MaxSaveSlotNameLength = 48;
        private const int SaveSlotScratchCapacity = 8;
        private const int MaxSaveLoadCandidateCount = 9;
        private const string InvalidSlotNameReason = "Invalid save slot name.";
        private const string InvalidSlotFileStem = "slot_invalid";
        private static readonly long CompressionThrottleBudgetTicks = Math.Max(1L, Stopwatch.Frequency / 100L);
        private const string NativeMemoryOwner = nameof(SaveManager);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Session;
        private const NativeAllocationLifetime NativeTransientMemoryLifetime = NativeAllocationLifetime.TransientArena;

        // ══════════════════════════════════════════════════════════
        //  SAVE STATE
        // ══════════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════════
        //  SERVICE STATE
        // ══════════════════════════════════════════════════════════

        public bool IsInitialized => _serviceRegistered && ReferenceEquals(GlobalRegistry.Save, this);
        public ServiceHeartbeatState HeartbeatState => IsInitialized ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;
        public bool IsServiceReady => IsInitialized;
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

        // COLD ALLOC: ISaveable[256] — fixed persistence registry prevents List resize during scene registration — owner: SaveManager
        private readonly ISaveable[] _saveables = new ISaveable[MaxRegisteredSaveables];
        // COLD ALLOC: List<IndexedSectorEntryInfo>[128] — reusable indexed-save directory probe scratch — owner: SaveManager
        private readonly SaveBinaryStorage.IndexedSectorEntryInfo[] _indexedSectorDirectoryScratch = new SaveBinaryStorage.IndexedSectorEntryInfo[128];
        // COLD ALLOC: List<SaveSlotInfo>[8] - instance-owned metadata projection scratch - owner: SaveManager
        private readonly SaveSlotInfo[] _saveSlotInfoScratch = new SaveSlotInfo[SaveSlotScratchCapacity];
        // COLD ALLOC: SaveLoadCandidate[9] - instance-owned load fallback chain scratch - owner: SaveManager
        private NativeArray<SaveLoadCandidate> _loadCandidateScratch;
        private int _saveableCount;
        private bool _registryDirty;
        private bool _saveableCapacityWarningLogged;
        private double _sessionStartTime;
        private double _totalPlayTime;
        private bool _isBusy;

        private static readonly IComparer<ISaveable> SavePriorityComparer = new SavePriorityComparerImpl();
        private static readonly IComparer<ISaveable> LoadPriorityComparer = new LoadPriorityComparerImpl();
        private static readonly Comparison<SaveSlotInfo> SaveSlotTimestampDescendingComparison = CompareSaveSlotTimestampDescending;
        // COLD ALLOC: object[1] - serializes static repair/audit save-slot scratch - owner: SaveManager
        private static readonly object SaveSlotInfoScratchSync = new object();
        // COLD ALLOC: List<SaveSlotInfo>[8] - static repair/audit slot enumeration scratch - owner: SaveManager
        private static readonly SaveSlotInfo[] SaveSlotInfoScratch = new SaveSlotInfo[SaveSlotScratchCapacity];
        // COLD ALLOC: object[1] - serializes static repair/audit candidate scratch - owner: SaveManager
        private static readonly object SaveLoadCandidateScratchSync = new object();
        // COLD ALLOC: SaveLoadCandidate[9] - static repair/audit load fallback scratch - owner: SaveManager
        private static NativeArray<SaveLoadCandidate> SaveLoadCandidateScratch;
        private static string s_persistentDataPathRoot;

        private sealed class SavePriorityComparerImpl : IComparer<ISaveable>
        {
            public int Compare(ISaveable x, ISaveable y)
            {
                return CompareSavePriority(x, y);
            }
        }

        private sealed class LoadPriorityComparerImpl : IComparer<ISaveable>
        {
            public int Compare(ISaveable x, ISaveable y)
            {
                return CompareLoadPriority(x, y);
            }
        }

        private static int CompareSaveSlotTimestampDescending(SaveSlotInfo a, SaveSlotInfo b)
        {
            long left = a != null && a.Metadata != null ? a.Metadata.Timestamp : a != null ? a.LastWriteTicksUtc : 0L;
            long right = b != null && b.Metadata != null ? b.Metadata.Timestamp : b != null ? b.LastWriteTicksUtc : 0L;
            return right.CompareTo(left);
        }

        private NativeArray<byte> _savePayloadBuffer;
        private NativeArray<byte> _compressedSaveBuffer;
        private ulong _expectedIntegrityPayloadHash64;
        private int _integrityPayloadLength;
        private bool _updatableRegistered;
        private bool _slowTickRegistered;
        private bool _lateFrameRegistered;
        private bool _serviceRegistered;
        private bool _compressionThrottleLateFrameArmed;
        private int _compressionThrottleReleaseFrame;
        private int _slowTickSequence;
        private long _lastSaveCompressionPipelineTicks;
        private LoadingScreenController _cachedLoadingScreenController;
        private string _integritySlotName;

        private sealed class MemoryCorruptionException : Exception
        {
            public MemoryCorruptionException(string message) : base(message) { }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 16)]
        private readonly struct SaveLoadCandidate
        {
            private const int BackupFlag = 1 << 0;

            public readonly int Flags;
            public readonly int BackupGeneration;
            public readonly int Reserved0;
            public readonly int Reserved1;

            public bool IsBackup => (Flags & BackupFlag) != 0;

            private SaveLoadCandidate(int flags, int backupGeneration)
            {
                Flags = flags;
                BackupGeneration = backupGeneration;
                Reserved0 = 0;
                Reserved1 = 0;
            }

            public static SaveLoadCandidate Primary()
            {
                return new SaveLoadCandidate(0, 0);
            }

            public static SaveLoadCandidate Backup(int backupGeneration)
            {
                return new SaveLoadCandidate(BackupFlag, math.max(1, backupGeneration));
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
            SaveManager manager = GlobalRegistry.SaveRuntime;
            if (manager != null)
            {
                switch (category)
                {
                    case SaveSlotCategory.Auto:
                        return math.clamp(manager.autoBackupGenerations, 1, 8);
                    case SaveSlotCategory.Quick:
                        return math.clamp(manager.quickBackupGenerations, 1, 8);
                    default:
                        return math.clamp(manager.manualBackupGenerations, 1, 8);
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
            SaveManager manager = GlobalRegistry.SaveRuntime;
            if (manager != null)
            {
                return math.clamp(
                    math.max(manager.manualBackupGenerations, math.max(manager.autoBackupGenerations, manager.quickBackupGenerations)),
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

        public static bool IsSafeSlotName(string slotName)
        {
            return TryResolveSafeSlotName(slotName, out _);
        }

        internal static bool TryResolveSafeSlotName(string slotName, out string safeSlotName)
        {
            safeSlotName = string.Empty;
            if (string.IsNullOrEmpty(slotName))
                return false;

            if ((uint)slotName.Length > MaxSaveSlotNameLength)
                return false;

            for (int i = 0; i < slotName.Length; i++)
            {
                char character = slotName[i];
                bool valid =
                    (character >= 'a' && character <= 'z') ||
                    (character >= 'A' && character <= 'Z') ||
                    (character >= '0' && character <= '9') ||
                    character == '_' ||
                    character == '-';

                if (!valid)
                    return false;
            }

            if (IsReservedManualSlotPattern(slotName) &&
                !SaveEvents.IsKnownManualSlotName(slotName))
            {
                return false;
            }

            safeSlotName = slotName;
            return true;
        }

        internal static string ResolveSafeSlotFileStem(string slotName)
        {
            return TryResolveSafeSlotName(slotName, out string safeSlotName)
                ? safeSlotName
                : InvalidSlotFileStem;
        }

        private static bool IsReservedManualSlotPattern(string slotName)
        {
            const string manualSlotPrefix = "slot_";
            if (string.IsNullOrEmpty(slotName) ||
                slotName.Length <= manualSlotPrefix.Length ||
                !slotName.StartsWith(manualSlotPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            for (int i = manualSlotPrefix.Length; i < slotName.Length; i++)
            {
                char character = slotName[i];
                if (character < '0' || character > '9')
                    return false;
            }

            return true;
        }

        private void Awake()
        {
            _sessionStartTime = Time.realtimeSinceStartupAsDouble;
            CachePersistentDataPathRoot();
            InitializeNativeBuffers();
            SaveBinaryStorage.WarmRuntime();
        }

        private void OnEnable()
        {
            if (!_serviceRegistered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            TryRegisterDispatcherLanes();
        }

        private void OnDisable()
        {
            UnregisterDispatcherLanes();
        }

        private void OnDestroy()
        {
            ShutdownServiceState();
        }

        public void OnServiceShutdown()
        {
            ShutdownServiceState();
        }

        private void UnregisterDispatcherLanes()
        {
            if (_updatableRegistered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
                _updatableRegistered = false;
            }

            if (_slowTickRegistered)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
                _slowTickRegistered = false;
            }

            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);
                _lateFrameRegistered = false;
            }
        }

        private void ShutdownServiceState()
        {
            UnregisterDispatcherLanes();

            if (_serviceRegistered && ReferenceEquals(GlobalRegistry.Save, this))
                GlobalRegistry.UnregisterSaveService(this);

            _serviceRegistered = false;
            _isBusy = false;
            _compressionThrottleLateFrameArmed = false;
            _compressionThrottleReleaseFrame = 0;
            _slowTickSequence = 0;
            _lastSaveCompressionPipelineTicks = 0L;
            _cachedLoadingScreenController = null;
            if (_saveableCount > 0)
                Array.Clear(_saveables, 0, _saveableCount);
            _saveableCount = 0;
            _debugRegisteredCount = 0;
            _registryDirty = false;

            DisposeNativeArray(ref _savePayloadBuffer);
            DisposeNativeArray(ref _compressedSaveBuffer);
            DisposeNativeArray(ref _loadCandidateScratch);
            DisposeStaticLoadCandidateScratch();

            DisposeIntegrityResources();
        }

        public void InitializeService()
        {
            CachePersistentDataPathRoot();
            InitializeNativeBuffers();

            if (_serviceRegistered)
            {
                if (isActiveAndEnabled && Application.isPlaying && GlobalRegistry.Dispatcher != null)
                    TryRegisterDispatcherLanes();

                return;
            }

            GlobalRegistry.RegisterSaveService(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.SaveRuntime, this);

            if (isActiveAndEnabled && Application.isPlaying && GlobalRegistry.Dispatcher != null)
                TryRegisterDispatcherLanes();
        }

        private void TryRegisterDispatcherLanes()
        {
            if (!_updatableRegistered)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Core);
                _updatableRegistered = GlobalRegistry.Updatables.Contains(this);
            }

            if (!_slowTickRegistered)
            {
                GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Core);
                _slowTickRegistered = GlobalRegistry.SlowTickables.Contains(this);
            }

            if (!_lateFrameRegistered)
            {
                GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Core);
                _lateFrameRegistered = SystemDispatcher.GetLateFrameLane(PriorityLayer.Core).Contains(this);
            }
        }

        private void InitializeNativeBuffers()
        {
            if (!_savePayloadBuffer.IsCreated)
            {
                // COLD ALLOC: NativeArray<byte>[67108864] — raw binary save staging buffer for save payload assembly — owner: SaveManager
                _savePayloadBuffer = new NativeArray<byte>(SaveBinaryStorage.RawPayloadCapacityBytes, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                NativeMemorySentinel.RegisterNativeArray(_savePayloadBuffer, NativeMemoryOwner, nameof(_savePayloadBuffer), NativeMemoryLifetime);
            }

            if (!_compressedSaveBuffer.IsCreated)
            {
                // COLD ALLOC: NativeArray<byte>[71303168] — protected 16KB LZ4 block-compressed save payload buffer for 64MB raw save budget — owner: SaveManager
                _compressedSaveBuffer = new NativeArray<byte>(SaveBinaryStorage.MaxCompressedPayloadBytes, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                NativeMemorySentinel.RegisterNativeArray(_compressedSaveBuffer, NativeMemoryOwner, nameof(_compressedSaveBuffer), NativeMemoryLifetime);
            }

            if (!_loadCandidateScratch.IsCreated)
            {
                // COLD ALLOC: NativeArray<SaveLoadCandidate>[9] - unmanaged load fallback descriptors - owner: SaveManager
                _loadCandidateScratch = new NativeArray<SaveLoadCandidate>(MaxSaveLoadCandidateCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(_loadCandidateScratch, NativeMemoryOwner, nameof(_loadCandidateScratch), NativeMemoryLifetime);
            }
        }

        private static void EnsureStaticLoadCandidateScratch()
        {
            if (SaveLoadCandidateScratch.IsCreated)
                return;

            // COLD ALLOC: NativeArray<SaveLoadCandidate>[9] - static repair/audit fallback descriptors - owner: SaveManager
            SaveLoadCandidateScratch = new NativeArray<SaveLoadCandidate>(MaxSaveLoadCandidateCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(SaveLoadCandidateScratch, NativeMemoryOwner, nameof(SaveLoadCandidateScratch), NativeMemoryLifetime);
        }

        private static void DisposeStaticLoadCandidateScratch()
        {
            lock (SaveLoadCandidateScratchSync)
            {
                if (!SaveLoadCandidateScratch.IsCreated)
                    return;

                NativeMemorySentinel.UnregisterNativeArray(SaveLoadCandidateScratch);
                SaveLoadCandidateScratch.Dispose();
                SaveLoadCandidateScratch = default;
            }
        }

        public void Tick(float deltaTime)
        {
        }

        public void SlowTick()
        {
            unchecked
            {
                _slowTickSequence++;
                if (_slowTickSequence == int.MinValue)
                    _slowTickSequence = 0;
            }
        }

        public void LateFrameTick()
        {
            if (!_compressionThrottleLateFrameArmed || Time.frameCount < _compressionThrottleReleaseFrame)
                return;

            _compressionThrottleLateFrameArmed = false;
        }

        private void StageIntegrityPayload(NativeArray<byte> payloadBytes, int payloadLength, ulong expectedHash64, string slotName)
        {
            if (!payloadBytes.IsCreated || payloadLength <= 0 || payloadLength > payloadBytes.Length)
                return;

            _integrityPayloadLength = payloadLength;
            _expectedIntegrityPayloadHash64 = expectedHash64;
            _integritySlotName = slotName ?? string.Empty;
        }

        private void DisposeIntegrityResources()
        {
            _integrityPayloadLength = 0;
            _expectedIntegrityPayloadHash64 = 0UL;
            _integritySlotName = string.Empty;
        }

        private static void RegisterVoxelDeltaSnapshot(NativeArray<byte> snapshot, string label)
        {
            RegisterTransientNativeArray(snapshot, label);
        }

        private static void RegisterTransientNativeArray<T>(NativeArray<T> array, string label) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeTransientMemoryLifetime);
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array, JobHandle dependency = default, bool deferDisposal = false) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            if (deferDisposal)
                array.Dispose(dependency);
            else
                array.Dispose();
            array = default;
        }

        public void Register(ISaveable saveable)
        {
            if (saveable == null) return;

            for (int i = 0; i < _saveableCount; i++)
            {
                if (ReferenceEquals(_saveables[i], saveable))
                {
                    _debugRegisteredCount = _saveableCount;
                    return;
                }
            }

            if (_saveableCount >= MaxRegisteredSaveables)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (!_saveableCapacityWarningLogged)
                {
                    Debug.LogError("[SaveManager] Saveable registry capacity exceeded. Increase MaxRegisteredSaveables or split save ownership.");
                    _saveableCapacityWarningLogged = true;
                }
#endif
                _debugRegisteredCount = _saveableCount;
                return;
            }

            _saveables[_saveableCount] = saveable;
            _saveableCount++;
            _registryDirty = true;
            _debugRegisteredCount = _saveableCount;
        }

        public void Unregister(ISaveable saveable)
        {
            if (saveable == null) return;

            for (int i = 0; i < _saveableCount; i++)
            {
                if (!ReferenceEquals(_saveables[i], saveable))
                    continue;

                _saveableCount--;
                _saveables[i] = _saveables[_saveableCount];
                _saveables[_saveableCount] = null;
                _registryDirty = true;
                break;
            }

            _debugRegisteredCount = _saveableCount;
        }

        private void SortRegistryIfDirty(IComparer<ISaveable> comparer)
        {
            PruneDeadSaveables();
            if (!_registryDirty) return;

            Array.Sort(_saveables, 0, _saveableCount, comparer);
            _registryDirty = false;
        }

        private void PruneDeadSaveables()
        {
            int writeIndex = 0;
            for (int readIndex = 0; readIndex < _saveableCount; readIndex++)
            {
                ISaveable saveable = _saveables[readIndex];
                if (!IsAlive(saveable))
                {
                    _registryDirty = true;
                    continue;
                }

                _saveables[writeIndex] = saveable;
                writeIndex++;
            }

            if (writeIndex == _saveableCount)
                return;

            Array.Clear(_saveables, writeIndex, _saveableCount - writeIndex);
            _saveableCount = writeIndex;
            _debugRegisteredCount = _saveableCount;
        }

        private static int CompareSavePriority(ISaveable a, ISaveable b)
        {
            if (ReferenceEquals(a, b))
                return 0;

            bool aAlive = IsAlive(a);
            bool bAlive = IsAlive(b);
            if (!aAlive)
                return bAlive ? 1 : 0;
            if (!bAlive)
                return -1;

            return a.SavePriority.CompareTo(b.SavePriority);
        }

        private static int CompareLoadPriority(ISaveable a, ISaveable b)
        {
            if (ReferenceEquals(a, b))
                return 0;

            bool aAlive = IsAlive(a);
            bool bAlive = IsAlive(b);
            if (!aAlive)
                return bAlive ? 1 : 0;
            if (!bAlive)
                return -1;

            return a.LoadPriority.CompareTo(b.LoadPriority);
        }

        private static bool IsAlive(ISaveable saveable)
        {
            if (saveable == null) return false;
            if (saveable is UnityEngine.Object unityObj) return unityObj != null;
            return true;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogInfo(string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(message);
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogWarning(string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(message);
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogError(string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError(message);
#endif
        }

        // ══════════════════════════════════════════════════════════
        //  ASYNC SAVE/LOAD
        // ══════════════════════════════════════════════════════════

        public async Awaitable SaveGameAsync(string slotName)
        {
            CachePersistentDataPathRoot();
            LastOperationSucceeded = false;
            LastOperationError = string.Empty;
            LastOperationSlot = string.Empty;
            LastLoadUsedBackup = false;
            LastLoadBackupGeneration = 0;
            LastLoadSelfRepaired = false;
            LastLoadUsedLegacyCompression = false;

            if (!TryResolveSafeSlotName(slotName, out slotName))
            {
                LastOperationError = InvalidSlotNameReason;
                LogWarning("[SaveManager] Ignored save request: invalid slot name.");
                SaveEvents.RaiseSaveFailed(string.Empty, InvalidSlotNameReason);
                return;
            }

            LastOperationSlot = slotName;

            if (_isBusy)
            {
                const string reason = "Save already in progress.";
                LastOperationError = reason;
                LogWarning($"[SaveManager] Ignored save request for '{slotName}': {reason}");
                SaveEvents.RaiseSaveFailed(slotName, reason);
                return;
            }

            if (HectonFloatingOrigin.IsShiftInProgress || HectonFloatingOrigin.IsPhysicsPausedForShift)
            {
                const string reason = "Save blocked during floating-origin shift.";
                LastOperationError = reason;
                LogWarning($"[SaveManager] Ignored save request for '{slotName}': {reason}");
                SaveEvents.RaiseSaveFailed(slotName, reason);
                return;
            }

            SaveThumbnailSystem.CaptureThumbnail(slotName);
            _isBusy = true;
            SaveEvents.RaiseSaveStarted(slotName);

            var totalTimer = Stopwatch.StartNew();
            var snapshotTimer = Stopwatch.StartNew();
            double playTime = ResolveCurrentPlayTimeSeconds();
            SaveData data = SaveData.CreateNew(playTime);
            PersistentWorldRegistry persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;
            NativeArray<PersistentWorldDeltaRecord> persistentWorldDeltaSnapshot = default;
            NativeArray<EcosystemSectorSaveRecord> ecosystemSectorSnapshot = default;
            NativeArray<uint> packedQuestStateSnapshot = default;
            QuestSaveHeader packedQuestSaveHeader = default;
            NativeArray<byte> voxelDeltaSnapshot = default;

            try
            {
                SortRegistryIfDirty(SavePriorityComparer);
                for (int i = 0; i < _saveableCount; i++)
                {
                    ISaveable saveable = _saveables[i];
                    if (!IsAlive(saveable))
                        continue;

                    if (saveable is VoxelDeltaProcessor voxelDeltaProcessor)
                    {
                        if (voxelDeltaSnapshot.IsCreated)
                            DisposeNativeArray(ref voxelDeltaSnapshot);

                        voxelDeltaSnapshot = voxelDeltaProcessor.CaptureNativeSnapshot(Allocator.Persistent);
                        RegisterVoxelDeltaSnapshot(voxelDeltaSnapshot, "voxelDeltaSnapshot");
                        continue;
                    }

                    saveable.PopulateSaveData(data);
                }

                StampRuntimeWorldSeed(data);
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
                long saveTimestampTicks = DateTime.UtcNow.Ticks;
                QuestManager questManager = GlobalRegistry.Quest;
                if (questManager != null)
                {
                    packedQuestStateSnapshot = questManager.CapturePackedStateSnapshot(
                        Allocator.Persistent,
                        out packedQuestSaveHeader,
                        saveTimestampTicks);
                    RegisterTransientNativeArray(packedQuestStateSnapshot, "packedQuestStateSnapshot");
                }

                SaveMetadata metadata = new SaveMetadata
                {
                    SlotName = slotName,
                    GameVersion = Application.version,
                    Timestamp = saveTimestampTicks,
                    PlayTimeSeconds = (float)playTime,
                    SceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                    PlayerPosition = data.playerStats.GetPosition(),
                    WorldSeed = data.ecosystemState.worldSeed,
                    WorldGenerationVersionId = data.ecosystemState.worldGenerationVersionId
                };

                snapshotTimer.Stop();
                WarnIfSnapshotBudgetExceeded(slotName, snapshotTimer.ElapsedMilliseconds);

                string tempPath = GetTempSaveFilePath(slotName);
                if (divergenceSnapshotTimer.ElapsedTicks > PreCompressionYieldBudgetTicks)
                    await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync();

                await Awaitable.MainThreadAsync();
                SaveContextFrameData frameData = SaveContextFrameData.CaptureMainThread();
                SaveEvents.RaiseMappedWriteStarted(slotName);
                await Awaitable.BackgroundThreadAsync();

                ulong payloadHash64;
                int rawPayloadLength;
                long compressionPipelineStartTicks = Stopwatch.GetTimestamp();

                ExecuteVerifiedSavePipeline(
                    slotName,
                    tempPath,
                    GetPrimarySaveFilePath(slotName),
                    metadata,
                    data,
                    persistentWorldDeltaSnapshot,
                    ecosystemSectorSnapshot,
                    packedQuestSaveHeader,
                    packedQuestStateSnapshot,
                    voxelDeltaSnapshot,
                    _savePayloadBuffer,
                    _compressedSaveBuffer,
                    out payloadHash64,
                    out rawPayloadLength);

                long compressionPipelineElapsedTicks = Stopwatch.GetTimestamp() - compressionPipelineStartTicks;
                await Awaitable.MainThreadAsync();
                RegisterCompressionPipelineElapsed(compressionPipelineElapsedTicks, in frameData);
                StageIntegrityPayload(_savePayloadBuffer, rawPayloadLength, payloadHash64, slotName);
                int backupRetention = GetBackupRetentionCount(slotName);
                SaveSlotIntegrityState savedIntegrity = backupRetention > 0
                    ? SaveSlotIntegrityState.HealthyWithBackup
                    : SaveSlotIntegrityState.Healthy;
                RecordSuccessfulSave(slotName, data.version, savedIntegrity);
                NotifyMappedInventoryWritesCommitted();

                LastOperationSucceeded = true;
                LogInfo($"[SaveManager] Saved '{slotName}' (XXH3-64: {metadata.Checksum}) in {totalTimer.ElapsedMilliseconds}ms");
                SaveEvents.RaiseSaveCompleted(slotName);
            }
            catch (Exception ex)
            {
                await Awaitable.MainThreadAsync();
                RecordFailure(slotName, "save", ex.Message);
                LastOperationError = ex.Message;
                LogError("[SaveManager] Save failed: " + ex);
                SaveEvents.RaiseSaveFailed(slotName, ex.Message);
            }
            finally
            {
                if (packedQuestStateSnapshot.IsCreated)
                    DisposeNativeArray(ref packedQuestStateSnapshot);

                if (voxelDeltaSnapshot.IsCreated)
                    DisposeNativeArray(ref voxelDeltaSnapshot);

                _isBusy = false;
            }
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void WarnIfSnapshotBudgetExceeded(string slotName, long snapshotElapsedMs)
        {
            if (snapshotElapsedMs <= MainThreadSnapshotBudgetMs)
                return;

            LogWarning(
                $"[SaveManager] Main-thread snapshot for '{slotName}' took {snapshotElapsedMs}ms. " +
                $"Budget is {MainThreadSnapshotBudgetMs}ms. Snapshot purity is pending verification.");
        }

        private readonly struct SaveContextFrameData
        {
            public readonly int FrameCount;

            private SaveContextFrameData(int frameCount)
            {
                FrameCount = frameCount;
            }

            public static SaveContextFrameData CaptureMainThread()
            {
                return new SaveContextFrameData(Time.frameCount);
            }
        }

        private void RegisterCompressionPipelineElapsed(long elapsedTicks, in SaveContextFrameData frameData)
        {
            _lastSaveCompressionPipelineTicks = elapsedTicks > 0L ? elapsedTicks : 0L;
            if (elapsedTicks <= CompressionThrottleBudgetTicks)
                return;

            _compressionThrottleReleaseFrame = frameData.FrameCount + 1;
            _compressionThrottleLateFrameArmed = true;
        }

        private void NotifyMappedInventoryWritesCommitted()
        {
            for (int i = 0; i < _saveableCount; i++)
            {
                if (_saveables[i] is PlayerInventory inventory && inventory != null)
                    inventory.NotifyMappedInventoryWriteCommitted();
            }
        }

        private static void StampRuntimeWorldSeed(SaveData data)
        {
            if (data == null)
                return;

            IWorldSeedProvider seedProvider = GlobalRegistry.WorldSeedProvider;
            if (seedProvider == null || !seedProvider.IsInitialized)
                return;

            data.ecosystemState.worldSeed = seedProvider.RuntimeWorldSeed;
            data.ecosystemState.worldGenerationVersionId = math.max(0, seedProvider.RuntimeWorldGenerationVersionId);
        }

        private static void ValidateRuntimeWorldSeed(SaveData data)
        {
            if (data == null)
                return;

            int savedSeed = data.ecosystemState.worldSeed;
            int savedWorldGenerationVersion = data.ecosystemState.worldGenerationVersionId;
            if (savedSeed == 0 && savedWorldGenerationVersion == 0)
                return;

            IWorldSeedProvider seedProvider = GlobalRegistry.WorldSeedProvider;
            if (seedProvider == null || !seedProvider.IsInitialized)
                return;

            int runtimeSeed = seedProvider.RuntimeWorldSeed;
            int runtimeWorldGenerationVersion = math.max(0, seedProvider.RuntimeWorldGenerationVersionId);
            bool seedMismatch = savedSeed != 0 && runtimeSeed != 0 && savedSeed != runtimeSeed;
            bool versionMismatch = savedWorldGenerationVersion > 0 &&
                                   savedWorldGenerationVersion != runtimeWorldGenerationVersion;
            if (!seedMismatch && !versionMismatch)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogWarning(
                "[SaveManager] Geological Anomaly: saved world seed " + savedSeed +
                " / version " + savedWorldGenerationVersion +
                " != runtime world seed " + runtimeSeed +
                " / version " + runtimeWorldGenerationVersion + ".");
#endif
            NotificationEvents.PushWarning(GeologicalAnomalyDetectedMessage);
            PlayerSignalEvents.RaiseTraumaHudSignal(new TraumaHudSignal(
                0.78f,
                0.12f,
                1f,
                1f,
                false));
        }

        private void ReportLoadPipelineStage(LoadingPipelineStage stage, float progress01)
        {
            if (!Application.isPlaying)
                return;

            LoadingScreenController loadingScreen = ResolveLoadingScreenController();
            if (loadingScreen == null)
                return;

            if (stage != LoadingPipelineStage.Completed)
                loadingScreen.Show();

            loadingScreen.UpdateProgress(progress01);
            loadingScreen.UpdatePipelineStage(stage);

            if (stage == LoadingPipelineStage.Completed)
                loadingScreen.Hide();
        }

        private void HideLoadingPipelineScreen()
        {
            if (!Application.isPlaying)
                return;

            LoadingScreenController loadingScreen = ResolveLoadingScreenController();
            if (loadingScreen != null)
                loadingScreen.Hide();
        }

        private LoadingScreenController ResolveLoadingScreenController()
        {
            if (_cachedLoadingScreenController != null)
                return _cachedLoadingScreenController;

            _cachedLoadingScreenController = GlobalRegistry.LoadingScreen;
            return _cachedLoadingScreenController;
        }

        private static void ReportCriticalSectorCorruptionDialog()
        {
            NotificationEvents.PushCritical(CriticalSectorCorruptionMessage);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogWarning($"[SaveManager] {CriticalSectorCorruptionMessage}");
#endif
        }

        private static bool TryApplySafeAupSnapOnLoad(SaveData data)
        {
            if (data == null)
                return false;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            Transform playerTransform = playerContext != null ? playerContext.PlayerTransform : null;
            if (playerTransform == null && !GameBootstrapper.TryGetCurrentPlayerTransform(out playerTransform))
                return false;

            Vector3 savedRuntimePosition = data.playerStats.GetPosition();
            if (!IsFinite(savedRuntimePosition))
                return false;

            AbsoluteUniversePosition savedAup = AbsoluteUniversePosition.FromRuntimePosition(savedRuntimePosition);
            float3 resolvedRuntime3 = savedAup.ToRuntimeFloat3();
            if (!math.all(math.isfinite(resolvedRuntime3)))
                return false;

            Vector3 resolvedRuntimePosition = new Vector3(
                resolvedRuntime3.x,
                resolvedRuntime3.y,
                resolvedRuntime3.z);
            if (!TryResolveCachedSafeAupSnapY(in savedAup, in resolvedRuntime3, out float safeY))
                return false;

            Vector3 snappedPosition = new Vector3(resolvedRuntimePosition.x, safeY, resolvedRuntimePosition.z);
            Quaternion savedRotation = data.playerStats.GetRotation();
            if (!IsFinite(savedRotation))
                savedRotation = playerTransform.rotation;

            Vector3 savedVelocity = data.playerStats.GetVelocity();
            if (!IsFinite(savedVelocity))
                savedVelocity = Vector3.zero;

            if (savedVelocity.y < 0f)
                savedVelocity.y = 0f;

            Rigidbody playerBody = playerContext != null ? playerContext.PlayerRigidbody : playerTransform.GetComponent<Rigidbody>();
            HectonFloatingOrigin.BeginSafeTeleportProtocol();
            try
            {
                TeleportLoadedPlayer(playerTransform, playerBody, snappedPosition, savedRotation, savedVelocity);
            }
            finally
            {
                HectonFloatingOrigin.EndSafeTeleportProtocol();
            }

            return true;
        }

        private static bool TryResolveCachedSafeAupSnapY(
            in AbsoluteUniversePosition savedAup,
            in float3 resolvedRuntimePosition,
            out float safeY)
        {
            safeY = 0f;

            HectonMapMagicVegetationBridge vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (vegetationBridge == null ||
                !vegetationBridge.TryGetCachedTerrainHeight(resolvedRuntimePosition.x, resolvedRuntimePosition.z, out float terrainHeight) ||
                !float.IsFinite(terrainHeight))
            {
                return false;
            }

            double liftMeters = ResolveAupRuntimeVerticalLiftMeters(in savedAup, terrainHeight) +
                                SafeAupSnapGroundPaddingMeters;
            if (!(liftMeters > SafeAupSnapMinimumLiftMeters))
                return false;

            safeY = terrainHeight + SafeAupSnapGroundPaddingMeters;
            return float.IsFinite(safeY);
        }

        private static double ResolveAupRuntimeVerticalLiftMeters(
            in AbsoluteUniversePosition savedAup,
            float terrainRuntimeY)
        {
            Vector3 committedOffset = HectonFloatingOrigin.CurrentTotalOffset;
            double savedRuntimeY = (savedAup.GridY * (double)AbsoluteUniversePosition.CellSizeMeters) +
                                   savedAup.LocalY -
                                   committedOffset.y;
            return terrainRuntimeY - savedRuntimeY;
        }

        private static void TeleportLoadedPlayer(
            Transform playerTransform,
            Rigidbody playerBody,
            Vector3 position,
            Quaternion rotation,
            Vector3 velocity)
        {
            if (playerBody == null)
            {
                playerTransform.SetPositionAndRotation(position, rotation);
                return;
            }

            bool wasKinematic = playerBody.isKinematic;
            bool wasDetectingCollisions = playerBody.detectCollisions;
            bool wasSleeping = playerBody.IsSleeping();

            playerBody.isKinematic = true;
            playerBody.detectCollisions = false;
            playerBody.ResetCenterOfMass();
            playerBody.transform.SetPositionAndRotation(position, rotation);
            playerBody.PublishTransform();
            playerBody.isKinematic = wasKinematic;
            playerBody.detectCollisions = wasDetectingCollisions;

            if (!wasKinematic)
            {
                playerBody.linearVelocity = HectonPlayerMotor.SafeVelocity(velocity);
                playerBody.angularVelocity = Vector3.zero;
                if (wasSleeping)
                    playerBody.Sleep();
                else
                    playerBody.WakeUp();
            }
            else if (wasSleeping)
            {
                playerBody.Sleep();
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return math.all(math.isfinite(new float3(value.x, value.y, value.z)));
        }

        private static bool IsFinite(Quaternion value)
        {
            return math.all(math.isfinite(new float4(value.x, value.y, value.z, value.w))) &&
                   math.lengthsq(new float4(value.x, value.y, value.z, value.w)) > 0.0001f;
        }

        public async Awaitable LoadGameAsync(string slotName)
        {
            CachePersistentDataPathRoot();
            LastOperationSucceeded = false;
            LastOperationError = string.Empty;
            LastOperationSlot = string.Empty;
            LastLoadUsedBackup = false;
            LastLoadBackupGeneration = 0;
            LastLoadSelfRepaired = false;
            LastLoadUsedLegacyCompression = false;

            if (!TryResolveSafeSlotName(slotName, out slotName))
            {
                LastOperationError = InvalidSlotNameReason;
                LogWarning("[SaveManager] Ignored load request: invalid slot name.");
                SaveEvents.RaiseLoadFailed(string.Empty, InvalidSlotNameReason);
                return;
            }

            LastOperationSlot = slotName;

            if (_isBusy)
            {
                const string reason = "Load already in progress.";
                LastOperationError = reason;
                LogWarning($"[SaveManager] Ignored load request for '{slotName}': {reason}");
                SaveEvents.RaiseLoadFailed(slotName, reason);
                return;
            }

            if (!SaveExists(slotName))
            {
                string reason = $"No primary or backup save found for '{slotName}'.";
                LastOperationError = reason;
                LogWarning($"[SaveManager] {reason}");
                SaveEvents.RaiseLoadFailed(slotName, reason);
                return;
            }

            _isBusy = true;
            SaveEvents.RaiseLoadStarted(slotName);
            ReportLoadPipelineStage(LoadingPipelineStage.PagingSectors, 0.08f);
            var totalTimer = Stopwatch.StartNew();
            NativeArray<byte> loadedVoxelDeltaSnapshot = default;
            NativeArray<SaveLoadCandidate> candidates = _loadCandidateScratch;
            int candidateCount = 0;

            try
            {
                SaveBinaryStorage.ConsumeIndexedSectorQuarantineFlag();
                await Awaitable.BackgroundThreadAsync();
                SaveData data = null;
                QuestSaveHeader loadedQuestHeader = default;
                uint[] loadedQuestStateWords = null;
                PersistentWorldDeltaRecord[] loadedWorldDeltas = null;
                EcosystemSectorSaveRecord[] loadedEcosystemSectors = null;
                SaveMetadata loadedMetadata = null;
                SaveLoadCandidate loadedCandidate = default;
                string lastErrorMessage = string.Empty;
                candidateCount = BuildLoadCandidates(slotName, candidates);
                bool usedLegacyFormat = false;
                ulong loadedPayloadHash64 = 0UL;
                int loadedPayloadLength = 0;
                bool criticalBackupPromotedForLoad = false;
                int criticalBackupGenerationForLoad = 0;

                for (int i = 0; i < candidateCount; i++)
                {
                    if (TryLoadCandidate(
                        slotName,
                        candidates[i],
                        out SaveData candidateData,
                        out QuestSaveHeader candidateQuestHeader,
                        out uint[] candidateQuestStateWords,
                        out PersistentWorldDeltaRecord[] candidateWorldDeltas,
                        out EcosystemSectorSaveRecord[] candidateEcosystemSectors,
                        out NativeArray<byte> candidateVoxelDeltaSnapshot,
                        out SaveMetadata candidateMetadata,
                        out ulong candidatePayloadHash64,
                        out int candidatePayloadLength,
                        out bool candidateUsedLegacyFormat,
                        out bool candidateIndexedBackupRecoveryUsed,
                        out string candidateError))
                    {
                        bool criticalBackupPromoted = false;
                        int criticalBackupGeneration = 0;
                        if (!candidates[i].IsBackup && candidateIndexedBackupRecoveryUsed)
                        {
                            if (candidateVoxelDeltaSnapshot.IsCreated)
                                DisposeNativeArray(ref candidateVoxelDeltaSnapshot);

                            if (!TryLoadAndPromoteCriticalBackup(
                                    slotName,
                                    "Indexed primary sector recovered from backup sector during load.",
                                    out candidateData,
                                    out candidateQuestHeader,
                                    out candidateQuestStateWords,
                                    out candidateWorldDeltas,
                                    out candidateEcosystemSectors,
                                    out candidateVoxelDeltaSnapshot,
                                    out candidateMetadata,
                                    out candidatePayloadHash64,
                                    out candidatePayloadLength,
                                    out candidateUsedLegacyFormat,
                                    out criticalBackupGeneration,
                                    out candidateError))
                            {
                                lastErrorMessage = candidateError;
                                LogWarning($"[SaveManager] CRITICAL_RECOVERY failed for '{slotName}': {candidateError}");
                                continue;
                            }

                            criticalBackupPromoted = true;
                        }

                        data = candidateData;
                        loadedQuestHeader = candidateQuestHeader;
                        loadedQuestStateWords = candidateQuestStateWords;
                        loadedWorldDeltas = candidateWorldDeltas;
                        loadedEcosystemSectors = candidateEcosystemSectors;
                        loadedVoxelDeltaSnapshot = candidateVoxelDeltaSnapshot;
                        loadedCandidate = criticalBackupPromoted
                            ? SaveLoadCandidate.Primary()
                            : candidates[i];
                        loadedMetadata = candidateMetadata;
                        loadedPayloadHash64 = candidatePayloadHash64;
                        loadedPayloadLength = candidatePayloadLength;
                        usedLegacyFormat = candidateUsedLegacyFormat;
                        criticalBackupPromotedForLoad = criticalBackupPromoted;
                        criticalBackupGenerationForLoad = criticalBackupPromoted ? criticalBackupGeneration : 0;
                        break;
                    }

                    if (!candidates[i].IsBackup &&
                        SaveBinaryStorage.IsIndexedBlockStorageRecoveryError(candidateError) &&
                        TryLoadAndPromoteCriticalBackup(
                            slotName,
                            candidateError,
                            out SaveData recoveryData,
                            out QuestSaveHeader recoveryQuestHeader,
                            out uint[] recoveryQuestStateWords,
                            out PersistentWorldDeltaRecord[] recoveryWorldDeltas,
                            out EcosystemSectorSaveRecord[] recoveryEcosystemSectors,
                            out NativeArray<byte> recoveryVoxelDeltaSnapshot,
                            out SaveMetadata recoveryMetadata,
                            out ulong recoveryPayloadHash64,
                            out int recoveryPayloadLength,
                            out bool recoveryUsedLegacyFormat,
                            out int recoveryBackupGeneration,
                            out string recoveryError))
                    {
                        data = recoveryData;
                        loadedQuestHeader = recoveryQuestHeader;
                        loadedQuestStateWords = recoveryQuestStateWords;
                        loadedWorldDeltas = recoveryWorldDeltas;
                        loadedEcosystemSectors = recoveryEcosystemSectors;
                        loadedVoxelDeltaSnapshot = recoveryVoxelDeltaSnapshot;
                        loadedCandidate = SaveLoadCandidate.Primary();
                        loadedMetadata = recoveryMetadata;
                        loadedPayloadHash64 = recoveryPayloadHash64;
                        loadedPayloadLength = recoveryPayloadLength;
                        usedLegacyFormat = recoveryUsedLegacyFormat;
                        criticalBackupPromotedForLoad = true;
                        criticalBackupGenerationForLoad = recoveryBackupGeneration;
                        break;
                    }

                    lastErrorMessage = candidateError;
                    string candidateLabel = candidates[i].IsBackup
                        ? $"backup g{candidates[i].BackupGeneration}"
                        : "primary";
                    LogWarning($"[SaveManager] Failed to load {candidateLabel} for '{slotName}': {candidateError}");
                }

                if (data == null)
                    throw new Exception(string.IsNullOrEmpty(lastErrorMessage) ? "No load candidate could be restored." : lastErrorMessage);

                await Awaitable.MainThreadAsync();
                StageIntegrityPayload(_savePayloadBuffer, loadedPayloadLength, loadedPayloadHash64, slotName);
                ReportLoadPipelineStage(LoadingPipelineStage.HydratingEntities, 0.42f);

                if (SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary))
                {
                    LogInfo($"[SaveManager] Migrated save '{slotName}' from v{originalVersion}: {summary}");
                }

                ValidateRuntimeWorldSeed(data);
                _totalPlayTime = data.totalPlayTime;
                _sessionStartTime = Time.realtimeSinceStartupAsDouble;
                PersistentWorldRegistry persistentWorldRegistryForLoad = GlobalRegistry.PersistentWorldRegistry;
                persistentWorldRegistryForLoad?.PreloadTombstonesFromLoadedRecords(loadedWorldDeltas);
                ModSaveStateStore.LoadFromSaveData(data);
                string loadedRelativeSavePath = GetCandidateSavePath(slotName, loadedCandidate);
                if (!ModSaveStateStore.TryLoadMmfPayloads(GetPersistentAbsolutePath(loadedRelativeSavePath), out string modPayloadLoadError) ||
                    !string.IsNullOrEmpty(modPayloadLoadError))
                {
                    ReportModPayloadLoadFailure(slotName, modPayloadLoadError);
                }

                QuestManager.StageLoadedPackedState(loadedQuestHeader, loadedQuestStateWords);
                
                _registryDirty = true;
                SortRegistryIfDirty(LoadPriorityComparer);

                VoxelDeltaProcessor voxelDeltaProcessor = null;
                long loadApplyDeadlineTicks = HydrationScheduler.CreateDeadlineTicks();
                for (int i = 0; i < _saveableCount; i++)
                {
                    ISaveable saveable = _saveables[i];
                    if (!IsAlive(saveable))
                        continue;

                    if (saveable is VoxelDeltaProcessor loadedVoxelDeltaProcessor)
                    {
                        voxelDeltaProcessor = loadedVoxelDeltaProcessor;
                        continue;
                    }

                    saveable.LoadFromSaveData(data);
                    if (i + 1 < _saveableCount && Stopwatch.GetTimestamp() >= loadApplyDeadlineTicks)
                    {
                        await HydrationScheduler.NextFrameAsync(destroyCancellationToken);
                        loadApplyDeadlineTicks = HydrationScheduler.CreateDeadlineTicks();
                    }
                }

                if (voxelDeltaProcessor != null && !voxelDeltaProcessor.TryLoadNativeSnapshot(loadedVoxelDeltaSnapshot, out string voxelLoadError))
                    throw new Exception(voxelLoadError);

                if (persistentWorldRegistryForLoad != null)
                {
                    ReportLoadPipelineStage(LoadingPipelineStage.PagingSectors, 0.68f);
                    string loadedAbsolutePath = GetPersistentAbsolutePath(loadedRelativeSavePath);
                    if (SaveBinaryStorage.TryReadIndexedPersistentWorldDirectory(
                            loadedAbsolutePath,
                            _indexedSectorDirectoryScratch,
                            out _,
                            out _,
                            out string loadedDirectoryError))
                    {
                        persistentWorldRegistryForLoad.RestoreFromIndexedSave(loadedAbsolutePath);
                    }
                    else
                    {
                        ReportIndexedDirectoryReadFailure(loadedAbsolutePath, loadedDirectoryError);
                        persistentWorldRegistryForLoad.DisableIndexedSavePaging();
                        persistentWorldRegistryForLoad.RestoreFromLoadedRecords(loadedWorldDeltas);
                    }
                }

                ReportLoadPipelineStage(LoadingPipelineStage.BuildingNavGrid, 0.84f);
                (GlobalRegistry.EcosystemDirector as EcosystemDirector)?.RestoreFromLoadedRecords(loadedEcosystemSectors);
                ReportLoadPipelineStage(LoadingPipelineStage.SafeAupSnap, 0.92f);
                bool appliedSafeAupSnap = TryApplySafeAupSnapOnLoad(data);
                bool quarantinedSectorRecoveredAsResetChunk = SaveBinaryStorage.ConsumeIndexedSectorQuarantineFlag();
                if (quarantinedSectorRecoveredAsResetChunk)
                    ReportCriticalSectorCorruptionDialog();

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
                        PlayerPosition = playerPosition,
                        WorldSeed = data.ecosystemState.worldSeed,
                        WorldGenerationVersionId = data.ecosystemState.worldGenerationVersionId
                    };
                    repairedPrimaryArtifacts = SelfRepairPrimaryArtifacts(slotName, data, repairMetadata, loadedQuestHeader, loadedQuestStateWords, loadedWorldDeltas, loadedEcosystemSectors, loadedVoxelDeltaSnapshot);
                    await Awaitable.MainThreadAsync();
                }

                string sourceLabel = criticalBackupPromotedForLoad
                    ? $"backup g{criticalBackupGenerationForLoad} promoted to primary"
                    : (loadedCandidate.IsBackup
                        ? $"backup g{loadedCandidate.BackupGeneration}"
                        : "primary");
                LastLoadUsedBackup = criticalBackupPromotedForLoad || loadedCandidate.IsBackup;
                LastLoadBackupGeneration = criticalBackupPromotedForLoad
                    ? criticalBackupGenerationForLoad
                    : loadedCandidate.BackupGeneration;
                LastLoadSelfRepaired = repairedPrimaryArtifacts || criticalBackupPromotedForLoad;
                LastLoadUsedLegacyCompression = usedLegacyFormat;
                SaveSlotInfo postLoadInfo = BuildSaveSlotInfoInternal(slotName);
                SaveSlotIntegrityState postLoadIntegrity = postLoadInfo != null ? postLoadInfo.IntegrityState : SaveSlotIntegrityState.Empty;
                RecordSuccessfulLoad(slotName, data.version, postLoadIntegrity, LastLoadUsedBackup, LastLoadBackupGeneration, LastLoadUsedLegacyCompression, LastLoadSelfRepaired);
                LastOperationSucceeded = true;
                string loadCompletionSuffix = criticalBackupPromotedForLoad
                    ? " and promoted .bak to primary."
                    : (repairedPrimaryArtifacts ? " and self-repaired primary artifacts." : (appliedSafeAupSnap ? " and snapped player to safe terrain." : "."));
                ReportLoadPipelineStage(LoadingPipelineStage.Completed, 1f);
                LogInfo($"[SaveManager] Loaded '{slotName}' from {sourceLabel} in {totalTimer.ElapsedMilliseconds}ms{loadCompletionSuffix}");
                SaveEvents.RaiseLoadCompleted(slotName);
            }
            catch (Exception ex)
            {
                await Awaitable.MainThreadAsync();
                RecordFailure(slotName, "load", ex.Message);
                LastOperationError = ex.Message;
                LogError("[SaveManager] Load failed: " + ex);
                SaveEvents.RaiseLoadFailed(slotName, ex.Message);
                HideLoadingPipelineScreen();
            }
            finally
            {
                ClearLoadCandidates(candidates, candidateCount);

                if (loadedVoxelDeltaSnapshot.IsCreated)
                    DisposeNativeArray(ref loadedVoxelDeltaSnapshot);

                _isBusy = false;
            }
        }

        public bool SaveExists(string slotName)
        {
            if (!TryResolveSafeSlotName(slotName, out slotName))
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
            int infoCount = 0;
            try
            {
                infoCount = CollectAvailableSaveSlotInfos(_saveSlotInfoScratch);
                for (int i = 0; i < infoCount; i++)
                {
                    SaveSlotInfo info = _saveSlotInfoScratch[i];
                    if (info != null && info.Metadata != null)
                        results.Add(info.Metadata);
                }
            }
            finally
            {
                ClearSaveSlotScratch(_saveSlotInfoScratch, infoCount);
            }
        }

        public bool TryGetSaveSlotInfo(string slotName, out SaveSlotInfo info)
        {
            info = null;
            if (!TryResolveSafeSlotName(slotName, out slotName))
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

            lock (SaveSlotInfoScratchSync)
            {
                int slotCount = 0;
                try
                {
                    slotCount = CollectAvailableSaveSlotInfos(SaveSlotInfoScratch);
                    for (int i = 0; i < slotCount; i++)
                    {
                        SaveSlotInfo slot = SaveSlotInfoScratch[i];
                        if (slot != null && TryRepairSaveSlotInternal(slot.slotName, out SaveSlotRepairResult result))
                            results.Add(result);
                    }
                }
                finally
                {
                    ClearSaveSlotScratch(SaveSlotInfoScratch, slotCount);
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

            lock (SaveSlotInfoScratchSync)
            {
                int slotCount = 0;
                try
                {
                    slotCount = CollectAvailableSaveSlotInfos(SaveSlotInfoScratch);
                    for (int i = 0; i < slotCount; i++)
                    {
                        SaveSlotInfo slot = SaveSlotInfoScratch[i];
                        if (slot != null && TryAuditSaveSlotInternal(slot.slotName, out SaveSlotAuditResult result))
                            results.Add(result);
                    }
                }
                finally
                {
                    ClearSaveSlotScratch(SaveSlotInfoScratch, slotCount);
                }
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
            int slotNameCount = 0;
            for (int i = 0; i < files.Length; i++)
            {
                string fileName = Path.GetFileName(files[i]);
                if (TryExtractSlotName(fileName, out string slotName) &&
                    !ContainsSlotName(files, slotNameCount, slotName))
                {
                    files[slotNameCount++] = slotName;
                }
            }

            SaveManager manager = GlobalRegistry.SaveRuntime;
            for (int i = 0; i < slotNameCount; i++)
            {
                string slotName = files[i];
                SaveSlotInfo info = manager != null ? manager.BuildSaveSlotInfo(slotName) : BuildSaveSlotInfoStatic(slotName);
                if (info != null && info.HasAnySaveData)
                    results.Add(info);
            }

            results.Sort(SaveSlotTimestampDescendingComparison);
        }

        private static int CollectAvailableSaveSlotInfos(SaveSlotInfo[] results)
        {
            if (results == null || results.Length <= 0)
                return 0;

            string persistentPath = Application.persistentDataPath;
            if (!Directory.Exists(persistentPath))
                return 0;

            string[] files = Directory.GetFiles(persistentPath);
            int slotNameCount = 0;
            for (int i = 0; i < files.Length; i++)
            {
                string fileName = Path.GetFileName(files[i]);
                if (TryExtractSlotName(fileName, out string slotName) &&
                    !ContainsSlotName(files, slotNameCount, slotName))
                {
                    files[slotNameCount++] = slotName;
                }
            }

            SaveManager manager = GlobalRegistry.SaveRuntime;
            int resultCount = 0;
            for (int i = 0; i < slotNameCount && resultCount < results.Length; i++)
            {
                string slotName = files[i];
                SaveSlotInfo info = manager != null ? manager.BuildSaveSlotInfo(slotName) : BuildSaveSlotInfoStatic(slotName);
                if (info != null && info.HasAnySaveData)
                    results[resultCount++] = info;
            }

            SortSaveSlotScratch(results, resultCount);
            return resultCount;
        }

        private static void SortSaveSlotScratch(SaveSlotInfo[] infos, int count)
        {
            int safeCount = math.min(count, infos != null ? infos.Length : 0);
            for (int i = 1; i < safeCount; i++)
            {
                SaveSlotInfo candidate = infos[i];
                int j = i - 1;
                while (j >= 0 && CompareSaveSlotTimestampDescending(infos[j], candidate) > 0)
                {
                    infos[j + 1] = infos[j];
                    j--;
                }

                infos[j + 1] = candidate;
            }
        }

        private static void ClearSaveSlotScratch(SaveSlotInfo[] infos, int count)
        {
            int safeCount = math.min(count, infos != null ? infos.Length : 0);
            for (int i = 0; i < safeCount; i++)
                infos[i] = null;
        }

        private static bool ContainsSlotName(string[] slotNames, int count, string slotName)
        {
            for (int i = 0; i < count; i++)
            {
                if (string.Equals(slotNames[i], slotName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
        
        public void DeleteSave(string slotName)
        {
            if (!TryResolveSafeSlotName(slotName, out slotName))
                return;

            DeleteKnownSaveArtifacts(slotName);
            SaveThumbnailSystem.DeleteThumbnail(slotName);
        }

        private static void DeleteKnownSaveArtifacts(string slotName)
        {
            DeleteFileIfExists(GetPrimarySaveFilePath(slotName));
            DeleteFileIfExists(GetTempSaveFilePath(slotName));
            DeleteFileIfExists(SaveSlotMaintenanceRecord.GetPath(slotName));

            int maxGeneration = GetMaxBackupGenerationCount();
            for (int generation = 1; generation <= maxGeneration; generation++)
                DeleteFileIfExists(GetBackupSaveFilePath(slotName, generation));
        }

        public static string GetPrimarySaveFilePath(string slotName) => $"{ResolveSafeSlotFileStem(slotName)}.sav";
        public static string GetBackupSaveFilePath(string slotName) => GetBackupSaveFilePath(slotName, 1);
        public static string GetBackupSaveFilePath(string slotName, int generation)
        {
            string safeSlotName = ResolveSafeSlotFileStem(slotName);
            if (generation <= 1)
                return $"{safeSlotName}.sav.bak";

            return $"{safeSlotName}.sav.bak{generation}";
        }
        public static string GetTempSaveFilePath(string slotName) => $"{ResolveSafeSlotFileStem(slotName)}.sav.tmp";
        public static string GetDiagnosticSaveFilePath(string slotName) => $"{ResolveSafeSlotFileStem(slotName)}.diag";
        private static string GetPersistentAbsolutePath(string relativePath)
        {
            if (Path.IsPathRooted(relativePath))
                return relativePath;

            string root = s_persistentDataPathRoot;
            if (string.IsNullOrEmpty(root))
            {
                root = Application.persistentDataPath;
                s_persistentDataPathRoot = root;
                SaveSidecarStorage.SetPersistentDataPathRoot(root);
            }

            return Path.Combine(root, relativePath);
        }

        private static void CachePersistentDataPathRoot()
        {
            string root = Application.persistentDataPath;
            if (string.IsNullOrEmpty(root))
                return;

            s_persistentDataPathRoot = root;
            SaveSidecarStorage.SetPersistentDataPathRoot(root);
        }

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
            int maxGeneration = GetMaxBackupGenerationCount();
            string[] paths = new string[3 + maxGeneration];
            paths[0] = GetPrimarySaveFilePath(slotName);
            paths[1] = GetTempSaveFilePath(slotName);
            paths[2] = SaveSlotMaintenanceRecord.GetPath(slotName);
            for (int generation = 1; generation <= maxGeneration; generation++)
            {
                paths[2 + generation] = GetBackupSaveFilePath(slotName, generation);
            }

            return paths;
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

            string absoluteFinalPath = GetPersistentAbsolutePath(finalPath);
            if (new FileInfo(absoluteFinalPath) is FileInfo promotedInfo && promotedInfo.Exists)
                AsyncWriteManager.QueueThrottledFlush(absoluteFinalPath, promotedInfo.Length, out _);

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
            QuestSaveHeader packedQuestHeader,
            NativeArray<uint> packedQuestStateWords,
            NativeArray<byte> voxelDeltaSnapshot,
            NativeArray<byte> rawBuffer,
            NativeArray<byte> compressedBuffer,
            out ulong payloadHash64,
            out int rawPayloadLength)
        {
            payloadHash64 = 0UL;
            rawPayloadLength = 0;
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
                    packedQuestHeader,
                    packedQuestStateWords,
                    voxelDeltaSnapshot,
                    rawBuffer,
                    compressedBuffer,
                    out payloadHash64,
                    out rawPayloadLength,
                    out string writeError))
            {
                throw new Exception(writeError);
            }

            // Step 4: the writer already re-reads metadata internally, but the pipeline still requires the temp artifact to exist here.
            if (!FileExists(tempPath))
                throw new FileNotFoundException("Verified temp save was not created by the binary writer.", absoluteTempPath);

            if (!ModSaveStateStore.TryCommitMmfPayloads(absoluteTempPath, out string modPayloadCommitError))
                ReportModPayloadCommitFailure(slotName, modPayloadCommitError);

            CommitTempSaveToPrimary(slotName, tempPath, finalPath);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void ReportModPayloadLoadFailure(string slotName, string error)
        {
            if (string.IsNullOrEmpty(error))
                return;

            LogWarning($"[SaveManager] Mod payload load warning for '{slotName}': {error}");
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void ReportModPayloadCommitFailure(string slotName, string error)
        {
            if (string.IsNullOrEmpty(error))
                return;

            LogWarning($"[SaveManager] Mod payload commit failed for '{slotName}': {error}");
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void ReportIndexedDirectoryReadFailure(string absolutePath, string error)
        {
            if (string.IsNullOrEmpty(error))
                return;

            LogWarning($"[SaveManager] Indexed save directory read failed for '{absolutePath}': {error}");
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

            return TryResolveSafeSlotName(slotName, out slotName);
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
            return TryResolveSafeSlotName(slotName, out slotName);
        }

        private static int BuildLoadCandidates(string slotName, NativeArray<SaveLoadCandidate> candidates)
        {
            if (!candidates.IsCreated || candidates.Length <= 0)
                return 0;

            int backupRetention = GetBackupRetentionCountStatic(slotName);
            int candidateCount = 0;

            string primarySavePath = GetPrimarySaveFilePath(slotName);
            if (FileExists(primarySavePath))
                candidates[candidateCount++] = SaveLoadCandidate.Primary();

            for (int generation = 1; generation <= backupRetention; generation++)
            {
                string backupSavePath = GetBackupSaveFilePath(slotName, generation);
                if (!FileExists(backupSavePath))
                    continue;

                if (candidateCount >= candidates.Length)
                    break;

                candidates[candidateCount++] = SaveLoadCandidate.Backup(generation);
            }

            return candidateCount;
        }

        private static void ClearLoadCandidates(NativeArray<SaveLoadCandidate> candidates, int candidateCount)
        {
            int safeCount = math.min(candidateCount, candidates.IsCreated ? candidates.Length : 0);
            for (int i = 0; i < safeCount; i++)
                candidates[i] = default;
        }

        private static string GetCandidateSavePath(string slotName, SaveLoadCandidate candidate)
        {
            return candidate.IsBackup
                ? GetBackupSaveFilePath(slotName, candidate.BackupGeneration)
                : GetPrimarySaveFilePath(slotName);
        }

        private static bool TryRepairSaveSlotInternal(string slotName, out SaveSlotRepairResult result)
        {
            if (!TryResolveSafeSlotName(slotName, out slotName))
            {
                result = new SaveSlotRepairResult
                {
                    SlotName = string.Empty,
                    IntegrityBefore = SaveSlotIntegrityState.Empty,
                    IntegrityAfter = SaveSlotIntegrityState.Empty,
                    Message = InvalidSlotNameReason
                };
                return false;
            }

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

            SaveData repairedData = null;
            QuestSaveHeader packedQuestHeader = default;
            uint[] packedQuestStateWords = null;
            PersistentWorldDeltaRecord[] persistentWorldItems = null;
            EcosystemSectorSaveRecord[] ecosystemSectorStates = null;
            NativeArray<byte> voxelDeltaSnapshot = default;
            SaveMetadata metadataSource = beforeInfo.Metadata;
            SaveLoadCandidate selectedCandidate = default;
            bool usedLegacyFormat = false;
            string errorMessage = string.Empty;

            int candidateCount = 0;
            lock (SaveLoadCandidateScratchSync)
            {
                EnsureStaticLoadCandidateScratch();
                NativeArray<SaveLoadCandidate> candidates = SaveLoadCandidateScratch;
                try
                {
                    candidateCount = BuildLoadCandidates(slotName, candidates);
                    for (int i = 0; i < candidateCount; i++)
                    {
                        SaveLoadCandidate candidate = candidates[i];
                        if (TryLoadCandidate(
                            slotName,
                            candidate,
                            out SaveData candidateData,
                            out QuestSaveHeader candidateQuestHeader,
                            out uint[] candidatePackedQuestStateWords,
                            out PersistentWorldDeltaRecord[] candidateWorldItems,
                            out EcosystemSectorSaveRecord[] candidateEcosystemSectorStates,
                            out NativeArray<byte> candidateVoxelDeltaSnapshot,
                            out SaveMetadata candidateMetadata,
                            out _,
                            out _,
                            out bool candidateUsedLegacyFormat,
                            out _,
                            out string candidateError))
                        {
                            repairedData = candidateData;
                            packedQuestHeader = candidateQuestHeader;
                            packedQuestStateWords = candidatePackedQuestStateWords;
                            persistentWorldItems = candidateWorldItems;
                            ecosystemSectorStates = candidateEcosystemSectorStates;
                            voxelDeltaSnapshot = candidateVoxelDeltaSnapshot;
                            metadataSource = candidateMetadata ?? beforeInfo.Metadata;
                            selectedCandidate = candidate;
                            usedLegacyFormat = candidateUsedLegacyFormat;
                            break;
                        }

                        errorMessage = candidateError;
                    }
                }
                finally
                {
                    ClearLoadCandidates(candidates, candidateCount);
                }
            }

            if (repairedData == null)
            {
                if (voxelDeltaSnapshot.IsCreated)
                    DisposeNativeArray(ref voxelDeltaSnapshot);

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
                packedQuestHeader,
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
                DisposeNativeArray(ref voxelDeltaSnapshot);

            return true;
        }

        private static bool TryAuditSaveSlotInternal(string slotName, out SaveSlotAuditResult result)
        {
            if (!TryResolveSafeSlotName(slotName, out slotName))
            {
                result = new SaveSlotAuditResult
                {
                    SlotName = string.Empty,
                    IntegrityState = SaveSlotIntegrityState.Empty,
                    Message = InvalidSlotNameReason
                };
                return false;
            }

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

            SaveLoadCandidate selectedCandidate = default;
            SaveData selectedData = null;
            bool selectedLegacyFormat = false;
            bool hasSelectedCandidate = false;

            int candidateCount = 0;
            lock (SaveLoadCandidateScratchSync)
            {
                EnsureStaticLoadCandidateScratch();
                NativeArray<SaveLoadCandidate> candidates = SaveLoadCandidateScratch;
                try
                {
                    candidateCount = BuildLoadCandidates(slotName, candidates);
                    for (int i = 0; i < candidateCount; i++)
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
                            out _,
                            out NativeArray<byte> candidateVoxelDeltaSnapshot,
                            out SaveMetadata _,
                            out _,
                            out _,
                            out bool candidateLegacyFormat,
                            out _,
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
                                DisposeNativeArray(ref candidateVoxelDeltaSnapshot);
                        }
                        else
                        {
                            if (isBackup)
                                result.BackupError = candidateError;
                            else
                                result.PrimaryError = candidateError;
                        }
                    }
                }
                finally
                {
                    ClearLoadCandidates(candidates, candidateCount);
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
            QuestSaveHeader packedQuestHeader,
            uint[] packedQuestStateWords,
            PersistentWorldDeltaRecord[] persistentWorldItems,
            EcosystemSectorSaveRecord[] ecosystemSectorStates,
            NativeArray<byte> voxelDeltaSnapshot)
        {
            return RepairPrimaryArtifacts(
                slotName,
                data,
                metadata,
                packedQuestHeader,
                packedQuestStateWords,
                persistentWorldItems,
                ecosystemSectorStates,
                voxelDeltaSnapshot,
                overwritePrimarySave: true);
        }

        private static bool TryLoadAndPromoteCriticalBackup(
            string slotName,
            string primaryError,
            out SaveData data,
            out QuestSaveHeader packedQuestHeader,
            out uint[] packedQuestStateWords,
            out PersistentWorldDeltaRecord[] persistentWorldItems,
            out EcosystemSectorSaveRecord[] ecosystemSectorStates,
            out NativeArray<byte> voxelDeltaSnapshot,
            out SaveMetadata metadata,
            out ulong payloadHash64,
            out int rawPayloadLength,
            out bool usedLegacyFormat,
            out int backupGeneration,
            out string errorMessage)
        {
            data = null;
            packedQuestHeader = default;
            packedQuestStateWords = null;
            persistentWorldItems = null;
            ecosystemSectorStates = null;
            voxelDeltaSnapshot = default;
            metadata = null;
            payloadHash64 = 0UL;
            rawPayloadLength = 0;
            usedLegacyFormat = false;
            backupGeneration = 1;
            errorMessage = string.Empty;

            string backupSavePath = GetBackupSaveFilePath(slotName, backupGeneration);
            if (!FileExists(backupSavePath))
            {
                errorMessage = $"CRITICAL_RECOVERY failed: '{backupSavePath}' is missing. Primary failure: {primaryError}";
                return false;
            }

            SaveLoadCandidate backupCandidate = SaveLoadCandidate.Backup(backupGeneration);
            if (!TryLoadCandidate(
                    slotName,
                    backupCandidate,
                    out data,
                    out packedQuestHeader,
                    out packedQuestStateWords,
                    out persistentWorldItems,
                    out ecosystemSectorStates,
                    out voxelDeltaSnapshot,
                    out metadata,
                    out payloadHash64,
                    out rawPayloadLength,
                    out usedLegacyFormat,
                    out bool indexedBackupRecoveryUsed,
                    out string backupError))
            {
                errorMessage = $"CRITICAL_RECOVERY rejected invalid '{backupSavePath}': {backupError}. Primary failure: {primaryError}";
                return false;
            }

            if (indexedBackupRecoveryUsed)
            {
                if (voxelDeltaSnapshot.IsCreated)
                    DisposeNativeArray(ref voxelDeltaSnapshot);

                errorMessage = $"CRITICAL_RECOVERY rejected cascading backup-sector recovery in '{backupSavePath}'. Primary failure: {primaryError}";
                return false;
            }

            if (!TryPromoteBackupToPrimaryAfterCriticalRecovery(slotName, backupSavePath, out string promotionError))
            {
                if (voxelDeltaSnapshot.IsCreated)
                    DisposeNativeArray(ref voxelDeltaSnapshot);

                errorMessage = $"CRITICAL_RECOVERY promotion failed for '{backupSavePath}': {promotionError}. Primary failure: {primaryError}";
                return false;
            }

            CrashTelemetryBuffer.ReportCriticalRecovery();
            LogError($"[SaveManager] CRITICAL_RECOVERY promoted '{backupSavePath}' to '{GetPrimarySaveFilePath(slotName)}'. Primary failure: {primaryError}");
            return true;
        }

        private static bool TryPromoteBackupToPrimaryAfterCriticalRecovery(
            string slotName,
            string backupSavePath,
            out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrEmpty(slotName) || string.IsNullOrEmpty(backupSavePath) || !FileExists(backupSavePath))
            {
                errorMessage = "Backup promotion input is invalid.";
                return false;
            }

            string primarySavePath = GetPrimarySaveFilePath(slotName);
            string tempSavePath = GetTempSaveFilePath(slotName);
            string absoluteBackupPath = GetPersistentAbsolutePath(backupSavePath);
            string absolutePrimaryPath = GetPersistentAbsolutePath(primarySavePath);
            string absoluteTempPath = GetPersistentAbsolutePath(tempSavePath);

            try
            {
                DeleteFileIfExists(tempSavePath);
                File.Copy(absoluteBackupPath, absoluteTempPath, true);

                if (File.Exists(absolutePrimaryPath))
                {
                    File.Replace(absoluteTempPath, absolutePrimaryPath, null, true);
                }
                else
                {
                    File.Move(absoluteTempPath, absolutePrimaryPath);
                }

                if (File.Exists(absoluteTempPath))
                    File.Delete(absoluteTempPath);

                if (!File.Exists(absolutePrimaryPath))
                {
                    errorMessage = "Primary file was missing after atomic backup promotion.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                try
                {
                    if (File.Exists(absoluteTempPath))
                        File.Delete(absoluteTempPath);
                }
                catch (Exception cleanupEx)
                {
                    errorMessage = $"{errorMessage}; temp cleanup failed: {cleanupEx.Message}";
                }

                return false;
            }
        }

        private static bool TryLoadCandidate(
            string slotName,
            SaveLoadCandidate candidate,
            out SaveData data,
            out QuestSaveHeader packedQuestHeader,
            out uint[] packedQuestStateWords,
            out PersistentWorldDeltaRecord[] persistentWorldItems,
            out EcosystemSectorSaveRecord[] ecosystemSectorStates,
            out NativeArray<byte> voxelDeltaSnapshot,
            out SaveMetadata metadata,
            out ulong payloadHash64,
            out int rawPayloadLength,
            out bool usedLegacyFormat,
            out bool indexedBackupRecoveryUsed,
            out string errorMessage)
        {
            data = null;
            packedQuestHeader = default;
            packedQuestStateWords = null;
            persistentWorldItems = null;
            ecosystemSectorStates = null;
            voxelDeltaSnapshot = default;
            metadata = null;
            payloadHash64 = 0UL;
            rawPayloadLength = 0;
            usedLegacyFormat = false;
            indexedBackupRecoveryUsed = false;
            errorMessage = string.Empty;

            string candidateSavePath = GetCandidateSavePath(slotName, candidate);
            if (!FileExists(candidateSavePath))
            {
                errorMessage = "Save artifact is missing.";
                return false;
            }

            return TryLoadBinaryCandidate(
                slotName,
                candidate,
                out data,
                out packedQuestHeader,
                out packedQuestStateWords,
                out persistentWorldItems,
                out ecosystemSectorStates,
                out voxelDeltaSnapshot,
                out metadata,
                out payloadHash64,
                out rawPayloadLength,
                out indexedBackupRecoveryUsed,
                out errorMessage);
        }

        private static bool TryLoadBinaryCandidate(
            string slotName,
            SaveLoadCandidate candidate,
            out SaveData data,
            out QuestSaveHeader packedQuestHeader,
            out uint[] packedQuestStateWords,
            out PersistentWorldDeltaRecord[] persistentWorldItems,
            out EcosystemSectorSaveRecord[] ecosystemSectorStates,
            out NativeArray<byte> voxelDeltaSnapshot,
            out SaveMetadata metadata,
            out ulong payloadHash64,
            out int rawPayloadLength,
            out bool indexedBackupRecoveryUsed,
            out string errorMessage)
        {
            data = null;
            packedQuestHeader = default;
            packedQuestStateWords = null;
            persistentWorldItems = null;
            ecosystemSectorStates = null;
            voxelDeltaSnapshot = default;
            metadata = null;
            payloadHash64 = 0UL;
            rawPayloadLength = 0;
            indexedBackupRecoveryUsed = false;
            errorMessage = string.Empty;

            AcquireReadBuffer(out NativeArray<byte> readBuffer, out bool ownsReadBuffer);
            try
            {
                if (!SaveBinaryStorage.TryLoadSaveData(
                    GetPersistentAbsolutePath(GetCandidateSavePath(slotName, candidate)),
                    slotName,
                    readBuffer,
                    out data,
                    out packedQuestHeader,
                    out packedQuestStateWords,
                    out persistentWorldItems,
                    out ecosystemSectorStates,
                    out voxelDeltaSnapshot,
                    out metadata,
                    out payloadHash64,
                    out rawPayloadLength,
                    out _,
                    out indexedBackupRecoveryUsed,
                    out errorMessage))
                {
                    if (voxelDeltaSnapshot.IsCreated)
                        DisposeNativeArray(ref voxelDeltaSnapshot);

                    return false;
                }

                RegisterVoxelDeltaSnapshot(voxelDeltaSnapshot, "loadedVoxelDeltaSnapshot");
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

            string candidateSavePath = GetCandidateSavePath(slotName, candidate);
            string absolutePath = GetPersistentAbsolutePath(candidateSavePath);
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

            errorMessage = "Unsupported non-binary save artifact.";
            return false;
        }

        private static bool RepairPrimaryArtifacts(
            string slotName,
            SaveData data,
            SaveMetadata metadataSource,
            QuestSaveHeader packedQuestHeader,
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
                        // COLD ALLOC: NativeArray<PersistentWorldDeltaRecord>[persistentWorldItems.Length] — static save assembly staging buffer — owner: SaveManager
                        persistentWorldItemBuffer = new NativeArray<PersistentWorldDeltaRecord>(
                            persistentWorldItems.Length,
                            Allocator.Temp,
                            NativeArrayOptions.UninitializedMemory);
                        RegisterTransientNativeArray(persistentWorldItemBuffer, "persistentWorldItemBuffer");
                        persistentWorldItemBuffer.CopyFrom(persistentWorldItems);
                    }

                    if (ecosystemSectorStates != null && ecosystemSectorStates.Length > 0)
                    {
                        // COLD ALLOC: NativeArray<EcosystemSectorSaveRecord>[ecosystemSectorStates.Length] — static save assembly staging buffer — owner: SaveManager
                        ecosystemSectorBuffer = new NativeArray<EcosystemSectorSaveRecord>(
                            ecosystemSectorStates.Length,
                            Allocator.Temp,
                            NativeArrayOptions.UninitializedMemory);
                        RegisterTransientNativeArray(ecosystemSectorBuffer, "ecosystemSectorBuffer");
                        ecosystemSectorBuffer.CopyFrom(ecosystemSectorStates);
                    }

                    if (packedQuestStateWords != null && packedQuestStateWords.Length > 0)
                    {
                        // COLD ALLOC: NativeArray<UInt32>[packedQuestStateWords.Length] — static save assembly staging buffer — owner: SaveManager
                        packedQuestStateBuffer = new NativeArray<uint>(
                            packedQuestStateWords.Length,
                            Allocator.Temp,
                            NativeArrayOptions.UninitializedMemory);
                        RegisterTransientNativeArray(packedQuestStateBuffer, "packedQuestStateBuffer");
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
                        packedQuestHeader,
                        packedQuestStateBuffer,
                        voxelDeltaSnapshot,
                        rawBuffer,
                        compressedBuffer,
                        out _,
                        out _);
                }
                finally
                {
                    if (persistentWorldItemBuffer.IsCreated)
                        DisposeNativeArray(ref persistentWorldItemBuffer);

                    if (ecosystemSectorBuffer.IsCreated)
                        DisposeNativeArray(ref ecosystemSectorBuffer);

                    if (packedQuestStateBuffer.IsCreated)
                        DisposeNativeArray(ref packedQuestStateBuffer);

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
                WorldSeed = data != null ? data.ecosystemState.worldSeed : 0,
                WorldGenerationVersionId = data != null ? data.ecosystemState.worldGenerationVersionId : 0,
                Checksum = source != null ? source.Checksum : string.Empty
            };
        }

        private static void AcquireReadBuffer(out NativeArray<byte> buffer, out bool ownsBuffer)
        {
            SaveManager manager = GlobalRegistry.SaveRuntime;
            if (manager != null && manager._savePayloadBuffer.IsCreated)
            {
                buffer = manager._savePayloadBuffer;
                ownsBuffer = false;
                return;
            }

            // COLD ALLOC: NativeArray<byte>[67108864] — fallback raw save read buffer when SaveManager instance is unavailable — owner: SaveManager
            buffer = new NativeArray<byte>(SaveBinaryStorage.RawPayloadCapacityBytes, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            NativeMemorySentinel.RegisterNativeArray(buffer, NativeMemoryOwner, "fallbackReadBuffer", NativeMemoryLifetime);
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
            SaveManager manager = GlobalRegistry.SaveRuntime;
            if (manager != null && manager._savePayloadBuffer.IsCreated && manager._compressedSaveBuffer.IsCreated)
            {
                rawBuffer = manager._savePayloadBuffer;
                compressedBuffer = manager._compressedSaveBuffer;
                ownsRawBuffer = false;
                ownsCompressedBuffer = false;
                return;
            }

            // COLD ALLOC: NativeArray<byte>[67108864] — fallback raw save write buffer when SaveManager instance is unavailable — owner: SaveManager
            rawBuffer = new NativeArray<byte>(SaveBinaryStorage.RawPayloadCapacityBytes, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            // COLD ALLOC: NativeArray<byte>[67378176] — fallback compressed save write buffer when SaveManager instance is unavailable — owner: SaveManager
            compressedBuffer = new NativeArray<byte>(SaveBinaryStorage.MaxCompressedPayloadBytes, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            NativeMemorySentinel.RegisterNativeArray(rawBuffer, NativeMemoryOwner, "fallbackRawWriteBuffer", NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(compressedBuffer, NativeMemoryOwner, "fallbackCompressedWriteBuffer", NativeMemoryLifetime);
            ownsRawBuffer = true;
            ownsCompressedBuffer = true;
        }

        private static void ReleaseBuffer(NativeArray<byte> buffer, bool ownsBuffer)
        {
            if (ownsBuffer && buffer.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(buffer);
                buffer.Dispose();
            }
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
            if (record == null)
                return;

            record.LastSuccessfulSaveTicksUtc = DateTime.UtcNow.Ticks;
            record.SuccessfulSaveCount++;
            record.LastKnownSaveVersion = dataVersion;
            record.LastKnownIntegrityState = SaveSlotInfo.ToStorageString(integrityState);
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
            if (record == null)
                return;

            record.LastSuccessfulLoadTicksUtc = DateTime.UtcNow.Ticks;
            record.SuccessfulLoadCount++;
            record.LastKnownSaveVersion = dataVersion;
            record.LastKnownIntegrityState = SaveSlotInfo.ToStorageString(integrityState);
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
            if (record == null)
                return;

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
            if (record == null)
                return;

            record.LastAuditTicksUtc = DateTime.UtcNow.Ticks;
            record.AuditCount++;
            record.LastAuditReadable = result.SlotReadable;
            record.LastAuditRecommendedRepair = result.RecommendedRepair;
            record.LastKnownSaveVersion = result.DetectedVersion;
            record.LastKnownIntegrityState = SaveSlotInfo.ToStorageString(result.IntegrityState);
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
            if (record == null)
                return;

            record.LastRepairTicksUtc = DateTime.UtcNow.Ticks;
            record.RepairCount++;
            record.LastKnownSaveVersion = dataVersion;
            record.LastKnownIntegrityState = SaveSlotInfo.ToStorageString(result.IntegrityAfter);
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
            if (!TryResolveSafeSlotName(slotName, out slotName))
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
                    SaveLoadCandidate.Primary(),
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
                    SaveLoadCandidate.Backup(generation),
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
