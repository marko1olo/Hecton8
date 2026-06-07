// ============================================================================
// HECTON-8 - ActionProgressHUD.cs
// Visual progress indicator for delayed player actions (eating, healing).
//
// ARCHITECTURE:
//   - Consumes PlayerAction SignalBus snapshots.
//   - ILateFrameTickable for smooth VISUAL_SYNC fade animations.
//   - CanvasGroup for alpha control (zero GC).
//   - Image.fillAmount for circular progress (zero GC).
//
// ZERO GC:
//   - No string operations in Tick.
//   - Pre-cached references.
//   - CanvasGroup.alpha instead of SetActive.
// ============================================================================

using System;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    /// <summary>
    /// HUD element displaying action progress as a circular fill.
    /// Reads PlayerAction signal snapshots from the dispatcher late-frame lane.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    [DisallowMultipleComponent]
    public sealed class ActionProgressHUD : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        // ----------------------------------------------------------
        //  INSPECTOR
        // ----------------------------------------------------------

        [Header("-- References -------------------------------")]
        [Tooltip("Circular progress image (Filled Radial 360).")]
        [SerializeField] private Image progressImage;

        [Tooltip("Optional text showing action name.")]
        [SerializeField] private TMPro.TMP_Text actionText;

        [Header("-- Animation ---------------------------------")]
        [Tooltip("Fade in duration when action starts.")]
        [SerializeField, Range(0f, 0.5f)] private float fadeInDuration = 0.15f;

        [Tooltip("Fade out duration when action ends.")]
        [SerializeField, Range(0f, 0.5f)] private float fadeOutDuration = 0.1f;

        [Header("-- Colors ------------------------------------")]
        [Tooltip("Progress bar color for food items.")]
        [SerializeField] private Color foodColor = new Color(0.4f, 0.8f, 0.3f);

        [Tooltip("Progress bar color for medical items.")]
        [SerializeField] private Color medicalColor = new Color(0.8f, 0.3f, 0.3f);

        [Tooltip("Progress bar color for oxygen items.")]
        [SerializeField] private Color oxygenColor = new Color(0.3f, 0.6f, 0.9f);

        [Tooltip("Progress bar color for generic items.")]
        [SerializeField] private Color defaultColor = new Color(0.7f, 0.7f, 0.7f);

        // ----------------------------------------------------------
        //  STATE
        // ----------------------------------------------------------

        private enum FadeState
        {
            Hidden,
            FadingIn,
            Visible,
            FadingOut
        }

        private CanvasGroup _canvasGroup;
        private FadeState _fadeState = FadeState.Hidden;
        private float _fadeTimer;
        private float _currentAlpha;
        private bool _registered;
        private bool _hotSwapListenerRegistered;
        private int _cachedActionTextVersion = -1;

        private static readonly char[] s_EatingTextChars = { 'E', 'a', 't', 'i', 'n', 'g', '.', '.', '.' };
        private static readonly char[] s_HealingTextChars = { 'A', 'p', 'p', 'l', 'y', 'i', 'n', 'g', '.', '.', '.' };
        private static readonly char[] s_OxygenTextChars = { 'I', 'n', 'h', 'a', 'l', 'i', 'n', 'g', '.', '.', '.' };
        private static readonly char[] s_DefaultTextChars = { 'U', 's', 'i', 'n', 'g', '.', '.', '.' };

        // ----------------------------------------------------------
        //  LIFECYCLE
        // ----------------------------------------------------------

        private void Awake()
        {
            TryGetComponent(out _canvasGroup);
            _currentAlpha = 0f;
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;

            if (progressImage != null)
            {
                progressImage.type = Image.Type.Filled;
                progressImage.fillMethod = Image.FillMethod.Radial360;
                progressImage.fillAmount = 0f;
                progressImage.raycastTarget = false;
            }

            if (actionText != null)
                actionText.raycastTarget = false;
        }

        private void OnEnable()
        {
            TryRegisterHotSwapListener();
            TryRegister();
        }

        private void Start()
        {
            TryRegisterHotSwapListener();
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
            ResetTransientState();
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
        }

        // ----------------------------------------------------------
        //  SIGNAL SNAPSHOTS
        // ----------------------------------------------------------

        private void ProcessPlayerActionSignals()
        {
            ReadOnlySpan<PlayerActionProgressSignal> progressSignals = SignalBus<PlayerActionProgressSignal>.GetFrameSnapshot();
            for (int i = 0; i < progressSignals.Length; i++)
                HandleActionProgress(in progressSignals[i]);

            ReadOnlySpan<PlayerActionCompletedSignal> completedSignals = SignalBus<PlayerActionCompletedSignal>.GetFrameSnapshot();
            for (int i = 0; i < completedSignals.Length; i++)
                HandleActionCompleted(in completedSignals[i]);

            ReadOnlySpan<PlayerActionCancelledSignal> cancelledSignals = SignalBus<PlayerActionCancelledSignal>.GetFrameSnapshot();
            for (int i = 0; i < cancelledSignals.Length; i++)
                HandleActionCancelled(in cancelledSignals[i]);
        }

        // ----------------------------------------------------------
        //  ILateFrameTickable
        // ----------------------------------------------------------

        public void LateFrameTick()
        {
            float safeDeltaTime = math.max(0f, SystemDispatcher.CurrentFrameDeltaTime);
            ProcessPlayerActionSignals();

            // Handle fade animations
            switch (_fadeState)
            {
                case FadeState.FadingIn:
                    _fadeTimer += safeDeltaTime;
                    if (fadeInDuration <= 0.0001f || _fadeTimer >= fadeInDuration)
                    {
                        SetCanvasAlphaIfChanged(1f);
                        _fadeState = FadeState.Visible;
                    }
                    else
                    {
                        SetCanvasAlphaIfChanged(math.saturate(_fadeTimer * math.rcp(fadeInDuration)));
                    }
                    break;

                case FadeState.FadingOut:
                    _fadeTimer += safeDeltaTime;
                    if (fadeOutDuration <= 0.0001f || _fadeTimer >= fadeOutDuration)
                    {
                        SetCanvasAlphaIfChanged(0f);
                        _fadeState = FadeState.Hidden;
                    }
                    else
                    {
                        SetCanvasAlphaIfChanged(1f - math.saturate(_fadeTimer * math.rcp(fadeOutDuration)));
                    }
                    break;
            }
        }

        // ----------------------------------------------------------
        //  SIGNAL HANDLERS
        // ----------------------------------------------------------

        private void HandleActionProgress(in PlayerActionProgressSignal signal)
        {
            // Start fade in on first progress update
            if (_fadeState == FadeState.Hidden || _fadeState == FadeState.FadingOut)
            {
                _fadeState = FadeState.FadingIn;
                _fadeTimer = math.saturate(_currentAlpha) * fadeInDuration;
                UpdateActionText(signal.ActionKind);
            }

            // Update progress bar
            if (progressImage != null)
            {
                progressImage.fillAmount = math.saturate(signal.Progress01);
            }
        }

        private void HandleActionCompleted(in PlayerActionCompletedSignal signal)
        {
            // Snap to full then fade out
            if (progressImage != null)
            {
                progressImage.fillAmount = 1f;
            }

            StartFadeOut();
        }

        private void HandleActionCancelled(in PlayerActionCancelledSignal signal)
        {
            // Snap to current progress then fade out
            if (progressImage != null)
                progressImage.fillAmount = math.saturate(signal.Progress01);

            StartFadeOut();
        }

        // ----------------------------------------------------------
        //  PRIVATE METHODS
        // ----------------------------------------------------------

        private void StartFadeOut()
        {
            if (_fadeState == FadeState.Hidden) return;

            _fadeState = FadeState.FadingOut;
            _fadeTimer = (1f - math.saturate(_currentAlpha)) * fadeOutDuration;
        }

        private void ResetTransientState()
        {
            _fadeState = FadeState.Hidden;
            _fadeTimer = 0f;
            _currentAlpha = 0f;
            _cachedActionTextVersion = -1;

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
            }

            if (progressImage != null)
                progressImage.fillAmount = 0f;
        }

        private void SetCanvasAlphaIfChanged(float alpha)
        {
            float targetAlpha = math.saturate(alpha);
            if (math.abs(_currentAlpha - targetAlpha) <= 0.0001f)
                return;

            _currentAlpha = targetAlpha;
            if (_canvasGroup != null)
                _canvasGroup.alpha = targetAlpha;
        }

        private void UpdateActionText(byte actionKind)
        {
            if (actionText == null) return;

            Color color = defaultColor;
            char[] textBuffer = s_DefaultTextChars;
            int textLength = s_DefaultTextChars.Length;
            int textVersion = 0;

            switch (actionKind)
            {
                case PlayerActionProgressSignal.ActionKindMedical:
                    color = medicalColor;
                    textBuffer = s_HealingTextChars;
                    textLength = s_HealingTextChars.Length;
                    textVersion = 1;
                    break;
                case PlayerActionProgressSignal.ActionKindOxygen:
                    color = oxygenColor;
                    textBuffer = s_OxygenTextChars;
                    textLength = s_OxygenTextChars.Length;
                    textVersion = 2;
                    break;
                case PlayerActionProgressSignal.ActionKindFood:
                    color = foodColor;
                    textBuffer = s_EatingTextChars;
                    textLength = s_EatingTextChars.Length;
                    textVersion = 3;
                    break;
            }

            if (progressImage != null)
                progressImage.color = color;

            if (_cachedActionTextVersion != textVersion)
            {
                actionText.SetCharArray(textBuffer, 0, textLength);
                _cachedActionTextVersion = textVersion;
            }
        }

        // ----------------------------------------------------------
        //  REGISTRATION
        // ----------------------------------------------------------

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying) return;

            _registered = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            TryUnregister();
            if (currentService != null && isActiveAndEnabled)
                TryRegister();
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

        private void TryUnregister()
        {
            if (!_registered) return;

            SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
            _registered = false;
        }
    }
}
