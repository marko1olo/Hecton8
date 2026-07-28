// ============================================================================
// HECTON-8 — SaveManager.cs
// Save persistence service. Runtime owner is injected through the core registry.
//
// ARHITEKTURA:
//   • Reestr ISaveable cherez explicit registration (zero GC pri save/load).
//   • XXHash3 checksums for header/payload integrity.
//   • Unity 6 Awaitable API: BackgroundThreadAsync / MainThreadAsync.
// ============================================================================

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Bootstrap;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Core.Persistence.Paging;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Modding;
using Hecton8.Quest;
using Hecton8.UI;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.SaveSystem
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-8000)]
    public sealed class SaveManager : MonoBehaviour, IAsyncPersistenceService, IUpdatable, ISlowTickable, IFrostTickable, ILateFrameTickable, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
#if UNITY_EDITOR
        internal static Action TestHookSimulateCleanupFailure;
#endif
        private static int _signalPushDropCount;
        private static readonly List<SaveManager> s_KnownInstances = new List<SaveManager>();
        private static int s_geologicalAnomalyNotificationMissCount;
        private static int s_criticalSectorCorruptionNotificationMissCount;

#if UNITY_EDITOR
        internal static Action Test_OnBeforeShutdownServiceState;
#endif
        private const long MainThreadSnapshotBudgetMs = 5L;
        private static readonly long PreCompressionYieldBudgetTicks = Math.Max(1L, Stopwatch.Frequency / 500L);
        private static readonly long LoadApplyFrameBudgetTicks = HydrationScheduler.FrameBudgetTicks;
        private const int SaveStagingBufferBytes = 10 * 1024 * 1024;
        private const int SaveTelemetryCapacity = 300;
        private const int WfcOutpostTelemetryCapacity = 300;
        private const int WfcOutpostEventTelemetryCapacity = 300;
        private const uint AsyncPersistenceSourceHash = 0x41505953u; // APYS
        private const uint WorldPagerSourceHash = 0x48384250u; // H8BP
        private const uint WfcOutpostPersistenceSourceHash = 0x57464350u; // WFCP
        private const uint WfcBytesSavedTelemetryHash = 0x57464253u; // WFBS
        private const uint WfcCorruptPayloadTelemetryHash = 0x57464358u; // WFCX
        private const uint WfcWriteFailureTelemetryHash = 0x57465746u; // WFWF
        private const uint WorldPagerSavingMessageHash = 0x53415647u; // SAVG
        private const uint WorldPagerSavingContextHash = 0x48384249u; // H8BI
        private const uint SaveRecoveredMessageHash = 0x53565243u; // SVRC
        private const uint SaveRecoveredContextHash = 0x42414B52u; // BAKR
        private const uint SaveSynchronizedMessageHash = 0x53565359u; // SVSY
        private const uint SaveDurationTelemetryHash = 0x5356444Du; // SVDM
        private const uint SaveCompressedSizeTelemetryHash = 0x53564342u; // SVCB
        private const uint ScreenshotSizeKbTelemetryHash = 0x53534B42u; // SSKB
        private const uint PersistenceCleanupFailureTelemetryHash = 0x53564346u; // SVCF
        private const uint PersistenceCleanupSaveContextHash = 0x53564353u; // SVCS
        private const uint PersistenceCleanupLoadContextHash = 0x5356434Cu; // SVCL
        private const uint PersistenceCleanupGateContextHash = 0x53564754u; // SVGT
        private const uint PersistenceSignalBridgeContextHash = 0x53565347u; // SVSG
        private const uint LoadPriorityConflictTelemetryHash = 0x4C504346u; // LPCF
        private const uint LoadPriorityConflictContextSeedHash = 0x4C50524Fu; // LPRO
        private const uint SaveableRegistryOverflowTelemetryHash = 0x53524F46u; // SROF
        private const uint ModPayloadLoadFallbackTelemetryHash = 0x4D504C46u; // MPLF
        private const uint SaveManagerNotificationContextHash = 0x534E5446u; // SNTF
        private const uint GeologicalAnomalyNotificationContextHash = 0x47414E46u; // GANF
        private const uint GeologicalAnomalyNotificationMissTelemetryHash = 0x47414E4Du; // GANM
        private const uint TerrainIdentityMismatchTelemetryHash = 0x54494D4Du; // TIMM
        private const uint TerrainIdentityMismatchContextHash = 0x54494443u; // TIDC
        private const uint CriticalSectorCorruptionNotificationContextHash = 0x4353434Eu; // CSCN
        private const uint CriticalSectorCorruptionNotificationMissTelemetryHash = 0x43534E4Du; // CSNM
        private const uint SaveOwnerCensusTelemetryHash = 0x534F434Eu; // SOCN
        private const uint SaveOwnerCensusLoadContextSeedHash = 0x534F434Cu; // SOCL
        private const uint SaveOwnerCensusSaveContextSeedHash = 0x534F4353u; // SOCS
        private const uint DeferredOwnerHydrationTelemetryHash = 0x444F4844u; // DOHD
        private const uint DeferredOwnerHydrationExpiredTelemetryHash = 0x444F4858u; // DOHX
        private const uint AsyncPersistenceOwnerCensusFailureFlag = 1u << 4;
        private const uint AsyncPersistenceDeferredHydrationAppliedFlag = 1u << 5;
        private const uint AsyncPersistenceDeferredHydrationExpiredFlag = 1u << 6;
        private const uint AsyncPersistenceLoadOperationFlag = 1u << 7;
        // Deferred-hydration window. GameBootstrapper re-enables the player several scene-activation
        // steps after Step 4: Save/Load, and Step 5/6 wait on world-ready and ground-ready gates that
        // are themselves bounded by the bootstrap timeout. 30 s of unscaled time covers that gap with
        // margin on a weak-tier load without pinning the loaded payload for the whole session.
        private const double DeferredOwnerHydrationWindowSeconds = 30d;
        private const int MaxChunkDehydrationSignalsPerTick = 2;
        private const int MaxWfcSectorHydrationProbesPerTick = 4;
        private const int MaxWfcDirtySectorStackEntries = 256;
        private const int WfcOutpostSnapshotCacheCapacity = 256;
        private const int WfcOutpostGridSnapshotScratchBytes = (WfcOutpostPersistenceConstants.CellCount + 7) & ~7;
        private const int MaxWfcDirtyAppendRetriesPerSlowTick = 2;
        private const float SafeAupSnapGroundPaddingMeters = 0.28f;
        private const float SafeAupSnapMinimumLiftMeters = 0.35f;
        private const string CriticalSectorCorruptionMessage = "CRITICAL ERROR: LOCALIZED DATA CORRUPTION. TERRAIN RE-INITIALIZED.";
        private const string GeologicalAnomalyDetectedMessage = "UNSTABLE REALITY";
        private const string WfcOutpostBlackBoxDumpRelativePath = "Docs/AgentLogs/Dump_SAVE_MANAGER_WFC_PERSISTENCE_SYNC.bin";
        private const string AsyncPersistenceBlackBoxDumpRelativePath = "Docs/AgentLogs/Dump_SAVE_MANAGER_ASYNC_PERSISTENCE.bin";
        private const int MaxRegisteredSaveables = 256;
        private const int MaxSaveSlotNameLength = 48;
        private const int SaveSlotScratchCapacity = 8;
        private const int MaxSaveLoadCandidateCount = 9;
        private const string InvalidSlotNameReason = "Invalid save slot name.";
        private const string SaveServiceUnavailableReason = "Save service unavailable.";
        private const string RespawnReconciliationInProgressReason = "Save blocked during respawn reconciliation.";
        private const string InvalidSlotFileStem = "slot_invalid";
        private const uint LoadStatusFlags = SaveStatusSignal.LoadOperationFlag;
        private const uint LoadFailureStatusFlags = SaveStatusSignal.FailureFlag | SaveStatusSignal.LoadOperationFlag;
        private static readonly long CompressionThrottleBudgetTicks = Math.Max(1L, Stopwatch.Frequency / 100L);
        private const string NativeMemoryOwner = nameof(SaveManager);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Session;
        private const NativeAllocationLifetime NativeTransientMemoryLifetime = NativeAllocationLifetime.TransientArena;
        private const string NativeMemoryRegistrationFailureMessage = "NativeMemorySentinel registration failed for persistent SaveManager buffer.";
        private const string NativeMemoryTransientRegistrationFailureMessage = "NativeMemorySentinel registration failed for transient SaveManager buffer.";
        private const uint WfcOutpostBlackBoxMagic = 0x57464342u; // WFCB
        private const uint WfcOutpostBlackBoxVersion = 3u;
        private const uint WfcOutpostBlackBoxOperationPersist = 0x50525354u; // PRST
        private const uint WfcOutpostBlackBoxOperationRestore = 0x52535452u; // RSTR
        private const uint WfcOutpostBlackBoxOperationHydration = 0x48594452u; // HYDR
        private const uint WfcOutpostBlackBoxOperationSignal = 0x5349474Eu; // SIGN
        private const uint WfcOutpostBlackBoxOperationAppend = 0x41504E44u; // APND
        private const uint WfcOutpostBlackBoxOperationFrame = 0x4652414Du; // FRAM
        private const uint WfcOutpostBlackBoxAppendFlagException = 1u << 0;
        private const uint WfcOutpostBlackBoxSignalFlagOverflow = 1u << 1;
        private const uint WfcOutpostSnapshotCacheFlagAppendPending = 1u << 0;
        private const uint WfcOutpostSnapshotCacheFlagAppendInFlight = 1u << 1;
        private const uint WfcOutpostSnapshotCacheFlagAppendAny =
            WfcOutpostSnapshotCacheFlagAppendPending | WfcOutpostSnapshotCacheFlagAppendInFlight;

        // ----------------------------------------------------------
        //  SAVE STATE
        // ----------------------------------------------------------

        // ----------------------------------------------------------
        //  SERVICE STATE
        // ----------------------------------------------------------

        public bool IsInitialized => _serviceRegistered && !_runtimeOwnerAborted;
        public ServiceHeartbeatState HeartbeatState => IsInitialized ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;
        public bool IsServiceReady => IsInitialized;
        public bool IsBusy => _isBusy;
        public static int GeologicalAnomalyNotificationMissCount => s_geologicalAnomalyNotificationMissCount;
        public static int CriticalSectorCorruptionNotificationMissCount => s_criticalSectorCorruptionNotificationMissCount;
        public float CurrentPlayTimeSeconds => _runtimeOwnerAborted ? 0f : (float)ResolveCurrentPlayTimeSeconds();
        public bool LastOperationSucceeded { get; private set; }
        public string LastOperationError { get; private set; }
        public string LastOperationSlot { get; private set; }
        public bool LastLoadUsedBackup { get; private set; }
        public int LastLoadBackupGeneration { get; private set; }
        public bool LastLoadSelfRepaired { get; private set; }
        public bool LastLoadUsedLegacyCompression { get; private set; }

        /// <summary>
        /// Required contract categories that had no live registered owner when the last load
        /// reached its apply loop. Zero means the payload reached a complete owner set.
        /// Read-only projection: a headless probe or player-build diagnostic can assert on this
        /// without the Inspector, which is the only place <c>_debugRegisteredCount</c> was ever visible.
        /// </summary>
        public uint LastLoadMissingOwnerCategories { get; private set; }

        /// <summary>Required contract categories that had no live registered owner when the last save populated its payload.</summary>
        public uint LastSaveMissingOwnerCategories { get; private set; }

        /// <summary>Live registrants that actually consumed the payload in the last load apply loop.</summary>
        public int LastLoadAppliedOwnerCount { get; private set; }

        /// <summary>Live registrants present in the registry when the last load reached its apply loop.</summary>
        public int LastLoadLiveOwnerCount { get; private set; }

        /// <summary>Owners hydrated by the deferred pass after they re-registered post-load.</summary>
        public int DeferredOwnerHydrationAppliedCount { get; private set; }

        /// <summary>True while a loaded payload is retained for owners that were absent at apply time.</summary>
        public bool HasPendingOwnerHydration => _pendingOwnerHydrationData != null;

        /// <summary>Categories the retained payload is still waiting to hand to a re-registering owner.</summary>
        public uint PendingOwnerHydrationMissingCategories => _pendingOwnerHydrationMissingCategories;

        public ushort PlayerDialogueChoiceFlags =>
            SaveBinaryStorage.SanitizePlayerDialogueChoiceFlags((ushort)(Volatile.Read(ref _playerDialogueChoiceFlags) & ushort.MaxValue));

        public void RecordPlayerDialogueChoiceFlag(ushort decisionMask)
        {
            decisionMask = SaveBinaryStorage.SanitizePlayerDialogueChoiceFlags(decisionMask);
            if (decisionMask == 0)
                return;

            int mask = decisionMask;
            int snapshot;
            int updated;
            do
            {
                snapshot = Volatile.Read(ref _playerDialogueChoiceFlags);
                updated = snapshot | mask;
                if (snapshot == updated)
                    return;
            }
            while (Interlocked.CompareExchange(ref _playerDialogueChoiceFlags, updated, snapshot) != snapshot);
        }

        private const int DefaultManualBackupGenerations = 3;
        private const int DefaultAutoBackupGenerations = 2;
        private const int DefaultQuickBackupGenerations = 2;
        private const int MaxBackupGenerations = 8;
        public const int MaxKnownArtifactPathCount = 3 + MaxBackupGenerations;

        // ----------------------------------------------------------
        //  INSPECTOR
        // ----------------------------------------------------------

        [Header("-- Settings ----------------------------------")]

        [Header("-- Backup Policy -----------------------------")]
        [SerializeField] private int manualBackupGenerations = DefaultManualBackupGenerations;
        [SerializeField] private int autoBackupGenerations = DefaultAutoBackupGenerations;
        [SerializeField] private int quickBackupGenerations = DefaultQuickBackupGenerations;

        [Header("-- Diagnostics -------------------------------")]
        [SerializeField] private bool verboseLogging;
        [SerializeField] private int _debugRegisteredCount;

        // COLD ALLOC: ISaveable[256] — fixed persistence registry prevents List resize during scene registration — owner: SaveManager
        private readonly ISaveable[] _saveables = new ISaveable[MaxRegisteredSaveables];
        // COLD ALLOC: List<IndexedSectorEntryInfo>[128] — reusable indexed-save directory probe scratch — owner: SaveManager
        private readonly SaveBinaryStorage.IndexedSectorEntryInfo[] _indexedSectorDirectoryScratch = new SaveBinaryStorage.IndexedSectorEntryInfo[128];
        // COLD ALLOC: List<SaveSlotInfo>[8] - instance-owned metadata projection scratch - owner: SaveManager
        private readonly SaveSlotInfo[] _saveSlotInfoScratch = new SaveSlotInfo[SaveSlotScratchCapacity];
        // COLD ALLOC: ulong[256] - WFC dirty-sector Tick scratch capped to fixed signal storm budget - owner: SaveManager
        private readonly ulong[] _wfcDirtySectorScratch = new ulong[MaxWfcDirtySectorStackEntries];
        // COLD ALLOC: ushort[256] - WFC dirty-cell index Tick scratch capped to fixed signal storm budget - owner: SaveManager
        private readonly ushort[] _wfcDirtyCellIndexScratch = new ushort[MaxWfcDirtySectorStackEntries];
        // COLD ALLOC: byte[256] - WFC dirty-cell flag Tick scratch capped to fixed signal storm budget - owner: SaveManager
        private readonly byte[] _wfcDirtyCellFlagScratch = new byte[MaxWfcDirtySectorStackEntries];
        // COLD ALLOC: SaveManagerNativeBufferSet[1] - native save buffer owner indirection - owner: SaveManager
        private SaveManagerNativeBufferSet _nativeBuffers = new SaveManagerNativeBufferSet();

        private ref NativeArray<SaveLoadCandidate> _loadCandidateScratch => ref _nativeBuffers.LoadCandidateScratch;

        private int _saveableCount;
        private bool _registryDirty;
        private bool _saveableCapacityWarningLogged;
        private int _lastLoadPriorityConflictCount;
        private int _lastLoadPriorityConflictFrame;
        private double _sessionStartTime;
        private double _totalPlayTime;
        private bool _isBusy;
        private int _playerDialogueChoiceFlags;
        private H8BinaryWorldPager _worldPager;

        // Deferred owner hydration. GameBootstrapper.DisablePlayer() runs before "Step 4: Save/Load",
        // so every player-owned ISaveable has already fired OnDisable -> Unregister by the time the
        // apply loop runs. The loaded payload is retained here, with the still-outstanding category
        // mask, and handed to those owners on the frame after they re-register.
        private SaveData _pendingOwnerHydrationData;
        private uint _pendingOwnerHydrationMissingCategories;
        private uint _pendingOwnerHydrationSlotHash;
        private uint _pendingOwnerHydrationOperationId;
        private double _pendingOwnerHydrationDeadlineSeconds;
        private bool _pendingOwnerHydrationDrainRequested;

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
        private static ref NativeArray<SaveLoadCandidate> SaveLoadCandidateScratch => ref StaticNativeBuffers.SaveLoadCandidateScratch;
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

        private sealed class SaveManagerNativeBufferSet : IDisposable
        {
            public NativeArray<SaveLoadCandidate> LoadCandidateScratch;
            public NativeArray<byte> SavePayloadBuffer;
            public NativeArray<byte> CompressedSaveBuffer;
            public NativeArray<byte> SaveStagingBuffer;
            public VaultGenerationHandle<byte> WfcOutpostGridHandle;
            public NativeArray<ulong> WfcOutpostPackedWords;
            public NativeArray<ulong> WfcOutpostRestoreWords;
            public NativeArray<byte> WfcOutpostGridSnapshotScratch;
            public NativeArray<byte> WfcOutpostPayloadBuffer;
            public NativeArray<WfcOutpostSnapshotCacheEntry> WfcOutpostSnapshotCache;
            public NativeArray<AsyncPersistenceTelemetryEntry> SaveTelemetryRing;
            public NativeArray<WfcOutpostTelemetryEntry> WfcOutpostTelemetryRing;
            public NativeArray<WfcOutpostTelemetryEntry> WfcOutpostEventTelemetryRing;

            public void EnsureInitial()
            {
                EnsureSaveTelemetryRing();
                EnsureWfcOutpostBlackBoxRing();
                EnsureWfcOutpostNativeBuffers();
                EnsureSaveStagingBuffer();
                EnsureLoadCandidateScratch();
            }

            public void EnsureSaveWorkingBuffers()
            {
                EnsureSavePayloadBuffer();
                EnsureCompressedSaveBuffer();
                EnsureSaveStagingBuffer();
                EnsureLoadCandidateScratch();
                EnsureSaveTelemetryRing();
            }

            public void EnsureSavePayloadBuffer()
            {
                if (SavePayloadBuffer.IsCreated)
                    return;

                // COLD ALLOC: NativeArray<byte>[67108864] - raw binary save staging buffer for save payload assembly - owner: SaveManagerNativeBufferSet
                SavePayloadBuffer = CreatePersistentNativeArray<byte>(
                    SaveBinaryStorage.RawPayloadCapacityBytes,
                    NativeArrayOptions.UninitializedMemory,
                    nameof(SavePayloadBuffer));
            }

            public void EnsureCompressedSaveBuffer()
            {
                if (CompressedSaveBuffer.IsCreated)
                    return;

                // COLD ALLOC: NativeArray<byte>[71303168] - protected 16KB LZ4 block-compressed save payload buffer for 64MB raw save budget - owner: SaveManagerNativeBufferSet
                CompressedSaveBuffer = CreatePersistentNativeArray<byte>(
                    SaveBinaryStorage.MaxCompressedPayloadBytes,
                    NativeArrayOptions.UninitializedMemory,
                    nameof(CompressedSaveBuffer));
            }

            public void EnsureSaveStagingBuffer()
            {
                if (SaveStagingBuffer.IsCreated)
                    return;

                // COLD ALLOC: NativeArray<byte>[10485760] - 10MB async persistence snapshot staging arena - owner: SaveManagerNativeBufferSet
                SaveStagingBuffer = CreatePersistentNativeArray<byte>(
                    SaveStagingBufferBytes,
                    NativeArrayOptions.UninitializedMemory,
                    nameof(SaveStagingBuffer));
            }

            public void EnsureWfcOutpostNativeBuffers()
            {
                if (!WfcOutpostPackedWords.IsCreated)
                {
                    // COLD ALLOC: NativeArray<ulong>[32] - WFC outpost mutable-bit payload pack scratch - owner: SaveManagerNativeBufferSet
                    WfcOutpostPackedWords = CreatePersistentNativeArray<ulong>(
                        WfcOutpostPersistenceConstants.PackedWordCount,
                        NativeArrayOptions.ClearMemory,
                        nameof(WfcOutpostPackedWords));
                }

                if (!WfcOutpostRestoreWords.IsCreated)
                {
                    // COLD ALLOC: NativeArray<ulong>[32] - WFC outpost mutable-bit restore scratch - owner: SaveManagerNativeBufferSet
                    WfcOutpostRestoreWords = CreatePersistentNativeArray<ulong>(
                        WfcOutpostPersistenceConstants.PackedWordCount,
                        NativeArrayOptions.ClearMemory,
                        nameof(WfcOutpostRestoreWords));
                }

                if (!WfcOutpostPayloadBuffer.IsCreated)
                {
                    // COLD ALLOC: NativeArray<byte>[288] - WFC outpost RLE payload staging buffer - owner: SaveManagerNativeBufferSet
                    WfcOutpostPayloadBuffer = CreatePersistentNativeArray<byte>(
                        WfcOutpostPersistenceConstants.PayloadMaxBytes,
                        NativeArrayOptions.UninitializedMemory,
                        nameof(WfcOutpostPayloadBuffer));
                }

                if (!WfcOutpostGridSnapshotScratch.IsCreated)
                {
                    // COLD ALLOC: NativeArray<byte>[504] - 8-byte-padded WFC mutable-cell grid snapshot copied under DataVault lock before payload packing - owner: SaveManagerNativeBufferSet
                    WfcOutpostGridSnapshotScratch = CreatePersistentNativeArray<byte>(
                        WfcOutpostGridSnapshotScratchBytes,
                        NativeArrayOptions.UninitializedMemory,
                        nameof(WfcOutpostGridSnapshotScratch));
                }

                if (!WfcOutpostSnapshotCache.IsCreated)
                {
                    // COLD ALLOC: NativeArray<WfcOutpostSnapshotCacheEntry>[256] - WFC per-sector payload-hash dedupe cache - owner: SaveManagerNativeBufferSet
                    WfcOutpostSnapshotCache = CreatePersistentNativeArray<WfcOutpostSnapshotCacheEntry>(
                        WfcOutpostSnapshotCacheCapacity,
                        NativeArrayOptions.ClearMemory,
                        nameof(WfcOutpostSnapshotCache));
                }
            }

            public void EnsureSaveTelemetryRing()
            {
                if (SaveTelemetryRing.IsCreated)
                    return;

                // COLD ALLOC: NativeArray<AsyncPersistenceTelemetryEntry>[300] - save black box duration/size ring - owner: SaveManagerNativeBufferSet
                SaveTelemetryRing = CreatePersistentNativeArray<AsyncPersistenceTelemetryEntry>(
                    SaveTelemetryCapacity,
                    NativeArrayOptions.ClearMemory,
                    nameof(SaveTelemetryRing));
            }

            public void EnsureWfcOutpostBlackBoxRing()
            {
                if (!WfcOutpostTelemetryRing.IsCreated)
                {
                    // COLD ALLOC: NativeArray<WfcOutpostTelemetryEntry>[300] - WFC outpost frame black-box ring - owner: SaveManagerNativeBufferSet
                    WfcOutpostTelemetryRing = CreatePersistentNativeArray<WfcOutpostTelemetryEntry>(
                        WfcOutpostTelemetryCapacity,
                        NativeArrayOptions.ClearMemory,
                        nameof(WfcOutpostTelemetryRing));
                }

                if (!WfcOutpostEventTelemetryRing.IsCreated)
                {
                    // COLD ALLOC: NativeArray<WfcOutpostTelemetryEntry>[300] - WFC outpost event black-box ring - owner: SaveManagerNativeBufferSet
                    WfcOutpostEventTelemetryRing = CreatePersistentNativeArray<WfcOutpostTelemetryEntry>(
                        WfcOutpostEventTelemetryCapacity,
                        NativeArrayOptions.ClearMemory,
                        nameof(WfcOutpostEventTelemetryRing));
                }
            }

            public void EnsureLoadCandidateScratch()
            {
                if (LoadCandidateScratch.IsCreated)
                    return;

                // COLD ALLOC: NativeArray<SaveLoadCandidate>[9] - unmanaged load fallback descriptors - owner: SaveManagerNativeBufferSet
                LoadCandidateScratch = CreatePersistentNativeArray<SaveLoadCandidate>(
                    MaxSaveLoadCandidateCount,
                    NativeArrayOptions.ClearMemory,
                    nameof(LoadCandidateScratch));
            }

#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
            internal System.Action TestHook_DisposeThrow;
#endif

            public void Dispose()
            {
#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
                TestHook_DisposeThrow?.Invoke();
#endif
                Exception firstException = null;
                DisposeNativeArrayBestEffort(ref SavePayloadBuffer, ref firstException, sentinelLabel: nameof(SavePayloadBuffer));
                DisposeNativeArrayBestEffort(ref CompressedSaveBuffer, ref firstException, sentinelLabel: nameof(CompressedSaveBuffer));
                DisposeNativeArrayBestEffort(ref SaveStagingBuffer, ref firstException, sentinelLabel: nameof(SaveStagingBuffer));
                WfcOutpostGridHandle = default;
                DisposeNativeArrayBestEffort(ref WfcOutpostPackedWords, ref firstException, sentinelLabel: nameof(WfcOutpostPackedWords));
                DisposeNativeArrayBestEffort(ref WfcOutpostRestoreWords, ref firstException, sentinelLabel: nameof(WfcOutpostRestoreWords));
                DisposeNativeArrayBestEffort(ref WfcOutpostGridSnapshotScratch, ref firstException, sentinelLabel: nameof(WfcOutpostGridSnapshotScratch));
                DisposeNativeArrayBestEffort(ref WfcOutpostPayloadBuffer, ref firstException, sentinelLabel: nameof(WfcOutpostPayloadBuffer));
                DisposeNativeArrayBestEffort(ref WfcOutpostSnapshotCache, ref firstException, sentinelLabel: nameof(WfcOutpostSnapshotCache));
                DisposeNativeArrayBestEffort(ref SaveTelemetryRing, ref firstException, sentinelLabel: nameof(SaveTelemetryRing));
                DisposeNativeArrayBestEffort(ref WfcOutpostTelemetryRing, ref firstException, sentinelLabel: nameof(WfcOutpostTelemetryRing));
                DisposeNativeArrayBestEffort(ref WfcOutpostEventTelemetryRing, ref firstException, sentinelLabel: nameof(WfcOutpostEventTelemetryRing));
                DisposeNativeArrayBestEffort(ref LoadCandidateScratch, ref firstException, sentinelLabel: nameof(LoadCandidateScratch));
                ThrowFirstDisposeException(firstException);
            }
        }

        private static class StaticNativeBuffers
        {
            #if UNITY_EDITOR || UNITY_INCLUDE_TESTS
            internal static System.Action s_TestReleaseOwnedBufferUnregisterHook;
            #endif
            internal static System.Action TestHook_DisposeThrow;
            private static readonly object Sync = new object();
            public static NativeArray<SaveLoadCandidate> SaveLoadCandidateScratch;
            public static NativeArray<byte> RawWriteBuffer;
            public static NativeArray<byte> CompressedWriteBuffer;
            private static bool s_writeBuffersInUse;
            private static bool s_disposeRequested;
            private static Exception s_disposeException;

            public static void EnsureLoadCandidateScratch()
            {
                lock (Sync)
                {
                    if (SaveLoadCandidateScratch.IsCreated)
                        return;

                    // COLD ALLOC: NativeArray<SaveLoadCandidate>[9] - static repair/audit fallback descriptors - owner: SaveManager.StaticNativeBuffers
                    SaveLoadCandidateScratch = CreatePersistentNativeArray<SaveLoadCandidate>(
                        MaxSaveLoadCandidateCount,
                        NativeArrayOptions.ClearMemory,
                        nameof(SaveLoadCandidateScratch));
                }
            }

            public static void AcquireWriteBuffers(
                out NativeArray<byte> rawBuffer,
                out bool ownsRawBuffer,
                out NativeArray<byte> compressedBuffer,
                out bool ownsCompressedBuffer)
            {
                rawBuffer = default;
                compressedBuffer = default;
                ownsRawBuffer = false;
                ownsCompressedBuffer = false;

                lock (Sync)
                {
                    ThrowFirstDisposeException(s_disposeException);
                    while (s_writeBuffersInUse || s_disposeRequested)
                    {
                        System.Threading.Monitor.Wait(Sync);
                        ThrowFirstDisposeException(s_disposeException);
                    }

                    EnsureWriteBuffers();
                    s_writeBuffersInUse = true;
                    rawBuffer = RawWriteBuffer;
                    compressedBuffer = CompressedWriteBuffer;
                }
            }

            public static void ReleaseWriteBuffers(
                NativeArray<byte> rawBuffer,
                bool ownsRawBuffer,
                NativeArray<byte> compressedBuffer,
                bool ownsCompressedBuffer)
            {
                if (ownsRawBuffer)
                    ReleaseOwnedBuffer(rawBuffer);

                if (ownsCompressedBuffer)
                    ReleaseOwnedBuffer(compressedBuffer);

                if (ownsRawBuffer && ownsCompressedBuffer)
                    return;

                bool disposeRequested;
                lock (Sync)
                {
                    s_writeBuffersInUse = false;
                    disposeRequested = s_disposeRequested;
                    System.Threading.Monitor.PulseAll(Sync);
                }

                if (disposeRequested)
                    Dispose();
            }

            public static void Dispose()
            {
                TestHook_DisposeThrow?.Invoke();
                lock (SaveLoadCandidateScratchSync)
                {
                    lock (Sync)
                    {
                        s_disposeRequested = true;
                        DisposeIfRequestedAndIdle();
                    }
                }
            }

            private static void EnsureWriteBuffers()
            {
                if (!RawWriteBuffer.IsCreated)
                {
                    // COLD ALLOC: NativeArray<byte>[67108864] - isolated static save write buffer prevents live SaveManager payload aliasing - owner: SaveManager.StaticNativeBuffers
                    RawWriteBuffer = CreatePersistentNativeArray<byte>(
                        SaveBinaryStorage.RawPayloadCapacityBytes,
                        NativeArrayOptions.UninitializedMemory,
                        nameof(RawWriteBuffer));
                }

                if (!CompressedWriteBuffer.IsCreated)
                {
                    // COLD ALLOC: NativeArray<byte>[71303168] - isolated static compressed save buffer prevents live SaveManager payload aliasing - owner: SaveManager.StaticNativeBuffers
                    CompressedWriteBuffer = CreatePersistentNativeArray<byte>(
                        SaveBinaryStorage.MaxCompressedPayloadBytes,
                        NativeArrayOptions.UninitializedMemory,
                        nameof(CompressedWriteBuffer));
                }
            }

            private static void DisposeIfRequestedAndIdle()
            {
                if (!s_disposeRequested || s_writeBuffersInUse)
                    return;

                Exception firstException = null;
                DisposeNativeArrayBestEffort(ref SaveLoadCandidateScratch, ref firstException, sentinelLabel: nameof(SaveLoadCandidateScratch));
                DisposeNativeArrayBestEffort(ref RawWriteBuffer, ref firstException, sentinelLabel: nameof(RawWriteBuffer));
                DisposeNativeArrayBestEffort(ref CompressedWriteBuffer, ref firstException, sentinelLabel: nameof(CompressedWriteBuffer));
                if (firstException == null)
                {
                    s_disposeException = null;
                    s_disposeRequested = false;
                }
                else
                {
                    s_disposeException = firstException;
                }

                System.Threading.Monitor.PulseAll(Sync);
                ThrowFirstDisposeException(firstException);
            }

            private static unsafe void ReleaseOwnedBuffer(NativeArray<byte> buffer)
            {
                if (!buffer.IsCreated)
                    return;

                void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(buffer);
                System.Exception nativeSentinelCleanupException0 = null;

                try
                {
#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
                    s_TestReleaseOwnedBufferUnregisterHook?.Invoke();
#endif
                    NativeMemorySentinel.UnregisterPointer(trackedPointer);
                }
                catch (System.Exception nativeSentinelException0)
                {
                    nativeSentinelCleanupException0 = nativeSentinelException0;
                }

                try
                {
                    buffer.Dispose();
                }
                catch (System.Exception nativeSentinelException0)
                {
                    if (nativeSentinelCleanupException0 == null)
                        nativeSentinelCleanupException0 = nativeSentinelException0;
                }

                if (nativeSentinelCleanupException0 != null)
                    throw nativeSentinelCleanupException0;
            }

        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void InstallEditorNativeBufferShutdownHooks()
        {
            EditorApplication.playModeStateChanged -= HandleEditorPlayModeStateChangedForNativeBuffers;
            EditorApplication.playModeStateChanged += HandleEditorPlayModeStateChangedForNativeBuffers;
            AssemblyReloadEvents.beforeAssemblyReload -= DisposeEditorNativeBuffersForLifecycle;
            AssemblyReloadEvents.beforeAssemblyReload += DisposeEditorNativeBuffersForLifecycle;
        }

        private static void HandleEditorPlayModeStateChangedForNativeBuffers(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode ||
                state == PlayModeStateChange.EnteredEditMode)
            {
                DisposeEditorNativeBuffersForLifecycle();
            }
        }

        private static void DisposeEditorNativeBuffersForLifecycle()
        {
            Exception firstException = null;
            for (int i = s_KnownInstances.Count - 1; i >= 0; i--)
            {
                SaveManager manager = s_KnownInstances[i];
                if (manager == null)
                {
                    s_KnownInstances.RemoveAt(i);
                    continue;
                }

                try
                {
#if UNITY_EDITOR
                    Test_OnBeforeShutdownServiceState?.Invoke();
#endif
                    manager.ShutdownServiceState();
                }
                catch (Exception exception)
                {
                    if (firstException == null)
                        firstException = exception;
                }
            }

            try
            {
                StaticNativeBuffers.Dispose();
            }
            catch (Exception exception)
            {
                if (firstException == null)
                    firstException = exception;
            }

            if (firstException != null)
                Debug.LogWarning("[SaveManager] Editor lifecycle native buffer shutdown fault: " + firstException.Message);
        }
