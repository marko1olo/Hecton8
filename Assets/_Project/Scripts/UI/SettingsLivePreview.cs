using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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
    public sealed class SettingsLivePreview : MonoBehaviour, ITickable
    {
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
        private float _pendingFOV = -1f;
        private bool _pendingBloom;
        private bool _pendingMotionBlur;
        private bool _hasPendingPostProcessing;

        // ══════════════════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void OnEnable()
        {
            if (GameTickManager.Instance != null && !_registered)
            {
                GameTickManager.Instance.Register(this);
                _registered = true;
            }
        }

        private void OnDisable()
        {
            if (GameTickManager.Instance != null && _registered)
            {
                GameTickManager.Instance.Unregister(this);
                _registered = false;
            }
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
        /// Ambient occlusion is persisted but not previewed here because the project
        /// does not expose a live renderer-feature owner through a Volume profile.
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
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null)
                {
                    _pendingFOV = -1f;
                    return;
                }
            }

            mainCamera.fieldOfView = _pendingFOV;
            _pendingFOV = -1f;
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
    }
}
