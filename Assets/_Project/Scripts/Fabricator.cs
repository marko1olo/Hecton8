// ============================================================================
// HECTON-8 — Fabricator.cs
// Машина-верстак для крафта предметов.
//
// РЕФАКТОРИНГ v3 — ДИНАМИЧЕСКОЕ ПИТАНИЕ:
//   • Реализует IPowerComponent для интеграции с PowerGrid.
//   • При отсутствии питания крафт встаёт на ПАУЗУ (не отменяется).
//   • PowerRating: 0 в idle, -craftPowerDraw при крафте.
//   • При восстановлении питания крафт продолжается с того же места.
//   • При StartCraft/CompleteCraft/CancelCraft → PowerGrid.UpdateBalance()
//     для мгновенного пересчёта баланса сети.
//
// ЖИЗНЕННЫЙ ЦИКЛ КРАФТА:
//   1. Игрок наводится → OnHoverStart → HUD показывает промпт
//   2. [E] → Interact → CraftingEvents.RaiseFabricatorOpened
//   3. UI вызывает StartCraft(recipe) → CanCraft проверка
//   4. Ресурсы списываются СРАЗУ → таймер запускается
//      → NotifyGridBalanceChanged() — сеть пересчитывает с -100W
//   5. Tick(dt): если HasPower → _craftTimer продвигается
//               если !HasPower → ПАУЗА (таймер не тикает)
//   6. Завершение → результат в инвентарь → OnCraftCompleted
//      → NotifyGridBalanceChanged() — сеть пересчитывает без -100W
//   7. Отмена → ресурсы возвращаются → OnCraftCancelled
//      → NotifyGridBalanceChanged() — сеть пересчитывает без -100W
//
// ZERO GC:
//   • Tick: float арифметика, delegate?.Invoke (no boxing)
//   • CanCraft: for-циклы с ReferenceEquals, no LINQ
//   • IPowerComponent свойства: value types only
//   • PowerNode кэширован в Awake — zero TryGetComponent в горячем пути
// ============================================================================

