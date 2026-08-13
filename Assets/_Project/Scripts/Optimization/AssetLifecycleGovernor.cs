using System;
using System.Buffers.Binary;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.SaveSystem;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;
using Stopwatch = System.Diagnostics.Stopwatch;
#if UNITY_ADDRESSABLES_EXIST
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace Hecton8.Optimization
{
    /// <summary>
    /// Global asset residency registry with deterministic ref-counting and deferred release draining.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-8012)]
    public sealed partial class AssetLifecycleGovernor : MonoBehaviour, ITickable, IUpdatable, ISlowTickable, ILateFrameTickable, IAssetLifecyclePressureSink, IGlobalRegistryHotSwapListener
    {
        private const uint CollisionSalt = 0xDEADBEEF;
        private const float NativeHeapOverheadFactor = 1.15f;
        private const float ColdReleaseIntervalSeconds = 1f;
        private const float DistantChunkReleaseDistanceMeters = 1500f;
        private const float HardReaperIntervalSeconds = 600f;
        private const float HardReaperGlitchDurationSeconds = 0.5f;
        private const int DefaultTrackedAddressableCapacity = 1024;
        private const int MaxTrackedAddressableCapacity = 8192;
        private const int MaxAddressableHandleMapCapacity = MaxTrackedAddressableCapacity * 2;
        private const int HeapTelemetryCapacity = 300;
        private const int CacheProfileCapacity = 256;
        private const int CacheProfileCsvScratchBytes = 64 * 1024;
        private const int DetachedReleaseHandleCapacity = 64;
        private const float AddressableMapPressureThreshold = 0.85f;
        private const float MinimumAdaptiveTtlSeconds = 10f;
        private const float DefaultHighEndTtlSeconds = 300f;
        private const float SharedBundleTtlMultiplier = 1.5f;
        private const int LeakRefCountThreshold = 50;
        private const uint HeapTelemetryFaultFlag = 1u << 0;
        private const uint HeapTelemetryVramPanicFlag = 1u << 1;
        private const uint HeapTelemetryBlindReleaseFlag = 1u << 2;
        private const uint HeapTelemetryLeakSuspectFlag = 1u << 3;
        private const int MaxColdDistantChunkReleases = 8;
        private const int MaxHardReaperEvictions = 64;
        private const double ColdTickWarningMilliseconds = 0.2d;
        private const float ColdTickWarningCooldownSeconds = 5f;
        private const SystemID VaultOwnerSystem = SystemID.WorldStreaming;
        private static readonly uint _AssetLifecycleContextHash = unchecked((uint)Hecton.Localization.LocHash.Compute("AssetLifecycleGovernor"));
        private static readonly uint _ColdTickOverBudgetWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("AssetLifecycleGovernor.ColdTickOverBudget"));
        private static readonly uint _DoubleReleaseWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("AssetLifecycleGovernor.DoubleRelease"));
        private static readonly uint _HardReaperSweepWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("AssetLifecycleGovernor.HardReaperSweep"));
        private static readonly uint _ShaderFallbackWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("AssetLifecycleGovernor.ShaderFallback"));
        private static readonly float[] _retryBackoffSeconds = { 5f, 15f, 60f };
        private static float RuntimeNowSeconds()
        {
            return (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
        }

        [Header("Asset Registry")]
        [Tooltip("Pre-sized residency registry capacity. This is cold-path storage only.")]
        [SerializeField] private int maxRegistryCapacity = 512;

        [Tooltip("Maximum deferred releases drained per frame before the gameplay handoff.")]
        [SerializeField] private int maxDeferredReleasesPerFrame = 8;

        [Tooltip("Fixed central Addressables handle slots. Overflow rejects new loads instead of leaking raw handles.")]
        [SerializeField] private int maxTrackedAddressableHandles = DefaultTrackedAddressableCapacity;

        [Tooltip("High-quality cache TTL in seconds. GlobalQualityWeight blends from 10s to this value.")]
        [SerializeField, Range(MinimumAdaptiveTtlSeconds, DefaultHighEndTtlSeconds)]
        private float baseAddressableTtlSeconds = DefaultHighEndTtlSeconds;

        [Tooltip("VRAM pressure factor where unused Addressables bypass TTL and release in the panic path.")]
        [SerializeField, Range(0.5f, 0.99f)]
        private float vramPanicThreshold = 0.9f;

        [Header("Authored Fallback Assets")]
        [Tooltip("Authored impostor mesh used when an Addressables visual dependency cannot resolve. Runtime cube synthesis is forbidden.")]
        [SerializeField] private Mesh authoredFallbackImpostorMesh;

        [Tooltip("Authored material assigned when a tracked material resolves to a failed shader. Runtime checkerboard material synthesis is forbidden.")]
        [SerializeField] private Material authoredCheckerboardMaterial;

        private bool _registeredTick;
        private bool _registeredSlowTick;
        private bool _registeredLateFrame;
        private bool _registeredService;
        private bool _registeredHotSwap;
        private bool _runtimeOwnerAborted;
        private readonly Component[] _pendingPresentationDisableOwners = new Component[MaxHardReaperEvictions];
        private int _pendingPresentationDisableCount;
        private long _frameSequence;
        private float _nextColdReleaseTime;
        private float _nextColdTickWarningTime;
        private float _nextHardReaperTime;
        private Material _checkerboardMaterial;
        private AsyncOperation _hardReaperUnloadOperation;
        private System.Action<AsyncOperation> _hardReaperUnloadCompletedCallback;
        private bool _hardReaperAsyncWindowActive;
        private bool _hardReaperUnloadComplete;
        private bool _hardReaperBundleCacheCleanComplete;
        private bool _mockScreenFadeToBlackActive;
        private float _mockScreenFadeToBlackUntil;
        private bool _explicitBlindFrameWindowActive;
        private float _explicitBlindFrameWindowUntil;
        private bool _externalVramPanicActive;
        private float _externalVramPanicUntil;
        private int _orphanedHandlesReleased;
        private int _cacheHitCount;
        private int _cacheMissCount;
        private int _forcedVramReleaseCount;
        private int _heapTelemetryCursor;
        private int _lastPendingTtlCount;
        private uint _lastLeakSuspectHash;
        private Mesh _fallbackImpostorMesh;
        private IDataVault _dataVault;
        private VaultGenerationHandle<AssetTrackerDTO> _assetTrackerVaultHandle;
        private VaultGenerationHandle<float> _assetTtlVaultHandle;
        private VaultGenerationHandle<byte> _assetTrackerFlagsVaultHandle;
        private VaultGenerationHandle<AssetHandleMapEntryDTO> _assetHandleMapVaultHandle;
        private VaultGenerationHandle<AssetCacheProfileDTO> _cacheProfileVaultHandle;
        private VaultGenerationHandle<byte> _cacheProfileCsvScratchVaultHandle;
        private VaultGenerationHandle<AssetHeapTelemetryEntry> _heapTelemetryVaultHandle;
        private SystemDispatcher _cachedDispatcher;
        private AssetLoadDispatcher _cachedAssetLoadDispatcher;
        private IVramPressureReadModel _cachedVramPressure;
        private IPlayerRuntimeContext _cachedPlayer;
        private IPlayerInventoryService _cachedPlayerInventory;
        private IScannerInterferenceUiSink _cachedScannerInterferenceUi;
        private bool _ttlEvaluationResultsPending;
        private bool _ttlEvaluationVramPanic;
        private bool _ttlEvaluationFlagsMirrored;
        private bool _nativeRefSyncRequired;
        private bool _nativeStorageInitialized;
        private int _resolvedHandleCapacity;
        private int _resolvedMapCapacity;
#if UNITY_ADDRESSABLES_EXIST
        private AsyncOperationHandle<bool> _hardReaperCleanBundleCacheHandle;
        private System.Action<AsyncOperationHandle<bool>> _hardReaperCleanBundleCacheCompletedCallback;
        private uint _lastAddressableDependencyGroupHash;
        private int _lastAddressableDependencyOrder;
        private int _addressableDependencyGroupLoadCount;
        private AsyncOperationHandle[] _addressableHandlePool;
        private AsyncOperationHandle[] _detachedReleaseHandles;
        private uint[] _addressableHandleHashes;
        private uint[] _addressableBundlePrefixHashes;
        private int _addressableHandleCount;
        private int _detachedReleaseHandleCount;
#endif

        private ManagedAssetRecordTable _assetRecords;
        private FixedUIntQueue _pendingReleaseQueue;
        private FixedUIntList _evictionCandidates;
        private FixedUIntList _retryCandidates;
        private bool _pendingReleaseOverflowDraining;
#if UNITY_EDITOR
        // COLD ALLOC: StringBuilder[512] - throttled diagnostics builder - owner: AssetLifecycleGovernor
#endif

        internal long TrackedResidentBytes { get; private set; }
        internal long NativeHeapEstimateBytes => (long)(TrackedResidentBytes * NativeHeapOverheadFactor);
        internal int PendingReleaseCount => _pendingReleaseQueue.Count;
        internal Material CheckerboardMaterial => _checkerboardMaterial;
#if UNITY_ADDRESSABLES_EXIST
        internal int AddressableDependencyGroupLoadCount => _addressableDependencyGroupLoadCount;
#endif

        private void Awake()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            int bootstrapHandleCapacity = Mathf.Clamp(maxTrackedAddressableHandles, 1, MaxTrackedAddressableCapacity);
            maxTrackedAddressableHandles = bootstrapHandleCapacity;
            maxRegistryCapacity = Mathf.Max(Mathf.Max(1, maxRegistryCapacity), bootstrapHandleCapacity);
            CacheDependencies();
            EnsureManagedRecordStorage();
            EnsureNativeHandleStorage();
            _nextHardReaperTime = RuntimeNowSeconds() + HardReaperIntervalSeconds;
            _hardReaperUnloadCompletedCallback = HandleHardReaperUnloadCompleted;
#if UNITY_ADDRESSABLES_EXIST
            _hardReaperCleanBundleCacheCompletedCallback = HandleHardReaperCleanBundleCacheCompleted;
#endif
            EnsureFallbackAssets();
        }

        private void OnEnable()
        {
            if (_runtimeOwnerAborted)
                return;

            if (!TryRegisterService())
                return;

            CacheDependencies();
            EnsureNativeHandleStorage();
            EnsureFallbackAssets();
            TryRegisterHotSwap();
            TryRegister();
        }

        private void Start()
        {
            if (_runtimeOwnerAborted)
                return;

            if (!_registeredService && !TryRegisterService())
                return;

            CacheDependencies();
            if (!_nativeStorageInitialized)
                EnsureNativeHandleStorage();

            EnsureFallbackAssets();
            TryRegisterHotSwap();
            TryRegister();
        }

        private void OnDisable()
        {
            if (_runtimeOwnerAborted)
                return;

            TryUnregister();
            TryUnregisterHotSwap();
            TryUnregisterService();
            ResetAddressableHeapRuntimeState(false);
        }

        private void OnDestroy()
        {
            if (_runtimeOwnerAborted)
                return;

            TryUnregister();
            TryUnregisterHotSwap();
            TryUnregisterService();
            ResetAddressableHeapRuntimeState(true);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnsafeUtility.SizeOf<AssetHandleMapEntryDTO>() != 64)
                Hecton8.Core.H8Debug.LogError("[AssetLifecycleGovernor] AssetHandleMapEntryDTO must remain 64 bytes.", this);
            if (UnsafeUtility.SizeOf<AssetTrackerDTO>() != 64)
                Hecton8.Core.H8Debug.LogError("[AssetLifecycleGovernor] AssetTrackerDTO must remain 64 bytes.", this);
        }
#endif

        private void ResetAddressableHeapRuntimeState(bool disposeFallbackAssets)
        {
            _explicitBlindFrameWindowActive = true;
            _explicitBlindFrameWindowUntil = 0f;
            if (!ReleaseHardReaperAsyncHandles())
            {
                SetHardReaperScannerInterferenceActive(false);
                _explicitBlindFrameWindowActive = false;
                _explicitBlindFrameWindowUntil = 0f;
                _mockScreenFadeToBlackActive = false;
                _mockScreenFadeToBlackUntil = 0f;
                _externalVramPanicActive = false;
                _externalVramPanicUntil = 0f;
                _lastLeakSuspectHash = _lastLeakSuspectHash != 0u ? _lastLeakSuspectHash : CollisionSalt;
                DumpHeapTelemetry();
                return;
            }

            SetHardReaperScannerInterferenceActive(false);
            _mockScreenFadeToBlackActive = false;
            _mockScreenFadeToBlackUntil = 0f;
            _externalVramPanicActive = false;
            _externalVramPanicUntil = 0f;

            if (!DisposeNativeHandleStorage())
            {
                _explicitBlindFrameWindowActive = false;
                _explicitBlindFrameWindowUntil = 0f;
                _lastLeakSuspectHash = _lastLeakSuspectHash != 0u ? _lastLeakSuspectHash : CollisionSalt;
                DumpHeapTelemetry();
                return;
            }
            _assetRecords.Clear();
            _pendingReleaseQueue.Clear();
            _evictionCandidates.Clear();
            _retryCandidates.Clear();
            _explicitBlindFrameWindowActive = false;
            _explicitBlindFrameWindowUntil = 0f;
            _frameSequence = 0L;
            _nextColdReleaseTime = 0f;
            _nextColdTickWarningTime = 0f;
            _nextHardReaperTime = RuntimeNowSeconds() + HardReaperIntervalSeconds;
            TrackedResidentBytes = 0L;
            _orphanedHandlesReleased = 0;
            _cacheHitCount = 0;
            _cacheMissCount = 0;
            _forcedVramReleaseCount = 0;
            _lastPendingTtlCount = 0;
            _lastLeakSuspectHash = 0u;
#if UNITY_ADDRESSABLES_EXIST
            _lastAddressableDependencyGroupHash = 0u;
            _lastAddressableDependencyOrder = 0;
            _addressableDependencyGroupLoadCount = 0;
#endif

            if (disposeFallbackAssets)
                DisposeFallbackAssets();
        }

        private void EnsureManagedRecordStorage()
        {
            _assetRecords.Initialize(MaxTrackedAddressableCapacity);
            _pendingReleaseQueue.Initialize(MaxTrackedAddressableCapacity);
            _evictionCandidates.Initialize(MaxHardReaperEvictions);
            _retryCandidates.Initialize(MaxHardReaperEvictions);
        }

        private bool EnqueuePendingRelease(uint key)
        {
            if (_pendingReleaseQueue.Contains(key))
                return true;

            if (_pendingReleaseQueue.Enqueue(key))
                return true;

            if (!_pendingReleaseOverflowDraining)
            {
                _pendingReleaseOverflowDraining = true;
                float panicUntil = RuntimeNowSeconds() + 0.25f;
                _externalVramPanicActive = true;
                if (_externalVramPanicUntil < panicUntil)
                    _externalVramPanicUntil = panicUntil;

                DrainPendingReleaseQueue(MaxTrackedAddressableCapacity);
                DrainDetachedAddressableReleaseHandles();
                _pendingReleaseOverflowDraining = false;

                if (_pendingReleaseQueue.Contains(key))
                    return true;
                if (_pendingReleaseQueue.Enqueue(key))
                    return true;
            }

            _lastLeakSuspectHash = key;
            DumpHeapTelemetry();
            return false;
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            _frameSequence++;
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            float now = RuntimeNowSeconds();
            if (now < _nextColdReleaseTime)
                return;

            _nextColdReleaseTime = now + ColdReleaseIntervalSeconds;
            long startTicks = Stopwatch.GetTimestamp();
            try
            {
                EvaluateAddressableTtlAndQueueReleases();
                DrainPendingReleaseQueue(maxDeferredReleasesPerFrame);
                DrainDetachedAddressableReleaseHandles();
                PumpRetries();
                ReleaseDistantChunkAddressables(MaxColdDistantChunkReleases);
                EvaluateHardMemoryReaper(now);
                WriteHeapTelemetrySample();
                ScheduleAddressableTtlEvaluation();
            }
            finally
            {
                ReportColdTickBudgetIfNeeded(startTicks);
            }
        }

        public void LateFrameTick()
        {
            FlushPendingPresentationDisables();
        }

        internal uint Acquire(
            string assetGuid,
            byte biomeId,
            byte lodLevel,
            string address,
            Component owner,
            AssetPriorityTier priority,
            AssetResidencyKind residencyKind,
            long sizeBytes,
            Object asset = null,
            bool ownsAssetInstance = false)
        {
            uint key = CreateKey(assetGuid, address, biomeId, lodLevel);
            if (!ResolveCollision(ref key, assetGuid, address))
                return key;

            if (_assetRecords.TryGetValue(key, out AssetRecord record))
            {
                record.RefCount++;
                record.Owner = owner != null ? owner : record.Owner;
                record.LastAccessFrame = _frameSequence;
                record.PendingRelease = false;

                if (asset != null)
                {
                    ReplaceTrackedSize(ref record, sizeBytes);
                    record.Asset = asset;
                    record.IsFallback = false;
                    record.OwnsAssetInstance = ownsAssetInstance;
                    record.NextRetryTime = 0f;
                    record.RetryCount = 0;
                    TryApplyShaderFallback(ref record, asset);
                }

                _assetRecords.Set(key, record);
                return key;
            }

            AssetRecord created = new AssetRecord
            {
                Key = key,
                AssetGuid = assetGuid,
                Address = address,
                Asset = asset,
                Owner = owner,
                RefCount = 1,
                Priority = priority,
                ResidencyKind = residencyKind,
                PendingRelease = false,
                IsFallback = false,
                OwnsAssetInstance = ownsAssetInstance,
                IsChunkAsset = false,
                RetryCount = 0,
                BiomeId = biomeId,
                LodLevel = lodLevel,
                LastAccessFrame = _frameSequence,
                SizeBytes = ClampNonNegative(sizeBytes),
                ActiveRequestId = 0,
                NextRetryTime = 0f
            };

            TryApplyShaderFallback(ref created, asset);
            _assetRecords.Set(key, created);
            TrackedResidentBytes += created.SizeBytes;

            if (asset == null && residencyKind != AssetResidencyKind.SceneOwned)
                QueueAsyncDispatch(key);

            return key;
        }

        internal void MarkLoaded(uint key, Object asset, long sizeBytes, bool ownsAssetInstance = false)
        {
            if (!_assetRecords.TryGetValue(key, out AssetRecord record))
                return;

            if (record.ActiveRequestId != 0)
            {
                AssetLoadDispatcher dispatcher = _cachedAssetLoadDispatcher;
                if (dispatcher != null)
                    dispatcher.AcknowledgeDispatchRequest(record.ActiveRequestId, true);

                record.ActiveRequestId = 0;
            }

            ReplaceTrackedSize(ref record, sizeBytes);
            record.Asset = asset;
            record.IsFallback = false;
            record.OwnsAssetInstance = ownsAssetInstance;
            record.NextRetryTime = 0f;
            record.RetryCount = 0;
            record.LastAccessFrame = _frameSequence;
            TryApplyShaderFallback(ref record, asset);
            _assetRecords.Set(key, record);
        }

