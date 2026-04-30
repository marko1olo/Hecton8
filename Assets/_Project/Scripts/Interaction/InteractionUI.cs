// ============================================================================
// HECTON-8 - InteractionUI.cs
// Event-driven interaction prompt owner for hover state transitions.
// No Update loop. No polling. Prefix refreshes only on input/layout changes.
// ============================================================================

namespace Hecton8.Interaction
{
    using System;
    using Hecton.Localization;
    using Hecton8.Input;
    using Hecton8.UI;
    using TMPro;
    using UnityEngine;

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Interaction UI")]
    public sealed class InteractionUI : MonoBehaviour, IInteractionEventListener
    {
        [Header("UI References")]
        [SerializeField, Tooltip("The TextMeshProUGUI label that displays the interaction prompt.")]
        private TextMeshProUGUI promptLabel;

        [SerializeField, Tooltip("Parent container GameObject that owns the prompt visuals.")]
        private GameObject promptContainer;

        [Header("Formatting")]
        [SerializeField, Tooltip("Fallback prefix used before runtime bindings are available.")]
        private string inputPrefix = "<button:interact> ";

        private IInteractable _lastDisplayedTarget;
        private int _cachedInteractPrefixLength;
        private CanvasGroup _promptCanvasGroup;

        // COLD ALLOC: char[192] - hover prompt rich-text buffer - owner: InteractionUI
        private readonly char[] _charBuffer = new char[192];
        // COLD ALLOC: char[96] - cached interaction prefix staging buffer - owner: InteractionUI
        private readonly char[] _prefixBuffer = new char[96];

        private void Awake()
        {
            InitializePromptContainer();
        }

        private void OnEnable()
        {
            InteractionEvents.Register(this);

            RebindingManager rebindingManager = RebindingManager.Instance;
            if (rebindingManager != null)
            {
                rebindingManager.OnRebindCompleted += HandleBindingChanged;
                rebindingManager.OnRebindCanceled += HandleBindingCanceled;
                rebindingManager.OnOverridesLoaded += HandleOverridesLoaded;
                rebindingManager.OnOverridesCleared += HandleOverridesCleared;
            }

            if (InputManager.Instance != null)
                InputManager.Instance.OnInputDisplayStyleChanged += HandleInputDisplayStyleChanged;

            LocalizationManager.OnLanguageChanged += HandleLanguageChanged;

            InitializePromptContainer();
            ConfigurePromptLabel();
            RefreshInteractPrefixCache();
            HidePrompt();
        }

        private void OnDisable()
        {
            InteractionEvents.Unregister(this);

            RebindingManager rebindingManager = RebindingManager.Instance;
            if (rebindingManager != null)
            {
                rebindingManager.OnRebindCompleted -= HandleBindingChanged;
                rebindingManager.OnRebindCanceled -= HandleBindingCanceled;
                rebindingManager.OnOverridesLoaded -= HandleOverridesLoaded;
                rebindingManager.OnOverridesCleared -= HandleOverridesCleared;
            }

            if (InputManager.Instance != null)
                InputManager.Instance.OnInputDisplayStyleChanged -= HandleInputDisplayStyleChanged;

            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;

            HidePrompt();
        }

        private void HandleHoverChanged(IInteractable target)
        {
            if (target != null)
            {
                if (ReferenceEquals(target, _lastDisplayedTarget))
                    return;

                _lastDisplayedTarget = target;
                ShowPrompt(target);
                return;
            }

            _lastDisplayedTarget = null;
            HidePrompt();
        }

        public void OnInteractionEvent(in InteractionEventPayload payload)
        {
            if ((InteractionEventType)payload.EventType != InteractionEventType.HoverChanged)
                return;

            InteractionEvents.TryResolveTarget(in payload, out IInteractable target);
            HandleHoverChanged(target);
        }

        private void HandleBindingChanged(string actionName, string actionMap, int bindingIndex, string display)
        {
            if (!Application.isPlaying)
                return;

            if (!string.Equals(actionMap, "Player", StringComparison.OrdinalIgnoreCase))
                return;

            if (!string.Equals(actionName, "Interact", StringComparison.OrdinalIgnoreCase))
                return;

            RefreshInteractPrefixCache();
            RefreshCurrentPrompt();
        }

        private void HandleBindingCanceled(string actionName, string actionMap, int bindingIndex)
        {
            if (!Application.isPlaying)
                return;

            if (!string.Equals(actionMap, "Player", StringComparison.OrdinalIgnoreCase))
                return;

            if (!string.Equals(actionName, "Interact", StringComparison.OrdinalIgnoreCase))
                return;

            RefreshInteractPrefixCache();
            RefreshCurrentPrompt();
        }

        private void HandleOverridesLoaded()
        {
            RefreshInteractPrefixCache();
            RefreshCurrentPrompt();
        }

        private void HandleOverridesCleared()
        {
            RefreshInteractPrefixCache();
            RefreshCurrentPrompt();
        }

        private void HandleInputDisplayStyleChanged(InputDisplayStyle displayStyle)
        {
            RefreshInteractPrefixCache();
            RefreshCurrentPrompt();
        }

        private void HandleLanguageChanged(GameLanguage language)
        {
            ConfigurePromptLabel();
            RefreshInteractPrefixCache();
            RefreshCurrentPrompt();
        }

