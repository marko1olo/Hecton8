using System;
using System.IO;
using Hecton8.VFX;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class GraphicsScalability14GRPEditTests
    {
        [Test]
        public void VisorHud_BiosFontDrainUsesCachedMaterial()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Visor/VisorHUDController.cs");
            string source = File.ReadAllText(path);
            string queueBody = ExtractMethodBlock(source, "private void QueueBiosFontSwap(TMP_FontAsset targetFont, Material targetMaterial)");
            string drainBody = ExtractMethodBlock(source, "private void DrainBiosFontSwapQueue()");
            string lateFrameBody = ExtractMethodBlock(source, "public void LateFrameTick()");
            string updateBody = ExtractMethodBlock(source, "private void UpdateBiosFontSwapState()");

            Assert.That(queueBody, Does.Contain("_queuedHudFontMaterial = targetMaterial;"));
            Assert.That(drainBody, Does.Contain("_biosFontSwapScheduler.DrainTick(_queuedHudFont, _queuedHudFontMaterial);"));
            Assert.That(drainBody, Does.Not.Contain("_queuedHudFont.material"));
            Assert.That(lateFrameBody, Does.Not.Contain(".material"));
            Assert.That(updateBody, Does.Not.Contain("PrewarmBiosTerminalFont("));
            Assert.That(updateBody, Does.Not.Contain("ResolvePrimaryHudFont("));
            Assert.That(updateBody, Does.Not.Contain(".material"));
        }

        [Test]
        public void VisorHud_DoesNotUseLegacyCameraCommandBuffers()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Visor/VisorHUDController.cs");
            string source = File.ReadAllText(path);
            string configureBody = ExtractMethodBlock(source, "private void ConfigureHudScissorCommandBuffers()");
            string coldBody = ExtractMethodBlock(source, "private void EnsureHudScissorCommandBuffersCold()");
            string releaseBody = ExtractMethodBlock(source, "private void ReleaseHudScissorCommandBuffers()");

            Assert.That(source, Does.Not.Contain("AddCommandBuffer("));
            Assert.That(source, Does.Not.Contain("RemoveCommandBuffer("));
            Assert.That(source, Does.Not.Contain("new CommandBuffer"));
            Assert.That(configureBody, Does.Contain("ClearHudScissorCommandBufferState();"));
            Assert.That(coldBody, Does.Not.Contain("new CommandBuffer"));
            Assert.That(releaseBody, Does.Not.Contain("CameraEvent"));
        }

        [Test]
        public void GlobalShaderDispatcher_PublishesGlobalsWithoutCommandBuffer()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Rendering/GlobalShaderDispatcher.cs");
            string source = File.ReadAllText(path);
            string lateFrameBody = ExtractMethodBlock(source, "public void LateFrameTick()");
            string dispatchBody = ExtractMethodBlock(source, "private void ExecuteGlobalDispatch(");

            Assert.That(source, Does.Not.Contain("using UnityEngine.Rendering;"));
            Assert.That(source, Does.Not.Contain("CommandBuffer"));
            Assert.That(source, Does.Not.Contain("ExecuteCommandBuffer"));
            Assert.That(source, Does.Not.Contain("TryEnsureCommandBuffer"));
            Assert.That(source, Does.Not.Contain("HasCommandBufferReady"));
            Assert.That(lateFrameBody, Does.Contain("HectonShaderGlobalDataVaultBridge.SetVisualSyncDispatcherActive(false);"));
            Assert.That(lateFrameBody, Does.Contain("ExecuteGlobalDispatch("));
            Assert.That(dispatchBody, Does.Contain("Shader.SetGlobalVector(_FogColorId, fogColor);"));
            Assert.That(dispatchBody, Does.Contain("Shader.SetGlobalBuffer(_ThermalAnomaliesId, _thermalAnomalyBuffer);"));
            Assert.That(dispatchBody, Does.Contain("HectonShaderGlobalDataVaultBridge.SetVisualSyncDispatcherActive(true);"));
        }

        [Test]
        public void ThermalDrsTelemetry_ReleasesTelemetryGuardBeforeInvalidStateReset()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs");
            string source = File.ReadAllText(path);
            string writeTelemetryBody = ExtractMethodBlock(source, "private bool WriteTelemetry(byte flags)");
            string recoverBody = ExtractMethodBlock(source, "private bool RecoverInvalidScaleState()");

            Assert.That(writeTelemetryBody, Does.Contain("finally"));
            Assert.That(writeTelemetryBody, Does.Contain("ReleaseTelemetryPointer();"));
            Assert.That(writeTelemetryBody, Does.Contain("DumpBlackBoxOnce();"));
            Assert.That(writeTelemetryBody, Does.Contain("ResetInvalidScaleStateAndCommit();"));
            Assert.That(writeTelemetryBody, Does.Not.Contain("DumpBlackBoxOnceLocked(telemetryRing, telemetryLength);"));
            AssertOrder(writeTelemetryBody, "ReleaseTelemetryPointer();", "DumpBlackBoxOnce();");
            AssertOrder(writeTelemetryBody, "ReleaseTelemetryPointer();", "ResetInvalidScaleStateAndCommit();");
            Assert.That(recoverBody, Does.Contain("bool resetByTelemetry = WriteTelemetry(FlagInvalidState);"));
            Assert.That(recoverBody, Does.Contain("if (!resetByTelemetry)"));
        }

        [Test]
        public void FontStreamingManager_LateFrameFontSwapUsesCachedMaterials()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/UI/FontStreamingManager.cs");
            string source = File.ReadAllText(path);
            string lateFrameBody = ExtractMethodBlock(source, "public void LateFrameTick()");
            string evaluateBody = ExtractMethodBlock(source, "private void EvaluatePendingFontReadiness()");
            string beginBody = ExtractMethodBlock(source, "private void BeginSwapQueue(TMP_FontAsset targetFont, Material targetFontMaterial, bool biosFallbackActive)");
            string cacheBody = ExtractMethodBlock(source, "private static Material ResolveFontMaterialCold(TMP_FontAsset font)");

            Assert.That(source, Does.Contain("private Material _primaryFontMaterial;"));
            Assert.That(source, Does.Contain("private Material _biosFallbackFontMaterial;"));
            Assert.That(evaluateBody, Does.Contain("IsCachedFontReady(_primaryFont, _primaryFontMaterial)"));
            Assert.That(evaluateBody, Does.Contain("IsCachedFontReady(_biosFallbackFont, _biosFallbackFontMaterial)"));
            Assert.That(evaluateBody, Does.Contain("BeginSwapQueue(_primaryFont, _primaryFontMaterial, biosFallbackActive: false);"));
            Assert.That(evaluateBody, Does.Contain("BeginSwapQueue(_biosFallbackFont, _biosFallbackFontMaterial, biosFallbackActive: true);"));
            Assert.That(evaluateBody, Does.Not.Contain("LocalizedFontResolver.IsFontReady"));
            Assert.That(beginBody, Does.Contain("_targetFontMaterial = targetFontMaterial;"));
            Assert.That(beginBody, Does.Not.Contain(".material"));
            Assert.That(evaluateBody, Does.Not.Contain(".material"));
            Assert.That(lateFrameBody, Does.Not.Contain(".material"));
            Assert.That(cacheBody, Does.Contain("font.material"));
        }

        [Test]
        public void VfxParticleBudget_NonCriticalKillSwitchKeepsSurvivalFloor()
        {
            int bubbleCount = VfxComputeParticleBudgetCatalog.ApplyKillSwitchCount(
                VfxComputeParticleBudgetCatalog.MinimumQualityBubbleCount,
                VFXEmissionProfile.FluidType.Bubble,
                VfxComputeParticleBudgetCatalog.NonCriticalVfxMask,
                3);
            int debrisCount = VfxComputeParticleBudgetCatalog.ApplyKillSwitchCount(
                VfxComputeParticleBudgetCatalog.MinimumQualityDebrisCount,
                VFXEmissionProfile.FluidType.Debris,
                VfxComputeParticleBudgetCatalog.NonCriticalVfxMask,
                3);
            int snowCount = VfxComputeParticleBudgetCatalog.ApplyKillSwitchCount(
                VfxComputeParticleBudgetCatalog.MinimumQualityMarineSnowCount,
                VFXEmissionProfile.FluidType.Snow,
                VfxComputeParticleBudgetCatalog.NonCriticalVfxMask,
                3);
            int ungatedBubbleCount = VfxComputeParticleBudgetCatalog.ApplyKillSwitchCount(
                VfxComputeParticleBudgetCatalog.MinimumQualityBubbleCount,
                VFXEmissionProfile.FluidType.Bubble,
                0UL,
                3);

            Assert.That(bubbleCount, Is.GreaterThanOrEqualTo(VfxComputeParticleBudgetCatalog.EmergencyBubbleSurvivalCount));
            Assert.That(bubbleCount, Is.LessThan(VfxComputeParticleBudgetCatalog.MinimumQualityBubbleCount));
            Assert.That(debrisCount, Is.GreaterThanOrEqualTo(VfxComputeParticleBudgetCatalog.EmergencyDebrisSurvivalCount));
            Assert.That(debrisCount, Is.LessThan(VfxComputeParticleBudgetCatalog.MinimumQualityDebrisCount));
            Assert.That(snowCount, Is.EqualTo(VfxComputeParticleBudgetCatalog.MinimumQualityMarineSnowCount * VfxComputeParticleBudgetCatalog.EmergencyMarineSnowMultiplierPermille / 1000));
            Assert.That(ungatedBubbleCount, Is.EqualTo(VfxComputeParticleBudgetCatalog.MinimumQualityBubbleCount));
        }

        [Test]
        public void MarineSnowPolicyMasks_CompressInsteadOfBinaryDisable()
        {
            float advectionWeight = VfxComputeParticleBudgetCatalog.ResolvePolicyQualityWeight(
                VfxComputeParticleBudgetCatalog.ParticleAdvectionMask,
                VfxComputeParticleBudgetCatalog.ParticleAdvectionMask,
                (byte)3,
                VfxComputeParticleBudgetCatalog.MaskedParticleAdvectionWeightFloor);
            float occlusionWeight = VfxComputeParticleBudgetCatalog.ResolvePolicyQualityWeight(
                VfxComputeParticleBudgetCatalog.VolumetricFogHighResMask,
                VfxComputeParticleBudgetCatalog.VolumetricFogHighResMask,
                (byte)2,
                VfxComputeParticleBudgetCatalog.MaskedVolumetricQualityWeightFloor);
            float unmaskedWeight = VfxComputeParticleBudgetCatalog.ResolvePolicyQualityWeight(
                0UL,
                VfxComputeParticleBudgetCatalog.ParticleAdvectionMask,
                (byte)3,
                VfxComputeParticleBudgetCatalog.MaskedParticleAdvectionWeightFloor);
            int maskedFlowCadence = VfxComputeParticleBudgetCatalog.ResolvePolicyFlowResampleFrames(
                VfxComputeParticleBudgetCatalog.MinimumQualityFlowResampleFrames,
                VfxComputeParticleBudgetCatalog.ParticleAdvectionMask,
                (byte)3);
            int unmaskedFlowCadence = VfxComputeParticleBudgetCatalog.ResolvePolicyFlowResampleFrames(
                VfxComputeParticleBudgetCatalog.MinimumQualityFlowResampleFrames,
                0UL,
                (byte)3);
            int maskedShadowTaps = VfxComputeParticleBudgetCatalog.ResolvePolicyShadowTaps(
                VfxComputeParticleBudgetCatalog.OverkillQualityShadowTaps,
                VfxComputeParticleBudgetCatalog.VolumetricFogHighResMask,
                (byte)1);
            int emergencyShadowTaps = VfxComputeParticleBudgetCatalog.ResolvePolicyShadowTaps(
                VfxComputeParticleBudgetCatalog.OverkillQualityShadowTaps,
                VfxComputeParticleBudgetCatalog.VolumetricFogHighResMask,
                (byte)3);
            int unmaskedShadowTaps = VfxComputeParticleBudgetCatalog.ResolvePolicyShadowTaps(
                VfxComputeParticleBudgetCatalog.OverkillQualityShadowTaps,
                0UL,
                (byte)3);

            Assert.That(advectionWeight, Is.GreaterThanOrEqualTo(VfxComputeParticleBudgetCatalog.MaskedParticleAdvectionWeightFloor));
            Assert.That(advectionWeight, Is.LessThan(1f));
            Assert.That(occlusionWeight, Is.GreaterThan(VfxComputeParticleBudgetCatalog.MaskedVolumetricQualityWeightFloor));
            Assert.That(occlusionWeight, Is.LessThan(1f));
            Assert.That(unmaskedWeight, Is.EqualTo(1f));
            Assert.That(maskedFlowCadence, Is.GreaterThan(0));
            Assert.That(maskedFlowCadence, Is.LessThanOrEqualTo(VfxComputeParticleBudgetCatalog.EmergencyFlowResampleFrames));
            Assert.That(unmaskedFlowCadence, Is.EqualTo(VfxComputeParticleBudgetCatalog.MinimumQualityFlowResampleFrames));
            Assert.That(maskedShadowTaps, Is.GreaterThan(VfxComputeParticleBudgetCatalog.MiddleQualityShadowTaps));
            Assert.That(maskedShadowTaps, Is.LessThan(VfxComputeParticleBudgetCatalog.OverkillQualityShadowTaps));
            Assert.That(emergencyShadowTaps, Is.EqualTo(VfxComputeParticleBudgetCatalog.MinimumQualityShadowTaps));
            Assert.That(unmaskedShadowTaps, Is.EqualTo(VfxComputeParticleBudgetCatalog.OverkillQualityShadowTaps));
        }

        [Test]
        public void MarineSnowRenderer_UsesContinuousPolicyWeightsForFlowAndCollision()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/VFX/HectonMarineSnowRenderer.cs");
            string source = File.ReadAllText(path);
            string scalabilityBody = ExtractMethodBlock(source, "private static Vector4 BuildContinuousScalabilityParams(");
            string budgetBody = ExtractMethodBlock(source, "private static VfxComputeParticleBudget BuildContinuousPressureBudget(");

            Assert.That(scalabilityBody, Does.Contain("VfxComputeParticleBudgetCatalog.ResolvePolicyQualityWeight("));
            Assert.That(scalabilityBody, Does.Contain("MaskedParticleAdvectionWeightFloor"));
            Assert.That(scalabilityBody, Does.Contain("MaskedVolumetricQualityWeightFloor"));
            Assert.That(scalabilityBody, Does.Not.Contain("? 0f : 1f"));
            Assert.That(budgetBody, Does.Contain("VfxComputeParticleBudgetCatalog.ResolvePolicyFlowResampleFrames("));
            Assert.That(budgetBody, Does.Contain("VfxComputeParticleBudgetCatalog.ResolvePolicyShadowTaps("));
            Assert.That(budgetBody, Does.Not.Contain("flowResampleFrames = 0;"));
            Assert.That(budgetBody, Does.Not.Contain("shadowTaps = math.min(shadowTaps, VfxComputeParticleBudgetCatalog.MiddleQualityShadowTaps);"));
        }

        [Test]
        public void NativeTrailRenderer_DrawsSingleMeshWithoutInstancingPayload()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/VFX/NativeTrailRenderer.cs");
            string source = File.ReadAllText(path);
            string renderBody = ExtractMethodBlock(source, "public void Render(float deltaTime)");

            Assert.That(source, Does.Not.Contain("_drawMatrices"));
            Assert.That(source, Does.Not.Contain("DrawMeshInstanced("));
            Assert.That(renderBody, Does.Contain("UnityEngine.Graphics.DrawMesh("));
            Assert.That(renderBody, Does.Contain("Matrix4x4.identity"));
        }

        [Test]
        public void PdaDataLogHologram_DrawsSingleMeshWithoutInstancingPayload()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/UI/PDADataLogTab.cs");
            string source = File.ReadAllText(path);
            string renderBody = ExtractMethodBlock(source, "private void RenderSelectedLoreHologram(float deltaTime)");

            Assert.That(source, Does.Not.Contain("_hologramMatrices"));
            Assert.That(renderBody, Does.Not.Contain("DrawMeshInstanced("));
            Assert.That(renderBody, Does.Contain("UnityEngine.Graphics.DrawMesh("));
            Assert.That(renderBody, Does.Contain("UnityEngine.Rendering.LightProbeUsage.Off"));
        }

        [Test]
        public void Fabricator_SelectedRecipeHologramDrawsSingleMeshWithoutInstancingPayload()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/HectonFabricatorUI.cs");
            string source = File.ReadAllText(path);
            string activeBody = ExtractMethodBlock(source, "private void RenderActiveRecipeHologram(float deltaTime)");
            string selectedBody = ExtractMethodBlock(source, "private void RenderSelectedRecipeHologram(RecipeData recipe, float deltaTime)");

            Assert.That(source, Does.Not.Contain("_selectedRecipeHologramBuffer"));
            Assert.That(activeBody, Does.Contain("UnityEngine.Graphics.DrawMeshInstanced("));
            Assert.That(activeBody, Does.Contain("_hologramMatrixBuffer"));
            Assert.That(selectedBody, Does.Not.Contain("DrawMeshInstanced("));
            Assert.That(selectedBody, Does.Contain("Matrix4x4 previewUnityMatrix = ToMatrix4x4(in previewMatrix);"));
            Assert.That(selectedBody, Does.Contain("UnityEngine.Graphics.DrawMesh("));
            Assert.That(selectedBody, Does.Contain("LightProbeUsage.Off"));
        }

        [Test]
        public void SuitHud_ScannerFlatHologramHasNoDeadMeshPayload()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs");
            string source = File.ReadAllText(path);
            string awakeBody = ExtractMethodBlock(source, "private void Awake()");
            string ensureBody = ExtractMethodBlock(source, "private void EnsureScannerHologramRuntimeResources()");
            string renderBody = ExtractMethodBlock(source, "private void RenderScannerHologram(float deltaTime)");

            Assert.That(source, Does.Not.Contain("_scannerHologramMatrices"));
            Assert.That(source, Does.Not.Contain("_scannerHologramPropertyBlock"));
            Assert.That(source, Does.Not.Contain("_scannerHologramMaterial"));
            Assert.That(source, Does.Not.Contain("_scannerHologramFallbackMesh"));
            Assert.That(awakeBody, Does.Not.Contain("MaterialPropertyBlock"));
            Assert.That(ensureBody, Does.Contain("Flat canvas scanner fake has no runtime mesh, material, or TRS buffer."));
            Assert.That(renderBody, Does.Not.Contain("Graphics."));
            Assert.That(renderBody, Does.Contain("_scannerFlatHologramRoot.sizeDelta"));
        }

        [Test]
        public void AbyssalFluidDecals_FallbackAndPressureSprayScaleContinuously()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/World/AbyssalFluidDecalManager.cs");
            string source = File.ReadAllText(path);
            string advanceBody = ExtractMethodBlock(source, "private void AdvanceFluidDecals(float dt)");
            string drawBody = ExtractMethodBlock(source, "private void DrawActiveDecals()");
            string copyBody = ExtractMethodBlock(source, "internal int CopyScreenSpaceDecals(");
            string pressureTickBody = ExtractMethodBlock(source, "private void TickPressureSprays(float deltaTime, Vector3 driftDelta)");
            string appendBody = ExtractMethodBlock(source, "private void AppendPressureSprayMatrix(");
            string drawLimitBody = ExtractMethodBlock(source, "internal static int ResolvePressureSprayDrawLimit(");

            Assert.That(source, Does.Contain("private const int ScreenSpaceConsumerGraceFrames = 2;"));
            Assert.That(source, Does.Contain("private bool _screenSpaceDecalConsumerSeen;"));
            Assert.That(source, Does.Contain("private int _lastScreenSpaceDecalCopyFrame;"));
            Assert.That(advanceBody, Does.Contain("ShouldDrawMeshFluidDecals()"));
            Assert.That(drawBody, Does.Contain("!ShouldDrawMeshFluidDecals()"));
            Assert.That(drawBody, Does.Not.Contain("|| screenSpaceFluidDecals"));
            Assert.That(copyBody, Does.Contain("_screenSpaceDecalConsumerSeen = true;"));
            Assert.That(copyBody, Does.Contain("_lastScreenSpaceDecalCopyFrame = Time.frameCount;"));
            Assert.That(pressureTickBody, Does.Contain("ResolvePressureSprayDrawLimit("));
            Assert.That(pressureTickBody, Does.Contain("HomeostasisBrain.GlobalQualityWeight"));
            Assert.That(pressureTickBody, Does.Contain("HomeostasisBrain.PressureLevel"));
            Assert.That(appendBody, Does.Contain("matrixCount >= drawLimit"));
            Assert.That(drawLimitBody, Does.Contain("MinimumPressureSprayDrawFraction"));
            Assert.That(drawLimitBody, Does.Contain("math.ceil(safeCapacity * drawFraction)"));
            Assert.That(drawLimitBody, Does.Contain("return math.clamp((int)math.ceil(safeCapacity * drawFraction), 1, safeCapacity);"));
        }

        [Test]
        public void BiolumBlackBoxDump_ReleasesRingGuardBeforeScratchGuard()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs");
            string source = File.ReadAllText(path);
            string dumpBody = ExtractMethodBlock(source, "private void DumpBlackBox(byte reason)");
            string copyBody = ExtractMethodBlock(source, "private bool CopyBlackBoxDumpSnapshot()");
            string writeBody = ExtractMethodBlock(source, "private bool WriteBlackBoxDumpSnapshotToScratch(byte reason)");
            string queueBody = ExtractMethodBlock(source, "private bool QueueBlackBoxDumpWrite()");
            string ensureWorkerBody = ExtractMethodBlock(source, "private void EnsureBlackBoxDumpWorker()");
            string stopWorkerBody = ExtractMethodBlock(source, "private bool StopBlackBoxDumpWorker()");
            string joinWorkerBody = ExtractMethodBlock(source, "private static bool TryJoinBlackBoxDumpWorkerNoThrow");
            string signalWorkerBody = ExtractMethodBlock(source, "private static bool SignalBlackBoxDumpWorkerNoThrow");
            string disposeSignalBody = ExtractMethodBlock(source, "private static void DisposeBlackBoxDumpSignalNoThrow");

            Assert.That(source, Does.Contain("private NativeArray<BiolumPulseTelemetryEntry> _blackBoxDumpSnapshot;"));
            Assert.That(source, Does.Contain("private bool EnsureBlackBoxDumpSnapshot()"));
            Assert.That(source, Does.Contain("private void DisposeBlackBoxDumpSnapshot()"));
            Assert.That(source, Does.Contain("private const int BlackBoxDumpWorkerJoinMilliseconds = 1000;"));
            Assert.That(dumpBody, Does.Contain("CopyBlackBoxDumpSnapshot()"));
            Assert.That(dumpBody, Does.Contain("WriteBlackBoxDumpSnapshotToScratch(reason)"));
            Assert.That(dumpBody, Does.Not.Contain("TryAcquireBlackBoxBuffer"));
            Assert.That(copyBody, Does.Contain("TryAcquireBlackBoxBuffer(out IDataVault vault, out NativeArray<BiolumPulseTelemetryEntry> blackBox)"));
            Assert.That(copyBody, Does.Contain("ReleaseBiolumGuard(vault, BlackBoxGuardMask);"));
            Assert.That(copyBody, Does.Not.Contain("BlackBoxDumpScratchGuardMask"));
            Assert.That(writeBody, Does.Contain("TryAcquireBiolumGuard(vault, BlackBoxDumpScratchGuardMask)"));
            Assert.That(writeBody, Does.Contain("ReleaseBiolumGuard(vault, BlackBoxDumpScratchGuardMask);"));
            Assert.That(writeBody, Does.Not.Contain("BlackBoxGuardMask"));
            Assert.That(queueBody, Does.Contain("return SignalBlackBoxDumpWorkerNoThrow(signal);"));
            Assert.That(ensureWorkerBody, Does.Contain("DisposeBlackBoxDumpSignalNoThrow(staleSignal);"));
            Assert.That(ensureWorkerBody, Does.Contain("DisposeBlackBoxDumpSignalNoThrow(_blackBoxDumpSignal);"));
            Assert.That(stopWorkerBody, Does.Contain("SignalBlackBoxDumpWorkerNoThrow(signal);"));
            Assert.That(stopWorkerBody, Does.Contain("TryJoinBlackBoxDumpWorkerNoThrow(thread);"));
            Assert.That(stopWorkerBody, Does.Contain("DisposeBlackBoxDumpSignalNoThrow(signal);"));
            Assert.That(joinWorkerBody, Does.Contain("ReferenceEquals(Thread.CurrentThread, thread)"));
            Assert.That(joinWorkerBody, Does.Contain("thread.Join(BlackBoxDumpWorkerJoinMilliseconds);"));
            Assert.That(joinWorkerBody, Does.Contain("return !thread.IsAlive;"));
            Assert.That(joinWorkerBody, Does.Contain("catch (Exception)"));
            Assert.That(signalWorkerBody, Does.Contain("signal.Set();"));
            Assert.That(signalWorkerBody, Does.Contain("catch (Exception)"));
            Assert.That(disposeSignalBody, Does.Contain("signal.Dispose();"));
            Assert.That(disposeSignalBody, Does.Contain("catch (Exception)"));
            Assert.That(source, Does.Not.Contain("signal?.Set();"));
            Assert.That(source, Does.Not.Contain("thread.Join(1000)"));
        }

        [Test]
        public void BiolumCsvBackgroundWatcher_UsesFailClosedNoThrowLifecycle()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs");
            string source = File.ReadAllText(path);
            string ensureBody = ExtractMethodBlock(source, "private void EnsureCsvBackgroundWatcher()");
            string stopBody = ExtractMethodBlock(source, "private void StopCsvBackgroundWatcher()");
            string createBody = ExtractMethodBlock(source, "private FileSystemWatcher TryCreateCsvBackgroundWatcher(string directory)");
            string stopNoThrowBody = ExtractMethodBlock(source, "private void StopCsvBackgroundWatcherNoThrow(FileSystemWatcher watcher)");

            Assert.That(ensureBody, Does.Contain("_csvWatcher = TryCreateCsvBackgroundWatcher(directory);"));
            Assert.That(stopBody, Does.Contain("_csvWatcher = null;"));
            Assert.That(stopBody, Does.Contain("StopCsvBackgroundWatcherNoThrow(watcher);"));
            Assert.That(createBody, Does.Contain("Directory.CreateDirectory(directory);"));
            Assert.That(createBody, Does.Contain("watcher.EnableRaisingEvents = true;"));
            Assert.That(createBody, Does.Contain("return watcher;"));
            Assert.That(createBody, Does.Contain("catch (Exception)"));
            Assert.That(createBody, Does.Contain("return null;"));
            Assert.That(stopNoThrowBody, Does.Contain("watcher.EnableRaisingEvents = false;"));
            Assert.That(stopNoThrowBody, Does.Contain("watcher.Changed -= OnCsvFileChanged;"));
            Assert.That(stopNoThrowBody, Does.Contain("watcher.Dispose();"));
            Assert.That(stopNoThrowBody, Does.Contain("catch (Exception)"));
        }

        [Test]
        public void SuitHud_AcousticRadarVisualSyncUsesCachedMaterialBindingState()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs");
            string source = File.ReadAllText(path);
            string applyBody = ExtractMethodBlock(source, "private void ApplyAcousticRadarVisuals(Color primary, Color warning, float corruptionIntensity)");
            string bindBody = ExtractMethodBlock(source, "private void BindAcousticRadarOverlayMaterial()");
            string disposeBody = ExtractMethodBlock(source, "private void DisposeAcousticRadarRuntimeResources()");

            Assert.That(source, Does.Contain("private bool _acousticRadarOverlayMaterialBound;"));
            Assert.That(applyBody, Does.Contain("BindAcousticRadarOverlayMaterial();"));
            Assert.That(applyBody, Does.Not.Contain(".material"));
            Assert.That(bindBody, Does.Contain("_acousticRadarOverlayMaterialBound"));
            Assert.That(bindBody, Does.Contain("_acousticRadarOverlay.material = _acousticRadarMaterial;"));
            Assert.That(bindBody, Does.Not.Contain(".material !="));
            Assert.That(bindBody, Does.Not.Contain(".material =="));
            Assert.That(disposeBody, Does.Contain("_acousticRadarOverlayMaterialBound = false;"));
            Assert.That(disposeBody, Does.Not.Contain(".material =="));
        }

        [Test]
        public void SuitHud_SavingProgressPulseUsesCachedMaterialBindingState()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs");
            string source = File.ReadAllText(path);
            string ensureBody = ExtractMethodBlock(source, "private void EnsureSavingProgressPulseRuntimeResources()");
            string bindBody = ExtractMethodBlock(source, "private void BindSavingProgressPulseMaterials()");
            string disposeBody = ExtractMethodBlock(source, "private void DisposeSavingProgressPulseRuntimeResources()");
            string buildBody = ExtractMethodBlock(source, "private void BuildSavingProgressHierarchy(RectTransform parent)");

            Assert.That(source, Does.Contain("private bool _savingProgressDataLampPulseMaterialBound;"));
            Assert.That(source, Does.Contain("private bool _savingProgressDataNeedlePulseMaterialBound;"));
            Assert.That(ensureBody, Does.Contain("BindSavingProgressPulseMaterials();"));
            Assert.That(ensureBody, Does.Not.Contain(".material !="));
            Assert.That(bindBody, Does.Contain("_savingProgressDataLampPulseMaterialBound"));
            Assert.That(bindBody, Does.Contain("_savingProgressDataNeedlePulseMaterialBound"));
            Assert.That(bindBody, Does.Contain("_savingProgressDataLamp.material = _savingProgressDataPulseMaterial;"));
            Assert.That(bindBody, Does.Contain("_savingProgressDataNeedle.material = _savingProgressDataPulseMaterial;"));
            Assert.That(bindBody, Does.Not.Contain(".material !="));
            Assert.That(bindBody, Does.Not.Contain(".material =="));
            Assert.That(disposeBody, Does.Contain("_savingProgressDataLampPulseMaterialBound = false;"));
            Assert.That(disposeBody, Does.Contain("_savingProgressDataNeedlePulseMaterialBound = false;"));
            Assert.That(disposeBody, Does.Not.Contain(".material =="));
            Assert.That(buildBody, Does.Contain("_savingProgressDataLampPulseMaterialBound = false;"));
            Assert.That(buildBody, Does.Contain("_savingProgressDataNeedlePulseMaterialBound = false;"));
        }

        [Test]
        public void SuitHud_DitheredBackgroundBindingDoesNotReadGraphicMaterial()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs");
            string source = File.ReadAllText(path);
            string applyBody = ExtractMethodBlock(source, "private void ApplyDitheredBackgroundMaterial(Graphic image)");

            Assert.That(applyBody, Does.Contain("EnsureDitheredUiBackgroundRuntimeResources();"));
            Assert.That(applyBody, Does.Contain("image.material = _ditheredUiBackgroundMaterial;"));
            Assert.That(applyBody, Does.Not.Contain("image.material !="));
            Assert.That(applyBody, Does.Not.Contain("image.material =="));
        }

        [Test]
        public void RadarPresentation_InvalidQualityFallsBackToMinimumCapacity()
        {
            string fakeRadarPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/UI/FakeRadarBlipController.cs");
            string acousticRadarPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/UI/AcousticRadarSphereRenderer.cs");
            string fakeRadarSource = File.ReadAllText(fakeRadarPath);
            string acousticRadarSource = File.ReadAllText(acousticRadarPath);
            string fakeRefreshBody = ExtractMethodBlock(fakeRadarSource, "private void RefreshQualityPolicy()");
            string fakeSanitizeBody = ExtractMethodBlock(fakeRadarSource, "private static float SanitizeQualityWeight01(float value)");
            string acousticRefreshBody = ExtractMethodBlock(acousticRadarSource, "private void RefreshQualityPolicy()");
            string acousticSanitizeBody = ExtractMethodBlock(acousticRadarSource, "private static float SanitizeQualityWeight01(float value)");

            Assert.That(fakeRefreshBody, Does.Contain("SanitizeQualityWeight01(HomeostasisBrain.GlobalQualityWeight)"));
            Assert.That(fakeRefreshBody, Does.Contain("_qualityBlipCapacity = ResolveQualityCapacity(qualityWeight01, MinimumQualityBlipCapacity, MaxBlips);"));
            Assert.That(fakeRefreshBody, Does.Contain("_qualityThermalGhostCapacity = ResolveQualityCapacity(qualityWeight01, 0, ThermalNoiseMaxGhostBlips);"));
            Assert.That(fakeSanitizeBody, Does.Contain("? value : 0f"));
            Assert.That(fakeSanitizeBody, Does.Not.Contain("? value : 1f"));
            Assert.That(acousticRefreshBody, Does.Contain("SanitizeQualityWeight01(HomeostasisBrain.GlobalQualityWeight)"));
            Assert.That(acousticRefreshBody, Does.Contain("_qualityMatrixCapacity = ResolveQualityCapacity(qualityWeight01, MinimumQualityBlipCapacity, MaxBlips);"));
            Assert.That(acousticSanitizeBody, Does.Contain("? value : 0f"));
            Assert.That(acousticSanitizeBody, Does.Not.Contain("? value : 1f"));
        }

        [Test]
        public void FakeRadarPresentation_RejectsNonFiniteAupDistances()
        {
            string fakeRadarPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/UI/FakeRadarBlipController.cs");
            string source = File.ReadAllText(fakeRadarPath);
            string scheduleBody = ExtractMethodBlock(source, "private void ScheduleBlipCull(Camera projectionCamera)");
            string playerAupBody = ExtractMethodBlock(source, "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)");

            Assert.That(scheduleBody, Does.Contain("if (!hitAup.IsFinite())"));
            Assert.That(scheduleBody, Does.Contain("if (!math.all(math.isfinite(enemyDeltaAup)))"));
            Assert.That(scheduleBody, Does.Contain("!math.isfinite(distanceSqr) || distanceSqr <= 0.0001f || distanceSqr > rangeSqr"));
            Assert.That(playerAupBody, Does.Contain("return playerAup.IsFinite();"));
            Assert.That(playerAupBody, Does.Not.Contain("return true;"));
        }

        [Test]
        public void AcousticRadarPresentation_RejectsNonFiniteAupDistances()
        {
            string acousticRadarPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/UI/AcousticRadarSphereRenderer.cs");
            string source = File.ReadAllText(acousticRadarPath);
            string refreshBody = ExtractMethodBlock(source, "private void RefreshMatricesForLateFrame()");
            string listenerAupBody = ExtractMethodBlock(source, "private bool TryResolveListenerAup(Vector3 listenerPosition, out AbsoluteUniversePosition listenerAup)");
            string offsetBody = ExtractMethodBlock(source, "private static AbsoluteUniversePosition OffsetAupLocal(in AbsoluteUniversePosition anchorAup, Vector3 runtimeOffset)");
            string validateBody = ExtractMethodBlock(source, "private void OnValidate()");

            Assert.That(refreshBody, Does.Contain("!IsFinite(anchorPosition)"));
            Assert.That(refreshBody, Does.Contain("ResolveMaxContactDistanceMeters(maxContactDistanceMeters)"));
            Assert.That(refreshBody, Does.Contain("if (!sampleAup.IsFinite())"));
            Assert.That(refreshBody, Does.Contain("!math.isfinite(distanceSq) || distanceSq <= 0.0001f || distanceSq > safeMaxDistanceSq"));
            Assert.That(listenerAupBody, Does.Contain("return listenerAup.IsFinite();"));
            Assert.That(listenerAupBody, Does.Not.Contain("return true;"));
            Assert.That(offsetBody, Does.Contain("if (!anchorAup.IsFinite() || !IsFinite(runtimeOffset))"));
            Assert.That(source, Does.Contain("private static bool IsFinite(Vector3 value)"));
            Assert.That(source, Does.Contain("private static float ResolveMaxContactDistanceMeters(float distanceMeters)"));
            Assert.That(source, Does.Contain("math.isfinite(distanceMeters) ? math.max(1f, distanceMeters) : 1f"));
            Assert.That(validateBody, Does.Contain("maxContactDistanceMeters = ResolveMaxContactDistanceMeters(maxContactDistanceMeters);"));
        }

        [Test]
        public void SonarHoloCompassProjection_RejectsNonFiniteAupDistances()
        {
            string compassPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/UI/SonarHoloCompass.cs");
            string source = File.ReadAllText(compassPath);
            string scheduleBody = ExtractMethodBlock(source, "private void ScheduleProjection(int emitterCount)");
            string viewAupBody = ExtractMethodBlock(source, "private bool TryResolveViewAup(Vector3 viewPosition, out AbsoluteUniversePosition viewAup)");
            string offsetBody = ExtractMethodBlock(source, "private static AbsoluteUniversePosition OffsetAupLocal(in AbsoluteUniversePosition anchorAup, Vector3 runtimeOffset)");

            Assert.That(scheduleBody, Does.Contain("!IsFinite(viewPosition)"));
            Assert.That(scheduleBody, Does.Contain("if (!sampleAup.IsFinite())"));
            Assert.That(scheduleBody, Does.Contain("_projectionInputs[i] = default;"));
            Assert.That(scheduleBody, Does.Contain("!math.all(math.isfinite(listenerRelativePosition))"));
            AssertOrder(scheduleBody, "AbsoluteUniversePosition sampleAup = sample.PositionAup;", "if (!sampleAup.IsFinite())");
            AssertOrder(scheduleBody, "if (!sampleAup.IsFinite())", "sampleAup.ToAbsoluteDouble3()");
            Assert.That(viewAupBody, Does.Contain("return viewAup.IsFinite();"));
            Assert.That(viewAupBody, Does.Not.Contain("return true;"));
            Assert.That(offsetBody, Does.Contain("if (!anchorAup.IsFinite() || !IsFinite(runtimeOffset))"));
            Assert.That(source, Does.Contain("private static bool IsFinite(Vector3 value)"));
        }

        [Test]
        public void ArWaypointRuntimeProjection_RejectsNonFiniteAupMetrics()
        {
            string waypointPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/UI/ARWaypointOverlay.cs");
            string source = File.ReadAllText(waypointPath);
            string collectBody = ExtractMethodBlock(source, "private void CollectRuntimeWaypoints()");
            string nativeCopyBody = ExtractMethodBlock(source, "private int CopyRuntimeTargetsForStencil(NativeArray<StencilTargetSourceDTO> destination, int capacity)");
            string spanCopyBody = ExtractMethodBlock(source, "private int CopyRuntimeTargetsForStencil(Span<StencilTargetSourceDTO> destination, int capacity)");
            string renderBody = ExtractMethodBlock(source, "private void RenderWaypoints()");
            string occlusionBody = ExtractMethodBlock(source, "private void RefreshOcclusionStates()");
            string projectBody = ExtractMethodBlock(source, "private bool TryProjectWaypointOntoHudPlane(");
            string frameBody = ExtractMethodBlock(source, "private WaypointProjectionFrame ResolveWaypointProjectionFrame()");
            string planeBody = ExtractMethodBlock(source, "private static float ResolveHudPlaneDistance(");
            string colorBody = ExtractMethodBlock(source, "private static Color ResolveWaypointColor(Color color)");

            Assert.That(collectBody, Does.Contain("runtimeWaypoint.Color = ResolveWaypointColor(externalWaypoint.Color);"));
            Assert.That(nativeCopyBody, Does.Contain("int writeCount = 0;"));
            Assert.That(nativeCopyBody, Does.Contain("!waypoint.Active || !waypoint.PositionAup.IsFinite()"));
            Assert.That(nativeCopyBody, Does.Contain("destination[writeCount] = new StencilTargetSourceDTO"));
            Assert.That(nativeCopyBody, Does.Contain("return writeCount;"));
            Assert.That(spanCopyBody, Does.Contain("int writeCount = 0;"));
            Assert.That(spanCopyBody, Does.Contain("!waypoint.Active || !waypoint.PositionAup.IsFinite()"));
            Assert.That(spanCopyBody, Does.Contain("destination[writeCount] = new StencilTargetSourceDTO"));
            Assert.That(spanCopyBody, Does.Contain("return writeCount;"));
            Assert.That(renderBody, Does.Contain("!waypoint.Active || !waypoint.PositionAup.IsFinite()"));
            Assert.That(occlusionBody, Does.Contain("!waypoint.PositionAup.IsFinite()"));
            Assert.That(occlusionBody, Does.Contain("!math.all(math.isfinite(delta)) || !math.isfinite(distanceSq)"));
            Assert.That(projectBody, Does.Contain("!waypointAup.IsFinite()"));
            Assert.That(projectBody, Does.Contain("!projectionFrame.CameraAup.IsFinite()"));
            Assert.That(projectBody, Does.Contain("!math.all(math.isfinite(deltaAup))"));
            Assert.That(projectBody, Does.Contain("!math.isfinite(viewDepth)"));
            Assert.That(projectBody, Does.Contain("!math.isfinite(projectedWorldX) || !math.isfinite(projectedWorldY)"));
            Assert.That(projectBody, Does.Contain("!IsFinite(projectedCanvasPosition)"));
            Assert.That(frameBody, Does.Contain("!IsFinite(cameraPosition)"));
            Assert.That(frameBody, Does.Contain("!math.isfinite(planeDistance) || planeDistance <= ProjectionDepthEpsilon"));
            Assert.That(frameBody, Does.Contain("!IsFinite(lossyScale)"));
            Assert.That(planeBody, Does.Contain("!IsFinite(cameraPosition)"));
            Assert.That(planeBody, Does.Contain("!IsFinite(canvasRect.position)"));
            Assert.That(planeBody, Does.Contain("math.isfinite(planeDistance)"));
            Assert.That(colorBody, Does.Contain("!IsFinite(color) || color.a <= 0f"));
            Assert.That(colorBody, Does.Contain("math.saturate(color.r)"));
            Assert.That(source, Does.Contain("private static bool IsFinite(Color color)"));
            Assert.That(source, Does.Contain("private static bool IsFinite(Vector2 value)"));
            Assert.That(source, Does.Contain("private static bool IsFinite(Vector3 value)"));
        }

        [Test]
        public void SubmarineSonarHoloMap_RejectsNonFiniteRuntimePresentation()
        {
            string mapPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/UI/SubmarineSonarHoloMapRenderer.cs");
            string source = File.ReadAllText(mapPath);
            string visualSyncBody = ExtractMethodBlock(source, "private void RunVisualSync(float deltaTime)");
            string lateFrameBody = ExtractMethodBlock(source, "public void LateFrameTick()");
            string refreshBody = ExtractMethodBlock(source, "private void RefreshMapSample(int gridCells)");
            string floorBody = ExtractMethodBlock(source, "private static float ResolveHybridFloorDelta(Vector3 samplePosition, float originY)");
            string interpolationBody = ExtractMethodBlock(source, "private void UploadInterpolatedVertices()");
            string visibleBody = ExtractMethodBlock(source, "private bool ResolveVisibleToPlayer()");
            string qualityBody = ExtractMethodBlock(source, "private void RefreshQualityPolicy()");
            string materialBody = ExtractMethodBlock(source, "private void ApplyMaterialPropertiesIfNeeded()");
            string boundsBody = ExtractMethodBlock(source, "private void RefreshRuntimeMeshBounds()");
            string clearBody = ExtractMethodBlock(source, "private void ClearMapSamples()");
            string validateBody = ExtractMethodBlock(source, "private void OnValidate()");

            Assert.That(visualSyncBody, Does.Contain("math.isfinite(deltaTime) ? math.max(0f, deltaTime) : 0f"));
            Assert.That(visualSyncBody, Does.Contain("SanitizeQualityWeight01(_cachedQualityWeight01)"));
            Assert.That(lateFrameBody, Does.Contain("TryResolveAnchorRenderPose(anchor, out Vector3 anchorPosition, out Quaternion anchorRotation)"));
            Assert.That(lateFrameBody, Does.Contain("Matrix4x4.TRS(anchorPosition, anchorRotation, Vector3.one)"));
            Assert.That(refreshBody, Does.Contain("if (!IsFinite(originPosition) || !IsFinite(originRotation))"));
            Assert.That(refreshBody, Does.Contain("ClearMapSamples();"));
            Assert.That(refreshBody, Does.Contain("ResolveSampleRadiusMeters(sampleRadiusMeters)"));
            Assert.That(refreshBody, Does.Contain("ResolveDisplayRadiusMeters(displayRadiusMeters)"));
            Assert.That(refreshBody, Does.Contain("ResolveMaxHeightDeltaMeters(maxHeightDeltaMeters)"));
            Assert.That(refreshBody, Does.Contain("ResolveVerticalExaggeration(verticalExaggeration)"));
            Assert.That(refreshBody, Does.Contain("math.isfinite(heightDelta)"));
            Assert.That(floorBody, Does.Contain("if (!IsFinite(samplePosition) || !math.isfinite(originY))"));
            Assert.That(floorBody, Does.Contain("math.isfinite(terrainDelta) ? terrainDelta : 0f"));
            Assert.That(floorBody, Does.Contain("math.isfinite(fallbackDelta) ? fallbackDelta : 0f"));
            Assert.That(interpolationBody, Does.Contain("IsFinite(vertex) ? vertex : Vector3.zero"));
            Assert.That(visibleBody, Does.Contain("!IsFinite(cameraPosition)"));
            Assert.That(visibleBody, Does.Contain("!IsFinite(anchor.position)"));
            Assert.That(visibleBody, Does.Contain("!math.isfinite(directionLengthSq)"));
            Assert.That(qualityBody, Does.Contain("SanitizeQualityWeight01(value, _cachedQualityWeight01)"));
            Assert.That(materialBody, Does.Contain("Color safeSonarColor = ResolveSonarColor(sonarColor);"));
            Assert.That(materialBody, Does.Contain("_materialProperties.SetColor(_BaseColorId, safeSonarColor);"));
            Assert.That(boundsBody, Does.Contain("ResolveSampleRadiusMeters(sampleRadiusMeters)"));
            Assert.That(boundsBody, Does.Contain("ResolveDisplayRadiusMeters(displayRadiusMeters)"));
            Assert.That(clearBody, Does.Contain("_hasCurrentSample = false;"));
            Assert.That(clearBody, Does.Contain("_hasPreviousSample = false;"));
            Assert.That(validateBody, Does.Contain("sonarColor = ResolveSonarColor(sonarColor);"));
            Assert.That(source, Does.Contain("private static bool TryResolveAnchorRenderPose(Transform anchor, out Vector3 position, out Quaternion rotation)"));
            Assert.That(source, Does.Contain("private static float SanitizeQualityWeight01(float value, float fallback = 0f)"));
            Assert.That(source, Does.Contain("private static Color ResolveSonarColor(Color color)"));
            Assert.That(source, Does.Contain("private static bool IsFinite(Color color)"));
            Assert.That(source, Does.Contain("private static bool IsFinite(Quaternion rotation)"));
            Assert.That(source, Does.Contain("private static bool IsFinite(Vector3 value)"));
        }

        [Test]
        public void PdaRuntimeAupReadouts_RejectNonFinitePlayerAndMarkerAups()
        {
            string spectrumPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/UI/PDASpectrumTab.cs");
            string mapPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/UI/PDAMapTab.cs");
            string spectrumSource = File.ReadAllText(spectrumPath);
            string mapSource = File.ReadAllText(mapPath);
            string spectrumPlayerAup = ExtractMethodBlock(spectrumSource, "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)");
            string spectrumDistance = ExtractMethodBlock(spectrumSource, "private static int ResolveRoundedApproximateAupDistanceMeters(");
            string mapPlayerAup = ExtractMethodBlock(mapSource, "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)");
            string mapRuntimePosition = ExtractMethodBlock(mapSource, "private static bool TryResolveRuntimePosition(in AbsoluteUniversePosition targetAup, out Vector3 runtimePosition)");
            string markerOverlayDelta = ExtractMethodBlock(mapSource, "private bool TryResolveMarkerOverlayDelta(");
            string playerDepth = ExtractMethodBlock(mapSource, "private float ResolvePlayerDepthMeters()");

            Assert.That(spectrumPlayerAup, Does.Contain("return playerAup.IsFinite();"));
            Assert.That(spectrumPlayerAup, Does.Not.Contain("return true;"));
            Assert.That(spectrumDistance, Does.Contain("if (!fromAup.IsFinite())"));
            Assert.That(mapPlayerAup, Does.Contain("return playerAup.IsFinite();"));
            Assert.That(mapPlayerAup, Does.Not.Contain("return true;"));
            Assert.That(mapRuntimePosition, Does.Contain("if (!targetAup.IsFinite() || !originAup.IsFinite())"));
            AssertOrder(mapRuntimePosition, "if (!targetAup.IsFinite() || !originAup.IsFinite())", "targetAup.ToAbsoluteDouble3()");
            Assert.That(markerOverlayDelta, Does.Contain("if (!markerAup.IsFinite())"));
            Assert.That(markerOverlayDelta, Does.Contain("double.IsNaN(x) || double.IsInfinity(x) || double.IsNaN(z) || double.IsInfinity(z)"));
            Assert.That(playerDepth, Does.Contain("math.isfinite(currentDepthMeters) ? math.max(0f, currentDepthMeters) : 0f"));
            Assert.That(playerDepth, Does.Contain("double.IsNaN(absoluteY) || double.IsInfinity(absoluteY)"));
        }

        [Test]
        public void PdaIntrusionEvents_ReportQueueOverflowDrops()
        {
            string intrusionPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/UI/PDAIntrusionManager.cs");
            string source = File.ReadAllText(intrusionPath);
            string resetBody = ExtractMethodBlock(source, "private static void ResetStaticState()");
            string raiseBody = ExtractMethodBlock(source, "internal static void RaiseRebootCompleted(uint sourceId)");
            string reportBody = ExtractMethodBlock(source, "private static void ReportEventQueueOverflow()");

            Assert.That(source, Does.Contain("private const uint PDAIntrusionEventOverflowWarningHash"));
            Assert.That(source, Does.Contain("private const uint PDAIntrusionEventContextHash"));
            Assert.That(source, Does.Contain("private static int _droppedEventCount;"));
            Assert.That(source, Does.Contain("private static int _lastEventOverflowTelemetryFrame = -1;"));
            Assert.That(source, Does.Contain("public static int DroppedEventCount => _droppedEventCount;"));
            Assert.That(resetBody, Does.Contain("_droppedEventCount = 0;"));
            Assert.That(resetBody, Does.Contain("_lastEventOverflowTelemetryFrame = -1;"));
            Assert.That(raiseBody, Does.Contain("if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)"));
            Assert.That(raiseBody, Does.Contain("ReportEventQueueOverflow();"));
            Assert.That(raiseBody, Does.Contain("if (!_nextFrameEvents.Enqueue(in payload))"));
            Assert.That(raiseBody, Does.Contain("if (!_pendingEvents.Enqueue(in payload))"));
            Assert.AreEqual(3, CountToken(raiseBody, "ReportEventQueueOverflow();"));
            Assert.That(reportBody, Does.Contain("_droppedEventCount++;"));
            Assert.That(reportBody, Does.Contain("if (_lastEventOverflowTelemetryFrame == frame)"));
            Assert.That(reportBody, Does.Contain("_lastEventOverflowTelemetryFrame = frame;"));
            Assert.That(reportBody, Does.Contain("GlobalTelemetryBus.PublishPerformanceWarning("));
            Assert.That(reportBody, Does.Contain("PDAIntrusionEventOverflowWarningHash"));
            Assert.That(reportBody, Does.Contain("PDAIntrusionEventContextHash"));
            Assert.That(reportBody, Does.Contain("Mathf.Max(1, _droppedEventCount)"));
        }

        [Test]
        public void PdaIntrusionManager_SanitizesRuntimeTimersAndDirectorInputs()
        {
            string intrusionPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/UI/PDAIntrusionManager.cs");
            string source = File.ReadAllText(intrusionPath);
            string progressBody = ExtractMethodBlock(source, "public float RebootProgressNormalized");
            string advanceBody = ExtractMethodBlock(source, "private void AdvanceIntrusionPresentationState(float dt)");
            string lateFrameBody = ExtractMethodBlock(source, "public void LateFrameTick()");
            string directorBody = ExtractMethodBlock(source, "private void HandleEquipmentGlitchRequested(float intensity)");
            string ambientBody = ExtractMethodBlock(source, "private void TickAmbientIntrusionThreat(float dt)");
            string abyssalBody = ExtractMethodBlock(source, "private bool ShouldTriggerAbyssalHack(Vector3 origin)");
            string visualBody = ExtractMethodBlock(source, "private void TickVisualCadence(float dt)");
            string rebootBody = ExtractMethodBlock(source, "private void TickRebootHold(float dt)");
            string textDriftBody = ExtractMethodBlock(source, "private void TickTextDrift(float dt)");
            string triggerBody = ExtractMethodBlock(source, "private void TriggerHack()");
            string resolveOwnersBody = ExtractMethodBlock(source, "private void ResolveRuntimeOwners(float dt)");
            string validateBody = ExtractMethodBlock(source, "private void OnValidate()");

            Assert.That(progressBody, Does.Contain("ResolveRebootHoldDurationSeconds(rebootHoldDuration)"));
            Assert.That(progressBody, Does.Contain("SanitizeNonNegativeSeconds(_rebootHoldTimer)"));
            Assert.That(advanceBody, Does.Contain("float safeDeltaTime = SanitizeDeltaTime(dt);"));
            Assert.That(advanceBody, Does.Contain("TickAmbientIntrusionThreat(safeDeltaTime);"));
            Assert.That(advanceBody, Does.Contain("TickVisualCadence(safeDeltaTime);"));
            Assert.That(advanceBody, Does.Contain("TickRebootHold(safeDeltaTime);"));
            Assert.That(lateFrameBody, Does.Contain("float dt = SanitizeDeltaTime(SystemDispatcher.CurrentFrameUnscaledDeltaTime);"));
            Assert.That(directorBody, Does.Contain("!math.isfinite(intensity)"));
            Assert.That(directorBody, Does.Contain("ResolveEquipmentGlitchThreshold01(equipmentGlitchThreshold)"));
            Assert.That(ambientBody, Does.Contain("SanitizeNonNegativeSeconds(_leviathanScanTimer) - SanitizeDeltaTime(dt)"));
            Assert.That(ambientBody, Does.Contain("ResolveLeviathanScanIntervalSeconds(leviathanScanInterval)"));
            Assert.That(ambientBody, Does.Contain("ResolveLeviathanHackRadiusMeters(leviathanHackRadius)"));
            Assert.That(abyssalBody, Does.Contain("math.isfinite(_playerMovement.CurrentHullStress01)"));
            Assert.That(abyssalBody, Does.Contain("return IsFinite(origin) && IsInsideDeadZone(origin);"));
            Assert.That(visualBody, Does.Contain("SanitizeNonNegativeSeconds(_visualPhaseTimer) - SanitizeDeltaTime(dt)"));
            Assert.That(visualBody, Does.Contain("ResolveVisualPhaseDurationSeconds(visualPhaseDuration)"));
            Assert.That(rebootBody, Does.Contain("float safeDeltaTime = SanitizeDeltaTime(dt);"));
            Assert.That(rebootBody, Does.Contain("SanitizeNonNegativeSeconds(_rebootHoldTimer) + safeDeltaTime"));
            Assert.That(rebootBody, Does.Contain("ResolveRebootHoldDurationSeconds(rebootHoldDuration)"));
            Assert.That(textDriftBody, Does.Contain("float safeDeltaTime = SanitizeDeltaTime(dt);"));
            Assert.That(textDriftBody, Does.Contain("math.isfinite(_textDriftRescanTimer)"));
            Assert.That(textDriftBody, Does.Contain("ResolveTextDriftRescanIntervalSeconds(TextDriftRescanInterval)"));
            Assert.That(textDriftBody, Does.Contain("math.isfinite(_textDriftWaveTime)"));
            Assert.That(triggerBody, Does.Contain("ResolveVisualPhaseDurationSeconds(visualPhaseDuration)"));
            Assert.That(resolveOwnersBody, Does.Contain("SanitizeNonNegativeSeconds(_runtimeOwnerResolveRetryTimer)"));
            Assert.That(resolveOwnersBody, Does.Contain("SanitizeDeltaTime(dt)"));
            Assert.That(validateBody, Does.Contain("equipmentGlitchThreshold = ResolveEquipmentGlitchThreshold01(equipmentGlitchThreshold);"));
            Assert.That(validateBody, Does.Contain("leviathanScanInterval = ResolveLeviathanScanIntervalSeconds(leviathanScanInterval);"));
            Assert.That(validateBody, Does.Contain("leviathanHackRadius = ResolveLeviathanHackRadiusMeters(leviathanHackRadius);"));
            Assert.That(validateBody, Does.Contain("visualPhaseDuration = ResolveVisualPhaseDurationSeconds(visualPhaseDuration);"));
            Assert.That(validateBody, Does.Contain("rebootHoldDuration = ResolveRebootHoldDurationSeconds(rebootHoldDuration);"));
            Assert.That(source, Does.Contain("private static float SanitizeDeltaTime(float seconds)"));
            Assert.That(source, Does.Contain("private static float SanitizeNonNegativeSeconds(float seconds)"));
            Assert.That(source, Does.Contain("private static float ResolveEquipmentGlitchThreshold01(float threshold)"));
            Assert.That(source, Does.Contain("private static float ResolveLeviathanScanIntervalSeconds(float intervalSeconds)"));
            Assert.That(source, Does.Contain("private static float ResolveLeviathanHackRadiusMeters(float radiusMeters)"));
            Assert.That(source, Does.Contain("private static bool IsFinite(Vector3 value)"));
        }

        [Test]
        public void AmbientWaterMotionManager_RejectsNonFinitePresentationInputs()
        {
            string motionPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/AmbientWaterMotionManager.cs");
            string source = File.ReadAllText(motionPath);
            string tickBody = ExtractMethodBlock(source, "public void Tick(float deltaTime)");
            string lateFrameBody = ExtractMethodBlock(source, "public void LateFrameTick()");
            string lodBody = ExtractMethodBlock(source, "private static byte ResolveDistanceLodBand(");
            string runtimeWorldBody = ExtractMethodBlock(source, "private static bool TryResolveRuntimeWorldPosition(");
            string runtimePositionBody = ExtractMethodBlock(source, "private static bool TryResolveRuntimePosition(in AbsoluteUniversePosition aup, out Vector3 runtimePosition)");
            string presentationPositionBody = ExtractMethodBlock(source, "private static bool TryResolvePresentationRestWorldPosition(AmbientWaterMotion motion, out Vector3 worldPosition)");
            string applyBody = ExtractMethodBlock(source, "private void ApplyMotion(AmbientWaterMotion motion, Vector3 worldPos)");
            string biomeBlendBody = ExtractMethodBlock(source, "private void UpdateBiomeCurrentBlend(float deltaTime)");
            string biomeTargetBody = ExtractMethodBlock(source, "private static Vector3 ResolveBiomeCurrentTarget(HectonBiomeMatrixProfile profile)");
            string refreshBody = ExtractMethodBlock(source, "private void RefreshDistanceThresholds()");
            string validateBody = ExtractMethodBlock(source, "private void OnValidate()");

            Assert.That(tickBody, Does.Contain("SanitizeDeltaTime(_pendingVisualDeltaTime) + SanitizeDeltaTime(deltaTime)"));
            Assert.That(lateFrameBody, Does.Contain("deltaTime = SanitizeDeltaTime(deltaTime);"));
            Assert.That(lateFrameBody, Does.Contain("_time = AdvanceRuntimeTime(_time, deltaTime);"));
            Assert.That(lateFrameBody, Does.Contain("motion.HasRestAup && motion.RestAup.IsFinite()"));
            Assert.That(lateFrameBody, Does.Contain("TryResolveRuntimeWorldPosition(motion, in motionAup, hasMotionAup, out Vector3 worldPos)"));
            Assert.That(runtimeWorldBody, Does.Contain("TryResolveRuntimePosition(in motionAup, out worldPos)"));
            Assert.That(runtimeWorldBody, Does.Contain("TryResolvePresentationRestWorldPosition(motion, out worldPos)"));
            Assert.That(runtimePositionBody, Does.Contain("if (!aup.IsFinite())"));
            Assert.That(runtimePositionBody, Does.Contain("return IsFinite(runtimePosition);"));
            Assert.That(presentationPositionBody, Does.Contain("return IsFinite(worldPosition);"));
            Assert.That(lodBody, Does.Contain("double.IsNaN(distanceSq) || double.IsInfinity(distanceSq) || distanceSq < 0d"));
            Assert.That(lodBody, Does.Contain("ResolveDistanceLimitSqr(nearSq, 1f)"));
            Assert.That(applyBody, Does.Contain("if (!IsFinite(worldPos))"));
            Assert.That(applyBody, Does.Contain("ResolveMotionCoupling(motion.CurrentCoupling)"));
            Assert.That(applyBody, Does.Contain("volumeCurrent = ClampFiniteVector(volumeCurrent, MaxAmbientMotionCurrentMetersPerSecond);"));
            Assert.That(applyBody, Does.Contain("if (!math.all(math.isfinite(phantomCurrent)))"));
            Assert.That(applyBody, Does.Contain("float time = SanitizeNonNegativeSeconds(_time);"));
            Assert.That(applyBody, Does.Contain("float frequency = ResolveMotionFrequency(motion.BaseFrequency) * ResolveMotionFrequency(globalFrequency);"));
            Assert.That(applyBody, Does.Contain("Vector3 positionalAmplitude = ClampFiniteVector(motion.PositionalAmplitude, MaxAmbientMotionAmplitudeMeters);"));
            Assert.That(applyBody, Does.Contain("if (IsFinite(localPosition))"));
            Assert.That(applyBody, Does.Contain("if (IsFinite(localRotation))"));
            Assert.That(biomeBlendBody, Does.Contain("ClampFiniteVector(_biomeCurrentStartVector, MaxAmbientMotionCurrentMetersPerSecond)"));
            Assert.That(biomeBlendBody, Does.Contain("SanitizeNonNegativeSeconds(_biomeCurrentBlendElapsed) + SanitizeDeltaTime(deltaTime)"));
            Assert.That(biomeTargetBody, Does.Contain("math.isfinite(profile.ambientFlowOverrideWeight) ? profile.ambientFlowOverrideWeight : 0f"));
            Assert.That(biomeTargetBody, Does.Contain("ClampFiniteVector(profile.ambientFlowOverride * weight, MaxAmbientMotionCurrentMetersPerSecond)"));
            Assert.That(refreshBody, Does.Contain("nearDistance = ResolveDistanceMeters(nearDistance, 1f);"));
            Assert.That(validateBody, Does.Contain("globalAmplitude = ResolveMotionAmplitude(globalAmplitude);"));
            Assert.That(validateBody, Does.Contain("globalFrequency = ResolveMotionFrequency(globalFrequency);"));
            Assert.That(source, Does.Contain("private static float SanitizeDeltaTime(float seconds)"));
            Assert.That(source, Does.Contain("private static float SanitizeNonNegativeSeconds(float seconds)"));
            Assert.That(source, Does.Contain("private static float AdvanceRuntimeTime(float currentSeconds, float deltaSeconds)"));
            Assert.That(source, Does.Contain("private static Vector3 ClampFiniteVector(Vector3 value, float maxMagnitude)"));
            Assert.That(source, Does.Contain("private static bool IsFinite(Vector3 value)"));
            Assert.That(source, Does.Contain("private static bool IsFinite(Quaternion rotation)"));
        }

        [Test]
        public void AmbientWaterMotion_AuthoringBridgeSanitizesProfileAndRestPose()
        {
            string motionPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/AmbientWaterMotion.cs");
            string profilePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/AmbientWaterMotionProfile.cs");
            string motionSource = File.ReadAllText(motionPath);
            string profileSource = File.ReadAllText(profilePath);
            string awakeBody = ExtractMethodBlock(motionSource, "private void Awake()");
            string captureBody = ExtractMethodBlock(motionSource, "public void CaptureRestPose()");
            string applyBody = ExtractMethodBlock(motionSource, "public void ApplyProfile()");
            string hotSwapBody = ExtractMethodBlock(motionSource, "public void OnGlobalRegistryServiceReplaced(");
            string rebindBody = ExtractMethodBlock(motionSource, "private void RebindManager(AmbientWaterMotionManager manager)");
            string unregisterBody = ExtractMethodBlock(motionSource, "private void UnregisterFromManager()");
            string registerListenerBody = ExtractMethodBlock(motionSource, "private void TryRegisterHotSwapListener()");
            string unregisterListenerBody = ExtractMethodBlock(motionSource, "private void TryUnregisterHotSwapListener()");
            string sanitizeBody = ExtractMethodBlock(motionSource, "private void SanitizeTuning()");
            string validateBody = ExtractMethodBlock(motionSource, "private void OnValidate()");
            string profileValidateBody = ExtractMethodBlock(profileSource, "private void OnValidate()");

            Assert.That(motionSource, Does.Contain("public sealed class AmbientWaterMotion : MonoBehaviour, IGlobalRegistryHotSwapListener"));
            Assert.That(motionSource, Does.Contain("private AmbientWaterMotionManager _registeredManager;"));
            Assert.That(motionSource, Does.Contain("private bool _hotSwapRegistered;"));
            Assert.That(awakeBody, Does.Contain("SanitizeTuning();"));
            Assert.That(captureBody, Does.Contain("_restLocalPosition = IsFinite(localPosition) ? localPosition : Vector3.zero;"));
            Assert.That(captureBody, Does.Contain("_restLocalRotation = IsFinite(localRotation) ? localRotation : Quaternion.identity;"));
            Assert.That(captureBody, Does.Contain("if (!IsFinite(worldPosition))"));
            Assert.That(captureBody, Does.Contain("_hasRestAup = false;"));
            Assert.That(applyBody, Does.Contain("AmbientWaterMotionProfile.ResolveAmplitude(profile.verticalAmplitude)"));
            Assert.That(applyBody, Does.Contain("AmbientWaterMotionProfile.ResolvePositionalAmplitude(profile.positionalAmplitude)"));
            Assert.That(applyBody, Does.Contain("AmbientWaterMotionProfile.ResolveAngularAmplitude(profile.angularAmplitude)"));
            Assert.That(applyBody, Does.Contain("AmbientWaterMotionProfile.ResolveFrequency(profile.baseFrequency)"));
            Assert.That(applyBody, Does.Contain("AmbientWaterMotionProfile.ResolveCurrentCoupling(profile.currentCoupling)"));
            Assert.That(applyBody, Does.Contain("AmbientWaterMotionProfile.ResolveLodBias(profile.lodBias)"));
            Assert.That(hotSwapBody, Does.Contain("serviceSlot != GlobalRegistryServiceSlot.AmbientWaterMotionRuntime"));
            Assert.That(hotSwapBody, Does.Contain("ReferenceEquals(_registeredManager, previousService)"));
            Assert.That(hotSwapBody, Does.Contain("RebindManager(currentService as AmbientWaterMotionManager);"));
            Assert.That(rebindBody, Does.Contain("UnregisterFromManager();"));
            Assert.That(rebindBody, Does.Contain("manager.Register(this);"));
            Assert.That(rebindBody, Does.Contain("_registeredManager = manager;"));
            Assert.That(unregisterBody, Does.Contain("manager.Unregister(this);"));
            Assert.That(unregisterBody, Does.Contain("_registeredManager = null;"));
            Assert.That(registerListenerBody, Does.Contain("GlobalRegistry.TryRegisterHotSwapListener(this)"));
            Assert.That(unregisterListenerBody, Does.Contain("GlobalRegistry.TryUnregisterHotSwapListener(this);"));
            Assert.That(sanitizeBody, Does.Contain("verticalAmplitude = AmbientWaterMotionProfile.ResolveAmplitude(verticalAmplitude);"));
            Assert.That(validateBody, Does.Contain("SanitizeTuning();"));
            Assert.That(profileValidateBody, Does.Contain("verticalAmplitude = ResolveAmplitude(verticalAmplitude);"));
            Assert.That(profileValidateBody, Does.Contain("positionalAmplitude = ResolvePositionalAmplitude(positionalAmplitude);"));
            Assert.That(profileValidateBody, Does.Contain("angularAmplitude = ResolveAngularAmplitude(angularAmplitude);"));
            Assert.That(profileValidateBody, Does.Contain("baseFrequency = ResolveFrequency(baseFrequency);"));
            Assert.That(profileValidateBody, Does.Contain("currentCoupling = ResolveCurrentCoupling(currentCoupling);"));
            Assert.That(profileValidateBody, Does.Contain("lodBias = ResolveLodBias(lodBias);"));
            Assert.That(motionSource, Does.Contain("private static bool IsFinite(Vector3 value)"));
            Assert.That(motionSource, Does.Contain("private static bool IsFinite(Quaternion rotation)"));
            Assert.That(profileSource, Does.Contain("internal static Vector3 ResolvePositionalAmplitude(Vector3 amplitude)"));
            Assert.That(profileSource, Does.Contain("private static Vector3 ClampFiniteVector(Vector3 value, float maxMagnitude)"));
            Assert.That(profileSource, Does.Contain("private static bool IsFinite(Vector3 value)"));
        }

        [Test]
        public void SuitHud_TextCreationUsesCachedFontSharedMaterial()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs");
            string source = File.ReadAllText(path);
            string createBody = ExtractMethodBlock(source, "private TextMeshProUGUI CreateText(string name, RectTransform parent, float size, FontStyles style, TextAlignmentOptions alignment, float alpha, TMP_FontAsset fontAsset)");
            string resolveBody = ExtractMethodBlock(source, "private Material ResolveFontSharedMaterial(TMP_FontAsset fontAsset)");
            string invalidateBody = ExtractMethodBlock(source, "private void InvalidateVisualCaches()");

            Assert.That(source, Does.Contain("private TMP_FontAsset _cachedFontMaterialAsset0;"));
            Assert.That(source, Does.Contain("private Material _cachedFontSharedMaterial0;"));
            Assert.That(createBody, Does.Contain("TMP_FontAsset resolvedFont"));
            Assert.That(createBody, Does.Contain("ResolveFontSharedMaterial(resolvedFont)"));
            Assert.That(createBody, Does.Not.Contain("label.font.material"));
            Assert.That(resolveBody, Does.Contain("ReferenceEquals(fontAsset, _cachedFontMaterialAsset0)"));
            Assert.That(resolveBody, Does.Contain("ReferenceEquals(fontAsset, _cachedFontMaterialAsset1)"));
            Assert.That(resolveBody, Does.Contain("Material material = fontAsset.material;"));
            Assert.That(invalidateBody, Does.Contain("_cachedFontMaterialAsset0 = null;"));
            Assert.That(invalidateBody, Does.Contain("_cachedFontSharedMaterial1 = null;"));
        }

        private static void AssertOrder(string source, string first, string second)
        {
            int firstIndex = source.IndexOf(first, StringComparison.Ordinal);
            int secondIndex = source.IndexOf(second, StringComparison.Ordinal);
            Assert.GreaterOrEqual(firstIndex, 0, "Missing token: " + first);
            Assert.GreaterOrEqual(secondIndex, 0, "Missing token: " + second);
            Assert.Less(firstIndex, secondIndex, first + " must appear before " + second);
        }

        private static string ExtractMethodBlock(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, "Missing method: " + signature);
            int brace = source.IndexOf('{', start);
            Assert.GreaterOrEqual(brace, 0, "Missing method body: " + signature);

            int depth = 0;
            for (int i = brace; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(start, i - start + 1);
                }
            }

            Assert.Fail("Unclosed method body: " + signature);
            return string.Empty;
        }
    }
}
