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

using System;
using System.Collections.Generic;
using Hecton.Localization;
using Hecton8.Audio;
using Hecton8.Building;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Economy;
using Hecton8.SaveSystem;
using Hecton8.Interaction;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Modding;
using Hecton8.Power;
using Hecton8.Tools;
using Hecton8.UI;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Crafting
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class Fabricator : MonoBehaviour, IInteractable, ISlowTickable, IUpdatable, IPowerComponent, IFabricator, IModRegistryEventListener, ILocalizationLanguageChangedListener
    {
        // COLD ALLOC: List<Fabricator>[8] - active fabricator registry for cold-path recipe lookups - owner: Fabricator
        private static readonly List<Fabricator> _activeFabricators = new List<Fabricator>(8);
        private static readonly int _uiFabricatorLocalizationHash = LocHash.Compute(LocalizationKeys.UI_FABRICATOR);
        private static readonly int _interactUseFabricatorLocalizationHash = LocHash.Compute(LocalizationKeys.INTERACT_USE_FABRICATOR);
        private static bool s_emergencyPowerLockActive;
        private const int InteractTextBufferCapacity = 96;

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

        [Tooltip("When enabled, a completed recipe immediately queues again if unlocks, ingredients, capacity, and power still pass.")]
        [SerializeField] private bool isContinuous;

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

        [Header("Fabrication Feedback")]
        [Tooltip("Pre-authored GPU particle sparks emitted from the nozzle while fabrication advances.")]
        [SerializeField] private ParticleSystem fabricationSparks;
        [SerializeField, Min(0f)] private float fabricationSparksBaseRate = 18f;
        [SerializeField] private AudioClip fabricationErrorBuzzerSound;
        [SerializeField] private Renderer[] errorFeedbackRenderers;
        [SerializeField] private Color errorEmissionColor = new Color(1f, 0.04f, 0.02f, 1f);
        [SerializeField, Min(0.05f)] private float errorFlashDurationSeconds = 0.55f;
        [SerializeField] private Color sparkProxyLightColor = new Color(1f, 0.48f, 0.12f, 1f);
        [SerializeField, Min(0.01f)] private float sparkProxyLightDurationSeconds = 0.1f;
        [SerializeField, Min(0.01f)] private float sparkProxyLightRangeMeters = 2.4f;
        [SerializeField, Min(0f)] private float sparkProxyLightIntensity = 0.72f;

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
        // COLD ALLOC: char[96] - cached IInteractable prompt staging buffer - owner: Fabricator
        private readonly char[] _interactTextBuffer = new char[InteractTextBufferCapacity];
        private int _interactTextLength;

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
        private int _activeCraftMultiplier = 1;
        private MaterialPropertyBlock _errorFeedbackBlock;
        private float _errorFlashRemainingSeconds;
        private bool _fabricationSparksPlaying;
        private bool _errorFeedbackApplied;
        private int _sparkProxyLightKey;
        private float _sparkProxyLightRemainingSeconds;
        private bool _sparkProxyLightRegistered;
        private bool _sparkLightTickRegistered;

        // ── Craft State ──
        private bool       _isCrafting;
        private RecipeData _activeRecipe;
        private float      _craftTimer;
        private float      _lastPublishedProgress;

        // ── Power State ──
        private bool _hasPower = true;
        private bool _emergencyPowerLockActive;

        internal struct CraftingTask
        {
            public int ResultHashId;
            public int ResultQuantity;
            public float Progress;
            public float DurationSeconds;
            public float PowerMultiplier;
            public int Multiplier;
        }

        private const int MaxLocalCraftReservations = 64;
        private const int MaxNetworkCraftCosts = 32;
        private const int MaxQueuedCraftingTasks = 1;
        private const int MaxUnlockedRecipeWords = 8;
        private const int RecipeUnlockWordShift = 6;
        private const int RecipeUnlockBitMask = 63;
        private const float SlowTickDeltaSeconds = 0.5f;
        private const float ThermalThrottleTemperatureCelsius = 50f;
        private const float ThermalThrottleProgressMultiplier = 0.5f;
        private const byte FabricatorHapticMotorMask = 0b0001;
        private const byte FabricatorHapticPriority = 2;
        private const byte FabricatorFinalHapticPriority = 3;
        private readonly PlayerInventory.CraftReservation[] _localCraftReservations = new PlayerInventory.CraftReservation[MaxLocalCraftReservations];
        private readonly int[] _networkCostItemHashes = new int[MaxNetworkCraftCosts];
        private readonly int[] _networkCostAmounts = new int[MaxNetworkCraftCosts];
        private int _localCraftReservationCount;
        private int _networkCostCount;
        private NativeParallelHashMap<int, int> _craftInventoryCounts;
        private NativeArray<int2> _craftRecipeCosts;
        private NativeArray<byte> _craftRecipeEvaluationResult;
        private NativeArray<int2> _deconstructionRecipeOutputs;
        private NativeArray<int> _deconstructionOutputCount;
        private NativeQueue<CraftingTask> _craftingTaskQueue;
        private NativeArray<int2> _complexRecipeGraphNodes;
        private NativeArray<int2> _complexRecipeGraphEdges;
        private NativeArray<int> _complexRecipeGraphInDegrees;
        private NativeArray<int> _complexRecipeGraphQueue;
        private NativeArray<int2> _complexRecipeRawCosts;
        private NativeArray<int> _complexRecipeRawCostCount;
        private NativeArray<byte> _complexRecipeGraphStatus;
        private NativeArray<ulong> _unlockedRecipes;
        private bool _unlockMaskDirty = true;

        private BaseLogisticsNetwork.LogisticsReservation _networkReservation;

        /// <summary>Порог публикации прогресса.</summary>
        private const float ProgressPublishThreshold = 0.01f;
        private const string NativeMemoryOwner = nameof(Fabricator);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — QUERIES
        // ══════════════════════════════════════════════════════════

        /// <summary>Идёт ли сейчас процесс крафта.</summary>
        public bool IsCrafting => _isCrafting;

        public bool IsContinuous
        {
            get => isContinuous;
            set => isContinuous = value;
        }

        /// <summary>Нормализованный прогресс (0..1).</summary>
        public float CraftProgress => _isCrafting && _activeRecipe != null
            ? Mathf.Clamp01(_craftTimer / Mathf.Max(0.001f, _activeRecipe.craftTime * Mathf.Max(1, _activeCraftMultiplier)))
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
            _sparkProxyLightKey = unchecked(GetInstanceID() ^ 0x4641424C);
            ToolHapticsRuntime.EnsureRuntimeInstance();
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

            SetFabricationSparksActive(false);
            UnregisterSparkProxyLight();
            TryUnregisterSparkLightTick();
            TryUnregister();
        }

        private void OnDestroy()
        {
            UnregisterActiveFabricator(this);
            BaseLogisticsNetwork.UnregisterFabricator(this);
            TryUnregister();
            SetFabricationSparksActive(false);
            UnregisterSparkProxyLight();
            TryUnregisterSparkLightTick();
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
            return CanCraft(recipe, 1);
        }

        public bool CanCraft(RecipeData recipe, int multiplier)
        {
            if (recipe == null) return false;
            if (_isCrafting) return false;
            if (!HasOperationalPower) return false;
            if (_playerInventory == null || _playerInventory.Grid == null) return false;
            if (recipe.ingredients == null || recipe.ingredients.Count == 0) return false;
            if (recipe.resultItem == null || recipe.resultQuantity <= 0) return false;
            if (!IsRecipeUnlocked(recipe)) return false;
            if (!PassesBiomeLock(recipe)) return false;

            int safeMultiplier = Mathf.Max(1, multiplier);
            if (!HasIngredients(recipe, safeMultiplier))
                return false;

            if (recipe.resultItem != null)
            {
                InventoryGrid grid = _playerInventory.Grid;
                int neededCells = recipe.resultItem.CellArea * recipe.resultQuantity * safeMultiplier;
                int ingredientCells = CountReclaimableIngredientCells(recipe, safeMultiplier);
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
            return StartCraft(recipe, 1);
        }

        public bool StartCraft(RecipeData recipe, int multiplier)
        {
            int safeMultiplier = Mathf.Max(1, multiplier);
            if (!CanCraft(recipe, safeMultiplier))
            {
                TriggerCraftFailureFeedback();
                return false;
            }

            _activeRecipe = recipe;
            _activeCraftMultiplier = safeMultiplier;
            if (!ConsumeIngredients(recipe, safeMultiplier))
            {
                RefundIngredients();
                _activeRecipe = null;
                _activeCraftMultiplier = 1;
                TriggerCraftFailureFeedback();
                return false;
            }

            _activeCraftPowerMultiplier = ResolveCraftPowerMultiplier(this, recipe);
            _craftTimer   = 0f;
            _isCrafting   = true;
            _lastPublishedProgress = -1f;
            EnqueueCraftingTask(recipe, _activeCraftPowerMultiplier, safeMultiplier);
            SetFabricationSparksActive(true);

            // ── Уведомляем энергосеть: PowerRating изменился (0 → -craftPowerDraw) ──
            NotifyGridBalanceChanged();

            CraftingEvents.RaiseCraftStarted(recipe);
            CraftingEvents.RaiseCraftProgressUpdated(0f);
            PlaySound(craftStartSound);

            return true;
        }

        void IFabricator.StartCraft(RecipeData recipe)
        {
            StartCraft(recipe);
        }

        void IFabricator.StartCraft(RecipeData recipe, int multiplier)
        {
            StartCraft(recipe, multiplier);
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
            _activeCraftMultiplier = 1;
            ClearCraftingTaskQueue();
            SetFabricationSparksActive(false);

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
            if (!(_sparkProxyLightRemainingSeconds > 0f))
            {
                UnregisterSparkProxyLight();
                TryUnregisterSparkLightTick();
                return;
            }

            _sparkProxyLightRemainingSeconds = Mathf.Max(0f, _sparkProxyLightRemainingSeconds - Mathf.Max(0f, deltaTime));
            if (_sparkProxyLightRemainingSeconds > 0f)
            {
                UpdateSparkProxyLightRegistration();
                return;
            }

            UnregisterSparkProxyLight();
            TryUnregisterSparkLightTick();
        }

        public void SlowTick()
        {
            UpdateErrorFeedback(SlowTickDeltaSeconds);

            if (!_isCrafting)
            {
                SetFabricationSparksActive(false);
                return;
            }

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
            if (!_craftingTaskQueue.IsCreated || !_craftingTaskQueue.TryDequeue(out CraftingTask task))
            {
                CancelCraft();
                return;
            }

            if (task.ResultHashId == 0 || task.ResultQuantity <= 0)
            {
                CancelCraft();
                return;
            }

            if (!HasFabricationProgressPower())
            {
                _craftingTaskQueue.Enqueue(task);
                SetFabricationSparksActive(false);
                return;
            }

            _activeCraftPowerMultiplier = Mathf.Max(1f, task.PowerMultiplier);
            SetFabricationSparksActive(true);
            float previousProgress = task.Progress;
            bool craftCompleted = AdvanceCraftingTask(
                ref task,
                SlowTickDeltaSeconds,
                ResolveCraftThermalThrottleMultiplier(),
                out float durationSeconds,
                out float progress);
            _craftTimer = task.Progress * durationSeconds;
            if (progress > previousProgress)
            {
                RaiseFabricatorProgressAudioPing();
                RaiseFabricatorProgressHaptics(progress);
                TriggerSparkProxyLight();
            }

            if (progress - _lastPublishedProgress > ProgressPublishThreshold
                || progress >= 1f)
            {
                _lastPublishedProgress = progress;
                CraftingEvents.RaiseCraftProgressUpdated(progress);
            }

            if (craftCompleted)
            {
                CompleteCraft();
                return;
            }

            _craftingTaskQueue.Enqueue(task);
        }

        internal static bool AdvanceCraftingTask(
            ref CraftingTask task,
            float deltaSeconds,
            float thermalThrottleMultiplier,
            out float durationSeconds,
            out float progress)
        {
            durationSeconds = Mathf.Max(0.001f, task.DurationSeconds);
            float safeDelta = Mathf.Max(0f, deltaSeconds);
            float throttle = Mathf.Clamp(thermalThrottleMultiplier, 0.05f, 1f);
            task.Progress = math.saturate(task.Progress + ((safeDelta * throttle) / durationSeconds));
            progress = task.Progress;
            return progress >= 1f;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — CRAFT COMPLETION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Завершает крафт: выдаёт результат в инвентарь.
        /// После смены _isCrafting → PowerRating меняется с -craftPowerDraw на 0.
        /// NotifyGridBalanceChanged() заставляет сеть мгновенно пересчитать баланс.
        /// </summary>
        private void EnqueueCraftingTask(RecipeData recipe, float powerMultiplier, int multiplier)
        {
            EnsureCraftingScratch();
            ClearCraftingTaskQueue();
            if (!_craftingTaskQueue.IsCreated || recipe == null)
                return;

            int safeMultiplier = Mathf.Max(1, multiplier);
            _craftingTaskQueue.Enqueue(new CraftingTask
            {
                ResultHashId = ComputeItemHash(recipe.resultItem),
                ResultQuantity = ResolveCraftOutputQuantity(recipe, safeMultiplier),
                Progress = 0f,
                DurationSeconds = Mathf.Max(0.001f, recipe.craftTime * safeMultiplier),
                PowerMultiplier = Mathf.Max(1f, powerMultiplier),
                Multiplier = safeMultiplier
            });
        }

        private void ClearCraftingTaskQueue()
        {
            if (!_craftingTaskQueue.IsCreated)
                return;

            while (_craftingTaskQueue.TryDequeue(out _))
            {
            }
        }

        private static int ResolveCraftOutputQuantity(RecipeData recipe, int multiplier)
        {
            if (recipe == null)
                return 0;

            long quantity = (long)math.max(1, recipe.resultQuantity) * math.max(1, multiplier);
            return quantity > int.MaxValue ? int.MaxValue : (int)quantity;
        }

        private bool HasFabricationProgressPower()
        {
            if (!HasOperationalPower)
                return false;

            IPowerGridService powerGrid = GlobalRegistry.PowerGrid;
            if (powerGrid != null && powerGrid.BatterySnapshot.EmergencyReserveActive)
                return false;

            PowerGrid grid = CurrentPowerGrid;
            return grid == null || !grid.HasPowerDeficit || grid.SupplyRatio > 0f;
        }

        private void CompleteCraft()
        {
            RecipeData recipe = _activeRecipe;
            int craftMultiplier = Mathf.Max(1, _activeCraftMultiplier);
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
                _activeCraftMultiplier = 1;
                ClearCraftingTaskQueue();
                SetFabricationSparksActive(false);
                NotifyGridBalanceChanged();
                return;
            }

            ItemData   result = recipe.resultItem;
            int outputQuantity = ResolveCraftOutputQuantity(recipe, craftMultiplier);
            float powerCost = ResolveCraftPowerCost(recipe) * craftMultiplier;
            float craftTemperatureDelta = ResolveCraftTemperatureDeltaCelsius() * craftMultiplier;

            _isCrafting   = false;
            _activeRecipe = null;
            _craftTimer   = 0f;
            _activeCraftPowerMultiplier = 1f;
            _activeCraftMultiplier = 1;
            ClearCraftingTaskQueue();
            SetFabricationSparksActive(false);

            if (_playerInventory != null && !_playerInventory.CommitCraftReservations(_localCraftReservations, _localCraftReservationCount))
            {
                _localCraftReservationCount = 0;
                if (_networkReservation != null)
                {
                    BaseLogisticsNetwork.RollbackReserved(_networkReservation);
                    _networkReservation = null;
                }

                NotifyGridBalanceChanged();
                TriggerCraftFailureFeedback();
                return;
            }

            _localCraftReservationCount = 0;

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

            if (result != null && !TrySynthesizeCraftOutput(recipe, result, outputQuantity) && _playerInventory != null)
            {
                int resultHashId = ComputeItemHash(result);
                for (int i = 0; i < outputQuantity; i++)
                {
                    if (resultHashId == 0 || !_playerInventory.TryAddItem(resultHashId, 1))
                    {
                        int remainingQuantity = outputQuantity - i;
                        TryEmitCraftOverflowStack(result, remainingQuantity);
                        RaiseStorageCapacityExceededBark();
                        TriggerCraftFailureFeedback();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.LogWarning("[Fabricator] Craft output overflow; routed to diegetic bark/drop fallback.");
#endif
                        break;
                    }
                }
            }

            CraftingEvents.RaiseCraftProgressUpdated(1f);

            if (result != null)
                CraftingEvents.RaiseCraftCompleted(result);

            PlaySound(craftCompleteSound);
            TryRestartContinuousCraft(recipe, craftMultiplier);
        }

        private void TryRestartContinuousCraft(RecipeData recipe, int multiplier)
        {
            if (!isContinuous || recipe == null || _isCrafting)
                return;

            StartCraft(recipe, multiplier);
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

        private bool PassesBiomeLock(RecipeData recipe)
        {
            if (recipe == null || !recipe.RequiresAnchoredBiomeLock)
                return true;

            if (thermalHostModule == null)
                CacheThermalHostModule();

            if (thermalHostModule == null || thermalHostModule.IsUnmoored || thermalHostModule.IsDetachedDebris)
                return false;

            Vector3 samplePosition = thermalHostModule.transform.position;
            WorldProceduralFieldSampler sampler = WorldProceduralFieldSampler.ActiveRuntimeInstance;
            if (sampler != null &&
                sampler.TrySampleBiomeInfluence(
                    samplePosition,
                    out WorldProceduralFieldSampler.BiomeInfluenceCell influence,
                    out HectonBiomeMatrixProfile primaryProfile,
                    out HectonBiomeMatrixProfile secondaryProfile))
            {
                return MatchesRecipeBiomeLock(recipe, primaryProfile, influence.PrimaryBiomeId) ||
                       MatchesRecipeBiomeLock(recipe, secondaryProfile, influence.SecondaryBiomeId);
            }

            BiomeMatrixDirector matrixDirector = BiomeMatrixDirector.ActiveRuntimeInstance;
            return matrixDirector != null && MatchesRecipeBiomeLock(
                recipe,
                matrixDirector.CurrentProfile,
                matrixDirector.CurrentProfile != null ? matrixDirector.CurrentProfile.matrixIndex : 0);
        }

        private static bool MatchesRecipeBiomeLock(RecipeData recipe, HectonBiomeMatrixProfile profile, int biomeId)
        {
            if (recipe == null || profile == null)
                return false;

            if (recipe.requiredAnchoredBiomeMatrixId > 0 && biomeId == recipe.requiredAnchoredBiomeMatrixId)
                return true;

            string requiredFamilyId = recipe.requiredAnchoredBiomeFamilyId;
            if (string.IsNullOrWhiteSpace(requiredFamilyId))
                return false;

            if (!string.IsNullOrEmpty(profile.familyId) &&
                string.Equals(profile.familyId, requiredFamilyId, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            HectonBiomeFamilyProfile family = profile.familyProfile;
            return family != null &&
                   !string.IsNullOrEmpty(family.familyId) &&
                   string.Equals(family.familyId, requiredFamilyId, System.StringComparison.OrdinalIgnoreCase);
        }

        private float ResolveCraftTemperatureDeltaCelsius()
        {
            if (!(craftTemperatureDeltaCelsius > 0f) || !float.IsFinite(craftTemperatureDeltaCelsius))
                return 0f;

            float delta = craftTemperatureDeltaCelsius * Mathf.Max(1f, _activeCraftPowerMultiplier);
            return float.IsFinite(delta) ? delta : 0f;
        }

        private float ResolveCraftThermalThrottleMultiplier()
        {
            if (thermalHostModule == null)
                CacheThermalHostModule();

            if (thermalHostModule == null)
                return 1f;

            float hostRoomTemperatureCelsius = thermalHostModule.ResolveHostRoomTemperatureCelsius();
            return hostRoomTemperatureCelsius > ThermalThrottleTemperatureCelsius
                ? ThermalThrottleProgressMultiplier
                : 1f;
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

        private bool HasIngredients(RecipeData recipe, int multiplier = 1)
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
                _craftRecipeEvaluationResult,
                _complexRecipeGraphNodes,
                _complexRecipeGraphEdges,
                _complexRecipeGraphInDegrees,
                _complexRecipeGraphQueue,
                _complexRecipeRawCosts,
                _complexRecipeRawCostCount,
                _complexRecipeGraphStatus,
                Mathf.Max(1, multiplier));
        }

        private void EnsureCraftingScratch()
        {
            if (!_craftInventoryCounts.IsCreated)
            {
                // COLD ALLOC: NativeParallelHashMap<Int32,Int32>[128] — temporary per-craft accessible item counts — owner: Fabricator
                _craftInventoryCounts = new NativeParallelHashMap<int, int>(128, Allocator.Persistent);
                NativeMemorySentinel.RegisterNativeParallelHashMap(_craftInventoryCounts, NativeMemoryOwner, nameof(_craftInventoryCounts), NativeMemoryLifetime);
            }

            if (!_craftRecipeCosts.IsCreated)
            {
                // COLD ALLOC: NativeArray<int2>[32] — flattened recipe ingredient cost buffer — owner: Fabricator
                _craftRecipeCosts = new NativeArray<int2>(CraftingSystem.MaxRecipeIngredientCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                RegisterTrackedNativeArray(_craftRecipeCosts, nameof(_craftRecipeCosts));
            }

            if (!_craftRecipeEvaluationResult.IsCreated)
            {
                // COLD ALLOC: NativeArray<byte>[1] — Burst crafting-availability result cell — owner: Fabricator
                _craftRecipeEvaluationResult = new NativeArray<byte>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                RegisterTrackedNativeArray(_craftRecipeEvaluationResult, nameof(_craftRecipeEvaluationResult));
            }

            if (!_deconstructionRecipeOutputs.IsCreated)
            {
                // COLD ALLOC: NativeArray<int2>[32] — deconstruction output yield scratch — owner: Fabricator
                _deconstructionRecipeOutputs = new NativeArray<int2>(CraftingSystem.MaxDeconstructionOutputCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                RegisterTrackedNativeArray(_deconstructionRecipeOutputs, nameof(_deconstructionRecipeOutputs));
            }

            if (!_deconstructionOutputCount.IsCreated)
            {
                // COLD ALLOC: NativeArray<int>[1] — deconstruction output count cell — owner: Fabricator
                _deconstructionOutputCount = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                RegisterTrackedNativeArray(_deconstructionOutputCount, nameof(_deconstructionOutputCount));
            }

            if (!_craftingTaskQueue.IsCreated)
            {
                // COLD ALLOC: NativeQueue<CraftingTask>[1] - asynchronous fabrication task lane - owner: Fabricator
                _craftingTaskQueue = new NativeQueue<CraftingTask>(Allocator.Persistent);
                NativeMemorySentinel.RegisterNativeQueue(
                    _craftingTaskQueue,
                    MaxQueuedCraftingTasks,
                    NativeMemoryOwner,
                    nameof(_craftingTaskQueue),
                    NativeMemoryLifetime);
                _craftingTaskQueue.Enqueue(default);
                _craftingTaskQueue.Dequeue();
            }

            if (!_complexRecipeGraphNodes.IsCreated)
            {
                _complexRecipeGraphNodes = new NativeArray<int2>(CraftingSystem.MaxComplexRecipeNodeCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                RegisterTrackedNativeArray(_complexRecipeGraphNodes, nameof(_complexRecipeGraphNodes));
            }

            if (!_complexRecipeGraphEdges.IsCreated)
            {
                _complexRecipeGraphEdges = new NativeArray<int2>(CraftingSystem.MaxComplexRecipeEdgeCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                RegisterTrackedNativeArray(_complexRecipeGraphEdges, nameof(_complexRecipeGraphEdges));
            }

            if (!_complexRecipeGraphInDegrees.IsCreated)
            {
                _complexRecipeGraphInDegrees = new NativeArray<int>(CraftingSystem.MaxComplexRecipeNodeCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                RegisterTrackedNativeArray(_complexRecipeGraphInDegrees, nameof(_complexRecipeGraphInDegrees));
            }

            if (!_complexRecipeGraphQueue.IsCreated)
            {
                _complexRecipeGraphQueue = new NativeArray<int>(CraftingSystem.MaxComplexRecipeNodeCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                RegisterTrackedNativeArray(_complexRecipeGraphQueue, nameof(_complexRecipeGraphQueue));
            }

            if (!_complexRecipeRawCosts.IsCreated)
            {
                _complexRecipeRawCosts = new NativeArray<int2>(CraftingSystem.MaxRecipeIngredientCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                RegisterTrackedNativeArray(_complexRecipeRawCosts, nameof(_complexRecipeRawCosts));
            }

            if (!_complexRecipeRawCostCount.IsCreated)
            {
                _complexRecipeRawCostCount = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                RegisterTrackedNativeArray(_complexRecipeRawCostCount, nameof(_complexRecipeRawCostCount));
            }

            if (!_complexRecipeGraphStatus.IsCreated)
            {
                _complexRecipeGraphStatus = new NativeArray<byte>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                RegisterTrackedNativeArray(_complexRecipeGraphStatus, nameof(_complexRecipeGraphStatus));
            }

            if (!_unlockedRecipes.IsCreated)
            {
                // COLD ALLOC: NativeArray<UInt64>[8] - recipe unlock bitset for fabricator craft gate - owner: Fabricator
                _unlockedRecipes = new NativeArray<ulong>(MaxUnlockedRecipeWords, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                RegisterTrackedNativeArray(_unlockedRecipes, nameof(_unlockedRecipes));
                _unlockMaskDirty = true;
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
            {
                NativeMemorySentinel.UnregisterNativeParallelHashMap(NativeMemoryOwner, nameof(_craftInventoryCounts));
                _craftInventoryCounts.Dispose();
            }

            DisposeTrackedNativeArray(ref _craftRecipeCosts);
            DisposeTrackedNativeArray(ref _craftRecipeEvaluationResult);
            DisposeTrackedNativeArray(ref _deconstructionRecipeOutputs);
            DisposeTrackedNativeArray(ref _deconstructionOutputCount);
            if (_craftingTaskQueue.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(NativeMemoryOwner, nameof(_craftingTaskQueue));
                _craftingTaskQueue.Dispose();
                _craftingTaskQueue = default;
            }

            DisposeTrackedNativeArray(ref _complexRecipeGraphNodes);
            DisposeTrackedNativeArray(ref _complexRecipeGraphEdges);
            DisposeTrackedNativeArray(ref _complexRecipeGraphInDegrees);
            DisposeTrackedNativeArray(ref _complexRecipeGraphQueue);
            DisposeTrackedNativeArray(ref _complexRecipeRawCosts);
            DisposeTrackedNativeArray(ref _complexRecipeRawCostCount);
            DisposeTrackedNativeArray(ref _complexRecipeGraphStatus);
            DisposeTrackedNativeArray(ref _unlockedRecipes);
        }

        private static void RegisterTrackedNativeArray<T>(NativeArray<T> array, string label) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.RegisterNativeArray(
                array,
                NativeMemoryOwner,
                label,
                NativeMemoryLifetime);
        }

        private static void DisposeTrackedNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }

        private bool TrySynthesizeCraftOutput(RecipeData recipe, ItemData result, int quantityOverride)
        {
            if (recipe == null || result == null)
                return false;

            PersistentWorldRegistry registry = GlobalRegistry.PersistentWorldRegistry;
            if (registry == null)
                return false;

            int quantity = math.max(1, quantityOverride);
            ResolveCraftOutputPose(out Vector3 spawnPosition, out Vector3 velocityChange);
            bool synthesized = registry.TryRegisterDroppedItem(result, quantity, spawnPosition, Vector3.zero, velocityChange);
            if (!synthesized)
                return false;

            CraftingEvents.RaiseCraftOutputSynthesized(
                new CraftedItemSynthesisEvent(result, quantity, spawnPosition, velocityChange));
            return true;
        }

        private bool TryEmitCraftOverflowStack(ItemData result, int quantity)
        {
            if (result == null || quantity <= 0)
                return false;

            PersistentWorldRegistry registry = GlobalRegistry.PersistentWorldRegistry;
            if (registry == null)
                return false;

            ResolveCraftOutputPose(out Vector3 spawnPosition, out Vector3 velocityChange);
            bool synthesized = registry.TryRegisterDroppedItem(result, quantity, spawnPosition, Vector3.zero, velocityChange);
            if (!synthesized)
                return false;

            CraftingEvents.RaiseCraftOutputSynthesized(
                new CraftedItemSynthesisEvent(result, quantity, spawnPosition, velocityChange));
            return true;
        }

        /// <summary>
        /// Grinds one crafted item back into authored salvage stacks.
        /// </summary>
        public bool TryDeconstructItem(int itemHashId)
        {
            if (itemHashId == 0 || _playerInventory == null)
                return false;

            Hecton8.SaveSystem.ItemCatalog itemCatalog = _playerInventory.ItemCatalog;
            if (itemCatalog == null)
                return false;

            ItemData targetItem = itemCatalog.FindByHash(itemHashId);
            if (targetItem == null || targetItem.DeconstructYieldCount <= 0)
                return false;

            if (!_playerInventory.TryRemoveFirstMatchingItemByHash(itemHashId))
                return false;

            EnsureCraftingScratch();
            if (!CraftingSystem.TryBuildDeconstructionYieldBuffer(
                    targetItem,
                    _deconstructionRecipeOutputs,
                    _deconstructionOutputCount))
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

                if (!TryEmitDeconstructionYield(outputItem, output.x, output.y, spawnPosition, velocityChange))
                    continue;

                CraftingEvents.RaiseCraftOutputSynthesized(
                    new CraftedItemSynthesisEvent(outputItem, output.y, spawnPosition, velocityChange));
                emittedAny = true;
            }

            if (!emittedAny)
                _playerInventory.TryAddItem(itemHashId, 1);

            return emittedAny;
        }

        private bool TryEmitDeconstructionYield(
            ItemData outputItem,
            int itemHashId,
            int quantity,
            Vector3 spawnPosition,
            Vector3 velocityChange)
        {
            if (outputItem == null || itemHashId == 0 || quantity <= 0)
                return false;

            PersistentWorldRegistry registry = GlobalRegistry.PersistentWorldRegistry;
            if (registry != null &&
                registry.TryRegisterDroppedItem(outputItem, quantity, spawnPosition, Vector3.zero, velocityChange))
            {
                return true;
            }

            return quantity == 1 && _playerInventory != null && _playerInventory.TryAddItem(itemHashId, 1);
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

        private int CountReclaimableIngredientCells(RecipeData recipe, int multiplier = 1)
        {
            if (recipe == null || recipe.ingredients == null || _playerInventory == null)
                return 0;

            int total = 0;
            List<InventoryCost> costs = recipe.ingredients;
            int safeMultiplier = Mathf.Max(1, multiplier);

            for (int i = 0, count = costs.Count; i < count; i++)
            {
                InventoryCost cost = costs[i];
                if (cost == null || cost.item == null) continue;

                int localAvailable = CountAvailableItemInInventory(_playerInventory, cost.item);
                int requiredAmount = GetAdjustedIngredientAmount(cost) * safeMultiplier;
                int removableCount = localAvailable < requiredAmount ? localAvailable : requiredAmount;
                total += cost.item.CellArea * removableCount;
            }

            return total;
        }

        private bool ConsumeIngredients(RecipeData recipe, int multiplier = 1)
        {
            if (recipe == null || recipe.ingredients == null || _playerInventory == null || _playerInventory.Grid == null)
                return false;

            EnsureCraftingScratch();
            _localCraftReservationCount = 0;
            _networkCostCount = 0;

            if (_networkReservation != null)
            {
                BaseLogisticsNetwork.RollbackReserved(_networkReservation);
                _networkReservation = null;
            }

            int safeMultiplier = Mathf.Max(1, multiplier);
            if (CraftingSystem.TryBuildRecipeCostBuffer(recipe, this, _craftRecipeCosts, out int recipeCostCount, safeMultiplier) &&
                TryReserveIngredientCostBuffer(_craftRecipeCosts, recipeCostCount))
                return true;

            RefundIngredients();

            if (CraftingSystem.TryBuildTotalRawCostBuffer(
                    recipe,
                    this,
                    _playerInventory.ItemCatalog,
                    _complexRecipeGraphNodes,
                    _complexRecipeGraphEdges,
                    _complexRecipeGraphInDegrees,
                    _complexRecipeGraphQueue,
                    _complexRecipeRawCosts,
                    _complexRecipeRawCostCount,
                    _complexRecipeGraphStatus,
                    safeMultiplier))
            {
                return TryReserveIngredientCostBuffer(_complexRecipeRawCosts, _complexRecipeRawCostCount[0]);
            }

            return false;
        }

        private bool TryReserveIngredientCostBuffer(NativeArray<int2> costs, int costCount)
        {
            if (!costs.IsCreated || costCount <= 0 || _playerInventory == null)
                return false;

            _localCraftReservationCount = 0;
            _networkCostCount = 0;
            if (_networkReservation != null)
            {
                BaseLogisticsNetwork.RollbackReserved(_networkReservation);
                _networkReservation = null;
            }

            for (int costIndex = 0; costIndex < costCount; costIndex++)
            {
                int2 cost = costs[costIndex];
                if (cost.x == 0 || cost.y <= 0)
                    continue;

                int remaining = cost.y;
                int localAvailable = _playerInventory.CountAvailableTotal(cost.x);
                int localTake = localAvailable < remaining ? localAvailable : remaining;
                if (localTake > 0)
                {
                    if (!_playerInventory.TryReserveQuantityForCraft(
                            cost.x,
                            localTake,
                            _localCraftReservations,
                            ref _localCraftReservationCount))
                        return false;

                    remaining -= localTake;
                }

                if (remaining > 0 && !TryAccumulateNetworkCost(cost.x, remaining))
                    return false;
            }

            if (_networkCostCount <= 0)
                return true;

            PowerGrid gridRef = _powerNode != null ? _powerNode.Grid : null;
            return BaseLogisticsNetwork.TryReserveResources(
                gridRef,
                _networkCostItemHashes,
                _networkCostAmounts,
                _networkCostCount,
                out _networkReservation);
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

        private void RaiseFabricatorProgressAudioPing()
        {
            float pitchCarrierHz = Mathf.Clamp(900f + (_activeCraftPowerMultiplier * 180f), 900f, 2200f);
            ProceduralAudioEvents.RaiseAudioPingTriggered(
                transform.position,
                Mathf.Clamp01(0.18f + _activeCraftPowerMultiplier * 0.08f),
                0.08f,
                1f,
                pitchCarrierHz,
                ProceduralAudioPingKind.MechanicalWhirr);
        }

        private static void RaiseStorageCapacityExceededBark()
        {
            AcousticEcholocationBarkEvents.RaiseStorageCapacityExceeded();
        }

        private static void RaiseFabricatorProgressHaptics(float progress)
        {
            float finalPulse01 = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.9f, 1f, progress));
            float lowFrequencyIntensity = Mathf.Clamp01(Mathf.Lerp(0.12f, 0.3f, progress) + finalPulse01 * 0.35f);
            float highFrequencyIntensity = Mathf.Clamp01(0.025f + finalPulse01 * 0.05f);
            float pulseFrequencyHz = Mathf.Lerp(18f, 30f, finalPulse01);
            ToolHapticsRuntime.EnqueueSinusoidalCommand(
                lowFrequencyIntensity,
                highFrequencyIntensity,
                0.18f,
                pulseFrequencyHz,
                finalPulse01 > 0f ? FabricatorFinalHapticPriority : FabricatorHapticPriority,
                FabricatorHapticMotorMask);
        }

        private void TriggerCraftFailureFeedback()
        {
            _errorFlashRemainingSeconds = Mathf.Max(_errorFlashRemainingSeconds, errorFlashDurationSeconds);
            ApplyErrorFeedback(1f);
            CraftingEvents.RaiseCraftFailed(this);
            PlaySound(fabricationErrorBuzzerSound);
            ProceduralAudioEvents.RaiseAudioPingTriggered(
                transform.position,
                0.85f,
                0.12f,
                1f,
                180f,
                ProceduralAudioPingKind.MechanicalWhirr);
        }

        private void TriggerSparkProxyLight()
        {
            _sparkProxyLightRemainingSeconds = Mathf.Max(_sparkProxyLightRemainingSeconds, Mathf.Max(0.01f, sparkProxyLightDurationSeconds));
            UpdateSparkProxyLightRegistration();
            TryRegisterSparkLightTick();
        }

        private void UpdateSparkProxyLightRegistration()
        {
            if (_sparkProxyLightKey == 0 || !(_sparkProxyLightRemainingSeconds > 0f))
                return;

            Transform origin = outputSocket != null ? outputSocket : transform;
            if (origin == null)
                return;

            Vector3 position = origin.position;
            AbsoluteUniversePosition positionAup = AbsoluteUniversePosition.FromRuntimePosition(position);
            float normalizedLifetime = Mathf.Clamp01(_sparkProxyLightRemainingSeconds / Mathf.Max(0.01f, sparkProxyLightDurationSeconds));
            float intensity = sparkProxyLightIntensity * normalizedLifetime * Mathf.Max(1f, _activeCraftPowerMultiplier);
            ProxyLightData lightData = ProxyLightData.CreateTransientPoint(
                positionAup,
                position,
                sparkProxyLightColor.linear,
                sparkProxyLightRangeMeters,
                intensity,
                Time.unscaledTime);

            _sparkProxyLightRegistered = ProxyLightRegistry.RegisterOrUpdate(_sparkProxyLightKey, in lightData) || _sparkProxyLightRegistered;
        }

        private void UnregisterSparkProxyLight()
        {
            if (!_sparkProxyLightRegistered || _sparkProxyLightKey == 0)
                return;

            ProxyLightRegistry.Unregister(_sparkProxyLightKey);
            _sparkProxyLightRegistered = false;
        }

        private void SetFabricationSparksActive(bool active)
        {
            if (fabricationSparks == null)
                return;

            ParticleSystem.EmissionModule emission = fabricationSparks.emission;
            float rate = active ? fabricationSparksBaseRate * Mathf.Max(1f, _activeCraftPowerMultiplier) : 0f;
            emission.rateOverTime = rate;

            if (active)
            {
                if (!_fabricationSparksPlaying)
                {
                    fabricationSparks.Play(false);
                    _fabricationSparksPlaying = true;
                }

                return;
            }

            _sparkProxyLightRemainingSeconds = 0f;
            UnregisterSparkProxyLight();
            TryUnregisterSparkLightTick();
            if (_fabricationSparksPlaying)
            {
                fabricationSparks.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                _fabricationSparksPlaying = false;
            }
        }

        private void UpdateErrorFeedback(float deltaSeconds)
        {
            if (!(_errorFlashRemainingSeconds > 0f))
            {
                if (_errorFeedbackApplied)
                    ApplyErrorFeedback(0f);
                return;
            }

            _errorFlashRemainingSeconds = Mathf.Max(0f, _errorFlashRemainingSeconds - Mathf.Max(0f, deltaSeconds));
            float intensity = Mathf.Clamp01(_errorFlashRemainingSeconds / Mathf.Max(0.001f, errorFlashDurationSeconds));
            ApplyErrorFeedback(intensity);
        }

        private void ApplyErrorFeedback(float intensity)
        {
            if (errorFeedbackRenderers == null || errorFeedbackRenderers.Length == 0)
            {
                _errorFeedbackApplied = intensity > 0f;
                return;
            }

            if (_errorFeedbackBlock == null)
                _errorFeedbackBlock = new MaterialPropertyBlock();
            Color color = errorEmissionColor * Mathf.Clamp01(intensity);
            for (int index = 0; index < errorFeedbackRenderers.Length; index++)
            {
                Renderer renderer = errorFeedbackRenderers[index];
                if (renderer == null)
                    continue;

                renderer.GetPropertyBlock(_errorFeedbackBlock);
                _errorFeedbackBlock.SetColor(EmissionColorId, color);
                renderer.SetPropertyBlock(_errorFeedbackBlock);
            }

            _errorFeedbackApplied = intensity > 0f;
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
            _unlockMaskDirty = true;
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

        private void EnsureRecipeUnlockMask()
        {
            EnsureCraftingScratch();
            if (!_unlockMaskDirty || !_unlockedRecipes.IsCreated)
                return;

            EnsureScanLogSystem();
            for (int wordIndex = 0; wordIndex < _unlockedRecipes.Length; wordIndex++)
                _unlockedRecipes[wordIndex] = 0UL;

            int unlockIndex = 0;
            if (availableRecipes != null)
            {
                for (int i = 0; i < availableRecipes.Count && unlockIndex < MaxUnlockedRecipeWords * 64; i++)
                    WriteRecipeUnlockBit(availableRecipes[i], unlockIndex++);
            }

            int runtimeRecipeCount = ModRecipeRegistry.Count;
            for (int i = 0; i < runtimeRecipeCount && unlockIndex < MaxUnlockedRecipeWords * 64; i++)
            {
                RecipeData recipe = ModRecipeRegistry.GetAt(i);
                if (recipe == null || ContainsAuthoredRecipeReference(recipe))
                    continue;

                WriteRecipeUnlockBit(recipe, unlockIndex++);
            }

            _unlockMaskDirty = false;
        }

        private void WriteRecipeUnlockBit(RecipeData recipe, int unlockIndex)
        {
            if (recipe == null || !_unlockedRecipes.IsCreated)
                return;

            int wordIndex = unlockIndex >> RecipeUnlockWordShift;
            if (wordIndex < 0 || wordIndex >= _unlockedRecipes.Length)
                return;

            if (recipe.IsUnlocked(_scanLogSystem))
                _unlockedRecipes[wordIndex] = _unlockedRecipes[wordIndex] | (1UL << (unlockIndex & RecipeUnlockBitMask));
        }

        private bool TryResolveRecipeUnlockIndex(RecipeData recipe, out int unlockIndex)
        {
            unlockIndex = -1;
            if (recipe == null)
                return false;

            int cursor = 0;
            if (availableRecipes != null)
            {
                for (int i = 0; i < availableRecipes.Count; i++)
                {
                    if (ReferenceEquals(availableRecipes[i], recipe))
                    {
                        unlockIndex = cursor;
                        return IsUnlockIndexInRange(unlockIndex);
                    }

                    cursor++;
                }
            }

            int runtimeRecipeCount = ModRecipeRegistry.Count;
            for (int i = 0; i < runtimeRecipeCount; i++)
            {
                RecipeData runtimeRecipe = ModRecipeRegistry.GetAt(i);
                if (runtimeRecipe == null || ContainsAuthoredRecipeReference(runtimeRecipe))
                    continue;

                if (ReferenceEquals(runtimeRecipe, recipe))
                {
                    unlockIndex = cursor;
                    return IsUnlockIndexInRange(unlockIndex);
                }

                cursor++;
            }

            return false;
        }

        private static bool IsUnlockIndexInRange(int unlockIndex)
        {
            return unlockIndex >= 0 && unlockIndex < MaxUnlockedRecipeWords * 64;
        }

        private bool IsRecipeUnlockBitSet(int unlockIndex)
        {
            if (!_unlockedRecipes.IsCreated || !IsUnlockIndexInRange(unlockIndex))
                return false;

            int wordIndex = unlockIndex >> RecipeUnlockWordShift;
            return (_unlockedRecipes[wordIndex] & (1UL << (unlockIndex & RecipeUnlockBitMask))) != 0UL;
        }

        private bool IsRecipeUnlocked(RecipeData recipe)
        {
            if (recipe == null)
                return false;

            EnsureRecipeUnlockMask();
            if (TryResolveRecipeUnlockIndex(recipe, out int unlockIndex))
                return IsRecipeUnlockBitSet(unlockIndex);

            return recipe.IsUnlocked(_scanLogSystem);
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

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _tickRegistered = GlobalRegistry.SlowTickables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_tickRegistered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _tickRegistered = false;
        }

        private void TryRegisterSparkLightTick()
        {
            if (_sparkLightTickRegistered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _sparkLightTickRegistered = GlobalRegistry.Updatables.Contains(this);
        }

        private void TryUnregisterSparkLightTick()
        {
            if (!_sparkLightTickRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _sparkLightTickRegistered = false;
        }

        private void RebuildInteractText()
        {
            ReadOnlySpan<char> fallbackName = string.IsNullOrWhiteSpace(fabricatorName)
                ? ResolveLocalizedSpan(_uiFabricatorLocalizationHash, "FABRICATOR".AsSpan())
                : fabricatorName.AsSpan();
            ReadOnlySpan<char> pattern = ResolveLocalizedSpan(_interactUseFabricatorLocalizationHash, "Use {0}".AsSpan());

            _interactTextLength = WriteInteractTemplate(pattern, fallbackName, _interactTextBuffer);
            ReadOnlySpan<char> cachedPrompt = _interactText == null ? ReadOnlySpan<char>.Empty : _interactText.AsSpan();
            ReadOnlySpan<char> nextPrompt = _interactTextBuffer.AsSpan(0, _interactTextLength);
            if (cachedPrompt.SequenceEqual(nextPrompt))
                return;

            _interactText = new string(_interactTextBuffer, 0, _interactTextLength); // COLD ALLOC: string[<=96] - cached IInteractable compatibility prompt rebuilt on localization change - owner: Fabricator
        }

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }


        private void HandleLanguageChanged(GameLanguage language)
        {
            RebuildInteractText();
        }

        private static ReadOnlySpan<char> ResolveLocalizedSpan(int keyHash, ReadOnlySpan<char> fallback)
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            if (manager == null)
                return fallback;

            ReadOnlySpan<char> localized = manager.GetRawSpanOrFallback(keyHash, fallback);
            return localized.IsEmpty ? fallback : localized;
        }

        private static int WriteInteractTemplate(ReadOnlySpan<char> template, ReadOnlySpan<char> value, char[] destination)
        {
            if (destination == null || destination.Length == 0)
                return 0;

            int cursor = 0;
            int placeholderIndex = template.IndexOf("{0}".AsSpan());
            if (placeholderIndex < 0)
            {
                cursor = AppendSpan(template, destination, cursor);
                if (cursor < destination.Length)
                    destination[cursor++] = ' ';
                return AppendSpan(value, destination, cursor);
            }

            cursor = AppendSpan(template.Slice(0, placeholderIndex), destination, cursor);
            cursor = AppendSpan(value, destination, cursor);
            return AppendSpan(template.Slice(placeholderIndex + 3), destination, cursor);
        }

        private static int AppendSpan(ReadOnlySpan<char> source, char[] destination, int cursor)
        {
            if (destination == null || destination.Length == 0)
                return 0;

            if (cursor >= destination.Length || source.IsEmpty)
                return Mathf.Clamp(cursor, 0, destination.Length);

            int writable = Mathf.Min(source.Length, destination.Length - cursor);
            source.Slice(0, writable).CopyTo(destination.AsSpan(cursor));
            return cursor + writable;
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

