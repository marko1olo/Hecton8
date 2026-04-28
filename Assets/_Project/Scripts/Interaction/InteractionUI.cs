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
    public sealed class InteractionUI : MonoBehaviour
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
        private string _cachedInteractPrefix;
        private CanvasGroup _promptCanvasGroup;

        // COLD ALLOC: char[192] - hover prompt rich-text buffer - owner: InteractionUI
        private readonly char[] _charBuffer = new char[192];

        private void Awake()
        {
            InitializePromptContainer();
        }

        private void OnEnable()
        {
            InteractionEvents.OnHoverChanged += HandleHoverChanged;

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
            InteractionEvents.OnHoverChanged -= HandleHoverChanged;

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
                if (localizationManager != null)
                    interactText = localizationManager.ExpandText(interactText);

                int totalLength = WriteToBuffer(_cachedInteractPrefix, interactText);
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
                _cachedInteractPrefix = manager != null
                    ? manager.GetExpandedOrFallback(manager.CurrentLanguage, LocalizationKeys.INTERACT_DEFAULT_PROMPT_FORMAT, inputPrefix + "{0} {1}")
                    : inputPrefix + "{0} {1}";
                _cachedInteractPrefix = ExtractPrefix(_cachedInteractPrefix);
                return;
            }

            if (InputManager.Instance != null &&
                InputManager.Instance.TryGetBindingMarkupForToken("interact", out string markup) &&
                !string.IsNullOrEmpty(markup))
            {
                _cachedInteractPrefix = string.Concat(markup, " ");
                return;
            }

            LocalizationManager localizationManager = LocalizationManager.Instance;
            if (localizationManager != null)
            {
                string fallbackTemplate = localizationManager.GetExpandedOrFallback(
                    localizationManager.CurrentLanguage,
                    LocalizationKeys.INTERACT_DEFAULT_PROMPT_FORMAT,
                    inputPrefix + "{0} {1}");
                _cachedInteractPrefix = ExtractPrefix(fallbackTemplate);
                return;
            }

            _cachedInteractPrefix = inputPrefix;
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

            if (!promptContainer.activeSelf)
                promptContainer.SetActive(true);

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
            if (_promptCanvasGroup != null)
            {
                _promptCanvasGroup.alpha = visible ? 1f : 0f;
                return;
            }

            if (promptContainer.activeSelf != visible)
                promptContainer.SetActive(visible);
        }

        private static string ExtractPrefix(string template)
        {
            if (string.IsNullOrEmpty(template))
                return string.Empty;

            int placeholderIndex = template.IndexOf("{0}", StringComparison.Ordinal);
            if (placeholderIndex <= 0)
                return template;

            return template.Substring(0, placeholderIndex);
        }

        private int WriteToBuffer(string prefix, string body)
        {
            int bufferLength = _charBuffer.Length;
            int offset = 0;

            if (!string.IsNullOrEmpty(prefix))
            {
                int prefixLength = Mathf.Min(prefix.Length, bufferLength);
                for (int i = 0; i < prefixLength; i++)
                    _charBuffer[offset++] = prefix[i];
            }

            if (!string.IsNullOrEmpty(body))
            {
                int remaining = bufferLength - offset;
                int bodyLength = Mathf.Min(body.Length, remaining);
                for (int i = 0; i < bodyLength; i++)
                    _charBuffer[offset++] = body[i];
            }

            return offset;
        }
    }
}
