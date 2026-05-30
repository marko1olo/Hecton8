using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Interaction;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Power;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Economy
{
    /// <summary>
    /// Powered base recycler with a local inventory buffer. Processing only consumes items that have been
    /// explicitly loaded into the module; it never auto-pulls from the player's inventory during interaction.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(PowerNode))]
    [AddComponentMenu("Hecton8/Economy/Resource Recycler Module")]
    public sealed class ResourceRecyclerModule : MonoBehaviour, ITickable, IUpdatable, IPowerComponent, IInteractable, IInteractableTextProvider, IGlobalRegistryHotSwapListener
    {
        private const string DefaultReadyText = "Start Recycler Batch";
        private const string DefaultEmptyText = "Recycler Buffer Empty";
        private const string DefaultBusyText = "Recycler Processing";
        private const string DefaultPausedText = "Recycler Paused";
        private const string DefaultCollectText = "Collect Recycled Output";
        private const int MaxBufferSlots = 8;
        private const int MaxActiveModuleCapacity = 128;

        private static readonly ResourceRecyclerModule[] s_ActiveModules = new ResourceRecyclerModule[MaxActiveModuleCapacity];
        private static int s_ActiveModuleCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            for (int i = 0; i < s_ActiveModuleCount; i++)
                s_ActiveModules[i] = null;

            s_ActiveModuleCount = 0;
        }

        [Header("── Process Settings ───────────────────────")]
        [Tooltip("Baseline recycle duration for one item batch.")]
        [SerializeField, Range(1f, 30f)] private float recycleDurationSeconds = 6f;

        [Tooltip("Continuous grid draw while a recycle batch is being purified.")]
        [SerializeField, Range(0f, 300f)] private float activePowerDraw = 140f;

        [Tooltip("One-shot grid energy burst consumed when the purification cycle starts.")]
        [SerializeField, Range(0f, 100f)] private float startupBurstPowerCost = 16f;

        [Tooltip("Power-shedding priority. Lower is more critical.")]
        [SerializeField, Range(0, 100)] private int powerPriority = 55;

        [Header("── Buffer ───────────────────────────────")]
        [Tooltip("Active slot count inside the recycler's local inventory buffer.")]
        [SerializeField, Range(4, MaxBufferSlots)] private int bufferSlotCount = 6;

        [Header("── Diagnostics ───────────────────────────")]
        #pragma warning disable CS0414
        [SerializeField] private bool _debugHasPower = true;
        [SerializeField] private bool _debugIsProcessing;
        [SerializeField] private bool _debugHasPendingOutput;
        #pragma warning restore CS0414
        [SerializeField] private string _debugActiveItemId;
        [SerializeField] private int _debugPendingYieldUnits;
        [SerializeField] private int _debugProcessedBatchCount;
        [SerializeField] private int _debugBufferedItemCount;

        private PowerNode _powerNode;
        private IPlayerInventoryService _inventoryService;
        private IResourceScarcityReadModel _scarcityReadModel;
        private PlayerInventory _cachedInventory;
        private bool _registered;
        private bool _hasPower = true;
        private bool _isProcessing;
        private bool _hasPendingOutput;
        private float _processTimer;
        private float _currentDuration = 1f;
        private float _activePowerMultiplier = 1f;
        private ItemData _activeSourceItem;
        private ResourceStack[] _pendingYield;
        private int _pendingYieldCount;
        private int _pendingYieldUnits;
        private readonly ResourceStack[] _pendingYieldScratch = new ResourceStack[ScrapManager.MaxRecycleYieldSlots];
        private int _processedBatchCount;
        private readonly ItemData[] _bufferItems = new ItemData[MaxBufferSlots];
        private readonly int[] _bufferQuantities = new int[MaxBufferSlots];
        private int _bufferedItemCount;

        /// <summary>Active recycler count used by world pollution telemetry.</summary>
        internal static int ActiveModuleCount => s_ActiveModuleCount;

        /// <summary>Returns an active recycler by index without exposing mutable registry storage.</summary>
        internal static ResourceRecyclerModule GetActiveModuleAt(int index)
        {
            return index >= 0 && index < s_ActiveModuleCount ? s_ActiveModules[index] : null;
        }

        /// <summary>True while the recycler is actively drawing process power.</summary>
        internal bool IsProcessing => _isProcessing;

        /// <summary>Total completed recycle batches since scene load.</summary>
        internal int TotalProcessedBatchCount => _processedBatchCount;

        /// <summary>True when at least one buffered recyclable item is waiting locally.</summary>
        public bool HasBufferedInput => _bufferedItemCount > 0;

        /// <summary>Dynamic active load injected into the power grid while processing is underway.</summary>
        public float PowerRating => _isProcessing ? -activePowerDraw * _activePowerMultiplier : 0f;

        /// <summary>Power-shedding priority for the recycler endpoint.</summary>
        public int PowerPriority => powerPriority;

        /// <summary>Cached grid availability propagated by the shared power grid.</summary>
        public bool HasPower => _hasPower;

        private void Awake()
        {
            TryGetComponent(out _powerNode);
            ClampBufferSlotCount();
        }

        private void OnEnable()
        {
            InteractableRegistry.RegisterTree(this);
            if (Application.isPlaying)
            {
                CacheRuntimeServicesCold();
                GlobalRegistry.TryRegisterHotSwapListener(this);
            }

            RegisterModuleInstance();
            BaseLogisticsNetwork.RegisterRecycler(this, _powerNode);
            TryRegister();
        }

        private void OnDisable()
        {
            InteractableRegistry.InvalidateTree(this);
            TryUnregister();
            GlobalRegistry.TryUnregisterHotSwapListener(this);
            BaseLogisticsNetwork.UnregisterRecycler(this);
            UnregisterModuleInstance();
        }

        private void OnDestroy()
        {
            InteractableRegistry.InvalidateTree(this);
        }

        public void Tick(float dt)
        {
            if (!_isProcessing || !_hasPower)
                return;

            _processTimer += dt;
            if (_processTimer < _currentDuration)
                return;

            _isProcessing = false;
            _debugIsProcessing = false;
            _hasPendingOutput = _pendingYield != null && _pendingYieldCount > 0;
            _debugHasPendingOutput = _hasPendingOutput;
            NotifyGridBalanceChanged();
        }

        void IInteractable.OnHoverStart()
        {
        }

        void IInteractable.OnHoverEnd()
        {
        }

        void IInteractable.Interact(Transform interactor)
        {
            PlayerInventory inventory = EnsureCachedInventory();
            if (_hasPendingOutput)
            {
                TryDeliverPendingYield(inventory);
                return;
            }

            if (_isProcessing || !_hasPower)
                return;

            TryStartBufferedRecycle();
        }

        string IInteractable.GetInteractText()
        {
            return ResolveInteractText();
        }

        private string ResolveInteractText()
        {
            if (_hasPendingOutput)
                return DefaultCollectText;

            if (_isProcessing)
                return _hasPower ? DefaultBusyText : DefaultPausedText;

            if (!_hasPower)
                return DefaultPausedText;

            return _bufferedItemCount > 0 ? DefaultReadyText : DefaultEmptyText;
        }

        public bool TryCopyInteractText(System.Span<char> destination, out int length)
        {
            return InteractableTextCopy.TryCopy(ResolveInteractText(), destination, out length);
        }

        public void OnPowerStatusChanged(bool hasPower)
        {
            _hasPower = hasPower;
            _debugHasPower = hasPower;
        }

        /// <summary>
        /// UI / interaction bridge: moves recyclable items from player inventory into the recycler's local buffer.
        /// This is the supported transfer contract for future drag-drop or PDA inventory wiring.
        /// </summary>
        public bool TryInsertFromInventory(PlayerInventory inventory, ItemData item, int quantity = 1)
        {
            if (inventory == null || item == null || quantity <= 0)
                return false;

            if (!IsRecyclableCandidate(item))
                return false;

            int inserted = 0;
            int targetQuantity = Mathf.Max(1, quantity);
            for (int i = 0; i < targetQuantity; i++)
            {
                if (!TryBufferItem(item))
                    break;

                if (!inventory.TryRemoveQuantity(item.PersistentHashId, 1))
                {
                    RemoveLastBufferedItem(item);
                    break;
                }

                inserted++;
            }

            _debugBufferedItemCount = _bufferedItemCount;
            return inserted > 0;
        }

        /// <summary>
        /// Returns the currently buffered item stack snapshot for lightweight UI inspection.
        /// </summary>
        public int CopyBufferSnapshot(ItemData[] items, int[] quantities)
        {
            if (items == null || quantities == null)
                return 0;

            int maxCount = items.Length < quantities.Length ? items.Length : quantities.Length;
            int copied = 0;
            for (int i = 0; i < bufferSlotCount && copied < maxCount; i++)
            {
                if (_bufferItems[i] == null || _bufferQuantities[i] <= 0)
                    continue;

                items[copied] = _bufferItems[i];
                quantities[copied] = _bufferQuantities[i];
                copied++;
            }

            return copied;
        }

        private void TryRegister()
        {
            if (_registered)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registered = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.PlayerInventory:
                    _inventoryService = currentService as IPlayerInventoryService;
                    _cachedInventory = ReadInventoryFromService(_inventoryService);
                    break;
                case GlobalRegistryServiceSlot.ResourceScarcityRuntime:
                    _scarcityReadModel = currentService as IResourceScarcityReadModel;
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    if (currentService == null)
                    {
                        _registered = false;
                        return;
                    }

                    if (isActiveAndEnabled)
                    {
                        TryUnregister();
                        TryRegister();
                    }
                    break;
            }
        }

        private void RegisterModuleInstance()
        {
            for (int i = 0; i < s_ActiveModuleCount; i++)
            {
                if (ReferenceEquals(s_ActiveModules[i], this))
                    return;
            }

            if (s_ActiveModuleCount >= s_ActiveModules.Length)
                return;

            s_ActiveModules[s_ActiveModuleCount] = this;
            s_ActiveModuleCount++;
        }

        private void UnregisterModuleInstance()
        {
            for (int i = s_ActiveModuleCount - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(s_ActiveModules[i], this))
                    continue;

                int lastIndex = s_ActiveModuleCount - 1;
                s_ActiveModules[i] = s_ActiveModules[lastIndex];
                s_ActiveModules[lastIndex] = null;
                s_ActiveModuleCount--;
                return;
            }
        }

        private PlayerInventory EnsureCachedInventory()
        {
            if (_cachedInventory != null)
                return _cachedInventory;

            _cachedInventory = ReadInventoryFromService(_inventoryService);
            return _cachedInventory;
        }

        private bool TryStartBufferedRecycle()
        {
            if (_isProcessing || !_hasPower || _hasPendingOutput)
                return false;

            if (!TryDequeueNextBufferedItem(out ItemData sourceItem))
                return false;

            if (!ScrapManager.TryBuildRecycleYieldSnapshot(sourceItem, _pendingYieldScratch, out int resolvedYieldCount))
            {
                TryBufferItem(sourceItem);
                _debugBufferedItemCount = _bufferedItemCount;
                return false;
            }

            _activeSourceItem = sourceItem;
            _pendingYield = _pendingYieldScratch;
            _pendingYieldCount = resolvedYieldCount;
            _pendingYieldUnits = ScrapManager.CountYieldUnits(_pendingYield, _pendingYieldCount);
            _debugPendingYieldUnits = _pendingYieldUnits;
            _currentDuration = ResolveRecycleDuration(sourceItem, _pendingYieldUnits);
            _activePowerMultiplier = ResolvePowerMultiplier(sourceItem, _pendingYieldUnits);
            _processTimer = 0f;
            _isProcessing = true;
            _debugIsProcessing = true;
            _debugActiveItemId = sourceItem.PersistentId;
            _debugBufferedItemCount = _bufferedItemCount;

            PowerGrid grid = _powerNode != null ? _powerNode.Grid : null;
            if (grid != null && startupBurstPowerCost > 0f)
                grid.ConsumePower(startupBurstPowerCost);

            NotifyGridBalanceChanged();
            return true;
        }

        private bool TryDeliverPendingYield(PlayerInventory inventory)
        {
            if (!_hasPendingOutput || inventory == null || _pendingYield == null || _pendingYieldCount <= 0)
                return false;

            int grantedStackCount = 0;
            if (!ScrapManager.GrantYield(inventory, _pendingYield, _pendingYieldCount, ref grantedStackCount))
            {
                ScrapManager.RollbackYield(inventory, _pendingYield, _pendingYieldCount, grantedStackCount);
                return false;
            }

            ItemLifecycleSignalRoute.TryPublishRecycled(_activeSourceItem, 1, _pendingYieldUnits);
            _processedBatchCount++;
            _debugProcessedBatchCount = _processedBatchCount;
            ClearPendingOutput();
            return true;
        }

        private bool TryBufferItem(ItemData item)
        {
            if (item == null)
                return false;

            ClampBufferSlotCount();

            for (int i = 0; i < bufferSlotCount; i++)
            {
                if (ReferenceEquals(_bufferItems[i], item))
                {
                    _bufferQuantities[i]++;
                    _bufferedItemCount++;
                    return true;
                }
            }

            for (int i = 0; i < bufferSlotCount; i++)
            {
                if (_bufferItems[i] != null)
                    continue;

                _bufferItems[i] = item;
                _bufferQuantities[i] = 1;
                _bufferedItemCount++;
                return true;
            }

            return false;
        }

        private void RemoveLastBufferedItem(ItemData item)
        {
            if (item == null)
                return;

            for (int i = bufferSlotCount - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(_bufferItems[i], item) || _bufferQuantities[i] <= 0)
                    continue;

                _bufferQuantities[i]--;
                _bufferedItemCount--;
                if (_bufferQuantities[i] <= 0)
                {
                    _bufferQuantities[i] = 0;
                    _bufferItems[i] = null;
                }

                return;
            }
        }

        private bool TryDequeueNextBufferedItem(out ItemData item)
        {
            item = null;
            if (_bufferedItemCount <= 0)
                return false;

            for (int i = 0; i < bufferSlotCount; i++)
            {
                if (_bufferItems[i] == null || _bufferQuantities[i] <= 0)
                    continue;

                item = _bufferItems[i];
                _bufferQuantities[i]--;
                _bufferedItemCount--;
                if (_bufferQuantities[i] <= 0)
                {
                    _bufferQuantities[i] = 0;
                    _bufferItems[i] = null;
                }

                return true;
            }

            _bufferedItemCount = 0;
            return false;
        }

        private static bool IsRecyclableCandidate(ItemData item)
        {
            if (item == null)
                return false;

            switch (item.category)
            {
                case ItemCategory.Material:
                case ItemCategory.Component:
                case ItemCategory.Tool:
                case ItemCategory.Equipment:
                case ItemCategory.Organic:
                    return true;
                default:
                    return false;
            }
        }

        private float ResolveRecycleDuration(ItemData sourceItem, int yieldUnits)
        {
            float categoryScale = 1f;
            if (sourceItem != null)
            {
                switch (sourceItem.category)
                {
                    case ItemCategory.Tool:
                    case ItemCategory.Equipment:
                        categoryScale = 1.35f;
                        break;
                    case ItemCategory.Component:
                        categoryScale = 1.15f;
                        break;
                }
            }

            float yieldScale = math.lerp(0.9f, 1.35f, math.saturate((yieldUnits - 1) / 5f));
            return math.max(1f, recycleDurationSeconds * categoryScale * yieldScale);
        }

        private float ResolvePowerMultiplier(ItemData sourceItem, int yieldUnits)
        {
            float scarcityScale = 1f;
            IResourceScarcityReadModel director = _scarcityReadModel;
            if (director != null && sourceItem != null)
                scarcityScale = Mathf.Max(1f, director.GetIngredientMultiplier(sourceItem.PersistentHashId));

            float yieldScale = math.lerp(1f, 1.35f, math.saturate((yieldUnits - 1) / 5f));
            return scarcityScale * yieldScale;
        }

        private void CacheRuntimeServicesCold()
        {
            _inventoryService = GlobalRegistry.PlayerInventory;
            _cachedInventory = ReadInventoryFromService(_inventoryService);
            _scarcityReadModel = GlobalRegistry.ResourceScarcityReadModel;
        }

        private static PlayerInventory ReadInventoryFromService(IPlayerInventoryService inventoryService)
        {
            return inventoryService != null && inventoryService.IsInitialized
                ? inventoryService.Inventory
                : null;
        }

        private void NotifyGridBalanceChanged()
        {
            PowerGrid grid = _powerNode != null ? _powerNode.Grid : null;
            if (grid != null)
                grid.MarkDirty();
        }

        private void ClearPendingOutput()
        {
            _hasPendingOutput = false;
            _debugHasPendingOutput = false;
            _activeSourceItem = null;
            ScrapManager.ClearYieldScratch(_pendingYieldScratch, _pendingYieldCount);
            _pendingYield = null;
            _pendingYieldCount = 0;
            _pendingYieldUnits = 0;
            _debugPendingYieldUnits = 0;
            _debugActiveItemId = string.Empty;
            _processTimer = 0f;
            _currentDuration = 1f;
            _activePowerMultiplier = 1f;
        }

        private void ClampBufferSlotCount()
        {
            if (bufferSlotCount < 4)
                bufferSlotCount = 4;
            else if (bufferSlotCount > MaxBufferSlots)
                bufferSlotCount = MaxBufferSlots;
        }
    }
}
