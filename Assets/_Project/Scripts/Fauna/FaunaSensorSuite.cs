using Hecton8.Core;
using UnityEngine;
using Unity.Jobs;
using UnityEngine.Serialization;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Mathematics;

namespace Hecton8.AI
{
    public struct FaunaPerceptionSnapshot
    {
        public bool HasPlayer;
        public bool HasPlayerAup;
        public AbsoluteUniversePosition PlayerAup;
        public Vector3 PlayerPosition;
        public Vector3 PlayerVelocity;
        public Vector3 PlayerForward;
        public bool HasPlayerVelocity;
        public bool HasPlayerForward;
        public bool PlayerFlashlightOn;
        public bool HasScavengeTool;
        public Vector3 ScavengeToolPosition;
        public Component ScavengeToolOwner;
    }

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
        private const int ForwardObstacleRayIndex = 0;
        private const int LeftObstacleRayIndex = 1;
        private const int RightObstacleRayIndex = 2;
        private const float SideObstacleRayYawDegrees = 45f;
        private const float PlayerFlashlightConeDotThreshold = 0.9f;
        private const float PlayerFlashlightBlindDistanceSq = 400f;
        private const float PlayerFlashlightBlindDurationSeconds = 0.35f;
        private const float VisionConeLutMinDegrees = 10f;
        private const float VisionConeLutMaxDegrees = 360f;
        private const float VisionConeLutInvStepDegrees = 0.1f;
        private const int VisionConeLutLastIndex = 35;
        private const float ObstacleRayYawSin45 = 0.70710678f;
        private const float ObstacleRayYawCos45 = 0.70710678f;

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
        public bool hasCurrentThreat;
        public Vector3 currentThreatPosition;
        public Component currentThreatOwner;
        public bool isAvoidingObstacle;
        public Vector3 bestFreeDirection;
        public bool lodDisabled;
        public bool isSleeping;
        public bool hasCurrentDistractor;
        public Vector3 currentDistractorPosition;
        public Component currentDistractorOwner;
        public bool hasCurrentScavengeTarget;
        public Vector3 currentScavengeTargetPosition;
        public Component currentScavengeTargetOwner;
        public bool hasPlayerFlashlightConeHit;
        public float playerFlashlightExposure01;
        public Vector3 playerFlashlightThreatPosition;

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
        [HideInInspector] public bool hasCurrentPrey;
        [HideInInspector] public Vector3 currentPreyPosition;
        [HideInInspector] public Component currentPreyOwner;

        private FaunaBrain _ownerBrain;
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
        private bool _hasDeferredForwardObstacleHit;
        private bool _hasDeferredLeftObstacleHit;
        private bool _hasDeferredRightObstacleHit;
        private FoveatedTickRate _foveatedTickRate = FoveatedTickRate.Center60Hz;
        private float _foveatedTickIntervalSeconds = 1.0f / 60.0f;
        private float _foveatedImportanceScore = 1.0f;
        private bool _foveatedInsideFrustum = true;
        private Vector3 _cachedSelfPosition;
        private Vector3 _cachedSelfForward;
        private AbsoluteUniversePosition _cachedSelfAup;
        private AbsoluteUniversePosition _cachedPlayerAup;
        private bool _hasPlayerSnapshot;
        private bool _hasPlayerVelocitySnapshot;
        private bool _hasPlayerForwardSnapshot;
        private bool _playerFlashlightOn;
        private Vector3 _cachedPlayerPosition;
        private Vector3 _cachedPlayerVelocity;
        private Vector3 _cachedPlayerForward;
        private bool _hasScavengeToolSnapshot;
        private Vector3 _cachedScavengeToolPosition;
        private Component _cachedScavengeToolOwner;
        private float _flashBlindUntilTimeSeconds;
        
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

