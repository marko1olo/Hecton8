using System;
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
    // Editor preview must keep the shared HUD RenderTexture current outside Play Mode.
    [ExecuteAlways]
    [AddComponentMenu("Hecton8/HUD/Suit HUD Presentation Controller")]
    public sealed class SuitHUDPresentationController : MonoBehaviour, ITickable, IUnscaledFastTickable, IUpdatable
    {
        private const float AutoResolveRetryInterval = 1f;
        private const float DegreesToHalfRadians = 0.00872664626f;
        private const float PrimaryHudTanMinRadians = 0.001f;
        private const float PrimaryHudTanMaxRadians = 1.55334306f;
        private static readonly int VrComfortVignette01Id = Shader.PropertyToID("_VRComfortVignette01");
        private static readonly int SomaticComfortVignetteId = Shader.PropertyToID("_VRComfortVignette");
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
        [SerializeField] private Renderer diegeticProjectionRenderer;

        [Header("Overlay Suppression")]
        [SerializeField] private SuitHUDV4CanvasOverlay canvasOverlay;
        [SerializeField] private SuitHUDV4CanvasOverlay projectionSourceOverlay;
        [SerializeField] private SuitHUDScreenCompositor screenCompositor;
        [SerializeField] private bool suppressOverlaysInProjectedMode = true;
        [SerializeField] private bool previewProjectedSourceOnScreen = false;
        [SerializeField] private bool preferCanvasProjectionSource = true;
        [SerializeField] private bool syncProjectionLayoutFromOverlay = false;

        [Header("Diegetic Projection Fit")]
        [SerializeField, Tooltip("Fits the physical HUD projection surface to the active camera frustum so the 16:9 HUD occupies the Game View instead of a small center patch.")]
        private bool fitDiegeticProjectionToCamera = true;
        [SerializeField, Range(0.55f, 0.82f), Tooltip("Viewport fill for the physical HUD projection surface. Capped below full frustum so the diegetic panel stays inside the visor frame.")]
        private float diegeticProjectionViewportFill = 0.82f;

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
        private VisorHUDController _cachedVisorRendererOwner;
        private Renderer _cachedVisorRenderer;
        private bool _cachedVisorRendererResolved;
        private Camera _cachedSelfCamera;
        private bool _cachedSelfCameraResolved;
        private bool _editorPresentationStateCached;
        private bool _editorLastProjectedPresentationAvailable;
        private bool _editorLastProjectedOutputSurfaceAvailable;
        private const string ProjectionSourceCanvasName = "Suit_HUD_ProjectionSource";
        private const string DiegeticProjectionSurfaceName = "Suit_Diegetic_HUD_V4_Projection";
        private const int ProjectionSourceLayer = 17;
#if UNITY_EDITOR
        private const double EditorProjectionPreviewInterval = 1.0 / 15.0;
        private double _nextEditorProjectionPreviewAt;
        private bool _editorPreviewBootstrapQueued;
#endif

        private void OnEnable()
        {
            _pendingApply = true;
            if (!Application.isPlaying)
            {
#if UNITY_EDITOR
                if (!IsEditorPreviewSafe())
                {
                    QueueEditorPreviewBootstrap();
                    return;
                }

                BootstrapEditorPreview();
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
            if (Application.isPlaying)
                return;

            if (!IsEditorPreviewSafe())
            {
                UnregisterEditorTick();
                return;
            }

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

            if (!IsEditorPreviewSafe())
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
            RenderProjectionTextureForEditorPreview(false);
            CacheEditorPresentationState();
            if (!ShouldTickInEditMode())
                UnregisterEditorTick();
        }

        private void BootstrapEditorPreview()
        {
            if (!IsEditorPreviewSafe() || Application.isPlaying || !isActiveAndEnabled)
                return;

            AutoResolveReferences(true);
            ApplyPresentation(force: true);
            _pendingApply = false;
            RenderProjectionTextureForEditorPreview(true);
            CacheEditorPresentationState();
            EvaluateEditorTickRegistration();
        }

        private void QueueEditorPreviewBootstrap()
        {
            if (_editorPreviewBootstrapQueued || !IsEditorPreviewSafe())
                return;

            _editorPreviewBootstrapQueued = true;
            EditorApplication.delayCall -= DelayedEditorPreviewBootstrap;
            EditorApplication.delayCall += DelayedEditorPreviewBootstrap;
        }

        private void DelayedEditorPreviewBootstrap()
        {
            _editorPreviewBootstrapQueued = false;

            if (this == null || !this)
                return;

            if (!IsEditorPreviewSafe())
            {
                UnregisterEditorTick();
                return;
            }

            BootstrapEditorPreview();
        }

        private void RenderProjectionTextureForEditorPreview(bool forceRender)
        {
            bool projectedPreviewAvailable = debugProjectedModeActive || IsProjectedPresentationAvailable();
            if (!IsEditorPreviewSafe() ||
                Application.isPlaying ||
                visorProjectionCamera == null ||
                sharedProjectionTexture == null ||
                !visorProjectionCamera.gameObject.activeInHierarchy ||
                !projectedPreviewAvailable)
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (!forceRender && now < _nextEditorProjectionPreviewAt)
                return;

            _nextEditorProjectionPreviewAt = now + EditorProjectionPreviewInterval;

            if (visorProjectionCamera.targetTexture != sharedProjectionTexture)
                visorProjectionCamera.targetTexture = sharedProjectionTexture;

            // Editor preview only. Runtime projection is rendered by Unity camera scheduling.
            visorProjectionCamera.Render();
        }
#endif

        public void Tick(float deltaTime)
        {
            AutoResolveReferences();
            ApplyPresentation(force: _pendingApply, allowProjectionSourceCreation: false);
            _pendingApply = false;
            EvaluateTickRegistration();
        }

        public void UnscaledFastTick(float unscaledDeltaTime)
        {
            Tick(unscaledDeltaTime);
        }

        private void AutoResolveReferences(bool force = false)
        {
            if (!force && !NeedsAutoResolve())
                return;

            float now = Time.realtimeSinceStartup;
            if (!force && now < _nextAutoResolveAt)
                return;

            _nextAutoResolveAt = now + AutoResolveRetryInterval;
            bool allowHierarchySearch = force || !Application.isPlaying;

            if (visorProjectionCamera == null)
                visorProjectionCamera = ResolveSelfCamera();

            if (overlayPresentationCamera == null && allowHierarchySearch)
            {
                Transform parent = transform.parent;
                if (parent != null)
                {
                    Transform mainCameraTransform = parent.Find("Main Camera");
                    if (mainCameraTransform != null)
                        mainCameraTransform.TryGetComponent(out overlayPresentationCamera);
                }
            }

            if (projectedModernHud == null)
                TryGetComponent(out projectedModernHud);

            if (overlayModernHud == null && overlayPresentationCamera != null)
                overlayPresentationCamera.TryGetComponent(out overlayModernHud);

            if (visorController == null && allowHierarchySearch)
            {
                Transform parent = transform.parent;
                if (parent != null)
                {
                    Transform visor = parent.Find("Suit_Visor");
                    if (visor != null)
                        visor.TryGetComponent(out visorController);
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
                if (allowHierarchySearch)
                {
                    canvasOverlay = FindOverlayByName(s_overlayResolveBuffer, "Suit_HUD_Canvas", canvasOverlay, root);
                    projectionSourceOverlay = FindOverlayByName(s_overlayResolveBuffer, ProjectionSourceCanvasName, projectionSourceOverlay, root);
                }
                else
                {
                    canvasOverlay = FindOverlayByRoot(s_overlayResolveBuffer, canvasOverlay, root);
                }

                s_overlayResolveBuffer.Clear();
            }

            if (screenCompositor == null && IsProjectionSourcePreviewEnabled())
            {
                SuitHUDScreenCompositor.CopyActiveCompositorsTo(s_compositorResolveBuffer);
                screenCompositor = FindCompositor(s_compositorResolveBuffer, screenCompositor, transform.root);
                s_compositorResolveBuffer.Clear();
            }

            if (diegeticProjectionRenderer == null && allowHierarchySearch)
                diegeticProjectionRenderer = FindDiegeticProjectionRenderer(transform.root);

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
                diegeticProjectionRenderer == null ||
                canvasOverlay == null ||
                (IsProjectionSourcePreviewEnabled() && screenCompositor == null))
            {
                return true;
            }

            bool projectedCanvasSourceNeeded =
                preferCanvasProjectionSource &&
                (presentationMode == PresentationMode.ModernProjectedSharedRT ||
                 presentationMode == PresentationMode.ModernProjectedRuntimeRT);

            return projectedCanvasSourceNeeded && projectionSourceOverlay == null && !Application.isPlaying;
        }

        private void ApplyPresentation(bool force, bool allowProjectionSourceCreation = true)
        {
            bool screenOverlayFallbackAllowed = IsScreenOverlayFallbackAllowed();
            bool projectedModeRequested =
                presentationMode == PresentationMode.ModernProjectedSharedRT ||
                presentationMode == PresentationMode.ModernProjectedRuntimeRT ||
                !screenOverlayFallbackAllowed;
            PresentationMode projectedProjectionMode = presentationMode == PresentationMode.ModernProjectedRuntimeRT
                ? PresentationMode.ModernProjectedRuntimeRT
                : PresentationMode.ModernProjectedSharedRT;

            EnsureProjectionSource(projectedModeRequested, allowProjectionSourceCreation);
            bool projectedMode = projectedModeRequested && IsProjectedPresentationAvailable();
            ApplyDiegeticProjectionFrustumFit(projectedMode);
            ApplyHudMotionVectorStabilization(projectedMode);

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

            bool useOverlayModern = !projectedMode && screenOverlayFallbackAllowed;
            bool useProjectedModern = projectedMode;

            if (visorController != null)
            {
                visorController.SetSharedRenderTexture(sharedProjectionTexture);
                visorController.SetProjectionMode(ResolveProjectionMode(projectedMode
                    ? projectedProjectionMode
                    : screenOverlayFallbackAllowed
                        ? PresentationMode.ModernOverlay
                        : projectedProjectionMode));
            }

            if (preferCanvasProjectionSource && projectedMode && projectionSourceOverlay != null)
                useProjectedModern = false;

            SetBehaviourEnabledIfChanged(overlayModernHud, useOverlayModern);
            SetBehaviourEnabledIfChanged(projectedModernHud, useProjectedModern);

            SuppressOverlayPaths(projectedMode, allowProjectionSourceCreation, screenOverlayFallbackAllowed);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            debugAppliedModeLabel = projectedModeRequested && !projectedMode
                ? screenOverlayFallbackAllowed
                    ? presentationMode + " -> FallbackOverlay"
                    : presentationMode + " -> ProjectionUnavailable"
                : presentationMode == PresentationMode.LegacyOverlay
                    ? "ModernOverlay (legacy retired)"
                    : ResolvePresentationModeLabel(presentationMode);
#endif
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

        private void SuppressOverlayPaths(bool projectedMode, bool allowHierarchySearch, bool screenOverlayFallbackAllowed)
        {
            bool suppress = (projectedMode || !screenOverlayFallbackAllowed) && suppressOverlaysInProjectedMode;
            bool showProjectionPreview = projectedMode && IsProjectionSourcePreviewEnabled();

            if (canvasOverlay != null)
            {
                if (suppress)
                    SetOverlayCanvasVisible(canvasOverlay, false, allowHierarchySearch);
                else
                {
                    canvasOverlay.SetRenderPathProjectionSource(!screenOverlayFallbackAllowed);
                    SetOverlayCanvasVisible(canvasOverlay, true, allowHierarchySearch);
                }

                _cachedHudCanvasTransform = canvasOverlay.transform;
            }

            SetBehaviourEnabledIfChanged(screenCompositor, showProjectionPreview);
            if (showProjectionPreview)
            {
                Transform compositorTransform = ResolveHudRtCompositorTransform(allowHierarchySearch);
                SetTransformCanvasVisible(compositorTransform, true, allowHierarchySearch);
            }
            else if (_cachedHudRtCompositorTransform != null)
            {
                SetTransformCanvasVisible(_cachedHudRtCompositorTransform, false, allowHierarchySearch);
            }

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

            if (IsProjectionSourcePreviewEnabled() && screenCompositor == null)
                return false;

            return true;
        }

        private bool HasProjectedOutputSurface()
        {
            if (visorController == null || !visorController.isActiveAndEnabled)
                return false;

            Renderer visorRenderer = ResolveVisorRenderer();
            bool visorSurfaceAvailable = visorRenderer != null &&
                                          visorRenderer.enabled &&
                                          !visorRenderer.forceRenderingOff &&
                                          visorRenderer.gameObject.activeInHierarchy;
            bool diegeticSurfaceAvailable = diegeticProjectionRenderer != null &&
                                             diegeticProjectionRenderer.enabled &&
                                             !diegeticProjectionRenderer.forceRenderingOff &&
                                             diegeticProjectionRenderer.gameObject.activeInHierarchy;
            Camera hudCamera = visorProjectionCamera != null ? visorProjectionCamera : visorController.HudCamera;
            return hudCamera != null &&
                   hudCamera.gameObject.activeInHierarchy &&
                   (visorSurfaceAvailable || diegeticSurfaceAvailable);
        }

        private void ApplyDiegeticProjectionFrustumFit(bool projectedMode)
        {
            if (!projectedMode ||
                !fitDiegeticProjectionToCamera ||
                diegeticProjectionRenderer == null)
            {
                return;
            }

            Camera fitCamera = ResolveOverlayHostCamera();
            if (fitCamera == null)
                return;

            Transform surface = diegeticProjectionRenderer.transform;
            float distance = Mathf.Abs(surface.localPosition.z);
            if (distance <= fitCamera.nearClipPlane + 0.01f)
                return;

            float height = 2f * distance * ExactPrimaryHudTanPositive(fitCamera.fieldOfView * DegreesToHalfRadians);
            float width = height * fitCamera.aspect;
            float comfortVignette01 = Application.isPlaying
                ? Mathf.Clamp01(Mathf.Max(
                    Shader.GetGlobalFloat(VrComfortVignette01Id),
                    Shader.GetGlobalFloat(SomaticComfortVignetteId)))
                : 0f;
            float comfortSafeFill = Mathf.Lerp(0.82f, 0.58f, comfortVignette01);
            float fill = Mathf.Min(Mathf.Clamp(diegeticProjectionViewportFill, 0.55f, 0.82f), comfortSafeFill);
            Vector3 targetScale = new Vector3(width * fill, height * fill, surface.localScale.z);

            if ((surface.localScale - targetScale).sqrMagnitude > 0.000001f)
                surface.localScale = targetScale;
        }

        private void ApplyHudMotionVectorStabilization(bool projectedMode)
        {
            if (!projectedMode || diegeticProjectionRenderer == null)
                return;

            if (diegeticProjectionRenderer.motionVectorGenerationMode != MotionVectorGenerationMode.ForceNoMotion)
                diegeticProjectionRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }

        private void EnsureProjectionSource(bool projectedMode, bool allowProjectionSourceCreation)
        {
            if (!projectedMode)
            {
                if (projectionSourceOverlay != null)
                    SetOverlayCanvasVisible(projectionSourceOverlay, false, allowProjectionSourceCreation);

                return;
            }

            if (canvasOverlay == null || visorProjectionCamera == null)
                return;

            bool createdThisPass = false;
            if (projectionSourceOverlay == null && allowProjectionSourceCreation)
            {
                projectionSourceOverlay = CreateProjectionSourceOverlay();
                createdThisPass = projectionSourceOverlay != null;
            }

            if (projectionSourceOverlay == null)
                return;

            SetOverlayCanvasVisible(projectionSourceOverlay, true, allowProjectionSourceCreation);

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
            Transform parent = ResolveProjectionSourceParent();
            GameObject go = new GameObject(
                ProjectionSourceCanvasName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(GraphicRaycaster),
                typeof(SuitHUDV4CanvasOverlay),
                typeof(HectonUIScaler)); // COLD ALLOC: GameObject[1] — projection-source canvas bootstrap; Tick calls disallow creation — owner: SuitHUDPresentationController

            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            go.layer = ProjectionSourceLayer;

            go.TryGetComponent(out GraphicRaycaster raycaster);
            SetBehaviourEnabledIfChanged(raycaster, false);

            go.TryGetComponent(out SuitHUDV4CanvasOverlay overlay);
            return overlay;
        }

        private Transform ResolveProjectionSourceParent()
        {
            if (visorProjectionCamera != null)
                return visorProjectionCamera.transform;

            if (transform != null)
                return transform;

            return canvasOverlay != null ? canvasOverlay.transform.parent : null;
        }

        private static void NormalizeProjectionSourceOverlay(SuitHUDV4CanvasOverlay overlay)
        {
            if (overlay == null)
                return;

            GameObject overlayObject = overlay.gameObject;
            if (overlayObject.layer != ProjectionSourceLayer)
                overlayObject.layer = ProjectionSourceLayer;

            if (overlay.TryGetComponent(out CanvasScaler canvasScaler))
                SetBehaviourEnabledIfChanged(canvasScaler, false);

            if (overlay.TryGetComponent(out GraphicRaycaster raycaster))
                SetBehaviourEnabledIfChanged(raycaster, false);

            if (!overlay.TryGetComponent(out HectonUIScaler _))
                overlayObject.AddComponent<HectonUIScaler>(); // COLD ALLOC: HectonUIScaler[1] — projection-source matrix scaler bootstrap — owner: SuitHUDPresentationController
        }

        private static void SetTransformCanvasVisible(Transform target, bool visible, bool allowCanvasGroupCreation)
        {
            if (!(target is RectTransform rect))
                return;

            rect.TryGetComponent(out CanvasGroup canvasGroup);
            if (canvasGroup == null)
            {
                if (!allowCanvasGroupCreation)
                    return;

                canvasGroup = rect.gameObject.AddComponent<CanvasGroup>(); // COLD ALLOC: CanvasGroup[1] — compositor visibility latch for projection preview — owner: SuitHUDPresentationController
            }

            SetCanvasGroupVisibleIfChanged(canvasGroup, visible);
        }

        private static void SetOverlayCanvasVisible(SuitHUDV4CanvasOverlay overlay, bool visible, bool allowCanvasGroupCreation)
        {
            if (overlay == null)
                return;

            if (!visible && overlay.isActiveAndEnabled)
                overlay.SetRenderPathProjectionSource(false);

            SetBehaviourEnabledIfChanged(overlay, visible);

            Canvas overlayCanvas = overlay.TargetCanvas;
            RectTransform rect = overlayCanvas != null
                ? overlayCanvas.transform as RectTransform
                : overlay.transform as RectTransform;
            SetTransformCanvasVisible(rect, visible, allowCanvasGroupCreation);
        }

        private static void SetBehaviourEnabledIfChanged(Behaviour behaviour, bool enabled)
        {
            if (behaviour != null && behaviour.enabled != enabled)
                behaviour.enabled = enabled;
        }

        private static void SetCanvasGroupVisibleIfChanged(CanvasGroup canvasGroup, bool visible)
        {
            float targetAlpha = visible ? 1f : 0f;
            if (canvasGroup.alpha == targetAlpha &&
                !canvasGroup.interactable &&
                !canvasGroup.blocksRaycasts)
            {
                return;
            }

            canvasGroup.alpha = targetAlpha;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
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

        private static SuitHUDV4CanvasOverlay FindOverlayByRoot(
            List<SuitHUDV4CanvasOverlay> overlays,
            SuitHUDV4CanvasOverlay current,
            Transform preferredRoot)
        {
            if (current != null && current.transform.root == preferredRoot)
                return current;

            for (int i = 0; i < overlays.Count; i++)
            {
                SuitHUDV4CanvasOverlay candidate = overlays[i];
                if (candidate != null &&
                    candidate.transform.root == preferredRoot)
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

        private Transform ResolveHudRtCompositorTransform(bool allowHierarchySearch)
        {
            if (_cachedHudRtCompositorTransform != null)
                return _cachedHudRtCompositorTransform;

            if (!allowHierarchySearch)
                return null;

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

        private bool IsProjectionSourcePreviewEnabled()
        {
#if UNITY_EDITOR
            return previewProjectedSourceOnScreen;
#else
            return false;
#endif
        }

        private static bool IsScreenOverlayFallbackAllowed()
        {
            return false;
        }

        private static float ExactPrimaryHudTanPositive(float radians)
        {
            float x = Mathf.Clamp(radians, PrimaryHudTanMinRadians, PrimaryHudTanMaxRadians);
            return (float)Math.Tan(x);
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

            return ResolveSelfCamera();
        }

        private Renderer ResolveVisorRenderer()
        {
            if (visorController == null)
            {
                _cachedVisorRendererOwner = null;
                _cachedVisorRenderer = null;
                _cachedVisorRendererResolved = false;
                return null;
            }

            if (_cachedVisorRendererOwner != visorController)
            {
                _cachedVisorRendererOwner = visorController;
                _cachedVisorRenderer = null;
                _cachedVisorRendererResolved = false;
            }

            if (!_cachedVisorRendererResolved)
            {
                visorController.TryGetComponent(out _cachedVisorRenderer);
                _cachedVisorRendererResolved = true;
            }

            return _cachedVisorRenderer;
        }

        private Camera ResolveSelfCamera()
        {
            if (!_cachedSelfCameraResolved)
            {
                TryGetComponent(out _cachedSelfCamera);
                _cachedSelfCameraResolved = true;
            }

            return _cachedSelfCamera;
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
            if (!isActiveAndEnabled)
                return false;

            if (RequiresRuntimePresentationMonitoring() && IsProjectedPresentationAvailable())
                return true;

            return _pendingApply || NeedsAutoResolve() || HasEditorPresentationStateChanged();
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
            EditorApplication.delayCall -= DelayedEditorPreviewBootstrap;
            _editorPreviewBootstrapQueued = false;
        }

        private static bool IsEditorPreviewSafe()
        {
            return !EditorApplication.isCompiling &&
                   !EditorApplication.isUpdating &&
                   !EditorApplication.isPlayingOrWillChangePlaymode;
        }
#endif

        private void RegisterTick()
        {
            if (_tickRegistered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _tickRegistered = GlobalRegistry.TryRegisterUnscaledFastTickable(this, PriorityLayer.UI);
        }

        private void UnregisterTick()
        {
            if (!_tickRegistered)
                return;

            GlobalRegistry.UnregisterUnscaledFastTickable(this, PriorityLayer.UI);
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

        private static Renderer FindDiegeticProjectionRenderer(Transform root)
        {
            if (root == null)
                return null;

            Transform surface = FindChildByName(root, DiegeticProjectionSurfaceName);
            if (surface == null)
                return null;

            surface.TryGetComponent(out Renderer renderer);
            return renderer;
        }

        private static Transform FindChildByName(Transform root, string targetName)
        {
            if (root == null)
                return null;

            if (root.name == targetName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildByName(root.GetChild(i), targetName);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
