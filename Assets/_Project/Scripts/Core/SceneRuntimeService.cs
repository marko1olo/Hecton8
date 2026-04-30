using System;
using Hecton8.Bootstrap;
using Hecton8.Physics;
using Hecton8.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Core
{
    /// <summary>
    /// Guarded scene transition owner for GlobalRegistry cleanup and bootstrap gating.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9940)]
    public sealed class SceneRuntimeService : MonoBehaviour, ISceneService, IUpdatable
    {
        private const int SceneActivationWatchdogInitialFrames = 1200;
        private const int SceneActivationWatchdogRepeatFrames = 300;

        private static SceneRuntimeService _instance;
        private bool _isInitialized;
        private bool _registeredSceneService;
        private bool _registeredSceneCallbacks;
        private bool _registeredUpdatable;
        private bool _sceneLoadInFlight;
        private string _pendingSceneName;
        private AsyncOperation _pendingSceneLoadOperation;

        /// <summary>
        /// True once the service has registered itself into <see cref="GlobalRegistry"/>.
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// True when bootstrap has completed and guarded transitions are allowed.
        /// </summary>
        public bool CanLoadScene => GameBootstrapper.IsBootstrapComplete;

        /// <summary>
        /// Ensures a live runtime instance exists.
        /// </summary>
        /// <returns>Live scene service instance.</returns>
        public static SceneRuntimeService EnsureRuntimeInstance()
        {
            if (_instance != null)
                return _instance;

            GameObject runtimeRoot = new GameObject("[SceneRuntimeService]");
            SceneRuntimeService sceneService = runtimeRoot.AddComponent<SceneRuntimeService>();
            return sceneService;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        /// <summary>
        /// Explicitly initializes the service and registers it into <see cref="GlobalRegistry"/>.
        /// </summary>
        public void InitializeService()
        {
            if (_isInitialized)
            {
                TryRegisterUpdatable();
                TryRegisterSceneService();
                TryRegisterSceneCallbacks();
                return;
            }

            _isInitialized = true;
            TryRegisterUpdatable();
            TryRegisterSceneService();
            TryRegisterSceneCallbacks();
        }

        /// <summary>
        /// Performs a guarded scene transition after clearing registry state.
        /// </summary>
        /// <param name="sceneName">Build-settings scene name.</param>
        public void LoadScene(string sceneName)
        {
            if (_sceneLoadInFlight)
            {
                Debug.LogWarning($"[SceneRuntimeService] Scene load '{sceneName}' rejected because '{_pendingSceneName}' is already in flight.");
                return;
            }

            _ = LoadSceneAsync(sceneName);
        }

        /// <inheritdoc />
        public async Awaitable LoadSceneAsync(string sceneName)
        {
            if (!CanLoadScene)
            {
                Debug.LogError($"[SceneRuntimeService] Scene load '{sceneName}' rejected while bootstrap is incomplete.");
                return;
            }

            if (_sceneLoadInFlight)
            {
                Debug.LogWarning($"[SceneRuntimeService] Scene load '{sceneName}' rejected because '{_pendingSceneName}' is already in flight.");
                return;
            }

            try
            {
                _sceneLoadInFlight = true;
                _pendingSceneName = sceneName;
                ClearRuntimeState();

                AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                if (loadOperation == null)
                {
                    Debug.LogError($"[SceneRuntimeService] Scene load '{sceneName}' failed to create an AsyncOperation.");
                    return;
                }

                _pendingSceneLoadOperation = loadOperation;
                _pendingSceneLoadOperation.allowSceneActivation = false;
                int waitFrames = 0;
                int nextWatchdogFrame = SceneActivationWatchdogInitialFrames;

                while (Application.isPlaying && ReferenceEquals(_instance, this) && !_pendingSceneLoadOperation.isDone)
                {
                    bool loadReady = _pendingSceneLoadOperation.progress >= 0.9f;
                    bool poolsReady = loadReady && ArePersistentWorldPoolsReadyForSceneActivation();
                    bool originStable = poolsReady && IsFloatingOriginStableForSceneActivation();

                    if (loadReady && poolsReady && originStable)
                    {
                        _pendingSceneLoadOperation.allowSceneActivation = true;
                    }
                    else if (waitFrames >= nextWatchdogFrame)
                    {
                        LogSceneActivationWatchdog(sceneName, _pendingSceneLoadOperation.progress, loadReady, poolsReady, originStable, waitFrames);
                        nextWatchdogFrame = waitFrames + SceneActivationWatchdogRepeatFrames;
                    }

                    waitFrames++;
                    await Awaitable.NextFrameAsync(cancellationToken: destroyCancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _sceneLoadInFlight = false;
                _pendingSceneName = null;
                _pendingSceneLoadOperation = null;
            }
        }

        /// <summary>
        /// Core-lane dispatcher hook required by the runtime registry contract.
        /// </summary>
        /// <param name="deltaTime">Scaled frame delta supplied by the dispatcher.</param>
        public void Tick(float deltaTime)
        {
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            if (Application.isPlaying)
            {
                if (transform.parent != null)
                    transform.SetParent(null, true);

                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnEnable()
        {
            TryRegisterUpdatable();
            if (_isInitialized)
            {
                TryRegisterSceneService();
                TryRegisterSceneCallbacks();
            }
        }

        private void OnDisable()
        {
            TryUnregisterUpdatable();
            TryUnregisterSceneCallbacks();
            TryUnregisterSceneService();
        }

        private void OnDestroy()
        {
            TryUnregisterUpdatable();
            TryUnregisterSceneCallbacks();
            TryUnregisterSceneService();
            _isInitialized = false;

            if (_instance == this)
                _instance = null;
        }

        private static void HandleSceneUnloaded(Scene scene)
        {
            ClearRuntimeState();
        }

        private static void ClearRuntimeState()
        {
            GlobalRegistry.ClearRuntimeBuckets();
            ThreadSafeCommandQueue.Clear();

            if (GlobalRegistry.Physics != null)
                GlobalRegistry.Physics.ClearQueuedPackets();
            else
                PhysicsApplySystem.ClearQueuedPacketsStatic();

            if (GlobalRegistry.InteractionSignals != null)
                GlobalRegistry.InteractionSignals.ClearQueuedSignals();

            if (GlobalRegistry.Debris != null)
                GlobalRegistry.Debris.ClearActiveDebris();

            GlobalPhysicsStateManager.ClearRuntimeStateStatic();
        }

        private static bool ArePersistentWorldPoolsReadyForSceneActivation()
        {
            if (!GameBootstrapper.ArePreWarmAssetsReady)
                return false;

            PersistentWorldRegistry registry = PersistentWorldRegistry.Instance;
            if (registry == null)
                return false;

            return registry.AreResidentWorldPrefabPoolsReady();
        }

        private static bool IsFloatingOriginStableForSceneActivation()
        {
            return !HectonFloatingOrigin.IsShiftInProgress &&
                   !HectonFloatingOrigin.IsPhysicsPausedForShift;
        }

        private static void LogSceneActivationWatchdog(
            string sceneName,
            float progress,
            bool loadReady,
            bool poolsReady,
            bool originStable,
            int waitFrames)
        {
            string blockedBy = GetSceneActivationBlockedReason(loadReady, poolsReady, originStable);
            Debug.LogError($"[SceneRuntimeService] Scene load '{sceneName}' still blocked after {waitFrames} frames. Reason: {blockedBy}. Progress: {progress:0.00}.");
        }

        private static string GetSceneActivationBlockedReason(bool loadReady, bool poolsReady, bool originStable)
        {
            if (!loadReady)
                return "AsyncOperation progress below activation threshold";
            if (!poolsReady)
                return "PersistentWorldRegistry pools not ready";
            if (!originStable)
                return "floating origin shift in progress";

            return "activation pending";
        }

        private void TryRegisterUpdatable()
        {
            if (_registeredUpdatable || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Core);
            _registeredUpdatable = true;
        }

        private void TryUnregisterUpdatable()
        {
            if (!_registeredUpdatable)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            _registeredUpdatable = false;
        }

        private void TryRegisterSceneService()
        {
            if (_registeredSceneService)
                return;

            GlobalRegistry.RegisterSceneService(this);
            _registeredSceneService = true;
        }

        private void TryUnregisterSceneService()
        {
            if (!_registeredSceneService)
                return;

            GlobalRegistry.UnregisterSceneService(this);
            _registeredSceneService = false;
        }

        private void TryRegisterSceneCallbacks()
        {
            if (_registeredSceneCallbacks)
                return;

            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            _registeredSceneCallbacks = true;
        }

        private void TryUnregisterSceneCallbacks()
        {
            if (!_registeredSceneCallbacks)
                return;

            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            _registeredSceneCallbacks = false;
        }
    }
}
