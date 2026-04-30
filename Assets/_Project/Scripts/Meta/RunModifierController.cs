using Hecton8.Core;
using Hecton8.Modding;
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
    public sealed class RunModifierController : MonoBehaviour, ISaveable
    {
        private static RunModifierController _instance;
        private static RunModifiersDTO _pendingNewGameModifiers;
        private static bool _hasPendingNewGameModifiers;

        private HectonEventSubscription _playerDiedSubscription;
        private HectonEventSubscription _gameLoadedSubscription;
        private RunModifiersDTO _currentModifiers;
        private bool _deleteIssuedForCurrentRun;

        /// <summary>
        /// Active runtime owner for slot-scoped hardcore modifiers.
        /// </summary>
        public static RunModifierController Instance => _instance;

        /// <summary>
        /// Current local-run modifier snapshot.
        /// </summary>
        public RunModifiersDTO CurrentModifiers => _currentModifiers;

        /// <summary>
        /// Returns true when the active run forces nightmare difficulty rules.
        /// </summary>
        public static bool IsNightmareModeActive => _instance != null && _instance._currentModifiers.isNightmareMode;

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
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            ResetForCurrentContext();
        }

        private void OnEnable()
        {
            Hecton8.Core.GlobalRegistry.SaveRuntime?.Register(this);
            SubscribeToEventBus();
            ResetForCurrentContext();
        }

        private void Start()
        {
            ResetForCurrentContext();
        }

        private void OnDisable()
        {
            Hecton8.Core.GlobalRegistry.SaveRuntime?.Unregister(this);
            UnsubscribeFromEventBus();
        }

        private void OnDestroy()
        {
            Hecton8.Core.GlobalRegistry.SaveRuntime?.Unregister(this);
            UnsubscribeFromEventBus();

            if (_instance == this)
                _instance = null;
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

        private void HandlePlayerDied(PlayerDiedEvent playerDiedEvent)
        {
            if (!_currentModifiers.isPermadeath)
                return;

            _currentModifiers.runMarkedDead = true;
            TryDeleteCurrentSlot();
        }

        private void HandleGameLoaded(GameLoadedEvent gameLoadedEvent)
        {
            _deleteIssuedForCurrentRun = false;
            if (_currentModifiers.runMarkedDead && _currentModifiers.isPermadeath)
                TryDeleteCurrentSlot();
        }

        private void SubscribeToEventBus()
        {
            if (_playerDiedSubscription == null)
                _playerDiedSubscription = HectonEventBus.Subscribe<PlayerDiedEvent>(HandlePlayerDied, "meta.run-modifiers");

            if (_gameLoadedSubscription == null)
                _gameLoadedSubscription = HectonEventBus.Subscribe<GameLoadedEvent>(HandleGameLoaded, "meta.run-modifiers");
        }

        private void UnsubscribeFromEventBus()
        {
            _playerDiedSubscription?.Dispose();
            _playerDiedSubscription = null;
            _gameLoadedSubscription?.Dispose();
            _gameLoadedSubscription = null;
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

            SaveManager saveManager = Hecton8.Core.GlobalRegistry.SaveRuntime;
            if (saveManager == null)
                return;

            saveManager.DeleteSave(slotName);
            _deleteIssuedForCurrentRun = true;
        }

        private static string ResolveCurrentSlotName()
        {
            GameStartContext context = GameStartContextHolder.Current;
            if (!string.IsNullOrWhiteSpace(context.TargetSaveSlot))
                return context.TargetSaveSlot;

            SaveManager saveManager = Hecton8.Core.GlobalRegistry.SaveRuntime;
            if (saveManager != null && !string.IsNullOrWhiteSpace(saveManager.LastOperationSlot))
                return saveManager.LastOperationSlot;

            return string.Empty;
        }
    }
}
