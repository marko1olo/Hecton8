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
    using Hecton.Localization;
    using Hecton8.Building;
    using Hecton8.Core;
    using Hecton8.Construction;
    using Hecton8.Inventory;
    using Hecton8.Items;
    using Hecton8.Input;
    using Hecton8.Physics;
    using Hecton8.Tools;
    using Unity.Mathematics;
    using UnityEngine;
#if UNITY_EDITOR
    using UnityEditor;
#endif

    [DisallowMultipleComponent]
    public sealed class PlayerToolManager : MonoBehaviour, ITickable, IUpdatable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── References ────────────────────────────────")]
        [Tooltip("Transform точки крепления инструмента (дочерний объект камеры).")]
        [SerializeField] private Transform handAnchor;

        [Tooltip("Ссылка на инвентарь игрока для проверки наличия инструментов.")]
        [SerializeField] private PlayerInventory playerInventory;
        [Tooltip("Optional coordinator used to suppress handheld tools while mounted transport owns the player.")]
        [SerializeField] private PlayerTransportCoordinator playerTransportCoordinator;

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
        private IInputService _subscribedInputManager;
        private readonly string[] _slotNameCache = new string[4];
        private bool _assignedPoolsWarmed;
        private bool _constructionGhostPoolsWarmed;
        private bool _handlingEquippedToolBreak;
        private bool _registeredToTick;
        private BaseModule _currentInteriorModule;
        private Rigidbody _currentInteriorCarrierBody;
        private bool _suppressInventoryChangedHandling;
        private PlayerRuntimeContext _runtimeContext;

        public event Action<int> ActiveSlotChanged;
        public event Action ToolAssignmentsChanged;

        internal Transform HandAnchor => handAnchor;
        internal PlayerInventory Inventory => playerInventory;

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
            ResolveRuntimeContextDependencies();
            if (handAnchor != null)
            {
                _anchorRestPosition    = handAnchor.localPosition;
                _anchorLoweredPosition = _anchorRestPosition + lowerOffset;
            }

            ResolveTransportCoordinator();
            RefreshSlotNameCache();
            PublishRuntimeContextState();
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
            ResolveRuntimeContextDependencies();
            TryRegisterToTickManager();
            RefreshInputSubscriptions();
            SubscribeModuleStatusEvents();
            RefreshInteriorCarrierCache(true);
            WarmRuntimePoolsIfNeeded();

            if (playerInventory != null)
                playerInventory.InventoryChanged += HandleInventoryChanged;
        }

        private void OnDisable()
        {
            TryUnregisterFromTickManager();
            UnsubscribeFromInputManager();
            UnsubscribeModuleStatusEvents();
            ClearInteriorCarrierCache();

            if (playerInventory != null)
                playerInventory.InventoryChanged -= HandleInventoryChanged;

            // Деспавним текущий инструмент при отключении менеджера
            DespawnCurrentTool();
        }

        private void TryRegisterToTickManager()
        {
            if (_registeredToTick || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Player);
            _registeredToTick = true;
        }

        private void TryUnregisterFromTickManager()
        {
            if (!_registeredToTick)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
            _registeredToTick = false;
        }

        private void RefreshInputSubscriptions()
        {
            IInputService currentManager = GlobalRegistry.Input;
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
            bool handheldToolsBlocked = IsHandheldToolUsageBlocked();
            // ── 1. Обработка ввода переключения слотов ──
            if (!handheldToolsBlocked)
                ProcessSlotInput();
            else if (_currentTool != null && _swapState == SwapState.Idle && _pendingSlotIndex < 0)
                Holster();

            // ── 2. Анимация смены инструмента ──
            ProcessSwapAnimation(deltaTime);

            if (handheldToolsBlocked)
            {
                PublishRuntimeContextState();
#if UNITY_EDITOR
                _debugCurrentSlot = _currentSlotIndex;
                _debugStateName   = _swapState.ToString();
#endif
                return;
            }

            // ── 3. Если инструмент активен и анимация завершена — обновляем ──
            if (_currentTool != null && _swapState == SwapState.Idle)
            {
                // ── Tick инструмента (idle-анимация, покачивание) ──
                _currentTool.ToolTick(deltaTime);

                IInputService inputService = GlobalRegistry.Input;
                PlayerInputState inputState = inputService != null && inputService.IsPlayerInputEnabled
                    ? inputService.GetState()
                    : default;

                if (inputState.HasAction(PlayerInputAction.PrimaryFire))
                {
                    _currentTool.UsePrimary(deltaTime);
                }

                if (inputState.HasAction(PlayerInputAction.SecondaryFire))
                {
                    _currentTool.UseSecondary(deltaTime);
                }
            }

            PublishRuntimeContextState();

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

            if (slotIndex >= 0 && IsHandheldToolUsageBlocked())
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

        /// <summary>Optional transport source of the current tool.</summary>
        public IPlayerTransportSource CurrentToolTransportSource => _currentTool as IPlayerTransportSource;

        /// <summary>Optional transport feel contract of the current tool.</summary>
        internal PlayerTransportFeelContract CurrentToolTransportFeelContract => _currentTool != null ? _currentTool.TransportFeelContract : null;

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

        public bool TryWriteCurrentToolOperationalSummary(Span<char> destination, out int length)
        {
            length = 0;
            if (destination.Length == 0)
                return false;

            if (_currentTool == null)
            {
                length = AppendLiteral(destination, 0, "NO TOOL ARMED");
                return length > 0;
            }

            if (_currentTool is ScannerTool scanner &&
                scanner.TryGetScientificScanSnapshot(out ScannerTool.ScientificScanSnapshot snapshot) &&
                snapshot.IsActive)
            {
                int cursor = 0;
                cursor = AppendLiteral(destination, cursor, "SCANNER // ");
                cursor = AppendLiteral(destination, cursor, DescribeScientificTarget(snapshot));
                cursor = AppendLiteral(destination, cursor, " // ");
                cursor = AppendInt(destination, cursor, Mathf.Clamp(Mathf.RoundToInt(snapshot.Progress01 * 100f), 0, 100));
                cursor = AppendLiteral(destination, cursor, "% // TEMP ");
                cursor = AppendInt(destination, cursor, Mathf.RoundToInt(snapshot.TemperatureC));
                cursor = AppendLiteral(destination, cursor, "C // SAL ");
                cursor = AppendInt(destination, cursor, Mathf.RoundToInt(snapshot.SalinityPpt));
                cursor = AppendLiteral(destination, cursor, " // TOX ");
                cursor = AppendInt(destination, cursor, Mathf.Clamp(Mathf.RoundToInt(snapshot.Toxicity01 * 100f), 0, 100));
                cursor = AppendLiteral(destination, cursor, "%");
                if (snapshot.HasAttractantTrace)
                {
                    cursor = AppendLiteral(destination, cursor, " // ");
                    cursor = AppendLiteral(destination, cursor, DescribeScientificAttractantChannel(snapshot.AttractantChannel));
                    cursor = AppendLiteral(destination, cursor, " VEC ");
                    cursor = AppendSignedInt(destination, cursor, Mathf.RoundToInt(snapshot.ScentDirection.x * 100f));
                    cursor = AppendLiteral(destination, cursor, ",");
                    cursor = AppendSignedInt(destination, cursor, Mathf.RoundToInt(snapshot.ScentDirection.y * 100f));
                    cursor = AppendLiteral(destination, cursor, ",");
                    cursor = AppendSignedInt(destination, cursor, Mathf.RoundToInt(snapshot.ScentDirection.z * 100f));
                }
                else if (snapshot.OrganicBlood01 > 0.1f)
                {
                    cursor = AppendLiteral(destination, cursor, " // TRACES OF ORGANIC BLOOD DETECTED");
                }
                length = cursor;
                return cursor > 0;
            }

            int toolCursor = 0;
            toolCursor = AppendUpper(destination, toolCursor, ResolveOperationalToolName(_currentTool));
            if (!_currentTool.IsEquipped)
            {
                toolCursor = AppendLiteral(destination, toolCursor, " // STANDBY");
                length = toolCursor;
                return true;
            }

            if (_currentTool.IsBroken)
            {
                toolCursor = AppendLiteral(destination, toolCursor, " // BROKEN");
                length = toolCursor;
                return true;
            }

            ToolMetadata metadata = _currentTool.Metadata;
            if (metadata != null)
            {
                toolCursor = AppendLiteral(destination, toolCursor, " // DUR ");
                toolCursor = AppendInt(destination, toolCursor, Mathf.Max(0, Mathf.RoundToInt(_currentTool.CurrentDurability)));
                toolCursor = AppendLiteral(destination, toolCursor, "/");
                toolCursor = AppendInt(destination, toolCursor, Mathf.Max(0, Mathf.RoundToInt(metadata.maxDurability)));
                length = toolCursor;
                return true;
            }

            toolCursor = AppendLiteral(destination, toolCursor, " // READY");
            length = toolCursor;
            return true;
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

        internal bool TryResolveInteriorCarrierBody(out Rigidbody carrierBody)
        {
            if (_currentInteriorModule != null && _currentInteriorModule.IsPlayerInsideInterior)
            {
                carrierBody = _currentInteriorCarrierBody;
                return carrierBody != null;
            }

            RefreshInteriorCarrierCache(true);
            carrierBody = _currentInteriorCarrierBody;
            return carrierBody != null;
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

        private static string ResolveOperationalToolName(PlayerTool tool)
        {
            if (tool == null)
                return "TOOL";

            ItemData toolData = tool.ToolData;
            if (toolData != null && !string.IsNullOrWhiteSpace(toolData.itemName))
                return toolData.itemName;

            ToolMetadata metadata = tool.Metadata;
            if (metadata != null && !string.IsNullOrWhiteSpace(metadata.toolID))
                return metadata.toolID;

            return "TOOL";
        }

        private static string DescribeScientificMaterial(ScannerTool.ScientificMaterialClass materialClass)
        {
            switch (materialClass)
            {
                case ScannerTool.ScientificMaterialClass.Basalt:
                    return "BASALT";
                case ScannerTool.ScientificMaterialClass.MetallicSilt:
                    return "METALLIC SILT";
                case ScannerTool.ScientificMaterialClass.Sediment:
                    return "SEDIMENT";
                default:
                    return "UNKNOWN";
            }
        }

        private static string DescribeScientificAttractantChannel(ScannerTool.ScientificAttractantChannel attractantChannel)
        {
            switch (attractantChannel)
            {
                case ScannerTool.ScientificAttractantChannel.Blood:
                    return "BLOOD";
                case ScannerTool.ScientificAttractantChannel.Exhaust:
                    return "EXHAUST";
                default:
                    return "TRACE";
            }
        }

        private static string DescribeScientificTarget(ScannerTool.ScientificScanSnapshot snapshot)
        {
            if (snapshot.HasFaunaContact)
                return "BIOFORM";

            return snapshot.MaterialClass != ScannerTool.ScientificMaterialClass.None
                ? DescribeScientificMaterial(snapshot.MaterialClass)
                : "WATER";
        }

        private static int AppendLiteral(Span<char> destination, int cursor, string literal)
        {
            if (string.IsNullOrEmpty(literal) || cursor >= destination.Length)
                return cursor;

            int safeLength = Mathf.Min(literal.Length, destination.Length - cursor);
            literal.AsSpan(0, safeLength).CopyTo(destination.Slice(cursor, safeLength));
            return cursor + safeLength;
        }

        private static int AppendUpper(Span<char> destination, int cursor, string value)
        {
            if (string.IsNullOrEmpty(value) || cursor >= destination.Length)
                return cursor;

            ReadOnlySpan<char> source = value.AsSpan();
            int safeLength = Mathf.Min(source.Length, destination.Length - cursor);
            Span<char> target = destination.Slice(cursor, safeLength);
            for (int i = 0; i < safeLength; i++)
                target[i] = char.ToUpperInvariant(source[i] == '_' ? ' ' : source[i]);

            return cursor + safeLength;
        }

        private static int AppendInt(Span<char> destination, int cursor, int value)
        {
            if (cursor >= destination.Length)
                return cursor;

            return value.TryFormat(destination.Slice(cursor), out int charsWritten)
                ? cursor + charsWritten
                : cursor;
        }

        private static int AppendSignedInt(Span<char> destination, int cursor, int value)
        {
            if (value >= 0)
                cursor = AppendLiteral(destination, cursor, "+");

            return AppendInt(destination, cursor, value);
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
            ConstructionManager constructionManager = Hecton8.Core.GlobalRegistry.ConstructionRuntime;
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

        private void ResolveTransportCoordinator()
        {
            if (playerTransportCoordinator == null)
                playerTransportCoordinator = _runtimeContext != null ? _runtimeContext.PlayerTransportCoordinator : null;
        }

        private void ResolveRuntimeContextDependencies()
        {
            if (!PlayerRuntimeContextService.TryBindPlayerRoot(gameObject, out PlayerRuntimeContext runtimeContext))
                return;

            _runtimeContext = runtimeContext;
            if (playerInventory == null)
                playerInventory = runtimeContext.Inventory;

            if (playerTransportCoordinator == null)
                playerTransportCoordinator = runtimeContext.PlayerTransportCoordinator;

            if (handAnchor == null)
                handAnchor = runtimeContext.HandAnchor;
        }

        private void PublishRuntimeContextState()
        {
            if (_runtimeContext == null)
                return;

            uint flags = 0u;
            if (_runtimeContext.IsBound)
                flags |= (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot;
            flags |= (uint)PlayerRuntimeSnapshotFlags.HasToolManager;
            if (playerInventory != null)
                flags |= (uint)PlayerRuntimeSnapshotFlags.HasInventory;
            if (playerTransportCoordinator != null)
                flags |= (uint)PlayerRuntimeSnapshotFlags.HasTransport;
            if (_currentTool != null)
                flags |= (uint)PlayerRuntimeSnapshotFlags.ToolEquipped;
            if (IsHandheldToolUsageBlocked())
                flags |= (uint)PlayerRuntimeSnapshotFlags.HandheldToolBlocked;

            float swapProgress01 = math.saturate(_swapProgress);
            float transportBoost01 = 0f;
            IPlayerTransportSource transportSource = CurrentToolTransportSource;
            if (transportSource != null)
                transportBoost01 = math.saturate(transportSource.GetTransportBoost01());

            PlayerInteractionRuntimeState interactionState = default;
            interactionState.ActiveToolSlot = _currentSlotIndex;
            interactionState.PendingToolSlot = _pendingSlotIndex;
            interactionState.SwapProgress01 = swapProgress01;
            interactionState.TransportBoost01 = transportBoost01;
            interactionState.Flags = flags;
            _runtimeContext.PublishInteractionState(in interactionState);
        }

        private void SubscribeModuleStatusEvents()
        {
            ModuleStatusEvents.OnModuleEnter -= HandleModuleEnter;
            ModuleStatusEvents.OnModuleExit -= HandleModuleExit;
            ModuleStatusEvents.OnModuleEnter += HandleModuleEnter;
            ModuleStatusEvents.OnModuleExit += HandleModuleExit;
        }

        private void UnsubscribeModuleStatusEvents()
        {
            ModuleStatusEvents.OnModuleEnter -= HandleModuleEnter;
            ModuleStatusEvents.OnModuleExit -= HandleModuleExit;
        }

        private void HandleModuleEnter(BaseModule module)
        {
            if (module == null)
                return;

            CacheInteriorCarrier(module);
        }

        private void HandleModuleExit(BaseModule module)
        {
            if (_currentInteriorModule == null || module == null)
                return;

            if (!ReferenceEquals(_currentInteriorModule, module))
                return;

            RefreshInteriorCarrierCache(true);
        }

        private void RefreshInteriorCarrierCache(bool allowSceneFallback)
        {
            if (_currentInteriorModule != null &&
                _currentInteriorModule.IsPlayerInsideInterior &&
                TryResolveInteriorCarrier(module: _currentInteriorModule, out Rigidbody carrierBody))
            {
                _currentInteriorCarrierBody = carrierBody;
                return;
            }

            ClearInteriorCarrierCache();
            if (!allowSceneFallback)
                return;

            // COLD SEARCH: recover current submarine interior ownership after enable/load or overlapping-module exit.
            BaseModule[] modules = UnityEngine.Object.FindObjectsByType<BaseModule>(FindObjectsInactive.Exclude);
            for (int i = 0; i < modules.Length; i++)
            {
                BaseModule module = modules[i];
                if (module == null || !module.IsPlayerInsideInterior)
                    continue;

                CacheInteriorCarrier(module);
                if (_currentInteriorCarrierBody != null)
                    return;
            }
        }

        private void CacheInteriorCarrier(BaseModule module)
        {
            _currentInteriorModule = module;
            _currentInteriorCarrierBody = null;

            if (module == null || !module.IsPlayerInsideInterior)
                return;

            TryResolveInteriorCarrier(module, out _currentInteriorCarrierBody);
        }

        private void ClearInteriorCarrierCache()
        {
            _currentInteriorModule = null;
            _currentInteriorCarrierBody = null;
        }

        private static bool TryResolveInteriorCarrier(BaseModule module, out Rigidbody carrierBody)
        {
            carrierBody = null;
            if (module == null || !module.IsPlayerInsideInterior)
                return false;

            SubmarineFluidDynamics fluidDynamics = module.GetComponentInParent<SubmarineFluidDynamics>();
            if (fluidDynamics == null)
                return false;

            return fluidDynamics.TryGetComponent(out carrierBody) && carrierBody != null;
        }

        private bool IsHandheldToolUsageBlocked()
        {
            ResolveTransportCoordinator();
            return playerTransportCoordinator != null && playerTransportCoordinator.BlocksHandheldToolUsage();
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

            if (IsHandheldToolUsageBlocked())
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
            if (newSlotIndex >= 0 && IsHandheldToolUsageBlocked())
                return;

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
                _currentTool.OnToolBroken += HandleEquippedToolBroken;
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
                _currentTool.OnToolBroken -= HandleEquippedToolBroken;
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
                    int cellHashId = playerInventory.GetItemHashAt(x, y);

                    // Сравниваем по ссылке — ScriptableObjects уникальны
                    if (cellHashId == Hecton.Localization.LocHash.Compute(targetData.PersistentId))
                        return true;
                }
            }

            return false;
        }

        private void HandleEquippedToolBroken()
        {
            if (_handlingEquippedToolBreak || _currentTool == null)
                return;

            _handlingEquippedToolBreak = true;
            try
            {
                ItemData brokenToolData = _currentTool.ToolData;
                ToolMetadata metadata = _currentTool.Metadata;
                if (brokenToolData == null || metadata == null)
                {
                    Holster();
                    return;
                }

                int toolHashId = LocHash.Compute(brokenToolData.PersistentId);
                if (toolHashId == 0)
                {
                    Holster();
                    return;
                }

                ConsumeBrokenToolInventoryEntry(toolHashId);
                PlayerSignalEvents.RaiseToolDepletedSignal(new ToolDepletedSignal(toolHashId));

                if (playerInventory != null && playerInventory.TryFindFirstAnchorByHash(toolHashId, out _))
                {
                    ToolDurabilitySystem durabilitySystem = ToolDurabilitySystem.Instance;
                    if (durabilitySystem != null)
                        durabilitySystem.ResetDurability(metadata.toolID, metadata.maxDurability);

                    ForceEquipCurrentSlotReplacement();
                    return;
                }

                Holster();
            }
            finally
            {
                _handlingEquippedToolBreak = false;
            }
        }

        private void ConsumeBrokenToolInventoryEntry(int toolHashId)
        {
            if (playerInventory == null)
                return;

            _suppressInventoryChangedHandling = true;
            try
            {
                playerInventory.TryRemoveFirstMatchingItemByHash(toolHashId);
            }
            finally
            {
                _suppressInventoryChangedHandling = false;
            }
        }

        private int TryResolveReplacementSlotForBrokenCurrentTool()
        {
            if (_currentTool == null)
                return -1;

            ItemData brokenToolData = _currentTool.ToolData;
            if (brokenToolData == null)
                return -1;

            for (int slotIndex = 0; slotIndex < SlotCount; slotIndex++)
            {
                if (slotIndex == _currentSlotIndex)
                    continue;

                GameObject assignedPrefab = GetAssignedToolPrefab(slotIndex);
                if (!IsCompatibleReplacementPrefab(assignedPrefab, brokenToolData))
                    continue;

                if (!HasToolInInventory(assignedPrefab) || IsPrefabBroken(assignedPrefab))
                    continue;

                return slotIndex;
            }

            if (knownToolPrefabs == null)
                return -1;

            for (int prefabIndex = 0; prefabIndex < knownToolPrefabs.Length; prefabIndex++)
            {
                GameObject candidatePrefab = knownToolPrefabs[prefabIndex];
                if (!IsCompatibleReplacementPrefab(candidatePrefab, brokenToolData))
                    continue;

                if (!HasToolInInventory(candidatePrefab) || IsPrefabBroken(candidatePrefab))
                    continue;

                int assignedSlot = FindAssignedSlotForPrefab(candidatePrefab);
                if (assignedSlot >= 0)
                    return assignedSlot;

                if (_currentSlotIndex >= 0)
                {
                    SetAssignedToolPrefab(_currentSlotIndex, candidatePrefab, holsterIfCurrentInvalid: false);
                    return _currentSlotIndex;
                }

                int emptySlot = FindFirstEmptyAssignedSlot();
                if (emptySlot >= 0)
                {
                    SetAssignedToolPrefab(emptySlot, candidatePrefab, holsterIfCurrentInvalid: false);
                    return emptySlot;
                }
            }

            return -1;
        }

        private bool IsCompatibleReplacementPrefab(GameObject candidatePrefab, ItemData brokenToolData)
        {
            if (candidatePrefab == null || brokenToolData == null)
                return false;

            if (!candidatePrefab.TryGetComponent(out PlayerTool candidateTool))
                return false;

            return ReferenceEquals(candidateTool.ToolData, brokenToolData);
        }

        private static bool IsPrefabBroken(GameObject prefab)
        {
            if (prefab == null || !prefab.TryGetComponent(out PlayerTool tool) || tool.Metadata == null)
                return false;

            ToolDurabilitySystem durabilitySystem = ToolDurabilitySystem.Instance;
            return durabilitySystem != null && durabilitySystem.IsBroken(tool.Metadata.toolID);
        }

        private int FindAssignedSlotForPrefab(GameObject prefab)
        {
            if (prefab == null || toolPrefabs == null)
                return -1;

            for (int slotIndex = 0; slotIndex < toolPrefabs.Length; slotIndex++)
            {
                if (ReferenceEquals(toolPrefabs[slotIndex], prefab))
                    return slotIndex;
            }

            return -1;
        }

        private int FindFirstEmptyAssignedSlot()
        {
            if (toolPrefabs == null)
                return -1;

            for (int slotIndex = 0; slotIndex < toolPrefabs.Length; slotIndex++)
            {
                if (toolPrefabs[slotIndex] == null)
                    return slotIndex;
            }

            return -1;
        }

        private void ForceEquipCurrentSlotReplacement()
        {
            int slotIndex = _currentSlotIndex;
            if (slotIndex < 0 || slotIndex >= SlotCount)
            {
                Holster();
                return;
            }

            GameObject replacementPrefab = GetAssignedToolPrefab(slotIndex);
            if (replacementPrefab == null)
            {
                Holster();
                return;
            }

            DespawnCurrentTool();
            SpawnNewTool(replacementPrefab, slotIndex);
            _currentSlotIndex = slotIndex;
            _pendingSlotIndex = -1;
            _swapState = SwapState.Idle;
            if (handAnchor != null)
                handAnchor.localPosition = _anchorRestPosition;

            ActiveSlotChanged?.Invoke(_currentSlotIndex);
        }

        private void HandleInventoryChanged()
        {
            if (_suppressInventoryChangedHandling || _handlingEquippedToolBreak)
                return;

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
