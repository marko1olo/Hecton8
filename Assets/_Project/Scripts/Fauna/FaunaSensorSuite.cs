using Hecton8.Core;
using UnityEngine;
using Unity.Jobs;
using UnityEngine.Serialization;
using Hecton8.Gameplay;
using Hecton8.World;

namespace Hecton8.AI
{
    /// <summary>
    /// Optimized sensory system for HECTON-8 Fauna.
    /// [RULE] ZERO GC IN HOT PATHS.
    /// </summary>
    [System.Serializable]
    public class FaunaSensorSuite
    {
        private const float ToolNoiseRadiusMultiplier = 1.35f;
        private const float PlayerAwarenessMemorySeconds = 0.75f;
        private const float PlayerNoiseFreshSeconds = 0.5f;

        [Header("── Avoidance ──────────────────────────────────")]
        public float avoidanceRange = 8f;
        public float lookAheadFactor = 0.5f;
        public float maxRayLength = 15f;
        public float spreadAngle = 35f;
        public float avoidanceSphereRadius = 0.8f;
        public LayerMask obstacleMask;

        [Header("── Detection ──────────────────────────────────")]
        public float aggroDistance = 25f;
        [FormerlySerializedAs("deaggroDistance")]
        public float deaggroDistance = 35f; 
        public float sleepDistance = 100f;
        [FormerlySerializedAs("reactToPlayerNoise")]
        public bool reactToPlayerNoise = true; 
        [FormerlySerializedAs("reactToPlayerLight")]
        public bool reactToPlayerLight = true; 
        public float distractorDetectRadius = 30f;
        public LayerMask distractorMask;
        public LayerMask territoryMask;
        
        [Header("── Internal State ─────────────────────────────")]
        public bool canSeePlayer;
        public bool hasVisualPlayerContact;
        public bool hasNoisePlayerContact;
        public float distSqrToPlayer;
        public bool isThreatened;
        public Transform currentThreat;
        public bool isAvoidingObstacle;
        public Vector3 bestFreeDirection;
        public bool lodDisabled;
        public bool isSleeping;
        public Transform currentDistractor;
        public Transform currentScavengeTarget;

        [Header("── Flocking ──────────────────────────────────")]
        public LayerMask flockMask;
        public float flockRadius = 10f;
        [HideInInspector] public Vector3 flockCenter, flockDirection, flockAvoidance;
        [HideInInspector] public int flockCount;
        [HideInInspector] public bool isScattering;
        [HideInInspector] public Vector3 scatterDirection;

        [Header("── POI ───────────────────────────────────────")]
        public LayerMask poiMask;
        [HideInInspector] public Vector3 currentEscapePOI;
        [HideInInspector] public bool hasEscapePOI;

        [Header("── Ecology ──────────────────────────────────────")]
        public LayerMask preyMask;
        [HideInInspector] public Transform currentPrey;

        private Transform _selfTransform;
        private Transform _playerTransform;
        private PlayerToolManager _playerToolManager;
        private FaunaSpeciesProfile _profile;
        private float _avoidanceTimeAccumulator;
        private NoiseSystem.PlayerNoiseSignal _lastReportedPlayerNoise;
        private bool _hasReportedPlayerNoise;
        private bool _hasLastKnownPlayerPosition;
        private Vector3 _lastKnownPlayerPosition;
        private float _lastReportedPlayerTimeSeconds;
        private float _lastKnownPlayerTimeSeconds;
        private float _authoredTimeSeconds;
        private float _queuedObstacleRayLength;
        private Vector3 _queuedObstacleRayDirection;
        private RaycastHit _deferredObstacleHit;
        private bool _hasDeferredObstacleHit;
        private FoveatedTickRate _foveatedTickRate = FoveatedTickRate.Center60Hz;
        private float _foveatedTickIntervalSeconds = 1.0f / 60.0f;
        private float _foveatedImportanceScore = 1.0f;
        private bool _foveatedInsideFrustum = true;
        
        /// <summary>
        /// True if the creature has been failing to move forward due to obstacles.
        /// </summary>
        public bool IsStuck => _avoidanceTimeAccumulator > 2f;

