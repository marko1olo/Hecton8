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
//   ITickable state machine and timer. Zero GC.
//   OnDisable resets state for pooled reuse.
// ============================================================================

using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Inventory;
using Hecton8.Interaction;
using Hecton8.Physics;
using Hecton8.SaveSystem;
using Hecton8.World;
using Hecton.Localization;

namespace Hecton8.Items
{
    using UnityEngine;

    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(InteractionHighlighter))]
    [DisallowMultipleComponent]
    public class HectonItem : MonoBehaviour, IInteractable, ITickable, IUpdatable, IInventoryPickupSource, IInteractionVulnerabilitySource, IPhysicsImpactMaterialProvider, ILocalizationLanguageChangedListener
    {
        private const float OverflowScatterImpulse = 2.5f;
        private const float OverflowScatterLiftImpulse = 1.2f;
        private const float OverflowScatterTorqueImpulse = 0.35f;
        private const float DeepSeaSeawaterDensityKgPerM3 = 1025f;
        private const float LooseItemBuoyancyAngularDragMultiplier = 2.75f;
        private const ushort DefaultQualityMilli = 1000;
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

        // Cached
        private InteractionHighlighter _highlighter;
        private Rigidbody _rb;
        private BuoyancyObject _buoyancy;
        private Collider _collider;
        private PhysicsMaterial _defaultColliderMaterial;
        private string _cachedInteractText = "???";
        private int _cachedItemHashId;
        private PersistentWorldRegistry _persistentWorldRegistry;
        private int _persistentWorldRecordIndex = -1;
        private ulong _geneticsMask;
        private ushort _qualityMilli = DefaultQualityMilli;

        // Lifecycle
        private void Awake()
        {
            _highlighter = GetComponent<InteractionHighlighter>();
            _rb = GetComponent<Rigidbody>();
            _collider = GetComponent<Collider>();
            _buoyancy = GetComponent<BuoyancyObject>();
            _defaultColliderMaterial = _collider != null ? _collider.sharedMaterial : null;
            ApplyPhysicalMetadata();
            ConfigureWaterDynamicsFromData();
            RefreshCachedItemHash();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (itemData == null)
                Debug.LogError($"[HectonItem] ItemData is not assigned on {gameObject.name}.", this);
#endif
        }

        // Pool-Safe Settle (v3.1)
        private void OnEnable()
        {
            LocalizationEvents.RegisterLanguageListener(this);
            RebuildInteractTextCache();

            if (_rb != null)
            {
                if (!_rb.isKinematic)
                    GlobalPhysicsStateManager.RegisterTrackedBody(_rb);

                _rb.WakeUp();
                BeginSettle();
            }
        }

        private void OnDisable()
        {
            InteractableRegistry.InvalidateTree(this);
            LocalizationEvents.UnregisterLanguageListener(this);

            // Guaranteed unsubscribe on pooled deactivation.
            // Reset phase so the next OnEnable starts clean.
            StopSettle();
            if (_rb != null)
                GlobalPhysicsStateManager.UnregisterTrackedBody(_rb);
            ClearPersistentWorldRecord();
        }

        // ITickable
        public void Tick(float deltaTime)
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
                    // Idle or Done should not tick, but unregister defensively.
                    StopSettle();
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
            StopTicking();
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
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null) return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _isTickRegistered = GlobalRegistry.Updatables.Contains(this);
        }

        private void StopTicking()
        {
            if (!_isTickRegistered) return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
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
                _buoyancy = GetComponent<BuoyancyObject>() ?? gameObject.AddComponent<BuoyancyObject>();

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

        private void PublishItemAcquiredSignal(int addedQuantity, Transform interactor)
        {
            if (addedQuantity <= 0 || _cachedItemHashId == 0)
                return;

            Vector3 signalPosition = transform.position;
            if (interactor != null && IsFiniteVector(interactor.position))
                signalPosition = interactor.position;

            ItemAcquiredSignal signal = new ItemAcquiredSignal
            {
                PositionAup = IsFiniteVector(signalPosition)
                    ? AbsoluteUniversePosition.FromRuntimePosition(signalPosition)
                    : default,
                ItemHash = unchecked((uint)_cachedItemHashId),
                OreHash = unchecked((uint)_cachedItemHashId),
                Quantity = (ushort)Mathf.Clamp(addedQuantity, 0, ushort.MaxValue),
                SourceKind = InventoryPickupSignalConstants.ItemSourceManualPickup,
                Flags = InventoryPickupSignalConstants.SignalFlagManualPickup,
                Frame = unchecked((uint)Time.frameCount)
            };
            GlobalSignals.Publish(in signal);
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
                _cachedInteractText = "???";
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
            if (_rb == null || _rb.isKinematic)
                return;

            Vector3 scatterDirection = ResolveScatterDirection(interactor);
            Vector3 impulse = scatterDirection * OverflowScatterImpulse;
            impulse.y += OverflowScatterLiftImpulse;

            if (!IsFiniteVector(impulse))
                return;

            _rb.WakeUp();
            PhysicsForceRouter.QueueForce(_rb, impulse, ForceMode.Impulse);

            Vector3 torqueAxis = Vector3.Cross(Vector3.up, scatterDirection);
            if (torqueAxis.sqrMagnitude <= 0.0001f)
                torqueAxis = Vector3.right;

            Vector3 torque = ResolveDominantPlanarDirection(torqueAxis) * OverflowScatterTorqueImpulse;
            if (IsFiniteVector(torque))
                PhysicsForceRouter.QueueTorque(_rb, torque, ForceMode.Impulse);
        }

        private Vector3 ResolveScatterDirection(Transform interactor)
        {
            if (interactor != null)
            {
                Vector3 scatterDirection = transform.position - interactor.position;
                scatterDirection.y = 0f;
                if (scatterDirection.sqrMagnitude > 0.0001f)
                    return ResolveDominantPlanarDirection(scatterDirection);

                Vector3 fallbackForward = -interactor.forward;
                fallbackForward.y = 0f;
                if (fallbackForward.sqrMagnitude > 0.0001f)
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

        // Editor
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (quantity < 1) quantity = 1;

            if (itemData != null && !Application.isPlaying)
                gameObject.name = $"Item_{itemData.itemName}";

            if (!Application.isPlaying)
            {
                _rb = GetComponent<Rigidbody>();
                _buoyancy = GetComponent<BuoyancyObject>();
                ConfigureWaterDynamicsFromData();
                RebuildInteractTextCache();
            }
        }
#endif
    }
}
