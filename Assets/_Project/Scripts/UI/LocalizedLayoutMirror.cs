using Hecton.Localization;
using System.Collections.Generic;
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

        [Tooltip("Mirror icon graphics by flipping localScale.x when the active language is RTL.")]
        [SerializeField] private bool mirrorIconScaleX = false;

        [Tooltip("Optional explicit icon roots to flip for RTL locales.")]
        [SerializeField] private RectTransform[] explicitIconRoots;

        [Tooltip("Auto-discover child icons by tag when icon flipping is enabled.")]
        [SerializeField] private bool mirrorTaggedIcons = true;

        [Tooltip("Tag used for icon roots that should flip in RTL locales.")]
        [SerializeField] private string iconTag = "Icon";

        [Tooltip("Auto-discover child graphics with 'icon' in the name when icon flipping is enabled.")]
        [SerializeField] private bool mirrorNamedIcons = true;

        private bool _capturedDefaults;
        private bool _baseReverseArrangement;
        private TextAnchor _baseChildAlignment;
        private Vector2 _basePivot;
        private Vector2 _baseAnchoredPosition;
        private bool _isAppliedRtl;
        private readonly List<RectTransform> _resolvedIconRoots = new List<RectTransform>(8); // COLD ALLOC: List[8] — cached mirrored icon roots — owner: LocalizedLayoutMirror
        private readonly List<Vector3> _baseIconScales = new List<Vector3>(8); // COLD ALLOC: List[8] — cached icon base scales — owner: LocalizedLayoutMirror

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

            ApplyIconMirroring(rtl);

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

            ResolveIconRoots();

            _capturedDefaults = true;
        }

        private void ApplyIconMirroring(bool rtl)
        {
            if (!mirrorIconScaleX)
                return;

            ResolveIconRoots();
            for (int i = 0; i < _resolvedIconRoots.Count; i++)
            {
                RectTransform iconRoot = _resolvedIconRoots[i];
                if (iconRoot == null)
                    continue;

                Vector3 baseScale = i < _baseIconScales.Count ? _baseIconScales[i] : iconRoot.localScale;
                float mirroredX = rtl ? -Mathf.Abs(baseScale.x) : Mathf.Abs(baseScale.x);
                iconRoot.localScale = new Vector3(mirroredX, baseScale.y, baseScale.z);
            }
        }

        private void ResolveIconRoots()
        {
            _resolvedIconRoots.Clear();
            _baseIconScales.Clear();

            if (!mirrorIconScaleX)
                return;

            AddExplicitIconRoots();
            if (mirrorTaggedIcons || mirrorNamedIcons)
                CollectIconRoots(transform);
        }

        private void AddExplicitIconRoots()
        {
            if (explicitIconRoots == null)
                return;

            for (int i = 0; i < explicitIconRoots.Length; i++)
                AddIconRoot(explicitIconRoots[i]);
        }

        private void CollectIconRoots(Transform parent)
        {
            if (parent == null)
                return;

            int childCount = parent.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child == null)
                    continue;

                RectTransform rect = child as RectTransform;
                if (rect != null && ShouldMirrorIcon(rect))
                    AddIconRoot(rect);

                CollectIconRoots(child);
            }
        }

        private bool ShouldMirrorIcon(RectTransform rect)
        {
            if (rect == null || rect == targetRect)
                return false;

            if (mirrorTaggedIcons && !string.IsNullOrEmpty(iconTag) && rect.CompareTag(iconTag))
                return true;

            if (!mirrorNamedIcons)
                return false;

            if (!rect.TryGetComponent(out Graphic _))
                return false;

            string objectName = rect.name;
            return !string.IsNullOrEmpty(objectName) &&
                   objectName.IndexOf("icon", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void AddIconRoot(RectTransform rect)
        {
            if (rect == null)
                return;

            for (int i = 0; i < _resolvedIconRoots.Count; i++)
            {
                if (_resolvedIconRoots[i] == rect)
                    return;
            }

            _resolvedIconRoots.Add(rect);
            _baseIconScales.Add(rect.localScale);
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
            mirror.mirrorIconScaleX = true;
            mirror.mirrorNamedIcons = true;
            mirror.ResolveTargets();
            mirror._capturedDefaults = false;
            mirror.CaptureDefaults();
            mirror.ApplyMirroring();
        }
    }
}
