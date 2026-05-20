// ============================================================================
// HECTON-8 - InteractionUI.cs
// Event-driven interaction prompt owner for hover state transitions.
// No Update loop. No polling. Prefix refreshes only on input/layout changes.
// ============================================================================

namespace Hecton8.Interaction
{
    using System;
    using Hecton.Localization;
    using Hecton8.Core;
    using Hecton8.Input;
    using Hecton8.UI;
    using TMPro;
    using Unity.Mathematics;
    using UnityEngine;

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Interaction UI")]
    public sealed class InteractionUI : MonoBehaviour, IInteractionEventListener, ILocalizationLanguageChangedListener, IGlobalRegistryHotSwapListener
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
        private IInputBindingService _subscribedInputBindingService;
        private InputManager _subscribedInputManager;
        private LocalizationManager _localizationManager;
        private bool _localizationColdResolved;
        private bool _hotSwapListenerRegistered;

        // COLD ALLOC: char[192] — hover prompt rich-text buffer — owner: InteractionUI
        private readonly char[] _charBuffer = new char[192];
        // COLD ALLOC: char[96] — cached interaction prefix staging buffer — owner: InteractionUI
        private readonly char[] _prefixBuffer = new char[96];

        private void Awake()
        {
            InitializePromptContainer();
        }

        private void OnEnable()
        {
            InteractionEvents.Register(this);

            TryRegisterHotSwapListener();
            CacheLocalizationCold(forceRefresh: true);
            SubscribeInputBindingServiceIfAvailable();

            SubscribeInputManagerIfAvailable();

            LocalizationEvents.RegisterLanguageListener(this);

            InitializePromptContainer();
            ConfigurePromptLabel();
            RefreshInteractPrefixCache();
            HidePrompt();
        }

        private void Start()
        {
            TryRegisterHotSwapListener();
            CacheLocalizationCold(forceRefresh: true);
            SubscribeInputBindingServiceIfAvailable();
            SubscribeInputManagerIfAvailable();
            RefreshInteractPrefixCache();
            RefreshCurrentPrompt();
        }

        private void OnDisable()
        {
            InteractionEvents.Unregister(this);

            UnsubscribeInputBindingService();

            UnsubscribeInputManager();

            TryUnregisterHotSwapListener();

            LocalizationEvents.UnregisterLanguageListener(this);

            HidePrompt();
        }

        private void OnDestroy()
        {
            UnsubscribeInputBindingService();
            UnsubscribeInputManager();
            TryUnregisterHotSwapListener();
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

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

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
                LocalizationManager localizationManager = ResolveLocalizationManager();
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
                LocalizationManager manager = ResolveLocalizationManager();
                string template = manager != null
                    ? manager.GetExpandedOrFallback(manager.CurrentLanguage, LocalizationKeys.INTERACT_DEFAULT_PROMPT_FORMAT, inputPrefix + "{0} {1}")
                    : inputPrefix + "{0} {1}";
                CachePrefixFromTemplate(template);
                return;
            }

            SubscribeInputManagerIfAvailable();
            if (_subscribedInputManager != null && CacheInteractBindingMarkup(_subscribedInputManager))
                return;

            LocalizationManager localizationManager = ResolveLocalizationManager();
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
                _promptCanvasGroup = promptContainer.AddComponent<CanvasGroup>(); // COLD ALLOC: CanvasGroup[1] — prompt visibility gating without SetActive — owner: InteractionUI
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
            int safeLength = math.min(prefix.Length, appendTrailingSpace ? maxLength - 1 : maxLength);
            prefix.Slice(0, safeLength).CopyTo(_prefixBuffer);

            int cursor = safeLength;
            if (appendTrailingSpace && cursor < maxLength)
                _prefixBuffer[cursor++] = ' ';