        public void Init(FaunaBrain ownerBrain, FaunaSpeciesProfile profile)
        {
            _ownerBrain = ownerBrain;
            _profile = profile;
            _lastReportedPlayerNoise = default;
            _hasReportedPlayerNoise = false;
            _hasLastKnownPlayerPosition = false;
            _lastKnownPlayerPosition = default;
            _lastReportedPlayerTimeSeconds = float.NegativeInfinity;
            _lastKnownPlayerTimeSeconds = float.NegativeInfinity;
            _authoredTimeSeconds = 0f;
            _queuedObstacleRayLength = avoidanceRange;
            _deferredForwardObstacleHit = default;
            _deferredLeftObstacleHit = default;
            _deferredRightObstacleHit = default;
            _hasDeferredForwardObstacleHit = false;
            _hasDeferredLeftObstacleHit = false;
            _hasDeferredRightObstacleHit = false;
            _foveatedTickRate = FoveatedTickRate.Center60Hz;
            _foveatedTickIntervalSeconds = 1.0f / 60.0f;
            _foveatedImportanceScore = 1.0f;
            _foveatedInsideFrustum = true;
            Vector3 initialForward = Vector3.forward;
            _queuedForwardObstacleRayDirection = initialForward;
            _queuedLeftObstacleRayDirection = initialForward;
            _queuedRightObstacleRayDirection = initialForward;
            _cachedSelfPosition = Vector3.zero;
            _cachedSelfForward = initialForward;
            _cachedSelfAup = AbsoluteUniversePosition.FromRuntimePosition(_cachedSelfPosition);
            _cachedPlayerAup = default;
            _hasPlayerSnapshot = false;
            _hasPlayerVelocitySnapshot = false;
            _hasPlayerForwardSnapshot = false;
            _playerFlashlightOn = false;
            _cachedPlayerPosition = default;
            _cachedPlayerVelocity = default;
            _cachedPlayerForward = Vector3.forward;
            _hasScavengeToolSnapshot = false;
            _cachedScavengeToolPosition = default;
            _cachedScavengeToolOwner = null;
            _flashBlindUntilTimeSeconds = float.NegativeInfinity;
            hasPlayerFlashlightConeHit = false;
            playerFlashlightExposure01 = 0f;
            playerFlashlightThreatPosition = default;
            ClearSpatialTargets();
        }

        public void Tick(
            float dt,
            Vector3 selfPosition,
            Vector3 selfForward,
            Vector3 velocity,
            in FaunaPerceptionSnapshot perceptionSnapshot,
            float currentTimeSeconds,
            bool forceLongRangeCognition)
        {
            float safeCurrentTimeSeconds = math.isfinite(currentTimeSeconds) ? currentTimeSeconds : _authoredTimeSeconds;
            _authoredTimeSeconds = safeCurrentTimeSeconds;
            if (!IsFinite(selfPosition))
            {
                distSqrToPlayer = float.MaxValue;
                lodDisabled = true;
                isSleeping = true;
                isAvoidingObstacle = false;
                canSeePlayer = false;
                hasVisualPlayerContact = false;
                hasNoisePlayerContact = false;
                isThreatened = false;
                hasPlayerFlashlightConeHit = false;
                playerFlashlightExposure01 = 0f;
                playerFlashlightThreatPosition = default;
                _avoidanceTimeAccumulator = 0f;
                ClearPerceptionCache();
                ClearDeferredObstacleHits();
                ClearSpatialTargets();
                return;
            }

            _cachedSelfPosition = selfPosition;
            _cachedSelfForward = ResolveDominantAxisDirection(selfForward, Vector3.forward);
            _cachedSelfAup = AbsoluteUniversePosition.FromRuntimePosition(_cachedSelfPosition);
            CachePerceptionSnapshot(in perceptionSnapshot);
            hasPlayerFlashlightConeHit = false;
            playerFlashlightExposure01 = 0f;
            playerFlashlightThreatPosition = default;

            if (_hasPlayerSnapshot)
            {
                distSqrToPlayer = (float)math.min(
                    AbsoluteUniversePosition.DistanceSq(in _cachedSelfAup, in _cachedPlayerAup),
                    float.MaxValue);
                lodDisabled = !forceLongRangeCognition && distSqrToPlayer > 150f * 150f;
                isSleeping = !forceLongRangeCognition && distSqrToPlayer > sleepDistance * sleepDistance;
            }
            else
            {
                distSqrToPlayer = float.MaxValue;
                lodDisabled = false;
                isSleeping = false;
            }

            if (lodDisabled || isSleeping)
            {
                isAvoidingObstacle = false;
                _avoidanceTimeAccumulator = 0f;
                ClearDeferredObstacleHits();
                return;
            }

            UpdateMajorSenses();
            UpdatePlayerFlashlightConeHit();
            UpdateObstacleAvoidance(dt, velocity);
            UpdateDistractorDetection();
            UpdatePreyDetection();
            UpdateThreatDetection();
            UpdateScavengeTarget();
            if (IsStuck) UpdatePOISearch();
        }

