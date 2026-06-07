using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Dispatcher-driven 3D analog needle with a cheap elastic overshoot response.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Analog Gauge Needle 3D")]
    public sealed class AnalogGaugeNeedle3D : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const float AngleWriteEpsilonDegrees = 0.001f;
        private const float SettleEpsilonDegrees = 0.01f;
        private const float DegreesToRadians = 0.01745329252f;
        private const float TwoPi = 6.28318530718f;
        private const float HalfPi = 1.57079632679f;
        private const float Pi = 3.14159265359f;

        private enum NeedleAxis : byte
        {
            X = 0,
            Y = 1,
            Z = 2
        }

        [SerializeField] private Transform needle = null;
        [SerializeField] private NeedleAxis rotationAxis = NeedleAxis.Z;
        [SerializeField] private float minimumValue = 0f;
        [SerializeField] private float maximumValue = 1f;
        [SerializeField] private float minimumAngleDegrees = -120f;
        [SerializeField] private float maximumAngleDegrees = 120f;
        [SerializeField, Min(0.1f)] private float springFrequencyHz = 4.5f;
        [SerializeField, Range(0.05f, 2f)] private float dampingRatio = 0.58f;
        [SerializeField, Range(0f, 0.5f)] private float overshootKickStrength = 0.14f;
        [SerializeField, Range(0f, 180f)] private float maxOvershootKickDegreesPerSecond = 48f;
        [SerializeField, Range(0f, 1f)] private float target01 = 0f;

        private bool _registeredLateFrame;
        private bool _initialized;
        private Quaternion _initialLocalRotation;
        private float3 _rotationAxisVector;
        private float _currentAngle;
        private float _overshootAngle;
        private float _lastTargetAngle;
        private float _lastAppliedAngle;
        private bool _rotationApplied;
        private bool _hotSwapListenerRegistered;

        private void OnEnable()
        {
            CaptureInitialState();
            TryRegisterHotSwapListener();
            TryRegisterTickManager();
        }

        private void Start()
        {
            CaptureInitialState();
            TryRegisterHotSwapListener();
            TryRegisterTickManager();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            TryUnregisterTickManager();
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            TryUnregisterTickManager();
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            if (needle == null)
                return;

            CaptureInitialState();
            float dt = math.max(0f, SystemDispatcher.CurrentFrameDeltaTime);
            if (dt <= 0f)
                return;

            float targetAngle = math.lerp(minimumAngleDegrees, maximumAngleDegrees, math.saturate(target01));
            float targetDelta = targetAngle - _lastTargetAngle;
            if (math.abs(targetDelta) > 0.001f && overshootKickStrength > 0f)
            {
                float maxKick = math.max(0f, maxOvershootKickDegreesPerSecond);
                _overshootAngle = math.clamp(targetDelta * math.max(0f, overshootKickStrength), -maxKick, maxKick);
                _lastTargetAngle = targetAngle;
            }

            float blend = FastDecayBlend(math.max(0.1f, springFrequencyHz) * math.max(0.1f, dampingRatio), dt);
            float elasticBlend = EvaluateElasticOut(blend);
            float animatedTarget = targetAngle + _overshootAngle;
            _currentAngle = math.lerp(_currentAngle, animatedTarget, elasticBlend);
            _overshootAngle = math.lerp(
                _overshootAngle,
                0f,
                FastDecayBlend(math.max(0.1f, springFrequencyHz) * 2.5f, dt));

            ApplyNeedleRotationIfChanged();
            if (IsNeedleSettled(targetAngle))
            {
                _currentAngle = targetAngle;
                _overshootAngle = 0f;
                ApplyNeedleRotationIfChanged();
            }
        }

        public void SetTarget01(float normalizedValue)
        {
            float nextTarget01 = math.saturate(normalizedValue);
            if (math.abs(target01 - nextTarget01) <= 0.0001f)
                return;

            target01 = nextTarget01;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            TryUnregisterTickManager();
            if (currentService != null && isActiveAndEnabled)
                TryRegisterTickManager();
        }

        public void SetTargetValue(float value)
        {
            float denominator = maximumValue - minimumValue;
            if (math.abs(denominator) <= 0.0001f)
            {
                SetTarget01(0f);
                return;
            }

            SetTarget01((value - minimumValue) / denominator);
        }

        public void SnapToTarget()
        {
            CaptureInitialState();
            _currentAngle = math.lerp(minimumAngleDegrees, maximumAngleDegrees, math.saturate(target01));
            _lastTargetAngle = _currentAngle;
            _overshootAngle = 0f;
            if (needle != null)
                ApplyNeedleRotation(force: true);
        }

        private void CaptureInitialState()
        {
            if (_initialized || needle == null)
                return;

            _initialLocalRotation = needle.localRotation;
            _rotationAxisVector = ResolveAxisFloat3(rotationAxis);
            _currentAngle = math.lerp(minimumAngleDegrees, maximumAngleDegrees, math.saturate(target01));
            _lastTargetAngle = _currentAngle;
            _overshootAngle = 0f;
            _rotationApplied = false;
            _initialized = true;
        }

        private void ApplyNeedleRotationIfChanged()
        {
            if (_rotationApplied && math.abs(_lastAppliedAngle - _currentAngle) <= AngleWriteEpsilonDegrees)
                return;

            ApplyNeedleRotation(force: true);
        }

        private void ApplyNeedleRotation(bool force)
        {
            if (!force || needle == null)
                return;

            quaternion baseRotation = new quaternion(
                _initialLocalRotation.x,
                _initialLocalRotation.y,
                _initialLocalRotation.z,
                _initialLocalRotation.w);
            quaternion deltaRotation = ApproximateRotationDegreesNoTrig(_rotationAxisVector, _currentAngle);
            quaternion resolvedRotation = math.mul(baseRotation, deltaRotation);
            needle.localRotation = new Quaternion(
                resolvedRotation.value.x,
                resolvedRotation.value.y,
                resolvedRotation.value.z,
                resolvedRotation.value.w);
            _lastAppliedAngle = _currentAngle;
            _rotationApplied = true;
        }

        private void TryRegisterTickManager()
        {
            if (_registeredLateFrame || !Application.isPlaying)
                return;

            if (needle == null)
                return;

            _registeredLateFrame = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private void TryUnregisterTickManager()
        {
            if (_registeredLateFrame)
            {
                SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
                _registeredLateFrame = false;
            }

        }

        private bool IsNeedleSettled(float targetAngle)
        {
            return math.abs(_currentAngle - targetAngle) <= SettleEpsilonDegrees &&
                   math.abs(_overshootAngle) <= SettleEpsilonDegrees;
        }

        private static float3 ResolveAxisFloat3(NeedleAxis axis)
        {
            switch (axis)
            {
                case NeedleAxis.X:
                    return new float3(1f, 0f, 0f);
                case NeedleAxis.Y:
                    return new float3(0f, 1f, 0f);
                default:
                    return new float3(0f, 0f, 1f);
            }
        }

        private static quaternion ApproximateRotationDegreesNoTrig(float3 axis, float angleDegrees)
        {
            ApproximateSinCosFullNoTrig(math.select(0f, angleDegrees, math.isfinite(angleDegrees)) * DegreesToRadians * 0.5f, out float sinHalf, out float cosHalf);
            float3 safeAxis = math.normalizesafe(axis, new float3(0f, 0f, 1f));
            quaternion rotation = new quaternion(new float4(safeAxis * sinHalf, cosHalf));
            float lengthSq = math.lengthsq(rotation.value);
            return math.isfinite(lengthSq) && lengthSq > 0.000001f
                ? new quaternion(rotation.value * math.rsqrt(lengthSq))
                : quaternion.identity;
        }

        private static void ApproximateSinCosFullNoTrig(float radians, out float sin, out float cos)
        {
            float x = radians - (TwoPi * math.round(radians / TwoPi));
            float cosSign = 1f;
            if (x > HalfPi)
            {
                x = Pi - x;
                cosSign = -1f;
            }
            else if (x < -HalfPi)
            {
                x = -Pi - x;
                cosSign = -1f;
            }

            float x2 = x * x;
            sin = x * (1f - (x2 * (0.16666667f - (x2 * 0.008333333f))));
            cos = cosSign * (1f - (x2 * (0.5f - (x2 * 0.041666667f))));
        }

        private static float FastDecayBlend(float speed, float deltaTime)
        {
            float x = math.max(0f, speed) * math.max(0f, deltaTime);
            if (x >= 3.5f)
                return 1f;

            return math.saturate((12f * x) / (12f + (6f * x) + (x * x)));
        }

        private static float EvaluateElasticOut(float t)
        {
            t = math.saturate(t);
            float inverse = t - 1f;
            return math.saturate(1f + (2.70158f * inverse * inverse * inverse) + (1.70158f * inverse * inverse));
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            springFrequencyHz = math.max(0.1f, springFrequencyHz);
            dampingRatio = math.clamp(dampingRatio, 0.05f, 2f);
            overshootKickStrength = math.clamp(overshootKickStrength, 0f, 0.5f);
            maxOvershootKickDegreesPerSecond = math.clamp(maxOvershootKickDegreesPerSecond, 0f, 180f);
            target01 = math.saturate(target01);
            _rotationAxisVector = ResolveAxisFloat3(rotationAxis);
        }
#endif
    }
}
