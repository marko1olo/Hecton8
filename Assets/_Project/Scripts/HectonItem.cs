// ============================================================================
// HECTON-8 - HectonItem.cs
// World pickup item. Implements IInteractable.
// Uses data-driven ItemData metadata.
//
// CHANGE v2:
//   Added SetItemData(ItemData, int) for programmatic initialization from
//   BaseModule.Deconstruct() spawns. A single worldItemPrefab can represent
//   multiple resource item records.
//
// CHANGE v3.1 (POOL-SAFE SETTLE):
//   Removed async Awaitable SettleAndSleepAsync because destroyCancellationToken
//   does not fire on SetActive(false) pooled despawn. Replaced with an
//   IFixedTickable state machine and timer. Zero GC.
//   OnDisable resets state for pooled reuse.
// ============================================================================

using System;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Inventory;
using Hecton8.Interaction;
using Hecton8.SaveSystem;
using Hecton8.World;
using Hecton.Localization;
using BuoyancyObject = Hecton8.Physics.BuoyancyObject;

namespace Hecton8.Items
{
    using Unity.Mathematics;
    using UnityEngine;

    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(InteractionHighlighter))]
    [DisallowMultipleComponent]
    public class HectonItem : MonoBehaviour, IInteractable, IInteractableTextProvider, IFixedTickable, IInventoryPickupSource, IInventoryPickupPreviewSource, IInteractionVulnerabilitySource, Hecton8.Core.Contracts.IPhysicsImpactMaterialProvider, ILocalizationLanguageChangedListener
    {
        private static int s_x001HectonItemSignalPushDropCount;
        private const float OverflowScatterImpulse = 2.5f;
        private const float OverflowScatterLiftImpulse = 1.2f;
        private const float OverflowScatterTorqueImpulse = 0.35f;
        private const float DeepSeaSeawaterDensityKgPerM3 = HectonPhysicsContract.WaterDensityKgPerCubicMeterConst;
        private const float LooseItemBuoyancyAngularDragMultiplier = 2.75f;
        private const ushort DefaultQualityMilli = 1000;
        private static IPlayerRuntimeContext s_playerRuntimeContext;
        private static IPlayerInventoryService s_playerInventoryService;
        private static IPhysicsService s_physicsService;
        private static IPhysicsStateEventService s_physicsStateEvents;
        private static IObjectPoolService s_objectPool;
        private static ILocalizationTextReadModel s_localizationText;
        // COLD ALLOC: StaticRegistryHotSwapListener[1] - shared pickup service cache rebind bridge - owner: HectonItem
        private static readonly StaticRegistryHotSwapListener s_hotSwapListener = new StaticRegistryHotSwapListener();
        private static bool s_hotSwapListenerRegistered;
        // Data
        [Header("Item Configuration")]
        [SerializeField] private ItemData itemData;
        [SerializeField] private int      quantity = 1;

        // Settle Config
        // Delay before the first Rigidbody sleep attempt.
        private const float SettleDelay       = 2.0f;
        // Delay before the retry sleep attempt.
        private const float SettleRetryDelay  = 1.0f;
        // Squared velocity threshold for sleeping.
        private const float SleepVelocitySqr  = 0.01f;

        // Settle State
        /// <summary>
        /// Rigidbody sleep state machine phases.
        /// Idle: no pending tick.
        /// Waiting: initial SettleDelay window.
        /// Retrying: retry SettleRetryDelay window.
        /// Done: sleeping succeeded or settling was abandoned.
        /// </summary>
        private enum SettlePhase : byte
        {
            Idle,
            Waiting,
            Retrying,
            Done
        }

        private SettlePhase _settlePhase;
        private float       _settleTimer;
        private bool        _isTickRegistered;
        private bool        _registeredPhysicsBodyTracking;

        // Cached
        private InteractionHighlighter _highlighter;
        private Rigidbody _rb;
        private BuoyancyObject _buoyancy;
        private Collider _collider;
        private PhysicsMaterial _defaultColliderMaterial;
        private const int InteractTextBufferCapacity = 128;
        private static readonly char[] UnknownInteractText = { '?', '?', '?' };
        private readonly char[] _cachedInteractTextBuffer = new char[InteractTextBufferCapacity];
        private int _cachedInteractTextLength;
        private int _cachedItemHashId;
        private PersistentWorldRegistry _persistentWorldRegistry;
        private int _persistentWorldRecordIndex = -1;
        private bool _isPooledRuntimeInstance;
        private ulong _geneticsMask;
        private ushort _qualityMilli = DefaultQualityMilli;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_playerRuntimeContext = null;
            s_playerInventoryService = null;
            s_physicsService = null;
            s_physicsStateEvents = null;
            s_objectPool = null;
            s_localizationText = null;
            s_hotSwapListenerRegistered = false;
        }

        // Lifecycle
        private void Awake()
        {
            CacheColdRegistryReferences();
            TryGetComponent(out _highlighter);
            TryGetComponent(out _rb);
            TryGetComponent(out _collider);
            TryGetComponent(out _buoyancy);
            RefreshPoolMarkerCacheCold();
            _defaultColliderMaterial = _collider != null ? _collider.sharedMaterial : null;
            ApplyPhysicalMetadata();
            ConfigureWaterDynamicsFromData();
            RefreshCachedItemHash();

        }

        // Pool-Safe Settle (v3.1)
        private void OnEnable()
        {
            CacheColdRegistryReferences();
            RefreshPoolMarkerCacheCold();
            InteractableRegistry.RegisterTree(this);
            LocalizationEvents.RegisterLanguageListener(this);
            RebuildInteractTextCache();

            if (_rb != null)
            {
                TryRegisterPhysicsBodyTracking();
                _rb.WakeUp();
                BeginSettle();
            }
        }

        private void Start()
        {
            CacheColdRegistryReferences();
        }

        private void OnDisable()
        {
            InteractableRegistry.InvalidateTree(this);
            LocalizationEvents.UnregisterLanguageListener(this);

            // Guaranteed unsubscribe on pooled deactivation.
            // Reset phase so the next OnEnable starts clean.
            StopSettle();
            TryUnregisterPhysicsBodyTracking();
            ClearPersistentWorldRecord();
        }

        private void OnDestroy()
        {
            InteractableRegistry.InvalidateTree(this);
        }

        public void FixedTick(float fixedDeltaTime)
        {
            TryRegisterPhysicsBodyTracking();

            if (_settlePhase == SettlePhase.Idle || _settlePhase == SettlePhase.Done)
                return;

            TickSettle(fixedDeltaTime);
        }

        private void TryRegisterPhysicsBodyTracking()
        {
            if (_registeredPhysicsBodyTracking || _rb == null || _rb.isKinematic || !Application.isPlaying)
                return;

            IPhysicsStateEventService physicsStateEvents = s_physicsStateEvents;
            if (physicsStateEvents == null || !physicsStateEvents.IsInitialized)
                return;

            physicsStateEvents.RegisterBodyStateTracking(_rb);
            _registeredPhysicsBodyTracking = true;
        }

        private void TryUnregisterPhysicsBodyTracking()
        {
            if (!_registeredPhysicsBodyTracking)
                return;

            s_physicsStateEvents?.UnregisterBodyStateTracking(_rb);
            _registeredPhysicsBodyTracking = false;
        }

        private void TickSettle(float deltaTime)
        {
            switch (_settlePhase)
            {
                case SettlePhase.Waiting:
                    _settleTimer -= deltaTime;
                    if (_settleTimer <= 0f)
                    {
                        if (TrySleepRigidbody())
                        {
                            FinishSettle();
                        }
                        else
                        {
                            // Still moving; allow one retry.
                            _settlePhase = SettlePhase.Retrying;
                            _settleTimer = SettleRetryDelay;
                        }
                    }
                    break;

                case SettlePhase.Retrying:
                    _settleTimer -= deltaTime;
                    if (_settleTimer <= 0f)
                    {
                        TrySleepRigidbody(); // Attempt once; final result is non-blocking.
                        FinishSettle();
                    }
                    break;

                default:
                    _settleTimer = 0f;
                    break;
            }
        }

        /// <summary>
        /// Attempts to sleep the Rigidbody when velocity is low enough.
        /// Returns true when sleep succeeded or no Rigidbody exists.
        /// </summary>
        private bool TrySleepRigidbody()
        {
            if (_rb == null) return true;

            if (_rb.linearVelocity.sqrMagnitude < SleepVelocitySqr)
            {
                _rb.Sleep();
                return true;
            }

            return false;
        }

        private void BeginSettle()
        {
            _settlePhase = SettlePhase.Waiting;
            _settleTimer = SettleDelay;
            StartTicking();
        }

        private void FinishSettle()
        {
            _settlePhase = SettlePhase.Done;
            _settleTimer = 0f;
        }

        private void StopSettle()
        {
            _settlePhase = SettlePhase.Idle;
            _settleTimer = 0f;
            StopTicking();
        }

        private void StartTicking()
        {
            if (_isTickRegistered) return;
            if (!Application.isPlaying || GlobalRegistry.TickDispatcher == null) return;

            _isTickRegistered = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Environment);
        }

        private void StopTicking()
        {
            if (!_isTickRegistered) return;

            GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
            _isTickRegistered = false;
        }

        // Public API

        /// <summary>
        /// Programmatic item-data initialization.
        /// Called by BaseModule.Deconstruct() spawn paths to bind a concrete
        /// resource record onto the shared worldItemPrefab.
        ///
        /// Safe to call repeatedly; the latest call overwrites item state.
        /// </summary>
        /// <param name="data">ItemData ScriptableObject payload.</param>
        /// <param name="qty">Stack quantity.</param>
        public void SetItemData(ItemData data, int qty)
        {
            SetItemData(data, qty, 0UL, DefaultQualityMilli);
        }

        /// <summary>Programmatic item initialization with persisted mutable item state.</summary>
        public void SetItemData(ItemData data, int qty, uint geneticsMask, ushort qualityMilli)
        {
            SetItemData(data, qty, (ulong)geneticsMask, qualityMilli);
        }

        /// <summary>Programmatic item initialization with persisted mutable item state.</summary>
        public void SetItemData(ItemData data, int qty, ulong geneticsMask, ushort qualityMilli)
        {
            itemData = data;
            quantity = qty > 0 ? qty : 1;
            _geneticsMask = geneticsMask;
            _qualityMilli = NormalizeQualityMilli(qualityMilli);
            RefreshCachedItemHash();
            ApplyPhysicalMetadata();
            ConfigureWaterDynamicsFromData();
            RebuildInteractTextCache();
        }

        public bool SetItemByHash(ItemCatalog catalog, int itemHashId, int qty)
        {
            if (catalog == null || itemHashId == 0)
                return false;

            ItemData resolvedItem = catalog.FindByHash(itemHashId);
            if (resolvedItem == null)
                return false;

            SetItemData(resolvedItem, qty);
            return true;
        }

        /// <summary>Current item data.</summary>
        public ItemData Data => itemData;

        /// <summary>Current stack quantity.</summary>
        public int Quantity => quantity;
        public int ItemHashId => _cachedItemHashId;
        /// <summary>Persisted genetics payload carried by biological seed world items.</summary>
        public ulong GeneticsMask => _geneticsMask;
        /// <summary>Persisted item quality in milli-normalized units.</summary>
        public ushort QualityMilli => _qualityMilli != 0 ? _qualityMilli : DefaultQualityMilli;
        public uint VulnerabilityMask => itemData != null ? itemData.VulnerabilityMask : 0u;
        public byte ImpactAudioMaterialId => itemData != null ? itemData.AudioMaterialByte : (byte)ItemAudioMaterialId.Organic;

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

        private void ConfigureWaterDynamicsFromData()
        {
            if (itemData == null || _rb == null)
                return;

            if (_buoyancy == null)
            {
                if (!TryGetComponent(out _buoyancy))
                {
                    // Player-build construction path: no authored/bootstrap instance reachable.
                    // Must construct in player builds when bootstrap reorders or skips registration.
                    _buoyancy = gameObject.AddComponent<BuoyancyObject>();
                }
            }

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

        private void ApplyPhysicalMetadata()
        {
            if (_rb != null && itemData != null)
                _rb.mass = itemData.MassKg;

            if (_collider == null)
                return;

            _collider.sharedMaterial = itemData != null && itemData.WorldPhysicMaterial != null
                ? itemData.WorldPhysicMaterial
                : _defaultColliderMaterial;
        }

        private void RefreshCachedItemHash()
        {
            _cachedItemHashId = itemData != null
                ? LocHash.Compute(itemData.PersistentId)
                : 0;
        }

        // IInteractable
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

            PublishItemAcquiredSignal(attempt.AddedQuantity, interactor);

            quantity = attempt.RejectedQuantity;
            if (quantity > 0)
            {
                RebuildInteractTextCache();
                DropOverflow(interactor);
                return true;
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
                Quantity = (ushort)Mathf.Clamp(addedQuantity, 0, ushort.MaxValue),
                SourceKind = InventoryPickupSignalConstants.ItemSourceManualPickup,
                Flags = InventoryPickupSignalConstants.SignalFlagManualPickup,
                Frame = ResolveCurrentFrameId()
            };
            SignalBus<ItemAcquiredSignal>.TryPushTracked(in signal, ref s_x001HectonItemSignalPushDropCount);
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

        private static void CacheColdRegistryReferences()
        {
            s_playerRuntimeContext = GlobalRegistry.Player;
            s_playerInventoryService = GlobalRegistry.PlayerInventory;
            s_physicsService = GlobalRegistry.Physics;
            s_physicsStateEvents = GlobalRegistry.PhysicsStateEvents;
            CacheObjectPoolService(null);
            s_localizationText = GlobalRegistry.LocalizationText;
            TryRegisterStaticHotSwapListener();
        }

        private static void CacheObjectPoolService(ObjectPoolManager candidate)
        {
            if (!ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(candidate))
            {
                s_objectPool = null;
                return;
            }

            s_objectPool = candidate;
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

        private static bool TryResolvePoolForInstance(GameObject instance, IObjectPoolService preferredPool, out IObjectPoolService pool)
        {
            return ObjectPoolManager.TryResolvePoolForInstance(instance, preferredPool, out pool);
        }

        private static void TryRegisterStaticHotSwapListener()
        {
            if (s_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(s_hotSwapListener);
            s_hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(s_hotSwapListener);
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
                    case GlobalRegistryServiceSlot.ObjectPool:
                        CacheObjectPoolService(currentService as ObjectPoolManager);
                        break;
                    case GlobalRegistryServiceSlot.LocalizationRuntime:
                        s_localizationText = currentService as ILocalizationTextReadModel;
                        break;
                }
            }
        }

        private static ushort NormalizeQualityMilli(ushort qualityMilli)
        {
            if (qualityMilli == 0)
                return DefaultQualityMilli;

            return (ushort)Mathf.Clamp((int)qualityMilli, 0, DefaultQualityMilli);
        }

        public string GetInteractText()
        {
            return itemData != null ? itemData.GetInteractText() : "UNKNOWN ITEM";
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

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }


        private void HandleLanguageChanged(GameLanguage language)
        {
            RebuildInteractTextCache();
        }

        private void ConsumeWorldProxy()
        {
            if (_highlighter != null)
                _highlighter.SetHighlight(false);

            TryResolveCachedObjectPool(out IObjectPoolService pool);
            if (_isPooledRuntimeInstance &&
                TryResolvePoolForInstance(gameObject, pool, out IObjectPoolService ownerPool))
            {
                ownerPool.Despawn(gameObject);
                return;
            }

            gameObject.SetActive(false);
        }

        private void RefreshPoolMarkerCacheCold()
        {
            _isPooledRuntimeInstance = TryGetComponent(out ObjectPoolManager.PoolItemMarker _);
        }

        private void DropOverflow(Transform interactor)
        {
            if (_rb == null || _rb.isKinematic)
                return;

            IPhysicsService physicsService = s_physicsService;
            if (physicsService == null)
                return;

            Vector3 scatterDirection = ResolveScatterDirection(interactor);
            Vector3 impulse = scatterDirection * OverflowScatterImpulse;
            impulse.y += OverflowScatterLiftImpulse;

            if (!IsFiniteVector(impulse))
                return;

            _rb.WakeUp();
            physicsService.QueueForce(_rb, impulse, ForceMode.Impulse);

            Vector3 torqueAxis = Vector3.Cross(Vector3.up, scatterDirection);
            if (torqueAxis.sqrMagnitude <= 0.0001f)
                torqueAxis = Vector3.right;

            Vector3 torque = ResolveDominantPlanarDirection(torqueAxis) * OverflowScatterTorqueImpulse;
            if (IsFiniteVector(torque))
                physicsService.QueueTorque(_rb, torque, ForceMode.Impulse);
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
                    if (scatterDirection.sqrMagnitude > 0.0001f)
                        return ResolveDominantPlanarDirection(scatterDirection);
                }

                Vector3 fallbackForward = -interactor.forward;
                fallbackForward.y = 0f;
                if (IsFiniteVector(fallbackForward) && fallbackForward.sqrMagnitude > 0.0001f)
                    return ResolveDominantPlanarDirection(fallbackForward);
            }

            return Vector3.forward;
        }

        private static Vector3 ResolveDominantPlanarDirection(Vector3 value)
        {
            float absX = value.x < 0f ? -value.x : value.x;
            float absZ = value.z < 0f ? -value.z : value.z;
            if (absX >= absZ)
                return value.x >= 0f ? Vector3.right : Vector3.left;

            return value.z >= 0f ? Vector3.forward : Vector3.back;
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

        // Editor
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (quantity < 1) quantity = 1;

            if (!Application.isPlaying)
            {
                TryGetComponent(out _rb);
                TryGetComponent(out _buoyancy);
                ConfigureWaterDynamicsFromData();
                RebuildInteractTextCache();
            }
        }
#endif
    }
}
