using UnityEngine;
using Hecton8.Core;
using Unity.Mathematics;

namespace Hecton8.UI
{
    /// <summary>
    /// UI screen shake for destructive actions (delete save, reset settings).
    /// EXCEEDS SUBNAUTICA: Subnautica has no screen shake on UI actions.
    /// Zero-GC: late-frame state machine, cached RectTransform, no coroutines.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/UI Screen Shake")]
    public sealed class UIScreenShake : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // INSPECTOR
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("=== SETTINGS ===")]
        [SerializeField] private float shakeDuration = 0.2f;
        [SerializeField] private float shakeIntensity = 10f;
        [SerializeField] private AnimationCurve shakeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // FIELDS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private RectTransform _rectTransform;
        private Vector2 _originalPosition;
        private bool _isShaking;
        private float _shakeTimer;
        private float _activeShakeIntensity;
        private bool _registered;
        private bool _hotSwapRegistered;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // LIFECYCLE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void Awake()
        {
            TryGetComponent(out _rectTransform);
            if (_rectTransform != null)
                _originalPosition = _rectTransform.anchoredPosition;
            _activeShakeIntensity = shakeIntensity;
        }

        private void OnEnable()
        {
            TryRegisterHotSwapListener();
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            Unregister();
            _isShaking = false;
            ResetPosition();
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            Unregister();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher && isActiveAndEnabled)
                TryRegister();
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // LATE FRAME
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public void LateFrameTick()
        {
            if (!_isShaking)
                return;

            float safeDeltaTime = math.max(0f, SystemDispatcher.CurrentFrameDeltaTime);
            float safeDuration = math.max(0.0001f, shakeDuration);
            _shakeTimer += safeDeltaTime;
            float t = math.saturate(_shakeTimer / safeDuration);
            float envelope = shakeCurve != null ? shakeCurve.Evaluate(t) : 1f - t;
            float intensity = envelope * _activeShakeIntensity;

            if (_rectTransform != null)
            {
                float phase = (_shakeTimer * 97.31f) + (t * 13.17f);
                Vector2 offset = new Vector2(
                    CheapSignedNoise(phase, 0.113f) * intensity,
                    CheapSignedNoise(phase, 0.719f) * intensity);
                _rectTransform.anchoredPosition = _originalPosition + offset;
            }

            if (t >= 1f)
            {
                _isShaking = false;
                ResetPosition();
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // PUBLIC API
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Trigger screen shake.
        /// </summary>
        public void Shake()
        {
            BeginShake(shakeIntensity);
        }

        /// <summary>
        /// Trigger screen shake with custom intensity.
        /// </summary>
        public void Shake(float customIntensity)
        {
            BeginShake(customIntensity);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // PRIVATE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void ResetPosition()
        {
            if (_rectTransform != null)
                _rectTransform.anchoredPosition = _originalPosition;
        }

        private void BeginShake(float intensity)
        {
            if (_rectTransform == null)
                return;

            _originalPosition = _rectTransform.anchoredPosition;
            _activeShakeIntensity = math.max(0f, intensity);
            _isShaking = true;
            _shakeTimer = 0f;
        }

        private static float CheapSignedNoise(float value, float seed)
        {
            float h = math.frac((value + seed) * 0.1031f);
            h *= h + 33.33f;
            h *= h + h;
            return (math.frac(h) * 2f) - 1f;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            _registered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void Unregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
            _registered = false;
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
    }
}
