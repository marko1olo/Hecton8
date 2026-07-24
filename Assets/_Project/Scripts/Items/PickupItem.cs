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
using Hecton8.Gameplay;
using BuoyancyObject = Hecton8.Physics.BuoyancyObject;

namespace Hecton8.Interaction
{
    using Hecton8.World;
    using Unity.Mathematics;
    using UnityEngine;

    [RequireComponent(typeof(InteractionHighlighter))]
    [RequireComponent(typeof(Collider))]
    public class PickupItem : MonoBehaviour, IInteractable, IInteractableTextProvider, ISlowTickable, IFixedTickable, IInventoryPickupSource, IInventoryPickupPreviewSource, IInteractionVulnerabilitySource, IFaunaBaitSource, Hecton8.Core.Contracts.IPhysicsImpactMaterialProvider
    {
        private static int s_x001PickupItemSignalPushDropCount;
        private const int WorldStateRegistryCapacity = 8192;

        // COLD ALLOC: RegistryBucket<PickupItem>[8192] - authored/persistent pickup registry for world-state scans and loot magnet hard-cap parity - owner: PickupItem
        private static readonly RegistryBucket<PickupItem> _worldStateRegistry = new RegistryBucket<PickupItem>(WorldStateRegistryCapacity);
        internal static PickupItem ActiveRuntimeInstance { get; private set; }
        private static IPlayerRuntimeContext s_playerRuntimeContext;
        private static IPlayerInventoryService s_playerInventoryService;
        private static IPhysicsService s_physicsService;
        private static IPhysicsStateEventService s_physicsStateEvents;
        private static IAmbientCurrentReadModel s_ambientCurrentReadModel;
        private static IObjectPoolService s_objectPool;
        private static ILocalizationTextReadModel s_localizationText;
        private static WorldStateManager s_worldStateManager;
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
            s_physicsStateEvents = null;
            s_ambientCurrentReadModel = null;
            s_objectPool = null;
            s_localizationText = null;
            s_worldStateManager = null;
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
        [Tooltip("Stable authored pickup identity. Scene instances auto-fill this in editor so hierarchy/name cleanup cannot respawn collected pickups.")]
        [SerializeField] private string stableWorldStateId = string.Empty;

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
        private bool _registeredPhysicsBodyTracking;
        private Vector3 _lastSpatialPosition;
        private bool _worldStateIdentityResolved;
        private bool _worldStateIdentityAvailable;
        private long _worldStatePersistenceKey;
        private long _worldStateChunkKey;
        private bool _legacyWorldStateIdentityAvailable;
        private long _legacyWorldStatePersistenceKey;
        private Vector3 _worldStateAnchorPosition;
        private bool _isPooledRuntimeInstance;
        private PersistentWorldRegistry _persistentWorldRegistry;
        private int _persistentWorldRecordIndex = -1;
        private bool _registeredToWorldStateRegistry;
        private bool _worldStateSuppressedByPersistence;
        private int _worldStateRestoreQuantity;
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
            _worldStateRestoreQuantity = quantity;
            _worldStateSuppressedByPersistence = false;
            _geneticsMask = geneticsMask;
            _qualityMilli = NormalizeQualityMilli(qualityMilli);
            RefreshCachedItemHash();
            ApplyPhysicalMetadata();
            InvalidateWorldStateIdentity();
            CaptureWorldStateIdentityCold();
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
            RefreshPoolMarkerCacheCold();
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
            CaptureWorldStateRestoreBaseline();
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
                ? itemData.PersistentHashId
                : 0;
        }

