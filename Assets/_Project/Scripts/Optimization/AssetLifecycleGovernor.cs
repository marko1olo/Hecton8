using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Hecton8.Core;
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
    public sealed class AssetLifecycleGovernor : MonoBehaviour, ITickable, IUpdatable, ISlowTickable
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
        private const float MinimumAdaptiveTtlSeconds = 10f;
        private const float DefaultHighEndTtlSeconds = 300f;
        private const float SharedBundleTtlMultiplier = 2f;
        private const int LeakRefCountThreshold = 50;
        private const uint HeapTelemetryFaultFlag = 1u << 0;
        private const uint HeapTelemetryVramPanicFlag = 1u << 1;
        private const uint HeapTelemetryBlindReleaseFlag = 1u << 2;
        private const uint HeapTelemetryLeakSuspectFlag = 1u << 3;
        private const int MaxColdDistantChunkReleases = 8;
        private const int MaxHardReaperEvictions = 64;
        private const double ColdTickWarningMilliseconds = 0.2d;
        private const float ColdTickWarningCooldownSeconds = 5f;
        private static readonly uint _AssetLifecycleContextHash = unchecked((uint)Hecton.Localization.LocHash.Compute("AssetLifecycleGovernor"));
        private static readonly uint _ColdTickOverBudgetWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("AssetLifecycleGovernor.ColdTickOverBudget"));
        private static readonly uint _DoubleReleaseWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("AssetLifecycleGovernor.DoubleRelease"));
        private static readonly uint _HardReaperSweepWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("AssetLifecycleGovernor.HardReaperSweep"));
        private static readonly uint _ShaderFallbackWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("AssetLifecycleGovernor.ShaderFallback"));
        private static readonly float[] _retryBackoffSeconds = { 5f, 15f, 60f };

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

        private bool _registeredTick;
        private bool _registeredSlowTick;
        private bool _registeredService;
        private long _frameSequence;
        private float _nextColdReleaseTime;
        private float _nextColdTickWarningTime;
        private float _nextHardReaperTime;
        private Texture2D _checkerboardTexture;
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
        private VaultBufferHandle<AssetTrackerDTO> _assetTrackerVaultHandle;
        private VaultBufferHandle<float> _assetTtlVaultHandle;
        private VaultBufferHandle<byte> _assetTrackerFlagsVaultHandle;
        private VaultBufferHandle<AssetHandleMapEntryDTO> _assetHandleMapVaultHandle;
        private VaultBufferHandle<AssetCacheProfileDTO> _cacheProfileVaultHandle;
        private VaultBufferHandle<AssetHeapTelemetryEntry> _heapTelemetryVaultHandle;
        private SystemDispatcher _cachedDispatcher;
        private VRAMPressureMonitor _cachedVramPressure;
        private JobHandle _ttlEvaluationHandle;
        private bool _ttlEvaluationScheduled;
        private bool _ttlEvaluationResultsPending;
        private bool _ttlEvaluationVaultLocksHeld;
        private bool _ttlEvaluationVramPanic;
        private bool _nativeRefSyncRequired;
        private int _deferredTrackerMutationCount;
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
        private uint[] _addressableHandleHashes;
        private uint[] _addressableBundlePrefixHashes;
        private int _addressableHandleCount;
#endif

        // COLD ALLOC: Dictionary<uint, AssetRecord>[MaxTrackedAddressableCapacity] - global asset residency registry - owner: AssetLifecycleGovernor
        private readonly Dictionary<uint, AssetRecord> _registry = new Dictionary<uint, AssetRecord>(MaxTrackedAddressableCapacity);
        // COLD ALLOC: Queue<uint>[MaxTrackedAddressableCapacity] - pending release queue drained on cold tick and pressure passes - owner: AssetLifecycleGovernor
        private readonly Queue<uint> _pendingRelease = new Queue<uint>(MaxTrackedAddressableCapacity);
        // COLD ALLOC: List<uint>[64] - eviction candidate scratch buffer capped by hard reaper pass - owner: AssetLifecycleGovernor
        private readonly List<uint> _evictionCandidates = new List<uint>(MaxHardReaperEvictions);
        // COLD ALLOC: List<uint>[64] - retry candidate scratch buffer for failed async dispatches - owner: AssetLifecycleGovernor
        private readonly List<uint> _retryCandidates = new List<uint>(MaxHardReaperEvictions);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // COLD ALLOC: StringBuilder[512] - throttled diagnostics builder - owner: AssetLifecycleGovernor
#endif

        internal long TrackedResidentBytes { get; private set; }
        internal long NativeHeapEstimateBytes => (long)(TrackedResidentBytes * NativeHeapOverheadFactor);
        internal int PendingReleaseCount => _pendingRelease.Count;
        internal Material CheckerboardMaterial => _checkerboardMaterial;
#if UNITY_ADDRESSABLES_EXIST
        internal int AddressableDependencyGroupLoadCount => _addressableDependencyGroupLoadCount;
