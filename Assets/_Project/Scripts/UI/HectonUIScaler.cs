using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    /// <summary>
    /// Canvas-root scaler that applies a single matrix-driven transform to a dedicated content root instead of using CanvasScaler relayout.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Hecton UI Scaler")]
    [RequireComponent(typeof(Canvas))]
    public sealed class HectonUIScaler : MonoBehaviour, ILateFrameTickable, ISlowTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener
    {
        private const string ContentRootName = "HectonUI_ScaledRoot";

        [Header("â”€â”€ Scale Policy â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Reference UI resolution used by the root transform matrix.")]
        [SerializeField] private Vector2 referenceResolution = new Vector2(1600f, 900f);
        [Tooltip("CanvasScaler-compatible logarithmic width/height blend. 0 = width, 1 = height.")]
        [SerializeField, Range(0f, 1f)] private float matchWidthOrHeight = 0.5f;
        [Tooltip("Lower clamp for the matrix scale to keep the HUD readable on 720p displays.")]
        [SerializeField, Range(0.5f, 1.5f)] private float minimumScale = 0.72f;
        [Tooltip("Upper clamp for the matrix scale so HUD chrome does not bloat on larger displays.")]
        [SerializeField, Range(0.75f, 2f)] private float maximumScale = 1.35f;
        [Tooltip("Aspect ratio where ultrawide compensation begins pulling edge-anchored HUD clusters inward.")]
        [SerializeField, Range(1.6f, 2.4f)] private float ultrawideAspectThreshold = 1.8f;
        [Tooltip("Maximum horizontal inset applied to the content root on very wide displays.")]
        [SerializeField, Range(0f, 320f)] private float ultrawideHorizontalInset = 128f;
        [Tooltip("Mild X-axis matrix correction applied on ultrawide displays so the HUD does not feel stretched.")]
        [SerializeField, Range(0.8f, 1f)] private float ultrawideScaleX = 0.94f;

        [Header("Zero Layout Groups")]
        [Tooltip("Disables Unity Horizontal/Vertical LayoutGroups under this content root. Runtime rows should use the manual linear layout below.")]
        [SerializeField] private bool disableLayoutGroupsUnderContentRoot = true;
        [Tooltip("Optional high-frequency UI items positioned by direct anchored-position writes instead of LayoutGroup rebuilds.")]
        [SerializeField] private RectTransform[] manualLinearLayoutItems;
        [Tooltip("Anchored position of manual item 0.")]
        [SerializeField] private Vector2 manualLayoutOrigin = Vector2.zero;
        [Tooltip("Per-item anchored-position delta.")]
        [SerializeField] private Vector2 manualLayoutStep = new Vector2(0f, -24f);
        [Tooltip("Size applied to each manual layout item.")]
        [SerializeField] private Vector2 manualLayoutItemSize = new Vector2(240f, 22f);

        private Canvas _targetCanvas;
        private RectTransform _contentRoot;
        private bool _registeredToTickManager;
        private bool _registeredToSlowTickManager;
        private bool _hotSwapRegistered;
        private bool _pendingContentRootBootstrap = true;
        private bool _runtimeActive;
        private int _cachedRenderWidth = 1;
        private int _cachedRenderHeight = 1;
        private int _lastScreenWidth = -1;
        private int _lastScreenHeight = -1;
        private float _lastAppliedScale = -1f;
        private Vector2 _lastAppliedReferenceResolution = Vector2.zero;
        private float _lastAppliedMatch = -1f;
        private Matrix4x4 _uiMatrix = Matrix4x4.identity;
        /// <summary>Current matrix applied to the scaled content root.</summary>
        public Matrix4x4 CurrentMatrix => _uiMatrix;

        /// <summary>Reference resolution currently used by the scaler.</summary>
        public Vector2 ReferenceResolution => referenceResolution;

        /// <summary>Scaled content parent used by first-party HUD overlays.</summary>
        public RectTransform ContentRoot => TryGetCachedContentRoot();

        private void OnEnable()
        {
            _runtimeActive = Application.isPlaying;
            ResolveCanvas();
            RefreshRenderDimensionsCold(force: true);
            RectTransform contentRoot = EnsureContentRoot();
            if (contentRoot == null)
            {
                _pendingContentRootBootstrap = true;
                return;
            }

            DisableUnityLayoutGroupsIfConfigured();
            ApplyManualLinearLayout();
            ApplyScale(force: true);
            _pendingContentRootBootstrap = ResolveContentRootInternal(createIfMissing: false) == null;
            if (!_runtimeActive)
                return;

            TryRegisterHotSwapListener();
            HectonFloatingOrigin.RegisterListener(this);
            RegisterToTickManager();
        }

        private void OnDisable()
        {
            _runtimeActive = false;
            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregisterHotSwapListener();
            UnregisterFromTickManager();
        }

        private void OnDestroy()
        {
            _runtimeActive = false;
            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregisterHotSwapListener();
            UnregisterFromTickManager();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                if (currentService == null)
                {
                    _registeredToTickManager = false;
                    _registeredToSlowTickManager = false;
                    return;
                }

                UnregisterFromTickManager();
                RegisterToTickManager();
            }
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            if (_pendingContentRootBootstrap)
            {
                RectTransform bootstrappedRoot = TryGetCachedContentRoot();
                if (bootstrappedRoot == null)
                    return;

                ApplyManualLinearLayout();
                ApplyScaleToCachedRoot(bootstrappedRoot, force: true);
                _pendingContentRootBootstrap = false;
                return;
            }

            RectTransform contentRoot = TryGetCachedContentRoot();
            if (contentRoot == null)
            {
                _pendingContentRootBootstrap = true;
                return;
            }

            ApplyScaleToCachedRoot(contentRoot, force: false);
        }

        public void SlowTick()
        {
            if (!_runtimeActive)
                return;

            RefreshRenderDimensionsCold(force: false);

            RectTransform contentRoot = TryGetCachedContentRoot();
            if (_pendingContentRootBootstrap)
            {
                if (contentRoot == null && TryResolveExistingContentRootCold())
                    contentRoot = _contentRoot;

                if (contentRoot == null)
                    contentRoot = EnsureContentRoot();

                if (contentRoot == null)
                {
                    _pendingContentRootBootstrap = true;
                    return;
                }

                ApplyManualLinearLayout();
                ApplyScaleToCachedRoot(contentRoot, force: true);
                _pendingContentRootBootstrap = false;
                return;
            }

            if (contentRoot != null)
                return;

            _pendingContentRootBootstrap = true;
        }

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            _lastScreenWidth = -1;
            _lastScreenHeight = -1;
            _lastAppliedScale = -1f;
            _lastAppliedReferenceResolution = Vector2.zero;
            _lastAppliedMatch = -1f;
            RectTransform contentRoot = TryGetCachedContentRoot();
            _pendingContentRootBootstrap = contentRoot == null;

            if (_pendingContentRootBootstrap)
                return;

            ApplyScaleToCachedRoot(contentRoot, force: true);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            referenceResolution = new Vector2(
                Mathf.Max(1f, referenceResolution.x),
                Mathf.Max(1f, referenceResolution.y));
            matchWidthOrHeight = Mathf.Clamp01(matchWidthOrHeight);
            minimumScale = Mathf.Max(0.1f, minimumScale);
            maximumScale = Mathf.Max(minimumScale, maximumScale);
            ultrawideAspectThreshold = Mathf.Clamp(ultrawideAspectThreshold, 1.1f, 3f);
            ultrawideHorizontalInset = Mathf.Max(0f, ultrawideHorizontalInset);
            ultrawideScaleX = Mathf.Clamp(ultrawideScaleX, 0.1f, 1f);
        }

        [ContextMenu("Rebuild UI")]
        private void RebuildUiInEditor()
        {
            if (Application.isPlaying)
                return;

            ResolveCanvas();
            EnsureContentRoot();
            ApplyManualLinearLayout();
            ApplyScale(force: true);
        }