        private void CachePerceptionSnapshot(in FaunaPerceptionSnapshot perceptionSnapshot)
        {
            _hasPlayerSnapshot = perceptionSnapshot.HasPlayer && IsFinite(perceptionSnapshot.PlayerPosition);
            _hasPlayerVelocitySnapshot = _hasPlayerSnapshot &&
                                         perceptionSnapshot.HasPlayerVelocity &&
                                         IsFinite(perceptionSnapshot.PlayerVelocity);
            _hasPlayerForwardSnapshot = _hasPlayerSnapshot &&
                                        perceptionSnapshot.HasPlayerForward &&
                                        IsFinite(perceptionSnapshot.PlayerForward);
            _playerFlashlightOn = _hasPlayerSnapshot && perceptionSnapshot.PlayerFlashlightOn;
            _cachedPlayerPosition = _hasPlayerSnapshot ? perceptionSnapshot.PlayerPosition : default;
            _cachedPlayerVelocity = _hasPlayerVelocitySnapshot ? perceptionSnapshot.PlayerVelocity : default;
            _cachedPlayerForward = _hasPlayerForwardSnapshot && perceptionSnapshot.PlayerForward.sqrMagnitude > 0.0001f
                ? ResolveDominantAxisDirection(perceptionSnapshot.PlayerForward, Vector3.forward)
                : Vector3.forward;
            _cachedPlayerAup = _hasPlayerSnapshot && perceptionSnapshot.HasPlayerAup && IsFiniteAup(in perceptionSnapshot.PlayerAup)
                ? perceptionSnapshot.PlayerAup
                : _hasPlayerSnapshot
                ? AbsoluteUniversePosition.FromRuntimePosition(_cachedPlayerPosition)
                : default;
            _hasScavengeToolSnapshot = perceptionSnapshot.HasScavengeTool &&
                                       perceptionSnapshot.ScavengeToolOwner != null &&
                                       IsFinite(perceptionSnapshot.ScavengeToolPosition);
            _cachedScavengeToolPosition = _hasScavengeToolSnapshot ? perceptionSnapshot.ScavengeToolPosition : default;
            _cachedScavengeToolOwner = _hasScavengeToolSnapshot ? perceptionSnapshot.ScavengeToolOwner : null;
        }

        private void ClearPerceptionCache()
        {
            _hasPlayerSnapshot = false;
            _hasPlayerVelocitySnapshot = false;
            _hasPlayerForwardSnapshot = false;
            _playerFlashlightOn = false;
            _cachedPlayerPosition = default;
            _cachedPlayerVelocity = default;
            _cachedPlayerForward = Vector3.forward;
            _cachedPlayerAup = default;
            _hasScavengeToolSnapshot = false;
            _cachedScavengeToolPosition = default;
            _cachedScavengeToolOwner = null;
        }

        private void ClearSpatialTargets()
        {
            hasCurrentThreat = false;
            currentThreatPosition = default;
            currentThreatOwner = null;
            hasCurrentDistractor = false;
            currentDistractorPosition = default;
            currentDistractorOwner = null;
            hasCurrentScavengeTarget = false;
            currentScavengeTargetPosition = default;
            currentScavengeTargetOwner = null;
            hasCurrentPrey = false;
            currentPreyPosition = default;
            currentPreyOwner = null;
        }

        private static float ResolveVisionConeDotThreshold(float fullConeAngleDegrees)
        {
            float clampedDegrees = math.clamp(fullConeAngleDegrees, VisionConeLutMinDegrees, VisionConeLutMaxDegrees);
            float scaledIndex = (clampedDegrees - VisionConeLutMinDegrees) * VisionConeLutInvStepDegrees;
            int lowerIndex = (int)scaledIndex;
            if (lowerIndex >= VisionConeLutLastIndex)
                return ResolveVisionConeDotThresholdSample(VisionConeLutLastIndex);

            float blend = scaledIndex - lowerIndex;
            return math.lerp(
                ResolveVisionConeDotThresholdSample(lowerIndex),
                ResolveVisionConeDotThresholdSample(lowerIndex + 1),
                blend);
        }