#endif

        private ref NativeArray<byte> _savePayloadBuffer => ref _nativeBuffers.SavePayloadBuffer;
        private ref NativeArray<byte> _compressedSaveBuffer => ref _nativeBuffers.CompressedSaveBuffer;
        private ref NativeArray<byte> _saveStagingBuffer => ref _nativeBuffers.SaveStagingBuffer;
        private ref VaultGenerationHandle<byte> _wfcOutpostGridHandle => ref _nativeBuffers.WfcOutpostGridHandle;
        private ref NativeArray<ulong> _wfcOutpostPackedWords => ref _nativeBuffers.WfcOutpostPackedWords;
        private ref NativeArray<ulong> _wfcOutpostRestoreWords => ref _nativeBuffers.WfcOutpostRestoreWords;
        private ref NativeArray<byte> _wfcOutpostGridSnapshotScratch => ref _nativeBuffers.WfcOutpostGridSnapshotScratch;
        private ref NativeArray<byte> _wfcOutpostPayloadBuffer => ref _nativeBuffers.WfcOutpostPayloadBuffer;
        private ref NativeArray<WfcOutpostSnapshotCacheEntry> _wfcOutpostSnapshotCache => ref _nativeBuffers.WfcOutpostSnapshotCache;
        private ref NativeArray<AsyncPersistenceTelemetryEntry> _saveTelemetryRing => ref _nativeBuffers.SaveTelemetryRing;
        private ref NativeArray<WfcOutpostTelemetryEntry> _wfcOutpostTelemetryRing => ref _nativeBuffers.WfcOutpostTelemetryRing;
        private ref NativeArray<WfcOutpostTelemetryEntry> _wfcOutpostEventTelemetryRing => ref _nativeBuffers.WfcOutpostEventTelemetryRing;
        private ulong _lastWfcOutpostSectorHash;
        private ulong _lastWfcOutpostPayloadHash;
        private ulong _wfcOutpostMutableGridSectorHash;
        private IMacroDatabaseService _macroDatabaseService;
        private IDataVault _dataVault;
        private bool _hasLastWfcOutpostSnapshot;
        private bool _wfcOutpostDependenciesReady;
        private ulong _expectedIntegrityPayloadHash64;
        private int _integrityPayloadLength;
        private bool _updatableRegistered;
        private bool _slowTickRegistered;
        private bool _frostTickRegistered;
        private bool _lateFrameRegistered;
        private bool _serviceRegistered;
        private bool _runtimeOwnerAborted;
        private bool _hotSwapRegistered;
        private bool _compressionThrottleLateFrameArmed;
        private int _compressionThrottleReleaseFrame;
        private int _slowTickSequence;
        private long _lastSaveCompressionPipelineTicks;
        private int _saveTelemetryWriteIndex;
        private int _wfcOutpostTelemetryWriteIndex;
        private int _wfcOutpostEventTelemetryWriteIndex;
        private int _wfcOutpostSnapshotCacheCount;
        private int _wfcOutpostSnapshotCacheNextIndex;
        private int _wfcOutpostSnapshotCacheRetryIndex;
        private uint _operationSequence;
        private bool _wfcOutpostBlackBoxDumped;
        private LoadingScreenController _cachedLoadingScreenController;
        private string _integritySlotName;

        private sealed class MemoryCorruptionException : Exception
        {
            public MemoryCorruptionException(string message) : base(message) { }
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 64)]
        private struct AsyncPersistenceTelemetryEntry
        {
            [System.Runtime.InteropServices.FieldOffset(0)]
            public uint Frame;
            [System.Runtime.InteropServices.FieldOffset(4)]
            public uint OperationId;
            [System.Runtime.InteropServices.FieldOffset(8)]
            public uint SaveDurationMs;
            [System.Runtime.InteropServices.FieldOffset(12)]
            public uint CompressedSizeBytes;
            [System.Runtime.InteropServices.FieldOffset(16)]
            public uint RawPayloadBytes;
            [System.Runtime.InteropServices.FieldOffset(20)]
            public uint Flags;
            [System.Runtime.InteropServices.FieldOffset(24)]
            public uint SlotHash;
            [System.Runtime.InteropServices.FieldOffset(28)]
            public uint Reserved;
            [System.Runtime.InteropServices.FieldOffset(32)]
            private byte _pad0;
            [System.Runtime.InteropServices.FieldOffset(33)]
            private byte _pad1;
            [System.Runtime.InteropServices.FieldOffset(34)]
            private byte _pad2;
            [System.Runtime.InteropServices.FieldOffset(35)]
            private byte _pad3;
            [System.Runtime.InteropServices.FieldOffset(36)]
            private byte _pad4;
            [System.Runtime.InteropServices.FieldOffset(37)]
            private byte _pad5;
            [System.Runtime.InteropServices.FieldOffset(38)]
            private byte _pad6;
            [System.Runtime.InteropServices.FieldOffset(39)]
            private byte _pad7;
            [System.Runtime.InteropServices.FieldOffset(40)]
            private byte _pad8;
            [System.Runtime.InteropServices.FieldOffset(41)]
            private byte _pad9;
            [System.Runtime.InteropServices.FieldOffset(42)]
            private byte _pad10;
            [System.Runtime.InteropServices.FieldOffset(43)]
            private byte _pad11;
            [System.Runtime.InteropServices.FieldOffset(44)]
            private byte _pad12;
            [System.Runtime.InteropServices.FieldOffset(45)]
            private byte _pad13;
            [System.Runtime.InteropServices.FieldOffset(46)]
            private byte _pad14;
            [System.Runtime.InteropServices.FieldOffset(47)]
            private byte _pad15;
            [System.Runtime.InteropServices.FieldOffset(48)]
            private byte _pad16;
            [System.Runtime.InteropServices.FieldOffset(49)]
            private byte _pad17;
            [System.Runtime.InteropServices.FieldOffset(50)]
            private byte _pad18;
            [System.Runtime.InteropServices.FieldOffset(51)]
            private byte _pad19;
            [System.Runtime.InteropServices.FieldOffset(52)]
            private byte _pad20;
            [System.Runtime.InteropServices.FieldOffset(53)]
            private byte _pad21;
            [System.Runtime.InteropServices.FieldOffset(54)]
            private byte _pad22;
            [System.Runtime.InteropServices.FieldOffset(55)]
            private byte _pad23;
            [System.Runtime.InteropServices.FieldOffset(56)]
            private byte _pad24;
            [System.Runtime.InteropServices.FieldOffset(57)]
            private byte _pad25;
            [System.Runtime.InteropServices.FieldOffset(58)]
            private byte _pad26;
            [System.Runtime.InteropServices.FieldOffset(59)]
            private byte _pad27;
            [System.Runtime.InteropServices.FieldOffset(60)]
            private byte _pad28;
            [System.Runtime.InteropServices.FieldOffset(61)]
            private byte _pad29;
            [System.Runtime.InteropServices.FieldOffset(62)]
            private byte _pad30;
            [System.Runtime.InteropServices.FieldOffset(63)]
            private byte _pad31;
        }

        [StructLayout(LayoutKind.Explicit, Size = 24)]
        private struct WfcOutpostSnapshotCacheEntry
        {
            [FieldOffset(0)]
            public ulong SectorHash;
            [FieldOffset(8)]
            public ulong PayloadHash;
            [FieldOffset(16)]
            public uint Flags;
            [FieldOffset(20)]
            public uint LastAppendFrame;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct WfcOutpostTelemetryEntry
        {
            [FieldOffset(0)]
            public uint Frame;
            [FieldOffset(4)]
            public uint Operation;
            [FieldOffset(8)]
            public uint Status;
            [FieldOffset(12)]
            public uint Flags;
            [FieldOffset(16)]
            public ulong SectorHash;
            [FieldOffset(24)]
            public ulong PayloadHash;
            [FieldOffset(32)]
            public ulong GridSectorHash;
            [FieldOffset(40)]
            public uint PayloadBytes;
            [FieldOffset(44)]
            public uint CellIndex;
            [FieldOffset(48)]
            public uint PreviousFlags;
            [FieldOffset(52)]
            public uint CurrentFlags;
            [FieldOffset(56)]
            public uint SignalSourceHash;
            [FieldOffset(60)]
            public uint Reserved0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct SaveStagingHeader
        {
            [FieldOffset(0)]
            public uint OperationId;
            [FieldOffset(4)]
            public uint SlotHash;
            [FieldOffset(8)]
            public uint SaveableCount;
            [FieldOffset(12)]
            public uint PersistentWorldRecordCount;
            [FieldOffset(16)]
            public uint EcosystemRecordCount;
            [FieldOffset(20)]
            public uint QuestWordCount;
            [FieldOffset(24)]
            public uint VoxelDeltaBytes;
            [FieldOffset(28)]
            public uint Frame;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private readonly struct SaveLoadCandidate
        {
            private const int BackupFlag = 1 << 0;

            [FieldOffset(0)]
            public readonly int Flags;
            [FieldOffset(4)]
            public readonly int BackupGeneration;
            [FieldOffset(8)]
            public readonly int Reserved0;
            [FieldOffset(12)]
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
            if (IsSaveRuntimeUsable(manager))
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
            if (IsSaveRuntimeUsable(manager))
            {
                return math.clamp(
                    math.max(manager.manualBackupGenerations, math.max(manager.autoBackupGenerations, manager.quickBackupGenerations)),
                    1,
                    MaxBackupGenerations);
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
            if (!s_KnownInstances.Contains(this))
                s_KnownInstances.Add(this);

            if (TryDeactivateDuplicateRuntimeOwner())
                return;

            _sessionStartTime = Time.realtimeSinceStartupAsDouble;
            CachePersistentDataPathRoot();
            InitializeNativeBuffers();
            SaveBinaryStorage.WarmRuntime();
            EnsureWorldPagerCold();
            EnsureWorldPagerInitialized();
        }

        private bool TryDeactivateDuplicateRuntimeOwner()
        {
            if (!Application.isPlaying)
                return false;

            if (TryAbortForUsableExistingRuntime())
                return true;

            return false;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            ISaveService registeredService = GlobalRegistry.Save;
            SaveManager registeredRuntime = GlobalRegistry.SaveRuntime;
            if (ReferenceEquals(registeredService, this) || ReferenceEquals(registeredRuntime, this))
                return false;

            if (IsSaveRuntimeUsable(registeredService) || IsSaveRuntimeUsable(registeredRuntime))
            {
                AbortDuplicateRuntimeOwner();
                return true;
            }

            if (registeredService != null)
                GlobalRegistry.UnregisterSaveService(registeredService);
            if (!ReferenceEquals(registeredRuntime, null))
                GlobalRegistry.UnregisterSaveService(registeredRuntime);

            return false;
        }

        private void AbortDuplicateRuntimeOwner()
        {
            if (_runtimeOwnerAborted)
                return;

            TryUnregisterHotSwapListener();
            UnregisterDispatcherLanes();
            if (_serviceRegistered && ReferenceEquals(GlobalRegistry.Save, this))
                GlobalRegistry.UnregisterSaveService(this);

            _runtimeOwnerAborted = true;
            _serviceRegistered = false;
            _isBusy = false;
            _updatableRegistered = false;
            _slowTickRegistered = false;
            _frostTickRegistered = false;
            _lateFrameRegistered = false;
            _hotSwapRegistered = false;
            _compressionThrottleLateFrameArmed = false;
            _compressionThrottleReleaseFrame = 0;
            ClearPendingOwnerHydration();
            try
            {
                _worldPager?.Dispose();
            }
            catch
            {
            }
            finally
            {
                _worldPager = null;
            }

            try
            {
                _nativeBuffers.Dispose();
            }
            catch
            {
            }

            _macroDatabaseService = null;
            _dataVault = null;
            enabled = false;
            Destroy(gameObject);
        }

        private static bool IsSaveRuntimeUsable(ISaveService service)
        {
            if (service == null)
                return false;

            SaveManager manager = service as SaveManager;
            if (!ReferenceEquals(manager, null))
            {
                return manager != null &&
                       manager._serviceRegistered &&
                       manager.isActiveAndEnabled &&
                       !manager._runtimeOwnerAborted;
            }

            if (service is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled && service.IsInitialized;

            return service.IsInitialized;
        }

        private void OnEnable()
        {
            if (_runtimeOwnerAborted || !_serviceRegistered || !Application.isPlaying)
                return;

            TryRegisterHotSwapListener();
            if (GlobalRegistry.Dispatcher == null)
                return;

            TryRegisterDispatcherLanes();
        }

        private void OnDisable()
        {
            if (_runtimeOwnerAborted)
                return;

            TryUnregisterHotSwapListener();
            UnregisterDispatcherLanes();
        }

        private void OnDestroy()
        {
            s_KnownInstances.Remove(this);

            if (_runtimeOwnerAborted)
                return;

            ShutdownServiceState();
        }

        private void OnApplicationQuit()
        {
            if (_runtimeOwnerAborted)
                return;

            FlushWorldPager();
            ShutdownServiceState();
        }

        public void OnServiceShutdown()
        {
            if (_runtimeOwnerAborted)
                return;

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

            if (_frostTickRegistered)
            {
                GlobalRegistry.UnregisterFrostTickable(this, PriorityLayer.Core);
                _frostTickRegistered = false;
            }

            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);
                _lateFrameRegistered = false;
            }
        }

        private void ShutdownServiceState()
        {
            if (_runtimeOwnerAborted)
                return;

            TryUnregisterHotSwapListener();
            UnregisterDispatcherLanes();

            if (_serviceRegistered && ReferenceEquals(GlobalRegistry.Save, this))
                GlobalRegistry.UnregisterSaveService(this);

            _serviceRegistered = false;
            _isBusy = false;
            _compressionThrottleLateFrameArmed = false;
            _compressionThrottleReleaseFrame = 0;
            _slowTickSequence = 0;
            _frostTickRegistered = false;
            _lastSaveCompressionPipelineTicks = 0L;
            _cachedLoadingScreenController = null;
            if (_saveableCount > 0)
                Array.Clear(_saveables, 0, _saveableCount);
            _saveableCount = 0;
            _debugRegisteredCount = 0;
            _registryDirty = false;
            _saveableCapacityWarningLogged = false;
            _lastLoadPriorityConflictCount = 0;
            _lastLoadPriorityConflictFrame = 0;
            ClearPendingOwnerHydration();
            ClearSaveNotificationDiagnostics();

            Exception firstDisposeException = null;
            if (_worldPager != null)
            {
                try
                {
                    _worldPager.Dispose();
                }
                catch (Exception exception)
                {
                    if (firstDisposeException == null)
                        firstDisposeException = exception;
                }
                finally
                {
                    _worldPager = null;
                }
            }

            try
            {
                _nativeBuffers.Dispose();
            }
            catch (Exception exception)
            {
                if (firstDisposeException == null)
                    firstDisposeException = exception;
            }

            try
            {
                StaticNativeBuffers.Dispose();
            }
            catch (Exception exception)
            {
                if (firstDisposeException == null)
                    firstDisposeException = exception;
            }

            _macroDatabaseService = null;
            _dataVault = null;
            _lastWfcOutpostSectorHash = 0UL;
            _lastWfcOutpostPayloadHash = 0UL;
            _wfcOutpostMutableGridSectorHash = 0UL;
            _hasLastWfcOutpostSnapshot = false;
            _wfcOutpostDependenciesReady = false;
            _wfcOutpostTelemetryWriteIndex = 0;
            _wfcOutpostEventTelemetryWriteIndex = 0;
            _wfcOutpostSnapshotCacheCount = 0;
            _wfcOutpostSnapshotCacheNextIndex = 0;
            _wfcOutpostSnapshotCacheRetryIndex = 0;
            _wfcOutpostBlackBoxDumped = false;

            DisposeIntegrityResources();
            ThrowFirstDisposeException(firstDisposeException);
        }

        public void InitializeService()
        {
            if (_runtimeOwnerAborted || TryDeactivateDuplicateRuntimeOwner())
                return;

            CachePersistentDataPathRoot();
            InitializeNativeBuffers();
            EnsureWorldPagerCold();
            EnsureWorldPagerInitialized();
            RefreshWfcOutpostDependencies();

            if (_serviceRegistered)
            {
                if (isActiveAndEnabled && Application.isPlaying && GlobalRegistry.Dispatcher != null)
                    TryRegisterDispatcherLanes();

                TryRegisterHotSwapListener();
                return;
            }

            if (TryAbortForUsableExistingRuntime())
                return;

            GlobalRegistry.RegisterAsyncPersistenceService(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.SaveRuntime, this);
            if (!_serviceRegistered)
            {
                AbortDuplicateRuntimeOwner();
                return;
            }

            TryRegisterHotSwapListener();
            if (isActiveAndEnabled && Application.isPlaying && GlobalRegistry.Dispatcher != null)
                TryRegisterDispatcherLanes();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (_runtimeOwnerAborted)
                return;

            if (serviceSlot == GlobalRegistryServiceSlot.Save)
            {
                bool stillOwner = ReferenceEquals(currentService, this);
                _serviceRegistered = stillOwner;
                if (!stillOwner)
                {
                    UnregisterDispatcherLanes();
                    TryUnregisterHotSwapListener();
                }

                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher &&
                currentService != null &&
                _serviceRegistered &&
                isActiveAndEnabled)
            {
                UnregisterDispatcherLanes();
                TryRegisterDispatcherLanes();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                RefreshWfcOutpostDependencies(_macroDatabaseService, currentService as IDataVault);
                EnsureWorldPagerInitialized();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.LoadingScreenRuntime)
            {
                CacheLoadingScreenController(currentService as LoadingScreenController);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.MacroDatabase)
            {
                RefreshWfcOutpostDependencies(currentService as IMacroDatabaseService, _dataVault);
            }
        }

        private void TryRegisterDispatcherLanes()
        {
            if (_runtimeOwnerAborted || !_serviceRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_updatableRegistered)
            {
                _updatableRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core);
            }

            if (!_slowTickRegistered)
            {
                _slowTickRegistered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Core);
            }

            if (!_frostTickRegistered)
            {
                _frostTickRegistered = GlobalRegistry.TryRegisterFrostTickable(this, PriorityLayer.Core);
            }

            if (!_lateFrameRegistered)
            {
                _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core);
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_runtimeOwnerAborted || _hotSwapRegistered || !_serviceRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        public unsafe bool TryPersistWfcOutpostStateSnapshot(
            ulong sectorHash,
            NativeArray<byte> wfcGrid,
            uint frame,
            out WfcOutpostPersistenceStatus status)
        {
            if (_runtimeOwnerAborted || !_serviceRegistered)
            {
                status = WfcOutpostPersistenceStatus.ServiceUnavailable;
                return false;
            }

            EnsureWfcOutpostBlackBoxRing();
            EnsureWfcOutpostNativeBuffers();
            RefreshWfcOutpostDependenciesForExternalRequest();
            return TryPersistWfcOutpostStateSnapshotInternal(sectorHash, wfcGrid, frame, out status);
        }

        private unsafe bool TryPersistWfcOutpostStateSnapshotInternal(
            ulong sectorHash,
            NativeArray<byte> wfcGrid,
            uint frame,
            out WfcOutpostPersistenceStatus status)
        {
            if (!TryStageWfcOutpostStateSnapshotPayload(
                    sectorHash,
                    wfcGrid,
                    frame,
                    out status,
                    out ulong packedHash,
                    out int payloadBytes,
                    out bool needsCommit,
                    out bool publishWriteFailure))
            {
                if (publishWriteFailure)
                    PublishWfcWriteFailureWarning();
                return false;
            }

            if (!needsCommit)
                return true;

            return TryCommitWfcOutpostStateSnapshotPayload(
                sectorHash,
                frame,
                packedHash,
                payloadBytes,
                out status);
        }

        private unsafe bool TryStageWfcOutpostStateSnapshotPayload(
            ulong sectorHash,
            NativeArray<byte> wfcGrid,
            uint frame,
            out WfcOutpostPersistenceStatus status,
            out ulong packedHash,
            out int payloadBytes,
            out bool needsCommit,
            out bool publishWriteFailure)
        {
            status = WfcOutpostPersistenceStatus.None;
            packedHash = 0UL;
            payloadBytes = 0;
            needsCommit = false;
            publishWriteFailure = false;
            if (sectorHash == 0UL)
            {
                status = WfcOutpostPersistenceStatus.Rejected;
                RecordWfcOutpostEventBlackBox(WfcOutpostBlackBoxOperationPersist, status, sectorHash);
                return false;
            }

            if (!IsValidWfcOutpostGrid(wfcGrid))
            {
                status = WfcOutpostPersistenceStatus.InvalidGrid;
                RecordWfcOutpostEventBlackBox(WfcOutpostBlackBoxOperationPersist, status, sectorHash);
                return false;
            }

            if (!HasWfcOutpostNativeBuffers())
            {
                status = WfcOutpostPersistenceStatus.ServiceUnavailable;
                RecordWfcOutpostEventBlackBox(WfcOutpostBlackBoxOperationPersist, status, sectorHash, packedHash, frame: frame);
                publishWriteFailure = true;
                return false;
            }

            PackWfcOutpostMutableStateGrid(wfcGrid, _wfcOutpostPackedWords);

            packedHash = ComputeWfcOutpostPackedHash(_wfcOutpostPackedWords);
            if (TryGetCachedWfcOutpostSnapshotHash(sectorHash, out ulong cachedPayloadHash) &&
                cachedPayloadHash == packedHash)
            {
                status = WfcOutpostPersistenceStatus.DirtySkippedUnchanged;
                RecordWfcOutpostEventBlackBox(WfcOutpostBlackBoxOperationPersist, status, sectorHash, packedHash, frame: frame);
                return true;
            }

            if (!SaveBinaryPayloadCodec.TryWriteWfcOutpostBitmaskPayload(
                    _wfcOutpostPackedWords,
                    WfcOutpostPersistenceConstants.PackedWordCount,
                    (byte*)_wfcOutpostPayloadBuffer.GetUnsafePtr(),
                    _wfcOutpostPayloadBuffer.Length,
                    out payloadBytes))
            {
                status = WfcOutpostPersistenceStatus.Rejected;
                RecordWfcOutpostEventBlackBox(WfcOutpostBlackBoxOperationPersist, status, sectorHash, packedHash, frame: frame);
                publishWriteFailure = true;
                return false;
            }

            needsCommit = true;
            return true;
        }

        private bool TryCopyWfcOutpostGridToSnapshotScratch(NativeArray<byte> wfcGrid)
        {
            if (!IsValidWfcOutpostGrid(wfcGrid) ||
                !IsValidWfcOutpostGrid(_wfcOutpostGridSnapshotScratch))
            {
                return false;
            }

            NativeArray<byte>.Copy(
                wfcGrid,
                _wfcOutpostGridSnapshotScratch,
                WfcOutpostPersistenceConstants.CellCount);
            return true;
        }

        private bool TryCommitWfcOutpostStateSnapshotPayload(
            ulong sectorHash,
            uint frame,
            ulong packedHash,
            int payloadBytes,
            out WfcOutpostPersistenceStatus status)
        {
            status = WfcOutpostPersistenceStatus.None;
            if (!TryResolveWfcOutpostMacroDatabase(out IMacroDatabaseService macroDatabase))
            {
                status = WfcOutpostPersistenceStatus.ServiceUnavailable;
                RecordWfcOutpostEventBlackBox(WfcOutpostBlackBoxOperationPersist, status, sectorHash);
                return false;
            }

            if (!macroDatabase.MarkDirty(
                    sectorHash,
                    _wfcOutpostPayloadBuffer,
                    payloadBytes,
                    0))
            {
                status = WfcOutpostPersistenceStatus.Rejected;
                RecordWfcOutpostEventBlackBox(WfcOutpostBlackBoxOperationPersist, status, sectorHash, packedHash, payloadBytes, frame: frame);
                PublishWfcWriteFailureWarning();
                return false;
            }

            RememberWfcOutpostSnapshotHash(
                sectorHash,
                packedHash,
                WfcOutpostSnapshotCacheFlagAppendPending,
                frame);
            status = WfcOutpostPersistenceStatus.DirtyQueued;
            RecordWfcOutpostEventBlackBox(WfcOutpostBlackBoxOperationPersist, status, sectorHash, packedHash, payloadBytes, frame: frame);
            PublishWfcBytesSaved(payloadBytes);
            QueueWfcOutpostDirtyAppend(sectorHash, packedHash, frame);
            return true;
        }

        public unsafe bool TryApplyWfcOutpostStateOverride(
            ulong sectorHash,
            NativeArray<byte> wfcGrid,
            out WfcOutpostPersistenceStatus status)
        {
            if (_runtimeOwnerAborted || !_serviceRegistered)
            {
                status = WfcOutpostPersistenceStatus.ServiceUnavailable;
                return false;
            }

            EnsureWfcOutpostBlackBoxRing();
            EnsureWfcOutpostNativeBuffers();
            RefreshWfcOutpostDependenciesForExternalRequest();
            status = WfcOutpostPersistenceStatus.None;
            if (sectorHash == 0UL)
            {
                status = WfcOutpostPersistenceStatus.Rejected;
                RecordWfcOutpostEventBlackBox(WfcOutpostBlackBoxOperationRestore, status, sectorHash);
                return false;
            }

            if (!IsValidWfcOutpostGrid(wfcGrid))
            {
                status = WfcOutpostPersistenceStatus.InvalidGrid;
                RecordWfcOutpostEventBlackBox(WfcOutpostBlackBoxOperationRestore, status, sectorHash);
                return false;
            }

            if (!TryResolveWfcOutpostMacroDatabase(out IMacroDatabaseService macroDatabase))
            {
                status = WfcOutpostPersistenceStatus.ServiceUnavailable;
                RecordWfcOutpostEventBlackBox(WfcOutpostBlackBoxOperationRestore, status, sectorHash);
                return false;
            }

            if (!HasWfcOutpostNativeBuffers())
            {
                status = WfcOutpostPersistenceStatus.ServiceUnavailable;
                RecordWfcOutpostEventBlackBox(WfcOutpostBlackBoxOperationRestore, status, sectorHash);
                return false;
            }

            if (!macroDatabase.TryCopyPayload(
                    sectorHash,
                    0,
                    _wfcOutpostPayloadBuffer,
                    _wfcOutpostPayloadBuffer.Length,
                    out int payloadBytes,
                    out MacroDatabasePayloadHandle handle) ||
                payloadBytes != handle.ByteLength)
            {
                status = WfcOutpostPersistenceStatus.Missing;
                RecordWfcOutpostEventBlackBox(WfcOutpostBlackBoxOperationRestore, status, sectorHash);
                return false;
            }

            byte* payloadPointer = (byte*)_wfcOutpostPayloadBuffer.GetUnsafeReadOnlyPtr();
            if (handle.ByteLength < WfcOutpostPersistenceConstants.PayloadHeaderBytes ||
                handle.ByteLength > WfcOutpostPersistenceConstants.PayloadMaxBytes ||
                !SaveBinaryPayloadCodec.TryReadWfcOutpostBitmaskPayload(
                    payloadPointer,
                    handle.ByteLength,
                    _wfcOutpostRestoreWords,
                    WfcOutpostPersistenceConstants.PackedWordCount,
                    out int wordsRead) ||
                wordsRead != WfcOutpostPersistenceConstants.PackedWordCount)
            {
                status = WfcOutpostPersistenceStatus.CorruptLength;
                RecordWfcOutpostEventBlackBox(WfcOutpostBlackBoxOperationRestore, status, sectorHash, payloadBytes: handle.ByteLength);
                PublishWfcCorruptPayloadWarning();
                return false;
            }

            UnpackWfcOutpostMutableStateGrid(_wfcOutpostRestoreWords, wfcGrid);
            ulong restoredPayloadHash = ComputeWfcOutpostPackedHash(_wfcOutpostRestoreWords);
            uint cacheFlags = ResolveWfcOutpostSnapshotCacheFlags(in handle);
            RememberWfcOutpostSnapshotHash(
                sectorHash,
                restoredPayloadHash,
                cacheFlags,
                ResolveWfcOutpostSnapshotCacheFrame(cacheFlags));
            status = WfcOutpostPersistenceStatus.Ready;
            RecordWfcOutpostEventBlackBox(WfcOutpostBlackBoxOperationRestore, status, sectorHash, restoredPayloadHash, handle.ByteLength);
            return true;
        }

        public bool TryEnqueueChunkPageWrite(
            long sectorHash,
            uint payloadType,
            NativeArray<byte> payload,
            int byteCount,
            uint sourceHash,
            uint frame)
        {
            if (_runtimeOwnerAborted || !_serviceRegistered)
                return false;

            H8BinaryWorldPager pager = EnsureWorldPager();
            bool queued = pager != null && pager.TryEnqueueWrite(sectorHash, payloadType, payload, byteCount, sourceHash, frame);
            if (queued)
                TryPublishWorldPagerSavingNotification();

            return queued;
        }

        public bool TryRequestChunkPageRead(
            long sectorHash,
            uint payloadType,
            uint requestId,
            out H8WorldPageReadTicket ticket)
        {
            uint frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            ticket = new H8WorldPageReadTicket
            {
                SectorHash = sectorHash,
                PayloadType = payloadType,
                RequestId = requestId,
                Frame = frame,
                Status = H8WorldPageStatus.Rejected
            };

            if (_runtimeOwnerAborted || !_serviceRegistered)
                return false;

            H8BinaryWorldPager pager = EnsureWorldPager();
            return pager != null && pager.TryRequestRead(sectorHash, payloadType, requestId, frame, out ticket);
        }

        public bool TryCopyCompletedChunkPage(
            in H8WorldPageReadTicket ticket,
            NativeArray<byte> destination,
            out int bytesWritten,
            out H8WorldPageStatus status)
        {
            if (_runtimeOwnerAborted || !_serviceRegistered)
            {
                bytesWritten = 0;
                status = H8WorldPageStatus.Rejected;
                return false;
            }

            H8BinaryWorldPager pager = _worldPager;
            if (pager == null)
            {
                bytesWritten = 0;
                status = H8WorldPageStatus.Rejected;
                return false;
            }

            return pager.TryCopyCompletedPage(in ticket, destination, out bytesWritten, out status);
        }

        public bool TryRetireCompletedChunkPage(
            in H8WorldPageReadTicket ticket,
            out H8WorldPageStatus status,
            out int byteCount)
        {
            if (_runtimeOwnerAborted || !_serviceRegistered)
            {
                status = H8WorldPageStatus.Rejected;
                byteCount = 0;
                return false;
            }

            H8BinaryWorldPager pager = _worldPager;
            if (pager == null)
            {
                status = H8WorldPageStatus.Rejected;
                byteCount = 0;
                return false;
            }

            return pager.TryRetireCompletedPage(in ticket, out status, out byteCount);
        }

        public H8WorldPagerTelemetrySnapshot GetWorldPagerTelemetry()
        {
            return !_runtimeOwnerAborted && _serviceRegistered && _worldPager != null ? _worldPager.GetTelemetry() : default;
        }

        public void FlushWorldPager()
        {
            if (_runtimeOwnerAborted || !_serviceRegistered)
                return;

            _worldPager?.Flush();
        }

        public bool TryRequestMacroDatabaseCompaction(MacroDatabaseTier tier, byte reasonFlags = 0)
        {
            if (_runtimeOwnerAborted || !_serviceRegistered)
                return false;

            if (_isBusy)
            {
                BlockMacroDatabaseCompactionForActivePersistence();
                return false;
            }

            return TryResolveWfcOutpostMacroDatabase(out IMacroDatabaseService macroDatabase) &&
                   macroDatabase.TryRequestBackgroundCompaction(tier, reasonFlags);
        }

        public bool TryCompleteMacroDatabaseCompaction(MacroDatabaseTier tier)
        {
            if (_runtimeOwnerAborted || !_serviceRegistered)
                return false;

            if (_isBusy)
            {
                BlockMacroDatabaseCompactionForActivePersistence();
                return false;
            }

            return TryResolveWfcOutpostMacroDatabase(out IMacroDatabaseService macroDatabase) &&
                   macroDatabase.TryCompleteCompactionSwap(tier, false);
        }

        public MacroDatabaseCompactionSnapshot GetMacroDatabaseCompactionSnapshot()
        {
            return !_runtimeOwnerAborted &&
                   _serviceRegistered &&
                   TryResolveWfcOutpostMacroDatabase(out IMacroDatabaseService macroDatabase)
                ? macroDatabase.Compaction
                : default;
        }

        private static MacroDatabaseTier ResolveMacroDatabaseCompactionTier()
        {
            return MacroDatabaseTier.Middle;
        }

        private void NotifyMacroDatabasePersistenceGate(bool blocked)
        {
            if (_runtimeOwnerAborted || !_serviceRegistered)
                return;

            if (TryResolveWfcOutpostMacroDatabase(out IMacroDatabaseService macroDatabase))
                macroDatabase.NotifyPersistenceGate(blocked, Hecton8.Core.SystemDispatcher.CurrentFrameId);
        }

        private H8BinaryWorldPager EnsureWorldPager()
        {
            if (_runtimeOwnerAborted || !_serviceRegistered)
                return null;

            EnsureWorldPagerCold();
            EnsureWorldPagerInitialized();
            return _worldPager;
        }

        private void InitializeNativeBuffers()
        {
            if (_runtimeOwnerAborted)
                return;

            _nativeBuffers.EnsureInitial();
        }

        private void EnsureSaveWorkingBuffers()
        {
            _nativeBuffers.EnsureSaveWorkingBuffers();
        }

        private void EnsureSavePayloadBuffer()
        {
            _nativeBuffers.EnsureSavePayloadBuffer();
        }

        private void EnsureCompressedSaveBuffer()
        {
            _nativeBuffers.EnsureCompressedSaveBuffer();
        }

        private void EnsureSaveStagingBuffer()
        {
            _nativeBuffers.EnsureSaveStagingBuffer();
        }

        private void RefreshWfcOutpostDependencies()
        {
            RefreshWfcOutpostDependencies(GlobalRegistry.MacroDatabase, GlobalRegistry.DataVault);
        }

        private void RefreshWfcOutpostDependenciesForExternalRequest()
        {
            if (_macroDatabaseService == null || _dataVault == null)
            {
                RefreshWfcOutpostDependencies();
                return;
            }

            RefreshWfcOutpostDependencyReadiness();
        }

        private void RefreshWfcOutpostDependencies(IMacroDatabaseService macroDatabase, IDataVault dataVault)
        {
            bool macroDatabaseChanged = !ReferenceEquals(_macroDatabaseService, macroDatabase);
            bool dataVaultChanged = !ReferenceEquals(_dataVault, dataVault);
            if (macroDatabaseChanged || dataVaultChanged)
            {
                ResetWfcOutpostSectorCaches(clearMutableGrid: !dataVaultChanged && dataVault != null);
            }

            _macroDatabaseService = macroDatabase;
            _dataVault = dataVault;
            _wfcOutpostDependenciesReady = _macroDatabaseService != null &&
                                           _macroDatabaseService.IsOpen &&
                                           _dataVault != null;
            if (!_wfcOutpostDependenciesReady)
                ResetWfcOutpostSectorCaches(clearMutableGrid: !dataVaultChanged && _dataVault != null);

            TryEnsureWfcOutpostGridHandle(out _);
        }

        private void RefreshWfcOutpostDependencyReadiness()
        {
            IDataVault dataVault = _dataVault;
            _wfcOutpostDependenciesReady = _macroDatabaseService != null &&
                                           _macroDatabaseService.IsOpen &&
                                           dataVault != null &&
                                           !dataVault.IsCompactionFenceActive &&
                                           IsWfcOutpostGridHandleCreated(in _wfcOutpostGridHandle);
        }

        private bool TryResolveWfcOutpostMacroDatabase(out IMacroDatabaseService macroDatabase)
        {
            macroDatabase = _macroDatabaseService;
            return macroDatabase != null && macroDatabase.IsOpen;
        }

        private bool TryEnsureWfcOutpostGridHandle(out VaultGenerationHandle<byte> wfcGridHandle)
        {
            wfcGridHandle = default;
            IDataVault dataVault = _dataVault;
            if (dataVault == null || dataVault.IsCompactionFenceActive)
            {
                _wfcOutpostGridHandle = default;
                _wfcOutpostDependenciesReady = false;
                return false;
            }

            wfcGridHandle = dataVault.EnsureGenerationHandle<byte>(
                BufferID.WfcOutpostGrid,
                WfcOutpostPersistenceConstants.CellCount,
                SystemID.CoreDataVault,
                NativeArrayOptions.ClearMemory);
            if (!IsWfcOutpostGridHandleCreated(in wfcGridHandle) ||
                dataVault.IsCompactionFenceActive ||
                !dataVault.TryReadOnlyHandle(in wfcGridHandle, out NativeArray<byte>.ReadOnly wfcGrid))
            {
                _wfcOutpostGridHandle = default;
                _wfcOutpostDependenciesReady = false;
                return false;
            }

            if (dataVault.IsCompactionFenceActive || !IsValidWfcOutpostGrid(wfcGrid))
            {
                _wfcOutpostGridHandle = default;
                _wfcOutpostDependenciesReady = false;
                return false;
            }

            _wfcOutpostGridHandle = wfcGridHandle;
            _wfcOutpostDependenciesReady = TryResolveWfcOutpostMacroDatabase(out _) && _dataVault != null;
            return true;
        }

        private bool TryAcquireWfcOutpostGridWrite(
            out NativeArray<byte> wfcGrid,
            out VaultGenerationHandle<byte> acquiredHandle,
            out IDataVault acquiredVault)
        {
            wfcGrid = default;
            acquiredHandle = default;
            acquiredVault = null;
            if (!TryEnsureWfcOutpostGridHandle(out VaultGenerationHandle<byte> wfcGridHandle))
                return false;

            IDataVault dataVault = _dataVault;
            if (dataVault == null ||
                dataVault.IsCompactionFenceActive ||
                !IsWfcOutpostGridHandleCreated(in wfcGridHandle) ||
                !dataVault.TryAcquireWriteLock(in wfcGridHandle, SystemID.CoreDataVault, out wfcGrid))
            {
                return false;
            }

            bool ownershipTransferred = false;
            try
            {
                if (!dataVault.IsCompactionFenceActive && IsValidWfcOutpostGrid(wfcGrid))
                {
                    acquiredHandle = wfcGridHandle;
                    acquiredVault = dataVault;
                    ownershipTransferred = true;
                    return true;
                }

                return false;
            }
            finally
            {
                if (!ownershipTransferred)
                {
                    dataVault.ReleaseWriteLock(in wfcGridHandle, SystemID.CoreDataVault);
                    wfcGrid = default;
                }
            }
        }

        private static bool IsWfcOutpostGridHandleCreated(in VaultGenerationHandle<byte> handle)
        {
            return handle.BufferID == unchecked((uint)(int)BufferID.WfcOutpostGrid) &&
                   handle.SystemID == (uint)SystemID.CoreDataVault &&
                   handle.Generation != 0u;
        }

        private void EnsureWfcOutpostNativeBuffers()
        {
            _nativeBuffers.EnsureWfcOutpostNativeBuffers();
        }

        private bool HasWfcOutpostNativeBuffers()
        {
            return _wfcOutpostPackedWords.IsCreated &&
                   _wfcOutpostPackedWords.Length >= WfcOutpostPersistenceConstants.PackedWordCount &&
                   _wfcOutpostRestoreWords.IsCreated &&
                   _wfcOutpostRestoreWords.Length >= WfcOutpostPersistenceConstants.PackedWordCount &&
                   _wfcOutpostGridSnapshotScratch.IsCreated &&
                   _wfcOutpostGridSnapshotScratch.Length >= WfcOutpostPersistenceConstants.CellCount &&
                   _wfcOutpostPayloadBuffer.IsCreated &&
                   _wfcOutpostPayloadBuffer.Length >= WfcOutpostPersistenceConstants.PayloadMaxBytes &&
                   _wfcOutpostSnapshotCache.IsCreated &&
                   _wfcOutpostSnapshotCache.Length >= WfcOutpostSnapshotCacheCapacity;
        }

        private void EnsureSaveTelemetryRing()
        {
            _nativeBuffers.EnsureSaveTelemetryRing();
        }

        private void EnsureWfcOutpostBlackBoxRing()
        {
            _nativeBuffers.EnsureWfcOutpostBlackBoxRing();
        }

        private void EnsureLoadCandidateScratch()
        {
            _nativeBuffers.EnsureLoadCandidateScratch();
        }

        private void EnsureWorldPagerCold()
        {
            if (_worldPager == null)
                _worldPager = new H8BinaryWorldPager(); // COLD ALLOC: H8BinaryWorldPager[1] - async chunk page persistence bridge warmed before Tick - owner: SaveManager
        }

        private bool EnsureWorldPagerInitialized()
        {
            H8BinaryWorldPager pager = _worldPager;
            if (pager == null)
                return false;

            IDataVault dataVault = _dataVault;
            if (dataVault == null || dataVault.IsCompactionFenceActive || dataVault.IsAllocationLocked)
                return false;

            if (!pager.IsInitialized && !pager.HasInitializationFault)
                pager.Initialize(HectonPersistentPathPolicy.CombineFile("world_data.h8bin"));

            return pager.IsInitialized && !pager.HasInitializationFault;
        }

        private static void EnsureStaticLoadCandidateScratch()
        {
            StaticNativeBuffers.EnsureLoadCandidateScratch();
        }

        private static void DisposeStaticLoadCandidateScratch()
        {
            StaticNativeBuffers.Dispose();
        }

        public void Tick(float deltaTime)
        {
            if (_runtimeOwnerAborted || !_serviceRegistered)
                return;

            RecordWfcOutpostFrameBlackBox(
                WfcOutpostBlackBoxOperationFrame,
                WfcOutpostPersistenceStatus.None,
                _lastWfcOutpostSectorHash,
                _lastWfcOutpostPayloadHash,
                flags: BuildWfcOutpostFrameBlackBoxFlags());
            DrainWfcOutpostStateChangedSignals();
            DrainWfcSectorHydratedSignals();
            DrainChunkDehydratedSignals();
            DrainPendingOwnerHydration();
        }

        public bool TryRequestSave(byte slotIndex, uint sourceHash, uint operationId = 0u)
        {
            uint resolvedOperationId = ResolveOperationId(operationId);
            if (_runtimeOwnerAborted || !_serviceRegistered)
            {
                LastOperationError = SaveServiceUnavailableReason;
                LastOperationSlot = slotIndex < SaveEvents.ManualSlotCount
                    ? SaveEvents.ResolveManualSlotName(slotIndex)
                    : string.Empty;
                SaveEvents.TryRaiseSaveFailed(ResolveSlotHash(slotIndex), SaveEvents.ComputeMessageHash(SaveServiceUnavailableReason), SaveServiceUnavailableReason);
                PublishSaveStatus(slotIndex, resolvedOperationId, SaveStatusSignal.Rejected, 0f, 1u);
                return false;
            }

            if (slotIndex >= SaveEvents.ManualSlotCount)
            {
                LastOperationError = InvalidSlotNameReason;
                LastOperationSlot = string.Empty;
                SaveEvents.TryRaiseSaveFailed(0u, SaveEvents.ComputeMessageHash(InvalidSlotNameReason), InvalidSlotNameReason);
                PublishSaveStatus(slotIndex, resolvedOperationId, SaveStatusSignal.Rejected, 0f, 1u);
                return false;
            }

            string slotName = SaveEvents.ResolveManualSlotName(slotIndex);
            if (_isBusy)
            {
                const string reason = "Save already in progress.";
                LastOperationError = reason;
                LastOperationSlot = slotName;
                SaveEvents.TryRaiseSaveFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(reason), reason);
                PublishSaveStatus(slotIndex, resolvedOperationId, SaveStatusSignal.Rejected, 0f, 1u);
                return false;
            }

            if (TryRejectSaveDuringRespawnReconciliation(slotIndex, resolvedOperationId, slotName))
                return false;

            SaveRequestSignal signal = new SaveRequestSignal
            {
                SourceHash = sourceHash != 0u ? sourceHash : AsyncPersistenceSourceHash,
                OperationId = resolvedOperationId,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                SlotIndex = slotIndex,
                Flags = SaveRequestSignal.ManualSlotFlag
            };
            PublishSaveStatus(slotIndex, signal.OperationId, SaveStatusSignal.Queued, 0f, 0u);
            ProcessSaveRequest(in signal);
            return true;
        }

        public void SlowTick()
        {
            if (_runtimeOwnerAborted || !_serviceRegistered)
                return;

            unchecked
            {
                _slowTickSequence++;
                if (_slowTickSequence == int.MinValue)
                    _slowTickSequence = 0;
            }

            RefreshWfcOutpostDependencyReadiness();
            if (!_wfcOutpostDependenciesReady)
                return;

            RetryPendingWfcOutpostDirtyAppends();
        }

        public void FrostTick()
        {
            if (_runtimeOwnerAborted || !_serviceRegistered)
                return;

            MacroDatabaseTier tier = ResolveMacroDatabaseCompactionTier();
            NotifyMacroDatabasePersistenceGate(_isBusy);
            if (_isBusy)
                return;

            if (TryResolveWfcOutpostMacroDatabase(out IMacroDatabaseService macroDatabase))
                macroDatabase.FrostTickCompaction(tier, false);
        }

        public void LateFrameTick()
        {
            if (_runtimeOwnerAborted ||
                !_serviceRegistered ||
                !_compressionThrottleLateFrameArmed ||
                SystemDispatcher.CurrentFrameIndex < _compressionThrottleReleaseFrame)
                return;

            _compressionThrottleLateFrameArmed = false;
        }

        private void ProcessSaveRequest(in SaveRequestSignal signal)
        {
            if (_runtimeOwnerAborted || !_serviceRegistered)
                return;

            byte slotIndex = signal.SlotIndex;
            uint operationId = ResolveOperationId(signal.OperationId);
            if (slotIndex >= SaveEvents.ManualSlotCount)
            {
                LastOperationError = InvalidSlotNameReason;
                LastOperationSlot = string.Empty;
                SaveEvents.TryRaiseSaveFailed(0u, SaveEvents.ComputeMessageHash(InvalidSlotNameReason), InvalidSlotNameReason);
                PublishSaveStatus(slotIndex, operationId, SaveStatusSignal.Rejected, 0f, 1u);
                return;
            }

            string slotName = SaveEvents.ResolveManualSlotName(slotIndex);
            if (_isBusy)
            {
                const string reason = "Save already in progress.";
                LastOperationError = reason;
                LastOperationSlot = slotName;
                SaveEvents.TryRaiseSaveFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(reason), reason);
                PublishSaveStatus(slotIndex, operationId, SaveStatusSignal.Rejected, 0f, 1u);
                return;
            }

            if (TryRejectSaveDuringRespawnReconciliation(slotIndex, operationId, slotName))
                return;

            _ = SaveGameAsyncInternal(slotName, slotIndex, operationId);
        }

        private void DrainWfcOutpostStateChangedSignals()
        {
            ReadOnlySpan<WfcOutpostStateChangedSignal> signals = SignalBus<WfcOutpostStateChangedSignal>.GetFrameSnapshot();
            if (signals.Length == 0)
                return;

            if (signals.Length > MaxWfcDirtySectorStackEntries)
            {
                DrainWfcOutpostStateChangedSignalsStorm(signals);
                return;
            }

            Span<ulong> dirtySectors = _wfcDirtySectorScratch.AsSpan(0, signals.Length);
            Span<ushort> dirtyCellIndices = _wfcDirtyCellIndexScratch.AsSpan(0, signals.Length);
            Span<byte> dirtyCellFlags = _wfcDirtyCellFlagScratch.AsSpan(0, signals.Length);
            int dirtySectorCount = 0;

            for (int i = 0; i < signals.Length; i++)
            {
                WfcOutpostStateChangedSignal signal = signals[i];
                if (!IsPersistableWfcOutpostStateSignal(in signal))
                    continue;

                RecordWfcOutpostStateChangedSignalEvent(in signal);

                if (!ContainsWfcOutpostSector(dirtySectors, dirtySectorCount, signal.SectorHash))
                    dirtySectors[dirtySectorCount++] = signal.SectorHash;
            }

            for (int sectorIndex = 0; sectorIndex < dirtySectorCount; sectorIndex++)
            {
                ulong dirtySectorHash = dirtySectors[sectorIndex];
                if (!ProcessWfcOutpostDirtySector(
                        dirtySectorHash,
                        signals,
                        dirtyCellIndices,
                        dirtyCellFlags,
                        isStormMode: false))
                {
                    return;
                }
            }
        }

        private void DrainWfcOutpostStateChangedSignalsStorm(ReadOnlySpan<WfcOutpostStateChangedSignal> signals)
        {
            Span<ulong> dirtySectors = _wfcDirtySectorScratch.AsSpan(0, MaxWfcDirtySectorStackEntries);
            Span<ushort> dirtyCellIndices = _wfcDirtyCellIndexScratch.AsSpan(0, MaxWfcDirtySectorStackEntries);
            Span<byte> dirtyCellFlags = _wfcDirtyCellFlagScratch.AsSpan(0, MaxWfcDirtySectorStackEntries);
            int dirtySectorCount = 0;
            bool sectorOverflow = false;
            uint overflowFrame = 0u;

            for (int i = 0; i < signals.Length; i++)
            {
                WfcOutpostStateChangedSignal signal = signals[i];
                if (!IsPersistableWfcOutpostStateSignal(in signal))
                    continue;

                RecordWfcOutpostStateChangedSignalEvent(in signal);

                if (ContainsWfcOutpostSector(dirtySectors, dirtySectorCount, signal.SectorHash))
                    continue;

                if (dirtySectorCount >= dirtySectors.Length)
                {
                    sectorOverflow = true;
                    overflowFrame = signal.Frame;
                    continue;
                }

                dirtySectors[dirtySectorCount++] = signal.SectorHash;
            }

            if (sectorOverflow)
            {
                RecordWfcOutpostEventBlackBox(
                    WfcOutpostBlackBoxOperationSignal,
                    WfcOutpostPersistenceStatus.Rejected,
                    0UL,
                    signalSourceHash: WfcOutpostPersistenceSourceHash,
                    flags: WfcOutpostBlackBoxSignalFlagOverflow,
                    frame: overflowFrame);
            }

            for (int sectorIndex = 0; sectorIndex < dirtySectorCount; sectorIndex++)
            {
                ulong dirtySectorHash = dirtySectors[sectorIndex];

                if (!ProcessWfcOutpostDirtySector(
                        dirtySectorHash,
                        signals,
                        dirtyCellIndices,
                        dirtyCellFlags,
                        isStormMode: true))
                {
                    return;
                }
            }
        }

        private bool ProcessWfcOutpostDirtySector(
            ulong dirtySectorHash,
            ReadOnlySpan<WfcOutpostStateChangedSignal> signals,
            Span<ushort> dirtyCellIndices,
            Span<byte> dirtyCellFlags,
            bool isStormMode)
        {
            bool hasHydration = false;
            ulong hydratedPayloadHash = 0UL;
            int hydratedPayloadBytes = 0;
            uint hydratedCacheFlags = 0u;
            uint hydratedCacheFrame = 0u;
            if (_wfcOutpostMutableGridSectorHash != dirtySectorHash)
            {
                hasHydration = TryStageWfcOutpostStateOverrideFromHydration(
                    dirtySectorHash,
                    out hydratedPayloadHash,
                    out hydratedPayloadBytes,
                    out hydratedCacheFlags,
                    out hydratedCacheFrame);
            }

            int dirtyCellWriteCount = CollectWfcOutpostSignalWrites(
                signals,
                dirtySectorHash,
                dirtyCellIndices,
                dirtyCellFlags,
                out uint dirtyFrame,
                out bool writeOverflow);
            if (dirtyCellWriteCount <= 0)
                return true;

            if (isStormMode && writeOverflow)
            {
                RecordWfcOutpostEventBlackBox(
                    WfcOutpostBlackBoxOperationSignal,
                    WfcOutpostPersistenceStatus.Rejected,
                    dirtySectorHash,
                    signalSourceHash: WfcOutpostPersistenceSourceHash,
                    flags: WfcOutpostBlackBoxSignalFlagOverflow,
                    frame: dirtyFrame);
                PublishWfcWriteFailureWarning();
                return true;
            }

            if (!TryApplyWfcOutpostGridWritesAndSnapshot(
                    dirtySectorHash,
                    dirtyCellIndices,
                    dirtyCellFlags,
                    dirtyCellWriteCount,
                    hasHydration,
                    hydratedPayloadHash,
                    hydratedPayloadBytes,
                    hydratedCacheFlags,
                    hydratedCacheFrame,
                    out bool copiedGridSnapshot,
                    out WfcOutpostPersistenceStatus status,
                    out bool publishWriteFailure))
            {
                return false;
            }

            StageAndCommitWfcOutpostGridSnapshot(
                dirtySectorHash,
                dirtyFrame,
                copiedGridSnapshot,
                status,
                publishWriteFailure);

            return true;
        }

        private bool TryApplyWfcOutpostGridWritesAndSnapshot(
            ulong dirtySectorHash,
            Span<ushort> dirtyCellIndices,
            Span<byte> dirtyCellFlags,
            int dirtyCellWriteCount,
            bool hasHydration,
            ulong hydratedPayloadHash,
            int hydratedPayloadBytes,
            uint hydratedCacheFlags,
            uint hydratedCacheFrame,
            out bool copiedGridSnapshot,
            out WfcOutpostPersistenceStatus status,
            out bool publishWriteFailure)
        {
            copiedGridSnapshot = false;
            status = WfcOutpostPersistenceStatus.None;
            publishWriteFailure = false;

            if (!TryAcquireWfcOutpostGridWrite(
                    out NativeArray<byte> wfcGrid,
                    out VaultGenerationHandle<byte> wfcGridHandle,
                    out IDataVault wfcGridVault))
            {
                RecordWfcOutpostEventBlackBox(
                    WfcOutpostBlackBoxOperationPersist,
                    WfcOutpostPersistenceStatus.InvalidGrid,
                    dirtySectorHash);
                return false;
            }

            bool appliedHydration = false;
            try
            {
                appliedHydration = PrepareWfcOutpostMutableGridForSector(
                    dirtySectorHash,
                    wfcGrid,
                    hasHydration,
                    hydratedPayloadHash,
                    hydratedPayloadBytes,
                    hydratedCacheFlags,
                    hydratedCacheFrame);

                for (int writeIndex = 0; writeIndex < dirtyCellWriteCount; writeIndex++)
                    wfcGrid[dirtyCellIndices[writeIndex]] = dirtyCellFlags[writeIndex];

                if (!TryCopyWfcOutpostGridToSnapshotScratch(wfcGrid))
                {
                    status = WfcOutpostPersistenceStatus.InvalidGrid;
                    publishWriteFailure = true;
                }
                else
                {
                    copiedGridSnapshot = true;
                }
            }
            finally
            {
                wfcGridVault.ReleaseWriteLock(in wfcGridHandle, SystemID.CoreDataVault);
            }

            if (appliedHydration)
            {
                RecordWfcOutpostEventBlackBox(
                    WfcOutpostBlackBoxOperationHydration,
                    WfcOutpostPersistenceStatus.Ready,
                    dirtySectorHash,
                    hydratedPayloadHash,
                    hydratedPayloadBytes);
            }

            return true;
        }

        private void StageAndCommitWfcOutpostGridSnapshot(
            ulong dirtySectorHash,
            uint dirtyFrame,
            bool copiedGridSnapshot,
            WfcOutpostPersistenceStatus status,
            bool publishWriteFailure)
        {
            bool stageSucceeded = false;
            bool needsCommit = false;
            ulong packedHash = 0UL;
            int payloadBytes = 0;

            if (copiedGridSnapshot)
            {
                stageSucceeded = TryStageWfcOutpostStateSnapshotPayload(
                    dirtySectorHash,
                    _wfcOutpostGridSnapshotScratch,
                    dirtyFrame,
                    out status,
                    out packedHash,
                    out payloadBytes,
                    out needsCommit,
                    out publishWriteFailure);
            }
            else if (status != WfcOutpostPersistenceStatus.None)
            {
                RecordWfcOutpostEventBlackBox(
                    WfcOutpostBlackBoxOperationPersist,
                    status,
                    dirtySectorHash,
                    frame: dirtyFrame);
            }

            if (publishWriteFailure)
                PublishWfcWriteFailureWarning();

            if (stageSucceeded && needsCommit)
            {
                TryCommitWfcOutpostStateSnapshotPayload(
                    dirtySectorHash,
                    dirtyFrame,
                    packedHash,
                    payloadBytes,
                    out _);
            }
        }

        private void RecordWfcOutpostStateChangedSignalEvent(in WfcOutpostStateChangedSignal signal)
        {
            RecordWfcOutpostEventBlackBox(
                WfcOutpostBlackBoxOperationSignal,
                WfcOutpostPersistenceStatus.None,
                signal.SectorHash,
                cellIndex: signal.CellIndex,
                previousFlags: signal.PreviousFlags,
                currentFlags: signal.CurrentFlags,
                signalSourceHash: signal.SourceHash,
                flags: signal.Flags,
                frame: signal.Frame);
        }

        private static int CollectWfcOutpostSignalWrites(
            ReadOnlySpan<WfcOutpostStateChangedSignal> signals,
            ulong sectorHash,
            Span<ushort> cellIndices,
            Span<byte> cellFlags,
            out uint dirtyFrame,
            out bool overflow)
        {
            dirtyFrame = 0u;
            overflow = false;
            int writeCount = 0;
            int writeCapacity = math.min(cellIndices.Length, cellFlags.Length);
            for (int signalIndex = 0; signalIndex < signals.Length; signalIndex++)
            {
                WfcOutpostStateChangedSignal signal = signals[signalIndex];
                if (signal.SectorHash != sectorHash ||
                    !IsPersistableWfcOutpostStateSignal(in signal))
                {
                    continue;
                }

                dirtyFrame = signal.Frame;
                if (writeCount >= writeCapacity)
                {
                    overflow = true;
                    continue;
                }

                cellIndices[writeCount] = signal.CellIndex;
                cellFlags[writeCount] = (byte)(signal.CurrentFlags & WfcOutpostPersistenceConstants.MutableFlagMask);
                writeCount++;
            }

            return writeCount;
        }

        private static bool IsPersistableWfcOutpostStateSignal(in WfcOutpostStateChangedSignal signal)
        {
            return signal.SectorHash != 0UL &&
                   signal.CellIndex < WfcOutpostPersistenceConstants.CellCount &&
                   ((signal.PreviousFlags ^ signal.CurrentFlags) & WfcOutpostPersistenceConstants.MutableFlagMask) != 0;
        }

        private static bool ContainsWfcOutpostSector(ReadOnlySpan<ulong> sectors, int sectorCount, ulong sectorHash)
        {
            for (int i = 0; i < sectorCount; i++)
            {
                if (sectors[i] == sectorHash)
                    return true;
            }

            return false;
        }

        private void DrainWfcSectorHydratedSignals()
        {
            ReadOnlySpan<Hecton8.Core.Contracts.Signals.MacroDatabaseSectorHydrationSignal> signals =
                SignalBus<Hecton8.Core.Contracts.Signals.MacroDatabaseSectorHydrationSignal>.GetFrameSnapshot();
            if (signals.Length == 0)
                return;

            IMacroDatabaseService macroDatabase = _macroDatabaseService;
            Span<ulong> hydrationSectors = stackalloc ulong[MaxWfcSectorHydrationProbesPerTick];
            int hydrationSectorCount = 0;

            for (int i = 0; i < signals.Length && hydrationSectorCount < MaxWfcSectorHydrationProbesPerTick; i++)
            {
                Hecton8.Core.Contracts.Signals.MacroDatabaseSectorHydrationSignal signal = signals[i];
                if (signal.SectorHash == 0UL ||
                    signal.PayloadBytes < sizeof(uint) ||
                    signal.PayloadBytes > WfcOutpostPersistenceConstants.PayloadMaxBytes ||
                    !IsWfcOutpostHydrationCandidate(in signal, macroDatabase))
                {
                    continue;
                }

                if (!ContainsWfcOutpostSector(hydrationSectors, hydrationSectorCount, signal.SectorHash))
                    hydrationSectors[hydrationSectorCount++] = signal.SectorHash;
            }

            for (int sectorIndex = 0; sectorIndex < hydrationSectorCount; sectorIndex++)
            {
                ulong sectorHash = hydrationSectors[sectorIndex];
                if (!TryStageWfcOutpostStateOverrideFromHydration(
                        sectorHash,
                        out ulong hydratedPayloadHash,
                        out int hydratedPayloadBytes,
                        out uint hydratedCacheFlags,
                        out uint hydratedCacheFrame))
                {
                    continue;
                }

                if (!TryAcquireWfcOutpostGridWrite(
                        out NativeArray<byte> wfcGrid,
                        out VaultGenerationHandle<byte> wfcGridHandle,
                        out IDataVault wfcGridVault))
                {
                    RecordWfcOutpostEventBlackBox(
                        WfcOutpostBlackBoxOperationHydration,
                        WfcOutpostPersistenceStatus.InvalidGrid,
                        sectorHash);
                    return;
                }

                bool appliedHydration = false;
                try
                {
                    appliedHydration = PrepareWfcOutpostMutableGridForSector(
                        sectorHash,
                        wfcGrid,
                        true,
                        hydratedPayloadHash,
                        hydratedPayloadBytes,
                        hydratedCacheFlags,
                        hydratedCacheFrame);
                }
                finally
                {
                    wfcGridVault.ReleaseWriteLock(in wfcGridHandle, SystemID.CoreDataVault);
                }

                if (appliedHydration)
                {
                    RecordWfcOutpostEventBlackBox(
                        WfcOutpostBlackBoxOperationHydration,
                        WfcOutpostPersistenceStatus.Ready,
                        sectorHash,
                        hydratedPayloadHash,
                        hydratedPayloadBytes);
                }
            }
        }

        private void DrainChunkDehydratedSignals()
        {
            int drained = 0;
            while (drained < MaxChunkDehydrationSignalsPerTick &&
                   SignalBus<ChunkDehydratedSignal>.TryConsumeFrame(out ChunkDehydratedSignal signal))
            {
                drained++;
                EnqueueChunkDehydrationPayloads(in signal);
            }
        }

        private void EnqueueChunkDehydrationPayloads(in ChunkDehydratedSignal signal)
        {
            EnsureWorldPagerInitialized();
            if (_worldPager == null || !_worldPager.IsInitialized || _worldPager.HasInitializationFault)
                return;

            if (!_saveStagingBuffer.IsCreated || _saveStagingBuffer.Length < SaveStagingBufferBytes)
                return;

            PlayerInventory inventory = ResolveRegisteredSaveable<PlayerInventory>();
            if (inventory != null &&
                inventory.TryCopyInventoryShadowPayload(_saveStagingBuffer, out int inventoryPayloadLength, out _))
            {
                if (_worldPager.TryEnqueueWrite(
                        DerivePayloadSectorHash(signal.SectorHash, H8WorldPagePayloadTypes.InventoryState),
                        H8WorldPagePayloadTypes.InventoryState,
                        _saveStagingBuffer,
                        inventoryPayloadLength,
                        WorldPagerSourceHash,
                        signal.Frame))
                {
                    TryPublishWorldPagerSavingNotification();
                }
            }

            int metadataPayloadLength = StageChunkDehydrationMetadata(in signal);
            if (metadataPayloadLength > 0)
            {
                if (_worldPager.TryEnqueueWrite(
                        DerivePayloadSectorHash(signal.SectorHash, H8WorldPagePayloadTypes.ChunkDehydratedMetadata),
                        H8WorldPagePayloadTypes.ChunkDehydratedMetadata,
                        _saveStagingBuffer,
                        metadataPayloadLength,
                        WorldPagerSourceHash,
                        signal.Frame))
                {
                    TryPublishWorldPagerSavingNotification();
                }
            }
        }

        private T ResolveRegisteredSaveable<T>() where T : class, ISaveable
        {
            for (int i = 0; i < _saveableCount; i++)
            {
                ISaveable saveable = _saveables[i];
                if (!IsAlive(saveable))
                {
                    _registryDirty = true;
                    continue;
                }

                if (saveable is T typed)
                    return typed;
            }

            return null;
        }

        private unsafe int StageChunkDehydrationMetadata(in ChunkDehydratedSignal signal)
        {
            int bytes = UnsafeUtility.SizeOf<ChunkDehydratedSignal>();
            if (!_saveStagingBuffer.IsCreated || _saveStagingBuffer.Length < bytes)
                return 0;

            ChunkDehydratedSignal copy = signal;
            UnsafeUtility.CopyStructureToPtr(ref copy, _saveStagingBuffer.GetUnsafePtr());
            return bytes;
        }

        private static long DerivePayloadSectorHash(long sectorHash, uint payloadType)
        {
            if (payloadType == H8WorldPagePayloadTypes.VoxelDeltaRle)
                return sectorHash;

            unchecked
            {
                ulong mixed = (ulong)sectorHash ^ ((ulong)payloadType * 11400714819323198485UL);
                mixed ^= mixed >> 33;
                mixed *= 0xff51afd7ed558ccdUL;
                mixed ^= mixed >> 33;
                return (long)mixed;
            }
        }

        private unsafe bool TryStageWfcOutpostStateOverrideFromHydration(
            ulong sectorHash,
            out ulong hydratedPayloadHash,
            out int payloadBytes,
            out uint cacheFlags,
            out uint cacheFrame)
        {
            hydratedPayloadHash = 0UL;
            payloadBytes = 0;
            cacheFlags = 0u;
            cacheFrame = 0u;
            if (sectorHash == 0UL)
            {
                RecordWfcOutpostEventBlackBox(WfcOutpostBlackBoxOperationHydration, WfcOutpostPersistenceStatus.Rejected, sectorHash);
                return false;
            }

            if (!TryResolveWfcOutpostMacroDatabase(out IMacroDatabaseService macroDatabase))
            {
                RecordWfcOutpostEventBlackBox(WfcOutpostBlackBoxOperationHydration, WfcOutpostPersistenceStatus.ServiceUnavailable, sectorHash);
                return false;
            }

            if (!HasWfcOutpostNativeBuffers())
            {
                RecordWfcOutpostEventBlackBox(WfcOutpostBlackBoxOperationHydration, WfcOutpostPersistenceStatus.ServiceUnavailable, sectorHash);
                return false;
            }

            if (!macroDatabase.TryCopyPayload(
                    sectorHash,
                    0,
                    _wfcOutpostPayloadBuffer,
                    _wfcOutpostPayloadBuffer.Length,
                    out payloadBytes,
                    out MacroDatabasePayloadHandle handle) ||
                payloadBytes != handle.ByteLength)
            {
                RecordWfcOutpostEventBlackBox(WfcOutpostBlackBoxOperationHydration, WfcOutpostPersistenceStatus.Missing, sectorHash);
                return false;
            }

            byte* payloadPointer = (byte*)_wfcOutpostPayloadBuffer.GetUnsafeReadOnlyPtr();
            if (!SaveBinaryPayloadCodec.HasWfcOutpostBitmaskMagic(payloadPointer, handle.ByteLength))
            {
                RecordWfcOutpostEventBlackBox(WfcOutpostBlackBoxOperationHydration, WfcOutpostPersistenceStatus.Missing, sectorHash, payloadBytes: handle.ByteLength);
                return false;
            }

            if (
                handle.ByteLength < WfcOutpostPersistenceConstants.PayloadHeaderBytes ||
                handle.ByteLength > WfcOutpostPersistenceConstants.PayloadMaxBytes ||
                !SaveBinaryPayloadCodec.TryReadWfcOutpostBitmaskPayload(
                    payloadPointer,
                    handle.ByteLength,
                    _wfcOutpostRestoreWords,
                    WfcOutpostPersistenceConstants.PackedWordCount,
                    out int wordsRead) ||
                wordsRead != WfcOutpostPersistenceConstants.PackedWordCount)
            {
                RecordWfcOutpostEventBlackBox(WfcOutpostBlackBoxOperationHydration, WfcOutpostPersistenceStatus.CorruptLength, sectorHash, payloadBytes: handle.ByteLength);
                PublishWfcCorruptPayloadWarning();
                return false;
            }

            hydratedPayloadHash = ComputeWfcOutpostPackedHash(_wfcOutpostRestoreWords);
            cacheFlags = ResolveWfcOutpostSnapshotCacheFlags(in handle);
            cacheFrame = ResolveWfcOutpostSnapshotCacheFrame(cacheFlags);
            return true;
        }

        private void ResetWfcOutpostSectorCaches(bool clearMutableGrid)
        {
            _hasLastWfcOutpostSnapshot = false;
            _lastWfcOutpostSectorHash = 0UL;
            _lastWfcOutpostPayloadHash = 0UL;
            _wfcOutpostMutableGridSectorHash = 0UL;
            _wfcOutpostSnapshotCacheCount = 0;
            _wfcOutpostSnapshotCacheNextIndex = 0;
            _wfcOutpostSnapshotCacheRetryIndex = 0;
            if (clearMutableGrid &&
                TryAcquireWfcOutpostGridWrite(
                    out NativeArray<byte> wfcGrid,
                    out VaultGenerationHandle<byte> wfcGridHandle,
                    out IDataVault wfcGridVault))
            {
                try
                {
                    ClearWfcOutpostMutableStateGrid(wfcGrid);
                }
                finally
                {
                    wfcGridVault.ReleaseWriteLock(in wfcGridHandle, SystemID.CoreDataVault);
                }
            }

            _wfcOutpostGridHandle = default;
        }

        private unsafe bool IsWfcOutpostHydrationCandidate(
            in Hecton8.Core.Contracts.Signals.MacroDatabaseSectorHydrationSignal signal,
            IMacroDatabaseService macroDatabase)
        {
            if (macroDatabase == null || !macroDatabase.IsOpen)
                return signal.PayloadBytes >= WfcOutpostPersistenceConstants.PayloadHeaderBytes;

            if (!HasWfcOutpostNativeBuffers())
                return false;

            if (!macroDatabase.TryCopyPayload(
                    signal.SectorHash,
                    0,
                    _wfcOutpostPayloadBuffer,
                    WfcOutpostPersistenceConstants.PayloadHeaderBytes,
                    out int bytesCopied,
                    out MacroDatabasePayloadHandle handle))
            {
                return signal.PayloadBytes >= WfcOutpostPersistenceConstants.PayloadHeaderBytes;
            }

            if (handle.ByteLength > WfcOutpostPersistenceConstants.PayloadMaxBytes)
                return true;

            return bytesCopied >= WfcOutpostPersistenceConstants.PayloadHeaderBytes &&
                   SaveBinaryPayloadCodec.HasWfcOutpostBitmaskMagic(
                       (byte*)_wfcOutpostPayloadBuffer.GetUnsafeReadOnlyPtr(),
                       bytesCopied);
        }

        private bool TryGetCachedWfcOutpostSnapshotHash(ulong sectorHash, out ulong payloadHash)
        {
            if (_hasLastWfcOutpostSnapshot &&
                _lastWfcOutpostSectorHash == sectorHash)
            {
                payloadHash = _lastWfcOutpostPayloadHash;
                return true;
            }

            payloadHash = 0UL;
            if (!_wfcOutpostSnapshotCache.IsCreated)
                return false;

            int count = math.min(_wfcOutpostSnapshotCacheCount, _wfcOutpostSnapshotCache.Length);
            for (int i = 0; i < count; i++)
            {
                WfcOutpostSnapshotCacheEntry entry = _wfcOutpostSnapshotCache[i];
                if (entry.SectorHash == sectorHash)
                {
                    payloadHash = entry.PayloadHash;
                    return payloadHash != 0UL;
                }
            }

            return false;
        }

        private static uint ResolveWfcOutpostSnapshotCacheFlags(in MacroDatabasePayloadHandle handle)
        {
            const byte DirtyPayloadFlag = 1 << 0;
            return (handle.Flags & DirtyPayloadFlag) != 0
                ? WfcOutpostSnapshotCacheFlagAppendPending
                : 0u;
        }

        private static uint ResolveWfcOutpostSnapshotCacheFrame(uint cacheFlags)
        {
            return cacheFlags != 0u ? Hecton8.Core.SystemDispatcher.CurrentFrameId : 0u;
        }

        private void RememberWfcOutpostSnapshotHash(
            ulong sectorHash,
            ulong payloadHash,
            uint cacheFlags,
            uint frame)
        {
            if (sectorHash == 0UL || payloadHash == 0UL)
                return;

            if (!_wfcOutpostSnapshotCache.IsCreated)
            {
                if (cacheFlags == 0u)
                    RememberLastWfcOutpostSnapshotHash(sectorHash, payloadHash);

                return;
            }

            int count = math.min(_wfcOutpostSnapshotCacheCount, _wfcOutpostSnapshotCache.Length);
            for (int i = 0; i < count; i++)
            {
                WfcOutpostSnapshotCacheEntry entry = _wfcOutpostSnapshotCache[i];
                if (entry.SectorHash == sectorHash)
                {
                    entry.PayloadHash = payloadHash;
                    entry.Flags = cacheFlags != 0u
                        ? cacheFlags | (entry.Flags & WfcOutpostSnapshotCacheFlagAppendInFlight)
                        : 0u;
                    entry.LastAppendFrame = frame;
                    _wfcOutpostSnapshotCache[i] = entry;
                    RememberLastWfcOutpostSnapshotHash(sectorHash, payloadHash);
                    return;
                }
            }

            int writeIndex;
            if (count < _wfcOutpostSnapshotCache.Length)
            {
                writeIndex = count;
                _wfcOutpostSnapshotCacheCount = count + 1;
                _wfcOutpostSnapshotCacheNextIndex = _wfcOutpostSnapshotCacheCount % _wfcOutpostSnapshotCache.Length;
            }
            else
            {
                writeIndex = FindWfcOutpostSnapshotCacheReplacementIndex(count);
                if (writeIndex < 0)
                {
                    if (cacheFlags == 0u)
                        RememberLastWfcOutpostSnapshotHash(sectorHash, payloadHash);

                    return;
                }

                _wfcOutpostSnapshotCacheNextIndex = WrapWfcOutpostSnapshotCacheIndex(writeIndex + 1);
            }

            _wfcOutpostSnapshotCache[writeIndex] = new WfcOutpostSnapshotCacheEntry
            {
                SectorHash = sectorHash,
                PayloadHash = payloadHash,
                Flags = cacheFlags,
                LastAppendFrame = frame
            };
            RememberLastWfcOutpostSnapshotHash(sectorHash, payloadHash);
        }

        private void RememberLastWfcOutpostSnapshotHash(ulong sectorHash, ulong payloadHash)
        {
            _hasLastWfcOutpostSnapshot = true;
            _lastWfcOutpostSectorHash = sectorHash;
            _lastWfcOutpostPayloadHash = payloadHash;
        }

        private bool PrepareWfcOutpostMutableGridForSector(
            ulong sectorHash,
            NativeArray<byte> wfcGrid,
            bool hasHydration,
            ulong hydratedPayloadHash,
            int hydratedPayloadBytes,
            uint hydratedCacheFlags,
            uint hydratedCacheFrame)
        {
            if (_wfcOutpostMutableGridSectorHash == sectorHash && !hasHydration)
                return false;

            ClearWfcOutpostMutableStateGrid(wfcGrid);
            if (hasHydration)
            {
                UnpackWfcOutpostMutableStateGrid(_wfcOutpostRestoreWords, wfcGrid);
                RememberWfcOutpostSnapshotHash(
                    sectorHash,
                    hydratedPayloadHash,
                    hydratedCacheFlags,
                    hydratedCacheFrame);
            }

            _wfcOutpostMutableGridSectorHash = sectorHash;
            return hasHydration;
        }

        private static unsafe void ClearWfcOutpostMutableStateGrid(NativeArray<byte> wfcGrid)
        {
            if (!IsValidWfcOutpostGrid(wfcGrid))
                return;

            UnsafeUtility.MemClear(wfcGrid.GetUnsafePtr(), WfcOutpostPersistenceConstants.CellCount);
        }

        private static bool IsValidWfcOutpostGrid(NativeArray<byte> wfcGrid)
        {
            return wfcGrid.IsCreated && wfcGrid.Length >= WfcOutpostPersistenceConstants.CellCount;
        }

        private static bool IsValidWfcOutpostGrid(NativeArray<byte>.ReadOnly wfcGrid)
        {
            return wfcGrid.IsCreated && wfcGrid.Length >= WfcOutpostPersistenceConstants.CellCount;
        }

        private static unsafe void UnpackWfcOutpostMutableStateGrid(NativeArray<ulong> packedWords, NativeArray<byte> wfcGrid)
        {
            ulong* words = (ulong*)packedWords.GetUnsafeReadOnlyPtr();
            byte* cells = (byte*)wfcGrid.GetUnsafePtr();
            for (int cell = 0; cell < WfcOutpostPersistenceConstants.CellCount; cell++)
            {
                int doorOpenBit = cell;
                int doorUnlockedBit = cell + WfcOutpostPersistenceConstants.CellCount;
                int powerOnBit = cell + (WfcOutpostPersistenceConstants.CellCount * 2);
                int datapadLootedBit = cell + (WfcOutpostPersistenceConstants.CellCount * 3);
                byte flags = (byte)(
                    (((words[doorOpenBit >> 6] >> (doorOpenBit & 63)) & 1UL) << 0) |
                    (((words[doorUnlockedBit >> 6] >> (doorUnlockedBit & 63)) & 1UL) << 1) |
                    (((words[powerOnBit >> 6] >> (powerOnBit & 63)) & 1UL) << 2) |
                    (((words[datapadLootedBit >> 6] >> (datapadLootedBit & 63)) & 1UL) << 3));

                cells[cell] = flags;
            }
        }

        private static unsafe ulong ComputeWfcOutpostPackedHash(NativeArray<ulong> packedWords)
        {
            ulong hash = 1469598103934665603UL;
            ulong* words = (ulong*)packedWords.GetUnsafeReadOnlyPtr();
            for (int i = 0; i < WfcOutpostPersistenceConstants.PackedWordCount; i++)
                hash = (hash ^ words[i]) * 1099511628211UL;

            return hash != 0UL ? hash : 1UL;
        }

        private static unsafe void PackWfcOutpostMutableStateGrid(
            NativeArray<byte> wfcGrid,
            NativeArray<ulong> packedWords)
        {
            ulong* words = (ulong*)packedWords.GetUnsafePtr();
            for (int word = 0; word < WfcOutpostPersistenceConstants.PackedWordCount; word++)
                words[word] = 0UL;

            byte* cells = (byte*)wfcGrid.GetUnsafeReadOnlyPtr();
            for (int cell = 0; cell < WfcOutpostPersistenceConstants.CellCount; cell++)
            {
                ulong flags = (ulong)(cells[cell] & 0x0F);
                int doorOpenBit = cell;
                int doorUnlockedBit = cell + WfcOutpostPersistenceConstants.CellCount;
                int powerOnBit = cell + (WfcOutpostPersistenceConstants.CellCount * 2);
                int datapadLootedBit = cell + (WfcOutpostPersistenceConstants.CellCount * 3);

                words[doorOpenBit >> 6] |= (flags & 1UL) << (doorOpenBit & 63);
                words[doorUnlockedBit >> 6] |= ((flags >> 1) & 1UL) << (doorUnlockedBit & 63);
                words[powerOnBit >> 6] |= ((flags >> 2) & 1UL) << (powerOnBit & 63);
                words[datapadLootedBit >> 6] |= ((flags >> 3) & 1UL) << (datapadLootedBit & 63);
            }
        }

        private void RetryPendingWfcOutpostDirtyAppends()
        {
            if (!_wfcOutpostSnapshotCache.IsCreated ||
                !TryResolveWfcOutpostMacroDatabase(out IMacroDatabaseService macroDatabase) ||
                IsWfcOutpostAppendDeferredByCompaction(macroDatabase))
            {
                return;
            }

            int count = math.min(_wfcOutpostSnapshotCacheCount, _wfcOutpostSnapshotCache.Length);
            if (count <= 0)
                return;

            int retries = 0;
            uint frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            int start = math.clamp(_wfcOutpostSnapshotCacheRetryIndex, 0, count - 1);
            int nextRetryIndex = start;
            for (int probe = 0; probe < count && retries < MaxWfcDirtyAppendRetriesPerSlowTick; probe++)
            {
                int i = start + probe;
                if (i >= count)
                    i -= count;

                nextRetryIndex = i + 1;
                if (nextRetryIndex >= count)
                    nextRetryIndex = 0;

                WfcOutpostSnapshotCacheEntry entry = _wfcOutpostSnapshotCache[i];
                if (entry.SectorHash == 0UL ||
                    entry.PayloadHash == 0UL ||
                    (entry.Flags & WfcOutpostSnapshotCacheFlagAppendPending) == 0u ||
                    (entry.Flags & WfcOutpostSnapshotCacheFlagAppendInFlight) != 0u ||
                    entry.LastAppendFrame == frame)
                {
                    continue;
                }

                QueueWfcOutpostDirtyAppend(entry.SectorHash, entry.PayloadHash, frame);
                retries++;
            }

            _wfcOutpostSnapshotCacheRetryIndex = nextRetryIndex;
        }

        private void MarkWfcOutpostAppendInFlight(ulong sectorHash, ulong payloadHash, uint frame)
        {
            UpdateWfcOutpostAppendFlags(
                sectorHash,
                payloadHash,
                WfcOutpostSnapshotCacheFlagAppendPending | WfcOutpostSnapshotCacheFlagAppendInFlight,
                frame);
        }

        private void MarkWfcOutpostAppendCompleted(ulong sectorHash, ulong payloadHash)
        {
            UpdateWfcOutpostAppendFlags(sectorHash, payloadHash, 0u, 0u);
        }

        private void MarkWfcOutpostAppendFailed(ulong sectorHash, ulong payloadHash, uint frame)
        {
            UpdateWfcOutpostAppendFlags(sectorHash, payloadHash, WfcOutpostSnapshotCacheFlagAppendPending, frame);
        }

        private void UpdateWfcOutpostAppendFlags(
            ulong sectorHash,
            ulong payloadHash,
            uint flags,
            uint frame)
        {
            if (!_wfcOutpostSnapshotCache.IsCreated ||
                sectorHash == 0UL ||
                payloadHash == 0UL)
            {
                return;
            }

            int count = math.min(_wfcOutpostSnapshotCacheCount, _wfcOutpostSnapshotCache.Length);
            for (int i = 0; i < count; i++)
            {
                WfcOutpostSnapshotCacheEntry entry = _wfcOutpostSnapshotCache[i];
                if (entry.SectorHash != sectorHash ||
                    entry.PayloadHash != payloadHash)
                {
                    continue;
                }

                entry.Flags = flags;
                entry.LastAppendFrame = frame;
                _wfcOutpostSnapshotCache[i] = entry;
                return;
            }

            if (flags != 0u)
                RememberWfcOutpostSnapshotHash(sectorHash, payloadHash, flags, frame);
        }

        private int FindWfcOutpostSnapshotCacheReplacementIndex(int count)
        {
            if (!_wfcOutpostSnapshotCache.IsCreated || count <= 0)
                return -1;

            int capacity = _wfcOutpostSnapshotCache.Length;
            int start = math.clamp(_wfcOutpostSnapshotCacheNextIndex, 0, capacity - 1);
            for (int probe = 0; probe < count; probe++)
            {
                int index = WrapWfcOutpostSnapshotCacheIndex(start + probe);
                WfcOutpostSnapshotCacheEntry entry = _wfcOutpostSnapshotCache[index];
                if ((entry.Flags & WfcOutpostSnapshotCacheFlagAppendAny) == 0u)
                    return index;
            }

            return -1;
        }

        private int WrapWfcOutpostSnapshotCacheIndex(int index)
        {
            int capacity = _wfcOutpostSnapshotCache.IsCreated
                ? _wfcOutpostSnapshotCache.Length
                : WfcOutpostSnapshotCacheCapacity;
            return index >= capacity ? index - capacity : index;
        }

        private void QueueWfcOutpostDirtyAppend(ulong sectorHash, ulong payloadHash, uint frame)
        {
            if (sectorHash == 0UL)
                return;

            if (payloadHash != 0UL)
                MarkWfcOutpostAppendInFlight(sectorHash, payloadHash, frame);

            _ = FlushWfcOutpostDirtyPayloadAsync(sectorHash, payloadHash, frame);
        }

        private async Awaitable FlushWfcOutpostDirtyPayloadAsync(ulong sectorHash, ulong payloadHash, uint frame)
        {
            bool appended = false;
            bool deferredByCompaction = false;
            uint appendFailureFlags = 0u;
            uint appendFailureCode = 0u;
            IMacroDatabaseService macroDatabase = _macroDatabaseService;
            try
            {
                await Awaitable.BackgroundThreadAsync();
                appended = macroDatabase != null &&
                           macroDatabase.IsOpen &&
                           macroDatabase.TryAppendDirtyPayload(sectorHash);
                deferredByCompaction = !appended && IsWfcOutpostAppendDeferredByCompaction(macroDatabase);
            }
            catch (Exception exception)
            {
                appended = false;
                appendFailureFlags |= WfcOutpostBlackBoxAppendFlagException;
                appendFailureCode = unchecked((uint)exception.HResult);
            }

            await Awaitable.MainThreadAsync();
            if (macroDatabase == null ||
                !ReferenceEquals(_macroDatabaseService, macroDatabase) ||
                !macroDatabase.IsOpen)
            {
                if (payloadHash != 0UL)
                    MarkWfcOutpostAppendFailed(sectorHash, payloadHash, frame);

                RecordWfcOutpostEventBlackBox(
                    WfcOutpostBlackBoxOperationAppend,
                    WfcOutpostPersistenceStatus.ServiceUnavailable,
                    sectorHash,
                    payloadHash,
                    signalSourceHash: appendFailureCode,
                    flags: appendFailureFlags,
                    frame: frame);
                if ((appendFailureFlags & WfcOutpostBlackBoxAppendFlagException) != 0u)
                    PublishWfcWriteFailureWarning();

                return;
            }

            if (!appended && (deferredByCompaction || IsWfcOutpostAppendDeferredByCompaction(macroDatabase)))
            {
                if (payloadHash != 0UL)
                    MarkWfcOutpostAppendFailed(sectorHash, payloadHash, frame);

                RecordWfcOutpostEventBlackBox(WfcOutpostBlackBoxOperationAppend, WfcOutpostPersistenceStatus.DirtyQueued, sectorHash, payloadHash, frame: frame);
                return;
            }

            if (appended)
            {
                if (payloadHash != 0UL)
                    MarkWfcOutpostAppendCompleted(sectorHash, payloadHash);

                RecordWfcOutpostEventBlackBox(WfcOutpostBlackBoxOperationAppend, WfcOutpostPersistenceStatus.Ready, sectorHash, payloadHash, frame: frame);
            }
            else
            {
                if (payloadHash != 0UL)
                    MarkWfcOutpostAppendFailed(sectorHash, payloadHash, frame);

                RecordWfcOutpostEventBlackBox(
                    WfcOutpostBlackBoxOperationAppend,
                    WfcOutpostPersistenceStatus.Rejected,
                    sectorHash,
                    payloadHash,
                    signalSourceHash: appendFailureCode,
                    flags: appendFailureFlags,
                    frame: frame);
                PublishWfcWriteFailureWarning();
            }
        }

        private static bool IsWfcOutpostAppendDeferredByCompaction(IMacroDatabaseService macroDatabase)
        {
            if (macroDatabase == null)
                return false;

            byte state = macroDatabase.Compaction.State;
            return state == (byte)MacroDatabaseCompactionState.Copying ||
                   state == (byte)MacroDatabaseCompactionState.Paused ||
                   state == (byte)MacroDatabaseCompactionState.ReadyToSwap ||
                   state == (byte)MacroDatabaseCompactionState.Swapping;
        }

        private static void PublishWfcBytesSaved(int payloadBytes)
        {
            int savedBytes = math.max(0, WfcOutpostPersistenceConstants.CellCount - payloadBytes);
            GlobalTelemetryBus.PublishModTelemetry(WfcOutpostPersistenceSourceHash, WfcBytesSavedTelemetryHash, savedBytes);
        }

        private void PublishWfcCorruptPayloadWarning()
        {
            GlobalTelemetryBus.PublishPerformanceWarning(WfcCorruptPayloadTelemetryHash, WfcOutpostPersistenceSourceHash, 1f);
            DumpWfcOutpostBlackBox();
        }

        private void PublishWfcWriteFailureWarning()
        {
            GlobalTelemetryBus.PublishPerformanceWarning(WfcWriteFailureTelemetryHash, WfcOutpostPersistenceSourceHash, 1f);
            DumpWfcOutpostBlackBox();
        }

        private void TryPublishWorldPagerSavingNotification()
        {
            H8BinaryWorldPager pager = _worldPager;
            if (pager == null || !pager.TryConsumePendingWriteBacklogNotification())
                return;

            HUDNotificationSignal notification = new HUDNotificationSignal
            {
                MessageHash = WorldPagerSavingMessageHash,
                ContextHash = WorldPagerSavingContextHash,
                SourceId = WorldPagerSourceHash,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                Severity = 0,
                Flags = 0
            };
            TryPushSignalTrackedBestEffort(in notification);
        }

        private uint ResolveOperationId(uint requestedOperationId)
        {
            if (requestedOperationId != 0u)
                return requestedOperationId;

            unchecked
            {
                _operationSequence++;
                if (_operationSequence == 0u)
                    _operationSequence = 1u;

                return _operationSequence;
            }
        }

        private static byte ResolveManualSlotIndex(string slotName)
        {
            int slotIndex = SaveEvents.ResolveKnownSlotIndex(slotName);
            return slotIndex >= 0 && slotIndex < SaveEvents.ManualSlotCount ? (byte)slotIndex : byte.MaxValue;
        }

        private static uint ComputeSlotHash(string slotName)
        {
            const uint fnvOffset = 2166136261u;
            const uint fnvPrime = 16777619u;
            uint hash = fnvOffset;
            if (!string.IsNullOrEmpty(slotName))
            {
                for (int i = 0; i < slotName.Length; i++)
                {
                    hash ^= slotName[i];
                    hash *= fnvPrime;
                }
            }

            return hash == 0u ? 1u : hash;
        }

        private static uint ResolveSlotHash(byte slotIndex)
        {
            return slotIndex < SaveEvents.ManualSlotCount
                ? ComputeSlotHash(SaveEvents.ResolveManualSlotName(slotIndex))
                : 0u;
        }

        private static uint ResolveSlotHash(byte slotIndex, string slotName)
        {
            if (slotIndex < SaveEvents.ManualSlotCount)
                return ResolveSlotHash(slotIndex);

            return string.IsNullOrEmpty(slotName) ? 0u : ComputeSlotHash(slotName);
        }

        private static uint ResolveUnavailableSlotContext(string slotName, byte slotIndex, out string safeSlotName)
        {
            if (TryResolveSafeSlotName(slotName, out safeSlotName))
                return ComputeSlotHash(safeSlotName);

            safeSlotName = string.Empty;
            return ResolveSlotHash(slotIndex);
        }

        private static uint SaturateToUInt(long value)
        {
            if (value <= 0L)
                return 0u;

            return value > uint.MaxValue ? uint.MaxValue : (uint)value;
        }