#if UNITY_ADDRESSABLES_EXIST
        internal void MarkAddressableLoaded(
            uint key,
            AsyncOperationHandle handle,
            Object asset,
            long sizeBytes,
            bool isChunkAsset)
        {
            if (!_assetRecords.TryGetValue(key, out AssetRecord record))
                return;

            if (record.ActiveRequestId != 0)
            {
                AssetLoadDispatcher dispatcher = _cachedAssetLoadDispatcher;
                if (dispatcher != null)
                    dispatcher.AcknowledgeDispatchRequest(record.ActiveRequestId, true);

                record.ActiveRequestId = 0;
            }

            ReplaceTrackedSize(ref record, sizeBytes);
            record.Asset = asset;
            record.IsFallback = false;
            record.OwnsAssetInstance = false;
            record.HasAddressableHandle = handle.IsValid();
            record.AddressableHandle = handle;
            record.IsChunkAsset = isChunkAsset;
            record.NextRetryTime = 0f;
            record.RetryCount = 0;
            record.LastAccessFrame = _frameSequence;
            TryApplyShaderFallback(ref record, asset);
            _assetRecords.Set(key, record);
        }

        internal void MarkAddressableDependencyGroupLoaded(
            uint groupHash,
            int dependencyOrder,
            AsyncOperationHandle handle)
        {
            if (groupHash == 0u || !handle.IsValid() || handle.Status != AsyncOperationStatus.Succeeded)
                return;

            _lastAddressableDependencyGroupHash = groupHash;
            _lastAddressableDependencyOrder = dependencyOrder;
            _addressableDependencyGroupLoadCount++;
        }

        internal bool TryAcquireAddressableGameObject(
            uint assetHash,
            string address,
            Component owner,
            AssetPriorityTier priority,
            AssetResidencyKind residencyKind,
            long sizeBytes,
            bool isChunkAsset,
            out AsyncOperationHandle<GameObject> handle,
            out bool cacheHit)
        {
            handle = default;
            cacheHit = false;
            if (assetHash == 0u || string.IsNullOrEmpty(address))
                return false;

            EnsureNativeHandleStorage();
            if (TryAcquireTrackedHandle(assetHash, address, owner, priority, residencyKind, sizeBytes, isChunkAsset, out handle, out cacheHit))
                return true;

            if (!TryResolveTrackerViews(
                    out NativeArray<AssetTrackerDTO> trackers,
                    out NativeArray<float> ttl,
                    out NativeArray<byte> trackerFlags,
                    out NativeArray<AssetHandleMapEntryDTO> handleMap))
            {
                return false;
            }

            HandleAddressableMapPressureIfNeeded(trackers, ttl, trackerFlags, handleMap);
            int slot = AllocateAddressableHandleSlot(assetHash, trackerFlags);
            if (slot < 0)
            {
                _lastLeakSuspectHash = assetHash;
                DumpHeapTelemetry();
                return false;
            }

            handle = Addressables.LoadAssetAsync<GameObject>(address);
            if (!handle.IsValid())
            {
                handle = default;
                _lastLeakSuspectHash = assetHash;
                DumpHeapTelemetry();
                return false;
            }

            if (!RegisterAddressableHandleSlot(
                    slot,
                    assetHash,
                    ComputeBundlePrefixHash(address),
                    address,
                    owner,
                    priority,
                    residencyKind,
                    sizeBytes,
                    isChunkAsset,
                    handle))
            {
                if (handle.IsValid())
                    TryExecuteOrForceAddressableReleaseFault(handle);

                handle = default;
                _lastLeakSuspectHash = assetHash;
                DumpHeapTelemetry();
                return false;
            }

            _cacheMissCount++;
            return true;
        }

        private void InvalidateVaultHandleDescriptors()
        {
            _assetTrackerVaultHandle = default;
            _assetTtlVaultHandle = default;
            _assetTrackerFlagsVaultHandle = default;
            _assetHandleMapVaultHandle = default;
            _cacheProfileVaultHandle = default;
            _cacheProfileCsvScratchVaultHandle = default;
            _heapTelemetryVaultHandle = default;
            _ttlEvaluationResultsPending = false;
            _ttlEvaluationFlagsMirrored = false;
            _nativeRefSyncRequired = true;
            _nativeStorageInitialized = false;
            _resolvedHandleCapacity = 0;
            _resolvedMapCapacity = 0;
        }

        internal bool TryAcquireAddressableGameObject(
            uint assetHash,
            AssetReferenceGameObject reference,
            Component owner,
            AssetPriorityTier priority,
            AssetResidencyKind residencyKind,
            long sizeBytes,
            bool isChunkAsset,
            out AsyncOperationHandle<GameObject> handle,
            out bool cacheHit)
        {
            handle = default;
            cacheHit = false;
            if (assetHash == 0u || reference == null || !reference.RuntimeKeyIsValid())
                return false;

            EnsureNativeHandleStorage();
            string address = reference.AssetGUID;
            if (TryAcquireTrackedHandle(assetHash, address, owner, priority, residencyKind, sizeBytes, isChunkAsset, out handle, out cacheHit))
                return true;

            if (!TryResolveTrackerViews(
                    out NativeArray<AssetTrackerDTO> trackers,
                    out NativeArray<float> ttl,
                    out NativeArray<byte> trackerFlags,
                    out NativeArray<AssetHandleMapEntryDTO> handleMap))
            {
                return false;
            }

            HandleAddressableMapPressureIfNeeded(trackers, ttl, trackerFlags, handleMap);
            int slot = AllocateAddressableHandleSlot(assetHash, trackerFlags);
            if (slot < 0)
            {
                _lastLeakSuspectHash = assetHash;
                DumpHeapTelemetry();
                return false;
            }

            handle = reference.LoadAssetAsync<GameObject>();
            if (!handle.IsValid())
            {
                handle = default;
                _lastLeakSuspectHash = assetHash;
                DumpHeapTelemetry();
                return false;
            }

            if (!RegisterAddressableHandleSlot(
                    slot,
                    assetHash,
                    ComputeBundlePrefixHash(address),
                    address,
                    owner,
                    priority,
                    residencyKind,
                    sizeBytes,
                    isChunkAsset,
                    handle))
            {
                if (handle.IsValid())
                    TryExecuteOrForceAddressableReleaseFault(handle);

                handle = default;
                _lastLeakSuspectHash = assetHash;
                DumpHeapTelemetry();
                return false;
            }

            _cacheMissCount++;
            return true;
        }

        internal void ReleaseAddressableAsset(uint assetHash)
        {
            if (assetHash == 0u)
                return;

            Release(assetHash);
        }

#if UNITY_ADDRESSABLES_EXIST
        internal bool TryStageExternalAddressableRelease(AsyncOperationHandle handle)
        {
            return TryExecuteOrDeferBlindFrameRelease(handle);
        }

        internal bool TryStageExternalAddressableRelease<TObject>(AsyncOperationHandle<TObject> handle)
        {
            return TryStageExternalAddressableRelease((AsyncOperationHandle)handle);
        }

        internal bool TryReleaseExternalAddressableFault(AsyncOperationHandle handle)
        {
            return TryExecuteOrForceAddressableReleaseFault(handle);
        }

        internal bool TryReleaseExternalAddressableFault<TObject>(AsyncOperationHandle<TObject> handle)
        {
            return TryReleaseExternalAddressableFault((AsyncOperationHandle)handle);
        }
#endif

        internal bool MarkAddressableAssetAup(uint assetHash, double3 assetAup)
        {
            if (assetHash == 0u || !math.all(math.isfinite(assetAup)))
                return false;

            if (!TryResolveTrackerViews(
                    out NativeArray<AssetTrackerDTO> trackers,
                    out _,
                    out _,
                    out NativeArray<AssetHandleMapEntryDTO> handleMap) ||
                !TryGetHandleSlot(assetHash, handleMap, out int slot) ||
                (uint)slot >= (uint)trackers.Length)
            {
                return false;
            }

            AssetTrackerDTO tracker = trackers[slot];
            if (!TryWriteAssetAup(ref tracker, assetAup))
                return false;

            tracker.MaxResidencyRadiusSq = DistantChunkReleaseDistanceMeters * DistantChunkReleaseDistanceMeters;
            trackers[slot] = tracker;
            return true;
        }

        public void SetHeapSanitizerMockBlindFrame(bool active, float durationSeconds)
        {
            _mockScreenFadeToBlackActive = active;
            _mockScreenFadeToBlackUntil = active && durationSeconds > 0f
                ? RuntimeNowSeconds() + durationSeconds
                : 0f;
        }
#endif

        public void SetHeapSanitizerBlindFrameWindow(bool active, float durationSeconds)
        {
            _explicitBlindFrameWindowActive = active;
            _explicitBlindFrameWindowUntil = active && durationSeconds > 0f
                ? RuntimeNowSeconds() + durationSeconds
                : 0f;
        }

        public void SetHeapSanitizerVramPanicWindow(bool active, float durationSeconds)
        {
            _externalVramPanicActive = active;
            _externalVramPanicUntil = active && durationSeconds > 0f
                ? RuntimeNowSeconds() + durationSeconds
                : 0f;
        }

        long IAssetLifecyclePressureSink.NativeHeapEstimateBytes => NativeHeapEstimateBytes;

        void IAssetLifecyclePressureSink.ForceDrainPendingReleaseQueue()
        {
            ForceDrainPendingReleaseQueue();
        }

        int IAssetLifecyclePressureSink.DrainPendingReleaseQueueBudgeted(int maxCount)
        {
            return DrainPendingReleaseQueueBudgeted(maxCount);
        }

        int IAssetLifecyclePressureSink.EvictLowestPriorityUnusedAssets(int maxCount, byte minimumPriorityCode)
        {
            return EvictLowestPriorityUnusedAssets(maxCount, ResolveAssetPriorityTier(minimumPriorityCode));
        }

#if UNITY_ADDRESSABLES_EXIST
        bool IAssetLifecyclePressureSink.TryStageExternalAddressableRelease(AsyncOperationHandle handle)
        {
            return TryStageExternalAddressableRelease(handle);
        }

        bool IAssetLifecyclePressureSink.TryReleaseExternalAddressableFault(AsyncOperationHandle handle)
        {
            return TryReleaseExternalAddressableFault(handle);
        }
#endif

        private static AssetPriorityTier ResolveAssetPriorityTier(byte code)
        {
            switch (code)
            {
                case AssetPriorityTierCodes.Tier0PlayerCritical:
                    return AssetPriorityTier.Tier0PlayerCritical;
                case AssetPriorityTierCodes.Tier1Equipped:
                    return AssetPriorityTier.Tier1Equipped;
                case AssetPriorityTierCodes.Tier2Proximity:
                    return AssetPriorityTier.Tier2Proximity;
                case AssetPriorityTierCodes.Tier3Ambient:
                    return AssetPriorityTier.Tier3Ambient;
                case AssetPriorityTierCodes.Tier4MidRange:
                    return AssetPriorityTier.Tier4MidRange;
                case AssetPriorityTierCodes.Tier5DistantHlod:
                    return AssetPriorityTier.Tier5DistantHlod;
                default:
                    return AssetPriorityTier.Tier6Speculative;
            }
        }

        internal void MarkChunkResidency(uint key)
        {
            if (!_assetRecords.TryGetValue(key, out AssetRecord record))
                return;

            record.IsChunkAsset = true;
            record.LastAccessFrame = _frameSequence;
            _assetRecords.Set(key, record);
        }

        internal void MarkAccessed(uint key)
        {
            if (!_assetRecords.TryGetValue(key, out AssetRecord record))
                return;

            record.LastAccessFrame = _frameSequence;
            _assetRecords.Set(key, record);
        }

        internal void Release(uint key)
        {
            if (!_assetRecords.TryGetValue(key, out AssetRecord record))
                return;

            int nativeRefCount = TryDecrementNativeRefCount(key, out int slot);
            bool nativeMutationDeferred = nativeRefCount == int.MinValue;
            record.RefCount = slot >= 0 && !nativeMutationDeferred
                ? nativeRefCount
                : record.RefCount - 1;
            if (record.RefCount < 0)
            {
                record.RefCount = 0;
                if (slot >= 0 && !nativeMutationDeferred)
                {
                    if (TryResolveTrackerViews(
                            out NativeArray<AssetTrackerDTO> trackers,
                            out _,
                            out _,
                            out NativeArray<AssetHandleMapEntryDTO> handleMap))
                    {
                        SetNativeRefCount(key, slot, 0, trackers, handleMap);
                    }
                }

                GlobalTelemetryBus.PublishPerformanceWarning(_DoubleReleaseWarningHash, _AssetLifecycleContextHash, key);
#if UNITY_EDITOR
                Hecton8.Core.H8Debug.LogError("[AssetLifecycleGovernor] Double release detected.", this);
#endif
            }

            if (record.RefCount == 0 && !record.PendingRelease)
            {
                if (nativeMutationDeferred)
                {
                    _nativeRefSyncRequired = true;
                }
                else if (!ArmNativeTtlRelease(key, slot))
                {
                    record.PendingRelease = EnqueuePendingRelease(key);
                }
            }

            _assetRecords.Set(key, record);
        }

        internal void ForceDrainPendingReleaseQueue()
        {
            DrainPendingReleaseQueue(int.MaxValue);
        }

        internal int DrainPendingReleaseQueueBudgeted(int maxCount)
        {
            return DrainPendingReleaseQueue(maxCount);
        }

        internal int EvictLowestPriorityUnusedAssets(int maxCount, AssetPriorityTier minimumPriority)
        {
            if (maxCount <= 0 || _assetRecords.Count == 0)
                return 0;

            _evictionCandidates.Clear();

            ManagedAssetRecordTable.Enumerator enumerator = _assetRecords.GetEnumerator();
            while (enumerator.MoveNext())
            {
                AssetRecord record = enumerator.Current.Value;
                if (record.RefCount != 0 || record.PendingRelease)
                    continue;

                if ((byte)record.Priority < (byte)minimumPriority)
                    continue;

                InsertEvictionCandidate(record.Key);
            }

            int evictions = 0;
            int count = _evictionCandidates.Count;
            if (count > maxCount)
                count = maxCount;

            for (int i = 0; i < count; i++)
            {
                uint key = _evictionCandidates[i];
                if (!_assetRecords.TryGetValue(key, out AssetRecord record))
                    continue;

                if (TryExecuteOrDeferBlindFrameRelease(key, record))
                    evictions++;
            }

            _evictionCandidates.Clear();
            return evictions;
        }

        internal void ForceHardMemoryReaperSweep()
        {
            ExecuteHardMemoryReaper(RuntimeNowSeconds());
        }

        internal void MarkLoadFailed(uint key, string error)
        {
            if (!_assetRecords.TryGetValue(key, out AssetRecord record))
                return;

            if (record.ActiveRequestId != 0)
            {
                AssetLoadDispatcher dispatcher = _cachedAssetLoadDispatcher;
                if (dispatcher != null)
                    dispatcher.AcknowledgeDispatchRequest(record.ActiveRequestId, false);

                record.ActiveRequestId = 0;
            }

            ReplaceTrackedSize(ref record, 0L);
            record.Asset = _checkerboardMaterial;
            record.IsFallback = true;
            record.OwnsAssetInstance = false;

            if (record.RetryCount < _retryBackoffSeconds.Length)
            {
                record.NextRetryTime = RuntimeNowSeconds() + _retryBackoffSeconds[record.RetryCount];
                record.RetryCount++;
            }

            ApplyFallbackMaterial(record.Owner);
            _assetRecords.Set(key, record);

#if UNITY_EDITOR
            Hecton8.Core.H8Debug.LogError("[AssetLifecycleGovernor] Asset load failed.", this);
#endif
        }

        private void EnsureNativeHandleStorage()
        {
            int capacity = Mathf.Clamp(maxTrackedAddressableHandles, 1, MaxTrackedAddressableCapacity);
            maxTrackedAddressableHandles = capacity;
            maxRegistryCapacity = Mathf.Max(Mathf.Max(1, maxRegistryCapacity), capacity);
            EnsureManagedRecordStorage();
            int mapCapacity = ResolveHandleMapCapacity(capacity);

            bool needsColdClear = !_nativeStorageInitialized ||
                                  _resolvedHandleCapacity != capacity ||
                                  _resolvedMapCapacity != mapCapacity ||
                                  !HasHeapSanitizerVaultBuffer(
                                      _dataVault,
                                      in _assetTrackerVaultHandle,
                                      BufferID.AddressableHeapTrackers,
                                      capacity) ||
                                  !HasHeapSanitizerVaultBuffer(
                                      _dataVault,
                                      in _assetTtlVaultHandle,
                                      BufferID.AddressableHeapTimeToLive,
                                      capacity) ||
                                  !HasHeapSanitizerVaultBuffer(
                                      _dataVault,
                                      in _assetTrackerFlagsVaultHandle,
                                      BufferID.AddressableHeapTrackerFlags,
                                      capacity) ||
                                  !HasHeapSanitizerVaultBuffer(
                                      _dataVault,
                                      in _assetHandleMapVaultHandle,
                                      BufferID.AddressableHeapHandleMap,
                                      mapCapacity) ||
                                  !HasHeapSanitizerVaultBuffer(
                                      _dataVault,
                                      in _cacheProfileVaultHandle,
                                      BufferID.AddressableHeapCacheProfiles,
                                      CacheProfileCapacity) ||
                                  !HasHeapSanitizerVaultBuffer(
                                      _dataVault,
                                      in _heapTelemetryVaultHandle,
                                      BufferID.AddressableHeapTelemetry,
                                      HeapTelemetryCapacity);

            if (!TryResolveHeapSanitizerVaultBuffers(capacity, mapCapacity))
            {
                EnsureFallbackImpostorMesh();
                return;
            }

            if (!TryResolveHeapSanitizerVaultBuffer(
                    ref _assetTrackerVaultHandle,
                    BufferID.AddressableHeapTrackers,
                    capacity,
                    out NativeArray<AssetTrackerDTO> trackers) ||
                !TryResolveHeapSanitizerVaultBuffer(
                    ref _assetTtlVaultHandle,
                    BufferID.AddressableHeapTimeToLive,
                    capacity,
                    out NativeArray<float> ttl) ||
                !TryResolveHeapSanitizerVaultBuffer(
                    ref _assetTrackerFlagsVaultHandle,
                    BufferID.AddressableHeapTrackerFlags,
                    capacity,
                    out NativeArray<byte> flags) ||
                !TryResolveHeapSanitizerVaultBuffer(
                    ref _assetHandleMapVaultHandle,
                    BufferID.AddressableHeapHandleMap,
                    mapCapacity,
                    out NativeArray<AssetHandleMapEntryDTO> map) ||
                !TryResolveHeapSanitizerVaultBuffer(
                    ref _cacheProfileVaultHandle,
                    BufferID.AddressableHeapCacheProfiles,
                    CacheProfileCapacity,
                    out NativeArray<AssetCacheProfileDTO> profiles) ||
                !TryResolveHeapSanitizerVaultBuffer(
                    ref _heapTelemetryVaultHandle,
                    BufferID.AddressableHeapTelemetry,
                    HeapTelemetryCapacity,
                    out NativeArray<AssetHeapTelemetryEntry> telemetry))
            {
                EnsureFallbackImpostorMesh();
                return;
            }

#if UNITY_ADDRESSABLES_EXIST
            if (needsColdClear && HasAddressableHandleStorage() && !TryReleaseAddressableHandleStorageForReset(true))
            {
                EnsureFallbackImpostorMesh();
                return;
            }
#endif

            if (needsColdClear)
            {
                ClearAddressableHeapVaultState(
                    trackers,
                    ttl,
                    flags,
                    map,
                    profiles,
                    telemetry,
                    true,
                    true);
            }

            _resolvedHandleCapacity = capacity;
            _resolvedMapCapacity = mapCapacity;
            _nativeStorageInitialized = true;

            if (!TryCopyCacheProfilesFromVault())
            {
                GenerateEmergencyMockCacheProfiles();
                MirrorCacheProfilesToVault();
            }
            EnsureFallbackImpostorMesh();

#if UNITY_ADDRESSABLES_EXIST
            if (_addressableHandlePool == null || _addressableHandlePool.Length != capacity)
            {
                if (HasAddressableHandleStorage() && !TryReleaseAddressableHandleStorageForReset(true))
                {
                    EnsureFallbackImpostorMesh();
                    return;
                }

                _addressableHandlePool = new AsyncOperationHandle[capacity]; // COLD ALLOC: AsyncOperationHandle[capacity] - Unity handle bridge, indexed by Vault tracker slot - owner: AssetLifecycleGovernor
                _detachedReleaseHandles = new AsyncOperationHandle[DetachedReleaseHandleCapacity]; // COLD ALLOC: AsyncOperationHandle[64] - raw handles awaiting blind-frame release after failed registration - owner: AssetLifecycleGovernor
                _addressableHandleHashes = new uint[capacity]; // COLD ALLOC: uint[capacity] - handle slot asset hashes - owner: AssetLifecycleGovernor
                _addressableBundlePrefixHashes = new uint[capacity]; // COLD ALLOC: uint[capacity] - bundle prefix hashes for TTL inflation - owner: AssetLifecycleGovernor
                _addressableHandleCount = 0;
                _detachedReleaseHandleCount = 0;
            }
#endif
        }

        private bool DisposeNativeHandleStorage()
        {
            CompleteTtlEvaluationForTeardown();

#if UNITY_ADDRESSABLES_EXIST
            if (!TryReleaseAddressableHandleStorageForReset(true))
                return false;

            _addressableHandlePool = null;
            _detachedReleaseHandles = null;
            _addressableHandleHashes = null;
            _addressableBundlePrefixHashes = null;
            _addressableHandleCount = 0;
            _detachedReleaseHandleCount = 0;
#endif
            ReleaseHeapSanitizerVaultHandles(_dataVault);
            _dataVault = null;
            InvalidateVaultHandleDescriptors();
            _cachedDispatcher = null;
            _cachedAssetLoadDispatcher = null;
            _cachedVramPressure = null;
            _cachedPlayer = null;
            _cachedPlayerInventory = null;
            _cachedScannerInterferenceUi = null;
            _ttlEvaluationResultsPending = false;
            _ttlEvaluationFlagsMirrored = false;
            _nativeRefSyncRequired = false;

            _fallbackImpostorMesh = null;

            return true;
        }

