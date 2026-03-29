using Hecton8.Core;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4200)]
    public sealed class ScatterBudgetController : MonoBehaviour, ISlowTickable
    {
        private enum BudgetBand
        {
            Surface,
            MidDepth,
            Deep
        }

        [System.Serializable]
        private struct BudgetProfile
        {
            public float scavengeUnloadDistance;
            public float scavengePriorityRadius;
            public int scavengeSpawnsPerTick;
            public float colliderActivateRadius;
            public float colliderDeactivateRadius;
            public int colliderOpsPerTick;
        }

        [Header("References")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private MapMagicBridge mapMagicBridge;
        [SerializeField] private ScavengePopulator scavengePopulator;
        [SerializeField] private ProximityColliderSystem proximityColliderSystem;
        [SerializeField] private BiomeSamplerCache biomeSamplerCache;

        [Header("Depth Thresholds")]
        [SerializeField] private float midDepthStart = 60f;
        [SerializeField] private float deepDepthStart = 180f;

        [Header("Profiles")]
        [SerializeField] private BudgetProfile surfaceProfile;
        [SerializeField] private BudgetProfile midDepthProfile;
        [SerializeField] private BudgetProfile deepProfile;

        [Header("Diagnostics")]
        [SerializeField] private string _debugCurrentBand = "Surface";
        [SerializeField] private float _debugCurrentDepth;
        [SerializeField] private bool _debugApplied;
        [SerializeField] private bool _debugPlayerReady;
        [SerializeField] private bool _debugBridgeReady;
        [SerializeField] private bool _debugScavengeReady;
        [SerializeField] private bool _debugColliderReady;
        [SerializeField] private float _debugDirectorScavengeRadiusScale = 1f;
        [SerializeField] private float _debugDirectorSpawnScale = 1f;
        [SerializeField] private float _debugDirectorColliderRadiusScale = 1f;
        [SerializeField] private float _debugDirectorColliderOpsScale = 1f;
        [SerializeField] private float _debugInterestScavengeRadiusScale = 1f;
        [SerializeField] private float _debugInterestSpawnScale = 1f;
        [SerializeField] private float _debugInterestColliderRadiusScale = 1f;
        [SerializeField] private float _debugInterestColliderOpsScale = 1f;

        private bool _registeredToTickManager;
        private BudgetBand _lastAppliedBand = (BudgetBand)(-1);
        private float _directorScavengeRadiusScale = 1f;
        private float _directorSpawnScale = 1f;
        private float _directorColliderRadiusScale = 1f;
        private float _directorColliderOpsScale = 1f;
        private float _interestScavengeRadiusScale = 1f;
        private float _interestSpawnScale = 1f;
        private float _interestColliderRadiusScale = 1f;
        private float _interestColliderOpsScale = 1f;

        private void Reset()
        {
            surfaceProfile = new BudgetProfile
            {
                scavengeUnloadDistance = 320f,
                scavengePriorityRadius = 150f,
                scavengeSpawnsPerTick = 24,
                colliderActivateRadius = 42f,
                colliderDeactivateRadius = 48f,
                colliderOpsPerTick = 64
            };

            midDepthProfile = new BudgetProfile
            {
                scavengeUnloadDistance = 260f,
                scavengePriorityRadius = 120f,
                scavengeSpawnsPerTick = 18,
                colliderActivateRadius = 34f,
                colliderDeactivateRadius = 40f,
                colliderOpsPerTick = 48
            };

            deepProfile = new BudgetProfile
            {
                scavengeUnloadDistance = 220f,
                scavengePriorityRadius = 90f,
                scavengeSpawnsPerTick = 12,
                colliderActivateRadius = 26f,
                colliderDeactivateRadius = 32f,
                colliderOpsPerTick = 32
            };
        }

        private void Awake()
        {
            ResolveReferences();
            ClampProfiles();
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

            ApplyCurrentBudget(force: true);
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
            ApplyCurrentBudget(force: false);
        }

        public void SetDirectorScales(
            float scavengeRadiusScale,
            float spawnScale,
            float colliderRadiusScale,
            float colliderOpsScale)
        {
            float clampedScavengeRadius = Mathf.Clamp(scavengeRadiusScale, 0.4f, 1.5f);
            float clampedSpawn = Mathf.Clamp(spawnScale, 0.4f, 1.5f);
            float clampedColliderRadius = Mathf.Clamp(colliderRadiusScale, 0.4f, 1.5f);
            float clampedColliderOps = Mathf.Clamp(colliderOpsScale, 0.4f, 1.5f);

            bool changed =
                !Mathf.Approximately(_directorScavengeRadiusScale, clampedScavengeRadius) ||
                !Mathf.Approximately(_directorSpawnScale, clampedSpawn) ||
                !Mathf.Approximately(_directorColliderRadiusScale, clampedColliderRadius) ||
                !Mathf.Approximately(_directorColliderOpsScale, clampedColliderOps);

            _directorScavengeRadiusScale = clampedScavengeRadius;
            _directorSpawnScale = clampedSpawn;
            _directorColliderRadiusScale = clampedColliderRadius;
            _directorColliderOpsScale = clampedColliderOps;

            if (changed)
                ApplyCurrentBudget(force: true);
        }

        public void SetInterestScales(
            float scavengeRadiusScale,
            float spawnScale,
            float colliderRadiusScale,
            float colliderOpsScale)
        {
            float clampedScavengeRadius = Mathf.Clamp(scavengeRadiusScale, 0.75f, 1.4f);
            float clampedSpawn = Mathf.Clamp(spawnScale, 0.75f, 1.4f);
            float clampedColliderRadius = Mathf.Clamp(colliderRadiusScale, 0.75f, 1.4f);
            float clampedColliderOps = Mathf.Clamp(colliderOpsScale, 0.75f, 1.4f);

            bool changed =
                !Mathf.Approximately(_interestScavengeRadiusScale, clampedScavengeRadius) ||
                !Mathf.Approximately(_interestSpawnScale, clampedSpawn) ||
                !Mathf.Approximately(_interestColliderRadiusScale, clampedColliderRadius) ||
                !Mathf.Approximately(_interestColliderOpsScale, clampedColliderOps);

            _interestScavengeRadiusScale = clampedScavengeRadius;
            _interestSpawnScale = clampedSpawn;
            _interestColliderRadiusScale = clampedColliderRadius;
            _interestColliderOpsScale = clampedColliderOps;

            if (changed)
                ApplyCurrentBudget(force: true);
        }

        private void ApplyCurrentBudget(bool force)
        {
            ResolveReferences();
            ClampProfiles();

            if (playerTransform == null || mapMagicBridge == null)
            {
                _debugApplied = false;
                UpdateDiagnostics();
                return;
            }

            float depth = Mathf.Max(0f, mapMagicBridge.WaterSurfaceLevel - playerTransform.position.y);
            BudgetBand band = GetBandForDepth(depth);

            _debugCurrentDepth = depth;
            _debugCurrentBand = band.ToString();

            if (!force && band == _lastAppliedBand)
            {
                UpdateDiagnostics();
                return;
            }

            BudgetProfile profile = GetProfile(band);

            if (scavengePopulator != null)
            {
                float scavengeRadiusScale = Mathf.Clamp(_directorScavengeRadiusScale * _interestScavengeRadiusScale, 0.35f, 1.75f);
                float spawnScale = Mathf.Clamp(_directorSpawnScale * _interestSpawnScale, 0.35f, 1.75f);
                scavengePopulator.SetRuntimeBudget(
                    profile.scavengeUnloadDistance * scavengeRadiusScale,
                    profile.scavengePriorityRadius * scavengeRadiusScale,
                    Mathf.Max(1, Mathf.RoundToInt(profile.scavengeSpawnsPerTick * spawnScale)));
            }

            if (proximityColliderSystem != null)
            {
                float colliderRadiusScale = Mathf.Clamp(_directorColliderRadiusScale * _interestColliderRadiusScale, 0.35f, 1.75f);
                float colliderOpsScale = Mathf.Clamp(_directorColliderOpsScale * _interestColliderOpsScale, 0.35f, 1.75f);
                proximityColliderSystem.SetRuntimeBudget(
                    profile.colliderActivateRadius * colliderRadiusScale,
                    profile.colliderDeactivateRadius * colliderRadiusScale,
                    Mathf.Max(4, Mathf.RoundToInt(profile.colliderOpsPerTick * colliderOpsScale)));
            }

            _lastAppliedBand = band;
            _debugApplied = true;
            UpdateDiagnostics();
        }

        private BudgetBand GetBandForDepth(float depth)
        {
            if (depth >= deepDepthStart)
                return BudgetBand.Deep;

            if (depth >= midDepthStart)
                return BudgetBand.MidDepth;

            return BudgetBand.Surface;
        }

        private BudgetProfile GetProfile(BudgetBand band)
        {
            if (band == BudgetBand.Deep)
                return deepProfile;

            if (band == BudgetBand.MidDepth)
                return midDepthProfile;

            return surfaceProfile;
        }

        private void ResolveReferences()
        {
            if (playerTransform == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null)
                    playerTransform = player.transform;
            }

            if (mapMagicBridge == null)
                mapMagicBridge = MapMagicBridge.Instance ?? FindAnyObjectByType<MapMagicBridge>();

            if (scavengePopulator == null)
                scavengePopulator = ScavengePopulator.Instance ?? FindAnyObjectByType<ScavengePopulator>();

            if (proximityColliderSystem == null)
                proximityColliderSystem = FindAnyObjectByType<ProximityColliderSystem>();

            if (biomeSamplerCache == null)
                biomeSamplerCache = FindAnyObjectByType<BiomeSamplerCache>();
        }

        private void ClampProfiles()
        {
            midDepthStart = Mathf.Max(10f, midDepthStart);
            deepDepthStart = Mathf.Max(midDepthStart + 20f, deepDepthStart);

            surfaceProfile = ClampProfile(surfaceProfile);
            midDepthProfile = ClampProfile(midDepthProfile);
            deepProfile = ClampProfile(deepProfile);
        }

        private static BudgetProfile ClampProfile(BudgetProfile profile)
        {
            profile.scavengeUnloadDistance = Mathf.Max(50f, profile.scavengeUnloadDistance);
            profile.scavengePriorityRadius = Mathf.Max(10f, profile.scavengePriorityRadius);
            profile.scavengeSpawnsPerTick = Mathf.Max(1, profile.scavengeSpawnsPerTick);
            profile.colliderActivateRadius = Mathf.Max(4f, profile.colliderActivateRadius);
            profile.colliderDeactivateRadius = Mathf.Max(profile.colliderActivateRadius + 2f, profile.colliderDeactivateRadius);
            profile.colliderOpsPerTick = Mathf.Max(4, profile.colliderOpsPerTick);
            return profile;
        }

        private void UpdateDiagnostics()
        {
            _debugPlayerReady = playerTransform != null;
            _debugBridgeReady = mapMagicBridge != null && mapMagicBridge.IsAvailable;
            _debugScavengeReady = scavengePopulator != null;
            _debugColliderReady = proximityColliderSystem != null;
            _debugDirectorScavengeRadiusScale = _directorScavengeRadiusScale;
            _debugDirectorSpawnScale = _directorSpawnScale;
            _debugDirectorColliderRadiusScale = _directorColliderRadiusScale;
            _debugDirectorColliderOpsScale = _directorColliderOpsScale;
            _debugInterestScavengeRadiusScale = _interestScavengeRadiusScale;
            _debugInterestSpawnScale = _interestSpawnScale;
            _debugInterestColliderRadiusScale = _interestColliderRadiusScale;
            _debugInterestColliderOpsScale = _interestColliderOpsScale;
        }
    }
}
