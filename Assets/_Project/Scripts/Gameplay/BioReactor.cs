// ============================================================================
// HECTON-8 — BioReactor.cs
// Storage-based power generator that consumes organic materials.
//
// ARCHITECTURE:
//   • IPowerComponent for power grid integration
//   • ITickable for fuel consumption (no Update)
//   • MaterialPropertyBlock for fuel level indicator (zero GC)
//   • Slot-based fuel storage
//
// FUEL SYSTEM:
//   • Player inserts organic items (ItemData with organic tag/category)
//   • Each item has fuelValue (energy content)
//   • Reactor consumes fuel over time, producing constant power
//
// INTEGRATION:
//   • IPowerComponent.PowerRating returns current production
//   • UnityEvent for fuel level changes
// ============================================================================

using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Interaction;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Power;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Represents a fuel item in the reactor.
    /// </summary>
    [System.Serializable]
    public class FuelItem
    {
        public ItemData itemData;
        public float fuelValue; // Energy content
        public float remainingFuel; // Current remaining fuel
    }

    /// <summary>
    /// Bio-reactor power generator.
    /// Consumes organic materials to produce power.
    /// Implements IPowerComponent for power grid integration.
    /// Implements IInteractable for player fuel deposit.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    [AddComponentMenu("Hecton/Gameplay/Bio Reactor")]
    public sealed class BioReactor : MonoBehaviour, IPowerComponent, ITickable, IUpdatable, IInteractable, ILocalizationLanguageChangedListener
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Power Settings ─────────────────────────────")]
        [Tooltip("Power output while fueled (Watts).")]
        [SerializeField, Range(10f, 500f)] private float powerOutput = 100f;

        [Tooltip("Fuel consumption rate (units per second).")]
        [SerializeField, Range(0.1f, 10f)] private float fuelConsumptionRate = 1f;

        [Header("── Fuel Storage ───────────────────────────────")]
        [Tooltip("Maximum number of fuel slots.")]
        [SerializeField, Range(1, 8)] private int maxFuelSlots = 4;

        [Tooltip("Item categories accepted as fuel.")]
        [SerializeField] private ItemCategory[] acceptedCategories = { ItemCategory.Material };

        [Tooltip("Default fuel value for items without explicit fuel value.")]
        [SerializeField, Range(10f, 100f)] private float defaultFuelValue = 50f;

        [Header("── Status Indicator ───────────────────────────")]
        [Tooltip("Renderer for the fuel level indicator.")]
        [SerializeField] private Renderer fuelIndicator;

        [Tooltip("Material property for indicator color.")]
        [SerializeField] private string emissionProperty = "_EmissionColor";

        [Tooltip("Color when fueled and producing.")]
        [SerializeField] private Color fueledColor = new Color(0.2f, 1f, 0.3f);

        [Tooltip("Color when low fuel.")]
        [SerializeField] private Color lowFuelColor = new Color(1f, 0.5f, 0.1f);

        [Tooltip("Color when empty.")]
        [SerializeField] private Color emptyColor = new Color(0.2f, 0.2f, 0.2f);

        [Tooltip("Fuel level threshold for low fuel warning (0-1).")]
        [SerializeField, Range(0.1f, 0.5f)] private float lowFuelThreshold = 0.25f;

        [Header("── Overheat ───────────────────────────────")]
        [Tooltip("Grid utilization threshold above which the reactor starts accumulating overheat.")]
        [SerializeField, Range(0.5f, 1f)] private float overheatUtilizationThreshold = 0.98f;

        [Tooltip("Seconds the reactor can sustain near-full utilization before damaging the host module.")]
        [SerializeField, Range(1f, 600f)] private float overheatGraceSeconds = 120f;

        [Tooltip("Integrity damage per second applied to the host module once overheat grace is exhausted.")]
        [SerializeField, Range(0f, 50f)] private float overheatIntegrityDamagePerSecond = 4f;

        [Tooltip("Rate at which stored overheat decays when the grid load drops.")]
        [SerializeField, Range(0.1f, 10f)] private float overheatCooldownRate = 1.5f;

        [Tooltip("Explosion radius applied when an overheated reactor catastrophically fails.")]
        [SerializeField, Range(1f, 40f)] private float meltdownRadius = 20f;

        [Tooltip("Peak module damage dealt at the center of the reactor meltdown.")]
        [SerializeField, Range(0f, 200f)] private float meltdownModuleDamage = 90f;

        [Tooltip("Peak player damage dealt at the center of the reactor meltdown.")]
        [SerializeField, Range(0f, 200f)] private float meltdownPlayerDamage = 65f;

        [Tooltip("Layers scanned during the reactor meltdown overlap pass.")]
        [SerializeField] private LayerMask meltdownMask = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;

        [Header("── Audio ──────────────────────────────────────")]
        [Tooltip("Sound played when fuel is inserted.")]
        [SerializeField] private AudioClip insertSound;

        [Tooltip("Sound played when fuel is depleted.")]
        [SerializeField] private AudioClip depletedSound;

        [Header("── Events ─────────────────────────────────────")]
        [Tooltip("Fired when fuel level changes. Parameter: normalized fuel level (0-1).")]
        [SerializeField] private UnityEvent<float> OnFuelLevelChanged;

        [Tooltip("Fired when reactor starts producing power.")]
        [SerializeField] private UnityEvent OnReactorStarted;

        [Tooltip("Fired when reactor stops (out of fuel).")]
        [SerializeField] private UnityEvent OnReactorStopped;

        [Tooltip("Fired when fuel is inserted. Parameter: slot index.")]
        [SerializeField] private UnityEvent<int> OnFuelInserted;

        [Tooltip("Fired when fuel is depleted. Parameter: slot index.")]
        [SerializeField] private UnityEvent<int> OnFuelDepleted;

        [Tooltip("Fired when player interacts with the reactor.")]
        [SerializeField] private UnityEvent OnInteract;

        // ══════════════════════════════════════════════════════════
        //  INTERACTION TEXT
        // ══════════════════════════════════════════════════════════

        private const string DefaultInteractText = "Deposit Fuel";
        private const string DefaultInteractFullText = "Reactor Full";
        private string _cachedInteractText;
        private string _cachedInteractFullText;

        // ══════════════════════════════════════════════════════════
        //  IInteractable IMPLEMENTATION
        // ══════════════════════════════════════════════════════════

        void IInteractable.OnHoverStart()
        {
            // Future: highlight effect
        }

        void IInteractable.OnHoverEnd()
        {
            // Future: remove highlight
        }

        void IInteractable.Interact(Transform interactor)
        {
            // Try to deposit fuel from player inventory
            PlayerInventory playerInventory = interactor.GetComponentInParent<PlayerInventory>();
            if (playerInventory == null)
            {
                playerInventory = Hecton8.Core.GlobalRegistry.Player != null ? Hecton8.Core.GlobalRegistry.Player.Inventory : null;
            }

            if (playerInventory != null)
            {
                DepositFuelFromInventory(playerInventory);
            }

            OnInteract?.Invoke();
        }

        string IInteractable.GetInteractText()
        {
            return _fuelItems.Count >= maxFuelSlots ? _cachedInteractFullText : _cachedInteractText;
        }

        // ══════════════════════════════════════════════════════════
        //  INVENTORY INTEGRATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Deposits fuel from the player's inventory into the reactor.
        /// Searches for accepted fuel items and deposits the first one found.
        /// </summary>
        /// <param name="playerInventory">Player's inventory to search.</param>
        /// <returns>True if fuel was deposited.</returns>
        public bool DepositFuelFromInventory(PlayerInventory playerInventory)
        {
            if (playerInventory == null || playerInventory.Grid == null)
                return false;

            if (_fuelItems.Count >= maxFuelSlots)
                return false;

            InventoryGrid grid = playerInventory.Grid;
            int cols = grid.Columns;
            int rows = grid.Rows;

            // Search for accepted fuel items
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    int anchorIndex = grid.GetCellAnchorIndex(x, y);
                    if (anchorIndex < 0 || anchorIndex != y * cols + x)
                        continue;

                    int itemHashId = playerInventory.GetItemHashAt(x, y);
                    ItemData item = itemHashId != 0 && playerInventory.ItemCatalog != null
                        ? playerInventory.ItemCatalog.FindByHash(itemHashId)
                        : null;
                    if (item == null)
                        continue;

                    if (!IsAcceptedFuel(item))
                        continue;

                    // Try to insert fuel
                    if (InsertFuel(item))
                    {
                        // Remove from player inventory
                        playerInventory.RemoveItemAt(x, y);
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Deposits a specific fuel item from the player's inventory.
        /// </summary>
        /// <param name="playerInventory">Player's inventory.</param>
        /// <param name="item">The item to deposit.</param>
        /// <returns>True if the item was deposited.</returns>
        public bool DepositSpecificFuel(PlayerInventory playerInventory, ItemData item)
        {
            if (playerInventory == null || item == null)
                return false;

            if (_fuelItems.Count >= maxFuelSlots)
                return false;

            if (!IsAcceptedFuel(item))
                return false;

            if (!playerInventory.ContainsItem(Hecton.Localization.LocHash.Compute(item.PersistentId)))
                return false;

            if (!InsertFuel(item))
                return false;

            // Remove one unit from player inventory
            playerInventory.TryRemoveQuantity(Hecton.Localization.LocHash.Compute(item.PersistentId), 1);
            return true;
        }

        /// <summary>
        /// Gets the count of accepted fuel items in the player's inventory.
        /// </summary>
        public int CountFuelInInventory(PlayerInventory playerInventory)
        {
            if (playerInventory == null || playerInventory.Grid == null)
                return 0;

            int count = 0;
            InventoryGrid grid = playerInventory.Grid;
            int cols = grid.Columns;
            int rows = grid.Rows;

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    int anchorIndex = grid.GetCellAnchorIndex(x, y);
                    if (anchorIndex < 0 || anchorIndex != y * cols + x)
                        continue;

                    int itemHashId = playerInventory.GetItemHashAt(x, y);
                    ItemData item = itemHashId != 0 && playerInventory.ItemCatalog != null
                        ? playerInventory.ItemCatalog.FindByHash(itemHashId)
                        : null;
                    if (item != null && IsAcceptedFuel(item))
                    {
                        count += playerInventory.GetStackCount(x, y);
                    }
                }
            }

            return count;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private List<FuelItem> _fuelItems;
        private float _totalFuelCapacity;
        private float _currentFuelLevel;
        private bool _isProducing;
        private bool _wasProducing;
        private bool _registered;
        private bool _hasPower = true; // IPowerComponent requirement
        private int _emissionPropertyId;
        private float _overheatTimer;
        private float _debugGridUtilization;
        private bool _meltdownTriggered;

        // Cached references
        private Transform _cachedTransform;
        private PowerNode _powerNode;
        private BaseModule _hostModule;
        private MaterialPropertyBlock _mpb;
        private static readonly int _EmissionColorID = Shader.PropertyToID("_EmissionColor");
        private static readonly Collider[] MeltdownOverlapBuffer = new Collider[48];
        private readonly int[] _damagedModuleIds = new int[24];
        private readonly int[] _damagedSurvivalIds = new int[8];

        // ══════════════════════════════════════════════════════════
        //  IPowerComponent IMPLEMENTATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Current power output (positive = generation).
        /// </summary>
        public float PowerRating => _isProducing ? powerOutput : 0f;

        /// <summary>
        /// Priority: generators are never disconnected.
        /// </summary>
        public int PowerPriority => 0;

        /// <summary>
        /// Always true for generators.
        /// </summary>
        public bool HasPower => _hasPower;

        /// <summary>
        /// Called by PowerGrid. Generators ignore this.
        /// </summary>
        public void OnPowerStatusChanged(bool hasPower)
        {
            _hasPower = true;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        /// <summary>True if currently producing power.</summary>
        public bool IsProducing => _isProducing;

        /// <summary>Current fuel level (0-1).</summary>
        public float FuelLevel => _totalFuelCapacity > 0 ? _currentFuelLevel / _totalFuelCapacity : 0f;

        /// <summary>Number of fuel slots in use.</summary>
        public int FuelSlotCount => _fuelItems?.Count ?? 0;

        /// <summary>Maximum fuel slots.</summary>
        public int MaxFuelSlots => maxFuelSlots;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _cachedTransform = transform;
            _powerNode = GetComponent<PowerNode>();
            _hostModule = GetComponent<BaseModule>();
            if (_hostModule == null)
                _hostModule = GetComponentInParent<BaseModule>();
            _emissionPropertyId = Shader.PropertyToID(string.IsNullOrEmpty(emissionProperty) ? "_EmissionColor" : emissionProperty);
            _mpb = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] — per-renderer props — owner: BioReactor
            _fuelItems = new List<FuelItem>(maxFuelSlots); // COLD ALLOC: List<FuelItem>[maxFuelSlots] — fuel storage — owner: BioReactor

            if (fuelIndicator == null)
                fuelIndicator = GetComponent<Renderer>();
        }

        private void OnEnable()
        {
            LocalizationEvents.RegisterLanguageListener(this);
            TryRegister();
            RebuildLocalizedTextCache();
            UpdateFuelIndicator();
        }

        private void OnDisable()
        {
            InteractableRegistry.InvalidateTree(this);
            LocalizationEvents.UnregisterLanguageListener(this);
            TryUnregister();
        }

        private void OnDestroy()
        {
            InteractableRegistry.InvalidateTree(this);
            TryUnregister();
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
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

        // ══════════════════════════════════════════════════════════
        //  ITickable — FUEL CONSUMPTION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// ITickable implementation. Handles fuel consumption.
        /// Zero GC: no allocations in hot path.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_fuelItems.Count == 0)
            {
                if (_isProducing)
                {
                    _isProducing = false;
                    OnReactorStopped?.Invoke();
                    NotifyGridBalanceChanged();
                    UpdateFuelIndicator();
                }

                UpdateOverheat(deltaTime);
                return;
            }

            // Consume fuel
            float fuelToConsume = fuelConsumptionRate * deltaTime;
            ConsumeFuel(fuelToConsume);

            // Update production state
            if (!_isProducing && _fuelItems.Count > 0)
            {
                _isProducing = true;
                OnReactorStarted?.Invoke();
                NotifyGridBalanceChanged();
            }

            UpdateOverheat(deltaTime);
            UpdateFuelIndicator();
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Inserts a fuel item into the reactor.
        /// </summary>
        /// <param name="item">The item to insert as fuel.</param>
        /// <returns>True if the item was accepted.</returns>
        public bool InsertFuel(ItemData item)
        {
            if (item == null || _fuelItems.Count >= maxFuelSlots)
                return false;

            // Check if item category is accepted
            if (!IsAcceptedFuel(item))
                return false;

            // Create fuel item
            float fuelValue = GetFuelValue(item);
            var fuelItem = new FuelItem
            {
                itemData = item,
                fuelValue = fuelValue,
                remainingFuel = fuelValue
            };

            _fuelItems.Add(fuelItem);
            _totalFuelCapacity += fuelValue;
            _currentFuelLevel += fuelValue;

            // Play insert sound
            if (insertSound != null && Hecton8.Core.GlobalRegistry.Audio is Hecton8.Core.IAudioService audio)
            {
                audio.PlayAtPoint(insertSound, _cachedTransform.position);
            }

            OnFuelInserted?.Invoke(_fuelItems.Count - 1);
            OnFuelLevelChanged?.Invoke(FuelLevel);

            return true;
        }

        /// <summary>
        /// Checks if an item can be used as fuel.
        /// </summary>
        /// <param name="item">The item to check.</param>
        /// <returns>True if the item is accepted as fuel.</returns>
        public bool IsAcceptedFuel(ItemData item)
        {
            if (item == null || acceptedCategories == null)
                return false;

            if (item.resourceFamily == ResourceFamily.Organic)
                return true;

            for (int i = 0; i < acceptedCategories.Length; i++)
            {
                if (item.category == acceptedCategories[i])
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the fuel value for an item.
        /// </summary>
        private float GetFuelValue(ItemData item)
        {
            // Future: ItemData could have an explicit fuelValue field
            // For now, use default based on item properties
            return defaultFuelValue;
        }

        // ══════════════════════════════════════════════════════════
        //  FUEL CONSUMPTION LOGIC
        // ══════════════════════════════════════════════════════════

        private void ConsumeFuel(float amount)
        {
            float previousLevel = _currentFuelLevel;

            while (amount > 0 && _fuelItems.Count > 0)
            {
                FuelItem firstItem = _fuelItems[0];

                if (firstItem.remainingFuel <= amount)
                {
                    // Item is depleted
                    amount -= firstItem.remainingFuel;
                    _currentFuelLevel -= firstItem.remainingFuel;
                    _totalFuelCapacity -= firstItem.fuelValue;

                    // Remove depleted item
                    int slotIndex = 0;
                    _fuelItems.RemoveAt(0);

                    // Play depleted sound
                    if (depletedSound != null && Hecton8.Core.GlobalRegistry.Audio is Hecton8.Core.IAudioService audio)
                    {
                        audio.PlayAtPoint(depletedSound, _cachedTransform.position);
                    }

                    OnFuelDepleted?.Invoke(slotIndex);
                }
                else
                {
                    // Partial consumption
                    firstItem.remainingFuel -= amount;
                    _currentFuelLevel -= amount;
                    amount = 0;
                }
            }

            // Fire event if level changed significantly
            if (math.abs(_currentFuelLevel - previousLevel) > 0.1f)
            {
                OnFuelLevelChanged?.Invoke(FuelLevel);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  VISUALS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Updates the fuel level indicator using MaterialPropertyBlock.
        /// Zero GC: uses cached MaterialPropertyBlock.
        /// </summary>
        private void UpdateOverheat(float deltaTime)
        {
            if (_meltdownTriggered)
                return;

            PowerGrid grid = _powerNode != null ? _powerNode.Grid : null;
            if (!_isProducing || grid == null)
            {
                CoolOverheat(deltaTime);
                return;
            }

            float totalGeneration = grid.TotalGeneration;
            if (totalGeneration <= 0.0001f)
            {
                CoolOverheat(deltaTime);
                return;
            }

            float utilization = math.saturate(grid.TotalConsumption / totalGeneration);
            _debugGridUtilization = utilization;

            if (utilization < overheatUtilizationThreshold)
            {
                CoolOverheat(deltaTime);
                return;
            }

            float parasiteOverheatMultiplier = _hostModule != null
                ? math.max(1f, _hostModule.ParasiteBioReactorOverheatMultiplier)
                : 1f;
            _overheatTimer += deltaTime * parasiteOverheatMultiplier;
            if (_hostModule == null || _overheatTimer < overheatGraceSeconds)
                return;

            _hostModule.ApplyDamage(overheatIntegrityDamagePerSecond * deltaTime * parasiteOverheatMultiplier);
            if (_hostModule.CurrentIntegrity <= 0f)
                TriggerMeltdown();
        }

        private void CoolOverheat(float deltaTime)
        {
            _debugGridUtilization = 0f;
            if (_overheatTimer <= 0f)
                return;

            _overheatTimer = math.max(0f, _overheatTimer - (deltaTime * math.max(0.1f, overheatCooldownRate)));
        }

        private void TriggerMeltdown()
        {
            if (_meltdownTriggered)
                return;

            _meltdownTriggered = true;
            _overheatTimer = 0f;
            _debugGridUtilization = 0f;
            _isProducing = false;
            _currentFuelLevel = 0f;
            _totalFuelCapacity = 0f;
            _fuelItems.Clear();
            OnReactorStopped?.Invoke();
            UpdateFuelIndicator();
            NotifyGridBalanceChanged();

            Vector3 origin = _cachedTransform != null ? _cachedTransform.position : transform.position;
            int hitCount = UnityEngine.Physics.OverlapSphereNonAlloc(
                origin,
                meltdownRadius,
                MeltdownOverlapBuffer,
                meltdownMask,
                QueryTriggerInteraction.Ignore);

            float safeMeltdownRadius = math.max(0.1f, meltdownRadius);
            float meltdownRadiusSq = safeMeltdownRadius * safeMeltdownRadius;
            float inverseMeltdownRadiusSq = math.rcp(meltdownRadiusSq);
            int damagedModuleCount = 0;
            int damagedSurvivalCount = 0;
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = MeltdownOverlapBuffer[i];
                MeltdownOverlapBuffer[i] = null;
                if (hit == null)
                    continue;

                Vector3 offset = hit.bounds.ClosestPoint(origin) - origin;
                float distanceSq = math.lengthsq(new float3(offset.x, offset.y, offset.z));
                if (distanceSq >= meltdownRadiusSq)
                    continue;

                float damage01 = 1f - math.saturate(distanceSq * inverseMeltdownRadiusSq);
                if (damage01 <= 0f)
                    continue;

                BaseModule module = hit.GetComponentInParent<BaseModule>();
                if (module != null)
                {
                    int moduleId = GetRuntimeId(module);
                    if (TryRegisterUniqueId(_damagedModuleIds, ref damagedModuleCount, moduleId))
                        module.ApplyDamage(meltdownModuleDamage * damage01);
                }

                HectonSurvivalSystem survival = hit.GetComponentInParent<HectonSurvivalSystem>();
                if (survival != null)
                {
                    int survivalId = GetRuntimeId(survival);
                    if (TryRegisterUniqueId(_damagedSurvivalIds, ref damagedSurvivalCount, survivalId))
                        survival.TakeDamage(meltdownPlayerDamage * damage01);
                }
            }

            if (_hostModule != null && !_hostModule.IsFlooded)
                _hostModule.ForceFlood();
        }

        private static bool TryRegisterUniqueId(int[] ids, ref int count, int value)
        {
            for (int i = 0; i < count; i++)
            {
                if (ids[i] == value)
                    return false;
            }

            if (count < ids.Length)
            {
                ids[count] = value;
                count++;
            }

            return true;
        }

        private static int GetRuntimeId(Component owner)
        {
            return owner == null
                ? 0
                : unchecked((int)EntityId.ToULong(owner.GetEntityId()));
        }

        private void NotifyGridBalanceChanged()
        {
            PowerGrid grid = _powerNode != null ? _powerNode.Grid : null;
            if (grid != null)
                grid.MarkDirty();
        }

        private void UpdateFuelIndicator()
        {
            if (fuelIndicator == null)
                return;

            float level = FuelLevel;
            Color indicatorColor;

            if (level <= 0.01f)
            {
                indicatorColor = emptyColor;
            }
            else if (level < lowFuelThreshold)
            {
                indicatorColor = lowFuelColor;
            }
            else
            {
                indicatorColor = fueledColor;
            }

            fuelIndicator.GetPropertyBlock(_mpb);
            _mpb.SetColor(_emissionPropertyId != 0 ? _emissionPropertyId : _EmissionColorID, indicatorColor);
            fuelIndicator.SetPropertyBlock(_mpb);
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (powerOutput < 1f) powerOutput = 1f;
            if (fuelConsumptionRate < 0.1f) fuelConsumptionRate = 0.1f;
            if (maxFuelSlots < 1) maxFuelSlots = 1;
            if (defaultFuelValue < 1f) defaultFuelValue = 1f;
            RebuildLocalizedTextCache();
        }

        private void OnDrawGizmosSelected()
        {
            // Draw fuel level indicator
            if (Application.isPlaying)
            {
                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * 2f,
                    $"Fuel: {FuelLevel:P0}\nSlots: {FuelSlotCount}/{MaxFuelSlots}\nPower: {PowerRating:F0}W"
                );
            }
        }
#endif

        private void RebuildLocalizedTextCache()
        {
            _cachedInteractText = ResolveLocalized(LocalizationKeys.INTERACT_DEPOSIT_FUEL, DefaultInteractText);
            _cachedInteractFullText = ResolveLocalized(LocalizationKeys.INTERACT_REACTOR_FULL, DefaultInteractFullText);
        }

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }


        private void HandleLanguageChanged(GameLanguage language)
        {
            RebuildLocalizedTextCache();
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
        }
    }
}

