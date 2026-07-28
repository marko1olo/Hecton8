// ============================================================================
// HECTON-8 — PlayerToolManager.cs
// Kontroller pereklyucheniya instrumentov v rukah igroka.
//
// Otvetstvennosti:
//   1. Slushaet vvod (knopki 1-4) cherez ITickable.Tick().
//   2. Proveryaet nalichie instrumenta v PlayerInventory.
//   3. Spavnit/despavnit instrumenty cherez ObjectPoolManager.
//   4. Upravlyaet plavnoy animatsiey smeny (lower → raise).
//   5. Delegiruet UsePrimary/UseSecondary tekuschemu instrumentu.
//
// ZERO GC:
//   • Keshirovannye KeyCode[] — net allokatsiy pri proverke vvoda.
//   • Spawn/Despawn cherez pul — nikakih Instantiate/Destroy.
//   • Nikakih strokovyh operatsiy v goryachih putyah.
//   • math.lerp dlya animatsii — zero GC.
//
// ZAVISIMOSTI:
//   • GameTickManager (registratsiya ITickable)
//   • ObjectPoolManager (spavn/despavn instrumentov)
//   • PlayerInventory (proverka nalichiya instrumenta)
//   • PlayerTool (bazovyy klass instrumentov)
// ============================================================================

namespace Hecton8.Gameplay
{
    using System;
    using Hecton.Localization;
    using Hecton8.Core;
    using Hecton8.Inventory;
    using Hecton8.Items;
    using Hecton8.Interaction;
    using Hecton8.Core.Contracts;
    using Hecton8.Core.Contracts.Signals;
    using Hecton8.Tools;
    using Hecton8.World;
    using Unity.Mathematics;
    using UnityEngine;
    using UnityEngine.Serialization;
#if UNITY_EDITOR
    using UnityEditor;
#endif

    [DisallowMultipleComponent]
    public sealed class PlayerToolManager : MonoBehaviour, ITickable, IUpdatable, ILateFrameTickable, IModuleStatusEventListener, IGlobalRegistryHotSwapListener
    {
        private static int s_x001PlayerToolManagerSignalPushDropCount;
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── References ────────────────────────────────")]
        [Tooltip("Transform tochki krepleniya instrumenta (docherniy obekt kamery).")]
        [SerializeField] private Transform handAnchor;

        [Tooltip("Ssylka na inventar igroka dlya proverki nalichiya instrumentov.")]
        [SerializeField] private PlayerInventory playerInventory;
        [Tooltip("Optional coordinator used to suppress handheld tools while mounted transport owns the player.")]
        [SerializeField] private PlayerTransportCoordinator playerTransportCoordinator;
        [Tooltip("Forward probe distance for zero-GC field loadout advice published before UI late-frame presentation.")]
        [SerializeField] private float fieldLoadoutAdviceRange = 18f;
        [SerializeField] private LayerMask fieldLoadoutAdviceMask = Hecton8.Core.HectonLayerMasks.FieldToolScanLayerMask;

        [Header("── Tool Prefabs (sloty 1-4) ──────────────────")]
        [Tooltip("Prefaby instrumentov, privyazannye k knopkam 1-4. " +
                 "Pustye sloty — ostavit null.")]
        [SerializeField] private GameObject[] toolPrefabs = new GameObject[4];

        [Header("── Known Tool Prefabs ────────────────────────")]
        [Tooltip("Polnyy reestr held-tool prefab'ov dlya PDA / quick-slot assignment.")]
        [SerializeField] private GameObject[] knownToolPrefabs = new GameObject[13];

        [Header("Production Starter Loadout")]
        [Tooltip("When playing, grant missing inventory items for authored quick-slot prefabs once. Dev provisioner remains editor/development-only.")]
        [SerializeField] private bool grantAssignedToolItemsOnRuntimeStart = true;
        [Tooltip("Maximum authored quick-slot tool items that runtime start may add to the inventory.")]
        [SerializeField, Min(0)] private int runtimeStartToolGrantBudget = 4;

