#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Audio.Editor
{
    /// <summary>
    /// Editor-only source smoke test for advanced acoustic propagation and DSP producer features.
    /// </summary>
    public static class AdvancedAcousticsSmokeTester
    {
        private const string RendererPath = "Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs";
        private const string SpatialAudioPath = "Assets/_Project/Scripts/SpatialAudioManager.cs";
        private const string PhysicsApplyPath = "Assets/_Project/Scripts/PhysicsApplySystem.cs";
        private const string SpectrumSystemPath = "Assets/_Project/Scripts/Visor/SpectrumSystem.cs";
        private const string ResourceNodePath = "Assets/_Project/Scripts/ResourceNode.cs";
        private const string ResourceNodeTemplatePath = "Assets/_Project/Scripts/Scavenging/ResourceNodeTemplate.cs";
        private const string SonarGridOverlayPath = "Assets/_Project/Art/Shaders/SonarGridOverlay.shader";
        private const string SonarPointCloudFeaturePath = "Assets/_Project/Scripts/Visor/HectonSonarPointCloudFeature.cs";
        private const string SuitVisorPath = "Assets/_Project/Art/Shaders/SuitVisor.shader";
        private const string LeviathanOrganicPath = "Assets/_Project/Art/Shaders/Hecton_LeviathanOrganic.shader";
        private const string ToolHapticsPath = "Assets/_Project/Scripts/Tools/ToolHapticsRuntime.cs";
        private const string FakeRadarPath = "Assets/_Project/Scripts/UI/FakeRadarBlipController.cs";
        private const string EcholocationTranslatorPath = "Assets/_Project/Scripts/UI/AcousticEcholocationTranslator.cs";
        private const string GlobalRegistryPath = "Assets/_Project/Scripts/Core/GlobalRegistry.cs";
        private const string GlobalRegistryContractsPath = "Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs";
        private const string OcclusionPath = "Assets/_Project/Scripts/World/AcousticOcclusionUtility.cs";
        private const string RingBufferPath = "Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs";
        private const string SynthesisPath = "Assets/_Project/Scripts/Audio/Synthesis/DepthStressGranularSynthesisKernel.cs";
        private const string TelemetryPath = "Assets/_Project/Scripts/CrashTelemetryBuffer.cs";
        private const string EventsPath = "Assets/_Project/Scripts/Audio/ProceduralAudioEvents.cs";
        private const string AcousticZonePath = "Assets/_Project/Scripts/AcousticZoneController.cs";
        private const string AudioLogEventsPath = "Assets/_Project/Scripts/AudioLog/AudioLogEvents.cs";
        private const string PlayerPdaPath = "Assets/_Project/Scripts/PlayerPDA.cs";
        private const string PlayerStressVfxPath = "Assets/_Project/Scripts/Visor/PlayerStressVFX.cs";
        private const string DeepPsychosisPath = "Assets/_Project/Scripts/Audio/DeepPsychosisController.cs";
        private const string HectonMusicDirectorPath = "Assets/_Project/Scripts/Audio/HectonMusicDirector.cs";

        [MenuItem("Hecton8/Audio/Run Advanced Acoustics Smoke Test")]
        public static void RunMenuItem()
        {
            bool passed = Run(out string report);
            if (passed)
                Debug.Log(report);
            else
                Debug.LogError(report);
        }

        public static bool Run(out string report)
        {
            int failureCount = 0;
            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine("[AdvancedAcousticsSmokeTester]");

            string renderer = ReadAssetText(RendererPath, builder, ref failureCount);
            string spatial = ReadAssetText(SpatialAudioPath, builder, ref failureCount);
            string physicsApply = ReadAssetText(PhysicsApplyPath, builder, ref failureCount);
            string spectrumSystem = ReadAssetText(SpectrumSystemPath, builder, ref failureCount);
            string resourceNode = ReadAssetText(ResourceNodePath, builder, ref failureCount);
            string resourceNodeTemplate = ReadAssetText(ResourceNodeTemplatePath, builder, ref failureCount);
            string sonarGridOverlay = ReadAssetText(SonarGridOverlayPath, builder, ref failureCount);
            string sonarPointCloudFeature = ReadAssetText(SonarPointCloudFeaturePath, builder, ref failureCount);
            string suitVisor = ReadAssetText(SuitVisorPath, builder, ref failureCount);
            string leviathanOrganic = ReadAssetText(LeviathanOrganicPath, builder, ref failureCount);
            string toolHaptics = ReadAssetText(ToolHapticsPath, builder, ref failureCount);
            string fakeRadar = ReadAssetText(FakeRadarPath, builder, ref failureCount);
            string echolocationTranslator = ReadAssetText(EcholocationTranslatorPath, builder, ref failureCount);
            string globalRegistry = ReadAssetText(GlobalRegistryPath, builder, ref failureCount);
            string globalRegistryContracts = ReadAssetText(GlobalRegistryContractsPath, builder, ref failureCount);
            string occlusion = ReadAssetText(OcclusionPath, builder, ref failureCount);
            string ringBuffer = ReadAssetText(RingBufferPath, builder, ref failureCount);
            string synthesis = ReadAssetText(SynthesisPath, builder, ref failureCount);
            string telemetry = ReadAssetText(TelemetryPath, builder, ref failureCount);
            string eventsSource = ReadAssetText(EventsPath, builder, ref failureCount);
            string acousticZone = ReadAssetText(AcousticZonePath, builder, ref failureCount);
            string audioLogEvents = ReadAssetText(AudioLogEventsPath, builder, ref failureCount);
            string playerPda = ReadAssetText(PlayerPdaPath, builder, ref failureCount);
            string playerStressVfx = ReadAssetText(PlayerStressVfxPath, builder, ref failureCount);
            string deepPsychosis = ReadAssetText(DeepPsychosisPath, builder, ref failureCount);
            string musicDirector = ReadAssetText(HectonMusicDirectorPath, builder, ref failureCount);

            if (spatial.Length > 0)
            {
                string spatialPortalPath = ExtractMethodBody(spatial, "private bool TryResolveAcousticPortalPath(");
                string spatialUsePortalPath = ExtractMethodBody(spatial, "private bool ShouldUseAcousticPortalPath()");
                string spatialVoiceLimit = ExtractMethodBody(spatial, "private void RefreshVirtualPhysicalVoiceLimit(bool immediate)");
                string spatialListenerAup = ExtractMethodBody(spatial, "private bool TryResolvePlayerListenerAup(");
                string spatialWindTarget = ExtractMethodBody(spatial, "private float ResolveGlobalWindHowlTarget01()");
                string spatialWindOcclusion = ExtractMethodBody(spatial, "private bool ResolveGlobalWindHowlOccluded()");
                string spatialWaterDensity = ExtractMethodBody(spatial, "private void UpdateListenerWaterDensityMul(float deltaTime)");
                AssertContains(spatial, "TryResolveCinematicZoneMismatch", "Delayed world events apply deterministic zone muffle", builder, ref failureCount);
                AssertContains(spatial, "CinematicZoneMuffleTransmission = 0.25118864f", "Cinematic zone muffle applies -12 dB transmission", builder, ref failureCount);
                AssertContains(spatial, "CinematicZoneMuffleCutoffHertz = 800f", "Cinematic zone muffle applies 800 Hz LPF", builder, ref failureCount);
                AssertContains(spatial, "IsInsideActiveBaseInteriorAup", "Base interior muffle uses deterministic AUP bounds", builder, ref failureCount);
                AssertContains(spatial, "PlayAtPointWithLowPass", "Delayed events route resolved low-pass cutoff into source filter", builder, ref failureCount);
                AssertContains(spatial, "ThermalShimmerMaximumPitchRatio", "Thermal plume shimmer pitch modulation exists", builder, ref failureCount);
                AssertContains(spatial, "RefreshListenerCaveState", "Listener cave state refresh exists", builder, ref failureCount);
                AssertContains(spatial, "HectonVoxelVolume", "Cave state uses authored voxel-volume records", builder, ref failureCount);
                AssertContains(spatial, "localBounds.Contains", "Cave interior check is a local AABB contains test", builder, ref failureCount);
                AssertContains(spatial, "IsListenerInsideCaveVolume", "Spatial manager exposes listener cave membership for fake reverb", builder, ref failureCount);
                AssertContains(spatial, "IPhysicsAcousticImpulseEventListener", "Spatial manager receives acoustic impulses", builder, ref failureCount);
                AssertContains(spatial, "PhysicsEventBus.Register(this)", "Spatial manager subscribes to sensory bus", builder, ref failureCount);
                AssertContains(spatial, "math.dot((float3)listener.right, sourceDirection)", "Binaural ITD uses one ear-axis dot product", builder, ref failureCount);
                AssertContains(spatial, "TryQueueImpactRadarEmitter(impulseEvent.RuntimePosition", "Acoustic impulses feed passive HUD emitters", builder, ref failureCount);
                AssertContains(spatial, "ResolveAupDelta", "Long-range spatial audio direction uses AUP delta helpers", builder, ref failureCount);
                AssertContains(spatial, "AbsoluteUniversePosition.DistanceSq(in listenerAup, in sourceAup)", "Spatial audio distance uses int64-sector AUP distance math", builder, ref failureCount);
                AssertContains(spatial, "AbsoluteUniversePosition.ToCameraRelativeFloat3(in sourceAup, in listenerAup)", "Doppler/radar direction uses AUP camera-relative math", builder, ref failureCount);
                AssertContains(spatial, "SpatialAudioPolicyRefreshFrames = 30", "Spatial audio quality policy is cadence-gated", builder, ref failureCount);
                AssertContains(spatial, "SpatialAudioRegistryRetryFrames = 30", "Spatial audio optional service lookup is cadence-gated", builder, ref failureCount);
                AssertContains(spatial, "ResolveCachedScalabilityTier()", "Spatial audio portal and virtualization policy use cached scalability tier", builder, ref failureCount);
                AssertContains(spatial, "ResolvePlayerRuntimeContext()", "Spatial audio listener AUP and water-density state use cached player context", builder, ref failureCount);
                AssertContains(spatial, "ResolveAcousticZone()", "Spatial audio interior checks use cached acoustic-zone resolution", builder, ref failureCount);
                AssertContains(spatial, "ResolveWeatherService()", "Spatial audio wind howl uses cached weather service resolution", builder, ref failureCount);
                AssertContains(spatial, "ResolveSurfaceWeatherDirector()", "Spatial audio surface weather uses cached director resolution", builder, ref failureCount);
                AssertNotContains(spatialPortalPath, "GlobalRegistry.ScalabilityTier", "Spatial audio portal path does not poll scalability registry directly", builder, ref failureCount);
                AssertNotContains(spatialUsePortalPath, "GlobalRegistry.ScalabilityTier", "Spatial audio portal policy does not poll scalability registry directly", builder, ref failureCount);
                AssertNotContains(spatialVoiceLimit, "GlobalRegistry.ScalabilityTier", "Spatial audio voice-limit policy does not poll scalability registry directly", builder, ref failureCount);
                AssertNotContains(spatialVoiceLimit, "GlobalRegistry.H8_LOW_MEMORY_PROFILE", "Spatial audio voice-limit policy does not poll low-memory registry directly", builder, ref failureCount);
                AssertNotContains(spatialListenerAup, "GlobalRegistry.Player", "Spatial audio listener AUP does not poll player registry directly", builder, ref failureCount);
                AssertNotContains(spatialWaterDensity, "GlobalRegistry.Player", "Spatial audio water-density update does not poll player registry directly", builder, ref failureCount);
                AssertNotContains(spatialWindTarget, "GlobalRegistry.Weather", "Spatial audio wind target does not poll weather registry directly", builder, ref failureCount);
                AssertNotContains(spatialWindTarget, "GlobalRegistry.SurfaceWeather", "Spatial audio wind target does not poll surface-weather registry directly", builder, ref failureCount);
                AssertNotContains(spatialWindOcclusion, "GlobalRegistry.AcousticZone", "Spatial audio wind occlusion does not poll acoustic-zone registry directly", builder, ref failureCount);
            }

            if (occlusion.Length > 0)
            {
                AssertNotContains(occlusion, "Acoustic" + "CinematicOcclusionResult", "Stale cinematic voxel occlusion payload is absent", builder, ref failureCount);
                AssertNotContains(occlusion, "TryResolve" + "CinematicVoxelOcclusion", "Stale cinematic voxel occlusion API is absent", builder, ref failureCount);
                AssertNotContains(occlusion, "RaycastNonAlloc", "Acoustic occlusion utility has no synchronous physics query", builder, ref failureCount);
            }

            if (renderer.Length > 0)
            {
                string updateCaveReverb = ExtractMethodBody(renderer, "private void UpdateCaveReverb(float deltaTime)");
                string handleSonarPingSent = ExtractMethodBody(renderer, "private void HandleSonarPingSent(float intensity)");
                string renderBubbleBlock = ExtractMethodBody(renderer, "private void RenderBubbleBlock(");
                string renderTinnitusSample = ExtractMethodBody(renderer, "private static float RenderTinnitusSample(");
                string renderHullStressBlock = ExtractMethodBody(renderer, "private void RenderHullStressBlock(");
                string renderSonarBlock = ExtractMethodBody(renderer, "private void RenderSonarBlock(int frameCount, long blockStartFrame, double invSampleRate)");
                AssertContains(renderer, "RenderLeviathanGranularRoarSample", "Leviathan granular synthesis kernel exists", builder, ref failureCount);
                AssertContains(renderer, "NativeArray<float> baseRoarClip", "Granular kernel consumes native base roar data", builder, ref failureCount);
                AssertContains(renderer, "LeviathanRoarAggro", "Aggro is synchronized through audio parameter snapshot", builder, ref failureCount);
                AssertContains(renderer, "LeviathanRoarPitchScale", "Leviathan roar pitch is driven by Doppler snapshot state", builder, ref failureCount);
                AssertContains(renderer, "ResolveLeviathanDopplerPitchScale", "Leviathan Doppler pitch resolver exists", builder, ref failureCount);
                AssertContains(renderer, "AbsoluteUniversePosition.ToCameraRelativeFloat3(predatorAup, playerAup)", "Doppler distance delta uses AUP camera-relative math", builder, ref failureCount);
                AssertContains(renderer, "RenderInteriorFdnReverbSample", "Dry interior FDN reverb exists", builder, ref failureCount);
                AssertContains(renderer, "bool nativeReverbActive = parameters.ReverbDspTier != (int)ReverbDspTier.UnityProfileOnly", "Low tier keeps native interior FDN disabled", builder, ref failureCount);
                AssertContains(renderer, "float interiorFdnSend = nativeReverbActive", "Interior FDN send is gated by native reverb tier", builder, ref failureCount);
                AssertContains(renderer, "AbyssalLowPassCutoffHertz = 380f", "Abyssal LPF reaches 380 Hz at full depth", builder, ref failureCount);
                AssertContains(renderer, "AbyssalLowPassFadeDepthMeters = 4500f", "5000 m depth maps to full abyssal LPF after 500 m start", builder, ref failureCount);
                AssertContains(renderer, "TinnitusCarrierHertz = 8000f", "O2 deprivation tinnitus carrier is 8000 Hz", builder, ref failureCount);
                AssertContains(renderer, "TinnitusLowPassCutoffHertz", "O2 deprivation lowers master LPF cutoff", builder, ref failureCount);
                AssertContains(renderTinnitusSample, "ApproximateOneMinusExpNegPositive(TinnitusPlayerStressExponentialSharpness * playerStress)", "O2 deprivation tinnitus uses Padé exponential stress scale", builder, ref failureCount);
                AssertContains(renderer, "120f - (60f * clamped) + (12f * x2) - x3", "Padé exp(-x) numerator is present", builder, ref failureCount);
                AssertContains(renderer, "PanicHeartbeatStressThreshold01 = 0.8f", "Panic heartbeat engages above 80 percent stress", builder, ref failureCount);
                AssertContains(renderer, "PanicHeartbeatAmbientHighCutMinimumGain = 0.38f", "Panic heartbeat dulls high-frequency ambient bed", builder, ref failureCount);
                AssertContains(updateCaveReverb, "targetWetMix = insideCaveVolume ? FakeCaveReverbMix01 : FakeOpenWaterReverbMix01", "Cave reverb uses fixed 0.8/0.2 fake volume mix", builder, ref failureCount);
                AssertNotContains(updateCaveReverb, "TryGetCachedEnclosureSample", "Critical cave reverb does not use enclosure ray fallback", builder, ref failureCount);
                AssertContains(renderer, "SonarGhostEchoTapCount = 3", "Sonar ghost echo is a three-tap synthetic echo", builder, ref failureCount);
                AssertNotContains(handleSonarPingSent, "Raycast", "Sonar ghost echo trigger has no raycast", builder, ref failureCount);
                AssertContains(renderSonarBlock, "tap.LeftPanDeltaGain", "Sonar ghost echoes use hash-derived stereo panning deltas", builder, ref failureCount);
                AssertContains(renderer, "BinauralMaximumMicroDelaySeconds = 0.0007f", "Fake ITD micro-delay caps at 0.7 ms", builder, ref failureCount);
                AssertContains(renderer, "math.abs(rightDot) * maxDelaySamples", "Fake ITD derives delay from head-right dot", builder, ref failureCount);
                AssertContains(renderer, "HullGroanLoopPitchMinimum = 0.8f", "Hull authored loop pitch minimum is 0.8", builder, ref failureCount);
                AssertContains(renderer, "HullGroanLoopPitchMaximum = 1.2f", "Hull authored loop pitch maximum is 1.2", builder, ref failureCount);
                AssertNotContains(renderHullStressBlock, "CarrierAPhase", "Hull DSP block has no FM carrier chain", builder, ref failureCount);
                AssertContains(renderBubbleBlock, "ToolCavitationMaximumGain", "Tool overheat cavitation writes high-frequency bursts into DSP scratch", builder, ref failureCount);
                AssertContains(renderBubbleBlock, "XorShiftSigned(sampleIndex, 0x7E5A3C91u)", "Tool cavitation noise is deterministic XorShift noise", builder, ref failureCount);
                AssertContains(renderer, "VehicleCavitationScreechStartMetersPerSecond = 20f", "Vehicle cavitation screech gates at 20 m/s", builder, ref failureCount);
                AssertContains(renderer, "VehicleCavitationHighPassAlpha", "Vehicle cavitation uses high-pass hash noise", builder, ref failureCount);
                AssertNotContains(renderer, "ResolveMinnaertFrequency", "Minnaert bubble formula is absent from critical renderer", builder, ref failureCount);
                AssertNotContains(renderer, "UnityEngine.Random", "Critical renderer has no UnityEngine.Random call", builder, ref failureCount);
                AssertContains(renderer, "PhysicsImpactMinimumAudibleMassVelocity = 5f", "Impact thuds gate at 5 m/s mass velocity", builder, ref failureCount);
                AssertContains(renderer, "ResolveImpactMaterialBlend", "Impact synthesis blends both AudioMaterialID values", builder, ref failureCount);
                AssertContains(renderer, "ResolveSonarMaterialPitchScale", "Sonar echo pitch uses AudioMaterialID", builder, ref failureCount);
                AssertContains(renderer, "ResolveSonarMaterialDecayMultiplier", "Sonar echo decay uses AudioMaterialID", builder, ref failureCount);
                AssertContains(renderer, "RenderPressureScrubberHumSample", "Pressure scrubber hum harmonic saturation exists", builder, ref failureCount);
                AssertContains(renderer, "FastSoftClip((fundamental + second + third) * cachedDrive)", "Pressure hum distortion uses cheap soft-clip saturation", builder, ref failureCount);
                AssertContains(renderer, "case ItemAudioMaterialId.Metal:", "Metal impacts route to clang multiplier", builder, ref failureCount);
                AssertContains(renderer, "return 1.1f;", "Metal impact clang multiplier is boosted", builder, ref failureCount);
                AssertContains(renderer, "return 0.4f;", "Rock/default impact clang multiplier remains dull", builder, ref failureCount);
                AssertContains(renderer, "SignalBus<HighSpeedImpactSignal>.GetFrameSnapshot()", "High-speed CCD impacts are consumed without dequeuing another domain's signal lane", builder, ref failureCount);
                AssertContains(renderer, "KineticImpactThudStartHertz = 150f", "Kinetic thud starts at 150 Hz", builder, ref failureCount);
                AssertContains(renderer, "KineticImpactThudEndHertz = 40f", "Kinetic thud descends to 40 Hz", builder, ref failureCount);
                AssertContains(renderer, "KineticImpactWaterLowPassHertz = 800f", "Underwater kinetic impacts use 800 Hz low-pass", builder, ref failureCount);
                AssertContains(renderer, "KineticImpactMaximumSafeEnergyJoules", "Kinetic energy is clamped before DSP gain mapping", builder, ref failureCount);
                AssertContains(renderer, "signal.EffectiveMass", "High-speed kinetic audio consumes authored effective mass when available", builder, ref failureCount);
                AssertContains(renderer, "ResolveHighSpeedImpactMaterialIds", "High-speed kinetic audio consumes material IDs instead of only source kind", builder, ref failureCount);
                AssertContains(renderer, "ResolveHighSpeedImpactMaterialPitchScale", "High-speed kinetic audio pitch responds to impact material", builder, ref failureCount);
                AssertContains(renderer, "NativeQueue<SonarEchoTap>", "Kinetic impact echo uses the existing native echo-tap bridge", builder, ref failureCount);
                AssertContains(renderer, "inactiveTapBuffer[0] = tap", "Kinetic impact echo writes its single generated tap without queue churn", builder, ref failureCount);
                AssertContains(renderer, "KineticImpactDuplicateHistoryCapacity = 8", "Kinetic impact duplicate admission keeps a fixed recent-packet ring", builder, ref failureCount);
                AssertContains(renderer, "RecordHighSpeedImpactSignal(signal.Frame, signalSignature)", "Kinetic impact duplicate admission records the precomputed signature", builder, ref failureCount);
                AssertContains(renderer, "entry.Valid != 0", "Kinetic impact duplicate admission ignores cold zeroed ring entries", builder, ref failureCount);
                AssertContains(renderer, "KineticImpactQualityPolicyRefreshFrames = 30", "Kinetic impact tier policy is cached instead of polled per packet", builder, ref failureCount);
                AssertContains(renderer, "RefreshKineticImpactQualityPolicyIfStale(Time.frameCount)", "Kinetic impact tier policy refreshes on a bounded cadence before signal admission", builder, ref failureCount);
                AssertContains(renderer, "RefreshAudioQualityPolicyIfStale(Time.frameCount)", "Critical audio quality policy is cached across DSP hot paths", builder, ref failureCount);
                AssertContains(renderer, "_cachedScalabilityTier", "Granular voice and sonar probe LOD use cached scalability tier", builder, ref failureCount);
                AssertContains(renderer, "_cachedQualityTier", "Reverb DSP tier uses cached quality tier", builder, ref failureCount);
                AssertContains(renderer, "ResolveKineticLowTierAudioService()", "Low-tier kinetic fallback uses cached audio-service resolution", builder, ref failureCount);
                AssertContains(renderer, "ResolveSpatialAudioManager()", "Critical audio reverb and binaural sampling use cached spatial-audio service resolution", builder, ref failureCount);
                AssertContains(renderer, "TransportCoordinatorLookupRetryFrames = 30", "Optional transport coordinator lookup is cadence-gated", builder, ref failureCount);
                AssertContains(renderer, "TryResolvePlayerTransportCoordinator()", "Transport audio helpers share the bounded coordinator resolver", builder, ref failureCount);
                AssertContains(renderer, "AudioServiceLookupRetryFrames = 30", "Optional cross-domain audio service lookups are cadence-gated", builder, ref failureCount);
                AssertContains(renderer, "ResolvePlayerRuntimeContext()", "Critical audio reads player runtime context through a bounded cached resolver", builder, ref failureCount);
                AssertContains(renderer, "ResolveEcosystemDirectorService()", "Apex heartbeat threat audio uses a bounded ecosystem service resolver", builder, ref failureCount);
                AssertContains(renderer, "ResolveSubmarineHullReadModel()", "Structural audio stress uses a bounded hull read-model resolver", builder, ref failureCount);
                AssertContains(renderer, "ResolveCachedBiomeId()", "Low-tier biome reverb uses cached biome policy", builder, ref failureCount);
                AssertNotContains(renderer, "OnAudioFilterRead", "Critical renderer has no managed Unity audio callback fallback", builder, ref failureCount);
            }

            if (synthesis.Length > 0)
            {
                AssertContains(synthesis, "KineticImpactSineOscillatorJob", "Burst kinetic impact sine oscillator job exists", builder, ref failureCount);
                AssertContains(synthesis, "CompileSynchronously = true", "Kinetic impact oscillator has synchronous Burst compile coverage", builder, ref failureCount);
                AssertContains(synthesis, "DepthStressGranularMath.FiniteOrDefault(StartHertz, 150f)", "Burst oscillator default starts at 150 Hz", builder, ref failureCount);
                AssertContains(synthesis, "DepthStressGranularMath.FiniteOrDefault(EndHertz, 40f)", "Burst oscillator default ends at 40 Hz", builder, ref failureCount);
            }

            if (deepPsychosis.Length > 0)
            {
                string psychosisSlowTick = ExtractMethodBody(deepPsychosis, "public void SlowTick()");
                string psychosisDependencyResolve = ExtractMethodBody(deepPsychosis, "private void TryResolveDependencies()");
                string psychosisCue = ExtractMethodBody(deepPsychosis, "private void PlayPsychosisCue()");
                AssertContains(deepPsychosis, "ResolvePlayerRuntimeContext()", "Deep psychosis player context uses a bounded cached resolver", builder, ref failureCount);
                AssertContains(deepPsychosis, "ResolveEnvironmentalStrainManager()", "Deep psychosis pollution stress uses a bounded environmental strain resolver", builder, ref failureCount);
                AssertContains(deepPsychosis, "ResolveAudioService()", "Deep psychosis cue playback uses cached audio-service resolution", builder, ref failureCount);
                AssertContains(deepPsychosis, "ResolveAcousticZone()", "Deep psychosis helmet whispers use cached acoustic-zone resolution", builder, ref failureCount);
                AssertContains(deepPsychosis, "DependencyRetryFrameInterval = 30", "Deep psychosis optional service retry cadence is bounded to 30 frames", builder, ref failureCount);
                AssertNotContains(psychosisSlowTick, "GlobalRegistry.EnvironmentalStrain", "Deep psychosis SlowTick does not poll environmental strain registry directly", builder, ref failureCount);
                AssertNotContains(psychosisDependencyResolve, "GlobalRegistry.Player", "Deep psychosis dependency resolver does not poll player registry directly", builder, ref failureCount);
                AssertNotContains(psychosisCue, "GlobalRegistry.Audio", "Deep psychosis cue playback does not poll audio registry directly", builder, ref failureCount);
                AssertNotContains(psychosisCue, "GlobalRegistry.AcousticZone", "Deep psychosis cue playback does not poll acoustic-zone registry directly", builder, ref failureCount);
            }

            if (musicDirector.Length > 0)
            {
                string musicResolveDependencies = ExtractMethodBody(musicDirector, "private void ResolveDependencies()");
                string musicResolveBaseContext = ExtractMethodBody(musicDirector, "private bool ResolveBaseContext()");
                string musicResolveMixerGroup = ExtractMethodBody(musicDirector, "private AudioMixerGroup ResolveMusicMixerGroup()");
                string musicResolveStormPressure = ExtractMethodBody(musicDirector, "private float ResolveStormPressure01(float depthMeters)");
                string musicDepthEntered = ExtractMethodBody(musicDirector, "private void HandleDepthZoneEntered(DepthZoneProfile zone)");
                string musicRareDiscovery = ExtractMethodBody(musicDirector, "private void HandleRareDiscoveryRequested(Vector3 position)");
                string musicShouldDepthDiscovery = ExtractMethodBody(musicDirector, "private bool ShouldPlayDepthDiscoveryStinger(DepthZoneProfile zone)");
                string musicFirstHourBoost = ExtractMethodBody(musicDirector, "private float ResolveFirstHourPressureBoost01(");
                AssertContains(musicDirector, "ResolvePlayerRuntimeContext()", "Music director player context uses a bounded cached resolver", builder, ref failureCount);
                AssertContains(musicDirector, "ResolveAudioService()", "Music director mixer routing uses cached audio-service resolution", builder, ref failureCount);
                AssertContains(musicDirector, "ResolveAcousticZone()", "Music director base context uses cached acoustic-zone resolution", builder, ref failureCount);
                AssertContains(musicDirector, "ResolveDepthZoneDirector()", "Music director depth-zone dependency uses cached runtime resolution", builder, ref failureCount);
                AssertContains(musicDirector, "ResolveSurfaceWeatherDirector()", "Music director storm pressure uses cached surface-weather resolution", builder, ref failureCount);
                AssertContains(musicDirector, "ResolveFirstHourDirector()", "Music director stinger gates use cached first-hour resolution", builder, ref failureCount);
                AssertContains(musicDirector, "ClearCachedRuntimeServices()", "Music director clears cached runtime services on disable/destroy", builder, ref failureCount);
                AssertNotContains(musicResolveDependencies, "GlobalRegistry.Player", "Music director dependency resolver does not poll player registry directly", builder, ref failureCount);
                AssertNotContains(musicResolveDependencies, "GlobalRegistry.DepthZone", "Music director dependency resolver does not poll depth-zone registry directly", builder, ref failureCount);
                AssertNotContains(musicResolveBaseContext, "GlobalRegistry.AcousticZone", "Music director base-context resolver does not poll acoustic-zone registry directly", builder, ref failureCount);
                AssertNotContains(musicResolveMixerGroup, "GlobalRegistry.Audio", "Music director mixer routing does not poll audio registry directly", builder, ref failureCount);
                AssertNotContains(musicResolveStormPressure, "GlobalRegistry.SurfaceWeather", "Music director storm pressure does not poll surface-weather registry directly", builder, ref failureCount);
                AssertNotContains(musicDepthEntered, "GlobalRegistry.FirstHour", "Music director depth stinger gate does not poll first-hour registry directly", builder, ref failureCount);
                AssertNotContains(musicRareDiscovery, "GlobalRegistry.FirstHour", "Music director rare-discovery gate does not poll first-hour registry directly", builder, ref failureCount);
                AssertNotContains(musicShouldDepthDiscovery, "GlobalRegistry.FirstHour", "Music director depth discovery gate does not poll first-hour registry directly", builder, ref failureCount);
                AssertNotContains(musicFirstHourBoost, "GlobalRegistry.FirstHour", "Music director first-hour pressure boost does not poll first-hour registry directly", builder, ref failureCount);
            }

            if (physicsApply.Length > 0)
            {
                AssertContains(physicsApply, "public readonly struct AcousticImpulseEvent", "Acoustic impulse event payload exists", builder, ref failureCount);
                AssertContains(physicsApply, "ForcePacketPriority.Critical", "Critical force packets are checked for acoustic routing", builder, ref failureCount);
                AssertContains(physicsApply, "ResolveKineticEnergyJoules", "Physics impulse energy resolver exists", builder, ref failureCount);
                AssertContains(physicsApply, "0.5f * math.max(0.0001f, massKg) * math.lengthsq(velocity)", "Kinetic energy uses 0.5*m*v^2", builder, ref failureCount);
                AssertContains(physicsApply, "ResolveAcousticImpulseVolume01", "Kinetic energy maps to audio volume", builder, ref failureCount);
                AssertContains(physicsApply, "PhysicsEventBus.NotifyAcousticImpulse", "Critical force packets publish acoustic impulses", builder, ref failureCount);
                AssertContains(physicsApply, "ProxyLightRegistry.RegisterOrUpdate", "Critical collisions spawn transient proxy light sparks", builder, ref failureCount);
                AssertContains(physicsApply, "return Instance;", "Physics apply runtime resolves through GlobalRegistry instead of self-spawn", builder, ref failureCount);
                AssertNotContains(physicsApply, "new GameObject(\"[PhysicsApplySystem]\")", "Physics apply runtime does not self-spawn", builder, ref failureCount);
            }

            if (spectrumSystem.Length > 0)
            {
                AssertContains(spectrumSystem, "public byte AudioMaterialId", "Sonar echo events carry AudioMaterialID", builder, ref failureCount);
                AssertContains(spectrumSystem, "Shader.SetGlobalVector(_ShaderHectonSonarPrimaryPulse", "Active sonar ping publishes primary pulse as an O(1) shader global", builder, ref failureCount);
                AssertContains(spectrumSystem, "Shader.SetGlobalVector(_ShaderHectonSonarVisualParams", "Active sonar ping publishes visual pulse parameters without object scanning", builder, ref failureCount);
                AssertContains(spectrumSystem, "PublishActiveSonarDangerImpulse", "Active sonar ping routes visibility cost into acoustic aggro", builder, ref failureCount);
                AssertContains(spectrumSystem, "PhysicsEventBus.NotifyLargeAcousticImpulse(in impulseEvent)", "Active sonar aggro publishes LargeAcousticImpulseEvent", builder, ref failureCount);
                AssertContains(spectrumSystem, "private NativeArray<uint> _aupDiscoveryGrid", "Sonar map owns a persistent AUP discovery bit grid", builder, ref failureCount);
                AssertContains(spectrumSystem, "NativeMemorySentinel.RegisterNativeArray", "AUP discovery grid is registered with NativeMemorySentinel", builder, ref failureCount);
                AssertContains(spectrumSystem, "nameof(_aupDiscoveryGrid)", "AUP discovery grid sentinel registration uses the concrete field name", builder, ref failureCount);
                AssertContains(spectrumSystem, "MarkAupDiscoveryPulseShell(origin, radius, pulseIntensity)", "Sonar reveal stamps discovery bits from pulse shell", builder, ref failureCount);
                AssertContains(spectrumSystem, "ResolvePlayerSpeedMagnitudeSqr() - speedStartSqr", "Radar distortion uses squared velocity thresholds", builder, ref failureCount);
                AssertContains(spectrumSystem, "_HectonSonarRadarDistortion", "Radar distortion global drives HUD ghost/flicker shader path", builder, ref failureCount);
                AssertContains(spectrumSystem, "if (!isActivePing)", "World snapshot build is excluded from active ping shader path", builder, ref failureCount);
                AssertContains(spectrumSystem, "WorldSpatialHashGrid.BuildSonarSnapshot", "Passive sonar snapshot remains explicit and separate from active ping shader path", builder, ref failureCount);
            }

            if (resourceNode.Length > 0)
            {
                AssertNotContains(resourceNode, "ISonarPingEventListener", "Resource nodes do not subscribe to the active-sonar object loop", builder, ref failureCount);
                AssertNotContains(resourceNode, "RegisterSonarPingListener(this)", "Resource sonar reflection remains shader-authored instead of per-node C# dispatch", builder, ref failureCount);
            }

            if (sonarGridOverlay.Length > 0)
            {
                AssertContains(sonarGridOverlay, "_HectonSonarPrimaryPulse", "Shader sonar reflection derives from primary pulse globals", builder, ref failureCount);
                AssertContains(sonarGridOverlay, "automaticEchoWave", "Shader sonar reflection derives automatic echo wave", builder, ref failureCount);
                AssertContains(sonarGridOverlay, "depthGradient = abs(depthDx) + abs(depthDy)", "Shader sonar contour uses scene-depth derivative edge test", builder, ref failureCount);
                AssertContains(sonarGridOverlay, "UNITY_PASS_STEREO_INSTANCE_ID", "Shader sonar overlay keeps stereo instance-safe UV flow", builder, ref failureCount);
                AssertContains(sonarGridOverlay, "EvaluateWorldMemoryPulseBand", "Shader sonar world memory stamps pulse history without object contacts", builder, ref failureCount);
                AssertNotContains(sonarGridOverlay, "_SonarRevealContacts", "Shader sonar overlay has no dead per-contact array loop", builder, ref failureCount);
                AssertNotContains(sonarGridOverlay, "normalize(", "Shader sonar overlay avoids normalize in fullscreen hot path", builder, ref failureCount);
            }

            if (sonarPointCloudFeature.Length > 0)
            {
                AssertContains(sonarPointCloudFeature, "using Unity.Mathematics", "Sonar point-cloud RenderGraph feature uses math intrinsics", builder, ref failureCount);
                AssertContains(sonarPointCloudFeature, "math.round", "Sonar point-cloud texture quantization uses math.round", builder, ref failureCount);
                AssertNotContains(sonarPointCloudFeature, "Mathf.", "Sonar point-cloud RenderGraph path avoids Mathf calls", builder, ref failureCount);
            }

            if (suitVisor.Length > 0)
            {
                AssertNotContains(suitVisor, "_SonarRevealContacts", "Suit visor sonar overlay has no dead contact array loop", builder, ref failureCount);
                AssertNotContains(suitVisor, "_SonarRevealContactCount", "Suit visor sonar overlay has no dead contact count uniform", builder, ref failureCount);
                AssertContains(suitVisor, "ApproximateMagnitude2D", "Suit visor visor-space magnitudes use cinematic axis approximation", builder, ref failureCount);
                AssertContains(suitVisor, "FastPowerCurve01", "Suit visor power curves use ALU-cheap approximation", builder, ref failureCount);
                AssertNotContains(suitVisor, "sqrt(", "Suit visor shader avoids sqrt in hot visual path", builder, ref failureCount);
                AssertNotContains(suitVisor, "pow(", "Suit visor shader avoids pow in hot visual path", builder, ref failureCount);
                AssertNotContains(suitVisor, "length(", "Suit visor shader avoids length in hot visual path", builder, ref failureCount);
            }

            if (leviathanOrganic.Length > 0)
            {
                AssertContains(leviathanOrganic, "_HectonSonarNoirHideDistance", "Leviathan shader receives global noir hide distance", builder, ref failureCount);
                AssertContains(leviathanOrganic, "ClipLeviathanNoirSilhouette", "Leviathan shader clips distant bodies unless sonar reveals them", builder, ref failureCount);
                AssertContains(leviathanOrganic, "EvaluateLeviathanSonarReveal", "Leviathan shader evaluates sonar reveal band before clipping", builder, ref failureCount);
                AssertContains(leviathanOrganic, "NormalizeApprox3D", "Leviathan shader uses cinematic normalization approximation", builder, ref failureCount);
                AssertNotContains(leviathanOrganic, "sqrt(", "Leviathan shader avoids sqrt in sonar-visible path", builder, ref failureCount);
                AssertNotContains(leviathanOrganic, "pow(", "Leviathan shader avoids pow in sonar-visible path", builder, ref failureCount);
                AssertNotContains(leviathanOrganic, "length(", "Leviathan shader avoids length in sonar-visible path", builder, ref failureCount);
                AssertNotContains(leviathanOrganic, "normalize(", "Leviathan shader avoids normalize in sonar-visible path", builder, ref failureCount);
            }

            if (resourceNodeTemplate.Length > 0)
                AssertContains(resourceNodeTemplate, "public byte AudioMaterialID", "Resource templates expose sonar AudioMaterialID", builder, ref failureCount);

            if (toolHaptics.Length > 0)
            {
                string hapticsTick = ExtractMethodBody(toolHaptics, "public void Tick(float deltaTime)");
                AssertContains(toolHaptics, "IPhysicsAcousticImpulseEventListener", "Tool haptics receive acoustic impulses", builder, ref failureCount);
                AssertContains(toolHaptics, "LeftMotorMask", "Left-side collision haptics route to left motor", builder, ref failureCount);
                AssertContains(toolHaptics, "GlobalRegistry.ToolHaptics", "Tool haptics resolve through GlobalRegistry", builder, ref failureCount);
                AssertContains(toolHaptics, "ResolveHapticDecayFactor", "Tool haptics use Padé decay approximation", builder, ref failureCount);
                AssertNotContains(toolHaptics, "_instance", "Tool haptics has no classic singleton field", builder, ref failureCount);
                AssertNotContains(toolHaptics, "new GameObject(\"[ToolHapticsRuntime]\")", "Tool haptics does not self-spawn", builder, ref failureCount);
                AssertNotContains(hapticsTick, "math.exp", "Tool haptics Tick avoids libm exp", builder, ref failureCount);
            }

            if (globalRegistry.Length > 0 && globalRegistryContracts.Length > 0)
            {
                AssertContains(globalRegistry, "public static ToolHapticsRuntime ToolHaptics", "GlobalRegistry exposes authoritative ToolHaptics slot", builder, ref failureCount);
                AssertContains(globalRegistry, "RegisterToolHapticsRuntime", "GlobalRegistry registers tool haptics runtime", builder, ref failureCount);
                AssertContains(globalRegistryContracts, "ToolHapticsRuntime = 118", "GlobalRegistry service enum includes ToolHaptics slot", builder, ref failureCount);
            }

            if (fakeRadar.Length > 0)
            {
                AssertContains(fakeRadar, "ThermalNoiseStartDepthMeters = 4000f", "Pressure ghost blips begin below 4000 m", builder, ref failureCount);
                AssertContains(fakeRadar, "HashThermalNoiseGhost", "Pressure ghost blips use deterministic hash noise", builder, ref failureCount);
            }

            if (echolocationTranslator.Length > 0)
            {
                AssertContains(echolocationTranslator, "IPhysicsAcousticImpulseEventListener", "Echolocation HUD receives acoustic impulses", builder, ref failureCount);
                AssertContains(echolocationTranslator, "DefaultVisualSoundWaveText", "Leviathan acoustic impulses render visual sound wave text", builder, ref failureCount);
                AssertContains(echolocationTranslator, "CurrentFogAttenuationDistance <= HeavyFogAttenuationDistanceMeters", "Visual sound waves require blindness or heavy fog", builder, ref failureCount);
            }

            if (playerPda.Length > 0)
            {
                string playSound = ExtractMethodBody(playerPda, "private void PlaySound(AudioClip clip, float volume, float pitch)");
                AssertContains(playSound, "audioManager.PlayAtPoint(clip, ResolvePdaAudioPosition(), volume, pitch, audioManager.InterfaceGroup)", "PDA clicks route through SpatialAudioManager at the PDA hand AUP", builder, ref failureCount);
                AssertNotContains(playSound, "PlayStatic2D", "PDA click helper does not route through 2D UI audio", builder, ref failureCount);
            }

            if (playerStressVfx.Length > 0)
            {
                AssertContains(playerStressVfx, "PlayHeartbeat(audioStress01)", "Heartbeat audio is driven from stress VFX update", builder, ref failureCount);
                AssertContains(playerStressVfx, "ApplyStressPulse(stress01, beat01, fog01, frost01)", "Heartbeat pulse is synchronized with visual UI distortion", builder, ref failureCount);
            }

            if (eventsSource.Length > 0)
                AssertContains(eventsSource, "LeviathanRoar", "Procedural audio event kind routes Leviathan roar", builder, ref failureCount);

            if (acousticZone.Length > 0)
            {
                string acousticPlayMadness = ExtractMethodBody(acousticZone, "internal void PlayMadnessWhisperCue()");
                string acousticEmitterOcclusion = ExtractMethodBody(acousticZone, "private void UpdateEmitterOcclusionState(AudioListener listener)");
                AssertContains(acousticZone, "[StructLayout(LayoutKind.Sequential, Size = 1)]", "Acoustic-zone NativeQueue payload is a one-byte blittable event token", builder, ref failureCount);
                AssertContains(acousticZone, "private readonly byte _isInterior", "Acoustic-zone payload avoids bool field layout ambiguity in native queues", builder, ref failureCount);
                AssertContains(acousticZone, "NativeMemorySentinel.RegisterNativeQueue", "Acoustic-zone event lanes are registered with NativeMemorySentinel", builder, ref failureCount);
                AssertContains(acousticZone, "PrewarmQueue(ref _pendingZoneChanges, PendingZoneChangeCapacity)", "Acoustic-zone front queue is cold-prewarmed before gameplay enqueue", builder, ref failureCount);
                AssertContains(acousticZone, "PrewarmQueue(ref _nextFrameZoneChanges, PendingZoneChangeCapacity)", "Acoustic-zone reentrant queue is cold-prewarmed before gameplay enqueue", builder, ref failureCount);
                AssertContains(acousticZone, "GlobalTelemetryBus.PublishPerformanceWarning(_overflowWarningHash, _zoneChangeQueueHash, PendingZoneChangeCapacity)", "Acoustic-zone overflow drop emits hash-only telemetry", builder, ref failureCount);
                AssertContains(acousticZone, "AudioServiceResolveRetryFrames = 30", "Acoustic-zone audio service lookup is cadence-gated", builder, ref failureCount);
                AssertContains(acousticZone, "ResolveAudioService()", "Acoustic-zone cue playback uses cached audio-service resolution", builder, ref failureCount);
                AssertContains(acousticZone, "ResolveSpatialAudioManager()", "Acoustic-zone emitter occlusion uses cached spatial-audio resolution", builder, ref failureCount);
                AssertContains(acousticZone, "ClearCachedAudioServices()", "Acoustic-zone clears cached audio services on disable/destroy", builder, ref failureCount);
                AssertNotContains(acousticPlayMadness, "GlobalRegistry.Audio", "Acoustic-zone madness cue does not poll audio registry directly", builder, ref failureCount);
                AssertNotContains(acousticEmitterOcclusion, "GlobalRegistry.Audio", "Acoustic-zone emitter occlusion does not poll audio registry directly", builder, ref failureCount);
            }

            if (audioLogEvents.Length > 0)
            {
                string audioLogEnqueue = ExtractMethodBody(audioLogEvents, "private static void Enqueue(AudioLogEventType type, uint logHash, float durationSeconds, AudioLogData data)");
                string queueOverflow = ExtractMethodBody(audioLogEvents, "private static void ReportQueueOverflow(AudioLogEventType type)");
                string referenceSlotOverflow = ExtractMethodBody(audioLogEvents, "private static void ReportReferenceSlotOverflow(AudioLogEventType type)");
                string resetBody = ExtractMethodBody(audioLogEvents, "private static void ResetStaticState()");
                AssertContains(audioLogEvents, "public static int DroppedEventCount => _droppedEventCount", "Audio-log events expose dropped queue-event counter", builder, ref failureCount);
                AssertContains(audioLogEvents, "public static int DroppedReferenceSlotCount => _droppedReferenceSlotCount", "Audio-log events expose dropped sidecar-reference counter", builder, ref failureCount);
                AssertContains(audioLogEnqueue, "ReportQueueOverflow(type)", "Audio-log queue overflow reports the event type", builder, ref failureCount);
                AssertContains(audioLogEnqueue, "ReportReferenceSlotOverflow(type)", "Audio-log sidecar-slot overflow reports the event type", builder, ref failureCount);
                AssertContains(queueOverflow, "AudioLogQueueContextHash ^ ((uint)type << 24)", "Audio-log queue overflow context encodes event type", builder, ref failureCount);
                AssertContains(referenceSlotOverflow, "AudioLogReferenceSlotContextHash ^ ((uint)type << 24)", "Audio-log reference-slot overflow context encodes event type", builder, ref failureCount);
                AssertContains(queueOverflow, "_lastQueueOverflowTelemetryFrame == frame", "Audio-log queue overflow telemetry is frame-rate limited", builder, ref failureCount);
                AssertContains(referenceSlotOverflow, "_lastReferenceSlotOverflowTelemetryFrame == frame", "Audio-log reference-slot overflow telemetry is frame-rate limited", builder, ref failureCount);
                AssertContains(resetBody, "_droppedEventCount = 0", "Audio-log reset clears dropped-event counter", builder, ref failureCount);
                AssertContains(resetBody, "_droppedReferenceSlotCount = 0", "Audio-log reset clears sidecar overflow counter", builder, ref failureCount);
            }

            if (ringBuffer.Length > 0)
            {
                AssertContains(ringBuffer, "CrashTelemetryBuffer.ReportAudioOverflowDropWarning", "SPSC overflow drop emits crash telemetry", builder, ref failureCount);
                AssertContains(ringBuffer, "_lastTelemetryOverflowDropCount", "SPSC overflow telemetry is rate-gated", builder, ref failureCount);
            }

            if (telemetry.Length > 0)
            {
                AssertContains(telemetry, "AudioOverflowDropWarning", "Crash telemetry stores audio overflow fault bit", builder, ref failureCount);
                AssertContains(telemetry, "WriteAudioOverflowDropTelemetry", "Crash telemetry writes audio overflow ring entry", builder, ref failureCount);
                AssertContains(telemetry, "SystemBits.Audio", "Crash telemetry tags audio subsystem rows", builder, ref failureCount);
            }

            builder.Append("STATUS: ");
            builder.AppendLine(failureCount == 0 ? "PASS" : "FAIL");
            report = builder.ToString();
            return failureCount == 0;
        }

        private static string ReadAssetText(string assetPath, StringBuilder builder, ref int failureCount)
        {
            string root = Directory.GetParent(Application.dataPath)?.FullName;
            string absolutePath = root == null
                ? assetPath
                : Path.Combine(root, assetPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absolutePath))
            {
                AppendFailure(builder, ref failureCount, "Missing asset: " + assetPath);
                return string.Empty;
            }

            return File.ReadAllText(absolutePath);
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            if (signatureIndex < 0)
                return string.Empty;

            int braceStart = source.IndexOf('{', signatureIndex);
            if (braceStart < 0)
                return string.Empty;

            int depth = 0;
            for (int i = braceStart; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                    depth++;
                else if (c == '}')
                    depth--;

                if (depth == 0)
                    return source.Substring(braceStart, i - braceStart + 1);
            }

            return string.Empty;
        }

        private static void AssertContains(string source, string needle, string message, StringBuilder builder, ref int failureCount)
        {
            if (source.IndexOf(needle, StringComparison.Ordinal) >= 0)
            {
                builder.Append("[PASS] ").AppendLine(message);
                return;
            }

            AppendFailure(builder, ref failureCount, message + " :: missing `" + needle + "`");
        }

        private static void AssertNotContains(string source, string needle, string message, StringBuilder builder, ref int failureCount)
        {
            if (source.IndexOf(needle, StringComparison.Ordinal) < 0)
            {
                builder.Append("[PASS] ").AppendLine(message);
                return;
            }

            AppendFailure(builder, ref failureCount, message + " :: found forbidden `" + needle + "`");
        }

        private static void AppendFailure(StringBuilder builder, ref int failureCount, string message)
        {
            failureCount++;
            builder.Append("[FAIL] ").AppendLine(message);
        }
    }
}
#endif
