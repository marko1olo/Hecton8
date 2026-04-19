using Hecton.Localization;
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
    public sealed class LocalizedTMPAutoSizer : MonoBehaviour
    {
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

        private void Awake()
        {
            ResolveTargetText();
            CaptureDefaults();
            ApplyConfiguration();
        }

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
            ApplyConfiguration();
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
        }

        private void OnRectTransformDimensionsChange()
        {
            ApplyConfiguration();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (minFontSize > maxFontSize)
                minFontSize = maxFontSize;

            ResolveTargetText();
            _capturedDefaults = false;
            CaptureDefaults();
            ApplyConfiguration();
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
            LocalizedTMPAutoSizer autoSizer = text.GetComponent<LocalizedTMPAutoSizer>();
            if (autoSizer == null)
                autoSizer = text.gameObject.AddComponent<LocalizedTMPAutoSizer>();

            autoSizer.targetText = text;
            autoSizer.minFontSize = resolvedMin;
            autoSizer.maxFontSize = resolvedMax;
            autoSizer.overflowMode = overflow;
            autoSizer.wrappingMode = wrapping;
            autoSizer.CaptureDefaults();
            autoSizer.ApplyConfiguration();
        }

        private void HandleLanguageChanged(GameLanguage language)
        {
            ApplyConfiguration();
        }

        private void ApplyConfiguration()
        {
            ResolveTargetText();
            if (targetText == null)
                return;

            CaptureDefaults();

            GameLanguage language = LocalizationManager.Instance != null
                ? LocalizationManager.Instance.CurrentLanguage
                : GameLanguage.English;
            float localeScale = ResolveLocaleFontScale(language);

            targetText.enableAutoSizing = true;
            targetText.fontSizeMin = Mathf.Max(1f, minFontSize * localeScale);
            targetText.fontSizeMax = Mathf.Max(targetText.fontSizeMin, maxFontSize * localeScale);
            targetText.overflowMode = overflowMode;
            targetText.textWrappingMode = wrappingMode;
            if (enableRightToLeft)
                ApplyRuntimeLocalizationLayout(targetText);
            targetText.ForceMeshUpdate(false, false);
        }

        private void ResolveTargetText()
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

            LocalizationManager manager = LocalizationManager.Instance;
            GameLanguage language = manager != null ? manager.CurrentLanguage : GameLanguage.English;
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
