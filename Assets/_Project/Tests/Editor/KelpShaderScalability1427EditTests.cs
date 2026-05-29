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
        public void TetherVisuals_BlendDearLieLineContinuously()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "TetherInstance.cs");

            Assert.That(source, Does.Contain("ResolveTautLineVisualCurveWeight"));
            Assert.That(source, Does.Contain("math.lerp(straightPoint, _verletPositions[i], curveWeight01)"));
            Assert.That(source, Does.Contain("const float qualityTierRange"));
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
            Assert.That(source, Does.Contain("ResolveUpscalerHash(float qualityWeight01"));
            Assert.That(source, Does.Contain("UpdateVisualBudget(float qualityWeight01"));
            Assert.That(source, Does.Contain("ResolveFsrUpscalerAllowed(float qualityWeight01)"));
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
            Assert.That(source, Does.Not.Contain("UpdateVisualBudget(HectonQualityTier"));
            Assert.That(source, Does.Not.Contain("quality >= 0.86f"));
            Assert.That(source, Does.Not.Contain("quality < 0.18f"));
            Assert.That(source, Does.Not.Contain("quality < 0.36f"));
            Assert.That(source, Does.Not.Contain("quality < 0.62f"));
            Assert.That(source, Does.Not.Contain("quality < 0.86f"));
            Assert.That(source, Does.Not.Contain("graphicsMemoryMb >= 3000"));
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

            Assert.That(contracts, Does.Contain("SurvivalPressureEmergency = 1UL << 24"));
            Assert.That(contracts, Does.Contain("LowTierEmergency = SurvivalPressureEmergency"));
            Assert.That(dictator, Does.Contain("SystemBit.SurvivalPressureEmergency"));
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
        public void WorldSampler_SurvivalSamplingPressureReplacesActiveMathLodLowFlag()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "GlobalWorldSampler.cs");
            string sampleBody = ExtractMethodBody(source, "public static void SampleDistanceOnly(");
            string normalBody = ExtractMethodBody(source, "public static void EstimateNormal(");
            string flagBody = ExtractMethodBody(source, "private static byte ResolveSurvivalSamplingPressureFlag(");

            Assert.That(source, Does.Contain("ForceSurvivalSamplingPressure = 1 << 0"));
            Assert.That(source, Does.Contain("ForceMathLodLow = ForceSurvivalSamplingPressure"));
            Assert.That(source, Does.Contain("SurvivalSamplingPressure = 1 << 3"));
            Assert.That(source, Does.Contain("MathLodLow = SurvivalSamplingPressure"));
            Assert.That(sampleBody, Does.Contain("ResolveSurvivalSamplingPressureFlag(qualityWeight)"));
            Assert.That(normalBody, Does.Contain("GlobalWorldSamplerResultFlags.SurvivalSamplingPressure"));
            Assert.That(flagBody, Does.Contain("ResolveExpensiveSamplingWeight(qualityWeight) <= 0.0001f"));
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
            int audioCursorAcquire = audioRecord.IndexOf("TryAcquireWriteLock(in _telemetryCursorHandle", System.StringComparison.Ordinal);
            int audioCursorRelease = audioRecord.IndexOf("ReleaseWriteLock(in _telemetryCursorHandle", System.StringComparison.Ordinal);
            int audioRingAcquire = audioRecord.IndexOf("TryAcquireWriteLock(in _telemetryRingHandle", System.StringComparison.Ordinal);
            Assert.That(audioCursorAcquire, Is.GreaterThanOrEqualTo(0));
            Assert.That(audioCursorRelease, Is.GreaterThan(audioCursorAcquire));
            Assert.That(audioRingAcquire, Is.GreaterThan(audioCursorRelease));

            Assert.That(audio, Does.Not.Contain("TryAcquireEncryptedFragmentState"));
            Assert.That(audio, Does.Not.Contain("ReleaseEncryptedFragmentWriteLocks"));
            int helperIndex = audio.IndexOf("private bool TryAcquireVaultWrite", System.StringComparison.Ordinal);
            int helperFinally = helperIndex >= 0
                ? audio.IndexOf("finally", helperIndex, System.StringComparison.Ordinal)
                : -1;
            int helperRelease = helperFinally >= 0
                ? audio.IndexOf("vault.ReleaseWriteLock(in handle, OwnerSystemId);", helperFinally, System.StringComparison.Ordinal)
                : -1;
            Assert.That(helperIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(helperFinally, Is.GreaterThan(helperIndex));
            Assert.That(helperRelease, Is.GreaterThan(helperFinally));

            int fragmentIndex = audio.IndexOf("private bool SetEncryptedFragmentBits", System.StringComparison.Ordinal);
            Assert.That(fragmentIndex, Is.GreaterThanOrEqualTo(0));
            int fragmentHashAcquire = audio.IndexOf("TryAcquireVaultWrite(in _encryptedFragmentLogHashesHandle", fragmentIndex, System.StringComparison.Ordinal);
            int fragmentHashRelease = fragmentHashAcquire >= 0
                ? audio.IndexOf("ReleaseVaultWrite(in _encryptedFragmentLogHashesHandle", fragmentHashAcquire, System.StringComparison.Ordinal)
                : -1;
            int fragmentBitsAcquire = fragmentHashRelease >= 0
                ? audio.IndexOf("TryAcquireVaultWrite(in _encryptedFragmentRecoveredBitsHandle", fragmentHashRelease, System.StringComparison.Ordinal)
                : -1;
            Assert.That(fragmentHashAcquire, Is.GreaterThanOrEqualTo(0));
            Assert.That(fragmentHashRelease, Is.GreaterThan(fragmentHashAcquire));
            Assert.That(fragmentBitsAcquire, Is.GreaterThan(fragmentHashRelease));
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
        public void ShinobuApexBrain_UsesSurvivalNodeBudgetPressureFlag()
        {
            string contracts = ReadProjectFile("Assets", "_Project", "Scripts", "AI", "Cognition", "ShinobuApexBrainContracts.cs");
            string jobs = ReadProjectFile("Assets", "_Project", "Scripts", "AI", "Cognition", "ShinobuApexBrainJobs.cs");
            string jobBody = ExtractTypeBody(jobs, "public struct ApexBrainJob");

            Assert.That(contracts, Does.Contain("public const byte SurvivalNodeBudgetPressure = 1 << 4;"));
            Assert.That(contracts, Does.Contain("public const byte ReducedQualityNodeBudget = SurvivalNodeBudgetPressure;"));
            Assert.That(jobBody, Does.Contain("ResolveSurvivalNodeBudgetPressureFlag(quality)"));
            Assert.That(jobBody, Does.Contain("ApexBrainFlags.SurvivalNodeBudgetPressure"));
            Assert.That(jobBody, Does.Not.Contain("ResolveReducedQualityNodeBudgetFlag"));
            Assert.That(jobBody, Does.Not.Contain("ApexBrainFlags.ReducedQualityNodeBudget"));
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
            Assert.That(contracts, Does.Contain("SurvivalProxySurface = 1"));
            Assert.That(bridge, Does.Contain("ResolveSurvivalProxyPressureWithHysteresis"));
            Assert.That(bridge, Does.Contain("ReadSurvivalProxyPressurePolicy"));
            Assert.That(director, Does.Contain("_runtime.SurvivalProxyPressure01"));
            Assert.That(director, Does.Contain("PrologueSequenceQualityPolicy.SurvivalProxyActivationThreshold01"));
            Assert.That(director, Does.Not.Contain("_runtime.IsLowTier"));
            Assert.That(director, Does.Not.Contain("LowTierProxySurface"));
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
            Assert.That(thermal, Does.Contain("_coldFsrCapabilityAllowed = !Application.isMobilePlatform && SystemInfo.supportsComputeShaders;"));
            Assert.That(thermal, Does.Not.Contain("private static bool ResolveFsrUpscalerAllowed"));
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
            Assert.That(source, Does.Not.Contain("SystemInfo.graphicsMemorySize <= 2048"));
            Assert.That(source, Does.Not.Contain("SystemInfo.graphicsMemorySize > 4096 ?"));
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