#if UNITY_ADDRESSABLES_EXIST
        private bool HasAddressableHandleStorage()
        {
            return _addressableHandlePool != null ||
                   _detachedReleaseHandles != null ||
                   _addressableHandleHashes != null ||
                   _addressableBundlePrefixHashes != null ||
                   _addressableHandleCount > 0 ||
                   _detachedReleaseHandleCount > 0;
        }

        private bool TryReleaseAddressableHandleStorageForReset(bool forceBlindFrame)
        {
            bool previousBlindActive = _explicitBlindFrameWindowActive;
            float previousBlindUntil = _explicitBlindFrameWindowUntil;
            if (forceBlindFrame)
            {
                _explicitBlindFrameWindowActive = true;
                _explicitBlindFrameWindowUntil = 0f;
            }

            bool allReleased = true;
            if (_addressableHandlePool != null)
            {
                for (int i = 0; i < _addressableHandlePool.Length; i++)
                {
                    AsyncOperationHandle handle = _addressableHandlePool[i];
                    if (handle.IsValid() && !TryExecuteOrDeferBlindFrameRelease(handle))
                    {
                        allReleased = false;
                        continue;
                    }

                    _addressableHandlePool[i] = default;
                    if (_addressableHandleHashes != null && (uint)i < (uint)_addressableHandleHashes.Length)
                        _addressableHandleHashes[i] = 0u;
                    if (_addressableBundlePrefixHashes != null && (uint)i < (uint)_addressableBundlePrefixHashes.Length)
                        _addressableBundlePrefixHashes[i] = 0u;
                }
            }

            if (_detachedReleaseHandles != null)
            {
                int count = math.min(_detachedReleaseHandleCount, _detachedReleaseHandles.Length);
                for (int i = 0; i < count; i++)
                {
                    AsyncOperationHandle handle = _detachedReleaseHandles[i];
                    if (handle.IsValid() && !TryExecuteOrDeferBlindFrameRelease(handle))
                    {
                        allReleased = false;
                        continue;
                    }

                    _detachedReleaseHandles[i] = default;
                }
            }

            if (allReleased)
            {
                _addressableHandleCount = 0;
                _detachedReleaseHandleCount = 0;
            }
            else
            {
                _lastLeakSuspectHash = _lastLeakSuspectHash != 0u ? _lastLeakSuspectHash : CollisionSalt;
                DumpHeapTelemetry();
            }

            if (forceBlindFrame)
            {
                _explicitBlindFrameWindowActive = previousBlindActive;
                _explicitBlindFrameWindowUntil = previousBlindUntil;
            }

            return allReleased;
        }
#endif

        private void ClearAddressableHeapVaultState(bool clearCacheProfiles, bool clearTelemetry)
        {
            if (_dataVault == null)
                return;

            TryResolveExistingHeapSanitizerVaultBuffer(
                in _assetTrackerVaultHandle,
                BufferID.AddressableHeapTrackers,
                1,
                out NativeArray<AssetTrackerDTO> trackers);
            TryResolveExistingHeapSanitizerVaultBuffer(
                in _assetTtlVaultHandle,
                BufferID.AddressableHeapTimeToLive,
                1,
                out NativeArray<float> ttl);
            TryResolveExistingHeapSanitizerVaultBuffer(
                in _assetTrackerFlagsVaultHandle,
                BufferID.AddressableHeapTrackerFlags,
                1,
                out NativeArray<byte> flags);
            TryResolveExistingHeapSanitizerVaultBuffer(
                in _assetHandleMapVaultHandle,
                BufferID.AddressableHeapHandleMap,
                1,
                out NativeArray<AssetHandleMapEntryDTO> map);
            TryResolveExistingHeapSanitizerVaultBuffer(
                in _cacheProfileVaultHandle,
                BufferID.AddressableHeapCacheProfiles,
                1,
                out NativeArray<AssetCacheProfileDTO> profiles);
            TryResolveExistingHeapSanitizerVaultBuffer(
                in _heapTelemetryVaultHandle,
                BufferID.AddressableHeapTelemetry,
                1,
                out NativeArray<AssetHeapTelemetryEntry> telemetry);

            ClearAddressableHeapVaultState(
                trackers,
                ttl,
                flags,
                map,
                profiles,
                telemetry,
                clearCacheProfiles,
                clearTelemetry);
        }

        private void ClearAddressableHeapVaultState(
            NativeArray<AssetTrackerDTO> trackers,
            NativeArray<float> ttl,
            NativeArray<byte> flags,
            NativeArray<AssetHandleMapEntryDTO> map,
            NativeArray<AssetCacheProfileDTO> profiles,
            NativeArray<AssetHeapTelemetryEntry> telemetry,
            bool clearCacheProfiles,
            bool clearTelemetry)
        {
            int clearLength = 0;
            if (trackers.IsCreated)
                clearLength = math.max(clearLength, trackers.Length);
            if (ttl.IsCreated)
                clearLength = math.max(clearLength, ttl.Length);
            if (flags.IsCreated)
                clearLength = math.max(clearLength, flags.Length);
            if (map.IsCreated)
                clearLength = math.max(clearLength, map.Length);

            if (clearLength > 0)
            {
                HeapSanitizerMemClearJob clearJob = new HeapSanitizerMemClearJob
                {
                    Trackers = trackers,
                    TimeToLiveSeconds = ttl,
                    HandleMap = map
                };
                // COLD SYNC JOB: boot/teardown Vault sanitizer clear; avoids OS memset on steady-state native heap.
                for (int i = 0; i < clearLength; i++)
                    clearJob.Execute(i);
            }

            if (flags.IsCreated)
            {
                for (int i = 0; i < flags.Length; i++)
                    flags[i] = 0;
            }

            if (clearCacheProfiles && profiles.IsCreated)
            {
                for (int i = 0; i < profiles.Length; i++)
                    profiles[i] = default;
            }

            if (!clearTelemetry || !telemetry.IsCreated)
                return;

            for (int i = 0; i < telemetry.Length; i++)
                telemetry[i] = default;

            _heapTelemetryCursor = 0;
        }

        private bool TryResolveTrackerViews(
            out NativeArray<AssetTrackerDTO> trackers,
            out NativeArray<float> ttl,
            out NativeArray<byte> flags,
            out NativeArray<AssetHandleMapEntryDTO> handleMap)
        {
            trackers = default;
            ttl = default;
            flags = default;
            handleMap = default;

            if (!TryResolveHeapSanitizerVaultBuffers())
                return false;

            int capacity = Mathf.Clamp(maxTrackedAddressableHandles, 1, MaxTrackedAddressableCapacity);
            int mapCapacity = ResolveHandleMapCapacity(capacity);
            return TryResolveHeapSanitizerVaultBuffer(
                       ref _assetTrackerVaultHandle,
                       BufferID.AddressableHeapTrackers,
                       capacity,
                       out trackers) &&
                   TryResolveHeapSanitizerVaultBuffer(
                       ref _assetTtlVaultHandle,
                       BufferID.AddressableHeapTimeToLive,
                       capacity,
                       out ttl) &&
                   TryResolveHeapSanitizerVaultBuffer(
                       ref _assetTrackerFlagsVaultHandle,
                       BufferID.AddressableHeapTrackerFlags,
                       capacity,
                       out flags) &&
                   TryResolveHeapSanitizerVaultBuffer(
                       ref _assetHandleMapVaultHandle,
                       BufferID.AddressableHeapHandleMap,
                       mapCapacity,
                       out handleMap);
        }

        private bool TryResolveCacheProfileView(out NativeArray<AssetCacheProfileDTO> profiles)
        {
            profiles = default;
            if (!TryResolveHeapSanitizerVaultBuffers())
                return false;

            return TryResolveHeapSanitizerVaultBuffer(
                ref _cacheProfileVaultHandle,
                BufferID.AddressableHeapCacheProfiles,
                CacheProfileCapacity,
                out profiles);
        }

        private bool TryResolveCacheProfileCsvScratch(out NativeArray<byte> scratch)
        {
            scratch = default;
            if (!TryResolveHeapSanitizerVaultBuffers())
                return false;

            return TryResolveHeapSanitizerVaultBuffer(
                ref _cacheProfileCsvScratchVaultHandle,
                BufferID.AddressableHeapCsvScratch,
                CacheProfileCsvScratchBytes,
                out scratch);
        }

        private bool TryResolveTelemetryView(out NativeArray<AssetHeapTelemetryEntry> telemetry)
        {
            telemetry = default;
            if (!TryResolveHeapSanitizerVaultBuffers())
                return false;

            return TryResolveHeapSanitizerVaultBuffer(
                ref _heapTelemetryVaultHandle,
                BufferID.AddressableHeapTelemetry,
                HeapTelemetryCapacity,
                out telemetry);
        }

        private void CompleteTtlEvaluationForTeardown()
        {
            _ttlEvaluationResultsPending = false;
            _ttlEvaluationVramPanic = false;
            _ttlEvaluationFlagsMirrored = false;
        }


#if UNITY_ADDRESSABLES_EXIST


        private bool TryAcquireTrackedHandle(
            uint assetHash,
            string address,
            Component owner,
            AssetPriorityTier priority,
            AssetResidencyKind residencyKind,
            long sizeBytes,
            bool isChunkAsset,
            out AsyncOperationHandle<GameObject> handle,
            out bool cacheHit)
        {
            handle = default;
            cacheHit = false;
            if (!TryResolveTrackerViews(
                    out NativeArray<AssetTrackerDTO> trackers,
                    out NativeArray<float> ttl,
                    out NativeArray<byte> trackerFlags,
                    out NativeArray<AssetHandleMapEntryDTO> handleMap) ||
                !TryGetHandleSlot(assetHash, handleMap, out int slot) ||
                !IsValidHandleSlot(slot, trackers))
            {
                return false;
            }

            AsyncOperationHandle rawHandle = _addressableHandlePool[slot];
            if (!rawHandle.IsValid())
            {
                ClearNativeHandleSlot(assetHash);
                return false;
            }

            AssetTrackerDTO previousTracker = trackers[slot];
            byte previousFlags = trackerFlags[slot];
            float previousTtl = ttl[slot];
            bool hadMapEntry = TryFindHandleMapIndex(assetHash, handleMap, out int mapIndex);
            AssetHandleMapEntryDTO previousMapEntry = hadMapEntry ? handleMap[mapIndex] : default;

            int refCount = AssetTrackerAtomic.Increment(trackers, slot);
            SetNativeRefCount(assetHash, slot, refCount, trackers, handleMap);
            AssetTrackerDTO refreshedTracker = trackers[slot];
            refreshedTracker.MaxResidencyRadiusSq = DistantChunkReleaseDistanceMeters * DistantChunkReleaseDistanceMeters;
            byte flags = trackerFlags[slot];
            flags = (byte)(flags & ~(AssetHandleFlags.PendingTtl | AssetHandleFlags.Releasable));
            flags = rawHandle.IsDone
                ? (byte)(flags & ~AssetHandleFlags.Loading)
                : (byte)(flags | AssetHandleFlags.Loading);
            refreshedTracker.Flags = (refreshedTracker.Flags & 0xFFFFFF00u) | flags;
            trackers[slot] = refreshedTracker;
            trackerFlags[slot] = flags;
            ttl[slot] = 0f;
            ClearHandleMapTtl(handleMap, assetHash);

            if (_assetRecords.TryGetValue(assetHash, out AssetRecord record))
            {
                record.RefCount = refCount;
                record.Owner = owner != null ? owner : record.Owner;
                record.Priority = priority;
                record.ResidencyKind = residencyKind;
                record.PendingRelease = false;
                record.LastAccessFrame = _frameSequence;
                record.IsChunkAsset = isChunkAsset;

                if (sizeBytes > 0L)
                    ReplaceTrackedSize(ref record, sizeBytes);

                _assetRecords.Set(assetHash, record);
            }
            else
            {
                long recoveredSize = ClampNonNegative(sizeBytes);
                AssetRecord recoveredRecord = new AssetRecord
                {
                    Key = assetHash,
                    AssetGuid = null,
                    Address = address,
                    Asset = null,
                    Owner = owner,
                    RefCount = refCount,
                    Priority = priority,
                    ResidencyKind = residencyKind,
                    PendingRelease = false,
                    IsFallback = false,
                    OwnsAssetInstance = false,
                    IsChunkAsset = isChunkAsset,
                    HasAddressableHandle = rawHandle.IsValid(),
                    AddressableHandle = rawHandle,
                    RetryCount = 0,
                    BiomeId = 0,
                    LodLevel = 0,
                    LastAccessFrame = _frameSequence,
                    SizeBytes = recoveredSize,
                    ActiveRequestId = 0,
                    NextRetryTime = 0f
                };

                if (!_assetRecords.Set(assetHash, recoveredRecord))
                {
                    trackers[slot] = previousTracker;
                    trackerFlags[slot] = previousFlags;
                    ttl[slot] = previousTtl;
                    if (hadMapEntry)
                    {
                        ref AssetHandleMapEntryDTO restoredEntry = ref GetEntryAsRef(handleMap, mapIndex);
                        restoredEntry = previousMapEntry;
                    }

                    _nativeRefSyncRequired = true;
                    _lastLeakSuspectHash = assetHash;
                    DumpHeapTelemetry();
                    return false;
                }

                TrackedResidentBytes += recoveredSize;
            }

            handle = rawHandle.Convert<GameObject>();
            cacheHit = true;
            _cacheHitCount++;
            return true;
        }

        private int AllocateAddressableHandleSlot(uint assetHash, NativeArray<byte> trackerFlags)
        {
            if (_addressableHandlePool == null)
                return -1;

            if (_addressableHandleCount < _addressableHandlePool.Length)
                return _addressableHandleCount++;

            for (int i = 0; i < _addressableHandlePool.Length; i++)
            {
                if ((trackerFlags[i] & AssetHandleFlags.Active) == 0)
                    return i;
            }

            _lastLeakSuspectHash = assetHash;
            return -1;
        }

        private void HandleAddressableMapPressureIfNeeded(
            NativeArray<AssetTrackerDTO> trackers,
            NativeArray<float> ttl,
            NativeArray<byte> trackerFlags,
            NativeArray<AssetHandleMapEntryDTO> handleMap)
        {
            if (!TryGetAddressableMapPressure(handleMap, out int occupied, out int used, out int pressureThreshold) ||
                used < pressureThreshold)
                return;

            if (occupied < pressureThreshold)
            {
                CompactAddressableHandleMap(trackers, ttl, trackerFlags, handleMap);
                return;
            }

            int forced = ForceFurthestUnusedAddressableTtlsToZero(
                trackers,
                ttl,
                trackerFlags,
                handleMap,
                ResolvePlayerAupForEviction());
            if (forced <= 0)
                return;

            _forcedVramReleaseCount += forced;
            DrainTtlEvaluationResults(trackers, ttl, trackerFlags, handleMap, true);
            CompactAddressableHandleMap(trackers, ttl, trackerFlags, handleMap);
        }

        private static bool TryGetAddressableMapPressure(
            NativeArray<AssetHandleMapEntryDTO> handleMap,
            out int occupied,
            out int used,
            out int pressureThreshold)
        {
            occupied = 0;
            used = 0;
            pressureThreshold = 0;
            if (!handleMap.IsCreated || handleMap.Length <= 0)
                return false;

            for (int i = 0; i < handleMap.Length; i++)
            {
                uint flags = handleMap[i].Flags;
                if ((flags & AssetHandleMapFlags.Occupied) != 0u)
                    occupied++;
                if ((flags & (AssetHandleMapFlags.Occupied | AssetHandleMapFlags.Tombstone)) != 0u)
                    used++;
            }

            pressureThreshold = (int)(handleMap.Length * AddressableMapPressureThreshold);
            return true;
        }

        private void CompactAddressableHandleMap(
            NativeArray<AssetTrackerDTO> trackers,
            NativeArray<float> ttl,
            NativeArray<byte> trackerFlags,
            NativeArray<AssetHandleMapEntryDTO> handleMap)
        {
            if (!trackers.IsCreated || !ttl.IsCreated || !trackerFlags.IsCreated || !handleMap.IsCreated)
                return;

            for (int i = 0; i < handleMap.Length; i++)
                handleMap[i] = default;

            int length = math.min(math.min(trackers.Length, ttl.Length), trackerFlags.Length);
            for (int slot = 0; slot < length; slot++)
            {
                byte flags = trackerFlags[slot];
                if ((flags & AssetHandleFlags.Active) == 0)
                    continue;

                AssetTrackerDTO tracker = trackers[slot];
                uint assetHash = tracker.AssetHash;
                if (assetHash == 0u)
                    continue;

                if ((flags & AssetHandleFlags.BundleShared) != 0)
                {
                    flags = (byte)(flags & ~AssetHandleFlags.BundleShared);
                    trackerFlags[slot] = flags;
                    tracker.Flags = (tracker.Flags & 0xFFFFFF00u) | flags;
                    trackers[slot] = tracker;
                }

                uint bundlePrefixHash = ResolveBundlePrefixHashForSlot(slot);
                bool sharedBundle = bundlePrefixHash != 0u && CountActiveBundlePrefix(handleMap, bundlePrefixHash) > 0;
                if (sharedBundle)
                {
                    MarkBundlePrefixShared(handleMap, trackerFlags, trackers, bundlePrefixHash, true);
                    flags = (byte)(flags | AssetHandleFlags.BundleShared);
                }

                float ttlSeconds = ttl[slot];
                ttlSeconds = math.isfinite(ttlSeconds) ? ttlSeconds : 0f;
                if (UpsertHandleMapEntry(handleMap, assetHash, slot, bundlePrefixHash, tracker.ReferenceCount, sharedBundle, ttlSeconds))
                {
                    if (sharedBundle)
                    {
                        trackerFlags[slot] = flags;
                        tracker.Flags = (tracker.Flags & 0xFFFFFF00u) | flags;
                        trackers[slot] = tracker;
                    }

                    continue;
                }

                _lastLeakSuspectHash = assetHash;
                DumpHeapTelemetry();
                return;
            }
        }

        private bool RegisterAddressableHandleSlot(
            int slot,
            uint assetHash,
            uint bundlePrefixHash,
            string address,
            Component owner,
            AssetPriorityTier priority,
            AssetResidencyKind residencyKind,
            long sizeBytes,
            bool isChunkAsset,
            AsyncOperationHandle handle)
        {
            if (!TryResolveTrackerViews(
                    out NativeArray<AssetTrackerDTO> trackers,
                    out NativeArray<float> ttl,
                    out NativeArray<byte> trackerFlags,
                    out NativeArray<AssetHandleMapEntryDTO> handleMap) ||
                !IsValidHandleSlot(slot, trackers))
            {
                return false;
            }

            bool hadRecord = _assetRecords.TryGetValue(assetHash, out AssetRecord previousRecord);
            AssetRecord record = hadRecord
                ? previousRecord
                : new AssetRecord
                {
                    Key = assetHash,
                    AssetGuid = null
                };
            long previousSize = hadRecord ? previousRecord.SizeBytes : 0L;
            long nextSize = sizeBytes > 0L ? ClampNonNegative(sizeBytes) : previousSize;

            record.Key = assetHash;
            record.Address = address;
            record.Asset = null;
            record.Owner = owner;
            record.RefCount = 1;
            record.Priority = priority;
            record.ResidencyKind = residencyKind;
            record.PendingRelease = false;
            record.IsFallback = false;
            record.OwnsAssetInstance = false;
            record.IsChunkAsset = isChunkAsset;
            record.HasAddressableHandle = handle.IsValid();
            record.AddressableHandle = handle;
            record.RetryCount = 0;
            record.BiomeId = 0;
            record.LodLevel = 0;
            record.LastAccessFrame = _frameSequence;
            record.SizeBytes = nextSize;
            record.ActiveRequestId = 0;
            record.NextRetryTime = 0f;
            if (!_assetRecords.Set(assetHash, record))
            {
                _lastLeakSuspectHash = assetHash;
                DumpHeapTelemetry();
                return false;
            }

            bool hadMapEntry = TryFindHandleMapIndex(assetHash, handleMap, out int previousMapIndex);
            AssetHandleMapEntryDTO previousMapEntry = hadMapEntry ? handleMap[previousMapIndex] : default;
            RemoveHandleMapEntry(handleMap, assetHash);
            bool sharedBundle = bundlePrefixHash != 0u && CountActiveBundlePrefix(handleMap, bundlePrefixHash) > 0;
            if (!UpsertHandleMapEntry(handleMap, assetHash, slot, bundlePrefixHash, 1, sharedBundle))
            {
                if (hadRecord)
                    _assetRecords.Set(assetHash, previousRecord);
                else
                    _assetRecords.Remove(assetHash);
                if (hadMapEntry)
                {
                    ref AssetHandleMapEntryDTO restoredEntry = ref GetEntryAsRef(handleMap, previousMapIndex);
                    restoredEntry = previousMapEntry;
                }

                _lastLeakSuspectHash = assetHash;
                DumpHeapTelemetry();
                return false;
            }

            _addressableHandlePool[slot] = handle;
            _addressableHandleHashes[slot] = assetHash;
            _addressableBundlePrefixHashes[slot] = bundlePrefixHash;
            byte flags = (byte)(AssetHandleFlags.Active | AssetHandleFlags.Loading);
            if (sharedBundle)
                flags = (byte)(flags | AssetHandleFlags.BundleShared);
            trackers[slot] = new AssetTrackerDTO
            {
                AssetHash = assetHash,
                ReferenceCount = 1,
                HandlePointer = unchecked((ulong)(slot + 1)),
                MaxResidencyRadiusSq = DistantChunkReleaseDistanceMeters * DistantChunkReleaseDistanceMeters,
                Flags = flags | AssetTrackerMetaFlags.UnknownAup
            };
            ttl[slot] = 0f;
            trackerFlags[slot] = flags;
            if (sharedBundle)
                MarkBundlePrefixShared(handleMap, trackerFlags, trackers, bundlePrefixHash, true);

            TrackedResidentBytes += record.SizeBytes - previousSize;
            if (TrackedResidentBytes < 0L)
                TrackedResidentBytes = 0L;
            return true;
        }

        private bool IsValidHandleSlot(int slot, NativeArray<AssetTrackerDTO> trackers)
        {
            return _addressableHandlePool != null &&
                   (uint)slot < (uint)_addressableHandlePool.Length &&
                   trackers.IsCreated &&
                   (uint)slot < (uint)trackers.Length;
        }
