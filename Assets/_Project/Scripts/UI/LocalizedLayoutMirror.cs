using Hecton.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    /// <summary>
    /// Mirrors horizontal layout groups and anchor alignment for right-to-left locales.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/UI/Localized Layout Mirror")]
    public sealed class LocalizedLayoutMirror : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Target layout group to mirror. Defaults to the component on the same GameObject.")]
        [SerializeField] private HorizontalOrVerticalLayoutGroup targetLayoutGroup;

        [Tooltip("Optional RectTransform whose pivot and anchored X should be mirrored.")]
        [SerializeField] private RectTransform targetRect;

        [Header("Mirroring")]
        [Tooltip("Reverse horizontal child order when the active language is RTL.")]
        [SerializeField] private bool mirrorChildOrder = true;

        [Tooltip("Mirror child alignment horizontally when the active language is RTL.")]
        [SerializeField] private bool mirrorChildAlignment = true;

        [Tooltip("Mirror pivot and anchored X when the active language is RTL.")]
        [SerializeField] private bool mirrorRectTransform = false;

        private bool _capturedDefaults;
        private bool _baseReverseArrangement;
        private TextAnchor _baseChildAlignment;
        private Vector2 _basePivot;
        private Vector2 _baseAnchoredPosition;
        private bool _isAppliedRtl;

        private void Awake()
        {
            ResolveTargets();
            CaptureDefaults();
        }

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
            ApplyMirroring();
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ResolveTargets();
            _capturedDefaults = false;
            CaptureDefaults();
            if (!Application.isPlaying)
                ApplyMirroring();
        }
#endif

        private void HandleLanguageChanged(GameLanguage language)
        {
            ApplyMirroring();
        }

        private void ApplyMirroring()
        {
            CaptureDefaults();

            GameLanguage language = LocalizationManager.Instance != null
                ? LocalizationManager.Instance.CurrentLanguage
                : GameLanguage.English;
            bool rtl = LocalizedMeasurementFormatter.IsRightToLeft(language);
            if (_isAppliedRtl == rtl && Application.isPlaying)
                return;

            if (targetLayoutGroup != null)
            {
                if (mirrorChildOrder && !(targetLayoutGroup is VerticalLayoutGroup))
                    targetLayoutGroup.reverseArrangement = rtl ? !_baseReverseArrangement : _baseReverseArrangement;

                if (mirrorChildAlignment)
                    targetLayoutGroup.childAlignment = rtl
                        ? MirrorAlignment(_baseChildAlignment)
                        : _baseChildAlignment;

                LayoutRebuilder.MarkLayoutForRebuild(targetLayoutGroup.transform as RectTransform);
            }

            if (mirrorRectTransform && targetRect != null)
            {
                if (rtl)
                {
                    targetRect.pivot = new Vector2(1f - _basePivot.x, _basePivot.y);
                    targetRect.anchoredPosition = new Vector2(-_baseAnchoredPosition.x, _baseAnchoredPosition.y);
                }
                else
                {
                    targetRect.pivot = _basePivot;
                    targetRect.anchoredPosition = _baseAnchoredPosition;
                }
            }

            _isAppliedRtl = rtl;
        }

        private void ResolveTargets()
        {
            if (targetLayoutGroup == null)
                TryGetComponent(out targetLayoutGroup);

            if (targetRect == null)
                TryGetComponent(out targetRect);
        }

        private void CaptureDefaults()
        {
            if (_capturedDefaults)
                return;

            ResolveTargets();

            if (targetLayoutGroup != null)
            {
                _baseReverseArrangement = targetLayoutGroup.reverseArrangement;
                _baseChildAlignment = targetLayoutGroup.childAlignment;
            }

            if (targetRect != null)
            {
                _basePivot = targetRect.pivot;
                _baseAnchoredPosition = targetRect.anchoredPosition;
            }

            _capturedDefaults = true;
        }

        private static TextAnchor MirrorAlignment(TextAnchor alignment)
        {
            switch (alignment)
            {
                case TextAnchor.UpperLeft:
                    return TextAnchor.UpperRight;
                case TextAnchor.MiddleLeft:
                    return TextAnchor.MiddleRight;
                case TextAnchor.LowerLeft:
                    return TextAnchor.LowerRight;
                case TextAnchor.UpperRight:
                    return TextAnchor.UpperLeft;
                case TextAnchor.MiddleRight:
                    return TextAnchor.MiddleLeft;
                case TextAnchor.LowerRight:
                    return TextAnchor.LowerLeft;
                default:
                    return alignment;
            }
        }

        /// <summary>
        /// Configure runtime-created layout roots for localization mirroring.
        /// </summary>
        public static void ConfigureRuntime(
            HorizontalOrVerticalLayoutGroup layoutGroup,
            RectTransform rectTransform,
            bool reverseChildren,
            bool mirrorAlignment,
            bool mirrorRectTransform)
        {
            if (layoutGroup == null && rectTransform == null)
                return;

            GameObject owner = layoutGroup != null
                ? layoutGroup.gameObject
                : rectTransform.gameObject;
            LocalizedLayoutMirror mirror = owner.GetComponent<LocalizedLayoutMirror>();
            if (mirror == null)
                mirror = owner.AddComponent<LocalizedLayoutMirror>();

            mirror.targetLayoutGroup = layoutGroup;
            mirror.targetRect = rectTransform;
            mirror.mirrorChildOrder = reverseChildren;
            mirror.mirrorChildAlignment = mirrorAlignment;
            mirror.mirrorRectTransform = mirrorRectTransform;
            mirror.ResolveTargets();
            mirror._capturedDefaults = false;
            mirror.CaptureDefaults();
            mirror.ApplyMirroring();
        }
    }
}