        [Header("â”€â”€ Pool Warmup â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("ÐŸÑ€Ð¾Ð³Ñ€ÐµÐ²Ð°ÐµÑ‚ assigned held-tool pools Ð¿Ñ€Ð¸ Ð²ÐºÐ»ÑŽÑ‡ÐµÐ½Ð¸Ð¸ Ð¼ÐµÐ½ÐµÐ´Ð¶ÐµÑ€Ð°, Ñ‡Ñ‚Ð¾Ð±Ñ‹ ÑƒÐ±Ñ€Ð°Ñ‚ÑŒ runtime Instantiate Ð¿Ñ€Ð¸ Ð¿ÐµÑ€Ð²Ð¾Ð¼ ÑÐºÐ¸Ð¿Ðµ.")]
        [SerializeField] private bool warmupAssignedToolPoolsOnEnable = true;
        [Tooltip("ÐœÐ¸Ð½Ð¸Ð¼Ð°Ð»ÑŒÐ½Ñ‹Ð¹ Ñ€ÐµÐ·ÐµÑ€Ð² ÑÐºÐ·ÐµÐ¼Ð¿Ð»ÑÑ€Ð¾Ð² Ð² pool Ð´Ð»Ñ ÐºÐ°Ð¶Ð´Ð¾Ð³Ð¾ assigned held-tool prefab.")]
        [FormerlySerializedAs("constructionGhostWarmupCount")]
        [SerializeField] private int toolPoolWarmupCount = 1;
        [Header("── Swap Animation ────────────────────────────")]
        [Tooltip("Skorost animatsii smeny instrumenta (lerp factor per second). " +
                 "Bolshe = bystree.")]
        [SerializeField] private float swapSpeed = 8f;

        [Tooltip("Smeschenie instrumenta vniz pri animatsii smeny (lokalnye koordinaty).")]
        [SerializeField] private Vector3 lowerOffset = new Vector3(0f, -0.5f, 0f);

        [Header("── Diagnostics ───────────────────────────────")]
        [SerializeField] private int _debugCurrentSlot = -1;
        [SerializeField] private string _debugStateName;
        [SerializeField] private bool toolDebugLogging;
        [SerializeField] private int _debugRuntimeStartToolGrants;

        // SlotKeys removed — handled by InputManager events

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>Tekuschiy aktivnyy ekzemplyar instrumenta (iz pula).</summary>
        private GameObject _currentInstance;
        private IObjectPoolService _currentInstancePool;

        /// <summary>Komponent PlayerTool na tekuschem ekzemplyare.</summary>
        private PlayerTool _currentTool;
        private uint _currentActiveToolHash;
        private uint _currentActiveToolMetadataHash;
        // COLD ALLOC: char[512] — zero-GC active tool HUD summary staging buffer — owner: PlayerToolManager
        private FixedCharBuffer _toolSummaryBuffer = new FixedCharBuffer(512);
        // COLD ALLOC: char[512] - zero-GC active tool HUD directive staging buffer - owner: PlayerToolManager
        private FixedCharBuffer _toolDirectiveBuffer = new FixedCharBuffer(512);

        /// <summary>Indeks tekuschego aktivnogo slota (-1 = nichego).</summary>
        private int _currentSlotIndex = -1;

        /// <summary>Indeks slota, na kotoryy pereklyuchaemsya (-1 = net zaprosa).</summary>
        private int _pendingSlotIndex = -1;

        /// <summary>Tekuschee sostoyanie konechnogo avtomata smeny instrumenta.</summary>
        private SwapState _swapState = SwapState.Idle;

        /// <summary>Progress animatsii [0..1]. 0 = nachalo, 1 = zaversheno.</summary>
        private float _swapProgress;

        /// <summary>
        /// Nachalnaya lokalnaya pozitsiya handAnchor.
        /// Zapominaem pri Awake — eto «normalnoe» polozhenie instrumenta.
        /// </summary>
        private Vector3 _anchorRestPosition;

        /// <summary>Tselevaya pozitsiya pri opuskanii (rest + offset).</summary>
        private Vector3 _anchorLoweredPosition;
        private uint _lastPlayerInputSignalSequence;
        private const uint PlayerInputSignalSourceHash = 0x504C494Eu;
        private bool _assignedPoolsWarmed;
        private bool _runtimeStartToolGrantCompleted;
        private bool _handlingEquippedToolBreak;
        private bool _registeredToTick;
        private bool _registeredToLateFrame;
        private bool _pendingSwapExecution;
        private bool _pendingCurrentToolDespawn;
        private bool _flushingToolLifecyclePresentation;
        private bool _pendingToolSpawnExecution;
        private GameObject _pendingToolSpawnPrefab;
        private int _pendingToolSpawnSlotIndex;
        private bool _pendingToolPoolDespawn;
        private GameObject _pendingToolPoolDespawnInstance;
        private IObjectPoolService _pendingToolPoolDespawnOwner;
        private Transform _pendingToolPoseTransform;
        private PhysicalToolGripOffsets _pendingToolGripOffsets;
        private bool _pendingToolPoseFlush;
        private Vector3 _pendingHandAnchorLocalPosition;
        private bool _hasPendingHandAnchorLocalPosition;
        private ulong _currentInteriorModuleEntityId;
        private bool _isInsideModuleInterior;
        private Rigidbody _currentInteriorCarrierBody;
        private bool _suppressInventoryChangedHandling;
        private bool _suppressToolLoadoutSignal;
        private uint _toolLoadoutSignalSourceId;
        private uint _toolLoadoutSignalSequence;
        private uint _inventorySignalHash;
        private uint _lastInventorySignalRevision;
        // COLD ALLOC: PlayerTool[toolPrefabs.Length] - cached assigned prefab tool components - owner: PlayerToolManager
        private PlayerTool[] _assignedToolPrefabComponents = Array.Empty<PlayerTool>();
        // COLD ALLOC: PlayerTool[knownToolPrefabs.Length] - cached known prefab tool components - owner: PlayerToolManager
        private PlayerTool[] _knownToolPrefabComponents = Array.Empty<PlayerTool>();
        private PlayerRuntimeContext _runtimeContext;
        private IPlayerRuntimeContext _playerRuntimeService;
        private PlayerInventory _boundInventorySignalSource;
        private PlayerTool _externallyDockedTool;
        private PlayerTool _batterySiphonTool;
        private IBatteryTool _batterySiphonBatteryTool;
        private int _batterySiphonSlotIndex = -1;
        private int _batterySiphonItemHashId;
        private float _batterySiphonRemainingSeconds;
        private float _batterySiphonDurationSeconds;
        private IInputService _inputService;
        private IPhysicsService _physicsService;
        private IPlayerMovementForceSink _playerMovementForceSink;
        private IObjectPoolService _objectPool;
        private IPersistentDroppedItemRegistry _persistentWorldRegistry;
        private IToolDurabilityService _toolDurability;
        private ISubmarineRuntimeContext _submarineRuntimeContext;
        private bool _hotSwapListenerRegistered;
        private FieldLoadoutAdvisor.ForwardLoadoutSnapshot _cachedFieldLoadoutAdvice;
        private bool _cachedFieldLoadoutAdviceValid;
        private float _nextFieldLoadoutAdviceRefreshAt = float.NegativeInfinity;

        private const float BatterySiphonLockoutSeconds = 1.5f;
        private const float BatteryDeadThreshold01 = 0.0001f;
        private const float FieldLoadoutAdviceRefreshInterval = 0.35f;
        private const string StandardBatteryPersistentId = "Comp_BatteryCell";
        private const string HighCapacityBatteryPersistentId = "Comp_HighCapacityCell";
        private static readonly int _standardBatteryHashId = LocHash.Compute(StandardBatteryPersistentId);
        private static readonly int _highCapacityBatteryHashId = LocHash.Compute(HighCapacityBatteryPersistentId);
        internal Transform HandAnchor => handAnchor;
        internal PlayerInventory Inventory => playerInventory;
        internal IPlayerRuntimeContext PlayerRuntimeContext => _playerRuntimeService;

        // ══════════════════════════════════════════════════════════
        //  SWAP STATE MACHINE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Konechnyy avtomat animatsii smeny instrumenta.
        ///
        /// Idle → Lowering → Raising → Idle
        ///
        /// Lowering: instrument plavno uhodit vniz. Po zavershenii —
        ///           despavn starogo, spavn novogo.
        /// Raising:  novyy instrument plavno podnimaetsya v rabochuyu pozitsiyu.
        /// </summary>
        private enum SwapState
        {
            /// <summary>Instrument na meste, animatsiya ne idet.</summary>
            Idle,

            /// <summary>Opuskaem tekuschiy instrument vniz pered smenoy.</summary>
            Lowering,

            /// <summary>Podnimaem novyy instrument vverh posle spavna.</summary>
            Raising
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            ResolveRuntimeContextDependencies();
            RefreshToolPrefabComponentCachesCold();
            if (handAnchor != null)
            {
                _anchorRestPosition    = handAnchor.localPosition;
                _anchorLoweredPosition = _anchorRestPosition + lowerOffset;
            }

            ResolveTransportCoordinator();
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
            RefreshToolPrefabComponentCachesCold();
            CacheRegistryServicesCold(forceRefresh: true);
            ToolHitUtility.CachePlayerToolManagerCold(this);
            ToolHitUtility.CachePhysicsServiceCold(_physicsService);
            ToolHitUtility.CachePlayerMovementForceSinkCold(_playerMovementForceSink);
            TryRegisterHotSwapListener();
            BaselineToolSlotInputSignalSequence();
            TryRegisterToTickManager();
            SubscribeModuleStatusEvents();
            ClearInteriorCarrierCache();
            // The starter-tool grant runs BEFORE the pool warmup on purpose. WarmRuntimePoolsIfNeeded
            // instantiates the authored slot prefabs, so an authoring defect in any one of them raises a
            // managed exception out of Instantiate and takes the remainder of OnEnable with it. Measured on
            // the headless route: slot 0 (Tool_Scanner_Held) carries DataArchaeologyRuntime, whose Awake
            // asserts "requires an authored reconstruction mesh", and that throw landed on the warmup call
            // that used to sit directly above this line. Warmup is a pure pre-allocation optimisation, so it
            // goes last - one unauthored prefab must not be able to deny the player the entire starter
            // loadout, which is exactly what IsToolAvailableInSlot reports on.
            TryGrantAssignedToolItemsOnRuntimeStart();
            BaselineInventoryChangedSignalRevision();
            WarmRuntimePoolsIfNeeded();
        }

        private void OnDisable()
        {
            ToolHitUtility.ClearPlayerToolManagerCold(this);
            ToolHitUtility.ClearPhysicsServiceCold(_physicsService);
            ToolHitUtility.ClearPlayerMovementForceSinkCold(_playerMovementForceSink);
            TryUnregisterFromTickManager();
            UnsubscribeModuleStatusEvents();
            ClearInteriorCarrierCache();
            TryUnregisterHotSwapListener();
            ClearCachedFieldLoadoutAdvice();

            // Despavnim tekuschiy instrument pri otklyuchenii menedzhera
            _flushingToolLifecyclePresentation = true;
            DespawnCurrentToolImmediate();
            _flushingToolLifecyclePresentation = false;
        }

        private void TryRegisterToTickManager()
        {
            if ((_registeredToTick && _registeredToLateFrame) || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredToTick)
            {
                _registeredToTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);
            }

            if (!_registeredToLateFrame)
                _registeredToLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterFromTickManager(bool clearPendingPresentation = true)
        {
            if (!_registeredToTick && !_registeredToLateFrame)
                return;

            if (_registeredToTick)
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
            if (_registeredToLateFrame)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);

            _registeredToTick = false;
            _registeredToLateFrame = false;
            if (clearPendingPresentation)
            {
                _pendingToolPoseFlush = false;
                _pendingToolPoseTransform = null;
                _pendingToolGripOffsets = null;
                _hasPendingHandAnchorLocalPosition = false;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable — MAIN LOOP (vyzyvaetsya kazhdyy kadr)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Glavnyy tsikl menedzhera instrumentov.
        /// Poryadok: Input → SwapAnimation → ToolTick → UseInput.
        /// </summary>
        public void Tick(float deltaTime)
        {
            ConsumeToolSlotInputSignals();
            if (ConsumeInventoryChangedSignals())
                HandleInventoryChanged();
            ConsumeEquippedToolDurabilitySignals();
            if (_currentTool != null)
                _currentTool.AdvanceRuntimeActiveIntent(deltaTime);

            if (_externallyDockedTool != null)
            {
                if (!ReferenceEquals(_externallyDockedTool, _currentTool))
                    _externallyDockedTool = null;
                else
                {
                    PublishRuntimeContextState();
#if UNITY_EDITOR
                    _debugCurrentSlot = _currentSlotIndex;
                    _debugStateName   = GetSwapStateDebugName(_swapState);
#endif
                    return;
                }
            }

            bool handheldToolsBlocked = IsHandheldToolUsageBlocked();
            bool batterySiphonLockout = IsBatterySiphonLockoutActive;
            // ── 1. Obrabotka vvoda pereklyucheniya slotov ──
            if (!handheldToolsBlocked && !batterySiphonLockout)
                ProcessSlotInput();
            else if (handheldToolsBlocked && _currentTool != null && _swapState == SwapState.Idle && _pendingSlotIndex < 0)
                Holster();

            // ── 2. Animatsiya smeny instrumenta ──
            ProcessSwapAnimation(deltaTime);

            if (IsBatterySiphonLockoutActive)
            {
                ProcessBatterySiphonLockout(deltaTime);
                PublishRuntimeContextState();
#if UNITY_EDITOR
                _debugCurrentSlot = _currentSlotIndex;
                _debugStateName   = GetSwapStateDebugName(_swapState);
#endif
                return;
            }

            if (handheldToolsBlocked)
            {
                PublishRuntimeContextState();
#if UNITY_EDITOR
                _debugCurrentSlot = _currentSlotIndex;
                _debugStateName   = GetSwapStateDebugName(_swapState);
#endif
                return;
            }

            // ── 3. Esli instrument aktiven i animatsiya zavershena — obnovlyaem ──
            if (_currentTool != null && _swapState == SwapState.Idle)
            {
                // ── Tick instrumenta (idle-animatsiya, pokachivanie) ──
                if (TryBeginBatterySiphonLockoutIfNeeded())
                {
                    PublishRuntimeContextState();
#if UNITY_EDITOR
                    _debugCurrentSlot = _currentSlotIndex;
                    _debugStateName   = GetSwapStateDebugName(_swapState);
#endif
                    return;
                }

                _currentTool.ToolTick(deltaTime);

                IInputService inputService = _inputService;
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
            _debugStateName   = GetSwapStateDebugName(_swapState);
#endif
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        public void LateFrameTick()
        {
            _flushingToolLifecyclePresentation = true;
            FlushPendingCurrentToolDespawn();
            FlushPendingSwapExecution();
            FlushPendingToolSpawnExecution();
            FlushPendingToolPoolDespawn();
            _flushingToolLifecyclePresentation = false;
            FlushPendingHandAnchorPose();
            FlushPendingToolPose();
            RefreshFieldLoadoutAdviceForVisualSync();
        }

        /// <summary>
        /// Programmnoe pereklyuchenie na slot po indeksu (0-3).
        /// Mozhno vyzvat iz drugih sistem (naprimer, pri potere instrumenta).
        /// </summary>
        /// <param name="slotIndex">Indeks slota (0-based). -1 = ubrat instrument.</param>
        public void SwitchToSlot(int slotIndex)
        {
            if (_externallyDockedTool != null)
                return;

            if (slotIndex < -1 || slotIndex >= toolPrefabs.Length)
                return;

            if (slotIndex >= 0 && (IsHandheldToolUsageBlocked() || IsBatterySiphonLockoutActive))
                return;

            RequestSwap(slotIndex);
        }

        /// <summary>
        /// Prinuditelno ubiraet tekuschiy instrument iz ruk.
        /// Zapuskaet animatsiyu opuskaniya, posle chego despavnit.
        /// </summary>
        public void Holster()
        {
            if (_externallyDockedTool != null)
                return;

            RequestSwap(-1);
        }

        /// <summary>Tekuschiy aktivnyy instrument (mozhet byt null).</summary>
        public PlayerTool CurrentTool => _currentTool;

        public uint CurrentActiveToolHash => _currentActiveToolHash;

        public bool TryBeginExternalToolDock(PlayerTool tool)
        {
            if (tool == null ||
                _currentTool == null ||
                !ReferenceEquals(tool, _currentTool) ||
                _swapState != SwapState.Idle ||
                _externallyDockedTool != null)
            {
                return false;
            }

            _externallyDockedTool = tool;
            PublishRuntimeContextState();
            return true;
        }

        public void EndExternalToolDock(PlayerTool tool)
        {
            if (tool == null || !ReferenceEquals(tool, _externallyDockedTool))
                return;

            _externallyDockedTool = null;
            PublishRuntimeContextState();
        }

        public bool TryForceDropCurrentToolFromHands(Vector3 inheritedVelocityChange)
        {
            if (_externallyDockedTool != null)
                return false;

            if (_currentTool == null)
                return false;

            ItemData toolData = _currentTool.ToolData;
            if (toolData == null)
            {
                DespawnCurrentTool();
                return true;
            }

            int toolHashId = ItemData.ResolvePersistentHashId(toolData);
            if (toolHashId == 0)
            {
                DespawnCurrentTool();
                return true;
            }

            IPersistentDroppedItemRegistry worldRegistry = _persistentWorldRegistry;
            if (worldRegistry == null || playerInventory == null)
            {
                DespawnCurrentTool();
                return true;
            }

            bool removedFromInventory = false;
            _suppressInventoryChangedHandling = true;
            try
            {
                removedFromInventory = playerInventory.TryRemoveFirstMatchingItemByHash(toolHashId);
            }
            finally
            {
                _suppressInventoryChangedHandling = false;
                BaselineInventoryChangedSignalRevision();
            }

            if (!removedFromInventory)
            {
                DespawnCurrentTool();
                return true;
            }

            Vector3 dropPosition = handAnchor != null
                ? handAnchor.position
                : transform.position + transform.forward * 0.65f;
            if (worldRegistry.TryRegisterDroppedItem(toolData, 1, dropPosition, inheritedVelocityChange))
            {
                DespawnCurrentTool();
                return true;
            }

            _suppressInventoryChangedHandling = true;
            try
            {
                playerInventory.TryAddItem(toolHashId, 1);
            }
            finally
            {
                _suppressInventoryChangedHandling = false;
                BaselineInventoryChangedSignalRevision();
            }

            DespawnCurrentToolImmediate();
            return true;
        }

        /// <summary>Optional swim-presentation contract of the current tool.</summary>
        public PlayerToolSwimContract CurrentToolSwimContract => _currentTool != null ? _currentTool.SwimContract : null;

        /// <summary>Optional transport source of the current tool.</summary>
        public IPlayerTransportSource CurrentToolTransportSource => _currentTool as IPlayerTransportSource;

        /// <summary>Optional transport feel contract of the current tool.</summary>
        internal PlayerTransportFeelContract CurrentToolTransportFeelContract => _currentTool != null ? _currentTool.TransportFeelContract : null;

        /// <summary>Indeks tekuschego slota (-1 = net instrumenta).</summary>
        public int CurrentSlotIndex => _currentSlotIndex;

        /// <summary>Idet li seychas animatsiya smeny instrumenta.</summary>
        public bool IsSwapping => _swapState != SwapState.Idle;

        public bool IsBatterySiphonLockoutActive => _batterySiphonRemainingSeconds > 0f;

        public float BatterySiphonProgress01 => ResolveBatterySiphonProgress01();

        public int SlotCount => toolPrefabs != null ? toolPrefabs.Length : 0;

        public string GetSlotName(int slotIndex)
        {
            return ResolveSlotName(slotIndex);
        }

        public bool TryWriteSlotName(int slotIndex, Span<char> destination, out int length)
        {
            length = 0;
            if (destination.Length == 0)
                return false;

            ReadOnlySpan<char> source = ResolveSlotNameSpan(slotIndex);
            if (source.Length == 0)
                return false;

            length = Mathf.Min(source.Length, destination.Length);
            source.Slice(0, length).CopyTo(destination);
            return length > 0;
        }

        public string GetCurrentToolOperationalSummary()
        {
            if (IsBatterySiphonLockoutActive)
                return "CELL SWAP // LOCKOUT";

            if (_currentTool == null)
                return "NO TOOL ARMED";

            return _currentTool.BuildLegacyOperationalSummaryString();
        }

        public bool TryWriteCurrentToolOperationalSummary(Span<char> destination, out int length)
        {
            length = 0;
            if (destination.Length == 0)
                return false;

            if (IsBatterySiphonLockoutActive)
            {
                int cursor = 0;
                cursor = AppendLiteral(destination, cursor, "CELL SWAP // ");
                cursor = AppendInt(destination, cursor, math.clamp(Mathf.RoundToInt(ResolveBatterySiphonProgress01() * 100f), 0, 100));
                cursor = AppendLiteral(destination, cursor, "%");
                length = cursor;
                return cursor > 0;
            }

            if (_currentTool == null)
            {
                length = AppendLiteral(destination, 0, "NO TOOL ARMED");
                return length > 0;
            }

            _toolSummaryBuffer.Clear();
            _currentTool.WriteOperationalSummary(ref _toolSummaryBuffer);
            ReadOnlySpan<char> summary = _toolSummaryBuffer.AsSpan();
            int copyLength = Mathf.Min(summary.Length, destination.Length);
            if (copyLength <= 0)
                return false;

            summary.Slice(0, copyLength).CopyTo(destination);
            length = copyLength;
            return true;
        }

        public string GetCurrentToolOperationalDirective()
        {
            if (IsBatterySiphonLockoutActive)
                return "Battery auto-swap in progress. Tool interaction locked.";

            if (IsSwapping)
                return "Tool swap in progress. Wait for the active handoff.";

            if (_currentTool == null)
                return "Arm a tool from quick slots or PDA loadout.";

            return _currentTool.BuildLegacyOperationalDirectiveString();
        }

        public bool TryWriteCurrentToolOperationalDirective(Span<char> destination, out int length)
        {
            length = 0;
            if (destination.Length == 0)
                return false;

            if (IsBatterySiphonLockoutActive)
            {
                length = AppendLiteral(destination, 0, "Battery auto-swap in progress. Tool interaction locked.");
                return length > 0;
            }

            if (IsSwapping)
            {
                length = AppendLiteral(destination, 0, "Tool swap in progress. Wait for the active handoff.");
                return length > 0;
            }

            if (_currentTool == null)
            {
                length = AppendLiteral(destination, 0, "Arm a tool from quick slots or PDA loadout.");
                return length > 0;
            }

            _toolDirectiveBuffer.Clear();
            _currentTool.WriteOperationalDirective(ref _toolDirectiveBuffer);
            ReadOnlySpan<char> directive = _toolDirectiveBuffer.AsSpan();
            int copyLength = Mathf.Min(directive.Length, destination.Length);
            if (copyLength <= 0)
                return false;

            directive.Slice(0, copyLength).CopyTo(destination);
            length = copyLength;
            return true;
        }

        public bool TryGetCachedFieldLoadoutPresetName(out string presetName)
        {
            presetName = null;
            return _cachedFieldLoadoutAdviceValid &&
                   FieldLoadoutAdvisor.TryGetPresetName(_cachedFieldLoadoutAdvice.PresetId, out presetName);
        }

        public bool TryGetCachedFieldLoadoutAdvice(out FieldLoadoutAdvisor.LoadoutAdvice advice)
        {
            advice = default;
            if (!_cachedFieldLoadoutAdviceValid)
                return false;

            if (!FieldLoadoutAdvisor.TryGetPresetName(_cachedFieldLoadoutAdvice.PresetId, out string presetName) ||
                !FieldLoadoutAdvisor.TryGetPresetSummary(_cachedFieldLoadoutAdvice.PresetId, out string summary))
            {
                return false;
            }

            advice = new FieldLoadoutAdvisor.LoadoutAdvice(presetName, summary);
            return true;
        }

        public GameObject GetAssignedToolPrefab(int slotIndex)
        {
            if (toolPrefabs == null || slotIndex < 0 || slotIndex >= toolPrefabs.Length)
                return null;

            return toolPrefabs[slotIndex];
        }

        public bool TryGetAssignedToolDataReadModel(int slotIndex, out IPlayerToolDataReadModel tool)
        {
            tool = GetAssignedToolPrefabComponent(slotIndex);
            return tool != null;
        }

        public bool TryGetToolDataReadModelForPrefab(GameObject prefab, out IPlayerToolDataReadModel tool)
        {
            tool = null;
            if (!TryGetCachedToolForPrefab(prefab, out PlayerTool cachedTool))
                return false;

            tool = cachedTool;
            return tool != null;
        }

        private void PublishToolLoadoutChanged(byte reason)
        {
            if (_suppressToolLoadoutSignal || !Application.isPlaying)
                return;

            uint sourceId = ResolveToolLoadoutSignalSourceId();
            if (sourceId == 0u)
                return;

            uint nextSequence = unchecked(_toolLoadoutSignalSequence + 1u);
            if (nextSequence == 0u)
                nextSequence = 1u;

            _toolLoadoutSignalSequence = nextSequence;
            ToolLoadoutChangedSignal signal = new ToolLoadoutChangedSignal
            {
                SourceId = sourceId,
                Sequence = nextSequence,
                Frame = ResolveCurrentToolLoadoutFrame(),
                ActiveToolHash = _currentActiveToolHash,
                AssignedSlotMask = ComputeAssignedSlotMask(),
                ActiveSlot = ResolveActiveSlotSignalValue(),
                SlotCount = ResolveSlotCountSignalValue(),
                Reason = reason,
                Flags = ResolveToolLoadoutFlags()
            };

            SignalBus<ToolLoadoutChangedSignal>.TryPushTracked(in signal, ref s_x001PlayerToolManagerSignalPushDropCount);
        }

        private static uint ResolveCurrentToolLoadoutFrame()
        {
            uint frame = TimeSliceScheduler.CurrentFrameId;
            return frame != 0u ? frame : 1u;
        }

        private uint ResolveToolLoadoutSignalSourceId()
        {
            if (_toolLoadoutSignalSourceId == 0u && gameObject != null)
                _toolLoadoutSignalSourceId = RuntimeOriginRoute.FoldEntityIdToSourceId(EntityId.ToULong(gameObject.GetEntityId()));

            return _toolLoadoutSignalSourceId;
        }

        private static uint ResolveActiveToolHash(PlayerTool tool)
        {
            if (tool == null || tool.ToolData == null)
                return 0u;

            uint itemHash = unchecked((uint)ItemData.ResolvePersistentHashId(tool.ToolData));
            if (itemHash != 0u)
                return itemHash;

            ToolMetadata metadata = tool.Metadata;
            return metadata != null && !string.IsNullOrWhiteSpace(metadata.toolID)
                ? unchecked((uint)LocHash.Compute(metadata.toolID))
                : 0u;
        }

        private uint ComputeAssignedSlotMask()
        {
            if (toolPrefabs == null)
                return 0u;

            uint mask = 0u;
            int count = Mathf.Min(toolPrefabs.Length, 32);
            for (int i = 0; i < count; i++)
            {
                if (toolPrefabs[i] != null)
                    mask |= 1u << i;
            }

            return mask;
        }

        private ushort ResolveActiveSlotSignalValue()
        {
            return _currentSlotIndex >= 0 && _currentSlotIndex <= ushort.MaxValue
                ? (ushort)_currentSlotIndex
                : ToolLoadoutChangedSignal.NoActiveSlot;
        }

        private ushort ResolveSlotCountSignalValue()
        {
            int slotCount = SlotCount;
            return slotCount > ushort.MaxValue ? ushort.MaxValue : (ushort)slotCount;
        }

        private byte ResolveToolLoadoutFlags()
        {
            byte flags = 0;
            if (_currentTool != null)
                flags |= ToolLoadoutChangedSignal.FlagHasActiveTool;
            if (_swapState != SwapState.Idle)
                flags |= ToolLoadoutChangedSignal.FlagSwapInProgress;

            return flags;
        }

        public bool SetAssignedToolPrefab(int slotIndex, GameObject prefab, bool holsterIfCurrentInvalid = true)
        {
            if (toolPrefabs == null || slotIndex < 0 || slotIndex >= toolPrefabs.Length)
                return false;

            if (ReferenceEquals(toolPrefabs[slotIndex], prefab))
                return true;

            toolPrefabs[slotIndex] = prefab;
            CacheAssignedToolPrefabComponentCold(slotIndex);
            EnsurePoolWarmup(prefab, toolPoolWarmupCount);
            PublishToolLoadoutChanged(ToolLoadoutChangedSignal.ReasonAssignmentsChanged);

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
            carrierBody = _currentInteriorCarrierBody;
            return _isInsideModuleInterior && carrierBody != null;
        }

        private void RefreshToolPrefabComponentCachesCold()
        {
            RefreshToolPrefabComponentCacheCold(toolPrefabs, ref _assignedToolPrefabComponents);
            RefreshToolPrefabComponentCacheCold(knownToolPrefabs, ref _knownToolPrefabComponents);
        }

        private static void RefreshToolPrefabComponentCacheCold(GameObject[] prefabs, ref PlayerTool[] components)
        {
            if (prefabs == null || prefabs.Length == 0)
            {
                components = Array.Empty<PlayerTool>();
                return;
            }

            if (components == null || components.Length != prefabs.Length)
                Array.Resize(ref components, prefabs.Length);

            for (int i = 0; i < prefabs.Length; i++)
                CacheToolPrefabComponentCold(prefabs[i], out components[i]);
        }

        private void CacheAssignedToolPrefabComponentCold(int slotIndex)
        {
            if (toolPrefabs == null || slotIndex < 0 || slotIndex >= toolPrefabs.Length)
                return;

            if (_assignedToolPrefabComponents == null || _assignedToolPrefabComponents.Length != toolPrefabs.Length)
            {
                RefreshToolPrefabComponentCacheCold(toolPrefabs, ref _assignedToolPrefabComponents);
                return;
            }

            GameObject prefab = toolPrefabs[slotIndex];
            if (TryFindCachedToolForPrefab(prefab, knownToolPrefabs, _knownToolPrefabComponents, out PlayerTool cachedTool))
            {
                _assignedToolPrefabComponents[slotIndex] = cachedTool;
                return;
            }

            CacheToolPrefabComponentCold(prefab, out _assignedToolPrefabComponents[slotIndex]);
        }

        private static void CacheToolPrefabComponentCold(GameObject prefab, out PlayerTool tool)
        {
            tool = null;
            if (prefab != null)
                prefab.TryGetComponent(out tool);
        }

        private PlayerTool GetAssignedToolPrefabComponent(int slotIndex)
        {
            return _assignedToolPrefabComponents != null &&
                   (uint)slotIndex < (uint)_assignedToolPrefabComponents.Length
                ? _assignedToolPrefabComponents[slotIndex]
                : null;
        }

        private PlayerTool GetKnownToolPrefabComponent(int prefabIndex)
        {
            return _knownToolPrefabComponents != null &&
                   (uint)prefabIndex < (uint)_knownToolPrefabComponents.Length
                ? _knownToolPrefabComponents[prefabIndex]
                : null;
        }

        private bool TryGetCachedToolForPrefab(GameObject prefab, out PlayerTool tool)
        {
            tool = null;
            if (prefab == null)
                return false;

            if (TryFindCachedToolForPrefab(prefab, toolPrefabs, _assignedToolPrefabComponents, out tool))
                return true;

            return TryFindCachedToolForPrefab(prefab, knownToolPrefabs, _knownToolPrefabComponents, out tool);
        }

        private static bool TryFindCachedToolForPrefab(
            GameObject prefab,
            GameObject[] prefabs,
            PlayerTool[] components,
            out PlayerTool tool)
        {
            tool = null;
            if (prefab == null || prefabs == null || components == null)
                return false;

            int count = Mathf.Min(prefabs.Length, components.Length);
            for (int i = 0; i < count; i++)
            {
                if (!ReferenceEquals(prefabs[i], prefab))
                    continue;

                tool = components[i];
                return tool != null;
            }

            return false;
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

                PlayerTool tool = GetKnownToolPrefabComponent(i);
                if (tool == null)
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

                if (GetKnownToolPrefabComponent(i) is TTool)
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
                target[i] = ToUpperAscii(source[i] == '_' ? ' ' : source[i]);

            return cursor + safeLength;
        }

        private static char ToUpperAscii(char value)
        {
            return value >= 'a' && value <= 'z'
                ? (char)(value - ('a' - 'A'))
                : value;
        }

        private static int AppendInt(Span<char> destination, int cursor, int value)
        {
            if (cursor >= destination.Length)
                return cursor;

            return value.TryFormat(destination.Slice(cursor), out int charsWritten)
                ? cursor + charsWritten
                : cursor;
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

                if (GetAssignedToolPrefabComponent(i) is TTool)
                    return i;
            }

            return -1;
        }

        public bool ApplyLoadoutPreset(ToolLoadoutPreset preset, bool holsterFirst = true)
        {
            if (preset == null || toolPrefabs == null || toolPrefabs.Length == 0)
                return false;

            GameObject[] presetSlots = preset.slotPrefabs;
            int count = presetSlots != null ? Mathf.Min(toolPrefabs.Length, presetSlots.Length) : 0;

            // A ToolLoadoutPreset is read-only DATA, not the owner of quick-slot truth - this manager
            // is. A slot the preset leaves empty says nothing about that slot, so it must not be able
            // to strip an assigned tool, and a preset that names no tool at all is an authoring gap
            // rather than an instruction to empty the bar. Both callers that can reach this from a
            // shipped build already render the refusal: PDALoadoutTab.cs:1180 ("FAILED TO APPLY") and
            // PlayerExpressionManager.cs:997.
            int namedSlots = 0;
            for (int i = 0; i < count; i++)
            {
                if (presetSlots[i] != null)
                    namedSlots++;
            }

            if (namedSlots == 0)
                return false;

            if (holsterFirst)
                Holster();

            GameObject previousCurrentSlotPrefab = GetAssignedToolPrefab(_currentSlotIndex);
            bool previousSignalSuppression = _suppressToolLoadoutSignal;
            _suppressToolLoadoutSignal = true;
            try
            {
                for (int i = 0; i < count; i++)
                {
                    GameObject slotPrefab = presetSlots[i];
                    if (slotPrefab == null)
                        continue;

                    SetAssignedToolPrefab(i, slotPrefab, holsterIfCurrentInvalid: false);
                }
            }
            finally
            {
                _suppressToolLoadoutSignal = previousSignalSuppression;
            }

            // The instance in hand must never disagree with the prefab its own slot now names.
            // holsterIfCurrentInvalid stays false in the loop so a reassignment cannot fight the swap
            // state machine mid-write, so the invariant is closed once, here. A caller passing
            // holsterFirst: false may skip the courtesy holster but cannot waive this: tools.md
            // section 7 puts tool truth on this owner, and SwitchToSlot would read the stale
            // _currentSlotIndex as "already equipped" (see RequestSwap early-out at :1729) and only
            // holster instead of raising the tool the bar is showing.
            if (!holsterFirst &&
                _currentTool != null &&
                _currentSlotIndex >= 0 &&
                !ReferenceEquals(GetAssignedToolPrefab(_currentSlotIndex), previousCurrentSlotPrefab))
            {
                Holster();
            }

            PublishToolLoadoutChanged(ToolLoadoutChangedSignal.ReasonAssignmentsChanged);
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
        }

        /// <summary>
        /// Grants the authored quick-slot tool items once so the production starter loadout actually exists
        /// in the inventory. Slot availability IS ownership: <see cref="IsToolAvailableInSlot"/> resolves
        /// through <see cref="HasToolInInventory"/>, so an assigned prefab on its own never makes a slot
        /// selectable. The completion latch is therefore only consumed when the grant genuinely finished -
        /// every examined slot is owned, permanently unresolvable, or the budget was spent. A refusal from
        /// the inventory (buffers not allocated yet on this activation, grid full) leaves the latch clear so
        /// the next activation or player-context rebind retries, instead of reporting a completed grant that
        /// added nothing and leaving all four slots unavailable for the rest of the session.
        /// </summary>
        private void TryGrantAssignedToolItemsOnRuntimeStart()
        {
            if (!Application.isPlaying ||
                _runtimeStartToolGrantCompleted ||
                !grantAssignedToolItemsOnRuntimeStart)
            {
                return;
            }

            if (playerInventory == null || toolPrefabs == null)
                return;

            int grantBudget = math.min(math.max(runtimeStartToolGrantBudget, 0), toolPrefabs.Length);
            if (grantBudget <= 0)
            {
                _runtimeStartToolGrantCompleted = true;
                return;
            }

            int granted = 0;
            int refused = 0;
            _suppressInventoryChangedHandling = true;
            try
            {
                for (int i = 0; i < toolPrefabs.Length && granted < grantBudget; i++)
                {
                    GameObject prefab = toolPrefabs[i];
                    if (prefab == null || !TryGetCachedToolForPrefab(prefab, out PlayerTool tool))
                        continue;

                    ItemData item = tool.ToolData;
                    int itemHash = ItemData.ResolvePersistentHashId(item);
                    if (itemHash == 0 || playerInventory.CountAvailableTotal(itemHash) > 0)
                        continue;

                    if (playerInventory.TryAddItem(itemHash, 1))
                    {
                        granted++;
                        continue;
                    }

                    refused++;
                }
            }
            finally
            {
                _suppressInventoryChangedHandling = false;
                BaselineInventoryChangedSignalRevision();
            }

            _debugRuntimeStartToolGrants += granted;
            _runtimeStartToolGrantCompleted = refused == 0;

            if (granted > 0)
                PublishToolLoadoutChanged(ToolLoadoutChangedSignal.ReasonAssignmentsChanged);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (refused > 0)
            {
                Hecton8.Core.H8Debug.LogWarning(
                    "[PlayerToolManager] STARTERGRANT deferred - the player inventory refused at least one " +
                    "assigned quick-slot tool item, so the grant stays open for the next player activation.");
            }
            else if (granted > 0)
            {
                Hecton8.Core.H8Debug.Log(
                    "[PlayerToolManager] STARTERGRANT applied - the assigned quick-slot tool items are owned.");
            }
            else
            {
                Hecton8.Core.H8Debug.Log(
                    "[PlayerToolManager] STARTERGRANT satisfied - every assigned quick-slot tool item was " +
                    "already owned.");
            }
#endif
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

            if (!TryResolveCachedObjectPool(out _))
                return;

            for (int i = 0; i < toolPrefabs.Length; i++)
                EnsurePoolWarmup(toolPrefabs[i], toolPoolWarmupCount);

            _assignedPoolsWarmed = true;
        }

        private void EnsurePoolWarmup(GameObject prefab, int minimumReserve)
        {
            if (prefab == null || minimumReserve <= 0)
                return;

            if (!TryResolveCachedObjectPool(out IObjectPoolService pool))
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

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Player:
                    IPlayerRuntimeContext previousPlayerContext = previousService as IPlayerRuntimeContext;
                    IPlayerRuntimeContext currentPlayerContext = currentService as IPlayerRuntimeContext;
                    bool previousOwnedThis = previousPlayerContext != null && ReferenceEquals(previousPlayerContext.ToolManager, this);
                    bool currentOwnsThis = currentPlayerContext != null && ReferenceEquals(currentPlayerContext.ToolManager, this);
                    if (previousOwnedThis && !currentOwnsThis)
                        ResetToolLifecycleForPlayerContextLoss();

                    ClearRuntimeContextOwnedReferences(previousPlayerContext);
                    _playerRuntimeService = currentPlayerContext;
                    RebindRuntimeContextFromPlayerService(_playerRuntimeService);
                    PublishRuntimeContextState();
                    break;

                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregisterFromTickManager(clearPendingPresentation: false);
                    if (currentService != null && isActiveAndEnabled)
                        TryRegisterToTickManager();
                    break;

                case GlobalRegistryServiceSlot.Input:
                    _inputService = currentService as IInputService;
                    break;

                case GlobalRegistryServiceSlot.Physics:
                    ToolHitUtility.ClearPhysicsServiceCold(_physicsService);
                    _physicsService = currentService as IPhysicsService;
                    ToolHitUtility.CachePhysicsServiceCold(_physicsService);
                    break;

                case GlobalRegistryServiceSlot.PlayerMovementContracts:
                    ToolHitUtility.ClearPlayerMovementForceSinkCold(_playerMovementForceSink);
                    _playerMovementForceSink = currentService as IPlayerMovementForceSink;
                    ToolHitUtility.CachePlayerMovementForceSinkCold(_playerMovementForceSink);
                    break;

                case GlobalRegistryServiceSlot.ObjectPool:
                    CacheObjectPoolService(currentService as ObjectPoolManager);
                    _assignedPoolsWarmed = false;
                    WarmRuntimePoolsIfNeeded();
                    break;

                case GlobalRegistryServiceSlot.Logistics:
                    break;

                case GlobalRegistryServiceSlot.PersistentWorldRegistry:
                    _persistentWorldRegistry = currentService as IPersistentDroppedItemRegistry;
                    break;

                case GlobalRegistryServiceSlot.ToolDurabilityRuntime:
                    _toolDurability = currentService as IToolDurabilityService;
                    break;

                case GlobalRegistryServiceSlot.Submarine:
                    _submarineRuntimeContext = currentService as ISubmarineRuntimeContext;
                    if (_isInsideModuleInterior)
                        CacheInteriorCarrierFromContext();
                    break;
            }
        }

        private void ClearRuntimeContextOwnedReferences(IPlayerRuntimeContext previousContext)
        {
            if (previousContext == null)
                return;

            if (ReferenceEquals(previousContext.ToolManager, this))
                _runtimeContext = null;
            if (ReferenceEquals(playerInventory, previousContext.Inventory))
                playerInventory = null;
            if (ReferenceEquals(playerTransportCoordinator, previousContext.PlayerTransportCoordinator))
                playerTransportCoordinator = null;
            if (ReferenceEquals(handAnchor, previousContext.HandAnchor))
                handAnchor = null;
        }

        private void ResetToolLifecycleForPlayerContextLoss()
        {
            _pendingSwapExecution = false;
            _pendingCurrentToolDespawn = false;
            _pendingToolSpawnExecution = false;
            _pendingToolSpawnPrefab = null;
            _pendingToolSpawnSlotIndex = -1;
            _pendingToolPoseFlush = false;
            _pendingToolPoseTransform = null;
            _pendingToolGripOffsets = null;
            _hasPendingHandAnchorLocalPosition = false;
            _pendingSlotIndex = -1;
            _swapState = SwapState.Idle;
            _swapProgress = 0f;
            ClearBatterySiphonLockout();
            FlushPendingToolPoolDespawn();
            DespawnCurrentToolImmediate();
            PublishRuntimeContextState();
        }

        private void RebindRuntimeContextFromPlayerService(IPlayerRuntimeContext playerContext)
        {
            if (playerContext == null || !ReferenceEquals(playerContext.ToolManager, this))
                return;

            if (PlayerRuntimeContextService.TryBindPlayerRoot(gameObject, out PlayerRuntimeContext runtimeContext))
                _runtimeContext = runtimeContext;

            if (playerInventory == null)
                playerInventory = playerContext.Inventory;

            if (playerTransportCoordinator == null)
                playerTransportCoordinator = playerContext.PlayerTransportCoordinator;

            if (handAnchor == null)
            {
                handAnchor = playerContext.HandAnchor;
                if (handAnchor != null)
                {
                    _anchorRestPosition = handAnchor.localPosition;
                    _anchorLoweredPosition = _anchorRestPosition + lowerOffset;
                }
            }

            TryGrantAssignedToolItemsOnRuntimeStart();
        }

        private void CacheRegistryServicesCold(bool forceRefresh = false)
        {
            if (forceRefresh || _playerRuntimeService == null)
                _playerRuntimeService = GlobalRegistry.Player;

            if (forceRefresh || _inputService == null)
                _inputService = GlobalRegistry.Input;

            if (forceRefresh || _physicsService == null)
                _physicsService = GlobalRegistry.Physics;

            if (forceRefresh || _playerMovementForceSink == null)
                _playerMovementForceSink = GlobalRegistry.PlayerMovementContracts;

            if (forceRefresh || _objectPool == null)
            {
                IObjectPoolService previousPool = _objectPool;
                CacheObjectPoolService(null);
                if (!ReferenceEquals(previousPool, _objectPool))
                    _assignedPoolsWarmed = false;
            }

            if (forceRefresh || _persistentWorldRegistry == null)
                _persistentWorldRegistry = GlobalRegistry.PersistentDroppedItems;

            if (forceRefresh || _toolDurability == null)
                _toolDurability = GlobalRegistry.ToolDurabilityService;

            if (forceRefresh || _submarineRuntimeContext == null)
                _submarineRuntimeContext = GlobalRegistry.Submarine;
        }

        private void CacheObjectPoolService(ObjectPoolManager candidate)
        {
            ObjectPoolManager pool = candidate;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(pool) ||
                ObjectPoolManager.TryResolveActiveRuntime(ref pool))
            {
                _objectPool = pool;
                return;
            }

            _objectPool = null;
        }

        private bool TryResolveCachedObjectPool(out IObjectPoolService pool)
        {
            ObjectPoolManager cached = _objectPool as ObjectPoolManager;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached))
            {
                pool = cached;
                return true;
            }

            ObjectPoolManager resolved = cached;
            if (ObjectPoolManager.TryResolveActiveRuntime(ref resolved))
            {
                _objectPool = resolved;
                pool = resolved;
                return true;
            }

            _objectPool = null;
            pool = null;
            return false;
        }