        private void OnEnable()
        {
            RefreshColdRegistryReferences();
            RefreshPoolMarkerCacheCold();
            InteractableRegistry.RegisterTree(this);
            RegisterWorldStateRegistry();

            if (ActiveRuntimeInstance == null)
                ActiveRuntimeInstance = this;

            CaptureWorldStateIdentityCold();

            WorldStateManager worldStateManager = ResolveWorldStateManager();
            if (_worldStateIdentityAvailable &&
                worldStateManager != null &&
                worldStateManager.TryResolveOrPromoteCollectedPickup(
                    _worldStatePersistenceKey,
                    _worldStateChunkKey,
                    _legacyWorldStateIdentityAvailable ? _legacyWorldStatePersistenceKey : 0L))
            {
                ApplyWorldStateSuppression();
                return;
            }

            RegisterSpatialHandle();
            TryRegisterSlowTick();
            TryRegisterFixedTick();
            TryRegisterPhysicsBodyTracking();
        }

        private void Start()
        {
            RefreshColdRegistryReferences();
            TryRegisterSlowTick();
            TryRegisterFixedTick();
            TryRegisterPhysicsBodyTracking();
        }

        private void OnDisable()
        {
            InteractableRegistry.InvalidateTree(this);
            TryUnregisterSlowTick();
            TryUnregisterFixedTick();
            UnregisterSpatialHandle();
            if (!ShouldRetainWorldStateRegistryWhileInactive())
                UnregisterWorldStateRegistry();
            ClearPersistentWorldRecord();
            RestoreDamping();
            RestoreLootMagnetRuntimeState();
            TryUnregisterPhysicsBodyTracking();

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
            TryUnregisterPhysicsBodyTracking();

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

            TryRegisterPhysicsBodyTracking();

            if (_rigidbody.IsSleeping())
                return;

            if (!ResolveSubmergedState())
            {
                RestoreDamping();
                return;
            }

            ApplyUnderwaterDamping();

            IAmbientCurrentReadModel ambientCurrentReadModel = s_ambientCurrentReadModel;
            if (ambientCurrentReadModel == null ||
                !ambientCurrentReadModel.TrySampleCombinedCurrent(_rigidbody.worldCenterOfMass, out Vector3 sampledCurrent) ||
                !IsFiniteVector(sampledCurrent))
            {
                return;
            }

            if (sampledCurrent.sqrMagnitude <= 0.0001f)
                return;

            float currentLength = EstimateLength3D(sampledCurrent);
            float currentScale = currentLength > 0.0001f
                ? math.min(6f, currentLength) / currentLength
                : 0f;
            Vector3 velocityChange = sampledCurrent * (currentScale * LooseCurrentVelocityInfluence * fdt);
            s_physicsService?.QueueAmbientForce(_rigidbody, velocityChange, ForceMode.VelocityChange, wake: false);

            Vector3 spinAxis = Vector3.Cross(Vector3.up, sampledCurrent);
            float spinAxisLength = EstimateLength3D(spinAxis);
            if (spinAxisLength > 0.0001f)
            {
                float velocityLength = EstimateLength3D(velocityChange);
                s_physicsService?.QueueAmbientTorque(
                    _rigidbody,
                    spinAxis * ((LooseCurrentSpinInfluence * velocityLength) / spinAxisLength),
                    ForceMode.VelocityChange,
                    wake: false);
            }

        }

        internal bool TryGetWorldStatePersistenceIdentity(out long persistenceKey, out long chunkKey)
        {
            persistenceKey = _worldStatePersistenceKey;
            chunkKey = _worldStateChunkKey;
            return _worldStateIdentityAvailable;
        }

