// ============================================================================
// HECTON-8 — PickupItem.cs
// Example IInteractable implementation showing all systems working together.
// ============================================================================

using System;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Physics;
using Hecton8.Gameplay;

namespace Hecton8.Interaction
{
    using Hecton8.World;
    using Unity.Mathematics;
    using UnityEngine;

    [RequireComponent(typeof(InteractionHighlighter))]
    [RequireComponent(typeof(Collider))]
    public class PickupItem : MonoBehaviour, IInteractable, IInteractableTextProvider, ISlowTickable, IFixedTickable, IInventoryPickupSource, IInventoryPickupPreviewSource, IInteractionVulnerabilitySource, IFaunaBaitSource, Hecton8.Physics.IPhysicsImpactMaterialProvider
    {
        private const int WorldStateRegistryCapacity = 8192;

        // COLD ALLOC: RegistryBucket<PickupItem>[8192] - authored/persistent pickup registry for world-state scans and loot magnet hard-cap parity - owner: PickupItem
        private static readonly RegistryBucket<PickupItem> _worldStateRegistry = new RegistryBucket<PickupItem>(WorldStateRegistryCapacity);
        internal static PickupItem ActiveRuntimeInstance { get; private set; }
        private static IPlayerRuntimeContext s_playerRuntimeContext;
        private static IPlayerInventoryService s_playerInventoryService;
        private static IPhysicsService s_physicsService;
        private static IObjectPoolService s_objectPool;
        private static readonly StaticRegistryHotSwapListener s_hotSwapListener = new StaticRegistryHotSwapListener();
        private static bool s_hotSwapListenerRegistered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ActiveRuntimeInstance = null;
            _worldStateRegistry.Clear();
            s_playerRuntimeContext = null;
            s_playerInventoryService = null;
            s_physicsService = null;
            s_objectPool = null;
            s_hotSwapListenerRegistered = false;
        }

        private const float LooseCurrentVelocityInfluence = 0.45f;
        private const float LooseCurrentSpinInfluence = 0.12f;
        private const float OverflowScatterImpulse = 2.5f;
        private const float OverflowScatterLiftImpulse = 1.2f;
        private const float OverflowScatterTorqueImpulse = 0.35f;
        private const float DeepSeaSeawaterDensityKgPerM3 = HectonPhysicsContract.WaterDensityKgPerCubicMeterConst;
        private const float LooseItemUnderwaterLinearDamping = 1.6f;
        private const float LooseItemUnderwaterAngularDamping = 6.5f;
        private const float LooseItemBuoyancyAngularDragMultiplier = 2.75f;
        private const ushort DefaultQualityMilli = 1000;
        private const int InteractTextBufferCapacity = 128;
        private static readonly char[] UnknownInteractText =
        {
            'P', 'i', 'c', 'k', ' ', 'u', 'p', ' ', 'U', 'n', 'k', 'n', 'o', 'w', 'n'
        };

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
        private Renderer _lootMagnetRenderer;
        private MotionVectorGenerationMode _defaultMotionVectorMode;
        private PhysicsMaterial _defaultColliderMaterial;
        private float _defaultLinearDamping;
        private float _defaultAngularDamping;
        private bool _lootMagnetRestoreRigidbodyKinematic;
        private bool _lootMagnetRestoreRigidbodyDetectCollisions;
        private WorldStateManager _worldStateManager;
        private HectonPlayerMovement _playerMovement;
        private readonly char[] _cachedInteractTextBuffer = new char[InteractTextBufferCapacity];
        private int _cachedInteractTextLength;
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
        private PersistentWorldRegistry _persistentWorldRegistry;
        private int _persistentWorldRecordIndex = -1;
        private bool _registeredToWorldStateRegistry;
        private ulong _geneticsMask;
        private ushort _qualityMilli = DefaultQualityMilli;
        private bool _lootMagnetMotionVectorForced;
        private bool _lootMagnetPhysicsSuppressed;

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

        /// <summary>Applies deterministic loot magnet presentation without Unity trigger callbacks.</summary>
        public void ApplyLootMagnetPose(Vector3 runtimePosition, float3 velocity, float motionVectorThresholdSq)
        {
            if (!IsFiniteVector(runtimePosition))
            {
                RestoreLootMagnetRuntimeState();
                return;
            }

            SuppressLootMagnetPhysics();
            transform.position = runtimePosition;

            Renderer renderer = _lootMagnetRenderer;
            if (renderer == null)
                return;

            float velocitySq = math.lengthsq(velocity);
            if (!math.isfinite(velocitySq) || velocitySq <= motionVectorThresholdSq)
            {
                RestoreLootMagnetMotionVectorMode();
                return;
            }

            if (_lootMagnetMotionVectorForced)
                return;

            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.Object;
            _lootMagnetMotionVectorForced = true;
        }