#endif

        private static int ResolveHandleMapCapacity(int handleCapacity)
        {
            return MaxAddressableHandleMapCapacity;
        }

        private bool TryGetHandleSlot(uint assetHash, out int slot)
        {
            slot = -1;
            if (!TryResolveTrackerViews(
                    out _,
                    out _,
                    out _,
                    out NativeArray<AssetHandleMapEntryDTO> handleMap))
            {
                return false;
            }

            return TryGetHandleSlot(assetHash, handleMap, out slot);
        }

        private static bool TryGetHandleSlot(
            uint assetHash,
            NativeArray<AssetHandleMapEntryDTO> handleMap,
            out int slot)
        {
            slot = -1;
            if (!TryFindHandleMapIndex(assetHash, handleMap, out int index))
                return false;

            AssetHandleMapEntryDTO entry = handleMap[index];
            slot = entry.PoolSlotIndex;
            return slot >= 0 && unchecked((uint)entry.AssetHash) == assetHash;
        }

        private static bool TryFindHandleMapIndex(
            uint assetHash,
            NativeArray<AssetHandleMapEntryDTO> handleMap,
            out int index)
        {
            index = -1;
            if (assetHash == 0u || !handleMap.IsCreated || handleMap.Length == 0)
                return false;

            int length = handleMap.Length;
            int start = (int)(assetHash % unchecked((uint)length));
            for (int probe = 0; probe < length; probe++)
            {
                int candidateIndex = start + probe;
                if (candidateIndex >= length)
                    candidateIndex -= length;

                AssetHandleMapEntryDTO entry = handleMap[candidateIndex];
                uint flags = entry.Flags;
                if ((flags & AssetHandleMapFlags.Occupied) != 0u)
                {
                    if (unchecked((uint)entry.AssetHash) == assetHash)
                    {
                        index = candidateIndex;
                        return true;
                    }

                    continue;
                }

                if ((flags & AssetHandleMapFlags.Tombstone) == 0u)
                    return false;
            }

            return false;
        }

        private static bool TryFindHandleMapInsertIndex(
            uint assetHash,
            NativeArray<AssetHandleMapEntryDTO> handleMap,
            out int index)
        {
            index = -1;
            if (assetHash == 0u || !handleMap.IsCreated || handleMap.Length == 0)
                return false;

            int firstTombstone = -1;
            int length = handleMap.Length;
            int start = (int)(assetHash % unchecked((uint)length));
            for (int probe = 0; probe < length; probe++)
            {
                int candidateIndex = start + probe;
                if (candidateIndex >= length)
                    candidateIndex -= length;

                AssetHandleMapEntryDTO entry = handleMap[candidateIndex];
                uint flags = entry.Flags;
                if ((flags & AssetHandleMapFlags.Occupied) != 0u)
                {
                    if (unchecked((uint)entry.AssetHash) == assetHash)
                    {
                        index = candidateIndex;
                        return true;
                    }

                    continue;
                }

                if ((flags & AssetHandleMapFlags.Tombstone) != 0u)
                {
                    if (firstTombstone < 0)
                        firstTombstone = candidateIndex;
                    continue;
                }

                index = firstTombstone >= 0 ? firstTombstone : candidateIndex;
                return true;
            }

            if (firstTombstone >= 0)
            {
                index = firstTombstone;
                return true;
            }

            return false;
        }

        private static ref AssetHandleMapEntryDTO GetEntryAsRef(
            NativeArray<AssetHandleMapEntryDTO> handleMap,
            int index)
        {
            unsafe
            {
                void* basePtr = NativeArrayUnsafeUtility.GetUnsafePtr(handleMap);
                int stride = UnsafeUtility.SizeOf<AssetHandleMapEntryDTO>();
                return ref UnsafeUtility.AsRef<AssetHandleMapEntryDTO>((byte*)basePtr + (stride * index));
            }
        }

        private static bool UpsertHandleMapEntry(
            NativeArray<AssetHandleMapEntryDTO> handleMap,
            uint assetHash,
            int slot,
            uint bundlePrefixHash,
            int refCount,
            bool sharedBundle,
            float timeToLive = 0f)
        {
            if (!TryFindHandleMapInsertIndex(assetHash, handleMap, out int index))
                return false;

            AssetHandleMapEntryDTO current = handleMap[index];
            uint generation = current.Generation + 1u;
            if (generation == 0u)
                generation = 1u;

            uint flags = AssetHandleMapFlags.Occupied;
            if (sharedBundle)
                flags |= AssetHandleMapFlags.BundleShared;

            ref AssetHandleMapEntryDTO target = ref GetEntryAsRef(handleMap, index);
            target = new AssetHandleMapEntryDTO
            {
                AssetHash = assetHash,
                BundlePrefixHash = bundlePrefixHash,
                PoolSlotIndex = slot,
                RefCount = refCount,
                TimeToLive = math.isfinite(timeToLive) ? timeToLive : 0f,
                Flags = flags,
                Generation = generation
            };
            return true;
        }

        private void UpdateHandleMapRefCount(
            NativeArray<AssetHandleMapEntryDTO> handleMap,
            uint assetHash,
            int slot,
            int refCount)
        {
            if (assetHash == 0u || slot < 0)
                return;

            if (TryFindHandleMapIndex(assetHash, handleMap, out int index))
            {
                ref AssetHandleMapEntryDTO entry = ref GetEntryAsRef(handleMap, index);
                entry.PoolSlotIndex = slot;
                entry.RefCount = refCount;
                return;
            }

            uint bundlePrefixHash = ResolveBundlePrefixHashForSlot(slot);
            bool sharedBundle = bundlePrefixHash != 0u && CountActiveBundlePrefix(handleMap, bundlePrefixHash) > 0;
            UpsertHandleMapEntry(handleMap, assetHash, slot, bundlePrefixHash, refCount, sharedBundle);
        }

        private static void ClearHandleMapTtl(NativeArray<AssetHandleMapEntryDTO> handleMap, uint assetHash)
        {
            if (assetHash == 0u || !TryFindHandleMapIndex(assetHash, handleMap, out int index))
                return;

            ref AssetHandleMapEntryDTO entry = ref GetEntryAsRef(handleMap, index);
            entry.TimeToLive = 0f;
        }

        private static void RemoveHandleMapEntry(NativeArray<AssetHandleMapEntryDTO> handleMap, uint assetHash)
        {
            if (!TryFindHandleMapIndex(assetHash, handleMap, out int index))
                return;

            ref AssetHandleMapEntryDTO entry = ref GetEntryAsRef(handleMap, index);
            ulong previousAssetHash = entry.AssetHash;
            uint nextGeneration = entry.Generation + 1u;
            if (nextGeneration == 0u)
                nextGeneration = 1u;

            entry = new AssetHandleMapEntryDTO
            {
                AssetHash = previousAssetHash,
                Flags = AssetHandleMapFlags.Tombstone,
                Generation = nextGeneration
            };
        }

        private static int CountActiveBundlePrefix(
            NativeArray<AssetHandleMapEntryDTO> handleMap,
            uint bundlePrefixHash)
        {
            if (bundlePrefixHash == 0u || !handleMap.IsCreated)
                return 0;

            int count = 0;
            for (int i = 0; i < handleMap.Length; i++)
            {
                AssetHandleMapEntryDTO entry = handleMap[i];
                if ((entry.Flags & AssetHandleMapFlags.Occupied) != 0u &&
                    unchecked((uint)entry.BundlePrefixHash) == bundlePrefixHash)
                {
                    count++;
                }
            }

            return count;
        }

        private static void MarkBundlePrefixShared(
            NativeArray<AssetHandleMapEntryDTO> handleMap,
            NativeArray<byte> trackerFlags,
            NativeArray<AssetTrackerDTO> trackers,
            uint bundlePrefixHash,
            bool shared)
        {
            if (bundlePrefixHash == 0u || !handleMap.IsCreated)
                return;

            for (int i = 0; i < handleMap.Length; i++)
            {
                AssetHandleMapEntryDTO entry = handleMap[i];
                if ((entry.Flags & AssetHandleMapFlags.Occupied) == 0u ||
                    unchecked((uint)entry.BundlePrefixHash) != bundlePrefixHash)
                {
                    continue;
                }

                ref AssetHandleMapEntryDTO mutableEntry = ref GetEntryAsRef(handleMap, i);
                mutableEntry.Flags = shared
                    ? entry.Flags | AssetHandleMapFlags.BundleShared
                    : entry.Flags & ~AssetHandleMapFlags.BundleShared;

                int slot = mutableEntry.PoolSlotIndex;
                if (trackerFlags.IsCreated && (uint)slot < (uint)trackerFlags.Length)
                {
                    byte flags = trackerFlags[slot];
                    byte updatedFlags = shared
                        ? (byte)(flags | AssetHandleFlags.BundleShared)
                        : (byte)(flags & ~AssetHandleFlags.BundleShared);
                    trackerFlags[slot] = updatedFlags;

                    if (trackers.IsCreated && (uint)slot < (uint)trackers.Length)
                    {
                        AssetTrackerDTO tracker = trackers[slot];
                        tracker.Flags = (tracker.Flags & 0xFFFFFF00u) | updatedFlags;
                        trackers[slot] = tracker;
                    }
                }
            }
        }

        private static void RecomputeBundlePrefixSharing(
            NativeArray<AssetHandleMapEntryDTO> handleMap,
            NativeArray<byte> trackerFlags,
            NativeArray<AssetTrackerDTO> trackers,
            uint bundlePrefixHash)
        {
            if (bundlePrefixHash == 0u)
                return;

            MarkBundlePrefixShared(
                handleMap,
                trackerFlags,
                trackers,
                bundlePrefixHash,
                CountActiveBundlePrefix(handleMap, bundlePrefixHash) > 1);
        }

        private uint ResolveBundlePrefixHashForSlot(int slot)
        {
#if UNITY_ADDRESSABLES_EXIST
            if (_addressableBundlePrefixHashes != null && (uint)slot < (uint)_addressableBundlePrefixHashes.Length)
                return _addressableBundlePrefixHashes[slot];
#endif
            return 0u;
        }

        private int TryDecrementNativeRefCount(uint assetHash, out int slot)
        {
            slot = -1;

            if (!TryResolveTrackerViews(
                    out NativeArray<AssetTrackerDTO> trackers,
                    out _,
                    out _,
                    out NativeArray<AssetHandleMapEntryDTO> handleMap) ||
                !TryGetHandleSlot(assetHash, handleMap, out slot) ||
                (uint)slot >= (uint)trackers.Length)
            {
                slot = -1;
                return -1;
            }

            int refCount = AssetTrackerAtomic.Decrement(trackers, slot);
            SetNativeRefCount(assetHash, slot, refCount, trackers, handleMap);
            return refCount;
        }

        private void SetNativeRefCount(
            uint assetHash,
            int slot,
            int refCount,
            NativeArray<AssetTrackerDTO> trackers,
            NativeArray<AssetHandleMapEntryDTO> handleMap)
        {
            UpdateHandleMapRefCount(handleMap, assetHash, slot, refCount);
            if (trackers.IsCreated && (uint)slot < (uint)trackers.Length)
            {
                AssetTrackerDTO tracker = trackers[slot];
                tracker.ReferenceCount = refCount;
                trackers[slot] = tracker;
            }
        }

        private static void MirrorTrackerFlagBytesIntoDto(
            NativeArray<AssetTrackerDTO> trackers,
            NativeArray<byte> trackerFlags)
        {
            if (!trackers.IsCreated || !trackerFlags.IsCreated)
                return;

            int length = math.min(trackers.Length, trackerFlags.Length);
            for (int i = 0; i < length; i++)
            {
                uint flags = trackerFlags[i];
                AssetTrackerDTO tracker = trackers[i];
                uint mirroredFlags = (tracker.Flags & 0xFFFFFF00u) | flags;
                if (tracker.Flags == mirroredFlags)
                    continue;

                tracker.Flags = mirroredFlags;
                trackers[i] = tracker;
            }
        }

        private static void MirrorTrackerDtoFlagsIntoBytes(
            NativeArray<AssetTrackerDTO> trackers,
            NativeArray<byte> trackerFlags)
        {
            if (!trackers.IsCreated || !trackerFlags.IsCreated)
                return;

            int length = math.min(trackers.Length, trackerFlags.Length);
            for (int i = 0; i < length; i++)
            {
                byte flags = (byte)(trackers[i].Flags & 0xFFu);
                if (trackerFlags[i] == flags)
                    continue;

                trackerFlags[i] = flags;
            }
        }

        private static void MirrorHandleMapTtlIntoSlots(
            NativeArray<float> ttl,
            NativeArray<AssetHandleMapEntryDTO> handleMap)
        {
            if (!ttl.IsCreated || !handleMap.IsCreated)
                return;

            for (int i = 0; i < handleMap.Length; i++)
            {
                AssetHandleMapEntryDTO entry = handleMap[i];
                if ((entry.Flags & AssetHandleMapFlags.Occupied) == 0u)
                    continue;

                int slot = entry.PoolSlotIndex;
                if ((uint)slot >= (uint)ttl.Length)
                    continue;

                float timeToLive = entry.TimeToLive;
                ttl[slot] = math.isfinite(timeToLive) ? timeToLive : 0f;
            }
        }

        private static void SetTrackerFlagByte(NativeArray<AssetTrackerDTO> trackers, int slot, byte flags)
        {
            if (!trackers.IsCreated || (uint)slot >= (uint)trackers.Length)
                return;

            AssetTrackerDTO tracker = trackers[slot];
            tracker.Flags = (tracker.Flags & 0xFFFFFF00u) | flags;
            trackers[slot] = tracker;
        }

        private bool ArmNativeTtlRelease(uint assetHash, int slot)
        {
            if (!TryResolveTrackerViews(
                    out NativeArray<AssetTrackerDTO> trackers,
                    out NativeArray<float> ttl,
                    out NativeArray<byte> trackerFlags,
                    out NativeArray<AssetHandleMapEntryDTO> handleMap) ||
                (uint)slot >= (uint)trackers.Length)
            {
                return false;
            }

            byte flags = trackerFlags[slot];
            if ((flags & AssetHandleFlags.Active) == 0)
                return false;

            float adjustedTtl = ResolveAdaptiveTtlSeconds(assetHash, flags);
            ttl[slot] = adjustedTtl;
            if (TryFindHandleMapIndex(assetHash, handleMap, out int mapIndex))
            {
                ref AssetHandleMapEntryDTO entry = ref GetEntryAsRef(handleMap, mapIndex);
                entry.TimeToLive = adjustedTtl;
            }

            trackerFlags[slot] = (byte)((flags | AssetHandleFlags.PendingTtl) & ~AssetHandleFlags.Releasable);
            SetTrackerFlagByte(trackers, slot, trackerFlags[slot]);
            return true;
        }

        private void EvaluateAddressableTtlAndQueueReleases()
        {
            OnEvaluateAddressableTtlAndQueueReleases?.Invoke();
            if (!TryResolveTrackerViews(
                    out NativeArray<AssetTrackerDTO> trackers,
                    out NativeArray<float> ttl,
                    out NativeArray<byte> trackerFlags,
                    out NativeArray<AssetHandleMapEntryDTO> handleMap))
            {
                return;
            }

            bool vramPanic = IsVramPanicReleaseFrame();
            if (_ttlEvaluationResultsPending)
            {
                _ttlEvaluationResultsPending = false;
                bool scheduledUnderVramPanic = _ttlEvaluationVramPanic;
                _ttlEvaluationVramPanic = false;
                if (!_ttlEvaluationFlagsMirrored)
                    MirrorTrackerDtoFlagsIntoBytes(trackers, trackerFlags);
                MirrorHandleMapTtlIntoSlots(ttl, handleMap);
                _ttlEvaluationFlagsMirrored = false;
                SyncNativeRefCountsFromRegistry(trackers, ttl, trackerFlags, handleMap);
                DrainTtlEvaluationResults(trackers, ttl, trackerFlags, handleMap, vramPanic || scheduledUnderVramPanic);
            }
            else if (_nativeRefSyncRequired)
            {
                SyncNativeRefCountsFromRegistry(trackers, ttl, trackerFlags, handleMap);
            }

            if (vramPanic)
            {
                ForceFurthestUnusedAddressableTtlsToZero(
                    trackers,
                    ttl,
                    trackerFlags,
                    handleMap,
                    ResolvePlayerAupForEviction());
                DrainTtlEvaluationResults(trackers, ttl, trackerFlags, handleMap, true);
            }
        }

        private void SyncNativeRefCountsFromRegistry(
            NativeArray<AssetTrackerDTO> trackers,
            NativeArray<float> ttl,
            NativeArray<byte> trackerFlags,
            NativeArray<AssetHandleMapEntryDTO> handleMap)
        {
            if (!_nativeRefSyncRequired || !trackers.IsCreated)
                return;

            _nativeRefSyncRequired = false;
            for (int i = 0; i < trackers.Length; i++)
            {
                byte flags = trackerFlags[i];
                if ((flags & AssetHandleFlags.Active) == 0)
                    continue;

                AssetTrackerDTO tracker = trackers[i];
                uint assetHash = tracker.AssetHash;
                if (assetHash == 0u)
                    continue;

                if (!_assetRecords.TryGetValue(assetHash, out AssetRecord record))
                {
#if UNITY_ADDRESSABLES_EXIST
                    if (!TryReleaseManagedAddressableSlotForOrphan(assetHash, i))
                    {
                        _nativeRefSyncRequired = true;
                        continue;
                    }
#endif
                    uint bundlePrefixHash = ResolveBundlePrefixHashForSlot(i);
                    RemoveHandleMapEntry(handleMap, assetHash);
                    RecomputeBundlePrefixSharing(handleMap, trackerFlags, trackers, bundlePrefixHash);
                    trackers[i] = default;
                    ttl[i] = 0f;
                    trackerFlags[i] = 0;
                    continue;
                }

                int refCount = math.max(0, record.RefCount);

                tracker.ReferenceCount = refCount;
                trackers[i] = tracker;
                UpdateHandleMapRefCount(handleMap, assetHash, i, refCount);

                if (refCount > 0)
                {
                    ClearNativeReleaseIntent(assetHash, i, trackers, ttl, trackerFlags, handleMap);
                    continue;
                }

                if (record.PendingRelease)
                {
                    ClearNativeReleaseIntent(assetHash, i, trackers, ttl, trackerFlags, handleMap);
                    continue;
                }

                if ((flags & AssetHandleFlags.Pinned) != 0 ||
                    (flags & AssetHandleFlags.PendingTtl) != 0)
                {
                    continue;
                }

                float adjustedTtl = ResolveAdaptiveTtlSeconds(assetHash, flags);
                ttl[i] = adjustedTtl;
                if (TryFindHandleMapIndex(assetHash, handleMap, out int mapIndex))
                {
                    ref AssetHandleMapEntryDTO entry = ref GetEntryAsRef(handleMap, mapIndex);
                    entry.TimeToLive = adjustedTtl;
                }

                trackerFlags[i] = (byte)((flags | AssetHandleFlags.PendingTtl) & ~AssetHandleFlags.Releasable);
                SetTrackerFlagByte(trackers, i, trackerFlags[i]);
            }
        }

        private void ScheduleAddressableTtlEvaluation()
        {
            if (!TryResolveTrackerViews(
                    out NativeArray<AssetTrackerDTO> trackers,
                    out NativeArray<float> ttl,
                    out NativeArray<byte> trackerFlags,
                    out NativeArray<AssetHandleMapEntryDTO> handleMap))
            {
                return;
            }

            MirrorTrackerFlagBytesIntoDto(trackers, trackerFlags);
            bool vramPanic = IsVramPanicReleaseFrame();
            AssetTtlEvaluationJob job = new AssetTtlEvaluationJob
            {
                Trackers = trackers,
                HandleMap = handleMap,
                PlayerAup = ResolvePlayerAupForEviction(),
                MaxResidencyRadiusSq = DistantChunkReleaseDistanceMeters * DistantChunkReleaseDistanceMeters,
                DeltaSeconds = ColdReleaseIntervalSeconds,
                QualityTtlDecayMultiplier = ResolveQualityTtlDecayMultiplier(ResolveGlobalQualityWeight()),
                ForceVramPanic = vramPanic ? (byte)1 : (byte)0
            };
            for (int i = 0; i < handleMap.Length; i++)
                job.Execute(i);

            MirrorTrackerDtoFlagsIntoBytes(trackers, trackerFlags);
            MirrorHandleMapTtlIntoSlots(ttl, handleMap);
            _ttlEvaluationResultsPending = true;
            _ttlEvaluationVramPanic = vramPanic;
            _ttlEvaluationFlagsMirrored = true;
        }

        private void DrainTtlEvaluationResults(
            NativeArray<AssetTrackerDTO> trackers,
            NativeArray<float> ttl,
            NativeArray<byte> trackerFlags,
            NativeArray<AssetHandleMapEntryDTO> handleMap,
            bool vramPanic)
        {
            bool releaseWindow = vramPanic || IsBlindReleaseFrame();
            int pending = 0;
            int forced = 0;
            for (int i = 0; i < trackers.Length; i++)
            {
                byte flags = trackerFlags[i];
                if ((flags & AssetHandleFlags.Active) == 0 ||
                    (flags & AssetHandleFlags.PendingTtl) == 0)
                {
                    continue;
                }

                pending++;
                if ((flags & AssetHandleFlags.Releasable) == 0 || !releaseWindow)
                    continue;

                AssetTrackerDTO tracker = trackers[i];
                if (tracker.AssetHash == 0u || tracker.ReferenceCount > 0)
                    continue;

                if (QueueExpiredAddressableRelease(tracker.AssetHash, i, trackers, ttl, trackerFlags, handleMap))
                {
                    if (vramPanic)
                        forced++;
                }
            }

            _lastPendingTtlCount = pending;
            if (forced > 0)
                _forcedVramReleaseCount += forced;
        }

        private static int ForceFurthestUnusedAddressableTtlsToZero(
            NativeArray<AssetTrackerDTO> trackers,
            NativeArray<float> ttl,
            NativeArray<byte> trackerFlags,
            NativeArray<AssetHandleMapEntryDTO> handleMap,
            double3 playerAup)
        {
            if (!trackers.IsCreated)
                return 0;

            int candidateCount = 0;
            for (int i = 0; i < trackers.Length; i++)
            {
                byte flags = trackerFlags[i];
                if ((flags & AssetHandleFlags.Active) == 0 ||
                    (flags & AssetHandleFlags.Pinned) != 0 ||
                    (flags & AssetHandleFlags.Releasable) != 0)
                {
                    continue;
                }

                AssetTrackerDTO tracker = trackers[i];
                if (tracker.ReferenceCount == 0 && AssetTrackerAtomic.IsRefCountZero(trackers, i))
                    candidateCount++;
            }

            if (candidateCount <= 0)
                return 0;

            int targetCount = math.max(1, (candidateCount + 9) / 10);
            int marked = 0;
            for (int selection = 0; selection < targetCount; selection++)
            {
                int bestIndex = -1;
                float bestDistanceSq = -1f;
                for (int i = 0; i < trackers.Length; i++)
                {
                    byte flags = trackerFlags[i];
                    if ((flags & AssetHandleFlags.Active) == 0 ||
                        (flags & AssetHandleFlags.Pinned) != 0 ||
                        (flags & AssetHandleFlags.Releasable) != 0)
                    {
                        continue;
                    }

                    AssetTrackerDTO tracker = trackers[i];
                    if (tracker.ReferenceCount != 0 || !AssetTrackerAtomic.IsRefCountZero(trackers, i))
                        continue;

                    float distanceSq = CalculateLocalizedDistanceSq(in tracker, playerAup);
                    if (distanceSq <= bestDistanceSq)
                        continue;

                    bestDistanceSq = distanceSq;
                    bestIndex = i;
                }

                if (bestIndex < 0)
                    break;

                ttl[bestIndex] = 0f;
                uint assetHash = trackers[bestIndex].AssetHash;
                if (TryFindHandleMapIndex(assetHash, handleMap, out int mapIndex))
                {
                    ref AssetHandleMapEntryDTO entry = ref GetEntryAsRef(handleMap, mapIndex);
                    entry.TimeToLive = 0f;
                }

                trackerFlags[bestIndex] = (byte)(trackerFlags[bestIndex] | AssetHandleFlags.PendingTtl | AssetHandleFlags.Releasable);
                SetTrackerFlagByte(trackers, bestIndex, trackerFlags[bestIndex]);
                marked++;
            }

            return marked;
        }

        private static float CalculateLocalizedDistanceSq(in AssetTrackerDTO tracker, double3 playerAup)
        {
            if ((tracker.Flags & AssetTrackerMetaFlags.UnknownAup) != 0u)
                return 0f;

            double3 delta = AssetTrackerAupMath.ToAbsoluteDouble3(in tracker) - playerAup;
            if (!math.all(math.isfinite(delta)))
                return float.MaxValue;

            float3 localDelta = new float3((float)delta.x, (float)delta.y, (float)delta.z);
            float distanceSq = math.lengthsq(localDelta);
            return math.isfinite(distanceSq) ? distanceSq : float.MaxValue;
        }

        private static bool TryWriteAssetAup(ref AssetTrackerDTO tracker, double3 assetAup)
        {
            if (!math.all(math.isfinite(assetAup)))
                return false;

            const double sectorSize = HectonPhysicsContract.AupSectorSizeMetersDouble;
            double invSectorSize = 1.0d / sectorSize;
            long sectorX = (long)math.floor(assetAup.x * invSectorSize);
            long sectorY = (long)math.floor(assetAup.y * invSectorSize);
            long sectorZ = (long)math.floor(assetAup.z * invSectorSize);
            double localX = assetAup.x - (sectorX * sectorSize);
            double localY = assetAup.y - (sectorY * sectorSize);
            double localZ = assetAup.z - (sectorZ * sectorSize);
            double3 local = new double3(localX, localY, localZ);
            if (!math.all(math.isfinite(local)))
                return false;

            tracker.AssetSectorX = sectorX;
            tracker.AssetSectorY = sectorY;
            tracker.AssetSectorZ = sectorZ;
            tracker.AssetLocalX = (float)localX;
            tracker.AssetLocalY = (float)localY;
            tracker.AssetLocalZ = (float)localZ;
            if (!math.all(math.isfinite(new float3(tracker.AssetLocalX, tracker.AssetLocalY, tracker.AssetLocalZ))))
                return false;

            tracker.Flags &= ~AssetTrackerMetaFlags.UnknownAup;
            tracker.AupShiftGeneration++;
            return true;
        }

        private static void ClearNativeReleaseIntent(
            uint assetHash,
            int slot,
            NativeArray<AssetTrackerDTO> trackers,
            NativeArray<float> ttl,
            NativeArray<byte> trackerFlags,
            NativeArray<AssetHandleMapEntryDTO> handleMap)
        {
            if (trackerFlags.IsCreated && (uint)slot < (uint)trackerFlags.Length)
            {
                byte clearedFlags = (byte)(trackerFlags[slot] & ~(AssetHandleFlags.PendingTtl | AssetHandleFlags.Releasable));
                trackerFlags[slot] = clearedFlags;
                SetTrackerFlagByte(trackers, slot, clearedFlags);
            }

            if (ttl.IsCreated && (uint)slot < (uint)ttl.Length)
                ttl[slot] = 0f;

            ClearHandleMapTtl(handleMap, assetHash);
        }

        private bool QueueExpiredAddressableRelease(
            uint assetHash,
            int slot,
            NativeArray<AssetTrackerDTO> trackers,
            NativeArray<float> ttl,
            NativeArray<byte> trackerFlags,
            NativeArray<AssetHandleMapEntryDTO> handleMap)
        {
            if (!_assetRecords.TryGetValue(assetHash, out AssetRecord record))
                return false;

            if (record.RefCount > 0 || record.PendingRelease)
                return false;

            if (!EnqueuePendingRelease(assetHash))
            {
                record.PendingRelease = false;
                _assetRecords.Set(assetHash, record);
                return false;
            }

            record.PendingRelease = true;
            _assetRecords.Set(assetHash, record);
            ClearNativeReleaseIntent(assetHash, slot, trackers, ttl, trackerFlags, handleMap);
            return true;
        }

        private float ResolveAdaptiveTtlSeconds(uint assetHash, byte flags)
        {
            float quality = math.saturate(ResolveGlobalQualityWeight());
            float highTtl = math.clamp(baseAddressableTtlSeconds, MinimumAdaptiveTtlSeconds, DefaultHighEndTtlSeconds);
            float ttl = highTtl;

            if (TryFindCacheProfile(assetHash, out AssetCacheProfileDTO profile))
            {
                if (math.isfinite(profile.BaseTtlSeconds) && profile.BaseTtlSeconds > 0f)
                    ttl = profile.BaseTtlSeconds;
                if (math.isfinite(profile.BundleTtlMultiplier) && profile.BundleTtlMultiplier > 0f)
                    ttl *= profile.BundleTtlMultiplier;
            }

            if ((flags & AssetHandleFlags.BundleShared) != 0)
                ttl *= SharedBundleTtlMultiplier;

            ttl *= ResolveQualityTtlScale(quality);
            return math.clamp(ttl, 0f, DefaultHighEndTtlSeconds * 4f);
        }

        private static float ResolveQualityTtlScale(float quality)
        {
            return math.lerp(0.1f, 3.0f, math.smoothstep(0.2f, 0.8f, math.saturate(quality)));
        }

        private static float ResolveQualityTtlDecayMultiplier(float quality)
        {
            return 1f / math.max(0.1f, ResolveQualityTtlScale(quality));
        }

        private bool TryFindCacheProfile(uint assetHash, out AssetCacheProfileDTO profile)
        {
            profile = default;
            if (assetHash == 0u || !TryResolveCacheProfileView(out NativeArray<AssetCacheProfileDTO> profiles))
                return false;

            for (int i = 0; i < profiles.Length; i++)
            {
                AssetCacheProfileDTO candidate = profiles[i];
                if (candidate.AssetHash != assetHash)
                    continue;

                profile = candidate;
                return true;
            }

            return false;
        }

        private static float ResolveGlobalQualityWeight()
        {
            if (MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config))
                return MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, 1f);

            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(quality) ? quality : 1f);
        }

        private float ResolveVramPressureFactor()
        {
            IVramPressureReadModel pressure = _cachedVramPressure;
            if (pressure == null)
                return 0f;

            float value = pressure.VramPressureFactor;
            return math.saturate(math.isfinite(value) ? value : 0f);
        }

        private double3 ResolvePlayerAupForEviction()
        {
            IPlayerRuntimeContext player = _cachedPlayer;
            if (player != null && player.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
            {
                double3 aup = ToAbsoluteDouble3(in snapshot);
                if (math.all(math.isfinite(aup)))
                    return aup;
            }

            return double3.zero;
        }

        private static double3 ToAbsoluteDouble3(in PlayerRuntimePoseSnapshot snapshot)
        {
            double cellSize = HectonPhysicsContract.AupSectorSizeMetersDouble;
            return new double3(
                (snapshot.Aup.GridX * cellSize) + snapshot.Aup.LocalX,
                (snapshot.Aup.GridY * cellSize) + snapshot.Aup.LocalY,
                (snapshot.Aup.GridZ * cellSize) + snapshot.Aup.LocalZ);
        }

        private bool IsVramPanicReleaseFrame()
        {
            if (_externalVramPanicActive)
            {
                if (_externalVramPanicUntil <= 0f || RuntimeNowSeconds() <= _externalVramPanicUntil)
                    return true;

                _externalVramPanicActive = false;
                _externalVramPanicUntil = 0f;
            }

            return ResolveVramPressureFactor() >= vramPanicThreshold;
        }

        private bool IsBlindReleaseFrame()
        {
            if (_hardReaperAsyncWindowActive)
                return true;

            if (_explicitBlindFrameWindowActive)
            {
                if (_explicitBlindFrameWindowUntil <= 0f || RuntimeNowSeconds() <= _explicitBlindFrameWindowUntil)
                    return true;

                _explicitBlindFrameWindowActive = false;
                _explicitBlindFrameWindowUntil = 0f;
            }

            if (_mockScreenFadeToBlackActive)
            {
                if (_mockScreenFadeToBlackUntil <= 0f || RuntimeNowSeconds() <= _mockScreenFadeToBlackUntil)
                    return true;

                _mockScreenFadeToBlackActive = false;
                _mockScreenFadeToBlackUntil = 0f;
            }

            SystemDispatcher dispatcher = _cachedDispatcher;
            return dispatcher != null && dispatcher.TimeSnapshot.UnscaledDeltaTime <= 0.0001d;
        }

        private bool ClearNativeHandleSlot(uint assetHash)
        {

            if (!TryResolveTrackerViews(
                    out NativeArray<AssetTrackerDTO> trackers,
                    out NativeArray<float> ttl,
                    out NativeArray<byte> trackerFlags,
                    out NativeArray<AssetHandleMapEntryDTO> handleMap) ||
                !TryGetHandleSlot(assetHash, handleMap, out int slot) ||
                (uint)slot >= (uint)trackers.Length)
            {
                return false;
            }

#if UNITY_ADDRESSABLES_EXIST
            if (_addressableHandlePool != null && (uint)slot < (uint)_addressableHandlePool.Length)
                _addressableHandlePool[slot] = default;
            if (_addressableHandleHashes != null && (uint)slot < (uint)_addressableHandleHashes.Length)
                _addressableHandleHashes[slot] = 0u;
            if (_addressableBundlePrefixHashes != null && (uint)slot < (uint)_addressableBundlePrefixHashes.Length)
            {
                uint bundlePrefixHash = _addressableBundlePrefixHashes[slot];
                _addressableBundlePrefixHashes[slot] = 0u;
                RemoveHandleMapEntry(handleMap, assetHash);
                RecomputeBundlePrefixSharing(handleMap, trackerFlags, trackers, bundlePrefixHash);
            }
            else
            {
                RemoveHandleMapEntry(handleMap, assetHash);
            }
#endif
            trackers[slot] = default;
            ttl[slot] = 0f;
            trackerFlags[slot] = 0;
            return true;
        }

        private static uint ComputeBundlePrefixHash(string address)
        {
            if (string.IsNullOrEmpty(address))
                return 0u;

            int end = address.Length;
            for (int i = address.Length - 1; i >= 0; i--)
            {
                char c = address[i];
                if (c == '/' || c == '\\' || c == '_')
                {
                    end = i;
                    break;
                }
            }

            if (end <= 0)
                end = address.Length;

            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < end; i++)
                {
                    hash ^= address[i];
                    hash *= 16777619u;
                }

                return hash != 0u ? hash : 1u;
            }
        }

        private void GenerateEmergencyMockCacheProfiles()
        {
            if (!TryResolveCacheProfileView(out NativeArray<AssetCacheProfileDTO> profiles) || profiles.Length < 4)
                return;

            profiles[0] = new AssetCacheProfileDTO { AssetHash = 0xA55E7001u, BaseTtlSeconds = 10f, BundleTtlMultiplier = 1f, Flags = 0u };
            profiles[1] = new AssetCacheProfileDTO { AssetHash = 0xA55E7002u, BaseTtlSeconds = 60f, BundleTtlMultiplier = 1.5f, Flags = 0u };
            profiles[2] = new AssetCacheProfileDTO { AssetHash = 0xA55E7003u, BaseTtlSeconds = 180f, BundleTtlMultiplier = 2f, Flags = 0u };
            profiles[3] = new AssetCacheProfileDTO { AssetHash = 0xA55E7004u, BaseTtlSeconds = 300f, BundleTtlMultiplier = 2f, Flags = 1u };
        }

        private bool TryResolveHeapSanitizerVaultBuffers(int handleCapacity = -1, int mapCapacity = -1)
        {
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (handleCapacity <= 0)
                handleCapacity = Mathf.Clamp(maxTrackedAddressableHandles, 1, MaxTrackedAddressableCapacity);
            if (mapCapacity <= 0)
                mapCapacity = ResolveHandleMapCapacity(handleCapacity);

            return EnsureHeapSanitizerVaultBuffer(
                       ref _assetTrackerVaultHandle,
                       BufferID.AddressableHeapTrackers,
                       handleCapacity,
                       NativeArrayOptions.UninitializedMemory,
                       out NativeArray<AssetTrackerDTO> trackers) &&
                   EnsureHeapSanitizerVaultBuffer(
                       ref _assetTtlVaultHandle,
                       BufferID.AddressableHeapTimeToLive,
                       handleCapacity,
                       NativeArrayOptions.UninitializedMemory,
                       out NativeArray<float> ttl) &&
                   EnsureHeapSanitizerVaultBuffer(
                       ref _assetTrackerFlagsVaultHandle,
                       BufferID.AddressableHeapTrackerFlags,
                       handleCapacity,
                       NativeArrayOptions.UninitializedMemory,
                       out NativeArray<byte> trackerFlags) &&
                   EnsureHeapSanitizerVaultBuffer(
                       ref _assetHandleMapVaultHandle,
                       BufferID.AddressableHeapHandleMap,
                       mapCapacity,
                       NativeArrayOptions.UninitializedMemory,
                       out NativeArray<AssetHandleMapEntryDTO> handleMap) &&
                   EnsureHeapSanitizerVaultBuffer(
                       ref _cacheProfileVaultHandle,
                       BufferID.AddressableHeapCacheProfiles,
                       CacheProfileCapacity,
                       NativeArrayOptions.UninitializedMemory,
                       out NativeArray<AssetCacheProfileDTO> profiles) &&
                   EnsureHeapSanitizerVaultBuffer(
                       ref _cacheProfileCsvScratchVaultHandle,
                       BufferID.AddressableHeapCsvScratch,
                       CacheProfileCsvScratchBytes,
                       NativeArrayOptions.UninitializedMemory,
                       out NativeArray<byte> csvScratch) &&
                   EnsureHeapSanitizerVaultBuffer(
                       ref _heapTelemetryVaultHandle,
                       BufferID.AddressableHeapTelemetry,
                       HeapTelemetryCapacity,
                       NativeArrayOptions.UninitializedMemory,
                       out NativeArray<AssetHeapTelemetryEntry> telemetry);
        }

        private bool EnsureHeapSanitizerVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive || requiredLength <= 0)
                return false;

            if (TryResolveHeapSanitizerVaultBuffer(ref handle, bufferId, requiredLength, out buffer))
                return true;

            handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, VaultOwnerSystem, options);
            return TryResolveHeapSanitizerVaultBuffer(ref handle, bufferId, requiredLength, out buffer);
        }

        private bool TryResolveHeapSanitizerVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive || requiredLength <= 0)
                return false;

            if (IsHeapSanitizerVaultHandle(in handle, bufferId) &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            if (!vault.TryGetGenerationHandle<T>(bufferId, out handle) ||
                !IsHeapSanitizerVaultHandle(in handle, bufferId) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                handle = default;
                buffer = default;
                return false;
            }

            return true;
        }

        private bool TryResolveExistingHeapSanitizerVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   requiredLength > 0 &&
                   IsHeapSanitizerVaultHandle(in handle, bufferId) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool HasHeapSanitizerVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength) where T : struct
        {
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   requiredLength > 0 &&
                   IsHeapSanitizerVaultHandle(in handle, bufferId) &&
                   vault.TryReadHandle(in handle, out NativeArray<T> buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool IsHeapSanitizerVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)VaultOwnerSystem &&
                   handle.Generation != 0u;
        }

        private void ReleaseHeapSanitizerVaultHandles(IDataVault vault)
        {
            ReleaseHeapSanitizerVaultHandle(vault, ref _assetTrackerVaultHandle);
            ReleaseHeapSanitizerVaultHandle(vault, ref _assetTtlVaultHandle);
            ReleaseHeapSanitizerVaultHandle(vault, ref _assetTrackerFlagsVaultHandle);
            ReleaseHeapSanitizerVaultHandle(vault, ref _assetHandleMapVaultHandle);
            ReleaseHeapSanitizerVaultHandle(vault, ref _cacheProfileVaultHandle);
            ReleaseHeapSanitizerVaultHandle(vault, ref _cacheProfileCsvScratchVaultHandle);
            ReleaseHeapSanitizerVaultHandle(vault, ref _heapTelemetryVaultHandle);
        }

        private static void ReleaseHeapSanitizerVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (vault != null && H8Memory.IsInitialized && handle.BufferID != 0u && handle.Generation != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private bool TryCopyCacheProfilesFromVault()
        {
            if (!TryResolveCacheProfileView(out NativeArray<AssetCacheProfileDTO> profiles))
            {
                return false;
            }

            bool hasAny = false;
            for (int i = 0; i < profiles.Length; i++)
            {
                AssetCacheProfileDTO profile = profiles[i];
                if (profile.AssetHash != 0u)
                    hasAny = true;
            }

            return hasAny;
        }

        private void MirrorCacheProfilesToVault()
        {
            TryResolveCacheProfileView(out _);
        }

        private void EnsureFallbackImpostorMesh()
        {
            _fallbackImpostorMesh = authoredFallbackImpostorMesh;
        }

        public bool TryGetFallbackImpostorMesh(out Mesh mesh)
        {
            EnsureFallbackImpostorMesh();
            mesh = _fallbackImpostorMesh;
            return mesh != null;
        }

        public int GetHeapSanitizerActiveHandleCount()
        {
            return CountActiveAddressableHandles();
        }

        public int GetHeapSanitizerOrphanedReleaseCount()
        {
            return _orphanedHandlesReleased;
        }

        public int GetHeapSanitizerCacheHitCount()
        {
            return _cacheHitCount;
        }

        public int GetHeapSanitizerCacheMissCount()
        {
            return _cacheMissCount;
        }

        public uint GetHeapSanitizerLastLeakSuspectHash()
        {
            return _lastLeakSuspectHash;
        }

        public float GetHeapSanitizerBaseTtlSeconds()
        {
            return baseAddressableTtlSeconds;
        }

        public void SetHeapSanitizerBaseTtlSeconds(float value)
        {
            baseAddressableTtlSeconds = math.clamp(value, MinimumAdaptiveTtlSeconds, DefaultHighEndTtlSeconds);
        }

        public float GetHeapSanitizerVramPanicThreshold()
        {
            return vramPanicThreshold;
        }

        public void SetHeapSanitizerVramPanicThreshold(float value)
        {
            vramPanicThreshold = math.clamp(value, 0.5f, 0.99f);
        }

        public bool TryGetHeapSanitizerTrackerAt(int ordinal, out AssetTrackerDTO tracker, out float ttlSeconds, out byte flags)
        {
            tracker = default;
            ttlSeconds = 0f;
            flags = 0;

            if (ordinal < 0 ||
                !TryResolveTrackerViews(
                    out NativeArray<AssetTrackerDTO> trackers,
                    out NativeArray<float> ttl,
                    out NativeArray<byte> trackerFlags,
                    out _))
            {
                return false;
            }

            int seen = 0;
            for (int i = 0; i < trackers.Length; i++)
            {
                byte candidateFlags = trackerFlags[i];
                if ((candidateFlags & AssetHandleFlags.Active) == 0)
                    continue;

                if (seen == ordinal)
                {
                    tracker = trackers[i];
                    ttlSeconds = ttl[i];
                    flags = candidateFlags;
                    return true;
                }

                seen++;
            }

            return false;
        }

        public bool TryGetHeapSanitizerTelemetryAt(
            int ordinalFromNewest,
            out uint activeHandles,
            out uint cacheHits,
            out uint cacheMisses,
            out float vramPressure,
            out uint flags)
        {
            activeHandles = 0u;
            cacheHits = 0u;
            cacheMisses = 0u;
            vramPressure = 0f;
            flags = 0u;
            if (ordinalFromNewest < 0 || !TryResolveTelemetryView(out NativeArray<AssetHeapTelemetryEntry> telemetry))
                return false;

            int length = telemetry.Length;
            if (length <= 0 || ordinalFromNewest >= length)
                return false;

            int index = _heapTelemetryCursor - 1 - ordinalFromNewest;
            while (index < 0)
                index += length;

            AssetHeapTelemetryEntry entry = telemetry[index % length];
            activeHandles = entry.ActiveHandles;
            cacheHits = entry.CacheHits;
            cacheMisses = entry.CacheMisses;
            vramPressure = entry.VramPressure;
            flags = entry.Flags;
            return entry.FrameIndex != 0u || entry.ResultHash != 0u;
        }

        public bool TryGetHeapSanitizerLeakSuspectAt(
            int ordinal,
            out uint assetHash,
            out ulong bundlePrefixHash,
            out int refCount)
        {
            assetHash = 0u;
            bundlePrefixHash = 0UL;
            refCount = 0;

            if (ordinal < 0 ||
                !TryResolveTrackerViews(
                    out NativeArray<AssetTrackerDTO> trackers,
                    out _,
                    out NativeArray<byte> trackerFlags,
                    out NativeArray<AssetHandleMapEntryDTO> handleMap))
            {
                return false;
            }

            int seen = 0;
            for (int i = 0; i < handleMap.Length; i++)
            {
                AssetHandleMapEntryDTO entry = handleMap[i];
                if ((entry.Flags & AssetHandleMapFlags.Occupied) == 0u)
                    continue;

                int slot = entry.PoolSlotIndex;
                if ((uint)slot >= (uint)trackers.Length)
                    continue;

                byte trackerFlag = trackerFlags[slot];
                if ((trackerFlag & AssetHandleFlags.Active) == 0)
                    continue;

                AssetTrackerDTO tracker = trackers[slot];
                if (tracker.ReferenceCount <= LeakRefCountThreshold)
                    continue;

                if (seen == ordinal)
                {
                    assetHash = tracker.AssetHash;
                    bundlePrefixHash = entry.BundlePrefixHash;
                    refCount = tracker.ReferenceCount;
                    return true;
                }

                seen++;
            }

            return false;
        }

        public bool SetHeapSanitizerPin(uint assetHash, bool pinned)
        {

            if (assetHash == 0u ||
                !TryResolveTrackerViews(
                    out NativeArray<AssetTrackerDTO> trackers,
                    out NativeArray<float> ttl,
                    out NativeArray<byte> trackerFlags,
                    out NativeArray<AssetHandleMapEntryDTO> handleMap) ||
                !TryGetHandleSlot(assetHash, handleMap, out int slot) ||
                (uint)slot >= (uint)trackerFlags.Length)
            {
                return false;
            }

            byte flags = trackerFlags[slot];
            byte nextFlags = pinned
                ? (byte)((flags | AssetHandleFlags.Pinned) & ~(AssetHandleFlags.PendingTtl | AssetHandleFlags.Releasable))
                : (byte)(flags & ~AssetHandleFlags.Pinned);
            trackerFlags[slot] = nextFlags;
            SetTrackerFlagByte(trackers, slot, nextFlags);

            if (pinned)
            {
                ClearNativeReleaseIntent(assetHash, slot, trackers, ttl, trackerFlags, handleMap);
                if (_assetRecords.TryGetValue(assetHash, out AssetRecord record) && record.PendingRelease)
                {
                    record.PendingRelease = false;
                    _assetRecords.Set(assetHash, record);
                }
            }

            _nativeRefSyncRequired = true;
            return true;
        }

#if UNITY_EDITOR
        public bool TryParseAssetCacheRulesCsv(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
                return false;

            EnsureNativeHandleStorage();
            if (!TryResolveCacheProfileCsvScratch(out NativeArray<byte> scratch))
                return false;

            using FileStream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (stream.Length <= 0L || stream.Length > scratch.Length)
                return false;

            unsafe
            {
                byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(scratch);
                int expectedBytes = (int)stream.Length;
                int totalRead = 0;
                while (totalRead < expectedBytes)
                {
                    Span<byte> target = new Span<byte>(basePtr + totalRead, expectedBytes - totalRead);
                    int read = stream.Read(target);
                    if (read <= 0)
                        break;

                    totalRead += read;
                }

                if (totalRead <= 0)
                    return false;

                return TryParseAssetCacheRules(new ReadOnlySpan<byte>(basePtr, totalRead));
            }
        }

        public bool TryParseAssetCacheRules(ReadOnlySpan<byte> csv)
        {
            if (!TryResolveCacheProfileView(out NativeArray<AssetCacheProfileDTO> profiles))
                return false;

            int profileIndex = 0;
            int cursor = 0;
            while (cursor < csv.Length && profileIndex < profiles.Length)
            {
                int lineStart = cursor;
                while (cursor < csv.Length && csv[cursor] != '\n' && csv[cursor] != '\r')
                    cursor++;

                ReadOnlySpan<byte> line = TrimAscii(csv.Slice(lineStart, cursor - lineStart));
                while (cursor < csv.Length && (csv[cursor] == '\n' || csv[cursor] == '\r'))
                    cursor++;

                if (line.Length == 0 || line[0] == (byte)'#')
                    continue;

                if (TryParseCacheProfileLine(line, out AssetCacheProfileDTO profile))
                    profiles[profileIndex++] = profile;
            }

            for (int i = profileIndex; i < profiles.Length; i++)
                profiles[i] = default;

            MirrorCacheProfilesToVault();
            return profileIndex > 0;
        }

        private static bool TryParseCacheProfileLine(ReadOnlySpan<byte> line, out AssetCacheProfileDTO profile)
        {
            profile = default;
            ReadOnlySpan<byte> key = NextCsvToken(ref line);
            ReadOnlySpan<byte> ttl = NextCsvToken(ref line);
            ReadOnlySpan<byte> multiplier = NextCsvToken(ref line);
            ReadOnlySpan<byte> flags = NextCsvToken(ref line);

            if (key.Length == 0 || !TryParseAssetHash(key, out uint assetHash))
                return false;

            if (!TryParseFloatAscii(ttl, out float ttlSeconds))
                ttlSeconds = DefaultHighEndTtlSeconds;

            if (!TryParseFloatAscii(multiplier, out float ttlMultiplier))
                ttlMultiplier = 1f;

            uint parsedFlags = 0u;
            if (flags.Length > 0)
                TryParseUIntAscii(flags, out parsedFlags);

            profile = new AssetCacheProfileDTO
            {
                AssetHash = assetHash,
                BaseTtlSeconds = math.clamp(ttlSeconds, 0f, DefaultHighEndTtlSeconds * 4f),
                BundleTtlMultiplier = math.clamp(ttlMultiplier, 0.1f, 8f),
                Flags = parsedFlags
            };
            return true;
        }

        private static ReadOnlySpan<byte> NextCsvToken(ref ReadOnlySpan<byte> line)
        {
            if (line.Length == 0)
                return ReadOnlySpan<byte>.Empty;

            int comma = line.IndexOf((byte)',');
            if (comma < 0)
            {
                ReadOnlySpan<byte> result = TrimAscii(line);
                line = ReadOnlySpan<byte>.Empty;
                return result;
            }

            ReadOnlySpan<byte> token = TrimAscii(line.Slice(0, comma));
            line = line.Slice(comma + 1);
            return token;
        }

        private static ReadOnlySpan<byte> TrimAscii(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && value[start] <= (byte)' ')
                start++;
            while (end >= start && value[end] <= (byte)' ')
                end--;

            return start > end ? ReadOnlySpan<byte>.Empty : value.Slice(start, end - start + 1);
        }

        private static bool TryParseAssetHash(ReadOnlySpan<byte> token, out uint assetHash)
        {
            assetHash = 0u;
            token = TrimAscii(token);
            if (token.Length == 0)
                return false;

            if (TryParseUIntAscii(token, out assetHash))
                return assetHash != 0u;

            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < token.Length; i++)
                {
                    hash ^= token[i];
                    hash *= 16777619u;
                }

                assetHash = hash != 0u ? hash : 1u;
                return true;
            }
        }

        private static bool TryParseUIntAscii(ReadOnlySpan<byte> token, out uint value)
        {
            value = 0u;
            token = TrimAscii(token);
            if (token.Length == 0)
                return false;

            int index = 0;
            int radix = 10;
            if (token.Length > 2 &&
                token[0] == (byte)'0' &&
                (token[1] == (byte)'x' || token[1] == (byte)'X'))
            {
                index = 2;
                radix = 16;
            }

            bool any = false;
            uint parsed = 0u;
            for (; index < token.Length; index++)
            {
                byte c = token[index];
                int digit;
                if (c >= (byte)'0' && c <= (byte)'9')
                    digit = c - (byte)'0';
                else if (radix == 16 && c >= (byte)'a' && c <= (byte)'f')
                    digit = c - (byte)'a' + 10;
                else if (radix == 16 && c >= (byte)'A' && c <= (byte)'F')
                    digit = c - (byte)'A' + 10;
                else
                    return false;

                if (digit >= radix)
                    return false;

                parsed = unchecked((parsed * (uint)radix) + (uint)digit);
                any = true;
            }

            value = parsed;
            return any;
        }

        private static bool TryParseFloatAscii(ReadOnlySpan<byte> token, out float value)
        {
            value = 0f;
            token = TrimAscii(token);
            if (token.Length == 0)
                return false;

            int index = 0;
            bool negative = false;
            if (token[index] == (byte)'-' || token[index] == (byte)'+')
            {
                negative = token[index] == (byte)'-';
                index++;
            }

            float parsed = 0f;
            bool any = false;
            while (index < token.Length && token[index] >= (byte)'0' && token[index] <= (byte)'9')
            {
                parsed = (parsed * 10f) + (token[index] - (byte)'0');
                index++;
                any = true;
            }

            if (index < token.Length && token[index] == (byte)'.')
            {
                index++;
                float place = 0.1f;
                while (index < token.Length && token[index] >= (byte)'0' && token[index] <= (byte)'9')
                {
                    parsed += (token[index] - (byte)'0') * place;
                    place *= 0.1f;
                    index++;
                    any = true;
                }
            }

            if (!any)
                return false;

            if (index < token.Length && (token[index] == (byte)'e' || token[index] == (byte)'E'))
            {
                index++;
                bool exponentNegative = false;
                if (index < token.Length && (token[index] == (byte)'-' || token[index] == (byte)'+'))
                {
                    exponentNegative = token[index] == (byte)'-';
                    index++;
                }

                int exponent = 0;
                bool hasExponent = false;
                while (index < token.Length && token[index] >= (byte)'0' && token[index] <= (byte)'9')
                {
                    exponent = math.min(38, (exponent * 10) + (token[index] - (byte)'0'));
                    index++;
                    hasExponent = true;
                }

                if (!hasExponent)
                    return false;

                float scale = 1f;
                for (int i = 0; i < exponent; i++)
                    scale *= 10f;

                parsed = exponentNegative ? parsed / scale : parsed * scale;
            }

            if (index != token.Length)
                return false;

            value = negative ? -parsed : parsed;
            return math.isfinite(value);
        }
