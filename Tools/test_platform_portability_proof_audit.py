#!/usr/bin/env python3
"""Tests for PlatformPortabilityProofAudit."""

from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

import PlatformPortabilityProofAudit as audit


def write_json(path: Path, payload: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload), encoding="utf-8")


OPENXR_SETTINGS_GUID = "3f1a06542f0878947a6b0e0bbec9c860"
OPENXR_LOADER_GUID = "ba8874bb90a5e8044bf5c14edba5990f"


def write_xr_management_assets(root: Path, serialize_loader_reference: bool, quest_feature_enabled: bool) -> None:
    project = root / "ProjectSettings"
    project.mkdir(parents=True, exist_ok=True)
    (project / "ProjectSettings.asset").write_text("m_BuildTargetVRSettings: []\n", encoding="utf-8")
    (project / "EditorBuildSettings.asset").write_text(
        "m_configObjects:\n"
        f"  com.unity.xr.openxr.settings4: {{fileID: 11400000, guid: {OPENXR_SETTINGS_GUID}, type: 2}}\n",
        encoding="utf-8",
    )
    loader = root / "Assets" / "XR" / "Loaders" / "OpenXRLoader.asset"
    loader.parent.mkdir(parents=True, exist_ok=True)
    loader.write_text(
        "m_Name: OpenXRLoader\n"
        "m_EditorClassIdentifier: Unity.XR.OpenXR::UnityEngine.XR.OpenXR.OpenXRLoader\n",
        encoding="utf-8",
    )
    (loader.parent / "OpenXRLoader.asset.meta").write_text(f"guid: {OPENXR_LOADER_GUID}\n", encoding="utf-8")
    settings = root / "Assets" / "XR" / "Settings" / "OpenXR Package Settings.asset"
    settings.parent.mkdir(parents=True, exist_ok=True)
    settings.write_text(
        "m_Name: Android\n"
        "m_EditorClassIdentifier: Unity.XR.OpenXR::UnityEngine.XR.OpenXR.OpenXRSettings\n"
        "m_renderMode: 1\n"
        "--- !u!114 &2\n"
        "m_Name: MetaQuestFeature Android\n"
        "m_EditorClassIdentifier: Unity.XR.OpenXR.Features.MetaQuestSupport::UnityEngine.XR.OpenXR.Features.MetaQuestSupport.MetaQuestFeature\n"
        f"m_enabled: {1 if quest_feature_enabled else 0}\n",
        encoding="utf-8",
    )
    (settings.parent / "OpenXR Package Settings.asset.meta").write_text(
        f"guid: {OPENXR_SETTINGS_GUID}\n",
        encoding="utf-8",
    )
    if serialize_loader_reference:
        manager = settings.parent / "XRManagerSettings.asset"
        manager.write_text(
            "m_EditorClassIdentifier: Unity.XR.Management::UnityEngine.XR.Management.XRManagerSettings\n"
            "m_Loaders:\n"
            f"- {{fileID: 11400000, guid: {OPENXR_LOADER_GUID}, type: 2}}\n",
            encoding="utf-8",
        )


