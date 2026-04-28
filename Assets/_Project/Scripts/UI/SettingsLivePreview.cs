using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Hecton8.Bootstrap;
using Hecton8.Core;

namespace Hecton8.UI
{
    /// <summary>
    /// Live preview for settings changes (Subnautica-style).
    /// Updates graphics/audio in real-time as user drags sliders.
    /// Zero-GC: ITickable, cached references, no coroutines.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Settings Live Preview")]
    public sealed class SettingsLivePreview : MonoBehaviour, ITickable, IUpdatable
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
        private bool _isDirty;
        private float _dirtyTimer;
        private float _mainCameraResolveRetryTimer;
        private float _pendingFOV = -1f;
        private bool _pendingBloom;
        private bool _pendingMotionBlur;
        private bool _hasPendingPostProcessing;

        // ══════════════════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void OnEnable()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
        }

        private void OnDestroy()
        {
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
        }

        // ══════════════════════════════════════════════════════════
        // ITICKABLE
        // ══════════════════════════════════════════════════════════

        public void Tick(float dt)
        {
            if (_mainCameraResolveRetryTimer > 0f)
                _mainCameraResolveRetryTimer -= dt;

            if (!_isDirty)
                return;

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
            if (!TryResolveMainCamera())
            {
                _pendingFOV = -1f;
                return;
            }

            mainCamera.fieldOfView = _pendingFOV;
            _pendingFOV = -1f;
        }

        private bool TryResolveMainCamera()
        {
            if (mainCamera != null)
                return true;

            if (_mainCameraResolveRetryTimer > 0f)
                return false;

            _mainCameraResolveRetryTimer = MainCameraResolveRetryInterval;

            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null)
            {
                if (playerTransform.TryGetComponent(out Camera playerOwnedCamera))
                {
                    mainCamera = playerOwnedCamera;
                    return true;
                }

                Camera playerChildCamera = ((Hecton8.Core.GlobalRegistry.Player != null && Hecton8.Core.GlobalRegistry.Player.PlayerCamera != null) ? Hecton8.Core.GlobalRegistry.Player.PlayerCamera : playerTransform.GetComponent<Camera>());
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

            Camera parentCamera = GetComponentInParent<Camera>();
            if (parentCamera != null)
            {
                mainCamera = parentCamera;
                return true;
            }

            mainCamera = null;
            return false;
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
            if (_registered)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registered = false;
        }
    }
}
