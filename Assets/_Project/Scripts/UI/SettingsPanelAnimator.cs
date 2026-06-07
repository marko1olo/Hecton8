using Hecton8.Core;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Hecton8.UI
{
    internal static class SettingsPanelAnimatorLayout
    {
        public const int GroupStateStrideBytes = 16;
    }

    /// <summary>
    /// Settings panel animator - staggered fade-in for UI elements.
    /// Zero-GC: late-frame state machine, cached CanvasGroup references, no coroutines.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Settings Panel Animator")]
    public sealed class SettingsPanelAnimator : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        // ----------------------------------------------------------
        // INSPECTOR
        // ----------------------------------------------------------

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

        [Header("=== FADE OUT ===")]
        [SerializeField] private bool supportFadeOut = true;
        [SerializeField] private float fadeOutDuration = 0.2f;

        // ----------------------------------------------------------
        // FIELDS
        // ----------------------------------------------------------

        private const byte AnimationIncomplete = 0;
        private const byte AnimationComplete = 1;

        private enum State { Idle, FadingIn, FadingOut }

        private State _state;
        private float _timer;
        private bool _registered;
        private bool _hotSwapListenerRegistered;
        private System.Action _onFadeOutComplete;

        [StructLayout(LayoutKind.Explicit, Size = SettingsPanelAnimatorLayout.GroupStateStrideBytes)]
        private struct GroupState
        {
            [FieldOffset(0)]
            public float startTime;
            [FieldOffset(4)]
            public float duration;
            [FieldOffset(8)]
            public byte completed;
            [FieldOffset(9)]
            private byte _pad0;
            [FieldOffset(10)]
            private ushort _pad1;
            [FieldOffset(12)]
            private uint _pad2;
        }

        private GroupState _headerState;
        private GroupState[] _presetStates;
        private GroupState[] _settingsStates;
        private GroupState _actionsState;

        // ----------------------------------------------------------
        // LIFECYCLE
        // ----------------------------------------------------------

        private void Awake()
        {
            InitializeStates();
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
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            Unregister();
            if (currentService != null && isActiveAndEnabled)
            {
                if (_state != State.Idle)
                    TryRegister();
            }
        }

        // ----------------------------------------------------------
        // LATE FRAME
        // ----------------------------------------------------------

        public void LateFrameTick()
        {
            if (_state == State.Idle)
            {
                Unregister();
                return;
            }

            float dt = Mathf.Max(0f, SystemDispatcher.CurrentFrameUnscaledDeltaTime);
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
            if (_headerState.completed != AnimationComplete)
                AnimateGroupFadeIn(headerGroup, ref _headerState);

            // Animate preset buttons
            if (_presetStates != null && presetButtonGroups != null)
            {
                for (int i = 0; i < _presetStates.Length && i < presetButtonGroups.Length; i++)
                {
                    if (_presetStates[i].completed != AnimationComplete)
                        AnimateGroupFadeIn(presetButtonGroups[i], ref _presetStates[i]);
                }
            }

            // Animate settings rows
            if (_settingsStates != null && settingsRowGroups != null)
            {
                for (int i = 0; i < _settingsStates.Length && i < settingsRowGroups.Length; i++)
                {
                    if (_settingsStates[i].completed != AnimationComplete)
                        AnimateGroupFadeIn(settingsRowGroups[i], ref _settingsStates[i]);
                }
            }

            // Animate action buttons
            if (_actionsState.completed != AnimationComplete)
                AnimateGroupFadeIn(actionButtonsGroup, ref _actionsState);

            // Check if all animations complete
            if (IsAnimationComplete())
            {
                _state = State.Idle;
                Unregister();
            }
        }

        private void TickFadeOut()
        {
            float duration = fadeOutDuration > 0.0001f ? fadeOutDuration : 0.0001f;
            float t = Mathf.Clamp01(_timer / duration);
            float alpha = 1f - SmoothStep01(t);

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
                System.Action onComplete = _onFadeOutComplete;
                _onFadeOutComplete = null;
                _state = State.Idle;
                Unregister();
                onComplete?.Invoke();
            }
        }

        // ----------------------------------------------------------
        // PUBLIC API
        // ----------------------------------------------------------

        /// <summary>
        /// Start fade-in animation.
        /// </summary>
        public void PlayFadeIn()
        {
            _state = State.FadingIn;
            _timer = 0f;
            _onFadeOutComplete = null;
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
                _state = State.Idle;
                _onFadeOutComplete = null;
                Unregister();
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
            _onFadeOutComplete = null;
            ShowAllGroups();
            Unregister();
        }

        /// <summary>
        /// Check if animation is currently playing.
        /// </summary>
        public bool IsPlaying()
        {
            return _state != State.Idle;
        }

        // ----------------------------------------------------------
        // PRIVATE
        // ----------------------------------------------------------

        private void InitializeStates()
        {
            // Header
            _headerState = new GroupState
            {
                startTime = headerDelay,
                duration = headerDuration,
                completed = AnimationIncomplete
            };

            // Preset buttons
            int presetCount = presetButtonGroups != null ? presetButtonGroups.Length : 0;
            if (presetCount > 0)
            {
                if (_presetStates == null || _presetStates.Length != presetCount)
                    _presetStates = new GroupState[presetCount]; // COLD ALLOC: GroupState[presetCount] — cached preset button animation states

                for (int i = 0; i < presetCount; i++)
                {
                    _presetStates[i] = new GroupState
                    {
                        startTime = presetDelay + i * presetStagger,
                        duration = presetDuration,
                        completed = AnimationIncomplete
                    };
                }
            }
            else
            {
                _presetStates = null;
            }

            // Settings rows
            int settingsCount = settingsRowGroups != null ? settingsRowGroups.Length : 0;
            if (settingsCount > 0)
            {
                if (_settingsStates == null || _settingsStates.Length != settingsCount)
                    _settingsStates = new GroupState[settingsCount]; // COLD ALLOC: GroupState[settingsCount] — cached settings row animation states

                for (int i = 0; i < settingsCount; i++)
                {
                    _settingsStates[i] = new GroupState
                    {
                        startTime = settingsDelay + i * settingsStagger,
                        duration = settingsDuration,
                        completed = AnimationIncomplete
                    };
                }
            }
            else
            {
                _settingsStates = null;
            }

            // Action buttons
            _actionsState = new GroupState
            {
                startTime = actionsDelay,
                duration = actionsDuration,
                completed = AnimationIncomplete
            };
        }

        private void AnimateGroupFadeIn(CanvasGroup group, ref GroupState state)
        {
            if (group == null || state.completed == AnimationComplete)
                return;

            if (_timer < state.startTime)
                return;

            float elapsed = _timer - state.startTime;
            float duration = state.duration > 0.0001f ? state.duration : 0.0001f;
            float t = Mathf.Clamp01(elapsed / duration);
            float alpha = SmoothStep01(t);
            group.alpha = alpha;

            if (t >= 1f)
                state.completed = AnimationComplete;
        }

        private bool IsAnimationComplete()
        {
            if (_headerState.completed != AnimationComplete)
                return false;

            if (_presetStates != null)
            {
                for (int i = 0; i < _presetStates.Length; i++)
                {
                    if (_presetStates[i].completed != AnimationComplete)
                        return false;
                }
            }

            if (_settingsStates != null)
            {
                for (int i = 0; i < _settingsStates.Length; i++)
                {
                    if (_settingsStates[i].completed != AnimationComplete)
                        return false;
                }
            }

            if (_actionsState.completed != AnimationComplete)
                return false;

            return true;
        }

        private static float SmoothStep01(float t)
        {
            return t * t * (3f - (2f * t));
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
            if (_registered || !Application.isPlaying)
                return;

            if (SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI))
            {
                _registered = true;
                return;
            }
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
    }
}