#endif

        private void Awake()
        {
            int bootstrapHandleCapacity = Mathf.Clamp(maxTrackedAddressableHandles, 1, MaxTrackedAddressableCapacity);
            maxTrackedAddressableHandles = bootstrapHandleCapacity;
            maxRegistryCapacity = Mathf.Max(Mathf.Max(1, maxRegistryCapacity), bootstrapHandleCapacity);
            _registry.EnsureCapacity(maxRegistryCapacity);
            EnsureNativeHandleStorage();
            _nextHardReaperTime = Time.unscaledTime + HardReaperIntervalSeconds;
            _hardReaperUnloadCompletedCallback = HandleHardReaperUnloadCompleted;
#if UNITY_ADDRESSABLES_EXIST
            _hardReaperCleanBundleCacheCompletedCallback = HandleHardReaperCleanBundleCacheCompleted;
#endif
            EnsureFallbackAssets();
        }

        private void OnEnable()
        {
            EnsureNativeHandleStorage();
            EnsureFallbackAssets();
            if (TryRegisterService())
                TryRegister();
        }

        private void Start()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterService();
            ResetAddressableHeapRuntimeState(false);
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterService();
            ResetAddressableHeapRuntimeState(true);
        }

        private void ResetAddressableHeapRuntimeState(bool disposeFallbackAssets)
        {
            ReleaseHardReaperAsyncHandles();
            SetHardReaperScannerInterferenceActive(false);
            _mockScreenFadeToBlackActive = false;
            _mockScreenFadeToBlackUntil = 0f;
            _explicitBlindFrameWindowActive = false;
            _explicitBlindFrameWindowUntil = 0f;
            _externalVramPanicActive = false;
            _externalVramPanicUntil = 0f;

            _registry.Clear();
            _pendingRelease.Clear();
            _evictionCandidates.Clear();
            _retryCandidates.Clear();
            DisposeNativeHandleStorage();
            _frameSequence = 0L;
            _nextColdReleaseTime = 0f;
            _nextColdTickWarningTime = 0f;
            _nextHardReaperTime = Time.unscaledTime + HardReaperIntervalSeconds;
            TrackedResidentBytes = 0L;
            _orphanedHandlesReleased = 0;
            _cacheHitCount = 0;
            _cacheMissCount = 0;
            _forcedVramReleaseCount = 0;
            _lastPendingTtlCount = 0;
            _lastLeakSuspectHash = 0u;
            _deferredTrackerMutationCount = 0;
#if UNITY_ADDRESSABLES_EXIST
            _lastAddressableDependencyGroupHash = 0u;
            _lastAddressableDependencyOrder = 0;
            _addressableDependencyGroupLoadCount = 0;
#endif

            if (disposeFallbackAssets)
                DisposeFallbackAssets();
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            _frameSequence++;
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            float now = Time.unscaledTime;
            if (now < _nextColdReleaseTime)
                return;

            _nextColdReleaseTime = now + ColdReleaseIntervalSeconds;
            long startTicks = Stopwatch.GetTimestamp();
            try
            {
                EvaluateAddressableTtlAndQueueReleases();
                DrainPendingReleaseQueue(maxDeferredReleasesPerFrame);
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

            if (_registry.TryGetValue(key, out AssetRecord record))
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

                _registry[key] = record;
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
            _registry[key] = created;
            TrackedResidentBytes += created.SizeBytes;

            if (asset == null && residencyKind != AssetResidencyKind.SceneOwned)
                QueueAsyncDispatch(key);

            return key;
        }

        internal void MarkLoaded(uint key, Object asset, long sizeBytes, bool ownsAssetInstance = false)
        {
            if (!_registry.TryGetValue(key, out AssetRecord record))
                return;

            if (record.ActiveRequestId != 0)
            {
                AssetLoadDispatcher dispatcher = GlobalRegistry.AssetLoadDispatcher;
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
            _registry[key] = record;
        }

#if UNITY_ADDRESSABLES_EXIST
        internal void MarkAddressableLoaded(
            uint key,
            AsyncOperationHandle handle,
            Object asset,
            long sizeBytes,
            bool isChunkAsset)
        {
            if (!_registry.TryGetValue(key, out AssetRecord record))
                return;

            if (record.ActiveRequestId != 0)
            {
                AssetLoadDispatcher dispatcher = GlobalRegistry.AssetLoadDispatcher;
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
            _registry[key] = record;
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

            if (!TryPrepareTrackerMutation())
            {
                return TryAcquireTrackedHandleFromManagedRecord(
                    assetHash,
                    owner,
                    priority,
                    residencyKind,
                    sizeBytes,
                    isChunkAsset,
                    out handle,
                    out cacheHit);
            }

            EnsureNativeHandleStorage();
            if (TryAcquireTrackedHandle(assetHash, owner, priority, residencyKind, sizeBytes, isChunkAsset, out handle, out cacheHit))
                return true;

            if (!TryResolveTrackerViews(
                    out _,
                    out _,
                    out NativeArray<byte> trackerFlags,
                    out _))
            {
                return false;
            }

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
                    Addressables.Release(handle);

                handle = default;
                _lastLeakSuspectHash = assetHash;
                DumpHeapTelemetry();
                return false;
            }

            _cacheMissCount++;
            return true;
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

            if (!TryPrepareTrackerMutation())
            {
                return TryAcquireTrackedHandleFromManagedRecord(
                    assetHash,
                    owner,
                    priority,
                    residencyKind,
                    sizeBytes,
                    isChunkAsset,
                    out handle,
                    out cacheHit);
            }

            EnsureNativeHandleStorage();
            if (TryAcquireTrackedHandle(assetHash, owner, priority, residencyKind, sizeBytes, isChunkAsset, out handle, out cacheHit))
                return true;

            if (!TryResolveTrackerViews(
                    out _,
                    out _,
                    out NativeArray<byte> trackerFlags,
                    out _))
            {
                return false;
            }

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

            string address = reference.AssetGUID;
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
                    Addressables.Release(handle);

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

        public void SetHeapSanitizerMockBlindFrame(bool active, float durationSeconds)
        {
            _mockScreenFadeToBlackActive = active;
            _mockScreenFadeToBlackUntil = active && durationSeconds > 0f
                ? Time.unscaledTime + durationSeconds
                : 0f;
        }
#endif

        public void SetHeapSanitizerBlindFrameWindow(bool active, float durationSeconds)
        {
            _explicitBlindFrameWindowActive = active;
            _explicitBlindFrameWindowUntil = active && durationSeconds > 0f
                ? Time.unscaledTime + durationSeconds
                : 0f;
        }

        public void SetHeapSanitizerVramPanicWindow(bool active, float durationSeconds)
        {
            _externalVramPanicActive = active;
            _externalVramPanicUntil = active && durationSeconds > 0f
                ? Time.unscaledTime + durationSeconds
                : 0f;
        }

        internal void MarkChunkResidency(uint key)
        {
            if (!_registry.TryGetValue(key, out AssetRecord record))
                return;

            record.IsChunkAsset = true;
            record.LastAccessFrame = _frameSequence;
            _registry[key] = record;
        }

        internal void MarkAccessed(uint key)
        {
            if (!_registry.TryGetValue(key, out AssetRecord record))
                return;

            record.LastAccessFrame = _frameSequence;
            _registry[key] = record;
        }

        internal void Release(uint key)
        {
            if (!_registry.TryGetValue(key, out AssetRecord record))
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[AssetLifecycleGovernor] Double release detected.", this);
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
                    record.PendingRelease = true;
                    _pendingRelease.Enqueue(key);
                }
            }

            _registry[key] = record;
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
            if (maxCount <= 0 || _registry.Count == 0)
                return 0;

            _evictionCandidates.Clear();

            Dictionary<uint, AssetRecord>.Enumerator enumerator = _registry.GetEnumerator();
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
                if (!_registry.TryGetValue(key, out AssetRecord record))
                    continue;

                if (TryExecuteOrDeferBlindFrameRelease(key, record))
                    evictions++;
            }

            _evictionCandidates.Clear();
            return evictions;
        }

        internal void ForceHardMemoryReaperSweep()
        {
            ExecuteHardMemoryReaper(Time.unscaledTime);
        }

        internal void MarkLoadFailed(uint key, string error)
        {
            if (!_registry.TryGetValue(key, out AssetRecord record))
                return;

            if (record.ActiveRequestId != 0)
            {
                AssetLoadDispatcher dispatcher = GlobalRegistry.AssetLoadDispatcher;
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
                record.NextRetryTime = Time.unscaledTime + _retryBackoffSeconds[record.RetryCount];
                record.RetryCount++;
            }

            ApplyFallbackMaterial(record.Owner);
            _registry[key] = record;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[AssetLifecycleGovernor] Asset load failed.", this);
#endif
        }

        private void EnsureNativeHandleStorage()
        {
            int capacity = Mathf.Clamp(maxTrackedAddressableHandles, 1, MaxTrackedAddressableCapacity);
            maxTrackedAddressableHandles = capacity;
            maxRegistryCapacity = Mathf.Max(Mathf.Max(1, maxRegistryCapacity), capacity);
            _registry.EnsureCapacity(maxRegistryCapacity);
            int mapCapacity = ResolveHandleMapCapacity(capacity);

            bool needsColdClear = !_nativeStorageInitialized ||
                                  _resolvedHandleCapacity != capacity ||
                                  _resolvedMapCapacity != mapCapacity ||
                                  !_assetTrackerVaultHandle.IsCreated ||
                                  !_assetHandleMapVaultHandle.IsCreated;

            if (!TryResolveHeapSanitizerVaultBuffers(capacity, mapCapacity))
            {
                EnsureFallbackImpostorMesh();
                return;
            }

            NativeArray<AssetTrackerDTO> trackers = _assetTrackerVaultHandle.Resolve(_dataVault);
            NativeArray<float> ttl = _assetTtlVaultHandle.Resolve(_dataVault);
            NativeArray<byte> flags = _assetTrackerFlagsVaultHandle.Resolve(_dataVault);
            NativeArray<AssetHandleMapEntryDTO> map = _assetHandleMapVaultHandle.Resolve(_dataVault);
            NativeArray<AssetCacheProfileDTO> profiles = _cacheProfileVaultHandle.Resolve(_dataVault);
            NativeArray<AssetHeapTelemetryEntry> telemetry = _heapTelemetryVaultHandle.Resolve(_dataVault);
            if (!trackers.IsCreated ||
                !ttl.IsCreated ||
                !flags.IsCreated ||
                !map.IsCreated ||
                !profiles.IsCreated ||
                !telemetry.IsCreated)
            {
                EnsureFallbackImpostorMesh();
                return;
            }

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
                GenerateEmergencyMockProfiles();
                MirrorCacheProfilesToVault();
            }
            EnsureFallbackImpostorMesh();

