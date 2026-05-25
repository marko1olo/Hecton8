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
    public sealed class OceanKinematicsRuntimeService : MonoBehaviour, IHectonOceanKinematicsService, IUpdatable, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        private const int ProviderCapacity = 4;

        // COLD ALLOC: object[4] - registered ocean-kinematics providers ordered by runtime priority, object-backed to avoid interface collections - owner: OceanKinematicsRuntimeService
        private readonly object[] _providers = new object[ProviderCapacity];

        private bool _isInitialized;
        private bool _registeredUpdatable;
        private bool _registeredService;
        private bool _hotSwapRegistered;
        private int _providerCount;
        private IHectonOceanKinematics _activeProvider;

        /// <inheritdoc />
        public bool IsInitialized => _isInitialized;

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => _isInitialized ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        /// <inheritdoc />
        public bool IsServiceReady => _isInitialized;

        /// <inheritdoc />
        public IHectonOceanKinematics ActiveProvider => _activeProvider;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            GlobalRegistry.ClearOceanKinematicsRuntime(null);
        }

        /// <summary>
        /// Ensures a live runtime instance exists.
        /// </summary>
        public static OceanKinematicsRuntimeService EnsureRuntimeInstance()
        {
            OceanKinematicsRuntimeService runtime = GlobalRegistry.OceanKinematicsRuntime;
            if (runtime != null)
                return runtime;

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
                TryRegisterHotSwapListener();
                TryRegisterUpdatable();
                TryRegisterService();
                RefreshActiveProvider();
                return;
            }

            EnsureSingletonOwnership();
            if (GlobalRegistry.OceanKinematicsRuntime != this)
                return;

            _isInitialized = true;
            TryRegisterHotSwapListener();
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
            OceanKinematicsRuntimeService runtime = GlobalRegistry.OceanKinematicsRuntime;
            if (runtime == null)
                return;

            runtime.UnregisterProviderInternal(provider);
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
                TryRegisterHotSwapListener();
                TryRegisterUpdatable();
                TryRegisterService();
                RefreshActiveProvider();
            }
        }

        private void OnDisable()
        {
            TryUnregisterUpdatable();
            TryUnregisterHotSwapListener();
            TryUnregisterService();
            _activeProvider = null;
        }

        private void OnDestroy()
        {
            ShutdownServiceState();
        }

        public void OnServiceShutdown()
        {
            ShutdownServiceState();
        }

        private void ShutdownServiceState()
        {
            TryUnregisterUpdatable();
            TryUnregisterHotSwapListener();
            TryUnregisterService();
            _isInitialized = false;
            System.Array.Clear(_providers, 0, _providerCount);
            _providerCount = 0;
            _activeProvider = null;

            GlobalRegistry.ClearOceanKinematicsRuntime(this);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher ||
                currentService == null ||
                !_isInitialized ||
                !isActiveAndEnabled)
            {
                return;
            }

            TryUnregisterUpdatable();
            TryRegisterUpdatable();
        }

        private void RegisterProviderInternal(IHectonOceanKinematics provider)
        {
            if (provider == null || IndexOfProvider(provider) >= 0)
                return;

            if (_providerCount >= ProviderCapacity)
            {
                LogProviderCapacityExceeded();
                return;
            }

            _providers[_providerCount] = provider;
            _providerCount++;
            RefreshActiveProvider();
        }

        private void UnregisterProviderInternal(IHectonOceanKinematics provider)
        {
            if (provider == null)
                return;

            int index = IndexOfProvider(provider);
            if (index < 0)
                return;

            RemoveProviderAt(index);
            RefreshActiveProvider();
        }

        private void EnsureSingletonOwnership()
        {
            OceanKinematicsRuntimeService runtime = GlobalRegistry.OceanKinematicsRuntime;
            if (runtime != null && runtime != this)
            {
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterOceanKinematicsRuntime(this);
        }

        private void RefreshActiveProvider()
        {
            IHectonOceanKinematics bestAvailableProvider = null;
            int bestAvailablePriority = int.MinValue;
            IHectonOceanKinematics bestFallbackProvider = null;
            int bestFallbackPriority = int.MinValue;

            for (int i = _providerCount - 1; i >= 0; i--)
            {
                IHectonOceanKinematics candidate = _providers[i] as IHectonOceanKinematics;
                if (candidate == null)
                {
                    RemoveProviderAt(i);
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

        private int IndexOfProvider(IHectonOceanKinematics provider)
        {
            for (int i = 0; i < _providerCount; i++)
            {
                if (ReferenceEquals(_providers[i], provider))
                    return i;
            }

            return -1;
        }

        private void RemoveProviderAt(int index)
        {
            _providerCount--;
            if (index < _providerCount)
                _providers[index] = _providers[_providerCount];

            _providers[_providerCount] = null;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogProviderCapacityExceeded()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[OceanKinematicsRuntimeService] Provider capacity exceeded. capacity=" + ProviderCapacity);
#endif
        }

        private void TryRegisterUpdatable()
        {
            if (_registeredUpdatable || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registeredUpdatable = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core);
        }

        private void TryUnregisterUpdatable()
        {
            if (!_registeredUpdatable)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            _registeredUpdatable = false;
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

        private void TryRegisterService()
        {
            if (_registeredService)
                return;

            GlobalRegistry.RegisterOceanKinematicsService(this);
            _registeredService = ReferenceEquals(GlobalRegistry.OceanKinematics, this);
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
