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
        private const string GameBootstrapperPath = "Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs";
        private const string MainMenuScenePath = "Assets/_Project/Scenes/01_MAIN_MENU.unity";
        private const string MasterMixerPath = "Assets/_Project/MasterMixer.mixer";
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
        private const string VehicleSubOsCockpitRuntimePath = "Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs";
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
        private const string AudioLogSystemPath = "Assets/_Project/Scripts/AudioLog/AudioLogSystem.cs";
        private const string SceneRuntimeServicePath = "Assets/_Project/Scripts/Core/SceneRuntimeService.cs";
        private const string HectonNarrativeDirectorPath = "Assets/_Project/Scripts/HectonNarrativeDirector.cs";
        private const string TraumaDispatcherPath = "Assets/_Project/Scripts/Gameplay/TraumaDispatcher.cs";
        private const string RandomEventSystemPath = "Assets/_Project/Scripts/Gameplay/RandomEventSystem.cs";
        private const string EclipseGameplaySystemPath = "Assets/_Project/Scripts/Gameplay/EclipseGameplaySystem.cs";
        private const string FirstHourDirectorPath = "Assets/_Project/Scripts/Gameplay/FirstHourDirector.cs";
        private const string EmergencyServiceRelayPath = "Assets/_Project/Scripts/World/EmergencyServiceRelay.cs";
        private const string NarrativeDiscoveryPath = "Assets/_Project/Scripts/NarrativeDiscovery.cs";
        private const string HectonPlayerHealthPath = "Assets/_Project/Scripts/Gameplay/HectonPlayerHealth.cs";
        private const string HectonPlayerMovementPath = "Assets/_Project/Scripts/HectonPlayerMovement.cs";
        private const string SubmarineAtmosphereSystemPath = "Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs";
        private const string SignalBeaconPath = "Assets/_Project/Scripts/AtlasSignal/SignalBeacon.cs";
        private const string AtlasSignalSystemPath = "Assets/_Project/Scripts/AtlasSignal/AtlasSignalSystem.cs";
        private const string Atlas6CorporateLiabilityManagerPath = "Assets/_Project/Scripts/Gameplay/Atlas6Liability/Atlas6CorporateLiabilityManager.cs";
        private const string NarrativeProgressionBridgePath = "Assets/_Project/Scripts/Progression/NarrativeProgressionBridge.cs";
        private const string PlayerFlashlightPath = "Assets/_Project/Scripts/PlayerFlashlight.cs";
        private const string PlayerPdaPath = "Assets/_Project/Scripts/PlayerPDA.cs";
        private const string PlayerInventoryPath = "Assets/_Project/Scripts/PlayerInventory.cs";
        private const string PDAInventoryTabPath = "Assets/_Project/Scripts/PDAInventoryTab.cs";
        private const string PDAMapTabPath = "Assets/_Project/Scripts/UI/PDAMapTab.cs";
        private const string PlayerStressVfxPath = "Assets/_Project/Scripts/Visor/PlayerStressVFX.cs";
        private const string DeepPsychosisPath = "Assets/_Project/Scripts/Audio/DeepPsychosisController.cs";
        private const string HectonMusicDirectorPath = "Assets/_Project/Scripts/Audio/HectonMusicDirector.cs";
        private const string HectonMusicDirectorConfigPath = "Assets/_Project/Data/Audio/Music/Configs/MusicDirectorConfig_Global.asset";
        private const string SoundscapeSystemPath = "Assets/_Project/Scripts/World/SoundscapeSystem.cs";
        private const string DestructibleOrganicManagerPath = "Assets/_Project/Scripts/World/DestructibleOrganicManager.cs";
        private const string SettingsManagerPath = "Assets/_Project/Scripts/UI/SettingsManager.cs";
        private const string AdaptiveStemMixerPath = "Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs";
        private const string DynamicMusicGranularSynthPath = "Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs";
        private const string DirectorAIPath = "Assets/_Project/Scripts/HectonDirectorAI.cs";
        private const string PrologueAcousticOrchestratorPath = "Assets/_Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs";
        private const string VocalWarningSystemPath = "Assets/_Project/Scripts/Audio/VocalWarningSystem.cs";
        private const string VocalBankRuntimePath = "Assets/_Project/Scripts/Audio/Synthesis/VocalBankPlaybackRuntime.cs";
        private const string PlayerThrusterAudioPath = "Assets/_Project/Scripts/PlayerThrusterAudio.cs";
        private const string PlayerFootstepAudioPath = "Assets/_Project/Scripts/PlayerFootstepAudio.cs";
        private const string PhysicalPanelDialPath = "Assets/_Project/Scripts/UI/PhysicalPanelDial.cs";
        private const string PhysicalTerminalKeyboardPath = "Assets/_Project/Scripts/UI/PhysicalTerminalKeyboard.cs";
        private const string PhysicalPanelButtonPath = "Assets/_Project/Scripts/UI/PhysicalPanelButton.cs";
        private const string SuitAdvisoryControllerPath = "Assets/_Project/Scripts/UI/SuitAdvisoryController.cs";
        private const string UIAudioFeedbackPath = "Assets/_Project/Scripts/UI/UIAudioFeedback.cs";
        private const string UIButtonAudioTriggerPath = "Assets/_Project/Scripts/UI/UIButtonAudioTrigger.cs";
        private const string SurfaceWeatherDirectorPath = "Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs";
        private const string HectonUnderwaterVisualsPath = "Assets/_Project/Scripts/HectonUnderwaterVisuals.cs";
        private const string PlayerInteractionPath = "Assets/_Project/Scripts/Interaction/PlayerInteraction.cs";
        private const string SaveStationPath = "Assets/_Project/Scripts/Interaction/SaveStation.cs";
        private const string PhysicalSnapSwitchPath = "Assets/_Project/Scripts/Interaction/PhysicalSnapSwitch.cs";
        private const string OxygenPlantPath = "Assets/_Project/Scripts/Gameplay/OxygenPlant.cs";
        private const string OxygenBubblePath = "Assets/_Project/Scripts/Gameplay/OxygenBubble.cs";
        private const string StorageCratePath = "Assets/_Project/Scripts/Gameplay/StorageCrate.cs";
        private const string MessageTerminalPath = "Assets/_Project/Scripts/Gameplay/MessageTerminal.cs";
        private const string PlayerActionControllerPath = "Assets/_Project/Scripts/Gameplay/PlayerActionController.cs";
        private const string ScannableFragmentPath = "Assets/_Project/Scripts/Gameplay/ScannableFragment.cs";
        private const string ClimbableLadderPath = "Assets/_Project/Scripts/Gameplay/ClimbableLadder.cs";
        private const string BioReactorPath = "Assets/_Project/Scripts/Gameplay/BioReactor.cs";
        private const string HarvestablePlantPath = "Assets/_Project/Scripts/Gameplay/HarvestablePlant.cs";
        private const string HarvestableOutcropPath = "Assets/_Project/Scripts/Gameplay/HarvestableOutcrop.cs";
        private const string HostileFloraPath = "Assets/_Project/Scripts/Gameplay/HostileFlora.cs";
        private const string FloaterPath = "Assets/_Project/Scripts/Gameplay/Floater.cs";
        private const string DeployableBeaconPath = "Assets/_Project/Scripts/Gameplay/DeployableBeacon.cs";
        private const string MountablePlayerTransportPath = "Assets/_Project/Scripts/Gameplay/MountablePlayerTransport.cs";
        private const string DropPodSeatControllerPath = "Assets/_Project/Scripts/Vehicles/DropPod/DropPodSeatController.cs";
        private const string DropPodDashboardToggleSwitchPath = "Assets/_Project/Scripts/Vehicles/DropPod/DropPodDashboardToggleSwitch.cs";
        private const string DropPodAirlockControllerPath = "Assets/_Project/Scripts/Vehicles/DropPod/DropPodAirlockController.cs";
        private const string BaseAirlockPath = "Assets/_Project/Scripts/Gameplay/BaseAirlock.cs";
        private const string BatteryChargerPath = "Assets/_Project/Scripts/Gameplay/BatteryCharger.cs";
        private const string SealedDoorPath = "Assets/_Project/Scripts/Gameplay/SealedDoor.cs";
        private const string ConstructionManagerPath = "Assets/_Project/Scripts/ConstructionManager.cs";
        private const string HabitatGraphManagerPath = "Assets/_Project/Scripts/Construction/HabitatGraphManager.cs";
        private const string FabricatorPath = "Assets/_Project/Scripts/Fabricator.cs";
        private const string BaseModulePath = "Assets/_Project/Scripts/BaseModule.cs";
        private const string RepairToolPath = "Assets/_Project/Scripts/RepairTool.cs";
        private const string PlayerBuilderPath = "Assets/_Project/Scripts/PlayerBuilder.cs";
        private const string LaserCutterPath = "Assets/_Project/Scripts/LaserCutter.cs";
        private const string SubmarineAutoLevelBallastControllerPath = "Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs";
        private const string IndirectVegetationRendererPath = "Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs";
        private const string ModCommandDispatcherPath = "Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs";
        private const string DiegeticPanelControllerPath = "Assets/_Project/Scripts/UI/DiegeticPanelController.cs";
        private const string SuitHudOverlayPath = "Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs";
        private const string AcousticRadarSphereRendererPath = "Assets/_Project/Scripts/UI/AcousticRadarSphereRenderer.cs";
        private const string SonarHoloCompassPath = "Assets/_Project/Scripts/UI/SonarHoloCompass.cs";
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
            string gameBootstrapper = ReadAssetText(GameBootstrapperPath, builder, ref failureCount);
            string mainMenuScene = ReadAssetText(MainMenuScenePath, builder, ref failureCount);
            string masterMixer = ReadAssetText(MasterMixerPath, builder, ref failureCount);
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
            string vehicleSubOsCockpit = ReadAssetText(VehicleSubOsCockpitRuntimePath, builder, ref failureCount);
            string echolocationTranslator = ReadAssetText(EcholocationTranslatorPath, builder, ref failureCount);
            string globalRegistry = ReadAssetText(GlobalRegistryPath, builder, ref failureCount);
            string globalRegistryContracts = ReadAssetText(GlobalRegistryContractsPath, builder, ref failureCount);
            string modCommandDispatcher = ReadAssetText(ModCommandDispatcherPath, builder, ref failureCount);
            string occlusion = ReadAssetText(OcclusionPath, builder, ref failureCount);
            string ringBuffer = ReadAssetText(RingBufferPath, builder, ref failureCount);
            string synthesis = ReadAssetText(SynthesisPath, builder, ref failureCount);
            string telemetry = ReadAssetText(TelemetryPath, builder, ref failureCount);
            string eventsSource = ReadAssetText(EventsPath, builder, ref failureCount);
            string globalSignals = ReadAssetText(GlobalSignalsPath, builder, ref failureCount);
            string acousticZone = ReadAssetText(AcousticZonePath, builder, ref failureCount);
            string audioLogEvents = ReadAssetText(AudioLogEventsPath, builder, ref failureCount);
            string audioLogSystem = ReadAssetText(AudioLogSystemPath, builder, ref failureCount);
            string sceneRuntime = ReadAssetText(SceneRuntimeServicePath, builder, ref failureCount);
            string narrativeDirector = ReadAssetText(HectonNarrativeDirectorPath, builder, ref failureCount);
            string traumaDispatcher = ReadAssetText(TraumaDispatcherPath, builder, ref failureCount);
            string randomEventSystem = ReadAssetText(RandomEventSystemPath, builder, ref failureCount);
            string eclipseGameplaySystem = ReadAssetText(EclipseGameplaySystemPath, builder, ref failureCount);
            string firstHourDirector = ReadAssetText(FirstHourDirectorPath, builder, ref failureCount);
            string emergencyServiceRelay = ReadAssetText(EmergencyServiceRelayPath, builder, ref failureCount);
            string narrativeDiscovery = ReadAssetText(NarrativeDiscoveryPath, builder, ref failureCount);
            string playerHealth = ReadAssetText(HectonPlayerHealthPath, builder, ref failureCount);
            string playerMovement = ReadAssetText(HectonPlayerMovementPath, builder, ref failureCount);
            string submarineAtmosphere = ReadAssetText(SubmarineAtmosphereSystemPath, builder, ref failureCount);
            string signalBeacon = ReadAssetText(SignalBeaconPath, builder, ref failureCount);
            string atlasSignalSystem = ReadAssetText(AtlasSignalSystemPath, builder, ref failureCount);
            string atlas6CorporateLiabilityManager = ReadAssetText(Atlas6CorporateLiabilityManagerPath, builder, ref failureCount);
            string narrativeProgressionBridge = ReadAssetText(NarrativeProgressionBridgePath, builder, ref failureCount);
            string playerFlashlight = ReadAssetText(PlayerFlashlightPath, builder, ref failureCount);
            string playerPda = ReadAssetText(PlayerPdaPath, builder, ref failureCount);
            string playerInventory = ReadAssetText(PlayerInventoryPath, builder, ref failureCount);
            string pdaInventoryTab = ReadAssetText(PDAInventoryTabPath, builder, ref failureCount);
            string pdaMapTab = ReadAssetText(PDAMapTabPath, builder, ref failureCount);
            string baseAirlock = ReadAssetText(BaseAirlockPath, builder, ref failureCount);
            string batteryCharger = ReadAssetText(BatteryChargerPath, builder, ref failureCount);
            string sealedDoor = ReadAssetText(SealedDoorPath, builder, ref failureCount);
            string constructionManager = ReadAssetText(ConstructionManagerPath, builder, ref failureCount);
            string habitatGraphManager = ReadAssetText(HabitatGraphManagerPath, builder, ref failureCount);
            string fabricator = ReadAssetText(FabricatorPath, builder, ref failureCount);
            string baseModule = ReadAssetText(BaseModulePath, builder, ref failureCount);
            string repairTool = ReadAssetText(RepairToolPath, builder, ref failureCount);
            string playerBuilder = ReadAssetText(PlayerBuilderPath, builder, ref failureCount);
            string submarineAutoLevelBallast = ReadAssetText(SubmarineAutoLevelBallastControllerPath, builder, ref failureCount);
            string playerStressVfx = ReadAssetText(PlayerStressVfxPath, builder, ref failureCount);
            string deepPsychosis = ReadAssetText(DeepPsychosisPath, builder, ref failureCount);
            string musicDirector = ReadAssetText(HectonMusicDirectorPath, builder, ref failureCount);
            string musicDirectorConfig = ReadAssetText(HectonMusicDirectorConfigPath, builder, ref failureCount);
            string soundscapeSystem = ReadAssetText(SoundscapeSystemPath, builder, ref failureCount);
            string destructibleOrganicManager = ReadAssetText(DestructibleOrganicManagerPath, builder, ref failureCount);
            string settingsManager = ReadAssetText(SettingsManagerPath, builder, ref failureCount);
            string adaptiveStemMixer = ReadAssetText(AdaptiveStemMixerPath, builder, ref failureCount);
            string dynamicMusicSynth = ReadAssetText(DynamicMusicGranularSynthPath, builder, ref failureCount);
            string directorAI = ReadAssetText(DirectorAIPath, builder, ref failureCount);
            string prologueAcoustic = ReadAssetText(PrologueAcousticOrchestratorPath, builder, ref failureCount);
            string vocalWarning = ReadAssetText(VocalWarningSystemPath, builder, ref failureCount);
            string vocalBankRuntime = ReadAssetText(VocalBankRuntimePath, builder, ref failureCount);
            string playerThrusterAudio = ReadAssetText(PlayerThrusterAudioPath, builder, ref failureCount);
            string playerFootstepAudio = ReadAssetText(PlayerFootstepAudioPath, builder, ref failureCount);
            string physicalPanelDial = ReadAssetText(PhysicalPanelDialPath, builder, ref failureCount);
            string physicalTerminalKeyboard = ReadAssetText(PhysicalTerminalKeyboardPath, builder, ref failureCount);
            string physicalPanelButton = ReadAssetText(PhysicalPanelButtonPath, builder, ref failureCount);
            string suitAdvisory = ReadAssetText(SuitAdvisoryControllerPath, builder, ref failureCount);
            string uiAudioFeedback = ReadAssetText(UIAudioFeedbackPath, builder, ref failureCount);
            string uiButtonAudioTrigger = ReadAssetText(UIButtonAudioTriggerPath, builder, ref failureCount);
            string surfaceWeatherDirector = ReadAssetText(SurfaceWeatherDirectorPath, builder, ref failureCount);
            string underwaterVisuals = ReadAssetText(HectonUnderwaterVisualsPath, builder, ref failureCount);
            string playerInteraction = ReadAssetText(PlayerInteractionPath, builder, ref failureCount);
            string saveStation = ReadAssetText(SaveStationPath, builder, ref failureCount);
            string physicalSnapSwitch = ReadAssetText(PhysicalSnapSwitchPath, builder, ref failureCount);
            string oxygenPlant = ReadAssetText(OxygenPlantPath, builder, ref failureCount);
            string oxygenBubble = ReadAssetText(OxygenBubblePath, builder, ref failureCount);
            string storageCrate = ReadAssetText(StorageCratePath, builder, ref failureCount);
            string messageTerminal = ReadAssetText(MessageTerminalPath, builder, ref failureCount);
            string playerAction = ReadAssetText(PlayerActionControllerPath, builder, ref failureCount);
            string scannableFragment = ReadAssetText(ScannableFragmentPath, builder, ref failureCount);
            string climbableLadder = ReadAssetText(ClimbableLadderPath, builder, ref failureCount);
            string bioReactor = ReadAssetText(BioReactorPath, builder, ref failureCount);
            string harvestablePlant = ReadAssetText(HarvestablePlantPath, builder, ref failureCount);
            string harvestableOutcrop = ReadAssetText(HarvestableOutcropPath, builder, ref failureCount);
            string hostileFlora = ReadAssetText(HostileFloraPath, builder, ref failureCount);
            string floater = ReadAssetText(FloaterPath, builder, ref failureCount);
            string deployableBeacon = ReadAssetText(DeployableBeaconPath, builder, ref failureCount);
            string mountablePlayerTransport = ReadAssetText(MountablePlayerTransportPath, builder, ref failureCount);
            string dropPodSeat = ReadAssetText(DropPodSeatControllerPath, builder, ref failureCount);
            string dropPodToggle = ReadAssetText(DropPodDashboardToggleSwitchPath, builder, ref failureCount);
            string dropPodAirlock = ReadAssetText(DropPodAirlockControllerPath, builder, ref failureCount);
            string laserCutter = ReadAssetText(LaserCutterPath, builder, ref failureCount);
            string indirectVegetationRenderer = ReadAssetText(IndirectVegetationRendererPath, builder, ref failureCount);
            string diegeticPanelController = ReadAssetText(DiegeticPanelControllerPath, builder, ref failureCount);
            string suitHudOverlay = ReadAssetText(SuitHudOverlayPath, builder, ref failureCount);
            string acousticRadarSphereRenderer = ReadAssetText(AcousticRadarSphereRendererPath, builder, ref failureCount);
            string sonarHoloCompass = ReadAssetText(SonarHoloCompassPath, builder, ref failureCount);
            string diegeticTooltipSystem = ReadAssetText(DiegeticTooltipSystemPath, builder, ref failureCount);

            if (gameBootstrapper.Length > 0)
            {
                string heartbeatReady = ExtractMethodBody(gameBootstrapper, "private static bool IsBootstrapDependencyHeartbeatReady(");
                string nodeReady = ExtractMethodBody(gameBootstrapper, "private static bool IsBootstrapDependencyNodeReady(BootstrapDependencyNode node, object service)");
                string initializeAudio = ExtractMethodBody(gameBootstrapper, "private static bool InitializeSpatialAudioBootstrapNode()");
                string audioFallback = ExtractMethodBody(gameBootstrapper, "private static bool TryRegisterNoOpAudioFallback(");
                string audioUsable = ExtractMethodBody(gameBootstrapper, "private static bool IsBootstrapAudioServiceUsable(");
                AssertContains(heartbeatReady, "if (node == BootstrapDependencyNode.SpatialAudioManager)", "Bootstrap heartbeat readiness handles spatial audio before generic heartbeat", builder, ref failureCount);
                AssertContains(heartbeatReady, "return _headlessBootMode || IsBootstrapAudioServiceUsable(service as IAudioService)", "Bootstrap heartbeat readiness validates spatial audio usability", builder, ref failureCount);
                AssertTextBefore(heartbeatReady, "if (node == BootstrapDependencyNode.SpatialAudioManager)", "if (service is IServiceHeartbeat heartbeat)", "Bootstrap spatial audio readiness is checked before generic heartbeat readiness", builder, ref failureCount);
                AssertContains(nodeReady, "case BootstrapDependencyNode.SpatialAudioManager:", "Bootstrap dependency node readiness has a spatial audio branch", builder, ref failureCount);
                AssertContains(nodeReady, "return _headlessBootMode || IsBootstrapAudioServiceUsable(service as IAudioService)", "Bootstrap dependency node readiness validates spatial audio usability", builder, ref failureCount);
                AssertContains(initializeAudio, "IsBootstrapAudioServiceUsable(GlobalRegistry.Audio)", "Bootstrap audio node validates registered audio service usability", builder, ref failureCount);
                AssertNotContains(initializeAudio, "GlobalRegistry.Audio != null", "Bootstrap audio node does not accept raw non-null audio registration", builder, ref failureCount);
                AssertContains(audioFallback, "return IsBootstrapAudioServiceUsable(audioService)", "NoOp audio fallback validates the final registered audio service", builder, ref failureCount);
                AssertNotContains(audioFallback, "return audioService != null", "NoOp audio fallback does not report raw non-null audio as ready", builder, ref failureCount);
                AssertContains(audioUsable, "audioService == null || !audioService.IsInitialized", "Bootstrap audio usability rejects uninitialized services", builder, ref failureCount);
                AssertContains(audioUsable, "audioService is Behaviour behaviour", "Bootstrap audio usability checks Unity component liveness", builder, ref failureCount);
                AssertContains(audioUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Bootstrap audio usability rejects destroyed or disabled components", builder, ref failureCount);
            }

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
                string spatialReboundRuntimeServices = ExtractMethodBody(spatial, "private void CacheReboundAudioRuntimeService(");
                string spatialRuntimeRegister = ExtractMethodBody(spatial, "private bool TryRegisterAudioRuntimeServices()");
                string spatialAudioOwnerUsable = ExtractMethodBody(spatial, "private static bool IsAudioServiceOwnerUsable(");
                string spatialVirtualizationOwnerUsable = ExtractMethodBody(spatial, "private static bool IsAudioVirtualizationOwnerUsable(");
                string spatialPlayerCriticalCache = ExtractMethodBody(spatial, "private void CachePlayerCriticalAudio(");
                string spatialPlayerCriticalResolve = ExtractMethodBody(spatial, "private IPlayerCriticalAudioSignalSink ResolvePlayerCriticalAudioSignalSink()");
                string spatialPlayerCriticalUsable = ExtractMethodBody(spatial, "private static bool IsPlayerCriticalAudioSignalSinkUsable(");
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
                AssertContains(spatialRuntimeRegister, "IAudioService registeredAudioService = GlobalRegistry.Audio", "Spatial audio snapshots the current audio owner once", builder, ref failureCount);
                AssertContains(spatialRuntimeRegister, "IAudioVirtualizationService registeredVirtualization = GlobalRegistry.AudioVirtualization", "Spatial audio snapshots the current virtualization owner once", builder, ref failureCount);
                AssertContains(spatialRuntimeRegister, "if (IsAudioServiceOwnerUsable(registeredAudioService))", "Spatial audio preserves usable existing audio owners", builder, ref failureCount);
                AssertContains(spatialRuntimeRegister, "if (IsAudioVirtualizationOwnerUsable(registeredVirtualization))", "Spatial audio preserves usable existing virtualization owners", builder, ref failureCount);
                AssertContains(spatialRuntimeRegister, "RestoreActiveRuntimeInstanceFromOwner(registeredAudioService)", "Spatial audio restores the active runtime pointer when a usable audio duplicate wins", builder, ref failureCount);
                AssertContains(spatialRuntimeRegister, "RestoreActiveRuntimeInstanceFromOwner(registeredVirtualization)", "Spatial audio restores the active runtime pointer when a usable virtualization duplicate wins", builder, ref failureCount);
                AssertContains(spatialRuntimeRegister, "GlobalRegistry.UnregisterAudioService(registeredAudioService);", "Spatial audio clears stale audio owners before strict register", builder, ref failureCount);
                AssertContains(spatialRuntimeRegister, "GlobalRegistry.UnregisterAudioVirtualizationService(registeredVirtualization);", "Spatial audio clears stale virtualization owners before strict register", builder, ref failureCount);
                AssertTextBefore(spatialRuntimeRegister, "GlobalRegistry.UnregisterAudioService(registeredAudioService);", "GlobalRegistry.RegisterAudioService(this);", "Spatial audio unregisters stale audio owners before self-register", builder, ref failureCount);
                AssertTextBefore(spatialRuntimeRegister, "GlobalRegistry.UnregisterAudioVirtualizationService(registeredVirtualization);", "GlobalRegistry.RegisterAudioVirtualizationService(this);", "Spatial audio unregisters stale virtualization owners before self-register", builder, ref failureCount);
                AssertContains(spatialRuntimeRegister, "return ReferenceEquals(GlobalRegistry.Audio, this) &&", "Spatial audio verifies both registry slots after strict register", builder, ref failureCount);
                AssertContains(spatialAudioOwnerUsable, "ReferenceEquals(audioService, null)", "Spatial audio owner usability rejects missing audio owners", builder, ref failureCount);
                AssertContains(spatialAudioOwnerUsable, "audioService is Behaviour behaviour", "Spatial audio owner usability validates MonoBehaviour-backed audio owners", builder, ref failureCount);
                AssertContains(spatialAudioOwnerUsable, "return audioService.IsInitialized", "Spatial audio owner usability requires initialized audio service owners", builder, ref failureCount);
                AssertContains(spatialVirtualizationOwnerUsable, "ReferenceEquals(virtualization, null)", "Spatial audio virtualization owner usability rejects missing owners", builder, ref failureCount);
                AssertContains(spatialVirtualizationOwnerUsable, "virtualization is Behaviour behaviour", "Spatial audio virtualization owner usability validates MonoBehaviour-backed owners", builder, ref failureCount);
                AssertContains(spatialVirtualizationOwnerUsable, "return virtualization.IsVirtualizationReady", "Spatial audio virtualization owner usability requires ready virtual voice storage", builder, ref failureCount);
                AssertContains(spatial, "public void OnGlobalRegistryServiceRebound(", "Spatial audio receives ref-forwarded service rebinds", builder, ref failureCount);
                AssertContains(spatial, "GlobalRegistryServiceSlot.PlayerCriticalAudioRuntime", "Spatial audio listens for player-critical audio runtime rebinding", builder, ref failureCount);
                AssertContains(spatialReboundRuntimeServices, "CachePlayerCriticalAudio(currentService as IPlayerCriticalAudioSignalSink)", "Spatial audio hot-swap player-critical DSP sink passes through the usable-runtime filter", builder, ref failureCount);
                AssertContains(spatialColdRuntimeServices, "CachePlayerCriticalAudio(GlobalRegistry.PlayerCriticalAudioSignals)", "Spatial audio seeds player-critical DSP sink only through the usable-runtime filter", builder, ref failureCount);
                AssertContains(spatialPlayerCriticalCache, "_cachedPlayerCriticalAudio = IsPlayerCriticalAudioSignalSinkUsable(playerCriticalAudio)", "Spatial audio stores only usable player-critical DSP sinks", builder, ref failureCount);
                AssertContains(spatialPlayerCriticalResolve, "if (IsPlayerCriticalAudioSignalSinkUsable(playerCriticalAudio))", "Spatial audio resolves player-critical DSP sink only while usable", builder, ref failureCount);
                AssertContains(spatialPlayerCriticalResolve, "_cachedPlayerCriticalAudio = null", "Spatial audio clears stale player-critical DSP sink references", builder, ref failureCount);
                AssertContains(spatialPlayerCriticalUsable, "playerCriticalAudio is Behaviour behaviour", "Spatial audio validates MonoBehaviour-backed player-critical DSP sinks", builder, ref failureCount);
                AssertContains(spatialPlayerCriticalUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Spatial audio rejects destroyed or disabled player-critical DSP sinks", builder, ref failureCount);
                AssertContains(spatialPrologueQueue, "IPlayerCriticalAudioSignalSink playerCriticalAudio = ResolvePlayerCriticalAudioSignalSink()", "Prologue audio transition queue uses the usable player-critical resolver", builder, ref failureCount);
                AssertContains(spatialHighSpeedQueue, "IPlayerCriticalAudioSignalSink renderer = ResolvePlayerCriticalAudioSignalSink()", "High-speed impact queue uses the usable player-critical resolver", builder, ref failureCount);
                AssertNotContains(spatialPrologueQueue, "GlobalRegistry.", "Prologue audio transition queue uses cached player-critical runtime", builder, ref failureCount);
                AssertNotContains(spatialPrologueQueue, "_cachedPlayerCriticalAudio", "Prologue audio transition queue does not trust the raw player-critical cache", builder, ref failureCount);
                AssertNotContains(spatialHighSpeedQueue, "GlobalRegistry.", "High-speed impact queue uses cached player-critical runtime", builder, ref failureCount);
                AssertNotContains(spatialHighSpeedQueue, "_cachedPlayerCriticalAudio", "High-speed impact queue does not trust the raw player-critical cache", builder, ref failureCount);
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
                string rendererAudioRuntimeCache = ExtractMethodBody(renderer, "private void CacheAudioRuntimeService(");
                string rendererCaveReadModelResolver = ExtractMethodBody(renderer, "private ISpatialAudioListenerCaveReadModel ResolveSpatialAudioListenerCaveReadModel()");
                string rendererBinauralReadModelResolver = ExtractMethodBody(renderer, "private ISpatialAudioBinauralEmitterReadModel ResolveSpatialAudioBinauralEmitterReadModel()");
                string rendererAudioUsable = ExtractMethodBody(renderer, "private static bool IsAudioServiceUsable(");
                string rendererAudioObjectUsable = ExtractMethodBody(renderer, "private static bool IsAudioRuntimeObjectUsable(");
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
                AssertContains(rendererRuntimeRegister, "PlayerCriticalProceduralAudioRenderer registeredInstance = GlobalRegistry.PlayerCriticalAudio", "Critical renderer snapshots the current registry owner once", builder, ref failureCount);
                AssertContains(rendererRuntimeRegister, "!ReferenceEquals(registeredInstance, null)", "Critical renderer detects stale destroyed registry references by actual reference", builder, ref failureCount);
                AssertContains(rendererRuntimeRegister, "!ReferenceEquals(registeredInstance, this)", "Critical renderer treats only other owners as registry conflicts", builder, ref failureCount);
                AssertContains(rendererRuntimeRegister, "if (IsPlayerCriticalAudioRuntimeUsable(registeredInstance))", "Critical renderer preserves usable existing runtime owners", builder, ref failureCount);
                AssertContains(rendererRuntimeRegister, "Destroy(this);", "Critical renderer destroys duplicate components only when the existing owner is usable", builder, ref failureCount);
                AssertContains(rendererRuntimeRegister, "GlobalRegistry.UnregisterPlayerCriticalAudioRuntime(registeredInstance);", "Critical renderer clears stale existing owners before registering", builder, ref failureCount);
                AssertTextBefore(rendererRuntimeRegister, "GlobalRegistry.UnregisterPlayerCriticalAudioRuntime(registeredInstance);", "GlobalRegistry.RegisterPlayerCriticalAudioRuntime(this);", "Critical renderer unregisters stale owners before self-register", builder, ref failureCount);
                AssertContains(renderer, "return renderer != null && renderer.isActiveAndEnabled", "Critical renderer owner usability rejects destroyed or disabled owners", builder, ref failureCount);
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
                AssertContains(rendererAudioRuntimeCache, "bool isUsable = IsAudioServiceUsable(audioService)", "Critical renderer stores spatial audio read-models only from usable audio services", builder, ref failureCount);
                AssertContains(rendererAudioRuntimeCache, "_spatialAudioListenerCaveReadModel = isUsable ? audioService as ISpatialAudioListenerCaveReadModel : null", "Critical renderer cave read-model cache is gated by usable audio service", builder, ref failureCount);
                AssertContains(rendererAudioRuntimeCache, "_spatialAudioBinauralEmitterReadModel = isUsable ? audioService as ISpatialAudioBinauralEmitterReadModel : null", "Critical renderer binaural read-model cache is gated by usable audio service", builder, ref failureCount);
                AssertContains(rendererCaveReadModelResolver, "if (IsAudioRuntimeObjectUsable(readModel))", "Critical renderer cave read-model resolver rejects stale Unity-backed runtimes", builder, ref failureCount);
                AssertContains(rendererCaveReadModelResolver, "_spatialAudioListenerCaveReadModel = null", "Critical renderer clears stale cave read-model references", builder, ref failureCount);
                AssertContains(rendererBinauralReadModelResolver, "if (IsAudioRuntimeObjectUsable(readModel))", "Critical renderer binaural read-model resolver rejects stale Unity-backed runtimes", builder, ref failureCount);
                AssertContains(rendererBinauralReadModelResolver, "_spatialAudioBinauralEmitterReadModel = null", "Critical renderer clears stale binaural read-model references", builder, ref failureCount);
                AssertContains(rendererAudioUsable, "audioService == null || !audioService.IsInitialized", "Critical renderer validates audio service initialization before spatial read-model caching", builder, ref failureCount);
                AssertContains(rendererAudioUsable, "return IsAudioRuntimeObjectUsable(audioService)", "Critical renderer reuses Unity object activity validation for audio services", builder, ref failureCount);
                AssertContains(rendererAudioObjectUsable, "runtime is IAudioService audioService && !audioService.IsInitialized", "Critical renderer rejects deinitialized cached audio runtime interfaces", builder, ref failureCount);
                AssertContains(rendererAudioObjectUsable, "runtime is Behaviour behaviour", "Critical renderer validates MonoBehaviour-backed audio runtime activity", builder, ref failureCount);
                AssertContains(rendererAudioObjectUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Critical renderer rejects destroyed or disabled audio runtimes", builder, ref failureCount);
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

            if (vehicleSubOsCockpit.Length > 0)
            {
                string cockpitHotSwap = ExtractMethodBody(vehicleSubOsCockpit, "public void OnGlobalRegistryServiceReplaced(");
                string cockpitColdCache = ExtractMethodBody(vehicleSubOsCockpit, "private void CacheRegistryServicesCold()");
                string cockpitPlayerCriticalCache = ExtractMethodBody(vehicleSubOsCockpit, "private void CachePlayerCriticalAudio(");
                string cockpitPlayerCriticalResolve = ExtractMethodBody(vehicleSubOsCockpit, "private IPlayerCriticalSonarEchoReadModel ResolvePlayerCriticalSonarEchoReadModel()");
                string cockpitPlayerCriticalUsable = ExtractMethodBody(vehicleSubOsCockpit, "private static bool IsPlayerCriticalSonarEchoReadModelUsable(");
                string cockpitSonarUpload = ExtractMethodBody(vehicleSubOsCockpit, "private void UploadSonarTapsAndDispatchRadar()");

                AssertContains(cockpitHotSwap, "CachePlayerCriticalAudio(currentService as IPlayerCriticalSonarEchoReadModel)", "Vehicle cockpit caches player-critical sonar hot swaps through the usable-runtime filter", builder, ref failureCount);
                AssertContains(cockpitColdCache, "CachePlayerCriticalAudio(GlobalRegistry.PlayerCriticalSonarEcho)", "Vehicle cockpit cold-caches player-critical sonar through the usable-runtime filter", builder, ref failureCount);
                AssertContains(cockpitPlayerCriticalCache, "_cachedPlayerCriticalAudio = IsPlayerCriticalSonarEchoReadModelUsable(playerCriticalAudio)", "Vehicle cockpit stores only usable player-critical sonar read models", builder, ref failureCount);
                AssertContains(cockpitPlayerCriticalResolve, "if (IsPlayerCriticalSonarEchoReadModelUsable(playerCriticalAudio))", "Vehicle cockpit resolves player-critical sonar read models only while usable", builder, ref failureCount);
                AssertContains(cockpitPlayerCriticalResolve, "_cachedPlayerCriticalAudio = null", "Vehicle cockpit clears stale player-critical sonar read models", builder, ref failureCount);
                AssertContains(cockpitPlayerCriticalUsable, "playerCriticalAudio is Behaviour behaviour", "Vehicle cockpit validates MonoBehaviour-backed player-critical sonar read models", builder, ref failureCount);
                AssertContains(cockpitPlayerCriticalUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Vehicle cockpit rejects destroyed or disabled player-critical sonar read models", builder, ref failureCount);
                AssertContains(cockpitSonarUpload, "IPlayerCriticalSonarEchoReadModel audioRuntime = ResolvePlayerCriticalSonarEchoReadModel()", "Vehicle cockpit sonar upload uses the usable player-critical resolver", builder, ref failureCount);
                AssertNotContains(cockpitSonarUpload, "IPlayerCriticalSonarEchoReadModel audioRuntime = _cachedPlayerCriticalAudio", "Vehicle cockpit sonar upload never trusts the raw player-critical cache", builder, ref failureCount);
            }

            if (adaptiveStemMixer.Length > 0)
            {
                string adaptiveDrain = ExtractMethodBody(adaptiveStemMixer, "private void DrainSignalInputs()");
                string adaptiveQuality = ExtractMethodBody(adaptiveStemMixer, "private float ResolveGlobalQualityWeightFromSnapshot()");
                string adaptiveLaneConfig = ExtractMethodBody(adaptiveStemMixer, "private static void EnsureDynamicMusicSignalLaneCold()");
                string adaptiveUnityMix = ExtractMethodBody(adaptiveStemMixer, "private void ApplyMixFrameToUnityAudio(");
                string adaptiveMusicPush = ExtractMethodBody(adaptiveStemMixer, "private void PushDynamicMusicSignal(");
                AssertContains(adaptiveStemMixer, "BufferID.ShinobuScalabilityState", "Adaptive stem mixer reads continuous quality from the vault-owned scalability state", builder, ref failureCount);
                AssertContains(adaptiveQuality, "state.GlobalQualityWeight", "Adaptive stem quality resolver consumes the continuous global quality weight", builder, ref failureCount);
                AssertNotContains(adaptiveDrain, "SignalBus<ScalabilityChangedEvent>.GetFrameSnapshot()", "Adaptive stem mixer does not drain binary scalability events for quality", builder, ref failureCount);
                AssertNotContains(adaptiveStemMixer, "ResolveQualityTierFallbackWeight", "Adaptive stem mixer has no quality-tier fallback mapper", builder, ref failureCount);
                AssertContains(adaptiveLaneConfig, "lowTierFrameSignals: 64", "Dynamic music scalar signal lane keeps full minimum-quality frame capacity", builder, ref failureCount);
                AssertContains(adaptiveUnityMix, "PushDynamicMusicSignal(tension, depthMeters, quality);", "Adaptive stem bridge forwards only context scalars into the dynamic music lane", builder, ref failureCount);
                AssertContains(adaptiveMusicPush, "DynamicMusicScalarSignal.FlagSuppressReactiveImpulses", "Adaptive stem bridge cannot wake reactive dynamic music punches", builder, ref failureCount);
                AssertContains(adaptiveMusicPush, "signal.DamageImpulse01 = 0f", "Adaptive stem bridge clears legacy damage impulses before publishing dynamic music scalars", builder, ref failureCount);
                AssertContains(adaptiveMusicPush, "signal.MusicActivity01 = 0f", "Adaptive stem bridge does not claim foreground music activity", builder, ref failureCount);
                AssertNotContains(adaptiveUnityMix, "frame.IoPressure01", "Adaptive stem bridge no longer maps IO pressure into dynamic music damage impulses", builder, ref failureCount);
            }

            if (dynamicMusicSynth.Length > 0)
            {
                string dynamicAwake = ExtractMethodBody(dynamicMusicSynth, "private void Awake()");
                string dynamicOnEnable = ExtractMethodBody(dynamicMusicSynth, "private void OnEnable()");
                string dynamicUnregister = ExtractMethodBody(dynamicMusicSynth, "private void UnregisterRuntime()");
                string dynamicClearCached = ExtractMethodBody(dynamicMusicSynth, "private void ClearCachedRuntimeServices()");
                string dynamicLaneConfig = ExtractMethodBody(dynamicMusicSynth, "private static void EnsureDynamicMusicSignalLaneCold()");
                string dynamicAudioCallback = ExtractMethodBody(dynamicMusicSynth, "private void OnAudioFilterRead(float[] data, int channels)");
                string dynamicLateFrame = ExtractMethodBody(dynamicMusicSynth, "public void LateFrameTick()");
                string dynamicMixerRoute = ExtractMethodBody(dynamicMusicSynth, "private void ApplyAudioHostMixerRoute()");
                string dynamicAudioCache = ExtractMethodBody(dynamicMusicSynth, "private void CacheAudioService(");
                string dynamicAudioResolver = ExtractMethodBody(dynamicMusicSynth, "private IAudioService ResolveAudioService()");
                string dynamicAudioUsable = ExtractMethodBody(dynamicMusicSynth, "private static bool IsAudioServiceUsable(");
                string dynamicSignalDrain = ExtractMethodBody(dynamicMusicSynth, "private void DrainSignalInputs()");
                string dynamicSchedule = ExtractMethodBody(dynamicMusicSynth, "private void ScheduleSynthJobs(");
                string dynamicMockJob = ExtractMethodBody(dynamicMusicSynth, "private unsafe struct GenerateMockTensionJob");
                AssertContains(dynamicAwake, "CacheMusicDirectorCold();", "Dynamic music synth cold-seeds music director before initial host routing", builder, ref failureCount);
                AssertContains(dynamicAwake, "CacheSettingsManagerCold();", "Dynamic music synth cold-seeds settings before initial fallback volume routing", builder, ref failureCount);
                AssertTextBefore(dynamicAwake, "CacheMusicDirectorCold();", "ConfigureAudioHostCold();", "Dynamic music synth resolves music route before Awake host configuration", builder, ref failureCount);
                AssertTextBefore(dynamicAwake, "CacheSettingsManagerCold();", "ConfigureAudioHostCold();", "Dynamic music synth resolves settings volume before Awake host configuration", builder, ref failureCount);
                AssertContains(dynamicOnEnable, "CacheMusicDirectorCold();", "Dynamic music synth refreshes music director before enable host routing", builder, ref failureCount);
                AssertContains(dynamicOnEnable, "CacheSettingsManagerCold();", "Dynamic music synth refreshes settings before enable host routing", builder, ref failureCount);
                AssertTextBefore(dynamicOnEnable, "CacheMusicDirectorCold();", "ConfigureAudioHostCold();", "Dynamic music synth resolves music route before OnEnable host configuration", builder, ref failureCount);
                AssertTextBefore(dynamicOnEnable, "CacheSettingsManagerCold();", "ConfigureAudioHostCold();", "Dynamic music synth resolves settings volume before OnEnable host configuration", builder, ref failureCount);
                AssertContains(dynamicUnregister, "ClearCachedRuntimeServices();", "Dynamic music synth clears cached runtime services on unregister", builder, ref failureCount);
                AssertContains(dynamicClearCached, "_cachedAudioService = null", "Dynamic music synth clears cached audio service", builder, ref failureCount);
                AssertContains(dynamicClearCached, "_cachedMusicDirector = null", "Dynamic music synth clears cached music director", builder, ref failureCount);
                AssertContains(dynamicClearCached, "_cachedSettingsManager = null", "Dynamic music synth clears cached settings manager", builder, ref failureCount);
                AssertContains(dynamicLaneConfig, "lowTierFrameSignals: 64", "Dynamic music granular synth keeps full minimum-quality scalar lane capacity", builder, ref failureCount);
                AssertContains(dynamicMusicSynth, "ResolveGlobalQualityWeightFromSnapshot()", "Dynamic music granular synth derives quality from the continuous quality snapshot", builder, ref failureCount);
                AssertContains(dynamicAudioCache, "_cachedAudioService = IsAudioServiceUsable(audioService) ? audioService : null", "Dynamic music synth stores only usable audio services", builder, ref failureCount);
                AssertContains(dynamicAudioResolver, "if (IsAudioServiceUsable(audioService))", "Dynamic music synth resolves cached audio services only while usable", builder, ref failureCount);
                AssertContains(dynamicAudioResolver, "_cachedAudioService = null", "Dynamic music synth clears stale audio-service references", builder, ref failureCount);
                AssertContains(dynamicAudioUsable, "audioService is Behaviour behaviour", "Dynamic music synth validates MonoBehaviour-backed audio service activity", builder, ref failureCount);
                AssertContains(dynamicAudioUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Dynamic music synth rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(dynamicMixerRoute, "musicDirector != null && musicDirector.isActiveAndEnabled ? musicDirector.DedicatedMusicMixerGroup : null", "Dynamic music synth refuses stale disabled music-director routes", builder, ref failureCount);
                AssertContains(dynamicMixerRoute, "IAudioService audioService = ResolveAudioService()", "Dynamic music synth mixer fallback uses the usable audio-service resolver", builder, ref failureCount);
                AssertContains(dynamicMixerRoute, "musicDirector.DedicatedMusicMixerGroup", "Dynamic music synth follows the active music director mixer route before ambient fallback", builder, ref failureCount);
                AssertContains(dynamicMixerRoute, "ResolveFallbackMusicHostVolume01()", "Dynamic music synth applies MusicVolume as a fallback host-volume multiplier when no dedicated music route exists", builder, ref failureCount);
                AssertContains(dynamicMixerRoute, "audioService.AmbientGroup", "Dynamic music synth keeps AmbientGroup as its final route fallback", builder, ref failureCount);
                AssertContains(dynamicSignalDrain, "bool signalIsMusicDirectorScalar", "Dynamic music synth classifies scalar source once per signal", builder, ref failureCount);
                AssertContains(dynamicSignalDrain, "if (signalIsMusicDirectorScalar || !receivedMusicDirectorScalar)", "Dynamic music synth lets the music director outrank fallback scalar sources", builder, ref failureCount);
                AssertContains(dynamicSignalDrain, "else if (!receivedMusicDirectorScalar && signal.MusicActivity01 > 0f)", "Fallback music activity cannot overwrite a director scalar in the same frame", builder, ref failureCount);
                AssertContains(dynamicSignalDrain, "FlagSuppressReactiveImpulses", "Dynamic music synth honors director emergency suppression before scheduling music punches", builder, ref failureCount);
                AssertContains(dynamicSignalDrain, "_externalMusicActivity01 = 0f", "Dynamic music synth clears external activity during emergency suppression", builder, ref failureCount);
                AssertContains(dynamicSignalDrain, "!_allowMockPlaybackWithoutDirector", "Dynamic music synth does not let raw damage signals wake music without a recent director scalar", builder, ref failureCount);
                AssertContains(dynamicSchedule, "mockJob.SuppressReactiveImpulses", "Dynamic music synth carries suppression into the scheduled scalar job", builder, ref failureCount);
                AssertContains(dynamicMockJob, "scalar.StingerImpulse = SuppressReactiveImpulses != 0", "Dynamic music scalar job clears stored stinger tail under emergency/no-director suppression", builder, ref failureCount);
                AssertContains(dynamicAudioCallback, "TryResolvePublishedAudioThreadCopyBuffer", "Dynamic music managed callback only reads the published audio-thread copy buffer", builder, ref failureCount);
                AssertContains(dynamicAudioCallback, "UnsafeUtility.MemCpy(destination, source", "Dynamic music managed callback copies prebuilt interleaved samples only", builder, ref failureCount);
                AssertContains(dynamicLateFrame, "PublishAudioThreadCopyBufferLateFrame()", "Dynamic music publishes a dedicated audio-thread copy buffer from LateFrame", builder, ref failureCount);
                AssertNotContains(dynamicMusicSynth, "PlayerStressSignal", "Dynamic music synth does not read player stress directly; the music director owns stress-to-activity policy", builder, ref failureCount);
                AssertNotContains(dynamicMusicSynth, "IVocalWarningSystem", "Dynamic music synth does not read vocal-warning state directly; the music director owns speech foreground policy", builder, ref failureCount);
                AssertNotContains(dynamicMusicSynth, "IAudioLogRuntime", "Dynamic music synth does not read audio-log state directly; the music director owns speech foreground policy", builder, ref failureCount);
                AssertNotContains(dynamicMusicSynth, "GlobalRegistry.VocalWarnings", "Dynamic music synth does not poll vocal-warning runtime directly", builder, ref failureCount);
                AssertNotContains(dynamicMusicSynth, "GlobalRegistry.AudioLogRuntime", "Dynamic music synth does not poll audio-log runtime directly", builder, ref failureCount);
                AssertNotContains(dynamicAudioCallback, "TryAcquire", "Dynamic music managed callback must not acquire DataVault or mutation guards", builder, ref failureCount);
                AssertNotContains(dynamicAudioCallback, "DataVault", "Dynamic music managed callback must not touch DataVault state", builder, ref failureCount);
                AssertNotContains(dynamicAudioCallback, "GlobalRegistry", "Dynamic music managed callback must not query runtime registries", builder, ref failureCount);
                AssertNotContains(dynamicAudioCallback, "ScheduleSynthJobs", "Dynamic music managed callback must not schedule synthesis work", builder, ref failureCount);
                AssertNotContains(dynamicAudioCallback, ".Schedule(", "Dynamic music managed callback must not schedule jobs", builder, ref failureCount);
                AssertNotContains(dynamicAudioCallback, "JobHandle", "Dynamic music managed callback must not observe or complete jobs", builder, ref failureCount);
                AssertNotContains(dynamicAudioCallback, "GranularSynthesisJob", "Dynamic music managed callback must not synthesize samples", builder, ref failureCount);
                AssertNotContains(dynamicAudioCallback, "Stopwatch", "Dynamic music managed callback must not measure timing on the audio thread", builder, ref failureCount);
                AssertNotContains(dynamicAudioCallback, "AudioSettings", "Dynamic music managed callback must not query Unity audio settings on the audio thread", builder, ref failureCount);
                AssertNotContains(dynamicAudioCallback, "ResolveElapsedMicroseconds", "Dynamic music managed callback must not call timing helpers", builder, ref failureCount);
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
                string psychosisAudioCache = ExtractMethodBody(deepPsychosis, "private void CacheAudioService(");
                string psychosisAudioUsable = ExtractMethodBody(deepPsychosis, "private static bool IsAudioServiceUsable(");
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
                AssertContains(psychosisAudioCache, "_audioService = IsAudioServiceUsable(audioService) ? audioService : null", "Deep psychosis stores only usable audio services", builder, ref failureCount);
                AssertContains(psychosisAudioResolver, "if (IsAudioServiceUsable(audioService))", "Deep psychosis resolves cached audio services only while usable", builder, ref failureCount);
                AssertContains(psychosisAudioResolver, "_audioService = null", "Deep psychosis clears stale audio-service references", builder, ref failureCount);
                AssertContains(psychosisAudioUsable, "audioService is Behaviour behaviour", "Deep psychosis validates MonoBehaviour-backed audio service activity", builder, ref failureCount);
                AssertContains(psychosisAudioUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Deep psychosis rejects destroyed or disabled audio services", builder, ref failureCount);
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
                string musicAudioCache = ExtractMethodBody(musicDirector, "private void CacheAudioService(");
                string musicAudioUsable = ExtractMethodBody(musicDirector, "private static bool IsAudioServiceUsable(");
                string musicAcousticResolver = ExtractMethodBody(musicDirector, "private IAcousticZoneReadModel ResolveAcousticZone()");
                string musicDepthResolver = ExtractMethodBody(musicDirector, "private DepthZoneDirector ResolveDepthZoneDirector()");
                string musicSurfaceResolver = ExtractMethodBody(musicDirector, "private ISurfaceWeatherReadModel ResolveSurfaceWeatherDirector()");
                string musicFirstHourResolver = ExtractMethodBody(musicDirector, "private IFirstHourReadModel ResolveFirstHourDirector()");
                string musicReboundRuntime = ExtractMethodBody(musicDirector, "private void CacheReboundRuntimeService(");
                string musicAcousticDrain = ExtractMethodBody(musicDirector, "private void DrainAcousticZoneSignal()");
                string musicSynthRuntime = ExtractMethodBody(musicDirector, "private void EnsureProceduralSynthRuntime()");
                string musicResolveProfile = ExtractMethodBody(musicDirector, "private HectonMusicBiomeProfile ResolveProfile(");
                string musicSoundscapeProfile = ExtractMethodBody(musicDirector, "private HectonMusicBiomeProfile ResolveSoundscapeTierProfile(");
                string musicTickRoute = ExtractMethodBody(musicDirector, "private void RunMusicTick(");
                string musicStingerFlush = ExtractMethodBody(musicDirector, "private void FlushPendingStingers()");
                string musicDiscoveryStinger = ExtractMethodBody(musicDirector, "public void PlayDiscoveryStinger()");
                string musicDangerStinger = ExtractMethodBody(musicDirector, "public void PlayDangerStinger()");
                string musicRecoveryStinger = ExtractMethodBody(musicDirector, "public void PlayRecoveryStinger()");
                string musicActivityUpdate = ExtractMethodBody(musicDirector, "private void UpdateProceduralMusicActivity(");
                string musicForceOpen = ExtractMethodBody(musicDirector, "private bool ShouldForceProceduralMusicOpen()");
                string musicWait = ExtractMethodBody(musicDirector, "private void BeginProceduralWait(");
                string musicRuntimeRegister = ExtractMethodBody(musicDirector, "private bool TryRegisterToGlobalRegistry()");
                string musicRuntimeUsable = ExtractMethodBody(musicDirector, "private static bool IsMusicDirectorRuntimeUsable(");
                string musicPhrase = ExtractMethodBody(musicDirector, "private float ResolveProceduralPhraseSeconds(");
                string musicActivityTarget = ExtractMethodBody(musicDirector, "private float ResolveProceduralMusicActivityTarget01()");
                string musicScalarPublish = ExtractMethodBody(musicDirector, "private void PublishDynamicMusicScalars(");
                string musicStopScalarPublish = ExtractMethodBody(musicDirector, "private void PublishProceduralMusicStopSignal()");
                string musicOverrideStart = ExtractMethodBody(musicDirector, "private void ForceOverrideTrackInternal(");
                string musicStopInternal = ExtractMethodBody(musicDirector, "private void StopMusicInternal(");
                string musicSoundscapeContext = ExtractMethodBody(musicDirector, "public void SetSoundscapeTierContext(");
                string musicLayerRouting = ExtractMethodBody(musicDirector, "private void UpdateLayerRouting(");
                string musicLayerApply = ExtractMethodBody(musicDirector, "private void ApplyLayerMixerState(");
                string musicLayerNormalize = ExtractMethodBody(musicDirector, "private static float NormalizedLayerValueToDb(");
                string musicLayerTryApply = ExtractMethodBody(musicDirector, "private bool TryApplyLayerMixerParameter(");
                string musicLayerReset = ExtractMethodBody(musicDirector, "private void ResetLayerMixerStateCache()");
                string musicResolveTension = ExtractMethodBody(musicDirector, "private float ResolveTension01()");
                string musicPlayerStressRefresh = ExtractMethodBody(musicDirector, "private void RefreshPlayerCriticalStressSignal()");
                string musicEmergencyDominance = ExtractMethodBody(musicDirector, "private float ResolveEmergencyAudioDominance01()");
                string musicEmergencyGate = ExtractMethodBody(musicDirector, "private bool IsEmergencyBreathDominant()");
                string musicVocalWarningDuck = ExtractMethodBody(musicDirector, "private void RefreshVocalWarningMusicDucking()");
                string musicForegroundSpeechApply = ExtractMethodBody(musicDirector, "private float ApplyForegroundSpeechMusicDuck01(");
                string musicForegroundSpeechActive = ExtractMethodBody(musicDirector, "private bool IsForegroundSpeechActive()");
                string musicForegroundSpeechRefresh = ExtractMethodBody(musicDirector, "private void RefreshForegroundSpeechMusicDucking()");
                string musicNarrativeAudioLogDuck = ExtractMethodBody(musicDirector, "private void RefreshNarrativeAudioLogMusicDucking()");
                string musicForegroundSpeechResolve = ExtractMethodBody(musicDirector, "private float ResolveForegroundSpeechMusicDuck01()");
                string musicVocalWarningResolve = ExtractMethodBody(musicDirector, "private static float ResolveVocalWarningMusicDuck01(");
                string musicVocalWarningStale = ExtractMethodBody(musicDirector, "private void RefreshVocalWarningRuntimeIfStale()");
                string musicVocalWarningResolver = ExtractMethodBody(musicDirector, "private IVocalWarningSystem ResolveVocalWarningSystem()");
                string musicVocalWarningCache = ExtractMethodBody(musicDirector, "private void CacheVocalWarningSystem(");
                string musicVocalWarningUsable = ExtractMethodBody(musicDirector, "private static bool IsVocalWarningRuntimeUsable(");
                string musicAudioLogResolver = ExtractMethodBody(musicDirector, "private IAudioLogRuntime ResolveAudioLogRuntime()");
                string musicAudioLogCache = ExtractMethodBody(musicDirector, "private void CacheAudioLogRuntime(");
                string musicAudioLogStale = ExtractMethodBody(musicDirector, "private void RefreshAudioLogRuntimeIfStale()");
                string musicAudioLogUsable = ExtractMethodBody(musicDirector, "private static bool IsAudioLogRuntimeUsable(");
                string soundscapeOnEnable = ExtractMethodBody(soundscapeSystem, "private void OnEnable()");
                string soundscapeOnDisable = ExtractMethodBody(soundscapeSystem, "private void OnDisable()");
                string soundscapeOnDestroy = ExtractMethodBody(soundscapeSystem, "private void OnDestroy()");
                string soundscapeSlowTick = ExtractMethodBody(soundscapeSystem, "public void SlowTick()");
                string soundscapeDepthTier = ExtractMethodBody(soundscapeSystem, "void IBiomeMatrixEventListener.OnDepthTierChanged(");
                string soundscapeRebound = ExtractMethodBody(soundscapeSystem, "void IGlobalRegistryHotSwapRefListener.OnGlobalRegistryServiceRebound(");
                string soundscapeHotSwap = ExtractMethodBody(soundscapeSystem, "void IGlobalRegistryHotSwapListener.OnGlobalRegistryServiceReplaced(");
                string soundscapeSyncMusic = ExtractMethodBody(soundscapeSystem, "private void SyncMusicDirectorSoundscapeContext(");
                string soundscapeSyncCachedMusic = ExtractMethodBody(soundscapeSystem, "private void SyncCachedMusicDirectorSoundscapeContext(");
                string soundscapeAudioCache = ExtractMethodBody(soundscapeSystem, "private void CacheAudioService(");
                string soundscapeAudioResolver = ExtractMethodBody(soundscapeSystem, "private IAudioService ResolveAudioService()");
                string soundscapeAudioUsable = ExtractMethodBody(soundscapeSystem, "private static bool IsAudioServiceUsable(");
                string soundscapeSignalDrain = ExtractMethodBody(soundscapeSystem, "private void DrainSignals()");
                string soundscapeImpactSignal = ExtractMethodBody(soundscapeSystem, "private void HandleImpactSignal(");
                string soundscapeCacheMusic = ExtractMethodBody(soundscapeSystem, "private void CacheMusicDirector(");
                AssertContains(musicDirector, "ResolvePlayerRuntimeContext()", "Music director player context uses a bounded cached resolver", builder, ref failureCount);
                AssertContains(musicDirector, "ResolveAudioService()", "Music director mixer routing uses cached audio-service resolution", builder, ref failureCount);
                AssertContains(musicAudioCache, "_cachedAudioService = IsAudioServiceUsable(audioService) ? audioService : null", "Music director stores only usable audio services", builder, ref failureCount);
                AssertContains(musicAudioResolver, "if (IsAudioServiceUsable(audioService))", "Music director resolves cached audio services only while usable", builder, ref failureCount);
                AssertContains(musicAudioResolver, "_cachedAudioService = null", "Music director clears stale audio-service references", builder, ref failureCount);
                AssertContains(musicAudioUsable, "audioService is Behaviour behaviour", "Music director validates MonoBehaviour-backed audio service activity", builder, ref failureCount);
                AssertContains(musicAudioUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Music director rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(musicDirector, "ResolveAcousticZone()", "Music director base context uses cached acoustic-zone resolution", builder, ref failureCount);
                AssertContains(musicDirector, "ResolveDepthZoneDirector()", "Music director depth-zone dependency uses cached runtime resolution", builder, ref failureCount);
                AssertContains(musicDirector, "ResolveSurfaceWeatherDirector()", "Music director storm pressure uses cached surface-weather resolution", builder, ref failureCount);
                AssertContains(musicDirector, "ResolveFirstHourDirector()", "Music director stinger gates use cached first-hour resolution", builder, ref failureCount);
                AssertContains(musicDirector, "ClearCachedRuntimeServices()", "Music director clears cached runtime services on disable/destroy", builder, ref failureCount);
                AssertContains(musicDirector, "IGlobalRegistryHotSwapListener", "Music director receives runtime service hot swaps", builder, ref failureCount);
                AssertContains(musicDirector, "TryRegisterHotSwapListener()", "Music director registers hot-swap listener during lifecycle", builder, ref failureCount);
                AssertContains(musicDirector, "GlobalRegistry.TryUnregisterHotSwapListener(this)", "Music director unregisters hot-swap listener during lifecycle", builder, ref failureCount);
                AssertContains(musicRuntimeRegister, "HectonMusicDirector activeDirector = GlobalRegistry.MusicDirector", "Music director snapshots the current registry owner once", builder, ref failureCount);
                AssertContains(musicRuntimeRegister, "!ReferenceEquals(activeDirector, null)", "Music director detects stale destroyed registry references by actual reference", builder, ref failureCount);
                AssertContains(musicRuntimeRegister, "!ReferenceEquals(activeDirector, this)", "Music director treats only other owners as registry conflicts", builder, ref failureCount);
                AssertContains(musicRuntimeRegister, "if (IsMusicDirectorRuntimeUsable(activeDirector))", "Music director preserves usable existing runtime owners", builder, ref failureCount);
                AssertContains(musicRuntimeRegister, "Destroy(gameObject);", "Music director destroys duplicate runtime roots only when the existing owner is usable", builder, ref failureCount);
                AssertContains(musicRuntimeRegister, "GlobalRegistry.UnregisterMusicDirectorRuntime(activeDirector);", "Music director clears stale existing owners before registering", builder, ref failureCount);
                AssertTextBefore(musicRuntimeRegister, "GlobalRegistry.UnregisterMusicDirectorRuntime(activeDirector);", "GlobalRegistry.RegisterMusicDirectorRuntime(this);", "Music director unregisters stale owners before self-register", builder, ref failureCount);
                AssertContains(musicRuntimeUsable, "return director != null && director.isActiveAndEnabled", "Music director owner usability rejects destroyed or disabled owners", builder, ref failureCount);
                AssertContains(musicDirector, "_depthZoneDirectorRuntimeCached", "Music director distinguishes serialized depth-zone references from runtime cache", builder, ref failureCount);
                AssertContains(musicReboundRuntime, "GlobalRegistryServiceSlot.Player", "Music director handles player service hot swaps", builder, ref failureCount);
                AssertContains(musicReboundRuntime, "GlobalRegistryServiceSlot.Audio", "Music director handles audio service hot swaps", builder, ref failureCount);
                AssertContains(musicReboundRuntime, "GlobalRegistryServiceSlot.AcousticZoneRuntime", "Music director handles acoustic-zone service hot swaps", builder, ref failureCount);
                AssertContains(musicReboundRuntime, "GlobalRegistryServiceSlot.DepthZoneRuntime", "Music director handles depth-zone service hot swaps", builder, ref failureCount);
                AssertContains(musicReboundRuntime, "GlobalRegistryServiceSlot.SurfaceWeatherRuntime", "Music director handles surface-weather service hot swaps", builder, ref failureCount);
                AssertContains(musicReboundRuntime, "GlobalRegistryServiceSlot.FirstHourRuntime", "Music director handles first-hour service hot swaps", builder, ref failureCount);
                AssertContains(musicReboundRuntime, "GlobalRegistryServiceSlot.VocalWarningRuntime", "Music director handles vocal-warning runtime hot swaps", builder, ref failureCount);
                AssertContains(musicReboundRuntime, "GlobalRegistryServiceSlot.AudioLogRuntime", "Music director handles audio-log runtime hot swaps", builder, ref failureCount);
                AssertContains(musicDirector, "DrainAcousticZoneSignal();", "Music director drains acoustic-zone typed signals from tick lanes", builder, ref failureCount);
                AssertContains(musicAcousticDrain, "ReadOnlySpan<AcousticZoneChangedEvent> signals = SignalBus<AcousticZoneChangedEvent>.GetFrameSnapshot();", "Music director consumes acoustic-zone changes through a ReadOnlySpan typed-lane snapshot", builder, ref failureCount);
                AssertContains(musicAcousticDrain, "_lastAcousticZoneSignalFrame == frame", "Music director drains acoustic-zone signals at most once per frame", builder, ref failureCount);
                AssertContains(musicAcousticDrain, "HandleAcousticZoneChanged(signal.IsInterior != 0)", "Music director routes the latest acoustic-zone signal into existing music context logic", builder, ref failureCount);
                AssertContains(musicResolveProfile, "ResolveSoundscapeTierProfile()", "Music director falls back to soundscape tier when biome matrix does not provide a profile", builder, ref failureCount);
                AssertContains(musicSoundscapeProfile, "case SoundscapeTier.Thermal", "Thermal soundscape tier selects thermal music profile as fallback", builder, ref failureCount);
                AssertContains(musicSoundscapeProfile, "case SoundscapeTier.DeepAbyss", "Deep abyss soundscape tier selects abyss music profile as fallback", builder, ref failureCount);
                AssertContains(musicSoundscapeProfile, "case SoundscapeTier.Darkness", "Darkness soundscape tier selects shelf music profile as fallback", builder, ref failureCount);
                AssertContains(musicDirector, "DrainDirectorAISignals();", "Music director drains DirectorAI music signals from typed lanes", builder, ref failureCount);
                AssertContains(musicDirector, "ReadOnlySpan<DirectorAIMusicSignal> signals = SignalBus<DirectorAIMusicSignal>.GetFrameSnapshot();", "Music director consumes DirectorAI cues through a ReadOnlySpan typed-lane snapshot", builder, ref failureCount);
                AssertContains(musicDirector, "RefreshPolledMusicContext();", "Music director polls biome/depth runtime state instead of listener queues", builder, ref failureCount);
                AssertContains(musicDirector, "RefreshObservedBiomeMatrixState()", "Music director observes biome-matrix profile/depth state without registration", builder, ref failureCount);
                AssertContains(musicDirector, "RefreshObservedDepthZoneState()", "Music director observes depth-zone transitions without registration", builder, ref failureCount);
                AssertContains(musicDirector, "CriticalPlayerStressDominatesThreshold = 0.88f", "Music director treats critical player stress as foreground emergency audio", builder, ref failureCount);
                AssertContains(musicDirector, "PlayerStressSignalHoldFrames = 8", "Music director releases stale player-stress signals quickly", builder, ref failureCount);
                AssertContains(musicDirector, "RefreshPlayerCriticalStressSignal();", "Music director refreshes player critical stress outside the audio callback", builder, ref failureCount);
                AssertContains(musicPlayerStressRefresh, "SignalBus<PlayerStressSignal>.TryGetLatest", "Music director consumes player stress through the typed signal lane", builder, ref failureCount);
                AssertContains(musicPlayerStressRefresh, "_lastPlayerStressSignalSeenFrame == int.MinValue", "Music director accepts the first player-stress signal even when sequence starts at zero", builder, ref failureCount);
                AssertContains(musicPlayerStressRefresh, "frame - _lastPlayerStressSignalSeenFrame > PlayerStressSignalHoldFrames", "Music director prevents stale player stress from holding music down indefinitely", builder, ref failureCount);
                AssertContains(musicPlayerStressRefresh, "_playerCriticalStress01 = math.saturate(signal.Stress01)", "Music director caches sanitized player stress for music policy", builder, ref failureCount);
                AssertContains(musicPlayerStressRefresh, "_playerCriticalStress01 = 0f", "Music director clears player-stress music pressure when signal freshness expires", builder, ref failureCount);
                AssertContains(musicEmergencyDominance, "math.max(_oxygenDanger01, _playerCriticalStress01)", "Music director merges oxygen danger and player stress into one emergency-audio dominance scalar", builder, ref failureCount);
                AssertContains(musicEmergencyGate, "_playerCriticalStress01 >= CriticalPlayerStressDominatesThreshold", "Critical player stress can make music yield even before oxygen danger crosses its threshold", builder, ref failureCount);
                AssertContains(musicDirector, "VocalWarningMusicDuckDefault01 = 0.38f", "Music director softly ducks music under non-critical vocal warnings", builder, ref failureCount);
                AssertContains(musicDirector, "VocalWarningMusicDuckCritical01 = 0.62f", "Music director strongly ducks music under critical vocal warnings", builder, ref failureCount);
                AssertContains(musicDirector, "NarrativeAudioLogMusicDuck01 = 0.48f", "Music director ducks music while narrative audio logs own speech foreground", builder, ref failureCount);
                AssertContains(musicDirector, "_lastForegroundSpeechDuckingRefreshFrame = -1", "Music director resets foreground-speech ducking cache on service clear/rebind", builder, ref failureCount);
                AssertContains(musicDirector, "CacheVocalWarningSystem(GlobalRegistry.VocalWarnings, frame)", "Music director seeds vocal-warning runtime only through the cold cache path", builder, ref failureCount);
                AssertContains(musicDirector, "CacheAudioLogRuntime(GlobalRegistry.AudioLogRuntime, frame)", "Music director seeds audio-log runtime only through the cold cache path", builder, ref failureCount);
                AssertContains(musicVocalWarningStale, "frame < _nextVocalWarningResolveFrame", "Music director retry-cadences missing vocal-warning runtime resolution", builder, ref failureCount);
                AssertContains(musicVocalWarningStale, "CacheVocalWarningSystem(GlobalRegistry.VocalWarnings, frame)", "Music director late-binds vocal-warning runtime through a bounded stale resolver", builder, ref failureCount);
                AssertContains(musicVocalWarningResolver, "if (IsVocalWarningRuntimeUsable(vocalWarningSystem))", "Music director vocal-warning resolver refuses unusable cached runtimes", builder, ref failureCount);
                AssertContains(musicVocalWarningResolver, "_cachedVocalWarningSystem = null", "Music director vocal-warning resolver clears stale cached runtimes", builder, ref failureCount);
                AssertContains(musicVocalWarningCache, "_cachedVocalWarningSystem = IsVocalWarningRuntimeUsable(vocalWarningSystem) ? vocalWarningSystem : null", "Music director caches only usable vocal-warning runtimes", builder, ref failureCount);
                AssertContains(musicVocalWarningStale, "if (IsVocalWarningRuntimeUsable(vocalWarningSystem))", "Music director stale vocal-warning refresh accepts only usable cached runtimes", builder, ref failureCount);
                AssertContains(musicVocalWarningStale, "_cachedVocalWarningSystem = null", "Music director clears unusable vocal-warning runtime before retry", builder, ref failureCount);
                AssertContains(musicVocalWarningUsable, "vocalWarningSystem == null || !vocalWarningSystem.IsInitialized", "Music director validates vocal-warning runtime initialization", builder, ref failureCount);
                AssertContains(musicVocalWarningUsable, "vocalWarningSystem is Behaviour behaviour", "Music director validates MonoBehaviour-backed vocal-warning runtime activity", builder, ref failureCount);
                AssertContains(musicVocalWarningUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Music director rejects destroyed or disabled vocal-warning runtimes", builder, ref failureCount);
                AssertContains(musicVocalWarningDuck, "IVocalWarningSystem vocalWarningSystem = ResolveVocalWarningSystem()", "Music director reads vocal-warning foreground state from the cached service", builder, ref failureCount);
                AssertContains(musicVocalWarningDuck, "vocalWarningSystem.IsWarningActive", "Music director treats active vocal warnings as foreground audio", builder, ref failureCount);
                AssertContains(musicVocalWarningDuck, "ResolveVocalWarningMusicDuck01(warningId)", "Music director maps warning identity to music duck strength", builder, ref failureCount);
                AssertContains(musicForegroundSpeechRefresh, "RefreshVocalWarningMusicDucking();", "Foreground speech refresh includes vocal-warning state", builder, ref failureCount);
                AssertContains(musicForegroundSpeechRefresh, "RefreshNarrativeAudioLogMusicDucking();", "Foreground speech refresh includes narrative audio-log state", builder, ref failureCount);
                AssertContains(musicForegroundSpeechRefresh, "int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex", "Foreground speech refresh reads dispatcher frame once", builder, ref failureCount);
                AssertContains(musicForegroundSpeechRefresh, "if (_lastForegroundSpeechDuckingRefreshFrame == frame)", "Foreground speech ducking refresh is cached per dispatcher frame", builder, ref failureCount);
                AssertContains(musicForegroundSpeechRefresh, "_lastForegroundSpeechDuckingRefreshFrame = frame", "Foreground speech ducking cache records the refreshed dispatcher frame", builder, ref failureCount);
                AssertContains(musicTickRoute, "RefreshForegroundSpeechMusicDucking();", "Music director refreshes speech ducking once from its tick route before stinger/scalar gates", builder, ref failureCount);
                AssertContains(musicStingerFlush, "RefreshForegroundSpeechMusicDucking();", "Pending stinger flush refreshes cached speech foreground before reading the gate", builder, ref failureCount);
                AssertContains(musicDiscoveryStinger, "RefreshForegroundSpeechMusicDucking();", "Discovery stinger refreshes cached speech foreground before reading the gate", builder, ref failureCount);
                AssertContains(musicDangerStinger, "RefreshForegroundSpeechMusicDucking();", "Danger stinger refreshes cached speech foreground before reading the gate", builder, ref failureCount);
                AssertContains(musicRecoveryStinger, "RefreshForegroundSpeechMusicDucking();", "Recovery stinger refreshes cached speech foreground before reading the gate", builder, ref failureCount);
                AssertContains(musicNarrativeAudioLogDuck, "IAudioLogRuntime audioLogRuntime = ResolveAudioLogRuntime()", "Music director reads audio-log foreground state from the cached service", builder, ref failureCount);
                AssertContains(musicNarrativeAudioLogDuck, "audioLogRuntime.IsPlaying || audioLogRuntime.IsNarrativeQueueBlocked", "Narrative audio-log playback or queue block owns speech foreground", builder, ref failureCount);
                AssertContains(musicForegroundSpeechResolve, "math.max(_vocalWarningMusicDuck01, _narrativeAudioLogMusicDuck01)", "Foreground speech duck chooses the strongest active speech owner", builder, ref failureCount);
                AssertContains(musicAudioLogResolver, "if (IsAudioLogRuntimeUsable(audioLogRuntime))", "Music director audio-log resolver refuses unusable cached runtimes", builder, ref failureCount);
                AssertContains(musicAudioLogResolver, "_cachedAudioLogRuntime = null", "Music director audio-log resolver clears stale cached runtimes", builder, ref failureCount);
                AssertContains(musicAudioLogCache, "_cachedAudioLogRuntime = IsAudioLogRuntimeUsable(audioLogRuntime) ? audioLogRuntime : null", "Music director caches only usable audio-log runtimes", builder, ref failureCount);
                AssertContains(musicAudioLogStale, "if (IsAudioLogRuntimeUsable(audioLogRuntime))", "Music director stale audio-log refresh accepts only usable cached runtimes", builder, ref failureCount);
                AssertContains(musicAudioLogStale, "_cachedAudioLogRuntime = null", "Music director clears unusable audio-log runtime before retry", builder, ref failureCount);
                AssertContains(musicAudioLogUsable, "audioLogRuntime is Behaviour behaviour", "Music director validates MonoBehaviour-backed audio-log runtime activity", builder, ref failureCount);
                AssertContains(musicAudioLogUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Music director rejects destroyed or disabled audio-log runtimes", builder, ref failureCount);
                AssertContains(musicForegroundSpeechApply, "safeActivity01 * (1f - duck01)", "Music activity target is reduced by foreground speech duck before publishing", builder, ref failureCount);
                AssertNotContains(musicForegroundSpeechRefresh, "RefreshVocalWarningRuntimeIfStale", "Foreground speech per-frame refresh uses cached speech runtimes only", builder, ref failureCount);
                AssertNotContains(musicForegroundSpeechRefresh, "RefreshAudioLogRuntimeIfStale", "Foreground speech per-frame refresh uses cached audio-log runtime only", builder, ref failureCount);
                AssertNotContains(musicVocalWarningDuck, "GlobalRegistry.", "Vocal-warning speech duck reads the cached runtime only", builder, ref failureCount);
                AssertNotContains(musicNarrativeAudioLogDuck, "GlobalRegistry.", "Narrative audio-log speech duck reads the cached runtime only", builder, ref failureCount);
                AssertNotContains(musicForegroundSpeechActive, "RefreshForegroundSpeechMusicDucking", "Foreground speech active gate is read-only after tick/public stinger refresh", builder, ref failureCount);
                AssertNotContains(musicForegroundSpeechActive, "GlobalRegistry.", "Foreground speech active gate does not poll registries", builder, ref failureCount);
                AssertContains(musicVocalWarningResolve, "VocalWarningId.CrushDepth", "Crush-depth warnings receive critical music duck", builder, ref failureCount);
                AssertContains(musicVocalWarningResolve, "VocalWarningId.HullBreach", "Hull-breach warnings receive critical music duck", builder, ref failureCount);
                AssertContains(musicVocalWarningResolve, "VocalWarningId.OxygenLow", "Oxygen warnings receive critical music duck", builder, ref failureCount);
                AssertContains(musicActivityUpdate, "ShouldForceProceduralMusicOpen()", "Music director opens procedural music immediately for non-rest critical contexts", builder, ref failureCount);
                AssertContains(musicForceOpen, "!IsEmergencyBreathDominant()", "Emergency breath remains higher priority than forced procedural music", builder, ref failureCount);
                AssertContains(musicForceOpen, "_combatLatched", "Combat forces procedural music out of rest", builder, ref failureCount);
                AssertContains(musicForceOpen, "_tenseExplorationLatched", "Tense exploration forces procedural music out of rest", builder, ref failureCount);
                AssertContains(musicForceOpen, "_currentBaseContext", "Base context keeps its low procedural bed active instead of waiting on exploration rests", builder, ref failureCount);
                AssertContains(musicDirector, "public void SetSoundscapeTierContext(SoundscapeTier tier, float depthMeters)", "Music director accepts authoritative soundscape tier/depth context", builder, ref failureCount);
                AssertContains(musicSoundscapeContext, "ResolveSoundscapeDepthHintMeters(safeTier)", "Music director maps soundscape tier to a depth hint when raw depth is unavailable", builder, ref failureCount);
                AssertContains(musicSoundscapeContext, "float pressure01 = ResolveSoundscapePressure01(safeTier)", "Music director resolves soundscape pressure once per context update", builder, ref failureCount);
                AssertContains(musicSoundscapeContext, "bool tierChanged = _currentSoundscapeTier != safeTier", "Music director detects soundscape tier transitions before writing tier state", builder, ref failureCount);
                AssertContains(musicSoundscapeContext, "_debugSoundscapePressure01 = pressure01", "Music director exposes soundscape pressure diagnostics", builder, ref failureCount);
                AssertContains(musicSoundscapeContext, "ReevaluateContext(true);", "Soundscape tier changes immediately refresh music profile routing", builder, ref failureCount);
                AssertContains(musicWait, "ResolveSoundscapeRestScale(_currentSoundscapeTier)", "Exploration rest windows scale from soundscape tier so deep water can breathe", builder, ref failureCount);
                AssertContains(musicPhrase, "ResolveSoundscapePhraseScale(_currentSoundscapeTier)", "Exploration phrase windows scale from soundscape tier instead of constant music beds", builder, ref failureCount);
                AssertContains(musicActivityTarget, "MusicActivityReason.Emergency", "Music director exposes emergency breath as the current music activity reason", builder, ref failureCount);
                AssertContains(musicActivityTarget, "MusicActivityReason.Combat", "Music director exposes combat as the current music activity reason", builder, ref failureCount);
                AssertContains(musicActivityTarget, "MusicActivityReason.Exploration", "Music director exposes exploration as the current music activity reason", builder, ref failureCount);
                AssertContains(musicActivityTarget, "ResolveSoundscapePressure01(_currentSoundscapeTier)", "Music activity target uses the soundscape tier as world-bed pressure", builder, ref failureCount);
                AssertContains(musicActivityTarget, "ResolveEmergencyAudioDominance01()", "Music activity target treats critical player audio as pressure before the hard emergency cutoff", builder, ref failureCount);
                AssertContains(musicActivityTarget, "ApplyForegroundSpeechMusicDuck01(", "Music activity targets are ducked while speech foreground owns the mix", builder, ref failureCount);
                AssertContains(musicResolveTension, "soundscapePressure01 * _soundscapePressureWeight", "Music tension blend includes bounded soundscape pressure", builder, ref failureCount);
                AssertContains(musicLayerRouting, "math.max(InverseLerp(20f, 900f, depthMeters), soundscapePressure01)", "Music layer routing falls back to soundscape tier depth pressure", builder, ref failureCount);
                AssertContains(musicLayerRouting, "float emergencyAudio01 = ResolveEmergencyAudioDominance01()", "Music layer routing uses unified emergency-audio pressure for bass and danger layers", builder, ref failureCount);
                AssertContains(musicLayerRouting, "_playerCriticalStress01 * 0.16f", "Critical player stress raises rhythm tension before the full music yield threshold", builder, ref failureCount);
                AssertContains(musicLayerRouting, "ApplyLayerMixerState(false);", "Music layer routing pushes smoothed layer values toward the mixer route", builder, ref failureCount);
                AssertContains(musicLayerApply, "NormalizedLayerValueToDb(_layerRhythm01)", "Music layer mixer routing converts rhythm intensity into dB", builder, ref failureCount);
                AssertContains(musicLayerApply, "_debugLayerMixerRouteAvailable = anyRouteAvailable", "Music director exposes whether optional layer mixer parameters are bound", builder, ref failureCount);
                AssertContains(musicDirector, "public float CurrentRhythmLayer01 => math.saturate(_layerRhythm01)", "Music director exposes rhythm-layer telemetry without reflection", builder, ref failureCount);
                AssertContains(musicDirector, "public float CurrentBassLayer01 => math.saturate(_layerBass01)", "Music director exposes bass-layer telemetry without reflection", builder, ref failureCount);
                AssertContains(musicDirector, "public float CurrentAtmosphereLayer01 => math.saturate(_layerAtmosphere01)", "Music director exposes atmosphere-layer telemetry without reflection", builder, ref failureCount);
                AssertContains(musicDirector, "public float CurrentDangerLayer01 => math.saturate(_layerDanger01)", "Music director exposes danger-layer telemetry without reflection", builder, ref failureCount);
                AssertContains(musicDirector, "public bool CurrentLayerMixerRouteAvailable => _debugLayerMixerRouteAvailable", "Music director exposes optional mixer-layer route availability", builder, ref failureCount);
                AssertContains(musicLayerNormalize, "Mathf.Log10(clamped)", "Music layer dB conversion uses logarithmic amplitude mapping", builder, ref failureCount);
                AssertContains(musicLayerTryApply, "unavailable && !force", "Missing optional music layer parameters are not retried every tick", builder, ref failureCount);
                AssertContains(musicLayerTryApply, "_layerMixer.SetFloat(parameterName, valueDb)", "Music director writes exposed music layer mixer parameters when present", builder, ref failureCount);
                AssertContains(musicLayerReset, "_rhythmLayerParameterUnavailable = false", "Music director resets optional layer mixer parameter cache when routing is rebound", builder, ref failureCount);
                AssertContains(musicScalarPublish, "FlagSuppressReactiveImpulses", "Music director suppresses reactive synth punches while emergency breath dominates", builder, ref failureCount);
                AssertContains(musicScalarPublish, "bool foregroundSpeechActive = IsForegroundSpeechActive();", "Music scalar publishing checks speech foreground before damage/stinger impulses", builder, ref failureCount);
                AssertContains(musicScalarPublish, "emergencyBreathDominates || foregroundSpeechActive", "Speech foreground suppresses reactive music impulses without becoming a new synth source", builder, ref failureCount);
                AssertContains(musicStopScalarPublish, "DynamicMusicScalarSignal.FlagExternalScalars | DynamicMusicScalarSignal.FlagSuppressReactiveImpulses", "Stopping procedural music immediately publishes zero activity and suppresses stale synth impulses", builder, ref failureCount);
                AssertContains(musicStopInternal, "PublishProceduralMusicStopSignal();", "Procedural music stop pushes an immediate scalar instead of waiting for the next director tick", builder, ref failureCount);
                AssertContains(musicOverrideStart, "RefreshForegroundSpeechMusicDucking();", "Forced override start refreshes cached speech foreground before publishing synth impulses", builder, ref failureCount);
                AssertContains(musicOverrideStart, "bool emergencyBreathDominates = IsEmergencyBreathDominant();", "Forced override start evaluates emergency breath before publishing synth impulses", builder, ref failureCount);
                AssertContains(musicOverrideStart, "bool foregroundSpeechActive = IsForegroundSpeechActive();", "Forced override start evaluates speech foreground before publishing synth impulses", builder, ref failureCount);
                AssertContains(musicOverrideStart, "bool suppressReactiveImpulses = emergencyBreathDominates || foregroundSpeechActive", "Forced override start merges emergency and speech foreground suppression", builder, ref failureCount);
                AssertContains(musicOverrideStart, "float overrideActivity01 = emergencyBreathDominates ? 0f : ApplyForegroundSpeechMusicDuck01(_overrideVolume)", "Forced override start publishes ducked music activity while speech foreground owns the mix", builder, ref failureCount);
                AssertContains(musicOverrideStart, "float overrideImpulse01 = suppressReactiveImpulses ? 0f : _overrideVolume", "Forced override start zeros reactive impulses during emergency or speech foreground", builder, ref failureCount);
                AssertContains(musicOverrideStart, "float overridePitchKick01 = suppressReactiveImpulses ? 0f : 1f", "Forced override start zeros pitch kicks during emergency or speech foreground", builder, ref failureCount);
                AssertContains(musicOverrideStart, "flags |= DynamicMusicScalarSignal.FlagSuppressReactiveImpulses", "Forced override start suppresses synth punches during emergency or speech foreground", builder, ref failureCount);
                AssertContains(musicOverrideStart, "else if (!foregroundSpeechActive)", "Forced override start only publishes override impulse flags when speech foreground is clear", builder, ref failureCount);
                AssertContains(soundscapeOnEnable, "CacheMusicDirector(GlobalRegistry.MusicDirector)", "Soundscape cold-seeds the music director before publishing tier context", builder, ref failureCount);
                AssertContains(soundscapeOnDisable, "_musicDirector = null", "Soundscape clears cached music director on disable", builder, ref failureCount);
                AssertContains(soundscapeOnDestroy, "_musicDirector = null", "Soundscape clears cached music director on destroy", builder, ref failureCount);
                AssertContains(soundscapeSlowTick, "SyncMusicDirectorSoundscapeContext(newTier, depth)", "Soundscape runtime mirrors depth-tier context into the music director", builder, ref failureCount);
                AssertContains(soundscapeDepthTier, "director.SetSoundscapeTierContext(CalculateTier(depthMeters, _currentTier), depthMeters)", "Biome depth-tier events refresh music soundscape context", builder, ref failureCount);
                AssertContains(soundscapeRebound, "GlobalRegistryServiceSlot.MusicDirectorRuntime", "Soundscape ref-rebinds the cached music director runtime", builder, ref failureCount);
                AssertContains(soundscapeRebound, "CacheMusicDirector(currentService as HectonMusicDirector)", "Soundscape ref-rebind stores the current music director service", builder, ref failureCount);
                AssertContains(soundscapeRebound, "SyncCachedMusicDirectorSoundscapeContext(_currentTier, survivalSystem != null ? survivalSystem.Depth : 0f)", "Soundscape ref-rebind immediately republishes current depth-tier music context without registry fallback", builder, ref failureCount);
                AssertContains(soundscapeRebound, "GlobalRegistryServiceSlot.Player", "Soundscape ref-rebind handles player runtime changes", builder, ref failureCount);
                AssertContains(soundscapeRebound, "SyncMusicDirectorSoundscapeContext(_currentTier, survivalSystem != null ? survivalSystem.Depth : 0f)", "Soundscape player ref-rebind immediately refreshes music depth context", builder, ref failureCount);
                AssertContains(soundscapeHotSwap, "GlobalRegistryServiceSlot.MusicDirectorRuntime", "Soundscape listens for music-director runtime replacement", builder, ref failureCount);
                AssertContains(soundscapeHotSwap, "CacheMusicDirector(currentService as HectonMusicDirector)", "Soundscape replacement stores the current music director service", builder, ref failureCount);
                AssertContains(soundscapeHotSwap, "SyncCachedMusicDirectorSoundscapeContext(_currentTier, survivalSystem != null ? survivalSystem.Depth : 0f)", "Soundscape replacement immediately republishes current depth-tier music context without registry fallback", builder, ref failureCount);
                AssertContains(soundscapeHotSwap, "GlobalRegistryServiceSlot.Player", "Soundscape replacement handles player runtime changes", builder, ref failureCount);
                AssertContains(soundscapeHotSwap, "SyncMusicDirectorSoundscapeContext(_currentTier, survivalSystem != null ? survivalSystem.Depth : 0f)", "Soundscape player replacement immediately refreshes music depth context", builder, ref failureCount);
                AssertContains(soundscapeAudioCache, "_audioService = IsAudioServiceUsable(audioService) ? audioService : null", "Soundscape stores only usable audio services", builder, ref failureCount);
                AssertContains(soundscapeAudioResolver, "if (IsAudioServiceUsable(audioService))", "Soundscape resolves cached audio services only while usable", builder, ref failureCount);
                AssertContains(soundscapeAudioResolver, "_audioService = null", "Soundscape clears stale audio-service references", builder, ref failureCount);
                AssertContains(soundscapeAudioUsable, "audioService is Behaviour behaviour", "Soundscape validates MonoBehaviour-backed audio service activity", builder, ref failureCount);
                AssertContains(soundscapeAudioUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Soundscape rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(soundscapeSignalDrain, "IAudioService audio = ResolveAudioService()", "Soundscape drains impact signals through the usable audio-service resolver", builder, ref failureCount);
                AssertContains(soundscapeImpactSignal, "if (!IsAudioServiceUsable(audio))", "Soundscape impact playback rechecks audio-service usability before queueing events", builder, ref failureCount);
                AssertContains(soundscapeSyncMusic, "director.SetSoundscapeTierContext(tier, depthMeters)", "Soundscape music sync is routed through one bounded helper", builder, ref failureCount);
                AssertContains(soundscapeSyncCachedMusic, "HectonMusicDirector director = _musicDirector", "Soundscape hot-swap music sync uses the cached runtime directly", builder, ref failureCount);
                AssertContains(soundscapeSyncCachedMusic, "director == null || !director.isActiveAndEnabled", "Soundscape hot-swap music sync refuses null or disabled cached runtimes", builder, ref failureCount);
                AssertContains(soundscapeSyncCachedMusic, "director.SetSoundscapeTierContext(tier, depthMeters)", "Soundscape hot-swap music sync publishes tier context to the cached runtime", builder, ref failureCount);
                AssertContains(soundscapeCacheMusic, "musicDirector != null && musicDirector.isActiveAndEnabled", "Soundscape music director cache ignores disabled runtimes", builder, ref failureCount);
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
                AssertNotContains(musicVocalWarningResolver, "GlobalRegistry.VocalWarnings", "Music director vocal-warning resolver returns cached runtime only", builder, ref failureCount);
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

            if (musicDirectorConfig.Length > 0)
            {
                const string masterMusicGroupReference = "{fileID: 1111111111, guid: 69195f25e7aad1b44a0d49cc645ff0f3, type: 2}";
                AssertContains(musicDirectorConfig, "_musicMixerGroup: " + masterMusicGroupReference, "Music director config routes bed music to the MasterMixer Music group", builder, ref failureCount);
                AssertContains(musicDirectorConfig, "_stingerMixerGroup: " + masterMusicGroupReference, "Music director config routes stingers through the music volume bus", builder, ref failureCount);
            }

            if (settingsManager.Length > 0 && masterMixer.Length > 0 && mainMenuScene.Length > 0)
            {
                string settingsMusicVolume = ExtractMethodBody(settingsManager, "public float MusicVolume");
                string settingsApplyAudio = ExtractMethodBody(settingsManager, "private bool ApplyAudioMixerSettings()");
                string settingsApplyMixer = ExtractMethodBody(settingsManager, "private bool ApplyMixerVolume(");
                AssertContains(masterMixer, "name: MusicVolume", "MasterMixer exposes MusicVolume", builder, ref failureCount);
                AssertContains(masterMixer, "m_Name: Music", "MasterMixer owns a Music mixer group", builder, ref failureCount);
                AssertContains(masterMixer, "m_GroupID: aaaaaaaa1111111111111111aaaaaaaa", "MasterMixer Music group has a stable group id", builder, ref failureCount);
                AssertContains(masterMixer, "m_Volume: 11111111111111111111111111111111", "MasterMixer Music group is bound to the MusicVolume parameter", builder, ref failureCount);
                AssertContains(mainMenuScene, "audioMixer: {fileID: 24100000, guid: 69195f25e7aad1b44a0d49cc645ff0f3, type: 2}", "Main menu SettingsManager references MasterMixer", builder, ref failureCount);
                AssertContains(settingsMusicVolume, "ApplyMixerVolume(\"MusicVolume\", clamped)", "SettingsManager MusicVolume setter writes the MusicVolume mixer parameter", builder, ref failureCount);
                AssertContains(settingsApplyAudio, "ApplyMixerVolume(\"MusicVolume\", _cachedMusicVolume)", "SettingsManager applies cached MusicVolume during audio binding", builder, ref failureCount);
                AssertContains(settingsApplyMixer, "audioMixer.SetFloat(parameterName, db)", "SettingsManager applies normalized volume as mixer dB", builder, ref failureCount);
            }

            if (prologueAcoustic.Length > 0)
            {
                string prologueLateFrame = ExtractMethodBody(prologueAcoustic, "public void LateFrameTick()");
                string prologuePublish = ExtractMethodBody(prologueAcoustic, "private void PublishAudioTransition(");
                string prologueNeutral = ExtractMethodBody(prologueAcoustic, "private void PublishNeutralTransitionOnDisable()");
                string prologueAudioCache = ExtractMethodBody(prologueAcoustic, "private void CacheAudioService(");
                string prologueAudioResolver = ExtractMethodBody(prologueAcoustic, "private IAudioService ResolveAudioService()");
                string prologueAudioUsable = ExtractMethodBody(prologueAcoustic, "private static bool IsAudioServiceUsable(");
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
                AssertContains(prologueAudioCache, "_audioService = IsAudioServiceUsable(audioService) ? audioService : null", "Prologue acoustic bridge stores only usable audio services", builder, ref failureCount);
                AssertContains(prologueAudioResolver, "if (IsAudioServiceUsable(audioService))", "Prologue acoustic bridge resolves cached audio services only while usable", builder, ref failureCount);
                AssertContains(prologueAudioResolver, "_audioService = null", "Prologue acoustic bridge clears stale audio-service references", builder, ref failureCount);
                AssertContains(prologueAudioUsable, "audioService is Behaviour behaviour", "Prologue acoustic bridge validates MonoBehaviour-backed audio service activity", builder, ref failureCount);
                AssertContains(prologueAudioUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Prologue acoustic bridge rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(prologuePublish, "IAudioService audioService = ResolveAudioService()", "Prologue transition publishing uses the usable audio-service resolver", builder, ref failureCount);
                AssertContains(prologueNeutral, "TryQueueNeutralTransition(ResolveAudioService())", "Prologue disable-neutral transition uses the usable audio-service resolver", builder, ref failureCount);
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
                string vocalTuningWrite = ExtractMethodBody(vocalWarning, "public unsafe bool EditorTryWriteTuning(");
                string vocalTuningAcquire = ExtractMethodBody(vocalWarning, "private bool TryAcquireTuningMutationView(");
                string vocalEnsureNative = ExtractMethodBody(vocalWarning, "private void EnsureNativeStorage()");
                string vocalRuntimeRegister = ExtractMethodBody(vocalWarning, "private bool TryRegisterRuntimeService()");
                string vocalRuntimeUsable = ExtractMethodBody(vocalWarning, "private static bool IsVocalWarningSystemUsable(");
                string vocalMockInject = ExtractMethodBody(vocalWarning, "public bool EditorInjectMockThreats(");
                string vocalScheduleFrame = ExtractMethodBody(vocalWarning, "private JobHandle ScheduleVocalWarningFrame(");
                string vocalVisualSync = ExtractMethodBody(vocalWarning, "private void VisualSyncPresentationTick()");
                string vocalClearQueues = ExtractMethodBody(vocalWarning, "private void CancelRendererPlaybackAndClearQueues()");
                AssertContains(vocalWarning, "ResolveGlobalQualityWeight01()", "Vocal warning system derives radio presentation from continuous global quality", builder, ref failureCount);
                AssertContains(vocalWarning, "ResolveRadioDistortion01(ref views, nextId)", "Vocal warning system resolves radio degradation through the warning payload path", builder, ref failureCount);
                AssertContains(vocalWarning, "VocalWarningTuningMutationGuardMask", "Vocal warning tuning buffer has a dedicated mutation guard", builder, ref failureCount);
                AssertContains(vocalWarning, "private bool TryResolveVwsOwnerViews(IDataVault vault, out VwsVaultViews views)", "Vocal warning owner-view resolver can bind to a specific guarded vault", builder, ref failureCount);
                AssertContains(vocalWarning, "return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);", "Vocal warning mutation guard uses DataVault active-lock lanes", builder, ref failureCount);
                AssertContains(vocalRuntimeRegister, "IVocalWarningSystem registeredVocalWarnings = GlobalRegistry.VocalWarnings", "Vocal warning snapshots the current registry owner once", builder, ref failureCount);
                AssertContains(vocalRuntimeRegister, "!ReferenceEquals(registeredVocalWarnings, null)", "Vocal warning detects stale destroyed registry references by actual reference", builder, ref failureCount);
                AssertContains(vocalRuntimeRegister, "!ReferenceEquals(registeredVocalWarnings, this)", "Vocal warning treats only other owners as registry conflicts", builder, ref failureCount);
                AssertContains(vocalRuntimeRegister, "if (IsVocalWarningSystemUsable(registeredVocalWarnings))", "Vocal warning preserves usable existing runtime owners", builder, ref failureCount);
                AssertContains(vocalRuntimeRegister, "Destroy(this);", "Vocal warning destroys duplicate components only when the existing owner is usable", builder, ref failureCount);
                AssertContains(vocalRuntimeRegister, "GlobalRegistry.UnregisterVocalWarningRuntime(registeredVocalWarnings);", "Vocal warning clears stale existing owners before registering", builder, ref failureCount);
                AssertTextBefore(vocalRuntimeRegister, "GlobalRegistry.UnregisterVocalWarningRuntime(registeredVocalWarnings);", "GlobalRegistry.RegisterVocalWarningRuntime(this);", "Vocal warning unregisters stale owners before self-register", builder, ref failureCount);
                AssertContains(vocalRuntimeUsable, "ReferenceEquals(vocalWarningSystem, null)", "Vocal warning owner usability rejects missing owners", builder, ref failureCount);
                AssertContains(vocalRuntimeUsable, "vocalWarningSystem is Behaviour behaviour", "Vocal warning validates MonoBehaviour-backed owner activity", builder, ref failureCount);
                AssertContains(vocalRuntimeUsable, "!behaviour.isActiveAndEnabled", "Vocal warning owner usability rejects disabled owners", builder, ref failureCount);
                AssertContains(vocalRuntimeUsable, "return vocalWarningSystem.IsInitialized", "Vocal warning owner usability requires initialized native storage", builder, ref failureCount);
                AssertContains(vocalTuningWrite, "TryAcquireTuningMutationView", "Vocal warning editor tuning writes acquire a guarded owner view", builder, ref failureCount);
                AssertContains(vocalTuningWrite, "ReleaseVocalWarningMutationGuard", "Vocal warning editor tuning writes release mutation guard in finally", builder, ref failureCount);
                AssertContains(vocalTuningAcquire, "TryAcquireMutationGuard(VocalWarningTuningMutationGuardMask)", "Vocal warning tuning view uses DataVault mutation guard", builder, ref failureCount);
                AssertContains(vocalTuningAcquire, "TryResolveHandle(in _tuningHandle", "Vocal warning tuning view resolves the tuning handle after guard acquisition", builder, ref failureCount);
                AssertContains(vocalEnsureNative, "TryAcquireVocalWarningFrameGuard", "Vocal warning cold storage initialization acquires the frame mutation guard", builder, ref failureCount);
                AssertContains(vocalEnsureNative, "TryResolveVwsOwnerViews(guardVault, out VwsVaultViews views)", "Vocal warning cold storage initialization resolves owner views through the guarded vault", builder, ref failureCount);
                AssertContains(vocalEnsureNative, "ReleaseVocalWarningFrameGuard", "Vocal warning cold storage initialization releases frame mutation guard in finally", builder, ref failureCount);
                AssertContains(vocalMockInject, "TryAcquireVocalWarningFrameGuard", "Vocal warning mock threat injection acquires the frame mutation guard", builder, ref failureCount);
                AssertContains(vocalMockInject, "TryResolveVwsOwnerViews(guardVault, out VwsVaultViews views)", "Vocal warning mock threat injection resolves owner views through the guarded vault", builder, ref failureCount);
                AssertContains(vocalMockInject, "ReleaseVocalWarningFrameGuard", "Vocal warning mock threat injection releases frame mutation guard in finally", builder, ref failureCount);
                AssertContains(vocalScheduleFrame, "TryResolveVwsOwnerViews(guardVault, out VwsVaultViews views)", "Vocal warning frame scheduling resolves owner views through the guarded vault", builder, ref failureCount);
                AssertContains(vocalVisualSync, "TryResolveVwsOwnerViews(guardVault, out VwsVaultViews views)", "Vocal warning visual sync resolves owner views through the guarded vault", builder, ref failureCount);
                AssertContains(vocalClearQueues, "TryAcquireVocalWarningFrameGuard", "Vocal warning teardown queue clear acquires the frame mutation guard", builder, ref failureCount);
                AssertContains(vocalClearQueues, "TryResolveVwsOwnerViews(guardVault, out VwsVaultViews views)", "Vocal warning teardown queue clear resolves owner views through the guarded vault", builder, ref failureCount);
                AssertContains(vocalClearQueues, "CancelRendererPlaybackAndClearQueues(ref views, true)", "Vocal warning teardown queue clear mutates Vault queues only through guarded owner views", builder, ref failureCount);
                AssertContains(vocalClearQueues, "ReleaseVocalWarningFrameGuard", "Vocal warning teardown queue clear releases frame mutation guard in finally", builder, ref failureCount);
                AssertNotContains(vocalWarning, "ConsumeScalabilitySignals();", "Vocal warning system no longer drains binary scalability changes", builder, ref failureCount);
                AssertNotContains(vocalWarning, "TryAcquireWriteLock", "Vocal warning system avoids direct DataVault write locks", builder, ref failureCount);
                AssertNotContains(vocalWarning, "ReleaseWriteLock", "Vocal warning system avoids direct DataVault write-lock release calls", builder, ref failureCount);
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

            if (vocalBankRuntime.Length > 0)
            {
                string vocalReleaseCallback = ExtractMethodBodyAfter(vocalBankRuntime, "#if !UNITY_EDITOR && !DEVELOPMENT_BUILD", "private void OnAudioFilterRead(float[] data, int channels)");
                AssertContains(vocalReleaseCallback, "ZeroManagedAudioBuffer(data", "Release vocal audio callback fails closed by zero-filling the managed buffer", builder, ref failureCount);
                AssertNotContains(vocalReleaseCallback, "VocalDecodeKernel.DecodeIntoAudioBuffer", "Release vocal audio callback does not decode speech on the managed audio thread", builder, ref failureCount);
                AssertNotContains(vocalReleaseCallback, "TryAcquireAudioCallbackViews", "Release vocal audio callback does not acquire DataVault views", builder, ref failureCount);
                AssertNotContains(vocalReleaseCallback, "Stopwatch.GetTimestamp", "Release vocal audio callback does not measure timing on the managed audio thread", builder, ref failureCount);
                AssertNotContains(vocalReleaseCallback, "GlobalRegistry", "Release vocal audio callback does not query runtime registries", builder, ref failureCount);
            }

            if (playerThrusterAudio.Length > 0)
            {
                string thrusterColdRuntime = ExtractMethodBody(playerThrusterAudio, "private void RefreshRuntimeAudioServicesCold()");
                string thrusterCacheSpatial = ExtractMethodBody(playerThrusterAudio, "private void CacheSpatialAudioManager(");
                string thrusterMixerRoute = ExtractMethodBody(playerThrusterAudio, "private void TryAssignMixerRoute(");
                string thrusterRouteResolver = ExtractMethodBody(playerThrusterAudio, "private ISpatialAudioSfxMixerRouteReadModel ResolveSpatialAudioSfxRoute()");
                string thrusterAudioUsable = ExtractMethodBody(playerThrusterAudio, "private static bool IsAudioServiceUsable(");
                string thrusterObjectUsable = ExtractMethodBody(playerThrusterAudio, "private static bool IsAudioRuntimeObjectUsable(");
                AssertContains(playerThrusterAudio, "IGlobalRegistryHotSwapRefListener", "Player thruster fallback audio listens for audio service rebinding", builder, ref failureCount);
                AssertContains(thrusterColdRuntime, "GlobalRegistry.Audio", "Player thruster fallback resolves audio service only during cold runtime refresh", builder, ref failureCount);
                AssertContains(playerThrusterAudio, "GlobalRegistryServiceSlot.Audio", "Player thruster fallback handles audio service hot swaps", builder, ref failureCount);
                AssertContains(thrusterCacheSpatial, "IsAudioServiceUsable(audioService)", "Player thruster fallback stores only usable audio services", builder, ref failureCount);
                AssertContains(thrusterMixerRoute, "ResolveSpatialAudioSfxRoute()", "Player thruster mixer route uses the usable spatial-audio SFX route resolver", builder, ref failureCount);
                AssertContains(thrusterRouteResolver, "if (IsAudioRuntimeObjectUsable(route))", "Player thruster spatial SFX route rejects stale Unity-backed runtimes", builder, ref failureCount);
                AssertContains(thrusterRouteResolver, "_cachedSpatialAudioSfxRoute = null", "Player thruster clears stale spatial SFX route references", builder, ref failureCount);
                AssertContains(thrusterAudioUsable, "audioService == null || !audioService.IsInitialized", "Player thruster validates audio service initialization before route caching", builder, ref failureCount);
                AssertContains(thrusterAudioUsable, "return IsAudioRuntimeObjectUsable(audioService)", "Player thruster reuses Unity object activity validation for audio services", builder, ref failureCount);
                AssertContains(thrusterObjectUsable, "runtime is IAudioService audioService && !audioService.IsInitialized", "Player thruster rejects deinitialized cached audio runtime interfaces", builder, ref failureCount);
                AssertContains(thrusterObjectUsable, "runtime is Behaviour behaviour", "Player thruster validates MonoBehaviour-backed audio runtime activity", builder, ref failureCount);
                AssertContains(thrusterObjectUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Player thruster rejects destroyed or disabled audio runtimes", builder, ref failureCount);
                AssertNotContains(thrusterMixerRoute, "GlobalRegistry.Audio", "Player thruster mixer route does not poll audio registry directly", builder, ref failureCount);
            }

            if (playerFootstepAudio.Length > 0)
            {
                string footstepHotSwap = ExtractMethodBody(playerFootstepAudio, "public void OnGlobalRegistryServiceReplaced(");
                string footstepColdRuntime = ExtractMethodBody(playerFootstepAudio, "private void RefreshColdRegistryReferences()");
                string footstepCache = ExtractMethodBody(playerFootstepAudio, "private void CacheAudioService(");
                string footstepResolver = ExtractMethodBody(playerFootstepAudio, "private IAudioService ResolveAudioService()");
                string footstepUsable = ExtractMethodBody(playerFootstepAudio, "private static bool IsAudioServiceUsable(");
                string footstepHandle = ExtractMethodBody(playerFootstepAudio, "private void HandleFootstep()");
                AssertContains(footstepHotSwap, "CacheAudioService(currentService as IAudioService)", "Player footstep audio caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(footstepColdRuntime, "CacheAudioService(GlobalRegistry.Audio)", "Player footstep audio cold-caches audio service through the usable-service filter", builder, ref failureCount);
                AssertContains(footstepCache, "_audioService = IsAudioServiceUsable(audioService) ? audioService : null", "Player footstep audio stores only usable audio services", builder, ref failureCount);
                AssertContains(footstepResolver, "if (IsAudioServiceUsable(audioService))", "Player footstep audio resolves cached audio services only while usable", builder, ref failureCount);
                AssertContains(footstepResolver, "_audioService = null", "Player footstep audio clears stale audio-service references", builder, ref failureCount);
                AssertContains(footstepUsable, "audioService is Behaviour behaviour", "Player footstep audio validates MonoBehaviour-backed audio service activity", builder, ref failureCount);
                AssertContains(footstepUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Player footstep audio rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(footstepHandle, "IAudioService sam = ResolveAudioService()", "Player footstep playback uses the usable audio-service resolver", builder, ref failureCount);
                AssertNotContains(footstepHandle, "GlobalRegistry.Audio", "Player footstep playback does not poll audio registry directly", builder, ref failureCount);
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
                string spectrumHotSwap = ExtractMethodBody(spectrumSystem, "public void OnGlobalRegistryServiceReplaced(");
                string spectrumColdCache = ExtractMethodBody(spectrumSystem, "private void CacheRegistryServicesCold()");
                string spectrumClearRegistry = ExtractMethodBody(spectrumSystem, "private void ClearCachedRegistryServices()");
                string spectrumCache = ExtractMethodBody(spectrumSystem, "private void CacheAudioService(");
                string spectrumResolve = ExtractMethodBody(spectrumSystem, "private IAudioService ResolveAudioService()");
                string spectrumClearAudio = ExtractMethodBody(spectrumSystem, "private void ClearCachedAudioService()");
                string spectrumUsable = ExtractMethodBody(spectrumSystem, "private static bool IsAudioServiceUsable(");
                string spectrumFlushAudio = ExtractMethodBody(spectrumSystem, "private void FlushQueuedSpectrumAudio()");
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
                AssertContains(spectrumHotSwap, "CacheAudioService(currentService as IAudioService)", "Spectrum system caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(spectrumColdCache, "CacheAudioService(GlobalRegistry.Audio)", "Spectrum system cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(spectrumClearRegistry, "ClearCachedAudioService()", "Spectrum system clears audio cache through its dependent-field helper", builder, ref failureCount);
                AssertContains(spectrumCache, "if (!IsAudioServiceUsable(audioService))", "Spectrum system cache rejects unusable audio services", builder, ref failureCount);
                AssertContains(spectrumCache, "_spatialAudioEmitterReadModel = _audioService as ISpatialAudioWorldEmitterReadModel", "Spectrum system derives spatial emitter read model only from usable audio service", builder, ref failureCount);
                AssertContains(spectrumResolve, "if (IsAudioServiceUsable(audioService))", "Spectrum system resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(spectrumResolve, "ClearCachedAudioService()", "Spectrum system resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(spectrumClearAudio, "_audioService = null", "Spectrum system audio clear resets cached audio service", builder, ref failureCount);
                AssertContains(spectrumClearAudio, "_spatialAudioEmitterReadModel = null", "Spectrum system audio clear resets dependent spatial read model", builder, ref failureCount);
                AssertContains(spectrumUsable, "audioService == null || !audioService.IsInitialized", "Spectrum system rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(spectrumUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Spectrum system rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(spectrumFlushAudio, "Hecton8.Core.IAudioService audioManager = ResolveAudioService()", "Spectrum system queued audio uses the usable audio-service resolver", builder, ref failureCount);
                AssertContains(spectrumFlushAudio, "audioManager.PlayStatic2D", "Spectrum system anchor-return cue remains a 2D interface sound", builder, ref failureCount);
                AssertNotContains(spectrumFlushAudio, "Hecton8.Core.IAudioService audioManager = _audioService", "Spectrum system queued audio never trusts the raw cached audio-service field", builder, ref failureCount);
            }

            if (resourceNode.Length > 0)
            {
                AssertNotContains(resourceNode, "ISonarPingEventListener", "Resource nodes do not subscribe to the active-sonar object loop", builder, ref failureCount);
                AssertNotContains(resourceNode, "RegisterSonarPingListener(this)", "Resource sonar reflection remains shader-authored instead of per-node C# dispatch", builder, ref failureCount);
            }

            if (destructibleOrganicManager.Length > 0)
            {
                string organicHotSwap = ExtractMethodBody(destructibleOrganicManager, "public void OnGlobalRegistryServiceReplaced(");
                string organicColdCache = ExtractMethodBody(destructibleOrganicManager, "private void CacheRegistryServicesCold()");
                string organicClearRegistry = ExtractMethodBody(destructibleOrganicManager, "private void ClearCachedRegistryServices()");
                string organicCache = ExtractMethodBody(destructibleOrganicManager, "private void CacheAudioService(");
                string organicResolve = ExtractMethodBody(destructibleOrganicManager, "private IAudioService ResolveAudioService()");
                string organicSinkResolve = ExtractMethodBody(destructibleOrganicManager, "private ISpatialAudioHarvestPlaybackSink ResolveHarvestAudioSink()");
                string organicClearAudio = ExtractMethodBody(destructibleOrganicManager, "private void ClearCachedAudioService()");
                string organicUsable = ExtractMethodBody(destructibleOrganicManager, "private static bool IsAudioServiceUsable(");
                string organicDispatchHarvest = ExtractMethodBody(destructibleOrganicManager, "private void DispatchHarvestAudioTransition(");
                string organicDispatchSpore = ExtractMethodBody(destructibleOrganicManager, "private void DispatchSporeAcousticEvent(");
                string organicFlushHarvest = ExtractMethodBody(destructibleOrganicManager, "private void FlushPendingHarvestAudioEvents()");
                AssertContains(organicHotSwap, "CacheAudioService(currentService as IAudioService)", "Destructible organic manager caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(organicColdCache, "CacheAudioService(GlobalRegistry.Audio)", "Destructible organic manager cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(organicClearRegistry, "ClearCachedAudioService()", "Destructible organic manager clears audio cache through its dependent-field helper", builder, ref failureCount);
                AssertContains(organicCache, "if (!IsAudioServiceUsable(audioService))", "Destructible organic manager cache rejects unusable audio services", builder, ref failureCount);
                AssertContains(organicCache, "_harvestAudioSink = _audioService as ISpatialAudioHarvestPlaybackSink", "Destructible organic manager derives harvest sink only from usable audio service", builder, ref failureCount);
                AssertContains(organicResolve, "if (IsAudioServiceUsable(audioService))", "Destructible organic manager resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(organicResolve, "ClearCachedAudioService()", "Destructible organic manager resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(organicSinkResolve, "ResolveAudioService() != null ? _harvestAudioSink : null", "Destructible organic manager gates harvest sink behind usable audio service", builder, ref failureCount);
                AssertContains(organicClearAudio, "_audioService = null", "Destructible organic manager audio clear resets cached audio service", builder, ref failureCount);
                AssertContains(organicClearAudio, "_harvestAudioSink = null", "Destructible organic manager audio clear resets dependent harvest sink", builder, ref failureCount);
                AssertContains(organicUsable, "audioService == null || !audioService.IsInitialized", "Destructible organic manager rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(organicUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Destructible organic manager rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(organicDispatchHarvest, "ResolveHarvestAudioSink() != null", "Destructible organic harvest events only mark AUP route when harvest sink is usable", builder, ref failureCount);
                AssertContains(organicDispatchSpore, "ISpatialAudioHarvestPlaybackSink harvestAudioSink = ResolveHarvestAudioSink()", "Destructible organic spore audio resolves only usable harvest sink", builder, ref failureCount);
                AssertContains(organicDispatchSpore, "IAudioService audioService = ResolveAudioService()", "Destructible organic spore fallback uses the usable audio-service resolver", builder, ref failureCount);
                AssertContains(organicFlushHarvest, "ISpatialAudioHarvestPlaybackSink harvestAudioSink = ResolveHarvestAudioSink()", "Destructible organic harvest audio resolves only usable harvest sink", builder, ref failureCount);
                AssertContains(organicFlushHarvest, "IAudioService audioService = ResolveAudioService()", "Destructible organic harvest fallback uses the usable audio-service resolver", builder, ref failureCount);
                AssertNotContains(organicDispatchSpore, "_audioService?.PlayAtPoint", "Destructible organic spore fallback never calls through raw cached audio service", builder, ref failureCount);
                AssertNotContains(organicFlushHarvest, "_audioService?.PlayAtPoint", "Destructible organic harvest fallback never calls through raw cached audio service", builder, ref failureCount);
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

            if (modCommandDispatcher.Length > 0)
            {
                string dispatcherHotSwap = ExtractMethodBody(modCommandDispatcher, "internal static void OnGlobalRegistryServiceReplaced(");
                string dispatcherColdCache = ExtractMethodBody(modCommandDispatcher, "internal static void BindRegistryServicesCold()");
                string dispatcherCache = ExtractMethodBody(modCommandDispatcher, "private static void CacheAudioService(");
                string dispatcherResolve = ExtractMethodBody(modCommandDispatcher, "private static IAudioService ResolveAudioService()");
                string dispatcherUsable = ExtractMethodBody(modCommandDispatcher, "private static bool IsAudioServiceUsable(");
                string dispatcherPing = ExtractMethodBody(modCommandDispatcher, "private static void ExecuteModAcousticPing(");
                AssertContains(dispatcherHotSwap, "CacheAudioService(currentService as IAudioService)", "Mod command dispatcher caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(dispatcherColdCache, "CacheAudioService(GlobalRegistry.Audio)", "Mod command dispatcher cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(dispatcherCache, "_audioService = IsAudioServiceUsable(audioService) ? audioService : null", "Mod command dispatcher stores only usable audio services", builder, ref failureCount);
                AssertContains(dispatcherResolve, "if (IsAudioServiceUsable(audioService))", "Mod command dispatcher resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(dispatcherResolve, "_audioService = null", "Mod command dispatcher resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(dispatcherUsable, "audioService == null || !audioService.IsInitialized", "Mod command dispatcher rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(dispatcherUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Mod command dispatcher rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(dispatcherPing, "IAudioService audioManager = ResolveAudioService()", "Mod command dispatcher acoustic ping uses the usable audio-service resolver", builder, ref failureCount);
                AssertContains(dispatcherPing, "audioManager.TryEmitModAcousticPing", "Mod command dispatcher acoustic ping remains engine-owned", builder, ref failureCount);
                AssertNotContains(dispatcherHotSwap, "_audioService = currentService as IAudioService", "Mod command dispatcher hot-swap never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(dispatcherColdCache, "_audioService = GlobalRegistry.Audio", "Mod command dispatcher cold cache never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(dispatcherPing, "IAudioService audioManager = _audioService", "Mod command dispatcher acoustic ping never trusts the raw cached audio-service field", builder, ref failureCount);
            }

            if (baseModule.Length > 0)
            {
                string baseColdCache = ExtractMethodBody(baseModule, "private void CacheRegistryServicesCold()");
                string baseHotSwap = ExtractMethodBody(baseModule, "public void OnGlobalRegistryServiceReplaced(");
                string baseCache = ExtractMethodBody(baseModule, "private void CacheAudioService(");
                string baseResolve = ExtractMethodBody(baseModule, "private Hecton8.Core.IAudioService ResolveAudioService()");
                string baseRouteResolve = ExtractMethodBody(baseModule, "private ISpatialAudioSfxMixerRouteReadModel ResolveSpatialAudioSfxRoute()");
                string baseClear = ExtractMethodBody(baseModule, "private void ClearCachedAudioService()");
                string baseUsable = ExtractMethodBody(baseModule, "private static bool IsAudioServiceUsable(");
                string baseRoute = ExtractMethodBody(baseModule, "private void TryRouteAudioSourceToSfxGroup(");
                string baseFlush = ExtractMethodBody(baseModule, "private void FlushPendingSpatialSfx()");
                AssertContains(baseColdCache, "CacheAudioService(Hecton8.Core.GlobalRegistry.Audio)", "Base module cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(baseHotSwap, "CacheAudioService(currentService as Hecton8.Core.IAudioService)", "Base module caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(baseCache, "if (!IsAudioServiceUsable(audioService))", "Base module rejects unusable audio services while caching", builder, ref failureCount);
                AssertContains(baseCache, "_cachedSpatialAudioSfxRoute = audioService as ISpatialAudioSfxMixerRouteReadModel", "Base module refreshes SFX route only from usable audio service", builder, ref failureCount);
                AssertContains(baseResolve, "if (IsAudioServiceUsable(audioService))", "Base module resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(baseResolve, "ClearCachedAudioService()", "Base module resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(baseRouteResolve, "ResolveAudioService() != null ? _cachedSpatialAudioSfxRoute : null", "Base module SFX route resolver is gated by usable audio service", builder, ref failureCount);
                AssertContains(baseClear, "_cachedSpatialAudioSfxRoute = null", "Base module clears dependent SFX route with audio cache", builder, ref failureCount);
                AssertContains(baseUsable, "audioService == null || !audioService.IsInitialized", "Base module rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(baseUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Base module rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(baseRoute, "ResolveSpatialAudioSfxRoute()", "Base module audio-source route uses the usable audio route resolver", builder, ref failureCount);
                AssertContains(baseFlush, "Hecton8.Core.IAudioService sam = ResolveAudioService()", "Base module spatial SFX flush uses the usable audio-service resolver", builder, ref failureCount);
                AssertNotContains(baseColdCache, "_cachedAudioService = Hecton8.Core.GlobalRegistry.Audio", "Base module cold cache never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(baseHotSwap, "_cachedAudioService = currentService as Hecton8.Core.IAudioService", "Base module hot-swap never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(baseFlush, "Hecton8.Core.IAudioService sam = _cachedAudioService", "Base module spatial SFX flush never trusts the raw cached audio-service field", builder, ref failureCount);
            }

            if (repairTool.Length > 0)
            {
                string repairHotSwap = ExtractMethodBody(repairTool, "protected override void OnToolRegistryServiceReplaced(");
                string repairColdCache = ExtractMethodBody(repairTool, "private void CacheRepairAudioCold()");
                string repairCache = ExtractMethodBody(repairTool, "private void CacheRepairAudioMixerGroup(");
                string repairUsable = ExtractMethodBody(repairTool, "private static bool IsAudioServiceUsable(");
                AssertContains(repairHotSwap, "CacheRepairAudioMixerGroup(currentService as IAudioService)", "Repair tool caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(repairColdCache, "CacheRepairAudioMixerGroup(GlobalRegistry.Audio)", "Repair tool cold-caches loop mixer route through the usable-service filter", builder, ref failureCount);
                AssertContains(repairCache, "_cachedRepairAudioMixerGroup = IsAudioServiceUsable(audioService)", "Repair tool stores only mixer groups from usable audio services", builder, ref failureCount);
                AssertContains(repairUsable, "audioService == null || !audioService.IsInitialized", "Repair tool rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(repairUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Repair tool rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertNotContains(repairHotSwap, "_cachedRepairAudioMixerGroup = currentService is IAudioService audioService", "Repair tool hot-swap never derives mixer group from raw audio service directly", builder, ref failureCount);
                AssertNotContains(repairColdCache, "_cachedRepairAudioMixerGroup = audioService != null", "Repair tool cold cache never derives mixer group from raw audio service directly", builder, ref failureCount);
            }

            if (suitHudOverlay.Length > 0)
            {
                string suitColdCache = ExtractMethodBody(suitHudOverlay, "private bool CacheRuntimeDependenciesCold()");
                string suitHotSwap = ExtractMethodBody(suitHudOverlay, "public void OnGlobalRegistryServiceReplaced(");
                string suitCache = ExtractMethodBody(suitHudOverlay, "private void CacheAudioService(");
                string suitResolve = ExtractMethodBody(suitHudOverlay, "private Hecton8.Core.IAudioService ResolveAudioService()");
                string suitUsable = ExtractMethodBody(suitHudOverlay, "private static bool IsAudioServiceUsable(");
                string suitRadar = ExtractMethodBody(suitHudOverlay, "private void RefreshAcousticRadarPayload()");
                AssertContains(suitColdCache, "CacheAudioService(GlobalRegistry.Audio)", "Suit HUD cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(suitHotSwap, "CacheAudioService(currentService as IAudioService)", "Suit HUD caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(suitCache, "_spatialAudioManager = IsAudioServiceUsable(audioService) ? audioService : null", "Suit HUD stores only usable audio services", builder, ref failureCount);
                AssertContains(suitResolve, "if (IsAudioServiceUsable(audioService))", "Suit HUD resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(suitResolve, "_spatialAudioManager = null", "Suit HUD resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(suitUsable, "audioService == null || !audioService.IsInitialized", "Suit HUD rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(suitUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Suit HUD rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(suitRadar, "Hecton8.Core.IAudioService audioManager = ResolveAudioService()", "Suit HUD acoustic radar reads use the usable audio-service resolver", builder, ref failureCount);
                AssertContains(suitRadar, "audioManager = ResolveAudioService()", "Suit HUD acoustic radar upload revalidates the audio service", builder, ref failureCount);
                AssertNotContains(suitColdCache, "_spatialAudioManager = GlobalRegistry.Audio", "Suit HUD cold cache never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(suitHotSwap, "_spatialAudioManager = currentService as IAudioService", "Suit HUD hot-swap never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(suitRadar, "_spatialAudioManager.TryGetAcousticRadarPayload", "Suit HUD acoustic radar read never trusts the raw cached audio-service field", builder, ref failureCount);
                AssertNotContains(suitRadar, "_spatialAudioManager.TryUploadAcousticRadarPayload", "Suit HUD acoustic radar upload never trusts the raw cached audio-service field", builder, ref failureCount);
            }

            if (submarineAutoLevelBallast.Length > 0)
            {
                string ballastHotSwap = ExtractMethodBody(submarineAutoLevelBallast, "public void OnGlobalRegistryServiceReplaced(");
                string ballastRegister = ExtractMethodBody(submarineAutoLevelBallast, "private void RegisterRuntime()");
                string ballastCache = ExtractMethodBody(submarineAutoLevelBallast, "private void CacheAudioService(");
                string ballastClear = ExtractMethodBody(submarineAutoLevelBallast, "private void ClearCachedAudioService()");
                string ballastUsable = ExtractMethodBody(submarineAutoLevelBallast, "private static bool IsAudioServiceUsable(");
                AssertContains(ballastHotSwap, "CacheAudioService(currentService as IAudioService)", "Ballast controller caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(ballastRegister, "CacheAudioService(GlobalRegistry.Audio)", "Ballast controller cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(ballastCache, "_audio = IsAudioServiceUsable(audioService) ? audioService : null", "Ballast controller stores only usable audio services", builder, ref failureCount);
                AssertContains(ballastClear, "_audio = null", "Ballast controller clears cached audio service references", builder, ref failureCount);
                AssertContains(ballastUsable, "audioService == null || !audioService.IsInitialized", "Ballast controller rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(ballastUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Ballast controller rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertNotContains(ballastHotSwap, "_audio = currentService as IAudioService", "Ballast controller hot-swap never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(ballastRegister, "_audio = GlobalRegistry.Audio", "Ballast controller cold cache never stores raw audio service directly", builder, ref failureCount);
            }

            if (signalBeacon.Length > 0)
            {
                string beaconHotSwap = ExtractMethodBody(signalBeacon, "public void OnGlobalRegistryServiceReplaced(");
                string beaconColdCache = ExtractMethodBody(signalBeacon, "private void CacheRegistryServicesCold()");
                string beaconCache = ExtractMethodBody(signalBeacon, "private void CacheSpatialAudio(");
                string beaconResolve = ExtractMethodBody(signalBeacon, "private ISpatialAudioListenerCaveReadModel ResolveSpatialAudio()");
                string beaconCave = ExtractMethodBody(signalBeacon, "private float ResolveCaveErrorMultiplier()");
                AssertContains(beaconHotSwap, "CacheSpatialAudio(currentService)", "Signal beacon caches spatial audio hot swaps through the usable-runtime filter", builder, ref failureCount);
                AssertContains(beaconColdCache, "CacheSpatialAudio(GlobalRegistry.Audio)", "Signal beacon cold-caches spatial audio through the usable-runtime filter", builder, ref failureCount);
                AssertContains(beaconCache, "_spatialAudio = IsAudioRuntimeObjectUsable(audioRuntime)", "Signal beacon stores cave read-models only from usable audio runtimes", builder, ref failureCount);
                AssertContains(beaconResolve, "if (IsAudioRuntimeObjectUsable(spatialAudio))", "Signal beacon resolves cave read-models only while usable", builder, ref failureCount);
                AssertContains(beaconCave, "ResolveSpatialAudio()", "Signal beacon cave error reads spatial audio through the usable resolver", builder, ref failureCount);
                AssertNotContains(beaconHotSwap, "_spatialAudio = currentService as", "Signal beacon hot-swap never stores raw spatial audio directly", builder, ref failureCount);
                AssertNotContains(beaconColdCache, "_spatialAudio = GlobalRegistry.Audio as", "Signal beacon cold cache never stores raw spatial audio directly", builder, ref failureCount);
            }

            if (narrativeDirector.Length > 0)
            {
                string narrativeHotSwap = ExtractMethodBody(narrativeDirector, "public void OnGlobalRegistryServiceReplaced(");
                string narrativeColdCache = ExtractMethodBody(narrativeDirector, "private void CacheRegistryReadModelsCold()");
                string narrativeCache = ExtractMethodBody(narrativeDirector, "private void CacheNarrativeAudioSink(");
                AssertContains(narrativeHotSwap, "CacheNarrativeAudioSink(currentService)", "Narrative director caches narrative audio hot swaps through the usable-runtime filter", builder, ref failureCount);
                AssertContains(narrativeColdCache, "CacheNarrativeAudioSink(GlobalRegistry.Audio)", "Narrative director cold-caches narrative audio through the usable-runtime filter", builder, ref failureCount);
                AssertContains(narrativeCache, "_narrativeAudioSink = IsAudioRuntimeObjectUsable(audioRuntime)", "Narrative director stores narrative audio sinks only from usable runtimes", builder, ref failureCount);
                AssertNotContains(narrativeHotSwap, "_narrativeAudioSink = currentService as", "Narrative director hot-swap never stores raw narrative audio directly", builder, ref failureCount);
                AssertNotContains(narrativeColdCache, "_narrativeAudioSink = GlobalRegistry.Audio as", "Narrative director cold cache never stores raw narrative audio directly", builder, ref failureCount);
            }

            if (traumaDispatcher.Length > 0)
            {
                string traumaHotSwap = ExtractMethodBody(traumaDispatcher, "public void OnGlobalRegistryServiceReplaced(");
                string traumaColdCache = ExtractMethodBody(traumaDispatcher, "private void CacheRegistryServicesCold()");
                string traumaCache = ExtractMethodBody(traumaDispatcher, "private void CacheSpatialAudioSink(");
                string traumaResolve = ExtractMethodBody(traumaDispatcher, "private ISpatialAudioEnvironmentModulationSink ResolveSpatialAudioSink()");
                string traumaFlush = ExtractMethodBody(traumaDispatcher, "private void FlushParasiteAudioLoad()");
                AssertContains(traumaHotSwap, "CacheSpatialAudioSink(currentService)", "Trauma dispatcher caches spatial audio hot swaps through the usable-runtime filter", builder, ref failureCount);
                AssertContains(traumaColdCache, "CacheSpatialAudioSink(GlobalRegistry.Audio)", "Trauma dispatcher cold-caches spatial audio through the usable-runtime filter", builder, ref failureCount);
                AssertContains(traumaCache, "_spatialAudioSink = IsAudioRuntimeObjectUsable(audioRuntime)", "Trauma dispatcher stores modulation sinks only from usable audio runtimes", builder, ref failureCount);
                AssertContains(traumaResolve, "if (IsAudioRuntimeObjectUsable(spatialAudioSink))", "Trauma dispatcher resolves modulation sinks only while usable", builder, ref failureCount);
                AssertContains(traumaFlush, "ISpatialAudioEnvironmentModulationSink spatialAudioSink = ResolveSpatialAudioSink()", "Trauma dispatcher parasite audio load uses the usable sink resolver", builder, ref failureCount);
                AssertNotContains(traumaFlush, "_spatialAudioSink.SetParasiteRoomAcousticLoad", "Trauma dispatcher parasite audio load never trusts the raw cached sink field", builder, ref failureCount);
            }

            if (randomEventSystem.Length > 0)
            {
                string randomHotSwap = ExtractMethodBody(randomEventSystem, "public void OnGlobalRegistryServiceReplaced(");
                string randomColdCache = ExtractMethodBody(randomEventSystem, "private void CacheRegistryServicesCold()");
                string randomCache = ExtractMethodBody(randomEventSystem, "private void CacheMeteorShowerAudioSink(");
                string randomResolve = ExtractMethodBody(randomEventSystem, "private IMeteorShowerAudioSink ResolveMeteorShowerAudioSink()");
                string randomMeteorBoom = ExtractMethodBody(randomEventSystem, "private void TryPublishMeteorBoom(");
                string randomWaterBoom = ExtractMethodBody(randomEventSystem, "private void TickMeteorWaterBoomDelay(");
                AssertContains(randomHotSwap, "CacheMeteorShowerAudioSink(currentService)", "Random events cache meteor audio hot swaps through the usable-runtime filter", builder, ref failureCount);
                AssertContains(randomColdCache, "CacheMeteorShowerAudioSink(GlobalRegistry.Audio)", "Random events cold-cache meteor audio through the usable-runtime filter", builder, ref failureCount);
                AssertContains(randomCache, "_cachedSpatialAudioManager = IsAudioRuntimeObjectUsable(audioRuntime)", "Random events store meteor sinks only from usable audio runtimes", builder, ref failureCount);
                AssertContains(randomResolve, "if (IsAudioRuntimeObjectUsable(spatialAudioManager))", "Random events resolve meteor sinks only while usable", builder, ref failureCount);
                AssertContains(randomMeteorBoom, "IMeteorShowerAudioSink spatialAudioManager = ResolveMeteorShowerAudioSink()", "Random events meteor booms use the usable sink resolver", builder, ref failureCount);
                AssertContains(randomWaterBoom, "IMeteorShowerAudioSink spatialAudioManager = ResolveMeteorShowerAudioSink()", "Random events water booms use the usable sink resolver", builder, ref failureCount);
                AssertNotContains(randomMeteorBoom, "IMeteorShowerAudioSink spatialAudioManager = _cachedSpatialAudioManager", "Random events meteor booms never trust the raw cached sink field", builder, ref failureCount);
            }

            if (eclipseGameplaySystem.Length > 0)
            {
                string eclipseOnEnable = ExtractMethodBody(eclipseGameplaySystem, "private void OnEnable()");
                string eclipseHotSwap = ExtractMethodBody(eclipseGameplaySystem, "public void OnGlobalRegistryServiceReplaced(");
                string eclipsePublish = ExtractMethodBody(eclipseGameplaySystem, "private void PublishEclipseAcousticPitchShift(");
                AssertContains(eclipseOnEnable, "CacheSpatialAudioSink(GlobalRegistry.Audio)", "Eclipse gameplay cold-caches spatial audio through the usable-runtime filter", builder, ref failureCount);
                AssertContains(eclipseHotSwap, "CacheSpatialAudioSink(currentService)", "Eclipse gameplay caches spatial audio hot swaps through the usable-runtime filter", builder, ref failureCount);
                AssertContains(eclipseHotSwap, "_currentAcousticPitchShiftCents = float.NaN", "Eclipse gameplay replays current pitch shift to a replacement audio runtime", builder, ref failureCount);
                AssertContains(eclipsePublish, "ISpatialAudioEnvironmentModulationSink spatialAudio = ResolveSpatialAudioSink()", "Eclipse gameplay pitch shift uses the usable sink resolver", builder, ref failureCount);
                AssertNotContains(eclipsePublish, "GlobalRegistry.Audio is ISpatialAudioEnvironmentModulationSink", "Eclipse gameplay pitch shift does not poll audio registry directly", builder, ref failureCount);
            }

            if (acousticRadarSphereRenderer.Length > 0 && sonarHoloCompass.Length > 0)
            {
                string radarHotSwap = ExtractMethodBody(acousticRadarSphereRenderer, "public void OnGlobalRegistryServiceReplaced(");
                string radarColdCache = ExtractMethodBody(acousticRadarSphereRenderer, "private void CacheRegistryServicesCold()");
                string radarRefresh = ExtractMethodBody(acousticRadarSphereRenderer, "private void RefreshMatricesForLateFrame()");
                string compassHotSwap = ExtractMethodBody(sonarHoloCompass, "public void OnGlobalRegistryServiceReplaced(");
                string compassColdCache = ExtractMethodBody(sonarHoloCompass, "private void CacheRegistryServicesCold()");
                string compassProjection = ExtractMethodBody(sonarHoloCompass, "private void AdvanceCompassProjection(");
                AssertContains(radarHotSwap, "CacheImpactEmitterReadModel(currentService)", "Acoustic radar caches impact emitters through the usable-runtime filter", builder, ref failureCount);
                AssertContains(radarColdCache, "CacheImpactEmitterReadModel(GlobalRegistry.Audio)", "Acoustic radar cold-caches impact emitters through the usable-runtime filter", builder, ref failureCount);
                AssertContains(radarRefresh, "ISpatialAudioImpactEmitterReadModel audioManager = ResolveImpactEmitterReadModel()", "Acoustic radar refresh uses the usable impact-emitter resolver", builder, ref failureCount);
                AssertContains(compassHotSwap, "CacheImpactEmitterReadModel(currentService)", "Sonar compass caches impact emitters through the usable-runtime filter", builder, ref failureCount);
                AssertContains(compassColdCache, "CacheImpactEmitterReadModel(GlobalRegistry.Audio)", "Sonar compass cold-caches impact emitters through the usable-runtime filter", builder, ref failureCount);
                AssertContains(compassProjection, "ISpatialAudioImpactEmitterReadModel audioManager = ResolveImpactEmitterReadModel()", "Sonar compass projection uses the usable impact-emitter resolver", builder, ref failureCount);
                AssertNotContains(radarRefresh, "ISpatialAudioImpactEmitterReadModel audioManager = _cachedAudioManager", "Acoustic radar never trusts the raw cached impact-emitter field", builder, ref failureCount);
                AssertNotContains(compassProjection, "ISpatialAudioImpactEmitterReadModel audioManager = _cachedAudioManager", "Sonar compass never trusts the raw cached impact-emitter field", builder, ref failureCount);
            }

            if (sceneRuntime.Length > 0)
            {
                string sceneHotSwap = ExtractMethodBody(sceneRuntime, "public void OnGlobalRegistryServiceReplaced(");
                string sceneColdCache = ExtractMethodBody(sceneRuntime, "private void RefreshTerminalBootServiceHandlesCold()");
                string sceneBegin = ExtractMethodBody(sceneRuntime, "private void BeginWorldDroneCrossfade()");
                string sceneUpdate = ExtractMethodBody(sceneRuntime, "private void UpdateWorldDroneCrossfade(");
                AssertContains(sceneHotSwap, "CacheSceneTransitionAudioBridge(currentService)", "Scene runtime caches transition audio hot swaps through the usable-runtime filter", builder, ref failureCount);
                AssertContains(sceneColdCache, "CacheSceneTransitionAudioBridge(GlobalRegistry.Audio)", "Scene runtime cold-caches transition audio through the usable-runtime filter", builder, ref failureCount);
                AssertContains(sceneBegin, "ISceneTransitionAudioBridge spatialAudio = ResolveSceneTransitionAudioBridge()", "Scene runtime world-drone begin uses the usable bridge resolver", builder, ref failureCount);
                AssertContains(sceneUpdate, "ISceneTransitionAudioBridge spatialAudio = ResolveSceneTransitionAudioBridge()", "Scene runtime world-drone update uses the usable bridge resolver", builder, ref failureCount);
                AssertNotContains(sceneBegin, "ISceneTransitionAudioBridge spatialAudio = _sceneTransitionAudioBridge", "Scene runtime world-drone begin never trusts the raw cached bridge", builder, ref failureCount);
                AssertNotContains(sceneUpdate, "ISceneTransitionAudioBridge spatialAudio = _sceneTransitionAudioBridge", "Scene runtime world-drone update never trusts the raw cached bridge", builder, ref failureCount);
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
                string pdaHotSwap = ExtractMethodBody(playerPda, "public void OnGlobalRegistryServiceReplaced(");
                string pdaColdCache = ExtractMethodBody(playerPda, "private void RefreshColdRegistryReferences()");
                string pdaCache = ExtractMethodBody(playerPda, "private void CacheAudioService(");
                string pdaResolve = ExtractMethodBody(playerPda, "private IAudioService ResolveAudioService()");
                string pdaUsable = ExtractMethodBody(playerPda, "private static bool IsAudioServiceUsable(");
                string playSound = ExtractMethodBody(playerPda, "private void PlaySound(AudioClip clip, float volume, float pitch)");
                string flushSounds = ExtractMethodBody(playerPda, "private void FlushPendingSounds()");
                AssertContains(pdaHotSwap, "CacheAudioService(currentService as IAudioService)", "PDA caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(pdaColdCache, "CacheAudioService(GlobalRegistry.Audio)", "PDA cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(pdaCache, "_audioService = IsAudioServiceUsable(audioService) ? audioService : null", "PDA stores only usable audio services", builder, ref failureCount);
                AssertContains(pdaResolve, "if (IsAudioServiceUsable(audioService))", "PDA resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(pdaResolve, "_audioService = null", "PDA resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(pdaUsable, "audioService == null || !audioService.IsInitialized", "PDA rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(pdaUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "PDA rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(playSound, "_pendingSoundClips[index] = clip", "PDA click helper stages pending audio without immediate playback", builder, ref failureCount);
                AssertContains(flushSounds, "IAudioService audioManager = ResolveAudioService()", "PDA pending sound flush uses the usable audio-service resolver", builder, ref failureCount);
                AssertContains(flushSounds, "audioManager.PlayAtPoint(clip, position, _pendingSoundVolumes[i], _pendingSoundPitches[i], audioManager.InterfaceGroup)", "PDA clicks route through SpatialAudioManager at the PDA hand AUP", builder, ref failureCount);
                AssertNotContains(flushSounds, "IAudioService audioManager = _audioService", "PDA pending sound flush never trusts the raw cached audio-service field", builder, ref failureCount);
                AssertNotContains(flushSounds, "PlayStatic2D", "PDA click flush does not route through 2D UI audio", builder, ref failureCount);
            }

            if (playerFlashlight.Length > 0)
            {
                string flashlightHotSwap = ExtractMethodBody(playerFlashlight, "private void ApplyRegistryServiceRebind(");
                string flashlightColdCache = ExtractMethodBody(playerFlashlight, "private void CachePlayerRuntimeContextCold()");
                string flashlightCache = ExtractMethodBody(playerFlashlight, "private void CacheAudioService(");
                string flashlightResolve = ExtractMethodBody(playerFlashlight, "private IAudioService ResolveAudioService()");
                string flashlightUsable = ExtractMethodBody(playerFlashlight, "private static bool IsAudioServiceUsable(");
                string flashlightFlush = ExtractMethodBody(playerFlashlight, "private void FlushPendingAudio()");
                AssertContains(flashlightHotSwap, "CacheAudioService(currentService as IAudioService)", "Player flashlight caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(flashlightColdCache, "CacheAudioService(GlobalRegistry.Audio)", "Player flashlight cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(flashlightCache, "_audioService = IsAudioServiceUsable(audioService) ? audioService : null", "Player flashlight stores only usable audio services", builder, ref failureCount);
                AssertContains(flashlightResolve, "if (IsAudioServiceUsable(audioService))", "Player flashlight resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(flashlightResolve, "_audioService = null", "Player flashlight resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(flashlightUsable, "audioService == null || !audioService.IsInitialized", "Player flashlight rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(flashlightUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Player flashlight rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(flashlightFlush, "IAudioService audioService = ResolveAudioService()", "Player flashlight audio flush uses the usable audio-service resolver", builder, ref failureCount);
                AssertNotContains(flashlightFlush, "IAudioService audioService = _audioService", "Player flashlight audio flush never trusts the raw cached audio-service field", builder, ref failureCount);
            }

            if (pdaInventoryTab.Length > 0)
            {
                string inventoryHotSwap = ExtractMethodBody(pdaInventoryTab, "public void OnGlobalRegistryServiceReplaced(");
                string inventoryColdCache = ExtractMethodBody(pdaInventoryTab, "private void CacheRegistryServicesCold()");
                string inventoryCache = ExtractMethodBody(pdaInventoryTab, "private void CacheAudioService(");
                string inventoryResolve = ExtractMethodBody(pdaInventoryTab, "private IAudioService ResolveAudioService()");
                string inventoryUsable = ExtractMethodBody(pdaInventoryTab, "private static bool IsAudioServiceUsable(");
                string inventoryPlay = ExtractMethodBody(pdaInventoryTab, "private void PlayUISound(");
                AssertContains(inventoryHotSwap, "CacheAudioService(currentService as IAudioService)", "PDA inventory tab caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(inventoryColdCache, "CacheAudioService(GlobalRegistry.Audio)", "PDA inventory tab cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(inventoryCache, "_audioService = IsAudioServiceUsable(audioService) ? audioService : null", "PDA inventory tab stores only usable audio services", builder, ref failureCount);
                AssertContains(inventoryResolve, "if (IsAudioServiceUsable(audioService))", "PDA inventory tab resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(inventoryResolve, "_audioService = null", "PDA inventory tab resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(inventoryUsable, "audioService == null || !audioService.IsInitialized", "PDA inventory tab rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(inventoryUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "PDA inventory tab rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(inventoryPlay, "IAudioService audio = ResolveAudioService()", "PDA inventory UI sound uses the usable audio-service resolver", builder, ref failureCount);
                AssertNotContains(inventoryPlay, "IAudioService audio = _audioService", "PDA inventory UI sound never trusts the raw cached audio-service field", builder, ref failureCount);
            }

            if (playerInventory.Length > 0)
            {
                string inventoryHotSwap = ExtractMethodBody(playerInventory, "public void OnGlobalRegistryServiceReplaced(");
                string inventoryColdCache = ExtractMethodBody(playerInventory, "private void CacheRegistryServicesCold()");
                string inventoryCache = ExtractMethodBody(playerInventory, "private void CacheAudioService(");
                string inventoryResolve = ExtractMethodBody(playerInventory, "private IAudioService ResolveAudioService()");
                string inventoryUsable = ExtractMethodBody(playerInventory, "private static bool IsAudioServiceUsable(");
                string inventoryRunaway = ExtractMethodBody(playerInventory, "private void DispatchInventoryThermalRunaway(");
                AssertContains(inventoryHotSwap, "CacheAudioService(currentService as IAudioService)", "Player inventory caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(inventoryColdCache, "CacheAudioService(GlobalRegistry.Audio)", "Player inventory cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(inventoryCache, "_cachedAudioService = IsAudioServiceUsable(audioService) ? audioService : null", "Player inventory stores only usable audio services", builder, ref failureCount);
                AssertContains(inventoryResolve, "if (IsAudioServiceUsable(audioService))", "Player inventory resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(inventoryResolve, "_cachedAudioService = null", "Player inventory resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(inventoryUsable, "audioService == null || !audioService.IsInitialized", "Player inventory rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(inventoryUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Player inventory rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(inventoryRunaway, "IAudioService audioService = ResolveAudioService()", "Player inventory thermal runaway audio resolves only usable audio services", builder, ref failureCount);
                AssertContains(inventoryRunaway, "audioService is ISpatialAudioInventoryRunawaySink inventoryAudio", "Player inventory thermal runaway sink is cast only after usable audio-service resolution", builder, ref failureCount);
                AssertNotContains(inventoryHotSwap, "_cachedAudioService = currentService as IAudioService", "Player inventory hot-swap never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(inventoryColdCache, "_cachedAudioService = GlobalRegistry.Audio", "Player inventory cold cache never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(inventoryRunaway, "_cachedAudioService is ISpatialAudioInventoryRunawaySink", "Player inventory thermal runaway never casts the raw cached audio-service field", builder, ref failureCount);
            }

            if (playerMovement.Length > 0)
            {
                string movementInject = ExtractMethodBody(playerMovement, "public void OnDependencyInject()");
                string movementHotSwap = ExtractMethodBody(playerMovement, "public void OnGlobalRegistryServiceReplaced(");
                string movementCache = ExtractMethodBody(playerMovement, "private void CacheAudioService(");
                string movementResolve = ExtractMethodBody(playerMovement, "private IAudioService ResolveAudioService()");
                string movementUsable = ExtractMethodBody(playerMovement, "private static bool IsAudioServiceUsable(");
                string movementFlush = ExtractMethodBody(playerMovement, "private void FlushPresentationAudioEvents()");
                AssertContains(movementInject, "CacheAudioService(GlobalRegistry.Audio)", "Player movement cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(movementHotSwap, "CacheAudioService(currentService as IAudioService)", "Player movement caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(movementCache, "_audioService = IsAudioServiceUsable(audioService) ? audioService : null", "Player movement stores only usable audio services", builder, ref failureCount);
                AssertContains(movementResolve, "if (IsAudioServiceUsable(audioService))", "Player movement resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(movementResolve, "_audioService = null", "Player movement resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(movementUsable, "audioService == null || !audioService.IsInitialized", "Player movement rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(movementUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Player movement rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(movementFlush, "IAudioService audioManager = ResolveAudioService()", "Player movement presentation audio flush uses the usable audio-service resolver", builder, ref failureCount);
                AssertContains(movementFlush, "audioManager.PlayStatic2D", "Player movement presentation audio still routes static cues", builder, ref failureCount);
                AssertContains(movementFlush, "audioManager.PlayAtPoint", "Player movement presentation audio still routes spatial cues", builder, ref failureCount);
                AssertNotContains(movementInject, "_audioService = GlobalRegistry.Audio", "Player movement cold cache never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(movementHotSwap, "_audioService = currentService as IAudioService", "Player movement hot-swap never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(movementFlush, "IAudioService audioManager = _audioService", "Player movement presentation audio flush never trusts the raw cached audio-service field", builder, ref failureCount);
            }

            if (pdaMapTab.Length > 0)
            {
                string mapHotSwap = ExtractMethodBody(pdaMapTab, "public void OnGlobalRegistryServiceReplaced(");
                string mapColdCache = ExtractMethodBody(pdaMapTab, "private void CacheRegistryServicesCold()");
                string mapCache = ExtractMethodBody(pdaMapTab, "private void CacheAudioService(");
                string mapResolve = ExtractMethodBody(pdaMapTab, "private IAudioService ResolveAudioService()");
                string mapUsable = ExtractMethodBody(pdaMapTab, "private static bool IsAudioServiceUsable(");
                string mapThreatPings = ExtractMethodBody(pdaMapTab, "private void RefreshThreatPings()");
                AssertContains(mapHotSwap, "CacheAudioService(currentService as IAudioService)", "PDA map tab caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(mapColdCache, "CacheAudioService(GlobalRegistry.Audio)", "PDA map tab cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(mapCache, "_audioService = IsAudioServiceUsable(audioService) ? audioService : null", "PDA map tab stores only usable audio services", builder, ref failureCount);
                AssertContains(mapResolve, "if (IsAudioServiceUsable(audioService))", "PDA map tab resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(mapResolve, "_audioService = null", "PDA map tab resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(mapUsable, "audioService == null || !audioService.IsInitialized", "PDA map tab rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(mapUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "PDA map tab rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(mapThreatPings, "IAudioService audio = ResolveAudioService()", "PDA map acoustic pings use the usable audio-service resolver", builder, ref failureCount);
                AssertNotContains(mapThreatPings, "_audioService", "PDA map acoustic pings never read the raw cached audio-service field", builder, ref failureCount);
            }

            if (playerStressVfx.Length > 0)
            {
                string stressHotSwap = ExtractMethodBody(playerStressVfx, "public void OnGlobalRegistryServiceReplaced(");
                string stressColdCache = ExtractMethodBody(playerStressVfx, "private void CacheRegistryServicesCold()");
                string stressAudioCache = ExtractMethodBody(playerStressVfx, "private void CacheAudioService(");
                string stressAudioResolve = ExtractMethodBody(playerStressVfx, "private IAudioService ResolveAudioService()");
                string stressAudioUsable = ExtractMethodBody(playerStressVfx, "private static bool IsAudioServiceUsable(");
                string stressHeartbeat = ExtractMethodBody(playerStressVfx, "private void PlayHeartbeat(");
                AssertContains(playerStressVfx, "PlayHeartbeat(audioStress01)", "Heartbeat audio is driven from stress VFX update", builder, ref failureCount);
                AssertContains(playerStressVfx, "QueueStressPulse(stress01, beat01, fog01, frost01)", "Heartbeat pulse is synchronized with visual UI distortion", builder, ref failureCount);
                AssertContains(stressHotSwap, "CacheAudioService(currentService as IAudioService)", "Stress VFX caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(stressColdCache, "CacheAudioService(GlobalRegistry.Audio)", "Stress VFX cold-caches audio service through the usable-service filter", builder, ref failureCount);
                AssertContains(stressAudioCache, "_cachedAudioService = IsAudioServiceUsable(audioService) ? audioService : null", "Stress VFX stores only usable audio services", builder, ref failureCount);
                AssertContains(stressAudioResolve, "if (IsAudioServiceUsable(audioService))", "Stress VFX resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(stressAudioResolve, "_cachedAudioService = null", "Stress VFX resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(stressAudioUsable, "audioService == null || !audioService.IsInitialized", "Stress VFX rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(stressAudioUsable, "audioService is Behaviour behaviour", "Stress VFX validates MonoBehaviour-backed audio services", builder, ref failureCount);
                AssertContains(stressAudioUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Stress VFX rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(stressHeartbeat, "IAudioService audioManager = ResolveAudioService()", "Stress VFX heartbeat playback uses the usable audio-service resolver", builder, ref failureCount);
                AssertNotContains(stressHeartbeat, "_cachedAudioService", "Stress VFX heartbeat playback never reads the raw cached audio-service field", builder, ref failureCount);
                AssertNotContains(stressHeartbeat, "GlobalRegistry.Audio", "Stress VFX heartbeat playback does not poll audio registry directly", builder, ref failureCount);
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

            if (physicalPanelDial.Length > 0)
            {
                string dialHotSwap = ExtractMethodBody(physicalPanelDial, "public void OnGlobalRegistryServiceReplaced(");
                string dialOnEnable = ExtractMethodBody(physicalPanelDial, "private void OnEnable()");
                string dialCache = ExtractMethodBody(physicalPanelDial, "private void CacheAudioService(");
                string dialResolve = ExtractMethodBody(physicalPanelDial, "private IAudioService ResolveAudioService()");
                string dialUsable = ExtractMethodBody(physicalPanelDial, "private static bool IsAudioServiceUsable(");
                string dialQueue = ExtractMethodBody(physicalPanelDial, "private void QueueScrollAudio()");
                AssertContains(dialOnEnable, "CacheAudioService(GlobalRegistry.Audio)", "Physical panel dial cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(dialHotSwap, "CacheAudioService(currentService as IAudioService)", "Physical panel dial caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(dialCache, "_cachedAudioService = IsAudioServiceUsable(audioService) ? audioService : null", "Physical panel dial stores only usable audio services", builder, ref failureCount);
                AssertContains(dialResolve, "if (IsAudioServiceUsable(audioService))", "Physical panel dial resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(dialResolve, "_cachedAudioService = null", "Physical panel dial resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(dialUsable, "audioService is Behaviour behaviour", "Physical panel dial validates MonoBehaviour-backed audio services", builder, ref failureCount);
                AssertContains(dialUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Physical panel dial rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(dialQueue, "IAudioService audio = ResolveAudioService()", "Physical panel dial scroll ticks use the usable audio-service resolver", builder, ref failureCount);
                AssertNotContains(dialQueue, "_cachedAudioService", "Physical panel dial scroll audio never reads the raw cached audio-service field", builder, ref failureCount);
            }

            if (physicalTerminalKeyboard.Length > 0)
            {
                string keyboardHotSwap = ExtractMethodBody(physicalTerminalKeyboard, "public void OnGlobalRegistryServiceReplaced(");
                string keyboardOnEnable = ExtractMethodBody(physicalTerminalKeyboard, "private void OnEnable()");
                string keyboardCache = ExtractMethodBody(physicalTerminalKeyboard, "private void CacheAudioService(");
                string keyboardResolve = ExtractMethodBody(physicalTerminalKeyboard, "private IAudioService ResolveAudioService()");
                string keyboardUsable = ExtractMethodBody(physicalTerminalKeyboard, "private static bool IsAudioServiceUsable(");
                string keyboardQueue = ExtractMethodBody(physicalTerminalKeyboard, "private void QueuePressAudio()");
                AssertContains(keyboardOnEnable, "CacheAudioService(GlobalRegistry.Audio)", "Physical terminal keyboard cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(keyboardHotSwap, "CacheAudioService(currentService as IAudioService)", "Physical terminal keyboard caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(keyboardCache, "_cachedAudioService = IsAudioServiceUsable(audioService) ? audioService : null", "Physical terminal keyboard stores only usable audio services", builder, ref failureCount);
                AssertContains(keyboardResolve, "if (IsAudioServiceUsable(audioService))", "Physical terminal keyboard resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(keyboardResolve, "_cachedAudioService = null", "Physical terminal keyboard resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(keyboardUsable, "audioService is Behaviour behaviour", "Physical terminal keyboard validates MonoBehaviour-backed audio services", builder, ref failureCount);
                AssertContains(keyboardUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Physical terminal keyboard rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(keyboardQueue, "IAudioService audio = ResolveAudioService()", "Physical terminal keyboard key clicks use the usable audio-service resolver", builder, ref failureCount);
                AssertNotContains(keyboardQueue, "_cachedAudioService", "Physical terminal keyboard key audio never reads the raw cached audio-service field", builder, ref failureCount);
            }

            if (physicalPanelButton.Length > 0)
            {
                string buttonHotSwap = ExtractMethodBody(physicalPanelButton, "public void OnGlobalRegistryServiceReplaced(");
                string buttonColdCache = ExtractMethodBody(physicalPanelButton, "private void CacheRegistryServicesCold()");
                string buttonCache = ExtractMethodBody(physicalPanelButton, "private void CacheAudioService(");
                string buttonResolve = ExtractMethodBody(physicalPanelButton, "private IAudioService ResolveAudioService()");
                string buttonUsable = ExtractMethodBody(physicalPanelButton, "private static bool IsAudioServiceUsable(");
                string buttonClick = ExtractMethodBody(physicalPanelButton, "private void PlayDiegeticClick(");
                AssertContains(buttonColdCache, "CacheAudioService(GlobalRegistry.Audio)", "Physical panel button cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(buttonHotSwap, "CacheAudioService(currentService as IAudioService)", "Physical panel button caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(buttonCache, "_cachedAudioService = IsAudioServiceUsable(audioService) ? audioService : null", "Physical panel button stores only usable audio services", builder, ref failureCount);
                AssertContains(buttonResolve, "if (IsAudioServiceUsable(audioService))", "Physical panel button resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(buttonResolve, "_cachedAudioService = null", "Physical panel button resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(buttonUsable, "audioService is Behaviour behaviour", "Physical panel button validates MonoBehaviour-backed audio services", builder, ref failureCount);
                AssertContains(buttonUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Physical panel button rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(buttonClick, "IAudioService audio = ResolveAudioService()", "Physical panel button click playback uses the usable audio-service resolver", builder, ref failureCount);
                AssertNotContains(buttonClick, "_cachedAudioService", "Physical panel button click playback never reads the raw cached audio-service field", builder, ref failureCount);
            }

            if (suitAdvisory.Length > 0)
            {
                string advisoryAwake = ExtractMethodBody(suitAdvisory, "private void Awake()");
                string advisoryOnEnable = ExtractMethodBody(suitAdvisory, "private void OnEnable()");
                string advisoryHotSwap = ExtractMethodBody(suitAdvisory, "public void OnGlobalRegistryServiceReplaced(");
                string advisoryCache = ExtractMethodBody(suitAdvisory, "private void CacheAudioService(");
                string advisoryResolve = ExtractMethodBody(suitAdvisory, "private IAudioService ResolveAudioService()");
                string advisoryUsable = ExtractMethodBody(suitAdvisory, "private static bool IsAudioServiceUsable(");
                string advisoryPlay = ExtractMethodBody(suitAdvisory, "private void PlayUiClip(");
                AssertContains(advisoryAwake, "CacheAudioService(GlobalRegistry.Audio)", "Suit advisory cold-caches audio through the usable-service filter in Awake", builder, ref failureCount);
                AssertContains(advisoryOnEnable, "CacheAudioService(GlobalRegistry.Audio)", "Suit advisory cold-caches audio through the usable-service filter in OnEnable", builder, ref failureCount);
                AssertContains(advisoryHotSwap, "CacheAudioService(currentService as IAudioService)", "Suit advisory caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(advisoryCache, "_cachedAudioService = IsAudioServiceUsable(audioService) ? audioService : null", "Suit advisory stores only usable audio services", builder, ref failureCount);
                AssertContains(advisoryResolve, "if (IsAudioServiceUsable(audioService))", "Suit advisory resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(advisoryResolve, "_cachedAudioService = null", "Suit advisory resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(advisoryUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Suit advisory rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(advisoryPlay, "IAudioService audio = ResolveAudioService()", "Suit advisory warning playback uses the usable audio-service resolver", builder, ref failureCount);
                AssertNotContains(advisoryPlay, "_cachedAudioService", "Suit advisory warning playback never reads the raw cached audio-service field", builder, ref failureCount);
            }

            if (uiAudioFeedback.Length > 0)
            {
                string feedbackAwake = ExtractMethodBody(uiAudioFeedback, "private void Awake()");
                string feedbackRegister = ExtractMethodBody(uiAudioFeedback, "private bool TryRegisterRuntime()");
                string feedbackExistingRuntime = ExtractMethodBody(uiAudioFeedback, "private bool TryAbortForUsableExistingRuntime()");
                string feedbackRuntimeUsable = ExtractMethodBody(uiAudioFeedback, "private static bool IsUIAudioFeedbackRuntimeUsable(");
                string feedbackRebound = ExtractMethodBody(uiAudioFeedback, "public void OnGlobalRegistryServiceRebound(");
                string feedbackHotSwap = ExtractMethodBody(uiAudioFeedback, "public void OnGlobalRegistryServiceReplaced(");
                string feedbackBind = ExtractMethodBody(uiAudioFeedback, "private void BindAudioAndRegisterControls()");
                string feedbackCache = ExtractMethodBody(uiAudioFeedback, "private void CacheAudioService(");
                string feedbackResolve = ExtractMethodBody(uiAudioFeedback, "private IAudioService ResolveAudioService()");
                string feedbackUsable = ExtractMethodBody(uiAudioFeedback, "private static bool IsAudioServiceUsable(");
                string feedbackPlay = ExtractMethodBody(uiAudioFeedback, "private void PlaySound(");
                AssertContains(feedbackAwake, "if (TryAbortForUsableExistingRuntime())", "UI audio feedback Awake routes duplicate-owner checks through the stale-owner gate", builder, ref failureCount);
                AssertContains(feedbackRegister, "if (TryAbortForUsableExistingRuntime())", "UI audio feedback registration routes duplicate-owner checks through the stale-owner gate", builder, ref failureCount);
                AssertTextBefore(feedbackRegister, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterUIAudioFeedbackRuntime(this);", "UI audio feedback clears stale owners before self-register", builder, ref failureCount);
                AssertContains(feedbackExistingRuntime, "UIAudioFeedback registered = GlobalRegistry.UIAudioFeedback", "UI audio feedback snapshots the current runtime owner once", builder, ref failureCount);
                AssertContains(feedbackExistingRuntime, "ReferenceEquals(registered, null)", "UI audio feedback detects stale destroyed registry references by actual reference", builder, ref failureCount);
                AssertContains(feedbackExistingRuntime, "ReferenceEquals(registered, this)", "UI audio feedback treats only other owners as registry conflicts", builder, ref failureCount);
                AssertContains(feedbackExistingRuntime, "if (IsUIAudioFeedbackRuntimeUsable(registered))", "UI audio feedback preserves usable existing runtime owners", builder, ref failureCount);
                AssertContains(feedbackExistingRuntime, "Destroy(gameObject);", "UI audio feedback destroys duplicate runtime roots only when the existing owner is usable", builder, ref failureCount);
                AssertContains(feedbackExistingRuntime, "GlobalRegistry.UnregisterUIAudioFeedbackRuntime(registered);", "UI audio feedback clears stale existing owners before registering", builder, ref failureCount);
                AssertContains(feedbackExistingRuntime, "s_activeRuntime = null", "UI audio feedback clears stale active-runtime mirror when clearing registry owners", builder, ref failureCount);
                AssertContains(feedbackRuntimeUsable, "feedback != null && feedback._runtimeRegistered && feedback.isActiveAndEnabled", "UI audio feedback validates existing owner readiness and activity", builder, ref failureCount);
                AssertNotContains(feedbackAwake, "registered != null && registered != this", "UI audio feedback Awake no longer treats stale owners as hard conflicts", builder, ref failureCount);
                AssertNotContains(feedbackRegister, "registered != null && registered != this", "UI audio feedback registration no longer treats stale owners as hard conflicts", builder, ref failureCount);
                AssertContains(feedbackRebound, "CacheAudioService(currentService as IAudioService)", "UI audio feedback rebound caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(feedbackHotSwap, "CacheAudioService(currentService as IAudioService)", "UI audio feedback hot-swap caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(feedbackBind, "CacheAudioService(GlobalRegistry.Audio)", "UI audio feedback cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(feedbackCache, "_audioManager = IsAudioServiceUsable(audioService) ? audioService : null", "UI audio feedback stores only usable audio services", builder, ref failureCount);
                AssertContains(feedbackResolve, "if (IsAudioServiceUsable(audioService))", "UI audio feedback resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(feedbackResolve, "_audioManager = null", "UI audio feedback resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(feedbackUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "UI audio feedback rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(feedbackPlay, "IAudioService audioManager = ResolveAudioService()", "UI audio feedback playback uses the usable audio-service resolver", builder, ref failureCount);
                AssertNotContains(feedbackPlay, "_audioManager.PlayStatic2D", "UI audio feedback playback never calls through the raw cached audio manager field", builder, ref failureCount);
            }

            if (uiButtonAudioTrigger.Length > 0)
            {
                string triggerAwake = ExtractMethodBody(uiButtonAudioTrigger, "private void Awake()");
                string triggerOnEnable = ExtractMethodBody(uiButtonAudioTrigger, "private void OnEnable()");
                string triggerHotSwap = ExtractMethodBody(uiButtonAudioTrigger, "public void OnGlobalRegistryServiceReplaced(");
                string triggerCache = ExtractMethodBody(uiButtonAudioTrigger, "private void CacheAudioService(");
                string triggerResolve = ExtractMethodBody(uiButtonAudioTrigger, "private IAudioService ResolveAudioService()");
                string triggerUsable = ExtractMethodBody(uiButtonAudioTrigger, "private static bool IsAudioServiceUsable(");
                string triggerClick = ExtractMethodBody(uiButtonAudioTrigger, "private void OnButtonClicked()");
                AssertContains(triggerAwake, "CacheAudioService(GlobalRegistry.Audio)", "UI button trigger cold-caches audio through the usable-service filter in Awake", builder, ref failureCount);
                AssertContains(triggerOnEnable, "CacheAudioService(GlobalRegistry.Audio)", "UI button trigger cold-caches audio through the usable-service filter in OnEnable", builder, ref failureCount);
                AssertContains(triggerHotSwap, "CacheAudioService(currentService as IAudioService)", "UI button trigger hot-swap caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(triggerCache, "_audioManager = IsAudioServiceUsable(audioService) ? audioService : null", "UI button trigger stores only usable audio services", builder, ref failureCount);
                AssertContains(triggerResolve, "if (IsAudioServiceUsable(audioService))", "UI button trigger resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(triggerResolve, "_audioManager = null", "UI button trigger resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(triggerUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "UI button trigger rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(triggerClick, "IAudioService audioManager = ResolveAudioService()", "UI button trigger playback uses the usable audio-service resolver", builder, ref failureCount);
                AssertNotContains(triggerClick, "_audioManager", "UI button trigger playback never reads the raw cached audio-manager field", builder, ref failureCount);
            }

            if (surfaceWeatherDirector.Length > 0)
            {
                string weatherHotSwap = ExtractMethodBody(surfaceWeatherDirector, "public void OnGlobalRegistryServiceReplaced(");
                string weatherColdCache = ExtractMethodBody(surfaceWeatherDirector, "private void CacheAudioRuntimeCold()");
                string weatherCache = ExtractMethodBody(surfaceWeatherDirector, "private void CacheAudioService(");
                string weatherResolve = ExtractMethodBody(surfaceWeatherDirector, "private IAudioService ResolveAudioService()");
                string weatherUsable = ExtractMethodBody(surfaceWeatherDirector, "private static bool IsAudioServiceUsable(");
                string weatherThunder = ExtractMethodBody(surfaceWeatherDirector, "private void PlayThunderNow()");
                AssertContains(weatherHotSwap, "CacheAudioService(currentService as IAudioService)", "Surface weather director caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(weatherColdCache, "CacheAudioService(GlobalRegistry.Audio)", "Surface weather director cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(weatherCache, "_audioRuntime = IsAudioServiceUsable(audioService) ? audioService : null", "Surface weather director stores only usable audio services", builder, ref failureCount);
                AssertContains(weatherResolve, "if (IsAudioServiceUsable(audioService))", "Surface weather director resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(weatherResolve, "_audioRuntime = null", "Surface weather director resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(weatherUsable, "audioService == null || !audioService.IsInitialized", "Surface weather director rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(weatherUsable, "audioService is Behaviour behaviour", "Surface weather director validates MonoBehaviour-backed audio services", builder, ref failureCount);
                AssertContains(weatherUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Surface weather director rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(weatherThunder, "IAudioService audioManager = ResolveAudioService()", "Surface weather thunder playback uses the usable audio-service resolver", builder, ref failureCount);
                AssertNotContains(weatherThunder, "_audioRuntime", "Surface weather thunder playback never reads the raw cached audio-runtime field", builder, ref failureCount);
                AssertNotContains(weatherThunder, "GlobalRegistry.Audio", "Surface weather thunder playback does not poll audio registry directly", builder, ref failureCount);
            }

            if (underwaterVisuals.Length > 0)
            {
                string visualsHotSwap = ExtractMethodBody(underwaterVisuals, "public void OnGlobalRegistryServiceReplaced(");
                string visualsColdCache = ExtractMethodBody(underwaterVisuals, "private void CacheRuntimeDependencies()");
                string visualsCache = ExtractMethodBody(underwaterVisuals, "private void CacheAudioService(");
                string visualsResolve = ExtractMethodBody(underwaterVisuals, "private IAudioService ResolveAudioService()");
                string visualsUsable = ExtractMethodBody(underwaterVisuals, "private static bool IsAudioServiceUsable(");
                string visualsThermocline = ExtractMethodBody(underwaterVisuals, "private void TryHandleThermoclineTransition(");
                AssertContains(visualsHotSwap, "CacheAudioService(currentService as IAudioService)", "Underwater visuals caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(visualsColdCache, "CacheAudioService(GlobalRegistry.Audio)", "Underwater visuals cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(visualsCache, "_audioRuntime = IsAudioServiceUsable(audioService) ? audioService : null", "Underwater visuals stores only usable audio services", builder, ref failureCount);
                AssertContains(visualsResolve, "if (IsAudioServiceUsable(audioService))", "Underwater visuals resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(visualsResolve, "_audioRuntime = null", "Underwater visuals resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(visualsUsable, "audioService == null || !audioService.IsInitialized", "Underwater visuals rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(visualsUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Underwater visuals rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(visualsThermocline, "IAudioService audioRuntime = ResolveAudioService()", "Underwater thermocline audio uses the usable audio-service resolver", builder, ref failureCount);
                AssertContains(visualsThermocline, "audioRuntime.PlayStatic2D", "Underwater thermocline audio remains a 2D transition cue", builder, ref failureCount);
                AssertNotContains(visualsHotSwap, "_audioRuntime = currentService as IAudioService", "Underwater visuals hot-swap never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(visualsColdCache, "_audioRuntime = GlobalRegistry.Audio", "Underwater visuals cold cache never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(visualsThermocline, "IAudioService audioRuntime = _audioRuntime", "Underwater thermocline audio never trusts the raw cached audio-runtime field", builder, ref failureCount);
            }

            if (playerInteraction.Length > 0)
            {
                string playerHotSwap = ExtractMethodBody(playerInteraction, "public void OnGlobalRegistryServiceReplaced(");
                string playerColdCache = ExtractMethodBody(playerInteraction, "private void RefreshCachedRegistryServices()");
                string playerCache = ExtractMethodBody(playerInteraction, "private void CacheAudioService(");
                string playerResolve = ExtractMethodBody(playerInteraction, "private IAudioService ResolveAudioService()");
                string playerUsable = ExtractMethodBody(playerInteraction, "private static bool IsAudioServiceUsable(");
                string playerQueue = ExtractMethodBody(playerInteraction, "private void QueueStaticAudio(");
                string playerFlush = ExtractMethodBody(playerInteraction, "private void FlushQueuedStaticAudio()");
                AssertContains(playerHotSwap, "CacheAudioService(currentService as IAudioService)", "Player interaction caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(playerColdCache, "CacheAudioService(GlobalRegistry.Audio)", "Player interaction cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(playerCache, "_audioService = IsAudioServiceUsable(audioService) ? audioService : null", "Player interaction stores only usable audio services", builder, ref failureCount);
                AssertContains(playerResolve, "if (IsAudioServiceUsable(audioService))", "Player interaction resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(playerResolve, "_audioService = null", "Player interaction resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(playerUsable, "audioService == null || !audioService.IsInitialized", "Player interaction rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(playerUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Player interaction rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(playerQueue, "ResolveAudioService() == null", "Player interaction queue path checks usable audio service before staging clicks", builder, ref failureCount);
                AssertNotContains(playerQueue, "_audioService", "Player interaction queue path never reads the raw cached audio-service field", builder, ref failureCount);
                AssertContains(playerFlush, "IAudioService audioService = ResolveAudioService()", "Player interaction late-frame audio flush uses the usable audio-service resolver", builder, ref failureCount);
                AssertNotContains(playerFlush, "IAudioService audioService = _audioService", "Player interaction late-frame audio flush never trusts the raw cached audio-service field", builder, ref failureCount);
            }

            if (saveStation.Length > 0)
            {
                string saveHotSwap = ExtractMethodBody(saveStation, "public void OnGlobalRegistryServiceReplaced(");
                string saveColdCache = ExtractMethodBody(saveStation, "private void CacheRegistryServicesCold()");
                string saveCache = ExtractMethodBody(saveStation, "private void CacheAudioService(");
                string saveResolve = ExtractMethodBody(saveStation, "private Hecton8.Core.IAudioService ResolveAudioService()");
                string saveUsable = ExtractMethodBody(saveStation, "private static bool IsAudioServiceUsable(");
                string savePlay = ExtractMethodBody(saveStation, "private void PlayInteractionSound()");
                AssertContains(saveHotSwap, "CacheAudioService(currentService as Hecton8.Core.IAudioService)", "Save station caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(saveColdCache, "CacheAudioService(GlobalRegistry.Audio)", "Save station cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(saveCache, "_audioService = IsAudioServiceUsable(audioService) ? audioService : null", "Save station stores only usable audio services", builder, ref failureCount);
                AssertContains(saveResolve, "if (IsAudioServiceUsable(audioService))", "Save station resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(saveResolve, "_audioService = null", "Save station resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(saveUsable, "audioService == null || !audioService.IsInitialized", "Save station rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(saveUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Save station rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(savePlay, "Hecton8.Core.IAudioService audioManager = ResolveAudioService()", "Save station interaction sound uses the usable audio-service resolver", builder, ref failureCount);
                AssertNotContains(savePlay, "_audioService", "Save station interaction sound never reads the raw cached audio-service field", builder, ref failureCount);
            }

            if (physicalSnapSwitch.Length > 0)
            {
                string switchHotSwap = ExtractMethodBody(physicalSnapSwitch, "public void OnGlobalRegistryServiceReplaced(");
                string switchColdCache = ExtractMethodBody(physicalSnapSwitch, "private void RefreshColdRegistryReferences()");
                string switchCache = ExtractMethodBody(physicalSnapSwitch, "private void CacheAudioService(");
                string switchResolve = ExtractMethodBody(physicalSnapSwitch, "private IAudioService ResolveAudioService()");
                string switchUsable = ExtractMethodBody(physicalSnapSwitch, "private static bool IsAudioServiceUsable(");
                string switchQueue = ExtractMethodBody(physicalSnapSwitch, "private void QueueSnapAudio(");
                AssertContains(switchHotSwap, "CacheAudioService(currentService as IAudioService)", "Physical snap switch caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(switchColdCache, "CacheAudioService(GlobalRegistry.Audio)", "Physical snap switch cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(switchCache, "_audioService = IsAudioServiceUsable(audioService) ? audioService : null", "Physical snap switch stores only usable audio services", builder, ref failureCount);
                AssertContains(switchResolve, "if (IsAudioServiceUsable(audioService))", "Physical snap switch resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(switchResolve, "_audioService = null", "Physical snap switch resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(switchUsable, "audioService == null || !audioService.IsInitialized", "Physical snap switch rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(switchUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Physical snap switch rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(switchQueue, "IAudioService audio = ResolveAudioService()", "Physical snap switch queues authored audio events through the usable audio-service resolver", builder, ref failureCount);
                AssertNotContains(switchQueue, "_audioService", "Physical snap switch audio queue never reads the raw cached audio-service field", builder, ref failureCount);
                AssertNotContains(switchQueue, "!audio.IsInitialized", "Physical snap switch audio queue does not duplicate stale initialization checks after resolver", builder, ref failureCount);
            }

            if (oxygenPlant.Length > 0)
            {
                string plantHotSwap = ExtractMethodBody(oxygenPlant, "public void OnGlobalRegistryServiceReplaced(");
                string plantColdCache = ExtractMethodBody(oxygenPlant, "private void RefreshColdRegistryReferences()");
                string plantCache = ExtractMethodBody(oxygenPlant, "private void CacheAudioService(");
                string plantResolve = ExtractMethodBody(oxygenPlant, "private IAudioService ResolveAudioService()");
                string plantUsable = ExtractMethodBody(oxygenPlant, "private static bool IsAudioServiceUsable(");
                string plantLateFrame = ExtractMethodBody(oxygenPlant, "public void LateFrameTick()");
                AssertContains(plantHotSwap, "CacheAudioService(currentService as IAudioService)", "Oxygen plant caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(plantColdCache, "CacheAudioService(GlobalRegistry.Audio)", "Oxygen plant cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(plantCache, "_audioService = IsAudioServiceUsable(audioService) ? audioService : null", "Oxygen plant stores only usable audio services", builder, ref failureCount);
                AssertContains(plantResolve, "if (IsAudioServiceUsable(audioService))", "Oxygen plant resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(plantResolve, "_audioService = null", "Oxygen plant resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(plantUsable, "audioService == null || !audioService.IsInitialized", "Oxygen plant rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(plantUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Oxygen plant rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(plantLateFrame, "IAudioService audio = ResolveAudioService()", "Oxygen plant release audio flush uses the usable audio-service resolver", builder, ref failureCount);
                AssertNotContains(plantLateFrame, "IAudioService audio = _audioService", "Oxygen plant release audio flush never trusts the raw cached audio-service field", builder, ref failureCount);
            }

            if (oxygenBubble.Length > 0)
            {
                string bubbleHotSwap = ExtractMethodBody(oxygenBubble, "public void OnGlobalRegistryServiceReplaced(");
                string bubbleColdCache = ExtractMethodBody(oxygenBubble, "private void CacheRegistryServicesCold()");
                string bubbleCache = ExtractMethodBody(oxygenBubble, "private void CacheAudioService(");
                string bubbleResolve = ExtractMethodBody(oxygenBubble, "private IAudioService ResolveAudioService()");
                string bubbleUsable = ExtractMethodBody(oxygenBubble, "private static bool IsAudioServiceUsable(");
                string bubbleCollect = ExtractMethodBody(oxygenBubble, "private void PlayCollectEffects(");
                AssertContains(bubbleHotSwap, "CacheAudioService(currentService as IAudioService)", "Oxygen bubble caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(bubbleColdCache, "CacheAudioService(GlobalRegistry.Audio)", "Oxygen bubble cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(bubbleCache, "_audioService = IsAudioServiceUsable(audioService) ? audioService : null", "Oxygen bubble stores only usable audio services", builder, ref failureCount);
                AssertContains(bubbleResolve, "if (IsAudioServiceUsable(audioService))", "Oxygen bubble resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(bubbleResolve, "_audioService = null", "Oxygen bubble resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(bubbleUsable, "audioService == null || !audioService.IsInitialized", "Oxygen bubble rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(bubbleUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Oxygen bubble rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(bubbleCollect, "IAudioService audio = ResolveAudioService()", "Oxygen bubble collect audio uses the usable audio-service resolver", builder, ref failureCount);
                AssertNotContains(bubbleCollect, "_audioService.PlayAtPoint", "Oxygen bubble collect audio never calls through the raw cached audio-service field", builder, ref failureCount);
            }

            if (storageCrate.Length > 0)
            {
                string crateHotSwap = ExtractMethodBody(storageCrate, "public void OnGlobalRegistryServiceReplaced(");
                string crateColdCache = ExtractMethodBody(storageCrate, "private void CacheRegistryServicesCold()");
                string crateCache = ExtractMethodBody(storageCrate, "private void CacheAudioService(");
                string crateResolve = ExtractMethodBody(storageCrate, "private IAudioService ResolveAudioService()");
                string crateUsable = ExtractMethodBody(storageCrate, "private static bool IsAudioServiceUsable(");
                string crateOpen = ExtractMethodBody(storageCrate, "public void OpenCrate()");
                string crateClose = ExtractMethodBody(storageCrate, "public void CloseCrate()");
                AssertContains(crateHotSwap, "CacheAudioService(currentService as IAudioService)", "Storage crate caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(crateColdCache, "CacheAudioService(GlobalRegistry.Audio)", "Storage crate cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(crateCache, "_audioService = IsAudioServiceUsable(audioService) ? audioService : null", "Storage crate stores only usable audio services", builder, ref failureCount);
                AssertContains(crateResolve, "if (IsAudioServiceUsable(audioService))", "Storage crate resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(crateResolve, "_audioService = null", "Storage crate resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(crateUsable, "audioService == null || !audioService.IsInitialized", "Storage crate rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(crateUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Storage crate rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(crateOpen, "IAudioService audio = ResolveAudioService()", "Storage crate open sound uses the usable audio-service resolver", builder, ref failureCount);
                AssertContains(crateClose, "IAudioService audio = ResolveAudioService()", "Storage crate close sound uses the usable audio-service resolver", builder, ref failureCount);
                AssertNotContains(crateOpen, "_audioService", "Storage crate open sound never reads the raw cached audio-service field", builder, ref failureCount);
                AssertNotContains(crateClose, "_audioService", "Storage crate close sound never reads the raw cached audio-service field", builder, ref failureCount);
            }

            if (messageTerminal.Length > 0)
            {
                string terminalHotSwap = ExtractMethodBody(messageTerminal, "public void OnGlobalRegistryServiceReplaced(");
                string terminalColdCache = ExtractMethodBody(messageTerminal, "private void CacheRegistryServicesCold()");
                string terminalCache = ExtractMethodBody(messageTerminal, "private void CacheAudioService(");
                string terminalResolve = ExtractMethodBody(messageTerminal, "private IAudioService ResolveAudioService()");
                string terminalUsable = ExtractMethodBody(messageTerminal, "private static bool IsAudioServiceUsable(");
                string terminalQueue = ExtractMethodBody(messageTerminal, "private void QueueStaticAudio(");
                string terminalFlush = ExtractMethodBody(messageTerminal, "private void FlushQueuedStaticAudio()");
                AssertContains(terminalHotSwap, "CacheAudioService(currentService as IAudioService)", "Message terminal caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(terminalColdCache, "CacheAudioService(GlobalRegistry.Audio)", "Message terminal cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(terminalCache, "_audioService = IsAudioServiceUsable(audioService) ? audioService : null", "Message terminal stores only usable audio services", builder, ref failureCount);
                AssertContains(terminalResolve, "if (IsAudioServiceUsable(audioService))", "Message terminal resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(terminalResolve, "_audioService = null", "Message terminal resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(terminalUsable, "audioService == null || !audioService.IsInitialized", "Message terminal rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(terminalUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Message terminal rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(terminalQueue, "ResolveAudioService() == null", "Message terminal queue path checks usable audio service before staging static cues", builder, ref failureCount);
                AssertNotContains(terminalQueue, "_audioService", "Message terminal queue path never reads the raw cached audio-service field", builder, ref failureCount);
                AssertContains(terminalFlush, "IAudioService audioService = ResolveAudioService()", "Message terminal late-frame audio flush uses the usable audio-service resolver", builder, ref failureCount);
                AssertNotContains(terminalFlush, "IAudioService audioService = _audioService", "Message terminal late-frame audio flush never trusts the raw cached audio-service field", builder, ref failureCount);
            }

            if (playerAction.Length > 0)
            {
                string actionHotSwap = ExtractMethodBody(playerAction, "public void OnGlobalRegistryServiceReplaced(");
                string actionColdCache = ExtractMethodBody(playerAction, "private void CacheRegistryServicesCold()");
                string actionCache = ExtractMethodBody(playerAction, "private void CacheAudioService(");
                string actionResolve = ExtractMethodBody(playerAction, "private IAudioService ResolveAudioService()");
                string actionUsable = ExtractMethodBody(playerAction, "private static bool IsAudioServiceUsable(");
                string actionCompletion = ExtractMethodBody(playerAction, "private void PlayCompletionSound(");
                string actionCancel = ExtractMethodBody(playerAction, "private void PlayCancelSound()");
                string actionFlush = ExtractMethodBody(playerAction, "private void FlushQueuedActionAudio()");
                AssertContains(actionHotSwap, "CacheAudioService(currentService as IAudioService)", "Player action controller caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(actionColdCache, "CacheAudioService(GlobalRegistry.Audio)", "Player action controller cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(actionCache, "_audioService = IsAudioServiceUsable(audioService) ? audioService : null", "Player action controller stores only usable audio services", builder, ref failureCount);
                AssertContains(actionResolve, "if (IsAudioServiceUsable(audioService))", "Player action controller resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(actionResolve, "_audioService = null", "Player action controller resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(actionUsable, "audioService == null || !audioService.IsInitialized", "Player action controller rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(actionUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Player action controller rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(actionCompletion, "ResolveAudioService() != null", "Player action completion audio queue is gated by the usable audio-service resolver", builder, ref failureCount);
                AssertContains(actionCancel, "ResolveAudioService() != null", "Player action cancel audio queue is gated by the usable audio-service resolver", builder, ref failureCount);
                AssertContains(actionFlush, "IAudioService audioService = ResolveAudioService()", "Player action queued audio flush uses the usable audio-service resolver", builder, ref failureCount);
                AssertNotContains(actionCompletion, "_audioService", "Player action completion audio never reads the raw cached audio-service field", builder, ref failureCount);
                AssertNotContains(actionCancel, "_audioService", "Player action cancel audio never reads the raw cached audio-service field", builder, ref failureCount);
                AssertNotContains(actionFlush, "IAudioService audioService = _audioService", "Player action queued audio flush never trusts the raw cached audio-service field", builder, ref failureCount);
            }

            if (scannableFragment.Length > 0)
            {
                string fragmentHotSwap = ExtractMethodBody(scannableFragment, "public void OnGlobalRegistryServiceReplaced(");
                string fragmentColdCache = ExtractMethodBody(scannableFragment, "private void CacheRegistryServicesCold()");
                string fragmentCache = ExtractMethodBody(scannableFragment, "private void CacheAudioService(");
                string fragmentResolve = ExtractMethodBody(scannableFragment, "private IAudioService ResolveAudioService()");
                string fragmentUsable = ExtractMethodBody(scannableFragment, "private static bool IsAudioServiceUsable(");
                string fragmentLateFrame = ExtractMethodBody(scannableFragment, "public void LateFrameTick()");
                AssertContains(fragmentHotSwap, "CacheAudioService(currentService as IAudioService)", "Scannable fragment caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(fragmentColdCache, "CacheAudioService(GlobalRegistry.Audio)", "Scannable fragment cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(fragmentCache, "_audioService = IsAudioServiceUsable(audioService) ? audioService : null", "Scannable fragment stores only usable audio services", builder, ref failureCount);
                AssertContains(fragmentResolve, "if (IsAudioServiceUsable(audioService))", "Scannable fragment resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(fragmentResolve, "_audioService = null", "Scannable fragment resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(fragmentUsable, "audioService == null || !audioService.IsInitialized", "Scannable fragment rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(fragmentUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Scannable fragment rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(fragmentLateFrame, "IAudioService audio = ResolveAudioService()", "Scannable fragment scan ping uses the usable audio-service resolver", builder, ref failureCount);
                AssertNotContains(fragmentHotSwap, "_audioService = currentService as IAudioService", "Scannable fragment hot-swap never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(fragmentColdCache, "_audioService = GlobalRegistry.Audio", "Scannable fragment cold cache never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(fragmentLateFrame, "IAudioService audio = _audioService", "Scannable fragment scan ping never trusts the raw cached audio-service field", builder, ref failureCount);
            }

            if (climbableLadder.Length > 0)
            {
                string ladderHotSwap = ExtractMethodBody(climbableLadder, "public void OnGlobalRegistryServiceReplaced(");
                string ladderColdCache = ExtractMethodBody(climbableLadder, "private void CacheRegistryServicesCold()");
                string ladderCache = ExtractMethodBody(climbableLadder, "private void CacheAudioService(");
                string ladderResolve = ExtractMethodBody(climbableLadder, "private IAudioService ResolveAudioService()");
                string ladderUsable = ExtractMethodBody(climbableLadder, "private static bool IsAudioServiceUsable(");
                string ladderMove = ExtractMethodBody(climbableLadder, "private bool RequestProceduralClimb(");
                AssertContains(ladderHotSwap, "CacheAudioService(currentService as IAudioService)", "Climbable ladder caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(ladderColdCache, "CacheAudioService(GlobalRegistry.Audio)", "Climbable ladder cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(ladderCache, "_audioService = IsAudioServiceUsable(audioService) ? audioService : null", "Climbable ladder stores only usable audio services", builder, ref failureCount);
                AssertContains(ladderResolve, "if (IsAudioServiceUsable(audioService))", "Climbable ladder resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(ladderResolve, "_audioService = null", "Climbable ladder resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(ladderUsable, "audioService == null || !audioService.IsInitialized", "Climbable ladder rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(ladderUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Climbable ladder rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(ladderMove, "IAudioService audio = ResolveAudioService()", "Climbable ladder movement sound uses the usable audio-service resolver", builder, ref failureCount);
                AssertNotContains(ladderHotSwap, "_audioService = currentService as IAudioService", "Climbable ladder hot-swap never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(ladderColdCache, "_audioService = GlobalRegistry.Audio", "Climbable ladder cold cache never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(ladderMove, "IAudioService audio = _audioService", "Climbable ladder movement sound never trusts the raw cached audio-service field", builder, ref failureCount);
            }

            if (bioReactor.Length > 0)
            {
                string reactorHotSwap = ExtractMethodBody(bioReactor, "public void OnGlobalRegistryServiceReplaced(");
                string reactorColdCache = ExtractMethodBody(bioReactor, "private void RefreshColdRegistryReferences()");
                string reactorCache = ExtractMethodBody(bioReactor, "private void CacheAudioService(");
                string reactorResolve = ExtractMethodBody(bioReactor, "private IAudioService ResolveAudioService()");
                string reactorUsable = ExtractMethodBody(bioReactor, "private static bool IsAudioServiceUsable(");
                string reactorLateFrame = ExtractMethodBody(bioReactor, "public void LateFrameTick()");
                AssertContains(reactorHotSwap, "CacheAudioService(currentService as IAudioService)", "Bio reactor caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(reactorColdCache, "CacheAudioService(GlobalRegistry.Audio)", "Bio reactor cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(reactorCache, "_audioService = IsAudioServiceUsable(audioService) ? audioService : null", "Bio reactor stores only usable audio services", builder, ref failureCount);
                AssertContains(reactorResolve, "if (IsAudioServiceUsable(audioService))", "Bio reactor resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(reactorResolve, "_audioService = null", "Bio reactor resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(reactorUsable, "audioService == null || !audioService.IsInitialized", "Bio reactor rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(reactorUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Bio reactor rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(reactorLateFrame, "IAudioService audio = ResolveAudioService()", "Bio reactor fuel hum uses the usable audio-service resolver", builder, ref failureCount);
                AssertNotContains(reactorHotSwap, "_audioService = currentService as IAudioService", "Bio reactor hot-swap never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(reactorColdCache, "_audioService = GlobalRegistry.Audio", "Bio reactor cold cache never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(reactorLateFrame, "IAudioService audio = _audioService", "Bio reactor fuel hum never trusts the raw cached audio-service field", builder, ref failureCount);
            }

            if (harvestablePlant.Length > 0)
            {
                string plantHotSwap = ExtractMethodBody(harvestablePlant, "public void OnGlobalRegistryServiceReplaced(");
                string plantColdCache = ExtractMethodBody(harvestablePlant, "private void CacheRegistryServicesCold()");
                string plantCache = ExtractMethodBody(harvestablePlant, "private void CacheAudioService(");
                string plantResolve = ExtractMethodBody(harvestablePlant, "private IAudioService ResolveAudioService()");
                string plantUsable = ExtractMethodBody(harvestablePlant, "private static bool IsAudioServiceUsable(");
                string plantFlush = ExtractMethodBody(harvestablePlant, "private void FlushQueuedSegmentPresentation()");
                AssertContains(plantHotSwap, "CacheAudioService(currentService as IAudioService)", "Harvestable plant caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(plantColdCache, "CacheAudioService(GlobalRegistry.Audio)", "Harvestable plant cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(plantCache, "_audioService = IsAudioServiceUsable(audioService) ? audioService : null", "Harvestable plant stores only usable audio services", builder, ref failureCount);
                AssertContains(plantResolve, "if (IsAudioServiceUsable(audioService))", "Harvestable plant resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(plantResolve, "_audioService = null", "Harvestable plant resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(plantUsable, "audioService == null || !audioService.IsInitialized", "Harvestable plant rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(plantUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Harvestable plant rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(plantFlush, "IAudioService audio = ResolveAudioService()", "Harvestable plant cut sound uses the usable audio-service resolver", builder, ref failureCount);
                AssertNotContains(plantHotSwap, "_audioService = currentService as IAudioService", "Harvestable plant hot-swap never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(plantColdCache, "_audioService = GlobalRegistry.Audio", "Harvestable plant cold cache never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(plantFlush, "IAudioService audio = _audioService", "Harvestable plant cut sound never trusts the raw cached audio-service field", builder, ref failureCount);
            }

            if (harvestableOutcrop.Length > 0)
            {
                string outcropHotSwap = ExtractMethodBody(harvestableOutcrop, "public void OnGlobalRegistryServiceReplaced(");
                string outcropColdCache = ExtractMethodBody(harvestableOutcrop, "private void CacheRegistryServicesCold()");
                string outcropCache = ExtractMethodBody(harvestableOutcrop, "private void CacheAudioService(");
                string outcropResolve = ExtractMethodBody(harvestableOutcrop, "private IAudioService ResolveAudioService()");
                string outcropUsable = ExtractMethodBody(harvestableOutcrop, "private static bool IsAudioServiceUsable(");
                string outcropLateFrame = ExtractMethodBody(harvestableOutcrop, "public void LateFrameTick()");
                AssertContains(outcropHotSwap, "CacheAudioService(currentService as IAudioService)", "Harvestable outcrop caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(outcropColdCache, "CacheAudioService(GlobalRegistry.Audio)", "Harvestable outcrop cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(outcropCache, "_audioService = IsAudioServiceUsable(audioService) ? audioService : null", "Harvestable outcrop stores only usable audio services", builder, ref failureCount);
                AssertContains(outcropResolve, "if (IsAudioServiceUsable(audioService))", "Harvestable outcrop resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(outcropResolve, "_audioService = null", "Harvestable outcrop resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(outcropUsable, "audioService == null || !audioService.IsInitialized", "Harvestable outcrop rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(outcropUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Harvestable outcrop rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(outcropLateFrame, "IAudioService audio = ResolveAudioService()", "Harvestable outcrop hit and break sounds use the usable audio-service resolver", builder, ref failureCount);
                AssertNotContains(outcropHotSwap, "_audioService = currentService as IAudioService", "Harvestable outcrop hot-swap never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(outcropColdCache, "_audioService = GlobalRegistry.Audio", "Harvestable outcrop cold cache never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(outcropLateFrame, "IAudioService audio = _audioService", "Harvestable outcrop hit and break sounds never trust the raw cached audio-service field", builder, ref failureCount);
            }

            if (hostileFlora.Length > 0)
            {
                string floraHotSwap = ExtractMethodBody(hostileFlora, "public void OnGlobalRegistryServiceReplaced(");
                string floraColdCache = ExtractMethodBody(hostileFlora, "private void CacheRegistryServicesCold()");
                string floraCache = ExtractMethodBody(hostileFlora, "private void CacheAudioService(");
                string floraResolve = ExtractMethodBody(hostileFlora, "private IAudioService ResolveAudioService()");
                string floraUsable = ExtractMethodBody(hostileFlora, "private static bool IsAudioServiceUsable(");
                string floraLateFrame = ExtractMethodBody(hostileFlora, "public void LateFrameTick()");
                AssertContains(floraHotSwap, "CacheAudioService(currentService as IAudioService)", "Hostile flora caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(floraColdCache, "CacheAudioService(GlobalRegistry.Audio)", "Hostile flora cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(floraCache, "_audioService = IsAudioServiceUsable(audioService) ? audioService : null", "Hostile flora stores only usable audio services", builder, ref failureCount);
                AssertContains(floraResolve, "if (IsAudioServiceUsable(audioService))", "Hostile flora resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(floraResolve, "_audioService = null", "Hostile flora resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(floraUsable, "audioService == null || !audioService.IsInitialized", "Hostile flora rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(floraUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Hostile flora rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(floraLateFrame, "IAudioService audio = ResolveAudioService()", "Hostile flora shot sound uses the usable audio-service resolver", builder, ref failureCount);
                AssertNotContains(floraHotSwap, "_audioService = currentService as IAudioService", "Hostile flora hot-swap never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(floraColdCache, "_audioService = GlobalRegistry.Audio", "Hostile flora cold cache never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(floraLateFrame, "IAudioService audio = _audioService", "Hostile flora shot sound never trusts the raw cached audio-service field", builder, ref failureCount);
                AssertNotContains(floraLateFrame, "audio.IsInitialized", "Hostile flora late-frame audio does not duplicate partial usability checks", builder, ref failureCount);
            }

            if (floater.Length > 0)
            {
                string floaterHotSwap = ExtractMethodBody(floater, "public void OnGlobalRegistryServiceReplaced(");
                string floaterColdCache = ExtractMethodBody(floater, "private void CacheRegistryServicesCold()");
                string floaterCache = ExtractMethodBody(floater, "private void CacheAudioService(");
                string floaterResolve = ExtractMethodBody(floater, "private IAudioService ResolveAudioService()");
                string floaterUsable = ExtractMethodBody(floater, "private static bool IsAudioServiceUsable(");
                string floaterLateFrame = ExtractMethodBody(floater, "public void LateFrameTick()");
                AssertContains(floaterHotSwap, "CacheAudioService(currentService as IAudioService)", "Floater caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(floaterColdCache, "CacheAudioService(GlobalRegistry.Audio)", "Floater cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(floaterCache, "_audioService = IsAudioServiceUsable(audioService) ? audioService : null", "Floater stores only usable audio services", builder, ref failureCount);
                AssertContains(floaterResolve, "if (IsAudioServiceUsable(audioService))", "Floater resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(floaterResolve, "_audioService = null", "Floater resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(floaterUsable, "audioService == null || !audioService.IsInitialized", "Floater rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(floaterUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Floater rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(floaterLateFrame, "IAudioService audio = ResolveAudioService()", "Floater pickup and attach sounds use the usable audio-service resolver", builder, ref failureCount);
                AssertNotContains(floaterHotSwap, "_audioService = currentService as IAudioService", "Floater hot-swap never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(floaterColdCache, "_audioService = GlobalRegistry.Audio", "Floater cold cache never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(floaterLateFrame, "IAudioService audio = _audioService", "Floater pickup and attach sounds never trust the raw cached audio-service field", builder, ref failureCount);
            }

            if (deployableBeacon.Length > 0)
            {
                string beaconHotSwap = ExtractMethodBody(deployableBeacon, "public void OnGlobalRegistryServiceReplaced(");
                string beaconColdCache = ExtractMethodBody(deployableBeacon, "private void CacheRegistryServicesCold()");
                string beaconCache = ExtractMethodBody(deployableBeacon, "private void CacheAudioService(");
                string beaconResolve = ExtractMethodBody(deployableBeacon, "private IAudioService ResolveAudioService()");
                string beaconUsable = ExtractMethodBody(deployableBeacon, "private static bool IsAudioServiceUsable(");
                string beaconLateFrame = ExtractMethodBody(deployableBeacon, "public void LateFrameTick()");
                AssertContains(beaconHotSwap, "CacheAudioService(currentService as IAudioService)", "Deployable beacon caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(beaconColdCache, "CacheAudioService(GlobalRegistry.Audio)", "Deployable beacon cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(beaconCache, "_audioService = IsAudioServiceUsable(audioService) ? audioService : null", "Deployable beacon stores only usable audio services", builder, ref failureCount);
                AssertContains(beaconResolve, "if (IsAudioServiceUsable(audioService))", "Deployable beacon resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(beaconResolve, "_audioService = null", "Deployable beacon resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(beaconUsable, "audioService == null || !audioService.IsInitialized", "Deployable beacon rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(beaconUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Deployable beacon rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(beaconLateFrame, "IAudioService audioService = ResolveAudioService()", "Deployable beacon deploy and interact sounds use the usable audio-service resolver", builder, ref failureCount);
                AssertNotContains(beaconHotSwap, "_audioService = currentService as IAudioService", "Deployable beacon hot-swap never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(beaconColdCache, "_audioService = GlobalRegistry.Audio", "Deployable beacon cold cache never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(beaconLateFrame, "IAudioService audioService = _audioService", "Deployable beacon deploy and interact sounds never trust the raw cached audio-service field", builder, ref failureCount);
            }

            if (mountablePlayerTransport.Length > 0)
            {
                string transportHotSwap = ExtractMethodBody(mountablePlayerTransport, "public void OnGlobalRegistryServiceReplaced(");
                string transportRefresh = ExtractMethodBody(mountablePlayerTransport, "private void RefreshCachedRegistryServices()");
                string transportCache = ExtractMethodBody(mountablePlayerTransport, "private void CacheAudioService(");
                string transportResolve = ExtractMethodBody(mountablePlayerTransport, "private IAudioService ResolveAudioService()");
                string transportUsable = ExtractMethodBody(mountablePlayerTransport, "private static bool IsAudioServiceUsable(");
                string transportFlush = ExtractMethodBody(mountablePlayerTransport, "private void FlushQueuedTransportAudio()");
                AssertContains(transportHotSwap, "serviceSlot != GlobalRegistryServiceSlot.Audio", "Mountable transport listens for audio service rebinds", builder, ref failureCount);
                AssertContains(transportRefresh, "CacheAudioService(GlobalRegistry.Audio)", "Mountable transport cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(transportCache, "_cachedAudioService = IsAudioServiceUsable(audioService) ? audioService : null", "Mountable transport stores only usable audio services", builder, ref failureCount);
                AssertContains(transportResolve, "if (IsAudioServiceUsable(audioService))", "Mountable transport resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(transportResolve, "_cachedAudioService = null", "Mountable transport resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(transportUsable, "audioService == null || !audioService.IsInitialized", "Mountable transport rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(transportUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Mountable transport rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(transportFlush, "IAudioService audioService = ResolveAudioService()", "Mountable transport one-shot audio flush uses the usable audio-service resolver", builder, ref failureCount);
                AssertContains(transportFlush, "audioService.PlayAtPoint", "Mountable transport one-shot audio remains spatial", builder, ref failureCount);
                AssertNotContains(transportRefresh, "_cachedAudioService = GlobalRegistry.Audio", "Mountable transport cold cache never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(transportFlush, "_cachedAudioService.PlayAtPoint", "Mountable transport one-shot audio never calls through the raw cached audio-service field", builder, ref failureCount);
            }

            if (dropPodSeat.Length > 0)
            {
                string seatHotSwap = ExtractMethodBody(dropPodSeat, "public void OnGlobalRegistryServiceReplaced(");
                string seatColdCache = ExtractMethodBody(dropPodSeat, "private void CacheColdReferences()");
                string seatCache = ExtractMethodBody(dropPodSeat, "private void CacheAudioService(");
                string seatResolve = ExtractMethodBody(dropPodSeat, "private IAudioService ResolveAudioService()");
                string seatUsable = ExtractMethodBody(dropPodSeat, "private static bool IsAudioServiceUsable(");
                string seatQueue = ExtractMethodBody(dropPodSeat, "private void QueueAudio(");
                AssertContains(seatHotSwap, "CacheAudioService(currentService as IAudioService)", "Drop pod seat caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(seatColdCache, "CacheAudioService(GlobalRegistry.Audio)", "Drop pod seat cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(seatCache, "_audioService = IsAudioServiceUsable(audioService) ? audioService : null", "Drop pod seat stores only usable audio services", builder, ref failureCount);
                AssertContains(seatResolve, "if (IsAudioServiceUsable(audioService))", "Drop pod seat resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(seatResolve, "_audioService = null", "Drop pod seat resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(seatUsable, "audioService == null || !audioService.IsInitialized", "Drop pod seat rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(seatUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Drop pod seat rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(seatQueue, "IAudioService audio = ResolveAudioService()", "Drop pod seat queued audio uses the usable audio-service resolver", builder, ref failureCount);
                AssertContains(seatQueue, "audio.QueueAudioEvent(in audioEvent)", "Drop pod seat queued audio remains event-based", builder, ref failureCount);
                AssertNotContains(seatHotSwap, "_audioService = currentService as IAudioService", "Drop pod seat hot-swap never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(seatColdCache, "_audioService = GlobalRegistry.Audio", "Drop pod seat cold cache never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(seatQueue, "IAudioService audio = _audioService", "Drop pod seat queued audio never trusts the raw cached audio-service field", builder, ref failureCount);
                AssertNotContains(seatQueue, "audio.IsInitialized", "Drop pod seat queued audio does not duplicate stale initialization checks", builder, ref failureCount);
            }

            if (dropPodToggle.Length > 0)
            {
                string toggleHotSwap = ExtractMethodBody(dropPodToggle, "public void OnGlobalRegistryServiceReplaced(");
                string toggleColdCache = ExtractMethodBody(dropPodToggle, "private void CacheColdReferences()");
                string toggleCache = ExtractMethodBody(dropPodToggle, "private void CacheAudioService(");
                string toggleResolve = ExtractMethodBody(dropPodToggle, "private IAudioService ResolveAudioService()");
                string toggleUsable = ExtractMethodBody(dropPodToggle, "private static bool IsAudioServiceUsable(");
                string toggleQueue = ExtractMethodBody(dropPodToggle, "private void QueueAudio(");
                AssertContains(toggleHotSwap, "CacheAudioService(currentService as IAudioService)", "Drop pod toggle caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(toggleColdCache, "CacheAudioService(GlobalRegistry.Audio)", "Drop pod toggle cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(toggleCache, "_audioService = IsAudioServiceUsable(audioService) ? audioService : null", "Drop pod toggle stores only usable audio services", builder, ref failureCount);
                AssertContains(toggleResolve, "if (IsAudioServiceUsable(audioService))", "Drop pod toggle resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(toggleResolve, "_audioService = null", "Drop pod toggle resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(toggleUsable, "audioService == null || !audioService.IsInitialized", "Drop pod toggle rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(toggleUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Drop pod toggle rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(toggleQueue, "IAudioService audio = ResolveAudioService()", "Drop pod toggle queued audio uses the usable audio-service resolver", builder, ref failureCount);
                AssertContains(toggleQueue, "audio.QueueAudioEvent(in audioEvent)", "Drop pod toggle queued audio remains event-based", builder, ref failureCount);
                AssertNotContains(toggleHotSwap, "_audioService = currentService as IAudioService", "Drop pod toggle hot-swap never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(toggleColdCache, "_audioService = GlobalRegistry.Audio", "Drop pod toggle cold cache never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(toggleQueue, "IAudioService audio = _audioService", "Drop pod toggle queued audio never trusts the raw cached audio-service field", builder, ref failureCount);
                AssertNotContains(toggleQueue, "audio.IsInitialized", "Drop pod toggle queued audio does not duplicate stale initialization checks", builder, ref failureCount);
            }

            if (dropPodAirlock.Length > 0)
            {
                string airlockHotSwap = ExtractMethodBody(dropPodAirlock, "public void OnGlobalRegistryServiceReplaced(");
                string airlockColdCache = ExtractMethodBody(dropPodAirlock, "private void CacheColdReferences()");
                string airlockCache = ExtractMethodBody(dropPodAirlock, "private void CacheAudioService(");
                string airlockResolve = ExtractMethodBody(dropPodAirlock, "private IAudioService ResolveAudioService()");
                string airlockUsable = ExtractMethodBody(dropPodAirlock, "private static bool IsAudioServiceUsable(");
                string airlockQueue = ExtractMethodBody(dropPodAirlock, "private void QueueAudio(");
                AssertContains(airlockHotSwap, "CacheAudioService(currentService as IAudioService)", "Drop pod airlock caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(airlockColdCache, "CacheAudioService(GlobalRegistry.Audio)", "Drop pod airlock cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(airlockCache, "_audioService = IsAudioServiceUsable(audioService) ? audioService : null", "Drop pod airlock stores only usable audio services", builder, ref failureCount);
                AssertContains(airlockResolve, "if (IsAudioServiceUsable(audioService))", "Drop pod airlock resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(airlockResolve, "_audioService = null", "Drop pod airlock resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(airlockUsable, "audioService == null || !audioService.IsInitialized", "Drop pod airlock rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(airlockUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Drop pod airlock rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(airlockQueue, "IAudioService audio = ResolveAudioService()", "Drop pod airlock queued audio uses the usable audio-service resolver", builder, ref failureCount);
                AssertContains(airlockQueue, "audio.QueueAudioEvent(in audioEvent)", "Drop pod airlock queued audio remains event-based", builder, ref failureCount);
                AssertNotContains(airlockHotSwap, "_audioService = currentService as IAudioService", "Drop pod airlock hot-swap never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(airlockColdCache, "_audioService = GlobalRegistry.Audio", "Drop pod airlock cold cache never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(airlockQueue, "IAudioService audio = _audioService", "Drop pod airlock queued audio never trusts the raw cached audio-service field", builder, ref failureCount);
                AssertNotContains(airlockQueue, "audio.IsInitialized", "Drop pod airlock queued audio does not duplicate stale initialization checks", builder, ref failureCount);
            }

            if (baseAirlock.Length > 0)
            {
                string airlockHotSwap = ExtractMethodBody(baseAirlock, "public void OnGlobalRegistryServiceReplaced(");
                string airlockColdCache = ExtractMethodBody(baseAirlock, "private void CacheRegistryServicesCold()");
                string airlockClear = ExtractMethodBody(baseAirlock, "private void ClearCachedRegistryServices()");
                string airlockCache = ExtractMethodBody(baseAirlock, "private void CacheAudioService(");
                string airlockResolve = ExtractMethodBody(baseAirlock, "private IAudioService ResolveAudioService()");
                string airlockUsable = ExtractMethodBody(baseAirlock, "private static bool IsAudioServiceUsable(");
                string airlockStart = ExtractMethodBody(baseAirlock, "private void StartCycle(");
                string airlockFlush = ExtractMethodBody(baseAirlock, "private void FlushAirlockAudioPresentation()");
                AssertContains(airlockHotSwap, "CacheAudioService(currentService as IAudioService)", "Base airlock caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(airlockColdCache, "CacheAudioService(GlobalRegistry.Audio)", "Base airlock cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(airlockClear, "ClearCachedAudioService()", "Base airlock clears cached audio service through the shared helper", builder, ref failureCount);
                AssertContains(airlockCache, "_cachedAudioService = IsAudioServiceUsable(audioService) ? audioService : null", "Base airlock stores only usable audio services", builder, ref failureCount);
                AssertContains(airlockResolve, "if (IsAudioServiceUsable(audioService))", "Base airlock resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(airlockResolve, "ClearCachedAudioService()", "Base airlock resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(airlockUsable, "audioService == null || !audioService.IsInitialized", "Base airlock rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(airlockUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Base airlock rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(airlockStart, "IAudioService audio = ResolveAudioService()", "Base airlock cycle start sound uses the usable audio-service resolver", builder, ref failureCount);
                AssertContains(airlockFlush, "IAudioService audio = ResolveAudioService()", "Base airlock cycle end sound uses the usable audio-service resolver", builder, ref failureCount);
                AssertNotContains(airlockStart, "IAudioService audio = _cachedAudioService", "Base airlock cycle start never trusts the raw cached audio-service field", builder, ref failureCount);
                AssertNotContains(airlockFlush, "IAudioService audio = _cachedAudioService", "Base airlock cycle end never trusts the raw cached audio-service field", builder, ref failureCount);
            }

            if (batteryCharger.Length > 0)
            {
                string chargerHotSwap = ExtractMethodBody(batteryCharger, "public void OnGlobalRegistryServiceReplaced(");
                string chargerColdCache = ExtractMethodBody(batteryCharger, "private void CacheRegistryServicesCold()");
                string chargerCache = ExtractMethodBody(batteryCharger, "private void CacheAudioService(");
                string chargerResolve = ExtractMethodBody(batteryCharger, "private IAudioService ResolveAudioService()");
                string chargerUsable = ExtractMethodBody(batteryCharger, "private static bool IsAudioServiceUsable(");
                string chargerFlush = ExtractMethodBody(batteryCharger, "private void FlushQueuedChargerAudio()");
                AssertContains(chargerHotSwap, "CacheAudioService(currentService as IAudioService)", "Battery charger caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(chargerColdCache, "CacheAudioService(GlobalRegistry.Audio)", "Battery charger cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(chargerCache, "_cachedAudioService = IsAudioServiceUsable(audioService) ? audioService : null", "Battery charger stores only usable audio services", builder, ref failureCount);
                AssertContains(chargerResolve, "if (IsAudioServiceUsable(audioService))", "Battery charger resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(chargerResolve, "_cachedAudioService = null", "Battery charger resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(chargerUsable, "audioService == null || !audioService.IsInitialized", "Battery charger rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(chargerUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Battery charger rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(chargerFlush, "IAudioService audio = ResolveAudioService()", "Battery charger queued audio flush uses the usable audio-service resolver", builder, ref failureCount);
                AssertNotContains(chargerFlush, "IAudioService audio = _cachedAudioService", "Battery charger queued audio never trusts the raw cached audio-service field", builder, ref failureCount);
            }

            if (sealedDoor.Length > 0)
            {
                string doorHotSwap = ExtractMethodBody(sealedDoor, "public void OnGlobalRegistryServiceReplaced(");
                string doorColdCache = ExtractMethodBody(sealedDoor, "private void CacheColdDependencies()");
                string doorCache = ExtractMethodBody(sealedDoor, "private void CacheAudioService(");
                string doorResolve = ExtractMethodBody(sealedDoor, "private IAudioService ResolveAudioService()");
                string doorUsable = ExtractMethodBody(sealedDoor, "private static bool IsAudioServiceUsable(");
                string doorFlush = ExtractMethodBody(sealedDoor, "private void FlushPresentation()");
                AssertContains(doorHotSwap, "CacheAudioService(currentService as IAudioService)", "Sealed door caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(doorColdCache, "CacheAudioService(GlobalRegistry.Audio)", "Sealed door cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(doorCache, "_cachedAudioService = IsAudioServiceUsable(audioService) ? audioService : null", "Sealed door stores only usable audio services", builder, ref failureCount);
                AssertContains(doorResolve, "if (IsAudioServiceUsable(audioService))", "Sealed door resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(doorResolve, "_cachedAudioService = null", "Sealed door resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(doorUsable, "audioService == null || !audioService.IsInitialized", "Sealed door rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(doorUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Sealed door rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(doorFlush, "IAudioService audio = ResolveAudioService()", "Sealed door cutting/open sounds use the usable audio-service resolver", builder, ref failureCount);
                AssertNotContains(doorFlush, "IAudioService audio = _cachedAudioService", "Sealed door presentation audio never trusts the raw cached audio-service field", builder, ref failureCount);
            }

            if (constructionManager.Length > 0)
            {
                string constructionHotSwap = ExtractMethodBody(constructionManager, "public void OnGlobalRegistryServiceReplaced(");
                string constructionColdCache = ExtractMethodBody(constructionManager, "private void CacheRegistryServicesCold()");
                string constructionBind = ExtractMethodBody(constructionManager, "private void BindConstructionRuntimeServices()");
                string constructionClearCached = ExtractMethodBody(constructionManager, "private void ClearCachedRegistryServices()");
                string constructionCache = ExtractMethodBody(constructionManager, "private void CacheAudioService(");
                string constructionResolve = ExtractMethodBody(constructionManager, "private IAudioService ResolveAudioService()");
                string constructionClearAudio = ExtractMethodBody(constructionManager, "private void ClearCachedAudioService()");
                string constructionUsable = ExtractMethodBody(constructionManager, "private static bool IsAudioServiceUsable(");
                string habitatGraphDispose = ExtractMethodBody(habitatGraphManager, "public void Dispose()");
                string habitatGraphSetRuntime = ExtractMethodBody(habitatGraphManager, "internal void SetRuntimeServices(");
                string habitatGraphSetAudio = ExtractMethodBody(habitatGraphManager, "internal void SetAudioService(");
                string habitatGraphPublish = ExtractMethodBody(habitatGraphManager, "private void PublishHullStressSignal(");
                string habitatGraphAudioCache = ExtractMethodBody(habitatGraphManager, "private void CacheAudioService(");
                string habitatGraphAudioResolve = ExtractMethodBody(habitatGraphManager, "private IAudioService ResolveAudioService()");
                string habitatGraphClearAudio = ExtractMethodBody(habitatGraphManager, "private void ClearCachedAudioService()");
                string habitatGraphAudioUsable = ExtractMethodBody(habitatGraphManager, "private static bool IsAudioServiceUsable(");
                AssertContains(constructionColdCache, "CacheAudioService(GlobalRegistry.Audio)", "Construction manager cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(constructionBind, "ResolveAudioService(),", "Construction manager binds habitat graph audio through the usable-service resolver", builder, ref failureCount);
                AssertContains(constructionClearCached, "ClearCachedAudioService()", "Construction manager clears cached audio service with registry services", builder, ref failureCount);
                AssertContains(constructionCache, "_cachedAudioService = IsAudioServiceUsable(audioService) ? audioService : null", "Construction manager stores only usable audio services", builder, ref failureCount);
                AssertContains(constructionResolve, "if (IsAudioServiceUsable(audioService))", "Construction manager resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(constructionResolve, "ClearCachedAudioService()", "Construction manager resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(constructionClearAudio, "_cachedAudioService = null", "Construction manager clears cached audio service explicitly", builder, ref failureCount);
                AssertContains(constructionUsable, "audioService == null || !audioService.IsInitialized", "Construction manager rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(constructionUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Construction manager rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(constructionHotSwap, "CacheAudioService(currentService as IAudioService)", "Construction manager caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(constructionHotSwap, "_habitatGraphManager?.SetAudioService(ResolveAudioService())", "Construction manager rebinds habitat graph audio through the resolver on hot swap", builder, ref failureCount);
                AssertNotContains(constructionColdCache, "_cachedAudioService = GlobalRegistry.Audio", "Construction manager cold cache never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(constructionHotSwap, "_cachedAudioService = currentService as IAudioService", "Construction manager hot-swap never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(constructionBind, "_cachedAudioService,", "Construction manager graph bind never passes the raw cached audio-service field", builder, ref failureCount);
                AssertContains(habitatGraphDispose, "ClearCachedAudioService()", "Habitat graph clears cached audio service on dispose", builder, ref failureCount);
                AssertContains(habitatGraphSetRuntime, "CacheAudioService(audioService)", "Habitat graph runtime-service bind filters audio service usability", builder, ref failureCount);
                AssertContains(habitatGraphSetAudio, "CacheAudioService(audioService)", "Habitat graph audio hot-swap bind filters audio service usability", builder, ref failureCount);
                AssertContains(habitatGraphPublish, "IAudioService audioService = ResolveAudioService()", "Habitat graph hull-stress signal resolves only usable audio services", builder, ref failureCount);
                AssertContains(habitatGraphAudioCache, "_audioService = IsAudioServiceUsable(audioService) ? audioService : null", "Habitat graph stores only usable audio services", builder, ref failureCount);
                AssertContains(habitatGraphAudioResolve, "if (IsAudioServiceUsable(audioService))", "Habitat graph resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(habitatGraphAudioResolve, "ClearCachedAudioService()", "Habitat graph resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(habitatGraphClearAudio, "_audioService = null", "Habitat graph clears cached audio service explicitly", builder, ref failureCount);
                AssertContains(habitatGraphAudioUsable, "audioService == null || !audioService.IsInitialized", "Habitat graph rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(habitatGraphAudioUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Habitat graph rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertNotContains(habitatGraphSetRuntime, "_audioService = audioService", "Habitat graph runtime-service bind never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(habitatGraphSetAudio, "_audioService = audioService", "Habitat graph audio hot-swap bind never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(habitatGraphPublish, "GetCachedAudioService()", "Habitat graph hull-stress path never trusts a raw cached audio-service getter", builder, ref failureCount);
            }

            if (fabricator.Length > 0)
            {
                string fabricatorHotSwap = ExtractMethodBody(fabricator, "public void OnGlobalRegistryServiceReplaced(");
                string fabricatorColdCache = ExtractMethodBody(fabricator, "private void CacheRegistryServicesCold()");
                string fabricatorCache = ExtractMethodBody(fabricator, "private void CacheAudioService(");
                string fabricatorResolve = ExtractMethodBody(fabricator, "private IAudioService ResolveAudioService()");
                string fabricatorClear = ExtractMethodBody(fabricator, "private void ClearCachedAudioService()");
                string fabricatorUsable = ExtractMethodBody(fabricator, "private static bool IsAudioServiceUsable(");
                string fabricatorOnDisable = ExtractMethodBody(fabricator, "private void OnDisable()");
                string fabricatorOnDestroy = ExtractMethodBody(fabricator, "private void OnDestroy()");
                string fabricatorFlush = ExtractMethodBody(fabricator, "private void FlushPendingAudio()");
                AssertContains(fabricatorHotSwap, "CacheAudioService(currentService as IAudioService)", "Fabricator caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(fabricatorColdCache, "CacheAudioService(GlobalRegistry.Audio)", "Fabricator cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(fabricatorCache, "_audioService = IsAudioServiceUsable(audioService) ? audioService : null", "Fabricator stores only usable audio services", builder, ref failureCount);
                AssertContains(fabricatorResolve, "if (IsAudioServiceUsable(audioService))", "Fabricator resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(fabricatorResolve, "ClearCachedAudioService()", "Fabricator resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(fabricatorClear, "_audioService = null", "Fabricator clears cached audio service explicitly", builder, ref failureCount);
                AssertContains(fabricatorUsable, "audioService == null || !audioService.IsInitialized", "Fabricator rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(fabricatorUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Fabricator rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(fabricatorOnDisable, "ClearCachedAudioService()", "Fabricator clears audio service on disable", builder, ref failureCount);
                AssertContains(fabricatorOnDestroy, "ClearCachedAudioService()", "Fabricator clears audio service on destroy", builder, ref failureCount);
                AssertContains(fabricatorFlush, "IAudioService audioService = ResolveAudioService()", "Fabricator queued craft sounds use the usable audio-service resolver", builder, ref failureCount);
                AssertNotContains(fabricatorHotSwap, "_audioService = currentService as IAudioService", "Fabricator hot-swap never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(fabricatorColdCache, "_audioService = GlobalRegistry.Audio", "Fabricator cold cache never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(fabricatorFlush, "_audioService?.PlayAtPoint", "Fabricator audio flush never calls through the raw cached audio-service field", builder, ref failureCount);
            }

            if (playerBuilder.Length > 0)
            {
                string builderHotSwap = ExtractMethodBody(playerBuilder, "protected override void OnToolRegistryServiceReplaced(");
                string builderBind = ExtractMethodBody(playerBuilder, "private void BindRuntimeReferences()");
                string builderCache = ExtractMethodBody(playerBuilder, "private void CacheAudioService(");
                string builderResolve = ExtractMethodBody(playerBuilder, "private IAudioService ResolveAudioService()");
                string builderClear = ExtractMethodBody(playerBuilder, "private void ClearCachedAudioService()");
                string builderUsable = ExtractMethodBody(playerBuilder, "private static bool IsAudioServiceUsable(");
                string builderFlush = ExtractMethodBody(playerBuilder, "private void FlushPendingBuilderAudio()");
                AssertContains(builderHotSwap, "case GlobalRegistryServiceSlot.Audio", "Player builder listens for audio service hot swaps", builder, ref failureCount);
                AssertContains(builderHotSwap, "CacheAudioService(currentService as IAudioService)", "Player builder caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(builderBind, "if (!IsAudioServiceUsable(_cachedAudioService))", "Player builder refreshes cold audio when the cached service is stale", builder, ref failureCount);
                AssertContains(builderBind, "CacheAudioService(GlobalRegistry.Audio)", "Player builder cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(builderCache, "_cachedAudioService = IsAudioServiceUsable(audioService) ? audioService : null", "Player builder stores only usable audio services", builder, ref failureCount);
                AssertContains(builderResolve, "if (IsAudioServiceUsable(audioService))", "Player builder resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(builderResolve, "ClearCachedAudioService()", "Player builder resolver clears stale audio-service references", builder, ref failureCount);
                AssertContains(builderClear, "_cachedAudioService = null", "Player builder clears cached audio service explicitly", builder, ref failureCount);
                AssertContains(builderUsable, "audioService == null || !audioService.IsInitialized", "Player builder rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(builderUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Player builder rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(builderFlush, "IAudioService audioService = ResolveAudioService()", "Player builder queued build sounds use the usable audio-service resolver", builder, ref failureCount);
                AssertNotContains(builderHotSwap, "_cachedAudioService = currentService as IAudioService", "Player builder hot-swap never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(builderBind, "_cachedAudioService = GlobalRegistry.Audio", "Player builder cold bind never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(builderFlush, "IAudioService audioService = _cachedAudioService", "Player builder queued audio never trusts the raw cached audio-service field", builder, ref failureCount);
            }

            if (laserCutter.Length > 0)
            {
                string laserHotSwap = ExtractMethodBody(laserCutter, "public void OnGlobalRegistryServiceReplaced(");
                string laserColdCache = ExtractMethodBody(laserCutter, "private void CacheColdDependencies()");
                string laserClearCold = ExtractMethodBody(laserCutter, "private void ClearColdDependencies()");
                string laserCache = ExtractMethodBody(laserCutter, "private void CacheAudioService(");
                string laserResolve = ExtractMethodBody(laserCutter, "private IAudioService ResolveAudioService()");
                string laserResidencyResolve = ExtractMethodBody(laserCutter, "private IAudioResidencyService ResolveAudioResidencyService()");
                string laserClearAudio = ExtractMethodBody(laserCutter, "private void ClearCachedAudioService()");
                string laserUsable = ExtractMethodBody(laserCutter, "private static bool IsAudioServiceUsable(");
                string laserPrewarm = ExtractMethodBody(laserCutter, "private void PrewarmEquippedAudio()");
                string laserRelease = ExtractMethodBody(laserCutter, "private void ReleaseEquippedAudio()");
                string laserOverheat = ExtractMethodBody(laserCutter, "private void ApplyOverheatLockoutCue()");
                AssertContains(laserHotSwap, "case GlobalRegistryServiceSlot.Audio", "Laser cutter listens for audio service hot swaps", builder, ref failureCount);
                AssertContains(laserHotSwap, "CacheAudioService(currentService as IAudioService)", "Laser cutter caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(laserColdCache, "CacheAudioService(GlobalRegistry.Audio)", "Laser cutter cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertNotContains(laserColdCache, "_cachedAudioService = GlobalRegistry.Audio", "Laser cutter cold cache does not store raw audio service directly", builder, ref failureCount);
                AssertContains(laserClearCold, "ClearCachedAudioService()", "Laser cutter clears dependent audio service caches together", builder, ref failureCount);
                AssertContains(laserCache, "ClearCachedAudioService()", "Laser cutter clears residency and mixer cache when audio service is unusable", builder, ref failureCount);
                AssertContains(laserCache, "_cachedAudioResidencyService = audioService as IAudioResidencyService", "Laser cutter derives residency cache only from usable audio service", builder, ref failureCount);
                AssertContains(laserCache, "_cachedCutAudioMixerGroup = audioService.AmbientGroup", "Laser cutter derives mixer cache only from usable audio service", builder, ref failureCount);
                AssertContains(laserResolve, "if (IsAudioServiceUsable(audioService))", "Laser cutter resolver accepts only usable cached audio service", builder, ref failureCount);
                AssertContains(laserResolve, "ClearCachedAudioService()", "Laser cutter resolver clears dependent stale audio caches", builder, ref failureCount);
                AssertContains(laserResidencyResolve, "ResolveAudioService() != null ? _cachedAudioResidencyService : null", "Laser cutter residency route is gated by usable audio service", builder, ref failureCount);
                AssertContains(laserClearAudio, "_cachedAudioService = null", "Laser cutter clears cached audio service explicitly", builder, ref failureCount);
                AssertContains(laserClearAudio, "_cachedAudioResidencyService = null", "Laser cutter clears cached audio residency service explicitly", builder, ref failureCount);
                AssertContains(laserClearAudio, "_cachedCutAudioMixerGroup = null", "Laser cutter clears cached audio mixer group explicitly", builder, ref failureCount);
                AssertContains(laserUsable, "audioService == null || !audioService.IsInitialized", "Laser cutter rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(laserUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Laser cutter rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(laserPrewarm, "IAudioResidencyService residency = ResolveAudioResidencyService()", "Laser cutter audio prewarm uses the residency resolver", builder, ref failureCount);
                AssertContains(laserRelease, "IAudioResidencyService residency = ResolveAudioResidencyService()", "Laser cutter audio release uses the residency resolver", builder, ref failureCount);
                AssertContains(laserOverheat, "IAudioService audioService = ResolveAudioService()", "Laser cutter overheat cue uses the usable audio-service resolver", builder, ref failureCount);
                AssertNotContains(laserOverheat, "_cachedAudioService.PlayStatic2D", "Laser cutter overheat cue never calls through the raw cached audio-service field", builder, ref failureCount);
            }

            if (acousticZone.Length > 0)
            {
                string acousticAwake = ExtractMethodBody(acousticZone, "private void Awake()");
                string acousticOnEnable = ExtractMethodBody(acousticZone, "private void OnEnable()");
                string acousticRegisterService = ExtractMethodBody(acousticZone, "private void TryRegisterService()");
                string acousticExistingRuntime = ExtractMethodBody(acousticZone, "private bool TryAbortForUsableExistingRuntime()");
                string acousticRuntimeUsable = ExtractMethodBody(acousticZone, "private static bool IsAcousticZoneRuntimeUsable(");
                string acousticPlayMadness = ExtractMethodBody(acousticZone, "public void PlayMadnessWhisperCue()");
                string acousticEmitterOcclusion = ExtractMethodBody(acousticZone, "private void UpdateEmitterOcclusionState(AudioListener listener)");
                string acousticHotSwap = ExtractMethodBody(acousticZone, "public void OnGlobalRegistryServiceReplaced(");
                string acousticColdCache = ExtractMethodBody(acousticZone, "private void CacheRegistryServicesCold()");
                string acousticTick = ExtractMethodBody(acousticZone, "public void Tick(");
                string acousticAmbientMix = ExtractMethodBody(acousticZone, "private void UpdateAmbientLoopMix(");
                string acousticAudioCache = ExtractMethodBody(acousticZone, "private void CacheAudioService(");
                string acousticAudioResolver = ExtractMethodBody(acousticZone, "private IAudioService ResolveAudioService()");
                string acousticAudioUsable = ExtractMethodBody(acousticZone, "private static bool IsAudioServiceUsable(");
                string acousticSpatialResolver = ExtractMethodBody(acousticZone, "private ISpatialAudioWorldEmitterReadModel ResolveSpatialAudioEmitterReadModel()");
                string acousticMusicDuckUpdate = ExtractMethodBody(acousticZone, "private void UpdateMusicAmbientDucking(");
                string acousticMusicDuckTarget = ExtractMethodBody(acousticZone, "private float ResolveMusicAmbientDuckTarget01(");
                string acousticMusicDuckVolume = ExtractMethodBody(acousticZone, "private float ResolveMusicAmbientDuckVolumeScale()");
                AssertContains(acousticAwake, "if (TryAbortForUsableExistingRuntime())", "Acoustic-zone Awake routes duplicate-owner checks through the stale-owner gate", builder, ref failureCount);
                AssertContains(acousticOnEnable, "if (TryAbortForUsableExistingRuntime())", "Acoustic-zone OnEnable routes duplicate-owner checks through the stale-owner gate", builder, ref failureCount);
                AssertTextBefore(acousticOnEnable, "if (TryAbortForUsableExistingRuntime())", "CacheRegistryServicesCold();", "Acoustic-zone clears stale owners before OnEnable cold-cache work", builder, ref failureCount);
                AssertTextBefore(acousticOnEnable, "if (TryAbortForUsableExistingRuntime())", "TryRegisterHotSwapListener();", "Acoustic-zone clears stale owners before OnEnable hot-swap subscription", builder, ref failureCount);
                AssertTextBefore(acousticOnEnable, "if (TryAbortForUsableExistingRuntime())", "AtmosphereEvents.Register(this);", "Acoustic-zone clears stale owners before atmosphere event subscription", builder, ref failureCount);
                AssertContains(acousticRegisterService, "if (TryAbortForUsableExistingRuntime())", "Acoustic-zone registration routes duplicate-owner checks through the stale-owner gate", builder, ref failureCount);
                AssertTextBefore(acousticRegisterService, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterAcousticZoneRuntime(this);", "Acoustic-zone clears stale owners before self-register", builder, ref failureCount);
                AssertContains(acousticExistingRuntime, "AcousticZoneController active = s_activeRuntimeInstance", "Acoustic-zone stale-owner gate checks the active runtime mirror", builder, ref failureCount);
                AssertContains(acousticExistingRuntime, "AcousticZoneController registered = GlobalRegistry.AcousticZone", "Acoustic-zone stale-owner gate checks the registry owner", builder, ref failureCount);
                AssertContains(acousticExistingRuntime, "if (IsAcousticZoneRuntimeUsable(active))", "Acoustic-zone preserves usable active runtime owners", builder, ref failureCount);
                AssertContains(acousticExistingRuntime, "if (IsAcousticZoneRuntimeUsable(registered))", "Acoustic-zone preserves usable registered runtime owners", builder, ref failureCount);
                AssertContains(acousticExistingRuntime, "GlobalRegistry.UnregisterAcousticZoneRuntime(active);", "Acoustic-zone clears stale active registry owners before registering", builder, ref failureCount);
                AssertContains(acousticExistingRuntime, "GlobalRegistry.UnregisterAcousticZoneRuntime(registered);", "Acoustic-zone clears stale registered owners before registering", builder, ref failureCount);
                AssertContains(acousticExistingRuntime, "s_activeRuntimeInstance = null", "Acoustic-zone clears stale active-runtime mirror", builder, ref failureCount);
                AssertContains(acousticRuntimeUsable, "controller._serviceRegistered", "Acoustic-zone validates existing owner registration state", builder, ref failureCount);
                AssertContains(acousticRuntimeUsable, "controller.isActiveAndEnabled", "Acoustic-zone validates existing owner activity", builder, ref failureCount);
                AssertNotContains(acousticAwake, "registered != null && registered != this", "Acoustic-zone Awake no longer treats stale owners as hard conflicts", builder, ref failureCount);
                AssertNotContains(acousticRegisterService, "registered != null && registered != this", "Acoustic-zone registration no longer treats stale owners as hard conflicts", builder, ref failureCount);
                AssertContains(globalSignals, "[StructLayout(LayoutKind.Explicit, Size = 16)]", "Acoustic-zone typed signal has explicit ARM64 layout with manual offsets", builder, ref failureCount);
                AssertContains(globalSignals, "public readonly struct AcousticZoneChangedEvent : ISignal", "Acoustic-zone transition payload is an immutable typed signal", builder, ref failureCount);
                AssertContains(globalSignals, "[FieldOffset(0)] public readonly byte IsInterior", "Acoustic-zone payload avoids bool field layout ambiguity", builder, ref failureCount);
                AssertContains(acousticZone, "SignalBus<AcousticZoneChangedEvent>.TryPushTracked(in payload", "Acoustic-zone transitions publish through the typed SignalBus lane with explicit drop semantics", builder, ref failureCount);
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
                AssertContains(acousticAudioCache, "if (!IsAudioServiceUsable(audioService))", "Acoustic-zone rejects unusable audio services while caching", builder, ref failureCount);
                AssertContains(acousticAudioCache, "_cachedSpatialAudioEmitterReadModel = null", "Acoustic-zone clears cached spatial read-model when audio service is unusable", builder, ref failureCount);
                AssertContains(acousticAudioResolver, "if (IsAudioServiceUsable(audioService))", "Acoustic-zone resolves cached audio services only while usable", builder, ref failureCount);
                AssertContains(acousticAudioResolver, "_cachedSpatialAudioEmitterReadModel = null", "Acoustic-zone clears stale spatial read-model with stale audio service", builder, ref failureCount);
                AssertContains(acousticAudioUsable, "audioService is Behaviour behaviour", "Acoustic-zone validates MonoBehaviour-backed audio service activity", builder, ref failureCount);
                AssertContains(acousticAudioUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Acoustic-zone rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(acousticSpatialResolver, "if (audioService == null)", "Acoustic-zone spatial read-model resolver fails closed when audio service is stale", builder, ref failureCount);
                AssertContains(acousticZone, "ClearCachedRegistryServices()", "Acoustic-zone clears cached registry services on disable/destroy", builder, ref failureCount);
                AssertContains(acousticHotSwap, "GlobalRegistryServiceSlot.MusicDirectorRuntime", "Acoustic-zone listens for music-director runtime hot swaps", builder, ref failureCount);
                AssertContains(acousticColdCache, "CacheMusicDirector(GlobalRegistry.MusicDirector)", "Acoustic-zone cold-seeds music director for ambient ducking", builder, ref failureCount);
                AssertContains(acousticTick, "UpdateMusicAmbientDucking(currentZone, deltaTime)", "Acoustic-zone refreshes music ambient ducking on the game-thread tick", builder, ref failureCount);
                AssertContains(acousticAmbientMix, "targetVolume *= ResolveMusicAmbientDuckVolumeScale();", "Underwater ambient loop yields volume to active music foreground", builder, ref failureCount);
                AssertContains(acousticMusicDuckUpdate, "ApproximateOneMinusExpNegPositive(math.max(0.01f, sharpness) * deltaTime)", "Music ambient ducking is smoothed with the existing cheap exponential helper", builder, ref failureCount);
                AssertContains(acousticMusicDuckTarget, "HectonMusicDirector musicDirector = _cachedMusicDirector", "Acoustic-zone music duck reads the cached music director only", builder, ref failureCount);
                AssertContains(acousticMusicDuckTarget, "HectonMusicDirector.MusicActivityReason.Emergency", "Emergency breath bypasses music ambient ducking", builder, ref failureCount);
                AssertContains(acousticMusicDuckTarget, "musicDirector.CurrentMusicActivity01", "Music ambient ducking follows the director's published activity scalar", builder, ref failureCount);
                AssertContains(acousticMusicDuckVolume, "math.max(0.1f, 1f - musicAmbientDuckMax)", "Music ambient ducking preserves a floor under the ocean bed", builder, ref failureCount);
                AssertNotContains(acousticMusicDuckTarget, "GlobalRegistry.", "Music ambient duck target does not poll registries from tick routing", builder, ref failureCount);
                AssertNotContains(acousticAmbientMix, "GlobalRegistry.", "Ambient loop mix does not poll registries while applying music duck", builder, ref failureCount);
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

            if (audioLogSystem.Length > 0)
            {
                string audioLogRegister = ExtractMethodBody(audioLogSystem, "private void TryRegisterService()");
                string audioLogSystemUsable = ExtractMethodBody(audioLogSystem, "private static bool IsAudioLogSystemUsable(");
                string audioLogSlowTick = ExtractMethodBody(audioLogSystem, "public void SlowTick()");
                string audioLogWarningBlocker = ExtractMethodBody(audioLogSystem, "private bool TickAtmosphericWarningBlocker()");
                string audioLogQueueEnqueue = ExtractMethodBody(audioLogSystem, "private void EnqueuePlayback");
                string audioLogQueueStart = ExtractMethodBody(audioLogSystem, "private void TryStartNextQueuedLog()");
                string audioLogQueueDedupRebuild = ExtractMethodBody(audioLogSystem, "private void RebuildQueuedLogHashDedupFromQueue");
                string audioLogStop = ExtractMethodBody(audioLogSystem, "public void StopPlayback()");
                string audioLogAudioCache = ExtractMethodBody(audioLogSystem, "private void CacheAudioService(");
                string audioLogAudioResolver = ExtractMethodBody(audioLogSystem, "private IAudioService ResolveAudioService()");
                string audioLogAudioUsable = ExtractMethodBody(audioLogSystem, "private static bool IsAudioServiceUsable(");
                string audioLogSinkResolver = ExtractMethodBody(audioLogSystem, "private ISpatialAudioNarrativeRadioSink ResolveNarrativeAudioSink()");
                string audioLogSinkUsable = ExtractMethodBody(audioLogSystem, "private static bool IsNarrativeAudioSinkUsable(");
                string audioLogPlaybackQueue = ExtractMethodBody(audioLogSystem, "private bool QueuePlaybackVisualSync(");
                string audioLogPlaybackFlush = ExtractMethodBody(audioLogSystem, "private void FlushPendingPlaybackVisualSync()");
                string audioLogGlitchRefresh = ExtractMethodBody(audioLogSystem, "private void RefreshActiveNarrativeRadioGlitchVisualSync()");
                string audioLogGlitchReset = ExtractMethodBody(audioLogSystem, "private void FlushPendingNarrativeRadioGlitchReset()");
                string audioLogAcquire = ExtractMethodBody(audioLogSystem, "private bool TryAcquireVaultMutation");
                string audioLogTelemetry = ExtractMethodBody(audioLogSystem, "private void RecordVaultTelemetry");
                AssertContains(audioLogSystem, "public bool IsNarrativeQueueBlocked => _isPlaying || _atmosphericWarningActive || _queueCount > 0", "Audio-log runtime exposes queued narrative speech as foreground-blocking state", builder, ref failureCount);
                AssertContains(audioLogRegister, "AudioLogSystem registeredAudioLogs = GlobalRegistry.AudioLogs", "Audio-log owner registration snapshots the current registry owner once", builder, ref failureCount);
                AssertContains(audioLogRegister, "!ReferenceEquals(registeredAudioLogs, null)", "Audio-log owner registration detects stale destroyed registry references by actual reference", builder, ref failureCount);
                AssertContains(audioLogRegister, "!ReferenceEquals(registeredAudioLogs, this)", "Audio-log owner registration only treats other owners as conflicts", builder, ref failureCount);
                AssertContains(audioLogRegister, "if (IsAudioLogSystemUsable(registeredAudioLogs))", "Audio-log owner registration preserves usable existing owners", builder, ref failureCount);
                AssertContains(audioLogRegister, "Destroy(gameObject);", "Audio-log duplicate self-destroys only when the existing owner is usable", builder, ref failureCount);
                AssertContains(audioLogRegister, "GlobalRegistry.UnregisterAudioLogRuntime(registeredAudioLogs);", "Audio-log owner registration clears stale existing owners before registering", builder, ref failureCount);
                AssertTextBefore(audioLogRegister, "GlobalRegistry.UnregisterAudioLogRuntime(registeredAudioLogs);", "GlobalRegistry.RegisterAudioLogRuntime(this);", "Audio-log owner registration unregisters stale owners before self-register", builder, ref failureCount);
                AssertContains(audioLogSystemUsable, "return audioLogSystem != null && audioLogSystem.isActiveAndEnabled", "Audio-log owner usability rejects destroyed or disabled owners", builder, ref failureCount);
                AssertContains(audioLogStop, "bool hadPlayback = _isPlaying || stoppedLog != null || _pendingPlaybackDirty || _currentPlaybackBitCrushed", "Audio-log stop records active playback state before clearing pending speech", builder, ref failureCount);
                AssertContains(audioLogStop, "ClearPlaybackQueue();", "Stopping audio-log playback clears pending queued speech so music does not remain ducked", builder, ref failureCount);
                AssertContains(audioLogStop, "if (!hadPlayback)", "Audio-log stop clears queued speech before deciding whether to publish a stopped-playback event", builder, ref failureCount);
                AssertContains(audioLogSlowTick, "bool queuedPlaybackStarted = TickAtmosphericWarningBlocker();", "Audio-log slow tick detects queued playback started by atmospheric warning expiry", builder, ref failureCount);
                AssertContains(audioLogWarningBlocker, "return !wasPlaying && _isPlaying;", "Audio-log warning blocker reports newly started queued playback without trimming its first tick", builder, ref failureCount);
                AssertContains(audioLogSlowTick, "if (!_isPlaying && !_atmosphericWarningActive && _queueCount > 0)", "Audio-log slow tick retries queued speech when a previous guarded queue drain could not start", builder, ref failureCount);
                AssertContains(audioLogSlowTick, "TryStartNextQueuedLog();", "Audio-log slow tick owns idle queued-playback recovery", builder, ref failureCount);
                AssertContains(audioLogQueueEnqueue, "TryAcquirePlaybackQueueMutationView", "Audio-log enqueue writes queue state through mutation-guarded vault views", builder, ref failureCount);
                AssertContains(audioLogQueueEnqueue, "ReleaseVaultMutation", "Audio-log enqueue releases queue mutation guards in finally", builder, ref failureCount);
                AssertContains(audioLogQueueStart, "TryAcquirePlaybackQueueMutationView", "Audio-log queue drain reads queued speech through mutation-guarded vault views", builder, ref failureCount);
                AssertContains(audioLogQueueStart, "ReleaseVaultMutation", "Audio-log queue drain releases queue mutation guards before starting playback", builder, ref failureCount);
                AssertContains(audioLogQueueStart, "RebuildQueuedLogHashDedupFromQueue(queue, _playbackQueueReadIndex, _queueCount)", "Audio-log queue drain rebuilds dedupe state from the remaining fixed queue", builder, ref failureCount);
                AssertContains(audioLogQueueDedupRebuild, "ClearQueuedLogHashes();", "Audio-log queue dedupe rebuild starts from a clean fixed buffer", builder, ref failureCount);
                AssertContains(audioLogQueueDedupRebuild, "!IsPlaybackQueued(logHash)", "Audio-log queue dedupe rebuild preserves unique queued hashes only", builder, ref failureCount);
                AssertContains(audioLogAudioCache, "if (!IsAudioServiceUsable(audioService))", "Audio-log runtime rejects unusable audio services while caching", builder, ref failureCount);
                AssertContains(audioLogAudioCache, "_cachedNarrativeAudioSink = null", "Audio-log runtime clears cached narrative sink when audio service is unusable", builder, ref failureCount);
                AssertContains(audioLogAudioResolver, "if (IsAudioServiceUsable(audioService))", "Audio-log runtime resolves cached audio service only while usable", builder, ref failureCount);
                AssertContains(audioLogAudioResolver, "_cachedNarrativeAudioSink = null", "Audio-log runtime clears stale narrative sink with stale audio service", builder, ref failureCount);
                AssertContains(audioLogAudioUsable, "audioService is Behaviour behaviour", "Audio-log runtime validates MonoBehaviour-backed audio service activity", builder, ref failureCount);
                AssertContains(audioLogAudioUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Audio-log runtime rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(audioLogSinkResolver, "IAudioService audioService = ResolveAudioService()", "Audio-log narrative sink resolver is gated by usable audio service", builder, ref failureCount);
                AssertContains(audioLogSinkUsable, "narrativeAudioSink is Behaviour behaviour", "Audio-log runtime validates MonoBehaviour-backed narrative sinks", builder, ref failureCount);
                AssertContains(audioLogPlaybackQueue, "ResolveNarrativeAudioSink()", "Audio-log playback queue detects bit-crush availability through usable sink resolver", builder, ref failureCount);
                AssertContains(audioLogPlaybackFlush, "ResolveNarrativeAudioSink()", "Audio-log playback flush uses the usable narrative sink resolver", builder, ref failureCount);
                AssertContains(audioLogPlaybackFlush, "ResolveAudioService()", "Audio-log playback fallback uses the usable audio-service resolver", builder, ref failureCount);
                AssertContains(audioLogGlitchRefresh, "ResolveNarrativeAudioSink()", "Audio-log active glitch visual sync uses the usable narrative sink resolver", builder, ref failureCount);
                AssertContains(audioLogGlitchReset, "ResolveNarrativeAudioSink()", "Audio-log glitch reset uses the usable narrative sink resolver", builder, ref failureCount);
                AssertContains(audioLogAcquire, "TryAcquireMutationGuard", "Audio-log vault mutation helper acquires mutation guards instead of write locks", builder, ref failureCount);
                AssertContains(audioLogAcquire, "TryResolveHandle", "Audio-log vault mutation helper resolves owner views after guard acquisition", builder, ref failureCount);
                AssertContains(audioLogTelemetry, "TryAcquireMutationGuard(TelemetryMutationGuardMask)", "Audio-log vault telemetry writes through a telemetry mutation guard", builder, ref failureCount);
                AssertContains(audioLogTelemetry, "TryResolveHandle(in _telemetryRingHandle", "Audio-log vault telemetry resolves the telemetry ring under guard", builder, ref failureCount);
                AssertNotContains(audioLogSystem, "TryAcquireWriteLock", "Audio-log runtime avoids direct DataVault write locks", builder, ref failureCount);
                AssertNotContains(audioLogSystem, "ReleaseWriteLock", "Audio-log runtime avoids direct DataVault write-lock release calls", builder, ref failureCount);
            }

            if (firstHourDirector.Length > 0)
            {
                string firstHourHotSwap = ExtractMethodBody(firstHourDirector, "public void OnGlobalRegistryServiceReplaced(");
                string firstHourColdCache = ExtractMethodBody(firstHourDirector, "private void CacheRuntimeServices()");
                string firstHourAudioLogCache = ExtractMethodBody(firstHourDirector, "private void CacheAudioLogSystem(");
                string firstHourAudioLogResolve = ExtractMethodBody(firstHourDirector, "private IAudioLogRuntime ResolveAudioLogSystem()");
                string firstHourAudioLogUsable = ExtractMethodBody(firstHourDirector, "private static bool IsAudioLogRuntimeUsable(");
                string firstHourContextSync = ExtractMethodBody(firstHourDirector, "private void SynchronizeContextFromRuntimeSystems()");
                AssertContains(firstHourHotSwap, "CacheAudioLogSystem(currentService as IAudioLogRuntime)", "First-hour route context caches audio-log hot swaps through the usable-runtime filter", builder, ref failureCount);
                AssertContains(firstHourColdCache, "CacheAudioLogSystem(Hecton8.Core.GlobalRegistry.AudioLogRuntime)", "First-hour route context cold-caches audio-log runtime through the usable-runtime filter", builder, ref failureCount);
                AssertContains(firstHourAudioLogCache, "_cachedAudioLogSystem = IsAudioLogRuntimeUsable(audioLogSystem) ? audioLogSystem : null", "First-hour route context stores only usable audio-log runtimes", builder, ref failureCount);
                AssertContains(firstHourAudioLogResolve, "if (IsAudioLogRuntimeUsable(audioLogSystem))", "First-hour route context resolves cached audio-log runtimes only while usable", builder, ref failureCount);
                AssertContains(firstHourAudioLogResolve, "_cachedAudioLogSystem = null", "First-hour route context clears stale audio-log runtime references", builder, ref failureCount);
                AssertContains(firstHourAudioLogUsable, "audioLogSystem is Behaviour behaviour", "First-hour route context validates MonoBehaviour-backed audio-log runtimes", builder, ref failureCount);
                AssertContains(firstHourAudioLogUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "First-hour route context rejects destroyed or disabled audio-log runtimes", builder, ref failureCount);
                AssertContains(firstHourContextSync, "IAudioLogRuntime audioLogSystem = ResolveAudioLogSystem()", "First-hour route-contact sync reads audio-log discovery state through the usable resolver", builder, ref failureCount);
                AssertNotContains(firstHourContextSync, "_cachedAudioLogSystem", "First-hour route-contact sync never reads the raw cached audio-log runtime directly", builder, ref failureCount);
            }

            if (emergencyServiceRelay.Length > 0)
            {
                string relayOnDisable = ExtractMethodBody(emergencyServiceRelay, "private void OnDisable()");
                string relayOnDestroy = ExtractMethodBody(emergencyServiceRelay, "private void OnDestroy()");
                string relayInteract = ExtractMethodBody(emergencyServiceRelay, "public void Interact(");
                string relayHotSwap = ExtractMethodBody(emergencyServiceRelay, "public void OnGlobalRegistryServiceReplaced(");
                string relayColdCache = ExtractMethodBody(emergencyServiceRelay, "private void CacheRegistryServicesCold()");
                string relayClearCache = ExtractMethodBody(emergencyServiceRelay, "private void ClearCachedRegistryServices()");
                string relayAudioLogCache = ExtractMethodBody(emergencyServiceRelay, "private void CacheAudioLogSystem(");
                string relayAudioLogResolve = ExtractMethodBody(emergencyServiceRelay, "private IAudioLogRuntime ResolveAudioLogSystem()");
                string relayAudioLogUsable = ExtractMethodBody(emergencyServiceRelay, "private static bool IsAudioLogRuntimeUsable(");
                AssertContains(relayOnDisable, "ClearCachedRegistryServices();", "Emergency relay clears cached runtime services on disable", builder, ref failureCount);
                AssertContains(relayOnDestroy, "ClearCachedRegistryServices();", "Emergency relay clears cached runtime services on destroy", builder, ref failureCount);
                AssertContains(relayInteract, "IAudioLogRuntime audioLogSystem = ResolveAudioLogSystem()", "Emergency relay linked-log playback resolves only usable audio-log runtimes", builder, ref failureCount);
                AssertContains(relayHotSwap, "CacheAudioLogSystem(currentService as IAudioLogRuntime)", "Emergency relay caches audio-log hot swaps through the usable-runtime filter", builder, ref failureCount);
                AssertContains(relayColdCache, "CacheAudioLogSystem(GlobalRegistry.AudioLogRuntime)", "Emergency relay cold-caches audio-log runtime through the usable-runtime filter", builder, ref failureCount);
                AssertContains(relayClearCache, "_cachedAudioLogSystem = null", "Emergency relay clears cached audio-log runtime references", builder, ref failureCount);
                AssertContains(relayAudioLogCache, "_cachedAudioLogSystem = IsAudioLogRuntimeUsable(audioLogSystem) ? audioLogSystem : null", "Emergency relay stores only usable audio-log runtimes", builder, ref failureCount);
                AssertContains(relayAudioLogResolve, "if (IsAudioLogRuntimeUsable(audioLogSystem))", "Emergency relay resolves cached audio-log runtime only while usable", builder, ref failureCount);
                AssertContains(relayAudioLogResolve, "_cachedAudioLogSystem = null", "Emergency relay clears stale audio-log runtime references", builder, ref failureCount);
                AssertContains(relayAudioLogUsable, "audioLogSystem is Behaviour behaviour", "Emergency relay validates MonoBehaviour-backed audio-log runtimes", builder, ref failureCount);
                AssertContains(relayAudioLogUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Emergency relay rejects destroyed or disabled audio-log runtimes", builder, ref failureCount);
                AssertNotContains(relayInteract, "_cachedAudioLogSystem", "Emergency relay interaction never reads the raw cached audio-log runtime directly", builder, ref failureCount);
            }

            if (narrativeDiscovery.Length > 0)
            {
                string discoveryOnDisable = ExtractMethodBody(narrativeDiscovery, "private void OnDisable()");
                string discoveryOnDestroy = ExtractMethodBody(narrativeDiscovery, "private void OnDestroy()");
                string discoveryHotSwap = ExtractMethodBody(narrativeDiscovery, "public void OnGlobalRegistryServiceReplaced(");
                string discoveryColdCache = ExtractMethodBody(narrativeDiscovery, "private void CacheRegistryServicesCold()");
                string discoveryClearCache = ExtractMethodBody(narrativeDiscovery, "private void ClearCachedRegistryServices()");
                string discoveryAudioLogCache = ExtractMethodBody(narrativeDiscovery, "private void CacheAudioLogSystem(");
                string discoveryAudioLogResolve = ExtractMethodBody(narrativeDiscovery, "private IAudioLogRuntime ResolveAudioLogSystem()");
                string discoveryAudioLogUsable = ExtractMethodBody(narrativeDiscovery, "private static bool IsAudioLogRuntimeUsable(");
                string discoveryPlayLinkedLog = ExtractMethodBody(narrativeDiscovery, "private bool TryPlayLinkedAudioLog()");
                AssertContains(discoveryOnDisable, "ClearCachedRegistryServices();", "Narrative discovery clears cached runtime services on disable", builder, ref failureCount);
                AssertContains(discoveryOnDestroy, "ClearCachedRegistryServices();", "Narrative discovery clears cached runtime services on destroy", builder, ref failureCount);
                AssertContains(discoveryHotSwap, "CacheAudioLogSystem(currentService as IAudioLogRuntime)", "Narrative discovery caches audio-log hot swaps through the usable-runtime filter", builder, ref failureCount);
                AssertContains(discoveryColdCache, "CacheAudioLogSystem(GlobalRegistry.AudioLogRuntime)", "Narrative discovery cold-caches audio-log runtime through the usable-runtime filter", builder, ref failureCount);
                AssertContains(discoveryClearCache, "_audioLogs = null", "Narrative discovery clears cached audio-log runtime references", builder, ref failureCount);
                AssertContains(discoveryAudioLogCache, "_audioLogs = IsAudioLogRuntimeUsable(audioLogSystem) ? audioLogSystem : null", "Narrative discovery stores only usable audio-log runtimes", builder, ref failureCount);
                AssertContains(discoveryAudioLogResolve, "if (IsAudioLogRuntimeUsable(audioLogSystem))", "Narrative discovery resolves cached audio-log runtime only while usable", builder, ref failureCount);
                AssertContains(discoveryAudioLogResolve, "_audioLogs = null", "Narrative discovery clears stale audio-log runtime references", builder, ref failureCount);
                AssertContains(discoveryAudioLogUsable, "audioLogSystem is Behaviour behaviour", "Narrative discovery validates MonoBehaviour-backed audio-log runtimes", builder, ref failureCount);
                AssertContains(discoveryAudioLogUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Narrative discovery rejects destroyed or disabled audio-log runtimes", builder, ref failureCount);
                AssertContains(discoveryPlayLinkedLog, "IAudioLogRuntime audioLogs = ResolveAudioLogSystem()", "Narrative discovery linked-log playback resolves only usable audio-log runtimes", builder, ref failureCount);
                AssertNotContains(discoveryPlayLinkedLog, "_audioLogs", "Narrative discovery linked-log playback never reads the raw cached audio-log runtime directly", builder, ref failureCount);
            }

            if (playerHealth.Length > 0)
            {
                string healthAdvisory = ExtractMethodBody(playerHealth, "private void TryIssueRadiationAdvisory(");
                string healthOnDisable = ExtractMethodBody(playerHealth, "private void OnDisable()");
                string healthOnDestroy = ExtractMethodBody(playerHealth, "private void OnDestroy()");
                string healthHotSwap = ExtractMethodBody(playerHealth, "private void OnRegistryServiceReplaced(");
                string healthColdCache = ExtractMethodBody(playerHealth, "private void CacheRegistryServicesCold()");
                string healthClearCache = ExtractMethodBody(playerHealth, "private void ClearCachedRegistryServices()");
                string healthAudioCache = ExtractMethodBody(playerHealth, "private void CacheAudioService(");
                string healthAudioResolve = ExtractMethodBody(playerHealth, "private IAudioService ResolveAudioService()");
                string healthAudioUsable = ExtractMethodBody(playerHealth, "private static bool IsAudioServiceUsable(");
                string healthAudioLogCache = ExtractMethodBody(playerHealth, "private void CacheAudioLogSystem(");
                string healthAudioLogResolve = ExtractMethodBody(playerHealth, "private IAudioLogRuntime ResolveAudioLogSystem()");
                string healthAudioLogUsable = ExtractMethodBody(playerHealth, "private static bool IsAudioLogRuntimeUsable(");
                string healthHeartbeat = ExtractMethodBody(playerHealth, "private void PlaySurvivalGraceHeartbeatPulse()");
                string healthFlush = ExtractMethodBody(playerHealth, "private void FlushQueuedPresentationFeedback()");
                AssertContains(healthAdvisory, "IAudioLogRuntime audioLogs = ResolveAudioLogSystem()", "Player health atmospheric warnings resolve only usable audio-log runtimes", builder, ref failureCount);
                AssertContains(healthAdvisory, "audioLogs.NotifyAtmosphericWarningStarted(glitchDuration)", "Player health atmospheric warnings notify the resolved audio-log runtime", builder, ref failureCount);
                AssertContains(healthOnDisable, "ClearCachedRegistryServices();", "Player health clears cached runtime services on disable", builder, ref failureCount);
                AssertContains(healthOnDestroy, "ClearCachedRegistryServices();", "Player health clears cached runtime services on destroy", builder, ref failureCount);
                AssertContains(healthHotSwap, "CacheAudioService(currentService as IAudioService)", "Player health caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(healthHotSwap, "CacheAudioLogSystem(currentService as IAudioLogRuntime)", "Player health caches audio-log hot swaps through the usable-runtime filter", builder, ref failureCount);
                AssertContains(healthColdCache, "CacheAudioService(GlobalRegistry.Audio)", "Player health cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(healthColdCache, "CacheAudioLogSystem(GlobalRegistry.AudioLogRuntime)", "Player health cold-caches audio-log runtime through the usable-runtime filter", builder, ref failureCount);
                AssertContains(healthClearCache, "_audioService = null", "Player health clears cached audio service references", builder, ref failureCount);
                AssertContains(healthClearCache, "_audioLogs = null", "Player health clears cached audio-log runtime references", builder, ref failureCount);
                AssertContains(healthAudioCache, "_audioService = IsAudioServiceUsable(audioService) ? audioService : null", "Player health stores only usable audio services", builder, ref failureCount);
                AssertContains(healthAudioResolve, "if (IsAudioServiceUsable(audioService))", "Player health resolves cached audio service only while usable", builder, ref failureCount);
                AssertContains(healthAudioResolve, "_audioService = null", "Player health clears stale audio service references", builder, ref failureCount);
                AssertContains(healthAudioUsable, "audioService == null || !audioService.IsInitialized", "Player health rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(healthAudioUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Player health rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(healthAudioLogCache, "_audioLogs = IsAudioLogRuntimeUsable(audioLogSystem) ? audioLogSystem : null", "Player health stores only usable audio-log runtimes", builder, ref failureCount);
                AssertContains(healthAudioLogResolve, "if (IsAudioLogRuntimeUsable(audioLogSystem))", "Player health resolves cached audio-log runtime only while usable", builder, ref failureCount);
                AssertContains(healthAudioLogResolve, "_audioLogs = null", "Player health clears stale audio-log runtime references", builder, ref failureCount);
                AssertContains(healthAudioLogUsable, "audioLogSystem is Behaviour behaviour", "Player health validates MonoBehaviour-backed audio-log runtimes", builder, ref failureCount);
                AssertContains(healthAudioLogUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Player health rejects destroyed or disabled audio-log runtimes", builder, ref failureCount);
                AssertContains(healthHeartbeat, "ResolveAudioService() == null", "Player health heartbeat queue is gated by usable audio-service resolution", builder, ref failureCount);
                AssertContains(healthFlush, "IAudioService audioService = ResolveAudioService()", "Player health heartbeat flush uses the usable audio-service resolver", builder, ref failureCount);
                AssertNotContains(healthAdvisory, "_audioLogs", "Player health atmospheric warning path never reads the raw cached audio-log runtime directly", builder, ref failureCount);
                AssertNotContains(healthColdCache, "_audioService = GlobalRegistry.Audio", "Player health cold cache never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(healthHotSwap, "_audioService = currentService as IAudioService", "Player health hot-swap never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(healthFlush, "IAudioService audioService = _audioService", "Player health heartbeat flush never trusts the raw cached audio-service field", builder, ref failureCount);
            }

            if (submarineAtmosphere.Length > 0)
            {
                string atmosphereOnDisable = ExtractMethodBody(submarineAtmosphere, "private void OnDisable()");
                string atmosphereOnDestroy = ExtractMethodBody(submarineAtmosphere, "private void OnDestroy()");
                string atmosphereHotSwap = ExtractMethodBody(submarineAtmosphere, "public void OnGlobalRegistryServiceReplaced(");
                string atmosphereColdCache = ExtractMethodBody(submarineAtmosphere, "private void CacheReferencesCold()");
                string atmosphereClearCache = ExtractMethodBody(submarineAtmosphere, "private void ClearCachedRuntimeServices()");
                string atmosphereAudioCache = ExtractMethodBody(submarineAtmosphere, "private void CacheAudioService(");
                string atmosphereAudioResolve = ExtractMethodBody(submarineAtmosphere, "private IAudioService ResolveAudioService()");
                string atmosphereAudioUsable = ExtractMethodBody(submarineAtmosphere, "private static bool IsAudioServiceUsable(");
                string atmosphereAudioLogCache = ExtractMethodBody(submarineAtmosphere, "private void CacheAudioLogSystem(");
                string atmosphereAudioLogResolve = ExtractMethodBody(submarineAtmosphere, "private AudioLogSystem ResolveAudioLogSystem()");
                string atmosphereAudioLogUsable = ExtractMethodBody(submarineAtmosphere, "private static bool IsAudioLogSystemUsable(");
                string atmosphereQueue = ExtractMethodBody(submarineAtmosphere, "private void QueueLowOxygenGaspingAudioLog()");
                string atmosphereFlush = ExtractMethodBody(submarineAtmosphere, "private void FlushQueuedAtmosphereAudio()");
                AssertContains(atmosphereOnDisable, "ClearCachedRuntimeServices();", "Submarine atmosphere clears cached runtime services on disable", builder, ref failureCount);
                AssertContains(atmosphereOnDestroy, "ClearCachedRuntimeServices();", "Submarine atmosphere clears cached runtime services on destroy", builder, ref failureCount);
                AssertContains(atmosphereHotSwap, "CacheAudioService(currentService as IAudioService)", "Submarine atmosphere caches audio hot swaps through the usable-service filter", builder, ref failureCount);
                AssertContains(atmosphereHotSwap, "CacheAudioLogSystem(currentService as AudioLogSystem)", "Submarine atmosphere caches audio-log hot swaps through the usable-runtime filter", builder, ref failureCount);
                AssertContains(atmosphereColdCache, "CacheAudioService(GlobalRegistry.Audio)", "Submarine atmosphere cold-caches audio through the usable-service filter", builder, ref failureCount);
                AssertContains(atmosphereColdCache, "CacheAudioLogSystem(GlobalRegistry.AudioLogs)", "Submarine atmosphere cold-caches audio-log runtime through the usable-runtime filter", builder, ref failureCount);
                AssertContains(atmosphereClearCache, "_audioService = null", "Submarine atmosphere clears cached audio service references", builder, ref failureCount);
                AssertContains(atmosphereClearCache, "_audioLogs = null", "Submarine atmosphere clears cached audio-log runtime references", builder, ref failureCount);
                AssertContains(atmosphereAudioCache, "_audioService = IsAudioServiceUsable(audioService) ? audioService : null", "Submarine atmosphere stores only usable audio services", builder, ref failureCount);
                AssertContains(atmosphereAudioResolve, "if (IsAudioServiceUsable(audioService))", "Submarine atmosphere resolves cached audio service only while usable", builder, ref failureCount);
                AssertContains(atmosphereAudioResolve, "_audioService = null", "Submarine atmosphere clears stale audio service references", builder, ref failureCount);
                AssertContains(atmosphereAudioUsable, "audioService == null || !audioService.IsInitialized", "Submarine atmosphere rejects uninitialized audio services", builder, ref failureCount);
                AssertContains(atmosphereAudioUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Submarine atmosphere rejects destroyed or disabled audio services", builder, ref failureCount);
                AssertContains(atmosphereAudioLogCache, "_audioLogs = IsAudioLogSystemUsable(audioLogs) ? audioLogs : null", "Submarine atmosphere stores only usable audio-log runtimes", builder, ref failureCount);
                AssertContains(atmosphereAudioLogResolve, "if (IsAudioLogSystemUsable(audioLogs))", "Submarine atmosphere resolves cached audio-log runtime only while usable", builder, ref failureCount);
                AssertContains(atmosphereAudioLogResolve, "_audioLogs = null", "Submarine atmosphere clears stale audio-log runtime references", builder, ref failureCount);
                AssertContains(atmosphereAudioLogUsable, "return audioLogs != null && audioLogs.isActiveAndEnabled", "Submarine atmosphere rejects destroyed or disabled audio-log runtimes", builder, ref failureCount);
                AssertContains(atmosphereQueue, "AudioLogSystem audioLogs = ResolveAudioLogSystem()", "Submarine atmosphere low-oxygen gasping log resolves only usable audio-log runtimes", builder, ref failureCount);
                AssertContains(atmosphereFlush, "AudioLogSystem audioLogs = ResolveAudioLogSystem()", "Submarine atmosphere audio flush resolves only usable audio-log runtimes", builder, ref failureCount);
                AssertContains(atmosphereFlush, "IAudioService audioService = ResolveAudioService()", "Submarine atmosphere pressure screech uses the usable audio-service resolver", builder, ref failureCount);
                AssertNotContains(atmosphereQueue, "_audioLogs", "Submarine atmosphere low-oxygen gasping queue never reads the raw cached audio-log runtime directly", builder, ref failureCount);
                AssertNotContains(atmosphereFlush, "_audioLogs", "Submarine atmosphere audio flush never reads the raw cached audio-log runtime directly", builder, ref failureCount);
                AssertNotContains(atmosphereColdCache, "_audioService = GlobalRegistry.Audio", "Submarine atmosphere cold cache never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(atmosphereHotSwap, "_audioService = currentService as IAudioService", "Submarine atmosphere hot-swap never stores raw audio service directly", builder, ref failureCount);
                AssertNotContains(atmosphereFlush, "IAudioService audioService = _audioService", "Submarine atmosphere pressure screech never trusts the raw cached audio-service field", builder, ref failureCount);
            }

            if (signalBeacon.Length > 0)
            {
                string beaconOnDisable = ExtractMethodBody(signalBeacon, "private void OnDisable()");
                string beaconOnDestroy = ExtractMethodBody(signalBeacon, "private void OnDestroy()");
                string beaconSolve = ExtractMethodBody(signalBeacon, "private void SolveTelemetry()");
                string beaconRecover = ExtractMethodBody(signalBeacon, "private bool TryRecoverFragment()");
                string beaconHotSwap = ExtractMethodBody(signalBeacon, "public void OnGlobalRegistryServiceReplaced(");
                string beaconColdCache = ExtractMethodBody(signalBeacon, "private void CacheRegistryServicesCold()");
                string beaconClearCache = ExtractMethodBody(signalBeacon, "private void ClearCachedRegistryServices()");
                string beaconAudioLogCache = ExtractMethodBody(signalBeacon, "private void CacheAudioLogSystem(");
                string beaconAudioLogResolve = ExtractMethodBody(signalBeacon, "private IAudioLogRuntime ResolveAudioLogSystem()");
                string beaconAudioLogUsable = ExtractMethodBody(signalBeacon, "private static bool IsAudioLogRuntimeUsable(");
                AssertContains(beaconOnDisable, "ClearCachedRegistryServices();", "Signal beacon clears cached runtime services on disable", builder, ref failureCount);
                AssertContains(beaconOnDestroy, "ClearCachedRegistryServices();", "Signal beacon clears cached runtime services on destroy", builder, ref failureCount);
                AssertContains(beaconSolve, "IAudioLogRuntime audioLogs = ResolveAudioLogSystem()", "Signal beacon telemetry reads encrypted-log state through the usable resolver", builder, ref failureCount);
                AssertContains(beaconRecover, "IAudioLogRuntime audioLogs = ResolveAudioLogSystem()", "Signal beacon fragment recovery writes through the usable audio-log resolver", builder, ref failureCount);
                AssertContains(beaconHotSwap, "CacheAudioLogSystem(currentService as IAudioLogRuntime)", "Signal beacon caches audio-log hot swaps through the usable-runtime filter", builder, ref failureCount);
                AssertContains(beaconColdCache, "CacheAudioLogSystem(GlobalRegistry.AudioLogRuntime)", "Signal beacon cold-caches audio-log runtime through the usable-runtime filter", builder, ref failureCount);
                AssertContains(beaconClearCache, "_audioLogs = null", "Signal beacon clears cached audio-log runtime references", builder, ref failureCount);
                AssertContains(beaconAudioLogCache, "_audioLogs = IsAudioLogRuntimeUsable(audioLogSystem) ? audioLogSystem : null", "Signal beacon stores only usable audio-log runtimes", builder, ref failureCount);
                AssertContains(beaconAudioLogResolve, "if (IsAudioLogRuntimeUsable(audioLogSystem))", "Signal beacon resolves cached audio-log runtime only while usable", builder, ref failureCount);
                AssertContains(beaconAudioLogResolve, "_audioLogs = null", "Signal beacon clears stale audio-log runtime references", builder, ref failureCount);
                AssertContains(beaconAudioLogUsable, "audioLogSystem is Behaviour behaviour", "Signal beacon validates MonoBehaviour-backed audio-log runtimes", builder, ref failureCount);
                AssertContains(beaconAudioLogUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Signal beacon rejects destroyed or disabled audio-log runtimes", builder, ref failureCount);
                AssertNotContains(beaconSolve, "_audioLogs", "Signal beacon telemetry never reads the raw cached audio-log runtime directly", builder, ref failureCount);
                AssertNotContains(beaconRecover, "_audioLogs", "Signal beacon fragment recovery never reads the raw cached audio-log runtime directly", builder, ref failureCount);
            }

            if (atlasSignalSystem.Length > 0)
            {
                string atlasHotSwap = ExtractMethodBody(atlasSignalSystem, "public void OnGlobalRegistryServiceReplaced(");
                string atlasColdCache = ExtractMethodBody(atlasSignalSystem, "private void CacheRuntimeDependencies()");
                string atlasClearCache = ExtractMethodBody(atlasSignalSystem, "private void ClearRuntimeDependencies()");
                string atlasAudioLogCache = ExtractMethodBody(atlasSignalSystem, "private void CacheAudioLogSystem(");
                string atlasAudioLogResolve = ExtractMethodBody(atlasSignalSystem, "private IAudioLogRuntime ResolveAudioLogSystem()");
                string atlasAudioLogUsable = ExtractMethodBody(atlasSignalSystem, "private static bool IsAudioLogRuntimeUsable(");
                string atlasReveal = ExtractMethodBody(atlasSignalSystem, "private void RevealEncryptedLogForStage(");
                AssertContains(atlasHotSwap, "CacheAudioLogSystem(currentService as IAudioLogRuntime)", "Atlas signal system caches audio-log hot swaps through the usable-runtime filter", builder, ref failureCount);
                AssertContains(atlasColdCache, "CacheAudioLogSystem(GlobalRegistry.AudioLogRuntime)", "Atlas signal system cold-caches audio-log runtime through the usable-runtime filter", builder, ref failureCount);
                AssertContains(atlasClearCache, "_audioLogs = null", "Atlas signal system clears cached audio-log runtime references", builder, ref failureCount);
                AssertContains(atlasAudioLogCache, "_audioLogs = IsAudioLogRuntimeUsable(audioLogSystem) ? audioLogSystem : null", "Atlas signal system stores only usable audio-log runtimes", builder, ref failureCount);
                AssertContains(atlasAudioLogResolve, "if (IsAudioLogRuntimeUsable(audioLogSystem))", "Atlas signal system resolves cached audio-log runtime only while usable", builder, ref failureCount);
                AssertContains(atlasAudioLogResolve, "_audioLogs = null", "Atlas signal system clears stale audio-log runtime references", builder, ref failureCount);
                AssertContains(atlasAudioLogUsable, "audioLogSystem is Behaviour behaviour", "Atlas signal system validates MonoBehaviour-backed audio-log runtimes", builder, ref failureCount);
                AssertContains(atlasAudioLogUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Atlas signal system rejects destroyed or disabled audio-log runtimes", builder, ref failureCount);
                AssertContains(atlasReveal, "IAudioLogRuntime audioLogs = ResolveAudioLogSystem()", "Atlas signal encrypted-log reveal resolves only usable audio-log runtimes", builder, ref failureCount);
                AssertNotContains(atlasReveal, "_audioLogs", "Atlas signal encrypted-log reveal never reads the raw cached audio-log runtime directly", builder, ref failureCount);
            }

            if (atlas6CorporateLiabilityManager.Length > 0)
            {
                string atlas6OnEnable = ExtractMethodBody(atlas6CorporateLiabilityManager, "private void OnEnable()");
                string atlas6OnDisable = ExtractMethodBody(atlas6CorporateLiabilityManager, "private void OnDisable()");
                string atlas6OnDestroy = ExtractMethodBody(atlas6CorporateLiabilityManager, "private void OnDestroy()");
                string atlas6HotSwap = ExtractMethodBody(atlas6CorporateLiabilityManager, "public void OnGlobalRegistryServiceReplaced(");
                string atlas6ClearCache = ExtractMethodBody(atlas6CorporateLiabilityManager, "private void ClearCachedRuntimeServices()");
                string atlas6Sync = ExtractMethodBody(atlas6CorporateLiabilityManager, "private void TrySyncDisasterEvidenceFromAudioLogRuntime()");
                string atlas6AudioLogCache = ExtractMethodBody(atlas6CorporateLiabilityManager, "private void CacheAudioLogSystem(");
                string atlas6AudioLogResolve = ExtractMethodBody(atlas6CorporateLiabilityManager, "private IAudioLogRuntime ResolveAudioLogSystem()");
                string atlas6AudioLogUsable = ExtractMethodBody(atlas6CorporateLiabilityManager, "private static bool IsAudioLogRuntimeUsable(");
                AssertContains(atlas6OnEnable, "CacheAudioLogSystem(GlobalRegistry.AudioLogRuntime)", "Atlas-6 liability manager cold-caches audio-log runtime through the usable-runtime filter", builder, ref failureCount);
                AssertContains(atlas6OnEnable, "TrySyncDisasterEvidenceFromAudioLogRuntime()", "Atlas-6 liability manager syncs disaster evidence after resolving cached audio-log runtime", builder, ref failureCount);
                AssertContains(atlas6OnDisable, "ClearCachedRuntimeServices();", "Atlas-6 liability manager clears cached runtime services on disable", builder, ref failureCount);
                AssertContains(atlas6OnDestroy, "ClearCachedRuntimeServices();", "Atlas-6 liability manager clears cached runtime services on destroy", builder, ref failureCount);
                AssertContains(atlas6HotSwap, "CacheAudioLogSystem(currentService as IAudioLogRuntime)", "Atlas-6 liability manager caches audio-log hot swaps through the usable-runtime filter", builder, ref failureCount);
                AssertContains(atlas6HotSwap, "TrySyncDisasterEvidenceFromAudioLogRuntime()", "Atlas-6 liability manager re-syncs evidence after audio-log hot swaps", builder, ref failureCount);
                AssertContains(atlas6ClearCache, "_audioLogs = null", "Atlas-6 liability manager clears cached audio-log runtime references", builder, ref failureCount);
                AssertContains(atlas6Sync, "IAudioLogRuntime audioLogRuntime = ResolveAudioLogSystem()", "Atlas-6 liability evidence sync resolves only usable audio-log runtimes", builder, ref failureCount);
                AssertContains(atlas6AudioLogCache, "_audioLogs = IsAudioLogRuntimeUsable(audioLogSystem) ? audioLogSystem : null", "Atlas-6 liability manager stores only usable audio-log runtimes", builder, ref failureCount);
                AssertContains(atlas6AudioLogResolve, "if (IsAudioLogRuntimeUsable(audioLogSystem))", "Atlas-6 liability manager resolves cached audio-log runtime only while usable", builder, ref failureCount);
                AssertContains(atlas6AudioLogResolve, "_audioLogs = null", "Atlas-6 liability manager clears stale audio-log runtime references", builder, ref failureCount);
                AssertContains(atlas6AudioLogUsable, "audioLogSystem is Behaviour behaviour", "Atlas-6 liability manager validates MonoBehaviour-backed audio-log runtimes", builder, ref failureCount);
                AssertContains(atlas6AudioLogUsable, "return behaviour != null && behaviour.isActiveAndEnabled", "Atlas-6 liability manager rejects destroyed or disabled audio-log runtimes", builder, ref failureCount);
                AssertNotContains(atlas6OnEnable, "TrySyncDisasterEvidenceFromAudioLogRuntime(" + "GlobalRegistry.AudioLogRuntime)", "Atlas-6 liability manager does not sync from a raw cold audio-log registry read", builder, ref failureCount);
                AssertNotContains(atlas6HotSwap, "TrySyncDisasterEvidenceFromAudioLogRuntime(" + "currentService as IAudioLogRuntime)", "Atlas-6 liability manager does not sync from a raw hot-swap audio-log cast", builder, ref failureCount);
                AssertNotContains(atlas6Sync, "GlobalRegistry.AudioLogRuntime", "Atlas-6 liability evidence sync does not poll the audio-log registry directly", builder, ref failureCount);
                AssertNotContains(atlas6Sync, "_audioLogs", "Atlas-6 liability evidence sync never reads the raw cached audio-log runtime directly", builder, ref failureCount);
            }

            if (narrativeProgressionBridge.Length > 0)
            {
                string progressionOnEnable = ExtractMethodBody(narrativeProgressionBridge, "private void OnEnable()");
                string progressionOnDisable = ExtractMethodBody(narrativeProgressionBridge, "private void OnDisable()");
                string progressionOnDestroy = ExtractMethodBody(narrativeProgressionBridge, "private void OnDestroy()");
                string progressionHotSwap = ExtractMethodBody(narrativeProgressionBridge, "public void OnGlobalRegistryServiceReplaced(");
                string progressionClearCache = ExtractMethodBody(narrativeProgressionBridge, "private void ClearCachedRuntimeServices()");
                string progressionAudioLogCache = ExtractMethodBody(narrativeProgressionBridge, "private void CacheAudioLogSystem(");
                string progressionAudioLogResolve = ExtractMethodBody(narrativeProgressionBridge, "private AudioLogSystem ResolveAudioLogSystem()");
                string progressionAudioLogUsable = ExtractMethodBody(narrativeProgressionBridge, "private static bool IsAudioLogSystemUsable(");
                string progressionBreach = ExtractMethodBody(narrativeProgressionBridge, "public void OnBaseIntegrityEvent(");
                AssertContains(progressionOnEnable, "CacheAudioLogSystem(GlobalRegistry.AudioLogs)", "Narrative progression bridge cold-caches audio-log runtime through the usable-runtime filter", builder, ref failureCount);
                AssertContains(progressionOnEnable, "TryRegisterHotSwapListener()", "Narrative progression bridge subscribes to audio-log hot swaps", builder, ref failureCount);
                AssertContains(progressionOnDisable, "TryUnregisterHotSwapListener()", "Narrative progression bridge unregisters hot-swap listener on disable", builder, ref failureCount);
                AssertContains(progressionOnDisable, "ClearCachedRuntimeServices();", "Narrative progression bridge clears cached runtime services on disable", builder, ref failureCount);
                AssertContains(progressionOnDestroy, "TryUnregisterHotSwapListener()", "Narrative progression bridge unregisters hot-swap listener on destroy", builder, ref failureCount);
                AssertContains(progressionOnDestroy, "ClearCachedRuntimeServices();", "Narrative progression bridge clears cached runtime services on destroy", builder, ref failureCount);
                AssertContains(progressionHotSwap, "GlobalRegistryServiceSlot.AudioLogRuntime", "Narrative progression bridge handles audio-log runtime hot swaps", builder, ref failureCount);
                AssertContains(progressionHotSwap, "CacheAudioLogSystem(currentService as AudioLogSystem)", "Narrative progression bridge caches audio-log hot swaps through the usable-runtime filter", builder, ref failureCount);
                AssertContains(progressionClearCache, "_audioLogs = null", "Narrative progression bridge clears cached audio-log runtime references", builder, ref failureCount);
                AssertContains(progressionAudioLogCache, "_audioLogs = IsAudioLogSystemUsable(audioLogs) ? audioLogs : null", "Narrative progression bridge stores only usable audio-log runtimes", builder, ref failureCount);
                AssertContains(progressionAudioLogResolve, "if (IsAudioLogSystemUsable(audioLogs))", "Narrative progression bridge resolves cached audio-log runtime only while usable", builder, ref failureCount);
                AssertContains(progressionAudioLogResolve, "_audioLogs = null", "Narrative progression bridge clears stale audio-log runtime references", builder, ref failureCount);
                AssertContains(progressionAudioLogUsable, "return audioLogs != null && audioLogs.isActiveAndEnabled", "Narrative progression bridge rejects destroyed or disabled audio-log runtimes", builder, ref failureCount);
                AssertContains(progressionBreach, "AudioLogSystem audioLogs = ResolveAudioLogSystem()", "Narrative progression hull-failure log resolves only usable audio-log runtimes", builder, ref failureCount);
                AssertNotContains(progressionBreach, "GlobalRegistry.AudioLogs", "Narrative progression hull-failure event does not poll the audio-log registry directly", builder, ref failureCount);
                AssertNotContains(progressionBreach, "_audioLogs", "Narrative progression hull-failure event never reads the raw cached audio-log runtime directly", builder, ref failureCount);
                AssertNotContains(progressionAudioLogResolve, "GlobalRegistry.AudioLogs", "Narrative progression audio-log resolver does not poll the registry directly", builder, ref failureCount);
            }

            if (adaptiveStemMixer.Length > 0)
            {
                string adaptiveRuleWrite = ExtractMethodBody(adaptiveStemMixer, "private bool TryWriteRuleForOwnerRoute(");
                string adaptiveRuleAcquire = ExtractMethodBody(adaptiveStemMixer, "private bool TryAcquireRuleMutationView(");
                string adaptiveTick = ExtractMethodBody(adaptiveStemMixer, "public void Tick(float deltaTime)");
                string adaptiveFrameAcquire = ExtractMethodBody(adaptiveStemMixer, "private bool TryAcquireStemFrameMutationView(");
                string adaptiveEnsureVaultStorage = ExtractMethodBody(adaptiveStemMixer, "private void EnsureVaultStorage()");
                string adaptiveEmergencyProfiles = ExtractMethodBody(adaptiveStemMixer, "private void GenerateEmergencyMockAudioProfiles()");
                AssertContains(adaptiveStemMixer, "AudioStemRulesMutationGuardMask", "Adaptive stem rule buffer has a dedicated mutation guard", builder, ref failureCount);
                AssertContains(adaptiveStemMixer, "AudioStemFrameMutationGuardMask", "Adaptive stem frame buffers share a frame mutation guard", builder, ref failureCount);
                AssertContains(adaptiveStemMixer, "AudioStemRulesMutationGuardMask |", "Adaptive stem frame guard includes the editor rule lane", builder, ref failureCount);
                AssertContains(adaptiveTick, "TryAcquireStemFrameMutationView", "Adaptive stem Tick acquires guarded frame owner views", builder, ref failureCount);
                AssertContains(adaptiveTick, "ReleaseAdaptiveStemMutationGuard(guardVault, AudioStemFrameMutationGuardMask)", "Adaptive stem Tick releases frame mutation guard before telemetry dump", builder, ref failureCount);
                AssertContains(adaptiveFrameAcquire, "TryAcquireMutationGuard(AudioStemFrameMutationGuardMask)", "Adaptive stem frame view uses DataVault mutation guard", builder, ref failureCount);
                AssertContains(adaptiveFrameAcquire, "TryResolveStemOwnerViews(guardVault, out views)", "Adaptive stem frame view resolves owner views after guard acquisition", builder, ref failureCount);
                AssertContains(adaptiveFrameAcquire, "ReleaseAdaptiveStemMutationGuard(guardVault, AudioStemFrameMutationGuardMask)", "Adaptive stem frame view releases guard on failed resolve", builder, ref failureCount);
                AssertContains(adaptiveEnsureVaultStorage, "TryAcquireStemFrameMutationView", "Adaptive stem cold memclear acquires guarded frame owner views", builder, ref failureCount);
                AssertContains(adaptiveEmergencyProfiles, "TryAcquireStemFrameMutationView", "Adaptive stem emergency mock profile write acquires guarded frame owner views", builder, ref failureCount);
                AssertContains(adaptiveRuleWrite, "TryAcquireRuleMutationView", "Adaptive stem editor rule writes acquire a guarded owner view", builder, ref failureCount);
                AssertContains(adaptiveRuleWrite, "ReleaseAdaptiveStemMutationGuard", "Adaptive stem editor rule writes release mutation guard in finally", builder, ref failureCount);
                AssertContains(adaptiveRuleAcquire, "TryAcquireMutationGuard(AudioStemRulesMutationGuardMask)", "Adaptive stem rule view uses DataVault mutation guard", builder, ref failureCount);
                AssertContains(adaptiveRuleAcquire, "TryResolveHandle(in _rulesHandle", "Adaptive stem rule view resolves the rules handle after guard acquisition", builder, ref failureCount);
                AssertContains(adaptiveStemMixer, "return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);", "Adaptive stem mutation guard uses DataVault active-lock lanes", builder, ref failureCount);
                AssertNotContains(adaptiveStemMixer, "TryAcquireWriteLock", "Adaptive stem mixer avoids direct DataVault write locks", builder, ref failureCount);
                AssertNotContains(adaptiveStemMixer, "ReleaseWriteLock", "Adaptive stem mixer avoids direct DataVault write-lock release calls", builder, ref failureCount);
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

        private static string ExtractMethodBodyAfter(string source, string anchor, string signature)
        {
            int anchorIndex = source.IndexOf(anchor, StringComparison.Ordinal);
            if (anchorIndex < 0)
                return string.Empty;

            return ExtractMethodBody(source.Substring(anchorIndex), signature);
        }

        private static void AssertTextBefore(string source, string first, string second, string message, StringBuilder builder, ref int failureCount)
        {
            int firstIndex = source.IndexOf(first, StringComparison.Ordinal);
            int secondIndex = source.IndexOf(second, StringComparison.Ordinal);
            if (firstIndex >= 0 && secondIndex >= 0 && firstIndex < secondIndex)
            {
                builder.Append("[PASS] ").AppendLine(message);
                return;
            }

            AppendFailure(builder, ref failureCount, message + " :: expected `" + first + "` before `" + second + "`");
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
