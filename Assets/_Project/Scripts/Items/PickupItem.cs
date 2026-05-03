// ============================================================================
// HECTON-8 — PickupItem.cs
// Example IInteractable implementation showing all systems working together.
// ============================================================================

using Hecton8.Core;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Modding;
using Hecton8.Physics;
using Hecton8.Gameplay;

namespace Hecton8.Interaction
{
    using Hecton8.World;
    using UnityEngine;

    [RequireComponent(typeof(InteractionHighlighter))]
    [RequireComponent(typeof(Collider))]
    public class PickupItem : MonoBehaviour, IInteractable, ISlowTickable, IFixedTickable, IInventoryPickupSource, IInteractionVulnerabilitySource, IPhysicsImpactMaterialProvider
    {
        // COLD ALLOC: RegistryBucket<PickupItem>[4096] — authored/persistent pickup registry for world-state scans — owner: PickupItem
        private static readonly RegistryBucket<PickupItem> _worldStateRegistry = new RegistryBucket<PickupItem>(4096);
        internal static PickupItem ActiveRuntimeInstance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ActiveRuntimeInstance = null;
            _worldStateRegistry.Clear();
        }

        private const float LooseCurrentVelocityInfluence = 0.45f;
        private const float LooseCurrentSpinInfluence = 0.12f;
        private const float CurrentSimulationCullDistance = 100f;
        private const float CurrentSimulationCullDistanceSqr = CurrentSimulationCullDistance * CurrentSimulationCullDistance;
        private const float OverflowScatterImpulse = 2.5f;
        private const float OverflowScatterLiftImpulse = 1.2f;
        private const float OverflowScatterTorqueImpulse = 0.35f;
        private const float DeepSeaSeawaterDensityKgPerM3 = 1025f;
        private const float LooseItemUnderwaterLinearDamping = 1.6f;
        private const float LooseItemUnderwaterAngularDamping = 6.5f;
        private const float LooseItemBuoyancyAngularDragMultiplier = 2.75f;
        private const ushort DefaultQualityMilli = 1000;

        [Header("Item Configuration")]
        [SerializeField] private ItemData itemData;
        [SerializeField] private int quantity = 1;

        [Header("World State")]
        [Tooltip("Persist this authored world pickup into WorldStateManager depletion storage.")]
        [SerializeField] private bool persistWorldState = true;

        private InteractionHighlighter _highlighter;
        private Rigidbody _rigidbody;
        private BuoyancyObject _buoyancy;
        private Collider _collider;
        private PhysicsMaterial _defaultColliderMaterial;
        private float _defaultLinearDamping;
        private float _defaultAngularDamping;
        private HectonPlayerMovement _playerMovement;
        private string _cachedInteractText;
        private int _cachedItemHashId;
        private int _spatialHandle;
        private int _faunaSpatialHandle;
        private bool _registeredToSlowTick;
        private bool _registeredToFixedTick;
        private Vector3 _lastSpatialPosition;
        private bool _worldStateIdentityResolved;
        private bool _worldStateIdentityAvailable;
        private long _worldStatePersistenceKey;
        private long _worldStateChunkKey;
        private Vector3 _worldStateAnchorPosition;
        private Transform _playerTransform;
        private PersistentWorldRegistry _persistentWorldRegistry;
        private int _persistentWorldRecordIndex = -1;
        private bool _registeredToWorldStateRegistry;
        private ulong _geneticsMask;
        private ushort _qualityMilli = DefaultQualityMilli;

        public ItemData ItemData => itemData;
        public int Quantity => quantity;
        public static int WorldStateRegistryCount => _worldStateRegistry.Count;
        public int ItemHashId => _cachedItemHashId;
        /// <summary>Persisted genetics payload carried by biological seed pickups.</summary>
        public ulong GeneticsMask => _geneticsMask;
        /// <summary>Persisted item quality in milli-normalized units.</summary>
        public ushort QualityMilli => _qualityMilli != 0 ? _qualityMilli : DefaultQualityMilli;
        public uint VulnerabilityMask => itemData != null ? itemData.VulnerabilityMask : 0u;
        public byte ImpactAudioMaterialId => itemData != null ? itemData.AudioMaterialByte : (byte)ItemAudioMaterialId.Organic;
        public bool IsFaunaBait => Hecton8.Inventory.PlayerInventory.IsFaunaBaitItem(itemData);

