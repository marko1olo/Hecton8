// ============================================================================
// HECTON-8 - VisualOmegaSmokeTester.cs
// Cold-path source audit for visual-domain OMEGA hardening.
// ============================================================================

using System.IO;
using System.Text;
using UnityEngine;

namespace Hecton8.Dev
{
    /// <summary>
    /// Batch/edit-mode smoke tester for visual hardening invariants.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/Visual Omega Smoke Tester")]
    public sealed class VisualOmegaSmokeTester : MonoBehaviour
    {
        private const string TesterName = "VisualOmegaSmokeTester";
        private const int ExpectedCheckCount = 36;

        [Header("Execution")]
        [Tooltip("Runs the cold source audit once when the component starts.")]
        [SerializeField] private bool runOnStart = false;
        [Tooltip("Logs the JSON smoke result even when the pass succeeds.")]
        [SerializeField] private bool verboseLogging = true;

#pragma warning disable CS0414
        [Header("Debug")]
        [Tooltip("Last source-audit result.")]
        [SerializeField] private bool _debugLastPass;
        [Tooltip("Number of checks executed by the last source audit.")]
        [SerializeField] private int _debugCheckCount;
        [Tooltip("Number of failed checks in the last source audit.")]
        [SerializeField] private int _debugFailureCount;
#if UNITY_EDITOR
        [Tooltip("Last JSON smoke result emitted by the audit.")]
        [SerializeField] private string _debugLastJson = string.Empty;
#endif
#pragma warning restore CS0414

#if UNITY_EDITOR
        // COLD ALLOC: StringBuilder[1024] - source-audit issue buffer - owner: VisualOmegaSmokeTester
        private readonly StringBuilder _issueBuilder = new StringBuilder(1024);
        // COLD ALLOC: StringBuilder[512] - source-audit JSON report - owner: VisualOmegaSmokeTester
        private readonly StringBuilder _jsonBuilder = new StringBuilder(512);
#endif

        private void Start()
        {
#if UNITY_EDITOR
            if (runOnStart)
                RunSmokePass();
#endif
        }

        /// <summary>
        /// Unity batch entry point.
        /// </summary>
        public static void RunBatchModeSmokeTest()
        {
#if UNITY_EDITOR
            VisualOmegaSmokeTester tester = new GameObject(TesterName).AddComponent<VisualOmegaSmokeTester>();
            bool pass = tester.RunSmokePass();
            if (Application.isBatchMode)
                Application.Quit(pass ? 0 : 1);
#else
            if (Application.isBatchMode)
                Application.Quit(1);
#endif
        }

