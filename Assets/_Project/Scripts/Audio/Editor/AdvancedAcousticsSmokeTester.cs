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
        private const string GlobalSignalsPath = "Assets/_Project/Scripts/Core/GlobalSignals.cs";
        private const string AcousticZonePath = "Assets/_Project/Scripts/AcousticZoneController.cs";
        private const string AudioLogEventsPath = "Assets/_Project/Scripts/AudioLog/AudioLogEvents.cs";
        private const string PlayerPdaPath = "Assets/_Project/Scripts/PlayerPDA.cs";
        private const string PlayerStressVfxPath = "Assets/_Project/Scripts/Visor/PlayerStressVFX.cs";
        private const string DeepPsychosisPath = "Assets/_Project/Scripts/Audio/DeepPsychosisController.cs";
        private const string HectonMusicDirectorPath = "Assets/_Project/Scripts/Audio/HectonMusicDirector.cs";
        private const string AdaptiveStemMixerPath = "Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs";
        private const string DynamicMusicGranularSynthPath = "Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs";
        private const string DirectorAIPath = "Assets/_Project/Scripts/HectonDirectorAI.cs";
        private const string PrologueAcousticOrchestratorPath = "Assets/_Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs";
        private const string VocalWarningSystemPath = "Assets/_Project/Scripts/Audio/VocalWarningSystem.cs";
        private const string PlayerThrusterAudioPath = "Assets/_Project/Scripts/PlayerThrusterAudio.cs";
        private const string IndirectVegetationRendererPath = "Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs";
        private const string DiegeticPanelControllerPath = "Assets/_Project/Scripts/UI/DiegeticPanelController.cs";
        private const string SuitHudOverlayPath = "Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs";
        private const string DiegeticTooltipSystemPath = "Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs";

        [MenuItem("Hecton8/Audio/Run Advanced Acoustics Smoke Test")]
        public static void RunMenuItem()
        {
            bool passed = Run(out string report);
            if (passed)
                Hecton8.Core.H8Debug.Log(report);
            else
                Hecton8.Core.H8Debug.LogError(report);
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
            string globalSignals = ReadAssetText(GlobalSignalsPath, builder, ref failureCount);
            string acousticZone = ReadAssetText(AcousticZonePath, builder, ref failureCount);
            string audioLogEvents = ReadAssetText(AudioLogEventsPath, builder, ref failureCount);
            string playerPda = ReadAssetText(PlayerPdaPath, builder, ref failureCount);
            string playerStressVfx = ReadAssetText(PlayerStressVfxPath, builder, ref failureCount);
            string deepPsychosis = ReadAssetText(DeepPsychosisPath, builder, ref failureCount);
            string musicDirector = ReadAssetText(HectonMusicDirectorPath, builder, ref failureCount);
            string adaptiveStemMixer = ReadAssetText(AdaptiveStemMixerPath, builder, ref failureCount);
            string dynamicMusicSynth = ReadAssetText(DynamicMusicGranularSynthPath, builder, ref failureCount);
            string directorAI = ReadAssetText(DirectorAIPath, builder, ref failureCount);
            string prologueAcoustic = ReadAssetText(PrologueAcousticOrchestratorPath, builder, ref failureCount);
            string vocalWarning = ReadAssetText(VocalWarningSystemPath, builder, ref failureCount);
            string playerThrusterAudio = ReadAssetText(PlayerThrusterAudioPath, builder, ref failureCount);
            string indirectVegetationRenderer = ReadAssetText(IndirectVegetationRendererPath, builder, ref failureCount);
            string diegeticPanelController = ReadAssetText(DiegeticPanelControllerPath, builder, ref failureCount);
            string suitHudOverlay = ReadAssetText(SuitHudOverlayPath, builder, ref failureCount);
            string diegeticTooltipSystem = ReadAssetText(DiegeticTooltipSystemPath, builder, ref failureCount);

            if (spatial.Length > 0)
            {
                string spatialPortalPath = ExtractMethodBody(spatial, "private bool TryResolveAcousticPortalPath(");
                string spatialUsePortalPath = ExtractMethodBody(spatial, "private bool ShouldUseAcousticPortalPath()");
                string spatialVoiceLimit = ExtractMethodBody(spatial, "private void RefreshVirtualPhysicalVoiceLimit(bool immediate)");
                string spatialListenerAup = ExtractMethodBody(spatial, "private bool TryResolvePlayerListenerAup(");
                string spatialWindTarget = ExtractMethodBody(spatial, "private float ResolveGlobalWindHowlTarget01()");
                string spatialWindOcclusion = ExtractMethodBody(spatial, "private bool ResolveGlobalWindHowlOccluded()");
                string spatialWaterDensity = ExtractMethodBody(spatial, "private void UpdateListenerWaterDensityMul(float deltaTime)");
                string spatialPolicyEnsure = ExtractMethodBody(spatial, "private void EnsureSpatialAudioPolicyCached()");
                string spatialPolicyCold = ExtractMethodBody(spatial, "private void RefreshSpatialAudioPolicyCold()");
                string spatialFoveatedRefresh = ExtractMethodBody(spatial, "private void RefreshFoveatedDirector()");
                string spatialFoveatedResolve = ExtractMethodBody(spatial, "private IFoveatedSimulationDirector ResolveFoveatedSimulationDirector()");
                string spatialColdRuntimeServices = ExtractMethodBody(spatial, "private void RefreshCachedAudioRuntimeServicesCold()");
                string spatialPrologueQueue = ExtractMethodBody(spatial, "public bool QueuePrologueAudioTransition(");
                string spatialHighSpeedQueue = ExtractMethodBody(spatial, "public bool QueueHighSpeedImpactSignal(");
                string spatialHabitatPortalGraph = ExtractMethodBody(spatial, "private bool TryBuildHabitatAcousticPortalGraph(");
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
                AssertContains(spatial, "SignalBus<PhysicsEventPayload>.GetFrameSnapshot()", "Spatial manager consumes acoustic impulses through the typed signal snapshot", builder, ref failureCount);
                AssertNotContains(spatial, "IPhysics" + "AcousticImpulseEventListener", "Spatial manager has no legacy acoustic-impulse listener interface", builder, ref failureCount);
                AssertNotContains(spatial, "Physics" + "Event" + "Bus.Register(this)", "Spatial manager does not subscribe to the legacy physics event bus", builder, ref failureCount);
                AssertContains(spatial, "math.dot((float3)listener.right, sourceDirection)", "Binaural ITD uses one ear-axis dot product", builder, ref failureCount);
                AssertContains(spatial, "TryQueueImpactRadarEmitter(impulseEvent.RuntimePosition", "Acoustic impulses feed passive HUD emitters", builder, ref failureCount);
                AssertContains(spatial, "ResolveAupDelta", "Long-range spatial audio direction uses AUP delta helpers", builder, ref failureCount);
                AssertContains(spatial, "AbsoluteUniversePosition.DistanceSq(in listenerAup, in sourceAup)", "Spatial audio distance uses int64-sector AUP distance math", builder, ref failureCount);
                AssertContains(spatial, "AbsoluteUniversePosition.ToCameraRelativeFloat3(in sourceAup, in listenerAup)", "Doppler/radar direction uses AUP camera-relative math", builder, ref failureCount);
                AssertContains(spatial, "ResolveGlobalSpatialAudioQualityWeight01()", "Spatial audio derives quality policy from continuous global quality", builder, ref failureCount);
                AssertContains(spatial, "SmoothQuality01(weight)", "Spatial audio virtual voice weight uses a smooth quality curve", builder, ref failureCount);
                AssertNotContains(spatial, "ConsumeScalabilitySignals();", "Spatial audio no longer drains binary scalability changes", builder, ref failureCount);
                AssertNotContains(spatial, "ReadOnlySpan<ScalabilityChangedEvent> signals = SignalBus<ScalabilityChangedEvent>.GetFrameSnapshot();", "Spatial audio avoids typed scalability snapshots for presentation policy", builder, ref failureCount);
                AssertNotContains(spatial, "IScalability" + "ChangedEventListener", "Spatial audio has no legacy scalability listener interface", builder, ref failureCount);
                AssertNotContains(spatial, "Scalability" + "Events.Register(this)", "Spatial audio does not register with the scalability listener registry", builder, ref failureCount);
                AssertNotContains(spatial, "Scalability" + "Events.Unregister(this)", "Spatial audio does not unregister from the scalability listener registry", builder, ref failureCount);
                AssertNotContains(spatial, "private void HandleScalabilityChanged(in ScalabilityChangedEvent payload)", "Spatial audio quality policy no longer updates from typed scalability payloads", builder, ref failureCount);
                AssertContains(spatial, "EnsureSpatialAudioPolicyCached()", "Spatial audio hot paths consume cached quality policy", builder, ref failureCount);
                AssertContains(spatialPolicyCold, "ResolveGlobalSpatialAudioQualityWeight01()", "Spatial audio seeds continuous quality policy only during cold cache refresh", builder, ref failureCount);
                AssertNotContains(spatialPolicyCold, "GlobalRegistry.ScalabilityTier", "Spatial audio does not seed scalability policy from hardware tier", builder, ref failureCount);
                AssertNotContains(spatialPolicyCold, "GlobalRegistry.H8_LOW_MEMORY_PROFILE", "Spatial audio does not seed low-memory policy", builder, ref failureCount);
                AssertNotContains(spatialPolicyEnsure, "GlobalRegistry.", "Spatial audio hot quality-cache guard does not hide registry reads", builder, ref failureCount);
                AssertContains(spatial, "SpatialAudioRegistryRetryFrames = 30", "Spatial audio optional service lookup is cadence-gated", builder, ref failureCount);
                AssertContains(spatial, "_cachedSpatialAudioQualityWeight01", "Spatial audio portal and virtualization policy use cached continuous quality", builder, ref failureCount);
                AssertNotContains(spatial, "ResolveCachedScalabilityTier()", "Spatial audio portal and virtualization policy no longer use cached scalability tier", builder, ref failureCount);
                AssertContains(spatial, "ResolvePlayerRuntimeContext()", "Spatial audio listener AUP and water-density state use cached player context", builder, ref failureCount);
                AssertContains(spatial, "ResolveAcousticZone()", "Spatial audio interior checks use cached acoustic-zone resolution", builder, ref failureCount);
                AssertContains(spatial, "ResolveWeatherService()", "Spatial audio wind howl uses cached weather service resolution", builder, ref failureCount);
                AssertContains(spatial, "ResolveSurfaceWeatherDirector()", "Spatial audio surface weather uses cached director resolution", builder, ref failureCount);
                AssertContains(spatial, "ResolveFoveatedSimulationDirector()", "Spatial audio foveated director uses bounded cached resolution", builder, ref failureCount);
                AssertContains(spatialFoveatedResolve, "GlobalRegistry.FoveatedSimulationDirector", "Spatial audio foveated director registry read is confined to the bounded resolver", builder, ref failureCount);
                AssertContains(spatialFoveatedResolve, "_foveatedDirectorResolveFrame = frame + SpatialAudioRegistryRetryFrames", "Spatial audio foveated director resolver is retry-cadenced", builder, ref failureCount);
                AssertNotContains(spatialFoveatedRefresh, "GlobalRegistry.FoveatedSimulationDirector", "Spatial audio slow-lane foveated refresh does not poll registry directly", builder, ref failureCount);
                AssertContains(spatial, "IGlobalRegistryHotSwapRefListener", "Spatial audio caches player-critical runtime through hot-swap rebinding", builder, ref failureCount);
                AssertContains(spatial, "TryRegisterHotSwapListener()", "Spatial audio registers for player-critical runtime hot swaps", builder, ref failureCount);
                AssertContains(spatial, "GlobalRegistry.TryUnregisterHotSwapListener(this)", "Spatial audio unregisters player-critical runtime hot-swap listener", builder, ref failureCount);
                AssertContains(spatial, "public void OnGlobalRegistryServiceRebound(", "Spatial audio receives ref-forwarded service rebinds", builder, ref failureCount);
                AssertContains(spatial, "GlobalRegistryServiceSlot.PlayerCriticalAudioRuntime", "Spatial audio listens for player-critical audio runtime rebinding", builder, ref failureCount);
                AssertContains(spatial, "_cachedPlayerCriticalAudio = currentService as IPlayerCriticalAudioSignalSink", "Spatial audio refreshes cached player-critical DSP sink from hot-swap payload", builder, ref failureCount);
                AssertContains(spatialColdRuntimeServices, "_cachedPlayerCriticalAudio = GlobalRegistry.PlayerCriticalAudioSignals", "Spatial audio seeds player-critical DSP sink only during cold cache refresh", builder, ref failureCount);
                AssertNotContains(spatialPrologueQueue, "GlobalRegistry.", "Prologue audio transition queue uses cached player-critical runtime", builder, ref failureCount);
                AssertNotContains(spatialHighSpeedQueue, "GlobalRegistry.", "High-speed impact queue uses cached player-critical runtime", builder, ref failureCount);
                AssertContains(spatialColdRuntimeServices, "_cachedHabitatGraph = GlobalRegistry.HabitatGraph", "Spatial audio seeds habitat portal graph only during cold cache refresh", builder, ref failureCount);
                AssertContains(spatial, "GlobalRegistryServiceSlot.Logistics", "Spatial audio listens for construction/logistics runtime rebinding", builder, ref failureCount);
                AssertContains(spatial, "_cachedHabitatGraph = currentService as IHabitatGraphService", "Spatial audio refreshes cached habitat graph from hot-swap payload", builder, ref failureCount);
                AssertNotContains(spatialHabitatPortalGraph, "GlobalRegistry.", "Habitat acoustic portal graph uses cached habitat graph route", builder, ref failureCount);
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
                string rendererTick = ExtractMethodBody(renderer, "public void Tick(float deltaTime)");
                string rendererReverbTier = ExtractMethodBody(renderer, "private ReverbDspTier ResolveReverbDspTier()");
                string rendererKineticLayer = ExtractMethodBody(renderer, "private bool TryQueueMinimumQualityKineticImpactLayer(Vector3 runtimePosition, float energy01, float proximity)");
                string rendererSonarProbeCount = ExtractMethodBody(renderer, "private int ResolveSonarSdfProbeCount()");
                string rendererEnsureQuality = ExtractMethodBody(renderer, "private void EnsureAudioQualityPolicyCached()");
                string rendererColdQuality = ExtractMethodBody(renderer, "private void RefreshAudioQualityPolicyCold()");
                string rendererRuntimeRegister = ExtractMethodBody(renderer, "private bool TryRegisterRuntimeService()");
                string rendererRuntimeUnregister = ExtractMethodBody(renderer, "private void TryUnregisterRuntimeService()");
                string rendererKineticAudioService = ExtractMethodBody(renderer, "private IAudioService ResolveKineticImpactAudioService()");
                string rendererSpatialAudioService = ExtractMethodBody(renderer, "private SpatialAudioManager ResolveSpatialAudioManager()");
                string rendererColdAudioServices = ExtractMethodBody(renderer, "private void RefreshAudioRuntimeServicesCold()");
                string rendererStaleAudioServices = ExtractMethodBody(renderer, "private void RefreshAudioRuntimeServicesIfStale()");
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
                AssertContains(renderer, "SignalBus<PhysicsEventPayload>.GetFrameSnapshot()", "Critical renderer consumes acoustic impulses through the typed physics payload snapshot", builder, ref failureCount);
                AssertContains(renderer, "SignalBus<PhysicsEventPayload>.TryPush(in payload)", "Critical renderer publishes predator acoustic impulses through the typed physics payload lane with explicit drop semantics", builder, ref failureCount);
                AssertContains(renderer, "ConsumeLaserCutterEventSignals();", "Critical renderer drains laser cutter state from typed signal snapshots", builder, ref failureCount);
                AssertContains(renderer, "SignalBus<global::Hecton8.Core.Contracts.Signals.LaserCutterEventPayload>.GetFrameSnapshot()", "Critical renderer consumes laser cutter payloads through the typed SignalBus lane", builder, ref failureCount);
                AssertContains(renderer, "ConsumeProceduralAudioSignals();", "Critical renderer drains procedural audio from typed signal snapshots", builder, ref failureCount);
                AssertContains(renderer, "ReadOnlySpan<AudioEvent> signals = SignalBus<AudioEvent>.GetFrameSnapshot();", "Critical renderer consumes procedural audio through a ReadOnlySpan typed-lane snapshot", builder, ref failureCount);
                AssertNotContains(renderer, "IPhysics" + "AcousticImpulseEventListener", "Critical renderer has no legacy acoustic-impulse listener interface", builder, ref failureCount);
                AssertNotContains(renderer, "Physics" + "Event" + "Bus.Register(this)", "Critical renderer does not subscribe to the legacy physics event bus", builder, ref failureCount);
                AssertNotContains(renderer, "ILaser" + "CutterEventListener", "Critical renderer has no legacy laser cutter listener interface", builder, ref failureCount);
                AssertNotContains(renderer, "LaserCutterEvents." + "Register(this)", "Critical renderer does not subscribe to laser cutter listener queues", builder, ref failureCount);
                AssertNotContains(renderer, "LaserCutterEvents." + "Unregister(this)", "Critical renderer does not unsubscribe from laser cutter listener queues", builder, ref failureCount);
                AssertNotContains(renderer, "IProcedural" + "AudioEventListener", "Critical renderer has no legacy procedural audio listener interface", builder, ref failureCount);
                AssertNotContains(renderer, "ProceduralAudioEvents." + "Register(this)", "Critical renderer does not subscribe to procedural audio listener queues", builder, ref failureCount);
                AssertNotContains(renderer, "ProceduralAudioEvents." + "Unregister(this)", "Critical renderer does not unsubscribe from procedural audio listener queues", builder, ref failureCount);
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
                AssertContains(renderer, "ResolveGlobalAudioQualityWeight01()", "Critical renderer derives audio presentation policy from continuous global quality", builder, ref failureCount);
                AssertContains(renderer, "ResolveCachedAudioQualityCurve01()", "Critical renderer uses a smooth cached quality curve for audio LOD", builder, ref failureCount);
                AssertNotContains(renderer, "ConsumeScalabilitySignals();", "Critical renderer no longer drains binary scalability changes", builder, ref failureCount);
                AssertNotContains(renderer, "ReadOnlySpan<ScalabilityChangedEvent> signals = SignalBus<ScalabilityChangedEvent>.GetFrameSnapshot();", "Critical renderer avoids typed scalability snapshots for audio presentation policy", builder, ref failureCount);
                AssertContains(renderer, "IGlobalRegistryHotSwapRefListener", "Critical renderer receives audio service hot-swap rebinds", builder, ref failureCount);
                AssertContains(renderer, "private static int s_runtimeInstalled", "Critical renderer publishes runtime-installed state without registry polling", builder, ref failureCount);
                AssertContains(renderer, "public static bool IsRuntimeInstalled => Volatile.Read(ref s_runtimeInstalled) != 0", "Critical renderer runtime-installed property reads the volatile lifecycle flag", builder, ref failureCount);
                AssertNotContains(renderer, "public static bool IsRuntimeInstalled => GlobalRegistry.PlayerCriticalAudio", "Critical renderer runtime-installed property does not poll the registry", builder, ref failureCount);
                AssertContains(rendererRuntimeRegister, "Volatile.Write(ref s_runtimeInstalled, 1)", "Critical renderer registration marks runtime installed", builder, ref failureCount);
                AssertContains(rendererRuntimeUnregister, "Volatile.Write(ref s_runtimeInstalled, GlobalRegistry.PlayerCriticalAudio != null ? 1 : 0)", "Critical renderer unregister refreshes runtime-installed flag from cold registry state", builder, ref failureCount);
                AssertNotContains(renderer, "IScalability" + "ChangedEventListener", "Critical renderer has no legacy scalability listener interface", builder, ref failureCount);
                AssertNotContains(renderer, "ScalabilityEvents." + "Register(this)", "Critical renderer does not subscribe to scalability listener queues", builder, ref failureCount);
                AssertNotContains(renderer, "ScalabilityEvents." + "Unregister(this)", "Critical renderer does not unsubscribe from scalability listener queues", builder, ref failureCount);
                AssertNotContains(renderer, "private void HandleScalabilityChanged(in ScalabilityChangedEvent payload)", "Critical renderer quality policy no longer updates from typed scalability payloads", builder, ref failureCount);
                AssertContains(renderer, "CacheAudioQualityPolicy(", "Critical renderer funnels quality policy through one cache writer", builder, ref failureCount);
                AssertNotContains(renderer, "payload.CurrentQualityTier", "Critical renderer event path does not consume binary scalability payload quality", builder, ref failureCount);
                AssertContains(renderer, "EnsureKineticImpactQualityPolicyCached()", "Kinetic impact tier policy uses the renderer quality cache", builder, ref failureCount);
                AssertContains(renderer, "EnsureAudioQualityPolicyCached()", "Critical audio quality policy is cached across DSP hot paths", builder, ref failureCount);
                AssertContains(rendererColdQuality, "ResolveGlobalAudioQualityWeight01()", "Critical renderer seeds continuous quality policy only during cold cache refresh", builder, ref failureCount);
                AssertNotContains(rendererColdQuality, "GlobalRegistry.ScalabilityTier", "Critical renderer does not seed scalability policy from hardware tier", builder, ref failureCount);
                AssertNotContains(rendererColdQuality, "GlobalRegistry.QualityTier", "Critical renderer does not seed hardware quality tier", builder, ref failureCount);
                AssertNotContains(rendererColdQuality, "GlobalRegistry.H8_LOW_MEMORY_PROFILE", "Critical renderer does not seed low-memory policy", builder, ref failureCount);
                AssertNotContains(rendererEnsureQuality, "GlobalRegistry.", "Critical renderer hot quality-cache guard does not hide registry reads", builder, ref failureCount);
                AssertNotContains(rendererTick, "GlobalRegistry.ScalabilityTier", "Critical renderer Tick does not poll scalability tier directly", builder, ref failureCount);
                AssertNotContains(rendererTick, "GlobalRegistry.QualityTier", "Critical renderer Tick does not poll hardware quality directly", builder, ref failureCount);
                AssertNotContains(rendererTick, "GlobalRegistry.H8_LOW_MEMORY_PROFILE", "Critical renderer Tick does not poll low-memory profile directly", builder, ref failureCount);
                AssertNotContains(rendererReverbTier, "GlobalRegistry.", "Critical renderer reverb tier resolver consumes cached quality only", builder, ref failureCount);
                AssertNotContains(rendererKineticLayer, "GlobalRegistry.", "Critical renderer kinetic minimum-quality layer consumes cached quality only", builder, ref failureCount);
                AssertNotContains(rendererSonarProbeCount, "GlobalRegistry.", "Critical renderer sonar probe LOD consumes cached quality only", builder, ref failureCount);
                AssertContains(rendererColdAudioServices, "GlobalRegistry.Audio", "Critical renderer seeds audio services only during cold runtime refresh", builder, ref failureCount);
                AssertContains(rendererStaleAudioServices, "AudioServiceLookupRetryFrames", "Critical renderer stale audio-service refresh is cadence-gated", builder, ref failureCount);
                AssertContains(rendererStaleAudioServices, "GlobalRegistry.Audio", "Critical renderer stale audio-service refresh owns the bounded registry read", builder, ref failureCount);
                AssertNotContains(rendererKineticAudioService, "GlobalRegistry.Audio", "Critical renderer low-tier fallback resolver does not poll audio registry directly", builder, ref failureCount);
                AssertNotContains(rendererSpatialAudioService, "GlobalRegistry.Audio", "Critical renderer spatial-audio resolver does not poll audio registry directly", builder, ref failureCount);
                AssertContains(renderer, "_cachedAudioQualityWeight01", "Granular voice, sonar probe, reverb, and kinetic layers use cached continuous quality", builder, ref failureCount);
                AssertNotContains(renderer, "_cachedScalabilityTier", "Granular voice and sonar probe LOD no longer use cached scalability tier", builder, ref failureCount);
                AssertNotContains(renderer, "_cachedQualityTier", "Reverb DSP tier no longer uses cached quality tier", builder, ref failureCount);
                AssertContains(renderer, "ResolveKineticImpactAudioService()", "Minimum-quality kinetic impact layer uses cached audio-service resolution", builder, ref failureCount);
                AssertNotContains(renderer, "ResolveKineticLowTierAudioService()", "Hardware-tier kinetic fallback resolver has been removed", builder, ref failureCount);
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

            if (adaptiveStemMixer.Length > 0)
            {
                string adaptiveDrain = ExtractMethodBody(adaptiveStemMixer, "private void DrainSignalInputs()");
                string adaptiveQuality = ExtractMethodBody(adaptiveStemMixer, "private float ResolveGlobalQualityWeightFromSnapshot()");
                string adaptiveLaneConfig = ExtractMethodBody(adaptiveStemMixer, "private static void EnsureDynamicMusicSignalLaneCold()");
                AssertContains(adaptiveStemMixer, "BufferID.ShinobuScalabilityState", "Adaptive stem mixer reads continuous quality from the vault-owned scalability state", builder, ref failureCount);
                AssertContains(adaptiveQuality, "state.GlobalQualityWeight", "Adaptive stem quality resolver consumes the continuous global quality weight", builder, ref failureCount);
                AssertNotContains(adaptiveDrain, "SignalBus<ScalabilityChangedEvent>.GetFrameSnapshot()", "Adaptive stem mixer does not drain binary scalability events for quality", builder, ref failureCount);
                AssertNotContains(adaptiveStemMixer, "ResolveQualityTierFallbackWeight", "Adaptive stem mixer has no quality-tier fallback mapper", builder, ref failureCount);
                AssertContains(adaptiveLaneConfig, "lowTierFrameSignals: 64", "Dynamic music scalar signal lane keeps full minimum-quality frame capacity", builder, ref failureCount);
            }

            if (dynamicMusicSynth.Length > 0)
            {
                string dynamicLaneConfig = ExtractMethodBody(dynamicMusicSynth, "private static void EnsureDynamicMusicSignalLaneCold()");
                string dynamicAudioCallback = ExtractMethodBody(dynamicMusicSynth, "private void OnAudioFilterRead(float[] data, int channels)");
                string dynamicLateFrame = ExtractMethodBody(dynamicMusicSynth, "public void LateFrameTick()");
                AssertContains(dynamicLaneConfig, "lowTierFrameSignals: 64", "Dynamic music granular synth keeps full minimum-quality scalar lane capacity", builder, ref failureCount);
                AssertContains(dynamicMusicSynth, "ResolveGlobalQualityWeightFromSnapshot()", "Dynamic music granular synth derives quality from the continuous quality snapshot", builder, ref failureCount);
                AssertContains(dynamicAudioCallback, "TryResolvePublishedAudioThreadCopyBuffer", "Dynamic music managed callback only reads the published audio-thread copy buffer", builder, ref failureCount);
                AssertContains(dynamicAudioCallback, "UnsafeUtility.MemCpy(destination, source", "Dynamic music managed callback copies prebuilt interleaved samples only", builder, ref failureCount);
                AssertContains(dynamicLateFrame, "PublishAudioThreadCopyBufferLateFrame()", "Dynamic music publishes a dedicated audio-thread copy buffer from LateFrame", builder, ref failureCount);
                AssertNotContains(dynamicAudioCallback, "TryAcquire", "Dynamic music managed callback must not acquire DataVault or mutation guards", builder, ref failureCount);
                AssertNotContains(dynamicAudioCallback, "ScheduleSynthJobs", "Dynamic music managed callback must not schedule synthesis work", builder, ref failureCount);
                AssertNotContains(dynamicAudioCallback, "GranularSynthesisJob", "Dynamic music managed callback must not synthesize samples", builder, ref failureCount);
                AssertNotContains(dynamicAudioCallback, "Stopwatch", "Dynamic music managed callback must not measure timing on the audio thread", builder, ref failureCount);
                AssertNotContains(dynamicAudioCallback, "AudioSettings", "Dynamic music managed callback must not query Unity audio settings on the audio thread", builder, ref failureCount);
            }

            if (indirectVegetationRenderer.Length > 0)
            {
                AssertContains(indirectVegetationRenderer, "ResolveVegetationQualityWeight01", "Indirect vegetation density policy reads continuous quality", builder, ref failureCount);
                AssertContains(indirectVegetationRenderer, "HomeostasisBrain.GlobalQualityWeight", "Indirect vegetation quality source is the continuous global weight", builder, ref failureCount);
                AssertNotContains(indirectVegetationRenderer, "GlobalRegistry.ScalabilityTierProfileByte", "Indirect vegetation does not read binary scalability profile", builder, ref failureCount);
                AssertNotContains(indirectVegetationRenderer, "SignalBus<ScalabilityChangedEvent>.GetFrameSnapshot()", "Indirect vegetation does not drain scalability snapshots", builder, ref failureCount);
                AssertNotContains(indirectVegetationRenderer, "ScalabilityEvents.", "Indirect vegetation has no scalability listener registration", builder, ref failureCount);
            }

            if (diegeticPanelController.Length > 0)
            {
                AssertContains(diegeticPanelController, "RefreshQualityPresentationIfNeeded()", "Diegetic panel refreshes presentation quality from runtime cadence", builder, ref failureCount);
                AssertContains(diegeticPanelController, "HomeostasisBrain.GlobalQualityWeight", "Diegetic panel quality source is the continuous global weight", builder, ref failureCount);
                AssertNotContains(diegeticPanelController, "IScalability" + "ChangedEventListener", "Diegetic panel has no scalability listener interface", builder, ref failureCount);
                AssertNotContains(diegeticPanelController, "ScalabilityEvents.", "Diegetic panel has no scalability listener registration", builder, ref failureCount);
                AssertNotContains(diegeticPanelController, "ScalabilityChangedEvent", "Diegetic panel no longer consumes binary scalability payloads", builder, ref failureCount);
            }

            if (suitHudOverlay.Length > 0)
            {
                AssertContains(suitHudOverlay, "HomeostasisBrain.GlobalQualityWeight", "Suit HUD quality source is the continuous global weight", builder, ref failureCount);
                AssertContains(suitHudOverlay, "_reactiveUiCadenceStride", "Suit HUD reactive cadence still scales from quality", builder, ref failureCount);
                AssertNotContains(suitHudOverlay, "IScalability" + "ChangedEventListener", "Suit HUD has no scalability listener interface", builder, ref failureCount);
                AssertNotContains(suitHudOverlay, "ScalabilityEvents.", "Suit HUD has no scalability listener registration", builder, ref failureCount);
                AssertNotContains(suitHudOverlay, "ScalabilityChangedEvent", "Suit HUD no longer consumes binary scalability payloads", builder, ref failureCount);
            }

            if (diegeticTooltipSystem.Length > 0)
            {
                AssertContains(diegeticTooltipSystem, "HomeostasisBrain.GlobalQualityWeight", "Diegetic tooltip quality source is the continuous global weight", builder, ref failureCount);
                AssertContains(diegeticTooltipSystem, "ResolveDitherWeight", "Diegetic tooltip dither remains continuous-quality scaled", builder, ref failureCount);
                AssertNotContains(diegeticTooltipSystem, "IScalability" + "ChangedEventListener", "Diegetic tooltip has no scalability listener interface", builder, ref failureCount);
                AssertNotContains(diegeticTooltipSystem, "ScalabilityEvents.", "Diegetic tooltip has no scalability listener registration", builder, ref failureCount);
                AssertNotContains(diegeticTooltipSystem, "ScalabilityChangedEvent", "Diegetic tooltip no longer consumes binary scalability payloads", builder, ref failureCount);
            }

            if (deepPsychosis.Length > 0)
            {
                string psychosisSlowTick = ExtractMethodBody(deepPsychosis, "public void SlowTick()");
                string psychosisDependencyResolve = ExtractMethodBody(deepPsychosis, "private void TryResolveDependencies()");
                string psychosisCue = ExtractMethodBody(deepPsychosis, "private void PlayPsychosisCue()");
                string psychosisPlayerResolver = ExtractMethodBody(deepPsychosis, "private IPlayerRuntimeContext ResolvePlayerRuntimeContext()");
                string psychosisStrainResolver = ExtractMethodBody(deepPsychosis, "private EnvironmentalStrainManager ResolveEnvironmentalStrainManager()");
                string psychosisAudioResolver = ExtractMethodBody(deepPsychosis, "private IAudioService ResolveAudioService()");
                string psychosisAcousticResolver = ExtractMethodBody(deepPsychosis, "private IAcousticZoneMadnessCueSink ResolveAcousticZone()");
                string psychosisColdRuntime = ExtractMethodBody(deepPsychosis, "private void RefreshCachedRuntimeServicesCold()");
                string psychosisReboundRuntime = ExtractMethodBody(deepPsychosis, "private void CacheReboundRuntimeService(");
                string psychosisPlayerRefresh = ExtractMethodBody(deepPsychosis, "private void RefreshPlayerRuntimeContextIfStale(");
                string psychosisStrainRefresh = ExtractMethodBody(deepPsychosis, "private void RefreshEnvironmentalStrainManagerIfStale(");
                string psychosisAudioRefresh = ExtractMethodBody(deepPsychosis, "private void RefreshAudioServiceIfStale(");
                string psychosisAcousticRefresh = ExtractMethodBody(deepPsychosis, "private void RefreshAcousticZoneIfStale(");
                AssertContains(deepPsychosis, "ResolvePlayerRuntimeContext()", "Deep psychosis player context uses a bounded cached resolver", builder, ref failureCount);
                AssertContains(deepPsychosis, "ResolveEnvironmentalStrainManager()", "Deep psychosis pollution stress uses a bounded environmental strain resolver", builder, ref failureCount);
                AssertContains(deepPsychosis, "ResolveAudioService()", "Deep psychosis cue playback uses cached audio-service resolution", builder, ref failureCount);
                AssertContains(deepPsychosis, "ResolveAcousticZone()", "Deep psychosis helmet whispers use cached acoustic-zone resolution", builder, ref failureCount);
                AssertContains(deepPsychosis, "IGlobalRegistryHotSwapRefListener", "Deep psychosis receives service hot-swap rebinds", builder, ref failureCount);
                AssertContains(deepPsychosis, "TryRegisterHotSwapListener()", "Deep psychosis registers hot-swap listener during lifecycle", builder, ref failureCount);
                AssertContains(deepPsychosis, "GlobalRegistry.TryUnregisterHotSwapListener(this)", "Deep psychosis unregisters hot-swap listener during lifecycle", builder, ref failureCount);
                AssertContains(psychosisColdRuntime, "GlobalRegistry.Player", "Deep psychosis cold-seeds player context", builder, ref failureCount);
                AssertContains(psychosisColdRuntime, "GlobalRegistry.EnvironmentalStrain", "Deep psychosis cold-seeds environmental strain service", builder, ref failureCount);
                AssertContains(psychosisColdRuntime, "GlobalRegistry.Audio", "Deep psychosis cold-seeds audio service", builder, ref failureCount);
                AssertContains(psychosisColdRuntime, "GlobalRegistry.AcousticZoneMadnessCueSink", "Deep psychosis cold-seeds acoustic-zone cue sink", builder, ref failureCount);
                AssertContains(psychosisReboundRuntime, "GlobalRegistryServiceSlot.Player", "Deep psychosis handles player service hot swaps", builder, ref failureCount);
                AssertContains(psychosisReboundRuntime, "GlobalRegistryServiceSlot.EnvironmentalStrainRuntime", "Deep psychosis handles strain service hot swaps", builder, ref failureCount);
                AssertContains(psychosisReboundRuntime, "GlobalRegistryServiceSlot.Audio", "Deep psychosis handles audio service hot swaps", builder, ref failureCount);
                AssertContains(psychosisReboundRuntime, "GlobalRegistryServiceSlot.AcousticZoneRuntime", "Deep psychosis handles acoustic-zone service hot swaps", builder, ref failureCount);
                AssertContains(deepPsychosis, "DependencyRetryFrameInterval = 30", "Deep psychosis optional service retry cadence is bounded to 30 frames", builder, ref failureCount);
                AssertContains(psychosisPlayerRefresh, "GlobalRegistry.Player", "Deep psychosis stale player refresh owns the bounded registry read", builder, ref failureCount);
                AssertContains(psychosisStrainRefresh, "GlobalRegistry.EnvironmentalStrain", "Deep psychosis stale strain refresh owns the bounded registry read", builder, ref failureCount);
                AssertContains(psychosisAudioRefresh, "GlobalRegistry.Audio", "Deep psychosis stale audio refresh owns the bounded registry read", builder, ref failureCount);
                AssertContains(psychosisAcousticRefresh, "GlobalRegistry.AcousticZoneMadnessCueSink", "Deep psychosis stale acoustic refresh owns the bounded registry read", builder, ref failureCount);
                AssertNotContains(psychosisPlayerResolver, "GlobalRegistry.Player", "Deep psychosis player resolver routes registry reads to the stale refresh helper", builder, ref failureCount);
                AssertNotContains(psychosisStrainResolver, "GlobalRegistry.EnvironmentalStrain", "Deep psychosis strain resolver routes registry reads to the stale refresh helper", builder, ref failureCount);
                AssertNotContains(psychosisAudioResolver, "GlobalRegistry.Audio", "Deep psychosis audio resolver routes registry reads to the stale refresh helper", builder, ref failureCount);
                AssertNotContains(psychosisAcousticResolver, "GlobalRegistry.AcousticZone", "Deep psychosis acoustic-zone resolver routes registry reads to the stale refresh helper", builder, ref failureCount);
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
                string musicPlayerResolver = ExtractMethodBody(musicDirector, "private IPlayerRuntimeContext ResolvePlayerRuntimeContext()");
                string musicAudioResolver = ExtractMethodBody(musicDirector, "private IAudioService ResolveAudioService()");
                string musicAcousticResolver = ExtractMethodBody(musicDirector, "private IAcousticZoneReadModel ResolveAcousticZone()");
                string musicDepthResolver = ExtractMethodBody(musicDirector, "private DepthZoneDirector ResolveDepthZoneDirector()");
                string musicSurfaceResolver = ExtractMethodBody(musicDirector, "private ISurfaceWeatherReadModel ResolveSurfaceWeatherDirector()");
                string musicFirstHourResolver = ExtractMethodBody(musicDirector, "private IFirstHourReadModel ResolveFirstHourDirector()");
                string musicReboundRuntime = ExtractMethodBody(musicDirector, "private void CacheReboundRuntimeService(");
                string musicAcousticDrain = ExtractMethodBody(musicDirector, "private void DrainAcousticZoneSignal()");
                string musicSynthRuntime = ExtractMethodBody(musicDirector, "private void EnsureProceduralSynthRuntime()");
                AssertContains(musicDirector, "ResolvePlayerRuntimeContext()", "Music director player context uses a bounded cached resolver", builder, ref failureCount);
                AssertContains(musicDirector, "ResolveAudioService()", "Music director mixer routing uses cached audio-service resolution", builder, ref failureCount);
                AssertContains(musicDirector, "ResolveAcousticZone()", "Music director base context uses cached acoustic-zone resolution", builder, ref failureCount);
                AssertContains(musicDirector, "ResolveDepthZoneDirector()", "Music director depth-zone dependency uses cached runtime resolution", builder, ref failureCount);
                AssertContains(musicDirector, "ResolveSurfaceWeatherDirector()", "Music director storm pressure uses cached surface-weather resolution", builder, ref failureCount);
                AssertContains(musicDirector, "ResolveFirstHourDirector()", "Music director stinger gates use cached first-hour resolution", builder, ref failureCount);
                AssertContains(musicDirector, "ClearCachedRuntimeServices()", "Music director clears cached runtime services on disable/destroy", builder, ref failureCount);
                AssertContains(musicDirector, "IGlobalRegistryHotSwapListener", "Music director receives runtime service hot swaps", builder, ref failureCount);
                AssertContains(musicDirector, "TryRegisterHotSwapListener()", "Music director registers hot-swap listener during lifecycle", builder, ref failureCount);
                AssertContains(musicDirector, "GlobalRegistry.TryUnregisterHotSwapListener(this)", "Music director unregisters hot-swap listener during lifecycle", builder, ref failureCount);
                AssertContains(musicDirector, "_depthZoneDirectorRuntimeCached", "Music director distinguishes serialized depth-zone references from runtime cache", builder, ref failureCount);
                AssertContains(musicReboundRuntime, "GlobalRegistryServiceSlot.Player", "Music director handles player service hot swaps", builder, ref failureCount);
                AssertContains(musicReboundRuntime, "GlobalRegistryServiceSlot.Audio", "Music director handles audio service hot swaps", builder, ref failureCount);
                AssertContains(musicReboundRuntime, "GlobalRegistryServiceSlot.AcousticZoneRuntime", "Music director handles acoustic-zone service hot swaps", builder, ref failureCount);
                AssertContains(musicReboundRuntime, "GlobalRegistryServiceSlot.DepthZoneRuntime", "Music director handles depth-zone service hot swaps", builder, ref failureCount);
                AssertContains(musicReboundRuntime, "GlobalRegistryServiceSlot.SurfaceWeatherRuntime", "Music director handles surface-weather service hot swaps", builder, ref failureCount);
                AssertContains(musicReboundRuntime, "GlobalRegistryServiceSlot.FirstHourRuntime", "Music director handles first-hour service hot swaps", builder, ref failureCount);
                AssertContains(musicDirector, "DrainAcousticZoneSignal();", "Music director drains acoustic-zone typed signals from tick lanes", builder, ref failureCount);
                AssertContains(musicAcousticDrain, "ReadOnlySpan<AcousticZoneChangedEvent> signals = SignalBus<AcousticZoneChangedEvent>.GetFrameSnapshot();", "Music director consumes acoustic-zone changes through a ReadOnlySpan typed-lane snapshot", builder, ref failureCount);
                AssertContains(musicAcousticDrain, "_lastAcousticZoneSignalFrame == frame", "Music director drains acoustic-zone signals at most once per frame", builder, ref failureCount);
                AssertContains(musicAcousticDrain, "HandleAcousticZoneChanged(signal.IsInterior != 0)", "Music director routes the latest acoustic-zone signal into existing music context logic", builder, ref failureCount);
                AssertContains(musicDirector, "DrainDirectorAISignals();", "Music director drains DirectorAI music signals from typed lanes", builder, ref failureCount);
                AssertContains(musicDirector, "ReadOnlySpan<DirectorAIMusicSignal> signals = SignalBus<DirectorAIMusicSignal>.GetFrameSnapshot();", "Music director consumes DirectorAI cues through a ReadOnlySpan typed-lane snapshot", builder, ref failureCount);
                AssertContains(musicDirector, "RefreshPolledMusicContext();", "Music director polls biome/depth runtime state instead of listener queues", builder, ref failureCount);
                AssertContains(musicDirector, "RefreshObservedBiomeMatrixState()", "Music director observes biome-matrix profile/depth state without registration", builder, ref failureCount);
                AssertContains(musicDirector, "RefreshObservedDepthZoneState()", "Music director observes depth-zone transitions without registration", builder, ref failureCount);
                AssertNotContains(musicDirector, "IAcoustic" + "ZoneEventListener", "Music director has no legacy acoustic-zone listener interface", builder, ref failureCount);
                AssertNotContains(musicDirector, "IBiome" + "MatrixEventListener", "Music director has no legacy biome-matrix listener interface", builder, ref failureCount);
                AssertNotContains(musicDirector, "IDepth" + "ZoneEventListener", "Music director has no legacy depth-zone listener interface", builder, ref failureCount);
                AssertNotContains(musicDirector, "IDirector" + "AIEventListener", "Music director has no legacy DirectorAI listener interface", builder, ref failureCount);
                AssertNotContains(musicDirector, "AcousticZoneEvents." + "Register(this)", "Music director does not subscribe to the old acoustic-zone event facade", builder, ref failureCount);
                AssertNotContains(musicDirector, "AcousticZoneEvents." + "Unregister(this)", "Music director does not unsubscribe from the old acoustic-zone event facade", builder, ref failureCount);
                AssertNotContains(musicDirector, "BiomeMatrixEvents." + "Register(this)", "Music director does not subscribe to biome-matrix listener queues", builder, ref failureCount);
                AssertNotContains(musicDirector, "DepthZoneEvents." + "Register(this)", "Music director does not subscribe to depth-zone listener queues", builder, ref failureCount);
                AssertNotContains(musicDirector, "DirectorAIEvents." + "Register(this)", "Music director does not subscribe to DirectorAI listener queues", builder, ref failureCount);
                AssertNotContains(musicDirector, "BiomeMatrixEvents." + "Unregister(this)", "Music director does not unsubscribe from biome-matrix listener queues", builder, ref failureCount);
                AssertNotContains(musicDirector, "DepthZoneEvents." + "Unregister(this)", "Music director does not unsubscribe from depth-zone listener queues", builder, ref failureCount);
                AssertNotContains(musicDirector, "DirectorAIEvents." + "Unregister(this)", "Music director does not unsubscribe from DirectorAI listener queues", builder, ref failureCount);
                AssertNotContains(musicPlayerResolver, "GlobalRegistry.Player", "Music director player resolver routes registry reads to the stale refresh helper", builder, ref failureCount);
                AssertNotContains(musicAudioResolver, "GlobalRegistry.Audio", "Music director audio resolver routes registry reads to the stale refresh helper", builder, ref failureCount);
                AssertNotContains(musicAcousticResolver, "GlobalRegistry.AcousticZone", "Music director acoustic resolver routes registry reads to the stale refresh helper", builder, ref failureCount);
                AssertNotContains(musicDepthResolver, "GlobalRegistry.DepthZone", "Music director depth resolver routes registry reads to the stale refresh helper", builder, ref failureCount);
                AssertNotContains(musicSurfaceResolver, "GlobalRegistry.SurfaceWeather", "Music director surface-weather resolver routes registry reads to the stale refresh helper", builder, ref failureCount);
                AssertNotContains(musicFirstHourResolver, "GlobalRegistry.FirstHour", "Music director first-hour resolver routes registry reads to the stale refresh helper", builder, ref failureCount);
                AssertNotContains(musicResolveDependencies, "GlobalRegistry.Player", "Music director dependency resolver does not poll player registry directly", builder, ref failureCount);
                AssertNotContains(musicResolveDependencies, "GlobalRegistry.DepthZone", "Music director dependency resolver does not poll depth-zone registry directly", builder, ref failureCount);
                AssertNotContains(musicResolveBaseContext, "GlobalRegistry.AcousticZone", "Music director base-context resolver does not poll acoustic-zone registry directly", builder, ref failureCount);
                AssertNotContains(musicResolveMixerGroup, "GlobalRegistry.Audio", "Music director mixer routing does not poll audio registry directly", builder, ref failureCount);
                AssertNotContains(musicResolveStormPressure, "GlobalRegistry.SurfaceWeather", "Music director storm pressure does not poll surface-weather registry directly", builder, ref failureCount);
                AssertNotContains(musicDepthEntered, "GlobalRegistry.FirstHour", "Music director depth stinger gate does not poll first-hour registry directly", builder, ref failureCount);
                AssertNotContains(musicRareDiscovery, "GlobalRegistry.FirstHour", "Music director rare-discovery gate does not poll first-hour registry directly", builder, ref failureCount);
                AssertNotContains(musicShouldDepthDiscovery, "GlobalRegistry.FirstHour", "Music director depth discovery gate does not poll first-hour registry directly", builder, ref failureCount);
                AssertNotContains(musicFirstHourBoost, "GlobalRegistry.FirstHour", "Music director first-hour pressure boost does not poll first-hour registry directly", builder, ref failureCount);
                AssertContains(musicSynthRuntime, "lowTierFrameSignals: 64", "Music director keeps full minimum-quality dynamic music scalar lane capacity", builder, ref failureCount);
            }

            if (prologueAcoustic.Length > 0)
            {
                string prologueLateFrame = ExtractMethodBody(prologueAcoustic, "public void LateFrameTick()");
                string prologueColdRuntime = ExtractMethodBody(prologueAcoustic, "private void RefreshRuntimeServicesCold()");
                string prologueQualityPolicy = ExtractMethodBody(prologueAcoustic, "private void RefreshQualityPolicy()");
                AssertContains(prologueAcoustic, "ResolveGlobalQualityWeight01()", "Prologue acoustic bridge derives presentation policy from continuous global quality", builder, ref failureCount);
                AssertContains(prologueAcoustic, "ResolveQualityCurve01()", "Prologue acoustic bridge uses a smooth quality curve for granular plasma stress", builder, ref failureCount);
                AssertNotContains(prologueAcoustic, "ConsumeScalabilitySignals();", "Prologue acoustic bridge no longer drains binary scalability changes", builder, ref failureCount);
                AssertNotContains(prologueAcoustic, "ReadOnlySpan<ScalabilityChangedEvent> signals = SignalBus<ScalabilityChangedEvent>.GetFrameSnapshot();", "Prologue acoustic bridge avoids typed scalability snapshots for presentation policy", builder, ref failureCount);
                AssertNotContains(prologueAcoustic, "IScalability" + "ChangedEventListener", "Prologue acoustic bridge has no legacy scalability listener interface", builder, ref failureCount);
                AssertNotContains(prologueAcoustic, "Scalability" + "Events.Register(this)", "Prologue acoustic bridge does not register with the scalability listener registry", builder, ref failureCount);
                AssertNotContains(prologueAcoustic, "Scalability" + "Events.Unregister(this)", "Prologue acoustic bridge does not unregister from the scalability listener registry", builder, ref failureCount);
                AssertNotContains(prologueAcoustic, "CacheQualityPolicy(payload.CurrentQualityTier, payload.CurrentTier, _lowMemoryProfile)", "Prologue acoustic bridge does not cache low-memory policy from scalability payloads", builder, ref failureCount);
                AssertContains(prologueColdRuntime, "GlobalRegistry.Audio", "Prologue acoustic bridge reads audio service only during cold runtime refresh", builder, ref failureCount);
                AssertContains(prologueColdRuntime, "GlobalRegistry.TickDispatcher", "Prologue acoustic bridge reads tick dispatcher only during cold runtime refresh", builder, ref failureCount);
                AssertContains(prologueQualityPolicy, "ResolveQualityTierByte(ResolveGlobalQualityWeight01())", "Prologue acoustic bridge refreshes continuous quality byte without binary tier state", builder, ref failureCount);
                AssertNotContains(prologueQualityPolicy, "GlobalRegistry.H8_LOW_MEMORY_PROFILE", "Prologue acoustic bridge does not seed low-memory policy during quality refresh", builder, ref failureCount);
                AssertNotContains(prologueLateFrame, "GlobalRegistry.", "Prologue acoustic LateFrameTick does not poll registry services directly", builder, ref failureCount);
                AssertNotContains(prologueLateFrame, "GlobalRegistry.ScalabilityTier", "Prologue acoustic LateFrameTick does not poll scalability tier registry directly", builder, ref failureCount);
                AssertNotContains(prologueLateFrame, "GlobalRegistry.ScalabilityTierProfileByte", "Prologue acoustic LateFrameTick does not poll scalability profile byte directly", builder, ref failureCount);
                AssertNotContains(prologueLateFrame, "GlobalRegistry.H8_LOW_MEMORY_PROFILE", "Prologue acoustic LateFrameTick does not poll low-memory registry directly", builder, ref failureCount);
                AssertNotContains(prologueLateFrame, "RefreshQualityTier", "Prologue acoustic LateFrameTick has no periodic registry quality refresh", builder, ref failureCount);
            }

            if (directorAI.Length > 0)
            {
                AssertContains(globalSignals, "[StructLayout(LayoutKind.Explicit, Size = 32)]", "DirectorAI music signal has explicit ARM64 layout with manual offsets", builder, ref failureCount);
                AssertContains(globalSignals, "public readonly struct DirectorAIMusicSignal : ISignal", "DirectorAI music cue is an immutable typed signal", builder, ref failureCount);
                AssertContains(directorAI, "SignalBus<DirectorAIMusicSignal>.TryPush(in signal)", "DirectorAI publishes music cues through the typed SignalBus lane with explicit drop semantics", builder, ref failureCount);
                AssertContains(directorAI, "PublishMusicSignal(ThreatSpikeEventType", "DirectorAI threat spikes publish typed music cues even without legacy listeners", builder, ref failureCount);
                AssertContains(directorAI, "PublishMusicSignal(PredatorPressureEventType", "DirectorAI predator pressure publishes typed music cues even without legacy listeners", builder, ref failureCount);
            }

            if (vocalWarning.Length > 0)
            {
                string vocalTick = ExtractMethodBody(vocalWarning, "public void Tick(float deltaTime)");
                string vocalSlowTick = ExtractMethodBody(vocalWarning, "public void SlowTick()");
                string vocalColdServices = ExtractMethodBody(vocalWarning, "private void RefreshCachedServicesCold()");
                AssertContains(vocalWarning, "ResolveGlobalQualityWeight01()", "Vocal warning system derives radio presentation from continuous global quality", builder, ref failureCount);
                AssertContains(vocalWarning, "ResolveRadioDistortion01(ref views, nextId)", "Vocal warning system resolves radio degradation through the warning payload path", builder, ref failureCount);
                AssertNotContains(vocalWarning, "ConsumeScalabilitySignals();", "Vocal warning system no longer drains binary scalability changes", builder, ref failureCount);
                AssertNotContains(vocalWarning, "ReadOnlySpan<ScalabilityChangedEvent> signals = SignalBus<ScalabilityChangedEvent>.GetFrameSnapshot();", "Vocal warning system avoids typed scalability snapshots for radio presentation", builder, ref failureCount);
                AssertNotContains(vocalWarning, "IScalability" + "ChangedEventListener", "Vocal warning system has no legacy scalability listener interface", builder, ref failureCount);
                AssertNotContains(vocalWarning, "Scalability" + "Events.Register(this)", "Vocal warning system does not register with the scalability listener registry", builder, ref failureCount);
                AssertNotContains(vocalWarning, "Scalability" + "Events.Unregister(this)", "Vocal warning system does not unregister from the scalability listener registry", builder, ref failureCount);
                AssertNotContains(vocalWarning, "private void HandleScalabilityChanged(in ScalabilityChangedEvent payload)", "Vocal warning quality no longer updates through typed scalability payloads", builder, ref failureCount);
                AssertContains(vocalColdServices, "GlobalRegistry.PlayerCriticalAudio", "Vocal warning renderer service is resolved only during cold cache refresh", builder, ref failureCount);
                AssertContains(vocalColdServices, "GlobalRegistry.Subtitles", "Vocal warning subtitles service is resolved only during cold cache refresh", builder, ref failureCount);
                AssertContains(vocalColdServices, "GlobalRegistry.Localization", "Vocal warning localization service is resolved only during cold cache refresh", builder, ref failureCount);
                AssertContains(vocalColdServices, "ResolveGlobalQualityWeight01()", "Vocal warning quality weight is seeded only during cold cache refresh", builder, ref failureCount);
                AssertNotContains(vocalColdServices, "GlobalRegistry.ScalabilityTier", "Vocal warning quality tier is not seeded from the hardware registry", builder, ref failureCount);
                AssertNotContains(vocalTick, "GlobalRegistry.", "Vocal warning Tick does not poll registry services directly", builder, ref failureCount);
                AssertNotContains(vocalSlowTick, "GlobalRegistry.", "Vocal warning SlowTick does not poll registry services directly", builder, ref failureCount);
                AssertNotContains(vocalTick, ".ToString(", "Vocal warning Tick has no string formatting", builder, ref failureCount);
                AssertNotContains(vocalSlowTick, ".ToString(", "Vocal warning SlowTick has no string formatting", builder, ref failureCount);
                AssertNotContains(vocalTick, "Debug.Log", "Vocal warning Tick has no debug log allocation path", builder, ref failureCount);
                AssertNotContains(vocalSlowTick, "Debug.Log", "Vocal warning SlowTick has no debug log allocation path", builder, ref failureCount);
            }

            if (playerThrusterAudio.Length > 0)
            {
                string thrusterColdRuntime = ExtractMethodBody(playerThrusterAudio, "private void RefreshRuntimeAudioServicesCold()");
                string thrusterMixerRoute = ExtractMethodBody(playerThrusterAudio, "private void TryAssignMixerRoute(");
                AssertContains(playerThrusterAudio, "IGlobalRegistryHotSwapRefListener", "Player thruster fallback audio listens for audio service rebinding", builder, ref failureCount);
                AssertContains(thrusterColdRuntime, "GlobalRegistry.Audio", "Player thruster fallback resolves audio service only during cold runtime refresh", builder, ref failureCount);
                AssertContains(playerThrusterAudio, "GlobalRegistryServiceSlot.Audio", "Player thruster fallback handles audio service hot swaps", builder, ref failureCount);
                AssertContains(thrusterMixerRoute, "_cachedSpatialAudioSfxRoute", "Player thruster mixer route uses cached spatial audio SFX route", builder, ref failureCount);
                AssertNotContains(thrusterMixerRoute, "GlobalRegistry.Audio", "Player thruster mixer route does not poll audio registry directly", builder, ref failureCount);
            }

            if (physicsApply.Length > 0)
            {
                AssertContains(physicsApply, "public readonly struct AcousticImpulseEvent", "Acoustic impulse event payload exists", builder, ref failureCount);
                AssertContains(physicsApply, "ForcePacketPriority.Critical", "Critical force packets are checked for acoustic routing", builder, ref failureCount);
                AssertContains(physicsApply, "ResolveKineticEnergyJoules", "Physics impulse energy resolver exists", builder, ref failureCount);
                AssertContains(physicsApply, "0.5f * math.max(0.0001f, massKg) * math.lengthsq(velocity)", "Kinetic energy uses 0.5*m*v^2", builder, ref failureCount);
                AssertContains(physicsApply, "ResolveAcousticImpulseVolume01", "Kinetic energy maps to audio volume", builder, ref failureCount);
                AssertContains(physicsApply, "Physics" + "Event" + "Bus.NotifyAcousticImpulse", "Critical force packets publish acoustic impulses", builder, ref failureCount);
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
                AssertContains(spectrumSystem, "Physics" + "Event" + "Bus.NotifyLargeAcousticImpulse(in impulseEvent)", "Active sonar aggro publishes LargeAcousticImpulseEvent", builder, ref failureCount);
                AssertContains(spectrumSystem, "private VaultGenerationHandle<uint> _aupDiscoveryGridHandle", "Sonar map persists a generation handle for the AUP discovery grid", builder, ref failureCount);
                AssertContains(spectrumSystem, "AupDiscoveryGridBufferId", "AUP discovery grid has a stable Vault BufferID", builder, ref failureCount);
                AssertContains(spectrumSystem, "TryResolveAupDiscoveryGrid(out NativeArray<uint>", "AUP discovery grid resolves through a method-local Vault view", builder, ref failureCount);
                AssertNotContains(spectrumSystem, "private NativeArray<uint> _aupDiscoveryGrid", "AUP discovery grid no longer persists a NativeArray alias", builder, ref failureCount);
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
                AssertContains(toolHaptics, "IPhysics" + "AcousticImpulseEventListener", "Tool haptics receive acoustic impulses", builder, ref failureCount);
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
                AssertContains(echolocationTranslator, "IPhysics" + "AcousticImpulseEventListener", "Echolocation HUD receives acoustic impulses", builder, ref failureCount);
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
            {
                AssertContains(eventsSource, "LeviathanRoar", "Procedural audio event kind routes Leviathan roar", builder, ref failureCount);
                AssertContains(eventsSource, "SignalBus<AudioEvent>.TryPush(in audioEvent)", "Procedural audio event source publishes through the typed SignalBus lane with explicit drop semantics", builder, ref failureCount);
                AssertContains(eventsSource, "GlobalSignals.InitializeAllQueues()", "Procedural audio event source enters through central signal lane authority", builder, ref failureCount);
            }

            if (globalSignals.Length > 0)
            {
                AssertContains(globalSignals, "public struct AudioEvent : ISignal", "Procedural audio event payload is a typed SignalBus payload", builder, ref failureCount);
                AssertContains(globalSignals, "SignalBus<global::Hecton8.Core.Contracts.Signals.AudioEvent>.Configure", "Procedural audio typed lane has a central stable lane policy", builder, ref failureCount);
                AssertContains(globalSignals, "laneHash: 0x41554445u", "Procedural audio typed lane preserves the AUDE stable hash", builder, ref failureCount);
            }

            if (acousticZone.Length > 0)
            {
                string acousticPlayMadness = ExtractMethodBody(acousticZone, "public void PlayMadnessWhisperCue()");
                string acousticEmitterOcclusion = ExtractMethodBody(acousticZone, "private void UpdateEmitterOcclusionState(AudioListener listener)");
                AssertContains(globalSignals, "[StructLayout(LayoutKind.Explicit, Size = 16)]", "Acoustic-zone typed signal has explicit ARM64 layout with manual offsets", builder, ref failureCount);
                AssertContains(globalSignals, "public readonly struct AcousticZoneChangedEvent : ISignal", "Acoustic-zone transition payload is an immutable typed signal", builder, ref failureCount);
                AssertContains(globalSignals, "[FieldOffset(0)] public readonly byte IsInterior", "Acoustic-zone payload avoids bool field layout ambiguity", builder, ref failureCount);
                AssertContains(acousticZone, "SignalBus<AcousticZoneChangedEvent>.TryPush(in payload)", "Acoustic-zone transitions publish through the typed SignalBus lane with explicit drop semantics", builder, ref failureCount);
                AssertContains(acousticZone, "SignalBus<AcousticZoneChangedEvent>.SnapshotCount", "Acoustic-zone pending telemetry reads typed-lane snapshot state", builder, ref failureCount);
                AssertContains(acousticZone, "SignalBus<AcousticZoneChangedEvent>.DroppedLastFlush", "Acoustic-zone drop telemetry reads typed-lane drop state", builder, ref failureCount);
                AssertContains(acousticZone, "SignalBus<AcousticZoneChangedEvent>.EnsureInitialized()", "Acoustic-zone facade initializes the typed SignalBus lane", builder, ref failureCount);
                AssertNotContains(acousticZone, "IAcoustic" + "ZoneEventListener", "Acoustic-zone facade exposes no legacy listener interface", builder, ref failureCount);
                AssertNotContains(acousticZone, "RegistryBucket<IAcoustic" + "ZoneEventListener>", "Acoustic-zone facade has no managed listener registry", builder, ref failureCount);
                AssertNotContains(acousticZone, "Native" + "Queue<AcousticZoneChangedEvent>", "Acoustic-zone facade owns no private native queue", builder, ref failureCount);
                AssertNotContains(acousticZone, "_pending" + "ZoneChanges", "Acoustic-zone facade has no local pending queue", builder, ref failureCount);
                AssertNotContains(acousticZone, "_nextFrame" + "ZoneChanges", "Acoustic-zone facade has no reentrant local queue", builder, ref failureCount);
                AssertContains(acousticZone, "AudioServiceResolveRetryFrames = 30", "Acoustic-zone audio service lookup is cadence-gated", builder, ref failureCount);
                AssertContains(acousticZone, "ResolveAudioService()", "Acoustic-zone cue playback uses cached audio-service resolution", builder, ref failureCount);
                AssertContains(acousticZone, "ResolveSpatialAudioEmitterReadModel()", "Acoustic-zone emitter occlusion uses cached spatial-audio read-model resolution", builder, ref failureCount);
                AssertContains(acousticZone, "ClearCachedRegistryServices()", "Acoustic-zone clears cached registry services on disable/destroy", builder, ref failureCount);
                AssertNotContains(acousticPlayMadness, "GlobalRegistry.Audio", "Acoustic-zone madness cue does not poll audio registry directly", builder, ref failureCount);
                AssertNotContains(acousticEmitterOcclusion, "GlobalRegistry.Audio", "Acoustic-zone emitter occlusion does not poll audio registry directly", builder, ref failureCount);
            }

            if (audioLogEvents.Length > 0)
            {
                string audioLogEnqueue = ExtractMethodBody(audioLogEvents, "private static bool Enqueue(");
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