        /// <summary>Restores authored physics/render state after math-driven magnet presentation stops owning the pickup pose.</summary>
        public void RestoreLootMagnetRuntimeState()
        {
            RestoreLootMagnetMotionVectorMode();
            if (!_lootMagnetPhysicsSuppressed)
                return;

            if (_rigidbody == null)
            {
                _lootMagnetPhysicsSuppressed = false;
                return;
            }

            _rigidbody.isKinematic = _lootMagnetRestoreRigidbodyKinematic;
            _rigidbody.detectCollisions = _lootMagnetRestoreRigidbodyDetectCollisions;
            _lootMagnetPhysicsSuppressed = false;
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

        private void RestoreLootMagnetMotionVectorMode()
        {
            if (!_lootMagnetMotionVectorForced || _lootMagnetRenderer == null)
                return;

            _lootMagnetRenderer.motionVectorGenerationMode = _defaultMotionVectorMode;
            _lootMagnetMotionVectorForced = false;
        }

        private void SuppressLootMagnetPhysics()
        {
            if (_lootMagnetPhysicsSuppressed || _rigidbody == null)
                return;

            _lootMagnetRestoreRigidbodyKinematic = _rigidbody.isKinematic;
            _lootMagnetRestoreRigidbodyDetectCollisions = _rigidbody.detectCollisions;
            _rigidbody.detectCollisions = false;
            _rigidbody.isKinematic = true;
            _lootMagnetPhysicsSuppressed = true;
        }

        private void Awake()
        {
            TryGetComponent(out _highlighter);
            TryGetComponent(out _rigidbody);
            TryGetComponent(out _buoyancy);
            TryGetComponent(out _collider);
            TryGetComponent(out _lootMagnetRenderer);
            _defaultColliderMaterial = _collider != null ? _collider.sharedMaterial : null;
            _defaultMotionVectorMode = _lootMagnetRenderer != null
                ? _lootMagnetRenderer.motionVectorGenerationMode
                : MotionVectorGenerationMode.Camera;
            if (_rigidbody != null)
            {
                _defaultLinearDamping = _rigidbody.linearDamping;
                _defaultAngularDamping = _rigidbody.angularDamping;
            }
            RefreshCachedItemHash();
            ApplyPhysicalMetadata();
            RebuildInteractTextCache();
            RefreshColdRegistryReferences();
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
            RefreshColdRegistryReferences();
            InteractableRegistry.RegisterTree(this);
            RegisterWorldStateRegistry();

            if (ActiveRuntimeInstance == null)
                ActiveRuntimeInstance = this;

            ResolveWorldStateIdentity();

            WorldStateManager worldStateManager = _worldStateManager;
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
            RefreshColdRegistryReferences();
            TryRegisterSlowTick();
            TryRegisterFixedTick();
        }

        private void OnDisable()
        {
            InteractableRegistry.InvalidateTree(this);
            TryUnregisterSlowTick();
            TryUnregisterFixedTick();
            UnregisterSpatialHandle();
            UnregisterWorldStateRegistry();
            ClearPersistentWorldRecord();
            RestoreDamping();
            RestoreLootMagnetRuntimeState();
            if (_rigidbody != null)
                GlobalPhysicsStateManager.UnregisterTrackedBody(_rigidbody);

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        private void OnDestroy()
        {
            InteractableRegistry.InvalidateTree(this);
            TryUnregisterSlowTick();
            TryUnregisterFixedTick();
            UnregisterSpatialHandle();
            UnregisterWorldStateRegistry();
            RestoreLootMagnetRuntimeState();
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
            if (!IsFiniteVector(currentPosition))
                return;

            WorldSpatialHashGrid.UpdateGridPosition(_spatialHandle, _lastSpatialPosition, currentPosition);
            if (_faunaSpatialHandle != 0)
                FaunaSpatialHashRegistry.Refresh(_faunaSpatialHandle);
            _lastSpatialPosition = currentPosition;
        }

        public void FixedTick(float fdt)
        {
            if (_rigidbody == null || _rigidbody.isKinematic || fdt <= 0f)
                return;

            if (_rigidbody.IsSleeping())
                return;

            if (!ResolveSubmergedState())
            {
                RestoreDamping();
                return;
            }

            ApplyUnderwaterDamping();

            Vector3 sampledCurrent = CurrentVolume.SampleCombinedCurrent(_rigidbody.worldCenterOfMass);
            if (!IsFiniteVector(sampledCurrent))
                return;

            if (sampledCurrent.sqrMagnitude <= 0.0001f)
                return;

            float currentLength = EstimateLength3D(sampledCurrent);
            float currentScale = currentLength > 0.0001f
                ? math.min(6f, currentLength) / currentLength
                : 0f;
            Vector3 velocityChange = sampledCurrent * (currentScale * LooseCurrentVelocityInfluence * fdt);
            PhysicsForceRouter.QueueAmbientForce(_rigidbody, velocityChange, ForceMode.VelocityChange, wake: false);

            Vector3 spinAxis = Vector3.Cross(Vector3.up, sampledCurrent);
            float spinAxisLength = EstimateLength3D(spinAxis);
            if (spinAxisLength > 0.0001f)
            {
                float velocityLength = EstimateLength3D(velocityChange);
                PhysicsForceRouter.QueueAmbientTorque(
                    _rigidbody,
                    spinAxis * ((LooseCurrentSpinInfluence * velocityLength) / spinAxisLength),
                    ForceMode.VelocityChange,
                    wake: false);
            }

            if (_spatialHandle != 0)
            {
                Vector3 currentPosition = transform.position;
                if (IsFiniteVector(currentPosition))
                {
                    WorldSpatialHashGrid.Refresh(_spatialHandle);
                    _lastSpatialPosition = currentPosition;
                }
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
            Vector3 currentPosition = transform.position;
            if (!IsFiniteVector(currentPosition))
                return;

            if (_spatialHandle == 0)
                _spatialHandle = WorldSpatialHashGrid.RegisterPickup(this);

            if (_faunaSpatialHandle == 0)
                _faunaSpatialHandle = FaunaSpatialHashRegistry.RegisterPickup(this);
            _lastSpatialPosition = currentPosition;
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
            if (_registeredToSlowTick || !Application.isPlaying)
                return;

            _registeredToSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryRegisterFixedTick()
        {
            if (_registeredToFixedTick || !Application.isPlaying)
                return;

            if (_rigidbody == null || _rigidbody.isKinematic)
                return;

            _registeredToFixedTick = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Environment);
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
            PlayerInventory inventory = s_playerInventoryService != null ? s_playerInventoryService.Inventory : null;
            TryHandleInventoryPickup(inventory, interactor);
        }

        public bool TryHandleInventoryPickup(PlayerInventory inventory, Transform interactor)
        {
            return TryHandleInventoryPickup(inventory, interactor, publishAcquiredSignal: true);
        }

        public bool TryHandleInventoryPickup(PlayerInventory inventory, Transform interactor, bool publishAcquiredSignal)
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

            if (publishAcquiredSignal)
                PublishItemAcquiredSignal(attempt.AddedQuantity, interactor);

            quantity = attempt.RejectedQuantity;
            if (quantity > 0)
            {
                RebuildInteractTextCache();
                DropOverflow(interactor);
                return true;
            }

            if (_worldStateIdentityAvailable)
                _worldStateManager?.RegisterCollectedPickup(_worldStatePersistenceKey, _worldStateChunkKey);

            _persistentWorldRegistry?.MarkRecordCollected(_persistentWorldRecordIndex);
            ConsumeWorldProxy();
            return true;
        }

        public bool TryPeekInventoryPickup(out ItemData previewItemData, out int previewQuantity)
        {
            previewItemData = itemData;
            previewQuantity = quantity;
            return previewItemData != null && previewQuantity > 0;
        }

        private void PublishItemAcquiredSignal(int addedQuantity, Transform interactor)
        {
            if (addedQuantity <= 0 || _cachedItemHashId == 0)
                return;

            if (!TryResolveSignalAup(interactor, out AbsoluteUniversePosition positionAup))
                return;

            ItemAcquiredSignal signal = new ItemAcquiredSignal
            {
                PositionAup = positionAup,
                ItemHash = unchecked((uint)_cachedItemHashId),
                OreHash = unchecked((uint)_cachedItemHashId),
                Quantity = (ushort)math.min(addedQuantity, (int)ushort.MaxValue),
                SourceKind = InventoryPickupSignalConstants.ItemSourceManualPickup,
                Flags = InventoryPickupSignalConstants.SignalFlagManualPickup,
                Frame = ResolveCurrentFrameId()
            };
            SignalBus<ItemAcquiredSignal>.TryPush(in signal);
        }

        private static uint ResolveCurrentFrameId()
        {
            uint frame = TimeSliceScheduler.CurrentFrameId;
            return frame != 0u ? frame : 1u;
        }

        private bool TryResolveSignalAup(Transform interactor, out AbsoluteUniversePosition positionAup)
        {
            if (interactor != null)
            {
                Vector3 interactorPosition = interactor.position;
                if (IsFiniteVector(interactorPosition) &&
                    TryBuildFiniteSignalAup(interactorPosition, out positionAup))
                {
                    return true;
                }
            }

            Vector3 signalPosition = transform.position;
            if (IsFiniteVector(signalPosition) &&
                TryBuildFiniteSignalAup(signalPosition, out positionAup))
            {
                return true;
            }

            positionAup = default;
            return false;
        }

        private static bool TryBuildFiniteSignalAup(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            IPlayerRuntimeContext playerContext = s_playerRuntimeContext;
            if (playerContext == null ||
                !playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) ||
                !IsFiniteAup(in snapshot.Aup))
            {
                return false;
            }

            double3 deltaMeters = new double3(
                (double)runtimePosition.x - snapshot.RuntimePosition.x,
                (double)runtimePosition.y - snapshot.RuntimePosition.y,
                (double)runtimePosition.z - snapshot.RuntimePosition.z);
            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in snapshot.Aup,
                deltaMeters);
            return IsFiniteAup(in positionAup);
        }