        private static float ResolveVisionConeDotThresholdSample(int index)
        {
            switch (index)
            {
                case 0: return 0.9961947f;
                case 1: return 0.98480775f;
                case 2: return 0.96592583f;
                case 3: return 0.93969262f;
                case 4: return 0.90630779f;
                case 5: return 0.8660254f;
                case 6: return 0.81915204f;
                case 7: return 0.76604444f;
                case 8: return 0.70710678f;
                case 9: return 0.64278761f;
                case 10: return 0.57357644f;
                case 11: return 0.5f;
                case 12: return 0.42261826f;
                case 13: return 0.34202014f;
                case 14: return 0.25881905f;
                case 15: return 0.17364818f;
                case 16: return 0.08715574f;
                case 17: return 0f;
                case 18: return -0.08715574f;
                case 19: return -0.17364818f;
                case 20: return -0.25881905f;
                case 21: return -0.34202014f;
                case 22: return -0.42261826f;
                case 23: return -0.5f;
                case 24: return -0.57357644f;
                case 25: return -0.64278761f;
                case 26: return -0.70710678f;
                case 27: return -0.76604444f;
                case 28: return -0.81915204f;
                case 29: return -0.8660254f;
                case 30: return -0.90630779f;
                case 31: return -0.93969262f;
                case 32: return -0.96592583f;
                case 33: return -0.98480775f;
                case 34: return -0.9961947f;
                default: return -1f;
            }
        }

        private void UpdateMajorSenses()
        {
            bool withinVisionCone = true;
            if (_hasPlayerSnapshot)
            {
                float3 toPlayer = (float3)(_cachedPlayerPosition - _cachedSelfPosition);
                float toPlayerLengthSq = math.lengthsq(toPlayer);
                if (toPlayerLengthSq > 0.0001f && visionConeAngle < 359f)
                {
                    float3 playerDirection = toPlayer * math.rsqrt(toPlayerLengthSq);
                    float coneDotThreshold = ResolveVisionConeDotThreshold(visionConeAngle);
                    withinVisionCone = math.dot((float3)_cachedSelfForward, playerDirection) >= coneDotThreshold;
                }
            }

            bool visualContact = _hasPlayerSnapshot &&
                                 withinVisionCone &&
                                 distSqrToPlayer < aggroDistance * aggroDistance &&
                                 HasPlayerLineOfSightThroughNavGrid(_cachedPlayerPosition);
            bool reportedContact = HasFreshReportedPlayerNoise();
            if (IsFlashBlinded())
            {
                visualContact = false;
                reportedContact = false;
            }

            hasVisualPlayerContact = visualContact;
            hasNoisePlayerContact = reportedContact;
            canSeePlayer = visualContact || reportedContact;

            if (visualContact)
                RememberPlayerPosition(_cachedPlayerPosition);
        }

        private void UpdatePlayerFlashlightConeHit()
        {
            hasPlayerFlashlightConeHit = false;
            playerFlashlightExposure01 = 0f;
            playerFlashlightThreatPosition = default;

            if (!reactToPlayerLight ||
                !_playerFlashlightOn ||
                !_hasPlayerSnapshot ||
                !_hasPlayerForwardSnapshot ||
                IsFlashBlinded())
            {
                return;
            }

            float3 toCreature = (float3)(_cachedSelfPosition - _cachedPlayerPosition);
            double aupDistanceSq = AbsoluteUniversePosition.DistanceSq(in _cachedSelfAup, in _cachedPlayerAup);
            if (aupDistanceSq <= 0.0001d || aupDistanceSq > PlayerFlashlightBlindDistanceSq)
                return;

            float distanceSq = (float)math.min(aupDistanceSq, float.MaxValue);
            float3 lightDirection = (float3)ResolveDominantAxisDirection(_cachedPlayerForward, Vector3.forward);
            float flashlightDotNumerator = math.dot(lightDirection, toCreature);
            if (flashlightDotNumerator <= 0f)
                return;

            float dotThresholdSq = PlayerFlashlightConeDotThreshold * PlayerFlashlightConeDotThreshold;
            float dotNumeratorSq = flashlightDotNumerator * flashlightDotNumerator;
            if (dotNumeratorSq < dotThresholdSq * distanceSq)
                return;

            float coneSq01 = math.saturate(((dotNumeratorSq / math.max(distanceSq, 0.0001f)) - dotThresholdSq) / math.max(0.001f, 1f - dotThresholdSq));
            float distance01 = 1f - math.saturate(distanceSq / PlayerFlashlightBlindDistanceSq);
            float cone01 = coneSq01 * coneSq01;
            float exposure01 = math.saturate(cone01 * distance01);
            if (exposure01 <= 0.001f)
                return;

            hasPlayerFlashlightConeHit = true;
            playerFlashlightExposure01 = exposure01;
            playerFlashlightThreatPosition = _cachedPlayerPosition;
            ApplyFlashBlind(_authoredTimeSeconds, PlayerFlashlightBlindDurationSeconds);
        }

