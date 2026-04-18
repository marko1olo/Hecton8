using UnityEngine;
using Hecton8.Core;

namespace Hecton8.UI
{
    /// <summary>
    /// Settings panel animator — staggered fade-in for UI elements.
    /// Zero-GC: ITickable state machine, cached CanvasGroup references, no coroutines.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Settings Panel Animator")]
    public sealed class SettingsPanelAnimator : MonoBehaviour, ITickable
    {
        // ══════════════════════════════════════════════════════════
        // INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("=== ANIMATION GROUPS ===")]
        [SerializeField] private CanvasGroup headerGroup;
        [SerializeField] private CanvasGroup[] presetButtonGroups;
        [SerializeField] private CanvasGroup[] settingsRowGroups;
        [SerializeField] private CanvasGroup actionButtonsGroup;

        [Header("=== TIMING ===")]
        [SerializeField] private float headerDelay = 0f;
        [SerializeField] private float headerDuration = 0.15f;
        [SerializeField] private float presetDelay = 0.15f;
        [SerializeField] private float presetDuration = 0.2f;
        [SerializeField] private float presetStagger = 0.05f;
        [SerializeField] private float settingsDelay = 0.35f;
        [SerializeField] private float settingsDuration = 0.25f;
        [SerializeField] private float settingsStagger = 0.08f;
        [SerializeField] private float actionsDelay = 0.6f;
        [SerializeField] private float actionsDuration = 0.3f;

        [Header("=== EASING ===")]
        [SerializeField] private AnimationCurve fadeInCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve fadeOutCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        [Header("=== FADE OUT ===")]
        [SerializeField] private bool supportFadeOut = true;
        [SerializeField] private float fadeOutDuration = 0.2f;

        // ══════════════════════════════════════════════════════════
        // FIELDS
        // ══════════════════════════════════════════════════════════

        private enum State { Idle, FadingIn, FadingOut }

        private State _state;
        private float _timer;
        private bool _registered;
        private System.Action _onFadeOutComplete;

        // Animation state per group
        private struct GroupState
        {
            public float startTime;
            public float duration;
            public bool completed;
        }

        private GroupState _headerState;
        private GroupState[] _presetStates;
        private GroupState[] _settingsStates;
        private GroupState _actionsState;

        // ══════════════════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            InitializeStates();
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
            if (_state == State.Idle)
                return;

            _timer += dt;

            if (_state == State.FadingIn)
            {
                TickFadeIn();
            }
            else if (_state == State.FadingOut)
            {
                TickFadeOut();
            }
        }

        private void TickFadeIn()
        {
            // Animate header
            if (!_headerState.completed)
                AnimateGroupFadeIn(headerGroup, ref _headerState);

            // Animate preset buttons
            if (_presetStates != null && presetButtonGroups != null)
            {
                for (int i = 0; i < _presetStates.Length && i < presetButtonGroups.Length; i++)
                {
                    if (!_presetStates[i].completed)
                        AnimateGroupFadeIn(presetButtonGroups[i], ref _presetStates[i]);
                }
            }

            // Animate settings rows
            if (_settingsStates != null && settingsRowGroups != null)
            {
                for (int i = 0; i < _settingsStates.Length && i < settingsRowGroups.Length; i++)
                {
                    if (!_settingsStates[i].completed)
                        AnimateGroupFadeIn(settingsRowGroups[i], ref _settingsStates[i]);
                }
            }

            // Animate action buttons
            if (!_actionsState.completed)
                AnimateGroupFadeIn(actionButtonsGroup, ref _actionsState);

            // Check if all animations complete
            if (IsAnimationComplete())
                _state = State.Idle;
        }

        private void TickFadeOut()
        {
            float t = Mathf.Clamp01(_timer / fadeOutDuration);
            float alpha = fadeOutCurve.Evaluate(t);

            // Fade out all groups simultaneously
            if (headerGroup != null)
                headerGroup.alpha = alpha;

            if (presetButtonGroups != null)
            {
                for (int i = 0; i < presetButtonGroups.Length; i++)
                {
                    if (presetButtonGroups[i] != null)
                        presetButtonGroups[i].alpha = alpha;
                }
            }

            if (settingsRowGroups != null)
            {
                for (int i = 0; i < settingsRowGroups.Length; i++)
                {
                    if (settingsRowGroups[i] != null)
                        settingsRowGroups[i].alpha = alpha;
                }
            }

            if (actionButtonsGroup != null)
                actionButtonsGroup.alpha = alpha;

            // Check if fade out complete
            if (t >= 1f)
            {
                _state = State.Idle;
                _onFadeOutComplete?.Invoke();
                _onFadeOutComplete = null;
            }
        }