class PlatformPortabilityProofAuditTests(unittest.TestCase):
    def test_detects_quest_scaffold_but_missing_runtime_proof(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_platform_audit_") as tmp:
            root = Path(tmp)
            deps = {
                "com.unity.addressables": "2.7.6",
                "com.unity.xr.management": "4.6.0",
                "com.unity.xr.openxr": "1.17.0",
                "com.unity.xr.meta-openxr": "2.5.0",
            }
            lock_deps = {key: {"version": value} for key, value in deps.items()}
            write_json(root / "Packages" / "manifest.json", {"dependencies": deps})
            write_json(root / "Packages" / "packages-lock.json", {"dependencies": lock_deps})
            settings = """
AndroidEnableSustainedPerformanceMode: 0
AndroidTargetSdkVersion: 35
AndroidMinSdkVersion: 25
AndroidTargetArchitectures: 2
m_BuildTargetVRSettings: []
m_BuildTargetGraphicsAPIs:
- m_BuildTarget: AndroidPlayer
  m_APIs: 15000000
  m_Automatic: 0
applicationIdentifier:
  Android: com.test.hecton8
scriptingBackend:
  Android: 1
il2cppCompilerConfiguration: {}
"""
            (root / "ProjectSettings").mkdir(parents=True)
            (root / "ProjectSettings" / "ProjectSettings.asset").write_text(settings, encoding="utf-8")
            (root / "ProjectSettings" / "XRSettings.asset").write_text(
                '{"m_SettingKeys":["VR Device Disabled"],"m_SettingValues":["False"]}',
                encoding="utf-8",
            )
            validator = root / "Assets" / "_Project" / "Scripts" / "Editor" / "Build" / "XrPlatformReadinessValidator.cs"
            validator.parent.mkdir(parents=True)
            validator.write_text(
                "private const string OpenXrLoaderTypeName = \"UnityEngine.XR.OpenXR.OpenXRLoader\";\n"
                "public static void WireAndroidOpenXrProviderRouteForCi() { "
                "_ = XRPackageMetadataStore.AssignLoader(null, OpenXrLoaderTypeName, BuildTargetGroup.Android); "
                "_ = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android); "
                "_ = \"CreateDefaultManagerSettingsForBuildTarget\"; "
                "_ = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android); "
                "_ = OpenXRSettings.RenderMode.SinglePassInstanced; }\n"
                "private static void ValidateOpenXrProviderRoute() { _ = \"HasOpenXrProviderRoute\"; _ = \"HasOpenXrLoader\"; _ = \"activeLoaders\"; _ = \"m_BuildTargetVRSettings: []\"; }\n",
                encoding="utf-8",
            )
            repairer = root / "Assets" / "_Project" / "Scripts" / "Editor" / "Build" / "PlatformPortabilityRouteRepairer.cs"
            repairer.write_text(
                "public static void WireAndroidQuestXrRoutesForCi() { "
                "QuestVulkanRenderPipelineConfigurator.ConfigureQuestAssetsForCi(); "
                "QuestVulkanRenderPipelineConfigurator.WireQuestAndroidQualityRouteForCi(); "
                "XrPlatformReadinessValidator.WireAndroidOpenXrProviderRouteForCi(); "
                "XrPlatformReadinessValidator.ValidateAndroidXrReadinessForCi(); "
                "AssetDatabase.SaveAssets(); }\n",
                encoding="utf-8",
            )
            compiler = root / "Assets" / "_Project" / "Scripts" / "Editor" / "DataMonolith" / "H8DataMonolithCompiler.cs"
            compiler.parent.mkdir(parents=True)
            compiler.write_text(
                "internal const string SourceFolder = \"Assets/_SourceData/DataMonolith\";\n"
                "internal const string BalanceSourceFolder = \"Data/Balance\";\n"
                "internal const string OutputAssetPath = \"Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin\";\n"
                "private const string TempOutputSuffix = \".tmp\";\n"
                "public static void BakeFromCommandLine() { bool valid = BakeAll(logSummary: true) && TryValidateOutputBlob(out string validationError); if (Application.isBatchMode) EditorApplication.Exit(valid ? 0 : 1); }\n"
                "internal static bool BakeAll(bool logSummary) { ValidateProductionSectionCoverage(null); return TryWriteValidatedBlob(null, out _); }\n"
                "internal static bool TryValidateOutputBlob(out string error) => TryValidateBlobFile(OutputAssetPath, out error);\n"
                "private static bool TryWriteValidatedBlob(byte[] blob, out string error) { if (!TryValidateBlobFile(tempPath, out error)) return false; File.Replace(tempPath, OutputAssetPath, backupPath, true); File.Move(tempPath, OutputAssetPath); error = \"Atomic output write failed\"; return false; }\n"
                "private static bool TryValidateBlobFile(string path, out string error) { error = \"XXHash3 checksum mismatch\"; return false; }\n"
                "private static void EnsureLittleEndianEditorHost() { if (!BitConverter.IsLittleEndian) throw new PlatformNotSupportedException(\"Big-endian editor hosts\"); }\n"
                "private static void ValidateProductionSectionCoverage(object dataSet) { _ = BuildProductionCoverageError(dataSet); throw new InvalidOperationException(\"Production static-data coverage gate failed\"); }\n"
                "private static string BuildProductionCoverageError(object dataSet) => string.Empty;\n"
                "internal sealed class H8DataMonolithBuildPreprocessor : IPreprocessBuildWithReport { public void OnPreprocessBuild(BuildReport report) { if (!BakeAll(logSummary: false)) throw new BuildFailedException(\"fail\"); if (!TryValidateOutputBlob(out string error)) throw new BuildFailedException(error); } }\n",
                encoding="utf-8",
            )
            validator_tool = root / "Tools" / "h8bin_validator.py"
            validator_tool.parent.mkdir(parents=True)
            validator_tool.write_text(
                "DEFAULT_STATIC_DATA_RELATIVE = 'Hecton8/DataMonolith/static_data.h8bin'\n"
                "AUP_BOUND_METERS = 100000.0\n"
                "def validate_h8bin_file(path, state, hashes):\n"
                "    checksum = 0\n"
                "    return 'STATIC_DATA_MISSING'\n",
                encoding="utf-8",
            )
            content_validator = root / "Assets" / "_Project" / "Scripts" / "Core" / "Content" / "Editor" / "ContentAuthorityBuildValidators.cs"
            content_validator.parent.mkdir(parents=True)
            content_validator.write_text(
                "internal static class ContentAuthorityBuildValidators { "
                "private const string CoreGroupName = \"Core\"; "
                "private const string HighResGroupName = \"High_Res\"; "
                "private const string OverkillGroupName = \"Overkill\"; "
                "public static void RunAllBuildValidators() { ValidateAddressableGroups(); ValidateComputeShaderThreadGroups(); } "
                "private static void ValidateAddressableGroups() { _ = \"Addressables tier group missing: Core\"; _ = \"Addressables tier group missing: High_Res\"; _ = \"Addressables tier group missing: Overkill\"; throw new BuildFailedException(\"fail\"); } "
                "private static void ValidateComputeShaderThreadGroups() {} } "
                "public sealed class ContentAuthorityBuildPreprocessor : IPreprocessBuildWithReport { public void OnPreprocessBuild(BuildReport report) { ContentAuthorityBuildValidators.RunAllBuildValidators(); throw new BuildFailedException(\"fail\"); } }\n",
                encoding="utf-8",
            )
            content_map = root / "Assets" / "_Project" / "Scripts" / "Core" / "Content" / "ContentAssetHashMap.cs"
            content_map.parent.mkdir(parents=True, exist_ok=True)
            content_map.write_text(
                "public sealed class ContentAssetHashMap { "
                "// Addressables address or GUID. Runtime callers must resolve by Hash first. "
                "}\n",
                encoding="utf-8",
            )
            bootstrapper = root / "Assets" / "_Project" / "Scripts" / "Bootstrap" / "GameBootstrapper.cs"
            bootstrapper.parent.mkdir(parents=True, exist_ok=True)
            bootstrapper.write_text(
                "private static bool HasEditorAddressablesRuntimeSettingsFile() => true;\n"
                "private static void PublishAddressableDependencyGroupLoaded() {}\n"
                "private static bool TryReleaseBootstrapDependencyHandle(AsyncOperationHandle handle) => true;\n"
                "private void Prewarm() { _ = Addressables.DownloadDependenciesAsync(\"Core\", false); }\n",
                encoding="utf-8",
            )
            lifecycle = root / "Assets" / "_Project" / "Scripts" / "Optimization" / "AssetLifecycleGovernor.cs"
            lifecycle.parent.mkdir(parents=True, exist_ok=True)
            lifecycle.write_text(
                "internal void MarkAddressableLoaded() {}\n"
                "internal bool TryAcquireAddressableGameObject() { var h = Addressables.LoadAssetAsync<GameObject>(address); return RegisterAddressableHandleSlot(); }\n"
                "private bool RegisterAddressableHandleSlot() => true;\n"
                "public void SetHeapSanitizerBlindFrameWindow(bool active, float durationSeconds) {}\n"
                "private bool TryExecuteOrDeferBlindFrameRelease(AsyncOperationHandle handle) { Addressables.Release(handle); return EnqueueDetachedAddressableRelease(handle); }\n"
                "private bool EnqueueDetachedAddressableRelease(AsyncOperationHandle handle) => true;\n"
                "private const string Dump = \"Dump_SHINOBU_101_Addressables.bin\";\n",
                encoding="utf-8",
            )
            texture_dictator = root / "Assets" / "_Project" / "Scripts" / "Editor" / "HectonTextureImportDictator.cs"
            texture_dictator.parent.mkdir(parents=True, exist_ok=True)
            texture_dictator.write_text(
                "private const string Menu = \"Sync Texture Addressables Tier Labels\";\n"
                "private void Sync() { _ = AddressableAssetSettingsDefaultObject.GetSettings(true); ResolveTieredTextureGroup(); }\n",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)

        self.assertTrue(payload["readiness"]["androidQuestScaffold"])
        self.assertFalse(payload["readiness"]["androidSustainedPerformanceEnabled"])
        self.assertTrue(payload["readiness"]["androidVulkanOnlySerialized"])
        self.assertFalse(payload["readiness"]["xrProviderSerializedProof"])
        self.assertTrue(payload["readiness"]["androidQuestXrRouteRepairerPresent"])
        self.assertTrue(payload["readiness"]["xrProviderRouteFixerPresent"])
        self.assertTrue(payload["readiness"]["xrProviderRouteValidatorPresent"])
        self.assertTrue(payload["readiness"]["addressablesPackagePresent"])
        self.assertFalse(payload["readiness"]["addressablesContentPresent"])
        self.assertTrue(payload["readiness"]["addressablesContentRoutePresent"])
        self.assertTrue(payload["readiness"]["addressablesRuntimeLifecycleRoutePresent"])
        self.assertFalse(payload["readiness"]["dataMonolithPresent"])
        self.assertTrue(payload["readiness"]["dataMonolithBakeRoutePresent"])
        self.assertTrue(payload["readiness"]["dataMonolithValidationRoutePresent"])
        self.assertFalse(payload["readiness"]["buildArtifactPresent"])

    def test_openxr_package_settings_without_loader_route_are_not_provider_proof(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_platform_audit_xr_assets_only_") as tmp:
            root = Path(tmp)
            write_xr_management_assets(root, serialize_loader_reference=False, quest_feature_enabled=False)

            payload = audit.build_payload(root)

        self.assertTrue(payload["projectSettings"]["xrManagementOpenXrSettingsAssetPresent"])
        self.assertTrue(payload["projectSettings"]["xrManagementOpenXrSettingsRegistered"])
        self.assertTrue(payload["projectSettings"]["xrManagementOpenXrLoaderAssetPresent"])
        self.assertEqual(payload["projectSettings"]["xrManagementOpenXrLoaderGuidReferenceCount"], 0)
        self.assertTrue(payload["projectSettings"]["xrManagementQuestFeaturePresent"])
        self.assertFalse(payload["projectSettings"]["xrManagementQuestFeatureEnabled"])
        self.assertFalse(payload["projectSettings"]["xrManagementProviderSerializedProof"])
        self.assertFalse(payload["readiness"]["xrProviderSerializedProof"])

    def test_serialized_xr_management_loader_reference_is_provider_proof(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_platform_audit_xr_loader_route_") as tmp:
            root = Path(tmp)
            write_xr_management_assets(root, serialize_loader_reference=True, quest_feature_enabled=True)

            payload = audit.build_payload(root)

        self.assertTrue(payload["projectSettings"]["xrManagementOpenXrSettingsAssetPresent"])
        self.assertTrue(payload["projectSettings"]["xrManagementOpenXrSettingsRegistered"])
        self.assertTrue(payload["projectSettings"]["xrManagementOpenXrLoaderAssetPresent"])
        self.assertEqual(payload["projectSettings"]["xrManagementOpenXrLoaderGuidReferenceCount"], 1)
        self.assertTrue(payload["projectSettings"]["xrManagementQuestFeatureEnabled"])
        self.assertTrue(payload["projectSettings"]["xrManagementProviderSerializedProof"])
        self.assertTrue(payload["readiness"]["xrProviderSerializedProof"])

    def test_detects_quest_urp_wiring_shader_warmup_and_compute_risk(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_platform_audit_graphics_") as tmp:
            root = Path(tmp)
            project = root / "ProjectSettings"
            project.mkdir(parents=True)
            (project / "ProjectSettings.asset").write_text(
                """
AndroidEnableSustainedPerformanceMode: 1
m_BuildTargetGraphicsAPIs:
- m_BuildTarget: AndroidPlayer
  m_APIs: 15000000
  m_Automatic: 0
""",
                encoding="utf-8",
            )
            quest_asset = root / "Assets" / "_Project" / "Data" / "URP_Quest_VR.asset"
            quest_asset.parent.mkdir(parents=True)
            quest_asset.write_text("m_Name: URP_Quest_VR\n", encoding="utf-8")
            (quest_asset.parent / "URP_Quest_VR.asset.meta").write_text(
                "guid: abcdef0123456789abcdef0123456789\n",
                encoding="utf-8",
            )
            (project / "QualitySettings.asset").write_text(
                """
m_QualitySettings:
  - serializedVersion: 5
    name: Abyss (Low)
    customRenderPipeline: {fileID: 11400000, guid: abcdef0123456789abcdef0123456789, type: 2}
m_TextureMipmapLimitGroupNames: []
m_PerPlatformDefaultQuality:
  Android: 0
""",
                encoding="utf-8",
            )
            (project / "GraphicsSettings.asset").write_text(
                """
m_PreloadedShaders: []
m_CustomRenderPipeline: {fileID: 11400000, guid: abcdef0123456789abcdef0123456789, type: 2}
""",
                encoding="utf-8",
            )
            shader_dir = root / "Assets" / "_Project" / "Art" / "Shaders"
            shader_dir.mkdir(parents=True)
            bootstrap = root / "Assets" / "_Project" / "Scripts" / "Bootstrap" / "GameBootstrapper.cs"
            bootstrap.parent.mkdir(parents=True)
            bootstrap.write_text(
                "private ShaderVariantCollection[] shaderVariantCollections;\n"
                "private void Warm(ShaderVariantCollection collection, Shader shader, ShaderWarmupSetup setup, GraphicsStateCollection graphics) { "
                "if (!collection.isWarmedUp) ShaderWarmup.WarmupShaderFromCollection(collection, shader, setup); "
                "graphics.WarmUpProgressively(1, default); }\n",
                encoding="utf-8",
            )
            configurator = root / "Assets" / "_Project" / "Scripts" / "Editor" / "Build" / "QuestVulkanRenderPipelineConfigurator.cs"
            configurator.parent.mkdir(parents=True)
            configurator.write_text(
                "private const string QuestUrpAssetPath = \"Assets/_Project/Data/URP_Quest_VR.asset\";\n"
                "private const string QuestQualityName = \"Quest (VR)\";\n"
                "private static void AppendQualityRouteAudit() { _ = \"m_PerPlatformDefaultQuality\"; _ = \"customRenderPipeline\"; }\n"
                "public static void WireQuestAndroidQualityRouteForCi() { _ = QualitySettings.GetQualitySettings(); _ = \"m_PerPlatformDefaultQuality\"; _ = QualitySettings.TryIncludePlatformAt(\"Android\", 0, out _); _ = QualitySettings.TryExcludePlatformAt(\"Android\", 1, out _); }\n",
                encoding="utf-8",
            )
            (shader_dir / "Warmup.shadervariants").write_text("variants", encoding="utf-8")
            (shader_dir / "Risky.compute").write_text(
                """
#pragma target 5.0
#pragma kernel CSMain
[numthreads(8, 8, 8)]
void CSMain(uint3 id : SV_DispatchThreadID) {}
""",
                encoding="utf-8",
            )
            (shader_dir / "Risky.compute.meta").write_text(
                "guid: 0123456789abcdef0123456789abcdef\n",
                encoding="utf-8",
            )
            prefab = root / "Assets" / "_Project" / "Prefabs" / "Player.prefab"
            prefab.parent.mkdir(parents=True)
            prefab.write_text(
                "compute: {fileID: 7200000, guid: 0123456789abcdef0123456789abcdef, type: 3}\n",
                encoding="utf-8",
            )
            (shader_dir / "Feature.shader").write_text(
                "#pragma shader_feature _ LOW HIGH\n#pragma target 4.5\n",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)

        self.assertTrue(payload["readiness"]["androidSustainedPerformanceEnabled"])
        self.assertTrue(payload["readiness"]["androidVulkanOnlySerialized"])
        self.assertTrue(payload["readiness"]["questUrpAssetPresent"])
        self.assertTrue(payload["readiness"]["questUrpWiredToAndroidQuality"])
        self.assertTrue(payload["qualityPipeline"]["questConfiguratorQualityRouteAuditPresent"])
        self.assertTrue(payload["qualityPipeline"]["questConfiguratorQualityRouteFixerPresent"])
        self.assertTrue(payload["readiness"]["graphicsSettingsShaderPreloadBypassDisabled"])
        self.assertTrue(payload["readiness"]["shaderVariantCollectionsPresent"])
        self.assertTrue(payload["readiness"]["bootstrapExplicitShaderWarmup"])
        self.assertTrue(payload["readiness"]["shaderWarmupRoutePresent"])
        self.assertEqual(payload["shaderWarmup"]["bootstrapExplicitWarmUpCallCount"], 0)
        self.assertEqual(payload["shaderWarmup"]["bootstrapShaderWarmupFromCollectionCallCount"], 1)
        self.assertEqual(payload["shaderWarmup"]["bootstrapGraphicsStateWarmUpProgressivelyCallCount"], 1)
        self.assertFalse(payload["readiness"]["noHighRiskComputeThreadGroups"])
        self.assertFalse(payload["readiness"]["noRuntimeHighRiskComputeThreadGroups"])
        self.assertEqual(payload["computeThreads"]["riskyThreadGroupCount"], 1)
        self.assertEqual(payload["computeThreads"]["runtimeRiskyThreadGroupCount"], 1)
        self.assertEqual(payload["computeThreads"]["runtimeAssetRiskyThreadGroupCount"], 1)
        self.assertEqual(payload["computeThreads"]["riskyThreadGroupCountByRuntimeReachability"]["RuntimeSerialized"], 1)
        self.assertEqual(payload["computeThreads"]["riskyThreadGroupCountByExecutionSurface"]["Runtime"], 1)
        self.assertEqual(payload["computeThreads"]["target50ComputeFileCount"], 1)
        self.assertEqual(payload["shaderWarmup"]["shaderFeaturePragmaCount"], 1)

    def test_unreferenced_runtime_compute_asset_does_not_fail_referenced_gate(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_platform_audit_unreferenced_compute_") as tmp:
            root = Path(tmp)
            shader_dir = root / "Assets" / "_Project" / "Art" / "Shaders"
            shader_dir.mkdir(parents=True)
            (shader_dir / "Dormant.compute").write_text(
                """
#pragma kernel CSMain
[numthreads(16, 16, 1)]
void CSMain(uint3 id : SV_DispatchThreadID) {}
""",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)

        self.assertEqual(payload["computeThreads"]["riskyThreadGroupCount"], 1)
        self.assertEqual(payload["computeThreads"]["runtimeAssetRiskyThreadGroupCount"], 1)
        self.assertEqual(payload["computeThreads"]["runtimeRiskyThreadGroupCount"], 0)
        self.assertEqual(payload["computeThreads"]["riskyThreadGroupCountByRuntimeReachability"]["UnreferencedAsset"], 1)
        self.assertTrue(payload["readiness"]["noHighRiskComputeThreadGroups"])
        self.assertTrue(payload["readiness"]["noRuntimeHighRiskComputeThreadGroups"])
        self.assertFalse(payload["readiness"]["noRuntimeAssetHighRiskComputeThreadGroups"])
        referenced_args = audit.build_parser().parse_args(["--fail-on-high-risk-compute"])
        asset_args = audit.build_parser().parse_args(["--fail-on-runtime-asset-high-risk-compute"])
        self.assertEqual(audit.hard_failures(payload, referenced_args), [])
        self.assertEqual(
            audit.hard_failures(payload, asset_args),
            ["high-risk runtime asset numeric compute thread group detected"],
        )

    def test_editor_test_only_runtime_compute_asset_is_reported_but_not_blocking(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_platform_audit_test_only_compute_") as tmp:
            root = Path(tmp)
            shader_dir = root / "Assets" / "_Project" / "Art" / "Shaders"
            shader_dir.mkdir(parents=True)
            (shader_dir / "TestOnly.compute").write_text(
                """
#pragma kernel CSMain
[numthreads(16, 16, 1)]
void CSMain(uint3 id : SV_DispatchThreadID) {}
""",
                encoding="utf-8",
            )
            (shader_dir / "TestOnly.compute.meta").write_text(
                "guid: abcdefabcdefabcdefabcdefabcdefab\n",
                encoding="utf-8",
            )
            test_dir = root / "Assets" / "_Project" / "Tests" / "Editor"
            test_dir.mkdir(parents=True)
            (test_dir / "ComputeLayoutTests.cs").write_text(
                "internal sealed class ComputeLayoutTests { private const string Path = \"Assets/_Project/Art/Shaders/TestOnly.compute\"; }\n",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)

        self.assertEqual(payload["computeThreads"]["riskyThreadGroupCount"], 1)
        self.assertEqual(payload["computeThreads"]["runtimeAssetRiskyThreadGroupCount"], 0)
        self.assertEqual(payload["computeThreads"]["editorOrTestOnlyRuntimeAssetRiskyThreadGroupCount"], 1)
        self.assertEqual(payload["computeThreads"]["runtimeRiskyThreadGroupCount"], 0)
        self.assertEqual(payload["computeThreads"]["riskyThreadGroupCountByRuntimeReachability"]["EditorOrTestOnly"], 1)
        self.assertTrue(payload["readiness"]["noRuntimeAssetHighRiskComputeThreadGroups"])
        args = audit.build_parser().parse_args(["--fail-on-runtime-asset-high-risk-compute"])
        self.assertEqual(audit.hard_failures(payload, args), [])

    def test_editor_compute_risk_does_not_fail_runtime_compute_gate(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_platform_audit_editor_compute_") as tmp:
            root = Path(tmp)
            editor_dir = root / "Assets" / "Editor" / "Bake"
            editor_dir.mkdir(parents=True)
            (editor_dir / "EditorOnly.compute").write_text(
                """
#pragma kernel CSMain
[numthreads(16, 16, 1)]
void CSMain(uint3 id : SV_DispatchThreadID) {}
""",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)

        self.assertEqual(payload["computeThreads"]["riskyThreadGroupCount"], 1)
        self.assertEqual(payload["computeThreads"]["runtimeRiskyThreadGroupCount"], 0)
        self.assertEqual(payload["computeThreads"]["riskyThreadGroupCountByExecutionSurface"]["Editor"], 1)
        self.assertTrue(payload["readiness"]["noHighRiskComputeThreadGroups"])
        self.assertTrue(payload["readiness"]["noRuntimeHighRiskComputeThreadGroups"])

    def test_runtime_compute_dispatch_without_threadgroup_query_trips_gate(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_platform_audit_dispatch_") as tmp:
            root = Path(tmp)
            runtime_dir = root / "Assets" / "_Project" / "Scripts" / "World"
            runtime_dir.mkdir(parents=True)
            (runtime_dir / "UnsafeDispatch.cs").write_text(
                "internal sealed class UnsafeDispatch { "
                "private UnityEngine.ComputeShader compute; "
                "private void Run() { compute.Dispatch(0, 8, 1, 1); } "
                "}\n",
                encoding="utf-8",
            )
            (runtime_dir / "SafeDispatch.cs").write_text(
                "internal sealed class SafeDispatch { "
                "private UnityEngine.ComputeShader compute; "
                "private void Run() { compute.GetKernelThreadGroupSizes(0, out uint x, out _, out _); compute.Dispatch(0, (63u + x) / x, 1, 1); } "
                "}\n",
                encoding="utf-8",
            )
            editor_dir = root / "Assets" / "_Project" / "Scripts" / "Editor" / "Bake"
            editor_dir.mkdir(parents=True)
            (editor_dir / "EditorUnsafeDispatch.cs").write_text(
                "internal sealed class EditorUnsafeDispatch { "
                "private UnityEngine.Rendering.CommandBuffer cmd; "
                "private void Run(UnityEngine.ComputeShader shader) { cmd.DispatchCompute(shader, 0, 8, 1, 1); } "
                "}\n",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)

        self.assertEqual(payload["computeThreads"]["dispatchCallCount"], 3)
        self.assertEqual(payload["computeThreads"]["runtimeDispatchCallCount"], 2)
        self.assertEqual(payload["computeThreads"]["dispatchCallsWithoutThreadGroupQueryCount"], 2)
        self.assertEqual(payload["computeThreads"]["runtimeDispatchCallsWithoutThreadGroupQueryCount"], 1)
        self.assertEqual(payload["computeThreads"]["firstPartyRuntimeDispatchCallsWithoutThreadGroupQueryCount"], 1)
        self.assertEqual(payload["computeThreads"]["firstPartyRuntimePayloadSizedDispatchCallsWithoutThreadGroupQueryCount"], 0)
        self.assertEqual(payload["computeThreads"]["vendorRuntimeDispatchCallsWithoutThreadGroupQueryCount"], 0)
        self.assertFalse(payload["readiness"]["noRuntimeComputeDispatchWithoutThreadGroupQuery"])
        self.assertFalse(payload["readiness"]["noFirstPartyRuntimeComputeDispatchWithoutThreadGroupQuery"])
        args = audit.build_parser().parse_args(["--fail-on-runtime-compute-dispatch-without-threadgroup-query"])
        self.assertEqual(
            audit.hard_failures(payload, args),
            ["first-party runtime compute dispatch without thread-group proof detected"],
        )

    def test_editor_only_compute_dispatch_without_query_does_not_trip_runtime_gate(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_platform_audit_editor_dispatch_") as tmp:
            root = Path(tmp)
            editor_dir = root / "Assets" / "_Project" / "Scripts" / "Editor" / "Bake"
            editor_dir.mkdir(parents=True)
            (editor_dir / "EditorUnsafeDispatch.cs").write_text(
                "internal sealed class EditorUnsafeDispatch { "
                "private UnityEngine.Rendering.CommandBuffer cmd; "
                "private void Run(UnityEngine.ComputeShader shader) { cmd.DispatchCompute(shader, 0, 8, 1, 1); } "
                "}\n",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)

        self.assertEqual(payload["computeThreads"]["dispatchCallCount"], 1)
        self.assertEqual(payload["computeThreads"]["runtimeDispatchCallCount"], 0)
        self.assertEqual(payload["computeThreads"]["runtimeDispatchCallsWithoutThreadGroupQueryCount"], 0)
        self.assertTrue(payload["readiness"]["noRuntimeComputeDispatchWithoutThreadGroupQuery"])
        args = audit.build_parser().parse_args(["--fail-on-runtime-compute-dispatch-without-threadgroup-query"])
        self.assertEqual(audit.hard_failures(payload, args), [])

    def test_payload_sized_first_party_dispatch_bridge_does_not_trip_runtime_gate(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_platform_audit_payload_dispatch_") as tmp:
            root = Path(tmp)
            runtime_dir = root / "Assets" / "_Project" / "Scripts" / "Rendering"
            runtime_dir.mkdir(parents=True)
            (runtime_dir / "PayloadBridge.cs").write_text(
                "internal sealed class PayloadBridge { "
                "private UnityEngine.Rendering.CommandBuffer cmd; "
                "private void Run(Payload payload) { cmd.DispatchCompute(\n"
                "payload.Compute,\n"
                "payload.Kernel,\n"
                "payload.DispatchGroupsX,\n"
                "payload.DispatchGroupsY,\n"
                "1); } "
                "private struct Payload { public UnityEngine.ComputeShader Compute; public int Kernel; public int DispatchGroupsX; public int DispatchGroupsY; } "
                "}\n",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)

        self.assertEqual(payload["computeThreads"]["runtimeDispatchCallsWithoutThreadGroupQueryCount"], 1)
        self.assertEqual(payload["computeThreads"]["firstPartyRuntimeDispatchCallsWithoutThreadGroupQueryCount"], 0)
        self.assertEqual(payload["computeThreads"]["firstPartyRuntimePayloadSizedDispatchCallsWithoutThreadGroupQueryCount"], 1)
        self.assertTrue(payload["readiness"]["noRuntimeComputeDispatchWithoutThreadGroupQuery"])
        args = audit.build_parser().parse_args(["--fail-on-runtime-compute-dispatch-without-threadgroup-query"])
        self.assertEqual(audit.hard_failures(payload, args), [])

    def test_vendor_runtime_dispatch_without_query_is_reported_but_not_first_party_blocking(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_platform_audit_vendor_dispatch_") as tmp:
            root = Path(tmp)
            runtime_dir = root / "Assets" / "Crest" / "Runtime"
            runtime_dir.mkdir(parents=True)
            (runtime_dir / "VendorDispatch.cs").write_text(
                "internal sealed class VendorDispatch { "
                "private UnityEngine.ComputeShader compute; "
                "private void Run() { compute.Dispatch(0, 8, 1, 1); } "
                "}\n",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)

        self.assertEqual(payload["computeThreads"]["runtimeDispatchCallsWithoutThreadGroupQueryCount"], 1)
        self.assertEqual(payload["computeThreads"]["vendorRuntimeDispatchCallsWithoutThreadGroupQueryCount"], 1)
        self.assertEqual(payload["computeThreads"]["firstPartyRuntimeDispatchCallsWithoutThreadGroupQueryCount"], 0)
        self.assertTrue(payload["readiness"]["noRuntimeComputeDispatchWithoutThreadGroupQuery"])
        args = audit.build_parser().parse_args(["--fail-on-runtime-compute-dispatch-without-threadgroup-query"])
        self.assertEqual(audit.hard_failures(payload, args), [])

    def test_detects_payloads_builds_and_plugins(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_platform_audit_full_") as tmp:
            root = Path(tmp)
            (root / "Assets" / "AddressableAssetsData").mkdir(parents=True)
            (root / "Assets" / "AddressableAssetsData" / "settings.asset").write_text("x", encoding="utf-8")
            monolith = root / "Assets" / "StreamingAssets" / "Hecton8" / "DataMonolith" / "static_data.h8bin"
            monolith.parent.mkdir(parents=True)
            monolith.write_bytes(b"h8")
            build = root / "Builds" / "Win" / "Hecton8.exe"
            build.parent.mkdir(parents=True)
            build.write_bytes(b"exe")
            plugin = root / "Assets" / "_Project" / "Plugins" / "Windows" / "x86_64" / "HectonAudioKernel.dll"
            plugin.parent.mkdir(parents=True)
            plugin.write_bytes(b"dll")

            payload = audit.build_payload(root)

        self.assertTrue(payload["readiness"]["addressablesContentPresent"])
        self.assertTrue(payload["readiness"]["dataMonolithPresent"])
        self.assertTrue(payload["readiness"]["buildArtifactPresent"])
        self.assertEqual(payload["nativePlugins"]["pluginFileCount"], 1)


if __name__ == "__main__":
    unittest.main()
