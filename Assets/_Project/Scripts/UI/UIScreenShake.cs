using UnityEngine;
using Hecton8.Core;

namespace Hecton8.UI
{
    /// <summary>
    /// UI screen shake for destructive actions (delete save, reset settings).
    /// EXCEEDS SUBNAUTICA: Subnautica has no screen shake on UI actions.
    /// Zero-GC: ITickable state machine, cached RectTransform, no coroutines.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/UI Screen Shake")]
    public sealed class UIScreenShake : MonoBehaviour, ITickable, IUpdatable
    {
        // ══════════════════════════════════════════════════════════
        // INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("=== SETTINGS ===")]
        [SerializeField] private float shakeDuration = 0.2f;
        [SerializeField] private float shakeIntensity = 10f;
        [SerializeField] private AnimationCurve shakeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        // ══════════════════════════════════════════════════════════
        // FIELDS
        // ══════════════════════════════════════════════════════════

        private RectTransform _rectTransform;
        private Vector2 _originalPosition;
        private bool _isShaking;
        private float _shakeTimer;
        private bool _registered;

        // ══════════════════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform != null)
                _originalPosition = _rectTransform.anchoredPosition;
        }

        private void OnEnable()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            Unregister();
            ResetPosition();
        }

        private void OnDestroy()
        {
            Unregister();
        }

        // ══════════════════════════════════════════════════════════
        // ITICKABLE
        // ══════════════════════════════════════════════════════════

        public void Tick(float dt)
        {
            if (!_isShaking)
                return;

            _shakeTimer += dt;
            float t = Mathf.Clamp01(_shakeTimer / shakeDuration);
            float intensity = shakeCurve.Evaluate(t) * shakeIntensity;

            if (_rectTransform != null)
            {
                Vector2 offset = new Vector2(
                    Random.Range(-intensity, intensity),
                    Random.Range(-intensity, intensity));
                _rectTransform.anchoredPosition = _originalPosition + offset;
            }

            if (t >= 1f)
            {
                _isShaking = false;
                ResetPosition();
            }
        }

        // ══════════════════════════════════════════════════════════
        // PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Trigger screen shake.
        /// </summary>
        public void Shake()
        {
            if (_rectTransform == null)
                return;

            _originalPosition = _rectTransform.anchoredPosition;
            _isShaking = true;
            _shakeTimer = 0f;
            TryRegister();
        }

        /// <summary>
        /// Trigger screen shake with custom intensity.
        /// </summary>
        public void Shake(float customIntensity)
        {
            float originalIntensity = shakeIntensity;
            shakeIntensity = customIntensity;
            Shake();
            shakeIntensity = originalIntensity;
        }

        // ══════════════════════════════════════════════════════════
        // PRIVATE
        // ══════════════════════════════════════════════════════════

        private void ResetPosition()
        {
            if (_rectTransform != null)
                _rectTransform.anchoredPosition = _originalPosition;
        }

        private void TryRegister()
        {
            if (_registered)
                return;

            SystemDispatcher.EnsureRuntimeInstance();
            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registered = true;
        }

        private void Unregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registered = false;
        }
    }
}