#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal static Action TestHook_PublishSaveStatus_SimulateException;
        internal static Action TestHook_DumpSaveBlackBox;
#endif

        private static void PublishSaveStatus(byte slotIndex, uint operationId, byte state, float progress01, uint flags)
        {
            try
            {
                uint slotHash = ResolveSlotHash(slotIndex);
                float clampedProgress = math.saturate(progress01);
                byte clampedFlags = (byte)math.min(flags, byte.MaxValue);
                SaveStatusSignal status = new SaveStatusSignal
                {
                    SlotHash = slotHash,
                    OperationId = operationId,
                    Progress01 = clampedProgress,
                    Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                    State = state,
                    Flags = clampedFlags
                };
                SaveEvents.PublishCurrentStatus(in status);
                TryPushSignalTrackedBestEffort(in status);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                TestHook_PublishSaveStatus_SimulateException?.Invoke();
#endif

                SaveLifecycleSignal lifecycle = new SaveLifecycleSignal
                {
                    SlotHash = slotHash,
                    OperationId = operationId,
                    Progress01 = clampedProgress,
                    Frame = status.Frame,
                    State = state,
                    Flags = clampedFlags
                };
#if UNITY_EDITOR
                PublishSaveLifecycleTestHook?.Invoke();
#endif
                TryPushSignalTrackedBestEffort(in lifecycle);
            }
            catch (Exception exception)
            {
                ReportPersistenceSignalBridgeFailure(exception);
            }
        }

        private static void PublishSaveStatusForSlotName(byte slotIndex, string slotName, uint operationId, byte state, float progress01, uint flags)
        {
            PublishSaveStatus(ResolveSlotHash(slotIndex, slotName), operationId, state, progress01, flags);
        }

