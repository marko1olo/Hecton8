using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.SaveSystem;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;
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
        private const float DistantChunkReleaseDistanceSq = DistantChunkReleaseDistanceMeters * DistantChunkReleaseDistanceMeters;
        private const float HardReaperIntervalSeconds = 600f;
        private const float HardReaperTravelDistanceMeters = 3000f;
        private const float HardReaperTravelDistanceSq = HardReaperTravelDistanceMeters * HardReaperTravelDistanceMeters;
        private const float HardReaperGlitchDurationSeconds = 0.5f;
        private const int MaxColdDistantChunkReleases = 8;
        private const int MaxHardReaperEvictions = 64;
        private const double ColdTickWarningMilliseconds = 0.2d;
        private const float ColdTickWarningCooldownSeconds = 5f;
        private static readonly uint _AssetLifecycleContextHash = unchecked((uint)Hecton.Localization.LocHash.Compute("AssetLifecycleGovernor"));
        private static readonly uint _ColdTickOverBudgetWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("AssetLifecycleGovernor.ColdTickOverBudget"));
        private static readonly uint _DoubleReleaseWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("AssetLifecycleGovernor.DoubleRelease"));
        private static readonly uint _HardReaperSweepWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("AssetLifecycleGovernor.HardReaperSweep"));
        private static readonly float[] _retryBackoffSeconds = { 5f, 15f, 60f };

        [Header("Asset Registry")]
        [Tooltip("Pre-sized residency registry capacity. This is cold-path storage only.")]
        [SerializeField] private int maxRegistryCapacity = 512;

        [Tooltip("Maximum deferred releases drained per frame before the gameplay handoff.")]
        [SerializeField] private int maxDeferredReleasesPerFrame = 8;

        private bool _registeredTick;
        private bool _registeredSlowTick;
        private bool _registeredService;
        private long _frameSequence;
        private float _nextColdReleaseTime;
        private float _nextColdTickWarningTime;
        private float _nextHardReaperTime;
        private bool _hasHardReaperAnchor;
        private AbsoluteUniversePosition _lastHardReaperAup;
        private Texture2D _checkerboardTexture;
        private Material _checkerboardMaterial;
        private AsyncOperation _hardReaperUnloadOperation;
        private System.Action<AsyncOperation> _hardReaperUnloadCompletedCallback;
        private bool _hardReaperAsyncWindowActive;
        private bool _hardReaperUnloadComplete;
        private bool _hardReaperBundleCacheCleanComplete;
#if UNITY_ADDRESSABLES_EXIST
        private AsyncOperationHandle<bool> _hardReaperCleanBundleCacheHandle;
        private System.Action<AsyncOperationHandle<bool>> _hardReaperCleanBundleCacheCompletedCallback;
#endif

        // COLD ALLOC: Dictionary<uint, AssetRecord>[512] - global asset residency registry - owner: AssetLifecycleGovernor
        private readonly Dictionary<uint, AssetRecord> _registry = new Dictionary<uint, AssetRecord>(512);
        // COLD ALLOC: Queue<uint>[128] - pending release queue drained on the next frame - owner: AssetLifecycleGovernor
        private readonly Queue<uint> _pendingRelease = new Queue<uint>(128);
        // COLD ALLOC: List<uint>[16] - eviction candidate scratch buffer - owner: AssetLifecycleGovernor
        private readonly List<uint> _evictionCandidates = new List<uint>(16);
        // COLD ALLOC: List<uint>[16] - retry candidate scratch buffer - owner: AssetLifecycleGovernor
        private readonly List<uint> _retryCandidates = new List<uint>(16);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // COLD ALLOC: StringBuilder[512] - throttled diagnostics builder - owner: AssetLifecycleGovernor
#endif

        internal long TrackedResidentBytes { get; private set; }
        internal long NativeHeapEstimateBytes => (long)(TrackedResidentBytes * NativeHeapOverheadFactor);
        internal int PendingReleaseCount => _pendingRelease.Count;
        internal Material CheckerboardMaterial => _checkerboardMaterial;

        private void Awake()
        {
            _registry.EnsureCapacity(Mathf.Max(1, maxRegistryCapacity));
            _nextHardReaperTime = Time.unscaledTime + HardReaperIntervalSeconds;
            _hardReaperUnloadCompletedCallback = HandleHardReaperUnloadCompleted;
#if UNITY_ADDRESSABLES_EXIST
            _hardReaperCleanBundleCacheCompletedCallback = HandleHardReaperCleanBundleCacheCompleted;
#endif
            EnsureFallbackAssets();
        }

        private void OnEnable()
        {
            if (TryRegisterService())
                TryRegister();
        }

        private void Start()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            SetHardReaperScannerInterferenceActive(false);
            TryUnregister();
            TryUnregisterService();
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterService();
            DisposeFallbackAssets();
            _registry.Clear();
            _pendingRelease.Clear();
            _evictionCandidates.Clear();
            _retryCandidates.Clear();
            TrackedResidentBytes = 0L;
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
                DrainPendingReleaseQueue(maxDeferredReleasesPerFrame);
                PumpRetries();
                ReleaseDistantChunkAddressables(MaxColdDistantChunkReleases);
                EvaluateHardMemoryReaper(now);
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
                HasAbsoluteUniversePosition = false,
                RetryCount = 0,
                BiomeId = biomeId,
                LodLevel = lodLevel,
                LastAccessFrame = _frameSequence,
                SizeBytes = ClampNonNegative(sizeBytes),
                ActiveRequestId = 0,
                NextRetryTime = 0f,
                AbsoluteUniverseAup = default,
                AbsoluteUniversePosition = Vector3.zero
            };

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
            _registry[key] = record;
        }

