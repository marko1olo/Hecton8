using Hecton8.Core;
using Hecton8.Gameplay;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4150)]
    public sealed class WorldStreamingDirector : MonoBehaviour, ISlowTickable
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
        private HectonPlayerMovement _playerMovement;

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
            TryRegister();
        }

        private void Start()
        {
            TryRegister();

            ApplyStreamingProfile(force: true);
        }

        private void OnDisable()
        {
            TryUnregister();
        }

        private void TryRegister()
        {
            if (_registeredToTickManager || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _registeredToTickManager = true;
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
            ApplyStreamingProfile(force: false);
        }

        public void SetChunkStreamingProfile(WorldChunkStreamingProfile profile)
        {
            chunkStreamingProfile = profile;
            RefreshRuntimeProfilesFromChunkProfile();
            ClampSettings();
            ApplyStreamingProfile(force: true);
        }

        private void ApplyStreamingProfile(bool force)
        {
            ResolveReferences();
            RefreshRuntimeProfilesFromChunkProfile();
            ClampSettings();

            StreamingProfile activeProfile = surfaceSurveyProfile;

            if (mapMagicBridge != null && mapMagicBridge.IsAvailable)
            {
                mapMagicBridge.ConfigureRuntimeTerrainStreaming(
                    terrainDraftsInPlaymode,
                    terrainMainRange,
                    terrainDraftRange,
                    terrainDraftResolution);
            }

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
                _debugApplied = false;
                UpdateDiagnostics();
                return;
            }

            float targetSpeedSq = GetCurrentSpeedSq();
            _smoothedSpeedSq = Mathf.Lerp(_smoothedSpeedSq, targetSpeedSq, speedSmoothing);

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

            _debugDepth = depth;
            _debugSpeed = Mathf.Sqrt(Mathf.Max(0f, _smoothedSpeedSq));
            _debugDepthZone = GetDepthZoneLabel(depthZone);
            _debugMotionMode = GetMotionModeLabel(motionMode);

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
                UpdateDiagnostics();
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
            _debugNearSliceScale = profile.nearSliceScale;
            _debugMidSliceScale = profile.midSliceScale;
            _debugApplied = true;
            UpdateDiagnostics();
        }

        private void ApplyMapMagicTerrainProfile(StreamingProfile profile)
        {
            if (mapMagicBridge == null || !mapMagicBridge.IsAvailable)
            {
                _debugMapMagicObjectsPerFrame = -1;
                return;
            }

            mapMagicBridge.SetRuntimeObjectsPerFrame(profile.mapMagicObjectsPerFrame);
            mapMagicBridge.ApplyRuntimeTerrainQuality(
                profile.terrainPixelError,
                profile.terrainBaseMapDistance,
                profile.terrainDetailDistance,
                profile.terrainDetailDensity,
                terrainHeightmapMaximumLod);

            _debugMapMagicObjectsPerFrame = profile.mapMagicObjectsPerFrame;
            _debugTerrainDraftsInPlaymode = terrainDraftsInPlaymode;
            _debugTerrainDraftResolution = GetTerrainResolutionLabel(terrainDraftResolution);
            _debugTerrainMainRange = terrainMainRange;
            _debugTerrainDraftRange = terrainDraftRange;
            _debugTerrainPixelError = profile.terrainPixelError;
            _debugTerrainBaseMapDistance = profile.terrainBaseMapDistance;
            _debugTerrainDetailDistance = profile.terrainDetailDistance;
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

        private float GetCurrentSpeedSq()
        {
            if (playerRigidbody != null)
                return playerRigidbody.linearVelocity.sqrMagnitude;

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
            if (playerTransform == null)
                WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);

            if (_playerMovement == null && playerTransform != null)
                playerTransform.TryGetComponent(out _playerMovement);

            if (playerRigidbody == null && playerTransform != null)
                playerRigidbody = playerTransform.GetComponent<Rigidbody>();

            if (mapMagicBridge == null)
                WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicBridge);

            if (biomeSamplerCache == null)
                WorldRuntimeReferenceUtility.TryResolveBiomeSamplerCache(ref biomeSamplerCache);

            if (scatterBudgetController == null)
                WorldRuntimeReferenceUtility.TryResolveScatterBudgetController(ref scatterBudgetController);

            if (worldSliceDirector == null)
                WorldRuntimeReferenceUtility.TryResolveWorldSliceDirector(ref worldSliceDirector);
        }

        private bool TryResolveCurrentDepth(out float depth)
        {
            depth = 0f;

            if (_playerMovement != null)
            {
                depth = Mathf.Max(0f, _playerMovement.CurrentDepth);
                return true;
            }

            if (playerTransform == null || mapMagicBridge == null || !mapMagicBridge.IsAvailable)
                return false;

            depth = Mathf.Max(0f, mapMagicBridge.WaterSurfaceLevel - playerTransform.position.y);
            return true;
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
            _debugUsingSharedChunkProfile = chunkStreamingProfile != null;
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
            _debugPlayerReady = playerTransform != null && playerRigidbody != null;
            _debugBridgeReady = mapMagicBridge != null && mapMagicBridge.IsAvailable;
            _debugBiomeCacheReady = biomeSamplerCache != null && biomeSamplerCache.IsReady;
            _debugBudgetReady = scatterBudgetController != null;
            _debugUsingSharedChunkProfile = chunkStreamingProfile != null;
        }
    }
}
