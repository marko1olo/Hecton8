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
    public sealed class HectonPlayerCameraRig : MonoBehaviour, ITickable, IUpdatable
    {
        [Header("References")]
        [SerializeField, Tooltip("Camera transform driven by the rig.")]
        private Transform cameraTransform;

        [SerializeField, Tooltip("Optional explicit camera component. Falls back to the driven transform.")]
        private Camera cameraComponent;

        private bool _registered;
        private bool _hasPendingState;
        private HectonCameraState _pendingState;

        /// <summary>
        /// Wires the runtime camera references used by this rig.
        /// </summary>
        public void Bind(Transform targetCameraTransform, Camera targetCameraComponent)
        {
            cameraTransform = targetCameraTransform;
            cameraComponent = targetCameraComponent != null
                ? targetCameraComponent
                : targetCameraTransform != null ? targetCameraTransform.GetComponent<Camera>() : null;
        }

        /// <summary>
        /// Pushes the latest locomotion-owned camera target for application on the rig tick.
        /// </summary>
        public void SetLocomotionState(HectonCameraState state)
        {
            _pendingState = state;
            _hasPendingState = true;
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

        private void ApplyCameraState(in HectonCameraState state)
        {
            float rotationT = 1f - math.exp(-math.max(0.01f, state.RotationSharpness) * math.max(0.0001f, state.DeltaTime));
            cameraTransform.rotation = Quaternion.Slerp(cameraTransform.rotation, state.TargetRotation, rotationT);

            float positionT = 1f - math.exp(-math.max(0.01f, state.PositionSharpness) * math.max(0.0001f, state.DeltaTime));
            cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, state.TargetLocalPosition, positionT);

            if (cameraComponent != null)
            {
                float fovT = 1f - math.exp(-math.max(0.01f, state.FieldOfViewSharpness) * math.max(0.0001f, state.DeltaTime));
                cameraComponent.fieldOfView = math.lerp(cameraComponent.fieldOfView, state.TargetFieldOfView, fovT);
            }
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Player);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
            _registered = false;
        }
    }
}
