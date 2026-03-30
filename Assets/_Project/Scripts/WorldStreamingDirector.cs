using Hecton8.Core;
using MapMagic.Core;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4150)]
    public sealed class WorldStreamingDirector : MonoBehaviour, ISlowTickable
    {
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

        private bool _registeredToTickManager;
        private float _smoothedSpeed;
        private DepthZone _lastDepthZone = (DepthZone)(-1);
        private MotionMode _lastMotionMode = (MotionMode)(-1);
        private int _lastObjectsPerFrame = -1;

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

        private void ApplyStreamingProfile(bool force)
        {
            ResolveReferences();
            ClampSettings();

            if (playerTransform == null || mapMagicBridge == null || !mapMagicBridge.IsAvailable || scatterBudgetController == null)
            {
                _debugApplied = false;
                UpdateDiagnostics();
                return;
            }

            float depth = Mathf.Max(0f, mapMagicBridge.WaterSurfaceLevel - playerTransform.position.y);
            float targetSpeed = GetCurrentSpeed();
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, targetSpeed, speedSmoothing);

            DepthZone depthZone = GetDepthZone(depth);
            MotionMode motionMode = _smoothedSpeed >= traverseSpeedStart ? MotionMode.Traverse : MotionMode.Survey;
            StreamingProfile profile = GetProfile(depthZone, motionMode);

            _debugDepth = depth;
            _debugSpeed = _smoothedSpeed;
            _debugDepthZone = depthZone.ToString();
            _debugMotionMode = motionMode.ToString();

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

            MapMagicObject mapMagicObject = mapMagicBridge.RuntimeMapMagicObject;
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

        private void ResolveReferences()
        {
            if (playerTransform == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null)
                    playerTransform = player.transform;
            }

            if (playerRigidbody == null && playerTransform != null)
                playerRigidbody = playerTransform.GetComponent<Rigidbody>();

            if (mapMagicBridge == null)
                mapMagicBridge = MapMagicBridge.Instance ?? FindAnyObjectByType<MapMagicBridge>();

            if (biomeSamplerCache == null)
                biomeSamplerCache = FindAnyObjectByType<BiomeSamplerCache>();

            if (scatterBudgetController == null)
                scatterBudgetController = FindAnyObjectByType<ScatterBudgetController>();

            if (worldSliceDirector == null)
                worldSliceDirector = FindAnyObjectByType<WorldSliceDirector>();
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
        }
    }
}
