using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Hecton8.Bootstrap;
using Hecton8.Core;

namespace Hecton8.UI
{
    /// <summary>
    /// Live preview for HECTON-8 settings changes.
    /// Updates graphics/audio in real-time as user drags sliders.
    /// Zero-GC: late-frame state machine, cached references, no coroutines.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Settings Live Preview")]
    public sealed class SettingsLivePreview : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const float MainCameraResolveRetryInterval = 1f;

        // ══════════════════════════════════════════════════════════
        // INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("=== REFERENCES ===")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private Volume urpVolume;

        [Header("=== SETTINGS ===")]
        [SerializeField, Tooltip("Debounce time for live updates (seconds)")]
        private float debounceTime = 0.05f;

        // ══════════════════════════════════════════════════════════
        // FIELDS
        // ══════════════════════════════════════════════════════════

        private bool _registered;
        private bool _hotSwapRegistered;
        private bool _isDirty;
        private float _dirtyTimer;
        private float _mainCameraResolveRetryTimer;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private float _pendingFOV = -1f;
        private bool _pendingBloom;
        private bool _pendingMotionBlur;
        private bool _hasPendingPostProcessing;

        // ══════════════════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryResolveMainCameraCold();
            TryRegisterHotSwapListener();
            RefreshTickRegistration();
        }

        private void Start()
        {
            TryResolveMainCameraCold();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            TryUnregister();
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            TryUnregister();
        }

        // ══════════════════════════════════════════════════════════
        // PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Queue FOV change for live preview.
        /// Actual update happens after debounce time.
        /// </summary>
        public void PreviewFOV(float fov)
        {
            _pendingFOV = fov;
            _isDirty = true;
            _dirtyTimer = 0f;
            TryRegister();
        }

        /// <summary>
        /// Queue post-processing changes for live preview.
        /// Ambient occlusion is persisted but not previewed here because Unity 6000
        /// exposes SSAO as a renderer feature, not a VolumeComponent in this project.
        /// </summary>
        public void PreviewPostProcessing(bool ao, bool bloom, bool motionBlur)
        {
            _pendingBloom = bloom;
            _pendingMotionBlur = motionBlur;
            _hasPendingPostProcessing = true;
            _isDirty = true;
            _dirtyTimer = 0f;
            TryRegister();
        }

        /// <summary>
        /// Immediately apply all pending changes (called on Apply button).
        /// </summary>
        public void ApplyImmediately()
        {
            if (_pendingFOV > 0f)
                ApplyFOV();

            if (_hasPendingPostProcessing)
                ApplyPostProcessing();

            _isDirty = false;
            _dirtyTimer = 0f;
            RefreshTickRegistration();
        }

        /// <summary>
        /// Cancel all pending changes (called on Cancel button).
        /// </summary>
        public void CancelPending()
        {
            _pendingFOV = -1f;
            _hasPendingPostProcessing = false;
            _isDirty = false;
            _dirtyTimer = 0f;
            RefreshTickRegistration();
        }

        // ══════════════════════════════════════════════════════════
        // LATE FRAME
        // ══════════════════════════════════════════════════════════

        public void LateFrameTick()
        {
            float dt = Mathf.Max(0f, SystemDispatcher.CurrentFrameDeltaTime);
            if (_mainCameraResolveRetryTimer > 0f)
                _mainCameraResolveRetryTimer -= dt;

            if (!_isDirty)
            {
                return;
            }

            _dirtyTimer += dt;
            if (_dirtyTimer < debounceTime)
                return;

            // Debounce time elapsed, apply changes
            if (_pendingFOV > 0f)
                ApplyFOV();

            if (_hasPendingPostProcessing)
                ApplyPostProcessing();

            _isDirty = false;
            _dirtyTimer = 0f;
        }

        // ══════════════════════════════════════════════════════════
        // PRIVATE — APPLY
        // ══════════════════════════════════════════════════════════

        private void ApplyFOV()
        {
            if (mainCamera == null)
            {
                _pendingFOV = -1f;
                return;
            }

            mainCamera.fieldOfView = _pendingFOV;
            _pendingFOV = -1f;
        }

        private bool TryResolveMainCameraCold()
        {
            if (mainCamera != null)
                return true;

            if (_mainCameraResolveRetryTimer > 0f)
                return false;

            _mainCameraResolveRetryTimer = MainCameraResolveRetryInterval;

            if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null)
            {
                if (playerTransform.TryGetComponent(out Camera playerOwnedCamera))
                {
                    mainCamera = playerOwnedCamera;
                    return true;
                }

                IPlayerRuntimeContext playerContext = _playerRuntimeContext;
                Camera playerChildCamera = playerContext != null ? playerContext.PlayerCamera : null;
                if (playerChildCamera != null)
                {
                    mainCamera = playerChildCamera;
                    return true;
                }
            }

            if (TryGetComponent(out Camera localCamera))
            {
                mainCamera = localCamera;
                return true;
            }

            Camera childCamera = Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<Camera>(transform);
            if (childCamera != null)
            {
                mainCamera = childCamera;
                return true;
            }

            Camera parentCamera = ResolveNearestParentCamera(transform);
            if (parentCamera != null)
            {
                mainCamera = parentCamera;
                return true;
            }

            mainCamera = null;
            return false;
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

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                _mainCameraResolveRetryTimer = 0f;
                TryResolveMainCameraCold();
            }
        }

        private void CacheRegistryServicesCold()
        {
            _playerRuntimeContext = GlobalRegistry.Player;
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

        private void ApplyPostProcessing()
        {
            if (urpVolume == null || urpVolume.profile == null)
            {
                _hasPendingPostProcessing = false;
                return;
            }

            VolumeProfile profile = urpVolume.profile;

            // Bloom
            if (profile.TryGet(out UnityEngine.Rendering.Universal.Bloom bloom))
            {
                bloom.active = _pendingBloom;
            }

            // Motion Blur
            if (profile.TryGet(out UnityEngine.Rendering.Universal.MotionBlur motionBlur))
            {
                motionBlur.active = _pendingMotionBlur;
            }

            _hasPendingPostProcessing = false;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
            _registered = false;
        }

        private void RefreshTickRegistration()
        {
            if (_isDirty || _mainCameraResolveRetryTimer > 0f)
                TryRegister();
            else
                TryUnregister();
        }
    }
}