#endif

        private int CountActiveAddressableHandles()
        {
            if (!TryResolveTrackerViews(
                    out _,
                    out _,
                    out NativeArray<byte> trackerFlags,
                    out _))
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < trackerFlags.Length; i++)
            {
                if ((trackerFlags[i] & AssetHandleFlags.Active) != 0)
                    count++;
            }

            return count;
        }

        private void WriteHeapTelemetrySample()
        {

            if (!TryResolveTelemetryView(out NativeArray<AssetHeapTelemetryEntry> telemetry) ||
                !TryResolveTrackerViews(
                    out NativeArray<AssetTrackerDTO> trackers,
                    out NativeArray<float> ttl,
                    out NativeArray<byte> trackerFlags,
                    out _))
            {
                return;
            }

            int active = 0;
            int leakCount = 0;
            int pending = 0;
            float longestTtl = 0f;
            uint leakHash = 0u;
            for (int i = 0; i < trackers.Length; i++)
            {
                byte flags = trackerFlags[i];
                if ((flags & AssetHandleFlags.Active) == 0)
                    continue;

                active++;
                if ((flags & AssetHandleFlags.PendingTtl) != 0)
                    pending++;

                float ttlSeconds = ttl[i];
                if (ttlSeconds > longestTtl && math.isfinite(ttlSeconds))
                    longestTtl = ttlSeconds;

                AssetTrackerDTO tracker = trackers[i];
                if (tracker.ReferenceCount > LeakRefCountThreshold)
                {
                    leakCount++;
                    leakHash = tracker.AssetHash;
                    trackerFlags[i] = (byte)(flags | AssetHandleFlags.LeakSuspect);
                }
            }

            _lastPendingTtlCount = pending;
            uint total = unchecked((uint)(_cacheHitCount + _cacheMissCount));
            float hitRatio = total > 0u ? _cacheHitCount / (float)total : 0f;
            uint telemetryFlags = 0u;
            if (ResolveVramPressureFactor() >= vramPanicThreshold)
                telemetryFlags |= HeapTelemetryVramPanicFlag;
            if (IsBlindReleaseFrame())
                telemetryFlags |= HeapTelemetryBlindReleaseFlag;
            if (leakCount > 0)
            {
                telemetryFlags |= HeapTelemetryLeakSuspectFlag;
                _lastLeakSuspectHash = leakHash;
            }

            AssetHeapTelemetryEntry entry = new AssetHeapTelemetryEntry
            {
                FrameIndex = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                ActiveHandles = unchecked((uint)active),
                OrphanedHandlesReleased = unchecked((uint)math.max(0, _orphanedHandlesReleased)),
                CacheHits = unchecked((uint)math.max(0, _cacheHitCount)),
                CacheMisses = unchecked((uint)math.max(0, _cacheMissCount)),
                PendingTtlReleases = unchecked((uint)math.max(0, _lastPendingTtlCount)),
                ForcedVramReleases = unchecked((uint)math.max(0, _forcedVramReleaseCount)),
                LeakSuspectHash = leakHash,
                CacheHitRatio = math.saturate(hitRatio),
                GlobalQualityWeight = ResolveGlobalQualityWeight(),
                VramPressure = ResolveVramPressureFactor(),
                LongestTtlSeconds = longestTtl,
                Flags = telemetryFlags,
                ResultHash = MixHeapTelemetryHash(active, pending, leakHash, telemetryFlags),
                Pad0 = 0u,
                Pad1 = 0u
            };

            int index = _heapTelemetryCursor % telemetry.Length;
            telemetry[index] = entry;

            _heapTelemetryCursor = (_heapTelemetryCursor + 1) % telemetry.Length;

            if (leakCount > 0)
                DumpHeapTelemetry();
        }

        private static uint MixHeapTelemetryHash(int active, int pending, uint leakHash, uint flags)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)active) * 16777619u;
                hash = (hash ^ (uint)pending) * 16777619u;
                hash = (hash ^ leakHash) * 16777619u;
                hash = (hash ^ flags) * 16777619u;
                return hash;
            }
        }

        private unsafe void DumpHeapTelemetry()
        {
            if (!TryResolveTelemetryView(out NativeArray<AssetHeapTelemetryEntry> telemetry))
                return;

            DumpHeapTelemetryToFile("Dump_MEMORY_SURGEON.bin", telemetry);
            DumpHeapTelemetryToFile("Dump_SHINOBU_101_Addressables.bin", telemetry);
        }

        private unsafe void DumpHeapTelemetryToFile(string fileName, NativeArray<AssetHeapTelemetryEntry> telemetry)
        {
            NativeArray<byte> payload = default;
            try
            {
                string root = Directory.GetCurrentDirectory();
                string path = Path.Combine(root, "Docs", "AgentLogs", fileName);
                const int headerBytes = 16;
                int entryBytes = UnsafeUtility.SizeOf<AssetHeapTelemetryEntry>();
                int telemetryBytes = entryBytes * telemetry.Length;
                int byteCount = headerBytes + telemetryBytes;
                payload = H8Memory.Allocate<byte>(
                    byteCount,
                    VaultOwnerSystem,
                    Allocator.Temp,
                    NativeArrayOptions.UninitializedMemory);
                if (!payload.IsCreated)
                {
                    return;
                }

                byte* bytes = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payload);
                WriteUInt64LittleEndian(bytes, 0, 0x484543544F4E3800UL);
                WriteUInt32LittleEndian(bytes, 8, HeapTelemetryCapacity);
                WriteUInt32LittleEndian(bytes, 12, unchecked((uint)entryBytes));

                if (telemetryBytes > 0)
                {
                    byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                    UnsafeUtility.MemCpy(bytes + headerBytes, ptr, telemetryBytes);
                }

                NativeFaultDumpWriter.TryWriteAll(path, payload, byteCount);
            }
            catch (Exception)
            {
                // Fault-path dump failure cannot throw into gameplay teardown.
            }
            finally
            {
                if (payload.IsCreated)
                    H8Memory.Release(ref payload, VaultOwnerSystem);
            }
        }

        private static unsafe void WriteUInt32LittleEndian(byte* data, int offset, uint value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
            data[offset + 2] = (byte)(value >> 16);
            data[offset + 3] = (byte)(value >> 24);
        }

        private static unsafe void WriteUInt64LittleEndian(byte* data, int offset, ulong value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
            data[offset + 2] = (byte)(value >> 16);
            data[offset + 3] = (byte)(value >> 24);
            data[offset + 4] = (byte)(value >> 32);
            data[offset + 5] = (byte)(value >> 40);
            data[offset + 6] = (byte)(value >> 48);
            data[offset + 7] = (byte)(value >> 56);
        }

        private void TryRegister()
        {
            if (_registeredTick)
                return;
            CacheDependencies();
            if (!Application.isPlaying || _cachedDispatcher == null)
                return;
            if (!ReferenceEquals(GlobalRegistry.AssetLifecycle, this))
                return;

            bool tickRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core);
            bool slowTickRegistered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Core);
            bool lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core);
            if (!tickRegistered || !slowTickRegistered || !lateFrameRegistered)
            {
                if (tickRegistered)
                    GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
                if (slowTickRegistered)
                    GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
                if (lateFrameRegistered)
                    GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);
                return;
            }

            _registeredTick = true;
            _registeredSlowTick = true;
            _registeredLateFrame = true;
        }

        private bool TryRegisterService()
        {
            if (_registeredService)
                return true;
            if (!Application.isPlaying)
                return false;

            if (TryAbortForUsableExistingRuntime())
                return false;

            GlobalRegistry.RegisterAssetLifecycleRuntime(this);
            _registeredService = ReferenceEquals(GlobalRegistry.AssetLifecycle, this);
            if (_registeredService)
                CacheDependencies();
            return _registeredService;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            if (!Application.isPlaying)
                return false;

            AssetLifecycleGovernor registered = GlobalRegistry.AssetLifecycle;
            if (ReferenceEquals(registered, null) || ReferenceEquals(registered, this))
                return false;

            if (IsAssetLifecycleRuntimeUsable(registered))
            {
                _runtimeOwnerAborted = true;
                Destroy(gameObject);
                return true;
            }

            GlobalRegistry.UnregisterAssetLifecycleRuntime(registered);
            return false;
        }

        private static bool IsAssetLifecycleRuntimeUsable(AssetLifecycleGovernor governor)
        {
            return governor != null &&
                   governor._registeredService &&
                   governor.isActiveAndEnabled;
        }

        private void TryUnregister()
        {
            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
                _registeredSlowTick = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);
                _registeredLateFrame = false;
                _pendingPresentationDisableCount = 0;
            }

            if (!_registeredTick)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            _registeredTick = false;
        }

        private void TryUnregisterService()
        {
            if (!_registeredService)
                return;

            GlobalRegistry.UnregisterAssetLifecycleRuntime(this);
            _registeredService = false;
        }

        private void TryRegisterHotSwap()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwap()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private void CacheDependencies()
        {
            IDataVault currentVault = GlobalRegistry.DataVault;
            if (!ReferenceEquals(_dataVault, currentVault))
            {
                IDataVault previousVault = _dataVault;
                if (previousVault != null)
                {
                    CompleteTtlEvaluationForTeardown();
                    ReleaseHeapSanitizerVaultHandles(previousVault);
                    InvalidateVaultHandleDescriptors();
                }

                _dataVault = currentVault;
            }

            if (_cachedDispatcher == null)
                _cachedDispatcher = GlobalRegistry.Dispatcher;
            if (_cachedAssetLoadDispatcher == null)
                _cachedAssetLoadDispatcher = GlobalRegistry.AssetLoadDispatcher;
            if (_cachedVramPressure == null)
                _cachedVramPressure = GlobalRegistry.VRAMPressureReadModel;
            if (_cachedPlayer == null)
                _cachedPlayer = GlobalRegistry.Player;
            if (_cachedPlayerInventory == null)
                _cachedPlayerInventory = GlobalRegistry.PlayerInventory;
            if (_cachedScannerInterferenceUi == null)
                _cachedScannerInterferenceUi = GlobalRegistry.UI as IScannerInterferenceUiSink;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    _cachedDispatcher = currentService as SystemDispatcher;
                    TryUnregister();
                    if (currentService != null && isActiveAndEnabled && !_runtimeOwnerAborted)
                        TryRegister();
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    IDataVault previousVault = previousService as IDataVault;
                    if (previousVault == null)
                        previousVault = _dataVault;
                    CompleteTtlEvaluationForTeardown();
                    ReleaseHeapSanitizerVaultHandles(previousVault);
                    _dataVault = currentService as IDataVault;
                    InvalidateVaultHandleDescriptors();
                    if (_dataVault != null)
                        EnsureNativeHandleStorage();
                    break;
                case GlobalRegistryServiceSlot.AssetLoadDispatcherRuntime:
                    _cachedAssetLoadDispatcher = currentService as AssetLoadDispatcher;
                    break;
                case GlobalRegistryServiceSlot.VRAMPressureRuntime:
                    _cachedVramPressure = currentService as IVramPressureReadModel;
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _cachedPlayer = currentService as IPlayerRuntimeContext;
                    break;
                case GlobalRegistryServiceSlot.PlayerInventory:
                    _cachedPlayerInventory = currentService as IPlayerInventoryService;
                    break;
                case GlobalRegistryServiceSlot.UI:
                    _cachedScannerInterferenceUi = currentService as IScannerInterferenceUiSink;
                    break;
            }
        }

        private void PumpRetries()
        {
            if (_assetRecords.Count == 0)
                return;

            float now = RuntimeNowSeconds();
            _retryCandidates.Clear();

            ManagedAssetRecordTable.Enumerator enumerator = _assetRecords.GetEnumerator();
            while (enumerator.MoveNext() && _retryCandidates.Count < MaxHardReaperEvictions)
            {
                AssetRecord record = enumerator.Current.Value;
                if (record.RefCount <= 0 || record.ActiveRequestId != 0 || record.NextRetryTime <= 0f)
                    continue;

                if (now < record.NextRetryTime)
                    continue;

                _retryCandidates.Add(record.Key);
            }

            for (int i = 0; i < _retryCandidates.Count; i++)
                QueueAsyncDispatch(_retryCandidates[i]);
        }

        private void ReportColdTickBudgetIfNeeded(long startTicks)
        {
            OnReportColdTickBudgetIfNeeded?.Invoke(startTicks);
            long elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            double elapsedMilliseconds = elapsedTicks * 1000d / Stopwatch.Frequency;
            if (elapsedMilliseconds <= ColdTickWarningMilliseconds)
                return;

            float now = RuntimeNowSeconds();
            if (now < _nextColdTickWarningTime)
                return;

            _nextColdTickWarningTime = now + ColdTickWarningCooldownSeconds;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _ColdTickOverBudgetWarningHash,
                _AssetLifecycleContextHash,
                (float)elapsedMilliseconds);
        }

        private void QueueAsyncDispatch(uint key)
        {
            if (!_assetRecords.TryGetValue(key, out AssetRecord record))
                return;

            if (record.ActiveRequestId != 0)
                return;

            AssetLoadDispatcher dispatcher = _cachedAssetLoadDispatcher;
            if (dispatcher == null)
                return;

            bool isDistantHlod = record.Priority == AssetPriorityTier.Tier5DistantHlod ||
                                 record.Priority == AssetPriorityTier.Tier6Speculative;

            if (!dispatcher.Enqueue(key, record.Priority, isDistantHlod, record.SizeBytes, out int requestId))
                return;

            record.ActiveRequestId = requestId;
            record.NextRetryTime = 0f;
            _assetRecords.Set(key, record);
        }

        private int DrainPendingReleaseQueue(int maxCount)
        {
            int drained = 0;
            if (maxCount <= 0 || _pendingReleaseQueue.Count <= 0)
                return 0;

            bool hasTrackerViews = TryResolveTrackerViews(
                out NativeArray<AssetTrackerDTO> trackers,
                out NativeArray<float> ttl,
                out NativeArray<byte> trackerFlags,
                out NativeArray<AssetHandleMapEntryDTO> handleMap);

            while (_pendingReleaseQueue.Count > 0 && drained < maxCount)
            {
                if (!_pendingReleaseQueue.TryDequeue(out uint key))
                    break;
                drained++;

                if (!_assetRecords.TryGetValue(key, out AssetRecord record))
                    continue;

                record.PendingRelease = false;
                _assetRecords.Set(key, record);

                if (record.RefCount > 0)
                {
                    if (hasTrackerViews &&
                        TryGetHandleSlot(key, handleMap, out int refSlot))
                    {
                        ClearNativeReleaseIntent(key, refSlot, trackers, ttl, trackerFlags, handleMap);
                    }
                    else
                    {
                        _nativeRefSyncRequired = true;
                    }

                    continue;
                }

                if (hasTrackerViews &&
                    IsNativeHandlePinned(key, trackerFlags, handleMap, out int pinnedSlot))
                {
                    ClearNativeReleaseIntent(key, pinnedSlot, trackers, ttl, trackerFlags, handleMap);
                    continue;
                }

                if (!TryExecuteOrDeferBlindFrameRelease(key, record) &&
                    _assetRecords.TryGetValue(key, out record) &&
                    record.RefCount == 0 &&
                    record.PendingRelease)
                {
                    break;
                }
            }

            return drained;
        }

        private static bool IsNativeHandlePinned(
            uint assetHash,
            NativeArray<byte> trackerFlags,
            NativeArray<AssetHandleMapEntryDTO> handleMap,
            out int slot)
        {
            slot = -1;
            if (assetHash == 0u ||
                !trackerFlags.IsCreated ||
                !handleMap.IsCreated ||
                !TryGetHandleSlot(assetHash, handleMap, out slot) ||
                (uint)slot >= (uint)trackerFlags.Length)
            {
                return false;
            }

            return (trackerFlags[slot] & AssetHandleFlags.Pinned) != 0;
        }

        private void EvaluateHardMemoryReaper(float now)
        {
            if (_hardReaperAsyncWindowActive)
            {
                PurgeAddressableCachesAsync();
                TryCompleteHardReaperAsyncWindow();
                return;
            }

            if (now < _nextHardReaperTime)
                return;

            ExecuteHardMemoryReaper(now);
        }

        private void ExecuteHardMemoryReaper(float now)
        {
            _nextHardReaperTime = now + HardReaperIntervalSeconds;
            if (_hardReaperAsyncWindowActive)
                return;

            _hardReaperAsyncWindowActive = true;
            _hardReaperUnloadComplete = false;
            _hardReaperBundleCacheCleanComplete = true;
            SetHardReaperScannerInterferenceActive(true);
            SystemDispatcher.RequestVisualStaticGlitch(HardReaperGlitchDurationSeconds);
            ForceDrainPendingReleaseQueue();
            int evicted = EvictLowestPriorityUnusedAssets(MaxHardReaperEvictions, AssetPriorityTier.Tier6Speculative);
            evicted += ReleaseDistantChunkAddressables(MaxHardReaperEvictions);
            PurgeAddressableCachesAsync();
            _hardReaperUnloadOperation = null;
            _hardReaperUnloadComplete = true;

            GlobalTelemetryBus.PublishPerformanceWarning(
                _HardReaperSweepWarningHash,
                _AssetLifecycleContextHash,
                evicted);

            TryCompleteHardReaperAsyncWindow();
        }

        private void PurgeAddressableCachesAsync()
        {
#if UNITY_ADDRESSABLES_EXIST
            if (_hardReaperCleanBundleCacheHandle.IsValid())
            {
                if (!_hardReaperCleanBundleCacheHandle.IsDone)
                {
                    _hardReaperBundleCacheCleanComplete = false;
                    return;
                }

                if (!CanAcceptAddressableRelease(_hardReaperCleanBundleCacheHandle))
                {
                    _hardReaperBundleCacheCleanComplete = false;
                    return;
                }

                if (!TryExecuteOrDeferBlindFrameRelease(_hardReaperCleanBundleCacheHandle))
                {
                    _hardReaperBundleCacheCleanComplete = false;
                    return;
                }

                _hardReaperCleanBundleCacheHandle = default;
                _hardReaperBundleCacheCleanComplete = true;
                return;
            }

            _hardReaperCleanBundleCacheHandle = Addressables.CleanBundleCache();
            if (_hardReaperCleanBundleCacheHandle.IsValid() && _hardReaperCleanBundleCacheCompletedCallback != null)
            {
                _hardReaperBundleCacheCleanComplete = false;
                _hardReaperCleanBundleCacheHandle.Completed += _hardReaperCleanBundleCacheCompletedCallback;
                return;
            }

            _hardReaperBundleCacheCleanComplete = true;
#endif
        }

        private void HandleHardReaperUnloadCompleted(AsyncOperation operation)
        {
            if (!ReferenceEquals(operation, _hardReaperUnloadOperation))
                return;

            if (_hardReaperUnloadCompletedCallback != null)
                operation.completed -= _hardReaperUnloadCompletedCallback;

            _hardReaperUnloadComplete = true;
            _hardReaperUnloadOperation = null;
            TryCompleteHardReaperAsyncWindow();
        }

