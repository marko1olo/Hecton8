using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Dispatcher-driven 3D analog needle with spring-damper lag.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Analog Gauge Needle 3D")]
    public sealed class AnalogGaugeNeedle3D : MonoBehaviour, ITickable, ILateFrameTickable
    {
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

        private bool _registeredToTick;
        private bool _registeredLateFrame;
        private bool _initialized;
        private Quaternion _initialLocalRotation;
        private float _currentAngle;
        private float _angularVelocity;
        private float _lastTargetAngle;
        private float _lastDeltaTime;

        private void OnEnable()
        {
            CaptureInitialState();
            TryRegisterTickManager();
        }

        private void Start()
        {
            CaptureInitialState();
            TryRegisterTickManager();
        }

        private void OnDisable()
        {
            TryUnregisterTickManager();
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            _lastDeltaTime = Mathf.Max(0f, deltaTime);
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            if (needle == null)
                return;

            CaptureInitialState();
            float dt = _lastDeltaTime;
            if (dt <= 0f)
                return;

            float targetAngle = math.lerp(minimumAngleDegrees, maximumAngleDegrees, math.saturate(target01));
            float targetDelta = targetAngle - _lastTargetAngle;
            if (math.abs(targetDelta) > 0.001f && overshootKickStrength > 0f)
            {
                float kick = targetDelta * math.max(0f, overshootKickStrength) / math.max(dt, 0.001f);
                float maxKick = math.max(0f, maxOvershootKickDegreesPerSecond);
                _angularVelocity += math.clamp(kick, -maxKick, maxKick);
                _lastTargetAngle = targetAngle;
            }

            float omega = math.max(0.1f, springFrequencyHz) * (Mathf.PI * 2f);
            float displacement = _currentAngle - targetAngle;
            float acceleration = (-omega * omega * displacement) - (2f * math.clamp(dampingRatio, 0.05f, 2f) * omega * _angularVelocity);
            _angularVelocity += acceleration * dt;
            _currentAngle += _angularVelocity * dt;

            needle.localRotation = _initialLocalRotation * Quaternion.AngleAxis(_currentAngle, ResolveAxisVector(rotationAxis));
        }

        public void SetTarget01(float normalizedValue)
        {
            target01 = math.saturate(normalizedValue);
        }

        public void SetTargetValue(float value)
        {
            float denominator = maximumValue - minimumValue;
            if (math.abs(denominator) <= 0.0001f)
            {
                target01 = 0f;
                return;
            }

            target01 = math.saturate((value - minimumValue) / denominator);
        }

        public void SnapToTarget()
        {
            CaptureInitialState();
            _currentAngle = math.lerp(minimumAngleDegrees, maximumAngleDegrees, math.saturate(target01));
            _lastTargetAngle = _currentAngle;
            _angularVelocity = 0f;
            if (needle != null)
                needle.localRotation = _initialLocalRotation * Quaternion.AngleAxis(_currentAngle, ResolveAxisVector(rotationAxis));
        }

        private void CaptureInitialState()
        {
            if (_initialized || needle == null)
                return;

            _initialLocalRotation = needle.localRotation;
            _currentAngle = math.lerp(minimumAngleDegrees, maximumAngleDegrees, math.saturate(target01));
            _lastTargetAngle = _currentAngle;
            _angularVelocity = 0f;
            _initialized = true;
        }

        private void TryRegisterTickManager()
        {
            if (_registeredToTick || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registeredToTick = GlobalRegistry.Updatables.Contains(this);
            GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.UI);
            _registeredLateFrame = SystemDispatcher.GetLateFrameLane(PriorityLayer.UI).Contains(this);
        }

        private void TryUnregisterTickManager()
        {
            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _registeredLateFrame = false;
            }

            if (_registeredToTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
                _registeredToTick = false;
            }
        }

        private static Vector3 ResolveAxisVector(NeedleAxis axis)
        {
            switch (axis)
            {
                case NeedleAxis.X:
                    return Vector3.right;
                case NeedleAxis.Y:
                    return Vector3.up;
                default:
                    return Vector3.forward;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            springFrequencyHz = math.max(0.1f, springFrequencyHz);
            dampingRatio = math.clamp(dampingRatio, 0.05f, 2f);
            overshootKickStrength = math.clamp(overshootKickStrength, 0f, 0.5f);
            maxOvershootKickDegreesPerSecond = math.clamp(maxOvershootKickDegreesPerSecond, 0f, 180f);
            target01 = math.saturate(target01);
        }
#endif
    }
}
