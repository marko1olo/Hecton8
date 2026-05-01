using Hecton8.Core;
using UnityEngine;
using Unity.Jobs;
using UnityEngine.Serialization;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;

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
        private const byte CaveSignedDistanceSolidThreshold = 128;
        private const float CaveVoxelDdaEpsilon = 0.000001f;
        private const int DeferredObstacleRayCount = 3;
        private const int DeferredRaycastCommandCount = 4;
        private const int ForwardObstacleRayIndex = 0;
        private const int LeftObstacleRayIndex = 1;
        private const int RightObstacleRayIndex = 2;
        private const int PlayerLightOcclusionRayIndex = 3;
        private const float SideObstacleRayYawDegrees = 45f;

        [Header("── Avoidance ──────────────────────────────────")]
        public float avoidanceRange = 8f;
        public float lookAheadFactor = 0.5f;
        public float maxRayLength = 15f;
        public float spreadAngle = 35f;
        public float avoidanceSphereRadius = 0.8f;
        public LayerMask obstacleMask = HectonLayerMasks.DefaultRaycastLayerMask;
        public float visionConeAngle = 135f;

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

        private FaunaBrain _ownerBrain;
        private Transform _selfTransform;
        private Transform _playerTransform;
        private Rigidbody _playerRigidbody;
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
        private Vector3 _queuedForwardObstacleRayDirection;
        private Vector3 _queuedLeftObstacleRayDirection;
        private Vector3 _queuedRightObstacleRayDirection;
        private RaycastHit _deferredForwardObstacleHit;
        private RaycastHit _deferredLeftObstacleHit;
        private RaycastHit _deferredRightObstacleHit;
        private RaycastHit _deferredPlayerLightOcclusionHit;
        private bool _hasDeferredForwardObstacleHit;
        private bool _hasDeferredLeftObstacleHit;
        private bool _hasDeferredRightObstacleHit;
        private bool _hasDeferredPlayerLightOcclusionHit;
        private bool _hasQueuedPlayerLightOcclusionRay;
        private float _queuedPlayerLightOcclusionDistance;
        private FoveatedTickRate _foveatedTickRate = FoveatedTickRate.Center60Hz;
        private float _foveatedTickIntervalSeconds = 1.0f / 60.0f;
        private float _foveatedImportanceScore = 1.0f;
        private bool _foveatedInsideFrustum = true;
        private Vector3 _cachedSelfPosition;
        private Vector3 _cachedSelfForward;
        
        /// <summary>
        /// True if the creature has been failing to move forward due to obstacles.
        /// </summary>
        public bool IsStuck => _avoidanceTimeAccumulator > 2f;

        // Buffers for Zero-GC
        // COLD ALLOC: Buffers for non-allocating physics queries
        private static readonly Vector3[] _rayDirs = new Vector3[7];
        // COLD ALLOC: SpatialQueryHit[8] - fauna distractor lookup buffer over spatial grid - owner: FaunaSensorSuite
        private static readonly SpatialQueryHit[] _distractorSpatialBuffer = new SpatialQueryHit[8];
        // COLD ALLOC: SpatialQueryHit[16] - fauna prey lookup buffer over spatial grid - owner: FaunaSensorSuite
        private static readonly SpatialQueryHit[] _preySpatialBuffer = new SpatialQueryHit[16];

        public void Init(FaunaBrain ownerBrain, Transform self, FaunaSpeciesProfile profile)
        {
            _ownerBrain = ownerBrain;
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
            Vector3 initialForward = self != null ? self.forward : Vector3.forward;
            _queuedForwardObstacleRayDirection = initialForward;
            _queuedLeftObstacleRayDirection = initialForward;
            _queuedRightObstacleRayDirection = initialForward;
            _deferredForwardObstacleHit = default;
            _deferredLeftObstacleHit = default;
            _deferredRightObstacleHit = default;
            _deferredPlayerLightOcclusionHit = default;
            _hasDeferredForwardObstacleHit = false;
            _hasDeferredLeftObstacleHit = false;
            _hasDeferredRightObstacleHit = false;
            _hasDeferredPlayerLightOcclusionHit = false;
            _hasQueuedPlayerLightOcclusionRay = false;
            _queuedPlayerLightOcclusionDistance = 0f;
            _foveatedTickRate = FoveatedTickRate.Center60Hz;
            _foveatedTickIntervalSeconds = 1.0f / 60.0f;
            _foveatedImportanceScore = 1.0f;
            _foveatedInsideFrustum = true;
            _cachedSelfPosition = self != null ? self.position : Vector3.zero;
            _cachedSelfForward = self != null ? self.forward : Vector3.forward;
            if (WorldStateManager.Instance != null)
                _playerTransform = WorldStateManager.Instance.PlayerTransform;

            if (_playerTransform != null)
            {
                _playerTransform.TryGetComponent(out _playerRigidbody);
                _playerToolManager = Hecton8.Core.GlobalRegistry.Player != null && Hecton8.Core.GlobalRegistry.Player.ToolManager != null
                    ? Hecton8.Core.GlobalRegistry.Player.ToolManager
                    : Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<PlayerToolManager>(_playerTransform);
                PlayerNoiseEmitter.EnsureAttached(_playerTransform);
            }
        }

        public void Tick(float dt, Vector3 velocity, float currentTimeSeconds, bool forceLongRangeCognition)
        {
            _authoredTimeSeconds = currentTimeSeconds;
            if (_selfTransform != null)
            {
                _cachedSelfPosition = _selfTransform.position;
                _cachedSelfForward = _selfTransform.forward;
            }

            if (_playerTransform != null)
            {
                distSqrToPlayer = (_playerTransform.position - _cachedSelfPosition).sqrMagnitude;
                lodDisabled = !forceLongRangeCognition && distSqrToPlayer > 150f * 150f;
                isSleeping = !forceLongRangeCognition && distSqrToPlayer > sleepDistance * sleepDistance;
            }

            if (lodDisabled || isSleeping)
            {
                isAvoidingObstacle = false;
                _avoidanceTimeAccumulator = 0f;
                ClearDeferredObstacleHits();
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
            bool withinVisionCone = true;
            if (_playerTransform != null)
            {
                float3 toPlayer = (float3)(_playerTransform.position - _cachedSelfPosition);
                float toPlayerLengthSq = math.lengthsq(toPlayer);
                if (toPlayerLengthSq > 0.0001f && visionConeAngle < 359f)
                {
                    float3 playerDirection = toPlayer * math.rsqrt(toPlayerLengthSq);
                    float coneDotThreshold = math.cos(math.radians(math.clamp(visionConeAngle, 10f, 360f) * 0.5f));
                    withinVisionCone = math.dot((float3)_cachedSelfForward, playerDirection) >= coneDotThreshold;
                }
            }

            bool visualContact = _playerTransform != null &&
                                 withinVisionCone &&
                                 distSqrToPlayer < aggroDistance * aggroDistance &&
                                 HasPlayerLineOfSightThroughCaveSdf(_cachedSelfPosition, _playerTransform.position);
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
            distSqrToPlayer = (_lastKnownPlayerPosition - _cachedSelfPosition).sqrMagnitude;
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

        public bool TryGetPerceivedPlayerVelocity(out Vector3 playerVelocity)
        {
            if (hasVisualPlayerContact && _playerRigidbody != null)
            {
                playerVelocity = _playerRigidbody.linearVelocity;
                return true;
            }

            playerVelocity = default;
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

            if (FaunaSpatialHashRegistry.TryGetNearestBioform(
                    _cachedSelfPosition,
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
            Vector3 forwardDirection = velocity.sqrMagnitude > 0.0001f ? velocity.normalized : _cachedSelfForward;
            _rayDirs[0] = forwardDirection;
            _queuedForwardObstacleRayDirection = forwardDirection;
            _queuedLeftObstacleRayDirection = RotateObstacleDirection(forwardDirection, -SideObstacleRayYawDegrees);
            _queuedRightObstacleRayDirection = RotateObstacleDirection(forwardDirection, SideObstacleRayYawDegrees);
            _queuedObstacleRayLength = length;
            isAvoidingObstacle = TryResolveObstacleAvoidanceDirection(forwardDirection, length, out Vector3 resolvedDirection, out _);
            if (isAvoidingObstacle)
            {
                _avoidanceTimeAccumulator += Mathf.Max(dt, _foveatedTickIntervalSeconds);
                bestFreeDirection = resolvedDirection;
            }
            else
            {
                _avoidanceTimeAccumulator = 0f;
                bestFreeDirection = forwardDirection;
            }

            ClearDeferredObstacleHits();
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
            if (_profile == null)
                return;

            // 1. Dedicated scavengers still prioritize exposed player tools.
            if (_profile.isScavenger &&
                _playerToolManager != null &&
                _playerToolManager.CurrentTool != null)
            {
                Transform toolTransform = _playerToolManager.CurrentTool.transform;
                if ((toolTransform.position - _cachedSelfPosition).sqrMagnitude < distractorDetectRadius * distractorDetectRadius)
                {
                    currentScavengeTarget = toolTransform;
                    return;
                }
            }

            currentScavengeTarget = ResolveNearestBaitPickup();
        }

        private void UpdatePreyDetection()
        {
            currentPrey = null;
            if (_ownerBrain == null)
                return;

            uint dietMaskBits = _ownerBrain.DietMaskBits;
            if (dietMaskBits != 0u)
            {
                int count = FaunaSpatialHashRegistry.CollectContactsNonAlloc(
                    _cachedSelfPosition,
                    aggroDistance,
                    SpatialTargetKind.Bioform,
                    _preySpatialBuffer);
                float bestDistanceSqr = float.MaxValue;
                for (int i = 0; i < count; i++)
                {
                    SpatialQueryHit hit = _preySpatialBuffer[i];
                    if (!(hit.Owner is FaunaBrain preyBrain) ||
                        preyBrain == _ownerBrain ||
                        preyBrain.IsDead ||
                        preyBrain.SpeciesId == _ownerBrain.SpeciesId ||
                        !preyBrain.IsValidPreyFor(_ownerBrain))
                    {
                        continue;
                    }

                    if (hit.DistanceSqr >= bestDistanceSqr)
                        continue;

                    bestDistanceSqr = hit.DistanceSqr;
                    currentPrey = hit.Transform;
                }

                if (currentPrey != null)
                    return;
            }

            if (_profile == null)
                return;

            LayerMask searchMask = _profile.preyMask != 0 ? _profile.preyMask : preyMask;
            if (searchMask == 0)
                return;

            if (FaunaSpatialHashRegistry.TryGetNearestBioform(
                    _cachedSelfPosition,
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

            int count = FaunaSpatialHashRegistry.CollectContactsNonAlloc(
                _cachedSelfPosition,
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

        private Transform ResolveNearestBaitPickup()
        {
            int count = FaunaSpatialHashRegistry.CollectContactsNonAlloc(
                _cachedSelfPosition,
                distractorDetectRadius,
                SpatialTargetKind.Pickup,
                _distractorSpatialBuffer);
            Transform nearestTransform = null;
            float bestDistanceSqr = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                SpatialQueryHit hit = _distractorSpatialBuffer[i];
                if (!(hit.Owner is Hecton8.Interaction.PickupItem pickupItem) ||
                    !pickupItem.IsFaunaBait ||
                    hit.Transform == null ||
                    hit.DistanceSqr >= bestDistanceSqr)
                {
                    continue;
                }

                bestDistanceSqr = hit.DistanceSqr;
                nearestTransform = hit.Transform;
            }

            return nearestTransform;
        }

        private Transform ResolveNearestBleedingDistractor()
        {
            if (_profile == null)
                return null;

            if (_profile.baseAggro < 0.45f)
                return null;

            int count = FaunaSpatialHashRegistry.CollectContactsNonAlloc(
                _cachedSelfPosition,
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

        internal int BuildDeferredRaycastCommands(RaycastCommand[] commands)
        {
            if (commands == null || commands.Length < DeferredRaycastCommandCount)
                return 0;

            if (_selfTransform == null || lodDisabled || isSleeping)
                return 0;

            int obstacleLayerMask = obstacleMask.value != 0
                ? obstacleMask.value
                : HectonLayerMasks.DefaultRaycastLayerMask;
            if (obstacleLayerMask == 0)
                return 0;

            if (_foveatedImportanceScore < 0.2f)
            {
                return 0;
            }

            float distance = Mathf.Clamp(
                _queuedObstacleRayLength > 0f ? _queuedObstacleRayLength : avoidanceRange,
                avoidanceRange,
                maxRayLength);
            QueryParameters queryParameters = new QueryParameters(obstacleLayerMask, false, QueryTriggerInteraction.Ignore);

            Vector3 forwardDirection = _queuedForwardObstacleRayDirection.sqrMagnitude > 0.0001f
                ? _queuedForwardObstacleRayDirection.normalized
                : _cachedSelfForward;
            Vector3 leftDirection = _queuedLeftObstacleRayDirection.sqrMagnitude > 0.0001f
                ? _queuedLeftObstacleRayDirection.normalized
                : forwardDirection;
            Vector3 rightDirection = _queuedRightObstacleRayDirection.sqrMagnitude > 0.0001f
                ? _queuedRightObstacleRayDirection.normalized
                : forwardDirection;

            commands[ForwardObstacleRayIndex] = new RaycastCommand(
                _cachedSelfPosition,
                forwardDirection,
                queryParameters,
                distance);
            commands[LeftObstacleRayIndex] = new RaycastCommand(
                _cachedSelfPosition,
                leftDirection,
                queryParameters,
                distance);
            commands[RightObstacleRayIndex] = new RaycastCommand(
                _cachedSelfPosition,
                rightDirection,
                queryParameters,
                distance);

            _hasQueuedPlayerLightOcclusionRay = TryBuildPlayerLightOcclusionCommand(commands, out int commandCount);
            return commandCount;
        }

        internal void ConsumeDeferredRaycastHit(int commandIndex, in RaycastHit hit)
        {
            switch (commandIndex)
            {
                case ForwardObstacleRayIndex:
                    _deferredForwardObstacleHit = hit;
                    _hasDeferredForwardObstacleHit = hit.collider != null;
                    break;
                case LeftObstacleRayIndex:
                    _deferredLeftObstacleHit = hit;
                    _hasDeferredLeftObstacleHit = hit.collider != null;
                    break;
                case RightObstacleRayIndex:
                    _deferredRightObstacleHit = hit;
                    _hasDeferredRightObstacleHit = hit.collider != null;
                    break;
                case PlayerLightOcclusionRayIndex:
                    _deferredPlayerLightOcclusionHit = hit;
                    _hasDeferredPlayerLightOcclusionHit = hit.collider != null;
                    break;
            }
        }

        internal bool TryGetDeferredObstacleAvoidance(out Vector3 avoidanceDirection, out float obstaclePressure01)
        {
            return TryResolveObstacleAvoidanceDirection(_cachedSelfForward, _queuedObstacleRayLength, out avoidanceDirection, out obstaclePressure01);
        }

        internal bool TryGetForwardObstacleSurface(out Vector3 surfaceNormal, out float obstaclePressure01)
        {
            surfaceNormal = Vector3.zero;
            obstaclePressure01 = 0f;

            if (!_hasDeferredForwardObstacleHit || _deferredForwardObstacleHit.collider == null)
                return false;

            float rayLength = Mathf.Max(avoidanceRange, _queuedObstacleRayLength);
            if (_deferredForwardObstacleHit.distance <= 0f || _deferredForwardObstacleHit.distance > rayLength)
                return false;

            surfaceNormal = _deferredForwardObstacleHit.normal;
            obstaclePressure01 = 1f - Mathf.Clamp01(_deferredForwardObstacleHit.distance / Mathf.Max(rayLength, 0.001f));
            return obstaclePressure01 > 0f && surfaceNormal.sqrMagnitude > 0.0001f;
        }

        public Transform GetPlayerTransform() => _playerTransform;

        internal bool HasPlayerLightLineOfSight()
        {
            if (!_hasQueuedPlayerLightOcclusionRay)
                return true;

            if (!_hasDeferredPlayerLightOcclusionHit || _deferredPlayerLightOcclusionHit.collider == null)
                return true;

            float blockedDistance = Mathf.Max(0f, _deferredPlayerLightOcclusionHit.distance);
            float targetDistance = Mathf.Max(0.01f, _queuedPlayerLightOcclusionDistance);
            return blockedDistance >= targetDistance - 0.2f;
        }

        private bool TryBuildPlayerLightOcclusionCommand(RaycastCommand[] commands, out int commandCount)
        {
            commandCount = DeferredObstacleRayCount;
            _deferredPlayerLightOcclusionHit = default;
            _hasDeferredPlayerLightOcclusionHit = false;
            _queuedPlayerLightOcclusionDistance = 0f;

            IPlayerRuntimeContext playerContext = Hecton8.Core.GlobalRegistry.Player;
            if (_playerTransform == null && playerContext != null)
                _playerTransform = playerContext.PlayerTransform;

            bool flashlightActive = _hasReportedPlayerNoise && _lastReportedPlayerNoise.FlashlightOn;
            if (!flashlightActive)
            {
                flashlightActive = playerContext != null &&
                                   playerContext.Flashlight != null &&
                                   playerContext.Flashlight.IsOn;
            }

            if (!reactToPlayerLight ||
                !flashlightActive ||
                _playerTransform == null)
            {
                return false;
            }

            Vector3 lightOrigin = _playerTransform.position;
            Vector3 toCreature = _cachedSelfPosition - lightOrigin;
            float distance = toCreature.magnitude;
            if (distance <= 0.01f)
                return false;

            int occlusionMask =
                HectonLayerMasks.TerrainLayerMask |
                HectonLayerMasks.BaseModuleLayerMask |
                HectonLayerMasks.VehicleLayerMask |
                HectonLayerMasks.VoxelCaveLayerMask |
                HectonLayerMasks.DebrisLayerMask;
            if (occlusionMask == 0)
                return false;

            QueryParameters queryParameters = new QueryParameters(occlusionMask, false, QueryTriggerInteraction.Ignore);
            _queuedPlayerLightOcclusionDistance = distance;
            commands[PlayerLightOcclusionRayIndex] = new RaycastCommand(
                lightOrigin,
                toCreature / distance,
                queryParameters,
                distance);
            commandCount = DeferredRaycastCommandCount;
            return true;
        }

        private bool TryResolveObstacleAvoidanceDirection(
            Vector3 fallbackForward,
            float rayLength,
            out Vector3 avoidanceDirection,
            out float obstaclePressure01)
        {
            float safeRayLength = Mathf.Max(avoidanceRange, rayLength);
            Vector3 resolvedForward = fallbackForward.sqrMagnitude > 0.0001f
                ? fallbackForward.normalized
                : _cachedSelfForward;
            float totalPressure = 0f;
            Vector3 avoidance = Vector3.zero;
            bool hasHit = false;

            AccumulateObstacleAvoidance(_hasDeferredForwardObstacleHit, _deferredForwardObstacleHit, safeRayLength, ref totalPressure, ref avoidance, ref hasHit);
            AccumulateObstacleAvoidance(_hasDeferredLeftObstacleHit, _deferredLeftObstacleHit, safeRayLength, ref totalPressure, ref avoidance, ref hasHit);
            AccumulateObstacleAvoidance(_hasDeferredRightObstacleHit, _deferredRightObstacleHit, safeRayLength, ref totalPressure, ref avoidance, ref hasHit);

            obstaclePressure01 = Mathf.Clamp01(totalPressure);
            if (!hasHit || obstaclePressure01 <= 0f)
            {
                avoidanceDirection = Vector3.zero;
                return false;
            }

            Vector3 sideBias = Vector3.zero;
            if (!_hasDeferredLeftObstacleHit)
                sideBias += _queuedLeftObstacleRayDirection;
            if (!_hasDeferredRightObstacleHit)
                sideBias += _queuedRightObstacleRayDirection;

            Vector3 combined = resolvedForward + avoidance + sideBias * 0.35f;
            if (combined.sqrMagnitude <= 0.0001f)
                combined = Vector3.Reflect(resolvedForward, _deferredForwardObstacleHit.normal);

            avoidanceDirection = combined.normalized;
            return avoidanceDirection.sqrMagnitude > 0.0001f;
        }

        private void AccumulateObstacleAvoidance(
            bool hasHit,
            in RaycastHit hit,
            float rayLength,
            ref float totalPressure,
            ref Vector3 avoidance,
            ref bool anyHit)
        {
            if (!hasHit || hit.collider == null || hit.distance <= 0f || hit.distance > rayLength)
                return;

            anyHit = true;
            float pressure = 1f - Mathf.Clamp01(hit.distance / Mathf.Max(rayLength, 0.001f));
            totalPressure += pressure;
            avoidance += hit.normal * pressure;
        }

        private static Vector3 RotateObstacleDirection(Vector3 forwardDirection, float yawDegrees)
        {
            float3 forward = math.normalizesafe((float3)forwardDirection, new float3(0f, 0f, 1f));
            quaternion rotation = quaternion.AxisAngle(new float3(0f, 1f, 0f), math.radians(yawDegrees));
            return (Vector3)math.normalizesafe(math.mul(rotation, forward), new float3(0f, 0f, 1f));
        }

        private void ClearDeferredObstacleHits()
        {
            _deferredForwardObstacleHit = default;
            _deferredLeftObstacleHit = default;
            _deferredRightObstacleHit = default;
            _deferredPlayerLightOcclusionHit = default;
            _hasDeferredForwardObstacleHit = false;
            _hasDeferredLeftObstacleHit = false;
            _hasDeferredRightObstacleHit = false;
            _hasDeferredPlayerLightOcclusionHit = false;
            _hasQueuedPlayerLightOcclusionRay = false;
            _queuedPlayerLightOcclusionDistance = 0f;
        }

        private static bool HasPlayerLineOfSightThroughCaveSdf(Vector3 startPosition, Vector3 endPosition)
        {
            HectonCaveVoxelLightingVolume caveVolume = HectonCaveVoxelLightingVolume.ActiveRuntimeInstance;
            if (caveVolume == null ||
                !caveVolume.TryGetPublishedSignedDistanceVoxelPayload(out NativeArray<byte> signedDistanceVoxels, out Vector3Int gridDimensions, out Vector3 gridOrigin, out Vector3 voxelCellSize))
            {
                return true;
            }

            int3 dimensions = new int3(gridDimensions.x, gridDimensions.y, gridDimensions.z);
            float3 origin = gridOrigin;
            float3 cellSize = voxelCellSize;
            float3 start = startPosition;
            float3 end = endPosition;

            if (!TryWorldToCaveVoxel(end, origin, cellSize, dimensions, out int3 endVoxel))
                return true;

            if (IsCaveVoxelSolid(SampleCaveVoxel(signedDistanceVoxels, endVoxel, dimensions)))
                return false;

            bool hasStartVoxel = TryWorldToCaveVoxel(start, origin, cellSize, dimensions, out int3 startVoxel);
            float3 rayStart = hasStartVoxel ? start : end;
            float3 rayEnd = hasStartVoxel ? end : start;
            int3 currentVoxel = hasStartVoxel ? startVoxel : endVoxel;
            float3 delta = rayEnd - rayStart;
            float distanceSq = math.lengthsq(delta);
            if (distanceSq <= CaveVoxelDdaEpsilon)
                return true;

            float3 rayDirection = delta * math.rsqrt(distanceSq);
            bool3 positiveMask = rayDirection >= 0f;
            bool3 activeAxisMask = math.abs(rayDirection) > CaveVoxelDdaEpsilon;
            int3 step = math.select(new int3(-1, -1, -1), new int3(1, 1, 1), positiveMask);
            float3 cellMin = origin + (new float3(currentVoxel.x, currentVoxel.y, currentVoxel.z) * cellSize);
            float3 voxelBoundary = cellMin + math.select(float3.zero, cellSize, positiveMask);
            float3 safeAbsDirection = math.max(math.abs(rayDirection), new float3(CaveVoxelDdaEpsilon, CaveVoxelDdaEpsilon, CaveVoxelDdaEpsilon));
            float3 rayDirectionInverse = 1f / safeAbsDirection;
            float3 tMax = math.abs((voxelBoundary - rayStart) * rayDirectionInverse);
            float3 tDelta = cellSize * rayDirectionInverse;
            float3 sentinel = new float3(1000000f, 1000000f, 1000000f);
            tMax = math.select(sentinel, tMax, activeAxisMask);
            tDelta = math.select(sentinel, tDelta, activeAxisMask);
            int maxSteps = math.min(dimensions.x + dimensions.y + dimensions.z, 4096);

            for (int i = 0; i < maxSteps; i++)
            {
                if (IsCaveVoxelSolid(SampleCaveVoxel(signedDistanceVoxels, currentVoxel, dimensions)))
                    return false;

                if (hasStartVoxel && math.all(currentVoxel == endVoxel))
                    return true;

                bool3 axisMask = (tMax <= tMax.yzx) & (tMax <= tMax.zxy);
                tMax += math.select(float3.zero, tDelta, axisMask);
                currentVoxel += math.select(int3.zero, step, axisMask);
                if (!IsCaveVoxelInside(currentVoxel, dimensions))
                    return true;
            }

            return true;
        }

        private static bool TryWorldToCaveVoxel(float3 worldPosition, float3 gridOrigin, float3 voxelCellSize, int3 dimensions, out int3 voxel)
        {
            float3 local = worldPosition - gridOrigin;
            if (local.x < 0f || local.y < 0f || local.z < 0f)
            {
                voxel = int3.zero;
                return false;
            }

            int3 candidate = new int3(
                (int)math.floor(local.x / math.max(voxelCellSize.x, CaveVoxelDdaEpsilon)),
                (int)math.floor(local.y / math.max(voxelCellSize.y, CaveVoxelDdaEpsilon)),
                (int)math.floor(local.z / math.max(voxelCellSize.z, CaveVoxelDdaEpsilon)));
            if (!IsCaveVoxelInside(candidate, dimensions))
            {
                voxel = int3.zero;
                return false;
            }

            voxel = candidate;
            return true;
        }

        private static bool IsCaveVoxelInside(int3 voxel, int3 dimensions)
        {
            return voxel.x >= 0 &&
                   voxel.y >= 0 &&
                   voxel.z >= 0 &&
                   voxel.x < dimensions.x &&
                   voxel.y < dimensions.y &&
                   voxel.z < dimensions.z;
        }

        private static byte SampleCaveVoxel(NativeArray<byte> signedDistanceVoxels, int3 voxel, int3 dimensions)
        {
            int flatIndex = voxel.x + (voxel.y * dimensions.x) + (voxel.z * dimensions.x * dimensions.y);
            if (flatIndex < 0 || flatIndex >= signedDistanceVoxels.Length)
                return 255;

            return signedDistanceVoxels[flatIndex];
        }

        private static bool IsCaveVoxelSolid(byte encodedSignedDistance)
        {
            return encodedSignedDistance < CaveSignedDistanceSolidThreshold;
        }
    }
}
