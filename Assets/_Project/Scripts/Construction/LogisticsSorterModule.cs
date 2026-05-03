using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Power;
using Hecton8.SaveSystem;
using UnityEngine;

namespace Hecton8.Construction
{
    /// <summary>
    /// Powered local sorter that accepts industrial output and routes it into authored destination crates.
    /// It never destroys buffered items: if the target crate is full, cargo simply remains staged in the sorter.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PowerNode))]
    [AddComponentMenu("Hecton8/Construction/Logistics Sorter Module")]
    public sealed class LogisticsSorterModule : MonoBehaviour, ISlowTickable, IPoolable, IPowerComponent
    {
        private const float SlowTickDeltaTime = 0.5f;
        private const int MaxBufferSlots = 8;

        [System.Serializable]
        private struct SortRoute
        {
            [Tooltip("Item routed by this rule.")]
            public ItemData item;

            [Tooltip("Destination storage crate for the routed item.")]
            public StorageCrate destination;

            public SortRoute(ItemData item, StorageCrate destination)
            {
                this.item = item;
                this.destination = destination;
            }
        }

        [Header("── Routing ────────────────────────────────")]
        [Tooltip("Destination used when no explicit item route is configured.")]
        [SerializeField] private StorageCrate defaultDestination;

        [Tooltip("Authored routing table. Matching items are pushed to their mapped destination crate.")]
        [SerializeField] private SortRoute[] routes;

        [Tooltip("Maximum active slot count inside the local sorter staging buffer.")]
        [SerializeField, Range(1, MaxBufferSlots)] private int bufferSlotCount = 6;

        [Header("── Power ──────────────────────────────────")]
        [Tooltip("Continuous draw while the sorter has staged cargo and power is available.")]
        [SerializeField, Range(0f, 120f)] private float activePowerDraw = 12f;

        [Tooltip("Priority used when the power grid starts shedding non-critical industrial loads.")]
        [SerializeField, Range(0, 100)] private int powerPriority = 52;

        [Header("── Diagnostics ───────────────────────────")]
        [SerializeField] private bool _debugHasPower = true;
        [SerializeField] private int _debugBufferedItemCount;
        [SerializeField] private string _debugHeadItemId;
        [SerializeField] private string _debugHeadDestinationName;

        private PowerNode _powerNode;
        private bool _registered;
        private bool _hasPower = true;
        private readonly ItemData[] _bufferItems = new ItemData[MaxBufferSlots];
        private readonly int[] _bufferQuantities = new int[MaxBufferSlots];
        private int _bufferedItemCount;

        public float PowerRating => _bufferedItemCount > 0 && _hasPower ? -activePowerDraw : 0f;
        public int PowerPriority => powerPriority;
        public bool HasPower => _hasPower;

        private void Awake()
        {
            _powerNode = GetComponent<PowerNode>();
            ClampBufferSlotCount();
        }

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
            TryUnregister();
        }

        public void OnSpawn()
        {
            _hasPower = true;
            _debugHasPower = true;
            ClearBufferedState();
            TryRegister();
        }

        public void OnDespawn()
        {
            ClearBufferedState();
            TryUnregister();
            _hasPower = true;
            _debugHasPower = true;
        }

        public void SlowTick()
        {
            if (!_hasPower || _bufferedItemCount <= 0)
                return;

            RouteNextBufferedItem();
        }

        public void OnPowerStatusChanged(bool hasPower)
        {
            _hasPower = hasPower;
            _debugHasPower = hasPower;
        }

        /// <summary>
        /// Receives inbound industrial output from drill modules or other logistics producers.
        /// Returns the number of units accepted into the local staging buffer.
        /// </summary>
        internal int AcceptInbound(ItemData item, int quantity)
        {
            if (item == null || quantity <= 0)
                return 0;

            ClampBufferSlotCount();

            int accepted = 0;
            for (int i = 0; i < quantity; i++)
            {
                if (!TryBufferItem(item))
                    break;

                accepted++;
            }

            _debugBufferedItemCount = _bufferedItemCount;
            _debugHeadItemId = _bufferedItemCount > 0 && _bufferItems[0] != null ? _bufferItems[0].PersistentId : string.Empty;
            if (accepted > 0)
                NotifyGridBalanceChanged();
            return accepted;
        }

        internal void PopulateSaveData(ref ModuleDTO dto)
        {
            if (_bufferedItemCount <= 0)
                return;

            int slotCount = bufferSlotCount;
            dto.sorterBufferedSlotCount = slotCount;
            dto.sorterBufferedItemIds = new string[slotCount];
            dto.sorterBufferedQuantities = new int[slotCount];

            for (int i = 0; i < slotCount; i++)
            {
                ItemData item = _bufferItems[i];
                dto.sorterBufferedItemIds[i] = item != null ? item.PersistentId : string.Empty;
                dto.sorterBufferedQuantities[i] = _bufferQuantities[i];
            }
        }