        public static PickupItem GetWorldStateRegistryAt(int index)
        {
            return _worldStateRegistry.GetAt(index);
        }

        public void Configure(ItemData data, int itemQuantity)
        {
            Configure(data, itemQuantity, 0UL, DefaultQualityMilli);
        }

        /// <summary>Configures pickup identity and persisted mutable item state.</summary>
        public void Configure(ItemData data, int itemQuantity, uint geneticsMask, ushort qualityMilli)
        {
            Configure(data, itemQuantity, (ulong)geneticsMask, qualityMilli);
        }

        /// <summary>Configures pickup identity and persisted mutable item state.</summary>
        public void Configure(ItemData data, int itemQuantity, ulong geneticsMask, ushort qualityMilli)
        {
            itemData = data;
            quantity = Mathf.Max(1, itemQuantity);
            _geneticsMask = geneticsMask;
            _qualityMilli = NormalizeQualityMilli(qualityMilli);
            RefreshCachedItemHash();
            ApplyPhysicalMetadata();
            InvalidateWorldStateIdentity();
            RebuildInteractTextCache();
        }

        internal void BindPersistentWorldRecord(PersistentWorldRegistry registry, int recordIndex)
        {
            _persistentWorldRegistry = registry;
            _persistentWorldRecordIndex = recordIndex;
        }

        internal void ClearPersistentWorldRecord()
        {
            _persistentWorldRegistry = null;
            _persistentWorldRecordIndex = -1;
        }

        private void Awake()
        {
            TryGetComponent(out _highlighter);
            TryGetComponent(out _rigidbody);
            TryGetComponent(out _buoyancy);
            TryGetComponent(out _collider);
            _defaultColliderMaterial = _collider != null ? _collider.sharedMaterial : null;
            if (_rigidbody != null)
            {
                _defaultLinearDamping = _rigidbody.linearDamping;
                _defaultAngularDamping = _rigidbody.angularDamping;
            }
            RefreshCachedItemHash();
            ApplyPhysicalMetadata();
            RebuildInteractTextCache();
        }

        private void ApplyPhysicalMetadata()
        {
            if (_rigidbody != null && itemData != null)
                _rigidbody.mass = itemData.MassKg;

            ConfigureWaterDynamicsFromData();

            if (_collider == null)
                return;

            _collider.sharedMaterial = itemData != null && itemData.WorldPhysicMaterial != null
                ? itemData.WorldPhysicMaterial
                : _defaultColliderMaterial;
        }

        private void RefreshCachedItemHash()
        {
            _cachedItemHashId = itemData != null
                ? Hecton.Localization.LocHash.Compute(itemData.PersistentId)
                : 0;
        }

        private void OnEnable()
        {
            RegisterWorldStateRegistry();

            if (ActiveRuntimeInstance == null)
                ActiveRuntimeInstance = this;

            ResolveWorldStateIdentity();

            WorldStateManager worldStateManager = Hecton8.Core.GlobalRegistry.WorldState;
            if (_worldStateIdentityAvailable &&
                worldStateManager != null &&
                worldStateManager.IsPickupDepleted(_worldStatePersistenceKey))
            {
                gameObject.SetActive(false);
                return;
            }

            RegisterSpatialHandle();
            TryRegisterSlowTick();
            TryRegisterFixedTick();

            if (_rigidbody != null && !_rigidbody.isKinematic)
                GlobalPhysicsStateManager.RegisterTrackedBody(_rigidbody);
        }

        private void Start()
        {
            TryRegisterSlowTick();
            TryRegisterFixedTick();
        }

