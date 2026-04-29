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

        [Header("Item Configuration")]
        [SerializeField] private ItemData itemData;
        [SerializeField] private int quantity = 1;

        [Header("World State")]
        [Tooltip("Persist this authored world pickup into WorldStateManager depletion storage.")]
        [SerializeField] private bool persistWorldState = true;

        private InteractionHighlighter _highlighter;
        private Rigidbody _rigidbody;
        private Collider _collider;
        private PhysicMaterial _defaultColliderMaterial;
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

        public ItemData ItemData => itemData;
        public int Quantity => quantity;
        public static int WorldStateRegistryCount => _worldStateRegistry.Count;
        public int ItemHashId => _cachedItemHashId;
        public uint VulnerabilityMask => itemData != null ? itemData.VulnerabilityMask : 0u;
        public byte ImpactAudioMaterialId => itemData != null ? itemData.AudioMaterialByte : (byte)ItemAudioMaterialId.Organic;
        public bool IsFaunaBait => Hecton8.Inventory.PlayerInventory.IsFaunaBaitItem(itemData);

        public static PickupItem GetWorldStateRegistryAt(int index)
        {
            return _worldStateRegistry.GetAt(index);
        }

        public void Configure(ItemData data, int itemQuantity)
        {
            itemData = data;
            quantity = Mathf.Max(1, itemQuantity);
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
            TryGetComponent(out _collider);
            _defaultColliderMaterial = _collider != null ? _collider.sharedMaterial : null;
            RefreshCachedItemHash();
            ApplyPhysicalMetadata();
            RebuildInteractTextCache();
        }

        private void ApplyPhysicalMetadata()
        {
            if (_rigidbody != null && itemData != null)
                _rigidbody.mass = itemData.MassKg;

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

            WorldStateManager worldStateManager = WorldStateManager.Instance;
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

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        private void OnDestroy()
        {
            TryUnregisterSlowTick();
            TryUnregisterFixedTick();
            UnregisterSpatialHandle();
            UnregisterWorldStateRegistry();

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
                return;

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
            TryHandleInventoryPickup(PlayerInventory.Instance, interactor);
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

            PlayerInventory.ScavengeAttemptResult attempt = inventory.ScavengeAttempt(_cachedItemHashId, quantity, interactor);
            if (!attempt.AnyAdded)
            {
                DropOverflow(interactor);
                return true;
            }

            InteractionEvents.RaiseItemCollected(itemData, attempt.AddedQuantity, interactor);
            HectonEventBus.Publish(new ItemCollectedEvent(itemData, _cachedItemHashId, attempt.AddedQuantity, interactor));

            quantity = attempt.RejectedQuantity;
            if (quantity > 0)
            {
                RebuildInteractTextCache();
                DropOverflow(interactor);
                return true;
            }

            if (_worldStateIdentityAvailable)
                WorldStateManager.Instance?.RegisterCollectedPickup(_worldStatePersistenceKey, _worldStateChunkKey);

            _persistentWorldRegistry?.MarkRecordCollected(_persistentWorldRecordIndex);
            ConsumeWorldProxy();
            return true;
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

            if (ObjectPoolManager.Instance != null && TryGetComponent(out ObjectPoolManager.PoolItemMarker _))
            {
                ObjectPoolManager.Instance.Despawn(gameObject);
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
                WorldStateManager.Instance != null &&
                WorldStateManager.Instance.PlayerTransform != null)
            {
                WorldStateManager.Instance.PlayerTransform.TryGetComponent(out _playerMovement);
            }

            if (_playerMovement == null)
                return true;

            float depth = Mathf.Max(0f, _playerMovement.CurrentWaterSurfaceY - transform.position.y);
            return SurfaceStateUtility.ResolveUnderwaterFromDepth(depth, true);
        }
    }
}