        private bool TryResolvePoolForInstance(
            IObjectPoolService preferredPool,
            GameObject instance,
            out IObjectPoolService pool)
        {
            return ObjectPoolManager.TryResolvePoolForInstance(instance, preferredPool, out pool);
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
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
            if (IsHandheldToolUsageBlocked() || IsBatterySiphonLockoutActive)
                flags |= (uint)PlayerRuntimeSnapshotFlags.HandheldToolBlocked;

            float swapProgress01 = IsBatterySiphonLockoutActive
                ? ResolveBatterySiphonProgress01()
                : math.saturate(_swapProgress);
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

        private void RefreshFieldLoadoutAdviceForVisualSync()
        {
            float now = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            if (now < _nextFieldLoadoutAdviceRefreshAt)
                return;

            _nextFieldLoadoutAdviceRefreshAt = now + FieldLoadoutAdviceRefreshInterval;
            Transform origin = transform;
            if (FieldLoadoutAdvisor.TryBuildForwardSnapshot(
                    origin,
                    fieldLoadoutAdviceRange,
                    ResolveFieldLoadoutAdviceMask(),
                    out FieldLoadoutAdvisor.ForwardLoadoutSnapshot snapshot))
            {
                _cachedFieldLoadoutAdvice = snapshot;
                _cachedFieldLoadoutAdviceValid = true;
                return;
            }

            _cachedFieldLoadoutAdvice = default;
            _cachedFieldLoadoutAdviceValid = false;
        }

        private LayerMask ResolveFieldLoadoutAdviceMask()
        {
            LayerMask resolvedMask = default;
            resolvedMask.value = HectonLayerMasks.ResolveFieldToolScanLayerMask(fieldLoadoutAdviceMask.value);
            return resolvedMask;
        }

        private void ClearCachedFieldLoadoutAdvice()
        {
            _cachedFieldLoadoutAdvice = default;
            _cachedFieldLoadoutAdviceValid = false;
            _nextFieldLoadoutAdviceRefreshAt = float.NegativeInfinity;
        }

        private void SubscribeModuleStatusEvents()
        {
            ModuleStatusEvents.Unregister(this);
            ModuleStatusEvents.Register(this);
        }

        private void UnsubscribeModuleStatusEvents()
        {
            ModuleStatusEvents.Unregister(this);
        }

        /// <inheritdoc />
        public void OnModuleStatusEvent(in ModuleStatusEventPayload payload)
        {
            if (ModuleStatusEvents.IsEnterEvent(in payload))
                HandleModuleEnter(in payload);
            else
                HandleModuleExit(in payload);
        }

        private void HandleModuleEnter(in ModuleStatusEventPayload payload)
        {
            if (payload.ModuleEntityId == 0ul ||
                !ModuleStatusEvents.IsPlayerInsideInterior(in payload))
                return;

            _currentInteriorModuleEntityId = payload.ModuleEntityId;
            _isInsideModuleInterior = true;
            CacheInteriorCarrierFromContext();
        }

        private void HandleModuleExit(in ModuleStatusEventPayload payload)
        {
            if (_currentInteriorModuleEntityId == 0ul || payload.ModuleEntityId == 0ul)
                return;

            if (_currentInteriorModuleEntityId != payload.ModuleEntityId)
                return;

            ClearInteriorCarrierCache();
        }

        private void CacheInteriorCarrierFromContext()
        {
            _currentInteriorCarrierBody = null;
            ISubmarineRuntimeContext submarine = _submarineRuntimeContext;
            if (submarine == null)
                return;

            _currentInteriorCarrierBody = submarine.HullRigidbody;
        }

        private void ClearInteriorCarrierCache()
        {
            _currentInteriorModuleEntityId = 0ul;
            _isInsideModuleInterior = false;
            _currentInteriorCarrierBody = null;
        }

        private bool IsHandheldToolUsageBlocked()
        {
            ResolveTransportCoordinator();
            return playerTransportCoordinator != null && playerTransportCoordinator.BlocksHandheldToolUsage();
        }

        private void ConsumeToolSlotInputSignals()
        {
            ReadOnlySpan<PlayerInputSignal> signals = SignalBus<PlayerInputSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PlayerInputSignal signal = signals[i];
                if (signal.SourceHash != PlayerInputSignalSourceHash ||
                    !IsNewerInputSequence(signal.Sequence, _lastPlayerInputSignalSequence))
                    continue;

                int slotIndex = ResolveToolSlotCommand(signal.Command);
                if (slotIndex < 0)
                    continue;

                _lastPlayerInputSignalSequence = signal.Sequence;
                HandleToolSlot(slotIndex);
            }
        }

