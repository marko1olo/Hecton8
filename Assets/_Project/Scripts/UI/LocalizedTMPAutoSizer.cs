using Hecton.Localization;
using Hecton8.Core;
using TMPro;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Configures TMP auto-sizing for long localized labels without per-frame work.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    [AddComponentMenu("Hecton/UI/Localized TMP Auto Sizer")]
    public sealed class LocalizedTMPAutoSizer : MonoBehaviour, ILateFrameTickable, ILocalizationLanguageChangedListener, IGlobalRegistryHotSwapListener
    {
        private const float CollapsedRectThreshold = 0.5f;
        private const int MaxRectRepairPasses = 4;
        private const int MaxRectRepairDepth = 4;
        private const int MaxConfigurationAppliesPerLateFrame = 18;
        private static ILocalizationTextReadModel s_cachedLocalization;
        private static bool s_localizationColdResolved;
        private static int s_failedLocalizationResolveFrame = -1;
        private static int s_applyBudgetFrame = -1;
        private static int s_applyBudgetUsed;

        [Header("References")]
        [Tooltip("Target TMP text. Defaults to TMP_Text on the same GameObject.")]
        [SerializeField] private TMP_Text targetText;

        [Header("Sizing")]
        [Tooltip("Maximum font size allowed for this label.")]
        [SerializeField, Min(1f)] private float maxFontSize = 36f;

        [Tooltip("Minimum font size allowed before truncation or ellipsis.")]
        [SerializeField, Min(1f)] private float minFontSize = 12f;

        [Tooltip("Overflow mode applied after autosize reaches the minimum font size.")]
        [SerializeField] private TextOverflowModes overflowMode = TextOverflowModes.Ellipsis;

        [Tooltip("Wrap mode used by the target TMP text.")]
        [SerializeField] private TextWrappingModes wrappingMode = TextWrappingModes.NoWrap;

        [Header("RTL")]
        [Tooltip("Apply right-to-left text flow when the active language requires it.")]
        [SerializeField] private bool enableRightToLeft = true;

        [Tooltip("Shrink long Arabic/RTL labels to reduce clipping in narrow controls.")]
        [SerializeField, Range(0.7f, 1f)] private float rtlFontScaleMultiplier = 0.9f;

        [Tooltip("Shrink dense CJK labels slightly to reduce clipping in narrow controls.")]
        [SerializeField, Range(0.7f, 1f)] private float cjkFontScaleMultiplier = 0.94f;

        private bool _capturedDefaults;
        private bool _configurationDirty = true;
        private bool _configurationApplyPending = true;
        private bool _isApplyingConfiguration;
        private GameLanguage _lastAppliedLanguage = (GameLanguage)(-1);
        private Vector2 _lastAppliedRectSize = new Vector2(-1f, -1f);
        private float _lastAppliedOverflowScale = 1f;
        private bool _registeredForTick;
        private bool _hotSwapRegistered;
#if UNITY_EDITOR
        private bool _isEditorValidating;
#endif

        private void Awake()
        {
            ResolveTargetTextCold();
            CaptureDefaults();
            QueueConfigurationApply();
        }

        private void OnEnable()
        {
            CacheLocalizationCold();
            TryRegisterHotSwapListener();
            LocalizationEvents.RegisterLanguageListener(this);
            InvalidateConfiguration();
            QueueConfigurationApply();
        }

        private void OnDisable()
        {
            LocalizationEvents.UnregisterLanguageListener(this);
            TryUnregisterHotSwapListener();
            TryUnregisterFromTick();
        }

        private void OnRectTransformDimensionsChange()
        {
            InvalidateConfiguration();
            QueueConfigurationApply();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying || _isEditorValidating)
                return;

            _isEditorValidating = true;
            try
            {
                if (minFontSize > maxFontSize)
                    minFontSize = maxFontSize;

                ResolveTargetTextCold();
                _capturedDefaults = false;
                CaptureDefaults();
                InvalidateConfiguration();
                RepairCollapsedRectHierarchy();
                ApplyConfiguration();
            }
            finally
            {
                _isEditorValidating = false;
            }
        }
