namespace Hecton8.Gameplay
{
    using Hecton8.AI;
    using Hecton8.Bootstrap;
    using Hecton8.Core;
    using Hecton8.Interaction;
    using Hecton8.Physics;
    using Hecton8.World;
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    /// Pooled emergency world-body for handheld Manta bailouts.
    /// Keeps the dropped scooter physical for a short sink/drift window, then returns it to the pool.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MantaEmergencyWreck : MonoBehaviour, IPoolable, IFixedTickable
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
            public float collisionDamageCooldownTimer;
            public bool isResident;
            public bool isDehydrated;
        }

        [DisallowMultipleComponent]
        [DefaultExecutionOrder(-4900)]
        private sealed class ResidencyRuntime : MonoBehaviour, IUpdatable
        {
            private bool _registered;
            private float _playerResolveCooldown;
            private Transform _playerTransform;

            private void OnEnable()
            {
                TryRegister();
            }

            private void OnDisable()
            {
                TryUnregister();
            }

            private void OnDestroy()
            {
                if (ReferenceEquals(s_residencyRuntime, this))
                    s_residencyRuntime = null;

                TryUnregister();
            }

            public void Tick(float deltaTime)
            {
                if (!Application.isPlaying || s_activeDehydratedResidencySlotCount <= 0)
                    return;

                if (!TryResolvePlayerTransform(deltaTime, out Transform playerTransform))
                    return;

                ObjectPoolManager poolManager = GlobalRegistry.ObjectPool;
                if (poolManager == null)
                    return;

                AbsoluteUniversePosition playerAup = AbsoluteUniversePosition.FromRuntimePosition(playerTransform.position);
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

                    if (!wreckInstance.TryGetComponent(out MantaEmergencyWreck wreck))
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

                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
                _registered = GlobalRegistry.Updatables.Contains(this);
            }

            private void TryUnregister()
            {
                if (!_registered)
                    return;

                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registered = false;
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
                if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform resolvedTransform) && resolvedTransform != null)
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

        [Header("-- Collision Damage --------------")]
        [Tooltip("Minimum collision speed where the drifting wreck starts dealing catastrophic kinetic damage to fauna.")]
        [SerializeField, Range(0f, 60f)] private float collisionDamageStartSpeed = 15f;

        [Tooltip("Collision speed where the drifting wreck reaches maximum kinetic damage.")]
        [SerializeField, Range(0f, 80f)] private float collisionDamageMaxSpeed = 32f;

        [Tooltip("Maximum damage applied when the emergency wreck slams into a fauna target at full authored speed.")]
        [SerializeField, Range(0f, 500f)] private float collisionDamageAtMaxSpeed = 260f;

        [Tooltip("Cooldown preventing the same wreck body from reapplying catastrophic collision damage every single contact frame.")]
        [SerializeField, Range(0f, 1f)] private float collisionDamageCooldown = 0.18f;

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

        private Rigidbody _rigidbody;
        private PickupItem _pickupItem;
        private InteractionHighlighter _interactionHighlighter;
        private bool _registeredFixedTick;
        private bool _emergencyActive;
        private bool _preserveResidencyOnDespawn;
        private float _remainingLifetime;
        private float _collisionDamageCooldownTimer;
        private float _dehydrationCheckTimer;
        private GameObject _residencyPrefabSource;
        private int _residencySlotIndex = InvalidResidencySlotIndex;

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
            _rigidbody.linearVelocity = ResolveSafeVelocity(launchVelocity, linearVelocityCap);

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
            _rigidbody.angularVelocity = ResolveSafeVelocity(angularVelocity, angularVelocityCap);

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
            CachePassiveReferences();
            ResetToIdlePickupState(releaseResidencySlot: true);
        }

        /// <inheritdoc />
        public void OnDespawn()
        {
            bool releaseResidencySlot = !_preserveResidencyOnDespawn;
            _preserveResidencyOnDespawn = false;
            ResetToIdlePickupState(releaseResidencySlot);
        }

        /// <inheritdoc />
        public void FixedTick(float fixedDeltaTime)
        {
            if (!_emergencyActive)
                return;

            if (_collisionDamageCooldownTimer > 0f)
            {
                _collisionDamageCooldownTimer -= math.max(0f, fixedDeltaTime);
                if (_collisionDamageCooldownTimer < 0f)
                    _collisionDamageCooldownTimer = 0f;
            }

            _remainingLifetime -= math.max(0f, fixedDeltaTime);
            if (_remainingLifetime <= 0f)
            {
                _remainingLifetime = 0f;
                DespawnSelf(preserveResidencySlot: false);
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
            PhysicsForceRouter.QueueForce(
                _rigidbody,
                Vector3.down * sinkVelocityChangePerSecond * fixedDeltaTime,
                ForceMode.VelocityChange);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!_emergencyActive || _collisionDamageCooldownTimer > 0f || collision == null)
                return;

            float impactSpeedSq = collision.relativeVelocity.sqrMagnitude;
            float collisionDamageStartSpeedSq = collisionDamageStartSpeed * collisionDamageStartSpeed;
            if (impactSpeedSq <= collisionDamageStartSpeedSq)
                return;

            Collider hitCollider = collision.collider;
            if (hitCollider == null)
                return;

            FaunaBrain faunaBrain = hitCollider.GetComponent<FaunaBrain>();
            if (faunaBrain == null)
                faunaBrain = hitCollider.GetComponentInParent<FaunaBrain>();

            if (faunaBrain == null)
                return;

            float impactSpeed = impactSpeedSq * math.rsqrt(impactSpeedSq);
            float maxSpeed = math.max(collisionDamageStartSpeed + 0.01f, collisionDamageMaxSpeed);
            float inverseDamageRange = 1f / math.max(0.0001f, maxSpeed - collisionDamageStartSpeed);
            float damageT = math.saturate((impactSpeed - collisionDamageStartSpeed) * inverseDamageRange);
            float damage = collisionDamageAtMaxSpeed * damageT;
            if (damage <= 0f)
                return;

            faunaBrain.TakeDamage(damage);
            _collisionDamageCooldownTimer = collisionDamageCooldown;
        }

        private void CachePassiveReferences()
        {
            if (_pickupItem == null)
                TryGetComponent(out _pickupItem);

            if (_interactionHighlighter == null)
                TryGetComponent(out _interactionHighlighter);

            if (_rigidbody == null)
                TryGetComponent(out _rigidbody);
        }

        private void EnsureRigidbody()
        {
            if (_rigidbody != null)
                return;

            _rigidbody = gameObject.AddComponent<Rigidbody>();
            _rigidbody.useGravity = false;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        private void ResetToIdlePickupState(bool releaseResidencySlot)
        {
            _emergencyActive = false;
            _remainingLifetime = 0f;
            _collisionDamageCooldownTimer = 0f;
            _dehydrationCheckTimer = 0f;
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

            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            _rigidbody.linearDamping = idleLinearDamping;
            _rigidbody.angularDamping = idleAngularDamping;
            _rigidbody.useGravity = false;
            _rigidbody.isKinematic = true;
        }

        private void DespawnSelf(bool preserveResidencySlot)
        {
            _preserveResidencyOnDespawn = preserveResidencySlot;
            ObjectPoolManager poolManager = GlobalRegistry.ObjectPool;
            if (poolManager != null)
            {
                poolManager.Despawn(gameObject);
                return;
            }

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

            GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Environment);
            _registeredFixedTick = GlobalRegistry.FixedTickables.Contains(this);
        }

        private void TryUnregisterFixedTick()
        {
            if (!_registeredFixedTick)
                return;

            GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
            _registeredFixedTick = false;
        }

        private void HydrateFromResidency(int slotIndex, in ResidencyState state, Vector3 runtimePosition)
        {
            CachePassiveReferences();
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
            _collisionDamageCooldownTimer = math.max(0f, state.collisionDamageCooldownTimer);
            _dehydrationCheckTimer = DehydrationCheckIntervalSeconds;
            _preserveResidencyOnDespawn = false;

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
            _rigidbody.linearVelocity = ResolveSafeVelocity(state.linearVelocity, ResolveLinearVelocityCap());
            _rigidbody.angularVelocity = ResolveSafeVelocity(state.angularVelocity, ResolveAngularVelocityCap());
            _rigidbody.WakeUp();

            UpdateResidencyState(markDehydrated: false);
            TryRegisterFixedTick();
        }

        private bool TryDehydrateDistantWreck()
        {
            if (!_emergencyActive ||
                !IsValidResidencySlot(_residencySlotIndex) ||
                _residencyPrefabSource == null ||
                !SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) ||
                playerTransform == null)
            {
                return false;
            }

            AbsoluteUniversePosition wreckAup = AbsoluteUniversePosition.FromRuntimePosition(transform.position);
            AbsoluteUniversePosition playerAup = AbsoluteUniversePosition.FromRuntimePosition(playerTransform.position);
            if (AbsoluteUniversePosition.DistanceSq(in wreckAup, in playerAup) <= DehydrationDistanceSq)
                return false;

            UpdateResidencyState(markDehydrated: true);
            AddActiveDehydratedResidencySlot(_residencySlotIndex);
            _residencySlotIndex = InvalidResidencySlotIndex;
            DespawnSelf(preserveResidencySlot: true);
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

            PoolSlotData slotData = s_residencySlots[_residencySlotIndex];
            AbsoluteUniversePosition positionAup = AbsoluteUniversePosition.FromRuntimePosition(transform.position);
            slotData.BoundGuid = unchecked((ulong)(_residencySlotIndex + 1));
            slotData.AupCell = new int3((int)positionAup.GridX, (int)positionAup.GridY, (int)positionAup.GridZ);
            slotData.LocalOffset = new float3(positionAup.LocalX, positionAup.LocalY, positionAup.LocalZ);
            slotData.HydrationFrame = unchecked((ushort)Time.frameCount);
            slotData.RefCount = 1;
            slotData.StateFlags = markDehydrated
                ? (byte)(PoolSlotStateFlags.Reserved | PoolSlotStateFlags.Dirty)
                : (byte)(PoolSlotStateFlags.Reserved | PoolSlotStateFlags.Hydrated);
            slotData.LastVisibleFrame = unchecked((ushort)Time.frameCount);
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
                collisionDamageCooldownTimer = _collisionDamageCooldownTimer,
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
            float authoredCap = math.max(collisionDamageMaxSpeed, sinkVelocityChangePerSecond * bailoutLifetime);
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

        private static AbsoluteUniversePosition ReadPoolSlotPosition(PoolSlotData slotData)
        {
            return new AbsoluteUniversePosition
            {
                GridX = slotData.AupCell.x,
                GridY = slotData.AupCell.y,
                GridZ = slotData.AupCell.z,
                LocalX = slotData.LocalOffset.x,
                LocalY = slotData.LocalOffset.y,
                LocalZ = slotData.LocalOffset.z
            };
        }
    }
}
