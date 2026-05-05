// ============================================================================
// HECTON-8 — SaveManager.cs
// Save persistence service. Runtime owner is injected through GlobalRegistry.
//
// АРХИТЕКТУРА:
//   • Реестр ISaveable через explicit registration (zero GC при save/load).
//   • XXHash3 checksums for header/payload integrity.
//   • Unity 6 Awaitable API: BackgroundThreadAsync / MainThreadAsync.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Hecton8.Bootstrap;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Gameplay;
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
    public sealed class SaveManager : MonoBehaviour, ISaveService, IUpdatable, ILateFrameTickable
    {
        private const long MainThreadSnapshotBudgetMs = 50L;
        private static readonly long PreCompressionYieldBudgetTicks = Math.Max(1L, Stopwatch.Frequency / 500L);
        private static readonly long LoadApplyFrameBudgetTicks = Math.Max(1L, Stopwatch.Frequency / 250L);
        private const float IntegrityScanIntervalSeconds = 10f;
        private const float IndexedDefragCheckIntervalSeconds = 5f;
        private const float PredictiveIndexedPagingLookaheadSeconds = 20f;
        private const float PredictiveIndexedPagingCheckIntervalSeconds = 0.5f;
        private const float PredictiveIndexedPagingMinimumPlanarSpeedMetersPerSecond = 0.5f;
        private const float CompressionFastModeFrameTimeThresholdSeconds = 0.014f;
        private const float SafeAupSnapSphereRadiusMeters = 0.45f;
        private const float SafeAupSnapCastStartHeightMeters = 12f;
        private const float SafeAupSnapCastDistanceMeters = 96f;
        private const float SafeAupSnapGroundPaddingMeters = 0.28f;
        private const float SafeAupSnapMinimumLiftMeters = 0.35f;
        private const string EmergencyIntegrityBackupSuffix = "_integrity_emergency";
        private const string CriticalSectorCorruptionMessage = "CRITICAL ERROR: SECTOR CORRUPTION DETECTED. TERRAIN RE-INITIALIZED.";
        private const int MaxRegisteredSaveables = 256;
        private const string NativeMemoryOwner = nameof(SaveManager);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Session;
        private const NativeAllocationLifetime NativeTransientMemoryLifetime = NativeAllocationLifetime.TransientArena;
        private static readonly uint _predictiveIndexedSectorPrewarmFailedWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Save.PredictiveIndexedSectorPrewarmFailed"));
        private static readonly uint _predictiveIndexedSectorPrewarmBackupWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Save.PredictiveIndexedSectorPrewarmBackup"));
        private static readonly uint _predictiveIndexedSectorPrewarmExceptionWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Save.PredictiveIndexedSectorPrewarmException"));
        private static readonly uint _predictiveIndexedSectorPrewarmContextHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Save.PredictiveIndexedSectorPrewarm"));

        // ══════════════════════════════════════════════════════════
        //  SAVE STATE
        // ══════════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════════
        //  SERVICE STATE
        // ══════════════════════════════════════════════════════════

        public bool IsInitialized => _serviceRegistered && ReferenceEquals(GlobalRegistry.Save, this);
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

        // COLD ALLOC: ISaveable[256] - fixed persistence registry prevents List resize during scene registration - owner: SaveManager
        private readonly ISaveable[] _saveables = new ISaveable[MaxRegisteredSaveables];
        // COLD ALLOC: List<IndexedSectorEntryInfo>[128] - reusable indexed-save directory probe scratch - owner: SaveManager
        private readonly List<SaveBinaryStorage.IndexedSectorEntryInfo> _indexedSectorDirectoryScratch = new List<SaveBinaryStorage.IndexedSectorEntryInfo>(128);
        private int _saveableCount;
        private bool _registryDirty;
        private bool _saveableCapacityWarningLogged;
        private double _sessionStartTime;
        private double _totalPlayTime;
        private bool _isBusy;

        private static readonly IComparer<ISaveable> SavePriorityComparer = new SavePriorityComparerImpl();
        private static readonly IComparer<ISaveable> LoadPriorityComparer = new LoadPriorityComparerImpl();

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

        private NativeArray<byte> _savePayloadBuffer;
        private NativeArray<byte> _compressedSaveBuffer;
        private NativeArray<byte> _integrityPayloadMirror;
        private NativeArray<ulong> _integrityScanResult;
        private NativeArray<byte> _pendingIntegrityPayloadSource;
        private JobHandle _integrityScanHandle;
        private ulong _expectedIntegrityPayloadHash64;
        private ulong _pendingExpectedIntegrityPayloadHash64;
        private float _nextIntegrityScanTime;
        private int _integrityPayloadLength;
        private int _pendingIntegrityPayloadLength;
        private bool _integrityScanScheduled;
        private bool _pendingIntegrityPayloadStage;
        private bool _updatableRegistered;
        private bool _lateFrameRegistered;
        private bool _serviceRegistered;
        private bool _emergencyBackupScheduled;
        private bool _indexedDefragInFlight;
        private bool _predictiveIndexedPagingInFlight;
        private LoadingScreenController _cachedLoadingScreenController;
        private string _integritySlotName;
        private string _pendingIntegritySlotName;
        private string _activeIndexedSavePath;
        private float _nextIndexedDefragCheckTime;
        private float _nextPredictiveIndexedPagingCheckTime;
        private int _activeIndexedSaveChunkSizeMeters = SaveBinaryStorage.DefaultIndexedPersistentWorldChunkSizeMeters;
        private long _lastPredictiveIndexedPagedSectorHash = long.MinValue;

        private sealed class MemoryCorruptionException : Exception
        {
            public MemoryCorruptionException(string message) : base(message) { }
        }

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

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct IntegrityScanJob : IJob
        {
            [ReadOnly] public NativeArray<byte> PayloadBytes;
            public NativeArray<ulong> ResultHash64;

            public void Execute()
            {
                if (!PayloadBytes.IsCreated || PayloadBytes.Length <= 0 || !ResultHash64.IsCreated || ResultHash64.Length <= 0)
                {
                    if (ResultHash64.IsCreated && ResultHash64.Length > 0)
                        ResultHash64[0] = 0UL;
                    return;
                }

                void* payloadPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(PayloadBytes);
                ResultHash64[0] = SaveBinaryStorage.Hash64(payloadPtr, PayloadBytes.Length);
            }
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

        private void Awake()
        {
            _sessionStartTime = Time.realtimeSinceStartupAsDouble;
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
            if (_updatableRegistered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
                _updatableRegistered = false;
            }

            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);
                _lateFrameRegistered = false;
            }
        }

        private void OnDestroy()
        {
            if (_serviceRegistered && ReferenceEquals(GlobalRegistry.Save, this))
                GlobalRegistry.UnregisterSaveService(this);

            if (_updatableRegistered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
                _updatableRegistered = false;
            }

            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);
                _lateFrameRegistered = false;
            }

            _serviceRegistered = false;

            DisposeNativeArray(ref _savePayloadBuffer);
            DisposeNativeArray(ref _compressedSaveBuffer);

            DisposeIntegrityResources();
        }

        public void InitializeService()
        {
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
                // COLD ALLOC: NativeArray<byte>[67108864] - raw binary save staging buffer for save payload assembly - owner: SaveManager
                _savePayloadBuffer = new NativeArray<byte>(SaveBinaryStorage.RawPayloadCapacityBytes, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }

            if (!_compressedSaveBuffer.IsCreated)
            {
                // COLD ALLOC: NativeArray<byte>[67378176] - worst-case LZ4 block-compressed save payload buffer for 64MB raw save budget - owner: SaveManager
                _compressedSaveBuffer = new NativeArray<byte>(SaveBinaryStorage.MaxCompressedPayloadBytes, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }

            if (!_integrityScanResult.IsCreated)
            {
                // COLD ALLOC: NativeArray<ulong>[1] - background save integrity hash output - owner: SaveManager
                _integrityScanResult = new NativeArray<ulong>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            }

            RegisterNativeMemorySentinel();
        }

        public void Tick(float deltaTime)
        {
            SaveBinaryStorage.SetRuntimeLz4FastMode(deltaTime > CompressionFastModeFrameTimeThresholdSeconds);

            if (!_isBusy &&
                !_integrityScanScheduled &&
                !_indexedDefragInFlight &&
                !_predictiveIndexedPagingInFlight &&
                !string.IsNullOrEmpty(_activeIndexedSavePath) &&
                Time.unscaledTime >= _nextIndexedDefragCheckTime)
            {
                _indexedDefragInFlight = true;
                _nextIndexedDefragCheckTime = Time.unscaledTime + IndexedDefragCheckIntervalSeconds;
                _ = RunIndexedSaveDefragAsync(_activeIndexedSavePath);
                return;
            }

            TrySchedulePredictiveIndexedSectorPrewarm();

            if (_isBusy ||
                _integrityScanScheduled ||
                !_integrityPayloadMirror.IsCreated ||
                _integrityPayloadLength <= 0 ||
                Time.unscaledTime < _nextIntegrityScanTime)
            {
                return;
            }

            IntegrityScanJob job = new IntegrityScanJob
            {
                PayloadBytes = _integrityPayloadMirror.GetSubArray(0, _integrityPayloadLength),
                ResultHash64 = _integrityScanResult
            };
            _integrityScanHandle = job.Schedule();
            _integrityScanScheduled = true;
            _nextIntegrityScanTime = Time.unscaledTime + IntegrityScanIntervalSeconds;
        }

        public void LateFrameTick()
        {
            if (_integrityScanScheduled)
            {
                if (!_integrityScanHandle.IsCompleted)
                    return;

                if (!DispatcherJobSwap.TryComplete(ref _integrityScanHandle, forceComplete: false))
                    return;

                _integrityScanScheduled = false;
                EvaluateIntegrityScanResult();
            }

            if (_pendingIntegrityPayloadStage)
                FlushPendingIntegrityPayloadStage();
        }

        private unsafe void StageIntegrityPayload(NativeArray<byte> payloadBytes, int payloadLength, ulong expectedHash64, string slotName)
        {
            if (!payloadBytes.IsCreated || payloadLength <= 0 || payloadLength > payloadBytes.Length)
                return;

            if (_integrityScanScheduled)
            {
                if (!_integrityScanHandle.IsCompleted)
                {
                    QueuePendingIntegrityPayloadStage(payloadBytes, payloadLength, expectedHash64, slotName);
                    return;
                }

                if (!DispatcherJobSwap.TryComplete(ref _integrityScanHandle, forceComplete: false))
                {
                    QueuePendingIntegrityPayloadStage(payloadBytes, payloadLength, expectedHash64, slotName);
                    return;
                }

                _integrityScanScheduled = false;
            }

            CopyIntegrityPayload(payloadBytes, payloadLength, expectedHash64, slotName);
        }

        private void QueuePendingIntegrityPayloadStage(NativeArray<byte> payloadBytes, int payloadLength, ulong expectedHash64, string slotName)
        {
            _pendingIntegrityPayloadSource = payloadBytes;
            _pendingIntegrityPayloadLength = payloadLength;
            _pendingExpectedIntegrityPayloadHash64 = expectedHash64;
            _pendingIntegritySlotName = slotName ?? string.Empty;
            _pendingIntegrityPayloadStage = true;
        }

        private void FlushPendingIntegrityPayloadStage()
        {
            if (!_pendingIntegrityPayloadSource.IsCreated ||
                _pendingIntegrityPayloadLength <= 0 ||
                _pendingIntegrityPayloadLength > _pendingIntegrityPayloadSource.Length)
            {
                ClearPendingIntegrityPayloadStage();
                return;
            }

            CopyIntegrityPayload(
                _pendingIntegrityPayloadSource,
                _pendingIntegrityPayloadLength,
                _pendingExpectedIntegrityPayloadHash64,
                _pendingIntegritySlotName);

            ClearPendingIntegrityPayloadStage();
        }

        private void ClearPendingIntegrityPayloadStage()
        {
            _pendingIntegrityPayloadSource = default;
            _pendingIntegrityPayloadLength = 0;
            _pendingExpectedIntegrityPayloadHash64 = 0UL;
            _pendingIntegritySlotName = string.Empty;
            _pendingIntegrityPayloadStage = false;
        }

        private unsafe void CopyIntegrityPayload(NativeArray<byte> payloadBytes, int payloadLength, ulong expectedHash64, string slotName)
        {
            EnsureIntegrityMirrorCapacity(payloadLength);
            void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payloadBytes);
            void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_integrityPayloadMirror);
            if (!UnsafeMemoryCopyGuard.SafeCopy(destinationPtr, _integrityPayloadMirror.Length, sourcePtr, payloadLength))
                UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(SaveManager));
            _integrityPayloadLength = payloadLength;
            _expectedIntegrityPayloadHash64 = expectedHash64;
            _integritySlotName = slotName ?? string.Empty;
            _nextIntegrityScanTime = Time.unscaledTime + IntegrityScanIntervalSeconds;
        }

        private void EnsureIntegrityMirrorCapacity(int requiredLength)
        {
            if (_integrityPayloadMirror.IsCreated && _integrityPayloadMirror.Length >= requiredLength)
                return;

            if (_integrityPayloadMirror.IsCreated)
                DisposeNativeArray(ref _integrityPayloadMirror);

            // COLD ALLOC: NativeArray<byte>[requiredLength] - resident decompressed save payload mirror for integrity scans - owner: SaveManager
            _integrityPayloadMirror = new NativeArray<byte>(requiredLength, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            NativeMemorySentinel.RegisterNativeArray(_integrityPayloadMirror, NativeMemoryOwner, nameof(_integrityPayloadMirror), NativeMemoryLifetime);
        }

        private void EvaluateIntegrityScanResult()
        {
            if (!_integrityScanResult.IsCreated || _integrityScanResult.Length <= 0)
                return;

            ulong computedHash64 = _integrityScanResult[0];
            if (computedHash64 == _expectedIntegrityPayloadHash64)
                return;

            string slotName = string.IsNullOrEmpty(_integritySlotName) ? "active" : _integritySlotName;
            string reason = $"Active save integrity drift detected for '{slotName}'. Expected XXH3-64 {_expectedIntegrityPayloadHash64:X16}, got {computedHash64:X16}.";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[SaveManager] {reason}");
#endif
            SaveEvents.RaiseEmergencyBackupRestoreRequested(slotName);
            SaveEvents.RaiseSaveFailed(slotName, reason);
            ScheduleEmergencyBackup(slotName);
            _nextIntegrityScanTime = Time.unscaledTime + IntegrityScanIntervalSeconds;
        }

        private void ScheduleEmergencyBackup(string slotName)
        {
            if (_emergencyBackupScheduled || string.IsNullOrEmpty(slotName))
                return;

            _emergencyBackupScheduled = true;
            _ = RunEmergencyIntegrityBackupAsync(slotName);
        }

        private async Awaitable RunEmergencyIntegrityBackupAsync(string slotName)
        {
            string emergencySlotName = $"{slotName}{EmergencyIntegrityBackupSuffix}";
            try
            {
                await SaveGameAsync(emergencySlotName);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Emergency integrity backup failed for '{emergencySlotName}': {ex.Message}");
            }
            finally
            {
                _emergencyBackupScheduled = false;
            }
        }

        private void TrySchedulePredictiveIndexedSectorPrewarm()
        {
            if (_isBusy ||
                _indexedDefragInFlight ||
                _predictiveIndexedPagingInFlight ||
                string.IsNullOrEmpty(_activeIndexedSavePath) ||
                Time.unscaledTime < _nextPredictiveIndexedPagingCheckTime)
            {
                return;
            }

            _nextPredictiveIndexedPagingCheckTime = Time.unscaledTime + PredictiveIndexedPagingCheckIntervalSeconds;
            if (!TryResolvePredictiveIndexedPagingPlayer(out HectonPlayerMovement playerMovement, out Transform playerTransform))
                return;

            Vector3 currentRuntimePosition = playerTransform.position;
            Vector3 playerVelocity = playerMovement.CurrentWorldVelocity;
            if (!SavePredictivePagingMath.IsFinite(currentRuntimePosition) ||
                !SavePredictivePagingMath.IsFinite(playerVelocity))
                return;

            float planarSpeedSq = (playerVelocity.x * playerVelocity.x) + (playerVelocity.z * playerVelocity.z);
            float minPlanarSpeedSq = PredictiveIndexedPagingMinimumPlanarSpeedMetersPerSecond * PredictiveIndexedPagingMinimumPlanarSpeedMetersPerSecond;
            if (planarSpeedSq < minPlanarSpeedSq)
                return;

            int chunkSizeMeters = math.max(1, _activeIndexedSaveChunkSizeMeters);
            if (!SavePredictivePagingMath.TryComputeIndexedSectorProjection(
                    currentRuntimePosition,
                    playerVelocity,
                    PredictiveIndexedPagingLookaheadSeconds,
                    chunkSizeMeters,
                    out PredictiveIndexedPagingProjection projection))
            {
                return;
            }

            bool projectedChunkChanged = !math.all(projection.CurrentChunkId == projection.ProjectedChunkId);
            if ((!projectedChunkChanged && projection.ProjectedSectorHash == projection.CurrentSectorHash) ||
                projection.ProjectedSectorHash == _lastPredictiveIndexedPagedSectorHash)
            {
                return;
            }

            _lastPredictiveIndexedPagedSectorHash = projection.ProjectedSectorHash;
            _predictiveIndexedPagingInFlight = true;
            _ = RunPredictiveIndexedSectorPrewarmAsync(_activeIndexedSavePath, projection.ProjectedSectorHash);
        }

        private static bool TryResolvePredictiveIndexedPagingPlayer(out HectonPlayerMovement playerMovement, out Transform playerTransform)
        {
            playerMovement = null;
            playerTransform = null;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext == null)
                return false;

            playerMovement = playerContext.PlayerMovement;
            playerTransform = playerContext.PlayerTransform;
            return playerMovement != null && playerTransform != null;
        }

        private async Awaitable RunPredictiveIndexedSectorPrewarmAsync(string absolutePath, long sectorHash)
        {
            bool sectorExists = false;
            bool usedBackup = false;
            string error = string.Empty;
            try
            {
                await Awaitable.BackgroundThreadAsync();
                bool prewarmSucceeded = SaveBinaryStorage.TryPrewarmIndexedPersistentWorldSector(
                    absolutePath,
                    sectorHash,
                    out sectorExists,
                    out usedBackup,
                    out error);
                await Awaitable.MainThreadAsync();

                if (!prewarmSucceeded)
                {
                    PublishPredictiveIndexedSectorPrewarmWarning(_predictiveIndexedSectorPrewarmFailedWarningHash, sectorHash, 1f);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning($"[SaveManager] Predictive indexed sector prewarm failed for 0x{sectorHash:X16}: {error}");
#endif
                }
                else if (usedBackup)
                {
                    PublishPredictiveIndexedSectorPrewarmWarning(_predictiveIndexedSectorPrewarmBackupWarningHash, sectorHash, 1f);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning($"[SaveManager] Predictive indexed sector prewarm used .bak for 0x{sectorHash:X16}; primary will promote through CRITICAL_RECOVERY on load.");
#endif
                }
                else if (verboseLogging && sectorExists)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[SaveManager] Predictive indexed sector prewarmed 0x{sectorHash:X16}.");
#endif
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                await Awaitable.MainThreadAsync();
                PublishPredictiveIndexedSectorPrewarmWarning(_predictiveIndexedSectorPrewarmExceptionWarningHash, sectorHash, 1f);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[SaveManager] Predictive indexed sector prewarm threw for 0x{sectorHash:X16}: {ex.Message}");
#endif
            }
            finally
            {
                await Awaitable.MainThreadAsync();
                _predictiveIndexedPagingInFlight = false;
                _nextPredictiveIndexedPagingCheckTime = Time.unscaledTime + PredictiveIndexedPagingCheckIntervalSeconds;
            }
        }

        private static void PublishPredictiveIndexedSectorPrewarmWarning(uint warningHash, long sectorHash, float scalarValue)
        {
            unchecked
            {
                ulong packedSectorHash = (ulong)sectorHash;
                uint contextHash = _predictiveIndexedSectorPrewarmContextHash ^ (uint)packedSectorHash ^ (uint)(packedSectorHash >> 32);
                GlobalTelemetryBus.PublishPerformanceWarning(warningHash, contextHash, scalarValue);
            }
        }

        private async Awaitable WaitForIndexedSaveMaintenanceIdleAsync()
        {
            while (_indexedDefragInFlight || _predictiveIndexedPagingInFlight)
                await Awaitable.NextFrameAsync(cancellationToken: destroyCancellationToken);
        }

        private async Awaitable RunIndexedSaveDefragAsync(string absolutePath)
        {
            long reclaimedBytes = 0L;
            string error = string.Empty;
            try
            {
                await Awaitable.BackgroundThreadAsync();
                SaveBinaryStorage.TryDefragmentIndexedPersistentWorldSectors(
                    absolutePath,
                    SaveBinaryStorage.IndexedSectorDefragSlackThresholdBytes,
                    out reclaimedBytes,
                    out error);
                await Awaitable.MainThreadAsync();

                if (!string.IsNullOrEmpty(error))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogError($"[SaveManager] Indexed save defrag failed for '{absolutePath}': {error}");
#endif
                }
                else if (verboseLogging && reclaimedBytes > 0L)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[SaveManager] Indexed save defrag reclaimed {reclaimedBytes} bytes for '{absolutePath}'.");
#endif
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                await Awaitable.MainThreadAsync();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError($"[SaveManager] Indexed save defrag threw for '{absolutePath}': {ex.Message}");
#endif
            }
            finally
            {
                await Awaitable.MainThreadAsync();
                _indexedDefragInFlight = false;
                _nextIndexedDefragCheckTime = Time.unscaledTime + IndexedDefragCheckIntervalSeconds;
            }
        }

        private void UpdateActiveIndexedSavePath(string absolutePath)
        {
            _activeIndexedSavePath = string.Empty;
            _activeIndexedSaveChunkSizeMeters = SaveBinaryStorage.DefaultIndexedPersistentWorldChunkSizeMeters;
            _lastPredictiveIndexedPagedSectorHash = long.MinValue;
            _nextPredictiveIndexedPagingCheckTime = 0f;
            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
                return;

            _indexedSectorDirectoryScratch.Clear();
            if (!SaveBinaryStorage.TryReadIndexedPersistentWorldDirectory(
                    absolutePath,
                    _indexedSectorDirectoryScratch,
                    out _activeIndexedSaveChunkSizeMeters,
                    out string directoryError))
            {
                ReportIndexedDirectoryReadFailure(absolutePath, directoryError);
                return;
            }

            _activeIndexedSavePath = absolutePath;
            _nextIndexedDefragCheckTime = Time.unscaledTime + IndexedDefragCheckIntervalSeconds;
        }

        private void DisposeIntegrityResources()
        {
            DisposeNativeArray(ref _integrityPayloadMirror, _integrityScanHandle, _integrityScanScheduled);
            DisposeNativeArray(ref _integrityScanResult, _integrityScanHandle, _integrityScanScheduled);

            _integrityScanScheduled = false;
            _integrityPayloadLength = 0;
            _expectedIntegrityPayloadHash64 = 0UL;
            ClearPendingIntegrityPayloadStage();
        }

        private void RegisterNativeMemorySentinel()
        {
            NativeMemorySentinel.RegisterNativeArray(_savePayloadBuffer, NativeMemoryOwner, nameof(_savePayloadBuffer), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_compressedSaveBuffer, NativeMemoryOwner, nameof(_compressedSaveBuffer), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_integrityScanResult, NativeMemoryOwner, nameof(_integrityScanResult), NativeMemoryLifetime);
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

            if (HectonFloatingOrigin.IsShiftInProgress || HectonFloatingOrigin.IsPhysicsPausedForShift)
            {
                const string reason = "Save blocked during floating-origin shift.";
                LastOperationError = reason;
                Debug.LogWarning($"[SaveManager] Ignored save request for '{slotName}': {reason}");
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
                await WaitForIndexedSaveMaintenanceIdleAsync();
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
                    PlayerPosition = data.playerStats.GetPosition()
                };

                snapshotTimer.Stop();
                WarnIfSnapshotBudgetExceeded(slotName, snapshotTimer.ElapsedMilliseconds);

                string tempPath = GetTempSaveFilePath(slotName);
                if (divergenceSnapshotTimer.ElapsedTicks > PreCompressionYieldBudgetTicks)
                    await Awaitable.NextFrameAsync();

                await Awaitable.BackgroundThreadAsync();

                ulong payloadHash64;
                int rawPayloadLength;

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

                await Awaitable.MainThreadAsync();
                StageIntegrityPayload(_savePayloadBuffer, rawPayloadLength, payloadHash64, slotName);
                UpdateActiveIndexedSavePath(GetPersistentAbsolutePath(GetPrimarySaveFilePath(slotName)));
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

            Debug.LogWarning(
                $"[SaveManager] Main-thread snapshot for '{slotName}' took {snapshotElapsedMs}ms. " +
                $"Budget is {MainThreadSnapshotBudgetMs}ms. Snapshot purity is pending verification.");
        }

        private static void StampRuntimeWorldSeed(SaveData data)
        {
            if (data == null)
                return;

            IWorldSeedProvider seedProvider = GlobalRegistry.WorldSeedProvider;
            if (seedProvider == null || !seedProvider.IsInitialized)
                return;

            data.ecosystemState.worldSeed = seedProvider.RuntimeWorldSeed;
        }

        private static void ValidateRuntimeWorldSeed(SaveData data)
        {
            if (data == null)
                return;

            int savedSeed = data.ecosystemState.worldSeed;
            if (savedSeed == 0)
                return;

            IWorldSeedProvider seedProvider = GlobalRegistry.WorldSeedProvider;
            if (seedProvider == null || !seedProvider.IsInitialized)
                return;

            int runtimeSeed = seedProvider.RuntimeWorldSeed;
            if (runtimeSeed == 0 || runtimeSeed == savedSeed)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[SaveManager] Geological Anomaly: saved world seed {savedSeed} != runtime world seed {runtimeSeed}.");
#endif
            NotificationEvents.PushWarning("GEOLOGICAL ANOMALY");
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

            _cachedLoadingScreenController = FindAnyObjectByType<LoadingScreenController>(FindObjectsInactive.Include);
            return _cachedLoadingScreenController;
        }

        private static void ReportCriticalSectorCorruptionDialog()
        {
            NotificationEvents.PushCritical(CriticalSectorCorruptionMessage);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[SaveManager] {CriticalSectorCorruptionMessage}");
#endif
        }

        private static bool TryApplySafeAupSnapOnLoad(SaveData data)
        {
            if (data == null)
                return false;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            Transform playerTransform = playerContext != null ? playerContext.PlayerTransform : null;
            if (playerTransform == null && !SceneBootstrap.TryGetCurrentPlayerTransform(out playerTransform))
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
            Vector3 castOrigin = resolvedRuntimePosition + (Vector3.up * SafeAupSnapCastStartHeightMeters);
            if (!UnityEngine.Physics.SphereCast(
                    castOrigin,
                    SafeAupSnapSphereRadiusMeters,
                    Vector3.down,
                    out RaycastHit hit,
                    SafeAupSnapCastDistanceMeters,
                    UnityEngine.Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            float safeY = hit.point.y + SafeAupSnapGroundPaddingMeters;
            if (safeY <= resolvedRuntimePosition.y + SafeAupSnapMinimumLiftMeters)
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
            ReportLoadPipelineStage(LoadingPipelineStage.PagingSectors, 0.08f);
            var totalTimer = Stopwatch.StartNew();
            NativeArray<byte> loadedVoxelDeltaSnapshot = default;

            try
            {
                await WaitForIndexedSaveMaintenanceIdleAsync();
                SaveBinaryStorage.ConsumeIndexedSectorQuarantineFlag();
                await Awaitable.BackgroundThreadAsync();
                SaveData data = null;
                QuestSaveHeader loadedQuestHeader = default;
                uint[] loadedQuestStateWords = null;
                PersistentWorldDeltaRecord[] loadedWorldDeltas = null;
                EcosystemSectorSaveRecord[] loadedEcosystemSectors = null;
                SaveMetadata loadedMetadata = null;
                SaveLoadCandidate loadedCandidate = default;
                Exception lastError = null;
                List<SaveLoadCandidate> candidates = BuildLoadCandidates(slotName);
                bool usedLegacyFormat = false;
                ulong loadedPayloadHash64 = 0UL;
                int loadedPayloadLength = 0;
                bool criticalBackupPromotedForLoad = false;
                int criticalBackupGenerationForLoad = 0;

                for (int i = 0; i < candidates.Count; i++)
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
                                lastError = new Exception(candidateError);
                                Debug.LogWarning($"[SaveManager] CRITICAL_RECOVERY failed for '{slotName}': {candidateError}");
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
                            ? new SaveLoadCandidate(GetPrimarySaveFilePath(slotName), false, 0)
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
                        loadedCandidate = new SaveLoadCandidate(GetPrimarySaveFilePath(slotName), false, 0);
                        loadedMetadata = recoveryMetadata;
                        loadedPayloadHash64 = recoveryPayloadHash64;
                        loadedPayloadLength = recoveryPayloadLength;
                        usedLegacyFormat = recoveryUsedLegacyFormat;
                        criticalBackupPromotedForLoad = true;
                        criticalBackupGenerationForLoad = recoveryBackupGeneration;
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
                StageIntegrityPayload(_savePayloadBuffer, loadedPayloadLength, loadedPayloadHash64, slotName);
                ReportLoadPipelineStage(LoadingPipelineStage.HydratingEntities, 0.42f);

                if (SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary))
                {
                    Debug.Log($"[SaveManager] Migrated save '{slotName}' from v{originalVersion}: {summary}");
                }

                ValidateRuntimeWorldSeed(data);
                _totalPlayTime = data.totalPlayTime;
                _sessionStartTime = Time.realtimeSinceStartupAsDouble;
                PersistentWorldRegistry persistentWorldRegistryForLoad = GlobalRegistry.PersistentWorldRegistry;
                persistentWorldRegistryForLoad?.PreloadTombstonesFromLoadedRecords(loadedWorldDeltas);
                ModSaveStateStore.LoadFromSaveData(data);
                if (!ModSaveStateStore.TryLoadMmfPayloads(GetPersistentAbsolutePath(loadedCandidate.SavePath), out string modPayloadLoadError) ||
                    !string.IsNullOrEmpty(modPayloadLoadError))
                {
                    ReportModPayloadLoadFailure(slotName, modPayloadLoadError);
                }

                QuestManager.StageLoadedPackedState(loadedQuestHeader, loadedQuestStateWords);
                
                _registryDirty = true;
                SortRegistryIfDirty(LoadPriorityComparer);

                VoxelDeltaProcessor voxelDeltaProcessor = null;
                long loadApplyDeadlineTicks = Stopwatch.GetTimestamp() + LoadApplyFrameBudgetTicks;
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
                        await Awaitable.NextFrameAsync(cancellationToken: destroyCancellationToken);
                        loadApplyDeadlineTicks = Stopwatch.GetTimestamp() + LoadApplyFrameBudgetTicks;
                    }
                }

                if (voxelDeltaProcessor != null && !voxelDeltaProcessor.TryLoadNativeSnapshot(loadedVoxelDeltaSnapshot, out string voxelLoadError))
                    throw new Exception(voxelLoadError);

                if (persistentWorldRegistryForLoad != null)
                {
                    ReportLoadPipelineStage(LoadingPipelineStage.PagingSectors, 0.68f);
                    string loadedAbsolutePath = GetPersistentAbsolutePath(loadedCandidate.SavePath);
                    _indexedSectorDirectoryScratch.Clear();
                    if (SaveBinaryStorage.TryReadIndexedPersistentWorldDirectory(
                            loadedAbsolutePath,
                            _indexedSectorDirectoryScratch,
                            out _,
                            out string loadedDirectoryError))
                    {
                        persistentWorldRegistryForLoad.RestoreFromIndexedSave(loadedAbsolutePath);

                        string primaryAbsolutePath = GetPersistentAbsolutePath(GetPrimarySaveFilePath(slotName));
                        UpdateActiveIndexedSavePath(FileExists(GetPrimarySaveFilePath(slotName)) ? primaryAbsolutePath : loadedAbsolutePath);
                    }
                    else
                    {
                        ReportIndexedDirectoryReadFailure(loadedAbsolutePath, loadedDirectoryError);
                        UpdateActiveIndexedSavePath(string.Empty);
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
                        PlayerPosition = playerPosition
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
                Debug.Log($"[SaveManager] Loaded '{slotName}' from {sourceLabel} in {totalTimer.ElapsedMilliseconds}ms{loadCompletionSuffix}");
                SaveEvents.RaiseLoadCompleted(slotName);
            }
            catch (Exception ex)
            {
                RecordFailure(slotName, "load", ex.Message);
                LastOperationError = ex.Message;
                Debug.LogError($"[SaveManager] Load failed: {ex.Message}");
                SaveEvents.RaiseLoadFailed(slotName, ex.Message);
                HideLoadingPipelineScreen();
            }
            finally
            {
                if (loadedVoxelDeltaSnapshot.IsCreated)
                    DisposeNativeArray(ref loadedVoxelDeltaSnapshot);

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

            SaveManager manager = GlobalRegistry.SaveRuntime;
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

            Debug.LogWarning($"[SaveManager] Mod payload load warning for '{slotName}': {error}");
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void ReportModPayloadCommitFailure(string slotName, string error)
        {
            if (string.IsNullOrEmpty(error))
                return;

            Debug.LogWarning($"[SaveManager] Mod payload commit failed for '{slotName}': {error}");
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void ReportIndexedDirectoryReadFailure(string absolutePath, string error)
        {
            if (string.IsNullOrEmpty(error))
                return;

            Debug.LogWarning($"[SaveManager] Indexed save directory read failed for '{absolutePath}': {error}");
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
            QuestSaveHeader packedQuestHeader = default;
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
                    selectedCandidate = candidates[i];
                    usedLegacyFormat = candidateUsedLegacyFormat;
                    break;
                }

                errorMessage = candidateError;
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

            SaveLoadCandidate backupCandidate = new SaveLoadCandidate(backupSavePath, true, backupGeneration);
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
            Debug.LogError($"[SaveManager] CRITICAL_RECOVERY promoted '{backupSavePath}' to '{GetPrimarySaveFilePath(slotName)}'. Primary failure: {primaryError}");
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

            if (!FileExists(candidate.SavePath))
            {
                errorMessage = $"Save artifact '{candidate.SavePath}' is missing.";
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
                    GetPersistentAbsolutePath(candidate.SavePath),
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
                        // COLD ALLOC: NativeArray<PersistentWorldDeltaRecord>[persistentWorldItems.Length] - static save assembly staging buffer - owner: SaveManager
                        persistentWorldItemBuffer = new NativeArray<PersistentWorldDeltaRecord>(
                            persistentWorldItems.Length,
                            Allocator.Temp,
                            NativeArrayOptions.UninitializedMemory);
                        RegisterTransientNativeArray(persistentWorldItemBuffer, "persistentWorldItemBuffer");
                        persistentWorldItemBuffer.CopyFrom(persistentWorldItems);
                    }

                    if (ecosystemSectorStates != null && ecosystemSectorStates.Length > 0)
                    {
                        // COLD ALLOC: NativeArray<EcosystemSectorSaveRecord>[ecosystemSectorStates.Length] - static save assembly staging buffer - owner: SaveManager
                        ecosystemSectorBuffer = new NativeArray<EcosystemSectorSaveRecord>(
                            ecosystemSectorStates.Length,
                            Allocator.Temp,
                            NativeArrayOptions.UninitializedMemory);
                        RegisterTransientNativeArray(ecosystemSectorBuffer, "ecosystemSectorBuffer");
                        ecosystemSectorBuffer.CopyFrom(ecosystemSectorStates);
                    }

                    if (packedQuestStateWords != null && packedQuestStateWords.Length > 0)
                    {
                        // COLD ALLOC: NativeArray<UInt32>[packedQuestStateWords.Length] - static save assembly staging buffer - owner: SaveManager
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

            // COLD ALLOC: NativeArray<byte>[67108864] - fallback raw save read buffer when SaveManager instance is unavailable - owner: SaveManager
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

            // COLD ALLOC: NativeArray<byte>[67108864] - fallback raw save write buffer when SaveManager instance is unavailable - owner: SaveManager
            rawBuffer = new NativeArray<byte>(SaveBinaryStorage.RawPayloadCapacityBytes, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            // COLD ALLOC: NativeArray<byte>[67378176] - fallback compressed save write buffer when SaveManager instance is unavailable - owner: SaveManager
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
