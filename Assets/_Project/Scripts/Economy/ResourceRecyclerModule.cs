using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Interaction;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Modding;
using Hecton8.Power;
using UnityEngine;

namespace Hecton8.Economy
{
    /// <summary>
    /// Powered base endpoint that converts one recyclable inventory item into cleaned resources over time instead of instant scrap conversion.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(PowerNode))]
    [AddComponentMenu("Hecton8/Economy/Resource Recycler Module")]
    public sealed class ResourceRecyclerModule : MonoBehaviour, ITickable, IPowerComponent, IInteractable
    {
        private const string DefaultReadyText = "Recycle Inventory Junk";
        private const string DefaultBusyText = "Recycler Processing";
        private const string DefaultPausedText = "Recycler Paused";
        private const string DefaultCollectText = "Collect Recycled Output";

        private static readonly List<ResourceRecyclerModule> s_ActiveModules = new List<ResourceRecyclerModule>(8);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_ActiveModules.Clear();
        }

        [Header("── Process Settings ───────────────────")]
        [Tooltip("Baseline recycle duration for one item batch.")]
        [SerializeField, Range(1f, 30f)] private float recycleDurationSeconds = 6f;

        [Tooltip("Continuous grid draw while a recycle batch is being purified.")]
        [SerializeField, Range(0f, 300f)] private float activePowerDraw = 140f;

        [Tooltip("One-shot grid energy burst consumed when the purification cycle starts.")]
        [SerializeField, Range(0f, 100f)] private float startupBurstPowerCost = 16f;

        [Tooltip("Power-shedding priority. Lower is more critical.")]
        [SerializeField, Range(0, 100)] private int powerPriority = 55;

        [Header("── Diagnostics ─────────────────────────")]
        [SerializeField] private bool _debugHasPower = true;
        [SerializeField] private bool _debugIsProcessing;
        [SerializeField] private bool _debugHasPendingOutput;
        [SerializeField] private string _debugActiveItemId;
        [SerializeField] private int _debugPendingYieldUnits;
        [SerializeField] private int _debugProcessedBatchCount;

        private Transform _cachedTransform;
        private PowerNode _powerNode;
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
        private int _pendingYieldUnits;
        private int _processedBatchCount;

        /// <summary>Live registry used by world pollution telemetry.</summary>
        internal static List<ResourceRecyclerModule> ActiveModules => s_ActiveModules;

        /// <summary>True while the recycler is actively drawing process power.</summary>
        internal bool IsProcessing => _isProcessing;

        /// <summary>Total completed recycle batches since scene load.</summary>
        internal int TotalProcessedBatchCount => _processedBatchCount;

        /// <summary>Dynamic active load injected into the power grid while processing is underway.</summary>
        public float PowerRating => _isProcessing ? -activePowerDraw * _activePowerMultiplier : 0f;

        /// <summary>Power-shedding priority for the recycler endpoint.</summary>
        public int PowerPriority => powerPriority;

        /// <summary>Cached grid availability propagated by the shared power grid.</summary>
        public bool HasPower => _hasPower;

        private void Awake()
        {
            _cachedTransform = transform;
            TryGetComponent(out _powerNode);
        }

        private void OnEnable()
        {
            RegisterModuleInstance();
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
            UnregisterModuleInstance();
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
            _hasPendingOutput = _pendingYield != null && _pendingYield.Length > 0;
            _debugHasPendingOutput = _hasPendingOutput;
            NotifyGridBalanceChanged();

            if (_hasPendingOutput)
                TryDeliverPendingYield(ResolveInventory(null));
        }

        void IInteractable.OnHoverStart()
        {
        }

        void IInteractable.OnHoverEnd()
        {
        }

        void IInteractable.Interact(Transform interactor)
        {
            PlayerInventory inventory = ResolveInventory(interactor);
            if (inventory == null)
                return;

            if (_hasPendingOutput)
            {
                TryDeliverPendingYield(inventory);
                return;
            }

            if (_isProcessing || !_hasPower)
                return;

            if (!TryResolveNextRecyclableItem(inventory, out ItemData sourceItem, out ResourceStack[] resolvedYield))
                return;

            if (!inventory.TryRemoveQuantity(sourceItem, 1))
                return;

            _activeSourceItem = sourceItem;
            _pendingYield = resolvedYield;
            _pendingYieldUnits = ScrapManager.CountYieldUnits(resolvedYield);
            _debugPendingYieldUnits = _pendingYieldUnits;
            _currentDuration = ResolveRecycleDuration(sourceItem, _pendingYieldUnits);
            _activePowerMultiplier = ResolvePowerMultiplier(sourceItem, _pendingYieldUnits);
            _processTimer = 0f;
            _isProcessing = true;
            _debugIsProcessing = true;
            _debugActiveItemId = sourceItem.PersistentId;

            PowerGrid grid = _powerNode != null ? _powerNode.Grid : null;
            if (grid != null && startupBurstPowerCost > 0f)
                grid.ConsumePower(startupBurstPowerCost);

            NotifyGridBalanceChanged();
        }

