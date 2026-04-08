using Hecton8.Core;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4300)]
    public sealed class BiomeSamplerCache : MonoBehaviour, ISlowTickable
    {
        internal static BiomeSamplerCache ActiveRuntimeInstance { get; private set; }

        public struct CachedSample
        {
            public Vector3 position;
            public float terrainHeight;
            public int biomeIndex;
            public bool hasHeight;
            public bool hasBiome;
            public bool isValid;
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
        private Vector3 _lastCenterPosition;
        private bool _hasLastCenterPosition;

        public bool IsReady => _debugCacheReady;
        public int SampleCount => _sampleCount;

        private void Awake()
        {
            ActiveRuntimeInstance = this;

            if (mapMagicBridge == null)
                WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicBridge);

            if (playerTransform == null)
                WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);

            EnsureStorage();
            UpdateDiagnostics();
        }

        private void OnEnable()
        {
            if (GameTickManager.Instance != null && !_registeredToTickManager)
            {
                GameTickManager.Instance.Register((ISlowTickable)this);
                _registeredToTickManager = true;
            }
        }

        private void Start()
        {
            if (!_registeredToTickManager && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Register((ISlowTickable)this);
                _registeredToTickManager = true;
            }

            RebuildCache(force: true);
        }

        private void OnDisable()
        {
            if (_registeredToTickManager && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Unregister((ISlowTickable)this);
                _registeredToTickManager = false;
            }
        }

        private void OnDestroy()
        {
            if (ActiveRuntimeInstance == this)
                ActiveRuntimeInstance = null;
        }

        public void SlowTick()
        {
            RebuildCache(force: false);
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
            return sample.isValid;
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
            return sample.isValid;
        }

        private void RebuildCache(bool force)
        {
            EnsureReferences();
            EnsureStorage();

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
                    sample.hasHeight = mapMagicBridge.TryGetHeight(samplePosition.x, samplePosition.z, out sample.terrainHeight);
                    sample.hasBiome = mapMagicBridge.TryGetBiomeIndex(samplePosition.x, samplePosition.z, out sample.biomeIndex);
                    sample.isValid = sample.hasHeight || sample.hasBiome;

                    _samples[writeIndex] = sample;
                    writeIndex++;
                }
            }

            _sampleCount = width * width;
            _lastCenterPosition = center;
            _hasLastCenterPosition = true;
            _debugCacheReady = true;

            int centerIndex = (radiusCells * width) + radiusCells;
            if (centerIndex >= 0 && centerIndex < _sampleCount && _samples[centerIndex].isValid)
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
                if (!sample.isValid)
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

        private void EnsureReferences()
        {
            if (mapMagicBridge == null)
                WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicBridge);

            if (playerTransform == null)
                WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);
        }

        private void EnsureStorage()
        {
            int clampedRadius = Mathf.Max(1, radiusCells);
            float clampedCellSize = Mathf.Max(8f, cellSize);
            float clampedRebuild = Mathf.Max(4f, rebuildDistance);

            radiusCells = clampedRadius;
            cellSize = clampedCellSize;
            rebuildDistance = clampedRebuild;

            int newWidth = clampedRadius * 2 + 1;
            int requiredSamples = newWidth * newWidth;

            if (_samples == null || _samples.Length != requiredSamples)
                _samples = new CachedSample[requiredSamples];

            _gridWidth = newWidth;
        }

        private void UpdateDiagnostics()
        {
            _debugBridgeReady = mapMagicBridge != null && mapMagicBridge.IsAvailable;
            _debugPlayerReady = playerTransform != null;
            _debugSampleCount = _sampleCount;
        }
    }
}