        internal bool TryGetWorldStatePersistenceIdentity(out long persistenceKey, out long chunkKey, out long legacyPersistenceKey)
        {
            persistenceKey = _worldStatePersistenceKey;
            chunkKey = _worldStateChunkKey;
            legacyPersistenceKey = _legacyWorldStateIdentityAvailable ? _legacyWorldStatePersistenceKey : 0L;
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

        private void TryRegisterPhysicsBodyTracking()
        {
            if (_registeredPhysicsBodyTracking || _rigidbody == null || _rigidbody.isKinematic || !Application.isPlaying)
                return;

            IPhysicsStateEventService physicsStateEvents = s_physicsStateEvents;
            if (physicsStateEvents == null || !physicsStateEvents.IsInitialized)
                return;

            physicsStateEvents.RegisterBodyStateTracking(_rigidbody);
            _registeredPhysicsBodyTracking = true;
        }

        private void TryUnregisterPhysicsBodyTracking()
        {
            if (!_registeredPhysicsBodyTracking)
                return;

            s_physicsStateEvents?.UnregisterBodyStateTracking(_rigidbody);
            _registeredPhysicsBodyTracking = false;
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
            PublishItemLifecycleCollectedSignal(attempt.AddedQuantity, interactor);

            quantity = attempt.RejectedQuantity;
            if (quantity > 0)
            {
                RebuildInteractTextCache();
                DropOverflow(interactor);
                return true;
            }

            if (_worldStateIdentityAvailable)
            {
                ResolveWorldStateManager()?.RegisterCollectedPickup(_worldStatePersistenceKey, _worldStateChunkKey);
                _worldStateSuppressedByPersistence = true;
            }

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
            SignalBus<ItemAcquiredSignal>.TryPushTracked(in signal, ref s_x001PickupItemSignalPushDropCount);
        }

        private void PublishItemLifecycleCollectedSignal(int addedQuantity, Transform interactor)
        {
            if (itemData == null || addedQuantity <= 0)
                return;

            bool hasInteractorPosition = interactor != null && IsFiniteVector(interactor.position);
            ulong interactorEntityId = hasInteractorPosition ? EntityId.ToULong(interactor.GetEntityId()) : 0ul;
            Vector3 runtimePosition = hasInteractorPosition ? interactor.position : transform.position;
            bool hasRuntimePosition = hasInteractorPosition || IsFiniteVector(runtimePosition);

            ItemLifecycleSignalRoute.TryPublishCollected(
                itemData,
                addedQuantity,
                interactorEntityId,
                runtimePosition,
                hasRuntimePosition);
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

            if (!itemData.TryWriteInteractText(s_localizationText, _cachedInteractTextBuffer, out _cachedInteractTextLength))
                _cachedInteractTextLength = CopySpanToInteractBuffer(UnknownInteractText);
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

            if (!ShouldRetainWorldStateRegistryWhileInactive())
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

        private bool ShouldRetainWorldStateRegistryWhileInactive()
        {
            return persistWorldState && !_isPooledRuntimeInstance;
        }

        internal void ApplyWorldStateSuppression()
        {
            if (!_worldStateIdentityAvailable)
                CaptureWorldStateIdentityCold();

            CaptureWorldStateRestoreBaseline();
            _worldStateSuppressedByPersistence = true;
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        internal bool TryRestoreWorldStateSuppression()
        {
            if (!_worldStateSuppressedByPersistence)
                return false;

            _worldStateSuppressedByPersistence = false;
            if (_worldStateRestoreQuantity > 0)
                quantity = _worldStateRestoreQuantity;

            RebuildInteractTextCache();
            RestoreLootMagnetRuntimeState();
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            return true;
        }

        private void CaptureWorldStateRestoreBaseline()
        {
            if (!ShouldRetainWorldStateRegistryWhileInactive() || quantity <= 0)
                return;

            _worldStateRestoreQuantity = quantity;
        }

        private void ConsumeWorldProxy()
        {
            if (_highlighter != null)
                _highlighter.SetHighlight(false);

            if (_isPooledRuntimeInstance && TryResolveCachedObjectPool(out IObjectPoolService pool))
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

        private void CaptureWorldStateIdentityCold()
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
                _legacyWorldStateIdentityAvailable = false;
                _legacyWorldStatePersistenceKey = 0L;
                return;
            }

            _worldStateAnchorPosition = anchorPosition;
            _worldStateIdentityAvailable = persistWorldState &&
                                           !_isPooledRuntimeInstance &&
                                           WorldPickupStateCodec.TryBuildIdentity(
                                               transform,
                                               gameObject.scene,
                                               itemData,
                                               stableWorldStateId,
                                               _worldStateAnchorPosition,
                                               out _worldStatePersistenceKey,
                                               out _worldStateChunkKey);

            if (_worldStateIdentityAvailable)
            {
                _legacyWorldStateIdentityAvailable = WorldPickupStateCodec.TryBuildLegacyIdentity(
                    transform,
                    gameObject.scene,
                    itemData,
                    _worldStateAnchorPosition,
                    out _legacyWorldStatePersistenceKey,
                    out long _) &&
                    _legacyWorldStatePersistenceKey != _worldStatePersistenceKey;
                return;
            }

            _worldStatePersistenceKey = 0L;
            _worldStateChunkKey = 0L;
            _legacyWorldStateIdentityAvailable = false;
            _legacyWorldStatePersistenceKey = 0L;
        }

        private void InvalidateWorldStateIdentity()
        {
            _worldStateIdentityResolved = false;
            _worldStateIdentityAvailable = false;
            _worldStatePersistenceKey = 0L;
            _worldStateChunkKey = 0L;
            _legacyWorldStateIdentityAvailable = false;
            _legacyWorldStatePersistenceKey = 0L;
            _worldStateAnchorPosition = default;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            InvalidateWorldStateIdentity();

            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (!persistWorldState ||
                gameObject == null ||
                !gameObject.scene.IsValid() ||
                string.IsNullOrEmpty(gameObject.scene.path) ||
                !gameObject.scene.path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ||
                TryGetComponent(out ObjectPoolManager.PoolItemMarker _))
            {
                return;
            }

            if (itemData == null || string.IsNullOrWhiteSpace(itemData.PersistentId))
            {
                UnityEngine.Debug.LogError(
                    "[PickupItem] Persistent scene pickup cannot seed stableWorldStateId without item persistent ID.",
                    this);
                return;
            }

            string normalizedStableId = string.IsNullOrWhiteSpace(stableWorldStateId)
                ? string.Empty
                : stableWorldStateId.Trim();
            bool stableIdChanged = false;
            if (string.IsNullOrEmpty(normalizedStableId))
            {
                UnityEditor.Undo.RecordObject(this, "Seed World Pickup Stable ID");
                normalizedStableId = Guid.NewGuid().ToString("N");
                stableWorldStateId = normalizedStableId;
                stableIdChanged = true;
            }
            else if (!string.Equals(stableWorldStateId, normalizedStableId, StringComparison.Ordinal))
            {
                UnityEditor.Undo.RecordObject(this, "Trim World Pickup Stable ID");
                stableWorldStateId = normalizedStableId;
                stableIdChanged = true;
            }

            for (int attempt = 0; attempt < 8 && HasDuplicateStableWorldStateIdInOpenScenes(normalizedStableId); attempt++)
            {
                if (!stableIdChanged)
                    UnityEditor.Undo.RecordObject(this, "Seed World Pickup Stable ID");

                normalizedStableId = Guid.NewGuid().ToString("N");
                stableWorldStateId = normalizedStableId;
                stableIdChanged = true;
            }

            if (HasDuplicateStableWorldStateIdInOpenScenes(normalizedStableId))
            {
                UnityEngine.Debug.LogError(
                    $"[PickupItem] Persistent pickup still has duplicate stableWorldStateId after repair attempts: {normalizedStableId}",
                    this);
            }

            if (stableIdChanged)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
        }

        private bool HasDuplicateStableWorldStateIdInOpenScenes(string normalizedStableId)
        {
            if (string.IsNullOrEmpty(normalizedStableId))
                return false;

            string scenePath = gameObject.scene.path;
            PickupItem[] pickups = UnityEngine.Object.FindObjectsByType<PickupItem>(
                UnityEngine.FindObjectsInactive.Include);

            for (int i = 0; i < pickups.Length; i++)
            {
                PickupItem candidate = pickups[i];
                if (candidate == null || ReferenceEquals(candidate, this))
                    continue;

                if (!candidate.gameObject.scene.IsValid() ||
                    !candidate.persistWorldState ||
                    !string.Equals(candidate.gameObject.scene.path, scenePath, StringComparison.Ordinal))
                {
                    continue;
                }

                if (candidate.TryGetComponent(out ObjectPoolManager.PoolItemMarker _))
                    continue;

                string candidateStableId = string.IsNullOrWhiteSpace(candidate.stableWorldStateId)
                    ? string.Empty
                    : candidate.stableWorldStateId.Trim();
                if (string.Equals(candidateStableId, normalizedStableId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
#endif

        private void RefreshPoolMarkerCacheCold()
        {
            _isPooledRuntimeInstance = TryGetComponent(out ObjectPoolManager.PoolItemMarker _);
        }

        private void RefreshColdRegistryReferences()
        {
            _worldStateManager = Hecton8.Core.GlobalRegistry.WorldState;
            s_worldStateManager = _worldStateManager;
            s_playerRuntimeContext = Hecton8.Core.GlobalRegistry.Player;
            s_playerInventoryService = Hecton8.Core.GlobalRegistry.PlayerInventory;
            s_physicsService = Hecton8.Core.GlobalRegistry.Physics;
            s_physicsStateEvents = Hecton8.Core.GlobalRegistry.PhysicsStateEvents;
            s_ambientCurrentReadModel = Hecton8.Core.GlobalRegistry.AmbientCurrent;
            CacheObjectPoolService(null);
            s_localizationText = Hecton8.Core.GlobalRegistry.LocalizationText;
            TryRegisterStaticHotSwapListener();
            RefreshCachedPlayerMovement();
        }

        private static void CacheObjectPoolService(ObjectPoolManager candidate)
        {
            ObjectPoolManager pool = candidate;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(pool) ||
                ObjectPoolManager.TryResolveActiveRuntime(ref pool))
            {
                s_objectPool = pool;
                return;
            }

            s_objectPool = null;
        }

        private static bool TryResolveCachedObjectPool(out IObjectPoolService pool)
        {
            ObjectPoolManager cached = s_objectPool as ObjectPoolManager;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached))
            {
                pool = cached;
                return true;
            }

            ObjectPoolManager resolved = cached;
            if (ObjectPoolManager.TryResolveActiveRuntime(ref resolved))
            {
                s_objectPool = resolved;
                pool = resolved;
                return true;
            }

            s_objectPool = null;
            pool = null;
            return false;
        }

        private static void TryRegisterStaticHotSwapListener()
        {
            if (s_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            Hecton8.Core.GlobalRegistry.TryUnregisterHotSwapListener(s_hotSwapListener);
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
                    case GlobalRegistryServiceSlot.PhysicsStateManager:
                        s_physicsStateEvents = currentService as IPhysicsStateEventService;
                        break;
                    case GlobalRegistryServiceSlot.FluidRuntime:
                        s_ambientCurrentReadModel = currentService as IAmbientCurrentReadModel;
                        break;
                    case GlobalRegistryServiceSlot.ObjectPool:
                        CacheObjectPoolService(currentService as ObjectPoolManager);
                        break;
                    case GlobalRegistryServiceSlot.LocalizationRuntime:
                        s_localizationText = currentService as ILocalizationTextReadModel;
                        break;
                    case GlobalRegistryServiceSlot.WorldStateRuntime:
                        s_worldStateManager = currentService as WorldStateManager;
                        break;
                }
            }
        }

        private WorldStateManager ResolveWorldStateManager()
        {
            WorldStateManager manager = s_worldStateManager;
            if (manager == null)
            {
                manager = Hecton8.Core.GlobalRegistry.WorldState;
                s_worldStateManager = manager;
            }

            _worldStateManager = manager;
            return manager;
        }

        private void RefreshCachedPlayerMovement()
        {
            WorldStateManager worldStateManager = ResolveWorldStateManager();
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
