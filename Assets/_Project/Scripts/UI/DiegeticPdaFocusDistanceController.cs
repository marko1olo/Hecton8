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
    public sealed class DiegeticPdaFocusDistanceController : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
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
        private IPlayerRuntimeContext _cachedPlayerContext;
        private bool _registered;
        private bool _hotSwapListenerRegistered;
        private bool _focusActive;
        private bool _targetCameraFromPlayerContext;
        private bool _targetVolumeFromCameraContext;
        private int _lastRaycastFrame = -1;
        private int _nextResolveFrame;
        private float _lastFocusDistance;

        public bool FocusActive => _focusActive;
        public int LastRaycastFrame => _lastRaycastFrame;
        public float LastFocusDistance => _lastFocusDistance;

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            ResolveReferences();
            _focusActive = focusActiveOnEnable;
            if (_focusActive)
                TryRegisterTick();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            TryUnregisterTick();
            _focusActive = false;
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
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

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Player)
                return;

            IPlayerRuntimeContext previousContext = previousService as IPlayerRuntimeContext;
            IPlayerRuntimeContext currentContext = currentService as IPlayerRuntimeContext;
            _cachedPlayerContext = currentContext;

            Camera previousCamera = previousContext != null ? previousContext.PlayerCamera : null;
            Camera currentCamera = currentContext != null ? currentContext.PlayerCamera : null;
            if (targetCamera == null || _targetCameraFromPlayerContext || ReferenceEquals(targetCamera, previousCamera))
            {
                targetCamera = currentCamera;
                _targetCameraFromPlayerContext = currentCamera != null;
                _cameraTransform = currentCamera != null ? currentCamera.transform : null;
                if ((targetVolume == null || _targetVolumeFromCameraContext) && currentCamera != null)
                {
                    targetVolume = ResolveCameraVolume(currentCamera.transform);
                    _targetVolumeFromCameraContext = targetVolume != null;
                    _depthOfField = null;
                }
                else if (currentCamera == null && _targetVolumeFromCameraContext)
                {
                    targetVolume = null;
                    _targetVolumeFromCameraContext = false;
                    _depthOfField = null;
                }
                if (_focusActive)
                    _nextResolveFrame = 0;
            }
        }

        private void CacheRegistryServicesCold()
        {
            _cachedPlayerContext = GlobalRegistry.Player;
        }

        private void ResolveReferences()
        {
            _nextResolveFrame = Time.frameCount + 30;

            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (targetCamera == null && playerContext != null)
            {
                targetCamera = playerContext.PlayerCamera;
                _targetCameraFromPlayerContext = targetCamera != null;
            }
            if (targetCamera == null)
            {
                targetCamera = ResolveNearestParentCamera(transform);
                _targetCameraFromPlayerContext = false;
            }
            _cameraTransform = targetCamera != null ? targetCamera.transform : null;

            if (targetVolume == null && targetCamera != null)
            {
                targetVolume = ResolveCameraVolume(targetCamera.transform);
                _targetVolumeFromCameraContext = targetVolume != null;
            }
            if (targetVolume == null)
            {
                targetVolume = ResolveNearestParentVolume(transform);
                _targetVolumeFromCameraContext = false;
            }

            if (_depthOfField == null && targetVolume != null && targetVolume.profile != null)
                targetVolume.profile.TryGet(out _depthOfField);
        }

        private void TryRegisterTick()
        {
            if (_registered || !_focusActive)
                return;

            _registered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
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
