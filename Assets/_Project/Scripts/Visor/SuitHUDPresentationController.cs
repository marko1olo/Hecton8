using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.UI;
using UnityEngine;
using UnityEngine.UI;

namespace NASAPunk.Visor
{
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [AddComponentMenu("Hecton8/HUD/Suit HUD Presentation Controller")]
    public sealed class SuitHUDPresentationController : MonoBehaviour, ITickable
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
        private const string ProjectionSourceCanvasName = "Suit_HUD_ProjectionSource";
        private const int ProjectionSourceLayer = 17;

        private void OnEnable()
        {
            AutoResolveReferences(true);
            _pendingApply = true;
            ApplyPresentation(force: true);
            _pendingApply = false;
            EvaluateTickRegistration();
        }

        private void OnDisable()
        {
            UnregisterTick();
        }

        private void OnValidate()
        {
            AutoResolveReferences(true);
            _pendingApply = true;
        }

        private void Update()
        {
            if (Application.isPlaying)
            {
                EvaluateTickRegistration();
                return;
            }

            AutoResolveReferences();
            ApplyPresentation(force: _pendingApply);
            _pendingApply = false;
        }

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
            bool projectedMode =
                presentationMode == PresentationMode.ModernProjectedSharedRT ||
                presentationMode == PresentationMode.ModernProjectedRuntimeRT;

            if (!force &&
                _appliedMode == presentationMode &&
                ReferenceEquals(_appliedFallbackProfile, standardFallbackProfile) &&
                ReferenceEquals(_appliedSharedTexture, sharedProjectionTexture) &&
                (!projectedMode || projectionSourceOverlay != null))
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
                visorController.SetProjectionMode(ResolveProjectionMode(presentationMode));
            }

            EnsureProjectionSource(projectedMode);
            if (preferCanvasProjectionSource && projectedMode && projectionSourceOverlay != null)
                useProjectedModern = false;

            if (overlayModernHud != null)
                overlayModernHud.enabled = useOverlayModern;

            if (projectedModernHud != null)
                projectedModernHud.enabled = useProjectedModern;

            SuppressOverlayPaths(projectedMode);

            debugAppliedModeLabel = presentationMode == PresentationMode.LegacyOverlay
                ? "ModernOverlay (legacy retired)"
                : presentationMode.ToString();
            debugModernEnabled =
                (overlayModernHud != null && overlayModernHud.enabled) ||
                (projectedModernHud != null && projectedModernHud.enabled);
            debugProjectedModeActive = projectedMode;

            _appliedMode = presentationMode;
            _appliedFallbackProfile = standardFallbackProfile;
            _appliedSharedTexture = sharedProjectionTexture;
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
            if (compositorTransform != null && compositorTransform.gameObject.activeSelf != showProjectionPreview)
                compositorTransform.gameObject.SetActive(showProjectionPreview);

            debugOverlaysSuppressed = suppress && !showProjectionPreview;
        }

        private void EnsureProjectionSource(bool projectedMode)
        {
            if (!projectedMode)
            {
                if (projectionSourceOverlay != null)
                    projectionSourceOverlay.gameObject.SetActive(false);

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

            if (!projectionSourceOverlay.gameObject.activeSelf)
                projectionSourceOverlay.gameObject.SetActive(true);

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
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(SuitHUDV4CanvasOverlay));

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

            if (standardFallbackProfile != null)
                hud.SetFallbackProfile(standardFallbackProfile);

            if (targetCamera != null)
                hud.SetHudCamera(targetCamera);
        }

        private void MarkDirty()
        {
            _pendingApply = true;
            EvaluateTickRegistration();
        }

        private bool ShouldTickInPlay()
        {
            return _pendingApply || NeedsAutoResolve();
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

        private void RegisterTick()
        {
            if (_tickRegistered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            tickManager.Register(this);
            _tickRegistered = true;
        }

        private void UnregisterTick()
        {
            if (!_tickRegistered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
                tickManager.Unregister(this);

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
