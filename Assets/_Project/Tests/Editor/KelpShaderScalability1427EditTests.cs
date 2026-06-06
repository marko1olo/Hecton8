using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class KelpShaderScalability1427EditTests
    {
        [TestCase("Hecton_KelpMaster.shader")]
        [TestCase("Hecton_KelpMaster_GPUI.shader")]
        [TestCase("Hecton_CoralMaster.shader")]
        [TestCase("Hecton_CoralMaster_GPUI.shader")]
        [TestCase("Hecton_ProceduralBio.shader")]
        [TestCase("Hecton_RetinaDistortion.shader")]
        public void PresentationShaders_RemoveBinaryQualityVariants(string shaderFile)
        {
            string source = ReadShader(shaderFile);

            Assert.That(source, Does.Not.Contain("_QUALITY_MX350"));
            Assert.That(source, Does.Not.Contain("_QUALITY_HIGH"));
            Assert.That(source, Does.Not.Contain("shader_feature_local _QUALITY"));
        }

        [TestCase("Hecton_KelpMaster.shader")]
        [TestCase("Hecton_KelpMaster_GPUI.shader")]
        public void KelpShaders_ConsumeGlobalQualityContinuously(string shaderFile)
        {
            string source = ReadShader(shaderFile);

            Assert.That(source, Does.Contain("_H8GlobalQualityWeight"));
            Assert.That(source, Does.Contain("HectonKelpSmoothRange01"));
            Assert.That(source, Does.Contain("lerp(0.58h, 1.0h, HectonKelpSmoothRange01(0.0h, 0.85h, qualityWeight))"));
        }

        [TestCase("Hecton_KelpMaster.shader")]
        [TestCase("Hecton_KelpMaster_GPUI.shader")]
        public void KelpShaders_UseArithmeticDearLieWaveInsteadOfTrig(string shaderFile)
        {
            string source = ReadShader(shaderFile);

            Assert.That(source, Does.Not.Contain("sin("));
            Assert.That(source, Does.Not.Contain("cos("));
            Assert.That(source, Does.Not.Contain("HectonKelpBoundedSin"));
            Assert.That(source, Does.Contain("HectonKelpDearLieWave"));
            Assert.That(source, Does.Contain("wave * (1.5 - 0.5 * wave * wave)"));
        }

        [TestCase("Hecton_KelpMaster.shader")]
        [TestCase("Hecton_KelpMaster_GPUI.shader")]
        [TestCase("Hecton_CoralMaster.shader")]
        [TestCase("Hecton_CoralMaster_GPUI.shader")]
        public void FloraShaders_KeepParallaxContinuousWithoutHighTierResample(string shaderFile)
        {
            string source = ReadShader(shaderFile);

            Assert.That(source, Does.Contain("parallaxQualityWeight"));
            Assert.That(source, Does.Contain("samplePositionWS -= viewDirWS * ((maskSample.b - 0.5h) * _HeightScale * parallaxQualityWeight)"));
            Assert.That(source, Does.Not.Contain("                maskSample = SampleFloraTriplanar"));
            Assert.That(source, Does.Not.Contain("                maskSample = SampleFloraDominantAxis"));
        }

        [Test]
        public void ProceduralBio_TriplanarSharpenConsumesContinuousQuality()
        {
            string source = ReadShader("Hecton_ProceduralBio.shader");

            Assert.That(source, Does.Contain("sharpen *= HectonProceduralBioSmoothRange01(0.35h, 0.95h, HectonProceduralBioGlobalQualityWeight())"));
            Assert.That(source, Does.Not.Contain("#if defined(_QUALITY_HIGH)"));
        }

        [Test]
        public void RetinaDistortion_UsesContinuousQualityWithoutRuntimeKeyword()
        {
            string shader = ReadShader("Hecton_RetinaDistortion.shader");
            string feature = ReadProjectFile("Assets", "_Project", "Scripts", "Visor", "HectonRetinaDistortionFeature.cs");

            Assert.That(shader, Does.Contain("_HectonRetinaQualityWeight"));
            Assert.That(shader, Does.Contain("HectonRetinaSmoothRange01(0.45, 0.95, retinaQuality)"));
            Assert.That(feature, Does.Contain("ResolveRetinaVisualQualityWeight"));
            Assert.That(feature, Does.Not.Contain("EnableKeyword"));
            Assert.That(feature, Does.Not.Contain("DisableKeyword"));
            Assert.That(feature, Does.Not.Contain("Mx350Keyword"));
        }

        [Test]
        public void GpuScatterLodManager_UsesContinuousMaterialScalabilityWithoutQualityKeywords()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Rendering", "Scatter", "GpuScatterLodManager.cs");

            Assert.That(source, Does.Not.Contain("_QUALITY_MX350"));
            Assert.That(source, Does.Not.Contain("_QUALITY_HIGH"));
            Assert.That(source, Does.Not.Contain("IsHighQuality"));
            Assert.That(source, Does.Not.Contain("IsKeywordEnabled(Quality"));
            Assert.That(source, Does.Contain("float quality = Smooth01(_cachedQualityWeight01)"));
            Assert.That(source, Does.Contain("math.lerp(SanitizeNonNegativeFinite(lowTierAnisotropicSssStrength)"));
        }

        [Test]
        public void EncounterDirector_CandidateBudgetUsesContinuousQuality()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "EncounterDirector.cs");

            Assert.That(source, Does.Contain("ResolveCandidateCount(_candidateHardwareWeight01)"));
            Assert.That(source, Does.Contain("HomeostasisBrain.GlobalQualityWeight"));
            Assert.That(source, Does.Contain("PlatformAdaptiveBudgetGovernor.RecommendedQualityWeight"));
            Assert.That(source, Does.Contain("math.lerp(BaseCandidateCount, HighCandidateCount, combinedWeight01)"));
            Assert.That(source, Does.Not.Contain("bool highTier"));
            Assert.That(source, Does.Not.Contain("return highTier ?"));
        }

        [Test]
        public void EncounterDirector_SuppressesSpawnsDuringCriticalOxygenLikeCriticalHealth()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "EncounterDirector.cs");
            string profile = ReadProjectFile("Assets", "_Project", "Scripts", "EncounterProfile.cs");
            string stateBody = ExtractTypeBody(source, "internal struct EncounterDirectorState");
            string jobBody = ExtractTypeBody(source, "internal struct EncounterDirectorJob");
            string blackBoxBody = ExtractTypeBody(source, "internal struct EncounterDirectorBlackBoxEntry");
            string dumpBody = ExtractMethodBody(source, "private static unsafe void WriteEncounterBlackBoxRow(");

            Assert.That(source, Does.Contain("private const float CriticalOxygenSpawnSuppressionThreshold = 0.15f;"));
            Assert.That(jobBody, Does.Contain("float playerHealth01 = SanitizeNormalized01(PlayerHealthNormalized);"));
            Assert.That(jobBody, Does.Contain("float playerOxygen01 = SanitizeNormalized01(PlayerOxygenNormalized);"));
            Assert.That(jobBody, Does.Contain("float avgFrameTimeMs = SanitizeNonNegativeFinite(AvgFrameTimeMs);"));
            Assert.That(source, Does.Contain("float health01 = SanitizeNormalized01(frameContext.PlayerHealthNormalized);"));
            Assert.That(source, Does.Contain("float oxygen01 = SanitizeNormalized01(frameContext.PlayerOxygenNormalized);"));
            Assert.That(jobBody, Does.Contain("bool healthCriticalSuppressed = playerHealth01 <= CriticalHealthSpawnSuppressionThreshold;"));
            Assert.That(jobBody, Does.Contain("bool oxygenCriticalSuppressed = playerOxygen01 <= CriticalOxygenSpawnSuppressionThreshold;"));
            Assert.That(jobBody, Does.Contain("bool survivalCriticalSuppressed = healthCriticalSuppressed || oxygenCriticalSuppressed;"));
            Assert.That(jobBody, Does.Contain("bool forceSpawn = !survivalCriticalSuppressed && ForcedThreatCount > 0 && ForcedThreatClass >= 0;"));
            Assert.That(jobBody, Does.Contain("TryResolveDesiredThreatClass(state.IntensityLevel, state.TokenBudget, survivalCriticalSuppressed"));
            Assert.That(jobBody, Does.Contain("ResolveCheapestAllowedCost(state.IntensityLevel, survivalCriticalSuppressed"));
            Assert.That(source, Does.Contain("AllowsSurvivalCriticalSpawn"));
            Assert.That(source, Does.Not.Contain("AllowsCriticalHealthSpawn"));
            Assert.That(source, Does.Not.Contain("bool criticalHealthSuppressed = PlayerHealthNormalized <= CriticalHealthSpawnSuppressionThreshold;"));
            Assert.That(source, Does.Not.Contain("if (AvgFrameTimeMs > LoadShedThresholdMs)"));
            Assert.That(profile, Does.Contain("survival-critical health or oxygen window"));
            Assert.That(stateBody, Does.Contain("public uint SurvivalCriticalFlags;"));
            Assert.That(stateBody, Does.Contain("public uint SurvivalCriticalSeverityPermille;"));
            Assert.That(blackBoxBody, Does.Contain("public uint SurvivalCriticalFlags;"));
            Assert.That(blackBoxBody, Does.Contain("public uint SurvivalCriticalSeverityPermille;"));
            Assert.That(source, Does.Contain("const int rowBytes = 56;"));
            Assert.That(dumpBody, Does.Contain("WriteUInt32LittleEndian(target, 44, entry.SurvivalCriticalFlags);"));
            Assert.That(dumpBody, Does.Contain("WriteUInt32LittleEndian(target, 48, entry.SurvivalCriticalSeverityPermille);"));
            Assert.That(dumpBody, Does.Contain("WriteUInt32LittleEndian(target, 52, entry.ActivePhase);"));
        }

        [Test]
        public void TetherVisuals_BlendDearLieLineContinuously()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "TetherInstance.cs");
            string manager = ReadProjectFile("Assets", "_Project", "Scripts", "TetherManager.cs");
            string visualBody = ExtractMethodBody(source, "internal void UpdateVisuals(");
            string uploadBody = ExtractMethodBody(source, "private void UpdateVerletVisualUpload(");
            string curveBody = ExtractMethodBody(source, "private float ResolveTautLineVisualCurveWeight(");
            string iterationBody = ExtractMethodBody(source, "private static int ResolveVerletIterationCount(");
            string dampingBody = ExtractMethodBody(source, "private static float ResolveVerletVelocityDamping(");
            string fixedTickBody = ExtractMethodBody(manager, "public void FixedTick(");
            string lateFrameBody = ExtractMethodBody(manager, "public void LateFrameTick()");

            Assert.That(source, Does.Contain("ResolveTautLineVisualCurveWeight"));
            Assert.That(source, Does.Contain("math.lerp(straightPoint, _verletPositions[i], curveWeight01)"));
            Assert.That(source, Does.Contain("private float _qualityWeight01 = 1f;"));
            Assert.That(manager, Does.Contain("internal float CachedQualityWeight01 => _cachedQualityWeight01;"));
            Assert.That(fixedTickBody, Does.Contain("float qualityWeight01 = _cachedQualityWeight01;"));
            Assert.That(fixedTickBody, Does.Contain("instance.Simulate(fixedDeltaTime, _fixedStepClockSeconds, fixedFrameIndex, activeCount, maxVisualizedTethers, qualityWeight01)"));
            Assert.That(fixedTickBody, Does.Not.Contain("_cachedQualityTier"));
            Assert.That(manager, Does.Contain("_supportsIndirectTetherRenderingCold = SystemInfo.supportsInstancing && SystemInfo.supportsComputeShaders;"));
            Assert.That(lateFrameBody, Does.Contain("instance.UpdateVisuals(deltaTime, qualityWeight01"));
            Assert.That(lateFrameBody, Does.Contain("HasIndirectTetherRenderResources()"));
            Assert.That(lateFrameBody, Does.Not.Contain("ShouldUseIndirectTetherRendering(qualityWeight01)"));
            Assert.That(manager, Does.Not.Contain("Smooth01(qualityWeight01) >= 0.62f"));
            Assert.That(visualBody, Does.Contain("float qualityWeight01"));
            Assert.That(uploadBody, Does.Contain("float qualityWeight01"));
            Assert.That(curveBody, Does.Contain("SanitizeQualityWeight(qualityWeight01)"));
            Assert.That(iterationBody, Does.Contain("SanitizeQualityWeight(qualityWeight01)"));
            Assert.That(dampingBody, Does.Contain("SanitizeQualityWeight(qualityWeight01)"));
            Assert.That(source, Does.Not.Contain("HomeostasisBrain.GlobalQualityWeight"));
            Assert.That(source, Does.Not.Contain("ShouldUseLowTierTautLineVisualFake"));
            Assert.That(source, Does.Not.Contain("return collapseWeight > 0f"));
            Assert.That(source, Does.Not.Contain("switch (TetherManager.SanitizeQualityTier"));
        }

        [Test]
        public void TetherManager_QualityTierCompatibilityMapperUsesScalarOrdinal()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "TetherManager.cs");
            string mapperBody = ExtractMethodBody(source, "private static HectonQualityTier ResolveQualityTierFromGlobalWeight(");
            string ordinalBody = ExtractMethodBody(source, "private static int ResolveCompatibilityQualityTierOrdinal(");

            Assert.That(mapperBody, Does.Contain("ResolveCompatibilityQualityTierOrdinal(qualityWeight)"));
            Assert.That(ordinalBody, Does.Contain("Smooth01(math.isfinite(qualityWeight) ? qualityWeight : 1f)"));
            Assert.That(ordinalBody, Does.Contain("math.lerp((int)HectonQualityTier.Low, (int)HectonQualityTier.Ultra, q)"));
            Assert.That(ordinalBody, Does.Contain("math.round(tierOrdinal)"));
            Assert.That(mapperBody, Does.Not.Contain("qualityWeight <"));
            Assert.That(ordinalBody, Does.Not.Contain("qualityWeight <"));
            Assert.That(source, Does.Not.Contain("qualityWeight < 0.18f"));
            Assert.That(source, Does.Not.Contain("qualityWeight < 0.36f"));
            Assert.That(source, Does.Not.Contain("qualityWeight < 0.62f"));
            Assert.That(source, Does.Not.Contain("qualityWeight < 0.86f"));
        }

        [Test]
        public void AudioVirtualization_ExposesContinuousQualityWeightContract()
        {
            string contracts = ReadProjectFile("Assets", "_Project", "Scripts", "Audio", "Virtualization", "Contracts", "AudioVirtualizationContracts.cs");
            string manager = ReadProjectFile("Assets", "_Project", "Scripts", "SpatialAudioManager.cs");

            Assert.That(contracts, Does.Contain("SetVirtualizationQualityWeight(float qualityWeight01)"));
            Assert.That(manager, Does.Contain("SetVirtualizationQualityWeight(float qualityWeight01)"));
            Assert.That(contracts, Does.Not.Contain("SetLowTierVirtualization"));
            Assert.That(manager, Does.Not.Contain("lowTier ? 0f : 1f"));
        }

        [Test]
        public void AcousticPortalPropagation_UsesSurvivalBudgetFallbackStatus()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Audio", "AcousticPortalPropagation.cs");
            string enumBody = ExtractTypeBody(source, "public enum AcousticPathStatus");
            string executeBody = ExtractTypeBody(source, "public struct AcousticPathJob");

            Assert.That(enumBody, Does.Contain("SurvivalBudgetFallback = 1"));
            Assert.That(enumBody, Does.Contain("LowTierFallback = SurvivalBudgetFallback"));
            Assert.That(executeBody, Does.Contain("ResolvePortalBudget01(Query.GlobalQualityWeight)"));
            Assert.That(executeBody, Does.Contain("AcousticPathStatus.SurvivalBudgetFallback"));
            Assert.That(executeBody, Does.Not.Contain("AcousticPathStatus.LowTierFallback"));
        }

        [Test]
        public void VrSomaticGhostHands_UseContinuousQualityToleranceWithoutLowTierName()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Gameplay", "VRSomaticProvider.cs");
            string maskBody = ExtractMethodBody(source, "private uint ResolveHandGhostMask(");

            Assert.That(source, Does.Contain("scaleGhostHandToleranceByQuality"));
            Assert.That(source, Does.Contain("FormerlySerializedAs(\"reduceGhostHandsAtLowQuality\")"));
            Assert.That(source, Does.Contain("FormerlySerializedAs(\"disableGhostHandsOnLowTier\")"));
            Assert.That(maskBody, Does.Contain("float qualityCurve = Smoothstep01(_globalQualityWeight01);"));
            Assert.That(maskBody, Does.Contain("math.lerp(2.5f, 1f, qualityCurve)"));
            Assert.That(maskBody, Does.Contain("scaleGhostHandToleranceByQuality"));
            Assert.That(maskBody, Does.Not.Contain("LowQuality"));
            Assert.That(maskBody, Does.Not.Contain("LowTier"));
            Assert.That(maskBody, Does.Not.Contain("reduceGhostHandsAtLowQuality"));
        }

        [Test]
        public void SettingsQualityLevel_RoutesToContinuousHomeostasisWeight()
        {
            string manager = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "SettingsManager.cs");
            string panel = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "SettingsPanel.cs");
            string homeostasis = ReadProjectFile("Assets", "_Project", "Scripts", "Core", "HomeostasisBrain.ScalabilityDictator.cs");

            Assert.That(manager, Does.Contain("MaxContinuousQualityLevel"));
            Assert.That(manager, Does.Contain("ResolveQualityWeightFromLevel"));
            Assert.That(manager, Does.Contain("normalized * normalized * (3f - 2f * normalized)"));
            Assert.That(manager, Does.Contain("HomeostasisBrain.SetUserGlobalQualityWeightPreference(qualityWeight01, true)"));
            Assert.That(manager, Does.Not.Contain("QualitySettings.SetQualityLevel"));
            Assert.That(manager, Does.Not.Contain("QualitySettings.names"));
            Assert.That(panel, Does.Contain("SettingsManager.MaxContinuousQualityLevel"));
            Assert.That(panel, Does.Contain("ResolveLocalizedQualityName(int qualityIndex)"));
            Assert.That(panel, Does.Not.Contain("QualitySettings.names"));
            Assert.That(homeostasis, Does.Contain("SetUserGlobalQualityWeightPreference(float qualityWeight, bool enabled)"));
        }

        [Test]
        public void DeployableSdfDrill_VisualCarveCadenceScalesByContinuousWeight()
        {
            string runtime = ReadProjectFile("Assets", "_Project", "Scripts", "Gameplay", "Mining", "DeployableSdfDrillRuntime.cs");
            string contracts = ReadProjectFile("Assets", "_Project", "Scripts", "Gameplay", "Mining", "Contracts", "DeployableSdfDrillContracts.cs");

            Assert.That(runtime, Does.Contain("sdfVisualCarveQualityFloor01"));
            Assert.That(runtime, Does.Contain("ResolveSdfVisualCarveWeight01"));
            Assert.That(runtime, Does.Contain("ResolveSdfVisualCarveIntervalSeconds"));
            Assert.That(runtime, Does.Contain("HomeostasisBrain.GlobalQualityWeight"));
            Assert.That(runtime, Does.Contain("normalized * normalized * (3f - 2f * normalized)"));
            Assert.That(runtime, Does.Contain("math.lerp(survivalMultiplier, 1f, math.saturate(visualWeight01))"));
            Assert.That(contracts, Does.Contain("SdfVisualDeferred = 1 << 4"));
            Assert.That(runtime, Does.Not.Contain("skipSdfVisualOnLowTier"));
            Assert.That(runtime, Does.Not.Contain("lowTierSdfSkipped"));
            Assert.That(contracts, Does.Not.Contain("LowTierSdfSkipped"));
            Assert.That(runtime, Does.Not.Contain("ToMathLod("));
        }

        [Test]
        public void ThermalDynamicResolution_PrimaryDecisionsStayOnContinuousWeight()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Graphics", "Scalability", "ThermalDynamicResolutionAdapter.cs");
            string contracts = ReadProjectFile("Assets", "_Project", "Scripts", "Core", "Contracts", "DrsContracts.cs");

            Assert.That(source, Does.Contain("ResolvePolicyScale(float qualityWeight01"));
            Assert.That(source, Does.Contain("ResolveMinScaleLimit(float qualityWeight01"));
            Assert.That(source, Does.Contain("ResolveUpscalerHash(float renderScale"));
            Assert.That(source, Does.Contain("UpdateVisualBudget(float qualityWeight01"));
            Assert.That(source, Does.Contain("ResolveBilateralDrsRouteAllowed()"));
            Assert.That(source, Does.Contain("UpscalerBilateralDrsHash"));
            Assert.That(source, Does.Contain("ResolveSurvivalPressureWeight01(float qualityWeight01)"));
            Assert.That(source, Does.Contain("ResolveTierEnvelope(float qualityWeight01"));
            Assert.That(source, Does.Contain("ResolveCompatibilityQualityTierOrdinal(float qualityWeight01)"));
            Assert.That(source, Does.Contain("ApplyMinScaleLimitPreference(float value)"));
            Assert.That(source, Does.Contain("SurvivalPressureFadeStart01"));
            Assert.That(source, Does.Contain("SurvivalPressureFadeEnd01"));
            Assert.That(source, Does.Contain("FlagSurvivalPressureEmergency"));
            Assert.That(source, Does.Contain("ResolutionScaleStateFlags.SurvivalPressureEmergency"));
            Assert.That(contracts, Does.Contain("SurvivalPressureEmergency = 1 << 0"));
            Assert.That(contracts, Does.Contain("LowTierEmergency = SurvivalPressureEmergency"));
            Assert.That(source, Does.Not.Contain("ResolveHardwareTierWeight01"));
            Assert.That(source, Does.Not.Contain("ResolveLowTierWeight01"));
            Assert.That(source, Does.Not.Contain("FlagLowTierEmergency"));
            Assert.That(source, Does.Not.Contain("lowTierWeight01"));
            Assert.That(source, Does.Not.Contain("ResolutionScaleStateFlags.LowTierEmergency"));
            Assert.That(source, Does.Not.Contain("ApplyMinScaleLimitForTier"));
            Assert.That(source, Does.Not.Contain("ResolveMinScaleLimit(HectonQualityTier"));
            Assert.That(source, Does.Not.Contain("ResolveUpscalerHash(HectonQualityTier"));
            Assert.That(source, Does.Not.Contain("ResolveFsrUpscalerAllowed"));
            Assert.That(source, Does.Not.Contain("ResolveFsrUpscalerEligibility01"));
            Assert.That(source, Does.Not.Contain("FsrEligibilityEpsilon"));
            Assert.That(source, Does.Not.Contain("UpscalerFsrTaaHash"));
            Assert.That(source, Does.Not.Contain("UpdateVisualBudget(HectonQualityTier"));
            Assert.That(source, Does.Not.Contain("quality >= 0.86f"));
            Assert.That(source, Does.Not.Contain("quality < 0.18f"));
            Assert.That(source, Does.Not.Contain("quality < 0.36f"));
            Assert.That(source, Does.Not.Contain("quality < 0.62f"));
            Assert.That(source, Does.Not.Contain("quality < 0.86f"));
            Assert.That(source, Does.Not.Contain("graphicsMemoryMb >= 3000"));
        }

        [Test]
        public void ThermalDynamicResolution_UpscalerRouteDoesNotForkOnQuality()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Graphics", "Scalability", "ThermalDynamicResolutionAdapter.cs");
            string hashBody = ExtractMethodBody(source, "private uint ResolveUpscalerHash(");
            string refreshBody = ExtractMethodBody(source, "private void RefreshQualityTierPolicyFromContinuousWeight(");

            Assert.That(source, Does.Contain("UpscalerBilateralDrsHash"));
            Assert.That(source, Does.Contain("_coldBilateralDrsRouteAllowed = !Application.isMobilePlatform && SystemInfo.supportsComputeShaders;"));
            Assert.That(hashBody, Does.Contain("_bilateralDrsRouteAllowed ? UpscalerBilateralDrsHash : UpscalerBilateralTaaHash"));
            Assert.That(hashBody, Does.Not.Contain("qualityWeight01"));
            Assert.That(refreshBody, Does.Contain("_bilateralDrsRouteAllowed = ResolveBilateralDrsRouteAllowed();"));
            Assert.That(source, Does.Not.Contain("ResolveFsrUpscalerAllowed"));
            Assert.That(source, Does.Not.Contain("ResolveFsrUpscalerEligibility01"));
            Assert.That(source, Does.Not.Contain("FsrEligibilityEpsilon"));
            Assert.That(source, Does.Not.Contain("UpscalerFsrTaaHash"));
        }

        [Test]
        public void ThermalDynamicResolution_VisualFeatureRoutesStayContinuous()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Graphics", "Scalability", "ThermalDynamicResolutionAdapter.cs");
            string updateBody = ExtractMethodBody(source, "private void UpdateVisualBudget(");
            string routeMaskBody = ExtractMethodBody(source, "private static uint ResolveVisualFeatureRouteMask(");

            Assert.That(source, Does.Contain("VisualFeatureRouteMask"));
            Assert.That(updateBody, Does.Contain("_visualFeatureFlags = ResolveVisualFeatureRouteMask();"));
            Assert.That(routeMaskBody, Does.Contain("return VisualFeatureRouteMask;"));
            Assert.That(source, Does.Not.Contain("VisualFeatureFlagEpsilon"));
            Assert.That(source, Does.Not.Contain("ResolveVisualFeatureFlags("));
        }

        [Test]
        public void VisorUberPost_ReconstructionOverkillUsesContinuousResponse()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Visor", "HectonVisorUberPostFeature.cs");
            string constantsBody = ExtractMethodBody(source, "private static UberNoirReconstructionConstantsDTO BuildReconstructionConstants(");
            string responseBody = ExtractMethodBody(source, "private static float ResolveCompatibilityVisualOverkillWeight01(");

            Assert.That(source, Does.Contain("[FormerlySerializedAs(\"visualOverkillThreshold\")]"));
            Assert.That(source, Does.Contain("public float visualOverkillResponse"));
            Assert.That(constantsBody, Does.Contain("ResolveCompatibilityVisualOverkillWeight01(quality01, overkillResponseSetting)"));
            Assert.That(responseBody, Does.Contain("quality * quality * math.lerp(0.5f, 1f, quality)"));
            Assert.That(responseBody, Does.Contain("math.lerp(0.65f, 1.35f, response)"));
            Assert.That(constantsBody, Does.Not.Contain("quality01 - threshold"));
            Assert.That(constantsBody, Does.Not.Contain("math.rcp(1f - threshold)"));
            Assert.That(source, Does.Not.Contain("s_editorVisualOverkillThreshold01"));
        }

        [Test]
        public void ActiveSonarGeoTelemetry_StoresQualityWeightWithoutBinaryFlag()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Visor", "SpectrumSystem.cs");
            string entryBody = ExtractTypeBody(source, "private struct ActiveSonarGeoTelemetryEntry");
            string writeBody = ExtractMethodBody(source, "private void WriteActiveSonarGeoTelemetry(");
            string serializeBody = ExtractMethodBody(source, "private static void WriteActiveSonarGeoTelemetryEntry(");
            string encodeBody = ExtractMethodBody(source, "private static byte EncodeActiveSonarGeoQualityWeightQ8(");

            Assert.That(entryBody, Does.Contain("[System.Runtime.InteropServices.FieldOffset(28)]"));
            Assert.That(entryBody, Does.Contain("public byte QualityWeightQ8;"));
            Assert.That(writeBody, Does.Contain("entry.Flags = 0u;"));
            Assert.That(writeBody, Does.Contain("entry.QualityWeightQ8 = EncodeActiveSonarGeoQualityWeightQ8"));
            Assert.That(serializeBody, Does.Contain("destination[28] = entry.QualityWeightQ8;"));
            Assert.That(encodeBody, Does.Contain("quality * byte.MaxValue"));
            Assert.That(writeBody, Does.Not.Contain("<= 0.15f"));
            Assert.That(source, Does.Not.Contain("ResolveActiveSonarGeoQualityWeight() <= 0.15f"));
        }

        [Test]
        public void DiegeticGyroCompass_VisualOverkillStaysPresentationOnlyAndContinuous()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "Navigation", "DiegeticGyroCompassRuntime.cs");
            string scheduleBody = ExtractMethodBody(source, "private void ScheduleDrift(");
            string presentationBody = ExtractMethodBody(source, "private void ApplyPresentation(");
            string drawBody = ExtractMethodBody(source, "private bool ShouldDrawIndirectDial(");
            string qualityBody = ExtractMethodBody(source, "private static float ResolveVisualOverkillWeight01(");
            string noiseBody = ExtractMethodBody(source, "private static float ResolveNoiseValue(");

            Assert.That(source, Does.Contain("[FormerlySerializedAs(\"enableIndirectHighTier\")]"));
            Assert.That(source, Does.Contain("private bool enableIndirectVisualRoute = true;"));
            Assert.That(qualityBody, Does.Contain("quality * quality * math.lerp(0.5f, 1f, quality)"));
            Assert.That(presentationBody, Does.Contain("ResolvePresentationVisualOverkillWeight01(in state)"));
            Assert.That(presentationBody, Does.Contain("ResolveVisualDialHeading(heading, anomaly, visualOverkillWeight, state.NoiseClockSeconds)"));
            Assert.That(presentationBody, Does.Contain("EmitVisualOverkillFailureParticles(powered, anomaly, visualOverkillWeight"));
            Assert.That(drawBody, Does.Contain("enableIndirectVisualRoute"));
            Assert.That(drawBody, Does.Not.Contain("_visualOverkillWeight01 >"));
            Assert.That(drawBody, Does.Not.Contain("state.SystemStress01 <="));
            Assert.That(scheduleBody, Does.Not.Contain("FlagIndirectDial"));
            Assert.That(noiseBody, Does.Not.Contain("FlagIndirectDial"));
            Assert.That(source, Does.Not.Contain("private bool enableIndirectHighTier"));
            Assert.That(source, Does.Not.Contain("ShouldUseVisualOverkill"));
            Assert.That(source, Does.Not.Contain("EmitHighTierFailureParticles"));
        }

        [Test]
        public void DiegeticGyroCompass_IndirectBufferRepairIsSlowPhaseOnly()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "Navigation", "DiegeticGyroCompassRuntime.cs");
            string lateFrameBody = ExtractMethodBody(source, "public void LateFrameTick()");
            string slowBody = ExtractMethodBody(source, "public void SlowTick()");
            string repairBody = ExtractMethodBody(source, "private void FlushIndirectBuffersRepairSlow()");
            string ensureBody = ExtractMethodBody(source, "private void EnsureIndirectBuffersCold()");
            string requirementBody = ExtractMethodBody(source, "private bool ShouldRequireIndirectBuffersCold()");

            Assert.That(slowBody, Does.Contain("FlushIndirectBuffersRepairSlow();"));
            Assert.That(slowBody, Does.Contain("ShouldRequireIndirectBuffersCold() && !HasIndirectBuffersReady()"));
            Assert.That(lateFrameBody, Does.Contain("if (_indirectBuffersDirty)"));
            Assert.That(lateFrameBody, Does.Not.Contain("EnsureIndirectBuffersCold();"));
            Assert.That(lateFrameBody, Does.Not.Contain("FlushIndirectBuffersRepairSlow();"));
            Assert.That(lateFrameBody, Does.Not.Contain("SupportsIndirectDialCold"));
            Assert.That(lateFrameBody, Does.Not.Contain("SystemInfo."));
            Assert.That(repairBody, Does.Contain("CacheGraphicsCapabilitiesCold();"));
            Assert.That(repairBody, Does.Contain("EnsureIndirectBuffersCold();"));
            Assert.That(repairBody, Does.Contain("_indirectBuffersDirty = false;"));
            Assert.That(repairBody, Does.Contain("_indirectBuffersDirty = !HasIndirectBuffersReady();"));
            Assert.That(requirementBody, Does.Contain("_supportsIndirectDialCold"));
            Assert.That(ensureBody, Does.Contain("new GraphicsBuffer"));
        }

        [Test]
        public void MathLodRuntimeConfig_PublishesContinuousQualityWeightsWithoutBinaryQualityFlags()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "MathLodApproximation.cs");
            string configDto = ExtractTypeBody(source, "public struct MathLodConfigDTO");
            string publishBody = ExtractMethodBody(source, "public static bool PublishConfig(");
            string defaultBody = ExtractMethodBody(source, "private static MathLodConfigDTO CreateDefaultConfig()");
            string survivalBody = ExtractMethodBody(source, "private static float ResolveSurvivalPressureWeight01(");
            string overkillBody = ExtractMethodBody(source, "private static float ResolveVisualOverkillWeight01(");

            Assert.That(configDto, Does.Contain("[FieldOffset(52)] public float VisualOverkillWeight01;"));
            Assert.That(publishBody, Does.Contain("float pressure = ResolveSurvivalPressureWeight01(quality);"));
            Assert.That(publishBody, Does.Contain("float visualOverkillWeight = ResolveVisualOverkillWeight01(quality);"));
            Assert.That(publishBody, Does.Contain("dto.VisualOverkillWeight01 = visualOverkillWeight;"));
            Assert.That(publishBody, Does.Not.Contain("ConfigFlagMinimumSurvival"));
            Assert.That(publishBody, Does.Not.Contain("ConfigFlagVisualOverkill"));
            Assert.That(publishBody, Does.Not.Contain("quality <= 0.1001f"));
            Assert.That(publishBody, Does.Not.Contain("quality >= 0.95f"));

            Assert.That(defaultBody, Does.Contain("dto.Flags = 0u;"));
            Assert.That(defaultBody, Does.Contain("dto.VisualOverkillWeight01 = 1f;"));
            Assert.That(defaultBody, Does.Not.Contain("ConfigFlagVisualOverkill"));

            Assert.That(survivalBody, Does.Contain("1f - quality"));
            Assert.That(overkillBody, Does.Contain("SmoothRange01(0.72f, 1f, quality)"));
        }

        [Test]
        public void AmbientBiota_UsesContinuousSpawnAndShaderQualitySignals()
        {
            string director = ReadProjectFile("Assets", "_Project", "Scripts", "AI", "Ambient", "AmbientBiotaDirector.cs");
            string signalSource = ReadProjectFile("Assets", "_Project", "Scripts", "Core", "Signals", "GlobalSignalPayloads.DomainRemainder.cs");
            string contracts = ReadProjectFile("Assets", "_Project", "Scripts", "Core", "GlobalRegistryContracts.cs");
            string shader = ReadProjectFile("Assets", "_Project", "Scripts", "AI", "Ambient", "Hecton_AmbientBiotaIndirect.shader");
            string hydrateBody = ExtractMethodBody(director, "public bool TryHydrateMacroSwarms(");
            string recountBody = ExtractMethodBody(director, "private void RecountActiveBiota(");
            string signalBody = ExtractTypeBody(signalSource, "public struct EntitySpawnSignal");
            string stateBody = ExtractTypeBody(contracts, "public struct AmbientBiotaState");
            string mutationFlags = ExtractTypeBody(contracts, "public static class FaunaGenomeMutationRequestFlags");

            Assert.That(signalBody, Does.Contain("[FieldOffset(57)] public byte QualityWeightQ8;"));
            Assert.That(signalBody, Does.Contain("[FieldOffset(57)] public byte QualityTier;"));
            Assert.That(signalBody, Does.Contain("[FieldOffset(59)] public byte SurvivalPressureQ8;"));
            Assert.That(signalBody, Does.Contain("[FieldOffset(59)] public byte Reserved;"));
            Assert.That(signalBody, Does.Contain("FlagSurvivalPressureVisual = 1 << 1"));
            Assert.That(signalBody, Does.Contain("FlagVisualOverkillCompatibility = 1 << 3"));
            Assert.That(signalBody, Does.Contain("FlagLowTierVisual = FlagSurvivalPressureVisual"));
            Assert.That(signalBody, Does.Contain("FlagHighTierOverkill = FlagVisualOverkillCompatibility"));

            Assert.That(hydrateBody, Does.Contain("QualityWeightQ8 = spawnQualityByte"));
            Assert.That(hydrateBody, Does.Contain("Flags = EntitySpawnSignal.FlagEcology"));
            Assert.That(hydrateBody, Does.Contain("SurvivalPressureQ8 = EncodeMacroSurvivalPressureSignalByte(macroSurvivalPressure01)"));
            Assert.That(hydrateBody, Does.Not.Contain("macroQualityWeight01 >= 0.95f"));
            Assert.That(hydrateBody, Does.Not.Contain("EntitySpawnMinimumQualityVisualFlag"));
            Assert.That(hydrateBody, Does.Not.Contain("EntitySpawnVisualOverkillFlag"));

            Assert.That(director, Does.Contain("EncodeMacroSurvivalPressureSignalByte(float survivalPressure01)"));
            Assert.That(director, Does.Contain("AmbientStateHeadlightReactiveFlag = AmbientBiotaState.FlagHeadlightReactive"));
            Assert.That(director, Does.Not.Contain("TelemetryFlagVisualOverkill"));
            Assert.That(director, Does.Not.Contain("_visualOverkillWeight01 >= 0.6f"));
            Assert.That(director, Does.Not.Contain("AmbientStateMinimumQualityBillboardFlag"));
            Assert.That(director, Does.Not.Contain("AmbientStateVisualOverkillReactiveFlag"));

            Assert.That(recountBody, Does.Not.Contain("TelemetryFlagVisualOverkill"));
            Assert.That(stateBody, Does.Contain("FlagSurvivalBillboardPressure = 1u << 1"));
            Assert.That(stateBody, Does.Contain("FlagHeadlightReactive = 1u << 4"));
            Assert.That(stateBody, Does.Contain("FlagLowTierBillboard = FlagSurvivalBillboardPressure"));
            Assert.That(stateBody, Does.Contain("FlagHighTierReactive = FlagHeadlightReactive"));
            Assert.That(mutationFlags, Does.Contain("SurvivalPressureMacroSkipped = 1 << 2"));
            Assert.That(mutationFlags, Does.Contain("LowTierMacroSkipped = SurvivalPressureMacroSkipped"));

            Assert.That(shader, Does.Contain("float survivalBillboardPressure = saturate(max(1.0 - _HectonBiotaQualityProfile, _HectonBiotaSystemStress01));"));
            Assert.That(shader, Does.Contain("float2 ParallaxCheat(float2 uv, float hash, float overkill01)"));
            Assert.That(shader, Does.Contain("uv = ParallaxCheat(uv, hash, _HectonBiotaOverkill01);"));
            Assert.That(shader, Does.Not.Contain("HECTON_BIOTA_MINIMUM_QUALITY_BILLBOARD"));
            Assert.That(shader, Does.Not.Contain("minimumQualityBillboard"));
            Assert.That(shader, Does.Not.Contain("Parallax16"));
            Assert.That(shader, Does.Not.Contain("if (_HectonBiotaOverkill01 >"));
        }

        [Test]
        public void AudioLootAndSubOs_UseSurvivalScalarNamesWithoutQualityTierCuts()
        {
            string lootContracts = ReadProjectFile("Assets", "_Project", "Scripts", "Gameplay", "Loot", "Contracts", "LootMagnetContracts.cs");
            string lootRuntime = ReadProjectFile("Assets", "_Project", "Scripts", "Gameplay", "Loot", "LootMagnetSystem.cs");
            string lootJob = ReadProjectFile("Assets", "_Project", "Scripts", "Gameplay", "Loot", "Contracts", "LootMagnetPullJob.cs");
            string spatial = ReadProjectFile("Assets", "_Project", "Scripts", "SpatialAudioManager.cs");
            string audioContracts = ReadProjectFile("Assets", "_Project", "Scripts", "Audio", "Virtualization", "Contracts", "AudioVirtualizationContracts.cs");
            string subOs = ReadProjectFile("Assets", "_Project", "Scripts", "Gameplay", "HectonSubmarineOS.cs");

            Assert.That(lootContracts, Does.Contain("SurvivalAcousticSignalsPerFrame"));
            Assert.That(lootContracts, Does.Contain("VisualOverkillWakeSignalsPerFrame"));
            Assert.That(lootContracts, Does.Contain("SurvivalPressureLerp"));
            Assert.That(lootRuntime, Does.Contain("SmoothRange01(quality, 0.42f, 0.74f)"));
            Assert.That(lootRuntime, Does.Contain("HighFidelityFluidImpulseRadiusMeters"));
            Assert.That(lootRuntime, Does.Not.Contain("LowTierAcousticSignalsPerFrame"));
            Assert.That(lootRuntime, Does.Not.Contain("LowTierWakeSignalsPerFrame"));
            Assert.That(lootJob, Does.Not.Contain("LowTierLerp"));

            Assert.That(spatial, Does.Contain("SurvivalAmbientOutputSampleRate"));
            Assert.That(spatial, Does.Contain("float occlusionWeight = math.lerp(0.25f, 1f, SmoothQuality01(qualityWeight));"));
            Assert.That(spatial, Does.Contain("QualityWeightQ8 = EncodeVirtualVoiceQualityQ8(_virtualVoiceQualityWeight)"));
            Assert.That(spatial, Does.Contain("private static ushort EncodeVirtualVoiceQualityQ8(float qualityWeight01)"));
            Assert.That(spatial, Does.Not.Contain("qualityWeight > 0.02f"));
            Assert.That(spatial, Does.Not.Contain("_cachedSpatialAudioQualityWeight01 <= 0.28f"));
            Assert.That(spatial, Does.Not.Contain("_virtualVoiceLowTierApplied"));
            Assert.That(spatial, Does.Not.Contain("_virtualVoiceSurvivalPressureApplied"));
            Assert.That(audioContracts, Does.Contain("SurvivalPhysicalVoiceCount"));
            Assert.That(
                audioContracts.Contains("[FieldOffset(52)]\n        public ushort QualityWeightQ8;") ||
                audioContracts.Contains("[FieldOffset(52)]\r\n        public ushort QualityWeightQ8;"),
                Is.True);

            Assert.That(subOs, Does.Contain("SurvivalSonarRefreshIntervalSeconds"));
            Assert.That(subOs, Does.Contain("VisualOverkillSonarRefreshIntervalSeconds"));
            Assert.That(subOs, Does.Not.Contain("LowTierSonarRefreshIntervalSeconds"));
            Assert.That(subOs, Does.Not.Contain("MidTierSonarRefreshIntervalSeconds"));
            Assert.That(subOs, Does.Not.Contain("HighTierSonarRefreshIntervalSeconds"));
        }

        [Test]
        public void HomeostasisEmergencyMask_UsesSurvivalPressureBit()
        {
            string contracts = ReadProjectFile("Assets", "_Project", "Scripts", "Core", "HomeostasisBrain.cs");
            string dictator = ReadProjectFile("Assets", "_Project", "Scripts", "Core", "HomeostasisBrain.ScalabilityDictator.cs");
            string watchdog = ReadProjectFile("Assets", "_Project", "Scripts", "QA", "Headless", "Shinobu38QaWatchdogRuntime.cs");
            string buildFlagsBody = ExtractMethodBody(contracts, "private static ushort BuildFlags(");
            string pressureBody = ExtractMethodBody(contracts, "private static ushort ApplyPressurePolicy(");

            Assert.That(contracts, Does.Contain("SurvivalPressureEmergency = 1UL << 24"));
            Assert.That(contracts, Does.Contain("LowTierEmergency = SurvivalPressureEmergency"));
            Assert.That(dictator, Does.Contain("SystemBit.SurvivalPressureEmergency"));
            Assert.That(dictator, Does.Not.Contain("VisualOverkillFlagQualityThreshold01"));
            Assert.That(buildFlagsBody, Does.Not.Contain("GlobalQualityWeight >="));
            Assert.That(buildFlagsBody, Does.Not.Contain("VisualOverkillBudgetOpen"));
            Assert.That(pressureBody, Does.Contain("if ((targetMask & (ulong)SystemBit.VisualOverkill) != 0UL)"));
            Assert.That(pressureBody, Does.Contain("HomeostasisSignalFlags.VisualOverkillBudgetOpen"));
            Assert.That(dictator, Does.Not.Contain("SystemBit.LowTierEmergency"));
            Assert.That(watchdog, Does.Contain("VaultFlagSurvivalPressureEmergency"));
            Assert.That(watchdog, Does.Contain("RichNormalFadeStart01"));
            Assert.That(watchdog, Does.Contain("float3 richNormal = Shinobu38MockTerrainSdf.SampleNormal(ahead);"));
            Assert.That(watchdog, Does.Contain("float normalBlend = richNormalGate * quality * quality"));
            Assert.That(watchdog, Does.Not.Contain("VaultFlagLowTierEmergency"));
            Assert.That(watchdog, Does.Not.Contain("LowQualityNormalCollapseThreshold"));
            Assert.That(watchdog, Does.Not.Contain("richNormalGate > 0f ?"));
        }

        [Test]
        public void RenderingShaderBridges_UseSurvivalPressureWeightNaming()
        {
            string dispatcher = ReadProjectFile("Assets", "_Project", "Scripts", "Rendering", "GlobalShaderDispatcher.cs");
            string noirBridge = ReadProjectFile("Assets", "_Project", "Scripts", "Rendering", "HectonUberNoirRuntimeBridge.cs");

            Assert.That(dispatcher, Does.Contain("ResolveSurvivalPressureWeight01"));
            Assert.That(dispatcher, Does.Contain("ResolveSurvivalPressureFloor01"));
            Assert.That(dispatcher, Does.Contain("SurvivalPressureWeight01"));
            Assert.That(dispatcher, Does.Contain("_lastSurvivalPressureBucket"));
            Assert.That(noirBridge, Does.Contain("ResolveSurvivalPressureWeight01"));
            Assert.That(noirBridge, Does.Contain("ResolveSurvivalPressureFloor01"));
            Assert.That(noirBridge, Does.Contain("survivalPressureWeight01"));
            Assert.That(noirBridge, Does.Contain("FeatureSurvivalPressure"));

            Assert.That(dispatcher, Does.Not.Contain("ResolveLowTierWeight01"));
            Assert.That(dispatcher, Does.Not.Contain("ResolveLowTierFloor01"));
            Assert.That(dispatcher, Does.Not.Contain("lowTierWeight01"));
            Assert.That(dispatcher, Does.Not.Contain("LowTierWeight01"));
            Assert.That(dispatcher, Does.Not.Contain("_lastLowTierWeightBucket"));
            Assert.That(noirBridge, Does.Not.Contain("ResolveLowTierWeight01"));
            Assert.That(noirBridge, Does.Not.Contain("ResolveLowTierFloor01"));
            Assert.That(noirBridge, Does.Not.Contain("lowTierWeight01"));
        }

        [Test]
        public void RenderingShaderBridges_UseContinuousOverkillAndRouteMasks()
        {
            string dispatcher = ReadProjectFile("Assets", "_Project", "Scripts", "Rendering", "GlobalShaderDispatcher.cs");
            string noirBridge = ReadProjectFile("Assets", "_Project", "Scripts", "Rendering", "HectonUberNoirRuntimeBridge.cs");
            string dispatcherOverkillBody = ExtractMethodBody(dispatcher, "private static float ResolveVisualOverkillWeight01(");
            string noirLateFrameBody = ExtractMethodBody(noirBridge, "public void LateFrameTick()");
            string stressWeightBody = ExtractMethodBody(noirBridge, "private float ResolveStressShedWeight01(");
            string featureMaskBody = ExtractMethodBody(noirBridge, "private static uint BuildFeatureMask()");

            Assert.That(dispatcherOverkillBody, Does.Contain("quality * quality * math.lerp(0.5f, 1f, quality)"));
            Assert.That(noirLateFrameBody, Does.Contain("ResolveStressShedWeight01(stress01)"));
            Assert.That(noirLateFrameBody, Does.Contain("ResolveVisualOverkillWeight01(quality01)"));
            Assert.That(stressWeightBody, Does.Contain("_stressShedWeight01"));
            Assert.That(featureMaskBody, Does.Contain("ContinuousUberNoirFeatureMask"));

            Assert.That(dispatcher, Does.Not.Contain("(quality01 - 0.78f) * 4.5454545f"));
            Assert.That(dispatcher, Does.Not.Contain("(quality - 0.78f) * 4.5454545f"));
            Assert.That(noirLateFrameBody, Does.Not.Contain("bool stressShed"));
            Assert.That(noirLateFrameBody, Does.Not.Contain("if (stressShed)"));
            Assert.That(noirBridge, Does.Not.Contain("private bool ResolveStressShed("));
            Assert.That(featureMaskBody, Does.Not.Contain("if ("));
            Assert.That(featureMaskBody, Does.Not.Contain("FeatureMaskEpsilon"));
        }

        [Test]
        public void AbyssalThermalGrid_UsesContinuousCostCadenceInsteadOfQualityThreshold()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "AbyssalThermalManager.cs");
            string usesBody = ExtractMethodBody(source, "private static bool UsesThermalGrid()");
            string advanceBody = ExtractMethodBody(source, "private void AdvanceThermalMapColdTick(");

            Assert.That(source, Does.Contain("ResolveThermalGridCostWeight01"));
            Assert.That(source, Does.Contain("ResolveThermalMapColdTickSeconds"));
            Assert.That(source, Does.Contain("ResolveThermalMapDiffusion01"));
            Assert.That(source, Does.Contain("HasThermalMapStorage"));
            Assert.That(source, Does.Contain("ThermalGridSurvivalColdTickMultiplier"));
            Assert.That(source, Does.Contain("ThermalGridSurvivalDiffusionScale"));
            Assert.That(usesBody, Does.Contain("ThermalGridResolution > 0 && ThermalMapCellCount > 0"));
            Assert.That(advanceBody, Does.Contain("if (!HasThermalMapStorage())"));
            Assert.That(advanceBody, Does.Contain("float coldTickSeconds = ResolveThermalMapColdTickSeconds();"));
            Assert.That(advanceBody, Does.Contain("Diffusion01 = ResolveThermalMapDiffusion01()"));
            Assert.That(source, Does.Not.Contain("ThermalGridEnableQualityThreshold01"));
            Assert.That(source, Does.Not.Contain("effectiveQuality >= "));
        }

        [Test]
        public void GasDynamicsCadence_UsesSurvivalToOverkillScalarNames()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Atmosphere", "GasDynamicsSolver.cs");
            string cadenceBody = ExtractMethodBody(source, "private float ResolveCadenceSeconds(");

            Assert.That(source, Does.Contain("survivalColdTickSeconds"));
            Assert.That(source, Does.Contain("standardColdTickSeconds"));
            Assert.That(source, Does.Contain("overkillColdTickSeconds"));
            Assert.That(source, Does.Contain("survivalHibernationDistanceMeters"));
            Assert.That(source, Does.Contain("[FormerlySerializedAs(\"lowTierColdTickSeconds\")]"));
            Assert.That(source, Does.Contain("[FormerlySerializedAs(\"lowTierHibernationDistanceMeters\")]"));
            Assert.That(cadenceBody, Does.Contain("float survivalCadence = math.max(0.1f, survivalColdTickSeconds);"));
            Assert.That(cadenceBody, Does.Contain("float survivalToStandard = math.lerp(survivalCadence, standardCadence, q);"));
            Assert.That(cadenceBody, Does.Contain("math.lerp(survivalToStandard, overkillCadence, q)"));
            Assert.That(cadenceBody, Does.Not.Contain("lowTierColdTickSeconds"));
            Assert.That(cadenceBody, Does.Not.Contain("midTierColdTickSeconds"));
            Assert.That(cadenceBody, Does.Not.Contain("highTierColdTickSeconds"));
            Assert.That(source, Does.Not.Contain("private float lowTierHibernationDistanceMeters"));
        }

        [Test]
        public void WorldSampler_DoesNotPublishBinarySurvivalSamplingPressureFlag()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "GlobalWorldSampler.cs");
            string sampleBody = ExtractMethodBody(source, "public static void SampleDistanceOnly(");
            string normalBody = ExtractMethodBody(source, "public static void EstimateNormal(");

            Assert.That(source, Does.Contain("ForceSurvivalSamplingPressure = 1 << 0"));
            Assert.That(source, Does.Contain("ForceMathLodLow = ForceSurvivalSamplingPressure"));
            Assert.That(source, Does.Contain("SurvivalSamplingPressure = 1 << 3"));
            Assert.That(source, Does.Contain("MathLodLow = SurvivalSamplingPressure"));
            Assert.That(sampleBody, Does.Contain("byte resultFlags = 0;"));
            Assert.That(normalBody, Does.Contain("GlobalWorldSamplerResultFlags.NormalEstimated"));
            Assert.That(sampleBody, Does.Not.Contain("ResolveSurvivalSamplingPressureFlag"));
            Assert.That(sampleBody, Does.Not.Contain("GlobalWorldSamplerResultFlags.SurvivalSamplingPressure"));
            Assert.That(normalBody, Does.Not.Contain("GlobalWorldSamplerResultFlags.SurvivalSamplingPressure"));
            Assert.That(source, Does.Not.Contain("private static byte ResolveSurvivalSamplingPressureFlag"));
            Assert.That(source, Does.Not.Contain("ResolveExpensiveSamplingWeight(qualityWeight) <= 0.0001f"));
            Assert.That(sampleBody, Does.Not.Contain("GlobalWorldSamplerResultFlags.MathLodLow"));
            Assert.That(normalBody, Does.Not.Contain("GlobalWorldSamplerResultFlags.MathLodLow"));
            Assert.That(source, Does.Not.Contain("qualityWeight <= 0.05f ?"));
        }

        [Test]
        public void ToxicOutgassingTelemetry_DoesNotPublishBinaryFallbackQualityFlag()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Atmosphere", "ToxicOutgassingChemistryRuntime.cs");
            string scheduleBody = ExtractMethodBody(source, "private void ScheduleSimulation(");
            string headerBody = ExtractMethodBody(source, "private void UpdateGridHeader()");

            Assert.That(source, Does.Not.Contain("TelemetryFlagFallbackRadial"));
            Assert.That(scheduleBody, Does.Not.Contain("qualityWeight < 0.3f"));
            Assert.That(headerBody, Does.Not.Contain("_activeResolution == LowResolution"));
            Assert.That(scheduleBody, Does.Contain("Flags = (byte)(_mockChemistry ? TelemetryFlagMockChemistry : 0)"));
            Assert.That(headerBody, Does.Contain("header.Flags = (byte)(_mockChemistry ? TelemetryFlagMockChemistry : 0);"));
            Assert.That(source, Does.Contain("header.GlobalQualityWeight = _lastQualityWeight;"));
            Assert.That(source, Does.Contain("header.Resolution = (ushort)math.clamp(_activeResolution"));
        }

        [Test]
        public void MarauderOutpostDescriptor_UsesContinuousQualitySnapshotWithoutTierCuts()
        {
            string jobs = ReadProjectFile("Assets", "_Project", "Scripts", "World", "Outposts", "MarauderOutpostJobs.cs");
            string service = ReadProjectFile("Assets", "_Project", "Scripts", "World", "Outposts", "MarauderOutpostGenerationService.cs");
            string contracts = ReadProjectFile("Assets", "_Project", "Scripts", "World", "Contracts", "OutpostGenerationContracts.cs");
            string descriptorBody = ExtractMethodBody(service, "private ushort ResolveDescriptorFlags()");
            string compatibilityBody = ExtractMethodBody(service, "private static OutpostGenerationQualityTier ResolveCompatibilityQualityTier(");
            string ordinalBody = ExtractMethodBody(service, "private static float ResolveCompatibilityQualityTierOrdinal(");
            string snapshotBody = ExtractMethodBody(service, "private void UpdateSnapshot()");
            string snapshotType = ExtractTypeBody(contracts, "public struct OutpostGenerationSnapshot");

            Assert.That(jobs, Does.Contain("public const uint SurvivalBandFlag = WfcOutpostGridConstants.DescriptorFlagLowTier;"));
            Assert.That(jobs, Does.Contain("public const uint LowTierFlag = SurvivalBandFlag;"));
            Assert.That(descriptorBody, Does.Not.Contain("MarauderOutpostConstants.SurvivalBandFlag"));
            Assert.That(descriptorBody, Does.Not.Contain("MarauderOutpostConstants.LowTierFlag"));
            Assert.That(service, Does.Contain("_compatibilityQualityTier = ResolveCompatibilityQualityTier(_generationQualityWeight01);"));
            Assert.That(service, Does.Contain("QualityWeightQ8 = EncodeQualityWeightQ8(_generationQualityWeight01)"));
            Assert.That(service, Does.Contain("SurvivalBandWeightQ8 = EncodeSurvivalBandWeightQ8(_generationQualityWeight01)"));
            Assert.That(service, Does.Contain("private static byte EncodeSurvivalBandWeightQ8(float qualityWeight01)"));
            Assert.That(service, Does.Contain("private static float ResolveSurvivalBandWeight01(float qualityWeight01)"));
            Assert.That(snapshotType, Does.Contain("[FieldOffset(56)] public byte QualityWeightQ8;"));
            Assert.That(snapshotType, Does.Contain("[FieldOffset(57)] public byte SurvivalBandWeightQ8;"));
            Assert.That(compatibilityBody, Does.Contain("ResolveCompatibilityQualityTierOrdinal(qualityWeight01)"));
            Assert.That(ordinalBody, Does.Contain("MathLodApproximation.SmoothStep01(MathLodApproximation.SaturateFinite(qualityWeight01, 1f))"));
            Assert.That(ordinalBody, Does.Contain("math.lerp("));
            Assert.That(snapshotBody, Does.Not.Contain("ResolveQualityTier("));
            Assert.That(descriptorBody, Does.Not.Contain("survivalBandWeight >"));
            Assert.That(service, Does.Not.Contain("MiddleOutpostQualityThreshold01"));
            Assert.That(service, Does.Not.Contain("HighOutpostQualityThreshold01"));
            Assert.That(service, Does.Not.Contain("UltraOutpostQualityThreshold01"));
            Assert.That(service, Does.Not.Contain("private OutpostGenerationQualityTier _qualityTier"));
            Assert.That(service, Does.Not.Contain("private static OutpostGenerationQualityTier ResolveQualityTier("));
            Assert.That(compatibilityBody, Does.Not.Contain("qualityWeight01 <"));
            Assert.That(ordinalBody, Does.Not.Contain("qualityWeight01 <"));
        }

        [Test]
        public void MarauderOutpostExtraction_UsesLocalScratchAndSingleWriterFlushes()
        {
            string jobs = ReadProjectFile("Assets", "_Project", "Scripts", "World", "Outposts", "MarauderOutpostJobs.cs");
            string service = ReadProjectFile("Assets", "_Project", "Scripts", "World", "Outposts", "MarauderOutpostGenerationService.cs");
            string scheduleBody = ExtractMethodBody(service, "private void ScheduleMatrixExtraction()");
            string lateFrameBody = ExtractMethodBody(service, "public void LateFrameTick()");
            string flushHelperBody = ExtractMethodBody(service, "private bool TryFlushScratchBuffer<T>(");
            string flushBody = ExtractMethodBody(service, "private bool FlushExtractionScratchToVault()");

            Assert.That(jobs, Does.Contain("[ReadOnly, NoAlias] public NativeArray<byte>.ReadOnly WfcGrid;"));
            Assert.That(service, Does.Contain("TryPrepareSolveScratch"));
            Assert.That(service, Does.Contain("FlushSolveScratchToVault"));
            Assert.That(service, Does.Contain("TryPrepareExtractionScratch"));
            Assert.That(service, Does.Contain("TryPrepareShiftScratch"));
            Assert.That(service, Does.Contain("FlushShiftScratchToVault"));
            Assert.That(service, Does.Contain("ReleaseExtractionScratchBuffers();"));
            Assert.That(service, Does.Not.Contain("TryAcquireSolveJobBuffer"));
            Assert.That(service, Does.Not.Contain("ReleaseSolveJobBufferLock"));
            Assert.That(service, Does.Not.Contain("_extractionJobBuffersLocked"));
            Assert.That(service, Does.Not.Contain("TryAcquireExtractionJobBuffers"));
            Assert.That(service, Does.Not.Contain("ReleaseExtractionJobBufferLocks"));
            Assert.That(service, Does.Not.Contain("TryAcquireShiftJobBuffer"));
            Assert.That(service, Does.Not.Contain("ReleaseShiftJobBufferLock"));
            Assert.That(lateFrameBody, Does.Contain("FlushSolveScratchToVault()"));
            Assert.That(lateFrameBody, Does.Contain("FlushExtractionScratchToVault()"));
            Assert.That(lateFrameBody, Does.Contain("FlushShiftScratchToVault()"));
            Assert.That(scheduleBody, Does.Contain("TryReadFullWfcGrid(out NativeArray<byte>.ReadOnly wfcGrid)"));
            Assert.That(scheduleBody, Does.Contain("TryPrepareExtractionScratch("));
            Assert.That(scheduleBody, Does.Not.Contain("TryAcquireWriteBuffer"));
            Assert.That(flushBody, Does.Contain("TryFlushScratchBuffer(in _wfcMutableStateGridHandle"));
            Assert.That(flushBody, Does.Contain("TryFlushScratchBuffer(in _countersHandle"));
            Assert.That(flushHelperBody, Does.Contain("TryAcquireWriteBuffer(in handle, bufferId, requiredLength"));
            Assert.That(flushHelperBody, Does.Contain("finally"));
            Assert.That(flushHelperBody, Does.Contain("ReleaseWriteBuffer(in handle, bufferId);"));
        }

        [Test]
        public void MarauderOutpostExtraction_FailsClosedWithoutHeightmapPayload()
        {
            string contracts = ReadProjectFile("Assets", "_Project", "Scripts", "Logistics", "Grid", "Contracts", "WfcOutpostGridContracts.cs");
            string jobs = ReadProjectFile("Assets", "_Project", "Scripts", "World", "Outposts", "MarauderOutpostJobs.cs");
            string service = ReadProjectFile("Assets", "_Project", "Scripts", "World", "Outposts", "MarauderOutpostGenerationService.cs");
            string scheduleBody = ExtractMethodBody(service, "private void ScheduleMatrixExtraction()");
            string extractionJob = ExtractTypeBody(jobs, "internal struct MarauderOutpostMatrixExtractionJob");
            string sampleBody = ExtractMethodBody(extractionJob, "private float SampleHeight(");
            string oldHeightmapFlag = "Heightmap" + "FallbackFlag";
            string oldHeightFallback = "fallback" + "Height";
            string oldFlatTerrain = "OriginMeters.y - " + "StiltClearanceMeters";
            string oldFlatTerrainSize = "new float3(32f, " + "8f, 32f)";
            int heightPayloadIndex = scheduleBody.IndexOf("ResolveHeightmapPayload()");
            int scratchPrepareIndex = scheduleBody.IndexOf("TryPrepareExtractionScratch(");

            Assert.That(scheduleBody, Does.Contain("if (!hasHeightmapPayload)"));
            Assert.That(scheduleBody, Does.Contain("_missingHeightmap = true;"));
            Assert.That(scheduleBody, Does.Contain("MarauderOutpostConstants.MissingHeightmapFlag"));
            Assert.That(scheduleBody, Does.Contain("DumpBlackBox();"));
            Assert.That(heightPayloadIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(scratchPrepareIndex, Is.GreaterThan(heightPayloadIndex));
            Assert.That(scheduleBody, Does.Not.Contain(oldFlatTerrain));
            Assert.That(scheduleBody, Does.Not.Contain(oldFlatTerrainSize));
            Assert.That(extractionJob, Does.Contain("if (!hasHeightmap)"));
            Assert.That(extractionJob, Does.Contain("Counters[4] = 1;"));
            Assert.That(extractionJob, Does.Not.Contain(oldHeightFallback));
            Assert.That(sampleBody, Does.Not.Contain("if (!hasHeightmap)"));
            Assert.That(service, Does.Not.Contain("_heightmap" + "Fallback"));
            Assert.That(service, Does.Not.Contain(oldHeightmapFlag));
            Assert.That(jobs, Does.Not.Contain(oldHeightmapFlag));
            Assert.That(contracts, Does.Contain("DescriptorFlagMissingHeightmap = 1 << 1"));
            Assert.That(contracts, Does.Not.Contain("DescriptorFlag" + "HeightmapFallback"));
        }

        [Test]
        public void FloraGenomeJobs_ConsumeContinuousQualityWeightInsteadOfHardwareTierSwitches()
        {
            string contracts = ReadProjectFile("Assets", "_Project", "Scripts", "World", "FloraGenomics", "FloraGenomeContracts.cs");
            string jobs = ReadProjectFile("Assets", "_Project", "Scripts", "World", "FloraGenomics", "FloraGenomeJobs.cs");
            string runtime = ReadProjectFile("Assets", "_Project", "Scripts", "World", "FloraGenomics", "FloraGenomeVaultRuntime.cs");
            string editor = ReadProjectFile("Assets", "_Project", "Scripts", "Editor", "LSystemGenomeLabWindow.cs");

            Assert.That(contracts, Does.Contain("public byte QualityWeightQ8;"));
            Assert.That(contracts, Does.Not.Contain("public enum FloraGenomeHardwareTier"));
            Assert.That(contracts, Does.Not.Contain("public byte HardwareTier;"));

            Assert.That(jobs, Does.Contain("ResolveIterationCap(float qualityWeight01"));
            Assert.That(jobs, Does.Contain("ResolveMatrixCap(float qualityWeight01"));
            Assert.That(jobs, Does.Contain("public float QualityWeight01;"));
            Assert.That(jobs, Does.Contain("float q = Smooth01(SaturateFinite(qualityWeight01, 1f));"));
            Assert.That(jobs, Does.Contain("SmoothRange01(0f, 0.35f, q)"));
            Assert.That(jobs, Does.Contain("SmoothRange01(0.75f, 1f, q)"));
            Assert.That(jobs, Does.Not.Contain("switch ((FloraGenomeHardwareTier)hardwareTier)"));
            Assert.That(jobs, Does.Not.Contain("case FloraGenomeHardwareTier."));
            Assert.That(jobs, Does.Not.Contain("LowTierMatrixCap"));

            Assert.That(runtime, Does.Contain("float qualityWeight01 = ResolveGenomeQualityWeight01();"));
            Assert.That(runtime, Does.Contain("resolvedSeed.QualityWeightQ8 = EncodeQualityWeightQ8(qualityWeight01);"));
            Assert.That(runtime, Does.Contain("QualityWeight01 = qualityWeight01"));
            Assert.That(runtime, Does.Not.Contain("ResolveHardwareTier"));
            Assert.That(runtime, Does.Not.Contain("MiddleHardwareQualityThreshold01"));

            Assert.That(editor, Does.Contain("QualityWeightQ8 = 255"));
            Assert.That(editor, Does.Contain("QualityWeight01 = 1f"));
            Assert.That(editor, Does.Not.Contain("FloraGenomeHardwareTier"));
        }

        [Test]
        public void IndirectVegetation_DataVaultWritesAreSingleLockPhases()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "HectonIndirectVegetationRenderer.cs");

            Assert.That(source, Does.Contain("TryAcquireCpuCullingMatricesForWrite"));
            Assert.That(source, Does.Contain("TryAcquireCpuCullingInstanceDataForWrite"));
            Assert.That(source, Does.Contain("TryUploadMatrixDirtyPages"));
            Assert.That(source, Does.Contain("TryUploadDataDirtyPages"));
            Assert.That(source, Does.Contain("TryMarkUploadedDirtyPages"));
            Assert.That(source, Does.Contain("TryAcquireVaultStorageForWrite"));
            Assert.That(source, Does.Contain("ResolvePlayerToolManagerFromCachedContext"));
            Assert.That(source, Does.Contain("if (!success && lockAcquired && vault != null)"));
            Assert.That(source, Does.Not.Contain("TryAcquireCpuCullingDataForWrite"));
            Assert.That(source, Does.Not.Contain("TryAcquireUploadedDirtyPagesForWrite"));
            Assert.That(source, Does.Not.Contain("ReleaseUploadedDirtyPageWriteLocks"));
            Assert.That(source, Does.Not.Contain("BootstrapState.TryGetCurrentPlayerTransform"));
            Assert.That(source, Does.Not.Contain("TryGetComponent"));
        }

        [Test]
        public void MigratorySargassumJob_DoesNotHoldTwoWriteLocksAcrossBurstJob()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "WorldProceduralScatterDirectorMigratorySargassum.cs");

            Assert.That(source, Does.Contain("TryPrepareMigratorySargassumFlowSamples"));
            Assert.That(source, Does.Contain("TryAcquireMigratorySargassumIslandJobBuffer"));
            Assert.That(source, Does.Contain("_migratorySargassumFlowSamples.ReleaseWriteLock(SystemID.WorldSargassum);"));
            Assert.That(source, Does.Contain("_migratorySargassumFlowSamples.TryLockBuffer(SystemID.WorldSargassum)"));
            Assert.That(source, Does.Contain("_migratorySargassumFlowSamples.TryUnlockBuffer(SystemID.WorldSargassum);"));
            Assert.That(source, Does.Not.Contain("TryAcquireMigratorySargassumJobBuffers"));
            Assert.That(source, Does.Not.Contain("flowSamplesLocked"));
            Assert.That(source, Does.Not.Contain("islandsLocked"));

            int prepareCallIndex = source.IndexOf("if (!TryPrepareMigratorySargassumFlowSamples", System.StringComparison.Ordinal);
            int islandWriteLockIndex = source.IndexOf("if (!TryAcquireMigratorySargassumIslandJobBuffer", System.StringComparison.Ordinal);
            int scheduleIndex = source.IndexOf("job.Schedule(_migratorySargassumIslandCount, 8)", System.StringComparison.Ordinal);

            Assert.That(prepareCallIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(islandWriteLockIndex, Is.GreaterThan(prepareCallIndex));
            Assert.That(scheduleIndex, Is.GreaterThan(islandWriteLockIndex));
        }

        [Test]
        public void JacobianFoamRuntime_DataVaultWritesAreSingleLockPhases()
        {
            string runtime = ReadProjectFile("Assets", "_Project", "Scripts", "VFX", "JacobianFoam", "JacobianFoamGpuRuntime.cs");
            string contracts = ReadProjectFile("Assets", "_Project", "Scripts", "VFX", "JacobianFoam", "JacobianFoamContracts.cs");

            Assert.That(runtime, Does.Contain("TryWriteTuning(in tuning)"));
            Assert.That(runtime, Does.Contain("TryWriteAndUploadParams(in parameters)"));
            Assert.That(runtime, Does.Contain("TryWriteAndUploadMockWakes(in tuning, phaseTime)"));
            Assert.That(runtime, Does.Contain("TryUploadReadOnlyWakes()"));
            Assert.That(runtime, Does.Contain("ReleaseWriteBuffer(in _tuningHandle, BufferID.JacobianFoamTuning);"));
            Assert.That(runtime, Does.Contain("ReleaseWriteBuffer(in _paramsHandle, BufferID.JacobianFoamParams);"));
            Assert.That(runtime, Does.Contain("ReleaseWriteBuffer(in _wakeHandle, BufferID.JacobianFoamWakeImpacts);"));
            Assert.That(runtime, Does.Contain("CacheGraphicsCapabilitySnapshotCold();"));
            Assert.That(runtime, Does.Contain("private bool _coldSupportsComputeShaders;"));
            Assert.That(runtime, Does.Contain("private GraphicsFormat _coldFoamTextureFormat = GraphicsFormat.None;"));
            Assert.That(runtime, Does.Contain("if (_computeShader == null || !_coldSupportsComputeShaders)"));
            Assert.That(runtime, Does.Contain("GraphicsFormat targetFormat = _coldFoamTextureFormat;"));
            Assert.That(runtime, Does.Contain("ResolveFoamTextureFormatCold()"));
            Assert.That(runtime, Does.Not.Contain("bool wakeWriteLocked"));
            Assert.That(runtime, Does.Not.Contain("bool tuningWriteLocked"));
            Assert.That(runtime, Does.Not.Contain("GenerateMockStormStateJob job"));
            Assert.That(runtime, Does.Not.Contain("if (_computeShader == null || !HardwareTierDetector.AllowHighResourceComputeShaders)"));
            Assert.That(runtime, Does.Not.Contain("GraphicsFormat targetFormat = ResolveFoamTextureFormat();"));
            Assert.That(contracts, Does.Contain("ResolveMockStormTuning(in FoamTuningDTO source, float qualityWeight)"));
            Assert.That(contracts, Does.Contain("BuildMockWakeImpact("));
        }

        [Test]
        public void WorldProceduralFieldSampler_CellSamplingJobPinsReadOnlyVaultBuffers()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "WorldProceduralFieldSampler.cs");

            Assert.That(source, Does.Contain("TryPinSamplingJobBuffers"));
            Assert.That(source, Does.Contain("_samplingJobBuffersPinned"));
            Assert.That(source, Does.Contain("TryLockBuffer(BufferID.WorldProceduralFieldZones, OwnerSystemId)"));
            Assert.That(source, Does.Contain("TryLockBuffer(BufferID.WorldProceduralFieldBiomeMatrices, OwnerSystemId)"));
            Assert.That(source, Does.Contain("TryLockBuffer(BufferID.WorldProceduralFieldBiomeMatrixIndex, OwnerSystemId)"));
            Assert.That(source, Does.Contain("TryLockBuffer(BufferID.WorldProceduralFieldBiomeFamilies, OwnerSystemId)"));
            Assert.That(source, Does.Contain("TryLockBuffer(BufferID.WorldProceduralFieldCaveEntranceHints, OwnerSystemId)"));
            Assert.That(source, Does.Contain("TryLockBuffer(BufferID.WorldProceduralFieldNoiseLookup, OwnerSystemId)"));
            Assert.That(source, Does.Contain("TryUnlockBuffer(BufferID.WorldProceduralFieldNoiseLookup, OwnerSystemId)"));
            Assert.That(source, Does.Contain("ReleaseSamplingJobBufferPins(pinnedCount);"));
            Assert.That(source, Does.Not.Contain("TryAcquireSamplingJobBuffers"));
            Assert.That(source, Does.Not.Contain("_samplingJobBuffersLocked"));
            Assert.That(source, Does.Not.Contain("ReleaseSamplingJobBufferLocks"));
            Assert.That(source, Does.Not.Contain("TryAcquireWriteLock(in _burstZoneDataHandle"));
            Assert.That(source, Does.Not.Contain("TryAcquireWriteLock(in _burstBiomeMatrixDataHandle"));
            Assert.That(source, Does.Not.Contain("TryAcquireWriteLock(in _burstBiomeMatrixIdToDataIndexHandle"));
            Assert.That(source, Does.Not.Contain("TryAcquireWriteLock(in _burstBiomeFamilyDataHandle"));
            Assert.That(source, Does.Not.Contain("TryAcquireWriteLock(in _burstCaveEntranceHintsHandle"));
            Assert.That(source, Does.Not.Contain("TryAcquireWriteLock(in _noiseLookupTableHandle"));

            int pinIndex = source.IndexOf("if (!_dataVault.TryLockBuffer(BufferID.WorldProceduralFieldZones", System.StringComparison.Ordinal);
            int resolveIndex = pinIndex >= 0
                ? source.IndexOf("if (!TryResolveSamplingData(", pinIndex, System.StringComparison.Ordinal)
                : -1;
            int scheduleIndex = resolveIndex >= 0
                ? source.IndexOf("job.Schedule(cellCount", resolveIndex, System.StringComparison.Ordinal)
                : -1;

            Assert.That(pinIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(resolveIndex, Is.GreaterThan(pinIndex));
            Assert.That(scheduleIndex, Is.GreaterThan(resolveIndex));
        }

        [Test]
        public void DataVaultTelemetryAndCsvPaths_DoNotNestWriteLocks()
        {
            string navGrid = ReadProjectFile("Assets", "_Project", "Scripts", "World", "VoxelDynamicNavGridRuntime.cs");
            string vegetation = ReadProjectFile("Assets", "_Project", "Scripts", "World", "VegetationMemorySovereigntyRuntime.cs");
            string surfaceNets = ReadProjectFile("Assets", "_Project", "Scripts", "World", "VoxelSurfaceNets", "VoxelSurfaceNetsVault.cs");

            Assert.That(navGrid, Does.Not.Contain("ringLocked"));
            Assert.That(navGrid, Does.Not.Contain("cursorLocked"));
            int navCursorAcquire = navGrid.IndexOf("TryAcquireWriteLock(in _navGridTelemetryCursorHandle", System.StringComparison.Ordinal);
            int navCursorRelease = navGrid.IndexOf("ReleaseWriteLock(in _navGridTelemetryCursorHandle", System.StringComparison.Ordinal);
            int navRingAcquire = navGrid.IndexOf("TryAcquireWriteLock(in _navGridTelemetryRingHandle", System.StringComparison.Ordinal);
            Assert.That(navCursorAcquire, Is.GreaterThanOrEqualTo(0));
            Assert.That(navCursorRelease, Is.GreaterThan(navCursorAcquire));
            Assert.That(navRingAcquire, Is.GreaterThan(navCursorRelease));

            Assert.That(vegetation, Does.Not.Contain("cursorLocked"));
            int vegetationCursorAcquire = vegetation.IndexOf("out NativeArray<int> cursorBuffer", System.StringComparison.Ordinal);
            int vegetationCursorRelease = vegetation.IndexOf("in _vegetationMemoryTelemetryCursorHandle,\r\n                    VegetationMemorySovereigntyConstants.OwnerSystemId);", System.StringComparison.Ordinal);
            if (vegetationCursorRelease < 0)
            {
                vegetationCursorRelease = vegetation.IndexOf("in _vegetationMemoryTelemetryCursorHandle,\n                    VegetationMemorySovereigntyConstants.OwnerSystemId);", System.StringComparison.Ordinal);
            }

            int vegetationRingAcquire = vegetation.IndexOf("out NativeArray<VegetationMemoryTelemetryEntry> telemetry", System.StringComparison.Ordinal);
            Assert.That(vegetationCursorAcquire, Is.GreaterThanOrEqualTo(0));
            Assert.That(vegetationCursorRelease, Is.GreaterThan(vegetationCursorAcquire));
            Assert.That(vegetationRingAcquire, Is.GreaterThan(vegetationCursorRelease));

            Assert.That(surfaceNets, Does.Not.Contain("tuningLocked"));
            Assert.That(surfaceNets, Does.Not.Contain("statesLocked"));
            int commitIndex = surfaceNets.IndexOf("private static bool TryCommitCsvTuning", System.StringComparison.Ordinal);
            int tuningAcquire = commitIndex >= 0
                ? surfaceNets.IndexOf("vault.TryAcquireWriteLock(in handles.Tuning", commitIndex, System.StringComparison.Ordinal)
                : -1;
            int tuningRelease = tuningAcquire >= 0
                ? surfaceNets.IndexOf("vault.ReleaseWriteLock(in handles.Tuning", tuningAcquire, System.StringComparison.Ordinal)
                : -1;
            int statesAcquire = tuningRelease >= 0
                ? surfaceNets.IndexOf("vault.TryAcquireWriteLock(in handles.States", tuningRelease, System.StringComparison.Ordinal)
                : -1;
            Assert.That(commitIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(tuningAcquire, Is.GreaterThanOrEqualTo(0));
            Assert.That(tuningRelease, Is.GreaterThan(tuningAcquire));
            Assert.That(statesAcquire, Is.GreaterThan(tuningRelease));
        }

        [Test]
        public void InputAndAudioTelemetry_DoNotNestRingAndCursorWriteLocks()
        {
            string input = ReadProjectFile("Assets", "_Project", "Scripts", "Input", "ControlRemapper.cs");
            string audio = ReadProjectFile("Assets", "_Project", "Scripts", "AudioLog", "AudioLogSystem.cs");

            int inputRecordIndex = input.IndexOf("public static void RecordTelemetry", System.StringComparison.Ordinal);
            int inputDumpIndex = input.IndexOf("public static bool TryDumpTelemetry", System.StringComparison.Ordinal);
            Assert.That(inputRecordIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(inputDumpIndex, Is.GreaterThan(inputRecordIndex));
            string inputRecord = input.Substring(inputRecordIndex, inputDumpIndex - inputRecordIndex);
            Assert.That(inputRecord, Does.Not.Contain("ringLocked"));
            Assert.That(inputRecord, Does.Not.Contain("cursorLocked"));
            int inputCursorAcquire = inputRecord.IndexOf("TryAcquireWriteLock(in cursorHandle", System.StringComparison.Ordinal);
            int inputCursorRelease = inputRecord.IndexOf("ReleaseWriteLock(in cursorHandle", System.StringComparison.Ordinal);
            int inputRingAcquire = inputRecord.IndexOf("TryAcquireWriteLock(in ringHandle", System.StringComparison.Ordinal);
            Assert.That(inputCursorAcquire, Is.GreaterThanOrEqualTo(0));
            Assert.That(inputCursorRelease, Is.GreaterThan(inputCursorAcquire));
            Assert.That(inputRingAcquire, Is.GreaterThan(inputCursorRelease));

            int audioRecordIndex = audio.IndexOf("private void RecordVaultTelemetry", System.StringComparison.Ordinal);
            int audioResolveIndex = audio.IndexOf("private uint ResolveExpectedGeneration", System.StringComparison.Ordinal);
            Assert.That(audioRecordIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(audioResolveIndex, Is.GreaterThan(audioRecordIndex));
            string audioRecord = audio.Substring(audioRecordIndex, audioResolveIndex - audioRecordIndex);
            Assert.That(audioRecord, Does.Not.Contain("cursorLocked"));
            Assert.That(audioRecord, Does.Not.Contain("TryAcquireWriteLock"));
            Assert.That(audioRecord, Does.Not.Contain("ReleaseWriteLock"));
            Assert.That(audioRecord, Does.Contain("TryAcquireMutationGuard(TelemetryMutationGuardMask)"));
            Assert.That(audioRecord, Does.Contain("TryResolveHandle(in _telemetryRingHandle"));
            Assert.That(audioRecord, Does.Contain("ReleaseVaultMutation(vault, TelemetryMutationGuardMask)"));

            Assert.That(audio, Does.Not.Contain("TryAcquireEncryptedFragmentState"));
            Assert.That(audio, Does.Not.Contain("ReleaseEncryptedFragmentWriteLocks"));
            Assert.That(audio, Does.Not.Contain("TryAcquireVaultWrite"));
            Assert.That(audio, Does.Not.Contain("ReleaseVaultWrite"));
            int helperIndex = audio.IndexOf("private bool TryAcquireVaultMutation", System.StringComparison.Ordinal);
            int helperFinally = helperIndex >= 0
                ? audio.IndexOf("finally", helperIndex, System.StringComparison.Ordinal)
                : -1;
            int helperRelease = helperFinally >= 0
                ? audio.IndexOf("ReleaseVaultMutation(guardVault, mutationGuardMask);", helperFinally, System.StringComparison.Ordinal)
                : -1;
            Assert.That(helperIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(helperFinally, Is.GreaterThan(helperIndex));
            Assert.That(helperRelease, Is.GreaterThan(helperFinally));
            Assert.That(audio, Does.Contain("guardVault.TryAcquireMutationGuard(mutationGuardMask)"));
            Assert.That(audio, Does.Contain("!guardVault.TryResolveHandle(in handle, out buffer)"));
            Assert.That(audio, Does.Contain("return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);"));

            int fragmentIndex = audio.IndexOf("private bool SetEncryptedFragmentBits", System.StringComparison.Ordinal);
            Assert.That(fragmentIndex, Is.GreaterThanOrEqualTo(0));
            int pairWriteIndex = audio.IndexOf("private bool TryWriteEncryptedFragmentPair", fragmentIndex, System.StringComparison.Ordinal);
            int pairAcquireIndex = audio.IndexOf("private bool TryAcquireEncryptedFragmentMutationView", pairWriteIndex, System.StringComparison.Ordinal);
            Assert.That(pairWriteIndex, Is.GreaterThan(fragmentIndex));
            Assert.That(pairAcquireIndex, Is.GreaterThan(pairWriteIndex));
            string pairWrite = audio.Substring(pairWriteIndex, pairAcquireIndex - pairWriteIndex);
            int fragmentBitsWrite = pairWrite.IndexOf("recoveredBitBuffer[slot] = recoveredBits & EncryptedLogCompleteMask;", System.StringComparison.Ordinal);
            int fragmentHashWrite = fragmentBitsWrite >= 0
                ? pairWrite.IndexOf("hashes[slot] = logHash;", fragmentBitsWrite, System.StringComparison.Ordinal)
                : -1;
            Assert.That(fragmentBitsWrite, Is.GreaterThanOrEqualTo(0));
            Assert.That(fragmentHashWrite, Is.GreaterThan(fragmentBitsWrite));
            Assert.That(pairWrite, Does.Contain("TryAcquireEncryptedFragmentMutationView"));
            Assert.That(pairWrite, Does.Contain("ReleaseVaultMutation(guardVault, EncryptedFragmentStateMutationGuardMask)"));
            Assert.That(audio, Does.Contain("private unsafe bool TryClearEncryptedFragmentBuffer"));
            Assert.That(audio, Does.Contain("private bool TryWriteEncryptedFragmentValue"));
            Assert.That(audio, Does.Contain("EncryptedFragmentStateMutationGuardMask"));
        }

        [Test]
        public void VoxelPathingProfiles_CsvImportDoesNotNestProfileAndCountWriteLocks()
        {
            string runtime = ReadProjectFile("Assets", "_Project", "Scripts", "AI", "Pathfinding", "PathFunnelNavmeshRuntime_VoxelAStar.cs");
            string parser = ReadProjectFile("Assets", "_Project", "Scripts", "AI", "Pathfinding", "VoxelAStarJobs.cs");

            Assert.That(parser, Does.Contain("out int written"));
            Assert.That(parser, Does.Contain("profileCount[0] = written;"));
            Assert.That(runtime, Does.Contain("VoxelPathingProfileCsvParser.TryParse(csvBytes, profiles, out written, out flags)"));
            int profileAcquire = runtime.IndexOf("TryAcquireWriteLock(in _voxelPathSpeciesProfilesHandle", System.StringComparison.Ordinal);
            int profileRelease = runtime.IndexOf("ReleaseWriteLock(in _voxelPathSpeciesProfilesHandle", System.StringComparison.Ordinal);
            int countAcquire = runtime.IndexOf("TryAcquireWriteLock(in _voxelPathSpeciesProfileCountHandle", System.StringComparison.Ordinal);
            Assert.That(profileAcquire, Is.GreaterThanOrEqualTo(0));
            Assert.That(profileRelease, Is.GreaterThan(profileAcquire));
            Assert.That(countAcquire, Is.GreaterThan(profileRelease));
            Assert.That(runtime, Does.Not.Contain("countLocked"));
        }

        [Test]
        public void UtilityAICognition_PublishesContinuousQualityQ8AndOverBudgetTelemetry()
        {
            string contracts = ReadProjectFile("Assets", "_Project", "Scripts", "AI", "Cognition", "UtilityAICognitionContracts.cs");
            string jobs = ReadProjectFile("Assets", "_Project", "Scripts", "AI", "Cognition", "UtilityAICognitionJobs.cs");
            string vault = ReadProjectFile("Assets", "_Project", "Scripts", "AI", "Cognition", "UtilityAICognitionVault.cs");

            Assert.That(contracts, Does.Contain("public const byte VisualOverkillScoring = 1 << 6;"));
            Assert.That(contracts, Does.Contain("public const byte OverBudget = VisualOverkillScoring;"));
            Assert.That(contracts, Does.Contain("[FieldOffset(58)] public byte QualityWeightQ8;"));

            string evaluationBody = ExtractTypeBody(jobs, "public struct EvaluateUtilityCognitionJob");
            Assert.That(evaluationBody, Does.Contain("EncodeQualityWeightQ8(quality)"));
            Assert.That(evaluationBody, Does.Contain("output.QualityWeightQ8 = qualityWeightQ8;"));
            Assert.That(evaluationBody, Does.Not.Contain("UtilityAICognitionActionFlags.VisualOverkillScoring"));
            Assert.That(evaluationBody, Does.Not.Contain("UtilityAICognitionActionFlags.HighQuality"));
            Assert.That(evaluationBody, Does.Not.Contain("quality > 0.75f"));

            string telemetryBody = ExtractTypeBody(jobs, "public struct RecordCognitionTelemetryJob");
            Assert.That(telemetryBody, Does.Contain("UtilityAICognitionActionFlags.OverBudget"));
            Assert.That(telemetryBody, Does.Not.Contain("UtilityAICognitionActionFlags.HighQuality"));

            string mathBody = ExtractTypeBody(jobs, "public static class UtilityAICognitionJobMath");
            Assert.That(mathBody, Does.Contain("public static byte EncodeQualityWeightQ8(float quality)"));
            Assert.That(mathBody, Does.Contain("encodedQuality * 255f"));

            Assert.That(vault, Does.Contain("UtilityAICognitionActionFlags.OverBudget"));
            Assert.That(vault, Does.Not.Contain("UtilityAICognitionActionFlags.HighQuality"));
        }

        [Test]
        public void ShinobuApexBrain_PublishesContinuousSurvivalNodeBudgetPressure()
        {
            string contracts = ReadProjectFile("Assets", "_Project", "Scripts", "AI", "Cognition", "ShinobuApexBrainContracts.cs");
            string jobs = ReadProjectFile("Assets", "_Project", "Scripts", "AI", "Cognition", "ShinobuApexBrainJobs.cs");
            string layout = ReadProjectFile("Assets", "_Project", "Scripts", "AI", "Cognition", "Editor", "AICognitionLayoutGuard1300.cs");
            string jobBody = ExtractTypeBody(jobs, "public struct ApexBrainJob");
            string outputType = ExtractTypeBody(contracts, "public struct ApexBrainOutputDTO");
            string influenceType = ExtractTypeBody(contracts, "public struct ApexInfluenceNode");

            Assert.That(contracts, Does.Contain("public const byte SurvivalNodeBudgetPressureCompatibility = 1 << 4;"));
            Assert.That(contracts, Does.Contain("public const byte SurvivalNodeBudgetPressure = SurvivalNodeBudgetPressureCompatibility;"));
            Assert.That(contracts, Does.Contain("public const byte ReducedQualityNodeBudget = SurvivalNodeBudgetPressure;"));
            Assert.That(outputType, Does.Contain("[FieldOffset(156)] public byte SurvivalNodeBudgetPressureQ8;"));
            Assert.That(influenceType, Does.Contain("[FieldOffset(52)] public byte SurvivalNodeBudgetPressureQ8;"));
            Assert.That(layout, Does.Contain("AssertOffset<ApexBrainOutputDTO>(nameof(ApexBrainOutputDTO.SurvivalNodeBudgetPressureQ8), 156);"));
            Assert.That(layout, Does.Contain("AssertOffset<ApexInfluenceNode>(nameof(ApexInfluenceNode.SurvivalNodeBudgetPressureQ8), 52);"));
            Assert.That(jobBody, Does.Contain("EncodeSurvivalNodeBudgetPressureQ8(quality)"));
            Assert.That(jobBody, Does.Contain("output.SurvivalNodeBudgetPressureQ8 = survivalNodeBudgetPressureQ8;"));
            Assert.That(jobBody, Does.Contain("SurvivalNodeBudgetPressureQ8 = survivalNodeBudgetPressureQ8"));
            Assert.That(jobBody, Does.Not.Contain("ResolveSurvivalNodeBudgetPressureFlag"));
            Assert.That(jobBody, Does.Not.Contain("ApexBrainFlags.SurvivalNodeBudgetPressure"));
            Assert.That(jobBody, Does.Not.Contain("ResolveReducedQualityNodeBudgetFlag"));
            Assert.That(jobBody, Does.Not.Contain("ApexBrainFlags.ReducedQualityNodeBudget"));
            Assert.That(jobBody, Does.Not.Contain("quality >= ApexBrainConstants.MinimumQualityNodeHold"));
        }

        [Test]
        public void CarveDebrisBlackBox_PublishesContinuousQualityPressure()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "VFX", "Debris", "CarveDebrisComputeRenderer.cs");
            string blackBoxBody = ExtractMethodBody(source, "private void WriteBlackBox");
            string entryBody = ExtractTypeBody(source, "private struct CarveDebrisTelemetryEntry");

            Assert.That(source, Does.Not.Contain("QualityPressureTelemetryFlag"));
            Assert.That(source, Does.Contain("private static byte EncodeQualityPressureQ8(float qualityPressure01)"));
            Assert.That(entryBody, Does.Contain("public byte QualityPressureQ8;"));
            Assert.That(blackBoxBody, Does.Contain("byte qualityPressureQ8 = EncodeQualityPressureQ8(qualityPressure01);"));
            Assert.That(blackBoxBody, Does.Contain("QualityPressureQ8 = qualityPressureQ8"));
            Assert.That(blackBoxBody, Does.Not.Contain("qualityPressure01 >= 0.75f"));
        }

        [Test]
        public void SplashdownFluidImpulseTelemetry_EncodesContinuousQualityPressure()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "HectonFluidEngine.cs");
            string queueBody = ExtractMethodBody(source, "private bool QueueSplashdownFluidImpulse");
            string publishBody = ExtractMethodBody(source, "private void PublishSplashdownFluidImpulseTelemetry");

            Assert.That(source, Does.Not.Contain("SplashdownImpulseQualityPressureFlag"));
            Assert.That(source, Does.Not.Contain("qualityPressure01 > 0.001f"));
            Assert.That(source, Does.Contain("private byte _splashdownImpulseQualityPressureQ8;"));
            Assert.That(source, Does.Contain("private static byte EncodeSplashdownImpulseQualityPressureQ8(float qualityPressure01)"));
            Assert.That(queueBody, Does.Contain("_splashdownImpulseQualityPressureQ8 = EncodeSplashdownImpulseQualityPressureQ8(qualityPressure01);"));
            Assert.That(publishBody, Does.Contain("uint qualityPressureContext = (uint)_splashdownImpulseQualityPressureQ8 << 16;"));
            Assert.That(publishBody, Does.Contain("SplashdownFluidImpulseContextHash ^ flags ^ qualityPressureContext"));
        }

        [Test]
        public void DispatcherBlackBox_PacksContinuousQualityWeight()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Core", "SystemDispatcher.cs");
            string writeBody = ExtractMethodBody(source, "private void RecordDispatcherBlackBoxHeartbeat");

            Assert.That(source, Does.Not.Contain("DispatcherBlackBoxFlagSurvivalQuality"));
            Assert.That(source, Does.Not.Contain("_globalQualityWeight01 <= 0.25f"));
            Assert.That(source, Does.Contain("private const int DispatcherBlackBoxQualityWeightQ8Shift = 8;"));
            Assert.That(source, Does.Contain("private static ushort EncodeDispatcherQualityWeightQ8(float qualityWeight01)"));
            Assert.That(writeBody, Does.Contain("ushort flags = EncodeDispatcherQualityWeightQ8(_globalQualityWeight01);"));
        }

        [Test]
        public void VisorFluidBlackBox_PublishesScalarQualityTelemetry()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Visor", "HectonVisorFluidDistortionFeature.cs");
            string entryBody = ExtractTypeBody(source, "private struct VisorRefractionTelemetryEntry");
            string writeBody = ExtractMethodBody(source, "private void WriteBlackBoxFrame");
            string hashBody = ExtractMethodBody(source, "private static uint BuildBlackBoxHash");
            string serializeBody = ExtractMethodBody(source, "private static void WriteTelemetryEntry");

            Assert.That(source, Does.Not.Contain("BlackBoxFlagQualityPressure"));
            Assert.That(source, Does.Not.Contain("BlackBoxFlagHomeostasisFallback"));
            Assert.That(source, Does.Not.Contain("BlackBoxFlagThermalMotionCull"));
            Assert.That(source, Does.Not.Contain("BlackBoxFlagVisualOverkill"));
            Assert.That(writeBody, Does.Not.Contain("runtimeState.QualityPressure01 > 0.001f"));
            Assert.That(writeBody, Does.Not.Contain("runtimeState.HomeostasisFallback01 > 0.5f"));
            Assert.That(writeBody, Does.Not.Contain("runtimeState.ThermalMotionCull01 > 0.5f"));
            Assert.That(writeBody, Does.Not.Contain("runtimeState.VisualOverkill01 > 0.001f"));
            Assert.That(entryBody, Does.Contain("[System.Runtime.InteropServices.FieldOffset(48)]"));
            Assert.That(entryBody, Does.Contain("public byte QualityPressureQ8;"));
            Assert.That(entryBody, Does.Contain("public byte HomeostasisFallbackQ8;"));
            Assert.That(entryBody, Does.Contain("public byte ThermalMotionCullQ8;"));
            Assert.That(entryBody, Does.Contain("public byte VisualOverkillQ8;"));
            Assert.That(writeBody, Does.Contain("entry.QualityPressureQ8 = EncodeUnitQ8(runtimeState.QualityPressure01);"));
            Assert.That(writeBody, Does.Contain("entry.VisualOverkillQ8 = EncodeUnitQ8(runtimeState.VisualOverkill01);"));
            Assert.That(hashBody, Does.Contain("Sanitize01(runtimeState.HomeostasisFallback01)"));
            Assert.That(hashBody, Does.Contain("Sanitize01(runtimeState.ThermalMotionCull01)"));
            Assert.That(serializeBody, Does.Contain("destination[48] = entry.QualityPressureQ8;"));
            Assert.That(serializeBody, Does.Contain("destination[51] = entry.VisualOverkillQ8;"));
        }

        [Test]
        public void AtmosphereFluidAndEquipmentTelemetry_DoNotNestCursorAndRingWriteLocks()
        {
            string atmosphere = ReadProjectFile("Assets", "_Project", "Scripts", "SubmarineAtmosphereSystem.cs");
            string fluid = ReadProjectFile("Assets", "_Project", "Scripts", "HectonFluidEngine.cs");
            string equipment = ReadProjectFile("Assets", "_Project", "Scripts", "ModularEquipmentEngine.cs");

            AssertCursorBeforeRing(atmosphere, "private void RecordAtmosphereBlackBox", "_telemetryCursorHandle", "_telemetryRingHandle");
            AssertCursorBeforeRing(atmosphere, "private void RecordAtmosphereFailure", "_telemetryCursorHandle", "_telemetryRingHandle");

            int fluidMethod = fluid.IndexOf("private void RecordFluidSovereigntyTelemetry", System.StringComparison.Ordinal);
            Assert.That(fluidMethod, Is.GreaterThanOrEqualTo(0));
            int fluidCursorAcquire = fluid.IndexOf("_fluidSovereigntyTelemetryCursor.TryAcquireWriteLock", fluidMethod, System.StringComparison.Ordinal);
            int fluidCursorRelease = fluid.IndexOf("_fluidSovereigntyTelemetryCursor.ReleaseWriteLock", fluidCursorAcquire, System.StringComparison.Ordinal);
            int fluidRingAcquire = fluid.IndexOf("_fluidSovereigntyTelemetry.TryAcquireWriteLock", fluidCursorRelease, System.StringComparison.Ordinal);
            Assert.That(fluidCursorAcquire, Is.GreaterThanOrEqualTo(0));
            Assert.That(fluidCursorRelease, Is.GreaterThan(fluidCursorAcquire));
            Assert.That(fluidRingAcquire, Is.GreaterThan(fluidCursorRelease));
            Assert.That(fluid.Substring(fluidMethod, fluid.IndexOf("private static uint ResolveFluidBufferGeneration", System.StringComparison.Ordinal) - fluidMethod), Does.Not.Contain("cursorLocked"));

            int equipmentMethod = equipment.IndexOf("private void TryRecordEquipmentWriteLockContention", System.StringComparison.Ordinal);
            Assert.That(equipmentMethod, Is.GreaterThanOrEqualTo(0));
            string equipmentSegment = equipment.Substring(equipmentMethod, equipment.IndexOf("private static bool TryResolveEquipmentBuffer", System.StringComparison.Ordinal) - equipmentMethod);
            Assert.That(equipmentSegment, Does.Not.Contain("ringLocked"));
            Assert.That(equipmentSegment, Does.Not.Contain("cursorLocked"));
            int equipmentCursorAcquire = equipmentSegment.IndexOf("TryAcquireWriteLock(in _equipmentTelemetryCursorHandle", System.StringComparison.Ordinal);
            int equipmentCursorRelease = equipmentSegment.IndexOf("ReleaseWriteLock(in _equipmentTelemetryCursorHandle", System.StringComparison.Ordinal);
            int equipmentRingAcquire = equipmentSegment.IndexOf("TryAcquireWriteLock(in _equipmentTelemetryRingHandle", System.StringComparison.Ordinal);
            Assert.That(equipmentCursorAcquire, Is.GreaterThanOrEqualTo(0));
            Assert.That(equipmentCursorRelease, Is.GreaterThan(equipmentCursorAcquire));
            Assert.That(equipmentRingAcquire, Is.GreaterThan(equipmentCursorRelease));
        }

        [Test]
        public void GroundRadarPendingPublish_UsesSingleWriteLockCopyPhases()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "GroundPenetratingRadarRuntime.cs");

            int publishIndex = source.IndexOf("private bool TryPublishRadarPendingJob", System.StringComparison.Ordinal);
            int scheduleIndex = source.IndexOf("private void ScheduleRadarJob", System.StringComparison.Ordinal);
            Assert.That(publishIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(scheduleIndex, Is.GreaterThan(publishIndex));

            string publishSegment = source.Substring(publishIndex, scheduleIndex - publishIndex);
            Assert.That(publishSegment, Does.Contain("TryCopyPendingBufferToVault"));
            Assert.That(publishSegment, Does.Not.Contain("hitsLocked"));
            Assert.That(publishSegment, Does.Not.Contain("signalLocked"));
            Assert.That(publishSegment, Does.Not.Contain("ageLocked"));
            Assert.That(publishSegment, Does.Not.Contain("oreTypesLocked"));
            Assert.That(publishSegment, Does.Not.Contain("pingGpuLocked"));
            Assert.That(publishSegment, Does.Not.Contain("countersLocked"));
            Assert.That(publishSegment, Does.Not.Contain("maxSignalLocked"));

            int maxSignalPublish = publishSegment.IndexOf("in _maxSignalStrengthHandle", System.StringComparison.Ordinal);
            int countersPublish = publishSegment.IndexOf("in _gprCountersHandle", System.StringComparison.Ordinal);
            int helperAcquire = publishSegment.IndexOf("TryAcquireWriteLock(in handle", System.StringComparison.Ordinal);
            int helperRelease = publishSegment.IndexOf("ReleaseWriteLock(in handle", System.StringComparison.Ordinal);
            Assert.That(maxSignalPublish, Is.GreaterThanOrEqualTo(0));
            Assert.That(countersPublish, Is.GreaterThan(maxSignalPublish));
            Assert.That(helperAcquire, Is.GreaterThan(countersPublish));
            Assert.That(helperRelease, Is.GreaterThan(helperAcquire));
        }

        [Test]
        public void ProximityColliderDistanceJob_HoldsOnlyResultWriterAcrossJob()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "ProximityColliderSystem.cs");

            int acquireIndex = source.IndexOf("private bool TryAcquireJobBuffers", System.StringComparison.Ordinal);
            int readIndex = source.IndexOf("private bool TryReadPositions", System.StringComparison.Ordinal);
            Assert.That(acquireIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(readIndex, Is.GreaterThan(acquireIndex));

            string acquireSegment = source.Substring(acquireIndex, readIndex - acquireIndex);
            Assert.That(acquireSegment, Does.Contain("NativeArray<byte>.Copy(_prevStatus, 0, prevStatusWrite, 0, _pointCount)"));
            Assert.That(acquireSegment, Does.Contain("vault.TryLockBuffer(PositionsBufferId, VaultOwnerSystemId)"));
            Assert.That(acquireSegment, Does.Contain("vault.TryLockBuffer(PrevStatusBufferId, VaultOwnerSystemId)"));
            Assert.That(acquireSegment, Does.Not.Contain("vault.TryAcquireWriteLock(in _positionsHandle"));
            Assert.That(acquireSegment, Does.Not.Contain("vault.TryAcquireWriteLock(in _prevStatusHandle, VaultOwnerSystemId, out prevStatus"));

            int prevAcquire = acquireSegment.IndexOf("TryAcquireWriteLock(in _prevStatusHandle", System.StringComparison.Ordinal);
            int prevRelease = acquireSegment.IndexOf("ReleaseWriteLock(in _prevStatusHandle", prevAcquire, System.StringComparison.Ordinal);
            int positionsPin = acquireSegment.IndexOf("TryLockBuffer(PositionsBufferId", prevRelease, System.StringComparison.Ordinal);
            int prevPin = acquireSegment.IndexOf("TryLockBuffer(PrevStatusBufferId", positionsPin, System.StringComparison.Ordinal);
            int resultAcquire = acquireSegment.IndexOf("TryAcquireWriteLock(in _jobResultsHandle", prevPin, System.StringComparison.Ordinal);
            Assert.That(prevAcquire, Is.GreaterThanOrEqualTo(0));
            Assert.That(prevRelease, Is.GreaterThan(prevAcquire));
            Assert.That(positionsPin, Is.GreaterThan(prevRelease));
            Assert.That(prevPin, Is.GreaterThan(positionsPin));
            Assert.That(resultAcquire, Is.GreaterThan(prevPin));

            int releaseIndex = source.IndexOf("private void ReleaseJobBufferLocks", System.StringComparison.Ordinal);
            int releaseEnd = source.IndexOf("private void ReleaseProximityBuffers()", System.StringComparison.Ordinal);
            Assert.That(releaseIndex, Is.GreaterThan(readIndex));
            Assert.That(releaseEnd, Is.GreaterThan(releaseIndex));
            string releaseSegment = source.Substring(releaseIndex, releaseEnd - releaseIndex);
            Assert.That(releaseSegment, Does.Contain("ReleaseWriteLock(in _jobResultsHandle"));
            Assert.That(releaseSegment, Does.Contain("TryUnlockBuffer(PrevStatusBufferId"));
            Assert.That(releaseSegment, Does.Contain("TryUnlockBuffer(PositionsBufferId"));
        }

        [Test]
        public void PrologueHydrationProxy_UsesContinuousSurvivalPressure()
        {
            string contracts = ReadProjectFile("Assets", "_Project", "Scripts", "Core", "Contracts", "PrologueSequenceContracts.cs");
            string bridge = ReadProjectFile("Assets", "_Project", "Scripts", "Core", "PrologueSequenceRegistryBridge.cs");
            string director = ReadProjectFile("Assets", "_Project", "Scripts", "Narrative", "Prologue", "AwaitableDropSequenceDirector.cs");

            Assert.That(contracts, Does.Contain("float SurvivalProxyPressure01 { get; }"));
            Assert.That(contracts, Does.Contain("bool IsSurvivalProxySurfaceActive { get; }"));
            Assert.That(contracts, Does.Contain("This member is a compatibility alias."));
            Assert.That(contracts, Does.Contain("SurvivalProxySurface = 1"));
            Assert.That(bridge, Does.Contain("ResolveSurvivalProxyPressureWithHysteresis"));
            Assert.That(bridge, Does.Contain("ReadSurvivalProxyPressurePolicy"));
            Assert.That(bridge, Does.Contain("public bool IsSurvivalProxySurfaceActive"));
            Assert.That(bridge, Does.Contain("public bool IsLowTier => IsSurvivalProxySurfaceActive;"));
            Assert.That(bridge, Does.Contain("float survivalPressure01 = 1.0f - SmoothStep01(qualityWeight01);"));
            Assert.That(bridge, Does.Contain("forcedLowMemory = pressure01 >= ForcedMemoryPressureThreshold01;"));
            Assert.That(director, Does.Contain("_runtime.SurvivalProxyPressure01"));
            Assert.That(director, Does.Contain("PrologueSequenceQualityPolicy.SurvivalProxyActivationThreshold01"));
            Assert.That(director, Does.Not.Contain("_runtime.IsLowTier"));
            Assert.That(director, Does.Not.Contain("LowTierProxySurface"));
            Assert.That(bridge, Does.Not.Contain("ForcedMemoryQualityThreshold01"));
            Assert.That(bridge, Does.Not.Contain("qualityWeight01 <= ForcedMemory"));
            Assert.That(bridge, Does.Not.Contain("ResolveLowTierWithHysteresis"));
            Assert.That(bridge, Does.Not.Contain("ReadLowTierPolicy"));
        }

        [Test]
        public void HardwareProfilerAndBootstrap_UseStartupQualityWeightNotForceLowTier()
        {
            string profiler = ReadProjectFile("Assets", "_Project", "Scripts", "Optimization", "HardwareProfiler.cs");
            string bootstrap = ReadProjectFile("Assets", "_Project", "Scripts", "Bootstrap", "GameBootstrapper.cs");

            Assert.That(profiler, Does.Contain("ResolveStartupQualityWeight01"));
            Assert.That(profiler, Does.Contain("StartupSurvivalPressureByte"));
            Assert.That(profiler, Does.Contain("ResolvePhysicsBenchmarkQualityWeight01"));
            Assert.That(profiler, Does.Contain("ResolveSystemInfoSurvivalPressure01"));
            Assert.That(profiler, Does.Not.Contain("ShouldForceLowTier"));
            Assert.That(profiler, Does.Not.Contain("ForceLowTier"));
            Assert.That(bootstrap, Does.Contain("startupQualityWeight01"));
            Assert.That(bootstrap, Does.Contain("ResolveBenchmarkScalabilityTier("));
            Assert.That(bootstrap, Does.Not.Contain("ShouldForceLowTier"));
        }

        [Test]
        public void LodAndImpostorThresholds_ConsumeContinuousQuality()
        {
            string lod = ReadProjectFile("Assets", "_Project", "Scripts", "World", "LODSystemManager.cs");
            string impostor = ReadProjectFile("Assets", "_Project", "Scripts", "World", "ImpostorSystem.cs");
            string renderer = ReadProjectFile("Assets", "_Project", "Scripts", "World", "HectonOctahedralImpostorRenderer.cs");
            string culling = ReadProjectFile("Assets", "_Project", "Scripts", "Graphics", "Culling", "InstanceCullingService.cs");
            string compute = ReadShader("InstanceCulling.compute");

            Assert.That(lod, Does.Contain("public float QualityWeight01 => _runtimeQualityWeight01;"));
            Assert.That(lod, Does.Contain("ResolveLODBiasFromQualityWeight"));
            Assert.That(lod, Does.Contain("math.lerp(1.5f, 0.7f, q)"));
            Assert.That(lod, Does.Contain("float ordinalWeight01 = math.saturate(rawPreset * 0.5f);"));
            Assert.That(lod, Does.Contain("return math.select(0.62f, math.saturate(curvedWeight01), (uint)rawPreset <= 2u);"));
            Assert.That(lod, Does.Not.Contain("case LODQualityPreset.Low"));
            Assert.That(lod, Does.Not.Contain("case LODQualityPreset.Medium"));
            Assert.That(lod, Does.Not.Contain("case LODQualityPreset.High"));
            Assert.That(impostor, Does.Contain("lodSystemManager.QualityWeight01"));
            Assert.That(impostor, Does.Contain("math.lerp(lowScale, highScale, Smooth01(lodSystemManager.QualityWeight01))"));
            Assert.That(impostor, Does.Not.Contain("switch (lodSystemManager.QualityPreset)"));
            Assert.That(lod, Does.Not.Contain("GameBootstrapper.TryGetCurrentPlayerTransform"));
            Assert.That(impostor, Does.Not.Contain("GameBootstrapper.TryGetCurrentPlayerTransform"));
            Assert.That(renderer, Does.Not.Contain("ResolveCullingQualityTier"));
            Assert.That(culling, Does.Contain("ResolveSurvivalDistanceWeight01"));
            Assert.That(culling, Does.Not.Contain("_QualityTierId"));
            Assert.That(culling, Does.Not.Contain("descriptor.QualityTier)"));
            Assert.That(compute, Does.Not.Contain("_HectonQualityTier"));
        }

        [Test]
        public void DynamicResolutionScaler_PresetCompatibilityMapsToContinuousRenderScale()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "DynamicResolutionScaler.cs");

            Assert.That(source, Does.Contain("ResolveMinimumRenderScaleFromPreset"));
            Assert.That(source, Does.Contain("ResolveMinimumRenderScaleFromQualityWeight"));
            Assert.That(source, Does.Contain("ResolveQualityPresetWeight01"));
            Assert.That(source, Does.Contain("math.lerp(0.7f, 0.9f, q)"));
            Assert.That(source, Does.Contain("math.select(0.5f, math.saturate(ordinalWeight), (uint)rawPreset <= 2u)"));
            Assert.That(source, Does.Not.Contain("GetMinimumRenderScaleForPreset"));
            Assert.That(source, Does.Not.Contain("case LODQualityPreset.Low"));
            Assert.That(source, Does.Not.Contain("case LODQualityPreset.Medium"));
            Assert.That(source, Does.Not.Contain("case LODQualityPreset.High"));
        }

        [Test]
        public void DynamicResolutionScaler_RenderScaleApplyUsesColdRuntimeLatch()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "DynamicResolutionScaler.cs");
            string awakeBody = ExtractMethodBody(source, "private void Awake()");
            string enableBody = ExtractMethodBody(source, "private void OnEnable()");
            string disableBody = ExtractMethodBody(source, "private void OnDisable()");
            string lateFrameBody = ExtractMethodBody(source, "public void LateFrameTick()");
            string applyBody = ExtractMethodBody(source, "private void ApplyRenderScale()");

            Assert.That(source, Does.Contain("private bool _runtimeRenderScaleQueueActive;"));
            Assert.That(awakeBody, Does.Contain("_runtimeRenderScaleQueueActive = Application.isPlaying;"));
            Assert.That(enableBody, Does.Contain("_runtimeRenderScaleQueueActive = Application.isPlaying;"));
            Assert.That(disableBody, Does.Contain("_runtimeRenderScaleQueueActive = false;"));
            Assert.That(applyBody, Does.Contain("_runtimeRenderScaleQueueActive && !_applyingRenderScaleLateFrame"));
            Assert.That(applyBody, Does.Not.Contain("Application.isPlaying"));
            Assert.That(lateFrameBody, Does.Not.Contain("Application."));
        }

        [Test]
        public void SystemDispatcher_GameplayBootstrapGateUsesColdRuntimeLatch()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Core", "SystemDispatcher.cs");
            string awakeBody = ExtractMethodBody(source, "private void Awake()");
            string enableBody = ExtractMethodBody(source, "private void OnEnable()");
            string disableBody = ExtractMethodBody(source, "private void OnDisable()");
            string shutdownBody = ExtractMethodBody(source, "private void ShutdownServiceState()");
            string updateBody = ExtractMethodBody(source, "private void RunDispatcherUpdate()");

            Assert.That(source, Does.Contain("private bool _runtimeGameplayBootstrapGateActive;"));
            Assert.That(awakeBody, Does.Contain("_runtimeGameplayBootstrapGateActive = Application.isPlaying;"));
            Assert.That(enableBody, Does.Contain("_runtimeGameplayBootstrapGateActive = Application.isPlaying;"));
            Assert.That(disableBody, Does.Contain("_runtimeGameplayBootstrapGateActive = false;"));
            Assert.That(shutdownBody, Does.Contain("_runtimeGameplayBootstrapGateActive = false;"));
            Assert.That(updateBody, Does.Contain("bool blockGameplayLanes = _runtimeGameplayBootstrapGateActive &&"));
            Assert.That(updateBody, Does.Not.Contain("Application.isPlaying"));
            Assert.That(updateBody, Does.Not.Contain("GlobalRegistry.Get<"));
            Assert.That(updateBody, Does.Not.Contain("GetComponent"));
        }

        [Test]
        public void GameTickManager_GameplayBootstrapGateUsesColdRuntimeLatch()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "GameTickManager.cs");
            string awakeBody = ExtractMethodBody(source, "private void Awake()");
            string enableBody = ExtractMethodBody(source, "private void OnEnable()");
            string disableBody = ExtractMethodBody(source, "private void OnDisable()");
            string shutdownBody = ExtractMethodBody(source, "private void ShutdownServiceState(");
            string tickBody = ExtractMethodBody(source, "public void Tick(");

            Assert.That(source, Does.Contain("private bool _runtimeGameplayBootstrapGateActive;"));
            Assert.That(awakeBody, Does.Contain("_runtimeGameplayBootstrapGateActive = Application.isPlaying;"));
            Assert.That(enableBody, Does.Contain("_runtimeGameplayBootstrapGateActive = Application.isPlaying;"));
            Assert.That(disableBody, Does.Contain("_runtimeGameplayBootstrapGateActive = false;"));
            Assert.That(shutdownBody, Does.Contain("_runtimeGameplayBootstrapGateActive = false;"));
            Assert.That(tickBody, Does.Contain("if (_runtimeGameplayBootstrapGateActive &&"));
            Assert.That(tickBody, Does.Not.Contain("Application.isPlaying"));
            Assert.That(tickBody, Does.Not.Contain("GlobalRegistry.Get<"));
            Assert.That(tickBody, Does.Not.Contain("GetComponent"));
        }

        [Test]
        public void NativeMemorySentinel_FrameResolveUsesDispatcherRuntimeState()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Core", "NativeMemorySentinel.cs");
            string frameBody = ExtractMethodBody(source, "private static int ResolveCurrentFrame(");
            string timeBody = ExtractMethodBody(source, "private static float ResolveCurrentUnscaledTime()");

            Assert.That(frameBody, Does.Contain("SystemDispatcher.ActiveRuntimeInstance != null ? SystemDispatcher.CurrentFrameIndex : fallbackFrame"));
            Assert.That(timeBody, Does.Contain("SystemDispatcher.ActiveRuntimeInstance != null ? (float)SystemDispatcher.CurrentUnscaledTimeSeconds : 0f"));
            Assert.That(frameBody, Does.Not.Contain("Application.isPlaying"));
            Assert.That(timeBody, Does.Not.Contain("Application.isPlaying"));
            Assert.That(frameBody, Does.Not.Contain("GlobalRegistry.Get<"));
            Assert.That(timeBody, Does.Not.Contain("GlobalRegistry.Get<"));
            Assert.That(frameBody, Does.Not.Contain("GetComponent"));
            Assert.That(timeBody, Does.Not.Contain("GetComponent"));
        }

        [Test]
        public void SeismicShaderShake_NamesSuppressionByScalarPressure()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Environment", "HectonSeismicTideDirector.cs");

            Assert.That(source, Does.Contain("_shaderShakeSuppressed"));
            Assert.That(source, Does.Contain("requestedShaderShakeSuppressed"));
            Assert.That(source, Does.Contain("shaderShakeQuality <= 0.15f"));
            Assert.That(source, Does.Not.Contain("IsLowTierShaderShakeDisabled"));
            Assert.That(source, Does.Not.Contain("_shaderShakeDisabled"));
            Assert.That(source, Does.Not.Contain("_pendingShaderShakeDisabled"));
        }

        [Test]
        public void PlatformPressureResponses_RunInLateFrameNotSimulationTick()
        {
            string pressure = ReadProjectFile("Assets", "_Project", "Scripts", "Optimization", "VRAMPressureMonitor.cs");
            string dispatcher = ReadProjectFile("Assets", "_Project", "Scripts", "Optimization", "AssetLoadDispatcher.cs");
            string thermal = ReadProjectFile("Assets", "_Project", "Scripts", "Graphics", "Scalability", "ThermalDynamicResolutionAdapter.cs");
            string content = ReadProjectFile("Assets", "_Project", "Scripts", "Core", "Content", "ContentRuntimeServices.cs");
            string foveated = ReadProjectFile("Assets", "_Project", "Scripts", "Graphics", "VR", "FoveatedRenderCommander.cs");
            string lod = ReadProjectFile("Assets", "_Project", "Scripts", "World", "LODSystemManager.cs");
            string impostor = ReadProjectFile("Assets", "_Project", "Scripts", "World", "ImpostorSystem.cs");
            string platformGovernor = ReadProjectFile("Assets", "_Project", "Scripts", "Core", "PlatformAdaptiveBudgetGovernor.cs");
            string objectPool = ReadProjectFile("Assets", "_Project", "Scripts", "ObjectPoolManager.cs");
            string registryContracts = ReadProjectFile("Assets", "_Project", "Scripts", "Core", "GlobalRegistryContracts.cs");

            Assert.That(pressure, Does.Contain("ILateFrameTickable"));
            Assert.That(pressure, Does.Contain("public void LateFrameTick()"));
            Assert.That(pressure, Does.Contain("private bool _forceSampleQueued;"));
            Assert.That(pressure, Does.Contain("if (!_forceSampleQueued)"));
            Assert.That(pressure, Does.Contain("_forceSampleQueued = true;"));
            Assert.That(pressure, Does.Contain("hardwareHeadroomBytes = _runtimeTotalVramBudgetBytes - monitor.TotalVRAMBytes;"));
            Assert.That(pressure, Does.Contain("GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core)"));
            Assert.That(pressure, Does.Not.Contain("public void Tick(float deltaTime)"));
            Assert.That(pressure, Does.Not.Contain("GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core)"));
            Assert.That(pressure, Does.Not.Contain("internal void ForceImmediateSampleAndResponse()\r\n        {\r\n            _framesUntilSample = Mathf.Max(1, sampleIntervalFrames);"));
            Assert.That(pressure, Does.Not.Contain("internal void ForceImmediateSampleAndResponse()\n        {\n            _framesUntilSample = Mathf.Max(1, sampleIntervalFrames);"));
            Assert.That(pressure, Does.Not.Contain("SystemInfo.graphicsMemorySize * 1024L"));

            Assert.That(dispatcher, Does.Contain("ILateFrameTickable"));
            Assert.That(dispatcher, Does.Contain("public void LateFrameTick()"));
            Assert.That(dispatcher, Does.Contain("QueueUiMipBiasGateEvaluation();"));
            Assert.That(dispatcher, Does.Contain("ResolveGraphicsBudgetBytes(0);"));
            Assert.That(dispatcher, Does.Contain("GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core)"));
            Assert.That(dispatcher, Does.Not.Contain("public void Tick(float deltaTime)\r\n        {\r\n            EvaluateUiMipBiasGate();"));
            Assert.That(dispatcher, Does.Not.Contain("public void Tick(float deltaTime)\n        {\n            EvaluateUiMipBiasGate();"));
            Assert.That(dispatcher, Does.Not.Contain("if (IsUiIconGroup(assetKey))\r\n                EvaluateUiMipBiasGate();"));
            Assert.That(dispatcher, Does.Not.Contain("if (IsUiIconGroup(assetKey))\n                EvaluateUiMipBiasGate();"));
            Assert.That(dispatcher, Does.Not.Contain("if (_graphicsBudgetBytes <= 0L)\r\n                RefreshGraphicsBudgetBytes();"));
            Assert.That(dispatcher, Does.Not.Contain("if (_graphicsBudgetBytes <= 0L)\n                RefreshGraphicsBudgetBytes();"));

            Assert.That(thermal, Does.Contain("CacheGraphicsCapabilitySnapshotCold();"));
            Assert.That(
                thermal.Contains("if (_stressEwmaScheduled)\n                return;") ||
                thermal.Contains("if (_stressEwmaScheduled)\r\n                return;"),
                Is.True);
            Assert.That(thermal, Does.Contain("_coldBilateralDrsRouteAllowed = !Application.isMobilePlatform && SystemInfo.supportsComputeShaders;"));
            Assert.That(thermal, Does.Not.Contain("ResolveFsrUpscalerAllowed"));
            Assert.That(thermal, Does.Not.Contain("int graphicsMemoryMb = SystemInfo.graphicsMemorySize;"));
            Assert.That(thermal, Does.Not.Contain("_coldGraphicsMemoryMb"));
            Assert.That(thermal, Does.Not.Contain("Application.isMobilePlatform || !SystemInfo.supportsComputeShaders"));

            Assert.That(content, Does.Contain("s_coldGraphicsMemoryMb"));
            Assert.That(content, Does.Contain("LogMissingRuntimeDataVault(hash);"));
            Assert.That(content, Does.Not.Contain("SystemInfo.graphicsMemorySize,\r\n                HectonXRRuntimeState.IsXRActive"));
            Assert.That(content, Does.Not.Contain("SystemInfo.graphicsMemorySize,\n                HectonXRRuntimeState.IsXRActive"));
            Assert.That(content, Does.Not.Contain("if (_dataVault == null)\r\n                CacheDependencies();"));
            Assert.That(content, Does.Not.Contain("if (_dataVault == null)\n                CacheDependencies();"));

            Assert.That(foveated, Does.Contain("quest2FoveationFloor01"));
            Assert.That(foveated, Does.Contain("ResolveQuest2FoveationFloor01"));
            Assert.That(foveated, Does.Contain("_coldFoveatedCaps"));
            Assert.That(foveated, Does.Contain("CacheRuntimeCapabilitySnapshotCold();"));
            Assert.That(foveated, Does.Not.Contain("lockQuest2HighFoveation"));
            Assert.That(foveated, Does.Not.Contain("FoveatedRenderingCaps caps = SystemInfo.foveatedRenderingCaps;"));
            Assert.That(foveated, Does.Not.Contain("bool xrActive = XRSettings.enabled && XRSettings.isDeviceActive;"));
            Assert.That(foveated, Does.Not.Contain("if (quest2Runtime ||\r\n                thermalPressure"));
            Assert.That(foveated, Does.Not.Contain("if (quest2Runtime ||\n                thermalPressure"));

            Assert.That(lod, Does.Contain("FlushQualityVisualSync();"));
            Assert.That(lod, Does.Contain("public float QualityWeight01 => _runtimeQualityWeight01;"));
            Assert.That(lod, Does.Contain("QualitySettings.lodBias = targetBias;"));
            Assert.That(lod, Does.Contain("DistanceMath.PushShaderMathLod(qualityWeight01);"));
            Assert.That(lod, Does.Not.Contain("QualitySettings.lodBias = ResolveLODBiasFromQualityWeight(qualityWeight01);"));
            Assert.That(lod, Does.Not.Contain("DistanceMath.PushShaderMathLod(qualityWeight01);\r\n\r\n            _dynamicResolutionScaler?.SetQualityPreset(preset);"));
            Assert.That(lod, Does.Not.Contain("DistanceMath.PushShaderMathLod(qualityWeight01);\n\n            _dynamicResolutionScaler?.SetQualityPreset(preset);"));

            Assert.That(impostor, Does.Contain("Smooth01(lodSystemManager.QualityWeight01)"));
            Assert.That(impostor, Does.Contain("pool.TryGetPooledRootRenderer(billboard, out renderer)"));
            Assert.That(impostor, Does.Not.Contain("billboard.TryGetComponent(out renderer)"));
            Assert.That(impostor, Does.Not.Contain("switch (lodSystemManager.QualityPreset)"));

            Assert.That(registryContracts, Does.Contain("bool TryGetPooledRootRenderer(GameObject instance, out Renderer renderer);"));
            Assert.That(objectPool, Does.Contain("private readonly Dictionary<GameObject, PoolItemMarker> _poolMarkerCache"));
            Assert.That(objectPool, Does.Contain("marker.Initialize(prefabId, rootRenderer, rootDespawnTimer, s_poolableCache);"));
            Assert.That(objectPool, Does.Contain("public bool TryGetPooledRootRenderer(GameObject instance, out Renderer renderer)"));
            Assert.That(objectPool, Does.Contain("renderer = marker.RootRenderer;"));
            Assert.That(objectPool, Does.Contain("NotifySpawn(marker);"));
            Assert.That(objectPool, Does.Contain("NotifyDespawn(marker);"));
            Assert.That(objectPool, Does.Contain("public int PoolableCount => _poolables.Length;"));
            Assert.That(objectPool, Does.Contain("pool.capacity = Mathf.Max(0, pool.capacity - 1);"));
            Assert.That(objectPool, Does.Contain("if (poolable != null)"));

            Assert.That(platformGovernor, Does.Contain("CacheHardwareBudgetProfileCold();"));
            Assert.That(platformGovernor, Does.Contain("_coldRecommendedVramBudgetBytes"));
            Assert.That(platformGovernor, Does.Contain("_coldTargetFrameTimeMs"));
            Assert.That(platformGovernor, Does.Not.Contain("public static void SampleAndApply(float deltaTime)\r\n        {\r\n            HardwareTierDetector.EnsureInitialized();"));
            Assert.That(platformGovernor, Does.Not.Contain("public static void SampleAndApply(float deltaTime)\n        {\n            HardwareTierDetector.EnsureInitialized();"));
            Assert.That(platformGovernor, Does.Not.Contain("HardwareTierDetector.IsQuest3Like\r\n                    ? HardwareProfileCatalog.Quest3BaselineRenderScaleMilli"));
            Assert.That(platformGovernor, Does.Not.Contain("HardwareTierDetector.IsQuest3Like\n                    ? HardwareProfileCatalog.Quest3BaselineRenderScaleMilli"));
        }

        [Test]
        public void VisualSyncConstantBufferSupport_IsColdCached()
        {
            string accessibility = ReadProjectFile("Assets", "_Project", "Scripts", "Input", "AccessibilitySettings.cs");
            string flora = ReadProjectFile("Assets", "_Project", "Scripts", "World", "FloraAmbientSway", "FloraAmbientSwayRuntime.cs");
            string water = ReadProjectFile("Assets", "_Project", "Scripts", "Rendering", "WaterOptics", "WaterOpticsRuntime.cs");

            Assert.That(accessibility, Does.Contain("private bool _supportsConstantBuffers;"));
            Assert.That(accessibility, Does.Contain("CacheGraphicsCapabilitiesCold();"));
            Assert.That(accessibility, Does.Contain("if (!_supportsConstantBuffers || !HasValidBuffers())"));
            Assert.That(CountOccurrences(accessibility, "SystemInfo.supportsSetConstantBuffer"), Is.EqualTo(1));

            Assert.That(flora, Does.Contain("private bool _supportsConstantBuffers;"));
            Assert.That(flora, Does.Contain("CacheGraphicsCapabilitiesCold();"));
            Assert.That(flora, Does.Contain("if (!_supportsConstantBuffers)"));
            Assert.That(CountOccurrences(flora, "SystemInfo.supportsSetConstantBuffer"), Is.EqualTo(1));

            Assert.That(water, Does.Contain("private bool _supportsConstantBuffers;"));
            Assert.That(water, Does.Contain("CacheGraphicsCapabilitiesCold();"));
            Assert.That(water, Does.Contain("if (!_supportsConstantBuffers)"));
            Assert.That(CountOccurrences(water, "SystemInfo.supportsSetConstantBuffer"), Is.EqualTo(1));
        }

        [Test]
        public void MarineSnowComputeCapabilities_AreColdCachedBeforeLateFrameUse()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "VFX", "HectonMarineSnowRenderer.cs");
            string underwater = ReadProjectFile("Assets", "_Project", "Scripts", "HectonUnderwaterVisuals.cs");

            Assert.That(source, Does.Contain("CacheGraphicsCapabilitySnapshotCold();"));
            Assert.That(source, Does.Contain("private bool _coldSupportsComputeShaders;"));
            Assert.That(source, Does.Contain("private TextureFormat _coldEmptyCaveSdfTextureFormat = TextureFormat.Alpha8;"));
            Assert.That(source, Does.Contain("private TextureFormat _coldEmptyAbyssalFlowTextureFormat = TextureFormat.RGBA32;"));
            Assert.That(source, Does.Contain("!_coldSupportsComputeShaders"));
            Assert.That(source, Does.Contain("TextureFormat textureFormat = _coldEmptyCaveSdfTextureFormat;"));
            Assert.That(source, Does.Contain("TextureFormat textureFormat = _coldEmptyAbyssalFlowTextureFormat;"));
            Assert.That(source, Does.Not.Contain("!HardwareTierDetector.AllowHighResourceComputeShaders"));
            Assert.That(source, Does.Not.Contain("TextureFormat textureFormat = SystemInfo.SupportsTextureFormat(TextureFormat.R8)"));
            Assert.That(source, Does.Not.Contain("TextureFormat textureFormat = SystemInfo.SupportsTextureFormat(TextureFormat.RGBAHalf)"));

            Assert.That(source, Does.Contain("private void ResolveTargetCameraCold()"));
            Assert.That(source, Does.Contain("if (!HasCachedTargetCamera())"));
            int visualTickIndex = source.IndexOf("private void RunMarineSnowVisualTick(float dt)", System.StringComparison.Ordinal);
            Assert.That(visualTickIndex, Is.GreaterThanOrEqualTo(0));
            string visualTickEntry = source.Substring(
                visualTickIndex,
                System.Math.Min(700, source.Length - visualTickIndex));
            Assert.That(visualTickEntry, Does.Not.Contain("ResolveTargetCameraCold"));
            Assert.That(visualTickEntry, Does.Not.Contain("ResolveComponent"));
            Assert.That(visualTickEntry, Does.Not.Contain("TryGetComponent"));

            Assert.That(source, Does.Contain("public void BindTargetCamera(Camera cameraComponent)"));
            Assert.That(underwater, Does.Contain("underwaterMarineSnow.BindTargetCamera(mainCamera);"));
            Assert.That(underwater, Does.Contain("RequestRuntimeVisualOwnerResolveIfMissing();"));
            Assert.That(underwater, Does.Contain("ResolveRuntimeVisualOwnersOnColdCadence();"));
            Assert.That(underwater, Does.Not.Contain("underwaterMarineSnow.BindTargetCamera(mainCamera.transform);"));
        }

        [Test]
        public void UnderwaterHudFogLuminanceReadbackStorage_IsSlowPhaseOnly()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "HectonUnderwaterVisuals.cs");
            string updateBody = ExtractMethodBody(source, "private void UpdateHudFogLuminanceDownsample()");
            string slowBody = ExtractMethodBody(source, "public void SlowTick()");
            string flushBody = ExtractMethodBody(source, "private void FlushHudFogLuminanceReadbackRepairSlow()");
            string ensureBody = ExtractMethodBody(source, "private void EnsureHudFogLuminanceReadbackData()");

            Assert.That(source, Does.Contain("private bool _hudFogLuminanceReadbackRepairRequested = true;"));
            Assert.That(updateBody, Does.Contain("QueueHudFogLuminanceReadbackRepair();"));
            Assert.That(updateBody, Does.Not.Contain("EnsureHudFogLuminanceReadbackData();"));
            Assert.That(updateBody, Does.Not.Contain("new NativeArray<float>"));
            Assert.That(slowBody, Does.Contain("FlushHudFogLuminanceReadbackRepairSlow();"));
            Assert.That(flushBody, Does.Contain("EnsureHudFogLuminanceReadbackData();"));
            Assert.That(ensureBody, Does.Contain("new NativeArray<float>("));
        }

        [Test]
        public void OpenXrManualOverrideLever_UsesRuntimeXrStateNotXrSettingsInTick()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "VR", "OpenXRManualOverrideLever.cs");

            Assert.That(source, Does.Contain("_xrActiveThisFrame = HectonXRRuntimeState.IsXRActive;"));
            Assert.That(source, Does.Not.Contain("XRSettings.enabled"));
            Assert.That(source, Does.Not.Contain("XRSettings.isDeviceActive"));
            Assert.That(source, Does.Not.Contain("using UnityEngine.XR;"));
        }

        [Test]
        public void FaunaAndGeologyTierNames_DoNotEncodeHardwareQualityForks()
        {
            string cognition = ReadProjectFile("Assets", "_Project", "Scripts", "Fauna", "PredatorCognitionDomain.cs");
            string compatibility = ReadProjectFile("Assets", "_Project", "Scripts", "Fauna", "FaunaBrain.Compatibility.cs");
            string geology = ReadProjectFile("Assets", "_Project", "Scripts", "WorldGenerativeGeologyTerrainSeamApplier.cs");
            string seamJobs = ReadProjectFile("Assets", "_Project", "Scripts", "World", "HybridTerrainSeamJobs.cs");

            Assert.That(cognition, Does.Contain("ApexSmoothSteering"));
            Assert.That(compatibility, Does.Contain("ApexSmoothSteering"));
            Assert.That(cognition, Does.Not.Contain("HighTierSmoothSteering"));
            Assert.That(compatibility, Does.Not.Contain("HighTierSmoothSteering"));
            Assert.That(geology, Does.Contain("maskDetailActive"));
            Assert.That(geology, Does.Contain("visualSamplingSuppressed"));
            Assert.That(seamJobs, Does.Contain("VisualSamplingSuppressed"));
            Assert.That(geology, Does.Not.Contain("highTierMaskDetail"));
            Assert.That(geology, Does.Not.Contain("lowTierVisualOnly"));
            Assert.That(seamJobs, Does.Not.Contain("LowTierVisualOnly"));
        }

        [Test]
        public void DroneFleetConstruction_UsesContinuousQualityEndpoints()
        {
            string manager = ReadProjectFile("Assets", "_Project", "Scripts", "Construction", "DroneFleetManager.cs");
            string kernel = ReadProjectFile("Assets", "_Project", "Scripts", "Construction", "DroneFleetNavigationKernel.cs");
            string tuner = ReadProjectFile("Assets", "_Project", "Scripts", "Editor", "FleetAutomationTunerWindow.cs");
            string construction = ReadProjectFile("Assets", "_Project", "Scripts", "ConstructionManager.cs");

            Assert.That(kernel, Does.Contain("public float SurvivalSteeringHz;"));
            Assert.That(kernel, Does.Contain("public float StandardSteeringHz;"));
            Assert.That(kernel, Does.Contain("public float HighFidelitySteeringHz;"));
            Assert.That(kernel, Does.Contain("public float OverkillSteeringHz;"));
            Assert.That(kernel, Does.Contain("public float SurvivalSolveBudget;"));
            Assert.That(kernel, Does.Contain("public float OverkillSolveBudget;"));
            Assert.That(kernel, Does.Not.Contain("public float LowTierSteeringHz;"));
            Assert.That(kernel, Does.Not.Contain("public float UltraTierSolveBudget;"));

            Assert.That(manager, Does.Contain("ResolveGlobalQualityWeight()"));
            Assert.That(manager, Does.Contain("math.lerp(SurvivalPhantomDroneCount, PhantomDroneCount, quality)"));
            Assert.That(manager, Does.Contain("math.lerp(SurvivalDroneRenderDistanceMeters, HighFidelityDroneRenderDistanceMeters, quality)"));
            Assert.That(manager, Does.Not.Contain("LowTierPhantomDroneCount"));
            Assert.That(manager, Does.Not.Contain("HighTierDroneRenderDistanceMeters"));
            Assert.That(manager, Does.Not.Contain("HardwareTierDetector.AllowHighResourceComputeShaders"));
            Assert.That(manager, Does.Not.Contain("TryAcquireDroneRenderUploadBuffers"));
            Assert.That(manager, Does.Contain("TryPrepareAndUploadDroneRenderInstances"));
            Assert.That(manager, Does.Contain("TryPrepareAndUploadDroneCullingStates"));
            Assert.That(manager, Does.Contain("\"LowTierSteeringHz\""));
            Assert.That(manager, Does.Contain("\"UltraTierSolveBudget\""));

            string renderBody = ExtractMethodBody(manager, "private static void RenderRealHeadlessFleet()");
            Assert.That(renderBody, Does.Contain("TryPrepareAndUploadDroneRenderInstances(droneRenderMatrices, droneStates)"));
            Assert.That(renderBody, Does.Contain("TryPrepareAndUploadDroneCullingStates(droneStates)"));
            Assert.That(renderBody, Does.Not.Contain("renderUploadVault"));
            Assert.That(renderBody, Does.Not.Contain("ReleaseWriteLock(in s_DroneCullingStatesHandle"));
            Assert.That(renderBody, Does.Not.Contain("ReleaseWriteLock(in s_DroneRenderInstancesHandle"));

            Assert.That(tuner, Does.Contain("_tuning.OverkillSolveBudget"));
            Assert.That(tuner, Does.Not.Contain("_tuning.UltraTierSolveBudget"));

            Assert.That(construction, Does.Contain("ModuleDeconstructFlagDfsSkippedByBudget"));
            Assert.That(construction, Does.Not.Contain("ModuleDeconstructFlagDfsSkippedLowTier"));
        }

        [Test]
        public void SaveThumbnail_CapturesContinuouslyInsteadOfLowTierSkip()
        {
            string thumbnail = ReadProjectFile("Assets", "_Project", "Scripts", "SaveThumbnailSystem.cs");
            string signals = ReadProjectFile("Assets", "_Project", "Scripts", "Core", "Signals", "GlobalSignalPayloads.UiSaveWorld.cs");

            Assert.That(thumbnail, Does.Contain("ThumbnailJpegQualitySurvival"));
            Assert.That(thumbnail, Does.Contain("ThumbnailJpegQualityVisualOverkill"));
            Assert.That(thumbnail, Does.Contain("ResolveThumbnailJpegQuality()"));
            Assert.That(thumbnail, Does.Contain("quality * quality * (3f - 2f * quality)"));
            Assert.That(thumbnail, Does.Not.Contain("ShouldSkipScreenshotForCurrentTier"));
            Assert.That(thumbnail, Does.Not.Contain("ThumbnailCaptureQualityThreshold01"));
            Assert.That(thumbnail, Does.Not.Contain("CaptureStatus.LowTierSkipped"));
            Assert.That(thumbnail, Does.Not.Contain("LowTierSkipped"));
            Assert.That(thumbnail, Does.Not.Contain("JpegQuality ="));

            Assert.That(signals, Does.Contain("DeferredByQuality"));
            Assert.That(signals, Does.Contain("QualityDeferredFlag"));
            Assert.That(signals, Does.Not.Contain("SkippedLowTier"));
            Assert.That(signals, Does.Not.Contain("LowTierFlag"));
        }

        [Test]
        public void SargassumCrestFacade_UsesContinuousResolutionAndComputeSupport()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "SargassumCrestDampingController.cs");

            Assert.That(source, Does.Contain("FacadeResolutionSurvivalScale"));
            Assert.That(source, Does.Contain("ResolveFacadeResolutionDimension"));
            Assert.That(source, Does.Contain("ResolveFacadeQualityWeight01()"));
            Assert.That(source, Does.Contain("Mathf.Lerp(FacadeResolutionSurvivalScale, FacadeResolutionVisualOverkillScale, curve)"));
            Assert.That(source, Does.Contain("!compute.IsSupported(kernel)"));
            Assert.That(source, Does.Not.Contain("HardwareTierDetector.AllowHighResourceComputeShaders"));
        }

        [Test]
        public void SargassumCrestFacade_KernelResolutionIsSlowPhaseOnly()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "SargassumCrestDampingController.cs");
            string slowBody = ExtractMethodBody(source, "public void SlowTick()");
            string lateFrameBody = ExtractMethodBody(source, "public void LateFrameTick()");
            string dispatchBody = ExtractMethodBody(source, "private void DispatchFacadeBake(");
            string repairBody = ExtractMethodBody(source, "private void FlushFacadeBakeKernelRepairSlow()");

            Assert.That(slowBody, Does.Contain("FlushFacadeBakeKernelRepairSlow();"));
            Assert.That(lateFrameBody, Does.Contain("RefreshFacadeTexturesCached(force);"));
            Assert.That(dispatchBody, Does.Contain("QueueFacadeBakeKernelRepair();"));
            Assert.That(dispatchBody, Does.Not.Contain("ResolveSupportedKernel("));
            Assert.That(dispatchBody, Does.Not.Contain("ResolveKernelThreadGroupSizes("));
            Assert.That(dispatchBody, Does.Not.Contain(".FindKernel("));
            Assert.That(dispatchBody, Does.Not.Contain(".HasKernel("));
            Assert.That(dispatchBody, Does.Not.Contain(".GetKernelThreadGroupSizes("));
            Assert.That(repairBody, Does.Contain("ResolveSupportedKernel(_facadeBakeCompute, \"CSMain\")"));
            Assert.That(repairBody, Does.Contain("ResolveKernelThreadGroupSizes("));
        }

        [Test]
        public void PDAMapTab_SonarKernelResolutionIsSlowPhaseOnly()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "PDAMapTab.cs");
            string lateFrameBody = ExtractMethodBody(source, "public void LateFrameTick()");
            string renderBody = ExtractMethodBody(source, "private void RenderPointCloud()");
            string dispatchBody = ExtractMethodBody(source, "private bool DispatchSonarPointCloud(");
            string slowBody = ExtractMethodBody(source, "public void SlowTick()");
            string flushBody = ExtractMethodBody(source, "private void FlushSonarComputeKernelRepairSlow()");
            string resolveBody = ExtractMethodBody(source, "private bool TryResolveSonarComputeKernels()");
            string registerBody = ExtractMethodBody(source, "private void RegisterToTickManager()");
            string unregisterBody = ExtractMethodBody(source, "private void UnregisterFromTickManager()");

            Assert.That(source, Does.Contain("ILateFrameTickable, ISlowTickable"));
            Assert.That(lateFrameBody, Does.Contain("RenderPointCloud();"));
            Assert.That(lateFrameBody, Does.Not.Contain("TryResolveSonarComputeKernels"));
            Assert.That(renderBody, Does.Contain("HasSonarComputeKernelsReady()"));
            Assert.That(renderBody, Does.Contain("QueueSonarComputeKernelRepair();"));
            Assert.That(renderBody, Does.Not.Contain("TryResolveSonarComputeKernels"));
            Assert.That(renderBody, Does.Not.Contain(".FindKernel("));
            Assert.That(renderBody, Does.Not.Contain(".HasKernel("));
            Assert.That(renderBody, Does.Not.Contain(".GetKernelThreadGroupSizes("));
            Assert.That(dispatchBody, Does.Contain("HasSonarComputeKernelsReady()"));
            Assert.That(dispatchBody, Does.Contain("QueueSonarComputeKernelRepair();"));
            Assert.That(dispatchBody, Does.Not.Contain("TryResolveSonarComputeKernels"));
            Assert.That(dispatchBody, Does.Not.Contain(".FindKernel("));
            Assert.That(dispatchBody, Does.Not.Contain(".HasKernel("));
            Assert.That(dispatchBody, Does.Not.Contain(".GetKernelThreadGroupSizes("));
            Assert.That(slowBody, Does.Contain("FlushSonarComputeKernelRepairSlow();"));
            Assert.That(flushBody, Does.Contain("TryResolveSonarComputeKernels()"));
            Assert.That(resolveBody, Does.Contain("sonarMapCompute.FindKernel"));
            Assert.That(resolveBody, Does.Contain("TryValidateSonarKernelThreadGroup("));
            Assert.That(registerBody, Does.Contain("TryRegisterSlowTick();"));
            Assert.That(unregisterBody, Does.Contain("GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.UI)"));
        }

        [Test]
        public void SubmarineStructuralGrid_LeakPlumeGpuResourcesAreSlowPhaseOnly()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "SubmarineStructuralGrid.cs");
            string lateFrameBody = ExtractMethodBody(source, "public void LateFrameTick()");
            string visualBody = ExtractMethodBody(source, "private void FlushLeakPlumeVisualSync()");
            string dispatchBody = ExtractMethodBody(source, "private void DispatchLeakPlumeCompute(");
            string slowBody = ExtractMethodBody(source, "public void SlowTick()");
            string repairBody = ExtractMethodBody(source, "private void FlushLeakPlumeGpuResourceRepairSlow()");
            string ensureBody = ExtractMethodBody(source, "private bool EnsureLeakPlumeGpuResources()");
            string registerBody = ExtractMethodBody(source, "private void TryRegister()");
            string unregisterBody = ExtractMethodBody(source, "private void TryUnregister()");

            Assert.That(source, Does.Contain("ILateFrameTickable, ISlowTickable"));
            Assert.That(lateFrameBody, Does.Contain("FlushLeakPlumeVisualSync();"));
            Assert.That(lateFrameBody, Does.Not.Contain("EnsureLeakPlumeGpuResources"));
            Assert.That(visualBody, Does.Contain("DispatchLeakPlumeCompute(deltaSeconds);"));
            Assert.That(visualBody, Does.Not.Contain("EnsureLeakPlumeGpuResources"));
            Assert.That(dispatchBody, Does.Contain("HasLeakPlumeGpuResourcesReady()"));
            Assert.That(dispatchBody, Does.Contain("QueueLeakPlumeGpuResourceRepair();"));
            Assert.That(dispatchBody, Does.Not.Contain("EnsureLeakPlumeGpuResources"));
            Assert.That(dispatchBody, Does.Not.Contain(".FindKernel("));
            Assert.That(dispatchBody, Does.Not.Contain(".HasKernel("));
            Assert.That(dispatchBody, Does.Not.Contain(".GetKernelThreadGroupSizes("));
            Assert.That(dispatchBody, Does.Not.Contain("GraphicsBufferUploadUtility.CreateStructuredLockBuffer"));
            Assert.That(slowBody, Does.Contain("FlushLeakPlumeGpuResourceRepairSlow();"));
            Assert.That(repairBody, Does.Contain("EnsureLeakPlumeGpuResources()"));
            Assert.That(ensureBody, Does.Contain("leakPlumeCompute.FindKernel"));
            Assert.That(ensureBody, Does.Contain("GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>"));
            Assert.That(registerBody, Does.Contain("TryRegisterSlowTickable(this, PriorityLayer.Environment)"));
            Assert.That(unregisterBody, Does.Contain("UnregisterSlowTickable(this, PriorityLayer.Environment)"));
        }

        [Test]
        public void SubmarineStructuralGrid_StructuralJobLocksReleaseInFinally()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "SubmarineStructuralGrid.cs");
            string breachRepairBody = ExtractMethodBody(source, "private void ConsumeCompletedBreachRepairJob()");
            string mappingBody = ExtractMethodBody(source, "private void ConsumeCompletedMappingJob()");
            string fatigueBody = ExtractMethodBody(source, "private void ConsumeCompletedFatigueJob()");
            string damageBody = ExtractMethodBody(source, "private void ConsumeCompletedDamageJob()");
            string telemetryBody = ExtractMethodBody(source, "private void WriteDamageControlTelemetry(uint reasonFlags, bool allowNativeBreachRead, ushort failureCode)");

            AssertLockReleaseFinally(breachRepairBody, "UnlockStructuralJobBuffers(_breachRepairJobLockMask, _breachRepairJobMutationGuardVault);");
            AssertLockReleaseFinally(mappingBody, "UnlockStructuralJobBuffers(_mappingJobLockMask, _mappingJobMutationGuardVault);");
            AssertLockReleaseFinally(fatigueBody, "UnlockStructuralJobBuffers(_fatigueJobLockMask, _fatigueJobMutationGuardVault);");
            AssertLockReleaseFinally(damageBody, "UnlockStructuralJobBuffers(_damageJobLockMask, _damageJobMutationGuardVault);");
            AssertLockReleaseFinally(telemetryBody, "telemetryWriteVault.ReleaseWriteLock(in _damageControlTelemetryHandle, VaultOwnerSystemId);");
            Assert.That(source, Does.Not.Contain("Run();"));
        }

        [Test]
        public void SargassumDebris_RuntimeTargetsAreSlowPhaseOnly()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "SargassumDebrisParticleSystem.cs");
            string lateFrameBody = ExtractMethodBody(source, "public void LateFrameTick()");
            string ambientBody = ExtractMethodBody(source, "private void AdvanceAmbientDebrisEmission(");
            string slowBody = ExtractMethodBody(source, "public void SlowTick()");
            string registerBody = ExtractMethodBody(source, "private void TryRegister()");

            Assert.That(source, Does.Contain("ILateFrameTickable, ISlowTickable"));
            Assert.That(lateFrameBody, Does.Contain("AdvanceAmbientDebrisEmission(SystemDispatcher.CurrentFrameDeltaTime);"));
            Assert.That(lateFrameBody, Does.Not.Contain("ResolveRuntimeTargets();"));
            Assert.That(ambientBody, Does.Contain("QueueRuntimeTargetRefresh();"));
            Assert.That(ambientBody, Does.Not.Contain("ResolveRuntimeTargets();"));
            Assert.That(slowBody, Does.Contain("ResolveRuntimeTargets();"));
            Assert.That(registerBody, Does.Contain("TryRegisterSlowTickable(this, PriorityLayer.Environment)"));
        }

        [Test]
        public void SargassumCut_PlayerDependencyLookupIsColdOnly()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "SargassumCutManager.cs");
            string externalCutBody = ExtractMethodBody(source, "public bool RegisterExternalCut(");
            string slowBody = ExtractMethodBody(source, "public void SlowTick()");
            string hotResolveBody = ExtractMethodBody(source, "private void ResolveDependencies()");
            string coldResolveBody = ExtractMethodBody(source, "private void ResolveDependenciesCold(");

            Assert.That(externalCutBody, Does.Contain("ResolveDependencies();"));
            Assert.That(slowBody, Does.Contain("ResolveDependenciesCold(allowComponentLookup: false);"));
            Assert.That(hotResolveBody, Does.Not.Contain("BootstrapState.CurrentPlayerTransform"));
            Assert.That(hotResolveBody, Does.Not.Contain("TryGetComponent"));
            Assert.That(coldResolveBody, Does.Contain("BootstrapState.CurrentPlayerTransform"));
            Assert.That(coldResolveBody, Does.Contain("TryGetComponent"));
        }

        [Test]
        public void FloraInteraction_PlayerBootstrapLookupIsSlowPhaseOnly()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "FloraInteractionManager.cs");
            string tickBody = ExtractMethodBody(source, "public void Tick(");
            string slowBody = ExtractMethodBody(source, "public void SlowTick()");
            string resolverBody = ExtractMethodBody(source, "private Transform ResolveRuntimePlayerTransform()");
            string coldRefreshBody = ExtractMethodBody(source, "private void RefreshPlayerReferenceCacheCold()");

            Assert.That(tickBody, Does.Contain("ResolveRuntimePlayerTransform();"));
            Assert.That(slowBody, Does.Contain("RefreshPlayerReferenceCacheCold();"));
            Assert.That(resolverBody, Does.Contain("_playerReferenceRefreshRequested = true;"));
            Assert.That(resolverBody, Does.Not.Contain("BootstrapState.CurrentPlayerTransform"));
            Assert.That(coldRefreshBody, Does.Contain("BootstrapState.CurrentPlayerTransform"));
            Assert.That(coldRefreshBody, Does.Contain("playerTransform.TryGetComponent(out _playerMovement);"));
        }

        [Test]
        public void CaveBioRoots_PlayerContextRefreshIsSlowPhaseOnly()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "CaveBioRootsGenerator.cs");
            string lateFrameBody = ExtractMethodBody(source, "public void LateFrameTick()");
            string slowBody = ExtractMethodBody(source, "public void SlowTick()");
            string registerBody = ExtractMethodBody(source, "private void TryRegister()");
            string resolveBody = ExtractMethodBody(source, "private void ResolvePlayerContext()");

            Assert.That(source, Does.Contain("ILateFrameTickable, ISlowTickable"));
            Assert.That(lateFrameBody, Does.Not.Contain("ResolvePlayerContext();"));
            Assert.That(lateFrameBody, Does.Contain("ResolvePlayerRuntimePosition();"));
            Assert.That(slowBody, Does.Contain("ResolvePlayerContext();"));
            Assert.That(registerBody, Does.Contain("TryRegisterSlowTickable(this, PriorityLayer.Environment)"));
            Assert.That(resolveBody, Does.Contain("BootstrapState.CurrentPlayerTransform"));
        }

        [Test]
        public void ScatterGpuiAdmission_UsesComputeSupportNotHardwareTierBool()
        {
            string director = ReadProjectFile("Assets", "_Project", "Scripts", "WorldProceduralScatterDirector.cs");
            string service = ReadProjectFile("Assets", "_Project", "Scripts", "World", "ScatterInstancingService.cs");
            string boids = ReadProjectFile("Assets", "_Project", "Scripts", "World", "SargassumMicroFaunaBoids.cs");

            string admissionBody = ExtractMethodBody(director, "private void ApplyVendorGpuiManagerAdmission()");
            Assert.That(admissionBody, Does.Contain("!SystemInfo.supportsComputeShaders"));
            Assert.That(admissionBody, Does.Contain("floraGpuiManager.enabled = false;"));
            Assert.That(admissionBody, Does.Not.Contain("HardwareTierDetector.AllowHighResourceComputeShaders"));
            Assert.That(admissionBody, Does.Not.Contain("floraGpuiManager.enabled = true"));

            Assert.That(service, Does.Contain("return SystemInfo.supportsComputeShaders;"));
            Assert.That(service, Does.Not.Contain("HardwareTierDetector.AllowHighResourceComputeShaders"));

            string boidKernelBody = ExtractMethodBody(boids, "private bool EnsureComputeKernelBindings()");
            Assert.That(boidKernelBody, Does.Contain("boidCompute == null || !SystemInfo.supportsComputeShaders"));
            Assert.That(boidKernelBody, Does.Not.Contain("HardwareTierDetector.AllowHighResourceComputeShaders"));
        }

        [Test]
        public void InstanceCulling_UsesSurvivalDistancePressureFlag()
        {
            string contracts = ReadProjectFile("Assets", "_Project", "Scripts", "World", "Contracts", "InstanceCullingContracts.cs");
            string service = ReadProjectFile("Assets", "_Project", "Scripts", "Graphics", "Culling", "InstanceCullingService.cs");
            string dispatchBody = ExtractMethodBody(service, "public bool Dispatch(");

            Assert.That(contracts, Does.Contain("SurvivalDistancePressure = 1u << 2"));
            Assert.That(contracts, Does.Contain("LowTierDistance = SurvivalDistancePressure"));
            Assert.That(dispatchBody, Does.Contain("ResolveSurvivalDistanceWeight01(qualityWeight)"));
            Assert.That(dispatchBody, Does.Contain("InstanceCullingDispatchFlags.SurvivalDistancePressure"));
            Assert.That(dispatchBody, Does.Not.Contain("InstanceCullingDispatchFlags.LowTierDistance"));
        }

        [Test]
        public void VolumetricFog_UsesComputeSupportAndContinuousQualityProxyBlend()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Visor", "HectonVolumetricParticulateFogFeature.cs");
            string addPassBody = ExtractMethodBody(source, "public override void AddRenderPasses(");

            Assert.That(addPassBody, Does.Contain("float qualityWeight = ResolveFiniteSaturated(HomeostasisBrain.GlobalQualityWeight);"));
            Assert.That(addPassBody, Does.Contain("settings.computeShader != null"));
            Assert.That(addPassBody, Does.Contain("SystemInfo.supportsComputeShaders"));
            Assert.That(addPassBody, Does.Not.Contain("HardwareTierDetector.AllowHighResourceComputeShaders"));
            Assert.That(source, Does.Contain("ResolveProxyBlend(float quality)"));
            Assert.That(source, Does.Contain("ResolveRaySteps(float quality)"));
        }

        [Test]
        public void VolumetricLight_UsesComputeSupportAndContinuousStepBudget()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Visor", "VolumetricLightFeature.cs");
            string addPassBody = ExtractMethodBody(source, "public override void AddRenderPasses(");

            Assert.That(addPassBody, Does.Contain("settings.computeShader != null"));
            Assert.That(addPassBody, Does.Contain("SystemInfo.supportsComputeShaders"));
            Assert.That(addPassBody, Does.Not.Contain("HardwareTierDetector.AllowHighResourceComputeShaders"));
            Assert.That(source, Does.Contain("ResolveContinuousStepLimit(float qualityWeight)"));
            Assert.That(source, Does.Contain("ResolveEffectiveQualityWeight01()"));
            Assert.That(source, Does.Contain("HomeostasisBrain.GlobalQualityWeight"));
        }

        [Test]
        public void BiolumSsgi_UsesComputeSupportAndContinuousSampleBudget()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Visor", "HectonBiolumSSGIFeature.cs");
            string addPassBody = ExtractMethodBody(source, "public override void AddRenderPasses(");

            Assert.That(addPassBody, Does.Contain("settings.computeShader == null || !SystemInfo.supportsComputeShaders"));
            Assert.That(addPassBody, Does.Not.Contain("HardwareTierDetector.AllowHighResourceComputeShaders"));
            Assert.That(source, Does.Contain("ResolveSampleCount()"));
            Assert.That(source, Does.Contain("Mathf.Lerp(1f, authoredSamples, qualityCurve)"));
            Assert.That(source, Does.Contain("HomeostasisBrain.GlobalQualityWeight"));
        }

        [Test]
        public void BiolumDiffusionVolume_ResourceRefreshRepairsInSlowTick()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "Biolum", "HectonBiolumDiffusionVolume.cs");
            string lateFrameBody = ExtractMethodBody(source, "public void LateFrameTick()");
            string slowBody = ExtractMethodBody(source, "public void SlowTick()");
            string ensureBody = ExtractMethodBody(source, "private void EnsureResources()");

            Assert.That(lateFrameBody, Does.Contain("_resourceRefreshRequested |="));
            Assert.That(lateFrameBody, Does.Not.Contain("EnsureResources();"));
            Assert.That(slowBody, Does.Contain("EnsureResources();"));
            Assert.That(slowBody.IndexOf("EnsureResources();", System.StringComparison.Ordinal),
                Is.LessThan(slowBody.IndexOf("if (!HasRequiredResources())", System.StringComparison.Ordinal)));
            Assert.That(ensureBody, Does.Contain("CreateVolumeTexture("));
            Assert.That(ensureBody, Does.Contain("GraphicsBufferUploadUtility.CreateStructuredLockBuffer<BiolumPointGpuData>"));
            Assert.That(ensureBody, Does.Contain("TryResolveKernel("));
        }

        [Test]
        public void PresentationComputeGates_UseCapabilityNotHardwareBucketSecondWave()
        {
            string parasites = ReadProjectFile("Assets", "_Project", "Scripts", "VFX", "Parasites", "ParasiteSwarmGpuRuntime.cs");
            string terminal = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "TerminalOS", "TerminalOsRuntime.cs");
            string bilateral = ReadProjectFile("Assets", "_Project", "Scripts", "Rendering", "BilateralDrs", "HectonBilateralDrsUpscalerFeature.cs");
            string foam = ReadProjectFile("Assets", "_Project", "Scripts", "VFX", "JacobianFoam", "JacobianFoamGpuRuntime.cs");
            string marineSnow = ReadProjectFile("Assets", "_Project", "Scripts", "VFX", "HectonMarineSnowRenderer.cs");

            Assert.That(parasites, Does.Contain("SystemInfo.supportsComputeShaders"));
            Assert.That(parasites, Does.Not.Contain("HardwareTierDetector.AllowHighResourceComputeShaders"));

            Assert.That(terminal, Does.Contain("terminalBlitCompute == null || !SystemInfo.supportsComputeShaders"));
            Assert.That(terminal, Does.Contain("!SystemInfo.supportsComputeShaders"));
            Assert.That(terminal, Does.Not.Contain("HardwareTierDetector.AllowHighResourceComputeShaders"));

            Assert.That(bilateral, Does.Contain("SystemInfo.supportsComputeShaders"));
            Assert.That(bilateral, Does.Contain("MinimumEdgeMaskQualityGate"));
            Assert.That(bilateral, Does.Contain("ResolveEdgeMaskQualityGate(qualityGate)"));
            Assert.That(bilateral, Does.Not.Contain("qualityGate == 0f"));
            Assert.That(bilateral, Does.Not.Contain("skipSobel"));
            Assert.That(bilateral, Does.Not.Contain("HardwareTierDetector.AllowHighResourceComputeShaders"));

            Assert.That(foam, Does.Contain("_coldSupportsComputeShaders = SystemInfo.supportsComputeShaders"));
            Assert.That(foam, Does.Contain("_computeShader == null || !_coldSupportsComputeShaders"));
            Assert.That(foam, Does.Not.Contain("_coldAllowHighResourceComputeShaders"));
            Assert.That(foam, Does.Not.Contain("HardwareTierDetector.AllowHighResourceComputeShaders"));

            Assert.That(marineSnow, Does.Contain("_coldSupportsComputeShaders = SystemInfo.supportsComputeShaders"));
            Assert.That(marineSnow, Does.Contain("marineSnowCompute == null || !_coldSupportsComputeShaders"));
            Assert.That(marineSnow, Does.Not.Contain("_coldAllowHighResourceComputeShaders"));
            Assert.That(marineSnow, Does.Not.Contain("HardwareTierDetector.AllowHighResourceComputeShaders"));
        }

        [Test]
        public void RendererFeatures_DoNotPollApplicationPlayingFromRenderRoutes()
        {
            string oceanFeature = ReadProjectFile("Assets", "_Project", "Scripts", "Rendering", "OceanSinglePass", "HectonSinglePassOceanFeature.cs");
            string oceanRuntime = ReadProjectFile("Assets", "_Project", "Scripts", "Rendering", "OceanSinglePass", "OceanSinglePassRuntime.cs");
            string causticsFeature = ReadProjectFile("Assets", "_Project", "Scripts", "Rendering", "AbyssalCaustics", "HectonDeferredCausticsFeature.cs");
            string bilateralFeature = ReadProjectFile("Assets", "_Project", "Scripts", "Rendering", "BilateralDrs", "HectonBilateralDrsUpscalerFeature.cs");
            string waterOpticsFeature = ReadProjectFile("Assets", "_Project", "Scripts", "Rendering", "WaterOptics", "HectonWaterOpticsTelemetryFeature.cs");
            string waterOpticsRuntime = ReadProjectFile("Assets", "_Project", "Scripts", "Rendering", "WaterOptics", "WaterOpticsRuntime.cs");

            string oceanAddBody = ExtractMethodBody(oceanFeature, "public override void AddRenderPasses(");
            string oceanRecordBody = ExtractMethodBody(oceanFeature, "public override void RecordRenderGraph(");
            string causticsAddBody = ExtractMethodBody(causticsFeature, "public override void AddRenderPasses(");
            string causticsRecordBody = ExtractMethodBody(causticsFeature, "public override void RecordRenderGraph(");
            string bilateralAddBody = ExtractMethodBody(bilateralFeature, "public override void AddRenderPasses(");
            string bilateralRecordBody = ExtractMethodBody(bilateralFeature, "public override void RecordRenderGraph(");
            string waterAddBody = ExtractMethodBody(waterOpticsFeature, "public override void AddRenderPasses(");
            string waterRecordBody = ExtractMethodBody(waterOpticsFeature, "public override void RecordRenderGraph(");

            Assert.That(oceanRuntime, Does.Contain("public static bool HasRendererFeatureRuntimeGate()"));
            Assert.That(oceanRuntime, Does.Contain("public static bool TryEnterRenderGraphRuntimeGate()"));
            Assert.That(oceanAddBody, Does.Contain("OceanSinglePassRuntime.HasRendererFeatureRuntimeGate()"));
            Assert.That(oceanRecordBody, Does.Contain("OceanSinglePassRuntime.TryEnterRenderGraphRuntimeGate()"));
            Assert.That(oceanAddBody, Does.Not.Contain("Application.isPlaying"));
            Assert.That(oceanRecordBody, Does.Not.Contain("Application.isPlaying"));

            Assert.That(causticsAddBody, Does.Contain("AbyssalDeferredCausticsRuntime.TryGetActiveConstantBuffer"));
            Assert.That(causticsAddBody, Does.Not.Contain("Application.isPlaying"));
            Assert.That(causticsRecordBody, Does.Contain("AbyssalDeferredCausticsRuntime.TryGetActiveConstantBuffer"));
            Assert.That(causticsRecordBody, Does.Not.Contain("Application.isPlaying"));

            Assert.That(bilateralAddBody, Does.Contain("HectonBilateralDrsUpscalerRuntime.TryGetRuntimeInstance(out _)"));
            Assert.That(bilateralAddBody, Does.Not.Contain("Application.isPlaying"));
            Assert.That(bilateralRecordBody, Does.Not.Contain("Application.isPlaying"));

            Assert.That(waterOpticsRuntime, Does.Contain("public static bool TryGetRuntimeInstance(out WaterOpticsRuntime runtime)"));
            Assert.That(waterAddBody, Does.Contain("WaterOpticsRuntime.TryGetRuntimeInstance(out _)"));
            Assert.That(waterAddBody, Does.Not.Contain("Application.isPlaying"));
            Assert.That(waterRecordBody, Does.Contain("WaterOpticsRuntime.TryGetRuntimeInstance(out _)"));
            Assert.That(waterRecordBody, Does.Not.Contain("Application.isPlaying"));
        }

        [Test]
        public void VisorRenderFeatures_DoNotPollApplicationPlayingFromRenderRoutes()
        {
            string gate = ReadProjectFile("Assets", "_Project", "Scripts", "Visor", "HectonDrsRenderFeatureGate.cs");
            string ssdo = ReadProjectFile("Assets", "_Project", "Scripts", "Visor", "HectonAbyssalSsdoFeature.cs");
            string halfResParticles = ReadProjectFile("Assets", "_Project", "Scripts", "Visor", "HectonHalfResParticlesFeature.cs");
            string depthFog = ReadProjectFile("Assets", "_Project", "Scripts", "Visor", "HectonNoirDepthFogFeature.cs");
            string scooterShafts = ReadProjectFile("Assets", "_Project", "Scripts", "Visor", "HectonScooterVolumetricShaftsFeature.cs");
            string stochasticSsr = ReadProjectFile("Assets", "_Project", "Scripts", "Visor", "HectonStochasticSsrFeature.cs");

            Assert.That(gate, Does.Contain("internal static bool HasRuntimeRenderOwner()"));
            Assert.That(gate, Does.Contain("SystemDispatcher.ActiveRuntimeInstance != null"));

            AssertVisorRenderRouteUsesRuntimeOwnerGate(ssdo);
            AssertVisorRenderRouteUsesRuntimeOwnerGate(halfResParticles);
            AssertVisorRenderRouteUsesRuntimeOwnerGate(depthFog);
            AssertVisorRenderRouteUsesRuntimeOwnerGate(scooterShafts);
            AssertVisorRenderRouteUsesRuntimeOwnerGate(stochasticSsr);
        }

        [Test]
        public void TerminalOsRuntime_ResourceAndCsvWorkAreSlowPhaseOnly()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "TerminalOS", "TerminalOsRuntime.cs");
            string lateFrameBody = ExtractMethodBody(source, "public void LateFrameTick()");
            string refreshBody = ExtractMethodBody(source, "private void RefreshScalabilityPolicy()");
            string slowBody = ExtractMethodBody(source, "public void SlowTick()");
            string flushBody = ExtractMethodBody(source, "private void FlushPendingGraphicsResourceRebuild()");
            string blackBoxFlushBody = ExtractMethodBody(source, "private void FlushQueuedBlackBoxDumps()");
            string decryptionFinalizeBody = ExtractMethodBody(source, "private void TryFinalizeDecryptionJob()");

            Assert.That(source, Does.Contain("MonoBehaviour, ISlowTickable, ILateFrameTickable"));
            Assert.That(lateFrameBody, Does.Not.Contain("TryMonitorLayoutCsv"));
            Assert.That(lateFrameBody, Does.Not.Contain("TryMonitorDecryptionCsv"));
            Assert.That(lateFrameBody, Does.Not.Contain("TryDumpBlackBox("));
            Assert.That(lateFrameBody, Does.Not.Contain("TryDumpDecryptionBlackBox("));
            Assert.That(lateFrameBody, Does.Contain("QueueTerminalBlackBoxDump(faultFlags);"));
            Assert.That(lateFrameBody, Does.Not.Contain("ReleaseRenderTexture"));
            Assert.That(lateFrameBody, Does.Not.Contain("EnsureGraphicsResources"));
            Assert.That(lateFrameBody, Does.Not.Contain("EnsureTextureArray"));
            Assert.That(lateFrameBody, Does.Not.Contain("new RenderTexture"));
            Assert.That(lateFrameBody, Does.Not.Contain("Destroy("));
            Assert.That(lateFrameBody, Does.Not.Contain("File."));
            Assert.That(lateFrameBody, Does.Not.Contain("new FileStream"));
            Assert.That(refreshBody, Does.Contain("QueueGraphicsResourceRebuild();"));
            Assert.That(refreshBody, Does.Not.Contain("ReleaseRenderTexture();"));
            Assert.That(slowBody, Does.Contain("TryMonitorLayoutCsv(ownerFrame);"));
            Assert.That(slowBody, Does.Contain("TryMonitorDecryptionCsv(ownerFrame);"));
            Assert.That(slowBody, Does.Contain("FlushPendingGraphicsResourceRebuild();"));
            Assert.That(slowBody, Does.Contain("FlushQueuedBlackBoxDumps();"));
            Assert.That(flushBody, Does.Contain("ReleaseRenderTexture();"));
            Assert.That(flushBody, Does.Contain("EnsureGraphicsResources();"));
            Assert.That(flushBody, Does.Contain("ForceAllDirty();"));
            Assert.That(blackBoxFlushBody, Does.Contain("TryDumpBlackBox(faultFlags);"));
            Assert.That(blackBoxFlushBody, Does.Contain("TryDumpDecryptionBlackBox(decryptionFaultFlags);"));
            Assert.That(decryptionFinalizeBody, Does.Contain("QueueDecryptionBlackBoxDump(faultFlags);"));
            Assert.That(decryptionFinalizeBody, Does.Not.Contain("TryDumpDecryptionBlackBox("));
        }

        [Test]
        public void BilateralDrsUpscaler_TelemetryWritesAfterSimulation()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Rendering", "BilateralDrs", "HectonBilateralDrsUpscalerRuntime.cs");
            string simulationBody = ExtractMethodBody(source, "private JobHandle ScheduleOwnerSimulation(");
            string postBody = ExtractMethodBody(source, "private void RunOwnerPostSimulation()");
            string visualSyncBody = ExtractMethodBody(source, "private void RunOwnerVisualSync()");
            string failClosedBody = ExtractMethodBody(source, "private void FailClosedRuntimeRoute(");
            string resetBody = ExtractMethodBody(source, "private void ResetVaultSeedState()");

            Assert.That(source, Does.Contain("private UpscalerTelemetryEntry _pendingTelemetryEntry;"));
            Assert.That(source, Does.Contain("private bool _pendingTelemetryEntryValid;"));
            Assert.That(simulationBody, Does.Contain("_pendingTelemetryEntry = telemetryEntry;"));
            Assert.That(simulationBody, Does.Contain("_pendingTelemetryEntryValid = true;"));
            Assert.That(simulationBody, Does.Not.Contain("RecordUpscalerTelemetryOneLock"));
            Assert.That(postBody, Does.Contain("RecordUpscalerTelemetryOneLock(in telemetryEntry);"));
            Assert.That(postBody, Does.Contain("PublishPendingParameters();"));
            Assert.That(postBody.IndexOf("RecordUpscalerTelemetryOneLock", System.StringComparison.Ordinal), Is.LessThan(postBody.IndexOf("PublishPendingParameters", System.StringComparison.Ordinal)));
            Assert.That(visualSyncBody, Does.Contain("UploadParametersToGpu()"));
            Assert.That(failClosedBody, Does.Contain("_pendingTelemetryEntryValid = false;"));
            Assert.That(failClosedBody, Does.Contain("_pendingTelemetryEntry = default;"));
            Assert.That(resetBody, Does.Contain("_pendingTelemetryEntryValid = false;"));
            Assert.That(resetBody, Does.Contain("_pendingTelemetryEntry = default;"));
        }

        [Test]
        public void RemainingComputeAdmission_UsesCapabilityNotHighResourceBucket()
        {
            string scriptsRoot = ResolveProjectPath("Assets", "_Project", "Scripts");
            string[] files = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string source = File.ReadAllText(files[i]);
                if (files[i].EndsWith(Path.Combine("Core", "HardwareTierDetector.cs"), System.StringComparison.Ordinal))
                    continue;

                Assert.That(source, Does.Not.Contain("HardwareTierDetector.AllowHighResourceComputeShaders"), files[i]);
            }

            string boids = ReadProjectFile("Assets", "_Project", "Scripts", "HectonBoidController.cs");
            string rocks = ReadProjectFile("Assets", "_Project", "Scripts", "HectonRockManager.cs");
            string scatter = ReadProjectFile("Assets", "_Project", "Scripts", "World", "GPUScatterDirector.cs");
            string debris = ReadProjectFile("Assets", "_Project", "Scripts", "VFX", "Debris", "CarveDebrisComputeRenderer.cs");
            string pda = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "PDAMapTab.cs");
            string oceanAtmosphere = ReadProjectFile("Assets", "_Project", "Scripts", "Atmosphere", "ShinobuOceanSurfaceAtmosphereRuntime.cs");
            string fluid = ReadProjectFile("Assets", "_Project", "Scripts", "HectonFluidEngine.cs");
            string vegetation = ReadProjectFile("Assets", "_Project", "Scripts", "World", "HectonIndirectVegetationRenderer.cs");
            string asyncBuoyancy = ReadProjectFile("Assets", "_Project", "Scripts", "Physics", "Buoyancy", "AsyncReadback", "AsyncBuoyancyReadbackRuntime.cs");
            string crest = ReadProjectFile("Assets", "_Project", "Scripts", "Plugins", "Crest", "Crest4KinematicsAdapter.cs");

            Assert.That(boids, Does.Contain("boidShader == null || !SystemInfo.supportsComputeShaders"));
            Assert.That(rocks, Does.Contain("return SystemInfo.supportsComputeShaders;"));
            Assert.That(scatter, Does.Contain("_coldSupportsComputeShaders = SystemInfo.supportsComputeShaders"));
            Assert.That(debris, Does.Contain("_coldSupportsComputeShaders = SystemInfo.supportsComputeShaders"));
            Assert.That(pda, Does.Contain("_coldSupportsComputeShaders = SystemInfo.supportsComputeShaders"));
            Assert.That(oceanAtmosphere, Does.Contain("_coldSupportsComputeShaders = SystemInfo.supportsComputeShaders"));
            Assert.That(fluid, Does.Contain("_coldSupportsComputeShaders = SystemInfo.supportsComputeShaders"));
            Assert.That(vegetation, Does.Contain("_supportsComputeShadersCold = SystemInfo.supportsComputeShaders"));
            Assert.That(asyncBuoyancy, Does.Contain("_coldSupportsComputeShaders = SystemInfo.supportsComputeShaders"));
            Assert.That(crest, Does.Contain("if (SystemInfo.supportsComputeShaders)"));
        }

        [Test]
        public void CoreMathLod_UsesContinuousShaderWeightWithoutKeywordToggles()
        {
            string distanceMath = ReadProjectFile("Assets", "_Project", "Scripts", "Core", "DistanceMath.cs");
            string registry = ReadProjectFile("Assets", "_Project", "Scripts", "Core", "GlobalRegistry.cs");
            string coreLit = ReadProjectFile("Assets", "_Project", "Art", "Shaders", "Hecton_CoreLit.hlsl");

            Assert.That(distanceMath, Does.Not.Contain("EnableKeyword"));
            Assert.That(distanceMath, Does.Not.Contain("DisableKeyword"));
            Assert.That(distanceMath, Does.Not.Contain("HectonQualityTier"));
            Assert.That(distanceMath, Does.Not.Contain("ResolveTierQualityWeight01"));
            Assert.That(registry, Does.Not.Contain("MathLodLowKeyword"));
            Assert.That(registry, Does.Not.Contain("MathLodHighKeyword"));
            Assert.That(coreLit, Does.Not.Contain("_MATH_LOD_LOW"));
            Assert.That(coreLit, Does.Not.Contain("_MATH_LOD_HIGH"));
            Assert.That(coreLit, Does.Contain("_HectonMathLodWeight"));
            Assert.That(coreLit, Does.Contain("HectonCoreLitMathLodWeight"));
            Assert.That(distanceMath, Does.Contain("Shader.SetGlobalFloat(_mathLodWeightPropertyId, quality)"));
        }

        [Test]
        public void ContentTieredGroupPolicy_UsesContinuousRuntimeBudgetInsteadOfVramFork()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Core", "Content", "ContentRuntimeServices.cs");

            Assert.That(source, Does.Contain("ResolveRuntimeVisualBudgetWeight01"));
            Assert.That(source, Does.Contain("ResolveHardwareVisualCapacity01"));
            Assert.That(source, Does.Contain("HomeostasisBrain.GlobalQualityWeight"));
            Assert.That(source, Does.Contain("SmoothRange01(0.28f, 1f, weight)"));
            Assert.That(source, Does.Contain("ContinuousVisualFeatureMask"));
            Assert.That(source, Does.Contain("VisualFeatureWeightQ8"));
            Assert.That(source, Does.Contain("PomWeightQ8"));
            Assert.That(source, Does.Not.Contain("SystemInfo.graphicsMemorySize <= 2048"));
            Assert.That(source, Does.Not.Contain("SystemInfo.graphicsMemorySize > 4096 ?"));
            Assert.That(source, Does.Not.Contain("visualWeight01 >= 0.18f"));
            Assert.That(source, Does.Not.Contain("visualWeight01 >= 0.34f"));
            Assert.That(source, Does.Not.Contain("visualWeight01 >= 0.52f"));
            Assert.That(source, Does.Not.Contain("visualWeight01 >= 0.64f"));
            Assert.That(source, Does.Not.Contain("visualWeight01 >= 0.78f"));
        }

        [Test]
        public void WorldChunkResidency_PredictiveVramAbortScalesContinuously()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "WorldChunkResidencyManager.cs");

            Assert.That(source, Does.Contain("ResolvePredictiveVramAbortThresholdBytes"));
            Assert.That(source, Does.Contain("ResolvePredictiveVramCeilingBytesCold"));
            Assert.That(source, Does.Contain("ResolveSmoothGlobalQualityWeight01"));
            Assert.That(source, Does.Contain("PredictiveVramVisualOverkillCeilingBytes"));
            Assert.That(source, Does.Contain("SurvivalLoadDispatchBudget"));
            Assert.That(source, Does.Contain("SurvivalUnloadRadiusMeters"));
            Assert.That(source, Does.Contain("VisualOverkillUnloadRadiusMeters"));
            Assert.That(source, Does.Contain("_predictiveVramCeilingBytes = ResolvePredictiveVramCeilingBytesCold()"));
            Assert.That(source, Does.Not.Contain("long ceilingBytes = ResolvePredictiveVramCeilingBytes();"));
            Assert.That(source, Does.Not.Contain("LowTierLoadDispatchBudget"));
            Assert.That(source, Does.Not.Contain("LowTierUnloadRadiusMeters"));
            Assert.That(source, Does.Not.Contain("UltraTierUnloadRadiusMeters"));
            Assert.That(source, Does.Not.Contain("SystemInfo.graphicsMemorySize > 2048)\r\n                return false"));
            Assert.That(source, Does.Not.Contain("SystemInfo.graphicsMemorySize > 2048)\n                return false"));
        }

        [Test]
        public void WorldChunkResidency_HlodImpostorFlagsAvoidQualitySnapThreshold()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "WorldChunkResidencyManager.cs");

            Assert.That(source, Does.Not.Contain("ChunkImpostorSurvivalSnapQualityThreshold"));
            Assert.That(source, Does.Not.Contain("ResolveSmoothGlobalQualityWeight01() <= ChunkImpostor"));
            Assert.That(source, Does.Not.Contain("flags |= HectonChunkImpostorResidency.FlagSurvivalSnap;"));
            Assert.That(source, Does.Contain("flags |= HectonChunkImpostorResidency.FlagDitherBlend;"));
        }

        [Test]
        public void WorldChunkResidency_AsyncUploadQualityPolicyIsSlowPhaseOnly()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "WorldChunkResidencyManager.cs");
            string awakeBody = ExtractMethodBody(source, "private void Awake()");
            string slowBody = ExtractMethodBody(source, "public void SlowTick()");
            string lateFrameBody = ExtractMethodBody(source, "public void LateFrameTick()");
            string policyBody = ExtractMethodBody(source, "private void FlushAsyncUploadBudgetPolicySlow()");
            int slowPolicyCall = slowBody.IndexOf("FlushAsyncUploadBudgetPolicySlow();", System.StringComparison.Ordinal);
            int slowChunkGate = slowBody.IndexOf("if (_chunkCount <= 0)", System.StringComparison.Ordinal);

            Assert.That(awakeBody, Does.Contain("FlushAsyncUploadBudgetPolicySlow();"));
            Assert.That(slowPolicyCall, Is.GreaterThanOrEqualTo(0));
            Assert.That(slowChunkGate, Is.GreaterThanOrEqualTo(0));
            Assert.That(slowPolicyCall, Is.LessThan(slowChunkGate));
            Assert.That(lateFrameBody, Does.Not.Contain("FlushAsyncUploadBudgetPolicySlow();"));
            Assert.That(lateFrameBody, Does.Not.Contain("QualitySettings."));
            Assert.That(policyBody, Does.Contain("ResolveAsyncUploadEffectiveQuality01();"));
            Assert.That(policyBody, Does.Contain("QualitySettings.asyncUploadBufferSize = uploadBufferSize;"));
            Assert.That(policyBody, Does.Contain("QualitySettings.asyncUploadTimeSlice = uploadTimeSlice;"));
            Assert.That(policyBody, Does.Contain("QualitySettings.asyncUploadPersistentBuffer = true;"));
            Assert.That(source, Does.Contain("_vramPressure = GlobalRegistry.VRAMPressureReadModel;"));
            Assert.That(source, Does.Contain("private float ResolveAsyncUploadPressure01()"));
            Assert.That(source, Does.Contain("math.smoothstep(0.55f, 0.98f, pressure)"));
        }

        [Test]
        public void ScreenSpaceLightShaft_LateFrameUsesRegistrationLatchNotApplicationPoll()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Lighting", "Shafts", "ScreenSpaceLightShaftRuntime.cs");
            string lateFrameBody = ExtractMethodBody(source, "public void LateFrameTick()");

            Assert.That(lateFrameBody, Does.Contain("!_registeredLateFrame"));
            Assert.That(lateFrameBody, Does.Not.Contain("Application.isPlaying"));
            Assert.That(lateFrameBody, Does.Not.Contain("GlobalRegistry.Get<"));
            Assert.That(lateFrameBody, Does.Not.Contain("GetComponent"));
        }

        [Test]
        public void ProjectVisualShaders_RemoveBinaryQualityAndMathLodKeywords()
        {
            string shaderRoot = ResolveProjectPath("Assets", "_Project", "Art", "Shaders");
            string[] files = Directory.GetFiles(shaderRoot, "*.*", SearchOption.AllDirectories);

            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                string extension = Path.GetExtension(file);
                if (extension != ".shader" && extension != ".hlsl")
                    continue;

                string source = File.ReadAllText(file);
                Assert.That(source, Does.Not.Contain("_QUALITY_MX350"), file);
                Assert.That(source, Does.Not.Contain("_QUALITY_HIGH"), file);
                Assert.That(source, Does.Not.Contain("_MATH_LOD_LOW"), file);
                Assert.That(source, Does.Not.Contain("_MATH_LOD_HIGH"), file);
            }
        }

        [Test]
        public void VisorArStencilFrameUpload_StagesLocallyAndCommitsSingleWriterPhases()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Visor", "HectonVisorARStencilRendererFeature.cs");
            int buildStart = source.IndexOf("private bool BuildAndUploadFrame(", System.StringComparison.Ordinal);
            int helperStart = source.IndexOf("private static bool TryCommitSingleToVault", buildStart, System.StringComparison.Ordinal);
            Assert.That(buildStart, Is.GreaterThanOrEqualTo(0));
            Assert.That(helperStart, Is.GreaterThan(buildStart));
            string buildBody = source.Substring(buildStart, helperStart - buildStart);

            Assert.That(buildBody, Does.Contain("stackalloc ARWaypointOverlay.StencilTargetSourceDTO[VisorARStencilContracts.MaxTargets]"));
            Assert.That(buildBody, Does.Contain("stackalloc VisorArTargetDTO[VisorARStencilContracts.MaxTargets]"));
            Assert.That(buildBody, Does.Not.Contain("TryAcquireWriteLock("));
            Assert.That(buildBody, Does.Not.Contain("hudLocked"));
            Assert.That(buildBody, Does.Not.Contain("targetSourceLocked"));
            Assert.That(buildBody, Does.Not.Contain("projectedLocked"));
            Assert.That(buildBody, Does.Not.Contain("digitLocked"));
            Assert.That(buildBody, Does.Not.Contain("telemetryLocked"));

            int sourceCommit = buildBody.IndexOf("in _targetSourceHandle", System.StringComparison.Ordinal);
            int projectedCommit = buildBody.IndexOf("in _projectedTargetHandle", sourceCommit, System.StringComparison.Ordinal);
            int digitCommit = buildBody.IndexOf("in _digitParamsHandle", projectedCommit, System.StringComparison.Ordinal);
            int hudCommit = buildBody.IndexOf("in _hudParamsHandle", digitCommit, System.StringComparison.Ordinal);
            int gpuUpload = buildBody.IndexOf("UpdateGpuPayload(in hudParams, in digitParams, projectedTargets)", hudCommit, System.StringComparison.Ordinal);
            int telemetryCommit = buildBody.IndexOf("TryCommitTelemetryFrame(", gpuUpload, System.StringComparison.Ordinal);

            Assert.That(sourceCommit, Is.GreaterThanOrEqualTo(0));
            Assert.That(projectedCommit, Is.GreaterThan(sourceCommit));
            Assert.That(digitCommit, Is.GreaterThan(projectedCommit));
            Assert.That(hudCommit, Is.GreaterThan(digitCommit));
            Assert.That(gpuUpload, Is.GreaterThan(hudCommit));
            Assert.That(telemetryCommit, Is.GreaterThan(gpuUpload));
            Assert.That(source, Does.Contain("private static bool TryCommitSpanToVault<T>"));
            Assert.That(source, Does.Contain("private static bool TryCommitSingleToVault<T>"));
            Assert.That(source, Does.Contain("private bool TryCommitTelemetryFrame("));
            Assert.That(source, Does.Contain("vault.ReleaseWriteLock(in handle, SystemID.UI);"));
            Assert.That(source, Does.Contain("vault.ReleaseWriteLock(in _telemetryHandle, SystemID.UI);"));
        }

        [Test]
        public void VisorNoirConstantsUpdate_WritesSingleDtoPerLockPhase()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Visor", "HectonVisorUberPostFeature.Noir.cs");
            int updateStart = source.IndexOf("private bool TryUpdateNoirConstants()", System.StringComparison.Ordinal);
            int helperStart = source.IndexOf("private bool TryWriteNoirDto<T>", updateStart, System.StringComparison.Ordinal);
            Assert.That(updateStart, Is.GreaterThanOrEqualTo(0));
            Assert.That(helperStart, Is.GreaterThan(updateStart));
            string updateBody = source.Substring(updateStart, helperStart - updateStart);

            Assert.That(updateBody, Does.Contain("TryWriteNoirDto(in _noirInputHandle, in input)"));
            Assert.That(updateBody, Does.Contain("TryWriteNoirDto(in _noirTuningHandle, in tuning)"));
            Assert.That(updateBody, Does.Contain("TryWriteNoirDto(in _noirConstantsHandle, in constants)"));
            Assert.That(updateBody, Does.Not.Contain("TryAcquireWriteLock("));
            Assert.That(updateBody, Does.Not.Contain("inputLocked"));
            Assert.That(updateBody, Does.Not.Contain("tuningLocked"));
            Assert.That(updateBody, Does.Not.Contain("constantsLocked"));
            Assert.That(updateBody, Does.Not.Contain("GetUnsafePtr()"));
            Assert.That(source, Does.Contain("vault.ReleaseWriteLock(in handle, SystemID.GraphicsScalability);"));

            string featureFlagsBody = ExtractMethodBody(source, "private static uint ResolveNoirFeatureFlags(");
            Assert.That(source, Does.Contain("entry.GlobalQualityWeight01 = input.GlobalQualityWeight01;"));
            Assert.That(featureFlagsBody, Does.Not.Contain("quality < 0.34f"));
            Assert.That(featureFlagsBody, Does.Not.Contain("QualityAndLimits.x"));
        }

        [Test]
        public void DiegeticVisorLensRuntime_StagesSimulationLocallyAndCommitsSingleWriterPhases()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Visor", "DiegeticVisorLensRuntime.cs");

            string scheduleBody = ExtractMethodBody(source, "private void ScheduleSimulation(");
            Assert.That(scheduleBody, Does.Contain("TryReadVisorValue(vault, in _stateHandle"));
            Assert.That(scheduleBody, Does.Contain("TryReadVisorValue(vault, in _nanFlagsHandle"));
            Assert.That(scheduleBody, Does.Contain("TryWriteVisorValue(vault, in _stateHandle"));
            Assert.That(scheduleBody, Does.Contain("TryWriteVisorValue(vault, in _gpuGlobalsHandle"));
            Assert.That(scheduleBody, Does.Not.Contain("TryAcquireVisorWriteBuffer"));
            Assert.That(scheduleBody, Does.Not.Contain("stateLocked"));
            Assert.That(scheduleBody, Does.Not.Contain("gpuGlobalsLocked"));
            Assert.That(scheduleBody, Does.Not.Contain("nanFlagsLocked"));

            string ingestBody = ExtractMethodBody(source, "private void IngestCoreSignals(");
            Assert.That(ingestBody, Does.Contain("SignalBus<PlayerExhaleSignal>.GetFrameSnapshot()"));
            Assert.That(ingestBody, Does.Contain("TryReadVisorValue(vault, in _physiologyHandle"));
            Assert.That(ingestBody, Does.Contain("TryWriteVisorValue(vault, in _environmentHandle"));
            Assert.That(ingestBody, Does.Not.Contain("TryAcquireVisorWriteBuffer"));
            Assert.That(ingestBody, Does.Not.Contain("physiologyLocked"));
            Assert.That(ingestBody, Does.Not.Contain("environmentLocked"));

            string emergencyBody = ExtractMethodBody(source, "private void ApplyEmergencyMockVisorData()");
            Assert.That(emergencyBody, Does.Contain("TryReadVisorValue(vault, in _stateHandle"));
            Assert.That(emergencyBody, Does.Contain("TryWriteVisorValue(vault, in _environmentHandle"));
            Assert.That(emergencyBody, Does.Not.Contain("TryAcquireVisorWriteBuffer"));
            Assert.That(emergencyBody, Does.Not.Contain("stateLocked"));
            Assert.That(emergencyBody, Does.Not.Contain("environmentLocked"));

            string telemetryBody = ExtractMethodBody(source, "private void WriteTelemetryFrame(");
            int cursorAcquire = telemetryBody.IndexOf("TryAcquireVisorWriteBuffer(vault, in _telemetryCursorHandle", System.StringComparison.Ordinal);
            int cursorRelease = telemetryBody.IndexOf("vault.ReleaseWriteLock(in _telemetryCursorHandle", cursorAcquire, System.StringComparison.Ordinal);
            int ringAcquire = telemetryBody.IndexOf("TryAcquireVisorWriteBuffer(vault, in _telemetryHandle", cursorRelease, System.StringComparison.Ordinal);
            Assert.That(cursorAcquire, Is.GreaterThanOrEqualTo(0));
            Assert.That(cursorRelease, Is.GreaterThan(cursorAcquire));
            Assert.That(ringAcquire, Is.GreaterThan(cursorRelease));

            string evaluatorBody = ExtractTypeBody(source, "private ref struct VisorCondensationEvaluator");
            Assert.That(evaluatorBody, Does.Contain("public VisorStateDTO State;"));
            Assert.That(evaluatorBody, Does.Contain("public DiegeticVisorLensGpuGlobalsDTO GpuGlobals;"));
            Assert.That(evaluatorBody, Does.Not.Contain("NativeArray<VisorStateDTO> State"));
            Assert.That(evaluatorBody, Does.Not.Contain("NativeArray<DiegeticVisorLensGpuGlobalsDTO> GpuGlobals"));
        }

        [Test]
        public void ThermodynamicsHazardGrid_VisualUploadCadenceUsesContinuousQuality()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Thermodynamics", "ThermodynamicsHazardGridRuntime.cs");
            string typesSource = ReadProjectFile("Assets", "_Project", "Scripts", "Thermodynamics", "ThermodynamicsHazardTypes.cs");

            string strideBody = ExtractMethodBody(source, "private static int ResolveVisualUploadStride(");
            Assert.That(strideBody, Does.Contain("qualityWeight01"));
            Assert.That(strideBody, Does.Contain("curvedQuality"));
            Assert.That(strideBody, Does.Contain("math.lerp(MaxVisualUploadStride, 1f, curvedQuality)"));
            Assert.That(strideBody, Does.Not.Contain("LowResolution"));

            string uploadBody = ExtractMethodBody(source, "private void UploadVisualTextureIfDirty()");
            Assert.That(uploadBody, Does.Contain("ResolveVisualUploadStride(ResolveContinuousQualityWeight())"));
            Assert.That(uploadBody, Does.Contain("_gridVersion % uploadStride"));
            Assert.That(uploadBody, Does.Not.Contain("_activeResolution == LowResolution"));
            Assert.That(uploadBody, Does.Not.Contain("LowTierVisualUploadStride"));

            Assert.That(source, Does.Not.Contain("HealthPressureLowTierFrames"));
            Assert.That(source, Does.Not.Contain("TelemetryFlagLowTier"));
            Assert.That(source, Does.Not.Contain("TelemetryFlagHealthPressureLowTier"));
            Assert.That(source, Does.Not.Contain("_healthPressureLowTierFrames"));

            string telemetryEntry = ExtractTypeBody(typesSource, "public struct ThermodynamicsHazardTelemetryEntry");
            string scanJob = ExtractTypeBody(source, "private struct ScanTelemetryJob");
            Assert.That(source, Does.Not.Contain("TelemetryFlagQualityPressure"));
            Assert.That(source, Does.Not.Contain("TelemetryFlagHealthPressureSurvival"));
            Assert.That(scanJob, Does.Not.Contain("QualityPressureQ8 >= 128u"));
            Assert.That(scanJob, Does.Not.Contain("HealthPressureQ8 > 0u"));
            Assert.That(scanJob, Does.Contain("QualityPressureQ8 = (byte)math.min(QualityPressureQ8, 255u)"));
            Assert.That(scanJob, Does.Contain("HealthPressureQ8 = (byte)math.min(HealthPressureQ8, 255u)"));
            Assert.That(telemetryEntry, Does.Contain("[FieldOffset(56)] public byte QualityPressureQ8;"));
            Assert.That(telemetryEntry, Does.Contain("[FieldOffset(57)] public byte HealthPressureQ8;"));
        }

        [Test]
        public void VolcanicUpdraftTelemetry_StoresDebrisLiftAsContinuousQ8()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "VolcanicUpdraftDirector.cs");
            string entryBody = ExtractTypeBody(source, "public struct VolcanicUpdraftTelemetryEntry");
            string finalizeJob = ExtractTypeBody(source, "internal struct VolcanicTelemetryFinalizeJob");

            Assert.That(source, Does.Not.Contain("TelemetryFlagDebrisCulled"));
            Assert.That(source, Does.Not.Contain("debris.Flags |= VolcanicUpdraftVault.TelemetryFlagDebrisCulled"));
            Assert.That(finalizeJob, Does.Not.Contain("ResolveDebrisLiftWeight(Settings.GlobalQualityWeight) <= 0.0001f"));
            Assert.That(entryBody, Does.Contain("[FieldOffset(60)] public byte DebrisLiftWeightQ8;"));
            Assert.That(source, Does.Contain("internal static byte EncodeUnitQ8(float value)"));
            Assert.That(finalizeJob, Does.Contain("DebrisLiftWeightQ8 = VolcanicUpdraftVault.EncodeUnitQ8(debrisLiftWeight)"));
        }

        [Test]
        public void ProceduralBiteIk_UsesPackedVisualOverkillWeightInsteadOfBinaryFlag()
        {
            string jobs = ReadProjectFile("Assets", "_Project", "Scripts", "Animation", "Fauna", "ProceduralBiteIkJobs.cs");
            string runtime = ReadProjectFile("Assets", "_Project", "Scripts", "Fauna", "FaunaKinematicsRuntime.cs");
            string constants = ExtractTypeBody(jobs, "public static class ProceduralBiteIkConstants");
            string jobBody = ExtractTypeBody(jobs, "public struct ProceduralBiteJob");
            string eventBody = ExtractTypeBody(jobs, "public struct BiteIkSolveEvent");
            string flagsBody = ExtractMethodBody(runtime, "private uint ResolveBiteRuntimeFlags()");
            string hullDentBody = ExtractMethodBody(runtime, "private void PublishBiteHullDent(");
            string debrisQuantityBody = ExtractMethodBody(runtime, "private static ushort ResolveBiteDebrisQuantity(");

            Assert.That(constants, Does.Contain("ResultVisualOverkillWeightMask = 0x00FF0000u"));
            Assert.That(constants, Does.Contain("PackVisualOverkillWeight(float weight01)"));
            Assert.That(constants, Does.Contain("DecodeVisualOverkillWeight01(uint flags)"));
            Assert.That(jobBody, Does.Contain("public float VisualOverkillWeight01;"));
            Assert.That(jobBody, Does.Contain("PackVisualOverkillWeight(visualOverkillWeight)"));
            Assert.That(jobBody, Does.Contain("wrapBoneCount"));
            Assert.That(eventBody, Does.Contain("[FieldOffset(108)] public float VisualOverkillWeight01;"));
            Assert.That(jobBody, Does.Contain("VisualOverkillWeight01 = ProceduralBiteIkConstants.DecodeVisualOverkillWeight01(pose.Flags)"));
            Assert.That(jobBody, Does.Not.Contain("RuntimeFlagMaximumQuality"));
            Assert.That(jobBody, Does.Not.Contain("RuntimeFlagVisualOverkill"));
            Assert.That(jobBody, Does.Not.Contain("ResultFlagVisualOverkill"));
            Assert.That(runtime, Does.Contain("VisualOverkillWeight01 = ResolveBiteVisualOverkillWeight(_globalQualityWeight)"));
            Assert.That(flagsBody, Does.Not.Contain("RuntimeFlagMaximumQuality"));
            Assert.That(flagsBody, Does.Not.Contain("RuntimeFlagVisualOverkill"));
            Assert.That(hullDentBody, Does.Contain("DecodeVisualOverkillWeight01(pose.Flags)"));
            Assert.That(hullDentBody, Does.Not.Contain("LowTierVisualOnlyFlag"));
            Assert.That(hullDentBody, Does.Not.Contain("overkill ?"));
            Assert.That(debrisQuantityBody, Does.Not.Contain("poseFlags"));
            Assert.That(runtime, Does.Not.Contain("ResultFlagVisualOverkill) != 0u"));
        }

        [Test]
        public void FaunaSdfHugging_UsesContinuousInfluenceWeight()
        {
            string runtime = ReadProjectFile("Assets", "_Project", "Scripts", "Fauna", "FaunaKinematicsRuntime.cs");
            string resolveSdfBody = ExtractMethodBody(runtime, "private void ResolveSdfPayload(");

            Assert.That(runtime, Does.Contain("private static float ResolveSdfHuggingWeight(float qualityWeight)"));
            Assert.That(resolveSdfBody, Does.Contain("float sdfHuggingWeight = ResolveSdfHuggingWeight(qualityWeight);"));
            Assert.That(resolveSdfBody, Does.Contain("sdfRange = math.max(0f, range) * sdfHuggingWeight;"));
            Assert.That(resolveSdfBody, Does.Not.Contain("SmoothQualityCurve(qualityWeight) <= 0.0001f"));
        }

        [Test]
        public void RepairTool_UsesContinuousSparkQuantityAndQ8RepairQuality()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "RepairTool.cs");
            string signals = ReadProjectFile("Assets", "_Project", "Scripts", "Core", "Signals", "GlobalSignalPayloads.DomainRemainder.cs");
            string sparkBody = ExtractMethodBody(source, "private void PublishRepairSparkSignal(");
            string quantityBody = ExtractMethodBody(source, "private static ushort ResolveRepairSparkQuantity(");
            string repairedBody = ExtractMethodBody(source, "private void PublishHullRepairedSignal(");
            string repairedSignalType = ExtractTypeBody(signals, "public struct HullRepairedSignal");

            Assert.That(sparkBody, Does.Contain("DebrisSpawnSignal.FlagToolSparks | DebrisSpawnSignal.FlagComputeShard"));
            Assert.That(sparkBody, Does.Contain("ResolveRepairSparkQuantity(safeIntensity01, quality01)"));
            Assert.That(sparkBody, Does.Not.Contain("FlagComputeShard *"));
            Assert.That(sparkBody, Does.Not.Contain("ResolveRepairQualityCurve(quality01) > 0.0001f"));
            Assert.That(quantityBody, Does.Contain("math.lerp(2f, 8f, qualityCurve)"));
            Assert.That(quantityBody, Does.Contain("math.lerp(6f, 32f, qualityCurve)"));
            Assert.That(repairedBody, Does.Contain("byte qualityWeightQ8 = ResolveRepairQualityWeightByte();"));
            Assert.That(repairedBody, Does.Contain("QualityWeightQ8 = qualityWeightQ8"));
            Assert.That(repairedBody, Does.Contain("byte flags = HullRepairedSignal.CompletedFlag;"));
            Assert.That(repairedBody, Does.Not.Contain("lowVisualProxy"));
            Assert.That(repairedBody, Does.Not.Contain("LowTierVisualOnlyFlag"));
            Assert.That(repairedSignalType, Does.Contain("SurvivalPressureVisualOnlyFlag = 1 << 1"));
            Assert.That(repairedSignalType, Does.Contain("LowTierVisualOnlyFlag = SurvivalPressureVisualOnlyFlag"));
            Assert.That(repairedSignalType, Does.Contain("[FieldOffset(62)] public byte QualityWeightQ8;"));
            Assert.That(repairedSignalType, Does.Contain("[FieldOffset(62)] public byte QualityTier;"));
        }

        [Test]
        public void ToolDiegeticDisplay_UsesScalarQualityWithoutFallbackThresholds()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "Tools", "ToolDiegeticDisplayController.cs");
            string policyBody = ExtractMethodBody(source, "private void ApplyQualityPolicy(");
            string fallbackBody = ExtractMethodBody(source, "private bool ResolveRequestedFallback()");
            string scannerTitleBody = ExtractMethodBody(source, "private void WriteScannerPrimaryLine(");

            Assert.That(policyBody, Does.Contain("_qualityFallback01 = 1f - qualityCurve;"));
            Assert.That(policyBody, Does.Contain("_visualOverkill01 = SmoothStep01"));
            Assert.That(fallbackBody, Does.Contain("return _poolUnavailableFallback;"));
            Assert.That(fallbackBody, Does.Not.Contain("_qualityFallback01 >= 0.75f"));
            Assert.That(scannerTitleBody, Does.Contain("bool compactTitle = _fallbackActive;"));
            Assert.That(scannerTitleBody, Does.Not.Contain("_qualityFallback01 >= 0.66f"));
            Assert.That(source, Does.Not.Contain("Texture used when quality pressure disables the render-texture camera."));
        }

        [Test]
        public void ToolDiegeticDisplay_RenderTextureResourceWorkIsSlowPhaseOnly()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "Tools", "ToolDiegeticDisplayController.cs");
            string slowBody = ExtractMethodBody(source, "public void SlowTick()");
            string lateFrameBody = ExtractMethodBody(source, "public void LateFrameTick()");
            string ensureBody = ExtractMethodBody(source, "private void EnsureRenderTexture()");
            string flushBody = ExtractMethodBody(source, "private void FlushPendingRenderTextureResourceState()");

            Assert.That(slowBody, Does.Contain("FlushPendingRenderTextureResourceState();"));
            Assert.That(flushBody, Does.Contain("ReleaseRenderTexture();"));
            Assert.That(flushBody, Does.Contain("EnsureRenderTexture();"));
            Assert.That(lateFrameBody, Does.Not.Contain("EnsureRenderTexture("));
            Assert.That(lateFrameBody, Does.Not.Contain("ReleaseRenderTexture("));
            Assert.That(ensureBody, Does.Contain("IRenderTexturePoolService pool = _cachedRenderTexturePool;"));
            Assert.That(ensureBody, Does.Not.Contain("CacheRenderTexturePoolCold()"));
            Assert.That(ensureBody, Does.Not.Contain("GlobalRegistry."));
        }

        [Test]
        public void MapMagicVegetation_TerrainTextureCacheAllocationIsColdOnly()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "HectonMapMagicVegetationBridge.cs");
            string cacheBody = ExtractMethodBody(source, "private bool CacheTileMasks(");
            string coldBody = ExtractMethodBody(source, "private static void RefreshTerrainTextureCachesCold(");
            string hotBody = ExtractMethodBody(source, "private static bool TryRefreshTerrainTextureCachesHot(");

            Assert.That(cacheBody, Does.Contain("TryRefreshTerrainTextureCachesHot(state, terrainData)"));
            Assert.That(cacheBody, Does.Not.Contain("RefreshTerrainTextureCachesCold("));
            Assert.That(coldBody, Does.Contain("new Texture2D[textureCount]"));
            Assert.That(hotBody, Does.Not.Contain("new Texture2D["));
        }

        [Test]
        public void SargassumDrag_VisualResourceRepairIsSlowPhaseOnly()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "SargassumGlobalDragManager.cs");
            string slowBody = ExtractMethodBody(source, "public void SlowTick()");
            string lateFrameBody = ExtractMethodBody(source, "public void LateFrameTick()");
            string repairBody = ExtractMethodBody(source, "private void EnsureVisualResourcesForSlowTick()");

            Assert.That(slowBody, Does.Contain("EnsureVisualResourcesForSlowTick();"));
            Assert.That(lateFrameBody, Does.Not.Contain("EnsureVisualResourcesForSlowTick("));
            Assert.That(lateFrameBody, Does.Not.Contain("CreateDensityTexture("));
            Assert.That(lateFrameBody, Does.Not.Contain("EnsureNestedAttachmentStorage("));
            Assert.That(lateFrameBody, Does.Not.Contain("EnsureScavengerRenderResources("));
            Assert.That(lateFrameBody, Does.Contain("_visualResourceRepairRequested = true;"));
            Assert.That(repairBody, Does.Contain("EnsureNestedAttachmentStorage();"));
            Assert.That(repairBody, Does.Contain("CreateDensityTexture();"));
            Assert.That(repairBody, Does.Contain("EnsureScavengerRenderResources();"));
        }

        [Test]
        public void VisorHud_LegacyCameraCommandBuffersAreDecommissioned()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Visor", "VisorHUDController.cs");
            string slowBody = ExtractMethodBody(source, "public void SlowTick()");
            string configureBody = ExtractMethodBody(source, "private void ConfigureHudScissorCommandBuffers()");
            string repairBody = ExtractMethodBody(source, "private void FlushHudScissorCommandBufferRepairSlow()");
            string coldBody = ExtractMethodBody(source, "private void EnsureHudScissorCommandBuffersCold()");
            string capabilityBody = ExtractMethodBody(source, "private void CacheGraphicsCapabilitiesCold()");

            Assert.That(slowBody, Does.Contain("FlushHudScissorCommandBufferRepairSlow();"));
            Assert.That(configureBody, Does.Contain("ClearHudScissorCommandBufferState();"));
            Assert.That(configureBody, Does.Not.Contain("EnsureHudScissorCommandBuffersCold("));
            Assert.That(configureBody, Does.Not.Contain("new CommandBuffer"));
            Assert.That(repairBody, Does.Contain("EnsureHudScissorCommandBuffersCold();"));
            Assert.That(coldBody, Does.Not.Contain("new CommandBuffer"));
            Assert.That(source, Does.Not.Contain("AddCommandBuffer("));
            Assert.That(source, Does.Not.Contain("RemoveCommandBuffer("));
            Assert.That(capabilityBody, Does.Contain("SampleScriptableRenderPipelineActiveCold()"));
        }

        [Test]
        public void CelestialEngine_AtmosphereLutUsesSampleArrayWithoutRuntimeTextureUpload()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "HectonCelestialEngine.cs");
            string slowBody = ExtractMethodBody(source, "public void SlowTick()");
            string visualBody = ExtractMethodBody(source, "private void FlushCelestialVisualSync()");
            string visualUpdateBody = ExtractMethodBody(source, "private void TryUpdateDynamicCelestialAtmosphereVisualSync(");
            string rebuildBody = ExtractMethodBody(source, "private void RebuildCelestialAtmosphereLut(");
            string publishBody = ExtractMethodBody(source, "private void PublishCelestialAtmosphereLut(");

            Assert.That(slowBody, Does.Contain("FlushCelestialAtmosphereLutRepairSlow();"));
            Assert.That(visualBody, Does.Contain("TryUpdateDynamicCelestialAtmosphereVisualSync(sunElevation);"));
            Assert.That(visualBody, Does.Not.Contain("EnsureCelestialAtmosphereLutReady("));
            Assert.That(visualBody, Does.Not.Contain("new Texture2D("));
            Assert.That(visualUpdateBody, Does.Contain("QueueCelestialAtmosphereLutRepair();"));
            Assert.That(visualUpdateBody, Does.Not.Contain("EnsureCelestialAtmosphereAuthoring("));
            Assert.That(rebuildBody, Does.Contain("_celestialAtmosphereLutSamples[i]"));
            Assert.That(rebuildBody, Does.Not.Contain("SetPixels("));
            Assert.That(rebuildBody, Does.Not.Contain("Apply("));
            Assert.That(publishBody, Does.Contain("Shader.SetGlobalColorArray(_ID_CelestialAtmosphereLutSamples, _celestialAtmosphereLutSamples);"));
            Assert.That(publishBody, Does.Not.Contain("Shader.SetGlobalTexture(_ID_CelestialAtmosphereLut"));
            Assert.That(source, Does.Not.Contain("new Texture2D("));
            Assert.That(source, Does.Not.Contain("SetPixels("));
            Assert.That(source, Does.Not.Contain("allowResourceRepair"));
        }

        [Test]
        public void GlobalWeatherDirector_NoirFogLutUsesSampleArrayWithoutRuntimeTextureUpload()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Environment", "GlobalWeatherDirector.cs");
            string slowBody = ExtractMethodBody(source, "public void SlowTick()");
            string lateFrameBody = ExtractMethodBody(source, "public void LateFrameTick()");
            string flushBody = ExtractMethodBody(source, "private void FlushNoirFogLutSamples()");
            string slowRepairBody = ExtractMethodBody(source, "private void FlushNoirFogLutRepairSlow()");
            string ensureBody = ExtractMethodBody(source, "private void EnsureNoirFogLutResources()");
            string publishBody = ExtractMethodBody(source, "private void PublishNoirFogShaderState()");

            Assert.That(slowBody, Does.Contain("FlushNoirFogLutRepairSlow();"));
            Assert.That(lateFrameBody, Does.Not.Contain("EnsureNoirFogLutResources("));
            Assert.That(flushBody, Does.Contain("RebuildNoirFogLutSamples("));
            Assert.That(flushBody, Does.Not.Contain("EnsureNoirFogLutResources("));
            Assert.That(slowRepairBody, Does.Contain("EnsureNoirFogLutResources();"));
            Assert.That(slowRepairBody, Does.Contain("_weatherShaderDirty = true;"));
            Assert.That(ensureBody, Does.Not.Contain("new Texture2D("));
            Assert.That(ensureBody, Does.Not.Contain("new Color["));
            Assert.That(publishBody, Does.Contain("Shader.SetGlobalColorArray(_NoirFogLutSamplesId, _noirFogLutSamples);"));
            Assert.That(source, Does.Contain("private readonly Color[] _noirFogLutSamples = new Color[NoirFogLutSampleCount];"));
            Assert.That(source, Does.Not.Contain("_noirFogLutTexture"));
            Assert.That(source, Does.Not.Contain("SetPixels("));
            Assert.That(source, Does.Not.Contain("Apply("));
        }

        [Test]
        public void NativeTrailRenderer_BufferRepairIsSlowPhaseOnly()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "VFX", "NativeTrailRenderer.cs");
            string lateFrameBody = ExtractMethodBody(source, "public void LateFrameTick()");
            string slowBody = ExtractMethodBody(source, "public void SlowTick()");
            string ensureBody = ExtractMethodBody(source, "private void EnsureBuffers()");

            Assert.That(source, Does.Contain("ISlowTickable"));
            Assert.That(lateFrameBody, Does.Contain("QueueBufferRepair();"));
            Assert.That(lateFrameBody, Does.Not.Contain("EnsureBuffers("));
            Assert.That(slowBody, Does.Contain("EnsureBuffers();"));
            Assert.That(ensureBody, Does.Contain("new TrailSample[_resolvedCapacity]"));
            Assert.That(ensureBody, Does.Contain("new Mesh"));
        }

        [Test]
        public void GpuScatterLod_VisibleCountReadbackRepairIsSlowPhaseOnly()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Rendering", "Scatter", "GpuScatterLodManager.cs");
            string visualBody = ExtractMethodBody(source, "private void RunScatterVisualTick(");
            string slowBody = ExtractMethodBody(source, "public void SlowTick()");
            string readbackBody = ExtractMethodBody(source, "private void UpdateVisibleCountReadback(");
            string repairBody = ExtractMethodBody(source, "private void FlushVisibleCountReadbackRepairSlow()");
            string ensureBody = ExtractMethodBody(source, "private bool EnsureVisibleCountReadbackData()");

            Assert.That(visualBody, Does.Contain("HasGpuStateReady()"));
            Assert.That(visualBody, Does.Not.Contain("TryEnsureGpuState("));
            Assert.That(slowBody, Does.Contain("FlushVisibleCountReadbackRepairSlow();"));
            Assert.That(readbackBody, Does.Contain("QueueVisibleCountReadbackRepair();"));
            Assert.That(readbackBody, Does.Not.Contain("EnsureVisibleCountReadbackData("));
            Assert.That(repairBody, Does.Contain("EnsureVisibleCountReadbackData();"));
            Assert.That(ensureBody, Does.Contain("new NativeArray<uint>"));
        }

        [Test]
        public void CarveDebris_FallbackRenderResourcesAreSlowPhaseOnly()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "VFX", "Debris", "CarveDebrisComputeRenderer.cs");
            string slowBody = ExtractMethodBody(source, "public void SlowTick()");
            string renderBody = ExtractMethodBody(source, "private void RenderDebris()");
            string materialBody = ExtractMethodBody(source, "private Material ResolveMaterial()");
            string meshBody = ExtractMethodBody(source, "private Mesh ResolveMesh()");
            string repairBody = ExtractMethodBody(source, "private void FlushFallbackRenderResourceRepairSlow()");
            string ensureBody = ExtractMethodBody(source, "private void EnsureOwnedMaterial()");

            Assert.That(slowBody, Does.Contain("FlushFallbackRenderResourceRepairSlow();"));
            Assert.That(renderBody, Does.Contain("ResolveMaterial();"));
            Assert.That(materialBody, Does.Contain("QueueFallbackRenderResourceRepair();"));
            Assert.That(materialBody, Does.Not.Contain("EnsureFallbackRenderResources("));
            Assert.That(meshBody, Does.Contain("QueueFallbackRenderResourceRepair();"));
            Assert.That(meshBody, Does.Not.Contain("BuildOctahedronMesh("));
            Assert.That(repairBody, Does.Contain("EnsureFallbackRenderResources();"));
            Assert.That(ensureBody, Does.Contain("new Material("));
        }

        [Test]
        public void GPUScatterDirector_VisibleCountReadbackRepairIsSlowPhaseOnly()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "GPUScatterDirector.cs");
            string lateFrameBody = ExtractMethodBody(source, "public void LateFrameTick()");
            string slowBody = ExtractMethodBody(source, "public void SlowTick()");
            string readbackBody = ExtractMethodBody(source, "private void UpdateVisibleCountReadback(");
            string repairBody = ExtractMethodBody(source, "private void FlushVisibleCountReadbackRepairSlow()");
            string ensureBody = ExtractMethodBody(source, "private bool EnsureVisibleCountReadbackData()");

            Assert.That(lateFrameBody, Does.Contain("UpdateVisibleCountReadback(frameIndex);"));
            Assert.That(slowBody, Does.Contain("FlushVisibleCountReadbackRepairSlow();"));
            Assert.That(readbackBody, Does.Contain("QueueVisibleCountReadbackRepair();"));
            Assert.That(readbackBody, Does.Not.Contain("EnsureVisibleCountReadbackData("));
            Assert.That(repairBody, Does.Contain("EnsureVisibleCountReadbackData();"));
            Assert.That(ensureBody, Does.Contain("new NativeArray<uint>"));
        }

        [Test]
        public void IndirectVegetation_CullTelemetryReadbackRepairIsSlowPhaseOnly()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "HectonIndirectVegetationRenderer.cs");
            string slowBody = ExtractMethodBody(source, "public void SlowTick()");
            string requestBody = ExtractMethodBody(source, "private void RequestCullTelemetryReadback(");
            string repairBody = ExtractMethodBody(source, "private void FlushCullTelemetryReadbackRepairSlow()");
            string ensureBody = ExtractMethodBody(source, "private void EnsureCullTelemetryReadbackData()");

            Assert.That(slowBody, Does.Contain("FlushCullTelemetryReadbackRepairSlow();"));
            Assert.That(requestBody, Does.Contain("QueueCullTelemetryReadbackRepair();"));
            Assert.That(requestBody, Does.Not.Contain("EnsureCullTelemetryReadbackData("));
            Assert.That(repairBody, Does.Contain("EnsureCullTelemetryReadbackData();"));
            Assert.That(ensureBody, Does.Contain("new NativeArray<uint>"));
        }

        [Test]
        public void MicroFauna_ParasiteLatchReadbackRepairIsSlowPhaseOnly()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "SargassumMicroFaunaBoids.cs");
            string slowBody = ExtractMethodBody(source, "public void SlowTick()");
            string requestBody = ExtractMethodBody(source, "private void TryRequestParasiteLatchReadback(");
            string repairBody = ExtractMethodBody(source, "private void FlushParasiteLatchReadbackRepairSlow()");
            string ensureBody = ExtractMethodBody(source, "private bool EnsureParasiteLatchReadbackData()");

            Assert.That(slowBody, Does.Contain("FlushParasiteLatchReadbackRepairSlow();"));
            Assert.That(requestBody, Does.Contain("QueueParasiteLatchReadbackRepair();"));
            Assert.That(requestBody, Does.Not.Contain("EnsureParasiteLatchReadbackData("));
            Assert.That(repairBody, Does.Contain("EnsureParasiteLatchReadbackData();"));
            Assert.That(ensureBody, Does.Contain("new NativeArray<int>"));
        }

        [Test]
        public void MapMagic_TileHeightReadbackRepairIsSlowPhaseOnly()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "HectonMapMagicVegetationBridge.cs");
            string slowBody = ExtractMethodBody(source, "public void SlowTick()");
            string refreshBody = ExtractMethodBody(source, "private bool CacheTileMasks(");
            string repairBody = ExtractMethodBody(source, "private void FlushTileHeightReadbackRepairsSlow()");
            string ensureBody = ExtractMethodBody(source, "private static void EnsureTileHeightReadbackData(");

            Assert.That(slowBody, Does.Contain("FlushTileHeightReadbackRepairsSlow();"));
            Assert.That(refreshBody, Does.Contain("QueueTileHeightReadbackRepair(state, heightSampleCount);"));
            Assert.That(refreshBody, Does.Not.Contain("EnsureTileHeightReadbackData("));
            Assert.That(repairBody, Does.Contain("EnsureTileHeightReadbackData(state, state.HeightReadbackRepairSampleCount);"));
            Assert.That(ensureBody, Does.Contain("new NativeArray<ushort>"));
        }

        [Test]
        public void LODSystemManager_QualitySettingsMutationIsSlowPhaseOnly()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "LODSystemManager.cs");
            string lateFrameBody = ExtractMethodBody(source, "public void LateFrameTick()");
            string slowBody = ExtractMethodBody(source, "public void SlowTick()");
            string policyBody = ExtractMethodBody(source, "private void FlushQualityPolicySlow()");
            string shaderBody = ExtractMethodBody(source, "private void FlushQualityShaderVisualSync()");
            string registerBody = ExtractMethodBody(source, "private void TryRegister()");

            Assert.That(source, Does.Contain("ITickable, ISlowTickable, ILateFrameTickable"));
            Assert.That(lateFrameBody, Does.Contain("FlushQualityShaderVisualSync();"));
            Assert.That(lateFrameBody, Does.Not.Contain("QualitySettings."));
            Assert.That(lateFrameBody, Does.Not.Contain("FlushQualityPolicySlow();"));
            Assert.That(slowBody, Does.Contain("FlushQualityPolicySlow();"));
            Assert.That(policyBody, Does.Contain("QualitySettings.lodBias = targetBias;"));
            Assert.That(policyBody, Does.Not.Contain("DistanceMath.PushShaderMathLod"));
            Assert.That(shaderBody, Does.Contain("DistanceMath.PushShaderMathLod(qualityWeight01);"));
            Assert.That(registerBody, Does.Contain("GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);"));
        }

        [Test]
        public void IndirectArgsUploads_DoNotAllocateGraphicsBuffersInHotWriters()
        {
            string boids = ReadProjectFile("Assets", "_Project", "Scripts", "World", "SargassumMicroFaunaBoids.cs");
            string outpost = ReadProjectFile("Assets", "_Project", "Scripts", "World", "Outposts", "MarauderOutpostGenerationService.cs");
            string boidUploadBody = ExtractMethodBody(boids, "private bool UploadBoidIndirectArgs(");
            string outpostArgsBody = ExtractMethodBody(outpost, "private void UpdateIndirectArgsBuffer(");
            string boidColdBody = ExtractMethodBody(boids, "private bool TryEnsureBoidIndirectArgsBufferCold()");

            Assert.That(boidUploadBody, Does.Contain("new GraphicsBuffer.IndirectDrawIndexedArgs"));
            Assert.That(outpostArgsBody, Does.Contain("new GraphicsBuffer.IndirectDrawIndexedArgs"));
            Assert.That(boidUploadBody, Does.Not.Contain("new GraphicsBuffer("));
            Assert.That(outpostArgsBody, Does.Not.Contain("new GraphicsBuffer("));
            Assert.That(boidColdBody, Does.Contain("new GraphicsBuffer("));
        }

        [Test]
        public void VehicleSubOs_RenderTextureFormatIsNotQualityForked()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "VehicleSubOsCockpitRuntime.cs");
            string formatBody = ExtractMethodBody(source, "private RenderTextureFormat ResolvePanelRenderTextureFormat()");

            Assert.That(formatBody, Does.Contain("_supportsRgb565RenderTextureCold"));
            Assert.That(formatBody, Does.Contain("RenderTextureFormat.RGB565"));
            Assert.That(source, Does.Contain("SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGB565)"));
            Assert.That(source, Does.Contain("RenderTextureFormat format = ResolvePanelRenderTextureFormat();"));
            Assert.That(source, Does.Not.Contain("ResolvePanelRenderTextureFormat(quality)"));
            Assert.That(formatBody, Does.Not.Contain("ResolveCheapVisualWeight"));
            Assert.That(formatBody, Does.Not.Contain("qualityWeight01"));
            Assert.That(source, Does.Contain("float curve = SmoothQuality(_qualityWeight01);"));
            Assert.That(source, Does.Contain("ResolveQualityDimension"));
        }

        [Test]
        public void VehicleSubOs_ExternalFeedIsContinuousAndResizesWithQuality()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "VehicleSubOsCockpitRuntime.cs");
            string weightBody = ExtractMethodBody(source, "private static float ResolveExternalFeedWeight(");
            string stateBody = ExtractMethodBody(source, "private void UpdateExternalFeedState()");
            string currentBody = ExtractMethodBody(source, "private bool IsExternalRenderTargetCurrent()");
            string ensureBody = ExtractMethodBody(source, "private void EnsureExternalRenderTextureCurrent()");
            string acquireBody = ExtractMethodBody(source, "private void AcquireExternalRenderTexture()");
            string visibilityBody = ExtractMethodBody(source, "private bool IsOffscreenUiVisible()");
            string modeBody = ExtractMethodBody(source, "private int ResolveStatusDisplayMode()");
            string textureBody = ExtractMethodBody(source, "private Texture ResolveActiveScreenTexture()");

            Assert.That(source, Does.Not.Contain("ExternalFeedEnableThreshold"));
            Assert.That(source, Does.Contain("private const float MinExternalFeedBlendWeight = 0.125f;"));
            Assert.That(weightBody, Does.Contain("float curve = SmoothQuality(qualityWeight01);"));
            Assert.That(weightBody, Does.Contain("math.lerp(MinExternalFeedBlendWeight, 1f, curve)"));
            Assert.That(stateBody, Does.Not.Contain("_externalFeedWeight01 <= 0.0001f"));
            Assert.That(visibilityBody, Does.Not.Contain("_externalFeedWeight01 <= 0.0001f"));
            Assert.That(modeBody, Does.Not.Contain("_externalFeedWeight01 <= 0.0001f"));
            Assert.That(textureBody, Does.Not.Contain("_externalFeedWeight01 <= 0.0001f"));
            Assert.That(currentBody, Does.Contain("_externalRenderTexture.width == ResolveExternalWidth()"));
            Assert.That(currentBody, Does.Contain("_externalRenderTexture.format == ResolvePanelRenderTextureFormat()"));
            Assert.That(ensureBody, Does.Contain("ReleaseExternalRenderTexture();"));
            Assert.That(ensureBody, Does.Contain("AcquireExternalRenderTexture();"));
            Assert.That(acquireBody, Does.Contain("RenderTextureFormat format = ResolvePanelRenderTextureFormat();"));
            Assert.That(acquireBody, Does.Contain("bool shouldEnableCamera = _externalRenderTexture != null;"));
        }

        [Test]
        public void DiegeticPanelController_ColorFormatIsHardwareRouteNotQualityFork()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "DiegeticPanelController.cs");
            string formatBody = ExtractMethodBody(source, "private GraphicsFormat ResolveColorGraphicsFormat()");

            Assert.That(formatBody, Does.Contain("return _isMx350Tier"));
            Assert.That(formatBody, Does.Contain("GraphicsFormat.B5G6R5_UNormPack16"));
            Assert.That(formatBody, Does.Contain("GraphicsFormat.R8G8B8A8_UNorm"));
            Assert.That(formatBody, Does.Not.Contain("_qualityWeight01 <"));
            Assert.That(formatBody, Does.Not.Contain("0.72f"));
        }

        [Test]
        public void WristHologramHudRuntime_CsvOverridePollingIsSlowPhaseOnly()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "WristHologramHudRuntime.cs");
            string lateFrameBody = ExtractMethodBody(source, "public void LateFrameTick()");
            string slowBody = ExtractMethodBody(source, "public void SlowTick()");
            string buildTextBody = ExtractMethodBody(source, "private void BuildTextQuadsOwnerPhase(");
            string flushDumpBody = ExtractMethodBody(source, "private void FlushQueuedBlackBoxDump()");
            string registerBody = ExtractMethodBody(source, "private void TryRegisterTickLanes()");
            string unregisterBody = ExtractMethodBody(source, "private void TryUnregisterTickLanes()");

            Assert.That(source, Does.Contain("MonoBehaviour, ISlowTickable, ILateFrameTickable"));
            Assert.That(lateFrameBody, Does.Not.Contain("PollCsvOverride"));
            Assert.That(lateFrameBody, Does.Not.Contain("TryReloadFontMetricsOverride"));
            Assert.That(lateFrameBody, Does.Not.Contain("File."));
            Assert.That(lateFrameBody, Does.Not.Contain("new FileStream"));
            Assert.That(slowBody, Does.Contain("PollCsvOverride(_lastHudDeltaTime);"));
            Assert.That(slowBody, Does.Contain("FlushQueuedBlackBoxDump();"));
            Assert.That(buildTextBody, Does.Contain("QueueBlackBoxDump();"));
            Assert.That(buildTextBody, Does.Not.Contain("DumpBlackBoxOnce();"));
            Assert.That(flushDumpBody, Does.Contain("DumpBlackBoxOnce();"));
            Assert.That(registerBody, Does.Contain("GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.UI)"));
            Assert.That(unregisterBody, Does.Contain("GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.UI)"));
        }

        [Test]
        public void PDADecryptionSpectrogramPanel_TelemetryDumpIsSlowPhaseOnly()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "PDADecryptionSpectrogramPanel.cs");
            string lateFrameBody = ExtractMethodBody(source, "public void LateFrameTick()");
            string slowBody = ExtractMethodBody(source, "public void SlowTick()");
            string commitBody = ExtractMethodBody(source, "private void CommitWaveResult(");
            string recordBody = ExtractMethodBody(source, "private void RecordTelemetry()");
            string flushDumpBody = ExtractMethodBody(source, "private void FlushQueuedTelemetryDump()");
            string registerBody = ExtractMethodBody(source, "private void TryRegisterTickHandlers()");
            string unregisterBody = ExtractMethodBody(source, "private void TryUnregisterTickHandlers()");

            Assert.That(source, Does.Contain("MonoBehaviour, ISlowTickable, ILateFrameTickable"));
            Assert.That(lateFrameBody, Does.Not.Contain("DumpTelemetryCold"));
            Assert.That(lateFrameBody, Does.Not.Contain("FileStream"));
            Assert.That(commitBody, Does.Contain("QueueTelemetryDump();"));
            Assert.That(commitBody, Does.Not.Contain("DumpTelemetryCold();"));
            Assert.That(recordBody, Does.Contain("QueueTelemetryDump();"));
            Assert.That(recordBody, Does.Not.Contain("DumpTelemetryCold();"));
            Assert.That(slowBody, Does.Contain("FlushQueuedTelemetryDump();"));
            Assert.That(flushDumpBody, Does.Contain("DumpTelemetryCold();"));
            Assert.That(registerBody, Does.Contain("GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.UI)"));
            Assert.That(unregisterBody, Does.Contain("GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.UI)"));
        }

        [Test]
        public void PDAEncyclopediaStreamer_BlackBoxDumpIsSlowPhaseOnly()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "PDAEncyclopediaStreamer.cs");
            string lateFrameBody = ExtractMethodBody(source, "public void LateFrameTick()");
            string slowBody = ExtractMethodBody(source, "public void SlowTick()");
            string flushDumpBody = ExtractMethodBody(source, "private void FlushQueuedBlackBoxDump()");
            string registerBody = ExtractMethodBody(source, "private void TryRegisterDispatcherLanes()");
            string unregisterBody = ExtractMethodBody(source, "private void UnregisterDispatcherLanes()");

            Assert.That(source, Does.Contain("ISlowTickable"));
            Assert.That(source, Does.Contain("ILateFrameTickable"));
            Assert.That(lateFrameBody, Does.Contain("QueueBlackBoxDump();"));
            Assert.That(lateFrameBody, Does.Not.Contain("DumpBlackBox();"));
            Assert.That(lateFrameBody, Does.Not.Contain("FileStream"));
            Assert.That(slowBody, Does.Contain("FlushQueuedBlackBoxDump();"));
            Assert.That(flushDumpBody, Does.Contain("DumpBlackBox();"));
            Assert.That(registerBody, Does.Contain("GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.UI)"));
            Assert.That(unregisterBody, Does.Contain("GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.UI)"));
        }

        [Test]
        public void DiegeticGyroCompass_BlackBoxDumpIsSlowPhaseOnly()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "Navigation", "DiegeticGyroCompassRuntime.cs");
            string lateFrameBody = ExtractMethodBody(source, "public void LateFrameTick()");
            string slowBody = ExtractMethodBody(source, "public void SlowTick()");
            string scheduleBody = ExtractMethodBody(source, "private void ScheduleDrift(");
            string commitCompletedBody = ExtractMethodBody(source, "private void CommitCompletedState()");
            string flushDumpBody = ExtractMethodBody(source, "private void FlushQueuedBlackBoxDump()");

            Assert.That(source, Does.Contain("ISlowTickable, ILateFrameTickable"));
            Assert.That(lateFrameBody, Does.Not.Contain("DumpBlackBoxOnce"));
            Assert.That(lateFrameBody, Does.Not.Contain("FileStream"));
            Assert.That(scheduleBody, Does.Contain("QueueBlackBoxDump(state.BlackBoxCursor);"));
            Assert.That(scheduleBody, Does.Not.Contain("DumpBlackBoxOnce("));
            Assert.That(commitCompletedBody, Does.Contain("QueueBlackBoxDump(state.BlackBoxCursor);"));
            Assert.That(commitCompletedBody, Does.Not.Contain("DumpBlackBoxOnce("));
            Assert.That(slowBody, Does.Contain("FlushQueuedBlackBoxDump();"));
            Assert.That(flushDumpBody, Does.Contain("DumpBlackBoxOnce(_queuedBlackBoxCursor);"));
        }

        [Test]
        public void OpenXRManualOverrideLever_BlackBoxDumpIsSlowPhaseOnly()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "VR", "OpenXRManualOverrideLever.cs");
            string tickBody = ExtractMethodBody(source, "public void Tick(");
            string slowBody = ExtractMethodBody(source, "public void SlowTick()");
            string integrateBody = ExtractMethodBody(source, "private void IntegrateSpring(");
            string blackBoxFrameBody = ExtractMethodBody(source, "private void WriteBlackBoxFrame(");
            string flushDumpBody = ExtractMethodBody(source, "private void FlushQueuedBlackBoxDump()");
            string registerBody = ExtractMethodBody(source, "private void TryRegisterTick()");
            string unregisterBody = ExtractMethodBody(source, "private void TryUnregisterTick()");

            Assert.That(source, Does.Contain("IUpdatable, ISlowTickable, ILateFrameTickable"));
            Assert.That(tickBody, Does.Not.Contain("DumpBlackBox();"));
            Assert.That(tickBody, Does.Not.Contain("FileStream"));
            Assert.That(integrateBody, Does.Contain("QueueBlackBoxDump();"));
            Assert.That(integrateBody, Does.Not.Contain("DumpBlackBox();"));
            Assert.That(blackBoxFrameBody, Does.Contain("QueueBlackBoxDump();"));
            Assert.That(blackBoxFrameBody, Does.Not.Contain("DumpBlackBox();"));
            Assert.That(slowBody, Does.Contain("FlushQueuedBlackBoxDump();"));
            Assert.That(flushDumpBody, Does.Contain("DumpBlackBox();"));
            Assert.That(registerBody, Does.Contain("GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Player)"));
            Assert.That(unregisterBody, Does.Contain("GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Player)"));
        }

        [Test]
        public void SuitHud_AcousticRadarTextureRefreshIsSlowPhaseOnly()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "SuitHUDV4CanvasOverlay.cs");
            string slowBody = ExtractMethodBody(source, "public void SlowTick()");
            string radarBody = ExtractMethodBody(source, "private void RefreshAcousticRadarPayload()");
            string flushBody = ExtractMethodBody(source, "private void FlushPendingAcousticRadarTextureRefresh()");
            string ensureBody = ExtractMethodBody(source, "private bool EnsureAcousticRadarTexture(");

            Assert.That(radarBody, Does.Contain("IsAcousticRadarTextureReady(radarResolution)"));
            Assert.That(radarBody, Does.Contain("QueueAcousticRadarTextureRefresh(radarResolution);"));
            Assert.That(radarBody, Does.Not.Contain("EnsureAcousticRadarTexture("));
            Assert.That(radarBody, Does.Not.Contain("new Texture2D"));
            Assert.That(radarBody, Does.Not.Contain("Destroy("));
            Assert.That(slowBody, Does.Contain("FlushPendingAcousticRadarTextureRefresh();"));
            Assert.That(flushBody, Does.Contain("EnsureAcousticRadarTexture(resolution);"));
            Assert.That(ensureBody, Does.Contain("new Texture2D"));
            Assert.That(ensureBody, Does.Contain("Destroy(_acousticRadarTexture);"));
        }

        [Test]
        public void TopographicalSonar_BlackBoxDumpIsSlowPhaseOnly()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "TopographicalSonar", "TopographicalSonarSynthesizer.cs");
            string lateFrameBody = ExtractMethodBody(source, "public void LateFrameTick()");
            string commitBody = ExtractMethodBody(source, "private void CommitCompletedScan()");
            string slowBody = ExtractMethodBody(source, "public void SlowTick()");
            string flushDumpBody = ExtractMethodBody(source, "private void FlushQueuedBlackBoxDump()");
            string registerBody = ExtractMethodBody(source, "private void TryRegisterSlowTickable()");
            string unregisterBody = ExtractMethodBody(source, "private void TryUnregisterSlowTickable()");

            Assert.That(source, Does.Contain("ILateFrameTickable, ISlowTickable, IRenderable"));
            Assert.That(lateFrameBody, Does.Not.Contain("DumpBlackBox();"));
            Assert.That(commitBody, Does.Contain("QueueBlackBoxDump();"));
            Assert.That(commitBody, Does.Not.Contain("DumpBlackBox();"));
            Assert.That(slowBody, Does.Contain("FlushQueuedBlackBoxDump();"));
            Assert.That(flushDumpBody, Does.Contain("DumpBlackBox();"));
            Assert.That(registerBody, Does.Contain("GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.UI)"));
            Assert.That(unregisterBody, Does.Contain("GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.UI)"));
        }

        [Test]
        public void DiegeticGlitchSurgeon_BlackBoxDumpIsSlowPhaseOnly()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "DiegeticGlitchSurgeonRuntime.cs");
            string lateFrameBody = ExtractMethodBody(source, "public void LateFrameTick()");
            string slowBody = ExtractMethodBody(source, "public void SlowTick()");
            string inspectBody = ExtractMethodBody(source, "private void InspectAndDumpIfNeeded()");
            string flushDumpBody = ExtractMethodBody(source, "private void FlushQueuedBlackBoxDump()");

            Assert.That(source, Does.Contain("ISlowTickable, ILateFrameTickable"));
            Assert.That(lateFrameBody, Does.Not.Contain("DumpBlackBox("));
            Assert.That(lateFrameBody, Does.Not.Contain("FileStream"));
            Assert.That(inspectBody, Does.Contain("QueueBlackBoxDump(faultFlags);"));
            Assert.That(inspectBody, Does.Not.Contain("DumpBlackBox(faultFlags);"));
            Assert.That(slowBody, Does.Contain("FlushQueuedBlackBoxDump();"));
            Assert.That(flushDumpBody, Does.Contain("DumpBlackBox(faultFlags);"));
        }

        [Test]
        public void DiegeticTooltipSystem_BlackBoxDumpIsSlowPhaseOnly()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "DiegeticTooltipSystem.cs");
            string renderBody = ExtractMethodBody(source, "public void Render(");
            string slowBody = ExtractMethodBody(source, "public void SlowTick()");
            string flushDumpBody = ExtractMethodBody(source, "private void FlushQueuedBlackBoxDump()");
            string registerBody = ExtractMethodBody(source, "private void TryRegisterRuntime()");
            string unregisterBody = ExtractMethodBody(source, "private void UnregisterRuntime()");

            Assert.That(source, Does.Contain("ISlowTickable, ILateFrameTickable"));
            Assert.That(renderBody, Does.Contain("QueueBlackBoxDump();"));
            Assert.That(renderBody, Does.Not.Contain("DumpBlackBox();"));
            Assert.That(renderBody, Does.Not.Contain("FileStream"));
            Assert.That(slowBody, Does.Contain("FlushQueuedBlackBoxDump();"));
            Assert.That(flushDumpBody, Does.Contain("DumpBlackBox();"));
            Assert.That(registerBody, Does.Contain("GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.UI)"));
            Assert.That(unregisterBody, Does.Contain("GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.UI)"));
        }

        [Test]
        public void DiegeticVisorHudMesh_BlackBoxDumpIsSlowPhaseOnly()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "DiegeticVisorHudMesh.cs");
            string lateFrameBody = ExtractMethodBody(source, "public void LateFrameTick()");
            string slowBody = ExtractMethodBody(source, "public void SlowTick()");
            string materialBody = ExtractMethodBody(source, "private void ApplyMaterialState()");
            string telemetryBody = ExtractMethodBody(source, "private void RecordTelemetry()");
            string flushDumpBody = ExtractMethodBody(source, "private void FlushQueuedBlackBoxDump()");
            string registerBody = ExtractMethodBody(source, "private void TryRegisterTick()");
            string unregisterBody = ExtractMethodBody(source, "private void TryUnregisterTick()");

            Assert.That(source, Does.Contain("ISlowTickable, ILateFrameTickable"));
            Assert.That(lateFrameBody, Does.Not.Contain("DumpBlackBox();"));
            Assert.That(lateFrameBody, Does.Not.Contain("FileStream"));
            Assert.That(materialBody, Does.Contain("QueueBlackBoxDump();"));
            Assert.That(materialBody, Does.Not.Contain("DumpBlackBox();"));
            Assert.That(telemetryBody, Does.Contain("QueueBlackBoxDump();"));
            Assert.That(telemetryBody, Does.Not.Contain("DumpBlackBox();"));
            Assert.That(slowBody, Does.Contain("FlushQueuedBlackBoxDump();"));
            Assert.That(flushDumpBody, Does.Contain("DumpBlackBox();"));
            Assert.That(registerBody, Does.Contain("GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.UI)"));
            Assert.That(unregisterBody, Does.Contain("GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.UI)"));
        }

        [Test]
        public void WristHudState_StoresMathLodPressureAsQ8InsteadOfSurvivalMathFlag()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "WristHologramHudRuntime.cs");
            string stateType = ExtractTypeBody(source, "public struct WristHudStateDTO");
            string executeBody = ExtractMethodBody(source, "public void Execute()");
            string seedBody = ExtractMethodBody(source, "private void SeedInitialState()");
            string encodeBody = ExtractMethodBody(source, "private static int EncodeUnitWeightQ8(");

            Assert.That(source, Does.Not.Contain("StateFlagSurvivalMath"));
            Assert.That(stateType, Does.Contain("[FieldOffset(236)]"));
            Assert.That(stateType, Does.Contain("public int MathLodPressureQ8;"));
            Assert.That(executeBody, Does.Contain("state.MathLodPressureQ8 = EncodeUnitWeightQ8(mathLodPressure01, 0f);"));
            Assert.That(executeBody, Does.Not.Contain("mathLodPressure01 >= 0.75f"));
            Assert.That(executeBody, Does.Not.Contain("state.Flags |= StateFlagSurvivalMath"));
            Assert.That(seedBody, Does.Contain("state.MathLodPressureQ8 = EncodeUnitWeightQ8(ResolveMathLodPressure01(), 0f);"));
            Assert.That(encodeBody, Does.Contain("math.select(fallback01, value01, math.isfinite(value01))"));
            Assert.That(encodeBody, Does.Contain("* 255f"));
        }

        [Test]
        public void HabitatModuleStress_PublishesContinuousQ8WithoutLowTierFlag()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Construction", "HabitatGraphManager.cs");
            string signals = ReadProjectFile("Assets", "_Project", "Scripts", "Core", "Signals", "GlobalSignalPayloads.DomainRemainder.cs");
            string publishBody = ExtractMethodBody(source, "private void TryPublishBaseModuleCompromisedSignal(");
            string signalType = ExtractTypeBody(signals, "public struct BaseModuleCompromisedSignal");

            Assert.That(source, Does.Contain("byte moduleStressQualityWeightQ8 = ResolveModuleStressQualityWeightQ8(qualityWeight);"));
            Assert.That(source, Does.Contain("private static byte ResolveModuleStressQualityWeightQ8(float globalQualityWeight)"));
            Assert.That(source, Does.Not.Contain("ResolveModuleStressQualityProfileByte"));
            Assert.That(publishBody, Does.Contain("Flags = BaseModuleCompromisedSignal.MaxDeformationFlag"));
            Assert.That(publishBody, Does.Contain("QualityWeightQ8 = qualityWeightQ8"));
            Assert.That(publishBody, Does.Not.Contain("LowTierVisualOnlyFlag"));
            Assert.That(publishBody, Does.Not.Contain("ResolveModuleStressDisplacementMaxMeters(globalQualityWeight) <= 0f"));
            Assert.That(signalType, Does.Contain("SurvivalPressureVisualOnlyFlag = 1 << 1"));
            Assert.That(signalType, Does.Contain("LowTierVisualOnlyFlag = SurvivalPressureVisualOnlyFlag"));
            Assert.That(signalType, Does.Contain("[FieldOffset(45)] public byte QualityWeightQ8;"));
            Assert.That(signalType, Does.Contain("[FieldOffset(45)] public byte QualityTier;"));
        }

        [Test]
        public void WorldProceduralProxyMaterials_DoNotSerializeDeadQualityKeywords()
        {
            string materialRoot = ResolveProjectPath("Assets", "_Project", "Art", "Materials", "WorldProceduralProxy");
            string[] files = Directory.GetFiles(materialRoot, "*.mat", SearchOption.AllDirectories);

            for (int i = 0; i < files.Length; i++)
            {
                string source = File.ReadAllText(files[i]);
                Assert.That(source, Does.Not.Contain("_QUALITY_MX350"), files[i]);
                Assert.That(source, Does.Not.Contain("_QUALITY_HIGH"), files[i]);
                Assert.That(source, Does.Not.Contain("_MATH_LOD_LOW"), files[i]);
                Assert.That(source, Does.Not.Contain("_MATH_LOD_HIGH"), files[i]);
            }
        }

        [Test]
        public void KccVelocitySignal_UsesContinuousQualityPressureByte()
        {
            string signalSource = ReadProjectFile("Assets", "_Project", "Scripts", "Core", "Signals", "GlobalSignalPayloads.CoreFoundation.cs");
            string somaticSource = ReadProjectFile("Assets", "_Project", "Scripts", "Gameplay", "SomaticKinematicsRuntime.cs");
            string kccSource = ReadProjectFile("Assets", "_Project", "Scripts", "Physics", "KCC", "HydrodynamicKccRuntime.cs");
            string playerSource = ReadProjectFile("Assets", "_Project", "Scripts", "Gameplay", "PlayerKinematicsRuntime.cs");
            string physicsSignalSource = ReadProjectFile("Assets", "_Project", "Scripts", "Physics", "PhysicsDeterminismSignals.cs");

            string signalBody = ExtractTypeBody(signalSource, "public struct KccVelocitySignal");
            Assert.That(signalBody, Does.Contain("[FieldOffset(77)]"));
            Assert.That(signalBody, Does.Contain("public byte QualityPressureQ8;"));
            Assert.That(signalBody, Does.Not.Contain("FlagLowTier"));
            Assert.That(signalBody, Does.Not.Contain("public byte Reserved0;"));

            string somaticBuildBody = ExtractMethodBody(somaticSource, "private void BuildFrameInput(");
            Assert.That(somaticBuildBody, Does.Contain("_frameContext.QualityPressureQ8 = qualityPressureQ8;"));
            Assert.That(somaticBuildBody, Does.Contain("state.Flags &= ~StateFlagQualityPressureReserved;"));
            Assert.That(somaticBuildBody, Does.Not.Contain("qualityPressureQ8 >= 128"));
            Assert.That(somaticBuildBody, Does.Not.Contain("StateFlagLowTier"));

            string somaticPublishBody = ExtractMethodBody(somaticSource, "private void PublishCompletedFrame()");
            Assert.That(somaticPublishBody, Does.Contain("velocitySignal.QualityPressureQ8 = _frameContext.QualityPressureQ8;"));
            Assert.That(somaticPublishBody, Does.Not.Contain("KccVelocitySignal.FlagLowTier"));
            Assert.That(somaticPublishBody, Does.Not.Contain("StateFlagLowTier"));

            string hydrodynamicPublishBody = ExtractMethodBody(kccSource, "private void PublishKccVelocitySnapshot(");
            Assert.That(hydrodynamicPublishBody, Does.Contain("ResolveQualityPressureQ8(_globalQualityWeight)"));
            Assert.That(hydrodynamicPublishBody, Does.Contain("signal.QualityPressureQ8 = qualityPressureQ8;"));
            Assert.That(hydrodynamicPublishBody, Does.Not.Contain("ResolveLowQualitySignalFlag"));
            Assert.That(hydrodynamicPublishBody, Does.Not.Contain("FlagLowTier"));

            string hydrodynamicQualityBody = ExtractMethodBody(kccSource, "private static byte ResolveQualityPressureQ8(");
            Assert.That(hydrodynamicQualityBody, Does.Contain("curvedPressure01"));
            Assert.That(hydrodynamicQualityBody, Does.Contain("* 255f"));
            Assert.That(hydrodynamicQualityBody, Does.Not.Contain("> 0.5f"));
            Assert.That(hydrodynamicQualityBody, Does.Not.Contain("FlagLowTier"));

            string playerPublishBody = ExtractMethodBody(playerSource, "private void PublishKccVelocitySignal(");
            Assert.That(playerPublishBody, Does.Contain("signal.QualityPressureQ8 = ResolveQualityPressureQ8(ReadCachedGlobalQualityWeight01())"));
            Assert.That(playerPublishBody, Does.Not.Contain("KccVelocityReducedQualityCompatibilityFlag"));

            string playerFixedBody = ExtractMethodBody(playerSource, "public void FixedTick(");
            Assert.That(playerFixedBody, Does.Not.Contain("KccVelocityReducedQualityCompatibilityFlag"));
            Assert.That(playerFixedBody, Does.Not.Contain("<= 0.0001f ? 1 : 0"));

            string bridgeBody = ExtractMethodBody(physicsSignalSource, "public static bool TryPublishKccVelocity(");
            Assert.That(bridgeBody, Does.Contain("byte qualityPressureQ8 = 0"));
            Assert.That(bridgeBody, Does.Contain("signal.QualityPressureQ8 = qualityPressureQ8;"));
        }

        [Test]
        public void SomaticKinematics_FixedTickDoesNotHoldMultipleVaultWriteLocks()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Gameplay", "SomaticKinematicsRuntime.cs");

            Assert.That(source, Does.Not.Contain("TryAcquireSimulationWriteBuffers"));
            Assert.That(source, Does.Not.Contain("ReleaseSimulationWriteBuffers"));

            string fixedTickBody = ExtractMethodBody(source, "public void FixedTick(");
            Assert.That(fixedTickBody, Does.Contain("EnsureLocalSimulationScratch()"));
            Assert.That(fixedTickBody, Does.Contain("HydrateLocalSimulationScratchFromVault()"));
            Assert.That(fixedTickBody, Does.Contain("FlushLocalSimulationScratchToVault()"));
            Assert.That(fixedTickBody, Does.Not.Contain("TryAcquire"));

            string awakeBody = ExtractMethodBody(source, "private void Awake()");
            Assert.That(awakeBody, Does.Contain("EnsureLocalSimulationScratch();"));

            string slowTickBody = ExtractMethodBody(source, "public void SlowTick()");
            Assert.That(slowTickBody, Does.Contain("TryApplyCsvOverrides();"));

            string hydrateBody = ExtractMethodBody(source, "private bool HydrateLocalSimulationScratchFromVault()");
            Assert.That(hydrateBody, Does.Not.Contain("TryReadBlackBoxBuffer"));
            Assert.That(hydrateBody, Does.Not.Contain("CopyReadOnlyToLocal(blackBox"));

            string flushBody = ExtractMethodBody(source, "private bool FlushLocalSimulationScratchToVault()");
            Assert.That(flushBody, Does.Contain("TryFlushSignalScratch()"));
            Assert.That(flushBody, Does.Contain("TryFlushStateScratch()"));
            Assert.That(flushBody, Does.Not.Contain("TryAcquire"));

            string[] singleWriterMethods =
            {
                "private bool TryFlushStateScratch()",
                "private bool TryFlushSphereScratch()",
                "private bool TryFlushHandHistoryScratch()",
                "private bool TryFlushSignalScratch()",
                "private bool TryFlushBlackBoxScratch()",
                "private bool TryFlushBlackBoxCursorScratch()",
                "private unsafe bool TryWriteCsvScratchChunk("
            };

            for (int i = 0; i < singleWriterMethods.Length; i++)
            {
                string body = ExtractMethodBody(source, singleWriterMethods[i]);
                Assert.That(CountOccurrences(body, "TryAcquire"), Is.EqualTo(1), singleWriterMethods[i]);
                Assert.That(body, Does.Contain("finally"), singleWriterMethods[i]);
                Assert.That(body, Does.Contain("Release"), singleWriterMethods[i]);
            }

            string applyTuningBody = ExtractMethodBody(source, "private void ApplyTuning(");
            int tuningAcquire = applyTuningBody.IndexOf("TryAcquireTuningWriteBuffer", System.StringComparison.Ordinal);
            int tuningRelease = applyTuningBody.IndexOf("ReleaseTuningWriteBuffer", tuningAcquire, System.StringComparison.Ordinal);
            int dragAcquire = applyTuningBody.IndexOf("TryAcquireDragLutWriteBuffer", tuningRelease, System.StringComparison.Ordinal);
            Assert.That(tuningAcquire, Is.GreaterThanOrEqualTo(0));
            Assert.That(tuningRelease, Is.GreaterThan(tuningAcquire));
            Assert.That(dragAcquire, Is.GreaterThan(tuningRelease));

            string csvApplyBody = ExtractMethodBody(source, "private unsafe bool TryApplyCsvScratchOverrides(");
            int csvScratchAcquire = csvApplyBody.IndexOf("TryAcquireCsvScratchWriteBuffer", System.StringComparison.Ordinal);
            int csvScratchRelease = csvApplyBody.IndexOf("ReleaseCsvScratchWriteBuffer", csvScratchAcquire, System.StringComparison.Ordinal);
            int csvTuningAcquire = csvApplyBody.IndexOf("TryAcquireTuningWriteBuffer", csvScratchRelease, System.StringComparison.Ordinal);
            int csvTuningRelease = csvApplyBody.IndexOf("ReleaseTuningWriteBuffer", csvTuningAcquire, System.StringComparison.Ordinal);
            int csvDragAcquire = csvApplyBody.IndexOf("TryAcquireDragLutWriteBuffer", csvTuningRelease, System.StringComparison.Ordinal);
            Assert.That(csvScratchAcquire, Is.GreaterThanOrEqualTo(0));
            Assert.That(csvScratchRelease, Is.GreaterThan(csvScratchAcquire));
            Assert.That(csvTuningAcquire, Is.GreaterThanOrEqualTo(0));
            Assert.That(csvTuningRelease, Is.GreaterThan(csvTuningAcquire));
            Assert.That(csvDragAcquire, Is.GreaterThan(csvTuningRelease));
        }

        [Test]
        public void KelpContinuousScalarSweep_IsMonotonicFiniteAndPopBounded()
        {
            float previousSway = -1f;
            float previousParallax = -1f;

            for (int i = 0; i <= 16; i++)
            {
                float quality = i * (1f / 16f);
                float motion = Lerp(0.42f, 1f, SmoothRange01(0.05f, 0.85f, quality));
                float survivalScale = Lerp(0.58f, 1f, SmoothRange01(0f, 0.85f, quality));
                float sway = motion * survivalScale;
                float parallax = SmoothRange01(0.55f, 0.95f, quality);

                Assert.IsFalse(float.IsNaN(sway));
                Assert.IsFalse(float.IsInfinity(sway));
                Assert.IsFalse(float.IsNaN(parallax));
                Assert.IsFalse(float.IsInfinity(parallax));
                Assert.That(sway, Is.GreaterThanOrEqualTo(previousSway));
                Assert.That(parallax, Is.GreaterThanOrEqualTo(previousParallax));

                if (i > 0)
                    Assert.That(sway - previousSway, Is.LessThan(0.16f));

                previousSway = sway;
                previousParallax = parallax;
            }
        }

        [Test]
        public void EcosystemDirector_BiomassPendingDrainUsesSingleMutationGuard()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "EcosystemDirector.cs");
            string drainBody = ExtractMethodBody(source, "private void ApplyPendingBiomassImpacts()");
            string clearBody = ExtractMethodBody(source, "private void ClearBiomassRuntimeState()");
            string completeSolveBody = ExtractMethodBody(source, "private void CompleteScheduledSolve(");

            Assert.That(source, Does.Contain("private static readonly ulong BiomassImpactDrainMutationGuardMask"));
            Assert.That(source, Does.Contain("MacroSwarmTravelMutationGuardMask |"));
            Assert.That(source, Does.Contain("EcosystemVaultMutationGuardBit(BufferID.EcosystemPendingBiomassImpacts);"));
            Assert.That(drainBody, Does.Contain("TryAcquireBiomassImpactDrainGuard(out IDataVault drainGuardVault)"));
            Assert.That(drainBody, Does.Contain("_pendingBiomassImpacts.Resolve()"));
            Assert.That(drainBody, Does.Contain("finally"));
            Assert.That(drainBody, Does.Contain("ReleaseBiomassImpactDrainGuard(drainGuardVault)"));
            Assert.That(drainBody, Does.Not.Contain("_pendingBiomassImpacts.TryAcquireWriteLock"));
            Assert.That(drainBody, Does.Not.Contain("_pendingBiomassImpacts.ReleaseWriteLock"));
            Assert.That(clearBody, Does.Contain("TryAcquireBiomassImpactDrainGuard(out IDataVault drainGuardVault)"));
            Assert.That(clearBody, Does.Contain("finally"));
            Assert.That(clearBody, Does.Not.Contain("_pendingBiomassImpacts.TryAcquireWriteLock"));
            Assert.That(clearBody, Does.Not.Contain("_pendingBiomassImpacts.ReleaseWriteLock"));

            int sectorUnlock = completeSolveBody.IndexOf("UnlockSectorSolveJobBuffers();", System.StringComparison.Ordinal);
            int pendingDrain = completeSolveBody.IndexOf("ApplyPendingBiomassImpacts();", System.StringComparison.Ordinal);
            Assert.That(sectorUnlock, Is.GreaterThanOrEqualTo(0));
            Assert.That(pendingDrain, Is.GreaterThan(sectorUnlock));
        }

        [Test]
        public void WorldProceduralScatterDirector_RuntimeCallbacksUseColdRuntimeLatch()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "WorldProceduralScatterDirector.cs");
            string runtimeNowBody = ExtractMethodBody(source, "private static float RuntimeNowSeconds()");
            string ownerBody = ExtractMethodBody(source, "private static bool HasRuntimeScatterOwner()");
            string awakeBody = ExtractMethodBody(source, "private void Awake()");
            string onEnableBody = ExtractMethodBody(source, "private void OnEnable()");
            string onDisableBody = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroyBody = ExtractMethodBody(source, "private void OnDestroy()");
            string prepareReloadBody = ExtractMethodBody(source, "internal void PrepareForEditorReload()");
            string tickBody = ExtractMethodBody(source, "public void Tick(");
            string slowBody = ExtractMethodBody(source, "public void SlowTick()");
            string lateBody = ExtractMethodBody(source, "public void LateFrameTick()");
            string rebuildBody = ExtractMethodBody(source, "public void RebuildScatterPreview()");
            string deferBody = ExtractMethodBody(source, "private bool ShouldDeferUntilBootstrapReady()");
            string skipBody = ExtractMethodBody(source, "private bool ShouldSkipScatterRefresh()");
            string radiusBody = ExtractMethodBody(source, "private int ResolveActiveScatterSamplingRadiusCells(");
            string registerBody = ExtractMethodBody(source, "private void TryEnsureTickRegistration()");

            Assert.That(source, Does.Contain("private bool _runtimeScatterCallbacksActive;"));
            Assert.That(ownerBody, Does.Contain("owner._runtimeScatterCallbacksActive"));
            Assert.That(runtimeNowBody, Does.Contain("HasRuntimeScatterOwner()"));
            Assert.That(runtimeNowBody, Does.Not.Contain("Application.isPlaying"));
            Assert.That(awakeBody, Does.Contain("_runtimeScatterCallbacksActive = Application.isPlaying;"));
            Assert.That(onEnableBody, Does.Contain("_runtimeScatterCallbacksActive = Application.isPlaying;"));
            Assert.That(onDisableBody, Does.Contain("_runtimeScatterCallbacksActive = false;"));
            Assert.That(onDestroyBody, Does.Contain("_runtimeScatterCallbacksActive = false;"));
            Assert.That(prepareReloadBody, Does.Contain("_runtimeScatterCallbacksActive = false;"));
            AssertHotBodyUsesRuntimeLatch(tickBody, "_runtimeScatterCallbacksActive");
            AssertHotBodyUsesRuntimeLatch(slowBody, "_runtimeScatterCallbacksActive");
            AssertHotBodyUsesRuntimeLatch(lateBody, "_runtimeScatterCallbacksActive");
            AssertHotBodyUsesRuntimeLatch(rebuildBody, "_runtimeScatterCallbacksActive");
            AssertHotBodyUsesRuntimeLatch(deferBody, "_runtimeScatterCallbacksActive");
            AssertHotBodyUsesRuntimeLatch(skipBody, "_runtimeScatterCallbacksActive");
            AssertHotBodyUsesRuntimeLatch(radiusBody, "_runtimeScatterCallbacksActive");
            AssertHotBodyUsesRuntimeLatch(registerBody, "_runtimeScatterCallbacksActive");
        }

        [Test]
        public void SuitHudRuntimeCallbacksUseColdRuntimeLatches()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "SuitHUDV4CanvasOverlay.cs");
            string suppressionBody = ExtractMethodBody(source, "private static bool IsStencilRenderGraphSuppressedRuntime()");
            string awakeBody = ExtractMethodBody(source, "private void Awake()");
            string onEnableBody = ExtractMethodBody(source, "private void OnEnable()");
            string onDisableBody = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroyBody = ExtractMethodBody(source, "private void OnDestroy()");
            string slowBody = ExtractMethodBody(source, "public void SlowTick()");
            string lateBody = ExtractMethodBody(source, "public void LateFrameTick()");
            string scalerBody = ExtractTypeBody(source, "public sealed class HectonUIScaler");
            string scalerOnEnableBody = ExtractMethodBody(scalerBody, "private void OnEnable()");
            string scalerOnDisableBody = ExtractMethodBody(scalerBody, "private void OnDisable()");
            string scalerOnDestroyBody = ExtractMethodBody(scalerBody, "private void OnDestroy()");
            string scalerSlowBody = ExtractMethodBody(scalerBody, "public void SlowTick()");
            string scalerRegisterBody = ExtractMethodBody(scalerBody, "private void RegisterToTickManager()");
            string scalerHotSwapBody = ExtractMethodBody(scalerBody, "private void TryRegisterHotSwapListener()");

            Assert.That(source, Does.Contain("private bool _runtimeHudCallbacksActive;"));
            Assert.That(source, Does.Contain("private bool _runtimeScalerCallbacksActive;"));
            Assert.That(suppressionBody, Does.Contain("SystemDispatcher.ActiveRuntimeInstance != null"));
            Assert.That(suppressionBody, Does.Not.Contain("Application.isPlaying"));
            Assert.That(awakeBody, Does.Contain("_runtimeHudCallbacksActive = Application.isPlaying;"));
            Assert.That(onEnableBody, Does.Contain("_runtimeHudCallbacksActive = Application.isPlaying;"));
            Assert.That(onDisableBody, Does.Contain("_runtimeHudCallbacksActive = false;"));
            Assert.That(onDestroyBody, Does.Contain("_runtimeHudCallbacksActive = false;"));
            AssertHotBodyUsesRuntimeLatch(slowBody, "_runtimeHudCallbacksActive");
            AssertHotBodyUsesRuntimeLatch(lateBody, "_runtimeHudCallbacksActive");
            Assert.That(scalerOnEnableBody, Does.Contain("_runtimeScalerCallbacksActive = Application.isPlaying;"));
            Assert.That(scalerOnDisableBody, Does.Contain("_runtimeScalerCallbacksActive = false;"));
            Assert.That(scalerOnDestroyBody, Does.Contain("_runtimeScalerCallbacksActive = false;"));
            AssertHotBodyUsesRuntimeLatch(scalerSlowBody, "_runtimeScalerCallbacksActive");
            AssertHotBodyUsesRuntimeLatch(scalerRegisterBody, "_runtimeScalerCallbacksActive");
            AssertHotBodyUsesRuntimeLatch(scalerHotSwapBody, "_runtimeScalerCallbacksActive");
        }

        [Test]
        public void HectonUnderwaterVisuals_HotCallbacksUseColdRuntimeLatch()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "HectonUnderwaterVisuals.cs");
            string awakeBody = ExtractMethodBody(source, "private void Awake()");
            string onEnableBody = ExtractMethodBody(source, "private void OnEnable()");
            string startBody = ExtractMethodBody(source, "private void Start()");
            string onDisableBody = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroyBody = ExtractMethodBody(source, "private void OnDestroy()");
            string coldBody = ExtractMethodBody(source, "public void ColdTick()");
            string slowBody = ExtractMethodBody(source, "public void SlowTick()");
            string renderBody = ExtractMethodBody(source, "public void Render(");
            string lateBody = ExtractMethodBody(source, "public void LateFrameTick()");
            string runVisualBody = ExtractMethodBody(source, "private void RunUnderwaterVisualTick(");
            string enforceFogBody = ExtractMethodBody(source, "private void EnforceFogState(");
            string shouldRenderBody = ExtractMethodBody(source, "private bool ShouldRenderUnderwaterFogForCamera(");
            string stackInitBody = ExtractMethodBody(source, "private void EnsureGameplayCameraStackInitializedOnTick()");
            string biomeBlendBody = ExtractMethodBody(source, "private void ApplyBiomeFogBlend(");
            string ownerResolveBody = ExtractMethodBody(source, "private void RequestRuntimeVisualOwnerResolveIfMissing()");
            string thermoclineBody = ExtractMethodBody(source, "private void TryHandleThermoclineTransition(");
            string motesBody = ExtractMethodBody(source, "private void UpdateUnderwaterSuspendedMotes(");
            string skyboxBody = ExtractMethodBody(source, "private void ApplyRuntimeSkyboxOwnership()");
            string noirBody = ExtractMethodBody(source, "private void ApplyNoirResolveGlobals()");
            string photophobiaBody = ExtractMethodBody(source, "private void UpdateFlashlightPhotophobiaField(");
            string healthGlitchBody = ExtractMethodBody(source, "private static void UpdateSuitHealthGlitchGlobal(");
            string spaceDepthBody = ExtractMethodBody(source, "private void ApplySpaceCameraDepthState(");
            string depthBody = ExtractMethodBody(source, "private float ResolveCurrentDepth()");
            string stateBody = ExtractMethodBody(source, "private bool ResolveUnderwaterVisualStateForCameraDepth(");
            string visualDepthBody = ExtractMethodBody(source, "private float ResolveActiveVisualCameraDepth()");
            string oceanGlobalsBody = ExtractMethodBody(source, "private static void ApplyOceanUnderwaterGlobals(");
            string tickRegisterBody = ExtractMethodBody(source, "private void TryRegisterTickManagers()");
            string renderRegisterBody = ExtractMethodBody(source, "private void TryRegisterRenderDispatcher()");

            Assert.That(source, Does.Contain("private bool _runtimeVisualCallbacksActive;"));
            Assert.That(awakeBody, Does.Contain("_runtimeVisualCallbacksActive = Application.isPlaying;"));
            Assert.That(onEnableBody, Does.Contain("_runtimeVisualCallbacksActive = Application.isPlaying;"));
            Assert.That(startBody, Does.Contain("_runtimeVisualCallbacksActive = true;"));
            Assert.That(onDisableBody, Does.Contain("_runtimeVisualCallbacksActive = false;"));
            Assert.That(onDestroyBody, Does.Contain("_runtimeVisualCallbacksActive = false;"));
            AssertHotBodyUsesRuntimeLatch(coldBody, "_runtimeVisualCallbacksActive");
            AssertHotBodyUsesRuntimeLatch(slowBody, "_runtimeVisualCallbacksActive");
            Assert.That(renderBody, Does.Not.Contain("Application.isPlaying"));
            Assert.That(lateBody, Does.Not.Contain("Application.isPlaying"));
            Assert.That(runVisualBody, Does.Not.Contain("Application.isPlaying"));
            AssertHotBodyUsesRuntimeLatch(enforceFogBody, "_runtimeVisualCallbacksActive");
            AssertHotBodyUsesRuntimeLatch(shouldRenderBody, "_runtimeVisualCallbacksActive");
            AssertHotBodyUsesRuntimeLatch(stackInitBody, "_runtimeVisualCallbacksActive");
            AssertHotBodyUsesRuntimeLatch(biomeBlendBody, "_runtimeVisualCallbacksActive");
            AssertHotBodyUsesRuntimeLatch(ownerResolveBody, "_runtimeVisualCallbacksActive");
            AssertHotBodyUsesRuntimeLatch(thermoclineBody, "_runtimeVisualCallbacksActive");
            AssertHotBodyUsesRuntimeLatch(motesBody, "_runtimeVisualCallbacksActive");
            AssertHotBodyUsesRuntimeLatch(skyboxBody, "_runtimeVisualCallbacksActive");
            AssertHotBodyUsesRuntimeLatch(noirBody, "_runtimeVisualCallbacksActive");
            AssertHotBodyUsesRuntimeLatch(photophobiaBody, "_runtimeVisualCallbacksActive");
            AssertHotBodyUsesRuntimeLatch(healthGlitchBody, "runtimeCallbacksActive");
            AssertHotBodyUsesRuntimeLatch(spaceDepthBody, "_runtimeVisualCallbacksActive");
            AssertHotBodyUsesRuntimeLatch(depthBody, "_runtimeVisualCallbacksActive");
            AssertHotBodyUsesRuntimeLatch(stateBody, "_runtimeVisualCallbacksActive");
            AssertHotBodyUsesRuntimeLatch(visualDepthBody, "_runtimeVisualCallbacksActive");
            AssertHotBodyUsesRuntimeLatch(oceanGlobalsBody, "runtimeCallbacksActive");
            AssertHotBodyUsesRuntimeLatch(tickRegisterBody, "_runtimeVisualCallbacksActive");
            AssertHotBodyUsesRuntimeLatch(renderRegisterBody, "_runtimeVisualCallbacksActive");
        }

        [Test]
        public void AmbientWaterMotionManager_HotRegistrationUsesColdRuntimeLatch()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "AmbientWaterMotionManager.cs");
            string awakeBody = ExtractMethodBody(source, "private void Awake()");
            string onEnableBody = ExtractMethodBody(source, "private void OnEnable()");
            string onDisableBody = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroyBody = ExtractMethodBody(source, "private void OnDestroy()");
            string tickBody = ExtractMethodBody(source, "public void Tick(");
            string lateBody = ExtractMethodBody(source, "public void LateFrameTick()");
            string registerBody = ExtractMethodBody(source, "private void TryRegister()");
            string registerLateBody = ExtractMethodBody(source, "private void TryRegisterLateFrame()");
            string serviceBody = ExtractMethodBody(source, "private void TryRegisterService()");
            string hotSwapBody = ExtractMethodBody(source, "private void TryRegisterHotSwapListener()");

            Assert.That(source, Does.Contain("private bool _runtimeWaterMotionCallbacksActive;"));
            Assert.That(awakeBody, Does.Contain("_runtimeWaterMotionCallbacksActive = Application.isPlaying;"));
            Assert.That(onEnableBody, Does.Contain("_runtimeWaterMotionCallbacksActive = Application.isPlaying;"));
            Assert.That(onEnableBody, Does.Contain("if (_runtimeWaterMotionCallbacksActive)"));
            Assert.That(onDisableBody, Does.Contain("_runtimeWaterMotionCallbacksActive = false;"));
            Assert.That(onDestroyBody, Does.Contain("_runtimeWaterMotionCallbacksActive = false;"));
            Assert.That(tickBody, Does.Contain("TryRegisterLateFrame()"));
            Assert.That(tickBody, Does.Not.Contain("Application.isPlaying"));
            Assert.That(lateBody, Does.Not.Contain("Application.isPlaying"));
            AssertHotBodyUsesRuntimeLatch(registerBody, "_runtimeWaterMotionCallbacksActive");
            AssertHotBodyUsesRuntimeLatch(registerLateBody, "_runtimeWaterMotionCallbacksActive");
            AssertHotBodyUsesRuntimeLatch(serviceBody, "_runtimeWaterMotionCallbacksActive");
            AssertHotBodyUsesRuntimeLatch(hotSwapBody, "_runtimeWaterMotionCallbacksActive");
        }

        private static string ReadShader(string shaderFile)
        {
            return ReadProjectFile("Assets", "_Project", "Art", "Shaders", shaderFile);
        }

        private static string ReadProjectFile(params string[] pathParts)
        {
            string filePath = ResolveProjectPath(pathParts);
            return File.ReadAllText(filePath);
        }

        private static string ResolveProjectPath(params string[] pathParts)
        {
            string filePath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            for (int i = 0; i < pathParts.Length; i++)
                filePath = Path.Combine(filePath, pathParts[i]);

            return filePath;
        }

        private static float SmoothRange01(float start, float end, float value)
        {
            float denom = Mathf.Max(end - start, 0.0001f);
            return Mathf.Clamp01((value - start) / denom);
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * Mathf.Clamp01(t);
        }

        private static int CountOccurrences(string source, string value)
        {
            int count = 0;
            int index = 0;
            while ((index = source.IndexOf(value, index, System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }

            return count;
        }

        private static void AssertLockReleaseFinally(string sourceBody, string releaseCall)
        {
            int releaseIndex = sourceBody.IndexOf(releaseCall, System.StringComparison.Ordinal);
            Assert.That(releaseIndex, Is.GreaterThanOrEqualTo(0), releaseCall);
            int finallyIndex = sourceBody.LastIndexOf("finally", releaseIndex, System.StringComparison.Ordinal);
            Assert.That(finallyIndex, Is.GreaterThanOrEqualTo(0), releaseCall);
            int tryIndex = sourceBody.LastIndexOf("try", finallyIndex, System.StringComparison.Ordinal);
            Assert.That(tryIndex, Is.GreaterThanOrEqualTo(0), releaseCall);
            Assert.That(releaseIndex, Is.GreaterThan(finallyIndex), releaseCall);
        }

        private static void AssertVisorRenderRouteUsesRuntimeOwnerGate(string source)
        {
            string addBody = ExtractMethodBody(source, "public override void AddRenderPasses(");
            string recordBody = ExtractMethodBody(source, "public override void RecordRenderGraph(");

            Assert.That(addBody, Does.Contain("HectonDrsRenderFeatureGate.HasRuntimeRenderOwner()"));
            Assert.That(recordBody, Does.Contain("HectonDrsRenderFeatureGate.HasRuntimeRenderOwner()"));
            Assert.That(addBody, Does.Not.Contain("Application.isPlaying"));
            Assert.That(recordBody, Does.Not.Contain("Application.isPlaying"));
            Assert.That(addBody, Does.Not.Contain("GlobalRegistry.Get<"));
            Assert.That(recordBody, Does.Not.Contain("GlobalRegistry.Get<"));
            Assert.That(addBody, Does.Not.Contain("GetComponent"));
            Assert.That(recordBody, Does.Not.Contain("GetComponent"));
        }

        private static void AssertHotBodyUsesRuntimeLatch(string body, string latch)
        {
            Assert.That(body, Does.Contain(latch));
            Assert.That(body, Does.Not.Contain("Application.isPlaying"));
            Assert.That(body, Does.Not.Contain("GlobalRegistry.Get<"));
            Assert.That(body, Does.Not.Contain("GetComponent"));
            Assert.That(body, Does.Not.Contain("TryGetComponent"));
        }

        private static string ExtractMethodBody(string source, string methodToken)
        {
            int start = source.IndexOf(methodToken, System.StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), methodToken);
            int openBrace = source.IndexOf('{', start);
            Assert.That(openBrace, Is.GreaterThan(start), methodToken);
            return ExtractBalancedBody(source, openBrace, methodToken);
        }

        private static string ExtractTypeBody(string source, string typeToken)
        {
            int start = source.IndexOf(typeToken, System.StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), typeToken);
            int openBrace = source.IndexOf('{', start);
            Assert.That(openBrace, Is.GreaterThan(start), typeToken);
            return ExtractBalancedBody(source, openBrace, typeToken);
        }

        private static string ExtractBalancedBody(string source, int openBrace, string context)
        {
            int depth = 0;
            for (int i = openBrace; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(openBrace, i - openBrace + 1);
                }
            }

            Assert.Fail("Unbalanced body: " + context);
            return string.Empty;
        }

        private static void AssertCursorBeforeRing(string source, string methodToken, string cursorHandle, string ringHandle)
        {
            int methodIndex = source.IndexOf(methodToken, System.StringComparison.Ordinal);
            Assert.That(methodIndex, Is.GreaterThanOrEqualTo(0), methodToken);
            int cursorAcquire = source.IndexOf("TryAcquireWriteLock(in " + cursorHandle, methodIndex, System.StringComparison.Ordinal);
            int cursorRelease = source.IndexOf("ReleaseWriteLock(in " + cursorHandle, cursorAcquire, System.StringComparison.Ordinal);
            int ringAcquire = source.IndexOf("TryAcquireWriteLock(in " + ringHandle, cursorRelease, System.StringComparison.Ordinal);
            Assert.That(cursorAcquire, Is.GreaterThanOrEqualTo(0), methodToken);
            Assert.That(cursorRelease, Is.GreaterThan(cursorAcquire), methodToken);
            Assert.That(ringAcquire, Is.GreaterThan(cursorRelease), methodToken);
        }
    }
}