        private void OnDisable()
        {
            TryUnregisterSlowTick();
            TryUnregisterFixedTick();
            UnregisterSpatialHandle();
            UnregisterWorldStateRegistry();
            ClearPersistentWorldRecord();
            RestoreDamping();
            if (_rigidbody != null)
                GlobalPhysicsStateManager.UnregisterTrackedBody(_rigidbody);

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        private void OnDestroy()
        {
            TryUnregisterSlowTick();
            TryUnregisterFixedTick();
            UnregisterSpatialHandle();
            UnregisterWorldStateRegistry();
            if (_rigidbody != null)
                GlobalPhysicsStateManager.UnregisterTrackedBody(_rigidbody);

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        public void SlowTick()
        {
            if (_spatialHandle == 0)
                return;

            Vector3 currentPosition = transform.position;
            WorldSpatialHashGrid.UpdateGridPosition(_spatialHandle, _lastSpatialPosition, currentPosition);
            if (_faunaSpatialHandle != 0)
                FaunaSpatialHashRegistry.Refresh(_faunaSpatialHandle);
            _lastSpatialPosition = currentPosition;
        }

        public void FixedTick(float fdt)
        {
            if (_rigidbody == null || _rigidbody.isKinematic || fdt <= 0f)
                return;

            WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref _playerTransform);
            if (_playerTransform != null)
            {
                Vector3 toPlayer = _playerTransform.position - _rigidbody.worldCenterOfMass;
                if (toPlayer.sqrMagnitude > CurrentSimulationCullDistanceSqr)
                {
                    if (!_rigidbody.IsSleeping())
                        _rigidbody.Sleep();

                    return;
                }

                if (_rigidbody.IsSleeping())
                    _rigidbody.WakeUp();
            }

            if (!ResolveSubmergedState())
            {
                RestoreDamping();
                return;
            }

            ApplyUnderwaterDamping();

            Vector3 sampledCurrent = CurrentVolume.SampleCombinedCurrent(_rigidbody.worldCenterOfMass);
            if (sampledCurrent.sqrMagnitude <= 0.0001f)
                return;

            Vector3 velocityChange = Vector3.ClampMagnitude(sampledCurrent, 6f) * (LooseCurrentVelocityInfluence * fdt);
            PhysicsForceRouter.QueueForce(_rigidbody, velocityChange, ForceMode.VelocityChange);

            Vector3 spinAxis = Vector3.Cross(Vector3.up, sampledCurrent);
            if (spinAxis.sqrMagnitude > 0.0001f)
                PhysicsForceRouter.QueueTorque(
                    _rigidbody,
                    spinAxis.normalized * (LooseCurrentSpinInfluence * velocityChange.magnitude),
                    ForceMode.VelocityChange);

            if (_spatialHandle != 0)
            {
                WorldSpatialHashGrid.Refresh(_spatialHandle);
                _lastSpatialPosition = transform.position;
            }

            if (_faunaSpatialHandle != 0)
                FaunaSpatialHashRegistry.Refresh(_faunaSpatialHandle);
        }

        internal bool TryGetWorldStatePersistenceIdentity(out long persistenceKey, out long chunkKey)
        {
            ResolveWorldStateIdentity();
            persistenceKey = _worldStatePersistenceKey;
            chunkKey = _worldStateChunkKey;
            return _worldStateIdentityAvailable;
        }

        private void RegisterSpatialHandle()
        {
            if (_spatialHandle == 0)
                _spatialHandle = WorldSpatialHashGrid.RegisterPickup(this);

            if (_faunaSpatialHandle == 0)
                _faunaSpatialHandle = FaunaSpatialHashRegistry.RegisterPickup(this);
            _lastSpatialPosition = transform.position;
        }

        private void UnregisterSpatialHandle()
        {
            if (_spatialHandle != 0)
            {
                WorldSpatialHashGrid.Unregister(_spatialHandle);
                _spatialHandle = 0;
            }

            if (_faunaSpatialHandle != 0)
            {
                FaunaSpatialHashRegistry.Unregister(_faunaSpatialHandle);
                _faunaSpatialHandle = 0;
            }
        }

        private void TryRegisterSlowTick()
        {
            if (_registeredToSlowTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _registeredToSlowTick = true;
        }

        private void TryRegisterFixedTick()
        {
            if (_registeredToFixedTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (_rigidbody == null || _rigidbody.isKinematic)
                return;

            GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Environment);
            _registeredToFixedTick = true;
        }

        private void TryUnregisterSlowTick()
        {
            if (!_registeredToSlowTick)
                return;

                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);

            _registeredToSlowTick = false;
        }

        private void TryUnregisterFixedTick()
        {
            if (!_registeredToFixedTick)
                return;

                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);

            _registeredToFixedTick = false;
        }

        public void OnHoverStart()
        {
            _highlighter.SetHighlight(true);
        }

        public void OnHoverEnd()
        {
            _highlighter.SetHighlight(false);
        }

