using System;
using System.IO;
using Hecton8.Lighting;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class LightingRuntimeEditTests
    {
        [Test]
        public void LightShaftLateFrameDoesNotAllocateVaultBuffers()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Lighting/Shafts/ScreenSpaceLightShaftRuntime.cs");
            string source = File.ReadAllText(path);
            string lateFrameBlock = ExtractMethodBlock(source, "public void LateFrameTick()");

            Assert.That(lateFrameBlock, Does.Contain("EnsureBuffers(false)"));
            Assert.That(lateFrameBlock, Does.Not.Contain("EnsureBuffers(true)"));
        }

        [Test]
        public void LightShaftColdPhasesOwnVaultAllocation()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Lighting/Shafts/ScreenSpaceLightShaftRuntime.cs");
            string source = File.ReadAllText(path);

            Assert.That(source, Does.Contain("EnsureBuffers(true);"));
            Assert.That(source, Does.Contain("if (!allowAllocation || vault.IsAllocationLocked)"));
            Assert.That(source, Does.Contain("Dump_13KRA.bin"));
            Assert.That(source, Does.Not.Contain("Dump_ABYSSAL_LIGHTING_TECH.bin"));
        }

        [Test]
        public void LightShaftClearShaderGlobalsIsDirtyGuarded()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Lighting/Shafts/ScreenSpaceLightShaftRuntime.cs");
            string source = File.ReadAllText(path);
            string clearBlock = ExtractMethodBlock(source, "private void ClearShaderGlobals()");
            string pushBlock = ExtractMethodBlock(source, "private void PushShaderGlobals(int activeCount, NativeArray<LightShaftContribution> topContributions)");

            Assert.That(source, Does.Contain("private bool _shaderGlobalsCleared;"));
            Assert.That(clearBlock, Does.Contain("if (_shaderGlobalsCleared)"));
            Assert.That(clearBlock, Does.Contain("_shaderGlobalsCleared = true;"));
            Assert.That(pushBlock, Does.Contain("_shaderGlobalsCleared = false;"));
        }

        [Test]
        public void LightShaftTelemetryStoresQualityPressureAsQ8()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Lighting/Shafts/ScreenSpaceLightShaftRuntime.cs");
            string source = File.ReadAllText(path);
            string lateFrameBlock = ExtractMethodBlock(source, "public void LateFrameTick()");
            string recordBlock = ExtractMethodBlock(source, "private void RecordTelemetry(");
            string dumpBlock = ExtractMethodBlock(source, "private void DumpBlackbox(");

            Assert.That(source, Does.Not.Contain("TelemetryFlagQualityPressure"));
            Assert.That(lateFrameBlock, Does.Not.Contain("_qualityPressure01 > 0.001f"));
            Assert.That(source, Does.Contain("[FieldOffset(33)]"));
            Assert.That(source, Does.Contain("public byte QualityPressureQ8;"));
            Assert.That(source, Does.Contain("private static byte EncodeQualityPressureQ8(float qualityPressure01)"));
            Assert.That(recordBlock, Does.Contain("QualityPressureQ8 = EncodeQualityPressureQ8(_qualityPressure01)"));
            Assert.That(dumpBlock, Does.Contain("writer.Write(entry.QualityPressureQ8);"));
        }

        [Test]
        public void DynamicPointLightRuntimeHotPathsDoNotAllocateVaultStorage()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Lighting/DynamicPointLightCulling/DynamicPointLightCullingDirector.cs");
            string source = File.ReadAllText(path);
            string tickBlock = ExtractMethodBlock(source, "public void Tick(float deltaTime)");
            string generateMockBlock = ExtractMethodBlock(source, "public bool GenerateMockLightCullingData()");
            string commitBlock = ExtractMethodBlock(source, "public bool TryCommitExternalSourceCount(int count, uint writerHash)");

            Assert.That(tickBlock, Does.Contain("EnsureNativeStorage(allowAllocation: false, allowMockGeneration: false)"));
            Assert.That(generateMockBlock, Does.Contain("EnsureNativeStorage(allowAllocation: false, allowMockGeneration: false)"));
            Assert.That(commitBlock, Does.Contain("EnsureNativeStorage(allowAllocation: false, allowMockGeneration: false)"));
        }

        [Test]
        public void DynamicPointLightColdAllocationIsExplicitAndBlackBoxOwned()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Lighting/DynamicPointLightCulling/DynamicPointLightCullingDirector.cs");
            string source = File.ReadAllText(path);

            Assert.That(source, Does.Contain("EnsureNativeStorage(bool allowAllocation = true, bool allowMockGeneration = true)"));
            Assert.That(source, Does.Contain("if (!allowAllocation || vault.IsAllocationLocked)"));
            Assert.That(source, Does.Contain("Dump_13KRA.bin"));
            Assert.That(source, Does.Not.Contain("Dump_LIGHT_DIRECTOR.bin"));
        }

        [Test]
        public void DynamicPointLightContractsUseCurrentOwnerProofText()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Lighting/DynamicPointLightCulling/DynamicPointLightCullingContracts.cs");
            string source = File.ReadAllText(path);

            Assert.That(source, Does.Contain("owned by 13KRA"));
            Assert.That(source, Does.Contain("maintained by 13KRA"));
            Assert.That(source, Does.Not.Contain("SHINOBU_151"));
        }

        [Test]
        public void DynamicPointLightNoAllocationPathDoesNotMutateRecoveryState()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Lighting/DynamicPointLightCulling/DynamicPointLightCullingDirector.cs");
            string source = File.ReadAllText(path);
            string storageBlock = ExtractMethodBlock(source, "private bool EnsureNativeStorage(bool allowAllocation = true, bool allowMockGeneration = true)");
            string normalizedStorageBlock = storageBlock.Replace("\r\n", "\n");

            Assert.That(storageBlock, Does.Contain("if (allowAllocation && sourceBuffersWillChange)"));
            Assert.That(storageBlock, Does.Contain("if (allowAllocation && sdfBufferWillChange)"));
            Assert.That(normalizedStorageBlock, Does.Contain("if (allowAllocation)\n                WriteSelfAudit();"));
            Assert.That(storageBlock, Does.Contain("if (allowAllocation && allowMockGeneration && generateMockDataOnEnable"));
        }

        [Test]
        public void DeferredCausticsColdAllocationHonorsVaultLockAndBlackBoxOwner()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Rendering/AbyssalCaustics/AbyssalDeferredCausticsRuntime.cs");
            string source = File.ReadAllText(path);
            string acquireBlock = ExtractMethodBlock(source, "private bool AcquireOrRefreshOwnedVaultBuffer<T>(");
            string dumpBlock = ExtractMethodBlock(source, "private void DumpBlackBox()");

            Assert.That(acquireBlock, Does.Contain("if (vault.IsAllocationLocked)"));
            Assert.That(source, Does.Contain("Docs/AgentLogs/Dump_13KRA.bin"));
            Assert.That(source, Does.Contain("private const string BlackBoxDumpPayloadLabel = \"abyssalCausticsBlackBoxDumpPayload\";"));
            Assert.That(dumpBlock, Does.Contain("NativeFaultDumpWriter.CreateTransientPayload("));
            Assert.That(dumpBlock, Does.Contain("nameof(AbyssalDeferredCausticsRuntime)"));
            Assert.That(dumpBlock, Does.Contain("BlackBoxDumpPayloadLabel"));
            Assert.That(dumpBlock, Does.Contain("NativeArrayOptions.UninitializedMemory"));
            Assert.That(dumpBlock, Does.Contain("NativeFaultDumpWriter.DisposeTransientPayload("));
            Assert.That(dumpBlock, Does.Not.Contain("new NativeArray<byte>(totalBytes"));
            Assert.That(dumpBlock, Does.Not.Contain("payload.Dispose()"));
            Assert.That(source, Does.Not.Contain("Dump_1719.bin"));
            Assert.That(source, Does.Not.Contain("Dump_SHINOBU_232.bin"));
        }

        [Test]
        public void DeferredCausticsFullscreenProxyUsesLowTierShaderTarget()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Art/Shaders/Hecton_DeferredCaustics.shader");
            string source = File.ReadAllText(path);

            Assert.That(source, Does.Contain("#pragma target 3.5"));
            Assert.That(source, Does.Not.Contain("#pragma target 4.5"));
            Assert.That(source, Does.Not.Contain("StructuredBuffer"));
            Assert.That(source, Does.Not.Contain("RWTexture"));
            Assert.That(source, Does.Not.Contain("RWStructuredBuffer"));
            Assert.That(source, Does.Not.Contain("ByteAddressBuffer"));
        }

        [Test]
        public void AbyssalCausticsProofStringsUseCurrentAgentId()
        {
            string contractsPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Rendering/AbyssalCaustics/AbyssalCausticsContracts.cs");
            string auditPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Rendering/AbyssalCaustics/Editor/AbyssalCausticsLayoutAudit.cs");
            string contractsSource = File.ReadAllText(contractsPath);
            string auditSource = File.ReadAllText(auditPath);

            Assert.That(contractsSource, Does.Contain("13KRA-owned Vault lane"));
            Assert.That(contractsSource, Does.Contain("13KRA-owned BufferID"));
            Assert.That(auditSource, Does.Contain("13KRA caustics DTO layout audit"));
            Assert.That(contractsSource, Does.Not.Contain("SHINOBU-owned"));
            Assert.That(auditSource, Does.Not.Contain("SHINOBU_232"));
        }

        [Test]
        public void DomainBlackBoxProofArtifactsUseCurrentAgentId()
        {
            string[] relativePaths =
            {
                "_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs",
                "_Project/Scripts/Lighting/HectonGIRelaySystem.cs",
                "_Project/Scripts/Lighting/HectonLightingRuntime_DayNightRelay.cs",
                "_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs",
                "_Project/Scripts/Lighting/Editor/OOP_Lighting_Scanner.cs"
            };

            foreach (string relativePath in relativePaths)
            {
                string source = File.ReadAllText(Path.Combine(Application.dataPath, relativePath));
                Assert.That(source, Does.Contain("Dump_13KRA.bin"), relativePath);
                Assert.That(source, Does.Not.Contain("Dump_SHINOBU_347.bin"), relativePath);
                Assert.That(source, Does.Not.Contain("Dump_SHINOBU_347_GI_RELAY_SYNC.bin"), relativePath);
                Assert.That(source, Does.Not.Contain("Dump_LIGHTING_SURGEON.bin"), relativePath);
                Assert.That(source, Does.Not.Contain("Dump_DRS_SURGEON.bin"), relativePath);
            }
        }

        [Test]
        public void LightingScannerReportArtifactsUseCurrentAgentId()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Lighting/Editor/OOP_Lighting_Scanner.cs");
            string source = File.ReadAllText(path);

            Assert.That(source, Does.Contain("RENDERING_OPTIMIZATION_REPORT_13KRA.json"));
            Assert.That(source, Does.Contain("\"agent\": \"13KRA\""));
            Assert.That(source, Does.Not.Contain("RENDERING_OPTIMIZATION_REPORT_SHINOBU_347.json"));
            Assert.That(source, Does.Not.Contain("\"agent\": \"SHINOBU_347\""));
        }

        [Test]
        public void InteriorGITickDoesNotAllocateNativeStorage()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs");
            string source = File.ReadAllText(path);
            string tickBlock = ExtractMethodBlock(source, "public void Tick(float deltaTime)");
            string acquireBlock = ExtractMethodBlock(source, "private VaultGenerationHandle<T> AcquireBuffer<T>(");
            string resolveBlock = ExtractMethodBlock(source, "private NativeArray<T> ResolveArray<T>(");

            Assert.That(tickBlock, Does.Contain("EnsureNativeState(allowAllocation: false)"));
            Assert.That(source, Does.Contain("EnsureNativeState(bool allowAllocation = true)"));
            Assert.That(source, Does.Contain("if (!allowAllocation || vault.IsAllocationLocked)"));
            Assert.That(source, Does.Contain("HasRequiredNativeBuffers()"));
            Assert.That(source, Does.Contain("TryReadOnlyArray("));
            Assert.That(source, Does.Contain("vault.TryAcquireWriteLock(in _tuning, MemoryOwner"));
            Assert.That(source, Does.Contain("vault.ReleaseWriteLock(in _tuning, MemoryOwner"));
            Assert.That(source, Does.Contain("vault.IsCompactionFenceActive"));
            Assert.That(acquireBlock, Does.Contain("return default;"));
            Assert.That(resolveBlock, Does.Contain("return default;"));
            Assert.That(source, Does.Not.Contain("Interior GI DataVault buffer acquisition failed"));
            Assert.That(source, Does.Not.Contain("Interior GI GlobalDataVault unavailable"));
            Assert.That(source, Does.Not.Contain("throw new InvalidOperationException"));
        }

        [Test]
        public void InteriorGIDtoLayoutsAreFixedAndEightByteAligned()
        {
            Assert.That(InteriorGIProbeVolumeRuntime.ValidateStructLayouts(out uint failureMask), Is.True, failureMask.ToString("X8"));
            Assert.That(UnsafeUtility.SizeOf<CustomLightProbeDTO>(), Is.EqualTo(InteriorGIProbeVolumeRuntime.CustomLightProbeDtoSizeBytes));
            Assert.That(UnsafeUtility.SizeOf<InteriorGISourceDTO>(), Is.EqualTo(InteriorGIProbeVolumeRuntime.InteriorGISourceDtoSizeBytes));
            Assert.That(UnsafeUtility.SizeOf<InteriorGIOcclusionCellDTO>(), Is.EqualTo(InteriorGIProbeVolumeRuntime.InteriorGIOcclusionCellDtoSizeBytes));
            Assert.That(UnsafeUtility.SizeOf<InteriorGITuningDTO>(), Is.EqualTo(InteriorGIProbeVolumeRuntime.InteriorGITuningDtoSizeBytes));
            Assert.That(UnsafeUtility.SizeOf<MockPowerState>(), Is.EqualTo(InteriorGIProbeVolumeRuntime.MockPowerStateSizeBytes));
            Assert.That(UnsafeUtility.SizeOf<InteriorGITelemetryEntry>(), Is.EqualTo(InteriorGIProbeVolumeRuntime.InteriorGITelemetryEntrySizeBytes));
            Assert.That(UnsafeUtility.SizeOf<CustomDynamicProbeLightDTO>(), Is.EqualTo(InteriorGIProbeVolumeRuntime.CustomDynamicProbeLightDtoSizeBytes));
            Assert.That(UnsafeUtility.SizeOf<AmbientLightingProfileDTO>(), Is.EqualTo(InteriorGIProbeVolumeRuntime.AmbientLightingProfileDtoSizeBytes));
        }

        [Test]
        public void LightmapBakerDeletesTemporaryReflectionProbeAssetsAfterAtlasPacking()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Editor/Lighting/LightmapBakerEngine.cs");
            string source = File.ReadAllText(path);
            string bakeBlock = ExtractMethodBlock(source, "private static void BakeReflectionProbes(");
            string deleteBlock = ExtractMethodBlock(source, "private static void DeleteTemporaryReflectionProbeAssets(");

            Assert.That(bakeBlock, Does.Contain("CreateReflectionCubemapArrayAtlas(sceneName, bakedProbeAssets"));
            Assert.That(bakeBlock, Does.Contain("DeleteTemporaryReflectionProbeAssets(bakedProbeAssets"));
            Assert.That(deleteBlock, Does.Contain("AssetDatabase.DeleteAsset(assetPath)"));
            Assert.That(source, Does.Contain("registerGeneratedAsset: false"));
            Assert.That(source, Does.Contain("TextureImporterFormat.BC6H"));
        }

        [Test]
        public void LightmapBakerDensifiesLightProbesAroundNavigationMarkersWithoutAiDependency()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Editor/Lighting/LightmapBakerEngine.cs");
            string source = File.ReadAllText(path);
            string gridBlock = ExtractMethodBlock(source, "private static void GenerateLightProbeGrid(");
            string markerBlock = ExtractMethodBlock(source, "private static bool LooksLikeNavigationLightingMarker(");

            Assert.That(gridBlock, Does.Contain("AddNavigationMarkerProbes(profile, probes, quantized, report)"));
            Assert.That(markerBlock, Does.Contain("StringComparison.OrdinalIgnoreCase"));
            Assert.That(markerBlock, Does.Contain("\"Waypoint\""));
            Assert.That(markerBlock, Does.Contain("\"Spawn\""));
            Assert.That(markerBlock, Does.Contain("\"Fauna\""));
            Assert.That(source, Does.Not.Contain("using UnityEngine.AI"));
            Assert.That(source, Does.Not.Contain("PathFunnelNavmeshRuntime"));
        }

        [Test]
        public void LightmapBakerDryRunDoesNotMutateBakeSettingsOrScenes()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Editor/Lighting/LightmapBakerEngine.cs");
            string source = File.ReadAllText(path);
            string targetBlock = ExtractMethodBlock(source, "private void ExecuteTargetScenes(bool dryRun)");
            string openSceneBlock = ExtractMethodBlock(source, "private void ExecuteOpenScene(");

            int dryRunBranch = openSceneBlock.IndexOf("if (dryRun)", StringComparison.Ordinal);
            int configureBranch = openSceneBlock.IndexOf("ConfigureLightmapping(sceneName, profile, report)", StringComparison.Ordinal);

            Assert.GreaterOrEqual(dryRunBranch, 0);
            Assert.GreaterOrEqual(configureBranch, 0);
            Assert.Less(dryRunBranch, configureBranch);
            Assert.That(targetBlock, Does.Contain("EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()"));
            Assert.That(openSceneBlock, Does.Contain("AuditSceneLightingInputs(report);"));
            Assert.That(openSceneBlock, Does.Contain("GenerateLightProbeGrid(profile, report, dryRun: true);"));
        }

        [Test]
        public void LightmapBakerOnlyBakesStaticCandidatesAndAvoidsManagedTextureMirrors()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Editor/Lighting/LightmapBakerEngine.cs");
            string source = File.ReadAllText(path);
            string configureRenderersBlock = ExtractMethodBlock(source, "private static void ConfigureStaticRenderers(");
            string validateUvsBlock = ExtractMethodBlock(source, "private static bool ValidateLightmapUvs(");
            string sceneBoundsBlock = ExtractMethodBlock(source, "private static bool TryResolveStaticSceneBounds(");
            string candidateBlock = ExtractMethodBlock(source, "private static bool IsStaticBakeCandidate(");
            string copyBlock = ExtractMethodBlock(source, "private static void CopyAssetFileAsBytes(");

            Assert.That(configureRenderersBlock, Does.Contain("IsStaticBakeCandidate(renderer)"));
            Assert.That(validateUvsBlock, Does.Contain("IsStaticBakeCandidate(renderer)"));
            Assert.That(sceneBoundsBlock, Does.Contain("IsStaticBakeCandidate(renderer)"));
            Assert.That(candidateBlock, Does.Contain("gameObject.isStatic"));
            Assert.That(candidateBlock, Does.Contain("StaticEditorFlags.ContributeGI"));
            Assert.That(candidateBlock, Does.Not.Contain("ReflectionProbeStatic"));
            Assert.That(copyBlock, Does.Contain("File.Copy(source, target, true);"));
            Assert.That(copyBlock, Does.Not.Contain("File.ReadAllBytes"));
            Assert.That(copyBlock, Does.Not.Contain("File.WriteAllBytes"));
        }

        [Test]
        public void LightmapBakerEditorFacadeUsesUiToolkitInsteadOfOngui()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Editor/Lighting/LightmapBakerEngine.cs");
            string source = File.ReadAllText(path);

            Assert.That(source, Does.Contain("using UnityEngine.UIElements;"));
            Assert.That(source, Does.Contain("public void CreateGUI()"));
            Assert.That(source, Does.Contain("new Slider(\"_H8GlobalQualityWeight\", 0f, 1f)"));
            Assert.That(source, Does.Contain("new SliderInt(\"Maximum Probe Count\", 512, 64000)"));
            Assert.That(source, Does.Contain("CreateCommandButton(\"Bake Target Scenes\", RunBakeTargetScenes)"));
            Assert.That(source, Does.Not.Contain("OnGUI("));
            Assert.That(source, Does.Not.Contain("EditorGUILayout."));
            Assert.That(source, Does.Not.Contain("GUILayout."));
        }

        [Test]
        public void RenderSettingsLifecycleRestoreDoesNotTriggerRuntimeDynamicGi()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Core/RenderSettingsLifecycleGuard.cs");
            string source = File.ReadAllText(path);
            string restoreBlock = ExtractMethodBlock(source, "public void Restore()");

            Assert.That(restoreBlock, Does.Contain("IGIRelaySystem giRelay"));
            Assert.That(restoreBlock, Does.Not.Contain("DynamicGI.UpdateEnvironment"));
            Assert.That(source, Does.Not.Contain("DynamicGI.UpdateEnvironment"));
        }

        [Test]
        public void ThermalDrsHotStateWritesDoNotAllocateVaultStorage()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs");
            string source = File.ReadAllText(path);

            Assert.That(source, Does.Contain("TryEnsureScaleStateHandle(bool allowAllocation = false)"));
            Assert.That(source, Does.Contain("TryEnsureDrsStateHandle(bool allowAllocation = false)"));
            Assert.That(source, Does.Contain("TryEnsureTelemetryHandle(bool allowAllocation = false)"));
            Assert.That(source, Does.Contain("if (!allowAllocation || vault.IsAllocationLocked)"));
            Assert.That(source, Does.Contain("TryEnsureScaleStateHandle(allowAllocation: true)"));
        }

        [Test]
        public void BilateralDrsHotInitializationDoesNotAllocateVaultOrGpuState()
        {
            string runtimePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Rendering/BilateralDrs/HectonBilateralDrsUpscalerRuntime.cs");
            string featurePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Rendering/BilateralDrs/HectonBilateralDrsUpscalerFeature.cs");
            string runtimeSource = File.ReadAllText(runtimePath);
            string featureSource = File.ReadAllText(featurePath);
            string onEnableBlock = ExtractMethodBlock(runtimeSource, "private void OnEnable()");
            string preSimulationBlock = ExtractMethodBlock(runtimeSource, "private void RunOwnerPreSimulation(float deltaTime)");
            string visualSyncBlock = ExtractMethodBlock(runtimeSource, "private void RunOwnerVisualSync()");
            string prepareBlock = ExtractMethodBlock(runtimeSource, "private bool PrepareServiceState(bool allowAllocation)");
            string vaultStateBlock = ExtractMethodBlock(runtimeSource, "private void EnsureVaultState(bool allowAllocation)");
            string constantBufferBlock = ExtractMethodBlock(runtimeSource, "private bool EnsureConstantBuffers(bool allowAllocation)");
            string acquireBlock = ExtractMethodBlock(runtimeSource, "private bool AcquireOrRefreshOwnedVaultBuffer<T>(");
            string hotSwapBlock = ExtractMethodBlock(runtimeSource, "public void OnGlobalRegistryServiceReplaced(");
            string editorReadBlock = ExtractMethodBlock(runtimeSource, "public static bool TryReadEditorTuning(out UpscalerTuningDTO tuning)");
            string readOnlyBlock = ExtractMethodBlock(runtimeSource, "private bool TryReadVaultBuffer<T>(");

            Assert.That(runtimeSource, Does.Not.Contain("Dump_SHINOBU_236.bin"));
            Assert.That(featureSource, Does.Not.Contain("[SHINOBU_236]"));
            Assert.That(onEnableBlock, Does.Contain("InitializeServiceForVisualSync(allowAllocation: true)"));
            Assert.That(preSimulationBlock, Does.Contain("InitializeServiceForSimulation(allowAllocation: false)"));
            Assert.That(visualSyncBlock, Does.Contain("InitializeServiceForVisualSync(allowAllocation: false)"));
            Assert.That(prepareBlock, Does.Contain("if (allowAllocation)"));
            Assert.That(prepareBlock, Does.Contain("if (!allowAllocation)"));
            Assert.That(vaultStateBlock, Does.Contain("EnsureCsvScratch(allowAllocation)"));
            Assert.That(constantBufferBlock, Does.Contain("if (missingBuffer && !allowAllocation)"));
            Assert.That(acquireBlock, Does.Contain("if (!allowAllocation || vault.IsAllocationLocked)"));
            Assert.That(hotSwapBlock, Does.Contain("InitializeServiceForVisualSync(allowAllocation: true)"));
            Assert.That(editorReadBlock, Does.Contain("TryReadVaultBuffer(in runtime._tuningHandle"));
            Assert.That(readOnlyBlock, Does.Contain("vault.TryReadOnlyHandle(in handle, out buffer)"));
        }

        [Test]
        public void GIRelayColdStorageHonorsVaultAllocationLock()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Lighting/HectonGIRelaySystem.cs");
            string source = File.ReadAllText(path);
            string storageBlock = ExtractMethodBlock(source, "private void EnsureNativeStorage()");
            string acquireBlock = ExtractMethodBlock(source, "private VaultGenerationHandle<T> AcquireBuffer<T>(");
            string dayNightPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Lighting/HectonLightingRuntime_DayNightRelay.cs");
            string dayNightSource = File.ReadAllText(dayNightPath);
            string dayNightStorageBlock = ExtractMethodBlock(dayNightSource, "private bool EnsureDayNightRelayNativeStorage()");

            Assert.That(storageBlock, Does.Contain("_vault == null || _vault.IsAllocationLocked"));
            Assert.That(storageBlock, Does.Contain("HasRequiredGIRelayStorage()"));
            Assert.That(storageBlock, Does.Contain("EnsureDayNightRelayNativeStorage()"));
            Assert.That(acquireBlock, Does.Contain("return default;"));
            Assert.That(dayNightStorageBlock, Does.Contain("HasRequiredDayNightRelayStorage()"));
            Assert.That(source, Does.Not.Contain("GI relay DataVault buffer acquisition failed"));
            Assert.That(source, Does.Not.Contain("throw new InvalidOperationException"));
        }

        [Test]
        public void GIRelayPublicReadPathsUseReadOnlyVaultViews()
        {
            string giRelayPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Lighting/HectonGIRelaySystem.cs");
            string dayNightPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Lighting/HectonLightingRuntime_DayNightRelay.cs");
            string giRelaySource = File.ReadAllText(giRelayPath);
            string dayNightSource = File.ReadAllText(dayNightPath);
            string celestialReadBlock = ExtractMethodBlock(giRelaySource, "private bool TryReadCelestialState(out CelestialStateDTO state)");
            string readRelayBlock = ExtractMethodBlock(dayNightSource, "private bool TryReadDayNightRelayArray<T>(");
            string environmentCopyBlock = ExtractMethodBlock(dayNightSource, "public bool TryGetEnvironmentLightingCopy(out EnvironmentLightingDTO lighting)");
            string telemetryReadbackBlock = ExtractMethodBlock(dayNightSource, "public bool TryGetDayNightTelemetryReadback(");
            string tuningCopyBlock = ExtractMethodBlock(dayNightSource, "public bool TryGetLightingRelayTuningCopy(out LightingRelayTuningDTO tuning)");

            Assert.That(celestialReadBlock, Does.Contain("TryReadOnlyHandle(in _celestialStateRead"));
            Assert.That(celestialReadBlock, Does.Contain("NativeArray<CelestialStateDTO>.ReadOnly states"));
            Assert.That(readRelayBlock, Does.Contain("out NativeArray<T>.ReadOnly buffer"));
            Assert.That(readRelayBlock, Does.Contain("_vault.TryReadOnlyHandle(in handle, out buffer)"));
            Assert.That(environmentCopyBlock, Does.Contain("NativeArray<EnvironmentLightingDTO>.ReadOnly environment"));
            Assert.That(telemetryReadbackBlock, Does.Contain("NativeArray<LightingRelayTelemetryEntry>.ReadOnly telemetryRing"));
            Assert.That(telemetryReadbackBlock, Does.Contain("telemetry = telemetryRing;"));
            Assert.That(tuningCopyBlock, Does.Contain("NativeArray<LightingRelayTuningDTO>.ReadOnly tuningArray"));
            Assert.That(giRelaySource, Does.Not.Contain("TryReadHandle("));
            Assert.That(dayNightSource, Does.Not.Contain("TryReadHandle("));
        }

        [Test]
        public void AbyssalLightingEditorDiagnosticsUseCurrentOwnerTag()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Lighting/Editor/AbyssalLightingTunerWindow.cs");
            string source = File.ReadAllText(path);
            string refreshBlock = ExtractMethodBlock(source, "private void RefreshStatus(bool force)");

            Assert.That(source, Does.Contain("[13KRA] Loaded-scene Unity probe group count"));
            Assert.That(source, Does.Contain("private const double RefreshIntervalSeconds = 0.25"));
            Assert.That(source, Does.Contain("RefreshStatus(force: true)"));
            Assert.That(refreshBlock, Does.Contain("EditorApplication.timeSinceStartup"));
            Assert.That(refreshBlock, Does.Contain("if (!force && now < _nextRefreshTime)"));
            Assert.That(source, Does.Not.Contain("[SHINOBU_131]"));
        }

        [Test]
        public void WaterOpticsColdAllocationHonorsVaultLockAndCurrentBlackBoxOwner()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Rendering/WaterOptics/WaterOpticsRuntime.cs");
            string source = File.ReadAllText(path);
            string bufferBlock = ExtractMethodBlock(source, "private bool EnsureVaultBuffers(IDataVault vault, bool clearExisting)");
            string latestParamsBlock = ExtractMethodBlock(source, "public bool TryReadLatestParams(out WaterOpticsDTO dto)");
            string latestTuningBlock = ExtractMethodBlock(source, "public bool TryReadLatestTuning(out WaterOpticsTuningDTO dto)");
            string latestTelemetryBlock = ExtractMethodBlock(source, "public bool TryReadLatestTelemetry(out WaterOpticsTelemetryEntry dto)");
            string telemetryEntryBlock = ExtractMethodBlock(source, "public bool TryReadTelemetryEntry(int framesBack, out WaterOpticsTelemetryEntry dto)");
            string readOnlyBlock = ExtractMethodBlock(source, "private static bool TryReadOnly<T>(");

            Assert.That(bufferBlock, Does.Contain("if (vault.IsAllocationLocked)"));
            Assert.That(source, Does.Contain("Dump_13KRA.bin"));
            Assert.That(source, Does.Not.Contain("Dump_SHINOBU_265.bin"));
            Assert.That(latestParamsBlock, Does.Contain("TryReadOnly(vault, in _paramsHandle"));
            Assert.That(latestTuningBlock, Does.Contain("TryReadOnly(vault, in _tuningHandle"));
            Assert.That(latestTelemetryBlock, Does.Contain("TryReadOnly(vault, in _telemetryHandle"));
            Assert.That(telemetryEntryBlock, Does.Contain("TryReadOnly(vault, in _telemetryCursorHandle"));
            Assert.That(readOnlyBlock, Does.Contain("vault.TryReadOnlyHandle(in handle, out buffer)"));
            Assert.That(source, Does.Not.Contain("vault.TryReadHandle(in handle, out buffer)"));
        }

        [Test]
        public void WaterOpticsEditorProofArtifactsUseCurrentAgentId()
        {
            string[] relativePaths =
            {
                "_Project/Scripts/Rendering/WaterOptics/Editor/PostProcess_Fog_Scanner.cs",
                "_Project/Scripts/Rendering/WaterOptics/Editor/WaterOpticsRuntimeOwnerInstaller.cs",
                "_Project/Scripts/Rendering/WaterOptics/Editor/WaterOpticsRendererFeatureInstaller.cs"
            };

            foreach (string relativePath in relativePaths)
            {
                string source = File.ReadAllText(Path.Combine(Application.dataPath, relativePath));
                Assert.That(source, Does.Contain("13KRA"), relativePath);
                Assert.That(source, Does.Not.Contain("SHINOBU_265"), relativePath);
                Assert.That(source, Does.Not.Contain("Dump_SHINOBU_265.bin"), relativePath);
            }
        }

        [Test]
        public void ScooterVolumetricShaftsUseContinuousQualityBudgets()
        {
            string featurePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Visor/HectonScooterVolumetricShaftsFeature.cs");
            string shaderPath = Path.Combine(
                Application.dataPath,
                "_Project/Art/Shaders/Hecton_ScooterVolumetricShafts.shader");
            string featureSource = File.ReadAllText(featurePath);
            string shaderSource = File.ReadAllText(shaderPath);

            Assert.That(featureSource, Does.Contain("ResolveGlobalQualityWeight01()"));
            Assert.That(featureSource, Does.Contain("HomeostasisBrain.GlobalQualityWeight"));
            Assert.That(featureSource, Does.Contain("ResolveQualityScaledRenderScale("));
            Assert.That(featureSource, Does.Contain("ResolveContactShadowStepBudget("));
            Assert.That(featureSource, Does.Contain("ResolveFlashlightShadowStepBudget("));
            Assert.That(featureSource, Does.Not.Contain("<= 2048 ? 16f : 24f"));
            Assert.That(featureSource, Does.Not.Contain("math.clamp(settings.contactShadowSteps, 4, 8)"));
            Assert.That(shaderSource, Does.Contain("HECTON_CONTACT_SHADOW_EVAL_MAX"));
            Assert.That(shaderSource, Does.Contain("_HectonContactShadowSteps + 0.5"));
            Assert.That(shaderSource, Does.Not.Contain("const int stepCount = 3;"));
        }

        [Test]
        public void NoirDepthFogUsesContinuousQualityAndSurfaceFade()
        {
            string featurePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Visor/HectonNoirDepthFogFeature.cs");
            string shaderPath = Path.Combine(
                Application.dataPath,
                "_Project/Art/Shaders/Hecton_NoirDepthFog.shader");
            string featureSource = File.ReadAllText(featurePath);
            string shaderSource = File.ReadAllText(shaderPath);
            string addPassesBlock = ExtractMethodBlock(featureSource, "public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)");

            Assert.That(featureSource, Does.Contain("ResolveGlobalQualityWeight01()"));
            Assert.That(featureSource, Does.Contain("HomeostasisBrain.GlobalQualityWeight"));
            Assert.That(featureSource, Does.Contain("ResolveSurfaceFogWeight01("));
            Assert.That(featureSource, Does.Contain("Smooth01(playerMovement.CurrentDepth / safeDepth)"));
            Assert.That(addPassesBlock, Does.Not.Contain("ShouldBypassForSurfaceReadability("));
            Assert.That(shaderSource, Does.Contain("x=quality, y=surface fog weight"));
            Assert.That(shaderSource, Does.Contain("qualityCurve = quality01 * quality01 * (3.0 - 2.0 * quality01)"));
            Assert.That(shaderSource, Does.Contain("surfaceFogWeight"));
            Assert.That(shaderSource, Does.Contain("ditherStrength"));
        }

        [Test]
        public void VolumetricFogKeepsDearLieProxyBelowComputeTier()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Visor/HectonVolumetricParticulateFogFeature.cs");
            string proxyShaderPath = Path.Combine(
                Application.dataPath,
                "_Project/Art/Shaders/Hecton_VolumetricFog_DearLie.shader");
            string source = File.ReadAllText(path);
            string proxyShaderSource = File.ReadAllText(proxyShaderPath);
            string createBlock = ExtractMethodBlock(source, "public override void Create()");
            string addPassesBlock = ExtractMethodBlock(source, "public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)");
            string setupBlock = ExtractMethodBlock(source, "public bool Setup(");
            string recordBlock = ExtractMethodBlock(source, "public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)");
            string nativePrepareBlock = ExtractMethodBlock(source, "public bool TryPrepareNativeState(IDataVault vault, bool allowAllocation)");
            string gpuPrepareBlock = ExtractMethodBlock(source, "public bool TryPrepareGpuState(bool allowAllocation)");
            string diagnosticMaintenanceBlock = ExtractMethodBlock(source, "private void RunDiagnosticMaintenanceIfDue(int currentFrame)");
            string externalHandleBlock = ExtractMethodBlock(source, "private static RTHandle ResolveExternalTextureHandle(Texture texture, ref RTHandle handle, ref Texture handleSource, bool allowAllocation)");
            string cachedExternalHandleBlock = ExtractMethodBlock(source, "private static RTHandle ResolveCachedExternalTextureHandle(");

            Assert.That(source, Does.Contain("Dump_13KRA.bin"));
            Assert.That(source, Does.Not.Contain("Dump_1309_VolumetricFog.bin"));
            Assert.That(source, Does.Not.Contain("SHINOBU_233"));
            Assert.That(addPassesBlock, Does.Not.Contain("settings.computeShader == null"));
            Assert.That(addPassesBlock, Does.Not.Contain("!HardwareTierDetector.AllowHighResourceComputeShaders"));
            Assert.That(addPassesBlock, Does.Contain("allowVolumetricCompute = HardwareTierDetector.AllowHighResourceComputeShaders"));
            Assert.That(addPassesBlock, Does.Contain("bool forceProxyOnly = !allowVolumetricCompute;"));
            Assert.That(addPassesBlock, Does.Contain("forceProxyOnly: true"));
            Assert.That(setupBlock, Does.Contain("bool forceProxyOnly"));
            Assert.That(setupBlock, Does.Contain("return forceProxyOnly || TryBindComputeShader(computeShader);"));
            Assert.That(recordBlock, Does.Contain("bool hasComputeKernels = _computeShader != null"));
            Assert.That(recordBlock, Does.Contain("_forceProxyOnly ||"));
            Assert.That(recordBlock, Does.Contain("!hasComputeKernels"));
            Assert.That(createBlock, Does.Contain("TryPrepareNativeState(GlobalRegistry.DataVault, allowAllocation: true)"));
            Assert.That(createBlock, Does.Contain("TryPrepareGpuState(allowAllocation: true)"));
            Assert.That(addPassesBlock, Does.Contain("RunDiagnosticMaintenanceIfDue(currentFrame)"));
            Assert.That(addPassesBlock, Does.Contain("RefreshExternalBridgeState(allowExternalTextureHandleAllocation: false)"));
            Assert.That(addPassesBlock, Does.Not.Contain("TryPrepareNativeState("));
            Assert.That(addPassesBlock, Does.Not.Contain("TryPrepareGpuState("));
            Assert.That(nativePrepareBlock, Does.Contain("if (!allowAllocation)"));
            Assert.That(nativePrepareBlock, Does.Contain("return EnsureVaultState();"));
            Assert.That(gpuPrepareBlock, Does.Contain("if (!allowAllocation && !HasGpuState)"));
            Assert.That(gpuPrepareBlock, Does.Contain("EnsureFallbackTextures();"));
            Assert.That(gpuPrepareBlock, Does.Contain("EnsureGpuBuffers(allowAllocation)"));
            Assert.That(diagnosticMaintenanceBlock, Does.Not.Contain("TryPrepareNativeState("));
            Assert.That(diagnosticMaintenanceBlock, Does.Not.Contain("TryPrepareGpuState("));
            Assert.That(externalHandleBlock, Does.Contain("if (!allowAllocation)"));
            Assert.That(cachedExternalHandleBlock, Does.Contain("if (!allowAllocation)"));
            Assert.That(proxyShaderSource, Does.Contain("#pragma target 3.5"));
            Assert.That(proxyShaderSource, Does.Not.Contain("#pragma target 4.5"));
        }

        [Test]
        public void VolumetricLightAndBiolumUseTransientGraphTexturesAndContinuousQuality()
        {
            string volumetricPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Visor/VolumetricLightFeature.cs");
            string volumetricProxyShaderPath = Path.Combine(
                Application.dataPath,
                "_Project/Art/Shaders/Hecton_VolumetricLightProxy.shader");
            string shaderCatalogPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Core/RuntimeShaderReferenceCatalog.cs");
            string shaderCatalogAssetPath = Path.Combine(
                Application.dataPath,
                "_Project/Data/RuntimeShaderReferenceCatalog.asset");
            string biolumPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Visor/HectonBiolumSSGIFeature.cs");
            string biolumShaderPath = Path.Combine(
                Application.dataPath,
                "_Project/Art/Shaders/Hecton_BiolumSSGIComposite.shader");
            string volumetricSource = File.ReadAllText(volumetricPath);
            string volumetricProxyShaderSource = File.ReadAllText(volumetricProxyShaderPath);
            string shaderCatalogSource = File.ReadAllText(shaderCatalogPath);
            string shaderCatalogAsset = File.ReadAllText(shaderCatalogAssetPath);
            string biolumSource = File.ReadAllText(biolumPath);
            string biolumShaderSource = File.ReadAllText(biolumShaderPath);
            string volumetricRecordBlock = ExtractMethodBlock(volumetricSource, "public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)");
            string volumetricAddPassesBlock = ExtractMethodBlock(volumetricSource, "public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)");
            string biolumRecordBlock = ExtractMethodBlock(biolumSource, "public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)");
            string biolumAddPassesBlock = ExtractMethodBlock(biolumSource, "public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)");

            Assert.That(volumetricSource, Does.Not.Contain("RTHandles.Alloc"));
            Assert.That(volumetricSource, Does.Not.Contain("EnsureRenderTargets("));
            Assert.That(volumetricSource, Does.Contain("ResolveRenderScale()"));
            Assert.That(volumetricSource, Does.Contain("HomeostasisBrain.GlobalQualityWeight"));
            Assert.That(volumetricRecordBlock, Does.Contain("renderGraph.CreateTexture(halfDesc)"));
            Assert.That(volumetricRecordBlock, Does.Contain("renderGraph.CreateTexture(compositeDesc)"));
            Assert.That(volumetricRecordBlock, Does.Contain("RecordProxyComposite("));
            Assert.That(volumetricAddPassesBlock, Does.Contain("allowComputeVolumetrics"));
            Assert.That(volumetricAddPassesBlock, Does.Contain("forceProxyOnly"));
            Assert.That(volumetricAddPassesBlock, Does.Not.Contain("!HardwareTierDetector.AllowHighResourceComputeShaders)"));
            Assert.That(volumetricProxyShaderSource, Does.Contain("Hidden/Hecton8/VolumetricLightProxy"));
            Assert.That(volumetricProxyShaderSource, Does.Contain("FastTrianglePulse01"));
            Assert.That(shaderCatalogSource, Does.Contain("TryGetVolumetricLightProxyShader"));
            Assert.That(shaderCatalogAsset, Does.Contain("volumetricLightProxyShader"));

            Assert.That(biolumSource, Does.Not.Contain("RTHandles.Alloc"));
            Assert.That(biolumSource, Does.Not.Contain("EnsureGiTexture("));
            Assert.That(biolumSource, Does.Contain("ResolveRenderScale()"));
            Assert.That(biolumSource, Does.Contain("ResolveSampleCount()"));
            Assert.That(biolumSource, Does.Contain("ResolveIntensity()"));
            Assert.That(biolumSource, Does.Contain("HomeostasisBrain.GlobalQualityWeight"));
            Assert.That(biolumRecordBlock, Does.Contain("renderGraph.CreateTexture(gatherDesc)"));
            Assert.That(biolumRecordBlock, Does.Contain("renderGraph.CreateTexture(giDesc)"));
            Assert.That(biolumRecordBlock, Does.Contain("RecordProxyComposite("));
            Assert.That(biolumSource, Does.Contain("forceProxyOnly"));
            Assert.That(biolumAddPassesBlock, Does.Not.Contain("!HardwareTierDetector.AllowHighResourceComputeShaders)"));
            Assert.That(biolumShaderSource, Does.Contain("Name \"ProxyComposite\""));
            Assert.That(biolumShaderSource, Does.Contain("_HectonSSGISampleCount"));
        }

        [Test]
        public void DrsSurvivalPressureScalesUnderwaterPresentationInsteadOfHardCulling()
        {
            string gatePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Visor/HectonDrsRenderFeatureGate.cs");
            string halfResPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Visor/HectonHalfResParticlesFeature.cs");
            string ssdoPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Visor/HectonAbyssalSsdoFeature.cs");
            string scooterPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Visor/HectonScooterVolumetricShaftsFeature.cs");
            string gateSource = File.ReadAllText(gatePath);
            string halfResSource = File.ReadAllText(halfResPath);
            string ssdoSource = File.ReadAllText(ssdoPath);
            string scooterSource = File.ReadAllText(scooterPath);
            string halfResAddPassesBlock = ExtractMethodBlock(halfResSource, "public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)");
            string ssdoAddPassesBlock = ExtractMethodBlock(ssdoSource, "public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)");
            string scooterAddPassesBlock = ExtractMethodBlock(scooterSource, "public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)");
            string halfResRecordBlock = ExtractMethodBlock(halfResSource, "public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)");
            string ssdoRecordBlock = ExtractMethodBlock(ssdoSource, "public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)");
            string scooterRecordBlock = ExtractMethodBlock(scooterSource, "public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)");

            Assert.That(gateSource, Does.Contain("internal static float ResolveSurvivalPressure01()"));
            Assert.That(gateSource, Does.Contain("internal static float ResolveSurvivalVisualWeight01()"));
            Assert.That(gateSource, Does.Not.Contain("ShouldCullForSurvivalScale("));
            Assert.That(gateSource, Does.Not.Contain(">= 0.999f"));

            Assert.That(halfResAddPassesBlock, Does.Not.Contain("ShouldCullForSurvivalScale("));
            Assert.That(ssdoAddPassesBlock, Does.Not.Contain("ShouldCullForSurvivalScale("));
            Assert.That(scooterAddPassesBlock, Does.Not.Contain("ShouldCullForSurvivalScale("));

            Assert.That(halfResRecordBlock, Does.Contain("ResolveSurvivalVisualWeight01()"));
            Assert.That(halfResRecordBlock, Does.Contain("ResolveRenderScale(survivalVisualWeight01)"));
            Assert.That(halfResRecordBlock, Does.Contain("ResolveCompositeStrength(survivalVisualWeight01)"));
            Assert.That(ssdoRecordBlock, Does.Contain("ResolveSurvivalVisualWeight01()"));
            Assert.That(ssdoRecordBlock, Does.Contain("ResolveRenderScale(survivalVisualWeight01)"));
            Assert.That(ssdoRecordBlock, Does.Contain("ResolveRadiusMeters(survivalVisualWeight01)"));
            Assert.That(scooterRecordBlock, Does.Contain("ResolveSurvivalPressure01()"));
            Assert.That(scooterRecordBlock, Does.Contain("CombineVisualBudgetPressure("));
        }

        [Test]
        public void VoxelSsaoDoesNotEnqueueDeadConsumerOrOwnPersistentRtHandles()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Visor/HectonVoxelSsaoFeature.cs");
            string source = File.ReadAllText(path);
            string recordBlock = ExtractMethodBlock(source, "public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)");
            string addPassesBlock = ExtractMethodBlock(source, "public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)");

            Assert.That(source, Does.Contain("private const bool HasRuntimeConsumer = false"));
            Assert.That(source, Does.Contain("HasRuntimeConsumerAvailable"));
            Assert.That(addPassesBlock, Does.Contain("if (!VoxelSsaoPass.HasRuntimeConsumerAvailable)"));
            Assert.That(source, Does.Not.Contain("RTHandles.Alloc"));
            Assert.That(source, Does.Not.Contain("ImportTexture(_aoTexture)"));
            Assert.That(recordBlock, Does.Contain("renderGraph.CreateTexture(aoDesc)"));
            Assert.That(recordBlock, Does.Contain("AddComputePass(\"Hecton Voxel SSAO\""));
            Assert.That(recordBlock, Does.Contain("builder.UseTexture(aoTexture, AccessFlags.Write)"));
            Assert.That(source, Does.Not.Contain("SetGlobalTextureAfterPass"));
            Assert.That(source, Does.Not.Contain("_HectonVoxelSSAOTex"));
        }

        [Test]
        public void VisorUberNoirReadAccessorsUseReadOnlyVaultViewsAndCurrentDumpOwner()
        {
            string noirPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Visor/HectonVisorUberPostFeature.Noir.cs");
            string reconstructionPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Visor/HectonVisorUberPostFeature.cs");
            string noirSource = File.ReadAllText(noirPath);
            string reconstructionSource = File.ReadAllText(reconstructionPath);
            string noirReadBlock = ExtractMethodBlock(noirSource, "private static bool TryReadNoirVaultBuffer<T>(");
            string noirResolveBlock = ExtractMethodBlock(noirSource, "private static bool TryResolveNoirVaultBuffer<T>(");
            string reconstructionReadBlock = ExtractMethodBlock(reconstructionSource, "private static bool TryReadReconstructionVaultBuffer<T>(");
            string reconstructionResolveBlock = ExtractMethodBlock(reconstructionSource, "private static bool TryResolveReconstructionVaultBuffer<T>(");

            Assert.That(noirSource, Does.Contain("NoirDumpFileName = \"Dump_13KRA.bin\""));
            Assert.That(noirSource, Does.Not.Contain("Dump_1309_VisorUberPostNoir.bin"));
            Assert.That(noirReadBlock, Does.Contain("out NativeArray<T>.ReadOnly buffer"));
            Assert.That(noirReadBlock, Does.Contain("vault.TryReadOnlyHandle(in handle, out buffer)"));
            Assert.That(noirResolveBlock, Does.Contain("vault.TryResolveHandle(in handle, out buffer)"));
            Assert.That(noirSource, Does.Contain("out NativeArray<NoirTelemetryEntry>.ReadOnly telemetry"));
            Assert.That(noirSource, Does.Not.Contain("vault.TryReadHandle(in handle"));
            Assert.That(noirSource, Does.Not.Contain("TryOpenNoirVaultBuffer"));

            Assert.That(reconstructionSource, Does.Contain("ReconstructionDumpFileName = \"Dump_13KRA.bin\""));
            Assert.That(reconstructionSource, Does.Not.Contain("Dump_UBER_NOIR.bin"));
            Assert.That(reconstructionReadBlock, Does.Contain("out NativeArray<T>.ReadOnly buffer"));
            Assert.That(reconstructionReadBlock, Does.Contain("vault.TryReadOnlyHandle(in handle, out buffer)"));
            Assert.That(reconstructionResolveBlock, Does.Contain("vault.TryResolveHandle(in handle, out buffer)"));
            Assert.That(reconstructionSource, Does.Contain("out NativeArray<ReconstructionTelemetryEntry>.ReadOnly telemetry"));
            Assert.That(reconstructionSource, Does.Contain("out NativeArray<MockReconstructionInputSignal>.ReadOnly mock"));
            Assert.That(reconstructionSource, Does.Not.Contain("vault.TryReadHandle(in handle"));
            Assert.That(reconstructionSource, Does.Not.Contain("TryOpenReconstructionVaultBuffer"));
        }

        [Test]
        public void VisorUberPostInternalWaterlineUsesContinuousSubmergeWeight()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Visor/HectonVisorUberPostFeature.cs");
            string source = File.ReadAllText(path);
            string waterlineBlock = ExtractMethodBlock(source, "private static Vector4 ResolveInternalWaterlineParams(Camera renderCamera, FeatureSettings settings)");
            string weightBlock = ExtractMethodBlock(source, "private static float ResolveInternalWaterlineSubmergedWeight01(float cameraY, float waterlineY)");

            Assert.That(source, Does.Contain("InternalWaterlineSubmergeFadeMeters"));
            Assert.That(waterlineBlock, Does.Contain("ResolveInternalWaterlineSubmergedWeight01(cameraY, waterlineY)"));
            Assert.That(waterlineBlock, Does.Contain("math.lerp(viewportSplit, InternalWaterlineFullScreenSplit, submerged01)"));
            Assert.That(weightBlock, Does.Contain("Smooth01(math.saturate"));
            Assert.That(source, Does.Not.Contain("cameraY < waterlineY - 0.03f"));
        }

        [Test]
        public void UberNoirRuntimeBridgeHotTelemetryDoesNotAllocateVaultBuffers()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Rendering/HectonUberNoirRuntimeBridge.cs");
            string source = File.ReadAllText(path);
            string awakeBlock = ExtractMethodBlock(source, "private void Awake()");
            string onEnableBlock = ExtractMethodBlock(source, "private void OnEnable()");
            string hotSwapBlock = ExtractMethodBlock(source, "public void OnGlobalRegistryServiceReplaced(");
            string ensureBlock = ExtractMethodBlock(source, "private bool EnsureTelemetryBuffer(bool allowAllocation)");
            string pushBlock = ExtractMethodBlock(source, "private void PushBlackBox(");
            string dumpBlock = ExtractMethodBlock(source, "private void DumpBlackBox(uint reasonFlags)");
            string emptyDumpBlock = ExtractMethodBlock(source, "private void WriteEmptyBlackBox(uint reasonFlags)");

            Assert.That(source, Does.Contain("DumpFileName = \"Dump_13KRA.bin\""));
            Assert.That(source, Does.Not.Contain("Dump_UBER_NOIR_INTEGRATOR.bin"));
            Assert.That(source, Does.Not.Contain("Dump_EXTINCTION_LUT_SAMPLER.bin"));
            Assert.That(source, Does.Not.Contain("IntegratorH8DumpFileName"));
            Assert.That(source, Does.Not.Contain("ExtinctionH8DumpFileName"));
            Assert.That(awakeBlock, Does.Contain("EnsureTelemetryBuffer(allowAllocation: true)"));
            Assert.That(onEnableBlock, Does.Contain("EnsureTelemetryBuffer(allowAllocation: true)"));
            Assert.That(hotSwapBlock, Does.Contain("EnsureTelemetryBuffer(allowAllocation: true)"));
            Assert.That(ensureBlock, Does.Contain("if (!allowAllocation || vault.IsAllocationLocked)"));
            Assert.That(ensureBlock, Does.Contain("vault.EnsureGenerationHandle<UberNoirShaderTelemetryEntry>"));
            Assert.That(pushBlock, Does.Contain("EnsureTelemetryBuffer(allowAllocation: false)"));
            Assert.That(dumpBlock, Does.Contain("EnsureTelemetryBuffer(allowAllocation: false)"));
            Assert.That(dumpBlock, Does.Contain("Path.Combine(logDirectory, DumpFileName)"));
            Assert.That(emptyDumpBlock, Does.Contain("Path.Combine(logDirectory, DumpFileName)"));
            Assert.That(pushBlock, Does.Not.Contain("EnsureGenerationHandle"));
            Assert.That(dumpBlock, Does.Not.Contain("EnsureGenerationHandle"));
        }

        [Test]
        public void GlobalShaderDispatcherLateFrameDoesNotAllocateShaderGlobalSlots()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Rendering/GlobalShaderDispatcher.cs");
            string source = File.ReadAllText(path);
            string awakeBlock = ExtractMethodBlock(source, "private void Awake()");
            string onEnableBlock = ExtractMethodBlock(source, "private void OnEnable()");
            string hotSwapBlock = ExtractMethodBlock(source, "public void OnGlobalRegistryServiceReplaced(");
            string lateFrameBlock = ExtractMethodBlock(source, "public void LateFrameTick()");
            string recordTelemetryBlock = ExtractMethodBlock(source, "private void RecordTelemetry(");
            string dumpTelemetryBlock = ExtractMethodBlock(source, "private void DumpTelemetry(uint reasonFlags)");
            string ensureBlock = ExtractMethodBlock(source, "private static bool EnsureShaderGlobalSlots(IDataVault vault, bool allowAllocation)");
            string commandBufferBlock = ExtractMethodBlock(source, "private static bool TryEnsureCommandBuffer(bool allowAllocation)");
            string readEditorBlock = ExtractMethodBlock(source, "public static bool TryReadEditorTuning(out UberNoirGlobalTuning tuning)");
            string readFlowBlock = ExtractMethodBlock(source, "public static bool TryGetEditorGlobalFlow(out Vector4 flow)");
            string readOnlyBlock = ExtractMethodBlock(source, "private static bool TryReadCachedShaderGlobalSlots(out NativeArray<float4>.ReadOnly slots)");
            string writeEditorBlock = ExtractMethodBlock(source, "public static bool TryWriteEditorTuning(in UberNoirGlobalTuning tuning)");

            Assert.That(source, Does.Contain("DumpFileName = \"Dump_13KRA.bin\""));
            Assert.That(source, Does.Not.Contain("Dump_CBUFFER_DISPATCH.bin"));
            Assert.That(source, Does.Not.Contain("Dump_CBUFFER_DISPATCH.h8dump"));
            Assert.That(source, Does.Not.Contain("DumpH8DumpFileName"));
            Assert.That(awakeBlock, Does.Contain("TryEnsureCommandBuffer(allowAllocation: true)"));
            Assert.That(awakeBlock, Does.Contain("EnsureShaderGlobalSlotsRuntime(out IDataVault vault, allowAllocation: true)"));
            Assert.That(onEnableBlock, Does.Contain("TryEnsureCommandBuffer(allowAllocation: true)"));
            Assert.That(onEnableBlock, Does.Contain("EnsureShaderGlobalSlotsRuntime(out IDataVault vault, allowAllocation: true)"));
            Assert.That(hotSwapBlock, Does.Contain("EnsureShaderGlobalSlotsRuntime(out _, allowAllocation: true)"));
            Assert.That(lateFrameBlock, Does.Contain("SetVisualSyncDispatcherActive(false)"));
            Assert.That(lateFrameBlock, Does.Contain("TryEnsureCommandBuffer(allowAllocation: false)"));
            Assert.That(lateFrameBlock, Does.Contain("EnsureShaderGlobalSlotsRuntime(out IDataVault vault, allowAllocation: false)"));
            Assert.That(recordTelemetryBlock, Does.Contain("EnsureShaderGlobalSlotsRuntime(out IDataVault currentVault, allowAllocation: false)"));
            Assert.That(dumpTelemetryBlock, Does.Contain("EnsureShaderGlobalSlotsRuntime(out IDataVault vault, allowAllocation: false)"));
            Assert.That(dumpTelemetryBlock, Does.Contain("telemetrySnapshot.Clear();"));
            Assert.That(dumpTelemetryBlock, Does.Contain("TelemetryFlagVaultUnavailable"));
            Assert.That(ensureBlock, Does.Contain("if (!allowAllocation || vault.IsAllocationLocked)"));
            Assert.That(ensureBlock, Does.Contain("vault.EnsureGenerationHandle<float4>"));
            Assert.That(commandBufferBlock, Does.Contain("if (!allowAllocation)"));
            Assert.That(lateFrameBlock, Does.Not.Contain("EnsureGenerationHandle"));
            Assert.That(recordTelemetryBlock, Does.Not.Contain("EnsureGenerationHandle"));
            Assert.That(dumpTelemetryBlock, Does.Not.Contain("EnsureGenerationHandle"));
            Assert.That(readEditorBlock, Does.Contain("TryReadCachedShaderGlobalSlots"));
            Assert.That(readFlowBlock, Does.Contain("TryReadCachedShaderGlobalSlots"));
            Assert.That(readOnlyBlock, Does.Contain("vault.TryReadOnlyHandle(in s_shaderSlotsHandle, out slots)"));
            Assert.That(writeEditorBlock, Does.Contain("EnsureShaderGlobalSlots(out IDataVault vault, allowAllocation: true)"));
            Assert.That(source, Does.Not.Contain("TryResolveCachedShaderGlobalSlots"));
        }

        [Test]
        public void ShaderGlobalBridgeFailsBackWhenDispatcherDrops()
        {
            string dispatcherPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Rendering/GlobalShaderDispatcher.cs");
            string bridgePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Rendering/HectonShaderGlobalDataVaultBridge.cs");
            string dispatcherSource = File.ReadAllText(dispatcherPath);
            string bridgeSource = File.ReadAllText(bridgePath);
            string lateFrameBlock = ExtractMethodBlock(dispatcherSource, "public void LateFrameTick()");
            string executeBlock = ExtractMethodBlock(dispatcherSource, "private void ExecuteGlobalDispatch(");
            string activeBlock = ExtractMethodBlock(bridgeSource, "internal static void SetVisualSyncDispatcherActive(bool active)");
            string flushBlock = ExtractMethodBlock(bridgeSource, "internal static void FlushFallbackVisualSync()");

            Assert.That(lateFrameBlock, Does.Contain("SetVisualSyncDispatcherActive(false)"));
            Assert.That(executeBlock, Does.Contain("SetVisualSyncDispatcherActive(true)"));
            Assert.That(activeBlock, Does.Contain("if (_visualSyncDispatcherActive == active)"));
            Assert.That(activeBlock, Does.Contain("_visualSyncDispatcherActive = active;"));
            Assert.That(activeBlock, Does.Contain("if (!active)"));
            Assert.That(activeBlock, Does.Contain("MarkFallbackShaderGlobalsDirty();"));
            Assert.That(flushBlock, Does.Contain("_visualSyncDispatcherActive || !_fallbackShaderGlobalsDirty"));
            Assert.That(flushBlock, Does.Contain("_fallbackShaderGlobalsDirty = false;"));
        }

        [Test]
        public void WaterOpticsTelemetryMarkerIsDevOnlyAndOffByDefault()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Rendering/WaterOptics/HectonWaterOpticsTelemetryFeature.cs");
            string installerPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Rendering/WaterOptics/Editor/WaterOpticsRendererFeatureInstaller.cs");
            string source = File.ReadAllText(path);
            string installerSource = File.ReadAllText(installerPath);
            string recordBlock = ExtractMethodBlock(source, "public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)");
            string addPassesBlock = ExtractMethodBlock(source, "public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)");
            string markerGateBlock = ExtractMethodBlock(source, "private static bool IsTelemetryMarkerAllowed(FeatureSettings settings)");
            string verifySettingsBlock = ExtractMethodBlock(installerSource, "private static bool VerifyFeatureSettings(HectonWaterOpticsTelemetryFeature feature)");
            string ensureSettingsBlock = ExtractMethodBlock(installerSource, "private static bool EnsureFeatureSettings(HectonWaterOpticsTelemetryFeature feature)");

            Assert.That(source, Does.Contain("public bool enableCommandBufferMarker = false;"));
            Assert.That(recordBlock, Does.Contain("!IsTelemetryMarkerAllowed(_settings)"));
            Assert.That(addPassesBlock, Does.Contain("!IsTelemetryMarkerAllowed(settings)"));
            Assert.That(markerGateBlock, Does.Contain("#if UNITY_EDITOR || DEVELOPMENT_BUILD"));
            Assert.That(markerGateBlock, Does.Contain("return settings != null && settings.enableCommandBufferMarker;"));
            Assert.That(markerGateBlock, Does.Contain("return false;"));
            Assert.That(verifySettingsBlock, Does.Contain("!markerProperty.boolValue"));
            Assert.That(ensureSettingsBlock, Does.Contain("EnsureBool(serializedFeature.FindProperty(\"settings.enableCommandBufferMarker\"), false)"));
            Assert.That(installerSource, Does.Not.Contain("EnsureBool(serializedFeature.FindProperty(\"settings.enableCommandBufferMarker\"), true)"));
            Assert.That(source, Does.Contain("builder.AllowPassCulling(false);"));
        }

        [Test]
        public void AbyssalCausticsPublicReadAccessorsUseReadOnlyVaultViews()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Rendering/AbyssalCaustics/AbyssalDeferredCausticsRuntime.cs");
            string source = File.ReadAllText(path);
            string parametersBlock = ExtractMethodBlock(source, "public static bool TryGetActiveParameters(out CausticsParametersDTO parameters)");
            string tuningBlock = ExtractMethodBlock(source, "public static bool TryGetTuning(out CausticsTuningDTO tuning)");
            string readOnlyBlock = ExtractMethodBlock(source, "private bool TryReadOnlyVaultBuffer<T>(");
            string externalInputBlock = ExtractMethodBlock(source, "private bool RefreshExternalInputHandle<T>(");

            Assert.That(parametersBlock, Does.Contain("TryReadOnlyVaultBuffer("));
            Assert.That(tuningBlock, Does.Contain("TryReadOnlyVaultBuffer("));
            Assert.That(parametersBlock, Does.Not.Contain("TryResolveVaultBuffer("));
            Assert.That(tuningBlock, Does.Not.Contain("TryResolveVaultBuffer("));
            Assert.That(readOnlyBlock, Does.Contain("vault.TryReadOnlyHandle(in handle, out buffer)"));
            Assert.That(externalInputBlock, Does.Contain("vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly buffer)"));
            Assert.That(externalInputBlock, Does.Contain("vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly refreshedBuffer)"));
        }

        private static string ExtractMethodBlock(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(signatureIndex, 0, "Missing method signature: " + signature);

            int openBrace = source.IndexOf('{', signatureIndex);
            Assert.GreaterOrEqual(openBrace, 0, "Missing method body: " + signature);

            int depth = 0;
            for (int i = openBrace; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(openBrace, i - openBrace + 1);
                }
            }

            Assert.Fail("Unclosed method body: " + signature);
            return string.Empty;
        }
    }
}
