using Hecton8.Core;
using UnityEngine;
using UnityEngine.Rendering;
using Hecton8.Physics;
using Unity.Mathematics;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.World
{
    /// <summary>
    /// Pooled collapse chunk spawned from catastrophic sargassum canopy failures.
    /// Owns rigidbody reset, optional silt trail playback, and timed despawn.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SargassumCollapseChunk : MonoBehaviour, ITickable, IFixedTickable, IPoolable, IOriginShiftListener
    {
        private const string ScrapPickupPrefabAssetPath = "Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_TitaniumScrap.prefab";
        private static readonly Vector3[] ScrapEjectDirections =
        {
            new Vector3(0.22f, 1f, 0.12f),
            new Vector3(-0.28f, 1f, 0.06f),
            new Vector3(0.09f, 1f, -0.25f),
            new Vector3(-0.12f, 1f, -0.18f)
        };
        [Header("Ã¢â€â‚¬Ã¢â€â‚¬ Runtime Wiring Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬")]
        [SerializeField]
        [Tooltip("Cached rigidbody used to drive the falling chunk.")]
        private Rigidbody chunkRigidbody;

        [SerializeField]
        [Tooltip("Optional looping particle trail emitted while the chunk sinks.")]
        private ParticleSystem siltTrail;

        [SerializeField]
        [Tooltip("Pooled physical scrap pickup prefab spawned when the chunk disintegrates into salvage.")]
        private GameObject scrapPickupPrefab;

        [Header("Ã¢â€â‚¬Ã¢â€â‚¬ Defaults Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬")]
        [SerializeField, Min(0.5f)]
        [Tooltip("Fallback lifetime used when ActivateChunk receives an invalid despawn delay.")]
        private float defaultLifetime = 18f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Drag blend applied while the chunk is active. Higher values keep the fall heavy and damped.")]
        private float activeDrag = 0.18f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Angular-drag blend applied while the chunk tumbles downward.")]
        private float activeAngularDrag = 0.55f;

        [SerializeField, Range(0f, 96f)]
        [Tooltip("Minimum muddy emission rate while the chunk is actively sinking.")]
        private float siltTrailBaseRate = 18f;

        [SerializeField, Range(0f, 160f)]
        [Tooltip("Maximum muddy emission rate reached by faster downward collapse chunks.")]
        private float siltTrailMaxRate = 62f;

        [SerializeField, Range(0.1f, 8f)]
        [Tooltip("Downward speed at which the muddy trail reaches full emission.")]
        private float siltTrailFullSpeed = 2.6f;

        [SerializeField, Range(0.01f, 1f)]
        [Tooltip("Downward-speed threshold below which the chunk is considered settled and the muddy trail is forced to stop.")]
        private float siltTrailStopSpeed = 0.18f;

        [Header("── Disintegration ──────────────────")]
        [SerializeField, Range(1f, 60f)]
        [Tooltip("How long a snagged chunk can hang before it tears apart into physical scrap.")]
        private float snagDisintegrationDelay = 48f;

        [SerializeField, Range(1, 4)]
        [Tooltip("How many pooled scrap pickups are released when the chunk disintegrates.")]
        private int scrapPickupCount = 3;

        [SerializeField, Range(0f, 16f)]
        [Tooltip("Thermal integrity budget consumed by cave geysers before this chunk disintegrates.")]
        private float thermalIntegrity = 4f;

        [SerializeField, Range(0f, 8f)]
        [Tooltip("Initial eject speed applied to released scrap pieces.")]
        private float scrapEjectSpeed = 1.8f;

        [Header("Ã¢â€â‚¬Ã¢â€â‚¬ Snag Joints Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬")]
        [SerializeField]
        [Tooltip("Layers treated as snag targets when a collapse chunk slams into the seabed or surrounding wreckage.")]
        private LayerMask snagLayers = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;

        [SerializeField, Range(0.1f, 4f)]
        [Tooltip("Search radius used when probing nearby rock or chunk anchors after impact.")]
        private float snagSearchRadius = 1.2f;

        [SerializeField, Range(0.1f, 12f)]
        [Tooltip("Minimum impact speed required before the chunk attempts to snag instead of simply bouncing.")]
        private float snagImpactSpeedThreshold = 1.6f;

        [SerializeField, Range(0.1f, 120f)]
        [Tooltip("Spring used by the hanging debris joint once the chunk snags into surrounding geometry.")]
        private float snagSpring = 28f;

        [SerializeField, Range(0f, 16f)]
        [Tooltip("Damper applied to the hanging debris spring to keep the joint heavy instead of rubbery.")]
        private float snagDamper = 4.2f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Maximum free distance preserved by the hanging spring before the chunk starts pulling taut.")]
        private float snagMaxDistance = 0.45f;

        [SerializeField, Range(0.01f, 0.5f)]
        [Tooltip("Surface-normal offset applied to snag anchors so hanging chunks pin against cave walls instead of embedding into them.")]
        private float snagSurfaceOffset = 0.08f;

        private Vector3 _defaultLocalScale = Vector3.one;
        private float _defaultLinearDamping;
        private float _defaultAngularDamping;
        private CollisionDetectionMode _defaultCollisionDetectionMode;
        private RigidbodyInterpolation _defaultInterpolation;
        private float _remainingLifetime;
        private bool _registeredTick;
        private bool _hasSnag;
        private bool _cascadeImpactConsumed;
        private int _fragmentDepth;
        private Rigidbody _snagConnectedBody;
        private Vector3 _snagLocalAnchor;
        private Vector3 _snagConnectedAnchor;
        private bool _snagUseSpringOnly;
        private bool _siltTrailSettled;
        private float _snagHangTimer;
        private float _remainingThermalIntegrity;
        private float _scavengerConsume01;
        private bool _registeredScavengerHost;
        private bool _disintegrating;
        private bool _registeredFixedTick;
        private readonly Collider[] _snagColliders = new Collider[8]; // COLD ALLOC: Collider[8] - bounded snag-target probe buffer for collapse chunks - owner: SargassumCollapseChunk
        // COLD ALLOC: ParticleSystem.Particle[192] - reusable world-space silt particle shift buffer - owner: SargassumCollapseChunk
        private ParticleSystem.Particle[] _siltTrailShiftParticles;

        private void Awake()
        {
            ResolveRuntimeWiring(createFallbackTrail: true);
            EnsureSnagJoints();
            EnsureShiftBuffers();

            _defaultLocalScale = transform.localScale;
            if (chunkRigidbody != null)
            {
                _defaultLinearDamping = chunkRigidbody.linearDamping;
                _defaultAngularDamping = chunkRigidbody.angularDamping;
                _defaultCollisionDetectionMode = chunkRigidbody.collisionDetectionMode;
                _defaultInterpolation = chunkRigidbody.interpolation;
            }
        }

        /// <summary>
        /// Arms the pooled chunk with its release velocities and lifetime.
        /// </summary>
        /// <param name="linearVelocityWS">Initial linear velocity in world space.</param>
        /// <param name="angularVelocityWS">Initial angular velocity in world space.</param>
        /// <param name="uniformScale">Uniform world scale multiplier applied to the root.</param>
        /// <param name="despawnDelay">Seconds before the chunk is returned to the pool.</param>
        public void ActivateChunk(Vector3 linearVelocityWS, Vector3 angularVelocityWS, float uniformScale, float despawnDelay)
        {
            ActivateChunk(linearVelocityWS, angularVelocityWS, uniformScale, despawnDelay, 0);
        }

        /// <summary>
        /// Arms the pooled chunk with its release velocities, lifetime, and cascade-fragment depth.
        /// </summary>
        public void ActivateChunk(Vector3 linearVelocityWS, Vector3 angularVelocityWS, float uniformScale, float despawnDelay, int fragmentDepth)
        {
            if (chunkRigidbody != null)
            {
                chunkRigidbody.detectCollisions = true;
                chunkRigidbody.isKinematic = false;
                chunkRigidbody.WakeUp();
                chunkRigidbody.linearDamping = activeDrag;
                chunkRigidbody.angularDamping = activeAngularDrag;
                chunkRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                chunkRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                chunkRigidbody.linearVelocity = linearVelocityWS;
                chunkRigidbody.angularVelocity = angularVelocityWS;
            }

            transform.localScale = _defaultLocalScale * Mathf.Max(0.1f, uniformScale);

            if (siltTrail != null)
            {
                UpdateSiltTrailEmission();
                siltTrail.Clear(true);
                siltTrail.Play(true);
            }

            _remainingLifetime = despawnDelay > 0f ? despawnDelay : defaultLifetime;
            _fragmentDepth = Mathf.Max(0, fragmentDepth);
            _hasSnag = false;
            _cascadeImpactConsumed = false;
            _siltTrailSettled = false;
            _snagHangTimer = 0f;
            _remainingThermalIntegrity = Mathf.Max(0.01f, thermalIntegrity);
            _scavengerConsume01 = 0f;
            _disintegrating = false;
            DisableSnagJoints();
            TryRegister();
        }

        /// <summary>
        /// Advances the pooled lifetime countdown.
        /// </summary>
        /// <param name="dt">Frame delta supplied by GameTickManager.</param>
        public void Tick(float dt)
        {
            if (_remainingLifetime <= 0f)
                return;

            _remainingLifetime -= Mathf.Max(0f, dt);
            UpdateSiltTrailEmission();
            if (_hasSnag)
            {
                _snagHangTimer += Mathf.Max(0f, dt);
                if (_snagHangTimer >= snagDisintegrationDelay)
                {
                    DisintegrateIntoScrap();
                    return;
                }
            }

            if (_remainingLifetime > 0f)
                return;

            ObjectPoolManager poolManager = GlobalRegistry.ObjectPool;
            if (poolManager != null)
                poolManager.Despawn(gameObject);
        }

        /// <summary>
        /// Applies snag-constraint physics through the centralized fixed-step router.
        /// </summary>
        /// <param name="fixedDeltaTime">Physics step delta supplied by GameTickManager.</param>
        public void FixedTick(float fixedDeltaTime)
        {
            if (!_hasSnag || chunkRigidbody == null || fixedDeltaTime <= 0f)
                return;

            ApplySnagConstraint(fixedDeltaTime);
        }

        /// <summary>
        /// Resets pooled state before the chunk becomes active.
        /// </summary>
        public void OnSpawn()
        {
            ResolveRuntimeWiring(createFallbackTrail: true);
            transform.localScale = _defaultLocalScale;
            _remainingLifetime = 0f;
            if (chunkRigidbody != null)
            {
                chunkRigidbody.detectCollisions = true;
                chunkRigidbody.isKinematic = false;
                chunkRigidbody.linearVelocity = Vector3.zero;
                chunkRigidbody.angularVelocity = Vector3.zero;
                chunkRigidbody.linearDamping = _defaultLinearDamping;
                chunkRigidbody.angularDamping = _defaultAngularDamping;
                chunkRigidbody.collisionDetectionMode = _defaultCollisionDetectionMode;
                chunkRigidbody.interpolation = _defaultInterpolation;
                chunkRigidbody.Sleep();
            }

            _hasSnag = false;
            _cascadeImpactConsumed = false;
            _fragmentDepth = 0;
            _siltTrailSettled = false;
            _snagHangTimer = 0f;
            _remainingThermalIntegrity = Mathf.Max(0.01f, thermalIntegrity);
            _scavengerConsume01 = 0f;
            _disintegrating = false;
            DisableSnagJoints();
            UpdateConsumedScale();

            if (siltTrail != null)
            {
                UpdateSiltTrailEmission();
                siltTrail.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        /// <summary>
        /// Clears pooled state before the chunk returns to the inactive pool container.
        /// </summary>
        public void OnDespawn()
        {
            transform.localScale = _defaultLocalScale;
            _remainingLifetime = 0f;
            TryUnregister();
            if (chunkRigidbody != null)
            {
                chunkRigidbody.detectCollisions = true;
                chunkRigidbody.linearVelocity = Vector3.zero;
                chunkRigidbody.angularVelocity = Vector3.zero;
                chunkRigidbody.linearDamping = _defaultLinearDamping;
                chunkRigidbody.angularDamping = _defaultAngularDamping;
                chunkRigidbody.collisionDetectionMode = _defaultCollisionDetectionMode;
                chunkRigidbody.interpolation = _defaultInterpolation;
                chunkRigidbody.Sleep();
            }

            _hasSnag = false;
            _cascadeImpactConsumed = false;
            _fragmentDepth = 0;
            _siltTrailSettled = false;
            _snagHangTimer = 0f;
            _remainingThermalIntegrity = Mathf.Max(0.01f, thermalIntegrity);
            _scavengerConsume01 = 0f;
            _disintegrating = false;
            DisableSnagJoints();
            TryUnregisterScavengerHost();
            UpdateConsumedScale();

            if (siltTrail != null)
                siltTrail.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_hasSnag || collision == null || collision.contactCount <= 0 || chunkRigidbody == null)
                return;

            float impactSpeedSq = collision.relativeVelocity.sqrMagnitude;
            float snagImpactSpeedThresholdSq = snagImpactSpeedThreshold * snagImpactSpeedThreshold;
            if (impactSpeedSq < snagImpactSpeedThresholdSq)
                return;

            int collisionLayerMask = 1 << collision.collider.gameObject.layer;
            if ((snagLayers.value & collisionLayerMask) == 0)
                return;

            ContactPoint contact = collision.GetContact(0);
            bool useVoxelRockSpring = collision.collider.CompareTag("VoxelRock");
            TryConfigureSnag(contact.point, contact.normal, collision.rigidbody, useVoxelRockSpring);
            if (ShouldStopSiltTrail(contact.normal, impactSpeedSq))
                StopSiltTrailEmission(clearParticles: false);

            if (_cascadeImpactConsumed)
                return;

            SargassumGlobalDragManager dragManager = Hecton8.Core.GlobalRegistry.SargassumDrag;
            if (dragManager != null)
                dragManager.RegisterCollapseChunkImpact(contact.point, contact.normal, impactSpeedSq, _fragmentDepth + 1);

            _cascadeImpactConsumed = true;
        }

        private void OnDisable()
        {
            TryUnregisterScavengerHost();
            TryUnregister();
            HectonFloatingOrigin.UnregisterListener(this);
        }

        private void OnDestroy()
        {
            TryUnregisterScavengerHost();
            TryUnregister();
            HectonFloatingOrigin.UnregisterListener(this);
        }

        private void TryRegister()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (_registeredTick)
            {
                if (_registeredFixedTick)
                {
                    HectonFloatingOrigin.RegisterListener(this);
                    return;
                }
            }
            else
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
                _registeredTick = GlobalRegistry.Updatables.Contains(this);
            }

            if (_registeredFixedTick)
            {
                HectonFloatingOrigin.RegisterListener(this);
                return;
            }

            GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Environment);
            _registeredFixedTick = GlobalRegistry.FixedTickables.Contains(this);
            HectonFloatingOrigin.RegisterListener(this);
        }

        private void TryUnregister()
        {
            if (_registeredTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredTick = false;
            }

            if (!_registeredFixedTick)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                return;
            }

            GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
            _registeredFixedTick = false;
            HectonFloatingOrigin.UnregisterListener(this);
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            if (!isActiveAndEnabled || shiftOffset.sqrMagnitude <= 0.000001f)
                return;

            _snagConnectedAnchor -= shiftOffset;
            EnsureShiftBuffers();
            RebaseWorldSpaceParticles(siltTrail, _siltTrailShiftParticles, shiftOffset);
        }

        internal bool CanHostScavengers => gameObject.activeInHierarchy && _hasSnag && !_disintegrating;

        internal Vector3 GetScavengerAnchorWS()
        {
            return transform.position;
        }

        internal void ClearScavengerHostRegistration()
        {
            _registeredScavengerHost = false;
        }

        internal void ApplyScavengerConsumptionDelta(float delta01)
        {
            if (delta01 <= 0f || !CanHostScavengers)
                return;

            _scavengerConsume01 = Mathf.Clamp01(_scavengerConsume01 + delta01);
            UpdateConsumedScale();
            if (_scavengerConsume01 >= 0.999f)
                DisintegrateIntoScrap();
        }

        private void ResolveRuntimeWiring(bool createFallbackTrail)
        {
            ResolveExistingComponentReferences();

            if (siltTrail == null && createFallbackTrail)
                siltTrail = CreateFallbackSiltTrail();
        }

        private bool ResolveExistingComponentReferences()
        {
            bool changed = false;
            if (chunkRigidbody == null && TryGetComponent(out chunkRigidbody))
                changed = true;

            if (siltTrail == null)
            {
                ParticleSystem resolvedTrail = ComponentReferenceUtility.ResolveOwnedComponent<ParticleSystem>(transform);
                if (resolvedTrail != null)
                {
                    siltTrail = resolvedTrail;
                    changed = true;
                }
            }

            return changed;
        }

        internal void ApplyThermalGeyserDamage(float damage)
        {
            if (damage <= 0f || !gameObject.activeInHierarchy)
                return;

            _remainingThermalIntegrity -= damage;
            if (_remainingThermalIntegrity > 0f)
                return;

            DisintegrateIntoScrap();
        }

        private void EnsureSnagJoints()
        {
            DisableSnagJoints();
        }

        private void DisableSnagJoints()
        {
            _snagConnectedBody = null;
            _snagLocalAnchor = Vector3.zero;
            _snagConnectedAnchor = Vector3.zero;
            _snagUseSpringOnly = false;
        }

        private void TryConfigureSnag(Vector3 contactPointWS, Vector3 contactNormalWS, Rigidbody preferredBody, bool useVoxelRockSpring)
        {
            Rigidbody connectedBody = preferredBody;
            Vector3 safeNormal = ResolveSafeDirection(contactNormalWS, Vector3.up);
            Vector3 connectedAnchorWS = contactPointWS + safeNormal * snagSurfaceOffset;

            int hitCount = UnityEngine.Physics.OverlapSphereNonAlloc(
                contactPointWS,
                snagSearchRadius,
                _snagColliders,
                snagLayers,
                QueryTriggerInteraction.Ignore);

            float nearestDistanceSq = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                Collider candidate = _snagColliders[i];
                if (candidate == null || candidate.attachedRigidbody == chunkRigidbody)
                    continue;

                Vector3 candidatePoint = candidate.ClosestPoint(contactPointWS);
                float distanceSq = (candidatePoint - contactPointWS).sqrMagnitude;
                if (distanceSq >= nearestDistanceSq)
                    continue;

                nearestDistanceSq = distanceSq;
                connectedAnchorWS = candidate.attachedRigidbody != null
                    ? candidatePoint
                    : candidatePoint + safeNormal * snagSurfaceOffset;
                connectedBody = candidate.attachedRigidbody;
            }

            Vector3 localAnchor = transform.InverseTransformPoint(contactPointWS);
            Vector3 connectedAnchor = connectedBody != null
                ? connectedBody.transform.InverseTransformPoint(connectedAnchorWS)
                : connectedAnchorWS;

            _snagConnectedBody = connectedBody;
            _snagLocalAnchor = localAnchor;
            _snagConnectedAnchor = connectedAnchor;
            _snagUseSpringOnly = useVoxelRockSpring;
            _hasSnag = true;
            _siltTrailSettled = true;
            TryRegisterScavengerHost();
        }

        private void ApplySnagConstraint(float fixedDeltaTime)
        {
            Vector3 connectedAnchorWS = _snagConnectedBody != null
                ? _snagConnectedBody.transform.TransformPoint(_snagConnectedAnchor)
                : _snagConnectedAnchor;
            Vector3 localAnchorWS = transform.TransformPoint(_snagLocalAnchor);
            Vector3 separation = localAnchorWS - connectedAnchorWS;
            float distanceSq = separation.sqrMagnitude;
            if (distanceSq <= 0.00000001f)
                return;

            float maxDistance = math.max(0f, snagMaxDistance);
            float maxDistanceSq = maxDistance * maxDistance;
            if (distanceSq <= maxDistanceSq)
                return;

            Vector3 directionAwayFromAnchor = separation * math.rsqrt(distanceSq);
            float extension = FastMagnitudeApprox(separation) - maxDistance;
            if (extension <= 0f)
                return;

            Vector3 connectedVelocity = _snagConnectedBody != null
                ? _snagConnectedBody.GetPointVelocity(connectedAnchorWS)
                : Vector3.zero;
            Vector3 chunkVelocity = chunkRigidbody.GetPointVelocity(localAnchorWS);
            float separationSpeed = Vector3.Dot(chunkVelocity - connectedVelocity, directionAwayFromAnchor);
            float requestedAcceleration = (extension * snagSpring) + (separationSpeed * snagDamper);
            if (requestedAcceleration <= 0f)
                return;

            PhysicsForceRouter.QueueForce(
                chunkRigidbody,
                -directionAwayFromAnchor * requestedAcceleration,
                ForceMode.Acceleration);

            if (_snagUseSpringOnly)
                return;

            Vector3 angularVelocity = chunkRigidbody.angularVelocity;
            float angularBlend = 1f / (1f + snagDamper * fixedDeltaTime);
            chunkRigidbody.angularVelocity = angularVelocity * angularBlend;
        }

        private void UpdateSiltTrailEmission()
        {
            if (siltTrail == null)
                return;

            float downwardSpeed = chunkRigidbody != null ? Mathf.Max(0f, -chunkRigidbody.linearVelocity.y) : 0f;
            if (_siltTrailSettled || downwardSpeed <= siltTrailStopSpeed)
            {
                ParticleSystem.EmissionModule settledEmission = siltTrail.emission;
                settledEmission.rateOverTime = 0f;
                if (siltTrail.isPlaying)
                    siltTrail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                return;
            }

            float speed01 = Mathf.Clamp01(downwardSpeed / Mathf.Max(0.1f, siltTrailFullSpeed));
            ParticleSystem.EmissionModule emission = siltTrail.emission;
            emission.rateOverTime = LerpClamped(siltTrailBaseRate, siltTrailMaxRate, speed01);
            if (!siltTrail.isPlaying)
                siltTrail.Play(true);
        }

        private bool ShouldStopSiltTrail(Vector3 contactNormalWS, float impactSpeedSq)
        {
            if (impactSpeedSq <= 0.00000001f)
                return false;

            if (_hasSnag)
                return true;

            return contactNormalWS.y >= 0.35f;
        }

        private void StopSiltTrailEmission(bool clearParticles)
        {
            _siltTrailSettled = true;
            if (siltTrail == null)
                return;

            ParticleSystem.EmissionModule emission = siltTrail.emission;
            emission.rateOverTime = 0f;
            siltTrail.Stop(true, clearParticles
                ? ParticleSystemStopBehavior.StopEmittingAndClear
                : ParticleSystemStopBehavior.StopEmitting);
        }

        private ParticleSystem CreateFallbackSiltTrail()
        {
            // COLD ALLOC: GameObject[1] Ã¢â‚¬â€ fallback collapse-chunk muddy trail child created when prefab wiring is missing Ã¢â‚¬â€ owner: SargassumCollapseChunk
            GameObject trailObject = new GameObject("SiltTrail");
            trailObject.transform.SetParent(transform, false);
            trailObject.transform.localPosition = Vector3.zero;
            trailObject.transform.localRotation = Quaternion.identity;
            trailObject.transform.localScale = Vector3.one;

            // COLD ALLOC: ParticleSystem[1] Ã¢â‚¬â€ fallback muddy trail component for collapse chunks Ã¢â‚¬â€ owner: SargassumCollapseChunk
            ParticleSystem particleSystem = trailObject.AddComponent<ParticleSystem>();
            // COLD ALLOC: ParticleSystemRenderer[1] Ã¢â‚¬â€ fallback muddy trail renderer for collapse chunks Ã¢â‚¬â€ owner: SargassumCollapseChunk
            ParticleSystemRenderer particleRenderer = trailObject.GetComponent<ParticleSystemRenderer>();

            ParticleSystem.MainModule main = particleSystem.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 4f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.18f, 0.65f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.62f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.24f, 0.22f, 0.18f, 0.55f), new Color(0.11f, 0.1f, 0.09f, 0.0f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 192;
            main.gravityModifier = 0f;
            main.stopAction = ParticleSystemStopAction.None;

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = siltTrailBaseRate;

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.28f;
            shape.radiusThickness = 1f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient colorGradient = new Gradient();
            colorGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.22f, 0.2f, 0.17f), 0f),
                    new GradientColorKey(new Color(0.14f, 0.13f, 0.12f), 0.55f),
                    new GradientColorKey(new Color(0.09f, 0.09f, 0.09f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.52f, 0f),
                    new GradientAlphaKey(0.34f, 0.42f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(colorGradient);

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.65f),
                new Keyframe(0.4f, 1f),
                new Keyframe(1f, 1.85f));
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            ParticleSystem.LimitVelocityOverLifetimeModule limitVelocity = particleSystem.limitVelocityOverLifetime;
            limitVelocity.enabled = true;
            limitVelocity.limit = 0.55f;
            limitVelocity.dampen = 0.38f;

            ParticleSystem.NoiseModule noise = particleSystem.noise;
            noise.enabled = true;
            noise.separateAxes = false;
            noise.strength = 0.42f;
            noise.frequency = 0.34f;
            noise.scrollSpeed = 0.16f;

            if (particleRenderer != null)
            {
                particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                particleRenderer.sortMode = ParticleSystemSortMode.Distance;
                particleRenderer.minParticleSize = 0.015f;
                particleRenderer.maxParticleSize = 0.16f;
                particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
                particleRenderer.receiveShadows = false;
                particleRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            }

            return particleSystem;
        }

        private void EnsureShiftBuffers()
        {
            int particleCapacity = 192;
            if (siltTrail != null)
                particleCapacity = Mathf.Max(1, siltTrail.main.maxParticles);

            if (_siltTrailShiftParticles == null || _siltTrailShiftParticles.Length < particleCapacity)
                _siltTrailShiftParticles = new ParticleSystem.Particle[particleCapacity];
        }

        private static void RebaseWorldSpaceParticles(ParticleSystem particleSystem, ParticleSystem.Particle[] buffer, Vector3 shiftOffset)
        {
            if (particleSystem == null || buffer == null)
                return;

            ParticleSystem.MainModule main = particleSystem.main;
            if (main.simulationSpace != ParticleSystemSimulationSpace.World)
                return;

            int particleCount = particleSystem.GetParticles(buffer);
            for (int i = 0; i < particleCount; i++)
                buffer[i].position -= shiftOffset;

            particleSystem.SetParticles(buffer, particleCount);
        }

        private void UpdateConsumedScale()
        {
            float scaleMultiplier = LerpClamped(1f, 0.26f, _scavengerConsume01);
            transform.localScale = _defaultLocalScale * Mathf.Max(0.1f, scaleMultiplier);
        }

        private void TryRegisterScavengerHost()
        {
            if (_registeredScavengerHost)
                return;

            SargassumGlobalDragManager dragManager = Hecton8.Core.GlobalRegistry.SargassumDrag;
            if (dragManager == null)
                return;

            _registeredScavengerHost = dragManager.RegisterSettledCollapseChunk(this);
        }

        private void TryUnregisterScavengerHost()
        {
            if (!_registeredScavengerHost)
                return;

            SargassumGlobalDragManager dragManager = Hecton8.Core.GlobalRegistry.SargassumDrag;
            if (dragManager != null)
                dragManager.UnregisterSettledCollapseChunk(this);

            _registeredScavengerHost = false;
        }

        private void DisintegrateIntoScrap()
        {
            if (_disintegrating)
                return;

            _disintegrating = true;
            TryUnregisterScavengerHost();
            ObjectPoolManager poolManager = GlobalRegistry.ObjectPool;
            if (poolManager != null && scrapPickupPrefab != null)
            {
                Vector3 origin = transform.position;
                for (int i = 0; i < scrapPickupCount && i < ScrapEjectDirections.Length; i++)
                {
                    GameObject scrap = poolManager.Spawn(scrapPickupPrefab, origin + (ScrapEjectDirections[i] * 0.18f), Quaternion.identity);
                    if (scrap == null || !scrap.TryGetComponent(out Rigidbody scrapRigidbody))
                        continue;

                    scrapRigidbody.linearVelocity = ResolveSafeDirection(ScrapEjectDirections[i], Vector3.up) * scrapEjectSpeed;
                }
            }

            if (poolManager != null)
                poolManager.Despawn(gameObject);
        }

        private static Vector3 ResolveSafeDirection(Vector3 value, Vector3 fallback)
        {
            float sqrMagnitude = value.sqrMagnitude;
            return sqrMagnitude > 0.000001f
                ? value * math.rsqrt(sqrMagnitude)
                : fallback;
        }

        private static float FastMagnitudeApprox(Vector3 value)
        {
            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            float az = math.abs(value.z);
            float max = math.max(ax, math.max(ay, az));
            float min = math.min(ax, math.min(ay, az));
            float mid = ax + ay + az - max - min;
            return max + (mid * 0.41421356f) + (min * 0.29289322f);
        }

        private static float LerpClamped(float from, float to, float t)
        {
            return math.lerp(from, to, math.saturate(t));
        }