        // Buffers for Zero-GC
        // COLD ALLOC: Buffers for non-allocating physics queries
        private static readonly Vector3[] _rayDirs = new Vector3[7];
        // COLD ALLOC: SpatialQueryHit[8] - fauna distractor lookup buffer over spatial grid - owner: FaunaSensorSuite
        private static readonly SpatialQueryHit[] _distractorSpatialBuffer = new SpatialQueryHit[8];

        public void Init(Transform self, FaunaSpeciesProfile profile)
        {
            _selfTransform = self;
            _profile = profile;
            _lastReportedPlayerNoise = default;
            _hasReportedPlayerNoise = false;
            _hasLastKnownPlayerPosition = false;
            _lastKnownPlayerPosition = default;
            _lastReportedPlayerTimeSeconds = float.NegativeInfinity;
            _lastKnownPlayerTimeSeconds = float.NegativeInfinity;
            _authoredTimeSeconds = 0f;
            _queuedObstacleRayLength = avoidanceRange;
            _queuedObstacleRayDirection = self != null ? self.forward : Vector3.forward;
            _deferredObstacleHit = default;
            _hasDeferredObstacleHit = false;
            _foveatedTickRate = FoveatedTickRate.Center60Hz;
            _foveatedTickIntervalSeconds = 1.0f / 60.0f;
            _foveatedImportanceScore = 1.0f;
            _foveatedInsideFrustum = true;
            if (WorldStateManager.Instance != null)
                _playerTransform = WorldStateManager.Instance.PlayerTransform;

            if (_playerTransform != null)
            {
                _playerToolManager = _playerTransform.GetComponentInChildren<PlayerToolManager>(true);
                PlayerNoiseEmitter.EnsureAttached(_playerTransform);
            }
        }

        public void Tick(float dt, Vector3 velocity, float currentTimeSeconds)
        {
            _authoredTimeSeconds = currentTimeSeconds;

            if (_playerTransform != null)
            {
                distSqrToPlayer = (_playerTransform.position - _selfTransform.position).sqrMagnitude;
                lodDisabled = distSqrToPlayer > 150f * 150f;
                isSleeping = distSqrToPlayer > sleepDistance * sleepDistance;
            }

            if (lodDisabled || isSleeping)
            {
                isAvoidingObstacle = false;
                _avoidanceTimeAccumulator = 0f;
                _hasDeferredObstacleHit = false;
                return;
            }

            UpdateMajorSenses();
            UpdateObstacleAvoidance(dt, velocity);
            UpdateDistractorDetection();
            UpdatePreyDetection();
            UpdateThreatDetection();
            UpdateScavengeTarget();
            if (IsStuck) UpdatePOISearch();
        }

        private void UpdateMajorSenses()
        {
            bool visualContact = distSqrToPlayer < aggroDistance * aggroDistance;
            bool reportedContact = HasFreshReportedPlayerNoise();
            hasVisualPlayerContact = visualContact;
            hasNoisePlayerContact = reportedContact;
            canSeePlayer = visualContact || reportedContact;

            if (visualContact && _playerTransform != null)
                RememberPlayerPosition(_playerTransform.position);
        }

        public void ReceivePlayerNoiseSignal(NoiseSystem.PlayerNoiseSignal playerNoise)
        {
            _lastReportedPlayerNoise = playerNoise;
            _hasReportedPlayerNoise = true;
            _lastReportedPlayerTimeSeconds = _authoredTimeSeconds;
            hasNoisePlayerContact = true;
            RememberPlayerPosition(playerNoise.Position);
            distSqrToPlayer = (_lastKnownPlayerPosition - _selfTransform.position).sqrMagnitude;
        }

        private bool HasFreshReportedPlayerNoise()
        {
            if (_playerTransform == null || !_hasReportedPlayerNoise)
                return false;

            if (_authoredTimeSeconds - _lastReportedPlayerTimeSeconds > PlayerNoiseFreshSeconds)
                return false;

            if (reactToPlayerLight && _lastReportedPlayerNoise.FlashlightOn)
                return true;

            if (!reactToPlayerNoise)
                return false;

            if (_lastReportedPlayerNoise.TransportBoost01 > 0f)
                return true;

            if (_lastReportedPlayerNoise.ToolUseNoise01 > 0f)
                return true;

            return _lastReportedPlayerNoise.MovementSpeedSqr >= 1.0f;
        }

