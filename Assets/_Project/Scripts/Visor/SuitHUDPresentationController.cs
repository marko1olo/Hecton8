using Hecton8.Gameplay;
using Hecton8.UI;
using UnityEngine;

namespace NASAPunk.Visor
{
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [AddComponentMenu("Hecton8/HUD/Suit HUD Presentation Controller")]
    public sealed class SuitHUDPresentationController : MonoBehaviour
    {
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
        [SerializeField] private HectonSuitHUD legacyHud;
        [SerializeField] private HectonSuitHUD_v4 overlayModernHud;
        [SerializeField] private HectonSuitHUD_v4 projectedModernHud;
        [SerializeField] private VisorHUDController visorController;
        [SerializeField] private SuitHUDProfile standardFallbackProfile;
        [SerializeField] private RenderTexture sharedProjectionTexture;
        [SerializeField] private Camera overlayPresentationCamera;
        [SerializeField] private Camera visorProjectionCamera;

        [Header("Overlay Suppression")]
        [SerializeField] private SuitHUDV4CanvasOverlay canvasOverlay;
        [SerializeField] private SuitHUDScreenCompositor screenCompositor;
        [SerializeField] private bool suppressOverlaysInProjectedMode = true;
        [SerializeField] private bool previewProjectedSourceOnScreen = true;

        [Header("Safety")]
        [SerializeField] private bool suppressAllLegacyHudInScene = true;

        [Header("Diagnostics")]
        [SerializeField] private string debugAppliedModeLabel;
        [SerializeField] private bool debugLegacyEnabled;
        [SerializeField] private bool debugModernEnabled;
        [SerializeField] private bool debugProjectedModeActive;
        [SerializeField] private bool debugOverlaysSuppressed;

        private PresentationMode _appliedMode = (PresentationMode)(-1);
        private SuitHUDProfile _appliedFallbackProfile;
        private RenderTexture _appliedSharedTexture;

        private void OnEnable()
        {
            AutoResolveReferences();
            ApplyPresentation(force: true);
        }

        private void OnValidate()
        {
            AutoResolveReferences();
            ApplyPresentation(force: true);
        }

        private void LateUpdate()
        {
            AutoResolveReferences();
            ApplyPresentation(force: false);
        }

        private void AutoResolveReferences()
        {
            if (legacyHud == null)
                legacyHud = GetComponent<HectonSuitHUD>();

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

            if (canvasOverlay == null)
                canvasOverlay = FindFirstObjectByType<SuitHUDV4CanvasOverlay>(FindObjectsInactive.Include);

            if (screenCompositor == null)
                screenCompositor = FindFirstObjectByType<SuitHUDScreenCompositor>(FindObjectsInactive.Include);
        }

        private void ApplyPresentation(bool force)
        {
            if (!force &&
                _appliedMode == presentationMode &&
                ReferenceEquals(_appliedFallbackProfile, standardFallbackProfile) &&
                ReferenceEquals(_appliedSharedTexture, sharedProjectionTexture))
            {
                return;
            }

            bool projectedMode =
                presentationMode == PresentationMode.ModernProjectedSharedRT ||
                presentationMode == PresentationMode.ModernProjectedRuntimeRT;

            PrepareModernHud(overlayModernHud, ResolveOverlayHostCamera());
            PrepareModernHud(projectedModernHud, visorProjectionCamera);

            bool useLegacy = presentationMode == PresentationMode.LegacyOverlay;
            bool useOverlayModern = !useLegacy && !projectedMode;
            bool useProjectedModern = !useLegacy && projectedMode;

            ApplyLegacyHudState(useLegacy);

            if (overlayModernHud != null)
                overlayModernHud.enabled = useOverlayModern;

            if (projectedModernHud != null)
                projectedModernHud.enabled = useProjectedModern;

            if (visorController != null)
            {
                visorController.SetSharedRenderTexture(sharedProjectionTexture);
                visorController.SetProjectionMode(ResolveProjectionMode(presentationMode));
            }

            SuppressOverlayPaths(projectedMode);

            debugAppliedModeLabel = presentationMode.ToString();
            debugLegacyEnabled = legacyHud != null && legacyHud.enabled;
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
            bool showProjectionPreview = false;

            if (canvasOverlay != null)
                canvasOverlay.SetRenderPathProjectionSource(false);

            if (screenCompositor != null)
                screenCompositor.enabled = showProjectionPreview;

            Transform canvasTransform = ResolveHudCanvasTransform();
            if (canvasTransform != null)
                SetChildActive(canvasTransform, "HUD_RT_Compositor", showProjectionPreview);

            debugOverlaysSuppressed = false;
        }

        private Transform ResolveHudCanvasTransform()
        {
            if (canvasOverlay != null)
                return canvasOverlay.transform;

            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas candidate = canvases[i];
                if (candidate != null && candidate.name == "Suit_HUD_Canvas")
                    return candidate.transform;
            }

            return null;
        }

        private static void SetChildActive(Transform parent, string childName, bool active)
        {
            if (parent == null)
                return;

            Transform child = parent.Find(childName);
            if (child == null || child.gameObject.activeSelf == active)
                return;

            child.gameObject.SetActive(active);
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

        private void PrepareModernHud(HectonSuitHUD_v4 hud, Camera targetCamera)
        {
            if (hud == null)
                return;

            if (standardFallbackProfile != null)
                hud.SetFallbackProfile(standardFallbackProfile);

            if (targetCamera != null)
                hud.SetHudCamera(targetCamera);
        }

        private void ApplyLegacyHudState(bool useLegacy)
        {
            if (suppressAllLegacyHudInScene)
            {
                HectonSuitHUD[] allLegacyHud = FindObjectsByType<HectonSuitHUD>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

                for (int i = 0; i < allLegacyHud.Length; i++)
                {
                    HectonSuitHUD candidate = allLegacyHud[i];
                    if (candidate == null)
                        continue;

                    candidate.enabled = useLegacy && candidate == legacyHud;
                }

                return;
            }

            if (legacyHud != null)
                legacyHud.enabled = useLegacy;
        }
    }
}
