using UnityEngine;
using Hecton8.Core;
using Unity.Mathematics;

namespace Hecton8.UI
{
    /// <summary>
    /// UI screen shake for destructive actions (delete save, reset settings).
    /// Cheap instrument shock cue without changing gameplay truth.
    /// Zero-GC: late-frame state machine, cached RectTransform, no coroutines.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/UI Screen Shake")]
    public sealed class UIScreenShake : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        // --------------------------------------------------------------------------
        // INSPECTOR
        // --------------------------------------------------------------------------

        [Header("=== SETTINGS ===")]
        [SerializeField] private float shakeDuration = 0.2f;
        [SerializeField] private float shakeIntensity = 10f;
        [SerializeField] private AnimationCurve shakeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        private const float DefaultGlobalMotionScale = 1f;
        private const float MinimumActiveMotionScale = 0.0001f;

        // --------------------------------------------------------------------------
        // FIELDS
        // --------------------------------------------------------------------------

        private RectTransform _rectTransform;
        private Vector2 _originalPosition;
        private bool _isShaking;
        private float _shakeTimer;
        private float _activeShakeIntensity;
        private bool _registered;
        private bool _hotSwapRegistered;
        private static float s_globalMotionScale = DefaultGlobalMotionScale;

        // --------------------------------------------------------------------------
        // LIFECYCLE
        // --------------------------------------------------------------------------

        private void Awake()
        {
            TryGetComponent(out _rectTransform);
            if (_rectTransform != null)
                _originalPosition = _rectTransform.anchoredPosition;
            _activeShakeIntensity = SanitizeNonNegativeFinite(shakeIntensity);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_globalMotionScale = DefaultGlobalMotionScale;
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
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            Unregister();
            if (currentService != null && isActiveAndEnabled && _isShaking)
                TryRegister();
        }

        // --------------------------------------------------------------------------
        // LATE FRAME
        // --------------------------------------------------------------------------

        public void LateFrameTick()
        {
            if (!_isShaking)
                return;

            float motionScale = SanitizeMotionScale(s_globalMotionScale);
            if (motionScale <= MinimumActiveMotionScale)
            {
                _isShaking = false;
                ResetPosition();
                return;
            }

            float safeDeltaTime = SanitizeNonNegativeFinite(SystemDispatcher.CurrentFrameDeltaTime);
            float safeDuration = SanitizePositiveFinite(shakeDuration, 0.0001f);
            _shakeTimer += safeDeltaTime;
            float t = math.saturate(_shakeTimer / safeDuration);
            float envelope = shakeCurve != null ? shakeCurve.Evaluate(t) : 1f - t;
            envelope = SanitizeNonNegativeFinite(envelope);
            float intensity = envelope * _activeShakeIntensity * motionScale;

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

        // --------------------------------------------------------------------------
        // PUBLIC API
        // --------------------------------------------------------------------------

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

        /// <summary>
        /// Presentation-only accessibility scalar for UI shock motion. Actual transform writes stay in LateFrameTick.
        /// </summary>
        public static void SetGlobalMotionScale(float scale)
        {
            s_globalMotionScale = SanitizeMotionScale(scale);
        }

        // --------------------------------------------------------------------------
        // PRIVATE
        // --------------------------------------------------------------------------

        private void ResetPosition()
        {
            if (_rectTransform != null)
                _rectTransform.anchoredPosition = _originalPosition;
        }

        private void BeginShake(float intensity)
        {
            if (_rectTransform == null)
                return;

            if (SanitizeMotionScale(s_globalMotionScale) <= MinimumActiveMotionScale)
                return;

            _originalPosition = _rectTransform.anchoredPosition;
            _activeShakeIntensity = SanitizeNonNegativeFinite(intensity);
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

        private static float SanitizeMotionScale(float scale)
        {
            return math.isfinite(scale) ? math.saturate(scale) : DefaultGlobalMotionScale;
        }

        private static float SanitizeNonNegativeFinite(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        private static float SanitizePositiveFinite(float value, float fallback)
        {
            if (!math.isfinite(value) || value <= 0f)
                return fallback;

            return value;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            _registered = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);
        }

        private void Unregister()
        {
            if (!_registered)
                return;

            SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
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
