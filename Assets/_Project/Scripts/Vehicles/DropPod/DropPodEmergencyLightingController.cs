namespace Hecton8.Vehicles.DropPod
{
    using System;
    using Hecton8.Core;
    using Hecton8.Core.Contracts;
    using Hecton8.Core.Contracts.Signals;
    using Unity.Mathematics;
    using UnityEngine;

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Vehicles/Drop Pod/Emergency Lighting Controller")]
    public sealed class DropPodEmergencyLightingController : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private static readonly int EmergencyLedColorId = Shader.PropertyToID("_H8DropPodEmergencyLedColor");
        private static readonly int CabinLightWeightId = Shader.PropertyToID("_H8DropPodCabinLightWeight");
        private const float LightingApplyEpsilon = 0.0005f;
        private const float MaxLightIntensity = 8f;
        private const float IdleAlertLightWeight = 0f;
        private const float TransitAlertLightWeight = 0.45f;
        private const float ArmedAlertLightWeight = 0.7f;
        private const float FullAlertLightWeight = 1f;

        [Header("Lights")]
        [SerializeField] private Light[] cabinLights;
        [SerializeField] private Light[] emergencyLights;
        [SerializeField, Range(0f, 8f)] private float cabinBaseIntensity = 1.2f;
        [SerializeField, Range(0f, 8f)] private float emergencyBaseIntensity = 2.7f;
        [SerializeField] private Color emergencyColor = new Color(1f, 0.05f, 0.02f, 1f);
        [SerializeField, Range(0.05f, 8f)] private float transitionSharpness = 4.2f;

        private float _emergency01;
        private float _targetEmergency01;
        private float _lastAppliedEmergency01 = -1f;
        private double _lastLateTickTimeSeconds;
        private uint _lastStatusFrame;
        private ushort _lastStatusSequence;
        private bool _lightingDirty = true;
        private bool _registeredLate;
        private bool _registeredHotSwap;

        private void Awake()
        {
            DropPodSignalLaneBootstrap.EnsureConfigured();
        }

        private void OnEnable()
        {
            DropPodSignalLaneBootstrap.EnsureConfigured();
            ResetStatusCursor();
            TryRegisterHotSwapListener();
            bool lateRouteReady = TryRegisterLate();
            _lastLateTickTimeSeconds = SystemDispatcher.CurrentUnscaledTimeSeconds;
            _lightingDirty = true;
            DrainStatusSignals();
            if (Application.isPlaying && !lateRouteReady)
                MarkFailClosedPresentationFallback();
            _emergency01 = _targetEmergency01;
            ApplyLightingIfNeeded(_emergency01, true);
        }

        private void OnDisable()
        {
            ClearPresentationLighting();
            UnregisterLate();
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            ClearPresentationLighting();
            UnregisterLate();
            TryUnregisterHotSwapListener();
        }

        public void LateFrameTick()
        {
            DrainStatusSignals();
            float dt = ResolveLateDeltaSeconds();

            float settleDelta = math.abs(_emergency01 - _targetEmergency01);
            if (settleDelta <= LightingApplyEpsilon)
            {
                if (settleDelta > 0f)
                    _lightingDirty = true;

                _emergency01 = _targetEmergency01;
                ApplyLightingIfNeeded(_emergency01, false);
                return;
            }

            float sharpness = math.isfinite(transitionSharpness) ? math.max(0f, transitionSharpness) : 0f;
            float t = 1f - math.exp(-sharpness * dt);
            _emergency01 = math.lerp(_emergency01, _targetEmergency01, t);
            settleDelta = math.abs(_emergency01 - _targetEmergency01);
            if (settleDelta <= LightingApplyEpsilon)
            {
                if (settleDelta > 0f)
                    _lightingDirty = true;

                _emergency01 = _targetEmergency01;
            }

            ApplyLightingIfNeeded(_emergency01, false);
        }

        private void ResetStatusCursor()
        {
            _lastStatusFrame = 0u;
            _lastStatusSequence = 0;
        }

        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            UnregisterLate();
            if (!isActiveAndEnabled)
                return;

            if (currentService == null || !TryRegisterLate())
                MarkFailClosedPresentationFallback();
        }

        private void DrainStatusSignals()
        {
            ReadOnlySpan<DropPodStatusSignal> signals = SignalBus<DropPodStatusSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                DropPodStatusSignal signal = signals[i];
                if (!DropPodSignalLaneBootstrap.IsNewerSignal(signal.Frame, signal.Sequence, _lastStatusFrame, _lastStatusSequence))
                    continue;

                _lastStatusFrame = signal.Frame;
                _lastStatusSequence = signal.Sequence;
                DropPodStatusId status = (DropPodStatusId)signal.StatusId;
                switch (status)
                {
                    case DropPodStatusId.AirlockMoving:
                    case DropPodStatusId.SeatTransitActive:
                        SetTargetEmergency01(TransitAlertLightWeight);
                        break;
                    case DropPodStatusId.SeatTransitArmed:
                        SetTargetEmergency01(ArmedAlertLightWeight);
                        break;
                    case DropPodStatusId.AirlockSealed:
                    case DropPodStatusId.Seated:
                    case DropPodStatusId.EngineIgnitionArmed:
                    case DropPodStatusId.SeatBlockedAirlockOpen:
                    case DropPodStatusId.FailClosed:
                        SetTargetEmergency01(FullAlertLightWeight);
                        break;
                    case DropPodStatusId.AirlockOpen:
                    case DropPodStatusId.Idle:
                        SetTargetEmergency01(IdleAlertLightWeight);
                        break;
                }
            }
        }

        private void SetTargetEmergency01(float target)
        {
            float safeTarget = DropPodSplineMath.SanitizeUnit01(target);
            if (math.abs(_targetEmergency01 - safeTarget) <= LightingApplyEpsilon)
                return;

            _targetEmergency01 = safeTarget;
            _lightingDirty = true;
        }

        private void ApplyLightingIfNeeded(float emergency01, bool force)
        {
            float safeEmergency = DropPodSplineMath.SanitizeUnit01(emergency01);
            if (!force && !_lightingDirty && math.abs(safeEmergency - _lastAppliedEmergency01) <= LightingApplyEpsilon)
                return;

            ApplyLighting(safeEmergency);
            _lastAppliedEmergency01 = safeEmergency;
            _lightingDirty = false;
        }

        private float ResolveLateDeltaSeconds()
        {
            double now = SystemDispatcher.CurrentUnscaledTimeSeconds;
            float rawDeltaSeconds = (float)(now - _lastLateTickTimeSeconds);
            _lastLateTickTimeSeconds = now;
            return math.clamp(math.isfinite(rawDeltaSeconds) ? rawDeltaSeconds : 0f, 0f, 0.05f);
        }

        private void ClearPresentationLighting()
        {
            ApplyLighting(0f);
            _lastAppliedEmergency01 = 0f;
            _lightingDirty = true;
        }

        private void ApplyLighting(float emergency01)
        {
            float cabin01 = 1f - emergency01;
            ApplyLightArray(cabinLights, ResolveFiniteIntensity(cabinBaseIntensity) * cabin01);
            ApplyLightArray(emergencyLights, ResolveFiniteIntensity(emergencyBaseIntensity) * emergency01);
            Color led = Color.Lerp(Color.black, ResolveFiniteColor(emergencyColor), emergency01);
            Shader.SetGlobalColor(EmergencyLedColorId, led);
            Shader.SetGlobalFloat(CabinLightWeightId, cabin01);
        }

        private static float ResolveFiniteIntensity(float value)
        {
            return DropPodSplineMath.SanitizeRange(value, 0f, MaxLightIntensity, 0f);
        }

        private static Color ResolveFiniteColor(Color value)
        {
            if (!float.IsFinite(value.r) ||
                !float.IsFinite(value.g) ||
                !float.IsFinite(value.b) ||
                !float.IsFinite(value.a))
                return Color.black;

            return new Color(
                DropPodSplineMath.SanitizeUnit01(value.r),
                DropPodSplineMath.SanitizeUnit01(value.g),
                DropPodSplineMath.SanitizeUnit01(value.b),
                DropPodSplineMath.SanitizeUnit01(value.a));
        }

        private static void ApplyLightArray(Light[] lights, float intensity)
        {
            if (lights == null)
                return;

            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                if (light != null)
                    light.intensity = intensity;
            }
        }

        private bool TryRegisterLate()
        {
            if (_registeredLate || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return _registeredLate;

            _registeredLate = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
            return _registeredLate;
        }

        private void MarkFailClosedPresentationFallback()
        {
            SetTargetEmergency01(FullAlertLightWeight);
        }

        private void UnregisterLate()
        {
            if (!_registeredLate)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
            _registeredLate = false;
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
    }
}
