// ============================================================================
// HECTON-8 — PlayerToolManager.cs
// Контроллер переключения инструментов в руках игрока.
//
// Ответственности:
//   1. Слушает ввод (кнопки 1-4) через ITickable.Tick().
//   2. Проверяет наличие инструмента в PlayerInventory.
//   3. Спавнит/деспавнит инструменты через ObjectPoolManager.
//   4. Управляет плавной анимацией смены (lower → raise).
//   5. Делегирует UsePrimary/UseSecondary текущему инструменту.
//
// ZERO GC:
//   • Кэшированные KeyCode[] — нет аллокаций при проверке ввода.
//   • Spawn/Despawn через пул — никаких Instantiate/Destroy.
//   • Никаких строковых операций в горячих путях.
//   • math.lerp для анимации — zero GC.
//
// ЗАВИСИМОСТИ:
//   • GameTickManager (регистрация ITickable)
//   • ObjectPoolManager (спавн/деспавн инструментов)
//   • PlayerInventory (проверка наличия инструмента)
//   • PlayerTool (базовый класс инструментов)
// ============================================================================

namespace Hecton8.Gameplay
{
    using System;
    using Hecton8.Building;
    using Hecton8.Core;
    using Hecton8.Construction;
    using Hecton8.Inventory;
    using Hecton8.Items;
    using Hecton8.Input;
    using Hecton8.Tools;
    using UnityEngine;
#if UNITY_EDITOR
    using UnityEditor;
#endif

    [DisallowMultipleComponent]
    public sealed class PlayerToolManager : MonoBehaviour, ITickable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── References ────────────────────────────────")]
        [Tooltip("Transform точки крепления инструмента (дочерний объект камеры).")]
        [SerializeField] private Transform handAnchor;

        [Tooltip("Ссылка на инвентарь игрока для проверки наличия инструментов.")]
        [SerializeField] private PlayerInventory playerInventory;

        [Header("── Tool Prefabs (слоты 1-4) ──────────────────")]
        [Tooltip("Префабы инструментов, привязанные к кнопкам 1-4. " +
                 "Пустые слоты — оставить null.")]
        [SerializeField] private GameObject[] toolPrefabs = new GameObject[4];

        [Header("── Known Tool Prefabs ────────────────────────")]
        [Tooltip("Полный реестр held-tool prefab'ов для PDA / quick-slot assignment.")]
        [SerializeField] private GameObject[] knownToolPrefabs = new GameObject[12];

