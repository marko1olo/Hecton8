using System.Collections.Generic;
using Crest;
using Hecton8.Celestial;
using Hecton8.Bootstrap;
using Hecton8.Core;
using System.Diagnostics;
using Unity.Mathematics;
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
        private enum DepthCacheOwnershipMode
        {
            None = 0,
            AuthoredLocal = 1,
            GlobalFallback = 2
        }

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
            public float CameraFarPlane;
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
        private const int DefaultCaptureLayerMask = 0;
        private const int RuntimeCameraBufferSize = 8;
        private const int RuntimeTerrainBufferSize = 64;
        private const string DepthDebugOutputPath = "C:/hades/Hecton8/Temp/depth_debug.png";
        private static int TerrainLayer = int.MinValue;
        private static int TerrainLayerWithTrailingSpace = int.MinValue;
        // COLD ALLOC: Camera[8] - reusable runtime-camera resolve scratch for Crest viewpoint ownership - owner: HectonCrestOceanDepthCacheBootstrap
        private static readonly Camera[] RuntimeCameraBuffer = new Camera[RuntimeCameraBufferSize];
        // COLD ALLOC: Terrain[64] - reusable MapMagic terrain coverage scratch; populated by MapMagicBridge tile registry - owner: HectonCrestOceanDepthCacheBootstrap
        private static readonly Terrain[] RuntimeTerrainBuffer = new Terrain[RuntimeTerrainBufferSize];

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

        [Header("-- Tidal Cache Modulation --")]
        [SerializeField] private bool enableTidalHeightCacheModulation = true;
        [SerializeField, Min(0f)] private float tidalHeightCacheAmplitudeMeters = 4f;

        [Tooltip("When enabled in editor/development, save one post-populate depth cache frame to Temp/depth_debug.png.")]
        [SerializeField] private bool dumpDepthDebugFrame;

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
        [SerializeField] private DepthCacheOwnershipMode _debugOwnershipMode;
        [SerializeField] private Vector3 _debugCaptureCameraPositionWS;
        [SerializeField] private float _debugCaptureCameraNear;
        [SerializeField] private float _debugCaptureCameraFar;
        [SerializeField] private float _debugCaptureCameraOrthoSize;
        [SerializeField] private float _debugTidalWaterLevelOffset;
        [SerializeField] private Vector3 _debugTidalAegirDirection;

        private bool _registeredToSlowTickManager;
        private bool _hasConfiguredBounds;
        private int _lastTerrainCount;
        private int _captureLayerMask;
        private float _lastAbsoluteWaterLevel;
        private float _lastAppliedCameraMaxTerrainHeight = MinimumCameraHeightAboveSeaLevel;
        private Bounds _lastTerrainBoundsAUP;
        // COLD ALLOC: List<OceanDepthCache>[4] - duplicate Crest cache recovery scratch buffer - owner: HectonCrestOceanDepthCacheBootstrap
        private readonly List<OceanDepthCache> _depthCacheScratch = new List<OceanDepthCache>(4);
        // COLD ALLOC: List<MonoBehaviour>[32] - Crest shifting-origin interface scratch buffer - owner: HectonCrestOceanDepthCacheBootstrap
        private readonly List<MonoBehaviour> _shiftingOriginScratch = new List<MonoBehaviour>(32);
        // COLD ALLOC: List<ShapeGerstnerBatched>[16] - Crest gerstner rebase scratch buffer - owner: HectonCrestOceanDepthCacheBootstrap
        private readonly List<ShapeGerstnerBatched> _gerstnerScratch = new List<ShapeGerstnerBatched>(16);
        // COLD ALLOC: List<GameObject>[16] - scene-root scratch used to sweep distributed Crest shapes during rare origin shifts - owner: HectonCrestOceanDepthCacheBootstrap
        private readonly List<GameObject> _sceneRootScratch = new List<GameObject>(16);
        private bool _loggedMissingResolvedTerrains;

        private void Awake()
        {
            EnsureTerrainLayerCache();
            TryResolveReferences();
            _captureLayerMask = ResolveCaptureLayerMask();
        }

        private static void EnsureTerrainLayerCache()
        {
            if (TerrainLayer == int.MinValue)
                TerrainLayer = HectonLayerMasks.Terrain;

            if (TerrainLayerWithTrailingSpace == int.MinValue)
                TerrainLayerWithTrailingSpace = HectonLayerMasks.Terrain;
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

            if (!isActiveAndEnabled || shiftData.ShiftOffset.sqrMagnitude <= 0.0001f)
                return;

            ResetCrestSimulationForOriginShift(shiftData.ShiftOffset);
            TryConfigureAndPopulate(forcePopulate: true);
        }

        private void ResetCrestSimulationForOriginShift(Vector3 shiftOffset)
        {
            if (!TryResolveReferences())
                return;

            OceanRenderer activeOcean = oceanRenderer != null ? oceanRenderer : Crest.OceanRenderer.Instance;
            if (activeOcean == null)
                return;

            activeOcean._lodTransform?.SetOrigin(shiftOffset);

            _shiftingOriginScratch.Clear();
            activeOcean.GetComponentsInChildren(includeInactive: true, _shiftingOriginScratch);
            for (int i = 0; i < _shiftingOriginScratch.Count; i++)
            {
                if (_shiftingOriginScratch[i] is IShiftingOrigin shiftingOrigin)
                    shiftingOrigin.SetOrigin(shiftOffset);
            }

            _sceneRootScratch.Clear();
            activeOcean.gameObject.scene.GetRootGameObjects(_sceneRootScratch);
            for (int rootIndex = 0; rootIndex < _sceneRootScratch.Count; rootIndex++)
            {
                GameObject rootObject = _sceneRootScratch[rootIndex];
                if (rootObject == null)
                    continue;

                _gerstnerScratch.Clear();
                rootObject.GetComponentsInChildren(includeInactive: true, _gerstnerScratch);
                for (int gerstnerIndex = 0; gerstnerIndex < _gerstnerScratch.Count; gerstnerIndex++)
                    _gerstnerScratch[gerstnerIndex].SetOrigin(shiftOffset);
            }

            // Clear persistent Crest simulation state so foam and dynamic waves do not integrate the 5000 m rebase as velocity.
            activeOcean.ClearLodData();
        }

        private void BootstrapDepthCache()
        {
            EnsureRuntimeOceanViewOwnership();

            if (TryUseAuthoredLocalDepthCaches(forcePopulate: true))
                return;

            PurgeLegacyDepthCaches();
            EnsureDepthCacheComponent();
            ApplyDepthCacheSettings();
            TryConfigureAndPopulate(forcePopulate: true);
        }

        private void TryRegister()
        {
            if (_registeredToSlowTickManager || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _registeredToSlowTickManager = GlobalRegistry.SlowTickables.Contains(this);
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

            EnsureRuntimeOceanViewOwnership();

            if (TryUseAuthoredLocalDepthCaches(forcePopulate))
                return true;

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
            Camera captureCamera = depthCache.HectonEnsureCaptureCamera(updateComponents: true);
            depthCache.HectonAlignCaptureCamera(
                captureCamera,
                alignment.RuntimeCacheCenter,
                alignment.CameraMaxTerrainHeight,
                alignment.CameraFarPlane,
                alignment.CoverageSize,
                _captureLayerMask);
            depthCache.PopulateCache(updateComponents: false);
            CacheCaptureCameraDiagnostics(captureCamera);
            TryDumpDepthDebugFrame(depthCache);

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

        private bool TryUseAuthoredLocalDepthCaches(bool forcePopulate)
        {
            _depthCacheScratch.Clear();
            GetComponentsInChildren(true, _depthCacheScratch);

            bool foundAuthoredLocalDepthCache = false;
            bool anyLocalCacheReady = false;
            int activeAuthoredLocalDepthCacheCount = 0;

            for (int depthCacheIndex = 0; depthCacheIndex < _depthCacheScratch.Count; depthCacheIndex++)
            {
                OceanDepthCache candidate = _depthCacheScratch[depthCacheIndex];
                if (candidate == null)
                    continue;

                if (IsAuthoredLocalDepthCache(candidate))
                {
                    foundAuthoredLocalDepthCache = true;
                    activeAuthoredLocalDepthCacheCount++;
                    EnableDepthCache(candidate);

                    if (forcePopulate || candidate.CacheTexture == null)
                        candidate.PopulateCache(updateComponents: true);

                    anyLocalCacheReady |= candidate.CacheTexture != null;
                    continue;
                }

                DisableLegacyDepthCache(candidate);
            }

            if (!foundAuthoredLocalDepthCache)
                return false;

            oceanDepthCache = null;
            _captureLayerMask = 0;
            _hasConfiguredBounds = false;
            _lastTerrainCount = activeAuthoredLocalDepthCacheCount;
            _lastAppliedCameraMaxTerrainHeight = MinimumCameraHeightAboveSeaLevel;
            _debugLastPopulateFrame = Time.frameCount;
            UpdateDiagnosticsForAuthoredLocalDepthCaches(anyLocalCacheReady, activeAuthoredLocalDepthCacheCount, ResolveWaterLevel());
            return true;
        }

        private void EnsureRuntimeOceanViewOwnership()
        {
            if (!Application.isPlaying || oceanRenderer == null)
                return;

            Camera runtimeCamera = ResolveRuntimeMainCamera();
            if (runtimeCamera == null)
                return;

            Transform runtimeViewpoint = runtimeCamera.transform;
            if (!ReferenceEquals(oceanRenderer.ViewCamera, runtimeCamera))
                oceanRenderer.ViewCamera = runtimeCamera;

            if (!ReferenceEquals(oceanRenderer.Viewpoint, runtimeViewpoint))
                oceanRenderer.Viewpoint = runtimeViewpoint;
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

        private static Camera ResolveRuntimeMainCamera()
        {
            if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null &&
                playerTransform.TryGetComponent(out Camera playerOwnedCamera) &&
                IsRuntimeMainCamera(playerOwnedCamera))
            {
                return playerOwnedCamera;
            }

            int totalFound = Camera.GetAllCameras(RuntimeCameraBuffer);
            int safeCount = Mathf.Min(totalFound, RuntimeCameraBuffer.Length);
            for (int i = 0; i < safeCount; i++)
            {
                Camera candidate = RuntimeCameraBuffer[i];
                if (IsRuntimeMainCamera(candidate) &&
                    candidate.enabled &&
                    candidate.gameObject.activeInHierarchy)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool IsRuntimeMainCamera(Camera camera)
        {
            return camera != null &&
                   camera.cameraType != CameraType.SceneView &&
                   camera.CompareTag("MainCamera");
        }

        private bool TryResolveTerrainCoverage(out TerrainCoverage terrainCoverage)
        {
            terrainCoverage = default;

            if (mapMagicBridge == null)
                WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicBridge);

            int terrainCount = mapMagicBridge != null
                ? mapMagicBridge.CopyResolvedTerrainsTo(RuntimeTerrainBuffer)
                : 0;

            if (terrainCount <= 0)
            {
                ReportMissingResolvedTerrains();
                return false;
            }

            _loggedMissingResolvedTerrains = false;

            bool initialized = false;
            for (int terrainIndex = 0; terrainIndex < terrainCount; terrainIndex++)
            {
                Terrain terrain = RuntimeTerrainBuffer[terrainIndex];
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

        private void ReportMissingResolvedTerrains()
        {
            if (_loggedMissingResolvedTerrains)
                return;

            _loggedMissingResolvedTerrains = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.Debug.LogError(
                "[HectonCrestOceanDepthCacheBootstrap] MapMagicBridge resolved no terrain tiles. Crest depth-cache bootstrap requires registry-owned MapMagic terrain coverage.",
                this);
#endif
        }

        private float ResolveWaterLevel()
        {
            float baseWaterLevel;
            if (oceanRenderer != null && oceanRenderer.Root != null)
            {
                float seaLevel = oceanRenderer.SeaLevel;
                if (IsFinite(seaLevel))
                {
                    baseWaterLevel = seaLevel;
                    return baseWaterLevel + ResolveTidalHeightCacheOffset();
                }
            }

            baseWaterLevel = ResolveFallbackWaterLevel();
            return baseWaterLevel + ResolveTidalHeightCacheOffset();
        }

        private float ResolveTidalHeightCacheOffset()
        {
            _debugTidalWaterLevelOffset = 0f;
            _debugTidalAegirDirection = Vector3.zero;

            if (!enableTidalHeightCacheModulation || tidalHeightCacheAmplitudeMeters <= 0f)
                return 0f;

            HectonCelestialEngine celestialEngine = GlobalRegistry.CelestialEngine;
            if (celestialEngine == null ||
                !celestialEngine.TryGetAegirSkyDirection(out Vector3 aegirDirection) ||
                !IsFiniteVector3(aegirDirection))
            {
                return 0f;
            }

            float directionMagnitudeSqr = aegirDirection.sqrMagnitude;
            if (directionMagnitudeSqr <= 0.0001f)
                return 0f;

            Vector3 normalizedAegirDirection = aegirDirection * math.rsqrt(directionMagnitudeSqr);
            float verticalDot = Mathf.Clamp(Vector3.Dot(normalizedAegirDirection, Vector3.up), -1f, 1f);
            float offset = verticalDot * Mathf.Max(0f, tidalHeightCacheAmplitudeMeters);
            _debugTidalAegirDirection = normalizedAegirDirection;
            _debugTidalWaterLevelOffset = offset;
            return offset;
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

        private bool IsAuthoredLocalDepthCache(OceanDepthCache candidate)
        {
            return candidate != null &&
                   !(candidate.transform.parent == transform && candidate.name == DepthCacheChildName);
        }

        private void EnableDepthCache(OceanDepthCache depthCache)
        {
            if (depthCache == null)
                return;

            GameObject depthCacheObject = depthCache.gameObject;
            if (!depthCacheObject.activeSelf)
                depthCacheObject.SetActive(true);

            if (!depthCache.enabled)
                depthCache.enabled = true;
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

        private static float ResolveCameraFarPlane(float terrainBottomY, float terrainTopY, float waterLevel)
        {
            if (!IsFinite(terrainBottomY) || !IsFinite(terrainTopY) || !IsFinite(waterLevel))
                return MinimumCameraHeightAboveSeaLevel + 64f;

            float cameraHeight = ResolveCameraMaxTerrainHeight(terrainTopY, waterLevel);
            float clearanceAboveWater = Mathf.Max(cameraHeight - 0.05f, MinimumCameraHeightAboveSeaLevel);
            float waterToTerrainBottom = Mathf.Max(waterLevel - terrainBottomY, 0f);
            return Mathf.Max(clearanceAboveWater + waterToTerrainBottom + 8f, 32f);
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
                CameraFarPlane = ResolveCameraFarPlane(terrainCoverage.RuntimeBounds.min.y, terrainCoverage.RuntimeTopY, runtimeWaterLevel),
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
            _debugOwnershipMode = cacheReady && oceanDepthCache != null
                ? DepthCacheOwnershipMode.GlobalFallback
                : DepthCacheOwnershipMode.None;
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
            _debugCaptureCameraPositionWS = Vector3.zero;
            _debugCaptureCameraNear = 0f;
            _debugCaptureCameraFar = 0f;
            _debugCaptureCameraOrthoSize = 0f;

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
            _debugOwnershipMode = cacheReady && oceanDepthCache != null
                ? DepthCacheOwnershipMode.GlobalFallback
                : DepthCacheOwnershipMode.None;
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

        private void UpdateDiagnosticsForAuthoredLocalDepthCaches(bool cacheReady, int localDepthCacheCount, float waterLevel)
        {
            _debugOwnershipMode = cacheReady
                ? DepthCacheOwnershipMode.AuthoredLocal
                : DepthCacheOwnershipMode.None;
            _debugCacheReady = cacheReady;
            _debugTerrainCount = localDepthCacheCount;
            _debugWaterLevel = waterLevel;
            _debugAbsoluteWaterLevel = ResolveAbsoluteUniverseY(waterLevel);
            _debugCaptureLayerMask = 0;
            _debugTerrainBoundsMinWS = Vector3.zero;
            _debugTerrainBoundsMaxWS = Vector3.zero;
            _debugTerrainBoundsMinAUP = Vector3.zero;
            _debugTerrainBoundsMaxAUP = Vector3.zero;
            _debugCacheBoundsMinWS = Vector3.zero;
            _debugCacheBoundsMaxWS = Vector3.zero;
            _debugCacheBoundsMinAUP = Vector3.zero;
            _debugCacheBoundsMaxAUP = Vector3.zero;
            _debugLastCacheCenterWS = Vector3.zero;
            _debugLastCacheScaleWS = Vector3.zero;
            _debugCameraMaxTerrainHeight = 0f;
            _debugCaptureCameraPositionWS = Vector3.zero;
            _debugCaptureCameraNear = 0f;
            _debugCaptureCameraFar = 0f;
            _debugCaptureCameraOrthoSize = 0f;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private void TryDumpDepthDebugFrame(OceanDepthCache depthCache)
        {
            if (!dumpDepthDebugFrame || depthCache == null)
                return;

            depthCache.HectonSaveDepthCacheTexturePng(DepthDebugOutputPath);
            dumpDepthDebugFrame = false;
        }

        private void CacheCaptureCameraDiagnostics(Camera captureCamera)
        {
            if (captureCamera == null)
            {
                _debugCaptureCameraPositionWS = Vector3.zero;
                _debugCaptureCameraNear = 0f;
                _debugCaptureCameraFar = 0f;
                _debugCaptureCameraOrthoSize = 0f;
                return;
            }

            _debugCaptureCameraPositionWS = captureCamera.transform.position;
            _debugCaptureCameraNear = captureCamera.nearClipPlane;
            _debugCaptureCameraFar = captureCamera.farClipPlane;
            _debugCaptureCameraOrthoSize = captureCamera.orthographicSize;
        }

        private static int ResolveCaptureLayerMask()
        {
            int resolvedMask = HectonLayerMasks.TerrainLayerMask;
            if (resolvedMask != 0)
                return resolvedMask;

            int fallbackMask = DefaultCaptureLayerMask;
            int terrainLayer = TerrainLayer;
            if (terrainLayer < 0)
                terrainLayer = TerrainLayerWithTrailingSpace;

            if (terrainLayer >= 0)
                fallbackMask |= 1 << terrainLayer;

            return fallbackMask;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            terrainBoundsPadding = Mathf.Max(0f, terrainBoundsPadding);
            tidalHeightCacheAmplitudeMeters = Mathf.Max(0f, tidalHeightCacheAmplitudeMeters);
        }
#endif

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
            return (float)(runtimeY + HectonFloatingOrigin.CurrentTotalOffsetDouble.y);
        }
    }
}
