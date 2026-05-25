using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Core-owned registry bridge for the contracts-only procedural GPU instance culling runtime.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-87)]
    public sealed class InstanceCullingServiceRegistryBridge : MonoBehaviour, ITickable, IGlobalRegistryHotSwapListener
    {
        private static int s_x001InstanceCullingServiceRegistryBridgeSignalPushDropCount;
        private const int OverloadVisibleThreshold = 50000;
        private const uint SourceHash = 0xC0111A90u;

        [SerializeField]
        [Tooltip("Component implementing IInstanceCullingService. Kept as MonoBehaviour to avoid a Core to Graphics assembly dependency.")]
        private MonoBehaviour _serviceComponent;

        private IInstanceCullingService _service;
        private bool _serviceRegistered;
        private bool _tickRegistered;
        private bool _hotSwapRegistered;
        private uint _lastOverloadFrame = uint.MaxValue;

        private void OnEnable()
        {
            ResolveService();
            TryRegisterService();
            TryRegisterHotSwapListener();
            TryRegisterTick();
        }

        private void Start()
        {
            TryRegisterTick();
        }

        private void OnDisable()
        {
            TryUnregisterTick();
            TryUnregisterHotSwapListener();
            TryUnregisterService();
            _service = null;
        }

        private void OnDestroy()
        {
            TryUnregisterTick();
            TryUnregisterHotSwapListener();
            TryUnregisterService();
            _service = null;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher || currentService == null || !isActiveAndEnabled)
                return;

            TryUnregisterTick();
            TryRegisterTick();
        }

        public void Tick(float deltaTime)
        {
            IInstanceCullingService service = _service;
            if (service == null)
                return;

            while (service.TryConsumeTelemetry(out InstanceCullingTelemetry telemetry))
            {
                if (telemetry.VisibleInstances <= OverloadVisibleThreshold || telemetry.Frame == _lastOverloadFrame)
                    continue;

                _lastOverloadFrame = telemetry.Frame;
                CullingOverloadSignal signal = new CullingOverloadSignal
                {
                    VisibleInstances = telemetry.VisibleInstances,
                    CulledInstances = telemetry.CulledInstances,
                    SourceInstances = telemetry.SourceInstances,
                    Frame = telemetry.Frame,
                    CullDistanceMeters = telemetry.CullDistanceMeters,
                    VramUsedMb = telemetry.VramUsedMb,
                    Flags = telemetry.Flags,
                    SourceHash = SourceHash
                };
                SignalBus<CullingOverloadSignal>.TryPushTracked(in signal, ref s_x001InstanceCullingServiceRegistryBridgeSignalPushDropCount);
            }
        }

        private bool ResolveService()
        {
            if (_service != null)
                return true;

            _service = _serviceComponent as IInstanceCullingService;
            return _service != null;
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying || !ResolveService())
                return;

            GlobalRegistry.RegisterInstanceCullingService(_service);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.InstanceCulling, _service);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterInstanceCullingService(_service);
            _serviceRegistered = false;
        }

        private void TryRegisterTick()
        {
            if (_tickRegistered || !Application.isPlaying)
                return;

            _tickRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterTick()
        {
            if (!_tickRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _tickRegistered = false;
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
    }
}
