using Hecton8.Core;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4300)]
    public sealed class BiomeSamplerCache : MonoBehaviour, ISlowTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener
    {
        internal static BiomeSamplerCache ActiveRuntimeInstance { get; private set; }
        internal static event System.Action<BiomeSamplerCache> ActiveRuntimeInstanceChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveRuntimeForSubsystemRegistration()
        {
            ActiveRuntimeInstance = null;
            ActiveRuntimeInstanceChanged = null;
        }

        public struct CachedSample
        {
            public Vector3 position;
            public float terrainHeight;
            public int biomeIndex;
            public byte hasHeight;
            public byte hasBiome;
            public byte isValid;
        }

        [Header("Cache Shape")]
        [SerializeField] private MapMagicBridge mapMagicBridge;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private float cellSize = 48f;
        [SerializeField] private int radiusCells = 3;
        [SerializeField] private float rebuildDistance = 24f;

        [Header("Diagnostics")]
        [SerializeField] private bool _debugBridgeReady;
        [SerializeField] private bool _debugPlayerReady;
        [SerializeField] private bool _debugCacheReady;
        [SerializeField] private int _debugSampleCount;
        [SerializeField] private int _debugLastCenterBiome = -1;
        [SerializeField] private Vector3 _debugLastCenterPosition;

        private CachedSample[] _samples;
        private int _gridWidth;
        private int _sampleCount;
        private bool _registeredToTickManager;
        private bool _hotSwapListenerRegistered;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private Vector3 _lastCenterPosition;
        private bool _hasLastCenterPosition;

        public bool IsReady => _debugCacheReady;
        public int SampleCount => _sampleCount;

        private void Awake()
        {
            PublishActiveRuntimeInstance();
            CacheRuntimeReferencesCold();
            EnsureStorageCold();
            UpdateDiagnostics();
        }

        private void OnEnable()
        {
            CacheRuntimeReferencesCold();
            EnsureStorageCold();
            HectonFloatingOrigin.RegisterListener(this);
            TryRegisterHotSwapListener();
            TryRegister();
        }

        private void Start()
        {
            TryRegisterHotSwapListener();
            TryRegister();

            RebuildCache(force: true);
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
            HectonFloatingOrigin.UnregisterListener(this);
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
            HectonFloatingOrigin.UnregisterListener(this);

            if (ActiveRuntimeInstance == this)
                ClearActiveRuntimeInstance();
        }

        private void PublishActiveRuntimeInstance()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                return;

            ActiveRuntimeInstance = this;
            ActiveRuntimeInstanceChanged?.Invoke(this);
        }

        private void ClearActiveRuntimeInstance()
        {
            ActiveRuntimeInstance = null;
            ActiveRuntimeInstanceChanged?.Invoke(null);
        }

        private void TryRegister()
        {
            if (_registeredToTickManager || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registeredToTickManager = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (!_registeredToTickManager)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registeredToTickManager = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    if (currentService == null)
                    {
                        _registeredToTickManager = false;
                        return;
                    }

                    if (isActiveAndEnabled)
                    {
                        TryUnregister();
                        TryRegister();
                    }
                    break;
                case GlobalRegistryServiceSlot.MapMagicRuntime:
                    if (previousService != null && ReferenceEquals(mapMagicBridge, previousService))
                        mapMagicBridge = null;

                    if (currentService is MapMagicBridge currentMapMagic)
                        mapMagicBridge = currentMapMagic;
                    break;
                case GlobalRegistryServiceSlot.Player:
                    IPlayerRuntimeContext previousContext = previousService as IPlayerRuntimeContext;
                    if (previousContext != null && ReferenceEquals(playerTransform, previousContext.PlayerTransform))
                        playerTransform = null;

                    _cachedPlayerContext = currentService as IPlayerRuntimeContext;
                    RefreshRuntimeReferencesFromCachedContext();
                    break;
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        public void SlowTick()
        {
            RebuildCache(force: false);
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (!isActiveAndEnabled || shiftData.ShiftOffset.sqrMagnitude <= 0.0001f)
                return;

            ApplyRuntimeOffsetToCachedState(-shiftData.ShiftOffset);
        }

        public bool TryGetCachedSample(Vector3 position, out CachedSample sample)
        {
            sample = default;

            if (_samples == null || _sampleCount <= 0 || !_debugCacheReady)
                return false;

            int index = FindNearestSampleIndex(position, cellSize * 0.75f);
            if (index < 0)
                return false;

            sample = _samples[index];
            return sample.isValid != 0;
        }

        public bool TryGetNearestSample(Vector3 position, float maxDistance, out CachedSample sample)
        {
            sample = default;

            if (_samples == null || _sampleCount <= 0 || !_debugCacheReady)
                return false;

            int index = FindNearestSampleIndex(position, maxDistance);
            if (index < 0)
                return false;

            sample = _samples[index];
            return sample.isValid != 0;
        }

        private void RebuildCache(bool force)
        {
            RefreshRuntimeReferencesFromCachedContext();
            if (!HasStorageForCurrentShape())
            {
                _sampleCount = 0;
                _debugCacheReady = false;
                UpdateDiagnostics();
                return;
            }

            if (mapMagicBridge == null || playerTransform == null)
            {
                _debugCacheReady = false;
                UpdateDiagnostics();
                return;
            }

            Vector3 center = playerTransform.position;

            if (!force && _hasLastCenterPosition)
            {
                Vector3 delta = center - _lastCenterPosition;
                delta.y = 0f;
                if (delta.sqrMagnitude < rebuildDistance * rebuildDistance)
                    return;
            }

            int writeIndex = 0;
            int width = _gridWidth;
            float step = cellSize;

            for (int z = -radiusCells; z <= radiusCells; z++)
            {
                for (int x = -radiusCells; x <= radiusCells; x++)
                {
                    Vector3 samplePosition = new Vector3(
                        center.x + x * step,
                        center.y,
                        center.z + z * step);

                    CachedSample sample = default;
                    sample.position = samplePosition;
                    sample.hasHeight = mapMagicBridge.TryGetHeight(samplePosition.x, samplePosition.z, out sample.terrainHeight) ? (byte)1 : (byte)0;
                    sample.hasBiome = mapMagicBridge.TryGetBiomeIndex(samplePosition.x, samplePosition.z, out sample.biomeIndex) ? (byte)1 : (byte)0;
                    sample.isValid = sample.hasHeight != 0 || sample.hasBiome != 0 ? (byte)1 : (byte)0;

                    _samples[writeIndex] = sample;
                    writeIndex++;
                }
            }

            _sampleCount = width * width;
            _lastCenterPosition = center;
            _hasLastCenterPosition = true;
            _debugCacheReady = true;

            int centerIndex = (radiusCells * width) + radiusCells;
            if (centerIndex >= 0 && centerIndex < _sampleCount && _samples[centerIndex].isValid != 0)
                _debugLastCenterBiome = _samples[centerIndex].biomeIndex;
            else
                _debugLastCenterBiome = -1;

            _debugLastCenterPosition = center;
            UpdateDiagnostics();
        }

        private int FindNearestSampleIndex(Vector3 position, float maxDistance)
        {
            float maxDistanceSqr = maxDistance * maxDistance;
            float bestDistanceSqr = maxDistanceSqr;
            int bestIndex = -1;

            for (int i = 0; i < _sampleCount; i++)
            {
                CachedSample sample = _samples[i];
                if (sample.isValid == 0)
                    continue;

                Vector3 delta = sample.position - position;
                delta.y = 0f;
                float sqrDistance = delta.sqrMagnitude;
                if (sqrDistance > bestDistanceSqr)
                    continue;

                bestDistanceSqr = sqrDistance;
                bestIndex = i;
            }

            return bestIndex;
        }

        private void ApplyRuntimeOffsetToCachedState(Vector3 runtimeOffset)
        {
            if (!_hasLastCenterPosition && (_samples == null || _sampleCount <= 0))
                return;

            if (_hasLastCenterPosition)
                _lastCenterPosition += runtimeOffset;

            _debugLastCenterPosition += runtimeOffset;

            for (int i = 0; i < _sampleCount; i++)
            {
                CachedSample sample = _samples[i];
                sample.position += runtimeOffset;
                _samples[i] = sample;
            }
        }

        private void CacheRuntimeReferencesCold()
        {
            if (mapMagicBridge == null)
                mapMagicBridge = GlobalRegistry.MapMagic;

            if (_cachedPlayerContext == null)
                _cachedPlayerContext = GlobalRegistry.Player;

            RefreshRuntimeReferencesFromCachedContext();
        }

        private void RefreshRuntimeReferencesFromCachedContext()
        {
            if (playerTransform != null)
                return;

            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext != null && playerContext.PlayerTransform != null)
                playerTransform = playerContext.PlayerTransform;
        }

        private void EnsureStorageCold()
        {
            int newWidth = ClampSamplingSettings(out int requiredSamples);

            if (_samples == null || _samples.Length != requiredSamples)
                _samples = new CachedSample[requiredSamples];

            _gridWidth = newWidth;
        }

        private bool HasStorageForCurrentShape()
        {
            int newWidth = ClampSamplingSettings(out int requiredSamples);
            if (_samples == null || _samples.Length != requiredSamples)
            {
                _gridWidth = 0;
                return false;
            }

            _gridWidth = newWidth;
            return true;
        }

        private int ClampSamplingSettings(out int requiredSamples)
        {
            int clampedRadius = Mathf.Max(1, radiusCells);
            float clampedCellSize = Mathf.Max(8f, cellSize);
            float clampedRebuild = Mathf.Max(4f, rebuildDistance);

            radiusCells = clampedRadius;
            cellSize = clampedCellSize;
            rebuildDistance = clampedRebuild;

            int newWidth = clampedRadius * 2 + 1;
            requiredSamples = newWidth * newWidth;
            return newWidth;
        }

        private void UpdateDiagnostics()
        {
            _debugBridgeReady = mapMagicBridge != null && mapMagicBridge.IsAvailable;
            _debugPlayerReady = playerTransform != null;
            _debugSampleCount = _sampleCount;
        }
    }
}
