using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Hecton8.UI
{
    /// <summary>
    /// PDA close-focus controller. Performs at most one non-alloc raycast per frame while armed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DiegeticPdaFocusDistanceController : MonoBehaviour, ILateFrameTickable
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Volume targetVolume;
        [SerializeField] private LayerMask pdaLayerMask = ~0;
        [SerializeField] private bool focusActiveOnEnable;
        [SerializeField] private bool disableDepthOfFieldWhenNoHit = true;
        [SerializeField, Min(0.05f)] private float maxDistanceMeters = 1.25f;
        [SerializeField, Min(0.01f)] private float minFocusDistanceMeters = 0.08f;
        [SerializeField, Min(0.01f)] private float maxFocusDistanceMeters = 4f;
        [SerializeField, Min(0.01f)] private float blendSharpness = 18f;

        // COLD ALLOC: RaycastHit[1] - single PDA focus raycast slot - owner: DiegeticPdaFocusDistanceController
        private readonly RaycastHit[] _hitBuffer = new RaycastHit[1];
        private DepthOfField _depthOfField;
        private Transform _cameraTransform;
        private bool _registered;
        private bool _focusActive;
        private int _lastRaycastFrame = -1;
        private int _nextResolveFrame;
        private float _lastFocusDistance;

        public bool FocusActive => _focusActive;
        public int LastRaycastFrame => _lastRaycastFrame;
        public float LastFocusDistance => _lastFocusDistance;

        private void OnEnable()
        {
            ResolveReferences();
            _focusActive = focusActiveOnEnable;
            if (_focusActive)
                TryRegisterTick();
        }

        private void OnDisable()
        {
            TryUnregisterTick();
            _focusActive = false;
        }

        private void OnDestroy()
        {
            TryUnregisterTick();
        }

        public void SetFocusActive(bool active)
        {
            _focusActive = active;
            if (active)
            {
                _nextResolveFrame = 0;
                _lastRaycastFrame = -1;
                TryRegisterTick();
            }
            else
            {
                if (disableDepthOfFieldWhenNoHit && _depthOfField != null)
                    _depthOfField.active = false;

                TryUnregisterTick();
            }
        }

        public void LateFrameTick()
        {
            if (!_focusActive)
            {
                TryUnregisterTick();
                return;
            }

            int frame = Time.frameCount;
            if ((_cameraTransform == null || _depthOfField == null) && frame >= _nextResolveFrame)
                ResolveReferences();
            if (_cameraTransform == null || _depthOfField == null)
                return;

            if (_lastRaycastFrame == frame)
                return;

            _lastRaycastFrame = frame;
            int hitCount = UnityEngine.Physics.RaycastNonAlloc(
                _cameraTransform.position,
                _cameraTransform.forward,
                _hitBuffer,
                maxDistanceMeters,
                pdaLayerMask,
                QueryTriggerInteraction.Collide);

            if (hitCount <= 0)
            {
                if (disableDepthOfFieldWhenNoHit)
                    _depthOfField.active = false;

                return;
            }

            float targetDistance = math.clamp(_hitBuffer[0].distance, minFocusDistanceMeters, maxFocusDistanceMeters);
            float current = _lastFocusDistance > 0f ? _lastFocusDistance : targetDistance;
            float deltaTime = math.max(0f, SystemDispatcher.CurrentFrameDeltaTime);
            float blend = ResolvePadeApproach01(blendSharpness, math.max(0f, deltaTime));
            _lastFocusDistance = math.lerp(current, targetDistance, blend);
            _depthOfField.focusDistance.value = _lastFocusDistance;
            _depthOfField.active = true;
        }

        private void ResolveReferences()
        {
            _nextResolveFrame = Time.frameCount + 30;

            if (targetCamera == null && GlobalRegistry.Player != null)
                targetCamera = GlobalRegistry.Player.PlayerCamera;
            if (targetCamera == null)
                targetCamera = ResolveNearestParentCamera(transform);
            _cameraTransform = targetCamera != null ? targetCamera.transform : null;

            if (targetVolume == null && targetCamera != null)
                targetVolume = ResolveCameraVolume(targetCamera.transform);
            if (targetVolume == null)
                targetVolume = ResolveNearestParentVolume(transform);

            if (_depthOfField == null && targetVolume != null && targetVolume.profile != null)
                targetVolume.profile.TryGet(out _depthOfField);
        }

        private void TryRegisterTick()
        {
            if (_registered || !_focusActive)
                return;

            _registered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void TryUnregisterTick()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
            _registered = false;
        }

        private static float ResolvePadeApproach01(float sharpness, float dt)
        {
            float x = math.min(math.max(0f, sharpness) * math.max(0f, dt), 8f);
            float x2 = x * x;
            float expNegApprox = math.rcp(1f + x + (0.48f * x2) + (0.235f * x2 * x));
            return math.saturate(1f - expNegApprox);
        }

        private static Camera ResolveNearestParentCamera(Transform start)
        {
            for (Transform current = start; current != null; current = current.parent)
            {
                if (current.TryGetComponent(out Camera camera))
                    return camera;
            }

            return null;
        }

        private static Volume ResolveNearestParentVolume(Transform start)
        {
            for (Transform current = start; current != null; current = current.parent)
            {
                if (current.TryGetComponent(out Volume volume))
                    return volume;
            }

            return null;
        }

        private static Volume ResolveCameraVolume(Transform cameraTransform)
        {
            if (cameraTransform == null)
                return null;

            if (cameraTransform.TryGetComponent(out Volume volume))
                return volume;

            return ResolveFirstChildVolume(cameraTransform);
        }

        private static Volume ResolveFirstChildVolume(Transform root)
        {
            int childCount = root.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (!child.gameObject.activeInHierarchy)
                    continue;

                if (child.TryGetComponent(out Volume volume))
                    return volume;

                volume = ResolveFirstChildVolume(child);
                if (volume != null)
                    return volume;
            }

            return null;
        }
    }
}
