using Hecton8.Core;
using Hecton8.Gameplay;
using MapMagic.Core;
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
        [SerializeField] private bool _debugApplied;
        [SerializeField] private bool _debugPlayerReady;
        [SerializeField] private bool _debugBridgeReady;
        [SerializeField] private bool _debugBiomeCacheReady;
        [SerializeField] private bool _debugBudgetReady;
        [SerializeField] private bool _debugUsingSharedChunkProfile;
#pragma warning restore CS0414

        private bool _registeredToTickManager;
        private float _smoothedSpeed;
        private DepthZone _lastDepthZone = (DepthZone)(-1);
        private MotionMode _lastMotionMode = (MotionMode)(-1);
        private int _lastObjectsPerFrame = -1;
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
                midSliceScale = 1f
            };

            surfaceTraverseProfile = new StreamingProfile
            {
                mapMagicObjectsPerFrame = 144,
                scavengeRadiusScale = 0.92f,
                scavengeSpawnScale = 0.82f,
                colliderRadiusScale = 0.9f,
                colliderOpsScale = 0.82f,
                nearSliceScale = 0.86f,
                midSliceScale = 1.16f
            };

            midSurveyProfile = new StreamingProfile
            {
                mapMagicObjectsPerFrame = 88,
                scavengeRadiusScale = 0.9f,
                scavengeSpawnScale = 0.88f,
                colliderRadiusScale = 0.86f,
                colliderOpsScale = 0.84f,
                nearSliceScale = 1f,
                midSliceScale = 0.96f
            };

            midTraverseProfile = new StreamingProfile
            {
                mapMagicObjectsPerFrame = 128,
                scavengeRadiusScale = 0.82f,
                scavengeSpawnScale = 0.72f,
                colliderRadiusScale = 0.78f,
                colliderOpsScale = 0.72f,
                nearSliceScale = 0.82f,
                midSliceScale = 1.12f
            };

            deepSurveyProfile = new StreamingProfile
            {
                mapMagicObjectsPerFrame = 72,
                scavengeRadiusScale = 0.78f,
                scavengeSpawnScale = 0.76f,
                colliderRadiusScale = 0.72f,
                colliderOpsScale = 0.72f,
                nearSliceScale = 0.94f,
                midSliceScale = 0.92f
            };

            deepTraverseProfile = new StreamingProfile
            {
                mapMagicObjectsPerFrame = 112,
                scavengeRadiusScale = 0.68f,
                scavengeSpawnScale = 0.58f,
                colliderRadiusScale = 0.62f,
                colliderOpsScale = 0.58f,
                nearSliceScale = 0.74f,
                midSliceScale = 1.06f
            };
        }

        private void Awake()
        {
            ResolveReferences();
            RefreshRuntimeProfilesFromChunkProfile();
            ClampSettings();
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

            ApplyStreamingProfile(force: true);
        }

        private void OnDisable()
        {
            if (_registeredToTickManager && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Unregister((ISlowTickable)this);
                _registeredToTickManager = false;
            }
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

            if (playerTransform == null || scatterBudgetController == null || !TryResolveCurrentDepth(out float depth))
            {
                _debugApplied = false;
                UpdateDiagnostics();
                return;
            }

            float targetSpeed = GetCurrentSpeed();
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, targetSpeed, speedSmoothing);

            DepthZone depthZone = GetDepthZone(depth);
            MotionMode motionMode = _smoothedSpeed >= traverseSpeedStart ? MotionMode.Traverse : MotionMode.Survey;
            StreamingProfile profile = GetProfile(depthZone, motionMode);

            _debugDepth = depth;
            _debugSpeed = _smoothedSpeed;
            _debugDepthZone = GetDepthZoneLabel(depthZone);
            _debugMotionMode = GetMotionModeLabel(motionMode);

            bool changed =
                force ||
                depthZone != _lastDepthZone ||
                motionMode != _lastMotionMode ||
                profile.mapMagicObjectsPerFrame != _lastObjectsPerFrame;

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

            MapMagicObject mapMagicObject = mapMagicBridge != null && mapMagicBridge.IsAvailable
                ? mapMagicBridge.RuntimeMapMagicObject
                : null;
            if (mapMagicObject != null && mapMagicObject.globals != null)
            {
                mapMagicObject.globals.objectsNumPerFrame = profile.mapMagicObjectsPerFrame;
                _debugMapMagicObjectsPerFrame = mapMagicObject.globals.objectsNumPerFrame;
            }
            else
            {
                _debugMapMagicObjectsPerFrame = -1;
            }

            _lastDepthZone = depthZone;
            _lastMotionMode = motionMode;
            _lastObjectsPerFrame = profile.mapMagicObjectsPerFrame;
            _debugNearSliceScale = profile.nearSliceScale;
            _debugMidSliceScale = profile.midSliceScale;
            _debugApplied = true;
            UpdateDiagnostics();
        }

        private float GetCurrentSpeed()
        {
            if (playerRigidbody != null)
                return playerRigidbody.linearVelocity.magnitude;

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
                midSliceScale = Mathf.Clamp(terrainLayer.midRadiusScale * (traverse ? 1.08f : 1f), 0.6f, 1.7f)
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
