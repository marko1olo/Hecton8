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
    using Hecton8.UI;
    using TMPro;
    using Unity.Mathematics;
    using UnityEngine;

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Interaction UI")]
    public sealed class InteractionUI : MonoBehaviour, IInteractionEventListener, ILocalizationLanguageChangedListener, IGlobalRegistryHotSwapListener, ILateFrameTickable
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
        private INativeInputManagerRuntime _subscribedInputManager;
        private ILocalizationTextReadModel _localizationManager;
        private bool _localizationColdResolved;
        private bool _hotSwapListenerRegistered;
        private bool _pendingLanguagePromptRefresh;
        private bool _lateFrameRefreshRegistered;

        // COLD ALLOC: char[192] — hover prompt rich-text buffer — owner: InteractionUI
        private readonly char[] _charBuffer = new char[192];
        // COLD ALLOC: char[96] — cached interaction prefix staging buffer — owner: InteractionUI
        private readonly char[] _prefixBuffer = new char[96];
        // COLD ALLOC: char[96] - optional IInteractableTextProvider staging buffer - owner: InteractionUI
        private readonly char[] _bodyBuffer = new char[96];

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

            CacheInputManagerCold();

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
            CacheInputManagerCold();
            InitializePromptContainer();
            RefreshInteractPrefixCache();
            RefreshCurrentPrompt();
        }

        private void OnDisable()
        {
            InteractionEvents.Unregister(this);

            UnsubscribeInputBindingService();

            UnsubscribeInputManager();

            TryUnregisterHotSwapListener();
            TryUnregisterLateFrameRefresh();

            LocalizationEvents.UnregisterLanguageListener(this);

            HidePrompt();
        }

        private void OnDestroy()
        {
            UnsubscribeInputBindingService();
            UnsubscribeInputManager();
            TryUnregisterHotSwapListener();
            TryUnregisterLateFrameRefresh();
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

        private void HandleOverridesSaved()
        {
            RefreshInteractPrefixCache();
            RefreshCurrentPrompt();
        }

        private void HandleOverridesCleared()
        {
            RefreshInteractPrefixCache();
            RefreshCurrentPrompt();
        }

        private void HandleInputDisplayStyleChanged(byte displayStyleCode)
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
            QueueLateFramePromptRefresh();
        }

        public void LateFrameTick()
        {
            if (!_pendingLanguagePromptRefresh)
            {
                TryUnregisterLateFrameRefresh();
                return;
            }

            _pendingLanguagePromptRefresh = false;
            RefreshCurrentPrompt();
            TryUnregisterLateFrameRefresh();
        }

        private void RefreshCurrentPrompt()
        {
            if (_lastDisplayedTarget == null)
                return;

            ShowPrompt(_lastDisplayedTarget);
        }

        private void ShowPrompt(IInteractable target)
        {
            ReadOnlySpan<char> interactText = ResolveInteractTextSpan(target);
            if (interactText.IsEmpty)
            {
                if (promptLabel != null)
                    promptLabel.SetCharArray(_charBuffer, 0, 0);

                SetPromptVisible(false);
                return;
            }

            if (promptLabel != null)
            {
                int totalLength = WriteToBuffer(_prefixBuffer.AsSpan(0, _cachedInteractPrefixLength), interactText);
                promptLabel.SetCharArray(_charBuffer, 0, totalLength);
            }

            SetPromptVisible(true);
        }

        private ReadOnlySpan<char> ResolveInteractTextSpan(IInteractable target)
        {
            if (target is IInteractableTextProvider textProvider &&
                textProvider.TryCopyInteractText(_bodyBuffer, out int bodyLength) &&
                bodyLength > 0)
            {
                return _bodyBuffer.AsSpan(0, math.min(bodyLength, _bodyBuffer.Length));
            }

            return ReadOnlySpan<char>.Empty;
        }

        private void HidePrompt()
        {
            SetPromptVisible(false);

            _lastDisplayedTarget = null;
            _pendingLanguagePromptRefresh = false;
            TryUnregisterLateFrameRefresh();
        }

        private void RefreshInteractPrefixCache()
        {
            if (!Application.isPlaying)
            {
                if (CacheLocalizedPrefixTemplate(GetCachedLocalizationManager()))
                    return;

                CachePrefixLiteral(inputPrefix.AsSpan(), appendTrailingSpace: false);
                return;
            }

            INativeInputManagerRuntime inputManager = _subscribedInputManager;
            if (inputManager != null && CacheInteractBindingMarkup(inputManager))
                return;

            ILocalizationTextReadModel localizationManager = GetCachedLocalizationManager();
            if (CacheLocalizedPrefixTemplate(localizationManager))
                return;

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

            if (_promptCanvasGroup == null)
                return;

            _promptCanvasGroup.alpha = visible ? 1f : 0f;
            _promptCanvasGroup.blocksRaycasts = false;
            _promptCanvasGroup.interactable = false;
        }

        private bool CacheLocalizedPrefixTemplate(ILocalizationTextReadModel manager)
        {
            if (manager == null)
                return false;

            ReadOnlySpan<char> template = manager.GetRawSpanOrFallback(
                LocHash.Compute(LocalizationKeys.INTERACT_DEFAULT_PROMPT_FORMAT),
                ReadOnlySpan<char>.Empty);
            if (template.Length <= 0)
                return false;

            CachePrefixFromTemplate(template);
            return true;
        }

        private void CachePrefixFromTemplate(ReadOnlySpan<char> template)
        {
            if (template.Length <= 0)
            {
                _cachedInteractPrefixLength = 0;
                return;
            }

            int placeholderIndex = IndexOfFirstPromptPlaceholder(template);
            ReadOnlySpan<char> prefixSpan = placeholderIndex <= 0
                ? template
                : template.Slice(0, placeholderIndex);
            CachePrefixLiteral(prefixSpan, appendTrailingSpace: false);
        }

        private static int IndexOfFirstPromptPlaceholder(ReadOnlySpan<char> template)
        {
            for (int i = 0; i <= template.Length - 3; i++)
            {
                if (template[i] == '{' && template[i + 1] == '0' && template[i + 2] == '}')
                    return i;
            }

            return -1;
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

        private void CacheInputManagerCold()
        {
            SubscribeInputManagerIfAvailable(GlobalRegistry.NativeInputRuntime);
        }

        private void SubscribeInputManagerIfAvailable(INativeInputManagerRuntime inputManager)
        {
            if (_subscribedInputManager != null || inputManager == null)
                return;

            _subscribedInputManager = inputManager;
            _subscribedInputManager.OnInputDisplayStyleCodeChanged += HandleInputDisplayStyleChanged;
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
            _subscribedInputBindingService.OnOverridesSaved += HandleOverridesSaved;
            _subscribedInputBindingService.OnOverridesCleared += HandleOverridesCleared;
        }

        private void UnsubscribeInputManager()
        {
            if (_subscribedInputManager == null)
                return;

            _subscribedInputManager.OnInputDisplayStyleCodeChanged -= HandleInputDisplayStyleChanged;
            _subscribedInputManager = null;
        }

        private void UnsubscribeInputBindingService()
        {
            if (_subscribedInputBindingService == null)
                return;

            _subscribedInputBindingService.OnRebindCompleted -= HandleBindingChanged;
            _subscribedInputBindingService.OnRebindCanceled -= HandleBindingCanceled;
            _subscribedInputBindingService.OnOverridesLoaded -= HandleOverridesLoaded;
            _subscribedInputBindingService.OnOverridesSaved -= HandleOverridesSaved;
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
                case GlobalRegistryServiceSlot.NativeInputManagerRuntime:
                    UnsubscribeInputManager();
                    if (isActiveAndEnabled)
                    {
                        SubscribeInputManagerIfAvailable(currentService as INativeInputManagerRuntime);
                        if (_subscribedInputManager == null)
                            CacheInputManagerCold();
                    }
                    break;

                case GlobalRegistryServiceSlot.InputBinding:
                    UnsubscribeInputBindingService();
                    if (isActiveAndEnabled)
                        SubscribeInputBindingServiceIfAvailable(currentService as IInputBindingService);
                    break;

                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _localizationManager = currentService as ILocalizationTextReadModel;
                    _localizationColdResolved = true;
                    break;

                case GlobalRegistryServiceSlot.Dispatcher:
                    if (currentService == null)
                    {
                        TryUnregisterLateFrameRefresh();
                    }
                    else if (_pendingLanguagePromptRefresh && isActiveAndEnabled)
                    {
                        TryRegisterLateFrameRefresh();
                    }

                    return;

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

        private void QueueLateFramePromptRefresh()
        {
            if (_lastDisplayedTarget == null)
                return;

            _pendingLanguagePromptRefresh = true;
            TryRegisterLateFrameRefresh();
        }

        private void TryRegisterLateFrameRefresh()
        {
            if (_lateFrameRefreshRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _lateFrameRefreshRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void TryUnregisterLateFrameRefresh()
        {
            if (!_lateFrameRefreshRegistered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
            _lateFrameRefreshRegistered = false;
        }

        private void CacheLocalizationCold(bool forceRefresh = false)
        {
            if (!forceRefresh && _localizationColdResolved)
                return;

            _localizationManager = Hecton8.Core.GlobalRegistry.LocalizationText;
            _localizationColdResolved = true;
        }

        private ILocalizationTextReadModel GetCachedLocalizationManager()
        {
            if (!Application.isPlaying && !_localizationColdResolved)
                CacheLocalizationCold();

            return _localizationManager;
        }

        private bool CacheInteractBindingMarkup(INativeInputManagerRuntime inputManager)
        {
            int offset = 0;
            if (!TryAppendPrefixLiteral("<b><color=#AEE8FF>".AsSpan(), ref offset))
                return false;

            if (offset >= _prefixBuffer.Length)
                return false;

            _prefixBuffer[offset++] = inputManager.CurrentDisplayStyleCode == NativeInputDisplayStyle.Gamepad
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
