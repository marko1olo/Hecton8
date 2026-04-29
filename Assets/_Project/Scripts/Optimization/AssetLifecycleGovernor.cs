using System.Collections.Generic;
using System.Text;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Optimization
{
    /// <summary>
    /// Global asset residency registry with deterministic ref-counting and deferred release draining.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-8012)]
    public sealed class AssetLifecycleGovernor : MonoBehaviour, ITickable, IUpdatable
    {
        private const uint CollisionSalt = 0xDEADBEEF;
        private const float NativeHeapOverheadFactor = 1.15f;
        private static readonly float[] _retryBackoffSeconds = { 5f, 15f, 60f };

        private static AssetLifecycleGovernor _instance;

        [Header("Asset Registry")]
        [Tooltip("Pre-sized residency registry capacity. This is cold-path storage only.")]
        [SerializeField] private int maxRegistryCapacity = 512;

        [Tooltip("Maximum deferred releases drained per frame before the gameplay handoff.")]
        [SerializeField] private int maxDeferredReleasesPerFrame = 8;

        private bool _registeredTick;
        private long _frameSequence;
        private Texture2D _checkerboardTexture;
        private Material _checkerboardMaterial;

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
        private readonly StringBuilder _logBuilder = new StringBuilder(512);
#endif

        internal static AssetLifecycleGovernor Instance => _instance;
        internal long TrackedResidentBytes { get; private set; }
        internal long NativeHeapEstimateBytes => (long)(TrackedResidentBytes * NativeHeapOverheadFactor);
        internal int PendingReleaseCount => _pendingRelease.Count;
        internal Material CheckerboardMaterial => _checkerboardMaterial;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _registry.EnsureCapacity(Mathf.Max(1, maxRegistryCapacity));
            EnsureFallbackAssets();
        }

        private void OnEnable()
        {
            TryRegister();
        }

        private void Start()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
        }

        private void OnDestroy()
        {
            TryUnregister();
            DisposeFallbackAssets();
            _registry.Clear();
            _pendingRelease.Clear();
            _evictionCandidates.Clear();
            _retryCandidates.Clear();
            TrackedResidentBytes = 0L;

            if (_instance == this)
                _instance = null;
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            _frameSequence++;
            DrainPendingReleaseQueue(maxDeferredReleasesPerFrame);
            PumpRetries();
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
                RetryCount = 0,
                BiomeId = biomeId,
                LodLevel = lodLevel,
                LastAccessFrame = _frameSequence,
                SizeBytes = ClampNonNegative(sizeBytes),
                ActiveRequestId = 0,
                NextRetryTime = 0f
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
                AssetLoadDispatcher dispatcher = AssetLoadDispatcher.Instance;
                if (dispatcher != null)
                    dispatcher.Complete(record.ActiveRequestId, true);

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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError($"[AssetLifecycleGovernor] Double release detected for asset key {key}.");
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

        internal void MarkLoadFailed(uint key, string error)
        {
            if (!_registry.TryGetValue(key, out AssetRecord record))
                return;

            if (record.ActiveRequestId != 0)
            {
                AssetLoadDispatcher dispatcher = AssetLoadDispatcher.Instance;
                if (dispatcher != null)
                    dispatcher.Complete(record.ActiveRequestId, false);

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
            _logBuilder.Clear();
            _logBuilder.Append("[AssetLifecycleGovernor] ASSET_FAIL key=")
                .Append(key)
                .Append(" error=")
                .Append(error);
            Debug.LogError(_logBuilder.ToString(), this);
#endif
        }

        private void TryRegister()
        {
            if (_registeredTick)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Core);
            _registeredTick = true;
        }

        private void TryUnregister()
        {
            if (!_registeredTick)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            _registeredTick = false;
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

        private void QueueAsyncDispatch(uint key)
        {
            if (!_registry.TryGetValue(key, out AssetRecord record))
                return;

            if (record.ActiveRequestId != 0)
                return;

            AssetLoadDispatcher dispatcher = AssetLoadDispatcher.Instance;
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

        private bool ExecuteReleaseFlow(uint key)
        {
            if (!_registry.TryGetValue(key, out AssetRecord record))
                return false;

            if (record.RefCount > 0)
                return false;

            AssetLoadDispatcher dispatcher = AssetLoadDispatcher.Instance;
            if (dispatcher != null)
            {
                dispatcher.CancelByAssetKey(key);
                if (record.ActiveRequestId != 0)
                    dispatcher.Complete(record.ActiveRequestId, false);
            }

            DisableOwnerPresentation(record.Owner);

            if (record.OwnsAssetInstance && record.Asset != null && !ReferenceEquals(record.Asset, _checkerboardMaterial))
                Destroy(record.Asset);

            TrackedResidentBytes -= record.SizeBytes;
            if (TrackedResidentBytes < 0L)
                TrackedResidentBytes = 0L;

            _registry.Remove(key);
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
            Debug.LogError($"[AssetLifecycleGovernor] Asset key collision for guid='{assetGuid}' address='{address}'.");
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