#if UNITY_EDITOR
        internal static System.Action PublishSaveLifecycleTestHook;
#endif

        private static void PublishSaveStatus(uint slotHash, uint operationId, byte state, float progress01, uint flags)
        {
            try
            {
                float clampedProgress = math.saturate(progress01);
                byte clampedFlags = (byte)math.min(flags, byte.MaxValue);
                SaveStatusSignal status = new SaveStatusSignal
                {
                    SlotHash = slotHash,
                    OperationId = operationId,
                    Progress01 = clampedProgress,
                    Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                    State = state,
                    Flags = clampedFlags
                };
                SaveEvents.PublishCurrentStatus(in status);
                TryPushSignalTrackedBestEffort(in status);

                SaveLifecycleSignal lifecycle = new SaveLifecycleSignal
                {
                    SlotHash = slotHash,
                    OperationId = operationId,
                    Progress01 = clampedProgress,
                    Frame = status.Frame,
                    State = state,
                    Flags = clampedFlags
                };
#if UNITY_EDITOR
                PublishSaveLifecycleTestHook?.Invoke();
#endif
                TryPushSignalTrackedBestEffort(in lifecycle);
            }
            catch (Exception exception)
            {
                ReportPersistenceSignalBridgeFailure(exception);
            }
        }

        public readonly struct PublishSaveCompletedArgs
        {
            public readonly uint OperationId;
            public readonly long DurationMs;
            public readonly long CompressedSizeBytes;
            public readonly bool Succeeded;

            public PublishSaveCompletedArgs(uint operationId, long durationMs, long compressedSizeBytes, bool succeeded)
            {
                OperationId = operationId;
                DurationMs = durationMs;
                CompressedSizeBytes = compressedSizeBytes;
                Succeeded = succeeded;
            }
        }

        private static void PublishSaveCompletedForSlotName(byte slotIndex, string slotName, in PublishSaveCompletedArgs args)
        {
            PublishSaveCompleted(ResolveSlotHash(slotIndex, slotName), in args);
        }

        private static void PublishSaveCompleted(uint slotHash, in PublishSaveCompletedArgs args)
        {
            try
            {
                SaveCompletedSignal completed = new SaveCompletedSignal
                {
                    SlotHash = slotHash,
                    OperationId = args.OperationId,
                    DurationMilliseconds = SaturateToUInt(args.DurationMs),
                    CompressedSizeBytes = SaturateToUInt(args.CompressedSizeBytes),
                    Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                    Result = args.Succeeded ? (byte)1 : (byte)0,
                    Flags = args.Succeeded ? (byte)0 : (byte)1
                };
#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
                TestHook_PublishSaveCompleted_BeforePush?.Invoke();
#endif
                TryPushSignalTrackedBestEffort(in completed);
            }
            catch (Exception exception)
            {
                ReportPersistenceSignalBridgeFailure(exception);
            }
        }

        private static void RequestSnapshotPause(uint operationId)
        {
            SimulationPauseSignal pause = new SimulationPauseSignal
            {
                SourceHash = AsyncPersistenceSourceHash,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                Sequence = operationId,
                Paused = 1,
                Flags = 0,
                RestoreScalar = 1f
            };
            SimulationSignalRoute.TryQueuePause(in pause);
        }

        private static void ReleaseSnapshotPause(uint operationId)
        {
            SimulationPauseSignal resume = new SimulationPauseSignal
            {
                SourceHash = AsyncPersistenceSourceHash,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                Sequence = operationId,
                Paused = 0,
                Flags = 0,
                RestoreScalar = 1f
            };
            SimulationSignalRoute.TryQueuePause(in resume);
        }

        private unsafe void StageSnapshotHeader(
            uint operationId,
            string slotName,
            NativeArray<PersistentWorldDeltaRecord>.ReadOnly persistentWorldDeltas,
            NativeArray<EcosystemSectorSaveRecord>.ReadOnly ecosystemSectorStates,
            NativeArray<uint> packedQuestStateWords,
            NativeArray<byte> voxelDeltaSnapshot)
        {
            if (!_saveStagingBuffer.IsCreated || _saveStagingBuffer.Length < UnsafeUtility.SizeOf<SaveStagingHeader>())
                return;

            SaveStagingHeader header = new SaveStagingHeader
            {
                OperationId = operationId,
                SlotHash = ComputeSlotHash(slotName),
                SaveableCount = (uint)math.max(0, _saveableCount),
                PersistentWorldRecordCount = persistentWorldDeltas.IsCreated ? (uint)math.max(0, persistentWorldDeltas.Length) : 0u,
                EcosystemRecordCount = ecosystemSectorStates.IsCreated ? (uint)math.max(0, ecosystemSectorStates.Length) : 0u,
                QuestWordCount = packedQuestStateWords.IsCreated ? (uint)math.max(0, packedQuestStateWords.Length) : 0u,
                VoxelDeltaBytes = voxelDeltaSnapshot.IsCreated ? (uint)math.max(0, voxelDeltaSnapshot.Length) : 0u,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId
            };

            void* stagingPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_saveStagingBuffer);
            UnsafeUtility.MemClear(stagingPtr, UnsafeUtility.SizeOf<SaveStagingHeader>());
            UnsafeUtility.CopyStructureToPtr(ref header, stagingPtr);
        }

        private void RecordAsyncPersistenceTelemetry(
            uint operationId,
            string slotName,
            long durationMs,
            long compressedSizeBytes,
            int rawPayloadBytes,
            int screenshotBytes,
            uint flags)
        {
            if (!_saveTelemetryRing.IsCreated)
                return;

            uint slotHash = ComputeSlotHash(slotName);
            int index = _saveTelemetryWriteIndex;
            _saveTelemetryRing[index] = new AsyncPersistenceTelemetryEntry
            {
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                OperationId = operationId,
                SaveDurationMs = SaturateToUInt(durationMs),
                CompressedSizeBytes = SaturateToUInt(compressedSizeBytes),
                RawPayloadBytes = SaturateToUInt(rawPayloadBytes),
                Flags = flags,
                SlotHash = slotHash,
                Reserved = SaturateToUInt(BytesToKilobytesCeil(screenshotBytes))
            };

            index++;
            if (index >= SaveTelemetryCapacity)
                index = 0;
            _saveTelemetryWriteIndex = index;

            float durationScalar = durationMs > 0L ? (float)math.min(durationMs, int.MaxValue) : 0f;
            float compressedScalar = compressedSizeBytes > 0L ? (float)math.min(compressedSizeBytes, int.MaxValue) : 0f;
            float screenshotScalar = screenshotBytes > 0 ? BytesToKilobytesCeil(screenshotBytes) : 0f;
            GlobalTelemetryBus.PublishPerformanceWarning(SaveDurationTelemetryHash, slotHash, durationScalar);
            GlobalTelemetryBus.PublishPerformanceWarning(SaveCompressedSizeTelemetryHash, slotHash, compressedScalar);
            GlobalTelemetryBus.PublishPerformanceWarning(ScreenshotSizeKbTelemetryHash, slotHash, screenshotScalar);
        }

        private static int BytesToKilobytesCeil(int bytes)
        {
            if (bytes <= 0)
                return 0;

            long kilobytes = ((long)bytes + 1023L) >> 10;
            return kilobytes > int.MaxValue ? int.MaxValue : (int)kilobytes;
        }

        private void RecordWfcOutpostFrameBlackBox(
            uint operation,
            WfcOutpostPersistenceStatus status,
            ulong sectorHash,
            ulong payloadHash = 0UL,
            int payloadBytes = 0,
            int cellIndex = -1,
            uint previousFlags = 0u,
            uint currentFlags = 0u,
            uint signalSourceHash = 0u,
            uint flags = 0u,
            uint frame = 0u)
        {
            if (!_wfcOutpostTelemetryRing.IsCreated)
                return;

            int index = _wfcOutpostTelemetryWriteIndex;
            _wfcOutpostTelemetryRing[index] = BuildWfcOutpostTelemetryEntry(
                operation,
                status,
                sectorHash,
                payloadHash,
                payloadBytes,
                cellIndex,
                previousFlags,
                currentFlags,
                signalSourceHash,
                flags,
                frame);

            index++;
            if (index >= WfcOutpostTelemetryCapacity)
                index = 0;

            _wfcOutpostTelemetryWriteIndex = index;
        }

        private uint BuildWfcOutpostFrameBlackBoxFlags()
        {
            uint flags = 0u;
            if (_hasLastWfcOutpostSnapshot)
                flags |= 1u << 0;
            if (_wfcOutpostDependenciesReady)
                flags |= 1u << 1;
            if (IsWfcOutpostGridHandleCreated(in _wfcOutpostGridHandle))
                flags |= 1u << 2;
            if (_macroDatabaseService != null && _macroDatabaseService.IsOpen)
                flags |= 1u << 3;
            if (_dataVault != null)
                flags |= 1u << 4;

            return flags;
        }

        private void RecordWfcOutpostEventBlackBox(
            uint operation,
            WfcOutpostPersistenceStatus status,
            ulong sectorHash,
            ulong payloadHash = 0UL,
            int payloadBytes = 0,
            int cellIndex = -1,
            uint previousFlags = 0u,
            uint currentFlags = 0u,
            uint signalSourceHash = 0u,
            uint flags = 0u,
            uint frame = 0u)
        {
            if (!_wfcOutpostEventTelemetryRing.IsCreated)
                return;

            int index = _wfcOutpostEventTelemetryWriteIndex;
            _wfcOutpostEventTelemetryRing[index] = BuildWfcOutpostTelemetryEntry(
                operation,
                status,
                sectorHash,
                payloadHash,
                payloadBytes,
                cellIndex,
                previousFlags,
                currentFlags,
                signalSourceHash,
                flags,
                frame);

            index++;
            if (index >= WfcOutpostEventTelemetryCapacity)
                index = 0;

            _wfcOutpostEventTelemetryWriteIndex = index;
        }

        private WfcOutpostTelemetryEntry BuildWfcOutpostTelemetryEntry(
            uint operation,
            WfcOutpostPersistenceStatus status,
            ulong sectorHash,
            ulong payloadHash,
            int payloadBytes,
            int cellIndex,
            uint previousFlags,
            uint currentFlags,
            uint signalSourceHash,
            uint flags,
            uint frame)
        {
            uint resolvedFrame = frame != 0u ? frame : Hecton8.Core.SystemDispatcher.CurrentFrameId;
            return new WfcOutpostTelemetryEntry
            {
                Frame = resolvedFrame,
                Operation = operation,
                Status = (uint)status,
                Flags = flags,
                SectorHash = sectorHash,
                PayloadHash = payloadHash,
                GridSectorHash = _wfcOutpostMutableGridSectorHash,
                PayloadBytes = payloadBytes > 0 ? (uint)payloadBytes : 0u,
                CellIndex = cellIndex >= 0 ? (uint)cellIndex : uint.MaxValue,
                PreviousFlags = previousFlags,
                CurrentFlags = currentFlags,
                SignalSourceHash = signalSourceHash,
                Reserved0 = 0u
            };
        }

        private unsafe void DumpWfcOutpostBlackBox()
        {
            if (!_wfcOutpostTelemetryRing.IsCreated ||
                !_wfcOutpostEventTelemetryRing.IsCreated ||
                _wfcOutpostBlackBoxDumped)
            {
                return;
            }

            NativeArray<byte> dumpBytes = default;
            try
            {
                int entrySize = UnsafeUtility.SizeOf<WfcOutpostTelemetryEntry>();
                int dumpBytesLength =
                    28 +
                    ((WfcOutpostTelemetryCapacity + WfcOutpostEventTelemetryCapacity) * entrySize);
                dumpBytes = CreateTransientNativeArray<byte>(
                    dumpBytesLength,
                    Allocator.Temp,
                    NativeArrayOptions.UninitializedMemory,
                    "wfcOutpostBlackBoxDumpBytes");

                byte* dumpPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(dumpBytes);
                int cursor = 0;
                WriteUInt32LittleEndian(dumpPtr, ref cursor, WfcOutpostBlackBoxMagic);
                WriteUInt32LittleEndian(dumpPtr, ref cursor, WfcOutpostBlackBoxVersion);
                WriteUInt32LittleEndian(dumpPtr, ref cursor, WfcOutpostTelemetryCapacity);
                WriteUInt32LittleEndian(dumpPtr, ref cursor, WfcOutpostEventTelemetryCapacity);
                WriteUInt32LittleEndian(dumpPtr, ref cursor, (uint)entrySize);
                WriteUInt32LittleEndian(dumpPtr, ref cursor, (uint)_wfcOutpostTelemetryWriteIndex);
                WriteUInt32LittleEndian(dumpPtr, ref cursor, (uint)_wfcOutpostEventTelemetryWriteIndex);

                for (int i = 0; i < WfcOutpostTelemetryCapacity; i++)
                {
                    int index = (_wfcOutpostTelemetryWriteIndex + i) % WfcOutpostTelemetryCapacity;
                    WfcOutpostTelemetryEntry entry = _wfcOutpostTelemetryRing[index];
                    WriteWfcOutpostTelemetryEntry(dumpPtr, ref cursor, in entry);
                }

                for (int i = 0; i < WfcOutpostEventTelemetryCapacity; i++)
                {
                    int index = (_wfcOutpostEventTelemetryWriteIndex + i) % WfcOutpostEventTelemetryCapacity;
                    WfcOutpostTelemetryEntry entry = _wfcOutpostEventTelemetryRing[index];
                    WriteWfcOutpostTelemetryEntry(dumpPtr, ref cursor, in entry);
                }

                if (NativeFaultDumpWriter.TryWriteAll(WfcOutpostBlackBoxDumpRelativePath, dumpBytes, cursor))
                    _wfcOutpostBlackBoxDumped = true;
                else
                    LogWarning("[SaveManager] WFC outpost black box dump failed.");
            }
            catch (Exception)
            {
                LogWarning("[SaveManager] WFC outpost black box dump failed.");
            }
            finally
            {
                DisposeTransientNativeArray(ref dumpBytes, sentinelLabel: "wfcOutpostBlackBoxDumpBytes");
            }
        }

        private static unsafe void WriteWfcOutpostTelemetryEntry(byte* destination, ref int cursor, in WfcOutpostTelemetryEntry entry)
        {
            WriteUInt32LittleEndian(destination, ref cursor, entry.Frame);
            WriteUInt32LittleEndian(destination, ref cursor, entry.Operation);
            WriteUInt32LittleEndian(destination, ref cursor, entry.Status);
            WriteUInt32LittleEndian(destination, ref cursor, entry.Flags);
            WriteUInt64LittleEndian(destination, ref cursor, entry.SectorHash);
            WriteUInt64LittleEndian(destination, ref cursor, entry.PayloadHash);
            WriteUInt64LittleEndian(destination, ref cursor, entry.GridSectorHash);
            WriteUInt32LittleEndian(destination, ref cursor, entry.PayloadBytes);
            WriteUInt32LittleEndian(destination, ref cursor, entry.CellIndex);
            WriteUInt32LittleEndian(destination, ref cursor, entry.PreviousFlags);
            WriteUInt32LittleEndian(destination, ref cursor, entry.CurrentFlags);
            WriteUInt32LittleEndian(destination, ref cursor, entry.SignalSourceHash);
            WriteUInt32LittleEndian(destination, ref cursor, entry.Reserved0);
        }

        private unsafe void DumpSaveBlackBox()
        {
            if (!_saveTelemetryRing.IsCreated)
                return;

            NativeArray<byte> dumpBytes = default;
            try
            {
                const int headerBytes = 12;
                int entrySize = UnsafeUtility.SizeOf<AsyncPersistenceTelemetryEntry>();
                int dumpBytesLength = headerBytes + (SaveTelemetryCapacity * entrySize);
                dumpBytes = CreateTransientNativeArray<byte>(
                    dumpBytesLength,
                    Allocator.Temp,
                    NativeArrayOptions.UninitializedMemory,
                    "asyncPersistenceBlackBoxDumpBytes");

                byte* dumpPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(dumpBytes);
                int cursor = 0;
                WriteUInt32LittleEndian(dumpPtr, ref cursor, 0x48384153u); // H8AS
                WriteUInt32LittleEndian(dumpPtr, ref cursor, SaveTelemetryCapacity);
                WriteUInt32LittleEndian(dumpPtr, ref cursor, (uint)entrySize);

                for (int i = 0; i < SaveTelemetryCapacity; i++)
                {
                    int index = (_saveTelemetryWriteIndex + i) % SaveTelemetryCapacity;
                    AsyncPersistenceTelemetryEntry entry = _saveTelemetryRing[index];
                    WriteAsyncPersistenceTelemetryEntry(dumpPtr, ref cursor, in entry);
                }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                TestHook_DumpSaveBlackBox?.Invoke();
#endif
                if (!NativeFaultDumpWriter.TryWriteAll(AsyncPersistenceBlackBoxDumpRelativePath, dumpBytes, cursor))
                    LogWarning("[SaveManager] Save black box dump failed.");
            }
            catch (Exception)
            {
                LogWarning("[SaveManager] Save black box dump failed.");
            }
            finally
            {
                DisposeTransientNativeArray(ref dumpBytes, sentinelLabel: "asyncPersistenceBlackBoxDumpBytes");
            }
        }

        private static unsafe void WriteAsyncPersistenceTelemetryEntry(byte* destination, ref int cursor, in AsyncPersistenceTelemetryEntry entry)
        {
            WriteUInt32LittleEndian(destination, ref cursor, entry.Frame);
            WriteUInt32LittleEndian(destination, ref cursor, entry.OperationId);
            WriteUInt32LittleEndian(destination, ref cursor, entry.SaveDurationMs);
            WriteUInt32LittleEndian(destination, ref cursor, entry.CompressedSizeBytes);
            WriteUInt32LittleEndian(destination, ref cursor, entry.RawPayloadBytes);
            WriteUInt32LittleEndian(destination, ref cursor, entry.Flags);
            WriteUInt32LittleEndian(destination, ref cursor, entry.SlotHash);
            WriteUInt32LittleEndian(destination, ref cursor, entry.Reserved);
            WriteUInt32LittleEndian(destination, ref cursor, 0u);
            WriteUInt32LittleEndian(destination, ref cursor, 0u);
            WriteUInt32LittleEndian(destination, ref cursor, 0u);
            WriteUInt32LittleEndian(destination, ref cursor, 0u);
            WriteUInt32LittleEndian(destination, ref cursor, 0u);
            WriteUInt32LittleEndian(destination, ref cursor, 0u);
            WriteUInt32LittleEndian(destination, ref cursor, 0u);
            WriteUInt32LittleEndian(destination, ref cursor, 0u);
        }

        private static unsafe void WriteUInt32LittleEndian(byte* destination, ref int cursor, int value)
        {
            WriteUInt32LittleEndian(destination, ref cursor, (uint)value);
        }

        private static unsafe void WriteUInt32LittleEndian(byte* destination, ref int cursor, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(new Span<byte>(destination + cursor, sizeof(uint)), value);
            cursor += sizeof(uint);
        }

        private static unsafe void WriteUInt64LittleEndian(byte* destination, ref int cursor, ulong value)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(new Span<byte>(destination + cursor, sizeof(ulong)), value);
            cursor += sizeof(ulong);
        }

        private static void PublishSaveRecoveredNotification(string slotName)
        {
            HUDNotificationSignal notification = new HUDNotificationSignal
            {
                MessageHash = SaveRecoveredMessageHash,
                ContextHash = ComputeSlotHash(slotName) ^ SaveRecoveredContextHash,
                SourceId = AsyncPersistenceSourceHash,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                Severity = 1,
                Flags = 0
            };
            TryPushSignalTrackedBestEffort(in notification);
        }

        private static void PublishSaveSynchronizedNotification(string slotName)
        {
            HUDNotificationSignal notification = new HUDNotificationSignal
            {
                MessageHash = SaveSynchronizedMessageHash,
                ContextHash = ComputeSlotHash(slotName),
                SourceId = AsyncPersistenceSourceHash,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                Severity = 0,
                Flags = 0
            };
            TryPushSignalTrackedBestEffort(in notification);
        }

        private static void RaiseSaveCompletedWithBackpressureRecovery(uint slotHash)
        {
            if (SaveEvents.TryRaiseSaveCompleted(slotHash))
                return;

            SaveEvents.FlushPending();
            SaveEvents.TryRaiseSaveCompleted(slotHash);
        }

        private static void RaiseLoadCompletedWithBackpressureRecovery(uint slotHash)
        {
            if (SaveEvents.TryRaiseLoadCompleted(slotHash))
                return;

            SaveEvents.FlushPending();
            SaveEvents.TryRaiseLoadCompleted(slotHash);
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

        private static void RegisterTransientNativeArray<T>(NativeArray<T> array, string label) where T : struct
        {
            if (!array.IsCreated)
                return;

            int registrationId = NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeTransientMemoryLifetime);
            if (registrationId <= 0)
                throw new InvalidOperationException(NativeMemoryTransientRegistrationFailureMessage);
        }

        private static NativeArray<T> CreateTransientNativeArray<T>(
            int length,
            Allocator allocator,
            NativeArrayOptions options,
            string sentinelLabel) where T : struct
        {
            NativeArray<T> array = default;
            try
            {
                array = new NativeArray<T>(length, allocator, options);
                RegisterTransientNativeArray(array, sentinelLabel);
                return array;
            }
            catch (Exception exception)
            {
                Exception cleanupException = null;
                DisposeNativeArrayBestEffort(ref array, ref cleanupException, sentinelLabel: sentinelLabel);
                if (cleanupException != null)
                    throw new AggregateException(
                        "Transient SaveManager NativeArray creation failed and cleanup also failed.",
                        exception,
                        cleanupException);

                throw;
            }
        }