        public void ReceivePlayerNoiseSignal(NoiseSystem.PlayerNoiseSignal playerNoise)
        {
            _lastReportedPlayerNoise = playerNoise;
            _hasReportedPlayerNoise = true;
            _lastReportedPlayerTimeSeconds = _authoredTimeSeconds;
            hasNoisePlayerContact = true;
            RememberPlayerPosition(playerNoise.Position);
            AbsoluteUniversePosition playerNoiseAup = playerNoise.PositionAup;
            distSqrToPlayer = (float)math.min(
                AbsoluteUniversePosition.DistanceSq(in playerNoiseAup, in _cachedSelfAup),
                float.MaxValue);
        }

        private bool HasFreshReportedPlayerNoise()
        {
            if (!_hasReportedPlayerNoise)
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

        public bool TryGetPerceivedPlayerPosition(out Vector3 playerPosition)
        {
            if (IsFlashBlinded())
            {
                playerPosition = default;
                return false;
            }

            if (hasVisualPlayerContact && _hasPlayerSnapshot)
            {
                playerPosition = _cachedPlayerPosition;
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
            if (IsFlashBlinded())
            {
                playerVelocity = default;
                return false;
            }

            if (hasVisualPlayerContact && _hasPlayerVelocitySnapshot)
            {
                playerVelocity = _cachedPlayerVelocity;
                return true;
            }

            playerVelocity = default;
            return false;
        }

        public bool TryGetPerceivedPlayerForward(out Vector3 playerForward)
        {
            if (IsFlashBlinded())
            {
                playerForward = default;
                return false;
            }

            if (hasVisualPlayerContact && _hasPlayerForwardSnapshot)
            {
                playerForward = _cachedPlayerForward;
                return true;
            }

            playerForward = default;
            return false;
        }

        public void ApplyFlashBlind(float currentTimeSeconds, float durationSeconds)
        {
            _flashBlindUntilTimeSeconds = math.max(_flashBlindUntilTimeSeconds, currentTimeSeconds + math.max(0f, durationSeconds));
            canSeePlayer = false;
            hasVisualPlayerContact = false;
            hasNoisePlayerContact = false;
            _hasLastKnownPlayerPosition = false;
            _cachedPlayerAup = default;
            distSqrToPlayer = float.MaxValue;
        }

        private bool IsFlashBlinded()
        {
            return _authoredTimeSeconds < _flashBlindUntilTimeSeconds;
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
            hasCurrentThreat = false;
            currentThreatPosition = default;
            currentThreatOwner = null;
            
            if (territoryMask == 0 || _profile == null)
                return;

            if (FaunaSpatialHashRegistry.TryGetNearestAdjacentBioform(
                    in _cachedSelfAup,
                    _profile.territoryThreatRadius,
                    _profile.predatorMask,
                    _ownerBrain,
                    _profile.speciesID,
                    false,
                    out SpatialQueryHit threatHit))
            {
                hasCurrentThreat = true;
                currentThreatPosition = threatHit.Position;
                currentThreatOwner = threatHit.Owner;
                isThreatened = currentThreatOwner != null;
            }
        }

        private void UpdateObstacleAvoidance(float dt, Vector3 velocity)
        {
            Vector3 safeVelocity = IsFinite(velocity) ? velocity : Vector3.zero;
            float safeDeltaTime = math.isfinite(dt) ? math.max(dt, 0f) : _foveatedTickIntervalSeconds;
            float length = math.clamp(avoidanceRange + ApproximateMagnitude(safeVelocity) * lookAheadFactor, avoidanceRange, maxRayLength);
            Vector3 forwardDirection = safeVelocity.sqrMagnitude > 0.0001f
                ? ResolveDominantAxisDirection(safeVelocity, _cachedSelfForward)
                : _cachedSelfForward;
            _rayDirs[0] = forwardDirection;
            _queuedForwardObstacleRayDirection = forwardDirection;
            _queuedLeftObstacleRayDirection = RotateObstacleDirection(forwardDirection, -SideObstacleRayYawDegrees);
            _queuedRightObstacleRayDirection = RotateObstacleDirection(forwardDirection, SideObstacleRayYawDegrees);
            _queuedObstacleRayLength = length;
            isAvoidingObstacle = TryResolveObstacleAvoidanceDirection(forwardDirection, length, out Vector3 resolvedDirection, out _);
            if (isAvoidingObstacle)
            {
                _avoidanceTimeAccumulator += math.max(safeDeltaTime, _foveatedTickIntervalSeconds);
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
            hasCurrentDistractor = false;
            currentDistractorPosition = default;
            currentDistractorOwner = null;

            if (TryResolveNearestBleedingDistractor(out SpatialQueryHit bleedingHit))
            {
                hasCurrentDistractor = true;
                currentDistractorPosition = bleedingHit.Position;
                currentDistractorOwner = bleedingHit.Owner;
                return;
            }

            if (TryResolveNearestFlareDistractor(out SpatialQueryHit flareHit))
            {
                hasCurrentDistractor = true;
                currentDistractorPosition = flareHit.Position;
                currentDistractorOwner = flareHit.Owner;
            }
        }

        private void UpdateScavengeTarget()
        {
            hasCurrentScavengeTarget = false;
            currentScavengeTargetPosition = default;
            currentScavengeTargetOwner = null;
            if (_profile == null)
                return;

            if (_profile.isScavenger &&
                _hasScavengeToolSnapshot &&
                _cachedScavengeToolOwner != null)
            {
                AbsoluteUniversePosition toolAup = AbsoluteUniversePosition.FromRuntimePosition(_cachedScavengeToolPosition);
                float toolDistanceSqr = (float)math.min(
                    AbsoluteUniversePosition.DistanceSq(in toolAup, in _cachedSelfAup),
                    float.MaxValue);
                if (toolDistanceSqr < distractorDetectRadius * distractorDetectRadius)
                {
                    hasCurrentScavengeTarget = true;
                    currentScavengeTargetPosition = _cachedScavengeToolPosition;
                    currentScavengeTargetOwner = _cachedScavengeToolOwner;
                    return;
                }
            }

            if (TryResolveNearestBaitPickup(out SpatialQueryHit baitHit))
            {
                hasCurrentScavengeTarget = true;
                currentScavengeTargetPosition = baitHit.Position;
                currentScavengeTargetOwner = baitHit.Owner;
            }
        }

        private void UpdatePreyDetection()
        {
            hasCurrentPrey = false;
            currentPreyPosition = default;
            currentPreyOwner = null;
            if (_ownerBrain == null)
                return;

            uint dietMaskBits = _ownerBrain.DietMaskBits;
            if (dietMaskBits != 0u)
            {
                int count = FaunaSpatialHashRegistry.CollectAdjacentContactsNonAlloc(
                    in _cachedSelfAup,
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
                    hasCurrentPrey = true;
                    currentPreyPosition = hit.Position;
                    currentPreyOwner = hit.Owner;
                }

                if (hasCurrentPrey)
                    return;
            }

            if (_profile == null)
                return;

            LayerMask searchMask = _profile.preyMask != 0 ? _profile.preyMask : preyMask;
            if (searchMask == 0)
                return;

            if (FaunaSpatialHashRegistry.TryGetNearestAdjacentBioform(
                    in _cachedSelfAup,
                    aggroDistance,
                    searchMask,
                    _ownerBrain,
                    -1,
                    true,
                    out SpatialQueryHit preyHit))
            {
                hasCurrentPrey = true;
                currentPreyPosition = preyHit.Position;
                currentPreyOwner = preyHit.Owner;
            }
        }

        private void UpdatePOISearch()
        {
            // Logic for finding EscapePoints via poiMask...
        }

        private bool TryResolveNearestFlareDistractor(out SpatialQueryHit nearestHit)
        {
            nearestHit = default;
            int layerMaskValue = distractorMask.value;
            if (layerMaskValue == 0)
                return false;

            int count = FaunaSpatialHashRegistry.CollectAdjacentContactsNonAlloc(
                in _cachedSelfAup,
                distractorDetectRadius,
                SpatialTargetKind.Pickup | SpatialTargetKind.Signal,
                _distractorSpatialBuffer);
            float bestDistanceSqr = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                SpatialQueryHit hit = _distractorSpatialBuffer[i];
                if ((layerMaskValue & (1 << hit.Layer)) == 0)
                    continue;

                if (!(hit.Owner is DeployableFlare))
                    continue;

                if (hit.DistanceSqr >= bestDistanceSqr)
                    continue;

                bestDistanceSqr = hit.DistanceSqr;
                nearestHit = hit;
            }

            return nearestHit.Owner != null;
        }

        private bool TryResolveNearestBaitPickup(out SpatialQueryHit nearestHit)
        {
            nearestHit = default;
            int count = FaunaSpatialHashRegistry.CollectAdjacentContactsNonAlloc(
                in _cachedSelfAup,
                distractorDetectRadius,
                SpatialTargetKind.Pickup,
                _distractorSpatialBuffer);
            float bestDistanceSqr = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                SpatialQueryHit hit = _distractorSpatialBuffer[i];
                if (!(hit.Owner is Hecton8.Interaction.PickupItem pickupItem) ||
                    !pickupItem.IsFaunaBait ||
                    hit.DistanceSqr >= bestDistanceSqr)
                {
                    continue;
                }

                bestDistanceSqr = hit.DistanceSqr;
                nearestHit = hit;
            }

            return nearestHit.Owner != null;
        }

        private bool TryResolveNearestBleedingDistractor(out SpatialQueryHit nearestHit)
        {
            nearestHit = default;
            if (_profile == null)
                return false;

            if (_profile.baseAggro < 0.45f)
                return false;

            int count = FaunaSpatialHashRegistry.CollectAdjacentContactsNonAlloc(
                in _cachedSelfAup,
                distractorDetectRadius,
                SpatialTargetKind.Signal,
                _distractorSpatialBuffer);
            float bestWeightedDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                SpatialQueryHit hit = _distractorSpatialBuffer[i];
                HectonSurvivalSystem survival = hit.Owner as HectonSurvivalSystem;
                if (survival == null || !survival.IsBleeding)
                    continue;

                float bleedSeverity = math.max(0.1f, survival.BleedingSeverity01);
                float weightedDistance = hit.DistanceSqr / bleedSeverity;
                if (weightedDistance >= bestWeightedDistance)
                    continue;

                bestWeightedDistance = weightedDistance;
                nearestHit = hit;
            }

            return nearestHit.Owner != null;
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
            ClearDeferredObstacleHits();
            return 0;
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

            float rayLength = math.max(avoidanceRange, _queuedObstacleRayLength);
            if (_deferredForwardObstacleHit.distance <= 0f || _deferredForwardObstacleHit.distance > rayLength)
                return false;

            surfaceNormal = _deferredForwardObstacleHit.normal;
            obstaclePressure01 = 1f - math.saturate(_deferredForwardObstacleHit.distance / math.max(rayLength, 0.001f));
            return obstaclePressure01 > 0f && surfaceNormal.sqrMagnitude > 0.0001f;
        }

