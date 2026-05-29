using Hecton.Localization;
using Hecton8.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    /// <summary>
    /// Mirrors horizontal layout groups and anchor alignment for right-to-left locales.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/UI/Localized Layout Mirror")]
    public sealed class LocalizedLayoutMirror : MonoBehaviour, ILateFrameTickable, ILocalizationLanguageChangedListener, IGlobalRegistryHotSwapListener
    {
        private static bool s_isRebuildingLayout;
        private static ILocalizationTextReadModel s_cachedLocalization;
        private static bool s_localizationColdResolved;
        private const int MaxMirroredIconRoots = 32;

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
        private bool _isApplyingMirroring;
        private bool _applyMirroringPending = true;
        private bool _registeredForTick;
        private bool _hotSwapRegistered;
        private RectTransform _resolvedIconRoot0;
        private RectTransform _resolvedIconRoot1;
        private RectTransform _resolvedIconRoot2;
        private RectTransform _resolvedIconRoot3;
        private RectTransform _resolvedIconRoot4;
        private RectTransform _resolvedIconRoot5;
        private RectTransform _resolvedIconRoot6;
        private RectTransform _resolvedIconRoot7;
        private RectTransform _resolvedIconRoot8;
        private RectTransform _resolvedIconRoot9;
        private RectTransform _resolvedIconRoot10;
        private RectTransform _resolvedIconRoot11;
        private RectTransform _resolvedIconRoot12;
        private RectTransform _resolvedIconRoot13;
        private RectTransform _resolvedIconRoot14;
        private RectTransform _resolvedIconRoot15;
        private RectTransform _resolvedIconRoot16;
        private RectTransform _resolvedIconRoot17;
        private RectTransform _resolvedIconRoot18;
        private RectTransform _resolvedIconRoot19;
        private RectTransform _resolvedIconRoot20;
        private RectTransform _resolvedIconRoot21;
        private RectTransform _resolvedIconRoot22;
        private RectTransform _resolvedIconRoot23;
        private RectTransform _resolvedIconRoot24;
        private RectTransform _resolvedIconRoot25;
        private RectTransform _resolvedIconRoot26;
        private RectTransform _resolvedIconRoot27;
        private RectTransform _resolvedIconRoot28;
        private RectTransform _resolvedIconRoot29;
        private RectTransform _resolvedIconRoot30;
        private RectTransform _resolvedIconRoot31;
        private Vector3 _baseIconScale0;
        private Vector3 _baseIconScale1;
        private Vector3 _baseIconScale2;
        private Vector3 _baseIconScale3;
        private Vector3 _baseIconScale4;
        private Vector3 _baseIconScale5;
        private Vector3 _baseIconScale6;
        private Vector3 _baseIconScale7;
        private Vector3 _baseIconScale8;
        private Vector3 _baseIconScale9;
        private Vector3 _baseIconScale10;
        private Vector3 _baseIconScale11;
        private Vector3 _baseIconScale12;
        private Vector3 _baseIconScale13;
        private Vector3 _baseIconScale14;
        private Vector3 _baseIconScale15;
        private Vector3 _baseIconScale16;
        private Vector3 _baseIconScale17;
        private Vector3 _baseIconScale18;
        private Vector3 _baseIconScale19;
        private Vector3 _baseIconScale20;
        private Vector3 _baseIconScale21;
        private Vector3 _baseIconScale22;
        private Vector3 _baseIconScale23;
        private Vector3 _baseIconScale24;
        private Vector3 _baseIconScale25;
        private Vector3 _baseIconScale26;
        private Vector3 _baseIconScale27;
        private Vector3 _baseIconScale28;
        private Vector3 _baseIconScale29;
        private Vector3 _baseIconScale30;
        private Vector3 _baseIconScale31;
        private int _resolvedIconRootCount;

        private void Awake()
        {
            ResolveTargets();
            CaptureDefaults();
        }

        private void OnEnable()
        {
            CacheLocalizationCold();
            CaptureDefaults();
            TryRegisterHotSwapListener();
            LocalizationEvents.RegisterLanguageListener(this);
            QueueApplyMirroring();
        }

        private void OnDisable()
        {
            LocalizationEvents.UnregisterLanguageListener(this);
            TryUnregisterHotSwapListener();
            TryUnregisterFromTick();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ResolveTargets();
            _capturedDefaults = false;
            CaptureDefaults();
            if (!Application.isPlaying)
                QueueApplyMirroring();
        }
#endif

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }


        private void HandleLanguageChanged(GameLanguage language)
        {
            QueueApplyMirroring();
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
                QueueApplyMirroring();
                return;
            }

            if (currentService == null)
            {
                _registeredForTick = false;
                return;
            }

            if (isActiveAndEnabled)
            {
                TryUnregisterFromTick();
                TryRegisterForTick();
            }
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            if (!_applyMirroringPending)
                return;

            _applyMirroringPending = false;
            ApplyMirroring();
        }

        private void TryRegisterForTick()
        {
            if (_registeredForTick || !Application.isPlaying)
                return;

            _registeredForTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void TryUnregisterFromTick()
        {
            if (!_registeredForTick)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
            _registeredForTick = false;
        }

        private void ApplyMirroring()
        {
            if (_isApplyingMirroring)
                return;

            GameLanguage language = ResolveCurrentLanguage();
            bool rtl = LocalizedMeasurementFormatter.IsRightToLeft(language);
            if (_isAppliedRtl == rtl && Application.isPlaying)
                return;

            _isApplyingMirroring = true;
            try
            {
                if (targetLayoutGroup != null)
                {
                    if (mirrorChildOrder && !(targetLayoutGroup is VerticalLayoutGroup))
                        targetLayoutGroup.reverseArrangement = rtl ? !_baseReverseArrangement : _baseReverseArrangement;

                    if (mirrorChildAlignment)
                        targetLayoutGroup.childAlignment = rtl
                            ? MirrorAlignment(_baseChildAlignment)
                            : _baseChildAlignment;

                    MarkLayoutForRebuildSafe(targetLayoutGroup.transform as RectTransform);
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
            finally
            {
                _isApplyingMirroring = false;
            }
        }

        private void QueueApplyMirroring()
        {
            _applyMirroringPending = true;
            TryRegisterForTick();
        }

        private static void CacheLocalizationCold()
        {
            if (s_localizationColdResolved && s_cachedLocalization != null)
                return;

            s_cachedLocalization = GlobalRegistry.LocalizationText;
            s_localizationColdResolved = s_cachedLocalization != null;
        }

        private static GameLanguage ResolveCurrentLanguage()
        {
            ILocalizationTextReadModel manager = s_cachedLocalization;
            return manager != null ? (GameLanguage)manager.ActiveLanguageId : GameLanguage.English;
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

        private static void MarkLayoutForRebuildSafe(RectTransform rectTransform)
        {
            if (rectTransform == null || s_isRebuildingLayout)
                return;

            s_isRebuildingLayout = true;
            try
            {
                LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
            }
            finally
            {
                s_isRebuildingLayout = false;
            }
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

            for (int i = 0; i < _resolvedIconRootCount; i++)
            {
                RectTransform iconRoot = GetResolvedIconRoot(i);
                if (iconRoot == null)
                    continue;

                Vector3 baseScale = GetBaseIconScale(i);
                float mirroredX = rtl ? -Mathf.Abs(baseScale.x) : Mathf.Abs(baseScale.x);
                iconRoot.localScale = new Vector3(mirroredX, baseScale.y, baseScale.z);
            }
        }

        private void ResolveIconRoots()
        {
            ClearIconRoots();

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

            for (int i = 0; i < _resolvedIconRootCount; i++)
            {
                if (GetResolvedIconRoot(i) == rect)
                    return;
            }

            if (_resolvedIconRootCount >= MaxMirroredIconRoots)
                return;

            SetResolvedIconRoot(_resolvedIconRootCount, rect);
            SetBaseIconScale(_resolvedIconRootCount, rect.localScale);
            _resolvedIconRootCount++;
        }

        private void ClearIconRoots()
        {
            for (int i = 0; i < _resolvedIconRootCount; i++)
            {
                SetResolvedIconRoot(i, null);
                SetBaseIconScale(i, default);
            }

            _resolvedIconRootCount = 0;
        }

        private RectTransform GetResolvedIconRoot(int index)
        {
            switch (index)
            {
                case 0: return _resolvedIconRoot0;
                case 1: return _resolvedIconRoot1;
                case 2: return _resolvedIconRoot2;
                case 3: return _resolvedIconRoot3;
                case 4: return _resolvedIconRoot4;
                case 5: return _resolvedIconRoot5;
                case 6: return _resolvedIconRoot6;
                case 7: return _resolvedIconRoot7;
                case 8: return _resolvedIconRoot8;
                case 9: return _resolvedIconRoot9;
                case 10: return _resolvedIconRoot10;
                case 11: return _resolvedIconRoot11;
                case 12: return _resolvedIconRoot12;
                case 13: return _resolvedIconRoot13;
                case 14: return _resolvedIconRoot14;
                case 15: return _resolvedIconRoot15;
                case 16: return _resolvedIconRoot16;
                case 17: return _resolvedIconRoot17;
                case 18: return _resolvedIconRoot18;
                case 19: return _resolvedIconRoot19;
                case 20: return _resolvedIconRoot20;
                case 21: return _resolvedIconRoot21;
                case 22: return _resolvedIconRoot22;
                case 23: return _resolvedIconRoot23;
                case 24: return _resolvedIconRoot24;
                case 25: return _resolvedIconRoot25;
                case 26: return _resolvedIconRoot26;
                case 27: return _resolvedIconRoot27;
                case 28: return _resolvedIconRoot28;
                case 29: return _resolvedIconRoot29;
                case 30: return _resolvedIconRoot30;
                case 31: return _resolvedIconRoot31;
                default: return null;
            }
        }

        private void SetResolvedIconRoot(int index, RectTransform value)
        {
            switch (index)
            {
                case 0: _resolvedIconRoot0 = value; break;
                case 1: _resolvedIconRoot1 = value; break;
                case 2: _resolvedIconRoot2 = value; break;
                case 3: _resolvedIconRoot3 = value; break;
                case 4: _resolvedIconRoot4 = value; break;
                case 5: _resolvedIconRoot5 = value; break;
                case 6: _resolvedIconRoot6 = value; break;
                case 7: _resolvedIconRoot7 = value; break;
                case 8: _resolvedIconRoot8 = value; break;
                case 9: _resolvedIconRoot9 = value; break;
                case 10: _resolvedIconRoot10 = value; break;
                case 11: _resolvedIconRoot11 = value; break;
                case 12: _resolvedIconRoot12 = value; break;
                case 13: _resolvedIconRoot13 = value; break;
                case 14: _resolvedIconRoot14 = value; break;
                case 15: _resolvedIconRoot15 = value; break;
                case 16: _resolvedIconRoot16 = value; break;
                case 17: _resolvedIconRoot17 = value; break;
                case 18: _resolvedIconRoot18 = value; break;
                case 19: _resolvedIconRoot19 = value; break;
                case 20: _resolvedIconRoot20 = value; break;
                case 21: _resolvedIconRoot21 = value; break;
                case 22: _resolvedIconRoot22 = value; break;
                case 23: _resolvedIconRoot23 = value; break;
                case 24: _resolvedIconRoot24 = value; break;
                case 25: _resolvedIconRoot25 = value; break;
                case 26: _resolvedIconRoot26 = value; break;
                case 27: _resolvedIconRoot27 = value; break;
                case 28: _resolvedIconRoot28 = value; break;
                case 29: _resolvedIconRoot29 = value; break;
                case 30: _resolvedIconRoot30 = value; break;
                case 31: _resolvedIconRoot31 = value; break;
            }
        }

        private Vector3 GetBaseIconScale(int index)
        {
            switch (index)
            {
                case 0: return _baseIconScale0;
                case 1: return _baseIconScale1;
                case 2: return _baseIconScale2;
                case 3: return _baseIconScale3;
                case 4: return _baseIconScale4;
                case 5: return _baseIconScale5;
                case 6: return _baseIconScale6;
                case 7: return _baseIconScale7;
                case 8: return _baseIconScale8;
                case 9: return _baseIconScale9;
                case 10: return _baseIconScale10;
                case 11: return _baseIconScale11;
                case 12: return _baseIconScale12;
                case 13: return _baseIconScale13;
                case 14: return _baseIconScale14;
                case 15: return _baseIconScale15;
                case 16: return _baseIconScale16;
                case 17: return _baseIconScale17;
                case 18: return _baseIconScale18;
                case 19: return _baseIconScale19;
                case 20: return _baseIconScale20;
                case 21: return _baseIconScale21;
                case 22: return _baseIconScale22;
                case 23: return _baseIconScale23;
                case 24: return _baseIconScale24;
                case 25: return _baseIconScale25;
                case 26: return _baseIconScale26;
                case 27: return _baseIconScale27;
                case 28: return _baseIconScale28;
                case 29: return _baseIconScale29;
                case 30: return _baseIconScale30;
                case 31: return _baseIconScale31;
                default: return default;
            }
        }

        private void SetBaseIconScale(int index, Vector3 value)
        {
            switch (index)
            {
                case 0: _baseIconScale0 = value; break;
                case 1: _baseIconScale1 = value; break;
                case 2: _baseIconScale2 = value; break;
                case 3: _baseIconScale3 = value; break;
                case 4: _baseIconScale4 = value; break;
                case 5: _baseIconScale5 = value; break;
                case 6: _baseIconScale6 = value; break;
                case 7: _baseIconScale7 = value; break;
                case 8: _baseIconScale8 = value; break;
                case 9: _baseIconScale9 = value; break;
                case 10: _baseIconScale10 = value; break;
                case 11: _baseIconScale11 = value; break;
                case 12: _baseIconScale12 = value; break;
                case 13: _baseIconScale13 = value; break;
                case 14: _baseIconScale14 = value; break;
                case 15: _baseIconScale15 = value; break;
                case 16: _baseIconScale16 = value; break;
                case 17: _baseIconScale17 = value; break;
                case 18: _baseIconScale18 = value; break;
                case 19: _baseIconScale19 = value; break;
                case 20: _baseIconScale20 = value; break;
                case 21: _baseIconScale21 = value; break;
                case 22: _baseIconScale22 = value; break;
                case 23: _baseIconScale23 = value; break;
                case 24: _baseIconScale24 = value; break;
                case 25: _baseIconScale25 = value; break;
                case 26: _baseIconScale26 = value; break;
                case 27: _baseIconScale27 = value; break;
                case 28: _baseIconScale28 = value; break;
                case 29: _baseIconScale29 = value; break;
                case 30: _baseIconScale30 = value; break;
                case 31: _baseIconScale31 = value; break;
            }
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
            owner.TryGetComponent(out LocalizedLayoutMirror mirror);
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