        [Header("â”€â”€ Pool Warmup â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("ÐŸÑ€Ð¾Ð³Ñ€ÐµÐ²Ð°ÐµÑ‚ assigned held-tool pools Ð¿Ñ€Ð¸ Ð²ÐºÐ»ÑŽÑ‡ÐµÐ½Ð¸Ð¸ Ð¼ÐµÐ½ÐµÐ´Ð¶ÐµÑ€Ð°, Ñ‡Ñ‚Ð¾Ð±Ñ‹ ÑƒÐ±Ñ€Ð°Ñ‚ÑŒ runtime Instantiate Ð¿Ñ€Ð¸ Ð¿ÐµÑ€Ð²Ð¾Ð¼ ÑÐºÐ¸Ð¿Ðµ.")]
        [SerializeField] private bool warmupAssignedToolPoolsOnEnable = true;
        [Tooltip("ÐœÐ¸Ð½Ð¸Ð¼Ð°Ð»ÑŒÐ½Ñ‹Ð¹ Ñ€ÐµÐ·ÐµÑ€Ð² ÑÐºÐ·ÐµÐ¼Ð¿Ð»ÑÑ€Ð¾Ð² Ð² pool Ð´Ð»Ñ ÐºÐ°Ð¶Ð´Ð¾Ð³Ð¾ assigned held-tool prefab.")]
        [SerializeField] private int toolPoolWarmupCount = 1;
        [Tooltip("ÐœÐ¸Ð½Ð¸Ð¼Ð°Ð»ÑŒÐ½Ñ‹Ð¹ Ñ€ÐµÐ·ÐµÑ€Ð² ghost prefab'Ð¾Ð² Ð´Ð»Ñ ÑÑ‚Ñ€Ð¾Ð¸Ñ‚ÐµÐ»ÑŒÐ½Ð¾Ð³Ð¾ ÐºÐ°Ñ‚Ð°Ð»Ð¾Ð³Ð°.")]
        [SerializeField] private int constructionGhostWarmupCount = 1;

        [Header("── Swap Animation ────────────────────────────")]
        [Tooltip("Скорость анимации смены инструмента (lerp factor per second). " +
                 "Больше = быстрее.")]
        [SerializeField] private float swapSpeed = 8f;

        [Tooltip("Смещение инструмента вниз при анимации смены (локальные координаты).")]
        [SerializeField] private Vector3 lowerOffset = new Vector3(0f, -0.5f, 0f);

        [Header("── Diagnostics ───────────────────────────────")]
        [SerializeField] private int _debugCurrentSlot = -1;
        [SerializeField] private string _debugStateName;
        [SerializeField] private bool toolDebugLogging;

        // SlotKeys removed — handled by InputManager events

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>Текущий активный экземпляр инструмента (из пула).</summary>
        private GameObject _currentInstance;

        /// <summary>Компонент PlayerTool на текущем экземпляре.</summary>
        private PlayerTool _currentTool;

        /// <summary>Индекс текущего активного слота (-1 = ничего).</summary>
        private int _currentSlotIndex = -1;

        /// <summary>Индекс слота, на который переключаемся (-1 = нет запроса).</summary>
        private int _pendingSlotIndex = -1;

        /// <summary>Текущее состояние конечного автомата смены инструмента.</summary>
        private SwapState _swapState = SwapState.Idle;

        /// <summary>Прогресс анимации [0..1]. 0 = начало, 1 = завершено.</summary>
        private float _swapProgress;

        /// <summary>
        /// Начальная локальная позиция handAnchor.
        /// Запоминаем при Awake — это «нормальное» положение инструмента.
        /// </summary>
        private Vector3 _anchorRestPosition;

        /// <summary>Целевая позиция при опускании (rest + offset).</summary>
        private Vector3 _anchorLoweredPosition;
        private InputManager _subscribedInputManager;
        private readonly string[] _slotNameCache = new string[4];
        private bool _assignedPoolsWarmed;
        private bool _constructionGhostPoolsWarmed;

        public event Action<int> ActiveSlotChanged;
        public event Action ToolAssignmentsChanged;

        // ══════════════════════════════════════════════════════════
        //  SWAP STATE MACHINE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Конечный автомат анимации смены инструмента.
        /// 
        /// Idle → Lowering → Raising → Idle
        ///
        /// Lowering: инструмент плавно уходит вниз. По завершении —
        ///           деспавн старого, спавн нового.
        /// Raising:  новый инструмент плавно поднимается в рабочую позицию.
        /// </summary>
        private enum SwapState
        {
            /// <summary>Инструмент на месте, анимация не идёт.</summary>
            Idle,

            /// <summary>Опускаем текущий инструмент вниз перед сменой.</summary>
            Lowering,

            /// <summary>Поднимаем новый инструмент вверх после спавна.</summary>
            Raising
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            if (handAnchor != null)
            {
                _anchorRestPosition    = handAnchor.localPosition;
                _anchorLoweredPosition = _anchorRestPosition + lowerOffset;
            }

            RefreshSlotNameCache();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            AutoResolveKnownToolPrefabs();
        }
#endif

        private void OnEnable()
        {
            GameTickManager.Instance?.Register((ITickable)this);
            RefreshInputSubscriptions();
            WarmRuntimePoolsIfNeeded();

            if (playerInventory != null)
                playerInventory.InventoryChanged += HandleInventoryChanged;
        }

        private void OnDisable()
        {
            GameTickManager.Instance?.Unregister((ITickable)this);
            UnsubscribeFromInputManager();

            if (playerInventory != null)
                playerInventory.InventoryChanged -= HandleInventoryChanged;

            // Деспавним текущий инструмент при отключении менеджера
            DespawnCurrentTool();
        }

        private void RefreshInputSubscriptions()
        {
            InputManager currentManager = InputManager.Instance;
            if (ReferenceEquals(_subscribedInputManager, currentManager))
                return;

            UnsubscribeFromInputManager();

            if (currentManager == null)
                return;

            currentManager.OnToolSlot1 += HandleToolSlot1;
            currentManager.OnToolSlot2 += HandleToolSlot2;
            currentManager.OnToolSlot3 += HandleToolSlot3;
            currentManager.OnToolSlot4 += HandleToolSlot4;
            _subscribedInputManager = currentManager;
        }

        private void UnsubscribeFromInputManager()
        {
            if (_subscribedInputManager == null)
                return;

            _subscribedInputManager.OnToolSlot1 -= HandleToolSlot1;
            _subscribedInputManager.OnToolSlot2 -= HandleToolSlot2;
            _subscribedInputManager.OnToolSlot3 -= HandleToolSlot3;
            _subscribedInputManager.OnToolSlot4 -= HandleToolSlot4;
            _subscribedInputManager = null;
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable — MAIN LOOP (вызывается каждый кадр)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Главный цикл менеджера инструментов.
        /// Порядок: Input → SwapAnimation → ToolTick → UseInput.
        /// </summary>
        public void Tick(float deltaTime)
        {
            RefreshInputSubscriptions();
            // ── 1. Обработка ввода переключения слотов ──
            ProcessSlotInput();

            // ── 2. Анимация смены инструмента ──
            ProcessSwapAnimation(deltaTime);

            // ── 3. Если инструмент активен и анимация завершена — обновляем ──
            if (_currentTool != null && _swapState == SwapState.Idle)
            {
                // ── Tick инструмента (idle-анимация, покачивание) ──
                _currentTool.ToolTick(deltaTime);

                if (InputManager.Instance != null)
                {
                    // ── Основное действие (ЛКМ) ──
                    if (InputManager.Instance.IsPrimaryActionHeld)
                    {
                        _currentTool.UsePrimary(deltaTime);
                    }

                    // ── Альтернативное действие (ПКМ) ──
                    if (InputManager.Instance.IsSecondaryActionHeld)
                    {
                        _currentTool.UseSecondary(deltaTime);
                    }
                }
            }

#if UNITY_EDITOR
            _debugCurrentSlot = _currentSlotIndex;
            _debugStateName   = _swapState.ToString();
#endif
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Программное переключение на слот по индексу (0-3).
        /// Можно вызвать из других систем (например, при потере инструмента).
        /// </summary>
        /// <param name="slotIndex">Индекс слота (0-based). -1 = убрать инструмент.</param>
        public void SwitchToSlot(int slotIndex)
        {
            if (slotIndex < -1 || slotIndex >= toolPrefabs.Length)
                return;

            RequestSwap(slotIndex);
        }

        /// <summary>
        /// Принудительно убирает текущий инструмент из рук.
        /// Запускает анимацию опускания, после чего деспавнит.
        /// </summary>
        public void Holster()
        {
            RequestSwap(-1);
        }

        /// <summary>Текущий активный инструмент (может быть null).</summary>
        public PlayerTool CurrentTool => _currentTool;

        /// <summary>Optional swim-presentation contract of the current tool.</summary>
        public PlayerToolSwimContract CurrentToolSwimContract => _currentTool != null ? _currentTool.SwimContract : null;

        /// <summary>Индекс текущего слота (-1 = нет инструмента).</summary>
        public int CurrentSlotIndex => _currentSlotIndex;

        /// <summary>Идёт ли сейчас анимация смены инструмента.</summary>
        public bool IsSwapping => _swapState != SwapState.Idle;

        public int SlotCount => toolPrefabs != null ? toolPrefabs.Length : 0;

        public string GetSlotName(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slotNameCache.Length)
                return null;

            return _slotNameCache[slotIndex];
        }

        public string GetCurrentToolOperationalSummary()
        {
            return _currentTool != null
                ? _currentTool.GetOperationalSummary()
                : "NO TOOL ARMED";
        }

        public string GetCurrentToolOperationalDirective()
        {
            if (IsSwapping)
                return "Tool swap in progress. Wait for the active handoff.";

            return _currentTool != null
                ? _currentTool.GetOperationalDirective()
                : "Arm a tool from quick slots or PDA loadout.";
        }

        public GameObject GetAssignedToolPrefab(int slotIndex)
        {
            if (toolPrefabs == null || slotIndex < 0 || slotIndex >= toolPrefabs.Length)
                return null;

            return toolPrefabs[slotIndex];
        }

        public bool SetAssignedToolPrefab(int slotIndex, GameObject prefab, bool holsterIfCurrentInvalid = true)
        {
            if (toolPrefabs == null || slotIndex < 0 || slotIndex >= toolPrefabs.Length)
                return false;

            if (ReferenceEquals(toolPrefabs[slotIndex], prefab))
                return true;

            toolPrefabs[slotIndex] = prefab;
            EnsurePoolWarmup(prefab, toolPoolWarmupCount);
            RefreshSlotNameCacheSlot(slotIndex);
            ToolAssignmentsChanged?.Invoke();

            if (!holsterIfCurrentInvalid || slotIndex != _currentSlotIndex)
                return true;

            if (prefab == null || !HasToolInInventory(prefab))
                Holster();

            return true;
        }

        public bool IsToolAvailableInSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount)
                return false;

            GameObject prefab = toolPrefabs[slotIndex];
            return prefab != null && HasToolInInventory(prefab);
        }

        public GameObject GetKnownToolPrefabForItem(ItemData item)
        {
            if (item == null || knownToolPrefabs == null)
                return null;

            for (int i = 0; i < knownToolPrefabs.Length; i++)
            {
                GameObject prefab = knownToolPrefabs[i];
                if (prefab == null)
                    continue;

                if (!prefab.TryGetComponent(out PlayerTool tool))
                    continue;

                if (ReferenceEquals(tool.ToolData, item))
                    return prefab;
            }

            return null;
        }

        public GameObject GetKnownToolPrefabForToolType<TTool>() where TTool : PlayerTool
        {
            if (knownToolPrefabs == null)
                return null;

            for (int i = 0; i < knownToolPrefabs.Length; i++)
            {
                GameObject prefab = knownToolPrefabs[i];
                if (prefab == null)
                    continue;

                if (prefab.GetComponent<TTool>() != null)
                    return prefab;
            }

            return null;
        }

        public int FindAssignedSlotForToolType<TTool>() where TTool : PlayerTool
        {
            if (toolPrefabs == null)
                return -1;

            for (int i = 0; i < toolPrefabs.Length; i++)
            {
                GameObject prefab = toolPrefabs[i];
                if (prefab == null)
                    continue;

                if (prefab.GetComponent<TTool>() != null)
                    return i;
            }

            return -1;
        }

        public bool ApplyLoadoutPreset(ToolLoadoutPreset preset, bool holsterFirst = true)
        {
            if (preset == null || toolPrefabs == null || toolPrefabs.Length == 0)
                return false;

            if (holsterFirst)
                Holster();

            int count = Mathf.Min(toolPrefabs.Length, preset.slotPrefabs != null ? preset.slotPrefabs.Length : 0);
            for (int i = 0; i < count; i++)
                SetAssignedToolPrefab(i, preset.slotPrefabs[i], holsterIfCurrentInvalid: false);

            RefreshSlotNameCache();
            ToolAssignmentsChanged?.Invoke();
            return true;
        }

        public int CopyAssignedToolPrefabs(GameObject[] buffer)
        {
            if (buffer == null || toolPrefabs == null)
                return 0;

            int count = Mathf.Min(buffer.Length, toolPrefabs.Length);
            for (int i = 0; i < count; i++)
                buffer[i] = toolPrefabs[i];

            return count;
        }

        // ProcessSlotInput and GetSlotKey removed — handled via events

        // ══════════════════════════════════════════════════════════
        //  INPUT CALLBACKS (ZERO GC)
        // ══════════════════════════════════════════════════════════

        private void ProcessSlotInput()
        {
            // Input is delivered through InputManager events now.
        }

        private void WarmRuntimePoolsIfNeeded()
        {
            WarmAssignedToolPoolsIfNeeded();
            WarmConstructionGhostPoolsIfNeeded();
        }

        private void WarmAssignedToolPoolsIfNeeded()
        {
            if (_assignedPoolsWarmed || !warmupAssignedToolPoolsOnEnable)
                return;

            if (toolPrefabs == null || toolPoolWarmupCount <= 0)
            {
                _assignedPoolsWarmed = true;
                return;
            }

            if (ObjectPoolManager.Instance == null)
                return;

            for (int i = 0; i < toolPrefabs.Length; i++)
                EnsurePoolWarmup(toolPrefabs[i], toolPoolWarmupCount);

            _assignedPoolsWarmed = true;
        }

        private void WarmConstructionGhostPoolsIfNeeded()
        {
            if (_constructionGhostPoolsWarmed || constructionGhostWarmupCount <= 0)
                return;

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            ConstructionManager constructionManager = ConstructionManager.Instance;
            ModuleCatalog catalog = constructionManager != null ? constructionManager.Catalog : null;
            if (pool == null || catalog == null || catalog.Count <= 0)
                return;

            for (int i = 0; i < catalog.Count; i++)
            {
                BuildableData buildable = catalog.GetAt(i);
                if (buildable == null || buildable.ghostPrefab == null)
                    continue;

                EnsurePoolWarmup(buildable.ghostPrefab, constructionGhostWarmupCount);
            }

            _constructionGhostPoolsWarmed = true;
        }

        private static void EnsurePoolWarmup(GameObject prefab, int minimumReserve)
        {
            if (prefab == null || minimumReserve <= 0)
                return;

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null)
                return;

            int availableCount = pool.GetAvailableCount(prefab);
            if (availableCount >= minimumReserve)
                return;

            pool.Warmup(prefab, minimumReserve - availableCount);
        }