        private void RefreshCurrentPrompt()
        {
            if (_lastDisplayedTarget == null)
                return;

            ShowPrompt(_lastDisplayedTarget);
        }

        private void ShowPrompt(IInteractable target)
        {
            if (promptLabel != null)
            {
                string interactText = target.GetInteractText();
                LocalizationManager localizationManager = LocalizationManager.Instance;
                if (localizationManager != null && ContainsExpansionToken(interactText))
                    interactText = localizationManager.ExpandText(interactText);

                int totalLength = WriteToBuffer(
                    _prefixBuffer.AsSpan(0, _cachedInteractPrefixLength),
                    interactText.AsSpan());
                promptLabel.SetCharArray(_charBuffer, 0, totalLength);
            }

            SetPromptVisible(true);
        }

        private void HidePrompt()
        {
            SetPromptVisible(false);

            _lastDisplayedTarget = null;
        }

        private void RefreshInteractPrefixCache()
        {
            if (!Application.isPlaying)
            {
                LocalizationManager manager = LocalizationManager.Instance;
                string template = manager != null
                    ? manager.GetExpandedOrFallback(manager.CurrentLanguage, LocalizationKeys.INTERACT_DEFAULT_PROMPT_FORMAT, inputPrefix + "{0} {1}")
                    : inputPrefix + "{0} {1}";
                CachePrefixFromTemplate(template);
                return;
            }

            if (InputManager.Instance != null &&
                InputManager.Instance.TryGetBindingMarkupForToken("interact", out string markup) &&
                !string.IsNullOrEmpty(markup))
            {
                CachePrefixLiteral(markup.AsSpan(), appendTrailingSpace: true);
                return;
            }

            LocalizationManager localizationManager = LocalizationManager.Instance;
            if (localizationManager != null)
            {
                string template = localizationManager.GetExpandedOrFallback(
                    localizationManager.CurrentLanguage,
                    LocalizationKeys.INTERACT_DEFAULT_PROMPT_FORMAT,
                    inputPrefix + "{0} {1}");
                CachePrefixFromTemplate(template);
                return;
            }

            CachePrefixLiteral(inputPrefix.AsSpan(), appendTrailingSpace: false);
        }

        private void ConfigurePromptLabel()
        {
            if (promptLabel == null)
                return;

            LocalizedTMPAutoSizer.Configure(
                promptLabel,
                promptLabel.fontSize * 0.72f,
                promptLabel.fontSize,
                TextOverflowModes.Ellipsis,
                TextWrappingModes.Normal);
        }

        private void InitializePromptContainer()
        {
            if (promptContainer == null)
                return;

            if (_promptCanvasGroup == null && !promptContainer.TryGetComponent(out _promptCanvasGroup))
            {
                _promptCanvasGroup = promptContainer.AddComponent<CanvasGroup>(); // COLD ALLOC: CanvasGroup[1] - prompt visibility gating without SetActive - owner: InteractionUI
            }

            if (_promptCanvasGroup == null)
                return;

            _promptCanvasGroup.blocksRaycasts = false;
            _promptCanvasGroup.interactable = false;
        }

        private void SetPromptVisible(bool visible)
        {
            if (promptContainer == null)
                return;

            InitializePromptContainer();
            if (_promptCanvasGroup == null)
                return;

            _promptCanvasGroup.alpha = visible ? 1f : 0f;
            _promptCanvasGroup.blocksRaycasts = false;
            _promptCanvasGroup.interactable = false;
        }

        private void CachePrefixFromTemplate(string template)
        {
            if (string.IsNullOrEmpty(template))
            {
                _cachedInteractPrefixLength = 0;
                return;
            }

            int placeholderIndex = template.IndexOf("{0}", StringComparison.Ordinal);
            ReadOnlySpan<char> prefixSpan = placeholderIndex <= 0
                ? template.AsSpan()
                : template.AsSpan(0, placeholderIndex);
            CachePrefixLiteral(prefixSpan, appendTrailingSpace: false);
        }

        private void CachePrefixLiteral(ReadOnlySpan<char> prefix, bool appendTrailingSpace)
        {
            int maxLength = _prefixBuffer.Length;
            int safeLength = Mathf.Min(prefix.Length, appendTrailingSpace ? maxLength - 1 : maxLength);
            prefix.Slice(0, safeLength).CopyTo(_prefixBuffer);

            int cursor = safeLength;
            if (appendTrailingSpace && cursor < maxLength)
                _prefixBuffer[cursor++] = ' ';

            _cachedInteractPrefixLength = cursor;
        }

        private static bool ContainsExpansionToken(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            return value.IndexOf('<') >= 0;
        }

        private int WriteToBuffer(ReadOnlySpan<char> prefix, ReadOnlySpan<char> body)
        {
            int bufferLength = _charBuffer.Length;
            int offset = 0;

            if (!prefix.IsEmpty)
            {
                int prefixLength = Mathf.Min(prefix.Length, bufferLength);
                prefix.Slice(0, prefixLength).CopyTo(_charBuffer);
                offset += prefixLength;
            }

            if (!body.IsEmpty)
            {
                int remaining = bufferLength - offset;
                int bodyLength = Mathf.Min(body.Length, remaining);
                body.Slice(0, bodyLength).CopyTo(_charBuffer.AsSpan(offset));
                offset += bodyLength;
            }

            return offset;
        }
    }
}