        public void Interact(Transform interactor)
        {
            TryHandleInventoryPickup(Hecton8.Core.GlobalRegistry.PlayerInventoryRuntime, interactor);
        }

        public bool TryHandleInventoryPickup(PlayerInventory inventory, Transform interactor)
        {
            if (itemData == null || quantity <= 0 || _cachedItemHashId == 0)
                return false;

            if (inventory == null)
            {
                DropOverflow(interactor);
                return true;
            }

            PlayerInventory.ScavengeAttemptResult attempt = inventory.ScavengeAttempt(_cachedItemHashId, quantity, interactor, _geneticsMask, QualityMilli);
            if (!attempt.AnyAdded)
            {
                DropOverflow(interactor);
                return true;
            }

            InteractionEvents.RaiseItemCollected(itemData, attempt.AddedQuantity, interactor);
            bool hasInteractorPosition = interactor != null;
            ulong interactorEntityId = hasInteractorPosition ? EntityId.ToULong(interactor.GetEntityId()) : 0ul;
            Vector3 interactorPosition = hasInteractorPosition ? interactor.position : Vector3.zero;
            HectonEventBus.Publish(new ItemCollectedEvent(
                itemData,
                _cachedItemHashId,
                attempt.AddedQuantity,
                interactorEntityId,
                interactorPosition,
                hasInteractorPosition));

            quantity = attempt.RejectedQuantity;
            if (quantity > 0)
            {
                RebuildInteractTextCache();
                DropOverflow(interactor);
                return true;
            }

            if (_worldStateIdentityAvailable)
                Hecton8.Core.GlobalRegistry.WorldState?.RegisterCollectedPickup(_worldStatePersistenceKey, _worldStateChunkKey);

            _persistentWorldRegistry?.MarkRecordCollected(_persistentWorldRecordIndex);
            ConsumeWorldProxy();
            return true;
        }

        private static ushort NormalizeQualityMilli(ushort qualityMilli)
        {
            if (qualityMilli == 0)
                return DefaultQualityMilli;

            return (ushort)Mathf.Clamp((int)qualityMilli, 0, DefaultQualityMilli);
        }

        public string GetInteractText()
        {
            return _cachedInteractText;
        }

        private void RebuildInteractTextCache()
        {
            if (itemData == null)
            {
                _cachedInteractText = "Pick up Unknown";
                return;
            }

            string baseText = itemData.GetInteractText();
            if (quantity > 1)
            {
                _cachedInteractText = baseText + " x" + quantity;
                return;
            }

            _cachedInteractText = baseText;
        }

        private void RegisterWorldStateRegistry()
        {
            if (_registeredToWorldStateRegistry)
                return;

            _worldStateRegistry.Register(this);
            _registeredToWorldStateRegistry = true;
        }

        private void UnregisterWorldStateRegistry()
        {
            if (!_registeredToWorldStateRegistry)
                return;

            _worldStateRegistry.Unregister(this);
            _registeredToWorldStateRegistry = false;
        }

        private void ConsumeWorldProxy()
        {
            if (_highlighter != null)
                _highlighter.SetHighlight(false);

            ObjectPoolManager pool = GlobalRegistry.ObjectPool;
            if (pool != null && TryGetComponent(out ObjectPoolManager.PoolItemMarker _))
            {
                pool.Despawn(gameObject);
                return;
            }

            gameObject.SetActive(false);
        }

        private void DropOverflow(Transform interactor)
        {
            if (_rigidbody == null || _rigidbody.isKinematic)
                return;

            Vector3 scatterDirection = ResolveScatterDirection(interactor);
            Vector3 impulse = scatterDirection * OverflowScatterImpulse;
            impulse.y += OverflowScatterLiftImpulse;

            if (!IsFiniteVector(impulse))
                return;

            _rigidbody.WakeUp();
            PhysicsForceRouter.QueueForce(_rigidbody, impulse, ForceMode.Impulse);

            Vector3 torqueAxis = Vector3.Cross(Vector3.up, scatterDirection);
            if (torqueAxis.sqrMagnitude <= 0.0001f)
                torqueAxis = Vector3.right;

            Vector3 torque = torqueAxis.normalized * OverflowScatterTorqueImpulse;
            if (IsFiniteVector(torque))
                PhysicsForceRouter.QueueTorque(_rigidbody, torque, ForceMode.Impulse);
        }