#endif

        public static void Configure(
            TMP_Text text,
            float minSize,
            float maxSize,
            TextOverflowModes overflow = TextOverflowModes.Ellipsis,
            TextWrappingModes wrapping = TextWrappingModes.NoWrap)
        {
            if (text == null)
                return;

            float resolvedMin = Mathf.Max(1f, Mathf.Min(minSize, maxSize));
            float resolvedMax = Mathf.Max(resolvedMin, maxSize);
            text.TryGetComponent(out LocalizedTMPAutoSizer autoSizer);
            if (autoSizer == null)
            {
                ApplyDirectTextConfiguration(text, resolvedMin, resolvedMax, overflow, wrapping);
                return;
            }

            autoSizer.targetText = text;
            autoSizer.minFontSize = resolvedMin;
            autoSizer.maxFontSize = resolvedMax;
            autoSizer.overflowMode = overflow;
            autoSizer.wrappingMode = wrapping;
            autoSizer.CaptureDefaults();
            autoSizer.InvalidateConfiguration();
            autoSizer.ApplyConfiguration();
        }

        private static void ApplyDirectTextConfiguration(
            TMP_Text text,
            float minSize,
            float maxSize,
            TextOverflowModes overflow,
            TextWrappingModes wrapping)
        {
            CacheLocalizationCold();
            text.enableAutoSizing = true;
            text.fontSizeMin = minSize;
            text.fontSizeMax = Mathf.Max(minSize, maxSize);
            text.overflowMode = ResolveSafeOverflowMode(text, overflow);
            text.textWrappingMode = wrapping;
            ApplyRuntimeLocalizationLayout(text);
        }

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }


        private void HandleLanguageChanged(GameLanguage language)
        {
            InvalidateConfiguration();
            QueueConfigurationApply();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.LocalizationRuntime &&
                serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            if (serviceSlot == GlobalRegistryServiceSlot.LocalizationRuntime)
            {
                s_cachedLocalization = currentService as ILocalizationTextReadModel;
                s_localizationColdResolved = true;
                InvalidateConfiguration();
                QueueConfigurationApply();
                return;
            }

            TryUnregisterFromTick();
            if (isActiveAndEnabled)
            {
                if (currentService != null)
                    TryRegisterForTick();
            }
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            if (!_configurationApplyPending)
            {
                TryUnregisterFromTick();
                return;
            }

            if (!TryConsumeApplyBudget())
                return;

            _configurationApplyPending = false;
            ApplyConfiguration();
            if (!_configurationApplyPending)
                TryUnregisterFromTick();
        }

        private void TryRegisterForTick()
        {
            if (_registeredForTick || !Application.isPlaying)
                return;

            _registeredForTick = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);
        }

        private void TryUnregisterFromTick()
        {
            if (!_registeredForTick)
                return;

            SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
            _registeredForTick = false;
        }

        private void ApplyConfiguration()
        {
            if (targetText == null || _isApplyingConfiguration)
                return;

            CaptureDefaults();
            RepairCollapsedRectHierarchy();

            GameLanguage language = ResolveCurrentLanguage();
            Vector2 rectSize = targetText.rectTransform != null ? targetText.rectTransform.rect.size : Vector2.zero;
            if (!_configurationDirty &&
                _lastAppliedLanguage == language &&
                Approximately(rectSize, _lastAppliedRectSize))
            {
                return;
            }

            float localeScale = ResolveLocaleFontScale(language);
            _isApplyingConfiguration = true;
            try
            {
                targetText.enableAutoSizing = true;
                targetText.fontSizeMin = Mathf.Max(1f, minFontSize * localeScale);
                targetText.fontSizeMax = Mathf.Max(targetText.fontSizeMin, maxFontSize * localeScale);
                targetText.overflowMode = ResolveSafeOverflowMode(targetText, overflowMode);
                targetText.textWrappingMode = wrappingMode;
                if (enableRightToLeft)
                    ApplyRuntimeLocalizationLayout(targetText);
                _lastAppliedOverflowScale = LocOverflowHandler.ApplyScale(
                    targetText,
                    _lastAppliedOverflowScale,
                    LocOverflowHandler.ResolveUniformScale(targetText));
                _lastAppliedLanguage = language;
                _lastAppliedRectSize = rectSize;
                _configurationDirty = false;
            }
            finally
            {
                _isApplyingConfiguration = false;
            }
        }

        private void ResolveTargetText()
        {
            ResolveTargetTextCold();
        }

        private void ResolveTargetTextCold()
        {
            if (targetText == null)
                TryGetComponent(out targetText);
        }

        private void CaptureDefaults()
        {
            if (_capturedDefaults || targetText == null)
                return;

            _capturedDefaults = true;
        }

        private void InvalidateConfiguration()
        {
            _configurationDirty = true;
        }

        private static TextOverflowModes ResolveSafeOverflowMode(TMP_Text text, TextOverflowModes requestedMode)
        {
            return requestedMode == TextOverflowModes.Ellipsis
                ? TextOverflowModes.Truncate
                : requestedMode;
        }

        private void QueueConfigurationApply()
        {
            _configurationApplyPending = true;
            TryRegisterForTick();
        }

        private static void CacheLocalizationCold()
        {
            if (s_localizationColdResolved && s_cachedLocalization != null)
                return;

            int frame = SystemDispatcher.CurrentFrameIndex;
            if (s_failedLocalizationResolveFrame == frame)
                return;

            s_cachedLocalization = GlobalRegistry.LocalizationText;
            s_localizationColdResolved = s_cachedLocalization != null;
            if (s_cachedLocalization == null)
                s_failedLocalizationResolveFrame = frame;
        }

        private static GameLanguage ResolveCurrentLanguage()
        {
            ILocalizationTextReadModel manager = s_cachedLocalization;
            return manager != null ? (GameLanguage)manager.ActiveLanguageId : GameLanguage.English;
        }

        private static bool TryConsumeApplyBudget()
        {
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (s_applyBudgetFrame != frame)
            {
                s_applyBudgetFrame = frame;
                s_applyBudgetUsed = 0;
            }

            if (s_applyBudgetUsed >= MaxConfigurationAppliesPerLateFrame)
                return false;

            s_applyBudgetUsed++;
            return true;
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

        private void RepairCollapsedRectHierarchy()
        {
            RectTransform textRect = targetText != null ? targetText.rectTransform : null;
            if (textRect == null)
                return;

            for (int pass = 0; pass < MaxRectRepairPasses; pass++)
            {
                bool repairedAny = false;
                RectTransform current = textRect;
                int depth = 0;
                while (current != null && depth++ < MaxRectRepairDepth)
                {
                    RectTransform parent = current.parent as RectTransform;
                    if (parent == null)
                        break;

                    if (IsCollapsedRect(current) && !IsCollapsedRect(parent))
                    {
                        StretchToParent(current);
                        repairedAny = true;
                    }

                    current = parent;
                }

                if (!repairedAny)
                    break;
            }
        }

        private static bool Approximately(Vector2 left, Vector2 right)
        {
            return Mathf.Approximately(left.x, right.x) &&
                   Mathf.Approximately(left.y, right.y);
        }

        private static bool IsCollapsedRect(RectTransform rect)
        {
            if (rect == null)
                return false;

            Rect bounds = rect.rect;
            return bounds.width <= CollapsedRectThreshold || bounds.height <= CollapsedRectThreshold;
        }

        private static void StretchToParent(RectTransform rect)
        {
            if (rect == null || rect.parent == null)
                return;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private float ResolveLocaleFontScale(GameLanguage language)
        {
            if (LocalizedMeasurementFormatter.IsRightToLeft(language))
                return rtlFontScaleMultiplier;

            switch (language)
            {
                case GameLanguage.ChineseSimplified:
                case GameLanguage.ChineseTraditional:
                case GameLanguage.Japanese:
                case GameLanguage.Korean:
                    return cjkFontScaleMultiplier;
                default:
                    return 1f;
            }
        }

        /// <summary>
        /// Apply right-to-left layout state to a TMP text owner without per-frame work.
        /// </summary>
        public static void ApplyRuntimeLocalizationLayout(TMP_Text text)
        {
            if (text == null)
                return;

            GameLanguage language = ResolveCurrentLanguage();
            bool rtl = LocalizedMeasurementFormatter.IsRightToLeft(language);
            if (text.isRightToLeftText == rtl)
                return;

            text.alignment = MirrorAlignment(text.alignment);
            text.isRightToLeftText = rtl;
        }

        private static TextAlignmentOptions MirrorAlignment(TextAlignmentOptions alignment)
        {
            switch (alignment)
            {
                case TextAlignmentOptions.Left:
                    return TextAlignmentOptions.Right;
                case TextAlignmentOptions.Right:
                    return TextAlignmentOptions.Left;
                case TextAlignmentOptions.TopLeft:
                    return TextAlignmentOptions.TopRight;
                case TextAlignmentOptions.TopRight:
                    return TextAlignmentOptions.TopLeft;
                case TextAlignmentOptions.BottomLeft:
                    return TextAlignmentOptions.BottomRight;
                case TextAlignmentOptions.BottomRight:
                    return TextAlignmentOptions.BottomLeft;
                case TextAlignmentOptions.MidlineLeft:
                    return TextAlignmentOptions.MidlineRight;
                case TextAlignmentOptions.MidlineRight:
                    return TextAlignmentOptions.MidlineLeft;
                case TextAlignmentOptions.BaselineLeft:
                    return TextAlignmentOptions.BaselineRight;
                case TextAlignmentOptions.BaselineRight:
                    return TextAlignmentOptions.BaselineLeft;
                case TextAlignmentOptions.CaplineLeft:
                    return TextAlignmentOptions.CaplineRight;
                case TextAlignmentOptions.CaplineRight:
                    return TextAlignmentOptions.CaplineLeft;
                default:
                    return alignment;
            }
        }
    }
}
