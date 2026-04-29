// ============================================================================
// HECTON-8 — ActionProgressHUD.cs
// Visual progress indicator for delayed player actions (eating, healing).
//
// ARCHITECTURE:
//   • Subscribes to PlayerActionController.OnActionProgress event.
//   • ITickable for smooth fade animations.
//   • CanvasGroup for alpha control (zero GC).
//   • Image.fillAmount for circular progress (zero GC).
//
// ZERO GC:
//   • No string operations in Tick.
//   • Pre-cached references.
//   • CanvasGroup.alpha instead of SetActive.
// ============================================================================

using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Items;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    /// <summary>
    /// HUD element displaying action progress as a circular fill.
    /// Subscribes to PlayerActionController events.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ActionProgressHUD : MonoBehaviour, ITickable, IUpdatable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── References ───────────────────────────────")]
        [Tooltip("Circular progress image (Filled Radial 360).")]
        [SerializeField] private Image progressImage;

        [Tooltip("Optional text showing action name.")]
        [SerializeField] private TMPro.TMP_Text actionText;

        [Header("── Animation ─────────────────────────────────")]
        [Tooltip("Fade in duration when action starts.")]
        [SerializeField, Range(0f, 0.5f)] private float fadeInDuration = 0.15f;

        [Tooltip("Fade out duration when action ends.")]
        [SerializeField, Range(0f, 0.5f)] private float fadeOutDuration = 0.1f;

        [Header("── Colors ────────────────────────────────────")]
        [Tooltip("Progress bar color for food items.")]
        [SerializeField] private Color foodColor = new Color(0.4f, 0.8f, 0.3f);

        [Tooltip("Progress bar color for medical items.")]
        [SerializeField] private Color medicalColor = new Color(0.8f, 0.3f, 0.3f);

        [Tooltip("Progress bar color for oxygen items.")]
        [SerializeField] private Color oxygenColor = new Color(0.3f, 0.6f, 0.9f);

        [Tooltip("Progress bar color for generic items.")]
        [SerializeField] private Color defaultColor = new Color(0.7f, 0.7f, 0.7f);

        // ══════════════════════════════════════════════════════════
        //  STATE
        // ══════════════════════════════════════════════════════════

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
        private float _targetAlpha;
        private bool _registered;
        private bool _eventSubscribed;
        private int _cachedActionTextVersion = -1;

        private static readonly char[] s_EatingTextChars = { 'E', 'a', 't', 'i', 'n', 'g', '.', '.', '.' };
        private static readonly char[] s_HealingTextChars = { 'A', 'p', 'p', 'l', 'y', 'i', 'n', 'g', '.', '.', '.' };
        private static readonly char[] s_OxygenTextChars = { 'I', 'n', 'h', 'a', 'l', 'i', 'n', 'g', '.', '.', '.' };
        private static readonly char[] s_DefaultTextChars = { 'U', 's', 'i', 'n', 'g', '.', '.', '.' };

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _currentAlpha = 0f;
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;

            if (progressImage != null)
            {
                progressImage.type = Image.Type.Filled;
                progressImage.fillMethod = Image.FillMethod.Radial360;
                progressImage.fillAmount = 0f;
            }
        }

        private void OnEnable()
        {
            SubscribeToEvents();
            TryRegister();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
            TryUnregister();
        }

        // ══════════════════════════════════════════════════════════
        //  EVENT SUBSCRIPTION
        // ══════════════════════════════════════════════════════════

        private void SubscribeToEvents()
        {
            if (_eventSubscribed) return;

            PlayerActionController controller = PlayerActionController.Instance;
            if (controller == null) return;

            controller.OnActionProgress += OnActionProgress;
            controller.OnActionCompleted += OnActionCompleted;
            controller.OnActionCancelled += OnActionCancelled;
            _eventSubscribed = true;
        }

        private void UnsubscribeFromEvents()
        {
            if (!_eventSubscribed) return;

            PlayerActionController controller = PlayerActionController.Instance;
            if (controller == null) return;

            controller.OnActionProgress -= OnActionProgress;
            controller.OnActionCompleted -= OnActionCompleted;
            controller.OnActionCancelled -= OnActionCancelled;
            _eventSubscribed = false;
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable
        // ══════════════════════════════════════════════════════════

        public void Tick(float deltaTime)
        {
            // Handle fade animations
            switch (_fadeState)
            {
                case FadeState.FadingIn:
                    _fadeTimer += deltaTime;
                    if (_fadeTimer >= fadeInDuration)
                    {
                        _currentAlpha = 1f;
                        _fadeState = FadeState.Visible;
                    }
                    else
                    {
                        _currentAlpha = _fadeTimer / fadeInDuration;
                    }
                    _canvasGroup.alpha = _currentAlpha;
                    break;

                case FadeState.FadingOut:
                    _fadeTimer += deltaTime;
                    if (_fadeTimer >= fadeOutDuration)
                    {
                        _currentAlpha = 0f;
                        _fadeState = FadeState.Hidden;
                    }
                    else
                    {
                        _currentAlpha = 1f - (_fadeTimer / fadeOutDuration);
                    }
                    _canvasGroup.alpha = _currentAlpha;
                    break;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  EVENT HANDLERS
        // ══════════════════════════════════════════════════════════

        private void OnActionProgress(float progress)
        {
            // Start fade in on first progress update
            if (_fadeState == FadeState.Hidden)
            {
                _fadeState = FadeState.FadingIn;
                _fadeTimer = 0f;
                UpdateActionText();
            }

            // Update progress bar
            if (progressImage != null)
            {
                progressImage.fillAmount = progress;
            }
        }

        private void OnActionCompleted(ItemData item)
        {
            // Snap to full then fade out
            if (progressImage != null)
            {
                progressImage.fillAmount = 1f;
            }

            StartFadeOut();
        }

        private void OnActionCancelled()
        {
            // Snap to current progress then fade out
            StartFadeOut();
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE METHODS
        // ══════════════════════════════════════════════════════════

        private void StartFadeOut()
        {
            if (_fadeState == FadeState.Hidden) return;

            _fadeState = FadeState.FadingOut;
            _fadeTimer = 0f;
        }

        private void UpdateActionText()
        {
            if (actionText == null) return;

            PlayerActionController controller = PlayerActionController.Instance;
            if (controller == null) return;

            ItemData item = controller.ActiveItem;
            if (item == null) return;

            Color color = defaultColor;
            char[] textBuffer = s_DefaultTextChars;
            int textLength = s_DefaultTextChars.Length;
            int textVersion = 0;

            if (item.integrityRestore > 0f)
            {
                color = medicalColor;
                textBuffer = s_HealingTextChars;
                textLength = s_HealingTextChars.Length;
                textVersion = 1;
            }
            else if (item.oxygenRestore > 0f)
            {
                color = oxygenColor;
                textBuffer = s_OxygenTextChars;
                textLength = s_OxygenTextChars.Length;
                textVersion = 2;
            }
            else if (item.hungerRestore > 0f || item.thirstRestore > 0f)
            {
                color = foodColor;
                textBuffer = s_EatingTextChars;
                textLength = s_EatingTextChars.Length;
                textVersion = 3;
            }

            if (progressImage != null)
                progressImage.color = color;

            if (_cachedActionTextVersion != textVersion)
            {
                actionText.SetCharArray(textBuffer, 0, textLength);
                _cachedActionTextVersion = textVersion;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  REGISTRATION
        // ══════════════════════════════════════════════════════════

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying) return;

            if (GlobalRegistry.Dispatcher == null) return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered) return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registered = false;
        }
    }
}
