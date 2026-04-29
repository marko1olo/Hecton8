using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.UI;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NASAPunk.Visor
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/HUD/Suit HUD Presentation Controller")]
    public sealed class SuitHUDPresentationController : MonoBehaviour, ITickable, IUpdatable
    {
        private const float AutoResolveRetryInterval = 1f;
        private static readonly List<SuitHUDV4CanvasOverlay> s_overlayResolveBuffer = new List<SuitHUDV4CanvasOverlay>(4);
        private static readonly List<SuitHUDScreenCompositor> s_compositorResolveBuffer = new List<SuitHUDScreenCompositor>(2);

        public enum PresentationMode
        {
            LegacyOverlay,
            ModernOverlay,
            ModernProjectedSharedRT,
            ModernProjectedRuntimeRT
        }

        [Header("Mode")]
        [SerializeField] private PresentationMode presentationMode = PresentationMode.ModernProjectedSharedRT;

        [Header("Core References")]
        [SerializeField] private HectonSuitHUD_v4 overlayModernHud;
        [SerializeField] private HectonSuitHUD_v4 projectedModernHud;
        [SerializeField] private VisorHUDController visorController;
        [SerializeField] private SuitHUDProfile standardFallbackProfile;
        [SerializeField] private RenderTexture sharedProjectionTexture;
        [SerializeField] private Camera overlayPresentationCamera;
        [SerializeField] private Camera visorProjectionCamera;

        [Header("Overlay Suppression")]
        [SerializeField] private SuitHUDV4CanvasOverlay canvasOverlay;
        [SerializeField] private SuitHUDV4CanvasOverlay projectionSourceOverlay;
        [SerializeField] private SuitHUDScreenCompositor screenCompositor;
        [SerializeField] private bool suppressOverlaysInProjectedMode = true;
        [SerializeField] private bool previewProjectedSourceOnScreen = true;
        [SerializeField] private bool preferCanvasProjectionSource = true;
        [SerializeField] private bool syncProjectionLayoutFromOverlay = false;

        [Header("Diagnostics")]
        [SerializeField] private string debugAppliedModeLabel;
        [SerializeField] private bool debugModernEnabled;
        [SerializeField] private bool debugProjectedModeActive;
        [SerializeField] private bool debugOverlaysSuppressed;

        private PresentationMode _appliedMode = (PresentationMode)(-1);
        private SuitHUDProfile _appliedFallbackProfile;
        private RenderTexture _appliedSharedTexture;
        private bool _pendingApply = true;
        private float _nextAutoResolveAt;
        private bool _tickRegistered;
        private Transform _cachedHudCanvasTransform;
        private Transform _cachedHudRtCompositorTransform;
        private bool _fallbackToOverlayActive;
        private SuitHUDV4CanvasOverlay _normalizedProjectionSourceOverlay;
        private bool _editorPresentationStateCached;
        private bool _editorLastProjectedPresentationAvailable;
        private bool _editorLastProjectedOutputSurfaceAvailable;
        private const string ProjectionSourceCanvasName = "Suit_HUD_ProjectionSource";
        private const int ProjectionSourceLayer = 17;

        private void OnEnable()
        {
            _pendingApply = true;
            if (!Application.isPlaying)
            {
#if UNITY_EDITOR
                EvaluateEditorTickRegistration();
#endif
                return;
            }

            AutoResolveReferences(true);
            ApplyPresentation(force: true);
            _pendingApply = false;
            EvaluateTickRegistration();
        }

        private void Start()
        {
            EvaluateTickRegistration();
        }

        private void OnDisable()
        {
            UnregisterTick();
#if UNITY_EDITOR
            UnregisterEditorTick();
#endif
        }

        private void OnValidate()
        {
            _pendingApply = true;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                EvaluateEditorTickRegistration();
#endif
        }

#if UNITY_EDITOR
        private void EditorTick()
        {
            if (this == null || !this)
            {
                UnregisterEditorTick();
                return;
            }

            if (Application.isPlaying || !isActiveAndEnabled)
            {
                UnregisterEditorTick();
                return;
            }

            if (!UnityEditorInternal.InternalEditorUtility.isApplicationActive)
                return;

            AutoResolveReferences();
            ApplyPresentation(force: _pendingApply);
            _pendingApply = false;
            CacheEditorPresentationState();
            if (!ShouldTickInEditMode())
                UnregisterEditorTick();
        }
#endif

        public void Tick(float deltaTime)
        {
            AutoResolveReferences();
            ApplyPresentation(force: _pendingApply);
            _pendingApply = false;
            EvaluateTickRegistration();
        }

        private void AutoResolveReferences(bool force = false)
        {
            if (!force && !NeedsAutoResolve())
                return;

            float now = Time.realtimeSinceStartup;
            if (!force && now < _nextAutoResolveAt)
                return;

            _nextAutoResolveAt = now + AutoResolveRetryInterval;

            if (visorProjectionCamera == null)
                visorProjectionCamera = GetComponent<Camera>();

            if (overlayPresentationCamera == null)
            {
                Transform parent = transform.parent;
                if (parent != null)
                {
                    Transform mainCameraTransform = parent.Find("Main Camera");
                    if (mainCameraTransform != null)
                        overlayPresentationCamera = mainCameraTransform.GetComponent<Camera>();
                }
            }

            if (projectedModernHud == null)
                projectedModernHud = GetComponent<HectonSuitHUD_v4>();

            if (overlayModernHud == null && overlayPresentationCamera != null)
                overlayModernHud = overlayPresentationCamera.GetComponent<HectonSuitHUD_v4>();

            if (visorController == null)
            {
                Transform parent = transform.parent;
                if (parent != null)
                {
                    Transform visor = parent.Find("Suit_Visor");
                    if (visor != null)
                        visorController = visor.GetComponent<VisorHUDController>();
                }
            }

            bool projectedCanvasSourceNeeded =
                preferCanvasProjectionSource &&
                (presentationMode == PresentationMode.ModernProjectedSharedRT ||
                 presentationMode == PresentationMode.ModernProjectedRuntimeRT);

            if (canvasOverlay == null || (projectedCanvasSourceNeeded && projectionSourceOverlay == null))
            {
                SuitHUDV4CanvasOverlay.CopyActiveOverlaysTo(s_overlayResolveBuffer);
                Transform root = transform.root;
                canvasOverlay = FindOverlayByName(s_overlayResolveBuffer, "Suit_HUD_Canvas", canvasOverlay, root);
                projectionSourceOverlay = FindOverlayByName(s_overlayResolveBuffer, ProjectionSourceCanvasName, projectionSourceOverlay, root);
                s_overlayResolveBuffer.Clear();
            }

            if (screenCompositor == null)
            {
                SuitHUDScreenCompositor.CopyActiveCompositorsTo(s_compositorResolveBuffer);
                screenCompositor = FindCompositor(s_compositorResolveBuffer, screenCompositor, transform.root);
                s_compositorResolveBuffer.Clear();
            }

            if (sharedProjectionTexture == null && visorController != null)
                sharedProjectionTexture = visorController.SharedRenderTexture;

            AutoRecoverInvisibleHybridOverlay();
        }

        private bool NeedsAutoResolve()
        {
            if (projectedModernHud == null ||
                visorProjectionCamera == null ||
                overlayPresentationCamera == null ||
                overlayModernHud == null ||
                visorController == null ||
                canvasOverlay == null ||
                screenCompositor == null)
            {
                return true;
            }

            bool projectedCanvasSourceNeeded =
                preferCanvasProjectionSource &&
                (presentationMode == PresentationMode.ModernProjectedSharedRT ||
                 presentationMode == PresentationMode.ModernProjectedRuntimeRT);

            return projectedCanvasSourceNeeded && projectionSourceOverlay == null;
        }

        private void ApplyPresentation(bool force)
        {
            bool projectedModeRequested =
                presentationMode == PresentationMode.ModernProjectedSharedRT ||
                presentationMode == PresentationMode.ModernProjectedRuntimeRT;

            EnsureProjectionSource(projectedModeRequested);
            bool projectedMode = projectedModeRequested && IsProjectedPresentationAvailable();

            if (!force &&
                _appliedMode == presentationMode &&
                ReferenceEquals(_appliedFallbackProfile, standardFallbackProfile) &&
                ReferenceEquals(_appliedSharedTexture, sharedProjectionTexture) &&
                (!projectedModeRequested || projectionSourceOverlay != null) &&
                _fallbackToOverlayActive != (projectedModeRequested && !projectedMode))
            {
                return;
            }

            PrepareModernHud(overlayModernHud, ResolveOverlayHostCamera());
            PrepareModernHud(projectedModernHud, visorProjectionCamera);

            bool useOverlayModern = !projectedMode;
            bool useProjectedModern = projectedMode;

            if (visorController != null)
            {
                visorController.SetSharedRenderTexture(sharedProjectionTexture);
                visorController.SetProjectionMode(ResolveProjectionMode(projectedMode
                    ? presentationMode
                    : PresentationMode.ModernOverlay));
            }

            if (preferCanvasProjectionSource && projectedMode && projectionSourceOverlay != null)
                useProjectedModern = false;

            if (overlayModernHud != null)
                overlayModernHud.enabled = useOverlayModern;

            if (projectedModernHud != null)
                projectedModernHud.enabled = useProjectedModern;

            SuppressOverlayPaths(projectedMode);

            debugAppliedModeLabel = projectedModeRequested && !projectedMode
                ? presentationMode + " -> FallbackOverlay"
                : presentationMode == PresentationMode.LegacyOverlay
                    ? "ModernOverlay (legacy retired)"
                    : ResolvePresentationModeLabel(presentationMode);
            debugModernEnabled =
                (overlayModernHud != null && overlayModernHud.enabled) ||
                (projectedModernHud != null && projectedModernHud.enabled);
            debugProjectedModeActive = projectedMode;
            _fallbackToOverlayActive = projectedModeRequested && !projectedMode;

            _appliedMode = presentationMode;
            _appliedFallbackProfile = standardFallbackProfile;
            _appliedSharedTexture = sharedProjectionTexture;
        }

        private void AutoRecoverInvisibleHybridOverlay()
        {
            if (!Application.isPlaying ||
                presentationMode != PresentationMode.ModernOverlay ||
                !preferCanvasProjectionSource ||
                visorProjectionCamera == null ||
                visorController == null ||
                sharedProjectionTexture == null)
            {
                return;
            }

            if (canvasOverlay == null)
            {
                presentationMode = PresentationMode.ModernProjectedSharedRT;
                _pendingApply = true;
                return;
            }

            Canvas overlayCanvas = canvasOverlay.TargetCanvas;
            if (overlayCanvas == null ||
                overlayCanvas.renderMode == RenderMode.WorldSpace ||
                overlayCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                presentationMode = PresentationMode.ModernProjectedSharedRT;
                _pendingApply = true;
                return;
            }
        }

        private void SuppressOverlayPaths(bool projectedMode)
        {
            bool suppress = projectedMode && suppressOverlaysInProjectedMode;
            bool showProjectionPreview = projectedMode && previewProjectedSourceOnScreen;

            if (canvasOverlay != null)
            {
                canvasOverlay.SetRenderPathProjectionSource(false);
                _cachedHudCanvasTransform = canvasOverlay.transform;
            }

            if (screenCompositor != null)
                screenCompositor.enabled = showProjectionPreview;

            Transform compositorTransform = ResolveHudRtCompositorTransform();
            SetTransformCanvasVisible(compositorTransform, showProjectionPreview);

            debugOverlaysSuppressed = suppress && !showProjectionPreview;
        }

        private bool IsProjectedPresentationAvailable()
        {
            if (visorProjectionCamera == null || !visorProjectionCamera.gameObject.activeInHierarchy)
                return false;

            if (visorController == null || !visorController.isActiveAndEnabled)
                return false;

            if (presentationMode == PresentationMode.ModernProjectedSharedRT && sharedProjectionTexture == null)
                return false;

            if (!HasProjectedOutputSurface())
                return false;

            if (preferCanvasProjectionSource && projectionSourceOverlay == null)
                return false;

            if (previewProjectedSourceOnScreen && screenCompositor == null)
                return false;

            return true;
        }

        private bool HasProjectedOutputSurface()
        {
            if (visorController == null || !visorController.isActiveAndEnabled)
                return false;

            Renderer visorRenderer = visorController.GetComponent<Renderer>();
            Camera hudCamera = visorProjectionCamera != null ? visorProjectionCamera : visorController.HudCamera;
            return hudCamera != null &&
                   hudCamera.gameObject.activeInHierarchy &&
                   visorRenderer != null &&
                   visorRenderer.enabled &&
                   !visorRenderer.forceRenderingOff &&
                   visorRenderer.gameObject.activeInHierarchy;
        }

        private void EnsureProjectionSource(bool projectedMode)
        {
            if (!projectedMode)
            {
                if (projectionSourceOverlay != null)
                    SetOverlayCanvasVisible(projectionSourceOverlay, false);

                return;
            }

            if (canvasOverlay == null || visorProjectionCamera == null)
                return;

            bool createdThisPass = false;
            if (projectionSourceOverlay == null)
            {
                projectionSourceOverlay = CreateProjectionSourceOverlay();
                createdThisPass = projectionSourceOverlay != null;
            }

            if (projectionSourceOverlay == null)
                return;

            SetOverlayCanvasVisible(projectionSourceOverlay, true);

            if (!ReferenceEquals(_normalizedProjectionSourceOverlay, projectionSourceOverlay))
            {
                NormalizeProjectionSourceOverlay(projectionSourceOverlay);
                _normalizedProjectionSourceOverlay = projectionSourceOverlay;
            }

            if (syncProjectionLayoutFromOverlay || createdThisPass)
                projectionSourceOverlay.CopyConfigurationFrom(canvasOverlay);
            projectionSourceOverlay.SetProjectionCamera(visorProjectionCamera);
            projectionSourceOverlay.SetRenderPathProjectionSource(true);
        }

        private SuitHUDV4CanvasOverlay CreateProjectionSourceOverlay()
        {
            Transform parent = canvasOverlay != null ? canvasOverlay.transform.parent : null;
            GameObject go = new GameObject(
                ProjectionSourceCanvasName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(GraphicRaycaster),
                typeof(SuitHUDV4CanvasOverlay),
                typeof(HectonUIScaler));

            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            go.layer = ProjectionSourceLayer;

            GraphicRaycaster raycaster = go.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
                raycaster.enabled = false;

            return go.GetComponent<SuitHUDV4CanvasOverlay>();
        }

        private static void NormalizeProjectionSourceOverlay(SuitHUDV4CanvasOverlay overlay)
        {
            if (overlay == null)
                return;

            GameObject overlayObject = overlay.gameObject;
            if (overlayObject.layer != ProjectionSourceLayer)
                overlayObject.layer = ProjectionSourceLayer;

            if (overlay.TryGetComponent(out CanvasScaler canvasScaler) && canvasScaler.enabled)
                canvasScaler.enabled = false;

            if (overlay.TryGetComponent(out GraphicRaycaster raycaster) && raycaster.enabled)
                raycaster.enabled = false;

            if (!overlay.TryGetComponent(out HectonUIScaler _))
                overlayObject.AddComponent<HectonUIScaler>();
        }

        private static void SetTransformCanvasVisible(Transform target, bool visible)
        {
            if (!(target is RectTransform rect))
                return;

            CanvasGroup canvasGroup = rect.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = rect.gameObject.AddComponent<CanvasGroup>();

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private static void SetOverlayCanvasVisible(SuitHUDV4CanvasOverlay overlay, bool visible)
        {
            if (overlay == null)
                return;

            if (!visible && overlay.isActiveAndEnabled)
                overlay.SetRenderPathProjectionSource(false);

            overlay.enabled = visible;

            Canvas overlayCanvas = overlay.TargetCanvas;
            RectTransform rect = overlayCanvas != null
                ? overlayCanvas.transform as RectTransform
                : overlay.transform as RectTransform;
            SetTransformCanvasVisible(rect, visible);
        }

        private static SuitHUDV4CanvasOverlay FindOverlayByName(
            List<SuitHUDV4CanvasOverlay> overlays,
            string expectedName,
            SuitHUDV4CanvasOverlay current,
            Transform preferredRoot)
        {
            if (current != null && current.name == expectedName)
                return current;

            for (int i = 0; i < overlays.Count; i++)
            {
                SuitHUDV4CanvasOverlay candidate = overlays[i];
                if (candidate != null &&
                    candidate.name == expectedName &&
                    candidate.transform.root == preferredRoot)
                    return candidate;
            }

            for (int i = 0; i < overlays.Count; i++)
            {
                SuitHUDV4CanvasOverlay candidate = overlays[i];
                if (candidate != null && candidate.name == expectedName)
                    return candidate;
            }

            return current;
        }

        private Transform ResolveHudCanvasTransform()
        {
            if (canvasOverlay != null)
            {
                _cachedHudCanvasTransform = canvasOverlay.transform;
                return _cachedHudCanvasTransform;
            }

            if (_cachedHudCanvasTransform != null)
                return _cachedHudCanvasTransform;

            SuitHUDV4CanvasOverlay.CopyActiveOverlaysTo(s_overlayResolveBuffer);
            canvasOverlay = FindOverlayByName(s_overlayResolveBuffer, "Suit_HUD_Canvas", canvasOverlay, transform.root);
            s_overlayResolveBuffer.Clear();

            if (canvasOverlay != null)
            {
                _cachedHudCanvasTransform = canvasOverlay.transform;
                _cachedHudRtCompositorTransform = null;
                return _cachedHudCanvasTransform;
            }

            return null;
        }

        private Transform ResolveHudRtCompositorTransform()
        {
            if (_cachedHudRtCompositorTransform != null)
                return _cachedHudRtCompositorTransform;

            Transform canvasTransform = ResolveHudCanvasTransform();
            if (canvasTransform == null)
                return null;

            _cachedHudRtCompositorTransform = canvasTransform.Find("HUD_RT_Compositor");
            return _cachedHudRtCompositorTransform;
        }

        private static VisorHUDController.ProjectionMode ResolveProjectionMode(PresentationMode mode)
        {
            switch (mode)
            {
                case PresentationMode.ModernProjectedSharedRT:
                    return VisorHUDController.ProjectionMode.SharedRenderTexture;

                case PresentationMode.ModernProjectedRuntimeRT:
                    return VisorHUDController.ProjectionMode.RuntimeRenderTexture;

                default:
                    return VisorHUDController.ProjectionMode.Disabled;
            }
        }

        private static string ResolvePresentationModeLabel(PresentationMode mode)
        {
            switch (mode)
            {
                case PresentationMode.LegacyOverlay: return "LegacyOverlay";
                case PresentationMode.ModernOverlay: return "ModernOverlay";
                case PresentationMode.ModernProjectedSharedRT: return "ModernProjectedSharedRT";
                case PresentationMode.ModernProjectedRuntimeRT: return "ModernProjectedRuntimeRT";
                default: return "UnknownPresentationMode";
            }
        }

        private Camera ResolveOverlayHostCamera()
        {
            if (overlayPresentationCamera != null)
                return overlayPresentationCamera;

            if (visorProjectionCamera != null)
                return visorProjectionCamera;

            return GetComponent<Camera>();
        }

        public void SetPresentationMode(PresentationMode mode)
        {
            if (presentationMode == mode)
                return;

            presentationMode = mode;
            MarkDirty();
        }

        public void SetSharedProjectionTexture(RenderTexture texture)
        {
            if (sharedProjectionTexture == texture)
                return;

            sharedProjectionTexture = texture;
            MarkDirty();
        }

        public void SetFallbackProfile(SuitHUDProfile profile)
        {
            if (ReferenceEquals(standardFallbackProfile, profile))
                return;

            standardFallbackProfile = profile;
            MarkDirty();
        }

        private void PrepareModernHud(HectonSuitHUD_v4 hud, Camera targetCamera)
        {
            if (hud == null)
                return;

            hud.SetFallbackProfile(standardFallbackProfile);

            if (targetCamera != null)
                hud.SetHudCamera(targetCamera);
        }

        private void MarkDirty()
        {
            _pendingApply = true;
            if (Application.isPlaying)
                EvaluateTickRegistration();
#if UNITY_EDITOR
            else
                EvaluateEditorTickRegistration();
#endif
        }

#if UNITY_EDITOR
        [ContextMenu("Rebuild HUD Presentation")]
        private void RebuildHudPresentation()
        {
            AutoResolveReferences(true);
            ApplyPresentation(force: true);
            _pendingApply = false;
        }
#endif

        private bool ShouldTickInPlay()
        {
            return _pendingApply || NeedsAutoResolve() || RequiresRuntimePresentationMonitoring();
        }

        private bool ShouldTickInEditMode()
        {
            return isActiveAndEnabled && (_pendingApply || NeedsAutoResolve() || HasEditorPresentationStateChanged());
        }

        private bool HasEditorPresentationStateChanged()
        {
            if (!RequiresRuntimePresentationMonitoring())
                return false;

            bool projectedAvailable = IsProjectedPresentationAvailable();
            bool projectedOutputSurfaceAvailable = HasProjectedOutputSurface();

            if (!_editorPresentationStateCached)
                return true;

            return _editorLastProjectedPresentationAvailable != projectedAvailable
                || _editorLastProjectedOutputSurfaceAvailable != projectedOutputSurfaceAvailable;
        }

        private void CacheEditorPresentationState()
        {
            if (!RequiresRuntimePresentationMonitoring())
            {
                _editorPresentationStateCached = false;
                _editorLastProjectedPresentationAvailable = false;
                _editorLastProjectedOutputSurfaceAvailable = false;
                return;
            }

            _editorLastProjectedPresentationAvailable = IsProjectedPresentationAvailable();
            _editorLastProjectedOutputSurfaceAvailable = HasProjectedOutputSurface();
            _editorPresentationStateCached = true;
        }

        private bool RequiresRuntimePresentationMonitoring()
        {
            return presentationMode == PresentationMode.ModernProjectedSharedRT ||
                   presentationMode == PresentationMode.ModernProjectedRuntimeRT;
        }

        private void EvaluateTickRegistration()
        {
            if (!Application.isPlaying || !isActiveAndEnabled)
            {
                UnregisterTick();
                return;
            }

            if (ShouldTickInPlay())
                RegisterTick();
            else
                UnregisterTick();
        }

#if UNITY_EDITOR
        private void EvaluateEditorTickRegistration()
        {
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
            EditorApplication.update -= EditorTick;
            EditorApplication.update += EditorTick;
        }

        private void UnregisterEditorTick()
        {
            EditorApplication.update -= EditorTick;
        }
#endif

        private void RegisterTick()
        {
            if (_tickRegistered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _tickRegistered = true;
        }

        private void UnregisterTick()
        {
            if (!_tickRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _tickRegistered = false;
        }

        private static SuitHUDScreenCompositor FindCompositor(
            List<SuitHUDScreenCompositor> compositors,
            SuitHUDScreenCompositor current,
            Transform preferredRoot)
        {
            if (current != null && current.isActiveAndEnabled)
                return current;

            for (int i = 0; i < compositors.Count; i++)
            {
                SuitHUDScreenCompositor candidate = compositors[i];
                if (candidate != null &&
                    candidate.transform.root == preferredRoot)
                {
                    return candidate;
                }
            }

            return compositors.Count > 0 ? compositors[0] : null;
        }
    }
}
