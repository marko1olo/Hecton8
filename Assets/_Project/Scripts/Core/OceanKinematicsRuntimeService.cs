using Hecton8.Core.Contracts;
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
        private const float ProviderAvailabilityProbeIntervalSeconds = 0.5f;

        // COLD ALLOC: object[4] - registered ocean-kinematics providers ordered by runtime priority, object-backed to avoid interface collections - owner: OceanKinematicsRuntimeService
        private readonly object[] _providers = new object[ProviderCapacity];

        private bool _isInitialized;
        private bool _registeredUpdatable;
        private bool _registeredService;
        private bool _hotSwapRegistered;
        private bool _runtimeOwnerAborted;
        private int _providerCount;
        private IHectonOceanKinematics _activeProvider;
        private bool _providerRefreshRequested = true;
        private float _providerAvailabilityProbeCountdown;

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
            OceanKinematicsRuntimeService runtime = ResolveUsableRuntime();
            if (runtime != null)
                return runtime;

            IHectonOceanKinematicsService registeredService = GlobalRegistry.OceanKinematics;
            if (IsOceanKinematicsServiceUsable(registeredService) &&
                ReferenceEquals(registeredService as OceanKinematicsRuntimeService, null))
            {
                return null;
            }

            // Player-build construction path: no authored/bootstrap instance reachable.
            // Crest/plugin kinematics selection registers through this owner; without create,
            // GlobalRegistry.OceanKinematics stays null and world load/kinematics degrade.
            GameObject runtimeRoot = new GameObject("[OceanKinematicsRuntimeService]"); // COLD ALLOC: GameObject[1] - bootstrap-owned ocean kinematics selector root - owner: OceanKinematicsRuntimeService
            return runtimeRoot.AddComponent<OceanKinematicsRuntimeService>();
        }


        /// <summary>
        /// Explicitly initializes the service and registers it into <see cref="GlobalRegistry"/>.
        /// </summary>
        public void InitializeService()
        {
            if (_runtimeOwnerAborted || !EnsureSingletonOwnership())
                return;

            if (_isInitialized)
            {
                if (!TryRegisterService())
                    return;

                TryRegisterHotSwapListener();
                TryRegisterUpdatable();
                RefreshActiveProvider();
                return;
            }

            if (!TryRegisterService())
                return;

            _isInitialized = true;
            TryRegisterHotSwapListener();
            TryRegisterUpdatable();
            RefreshActiveProvider();
        }

        /// <summary>
        /// Registers one provider candidate with the runtime selector.
        /// </summary>
        public static void RegisterProvider(IHectonOceanKinematics provider)
        {
            OceanKinematicsRuntimeService runtime = EnsureRuntimeInstance();
            if (runtime == null)
                return;

            runtime.RegisterProviderInternal(provider);
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
            if (!_providerRefreshRequested)
            {
                _providerAvailabilityProbeCountdown -= deltaTime > 0f ? deltaTime : 0f;
                if (_providerAvailabilityProbeCountdown > 0f)
                    return;
            }

            RefreshActiveProvider();
        }

        private void Awake()
        {
            EnsureSingletonOwnership();
        }

        private void OnEnable()
        {
            if (_runtimeOwnerAborted)
                return;

            if (_isInitialized)
            {
                if (!EnsureSingletonOwnership())
                    return;

                if (!TryRegisterService())
                    return;

                TryRegisterHotSwapListener();
                TryRegisterUpdatable();
                RefreshActiveProvider();
            }
        }

        private void OnDisable()
        {
            if (_runtimeOwnerAborted)
                return;

            TryUnregisterUpdatable();
            TryUnregisterHotSwapListener();
            TryUnregisterService();
            _activeProvider = null;
            _providerRefreshRequested = true;
            _providerAvailabilityProbeCountdown = 0f;
        }

        private void OnDestroy()
        {
            if (_runtimeOwnerAborted)
                return;

            ShutdownServiceState();
        }

        public void OnServiceShutdown()
        {
            ShutdownServiceState();
        }

        private void ShutdownServiceState()
        {
            if (_runtimeOwnerAborted)
                return;

            TryUnregisterUpdatable();
            TryUnregisterHotSwapListener();
            TryUnregisterService();
            _isInitialized = false;
            System.Array.Clear(_providers, 0, _providerCount);
            _providerCount = 0;
            _activeProvider = null;
            _providerRefreshRequested = true;
            _providerAvailabilityProbeCountdown = 0f;

            GlobalRegistry.ClearOceanKinematicsRuntime(this);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (_runtimeOwnerAborted)
                return;

            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            TryUnregisterUpdatable();
            if (currentService != null &&
                _isInitialized &&
                isActiveAndEnabled)
            {
                TryRegisterUpdatable();
                RequestProviderRefresh();
            }
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

        private bool EnsureSingletonOwnership()
        {
            if (_runtimeOwnerAborted)
                return false;

            if (TryAbortForUsableExistingRuntime())
                return false;

            OceanKinematicsRuntimeService runtime = GlobalRegistry.OceanKinematicsRuntime;
            if (!ReferenceEquals(runtime, null) && !ReferenceEquals(runtime, this))
            {
                GlobalRegistry.ClearOceanKinematicsRuntime(runtime);
                runtime._registeredService = false;
                runtime._isInitialized = false;
            }

            if (TryAbortForUsableExistingRuntime())
                return false;

            GlobalRegistry.RegisterOceanKinematicsRuntime(this);
            return ReferenceEquals(GlobalRegistry.OceanKinematicsRuntime, this);
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
            _providerRefreshRequested = false;
            _providerAvailabilityProbeCountdown = ProviderAvailabilityProbeIntervalSeconds;
        }

        private void RequestProviderRefresh()
        {
            _providerRefreshRequested = true;
            _providerAvailabilityProbeCountdown = 0f;
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
            Hecton8.Core.H8Debug.LogError("[OceanKinematicsRuntimeService] Provider capacity exceeded. capacity=" + ProviderCapacity);
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

        private bool TryRegisterService()
        {
            if (_runtimeOwnerAborted)
                return false;

            if (_registeredService)
                return true;

            if (TryAbortForUsableExistingRuntime())
                return false;

            IHectonOceanKinematicsService registeredService = GlobalRegistry.OceanKinematics;
            if (!ReferenceEquals(registeredService, null) && !ReferenceEquals(registeredService, this))
            {
                OceanKinematicsRuntimeService staleRuntime = registeredService as OceanKinematicsRuntimeService;
                if (ReferenceEquals(staleRuntime, null))
                {
                    _runtimeOwnerAborted = true;
                    Destroy(gameObject);
                    return false;
                }

                GlobalRegistry.UnregisterOceanKinematicsService(registeredService);
                GlobalRegistry.ClearOceanKinematicsRuntime(staleRuntime);
                staleRuntime._registeredService = false;
                staleRuntime._isInitialized = false;
            }

            if (TryAbortForUsableExistingRuntime())
                return false;

            GlobalRegistry.RegisterOceanKinematicsService(this);
            _registeredService = ReferenceEquals(GlobalRegistry.OceanKinematics, this);
            _runtimeOwnerAborted = !_registeredService;
            if (_runtimeOwnerAborted)
                Destroy(gameObject);
            return _registeredService;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            OceanKinematicsRuntimeService runtime = GlobalRegistry.OceanKinematicsRuntime;
            if (!ReferenceEquals(runtime, null) && !ReferenceEquals(runtime, this))
            {
                if (IsOceanKinematicsRuntimeUsable(runtime))
                {
                    _runtimeOwnerAborted = true;
                    Destroy(gameObject);
                    return true;
                }

                GlobalRegistry.ClearOceanKinematicsRuntime(runtime);
                runtime._registeredService = false;
                runtime._isInitialized = false;
            }

            IHectonOceanKinematicsService registeredService = GlobalRegistry.OceanKinematics;
            if (ReferenceEquals(registeredService, null) || ReferenceEquals(registeredService, this))
                return false;

            if (IsOceanKinematicsServiceUsable(registeredService))
            {
                _runtimeOwnerAborted = true;
                Destroy(gameObject);
                return true;
            }

            OceanKinematicsRuntimeService staleRuntime = registeredService as OceanKinematicsRuntimeService;
            if (!ReferenceEquals(staleRuntime, null))
            {
                GlobalRegistry.UnregisterOceanKinematicsService(registeredService);
                GlobalRegistry.ClearOceanKinematicsRuntime(staleRuntime);
                staleRuntime._registeredService = false;
                staleRuntime._isInitialized = false;
            }

            return false;
        }

        private static OceanKinematicsRuntimeService ResolveUsableRuntime()
        {
            OceanKinematicsRuntimeService runtime = GlobalRegistry.OceanKinematicsRuntime;
            if (IsOceanKinematicsRuntimeUsable(runtime))
                return runtime;

            if (!ReferenceEquals(runtime, null))
            {
                GlobalRegistry.ClearOceanKinematicsRuntime(runtime);
                runtime._registeredService = false;
                runtime._isInitialized = false;
            }

            IHectonOceanKinematicsService registeredService = GlobalRegistry.OceanKinematics;
            if (IsOceanKinematicsServiceUsable(registeredService))
                return registeredService as OceanKinematicsRuntimeService;

            OceanKinematicsRuntimeService staleRuntime = registeredService as OceanKinematicsRuntimeService;
            if (!ReferenceEquals(staleRuntime, null))
            {
                GlobalRegistry.UnregisterOceanKinematicsService(registeredService);
                GlobalRegistry.ClearOceanKinematicsRuntime(staleRuntime);
                staleRuntime._registeredService = false;
                staleRuntime._isInitialized = false;
            }

            return null;
        }

        private static bool IsOceanKinematicsServiceUsable(IHectonOceanKinematicsService service)
        {
            if (ReferenceEquals(service, null))
                return false;

            OceanKinematicsRuntimeService runtime = service as OceanKinematicsRuntimeService;
            return ReferenceEquals(runtime, null) ||
                   (runtime._registeredService && IsOceanKinematicsRuntimeUsable(runtime));
        }

        private static bool IsOceanKinematicsRuntimeUsable(OceanKinematicsRuntimeService runtime)
        {
            return runtime != null &&
                   runtime.isActiveAndEnabled &&
                   !runtime._runtimeOwnerAborted;
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