        internal bool HasPlayerLightLineOfSight()
        {
            return true;
        }

        private bool TryResolveObstacleAvoidanceDirection(
            Vector3 fallbackForward,
            float rayLength,
            out Vector3 avoidanceDirection,
            out float obstaclePressure01)
        {
            float safeRayLength = math.max(avoidanceRange, rayLength);
            Vector3 resolvedForward = fallbackForward.sqrMagnitude > 0.0001f
                ? ResolveDominantAxisDirection(fallbackForward, _cachedSelfForward)
                : _cachedSelfForward;
            Vector3 leftDirection = _queuedLeftObstacleRayDirection.sqrMagnitude > 0.0001f
                ? ResolveDominantAxisDirection(_queuedLeftObstacleRayDirection, resolvedForward)
                : RotateObstacleDirection(resolvedForward, -SideObstacleRayYawDegrees);
            Vector3 rightDirection = _queuedRightObstacleRayDirection.sqrMagnitude > 0.0001f
                ? ResolveDominantAxisDirection(_queuedRightObstacleRayDirection, resolvedForward)
                : RotateObstacleDirection(resolvedForward, SideObstacleRayYawDegrees);

            bool forwardClosed = TrySampleNavGridObstacle(resolvedForward, safeRayLength, out float forwardPressure01);
            bool leftClosed = TrySampleNavGridObstacle(leftDirection, safeRayLength, out float leftPressure01);
            bool rightClosed = TrySampleNavGridObstacle(rightDirection, safeRayLength, out float rightPressure01);
            if (!forwardClosed && !leftClosed && !rightClosed)
            {
                avoidanceDirection = Vector3.zero;
                obstaclePressure01 = 0f;
                return false;
            }

            float totalPressure = 0f;
            Vector3 avoidance = Vector3.zero;
            if (forwardClosed)
            {
                totalPressure += forwardPressure01;
                avoidance -= resolvedForward * (1f + forwardPressure01);
                if (!leftClosed)
                    avoidance += leftDirection * 0.75f;
                if (!rightClosed)
                    avoidance += rightDirection * 0.75f;
            }

            if (leftClosed)
            {
                totalPressure += leftPressure01 * 0.75f;
                avoidance -= leftDirection * leftPressure01;
                if (!rightClosed)
                    avoidance += rightDirection * 0.5f;
            }

            if (rightClosed)
            {
                totalPressure += rightPressure01 * 0.75f;
                avoidance -= rightDirection * rightPressure01;
                if (!leftClosed)
                    avoidance += leftDirection * 0.5f;
            }

            obstaclePressure01 = math.saturate(totalPressure);
            Vector3 combined = resolvedForward + avoidance;
            if (combined.sqrMagnitude <= 0.0001f)
                combined = -resolvedForward;

            avoidanceDirection = ResolveDominantAxisDirection(combined, -resolvedForward);
            return avoidanceDirection.sqrMagnitude > 0.0001f;
        }

