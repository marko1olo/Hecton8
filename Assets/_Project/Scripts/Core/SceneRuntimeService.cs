using Hecton8.Bootstrap;
using Hecton8.Physics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Core
{
    /// <summary>
    /// Guarded scene transition owner for GlobalRegistry cleanup and bootstrap gating.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9940)]
    public sealed class SceneRuntimeService : MonoBehaviour, ISceneService
    {
        private static SceneRuntimeService _instance;
        private bool _isInitialized;

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
                return;

            GlobalRegistry.RegisterSceneService(this);
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            _isInitialized = true;
        }

        /// <summary>
        /// Performs a guarded scene transition after clearing registry state.
        /// </summary>
        /// <param name="sceneName">Build-settings scene name.</param>
        public void LoadScene(string sceneName)
        {
            if (!CanLoadScene)
            {
                Debug.LogError($"[SceneRuntimeService] Scene load '{sceneName}' rejected while bootstrap is incomplete.");
                return;
            }

            ClearRuntimeState();
            SceneManager.LoadScene(sceneName);
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

        private void OnDestroy()
        {
            if (_isInitialized)
            {
                SceneManager.sceneUnloaded -= HandleSceneUnloaded;
                GlobalRegistry.UnregisterSceneService(this);
                _isInitialized = false;
            }

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
    }
}