using System.Collections.Generic;
using Hecton.Localization;
using Hecton8.Audio;
using Hecton8.Building;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Economy;
using Hecton8.SaveSystem;
using Hecton8.Interaction;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Modding;
using Hecton8.Power;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Crafting
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class Fabricator : MonoBehaviour, IInteractable, ITickable, IUpdatable, IPowerComponent, IFabricator, IModRegistryEventListener, ILocalizationLanguageChangedListener
    {
        // COLD ALLOC: List<Fabricator>[8] - active fabricator registry for cold-path recipe lookups - owner: Fabricator
        private static readonly List<Fabricator> _activeFabricators = new List<Fabricator>(8);
        private static bool s_emergencyPowerLockActive;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Identity ──────────────────────────────────")]
        [Tooltip("Название фабрикатора для UI промпта")]
        [SerializeField] private string fabricatorName = "Фабрикатор";

        [Header("── Recipes ───────────────────────────────────")]
        [Tooltip("Список доступных рецептов на этом верстаке")]
        [SerializeField] private List<RecipeData> availableRecipes = new List<RecipeData>();

        [Header("── Settings ──────────────────────────────────")]
        [Tooltip("Максимальная дистанция использования (метры). " +
                 "Если игрок отойдёт дальше — крафт отменяется.")]
        [SerializeField] private float maxUseDistance = 3.5f;

        [Header("── Power ─────────────────────────────────────")]
        [Tooltip("Потребление энергии ВО ВРЕМЯ КРАФТА (Ватты). " +
                 "В idle фабрикатор не потребляет дополнительно. " +
                 "Базовое потребление модуля берётся из BuildableData через PowerNode.")]
        [SerializeField] private float craftPowerDraw = 100f;

        [Tooltip("Приоритет отключения при дефиците. " +
                 "0 = критический (не отключать), 100 = роскошь (отключить первым).")]
        [Range(0, 100)]
        [SerializeField] private int powerPriority = 50;

        [Header("── Audio (optional) ──────────────────────────")]
        [SerializeField] private AudioClip   craftStartSound;
        [SerializeField] private AudioClip   craftCompleteSound;
        [SerializeField] private AudioClip   craftCancelSound;
        [SerializeField] private AudioClip   powerLostSound;

        [Header("── Physical Output ──────────────────────────")]
        [Tooltip("Optional socket used as the fabrication output origin.")]
        [SerializeField] private Transform outputSocket;
        [Tooltip("Local output direction used when no dedicated socket forward is authored.")]
        [SerializeField] private Vector3 outputDirectionLocal = Vector3.forward;
        [Tooltip("Meters pushed forward from the output origin before the stack is registered in the world.")]
        [SerializeField] private float outputForwardOffset = 0.45f;
        [Tooltip("Meters lifted above the output origin before the crafted stack is released.")]
        [SerializeField] private float outputLiftOffset = 0.12f;
        [Tooltip("Initial synthesized velocity change along the output direction.")]
        [SerializeField] private float outputVelocityChange = 1.75f;
        [Tooltip("Extra upward velocity change so the crafted stack clears the hatch before falling.")]
        [SerializeField] private float outputUpwardVelocityChange = 0.55f;

        [Header("── Deconstruction Output ────────────────────────")]
        [Tooltip("Optional catch-bin socket used when salvage components are ground back out of the fabricator.")]
        [SerializeField] private Transform deconstructOutputSocket;
        [Tooltip("Local ejection direction for reclaimed salvage when no dedicated catch-bin socket is authored.")]
        [SerializeField] private Vector3 deconstructOutputDirectionLocal = Vector3.forward;
        [Tooltip("Meters pushed forward from the deconstruction socket before reclaimed components register in the world.")]
        [SerializeField] private float deconstructOutputForwardOffset = 0.28f;
        [Tooltip("Meters lifted above the deconstruction socket before reclaimed components are released.")]
        [SerializeField] private float deconstructOutputLiftOffset = 0.08f;
        [Tooltip("Initial velocity change used to pop reclaimed salvage into the catch-bin.")]
        [SerializeField] private float deconstructOutputVelocityChange = 1.1f;
        [Tooltip("Extra upward velocity change used to keep reclaimed salvage from colliding with the grinder lip.")]
        [SerializeField] private float deconstructOutputUpwardVelocityChange = 0.25f;

        [Header("Crafting Thermodynamics")]
        [Tooltip("Base temperature delta injected into the hosting base module when a craft completes.")]
        [SerializeField, Min(0f)] private float craftTemperatureDeltaCelsius = 0.35f;

        [Tooltip("Optional host module receiving the craft heat pulse. If unset, the fabricator resolves the nearest parent module once.")]
        [SerializeField] private BaseModule thermalHostModule;

        // ══════════════════════════════════════════════════════════
        //  CACHED STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>Кэшированный текст промпта. Строится один раз.</summary>
        private string _interactText;

        /// <summary>Ссылка на инвентарь текущего игрока.</summary>
        private PlayerInventory _playerInventory;

        /// <summary>Transform игрока для проверки дистанции.</summary>
        private Transform _playerTransform;

        /// <summary>
        /// Кэшированный PowerNode на этом же GameObject.
        /// Используется для мгновенного уведомления PowerGrid
        /// при изменении состояния крафта (PowerRating меняется).
        /// Null-safe: если PowerNode отсутствует — уведомление не отправляется.
        /// </summary>
        private PowerNode _powerNode;
        private ScanLogSystem _scanLogSystem;
        private readonly List<RecipeData> _visibleRecipes = new List<RecipeData>(16);
        private bool _recipeCacheDirty = true;
        private bool _tickRegistered;
        private int _lockedRecipeCount;
        private float _activeCraftPowerMultiplier = 1f;

        // ── Craft State ──
        private bool       _isCrafting;
        private RecipeData _activeRecipe;
        private float      _craftTimer;
        private float      _lastPublishedProgress;

        // ── Power State ──
        private bool _hasPower = true;
        private bool _emergencyPowerLockActive;

        private const int MaxLocalCraftReservations = 64;
        private const int MaxNetworkCraftCosts = 32;
        private readonly PlayerInventory.CraftReservation[] _localCraftReservations = new PlayerInventory.CraftReservation[MaxLocalCraftReservations];
        private readonly int[] _networkCostItemHashes = new int[MaxNetworkCraftCosts];
        private readonly int[] _networkCostAmounts = new int[MaxNetworkCraftCosts];
        private int _localCraftReservationCount;
        private int _networkCostCount;
        private NativeParallelHashMap<int, int> _craftInventoryCounts;
        private NativeArray<int2> _craftRecipeCosts;
        private NativeArray<byte> _craftRecipeEvaluationResult;
        private NativeArray<int2> _deconstructionFlattenedCosts;
        private NativeArray<int2> _deconstructionRecipeOutputs;
        private NativeArray<int> _deconstructionOutputCount;

        private BaseLogisticsNetwork.LogisticsReservation _networkReservation;

        /// <summary>Порог публикации прогресса.</summary>
        private const float ProgressPublishThreshold = 0.01f;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — QUERIES
        // ══════════════════════════════════════════════════════════

        /// <summary>Идёт ли сейчас процесс крафта.</summary>
        public bool IsCrafting => _isCrafting;

        /// <summary>Нормализованный прогресс (0..1).</summary>
        public float CraftProgress => _isCrafting && _activeRecipe != null
            ? Mathf.Clamp01(_craftTimer / _activeRecipe.craftTime)
            : 0f;

        /// <summary>Активный рецепт (null если не крафтим).</summary>
        public RecipeData ActiveRecipe => _activeRecipe;

        /// <summary>Список доступных рецептов. Read-only для UI.</summary>
        public IReadOnlyList<RecipeData> AvailableRecipes
        {
            get
            {
                EnsureRecipeCache();
                return _visibleRecipes;
            }
        }

        public int TotalRecipeCount
        {
            get
            {
                EnsureRecipeCache();
                return _visibleRecipes.Count + _lockedRecipeCount;
            }
        }
        public int LockedRecipeCount
        {
            get
            {
                EnsureRecipeCache();
                return _lockedRecipeCount;
            }
        }

        /// <summary>Крафт на паузе из-за отсутствия питания.</summary>
        public bool IsPausedNoPower => _isCrafting && !HasOperationalPower;

        internal PowerGrid CurrentPowerGrid => _powerNode != null ? _powerNode.Grid : null;

        // ══════════════════════════════════════════════════════════
        //  IPowerComponent — ЭНЕРГОСИСТЕМА
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Потребление энергии фабрикатором.
        ///
        /// Idle (не крафтит): 0 Вт.
        ///   Базовое потребление модуля обеспечивается PowerNode
        ///   через BuildableData.powerRating.
        ///
        /// Crafting: -craftPowerDraw Вт.
        ///   Дополнительное потребление на работу станка.
        ///
        /// Итого при крафте: BuildableData.powerRating + (-craftPowerDraw).
        ///   Пример: -20 (базовый) + (-100) (крафт) = -120 Вт.
        /// </summary>
        public float PowerRating => _isCrafting && !_emergencyPowerLockActive ? -craftPowerDraw * _activeCraftPowerMultiplier : 0f;

        /// <summary>Приоритет отключения.</summary>
        public int PowerPriority => powerPriority;

        /// <summary>Текущее состояние питания.</summary>
        public bool HasPower => _hasPower;

        /// <summary>True while the submarine OS has suspended this fabricator from non-essential load service.</summary>
        public bool IsEmergencyPowerLocked => _emergencyPowerLockActive;

        /// <summary>
        /// Уведомление от PowerGrid об изменении питания.
        ///
        /// При потере питания:
        ///   • Крафт ЗАМОРАЖИВАЕТСЯ (таймер не тикает).
        ///   • Крафт НЕ отменяется — ресурсы уже списаны.
        ///   • При восстановлении — крафт продолжится.
        ///
        /// При восстановлении:
        ///   • Крафт продолжается с того же места.
        /// </summary>
        public void OnPowerStatusChanged(bool hasPower)
        {
            _hasPower = hasPower;

            if (!hasPower && _isCrafting)
            {
                // Крафт заморожен
                PlaySound(powerLostSound);
            }
        }

        /// <summary>
        /// Applies or clears the submarine-wide non-essential power lock across all live fabricators.
        /// Active crafts pause without losing inputs and resume automatically once the lock clears.
        /// </summary>
        public static void SetEmergencyPowerLockAll(bool active)
        {
            if (s_emergencyPowerLockActive == active)
                return;

            s_emergencyPowerLockActive = active;
            for (int i = 0; i < _activeFabricators.Count; i++)
            {
                Fabricator fabricator = _activeFabricators[i];
                if (fabricator == null)
                    continue;

                fabricator.ApplyEmergencyPowerLock(active);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            RebuildInteractText();

            // Кэшируем PowerNode для мгновенного уведомления сети.
            // PowerNode должен быть на том же GameObject, что и Fabricator.
            TryGetComponent(out _powerNode);
            EnsureScanLogSystem();
            MarkRecipeCacheDirty();
            _activeCraftPowerMultiplier = 1f;
            EnsureCraftingScratch();
        }

        private void Start()
        {
            CacheThermalHostModule();
        }

        private void OnEnable()
        {
            RegisterActiveFabricator(this);
            BaseLogisticsNetwork.RegisterFabricator(this, _powerNode);
            LocalizationEvents.RegisterLanguageListener(this);
            ModRegistryEvents.Register(this);
            RebuildInteractText();
            TryRegister();
            EnsureScanLogSystem();
            SubscribeToScanLog();
            MarkRecipeCacheDirty();
            ApplyEmergencyPowerLock(s_emergencyPowerLockActive);
        }

        private void OnDisable()
        {
            UnregisterActiveFabricator(this);
            BaseLogisticsNetwork.UnregisterFabricator(this);
            LocalizationEvents.UnregisterLanguageListener(this);
            ModRegistryEvents.Unregister(this);
            UnsubscribeFromScanLog();

            if (_isCrafting)
                CancelCraft();

            TryUnregister();
        }

        private void OnDestroy()
        {
            UnregisterActiveFabricator(this);
            BaseLogisticsNetwork.UnregisterFabricator(this);
            TryUnregister();
            DisposeCraftingScratch();
        }

        // ══════════════════════════════════════════════════════════
        //  IInteractable
        // ══════════════════════════════════════════════════════════

        public void OnHoverStart() { }

        public void OnHoverEnd() { }

        public void Interact(Transform interactor)
        {
            _playerTransform = interactor;

            if (_playerInventory == null && interactor != null)
                interactor.TryGetComponent(out _playerInventory);

            CraftingEvents.RaiseFabricatorOpened(this);
            InteractionEvents.RaiseInteractionStarted(this, interactor);
        }

        public string GetInteractText()
        {
            return _interactText;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — CRAFTING
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Проверяет, можно ли скрафтить данный рецепт.
        /// Добавлена проверка питания: без питания крафт не начинается.
        /// </summary>
        public bool CanCraft(RecipeData recipe)
        {
            if (recipe == null) return false;
            if (_isCrafting) return false;
            if (!HasOperationalPower) return false;
            if (_playerInventory == null || _playerInventory.Grid == null) return false;
            if (recipe.ingredients == null || recipe.ingredients.Count == 0) return false;
            if (recipe.resultItem == null || recipe.resultQuantity <= 0) return false;
            if (!IsRecipeUnlocked(recipe)) return false;

            if (!HasIngredients(recipe))
                return false;

            if (recipe.resultItem != null)
            {
                InventoryGrid grid = _playerInventory.Grid;
                int neededCells = recipe.resultItem.CellArea * recipe.resultQuantity;
                int ingredientCells = CountReclaimableIngredientCells(recipe);
                int availableAfter = grid.FreeCells + ingredientCells;

                if (neededCells > availableAfter)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Counts ingredient units available to this fabricator across the player inventory and its linked logistics grid.
        /// </summary>
        public int CountAccessibleItem(ItemData item, PlayerInventory inventoryOverride = null)
        {
            if (item == null)
                return 0;

            PlayerInventory inventory = inventoryOverride != null ? inventoryOverride : _playerInventory;
            int count = CountAvailableItemInInventory(inventory, item);
            PowerGrid grid = _powerNode != null ? _powerNode.Grid : null;
            if (grid != null)
                count += BaseLogisticsNetwork.CountAccessibleItem(grid, ComputeItemHash(item));

            return count;
        }

        internal int GetAdjustedIngredientAmount(InventoryCost cost)
        {
            if (cost == null || cost.item == null || cost.amount <= 0)
                return 0;

            int itemHashId = ComputeItemHash(cost.item);
            ResourceScarcityDirector scarcityDirector = GlobalRegistry.ResourceScarcity;
            return scarcityDirector != null
                ? scarcityDirector.ResolveInflatedIngredientAmount(itemHashId, cost.amount, transform.position, CountAccessibleItem(cost.item))
                : cost.amount;
        }

        internal float GetRecipeInflationMultiplier(RecipeData recipe)
        {
            if (recipe == null || recipe.ingredients == null || recipe.ingredients.Count <= 0)
                return 1f;

            float maxMultiplier = 1f;
            for (int i = 0; i < recipe.ingredients.Count; i++)
            {
                InventoryCost cost = recipe.ingredients[i];
                if (cost == null || cost.item == null || cost.amount <= 0)
                    continue;

                int adjustedAmount = GetAdjustedIngredientAmount(cost);
                if (adjustedAmount <= cost.amount)
                    continue;

                float multiplier = (float)adjustedAmount / cost.amount;
                if (multiplier > maxMultiplier)
                    maxMultiplier = multiplier;
            }

            return maxMultiplier;
        }

        /// <summary>
        /// Запускает процесс крафта.
        /// После смены _isCrafting → PowerRating меняется с 0 на -craftPowerDraw.
        /// NotifyGridBalanceChanged() заставляет сеть мгновенно пересчитать баланс.
        /// </summary>
        public bool StartCraft(RecipeData recipe)
        {
            if (!CanCraft(recipe)) return false;

            _activeRecipe = recipe;
            if (!ConsumeIngredients(recipe))
            {
                RefundIngredients();
                _activeRecipe = null;
                return false;
            }

            _activeCraftPowerMultiplier = ResolveCraftPowerMultiplier(this, recipe);
            _craftTimer   = 0f;
            _isCrafting   = true;
            _lastPublishedProgress = -1f;

            // ── Уведомляем энергосеть: PowerRating изменился (0 → -craftPowerDraw) ──
            NotifyGridBalanceChanged();

            CraftingEvents.RaiseCraftStarted(recipe);
            PlaySound(craftStartSound);

            return true;
        }

        void IFabricator.StartCraft(RecipeData recipe)
        {
            StartCraft(recipe);
        }

        /// <summary>
        /// Отменяет текущий крафт. Возвращает ингредиенты.
        /// После смены _isCrafting → PowerRating меняется с -craftPowerDraw на 0.
        /// NotifyGridBalanceChanged() заставляет сеть мгновенно пересчитать баланс.
        /// </summary>
        public void CancelCraft()
        {
            if (!_isCrafting) return;

            RefundIngredients();

            _isCrafting   = false;
            _activeRecipe = null;
            _craftTimer   = 0f;
            _activeCraftPowerMultiplier = 1f;

            // ── Уведомляем энергосеть: PowerRating изменился (-craftPowerDraw → 0) ──
            NotifyGridBalanceChanged();

            CraftingEvents.RaiseCraftCancelled();
            CraftingEvents.RaiseCraftProgressUpdated(0f);

            PlaySound(craftCancelSound);
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable — ТАЙМЕР КРАФТА
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Вызывается GameTickManager каждый кадр.
        ///
        /// ЭНЕРГОПАУЗА: если _hasPower == false и идёт крафт:
        ///   • Таймер НЕ продвигается.
        ///   • Прогресс НЕ публикуется (UI показывает паузу).
        ///   • Крафт НЕ отменяется.
        ///   • Проверка дистанции продолжается (игрок может отойти).
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!_isCrafting) return;
            if (_activeRecipe == null)
            {
                CancelCraft();
                return;
            }

            // ── Проверка дистанции (всегда, даже без питания) ──
            if (!IsPlayerInRange())
            {
                CancelCraft();
                return;
            }

            // ═══════════════════════════════════════════════════
            //  POWER PAUSE: нет питания → таймер заморожен
            // ═══════════════════════════════════════════════════
            if (!HasOperationalPower)
                return;

            // ── Обновление таймера ──
            _craftTimer += deltaTime;

            // ── Публикация прогресса ──
            float progress = Mathf.Clamp01(_craftTimer / _activeRecipe.craftTime);

            if (progress - _lastPublishedProgress > ProgressPublishThreshold
                || progress >= 1f)
            {
                _lastPublishedProgress = progress;
                CraftingEvents.RaiseCraftProgressUpdated(progress);
            }

            // ── Завершение ──
            if (_craftTimer >= _activeRecipe.craftTime)
            {
                CompleteCraft();
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — CRAFT COMPLETION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Завершает крафт: выдаёт результат в инвентарь.
        /// После смены _isCrafting → PowerRating меняется с -craftPowerDraw на 0.
        /// NotifyGridBalanceChanged() заставляет сеть мгновенно пересчитать баланс.
        /// </summary>
        private void CompleteCraft()
        {
            RecipeData recipe = _activeRecipe;
            if (recipe == null)
            {
                if (_networkReservation != null)
                {
                    BaseLogisticsNetwork.RollbackReserved(_networkReservation);
                    _networkReservation = null;
                }

                _isCrafting = false;
                _craftTimer = 0f;
                _lastPublishedProgress = 0f;
                _activeCraftPowerMultiplier = 1f;
                NotifyGridBalanceChanged();
                return;
            }

            ItemData   result = recipe.resultItem;
            float powerCost = ResolveCraftPowerCost(recipe);
            float craftTemperatureDelta = ResolveCraftTemperatureDeltaCelsius();

            _isCrafting   = false;
            _activeRecipe = null;
            _craftTimer   = 0f;
            _activeCraftPowerMultiplier = 1f;

            if (_networkReservation != null)
            {
                BaseLogisticsNetwork.CommitReserved(_networkReservation);
                _networkReservation = null;
            }

            // ── Уведомляем энергосеть: PowerRating изменился (-craftPowerDraw → 0) ──
            NotifyGridBalanceChanged();

            // ── Потребляем энергию из сети при завершении крафта ──
            if (powerCost > 0f && _powerNode != null && _powerNode.Grid != null)
            {
                _powerNode.Grid.ConsumePower(powerCost);
            }

            ApplyCraftingThermodynamics(craftTemperatureDelta);

            if (result != null && !TrySynthesizeCraftOutput(recipe, result) && _playerInventory != null)
            {
                int resultHashId = ComputeItemHash(result);
                for (int i = 0; i < recipe.resultQuantity; i++)
                {
                    if (resultHashId == 0 || !_playerInventory.TryAddItem(resultHashId, 1))
                    {
                        Debug.LogWarning(
                            $"[Fabricator] Инвентарь полон! " +
                            $"Не удалось добавить: {result.itemName} " +
                            $"(потеряно {recipe.resultQuantity - i} шт.)");
                        break;
                    }
                }
            }

            CraftingEvents.RaiseCraftProgressUpdated(1f);

            if (result != null)
                CraftingEvents.RaiseCraftCompleted(result);

            PlaySound(craftCompleteSound);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — POWER GRID NOTIFICATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Уведомляет PowerGrid о необходимости пересчёта баланса.
        ///
        /// Вызывается при каждом изменении PowerRating:
        ///   • StartCraft:    0 → -craftPowerDraw (начало потребления)
        ///   • CompleteCraft: -craftPowerDraw → 0 (конец потребления)
        ///   • CancelCraft:   -craftPowerDraw → 0 (отмена потребления)
        ///
        /// Без этого вызова PowerGrid узнал бы об изменении только
        /// при следующем SlowTick (~0.5-1с задержка). С вызовом —
        /// баланс пересчитывается мгновенно.
        ///
        /// Null-safe: если PowerNode или Grid отсутствуют — no-op.
        /// </summary>
        private void NotifyGridBalanceChanged()
        {
            if (_powerNode != null && _powerNode.Grid != null)
                _powerNode.Grid.MarkDirty();
        }

        private bool HasOperationalPower => _hasPower && !_emergencyPowerLockActive;

        private void ApplyEmergencyPowerLock(bool active)
        {
            if (_emergencyPowerLockActive == active)
                return;

            _emergencyPowerLockActive = active;
            if (_isCrafting)
                NotifyGridBalanceChanged();
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — INGREDIENT MANAGEMENT
        // ══════════════════════════════════════════════════════════

        private void CacheThermalHostModule()
        {
            if (thermalHostModule != null)
                return;

            thermalHostModule = GetComponentInParent<BaseModule>();
        }

        private float ResolveCraftTemperatureDeltaCelsius()
        {
            if (!(craftTemperatureDeltaCelsius > 0f) || !float.IsFinite(craftTemperatureDeltaCelsius))
                return 0f;

            float delta = craftTemperatureDeltaCelsius * Mathf.Max(1f, _activeCraftPowerMultiplier);
            return float.IsFinite(delta) ? delta : 0f;
        }

        private void ApplyCraftingThermodynamics(float deltaCelsius)
        {
            if (!(deltaCelsius > 0f))
                return;

            if (thermalHostModule == null)
                CacheThermalHostModule();

            if (thermalHostModule == null)
                return;

            thermalHostModule.TryInjectHostRoomTemperatureDeltaCelsius(deltaCelsius);
        }

        private bool HasIngredients(RecipeData recipe)
        {
            if (recipe == null || _playerInventory == null)
                return false;

            EnsureCraftingScratch();
            return CraftingSystem.CanCraft(
                recipe,
                this,
                _playerInventory,
                _craftInventoryCounts,
                _craftRecipeCosts,
                _craftRecipeEvaluationResult);
        }

        private void EnsureCraftingScratch()
        {
            if (!_craftInventoryCounts.IsCreated)
            {
                // COLD ALLOC: NativeParallelHashMap<Int32,Int32>[128] — temporary per-craft accessible item counts — owner: Fabricator
                _craftInventoryCounts = new NativeParallelHashMap<int, int>(128, Allocator.Persistent);
            }

            if (!_craftRecipeCosts.IsCreated)
            {
                // COLD ALLOC: NativeArray<int2>[32] — flattened recipe ingredient cost buffer — owner: Fabricator
                _craftRecipeCosts = new NativeArray<int2>(CraftingSystem.MaxRecipeIngredientCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            }

            if (!_craftRecipeEvaluationResult.IsCreated)
            {
                // COLD ALLOC: NativeArray<byte>[1] — Burst crafting-availability result cell — owner: Fabricator
                _craftRecipeEvaluationResult = new NativeArray<byte>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            }

            if (!_deconstructionFlattenedCosts.IsCreated)
            {
                // COLD ALLOC: NativeArray<int2>[64] — recursive deconstruction flattened ingredient scratch — owner: Fabricator
                _deconstructionFlattenedCosts = new NativeArray<int2>(CraftingSystem.MaxRecursiveDeconstructionNodeCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            }

            if (!_deconstructionRecipeOutputs.IsCreated)
            {
                // COLD ALLOC: NativeArray<int2>[32] — deconstruction output yield scratch — owner: Fabricator
                _deconstructionRecipeOutputs = new NativeArray<int2>(CraftingSystem.MaxDeconstructionOutputCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            }

            if (!_deconstructionOutputCount.IsCreated)
            {
                // COLD ALLOC: NativeArray<int>[1] — deconstruction output count cell — owner: Fabricator
                _deconstructionOutputCount = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            }
        }

        private void DisposeCraftingScratch()
        {
            if (_networkReservation != null)
            {
                BaseLogisticsNetwork.RollbackReserved(_networkReservation);
                _networkReservation = null;
            }

            if (_craftInventoryCounts.IsCreated)
                _craftInventoryCounts.Dispose();

            if (_craftRecipeCosts.IsCreated)
                _craftRecipeCosts.Dispose();

            if (_craftRecipeEvaluationResult.IsCreated)
                _craftRecipeEvaluationResult.Dispose();

            if (_deconstructionFlattenedCosts.IsCreated)
                _deconstructionFlattenedCosts.Dispose();

            if (_deconstructionRecipeOutputs.IsCreated)
                _deconstructionRecipeOutputs.Dispose();

            if (_deconstructionOutputCount.IsCreated)
                _deconstructionOutputCount.Dispose();
        }

        private bool TrySynthesizeCraftOutput(RecipeData recipe, ItemData result)
        {
            if (recipe == null || result == null)
                return false;

            PersistentWorldRegistry registry = GlobalRegistry.PersistentWorldRegistry;
            if (registry == null)
                return false;

            int quantity = math.max(1, recipe.resultQuantity);
            ResolveCraftOutputPose(out Vector3 spawnPosition, out Vector3 velocityChange);
            bool synthesized = registry.TryRegisterDroppedItem(result, quantity, spawnPosition, Vector3.zero, velocityChange);
            if (!synthesized)
                return false;

            CraftingEvents.RaiseCraftOutputSynthesized(
                new CraftedItemSynthesisEvent(result, quantity, spawnPosition, velocityChange));
            return true;
        }

        /// <summary>
        /// Grinds one crafted item back into reclaimed ingredient stacks using the reverse recipe path.
        /// </summary>
        public bool TryDeconstructItem(int itemHashId)
        {
            if (itemHashId == 0 || _playerInventory == null)
                return false;

            Hecton8.SaveSystem.ItemCatalog itemCatalog = _playerInventory.ItemCatalog;
            if (itemCatalog == null)
                return false;

            ItemData targetItem = itemCatalog.FindByHash(itemHashId);
            if (targetItem == null || !TryResolveRecipeForResultItem(targetItem, out RecipeData sourceRecipe))
                return false;

            if (!_playerInventory.TryConsumeFirstMatchingItemByHash(itemHashId, out ushort stateFlags, out ushort qualityMilli))
                return false;

            EnsureCraftingScratch();
            int normalizedQualityMilli = qualityMilli > 0 ? qualityMilli : 1000;
            bool isDegraded = (stateFlags & PlayerInventory.DegradedItemStateMask) != 0 ||
                              normalizedQualityMilli < PlayerInventory.DegradedQualityMilliThreshold;
            bool forceScrapYield = normalizedQualityMilli < PlayerInventory.DegradedQualityMilliThreshold;
            int reclaimPercent = isDegraded ? 30 : 80;
            if (!CraftingSystem.TryBuildDeconstructionYieldBuffer(
                    sourceRecipe,
                    this,
                    itemCatalog,
                    forceScrapYield,
                    reclaimPercent,
                    _craftRecipeCosts,
                    _deconstructionFlattenedCosts,
                    _deconstructionRecipeOutputs,
                    _deconstructionOutputCount,
                    ResolveScrapMetalHashId(itemCatalog)))
            {
                _playerInventory.TryAddItem(itemHashId, 1);
                return false;
            }

            int outputCount = _deconstructionOutputCount[0];
            if (outputCount <= 0)
            {
                _playerInventory.TryAddItem(itemHashId, 1);
                return false;
            }

            PersistentWorldRegistry registry = GlobalRegistry.PersistentWorldRegistry;
            ResolveDeconstructionOutputPose(out Vector3 spawnPosition, out Vector3 velocityChange);
            bool emittedAny = false;

            for (int outputIndex = 0; outputIndex < outputCount; outputIndex++)
            {
                int2 output = _deconstructionRecipeOutputs[outputIndex];
                if (output.x == 0 || output.y <= 0)
                    continue;

                ItemData outputItem = itemCatalog.FindByHash(output.x);
                if (outputItem == null)
                    continue;

                bool emitted = registry != null &&
                               registry.TryRegisterDroppedItem(outputItem, output.y, spawnPosition, Vector3.zero, velocityChange);
                if (!emitted)
                {
                    for (int quantityIndex = 0; quantityIndex < output.y; quantityIndex++)
                    {
                        if (!_playerInventory.TryAddItem(output.x, 1))
                            break;
                    }
                }

                CraftingEvents.RaiseCraftOutputSynthesized(
                    new CraftedItemSynthesisEvent(outputItem, output.y, spawnPosition, velocityChange));
                emittedAny = true;
            }

            if (!emittedAny)
                _playerInventory.TryAddItem(itemHashId, 1);

            return emittedAny;
        }

        private void ResolveCraftOutputPose(out Vector3 spawnPosition, out Vector3 velocityChange)
        {
            Transform origin = outputSocket != null ? outputSocket : transform;
            Vector3 localDirection = outputDirectionLocal.sqrMagnitude > 0.0001f
                ? outputDirectionLocal.normalized
                : Vector3.forward;
            Vector3 worldDirection = origin.TransformDirection(localDirection);
            if (worldDirection.sqrMagnitude <= 0.0001f)
                worldDirection = origin.forward;

            worldDirection.Normalize();
            spawnPosition = origin.position + worldDirection * outputForwardOffset + Vector3.up * outputLiftOffset;
            velocityChange = worldDirection * outputVelocityChange + Vector3.up * outputUpwardVelocityChange;
        }

        private void ResolveDeconstructionOutputPose(out Vector3 spawnPosition, out Vector3 velocityChange)
        {
            Transform origin = deconstructOutputSocket != null ? deconstructOutputSocket : (outputSocket != null ? outputSocket : transform);
            Vector3 localDirection = deconstructOutputDirectionLocal.sqrMagnitude > 0.0001f
                ? deconstructOutputDirectionLocal.normalized
                : Vector3.forward;
            Vector3 worldDirection = origin.TransformDirection(localDirection);
            if (worldDirection.sqrMagnitude <= 0.0001f)
                worldDirection = origin.forward;

            worldDirection.Normalize();
            spawnPosition = origin.position + worldDirection * deconstructOutputForwardOffset + Vector3.up * deconstructOutputLiftOffset;
            velocityChange = worldDirection * deconstructOutputVelocityChange + Vector3.up * deconstructOutputUpwardVelocityChange;
        }

        private static int CountAvailableItemInInventory(PlayerInventory inventory, ItemData item)
        {
            if (inventory == null || item == null)
                return 0;

            return inventory.CountAvailableTotal(ComputeItemHash(item));
        }

        private static int ComputeItemHash(ItemData item)
        {
            return item == null ? 0 : LocHash.Compute(item.PersistentId);
        }

        private bool TryAccumulateNetworkCost(int itemHashId, int amount)
        {
            if (itemHashId == 0 || amount <= 0)
                return false;

            for (int i = 0; i < _networkCostCount; i++)
            {
                if (_networkCostItemHashes[i] != itemHashId)
                    continue;

                _networkCostAmounts[i] += amount;
                return true;
            }

            if (_networkCostCount >= MaxNetworkCraftCosts)
                return false;

            _networkCostItemHashes[_networkCostCount] = itemHashId;
            _networkCostAmounts[_networkCostCount] = amount;
            _networkCostCount++;
            return true;
        }

        private int CountReclaimableIngredientCells(RecipeData recipe)
        {
            if (recipe == null || recipe.ingredients == null || _playerInventory == null)
                return 0;

            int total = 0;
            List<InventoryCost> costs = recipe.ingredients;

            for (int i = 0, count = costs.Count; i < count; i++)
            {
                InventoryCost cost = costs[i];
                if (cost == null || cost.item == null) continue;

                int localAvailable = CountAvailableItemInInventory(_playerInventory, cost.item);
                int requiredAmount = GetAdjustedIngredientAmount(cost);
                int removableCount = localAvailable < requiredAmount ? localAvailable : requiredAmount;
                total += cost.item.CellArea * removableCount;
            }

            return total;
        }

        private bool ConsumeIngredients(RecipeData recipe)
        {
            if (recipe == null || recipe.ingredients == null || _playerInventory == null || _playerInventory.Grid == null)
                return false;

            List<InventoryCost> costs = recipe.ingredients;
            _localCraftReservationCount = 0;
            _networkCostCount = 0;

            if (_networkReservation != null)
            {
                BaseLogisticsNetwork.RollbackReserved(_networkReservation);
                _networkReservation = null;
            }

            for (int c = 0, cCount = costs.Count; c < cCount; c++)
            {
                InventoryCost cost = costs[c];
                if (cost == null || cost.item == null) continue;

                int remaining = GetAdjustedIngredientAmount(cost);
                if (remaining <= 0)
                    continue;

                int localAvailable = CountAvailableItemInInventory(_playerInventory, cost.item);
                int localTake = localAvailable < remaining ? localAvailable : remaining;
                if (localTake > 0)
                {
                    if (!_playerInventory.TryReserveQuantityForCraft(
                            ComputeItemHash(cost.item),
                            localTake,
                            _localCraftReservations,
                            ref _localCraftReservationCount))
                        return false;

                    remaining -= localTake;
                }

                if (remaining > 0)
                {
                    if (!TryAccumulateNetworkCost(ComputeItemHash(cost.item), remaining))
                        return false;
                }
            }

            if (_networkCostCount > 0)
            {
                PowerGrid gridRef = _powerNode != null ? _powerNode.Grid : null;
                if (!BaseLogisticsNetwork.TryReserveResources(
                        gridRef,
                        _networkCostItemHashes,
                        _networkCostAmounts,
                        _networkCostCount,
                        out _networkReservation))
                {
                    return false;
                }
            }

            return true;
        }

        private void RefundIngredients()
        {
            if (_activeRecipe == null || _playerInventory == null || _playerInventory.Grid == null) return;

            _playerInventory.ReleaseCraftReservations(_localCraftReservations, _localCraftReservationCount);
            _localCraftReservationCount = 0;

            if (_networkReservation != null)
            {
                BaseLogisticsNetwork.RollbackReserved(_networkReservation);
                _networkReservation = null;
            }
            _networkCostCount = 0;

        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — DISTANCE CHECK
        // ══════════════════════════════════════════════════════════

        private bool IsPlayerInRange()
        {
            if (_playerTransform == null) return false;

            float sqrDist = (_playerTransform.position - transform.position).sqrMagnitude;
            return sqrDist <= maxUseDistance * maxUseDistance;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — AUDIO
        // ══════════════════════════════════════════════════════════

        private void PlaySound(AudioClip clip)
        {
            if (clip == null)
                return;

            if (Hecton8.Core.GlobalRegistry.Audio != null)
                Hecton8.Core.GlobalRegistry.Audio.PlayAtPoint(clip, transform.position);
        }

        private void EnsureScanLogSystem()
        {
            if (_scanLogSystem == null)
                _scanLogSystem = Hecton8.Core.GlobalRegistry.ScanLog;
        }

        private void SubscribeToScanLog()
        {
            if (_scanLogSystem != null)
                _scanLogSystem.ScanLogChanged += HandleScanLogChanged;
        }

        private void UnsubscribeFromScanLog()
        {
            if (_scanLogSystem != null)
                _scanLogSystem.ScanLogChanged -= HandleScanLogChanged;
        }

        private void HandleScanLogChanged()
        {
            MarkRecipeCacheDirty();
        }

        private void MarkRecipeCacheDirty()
        {
            _recipeCacheDirty = true;
        }

        private void EnsureRecipeCache()
        {
            if (!_recipeCacheDirty)
                return;

            EnsureScanLogSystem();

            _visibleRecipes.Clear();
            _lockedRecipeCount = 0;

            if (availableRecipes != null)
            {
                for (int i = 0; i < availableRecipes.Count; i++)
                {
                    AppendRecipeToCache(availableRecipes[i]);
                }
            }

            int runtimeRecipeCount = ModRecipeRegistry.Count;
            for (int i = 0; i < runtimeRecipeCount; i++)
            {
                RecipeData recipe = ModRecipeRegistry.GetAt(i);
                if (recipe == null || ContainsAuthoredRecipeReference(recipe))
                    continue;

                AppendRecipeToCache(recipe);
            }

            _recipeCacheDirty = false;
        }

        private bool IsRecipeUnlocked(RecipeData recipe)
        {
            return recipe != null && recipe.IsUnlocked(_scanLogSystem);
        }

        private void AppendRecipeToCache(RecipeData recipe)
        {
            if (recipe == null)
                return;

            if (IsRecipeUnlocked(recipe))
                _visibleRecipes.Add(recipe);
            else
                _lockedRecipeCount++;
        }

        private bool ContainsAuthoredRecipeReference(RecipeData recipe)
        {
            if (recipe == null || availableRecipes == null)
                return false;

            for (int i = 0; i < availableRecipes.Count; i++)
            {
                if (ReferenceEquals(availableRecipes[i], recipe))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Handles deferred mod registry events that affect available recipes.
        /// </summary>
        /// <param name="payload">Unmanaged mod registry payload.</param>
        public void OnModRegistryEvent(in ModRegistryEventPayload payload)
        {
            if ((ModRegistryEventType)payload.EventType != ModRegistryEventType.RecipeRegistryChanged)
                return;

            MarkRecipeCacheDirty();
            EnsureRecipeCache();
        }

        internal static bool TryResolveRecipeForResultItem(ItemData resultItem, out RecipeData recipe)
        {
            if (resultItem != null)
            {
                for (int i = 0; i < _activeFabricators.Count; i++)
                {
                    Fabricator fabricator = _activeFabricators[i];
                    if (fabricator == null)
                        continue;

                    if (TryResolveRecipeForResultItem(fabricator.availableRecipes, resultItem, out recipe))
                        return true;
                }

                int runtimeRecipeCount = ModRecipeRegistry.Count;
                for (int i = 0; i < runtimeRecipeCount; i++)
                {
                    RecipeData runtimeRecipe = ModRecipeRegistry.GetAt(i);
                    if (RecipeProducesItem(runtimeRecipe, resultItem))
                    {
                        recipe = runtimeRecipe;
                        return true;
                    }
                }
            }

            recipe = null;
            return false;
        }

        internal static bool TryResolveRecipeForResultHash(ItemCatalog itemCatalog, int resultHashId, out RecipeData recipe)
        {
            ItemData resultItem = itemCatalog != null && resultHashId != 0
                ? itemCatalog.FindByHash(resultHashId)
                : null;
            return TryResolveRecipeForResultItem(resultItem, out recipe);
        }

        internal static bool TryGetActiveFabricator(string targetName, out Fabricator fabricator)
        {
            bool matchAny = string.IsNullOrWhiteSpace(targetName);
            for (int i = 0; i < _activeFabricators.Count; i++)
            {
                Fabricator candidate = _activeFabricators[i];
                if (candidate == null)
                    continue;

                if (matchAny || candidate.name == targetName)
                {
                    fabricator = candidate;
                    return true;
                }
            }

            fabricator = null;
            return false;
        }

        private static int ResolveScrapMetalHashId(ItemCatalog itemCatalog)
        {
            int scrapMetalHashId = LocHash.Compute("Data_ScrapMetal");
            if (itemCatalog != null && itemCatalog.FindByHash(scrapMetalHashId) != null)
                return scrapMetalHashId;

            return LocHash.Compute("Data_TitaniumScrap");
        }

        private static bool TryResolveRecipeForResultItem(List<RecipeData> recipes, ItemData resultItem, out RecipeData recipe)
        {
            if (recipes != null && resultItem != null)
            {
                for (int i = 0; i < recipes.Count; i++)
                {
                    RecipeData candidate = recipes[i];
                    if (RecipeProducesItem(candidate, resultItem))
                    {
                        recipe = candidate;
                        return true;
                    }
                }
            }

            recipe = null;
            return false;
        }

        private static bool RecipeProducesItem(RecipeData recipe, ItemData resultItem)
        {
            if (recipe == null || recipe.resultItem == null || resultItem == null)
                return false;

            if (ReferenceEquals(recipe.resultItem, resultItem))
                return true;

            return !string.IsNullOrWhiteSpace(recipe.resultItem.PersistentId) &&
                   string.Equals(recipe.resultItem.PersistentId, resultItem.PersistentId, System.StringComparison.Ordinal);
        }

        private static void RegisterActiveFabricator(Fabricator fabricator)
        {
            if (fabricator == null)
                return;

            for (int i = 0; i < _activeFabricators.Count; i++)
            {
                if (ReferenceEquals(_activeFabricators[i], fabricator))
                    return;
            }

            _activeFabricators.Add(fabricator);
        }

        private static void UnregisterActiveFabricator(Fabricator fabricator)
        {
            if (fabricator == null)
                return;

            for (int i = _activeFabricators.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_activeFabricators[i], fabricator))
                {
                    _activeFabricators.RemoveAt(i);
                    break;
                }
            }
        }

        private static float ResolveCraftPowerMultiplier(Fabricator owner, RecipeData recipe)
        {
            return owner != null
                ? Mathf.Max(1f, owner.GetRecipeInflationMultiplier(recipe))
                : Mathf.Max(1f, ResourceScarcityDirector.ResolveCraftPowerMultiplier(recipe));
        }

        private float ResolveCraftPowerCost(RecipeData recipe)
        {
            return recipe != null && recipe.powerCost > 0f
                ? recipe.powerCost * _activeCraftPowerMultiplier
                : 0f;
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

        private void TryRegister()
        {
            if (_tickRegistered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _tickRegistered = true;
        }

        private void TryUnregister()
        {
            if (!_tickRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _tickRegistered = false;
        }

        private void RebuildInteractText()
        {
            string fallbackName = string.IsNullOrWhiteSpace(fabricatorName)
                ? ResolveLocalized(LocalizationKeys.UI_FABRICATOR, "FABRICATOR")
                : fabricatorName;
            string pattern = ResolveLocalized(LocalizationKeys.INTERACT_USE_FABRICATOR, "Use {0}");
            _interactText = string.Format(pattern, fallbackName);
        }

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }


        private void HandleLanguageChanged(GameLanguage language)
        {
            RebuildInteractText();
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            if (manager == null)
                return fallback;

            string localized = manager.Get(key);
            return string.IsNullOrWhiteSpace(localized) ? fallback : localized;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (maxUseDistance < 1f) maxUseDistance = 1f;
            if (craftPowerDraw < 0f) craftPowerDraw = 0f;
            if (string.IsNullOrEmpty(fabricatorName)) fabricatorName = "Фабрикатор";

            _interactText = $"Использовать {fabricatorName}";
            MarkRecipeCacheDirty();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, maxUseDistance);
        }
#endif
    }
}

