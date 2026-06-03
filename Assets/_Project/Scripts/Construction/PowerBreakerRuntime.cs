using System;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Power
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PowerNode))]
    [RequireComponent(typeof(BreakerMetadata))]
    [AddComponentMenu("Hecton8/Power/Power Breaker Runtime")]
    public sealed class PowerBreakerRuntime : MonoBehaviour, ILateFrameTickable, IPoolable, IGlobalRegistryHotSwapListener
    {
        private const int MinimumVisualCadenceFrames = 1;
        private const int MaximumVisualCadenceFrames = 12;

        [SerializeField] private PowerNode powerNode;
        [SerializeField] private BreakerMetadata breakerMetadata;
        [SerializeField] private PowerStatusEmissiveBinding emissiveBinding;
        [SerializeField] private MonoBehaviour[] activationTargets = Array.Empty<MonoBehaviour>();
        [SerializeField] private bool startsClosed = true;

        private bool _closed;
        private bool _registeredLateFrame;
        private bool _registeredHotSwap;
        private bool _visualDirty = true;
        private float _queuedLoad01;
        private float _queuedFailure01;
        private float _queuedQualityWeight01 = -1f;
        private float _queuedPulsePhase01;

        public PowerNode Node => powerNode;
        public BreakerMetadata Metadata => breakerMetadata;
        public bool IsClosed => _closed;
        public int ActivationTargetCount => activationTargets != null ? activationTargets.Length : 0;
        public bool HasSerializedRuntimeBindings => powerNode != null && breakerMetadata != null && emissiveBinding != null;
        public bool HasValidActivationTargets
        {
            get
            {
                MonoBehaviour[] targets = activationTargets;
                int targetCount = targets != null ? targets.Length : 0;
                if (targetCount <= 0)
                    return powerNode != null;

                for (int i = 0; i < targetCount; i++)
                {
                    if (!(targets[i] is IPowerActivationTarget))
                        return false;
                }

                return true;
            }
        }

        private void Awake()
        {
            _closed = startsClosed;
        }

        private void OnEnable()
        {
            _visualDirty = true;
            ApplyAuthorityState();
            TryRegisterHotSwapListener();
            TryRegisterLateFrame();
        }

        private void OnDisable()
        {
            TryUnregisterLateFrame();
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            TryUnregisterLateFrame();
            TryUnregisterHotSwapListener();
        }

        public void OnSpawn()
        {
            _closed = startsClosed;
            _visualDirty = true;
            ApplyAuthorityState();
            TryRegisterHotSwapListener();
            TryRegisterLateFrame();
        }

        public void OnDespawn()
        {
            TryUnregisterLateFrame();
            TryUnregisterHotSwapListener();
            _closed = startsClosed;
            _visualDirty = true;
            _queuedLoad01 = 0f;
            _queuedFailure01 = 0f;
            _queuedQualityWeight01 = -1f;
            _queuedPulsePhase01 = 0f;
        }

#if UNITY_EDITOR
        public void ConfigureEditorBake(
            PowerNode node,
            BreakerMetadata metadata,
            PowerStatusEmissiveBinding statusBinding,
            bool defaultClosed,
            MonoBehaviour[] gatedTargets)
        {
            powerNode = node;
            breakerMetadata = metadata;
            emissiveBinding = statusBinding;
            activationTargets = gatedTargets ?? Array.Empty<MonoBehaviour>();
            startsClosed = defaultClosed;
            _closed = defaultClosed;
        }
#endif

        public void SetBreakerClosed(bool closed)
        {
            if (_closed == closed)
                return;

            _closed = closed;
            _visualDirty = true;
            ApplyAuthorityState();
        }

        public void ToggleBreaker()
        {
            SetBreakerClosed(!_closed);
        }

        public void SetBreakerClosed01(float closed01)
        {
            SetBreakerClosed(Sanitize01(closed01) >= 0.5f);
        }

        public void ApplyVisualSync(float load01, float failure01, float globalQualityWeight, float normalizedPulsePhase)
        {
            QueueVisualSync(load01, failure01, globalQualityWeight, normalizedPulsePhase);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            TryUnregisterLateFrame();
            if (!isActiveAndEnabled || currentService == null)
                return;

            _visualDirty = true;
            TryRegisterLateFrame();
        }

        public void QueueVisualSync(float load01, float failure01, float globalQualityWeight, float normalizedPulsePhase)
        {
            _queuedLoad01 = Sanitize01(load01);
            _queuedFailure01 = Sanitize01(failure01);
            _queuedQualityWeight01 = Sanitize01(globalQualityWeight);
            _queuedPulsePhase01 = Sanitize01(normalizedPulsePhase);
            _visualDirty = true;
        }

        public void LateFrameTick()
        {
            PowerStatusEmissiveBinding binding = emissiveBinding;
            if (binding == null)
                return;

            float quality = _queuedQualityWeight01 >= 0f
                ? _queuedQualityWeight01
                : SignalBusRegistry.GlobalQualityWeight01;
            quality = Sanitize01(quality);
            int cadenceFrames = ResolveVisualCadenceFrames(quality);
            uint frame = SystemDispatcher.CurrentFrameId;
            if (!_visualDirty && (frame % (uint)cadenceFrames) != 0u)
                return;

            _visualDirty = false;
            float closed01 = _closed ? 1f : 0f;
            float voltage01 = powerNode != null ? powerNode.Voltage01 : 1f;
            float load = math.max(_queuedLoad01, Sanitize01(voltage01)) * closed01;
            float failure = Sanitize01(_queuedFailure01 + (1f - closed01) + (1f - Sanitize01(voltage01)));
            float phase = _queuedPulsePhase01 > 0f ? _queuedPulsePhase01 : ResolvePulsePhase01(frame);
            binding.ApplyVisualSync(load, failure, quality, phase);
        }

        private void TryRegisterLateFrame()
        {
            if (_registeredLateFrame || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterLateFrame()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLateFrame = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private static float Sanitize01(float value)
        {
            return math.saturate(math.select(0f, value, math.isfinite(value)));
        }

        private static int ResolveVisualCadenceFrames(float quality)
        {
            float q = math.smoothstep(0f, 1f, Sanitize01(quality));
            return math.clamp(
                (int)math.round(math.lerp(MaximumVisualCadenceFrames, MinimumVisualCadenceFrames, q)),
                MinimumVisualCadenceFrames,
                MaximumVisualCadenceFrames);
        }

        private static float ResolvePulsePhase01(uint frame)
        {
            return math.frac((frame & 1023u) * 0.061803398f);
        }

        private void ApplyAuthorityState()
        {
            float activation01 = _closed ? 1f : 0f;
            bool applied = false;
            MonoBehaviour[] targets = activationTargets;
            int targetCount = targets != null ? targets.Length : 0;
            for (int i = 0; i < targetCount; i++)
            {
                MonoBehaviour target = targets[i];
                if (target is IPowerActivationTarget activationTarget)
                {
                    activationTarget.SetRuntimeActivation01(activation01);
                    applied = true;
                }
            }

            if (!applied && powerNode != null)
                powerNode.SetRuntimeActivation01(activation01);
        }
    }
}
