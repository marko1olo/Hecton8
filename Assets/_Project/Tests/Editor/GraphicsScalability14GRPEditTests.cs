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
