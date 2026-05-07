using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Sole owner that applies camera transform and FOV state for the player rig.
    /// Locomotion publishes desired state; the rig interpolates and applies it.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Hecton Player Camera Rig")]
    public sealed class HectonPlayerCameraRig : MonoBehaviour, ITickable, IUpdatable, IOriginShiftListener
    {
        private const float MinimumBlendSharpness = 0.01f;
        private const float MinimumBlendDeltaTime = 0.0001f;

        [Header("References")]
        [SerializeField, Tooltip("Camera transform driven by the rig.")]
        private Transform cameraTransform;

        [SerializeField, Tooltip("Optional explicit camera component. Falls back to the driven transform.")]
        private Camera cameraComponent;

        [SerializeField, Tooltip("Tracking-space root reparented under active AUP anchors for VR cockpit motion.")]
        private Transform trackingSpaceRoot;

        private bool _registered;
        private bool _registeredOriginShiftListener;
        private bool _hasPendingState;
        private HectonCameraState _pendingState;
        private bool _hasLastAppliedTrackingState;
        private int _originShiftTrackingLockFrame = -1;
        private Transform _defaultTrackingSpaceParent;
        private Transform _pendingAupAnchor;
        private Transform _appliedAupAnchor;
        private Vector3 _lastAppliedLocalPosition;
        private Quaternion _lastAppliedWorldRotation = Quaternion.identity;

        /// <summary>
        /// Wires the runtime camera references used by this rig.
        /// </summary>
        public void Bind(Transform targetCameraTransform, Camera targetCameraComponent)
        {
            cameraTransform = targetCameraTransform;
            cameraComponent = targetCameraComponent != null
                ? targetCameraComponent
                : targetCameraTransform != null ? targetCameraTransform.GetComponent<Camera>() : null;
            if (trackingSpaceRoot == null && targetCameraTransform != null)
                trackingSpaceRoot = targetCameraTransform.parent;
            if (trackingSpaceRoot != null && _defaultTrackingSpaceParent == null)
                _defaultTrackingSpaceParent = trackingSpaceRoot.parent;
        }

        /// <summary>
        /// Pushes the latest locomotion-owned camera target for application on the rig tick.
        /// </summary>
        public void SetLocomotionState(HectonCameraState state)
        {
            _pendingState = state;
            _hasPendingState = true;
        }

        /// <summary>
        /// Assigns the parent frame for VR tracking space. Null restores the original scene parent.
        /// </summary>
        public void SetAupAnchor(Transform aupAnchor)
        {
            _pendingAupAnchor = aupAnchor;
        }

        private void OnEnable()
        {
            TryRegister();
        }

        private void Start()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
            if (!_hasPendingState || cameraTransform == null)
                return;

            ApplyCameraState(_pendingState);
        }

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            _originShiftTrackingLockFrame = shiftData.Frame + 1;
        }

        private void ApplyCameraState(in HectonCameraState state)
        {
            ApplyPendingAupAnchor();
            Quaternion targetRotation = state.TargetRotation;
            Vector3 targetLocalPosition = state.TargetLocalPosition;
            bool lockTrackingForAup = _hasLastAppliedTrackingState && Time.frameCount == _originShiftTrackingLockFrame;
            if (lockTrackingForAup)
            {
                targetRotation = _lastAppliedWorldRotation;
                targetLocalPosition = _lastAppliedLocalPosition;
                _originShiftTrackingLockFrame = -1;
            }
            else if (_originShiftTrackingLockFrame >= 0 && Time.frameCount > _originShiftTrackingLockFrame)
            {
                _originShiftTrackingLockFrame = -1;
            }

            float rotationT = ResolvePresentationBlendT(state.RotationSharpness, state.DeltaTime);
            cameraTransform.rotation = Quaternion.Slerp(cameraTransform.rotation, targetRotation, rotationT);

            float positionT = ResolvePresentationBlendT(state.PositionSharpness, state.DeltaTime);
            cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, targetLocalPosition, positionT);

            if (cameraComponent != null)
            {
                float fovT = ResolvePresentationBlendT(state.FieldOfViewSharpness, state.DeltaTime);
                cameraComponent.fieldOfView = math.lerp(cameraComponent.fieldOfView, state.TargetFieldOfView, fovT);
            }

            _lastAppliedLocalPosition = cameraTransform.localPosition;
            _lastAppliedWorldRotation = cameraTransform.rotation;
            _hasLastAppliedTrackingState = true;
        }

        private static float ResolvePresentationBlendT(float sharpness, float deltaTime)
        {
            float x = math.max(MinimumBlendSharpness, sharpness) * math.max(MinimumBlendDeltaTime, deltaTime);
            return math.saturate(x / (1f + 0.5f * x));
        }

        private void ApplyPendingAupAnchor()
        {
            if (trackingSpaceRoot == null)
                return;

            Transform targetParent = _pendingAupAnchor != null ? _pendingAupAnchor : _defaultTrackingSpaceParent;
            if (trackingSpaceRoot.parent != targetParent)
                trackingSpaceRoot.SetParent(targetParent, true);

            _appliedAupAnchor = _pendingAupAnchor;
            if (_appliedAupAnchor == null)
                return;

            trackingSpaceRoot.localPosition = Vector3.zero;
            trackingSpaceRoot.localRotation = Quaternion.identity;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Player);
            _registered = GlobalRegistry.Updatables.Contains(this);
            if (!_registeredOriginShiftListener)
            {
                HectonFloatingOrigin.RegisterListener(this);
                _registeredOriginShiftListener = true;
            }
        }

        private void TryUnregister()
        {
            if (_registeredOriginShiftListener)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _registeredOriginShiftListener = false;
            }

            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
            _registered = false;
        }
    }
}
