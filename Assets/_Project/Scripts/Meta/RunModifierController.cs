using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Hecton8.SaveSystem;
using UnityEngine;

namespace Hecton8.Meta
{
    /// <summary>
    /// Scene-level owner for hardcore run modifiers persisted inside slot saves.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-6350)]
    [AddComponentMenu("Hecton8/Meta/Run Modifier Controller")]
    public sealed class RunModifierController : MonoBehaviour, ISaveable, IUpdatable, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        private static RunModifiersDTO _pendingNewGameModifiers;
        private static bool _hasPendingNewGameModifiers;

        private RunModifiersDTO _currentModifiers;
        private bool _deleteIssuedForCurrentRun;
        private bool _serviceRegistered;
        private bool _saveRegistered;
        private bool _registeredToUpdate;
        private bool _hotSwapRegistered;
        private ISaveService _saveService;
        private ISaveService _registeredSaveService;
        private SaveManager _saveManager;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private HectonSurvivalSystem _survivalSystem;
        private uint _survivalSignalSourceId;
        private int _lastSurvivalDeathSignalSequence;
        private uint _lastSessionLifecycleSequence;

        /// <summary>
        /// Current local-run modifier snapshot.
        /// </summary>
        public RunModifiersDTO CurrentModifiers => _currentModifiers;

        /// <summary>
        /// Returns true when the active run forces nightmare difficulty rules.
        /// </summary>
        public static bool IsNightmareModeActive
        {
            get
            {
                RunModifierController runtime = ResolveActiveRuntime();
                return runtime != null && runtime._currentModifiers.isNightmareMode;
            }
        }

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => _serviceRegistered ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        /// <inheritdoc />
        public bool IsServiceReady => _serviceRegistered;

        /// <summary>
        /// Queues modifier flags for the next new-game bootstrap.
        /// </summary>
        /// <param name="modifiers">Modifier snapshot that should seed the next new run.</param>
        public static void ConfigureNextNewGame(RunModifiersDTO modifiers)
        {
            _pendingNewGameModifiers = modifiers;
            _hasPendingNewGameModifiers = true;
        }