#if UNITY_EDITOR
        internal static Action s_testHookCreatePersistentNativeArrayException;
        internal static Action s_testHookDisposeNativeArrayException;
#endif

        private static NativeArray<T> CreatePersistentNativeArray<T>(
            int length,
            NativeArrayOptions options,
            string sentinelLabel) where T : struct
        {
            NativeArray<T> array = default;
            try
            {
                array = new NativeArray<T>(length, Allocator.Persistent, options);
#if UNITY_EDITOR
                s_testHookCreatePersistentNativeArrayException?.Invoke();
#endif
                int registrationId = NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, sentinelLabel, NativeMemoryLifetime);
                if (registrationId <= 0)
                    throw new InvalidOperationException(NativeMemoryRegistrationFailureMessage);

                return array;
            }
            catch (Exception exception)
            {
                Exception cleanupException = null;
#if UNITY_EDITOR
                if (TestHookSimulateCleanupFailure != null)
                {
                    try { TestHookSimulateCleanupFailure(); }
                    catch (Exception hookEx) { cleanupException = hookEx; }
                }
#endif
                DisposeNativeArrayBestEffort(ref array, ref cleanupException, sentinelLabel: sentinelLabel);
                if (cleanupException != null)
                    throw new AggregateException(
                        "Persistent SaveManager NativeArray creation failed and cleanup also failed.",
                        exception,
                        cleanupException);

                throw;
            }
        }

        private static void DisposeTransientNativeArray<T>(
            ref NativeArray<T> array,
            JobHandle dependency = default,
            bool deferDisposal = false,
            string sentinelLabel = null) where T : struct
        {
            DisposeNativeArray(ref array, dependency, deferDisposal);
        }

#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
        internal static System.Action DisposeNativeArrayTestHook;
        internal static System.Action s_testHookDisposeNativeArrayImmediateSentinelUnregisterError;
#endif

        private static void DisposeTransientNativeArrayBestEffort<T>(
            ref NativeArray<T> array,
            ref Exception firstException,
            JobHandle dependency = default,
            bool deferDisposal = false,
            string sentinelLabel = null) where T : struct
        {
            try
            {
#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
                DisposeNativeArrayTestHook?.Invoke();
#endif
                DisposeNativeArray(ref array, dependency, deferDisposal);
            }
            catch (Exception exception)
            {
                CaptureFirstCleanupException(ref firstException, exception);
            }
        }

        private static void DisposeTransientNativeArrayBestEffortAndReport<T>(
            ref NativeArray<T> array,
            string operationName,
            string sentinelLabel) where T : struct
        {
            Exception cleanupException = null;
            DisposeTransientNativeArrayBestEffort(ref array, ref cleanupException, sentinelLabel: sentinelLabel);
            ReportPersistenceCleanupFailure(operationName, cleanupException);
        }

        private static unsafe void DisposeNativeArray<T>(
            ref NativeArray<T> array,
            JobHandle dependency = default,
            bool deferDisposal = false) where T : struct
        {
            if (!array.IsCreated)
                return;

            void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);
            Exception firstException = null;

            if (deferDisposal)
            {
                JobHandle disposeHandle = array.Dispose(dependency);
                if (!DispatcherJobFence.TryComplete(ref disposeHandle, forceComplete: true))
                    throw new InvalidOperationException("SaveManager native array disposal did not complete before sentinel unregister.");

                try
                {
                    NativeMemorySentinel.UnregisterPointer(trackedPointer);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }
            }
            else
            {
                try
                {
#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
                    s_testHookDisposeNativeArrayImmediateSentinelUnregisterError?.Invoke();
#endif
                    NativeMemorySentinel.UnregisterPointer(trackedPointer);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }

                try
                {
                    array.Dispose();
                }
                catch (Exception exception)
                {
                    if (firstException == null)
                        firstException = exception;
                }
            }

            array = default;

#if UNITY_EDITOR
            s_testHookDisposeNativeArrayException?.Invoke();
#endif

            if (firstException != null)
                throw firstException;
        }

        internal static Action<Exception> s_TestDisposeNativeArrayBestEffortHook;

        private static void DisposeNativeArrayBestEffort<T>(
            ref NativeArray<T> array,
            ref Exception firstException,
            JobHandle dependency = default,
            bool deferDisposal = false,
            string sentinelLabel = null) where T : struct
        {
            try
            {
                if (s_TestDisposeNativeArrayBestEffortHook != null)
                {
                    s_TestDisposeNativeArrayBestEffortHook(firstException);
                }
                DisposeNativeArray(ref array, dependency, deferDisposal);
            }
            catch (Exception exception)
            {
                if (firstException == null)
                    firstException = exception;
            }
        }

        private static void ReleaseSnapshotPauseBestEffort(uint operationId, ref Exception firstException)
        {
            try
            {
                ReleaseSnapshotPause(operationId);
            }
            catch (Exception exception)
            {
                CaptureFirstCleanupException(ref firstException, exception);
            }
        }

        private static void ReleaseBorrowedVoxelDeltaSnapshotBestEffort(
            VoxelDeltaProcessor owner,
            ref Exception firstException)
        {
            if (owner == null)
                return;

            try
            {
                owner.ReleaseBorrowedNativeSnapshotScratch();
            }
            catch (Exception exception)
            {
                CaptureFirstCleanupException(ref firstException, exception);
            }
        }

        private void NotifyMacroDatabasePersistenceGateBestEffort(bool blocked, ref Exception firstException)
        {
            try
            {
                NotifyMacroDatabasePersistenceGate(blocked);
            }
            catch (Exception exception)
            {
                CaptureFirstCleanupException(ref firstException, exception);
            }
        }

        private void BlockMacroDatabaseCompactionForActivePersistence()
        {
            Exception gateException = null;
            NotifyMacroDatabasePersistenceGateBestEffort(true, ref gateException);
            ReportPersistenceCleanupFailure("gate", gateException);
        }

        private static void CaptureFirstCleanupException(ref Exception firstException, Exception exception)
        {
            if (firstException == null)
                firstException = exception;
        }

        private static void ReportPersistenceCleanupFailure(string operationName, Exception exception)
        {
            if (exception == null)
                return;

            uint contextHash = string.Equals(operationName, "load", StringComparison.Ordinal)
                ? PersistenceCleanupLoadContextHash
                : string.Equals(operationName, "gate", StringComparison.Ordinal)
                    ? PersistenceCleanupGateContextHash
                    : PersistenceCleanupSaveContextHash;
            try
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    PersistenceCleanupFailureTelemetryHash,
                    contextHash,
                    1f);
            }
            catch (Exception telemetryException)
            {
                try
                {
                    LogError("[SaveManager] cleanup telemetry failed: " + telemetryException);
                }
                catch
                {
                }
            }

            try
            {
                LogError("[SaveManager] " + operationName + " cleanup failed: " + exception);
            }
            catch
            {
            }
        }

        private static void ReportPersistenceSignalBridgeFailure(Exception exception)
        {
            if (exception == null)
                return;

            PublishPerformanceWarningBestEffort(
                PersistenceCleanupFailureTelemetryHash,
                PersistenceSignalBridgeContextHash,
                1f);
            LogErrorBestEffort("[SaveManager] signal bridge failed: " + exception);
        }

#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
        internal static Action TestHook_PublishSaveCompleted_BeforePush;