#if UNITY_ADDRESSABLES_EXIST
        private void HandleHardReaperCleanBundleCacheCompleted(AsyncOperationHandle<bool> handle)
        {
            if (_hardReaperCleanBundleCacheHandle.IsValid() &&
                _hardReaperCleanBundleCacheHandle.Equals(handle))
            {
                if (!CanAcceptAddressableRelease(_hardReaperCleanBundleCacheHandle))
                {
                    _hardReaperBundleCacheCleanComplete = false;
                    return;
                }

                if (!TryExecuteOrDeferBlindFrameRelease(_hardReaperCleanBundleCacheHandle))
                {
                    _hardReaperBundleCacheCleanComplete = false;
                    return;
                }

                _hardReaperCleanBundleCacheHandle = default;
            }
            else if (handle.IsValid())
            {
                if (!CanAcceptAddressableRelease(handle))
                {
                    if (!_hardReaperCleanBundleCacheHandle.IsValid())
                    {
                        _hardReaperCleanBundleCacheHandle = handle;
                    }
                    else if (!TryExecuteOrForceAddressableReleaseFault(handle))
                    {
                        _hardReaperBundleCacheCleanComplete = false;
                        return;
                    }

                    _hardReaperBundleCacheCleanComplete = false;
                    return;
                }

                if (!TryExecuteOrDeferBlindFrameRelease(handle))
                {
                    if (!_hardReaperCleanBundleCacheHandle.IsValid())
                    {
                        _hardReaperCleanBundleCacheHandle = handle;
                    }
                    else if (!TryExecuteOrForceAddressableReleaseFault(handle))
                    {
                        _hardReaperBundleCacheCleanComplete = false;
                        return;
                    }

                    _hardReaperBundleCacheCleanComplete = false;
                    return;
                }
            }

            _hardReaperBundleCacheCleanComplete = true;
            TryCompleteHardReaperAsyncWindow();
        }