        // ══════════════════════════════════════════════════════════
        // PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Start fade-in animation.
        /// </summary>
        public void PlayFadeIn()
        {
            _state = State.FadingIn;
            _timer = 0f;
            InitializeStates();
            HideAllGroups();
            TryRegister();
        }

        /// <summary>
        /// Start fade-out animation.
        /// </summary>
        /// <param name="onComplete">Callback when fade-out completes (optional)</param>
        public void PlayFadeOut(System.Action onComplete = null)
        {
            if (!supportFadeOut)
            {
                onComplete?.Invoke();
                return;
            }

            _state = State.FadingOut;
            _timer = 0f;
            _onFadeOutComplete = onComplete;
            TryRegister();
        }

        /// <summary>
        /// Skip animation and show all groups immediately.
        /// </summary>
        public void SkipAnimation()
        {
            _state = State.Idle;
            ShowAllGroups();
        }

        /// <summary>
        /// Check if animation is currently playing.
        /// </summary>
        public bool IsPlaying()
        {
            return _state != State.Idle;
        }

        // ══════════════════════════════════════════════════════════
        // PRIVATE
        // ══════════════════════════════════════════════════════════

        private void InitializeStates()
        {
            // Header
            _headerState = new GroupState
            {
                startTime = headerDelay,
                duration = headerDuration,
                completed = false
            };

            // Preset buttons
            if (presetButtonGroups != null && presetButtonGroups.Length > 0)
            {
                _presetStates = new GroupState[presetButtonGroups.Length]; // COLD ALLOC: GroupState[4] — preset button animation states
                for (int i = 0; i < presetButtonGroups.Length; i++)
                {
                    _presetStates[i] = new GroupState
                    {
                        startTime = presetDelay + i * presetStagger,
                        duration = presetDuration,
                        completed = false
                    };
                }
            }

            // Settings rows
            if (settingsRowGroups != null && settingsRowGroups.Length > 0)
            {
                _settingsStates = new GroupState[settingsRowGroups.Length]; // COLD ALLOC: GroupState[N] — settings row animation states
                for (int i = 0; i < settingsRowGroups.Length; i++)
                {
                    _settingsStates[i] = new GroupState
                    {
                        startTime = settingsDelay + i * settingsStagger,
                        duration = settingsDuration,
                        completed = false
                    };
                }
            }

            // Action buttons
            _actionsState = new GroupState
            {
                startTime = actionsDelay,
                duration = actionsDuration,
                completed = false
            };
        }

        private void AnimateGroupFadeIn(CanvasGroup group, ref GroupState state)
        {
            if (group == null || state.completed)
                return;

            if (_timer < state.startTime)
                return;

            float elapsed = _timer - state.startTime;
            float t = Mathf.Clamp01(elapsed / state.duration);
            float alpha = fadeInCurve.Evaluate(t);
            group.alpha = alpha;

            if (t >= 1f)
                state.completed = true;
        }

        private bool IsAnimationComplete()
        {
            if (!_headerState.completed)
                return false;

            if (_presetStates != null)
            {
                for (int i = 0; i < _presetStates.Length; i++)
                {
                    if (!_presetStates[i].completed)
                        return false;
                }
            }

            if (_settingsStates != null)
            {
                for (int i = 0; i < _settingsStates.Length; i++)
                {
                    if (!_settingsStates[i].completed)
                        return false;
                }
            }

            if (!_actionsState.completed)
                return false;

            return true;
        }

        private void HideAllGroups()
        {
            if (headerGroup != null)
                headerGroup.alpha = 0f;

            if (presetButtonGroups != null)
            {
                for (int i = 0; i < presetButtonGroups.Length; i++)
                {
                    if (presetButtonGroups[i] != null)
                        presetButtonGroups[i].alpha = 0f;
                }
            }

            if (settingsRowGroups != null)
            {
                for (int i = 0; i < settingsRowGroups.Length; i++)
                {
                    if (settingsRowGroups[i] != null)
                        settingsRowGroups[i].alpha = 0f;
                }
            }

            if (actionButtonsGroup != null)
                actionButtonsGroup.alpha = 0f;
        }

        private void ShowAllGroups()
        {
            if (headerGroup != null)
                headerGroup.alpha = 1f;

            if (presetButtonGroups != null)
            {
                for (int i = 0; i < presetButtonGroups.Length; i++)
                {
                    if (presetButtonGroups[i] != null)
                        presetButtonGroups[i].alpha = 1f;
                }
            }

            if (settingsRowGroups != null)
            {
                for (int i = 0; i < settingsRowGroups.Length; i++)
                {
                    if (settingsRowGroups[i] != null)
                        settingsRowGroups[i].alpha = 1f;
                }
            }

            if (actionButtonsGroup != null)
                actionButtonsGroup.alpha = 1f;
        }

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