        internal void RestoreFromSaveData(ModuleDTO dto, ItemCatalog itemCatalog)
        {
            ClearBufferedState();

            if (itemCatalog == null ||
                dto.sorterBufferedItemIds == null ||
                dto.sorterBufferedQuantities == null)
            {
                return;
            }

            ClampBufferSlotCount();
            int slotCount = Mathf.Min(bufferSlotCount, Mathf.Min(dto.sorterBufferedItemIds.Length, dto.sorterBufferedQuantities.Length));
            for (int i = 0; i < slotCount; i++)
            {
                string itemId = dto.sorterBufferedItemIds[i];
                int quantity = dto.sorterBufferedQuantities[i];
                if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0)
                    continue;

                ItemData item = itemCatalog.FindById(itemId);
                if (item == null)
                    continue;

                _bufferItems[i] = item;
                _bufferQuantities[i] = quantity;
                _bufferedItemCount += quantity;
            }

            _debugBufferedItemCount = _bufferedItemCount;
            if (_bufferedItemCount <= 0)
                return;

            for (int i = 0; i < bufferSlotCount; i++)
            {
                if (_bufferItems[i] == null || _bufferQuantities[i] <= 0)
                    continue;

                _debugHeadItemId = _bufferItems[i].PersistentId;
                StorageCrate destination = ResolveDestination(_bufferItems[i]);
                _debugHeadDestinationName = destination != null ? destination.name : string.Empty;
                return;
            }
        }

        internal void EjectBufferedContents(BaseModule owner, PlayerInventory inventory, ObjectPoolManager pool, ref Vector3 dropPosition)
        {
            if (owner == null || _bufferedItemCount <= 0)
                return;

            for (int i = 0; i < bufferSlotCount; i++)
            {
                ItemData item = _bufferItems[i];
                int quantity = _bufferQuantities[i];
                if (item == null || quantity <= 0)
                    continue;

                int itemHashId = Hecton.Localization.LocHash.Compute(item.PersistentId);
                if (itemHashId == 0)
                    continue;

                owner.DropItemQuantityToInventoryOrWorld(itemHashId, quantity, inventory, pool, ref dropPosition);
            }

            ClearBufferedState();
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _registered = GlobalRegistry.SlowTickables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registered = false;
        }

        private bool TryBufferItem(ItemData item)
        {
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

        private void RouteNextBufferedItem()
        {
            for (int i = 0; i < bufferSlotCount; i++)
            {
                ItemData item = _bufferItems[i];
                if (item == null || _bufferQuantities[i] <= 0)
                    continue;

                StorageCrate destination = ResolveDestination(item);
                _debugHeadItemId = item.PersistentId;
                _debugHeadDestinationName = destination != null ? destination.name : string.Empty;

                if (destination == null || !destination.HasAutomatedCapacity() || !destination.TryAddAutomatedItem(item))
                    return;

                _bufferQuantities[i]--;
                _bufferedItemCount--;
                if (_bufferQuantities[i] <= 0)
                {
                    _bufferQuantities[i] = 0;
                    _bufferItems[i] = null;
                }

                _debugBufferedItemCount = _bufferedItemCount;
                if (_bufferedItemCount <= 0)
                {
                    _debugHeadItemId = string.Empty;
                    _debugHeadDestinationName = string.Empty;
                }

                NotifyGridBalanceChanged();
                return;
            }

            _bufferedItemCount = 0;
            _debugBufferedItemCount = 0;
            _debugHeadItemId = string.Empty;
            _debugHeadDestinationName = string.Empty;
        }

        private StorageCrate ResolveDestination(ItemData item)
        {
            if (item != null && routes != null)
            {
                for (int i = 0; i < routes.Length; i++)
                {
                    SortRoute route = routes[i];
                    if (ReferenceEquals(route.item, item) && route.destination != null)
                        return route.destination;
                }
            }

            return defaultDestination;
        }

        private void NotifyGridBalanceChanged()
        {
            PowerGrid grid = _powerNode != null ? _powerNode.Grid : null;
            if (grid != null)
                grid.MarkDirty();
        }

        private void ClampBufferSlotCount()
        {
            if (bufferSlotCount < 1)
                bufferSlotCount = 1;
            else if (bufferSlotCount > MaxBufferSlots)
                bufferSlotCount = MaxBufferSlots;
        }

        private void ClearBufferedState()
        {
            for (int i = 0; i < MaxBufferSlots; i++)
            {
                _bufferItems[i] = null;
                _bufferQuantities[i] = 0;
            }

            _bufferedItemCount = 0;
            _debugBufferedItemCount = 0;
            _debugHeadItemId = string.Empty;
            _debugHeadDestinationName = string.Empty;
        }
    }
}