        private void Awake()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            ResetForCurrentContext();
        }

        private void OnEnable()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            TryRegisterService();
            if (Application.isPlaying && !_serviceRegistered)
                return;

            TryRegisterHotSwapListener();
            RefreshColdRegistryDependencies();
            TryRegisterSaveOwner();
            TryRegisterWithUpdateDispatcher();
            ResetForCurrentContext();
        }

        private void Start()
        {
            if (Application.isPlaying && !_serviceRegistered)
                return;

            ResetForCurrentContext();
            TryRegisterWithUpdateDispatcher();
        }

        private void OnDisable()
        {
            TryUnregisterSaveOwner();
            UnregisterFromUpdateDispatcher();
            TryUnregisterHotSwapListener();
            ClearPlayerRuntimeContext();
            TryUnregisterService();
        }

        private void OnDestroy()
        {
            OnServiceShutdown();
        }

        /// <inheritdoc />
        public void OnServiceShutdown()
        {
            TryUnregisterSaveOwner();
            UnregisterFromUpdateDispatcher();
            TryUnregisterHotSwapListener();
            ClearPlayerRuntimeContext();
            TryUnregisterService();
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                UnregisterFromUpdateDispatcher();
                if (currentService != null && isActiveAndEnabled)
                    TryRegisterWithUpdateDispatcher();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                CachePlayerRuntimeContext(currentService as IPlayerRuntimeContext);
                RefreshSurvivalSignalBinding();
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.Save)
                return;

            TryUnregisterSaveOwner();
            _saveService = currentService as ISaveService;
            _saveManager = currentService as SaveManager;

            if (Application.isPlaying && isActiveAndEnabled)
                TryRegisterSaveOwner();
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            if (TryAbortForUsableExistingRuntime())
                return;

            GlobalRegistry.RegisterRunModifierRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.RunModifiers, this);
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            RunModifierController registered = GlobalRegistry.RunModifiers;
            if (ReferenceEquals(registered, null) || ReferenceEquals(registered, this))
                return false;

            if (IsRunModifierRuntimeUsable(registered))
            {
                Destroy(gameObject);
                return true;
            }

            GlobalRegistry.UnregisterRunModifierRuntime(registered);
            return false;
        }

        private static RunModifierController ResolveActiveRuntime()
        {
            RunModifierController registered = GlobalRegistry.RunModifiers;
            if (IsRunModifierRuntimeUsable(registered))
                return registered;

            if (!ReferenceEquals(registered, null))
                GlobalRegistry.UnregisterRunModifierRuntime(registered);

            return null;
        }

        private static bool IsRunModifierRuntimeUsable(RunModifierController controller)
        {
            return controller != null &&
                   controller._serviceRegistered &&
                   controller.isActiveAndEnabled;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterRunModifierRuntime(this);
            _serviceRegistered = false;
        }

        /// <summary>
        /// Returns true when the active run has been marked permanently dead.
        /// </summary>
        public bool IsRunMarkedDead()
        {
            return _currentModifiers.runMarkedDead;
        }

        /// <inheritdoc />
        public int SavePriority => 6;

        /// <inheritdoc />
        public int LoadPriority => 6;

        /// <inheritdoc />
        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            data.runModifiers = _currentModifiers;
        }

        /// <inheritdoc />
        public void LoadFromSaveData(SaveData data)
        {
            if (data == null)
            {
                _currentModifiers = default;
                _deleteIssuedForCurrentRun = false;
                return;
            }

            _currentModifiers = data.runModifiers;
            NormalizeCurrentModifiers();
            _deleteIssuedForCurrentRun = false;
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            ProcessSessionLifecycleSignals();
            ConsumeSurvivalDeathSignal();
        }

        private void HandlePlayerDied()
        {
            if (!_currentModifiers.isPermadeath)
                return;

            _currentModifiers.runMarkedDead = true;
            TryDeleteCurrentSlot();
        }

        private void HandleGameLoaded()
        {
            _deleteIssuedForCurrentRun = false;
            if (_currentModifiers.runMarkedDead && _currentModifiers.isPermadeath)
                TryDeleteCurrentSlot();
        }

        private void ProcessSessionLifecycleSignals()
        {
            global::System.ReadOnlySpan<SessionLifecycleSignal> signals = SignalBus<SessionLifecycleSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                SessionLifecycleSignal signal = signals[i];
                if (!IsNewerSequence(signal.Sequence, _lastSessionLifecycleSequence))
                    continue;

                _lastSessionLifecycleSequence = signal.Sequence;
                if (signal.Kind == SessionLifecycleSignal.KindGameLoaded)
                    HandleGameLoaded();
            }
        }

        private void ConsumeSurvivalDeathSignal()
        {
            uint sourceId = _survivalSignalSourceId;
            if (sourceId == 0u)
                return;

            if (!SurvivalSignalRoute.TryGetLatestDeathForSource(sourceId, out SurvivalVitalsChangedSignal signal, out int sequence))
                return;

            if (sequence == _lastSurvivalDeathSignalSequence)
                return;

            _lastSurvivalDeathSignalSequence = sequence;
            if (signal.SourceId != sourceId ||
                (signal.Flags & SurvivalVitalsChangedSignalFlags.Death) == 0u)
            {
                return;
            }

            HandlePlayerDied();
        }

        private void ResetForCurrentContext()
        {
            GameStartContext context = GameStartContextHolder.Current;
            if (context.StartMode != GameStartMode.NewGame)
                return;

            if (_hasPendingNewGameModifiers)
            {
                _currentModifiers = _pendingNewGameModifiers;
                _hasPendingNewGameModifiers = false;
                _pendingNewGameModifiers = default;
            }
            else
            {
                _currentModifiers = default;
            }

            NormalizeCurrentModifiers();
            _deleteIssuedForCurrentRun = false;
        }

        private void NormalizeCurrentModifiers()
        {
            if (!_currentModifiers.isPermadeath)
                _currentModifiers.runMarkedDead = false;

            if (!_currentModifiers.isDailySeed)
                _currentModifiers.dailySeedId = string.Empty;
            else if (_currentModifiers.dailySeedId == null)
                _currentModifiers.dailySeedId = string.Empty;
        }

        private void TryDeleteCurrentSlot()
        {
            if (_deleteIssuedForCurrentRun)
                return;

            string slotName = ResolveCurrentSlotName();
            if (string.IsNullOrWhiteSpace(slotName))
                return;

            SaveManager saveManager = _saveManager;
            if (!IsSaveManagerUsable(saveManager))
                return;

            if (!SaveManager.TryResolveSafeSlotName(slotName, out slotName))
                return;

            saveManager.DeleteSave(slotName);
            _deleteIssuedForCurrentRun = true;
        }

        private string ResolveCurrentSlotName()
        {
            GameStartContext context = GameStartContextHolder.Current;
            if (!string.IsNullOrWhiteSpace(context.TargetSaveSlot) &&
                SaveManager.TryResolveSafeSlotName(context.TargetSaveSlot, out string safeContextSlotName))
            {
                return safeContextSlotName;
            }

            SaveManager saveManager = _saveManager;
            if (IsSaveManagerUsable(saveManager) &&
                !string.IsNullOrWhiteSpace(saveManager.LastOperationSlot) &&
                SaveManager.TryResolveSafeSlotName(saveManager.LastOperationSlot, out string safeLastOperationSlot))
            {
                return safeLastOperationSlot;
            }

            return string.Empty;
        }

        private void RefreshColdRegistryDependencies()
        {
            _saveService = GlobalRegistry.Save;
            _saveManager = _saveService as SaveManager;
            CachePlayerRuntimeContext(GlobalRegistry.Player);
            RefreshSurvivalSignalBinding();
        }

        private void TryRegisterSaveOwner()
        {
            if (!Application.isPlaying || _saveRegistered)
                return;

            ISaveService saveService = _saveService;
            if (!IsSaveServiceUsable(saveService))
            {
                saveService = GlobalRegistry.Save;
                _saveService = saveService;
                _saveManager = saveService as SaveManager;
            }

            if (!IsSaveServiceUsable(saveService))
                return;

            saveService.Register(this);
            _registeredSaveService = saveService;
            _saveRegistered = true;
        }

        private void TryUnregisterSaveOwner()
        {
            if (!_saveRegistered && _registeredSaveService == null)
                return;

            ISaveService saveService = _registeredSaveService != null ? _registeredSaveService : _saveService;
            if (saveService != null)
                saveService.Unregister(this);

            _registeredSaveService = null;
            _saveRegistered = false;
        }

        private void TryRegisterWithUpdateDispatcher()
        {
            if (_registeredToUpdate || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            uint sourceId = _survivalSignalSourceId;
            _lastSurvivalDeathSignalSequence = sourceId != 0u &&
                                               SurvivalSignalRoute.TryGetLatestDeathForSource(sourceId, out _, out int sequence)
                ? sequence
                : 0;
            _registeredToUpdate = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core);
        }

        private void UnregisterFromUpdateDispatcher()
        {
            if (!_registeredToUpdate)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            _registeredToUpdate = false;
        }

        private static bool IsSaveServiceUsable(ISaveService saveService)
        {
            return saveService != null && saveService.IsInitialized;
        }

        private static bool IsSaveManagerUsable(SaveManager saveManager)
        {
            return saveManager != null && saveManager.IsInitialized;
        }

        private void CachePlayerRuntimeContext(IPlayerRuntimeContext playerContext)
        {
            _playerRuntimeContext = playerContext != null && playerContext.IsInitialized ? playerContext : null;
            _survivalSystem = _playerRuntimeContext != null ? _playerRuntimeContext.SurvivalSystem : null;
        }

        private void ClearPlayerRuntimeContext()
        {
            _playerRuntimeContext = null;
            _survivalSystem = null;
            _survivalSignalSourceId = 0u;
            _lastSurvivalDeathSignalSequence = 0;
        }

        private void RefreshSurvivalSignalBinding()
        {
            uint sourceId = ResolveSurvivalSignalSourceId(_survivalSystem);
            if (_survivalSignalSourceId == sourceId)
                return;

            _survivalSignalSourceId = sourceId;
            _lastSurvivalDeathSignalSequence = sourceId != 0u &&
                                               SurvivalSignalRoute.TryGetLatestDeathForSource(sourceId, out _, out int sequence)
                ? sequence
                : 0;
        }

        private static uint ResolveSurvivalSignalSourceId(HectonSurvivalSystem system)
        {
            return system != null
                ? RuntimeOriginRoute.FoldEntityIdToSourceId(EntityId.ToULong(system.GetEntityId()))
                : 0u;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private static bool IsNewerSequence(uint candidate, uint lastProcessed)
        {
            return candidate != 0u && (lastProcessed == 0u || unchecked((int)(candidate - lastProcessed)) > 0);
        }
    }
}