        /// <summary>
        /// Runs the cold source audit and logs a JSON result.
        /// </summary>
        [ContextMenu("Run Visual Omega Smoke Pass")]
        public bool RunSmokePass()
        {
#if UNITY_EDITOR
            _issueBuilder.Clear();
            _debugCheckCount = 0;
            _debugFailureCount = 0;

            string projectRoot = ResolveProjectRoot();
            string flashlightSource = ReadProjectFile(projectRoot, "Assets/_Project/Scripts/Visor/HectonFlashlightVoxelShadowProvider.cs");
            string atmosphereSource = ReadProjectFile(projectRoot, "Assets/_Project/Scripts/HectonAtmosphereManager.cs");
            string mapMagicSource = ReadProjectFile(projectRoot, "Assets/_Project/Scripts/MapMagicBridge.cs");
            string registrySource = ReadProjectFile(projectRoot, "Assets/_Project/Scripts/Core/GlobalRegistry.cs");
            string registryContractsSource = ReadProjectFile(projectRoot, "Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs");
            string voxelStreamingSource = ReadProjectFile(projectRoot, "Assets/_Project/Scripts/World/HectonVoxelStreamingBridge.cs");
            string volumetricComputeSource = ReadProjectFile(projectRoot, "Assets/_Project/Art/Shaders/Hecton_VolumetricLight.compute");
            string retinaShaderSource = ReadProjectFile(projectRoot, "Assets/_Project/Art/Shaders/Hecton_RetinaDistortion.shader");
            string causticsRuntimeSource = ReadProjectFile(projectRoot, "Assets/_Project/Scripts/Rendering/AbyssalCaustics/AbyssalDeferredCausticsRuntime.cs");
            string causticsDeferredShaderSource = ReadProjectFile(projectRoot, "Assets/_Project/Art/Shaders/Hecton_DeferredCaustics.shader");
            string underwaterVisualsSource = ReadProjectFile(projectRoot, "Assets/_Project/Scripts/HectonUnderwaterVisuals.cs");
            string shadowGuardSource = ReadProjectFile(projectRoot, "Assets/_Project/Scripts/Core/HectonUrpShadowBudgetGuard.cs");
            string coreLitSource = ReadProjectFile(projectRoot, "Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl");
            string suitVisorSource = ReadProjectFile(projectRoot, "Assets/_Project/Art/Shaders/SuitVisor.shader");
            string visorFluidFeatureSource = ReadProjectFile(projectRoot, "Assets/_Project/Scripts/Visor/HectonVisorFluidDistortionFeature.cs");
            string visorFluidShaderSource = ReadProjectFile(projectRoot, "Assets/_Project/Art/Shaders/Hecton_VisorFluidDistortion.shader");
            string scooterShaftSource = ReadProjectFile(projectRoot, "Assets/_Project/Art/Shaders/Hecton_ScooterVolumetricShafts.shader");

            CheckContains(flashlightSource, "RuntimeVoxelShadowProviderEnabled = false", "flashlight-voxel-provider-runtime-disabled");
            CheckContains(flashlightSource, "PublishInactiveGlobals", "flashlight-legacy-provider-fails-closed");
            CheckNotContains(flashlightSource, "new NativeArray", "flashlight-provider-nativearray-evicted");
            CheckNotContains(flashlightSource, "OverlapBoxNonAlloc", "flashlight-provider-physics-scan-evicted");
            CheckNotContains(flashlightSource, "RegisterUpdatable", "flashlight-provider-update-loop-evicted");
            CheckNotContains(atmosphereSource, "_instance", "atmosphere-static-instance-removed");
            CheckNotContains(mapMagicSource, "private static MapMagicBridge _instance", "mapmagic-legacy-static-instance-removed");
            CheckContains(mapMagicSource, "private static MapMagicBridge s_activeRuntimeInstance;", "mapmagic-owner-local-active-instance");
            CheckContains(mapMagicSource, "return s_activeRuntimeInstance;", "mapmagic-instance-owner-local-facade");
            CheckContains(registrySource, "RegisterMapMagicRuntime", "globalregistry-mapmagic-register");
            CheckContains(registrySource, "_mapMagicRuntime = null;", "globalregistry-mapmagic-reset");
            CheckContains(registryContractsSource, "MapMagicRuntime = 102", "globalregistry-mapmagic-slot");
            CheckNotContains(voxelStreamingSource, "$\"VoxelCave_", "voxel-streaming-hotpath-string-purged");
            CheckContains(volumetricComputeSource, "clamp((int)round(_HectonVolumetricShadowParams.x), 1, HECTON_VOLUMETRIC_LIGHT_MAX_STEPS)", "volumetric-shadow-step-continuous-quality-cap");
            CheckNotContains(volumetricComputeSource, "_MATH_LOD" + "_LOW", "volumetric-light-no-binary-math-lod");
            CheckNotContains(retinaShaderSource, "_QUALITY_MX350", "retina-no-binary-mx350-keyword");
            CheckContains(retinaShaderSource, "_HectonRetinaQualityWeight", "retina-continuous-quality-weight");
            CheckNotContains(causticsRuntimeSource, "TrySampleWaveKinematics", "caustics-ocean-kinematics-sample-purged");
            CheckContains(causticsRuntimeSource, "RunPendingCausticsKernel(job);", "caustics-one-dto-job-run-path");
            CheckContains(causticsDeferredShaderSource, "sdfSampleBudget", "caustics-continuous-sdf-sample-budget");
            CheckNotContains(underwaterVisualsSource, "RaycastNonAlloc", "bottom-silt-raycast-purged");
            CheckContains(underwaterVisualsSource, "ResolveFakeBottomSiltDistance", "bottom-silt-alu-distance-fake");
            CheckContains(underwaterVisualsSource, "WorldRuntimeReferenceUtility.TryResolveMapMagicBridge", "bottom-silt-live-mapmagic-resolver");
            CheckContains(shadowGuardSource, "EnforceSceneShadowDictatorshipCold", "shadow-dictatorship-scene-enforced");
            CheckContains(shadowGuardSource, "IsAllowedForwardSpotlightCold", "shadow-dictatorship-forward-spot-only");
            CheckNotContains(shadowGuardSource, "nearestIndexB", "shadow-dictatorship-single-forward-spot");
            CheckContains(coreLitSource, "HectonCoreLitEvaluateAdditionalLightContactShadow", "additional-lights-screen-space-contact-shadow");
            CheckContains(coreLitSource, "for (int stepIndex = 0; stepIndex < 4; stepIndex++)", "contact-shadow-four-steps");
            CheckContains(coreLitSource, "HectonCoreLitEvaluateCausticsSceneDepthFade", "caustics-scene-depth-fade");
            CheckContains(coreLitSource, "HectonCoreLitSanitizePositionOS", "shader-aup-nan-sentinel");
            CheckContains(suitVisorSource, "glareDepthVisibility", "hud-glare-depth-occlusion");
            CheckContains(suitVisorSource, "foveatedQuantized", "visor-foveated-dither");
            CheckContains(visorFluidFeatureSource, "ThermalDistortionCullSpeedMetersPerSecond = 15f", "thermal-distortion-speed-cull");
            CheckContains(visorFluidShaderSource, "_HectonThermalDistortionMotionCull", "thermal-distortion-shader-cull-uniform");
            CheckContains(scooterShaftSource, "for (int stepIndex = 0; stepIndex < 4; stepIndex++)", "scooter-contact-shadow-four-steps");

            if (_debugCheckCount != ExpectedCheckCount)
                AddIssue("visual-omega-check-count-mismatch");

            _debugLastPass = _debugFailureCount == 0;
            _debugLastJson = BuildJsonReport();
            if (verboseLogging || !_debugLastPass || Application.isBatchMode)
                Hecton8.Core.H8Debug.Log(_debugLastJson, this);

            return _debugLastPass;
#else
            _debugLastPass = false;
            _debugCheckCount = 0;
            _debugFailureCount = 1;
            return false;
#endif
        }

#if UNITY_EDITOR
        private void CheckContains(string source, string requiredToken, string checkName)
        {
            _debugCheckCount++;
            if (!string.IsNullOrEmpty(source) && source.Contains(requiredToken))
                return;

            AddIssue(checkName);
        }

