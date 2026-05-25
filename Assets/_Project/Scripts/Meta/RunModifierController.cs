using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
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
        private SaveManager _saveManager;
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
                RunModifierController runtime = GlobalRegistry.RunModifiers;
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
            RunModifierController registered = GlobalRegistry.RunModifiers;
            if (registered != null && registered != this)
            {
                Destroy(gameObject);
                return;
            }

            ResetForCurrentContext();
        }

        private void OnEnable()
        {
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
            TryUnregisterService();
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Save)
                return;

            if (_saveRegistered && previousService is ISaveService previousSave)
                previousSave.Unregister(this);

            _saveRegistered = false;
            _saveService = currentService as ISaveService;
            _saveManager = currentService as SaveManager;

            if (Application.isPlaying && isActiveAndEnabled)
                TryRegisterSaveOwner();
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            RunModifierController registered = GlobalRegistry.RunModifiers;
            if (registered != null && registered != this)
            {
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterRunModifierRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.RunModifiers, this);
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
            if (!SurvivalSignalRoute.TryGetLatestDeath(out _, out int sequence))
                return;

            if (sequence == _lastSurvivalDeathSignalSequence)
                return;

            _lastSurvivalDeathSignalSequence = sequence;
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
            if (saveManager == null)
                return;

            saveManager.DeleteSave(slotName);
            _deleteIssuedForCurrentRun = true;
        }

        private string ResolveCurrentSlotName()
        {
            GameStartContext context = GameStartContextHolder.Current;
            if (!string.IsNullOrWhiteSpace(context.TargetSaveSlot))
                return context.TargetSaveSlot;

            SaveManager saveManager = _saveManager;
            if (saveManager != null && !string.IsNullOrWhiteSpace(saveManager.LastOperationSlot))
                return saveManager.LastOperationSlot;

            return string.Empty;
        }

        private void RefreshColdRegistryDependencies()
        {
            _saveService = Hecton8.SaveSystem.SaveManager.ActiveRuntimeInstance;
            _saveManager = _saveService as SaveManager;
        }

        private void TryRegisterSaveOwner()
        {
            if (!Application.isPlaying || _saveRegistered)
                return;

            ISaveService saveService = _saveService;
            if (saveService == null)
                return;

            saveService.Register(this);
            _saveRegistered = true;
        }

        private void TryUnregisterSaveOwner()
        {
            if (!_saveRegistered)
                return;

            _saveService?.Unregister(this);
            _saveRegistered = false;
        }

        private void TryRegisterWithUpdateDispatcher()
        {
            if (_registeredToUpdate || !Application.isPlaying)
                return;

            _lastSurvivalDeathSignalSequence = SurvivalSignalRoute.TryGetLatestDeath(out _, out int sequence)
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