        public bool TryGetDirectPlayerTransform(out Transform playerTransform)
        {
            if (hasVisualPlayerContact && _playerTransform != null)
            {
                playerTransform = _playerTransform;
                return true;
            }

            playerTransform = null;
            return false;
        }

        public bool TryGetPerceivedPlayerPosition(out Vector3 playerPosition)
        {
            if (hasVisualPlayerContact && _playerTransform != null)
            {
                playerPosition = _playerTransform.position;
                return true;
            }

            if (_hasLastKnownPlayerPosition &&
                _authoredTimeSeconds - _lastKnownPlayerTimeSeconds <= PlayerAwarenessMemorySeconds)
            {
                playerPosition = _lastKnownPlayerPosition;
                return true;
            }

            playerPosition = default;
            return false;
        }

        private void RememberPlayerPosition(Vector3 playerPosition)
        {
            _hasLastKnownPlayerPosition = true;
            _lastKnownPlayerPosition = playerPosition;
            _lastKnownPlayerTimeSeconds = _authoredTimeSeconds;
        }

        private void UpdateThreatDetection()
        {
            isThreatened = false;
            currentThreat = null;
            
            if (territoryMask == 0 || _profile == null)
                return;

            if (WorldSpatialHashGrid.TryGetNearestBioform(
                    _selfTransform.position,
                    _profile.territoryThreatRadius,
                    _profile.predatorMask,
                    _selfTransform,
                    _profile.speciesID,
                    false,
                    out SpatialQueryHit threatHit))
            {
                currentThreat = threatHit.Transform;
                isThreatened = currentThreat != null;
            }
        }

        private void UpdateObstacleAvoidance(float dt, Vector3 velocity)
        {
            float length = Mathf.Clamp(avoidanceRange + velocity.magnitude * lookAheadFactor, avoidanceRange, maxRayLength);
            Vector3 forwardDirection = velocity.sqrMagnitude > 0.0001f ? velocity.normalized : _selfTransform.forward;
            _rayDirs[0] = forwardDirection;
            _queuedObstacleRayDirection = forwardDirection;
            _queuedObstacleRayLength = length;

            isAvoidingObstacle = _hasDeferredObstacleHit &&
                                 _deferredObstacleHit.collider != null &&
                                 _deferredObstacleHit.distance > 0f &&
                                 _deferredObstacleHit.distance <= length;
            if (isAvoidingObstacle)
            {
                _avoidanceTimeAccumulator += Mathf.Max(dt, _foveatedTickIntervalSeconds);
                bestFreeDirection = Vector3.Reflect(forwardDirection, _deferredObstacleHit.normal).normalized;
            }
            else
            {
                _avoidanceTimeAccumulator = 0f;
                bestFreeDirection = forwardDirection;
            }

            _hasDeferredObstacleHit = false;
        }

        private void UpdateDistractorDetection()
        {
            Transform bleedingTarget = ResolveNearestBleedingDistractor();
            if (bleedingTarget != null)
            {
                currentDistractor = bleedingTarget;
                return;
            }

            currentDistractor = ResolveNearestDistractorByTag("Flare");
        }

        private void UpdateScavengeTarget()
        {
            currentScavengeTarget = null;
            if (_profile == null || !_profile.isScavenger) return;

            // 1. Check for player tool (priority)
            if (_playerToolManager != null && _playerToolManager.CurrentTool != null)
            {
                Transform toolTransform = _playerToolManager.CurrentTool.transform;
                if ((toolTransform.position - _selfTransform.position).sqrMagnitude < distractorDetectRadius * distractorDetectRadius)
                {
                    currentScavengeTarget = toolTransform;
                    return;
                }
            }

            currentScavengeTarget = ResolveNearestDistractorByTag("DroppedFood");
        }

        private void UpdatePreyDetection()
        {
            currentPrey = null;
            if (_profile == null)
                return;

            LayerMask searchMask = _profile.preyMask != 0 ? _profile.preyMask : preyMask;
            if (searchMask == 0)
                return;

            if (WorldSpatialHashGrid.TryGetNearestBioform(
                    _selfTransform.position,
                    aggroDistance,
                    searchMask,
                    _selfTransform,
                    -1,
                    true,
                    out SpatialQueryHit preyHit))
            {
                currentPrey = preyHit.Transform;
            }
        }