        private void CheckNotContains(string source, string forbiddenToken, string checkName)
        {
            _debugCheckCount++;
            if (!string.IsNullOrEmpty(source) && !source.Contains(forbiddenToken))
                return;

            AddIssue(checkName);
        }

        private void AddIssue(string checkName)
        {
            _debugFailureCount++;
            if (_issueBuilder.Length > 0)
                _issueBuilder.Append(';');
            _issueBuilder.Append(checkName);
        }

        private string BuildJsonReport()
        {
            _jsonBuilder.Clear();
            _jsonBuilder.Append('{')
                .Append("\"tester\":\"").Append(TesterName).Append("\",")
                .Append("\"pass\":").Append(_debugFailureCount == 0 ? "true" : "false").Append(',')
                .Append("\"checks\":").Append(_debugCheckCount).Append(',')
                .Append("\"expectedChecks\":").Append(ExpectedCheckCount).Append(',')
                .Append("\"failures\":").Append(_debugFailureCount).Append(',')
                .Append("\"issues\":\"").Append(_issueBuilder.ToString()).Append("\"")
                .Append('}');
            return _jsonBuilder.ToString();
        }

        private static string ResolveProjectRoot()
        {
            DirectoryInfo dataDirectory = Directory.GetParent(Application.dataPath);
            return dataDirectory != null ? dataDirectory.FullName : Application.dataPath;
        }

        private static string ReadProjectFile(string projectRoot, string relativePath)
        {
            string fullPath = Path.Combine(projectRoot, relativePath);
            return File.Exists(fullPath) ? File.ReadAllText(fullPath) : string.Empty;
        }
#endif
    }
}
