using Hecton8.Gameplay;
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
        [SerializeField] private PresentationMode presentationMode = PresentationMode.ModernOverlay;

        [Header("References")]
        [SerializeField] private HectonSuitHUD legacyHud;
        [SerializeField] private HectonSuitHUD_v4 modernHud;
        [SerializeField] private VisorHUDController visorController;
        [SerializeField] private SuitHUDProfile standardFallbackProfile;
        [SerializeField] private RenderTexture sharedProjectionTexture;
        [SerializeField] private Camera overlayPresentationCamera;
        [SerializeField] private Camera visorProjectionCamera;

        [Header("Diagnostics")]
        [SerializeField] private string debugAppliedModeLabel;
        [SerializeField] private bool debugLegacyEnabled;
        [SerializeField] private bool debugModernEnabled;
        [SerializeField] private bool debugProjectedModeActive;

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

            if (modernHud == null)
                modernHud = GetComponent<HectonSuitHUD_v4>();

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

            if (modernHud != null && standardFallbackProfile != null)
                modernHud.SetFallbackProfile(standardFallbackProfile);

            if (modernHud != null)
            {
                bool projectedMode =
                    presentationMode == PresentationMode.ModernProjectedSharedRT ||
                    presentationMode == PresentationMode.ModernProjectedRuntimeRT;

                Camera targetCamera = projectedMode
                    ? visorProjectionCamera
                    : ResolveOverlayHostCamera();

                modernHud.SetHudCamera(targetCamera);
            }

            bool useLegacy = presentationMode == PresentationMode.LegacyOverlay;
            bool useModern = !useLegacy;

            if (legacyHud != null)
                legacyHud.enabled = useLegacy;

            if (modernHud != null)
                modernHud.enabled = useModern;

            if (visorController != null)
            {
                visorController.SetSharedRenderTexture(sharedProjectionTexture);
                visorController.SetProjectionMode(ResolveProjectionMode(presentationMode));
            }

            debugAppliedModeLabel = presentationMode.ToString();
            debugLegacyEnabled = legacyHud != null && legacyHud.enabled;
            debugModernEnabled = modernHud != null && modernHud.enabled;
            debugProjectedModeActive =
                presentationMode == PresentationMode.ModernProjectedSharedRT ||
                presentationMode == PresentationMode.ModernProjectedRuntimeRT;

            _appliedMode = presentationMode;
            _appliedFallbackProfile = standardFallbackProfile;
            _appliedSharedTexture = sharedProjectionTexture;
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
            if (visorProjectionCamera != null)
                return visorProjectionCamera;

            if (overlayPresentationCamera != null)
                return overlayPresentationCamera;

            return GetComponent<Camera>();
        }
    }
}
