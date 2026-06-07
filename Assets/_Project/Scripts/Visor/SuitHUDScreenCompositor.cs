using Hecton8.Core;
using Hecton8.UI;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NASAPunk.Visor
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/HUD/Suit HUD Screen Compositor")]
    public sealed class SuitHUDScreenCompositor : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const int MaxActiveCompositors = 2;
        private static SuitHUDScreenCompositor s_activeCompositor0;
        private static SuitHUDScreenCompositor s_activeCompositor1;
        private static int s_activeCompositorCount;
        private const float AutoResolveRetryInterval = 1f;

        [Header("References")]
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private RenderTexture sharedProjectionTexture;
        [SerializeField] private VisorHUDController visorController;

        [Header("Presentation")]
        [SerializeField] private string overlayName = "HUD_RT_Compositor";
        [SerializeField] [Range(0f, 1f)] private float overlayAlpha = 1f;
        [SerializeField] private bool forceCanvasActive;
        [SerializeField] private bool forceScreenSpaceOverlay;
        [SerializeField] private bool forceSharedProjection = true;
        [SerializeField] private bool hideWhenTextureMissing;
        [SerializeField] private bool preserveExistingChildren = true;
        [SerializeField] private int overlaySortingOrder = 80;
        [SerializeField] private bool manageCanvasInEditMode = true;
        [SerializeField] private bool showAsInsetPreview = true;
        [SerializeField] private Vector2 insetSize = new Vector2(340f, 340f);
        [SerializeField] private Vector2 insetMargin = new Vector2(18f, 18f);

        // Inspector-only compositor diagnostics for visor HUD bring-up.
#pragma warning disable CS0414
        [Header("Diagnostics")]
        [SerializeField] private bool debugCanvasReady;
        [SerializeField] private bool debugOverlayReady;
        [SerializeField] private bool debugTextureAssigned;
#pragma warning restore CS0414

        private RawImage _overlayImage;
        private RectTransform _overlayRect;
        private CanvasGroup _overlayCanvasGroup;
        private float _nextAutoResolveAt;
        private bool _tickRegistered;
        private bool _hotSwapRegistered;
        private bool _pendingRefresh = true;
        private string _appliedOverlayName;

        internal static int ActiveCompositorCount => s_activeCompositorCount;

        private void OnEnable()
        {
            if (Application.isPlaying && !IsRuntimeScreenPreviewAllowed())
            {
                HideExistingOverlay();
                enabled = false;
                return;
            }

            RegisterActiveCompositor();
            TryRegisterHotSwapListener();
            _pendingRefresh = true;
            if (!Application.isPlaying)
                return;

            AutoResolveReferences(true);
            RefreshCompositor();
            TryRegisterRuntimeTick();
        }

        private void Start()
        {
            TryRegisterHotSwapListener();
            TryRegisterRuntimeTick();
        }

        private void OnDisable()
        {
            UnregisterActiveCompositor();
            TryUnregisterHotSwapListener();
            UnregisterRuntimeTick();
#if UNITY_EDITOR
            EditorApplication.update -= EditorTick;
#endif

            if (!Application.isPlaying)
                return;

            if (_overlayImage != null)
                _overlayImage.enabled = false;

            SetOverlayVisible(false);
        }

#if UNITY_EDITOR
        private void EditorTick()
        {
            if (!IsEditorPreviewSafe())
            {
                UnregisterEditorTick();
                return;
            }

            if (Application.isPlaying || !isActiveAndEnabled || !manageCanvasInEditMode)
            {
                UnregisterEditorTick();
                return;
            }

            if (!UnityEditorInternal.InternalEditorUtility.isApplicationActive)
                return;

            RefreshCompositor();
            _pendingRefresh = false;
            if (!ShouldTickInEditMode())
                UnregisterEditorTick();
        }