        private void UpdatePOISearch()
        {
            // Logic for finding EscapePoints via poiMask...
        }

        private Transform ResolveNearestDistractorByTag(string requiredTag)
        {
            int layerMaskValue = distractorMask.value;
            if (layerMaskValue == 0)
                return null;

            int count = WorldSpatialHashGrid.CollectContactsNonAlloc(
                _selfTransform.position,
                distractorDetectRadius,
                SpatialTargetKind.Pickup | SpatialTargetKind.Signal,
                _distractorSpatialBuffer);
            Transform nearestTransform = null;
            float bestDistanceSqr = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                SpatialQueryHit hit = _distractorSpatialBuffer[i];
                Transform hitTransform = hit.Transform;
                if (hitTransform == null)
                    continue;

                if ((layerMaskValue & (1 << hit.Layer)) == 0)
                    continue;

                if (!hitTransform.CompareTag(requiredTag))
                    continue;

                if (hit.DistanceSqr >= bestDistanceSqr)
                    continue;

                bestDistanceSqr = hit.DistanceSqr;
                nearestTransform = hitTransform;
            }

            return nearestTransform;
        }

        private Transform ResolveNearestBleedingDistractor()
        {
            if (_profile == null)
                return null;

            if (_profile.baseAggro < 0.45f)
                return null;

            int count = WorldSpatialHashGrid.CollectContactsNonAlloc(
                _selfTransform.position,
                distractorDetectRadius,
                SpatialTargetKind.Signal,
                _distractorSpatialBuffer);
            Transform nearestTransform = null;
            float bestWeightedDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                SpatialQueryHit hit = _distractorSpatialBuffer[i];
                HectonSurvivalSystem survival = hit.Owner as HectonSurvivalSystem;
                if (survival == null || !survival.IsBleeding)
                    continue;

                float bleedSeverity = Mathf.Max(0.1f, survival.BleedingSeverity01);
                float weightedDistance = hit.DistanceSqr / bleedSeverity;
                if (weightedDistance >= bestWeightedDistance)
                    continue;

                bestWeightedDistance = weightedDistance;
                nearestTransform = hit.Transform;
            }

            return nearestTransform;
        }

        internal void SetFoveatedCadence(FoveatedTickRate tickRate, float tickIntervalSeconds, float importanceScore, bool insideFrustum)
        {
            _foveatedTickRate = tickRate;
            _foveatedTickIntervalSeconds = tickIntervalSeconds > 0f ? tickIntervalSeconds : (1.0f / 60.0f);
            _foveatedImportanceScore = importanceScore;
            _foveatedInsideFrustum = insideFrustum;
        }

        internal bool TryBuildDeferredRaycastCommand(out RaycastCommand command)
        {
            command = default;
            if (_selfTransform == null || lodDisabled || isSleeping)
                return false;

            int obstacleLayerMask = obstacleMask.value;
            if (obstacleLayerMask == 0)
                return false;

            if (_foveatedTickRate == FoveatedTickRate.Rear5Hz &&
                !_foveatedInsideFrustum &&
                _foveatedImportanceScore < 0.2f)
            {
                return false;
            }

            Vector3 direction = _queuedObstacleRayDirection.sqrMagnitude > 0.0001f
                ? _queuedObstacleRayDirection.normalized
                : _selfTransform.forward;
            float distance = Mathf.Clamp(
                _queuedObstacleRayLength > 0f ? _queuedObstacleRayLength : avoidanceRange,
                avoidanceRange,
                maxRayLength);

            command = new RaycastCommand(
                _selfTransform.position,
                direction,
                new QueryParameters(obstacleLayerMask, false, QueryTriggerInteraction.Ignore),
                distance);
            return true;
        }

        internal void ConsumeDeferredRaycastHit(in RaycastHit hit)
        {
            _deferredObstacleHit = hit;
            _hasDeferredObstacleHit = hit.collider != null;
        }

        public Transform GetPlayerTransform() => _playerTransform;
    }
}