        private static ushort NormalizeQualityMilli(ushort qualityMilli)
        {
            if (qualityMilli == 0)
                return DefaultQualityMilli;

            return (ushort)Mathf.Clamp((int)qualityMilli, 0, DefaultQualityMilli);
        }

        public string GetInteractText()
        {
            return itemData != null ? itemData.GetInteractText() : "Pick up Unknown";
        }

        public bool TryCopyInteractText(System.Span<char> destination, out int length)
        {
            return InteractableTextCopy.TryCopyWithQuantity(
                _cachedInteractTextBuffer.AsSpan(0, _cachedInteractTextLength),
                quantity,
                destination,
                out length);
        }

        private void RebuildInteractTextCache()
        {
            if (itemData == null)
            {
                _cachedInteractTextLength = CopySpanToInteractBuffer(UnknownInteractText);
                return;
            }

            if (!itemData.TryWriteInteractText(Hecton8.Core.GlobalRegistry.LocalizationText, _cachedInteractTextBuffer, out _cachedInteractTextLength))
                _cachedInteractTextLength = CopySpanToInteractBuffer(itemData.GetInteractText().AsSpan());
        }

        private int CopySpanToInteractBuffer(System.ReadOnlySpan<char> source)
        {
            int length = math.min(source.Length, _cachedInteractTextBuffer.Length);
            if (length > 0)
                source.Slice(0, length).CopyTo(_cachedInteractTextBuffer);
            return length;
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

            IObjectPoolService pool = s_objectPool;
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

            IPhysicsService physicsService = s_physicsService;
            if (physicsService == null)
                return;

            Vector3 scatterDirection = ResolveScatterDirection(interactor);
            Vector3 impulse = scatterDirection * OverflowScatterImpulse;
            impulse.y += OverflowScatterLiftImpulse;

            if (!IsFiniteVector(impulse))
                return;

            _rigidbody.WakeUp();
            physicsService.QueueForce(_rigidbody, impulse, ForceMode.Impulse);

            Vector3 torqueAxis = Vector3.Cross(Vector3.up, scatterDirection);
            if (torqueAxis.sqrMagnitude <= 0.0001f)
                torqueAxis = Vector3.right;

            float torqueAxisLength = EstimateLength3D(torqueAxis);
            Vector3 torque = torqueAxisLength > 0.0001f
                ? torqueAxis * (OverflowScatterTorqueImpulse / torqueAxisLength)
                : Vector3.zero;
            if (IsFiniteVector(torque))
                physicsService.QueueTorque(_rigidbody, torque, ForceMode.Impulse);
        }