        private Vector3 ResolveScatterDirection(Transform interactor)
        {
            if (interactor != null)
            {
                Vector3 scatterDirection = transform.position - interactor.position;
                scatterDirection.y = 0f;
                if (scatterDirection.sqrMagnitude > 0.0001f)
                    return scatterDirection.normalized;

                Vector3 fallbackForward = -interactor.forward;
                fallbackForward.y = 0f;
                if (fallbackForward.sqrMagnitude > 0.0001f)
                    return fallbackForward.normalized;
            }

            return Vector3.forward;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private void ConfigureWaterDynamicsFromData()
        {
            if (itemData == null || _rigidbody == null)
                return;

            if (_buoyancy == null)
            {
                TryGetComponent(out _buoyancy);
                if (_buoyancy == null)
                    _buoyancy = gameObject.AddComponent<BuoyancyObject>(); // COLD ALLOC: BuoyancyObject[1] â€” runtime world-item buoyancy proxy — owner: PickupItem
            }

            if (_buoyancy == null)
                return;

            if (itemData.worldBuoyancyProfile != null)
                _buoyancy.SetProfile(itemData.worldBuoyancyProfile);

            _buoyancy.ConfigureRuntimeFluidState(
                itemData.MassKg,
                itemData.VolumeM3,
                ResolveBuoyancyHeightMeters(),
                DeepSeaSeawaterDensityKgPerM3,
                LooseItemBuoyancyAngularDragMultiplier);
        }

        private float ResolveBuoyancyHeightMeters()
        {
            if (_collider != null)
            {
                Bounds bounds = _collider.bounds;
                if (bounds.size.y > 0.01f)
                    return bounds.size.y;
            }

            Vector3 lossyScale = transform.lossyScale;
            return Mathf.Max(0.1f, lossyScale.y);
        }

        private void ApplyUnderwaterDamping()
        {
            if (_rigidbody == null)
                return;

            _rigidbody.linearDamping = Mathf.Max(_defaultLinearDamping, LooseItemUnderwaterLinearDamping);
            _rigidbody.angularDamping = Mathf.Max(_defaultAngularDamping, LooseItemUnderwaterAngularDamping);
        }

        private void RestoreDamping()
        {
            if (_rigidbody == null)
                return;

            _rigidbody.linearDamping = _defaultLinearDamping;
            _rigidbody.angularDamping = _defaultAngularDamping;
        }

        private void ResolveWorldStateIdentity()
        {
            if (_worldStateIdentityResolved)
                return;

            _worldStateAnchorPosition = transform.position;
            _worldStateIdentityResolved = true;
            bool isPooledRuntimeInstance = TryGetComponent(out ObjectPoolManager.PoolItemMarker _);
            _worldStateIdentityAvailable = persistWorldState &&
                                           !isPooledRuntimeInstance &&
                                           WorldPickupStateCodec.TryBuildIdentity(
                                               transform,
                                               gameObject.scene,
                                               itemData,
                                               _worldStateAnchorPosition,
                                               out _worldStatePersistenceKey,
                                               out _worldStateChunkKey);

            if (_worldStateIdentityAvailable)
                return;

            _worldStatePersistenceKey = 0L;
            _worldStateChunkKey = 0L;
        }

        private void InvalidateWorldStateIdentity()
        {
            _worldStateIdentityResolved = false;
            _worldStateIdentityAvailable = false;
            _worldStatePersistenceKey = 0L;
            _worldStateChunkKey = 0L;
            _worldStateAnchorPosition = default;
        }

        private bool ResolveSubmergedState()
        {
            if (_playerMovement == null &&
                Hecton8.Core.GlobalRegistry.WorldState != null &&
                Hecton8.Core.GlobalRegistry.WorldState.PlayerTransform != null)
            {
                Hecton8.Core.GlobalRegistry.WorldState.PlayerTransform.TryGetComponent(out _playerMovement);
            }

            if (_playerMovement == null)
                return true;

            float depth = Mathf.Max(0f, _playerMovement.CurrentWaterSurfaceY - transform.position.y);
            return SurfaceStateUtility.ResolveUnderwaterFromDepth(depth, true);
        }
    }
}
