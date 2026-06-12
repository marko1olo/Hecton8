using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Gameplay;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4150)]
    public sealed class WorldStreamingDirector : MonoBehaviour, ISlowTickable, IGlobalRegistryHotSwapListener
#if UNITY_EDITOR
        , RuntimeWatchdog.IEmergencyResetTarget
#endif
    {
        private const string DepthZoneSurfaceLabel = "Surface";
        private const string DepthZoneMidLabel = "Mid";
        private const string DepthZoneDeepLabel = "Deep";
        private const string MotionModeSurveyLabel = "Survey";
        private const string MotionModeTraverseLabel = "Traverse";
        private const string TerrainResolution33Label = "33";
        private const string TerrainResolution65Label = "65";
        private const string TerrainResolution129Label = "129";
        private const string TerrainResolution257Label = "257";
        private const string TerrainResolution513Label = "513";
        private const string TerrainResolution1025Label = "1025";
        private const string TerrainResolution2049Label = "2049";
        private const uint KccVelocityStreamingMaxAgeFrames = 12u;
        private const float DefaultWaterSurfaceLevelY = WorldWaterLevelCalibrationMath.DefaultWaterLevelY;

        private enum DepthZone
        {
            Surface,
            Mid,
            Deep
        }

        private enum MotionMode
        {
            Survey,
            Traverse
        }

        [System.Serializable]
        private struct StreamingProfile
        {
            public int mapMagicObjectsPerFrame;
            public float scavengeRadiusScale;
            public float scavengeSpawnScale;
            public float colliderRadiusScale;
            public float colliderOpsScale;
            public float nearSliceScale;
            public float midSliceScale;
            public int terrainPixelError;
            public int terrainBaseMapDistance;
            public float terrainDetailDistance;
            public float terrainDetailDensity;
        }

        [Header("References")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private Rigidbody playerRigidbody;
        [SerializeField] private MapMagicBridge mapMagicBridge;
        [SerializeField] private BiomeSamplerCache biomeSamplerCache;
        [SerializeField] private ScatterBudgetController scatterBudgetController;
        [SerializeField] private WorldSliceDirector worldSliceDirector;
        [SerializeField] private WorldChunkStreamingProfile chunkStreamingProfile;

        [Header("Depth Thresholds")]
        [SerializeField] private float midDepthStart = 60f;
        [SerializeField] private float deepDepthStart = 180f;

        [Header("Motion Thresholds")]
        [SerializeField] private float traverseSpeedStart = 4.5f;
        [SerializeField] private float speedSmoothing = 0.2f;

        [Header("Terrain Runtime LOD")]
        [SerializeField] private bool terrainDraftsInPlaymode = true;
        [SerializeField] private int terrainDraftResolution = 65;
        [SerializeField] private int terrainMainRange = 2;
        [SerializeField] private int terrainDraftRange = 2;
        [SerializeField] private int terrainMainTeardownRange = 3;
        [SerializeField] private int terrainMainPixelError = 2;
        [SerializeField] private int terrainMainBaseMapDistance = 1000;
        [SerializeField] private int terrainDraftPixelError = 6;
        [SerializeField] private int terrainDraftBaseMapDistance = 384;
        [SerializeField] private int terrainHeightmapMaximumLod;

        [Header("Profiles")]
        [SerializeField] private StreamingProfile surfaceSurveyProfile;
        [SerializeField] private StreamingProfile surfaceTraverseProfile;
        [SerializeField] private StreamingProfile midSurveyProfile;
        [SerializeField] private StreamingProfile midTraverseProfile;
        [SerializeField] private StreamingProfile deepSurveyProfile;
        [SerializeField] private StreamingProfile deepTraverseProfile;

        // Inspector-only live diagnostics for streaming-profile switching.
#pragma warning disable CS0414
        [Header("Diagnostics")]
        [SerializeField] private string _debugDepthZone = "Surface";
        [SerializeField] private string _debugMotionMode = "Survey";
        [SerializeField] private float _debugDepth;
        [SerializeField] private float _debugSpeed;
        [SerializeField] private int _debugMapMagicObjectsPerFrame;
        [SerializeField] private float _debugNearSliceScale = 1f;
        [SerializeField] private float _debugMidSliceScale = 1f;
        [SerializeField] private bool _debugTerrainDraftsInPlaymode;
        [SerializeField] private string _debugTerrainDraftResolution = "65";
        [SerializeField] private int _debugTerrainMainRange = 1;
        [SerializeField] private int _debugTerrainDraftRange = 2;
        [SerializeField] private int _debugTerrainPixelError = 4;
        [SerializeField] private int _debugTerrainBaseMapDistance = 1600;
        [SerializeField] private float _debugTerrainDetailDistance = 96f;
        [SerializeField] private bool _debugApplied;
        [SerializeField] private bool _debugPlayerReady;
        [SerializeField] private bool _debugBridgeReady;
        [SerializeField] private bool _debugBiomeCacheReady;
        [SerializeField] private bool _debugBudgetReady;
        [SerializeField] private bool _debugUsingSharedChunkProfile;
#pragma warning restore CS0414

        private bool _registeredToTickManager;
        private float _smoothedSpeedSq;
        private DepthZone _lastDepthZone = (DepthZone)(-1);
        private MotionMode _lastMotionMode = (MotionMode)(-1);
        private int _lastObjectsPerFrame = -1;
        private int _lastTerrainPixelError = -1;
        private int _lastTerrainBaseMapDistance = -1;
        private float _lastTerrainDetailDistance = -1f;
        private float _lastTerrainDetailDensity = -1f;
        private bool _lastTerrainDraftsInPlaymode;
        private int _lastTerrainMainRange = -1;
        private int _lastTerrainDraftRange = -1;
        private int _lastTerrainDraftResolution = -1;
        private bool _terrainStreamingTopologyDirty = true;
        private bool _profilesDirty = true;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private HectonPlayerMovement _playerMovement;
        private IHectonOceanKinematicsService _oceanKinematicsService;

        private void Reset()
        {
            surfaceSurveyProfile = new StreamingProfile
            {
                mapMagicObjectsPerFrame = 96,
                scavengeRadiusScale = 1f,
                scavengeSpawnScale = 1f,
                colliderRadiusScale = 1f,
                colliderOpsScale = 1f,
                nearSliceScale = 1.06f,
                midSliceScale = 1f,
                terrainPixelError = 3,
                terrainBaseMapDistance = 2200,
                terrainDetailDistance = 120f,
                terrainDetailDensity = 1f
            };

            surfaceTraverseProfile = new StreamingProfile
            {
                mapMagicObjectsPerFrame = 144,
                scavengeRadiusScale = 0.92f,
                scavengeSpawnScale = 0.82f,
                colliderRadiusScale = 0.9f,
                colliderOpsScale = 0.82f,
                nearSliceScale = 0.86f,
                midSliceScale = 1.16f,
                terrainPixelError = 4,
                terrainBaseMapDistance = 1800,
                terrainDetailDistance = 100f,
                terrainDetailDensity = 0.92f
            };

            midSurveyProfile = new StreamingProfile
            {
                mapMagicObjectsPerFrame = 88,
                scavengeRadiusScale = 0.9f,
                scavengeSpawnScale = 0.88f,
                colliderRadiusScale = 0.86f,
                colliderOpsScale = 0.84f,
                nearSliceScale = 1f,
                midSliceScale = 0.96f,
                terrainPixelError = 4,
                terrainBaseMapDistance = 2000,
                terrainDetailDistance = 108f,
                terrainDetailDensity = 0.96f
            };

            midTraverseProfile = new StreamingProfile
            {
                mapMagicObjectsPerFrame = 128,
                scavengeRadiusScale = 0.82f,
                scavengeSpawnScale = 0.72f,
                colliderRadiusScale = 0.78f,
                colliderOpsScale = 0.72f,
                nearSliceScale = 0.82f,
                midSliceScale = 1.12f,
                terrainPixelError = 5,
                terrainBaseMapDistance = 1600,
                terrainDetailDistance = 92f,
                terrainDetailDensity = 0.88f
            };

            deepSurveyProfile = new StreamingProfile
            {
                mapMagicObjectsPerFrame = 72,
                scavengeRadiusScale = 0.78f,
                scavengeSpawnScale = 0.76f,
                colliderRadiusScale = 0.72f,
                colliderOpsScale = 0.72f,
                nearSliceScale = 0.94f,
                midSliceScale = 0.92f,
                terrainPixelError = 5,
                terrainBaseMapDistance = 1700,
                terrainDetailDistance = 96f,
                terrainDetailDensity = 0.88f
            };

            deepTraverseProfile = new StreamingProfile
            {
                mapMagicObjectsPerFrame = 112,
                scavengeRadiusScale = 0.68f,
                scavengeSpawnScale = 0.58f,
                colliderRadiusScale = 0.62f,
                colliderOpsScale = 0.58f,
                nearSliceScale = 0.74f,
                midSliceScale = 1.06f,
                terrainPixelError = 6,
                terrainBaseMapDistance = 1400,
                terrainDetailDistance = 80f,
                terrainDetailDensity = 0.78f
            };
        }

        private void Awake()
        {
            ResolveReferences();
            RefreshRuntimeProfilesFromChunkProfile();
            ClampSettings();
            ApplyStreamingProfile(force: true);
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            RuntimeWatchdog.RegisterEmergencyResetTarget(RuntimeWatchdog.RuntimeWatchdogLane.WorldStreaming, this);
#endif
            if (Application.isPlaying)
                GlobalRegistry.TryRegisterHotSwapListener(this);

            TryRegister();
        }

        private void Start()
        {
            TryRegister();

            ApplyStreamingProfile(force: true);
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            RuntimeWatchdog.UnregisterEmergencyResetTarget(RuntimeWatchdog.RuntimeWatchdogLane.WorldStreaming, this);
#endif
            TryUnregister();
            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _oceanKinematicsService = null;
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR
            RuntimeWatchdog.UnregisterEmergencyResetTarget(RuntimeWatchdog.RuntimeWatchdogLane.WorldStreaming, this);
#endif
            TryUnregister();
            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _oceanKinematicsService = null;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregister();
                    if (isActiveAndEnabled && currentService != null)
                        TryRegister();
                    break;
                case GlobalRegistryServiceSlot.Player:
                    RebindPlayerRuntimeContext(currentService as IPlayerRuntimeContext);
                    InvalidateStreamingProfileState();
                    if (isActiveAndEnabled && currentService != null)
                        ApplyStreamingProfile(force: true);
                    break;
                case GlobalRegistryServiceSlot.MapMagicRuntime:
                    mapMagicBridge = currentService as MapMagicBridge;
                    InvalidateStreamingProfileState();
                    if (isActiveAndEnabled && currentService != null)
                        ApplyStreamingProfile(force: true);
                    break;
                case GlobalRegistryServiceSlot.TerrainProviderRuntime:
                    if (currentService is MapMagicBridge currentMapMagic)
                        mapMagicBridge = currentMapMagic;
                    else if (ReferenceEquals(previousService, mapMagicBridge))
                        mapMagicBridge = null;

                    InvalidateStreamingProfileState();
                    if (isActiveAndEnabled && currentService != null)
                        ApplyStreamingProfile(force: true);
                    break;
                case GlobalRegistryServiceSlot.OceanKinematics:
                    _oceanKinematicsService = currentService as IHectonOceanKinematicsService;
                    InvalidateStreamingProfileState();
                    if (isActiveAndEnabled)
                        ApplyStreamingProfile(force: true);
                    break;
            }
        }

        internal void ServiceEmergencyReset()
        {
            InvalidateStreamingProfileState();
            _profilesDirty = true;
            ResolveReferences();
            ApplyStreamingProfile(force: true);
        }

#if UNITY_EDITOR
        void RuntimeWatchdog.IEmergencyResetTarget.ServiceEmergencyReset()
        {
            ServiceEmergencyReset();
        }
#endif

        private void TryRegister()
        {
            if (_registeredToTickManager || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
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

        public void SlowTick()
        {
#if UNITY_EDITOR
            RuntimeWatchdog.Signal(RuntimeWatchdog.RuntimeWatchdogLane.WorldStreaming);
#endif
            ApplyStreamingProfile(force: false);
        }

        public void SetChunkStreamingProfile(WorldChunkStreamingProfile profile)
        {
            chunkStreamingProfile = profile;
            _profilesDirty = true;
            ApplyStreamingProfile(force: true);
        }

        private void ApplyStreamingProfile(bool force)
        {
            ResolveReferences();
            if (force || _profilesDirty)
            {
                RefreshRuntimeProfilesFromChunkProfile();
                ClampSettings();
                _profilesDirty = false;
            }

            StreamingProfile activeProfile = surfaceSurveyProfile;

            bool topologyChanged = ConfigureMapMagicTerrainTopology(force);
            force |= topologyChanged;

            if (playerTransform == null || scatterBudgetController == null || !TryResolveCurrentDepth(out float depth))
            {
                if (mapMagicBridge != null && mapMagicBridge.IsAvailable)
                {
                    mapMagicBridge.MaintainRuntimeTerrainDetailLevels(
                        terrainMainRange,
                        terrainMainTeardownRange,
                        terrainMainPixelError,
                        terrainMainBaseMapDistance,
                        terrainDraftPixelError,
                        terrainDraftBaseMapDistance,
                        activeProfile.terrainDetailDistance,
                        activeProfile.terrainDetailDensity,
                        terrainHeightmapMaximumLod);
                }

                ApplyMapMagicTerrainProfile(surfaceSurveyProfile);
#if UNITY_EDITOR
                _debugApplied = false;
                UpdateDiagnostics();
#endif
                return;
            }

            float targetSpeedSq = GetCurrentSpeedSq();
            _smoothedSpeedSq = math.lerp(_smoothedSpeedSq, targetSpeedSq, math.saturate(speedSmoothing));

            DepthZone depthZone = GetDepthZone(depth);
            float traverseSpeedThresholdSq = traverseSpeedStart * traverseSpeedStart;
            MotionMode motionMode = _smoothedSpeedSq >= traverseSpeedThresholdSq ? MotionMode.Traverse : MotionMode.Survey;
            StreamingProfile profile = GetProfile(depthZone, motionMode);
            activeProfile = profile;

            if (mapMagicBridge != null && mapMagicBridge.IsAvailable)
            {
                mapMagicBridge.MaintainRuntimeTerrainDetailLevels(
                    terrainMainRange,
                    terrainMainTeardownRange,
                    terrainMainPixelError,
                    terrainMainBaseMapDistance,
                    terrainDraftPixelError,
                    terrainDraftBaseMapDistance,
                    activeProfile.terrainDetailDistance,
                    activeProfile.terrainDetailDensity,
                    terrainHeightmapMaximumLod);
            }

#if UNITY_EDITOR
            _debugDepth = depth;
            _debugSpeed = ApproximateSqrtPositive(_smoothedSpeedSq);
            _debugDepthZone = GetDepthZoneLabel(depthZone);
            _debugMotionMode = GetMotionModeLabel(motionMode);
#endif

            bool changed =
                force ||
                depthZone != _lastDepthZone ||
                motionMode != _lastMotionMode ||
                profile.mapMagicObjectsPerFrame != _lastObjectsPerFrame ||
                profile.terrainPixelError != _lastTerrainPixelError ||
                profile.terrainBaseMapDistance != _lastTerrainBaseMapDistance ||
                !Mathf.Approximately(profile.terrainDetailDistance, _lastTerrainDetailDistance) ||
                !Mathf.Approximately(profile.terrainDetailDensity, _lastTerrainDetailDensity);

            if (!changed)
            {
#if UNITY_EDITOR
                UpdateDiagnostics();
#endif
                return;
            }

            scatterBudgetController.SetDirectorScales(
                profile.scavengeRadiusScale,
                profile.scavengeSpawnScale,
                profile.colliderRadiusScale,
                profile.colliderOpsScale);
            if (worldSliceDirector != null)
                worldSliceDirector.SetDistanceScales(profile.nearSliceScale, profile.midSliceScale);

            ApplyMapMagicTerrainProfile(profile);

            _lastDepthZone = depthZone;
            _lastMotionMode = motionMode;
            _lastObjectsPerFrame = profile.mapMagicObjectsPerFrame;
            _lastTerrainPixelError = profile.terrainPixelError;
            _lastTerrainBaseMapDistance = profile.terrainBaseMapDistance;
            _lastTerrainDetailDistance = profile.terrainDetailDistance;
            _lastTerrainDetailDensity = profile.terrainDetailDensity;
#if UNITY_EDITOR
            _debugNearSliceScale = profile.nearSliceScale;
            _debugMidSliceScale = profile.midSliceScale;
            _debugApplied = true;
            UpdateDiagnostics();
#endif
        }

        private void ApplyMapMagicTerrainProfile(StreamingProfile profile)
        {
            if (mapMagicBridge == null || !mapMagicBridge.IsAvailable)
            {
#if UNITY_EDITOR
                _debugMapMagicObjectsPerFrame = -1;
#endif
                return;
            }

            mapMagicBridge.SetRuntimeObjectsPerFrame(profile.mapMagicObjectsPerFrame);
            mapMagicBridge.ApplyRuntimeTerrainQuality(
                profile.terrainPixelError,
                profile.terrainBaseMapDistance,
                profile.terrainDetailDistance,
                profile.terrainDetailDensity,
                terrainHeightmapMaximumLod);

#if UNITY_EDITOR
            _debugMapMagicObjectsPerFrame = profile.mapMagicObjectsPerFrame;
            _debugTerrainDraftsInPlaymode = terrainDraftsInPlaymode;
            _debugTerrainDraftResolution = GetTerrainResolutionLabel(terrainDraftResolution);
            _debugTerrainMainRange = terrainMainRange;
            _debugTerrainDraftRange = terrainDraftRange;
            _debugTerrainPixelError = profile.terrainPixelError;
            _debugTerrainBaseMapDistance = profile.terrainBaseMapDistance;
            _debugTerrainDetailDistance = profile.terrainDetailDistance;
#endif
        }

        private bool ConfigureMapMagicTerrainTopology(bool force)
        {
            if (mapMagicBridge == null || !mapMagicBridge.IsAvailable)
            {
                _terrainStreamingTopologyDirty = true;
                return false;
            }

            bool settingsChanged =
                _terrainStreamingTopologyDirty ||
                force ||
                _lastTerrainDraftsInPlaymode != terrainDraftsInPlaymode ||
                _lastTerrainMainRange != terrainMainRange ||
                _lastTerrainDraftRange != terrainDraftRange ||
                _lastTerrainDraftResolution != terrainDraftResolution;

            if (!settingsChanged)
                return false;

            bool topologyChanged = mapMagicBridge.ConfigureRuntimeTerrainStreaming(
                terrainDraftsInPlaymode,
                terrainMainRange,
                terrainDraftRange,
                terrainDraftResolution);

            _lastTerrainDraftsInPlaymode = terrainDraftsInPlaymode;
            _lastTerrainMainRange = terrainMainRange;
            _lastTerrainDraftRange = terrainDraftRange;
            _lastTerrainDraftResolution = terrainDraftResolution;
            _terrainStreamingTopologyDirty = false;
            return topologyChanged;
        }

        private static string GetTerrainResolutionLabel(int resolution)
        {
            switch (resolution)
            {
                case 33:
                    return TerrainResolution33Label;
                case 65:
                    return TerrainResolution65Label;
                case 129:
                    return TerrainResolution129Label;
                case 257:
                    return TerrainResolution257Label;
                case 513:
                    return TerrainResolution513Label;
                case 1025:
                    return TerrainResolution1025Label;
                case 2049:
                    return TerrainResolution2049Label;
                default:
                    return TerrainResolution65Label;
            }
        }

        private static int NormalizeTerrainDraftResolution(int resolution)
        {
            switch (resolution)
            {
                case 33:
                case 65:
                case 129:
                case 257:
                case 513:
                case 1025:
                case 2049:
                    return resolution;
                default:
                    return 65;
            }
        }

        private static float ApproximateSqrtPositive(float value)
        {
            float safeValue = math.max(0f, value);
            float invSqrt = math.rsqrt(math.max(0.0001f, safeValue));
            return math.select(0f, safeValue * invSqrt, safeValue > 0f);
        }

        private float GetCurrentSpeedSq()
        {
            if (CoreDeterminismSignals.TryGetLatestKccVelocityFloat3(KccVelocityStreamingMaxAgeFrames, out float3 velocity))
                return math.lengthsq(velocity);

            return 0f;
        }

        private DepthZone GetDepthZone(float depth)
        {
            if (depth >= deepDepthStart)
                return DepthZone.Deep;

            if (depth >= midDepthStart)
                return DepthZone.Mid;

            return DepthZone.Surface;
        }

        private StreamingProfile GetProfile(DepthZone depthZone, MotionMode motionMode)
        {
            if (depthZone == DepthZone.Deep)
                return motionMode == MotionMode.Traverse ? deepTraverseProfile : deepSurveyProfile;

            if (depthZone == DepthZone.Mid)
                return motionMode == MotionMode.Traverse ? midTraverseProfile : midSurveyProfile;

            return motionMode == MotionMode.Traverse ? surfaceTraverseProfile : surfaceSurveyProfile;
        }

        private static string GetDepthZoneLabel(DepthZone depthZone)
        {
            switch (depthZone)
            {
                case DepthZone.Mid:
                    return DepthZoneMidLabel;
                case DepthZone.Deep:
                    return DepthZoneDeepLabel;
                default:
                    return DepthZoneSurfaceLabel;
            }
        }

        private static string GetMotionModeLabel(MotionMode motionMode)
        {
            return motionMode == MotionMode.Traverse
                ? MotionModeTraverseLabel
                : MotionModeSurveyLabel;
        }

        private void ResolveReferences()
        {
            if (_playerRuntimeContext == null)
            {
                IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;
                if (IsPlayerRuntimeContextBound(runtimeContext))
                {
                    RebindPlayerRuntimeContext(runtimeContext);
                }
            }
            else if (!IsPlayerRuntimeContextBound(_playerRuntimeContext))
            {
                RebindPlayerRuntimeContext(null);
            }

            if (playerTransform == null)
                WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);

            if (mapMagicBridge == null || !mapMagicBridge.isActiveAndEnabled)
                WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicBridge);

            if (_oceanKinematicsService == null)
                _oceanKinematicsService = GlobalRegistry.OceanKinematics;

            if (biomeSamplerCache == null || !biomeSamplerCache.isActiveAndEnabled)
                WorldRuntimeReferenceUtility.TryResolveBiomeSamplerCache(ref biomeSamplerCache);

            if (scatterBudgetController == null || !scatterBudgetController.isActiveAndEnabled)
                WorldRuntimeReferenceUtility.TryResolveScatterBudgetController(ref scatterBudgetController);

            if (worldSliceDirector == null || !worldSliceDirector.isActiveAndEnabled)
                WorldRuntimeReferenceUtility.TryResolveWorldSliceDirector(ref worldSliceDirector);
        }

        private void RebindPlayerRuntimeContext(IPlayerRuntimeContext runtimeContext)
        {
            if (IsPlayerRuntimeContextBound(runtimeContext))
            {
                _playerRuntimeContext = runtimeContext;
                playerTransform = runtimeContext.PlayerTransform;
                playerRigidbody = runtimeContext.PlayerRigidbody;
                _playerMovement = runtimeContext.PlayerMovement;
                return;
            }

            _playerRuntimeContext = null;
            playerTransform = null;
            playerRigidbody = null;
            _playerMovement = null;
        }

        private static bool IsPlayerRuntimeContextBound(IPlayerRuntimeContext runtimeContext)
        {
            return runtimeContext != null &&
                   runtimeContext.IsInitialized &&
                   runtimeContext.PlayerObject != null &&
                   runtimeContext.PlayerTransform != null;
        }

        private void InvalidateStreamingProfileState()
        {
            _smoothedSpeedSq = 0f;
            _lastDepthZone = (DepthZone)(-1);
            _lastMotionMode = (MotionMode)(-1);
            _lastObjectsPerFrame = -1;
            _lastTerrainPixelError = -1;
            _lastTerrainBaseMapDistance = -1;
            _lastTerrainDetailDistance = -1f;
            _lastTerrainDetailDensity = -1f;
            _lastTerrainMainRange = -1;
            _lastTerrainDraftRange = -1;
            _lastTerrainDraftResolution = -1;
            _terrainStreamingTopologyDirty = true;
        }

        private bool TryResolveCurrentDepth(out float depth)
        {
            depth = 0f;

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null &&
                playerContext.IsInitialized &&
                playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                math.isfinite(movementState.DepthMeters))
            {
                depth = math.max(0f, movementState.DepthMeters);
                return true;
            }

            if (playerContext != null)
                return false;

            if (_playerMovement != null && math.isfinite(_playerMovement.CurrentDepth))
            {
                depth = math.max(0f, _playerMovement.CurrentDepth);
                return true;
            }

            if (playerTransform == null)
                return false;

            depth = Mathf.Max(0f, ResolveWaterSurfaceLevel() - playerTransform.position.y);
            return true;
        }

        private float ResolveWaterSurfaceLevel()
        {
            if (TryResolveOceanWaterSurfaceLevel(out float oceanWaterSurfaceLevel))
                return oceanWaterSurfaceLevel;

            if (mapMagicBridge != null && TryResolveWaterSurfaceLevel(mapMagicBridge.WaterSurfaceLevel, out float waterSurfaceLevel))
                return waterSurfaceLevel;

            return DefaultWaterSurfaceLevelY;
        }

        private bool TryResolveOceanWaterSurfaceLevel(out float waterSurfaceLevel)
        {
            IHectonOceanKinematicsService oceanKinematicsService = _oceanKinematicsService;
            IHectonOceanKinematics oceanKinematics = oceanKinematicsService != null && oceanKinematicsService.IsInitialized
                ? oceanKinematicsService.ActiveProvider
                : null;

            if (oceanKinematics != null &&
                oceanKinematics.IsAvailable &&
                TryResolveOceanWaterSurfaceLevel(oceanKinematics.SeaLevel, out waterSurfaceLevel))
            {
                return true;
            }

            waterSurfaceLevel = DefaultWaterSurfaceLevelY;
            return false;
        }

        private static bool TryResolveOceanWaterSurfaceLevel(float candidateWaterSurfaceLevel, out float waterSurfaceLevel)
        {
            if (math.isfinite(candidateWaterSurfaceLevel) &&
                math.abs(candidateWaterSurfaceLevel) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                waterSurfaceLevel = candidateWaterSurfaceLevel;
                return true;
            }

            waterSurfaceLevel = DefaultWaterSurfaceLevelY;
            return false;
        }

        private static bool TryResolveWaterSurfaceLevel(float candidateWaterSurfaceLevel, out float waterSurfaceLevel)
        {
            if (math.isfinite(candidateWaterSurfaceLevel) &&
                math.abs(candidateWaterSurfaceLevel) > 0.0001f &&
                math.abs(candidateWaterSurfaceLevel) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                waterSurfaceLevel = candidateWaterSurfaceLevel;
                return true;
            }

            waterSurfaceLevel = DefaultWaterSurfaceLevelY;
            return false;
        }

        private void ClampSettings()
        {
            midDepthStart = Mathf.Max(10f, midDepthStart);
            deepDepthStart = Mathf.Max(midDepthStart + 20f, deepDepthStart);
            traverseSpeedStart = Mathf.Max(0.5f, traverseSpeedStart);
            speedSmoothing = Mathf.Clamp01(speedSmoothing);
            terrainMainRange = Mathf.Clamp(terrainMainRange, 2, 4);
            terrainDraftRange = Mathf.Max(terrainMainRange, terrainDraftRange);
            terrainMainTeardownRange = Mathf.Max(terrainMainRange + 1, terrainMainTeardownRange);
            terrainMainPixelError = Mathf.Clamp(terrainMainPixelError, 1, 3);
            terrainMainBaseMapDistance = Mathf.Clamp(terrainMainBaseMapDistance, 512, 2000);
            terrainDraftPixelError = Mathf.Clamp(terrainDraftPixelError, terrainMainPixelError + 1, 12);
            terrainDraftBaseMapDistance = Mathf.Clamp(terrainDraftBaseMapDistance, 256, terrainMainBaseMapDistance);
            terrainHeightmapMaximumLod = Mathf.Clamp(terrainHeightmapMaximumLod, 0, 3);
            terrainDraftResolution = NormalizeTerrainDraftResolution(terrainDraftResolution);

            surfaceSurveyProfile = ClampProfile(surfaceSurveyProfile);
            surfaceTraverseProfile = ClampProfile(surfaceTraverseProfile);
            midSurveyProfile = ClampProfile(midSurveyProfile);
            midTraverseProfile = ClampProfile(midTraverseProfile);
            deepSurveyProfile = ClampProfile(deepSurveyProfile);
            deepTraverseProfile = ClampProfile(deepTraverseProfile);
        }

        private void RefreshRuntimeProfilesFromChunkProfile()
        {
#if UNITY_EDITOR
            _debugUsingSharedChunkProfile = chunkStreamingProfile != null;
#endif
            if (chunkStreamingProfile == null)
                return;

            WorldChunkStreamingProfile.LayerProfile terrainLayer =
                chunkStreamingProfile.GetLayerProfileOrDefault(WorldStreamingLayer.TerrainLod);
            WorldChunkStreamingProfile.LayerProfile floraLayer =
                chunkStreamingProfile.GetLayerProfileOrDefault(WorldStreamingLayer.Flora);
            WorldChunkStreamingProfile.LayerProfile debrisLayer =
                chunkStreamingProfile.GetLayerProfileOrDefault(WorldStreamingLayer.Debris);
            WorldChunkStreamingProfile.LayerProfile resourcesLayer =
                chunkStreamingProfile.GetLayerProfileOrDefault(WorldStreamingLayer.Resources);

            surfaceSurveyProfile = BuildProfileFromWorldScale(1f, false, terrainLayer, floraLayer, debrisLayer, resourcesLayer);
            surfaceTraverseProfile = BuildProfileFromWorldScale(1f, true, terrainLayer, floraLayer, debrisLayer, resourcesLayer);
            midSurveyProfile = BuildProfileFromWorldScale(0.86f, false, terrainLayer, floraLayer, debrisLayer, resourcesLayer);
            midTraverseProfile = BuildProfileFromWorldScale(0.86f, true, terrainLayer, floraLayer, debrisLayer, resourcesLayer);
            deepSurveyProfile = BuildProfileFromWorldScale(0.72f, false, terrainLayer, floraLayer, debrisLayer, resourcesLayer);
            deepTraverseProfile = BuildProfileFromWorldScale(0.72f, true, terrainLayer, floraLayer, debrisLayer, resourcesLayer);
        }

        private static StreamingProfile BuildProfileFromWorldScale(
            float depthScale,
            bool traverse,
            WorldChunkStreamingProfile.LayerProfile terrainLayer,
            WorldChunkStreamingProfile.LayerProfile floraLayer,
            WorldChunkStreamingProfile.LayerProfile debrisLayer,
            WorldChunkStreamingProfile.LayerProfile resourcesLayer)
        {
            float traverseScale = traverse ? 1.28f : 1f;
            float traverseCompression = traverse ? 0.86f : 1f;
            int objectBudget = Mathf.RoundToInt(
                (terrainLayer.maxChunkLoadsPerTick * 18f +
                 floraLayer.maxChunkLoadsPerTick * 10f +
                 debrisLayer.maxChunkLoadsPerTick * 8f) * depthScale * traverseScale);

            return new StreamingProfile
            {
                mapMagicObjectsPerFrame = Mathf.Max(48, objectBudget),
                scavengeRadiusScale = Mathf.Clamp(resourcesLayer.nearRadiusScale * depthScale * traverseCompression, 0.55f, 1.6f),
                scavengeSpawnScale = Mathf.Clamp((resourcesLayer.maxActivationsPerTick / 24f) * depthScale * (traverse ? 0.92f : 1f), 0.5f, 1.6f),
                colliderRadiusScale = Mathf.Clamp(((debrisLayer.nearRadiusScale + terrainLayer.nearRadiusScale) * 0.5f) * depthScale * traverseCompression, 0.55f, 1.5f),
                colliderOpsScale = Mathf.Clamp((debrisLayer.maxActivationsPerTick / 18f) * depthScale * (traverse ? 0.88f : 1f), 0.55f, 1.6f),
                nearSliceScale = Mathf.Clamp(terrainLayer.nearRadiusScale * (traverse ? 0.82f : 1f), 0.6f, 1.6f),
                midSliceScale = Mathf.Clamp(terrainLayer.midRadiusScale * (traverse ? 1.08f : 1f), 0.6f, 1.7f),
                terrainPixelError = Mathf.Clamp(Mathf.RoundToInt((traverse ? 3.5f : 2.5f) + (1f - depthScale) * 6f), 3, 8),
                terrainBaseMapDistance = Mathf.RoundToInt(Mathf.Clamp(1400f * terrainLayer.farRadiusScale * depthScale * (traverse ? 0.95f : 1.15f), 1200f, 2400f)),
                terrainDetailDistance = Mathf.Clamp(96f * terrainLayer.nearRadiusScale * depthScale * (traverse ? 0.94f : 1.08f), 72f, 128f),
                terrainDetailDensity = Mathf.Clamp(depthScale * (traverse ? 0.92f : 1f), 0.72f, 1f)
            };
        }

        private static StreamingProfile ClampProfile(StreamingProfile profile)
        {
            profile.mapMagicObjectsPerFrame = Mathf.Clamp(profile.mapMagicObjectsPerFrame, 32, 256);
            profile.scavengeRadiusScale = Mathf.Clamp(profile.scavengeRadiusScale, 0.4f, 1.5f);
            profile.scavengeSpawnScale = Mathf.Clamp(profile.scavengeSpawnScale, 0.4f, 1.5f);
            profile.colliderRadiusScale = Mathf.Clamp(profile.colliderRadiusScale, 0.4f, 1.5f);
            profile.colliderOpsScale = Mathf.Clamp(profile.colliderOpsScale, 0.4f, 1.5f);
            profile.nearSliceScale = Mathf.Clamp(profile.nearSliceScale, 0.6f, 1.4f);
            profile.midSliceScale = Mathf.Clamp(profile.midSliceScale, 0.6f, 1.5f);
            profile.terrainPixelError = Mathf.Clamp(profile.terrainPixelError <= 0 ? 4 : profile.terrainPixelError, 1, 12);
            profile.terrainBaseMapDistance = Mathf.Clamp(profile.terrainBaseMapDistance <= 0 ? 1600 : profile.terrainBaseMapDistance, 512, 4000);
            profile.terrainDetailDistance = Mathf.Clamp(profile.terrainDetailDistance <= 0f ? 96f : profile.terrainDetailDistance, 0f, 160f);
            profile.terrainDetailDensity = Mathf.Clamp(profile.terrainDetailDensity <= 0f ? 1f : profile.terrainDetailDensity, 0.4f, 1.2f);
            return profile;
        }

        private void UpdateDiagnostics()
        {
#if UNITY_EDITOR
            _debugPlayerReady = playerTransform != null && playerRigidbody != null;
            _debugBridgeReady = mapMagicBridge != null && mapMagicBridge.IsAvailable;
            _debugBiomeCacheReady = biomeSamplerCache != null && biomeSamplerCache.IsReady;
            _debugBudgetReady = scatterBudgetController != null;
            _debugUsingSharedChunkProfile = chunkStreamingProfile != null;
#endif
        }
    }
}
