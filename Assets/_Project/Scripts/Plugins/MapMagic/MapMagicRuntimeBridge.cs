// ============================================================================
// HECTON-8 — MapMagicBridge.cs
// Informatsionnyy sloy mezhdu igrovymi sistemami i MapMagic 2.1.18.
//
// ═══════════════════════════════════════════════════════════════
// v3.1 — BULLETPROOF BIOME FALLBACK
// ═══════════════════════════════════════════════════════════════
//
// IZMENENIYa v3.1:
//   [FIX] TryGetBiomeIndex: dobavleny dopolnitelnye safety checks:
//     • terrainData.alphamapTextureCount proveryaetsya DO obrascheniya k
//       alphamapTextures (predotvraschaet IndexOutOfRange na pustyh terrain).
//     • Esli alphamapLayers == 0 → biomeIndex = 0, return false.
//     • Esli vse tekstury null → biomeIndex = 0, return false.
//     • Esli mapMagicObject == null → biomeIndex = 0, return false.
//     Vo VSEH sluchayah biomeIndex garantirovanno = 0 (ne musor).
//
//   [FIX] DetectAndPublishBiome: esli TryGetBiomeIndex vozvraschaet false,
//     biom fiksiruetsya na 0. Esli _lastBiomeID == -1 (pervyy vyzov),
//     MapMagicBiomeEvents.TryRaiseBiomeChanged(0) vyzyvaetsya prinuditelno, chtoby podpischiki
//     (UnderwaterVisuals, AtmosphereManager) poluchili nachalnoe znachenie.
//     Bez etogo pri otsutstvii biomov podpischiki NIKOGDA ne poluchayut
//     sobytie → UnderwaterVisuals ne initsializiruet profil → kresh/artefakty.
//
// PREDYDUSchIE VERSII (sohraneny):
//   v3.0: Zero-GC biome detection via alphamapTextures.
//   v2.0: Biome Event System, ISlowTickable, OnBiomeChanged event.
//   v1.0: Height queries, terrain lookup.
//
// ZERO GC:
//   • TryGetBiomeIndex: GetPixelBilinear — zero GC (Color struct).
//   • alphamapTextures — Unity cached property, zero GC.
//   • TryGetHeight: normalized TerrainData interpolation after cached tile lookup.
//   • FindTerrainAt: cached MapMagic TerrainTile array — no Unity global terrain scan.
//   • SlowTick: no allocations at all.
// ============================================================================

using System;
using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Environment;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using MapMagic.Core;
using MapMagic.Products;
using MapMagic.Terrains;

namespace Hecton8.Core
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-7000)]
    public sealed class MapMagicRuntimeBridge : MapMagicBridge, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const float SceneBindingRefreshInterval = 1f;
        private const int MainTerrainBaseMapResolutionBudget = 512;
        private const int DraftTerrainBaseMapResolutionBudget = 128;
        private const int BiomeMatrixLayerCount = 108;
        private const string TectonicSpineFamilyId = "biome.family.tectonic_spine";
        private const float MatrixBiomeBorderBlendProbeMeters = WorldProceduralFieldSampler.BiomeBorderOverlapMeters;
        private const float DistantTerrainShadowMaskUpdateIntervalSeconds = 2f;
        private const float DistantTerrainShadowSolveBudgetWarningMilliseconds = 0.2f;
        private const int DistantTerrainShadowPerformanceWarningCooldownFrames = 30;
        private const int DistantTerrainShadowMaskMaxResolution = 256;
        private const int BiomeAlphaTextureCacheCapacity = 128;
        private const int BiomeTerrainLayerCacheCapacity = 128;
        private const float DefaultWaterSurfaceLevel = 14.02f;
        private const int TerrainTileCacheCapacity = 512;
        private static readonly int _TerrainFadeDistanceId = Shader.PropertyToID("_FadeDistance");
        private static readonly int _TerrainFadeParamsId = Shader.PropertyToID("_HectonTerrainFadeParams");
        private static readonly int _TerrainFadeRuntimeOriginId = Shader.PropertyToID("_HectonTerrainFadeRuntimeOrigin");
        private static readonly int _TerrainFadeAupOriginId = Shader.PropertyToID("_HectonTerrainFadeAupOrigin");
        private static readonly int _DistantTerrainShadowMaskId = Shader.PropertyToID("_HectonDistantTerrainShadowMask");
        private static readonly int _DistantTerrainShadowRectId = Shader.PropertyToID("_HectonDistantTerrainShadowRect");
        private static readonly int _DistantTerrainShadowParamsId = Shader.PropertyToID("_HectonDistantTerrainShadowParams");
        private static readonly uint _DistantTerrainShadowSolveWarningHash =
            unchecked((uint)Hecton.Localization.LocHash.Compute("MapMagicBridge.DistantTerrainShadowSolveOverBudget"));
        private static readonly uint _MapMagicBridgeTelemetryContextHash =
            unchecked((uint)Hecton.Localization.LocHash.Compute(nameof(MapMagicBridge)));

        // ══════════════════════════════════════════════════════════
        //  RUNTIME AUTHORITY
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Read-only alias to the active R16 terrain height cache owned by HectonMapMagicVegetationBridge.
        /// Consumers must retain CacheRevision and re-query after terrain streaming or origin shifts.
        /// </summary>
        // Runtime data contracts live in the Core MapMagicBridge base.

        // ══════════════════════════════════════════════════════════
        //  GLOBAL EVENT — BIOME CHANGE
        // ══════════════════════════════════════════════════════════
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── MapMagic Reference ────────────────────────")]
        [Tooltip("Ssylka na MapMagicObject v stsene. " +
                 "Esli ne naznachena — naydetsya avtomaticheski.")]
        [SerializeField] private MapMagic.Core.MapMagicObject mapMagicObject;

        [Header("── Water Settings ────────────────────────────")]
        [Tooltip("Uroven poverhnosti vody (mirovaya Y-koordinata). " +
                 "Ispolzuetsya dlya opredeleniya 'pod vodoy'.")]
        [SerializeField] private float waterSurfaceLevel = DefaultWaterSurfaceLevel;

        [Header("── Player Reference ──────────────────────────")]
        [Tooltip("Transform igroka dlya biom-detektsii v SlowTick.\n" +
                 "Esli ne naznachen — ischetsya po tegu 'Player' pri starte.")]
        [SerializeField] private Transform playerTransform;

        [Header("── Biome Detection ───────────────────────────")]
        [Tooltip("Maksimalnoe kolichestvo biomov v Biomes Set MapMagic.\n" +
                 "Opredelyaet limit poiska dominiruyuschego sloya.\n" +
                 "Dolzhno sovpadat s kolichestvom vyhodov Biomes Set nody.")]
        [SerializeField] private int maxBiomeCount = 8;

        [Header("Sandbox Generation")]
        [Tooltip("When enabled, world-gen consumers treat MapMagic terrain as the only terrain authority and skip prebaked scene/fallback terrain reads.")]
        [SerializeField] private bool sandboxProceduralTerrainOnly;
        [Tooltip("When enabled, MapMagic alphamap layers 0..107 are interpreted as HECTON biome matrix IDs 1..108.")]
        [SerializeField] private bool sandboxUseBiomeMatrixAlphamapLayers = true;
        [SerializeField] private bool enableSandboxThermalWeathering = true;
        [SerializeField, Range(0f, 1f)] private float sandboxThermalWeatheringStrength = 0.18f;
        [SerializeField, Range(5f, 60f)] private float sandboxThermalWeatheringTalusAngleDegrees = 32f;
        [SerializeField] private bool enableSandboxTectonicSpineDisplacement = true;
        [SerializeField, Range(0f, 0.35f)] private float sandboxTectonicSpineStrength = 0.12f;
        [SerializeField, Min(0.0001f)] private float sandboxTectonicSpineFrequency = 0.0065f;
        [SerializeField, Range(0.5f, 8f)] private float sandboxTectonicSpineRidgeSharpness = 3.25f;
        [SerializeField] private int sandboxTectonicSpineSeed = 83117;
        [SerializeField] private bool enableSandboxFakeCliffOverhangOffsets = true;
        [SerializeField, Range(60f, 88f)] private float sandboxFakeOverhangSlopeThresholdDegrees = 75f;
        [SerializeField, Range(0f, 2f)] private float sandboxFakeOverhangMaxOffsetMeters = 0.75f;
        [SerializeField, Min(0.0001f)] private float sandboxFakeOverhangNoiseFrequency = 0.085f;
        [SerializeField] private int sandboxFakeOverhangSeed = 42109;

        [Header("Planetary Canvas Shader")]
        [SerializeField] private bool enablePlanetaryCanvasTerrainFade = true;
        [SerializeField, Min(128f)] private float terrainFadeDistanceMeters = 2600f;
        [SerializeField, Min(1f)] private float terrainFadeWidthMeters = 420f;
        [SerializeField, Range(0f, 1f)] private float terrainFadeNoirFogBlend = 0.85f;
        [SerializeField] private bool enableDistantTerrainShadowMask = true;
        [SerializeField, Tooltip("Optional pre-rendered 256x256 canyon darkness mask. When assigned, runtime generation is bypassed.")]
        private Texture2D distantTerrainShadowMaskOverride;
        [SerializeField, Range(32, DistantTerrainShadowMaskMaxResolution)] private int distantTerrainShadowMaskResolution = 256;
        [SerializeField, Min(256f)] private float distantTerrainShadowMaskWorldSize = 4096f;
        [SerializeField, Min(1f)] private float distantTerrainShadowProbeDistanceMeters = 140f;
        [SerializeField, Min(1f)] private float distantTerrainShadowHeightScaleMeters = 90f;
        [SerializeField, Range(0f, 1f)] private float distantTerrainShadowStrength = 0.72f;

        [Header("── Diagnostics ───────────────────────────────")]
#pragma warning disable CS0414
        [SerializeField] private bool _debugMapMagicFound;
        [SerializeField] private int  _debugTileCount;
        [SerializeField] private int  _debugCurrentBiome = -1;
        [SerializeField] private bool _debugPlayerFound;
        [SerializeField] private bool _debugBiomesAvailable;
