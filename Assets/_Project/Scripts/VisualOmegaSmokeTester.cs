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
        private const int ExpectedCheckCount = 14;

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
        [Tooltip("Last JSON smoke result emitted by the audit.")]
        [SerializeField] private string _debugLastJson = string.Empty;
#pragma warning restore CS0414

        // COLD ALLOC: StringBuilder[1024] - source-audit issue buffer - owner: VisualOmegaSmokeTester
        private readonly StringBuilder _issueBuilder = new StringBuilder(1024);
        // COLD ALLOC: StringBuilder[512] - source-audit JSON report - owner: VisualOmegaSmokeTester
        private readonly StringBuilder _jsonBuilder = new StringBuilder(512);

        private void Start()
        {
            if (runOnStart)
                RunSmokePass();
        }

        /// <summary>
        /// Unity batch entry point.
        /// </summary>
        public static void RunBatchModeSmokeTest()
        {
            VisualOmegaSmokeTester tester = new GameObject(TesterName).AddComponent<VisualOmegaSmokeTester>();
            bool pass = tester.RunSmokePass();
            if (Application.isBatchMode)
                Application.Quit(pass ? 0 : 1);
        }

        /// <summary>
        /// Runs the cold source audit and logs a JSON result.
        /// </summary>
        [ContextMenu("Run Visual Omega Smoke Pass")]
        public bool RunSmokePass()
        {
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

            CheckContains(flashlightSource, "NativeMemorySentinel.RegisterNativeArray(", "flashlight-nativearray-registered");
            CheckContains(flashlightSource, "nameof(_occupancyVolume)", "flashlight-occupancy-sentinel-owner");
            CheckContains(flashlightSource, "nameof(_sdfVolume)", "flashlight-sdf-sentinel-owner");
            CheckContains(flashlightSource, "PublishPerformanceWarningRateLimited", "flashlight-telemetry-warning-hook");
            CheckContains(flashlightSource, "ResolveNoirSignalInstability", "flashlight-noir-instability-polish");
            CheckNotContains(atmosphereSource, "_instance", "atmosphere-static-instance-removed");
            CheckNotContains(mapMagicSource, "_instance", "mapmagic-static-instance-removed");
            CheckContains(mapMagicSource, "return GlobalRegistry.MapMagic;", "mapmagic-instance-globalregistry-facade");
            CheckContains(registrySource, "RegisterMapMagicRuntime", "globalregistry-mapmagic-register");
            CheckContains(registrySource, "_mapMagicRuntime = null;", "globalregistry-mapmagic-reset");
            CheckContains(registryContractsSource, "MapMagicRuntime = 102", "globalregistry-mapmagic-slot");
            CheckNotContains(voxelStreamingSource, "$\"VoxelCave_", "voxel-streaming-hotpath-string-purged");
            CheckContains(volumetricComputeSource, "clamp((int)round(_HectonVolumetricShadowParams.x), 1, 7)", "volumetric-shadow-step-cap-mx350");
            CheckContains(retinaShaderSource, "_QUALITY_MX350", "retina-mx350-mode-toggle");

            _debugLastPass = _debugFailureCount == 0;
            _debugLastJson = BuildJsonReport();
            if (verboseLogging || !_debugLastPass || Application.isBatchMode)
                Debug.Log(_debugLastJson, this);

            return _debugLastPass;
        }

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
    }
}