#endif

        private void TryCompleteHardReaperAsyncWindow()
        {
            if (!_hardReaperAsyncWindowActive ||
                !_hardReaperUnloadComplete ||
                !_hardReaperBundleCacheCleanComplete)
            {
                return;
            }

            _hardReaperAsyncWindowActive = false;
            SetHardReaperScannerInterferenceActive(false);
        }

        private bool ReleaseHardReaperAsyncHandles()
        {
            if (_hardReaperUnloadOperation != null && _hardReaperUnloadCompletedCallback != null)
                _hardReaperUnloadOperation.completed -= _hardReaperUnloadCompletedCallback;

            _hardReaperUnloadOperation = null;
#if UNITY_ADDRESSABLES_EXIST
            if (_hardReaperCleanBundleCacheHandle.IsValid())
            {
                if (_hardReaperCleanBundleCacheCompletedCallback != null)
                    _hardReaperCleanBundleCacheHandle.Completed -= _hardReaperCleanBundleCacheCompletedCallback;

                if (!TryExecuteOrForceAddressableReleaseFault(_hardReaperCleanBundleCacheHandle))
                {
                    _hardReaperBundleCacheCleanComplete = false;
                    return false;
                }

                _hardReaperCleanBundleCacheHandle = default;
            }
#endif
            _hardReaperAsyncWindowActive = false;
            _hardReaperUnloadComplete = false;
            _hardReaperBundleCacheCleanComplete = false;
            return true;
        }

        private void SetHardReaperScannerInterferenceActive(bool active)
        {
            IScannerInterferenceUiSink scannerInterference = _cachedScannerInterferenceUi;
            if (scannerInterference != null)
                scannerInterference.SetScannerInterferenceActive(active);
        }

        private bool ExecuteReleaseFlow(uint key)
        {
            if (!_assetRecords.TryGetValue(key, out AssetRecord record))
                return false;

            if (record.RefCount > 0)
                return false;

#if UNITY_ADDRESSABLES_EXIST
            bool hasValidAddressableHandle = record.HasAddressableHandle && record.AddressableHandle.IsValid();
            bool releaseExecutesInCurrentFrame = hasValidAddressableHandle &&
                                                 (IsBlindReleaseFrame() || IsVramPanicReleaseFrame());
            if (hasValidAddressableHandle && !CanAcceptAddressableRelease(record.AddressableHandle))
            {
                record.PendingRelease = EnqueuePendingRelease(key);
                _assetRecords.Set(key, record);
                return false;
            }
#endif

            bool nativeSlotCleared = ClearNativeHandleSlot(key);

            if (!nativeSlotCleared)
            {
#if UNITY_ADDRESSABLES_EXIST
                ClearManagedAddressableSlotBestEffort(key, record.AddressableHandle);
#endif
                _lastLeakSuspectHash = key;
                _nativeRefSyncRequired = true;
            }

            AssetLoadDispatcher dispatcher = _cachedAssetLoadDispatcher;
            if (dispatcher != null)
            {
                dispatcher.CancelByAssetKey(key);
                if (record.ActiveRequestId != 0)
                    dispatcher.AcknowledgeDispatchRequest(record.ActiveRequestId, false);
            }

            QueueOwnerPresentationDisable(record.Owner);

#if UNITY_ADDRESSABLES_EXIST
            if (hasValidAddressableHandle)
            {
                if (!TryExecuteOrDeferBlindFrameRelease(record.AddressableHandle))
                {
                    record.PendingRelease = EnqueuePendingRelease(key);
                    _assetRecords.Set(key, record);
                    return false;
                }

                if (releaseExecutesInCurrentFrame)
                    _orphanedHandlesReleased++;
            }
            else if (record.OwnsAssetInstance && record.Asset != null && !ReferenceEquals(record.Asset, _checkerboardMaterial))
#else
            if (record.OwnsAssetInstance && record.Asset != null && !ReferenceEquals(record.Asset, _checkerboardMaterial))
#endif
            {
                Destroy(record.Asset);
            }

            TrackedResidentBytes -= record.SizeBytes;
            if (TrackedResidentBytes < 0L)
                TrackedResidentBytes = 0L;

            _assetRecords.Remove(key);
            return true;
        }

#if UNITY_ADDRESSABLES_EXIST
        private bool CanAcceptAddressableRelease(AsyncOperationHandle handle)
        {
            if (!handle.IsValid())
                return true;

            if (IsBlindReleaseFrame() || IsVramPanicReleaseFrame())
                return true;

            if (_detachedReleaseHandles == null)
                return false;

            for (int i = 0; i < _detachedReleaseHandles.Length; i++)
            {
                if (!_detachedReleaseHandles[i].IsValid() ||
                    _detachedReleaseHandles[i].Equals(handle))
                {
                    return true;
                }
            }

            return false;
        }

        private void ClearManagedAddressableSlotBestEffort(uint assetHash, AsyncOperationHandle handle)
        {
            if (_addressableHandlePool == null)
                return;

            bool hasComparableHandle = handle.IsValid();
            for (int i = 0; i < _addressableHandlePool.Length; i++)
            {
                bool hashMatches = _addressableHandleHashes != null &&
                                   (uint)i < (uint)_addressableHandleHashes.Length &&
                                   _addressableHandleHashes[i] == assetHash;
                bool handleMatches = hasComparableHandle && _addressableHandlePool[i].Equals(handle);
                if (!hashMatches && !handleMatches)
                    continue;

                _addressableHandlePool[i] = default;
                if (_addressableHandleHashes != null && (uint)i < (uint)_addressableHandleHashes.Length)
                    _addressableHandleHashes[i] = 0u;
                if (_addressableBundlePrefixHashes != null && (uint)i < (uint)_addressableBundlePrefixHashes.Length)
                    _addressableBundlePrefixHashes[i] = 0u;
                return;
            }
        }

        private bool TryReleaseManagedAddressableSlotForOrphan(uint assetHash, int slot)
        {
            if (_addressableHandlePool == null)
                return true;

            bool released = true;
            if ((uint)slot < (uint)_addressableHandlePool.Length)
            {
                AsyncOperationHandle slotHandle = _addressableHandlePool[slot];
                if (slotHandle.IsValid() && !TryExecuteOrForceAddressableReleaseFault(slotHandle))
                {
                    released = false;
                }
                else
                {
                    _addressableHandlePool[slot] = default;
                    if (_addressableHandleHashes != null && (uint)slot < (uint)_addressableHandleHashes.Length)
                        _addressableHandleHashes[slot] = 0u;
                    if (_addressableBundlePrefixHashes != null && (uint)slot < (uint)_addressableBundlePrefixHashes.Length)
                        _addressableBundlePrefixHashes[slot] = 0u;
                }
            }

            for (int i = 0; i < _addressableHandlePool.Length; i++)
            {
                if (i == slot)
                    continue;

                bool hashMatches = _addressableHandleHashes != null &&
                                   (uint)i < (uint)_addressableHandleHashes.Length &&
                                   _addressableHandleHashes[i] == assetHash;
                if (!hashMatches)
                    continue;

                AsyncOperationHandle handle = _addressableHandlePool[i];
                if (handle.IsValid() && !TryExecuteOrForceAddressableReleaseFault(handle))
                {
                    released = false;
                    continue;
                }

                _addressableHandlePool[i] = default;
                if (_addressableHandleHashes != null && (uint)i < (uint)_addressableHandleHashes.Length)
                    _addressableHandleHashes[i] = 0u;
                if (_addressableBundlePrefixHashes != null && (uint)i < (uint)_addressableBundlePrefixHashes.Length)
                    _addressableBundlePrefixHashes[i] = 0u;
            }

            if (!released)
            {
                _lastLeakSuspectHash = assetHash;
                DumpHeapTelemetry();
            }

            return released;
        }