        private static Vector3 RotateObstacleDirection(Vector3 forwardDirection, float yawDegrees)
        {
            float3 forward = (float3)ResolveDominantAxisDirection(forwardDirection, Vector3.forward);
            float yawSign = yawDegrees < 0f ? -1f : 1f;
            float yawSin = ObstacleRayYawSin45 * yawSign;
            float3 rotated = new float3(
                (forward.x * ObstacleRayYawCos45) + (forward.z * yawSin),
                forward.y,
                (-forward.x * yawSin) + (forward.z * ObstacleRayYawCos45));
            return ResolveDominantAxisDirection((Vector3)rotated, Vector3.forward);
        }

        private void ClearDeferredObstacleHits()
        {
            _deferredForwardObstacleHit = default;
            _deferredLeftObstacleHit = default;
            _deferredRightObstacleHit = default;
            _hasDeferredForwardObstacleHit = false;
            _hasDeferredLeftObstacleHit = false;
            _hasDeferredRightObstacleHit = false;
        }

        private static bool HasPlayerLineOfSightThroughNavGrid(Vector3 endPosition)
        {
            return !TrySampleClosedNavGridCell(endPosition);
        }

        private bool TrySampleNavGridObstacle(Vector3 direction, float distanceMeters, out float pressure01)
        {
            pressure01 = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
                return false;

            float probeDistance = math.clamp(distanceMeters, math.max(0.25f, avoidanceRange * 0.5f), maxRayLength);
            Vector3 probePosition = _cachedSelfPosition + ResolveDominantAxisDirection(direction, _cachedSelfForward) * probeDistance;
            if (!TrySampleClosedNavGridCell(probePosition))
                return false;

            pressure01 = math.saturate(1f - (probeDistance / math.max(maxRayLength, 0.001f)));
            if (pressure01 <= 0.001f)
                pressure01 = 1f;
            return true;
        }

