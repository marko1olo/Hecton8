using Hecton8.Core;
using UnityEngine;
using UnityEngine.Rendering;
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
    public sealed class SargassumCollapseChunk : MonoBehaviour, ITickable, IFixedTickable, ILateFrameTickable, IPoolable, IOriginShiftListener, IGlobalRegistryHotSwapListener
    {
        private const string ScrapPickupPrefabAssetPath = "Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_TitaniumScrap.prefab";
        private static readonly Vector3[] ScrapEjectDirections =
        {
            new Vector3(0.22f, 1f, 0.12f),
            new Vector3(-0.28f, 1f, 0.06f),
            new Vector3(0.09f, 1f, -0.25f),
            new Vector3(-0.12f, 1f, -0.18f)
        };
        [Header("Runtime Wiring")]
        [SerializeField]
        [Tooltip("Cached rigidbody used to drive the falling chunk.")]
        private Rigidbody chunkRigidbody;

        [SerializeField]
        [Tooltip("Optional looping particle trail emitted while the chunk sinks.")]
        private ParticleSystem siltTrail;

        [SerializeField]
        [Tooltip("Optional pooled silt trail prefab used when the chunk prefab has no authored child trail.")]
        private GameObject authoredSiltTrailPrefab;

        [SerializeField]
        [Tooltip("Pooled physical scrap pickup prefab spawned when the chunk disintegrates into salvage.")]
        private GameObject scrapPickupPrefab;

        [Header("Defaults")]
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

        [Header("Disintegration")]
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

        [Header("Snag Constraint")]
        [SerializeField, Range(0.1f, 120f)]
        [Tooltip("Spring used by the hanging debris joint once the chunk snags into surrounding geometry.")]
        private float snagSpring = 28f;

        [SerializeField, Range(0f, 16f)]
        [Tooltip("Damper applied to the hanging debris spring to keep the joint heavy instead of rubbery.")]
        private float snagDamper = 4.2f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Maximum free distance preserved by the hanging spring before the chunk starts pulling taut.")]
        private float snagMaxDistance = 0.45f;

        private Vector3 _defaultLocalScale = Vector3.one;
        private float _defaultLinearDamping;
        private float _defaultAngularDamping;
        private CollisionDetectionMode _defaultCollisionDetectionMode;
        private RigidbodyInterpolation _defaultInterpolation;
        private float _remainingLifetime;
        private bool _registeredTick;
        private bool _hasSnag;
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
        private bool _registeredLateFrameTick;
        private bool _pendingPoolDespawn;
        private bool _pendingScrapDisintegration;
        private bool _hotSwapRegistered;
        private bool _siltTrailVisualDirty;
        private bool _pendingSiltTrailPlay;
        private bool _pendingSiltTrailClear;
        private float _pendingSiltTrailEmissionRate;
        private GameObject _pooledSiltTrailInstance;
        private IObjectPoolService _objectPool;
        private SargassumGlobalDragManager _sargassumDrag;
        private IPhysicsService _physicsService;
        // COLD ALLOC: ParticleSystem.Particle[192] - reusable world-space silt particle shift buffer - owner: SargassumCollapseChunk
        private ParticleSystem.Particle[] _siltTrailShiftParticles;

        private void Awake()
        {
            ResolveRuntimeWiring();
            CacheRegistryServicesCold();
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
                IPhysicsService physicsService = _physicsService;
                physicsService?.QueueLinearVelocitySet(chunkRigidbody, linearVelocityWS);
                physicsService?.QueueAngularVelocitySet(chunkRigidbody, angularVelocityWS);
            }

            transform.localScale = _defaultLocalScale * Mathf.Max(0.1f, uniformScale);
            EnsurePooledSiltTrailActive();

            if (siltTrail != null)
            {
                UpdateSiltTrailEmission();
                QueueSiltTrailVisualSync(_pendingSiltTrailEmissionRate, play: true, clearParticles: true);
            }

            _remainingLifetime = despawnDelay > 0f ? despawnDelay : defaultLifetime;
            _fragmentDepth = Mathf.Max(0, fragmentDepth);
            _hasSnag = false;
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

            _pendingPoolDespawn = true;
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

        public void LateFrameTick()
        {
            FlushSiltTrailVisualSync();
            if (_pendingScrapDisintegration)
            {
                _pendingScrapDisintegration = false;
                ExecuteDisintegrationPoolCommands();
                return;
            }

            if (_pendingPoolDespawn)
            {
                _pendingPoolDespawn = false;
                IObjectPoolService poolManager = _objectPool;
                if (poolManager != null)
                    poolManager.Despawn(gameObject);
            }
        }

        /// <summary>
        /// Resets pooled state before the chunk becomes active.
        /// </summary>
        public void OnSpawn()
        {
            ResolveRuntimeWiring();
            CacheRegistryServicesCold();
            transform.localScale = _defaultLocalScale;
            _remainingLifetime = 0f;
            if (chunkRigidbody != null)
            {
                chunkRigidbody.detectCollisions = true;
                chunkRigidbody.isKinematic = false;
                IPhysicsService physicsService = _physicsService;
                physicsService?.QueueLinearVelocitySet(chunkRigidbody, Vector3.zero, wake: false);
                physicsService?.QueueAngularVelocitySet(chunkRigidbody, Vector3.zero, wake: false);
                chunkRigidbody.linearDamping = _defaultLinearDamping;
                chunkRigidbody.angularDamping = _defaultAngularDamping;
                chunkRigidbody.collisionDetectionMode = _defaultCollisionDetectionMode;
                chunkRigidbody.interpolation = _defaultInterpolation;
                chunkRigidbody.Sleep();
            }

            _hasSnag = false;
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
                IPhysicsService physicsService = _physicsService;
                physicsService?.QueueLinearVelocitySet(chunkRigidbody, Vector3.zero, wake: false);
                physicsService?.QueueAngularVelocitySet(chunkRigidbody, Vector3.zero, wake: false);
                chunkRigidbody.linearDamping = _defaultLinearDamping;
                chunkRigidbody.angularDamping = _defaultAngularDamping;
                chunkRigidbody.collisionDetectionMode = _defaultCollisionDetectionMode;
                chunkRigidbody.interpolation = _defaultInterpolation;
                chunkRigidbody.Sleep();
            }

            _hasSnag = false;
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

            ReleasePooledSiltTrail(clearParticles: true);
        }

        private void OnDisable()
        {
            ReleasePooledSiltTrail(clearParticles: true);
            TryUnregisterScavengerHost();
            TryUnregister();
            TryUnregisterHotSwapListener();
            HectonFloatingOrigin.UnregisterListener(this);
        }

        private void OnDestroy()
        {
            ReleasePooledSiltTrail(clearParticles: true);
            TryUnregisterScavengerHost();
            TryUnregister();
            TryUnregisterHotSwapListener();
            HectonFloatingOrigin.UnregisterListener(this);
        }

        private void TryRegister()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();

            if (_registeredTick)
            {
                if (_registeredFixedTick)
                {
                    if (!_registeredLateFrameTick)
                        _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
                    HectonFloatingOrigin.RegisterListener(this);
                    return;
                }
            }
            else
            {
                _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            }

            if (_registeredFixedTick)
            {
                HectonFloatingOrigin.RegisterListener(this);
                if (!_registeredLateFrameTick)
                    _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
                return;
            }

            _registeredFixedTick = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Environment);
            if (!_registeredLateFrameTick)
                _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            HectonFloatingOrigin.RegisterListener(this);
        }

        private void TryUnregister()
        {
            if (_registeredTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredTick = false;
            }

            if (_registeredLateFrameTick)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrameTick = false;
            }

            if (!_registeredFixedTick)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                TryUnregisterHotSwapListener();
                return;
            }

            GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
            _registeredFixedTick = false;
            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregisterHotSwapListener();
        }

        private void TryUnregisterDispatcherTicks()
        {
            if (_registeredTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredTick = false;
            }

            if (_registeredFixedTick)
            {
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
                _registeredFixedTick = false;
            }

            if (_registeredLateFrameTick)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrameTick = false;
            }
        }

        private void CacheRegistryServicesCold()
        {
            _objectPool = GlobalRegistry.ObjectPoolService;
            _sargassumDrag = SargassumGlobalDragManager.Instance;
            _physicsService = GlobalRegistry.Physics;
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

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.ObjectPool:
                    _objectPool = currentService as IObjectPoolService;
                    break;
                case GlobalRegistryServiceSlot.SargassumDragRuntime:
                    if (_registeredScavengerHost && previousService is SargassumGlobalDragManager previousDrag)
                        previousDrag.UnregisterSettledCollapseChunk(this);

                    _sargassumDrag = currentService as SargassumGlobalDragManager;
                    _registeredScavengerHost = false;
                    if (CanHostScavengers)
                        TryRegisterScavengerHost();
                    break;
                case GlobalRegistryServiceSlot.Physics:
                    _physicsService = currentService as IPhysicsService;
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregisterDispatcherTicks();
                    if (currentService != null && isActiveAndEnabled && _remainingLifetime > 0f)
                        TryRegister();
                    break;
            }
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

        private void ResolveRuntimeWiring()
        {
            ResolveExistingComponentReferences();
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

        private void EnsurePooledSiltTrailActive()
        {
            if (siltTrail != null || authoredSiltTrailPrefab == null)
                return;

            IObjectPoolService poolManager = _objectPool;
            if (poolManager == null)
                return;

            GameObject instance = poolManager.Spawn(authoredSiltTrailPrefab, transform.position, transform.rotation, allowExpand: false);
            if (instance == null)
                return;

            if (!poolManager.TryGetPooledComponent(instance, out ParticleSystem pooledTrail) || pooledTrail == null)
            {
                poolManager.Despawn(instance);
                return;
            }

            Transform trailTransform = instance.transform;
            trailTransform.SetParent(transform, false);
            trailTransform.localPosition = Vector3.zero;
            trailTransform.localRotation = Quaternion.identity;
            trailTransform.localScale = Vector3.one;
            _pooledSiltTrailInstance = instance;
            siltTrail = pooledTrail;
            EnsureShiftBuffers();
        }

        private void ReleasePooledSiltTrail(bool clearParticles)
        {
            GameObject instance = _pooledSiltTrailInstance;
            if (instance == null)
                return;

            ParticleSystem pooledTrail = siltTrail;
            if (pooledTrail != null && (pooledTrail.isPlaying || clearParticles))
            {
                pooledTrail.Stop(true, clearParticles
                    ? ParticleSystemStopBehavior.StopEmittingAndClear
                    : ParticleSystemStopBehavior.StopEmitting);
            }

            _pooledSiltTrailInstance = null;
            siltTrail = null;

            IObjectPoolService poolManager = _objectPool;
            if (poolManager != null)
                poolManager.Despawn(instance);
            else
                instance.SetActive(false);
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

            IPhysicsService physicsService = _physicsService;
            if (physicsService == null)
                return;

            physicsService.QueueForce(
                chunkRigidbody,
                -directionAwayFromAnchor * requestedAcceleration,
                ForceMode.Acceleration);

            if (_snagUseSpringOnly)
                return;

            Vector3 angularVelocity = chunkRigidbody.angularVelocity;
            float angularBlend = 1f / (1f + snagDamper * fixedDeltaTime);
            physicsService.QueueAngularVelocitySet(chunkRigidbody, angularVelocity * angularBlend);
        }

        private void UpdateSiltTrailEmission()
        {
            if (siltTrail == null)
                return;

            float downwardSpeed = chunkRigidbody != null ? Mathf.Max(0f, -chunkRigidbody.linearVelocity.y) : 0f;
            if (_siltTrailSettled || downwardSpeed <= siltTrailStopSpeed)
            {
                QueueSiltTrailVisualSync(0f, play: false, clearParticles: false);
                return;
            }

            float speed01 = Mathf.Clamp01(downwardSpeed / Mathf.Max(0.1f, siltTrailFullSpeed));
            QueueSiltTrailVisualSync(
                LerpClamped(siltTrailBaseRate, siltTrailMaxRate, speed01) * FrameTimeWatchdog.ParticleEmissionScale,
                play: true,
                clearParticles: false);
        }

        private void StopSiltTrailEmission(bool clearParticles)
        {
            _siltTrailSettled = true;
            if (siltTrail == null)
                return;

            QueueSiltTrailVisualSync(0f, play: false, clearParticles: clearParticles);
        }

        private void QueueSiltTrailVisualSync(float emissionRate, bool play, bool clearParticles)
        {
            _pendingSiltTrailEmissionRate = Mathf.Max(0f, emissionRate);
            _pendingSiltTrailPlay = play;
            _pendingSiltTrailClear |= clearParticles;
            _siltTrailVisualDirty = true;
        }

        private void FlushSiltTrailVisualSync()
        {
            if (!_siltTrailVisualDirty || siltTrail == null)
                return;

            _siltTrailVisualDirty = false;
            ParticleSystem.EmissionModule emission = siltTrail.emission;
            emission.rateOverTime = _pendingSiltTrailEmissionRate;
            bool clearParticles = _pendingSiltTrailClear;
            _pendingSiltTrailClear = false;

            if (_pendingSiltTrailPlay)
            {
                if (clearParticles)
                    siltTrail.Clear(true);
                if (!siltTrail.isPlaying)
                    siltTrail.Play(true);
                return;
            }

            if (siltTrail.isPlaying || clearParticles)
            {
                siltTrail.Stop(true, clearParticles
                    ? ParticleSystemStopBehavior.StopEmittingAndClear
                    : ParticleSystemStopBehavior.StopEmitting);
            }
        }

#if UNITY_EDITOR
        private ParticleSystem CreateEditorAuthoredSiltTrail()
        {
            // EDITOR ALLOC: GameObject[1] - authored collapse-chunk muddy trail child created before build - owner: SargassumCollapseChunk
            GameObject trailObject = new GameObject("SiltTrail");
            trailObject.transform.SetParent(transform, false);
            trailObject.transform.localPosition = Vector3.zero;
            trailObject.transform.localRotation = Quaternion.identity;
            trailObject.transform.localScale = Vector3.one;

            // EDITOR ALLOC: ParticleSystem[1] - authored muddy trail component for collapse chunks - owner: SargassumCollapseChunk
            ParticleSystem particleSystem = trailObject.AddComponent<ParticleSystem>();
            // EDITOR ALLOC: ParticleSystemRenderer[1] - authored muddy trail renderer for collapse chunks - owner: SargassumCollapseChunk
            trailObject.TryGetComponent(out ParticleSystemRenderer particleRenderer);

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
            emission.rateOverTime = siltTrailBaseRate * FrameTimeWatchdog.ParticleEmissionScale;

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
#endif

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

            SargassumGlobalDragManager dragManager = _sargassumDrag;
            if (dragManager == null)
                return;

            _registeredScavengerHost = dragManager.RegisterSettledCollapseChunk(this);
        }

        private void TryUnregisterScavengerHost()
        {
            if (!_registeredScavengerHost)
                return;

            SargassumGlobalDragManager dragManager = _sargassumDrag;
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
            _pendingScrapDisintegration = true;
        }

        private void ExecuteDisintegrationPoolCommands()
        {
            IObjectPoolService poolManager = _objectPool;
            if (poolManager != null && scrapPickupPrefab != null)
            {
                Vector3 origin = transform.position;
                for (int i = 0; i < scrapPickupCount && i < ScrapEjectDirections.Length; i++)
                {
                    GameObject scrap = poolManager.Spawn(scrapPickupPrefab, origin + (ScrapEjectDirections[i] * 0.18f), Quaternion.identity);
                    if (scrap == null || !poolManager.TryGetPooledRootRigidbody(scrap, out Rigidbody scrapRigidbody))
                        continue;

                    _physicsService?.QueueLinearVelocitySet(
                        scrapRigidbody,
                        ResolveSafeDirection(ScrapEjectDirections[i], Vector3.up) * scrapEjectSpeed);
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
        [ContextMenu("Author Missing Silt Trail")]
        private void AuthorMissingSiltTrail()
        {
            if (Application.isPlaying)
                return;

            bool changed = ResolveExistingComponentReferences();
            if (siltTrail == null)
            {
                siltTrail = CreateEditorAuthoredSiltTrail();
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
            snagSpring = Mathf.Clamp(snagSpring, 0.1f, 120f);
            snagDamper = Mathf.Clamp(snagDamper, 0f, 16f);
            snagMaxDistance = Mathf.Clamp(snagMaxDistance, 0f, 2f);
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