            _cachedInteractPrefixLength = cursor;
        }

        private void SubscribeInputManagerIfAvailable()
        {
            if (_subscribedInputManager != null)
                return;

            InputManager inputManager = GlobalRegistry.NativeInputManager;
            if (inputManager == null)
                return;

            _subscribedInputManager = inputManager;
            _subscribedInputManager.OnInputDisplayStyleChanged += HandleInputDisplayStyleChanged;
        }

        private void SubscribeInputBindingServiceIfAvailable()
        {
            SubscribeInputBindingServiceIfAvailable(GlobalRegistry.InputBinding);
        }

        private void SubscribeInputBindingServiceIfAvailable(IInputBindingService rebindingManager)
        {
            if (_subscribedInputBindingService != null)
                return;

            if (rebindingManager == null)
                return;

            _subscribedInputBindingService = rebindingManager;
            _subscribedInputBindingService.OnRebindCompleted += HandleBindingChanged;
            _subscribedInputBindingService.OnRebindCanceled += HandleBindingCanceled;
            _subscribedInputBindingService.OnOverridesLoaded += HandleOverridesLoaded;
            _subscribedInputBindingService.OnOverridesCleared += HandleOverridesCleared;
        }

        private void UnsubscribeInputManager()
        {
            if (_subscribedInputManager == null)
                return;

            _subscribedInputManager.OnInputDisplayStyleChanged -= HandleInputDisplayStyleChanged;
            _subscribedInputManager = null;
        }

        private void UnsubscribeInputBindingService()
        {
            if (_subscribedInputBindingService == null)
                return;

            _subscribedInputBindingService.OnRebindCompleted -= HandleBindingChanged;
            _subscribedInputBindingService.OnRebindCanceled -= HandleBindingCanceled;
            _subscribedInputBindingService.OnOverridesLoaded -= HandleOverridesLoaded;
            _subscribedInputBindingService.OnOverridesCleared -= HandleOverridesCleared;
            _subscribedInputBindingService = null;
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Input:
                    UnsubscribeInputManager();
                    if (isActiveAndEnabled)
                        SubscribeInputManagerIfAvailable();
                    break;

                case GlobalRegistryServiceSlot.InputBinding:
                    UnsubscribeInputBindingService();
                    if (isActiveAndEnabled)
                        SubscribeInputBindingServiceIfAvailable(currentService as IInputBindingService);
                    break;

                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _localizationManager = currentService as LocalizationManager;
                    _localizationColdResolved = _localizationManager != null;
                    break;

                default:
                    return;
            }

            RefreshInteractPrefixCache();
            RefreshCurrentPrompt();
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

        private void CacheLocalizationCold(bool forceRefresh = false)
        {
            if (!forceRefresh && _localizationColdResolved && _localizationManager != null)
                return;

            _localizationManager = Hecton8.Core.GlobalRegistry.Localization;
            _localizationColdResolved = _localizationManager != null;
        }

        private LocalizationManager ResolveLocalizationManager()
        {
            CacheLocalizationCold();
            return _localizationManager;
        }

        private bool CacheInteractBindingMarkup(InputManager inputManager)
        {
            int offset = 0;
            if (!TryAppendPrefixLiteral("<b><color=#AEE8FF>".AsSpan(), ref offset))
                return false;

            if (offset >= _prefixBuffer.Length)
                return false;

            _prefixBuffer[offset++] = inputManager.CurrentDisplayStyle == InputDisplayStyle.Gamepad
                ? '\u25C6'
                : '\u2328';

            if (!TryAppendPrefixLiteral("</color> ".AsSpan(), ref offset))
                return false;

            if (!inputManager.TryWriteBindingDisplayString(
                    "Interact",
                    "Player",
                    -1,
                    _prefixBuffer,
                    offset,
                    out int displayLength) ||
                displayLength <= 0)
            {
                return false;
            }

            offset += displayLength;
            if (!TryAppendPrefixLiteral("</b> ".AsSpan(), ref offset))
                return false;

            _cachedInteractPrefixLength = offset;
            return true;
        }

        private bool TryAppendPrefixLiteral(ReadOnlySpan<char> literal, ref int offset)
        {
            if (literal.IsEmpty || offset < 0 || offset > _prefixBuffer.Length - literal.Length)
                return false;

            literal.CopyTo(_prefixBuffer.AsSpan(offset));
            offset += literal.Length;
            return true;
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
                int prefixLength = math.min(prefix.Length, bufferLength);
                prefix.Slice(0, prefixLength).CopyTo(_charBuffer);
                offset += prefixLength;
            }

            if (!body.IsEmpty)
            {
                int remaining = bufferLength - offset;
                int bodyLength = math.min(body.Length, remaining);
                body.Slice(0, bodyLength).CopyTo(_charBuffer.AsSpan(offset));
                offset += bodyLength;
            }

            return offset;
        }
    }
}
