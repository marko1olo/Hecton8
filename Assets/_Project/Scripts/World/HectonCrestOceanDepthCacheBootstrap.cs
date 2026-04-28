using System.Collections.Generic;
using Crest;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Keeps Crest sea-floor depth cache coverage aligned to streamed terrain bounds.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-6900)] // After MapMagicBridge (-7000) so water level exists before the first cache populate.
    public sealed class HectonCrestOceanDepthCacheBootstrap : MonoBehaviour, ISlowTickable, IOriginShiftListener
    {
        private struct TerrainCoverage
        {
            public Bounds RuntimeBounds;
            public Bounds AbsoluteBounds;
            public float RuntimeTopY;
            public int TerrainCount;
        }

        private struct DepthCacheAlignment
        {
            public TerrainCoverage TerrainCoverage;
            public float RuntimeWaterLevel;
            public float AbsoluteWaterLevel;
            public float CameraMaxTerrainHeight;
            public float CoverageSize;
            public Vector3 RuntimeCacheCenter;
            public Vector3 RuntimeCacheMin;
            public Vector3 RuntimeCacheMax;
            public Vector3 AbsoluteCacheCenter;
            public Vector3 AbsoluteCacheMin;
            public Vector3 AbsoluteCacheMax;
        }

        private const string DepthCacheChildName = "OceanDepthCache";
        private const float BoundsChangeThreshold = 1f;
        private const float BoundsChangeThresholdSqr = BoundsChangeThreshold * BoundsChangeThreshold;
        private const float WaterLevelChangeThreshold = 0.05f;
        private const float MinimumCameraHeightAboveSeaLevel = 8f;
        private const float MinimumCoverageMeters = 256f;
        private const int DefaultDepthCacheResolution = 512;
        private const int DefaultCaptureLayerMask = 1;
        private static int TerrainLayer = int.MinValue;
        private static int TerrainLayerWithTrailingSpace = int.MinValue;
        private static int VoxelCaveLayer = int.MinValue;
        private static int VoxelCaveLayerWithTrailingSpace = int.MinValue;

        [Header("-- References ----------------")]
        [Tooltip("Explicit Crest ocean owner. Auto-resolved from the prefab root when left empty.")]
        [SerializeField] private OceanRenderer oceanRenderer;
        [Tooltip("Optional explicit depth-cache component. Auto-resolved or regenerated when authoring loses it.")]
        [SerializeField] private OceanDepthCache oceanDepthCache;
        [Tooltip("Optional MapMagic bridge used to recover the authored water surface level.")]
        [SerializeField] private MapMagicBridge mapMagicBridge;

        [Header("-- Cache Settings ------------")]
        [Tooltip("Extra world-space padding applied around the aggregated terrain footprint.")]
        [SerializeField, Min(0f)] private float terrainBoundsPadding = 64f;
        [Tooltip("Realtime depth-cache resolution. 512 is the MX350 baseline.")]
        [SerializeField, UnityEngine.Range(128, 1024)] private int depthCacheResolution = DefaultDepthCacheResolution;
        [Tooltip("When enabled, the cache repopulates after terrain streaming changes the captured footprint.")]
        [SerializeField] private bool repopulateOnTerrainChange = true;

        [Header("-- Diagnostics ---------------")]
        [SerializeField] private bool _debugCacheReady;
        [SerializeField] private int _debugTerrainCount;
        [SerializeField] private float _debugWaterLevel;
        [SerializeField] private Vector3 _debugLastCacheCenterWS;
        [SerializeField] private Vector3 _debugLastCacheScaleWS;
        [SerializeField] private int _debugCaptureLayerMask;
        [SerializeField] private float _debugCameraMaxTerrainHeight;
        [SerializeField] private float _debugAbsoluteWaterLevel;
        [SerializeField] private Vector3 _debugTerrainBoundsMinWS;
        [SerializeField] private Vector3 _debugTerrainBoundsMaxWS;
        [SerializeField] private Vector3 _debugTerrainBoundsMinAUP;
        [SerializeField] private Vector3 _debugTerrainBoundsMaxAUP;
        [SerializeField] private Vector3 _debugCacheBoundsMinWS;
        [SerializeField] private Vector3 _debugCacheBoundsMaxWS;
        [SerializeField] private Vector3 _debugCacheBoundsMinAUP;
        [SerializeField] private Vector3 _debugCacheBoundsMaxAUP;
        [SerializeField] private int _debugLastPopulateFrame = -1;

        private bool _registeredToSlowTickManager;
        private bool _hasConfiguredBounds;
        private int _lastTerrainCount;
        private int _captureLayerMask;
        private float _lastAbsoluteWaterLevel;
        private float _lastAppliedCameraMaxTerrainHeight = MinimumCameraHeightAboveSeaLevel;
        private Bounds _lastTerrainBoundsAUP;
        // COLD ALLOC: List<OceanDepthCache>[4] - duplicate Crest cache recovery scratch buffer - owner: HectonCrestOceanDepthCacheBootstrap
        private readonly List<OceanDepthCache> _depthCacheScratch = new List<OceanDepthCache>(4);

        private void Awake()
        {
            EnsureTerrainLayerCache();
            TryResolveReferences();
            _captureLayerMask = ResolveCaptureLayerMask();
        }

        private static void EnsureTerrainLayerCache()
        {
            if (TerrainLayer == int.MinValue)
                TerrainLayer = LayerMask.NameToLayer("Terrain");

            if (TerrainLayerWithTrailingSpace == int.MinValue)
                TerrainLayerWithTrailingSpace = LayerMask.NameToLayer("Terrain ");

            if (VoxelCaveLayer == int.MinValue)
                VoxelCaveLayer = LayerMask.NameToLayer("VoxelCave");

            if (VoxelCaveLayerWithTrailingSpace == int.MinValue)
                VoxelCaveLayerWithTrailingSpace = LayerMask.NameToLayer("VoxelCave ");
        }

        private void OnEnable()
        {
            HectonFloatingOrigin.RegisterListener(this);
            TryRegister();
        }

        private void Start()
        {
            TryRegister();
            if (Crest.OceanRenderer.Instance == null)
            {
                UpdateDiagnostics(cacheReady: false, terrainCount: 0, waterLevel: ResolveFallbackWaterLevel());
                return;
            }

            BootstrapDepthCache();
        }

        private void OnDisable()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregister();
        }

        private void OnDestroy()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregister();
        }

        /// <summary>
        /// Revalidates terrain coverage and repopulates the Crest depth cache when streamed terrain changes it.
        /// </summary>
        public void SlowTick()
        {
            if (_debugCacheReady && !repopulateOnTerrainChange)
                return;

            if (Crest.OceanRenderer.Instance == null)
            {
                UpdateDiagnostics(cacheReady: false, terrainCount: _debugTerrainCount, waterLevel: ResolveFallbackWaterLevel());
                return;
            }

            TryConfigureAndPopulate(forcePopulate: !_debugCacheReady);
        }

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            _hasConfiguredBounds = false;
            _debugCacheReady = false;

            if (!isActiveAndEnabled || Crest.OceanRenderer.Instance == null)
                return;

            TryConfigureAndPopulate(forcePopulate: true);
        }

        private void BootstrapDepthCache()
        {
            PurgeLegacyDepthCaches();
            EnsureDepthCacheComponent();
            ApplyDepthCacheSettings();
            TryConfigureAndPopulate(forcePopulate: true);
        }

        private void TryRegister()
        {
            if (_registeredToSlowTickManager)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _registeredToSlowTickManager = true;
        }

        private void TryUnregister()
        {
            if (!_registeredToSlowTickManager)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registeredToSlowTickManager = false;
        }

        private bool TryConfigureAndPopulate(bool forcePopulate)
        {
            if (!TryResolveReferences() ||
                Crest.OceanRenderer.Instance == null ||
                oceanRenderer == null ||
                !oceanRenderer.CreateSeaFloorDepthData)
            {
                UpdateDiagnostics(cacheReady: false, terrainCount: 0, waterLevel: ResolveFallbackWaterLevel());
                return false;
            }

            PurgeLegacyDepthCaches();
            OceanDepthCache depthCache = EnsureDepthCacheComponent();
            if (depthCache == null)
            {
                UpdateDiagnostics(cacheReady: false, terrainCount: 0, waterLevel: ResolveFallbackWaterLevel());
                return false;
            }

            ApplyDepthCacheSettings();

            if (!TryResolveTerrainCoverage(out TerrainCoverage terrainCoverage))
            {
                UpdateDiagnostics(cacheReady: false, terrainCount: 0, waterLevel: ResolveFallbackWaterLevel());
                return false;
            }

            float waterLevel = ResolveWaterLevel();
            DepthCacheAlignment alignment = BuildDepthCacheAlignment(in terrainCoverage, waterLevel);
            bool terrainChanged = !_hasConfiguredBounds || TerrainBoundsChanged(alignment.TerrainCoverage.AbsoluteBounds);
            bool waterChanged = Mathf.Abs(alignment.AbsoluteWaterLevel - _lastAbsoluteWaterLevel) > WaterLevelChangeThreshold;
            bool needsPopulate = forcePopulate ||
                                 depthCache.CacheTexture == null ||
                                 terrainChanged ||
                                 waterChanged ||
                                 Mathf.Abs(_lastAppliedCameraMaxTerrainHeight - alignment.CameraMaxTerrainHeight) > WaterLevelChangeThreshold ||
                                 alignment.TerrainCoverage.TerrainCount != _lastTerrainCount;

            if (!needsPopulate)
            {
                UpdateDiagnostics(cacheReady: oceanDepthCache != null && oceanDepthCache.CacheTexture != null, in alignment);
                return oceanDepthCache != null && oceanDepthCache.CacheTexture != null;
            }

            ConfigureDepthCacheTransform(depthCache.transform, in alignment);
            depthCache.HectonConfigureRealtimeCapture(
                _captureLayerMask,
                depthCacheResolution,
                alignment.CameraMaxTerrainHeight,
                relativeToSeaLevel: true);
            depthCache.PopulateCache(updateComponents: true);

            oceanDepthCache = depthCache;
            _hasConfiguredBounds = true;
            _lastTerrainBoundsAUP = alignment.TerrainCoverage.AbsoluteBounds;
            _lastAbsoluteWaterLevel = alignment.AbsoluteWaterLevel;
            _lastTerrainCount = alignment.TerrainCoverage.TerrainCount;
            _lastAppliedCameraMaxTerrainHeight = alignment.CameraMaxTerrainHeight;
            _debugLastPopulateFrame = Time.frameCount;

            bool cacheReady = depthCache.CacheTexture != null;
            UpdateDiagnostics(cacheReady, in alignment);
            return cacheReady;
        }

        private bool TryResolveReferences()
        {
            if (oceanRenderer == null)
                TryGetComponent(out oceanRenderer);

            if (mapMagicBridge == null)
                WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicBridge);

            oceanDepthCache = ResolvePreferredDepthCache();

            return oceanRenderer != null;
        }

        private OceanDepthCache EnsureDepthCacheComponent()
        {
            if (oceanDepthCache != null)
                return oceanDepthCache;

            Transform depthCacheTransform = transform.Find(DepthCacheChildName);
            if (depthCacheTransform != null)
                depthCacheTransform.TryGetComponent(out oceanDepthCache);

            if (oceanDepthCache == null)
                oceanDepthCache = ResolvePreferredDepthCache();

            if (oceanDepthCache != null)
                return oceanDepthCache;

            // COLD ALLOC: GameObject[1] - restore missing Crest depth-cache authoring child when prefab data is incomplete - owner: HectonCrestOceanDepthCacheBootstrap
            GameObject depthCacheObject = new GameObject(DepthCacheChildName);
            depthCacheObject.layer = oceanRenderer != null ? oceanRenderer.Layer : gameObject.layer;

            depthCacheTransform = depthCacheObject.transform;
            depthCacheTransform.SetParent(transform, false);
            depthCacheTransform.localRotation = Quaternion.identity;
            depthCacheTransform.localScale = new Vector3(MinimumCoverageMeters, 1f, MinimumCoverageMeters);

            float waterLevel = ResolveWaterLevel();
            depthCacheTransform.localPosition = transform.InverseTransformPoint(new Vector3(transform.position.x, waterLevel, transform.position.z));

            oceanDepthCache = depthCacheObject.AddComponent<OceanDepthCache>();
            return oceanDepthCache;
        }

        private void ApplyDepthCacheSettings()
        {
            if (oceanDepthCache == null)
                return;

            _captureLayerMask = ResolveCaptureLayerMask();
            float configuredCameraMaxTerrainHeight =
                Mathf.Max(_lastAppliedCameraMaxTerrainHeight, MinimumCameraHeightAboveSeaLevel);
            oceanDepthCache.HectonConfigureRealtimeCapture(
                _captureLayerMask,
                depthCacheResolution,
                configuredCameraMaxTerrainHeight,
                relativeToSeaLevel: true);
            _lastAppliedCameraMaxTerrainHeight = configuredCameraMaxTerrainHeight;

            GameObject depthCacheObject = oceanDepthCache.gameObject;
            if (!oceanDepthCache.enabled)
                oceanDepthCache.enabled = true;

            if (oceanRenderer != null && depthCacheObject.layer != oceanRenderer.Layer)
                depthCacheObject.layer = oceanRenderer.Layer;

            if (!depthCacheObject.activeSelf)
                depthCacheObject.SetActive(true);
        }

        private bool TryResolveTerrainCoverage(out TerrainCoverage terrainCoverage)
        {
            terrainCoverage = default;

            Terrain[] terrains = Terrain.activeTerrains;
            if (terrains == null || terrains.Length == 0)
            {
                terrains =
                    Resources.FindObjectsOfTypeAll<Terrain>(); // COLD ALLOC: Terrain[] - depth-cache recovery fallback when Unity's active terrain cache is empty - owner: HectonCrestOceanDepthCacheBootstrap
                if (terrains == null || terrains.Length == 0)
                    return false;
            }

            bool initialized = false;
            for (int terrainIndex = 0; terrainIndex < terrains.Length; terrainIndex++)
            {
                Terrain terrain = terrains[terrainIndex];
                if (terrain == null || terrain.terrainData == null)
                    continue;

                Vector3 terrainPosition = terrain.transform.position;
                Vector3 terrainSize = terrain.terrainData.size;
                if (!IsFiniteVector3(terrainPosition) ||
                    !IsFiniteVector3(terrainSize) ||
                    terrainSize.x <= 0f ||
                    terrainSize.z <= 0f)
                {
                    continue;
                }

                Vector3 runtimeMin = terrainPosition;
                Vector3 runtimeMax = terrainPosition + terrainSize;
                Vector3 absoluteMin = ResolveAbsoluteUniversePoint(runtimeMin);
                Vector3 absoluteMax = ResolveAbsoluteUniversePoint(runtimeMax);
                Bounds runtimeBounds = CreateBoundsFromMinMax(runtimeMin, runtimeMax);
                Bounds absoluteBounds = CreateBoundsFromMinMax(absoluteMin, absoluteMax);
                float terrainMaxY = runtimeMax.y;

                if (!initialized)
                {
                    terrainCoverage.RuntimeBounds = runtimeBounds;
                    terrainCoverage.AbsoluteBounds = absoluteBounds;
                    terrainCoverage.RuntimeTopY = terrainMaxY;
                    initialized = true;
                }
                else
                {
                    terrainCoverage.RuntimeBounds.Encapsulate(runtimeBounds);
                    terrainCoverage.AbsoluteBounds.Encapsulate(absoluteBounds);
                    terrainCoverage.RuntimeTopY = Mathf.Max(terrainCoverage.RuntimeTopY, terrainMaxY);
                }

                terrainCoverage.TerrainCount++;
            }

            return initialized;
        }

        private float ResolveWaterLevel()
        {
            if (oceanRenderer != null && oceanRenderer.Root != null)
            {
                float seaLevel = oceanRenderer.SeaLevel;
                if (IsFinite(seaLevel))
                    return seaLevel;
            }

            return ResolveFallbackWaterLevel();
        }

        private void PurgeLegacyDepthCaches()
        {
            _depthCacheScratch.Clear();
            GetComponentsInChildren(true, _depthCacheScratch);

            if (_depthCacheScratch.Count == 0)
            {
                oceanDepthCache = null;
                return;
            }

            OceanDepthCache authoritativeDepthCache = ResolvePreferredDepthCacheFromScratch();
            oceanDepthCache = authoritativeDepthCache;

            for (int depthCacheIndex = 0; depthCacheIndex < _depthCacheScratch.Count; depthCacheIndex++)
            {
                OceanDepthCache candidate = _depthCacheScratch[depthCacheIndex];
                if (candidate == null || candidate == authoritativeDepthCache)
                    continue;

                DisableLegacyDepthCache(candidate);
            }
        }

        private OceanDepthCache ResolvePreferredDepthCache()
        {
            _depthCacheScratch.Clear();
            GetComponentsInChildren(true, _depthCacheScratch);
            return ResolvePreferredDepthCacheFromScratch();
        }

        private OceanDepthCache ResolvePreferredDepthCacheFromScratch()
        {
            OceanDepthCache fallbackDepthCache = null;
            for (int depthCacheIndex = 0; depthCacheIndex < _depthCacheScratch.Count; depthCacheIndex++)
            {
                OceanDepthCache candidate = _depthCacheScratch[depthCacheIndex];
                if (candidate == null)
                    continue;

                if (fallbackDepthCache == null)
                    fallbackDepthCache = candidate;

                if (candidate.transform.parent == transform && candidate.name == DepthCacheChildName)
                    return candidate;
            }

            return fallbackDepthCache;
        }

        private static void DisableLegacyDepthCache(OceanDepthCache legacyDepthCache)
        {
            if (legacyDepthCache == null)
                return;

            if (legacyDepthCache.enabled)
                legacyDepthCache.enabled = false;

            GameObject legacyDepthCacheObject = legacyDepthCache.gameObject;
            if (legacyDepthCacheObject.activeSelf)
                legacyDepthCacheObject.SetActive(false);
        }

        private float ResolveFallbackWaterLevel()
        {
            if (mapMagicBridge == null)
                WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicBridge);

            if (mapMagicBridge != null)
            {
                float bridgedWaterLevel = mapMagicBridge.WaterSurfaceLevel;
                if (IsFinite(bridgedWaterLevel))
                    return bridgedWaterLevel;
            }

            return oceanRenderer != null ? oceanRenderer.transform.position.y : transform.position.y;
        }

        private static float ResolveCameraMaxTerrainHeight(float terrainTopY, float waterLevel)
        {
            if (!IsFinite(terrainTopY) || !IsFinite(waterLevel))
                return MinimumCameraHeightAboveSeaLevel;

            return Mathf.Max(MinimumCameraHeightAboveSeaLevel, terrainTopY - waterLevel);
        }

        private DepthCacheAlignment BuildDepthCacheAlignment(in TerrainCoverage terrainCoverage, float runtimeWaterLevel)
        {
            float paddedAbsoluteMinX = terrainCoverage.AbsoluteBounds.min.x - terrainBoundsPadding;
            float paddedAbsoluteMaxX = terrainCoverage.AbsoluteBounds.max.x + terrainBoundsPadding;
            float paddedAbsoluteMinZ = terrainCoverage.AbsoluteBounds.min.z - terrainBoundsPadding;
            float paddedAbsoluteMaxZ = terrainCoverage.AbsoluteBounds.max.z + terrainBoundsPadding;
            float coverageSize = Mathf.Max(
                MinimumCoverageMeters,
                Mathf.Max(paddedAbsoluteMaxX - paddedAbsoluteMinX, paddedAbsoluteMaxZ - paddedAbsoluteMinZ));
            float absoluteWaterLevel = ResolveAbsoluteUniverseY(runtimeWaterLevel);
            Vector3 absoluteCacheCenter = new Vector3(
                (paddedAbsoluteMinX + paddedAbsoluteMaxX) * 0.5f,
                absoluteWaterLevel,
                (paddedAbsoluteMinZ + paddedAbsoluteMaxZ) * 0.5f);
            Vector3 absoluteHalfExtents = new Vector3(coverageSize * 0.5f, 0f, coverageSize * 0.5f);
            Vector3 runtimeCacheCenter = HectonFloatingOrigin.ToRuntimePosition(absoluteCacheCenter);

            return new DepthCacheAlignment
            {
                TerrainCoverage = terrainCoverage,
                RuntimeWaterLevel = runtimeWaterLevel,
                AbsoluteWaterLevel = absoluteWaterLevel,
                CameraMaxTerrainHeight = ResolveCameraMaxTerrainHeight(terrainCoverage.RuntimeTopY, runtimeWaterLevel),
                CoverageSize = coverageSize,
                RuntimeCacheCenter = runtimeCacheCenter,
                RuntimeCacheMin = runtimeCacheCenter - absoluteHalfExtents,
                RuntimeCacheMax = runtimeCacheCenter + absoluteHalfExtents,
                AbsoluteCacheCenter = absoluteCacheCenter,
                AbsoluteCacheMin = absoluteCacheCenter - absoluteHalfExtents,
                AbsoluteCacheMax = absoluteCacheCenter + absoluteHalfExtents
            };
        }

        private void ConfigureDepthCacheTransform(Transform depthCacheTransform, in DepthCacheAlignment alignment)
        {
            depthCacheTransform.position = alignment.RuntimeCacheCenter;
            depthCacheTransform.rotation = Quaternion.identity;

            Vector3 parentScale = transform.lossyScale;
            float inverseParentScaleX = parentScale.x > 0.0001f ? 1f / parentScale.x : 1f;
            float inverseParentScaleY = parentScale.y > 0.0001f ? 1f / parentScale.y : 1f;
            float inverseParentScaleZ = parentScale.z > 0.0001f ? 1f / parentScale.z : 1f;
            depthCacheTransform.localScale = new Vector3(
                alignment.CoverageSize * inverseParentScaleX,
                inverseParentScaleY,
                alignment.CoverageSize * inverseParentScaleZ);
        }

        private bool TerrainBoundsChanged(Bounds terrainBoundsAUP)
        {
            if (!_hasConfiguredBounds)
                return true;

            Vector3 centerDelta = terrainBoundsAUP.center - _lastTerrainBoundsAUP.center;
            if (centerDelta.sqrMagnitude > BoundsChangeThresholdSqr)
                return true;

            Vector3 sizeDelta = terrainBoundsAUP.size - _lastTerrainBoundsAUP.size;
            return sizeDelta.sqrMagnitude > BoundsChangeThresholdSqr;
        }

        private void UpdateDiagnostics(bool cacheReady, int terrainCount, float waterLevel)
        {
            _debugCacheReady = cacheReady;
            _debugTerrainCount = terrainCount;
            _debugWaterLevel = waterLevel;
            _debugAbsoluteWaterLevel = ResolveAbsoluteUniverseY(waterLevel);
            _debugCaptureLayerMask = _captureLayerMask;
            _debugTerrainBoundsMinWS = Vector3.zero;
            _debugTerrainBoundsMaxWS = Vector3.zero;
            _debugTerrainBoundsMinAUP = Vector3.zero;
            _debugTerrainBoundsMaxAUP = Vector3.zero;
            _debugCacheBoundsMinWS = Vector3.zero;
            _debugCacheBoundsMaxWS = Vector3.zero;
            _debugCacheBoundsMinAUP = Vector3.zero;
            _debugCacheBoundsMaxAUP = Vector3.zero;

            if (oceanDepthCache == null)
            {
                _debugLastCacheCenterWS = Vector3.zero;
                _debugLastCacheScaleWS = Vector3.zero;
                _debugCameraMaxTerrainHeight = 0f;
                return;
            }

            Transform depthCacheTransform = oceanDepthCache.transform;
            _debugLastCacheCenterWS = depthCacheTransform.position;
            _debugLastCacheScaleWS = depthCacheTransform.lossyScale;
            _debugCameraMaxTerrainHeight = _lastAppliedCameraMaxTerrainHeight;
        }

        private void UpdateDiagnostics(bool cacheReady, in DepthCacheAlignment alignment)
        {
            _debugCacheReady = cacheReady;
            _debugTerrainCount = alignment.TerrainCoverage.TerrainCount;
            _debugWaterLevel = alignment.RuntimeWaterLevel;
            _debugAbsoluteWaterLevel = alignment.AbsoluteWaterLevel;
            _debugCaptureLayerMask = _captureLayerMask;
            _debugTerrainBoundsMinWS = alignment.TerrainCoverage.RuntimeBounds.min;
            _debugTerrainBoundsMaxWS = alignment.TerrainCoverage.RuntimeBounds.max;
            _debugTerrainBoundsMinAUP = alignment.TerrainCoverage.AbsoluteBounds.min;
            _debugTerrainBoundsMaxAUP = alignment.TerrainCoverage.AbsoluteBounds.max;
            _debugCacheBoundsMinWS = alignment.RuntimeCacheMin;
            _debugCacheBoundsMaxWS = alignment.RuntimeCacheMax;
            _debugCacheBoundsMinAUP = alignment.AbsoluteCacheMin;
            _debugCacheBoundsMaxAUP = alignment.AbsoluteCacheMax;

            if (oceanDepthCache == null)
            {
                _debugLastCacheCenterWS = alignment.RuntimeCacheCenter;
                _debugLastCacheScaleWS = Vector3.zero;
                _debugCameraMaxTerrainHeight = alignment.CameraMaxTerrainHeight;
                return;
            }

            Transform depthCacheTransform = oceanDepthCache.transform;
            _debugLastCacheCenterWS = depthCacheTransform.position;
            _debugLastCacheScaleWS = depthCacheTransform.lossyScale;
            _debugCameraMaxTerrainHeight = alignment.CameraMaxTerrainHeight;
        }

        private static int ResolveCaptureLayerMask()
        {
            int resolvedMask = LayerMask.GetMask("Default", "Terrain", "Terrain ", "VoxelCave", "VoxelCave ");
            if (resolvedMask != 0)
                return resolvedMask;

            int fallbackMask = DefaultCaptureLayerMask;
            int terrainLayer = TerrainLayer;
            if (terrainLayer < 0)
                terrainLayer = TerrainLayerWithTrailingSpace;

            if (terrainLayer >= 0)
                fallbackMask |= 1 << terrainLayer;

            int voxelCaveLayer = VoxelCaveLayer;
            if (voxelCaveLayer < 0)
                voxelCaveLayer = VoxelCaveLayerWithTrailingSpace;

            if (voxelCaveLayer >= 0)
                fallbackMask |= 1 << voxelCaveLayer;

            return fallbackMask;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFiniteVector3(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static Bounds CreateBoundsFromMinMax(Vector3 min, Vector3 max)
        {
            Vector3 center = (min + max) * 0.5f;
            Vector3 size = max - min;
            return new Bounds(center, size);
        }

        private static Vector3 ResolveAbsoluteUniversePoint(Vector3 runtimePosition)
        {
            return HectonFloatingOrigin.ToAbsoluteUniversePosition(runtimePosition);
        }

        private static float ResolveAbsoluteUniverseY(float runtimeY)
        {
            return runtimeY + HectonFloatingOrigin.CurrentTotalOffset.y;
        }
    }
}