        private void HandleToolSlot1() => HandleToolSlot(0);
        private void HandleToolSlot2() => HandleToolSlot(1);
        private void HandleToolSlot3() => HandleToolSlot(2);
        private void HandleToolSlot4() => HandleToolSlot(3);

        private void HandleToolSlot(int index)
        {
            // Do not accept input during swap animation
            if (_swapState != SwapState.Idle)
                return;

            if (index < 0 || index >= toolPrefabs.Length)
                return;

            // Toggle logic: same slot = holster
            if (_currentSlotIndex == index)
                RequestSwap(-1);
            else
                RequestSwap(index);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — SWAP LOGIC
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Запрашивает смену инструмента.
        /// Если текущий инструмент есть — начинает анимацию опускания.
        /// Если нет — сразу спавнит новый (анимация подъёма).
        /// </summary>
        private void RequestSwap(int newSlotIndex)
        {
            LogToolDebug(
                $"RequestSwap new={newSlotIndex} current={_currentSlotIndex} pending={_pendingSlotIndex} " +
                $"state={_swapState} hasCurrent={_currentTool != null}");

            // Уже на этом слоте и не holster
            if (newSlotIndex == _currentSlotIndex)
                return;

            // Проверяем наличие в инвентаре (только для валидных слотов)
            if (newSlotIndex >= 0)
            {
                GameObject prefab = toolPrefabs[newSlotIndex];
                if (prefab == null)
                {
                    LogToolDebug($"RequestSwap abort: slot {newSlotIndex} prefab null");
                    Debug.LogWarning(
                        $"[PlayerToolManager] Slot {newSlotIndex + 1}: no prefab assigned.");
                    return;
                }

                // Проверяем ItemData на префабе
                if (!HasToolInInventory(prefab))
                {
                    LogToolDebug($"RequestSwap abort: slot {newSlotIndex} missing in inventory ({prefab.name})");
                    Debug.Log(
                        $"[PlayerToolManager] Slot {newSlotIndex + 1}: " +
                        "tool not found in inventory.");
                    return;
                }
            }

            _pendingSlotIndex = newSlotIndex;

            // Если есть текущий инструмент — опускаем сначала
            if (_currentTool != null)
            {
                LogToolDebug($"RequestSwap lowering current tool {_currentTool.GetType().Name}");
                _swapState    = SwapState.Lowering;
                _swapProgress = 0f;
            }
            else
            {
                // Нет текущего — сразу спавним
                LogToolDebug("RequestSwap performing immediate swap");
                PerformSwap();
            }
        }

        /// <summary>
        /// Выполняет фактическую смену: деспавн старого → спавн нового.
        /// Вызывается после завершения анимации опускания (или сразу,
        /// если инструмента не было).
        /// </summary>
        private void PerformSwap()
        {
            LogToolDebug(
                $"PerformSwap begin pending={_pendingSlotIndex} current={_currentSlotIndex} " +
                $"currentTool={(_currentTool != null ? _currentTool.GetType().Name : "null")}");
            // ── Деспавн текущего ──
            DespawnCurrentTool();

            // ── Спавн нового ──
            if (_pendingSlotIndex >= 0 && _pendingSlotIndex < toolPrefabs.Length)
            {
                GameObject prefab = toolPrefabs[_pendingSlotIndex];

                if (prefab != null && handAnchor != null)
                {
                    LogToolDebug($"PerformSwap spawning slot={_pendingSlotIndex} prefab={prefab.name}");
                    SpawnNewTool(prefab, _pendingSlotIndex);
                }
            }

            _currentSlotIndex = _pendingSlotIndex;
            _pendingSlotIndex = -1;
            LogToolDebug(
                $"PerformSwap assigned currentSlot={_currentSlotIndex} currentTool=" +
                $"{(_currentTool != null ? _currentTool.GetType().Name : "null")}");
            ActiveSlotChanged?.Invoke(_currentSlotIndex);

            // Если спавнили новый — запускаем анимацию подъёма
            if (_currentTool != null)
            {
                LogToolDebug($"PerformSwap raising {_currentTool.GetType().Name}");
                _swapState    = SwapState.Raising;
                _swapProgress = 0f;

                // Начинаем из нижней позиции
                if (handAnchor != null)
                    handAnchor.localPosition = _anchorLoweredPosition;
            }
            else
            {
                // Holster — возвращаем anchor в нормальную позицию
                LogToolDebug("PerformSwap completed with no current tool");
                _swapState = SwapState.Idle;
                if (handAnchor != null)
                    handAnchor.localPosition = _anchorRestPosition;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — SWAP ANIMATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Обрабатывает анимацию смены инструмента (state machine).
        /// Использует Mathf.Lerp — zero GC, frame-independent.
        /// </summary>
        private void ProcessSwapAnimation(float deltaTime)
        {
            if (_swapState == SwapState.Idle)
                return;

            if (handAnchor == null)
            {
                // Нет anchor — пропускаем анимацию, выполняем мгновенно
                if (_swapState == SwapState.Lowering)
                    PerformSwap();
                else
                    _swapState = SwapState.Idle;
                return;
            }

            // Продвигаем прогресс
            _swapProgress += deltaTime * swapSpeed;

            // Clamp
            if (_swapProgress > 1f)
                _swapProgress = 1f;

            switch (_swapState)
            {
                // ── LOWERING: rest → lowered ──
                case SwapState.Lowering:
                {
                    handAnchor.localPosition = Vector3.Lerp(
                        _anchorRestPosition,
                        _anchorLoweredPosition,
                        _swapProgress);

                    if (_swapProgress >= 1f)
                    {
                        PerformSwap();
                    }

                    break;
                }

                // ── RAISING: lowered → rest ──
                case SwapState.Raising:
                {
                    handAnchor.localPosition = Vector3.Lerp(
                        _anchorLoweredPosition,
                        _anchorRestPosition,
                        _swapProgress);

                    if (_swapProgress >= 1f)
                    {
                        handAnchor.localPosition = _anchorRestPosition;
                        _swapState = SwapState.Idle;
                    }

                    break;
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — SPAWN / DESPAWN
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Спавнит инструмент из пула и настраивает его.
        /// </summary>
        private void SpawnNewTool(GameObject prefab, int slotIndex)
        {
            LogToolDebug($"SpawnNewTool begin slot={slotIndex} prefab={prefab.name}");
            EnsurePoolWarmup(prefab, toolPoolWarmupCount);
            WarmConstructionGhostPoolsIfNeeded();
            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null)
            {
                Debug.LogError("[PlayerToolManager] ObjectPoolManager.Instance is null!");
                return;
            }

            // Спавним через пул в позицию anchor
            _currentInstance = pool.Spawn(
                prefab,
                handAnchor.position,
                handAnchor.rotation);

            if (_currentInstance == null)
            {
                LogToolDebug($"SpawnNewTool failed: pool returned null for {prefab.name}");
                Debug.LogError(
                    $"[PlayerToolManager] Failed to spawn tool from slot {slotIndex + 1}.");
                return;
            }

            // Привязываем к anchor
            _currentInstance.transform.SetParent(handAnchor, false);
            _currentInstance.transform.localPosition = Vector3.zero;
            _currentInstance.transform.localRotation = Quaternion.identity;

            // Получаем компонент PlayerTool
            if (_currentInstance.TryGetComponent(out PlayerTool tool))
            {
                _currentTool = tool;
                LogToolDebug(
                    $"SpawnNewTool got instance={_currentInstance.name} tool={tool.GetType().Name} " +
                    $"toolData={(tool.ToolData != null ? tool.ToolData.name : "null")}");
                _currentTool.OnEquip();
                LogToolDebug(
                    $"SpawnNewTool after OnEquip instanceActive={_currentInstance.activeInHierarchy} " +
                    $"toolEquipped={_currentTool.IsEquipped}");
            }
            else
            {
                Debug.LogError(
                    $"[PlayerToolManager] Prefab '{prefab.name}' " +
                    "has no PlayerTool component!");
                _currentTool = null;
            }
        }

        /// <summary>
        /// Деспавнит текущий инструмент (возврат в пул).
        /// Безопасно вызывать при отсутствии инструмента.
        /// </summary>
        private void DespawnCurrentTool()
        {
            LogToolDebug(
                $"DespawnCurrentTool begin currentTool={(_currentTool != null ? _currentTool.GetType().Name : "null")} " +
                $"currentInstance={(_currentInstance != null ? _currentInstance.name : "null")} currentSlot={_currentSlotIndex}");
            if (_currentTool != null)
            {
                _currentTool.OnUnequip();
                _currentTool = null;
            }

            if (_currentInstance != null)
            {
                // Отцепляем от anchor перед деспавном
                _currentInstance.transform.SetParent(null, false);

                ObjectPoolManager pool = ObjectPoolManager.Instance;
                if (pool != null)
                {
                    pool.Despawn(_currentInstance);
                }

                _currentInstance = null;
            }

            _currentSlotIndex = -1;
            LogToolDebug("DespawnCurrentTool complete currentSlot=-1");
            ActiveSlotChanged?.Invoke(_currentSlotIndex);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — INVENTORY CHECK
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Проверяет наличие инструмента в инвентаре игрока.
        /// Сканирует InventoryGrid на предмет совпадения ItemData.
        ///
        /// Время: O(cols × rows) в worst case, но вызывается только
        /// при нажатии кнопки (не каждый кадр).
        /// </summary>
        private bool HasToolInInventory(GameObject toolPrefab)
        {
            if (playerInventory == null)
            {
                Debug.LogWarning("[PlayerToolManager] PlayerInventory reference is null!");
                return false;
            }

            // Получаем ItemData с префаба
            if (!toolPrefab.TryGetComponent(out PlayerTool prefabTool))
                return false;

            ItemData targetData = prefabTool.ToolData;
            if (targetData == null)
                return false;

            // Сканируем инвентарь
            InventoryGrid grid = playerInventory.Grid;
            if (grid == null)
                return false;

            int cols = grid.Columns;
            int rows = grid.Rows;

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    ItemData cell = grid.GetCell(x, y);

                    // Сравниваем по ссылке — ScriptableObjects уникальны
                    if (ReferenceEquals(cell, targetData))
                        return true;
                }
            }

            return false;
        }

        private void HandleInventoryChanged()
        {
            if (_currentSlotIndex < 0 || _swapState != SwapState.Idle)
                return;

            GameObject currentPrefab = GetAssignedToolPrefab(_currentSlotIndex);
            if (currentPrefab == null || HasToolInInventory(currentPrefab))
                return;

            LogToolDebug(
                $"HandleInventoryChanged holstering current slot {_currentSlotIndex} because assigned prefab missing from inventory");
            Holster();
        }

        private void RefreshSlotNameCache()
        {
            for (int i = 0; i < _slotNameCache.Length; i++)
                RefreshSlotNameCacheSlot(i);
        }

        private void RefreshSlotNameCacheSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slotNameCache.Length)
                return;

            _slotNameCache[slotIndex] = ResolveSlotName(slotIndex);
        }

        private string ResolveSlotName(int slotIndex)
        {
            if (toolPrefabs == null || slotIndex < 0 || slotIndex >= toolPrefabs.Length)
                return null;

            GameObject prefab = toolPrefabs[slotIndex];
            if (prefab == null)
                return null;

            if (prefab.TryGetComponent(out PlayerTool tool) && tool.ToolData != null && !string.IsNullOrWhiteSpace(tool.ToolData.itemName))
                return tool.ToolData.itemName;

            return prefab.name;
        }

        private void LogToolDebug(string message)
        {
            if (!toolDebugLogging)
                return;

            Debug.Log($"[ToolMgr] {message}");
        }

#if UNITY_EDITOR
        private void AutoResolveKnownToolPrefabs()
        {
            string[] prefabPaths =
            {
                "Assets/_Project/Prefabs/Tools/Held/Tool_Scanner_Held.prefab",
                "Assets/_Project/Prefabs/Tools/Held/Tool_Repair_Held.prefab",
                "Assets/_Project/Prefabs/Tools/Held/Tool_Builder_Held.prefab",
                "Assets/_Project/Prefabs/Tools/Held/Tool_LaserCutter_Held.prefab",
                "Assets/_Project/Prefabs/Tools/Held/Tool_Flashlight_Held.prefab",
                "Assets/_Project/Prefabs/Tools/Held/Tool_Propulsion_Held.prefab",
                "Assets/_Project/Prefabs/Tools/Held/Tool_SalvageSampler_Held.prefab",
                "Assets/_Project/Prefabs/Tools/Held/Tool_BeaconDeployer_Held.prefab",
                "Assets/_Project/Prefabs/Tools/Held/Tool_EnvAnalyzer_Held.prefab",
                "Assets/_Project/Prefabs/Tools/Held/Tool_Knife_Held.prefab",
                "Assets/_Project/Prefabs/Tools/Held/Tool_StunPistol_Held.prefab",
                "Assets/_Project/Prefabs/Tools/Held/Tool_HarpoonLauncher_Held.prefab"
            };

            if (knownToolPrefabs == null || knownToolPrefabs.Length != prefabPaths.Length)
                Array.Resize(ref knownToolPrefabs, prefabPaths.Length);

            for (int i = 0; i < prefabPaths.Length; i++)
            {
                if (knownToolPrefabs[i] != null)
                    continue;

                knownToolPrefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPaths[i]);
            }
        }
#endif
    }
}
