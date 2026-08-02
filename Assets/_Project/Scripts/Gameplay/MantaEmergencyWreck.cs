namespace Hecton8.Gameplay
{
    using Hecton8.AI;
    using Hecton8.Bootstrap;
    using Hecton8.Core;
    using Hecton8.Interaction;
    using Hecton8.World;
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    /// Pooled emergency world-body for handheld Manta bailouts.
    /// Keeps the dropped scooter physical for a short sink/drift window, then returns it to the pool.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MantaEmergencyWreck : MonoBehaviour, IPoolable, IFixedTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const float DehydrationDistanceMeters = 160f;
        private const double DehydrationDistanceSq = DehydrationDistanceMeters * DehydrationDistanceMeters;
        private const float DehydrationCheckIntervalSeconds = 0.25f;
        private const float PlayerResolveRetrySeconds = 1f;
        private const int MaxResidencySlots = 16;
        private const int InvalidResidencySlotIndex = -1;
        private const float VelocityClampSafetyMultiplier = 1.5f;

        private struct ResidencyState
        {
            public GameObject prefabSource;
            public Quaternion rotation;
            public Vector3 linearVelocity;
            public Vector3 angularVelocity;
            public float remainingLifetime;
            public bool isResident;
            public bool isDehydrated;
        }

        [DisallowMultipleComponent]
        [DefaultExecutionOrder(-4900)]
        private sealed class ResidencyRuntime : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
        {
            private bool _registered;
            private bool _hotSwapListenerRegistered;
            private bool _runtimeActive;
            private float _playerResolveCooldown;
            private Transform _playerTransform;

            private void OnEnable()
            {
                _runtimeActive = Application.isPlaying;
                CacheRegistryServicesCold();
                TryRegister();
                TryRegisterHotSwapListener();
            }

            private void OnDisable()
            {
                TryUnregisterHotSwapListener();
                TryUnregister();
                _runtimeActive = false;
            }

            private void OnDestroy()
            {
                if (ReferenceEquals(s_residencyRuntime, this))
                    s_residencyRuntime = null;

                TryUnregisterHotSwapListener();
                TryUnregister();
            }

            public void LateFrameTick()
            {
                if (!_runtimeActive || s_activeDehydratedResidencySlotCount <= 0)
                    return;

                float deltaTime = math.max(0f, SystemDispatcher.CurrentFrameDeltaTime);
                if (!TryResolveCurrentPlayerAup(out AbsoluteUniversePosition playerAup))
                    return;

                if (!MantaEmergencyWreck.TryResolveCachedObjectPool(out IObjectPoolService poolManager))
                    return;

                for (int i = s_activeDehydratedResidencySlotCount - 1; i >= 0; i--)
                {
                    int slotIndex = s_activeDehydratedResidencySlots[i];
                    if (!IsValidResidencySlot(slotIndex))
                    {
                        RemoveActiveDehydratedResidencySlotAt(i);
                        continue;
                    }

                    ResidencyState state = s_residencyStates[slotIndex];
                    if (!state.isResident || !state.isDehydrated || state.prefabSource == null)
                    {
                        ReleaseResidencySlot(slotIndex);
                        continue;
                    }

                    state.remainingLifetime -= math.max(0f, deltaTime);
                    if (state.remainingLifetime <= 0f)
                    {
                        ReleaseResidencySlot(slotIndex);
                        continue;
                    }

                    s_residencyStates[slotIndex] = state;

                    AbsoluteUniversePosition wreckAup = ReadPoolSlotPosition(s_residencySlots[slotIndex]);
                    if (AbsoluteUniversePosition.DistanceSq(in wreckAup, in playerAup) > DehydrationDistanceSq)
                        continue;

                    Vector3 runtimePosition = wreckAup.ToRuntimeFloat3();
                    GameObject wreckInstance = poolManager.Spawn(state.prefabSource, runtimePosition, state.rotation);
                    if (wreckInstance == null)
                        continue;

                    if (!TryResolveLastSpawnedWreck(wreckInstance, out MantaEmergencyWreck wreck))
                    {
                        poolManager.Despawn(wreckInstance);
                        ReleaseResidencySlot(slotIndex);
                        continue;
                    }

                    RemoveActiveDehydratedResidencySlotAt(i);
                    wreck.HydrateFromResidency(slotIndex, in state, runtimePosition);
                }
            }

            private void TryRegister()
            {
                if (_registered)
                    return;
                if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                    return;

                _registered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            }

            private void TryUnregister()
            {
                if (!_registered)
                    return;

                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registered = false;
            }

            public void OnGlobalRegistryServiceReplaced(
                GlobalRegistryServiceSlot serviceSlot,
                object previousService,
                object currentService)
            {
                if (serviceSlot == GlobalRegistryServiceSlot.ObjectPool)
                    MantaEmergencyWreck.CacheObjectPoolService(currentService as ObjectPoolManager);
            }

            private void TryRegisterHotSwapListener()
            {
                if (_hotSwapListenerRegistered || !Application.isPlaying)
                    return;

                _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
            }

            private void TryUnregisterHotSwapListener()
            {
                if (!_hotSwapListenerRegistered)
                    return;

                GlobalRegistry.TryUnregisterHotSwapListener(this);
                _hotSwapListenerRegistered = false;
            }

            private static void CacheRegistryServicesCold()
            {
                MantaEmergencyWreck.CacheObjectPoolService(null);
            }

            private bool TryResolvePlayerTransform(float deltaTime, out Transform playerTransform)
            {
                if (_playerTransform != null && _playerTransform.gameObject.activeInHierarchy)
                {
                    playerTransform = _playerTransform;
                    return true;
                }

                _playerResolveCooldown -= math.max(0f, deltaTime);
                if (_playerResolveCooldown > 0f)
                {
                    playerTransform = null;
                    return false;
                }

                _playerResolveCooldown = PlayerResolveRetrySeconds;
                if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform resolvedTransform) && resolvedTransform != null)
                {
                    _playerTransform = resolvedTransform;
                    playerTransform = resolvedTransform;
                    return true;
                }

                _playerTransform = null;
                playerTransform = null;
                return false;
            }
        }

        [Header("-- Bailout Drift ----------------")]
        [Tooltip("How long the detached Manta wreck remains in the world before returning to the pool.")]
        [SerializeField, Range(2f, 60f)] private float bailoutLifetime = 18f;

        [Tooltip("Continuous downward velocity-change applied while the detached wreck sinks.")]
        [SerializeField, Range(0f, 8f)] private float sinkVelocityChangePerSecond = 1.35f;

        [Tooltip("Linear damping used while the detached wreck coasts away from the player.")]
        [SerializeField, Range(0f, 8f)] private float activeLinearDamping = 0.18f;

        [Tooltip("Angular damping used while the detached wreck tumbles into the abyss.")]
        [SerializeField, Range(0f, 8f)] private float activeAngularDamping = 0.52f;

        [Tooltip("Minimum angular spin applied on bailout.")]
        [SerializeField, Range(0f, 12f)] private float spinVelocityMin = 1.35f;

        [Tooltip("Maximum angular spin applied on bailout.")]
        [SerializeField, Range(0f, 24f)] private float spinVelocityMax = 4.6f;

        [Tooltip("Authored velocity cap used when persisting or restoring a drifting emergency wreck.")]
        [SerializeField, Range(0f, 80f)] private float bailoutVelocityCapMaxSpeed = 32f;


        [Header("-- Idle Reset --------------------")]
        [Tooltip("Linear damping used when the wreck object returns to idle pooled pickup behavior.")]
        [SerializeField, Range(0f, 16f)] private float idleLinearDamping = 8f;

        [Tooltip("Angular damping used when the wreck object returns to idle pooled pickup behavior.")]
        [SerializeField, Range(0f, 16f)] private float idleAngularDamping = 8f;

        private static PoolSlotData[] s_residencySlots;
        private static ResidencyState[] s_residencyStates;
        private static int[] s_freeResidencySlots;
        private static int[] s_activeDehydratedResidencySlots;
        private static int s_freeResidencySlotCount;
        private static int s_activeDehydratedResidencySlotCount;
        private static ResidencyRuntime s_residencyRuntime;
        private static IObjectPoolService s_cachedObjectPool;
        private static MantaEmergencyWreck s_lastSpawnedWreck;

        private Rigidbody _rigidbody;
        private PickupItem _pickupItem;
        private InteractionHighlighter _interactionHighlighter;
        private bool _registeredFixedTick;
        private bool _registeredLateFrame;
        private bool _hotSwapListenerRegistered;
        private bool _emergencyActive;
        private bool _preserveResidencyOnDespawn;
        private bool _selfDeactivateQueued;
        private float _remainingLifetime;
        private float _dehydrationCheckTimer;
        private GameObject _residencyPrefabSource;
        private int _residencySlotIndex = InvalidResidencySlotIndex;
        private AbsoluteUniversePosition _currentAup;
        private Vector3 _currentRuntimePosition;
        private bool _hasCurrentAup;
        private IPhysicsService _physicsService;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetResidencyStatics()
        {
            s_residencySlots = null;
            s_residencyStates = null;
            s_freeResidencySlots = null;
            s_activeDehydratedResidencySlots = null;
            s_freeResidencySlotCount = 0;
            s_activeDehydratedResidencySlotCount = 0;
            s_residencyRuntime = null;
            s_cachedObjectPool = null;
            s_lastSpawnedWreck = null;
        }

        private static void CacheObjectPoolService(ObjectPoolManager candidate)
        {
            ObjectPoolManager pool = candidate;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(pool) ||
                ObjectPoolManager.TryResolveActiveRuntime(ref pool))
            {
                s_cachedObjectPool = pool;
                return;
            }

            s_cachedObjectPool = null;
        }

        private static bool TryResolveCachedObjectPool(out IObjectPoolService pool)
        {
            ObjectPoolManager cached = s_cachedObjectPool as ObjectPoolManager;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached))
            {
                pool = cached;
                return true;
            }

            ObjectPoolManager resolved = cached;
            if (ObjectPoolManager.TryResolveActiveRuntime(ref resolved))
            {
                s_cachedObjectPool = resolved;
                pool = resolved;
                return true;
            }

            s_cachedObjectPool = null;
            pool = null;
            return false;
        }

        /// <summary>
        /// Arms the pooled wreck body with inherited bailout inertia and enables short-lived physics drift.
        /// </summary>
        /// <param name="inheritedVelocity">Player/body velocity at the instant of bailout.</param>
        /// <param name="bailoutImpulse">Controller-resolved bailout impulse.</param>
        /// <param name="severity">Normalized crash severity.</param>
        public void ActivateEmergencyDrift(Vector3 inheritedVelocity, Vector3 bailoutImpulse, float severity)
        {
            CachePassiveReferences();
            EnsureRigidbody();
            if (_rigidbody == null)
                return;

            EnsureResidencyRuntime();
            EnsureResidencySlotAllocated();

            float clampedSeverity = math.saturate(severity);
            _emergencyActive = true;
            _remainingLifetime = math.max(0.05f, bailoutLifetime);
            _dehydrationCheckTimer = DehydrationCheckIntervalSeconds;
            _currentRuntimePosition = ResolveCurrentRuntimePosition();
            _hasCurrentAup = TryResolveAupFromPlayerObserver(_currentRuntimePosition, out _currentAup);

            if (_pickupItem != null)
                _pickupItem.enabled = false;

            if (_interactionHighlighter != null)
                _interactionHighlighter.enabled = false;

            _rigidbody.isKinematic = false;
            _rigidbody.useGravity = false;
            _rigidbody.linearDamping = activeLinearDamping;
            _rigidbody.angularDamping = activeAngularDamping;
            _rigidbody.WakeUp();

            float linearVelocityCap = ResolveLinearVelocityCap();
            float angularVelocityCap = ResolveAngularVelocityCap();
            Vector3 launchVelocity = inheritedVelocity * math.lerp(0.94f, 1.08f, clampedSeverity) +
                                     bailoutImpulse * math.lerp(0.12f, 0.28f, clampedSeverity);
            launchVelocity.y -= math.lerp(0.05f, 0.45f, clampedSeverity);
            IPhysicsService physicsService = _physicsService;
            physicsService?.QueueLinearVelocitySet(_rigidbody, ResolveSafeVelocity(launchVelocity, linearVelocityCap));

            float bailoutImpulseSq = bailoutImpulse.sqrMagnitude;
            Vector3 spinAxis = bailoutImpulseSq > 0.0001f
                ? Vector3.Cross(Vector3.up, bailoutImpulse * math.rsqrt(bailoutImpulseSq))
                : transform.right;
            float spinAxisSq = spinAxis.sqrMagnitude;
            if (spinAxisSq <= 0.0001f)
            {
                spinAxis = transform.forward;
                spinAxisSq = spinAxis.sqrMagnitude;
            }

            float spinSign = math.sign(Vector3.Dot(bailoutImpulse, transform.right));
            if (spinSign == 0f)
                spinSign = 1f;

            Vector3 normalizedSpinAxis = spinAxisSq > 0.0001f
                ? spinAxis * math.rsqrt(spinAxisSq)
                : Vector3.forward;
            Vector3 angularVelocity = normalizedSpinAxis *
                                      (spinSign * math.lerp(spinVelocityMin, spinVelocityMax, clampedSeverity));
            physicsService?.QueueAngularVelocitySet(_rigidbody, ResolveSafeVelocity(angularVelocity, angularVelocityCap));

            UpdateResidencyState(markDehydrated: false);
            TryRegisterFixedTick();
        }

        internal void BindResidencyPrefabSource(GameObject prefabSource)
        {
            _residencyPrefabSource = prefabSource;
        }

        /// <inheritdoc />
        public void OnSpawn()
        {
            s_lastSpawnedWreck = this;
            CachePassiveReferences();
            ResetToIdlePickupState(releaseResidencySlot: true);
        }

        /// <inheritdoc />
        public void OnDespawn()
        {
            if (ReferenceEquals(s_lastSpawnedWreck, this))
                s_lastSpawnedWreck = null;

            bool releaseResidencySlot = !_preserveResidencyOnDespawn;
            _preserveResidencyOnDespawn = false;
            ResetToIdlePickupState(releaseResidencySlot);
        }

        /// <inheritdoc />
        public void FixedTick(float fixedDeltaTime)
        {
            if (!_emergencyActive)
                return;

            _remainingLifetime -= math.max(0f, fixedDeltaTime);
            if (_remainingLifetime <= 0f)
            {
                _remainingLifetime = 0f;
                QueueSelfDespawn(preserveResidencySlot: false);
                return;
            }

            if (_rigidbody == null)
                return;

            _dehydrationCheckTimer -= math.max(0f, fixedDeltaTime);
            if (_dehydrationCheckTimer <= 0f)
            {
                _dehydrationCheckTimer = DehydrationCheckIntervalSeconds;
                if (TryDehydrateDistantWreck())
                    return;
            }

            UpdateResidencyState(markDehydrated: false);
            _physicsService?.QueueForce(
                _rigidbody,
                Vector3.down * sinkVelocityChangePerSecond * fixedDeltaTime,
                ForceMode.VelocityChange);
        }

        private static Vector3 NormalizeOrForward(Vector3 direction)
        {
            if (!IsFiniteVector(direction))
                return Vector3.forward;

            float sqrMagnitude = direction.sqrMagnitude;
            if (sqrMagnitude <= 0.0001f)
                return Vector3.forward;

            float invMagnitude = math.rsqrt(sqrMagnitude);
            return direction * invMagnitude;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }

        private void CachePassiveReferences()
        {
            if (_pickupItem == null)
                TryGetComponent(out _pickupItem);

            if (_interactionHighlighter == null)
                TryGetComponent(out _interactionHighlighter);

            if (_rigidbody == null)
                TryGetComponent(out _rigidbody);

            if (_physicsService == null)
                _physicsService = GlobalRegistry.Physics;
        }

        private void CachePassiveServicesOnly()
        {
            if (_physicsService == null)
                _physicsService = GlobalRegistry.Physics;
        }

        private void EnsureRigidbody()
        {
            if (_rigidbody != null)
                return;

            // Player-build construction path: no authored/bootstrap instance reachable.
            // Must construct in player builds when bootstrap reorders or skips registration.
            _rigidbody = gameObject.AddComponent<Rigidbody>();
            _rigidbody.useGravity = false;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        private void ResetToIdlePickupState(bool releaseResidencySlot)
        {
            _emergencyActive = false;
            _remainingLifetime = 0f;
            _dehydrationCheckTimer = 0f;
            _currentAup = default;
            _currentRuntimePosition = default;
            _hasCurrentAup = false;
            TryUnregisterFixedTick();
            if (releaseResidencySlot)
                ReleaseAssignedResidencySlot();

            _preserveResidencyOnDespawn = false;
            _residencyPrefabSource = null;

            if (_pickupItem != null)
                _pickupItem.enabled = true;

            if (_interactionHighlighter != null)
                _interactionHighlighter.enabled = true;

            if (_rigidbody == null)
                return;

            IPhysicsService physicsService = _physicsService;
            physicsService?.QueueLinearVelocitySet(_rigidbody, Vector3.zero, wake: false);
            physicsService?.QueueAngularVelocitySet(_rigidbody, Vector3.zero, wake: false);
            _rigidbody.linearDamping = idleLinearDamping;
            _rigidbody.angularDamping = idleAngularDamping;
            _rigidbody.useGravity = false;
            _rigidbody.isKinematic = true;
        }

        private void QueueSelfDespawn(bool preserveResidencySlot)
        {
            _preserveResidencyOnDespawn = preserveResidencySlot;
            _selfDeactivateQueued = true;
        }

        private static bool TryResolveLastSpawnedWreck(GameObject instance, out MantaEmergencyWreck wreck)
        {
            wreck = s_lastSpawnedWreck;
            return instance != null &&
                   wreck != null &&
                   ReferenceEquals(wreck.gameObject, instance);
        }

        public void LateFrameTick()
        {
            if (!_selfDeactivateQueued)
                return;

            _selfDeactivateQueued = false;
            if (TryResolveCachedObjectPool(out IObjectPoolService poolManager))
            {
                poolManager.Despawn(gameObject);
                return;
            }

            bool preserveResidencySlot = _preserveResidencyOnDespawn;
            ResetToIdlePickupState(releaseResidencySlot: !preserveResidencySlot);
            _preserveResidencyOnDespawn = false;
            gameObject.SetActive(false);
        }

        private void TryRegisterFixedTick()
        {
            if (_registeredFixedTick)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredFixedTick = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Environment);
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            if (_registeredFixedTick || _registeredLateFrame)
                TryRegisterHotSwapListener();
        }

        private void TryUnregisterFixedTick()
        {
            if (_registeredLateFrame && !_selfDeactivateQueued)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }

            if (_registeredFixedTick)
            {
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
                _registeredFixedTick = false;
            }

            if (!_registeredFixedTick && !_registeredLateFrame)
                TryUnregisterHotSwapListener();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Physics)
            {
                _physicsService = currentService as IPhysicsService;
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
            {
                return;
            }

            bool needsFixed = _registeredFixedTick || _emergencyActive;
            bool needsLate = _registeredLateFrame || _selfDeactivateQueued;
            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }

            if (_registeredFixedTick)
            {
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
                _registeredFixedTick = false;
            }

            if (currentService == null || !isActiveAndEnabled)
                return;

            if (needsFixed)
            {
                TryRegisterFixedTick();
            }
            else if (needsLate)
            {
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
                if (_registeredLateFrame)
                    TryRegisterHotSwapListener();
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private void HydrateFromResidency(int slotIndex, in ResidencyState state, Vector3 runtimePosition)
        {
            CachePassiveServicesOnly();
            EnsureRigidbody();
            if (_rigidbody == null)
            {
                ReleaseResidencySlot(slotIndex);
                return;
            }

            _residencySlotIndex = slotIndex;
            _residencyPrefabSource = state.prefabSource;
            _emergencyActive = true;
            _remainingLifetime = math.max(0.05f, state.remainingLifetime);
            _dehydrationCheckTimer = DehydrationCheckIntervalSeconds;
            _preserveResidencyOnDespawn = false;
            _currentAup = ReadPoolSlotPosition(s_residencySlots[slotIndex]);
            _currentRuntimePosition = runtimePosition;
            _hasCurrentAup = _currentAup.IsFinite();

            transform.SetPositionAndRotation(runtimePosition, state.rotation);

            if (_pickupItem != null)
                _pickupItem.enabled = false;

            if (_interactionHighlighter != null)
                _interactionHighlighter.enabled = false;

            _rigidbody.isKinematic = false;
            _rigidbody.useGravity = false;
            _rigidbody.linearDamping = activeLinearDamping;
            _rigidbody.angularDamping = activeAngularDamping;
            _rigidbody.position = runtimePosition;
            _rigidbody.rotation = state.rotation;
            IPhysicsService physicsService = _physicsService;
            physicsService?.QueueLinearVelocitySet(_rigidbody, ResolveSafeVelocity(state.linearVelocity, ResolveLinearVelocityCap()));
            physicsService?.QueueAngularVelocitySet(_rigidbody, ResolveSafeVelocity(state.angularVelocity, ResolveAngularVelocityCap()));
            _rigidbody.WakeUp();

            UpdateResidencyState(markDehydrated: false);
            TryRegisterFixedTick();
        }

        private bool TryDehydrateDistantWreck()
        {
            if (!_emergencyActive ||
                !IsValidResidencySlot(_residencySlotIndex) ||
                _residencyPrefabSource == null ||
                !TryResolveCurrentPlayerAup(out AbsoluteUniversePosition playerAup))
            {
                return false;
            }

            if (!TryResolveActiveWreckAup(out AbsoluteUniversePosition wreckAup))
                return false;

            if (AbsoluteUniversePosition.DistanceSq(in wreckAup, in playerAup) <= DehydrationDistanceSq)
                return false;

            UpdateResidencyState(markDehydrated: true);
            AddActiveDehydratedResidencySlot(_residencySlotIndex);
            _residencySlotIndex = InvalidResidencySlotIndex;
            QueueSelfDespawn(preserveResidencySlot: true);
            return true;
        }

        private void EnsureResidencySlotAllocated()
        {
            if (IsValidResidencySlot(_residencySlotIndex))
                return;

            EnsureResidencyStateInitialized();
            if (s_freeResidencySlotCount <= 0)
                return;

            s_freeResidencySlotCount--;
            _residencySlotIndex = s_freeResidencySlots[s_freeResidencySlotCount];
            s_freeResidencySlots[s_freeResidencySlotCount] = 0;
        }

        private void ReleaseAssignedResidencySlot()
        {
            if (!IsValidResidencySlot(_residencySlotIndex))
                return;

            ReleaseResidencySlot(_residencySlotIndex);
            _residencySlotIndex = InvalidResidencySlotIndex;
        }

        private void UpdateResidencyState(bool markDehydrated)
        {
            if (!IsValidResidencySlot(_residencySlotIndex) || _residencyPrefabSource == null)
                return;

            EnsureResidencyStateInitialized();

            if (!TryResolveActiveWreckAup(out AbsoluteUniversePosition positionAup))
                return;

            PoolSlotData slotData = s_residencySlots[_residencySlotIndex];
            slotData.BoundGuid = unchecked((ulong)(_residencySlotIndex + 1));
            slotData.GridX = positionAup.GridX;
            slotData.GridY = positionAup.GridY;
            slotData.GridZ = positionAup.GridZ;
            slotData.LocalOffset = new float3(positionAup.LocalX, positionAup.LocalY, positionAup.LocalZ);
            slotData.HydrationFrame = unchecked((ushort)SystemDispatcher.CurrentFrameIndex);
            slotData.RefCount = 1;
            slotData.StateFlags = markDehydrated
                ? (byte)(PoolSlotStateFlags.Reserved | PoolSlotStateFlags.Dirty)
                : (byte)(PoolSlotStateFlags.Reserved | PoolSlotStateFlags.Hydrated);
            slotData.LastVisibleFrame = unchecked((ushort)SystemDispatcher.CurrentFrameIndex);
            s_residencySlots[_residencySlotIndex] = slotData;

            Vector3 linearVelocity = Vector3.zero;
            Vector3 angularVelocity = Vector3.zero;
            if (_rigidbody != null)
            {
                linearVelocity = ResolveSafeVelocity(_rigidbody.linearVelocity, ResolveLinearVelocityCap());
                angularVelocity = ResolveSafeVelocity(_rigidbody.angularVelocity, ResolveAngularVelocityCap());
            }

            s_residencyStates[_residencySlotIndex] = new ResidencyState
            {
                prefabSource = _residencyPrefabSource,
                rotation = transform.rotation,
                linearVelocity = linearVelocity,
                angularVelocity = angularVelocity,
                remainingLifetime = _remainingLifetime,
                isResident = true,
                isDehydrated = markDehydrated
            };
        }

        private static void EnsureResidencyStateInitialized()
        {
            if (s_residencySlots != null &&
                s_residencyStates != null &&
                s_freeResidencySlots != null &&
                s_activeDehydratedResidencySlots != null)
            {
                return;
            }

            // COLD ALLOC: PoolSlotData[16] - emergency wreck dehydration slot metadata - owner: MantaEmergencyWreck
            s_residencySlots = new PoolSlotData[MaxResidencySlots];
            // COLD ALLOC: ResidencyState[16] - emergency wreck dehydration restore state - owner: MantaEmergencyWreck
            s_residencyStates = new ResidencyState[MaxResidencySlots];
            // COLD ALLOC: int[16] - emergency wreck free residency slot stack - owner: MantaEmergencyWreck
            s_freeResidencySlots = new int[MaxResidencySlots];
            // COLD ALLOC: int[16] - emergency wreck active dehydrated slot list - owner: MantaEmergencyWreck
            s_activeDehydratedResidencySlots = new int[MaxResidencySlots];

            s_freeResidencySlotCount = MaxResidencySlots;
            s_activeDehydratedResidencySlotCount = 0;
            for (int i = 0; i < MaxResidencySlots; i++)
                s_freeResidencySlots[i] = MaxResidencySlots - 1 - i;
        }

        private float ResolveLinearVelocityCap()
        {
            float authoredCap = math.max(bailoutVelocityCapMaxSpeed, sinkVelocityChangePerSecond * bailoutLifetime);
            authoredCap *= VelocityClampSafetyMultiplier;
            return float.IsFinite(authoredCap) ? math.max(1f, authoredCap) : 1f;
        }

        private float ResolveAngularVelocityCap()
        {
            float authoredCap = spinVelocityMax * VelocityClampSafetyMultiplier;
            return float.IsFinite(authoredCap) ? math.max(1f, authoredCap) : 1f;
        }

        private static Vector3 ResolveSafeVelocity(Vector3 velocity, float maxMagnitude)
        {
            if (!IsFinite(velocity))
                return Vector3.zero;

            float speedSq = velocity.sqrMagnitude;
            if (!float.IsFinite(speedSq) || speedSq <= 0.000001f)
                return Vector3.zero;

            float safeMax = math.max(0.01f, maxMagnitude);
            float maxSq = safeMax * safeMax;
            return speedSq <= maxSq
                ? velocity
                : velocity * (safeMax * math.rsqrt(speedSq));
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private static void EnsureResidencyRuntime()
        {
            EnsureResidencyStateInitialized();
            if (s_residencyRuntime != null)
                return;

            GameObject runtimeRoot = new GameObject("[MantaEmergencyWreckResidencyRuntime]"); // COLD ALLOC: GameObject[1] - emergency wreck dehydration runtime owner - owner: MantaEmergencyWreck
            s_residencyRuntime = runtimeRoot.AddComponent<ResidencyRuntime>();
            if (Application.isPlaying)
                GameBootstrapper.PersistRuntimeService(s_residencyRuntime);
        }

        private static void ReleaseResidencySlot(int slotIndex)
        {
            if (!IsValidResidencySlot(slotIndex) || s_residencyStates == null || s_freeResidencySlots == null)
                return;

            if (!s_residencyStates[slotIndex].isResident)
                return;

            RemoveActiveDehydratedResidencySlot(slotIndex);
            s_residencyStates[slotIndex] = default;
            s_residencySlots[slotIndex] = default;

            if (s_freeResidencySlotCount < s_freeResidencySlots.Length)
            {
                s_freeResidencySlots[s_freeResidencySlotCount] = slotIndex;
                s_freeResidencySlotCount++;
            }
        }

        private static void AddActiveDehydratedResidencySlot(int slotIndex)
        {
            if (!IsValidResidencySlot(slotIndex) || s_activeDehydratedResidencySlots == null)
                return;

            for (int i = 0; i < s_activeDehydratedResidencySlotCount; i++)
            {
                if (s_activeDehydratedResidencySlots[i] == slotIndex)
                    return;
            }

            if (s_activeDehydratedResidencySlotCount >= s_activeDehydratedResidencySlots.Length)
            {
                ReleaseResidencySlot(slotIndex);
                return;
            }

            s_activeDehydratedResidencySlots[s_activeDehydratedResidencySlotCount] = slotIndex;
            s_activeDehydratedResidencySlotCount++;
        }

        private static void RemoveActiveDehydratedResidencySlot(int slotIndex)
        {
            if (!IsValidResidencySlot(slotIndex) || s_activeDehydratedResidencySlots == null)
                return;

            for (int i = 0; i < s_activeDehydratedResidencySlotCount; i++)
            {
                if (s_activeDehydratedResidencySlots[i] != slotIndex)
                    continue;

                RemoveActiveDehydratedResidencySlotAt(i);
                return;
            }
        }

        private static void RemoveActiveDehydratedResidencySlotAt(int index)
        {
            if (s_activeDehydratedResidencySlots == null || index < 0 || index >= s_activeDehydratedResidencySlotCount)
                return;

            int lastIndex = s_activeDehydratedResidencySlotCount - 1;
            s_activeDehydratedResidencySlots[index] = s_activeDehydratedResidencySlots[lastIndex];
            s_activeDehydratedResidencySlots[lastIndex] = 0;
            s_activeDehydratedResidencySlotCount = lastIndex;
        }

        private static bool IsValidResidencySlot(int slotIndex)
        {
            return slotIndex >= 0 && s_residencySlots != null && slotIndex < s_residencySlots.Length;
        }

        private static bool TryResolveCurrentPlayerAup(out AbsoluteUniversePosition playerAup)
        {
            playerAup = default;
            IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;
            if (runtimeContext == null)
            {
                return false;
            }

            if (runtimeContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                (snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
            {
                playerAup = snapshot.Aup;
                if (playerAup.IsFinite())
                    return true;
            }

            if (!runtimeContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) ||
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u)
            {
                return false;
            }

            playerAup = movementState.PredictedAup;
            if (playerAup.IsFinite())
                return true;

            return false;
        }

        private static AbsoluteUniversePosition ReadPoolSlotPosition(PoolSlotData slotData)
        {
            return new AbsoluteUniversePosition
            {
                GridX = slotData.GridX,
                GridY = slotData.GridY,
                GridZ = slotData.GridZ,
                LocalX = slotData.LocalOffset.x,
                LocalY = slotData.LocalOffset.y,
                LocalZ = slotData.LocalOffset.z
            };
        }

        private Vector3 ResolveCurrentRuntimePosition()
        {
            if (_rigidbody != null)
                return _rigidbody.position;

            return transform.position;
        }

        private bool TryResolveActiveWreckAup(out AbsoluteUniversePosition wreckAup)
        {
            wreckAup = default;
            Vector3 runtimePosition = ResolveCurrentRuntimePosition();
            if (!IsFinite(runtimePosition))
                return false;

            if (_hasCurrentAup &&
                IsFinite(_currentRuntimePosition) &&
                _currentAup.IsFinite() &&
                TryOffsetAupByRuntimeDelta(in _currentAup, _currentRuntimePosition, runtimePosition, out wreckAup))
            {
                _currentAup = wreckAup;
                _currentRuntimePosition = runtimePosition;
                return true;
            }

            if (!TryResolveAupFromPlayerObserver(runtimePosition, out wreckAup))
                return false;

            _currentAup = wreckAup;
            _currentRuntimePosition = runtimePosition;
            _hasCurrentAup = true;
            return true;
        }

        private static bool TryResolveAupFromPlayerObserver(
            Vector3 targetRuntimePosition,
            out AbsoluteUniversePosition targetAup)
        {
            targetAup = default;
            if (!IsFinite(targetRuntimePosition) ||
                !TryResolveCurrentPlayerAup(out AbsoluteUniversePosition playerAup))
            {
                return false;
            }

            float3 playerRuntime = playerAup.ToRuntimeFloat3();
            if (!math.all(math.isfinite(playerRuntime)))
                return false;

            return TryOffsetAupByRuntimeDelta(
                in playerAup,
                new Vector3(playerRuntime.x, playerRuntime.y, playerRuntime.z),
                targetRuntimePosition,
                out targetAup);
        }

        private static bool TryOffsetAupByRuntimeDelta(
            in AbsoluteUniversePosition referenceAup,
            Vector3 referenceRuntimePosition,
            Vector3 targetRuntimePosition,
            out AbsoluteUniversePosition targetAup)
        {
            targetAup = default;
            if (!referenceAup.IsFinite() ||
                !IsFinite(referenceRuntimePosition) ||
                !IsFinite(targetRuntimePosition))
            {
                return false;
            }

            double3 localDelta = new double3(
                (double)targetRuntimePosition.x - referenceRuntimePosition.x,
                (double)targetRuntimePosition.y - referenceRuntimePosition.y,
                (double)targetRuntimePosition.z - referenceRuntimePosition.z);
            targetAup = AbsoluteUniversePosition.OffsetMeters(in referenceAup, localDelta);
            return targetAup.IsFinite();
        }
    }
}
