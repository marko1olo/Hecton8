using UnityEngine;
using Hecton8.Core;

namespace Hecton8.UI
{
    /// <summary>
    /// Generic fade transition for UI elements.
    /// Zero-GC: late-frame state machine, CanvasGroup alpha, no coroutines.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    [AddComponentMenu("Hecton8/UI/UI Fade Transition")]
    public sealed class UIFadeTransition : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        // --------------------------------------------------------------------------
        // INSPECTOR
        // --------------------------------------------------------------------------

        [Header("=== SETTINGS ===")]
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float fadeOutDuration = 0.2f;
        [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        // --------------------------------------------------------------------------
        // FIELDS
        // --------------------------------------------------------------------------

        private enum State { Idle, FadingIn, FadingOut }

        private CanvasGroup _canvasGroup;
        private State _state;
        private float _timer;
        private float _targetAlpha;
        private float _startAlpha;
        private bool _registered;
        private bool _hotSwapRegistered;

        // --------------------------------------------------------------------------
        // LIFECYCLE
        // --------------------------------------------------------------------------

        private void Awake()
        {
            TryGetComponent(out _canvasGroup);
        }

        private void OnEnable()
        {
            TryRegisterHotSwapListener();
            if (_state != State.Idle)
                TryRegister();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            Unregister();
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
            {
                if (currentService == null)
                {
                    _registered = false;
                    return;
                }

                Unregister();
                if (_state != State.Idle)
                    TryRegister();
            }
        }

        // --------------------------------------------------------------------------
        // LATE FRAME
        // --------------------------------------------------------------------------

        public void LateFrameTick()
        {
            if (_state == State.Idle || _canvasGroup == null)
            {
                Unregister();
                return;
            }

            float dt = Mathf.Max(0f, SystemDispatcher.CurrentFrameUnscaledDeltaTime);
            _timer += dt;

            float duration = _state == State.FadingIn ? fadeInDuration : fadeOutDuration;
            float safeDuration = Mathf.Max(0.0001f, duration);
            float t = Mathf.Clamp01(_timer / safeDuration);
            float curveT = fadeCurve != null ? fadeCurve.Evaluate(t) : t;

            _canvasGroup.alpha = _startAlpha + ((_targetAlpha - _startAlpha) * curveT);

            if (t >= 1f)
            {
                _state = State.Idle;
                Unregister();
            }
        }

        // --------------------------------------------------------------------------
        // PUBLIC API
        // --------------------------------------------------------------------------

        /// <summary>
        /// Fade in from current alpha to 1.
        /// </summary>
        public void FadeIn()
        {
            if (_canvasGroup == null)
                return;

            _startAlpha = _canvasGroup.alpha;
            _targetAlpha = 1f;
            _timer = 0f;
            _state = State.FadingIn;
            TryRegister();
        }

        /// <summary>
        /// Fade out from current alpha to 0.
        /// </summary>
        public void FadeOut()
        {
            if (_canvasGroup == null)
                return;

            _startAlpha = _canvasGroup.alpha;
            _targetAlpha = 0f;
            _timer = 0f;
            _state = State.FadingOut;
            TryRegister();
        }

        /// <summary>
        /// Fade to specific alpha value.
        /// </summary>
        public void FadeTo(float targetAlpha)
        {
            if (_canvasGroup == null)
                return;

            _startAlpha = _canvasGroup.alpha;
            _targetAlpha = Mathf.Clamp01(targetAlpha);
            _timer = 0f;
            _state = _targetAlpha > _startAlpha ? State.FadingIn : State.FadingOut;
            TryRegister();
        }

        /// <summary>
        /// Set alpha immediately without animation.
        /// </summary>
        public void SetAlphaImmediate(float alpha)
        {
            if (_canvasGroup == null)
                return;

            _canvasGroup.alpha = Mathf.Clamp01(alpha);
            _state = State.Idle;
            Unregister();
        }

        /// <summary>
        /// Check if currently fading.
        /// </summary>
        public bool IsFading => _state != State.Idle;

        // --------------------------------------------------------------------------
        // PRIVATE
        // --------------------------------------------------------------------------

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