#if UNITY_EDITOR
        [ContextMenu("Author Missing Fallback Silt Trail")]
        private void AuthorMissingFallbackSiltTrail()
        {
            if (Application.isPlaying)
                return;

            bool changed = ResolveExistingComponentReferences();
            if (siltTrail == null)
            {
                siltTrail = CreateFallbackSiltTrail();
                changed = siltTrail != null;
            }

            if (scrapPickupPrefab == null)
            {
                scrapPickupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScrapPickupPrefabAssetPath);
                changed |= scrapPickupPrefab != null;
            }

            if (!changed)
                return;

            EditorUtility.SetDirty(this);
            EditorUtility.SetDirty(gameObject);
            if (siltTrail != null)
                EditorUtility.SetDirty(siltTrail.gameObject);

            if (PrefabUtility.IsPartOfPrefabInstance(gameObject))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(this);
                PrefabUtility.RecordPrefabInstancePropertyModifications(gameObject);
                if (siltTrail != null)
                    PrefabUtility.RecordPrefabInstancePropertyModifications(siltTrail.gameObject);
            }
        }

        private void OnValidate()
        {
            siltTrailBaseRate = Mathf.Clamp(siltTrailBaseRate, 0f, 96f);
            siltTrailMaxRate = Mathf.Clamp(siltTrailMaxRate, siltTrailBaseRate, 160f);
            siltTrailFullSpeed = Mathf.Clamp(siltTrailFullSpeed, 0.1f, 8f);
            siltTrailStopSpeed = Mathf.Clamp(siltTrailStopSpeed, 0.01f, 1f);
            snagSearchRadius = Mathf.Clamp(snagSearchRadius, 0.1f, 4f);
            snagImpactSpeedThreshold = Mathf.Clamp(snagImpactSpeedThreshold, 0.1f, 12f);
            snagSpring = Mathf.Clamp(snagSpring, 0.1f, 120f);
            snagDamper = Mathf.Clamp(snagDamper, 0f, 16f);
            snagMaxDistance = Mathf.Clamp(snagMaxDistance, 0f, 2f);
            snagSurfaceOffset = Mathf.Clamp(snagSurfaceOffset, 0.01f, 0.5f);
            snagDisintegrationDelay = Mathf.Clamp(snagDisintegrationDelay, 30f, 90f);
            scrapPickupCount = Mathf.Clamp(scrapPickupCount, 1, ScrapEjectDirections.Length);
            thermalIntegrity = Mathf.Clamp(thermalIntegrity, 0f, 16f);
            scrapEjectSpeed = Mathf.Clamp(scrapEjectSpeed, 0f, 8f);

            if (!Application.isPlaying)
            {
                bool changed = ResolveExistingComponentReferences();
                if (scrapPickupPrefab == null)
                {
                    scrapPickupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScrapPickupPrefabAssetPath);
                    changed |= scrapPickupPrefab != null;
                }

                if (changed)
                    EditorUtility.SetDirty(this);
            }
        }
#endif
    }
}