        private static bool TrySampleClosedNavGridCell(Vector3 runtimePosition)
        {
            if (!IsFinite(runtimePosition))
                return false;

            if (!VoxelDynamicNavGridRuntime.TrySampleHybridNavigation(
                    new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z),
                    out VoxelDynamicNavGridRuntime.HybridNavigationSample sample))
            {
                return false;
            }

            return sample.Mode == VoxelDynamicNavGridRuntime.HybridNavigationMode.SolidVoxel ||
                   sample.Passability == VoxelDynamicNavGridRuntime.SolidCell;
        }

        private static Vector3 ResolveDominantAxisDirection(Vector3 direction, Vector3 fallback)
        {
            if (!IsFinite(direction) || direction.sqrMagnitude <= 0.0001f)
                direction = fallback;

            if (!IsFinite(direction) || direction.sqrMagnitude <= 0.0001f)
                return Vector3.forward;

            float absX = math.abs(direction.x);
            float absY = math.abs(direction.y);
            float absZ = math.abs(direction.z);
            if (absX >= absY && absX >= absZ)
                return direction.x < 0f ? Vector3.left : Vector3.right;

            if (absY >= absZ)
                return direction.y < 0f ? Vector3.down : Vector3.up;

            return direction.z < 0f ? Vector3.back : Vector3.forward;
        }

        private static float ApproximateMagnitude(Vector3 value)
        {
            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            float az = math.abs(value.z);
            float max = math.max(ax, math.max(ay, az));
            float min = math.min(ax, math.min(ay, az));
            float mid = ax + ay + az - max - min;
            return max + mid * 0.375f + min * 0.125f;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z);
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition position)
        {
            return math.all(math.isfinite(new float3(position.LocalX, position.LocalY, position.LocalZ)));
        }
    }
}