#if UNITY_ADDRESSABLES_EXIST
            if (_addressableHandlePool == null || _addressableHandlePool.Length != capacity)
            {
                _addressableHandlePool = new AsyncOperationHandle[capacity]; // COLD ALLOC: AsyncOperationHandle[capacity] - Unity handle bridge, indexed by Vault tracker slot - owner: AssetLifecycleGovernor
                _addressableHandleHashes = new uint[capacity]; // COLD ALLOC: uint[capacity] - handle slot asset hashes - owner: AssetLifecycleGovernor
                _addressableBundlePrefixHashes = new uint[capacity]; // COLD ALLOC: uint[capacity] - bundle prefix hashes for TTL inflation - owner: AssetLifecycleGovernor
                _addressableHandleCount = 0;
            }
#endif
        }

        private void DisposeNativeHandleStorage()
        {
            CompleteTtlEvaluationForTeardown();

#if UNITY_ADDRESSABLES_EXIST
            if (_addressableHandlePool != null)
            {
                for (int i = 0; i < _addressableHandlePool.Length; i++)
                {
                    AsyncOperationHandle handle = _addressableHandlePool[i];
                    if (handle.IsValid())
                        Addressables.Release(handle);

                    _addressableHandlePool[i] = default;
                }
            }

            _addressableHandlePool = null;
            _addressableHandleHashes = null;
            _addressableBundlePrefixHashes = null;
            _addressableHandleCount = 0;
#endif
            ClearAddressableHeapVaultState(false, false);
            _dataVault = null;
            _assetTrackerVaultHandle = default;
            _assetTtlVaultHandle = default;
            _assetTrackerFlagsVaultHandle = default;
            _assetHandleMapVaultHandle = default;
            _cacheProfileVaultHandle = default;
            _heapTelemetryVaultHandle = default;
            _cachedDispatcher = null;
            _cachedVramPressure = null;
            _ttlEvaluationResultsPending = false;
            _nativeRefSyncRequired = false;
            _nativeStorageInitialized = false;
            _resolvedHandleCapacity = 0;
            _resolvedMapCapacity = 0;

            if (_fallbackImpostorMesh != null)
            {
                Destroy(_fallbackImpostorMesh);
                _fallbackImpostorMesh = null;
            }
        }

        private void ClearAddressableHeapVaultState(bool clearCacheProfiles, bool clearTelemetry)
        {
            if (_dataVault == null)
                return;

            NativeArray<AssetTrackerDTO> trackers = _assetTrackerVaultHandle.IsCreated
                ? _assetTrackerVaultHandle.Resolve(_dataVault)
                : default;
            NativeArray<float> ttl = _assetTtlVaultHandle.IsCreated
                ? _assetTtlVaultHandle.Resolve(_dataVault)
                : default;
            NativeArray<byte> flags = _assetTrackerFlagsVaultHandle.IsCreated
                ? _assetTrackerFlagsVaultHandle.Resolve(_dataVault)
                : default;
            NativeArray<AssetHandleMapEntryDTO> map = _assetHandleMapVaultHandle.IsCreated
                ? _assetHandleMapVaultHandle.Resolve(_dataVault)
                : default;
            NativeArray<AssetCacheProfileDTO> profiles = _cacheProfileVaultHandle.IsCreated
                ? _cacheProfileVaultHandle.Resolve(_dataVault)
                : default;
            NativeArray<AssetHeapTelemetryEntry> telemetry = _heapTelemetryVaultHandle.IsCreated
                ? _heapTelemetryVaultHandle.Resolve(_dataVault)
                : default;

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
            if (trackers.IsCreated)
            {
                for (int i = 0; i < trackers.Length; i++)
                    trackers[i] = default;
            }

            if (ttl.IsCreated)
            {
                for (int i = 0; i < ttl.Length; i++)
                    ttl[i] = 0f;
            }

            if (flags.IsCreated)
            {
                for (int i = 0; i < flags.Length; i++)
                    flags[i] = 0;
            }

            if (map.IsCreated)
            {
                for (int i = 0; i < map.Length; i++)
                    map[i] = default;
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

            trackers = _assetTrackerVaultHandle.Resolve(_dataVault);
            ttl = _assetTtlVaultHandle.Resolve(_dataVault);
            flags = _assetTrackerFlagsVaultHandle.Resolve(_dataVault);
            handleMap = _assetHandleMapVaultHandle.Resolve(_dataVault);
            return trackers.IsCreated &&
                   ttl.IsCreated &&
                   flags.IsCreated &&
                   handleMap.IsCreated;
        }

        private bool TryResolveCacheProfileView(out NativeArray<AssetCacheProfileDTO> profiles)
        {
            profiles = default;
            if (!TryResolveHeapSanitizerVaultBuffers())
                return false;

            profiles = _cacheProfileVaultHandle.Resolve(_dataVault);
            return profiles.IsCreated;
        }

        private bool TryResolveTelemetryView(out NativeArray<AssetHeapTelemetryEntry> telemetry)
        {
            telemetry = default;
            if (!TryResolveHeapSanitizerVaultBuffers())
                return false;

            telemetry = _heapTelemetryVaultHandle.Resolve(_dataVault);
            return telemetry.IsCreated;
        }

        private bool TryLockTtlEvaluationVaultBuffers()
        {
            if (_dataVault == null)
                return false;

            bool lockedTrackers = _dataVault.TryLockBuffer(BufferID.AddressableHeapTrackers, SystemID.WorldStreaming);
            bool lockedTtl = lockedTrackers &&
                             _dataVault.TryLockBuffer(BufferID.AddressableHeapTimeToLive, SystemID.WorldStreaming);
            bool lockedFlags = lockedTtl &&
                               _dataVault.TryLockBuffer(BufferID.AddressableHeapTrackerFlags, SystemID.WorldStreaming);
            if (lockedTrackers && lockedTtl && lockedFlags)
            {
                _ttlEvaluationVaultLocksHeld = true;
                return true;
            }

            if (lockedFlags)
                _dataVault.TryUnlockBuffer(BufferID.AddressableHeapTrackerFlags, SystemID.WorldStreaming);
            if (lockedTtl)
                _dataVault.TryUnlockBuffer(BufferID.AddressableHeapTimeToLive, SystemID.WorldStreaming);
            if (lockedTrackers)
                _dataVault.TryUnlockBuffer(BufferID.AddressableHeapTrackers, SystemID.WorldStreaming);
            return false;
        }

        private void ReleaseTtlEvaluationVaultLocks()
        {
            if (!_ttlEvaluationVaultLocksHeld || _dataVault == null)
            {
                _ttlEvaluationVaultLocksHeld = false;
                return;
            }

            _dataVault.TryUnlockBuffer(BufferID.AddressableHeapTrackerFlags, SystemID.WorldStreaming);
            _dataVault.TryUnlockBuffer(BufferID.AddressableHeapTimeToLive, SystemID.WorldStreaming);
            _dataVault.TryUnlockBuffer(BufferID.AddressableHeapTrackers, SystemID.WorldStreaming);
            _ttlEvaluationVaultLocksHeld = false;
        }

        private void CompleteTtlEvaluationForTeardown()
        {
            if (!_ttlEvaluationScheduled)
                return;

            _ttlEvaluationHandle.Complete();
            _ttlEvaluationScheduled = false;
            _ttlEvaluationResultsPending = false;
            _ttlEvaluationVramPanic = false;
            ReleaseTtlEvaluationVaultLocks();
        }

        private bool TryPrepareTrackerMutation()
        {
            if (!_ttlEvaluationScheduled)
                return true;

            if (!_ttlEvaluationHandle.IsCompleted)
            {
                _nativeRefSyncRequired = true;
                _deferredTrackerMutationCount++;
                return false;
            }

            _ttlEvaluationHandle.Complete();
            _ttlEvaluationScheduled = false;
            _ttlEvaluationResultsPending = true;
            ReleaseTtlEvaluationVaultLocks();
            return true;
        }

        private bool IsTrackerMutationBlockedByScheduledJob()
        {
            return _ttlEvaluationScheduled && !_ttlEvaluationHandle.IsCompleted;
        }

#if UNITY_ADDRESSABLES_EXIST
        private bool TryAcquireTrackedHandleFromManagedRecord(
            uint assetHash,
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
            if (!_registry.TryGetValue(assetHash, out AssetRecord record) ||
                !record.HasAddressableHandle ||
                !record.AddressableHandle.IsValid())
            {
                return false;
            }

            record.RefCount = math.max(0, record.RefCount) + 1;
            record.Owner = owner != null ? owner : record.Owner;
            record.Priority = priority;
            record.ResidencyKind = residencyKind;
            record.PendingRelease = false;
            record.LastAccessFrame = _frameSequence;
            record.IsChunkAsset = isChunkAsset;

            if (sizeBytes > 0L)
                ReplaceTrackedSize(ref record, sizeBytes);

            _registry[assetHash] = record;
            _nativeRefSyncRequired = true;
            handle = record.AddressableHandle.Convert<GameObject>();
            cacheHit = true;
            _cacheHitCount++;
            return true;
        }

        private bool TryAcquireTrackedHandle(
            uint assetHash,
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

            int refCount = AssetTrackerAtomic.Increment(trackers, slot);
            SetNativeRefCount(assetHash, slot, refCount, trackers, handleMap);
            byte flags = trackerFlags[slot];
            flags = (byte)(flags & ~(AssetHandleFlags.PendingTtl | AssetHandleFlags.Releasable));
            flags = rawHandle.IsDone
                ? (byte)(flags & ~AssetHandleFlags.Loading)
                : (byte)(flags | AssetHandleFlags.Loading);
            trackerFlags[slot] = flags;
            ttl[slot] = 0f;

            if (_registry.TryGetValue(assetHash, out AssetRecord record))
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

                _registry[assetHash] = record;
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

            RemoveHandleMapEntry(handleMap, assetHash);
            bool sharedBundle = bundlePrefixHash != 0u && CountActiveBundlePrefix(handleMap, bundlePrefixHash) > 0;
            if (sharedBundle)
                MarkBundlePrefixShared(handleMap, trackerFlags, bundlePrefixHash, true);
            UpsertHandleMapEntry(handleMap, assetHash, slot, bundlePrefixHash, 1, sharedBundle);
            _addressableHandlePool[slot] = handle;
            _addressableHandleHashes[slot] = assetHash;
            _addressableBundlePrefixHashes[slot] = bundlePrefixHash;
            trackers[slot] = new AssetTrackerDTO
            {
                AssetHash = assetHash,
                ReferenceCount = 1,
                HandlePointer = unchecked((ulong)(slot + 1))
            };
            ttl[slot] = 0f;

            byte flags = (byte)(AssetHandleFlags.Active | AssetHandleFlags.Loading);
            if (sharedBundle)
                flags = (byte)(flags | AssetHandleFlags.BundleShared);
            trackerFlags[slot] = flags;

            if (_registry.TryGetValue(assetHash, out AssetRecord record))
            {
                ReplaceTrackedSize(ref record, sizeBytes);
            }
            else
            {
                record = new AssetRecord
                {
                    Key = assetHash,
                    AssetGuid = null,
                    SizeBytes = ClampNonNegative(sizeBytes)
                };
                TrackedResidentBytes += record.SizeBytes;
            }

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
            record.ActiveRequestId = 0;
            record.NextRetryTime = 0f;
            _registry[assetHash] = record;
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
            int requested = Mathf.Clamp(handleCapacity * 2, 2, MaxAddressableHandleMapCapacity);
            int capacity = 1;
            while (capacity < requested && capacity < MaxAddressableHandleMapCapacity)
                capacity <<= 1;
            return capacity;
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
            slot = entry.Slot;
            return slot >= 0 && entry.AssetHash == assetHash;
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
                    if (entry.AssetHash == assetHash)
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
                    if (entry.AssetHash == assetHash)
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

        private static bool UpsertHandleMapEntry(
            NativeArray<AssetHandleMapEntryDTO> handleMap,
            uint assetHash,
            int slot,
            uint bundlePrefixHash,
            int refCount,
            bool sharedBundle)
        {
            if (!TryFindHandleMapInsertIndex(assetHash, handleMap, out int index))
                return false;

            uint generation = 1u;
            AssetHandleMapEntryDTO current = handleMap[index];
            if ((current.Flags & AssetHandleMapFlags.Occupied) != 0u && current.AssetHash == assetHash)
                generation = current.Generation + 1u;

            uint flags = AssetHandleMapFlags.Occupied;
            if (sharedBundle)
                flags |= AssetHandleMapFlags.BundleShared;

            handleMap[index] = new AssetHandleMapEntryDTO
            {
                HandlePointer = unchecked((ulong)(slot + 1)),
                AssetHash = assetHash,
                BundlePrefixHash = bundlePrefixHash,
                Slot = slot,
                RefCount = refCount,
                Flags = flags,
                Generation = generation,
                _pad0 = 0UL,
                _pad1 = 0UL,
                _pad2 = 0UL,
                _pad3 = 0UL
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
                AssetHandleMapEntryDTO entry = handleMap[index];
                entry.Slot = slot;
                entry.RefCount = refCount;
                entry.HandlePointer = unchecked((ulong)(slot + 1));
                handleMap[index] = entry;
                return;
            }

            uint bundlePrefixHash = ResolveBundlePrefixHashForSlot(slot);
            bool sharedBundle = bundlePrefixHash != 0u && CountActiveBundlePrefix(handleMap, bundlePrefixHash) > 0;
            UpsertHandleMapEntry(handleMap, assetHash, slot, bundlePrefixHash, refCount, sharedBundle);
        }

        private static void RemoveHandleMapEntry(NativeArray<AssetHandleMapEntryDTO> handleMap, uint assetHash)
        {
            if (!TryFindHandleMapIndex(assetHash, handleMap, out int index))
                return;

            AssetHandleMapEntryDTO entry = handleMap[index];
            handleMap[index] = new AssetHandleMapEntryDTO
            {
                AssetHash = entry.AssetHash,
                Flags = AssetHandleMapFlags.Tombstone,
                Generation = entry.Generation + 1u
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
                    entry.BundlePrefixHash == bundlePrefixHash)
                {
                    count++;
                }
            }

            return count;
        }

        private static void MarkBundlePrefixShared(
            NativeArray<AssetHandleMapEntryDTO> handleMap,
            NativeArray<byte> trackerFlags,
            uint bundlePrefixHash,
            bool shared)
        {
            if (bundlePrefixHash == 0u || !handleMap.IsCreated)
                return;

            for (int i = 0; i < handleMap.Length; i++)
            {
                AssetHandleMapEntryDTO entry = handleMap[i];
                if ((entry.Flags & AssetHandleMapFlags.Occupied) == 0u ||
                    entry.BundlePrefixHash != bundlePrefixHash)
                {
                    continue;
                }

                entry.Flags = shared
                    ? entry.Flags | AssetHandleMapFlags.BundleShared
                    : entry.Flags & ~AssetHandleMapFlags.BundleShared;
                handleMap[i] = entry;

                int slot = entry.Slot;
                if (trackerFlags.IsCreated && (uint)slot < (uint)trackerFlags.Length)
                {
                    byte flags = trackerFlags[slot];
                    trackerFlags[slot] = shared
                        ? (byte)(flags | AssetHandleFlags.BundleShared)
                        : (byte)(flags & ~AssetHandleFlags.BundleShared);
                }
            }
        }

        private static void RecomputeBundlePrefixSharing(
            NativeArray<AssetHandleMapEntryDTO> handleMap,
            NativeArray<byte> trackerFlags,
            uint bundlePrefixHash)
        {
            if (bundlePrefixHash == 0u)
                return;

            MarkBundlePrefixShared(
                handleMap,
                trackerFlags,
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
            if (!TryPrepareTrackerMutation())
                return int.MinValue;

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

        private bool ArmNativeTtlRelease(uint assetHash, int slot)
        {
            if (!TryResolveTrackerViews(
                    out NativeArray<AssetTrackerDTO> trackers,
                    out NativeArray<float> ttl,
                    out NativeArray<byte> trackerFlags,
                    out _) ||
                (uint)slot >= (uint)trackers.Length)
            {
                return false;
            }

            byte flags = trackerFlags[slot];
            if ((flags & AssetHandleFlags.Active) == 0)
                return false;

            ttl[slot] = ResolveAdaptiveTtlSeconds(assetHash, flags);
            trackerFlags[slot] = (byte)((flags | AssetHandleFlags.PendingTtl) & ~AssetHandleFlags.Releasable);
            return true;
        }

        private void EvaluateAddressableTtlAndQueueReleases()
        {
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
                SyncNativeRefCountsFromRegistry(trackers, ttl, trackerFlags, handleMap);
                DrainTtlEvaluationResults(trackers, trackerFlags, vramPanic || scheduledUnderVramPanic);
            }
            else if (_nativeRefSyncRequired)
            {
                SyncNativeRefCountsFromRegistry(trackers, ttl, trackerFlags, handleMap);
            }

            if (_ttlEvaluationScheduled)
            {
                if (!_ttlEvaluationHandle.IsCompleted)
                {
                    _lastPendingTtlCount = CountPendingTtlReleases(trackerFlags);
                    return;
                }

                _ttlEvaluationHandle.Complete();
                _ttlEvaluationScheduled = false;
                _ttlEvaluationResultsPending = false;
                bool scheduledUnderVramPanic = _ttlEvaluationVramPanic;
                _ttlEvaluationVramPanic = false;
                ReleaseTtlEvaluationVaultLocks();
                SyncNativeRefCountsFromRegistry(trackers, ttl, trackerFlags, handleMap);
                DrainTtlEvaluationResults(trackers, trackerFlags, vramPanic || scheduledUnderVramPanic);
            }

            if (vramPanic)
            {
                ForceUnusedAddressableTtlsToZero(trackers, ttl, trackerFlags);
                DrainTtlEvaluationResults(trackers, trackerFlags, true);
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

                if (!_registry.TryGetValue(assetHash, out AssetRecord record))
                {
                    uint bundlePrefixHash = ResolveBundlePrefixHashForSlot(i);
                    RemoveHandleMapEntry(handleMap, assetHash);
#if UNITY_ADDRESSABLES_EXIST
                    ClearManagedAddressableSlotBestEffort(assetHash, default);
#endif
                    RecomputeBundlePrefixSharing(handleMap, trackerFlags, bundlePrefixHash);
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
                    ttl[i] = 0f;
                    trackerFlags[i] = (byte)(flags & ~(AssetHandleFlags.PendingTtl | AssetHandleFlags.Releasable));
                    continue;
                }

                if ((flags & AssetHandleFlags.Pinned) != 0 ||
                    (flags & AssetHandleFlags.PendingTtl) != 0)
                {
                    continue;
                }

                ttl[i] = ResolveAdaptiveTtlSeconds(assetHash, flags);
                trackerFlags[i] = (byte)((flags | AssetHandleFlags.PendingTtl) & ~AssetHandleFlags.Releasable);
            }
        }

        private void ScheduleAddressableTtlEvaluation()
        {
            if (_ttlEvaluationScheduled)
                return;

            if (!TryResolveTrackerViews(
                    out NativeArray<AssetTrackerDTO> trackers,
                    out NativeArray<float> ttl,
                    out NativeArray<byte> trackerFlags,
                    out _))
            {
                return;
            }

            if (!TryLockTtlEvaluationVaultBuffers())
                return;

            AssetTtlEvaluationJob job = new AssetTtlEvaluationJob
            {
                Trackers = trackers,
                TimeToLiveSeconds = ttl,
                Flags = trackerFlags,
                DeltaSeconds = ColdReleaseIntervalSeconds
            };
            _ttlEvaluationHandle = job.Schedule();
            _ttlEvaluationScheduled = true;
            _ttlEvaluationVramPanic = IsVramPanicReleaseFrame();
        }

        private void DrainTtlEvaluationResults(
            NativeArray<AssetTrackerDTO> trackers,
            NativeArray<byte> trackerFlags,
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

                if (QueueExpiredAddressableRelease(tracker.AssetHash, i, trackerFlags))
                {
                    if (vramPanic)
                        forced++;
                }
            }

            _lastPendingTtlCount = pending;
            if (forced > 0)
                _forcedVramReleaseCount += forced;
        }

        private static int CountPendingTtlReleases(NativeArray<byte> trackerFlags)
        {
            if (!trackerFlags.IsCreated)
                return 0;

            int pending = 0;
            for (int i = 0; i < trackerFlags.Length; i++)
            {
                byte flags = trackerFlags[i];
                if ((flags & AssetHandleFlags.Active) != 0 &&
                    (flags & AssetHandleFlags.PendingTtl) != 0)
                {
                    pending++;
                }
            }

            return pending;
        }

        private static void ForceUnusedAddressableTtlsToZero(
            NativeArray<AssetTrackerDTO> trackers,
            NativeArray<float> ttl,
            NativeArray<byte> trackerFlags)
        {
            if (!trackers.IsCreated)
                return;

            for (int i = 0; i < trackers.Length; i++)
            {
                byte flags = trackerFlags[i];
                if ((flags & AssetHandleFlags.Active) == 0 ||
                    (flags & AssetHandleFlags.Pinned) != 0)
                {
                    continue;
                }

                AssetTrackerDTO tracker = trackers[i];
                if (tracker.ReferenceCount == 0)
                {
                    ttl[i] = 0f;
                    trackerFlags[i] = (byte)(flags | AssetHandleFlags.PendingTtl | AssetHandleFlags.Releasable);
                }
            }
        }

        private bool QueueExpiredAddressableRelease(uint assetHash, int slot, NativeArray<byte> trackerFlags)
        {
            if (!_registry.TryGetValue(assetHash, out AssetRecord record))
                return false;

            if (record.RefCount > 0 || record.PendingRelease)
                return false;

            record.PendingRelease = true;
            _registry[assetHash] = record;
            _pendingRelease.Enqueue(assetHash);
            trackerFlags[slot] = (byte)(trackerFlags[slot] & ~(AssetHandleFlags.PendingTtl | AssetHandleFlags.Releasable));
            return true;
        }

        private float ResolveAdaptiveTtlSeconds(uint assetHash, byte flags)
        {
            float quality = math.saturate(ResolveGlobalQualityWeight());
            float normalizedQuality = math.saturate((quality - 0.3f) * math.rcp(0.7f));
            float polynomial = normalizedQuality * normalizedQuality * (3f - 2f * normalizedQuality);
            float curve = math.step(0.3f, quality) * polynomial;
            float highTtl = math.clamp(baseAddressableTtlSeconds, MinimumAdaptiveTtlSeconds, DefaultHighEndTtlSeconds);
            float ttl = math.lerp(MinimumAdaptiveTtlSeconds, highTtl, curve);

            if (TryFindCacheProfile(assetHash, out AssetCacheProfileDTO profile))
            {
                if (math.isfinite(profile.BaseTtlSeconds) && profile.BaseTtlSeconds > 0f)
                    ttl = profile.BaseTtlSeconds;
                if (math.isfinite(profile.BundleTtlMultiplier) && profile.BundleTtlMultiplier > 0f)
                    ttl *= profile.BundleTtlMultiplier;
            }

            if ((flags & AssetHandleFlags.BundleShared) != 0)
                ttl *= SharedBundleTtlMultiplier;

            return math.clamp(ttl, 0f, DefaultHighEndTtlSeconds * 4f);
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
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(quality) ? quality : 1f);
        }

        private float ResolveVramPressureFactor()
        {
            VRAMPressureMonitor pressure = _cachedVramPressure;
            if (pressure == null)
                return 0f;

            float value = pressure.VramPressureFactor;
            return math.saturate(math.isfinite(value) ? value : 0f);
        }

        private bool IsVramPanicReleaseFrame()
        {
            if (_externalVramPanicActive)
            {
                if (_externalVramPanicUntil <= 0f || Time.unscaledTime <= _externalVramPanicUntil)
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
                if (_explicitBlindFrameWindowUntil <= 0f || Time.unscaledTime <= _explicitBlindFrameWindowUntil)
                    return true;

                _explicitBlindFrameWindowActive = false;
                _explicitBlindFrameWindowUntil = 0f;
            }

            if (_mockScreenFadeToBlackActive)
            {
                if (_mockScreenFadeToBlackUntil <= 0f || Time.unscaledTime <= _mockScreenFadeToBlackUntil)
                    return true;

                _mockScreenFadeToBlackActive = false;
                _mockScreenFadeToBlackUntil = 0f;
            }

            SystemDispatcher dispatcher = _cachedDispatcher;
            return dispatcher != null && dispatcher.TimeSnapshot.UnscaledDeltaTime <= 0.0001d;
        }

        private bool ClearNativeHandleSlot(uint assetHash)
        {
            if (!TryPrepareTrackerMutation())
                return false;

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
                RecomputeBundlePrefixSharing(handleMap, trackerFlags, bundlePrefixHash);
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

        private void GenerateEmergencyMockProfiles()
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
            IDataVault vault = _dataVault != null ? _dataVault : GlobalRegistry.DataVault;
            if (vault == null)
                return false;

            if (handleCapacity <= 0)
                handleCapacity = Mathf.Clamp(maxTrackedAddressableHandles, 1, MaxTrackedAddressableCapacity);
            if (mapCapacity <= 0)
                mapCapacity = ResolveHandleMapCapacity(handleCapacity);

            bool newVaultHandles = !ReferenceEquals(_dataVault, vault) ||
                                   !_assetTrackerVaultHandle.IsCreated ||
                                   _assetTrackerVaultHandle.Length < handleCapacity ||
                                   !_assetTtlVaultHandle.IsCreated ||
                                   _assetTtlVaultHandle.Length < handleCapacity ||
                                   !_assetTrackerFlagsVaultHandle.IsCreated ||
                                   _assetTrackerFlagsVaultHandle.Length < handleCapacity ||
                                   !_assetHandleMapVaultHandle.IsCreated ||
                                   _assetHandleMapVaultHandle.Length < mapCapacity ||
                                   !_cacheProfileVaultHandle.IsCreated ||
                                   !_heapTelemetryVaultHandle.IsCreated;
            if (newVaultHandles)
            {
                _dataVault = vault;
                _assetTrackerVaultHandle = vault.GetBufferHandle<AssetTrackerDTO>(
                    BufferID.AddressableHeapTrackers,
                    handleCapacity,
                    SystemID.WorldStreaming,
                    NativeArrayOptions.UninitializedMemory);
                _assetTtlVaultHandle = vault.GetBufferHandle<float>(
                    BufferID.AddressableHeapTimeToLive,
                    handleCapacity,
                    SystemID.WorldStreaming,
                    NativeArrayOptions.UninitializedMemory);
                _assetTrackerFlagsVaultHandle = vault.GetBufferHandle<byte>(
                    BufferID.AddressableHeapTrackerFlags,
                    handleCapacity,
                    SystemID.WorldStreaming,
                    NativeArrayOptions.UninitializedMemory);
                _assetHandleMapVaultHandle = vault.GetBufferHandle<AssetHandleMapEntryDTO>(
                    BufferID.AddressableHeapHandleMap,
                    mapCapacity,
                    SystemID.WorldStreaming,
                    NativeArrayOptions.UninitializedMemory);
                _cacheProfileVaultHandle = vault.GetBufferHandle<AssetCacheProfileDTO>(
                    BufferID.AddressableHeapCacheProfiles,
                    CacheProfileCapacity,
                    SystemID.WorldStreaming,
                    NativeArrayOptions.UninitializedMemory);
                _heapTelemetryVaultHandle = vault.GetBufferHandle<AssetHeapTelemetryEntry>(
                    BufferID.AddressableHeapTelemetry,
                    HeapTelemetryCapacity,
                    SystemID.WorldStreaming,
                    NativeArrayOptions.UninitializedMemory);
            }

            return _assetTrackerVaultHandle.IsCreated &&
                   _assetTtlVaultHandle.IsCreated &&
                   _assetTrackerFlagsVaultHandle.IsCreated &&
                   _assetHandleMapVaultHandle.IsCreated &&
                   _cacheProfileVaultHandle.IsCreated &&
                   _heapTelemetryVaultHandle.IsCreated;
        }

        private bool TryCopyCacheProfilesFromVault()
        {
            if (!TryResolveCacheProfileView(out NativeArray<AssetCacheProfileDTO> profiles) ||
                !TryResolveHeapSanitizerVaultBuffers())
            {
                return false;
            }

            NativeArray<AssetCacheProfileDTO> vaultProfiles = _cacheProfileVaultHandle.Resolve(_dataVault);
            if (!vaultProfiles.IsCreated)
                return false;

            int count = math.min(profiles.Length, vaultProfiles.Length);
            bool hasAny = false;
            for (int i = 0; i < count; i++)
            {
                AssetCacheProfileDTO profile = vaultProfiles[i];
                profiles[i] = profile;
                if (profile.AssetHash != 0u)
                    hasAny = true;
            }

            for (int i = count; i < profiles.Length; i++)
                profiles[i] = default;

            return hasAny;
        }

        private void MirrorCacheProfilesToVault()
        {
            if (!TryResolveCacheProfileView(out NativeArray<AssetCacheProfileDTO> profiles) ||
                !TryResolveHeapSanitizerVaultBuffers())
            {
                return;
            }

            NativeArray<AssetCacheProfileDTO> vaultProfiles = _cacheProfileVaultHandle.Resolve(_dataVault);
            if (!vaultProfiles.IsCreated)
                return;

            int count = math.min(profiles.Length, vaultProfiles.Length);
            for (int i = 0; i < count; i++)
                vaultProfiles[i] = profiles[i];

            for (int i = count; i < vaultProfiles.Length; i++)
                vaultProfiles[i] = default;
        }

        private void EnsureFallbackImpostorMesh()
        {
            if (_fallbackImpostorMesh != null)
                return;

            _fallbackImpostorMesh = CreateFallbackCubeMesh();
        }

        private static Mesh CreateFallbackCubeMesh()
        {
            Mesh mesh = new Mesh
            {
                name = "__AddressablesFallbackImpostor_MESH",
                hideFlags = HideFlags.HideAndDontSave
            }; // COLD ALLOC: Mesh[1] - fallback impostor mesh for unresolved Addressables - owner: AssetLifecycleGovernor

            Vector3[] vertices =
            {
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f)
            };
            int[] indices =
            {
                0, 2, 1, 0, 3, 2,
                4, 5, 6, 4, 6, 7,
                0, 1, 5, 0, 5, 4,
                2, 3, 7, 2, 7, 6,
                0, 4, 7, 0, 7, 3,
                1, 2, 6, 1, 6, 5
            };
            mesh.vertices = vertices;
            mesh.triangles = indices;
            mesh.RecalculateBounds();
            return mesh;
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

        public bool SetHeapSanitizerPin(uint assetHash, bool pinned)
        {
            if (!TryPrepareTrackerMutation())
                return false;

            if (assetHash == 0u ||
                !TryResolveTrackerViews(
                    out _,
                    out _,
                    out NativeArray<byte> trackerFlags,
                    out NativeArray<AssetHandleMapEntryDTO> handleMap) ||
                !TryGetHandleSlot(assetHash, handleMap, out int slot) ||
                (uint)slot >= (uint)trackerFlags.Length)
            {
                return false;
            }

            byte flags = trackerFlags[slot];
            trackerFlags[slot] = pinned
                ? (byte)(flags | AssetHandleFlags.Pinned)
                : (byte)(flags & ~AssetHandleFlags.Pinned);
            return true;
        }

        public bool TryParseAssetCacheRulesCsv(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
                return false;

            EnsureNativeHandleStorage();
            string csv = File.ReadAllText(absolutePath);
            return TryParseAssetCacheRules(csv.AsSpan());
        }

        public bool TryParseAssetCacheRules(ReadOnlySpan<char> csv)
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

                ReadOnlySpan<char> line = csv.Slice(lineStart, cursor - lineStart).Trim();
                while (cursor < csv.Length && (csv[cursor] == '\n' || csv[cursor] == '\r'))
                    cursor++;

                if (line.Length == 0 || line[0] == '#')
                    continue;

                if (TryParseCacheProfileLine(line, out AssetCacheProfileDTO profile))
                    profiles[profileIndex++] = profile;
            }

            for (int i = profileIndex; i < profiles.Length; i++)
                profiles[i] = default;

            MirrorCacheProfilesToVault();
            return profileIndex > 0;
        }

        private static bool TryParseCacheProfileLine(ReadOnlySpan<char> line, out AssetCacheProfileDTO profile)
        {
            profile = default;
            ReadOnlySpan<char> key = NextCsvToken(ref line);
            ReadOnlySpan<char> ttl = NextCsvToken(ref line);
            ReadOnlySpan<char> multiplier = NextCsvToken(ref line);
            ReadOnlySpan<char> flags = NextCsvToken(ref line);

            if (key.Length == 0 || !TryParseAssetHash(key, out uint assetHash))
                return false;

            if (!float.TryParse(ttl, NumberStyles.Float, CultureInfo.InvariantCulture, out float ttlSeconds))
                ttlSeconds = DefaultHighEndTtlSeconds;

            if (!float.TryParse(multiplier, NumberStyles.Float, CultureInfo.InvariantCulture, out float ttlMultiplier))
                ttlMultiplier = 1f;

            uint parsedFlags = 0u;
            if (flags.Length > 0)
                uint.TryParse(flags, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedFlags);

            profile = new AssetCacheProfileDTO
            {
                AssetHash = assetHash,
                BaseTtlSeconds = math.clamp(ttlSeconds, 0f, DefaultHighEndTtlSeconds * 4f),
                BundleTtlMultiplier = math.clamp(ttlMultiplier, 0.1f, 8f),
                Flags = parsedFlags
            };
            return true;
        }

        private static ReadOnlySpan<char> NextCsvToken(ref ReadOnlySpan<char> line)
        {
            if (line.Length == 0)
                return ReadOnlySpan<char>.Empty;

            int comma = line.IndexOf(',');
            if (comma < 0)
            {
                ReadOnlySpan<char> result = line.Trim();
                line = ReadOnlySpan<char>.Empty;
                return result;
            }

            ReadOnlySpan<char> token = line.Slice(0, comma).Trim();
            line = line.Slice(comma + 1);
            return token;
        }

        private static bool TryParseAssetHash(ReadOnlySpan<char> token, out uint assetHash)
        {
            assetHash = 0u;
            if (token.Length > 2 && token[0] == '0' && (token[1] == 'x' || token[1] == 'X'))
                return uint.TryParse(token.Slice(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out assetHash);

            if (uint.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out assetHash))
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
                FrameIndex = unchecked((uint)Time.frameCount),
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
            DumpHeapTelemetryToFile("Dump_SHINOBU_67_Addressables.bin", telemetry);
        }

        private unsafe void DumpHeapTelemetryToFile(string fileName, NativeArray<AssetHeapTelemetryEntry> telemetry)
        {
            try
            {
                string root = Directory.GetCurrentDirectory();
                string directory = Path.Combine(root, "Docs", "AgentLogs");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, fileName);
                using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                Span<byte> header = stackalloc byte[16];
                BinaryPrimitives.WriteUInt64LittleEndian(header.Slice(0, 8), 0x484543544F4E3800UL);
                BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(8, 4), HeapTelemetryCapacity);
                BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(12, 4), unchecked((uint)UnsafeUtility.SizeOf<AssetHeapTelemetryEntry>()));
                stream.Write(header);

                int bytes = UnsafeUtility.SizeOf<AssetHeapTelemetryEntry>() * telemetry.Length;
                byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                stream.Write(new ReadOnlySpan<byte>(ptr, bytes));
            }
            catch (Exception)
            {
                // Fault-path dump failure cannot throw into gameplay teardown.
            }
        }

        private void TryRegister()
        {
            if (_registeredTick)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;
            if (!ReferenceEquals(GlobalRegistry.AssetLifecycle, this))
                return;

            _cachedDispatcher = GlobalRegistry.Dispatcher;
            _cachedVramPressure = GlobalRegistry.VRAMPressure;
            bool tickRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core);
            bool slowTickRegistered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Core);
            if (!tickRegistered || !slowTickRegistered)
            {
                if (tickRegistered)
                    GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
                if (slowTickRegistered)
                    GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
                return;
            }

            _registeredTick = true;
            _registeredSlowTick = true;
        }

        private bool TryRegisterService()
        {
            if (_registeredService)
                return true;
            if (!Application.isPlaying)
                return false;

            AssetLifecycleGovernor registered = GlobalRegistry.AssetLifecycle;
            if (registered != null && !ReferenceEquals(registered, this))
            {
                Destroy(gameObject);
                return false;
            }

            GlobalRegistry.RegisterAssetLifecycleRuntime(this);
            _registeredService = ReferenceEquals(GlobalRegistry.AssetLifecycle, this);
            return _registeredService;
        }

        private void TryUnregister()
        {
            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
                _registeredSlowTick = false;
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

        private void PumpRetries()
        {
            if (_registry.Count == 0)
                return;

            float now = Time.unscaledTime;
            _retryCandidates.Clear();

            Dictionary<uint, AssetRecord>.Enumerator enumerator = _registry.GetEnumerator();
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
            long elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            double elapsedMilliseconds = elapsedTicks * 1000d / Stopwatch.Frequency;
            if (elapsedMilliseconds <= ColdTickWarningMilliseconds)
                return;

            float now = Time.unscaledTime;
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
            if (!_registry.TryGetValue(key, out AssetRecord record))
                return;

            if (record.ActiveRequestId != 0)
                return;

            AssetLoadDispatcher dispatcher = GlobalRegistry.AssetLoadDispatcher;
            if (dispatcher == null)
                return;

            bool isDistantHlod = record.Priority == AssetPriorityTier.Tier5DistantHlod ||
                                 record.Priority == AssetPriorityTier.Tier6Speculative;

            if (!dispatcher.Enqueue(key, record.Priority, isDistantHlod, out int requestId))
                return;

            record.ActiveRequestId = requestId;
            record.NextRetryTime = 0f;
            _registry[key] = record;
        }

        private int DrainPendingReleaseQueue(int maxCount)
        {
            int drained = 0;
            while (_pendingRelease.Count > 0 && drained < maxCount)
            {
                uint key = _pendingRelease.Dequeue();
                drained++;

                if (!_registry.TryGetValue(key, out AssetRecord record))
                    continue;

                record.PendingRelease = false;
                _registry[key] = record;

                if (record.RefCount > 0)
                    continue;

                if (IsAddressableReleaseBlockedByBlindFrame(in record))
                {
                    record.PendingRelease = true;
                    _registry[key] = record;
                    _pendingRelease.Enqueue(key);
                    break;
                }

                if (!ExecuteReleaseFlow(key))
                {
                    if (_registry.TryGetValue(key, out record) && record.RefCount == 0)
                    {
                        record.PendingRelease = true;
                        _registry[key] = record;
                        _pendingRelease.Enqueue(key);
                        break;
                    }
                }
            }

            return drained;
        }

        private void EvaluateHardMemoryReaper(float now)
        {
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

                Addressables.Release(_hardReaperCleanBundleCacheHandle);
                _hardReaperCleanBundleCacheHandle = default;
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
                Addressables.Release(_hardReaperCleanBundleCacheHandle);
                _hardReaperCleanBundleCacheHandle = default;
            }
            else if (handle.IsValid())
            {
                Addressables.Release(handle);
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

        private void ReleaseHardReaperAsyncHandles()
        {
            if (_hardReaperUnloadOperation != null && _hardReaperUnloadCompletedCallback != null)
                _hardReaperUnloadOperation.completed -= _hardReaperUnloadCompletedCallback;

            _hardReaperUnloadOperation = null;
#if UNITY_ADDRESSABLES_EXIST
            if (_hardReaperCleanBundleCacheHandle.IsValid())
            {
                if (_hardReaperCleanBundleCacheCompletedCallback != null)
                    _hardReaperCleanBundleCacheHandle.Completed -= _hardReaperCleanBundleCacheCompletedCallback;

                Addressables.Release(_hardReaperCleanBundleCacheHandle);
                _hardReaperCleanBundleCacheHandle = default;
            }
#endif
            _hardReaperAsyncWindowActive = false;
            _hardReaperUnloadComplete = false;
            _hardReaperBundleCacheCleanComplete = false;
        }

        private static void SetHardReaperScannerInterferenceActive(bool active)
        {
            if (GlobalRegistry.UI is IScannerInterferenceUiSink scannerInterference)
                scannerInterference.SetScannerInterferenceActive(active);
        }

        private bool ExecuteReleaseFlow(uint key)
        {
            if (!_registry.TryGetValue(key, out AssetRecord record))
                return false;

            if (record.RefCount > 0)
                return false;

            bool nativeSlotCleared = ClearNativeHandleSlot(key);
            if (!nativeSlotCleared && IsTrackerMutationBlockedByScheduledJob())
            {
                record.PendingRelease = true;
                _registry[key] = record;
                return false;
            }

            if (!nativeSlotCleared)
            {
#if UNITY_ADDRESSABLES_EXIST
                ClearManagedAddressableSlotBestEffort(key, record.AddressableHandle);
#endif
                _lastLeakSuspectHash = key;
                _nativeRefSyncRequired = true;
            }

            AssetLoadDispatcher dispatcher = GlobalRegistry.AssetLoadDispatcher;
            if (dispatcher != null)
            {
                dispatcher.CancelByAssetKey(key);
                if (record.ActiveRequestId != 0)
                    dispatcher.AcknowledgeDispatchRequest(record.ActiveRequestId, false);
            }

            DisableOwnerPresentation(record.Owner);

#if UNITY_ADDRESSABLES_EXIST
            if (record.HasAddressableHandle && record.AddressableHandle.IsValid())
            {
                Addressables.Release(record.AddressableHandle);
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

            _registry.Remove(key);
            return true;
        }

#if UNITY_ADDRESSABLES_EXIST
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

        private bool TryExecuteOrDeferBlindFrameRelease(uint key, AssetRecord record)
        {
            if (IsAddressableReleaseBlockedByBlindFrame(in record))
            {
                if (!record.PendingRelease)
                {
                    record.PendingRelease = true;
                    _pendingRelease.Enqueue(key);
                }

                _nativeRefSyncRequired = true;
                _registry[key] = record;
                return false;
            }

            record.PendingRelease = false;
            _registry[key] = record;
            return ExecuteReleaseFlow(key);
        }

        private int ReleaseDistantChunkAddressables(int maxReleaseCount)
        {
            if (maxReleaseCount <= 0)
                return 0;

            ItemCatalog catalog = GlobalRegistry.PlayerInventory != null && GlobalRegistry.PlayerInventory.Inventory != null
                ? GlobalRegistry.PlayerInventory.Inventory.ItemCatalog
                : null;
            if (catalog == null)
                return 0;

            return catalog.EvictWorldPrefabsBeyondPlayerAup(
                DistantChunkReleaseDistanceMeters,
                maxReleaseCount);
        }

        private static void DisableOwnerPresentation(Component owner)
        {
            if (owner == null)
                return;

            if (owner is Renderer renderer)
            {
                renderer.enabled = false;
                return;
            }

            if (owner is AudioSource audioSource)
            {
                audioSource.enabled = false;
                return;
            }

            if (owner.TryGetComponent(out Renderer ownerRenderer))
                ownerRenderer.enabled = false;
        }

        private void ApplyFallbackMaterial(Component owner)
        {
            if (owner == null || _checkerboardMaterial == null)
                return;

            Renderer targetRenderer = owner as Renderer;
            if (targetRenderer == null && !owner.TryGetComponent(out targetRenderer))
                return;

            targetRenderer.sharedMaterial = _checkerboardMaterial;
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
            AssetRecord left = _registry[leftKey];
            AssetRecord right = _registry[rightKey];

            if (left.Priority != right.Priority)
                return (byte)right.Priority - (byte)left.Priority;

            if (left.LastAccessFrame < right.LastAccessFrame)
                return -1;

            if (left.LastAccessFrame > right.LastAccessFrame)
                return 1;

            return 0;
        }

        private void EnsureFallbackAssets()
        {
            if (_checkerboardTexture == null)
            {
                // COLD ALLOC: Color32[4] - checkerboard fallback pixel payload - owner: AssetLifecycleGovernor
                Color32[] pixels =
                {
                    new Color32(255, 0, 255, 255),
                    new Color32(16, 16, 16, 255),
                    new Color32(16, 16, 16, 255),
                    new Color32(255, 0, 255, 255)
                };

                _checkerboardTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false)
                {
                    name = "__AssetFailCheckerboard_TEX",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Repeat,
                    hideFlags = HideFlags.HideAndDontSave
                }; // COLD ALLOC: Texture2D[1] - persistent checkerboard fallback texture - owner: AssetLifecycleGovernor
                _checkerboardTexture.SetPixels32(pixels);
                _checkerboardTexture.Apply(false, true);
            }

            if (_checkerboardMaterial != null)
                return;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Texture");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            if (shader == null)
                return;

            _checkerboardMaterial = new Material(shader)
            {
                name = "__AssetFailCheckerboard_MAT",
                hideFlags = HideFlags.HideAndDontSave
            }; // COLD ALLOC: Material[1] - persistent checkerboard fallback material - owner: AssetLifecycleGovernor

            if (_checkerboardMaterial.HasProperty("_BaseMap"))
                _checkerboardMaterial.SetTexture("_BaseMap", _checkerboardTexture);
            if (_checkerboardMaterial.HasProperty("_MainTex"))
                _checkerboardMaterial.SetTexture("_MainTex", _checkerboardTexture);
            if (_checkerboardMaterial.HasProperty("_BaseColor"))
                _checkerboardMaterial.SetColor("_BaseColor", Color.white);
            if (_checkerboardMaterial.HasProperty("_Color"))
                _checkerboardMaterial.SetColor("_Color", Color.white);
        }

        private void DisposeFallbackAssets()
        {
            if (_checkerboardMaterial != null)
            {
                Destroy(_checkerboardMaterial);
                _checkerboardMaterial = null;
            }

            if (_checkerboardTexture != null)
            {
                Destroy(_checkerboardTexture);
                _checkerboardTexture = null;
            }
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
            if (!_registry.TryGetValue(key, out AssetRecord existing))
                return true;

            if (MatchesIdentity(existing, assetGuid, address))
                return true;

            uint saltedKey = key ^ CollisionSalt;
            if (!_registry.TryGetValue(saltedKey, out existing))
            {
                key = saltedKey;
                return true;
            }

            if (MatchesIdentity(existing, assetGuid, address))
            {
                key = saltedKey;
                return true;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[AssetLifecycleGovernor] Asset key collision.");
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