#pragma warning restore CS0414

        // ══════════════════════════════════════════════════════════
        //  CACHED STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Last known biome ID. Used for edge detection in SlowTick.
        /// -1 = not yet determined (forces first event fire).
        /// </summary>
        private int _lastBiomeID = -1;

        /// <summary>
        /// Registration tracking flag for GameTickManager.
        /// </summary>
        private bool _registeredToTickManager;
        private bool _registeredToLateFrameTickManager;
        private bool _registeredMapMagicRuntime;
        private bool _hotSwapRegistered;
        private bool _pendingPlanetaryTerrainShaderGlobals;
        private bool _pendingRuntimeMapMagicGenerationFence;

        /// <summary>
        /// v3.1: Flag indicating biome detection has been attempted at least once.
        /// If first attempt returns false (no biomes), we force-publish biome 0.
        /// </summary>
        private bool _initialBiomePublished;

        /// <summary>
        /// Cached MapMagic terrain tiles. Uses tile-backed draft terrain when
        /// MapMagic keeps active terrain references null.
        /// </summary>
        private readonly List<TerrainTile> _cachedTerrainTiles = new List<TerrainTile>(TerrainTileCacheCapacity); // COLD ALLOC: tile cache for MapMagic terrain lookup

        /// <summary>
        /// Tracks root child count to avoid reallocating tile cache when the
        /// MapMagic hierarchy has not structurally changed.
        /// </summary>
        private int _cachedTerrainTileRootCount = -1;
        /// <summary>
        /// Last tile resolved for height/biome sampling. Scatter samples cluster
        /// spatially, so this avoids full tile scans on repeated queries.
        /// </summary>
        private TerrainTile _lastResolvedTerrainTile;
        private HectonMapMagicVegetationBridge _cachedVegetationBridge;
        private TerrainData _cachedBiomeTerrainData;
        private Texture2D[] _cachedBiomeAlphaTextures = Array.Empty<Texture2D>();
        private TerrainLayer[] _cachedBiomeTerrainLayers = Array.Empty<TerrainLayer>();
        private int _cachedBiomeAlphaTextureCount = -1;
        private int _cachedBiomeAlphaExpectedTextureCount = -1;
        private int _cachedBiomeTerrainLayerCount = -1;
        private int _cachedBiomeTerrainLayerExpectedCount = -1;

        /// <summary>
        /// Retry gate for recovering lost scene bindings after reload.
        /// </summary>
        private float _nextSceneBindingRefreshTime = float.NegativeInfinity;
        private bool _runtimeTerrainResolutionRepairPending;
        private bool _terrainTileEventsSubscribed;
        private bool _loggedMissingMapMagicBinding;
        private Texture2D _distantTerrainShadowMask;
        private Color32[] _distantTerrainShadowPixels = Array.Empty<Color32>(); // COLD ALLOC: Color32[resolution^2] - CPU staging for distant terrain height-shadow mask - owner: MapMagicBridge
        private int _distantTerrainShadowMaskCapacity;
        private int _distantTerrainShadowMaskAppliedResolution;
        private float _nextDistantTerrainShadowMaskUpdateTime = float.NegativeInfinity;
        private int _nextDistantTerrainShadowPerformanceWarningFrame;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        /// <summary>Uroven poverhnosti vody (Y).</summary>
        public override float WaterSurfaceLevel => SanitizeWaterSurfaceLevel(waterSurfaceLevel);

        /// <summary>MapMagic nayden i dostupen.</summary>
        public override bool IsAvailable => mapMagicObject != null;
        public override Component RuntimeMapMagicObject => mapMagicObject;
        public override bool SandboxProceduralTerrainOnly => sandboxProceduralTerrainOnly;
        public override bool SandboxUseBiomeMatrixAlphamapLayers => sandboxUseBiomeMatrixAlphamapLayers;
        public override bool EnableSandboxThermalWeathering => enableSandboxThermalWeathering;
        public override float SandboxThermalWeatheringStrength => sandboxThermalWeatheringStrength;
        public override float SandboxThermalWeatheringTalusAngleDegrees => sandboxThermalWeatheringTalusAngleDegrees;
        public override bool EnableSandboxTectonicSpineDisplacement => enableSandboxTectonicSpineDisplacement;
        public override float SandboxTectonicSpineStrength => sandboxTectonicSpineStrength;
        public override float SandboxTectonicSpineFrequency => sandboxTectonicSpineFrequency;
        public override float SandboxTectonicSpineRidgeSharpness => sandboxTectonicSpineRidgeSharpness;
        public override uint SandboxTectonicSpineSeed => unchecked((uint)sandboxTectonicSpineSeed);
        public override bool EnableSandboxFakeCliffOverhangOffsets => enableSandboxFakeCliffOverhangOffsets;

        /// <summary>
        /// Current biome ID under the player.
        /// -1 if not yet determined or player not found.
        /// v3.1: After Start(), guaranteed to be >= 0 (at least 0 as fallback).
        /// </summary>
        public override int CurrentBiomeID => _lastBiomeID;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            // ── Singleton ──
            TryResolveCoLocatedMapMagicObject();
            FenceRuntimeMapMagicGenerationIfNeeded();
            ReportMissingMapMagicBindingIfNeeded();

            _runtimeTerrainResolutionRepairPending = !Application.isPlaying && mapMagicObject != null;
            if (!Application.isPlaying)
                EnsureRuntimeTerrainConnectivityCompatibility(forceApplyToCachedTerrains: false);
            RefreshTerrainTileCache(force: true);
            PrewarmDistantTerrainShadowMaskCold();
            PrewarmBiomeTextureCacheStorageCold();
            if (!Application.isPlaying)
            {
                ApplyTerrainDataMemoryBudgetToCachedTerrains();
                RepairRuntimeTerrainResolutionMismatchIfNeeded();
            }

            // ── Poisk igroka ──
            RefreshPlayerTransformFromRegistryCold();

            _lastBiomeID = -1;
            _registeredToTickManager = false;
            _initialBiomePublished = false;

            UpdateDiagnostics();
        }

        // ════════════════════════════════════════════════════════
        // TICK REGISTRATION — Deferred two-phase pattern.
        // ════════════════════════════════════════════════════════

        private void OnEnable()
        {
            RefreshPlayerTransformFromRegistryCold();
            PrewarmDistantTerrainShadowMaskCold();
            PrewarmBiomeTextureCacheStorageCold();
            TrySubscribeTerrainTileEvents();
            TryRegisterHotSwapListener();
            TryRegisterMapMagicRuntime();
            TryRegisterToTickManager();
            TryRegisterToLateFrameTickManager();
        }

        private void Start()
        {
            RefreshPlayerTransformFromRegistryCold();
            TryRegisterMapMagicRuntime();
            TryRegisterToTickManager();
            TryRegisterToLateFrameTickManager();
            UpdateLastResolvedTerrainTileOwnerPhase();
            PrewarmDistantTerrainShadowMaskCold();
            PrewarmBiomeTextureCacheStorageCold();
            PrewarmBiomeAlphaTextureCacheOwnerPhase();

            // ── Initial biome detection ──
            // v3.1: Guaranteed to publish at least biome 0.
            DetectAndPublishBiome();
        }

        private void OnDisable()
        {
            TryUnregisterFromTickManager();
            TryUnregisterFromLateFrameTickManager();
            TryUnregisterMapMagicRuntime();
            TryUnregisterHotSwapListener();
            TryUnsubscribeTerrainTileEvents();
        }

        private void OnDestroy()
        {
            TryUnregisterFromTickManager();
            TryUnregisterFromLateFrameTickManager();
            TryUnregisterMapMagicRuntime();
            TryUnregisterHotSwapListener();
            TryUnsubscribeTerrainTileEvents();
            ReleaseDistantTerrainShadowMask();
        }

        private void TryRegisterMapMagicRuntime()
        {
            if (_registeredMapMagicRuntime || !Application.isPlaying)
                return;

            MapMagicBridge current = ActiveRuntimeInstance;
            if (current == null)
                current = GlobalRegistry.MapMagic;

            if (current != null && !ReferenceEquals(current, this))
            {
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterMapMagicRuntime(this);
            GlobalRegistry.RegisterTerrainProvider(this);
            WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref _cachedVegetationBridge);
            _registeredMapMagicRuntime = ReferenceEquals(GlobalRegistry.MapMagic, this);
            if (_registeredMapMagicRuntime)
                PublishActiveRuntimeInstance();
        }

        private void TryUnregisterMapMagicRuntime()
        {
            ClearActiveRuntimeInstance();
            WorldRuntimeReferenceUtility.InvalidateMapMagicBridgeCache(this);
            if (!_registeredMapMagicRuntime)
                return;

            if (ReferenceEquals(GlobalRegistry.MapMagic, this))
                GlobalRegistry.UnregisterMapMagicRuntime(this);

            if (ReferenceEquals(GlobalRegistry.Terrain, this))
                GlobalRegistry.UnregisterTerrainProvider(this);

            _registeredMapMagicRuntime = false;
        }

        private void TryRegisterToTickManager()
        {
            if (_registeredToTickManager || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registeredToTickManager = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterFromTickManager()
        {
            if (!_registeredToTickManager)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registeredToTickManager = false;
        }

        private void TryRegisterToLateFrameTickManager()
        {
            if (_registeredToLateFrameTickManager || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registeredToLateFrameTickManager = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterFromLateFrameTickManager()
        {
            if (!_registeredToLateFrameTickManager)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredToLateFrameTickManager = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.MapMagicVegetationRuntime)
            {
                _cachedVegetationBridge = currentService as HectonMapMagicVegetationBridge;
                WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref _cachedVegetationBridge);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                RebindPlayerTransform(
                    previousService as IPlayerRuntimeContext,
                    currentService as IPlayerRuntimeContext);
                UpdateDiagnostics();
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher && serviceSlot != GlobalRegistryServiceSlot.TickManager)
                return;

            TryUnregisterFromTickManager();
            TryUnregisterFromLateFrameTickManager();
            if (currentService == null || !isActiveAndEnabled)
                return;

            TryRegisterToTickManager();
            TryRegisterToLateFrameTickManager();
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
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

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable — BIOME DETECTION (2 Hz)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Called by GameTickManager at slowTickInterval (~0.5s).
        /// v3.1: Zero GC. Guaranteed biome fallback.
        /// </summary>
        public override void SlowTick()
        {
            RefreshRuntimeSceneBindingDiagnostics();
            ValidateTerrainTileCacheOwnerPhase();
            UpdateLastResolvedTerrainTileOwnerPhase();
            PrewarmBiomeAlphaTextureCacheOwnerPhase();
            DetectAndPublishBiome();
            QueuePlanetaryTerrainShaderGlobals();
        }

        public void LateFrameTick()
        {
            if (_pendingRuntimeMapMagicGenerationFence)
            {
                _pendingRuntimeMapMagicGenerationFence = false;
                FenceRuntimeMapMagicGenerationImmediate();
            }

            if (_pendingPlanetaryTerrainShaderGlobals)
            {
                _pendingPlanetaryTerrainShaderGlobals = false;
                PublishPlanetaryTerrainShaderGlobals();
            }
        }

        private void QueuePlanetaryTerrainShaderGlobals()
        {
            _pendingPlanetaryTerrainShaderGlobals = true;
        }

        private void PublishPlanetaryTerrainShaderGlobals()
        {
            if (!enablePlanetaryCanvasTerrainFade)
            {
                Shader.SetGlobalFloat(_TerrainFadeDistanceId, 0f);
                Shader.SetGlobalVector(_TerrainFadeParamsId, Vector4.zero);
                Shader.SetGlobalVector(_DistantTerrainShadowParamsId, Vector4.zero);
                return;
            }

            if (playerTransform == null)
            {
                Shader.SetGlobalFloat(_TerrainFadeDistanceId, 0f);
                Shader.SetGlobalVector(_TerrainFadeParamsId, Vector4.zero);
                Shader.SetGlobalVector(_DistantTerrainShadowParamsId, Vector4.zero);
                return;
            }

            Vector3 runtimeOrigin = playerTransform.position;
            double3 aupOrigin = TryResolveAupDoubleFromRuntimeOrigin(runtimeOrigin, out double3 resolvedAupOrigin)
                ? resolvedAupOrigin
                : double3.zero;
            float safeFadeDistance = Mathf.Max(128f, terrainFadeDistanceMeters);
            float safeFadeWidth = Mathf.Max(1f, terrainFadeWidthMeters);

            Shader.SetGlobalFloat(_TerrainFadeDistanceId, safeFadeDistance);
            Shader.SetGlobalVector(
                _TerrainFadeParamsId,
                new Vector4(safeFadeDistance, 1f / safeFadeWidth, 1f, Mathf.Clamp01(terrainFadeNoirFogBlend)));
            Shader.SetGlobalVector(
                _TerrainFadeRuntimeOriginId,
                new Vector4(runtimeOrigin.x, runtimeOrigin.y, runtimeOrigin.z, 1f));
            Shader.SetGlobalVector(
                _TerrainFadeAupOriginId,
                new Vector4((float)aupOrigin.x, (float)aupOrigin.y, (float)aupOrigin.z, 1f));

            UpdateDistantTerrainShadowMask(runtimeOrigin);
        }

        private static bool TryResolveAupDoubleFromRuntimeOrigin(Vector3 runtimePosition, out double3 absoluteAup)
        {
            absoluteAup = default;
            float3 runtimeMeters = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(runtimeMeters)))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            AbsoluteUniversePosition positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            if (!positionAup.IsFinite())
                return false;

            absoluteAup = positionAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(absoluteAup));
        }

        private void UpdateDistantTerrainShadowMask(Vector3 runtimeCenter)
        {
            if (!enableDistantTerrainShadowMask || mapMagicObject == null)
            {
                Shader.SetGlobalVector(_DistantTerrainShadowParamsId, Vector4.zero);
                return;
            }

            if (distantTerrainShadowMaskOverride != null)
            {
                PublishDistantTerrainShadowMaskGlobals(runtimeCenter);
                return;
            }

            if (_distantTerrainShadowMask != null &&
                Time.time < _nextDistantTerrainShadowMaskUpdateTime)
            {
                PublishDistantTerrainShadowMaskGlobals(runtimeCenter);
                return;
            }

            int resolution = Mathf.Clamp(distantTerrainShadowMaskResolution, 32, DistantTerrainShadowMaskMaxResolution);
            int requiredCapacity = Mathf.Max(1, resolution * resolution);
            if (_distantTerrainShadowMask == null ||
                _distantTerrainShadowPixels == null ||
                _distantTerrainShadowMaskCapacity != requiredCapacity ||
                _distantTerrainShadowPixels.Length != requiredCapacity)
            {
                Shader.SetGlobalVector(_DistantTerrainShadowParamsId, Vector4.zero);
                return;
            }

            float worldSize = Mathf.Max(256f, distantTerrainShadowMaskWorldSize);
            float halfSize = worldSize * 0.5f;
            float minX = runtimeCenter.x - halfSize;
            float minZ = runtimeCenter.z - halfSize;
            float texelSize = worldSize / Mathf.Max(1, resolution);
            float invWorldSize = 1f / worldSize;
            Vector2 lightDirection = ResolveDistantShadowDirectionXZ();
            float cinematicRidgeScale = math.saturate(Mathf.Max(1f, distantTerrainShadowProbeDistanceMeters) / 16f);
            float cinematicShadowCompression = math.saturate(24f / Mathf.Max(1f, distantTerrainShadowHeightScaleMeters));
            long solveStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            int pixelIndex = 0;

            for (int z = 0; z < resolution; z++)
            {
                float sampleZ = minZ + (z + 0.5f) * texelSize;
                float normalizedZ = (sampleZ - runtimeCenter.z) * invWorldSize + 0.5f;
                for (int x = 0; x < resolution; x++)
                {
                    float sampleX = minX + (x + 0.5f) * texelSize;
                    float normalizedX = (sampleX - runtimeCenter.x) * invWorldSize + 0.5f;
                    float occlusion = ResolveCinematicDistantTerrainShadow01(
                        normalizedX,
                        normalizedZ,
                        lightDirection,
                        cinematicRidgeScale,
                        cinematicShadowCompression);

                    byte packed = (byte)Mathf.Clamp(Mathf.RoundToInt(occlusion * 255f), 0, 255);
                    _distantTerrainShadowPixels[pixelIndex] = new Color32(packed, packed, packed, 255);
                    pixelIndex++;
                }
            }

            _distantTerrainShadowMask.SetPixelData(_distantTerrainShadowPixels, 0);
            _distantTerrainShadowMask.Apply(false, false);
            _distantTerrainShadowMaskAppliedResolution = resolution;
            _nextDistantTerrainShadowMaskUpdateTime = Time.time + DistantTerrainShadowMaskUpdateIntervalSeconds;
            PublishDistantTerrainShadowMaskGlobals(runtimeCenter);
            PublishDistantTerrainShadowSolveWarningIfNeeded(solveStartTicks);
        }

        private void PrewarmDistantTerrainShadowMaskCold()
        {
            if (!enableDistantTerrainShadowMask || distantTerrainShadowMaskOverride != null)
                return;

            int resolution = Mathf.Clamp(distantTerrainShadowMaskResolution, 32, DistantTerrainShadowMaskMaxResolution);
            EnsureDistantTerrainShadowMaskCapacity(resolution);
        }

        private static float ResolveCinematicDistantTerrainShadow01(
            float normalizedX,
            float normalizedZ,
            Vector2 lightDirection,
            float ridgeScale,
            float shadowCompression)
        {
            float centeredX = normalizedX - 0.5f;
            float centeredZ = normalizedZ - 0.5f;
            float directional = centeredX * lightDirection.x + centeredZ * lightDirection.y;
            float cross = centeredX * lightDirection.y - centeredZ * lightDirection.x;
            float broadShelf = math.saturate(0.52f + directional * math.lerp(1.04f, 1.48f, shadowCompression));
            float ridgeBand = math.saturate(1f - math.abs(cross) * math.lerp(4.2f, 7.4f, ridgeScale));
            float cellHash = math.frac((normalizedX * 173.31f + normalizedZ * 91.17f) * (normalizedX * 13.13f + normalizedZ * 7.71f + 0.17f));
            float fracturedNoise = math.saturate((cellHash - 0.42f) * 0.18f);
            return math.saturate(broadShelf * math.lerp(0.44f, 0.62f, shadowCompression) + ridgeBand * 0.30f + fracturedNoise);
        }

        private void PublishDistantTerrainShadowSolveWarningIfNeeded(long solveStartTicks)
        {
            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - solveStartTicks;
            float elapsedMilliseconds = (float)(elapsedTicks * 1000.0d / System.Diagnostics.Stopwatch.Frequency);
            if (elapsedMilliseconds <= DistantTerrainShadowSolveBudgetWarningMilliseconds ||
                SystemDispatcher.CurrentFrameIndex < _nextDistantTerrainShadowPerformanceWarningFrame)
            {
                return;
            }

            GlobalTelemetryBus.PublishPerformanceWarning(
                _DistantTerrainShadowSolveWarningHash,
                _MapMagicBridgeTelemetryContextHash,
                elapsedMilliseconds);
            _nextDistantTerrainShadowPerformanceWarningFrame =
                SystemDispatcher.CurrentFrameIndex + DistantTerrainShadowPerformanceWarningCooldownFrames;
        }

        private void EnsureDistantTerrainShadowMaskCapacity(int resolution)
        {
            int requiredCapacity = Mathf.Max(1, resolution * resolution);
            if (_distantTerrainShadowMask != null &&
                _distantTerrainShadowMaskCapacity == requiredCapacity &&
                _distantTerrainShadowPixels != null &&
                _distantTerrainShadowPixels.Length == requiredCapacity)
            {
                return;
            }

            ReleaseDistantTerrainShadowMask();

            _distantTerrainShadowMaskCapacity = requiredCapacity;
            _distantTerrainShadowPixels = new Color32[requiredCapacity]; // COLD ALLOC: Color32[resolution^2] - CPU staging for distant terrain height-shadow mask - owner: MapMagicBridge
            _distantTerrainShadowMask = new Texture2D(
                resolution,
                resolution,
                TextureFormat.RGBA32,
                mipChain: false,
                linear: true)
            {
                name = "__HectonDistantTerrainShadowMask",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            }; // COLD ALLOC: Texture2D[1] - low-res distant terrain shadow mask for noir horizon fade - owner: MapMagicBridge
        }

        private void PublishDistantTerrainShadowMaskGlobals(Vector3 runtimeCenter)
        {
            Texture2D effectiveMask = distantTerrainShadowMaskOverride != null
                ? distantTerrainShadowMaskOverride
                : _distantTerrainShadowMask;
            int effectiveResolution = distantTerrainShadowMaskOverride != null
                ? Mathf.Min(distantTerrainShadowMaskOverride.width, distantTerrainShadowMaskOverride.height)
                : _distantTerrainShadowMaskAppliedResolution;
            if (effectiveMask == null || effectiveResolution <= 0)
            {
                Shader.SetGlobalVector(_DistantTerrainShadowParamsId, Vector4.zero);
                return;
            }

            float worldSize = Mathf.Max(256f, distantTerrainShadowMaskWorldSize);
            float halfSize = worldSize * 0.5f;
            Shader.SetGlobalTexture(_DistantTerrainShadowMaskId, effectiveMask);
            Shader.SetGlobalVector(
                _DistantTerrainShadowRectId,
                new Vector4(runtimeCenter.x - halfSize, runtimeCenter.z - halfSize, 1f / worldSize, 1f / worldSize));
            Shader.SetGlobalVector(
                _DistantTerrainShadowParamsId,
                new Vector4(Mathf.Clamp01(distantTerrainShadowStrength), effectiveResolution, 1f, 0f));
        }

        private static Vector2 ResolveDistantShadowDirectionXZ()
        {
            Light sun = RenderSettings.sun;
            Vector3 forward = sun != null
                ? sun.transform.forward
                : new Vector3(0.42f, -0.64f, 0.63f);
            Vector2 direction = new Vector2(forward.x, forward.z);
            if (direction.sqrMagnitude < 0.0001f)
                direction = new Vector2(0.42f, 0.63f);

            return ResolveOctantDirectionXZ(direction.x, direction.y);
        }

        private void ReleaseDistantTerrainShadowMask()
        {
            if (_distantTerrainShadowMask != null)
            {
                if (Application.isPlaying)
                    Destroy(_distantTerrainShadowMask);
                else
                    DestroyImmediate(_distantTerrainShadowMask);

                _distantTerrainShadowMask = null;
            }

            _distantTerrainShadowPixels = Array.Empty<Color32>();
            _distantTerrainShadowMaskCapacity = 0;
            _distantTerrainShadowMaskAppliedResolution = 0;
            Shader.SetGlobalVector(_DistantTerrainShadowParamsId, Vector4.zero);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — BIOME DETECTION + EVENT PUBLISHING (v3.1)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Core biome detection logic.
        ///
        /// v3.1 CHANGES:
        ///   If TryGetBiomeIndex returns false (no terrain, no biomes,
        ///   MapMagic not ready), we fallback to biome 0.
        ///
        ///   On FIRST call (_initialBiomePublished == false):
        ///     Always publish biome 0 via OnBiomeChanged, even if
        ///     detection succeeded with biome 0. This guarantees that
        ///     UnderwaterVisuals and AtmosphereManager receive their
        ///     initial biome event and initialize correctly.
        ///
        ///   Without this: if MapMagic has no biomes configured,
        ///     OnBiomeChanged NEVER fires → UnderwaterVisuals never
        ///     initializes its target profile → fog/ambient stay at
        ///     hardcoded defaults → black terrain / wrong fog.
        /// </summary>
        private void DetectAndPublishBiome()
        {
            if (playerTransform == null) return;

            float3 pos = playerTransform.position;

            int biomeID;

            if (!TryGetBiomeIndex(pos.x, pos.z, out biomeID))
            {
                // v3.1: No biomes available — fallback to 0
                biomeID = 0;

#if UNITY_EDITOR
                _debugBiomesAvailable = false;
#endif
            }
#if UNITY_EDITOR
            else
            {
                _debugBiomesAvailable = true;
            }
#endif

            // ── v3.1: Force initial publish ──
            // First call MUST publish to initialize all subscribers,
            // even if biomeID == 0 (which is the "unchanged" default).
            if (!_initialBiomePublished)
            {
                _initialBiomePublished = true;
                _lastBiomeID = biomeID;

                MapMagicBiomeEvents.TryRaiseBiomeChanged(biomeID);

                UpdateBiomeDiagnostics(biomeID);
                return;
            }

            // ── Edge detection: only fire on change ──
            if (biomeID == _lastBiomeID)
                return;

            _lastBiomeID = biomeID;

            MapMagicBiomeEvents.TryRaiseBiomeChanged(biomeID);

            UpdateBiomeDiagnostics(biomeID);
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — HEIGHT QUERY
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Vozvraschaet vysotu terreyna (dna) v mirovyh koordinatah.
        /// ZERO GC: SampleHeight returns float (struct).
        /// </summary>
        public override bool TryGetHeight(float x, float z, out float height)
        {
            height = 0f;

            HectonMapMagicVegetationBridge vegetationBridge = _cachedVegetationBridge;
            if (vegetationBridge != null && vegetationBridge.TryGetCachedTerrainHeight(x, z, out height))
                return true;

            if (mapMagicObject == null)
                return false;

            Terrain terrain = FindTerrainAt(x, z);

            if (terrain == null || terrain.terrainData == null)
                return false;

            TerrainData terrainData = terrain.terrainData;
            Vector3 terrainPosition = terrain.transform.position;

            Vector3 terrainSize = terrainData.size;
            if (terrainSize.x <= 0f || terrainSize.z <= 0f)
                return false;

            float normalizedX = math.saturate((x - terrainPosition.x) / terrainSize.x);
            float normalizedZ = math.saturate((z - terrainPosition.z) / terrainSize.z);
            float interpolatedHeight = terrainData.GetInterpolatedHeight(normalizedX, normalizedZ);
            height = interpolatedHeight + terrainPosition.y;

            return true;
        }

        /// <summary>
        /// Samples the authoritative Unity Terrain normal from the resolved MapMagic tile.
        /// ZERO GC: uses cached TerrainTile lookup and TerrainData.GetInterpolatedNormal.
        /// </summary>
        public override bool TryGetNormal(float x, float z, float sampleDistance, out Vector3 normal)
        {
            normal = Vector3.up;

            if (mapMagicObject != null)
            {
                Terrain terrain = FindTerrainAt(x, z);
                if (terrain != null && terrain.terrainData != null)
                {
                    TerrainData terrainData = terrain.terrainData;
                    Vector3 terrainSize = terrainData.size;
                    if (terrainSize.x > 0f && terrainSize.z > 0f)
                    {
                        Vector3 terrainPosition = terrain.transform.position;
                        float normalizedX = math.saturate((x - terrainPosition.x) / terrainSize.x);
                        float normalizedZ = math.saturate((z - terrainPosition.z) / terrainSize.z);
                        Vector3 localNormal = terrainData.GetInterpolatedNormal(normalizedX, normalizedZ);
                        if (localNormal.sqrMagnitude > 0.0001f)
                        {
                            Vector3 worldNormal = terrain.transform.TransformDirection(localNormal);
                            if (worldNormal.sqrMagnitude > 0.0001f)
                            {
                                normal = NormalizeFastOrDefault(worldNormal, Vector3.up);
                                return true;
                            }
                        }
                    }
                }
            }

            return TryGetGradientNormal(x, z, sampleDistance, out normal);
        }

        private bool TryGetGradientNormal(float x, float z, float sampleDistance, out Vector3 normal)
        {
            normal = Vector3.up;
            if (!TryGetHeight(x, z, out float centerHeight))
                return false;

            float probe = Mathf.Max(0.25f, sampleDistance);
            bool hasWest = TryGetHeight(x - probe, z, out float westHeight);
            bool hasEast = TryGetHeight(x + probe, z, out float eastHeight);
            bool hasSouth = TryGetHeight(x, z - probe, out float southHeight);
            bool hasNorth = TryGetHeight(x, z + probe, out float northHeight);

            if (!hasWest) westHeight = centerHeight;
            if (!hasEast) eastHeight = centerHeight;
            if (!hasSouth) southHeight = centerHeight;
            if (!hasNorth) northHeight = centerHeight;

            if (!hasWest && !hasEast && !hasSouth && !hasNorth)
                return false;

            Vector3 tangentX = new Vector3(probe * 2f, eastHeight - westHeight, 0f);
            Vector3 tangentZ = new Vector3(0f, northHeight - southHeight, probe * 2f);
            Vector3 sampledNormal = Vector3.Cross(tangentZ, tangentX);
            if (sampledNormal.sqrMagnitude <= 0.0001f)
                return false;

            normal = NormalizeFastOrDefault(sampledNormal, Vector3.up);
            return true;
        }

        private static Vector2 ResolveOctantDirectionXZ(float x, float y)
        {
            float absX = math.abs(x);
            float absY = math.abs(y);
            if (absX <= 0.000001f && absY <= 0.000001f)
                return new Vector2(0.70710677f, 0.70710677f);

            float signX = x < 0f ? -1f : 1f;
            float signY = y < 0f ? -1f : 1f;
            float minor = math.min(absX, absY);
            float major = math.max(absX, absY);
            if (minor * 2f >= major)
                return new Vector2(signX * 0.70710677f, signY * 0.70710677f);

            return absX >= absY ? new Vector2(signX, 0f) : new Vector2(0f, signY);
        }

        private static Vector3 NormalizeFastOrDefault(Vector3 value, Vector3 fallback)
        {
            float lengthSq = value.x * value.x + value.y * value.y + value.z * value.z;
            return lengthSq > 0.0001f ? value * math.rsqrt(lengthSq) : fallback;
        }

        /// <summary>
        /// Resolves terrain height from an absolute-universe position so long-running async voxel pipelines
        /// do not sample stale runtime coordinates after floating-origin shifts.
        /// </summary>
        public override bool TryGetHeightAUP(Vector3 absoluteUniversePosition, out float height)
        {
            return TryGetHeightAUP(
                new double3(absoluteUniversePosition.x, absoluteUniversePosition.y, absoluteUniversePosition.z),
                out height);
        }

        public override bool TryGetHeightAUP(in AbsoluteUniversePosition absoluteUniversePosition, out float height)
        {
            height = 0f;
            return absoluteUniversePosition.IsFinite() &&
                   TryGetHeightAUP(absoluteUniversePosition.ToAbsoluteDouble3(), out height);
        }

        private bool TryGetHeightAUP(double3 absoluteUniversePosition, out float height)
        {
            height = 0f;
            if (mapMagicObject == null || !math.all(math.isfinite(absoluteUniversePosition)))
                return false;

            Terrain terrain = FindTerrainAtAUP(
                absoluteUniversePosition,
                out Vector3 terrainRuntimePosition,
                out Vector3 terrainSize,
                out double3 terrainAbsolutePosition);
            if (terrain == null || terrain.terrainData == null || terrainSize.x <= 0f || terrainSize.z <= 0f)
                return false;

            float normalizedX = math.saturate((float)((absoluteUniversePosition.x - terrainAbsolutePosition.x) / terrainSize.x));
            float normalizedZ = math.saturate((float)((absoluteUniversePosition.z - terrainAbsolutePosition.z) / terrainSize.z));
            float interpolatedHeight = terrain.terrainData.GetInterpolatedHeight(normalizedX, normalizedZ);
            height = interpolatedHeight + terrainRuntimePosition.y;
            return true;
        }

        public override bool TryGetNormalAUP(Vector3 absoluteUniversePosition, float sampleDistance, out Vector3 normal)
        {
            return TryGetNormalAUP(
                new double3(absoluteUniversePosition.x, absoluteUniversePosition.y, absoluteUniversePosition.z),
                sampleDistance,
                out normal);
        }

        public override bool TryGetNormalAUP(in AbsoluteUniversePosition absoluteUniversePosition, float sampleDistance, out Vector3 normal)
        {
            normal = Vector3.up;
            return absoluteUniversePosition.IsFinite() &&
                   TryGetNormalAUP(absoluteUniversePosition.ToAbsoluteDouble3(), sampleDistance, out normal);
        }

        private bool TryGetNormalAUP(double3 absoluteUniversePosition, float sampleDistance, out Vector3 normal)
        {
            normal = Vector3.up;
            if (mapMagicObject == null || !math.all(math.isfinite(absoluteUniversePosition)))
                return false;

            Terrain terrain = FindTerrainAtAUP(
                absoluteUniversePosition,
                out _,
                out Vector3 terrainSize,
                out double3 terrainAbsolutePosition);
            if (terrain == null || terrain.terrainData == null || terrainSize.x <= 0f || terrainSize.z <= 0f)
                return false;

            float normalizedX = math.saturate((float)((absoluteUniversePosition.x - terrainAbsolutePosition.x) / terrainSize.x));
            float normalizedZ = math.saturate((float)((absoluteUniversePosition.z - terrainAbsolutePosition.z) / terrainSize.z));
            Vector3 localNormal = terrain.terrainData.GetInterpolatedNormal(normalizedX, normalizedZ);
            if (localNormal.sqrMagnitude <= 0.0001f)
                return false;

            Vector3 worldNormal = terrain.transform.TransformDirection(localNormal);
            if (worldNormal.sqrMagnitude <= 0.0001f)
                return false;

            normal = NormalizeFastOrDefault(worldNormal, Vector3.up);
            return true;
        }

        public override bool TryGetActiveQuantizedHeightmapPayload(out QuantizedHeightmapPayload payload)
        {
            payload = default;
            HectonMapMagicVegetationBridge vegetationBridge = _cachedVegetationBridge;
            if (vegetationBridge == null ||
                !vegetationBridge.TryGetActiveHeightSamplePayload(out HectonMapMagicVegetationBridge.TerrainHeightSamplePayload sourcePayload))
            {
                return false;
            }

            payload = new QuantizedHeightmapPayload(
                sourcePayload.HeightSamples,
                sourcePayload.TerrainPosition,
                sourcePayload.TerrainSize,
                sourcePayload.HeightmapResolution,
                sourcePayload.CacheRevision);
            return QuantizedHeightmapPayload.IsValid(in payload);
        }

        public override bool TryGetQuantizedHeightmapPayload(float x, float z, out QuantizedHeightmapPayload payload)
        {
            payload = default;
            HectonMapMagicVegetationBridge vegetationBridge = _cachedVegetationBridge;
            if (vegetationBridge == null ||
                !vegetationBridge.TryGetHeightSamplePayload(x, z, out HectonMapMagicVegetationBridge.TerrainHeightSamplePayload sourcePayload))
            {
                return false;
            }

            payload = new QuantizedHeightmapPayload(
                sourcePayload.HeightSamples,
                sourcePayload.TerrainPosition,
                sourcePayload.TerrainSize,
                sourcePayload.HeightmapResolution,
                sourcePayload.CacheRevision);
            return QuantizedHeightmapPayload.IsValid(in payload);
        }

        public override bool TryGetQuantizedHeightmapPayloadAUP(Vector3 absoluteUniversePosition, out QuantizedHeightmapPayload payload)
        {
            return TryGetQuantizedHeightmapPayloadAUP(
                new double3(absoluteUniversePosition.x, absoluteUniversePosition.y, absoluteUniversePosition.z),
                out payload);
        }

        public override bool TryGetQuantizedHeightmapPayloadAUP(in AbsoluteUniversePosition absoluteUniversePosition, out QuantizedHeightmapPayload payload)
        {
            payload = default;
            return absoluteUniversePosition.IsFinite() &&
                   TryGetQuantizedHeightmapPayloadAUP(absoluteUniversePosition.ToAbsoluteDouble3(), out payload);
        }

        private bool TryGetQuantizedHeightmapPayloadAUP(double3 absoluteUniversePosition, out QuantizedHeightmapPayload payload)
        {
            payload = default;
            if (mapMagicObject == null || !math.all(math.isfinite(absoluteUniversePosition)))
                return false;

            Terrain terrain = FindTerrainAtAUP(
                absoluteUniversePosition,
                out Vector3 terrainRuntimePosition,
                out Vector3 terrainSize,
                out double3 terrainAbsolutePosition);
            if (terrain == null || terrain.terrainData == null || terrainSize.x <= 0f || terrainSize.z <= 0f)
                return false;

            float runtimeX = terrainRuntimePosition.x + (float)(absoluteUniversePosition.x - terrainAbsolutePosition.x);
            float runtimeZ = terrainRuntimePosition.z + (float)(absoluteUniversePosition.z - terrainAbsolutePosition.z);
            return TryGetQuantizedHeightmapPayload(runtimeX, runtimeZ, out payload);
        }

        public override bool TryGetTerrainSplatColorAUP(Vector3 absoluteUniversePosition, out Color color, out float confidence)
        {
            return TryGetTerrainSplatColorAUP(
                new double3(absoluteUniversePosition.x, absoluteUniversePosition.y, absoluteUniversePosition.z),
                out color,
                out confidence);
        }

        public override bool TryGetTerrainSplatColorAUP(in AbsoluteUniversePosition absoluteUniversePosition, out Color color, out float confidence)
        {
            color = Color.clear;
            confidence = 0f;
            return absoluteUniversePosition.IsFinite() &&
                   TryGetTerrainSplatColorAUP(absoluteUniversePosition.ToAbsoluteDouble3(), out color, out confidence);
        }

        private bool TryGetTerrainSplatColorAUP(double3 absoluteUniversePosition, out Color color, out float confidence)
        {
            color = Color.clear;
            confidence = 0f;

            if (mapMagicObject == null || !math.all(math.isfinite(absoluteUniversePosition)))
                return false;

            Terrain terrain = FindTerrainAtAUP(
                absoluteUniversePosition,
                out _,
                out Vector3 terrainSize,
                out double3 terrainAbsolutePosition);
            if (terrain == null || terrain.terrainData == null || terrainSize.x <= 0f || terrainSize.z <= 0f)
                return false;

            float u = math.saturate((float)((absoluteUniversePosition.x - terrainAbsolutePosition.x) / terrainSize.x));
            float v = math.saturate((float)((absoluteUniversePosition.z - terrainAbsolutePosition.z) / terrainSize.z));
            return TryGetTerrainSplatColorAtUv(terrain.terrainData, u, v, out color, out confidence);
        }

        public override bool TryGetTerrainSplatColor(float x, float z, out Color color, out float confidence)
        {
            color = Color.clear;
            confidence = 0f;

            if (mapMagicObject == null)
                return false;

            Terrain terrain = FindTerrainAt(x, z);
            if (terrain == null || terrain.terrainData == null)
                return false;

            Vector3 terrainPosition = terrain.transform.position;
            Vector3 terrainSize = terrain.terrainData.size;
            if (terrainSize.x <= 0f || terrainSize.z <= 0f)
                return false;

            float u = math.saturate((x - terrainPosition.x) / terrainSize.x);
            float v = math.saturate((z - terrainPosition.z) / terrainSize.z);
            return TryGetTerrainSplatColorAtUv(terrain.terrainData, u, v, out color, out confidence);
        }

        private bool TryGetTerrainSplatColorAtUv(TerrainData terrainData, float u, float v, out Color color, out float confidence)
        {
            color = Color.clear;
            confidence = 0f;
            if (terrainData == null)
                return false;

            int totalLayers = terrainData.alphamapLayers;
            int textureCount = terrainData.alphamapTextureCount;
            if (totalLayers <= 0 || textureCount <= 0)
                return false;

            if (!TryGetCachedBiomeAlphaTextures(terrainData, textureCount, out Texture2D[] alphaTextures))
                return false;

            TryGetCachedBiomeTerrainLayers(terrainData, totalLayers, out TerrainLayer[] terrainLayers);
            float3 accumulated = float3.zero;
            float totalWeight = 0f;
            float maxWeight = 0f;

            for (int textureIndex = 0; textureIndex < textureCount; textureIndex++)
            {
                Texture2D alphaTexture = alphaTextures[textureIndex];
                if (alphaTexture == null)
                    continue;

                float4 weights = SampleAlphaTextureBilinear01(alphaTexture, u, v);
                int baseLayer = textureIndex * 4;
                AccumulateLayerColor(baseLayer, weights.x, totalLayers, terrainLayers, ref accumulated, ref totalWeight, ref maxWeight);
                AccumulateLayerColor(baseLayer + 1, weights.y, totalLayers, terrainLayers, ref accumulated, ref totalWeight, ref maxWeight);
                AccumulateLayerColor(baseLayer + 2, weights.z, totalLayers, terrainLayers, ref accumulated, ref totalWeight, ref maxWeight);
                AccumulateLayerColor(baseLayer + 3, weights.w, totalLayers, terrainLayers, ref accumulated, ref totalWeight, ref maxWeight);
            }

            if (totalWeight <= 0.0001f)
                return false;

            float3 resolved = math.saturate(accumulated / totalWeight);
            color = new Color(resolved.x, resolved.y, resolved.z, math.saturate(maxWeight));
            confidence = math.saturate(totalWeight);
            return true;
        }

        /// <summary>
        /// Returns MapMagic terrain height for an absolute-universe position.
        /// Fallback is returned when no terrain tile can be resolved.
        /// </summary>
        public override float SampleHeightAUP(Vector3 absoluteUniversePosition, float fallbackHeight = 0f)
        {
            return TryGetHeightAUP(absoluteUniversePosition, out float height)
                ? height
                : fallbackHeight;
        }

        public override float SampleHeightAUP(in AbsoluteUniversePosition absoluteUniversePosition, float fallbackHeight = 0f)
        {
            return TryGetHeightAUP(in absoluteUniversePosition, out float height)
                ? height
                : fallbackHeight;
        }

        /// <summary>
        /// Bystraya versiya bez out. Vozvraschaet 0 pri oshibke.
        /// </summary>
        public override float GetHeight(float x, float z)
        {
            TryGetHeight(x, z, out float h);
            return h;
        }

        public override bool TryResolveTerrainAt(float x, float z, out Terrain terrain)
        {
            terrain = null;

            if (mapMagicObject == null)
                return false;

            terrain = FindTerrainAt(x, z);
            return terrain != null && terrain.terrainData != null;
        }

        public override int CopyResolvedTerrainsTo(Terrain[] destination)
        {
            if (destination == null || destination.Length == 0 || mapMagicObject == null)
                return 0;

            List<TerrainTile> terrainTiles = _cachedTerrainTiles;
            int tileCount = terrainTiles.Count;
            int written = 0;

            for (int i = 0; i < tileCount && written < destination.Length; i++)
            {
                TerrainTile tile = terrainTiles[i];
                Terrain terrain = ResolveTileTerrain(tile);
                if (terrain == null || terrain.terrainData == null)
                    continue;

                bool alreadyWritten = false;
                for (int j = 0; j < written; j++)
                {
                    if (!ReferenceEquals(destination[j], terrain))
                        continue;

                    alreadyWritten = true;
                    break;
                }

                if (alreadyWritten)
                    continue;

                destination[written] = terrain;
                written++;
            }

            return written;
        }

        public override int CopyTerrainTileSnapshotsTo(MapMagicTerrainTileSnapshot[] destination)
        {
            if (destination == null || destination.Length == 0 || mapMagicObject == null)
                return 0;

            List<TerrainTile> terrainTiles = _cachedTerrainTiles;
            int tileCount = terrainTiles.Count;
            int written = 0;
            for (int i = 0; i < tileCount && written < destination.Length; i++)
            {
                if (!TryCreateTerrainTileSnapshot(terrainTiles[i], out MapMagicTerrainTileSnapshot snapshot))
                    continue;

                destination[written] = snapshot;
                written++;
            }

            return written;
        }

        /// <summary>
        /// Proveryaet, nahoditsya li tochka pod vodoy.
        /// </summary>
        public override bool IsUnderwater(float x, float y, float z)
        {
            return y < WaterSurfaceLevel;
        }

        /// <summary>
        /// Kombinirovannaya proverka dlya spavn-sistem.
        /// </summary>
        public override bool IsValidSpawnPoint(
            float x, float y, float z, out float bottomHeight)
        {
            if (!TryGetHeight(x, z, out bottomHeight))
                return false;

            return y < WaterSurfaceLevel && y > bottomHeight;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — BIOME QUERY (v3.1: bulletproof safety)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Vozvraschaet indeks dominiruyuschego bioma v mirovyh koordinatah.
        ///
        /// v3.1 SAFETY CHANGES:
        ///   1. biomeIndex is ALWAYS set to 0 before any checks.
        ///      On ANY failure path, caller gets 0 (not garbage).
        ///   2. alphamapTextureCount is checked BEFORE accessing
        ///      alphamapTextures array (prevents IndexOutOfRange
        ///      on empty/uninitialized terrains).
        ///   3. Null texture entries are skipped gracefully.
        ///   4. If NO valid texture found → return false, biomeIndex=0.
        ///
        /// ZERO GC: all struct math, no allocations.
        /// </summary>
        public override bool TryGetBiomeIndex(
            float x, float z, out int biomeIndex)
        {
            // v3.1: ALWAYS initialize to 0. Never return garbage.
            biomeIndex = 0;

            // ── Guard: MapMagic not available ──
            if (mapMagicObject == null)
                return false;

            // ── Guard: No terrain at this position ──
            Terrain terrain = FindTerrainAt(x, z);
            if (terrain == null)
                return false;

            TerrainData td = terrain.terrainData;
            if (td == null)
                return false;

            // ── Guard: No alphamap layers configured ──
            // This is the primary "no biomes" check.
            // MapMagic with no Biomes Set node → alphamapLayers = 0.
            int totalLayers = td.alphamapLayers;
            if (totalLayers <= 0)
                return false;

            // ── Guard: No alphamap textures ──
            // v3.1: Check alphamapTextureCount BEFORE accessing array.
            // On some terrain configurations, alphamapLayers > 0 but
            // textures aren't generated yet (terrain still generating).
            int textureCount = td.alphamapTextureCount;
            if (textureCount <= 0)
                return false;

            if (!TryGetCachedBiomeAlphaTextures(td, textureCount, out Texture2D[] alphaTextures))
                return false;

            // ── World → normalized UV coordinates [0..1] ──
            Vector3 terrainPos  = terrain.transform.position;
            Vector3 terrainSize = td.size;

            float u = math.saturate((x - terrainPos.x) / terrainSize.x);
            float v = math.saturate((z - terrainPos.z) / terrainSize.z);

            // ── Search for dominant layer ──
            float maxWeight = -1f;
            int   maxIndex  = 0;
            bool  anyValidTexture = false;

            int configuredSearchLimit = sandboxProceduralTerrainOnly && sandboxUseBiomeMatrixAlphamapLayers
                ? math.max(maxBiomeCount, BiomeMatrixLayerCount)
                : maxBiomeCount;
            int searchLimit = math.min(totalLayers, math.max(1, configuredSearchLimit));

            for (int texIdx = 0; texIdx < textureCount; texIdx++)
            {
                Texture2D tex = alphaTextures[texIdx];

                // v3.1: Skip null textures gracefully
                if (tex == null) continue;

                anyValidTexture = true;

                float4 pixel = SampleAlphaTextureBilinear01(tex, u, v);

                int baseLayerIdx = texIdx * 4;

                // Channel R → layer baseLayerIdx + 0
                if (baseLayerIdx < searchLimit)
                {
                    if (pixel.x > maxWeight)
                    {
                        maxWeight = pixel.x;
                        maxIndex  = baseLayerIdx;
                    }
                }

                // Channel G → layer baseLayerIdx + 1
                int layer1 = baseLayerIdx + 1;
                if (layer1 < searchLimit)
                {
                    if (pixel.y > maxWeight)
                    {
                        maxWeight = pixel.y;
                        maxIndex  = layer1;
                    }
                }

                // Channel B → layer baseLayerIdx + 2
                int layer2 = baseLayerIdx + 2;
                if (layer2 < searchLimit)
                {
                    if (pixel.z > maxWeight)
                    {
                        maxWeight = pixel.z;
                        maxIndex  = layer2;
                    }
                }

                // Channel A → layer baseLayerIdx + 3
                int layer3 = baseLayerIdx + 3;
                if (layer3 < searchLimit)
                {
                    if (pixel.w > maxWeight)
                    {
                        maxWeight = pixel.w;
                        maxIndex  = layer3;
                    }
                }

                // Early exit: all layers up to searchLimit checked
                if (layer3 >= searchLimit - 1)
                    break;
            }

            // v3.1: If no valid textures were found, return false
            if (!anyValidTexture)
                return false;

            biomeIndex = maxIndex;
            return true;
        }

        public override bool TryGetMatrixBiomeId(float x, float z, out int matrixBiomeId)
        {
            return TryGetMatrixBiomeId(x, z, out matrixBiomeId, out _);
        }

        public override bool TryGetMatrixBiomeId(float x, float z, out int matrixBiomeId, out int alphamapLayer)
        {
            matrixBiomeId = 0;
            alphamapLayer = -1;

            if (!sandboxProceduralTerrainOnly || !sandboxUseBiomeMatrixAlphamapLayers)
                return false;

            if (!TryGetBiomeIndex(x, z, out int dominantLayer))
                return false;

            if (!TryResolveBiomeMatrixAlphamapLayer(dominantLayer + 1, out int resolvedLayer))
                return false;

            matrixBiomeId = dominantLayer + 1;
            alphamapLayer = resolvedLayer;
            return true;
        }

        public override bool TryGetMatrixBiomeInfluence(
            float x,
            float z,
            out int primaryBiomeId,
            out int secondaryBiomeId,
            out byte blend255,
            out int primaryAlphamapLayer,
            out int secondaryAlphamapLayer)
        {
            primaryBiomeId = 0;
            secondaryBiomeId = 0;
            blend255 = 0;
            primaryAlphamapLayer = -1;
            secondaryAlphamapLayer = -1;

            if (!sandboxProceduralTerrainOnly || !sandboxUseBiomeMatrixAlphamapLayers)
                return false;

            if (!TrySampleTopMatrixBiomeLayers(
                    x,
                    z,
                    out primaryBiomeId,
                    out secondaryBiomeId,
                    out float primaryWeight,
                    out float secondaryWeight,
                    out primaryAlphamapLayer,
                    out secondaryAlphamapLayer))
            {
                return false;
            }

            float centerBlend01 = 0f;
            if (secondaryBiomeId != 0 &&
                secondaryBiomeId != primaryBiomeId &&
                primaryWeight > 0.0001f &&
                secondaryWeight > 0.0001f)
            {
                centerBlend01 = SmoothStep01(secondaryWeight / math.max(0.0001f, primaryWeight + secondaryWeight));
            }

            if (TryResolveMatrixBiomeBorderOverlap(
                    x,
                    z,
                    primaryBiomeId,
                    secondaryBiomeId,
                    centerBlend01,
                    out int borderSecondaryBiomeId,
                    out float borderBlend01,
                    out int borderSecondaryAlphamapLayer) &&
                borderBlend01 > centerBlend01)
            {
                secondaryBiomeId = borderSecondaryBiomeId;
                secondaryAlphamapLayer = borderSecondaryAlphamapLayer;
                centerBlend01 = borderBlend01;
            }

            if (secondaryBiomeId == 0 ||
                secondaryBiomeId == primaryBiomeId ||
                centerBlend01 <= 0.0001f)
            {
                secondaryBiomeId = 0;
                secondaryAlphamapLayer = -1;
                blend255 = 0;
                return true;
            }

            blend255 = (byte)Mathf.Clamp(Mathf.RoundToInt(centerBlend01 * 255f), 0, 255);
            if (blend255 == 0)
            {
                secondaryBiomeId = 0;
                secondaryAlphamapLayer = -1;
            }

            return true;
        }

        private bool TryResolveMatrixBiomeBorderOverlap(
            float x,
            float z,
            int primaryBiomeId,
            int centerSecondaryBiomeId,
            float centerBlend01,
            out int secondaryBiomeId,
            out float blend01,
            out int secondaryAlphamapLayer)
        {
            secondaryBiomeId = centerSecondaryBiomeId;
            blend01 = centerBlend01;
            secondaryAlphamapLayer = centerSecondaryBiomeId > 0 ? centerSecondaryBiomeId - 1 : -1;

            float overlapMeters = Mathf.Max(1f, MatrixBiomeBorderBlendProbeMeters);
            float stepMeters = overlapMeters * 0.25f;
            bool found = false;

            found |= TryResolveMatrixBiomeBorderProbe(x, z, 1f, 0f, stepMeters, overlapMeters, primaryBiomeId, ref secondaryBiomeId, ref blend01, ref secondaryAlphamapLayer);
            found |= TryResolveMatrixBiomeBorderProbe(x, z, -1f, 0f, stepMeters, overlapMeters, primaryBiomeId, ref secondaryBiomeId, ref blend01, ref secondaryAlphamapLayer);
            found |= TryResolveMatrixBiomeBorderProbe(x, z, 0f, 1f, stepMeters, overlapMeters, primaryBiomeId, ref secondaryBiomeId, ref blend01, ref secondaryAlphamapLayer);
            found |= TryResolveMatrixBiomeBorderProbe(x, z, 0f, -1f, stepMeters, overlapMeters, primaryBiomeId, ref secondaryBiomeId, ref blend01, ref secondaryAlphamapLayer);

            const float DiagonalInvLength = 0.70710678118f;
            found |= TryResolveMatrixBiomeBorderProbe(x, z, DiagonalInvLength, DiagonalInvLength, stepMeters, overlapMeters, primaryBiomeId, ref secondaryBiomeId, ref blend01, ref secondaryAlphamapLayer);
            found |= TryResolveMatrixBiomeBorderProbe(x, z, -DiagonalInvLength, DiagonalInvLength, stepMeters, overlapMeters, primaryBiomeId, ref secondaryBiomeId, ref blend01, ref secondaryAlphamapLayer);
            found |= TryResolveMatrixBiomeBorderProbe(x, z, DiagonalInvLength, -DiagonalInvLength, stepMeters, overlapMeters, primaryBiomeId, ref secondaryBiomeId, ref blend01, ref secondaryAlphamapLayer);
            found |= TryResolveMatrixBiomeBorderProbe(x, z, -DiagonalInvLength, -DiagonalInvLength, stepMeters, overlapMeters, primaryBiomeId, ref secondaryBiomeId, ref blend01, ref secondaryAlphamapLayer);

            return found && secondaryBiomeId > 0 && secondaryBiomeId != primaryBiomeId && blend01 > 0.0001f;
        }

        private bool TryResolveMatrixBiomeBorderProbe(
            float x,
            float z,
            float directionX,
            float directionZ,
            float stepMeters,
            float overlapMeters,
            int primaryBiomeId,
            ref int secondaryBiomeId,
            ref float blend01,
            ref int secondaryAlphamapLayer)
        {
            bool found = false;
            for (int step = 1; step <= 4; step++)
            {
                float distanceMeters = stepMeters * step;
                float sampleX = x + directionX * distanceMeters;
                float sampleZ = z + directionZ * distanceMeters;
                if (!TrySampleTopMatrixBiomeLayers(
                        sampleX,
                        sampleZ,
                        out int probePrimaryBiomeId,
                        out int probeSecondaryBiomeId,
                        out float probePrimaryWeight,
                        out float probeSecondaryWeight,
                        out int probePrimaryAlphamapLayer,
                        out int probeSecondaryAlphamapLayer))
                {
                    continue;
                }

                int candidateBiomeId = 0;
                int candidateAlphamapLayer = -1;
                float candidateStrength = 0f;
                if (probePrimaryBiomeId != primaryBiomeId)
                {
                    candidateBiomeId = probePrimaryBiomeId;
                    candidateAlphamapLayer = probePrimaryAlphamapLayer;
                    candidateStrength = math.saturate(probePrimaryWeight);
                }
                else if (probeSecondaryBiomeId != 0 &&
                         probeSecondaryBiomeId != primaryBiomeId &&
                         probeSecondaryWeight > 0.0001f)
                {
                    candidateBiomeId = probeSecondaryBiomeId;
                    candidateAlphamapLayer = probeSecondaryAlphamapLayer;
                    candidateStrength = math.saturate(probeSecondaryWeight);
                }

                if (candidateBiomeId == 0 || candidateBiomeId == primaryBiomeId)
                    continue;

                float candidateBlend =
                    WorldProceduralFieldSampler.EvaluateBiomeBorderSmoothstepBlend01(distanceMeters, overlapMeters) *
                    candidateStrength;
                if (candidateBlend <= blend01)
                    continue;

                secondaryBiomeId = candidateBiomeId;
                secondaryAlphamapLayer = candidateAlphamapLayer;
                blend01 = candidateBlend;
                found = true;
            }

            return found;
        }

        public override bool TryGetMatrixBiomeId(
            float x,
            float z,
            HectonBiomeMatrixCatalog catalog,
            out int matrixBiomeId,
            out int alphamapLayer)
        {
            if (!TryGetMatrixBiomeId(x, z, out matrixBiomeId, out alphamapLayer))
                return false;

            return catalog == null || catalog.GetByMatrixIndex(matrixBiomeId) != null;
        }

        public new static bool TryResolveBiomeMatrixAlphamapLayer(int matrixBiomeId, out int alphamapLayer)
        {
            alphamapLayer = -1;
            if (matrixBiomeId < 1 || matrixBiomeId > BiomeMatrixLayerCount)
                return false;

            alphamapLayer = matrixBiomeId - 1;
            return true;
        }

        private bool TrySampleTopMatrixBiomeLayers(
            float x,
            float z,
            out int primaryBiomeId,
            out int secondaryBiomeId,
            out float primaryWeight,
            out float secondaryWeight,
            out int primaryAlphamapLayer,
            out int secondaryAlphamapLayer)
        {
            primaryBiomeId = 0;
            secondaryBiomeId = 0;
            primaryWeight = -1f;
            secondaryWeight = -1f;
            primaryAlphamapLayer = -1;
            secondaryAlphamapLayer = -1;

            if (mapMagicObject == null)
                return false;

            Terrain terrain = FindTerrainAt(x, z);
            if (terrain == null || terrain.terrainData == null)
                return false;

            TerrainData terrainData = terrain.terrainData;
            int totalLayers = terrainData.alphamapLayers;
            int textureCount = terrainData.alphamapTextureCount;
            if (totalLayers <= 0 || textureCount <= 0)
                return false;

            if (!TryGetCachedBiomeAlphaTextures(terrainData, textureCount, out Texture2D[] alphaTextures))
                return false;

            Vector3 terrainPosition = terrain.transform.position;
            Vector3 terrainSize = terrainData.size;
            if (terrainSize.x <= 0f || terrainSize.z <= 0f)
                return false;

            float u = math.saturate((x - terrainPosition.x) / terrainSize.x);
            float v = math.saturate((z - terrainPosition.z) / terrainSize.z);
            int searchLimit = math.min(totalLayers, BiomeMatrixLayerCount);
            bool anyValidTexture = false;

            for (int textureIndex = 0; textureIndex < textureCount; textureIndex++)
            {
                Texture2D alphaTexture = alphaTextures[textureIndex];
                if (alphaTexture == null)
                    continue;

                anyValidTexture = true;
                float4 weights = SampleAlphaTextureBilinear01(alphaTexture, u, v);
                int baseLayer = textureIndex * 4;
                ConsiderMatrixBiomeLayer(baseLayer, weights.x, searchLimit, ref primaryBiomeId, ref secondaryBiomeId, ref primaryWeight, ref secondaryWeight, ref primaryAlphamapLayer, ref secondaryAlphamapLayer);
                ConsiderMatrixBiomeLayer(baseLayer + 1, weights.y, searchLimit, ref primaryBiomeId, ref secondaryBiomeId, ref primaryWeight, ref secondaryWeight, ref primaryAlphamapLayer, ref secondaryAlphamapLayer);
                ConsiderMatrixBiomeLayer(baseLayer + 2, weights.z, searchLimit, ref primaryBiomeId, ref secondaryBiomeId, ref primaryWeight, ref secondaryWeight, ref primaryAlphamapLayer, ref secondaryAlphamapLayer);
                ConsiderMatrixBiomeLayer(baseLayer + 3, weights.w, searchLimit, ref primaryBiomeId, ref secondaryBiomeId, ref primaryWeight, ref secondaryWeight, ref primaryAlphamapLayer, ref secondaryAlphamapLayer);

                if (baseLayer + 3 >= searchLimit - 1)
                    break;
            }

            return anyValidTexture && primaryBiomeId > 0;
        }

        private static void ConsiderMatrixBiomeLayer(
            int layerIndex,
            float weight,
            int searchLimit,
            ref int primaryBiomeId,
            ref int secondaryBiomeId,
            ref float primaryWeight,
            ref float secondaryWeight,
            ref int primaryAlphamapLayer,
            ref int secondaryAlphamapLayer)
        {
            if (layerIndex < 0 ||
                layerIndex >= searchLimit ||
                !TryResolveBiomeMatrixAlphamapLayer(layerIndex + 1, out int resolvedLayer))
            {
                return;
            }

            float safeWeight = math.saturate(weight);
            int biomeId = layerIndex + 1;
            if (safeWeight > primaryWeight)
            {
                secondaryWeight = primaryWeight;
                secondaryBiomeId = primaryBiomeId;
                secondaryAlphamapLayer = primaryAlphamapLayer;
                primaryWeight = safeWeight;
                primaryBiomeId = biomeId;
                primaryAlphamapLayer = resolvedLayer;
            }
            else if (biomeId != primaryBiomeId && safeWeight > secondaryWeight)
            {
                secondaryWeight = safeWeight;
                secondaryBiomeId = biomeId;
                secondaryAlphamapLayer = resolvedLayer;
            }
        }

        private static float SmoothStep01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - 2f * t);
        }

        private bool TryGetCachedBiomeAlphaTextures(
            TerrainData terrainData,
            int expectedTextureCount,
            out Texture2D[] alphaTextures)
        {
            if (terrainData == null || expectedTextureCount <= 0)
            {
                alphaTextures = null;
                return false;
            }

            if (_cachedBiomeAlphaTextures == null ||
                expectedTextureCount > _cachedBiomeAlphaTextures.Length)
            {
                alphaTextures = _cachedBiomeAlphaTextures;
                return false;
            }

            if (_cachedBiomeTerrainData != terrainData ||
                _cachedBiomeAlphaTextures == null ||
                _cachedBiomeAlphaExpectedTextureCount != expectedTextureCount)
            {
                alphaTextures = _cachedBiomeAlphaTextures;
                return false;
            }

            alphaTextures = _cachedBiomeAlphaTextures;
            return alphaTextures != null && _cachedBiomeAlphaTextureCount > 0;
        }

        private void RefreshBiomeAlphaTextureCacheOwnerPhase(TerrainData terrainData, int expectedTextureCount)
        {
            if (terrainData == null ||
                expectedTextureCount <= 0 ||
                _cachedBiomeAlphaTextures == null ||
                expectedTextureCount > _cachedBiomeAlphaTextures.Length)
            {
                _cachedBiomeAlphaExpectedTextureCount = -1;
                _cachedBiomeAlphaTextureCount = -1;
                return;
            }

            _cachedBiomeTerrainData = terrainData;
            EnsureBiomeAlphaTextureCacheCapacity(expectedTextureCount);
            _cachedBiomeAlphaExpectedTextureCount = expectedTextureCount;
            _cachedBiomeAlphaTextureCount = 0;

            for (int i = 0; i < expectedTextureCount; i++)
            {
                Texture2D alphaTexture = terrainData.GetAlphamapTexture(i);
                _cachedBiomeAlphaTextures[i] = alphaTexture;
                if (alphaTexture != null)
                    _cachedBiomeAlphaTextureCount = i + 1;
            }

            for (int i = _cachedBiomeAlphaTextureCount; i < _cachedBiomeAlphaTextures.Length; i++)
                _cachedBiomeAlphaTextures[i] = null;
        }

        private void EnsureBiomeAlphaTextureCacheCapacity(int requiredCount)
        {
            int safeCount = Mathf.Max(1, requiredCount);
            if (_cachedBiomeAlphaTextures != null && _cachedBiomeAlphaTextures.Length >= safeCount)
                return;
        }

        private bool TryGetCachedBiomeTerrainLayers(
            TerrainData terrainData,
            int expectedLayerCount,
            out TerrainLayer[] terrainLayers)
        {
            if (terrainData == null || expectedLayerCount <= 0)
            {
                terrainLayers = null;
                return false;
            }

            if (_cachedBiomeTerrainLayers == null ||
                expectedLayerCount > _cachedBiomeTerrainLayers.Length)
            {
                terrainLayers = _cachedBiomeTerrainLayers;
                return false;
            }

            if (_cachedBiomeTerrainData != terrainData ||
                _cachedBiomeTerrainLayers == null ||
                _cachedBiomeTerrainLayerExpectedCount != expectedLayerCount)
            {
                terrainLayers = _cachedBiomeTerrainLayers;
                return false;
            }

            terrainLayers = _cachedBiomeTerrainLayers;
            return terrainLayers != null && _cachedBiomeTerrainLayerCount > 0;
        }

        private void RefreshBiomeTerrainLayerCacheOwnerPhase(TerrainData terrainData, int expectedLayerCount)
        {
            if (terrainData == null ||
                expectedLayerCount <= 0 ||
                _cachedBiomeTerrainLayers == null ||
                expectedLayerCount > _cachedBiomeTerrainLayers.Length)
            {
                _cachedBiomeTerrainLayerExpectedCount = -1;
                _cachedBiomeTerrainLayerCount = -1;
                return;
            }

            _cachedBiomeTerrainData = terrainData;
            EnsureBiomeTerrainLayerCacheCapacity(expectedLayerCount);
            _cachedBiomeTerrainLayerExpectedCount = expectedLayerCount;

            TerrainLayer[] sourceLayers = terrainData.terrainLayers;
            int sourceCount = sourceLayers != null ? math.min(expectedLayerCount, sourceLayers.Length) : 0;
            _cachedBiomeTerrainLayerCount = sourceCount;

            for (int i = 0; i < sourceCount; i++)
                _cachedBiomeTerrainLayers[i] = sourceLayers[i];

            for (int i = sourceCount; i < _cachedBiomeTerrainLayers.Length; i++)
                _cachedBiomeTerrainLayers[i] = null;
        }

        private void EnsureBiomeTerrainLayerCacheCapacity(int requiredCount)
        {
            int safeCount = Mathf.Max(1, requiredCount);
            if (_cachedBiomeTerrainLayers != null && _cachedBiomeTerrainLayers.Length >= safeCount)
                return;
        }

        private static void AccumulateLayerColor(
            int layerIndex,
            float weight,
            int totalLayers,
            TerrainLayer[] terrainLayers,
            ref float3 accumulated,
            ref float totalWeight,
            ref float maxWeight)
        {
            if (layerIndex < 0 || layerIndex >= totalLayers || weight <= 0.0001f)
                return;

            float3 layerColor = ResolveTerrainLayerColor(layerIndex, terrainLayers);
            accumulated += layerColor * weight;
            totalWeight += weight;
            if (weight > maxWeight)
                maxWeight = weight;
        }

        private static float3 ResolveTerrainLayerColor(int layerIndex, TerrainLayer[] terrainLayers)
        {
            if (terrainLayers != null && layerIndex >= 0 && layerIndex < terrainLayers.Length)
            {
                TerrainLayer layer = terrainLayers[layerIndex];
                if (layer != null)
                {
                    Vector4 min = layer.diffuseRemapMin;
                    Vector4 max = layer.diffuseRemapMax;
                    float3 remapColor = math.saturate(new float3(
                        (min.x + max.x) * 0.5f,
                        (min.y + max.y) * 0.5f,
                        (min.z + max.z) * 0.5f));

                    if (math.lengthsq(remapColor) > 0.0001f)
                        return remapColor;
                }
            }

            return ResolveFallbackTerrainLayerColor(layerIndex);
        }

        private static float3 ResolveFallbackTerrainLayerColor(int layerIndex)
        {
            switch (math.abs(layerIndex) % 8)
            {
                case 0: return new float3(0.46f, 0.43f, 0.35f);
                case 1: return new float3(0.28f, 0.34f, 0.30f);
                case 2: return new float3(0.34f, 0.38f, 0.42f);
                case 3: return new float3(0.18f, 0.22f, 0.24f);
                case 4: return new float3(0.50f, 0.39f, 0.28f);
                case 5: return new float3(0.24f, 0.30f, 0.36f);
                case 6: return new float3(0.38f, 0.47f, 0.44f);
                default: return new float3(0.32f, 0.30f, 0.27f);
            }
        }

        private static float4 SampleAlphaTextureBilinear01(Texture2D texture, float u, float v)
        {
            if (texture == null || !texture.isReadable)
                return float4.zero;

            int width = texture.width;
            int height = texture.height;
            if (width <= 0 || height <= 0)
                return float4.zero;

            NativeArray<Color32> pixels = texture.GetPixelData<Color32>(0);
            int pixelCount = width * height;
            if (!pixels.IsCreated || pixels.Length < pixelCount)
                return float4.zero;

            float sampleX = math.saturate(u) * (width - 1);
            float sampleY = math.saturate(v) * (height - 1);
            int x0 = (int)math.floor(sampleX);
            int y0 = (int)math.floor(sampleY);
            int x1 = math.min(x0 + 1, width - 1);
            int y1 = math.min(y0 + 1, height - 1);
            float tx = sampleX - x0;
            float ty = sampleY - y0;

            int row0 = y0 * width;
            int row1 = y1 * width;
            float4 c00 = UnpackColor32(pixels[row0 + x0]);
            float4 c10 = UnpackColor32(pixels[row0 + x1]);
            float4 c01 = UnpackColor32(pixels[row1 + x0]);
            float4 c11 = UnpackColor32(pixels[row1 + x1]);
            float4 top = math.lerp(c00, c10, tx);
            float4 bottom = math.lerp(c01, c11, tx);
            return math.lerp(top, bottom, ty);
        }

        private static float4 UnpackColor32(Color32 color)
        {
            const float ByteToFloat = 1f / 255f;
            return new float4(color.r, color.g, color.b, color.a) * ByteToFloat;
        }

        /// <summary>
        /// Bystraya versiya bez out. Vozvraschaet 0 pri oshibke.
        /// </summary>
        public override int GetBiomeIndex(float x, float z)
        {
            TryGetBiomeIndex(x, z, out int idx);
            return idx;
        }

        /// <summary>
        /// Convenience overload accepting float3 position.
        /// Uses x and z components.
        /// </summary>
        public override int GetCurrentBiome(float3 position)
        {
            TryGetBiomeIndex(position.x, position.z, out int idx);
            return idx;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — RUNTIME SETTERS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Assigns player transform at runtime.
        /// Called by player initialization if Inspector ref is empty.
        /// </summary>
        public override void SetPlayerTransform(Transform player)
        {
            playerTransform = player;
            _nextSceneBindingRefreshTime = float.NegativeInfinity;
            UpdateDiagnostics();
        }

        private void RefreshPlayerTransformFromRegistryCold()
        {
            CachePlayerTransform(GlobalRegistry.Player);
        }

        private void RebindPlayerTransform(IPlayerRuntimeContext previousContext, IPlayerRuntimeContext currentContext)
        {
            if (previousContext != null &&
                previousContext.PlayerTransform != null &&
                ReferenceEquals(playerTransform, previousContext.PlayerTransform))
            {
                playerTransform = null;
            }

            CachePlayerTransform(currentContext);
            _nextSceneBindingRefreshTime = float.NegativeInfinity;
        }

        private void CachePlayerTransform(IPlayerRuntimeContext playerContext)
        {
            if (playerContext == null || playerContext.PlayerTransform == null)
                return;

            playerTransform = playerContext.PlayerTransform;
        }

        /// <summary>
        /// Assigns the scene MapMagicObject and refreshes cached tile state.
        /// </summary>
        public override void SetMapMagicObject(UnityEngine.Object target)
        {
            mapMagicObject = target as MapMagicObject;
            FenceRuntimeMapMagicGenerationIfNeeded();
            _cachedTerrainTileRootCount = -1;
            _lastResolvedTerrainTile = null;
            InvalidateBiomeTextureCache();
            if (!Application.isPlaying)
                EnsureRuntimeTerrainConnectivityCompatibility(forceApplyToCachedTerrains: false);
            RefreshTerrainTileCache(force: true);
            if (!Application.isPlaying)
                ApplyTerrainDataMemoryBudgetToCachedTerrains();
            _nextSceneBindingRefreshTime = float.NegativeInfinity;
            UpdateDiagnostics();
        }

        /// <summary>
        /// Updates water surface level at runtime.
        /// </summary>
        public override void SetWaterSurfaceLevel(float y)
        {
            waterSurfaceLevel = SanitizeWaterSurfaceLevel(y);
        }

        private static float SanitizeWaterSurfaceLevel(float y)
        {
            return math.isfinite(y) && math.abs(y) > 0.0001f
                ? y
                : DefaultWaterSurfaceLevel;
        }

        /// <summary>
        /// Enables or disables sandbox mode where downstream systems trust procedural terrain data only.
        /// </summary>
        /// <param name="enabled">True to ignore pre-baked matrix terrain inputs for sandbox sampling.</param>
        public override void SetSandboxProceduralTerrainOnly(bool enabled)
        {
            sandboxProceduralTerrainOnly = enabled;
        }

        /// <summary>
        /// Enables or disables biome-matrix driven alphamap layer resolution for sandbox tiles.
        /// </summary>
        /// <param name="enabled">True to remap matrix biome IDs into procedural texture layers.</param>
        public override void SetSandboxBiomeMatrixAlphamapLayers(bool enabled)
        {
            sandboxUseBiomeMatrixAlphamapLayers = enabled;
        }

        /// <summary>
        /// Schedules the sandbox thermal weathering post-process over a normalized height field.
        /// </summary>
        /// <param name="inputHeights01">Read-only normalized source heights.</param>
        /// <param name="outputHeights01">Write target for normalized eroded heights.</param>
        /// <param name="width">Height field width in samples.</param>
        /// <param name="height">Height field height in samples.</param>
        /// <param name="cellSizeMeters">World-space spacing between height samples.</param>
        /// <param name="heightScaleMeters">World-space height scale used to normalize talus transfer.</param>
        /// <param name="dependency">Input dependency for prior height jobs.</param>
        /// <returns>Job handle for the weathering pass, or the input dependency when disabled/invalid.</returns>
        public override JobHandle ScheduleSandboxThermalWeatheringPostProcess(
            NativeArray<float> inputHeights01,
            NativeArray<float> outputHeights01,
            int width,
            int height,
            float cellSizeMeters,
            float heightScaleMeters,
            JobHandle dependency = default)
        {
            if (Application.isPlaying)
                return dependency;

            if (!enableSandboxThermalWeathering ||
                !inputHeights01.IsCreated ||
                !outputHeights01.IsCreated ||
                width <= 2 ||
                height <= 2)
            {
                return dependency;
            }

            int cellCount = width * height;
            if (inputHeights01.Length < cellCount || outputHeights01.Length < cellCount)
                return dependency;

            var job = new Hecton8.World.WorldProceduralTerrainThermalWeatheringJob
            {
                InputHeights01 = inputHeights01,
                OutputHeights01 = outputHeights01,
                Width = width,
                Height = height,
                CellSizeMeters = math.max(0.001f, cellSizeMeters),
                HeightScaleMeters = math.max(0.001f, heightScaleMeters),
                TalusAngleDegrees = sandboxThermalWeatheringTalusAngleDegrees,
                Strength = sandboxThermalWeatheringStrength
            };

            int batchCount = math.max(1, math.min(64, cellCount / 16));
            return job.Schedule(cellCount, batchCount, dependency);
        }

        /// <summary>
        /// Returns true for matrix biomes that belong to the tectonic-spine family.
        /// </summary>
        /// <param name="profile">Biome matrix profile resolved from the 108-entry catalog.</param>
        public new static bool IsTectonicSpineMatrixBiome(HectonBiomeMatrixProfile profile)
        {
            if (profile == null)
                return false;

            if (IsTectonicSpineFamilyId(profile.familyId))
                return true;

            HectonBiomeFamilyProfile familyProfile = profile.familyProfile;
            return familyProfile != null && IsTectonicSpineFamilyId(familyProfile.familyId);
        }

        /// <summary>
        /// Schedules tectonic-spine ridge extrusion over a normalized height field.
        /// </summary>
        /// <param name="biomeProfile">Matrix profile used to gate the tectonic-spine processor.</param>
        /// <param name="inputHeights01">Read-only normalized source heights.</param>
        /// <param name="outputHeights01">Write target for normalized displaced heights.</param>
        /// <param name="width">Height field width in samples.</param>
        /// <param name="height">Height field height in samples.</param>
        /// <param name="worldOriginXZ">Absolute world-space origin of sample (0,0).</param>
        /// <param name="cellSizeMeters">World-space spacing between height samples.</param>
        /// <param name="dependency">Input dependency for prior height jobs.</param>
        /// <returns>Job handle for the displacement pass, or the input dependency when disabled/invalid.</returns>
        public override JobHandle ScheduleSandboxTectonicSpineDisplacementPostProcess(
            HectonBiomeMatrixProfile biomeProfile,
            NativeArray<float> inputHeights01,
            NativeArray<float> outputHeights01,
            int width,
            int height,
            float2 worldOriginXZ,
            float cellSizeMeters,
            JobHandle dependency = default)
        {
            return ScheduleSandboxTectonicSpineDisplacementPostProcess(
                IsTectonicSpineMatrixBiome(biomeProfile),
                inputHeights01,
                outputHeights01,
                width,
                height,
                worldOriginXZ,
                cellSizeMeters,
                dependency);
        }

        /// <summary>
        /// Schedules tectonic-spine ridge extrusion when the caller already has a biome-family gate.
        /// </summary>
        /// <param name="isTectonicSpineBiome">True when the tile belongs to biome.family.tectonic_spine.</param>
        /// <param name="inputHeights01">Read-only normalized source heights.</param>
        /// <param name="outputHeights01">Write target for normalized displaced heights.</param>
        /// <param name="width">Height field width in samples.</param>
        /// <param name="height">Height field height in samples.</param>
        /// <param name="worldOriginXZ">Absolute world-space origin of sample (0,0).</param>
        /// <param name="cellSizeMeters">World-space spacing between height samples.</param>
        /// <param name="dependency">Input dependency for prior height jobs.</param>
        /// <returns>Job handle for the displacement pass, or the input dependency when disabled/invalid.</returns>
        public override JobHandle ScheduleSandboxTectonicSpineDisplacementPostProcess(
            bool isTectonicSpineBiome,
            NativeArray<float> inputHeights01,
            NativeArray<float> outputHeights01,
            int width,
            int height,
            float2 worldOriginXZ,
            float cellSizeMeters,
            JobHandle dependency = default)
        {
            if (Application.isPlaying)
                return dependency;

            if (!enableSandboxTectonicSpineDisplacement ||
                !isTectonicSpineBiome ||
                !inputHeights01.IsCreated ||
                !outputHeights01.IsCreated ||
                width <= 1 ||
                height <= 1)
            {
                return dependency;
            }

            int cellCount = width * height;
            if (inputHeights01.Length < cellCount || outputHeights01.Length < cellCount)
                return dependency;

            var job = new Hecton8.World.WorldProceduralTerrainTectonicDisplacementJob
            {
                InputHeights01 = inputHeights01,
                OutputHeights01 = outputHeights01,
                Width = width,
                Height = height,
                WorldOriginXZ = worldOriginXZ,
                CellSizeMeters = math.max(0.001f, cellSizeMeters),
                Strength01 = sandboxTectonicSpineStrength,
                Frequency = sandboxTectonicSpineFrequency,
                RidgeSharpness = sandboxTectonicSpineRidgeSharpness,
                Seed = unchecked((uint)sandboxTectonicSpineSeed)
            };

            int batchCount = math.max(1, math.min(64, cellCount / 16));
            return job.Schedule(cellCount, batchCount, dependency);
        }

        /// <summary>
        /// Schedules fake horizontal cliff-overhang offsets for voxel handoff consumers.
        /// Heightmap terrain remains vertical-only; callers apply the returned offsets to voxel/contact vertices.
        /// </summary>
        public override JobHandle ScheduleSandboxFakeCliffOverhangOffsets(
            NativeArray<float> heights01,
            NativeArray<float2> horizontalOffsetsMeters,
            int width,
            int height,
            float cellSizeMeters,
            float heightScaleMeters,
            JobHandle dependency = default)
        {
            if (Application.isPlaying)
                return dependency;

            if (!enableSandboxFakeCliffOverhangOffsets ||
                !heights01.IsCreated ||
                !horizontalOffsetsMeters.IsCreated ||
                width <= 2 ||
                height <= 2)
            {
                return dependency;
            }

            int cellCount = width * height;
            if (heights01.Length < cellCount || horizontalOffsetsMeters.Length < cellCount)
                return dependency;

            var job = new Hecton8.World.WorldProceduralTerrainFakeOverhangOffsetJob
            {
                Heights01 = heights01,
                HorizontalOffsetsMeters = horizontalOffsetsMeters,
                Width = width,
                Height = height,
                CellSizeMeters = math.max(0.001f, cellSizeMeters),
                HeightScaleMeters = heightScaleMeters,
                SlopeThresholdDegrees = sandboxFakeOverhangSlopeThresholdDegrees,
                MaxOffsetMeters = sandboxFakeOverhangMaxOffsetMeters,
                NoiseFrequency = sandboxFakeOverhangNoiseFrequency,
                Seed = unchecked((uint)sandboxFakeOverhangSeed)
            };

            int batchCount = math.max(1, math.min(64, cellCount / 16));
            return job.Schedule(cellCount, batchCount, dependency);
        }

        /// <summary>
        /// Builds a positive rim-height overlay around brine basin edges for terrain height and normal-map blending.
        /// </summary>
        public new static JobHandle ScheduleBrineBasinLipRidgeOverlay(
            NativeArray<byte> basinMask,
            NativeArray<float> lipOffsetMeters,
            int width,
            int height,
            int falloffCells,
            float lipHeightMeters,
            JobHandle dependency = default)
        {
            if (!basinMask.IsCreated ||
                !lipOffsetMeters.IsCreated ||
                width <= 2 ||
                height <= 2)
            {
                return dependency;
            }

            int cellCount = width * height;
            if (basinMask.Length < cellCount || lipOffsetMeters.Length < cellCount)
                return dependency;

            var job = new BrineBasinLipRidgeOverlayJob
            {
                BasinMask = basinMask,
                LipOffsetMeters = lipOffsetMeters,
                Width = width,
                Height = height,
                FalloffCells = math.max(1, falloffCells),
                LipHeightMeters = math.max(0f, lipHeightMeters)
            };

            int batchCount = math.max(1, math.min(64, cellCount / 16));
            return job.Schedule(cellCount, batchCount, dependency);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct BrineBasinLipRidgeOverlayJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<byte> BasinMask;
            public NativeArray<float> LipOffsetMeters;
            public int Width;
            public int Height;
            public int FalloffCells;
            public float LipHeightMeters;

            public void Execute(int index)
            {
                if (BasinMask[index] != 0)
                {
                    LipOffsetMeters[index] = 0f;
                    return;
                }

                int x = index % Width;
                int z = index / Width;
                int radius = math.max(1, FalloffCells);
                float radiusSq = math.max(1f, radius * radius);
                float best = 0f;
                for (int dz = -radius; dz <= radius; dz++)
                {
                    int nz = z + dz;
                    if ((uint)nz >= (uint)Height)
                        continue;

                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        if (dx == 0 && dz == 0)
                            continue;

                        int nx = x + dx;
                        if ((uint)nx >= (uint)Width)
                            continue;

                        int neighbor = nx + nz * Width;
                        if (BasinMask[neighbor] == 0)
                            continue;

                        // Cinematic fake: squared falloff keeps the basin lip organic without a sqrt per neighbor sample.
                        float distanceSq = (dx * dx) + (dz * dz);
                        float ridge = 1f - math.saturate((distanceSq - 1f) / radiusSq);
                        best = math.max(best, ridge);
                    }
                }

                LipOffsetMeters[index] = best * LipHeightMeters;
            }
        }

        private static bool IsTectonicSpineFamilyId(string familyId)
        {
            return string.Equals(familyId, TectonicSpineFamilyId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Applies the runtime object-generation budget to the bound
        /// MapMagicObject.
        /// </summary>
        /// <param name="objectsPerFrame">Target per-frame object apply budget.</param>
        /// <returns>True when the serialized runtime value changed.</returns>
        public override bool SetRuntimeObjectsPerFrame(int objectsPerFrame)
        {
            if (mapMagicObject == null || mapMagicObject.globals == null)
                return false;

            int clampedObjectsPerFrame = Mathf.Clamp(objectsPerFrame, 32, 512);
            if (mapMagicObject.globals.objectsNumPerFrame == clampedObjectsPerFrame)
                return false;

            mapMagicObject.globals.objectsNumPerFrame = clampedObjectsPerFrame;
            return true;
        }

        /// <summary>
        /// Configures the runtime terrain draft/main continuum for MapMagic tiles.
        /// </summary>
        /// <param name="draftsInPlaymode">Whether draft terrains remain active in play mode.</param>
        /// <param name="mainRange">Main-terrain ring radius around the observer.</param>
        /// <param name="draftRange">Draft-terrain ring radius around the observer.</param>
        /// <param name="draftResolution">Draft terrain height resolution.</param>
        /// <returns>True when topology-affecting settings changed.</returns>
        public override bool ConfigureRuntimeTerrainStreaming(
            bool draftsInPlaymode,
            int mainRange,
            int draftRange,
            int draftResolutionValue)
        {
            return ConfigureRuntimeTerrainStreaming(
                draftsInPlaymode,
                mainRange,
                draftRange,
                ResolveRuntimeTerrainResolution(draftResolutionValue));
        }

        public bool ConfigureRuntimeTerrainStreaming(
            bool draftsInPlaymode,
            int mainRange,
            int draftRange,
            MapMagicObject.Resolution draftResolution)
        {
            if (mapMagicObject == null)
                return false;

            int clampedMainRange = Mathf.Max(1, mainRange);
            int clampedDraftRange = Mathf.Max(clampedMainRange, draftRange);
            if (Application.isPlaying && draftsInPlaymode)
                draftResolution = mapMagicObject.tileResolution;

            bool topologyChanged = false;
            bool resolutionChanged = false;

            if (mapMagicObject.draftsInPlaymode != draftsInPlaymode)
            {
                mapMagicObject.draftsInPlaymode = draftsInPlaymode;
                topologyChanged = true;
            }

            if (mapMagicObject.mainRange != clampedMainRange)
            {
                mapMagicObject.mainRange = clampedMainRange;
                topologyChanged = true;
            }

            if (mapMagicObject.tiles.generateRange != clampedDraftRange)
            {
                mapMagicObject.tiles.generateRange = clampedDraftRange;
                topologyChanged = true;
            }

            if (mapMagicObject.draftResolution != draftResolution)
            {
                mapMagicObject.draftResolution = draftResolution;
                resolutionChanged = true;
            }

            if (topologyChanged || resolutionChanged)
                _runtimeTerrainResolutionRepairPending = true;

            EnsureRuntimeTerrainConnectivityCompatibility(forceApplyToCachedTerrains: topologyChanged || resolutionChanged);

            if (!draftsInPlaymode)
            {
                RefreshTerrainTilesForStreaming(clampedMainRange, rebuildInRange: resolutionChanged);
                return topologyChanged || resolutionChanged;
            }

            RefreshTerrainTileCache(force: true);
            ApplyTerrainDataMemoryBudgetToCachedTerrains();

            List<TerrainTile> terrainTiles = _cachedTerrainTiles;
            int tileCount = terrainTiles.Count;

            for (int i = 0; i < tileCount; i++)
            {
                TerrainTile tile = terrainTiles[i];
                if (tile == null || tile.draft != null)
                    continue;

                // COLD ALLOC: TerrainTile.DetailLevel[1] — enable runtime draft terrain continuum — owner: MapMagicBridge
                tile.draft = CreateRuntimeDetailLevel(tile, isDraft: true);
                topologyChanged = true;
            }

            RefreshTerrainTilesForStreaming(clampedDraftRange, rebuildInRange: topologyChanged || resolutionChanged);
            return topologyChanged || resolutionChanged;
        }

        private static MapMagicObject.Resolution ResolveRuntimeTerrainResolution(int draftResolutionValue)
        {
            switch (draftResolutionValue)
            {
                case 33:
                    return MapMagicObject.Resolution._33;
                case 65:
                    return MapMagicObject.Resolution._65;
                case 129:
                    return MapMagicObject.Resolution._129;
                case 257:
                    return MapMagicObject.Resolution._257;
                case 513:
                    return MapMagicObject.Resolution._513;
                case 1025:
                    return MapMagicObject.Resolution._1025;
                case 2049:
                    return MapMagicObject.Resolution._2049;
                default:
                    return MapMagicObject.Resolution._65;
            }
        }

        /// <summary>
        /// Applies runtime terrain visual fidelity to current MapMagic tiles.
        /// </summary>
        /// <param name="pixelError">Unity terrain pixel error for geometry tessellation.</param>
        /// <param name="baseMapDistance">Distance before terrain falls back to basemap shading.</param>
        /// <param name="detailDistance">Distance for terrain detail instances.</param>
        /// <param name="detailDensity">Density multiplier for terrain detail instances.</param>
        /// <param name="heightmapMaximumLod">Maximum heightmap LOD simplification.</param>
        /// <returns>True when the terrain settings changed and were re-applied.</returns>
        public override bool ApplyRuntimeTerrainQuality(
            int pixelError,
            int baseMapDistance,
            float detailDistance,
            float detailDensity,
            int heightmapMaximumLod)
        {
            if (mapMagicObject == null)
                return false;

            MapMagic.Terrains.TerrainSettings terrainSettings = mapMagicObject.terrainSettings;
            if (terrainSettings == null)
                return false;

            int clampedPixelError = Mathf.Clamp(pixelError, 1, 12);
            int clampedBaseMapDistance = Mathf.Clamp(baseMapDistance, 512, 4000);
            float clampedDetailDistance = Mathf.Clamp(detailDistance, 0f, 160f);
            float clampedDetailDensity = Mathf.Clamp(detailDensity, 0.4f, 1.2f);
            int clampedHeightmapMaximumLod = Mathf.Clamp(heightmapMaximumLod, 0, 3);

            bool changed = false;

            if (terrainSettings.pixelError != clampedPixelError)
            {
                terrainSettings.pixelError = clampedPixelError;
                changed = true;
            }

            if (terrainSettings.baseMapDist != clampedBaseMapDistance)
            {
                terrainSettings.baseMapDist = clampedBaseMapDistance;
                changed = true;
            }

            if (!terrainSettings.showBaseMap)
            {
                terrainSettings.showBaseMap = true;
                changed = true;
            }

            if (!Mathf.Approximately(terrainSettings.detailDistance, clampedDetailDistance))
            {
                terrainSettings.detailDistance = clampedDetailDistance;
                changed = true;
            }

            if (!Mathf.Approximately(terrainSettings.detailDensity, clampedDetailDensity))
            {
                terrainSettings.detailDensity = clampedDetailDensity;
                changed = true;
            }

            if (terrainSettings.heightmapMaximumLOD != clampedHeightmapMaximumLod)
            {
                terrainSettings.heightmapMaximumLOD = clampedHeightmapMaximumLod;
                changed = true;
            }

            if (!changed)
                return false;

            ApplyTerrainSettingsToCachedTerrains();
            return true;
        }

        /// <summary>
        /// Ensures tiles near the player have a live main-detail terrain while
        /// far tiles release their main-detail payload back to draft-only.
        /// </summary>
        public override void MaintainRuntimeTerrainDetailLevels(
            int mainRange,
            int teardownRange,
            int mainPixelError,
            int mainBaseMapDistance,
            int draftPixelError,
            int draftBaseMapDistance,
            float detailDistance,
            float detailDensity,
            int heightmapMaximumLod)
        {
            if (mapMagicObject == null || !Application.isPlaying)
                return;

            if (playerTransform == null)
                return;

            RefreshTerrainTileCache(force: true);

            int clampedMainRange = Mathf.Max(1, mainRange);
            int clampedTeardownRange = Mathf.Max(clampedMainRange + 1, teardownRange);
            int clampedMainPixelError = Mathf.Clamp(mainPixelError, 1, 4);
            int clampedMainBaseMapDistance = Mathf.Clamp(mainBaseMapDistance, 512, 4000);
            int clampedDraftPixelError = Mathf.Clamp(draftPixelError, clampedMainPixelError + 1, 12);
            int clampedDraftBaseMapDistance = Mathf.Clamp(draftBaseMapDistance, 256, clampedMainBaseMapDistance);
            float clampedDetailDistance = Mathf.Clamp(detailDistance, 0f, 160f);
            float clampedDetailDensity = Mathf.Clamp(detailDensity, 0.4f, 1.2f);
            int clampedHeightmapMaximumLod = Mathf.Clamp(heightmapMaximumLod, 0, 3);
            Vector3 playerPosition = playerTransform.position;
            int playerTileX = Mathf.FloorToInt(playerPosition.x / Mathf.Max(1f, mapMagicObject.tileSize.x));
            int playerTileZ = Mathf.FloorToInt(playerPosition.z / Mathf.Max(1f, mapMagicObject.tileSize.z));

            List<TerrainTile> terrainTiles = _cachedTerrainTiles;
            int tileCount = terrainTiles.Count;

            for (int i = 0; i < tileCount; i++)
            {
                TerrainTile tile = terrainTiles[i];
                if (tile == null)
                    continue;

                int tileDistance = Mathf.Max(
                    Mathf.Abs(tile.coord.x - playerTileX),
                    Mathf.Abs(tile.coord.z - playerTileZ));

                if (mapMagicObject.draftsInPlaymode && tile.draft == null)
                {
                    // COLD ALLOC: TerrainTile.DetailLevel[1] - keep newly streamed tiles on the runtime draft continuum - owner: MapMagicBridge
                    tile.draft = CreateRuntimeDetailLevel(tile, isDraft: true);
                    tile.Dist(tileDistance);
                }
                else if (!Mathf.Approximately(tile.distance, tileDistance))
                {
                    tile.Dist(tileDistance);
                }

                if (tileDistance <= clampedMainRange)
                {
                    if (tile.main == null)
                    {
                        tile.main = CreateRuntimeDetailLevel(tile, isDraft: false);
                        tile.Dist(tileDistance);
                    }
                    else if (!tile.main.generateStarted)
                    {
                        tile.Dist(tileDistance);
                    }
                }
                else if (tileDistance >= clampedTeardownRange && CanReleaseMainDetailLevel(tile))
                {
                    ReleaseMainDetailLevel(tile);
                }

                ApplyPerTileTerrainQuality(
                    tile,
                    clampedMainRange,
                    clampedMainPixelError,
                    clampedMainBaseMapDistance,
                    clampedDraftPixelError,
                    clampedDraftBaseMapDistance,
                    clampedDetailDistance,
                    clampedDetailDensity,
                    clampedHeightmapMaximumLod);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — TERRAIN LOOKUP
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Nahodit Terrain, pokryvayuschiy mirovye koordinaty (x, z).
        ///
        /// Strategiya:
        ///   1. Reuse the last resolved MapMagic TerrainTile.
        ///   2. Scan the cached MapMagic TerrainTile array refreshed outside the hot path.
        ///
        /// ZERO GC: no Unity global terrain fallback.
        /// </summary>
        private Terrain FindTerrainAt(float x, float z)
        {
            if (_lastResolvedTerrainTile != null &&
                _lastResolvedTerrainTile.ContainsWorldPosition(x, z))
            {
                Terrain cachedTerrain = ResolveTileTerrain(_lastResolvedTerrainTile);
                if (cachedTerrain != null && cachedTerrain.terrainData != null)
                    return cachedTerrain;
            }

            List<TerrainTile> terrainTiles = _cachedTerrainTiles;
            int tileCount = terrainTiles.Count;

            for (int i = 0; i < tileCount; i++)
            {
                TerrainTile tile = terrainTiles[i];
                if (tile == null || !tile.ContainsWorldPosition(x, z))
                    continue;

                Terrain tileTerrain = ResolveTileTerrain(tile);
                if (tileTerrain == null || tileTerrain.terrainData == null)
                    continue;

                return tileTerrain;
            }

            return null;
        }

        private Terrain FindTerrainAtAUP(
            double3 absoluteUniversePosition,
            out Vector3 terrainRuntimePosition,
            out Vector3 terrainSize,
            out double3 terrainAbsolutePosition)
        {
            terrainRuntimePosition = default;
            terrainSize = default;
            terrainAbsolutePosition = default;

            if (_lastResolvedTerrainTile != null &&
                TryResolveTileTerrainAupFrame(
                    _lastResolvedTerrainTile,
                    out Terrain cachedTerrain,
                    out terrainRuntimePosition,
                    out terrainSize,
                    out terrainAbsolutePosition) &&
                ContainsAupXZ(absoluteUniversePosition, terrainAbsolutePosition, terrainSize))
            {
                return cachedTerrain;
            }

            List<TerrainTile> terrainTiles = _cachedTerrainTiles;
            int tileCount = terrainTiles.Count;
            for (int i = 0; i < tileCount; i++)
            {
                TerrainTile tile = terrainTiles[i];
                if (!TryResolveTileTerrainAupFrame(
                        tile,
                        out Terrain terrain,
                        out terrainRuntimePosition,
                        out terrainSize,
                        out terrainAbsolutePosition) ||
                    !ContainsAupXZ(absoluteUniversePosition, terrainAbsolutePosition, terrainSize))
                {
                    continue;
                }

                return terrain;
            }

            terrainRuntimePosition = default;
            terrainSize = default;
            terrainAbsolutePosition = default;
            return null;
        }

        private static bool TryResolveTileTerrainAupFrame(
            TerrainTile tile,
            out Terrain terrain,
            out Vector3 terrainRuntimePosition,
            out Vector3 terrainSize,
            out double3 terrainAbsolutePosition)
        {
            terrain = ResolveTileTerrain(tile);
            terrainRuntimePosition = default;
            terrainSize = default;
            terrainAbsolutePosition = default;
            if (terrain == null || terrain.terrainData == null)
                return false;

            terrainRuntimePosition = terrain.transform.position;
            terrainSize = terrain.terrainData.size;
            if (terrainSize.x <= 0f || terrainSize.z <= 0f)
                return false;

            return TryResolveAupDoubleFromRuntimeOrigin(terrainRuntimePosition, out terrainAbsolutePosition);
        }

        private static bool ContainsAupXZ(double3 absoluteUniversePosition, double3 terrainAbsolutePosition, Vector3 terrainSize)
        {
            double x = absoluteUniversePosition.x;
            double z = absoluteUniversePosition.z;
            return x >= terrainAbsolutePosition.x &&
                   z >= terrainAbsolutePosition.z &&
                   x <= terrainAbsolutePosition.x + terrainSize.x &&
                   z <= terrainAbsolutePosition.z + terrainSize.z;
        }

        private void UpdateLastResolvedTerrainTileOwnerPhase()
        {
            if (playerTransform == null)
                return;

            Vector3 position = playerTransform.position;
            if (_lastResolvedTerrainTile != null &&
                _lastResolvedTerrainTile.ContainsWorldPosition(position.x, position.z))
            {
                Terrain cachedTerrain = ResolveTileTerrain(_lastResolvedTerrainTile);
                if (cachedTerrain != null && cachedTerrain.terrainData != null)
                    return;
            }

            _lastResolvedTerrainTile = null;
            List<TerrainTile> terrainTiles = _cachedTerrainTiles;
            int tileCount = terrainTiles.Count;

            for (int i = 0; i < tileCount; i++)
            {
                TerrainTile tile = terrainTiles[i];
                if (tile == null || !tile.ContainsWorldPosition(position.x, position.z))
                    continue;

                Terrain tileTerrain = ResolveTileTerrain(tile);
                if (tileTerrain == null || tileTerrain.terrainData == null)
                    continue;

                _lastResolvedTerrainTile = tile;
                return;
            }
        }

        private void PrewarmBiomeAlphaTextureCacheOwnerPhase()
        {
            TerrainTile tile = _lastResolvedTerrainTile;
            if (tile == null)
                return;

            Terrain terrain = ResolveTileTerrain(tile);
            TerrainData terrainData = terrain != null ? terrain.terrainData : null;
            if (terrainData == null)
                return;

            int textureCount = terrainData.alphamapTextureCount;
            if (textureCount <= 0)
                return;

            if (!TryGetCachedBiomeAlphaTextures(terrainData, textureCount, out _))
                RefreshBiomeAlphaTextureCacheOwnerPhase(terrainData, textureCount);

            int layerCount = terrainData.alphamapLayers;
            if (layerCount > 0 && !TryGetCachedBiomeTerrainLayers(terrainData, layerCount, out _))
                RefreshBiomeTerrainLayerCacheOwnerPhase(terrainData, layerCount);
        }

        private void RefreshRuntimeSceneBindingDiagnostics()
        {
            if (mapMagicObject != null && playerTransform != null)
                return;

            float currentTime = Time.unscaledTime;
            if (currentTime < _nextSceneBindingRefreshTime)
                return;

            _nextSceneBindingRefreshTime = currentTime + SceneBindingRefreshInterval;
            ReportMissingMapMagicBindingIfNeeded();
            UpdateDiagnostics();
        }

        private void FenceRuntimeMapMagicGenerationIfNeeded()
        {
            if (!Application.isPlaying || mapMagicObject == null)
                return;

            FenceRuntimeMapMagicGenerationImmediate();
        }

        private void FenceRuntimeMapMagicGenerationImmediate()
        {
            if (!Application.isPlaying || mapMagicObject == null)
                return;

            mapMagicObject.enabled = true;
            mapMagicObject.instantGenerate = false;
            mapMagicObject.draftsInPlaymode = true;
            mapMagicObject.serializedMultithreading = true;
            mapMagicObject.serializedAutoMaxThreads = false;
            mapMagicObject.serializedMaxThreads = 1;
            mapMagicObject.serializedMaxApplyTime = 1f;
            Den.Tools.Tasks.ThreadManager.useMultithreading = true;
            Den.Tools.Tasks.ThreadManager.autoMaxThreads = false;
            Den.Tools.Tasks.ThreadManager.maxThreads = 1;
            Den.Tools.Tasks.CoroutineManager.timePerFrame = 1f;
            _pendingRuntimeMapMagicGenerationFence = false;
            _runtimeTerrainResolutionRepairPending = true;
        }

        private void TryResolveCoLocatedMapMagicObject()
        {
            if (mapMagicObject != null)
            {
                _loggedMissingMapMagicBinding = false;
                return;
            }

            if (TryGetComponent(out MapMagicObject coLocatedObject))
            {
                mapMagicObject = coLocatedObject;
                _loggedMissingMapMagicBinding = false;
            }
        }

        private void ReportMissingMapMagicBindingIfNeeded()
        {
            if (mapMagicObject != null || _loggedMissingMapMagicBinding)
                return;

            _loggedMissingMapMagicBinding = true;
#if UNITY_EDITOR
            Hecton8.Core.H8Debug.LogError(
                "[MapMagicBridge] Missing MapMagicObject binding. Assign it explicitly or place MapMagicBridge on the same GameObject as MapMagicObject. Runtime scene-wide fallback search is forbidden.",
                this);
#endif
        }

        /// <summary>
        /// Refreshes cached TerrainTile array when the MapMagic root hierarchy
        /// changes. Hot queries then reuse this cache without allocations.
        /// </summary>
        private void RefreshTerrainTileCache(bool force)
        {
            if (mapMagicObject == null)
            {
                _cachedTerrainTiles.Clear();
                _cachedTerrainTileRootCount = -1;
                _lastResolvedTerrainTile = null;
                InvalidateBiomeTextureCache();
                return;
            }

            if (!force)
            {
                ValidateTerrainTileCacheOwnerPhase();
                return;
            }

            RefreshTerrainTileCacheCold();
        }

        private void RefreshTerrainTileCacheCold()
        {
            if (mapMagicObject == null)
            {
                _cachedTerrainTiles.Clear();
                _cachedTerrainTileRootCount = -1;
                _lastResolvedTerrainTile = null;
                InvalidateBiomeTextureCache();
                return;
            }

            Transform mapMagicTransform = mapMagicObject.transform;
            if (mapMagicTransform == null)
                return;

            int rootChildCount = mapMagicTransform.childCount;
            _cachedTerrainTiles.Clear();
            mapMagicObject.GetComponentsInChildren<TerrainTile>(true, _cachedTerrainTiles);
            if (_cachedTerrainTiles.Count > TerrainTileCacheCapacity)
                _cachedTerrainTiles.RemoveRange(TerrainTileCacheCapacity, _cachedTerrainTiles.Count - TerrainTileCacheCapacity);
            if (_cachedTerrainTiles.Capacity > TerrainTileCacheCapacity)
                _cachedTerrainTiles.Capacity = TerrainTileCacheCapacity;
            _cachedTerrainTileRootCount = rootChildCount;
            _lastResolvedTerrainTile = null;
            InvalidateBiomeTextureCache();
            ApplyTerrainDataMemoryBudgetToCachedTerrains();
        }

        private void ValidateTerrainTileCacheOwnerPhase()
        {
            Transform mapMagicTransform = mapMagicObject != null ? mapMagicObject.transform : null;
            if (mapMagicTransform == null)
                return;

            int rootChildCount = mapMagicTransform.childCount;
            List<TerrainTile> terrainTiles = _cachedTerrainTiles;
            int readCount = terrainTiles.Count;
            int writeIndex = 0;

            for (int i = 0; i < readCount; i++)
            {
                TerrainTile tile = terrainTiles[i];
                if (tile == null || tile.mapMagic != mapMagicObject)
                    continue;

                if (writeIndex != i)
                    terrainTiles[writeIndex] = tile;
                writeIndex++;
            }

            if (writeIndex < readCount)
            {
                terrainTiles.RemoveRange(writeIndex, readCount - writeIndex);
                _lastResolvedTerrainTile = null;
                InvalidateBiomeTextureCache();
            }

            if (rootChildCount != _cachedTerrainTileRootCount)
            {
                _cachedTerrainTileRootCount = rootChildCount;
                _lastResolvedTerrainTile = null;
                InvalidateBiomeTextureCache();
            }
        }

        private bool TryCacheTerrainTileOwnerPhase(TerrainTile tile)
        {
            if (tile == null || mapMagicObject == null || tile.mapMagic != mapMagicObject)
                return false;

            List<TerrainTile> terrainTiles = _cachedTerrainTiles;
            int tileCount = terrainTiles.Count;
            for (int i = 0; i < tileCount; i++)
            {
                if (ReferenceEquals(terrainTiles[i], tile))
                    return true;
            }

            if (tileCount >= TerrainTileCacheCapacity || tileCount >= terrainTiles.Capacity)
                return false;

            terrainTiles.Add(tile);
            _cachedTerrainTileRootCount = mapMagicObject.transform != null
                ? mapMagicObject.transform.childCount
                : _cachedTerrainTileRootCount;
            _lastResolvedTerrainTile = null;
            InvalidateBiomeTextureCache();
            return true;
        }

        private void InvalidateBiomeTextureCache()
        {
            _cachedBiomeTerrainData = null;
            if (_cachedBiomeAlphaTextures != null && _cachedBiomeAlphaTextures.Length > 0)
                Array.Clear(_cachedBiomeAlphaTextures, 0, _cachedBiomeAlphaTextures.Length);
            if (_cachedBiomeTerrainLayers != null && _cachedBiomeTerrainLayers.Length > 0)
                Array.Clear(_cachedBiomeTerrainLayers, 0, _cachedBiomeTerrainLayers.Length);
            _cachedBiomeAlphaTextureCount = -1;
            _cachedBiomeAlphaExpectedTextureCount = -1;
            _cachedBiomeTerrainLayerCount = -1;
            _cachedBiomeTerrainLayerExpectedCount = -1;
        }

        private void PrewarmBiomeTextureCacheStorageCold()
        {
            if (_cachedBiomeAlphaTextures == null ||
                _cachedBiomeAlphaTextures.Length != BiomeAlphaTextureCacheCapacity)
            {
                _cachedBiomeAlphaTextures = new Texture2D[BiomeAlphaTextureCacheCapacity]; // COLD ALLOC: fixed terrain alpha texture handle cache - owner: MapMagicBridge
            }

            if (_cachedBiomeTerrainLayers == null ||
                _cachedBiomeTerrainLayers.Length != BiomeTerrainLayerCacheCapacity)
            {
                _cachedBiomeTerrainLayers = new TerrainLayer[BiomeTerrainLayerCacheCapacity]; // COLD ALLOC: fixed terrain layer handle cache - owner: MapMagicBridge
            }
        }

        /// <summary>
        /// Re-applies current MapMagic terrain settings to all cached terrain
        /// instances without rebuilding the graph.
        /// </summary>
        private void ApplyTerrainSettingsToCachedTerrains()
        {
            if (mapMagicObject == null || mapMagicObject.terrainSettings == null)
                return;

            RefreshTerrainTileCache(force: true);

            List<TerrainTile> terrainTiles = _cachedTerrainTiles;
            int tileCount = terrainTiles.Count;

            for (int i = 0; i < tileCount; i++)
            {
                TerrainTile tile = terrainTiles[i];
                if (tile == null)
                    continue;

                Terrain mainTerrain = tile.GetTerrain(false);
                if (mainTerrain != null)
                {
                    mapMagicObject.terrainSettings.ApplySettings(mainTerrain);
                    ApplyTerrainDataMemoryBudget(mainTerrain, false);
                }

                Terrain draftTerrain = tile.GetTerrain(true);
                if (draftTerrain != null)
                {
                    mapMagicObject.terrainSettings.ApplySettings(draftTerrain);
                    ApplyTerrainDataMemoryBudget(draftTerrain, true);
                }
            }
        }

        /// <summary>
        /// Disables Unity Terrain auto-connect at runtime when MapMagic keeps
        /// main and draft tiles alive with incompatible heightmap resolutions.
        /// </summary>
        private void EnsureRuntimeTerrainConnectivityCompatibility(bool forceApplyToCachedTerrains)
        {
            if (mapMagicObject == null || mapMagicObject.terrainSettings == null)
                return;

            if (NormalizeRuntimeDraftResolution())
            {
                forceApplyToCachedTerrains = true;
                _runtimeTerrainResolutionRepairPending = true;
            }

            bool incompatibleDraftConnectivity =
                Application.isPlaying &&
                mapMagicObject.draftsInPlaymode &&
                mapMagicObject.tileResolution != mapMagicObject.draftResolution;

            bool desiredAllowAutoConnect = !incompatibleDraftConnectivity;
            TerrainSettings terrainSettings = mapMagicObject.terrainSettings;
            if (terrainSettings.allowAutoConnect != desiredAllowAutoConnect)
                terrainSettings.allowAutoConnect = desiredAllowAutoConnect;

            if (forceApplyToCachedTerrains)
                ApplyTerrainSettingsToCachedTerrains();
        }

        /// <summary>
        /// Normalizes runtime draft terrain resolution to the main-tile
        /// heightmap so Unity terrain connectivity can stay enabled.
        /// </summary>
        private bool NormalizeRuntimeDraftResolution()
        {
            if (mapMagicObject == null ||
                !Application.isPlaying ||
                !mapMagicObject.draftsInPlaymode ||
                mapMagicObject.tileResolution == mapMagicObject.draftResolution)
            {
                return false;
            }

            mapMagicObject.draftResolution = mapMagicObject.tileResolution;
            return true;
        }

        private void RepairRuntimeTerrainResolutionMismatchIfNeeded()
        {
            if (!_runtimeTerrainResolutionRepairPending ||
                !Application.isPlaying ||
                mapMagicObject == null ||
                mapMagicObject.graph == null)
            {
                return;
            }

            RefreshTerrainTileCache(force: true);
            if (!HasRuntimeTerrainResolutionMismatch())
            {
                _runtimeTerrainResolutionRepairPending = false;
                return;
            }

            int rebuildRange = Mathf.Max(1, Mathf.Max(mapMagicObject.mainRange, mapMagicObject.tiles.generateRange));
            RefreshTerrainTilesForStreaming(rebuildRange, rebuildInRange: true);
            _runtimeTerrainResolutionRepairPending = false;
        }

        private bool HasRuntimeTerrainResolutionMismatch()
        {
            if (mapMagicObject == null)
                return false;

            int expectedMainResolution = (int)mapMagicObject.tileResolution;
            int expectedDraftResolution = (int)mapMagicObject.draftResolution;
            List<TerrainTile> terrainTiles = _cachedTerrainTiles;
            int tileCount = terrainTiles.Count;

            for (int i = 0; i < tileCount; i++)
            {
                TerrainTile tile = terrainTiles[i];
                if (tile == null)
                    continue;

                if (HasTerrainResolutionMismatch(tile.GetTerrain(false), expectedMainResolution) ||
                    HasTerrainResolutionMismatch(tile.GetTerrain(true), expectedDraftResolution))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasTerrainResolutionMismatch(Terrain terrain, int expectedResolution)
        {
            return terrain != null &&
                   terrain.terrainData != null &&
                   terrain.terrainData.heightmapResolution != expectedResolution;
        }

        /// <summary>
        /// Re-evaluates tile draft/main activation after runtime streaming
        /// topology changes. In-range tiles optionally rebuild using the new
        /// resolution settings.
        /// </summary>
        private void RefreshTerrainTilesForStreaming(int activeRange, bool rebuildInRange)
        {
            if (mapMagicObject == null)
                return;

            RefreshTerrainTileCache(force: true);

            List<TerrainTile> terrainTiles = _cachedTerrainTiles;
            int tileCount = terrainTiles.Count;

            for (int i = 0; i < tileCount; i++)
            {
                TerrainTile tile = terrainTiles[i];
                if (tile == null)
                    continue;

                if (tile.distance < 0f)
                    continue;

                if (!Application.isPlaying &&
                    rebuildInRange &&
                    mapMagicObject.graph != null &&
                    (int)tile.distance <= activeRange)
                {
                    tile.Refresh(mapMagicObject.graph, clearAll: true);
                    continue;
                }

                tile.Dist(tile.distance);
            }
        }

        /// <summary>
        /// Creates a runtime terrain detail level and marks it as pending
        /// generation so the next Dist pass can enqueue MapMagic work.
        /// </summary>
        private static TerrainTile.DetailLevel CreateRuntimeDetailLevel(TerrainTile tile, bool isDraft)
        {
            // COLD ALLOC: TerrainTile.DetailLevel[1] — runtime terrain detail level promotion — owner: MapMagicBridge
            TerrainTile.DetailLevel detailLevel = new TerrainTile.DetailLevel(tile, isDraft);
            detailLevel.generateStarted = false;
            detailLevel.generateReady = false;
            detailLevel.applyReady = false;
            return detailLevel;
        }

        /// <summary>
        /// Returns true when a main-detail level can be safely removed without
        /// interrupting an active MapMagic task or the currently visible terrain.
        /// </summary>
        private static bool CanReleaseMainDetailLevel(TerrainTile tile)
        {
            if (tile == null || tile.main == null)
                return false;

            if (tile.ActiveTerrain == tile.main.terrain)
                return false;

            TerrainTile.DetailLevel mainDetail = tile.main;
            if (mainDetail.task != null && (mainDetail.task.Active || mainDetail.task.Enqueued))
                return false;

            if (mainDetail.coroutine != null)
                return false;

            return true;
        }

        /// <summary>
        /// Removes a far-away main-detail terrain and leaves the tile operating
        /// on draft-only data again.
        /// </summary>
        private static void ReleaseMainDetailLevel(TerrainTile tile)
        {
            if (tile == null || tile.main == null)
                return;

            tile.main.Remove();
            tile.main = null;
            tile.Dist(tile.distance);
        }

        /// <summary>
        /// Applies sharper terrain settings to near-field main terrains and a
        /// coarser budget to draft terrains that remain as the far-field shell.
        /// </summary>
        private static void ApplyPerTileTerrainQuality(
            TerrainTile tile,
            int mainRange,
            int mainPixelError,
            int mainBaseMapDistance,
            int draftPixelError,
            int draftBaseMapDistance,
            float detailDistance,
            float detailDensity,
            int heightmapMaximumLod)
        {
            if (tile == null)
                return;

            Terrain mainTerrain = tile.GetTerrain(false);
            Terrain draftTerrain = tile.GetTerrain(true);

            if (mainTerrain != null)
            {
                bool highDetailMain = Mathf.RoundToInt(tile.distance) <= mainRange;
                ApplyTerrainQuality(
                    mainTerrain,
                    highDetailMain ? mainPixelError : draftPixelError,
                    highDetailMain ? mainBaseMapDistance : draftBaseMapDistance,
                    detailDistance,
                    detailDensity,
                    heightmapMaximumLod,
                    !highDetailMain);
            }

            if (draftTerrain != null)
            {
                ApplyTerrainQuality(
                    draftTerrain,
                    draftPixelError,
                    draftBaseMapDistance,
                    detailDistance,
                    detailDensity,
                    heightmapMaximumLod,
                    true);
            }
        }

        /// <summary>
        /// Applies explicit runtime quality overrides directly to a Unity terrain
        /// instance without mutating ScriptableObject authoring state.
        /// </summary>
        private static void ApplyTerrainQuality(
            Terrain terrain,
            int pixelError,
            int baseMapDistance,
            float detailDistance,
            float detailDensity,
            int heightmapMaximumLod,
            bool useDraftBaseMapBudget)
        {
            if (terrain == null || terrain.terrainData == null)
                return;

            terrain.heightmapPixelError = pixelError;
            terrain.basemapDistance = baseMapDistance;
            terrain.heightmapMaximumLOD = heightmapMaximumLod;
            terrain.detailObjectDistance = detailDistance;
            terrain.detailObjectDensity = detailDensity;
            ApplyTerrainDataMemoryBudget(terrain, useDraftBaseMapBudget);
        }

        private void ApplyTerrainDataMemoryBudgetToCachedTerrains()
        {
            List<TerrainTile> terrainTiles = _cachedTerrainTiles;
            int tileCount = terrainTiles.Count;
            for (int i = 0; i < tileCount; i++)
            {
                TerrainTile tile = terrainTiles[i];
                if (tile == null)
                    continue;

                ApplyTerrainDataMemoryBudget(tile.GetTerrain(false), false);
                ApplyTerrainDataMemoryBudget(tile.GetTerrain(true), true);
            }
        }

        private static void ApplyTerrainDataMemoryBudget(Terrain terrain, bool isDraft)
        {
            if (terrain == null || terrain.terrainData == null)
                return;

            TerrainData terrainData = terrain.terrainData;
            int targetResolution = isDraft
                ? DraftTerrainBaseMapResolutionBudget
                : MainTerrainBaseMapResolutionBudget;
            if (terrainData.baseMapResolution > targetResolution)
                terrainData.baseMapResolution = targetResolution;
        }

        /// <summary>
        /// Resolves the best terrain representation for a tile. MapMagic keeps
        /// runtime data on draft terrain when ActiveTerrain/main are null.
        /// </summary>
        private static Terrain ResolveTileTerrain(TerrainTile tile)
        {
            if (tile == null)
                return null;

            Terrain mainTerrain = tile.GetTerrain(false);
            if (mainTerrain != null && mainTerrain.terrainData != null)
                return mainTerrain;

            Terrain activeTerrain = tile.ActiveTerrain;
            if (activeTerrain != null && activeTerrain.terrainData != null)
                return activeTerrain;

            Terrain draftTerrain = tile.GetTerrain(true);
            if (draftTerrain != null && draftTerrain.terrainData != null)
                return draftTerrain;

            return null;
        }

        private bool TryCreateTerrainTileSnapshot(TerrainTile tile, out MapMagicTerrainTileSnapshot snapshot)
        {
            snapshot = default;
            if (tile == null || mapMagicObject == null || tile.mapMagic != mapMagicObject)
                return false;

            Terrain terrain = ResolveTileTerrain(tile);
            if (terrain == null || terrain.terrainData == null)
                return false;

            snapshot = new MapMagicTerrainTileSnapshot(this, tile.coord.x, tile.coord.z, terrain);
            return true;
        }

        private void HandleTerrainTileApplied(TerrainTile tile, TileData tileData, StopToken stop)
        {
            TryCacheTerrainTileOwnerPhase(tile);
            if (TryCreateTerrainTileSnapshot(tile, out MapMagicTerrainTileSnapshot snapshot))
                MapMagicTerrainTileEvents.TryRaiseTileApplied(in snapshot);
        }

        private void HandleTerrainTileMoved(TerrainTile tile)
        {
            TryCacheTerrainTileOwnerPhase(tile);
            if (TryCreateTerrainTileSnapshot(tile, out MapMagicTerrainTileSnapshot snapshot))
                MapMagicTerrainTileEvents.TryRaiseTileMoved(in snapshot);
        }

        private void TrySubscribeTerrainTileEvents()
        {
            if (_terrainTileEventsSubscribed)
                return;

            TerrainTile.OnTileComplete += HandleTerrainTileApplied;
            TerrainTile.OnTileMoved += HandleTerrainTileMoved;
            _terrainTileEventsSubscribed = true;
        }

        private void TryUnsubscribeTerrainTileEvents()
        {
            if (!_terrainTileEventsSubscribed)
                return;

            TerrainTile.OnTileComplete -= HandleTerrainTileApplied;
            TerrainTile.OnTileMoved -= HandleTerrainTileMoved;
            _terrainTileEventsSubscribed = false;
        }

        /// <summary>
        /// Proveryaet, popadaet li tochka (x, z) v bounds terreyna.
        /// </summary>
        private static bool IsPointInTerrain(
            Terrain terrain, float x, float z)
        {
            if (terrain.terrainData == null)
                return false;

            Vector3 pos  = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;

            return x >= pos.x && x <= pos.x + size.x &&
                   z >= pos.z && z <= pos.z + size.z;
        }

        // ══════════════════════════════════════════════════════════
        //  DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateDiagnostics()
        {
            _debugMapMagicFound = mapMagicObject != null;
            _debugPlayerFound   = playerTransform != null;
            _debugTileCount     = _cachedTerrainTiles.Count;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateBiomeDiagnostics(int biomeID)
        {
            _debugCurrentBiome = biomeID;
        }
    }
}