        string IInteractable.GetInteractText()
        {
            if (_hasPendingOutput)
                return DefaultCollectText;

            if (_isProcessing)
                return _hasPower ? DefaultBusyText : DefaultPausedText;

            return _hasPower ? DefaultReadyText : DefaultPausedText;
        }

        public void OnPowerStatusChanged(bool hasPower)
        {
            _hasPower = hasPower;
            _debugHasPower = hasPower;
        }

        private void TryRegister()
        {
            if (_registered || GameTickManager.Instance == null)
                return;

            GameTickManager.Instance.Register((ITickable)this);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered || GameTickManager.Instance == null)
                return;

            GameTickManager.Instance.Unregister((ITickable)this);
            _registered = false;
        }

        private void RegisterModuleInstance()
        {
            for (int i = 0; i < s_ActiveModules.Count; i++)
            {
                if (ReferenceEquals(s_ActiveModules[i], this))
                    return;
            }

            s_ActiveModules.Add(this);
        }

        private void UnregisterModuleInstance()
        {
            for (int i = s_ActiveModules.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(s_ActiveModules[i], this))
                    s_ActiveModules.RemoveAt(i);
            }
        }

        private PlayerInventory ResolveInventory(Transform interactor)
        {
            if (_cachedInventory != null)
                return _cachedInventory;

            if (interactor != null)
                _cachedInventory = interactor.GetComponentInParent<PlayerInventory>();

            if (_cachedInventory == null)
                _cachedInventory = PlayerInventory.Instance;

            return _cachedInventory;
        }

        private static bool TryResolveNextRecyclableItem(PlayerInventory inventory, out ItemData sourceItem, out ResourceStack[] resolvedYield)
        {
            sourceItem = null;
            resolvedYield = null;

            InventoryGrid grid = inventory != null ? inventory.Grid : null;
            if (grid == null)
                return false;

            int cols = grid.Columns;
            int rows = grid.Rows;
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    ItemData item = grid.GetCell(x, y);
                    if (item == null)
                        continue;

                    if (x > 0 && ReferenceEquals(grid.GetCell(x - 1, y), item))
                        continue;

                    if (y > 0 && ReferenceEquals(grid.GetCell(x, y - 1), item))
                        continue;

                    if (!IsRecyclableCandidate(item))
                        continue;

                    if (!ScrapManager.TryResolveRecycleYield(item, out resolvedYield) || resolvedYield == null || resolvedYield.Length == 0)
                        continue;

                    sourceItem = item;
                    return true;
                }
            }

            return false;
        }

        private bool TryDeliverPendingYield(PlayerInventory inventory)
        {
            if (!_hasPendingOutput || inventory == null || _pendingYield == null)
                return false;

            int grantedStackCount = 0;
            if (!ScrapManager.GrantYield(inventory, _pendingYield, ref grantedStackCount))
            {
                ScrapManager.RollbackYield(inventory, _pendingYield, grantedStackCount);
                return false;
            }

            HectonEventBus.Publish(new ItemRecycledEvent(_activeSourceItem, 1, _pendingYieldUnits));
            _processedBatchCount++;
            _debugProcessedBatchCount = _processedBatchCount;
            ClearPendingOutput();
            return true;
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

            float yieldScale = Mathf.Lerp(0.9f, 1.35f, Mathf.Clamp01((yieldUnits - 1) / 5f));
            return Mathf.Max(1f, recycleDurationSeconds * categoryScale * yieldScale);
        }

        private static float ResolvePowerMultiplier(ItemData sourceItem, int yieldUnits)
        {
            float scarcityScale = 1f;
            ResourceScarcityDirector director = ResourceScarcityDirector.Instance;
            if (director != null && sourceItem != null)
                scarcityScale = Mathf.Max(1f, director.GetIngredientMultiplier(sourceItem.PersistentId));

            float yieldScale = Mathf.Lerp(1f, 1.35f, Mathf.Clamp01((yieldUnits - 1) / 5f));
            return scarcityScale * yieldScale;
        }

        private void NotifyGridBalanceChanged()
        {
            PowerGrid grid = _powerNode != null ? _powerNode.Grid : null;
            if (grid != null)
                grid.UpdateBalance();
        }

        private void ClearPendingOutput()
        {
            _hasPendingOutput = false;
            _debugHasPendingOutput = false;
            _activeSourceItem = null;
            _pendingYield = null;
            _pendingYieldUnits = 0;
            _debugPendingYieldUnits = 0;
            _debugActiveItemId = string.Empty;
            _processTimer = 0f;
            _currentDuration = 1f;
            _activePowerMultiplier = 1f;
        }
    }
}
