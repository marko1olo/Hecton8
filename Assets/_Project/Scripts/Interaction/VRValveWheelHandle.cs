using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Interaction
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Interaction/VR Valve Wheel Handle")]
    public sealed class VRValveWheelHandle : MonoBehaviour, IUpdatable
    {
        [Header("Valve")]
        [SerializeField] private Transform wheelVisual;
        [SerializeField] private Vector3 localRotationAxis = Vector3.forward;
        [SerializeField, Min(1f)] private float degreesToOpen = 360f;
        [SerializeField, Range(0f, 1f)] private float initialOpen01;
        [SerializeField, Range(1f, 180f)] private float maxAcceptedSampleDeltaDegrees = 80f;
        [SerializeField, Min(0f)] private float angularDragPerSecond = 7.5f;
        [SerializeField, Min(0f)] private float minimumMomentumDegreesPerSecond = 2f;

        private Quaternion _closedLocalRotation;
        private Vector3 _previousPlaneVector;
        private float _accumulatedDegrees;
        private float _angularVelocityDegreesPerSecond;
        private float _isOpen01;
        private bool _grabbed;
        private bool _hasPreviousVector;
        private bool _registeredMomentumTick;

        public float IsOpen01 => _isOpen01;
        public bool IsGrabbed => _grabbed;
        public float AngularVelocityDegreesPerSecond => _angularVelocityDegreesPerSecond;

        private void Awake()
        {
            Transform visual = ResolveVisual();
            _closedLocalRotation = visual.localRotation;
            SetOpen01Direct(initialOpen01);
        }

        public void BeginGrab(Vector3 controllerWorldPosition)
        {
            _grabbed = true;
            _angularVelocityDegreesPerSecond = 0f;
            TryUnregisterMomentumTick();
            _hasPreviousVector = TryProjectControllerVector(controllerWorldPosition, out _previousPlaneVector);
        }

        public bool SampleControllerPose(Vector3 controllerWorldPosition)
        {
            if (!_grabbed)
                BeginGrab(controllerWorldPosition);

            if (!TryProjectControllerVector(controllerWorldPosition, out Vector3 currentPlaneVector))
                return false;

            if (!_hasPreviousVector)
            {
                _previousPlaneVector = currentPlaneVector;
                _hasPreviousVector = true;
                return false;
            }

            Vector3 axisWorld = ResolveWorldAxis();
            float deltaDegrees = ResolveSignedDeltaDegrees(_previousPlaneVector, currentPlaneVector, axisWorld);
            _previousPlaneVector = currentPlaneVector;

            if (!float.IsFinite(deltaDegrees) || math.abs(deltaDegrees) > maxAcceptedSampleDeltaDegrees)
                return false;

            float sampleDeltaTime = math.max(0.0001f, Time.unscaledDeltaTime);
            _angularVelocityDegreesPerSecond = deltaDegrees / sampleDeltaTime;
            ApplyControllerAngularDeltaDegrees(deltaDegrees);
            return true;
        }

        public void EndGrab()
        {
            _grabbed = false;
            _hasPreviousVector = false;
            if (math.abs(_angularVelocityDegreesPerSecond) > minimumMomentumDegreesPerSecond)
                TryRegisterMomentumTick();
        }

        public void ApplyControllerAngularDeltaDegrees(float deltaDegrees)
        {
            if (!float.IsFinite(deltaDegrees))
                return;

            float safeDegreesToOpen = math.max(1f, degreesToOpen);
            _accumulatedDegrees = math.clamp(_accumulatedDegrees + deltaDegrees, 0f, safeDegreesToOpen);
            _isOpen01 = math.saturate(_accumulatedDegrees / safeDegreesToOpen);
            ApplyWheelVisual();
        }

        public void Tick(float deltaTime)
        {
            if (_grabbed)
                return;

            float speed = math.abs(_angularVelocityDegreesPerSecond);
            if (speed <= minimumMomentumDegreesPerSecond || deltaTime <= 0f)
            {
                _angularVelocityDegreesPerSecond = 0f;
                TryUnregisterMomentumTick();
                return;
            }

            float before = _accumulatedDegrees;
            ApplyControllerAngularDeltaDegrees(_angularVelocityDegreesPerSecond * deltaTime);
            if (math.abs(_accumulatedDegrees - before) <= 0.0001f &&
                (_accumulatedDegrees <= 0f || _accumulatedDegrees >= math.max(1f, degreesToOpen)))
            {
                _angularVelocityDegreesPerSecond = 0f;
                TryUnregisterMomentumTick();
                return;
            }

            float dragScalar = math.max(0f, 1f - angularDragPerSecond * deltaTime);
            _angularVelocityDegreesPerSecond *= dragScalar;
        }

        public void SetOpen01Direct(float open01)
        {
            _isOpen01 = math.saturate(open01);
            _accumulatedDegrees = _isOpen01 * math.max(1f, degreesToOpen);
            _angularVelocityDegreesPerSecond = 0f;
            ApplyWheelVisual();
        }

        private bool TryProjectControllerVector(Vector3 controllerWorldPosition, out Vector3 planeVector)
        {
            Vector3 axisWorld = ResolveWorldAxis();
            Vector3 toController = controllerWorldPosition - transform.position;
            float axisDot = toController.x * axisWorld.x + toController.y * axisWorld.y + toController.z * axisWorld.z;
            planeVector = toController - axisWorld * axisDot;
            float lengthSq = planeVector.sqrMagnitude;
            if (lengthSq <= 0.000001f || !math.all(math.isfinite(new float3(planeVector.x, planeVector.y, planeVector.z))))
            {
                planeVector = Vector3.zero;
                return false;
            }

            planeVector *= math.rsqrt(lengthSq);
            return true;
        }

        private static float ResolveSignedDeltaDegrees(Vector3 previous, Vector3 current, Vector3 axisWorld)
        {
            float3 prev = new float3(previous.x, previous.y, previous.z);
            float3 curr = new float3(current.x, current.y, current.z);
            float3 axis = new float3(axisWorld.x, axisWorld.y, axisWorld.z);
            float sin = math.dot(axis, math.cross(prev, curr));
            float cos = math.clamp(math.dot(prev, curr), -1f, 1f);
            return math.degrees(math.atan2(sin, cos));
        }

        private void ApplyWheelVisual()
        {
            Transform visual = ResolveVisual();
            Vector3 localAxis = ResolveLocalAxis();
            visual.localRotation = _closedLocalRotation * Quaternion.AngleAxis(_accumulatedDegrees, localAxis);
        }

        private Transform ResolveVisual()
        {
            return wheelVisual != null ? wheelVisual : transform;
        }

        private Vector3 ResolveLocalAxis()
        {
            float lengthSq = localRotationAxis.sqrMagnitude;
            if (lengthSq <= 0.000001f)
                return Vector3.forward;

            return localRotationAxis * math.rsqrt(lengthSq);
        }

        private Vector3 ResolveWorldAxis()
        {
            return transform.TransformDirection(ResolveLocalAxis());
        }

        private void TryRegisterMomentumTick()
        {
            if (_registeredMomentumTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Player);
            _registeredMomentumTick = GlobalRegistry.Updatables.Contains(this);
        }

        private void TryUnregisterMomentumTick()
        {
            if (!_registeredMomentumTick)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
            _registeredMomentumTick = false;
        }

        private void OnDisable()
        {
            _angularVelocityDegreesPerSecond = 0f;
            TryUnregisterMomentumTick();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (degreesToOpen < 1f)
                degreesToOpen = 1f;
            if (maxAcceptedSampleDeltaDegrees < 1f)
                maxAcceptedSampleDeltaDegrees = 1f;
            if (angularDragPerSecond < 0f)
                angularDragPerSecond = 0f;
            if (minimumMomentumDegreesPerSecond < 0f)
                minimumMomentumDegreesPerSecond = 0f;
        }
#endif
    }
}