#endif

        private void OnValidate()
        {
            _pendingRefresh = true;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            UnregisterRuntimeTick();
            if (currentService != null && isActiveAndEnabled)
                TryRegisterRuntimeTick();
        }

        public void LateFrameTick()
        {
            if (!_pendingRefresh && !NeedsAutoResolve())
                return;

            RefreshCompositorHot();
            _pendingRefresh = false;
        }

        private void RefreshCompositor(bool allowOverlayCreation = true)
        {
            AutoResolveReferences();
            EnsureCanvasState();
            EnsureOverlay(allowOverlayCreation);
            EnsureProjection();
            BindTexture();
        }

        private void RefreshCompositorHot()
        {
            RefreshRuntimeReferenceCache();
            EnsureCanvasState();
            EnsureCachedOverlayState();
            EnsureProjection();
            BindTexture();
        }

        private void RefreshRuntimeReferenceCache()
        {
            if (sharedProjectionTexture == null && visorController != null)
                sharedProjectionTexture = visorController.SharedRenderTexture;
        }

        private void AutoResolveReferences(bool force = false)
        {
            if (!force && !NeedsAutoResolve())
                return;

            float now = Application.isPlaying
                ? (float)SystemDispatcher.CurrentUnscaledTimeSeconds
                : ResolveEditorPreviewClockSeconds();
            if (!force && now < _nextAutoResolveAt)
                return;

            _nextAutoResolveAt = now + AutoResolveRetryInterval;
            bool allowHierarchySearch = force || !Application.isPlaying;

            if (targetCanvas == null)
            {
                Transform root = transform.root;
                for (int i = 0; i < SuitHUDV4CanvasOverlay.ActiveOverlayCount; i++)
                {
                    SuitHUDV4CanvasOverlay overlay = SuitHUDV4CanvasOverlay.GetActiveOverlay(i);
                    Canvas candidateCanvas = overlay != null ? overlay.TargetCanvas : null;
                    if (candidateCanvas == null || candidateCanvas.transform.root != root)
                        continue;

                    if (!allowHierarchySearch || candidateCanvas.name == "Suit_HUD_Canvas")
                    {
                        targetCanvas = candidateCanvas;
                        break;
                    }
                }

                if (targetCanvas == null)
                {
                    for (int i = 0; i < SuitHUDV4CanvasOverlay.ActiveOverlayCount; i++)
                    {
                        SuitHUDV4CanvasOverlay overlay = SuitHUDV4CanvasOverlay.GetActiveOverlay(i);
                        Canvas candidateCanvas = overlay != null ? overlay.TargetCanvas : null;
                        if (candidateCanvas != null &&
                            (!allowHierarchySearch || candidateCanvas.name == "Suit_HUD_Canvas"))
                        {
                            targetCanvas = candidateCanvas;
                            break;
                        }
                    }
                }
            }

            if (visorController == null)
            {
                if (allowHierarchySearch)
                {
                    Transform parent = transform.parent;
                    if (parent != null)
                    {
                        Transform visor = parent.Find("Suit_Visor");
                        if (visor != null)
                            visor.TryGetComponent(out visorController);
                    }
                }

                if (visorController == null)
                {
                    Transform root = transform.root;
                    for (int i = 0; i < VisorHUDController.ActiveControllerCount; i++)
                    {
                        VisorHUDController controller = VisorHUDController.GetActiveController(i);
                        if (controller != null && controller.transform.root == root)
                        {
                            visorController = controller;
                            break;
                        }
                    }

                    if (visorController == null && VisorHUDController.ActiveControllerCount > 0)
                        visorController = VisorHUDController.GetActiveController(0);
                }
            }

            if (sharedProjectionTexture == null && visorController != null)
                sharedProjectionTexture = visorController.SharedRenderTexture;
        }

        private bool NeedsAutoResolve()
        {
            return targetCanvas == null ||
                   visorController == null ||
                   sharedProjectionTexture == null;
        }

        private static float ResolveEditorPreviewClockSeconds()
        {
            return (float)(System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency);
        }

        private void EnsureCanvasState()
        {
            debugCanvasReady = targetCanvas != null;
            if (targetCanvas == null)
                return;

            if (forceCanvasActive && !targetCanvas.enabled)
                targetCanvas.enabled = true;

            RectTransform rect = targetCanvas.transform as RectTransform;
            if (rect != null && rect.localScale == Vector3.zero)
                rect.localScale = Vector3.one;

#if UNITY_EDITOR
            if (!Application.isPlaying && forceScreenSpaceOverlay)
            {
                if (targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                if (targetCanvas.worldCamera != null)
                    targetCanvas.worldCamera = null;
            }
#endif

            if (!targetCanvas.overrideSorting)
                targetCanvas.overrideSorting = true;
            if (targetCanvas.sortingOrder != overlaySortingOrder)
                targetCanvas.sortingOrder = overlaySortingOrder;
        }

        private void EnsureOverlay(bool allowOverlayCreation)
        {
            debugOverlayReady = false;
            if (targetCanvas == null)
                return;

            Transform canvasTransform = targetCanvas.transform;
            Transform overlayTransform;
            if (_overlayRect != null && _appliedOverlayName == overlayName && _overlayRect.parent == canvasTransform)
            {
                overlayTransform = _overlayRect.transform;
            }
            else
            {
                if (!allowOverlayCreation)
                    return;

                overlayTransform = canvasTransform.Find(overlayName);
            }

            if (overlayTransform == null)
            {
                if (!allowOverlayCreation)
                    return;

                // COLD ALLOC: GameObject[1] — compositor RawImage overlay hierarchy root — owner: SuitHUDScreenCompositor
                GameObject overlayObject = new GameObject(overlayName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                overlayObject.transform.SetParent(canvasTransform, false);
                overlayTransform = overlayObject.transform;
            }

            if (showAsInsetPreview || !preserveExistingChildren)
            {
                int lastSiblingIndex = canvasTransform.childCount - 1;
                if (overlayTransform.GetSiblingIndex() != lastSiblingIndex)
                    overlayTransform.SetAsLastSibling();
            }
            else if (overlayTransform.GetSiblingIndex() != 0)
            {
                overlayTransform.SetAsFirstSibling();
            }

            _overlayRect = overlayTransform as RectTransform;
            overlayTransform.TryGetComponent(out _overlayImage);
            _appliedOverlayName = overlayName;

            if (_overlayRect == null || _overlayImage == null)
                return;

            _overlayCanvasGroup = EnsureCanvasGroup(_overlayRect, allowOverlayCreation);
            SetOverlayVisible(true);

            if (showAsInsetPreview)
            {
                Vector2 cornerAnchor = Vector2.one;
                Vector2 targetPosition = new Vector2(-insetMargin.x, -insetMargin.y);
                if (_overlayRect.anchorMin != cornerAnchor)
                    _overlayRect.anchorMin = cornerAnchor;
                if (_overlayRect.anchorMax != cornerAnchor)
                    _overlayRect.anchorMax = cornerAnchor;
                if (_overlayRect.pivot != cornerAnchor)
                    _overlayRect.pivot = cornerAnchor;
                if (_overlayRect.sizeDelta != insetSize)
                    _overlayRect.sizeDelta = insetSize;
                if (_overlayRect.anchoredPosition != targetPosition)
                    _overlayRect.anchoredPosition = targetPosition;
            }
            else
            {
                if (_overlayRect.anchorMin != Vector2.zero)
                    _overlayRect.anchorMin = Vector2.zero;
                if (_overlayRect.anchorMax != Vector2.one)
                    _overlayRect.anchorMax = Vector2.one;
                if (_overlayRect.offsetMin != Vector2.zero)
                    _overlayRect.offsetMin = Vector2.zero;
                if (_overlayRect.offsetMax != Vector2.zero)
                    _overlayRect.offsetMax = Vector2.zero;
                if (_overlayRect.anchoredPosition3D != Vector3.zero)
                    _overlayRect.anchoredPosition3D = Vector3.zero;
            }

            if (_overlayRect.localScale != Vector3.one)
                _overlayRect.localScale = Vector3.one;
            if (_overlayRect.localRotation != Quaternion.identity)
                _overlayRect.localRotation = Quaternion.identity;

            Color color = new Color(0.92f, 1f, 0.96f, overlayAlpha);
            if (_overlayImage.color != color)
                _overlayImage.color = color;
            if (_overlayImage.raycastTarget)
                _overlayImage.raycastTarget = false;
            debugOverlayReady = true;
        }

        private void EnsureProjection()
        {
            if (!forceSharedProjection || visorController == null)
                return;

            visorController.SetSharedRenderTexture(sharedProjectionTexture);
            visorController.SetProjectionMode(VisorHUDController.ProjectionMode.SharedRenderTexture);
        }

        private void BindTexture()
        {
            debugTextureAssigned = false;
            if (_overlayImage == null)
                return;

            if (_overlayImage.texture != sharedProjectionTexture)
                _overlayImage.texture = sharedProjectionTexture;
            debugTextureAssigned = sharedProjectionTexture != null;
            bool targetEnabled = !hideWhenTextureMissing || sharedProjectionTexture != null;
            if (_overlayImage.enabled != targetEnabled)
                _overlayImage.enabled = targetEnabled;
            SetOverlayVisible(targetEnabled);
        }

        private void SetOverlayVisible(bool visible)
        {
            if (_overlayCanvasGroup == null)
                return;

            float targetAlpha = visible ? 1f : 0f;
            if (_overlayCanvasGroup.alpha == targetAlpha &&
                !_overlayCanvasGroup.interactable &&
                !_overlayCanvasGroup.blocksRaycasts)
            {
                return;
            }

            _overlayCanvasGroup.alpha = targetAlpha;
            _overlayCanvasGroup.interactable = false;
            _overlayCanvasGroup.blocksRaycasts = false;
        }

        private void EnsureCachedOverlayState()
        {
            debugOverlayReady = false;
            if (_overlayRect == null || _overlayImage == null)
                return;

            Transform canvasTransform = targetCanvas != null ? targetCanvas.transform : null;
            if (canvasTransform != null && _overlayRect.parent != canvasTransform)
                return;

            if (showAsInsetPreview)
            {
                Vector2 cornerAnchor = Vector2.one;
                Vector2 targetPosition = new Vector2(-insetMargin.x, -insetMargin.y);
                if (_overlayRect.anchorMin != cornerAnchor)
                    _overlayRect.anchorMin = cornerAnchor;
                if (_overlayRect.anchorMax != cornerAnchor)
                    _overlayRect.anchorMax = cornerAnchor;
                if (_overlayRect.pivot != cornerAnchor)
                    _overlayRect.pivot = cornerAnchor;
                if (_overlayRect.sizeDelta != insetSize)
                    _overlayRect.sizeDelta = insetSize;
                if (_overlayRect.anchoredPosition != targetPosition)
                    _overlayRect.anchoredPosition = targetPosition;
            }

            if (_overlayRect.localScale != Vector3.one)
                _overlayRect.localScale = Vector3.one;
            if (_overlayRect.localRotation != Quaternion.identity)
                _overlayRect.localRotation = Quaternion.identity;

            Color color = new Color(0.92f, 1f, 0.96f, overlayAlpha);
            if (_overlayImage.color != color)
                _overlayImage.color = color;
            if (_overlayImage.raycastTarget)
                _overlayImage.raycastTarget = false;
            debugOverlayReady = _overlayCanvasGroup != null;
        }

        private void HideExistingOverlay()
        {
            if (targetCanvas == null)
                return;

            Transform overlayTransform = targetCanvas.transform.Find(overlayName);
            if (overlayTransform == null)
                return;

            if (overlayTransform.TryGetComponent(out RawImage rawImage))
                rawImage.enabled = false;

            if (!overlayTransform.TryGetComponent(out CanvasGroup canvasGroup))
                return;

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private static bool IsRuntimeScreenPreviewAllowed()
        {
#if UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }

        private static CanvasGroup EnsureCanvasGroup(RectTransform rect, bool allowCreation)
        {
            if (rect == null)
                return null;

            rect.TryGetComponent(out CanvasGroup canvasGroup);
            if (canvasGroup == null)
            {
                if (!allowCreation)
                    return null;

                canvasGroup = rect.gameObject.AddComponent<CanvasGroup>(); // COLD ALLOC: CanvasGroup[1] — compositor overlay visibility latch — owner: SuitHUDScreenCompositor
            }

            return canvasGroup;
        }

        private void TryRegisterRuntimeTick()
        {
            if (!Application.isPlaying || _tickRegistered || GlobalRegistry.Dispatcher == null)
                return;

            _tickRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void UnregisterRuntimeTick()
        {
            if (!_tickRegistered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);

            _tickRegistered = false;
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

        public void SetSharedProjectionTexture(RenderTexture texture)
        {
            if (sharedProjectionTexture == texture)
                return;

            sharedProjectionTexture = texture;
            MarkDirty();
        }

        public void SetOverlayAlpha(float alpha)
        {
            float clampedAlpha = math.saturate(alpha);
            if (math.abs(overlayAlpha - clampedAlpha) <= 0.0001f)
                return;

            overlayAlpha = clampedAlpha;
            MarkDirty();
        }

        private void MarkDirty()
        {
            _pendingRefresh = true;
            if (Application.isPlaying)
                TryRegisterRuntimeTick();
        }

#if UNITY_EDITOR
        [ContextMenu("Rebuild Screen Compositor")]
        private void RebuildScreenCompositor()
        {
            AutoResolveReferences(true);
            RefreshCompositor();
            _pendingRefresh = false;
        }
#endif

        private bool ShouldTickInEditMode()
        {
            return isActiveAndEnabled &&
                   manageCanvasInEditMode &&
                   (_pendingRefresh || NeedsAutoResolve());
        }

#if UNITY_EDITOR
        private void EvaluateEditorTickRegistration()
        {
            if (!IsEditorPreviewSafe())
            {
                UnregisterEditorTick();
                return;
            }

            if (Application.isPlaying)
            {
                UnregisterEditorTick();
                return;
            }

            if (ShouldTickInEditMode())
                RegisterEditorTick();
            else
                UnregisterEditorTick();
        }

        private void RegisterEditorTick()
        {
            if (!IsEditorPreviewSafe())
                return;

            EditorApplication.update -= EditorTick;
            EditorApplication.update += EditorTick;
        }

        private void UnregisterEditorTick()
        {
            EditorApplication.update -= EditorTick;
        }

        private static bool IsEditorPreviewSafe()
        {
            return !EditorApplication.isCompiling &&
                   !EditorApplication.isUpdating &&
                   !EditorApplication.isPlayingOrWillChangePlaymode;
        }
#endif

        private void RegisterActiveCompositor()
        {
            for (int i = 0; i < s_activeCompositorCount; i++)
            {
                if (ReferenceEquals(GetActiveCompositor(i), this))
                    return;
            }

            if (s_activeCompositorCount >= MaxActiveCompositors)
                return;

            SetActiveCompositorSlot(s_activeCompositorCount, this);
            s_activeCompositorCount++;
        }

        private void UnregisterActiveCompositor()
        {
            for (int i = 0; i < s_activeCompositorCount; i++)
            {
                if (!ReferenceEquals(GetActiveCompositor(i), this))
                    continue;

                int lastIndex = s_activeCompositorCount - 1;
                SetActiveCompositorSlot(i, GetActiveCompositor(lastIndex));
                SetActiveCompositorSlot(lastIndex, null);
                s_activeCompositorCount = lastIndex;
                return;
            }
        }

        internal static SuitHUDScreenCompositor GetActiveCompositor(int index)
        {
            switch (index)
            {
                case 0: return s_activeCompositor0;
                case 1: return s_activeCompositor1;
                default: return null;
            }
        }

        private static void SetActiveCompositorSlot(int index, SuitHUDScreenCompositor compositor)
        {
            switch (index)
            {
                case 0:
                    s_activeCompositor0 = compositor;
                    break;
                case 1:
                    s_activeCompositor1 = compositor;
                    break;
            }
        }
    }
}