#endif

        private bool IsAddressableReleaseBlockedByBlindFrame(in AssetRecord record)
        {
#if UNITY_ADDRESSABLES_EXIST
            if (!record.HasAddressableHandle)
                return false;

            if (IsVramPanicReleaseFrame())
                return false;

            return !IsBlindReleaseFrame();
#else
            return false;
#endif
        }

#if UNITY_ADDRESSABLES_EXIST
        private bool TryExecuteOrDeferBlindFrameRelease(AsyncOperationHandle handle)
        {
            if (!handle.IsValid())
                return true;

            if (IsBlindReleaseFrame() || IsVramPanicReleaseFrame())
            {
                Addressables.Release(handle);
                return true;
            }

            return EnqueueDetachedAddressableRelease(handle);
        }

        private bool TryExecuteOrDeferBlindFrameRelease<TObject>(AsyncOperationHandle<TObject> handle)
        {
            return TryExecuteOrDeferBlindFrameRelease((AsyncOperationHandle)handle);
        }

        private bool TryExecuteOrForceAddressableReleaseFault(AsyncOperationHandle handle)
        {
            if (!handle.IsValid())
                return true;

            if (TryExecuteOrDeferBlindFrameRelease(handle))
                return true;

            bool previousPanicActive = _externalVramPanicActive;
            float previousPanicUntil = _externalVramPanicUntil;
            _externalVramPanicActive = true;
            _externalVramPanicUntil = 0f;
            bool released = TryExecuteOrDeferBlindFrameRelease(handle);
            _externalVramPanicActive = previousPanicActive;
            _externalVramPanicUntil = previousPanicUntil;
            if (released)
                return true;

            _lastLeakSuspectHash = _lastLeakSuspectHash != 0u ? _lastLeakSuspectHash : CollisionSalt;
            DumpHeapTelemetry();
            return false;
        }

        private bool TryExecuteOrForceAddressableReleaseFault<TObject>(AsyncOperationHandle<TObject> handle)
        {
            return TryExecuteOrForceAddressableReleaseFault((AsyncOperationHandle)handle);
        }
#endif

        private bool TryExecuteOrDeferBlindFrameRelease(uint key, AssetRecord record)
        {
            if (IsAddressableReleaseBlockedByBlindFrame(in record))
            {
                if (!record.PendingRelease)
                {
                    record.PendingRelease = EnqueuePendingRelease(key);
                }

                _nativeRefSyncRequired = true;
                _assetRecords.Set(key, record);
                return false;
            }

            record.PendingRelease = false;
            _assetRecords.Set(key, record);
            return ExecuteReleaseFlow(key);
        }

#if UNITY_ADDRESSABLES_EXIST
        private bool EnqueueDetachedAddressableRelease(AsyncOperationHandle handle)
        {
            if (!handle.IsValid())
                return true;

            if (_detachedReleaseHandles == null)
            {
                _lastLeakSuspectHash = _lastLeakSuspectHash != 0u ? _lastLeakSuspectHash : CollisionSalt;
                DumpHeapTelemetry();
                return false;
            }

            for (int i = 0; i < _detachedReleaseHandles.Length; i++)
            {
                if (_detachedReleaseHandles[i].IsValid())
                {
                    if (_detachedReleaseHandles[i].Equals(handle))
                        return true;

                    continue;
                }

                _detachedReleaseHandles[i] = handle;
                if (i >= _detachedReleaseHandleCount)
                    _detachedReleaseHandleCount = i + 1;
                return true;
            }

            _lastLeakSuspectHash = _lastLeakSuspectHash != 0u ? _lastLeakSuspectHash : CollisionSalt;
            DumpHeapTelemetry();
            return false;
        }

        private void DrainDetachedAddressableReleaseHandles()
        {
            if (_detachedReleaseHandles == null ||
                _detachedReleaseHandleCount <= 0 ||
                (!IsBlindReleaseFrame() && !IsVramPanicReleaseFrame()))
            {
                return;
            }

            int highestValid = -1;
            int count = math.min(_detachedReleaseHandleCount, _detachedReleaseHandles.Length);
            for (int i = 0; i < count; i++)
            {
                AsyncOperationHandle handle = _detachedReleaseHandles[i];
                if (!handle.IsValid() || TryExecuteOrDeferBlindFrameRelease(handle))
                {
                    _detachedReleaseHandles[i] = default;
                    continue;
                }

                highestValid = i;
            }

            for (int i = count; i < _detachedReleaseHandles.Length; i++)
            {
                if (_detachedReleaseHandles[i].IsValid())
                    highestValid = i;
            }

            _detachedReleaseHandleCount = highestValid + 1;
        }
#else
        private void DrainDetachedAddressableReleaseHandles()
        {
            // No operation required when Addressables are not present in the project.
        }
#endif

        private int ReleaseDistantChunkAddressables(int maxReleaseCount)
        {
            if (maxReleaseCount <= 0)
                return 0;

            IPlayerInventoryService playerInventory = _cachedPlayerInventory;
            ItemCatalog catalog = playerInventory != null && playerInventory.Inventory != null
                ? playerInventory.Inventory.ItemCatalog
                : null;
            if (catalog == null)
                return 0;

            return catalog.EvictWorldPrefabsBeyondPlayerAup(
                DistantChunkReleaseDistanceMeters,
                maxReleaseCount);
        }

        private static Component ResolvePresentationDisableTargetCold(Component owner)
        {
            if (owner == null)
                return null;

            if (owner is Renderer renderer)
                return renderer;

            if (owner is AudioSource audioSource)
                return audioSource;

            if (owner.TryGetComponent(out Renderer ownerRenderer))
                return ownerRenderer;

            if (owner.TryGetComponent(out AudioSource ownerAudioSource))
                return ownerAudioSource;

            return null;
        }

        private static void DisableOwnerPresentation(Component presentationTarget)
        {
            if (presentationTarget == null)
                return;

            if (presentationTarget is Renderer renderer)
            {
                renderer.enabled = false;
                return;
            }

            if (presentationTarget is AudioSource audioSource)
                audioSource.enabled = false;
        }

        private void QueueOwnerPresentationDisable(Component owner)
        {
            Component presentationTarget = ResolvePresentationDisableTargetCold(owner);
            if (presentationTarget == null)
                return;

            if (_pendingPresentationDisableCount >= _pendingPresentationDisableOwners.Length)
                return;

            _pendingPresentationDisableOwners[_pendingPresentationDisableCount++] = presentationTarget;
        }

        private void FlushPendingPresentationDisables()
        {
            for (int i = 0; i < _pendingPresentationDisableCount; i++)
            {
                Component owner = _pendingPresentationDisableOwners[i];
                _pendingPresentationDisableOwners[i] = null;
                DisableOwnerPresentation(owner);
            }

            _pendingPresentationDisableCount = 0;
        }

        private void ApplyFallbackMaterial(Component owner)
        {
            if (owner == null || _checkerboardMaterial == null)
                return;

            Renderer targetRenderer = ResolveOwnerRendererCold(owner);
            if (targetRenderer == null)
                return;

            targetRenderer.sharedMaterial = _checkerboardMaterial;
        }

        private static Renderer ResolveOwnerRendererCold(Component owner)
        {
            if (owner == null)
                return null;

            if (owner is Renderer renderer)
                return renderer;

            return owner.TryGetComponent(out Renderer ownerRenderer) ? ownerRenderer : null;
        }

        private bool TryApplyShaderFallback(ref AssetRecord record, Object asset)
        {
            Material material = asset as Material;
            if (material == null)
                return false;

            Shader shader = material.shader;
            if (!IsFailedShader(shader, out uint shaderHash))
                return false;

            EnsureFallbackAssets();
            if (_checkerboardMaterial == null)
                return false;

            uint materialHash = unchecked((uint)EntityId.ToULong(material.GetEntityId()));
            if (record.OwnsAssetInstance && !ReferenceEquals(material, _checkerboardMaterial))
                Destroy(material);

            record.Asset = _checkerboardMaterial;
            record.IsFallback = true;
            record.OwnsAssetInstance = false;
            ApplyFallbackMaterial(record.Owner);
            GlobalTelemetryBus.PublishShaderFallback(materialHash, shaderHash, 1f);
            GlobalTelemetryBus.PublishPerformanceWarning(_ShaderFallbackWarningHash, materialHash, 1f);
            return true;
        }

        private static bool IsFailedShader(Shader shader, out uint shaderHash)
        {
            shaderHash = 0u;
            if (shader == null)
                return true;

            shaderHash = unchecked((uint)EntityId.ToULong(shader.GetEntityId()));
            if (!shader.isSupported)
                return true;

            string shaderName = shader.name;
            return !string.IsNullOrEmpty(shaderName) &&
                   shaderName.IndexOf("InternalErrorShader", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void InsertEvictionCandidate(uint key)
        {
            int insertIndex = _evictionCandidates.Count;
            for (int i = 0; i < _evictionCandidates.Count; i++)
            {
                if (CompareEvictionPriority(key, _evictionCandidates[i]) < 0)
                {
                    insertIndex = i;
                    break;
                }
            }

            _evictionCandidates.Insert(insertIndex, key);
            if (_evictionCandidates.Count > 16)
                _evictionCandidates.RemoveAt(_evictionCandidates.Count - 1);
        }

        private int CompareEvictionPriority(uint leftKey, uint rightKey)
        {
            AssetRecord left = _assetRecords.GetUnchecked(leftKey);
            AssetRecord right = _assetRecords.GetUnchecked(rightKey);

            if (left.Priority != right.Priority)
                return (byte)right.Priority - (byte)left.Priority;

            if (left.LastAccessFrame < right.LastAccessFrame)
                return -1;

            if (left.LastAccessFrame > right.LastAccessFrame)
                return 1;

            return 0;
        }

        private struct FixedUIntList
        {
            private uint[] _items;
            private int _count;

            public int Count => _count;

            public uint this[int index] => _items[index];

            public void Initialize(int capacity)
            {
                int safeCapacity = Mathf.Max(1, capacity);
                if (_items != null && _items.Length == safeCapacity)
                    return;

                _items = new uint[safeCapacity]; // COLD ALLOC: uint[capacity] - fixed scratch list storage - owner: AssetLifecycleGovernor
                _count = 0;
            }

            public void Clear()
            {
                _count = 0;
            }

            public bool Add(uint value)
            {
                if (_items == null || _count >= _items.Length)
                    return false;

                _items[_count] = value;
                _count++;
                return true;
            }

            public void Insert(int index, uint value)
            {
                if (_items == null || _items.Length == 0)
                    return;

                int clamped = math.clamp(index, 0, _count);
                int last = math.min(_count, _items.Length - 1);
                for (int i = last; i > clamped; i--)
                    _items[i] = _items[i - 1];

                _items[clamped] = value;
                if (_count < _items.Length)
                    _count++;
            }

            public void RemoveAt(int index)
            {
                if (_items == null || (uint)index >= (uint)_count)
                    return;

                for (int i = index; i < _count - 1; i++)
                    _items[i] = _items[i + 1];

                _items[_count - 1] = 0u;
                _count--;
            }
        }

        private struct FixedUIntQueue
        {
            private uint[] _items;
            private int _head;
            private int _tail;
            private int _count;

            public int Count => _count;

            public void Initialize(int capacity)
            {
                int safeCapacity = Mathf.Max(1, capacity);
                if (_items != null && _items.Length == safeCapacity)
                    return;

                _items = new uint[safeCapacity]; // COLD ALLOC: uint[capacity] - fixed pending release ring - owner: AssetLifecycleGovernor
                _head = 0;
                _tail = 0;
                _count = 0;
            }

            public void Clear()
            {
                _head = 0;
                _tail = 0;
                _count = 0;
            }

            public bool Enqueue(uint value)
            {
                if (_items == null || _items.Length == 0 || _count >= _items.Length)
                    return false;

                _items[_tail] = value;
                _tail++;
                if (_tail >= _items.Length)
                    _tail = 0;

                _count++;
                return true;
            }

            public bool Contains(uint value)
            {
                if (_items == null || _items.Length == 0 || _count <= 0)
                    return false;

                int index = _head;
                for (int i = 0; i < _count; i++)
                {
                    if (_items[index] == value)
                        return true;

                    index++;
                    if (index >= _items.Length)
                        index = 0;
                }

                return false;
            }

            public bool TryDequeue(out uint value)
            {
                value = 0u;
                if (_items == null || _items.Length == 0 || _count <= 0)
                    return false;

                value = _items[_head];
                _items[_head] = 0u;
                _head++;
                if (_head >= _items.Length)
                    _head = 0;

                _count--;
                return true;
            }
        }

        private struct ManagedAssetRecordTable
        {
            private const byte Empty = 0;
            private const byte Occupied = 1;
            private const byte Tombstone = 2;

            private uint[] _keys;
            private AssetRecord[] _records;
            private byte[] _states;
            private int _count;

            public int Count => _count;

            public void Initialize(int capacity)
            {
                int safeCapacity = Mathf.Max(2, capacity);
                if (_keys != null && _keys.Length == safeCapacity)
                    return;

                _keys = new uint[safeCapacity]; // COLD ALLOC: uint[capacity] - fixed asset record hash keys - owner: AssetLifecycleGovernor
                _records = new AssetRecord[safeCapacity]; // COLD ALLOC: AssetRecord[capacity] - fixed managed asset metadata slots - owner: AssetLifecycleGovernor
                _states = new byte[safeCapacity]; // COLD ALLOC: byte[capacity] - fixed asset record slot states - owner: AssetLifecycleGovernor
                _count = 0;
            }

            public void Clear()
            {
                if (_keys == null)
                    return;

                for (int i = 0; i < _keys.Length; i++)
                {
                    _keys[i] = 0u;
                    _records[i] = default;
                    _states[i] = Empty;
                }

                _count = 0;
            }

            public bool TryGetValue(uint key, out AssetRecord record)
            {
                record = default;
                if (!TryFindIndex(key, out int index))
                    return false;

                record = _records[index];
                return true;
            }

            public AssetRecord GetUnchecked(uint key)
            {
                return TryGetValue(key, out AssetRecord record) ? record : default;
            }

            public bool Set(uint key, AssetRecord record)
            {
                if (!TryFindInsertIndex(key, out int index))
                    return false;

                if (_states[index] != Occupied)
                    _count++;

                _keys[index] = key;
                _records[index] = record;
                _states[index] = Occupied;
                return true;
            }

            public bool Remove(uint key)
            {
                if (!TryFindIndex(key, out int index))
                    return false;

                _records[index] = default;
                _states[index] = Tombstone;
                _count = math.max(0, _count - 1);
                return true;
            }

            public Enumerator GetEnumerator()
            {
                return new Enumerator(_keys, _records, _states);
            }

            private bool TryFindIndex(uint key, out int index)
            {
                index = -1;
                if (_keys == null || _keys.Length == 0)
                    return false;

                int length = _keys.Length;
                int start = (int)(key % unchecked((uint)length));
                for (int probe = 0; probe < length; probe++)
                {
                    int candidate = start + probe;
                    if (candidate >= length)
                        candidate -= length;

                    byte state = _states[candidate];
                    if (state == Occupied)
                    {
                        if (_keys[candidate] == key)
                        {
                            index = candidate;
                            return true;
                        }

                        continue;
                    }

                    if (state == Empty)
                        return false;
                }

                return false;
            }

            private bool TryFindInsertIndex(uint key, out int index)
            {
                index = -1;
                if (_keys == null || _keys.Length == 0)
                    return false;

                int firstTombstone = -1;
                int length = _keys.Length;
                int start = (int)(key % unchecked((uint)length));
                for (int probe = 0; probe < length; probe++)
                {
                    int candidate = start + probe;
                    if (candidate >= length)
                        candidate -= length;

                    byte state = _states[candidate];
                    if (state == Occupied)
                    {
                        if (_keys[candidate] == key)
                        {
                            index = candidate;
                            return true;
                        }

                        continue;
                    }

                    if (state == Tombstone)
                    {
                        if (firstTombstone < 0)
                            firstTombstone = candidate;
                        continue;
                    }

                    index = firstTombstone >= 0 ? firstTombstone : candidate;
                    return true;
                }

                if (firstTombstone >= 0)
                {
                    index = firstTombstone;
                    return true;
                }

                return false;
            }

            public struct Entry
            {
                public uint Key;
                public AssetRecord Value;

                public Entry(uint key, AssetRecord value)
                {
                    Key = key;
                    Value = value;
                }
            }

            public struct Enumerator
            {
                private readonly uint[] _keys;
                private readonly AssetRecord[] _records;
                private readonly byte[] _states;
                private int _index;

                public Entry Current;

                public Enumerator(uint[] keys, AssetRecord[] records, byte[] states)
                {
                    _keys = keys;
                    _records = records;
                    _states = states;
                    _index = -1;
                    Current = default;
                }

                public bool MoveNext()
                {
                    if (_keys == null)
                        return false;

                    while (++_index < _keys.Length)
                    {
                        if (_states[_index] != Occupied)
                            continue;

                        Current = new Entry(_keys[_index], _records[_index]);
                        return true;
                    }

                    return false;
                }
            }
        }

        private void EnsureFallbackAssets()
        {
            _checkerboardMaterial = authoredCheckerboardMaterial;
        }

        private void DisposeFallbackAssets()
        {
            _checkerboardMaterial = null;
        }

        private static long ClampNonNegative(long value)
        {
            return value > 0L ? value : 0L;
        }

        private void ReplaceTrackedSize(ref AssetRecord record, long nextSizeBytes)
        {
            long clampedNextSize = ClampNonNegative(nextSizeBytes);
            TrackedResidentBytes -= record.SizeBytes;
            if (TrackedResidentBytes < 0L)
                TrackedResidentBytes = 0L;

            TrackedResidentBytes += clampedNextSize;
            record.SizeBytes = clampedNextSize;
        }

        private bool ResolveCollision(ref uint key, string assetGuid, string address)
        {
            if (!_assetRecords.TryGetValue(key, out AssetRecord existing))
                return true;

            if (MatchesIdentity(existing, assetGuid, address))
                return true;

            uint saltedKey = key ^ CollisionSalt;
            if (!_assetRecords.TryGetValue(saltedKey, out existing))
            {
                key = saltedKey;
                return true;
            }

            if (MatchesIdentity(existing, assetGuid, address))
            {
                key = saltedKey;
                return true;
            }

#if UNITY_EDITOR
            Hecton8.Core.H8Debug.LogError("[AssetLifecycleGovernor] Asset key collision.");
#endif
            return false;
        }

        private static bool MatchesIdentity(AssetRecord record, string assetGuid, string address)
        {
            if (!string.IsNullOrEmpty(assetGuid) && !string.IsNullOrEmpty(record.AssetGuid))
                return string.Equals(record.AssetGuid, assetGuid, System.StringComparison.Ordinal);

            return string.Equals(record.Address, address, System.StringComparison.Ordinal);
        }

        private static uint CreateKey(string assetGuid, string address, byte biomeId, byte lodLevel)
        {
            if (!string.IsNullOrEmpty(assetGuid) &&
                PreInitAssetIdMap.TryResolve(assetGuid.AsSpan(), out uint assetId))
            {
                return PreInitAssetIdMap.MixAssetVariant(assetId, biomeId, lodLevel);
            }

            string identity = !string.IsNullOrEmpty(assetGuid) ? assetGuid : address;
            if (string.IsNullOrEmpty(identity))
                identity = "UNRESOLVED_ASSET";

            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < identity.Length; i++)
                {
                    hash ^= identity[i];
                    hash *= 16777619u;
                }

                hash ^= biomeId;
                hash *= 16777619u;
                hash ^= lodLevel;
                hash *= 16777619u;
                return hash;
            }
        }
    }
}
