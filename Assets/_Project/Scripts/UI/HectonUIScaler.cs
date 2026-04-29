using Hecton8.Core;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Canvas-root scaler that applies a single matrix-driven transform to a dedicated content root instead of using CanvasScaler relayout.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Hecton UI Scaler")]
    [RequireComponent(typeof(Canvas))]
    public sealed class HectonUIScaler : MonoBehaviour, ITickable, IUpdatable, ISlowTickable, IOriginShiftListener
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

        private Canvas _targetCanvas;
        private RectTransform _contentRoot;
        private bool _registeredToTickManager;
        private bool _registeredToSlowTickManager;
        private bool _pendingContentRootBootstrap = true;
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
        public RectTransform ContentRoot => ResolveContentRootInternal(createIfMissing: Application.isPlaying);

        private void OnEnable()
        {
            ResolveCanvas();
            EnsureContentRoot();
            ApplyScale(force: true);
            _pendingContentRootBootstrap = ResolveContentRootInternal(createIfMissing: false) == null;
            if (!Application.isPlaying)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            RegisterToTickManager();
        }

        private void OnDisable()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            UnregisterFromTickManager();
        }

        private void OnDestroy()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            UnregisterFromTickManager();
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
            if (_pendingContentRootBootstrap)
                return;

            if (ResolveContentRootInternal(createIfMissing: false) == null)
                return;

            ApplyScale(force: false);
        }

        public void SlowTick()
        {
            if (!Application.isPlaying)
                return;

            RegisterToTickManager();
            if (!_pendingContentRootBootstrap && ResolveContentRootInternal(createIfMissing: false) != null)
                return;

            EnsureContentRoot();
            ApplyScale(force: true);
            _pendingContentRootBootstrap = ResolveContentRootInternal(createIfMissing: false) == null;
        }

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            _lastScreenWidth = -1;
            _lastScreenHeight = -1;
            _lastAppliedScale = -1f;
            _lastAppliedReferenceResolution = Vector2.zero;
            _lastAppliedMatch = -1f;
            _pendingContentRootBootstrap = ResolveContentRootInternal(createIfMissing: false) == null;

            if (_pendingContentRootBootstrap)
                return;

            ApplyScale(force: true);
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
                _pendingContentRootBootstrap = _pendingContentRootBootstrap || ResolveContentRootInternal(createIfMissing: false) == null;
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

            if (canvas.TryGetComponent(out HectonUIScaler scaler))
            {
                RectTransform contentRoot = scaler.ResolveContentRootInternal(createIfMissing: false);
                if (contentRoot != null)
                    return contentRoot;
            }

            return canvas.transform as RectTransform;
        }

        private RectTransform ResolveContentRootInternal(bool createIfMissing)
        {
            ResolveCanvas();
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
                _targetCanvas = GetComponent<Canvas>();
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
                // COLD ALLOC: GameObject[1] â€” matrix-scaled HUD content root â€” owner: HectonUIScaler
                GameObject rootObject = new GameObject(ContentRootName, typeof(RectTransform));
                rootObject.layer = canvasRoot.gameObject.layer;
                _contentRoot = rootObject.GetComponent<RectTransform>();
                _contentRoot.SetParent(canvasRoot, false);
            }

            return SanitizeContentRoot(_contentRoot);
        }

        private RectTransform SanitizeContentRoot(RectTransform contentRoot)
        {
            if (contentRoot == null)
                return null;

            contentRoot.anchorMin = new Vector2(0.5f, 0.5f);
            contentRoot.anchorMax = new Vector2(0.5f, 0.5f);
            contentRoot.pivot = new Vector2(0.5f, 0.5f);
            contentRoot.anchoredPosition = Vector2.zero;
            contentRoot.localRotation = Quaternion.identity;
            // Stamp the reference rect immediately so stretched HUD children never inherit Unity's 100x100 default RectTransform.
            contentRoot.sizeDelta = referenceResolution;
            contentRoot.localScale = Vector3.one;
            return contentRoot;
        }

        private void ApplyScale(bool force)
        {
            RectTransform contentRoot = EnsureContentRoot();
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
            width = Mathf.Max(1, Screen.width);
            height = Mathf.Max(1, Screen.height);

            if (_targetCanvas == null ||
                _targetCanvas.renderMode != RenderMode.WorldSpace ||
                _targetCanvas.worldCamera == null)
            {
                return;
            }

            // World-space HUD layout is already projected onto the visor frustum by the canvas rect itself.
            // Scaling again from RT resolution collapses the authored layout toward the center as the RT downsamples.
            width = Mathf.RoundToInt(Mathf.Max(1f, referenceResolution.x));
            height = Mathf.RoundToInt(Mathf.Max(1f, referenceResolution.y));
        }

        private float ComputeScale(int screenWidth, int screenHeight)
        {
            float scaleX = screenWidth / Mathf.Max(1f, referenceResolution.x);
            float scaleY = screenHeight / Mathf.Max(1f, referenceResolution.y);
            float logWidth = Mathf.Log(Mathf.Max(0.0001f, scaleX), 2f);
            float logHeight = Mathf.Log(Mathf.Max(0.0001f, scaleY), 2f);
            float blendedScale = Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, matchWidthOrHeight));
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
            aspectScaleX = Mathf.Lerp(1f, ultrawideScaleX, normalizedWide);
            horizontalInset = ultrawideHorizontalInset * normalizedWide;
        }

        private void RegisterToTickManager()
        {
            if (!Application.isPlaying)
                return;

            if (!_registeredToTickManager && GlobalRegistry.Dispatcher != null)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
                _registeredToTickManager = true;
            }

            if (_registeredToSlowTickManager || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.UI);
            _registeredToSlowTickManager = true;
        }

        private void UnregisterFromTickManager()
        {
            if (_registeredToTickManager)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
                _registeredToTickManager = false;
            }

            if (!_registeredToSlowTickManager)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.UI);
            _registeredToSlowTickManager = false;
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