#endif
        private static void TryPushSignalTrackedBestEffort<TSignal>(in TSignal signal)
            where TSignal : unmanaged, ISignal
        {
            try
            {
                SignalBus<TSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
            }
            catch (Exception exception)
            {
                ReportPersistenceSignalBridgeFailure(exception);
            }
        }

        private static void PublishPerformanceWarningBestEffort(uint warningHash, uint contextHash, float value)
        {
            try
            {
                GlobalTelemetryBus.PublishPerformanceWarning(warningHash, contextHash, value);
            }
            catch (Exception telemetryException)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LogErrorBestEffort("[SaveManager] performance warning telemetry failed: " + telemetryException);
#endif
            }
        }

        private static void LogErrorBestEffort(string message)
        {
            try
            {
                LogError(message);
            }
            catch
            {
            }
        }

        private static void LogWarningBestEffort(string message)
        {
            try
            {
                LogWarning(message);
            }
            catch
            {
            }
        }

        private static void ThrowFirstDisposeException(Exception firstException)
        {
            if (firstException != null)
                throw firstException;
        }

        public void Register(ISaveable saveable)
        {
            if (_runtimeOwnerAborted || !_serviceRegistered || !IsAlive(saveable)) return;

            PruneDeadSaveables();

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
                if (!_saveableCapacityWarningLogged)
                {
                    PublishPerformanceWarningBestEffort(
                        SaveableRegistryOverflowTelemetryHash,
                        AsyncPersistenceSourceHash,
                        MaxRegisteredSaveables);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    LogErrorBestEffort("[SaveManager] Saveable registry capacity exceeded. Increase MaxRegisteredSaveables or split save ownership.");
#endif
                    _saveableCapacityWarningLogged = true;
                }

                _debugRegisteredCount = _saveableCount;
                return;
            }

            _saveables[_saveableCount] = saveable;
            _saveableCount++;
            _registryDirty = true;
            _debugRegisteredCount = _saveableCount;

            // Flag only. The caller is mid-OnEnable, so the retained payload is handed over on the
            // dispatcher's Core update lane instead of re-entering the owner's own enable path.
            if (_pendingOwnerHydrationData != null)
                _pendingOwnerHydrationDrainRequested = true;
        }

        public void Unregister(ISaveable saveable)
        {
            if (_runtimeOwnerAborted || saveable == null) return;

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

        private void ReportLoadPriorityConflictsForLoad(string slotName)
        {
            int conflictCount = 0;
            int readIndex = 0;
            while (readIndex < _saveableCount)
            {
                ISaveable first = _saveables[readIndex];
                if (!IsAlive(first))
                {
                    readIndex++;
                    continue;
                }

                int priority = first.LoadPriority;
                Type ownerType = first.GetType();
                int groupCount = 1;
                int probeIndex = readIndex + 1;
                while (probeIndex < _saveableCount)
                {
                    ISaveable candidate = _saveables[probeIndex];
                    if (!IsAlive(candidate))
                    {
                        probeIndex++;
                        continue;
                    }

                    if (candidate.LoadPriority != priority)
                        break;

                    Type candidateType = candidate.GetType();
                    if (ReferenceEquals(candidateType, ownerType))
                    {
                        groupCount++;
                    }
                    else
                    {
                        if (groupCount > 1)
                        {
                            conflictCount += groupCount;
                            uint contextHash = BuildLoadPriorityConflictContextHash(slotName, priority, groupCount, ownerType);
                            PublishPerformanceWarningBestEffort(LoadPriorityConflictTelemetryHash, contextHash, groupCount);
                        }

                        ownerType = candidateType;
                        groupCount = 1;
                    }

                    probeIndex++;
                }

                if (groupCount > 1)
                {
                    conflictCount += groupCount;
                    uint contextHash = BuildLoadPriorityConflictContextHash(slotName, priority, groupCount, ownerType);
                    PublishPerformanceWarningBestEffort(LoadPriorityConflictTelemetryHash, contextHash, groupCount);
                }

                readIndex = probeIndex;
            }

            _lastLoadPriorityConflictCount = conflictCount;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (conflictCount > 0)
            {
                int frame = Time.frameCount;
                if (_lastLoadPriorityConflictFrame != frame)
                {
                    _lastLoadPriorityConflictFrame = frame;
                    LogWarningBestEffort("[SaveManager] Duplicate ISaveable owner type detected before load. Keep one runtime owner per persisted state section.");
                }
            }
#endif
        }

        private static uint BuildLoadPriorityConflictContextHash(string slotName, int priority, int groupCount, Type ownerType)
        {
            unchecked
            {
                uint hash = LoadPriorityConflictContextSeedHash ^ ComputeSlotHash(slotName);
                hash = (hash * 16777619u) ^ (uint)priority;
                hash = (hash * 16777619u) ^ (uint)groupCount;
                string ownerName = ownerType != null ? ownerType.FullName : null;
                if (!string.IsNullOrEmpty(ownerName))
                {
                    for (int i = 0; i < ownerName.Length; i++)
                        hash = (hash * 16777619u) ^ ownerName[i];
                }

                return hash == 0u ? LoadPriorityConflictContextSeedHash : hash;
            }
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

        // ----------------------------------------------------------
        //  REQUIRED-OWNER CENSUS AND DEFERRED HYDRATION
        // ----------------------------------------------------------

        /// <summary>
        /// Maps one live registrant onto the First-20 save/load contract categories it authoritatively
        /// owns (FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md:88 - position, inventory, route state,
        /// opened/looted/scanned flags, hazard state). Cold path: one pass per save and per load over at
        /// most <see cref="MaxRegisteredSaveables"/> entries, never inside a tick, job, or solver loop.
        /// </summary>
        private static uint ClassifySaveOwnerCategories(ISaveable saveable)
        {
            uint categories = 0u;
            if (saveable is HectonSurvivalSystem)
                categories |= SaveOwnerCensus.CategoryPlayerPosition;

            if (saveable is PlayerInventory)
                categories |= SaveOwnerCensus.CategoryInventory;

            if (saveable is FirstHourDirector)
                categories |= SaveOwnerCensus.CategoryRouteState;

            if (saveable is WorldStateManager ||
                saveable is HectonDiscoveryManager ||
                saveable is ScanLogSystem)
            {
                categories |= SaveOwnerCensus.CategoryWorldObjectFlags;
            }

            if (saveable is HazardZoneManager || saveable is RadiationHazardGrid)
                categories |= SaveOwnerCensus.CategoryHazardState;

            return categories;
        }

        private uint CollectRegisteredOwnerCategories(out int liveOwnerCount)
        {
            uint categories = 0u;
            int liveOwners = 0;
            for (int i = 0; i < _saveableCount; i++)
            {
                ISaveable saveable = _saveables[i];
                if (!IsAlive(saveable))
                    continue;

                liveOwners++;
                categories |= ClassifySaveOwnerCategories(saveable);
            }

            liveOwnerCount = liveOwners;
            return categories;
        }

        /// <summary>
        /// Runs the required-owner census and surfaces the verdict on routes that survive a RELEASE
        /// player build: the unconditional <see cref="GlobalTelemetryBus"/>, the async-persistence
        /// black-box ring, and the public read-only projections on this service.
        /// <see cref="LogInfo"/>/<see cref="LogWarning"/>/<see cref="LogError"/> here are
        /// <c>[Conditional]</c> on UNITY_EDITOR/DEVELOPMENT_BUILD, so a log line alone would leave a
        /// shipped build exactly as silent as the defect this census exists to expose. An empty registry
        /// counts as a failure: the apply loop over zero entries emits nothing at all.
        /// </summary>
        private void ReportSaveOwnerCensus(string slotName, uint operationId, bool isLoadOperation)
        {
            uint presentCategories = CollectRegisteredOwnerCategories(out int liveOwnerCount);
            uint missingCategories = SaveOwnerCensus.ResolveMissingCategories(presentCategories);
            uint slotHash = ComputeSlotHash(slotName);

            if (isLoadOperation)
            {
                LastLoadMissingOwnerCategories = missingCategories;
                LastLoadLiveOwnerCount = liveOwnerCount;
            }
            else
            {
                LastSaveMissingOwnerCategories = missingCategories;
            }

            if (SaveOwnerCensus.IsCensusSatisfied(presentCategories, liveOwnerCount))
                return;

            uint seedHash = isLoadOperation
                ? SaveOwnerCensusLoadContextSeedHash
                : SaveOwnerCensusSaveContextSeedHash;
            uint contextHash = SaveOwnerCensus.ComputeCensusContextHash(
                seedHash,
                slotHash,
                missingCategories,
                liveOwnerCount);
            PublishPerformanceWarningBestEffort(
                SaveOwnerCensusTelemetryHash,
                contextHash,
                SaveOwnerCensus.ResolveCensusCoverage01(presentCategories));

            uint blackBoxFlags = AsyncPersistenceOwnerCensusFailureFlag |
                                 (isLoadOperation ? AsyncPersistenceLoadOperationFlag : 0u);
            RecordSaveOwnerCensusBlackBox(
                operationId,
                slotHash,
                missingCategories,
                liveOwnerCount,
                blackBoxFlags);
            LogSaveOwnerCensusFailure(slotName, missingCategories, liveOwnerCount, isLoadOperation);
        }

        /// <summary>
        /// Writes one census verdict into the existing async-persistence black-box ring so it lands in
        /// <c>Docs/AgentLogs/Dump_SAVE_MANAGER_ASYNC_PERSISTENCE.bin</c> alongside the save timings.
        /// The 64-byte <see cref="AsyncPersistenceTelemetryEntry"/> ABI is unchanged; the numeric slots
        /// are re-read against the census flags as:
        /// <c>CompressedSizeBytes</c> = outstanding category mask,
        /// <c>RawPayloadBytes</c> = live registrant count (or owners hydrated, on a deferred entry),
        /// <c>Reserved</c> = outstanding category count.
        /// </summary>
        private void RecordSaveOwnerCensusBlackBox(
            uint operationId,
            uint slotHash,
            uint categoryMask,
            int ownerCount,
            uint flags)
        {
            if (!_saveTelemetryRing.IsCreated)
                return;

            int index = _saveTelemetryWriteIndex;
            _saveTelemetryRing[index] = new AsyncPersistenceTelemetryEntry
            {
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                OperationId = operationId,
                SaveDurationMs = 0u,
                CompressedSizeBytes = categoryMask & SaveOwnerCensus.RequiredCategoryMask,
                RawPayloadBytes = ownerCount > 0 ? (uint)ownerCount : 0u,
                Flags = flags,
                SlotHash = slotHash,
                Reserved = (uint)SaveOwnerCensus.CountCategories(categoryMask)
            };

            index++;
            if (index >= SaveTelemetryCapacity)
                index = 0;
            _saveTelemetryWriteIndex = index;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogSaveOwnerCensusFailure(
            string slotName,
            uint missingCategories,
            int liveOwnerCount,
            bool isLoadOperation)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogErrorBestEffort(
                "[SaveManager] Required save-owner census FAILED for '" + slotName + "' during " +
                (isLoadOperation ? "load" : "save") + ": " + liveOwnerCount.ToString() +
                " live registrant(s), missing categories: " + DescribeCategoryMask(missingCategories) +
                ". Each missing category's payload section is applied to nothing.");
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogDeferredOwnerHydrationExpiry(uint outstandingCategories)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogErrorBestEffort(
                "[SaveManager] Deferred owner hydration window expired with categories still unowned: " +
                DescribeCategoryMask(outstandingCategories) +
                ". The loaded payload for those categories was never applied to a runtime owner.");
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static string DescribeCategoryMask(uint categoryMask)
        {
            string description = string.Empty;
            for (int i = 0; i < SaveOwnerCensus.RequiredCategoryCount; i++)
            {
                uint category = SaveOwnerCensus.ResolveCategoryAtIndex(i);
                if ((categoryMask & category) == 0u)
                    continue;

                string label = SaveOwnerCensus.DescribeCategory(category);
                description = description.Length == 0 ? label : description + ", " + label;
            }

            return description.Length == 0 ? "none" : description;
        }
#endif

        /// <summary>
        /// Retains the loaded payload for the categories no live owner consumed and arms the
        /// registration-driven drain.
        ///
        /// This is deliberately NOT a bootstrap reorder. GameBootstrapper Step 6
        /// <c>WaitForGroundReadyAsync</c> reads the player transform expecting the restored value, and
        /// <c>_isLoadingSave</c> gates <c>SpawnPlayerAsync</c>, so load must keep preceding spawn.
        /// Reordering would invert that dependency instead of fixing it; deferring the apply to the
        /// owners' own re-registration leaves the bootstrap order intact.
        /// </summary>
        private void ArmDeferredOwnerHydration(
            SaveData data,
            uint slotHash,
            uint operationId,
            uint missingCategories)
        {
            uint outstanding = missingCategories & SaveOwnerCensus.RequiredCategoryMask;
            if (data == null || outstanding == 0u)
            {
                ClearPendingOwnerHydration();
                return;
            }

            _pendingOwnerHydrationData = data;
            _pendingOwnerHydrationMissingCategories = outstanding;
            _pendingOwnerHydrationSlotHash = slotHash;
            _pendingOwnerHydrationOperationId = operationId;
            _pendingOwnerHydrationDeadlineSeconds =
                Hecton8.Core.SystemDispatcher.CurrentUnscaledTimeSeconds + DeferredOwnerHydrationWindowSeconds;
            _pendingOwnerHydrationDrainRequested = true;
        }

        private void ClearPendingOwnerHydration()
        {
            _pendingOwnerHydrationData = null;
            _pendingOwnerHydrationMissingCategories = 0u;
            _pendingOwnerHydrationSlotHash = 0u;
            _pendingOwnerHydrationOperationId = 0u;
            _pendingOwnerHydrationDeadlineSeconds = 0d;
            _pendingOwnerHydrationDrainRequested = false;
        }

        /// <summary>
        /// Hands the retained payload to the owners that re-registered after the apply loop ran, then
        /// releases the payload. Runs on the Core <c>IUpdatable</c> dispatcher lane this service is
        /// already registered on - not a private <c>Update</c>. An owner is only re-applied when it
        /// carries a category that is still outstanding, so an owner that already consumed the payload
        /// in the apply loop is never overwritten by this pass.
        /// </summary>
        private void DrainPendingOwnerHydration()
        {
            SaveData pendingData = _pendingOwnerHydrationData;
            if (pendingData == null || _isBusy)
                return;

            if (_pendingOwnerHydrationDrainRequested)
            {
                _pendingOwnerHydrationDrainRequested = false;
                SortRegistryIfDirty(LoadPriorityComparer);

                uint outstanding = _pendingOwnerHydrationMissingCategories;
                int appliedThisPass = 0;
                for (int i = 0; i < _saveableCount && outstanding != 0u; i++)
                {
                    ISaveable saveable = _saveables[i];
                    if (!IsAlive(saveable) || saveable is VoxelDeltaProcessor)
                        continue;

                    uint satisfied = SaveOwnerCensus.ResolveSatisfiedCategories(
                        outstanding,
                        ClassifySaveOwnerCategories(saveable));
                    if (satisfied == 0u)
                        continue;

                    saveable.LoadFromSaveData(pendingData);
                    outstanding = SaveOwnerCensus.ClearSatisfiedCategories(outstanding, satisfied);
                    appliedThisPass++;
                }

                _pendingOwnerHydrationMissingCategories = outstanding;
                if (appliedThisPass > 0)
                {
                    DeferredOwnerHydrationAppliedCount += appliedThisPass;
                    PublishPerformanceWarningBestEffort(
                        DeferredOwnerHydrationTelemetryHash,
                        _pendingOwnerHydrationSlotHash,
                        appliedThisPass);
                    RecordSaveOwnerCensusBlackBox(
                        _pendingOwnerHydrationOperationId,
                        _pendingOwnerHydrationSlotHash,
                        outstanding,
                        appliedThisPass,
                        AsyncPersistenceDeferredHydrationAppliedFlag | AsyncPersistenceLoadOperationFlag);
                    LogInfo(
                        "[SaveManager] Deferred owner hydration applied the loaded payload to " +
                        appliedThisPass.ToString() + " owner(s) that re-registered after the load apply loop.");
                }

                if (outstanding == 0u)
                {
                    ClearPendingOwnerHydration();
                    return;
                }
            }

            if (!SaveOwnerCensus.IsDeferredHydrationExpired(
                    Hecton8.Core.SystemDispatcher.CurrentUnscaledTimeSeconds,
                    _pendingOwnerHydrationDeadlineSeconds))
            {
                return;
            }

            uint expiredCategories = _pendingOwnerHydrationMissingCategories;
            PublishPerformanceWarningBestEffort(
                DeferredOwnerHydrationExpiredTelemetryHash,
                SaveOwnerCensus.ComputeCensusContextHash(
                    SaveOwnerCensusLoadContextSeedHash,
                    _pendingOwnerHydrationSlotHash,
                    expiredCategories,
                    _saveableCount),
                SaveOwnerCensus.CountCategories(expiredCategories));
            RecordSaveOwnerCensusBlackBox(
                _pendingOwnerHydrationOperationId,
                _pendingOwnerHydrationSlotHash,
                expiredCategories,
                _saveableCount,
                AsyncPersistenceDeferredHydrationExpiredFlag | AsyncPersistenceLoadOperationFlag);
            LogDeferredOwnerHydrationExpiry(expiredCategories);
            ClearPendingOwnerHydration();
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

            int priorityComparison = a.SavePriority.CompareTo(b.SavePriority);
            return priorityComparison != 0 ? priorityComparison : CompareSaveableTypeName(a, b);
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

            int priorityComparison = a.LoadPriority.CompareTo(b.LoadPriority);
            return priorityComparison != 0 ? priorityComparison : CompareSaveableTypeName(a, b);
        }

        private static int CompareSaveableTypeName(ISaveable a, ISaveable b)
        {
            Type aType = a.GetType();
            Type bType = b.GetType();
            int typeComparison = string.CompareOrdinal(aType.FullName, bType.FullName);
            if (typeComparison != 0)
                return typeComparison;

            return string.CompareOrdinal(aType.AssemblyQualifiedName, bType.AssemblyQualifiedName);
        }

        private bool TryRejectSaveDuringRespawnReconciliation(
            byte slotIndex,
            uint operationId,
            string slotName,
            bool activeSaveStarted = false,
            long elapsedMs = 0L)
        {
            if (!HasPendingRespawnReconciliationSaveGate())
                return false;

            LastOperationError = RespawnReconciliationInProgressReason;
            LastOperationSlot = slotName ?? string.Empty;
            LogWarning($"[SaveManager] Ignored save request for '{LastOperationSlot}': {RespawnReconciliationInProgressReason}");
            SaveEvents.TryRaiseSaveFailed(
                ResolveSlotHash(slotIndex, slotName),
                SaveEvents.ComputeMessageHash(RespawnReconciliationInProgressReason),
                RespawnReconciliationInProgressReason);
            if (activeSaveStarted)
            {
                RecordAsyncPersistenceTelemetry(operationId, LastOperationSlot, elapsedMs, 0L, 0, 0, 1u);
                PublishSaveCompletedForSlotName(slotIndex, slotName, new PublishSaveCompletedArgs(operationId: operationId, durationMs: elapsedMs, compressedSizeBytes: 0L, succeeded: false));
                PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Failed, 1f, 1u);
            }
            else
            {
                PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Rejected, 0f, 1u);
            }

            return true;
        }

        private bool HasPendingRespawnReconciliationSaveGate()
        {
            for (int i = 0; i < _saveableCount; i++)
            {
                ISaveable saveable = _saveables[i];
                if (!IsAlive(saveable))
                    continue;

                if (saveable is HectonSurvivalSystem survival && survival.RespawnReconciliationPending)
                    return true;

                if (saveable is HectonPlayerHealth health && health.RespawnReconciliationPending)
                    return true;
            }

            return false;
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
            Hecton8.Core.H8Debug.Log(message);
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogWarning(string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogWarning(message);
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogError(string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogError(message);
#endif
        }

        // ----------------------------------------------------------
        //  ASYNC SAVE/LOAD
        // ----------------------------------------------------------

        public Awaitable SaveGameAsync(string slotName)
        {
            return SaveGameAsyncInternal(slotName, ResolveManualSlotIndex(slotName), ResolveOperationId(0u));
        }

        private bool TryPopulateSaveDataAndCaptureVoxelSnapshot(
            string slotName,
            byte slotIndex,
            uint operationId,
            SaveData data,
            long elapsedMilliseconds,
            ref NativeArray<byte> voxelDeltaSnapshot,
            ref bool ownsVoxelDeltaSnapshot,
            ref VoxelDeltaProcessor borrowedVoxelDeltaSnapshotOwner,
            ref bool snapshotPauseActive)
        {
            SortRegistryIfDirty(SavePriorityComparer);
            ReportSaveOwnerCensus(slotName, operationId, isLoadOperation: false);
            for (int i = 0; i < _saveableCount; i++)
            {
                ISaveable saveable = _saveables[i];
                if (!IsAlive(saveable))
                    continue;

                if (saveable is VoxelDeltaProcessor voxelDeltaProcessor)
                {
                    Exception cleanupException = null;
                    if (borrowedVoxelDeltaSnapshotOwner != null)
                    {
                        ReleaseBorrowedVoxelDeltaSnapshotBestEffort(borrowedVoxelDeltaSnapshotOwner, ref cleanupException);
                        borrowedVoxelDeltaSnapshotOwner = null;
                    }

                    if (voxelDeltaSnapshot.IsCreated && ownsVoxelDeltaSnapshot)
                        DisposeTransientNativeArrayBestEffort(ref voxelDeltaSnapshot, ref cleanupException, sentinelLabel: "voxelDeltaSnapshot");
                    else
                        voxelDeltaSnapshot = default;

                    ReportPersistenceCleanupFailure("save", cleanupException);
                    ownsVoxelDeltaSnapshot = false;
                    if (!voxelDeltaProcessor.TryCopyNativeSnapshotToBorrowedScratch(
                            out voxelDeltaSnapshot,
                            out int voxelDeltaSnapshotByteCount) ||
                        voxelDeltaSnapshotByteCount <= 0)
                    {
                        if (voxelDeltaSnapshotByteCount > 0)
                        {
                            const string reason = "Voxel delta native snapshot copy failed.";
                            const string logReason = "[SaveManager] Save failed: voxel delta native snapshot copy failed.";
                            const uint failureCode = 3u;
                            voxelDeltaSnapshot = default;
                            HandleSaveFailure(slotName, slotIndex, operationId, reason, logReason, failureCode, elapsedMilliseconds, ref cleanupException, ref snapshotPauseActive);
                            return false;
                        }

                        voxelDeltaSnapshot = default;
                    }
                    else
                    {
                        borrowedVoxelDeltaSnapshotOwner = voxelDeltaProcessor;
                    }
                }

                saveable.PopulateSaveData(data);
            }
            return true;
        }

        /// <summary>
        /// Maps a <see cref="PersistentWorldRegistry.LastSaveSnapshotFailureCode"/> discriminator onto a
        /// branch-distinct player/diag reason and a grep-token log line. Every output is a const string
        /// selected by a switch on a byte, so this stays allocation-free on the save staging cadence.
        /// One grep locates any occurrence in a run log: SAVEFAIL_WORLDSNAPSHOT_
        /// </summary>
        private static void ResolvePersistentWorldSnapshotFailureText(
            byte snapshotFailureCode,
            out string reason,
            out string logReason)
        {
            switch (snapshotFailureCode)
            {
                case PersistentWorldRegistry.SaveSnapshotFailureStorageNotCreated:
                    reason = "Persistent world registry storage was never allocated; world state cannot be saved.";
                    logReason = "[SaveManager] Save failed: SAVEFAIL_WORLDSNAPSHOT_STORAGE_NOT_CREATED - PersistentWorldRegistry vault buffers are not created. Awake() returned before InitializeVaultBackedStorage() because service registration failed; check for a ready-locked GlobalRegistry rejecting PersistentWorldRegistry.";
                    return;
                case PersistentWorldRegistry.SaveSnapshotFailureTombstoneStaging:
                    reason = "Persistent world resource-node tombstone staging failed; world state cannot be saved.";
                    logReason = "[SaveManager] Save failed: SAVEFAIL_WORLDSNAPSHOT_TOMBSTONE_STAGING - StageResourceNodeTombstonesForSave() rejected the capture. Inspect the resource-node tombstone and deleted-instance buffer capacities in the world telemetry ring.";
                    return;
                case PersistentWorldRegistry.SaveSnapshotFailureSnapshotClear:
                    reason = "Persistent world snapshot buffer could not be cleared; world state cannot be saved.";
                    logReason = "[SaveManager] Save failed: SAVEFAIL_WORLDSNAPSHOT_SNAPSHOT_CLEAR - TryClearSaveSnapshotDeltas() rejected the capture. The save-snapshot delta buffer refused a Clear(); see the capacity-mismatch entry in the world telemetry ring.";
                    return;
                case PersistentWorldRegistry.SaveSnapshotFailureCapacityOverflow:
                    reason = "Persistent world snapshot exceeded its capacity; world state cannot be saved.";
                    logReason = "[SaveManager] Save failed: SAVEFAIL_WORLDSNAPSHOT_CAPACITY_OVERFLOW - the save-snapshot delta buffer overflowed while expanding delta records. Raise PersistentWorldRegistry.maxTrackedItems or reduce tracked world deltas.";
                    return;
                default:
                    // Includes SaveSnapshotFailureNone: CaptureSaveSnapshot() returned false without
                    // setting a discriminator, which means a false-return path was added upstream
                    // without assigning _lastSaveSnapshotFailureCode.
                    reason = "Persistent world save snapshot capture failed.";
                    logReason = "[SaveManager] Save failed: SAVEFAIL_WORLDSNAPSHOT_UNATTRIBUTED - PersistentWorldRegistry.CaptureSaveSnapshot() returned false with no failure discriminator set.";
                    return;
            }
        }

        private bool TryCapturePersistentWorldSnapshot(
            string slotName,
            byte slotIndex,
            uint operationId,
            PersistentWorldRegistry persistentWorldRegistry,
            long elapsedMilliseconds,
            ref NativeArray<PersistentWorldDeltaRecord>.ReadOnly persistentWorldDeltaSnapshot,
            ref NativeArray<PersistentWorldDeltaRecord> persistentWorldDeltaSnapshotOwner,
            ref bool snapshotPauseActive)
        {
            if (persistentWorldRegistry != null)
            {
                if (!persistentWorldRegistry.CaptureSaveSnapshot())
                {
                    // Attribute the loss to the exact registry branch. A single shared reason string
                    // meant a total loss of player progress could only be diagnosed by byte-decoding
                    // the slot_N.diag UTF-16 payload, and even then named none of the four branches.
                    // reason -> player-facing UI + the diag sidecar (branch-distinct MessageHash).
                    // logReason -> carries the SAVEFAIL_WORLDSNAPSHOT_ grep token for the run log.
                    // All const strings selected by a switch: no concat, no interpolation, no boxing.
                    ResolvePersistentWorldSnapshotFailureText(
                        persistentWorldRegistry.LastSaveSnapshotFailureCode,
                        out string reason,
                        out string logReason);
                    const uint failureCode = 3u;
                    Exception cleanupException = null;
                    HandleSaveFailure(slotName, slotIndex, operationId, reason, logReason, failureCode, elapsedMilliseconds, ref cleanupException, ref snapshotPauseActive);
                    return false;
                }

                int persistentWorldSnapshotCapacity = persistentWorldRegistry.SaveSnapshotCapacity;
                if (persistentWorldSnapshotCapacity > 0)
                {
                    persistentWorldDeltaSnapshotOwner = CreateTransientNativeArray<PersistentWorldDeltaRecord>(
                        persistentWorldSnapshotCapacity,
                        Allocator.Persistent,
                        NativeArrayOptions.UninitializedMemory,
                        "persistentWorldDeltaSnapshotOwner");

                    if (!persistentWorldRegistry.TryCopySaveSnapshotDeltas(
                        persistentWorldDeltaSnapshotOwner,
                        persistentWorldSnapshotCapacity,
                        out int copiedPersistentWorldDeltas))
                    {
                        Exception cleanupException = null;
                        DisposeTransientNativeArrayBestEffort(ref persistentWorldDeltaSnapshotOwner, ref cleanupException, sentinelLabel: "persistentWorldDeltaSnapshotOwner");
                        const string reason = "Persistent world save snapshot copy failed.";
                        const string logReason = "[SaveManager] Save failed: persistent world save snapshot copy failed.";
                        const uint failureCode = 3u;
                        HandleSaveFailure(slotName, slotIndex, operationId, reason, logReason, failureCode, elapsedMilliseconds, ref cleanupException, ref snapshotPauseActive);
                        return false;
                    }

                    if (copiedPersistentWorldDeltas > 0)
                    {
                        NativeArray<PersistentWorldDeltaRecord> copiedView = copiedPersistentWorldDeltas < persistentWorldDeltaSnapshotOwner.Length
                            ? persistentWorldDeltaSnapshotOwner.GetSubArray(0, copiedPersistentWorldDeltas)
                            : persistentWorldDeltaSnapshotOwner;
                        persistentWorldDeltaSnapshot = copiedView.AsReadOnly();
                    }
                }
            }
            return true;
        }

        private void CaptureEcosystemSnapshot(
            ref NativeArray<EcosystemSectorSaveRecord>.ReadOnly ecosystemSectorSnapshot,
            ref NativeArray<EcosystemSectorSaveRecord> ecosystemSectorSnapshotOwner)
        {
            EcosystemDirector ecosystemDirector = GlobalRegistry.EcosystemDirector as EcosystemDirector;
            if (ecosystemDirector != null)
            {
                ecosystemDirector.CaptureSaveSnapshot();
                NativeArray<EcosystemSectorSaveRecord>.ReadOnly ecosystemView = ecosystemDirector.GetSaveSnapshotArray(out int ecosystemRecordCount);
                if (ecosystemView.IsCreated && ecosystemRecordCount > 0)
                {
                    ecosystemSectorSnapshotOwner = CreateTransientNativeArray<EcosystemSectorSaveRecord>(
                        ecosystemRecordCount,
                        Allocator.Persistent,
                        NativeArrayOptions.UninitializedMemory,
                        "ecosystemSectorSnapshotOwner");

                    for (int i = 0; i < ecosystemRecordCount; i++)
                        ecosystemSectorSnapshotOwner[i] = ecosystemView[i];

                    ecosystemSectorSnapshot = ecosystemSectorSnapshotOwner.AsReadOnly();
                }
            }
        }

        private void CaptureQuestSnapshot(
            long saveTimestampTicks,
            ref NativeArray<uint> packedQuestStateSnapshot,
            ref QuestSaveHeader packedQuestSaveHeader)
        {
            QuestManager questManager = GlobalRegistry.Quest;
            if (questManager != null)
            {
                int packedQuestWordCount = questManager.PackedStateWordCount;
                if (packedQuestWordCount > 0)
                {
                    packedQuestStateSnapshot = CreateTransientNativeArray<uint>(
                        packedQuestWordCount,
                        Allocator.Persistent,
                        NativeArrayOptions.ClearMemory,
                        "packedQuestStateSnapshot");

                    bool copiedQuestState;
                    unsafe
                    {
                        void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(packedQuestStateSnapshot);
                        copiedQuestState = questManager.TryCopyPackedStateSnapshot(
                            destinationPtr,
                            packedQuestStateSnapshot.Length,
                            out packedQuestSaveHeader,
                            saveTimestampTicks);
                    }

                    if (!copiedQuestState)
                        DisposeTransientNativeArrayBestEffortAndReport(ref packedQuestStateSnapshot, "save", "packedQuestStateSnapshot");
                }
            }
        }

        private void HandleSaveFailure(
            string slotName,
            byte slotIndex,
            uint operationId,
            string reason,
            string logReason,
            uint failureCode,
            long elapsedMilliseconds,
            ref Exception cleanupException,
            ref bool snapshotPauseActive)
        {
            if (snapshotPauseActive)
            {
                ReleaseSnapshotPauseBestEffort(operationId, ref cleanupException);
                snapshotPauseActive = false;
            }

            ReportPersistenceCleanupFailure("save", cleanupException);
            RecordAsyncPersistenceTelemetry(operationId, slotName, elapsedMilliseconds, 0L, 0, 0, failureCode);
            PublishSaveCompletedForSlotName(slotIndex, slotName, new PublishSaveCompletedArgs(operationId: operationId, durationMs: elapsedMilliseconds, compressedSizeBytes: 0L, succeeded: false));
            PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Failed, 1f, failureCode);
            DumpSaveBlackBox();
            RecordFailure(slotName, "save", reason);
            LastOperationError = reason;
            LogError(logReason);
            SaveEvents.TryRaiseSaveFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(reason), reason);
        }

        private bool TryPassPreflightChecks(ref string slotName, byte slotIndex, uint operationId)
        {
            if (_runtimeOwnerAborted || !_serviceRegistered)
            {
                uint unavailableSlotHash = ResolveUnavailableSlotContext(slotName, slotIndex, out string unavailableSlotName);
                LastOperationError = SaveServiceUnavailableReason;
                LastOperationSlot = unavailableSlotName;
                SaveEvents.TryRaiseSaveFailed(unavailableSlotHash, SaveEvents.ComputeMessageHash(SaveServiceUnavailableReason), SaveServiceUnavailableReason);
                PublishSaveStatus(unavailableSlotHash, operationId, SaveStatusSignal.Rejected, 0f, 1u);
                return false;
            }

            if (!TryResolveSafeSlotName(slotName, out slotName))
            {
                LastOperationError = InvalidSlotNameReason;
                LogWarning("[SaveManager] Ignored save request: invalid slot name.");
                SaveEvents.TryRaiseSaveFailed(0u, SaveEvents.ComputeMessageHash(InvalidSlotNameReason), InvalidSlotNameReason);
                PublishSaveStatus(slotIndex, operationId, SaveStatusSignal.Rejected, 0f, 1u);
                return false;
            }

            LastOperationSlot = slotName;

            if (_isBusy)
            {
                const string reason = "Save already in progress.";
                LastOperationError = reason;
                LogWarning($"[SaveManager] Ignored save request for '{slotName}': {reason}");
                SaveEvents.TryRaiseSaveFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(reason), reason);
                PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Rejected, 0f, 1u);
                return false;
            }

            if (TryRejectSaveDuringRespawnReconciliation(slotIndex, operationId, slotName))
                return false;

            if (HectonFloatingOrigin.IsShiftInProgress || HectonFloatingOrigin.IsPhysicsPausedForShift)
            {
                const string reason = "Save blocked during floating-origin shift.";
                LastOperationError = reason;
                LogWarning($"[SaveManager] Ignored save request for '{slotName}': {reason}");
                SaveEvents.TryRaiseSaveFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(reason), reason);
                PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Rejected, 0f, 1u);
                return false;
            }

            _isBusy = true;
            Exception startupException = null;
            NotifyMacroDatabasePersistenceGateBestEffort(true, ref startupException);
            if (startupException != null)
            {
                const string reason = "Save persistence gate request failed.";
                LastOperationError = reason;
                LogWarningBestEffort($"[SaveManager] Save failed for '{slotName}': {reason}");
                _isBusy = false;
                NotifyMacroDatabasePersistenceGateBestEffort(false, ref startupException);
                ReportPersistenceCleanupFailure("save", startupException);
                SaveEvents.TryRaiseSaveFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(reason), reason);
                PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Failed, 1f, 1u);
                return false;
            }

            return true;
        }

        private async Awaitable SaveGameAsyncInternal(string slotName, byte slotIndex, uint operationId)
        {
            CachePersistentDataPathRoot();
            LastOperationSucceeded = false;
            LastOperationError = string.Empty;
            LastOperationSlot = string.Empty;
            LastLoadUsedBackup = false;
            LastLoadBackupGeneration = 0;
            LastLoadSelfRepaired = false;
            LastLoadUsedLegacyCompression = false;

            if (!TryPassPreflightChecks(ref slotName, slotIndex, operationId))
                return;

            SaveThumbnailSystem.CaptureTicket thumbnailTicket = default;
            var totalTimer = Stopwatch.StartNew();
            var snapshotTimer = Stopwatch.StartNew();
            bool snapshotPauseActive = false;
            double playTime = ResolveCurrentPlayTimeSeconds();
            SaveData data = SaveData.CreateNew(playTime);
            PersistentWorldRegistry persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;
            NativeArray<PersistentWorldDeltaRecord>.ReadOnly persistentWorldDeltaSnapshot = default;
            NativeArray<PersistentWorldDeltaRecord> persistentWorldDeltaSnapshotOwner = default;
            NativeArray<EcosystemSectorSaveRecord>.ReadOnly ecosystemSectorSnapshot = default;
            NativeArray<EcosystemSectorSaveRecord> ecosystemSectorSnapshotOwner = default;
            NativeArray<uint> packedQuestStateSnapshot = default;
            QuestSaveHeader packedQuestSaveHeader = default;
            NativeArray<byte> voxelDeltaSnapshot = default;
            bool ownsVoxelDeltaSnapshot = false;
            VoxelDeltaProcessor borrowedVoxelDeltaSnapshotOwner = null;

            try
            {
                SaveEvents.TryRaiseSaveStarted(SaveEvents.ComputeSlotHash(slotName));
                PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.InProgress, 0.05f, 0u);
                EnsureSaveWorkingBuffers();
                RequestSnapshotPause(operationId);
                snapshotPauseActive = true;
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(destroyCancellationToken);
                if (TryRejectSaveDuringRespawnReconciliation(
                        slotIndex,
                        operationId,
                        slotName,
                        activeSaveStarted: true,
                        elapsedMs: totalTimer.ElapsedMilliseconds))
                {
                    return;
                }

                thumbnailTicket = SaveThumbnailSystem.CaptureThumbnailForSave(slotName, slotIndex, operationId);
                ThreadSafeCommandQueue.PrepareStorageReservationCommitBridgeForPersistenceSnapshot();
                snapshotTimer.Restart();

                if (!TryPopulateSaveDataAndCaptureVoxelSnapshot(
                    slotName, slotIndex, operationId, data, totalTimer.ElapsedMilliseconds,
                    ref voxelDeltaSnapshot, ref ownsVoxelDeltaSnapshot,
                    ref borrowedVoxelDeltaSnapshotOwner, ref snapshotPauseActive))
                {
                    return;
                }

                StampRuntimeWorldSeed(data);
                StampProceduralTerrainIdentity(data);
                ModSaveStateStore.PopulateSaveData(data);
                Stopwatch divergenceSnapshotTimer = Stopwatch.StartNew();

                if (!TryCapturePersistentWorldSnapshot(
                    slotName, slotIndex, operationId, persistentWorldRegistry, totalTimer.ElapsedMilliseconds,
                    ref persistentWorldDeltaSnapshot, ref persistentWorldDeltaSnapshotOwner, ref snapshotPauseActive))
                {
                    return;
                }

                CaptureEcosystemSnapshot(ref ecosystemSectorSnapshot, ref ecosystemSectorSnapshotOwner);

                divergenceSnapshotTimer.Stop();
                long saveTimestampTicks = DateTime.UtcNow.Ticks;

                CaptureQuestSnapshot(saveTimestampTicks, ref packedQuestStateSnapshot, ref packedQuestSaveHeader);

                RecordPlayerDialogueChoiceFlag(SaveBinaryStorage.ExtractPlayerDialogueChoiceFlags(packedQuestStateSnapshot));
                ushort playerDialogueChoiceFlagsSnapshot = PlayerDialogueChoiceFlags;

                SaveMetadata metadata = new SaveMetadata
                {
                    SlotName = slotName,
                    GameVersion = Application.version,
                    Timestamp = saveTimestampTicks,
                    PlayTimeSeconds = (float)playTime,
                    SceneName = SaveMetadata.NormalizeSceneName(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name),
                    PlayerPosition = data.playerStats.GetPosition(),
                    WorldSeed = data.ecosystemState.worldSeed,
                    WorldGenerationVersionId = data.ecosystemState.worldGenerationVersionId
                };

                snapshotTimer.Stop();
                StageSnapshotHeader(operationId, slotName, persistentWorldDeltaSnapshot, ecosystemSectorSnapshot, packedQuestStateSnapshot, voxelDeltaSnapshot);
                Exception snapshotPauseReleaseException = null;
                ReleaseSnapshotPauseBestEffort(operationId, ref snapshotPauseReleaseException);
                snapshotPauseActive = false;
                ReportPersistenceCleanupFailure("save", snapshotPauseReleaseException);
                WarnIfSnapshotBudgetExceeded(slotName, snapshotTimer.ElapsedMilliseconds);

                int backupRetention = GetBackupRetentionCount(slotName);
                string tempPath = GetTempSaveFilePath(slotName);
                if (divergenceSnapshotTimer.ElapsedTicks > PreCompressionYieldBudgetTicks)
                    await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync();

                await Awaitable.MainThreadAsync();
                SaveContextFrameData frameData = SaveContextFrameData.CaptureMainThread();
                SaveEvents.TryRaiseMappedWriteStarted(SaveEvents.ComputeSlotHash(slotName));
                await Awaitable.BackgroundThreadAsync();

                ulong payloadHash64;
                int rawPayloadLength;
                long compressionPipelineStartTicks = Stopwatch.GetTimestamp();

                if (!TryExecuteVerifiedSavePipeline(
                    slotName,
                    tempPath,
                    GetPrimarySaveFilePath(slotName),
                    metadata,
                    data,
                    persistentWorldDeltaSnapshot,
                    ecosystemSectorSnapshot,
                    packedQuestSaveHeader,
                    packedQuestStateSnapshot,
                    playerDialogueChoiceFlagsSnapshot,
                    voxelDeltaSnapshot,
                    _savePayloadBuffer,
                    _compressedSaveBuffer,
                    backupRetention,
                    out payloadHash64,
                    out rawPayloadLength,
                    out long compressedSizeBytes,
                    out string savePipelineError))
                {
                    await Awaitable.MainThreadAsync();
                    const uint failureCode = 3u;
                    string failureMessage = string.IsNullOrEmpty(savePipelineError)
                        ? "Verified save pipeline failed."
                        : savePipelineError;
                    Exception pipelineException = null;
                    HandleSaveFailure(slotName, slotIndex, operationId, failureMessage, "[SaveManager] Save failed: " + failureMessage, failureCode, totalTimer.ElapsedMilliseconds, ref pipelineException, ref snapshotPauseActive);
                    return;
                }

                long compressionPipelineElapsedTicks = Stopwatch.GetTimestamp() - compressionPipelineStartTicks;
                await Awaitable.MainThreadAsync();
                RegisterCompressionPipelineElapsed(compressionPipelineElapsedTicks, in frameData);
                SaveThumbnailSystem.CaptureCompletion thumbnailCompletion =
                    await SaveThumbnailSystem.WaitForCompletionAsync(thumbnailTicket, destroyCancellationToken);
                RecordAsyncPersistenceTelemetry(
                    operationId,
                    slotName,
                    totalTimer.ElapsedMilliseconds,
                    compressedSizeBytes,
                    rawPayloadLength,
                    thumbnailCompletion.ByteLength,
                    thumbnailCompletion.Succeeded != 0 ? 0u : 2u);
                PublishSaveCompletedForSlotName(slotIndex, slotName, new PublishSaveCompletedArgs(operationId: operationId, durationMs: totalTimer.ElapsedMilliseconds, compressedSizeBytes: compressedSizeBytes, succeeded: true));
                PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Completed, 1f, 0u);
                StageIntegrityPayload(_savePayloadBuffer, rawPayloadLength, payloadHash64, slotName);
                SaveSlotIntegrityState savedIntegrity = backupRetention > 0
                    ? SaveSlotIntegrityState.HealthyWithBackup
                    : SaveSlotIntegrityState.Healthy;
                RecordSuccessfulSave(slotName, data.version, savedIntegrity);
                NotifyMappedInventoryWritesCommitted();

                LastOperationSucceeded = true;
                LogInfo($"[SaveManager] Saved '{slotName}' (XXH3-64: {metadata.Checksum}) in {totalTimer.ElapsedMilliseconds}ms");
                RaiseSaveCompletedWithBackpressureRecovery(SaveEvents.ComputeSlotHash(slotName));
                PublishSaveSynchronizedNotification(slotName);
            }
            catch (Exception ex)
            {
                await Awaitable.MainThreadAsync();
                Exception cleanupException = null;
                HandleSaveFailure(slotName, slotIndex, operationId, ex.Message, "[SaveManager] Save failed: " + ex, 1u, totalTimer.ElapsedMilliseconds, ref cleanupException, ref snapshotPauseActive);
            }
            finally
            {
                Exception cleanupException = null;

                if (snapshotPauseActive)
                    ReleaseSnapshotPauseBestEffort(operationId, ref cleanupException);

                if (packedQuestStateSnapshot.IsCreated)
                    DisposeTransientNativeArrayBestEffort(ref packedQuestStateSnapshot, ref cleanupException, sentinelLabel: "packedQuestStateSnapshot");

                if (persistentWorldDeltaSnapshotOwner.IsCreated)
                    DisposeTransientNativeArrayBestEffort(ref persistentWorldDeltaSnapshotOwner, ref cleanupException, sentinelLabel: "persistentWorldDeltaSnapshotOwner");

                if (ecosystemSectorSnapshotOwner.IsCreated)
                    DisposeTransientNativeArrayBestEffort(ref ecosystemSectorSnapshotOwner, ref cleanupException, sentinelLabel: "ecosystemSectorSnapshotOwner");

                if (voxelDeltaSnapshot.IsCreated && ownsVoxelDeltaSnapshot)
                    DisposeTransientNativeArrayBestEffort(ref voxelDeltaSnapshot, ref cleanupException, sentinelLabel: "voxelDeltaSnapshot");

                if (borrowedVoxelDeltaSnapshotOwner != null)
                {
                    ReleaseBorrowedVoxelDeltaSnapshotBestEffort(borrowedVoxelDeltaSnapshotOwner, ref cleanupException);
                    borrowedVoxelDeltaSnapshotOwner = null;
                }

                _isBusy = false;
                NotifyMacroDatabasePersistenceGateBestEffort(false, ref cleanupException);
                ReportPersistenceCleanupFailure("save", cleanupException);
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

        [StructLayout(LayoutKind.Explicit, Size = 4)]
        private readonly struct SaveContextFrameData
        {
            [FieldOffset(0)]
            public readonly int FrameCount;

            private SaveContextFrameData(int frameCount)
            {
                FrameCount = frameCount;
            }

            public static SaveContextFrameData CaptureMainThread()
            {
                return new SaveContextFrameData(SystemDispatcher.CurrentFrameIndex);
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
                ISaveable saveable = _saveables[i];
                if (!IsAlive(saveable))
                {
                    _registryDirty = true;
                    continue;
                }

                if (saveable is IMappedInventoryWriteCommitSink sink)
                    sink.NotifyMappedInventoryWriteCommitted();
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
            TryPushGeologicalAnomalyNotification();
            PlayerSignalEvents.TryRaiseTraumaHudSignal(new TraumaHudSignal(
                0.78f,
                0.12f,
                1f,
                1f,
                false));
        }

        private static void StampProceduralTerrainIdentity(SaveData data)
        {
            if (data == null)
                return;

            IWorldSeedProvider seedProvider = GlobalRegistry.WorldSeedProvider;
            int runtimeSeed = 0;
            int worldGenerationVersionId = 0;
            if (seedProvider != null && seedProvider.IsInitialized)
            {
                runtimeSeed = seedProvider.RuntimeWorldSeed;
                worldGenerationVersionId = math.max(0, seedProvider.RuntimeWorldGenerationVersionId);
            }

            TerrainArtifactIdentityDTO terrainIdentity =
                ResolveRuntimeTerrainArtifactIdentity(runtimeSeed, worldGenerationVersionId);

            ProceduralTerrainIdentityDTO identity = default;
            identity.authoringSeed = terrainIdentity.AuthoringSeed;
            identity.runtimeSeed = terrainIdentity.RuntimeSeed;
            identity.worldGenerationVersionId = terrainIdentity.WorldGenerationVersionId;
            identity.macroArtifactVersion = terrainIdentity.MacroArtifactVersion;
            identity.macroChunkSizeMeters = terrainIdentity.ChunkSizeMeters;
            identity.chunkMinX = terrainIdentity.ChunkMinX;
            identity.chunkMinZ = terrainIdentity.ChunkMinZ;
            identity.chunkMaxX = terrainIdentity.ChunkMaxX;
            identity.chunkMaxZ = terrainIdentity.ChunkMaxZ;
            identity.chunkArtifactRangeHash = terrainIdentity.ChunkArtifactRangeHash;
            identity.flags = ProceduralTerrainIdentityDTO.FlagsMacroGeologyPresent;
            if ((terrainIdentity.Flags & TerrainArtifactIdentityDTO.FlagsDefaultChunkRange) != 0u)
                identity.flags |= ProceduralTerrainIdentityDTO.FlagsDefaultChunkRange;

            if (TryResolveActiveWaterCalibration(out WorldWaterLevelCalibrationDTO waterSnapshot))
            {
                identity.selectedWaterLevelY = waterSnapshot.ResolvedWaterLevelY;
                identity.waterCalibrationTravelMeters = waterSnapshot.CalibrationTravelMeters;
                identity.waterCalibrationSourceHash = waterSnapshot.SourceHash;
                identity.flags |= ProceduralTerrainIdentityDTO.FlagsWaterCalibrationPresent;
            }

            identity.terrainProviderFlags = terrainIdentity.Flags;
            identity.heightCacheRevision = math.max(0, terrainIdentity.CacheRevision);
            identity.terrainEntityHash = terrainIdentity.TerrainEntityHash;
            identity.surfaceMaterialContractVersion = WorldTerrainSurfaceMaterialResolver.ContractVersion;
            identity.mesoDetailContractVersion = WorldTerrainMesoDetailFields.ContractVersion;
            identity.detailEligibilityContractVersion = WorldTerrainDetailContracts.ContractVersion;
            identity.mesoParamsHash = BuildTerrainMesoParamsHash(
                terrainIdentity.AuthoringSeed,
                terrainIdentity.RuntimeSeed);
            identity.flags |= ProceduralTerrainIdentityDTO.FlagsTerrainProviderIdentityPresent |
                              ProceduralTerrainIdentityDTO.FlagsTerrainMaterialContractsPresent |
                              ProceduralTerrainIdentityDTO.FlagsTerrainMesoContractsPresent;
            if ((terrainIdentity.Flags & TerrainArtifactIdentityDTO.FlagsHeightPayloadPresent) != 0u)
                identity.flags |= ProceduralTerrainIdentityDTO.FlagsTerrainHeightPayloadPresent;

            data.proceduralTerrainIdentity = identity;
        }

        private static bool CheckProceduralTerrainMacroMismatch(ProceduralTerrainIdentityDTO saved, TerrainArtifactIdentityDTO expected)
        {
            return saved.authoringSeed != expected.AuthoringSeed ||
                   saved.macroArtifactVersion != expected.MacroArtifactVersion ||
                   math.abs(saved.macroChunkSizeMeters - expected.ChunkSizeMeters) > 0.001f ||
                   saved.chunkMinX != expected.ChunkMinX ||
                   saved.chunkMinZ != expected.ChunkMinZ ||
                   saved.chunkMaxX != expected.ChunkMaxX ||
                   saved.chunkMaxZ != expected.ChunkMaxZ ||
                   saved.chunkArtifactRangeHash != expected.ChunkArtifactRangeHash;
        }

        private static bool CheckProceduralTerrainSeedMismatch(
            ProceduralTerrainIdentityDTO saved,
            IWorldSeedProvider seedProvider,
            int runtimeSeed,
            int worldGenerationVersionId)
        {
            return seedProvider != null &&
                   seedProvider.IsInitialized &&
                   ((saved.runtimeSeed != 0 && saved.runtimeSeed != runtimeSeed) ||
                    (saved.worldGenerationVersionId > 0 &&
                     saved.worldGenerationVersionId != worldGenerationVersionId));
        }

        private static bool CheckProceduralTerrainWaterMismatch(ProceduralTerrainIdentityDTO saved)
        {
            bool hasSavedWaterCalibration =
                (saved.flags & ProceduralTerrainIdentityDTO.FlagsWaterCalibrationPresent) != 0u &&
                saved.waterCalibrationSourceHash != 0u;

            if (hasSavedWaterCalibration &&
                TryResolveActiveWaterCalibration(out WorldWaterLevelCalibrationDTO waterSnapshot))
            {
                bool waterMismatch =
                    waterSnapshot.SourceHash != 0u &&
                    waterSnapshot.SourceHash != saved.waterCalibrationSourceHash;
                waterMismatch |=
                    math.abs(waterSnapshot.ResolvedWaterLevelY - saved.selectedWaterLevelY) > 0.01f;
                return waterMismatch;
            }

            return hasSavedWaterCalibration;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static void LogProceduralTerrainIdentityMismatch(
            ProceduralTerrainIdentityDTO saved,
            TerrainArtifactIdentityDTO expected,
            int runtimeSeed,
            int worldGenerationVersionId,
            uint expectedMesoParamsHash)
        {
            LogWarning(
                "[SaveManager] Procedural terrain identity mismatch: saved seed " +
                saved.runtimeSeed +
                " / generation " +
                saved.worldGenerationVersionId +
                " / macro artifact " +
                saved.macroArtifactVersion +
                " / chunk " +
                saved.macroChunkSizeMeters +
                "m / range hash " +
                saved.chunkArtifactRangeHash +
                " / water " +
                saved.selectedWaterLevelY +
                " / material contract " +
                saved.surfaceMaterialContractVersion +
                " / meso contract " +
                saved.mesoDetailContractVersion +
                " / detail contract " +
                saved.detailEligibilityContractVersion +
                " / meso params " +
                saved.mesoParamsHash +
                " / provider flags " +
                saved.terrainProviderFlags +
                " / height cache " +
                saved.heightCacheRevision +
                " / terrain entity " +
                saved.terrainEntityHash +
                " != runtime seed " +
                runtimeSeed +
                " / generation " +
                worldGenerationVersionId +
                " / macro artifact " +
                expected.MacroArtifactVersion +
                " / chunk " +
                expected.ChunkSizeMeters +
                "m / range hash " +
                expected.ChunkArtifactRangeHash +
                " / material contract " +
                WorldTerrainSurfaceMaterialResolver.ContractVersion +
                " / meso contract " +
                WorldTerrainMesoDetailFields.ContractVersion +
                " / detail contract " +
                WorldTerrainDetailContracts.ContractVersion +
                " / meso params " +
                expectedMesoParamsHash +
                " / provider flags " +
                expected.Flags +
                " / height cache " +
                expected.CacheRevision +
                " / terrain entity " +
                expected.TerrainEntityHash +
                ".");
        }
#endif

        private static void ValidateProceduralTerrainIdentity(SaveData data)
        {
            if (data == null || !data.proceduralTerrainIdentity.HasMacroIdentity)
                return;

            ProceduralTerrainIdentityDTO saved = data.proceduralTerrainIdentity;
            IWorldSeedProvider seedProvider = GlobalRegistry.WorldSeedProvider;

            int runtimeSeed = seedProvider != null && seedProvider.IsInitialized
                ? seedProvider.RuntimeWorldSeed
                : saved.runtimeSeed;
            int worldGenerationVersionId = seedProvider != null && seedProvider.IsInitialized
                ? math.max(0, seedProvider.RuntimeWorldGenerationVersionId)
                : saved.worldGenerationVersionId;

            TerrainArtifactIdentityDTO expected = ResolveRuntimeTerrainArtifactIdentity(
                runtimeSeed,
                worldGenerationVersionId);

            bool macroMismatch = CheckProceduralTerrainMacroMismatch(saved, expected);
            bool seedMismatch = CheckProceduralTerrainSeedMismatch(saved, seedProvider, runtimeSeed, worldGenerationVersionId);
            bool waterMismatch = CheckProceduralTerrainWaterMismatch(saved);

            uint expectedMesoParamsHash = BuildTerrainMesoParamsHash(expected.AuthoringSeed, expected.RuntimeSeed);
            bool materialContractMismatch =
                saved.surfaceMaterialContractVersion != 0u &&
                saved.surfaceMaterialContractVersion != WorldTerrainSurfaceMaterialResolver.ContractVersion;
            bool mesoContractMismatch =
                (saved.mesoDetailContractVersion != 0u &&
                 saved.mesoDetailContractVersion != WorldTerrainMesoDetailFields.ContractVersion) ||
                (saved.detailEligibilityContractVersion != 0u &&
                 saved.detailEligibilityContractVersion != WorldTerrainDetailContracts.ContractVersion) ||
                (saved.mesoParamsHash != 0u &&
                 saved.mesoParamsHash != expectedMesoParamsHash);
            bool providerIdentityMismatch =
                saved.terrainProviderFlags != 0u &&
                expected.Flags != 0u &&
                (saved.terrainProviderFlags & TerrainArtifactIdentityDTO.FlagsMapMagicProvider) !=
                (expected.Flags & TerrainArtifactIdentityDTO.FlagsMapMagicProvider);
            bool heightPayloadMismatch =
                saved.heightCacheRevision > 0 &&
                expected.CacheRevision > 0 &&
                saved.heightCacheRevision != expected.CacheRevision;
            bool terrainEntityMismatch =
                saved.terrainEntityHash != 0u &&
                expected.TerrainEntityHash != 0u &&
                saved.terrainEntityHash != expected.TerrainEntityHash;

            if (!macroMismatch &&
                !seedMismatch &&
                !waterMismatch &&
                !materialContractMismatch &&
                !mesoContractMismatch &&
                !providerIdentityMismatch &&
                !heightPayloadMismatch &&
                !terrainEntityMismatch)
            {
                return;
            }

            float mismatchMask = 0f;
            if (macroMismatch) mismatchMask += 1f;
            if (seedMismatch) mismatchMask += 2f;
            if (waterMismatch) mismatchMask += 4f;
            if (materialContractMismatch) mismatchMask += 8f;
            if (mesoContractMismatch) mismatchMask += 16f;
            if (providerIdentityMismatch) mismatchMask += 32f;
            if (heightPayloadMismatch) mismatchMask += 64f;
            if (terrainEntityMismatch) mismatchMask += 128f;

            PublishPerformanceWarningBestEffort(
                TerrainIdentityMismatchTelemetryHash,
                TerrainIdentityMismatchContextHash,
                mismatchMask);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogProceduralTerrainIdentityMismatch(
                saved, expected, runtimeSeed, worldGenerationVersionId, expectedMesoParamsHash);
#endif
            if (macroMismatch || seedMismatch || waterMismatch || materialContractMismatch || mesoContractMismatch)
                TryPushGeologicalAnomalyNotification();
        }

        private static bool TryRestoreWaterCalibrationFromSave(SaveData data)
        {
            if (data == null)
                return false;

            ProceduralTerrainIdentityDTO saved = data.proceduralTerrainIdentity;
            if ((saved.flags & ProceduralTerrainIdentityDTO.FlagsWaterCalibrationPresent) == 0u ||
                saved.waterCalibrationSourceHash == 0u)
            {
                return false;
            }

            if (!WorldWaterLevelCalibrationMath.TryResolveWaterLevelY(
                    saved.selectedWaterLevelY,
                    WorldWaterLevelCalibrationMath.DefaultWaterLevelY,
                    saved.waterCalibrationTravelMeters,
                    out float restoredWaterLevelY))
            {
                return false;
            }

            if (WorldWaterLevelCalibrationRuntimeRegistry.TryGetActiveSnapshot(out WorldWaterLevelCalibrationDTO activeSnapshot) &&
                activeSnapshot.SourceHash != 0u &&
                activeSnapshot.SourceHash != saved.waterCalibrationSourceHash)
            {
                return false;
            }

            return WorldWaterLevelCalibrationRuntimeRegistry.TryApplySavedCalibration(
                restoredWaterLevelY,
                saved.waterCalibrationTravelMeters,
                saved.waterCalibrationSourceHash);
        }

        private static TerrainArtifactIdentityDTO ResolveRuntimeTerrainArtifactIdentity(
            int fallbackRuntimeSeed,
            int fallbackWorldGenerationVersionId)
        {
            ITerrainProvider terrainProvider = GlobalRegistry.Terrain;
            if (terrainProvider != null &&
                terrainProvider.IsAvailable &&
                terrainProvider.TryGetTerrainArtifactIdentity(out TerrainArtifactIdentityDTO providerIdentity) &&
                providerIdentity.HasMacroIdentity)
            {
                return providerIdentity;
            }

            float chunkSizeMeters = WorldMacroGeologyFields.DefaultChunkSizeMeters;
            WorldMacroGeologyFields.ResolveMinimumChunkRange(
                chunkSizeMeters,
                out int chunkMinX,
                out int chunkMinZ,
                out int chunkMaxX,
                out int chunkMaxZ);

            uint authoringSeed = unchecked((uint)WorldMacroGeologyFields.DefaultAuthoringSeed);
            return new TerrainArtifactIdentityDTO
            {
                AuthoringSeed = authoringSeed,
                RuntimeSeed = fallbackRuntimeSeed,
                WorldGenerationVersionId = fallbackWorldGenerationVersionId,
                MacroArtifactVersion = WorldMacroGeologyFields.ArtifactVersion,
                ChunkSizeMeters = chunkSizeMeters,
                ChunkMinX = chunkMinX,
                ChunkMinZ = chunkMinZ,
                ChunkMaxX = chunkMaxX,
                ChunkMaxZ = chunkMaxZ,
                ChunkArtifactRangeHash = WorldMacroGeologyFields.BuildChunkArtifactRangeHash(
                    authoringSeed,
                    fallbackRuntimeSeed,
                    fallbackWorldGenerationVersionId,
                    WorldMacroGeologyFields.ArtifactVersion,
                    chunkSizeMeters,
                    chunkMinX,
                    chunkMinZ,
                    chunkMaxX,
                    chunkMaxZ),
                Flags = TerrainArtifactIdentityDTO.FlagsMacroGeologyPresent |
                        TerrainArtifactIdentityDTO.FlagsDefaultChunkRange
            };
        }

        private static uint BuildTerrainMesoParamsHash(uint authoringSeed, int runtimeSeed)
        {
            uint combinedSeed = WorldMacroGeologyFields.CombineWorldSeed(authoringSeed, runtimeSeed);
            WorldTerrainMesoDetailParams meso = WorldTerrainMesoDetailFields.CreateDefaultParams(combinedSeed);
            uint hash = 2166136261u;
            hash = MixTerrainIdentityHash(hash, meso.Seed);
            hash = MixTerrainIdentityHash(hash, math.asuint(meso.PreviewExtentMeters));
            hash = MixTerrainIdentityHash(hash, math.asuint(meso.TerraceStrengthMeters));
            hash = MixTerrainIdentityHash(hash, math.asuint(meso.SlumpStrengthMeters));
            hash = MixTerrainIdentityHash(hash, math.asuint(meso.TributaryStrengthMeters));
            hash = MixTerrainIdentityHash(hash, math.asuint(meso.TalusStrengthMeters));
            hash = MixTerrainIdentityHash(hash, math.asuint(meso.RubbleStrengthMeters));
            hash = MixTerrainIdentityHash(hash, math.asuint(meso.ReefStrengthMeters));
            hash = MixTerrainIdentityHash(hash, math.asuint(meso.MaxMesoDeltaMeters));
            return hash == 0u ? 1u : hash;
        }

        private static uint MixTerrainIdentityHash(uint hash, uint value)
        {
            unchecked
            {
                hash ^= value;
                hash *= 16777619u;
                return hash;
            }
        }

        private static bool TryResolveActiveWaterCalibration(out WorldWaterLevelCalibrationDTO snapshot)
        {
            snapshot = default;
            if (!Application.isPlaying)
                return false;

            return WorldWaterLevelCalibrationRuntimeRegistry.TryGetActiveSnapshot(out snapshot);
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
            LoadingScreenController loadingScreen = _cachedLoadingScreenController;
            if (IsLoadingScreenControllerUsable(loadingScreen) &&
                ReferenceEquals(loadingScreen, GlobalRegistry.LoadingScreen))
            {
                return loadingScreen;
            }

            _cachedLoadingScreenController = null;
            loadingScreen = GlobalRegistry.LoadingScreen;
            CacheLoadingScreenController(loadingScreen);
            return _cachedLoadingScreenController;
        }

        private void CacheLoadingScreenController(LoadingScreenController loadingScreen)
        {
            _cachedLoadingScreenController = IsLoadingScreenControllerUsable(loadingScreen) ? loadingScreen : null;
        }

        private static bool IsLoadingScreenControllerUsable(LoadingScreenController loadingScreen)
        {
            return loadingScreen != null &&
                   loadingScreen.IsServiceReady &&
                   loadingScreen.isActiveAndEnabled;
        }

        private static void ReportCriticalSectorCorruptionDialog()
        {
            TryPushCriticalSectorCorruptionNotification();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogWarning($"[SaveManager] {CriticalSectorCorruptionMessage}");
#endif
        }

        private static void TryPushGeologicalAnomalyNotification()
        {
            if (NotificationEvents.TryPushWarning(GeologicalAnomalyDetectedMessage.AsSpan()))
                return;

            ReportSaveNotificationMiss(
                GeologicalAnomalyNotificationMissTelemetryHash,
                GeologicalAnomalyNotificationContextHash,
                ref s_geologicalAnomalyNotificationMissCount);
        }

        private static void TryPushCriticalSectorCorruptionNotification()
        {
            if (NotificationEvents.TryPushCritical(CriticalSectorCorruptionMessage.AsSpan()))
                return;

            ReportSaveNotificationMiss(
                CriticalSectorCorruptionNotificationMissTelemetryHash,
                CriticalSectorCorruptionNotificationContextHash,
                ref s_criticalSectorCorruptionNotificationMissCount);
        }

        private static void ReportSaveNotificationMiss(uint warningHash, uint contextHash, ref int missCount)
        {
            missCount++;
            PublishPerformanceWarningBestEffort(
                warningHash,
                SaveManagerNotificationContextHash ^ contextHash,
                math.max(1, missCount));
        }

        private static void ClearSaveNotificationDiagnostics()
        {
            s_geologicalAnomalyNotificationMissCount = 0;
            s_criticalSectorCorruptionNotificationMissCount = 0;
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

            if (!TryResolveAupFromRuntimeOrigin(savedRuntimePosition, out AbsoluteUniversePosition savedAup))
                return false;

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

            Rigidbody playerBody = playerContext != null ? playerContext.PlayerRigidbody : null;
            if (playerBody == null)
                playerTransform.TryGetComponent(out playerBody);

            HectonFloatingOrigin.BeginSafeTeleportProtocol();
            try
            {
                TeleportLoadedPlayer(playerTransform, playerBody, snappedPosition, savedRotation, savedVelocity);
            }
            finally
            {
                HectonFloatingOrigin.EndSafeTeleportProtocol();
            }

            data.playerStats.SetPosition(snappedPosition);
            data.playerStats.SetRotation(savedRotation);
            data.playerStats.SetVelocity(savedVelocity);
            data.playerKinematicState = PlayerKinematicStateDTO.FromPlayerStats(in data.playerStats);
            return true;
        }

        private static bool TryResolveCachedSafeAupSnapY(
            in AbsoluteUniversePosition savedAup,
            in float3 resolvedRuntimePosition,
            out float safeY)
        {
            safeY = 0f;

            HectonMapMagicVegetationBridge vegetationBridge = null;
            if (!WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref vegetationBridge) ||
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
            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return double.NaN;

            double3 runtimeDelta = AUPMath.AUPDeltaClamped(in savedAup, in originAup);
            if (!math.all(math.isfinite(runtimeDelta)))
                return double.NaN;

            return terrainRuntimeY - runtimeDelta.y;
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!IsFinite(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return positionAup.IsFinite();
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

            if (playerBody.TryGetComponent(out HectonPlayerMotor playerMotor) &&
                playerMotor.HydrodynamicKccOwnsCollisionAuthority)
            {
                playerMotor.MovePosition(position);
                playerMotor.SetLinearVelocity(HectonPlayerMotor.SafeVelocity(velocity));
                playerTransform.SetPositionAndRotation(position, rotation);
                return;
            }

            TeleportLegacyLoadedPlayerBody(playerBody, position, rotation, velocity);
        }

        private static void TeleportLegacyLoadedPlayerBody(
            Rigidbody body,
            Vector3 position,
            Quaternion rotation,
            Vector3 velocity)
        {
            bool wasKinematic = body.isKinematic;
            bool wasDetectingCollisions = body.detectCollisions;
            bool wasSleeping = body.IsSleeping();

            body.isKinematic = true;
            body.detectCollisions = false;
            body.ResetCenterOfMass();
            body.transform.SetPositionAndRotation(position, rotation);
            body.PublishTransform();
            body.isKinematic = wasKinematic;
            body.detectCollisions = wasDetectingCollisions;

            if (!wasKinematic)
            {
                HectonPlayerMotor playerMotor = null;
                if (body.TryGetComponent(out playerMotor))
                    playerMotor.SetLinearVelocity(HectonPlayerMotor.SafeVelocity(velocity));
                if (playerMotor == null || !playerMotor.HydrodynamicKccOwnsCollisionAuthority)
                {
                    IPhysicsService physicsService = GlobalRegistry.Physics;
                    if (physicsService != null)
                        physicsService.QueueAngularVelocitySet(body, Vector3.zero, wake: false);
                }
                if (wasSleeping)
                    body.Sleep();
                else
                    body.WakeUp();
            }
            else if (wasSleeping)
            {
                body.Sleep();
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

        private static bool HasVoxelDeltaPayloadForLoad(SaveData data, NativeArray<byte> loadedVoxelDeltaSnapshot)
        {
            if (loadedVoxelDeltaSnapshot.IsCreated && loadedVoxelDeltaSnapshot.Length > 0)
                return true;

            return HasVoxelDeltaDtoPayloadForLoad(data);
        }

        private static bool HasVoxelDeltaDtoPayloadForLoad(SaveData data)
        {
            if (data == null)
                return false;

            VoxelDeltaPersistenceDTO voxelDeltaPersistence = data.voxelDeltaPersistence;
            return voxelDeltaPersistence.chunkCount > 0 ||
                   voxelDeltaPersistence.totalCellCount > 0;
        }

        private static bool HasLoadableVoxelDeltaDtoFallback(SaveData data)
        {
            if (data == null)
                return false;

            VoxelDeltaPersistenceDTO voxelDeltaPersistence = data.voxelDeltaPersistence;
            return voxelDeltaPersistence.chunkCount > 0 &&
                   VoxelDeltaProcessor.TryValidateSaveDataForLoad(data, out _);
        }

        public async Awaitable LoadGameAsync(string slotName)
        {
            CachePersistentDataPathRoot();
            uint operationId = ResolveOperationId(0u);
            byte slotIndex = ResolveManualSlotIndex(slotName);
            LastOperationSucceeded = false;
            LastOperationError = string.Empty;
            LastOperationSlot = string.Empty;
            LastLoadUsedBackup = false;
            LastLoadBackupGeneration = 0;
            LastLoadSelfRepaired = false;
            LastLoadUsedLegacyCompression = false;

            if (_runtimeOwnerAborted || !_serviceRegistered)
            {
                uint unavailableSlotHash = ResolveUnavailableSlotContext(slotName, byte.MaxValue, out string unavailableSlotName);
                LastOperationError = SaveServiceUnavailableReason;
                LastOperationSlot = unavailableSlotName;
                SaveEvents.TryRaiseLoadFailed(unavailableSlotHash, SaveEvents.ComputeMessageHash(SaveServiceUnavailableReason), SaveServiceUnavailableReason);
                PublishSaveStatus(unavailableSlotHash, operationId, SaveStatusSignal.Rejected, 0f, LoadFailureStatusFlags);
                return;
            }

            if (!TryResolveSafeSlotName(slotName, out slotName))
            {
                LastOperationError = InvalidSlotNameReason;
                LogWarning("[SaveManager] Ignored load request: invalid slot name.");
                SaveEvents.TryRaiseLoadFailed(0u, SaveEvents.ComputeMessageHash(InvalidSlotNameReason), InvalidSlotNameReason);
                PublishSaveStatus(slotIndex, operationId, SaveStatusSignal.Rejected, 0f, LoadFailureStatusFlags);
                return;
            }

            slotIndex = ResolveManualSlotIndex(slotName);
            LastOperationSlot = slotName;

            if (_isBusy)
            {
                const string reason = "Load already in progress.";
                LastOperationError = reason;
                LogWarning($"[SaveManager] Ignored load request for '{slotName}': {reason}");
                SaveEvents.TryRaiseLoadFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(reason), reason);
                PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Rejected, 0f, LoadFailureStatusFlags);
                return;
            }

            if (!SaveExists(slotName))
            {
                string reason = $"No primary or backup save found for '{slotName}'.";
                LastOperationError = reason;
                LogWarning($"[SaveManager] {reason}");
                SaveEvents.TryRaiseLoadFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(reason), reason);
                PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Rejected, 0f, LoadFailureStatusFlags);
                return;
            }

            _isBusy = true;
            // A new load supersedes any payload still retained for owners that never came back.
            ClearPendingOwnerHydration();
            Exception startupException = null;
            NotifyMacroDatabasePersistenceGateBestEffort(true, ref startupException);
            if (startupException != null)
            {
                const string reason = "Load persistence gate request failed.";
                LastOperationError = reason;
                LogWarningBestEffort($"[SaveManager] Load failed for '{slotName}': {reason}");
                _isBusy = false;
                NotifyMacroDatabasePersistenceGateBestEffort(false, ref startupException);
                ReportPersistenceCleanupFailure("load", startupException);
                SaveEvents.TryRaiseLoadFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(reason), reason);
                PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Failed, 1f, LoadFailureStatusFlags);
                return;
            }

            var totalTimer = Stopwatch.StartNew();
            NativeArray<byte> loadedVoxelDeltaSnapshot = default;
            NativeArray<SaveLoadCandidate> candidates = default;
            int candidateCount = 0;

            try
            {
                SaveEvents.TryRaiseLoadStarted(SaveEvents.ComputeSlotHash(slotName));
                PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.InProgress, 0.08f, LoadStatusFlags);
                ReportLoadPipelineStage(LoadingPipelineStage.PagingSectors, 0.08f);
                EnsureSavePayloadBuffer();
                EnsureLoadCandidateScratch();
                candidates = _loadCandidateScratch;
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
                ushort loadedPlayerDialogueChoiceFlags = 0;
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
                        out ushort candidatePlayerDialogueChoiceFlags,
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
                                DisposeTransientNativeArrayBestEffortAndReport(ref candidateVoxelDeltaSnapshot, "load", "loadedVoxelDeltaSnapshot");

                            if (!TryLoadAndPromoteCriticalBackup(
                                    slotName,
                                    "Indexed primary sector recovered from backup sector during load.",
                                    out candidateData,
                                    out candidateQuestHeader,
                                    out candidateQuestStateWords,
                                    out candidateWorldDeltas,
                                    out candidateEcosystemSectors,
                                    out candidateVoxelDeltaSnapshot,
                                    out candidatePlayerDialogueChoiceFlags,
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
                        loadedPlayerDialogueChoiceFlags = candidatePlayerDialogueChoiceFlags;
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
                            out ushort recoveryPlayerDialogueChoiceFlags,
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
                        loadedPlayerDialogueChoiceFlags = recoveryPlayerDialogueChoiceFlags;
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
                {
                    string loadFailure = string.IsNullOrEmpty(lastErrorMessage)
                        ? "No load candidate could be restored."
                        : lastErrorMessage;
                    await Awaitable.MainThreadAsync();
                    RecordFailure(slotName, "load", loadFailure);
                    LastOperationError = loadFailure;
                    LogError("[SaveManager] Load failed: " + loadFailure);
                    SaveEvents.TryRaiseLoadFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(loadFailure), loadFailure);
                    PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Failed, 1f, LoadFailureStatusFlags);
                    HideLoadingPipelineScreen();
                    return;
                }

                await Awaitable.MainThreadAsync();
                ReportLoadPipelineStage(LoadingPipelineStage.HydratingEntities, 0.42f);

                if (SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary))
                {
                    LogInfo($"[SaveManager] Migrated save '{slotName}' from v{originalVersion}: {summary}");
                }

                ValidateRuntimeWorldSeed(data);
                TryRestoreWaterCalibrationFromSave(data);
                ValidateProceduralTerrainIdentity(data);
                PersistentWorldRegistry persistentWorldRegistryForLoad = GlobalRegistry.PersistentWorldRegistry;
                string loadedRelativeSavePath = GetCandidateSavePath(slotName, loadedCandidate);

                _registryDirty = true;
                SortRegistryIfDirty(LoadPriorityComparer);
                ReportLoadPriorityConflictsForLoad(slotName);

                var voxelRestoreResult = await TryRestoreVoxelDataAsync(slotName, slotIndex, operationId, data, loadedVoxelDeltaSnapshot);
                if (!voxelRestoreResult.success)
                {
                    return;
                }

                if (voxelRestoreResult.rejected && loadedVoxelDeltaSnapshot.IsCreated)
                    DisposeTransientNativeArrayBestEffortAndReport(ref loadedVoxelDeltaSnapshot, "load", "loadedVoxelDeltaSnapshot");

                StageIntegrityPayload(_savePayloadBuffer, loadedPayloadLength, loadedPayloadHash64, slotName);
                _totalPlayTime = data.totalPlayTime;
                _sessionStartTime = Time.realtimeSinceStartupAsDouble;
                persistentWorldRegistryForLoad?.PreloadTombstonesFromLoadedRecords(loadedWorldDeltas);
                ModSaveStateStore.LoadFromSaveData(data);
                if (!ModSaveStateStore.TryLoadMmfPayloads(GetPersistentAbsolutePath(loadedRelativeSavePath), out string modPayloadLoadError) ||
                    !string.IsNullOrEmpty(modPayloadLoadError))
                {
                    ReportModPayloadLoadFailure(slotName, modPayloadLoadError);
                }

                Volatile.Write(
                    ref _playerDialogueChoiceFlags,
                    loadedPlayerDialogueChoiceFlags);
                QuestManager.StageLoadedPackedState(loadedQuestHeader, loadedQuestStateWords);

                ReportSaveOwnerCensus(slotName, operationId, isLoadOperation: true);
                uint appliedOwnerCategories = 0u;
                int appliedOwnerCount = 0;
                long loadApplyDeadlineTicks = HydrationScheduler.CreateDeadlineTicks();
                for (int i = 0; i < _saveableCount; i++)
                {
                    ISaveable saveable = _saveables[i];
                    if (!IsAlive(saveable) || saveable is VoxelDeltaProcessor)
                        continue;

                    saveable.LoadFromSaveData(data);
                    appliedOwnerCount++;
                    appliedOwnerCategories |= ClassifySaveOwnerCategories(saveable);
                    if (i + 1 < _saveableCount && Stopwatch.GetTimestamp() >= loadApplyDeadlineTicks)
                    {
                        await HydrationScheduler.NextFrameAsync(destroyCancellationToken);
                        loadApplyDeadlineTicks = HydrationScheduler.CreateDeadlineTicks();
                    }
                }

                // Derived from what actually consumed the payload, not from the pre-loop snapshot, so an
                // owner that registered part-way through the awaited apply loop is not hydrated twice.
                LastLoadAppliedOwnerCount = appliedOwnerCount;
                ArmDeferredOwnerHydration(
                    data,
                    ComputeSlotHash(slotName),
                    operationId,
                    SaveOwnerCensus.ResolveMissingCategories(appliedOwnerCategories));

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

                    int suppressedResourceNodes = Hecton8.Scavenging.ResourceNode.ApplyPersistentWorldRegistryStateToRegisteredNodes(persistentWorldRegistryForLoad);
                    if (suppressedResourceNodes > 0)
                        LogInfo($"[SaveManager] Suppressed {suppressedResourceNodes} resource nodes after persistent world registry restore.");
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
                    SaveMetadata repairMetadata = loadedMetadata ?? new SaveMetadata
                    {
                        SlotName = slotName,
                        GameVersion = Application.version,
                        Timestamp = DateTime.UtcNow.Ticks,
                        PlayTimeSeconds = (float)data.totalPlayTime,
                        SceneName = SaveMetadata.NormalizeSceneName(activeSceneName),
                        PlayerPosition = playerPosition,
                        WorldSeed = data.ecosystemState.worldSeed,
                        WorldGenerationVersionId = data.ecosystemState.worldGenerationVersionId
                    };
                    int repairBackupRetention = GetBackupRetentionCount(slotName);
                    RepairPrimaryArtifactsArgs repairArgs = new RepairPrimaryArtifactsArgs(
                        slotName: slotName,
                        data: data,
                        metadataSource: repairMetadata,
                        packedQuestHeader: loadedQuestHeader,
                        packedQuestStateWords: loadedQuestStateWords,
                        playerDialogueChoiceFlags: PlayerDialogueChoiceFlags,
                        persistentWorldItems: loadedWorldDeltas,
                        ecosystemSectorStates: loadedEcosystemSectors,
                        voxelDeltaSnapshot: loadedVoxelDeltaSnapshot,
                        backupRetentionCount: repairBackupRetention,
                        overwritePrimarySave: true
                    );
                    await Awaitable.BackgroundThreadAsync();
                    repairedPrimaryArtifacts = RepairPrimaryArtifacts(in repairArgs);
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
                if (LastLoadUsedBackup || LastLoadSelfRepaired)
                    PublishSaveRecoveredNotification(slotName);

                LastOperationSucceeded = true;
                string loadCompletionSuffix = criticalBackupPromotedForLoad
                    ? " and promoted .bak to primary."
                    : (repairedPrimaryArtifacts ? " and self-repaired primary artifacts." : (appliedSafeAupSnap ? " and snapped player to safe terrain." : "."));
                ReportLoadPipelineStage(LoadingPipelineStage.Completed, 1f);
                LogInfo($"[SaveManager] Loaded '{slotName}' from {sourceLabel} in {totalTimer.ElapsedMilliseconds}ms{loadCompletionSuffix}");
                PublishSaveCompletedForSlotName(slotIndex, slotName, new PublishSaveCompletedArgs(operationId: operationId, durationMs: totalTimer.ElapsedMilliseconds, compressedSizeBytes: 0L, succeeded: true));
                PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Completed, 1f, LoadStatusFlags);
                RaiseLoadCompletedWithBackpressureRecovery(SaveEvents.ComputeSlotHash(slotName));
            }
            catch (Exception ex)
            {
                await Awaitable.MainThreadAsync();
                RecordFailure(slotName, "load", ex.Message);
                LastOperationError = ex.Message;
                LogError("[SaveManager] Load failed: " + ex);
                SaveEvents.TryRaiseLoadFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(ex.Message), ex.Message);
                PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Failed, 1f, LoadFailureStatusFlags);
                HideLoadingPipelineScreen();
            }
            finally
            {
                Exception cleanupException = null;

                try
                {
                    ClearLoadCandidates(candidates, candidateCount);
                }
                catch (Exception exception)
                {
                    CaptureFirstCleanupException(ref cleanupException, exception);
                }

                if (loadedVoxelDeltaSnapshot.IsCreated)
                    DisposeTransientNativeArrayBestEffort(ref loadedVoxelDeltaSnapshot, ref cleanupException, sentinelLabel: "loadedVoxelDeltaSnapshot");

                _isBusy = false;
                NotifyMacroDatabasePersistenceGateBestEffort(false, ref cleanupException);
                ReportPersistenceCleanupFailure("load", cleanupException);
            }
        }

        private async Awaitable<(bool success, bool rejected)> TryRestoreVoxelDataAsync(string slotName, byte slotIndex, uint operationId, SaveData data, NativeArray<byte> loadedVoxelDeltaSnapshot)
        {
            VoxelDeltaProcessor voxelDeltaProcessor = null;
            for (int i = 0; i < _saveableCount; i++)
            {
                ISaveable saveable = _saveables[i];
                if (!IsAlive(saveable))
                    continue;

                if (saveable is VoxelDeltaProcessor loadedVoxelDeltaProcessor)
                {
                    voxelDeltaProcessor = loadedVoxelDeltaProcessor;
                    break;
                }
            }

            if (voxelDeltaProcessor != null)
            {
                return await RestoreVoxelProcessorDataAsync(slotName, slotIndex, operationId, data, loadedVoxelDeltaSnapshot, voxelDeltaProcessor);
            }
            else if (HasVoxelDeltaPayloadForLoad(data, loadedVoxelDeltaSnapshot))
            {
                return await FailVoxelDataLoadAsync(slotName, slotIndex, operationId, "Voxel delta payload exists, but no VoxelDeltaProcessor is registered for load.", false, false);
            }

            return (true, false);
        }

        private async Awaitable<(bool success, bool rejected)> RestoreVoxelProcessorDataAsync(string slotName, byte slotIndex, uint operationId, SaveData data, NativeArray<byte> loadedVoxelDeltaSnapshot, VoxelDeltaProcessor voxelDeltaProcessor)
        {
            bool loadedVoxelDeltaSnapshotRejectedForLoad = false;

            if (loadedVoxelDeltaSnapshot.IsCreated && loadedVoxelDeltaSnapshot.Length > 0)
            {
                NativeArray<byte> rollbackVoxelDeltaSnapshot = default;
                bool rollbackVoxelDeltaSnapshotAcquired = false;
                try
                {
                    bool rollbackVoxelDeltaSnapshotCopied = voxelDeltaProcessor.TryCopyNativeSnapshotToBorrowedScratch(
                        out rollbackVoxelDeltaSnapshot,
                        out int rollbackVoxelDeltaSnapshotBytes);
                    if (rollbackVoxelDeltaSnapshotCopied && rollbackVoxelDeltaSnapshotBytes > 0)
                    {
                        rollbackVoxelDeltaSnapshotAcquired = true;
                    }
                    else if (!rollbackVoxelDeltaSnapshotCopied && rollbackVoxelDeltaSnapshotBytes > 0)
                    {
                        return await FailVoxelDataLoadAsync(slotName, slotIndex, operationId, "Voxel delta rollback snapshot copy failed before load.", true, loadedVoxelDeltaSnapshotRejectedForLoad);
                    }

                    if (!voxelDeltaProcessor.TryLoadNativeSnapshot(loadedVoxelDeltaSnapshot, out string voxelLoadError))
                    {
                        loadedVoxelDeltaSnapshotRejectedForLoad = true;
                        if (HasLoadableVoxelDeltaDtoFallback(data))
                        {
                            string fallbackReason = string.IsNullOrEmpty(voxelLoadError)
                                ? "Voxel delta native snapshot load failed."
                                : voxelLoadError;
                            LogWarning("[SaveManager] Voxel delta native snapshot rejected; falling back to binary voxel payload: " + fallbackReason);
                            if (!voxelDeltaProcessor.TryLoadFromSaveData(data, out string voxelFallbackError))
                            {
                                TryRestoreRollbackSnapshot(voxelDeltaProcessor, rollbackVoxelDeltaSnapshotAcquired, rollbackVoxelDeltaSnapshot, "fallback payload");

                                string loadFailure = string.IsNullOrEmpty(voxelFallbackError)
                                    ? "Voxel delta binary payload load failed."
                                    : voxelFallbackError;
                                return await FailVoxelDataLoadAsync(slotName, slotIndex, operationId, loadFailure, true, loadedVoxelDeltaSnapshotRejectedForLoad);
                            }
                        }
                        else
                        {
                            TryRestoreRollbackSnapshot(voxelDeltaProcessor, rollbackVoxelDeltaSnapshotAcquired, rollbackVoxelDeltaSnapshot, "load snapshot");

                            string loadFailure = string.IsNullOrEmpty(voxelLoadError)
                                ? "Voxel delta native snapshot load failed."
                                : voxelLoadError;
                            return await FailVoxelDataLoadAsync(slotName, slotIndex, operationId, loadFailure, true, loadedVoxelDeltaSnapshotRejectedForLoad);
                        }
                    }
                }
                finally
                {
                    if (rollbackVoxelDeltaSnapshotAcquired)
                        voxelDeltaProcessor.ReleaseBorrowedNativeSnapshotScratch();
                }
            }
            else
            {
                if (!voxelDeltaProcessor.TryLoadFromSaveData(data, out string voxelFallbackError))
                {
                    string loadFailure = string.IsNullOrEmpty(voxelFallbackError)
                        ? "Voxel delta binary payload load failed."
                        : voxelFallbackError;
                    return await FailVoxelDataLoadAsync(slotName, slotIndex, operationId, loadFailure, true, loadedVoxelDeltaSnapshotRejectedForLoad);
                }
            }

            return (true, loadedVoxelDeltaSnapshotRejectedForLoad);
        }

        private async Awaitable<(bool success, bool rejected)> FailVoxelDataLoadAsync(string slotName, byte slotIndex, uint operationId, string message, bool requiresMainThread, bool rejected)
        {
            if (requiresMainThread)
                await Awaitable.MainThreadAsync();
            RecordFailure(slotName, "load", message);
            LastOperationError = message;
            LogError("[SaveManager] Load failed: " + message);
            SaveEvents.TryRaiseLoadFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(message), message);
            PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Failed, 1f, LoadFailureStatusFlags);
            HideLoadingPipelineScreen();
            return (false, rejected);
        }

        private void TryRestoreRollbackSnapshot(VoxelDeltaProcessor voxelDeltaProcessor, bool rollbackAcquired, NativeArray<byte> rollbackSnapshot, string failureContext)
        {
            bool rollbackRestoreSucceeded = false;
            if (rollbackAcquired)
            {
                if (voxelDeltaProcessor.TryLoadNativeSnapshot(rollbackSnapshot, out string rollbackError))
                {
                    rollbackRestoreSucceeded = true;
                }
                else
                {
                    LogError($"[SaveManager] Failed to restore voxel state after rejected {failureContext}: {rollbackError}");
                }
            }

            if (!rollbackRestoreSucceeded)
                voxelDeltaProcessor.LoadFromSaveData(null);
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

            string persistentPath = HectonPersistentPathPolicy.RootPath;
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

            string persistentPath = HectonPersistentPathPolicy.RootPath;
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
                root = HectonPersistentPathPolicy.RootPath;
                s_persistentDataPathRoot = root;
                SaveSidecarStorage.SetPersistentDataPathRoot(root);
            }

            return Path.Combine(root, NormalizePersistentRelativeSegment(relativePath));
        }

        private static string NormalizePersistentRelativeSegment(string segment)
        {
            if (string.IsNullOrEmpty(segment))
                return string.Empty;

            string normalized = segment
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return normalized.IndexOf("..", StringComparison.Ordinal) >= 0
                ? Path.GetFileName(normalized)
                : normalized;
        }

        private static void CachePersistentDataPathRoot()
        {
            string root = HectonPersistentPathPolicy.RootPath;
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
            if (string.IsNullOrEmpty(path))
                return;

            string absolutePath = GetPersistentAbsolutePath(path);
            AsyncWriteManager.InvalidateCachedReadWindows(absolutePath);
            try
            {
                if (File.Exists(absolutePath))
                    File.Delete(absolutePath);
            }
            finally
            {
                AsyncWriteManager.InvalidateCachedReadWindows(absolutePath);
            }
        }

        private static bool TryDeleteAbsoluteFileIfExists(string absolutePath, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(absolutePath))
                return true;

            AsyncWriteManager.InvalidateCachedReadWindows(absolutePath);
            try
            {
                if (File.Exists(absolutePath))
                    File.Delete(absolutePath);
                return true;
            }
            catch (Exception exception) when (exception is IOException ||
                                             exception is UnauthorizedAccessException ||
                                             exception is ArgumentException ||
                                             exception is NotSupportedException ||
                                             exception is System.Security.SecurityException)
            {
                error = exception.Message;
                return false;
            }
            finally
            {
                AsyncWriteManager.InvalidateCachedReadWindows(absolutePath);
            }
        }

        private static bool TryStageFileCopyForPromotion(string absoluteSourcePath, string absoluteTempPath, long expectedBytes, out string error)
        {
            error = string.Empty;
            if (!TryDeleteAbsoluteFileIfExists(absoluteTempPath, out error))
                return false;

            AsyncWriteManager.InvalidateCachedReadWindows(absoluteSourcePath);
            AsyncWriteManager.InvalidateCachedReadWindows(absoluteTempPath);
            try
            {
                string directory = Path.GetDirectoryName(absoluteTempPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                File.Copy(absoluteSourcePath, absoluteTempPath, true);
            }
            catch (Exception exception) when (exception is IOException ||
                                             exception is UnauthorizedAccessException ||
                                             exception is ArgumentException ||
                                             exception is NotSupportedException ||
                                             exception is System.Security.SecurityException)
            {
                error = exception.Message;
                TryDeleteAbsoluteFileIfExists(absoluteTempPath, out _);
                return false;
            }
            finally
            {
                AsyncWriteManager.InvalidateCachedReadWindows(absoluteSourcePath);
                AsyncWriteManager.InvalidateCachedReadWindows(absoluteTempPath);
            }

            if (!AsyncWriteManager.TryGetFileLength(absoluteTempPath, out long tempBytes, out string tempLengthError))
            {
                error = string.IsNullOrEmpty(tempLengthError)
                    ? "Staged backup rotation temp file length could not be resolved."
                    : tempLengthError;
                TryDeleteAbsoluteFileIfExists(absoluteTempPath, out _);
                return false;
            }

            if (tempBytes != expectedBytes)
            {
                error = "Staged backup rotation temp file length did not match source.";
                TryDeleteAbsoluteFileIfExists(absoluteTempPath, out _);
                return false;
            }

            if (!AsyncWriteManager.FlushCriticalSavePath(absoluteTempPath, tempBytes, out string flushError))
            {
                error = string.IsNullOrEmpty(flushError)
                    ? "Staged backup rotation temp critical flush failed."
                    : flushError;
                TryDeleteAbsoluteFileIfExists(absoluteTempPath, out _);
                return false;
            }

            return true;
        }

        private static bool TryPromoteStagedFile(string absoluteTempPath, string absoluteTargetPath, long expectedBytes, out string error)
        {
            error = string.Empty;
            AsyncWriteManager.InvalidateCachedReadWindows(absoluteTempPath);
            AsyncWriteManager.InvalidateCachedReadWindows(absoluteTargetPath);
            try
            {
                string directory = Path.GetDirectoryName(absoluteTargetPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                if (File.Exists(absoluteTargetPath))
                    File.Replace(absoluteTempPath, absoluteTargetPath, null, true);
                else
                    File.Move(absoluteTempPath, absoluteTargetPath);
            }
            catch (Exception exception) when (exception is IOException ||
                                             exception is UnauthorizedAccessException ||
                                             exception is ArgumentException ||
                                             exception is NotSupportedException ||
                                             exception is PlatformNotSupportedException ||
                                             exception is System.Security.SecurityException)
            {
                error = exception.Message;
                return false;
            }
            finally
            {
                AsyncWriteManager.InvalidateCachedReadWindows(absoluteTempPath);
                AsyncWriteManager.InvalidateCachedReadWindows(absoluteTargetPath);
            }

            if (!AsyncWriteManager.TryGetFileLength(absoluteTargetPath, out long targetBytes, out string lengthError))
            {
                error = string.IsNullOrEmpty(lengthError)
                    ? "Promoted backup rotation target length could not be resolved."
                    : lengthError;
                return false;
            }

            if (targetBytes != expectedBytes)
            {
                error = "Promoted backup rotation target length did not match source.";
                return false;
            }

            if (!AsyncWriteManager.FlushCriticalSavePath(absoluteTargetPath, targetBytes, out string flushError))
            {
                error = string.IsNullOrEmpty(flushError)
                    ? "Promoted backup rotation target critical flush failed."
                    : flushError;
                return false;
            }

            return true;
        }

        public static string[] GetAllKnownArtifactPaths(string slotName)
        {
            int maxGeneration = GetMaxBackupGenerationCount();
            string[] paths = new string[3 + maxGeneration]; // COLD COMPAT ALLOC: string[][artifact count] - legacy editor/API return buffer - owner: SaveManager
            CollectAllKnownArtifactPaths(slotName, paths);
            return paths;
        }

        public static int CollectAllKnownArtifactPaths(string slotName, string[] paths)
        {
            if (paths == null || paths.Length == 0)
                return 0;

            int count = 0;
            paths[count++] = GetPrimarySaveFilePath(slotName);
            if (count >= paths.Length)
                return count;

            paths[count++] = GetTempSaveFilePath(slotName);
            if (count >= paths.Length)
                return count;

            paths[count++] = SaveSlotMaintenanceRecord.GetPath(slotName);
            if (count >= paths.Length)
                return count;

            int maxGeneration = GetMaxBackupGenerationCount();
            for (int generation = 1; generation <= maxGeneration; generation++)
            {
                if (count >= paths.Length)
                    return count;

                paths[count++] = GetBackupSaveFilePath(slotName, generation);
            }

            return count;
        }

        private static bool TryRotateBackupChain(string primaryPath, Func<int, string> backupPathFactory, int retentionCount, out string error)
        {
            error = string.Empty;
            if (retentionCount <= 0)
                return true;

            for (int generation = retentionCount; generation >= 1; generation--)
            {
                string targetPath = backupPathFactory(generation);
                string sourcePath = generation == 1 ? primaryPath : backupPathFactory(generation - 1);
                string absoluteTargetPath = GetPersistentAbsolutePath(targetPath);
                string absoluteSourcePath = GetPersistentAbsolutePath(sourcePath);
                string absoluteTempPath = absoluteTargetPath + ".rotate.tmp";

                if (!File.Exists(absoluteSourcePath))
                {
                    if (!TryDeleteAbsoluteFileIfExists(absoluteTempPath, out error))
                        return false;
                    if (!TryDeleteAbsoluteFileIfExists(absoluteTargetPath, out error))
                        return false;
                    continue;
                }

                AsyncWriteManager.InvalidateCachedReadWindows(absoluteSourcePath);
                AsyncWriteManager.InvalidateCachedReadWindows(absoluteTargetPath);
                if (!AsyncWriteManager.TryGetFileLength(absoluteSourcePath, out long sourceBytes, out string sourceLengthError))
                {
                    error = string.IsNullOrEmpty(sourceLengthError)
                        ? "Backup rotation source save file length could not be resolved."
                        : sourceLengthError;
                    return false;
                }

                if (!TryStageFileCopyForPromotion(absoluteSourcePath, absoluteTempPath, sourceBytes, out error))
                    return false;

                if (!TryPromoteStagedFile(absoluteTempPath, absoluteTargetPath, sourceBytes, out error))
                {
                    TryDeleteAbsoluteFileIfExists(absoluteTempPath, out _);
                    return false;
                }
            }

            int maxGeneration = GetMaxBackupGenerationCount();
            for (int generation = retentionCount + 1; generation <= maxGeneration; generation++)
            {
                string absoluteStaleBackupPath = GetPersistentAbsolutePath(backupPathFactory(generation));
                if (!TryDeleteAbsoluteFileIfExists(absoluteStaleBackupPath, out error))
                    return false;
            }

            return true;
        }

        private static bool TryCommitTempSaveToPrimary(string slotName, string tempPath, string finalPath, int backupRetentionCount, out string error)
        {
            error = string.Empty;
            if (!FileExists(tempPath))
            {
                error = "Verified temp save was not found during final rotation.";
                return false;
            }

            try
            {
                // Step 5: rotate the previously committed primary into the backup chain before overwrite.
                if (!TryRotateBackupChain(finalPath, generation => GetBackupSaveFilePath(slotName, generation), math.clamp(backupRetentionCount, 1, 8), out error))
                    return false;

                // Step 6: promote the verified temp artifact to the authoritative primary slot.
                string absoluteTempPath = GetPersistentAbsolutePath(tempPath);
                string absoluteFinalPath = GetPersistentAbsolutePath(finalPath);
                if (!AsyncWriteManager.TryGetFileLength(absoluteTempPath, out long tempBytesBeforePromotion, out string tempLengthError))
                {
                    error = string.IsNullOrEmpty(tempLengthError)
                        ? "Verified temp save file length could not be resolved before final promotion."
                        : tempLengthError;
                    return false;
                }

                if (!TryPromoteStagedFile(absoluteTempPath, absoluteFinalPath, tempBytesBeforePromotion, out error))
                    return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }

            // Step 7: primary must exist after promotion.
            if (!FileExists(finalPath))
            {
                error = "Primary save promotion failed.";
                return false;
            }

            // Step 8: temp must be fully consumed after promotion.
            if (FileExists(tempPath))
            {
                error = "Temp save cleanup failed.";
                return false;
            }

            return true;
        }

        private static bool TryExecuteVerifiedSavePipeline(
            string slotName,
            string tempPath,
            string finalPath,
            SaveMetadata metadata,
            SaveData data,
            NativeArray<PersistentWorldDeltaRecord>.ReadOnly persistentWorldItems,
            NativeArray<EcosystemSectorSaveRecord>.ReadOnly ecosystemSectorStates,
            QuestSaveHeader packedQuestHeader,
            NativeArray<uint> packedQuestStateWords,
            ushort playerDialogueChoiceFlags,
            NativeArray<byte> voxelDeltaSnapshot,
            NativeArray<byte> rawBuffer,
            NativeArray<byte> compressedBuffer,
            int backupRetentionCount,
            out ulong payloadHash64,
            out int rawPayloadLength,
            out long compressedSizeBytes,
            out string error)
        {
            payloadHash64 = 0UL;
            rawPayloadLength = 0;
            compressedSizeBytes = 0L;
            error = string.Empty;
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
                    playerDialogueChoiceFlags,
                    voxelDeltaSnapshot,
                    rawBuffer,
                    compressedBuffer,
                    out payloadHash64,
                    out rawPayloadLength,
                    out string writeError))
            {
                error = writeError;
                return false;
            }

            // Step 4: the writer already re-reads metadata internally, but the pipeline still requires the temp artifact to exist here.
            if (!FileExists(tempPath))
            {
                error = "Verified temp save was not created by the binary writer.";
                return false;
            }

            if (!ModSaveStateStore.TryCommitMmfPayloads(absoluteTempPath, out string modPayloadCommitError) ||
                !string.IsNullOrEmpty(modPayloadCommitError))
            {
                ReportModPayloadCommitFailure(slotName, modPayloadCommitError);
                error = string.IsNullOrEmpty(modPayloadCommitError)
                    ? "Mod payload commit failed."
                    : modPayloadCommitError;
                return false;
            }

            compressedSizeBytes = TryGetAbsoluteFileLength(absoluteTempPath, out long tempBytes) ? tempBytes : 0L;
            return TryCommitTempSaveToPrimary(slotName, tempPath, finalPath, backupRetentionCount, out error);
        }

        private static void ReportModPayloadLoadFailure(string slotName, string error)
        {
            string message = string.IsNullOrEmpty(error)
                ? "Mod payload load fallback used."
                : error;

            PublishPerformanceWarningBestEffort(ModPayloadLoadFallbackTelemetryHash, ComputeSlotHash(slotName), 1f);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogWarning($"[SaveManager] Mod payload load warning for '{slotName}': {message}");
#endif
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

        private struct RepairCandidateResult
        {
            public SaveData Data;
            public QuestSaveHeader PackedQuestHeader;
            public uint[] PackedQuestStateWords;
            public PersistentWorldDeltaRecord[] PersistentWorldItems;
            public EcosystemSectorSaveRecord[] EcosystemSectorStates;
            public NativeArray<byte> VoxelDeltaSnapshot;
            public SaveMetadata Metadata;
            public SaveLoadCandidate Candidate;
            public bool UsedLegacyFormat;
            public ushort PlayerDialogueChoiceFlags;
            public string ErrorMessage;
        }

        private readonly struct RepairPrimaryArtifactsArgs
        {
            public readonly string SlotName;
            public readonly SaveData Data;
            public readonly SaveMetadata MetadataSource;
            public readonly QuestSaveHeader PackedQuestHeader;
            public readonly uint[] PackedQuestStateWords;
            public readonly ushort PlayerDialogueChoiceFlags;
            public readonly PersistentWorldDeltaRecord[] PersistentWorldItems;
            public readonly EcosystemSectorSaveRecord[] EcosystemSectorStates;
            public readonly NativeArray<byte> VoxelDeltaSnapshot;
            public readonly int BackupRetentionCount;
            public readonly bool OverwritePrimarySave;

            public RepairPrimaryArtifactsArgs(
                string slotName,
                SaveData data,
                SaveMetadata metadataSource,
                QuestSaveHeader packedQuestHeader,
                uint[] packedQuestStateWords,
                ushort playerDialogueChoiceFlags,
                PersistentWorldDeltaRecord[] persistentWorldItems,
                EcosystemSectorSaveRecord[] ecosystemSectorStates,
                NativeArray<byte> voxelDeltaSnapshot,
                int backupRetentionCount,
                bool overwritePrimarySave)
            {
                SlotName = slotName;
                Data = data;
                MetadataSource = metadataSource;
                PackedQuestHeader = packedQuestHeader;
                PackedQuestStateWords = packedQuestStateWords;
                PlayerDialogueChoiceFlags = playerDialogueChoiceFlags;
                PersistentWorldItems = persistentWorldItems;
                EcosystemSectorStates = ecosystemSectorStates;
                VoxelDeltaSnapshot = voxelDeltaSnapshot;
                BackupRetentionCount = backupRetentionCount;
                OverwritePrimarySave = overwritePrimarySave;
            }
        }

        private static bool TryFindValidRepairCandidate(string slotName, out RepairCandidateResult result)
        {
            result = new RepairCandidateResult();
            bool foundValid = false;

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
                            out ushort candidatePlayerDialogueChoiceFlags,
                            out SaveMetadata loadedCandidateMetadata,
                            out _,
                            out _,
                            out bool candidateUsedLegacyFormat,
                            out _,
                            out string candidateError))
                        {
                            result.Data = candidateData;
                            result.PackedQuestHeader = candidateQuestHeader;
                            result.PackedQuestStateWords = candidatePackedQuestStateWords;
                            result.PersistentWorldItems = candidateWorldItems;
                            result.EcosystemSectorStates = candidateEcosystemSectorStates;
                            result.VoxelDeltaSnapshot = candidateVoxelDeltaSnapshot;
                            result.Metadata = loadedCandidateMetadata;
                            result.Candidate = candidate;
                            result.UsedLegacyFormat = candidateUsedLegacyFormat;
                            result.PlayerDialogueChoiceFlags = candidatePlayerDialogueChoiceFlags;
                            foundValid = true;
                            break;
                        }

                        result.ErrorMessage = candidateError;
                    }
                }
                finally
                {
                    ClearLoadCandidates(candidates, candidateCount);
                }
            }
            return foundValid;
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

            TryFindValidRepairCandidate(slotName, out RepairCandidateResult candidateResult);

            SaveMetadata metadataSource = candidateResult.Metadata ?? beforeInfo.Metadata;

            if (candidateResult.Data == null)
            {
                if (candidateResult.VoxelDeltaSnapshot.IsCreated)
                    DisposeTransientNativeArrayBestEffortAndReport(ref candidateResult.VoxelDeltaSnapshot, "load", "loadedVoxelDeltaSnapshot");

                result.Message = string.IsNullOrEmpty(candidateResult.ErrorMessage)
                    ? "No valid save candidate could be repaired."
                    : candidateResult.ErrorMessage;
                result.IntegrityAfter = beforeInfo.IntegrityState;
                return false;
            }

            bool shouldRewritePrimarySave = candidateResult.Candidate.IsBackup
                || !FileExists(GetPrimarySaveFilePath(slotName))
                || candidateResult.UsedLegacyFormat;

            bool shouldRewritePrimaryMetadata = shouldRewritePrimarySave
                || metadataSource == null;

            RepairPrimaryArtifactsArgs repairArgs = new RepairPrimaryArtifactsArgs(
                slotName: slotName,
                data: candidateResult.Data,
                metadataSource: metadataSource,
                packedQuestHeader: candidateResult.PackedQuestHeader,
                packedQuestStateWords: candidateResult.PackedQuestStateWords,
                playerDialogueChoiceFlags: candidateResult.PlayerDialogueChoiceFlags,
                persistentWorldItems: candidateResult.PersistentWorldItems,
                ecosystemSectorStates: candidateResult.EcosystemSectorStates,
                voxelDeltaSnapshot: candidateResult.VoxelDeltaSnapshot,
                backupRetentionCount: GetBackupRetentionCountStatic(slotName),
                overwritePrimarySave: shouldRewritePrimarySave
            );
            bool changedAnything = RepairPrimaryArtifacts(in repairArgs);

            SaveSlotInfo afterInfo = BuildSaveSlotInfoInternal(slotName);

            result.Success = true;
            result.ChangedAnything = changedAnything;
            result.UsedBackupSource = candidateResult.Candidate.IsBackup;
            result.SourceBackupGeneration = candidateResult.Candidate.IsBackup ? candidateResult.Candidate.BackupGeneration : 0;
            result.UsedLegacyCompression = candidateResult.UsedLegacyFormat;
            result.RewrotePrimarySave = shouldRewritePrimarySave;
            result.RewrotePrimaryMetadata = shouldRewritePrimaryMetadata;
            result.IntegrityAfter = afterInfo != null ? afterInfo.IntegrityState : beforeInfo.IntegrityState;
            result.Message = changedAnything
                ? "Slot repaired and normalized."
                : "Slot already healthy.";
            RecordRepairResult(result, candidateResult.Data != null ? candidateResult.Data.version : 0);

            if (candidateResult.VoxelDeltaSnapshot.IsCreated)
                DisposeTransientNativeArrayBestEffortAndReport(ref candidateResult.VoxelDeltaSnapshot, "load", "loadedVoxelDeltaSnapshot");

            return true;
        }

        private static void EvaluateAuditCandidates(
            string slotName,
            SaveSlotAuditResult result,
            out bool hasSelectedCandidate,
            out SaveLoadCandidate selectedCandidate,
            out SaveData selectedData,
            out bool selectedLegacyFormat)
        {
            hasSelectedCandidate = false;
            selectedCandidate = default;
            selectedData = null;
            selectedLegacyFormat = false;

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
                            out ushort _,
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
                                DisposeTransientNativeArrayBestEffortAndReport(ref candidateVoxelDeltaSnapshot, "load", "loadedVoxelDeltaSnapshot");
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

            EvaluateAuditCandidates(
                slotName,
                result,
                out bool hasSelectedCandidate,
                out SaveLoadCandidate selectedCandidate,
                out SaveData selectedData,
                out bool selectedLegacyFormat);

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

        private static bool TryLoadAndPromoteCriticalBackup(
            string slotName,
            string primaryError,
            out SaveData data,
            out QuestSaveHeader packedQuestHeader,
            out uint[] packedQuestStateWords,
            out PersistentWorldDeltaRecord[] persistentWorldItems,
            out EcosystemSectorSaveRecord[] ecosystemSectorStates,
            out NativeArray<byte> voxelDeltaSnapshot,
            out ushort playerDialogueChoiceFlags,
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
            playerDialogueChoiceFlags = 0;
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
                    out playerDialogueChoiceFlags,
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
                    DisposeTransientNativeArrayBestEffortAndReport(ref voxelDeltaSnapshot, "load", "loadedVoxelDeltaSnapshot");

                errorMessage = $"CRITICAL_RECOVERY rejected cascading backup-sector recovery in '{backupSavePath}'. Primary failure: {primaryError}";
                return false;
            }

            if (!TryPromoteBackupToPrimaryAfterCriticalRecovery(slotName, backupSavePath, out string promotionError))
            {
                if (voxelDeltaSnapshot.IsCreated)
                    DisposeTransientNativeArrayBestEffortAndReport(ref voxelDeltaSnapshot, "load", "loadedVoxelDeltaSnapshot");

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
                if (!AsyncWriteManager.TryGetFileLength(absoluteBackupPath, out long backupSourceBytes, out string backupLengthError))
                {
                    errorMessage = string.IsNullOrEmpty(backupLengthError)
                        ? "Critical recovery backup source file length could not be resolved."
                        : backupLengthError;
                    return false;
                }

                if (!TryCopyBackupToTempForPromotion(absoluteBackupPath, absoluteTempPath, tempSavePath, backupSourceBytes, out errorMessage))
                {
                    return false;
                }

                if (!TryCommitTempToPrimaryForPromotion(absoluteTempPath, absolutePrimaryPath, backupSourceBytes, out errorMessage))
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                try
                {
                    DeleteFileIfExists(tempSavePath);
                }
                catch (Exception cleanupEx)
                {
                    errorMessage = $"{errorMessage}; temp cleanup failed: {cleanupEx.Message}";
                }

                return false;
            }
        }

        private static bool TryCopyBackupToTempForPromotion(
            string absoluteBackupPath,
            string absoluteTempPath,
            string tempSavePath,
            long backupSourceBytes,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            AsyncWriteManager.InvalidateCachedReadWindows(absoluteTempPath);
            DeleteFileIfExists(tempSavePath);
            AsyncWriteManager.InvalidateCachedReadWindows(absoluteTempPath);
            File.Copy(absoluteBackupPath, absoluteTempPath, true);
            AsyncWriteManager.InvalidateCachedReadWindows(absoluteTempPath);

            if (!AsyncWriteManager.TryGetFileLength(absoluteTempPath, out long tempBytes, out string tempLengthError))
            {
                errorMessage = string.IsNullOrEmpty(tempLengthError)
                    ? "Critical recovery temp promoted file length could not be resolved."
                    : tempLengthError;
                DeleteFileIfExists(tempSavePath);
                return false;
            }

            if (tempBytes != backupSourceBytes)
            {
                errorMessage = "Critical recovery temp promoted file length did not match backup source.";
                DeleteFileIfExists(tempSavePath);
                return false;
            }

            if (!AsyncWriteManager.FlushCriticalSavePath(absoluteTempPath, tempBytes, out string tempFlushError))
            {
                errorMessage = string.IsNullOrEmpty(tempFlushError)
                    ? "Critical recovery temp promoted file flush failed."
                    : tempFlushError;
                DeleteFileIfExists(tempSavePath);
                return false;
            }

            return true;
        }

        private static bool TryCommitTempToPrimaryForPromotion(
            string absoluteTempPath,
            string absolutePrimaryPath,
            long backupSourceBytes,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            AsyncWriteManager.InvalidateCachedReadWindows(absoluteTempPath);
            AsyncWriteManager.InvalidateCachedReadWindows(absolutePrimaryPath);
            if (File.Exists(absolutePrimaryPath))
            {
                File.Replace(absoluteTempPath, absolutePrimaryPath, null, true);
            }
            else
            {
                File.Move(absoluteTempPath, absolutePrimaryPath);
            }
            AsyncWriteManager.InvalidateCachedReadWindows(absoluteTempPath);
            AsyncWriteManager.InvalidateCachedReadWindows(absolutePrimaryPath);

            if (File.Exists(absoluteTempPath))
            {
                try
                {
                    File.Delete(absoluteTempPath);
                }
                finally
                {
                    AsyncWriteManager.InvalidateCachedReadWindows(absoluteTempPath);
                }
            }

            if (!File.Exists(absolutePrimaryPath))
            {
                errorMessage = "Primary file was missing after atomic backup promotion.";
                return false;
            }

            if (!AsyncWriteManager.TryGetFileLength(absolutePrimaryPath, out long promotedBytes, out string lengthError))
            {
                errorMessage = string.IsNullOrEmpty(lengthError)
                    ? "Critical recovery promoted primary file length could not be resolved."
                    : lengthError;
                return false;
            }

            if (promotedBytes != backupSourceBytes)
            {
                errorMessage = "Critical recovery promoted primary length did not match backup source.";
                return false;
            }

            if (!AsyncWriteManager.FlushCriticalSavePath(absolutePrimaryPath, promotedBytes, out string flushError))
            {
                errorMessage = string.IsNullOrEmpty(flushError)
                    ? "Critical recovery promoted primary flush failed."
                    : flushError;
                return false;
            }

            return true;
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
            out ushort playerDialogueChoiceFlags,
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
            playerDialogueChoiceFlags = 0;
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
                out playerDialogueChoiceFlags,
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
            out ushort playerDialogueChoiceFlags,
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
            playerDialogueChoiceFlags = 0;
            metadata = null;
            payloadHash64 = 0UL;
            rawPayloadLength = 0;
            indexedBackupRecoveryUsed = false;
            errorMessage = string.Empty;

            AcquireWriteBuffers(
                out NativeArray<byte> readBuffer,
                out bool ownsReadBuffer,
                out NativeArray<byte> compressedReadBuffer,
                out bool ownsCompressedReadBuffer);
            NativeArray<byte> loadedVoxelDeltaSnapshot = default;
            try
            {
                string absolutePath = GetPersistentAbsolutePath(GetCandidateSavePath(slotName, candidate));
                if (!SaveBinaryStorage.TryMeasureLoadVoxelDeltaSnapshotByteLength(
                        absolutePath,
                        readBuffer,
                        compressedReadBuffer,
                        out int voxelDeltaSnapshotByteLength,
                        out errorMessage))
                {
                    return false;
                }

                if (voxelDeltaSnapshotByteLength > 0)
                {
                    loadedVoxelDeltaSnapshot = CreateTransientNativeArray<byte>(
                        voxelDeltaSnapshotByteLength,
                        Allocator.Persistent,
                        NativeArrayOptions.UninitializedMemory,
                        "loadedVoxelDeltaSnapshot");
                }

                if (!SaveBinaryStorage.TryLoadSaveData(
                    absolutePath,
                    slotName,
                    readBuffer,
                    compressedReadBuffer,
                    loadedVoxelDeltaSnapshot,
                    out data,
                    out packedQuestHeader,
                    out packedQuestStateWords,
                    out persistentWorldItems,
                    out ecosystemSectorStates,
                    out int voxelDeltaSnapshotBytes,
                    out playerDialogueChoiceFlags,
                    out metadata,
                    out payloadHash64,
                    out rawPayloadLength,
                    out _,
                    out indexedBackupRecoveryUsed,
                    out errorMessage))
                {
                    if (loadedVoxelDeltaSnapshot.IsCreated)
                        DisposeTransientNativeArrayBestEffortAndReport(ref loadedVoxelDeltaSnapshot, "load", "loadedVoxelDeltaSnapshot");

                    return false;
                }

                return TryValidateLoadedVoxelSnapshot(
                    data,
                    voxelDeltaSnapshotBytes,
                    voxelDeltaSnapshotByteLength,
                    ref loadedVoxelDeltaSnapshot,
                    out voxelDeltaSnapshot,
                    out errorMessage);
            }
            finally
            {
                Exception cleanupException = null;

                if (loadedVoxelDeltaSnapshot.IsCreated)
                    DisposeTransientNativeArrayBestEffort(ref loadedVoxelDeltaSnapshot, ref cleanupException, sentinelLabel: "loadedVoxelDeltaSnapshot");

                ReleaseWriteBuffersBestEffort(readBuffer, ownsReadBuffer, compressedReadBuffer, ownsCompressedReadBuffer, ref cleanupException);
                ReportPersistenceCleanupFailure("load", cleanupException);
            }
        }

        private static bool TryValidateLoadedVoxelSnapshot(
            SaveData data,
            int voxelDeltaSnapshotBytes,
            int voxelDeltaSnapshotByteLength,
            ref NativeArray<byte> loadedVoxelDeltaSnapshot,
            out NativeArray<byte> finalVoxelDeltaSnapshot,
            out string errorMessage)
        {
            finalVoxelDeltaSnapshot = default;
            errorMessage = string.Empty;

            if (voxelDeltaSnapshotBytes != voxelDeltaSnapshotByteLength)
            {
                errorMessage = "Loaded voxel delta snapshot byte count mismatch.";
                if (loadedVoxelDeltaSnapshot.IsCreated)
                    DisposeTransientNativeArrayBestEffortAndReport(ref loadedVoxelDeltaSnapshot, "load", "loadedVoxelDeltaSnapshot");

                return false;
            }

            if (voxelDeltaSnapshotBytes > 0 &&
                !VoxelDeltaProcessor.TryValidateNativeSnapshotForLoad(loadedVoxelDeltaSnapshot, out string voxelSnapshotValidationError))
            {
                string fallbackReason = string.IsNullOrEmpty(voxelSnapshotValidationError)
                    ? "Loaded voxel delta snapshot failed validation."
                    : voxelSnapshotValidationError;
                if (HasLoadableVoxelDeltaDtoFallback(data))
                {
                    LogWarning("[SaveManager] Loaded voxel delta native snapshot failed validation; falling back to binary voxel payload: " + fallbackReason);
                    if (loadedVoxelDeltaSnapshot.IsCreated)
                        DisposeTransientNativeArrayBestEffortAndReport(ref loadedVoxelDeltaSnapshot, "load", "loadedVoxelDeltaSnapshot");

                    finalVoxelDeltaSnapshot = default;
                    return true;
                }

                errorMessage = fallbackReason;
                if (loadedVoxelDeltaSnapshot.IsCreated)
                    DisposeTransientNativeArrayBestEffortAndReport(ref loadedVoxelDeltaSnapshot, "load", "loadedVoxelDeltaSnapshot");

                return false;
            }

            finalVoxelDeltaSnapshot = loadedVoxelDeltaSnapshot;
            loadedVoxelDeltaSnapshot = default;
            return true;
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
                AcquireWriteBuffers(
                    out NativeArray<byte> readBuffer,
                    out bool ownsReadBuffer,
                    out NativeArray<byte> compressedReadBuffer,
                    out bool ownsCompressedReadBuffer);
                try
                {
                    return SaveBinaryStorage.TryReadMetadata(absolutePath, slotName, readBuffer, compressedReadBuffer, out metadata, out detectedVersion, out errorMessage);
                }
                finally
                {
                    Exception cleanupException = null;
                    ReleaseWriteBuffersBestEffort(readBuffer, ownsReadBuffer, compressedReadBuffer, ownsCompressedReadBuffer, ref cleanupException);
                    ReportPersistenceCleanupFailure("load", cleanupException);
                }
            }

            errorMessage = "Unsupported non-binary save artifact.";
            return false;
        }

        private static bool RepairPrimaryArtifacts(in RepairPrimaryArtifactsArgs args)
        {
            string primarySavePath = GetPrimarySaveFilePath(args.SlotName);
            string tempSavePath = GetTempSaveFilePath(args.SlotName);

            bool changedAnything = false;
            if (args.OverwritePrimarySave || !FileExists(primarySavePath))
            {
                SaveMetadata writeMetadata = CreateMetadataFromData(args.SlotName, args.Data, args.MetadataSource);

                if (!ExecutePrimaryArtifactRepairWrite(in args, tempSavePath, primarySavePath, writeMetadata))
                {
                    return false;
                }

                changedAnything = true;
            }

            return changedAnything;
        }

        private static bool ExecutePrimaryArtifactRepairWrite(
            in RepairPrimaryArtifactsArgs args,
            string tempSavePath,
            string primarySavePath,
            SaveMetadata writeMetadata)
        {
            AcquireWriteBuffers(out NativeArray<byte> rawBuffer, out bool ownsRawBuffer, out NativeArray<byte> compressedBuffer, out bool ownsCompressedBuffer);
            NativeArray<PersistentWorldDeltaRecord> persistentWorldItemBuffer = default;
            NativeArray<EcosystemSectorSaveRecord> ecosystemSectorBuffer = default;
            NativeArray<uint> packedQuestStateBuffer = default;
            try
            {
                if (args.PersistentWorldItems != null && args.PersistentWorldItems.Length > 0)
                {
                    // COLD ALLOC: NativeArray<PersistentWorldDeltaRecord>[persistentWorldItems.Length] — static save assembly staging buffer — owner: SaveManager
                    persistentWorldItemBuffer = CreateTransientNativeArray<PersistentWorldDeltaRecord>(
                        args.PersistentWorldItems.Length,
                        Allocator.Temp,
                        NativeArrayOptions.UninitializedMemory,
                        "persistentWorldItemBuffer");
                    persistentWorldItemBuffer.CopyFrom(args.PersistentWorldItems);
                }

                if (args.EcosystemSectorStates != null && args.EcosystemSectorStates.Length > 0)
                {
                    // COLD ALLOC: NativeArray<EcosystemSectorSaveRecord>[ecosystemSectorStates.Length] — static save assembly staging buffer — owner: SaveManager
                    ecosystemSectorBuffer = CreateTransientNativeArray<EcosystemSectorSaveRecord>(
                        args.EcosystemSectorStates.Length,
                        Allocator.Temp,
                        NativeArrayOptions.UninitializedMemory,
                        "ecosystemSectorBuffer");
                    ecosystemSectorBuffer.CopyFrom(args.EcosystemSectorStates);
                }

                if (args.PackedQuestStateWords != null && args.PackedQuestStateWords.Length > 0)
                {
                    // COLD ALLOC: NativeArray<UInt32>[packedQuestStateWords.Length] — static save assembly staging buffer — owner: SaveManager
                    packedQuestStateBuffer = CreateTransientNativeArray<uint>(
                        args.PackedQuestStateWords.Length,
                        Allocator.Temp,
                        NativeArrayOptions.UninitializedMemory,
                        "packedQuestStateBuffer");
                    packedQuestStateBuffer.CopyFrom(args.PackedQuestStateWords);
                }

                return TryExecuteVerifiedSavePipeline(
                    args.SlotName,
                    tempSavePath,
                    primarySavePath,
                    writeMetadata,
                    args.Data,
                    persistentWorldItemBuffer.IsCreated ? persistentWorldItemBuffer.AsReadOnly() : default,
                    ecosystemSectorBuffer.IsCreated ? ecosystemSectorBuffer.AsReadOnly() : default,
                    args.PackedQuestHeader,
                    packedQuestStateBuffer,
                    args.PlayerDialogueChoiceFlags,
                    args.VoxelDeltaSnapshot,
                    rawBuffer,
                    compressedBuffer,
                    args.BackupRetentionCount,
                    out _,
                    out _,
                    out _,
                    out _);
            }
            finally
            {
                Exception cleanupException = null;

                if (persistentWorldItemBuffer.IsCreated)
                    DisposeTransientNativeArrayBestEffort(ref persistentWorldItemBuffer, ref cleanupException, sentinelLabel: "persistentWorldItemBuffer");

                if (ecosystemSectorBuffer.IsCreated)
                    DisposeTransientNativeArrayBestEffort(ref ecosystemSectorBuffer, ref cleanupException, sentinelLabel: "ecosystemSectorBuffer");

                if (packedQuestStateBuffer.IsCreated)
                    DisposeTransientNativeArrayBestEffort(ref packedQuestStateBuffer, ref cleanupException, sentinelLabel: "packedQuestStateBuffer");

                ReleaseWriteBuffersBestEffort(rawBuffer, ownsRawBuffer, compressedBuffer, ownsCompressedBuffer, ref cleanupException);
                ReportPersistenceCleanupFailure("save", cleanupException);
            }
        }

        private static SaveMetadata CreateMetadataFromData(string slotName, SaveData data, SaveMetadata source)
        {
            string sceneName = SaveMetadata.NormalizeSceneName(source != null ? source.SceneName : null);
            string gameVersion = source != null && !string.IsNullOrEmpty(source.GameVersion)
                ? source.GameVersion
                : "Unknown";
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
            StaticNativeBuffers.AcquireWriteBuffers(out rawBuffer, out ownsRawBuffer, out compressedBuffer, out ownsCompressedBuffer);
        }

        private static void ReleaseWriteBuffers(
            NativeArray<byte> rawBuffer,
            bool ownsRawBuffer,
            NativeArray<byte> compressedBuffer,
            bool ownsCompressedBuffer)
        {
            StaticNativeBuffers.ReleaseWriteBuffers(rawBuffer, ownsRawBuffer, compressedBuffer, ownsCompressedBuffer);
        }

        private static void ReleaseWriteBuffersBestEffort(
            NativeArray<byte> rawBuffer,
            bool ownsRawBuffer,
            NativeArray<byte> compressedBuffer,
            bool ownsCompressedBuffer,
            ref Exception firstException)
        {
            try
            {
                ReleaseWriteBuffers(rawBuffer, ownsRawBuffer, compressedBuffer, ownsCompressedBuffer);
            }
            catch (Exception exception)
            {
                CaptureFirstCleanupException(ref firstException, exception);
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

        private struct SaveSlotInfoBuilderContext
        {
            public SaveMetadata Metadata;
            public bool HasPrimaryMetadata;
            public bool HasBackupMetadata;
            public bool MetadataRecoveredFromBackup;
            public bool MetadataSynthesized;
            public bool MetadataCorrupted;
            public long LastWriteTicksUtc;
            public long PrimaryBytes;
            public long BackupBytes;
        }

        private static bool HasAnyBackupSave(string slotName, int backupRetention)
        {
            for (int generation = 1; generation <= backupRetention; generation++)
            {
                if (FileExists(GetBackupSaveFilePath(slotName, generation)))
                {
                    return true;
                }
            }
            return false;
        }

        private static void ProcessPrimarySaveInfo(string slotName, string primarySavePath, ref SaveSlotInfoBuilderContext ctx)
        {
            if (TryReadCandidateMetadata(
                slotName,
                SaveLoadCandidate.Primary(),
                out SaveMetadata primaryMetadata,
                out _, out _, out _))
            {
                ctx.Metadata = primaryMetadata;
                ctx.HasPrimaryMetadata = primaryMetadata != null;
            }
            else
            {
                ctx.MetadataCorrupted = true;
            }

            ctx.PrimaryBytes = GetPersistentFileSize(primarySavePath);
            UpdateLastWrite(primarySavePath, ref ctx.LastWriteTicksUtc);
        }

        private static void ProcessBackupSaves(string slotName, int backupRetention, bool hasPrimarySave, ref SaveSlotInfoBuilderContext ctx)
        {
            for (int generation = 1; generation <= backupRetention; generation++)
            {
                string backupSavePath = GetBackupSaveFilePath(slotName, generation);
                if (!FileExists(backupSavePath))
                    continue;

                if (TryReadCandidateMetadata(
                    slotName,
                    SaveLoadCandidate.Backup(generation),
                    out SaveMetadata backupMetadata,
                    out _, out _, out _))
                {
                    ctx.HasBackupMetadata = backupMetadata != null;
                    if (ctx.Metadata == null && backupMetadata != null)
                    {
                        ctx.Metadata = backupMetadata;
                        ctx.MetadataRecoveredFromBackup = hasPrimarySave && !ctx.HasPrimaryMetadata;
                    }
                }
                else
                {
                    ctx.MetadataCorrupted = true;
                }

                ctx.BackupBytes += GetPersistentFileSize(backupSavePath);
                UpdateLastWrite(backupSavePath, ref ctx.LastWriteTicksUtc);
            }
        }

        private static SaveSlotIntegrityState DetermineIntegrityState(bool hasPrimarySave, bool hasBackupSave, ref SaveSlotInfoBuilderContext ctx)
        {
            if (ctx.MetadataCorrupted && ctx.MetadataRecoveredFromBackup)
                return SaveSlotIntegrityState.MetadataRecoveredFromBackup;

            if (hasPrimarySave && ctx.HasPrimaryMetadata && hasBackupSave && ctx.HasBackupMetadata)
                return SaveSlotIntegrityState.HealthyWithBackup;

            if (hasPrimarySave && ctx.HasPrimaryMetadata)
                return SaveSlotIntegrityState.Healthy;

            if (!hasPrimarySave && hasBackupSave && ctx.HasBackupMetadata)
                return SaveSlotIntegrityState.BackupOnly;

            if (ctx.MetadataCorrupted && !ctx.MetadataSynthesized)
                return SaveSlotIntegrityState.CorruptedMetadata;

            if (ctx.MetadataRecoveredFromBackup)
                return SaveSlotIntegrityState.MetadataRecoveredFromBackup;

            if (ctx.MetadataSynthesized)
                return SaveSlotIntegrityState.MetadataSynthesized;

            return SaveSlotIntegrityState.MissingMetadata;
        }

        private static SaveSlotInfo BuildSaveSlotInfoInternal(string slotName)
        {
            if (!TryResolveSafeSlotName(slotName, out slotName))
                return null;

            int backupRetention = GetBackupRetentionCountStatic(slotName);
            string primarySavePath = GetPrimarySaveFilePath(slotName);

            bool hasPrimarySave = FileExists(primarySavePath);
            bool hasBackupSave = HasAnyBackupSave(slotName, backupRetention);

            if (!hasPrimarySave && !hasBackupSave)
                return null;

            bool hasThumbnail = File.Exists(SaveThumbnailSystem.GetThumbnailPath(slotName));

            SaveSlotInfoBuilderContext ctx = new SaveSlotInfoBuilderContext();

            if (hasPrimarySave)
            {
                ProcessPrimarySaveInfo(slotName, primarySavePath, ref ctx);
            }

            ProcessBackupSaves(slotName, backupRetention, hasPrimarySave, ref ctx);

            UpdateLastWrite(SaveSlotMaintenanceRecord.GetPath(slotName), ref ctx.LastWriteTicksUtc);
            UpdateLastWrite(Path.GetFileName(SaveThumbnailSystem.GetThumbnailPath(slotName)), ref ctx.LastWriteTicksUtc);

            if (ctx.Metadata == null)
            {
                ctx.Metadata = SaveMetadata.CreateFallback(slotName, ctx.LastWriteTicksUtc);
                ctx.MetadataSynthesized = true;
            }

            ctx.Metadata.SlotName = slotName;

            return new SaveSlotInfo
            {
                SlotName = slotName,
                Metadata = ctx.Metadata,
                IntegrityState = DetermineIntegrityState(hasPrimarySave, hasBackupSave, ref ctx),
                HasPrimarySave = hasPrimarySave,
                HasBackupSave = hasBackupSave,
                HasPrimaryMetadata = ctx.HasPrimaryMetadata,
                HasBackupMetadata = ctx.HasBackupMetadata,
                HasThumbnail = hasThumbnail,
                MetadataRecoveredFromBackup = ctx.MetadataRecoveredFromBackup,
                MetadataSynthesized = ctx.MetadataSynthesized,
                LastWriteTicksUtc = ctx.LastWriteTicksUtc,
                PrimarySaveBytes = ctx.PrimaryBytes,
                BackupSaveBytes = ctx.BackupBytes
            };
        }

        private static long GetPersistentFileSize(string relativeFileName)
        {
            if (string.IsNullOrEmpty(relativeFileName))
                return 0L;

            string fullPath = GetPersistentAbsolutePath(relativeFileName);
            return TryGetAbsoluteFileLength(fullPath, out long fileBytes) ? fileBytes : 0L;
        }

        private static bool TryGetAbsoluteFileLength(string absolutePath, out long fileLength)
        {
            fileLength = 0L;
            if (string.IsNullOrEmpty(absolutePath))
                return false;

            return AsyncWriteManager.TryGetFileLength(absolutePath, out fileLength, out _);
        }

        private static void UpdateLastWrite(string relativeFileName, ref long lastWriteTicksUtc)
        {
            if (string.IsNullOrEmpty(relativeFileName))
                return;

            string fullPath = GetPersistentAbsolutePath(relativeFileName);
            if (!File.Exists(fullPath))
                return;

            long ticks = File.GetLastWriteTimeUtc(fullPath).Ticks;
            if (ticks > lastWriteTicksUtc)
                lastWriteTicksUtc = ticks;
        }
    }
}