        private void BaselineToolSlotInputSignalSequence()
        {
            ReadOnlySpan<PlayerInputSignal> signals = SignalBus<PlayerInputSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PlayerInputSignal signal = signals[i];
                if (signal.SourceHash == PlayerInputSignalSourceHash &&
                    IsNewerInputSequence(signal.Sequence, _lastPlayerInputSignalSequence))
                    _lastPlayerInputSignalSequence = signal.Sequence;
            }
        }

        private static int ResolveToolSlotCommand(byte command)
        {
            switch (command)
            {
                case PlayerInputSignalCommands.ToolSlot1:
                    return 0;
                case PlayerInputSignalCommands.ToolSlot2:
                    return 1;
                case PlayerInputSignalCommands.ToolSlot3:
                    return 2;
                case PlayerInputSignalCommands.ToolSlot4:
                    return 3;
                default:
                    return -1;
            }
        }

        private static bool IsNewerInputSequence(uint candidate, uint current)
        {
            return candidate != 0u && candidate != current && unchecked(candidate - current) < 0x80000000u;
        }

        private void HandleToolSlot(int index)
        {
            // Do not accept input during swap animation
            if (_swapState != SwapState.Idle)
                return;

            if (IsHandheldToolUsageBlocked() || IsBatterySiphonLockoutActive)
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
        /// Zaprashivaet smenu instrumenta.
        /// Esli tekuschiy instrument est — nachinaet animatsiyu opuskaniya.
        /// Esli net — srazu spavnit novyy (animatsiya podema).
        /// </summary>
        private void RequestSwap(int newSlotIndex)
        {
            if (newSlotIndex >= 0 && (IsHandheldToolUsageBlocked() || IsBatterySiphonLockoutActive))
                return;

            LogToolDebug("RequestSwap");

            // Uzhe na etom slote i ne holster
            if (newSlotIndex == _currentSlotIndex)
                return;

            // Proveryaem nalichie v inventare (tolko dlya validnyh slotov)
            if (newSlotIndex >= 0)
            {
                GameObject prefab = toolPrefabs[newSlotIndex];
                if (prefab == null)
                {
                    LogToolDebug("RequestSwap abort: slot prefab null");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Hecton8.Core.H8Debug.LogWarning("[PlayerToolManager] Slot prefab missing.");
#endif
                    return;
                }

                // Proveryaem ItemData na prefabe
                if (!HasToolInInventory(prefab))
                {
                    LogToolDebug("RequestSwap abort: slot missing in inventory");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Hecton8.Core.H8Debug.Log("[PlayerToolManager] Tool not found in inventory.");
#endif
                    return;
                }
            }

            _pendingSlotIndex = newSlotIndex;

            // Esli est tekuschiy instrument — opuskaem snachala
            if (_currentTool != null)
            {
                LogToolDebug("RequestSwap lowering current tool");
                _swapState    = SwapState.Lowering;
                _swapProgress = 0f;
            }
            else
            {
                // Net tekuschego — srazu spavnim
                LogToolDebug("RequestSwap performing immediate swap");
                QueueSwapExecution();
            }
        }

        /// <summary>
        /// Vypolnyaet fakticheskuyu smenu: despavn starogo → spavn novogo.
        /// Vyzyvaetsya posle zaversheniya animatsii opuskaniya (ili srazu,
        /// esli instrumenta ne bylo).
        /// </summary>
        private void PerformSwap()
        {
            LogToolDebug("PerformSwap begin");
            // ── Despavn tekuschego ──
            DespawnCurrentTool();

            // ── Spavn novogo ──
            if (_pendingSlotIndex >= 0 && _pendingSlotIndex < toolPrefabs.Length)
            {
                GameObject prefab = toolPrefabs[_pendingSlotIndex];

                if (prefab != null && handAnchor != null)
                {
                    LogToolDebug("PerformSwap spawning slot");
                    SpawnNewToolImmediate(prefab, _pendingSlotIndex);
                }
            }

            _currentSlotIndex = _pendingSlotIndex;
            _pendingSlotIndex = -1;
            LogToolDebug("PerformSwap assigned current slot");
            PublishToolLoadoutChanged(ToolLoadoutChangedSignal.ReasonActiveSlotChanged);

            // Esli spavnili novyy — zapuskaem animatsiyu podema
            if (_currentTool != null)
            {
                LogToolDebug("PerformSwap raising current tool");
                _swapState    = SwapState.Raising;
                _swapProgress = 0f;

                // Nachinaem iz nizhney pozitsii
                if (handAnchor != null)
                    QueueHandAnchorLocalPosition(_anchorLoweredPosition);
            }
            else
            {
                // Holster — vozvraschaem anchor v normalnuyu pozitsiyu
                LogToolDebug("PerformSwap completed with no current tool");
                _swapState = SwapState.Idle;
                if (handAnchor != null)
                    QueueHandAnchorLocalPosition(_anchorRestPosition);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — SWAP ANIMATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Obrabatyvaet animatsiyu smeny instrumenta (state machine).
        /// Ispolzuet math.lerp — zero GC, frame-independent.
        /// </summary>
        private void ProcessSwapAnimation(float deltaTime)
        {
            if (_swapState == SwapState.Idle)
                return;

            if (handAnchor == null)
            {
                // Net anchor — propuskaem animatsiyu, vypolnyaem mgnovenno
                if (_swapState == SwapState.Lowering)
                    QueueSwapExecution();
                else
                    _swapState = SwapState.Idle;
                return;
            }

            // Prodvigaem progress
            _swapProgress += deltaTime * swapSpeed;

            // Clamp
            if (_swapProgress > 1f)
                _swapProgress = 1f;

            switch (_swapState)
            {
                // ── LOWERING: rest → lowered ──
                case SwapState.Lowering:
                {
                    QueueHandAnchorLocalPosition((Vector3)math.lerp(
                        (float3)_anchorRestPosition,
                        (float3)_anchorLoweredPosition,
                        _swapProgress));

                    if (_swapProgress >= 1f)
                    {
                        QueueSwapExecution();
                    }

                    break;
                }

                // ── RAISING: lowered → rest ──
                case SwapState.Raising:
                {
                    QueueHandAnchorLocalPosition((Vector3)math.lerp(
                        (float3)_anchorLoweredPosition,
                        (float3)_anchorRestPosition,
                        _swapProgress));

                    if (_swapProgress >= 1f)
                    {
                        QueueHandAnchorLocalPosition(_anchorRestPosition);
                        _swapState = SwapState.Idle;
                    }

                    break;
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — SPAWN / DESPAWN
        // ══════════════════════════════════════════════════════════

        private void QueueSwapExecution()
        {
            _pendingSwapExecution = true;
        }

        private void FlushPendingSwapExecution()
        {
            if (!_pendingSwapExecution)
                return;

            _pendingSwapExecution = false;
            PerformSwap();
        }

        private void FlushPendingCurrentToolDespawn()
        {
            if (!_pendingCurrentToolDespawn)
                return;

            _pendingCurrentToolDespawn = false;
            DespawnCurrentTool();
        }

        private void QueueToolSpawnExecution(GameObject prefab, int slotIndex)
        {
            _pendingToolSpawnPrefab = prefab;
            _pendingToolSpawnSlotIndex = slotIndex;
            _pendingToolSpawnExecution = true;
        }

        private void FlushPendingToolSpawnExecution()
        {
            if (!_pendingToolSpawnExecution)
                return;

            GameObject prefab = _pendingToolSpawnPrefab;
            int slotIndex = _pendingToolSpawnSlotIndex;
            _pendingToolSpawnPrefab = null;
            _pendingToolSpawnSlotIndex = -1;
            _pendingToolSpawnExecution = false;
            SpawnNewToolImmediate(prefab, slotIndex);
        }

        /// <summary>
        /// Spavnit instrument iz pula i nastraivaet ego.
        /// </summary>
        private void SpawnNewTool(GameObject prefab, int slotIndex)
        {
            QueueToolSpawnExecution(prefab, slotIndex);
        }

        private void SpawnNewToolImmediate(GameObject prefab, int slotIndex)
        {
            LogToolDebug("SpawnNewTool begin");
            EnsurePoolWarmup(prefab, toolPoolWarmupCount);
            if (!TryResolveCachedObjectPool(out IObjectPoolService pool))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[PlayerToolManager] ObjectPool runtime owner is unavailable.");
#endif
                return;
            }

            // Spavnim cherez pul v pozitsiyu anchor
            _currentInstance = pool.Spawn(
                prefab,
                handAnchor.position,
                handAnchor.rotation);
            _currentInstancePool = _currentInstance != null ? pool : null;

            if (_currentInstance == null)
            {
                LogToolDebug("SpawnNewTool failed: pool returned null");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[PlayerToolManager] Failed to spawn assigned tool.");
#endif
                return;
            }

            // Privyazyvaem k anchor
            _currentInstance.transform.SetParent(handAnchor, false);
            PhysicalToolGripOffsets.TryResolveLastSpawned(_currentInstance, out PhysicalToolGripOffsets gripOffsets);
            QueueToolPoseFlush(_currentInstance.transform, gripOffsets);

            if (PlayerTool.TryResolveLastSpawnedTool(_currentInstance, out PlayerTool tool))
            {
                PlayerToolSwimContract.TryResolveLastSpawned(_currentInstance, out PlayerToolSwimContract swimContract);
                PlayerTransportFeelContract.TryResolveLastSpawned(_currentInstance, out PlayerTransportFeelContract transportFeelContract);
                tool.BindSpawnedPresentationContractsCold(swimContract, transportFeelContract);

                _currentTool = tool;
                _currentActiveToolHash = ResolveActiveToolHash(tool);
                _currentActiveToolMetadataHash = ResolveActiveToolMetadataHash(tool);
                LogToolDebug("SpawnNewTool got PlayerTool");
                _currentTool.OnEquip();
                LogToolDebug("SpawnNewTool after OnEquip");
            }
            else
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[PlayerToolManager] Assigned tool prefab has no PlayerTool component.");
#endif
                _currentTool = null;
                _currentActiveToolHash = 0u;
                _currentActiveToolMetadataHash = 0u;
            }
        }

        private void QueueToolPoseFlush(Transform target, PhysicalToolGripOffsets gripOffsets)
        {
            if (target == null)
                return;

            _pendingToolPoseTransform = target;
            _pendingToolGripOffsets = gripOffsets;
            _pendingToolPoseFlush = true;
        }

        private void QueueHandAnchorLocalPosition(Vector3 localPosition)
        {
            if (handAnchor == null)
                return;

            _pendingHandAnchorLocalPosition = localPosition;
            _hasPendingHandAnchorLocalPosition = true;
        }

        private void FlushPendingHandAnchorPose()
        {
            if (!_hasPendingHandAnchorLocalPosition)
                return;

            _hasPendingHandAnchorLocalPosition = false;
            if (handAnchor != null)
                handAnchor.localPosition = _pendingHandAnchorLocalPosition;
        }

        private void FlushPendingToolPose()
        {
            if (!_pendingToolPoseFlush)
                return;

            Transform target = _pendingToolPoseTransform;
            PhysicalToolGripOffsets gripOffsets = _pendingToolGripOffsets;
            _pendingToolPoseTransform = null;
            _pendingToolGripOffsets = null;
            _pendingToolPoseFlush = false;
            if (target == null)
                return;

            target.localPosition = Vector3.zero;
            target.localRotation = Quaternion.identity;
            if (gripOffsets != null)
                gripOffsets.TryApplyGripOffset(target, PhysicalHandSide.Right);
        }

        /// <summary>
        /// Despavnit tekuschiy instrument (vozvrat v pul).
        /// Bezopasno vyzyvat pri otsutstvii instrumenta.
        /// </summary>
        private void DespawnCurrentTool()
        {
            LogToolDebug("DespawnCurrentTool begin");
            _externallyDockedTool = null;

            if (ReferenceEquals(_currentTool, _batterySiphonTool))
                ClearBatterySiphonLockout();

            if (_currentTool != null)
            {
                _currentTool.OnUnequip();
                _currentTool = null;
                _currentActiveToolHash = 0u;
                _currentActiveToolMetadataHash = 0u;
            }

            if (_currentInstance != null)
            {
                _currentInstance.transform.SetParent(null, false);
                QueueToolPoolDespawn(_currentInstance, _currentInstancePool);
                _currentInstance = null;
                _currentInstancePool = null;
            }

            _currentSlotIndex = -1;
            LogToolDebug("DespawnCurrentTool complete currentSlot=-1");
            PublishToolLoadoutChanged(ToolLoadoutChangedSignal.ReasonActiveSlotChanged);
        }

        private void QueueToolPoolDespawn(GameObject instance, IObjectPoolService owningPool)
        {
            if (instance == null)
                return;

            _pendingToolPoolDespawnInstance = instance;
            _pendingToolPoolDespawnOwner = owningPool;
            _pendingToolPoolDespawn = true;
        }

        private void FlushPendingToolPoolDespawn()
        {
            if (!_pendingToolPoolDespawn)
                return;

            GameObject instance = _pendingToolPoolDespawnInstance;
            IObjectPoolService owningPool = _pendingToolPoolDespawnOwner;
            _pendingToolPoolDespawnInstance = null;
            _pendingToolPoolDespawnOwner = null;
            _pendingToolPoolDespawn = false;
            if (instance == null)
                return;

            if (TryResolvePoolForInstance(owningPool, instance, out IObjectPoolService pool))
                pool.Despawn(instance);
            else
                instance.SetActive(false);
        }

        private void DespawnCurrentToolImmediate()
        {
            LogToolDebug("DespawnCurrentTool begin");
            _externallyDockedTool = null;

            if (ReferenceEquals(_currentTool, _batterySiphonTool))
                ClearBatterySiphonLockout();

            if (_currentTool != null)
            {
                _currentTool.OnUnequip();
                _currentTool = null;
                _currentActiveToolHash = 0u;
                _currentActiveToolMetadataHash = 0u;
            }

            if (_currentInstance != null)
            {
                // Ottseplyaem ot anchor pered despavnom
                _currentInstance.transform.SetParent(null, false);

                if (TryResolvePoolForInstance(_currentInstancePool, _currentInstance, out IObjectPoolService pool))
                    pool.Despawn(_currentInstance);
                else
                    _currentInstance.SetActive(false);

                _currentInstance = null;
                _currentInstancePool = null;
            }

            _currentSlotIndex = -1;
            LogToolDebug("DespawnCurrentTool complete currentSlot=-1");
            PublishToolLoadoutChanged(ToolLoadoutChangedSignal.ReasonActiveSlotChanged);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — INVENTORY CHECK
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Proveryaet nalichie instrumenta v inventare igroka.
        /// Skaniruet InventoryGrid na predmet sovpadeniya ItemData.
        ///
        /// Vremya: O(cols × rows) v worst case, no vyzyvaetsya tolko
        /// pri nazhatii knopki (ne kazhdyy kadr).
        /// </summary>
        // Timed battery siphon; no SOA inventory mutation until the lockout completes.
        private bool TryBeginBatterySiphonLockoutIfNeeded()
        {
            if (_currentTool == null ||
                _currentSlotIndex < 0 ||
                _pendingSlotIndex >= 0 ||
                _swapState != SwapState.Idle ||
                playerInventory == null)
            {
                return false;
            }

            if (!(_currentTool is IBatteryTool batteryTool) || !IsBatteryToolDead(batteryTool))
                return false;

            if (!TryResolveInventoryBatteryCandidate(batteryTool, out int batteryHashId, out _))
                return false;

            _batterySiphonTool = _currentTool;
            _batterySiphonBatteryTool = batteryTool;
            _batterySiphonSlotIndex = _currentSlotIndex;
            _batterySiphonItemHashId = batteryHashId;
            _batterySiphonDurationSeconds = BatterySiphonLockoutSeconds;
            _batterySiphonRemainingSeconds = BatterySiphonLockoutSeconds;
            return true;
        }

        private void ProcessBatterySiphonLockout(float deltaTime)
        {
            if (!IsBatterySiphonContextValid())
            {
                ClearBatterySiphonLockout();
                return;
            }

            _batterySiphonRemainingSeconds = math.max(0f, _batterySiphonRemainingSeconds - math.max(0f, deltaTime));
            if (_batterySiphonRemainingSeconds > 0f)
                return;

            CompleteBatterySiphonLockout();
        }

        private void CompleteBatterySiphonLockout()
        {
            IBatteryTool batteryTool = _batterySiphonBatteryTool;
            int batteryHashId = _batterySiphonItemHashId;
            if (batteryTool == null ||
                batteryHashId == 0 ||
                playerInventory == null ||
                !TryResolveInventoryBatteryItem(batteryHashId, out ItemData batteryItem) ||
                !playerInventory.TryConsumeFirstMatchingItemByHash(batteryHashId, out _, out ushort qualityMilli, out ulong geneticsMask))
            {
                ClearBatterySiphonLockout();
                return;
            }

            ItemData removedBattery = batteryTool.HasBattery ? batteryTool.RemoveBattery() : null;
            if (!batteryTool.InsertBattery(batteryItem, 1f))
            {
                playerInventory.TryAddItemWithState(batteryHashId, new PlayerInventory.ItemState(geneticsMask, qualityMilli), 1);
                if (removedBattery != null)
                    batteryTool.InsertBattery(removedBattery, 0f);
            }

            ClearBatterySiphonLockout();
        }

        private bool IsBatterySiphonContextValid()
        {
            return _batterySiphonTool != null &&
                   _batterySiphonBatteryTool != null &&
                   ReferenceEquals(_currentTool, _batterySiphonTool) &&
                   _currentSlotIndex == _batterySiphonSlotIndex &&
                   _swapState == SwapState.Idle &&
                   !IsHandheldToolUsageBlocked() &&
                   IsBatterySiphonToolStillOwned() &&
                   IsBatteryToolDead(_batterySiphonBatteryTool);
        }

        private bool IsBatterySiphonToolStillOwned()
        {
            if (_batterySiphonSlotIndex < 0 || _batterySiphonSlotIndex >= SlotCount)
                return false;

            GameObject assignedPrefab = GetAssignedToolPrefab(_batterySiphonSlotIndex);
            return assignedPrefab != null && HasToolInInventory(assignedPrefab);
        }

        private void ClearBatterySiphonLockout()
        {
            _batterySiphonTool = null;
            _batterySiphonBatteryTool = null;
            _batterySiphonSlotIndex = -1;
            _batterySiphonItemHashId = 0;
            _batterySiphonRemainingSeconds = 0f;
            _batterySiphonDurationSeconds = 0f;
        }

        private float ResolveBatterySiphonProgress01()
        {
            if (_batterySiphonTool == null)
                return 0f;

            if (_batterySiphonDurationSeconds <= 0f)
                return 1f;

            return math.saturate(1f - (_batterySiphonRemainingSeconds / _batterySiphonDurationSeconds));
        }

        private bool TryResolveInventoryBatteryCandidate(
            IBatteryTool batteryTool,
            out int batteryHashId,
            out ItemData batteryItem)
        {
            batteryHashId = 0;
            batteryItem = null;

            ItemData installedBattery = batteryTool != null ? batteryTool.BatteryItem : null;
            int installedBatteryHash = ItemData.ResolvePersistentHashId(installedBattery);

            if (TryResolveInventoryBatteryItem(installedBatteryHash, out batteryItem))
            {
                batteryHashId = installedBatteryHash;
                return true;
            }

            if (TryResolveInventoryBatteryItem(_highCapacityBatteryHashId, out batteryItem))
            {
                batteryHashId = _highCapacityBatteryHashId;
                return true;
            }

            if (TryResolveInventoryBatteryItem(_standardBatteryHashId, out batteryItem))
            {
                batteryHashId = _standardBatteryHashId;
                return true;
            }

            return false;
        }

        private bool TryResolveInventoryBatteryItem(int batteryHashId, out ItemData batteryItem)
        {
            batteryItem = null;
            if (batteryHashId == 0 || playerInventory == null || playerInventory.CountAvailableTotal(batteryHashId) <= 0)
                return false;

            var catalog = playerInventory.ItemCatalog;
            batteryItem = catalog != null ? catalog.FindByHash(batteryHashId) : null;
            return batteryItem != null;
        }

        private static bool IsBatteryToolDead(IBatteryTool batteryTool)
        {
            return batteryTool != null &&
                   (!batteryTool.HasBattery || batteryTool.BatteryCharge <= BatteryDeadThreshold01);
        }

        private bool HasToolInInventory(GameObject toolPrefab)
        {
            if (playerInventory == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning("[PlayerToolManager] PlayerInventory reference is null!");
#endif
                return false;
            }

            if (!TryGetCachedToolForPrefab(toolPrefab, out PlayerTool prefabTool))
                return false;

            ItemData targetData = prefabTool.ToolData;
            int targetHashId = ItemData.ResolvePersistentHashId(targetData);
            if (targetHashId == 0)
                return false;

            return playerInventory.CountAvailableTotal(targetHashId) > 0;
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

                int toolHashId = ItemData.ResolvePersistentHashId(brokenToolData);
                if (toolHashId == 0)
                {
                    Holster();
                    return;
                }

                ConsumeBrokenToolInventoryEntry(toolHashId);
                PlayerSignalEvents.TryRaiseToolDepletedSignal(new PlayerToolDepletedSignal(toolHashId));

                if (playerInventory != null && playerInventory.TryFindFirstAnchorByHash(toolHashId, out _))
                {
                    IToolDurabilityService durabilitySystem = _toolDurability;
                    if (durabilitySystem != null)
                        durabilitySystem.TryResetDurability(unchecked((uint)toolHashId), metadata.maxDurability);

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
                BaselineInventoryChangedSignalRevision();
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

            if (!TryGetCachedToolForPrefab(candidatePrefab, out PlayerTool candidateTool))
                return false;

            return ReferenceEquals(candidateTool.ToolData, brokenToolData);
        }

        private bool IsPrefabBroken(GameObject prefab)
        {
            if (prefab == null || !TryGetCachedToolForPrefab(prefab, out PlayerTool tool) || tool.Metadata == null)
                return false;

            IToolDurabilityService durabilitySystem = _toolDurability;
            if (durabilitySystem == null)
                return false;

            uint itemHash = ResolveActiveToolHash(tool);
            if (itemHash != 0u && durabilitySystem.TryReadBroken(itemHash, out bool brokenByItem))
                return brokenByItem;

            uint metadataHash = ResolveActiveToolMetadataHash(tool);
            return metadataHash != 0u &&
                   metadataHash != itemHash &&
                   durabilitySystem.TryReadBroken(metadataHash, out bool brokenByMetadata) &&
                   brokenByMetadata;
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
                QueueHandAnchorLocalPosition(_anchorRestPosition);

            PublishToolLoadoutChanged(ToolLoadoutChangedSignal.ReasonActiveSlotChanged);
        }

        private void RefreshInventorySignalFilter()
        {
            if (ReferenceEquals(_boundInventorySignalSource, playerInventory))
                return;

            _boundInventorySignalSource = playerInventory;
            _inventorySignalHash = ResolveInventorySignalHash(playerInventory);
            _lastInventorySignalRevision = playerInventory != null ? unchecked((uint)playerInventory.InventoryVersion) : 0u;
        }

        private void BaselineInventoryChangedSignalRevision()
        {
            RefreshInventorySignalFilter();
            _lastInventorySignalRevision = playerInventory != null ? unchecked((uint)playerInventory.InventoryVersion) : 0u;
        }

        private bool ConsumeInventoryChangedSignals()
        {
            RefreshInventorySignalFilter();
            uint inventoryHash = _inventorySignalHash;
            if (inventoryHash == 0u)
                return false;

            bool changed = false;
            ReadOnlySpan<InventoryChangedSignal> signals = SignalBus<InventoryChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                ref readonly InventoryChangedSignal signal = ref signals[i];
                if (signal.InventoryHash != inventoryHash ||
                    signal.Revision == 0u ||
                    (_lastInventorySignalRevision != 0u && signal.Revision <= _lastInventorySignalRevision))
                {
                    continue;
                }

                _lastInventorySignalRevision = signal.Revision;
                changed = true;
            }

            return changed;
        }

        private bool ConsumeEquippedToolDurabilitySignals()
        {
            if (_currentTool == null || _handlingEquippedToolBreak)
                return false;

            ReadOnlySpan<ItemDurabilityChangedSignal> signals = SignalBus<ItemDurabilityChangedSignal>.GetFrameSnapshot();
            if (signals.Length == 0)
                return false;

            uint itemHash = _currentActiveToolHash;
            uint metadataHash = _currentActiveToolMetadataHash;
            if (itemHash == 0u && metadataHash == 0u)
                return false;

            for (int i = 0; i < signals.Length; i++)
            {
                ref readonly ItemDurabilityChangedSignal signal = ref signals[i];
                bool broken = signal.Reason == ItemDurabilityChangedSignal.ReasonBreak ||
                              (signal.Flags & ItemDurabilityChangedSignal.FlagBroken) != 0;
                if (!broken)
                    continue;

                uint signalHash = signal.ItemHash;
                if (signalHash == 0u ||
                    (signalHash != itemHash && signalHash != metadataHash))
                {
                    continue;
                }

                HandleEquippedToolBroken();
                return true;
            }

            return false;
        }

        private static uint ResolveActiveToolMetadataHash(PlayerTool tool)
        {
            ToolMetadata metadata = tool != null ? tool.Metadata : null;
            if (metadata == null || string.IsNullOrWhiteSpace(metadata.toolID))
                return 0u;

            return unchecked((uint)Animator.StringToHash(metadata.toolID));
        }

        private static uint ResolveInventorySignalHash(PlayerInventory inventory)
        {
            return inventory != null && inventory.gameObject != null
                ? unchecked((uint)EntityId.ToULong(inventory.gameObject.GetEntityId()))
                : 0u;
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

            LogToolDebug("HandleInventoryChanged holstering missing assigned prefab");
            Holster();
        }

        private string ResolveSlotName(int slotIndex)
        {
            if (toolPrefabs == null || slotIndex < 0 || slotIndex >= toolPrefabs.Length)
                return null;

            GameObject prefab = toolPrefabs[slotIndex];
            if (prefab == null)
                return null;

            PlayerTool tool = GetAssignedToolPrefabComponent(slotIndex);
            if (tool != null && tool.ToolData != null && !string.IsNullOrWhiteSpace(tool.ToolData.itemName))
                return tool.ToolData.itemName;

            return prefab.name;
        }

        private ReadOnlySpan<char> ResolveSlotNameSpan(int slotIndex)
        {
            if (toolPrefabs == null || slotIndex < 0 || slotIndex >= toolPrefabs.Length)
                return ReadOnlySpan<char>.Empty;

            GameObject prefab = toolPrefabs[slotIndex];
            if (prefab == null)
                return ReadOnlySpan<char>.Empty;

            PlayerTool tool = GetAssignedToolPrefabComponent(slotIndex);
            if (tool != null &&
                tool.ToolData != null &&
                !string.IsNullOrWhiteSpace(tool.ToolData.itemName))
                return tool.ToolData.itemName.AsSpan();

            return "TOOL".AsSpan();
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogToolDebug(string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!toolDebugLogging)
                return;

            Hecton8.Core.H8Debug.Log(message);
#endif
        }

#if UNITY_EDITOR
        private static string GetSwapStateDebugName(SwapState state)
        {
            switch (state)
            {
                case SwapState.Lowering:
                    return "Lowering";
                case SwapState.Raising:
                    return "Raising";
                default:
                    return "Idle";
            }
        }
#endif

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
                "Assets/_Project/Prefabs/Tools/Held/Tool_HarpoonLauncher_Held.prefab",
                "Assets/_Project/Prefabs/Tools/Held/Tool_SeafloorDrill_Held.prefab"
            };

            if (knownToolPrefabs == null || knownToolPrefabs.Length != prefabPaths.Length)
                Array.Resize(ref knownToolPrefabs, prefabPaths.Length);

            for (int i = 0; i < prefabPaths.Length; i++)
            {
                if (knownToolPrefabs[i] != null)
                    continue;

                knownToolPrefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPaths[i]);
            }

            RefreshToolPrefabComponentCachesCold();
        }
#endif
    }
}