#endif

        /// <summary>
        /// Configures the scaler from an owning canvas system.
        /// </summary>
        public void Configure(Vector2 nextReferenceResolution, float nextMatchWidthOrHeight)
        {
            Vector2 sanitizedResolution = new Vector2(
                Mathf.Max(1f, nextReferenceResolution.x),
                Mathf.Max(1f, nextReferenceResolution.y));
            float sanitizedMatch = Mathf.Clamp01(nextMatchWidthOrHeight);
            if (Approximately(referenceResolution, sanitizedResolution) &&
                Mathf.Approximately(matchWidthOrHeight, sanitizedMatch))
            {
                return;
            }

            referenceResolution = sanitizedResolution;
            matchWidthOrHeight = sanitizedMatch;
            if (Application.isPlaying)
            {
                RefreshRenderDimensionsCold(force: true);
                _pendingContentRootBootstrap = _pendingContentRootBootstrap || TryGetCachedContentRoot() == null;
                if (!_pendingContentRootBootstrap)
                    ApplyScale(force: true);
            }
            else
            {
                ApplyScale(force: true);
            }
        }

        /// <summary>
        /// Resolves the scaled content parent for a canvas, or falls back to the canvas RectTransform when no scaler is present.
        /// </summary>
        public static RectTransform ResolveContentRoot(Canvas canvas)
        {
            if (canvas == null)
                return null;

            RectTransform canvasRoot = canvas.transform as RectTransform;
            RectTransform contentRoot = FindExistingChild(canvasRoot, ContentRootName);
            if (contentRoot != null)
                return contentRoot;

            return canvasRoot;
        }

        private RectTransform ResolveContentRootInternal(bool createIfMissing)
        {
            if (_targetCanvas == null)
            {
                if (!createIfMissing)
                    return null;

                ResolveCanvas();
            }

            if (_targetCanvas == null)
                return null;

            RectTransform canvasRoot = _targetCanvas.transform as RectTransform;
            if (canvasRoot == null)
                return null;

            if (_contentRoot != null && _contentRoot.gameObject != null)
                return SanitizeContentRoot(_contentRoot);

            _contentRoot = FindExistingChild(canvasRoot, ContentRootName);
            if (_contentRoot != null)
                return SanitizeContentRoot(_contentRoot);

            if (!createIfMissing)
                return null;

            return EnsureContentRoot();
        }

        private void ResolveCanvas()
        {
            if (_targetCanvas == null)
                TryGetComponent(out _targetCanvas);
        }

        private RectTransform TryGetCachedContentRoot()
        {
            return _contentRoot != null ? _contentRoot : null;
        }

        private bool TryResolveExistingContentRootCold()
        {
            if (_targetCanvas == null)
                ResolveCanvas();

            if (_targetCanvas == null)
                return false;

            RectTransform contentRoot = TryGetCachedContentRoot();
            if (contentRoot != null)
                return true;

            RectTransform canvasRoot = _targetCanvas.transform as RectTransform;
            if (canvasRoot == null)
                return false;

            _contentRoot = FindExistingChild(canvasRoot, ContentRootName);
            if (_contentRoot == null)
                return false;

            _contentRoot = SanitizeContentRoot(_contentRoot);
            return _contentRoot != null;
        }

        private RectTransform EnsureContentRoot()
        {
            ResolveCanvas();
            if (_targetCanvas == null)
                return null;

            RectTransform canvasRoot = _targetCanvas.transform as RectTransform;
            if (canvasRoot == null)
                return null;

            if (_contentRoot == null || _contentRoot.gameObject == null)
                _contentRoot = FindExistingChild(canvasRoot, ContentRootName);

            if (_contentRoot == null)
            {
                // COLD ALLOC: GameObject[1] — matrix-scaled HUD content root — owner: HectonUIScaler
                GameObject rootObject = new GameObject(ContentRootName, typeof(RectTransform));
                rootObject.layer = canvasRoot.gameObject.layer;
                _contentRoot = rootObject.transform as RectTransform;
                _contentRoot.SetParent(canvasRoot, false);
            }

            return SanitizeContentRoot(_contentRoot);
        }

        private RectTransform SanitizeContentRoot(RectTransform contentRoot)
        {
            if (contentRoot == null)
                return null;

            Vector2 centered = new Vector2(0.5f, 0.5f);
            if (contentRoot.anchorMin != centered)
                contentRoot.anchorMin = centered;
            if (contentRoot.anchorMax != centered)
                contentRoot.anchorMax = centered;
            if (contentRoot.pivot != centered)
                contentRoot.pivot = centered;
            if (contentRoot.anchoredPosition != Vector2.zero)
                contentRoot.anchoredPosition = Vector2.zero;
            if (contentRoot.localRotation != Quaternion.identity)
                contentRoot.localRotation = Quaternion.identity;
            return contentRoot;
        }

        private void ApplyScale(bool force)
        {
            RectTransform contentRoot = TryGetCachedContentRoot();
            if (contentRoot == null)
            {
                if (Application.isPlaying)
                    _pendingContentRootBootstrap = true;
                else
                    contentRoot = ResolveContentRootInternal(createIfMissing: false);
            }

            if (contentRoot == null)
                return;

            ApplyScaleToCachedRoot(contentRoot, force);
        }

        private void ApplyScaleToCachedRoot(RectTransform contentRoot, bool force)
        {
            if (contentRoot == null)
                return;

            ResolveRenderDimensions(out int screenWidth, out int screenHeight);
            if (!force &&
                screenWidth == _lastScreenWidth &&
                screenHeight == _lastScreenHeight &&
                Approximately(referenceResolution, _lastAppliedReferenceResolution) &&
                Mathf.Approximately(matchWidthOrHeight, _lastAppliedMatch))
            {
                return;
            }

            bool isPhysicalWorldSpaceCanvas =
                _targetCanvas != null &&
                _targetCanvas.renderMode == RenderMode.WorldSpace;
            float scale = isPhysicalWorldSpaceCanvas ? 1f : ComputeScale(screenWidth, screenHeight);
            ResolveUltrawideAdjustments(screenWidth, screenHeight, isPhysicalWorldSpaceCanvas, out float aspectScaleX, out float horizontalInset);
            Vector2 resolvedContentSize = new Vector2(
                Mathf.Max(1f, referenceResolution.x - (horizontalInset * 2f)),
                referenceResolution.y);
            if (!force &&
                Mathf.Approximately(scale, _lastAppliedScale) &&
                contentRoot.sizeDelta == resolvedContentSize)
            {
                _lastScreenWidth = screenWidth;
                _lastScreenHeight = screenHeight;
                return;
            }

            _uiMatrix = Matrix4x4.Scale(new Vector3(scale * aspectScaleX, scale, 1f));

            contentRoot.sizeDelta = resolvedContentSize;
            contentRoot.anchoredPosition = Vector2.zero;
            contentRoot.localScale = new Vector3(scale * aspectScaleX, scale, 1f);
            if (_targetCanvas != null && !Mathf.Approximately(_targetCanvas.scaleFactor, 1f))
                _targetCanvas.scaleFactor = 1f;

            _lastScreenWidth = screenWidth;
            _lastScreenHeight = screenHeight;
            _lastAppliedScale = scale;
            _lastAppliedReferenceResolution = referenceResolution;
            _lastAppliedMatch = matchWidthOrHeight;
        }

        private void ResolveRenderDimensions(out int width, out int height)
        {
            width = Mathf.Max(1, _cachedRenderWidth);
            height = Mathf.Max(1, _cachedRenderHeight);
        }

        private void RefreshRenderDimensionsCold(bool force)
        {
            int width = Mathf.Max(1, Screen.width);
            int height = Mathf.Max(1, Screen.height);

            if (_targetCanvas != null &&
                _targetCanvas.renderMode == RenderMode.WorldSpace &&
                _targetCanvas.worldCamera != null)
            {
                // World-space HUD layout is already projected onto the visor frustum by the canvas rect itself.
                // Scaling again from RT resolution collapses the authored layout toward the center as the RT downsamples.
                width = Mathf.RoundToInt(Mathf.Max(1f, referenceResolution.x));
                height = Mathf.RoundToInt(Mathf.Max(1f, referenceResolution.y));
            }

            if (!force && width == _cachedRenderWidth && height == _cachedRenderHeight)
                return;

            _cachedRenderWidth = width;
            _cachedRenderHeight = height;
        }

        private float ComputeScale(int screenWidth, int screenHeight)
        {
            float scaleX = screenWidth / Mathf.Max(1f, referenceResolution.x);
            float scaleY = screenHeight / Mathf.Max(1f, referenceResolution.y);
            float blendedScale = math.lerp(scaleX, scaleY, matchWidthOrHeight);
            return Mathf.Clamp(blendedScale, minimumScale, maximumScale);
        }

        private void ResolveUltrawideAdjustments(
            int screenWidth,
            int screenHeight,
            bool isPhysicalWorldSpaceCanvas,
            out float aspectScaleX,
            out float horizontalInset)
        {
            aspectScaleX = 1f;
            horizontalInset = 0f;
            if (isPhysicalWorldSpaceCanvas)
                return;

            float aspect = screenHeight > 0 ? screenWidth / (float)screenHeight : 1f;
            if (aspect <= ultrawideAspectThreshold)
                return;

            float normalizedWide = Mathf.Clamp01((aspect - ultrawideAspectThreshold) / Mathf.Max(0.01f, 2.4f - ultrawideAspectThreshold));
            aspectScaleX = math.lerp(1f, ultrawideScaleX, normalizedWide);
            horizontalInset = ultrawideHorizontalInset * normalizedWide;
        }

        private void DisableUnityLayoutGroupsIfConfigured()
        {
            if (!disableLayoutGroupsUnderContentRoot)
                return;

            RectTransform contentRoot = ResolveContentRootInternal(createIfMissing: false);
            if (contentRoot == null)
                return;

            DisableUnityLayoutGroupsInHierarchy(contentRoot);
        }

        private static void DisableUnityLayoutGroupsInHierarchy(Transform root)
        {
            if (root == null)
                return;

            if (root.TryGetComponent(out HorizontalOrVerticalLayoutGroup layoutGroup) && layoutGroup.enabled)
                layoutGroup.enabled = false;

            for (int i = 0; i < root.childCount; i++)
                DisableUnityLayoutGroupsInHierarchy(root.GetChild(i));
        }

        private void ApplyManualLinearLayout()
        {
            if (manualLinearLayoutItems == null || manualLinearLayoutItems.Length == 0)
                return;

            int itemCount = ResolveManualLayoutItemCount();
            if (itemCount <= 0)
                return;

            for (int i = 0; i < itemCount; i++)
            {
                RectTransform item = manualLinearLayoutItems[i];
                if (item == null)
                    continue;

                item.anchoredPosition = manualLayoutOrigin + manualLayoutStep * i;
                item.sizeDelta = manualLayoutItemSize;
            }
        }

        private int ResolveManualLayoutItemCount()
        {
            if (manualLinearLayoutItems == null)
                return 0;

            return manualLinearLayoutItems.Length;
        }

        private void RegisterToTickManager()
        {
            if (!Application.isPlaying)
                return;

            if (_registeredToTickManager && _registeredToSlowTickManager)
                return;

            if (!_registeredToTickManager && GlobalRegistry.Dispatcher != null)
            {
                _registeredToTickManager = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
            }

            if (_registeredToSlowTickManager || GlobalRegistry.Dispatcher == null)
                return;

            _registeredToSlowTickManager = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.UI);
        }

        private void UnregisterFromTickManager()
        {
            if (_registeredToTickManager)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _registeredToTickManager = false;
            }

            if (!_registeredToSlowTickManager)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.UI);
            _registeredToSlowTickManager = false;
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

        private static RectTransform FindExistingChild(Transform parent, string childName)
        {
            if (parent == null)
                return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName)
                    return child as RectTransform;
            }

            return null;
        }

        private static bool Approximately(Vector2 a, Vector2 b)
        {
            return Mathf.Approximately(a.x, b.x) &&
                   Mathf.Approximately(a.y, b.y);
        }
    }
}
