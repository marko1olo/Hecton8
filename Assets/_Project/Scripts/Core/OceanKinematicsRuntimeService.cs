using System.Collections.Generic;
using Hecton8.Physics;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Bootstrap-owned selector service for ocean-kinematics providers.
    /// Keeps provider arbitration out of gameplay controllers and away from third-party adapter types.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9926)]
    public sealed class OceanKinematicsRuntimeService : MonoBehaviour, IHectonOceanKinematicsService, IUpdatable
    {
        private static OceanKinematicsRuntimeService _instance;

        // COLD ALLOC: List<IHectonOceanKinematics>[4] - registered ocean-kinematics providers ordered by runtime priority - owner: OceanKinematicsRuntimeService
        private readonly List<IHectonOceanKinematics> _providers = new List<IHectonOceanKinematics>(4);

        private bool _isInitialized;
        private bool _registeredUpdatable;
        private bool _registeredService;
        private IHectonOceanKinematics _activeProvider;

        /// <inheritdoc />
        public bool IsInitialized => _isInitialized;

        /// <inheritdoc />
        public IHectonOceanKinematics ActiveProvider
        {
            get
            {
                RefreshActiveProvider();
                return _activeProvider;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        /// <summary>
        /// Ensures a live runtime instance exists.
        /// </summary>
        public static OceanKinematicsRuntimeService EnsureRuntimeInstance()
        {
            if (_instance != null)
                return _instance;

            GameObject runtimeRoot = new GameObject("[OceanKinematicsRuntimeService]"); // COLD ALLOC: GameObject[1] - bootstrap-owned ocean kinematics selector root - owner: OceanKinematicsRuntimeService
            return runtimeRoot.AddComponent<OceanKinematicsRuntimeService>();
        }

        /// <summary>
        /// Explicitly initializes the service and registers it into <see cref="GlobalRegistry"/>.
        /// </summary>
        public void InitializeService()
        {
            if (_isInitialized)
            {
                TryRegisterUpdatable();
                TryRegisterService();
                RefreshActiveProvider();
                return;
            }

            EnsureSingletonOwnership();
            if (_instance != this)
                return;

            if (Application.isPlaying)
            {
                if (transform.parent != null)
                    transform.SetParent(null, true);

                DontDestroyOnLoad(gameObject);
            }

            _isInitialized = true;
            TryRegisterUpdatable();
            TryRegisterService();
            RefreshActiveProvider();
        }

        /// <summary>
        /// Registers one provider candidate with the runtime selector.
        /// </summary>
        public static void RegisterProvider(IHectonOceanKinematics provider)
        {
            EnsureRuntimeInstance().RegisterProviderInternal(provider);
        }

        /// <summary>
        /// Removes one provider candidate from the runtime selector.
        /// </summary>
        public static void UnregisterProvider(IHectonOceanKinematics provider)
        {
            if (_instance == null)
                return;

            _instance.UnregisterProviderInternal(provider);
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            RefreshActiveProvider();
        }

        private void Awake()
        {
            EnsureSingletonOwnership();
        }

        private void OnEnable()
        {
            if (_isInitialized)
            {
                TryRegisterUpdatable();
                TryRegisterService();
                RefreshActiveProvider();
            }
        }

        private void OnDisable()
        {
            TryUnregisterUpdatable();
            TryUnregisterService();
            _activeProvider = null;
        }

        private void OnDestroy()
        {
            TryUnregisterUpdatable();
            TryUnregisterService();
            _providers.Clear();
            _activeProvider = null;

            if (_instance == this)
                _instance = null;
        }

        private void RegisterProviderInternal(IHectonOceanKinematics provider)
        {
            if (provider == null || _providers.Contains(provider))
                return;

            _providers.Add(provider);
            RefreshActiveProvider();
        }

        private void UnregisterProviderInternal(IHectonOceanKinematics provider)
        {
            if (provider == null)
                return;

            if (!_providers.Remove(provider))
                return;

            RefreshActiveProvider();
        }

        private void EnsureSingletonOwnership()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void RefreshActiveProvider()
        {
            IHectonOceanKinematics bestAvailableProvider = null;
            int bestAvailablePriority = int.MinValue;
            IHectonOceanKinematics bestFallbackProvider = null;
            int bestFallbackPriority = int.MinValue;

            for (int i = _providers.Count - 1; i >= 0; i--)
            {
                IHectonOceanKinematics candidate = _providers[i];
                if (candidate == null)
                {
                    _providers.RemoveAt(i);
                    continue;
                }

                int candidatePriority = candidate.Priority;
                if (candidatePriority > bestFallbackPriority)
                {
                    bestFallbackPriority = candidatePriority;
                    bestFallbackProvider = candidate;
                }

                if (!candidate.IsAvailable || candidatePriority <= bestAvailablePriority)
                    continue;

                bestAvailablePriority = candidatePriority;
                bestAvailableProvider = candidate;
            }

            _activeProvider = bestAvailableProvider ?? bestFallbackProvider;
        }

        private void TryRegisterUpdatable()
        {
            if (_registeredUpdatable)
                return;

            SystemDispatcher.EnsureRuntimeInstance();
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

        private void TryRegisterService()
        {
            if (_registeredService)
                return;

            GlobalRegistry.RegisterOceanKinematicsService(this);
            _registeredService = true;
        }

        private void TryUnregisterService()
        {
            if (!_registeredService)
                return;

            GlobalRegistry.UnregisterOceanKinematicsService(this);
            _registeredService = false;
        }
    }
}