        private Vector3 ResolveScatterDirection(Transform interactor)
        {
            if (interactor != null)
            {
                Vector3 currentPosition = transform.position;
                Vector3 interactorPosition = interactor.position;
                if (IsFiniteVector(currentPosition) && IsFiniteVector(interactorPosition))
                {
                    Vector3 scatterDirection = currentPosition - interactorPosition;
                    scatterDirection.y = 0f;
                    float scatterLength = EstimateLength3D(scatterDirection);
                    if (scatterLength > 0.0001f)
                        return scatterDirection * (1f / scatterLength);
                }

                Vector3 fallbackForward = -interactor.forward;
                fallbackForward.y = 0f;
                if (IsFiniteVector(fallbackForward))
                {
                    float fallbackLength = EstimateLength3D(fallbackForward);
                    if (fallbackLength > 0.0001f)
                        return fallbackForward * (1f / fallbackLength);
                }
            }

            return Vector3.forward;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition value)
        {
            return !float.IsNaN(value.LocalX) && !float.IsInfinity(value.LocalX) &&
                   !float.IsNaN(value.LocalY) && !float.IsInfinity(value.LocalY) &&
                   !float.IsNaN(value.LocalZ) && !float.IsInfinity(value.LocalZ);
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

            Vector3 anchorPosition = transform.position;
            _worldStateIdentityResolved = true;
            if (!IsFiniteVector(anchorPosition))
            {
                _worldStateAnchorPosition = default;
                _worldStateIdentityAvailable = false;
                _worldStatePersistenceKey = 0L;
                _worldStateChunkKey = 0L;
                return;
            }

            _worldStateAnchorPosition = anchorPosition;
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

        private void RefreshColdRegistryReferences()
        {
            _worldStateManager = Hecton8.Core.GlobalRegistry.WorldState;
            s_playerRuntimeContext = Hecton8.Core.GlobalRegistry.Player;
            s_playerInventoryService = Hecton8.Core.GlobalRegistry.PlayerInventory;
            s_physicsService = Hecton8.Core.GlobalRegistry.Physics;
            s_objectPool = Hecton8.Core.GlobalRegistry.ObjectPoolService;
            TryRegisterStaticHotSwapListener();
            RefreshCachedPlayerMovement();
        }

        private static void TryRegisterStaticHotSwapListener()
        {
            if (s_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            s_hotSwapListenerRegistered = Hecton8.Core.GlobalRegistry.TryRegisterHotSwapListener(s_hotSwapListener);
        }

        private sealed class StaticRegistryHotSwapListener : IGlobalRegistryHotSwapListener
        {
            public void OnGlobalRegistryServiceReplaced(
                GlobalRegistryServiceSlot serviceSlot,
                object previousService,
                object currentService)
            {
                switch (serviceSlot)
                {
                    case GlobalRegistryServiceSlot.Player:
                        s_playerRuntimeContext = currentService as IPlayerRuntimeContext;
                        break;
                    case GlobalRegistryServiceSlot.PlayerInventory:
                        s_playerInventoryService = currentService as IPlayerInventoryService;
                        break;
                    case GlobalRegistryServiceSlot.Physics:
                        s_physicsService = currentService as IPhysicsService;
                        break;
                    case GlobalRegistryServiceSlot.ObjectPool:
                        s_objectPool = currentService as IObjectPoolService;
                        break;
                }
            }
        }

        private void RefreshCachedPlayerMovement()
        {
            WorldStateManager worldStateManager = _worldStateManager;
            Transform playerTransform = worldStateManager != null ? worldStateManager.PlayerTransform : null;
            if (playerTransform == null)
            {
                _playerMovement = null;
                return;
            }

            playerTransform.TryGetComponent(out _playerMovement);
        }

        private bool ResolveSubmergedState()
        {
            if (_playerMovement == null)
                return true;

            Vector3 currentPosition = transform.position;
            if (!IsFiniteVector(currentPosition))
                return false;

            float depth = Mathf.Max(0f, _playerMovement.CurrentWaterSurfaceY - currentPosition.y);
            return SurfaceStateUtility.ResolveUnderwaterFromDepth(depth, true);
        }

        private static float EstimateLength3D(Vector3 value)
        {
            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            float az = math.abs(value.z);
            float maxAxis = math.max(ax, math.max(ay, az));
            float minAxis = math.min(ax, math.min(ay, az));
            float midAxis = ax + ay + az - maxAxis - minAxis;
            return maxAxis + (midAxis * 0.375f) + (minAxis * 0.25f);
        }
    }
}
