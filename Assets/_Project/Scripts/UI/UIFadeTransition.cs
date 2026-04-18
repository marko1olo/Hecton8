using UnityEngine;
using Hecton8.Core;

namespace Hecton8.UI
{
    /// <summary>
    /// Generic fade transition for UI elements.
    /// Zero-GC: ITickable state machine, CanvasGroup alpha, no coroutines.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    [AddComponentMenu("Hecton8/UI/UI Fade Transition")]
    public sealed class UIFadeTransition : MonoBehaviour, ITickable
    {
        // ══════════════════════════════════════════════════════════
        // INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("=== SETTINGS ===")]
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float fadeOutDuration = 0.2f;
        [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        // ══════════════════════════════════════════════════════════
        // FIELDS
        // ══════════════════════════════════════════════════════════

        private enum State { Idle, FadingIn, FadingOut }

        private CanvasGroup _canvasGroup;
        private State _state;
        private float _timer;
        private float _targetAlpha;
        private float _startAlpha;
        private bool _registered;

        // ══════════════════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            Unregister();
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
            if (_state == State.Idle || _canvasGroup == null)
                return;

            _timer += dt;

            float duration = _state == State.FadingIn ? fadeInDuration : fadeOutDuration;
            float t = Mathf.Clamp01(_timer / duration);
            float curveT = fadeCurve.Evaluate(t);

            _canvasGroup.alpha = Mathf.Lerp(_startAlpha, _targetAlpha, curveT);

            if (t >= 1f)
                _state = State.Idle;
        }

        // ══════════════════════════════════════════════════════════
        // PUBLIC API
        // ══════════════════════════════════════════════════════════

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
        }

        /// <summary>
        /// Check if currently fading.
        /// </summary>
        public bool IsFading => _state != State.Idle;

        // ══════════════════════════════════════════════════════════
        // PRIVATE
        // ══════════════════════════════════════════════════════════

        private void TryRegister()
        {
            if (_registered || GameTickManager.Instance == null)
                return;

            GameTickManager.Instance.Register(this);
            _registered = true;
        }

        private void Unregister()
        {
            if (!_registered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
            {
                tickManager.Unregister(this);
            }

            _registered = false;
        }
    }
}