#if UNITY_ADDRESSABLES_EXIST
        internal void MarkAddressableLoaded(
            uint key,
            AsyncOperationHandle handle,
            Object asset,
            long sizeBytes,
            Vector3 absoluteUniversePosition,
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
            record.HasAbsoluteUniversePosition = true;
            record.AbsoluteUniversePosition = absoluteUniversePosition;
            record.AbsoluteUniverseAup = AbsoluteUniversePosition.FromAbsolutePosition(new double3(
                absoluteUniversePosition.x,
                absoluteUniversePosition.y,
                absoluteUniversePosition.z));
            record.NextRetryTime = 0f;
            record.RetryCount = 0;
            record.LastAccessFrame = _frameSequence;
            _registry[key] = record;
        }
#endif

        internal void MarkChunkResidency(uint key, Vector3 absoluteUniversePosition)
        {
            if (!_registry.TryGetValue(key, out AssetRecord record))
                return;

            record.IsChunkAsset = true;
            record.HasAbsoluteUniversePosition = true;
            record.AbsoluteUniversePosition = absoluteUniversePosition;
            record.AbsoluteUniverseAup = AbsoluteUniversePosition.FromAbsolutePosition(new double3(
                absoluteUniversePosition.x,
                absoluteUniversePosition.y,
                absoluteUniversePosition.z));
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

            record.RefCount--;
            if (record.RefCount < 0)
            {
                record.RefCount = 0;
                GlobalTelemetryBus.PublishPerformanceWarning(_DoubleReleaseWarningHash, _AssetLifecycleContextHash, key);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[AssetLifecycleGovernor] Double release detected.", this);
#endif
            }

            if (record.RefCount == 0 && !record.PendingRelease)
            {
                record.PendingRelease = true;
                _pendingRelease.Enqueue(key);
            }

            _registry[key] = record;
        }

        internal void ForceDrainPendingReleaseQueue()
        {
            DrainPendingReleaseQueue(int.MaxValue);
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
                if (ExecuteReleaseFlow(_evictionCandidates[i]))
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

        private void TryRegister()
        {
            if (_registeredTick)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;
            if (!ReferenceEquals(GlobalRegistry.AssetLifecycle, this))
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Core);
            _registeredTick = GlobalRegistry.Updatables.Contains(this);

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Core);
            _registeredSlowTick = GlobalRegistry.SlowTickables.Contains(this);
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
            while (enumerator.MoveNext())
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

        private void DrainPendingReleaseQueue(int maxCount)
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

                ExecuteReleaseFlow(key);
            }
        }

        private void EvaluateHardMemoryReaper(float now)
        {
            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                return;

            if (!_hasHardReaperAnchor)
            {
                _lastHardReaperAup = playerAup;
                _hasHardReaperAnchor = true;
                _nextHardReaperTime = now + HardReaperIntervalSeconds;
                return;
            }

            double travelDistanceSq = AbsoluteUniversePosition.DistanceSq(in playerAup, in _lastHardReaperAup);
            if (now < _nextHardReaperTime && travelDistanceSq < HardReaperTravelDistanceSq)
                return;

            _lastHardReaperAup = playerAup;
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
            _hardReaperUnloadOperation = Resources.UnloadUnusedAssets();
            if (_hardReaperUnloadOperation != null && _hardReaperUnloadCompletedCallback != null)
                _hardReaperUnloadOperation.completed += _hardReaperUnloadCompletedCallback;
            else
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

        private int ReleaseDistantChunkAddressables(int maxReleaseCount)
        {
            if (maxReleaseCount <= 0)
                return 0;

            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                return 0;

            _evictionCandidates.Clear();

            Dictionary<uint, AssetRecord>.Enumerator enumerator = _registry.GetEnumerator();
            while (enumerator.MoveNext() && _evictionCandidates.Count < maxReleaseCount)
            {
                AssetRecord record = enumerator.Current.Value;
                if (!record.IsChunkAsset || !record.HasAbsoluteUniversePosition)
                    continue;

#if UNITY_ADDRESSABLES_EXIST
                if (!record.HasAddressableHandle || !record.AddressableHandle.IsValid())
                    continue;
#else
                continue;
#endif

                double distanceSq = AbsoluteUniversePosition.DistanceSq(in record.AbsoluteUniverseAup, in playerAup);
                if (distanceSq <= DistantChunkReleaseDistanceSq)
                    continue;

                _evictionCandidates.Add(record.Key);
            }

            int releaseCount = 0;
            for (int i = 0; i < _evictionCandidates.Count; i++)
            {
                uint key = _evictionCandidates[i];
                if (!_registry.TryGetValue(key, out AssetRecord record))
                    continue;

                record.RefCount = 0;
                record.PendingRelease = false;
                _registry[key] = record;

                if (ExecuteReleaseFlow(key))
                    releaseCount++;
            }

            _evictionCandidates.Clear();

            ItemCatalog catalog = GlobalRegistry.PlayerInventory != null && GlobalRegistry.PlayerInventory.Inventory != null
                ? GlobalRegistry.PlayerInventory.Inventory.ItemCatalog
                : null;
            if (catalog != null && releaseCount < maxReleaseCount)
            {
                releaseCount += catalog.EvictWorldPrefabsBeyondPlayerAup(
                    DistantChunkReleaseDistanceMeters,
                    maxReleaseCount - releaseCount);
            }

            return releaseCount;
        }

        private static bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            playerAup = default;
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            Transform playerTransform = playerContext != null ? playerContext.PlayerTransform : null;
            if (playerTransform == null)
                return false;

            playerAup = AbsoluteUniversePosition.FromRuntimePosition(playerTransform.position);
            return true;
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
