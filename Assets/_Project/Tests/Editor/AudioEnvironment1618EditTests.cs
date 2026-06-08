namespace Hecton8.Tests.Editor
{
    using System;
    using System.IO;
    using NUnit.Framework;

    public sealed class AudioEnvironment1618EditTests
    {
        private const string RendererPath = "Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs";
        private const string GameBootstrapperPath = "Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs";
        private const string PrologueAudioPath = "Assets/_Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs";
        private const string DirectorPath = "Assets/_Project/Scripts/Narrative/Prologue/AwaitableDropSequenceDirector.cs";
        private const string MainMenuScenePath = "Assets/_Project/Scenes/01_MAIN_MENU.unity";
        private const string WorldScenePath = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";
        private const string MasterMixerPath = "Assets/_Project/MasterMixer.mixer";
        private const string PrologueSignalWarmupPath = "Assets/_Project/Scripts/Core/Signals/PrologueReentrySignals.cs";
        private const string MusicDirectorPath = "Assets/_Project/Scripts/Audio/HectonMusicDirector.cs";
        private const string MusicDirectorAnchorPath = "Assets/_Project/Scripts/Audio/HectonMusicDirectorAnchor.cs";
        private const string MusicDirectorConfigPath = "Assets/_Project/Data/Audio/Music/Configs/MusicDirectorConfig_Global.asset";
        private const string MusicDirectorPrefabPath = "Assets/_Project/Prefabs/Audio/PFB_HectonMusicDirectorRoot.prefab";
        private const string DynamicMusicSignalPath = "Assets/_Project/Scripts/Core/Contracts/Signals/DynamicMusicScalarSignal.cs";
        private const string SignalBusRuntimePath = "Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs";
        private const string DynamicMusicSynthPath = "Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs";
        private const string SystemsDebugUiPath = "Assets/_Project/Scripts/UI/HectonSystemsDebugUI.cs";
        private const string SettingsManagerPath = "Assets/_Project/Scripts/UI/SettingsManager.cs";
        private const string ObjectPoolManagerPath = "Assets/_Project/Scripts/ObjectPoolManager.cs";
        private const string AudioLogPickupPath = "Assets/_Project/Scripts/AudioLog/AudioLogPickup.cs";
        private const string AudioLogSystemPath = "Assets/_Project/Scripts/AudioLog/AudioLogSystem.cs";
        private const string FirstHourDirectorPath = "Assets/_Project/Scripts/Gameplay/FirstHourDirector.cs";
        private const string EmergencyServiceRelayPath = "Assets/_Project/Scripts/World/EmergencyServiceRelay.cs";
        private const string NarrativeDiscoveryPath = "Assets/_Project/Scripts/NarrativeDiscovery.cs";
        private const string HectonPlayerHealthPath = "Assets/_Project/Scripts/Gameplay/HectonPlayerHealth.cs";
        private const string HectonPlayerMovementPath = "Assets/_Project/Scripts/HectonPlayerMovement.cs";
        private const string SignalBeaconPath = "Assets/_Project/Scripts/AtlasSignal/SignalBeacon.cs";
        private const string AtlasSignalSystemPath = "Assets/_Project/Scripts/AtlasSignal/AtlasSignalSystem.cs";
        private const string Atlas6CorporateLiabilityManagerPath = "Assets/_Project/Scripts/Gameplay/Atlas6Liability/Atlas6CorporateLiabilityManager.cs";
        private const string SubmarineAtmosphereSystemPath = "Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs";
        private const string SceneRuntimeServicePath = "Assets/_Project/Scripts/Core/SceneRuntimeService.cs";
        private const string HectonNarrativeDirectorPath = "Assets/_Project/Scripts/HectonNarrativeDirector.cs";
        private const string TraumaDispatcherPath = "Assets/_Project/Scripts/Gameplay/TraumaDispatcher.cs";
        private const string RandomEventSystemPath = "Assets/_Project/Scripts/Gameplay/RandomEventSystem.cs";
        private const string EclipseGameplaySystemPath = "Assets/_Project/Scripts/Gameplay/EclipseGameplaySystem.cs";
        private const string ProceduralLoreDirectorPath = "Assets/_Project/Scripts/Narrative/ProceduralLoreDirector.cs";
        private const string PdaDataLogTabPath = "Assets/_Project/Scripts/UI/PDADataLogTab.cs";
        private const string NarrativeProgressionBridgePath = "Assets/_Project/Scripts/Progression/NarrativeProgressionBridge.cs";
        private const string VocalBankRuntimePath = "Assets/_Project/Scripts/Audio/Synthesis/VocalBankPlaybackRuntime.cs";
        private const string VocalWarningSystemPath = "Assets/_Project/Scripts/Audio/VocalWarningSystem.cs";
        private const string SoundscapeSystemPath = "Assets/_Project/Scripts/World/SoundscapeSystem.cs";
        private const string DestructibleOrganicManagerPath = "Assets/_Project/Scripts/World/DestructibleOrganicManager.cs";
        private const string AcousticZoneControllerPath = "Assets/_Project/Scripts/AcousticZoneController.cs";
        private const string DeepPsychosisControllerPath = "Assets/_Project/Scripts/Audio/DeepPsychosisController.cs";
        private const string PlayerFootstepAudioPath = "Assets/_Project/Scripts/PlayerFootstepAudio.cs";
        private const string PlayerStressVfxPath = "Assets/_Project/Scripts/Visor/PlayerStressVFX.cs";
        private const string SpectrumSystemPath = "Assets/_Project/Scripts/Visor/SpectrumSystem.cs";
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
        private const string SuitHudOverlayPath = "Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs";
        private const string AcousticRadarSphereRendererPath = "Assets/_Project/Scripts/UI/AcousticRadarSphereRenderer.cs";
        private const string SonarHoloCompassPath = "Assets/_Project/Scripts/UI/SonarHoloCompass.cs";
        private const string SubmarineAutoLevelBallastControllerPath = "Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs";
        private const string PlayerFlashlightPath = "Assets/_Project/Scripts/PlayerFlashlight.cs";
        private const string PlayerPdaPath = "Assets/_Project/Scripts/PlayerPDA.cs";
        private const string PlayerInventoryPath = "Assets/_Project/Scripts/PlayerInventory.cs";
        private const string PDAInventoryTabPath = "Assets/_Project/Scripts/PDAInventoryTab.cs";
        private const string PDAMapTabPath = "Assets/_Project/Scripts/UI/PDAMapTab.cs";
        private const string AdaptiveStemMixerPath = "Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs";
        private const string ModCommandDispatcherPath = "Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs";

        [Test]
        public void GameBootstrapperAudioFallbackRequiresUsableRegisteredAudioService()
        {
            string bootstrap = Read(GameBootstrapperPath);
            string heartbeat = ExtractMethodBody(bootstrap, "private static bool IsBootstrapDependencyHeartbeatReady(");
            string nodeReady = ExtractMethodBody(bootstrap, "private static bool IsBootstrapDependencyNodeReady(BootstrapDependencyNode node, object service)");
            string initialize = ExtractMethodBody(bootstrap, "private static bool InitializeSpatialAudioBootstrapNode()");
            string fallback = ExtractMethodBody(bootstrap, "private static bool TryRegisterNoOpAudioFallback(");
            string usable = ExtractMethodBody(bootstrap, "private static bool IsBootstrapAudioServiceUsable(");

            StringAssert.Contains("if (node == BootstrapDependencyNode.SpatialAudioManager)", heartbeat);
            StringAssert.Contains("return _headlessBootMode || IsBootstrapAudioServiceUsable(service as IAudioService)", heartbeat);
            Assert.That(
                heartbeat.IndexOf("if (node == BootstrapDependencyNode.SpatialAudioManager)", StringComparison.Ordinal),
                Is.LessThan(heartbeat.IndexOf("if (service is IServiceHeartbeat heartbeat)", StringComparison.Ordinal)));
            StringAssert.Contains("case BootstrapDependencyNode.SpatialAudioManager:", nodeReady);
            StringAssert.Contains("return _headlessBootMode || IsBootstrapAudioServiceUsable(service as IAudioService)", nodeReady);
            StringAssert.Contains("IsBootstrapAudioServiceUsable(GlobalRegistry.Audio)", initialize);
            Assert.That(initialize.IndexOf("GlobalRegistry.Audio != null", StringComparison.Ordinal), Is.LessThan(0));
            StringAssert.Contains("return IsBootstrapAudioServiceUsable(audioService)", fallback);
            Assert.That(fallback.IndexOf("return audioService != null", StringComparison.Ordinal), Is.LessThan(0));
            StringAssert.Contains("audioService == null || !audioService.IsInitialized", usable);
            StringAssert.Contains("audioService is Behaviour behaviour", usable);
            StringAssert.Contains("return behaviour != null && behaviour.isActiveAndEnabled", usable);
        }

        [Test]
        public void ReentryAudioCutoffsMatchVacuumPlasmaSplashdownContract()
        {
            string renderer = Read(RendererPath);
            string prologueAudio = Read(PrologueAudioPath);
            string director = Read(DirectorPath);

            StringAssert.Contains("PrologueClosedLowPassHertz = 150f", renderer);
            StringAssert.Contains("vacuumLowPassCutoffHertz = 150f", prologueAudio);
            StringAssert.Contains("SplashdownLowPassCutoffHertz = 350f", prologueAudio);
            StringAssert.Contains("AcousticVacuumLowPassCutoffHertz = 150f", director);
            StringAssert.Contains("AcousticPlasmaLowPassCutoffHertz = 20000f", director);
            StringAssert.Contains("AcousticSplashdownLowPassCutoffHertz = 350f", director);
            StringAssert.Contains("signal.LowPassCutoffHz = AcousticSplashdownLowPassCutoffHertz", director);
        }

        [Test]
        public void PlasmaRendererUsesDedicatedPinkNoiseBiquadState()
        {
            string renderer = Read(RendererPath);
            string mix = ExtractMethodBody(renderer, "private void MixAndFilterBlock(");
            string snapshot = ExtractMethodBody(renderer, "private struct AudioParameterSnapshot");

            StringAssert.Contains("private struct ProloguePlasmaSynthesisState", renderer);
            StringAssert.Contains("[StructLayout(LayoutKind.Explicit, Size = 256)]", renderer);
            StringAssert.Contains("[FieldOffset(220)]", snapshot);
            StringAssert.Contains("public float GlobalQualityWeight", snapshot);
            StringAssert.Contains("GlobalQualityWeight = SanitizeQuality01(_cachedAudioQualityWeight01)", renderer);
            StringAssert.Contains("ApplyPaulKelletPink(ref ProloguePlasmaSynthesisState", renderer);
            StringAssert.Contains("RenderProloguePlasmaSample(", mix);
            StringAssert.Contains("prologuePlasmaQuality = SmoothQuality01(parameters.GlobalQualityWeight)", mix);
            StringAssert.Contains("if (blockProloguePlasmaDrive > HullNoiseFloor)", mix);
            StringAssert.Contains("ComputeBandPassCoefficients(", mix);
            StringAssert.Contains("ProloguePlasmaBandPassQ", renderer);
            string plasma = ExtractMethodBody(renderer, "private static float RenderProloguePlasmaSample(");
            StringAssert.Contains("ProcessBiquad(", plasma);
            StringAssert.Contains("AdvanceTriangle01(ref state.LfoPhase", plasma);
            StringAssert.Contains("ProloguePlasmaMinimumQualityGain", plasma);
        }

        [Test]
        public void PrologueDspHotMethodsContainNoManagedHazardTokens()
        {
            string renderer = Read(RendererPath);
            AssertNoForbiddenHotTokens(ExtractMethodBody(renderer, "private static float RenderProloguePlasmaSample("));
            AssertNoForbiddenHotTokens(ExtractMethodBody(renderer, "private float RenderPrologueSplashdownSample("));
            AssertNoForbiddenHotTokens(ExtractMethodBody(renderer, "private static void ResetProloguePlasmaState("));
            AssertNoForbiddenHotTokens(ExtractMethodBody(renderer, "private static float AdvanceTriangle01("));

            StringAssert.Contains("ResetProloguePlasmaState(ref state)", renderer);
            StringAssert.Contains("return math.isfinite(sample) ? sample : 0f", renderer);
        }

        [Test]
        public void PrologueHapticsRouteThroughCanonicalRequestSignal()
        {
            string renderer = Read(RendererPath);
            string prologueAudio = Read(PrologueAudioPath);
            string haptics = ExtractMethodBody(prologueAudio, "private void PublishSynchronizedHaptics(");

            StringAssert.Contains("SignalBus<HapticRequest>.TryPushTracked(in request", haptics);
            StringAssert.Contains("SignalBus<HapticRequest>.TryPushTracked(in plasma", haptics);
            StringAssert.Contains("HapticRequest.FlagCrush", haptics);
            StringAssert.Contains("HapticRequest.FlagMicroVibration", haptics);
            StringAssert.Contains("unchecked(state.Frame - _lastPlasmaHapticFrame)", haptics);
            StringAssert.Contains("_lastPlasmaHapticFrame = state.Frame", haptics);
            Assert.That(renderer.IndexOf("SignalBus<HapticRequest>", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void PrologueSignalWarmupCoversLateFrameLanes()
        {
            string warmup = ExtractMethodBody(Read(PrologueSignalWarmupPath), "public static void Warm()");

            StringAssert.Contains("SignalCorridorRuntime.EnsureInitialized()", warmup);
            StringAssert.Contains("SignalBus<AtmosphericReentrySignal>.EnsureInitialized()", warmup);
            StringAssert.Contains("SignalBus<ReentryAcousticStressSignal>.EnsureInitialized()", warmup);
            StringAssert.Contains("SignalBus<PrologueCompleteSignal>.EnsureInitialized()", warmup);
            StringAssert.Contains("SignalBus<HapticRequest>.EnsureInitialized()", warmup);
        }

        [Test]
        public void PrologueTransitionQueueUsesBoundedRingAndNoManagedQueue()
        {
            string renderer = Read(RendererPath);
            string enqueue = ExtractMethodBody(renderer, "public bool QueuePrologueAudioTransition(");
            string dequeue = ExtractMethodBody(renderer, "private bool TryDequeuePrologueTransitionState(");

            StringAssert.Contains("PrologueTransitionQueueCapacity = 32", renderer);
            StringAssert.Contains("TryWriteRing(", enqueue);
            StringAssert.Contains("TryReadRing(", dequeue);
            Assert.That(enqueue.IndexOf("Queue<", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(enqueue.IndexOf("ConcurrentQueue", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(enqueue.IndexOf("lock", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(dequeue.IndexOf("Queue<", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(dequeue.IndexOf("ConcurrentQueue", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(dequeue.IndexOf("lock", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void HighFrequencyLoopsUseColdCachedDependencies()
        {
            string renderer = Read(RendererPath);
            string prologueAudio = Read(PrologueAudioPath);
            string director = Read(DirectorPath);
            string spatial = Read("Assets/_Project/Scripts/SpatialAudioManager.cs");
            string musicDirector = Read(MusicDirectorPath);

            AssertNoHotDependencyLookups(ExtractMethodBody(renderer, "public void Tick(float deltaTime)"));
            AssertNoHotDependencyLookups(ExtractMethodBody(renderer, "public void LateFrameTick()"));
            AssertNoHotDependencyLookups(ExtractMethodBody(prologueAudio, "public void LateFrameTick()"));
            AssertNoHotDependencyLookups(ExtractMethodBody(director, "public void Tick(float deltaTime)"));
            AssertNoHotDependencyLookups(ExtractMethodBody(spatial, "public void Tick(float deltaTime)"));
            AssertNoHotDependencyLookups(ExtractMethodBody(spatial, "public void FastTick(float deltaTime)"));
            AssertNoHotDependencyLookups(ExtractMethodBody(spatial, "public void SlowTick()"));
            AssertNoHotDependencyLookups(ExtractMethodBody(spatial, "public void LateFrameTick()"));
            AssertNoHotDependencyLookups(ExtractMethodBody(musicDirector, "public void Tick(float deltaTime)"));
            AssertNoHotDependencyLookups(ExtractMethodBody(musicDirector, "public void LateFrameTick()"));
            AssertNoHotDependencyLookups(ExtractMethodBody(musicDirector, "private void RunMusicSlowTick()"));

            string prologueCold = ExtractMethodBody(prologueAudio, "private void RefreshRuntimeServicesCold()");
            StringAssert.Contains("GlobalRegistry.Audio", prologueCold);
            StringAssert.Contains("GlobalRegistry.TickDispatcher", prologueCold);
        }

        [Test]
        public void PlayerCriticalRuntimeContextBindingUsesColdCachedContextOnly()
        {
            string renderer = Read(RendererPath);
            string tick = ExtractMethodBody(renderer, "public void Tick(float deltaTime)");
            string slowTick = ExtractMethodBody(renderer, "public void SlowTick()");
            string binder = ExtractMethodBody(renderer, "private void TryBindFromCachedRuntimeContext()");
            string cold = ExtractMethodBody(renderer, "private void CacheColdRegistryReferences()");
            string rebound = ExtractMethodBody(renderer, "private void CacheRegistryServiceReference(");
            string audioCache = ExtractMethodBody(renderer, "private void CacheAudioRuntimeService(");
            string caveResolve = ExtractMethodBody(renderer, "private ISpatialAudioListenerCaveReadModel ResolveSpatialAudioListenerCaveReadModel()");
            string binauralResolve = ExtractMethodBody(renderer, "private ISpatialAudioBinauralEmitterReadModel ResolveSpatialAudioBinauralEmitterReadModel()");
            string audioUsable = ExtractMethodBody(renderer, "private static bool IsAudioServiceUsable(");
            string runtimeObjectUsable = ExtractMethodBody(renderer, "private static bool IsAudioRuntimeObjectUsable(");

            StringAssert.Contains("TryBindFromCachedRuntimeContext();", tick);
            StringAssert.Contains("TryBindFromCachedRuntimeContext();", slowTick);
            StringAssert.Contains("IPlayerRuntimeContext playerContext = _playerRuntimeContext", binder);
            StringAssert.Contains("BindToPlayerRuntimeContext(playerContext)", binder);
            AssertNoHotDependencyLookups(binder);
            Assert.That(binder.IndexOf("PlayerRuntimeContextService", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(renderer.IndexOf("PlayerRuntimeContextService.ActiveRuntimeContext", StringComparison.Ordinal), Is.LessThan(0));
            StringAssert.Contains("_playerRuntimeContext = CacheReadyPlayerRuntime(GlobalRegistry.Player)", cold);
            StringAssert.Contains("_playerRuntimeContext = CacheReadyPlayerRuntime(currentService as IPlayerRuntimeContext)", rebound);
            StringAssert.Contains("CacheAudioRuntimeService(GlobalRegistry.Audio", cold);
            StringAssert.Contains("CacheAudioRuntimeService(currentService as IAudioService", rebound);
            StringAssert.Contains("bool isUsable = IsAudioServiceUsable(audioService)", audioCache);
            StringAssert.Contains("_spatialAudioListenerCaveReadModel = isUsable ? audioService as ISpatialAudioListenerCaveReadModel : null", audioCache);
            StringAssert.Contains("_spatialAudioBinauralEmitterReadModel = isUsable ? audioService as ISpatialAudioBinauralEmitterReadModel : null", audioCache);
            StringAssert.Contains("if (IsAudioRuntimeObjectUsable(readModel))", caveResolve);
            StringAssert.Contains("_spatialAudioListenerCaveReadModel = null", caveResolve);
            StringAssert.Contains("if (IsAudioRuntimeObjectUsable(readModel))", binauralResolve);
            StringAssert.Contains("_spatialAudioBinauralEmitterReadModel = null", binauralResolve);
            StringAssert.Contains("audioService == null || !audioService.IsInitialized", audioUsable);
            StringAssert.Contains("return IsAudioRuntimeObjectUsable(audioService)", audioUsable);
            AssertAudioRuntimeObjectUsableBody(runtimeObjectUsable);
        }

        [Test]
        public void PlayerCriticalAudioProducerStartFailureClearsStickyRunningState()
        {
            string renderer = Read(RendererPath);
            string startProducer = ExtractMethodBody(renderer, "private void StartAudioProducerThread()");

            StringAssert.Contains("Thread nextProducerThread = new Thread(AudioProducerLoop)", startProducer);
            StringAssert.Contains("_audioProducerThread = nextProducerThread;", startProducer);
            StringAssert.Contains("nextProducerThread.Start();", startProducer);
            StringAssert.Contains("catch (Exception)", startProducer);
            StringAssert.Contains("Interlocked.Exchange(ref _audioProducerRestartRequested, 0);", startProducer);
            StringAssert.Contains("Interlocked.Exchange(ref _audioProducerRunning, 0);", startProducer);
            StringAssert.Contains("_audioProducerThread = null;", startProducer);
            StringAssert.Contains("return;", startProducer);
            AssertTextBefore(startProducer, "nextProducerThread.Start();", "SignalAudioProducerThread();");
        }

        [Test]
        public void PlayerCriticalAudioProducerLifecycleUsesNoThrowJoinHelper()
        {
            string renderer = Read(RendererPath);
            string startProducer = ExtractMethodBody(renderer, "private void StartAudioProducerThread()");
            string stopProducer = ExtractMethodBody(renderer, "private bool StopAudioProducerThread()");
            string joinHelper = ExtractMethodBody(renderer, "private static bool TryJoinAudioProducerThreadNoThrow(");

            StringAssert.Contains("TryJoinAudioProducerThreadNoThrow(producerThread, 0)", startProducer);
            StringAssert.Contains("TryJoinAudioProducerThreadNoThrow(producerThread, AudioProducerJoinTimeoutMs)", stopProducer);
            Assert.That(CountOccurrences(startProducer, "producerThread.Join(0)"), Is.EqualTo(0));
            Assert.That(CountOccurrences(stopProducer, "producerThread.Join(AudioProducerJoinTimeoutMs)"), Is.EqualTo(0));
            StringAssert.Contains("ReferenceEquals(Thread.CurrentThread, producerThread)", joinHelper);
            StringAssert.Contains("producerThread.Join(timeoutMilliseconds);", joinHelper);
            StringAssert.Contains("return !producerThread.IsAlive;", joinHelper);
            StringAssert.Contains("catch (Exception)", joinHelper);
        }

        [Test]
        public void DeferredMusicContextUsesColdCachedDependenciesOnly()
        {
            string musicDirector = Read(MusicDirectorPath);
            string lateFrame = ExtractMethodBody(musicDirector, "public void LateFrameTick()");
            string slow = ExtractMethodBody(musicDirector, "private void RunMusicSlowTick()");
            string refresh = ExtractMethodBody(musicDirector, "private void RefreshPolledMusicContext()");
            string resolveDependencies = ExtractMethodBody(musicDirector, "private void ResolveDependencies()");
            string coldRefresh = ExtractMethodBody(musicDirector, "private void RefreshCachedRuntimeServicesCold()");

            AssertTextBefore(lateFrame, "RunMusicSlowTick();", "RunMusicTick(deltaTime);");
            AssertNoHotDependencyLookups(lateFrame);
            AssertNoHotDependencyLookups(slow);
            AssertNoHotDependencyLookups(refresh);
            AssertNoHotDependencyLookups(resolveDependencies);
            Assert.That(slow.IndexOf("RefreshCachedRuntimeServicesCold", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(refresh.IndexOf("RefreshCachedRuntimeServicesCold", StringComparison.Ordinal), Is.LessThan(0));
            StringAssert.Contains("ResolvePlayerRuntimeContext()", resolveDependencies);
            StringAssert.Contains("ResolveDepthZoneReadModel()", resolveDependencies);
            StringAssert.Contains("CachePlayerRuntimeContext(GlobalRegistry.Player", coldRefresh);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio", coldRefresh);
        }

        [Test]
        public void DynamicMusicScalarSignalCarriesMusicActivityInStableSanitizedSlot()
        {
            string signal = Read(DynamicMusicSignalPath);
            string signalBusRuntime = Read(SignalBusRuntimePath);

            StringAssert.Contains("SignalStrideBytes = 64", signal);
            StringAssert.Contains("[StructLayout(LayoutKind.Explicit, Size = DynamicMusicScalarSignalLayout.SignalStrideBytes)]", signal);
            StringAssert.Contains("[FieldOffset(32)] public uint SourceHash", signal);
            StringAssert.Contains("[FieldOffset(40)] public float MusicActivity01", signal);
            StringAssert.Contains("SourceMusicDirectorHash", signal);
            StringAssert.Contains("FlagSuppressReactiveImpulses = 1u << 3", signal);
            StringAssert.Contains("SanitizeUnit01(ref signal.MusicActivity01)", signalBusRuntime);
            AssertTextBefore(signal, "public uint SourceHash", "public float MusicActivity01");
        }

        [Test]
        public void MusicDirectorAnchorsExistInMenuAndWorldScenes()
        {
            const string configGuid = "3fe2e07be4fdac24cb6b2f12b438dcc3";
            const string prefabGuid = "7a86aa3ad745a104d84c2f6622d12430";
            const string masterMixerGuid = "69195f25e7aad1b44a0d49cc645ff0f3";
            const string musicGroupReference = "{fileID: 1111111111, guid: " + masterMixerGuid + ", type: 2}";
            string mainMenuScene = Read(MainMenuScenePath);
            string worldScene = Read(WorldScenePath);
            string config = Read(MusicDirectorConfigPath);
            string prefab = Read(MusicDirectorPrefabPath);

            Assert.That(CountOccurrences(mainMenuScene, "Hecton8.Audio.HectonMusicDirectorAnchor"), Is.EqualTo(1));
            Assert.That(CountOccurrences(worldScene, "Hecton8.Audio.HectonMusicDirectorAnchor"), Is.EqualTo(1));
            StringAssert.Contains("_config: {fileID: 11400000, guid: " + configGuid + ", type: 2}", mainMenuScene);
            StringAssert.Contains("_config: {fileID: 11400000, guid: " + configGuid + ", type: 2}", worldScene);
            StringAssert.Contains("_runtimeDirectorPrefab: {fileID: 4511111111111111111, guid: " + prefabGuid + ", type: 3}", config);
            StringAssert.Contains("_musicMixerGroup: " + musicGroupReference, config);
            StringAssert.Contains("_stingerMixerGroup: " + musicGroupReference, config);
            StringAssert.Contains("m_EditorClassIdentifier: Hecton8.Core::Hecton8.Audio.HectonMusicDirector", prefab);
        }

        [Test]
        public void MusicDirectorAnchorFallbackUsesLiveRuntimeResolver()
        {
            string anchor = Read(MusicDirectorAnchorPath);
            string director = Read(MusicDirectorPath);
            string disable = ExtractMethodBody(anchor, "private void OnDisable()");
            string destroy = ExtractMethodBody(anchor, "private void OnDestroy()");
            string instantiate = ExtractMethodBody(director, "private static bool TryInstantiateConfiguredRuntimeDirector(");
            string applySceneConfig = ExtractMethodBody(director, "private void ApplySceneConfigCold(");

            StringAssert.Contains("public static bool TryResolveActiveRuntime(ref HectonMusicDirectorAnchor target)", anchor);
            StringAssert.Contains("active = FindFirstLiveAnchor();", anchor);
            StringAssert.Contains("private static void RefreshActiveRuntimeInstance()", anchor);
            StringAssert.Contains("private static bool IsLiveAnchor(HectonMusicDirectorAnchor anchor)", anchor);
            StringAssert.Contains("RefreshActiveRuntimeInstance();", disable);
            StringAssert.Contains("RefreshActiveRuntimeInstance();", destroy);

            StringAssert.Contains("HectonMusicDirectorAnchor.TryResolveActiveRuntime(ref anchor)", instantiate);
            Assert.That(instantiate.Contains("HectonMusicDirectorAnchor.ActiveRuntimeInstance"), Is.False);
            StringAssert.Contains("HectonMusicDirectorAnchor.TryResolveActiveRuntime(ref anchor)", applySceneConfig);
            Assert.That(applySceneConfig.Contains("HectonMusicDirectorAnchor.ActiveRuntimeInstance"), Is.False);
        }

        [Test]
        public void MusicVolumeSettingsDriveMasterMixerMusicBus()
        {
            const string masterMixerGuid = "69195f25e7aad1b44a0d49cc645ff0f3";
            const string musicGroupGuid = "aaaaaaaa1111111111111111aaaaaaaa";
            const string musicVolumeGuid = "11111111111111111111111111111111";
            const string musicGroupReference = "{fileID: 1111111111, guid: " + masterMixerGuid + ", type: 2}";
            string settingsManager = Read(SettingsManagerPath);
            string mainMenuScene = Read(MainMenuScenePath);
            string masterMixer = Read(MasterMixerPath);
            string config = Read(MusicDirectorConfigPath);
            string musicProperty = ExtractMethodBody(settingsManager, "public float MusicVolume");
            string applyAudio = ExtractMethodBody(settingsManager, "private bool ApplyAudioMixerSettings()");
            string applyMixer = ExtractMethodBody(settingsManager, "private bool ApplyMixerVolume(");

            StringAssert.Contains("name: MusicVolume", masterMixer);
            StringAssert.Contains("m_Name: Music", masterMixer);
            StringAssert.Contains("m_GroupID: " + musicGroupGuid, masterMixer);
            StringAssert.Contains("m_Volume: " + musicVolumeGuid, masterMixer);
            StringAssert.Contains("audioMixer: {fileID: 24100000, guid: " + masterMixerGuid + ", type: 2}", mainMenuScene);
            StringAssert.Contains("ApplyMixerVolume(\"MusicVolume\", clamped)", musicProperty);
            StringAssert.Contains("ApplyMixerVolume(\"MusicVolume\", _cachedMusicVolume)", applyAudio);
            StringAssert.Contains("audioMixer.SetFloat(parameterName, db)", applyMixer);
            StringAssert.Contains("_musicMixerGroup: " + musicGroupReference, config);
            StringAssert.Contains("_stingerMixerGroup: " + musicGroupReference, config);
        }

        [Test]
        public void MusicDirectorRuntimePoolFallbackWarmsReserveBeforeSpawn()
        {
            string musicDirector = Read(MusicDirectorPath);
            string objectPool = Read(ObjectPoolManagerPath);
            string instantiate = ExtractMethodBody(musicDirector, "private static bool TryInstantiateConfiguredRuntimeDirector(");
            string reserve = ExtractMethodBody(musicDirector, "private static void EnsureRuntimeDirectorPoolReserve(");
            string resolvePool = ExtractMethodBody(musicDirector, "private static IObjectPoolService ResolveRuntimeObjectPool()");
            string warmup = ExtractMethodBody(objectPool, "public void Warmup(GameObject prefab, int count)");
            string spawn = ExtractMethodBody(objectPool, "public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, bool allowExpand)");

            StringAssert.Contains("RuntimeDirectorPoolReserveCount = 1", musicDirector);
            StringAssert.Contains("EnsureRuntimeDirectorPoolReserve(pool, runtimeDirectorPrefab);", instantiate);
            AssertTextBefore(instantiate, "EnsureRuntimeDirectorPoolReserve(pool, runtimeDirectorPrefab);", "pool.Spawn(runtimeDirectorPrefab");
            StringAssert.Contains("pool.Warmup(runtimeDirectorPrefab, RuntimeDirectorPoolReserveCount);", reserve);
            StringAssert.Contains("ObjectPoolManager.TryResolveActiveRuntime(ref pool)", resolvePool);
            StringAssert.Contains("Pool pool = PreparePool(prefab, registry);", warmup);
            StringAssert.Contains("if (!_pools.TryGetValue(prefabId, out Pool pool))", spawn);
            StringAssert.Contains("return null;", spawn);
        }

        [Test]
        public void ProceduralMusicActivityUsesPhrasesAndYieldsToEmergencyBreath()
        {
            string musicDirector = Read(MusicDirectorPath);
            string update = ExtractMethodBody(musicDirector, "private void UpdateProceduralMusicActivity(");
            string forceOpen = ExtractMethodBody(musicDirector, "private bool ShouldForceProceduralMusicOpen()");
            string tickRoute = ExtractMethodBody(musicDirector, "private void RunMusicTick(");
            string wait = ExtractMethodBody(musicDirector, "private void BeginProceduralWait(");
            string phrase = ExtractMethodBody(musicDirector, "private float ResolveProceduralPhraseSeconds(");
            string target = ExtractMethodBody(musicDirector, "private float ResolveProceduralMusicActivityTarget01()");
            string publish = ExtractMethodBody(musicDirector, "private void PublishDynamicMusicScalars(");
            string stopPublish = ExtractMethodBody(musicDirector, "private void PublishProceduralMusicStopSignal()");
            string stinger = ExtractMethodBody(musicDirector, "private void InjectProceduralStinger(");
            string stingerFlush = ExtractMethodBody(musicDirector, "private void FlushPendingStingers()");
            string discoveryStinger = ExtractMethodBody(musicDirector, "public void PlayDiscoveryStinger()");
            string dangerStinger = ExtractMethodBody(musicDirector, "public void PlayDangerStinger()");
            string recoveryStinger = ExtractMethodBody(musicDirector, "public void PlayRecoveryStinger()");
            string overrideStart = ExtractMethodBody(musicDirector, "private void ForceOverrideTrackInternal(");
            string stop = ExtractMethodBody(musicDirector, "private void StopMusicInternal(");
            string stressRefresh = ExtractMethodBody(musicDirector, "private void RefreshPlayerCriticalStressSignal()");
            string emergencyDominance = ExtractMethodBody(musicDirector, "private float ResolveEmergencyAudioDominance01()");
            string emergencyGate = ExtractMethodBody(musicDirector, "private bool IsEmergencyBreathDominant()");
            string warningDuck = ExtractMethodBody(musicDirector, "private void RefreshVocalWarningMusicDucking()");
            string warningDuckResolve = ExtractMethodBody(musicDirector, "private static float ResolveVocalWarningMusicDuck01(");
            string speechDuck = ExtractMethodBody(musicDirector, "private void RefreshForegroundSpeechMusicDucking()");
            string speechApply = ExtractMethodBody(musicDirector, "private float ApplyForegroundSpeechMusicDuck01(");
            string speechActive = ExtractMethodBody(musicDirector, "private bool IsForegroundSpeechActive()");
            string audioLogDuck = ExtractMethodBody(musicDirector, "private void RefreshNarrativeAudioLogMusicDucking()");
            string speechDuckResolve = ExtractMethodBody(musicDirector, "private float ResolveForegroundSpeechMusicDuck01()");
            string vocalWarningResolve = ExtractMethodBody(musicDirector, "private IVocalWarningSystem ResolveVocalWarningSystem()");
            string vocalWarningCache = ExtractMethodBody(musicDirector, "private void CacheVocalWarningSystem(");
            string vocalWarningStale = ExtractMethodBody(musicDirector, "private void RefreshVocalWarningRuntimeIfStale()");
            string vocalWarningUsable = ExtractMethodBody(musicDirector, "private static bool IsVocalWarningRuntimeUsable(");
            string audioLogResolve = ExtractMethodBody(musicDirector, "private IAudioLogRuntime ResolveAudioLogRuntime()");
            string audioLogCache = ExtractMethodBody(musicDirector, "private void CacheAudioLogRuntime(");
            string audioLogStale = ExtractMethodBody(musicDirector, "private void RefreshAudioLogRuntimeIfStale()");
            string audioLogUsable = ExtractMethodBody(musicDirector, "private static bool IsAudioLogRuntimeUsable(");
            string runtimeRebind = ExtractMethodBody(musicDirector, "private void CacheReboundRuntimeService(");

            StringAssert.Contains("public enum MusicActivityReason", musicDirector);
            StringAssert.Contains("CurrentMusicActivity01 => math.saturate(_proceduralMusicActivity01)", musicDirector);
            StringAssert.Contains("CurrentMusicActivityReason => _musicActivityReason", musicDirector);
            StringAssert.Contains("public SoundscapeTier CurrentSoundscapeTier", musicDirector);
            StringAssert.Contains("public float CurrentSoundscapePressure01", musicDirector);
            StringAssert.Contains("public void SetSoundscapeTierContext(SoundscapeTier tier, float depthMeters)", musicDirector);
            StringAssert.Contains("_soundscapePressureWeight", musicDirector);
            StringAssert.Contains("_soundscapeDepthHintMeters", musicDirector);
            StringAssert.Contains("CriticalPlayerStressDominatesThreshold = 0.88f", musicDirector);
            StringAssert.Contains("PlayerStressSignalHoldFrames = 8", musicDirector);
            StringAssert.Contains("VocalWarningMusicDuckDefault01 = 0.38f", musicDirector);
            StringAssert.Contains("VocalWarningMusicDuckCritical01 = 0.62f", musicDirector);
            StringAssert.Contains("NarrativeAudioLogMusicDuck01 = 0.48f", musicDirector);
            StringAssert.Contains("_lastForegroundSpeechDuckingRefreshFrame = -1", musicDirector);
            StringAssert.Contains("RefreshPlayerCriticalStressSignal();", musicDirector);
            StringAssert.Contains("SignalBus<PlayerStressSignal>.TryGetLatest", stressRefresh);
            StringAssert.Contains("_lastPlayerStressSignalSeenFrame == int.MinValue", stressRefresh);
            StringAssert.Contains("frame - _lastPlayerStressSignalSeenFrame > PlayerStressSignalHoldFrames", stressRefresh);
            StringAssert.Contains("_playerCriticalStress01 = math.saturate(signal.Stress01)", stressRefresh);
            StringAssert.Contains("_playerCriticalStress01 = 0f", stressRefresh);
            StringAssert.Contains("math.max(_oxygenDanger01, _playerCriticalStress01)", emergencyDominance);
            StringAssert.Contains("_playerCriticalStress01 >= CriticalPlayerStressDominatesThreshold", emergencyGate);
            StringAssert.Contains("GlobalRegistryServiceSlot.VocalWarningRuntime", runtimeRebind);
            StringAssert.Contains("GlobalRegistryServiceSlot.AudioLogRuntime", runtimeRebind);
            StringAssert.Contains("RefreshForegroundSpeechMusicDucking();", tickRoute);
            StringAssert.Contains("IVocalWarningSystem vocalWarningSystem = ResolveVocalWarningSystem()", warningDuck);
            StringAssert.Contains("vocalWarningSystem.IsWarningActive", warningDuck);
            StringAssert.Contains("ResolveVocalWarningMusicDuck01(warningId)", warningDuck);
            StringAssert.Contains("RefreshVocalWarningMusicDucking();", speechDuck);
            StringAssert.Contains("RefreshNarrativeAudioLogMusicDucking();", speechDuck);
            StringAssert.Contains("int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex", speechDuck);
            StringAssert.Contains("if (_lastForegroundSpeechDuckingRefreshFrame == frame)", speechDuck);
            StringAssert.Contains("_lastForegroundSpeechDuckingRefreshFrame = frame", speechDuck);
            StringAssert.Contains("safeActivity01 * (1f - duck01)", speechApply);
            StringAssert.Contains("ResolveForegroundSpeechMusicDuck01() > 0.001f", speechActive);
            StringAssert.Contains("if (IsVocalWarningRuntimeUsable(vocalWarningSystem))", vocalWarningResolve);
            StringAssert.Contains("_cachedVocalWarningSystem = null", vocalWarningResolve);
            StringAssert.Contains("_cachedVocalWarningSystem = IsVocalWarningRuntimeUsable(vocalWarningSystem) ? vocalWarningSystem : null", vocalWarningCache);
            StringAssert.Contains("if (IsVocalWarningRuntimeUsable(vocalWarningSystem))", vocalWarningStale);
            StringAssert.Contains("_cachedVocalWarningSystem = null", vocalWarningStale);
            StringAssert.Contains("vocalWarningSystem == null || !vocalWarningSystem.IsInitialized", vocalWarningUsable);
            StringAssert.Contains("vocalWarningSystem is Behaviour behaviour", vocalWarningUsable);
            StringAssert.Contains("return behaviour != null && behaviour.isActiveAndEnabled", vocalWarningUsable);
            StringAssert.Contains("IAudioLogRuntime audioLogRuntime = ResolveAudioLogRuntime()", audioLogDuck);
            StringAssert.Contains("audioLogRuntime.IsPlaying || audioLogRuntime.IsNarrativeQueueBlocked", audioLogDuck);
            StringAssert.Contains("NarrativeAudioLogMusicDuck01", audioLogDuck);
            StringAssert.Contains("math.max(_vocalWarningMusicDuck01, _narrativeAudioLogMusicDuck01)", speechDuckResolve);
            StringAssert.Contains("if (IsAudioLogRuntimeUsable(audioLogRuntime))", audioLogResolve);
            StringAssert.Contains("_cachedAudioLogRuntime = null", audioLogResolve);
            StringAssert.Contains("_cachedAudioLogRuntime = IsAudioLogRuntimeUsable(audioLogRuntime) ? audioLogRuntime : null", audioLogCache);
            StringAssert.Contains("if (IsAudioLogRuntimeUsable(audioLogRuntime))", audioLogStale);
            StringAssert.Contains("_cachedAudioLogRuntime = null", audioLogStale);
            StringAssert.Contains("audioLogRuntime is Behaviour behaviour", audioLogUsable);
            StringAssert.Contains("return behaviour != null && behaviour.isActiveAndEnabled", audioLogUsable);
            Assert.That(warningDuck.IndexOf("GlobalRegistry.", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(audioLogDuck.IndexOf("GlobalRegistry.", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(speechDuck.IndexOf("RefreshVocalWarningRuntimeIfStale", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(speechDuck.IndexOf("RefreshAudioLogRuntimeIfStale", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(speechActive.IndexOf("RefreshForegroundSpeechMusicDucking", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(speechActive.IndexOf("GlobalRegistry.", StringComparison.Ordinal), Is.LessThan(0));
            StringAssert.Contains("VocalWarningId.CrushDepth", warningDuckResolve);
            StringAssert.Contains("VocalWarningId.HullBreach", warningDuckResolve);
            StringAssert.Contains("VocalWarningId.OxygenLow", warningDuckResolve);
            StringAssert.Contains("ShouldForceProceduralMusicOpen()", update);
            StringAssert.Contains("StartProceduralPhrase(true);", update);
            StringAssert.Contains("BeginProceduralWait(", update);
            StringAssert.Contains("StartProceduralPhrase(false)", update);
            StringAssert.Contains("_proceduralMusicActivity01 = MoveTowards(", update);
            StringAssert.Contains("ResolveSoundscapeRestScale(_currentSoundscapeTier)", wait);
            StringAssert.Contains("ResolveSoundscapePhraseScale(_currentSoundscapeTier)", phrase);
            StringAssert.Contains("!IsEmergencyBreathDominant()", forceOpen);
            StringAssert.Contains("_combatLatched", forceOpen);
            StringAssert.Contains("_tenseExplorationLatched", forceOpen);
            StringAssert.Contains("_currentBaseContext", forceOpen);
            StringAssert.Contains("IsEmergencyBreathDominant()", target);
            StringAssert.Contains("MusicActivityReason.Emergency", target);
            StringAssert.Contains("MusicActivityReason.Combat", target);
            StringAssert.Contains("MusicActivityReason.Exploration", target);
            StringAssert.Contains("return 0f", target);
            StringAssert.Contains("MusicActivityReason.Menu", target);
            StringAssert.Contains("MusicActivityReason.Prologue", target);
            StringAssert.Contains("if (_menuSceneActive)", target);
            StringAssert.Contains("if (_prologueSceneActive)", target);
            StringAssert.Contains("_playbackState != PlaybackState.Playing", target);
            StringAssert.Contains("ResolveSoundscapePressure01(_currentSoundscapeTier)", target);
            StringAssert.Contains("soundscapePressure01 * 0.12f", target);
            StringAssert.Contains("ResolveEmergencyAudioDominance01()", target);
            StringAssert.Contains("ApplyForegroundSpeechMusicDuck01(", target);
            StringAssert.Contains("bool foregroundSpeechActive = IsForegroundSpeechActive();", publish);
            StringAssert.Contains("emergencyBreathDominates || foregroundSpeechActive", publish);
            StringAssert.Contains("float activity01 = emergencyBreathDominates ? 0f", publish);
            StringAssert.Contains("flags |= DynamicMusicScalarSignal.FlagSuppressReactiveImpulses", publish);
            StringAssert.Contains("PushDynamicMusicSignal(", publish);
            StringAssert.Contains("DynamicMusicScalarSignal.FlagExternalScalars | DynamicMusicScalarSignal.FlagSuppressReactiveImpulses", stopPublish);
            StringAssert.Contains("PublishProceduralMusicStopSignal();", stop);
            StringAssert.Contains("signal.MusicActivity01 = math.saturate", musicDirector);
            StringAssert.Contains("if (IsEmergencyBreathDominant() || IsForegroundSpeechActive())", stinger);
            StringAssert.Contains("RefreshForegroundSpeechMusicDucking();", stingerFlush);
            StringAssert.Contains("RefreshForegroundSpeechMusicDucking();", discoveryStinger);
            StringAssert.Contains("RefreshForegroundSpeechMusicDucking();", dangerStinger);
            StringAssert.Contains("RefreshForegroundSpeechMusicDucking();", recoveryStinger);
            StringAssert.Contains("RefreshForegroundSpeechMusicDucking();", overrideStart);
            StringAssert.Contains("bool emergencyBreathDominates = IsEmergencyBreathDominant();", overrideStart);
            StringAssert.Contains("bool foregroundSpeechActive = IsForegroundSpeechActive();", overrideStart);
            StringAssert.Contains("bool suppressReactiveImpulses = emergencyBreathDominates || foregroundSpeechActive", overrideStart);
            StringAssert.Contains("float overrideActivity01 = emergencyBreathDominates ? 0f : ApplyForegroundSpeechMusicDuck01(_overrideVolume)", overrideStart);
            StringAssert.Contains("float overrideImpulse01 = suppressReactiveImpulses ? 0f : _overrideVolume", overrideStart);
            StringAssert.Contains("float overridePitchKick01 = suppressReactiveImpulses ? 0f : 1f", overrideStart);
            StringAssert.Contains("flags |= DynamicMusicScalarSignal.FlagSuppressReactiveImpulses", overrideStart);
            StringAssert.Contains("_musicActivityReason = MusicActivityReason.Emergency", overrideStart);
            StringAssert.Contains("else if (!foregroundSpeechActive)", overrideStart);
            StringAssert.Contains("flags |= DynamicMusicScalarSignal.FlagStingerImpulse | DynamicMusicScalarSignal.FlagOverrideImpulse", overrideStart);
            AssertTextBefore(overrideStart, "RefreshForegroundSpeechMusicDucking();", "bool emergencyBreathDominates = IsEmergencyBreathDominant();");
            AssertTextBefore(overrideStart, "bool emergencyBreathDominates = IsEmergencyBreathDominant();", "PushDynamicMusicSignal(");
            AssertTextBefore(overrideStart, "bool foregroundSpeechActive = IsForegroundSpeechActive();", "PushDynamicMusicSignal(");
            AssertTextBefore(stingerFlush, "RefreshForegroundSpeechMusicDucking();", "IsForegroundSpeechActive()");
            AssertTextBefore(discoveryStinger, "RefreshForegroundSpeechMusicDucking();", "IsForegroundSpeechActive()");
            AssertTextBefore(dangerStinger, "RefreshForegroundSpeechMusicDucking();", "IsForegroundSpeechActive()");
            AssertTextBefore(recoveryStinger, "RefreshForegroundSpeechMusicDucking();", "IsForegroundSpeechActive()");
            AssertTextBefore(target, "if (IsEmergencyBreathDominant())", "if (_overrideActive)");
            AssertTextBefore(target, "if (IsEmergencyBreathDominant())", "if (_menuSceneActive)");
            AssertTextBefore(publish, "bool emergencyBreathDominates = IsEmergencyBreathDominant();", "float activity01 = emergencyBreathDominates ? 0f");
            AssertTextBefore(publish, "bool foregroundSpeechActive = IsForegroundSpeechActive();", "float damageImpulse01");
            AssertTextBefore(emergencyGate, "RefreshPlayerCriticalStressSignal();", "float liveOxygenDanger01");
            AssertTextBefore(stop, "_debugMusicActivity01 = 0f", "PublishProceduralMusicStopSignal();");
        }

        [Test]
        public void SoundscapeTierContextFeedsMusicDirectorPhrasing()
        {
            string soundscape = Read(SoundscapeSystemPath);
            string musicDirector = Read(MusicDirectorPath);
            string slowTick = ExtractMethodBody(soundscape, "public void SlowTick()");
            string onEnable = ExtractMethodBody(soundscape, "private void OnEnable()");
            string onDisable = ExtractMethodBody(soundscape, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(soundscape, "private void OnDestroy()");
            string depthTier = ExtractMethodBody(soundscape, "void IBiomeMatrixEventListener.OnDepthTierChanged(");
            string rebound = ExtractMethodBody(soundscape, "void IGlobalRegistryHotSwapRefListener.OnGlobalRegistryServiceRebound(");
            string hotSwap = ExtractMethodBody(soundscape, "void IGlobalRegistryHotSwapListener.OnGlobalRegistryServiceReplaced(");
            string resolveSoundscapeDepth = ExtractMethodBody(soundscape, "private bool TryResolveCurrentDepthMeters(out float depthMeters)");
            string sync = ExtractMethodBody(soundscape, "private void SyncMusicDirectorSoundscapeContext(");
            string syncCached = ExtractMethodBody(soundscape, "private void SyncCachedMusicDirectorSoundscapeContext(");
            string cacheMusic = ExtractMethodBody(soundscape, "private void CacheMusicDirector(");
            string setContext = ExtractMethodBody(musicDirector, "public void SetSoundscapeTierContext(");
            string resolveProfile = ExtractMethodBody(musicDirector, "private HectonMusicBiomeProfile ResolveProfile(");
            string soundscapeProfile = ExtractMethodBody(musicDirector, "private HectonMusicBiomeProfile ResolveSoundscapeTierProfile(");
            string resolveLayerDepth = ExtractMethodBody(musicDirector, "private float ResolveLayerDepthMeters()");
            string tryResolvePlayerLayerDepth = ExtractMethodBody(musicDirector, "private bool TryResolvePlayerMovementDepthMeters(");
            string tension = ExtractMethodBody(musicDirector, "private float ResolveTension01()");
            string route = ExtractMethodBody(musicDirector, "private void UpdateLayerRouting(");

            StringAssert.Contains("CacheMusicDirector(GlobalRegistry.MusicDirector)", onEnable);
            StringAssert.Contains("SyncMusicDirectorSoundscapeContext(_currentTier", onEnable);
            StringAssert.Contains("_musicDirector = null", onDisable);
            StringAssert.Contains("_musicDirector = null", onDestroy);
            StringAssert.Contains("SyncMusicDirectorSoundscapeContext(newTier, depth)", slowTick);
            AssertTextBefore(slowTick, "SyncMusicDirectorSoundscapeContext(newTier, depth)", "if (newTier == _currentTier)");
            StringAssert.Contains("TryResolveCurrentDepthMeters(out float playerDepthMeters)", depthTier);
            StringAssert.Contains("? playerDepthMeters", depthTier);
            StringAssert.Contains(": math.max(0f, math.isfinite(depthMeters) ? depthMeters : 0f)", depthTier);
            StringAssert.Contains("director.SetSoundscapeTierContext(CalculateTier(resolvedDepthMeters, _currentTier), resolvedDepthMeters)", depthTier);
            StringAssert.Contains("GlobalRegistryServiceSlot.MusicDirectorRuntime", rebound);
            StringAssert.Contains("CacheMusicDirector(currentService as HectonMusicDirector)", rebound);
            StringAssert.Contains("SyncCachedMusicDirectorSoundscapeContext(_currentTier, ResolveCurrentDepthMeters())", rebound);
            StringAssert.Contains("GlobalRegistryServiceSlot.Player", rebound);
            StringAssert.Contains("SyncMusicDirectorSoundscapeContext(_currentTier, ResolveCurrentDepthMeters())", rebound);
            StringAssert.Contains("GlobalRegistryServiceSlot.MusicDirectorRuntime", hotSwap);
            StringAssert.Contains("CacheMusicDirector(currentService as HectonMusicDirector)", hotSwap);
            StringAssert.Contains("SyncCachedMusicDirectorSoundscapeContext(_currentTier, ResolveCurrentDepthMeters())", hotSwap);
            StringAssert.Contains("GlobalRegistryServiceSlot.Player", hotSwap);
            StringAssert.Contains("SyncMusicDirectorSoundscapeContext(_currentTier, ResolveCurrentDepthMeters())", hotSwap);
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", resolveSoundscapeDepth);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", resolveSoundscapeDepth);
            AssertTextBefore(resolveSoundscapeDepth, "playerContext.TryGetMovementRuntimeState", "HectonSurvivalSystem currentSurvival = survivalSystem");
            StringAssert.Contains("director.SetSoundscapeTierContext(tier, depthMeters)", sync);
            StringAssert.Contains("HectonMusicDirector director = _musicDirector", syncCached);
            StringAssert.Contains("director == null || !director.isActiveAndEnabled", syncCached);
            StringAssert.Contains("director.SetSoundscapeTierContext(tier, depthMeters)", syncCached);
            StringAssert.Contains("musicDirector != null && musicDirector.isActiveAndEnabled", cacheMusic);
            StringAssert.Contains("SoundscapeTier safeTier = SanitizeSoundscapeTier(tier)", setContext);
            StringAssert.Contains("ResolveSoundscapeDepthHintMeters(safeTier)", setContext);
            StringAssert.Contains("float pressure01 = ResolveSoundscapePressure01(safeTier)", setContext);
            StringAssert.Contains("bool tierChanged = _currentSoundscapeTier != safeTier", setContext);
            StringAssert.Contains("_debugSoundscapePressure01 = pressure01", setContext);
            StringAssert.Contains("if (tierChanged)", setContext);
            StringAssert.Contains("ReevaluateContext(true);", setContext);
            StringAssert.Contains("ResolveSoundscapeTierProfile()", resolveProfile);
            AssertTextBefore(resolveProfile, "ResolveSoundscapeTierProfile()", "return _fallbackProfile");
            StringAssert.Contains("case SoundscapeTier.Thermal", soundscapeProfile);
            StringAssert.Contains("_thermalProfile != null ? _thermalProfile", soundscapeProfile);
            StringAssert.Contains("case SoundscapeTier.DeepAbyss", soundscapeProfile);
            StringAssert.Contains("_abyssProfile != null ? _abyssProfile", soundscapeProfile);
            StringAssert.Contains("case SoundscapeTier.Darkness", soundscapeProfile);
            StringAssert.Contains("_shelfProfile != null ? _shelfProfile", soundscapeProfile);
            StringAssert.Contains("case SoundscapeTier.Shallow", soundscapeProfile);
            StringAssert.Contains("_shallowProfile != null ? _shallowProfile", soundscapeProfile);
            StringAssert.Contains("float soundscapeDepthMeters = math.max(0f, _soundscapeDepthHintMeters);", resolveLayerDepth);
            StringAssert.Contains("TryResolvePlayerMovementDepthMeters(out float playerDepthMeters)", resolveLayerDepth);
            AssertTextBefore(resolveLayerDepth, "TryResolvePlayerMovementDepthMeters(out float playerDepthMeters)", "TryResolveBiomeMatrixContext(out _, out _, out float biomeDepthMeters)");
            StringAssert.Contains("IPlayerRuntimeContext playerContext = ResolvePlayerRuntimeContext();", tryResolvePlayerLayerDepth);
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", tryResolvePlayerLayerDepth);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u", tryResolvePlayerLayerDepth);
            StringAssert.Contains("depthMeters = math.max(0f, movementState.DepthMeters);", tryResolvePlayerLayerDepth);
            StringAssert.Contains("IPlayerRuntimeContext playerContext = ResolvePlayerRuntimeContext();", resolveLayerDepth);
            StringAssert.Contains("if (playerContext != null)", resolveLayerDepth);
            StringAssert.Contains("return 0f;", resolveLayerDepth);
            StringAssert.Contains("return soundscapeDepthMeters;", resolveLayerDepth);
            StringAssert.Contains("soundscapePressure01 * _soundscapePressureWeight", tension);
            StringAssert.Contains("_debugSoundscapePressure01 = soundscapePressure01", tension);
            StringAssert.Contains("math.max(InverseLerp(20f, 900f, depthMeters), soundscapePressure01)", route);
            StringAssert.Contains("soundscapePressure01 * 0.18f", route);
        }

        [Test]
        public void PlayerCriticalAudioAupRoutesUseRuntimePoseSnapshotBeforeLegacyMovement()
        {
            string renderer = Read(RendererPath);
            string musicDirector = Read(MusicDirectorPath);
            string psychosis = Read(DeepPsychosisControllerPath);
            string stressVfx = Read(PlayerStressVfxPath);

            string boundDistance = ExtractMethodBody(renderer, "private bool TryResolveBoundPlayerAupDistance(");
            string forwardProbe = ExtractMethodBody(renderer, "private bool TryResolveForwardEchoProbe(");
            string threatPulse = ExtractMethodBody(renderer, "private void UpdateAcousticThreatPulse()");
            string absoluteDepth = ExtractMethodBody(renderer, "private float ResolveAbsoluteDepthMeters()");
            string apexHeartbeat = ExtractMethodBody(renderer, "private void UpdateApexHeartbeatThreatCache()");
            string poseAup = ExtractMethodBody(renderer, "private bool TryResolvePlayerPoseAup(");
            string poseRuntimePosition = ExtractMethodBody(renderer, "private bool TryResolvePlayerPoseRuntimePosition(");
            string musicThreat = ExtractMethodBody(musicDirector, "private void RefreshLayerThreatSnapshot()");
            string musicThreatPose = ExtractMethodBody(musicDirector, "private bool TryResolvePlayerThreatPose(");
            string psychosisPosition = ExtractMethodBody(psychosis, "private bool TryResolvePlayerAupRuntimePosition(");
            string stressHeartbeat = ExtractMethodBody(stressVfx, "private Vector3 ResolveHeartbeatAudioPosition()");

            StringAssert.Contains("TryResolvePlayerPoseAup(out playerAup)", boundDistance);
            Assert.That(boundDistance.IndexOf("playerMovement.CurrentAup", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(CountOccurrences(renderer, "TryResolvePlayerPoseAup(out playerAup)"), Is.EqualTo(3));

            StringAssert.Contains("TryResolvePlayerPoseRuntimePosition(out Vector3 playerRuntimePosition)", forwardProbe);
            StringAssert.Contains("TryResolvePlayerPoseRuntimePosition(out Vector3 playerRuntimePosition)", threatPulse);
            StringAssert.Contains("TryResolvePlayerPoseRuntimePosition(out Vector3 playerPosition)", apexHeartbeat);
            Assert.That(forwardProbe.IndexOf("playerMovement.CurrentAup", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(threatPulse.IndexOf("playerMovement.CurrentAup", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(apexHeartbeat.IndexOf("playerMovement.CurrentAup", StringComparison.Ordinal), Is.LessThan(0));

            StringAssert.Contains("TryResolvePlayerPoseAup(out AbsoluteUniversePosition playerAup)", absoluteDepth);
            Assert.That(absoluteDepth.IndexOf("playerMovement.CurrentAup", StringComparison.Ordinal), Is.LessThan(0));

            StringAssert.Contains("IPlayerRuntimeContext playerContext = ResolvePlayerRuntimeContext();", poseAup);
            StringAssert.Contains("playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot pose)", poseAup);
            StringAssert.Contains("pose.Aup.IsFinite()", poseAup);
            StringAssert.Contains("return playerAup.IsFinite();", poseAup);
            AssertTextBefore(poseAup, "playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot pose)", "HectonPlayerMovement movement = playerMovement");
            AssertTextBefore(poseAup, "return false;", "HectonPlayerMovement movement = playerMovement");

            StringAssert.Contains("playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot pose)", poseRuntimePosition);
            StringAssert.Contains("math.all(math.isfinite(pose.RuntimePosition))", poseRuntimePosition);
            AssertTextBefore(poseRuntimePosition, "playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot pose)", "HectonPlayerMovement movement = playerMovement");
            AssertTextBefore(poseRuntimePosition, "return false;", "HectonPlayerMovement movement = playerMovement");

            StringAssert.Contains("TryResolvePlayerThreatPose(out AbsoluteUniversePosition playerAup, out Vector3 playerRuntimePosition)", musicThreat);
            Assert.That(musicThreat.IndexOf("_playerMovement.CurrentAup", StringComparison.Ordinal), Is.LessThan(0));
            StringAssert.Contains("playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot pose)", musicThreatPose);
            StringAssert.Contains("math.all(math.isfinite(pose.RuntimePosition))", musicThreatPose);
            AssertTextBefore(musicThreatPose, "playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot pose)", "HectonPlayerMovement movement = _playerMovement");
            AssertTextBefore(musicThreatPose, "return false;", "HectonPlayerMovement movement = _playerMovement");

            StringAssert.Contains("playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot pose)", psychosisPosition);
            StringAssert.Contains("math.all(math.isfinite(pose.RuntimePosition))", psychosisPosition);
            AssertTextBefore(psychosisPosition, "playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot pose)", "HectonPlayerMovement movement = _playerMovement");
            AssertTextBefore(psychosisPosition, "return false;", "HectonPlayerMovement movement = _playerMovement");

            StringAssert.Contains("playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot pose)", stressHeartbeat);
            StringAssert.Contains("math.all(math.isfinite(pose.RuntimePosition))", stressHeartbeat);
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", stressHeartbeat);
            AssertTextBefore(stressHeartbeat, "playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot pose)", "HectonPlayerMovement movement = _playerMovement");
            AssertTextBefore(stressHeartbeat, "return Vector3.zero;", "HectonPlayerMovement movement = _playerMovement");
            Assert.That(stressHeartbeat.IndexOf("playerContext.PlayerMovement", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void MusicDirectorLayerMixerRouteAppliesCachedOptionalExposedParameters()
        {
            string musicDirector = Read(MusicDirectorPath);
            string bind = ExtractMethodBody(musicDirector, "private void BindRuntimeVoiceRoutingCold()");
            string route = ExtractMethodBody(musicDirector, "private void UpdateLayerRouting(");
            string apply = ExtractMethodBody(musicDirector, "private void ApplyLayerMixerState(");
            string normalize = ExtractMethodBody(musicDirector, "private static float NormalizedLayerValueToDb(");
            string tryApply = ExtractMethodBody(musicDirector, "private bool TryApplyLayerMixerParameter(");
            string reset = ExtractMethodBody(musicDirector, "private void ResetLayerMixerStateCache()");

            StringAssert.Contains("_debugLayerMixerRouteAvailable", musicDirector);
            StringAssert.Contains("public float CurrentRhythmLayer01 => math.saturate(_layerRhythm01)", musicDirector);
            StringAssert.Contains("public float CurrentBassLayer01 => math.saturate(_layerBass01)", musicDirector);
            StringAssert.Contains("public float CurrentAtmosphereLayer01 => math.saturate(_layerAtmosphere01)", musicDirector);
            StringAssert.Contains("public float CurrentDangerLayer01 => math.saturate(_layerDanger01)", musicDirector);
            StringAssert.Contains("public bool CurrentLayerMixerRouteAvailable => _debugLayerMixerRouteAvailable", musicDirector);
            StringAssert.Contains("ResetLayerMixerStateCache();", bind);
            StringAssert.Contains("ApplyLayerMixerState(true);", bind);
            StringAssert.Contains("float emergencyAudio01 = ResolveEmergencyAudioDominance01()", route);
            StringAssert.Contains("_playerCriticalStress01 * 0.16f", route);
            StringAssert.Contains("ApplyLayerMixerState(false);", route);
            StringAssert.Contains("NormalizedLayerValueToDb(_layerRhythm01)", apply);
            StringAssert.Contains("NormalizedLayerValueToDb(_layerBass01)", apply);
            StringAssert.Contains("NormalizedLayerValueToDb(_layerAtmosphere01)", apply);
            StringAssert.Contains("NormalizedLayerValueToDb(_layerDanger01)", apply);
            StringAssert.Contains("_debugLayerMixerRouteAvailable = anyRouteAvailable", apply);
            StringAssert.Contains("Mathf.Log10(clamped)", normalize);
            StringAssert.Contains("MixerFloorDb", normalize);
            StringAssert.Contains("MixerCeilingDb", normalize);
            StringAssert.Contains("string.IsNullOrEmpty(parameterName)", tryApply);
            StringAssert.Contains("unavailable && !force", tryApply);
            StringAssert.Contains("math.abs(lastValueDb - valueDb) < 0.05f", tryApply);
            StringAssert.Contains("_layerMixer.SetFloat(parameterName, valueDb)", tryApply);
            StringAssert.Contains("unavailable = true", tryApply);
            StringAssert.Contains("lastValueDb = float.MinValue", tryApply);
            StringAssert.Contains("_rhythmLayerParameterUnavailable = false", reset);
            AssertTextBefore(bind, "ResetLayerMixerStateCache();", "ApplyLayerMixerState(true);");
            AssertTextBefore(tryApply, "if (unavailable && !force)", "if (!_layerMixer.SetFloat(parameterName, valueDb))");
        }

        [Test]
        public void AcousticZoneMusicAmbientDuckingUsesMusicDirectorStateOffAudioThread()
        {
            string acousticZone = Read(AcousticZoneControllerPath);
            string hotSwap = ExtractMethodBody(acousticZone, "public void OnGlobalRegistryServiceReplaced(");
            string clear = ExtractMethodBody(acousticZone, "private void ClearCachedRegistryServices()");
            string coldCache = ExtractMethodBody(acousticZone, "private void CacheRegistryServicesCold()");
            string cacheMusic = ExtractMethodBody(acousticZone, "private void CacheMusicDirector(");
            string tick = ExtractMethodBody(acousticZone, "public void Tick(");
            string diagnostics = ExtractMethodBody(acousticZone, "private void UpdateDiagnostics(");
            string mix = ExtractMethodBody(acousticZone, "private void UpdateAmbientLoopMix(");
            string duckUpdate = ExtractMethodBody(acousticZone, "private void UpdateMusicAmbientDucking(");
            string duckTarget = ExtractMethodBody(acousticZone, "private float ResolveMusicAmbientDuckTarget01(");
            string duckWeight = ExtractMethodBody(acousticZone, "private float ResolveMusicAmbientDuckReasonWeight01(");
            string duckVolume = ExtractMethodBody(acousticZone, "private float ResolveMusicAmbientDuckVolumeScale()");
            string onValidate = ExtractMethodBody(acousticZone, "private void OnValidate()");

            StringAssert.Contains("musicAmbientDuckMax", acousticZone);
            StringAssert.Contains("_debugMusicAmbientDuck", acousticZone);
            StringAssert.Contains("GlobalRegistryServiceSlot.MusicDirectorRuntime", hotSwap);
            StringAssert.Contains("CacheMusicDirector(currentService as HectonMusicDirector)", hotSwap);
            StringAssert.Contains("_cachedMusicDirector = null", clear);
            StringAssert.Contains("_currentMusicAmbientDuck01 = 0f", clear);
            StringAssert.Contains("CacheMusicDirector(GlobalRegistry.MusicDirector)", coldCache);
            StringAssert.Contains("musicDirector != null && musicDirector.isActiveAndEnabled", cacheMusic);
            StringAssert.Contains("UpdateMusicAmbientDucking(AcousticZoneState.Surface, deltaTime)", tick);
            StringAssert.Contains("UpdateMusicAmbientDucking(currentZone, deltaTime)", tick);
            Assert.That(
                tick.IndexOf("RefreshSoundscapeTierContext(false);", StringComparison.Ordinal),
                Is.LessThan(tick.LastIndexOf("UpdateMusicAmbientDucking(currentZone, deltaTime)", StringComparison.Ordinal)));
            StringAssert.Contains("ResolveMusicAmbientDuckVolumeScale()", diagnostics);
            StringAssert.Contains("targetVolume *= ResolveMusicAmbientDuckVolumeScale();", mix);
            StringAssert.Contains("ResolveMusicAmbientDuckTarget01(zone)", duckUpdate);
            StringAssert.Contains("ApproximateOneMinusExpNegPositive(math.max(0.01f, sharpness) * deltaTime)", duckUpdate);
            StringAssert.Contains("_debugMusicAmbientDuck = _currentMusicAmbientDuck01", duckUpdate);
            StringAssert.Contains("zone != AcousticZoneState.Underwater", duckTarget);
            StringAssert.Contains("HectonMusicDirector musicDirector = _cachedMusicDirector", duckTarget);
            StringAssert.Contains("musicDirector.CurrentMusicActivityReason", duckTarget);
            StringAssert.Contains("HectonMusicDirector.MusicActivityReason.Emergency", duckTarget);
            StringAssert.Contains("HectonMusicDirector.MusicActivityReason.Rest", duckTarget);
            StringAssert.Contains("musicDirector.CurrentMusicActivity01", duckTarget);
            StringAssert.Contains("ResolveMusicAmbientDuckReasonWeight01(reason)", duckTarget);
            AssertTextBefore(duckTarget, "HectonMusicDirector.MusicActivityReason.Emergency", "musicDirector.CurrentMusicActivity01");
            AssertTextBefore(duckTarget, "HectonMusicDirector.MusicActivityReason.Rest", "musicDirector.CurrentMusicActivity01");
            StringAssert.Contains("MusicActivityReason.Exploration", duckWeight);
            StringAssert.Contains("MusicActivityReason.Base", duckWeight);
            StringAssert.Contains("MusicActivityReason.Tense", duckWeight);
            StringAssert.Contains("MusicActivityReason.Combat", duckWeight);
            StringAssert.Contains("MusicActivityReason.Override", duckWeight);
            StringAssert.Contains("math.lerp(1f, math.max(0.1f, 1f - musicAmbientDuckMax)", duckVolume);
            StringAssert.Contains("if (musicAmbientDuckMax < 0f) musicAmbientDuckMax = 0f", onValidate);
            StringAssert.Contains("if (foregroundMusicAmbientDuckWeight > 1f) foregroundMusicAmbientDuckWeight = 1f", onValidate);
            Assert.That(duckUpdate.IndexOf("GlobalRegistry.", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(duckTarget.IndexOf("GlobalRegistry.", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(mix.IndexOf("GlobalRegistry.", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void MusicDebugHudShowsActivityReasonWithoutStringFormatting()
        {
            string debugUi = Read(SystemsDebugUiPath);
            string setter = ExtractMethodBody(debugUi, "private void SetMusicTensionActivityText(");
            string layerSetter = ExtractMethodBody(debugUi, "private void SetMusicLayerText(");
            string compact = ExtractMethodBody(debugUi, "private static int WriteCompactBudgetEntry(");
            string reason = ExtractMethodBody(debugUi, "private static string ResolveMusicReasonCode(");

            StringAssert.Contains("private readonly char[] _musicTensionBuffer = new char[32]", debugUi);
            StringAssert.Contains("private readonly char[] _musicLayerBuffer = new char[40]", debugUi);
            StringAssert.Contains("music.CurrentMusicActivityReason", debugUi);
            StringAssert.Contains("debugMusicReason = ResolveMusicReasonCode(music.CurrentMusicActivityReason)", debugUi);
            StringAssert.Contains("CreateLabel(\"MusicLayerLabel\", \"MUSIC LAYERS\"", debugUi);
            StringAssert.Contains("_titleValue == null || _musicLayerValue == null", debugUi);
            StringAssert.Contains("SetMusicLayerText(", debugUi);
            StringAssert.Contains("music.CurrentRhythmLayer01", debugUi);
            StringAssert.Contains("music.CurrentBassLayer01", debugUi);
            StringAssert.Contains("music.CurrentAtmosphereLayer01", debugUi);
            StringAssert.Contains("music.CurrentDangerLayer01", debugUi);
            StringAssert.Contains("music.CurrentLayerMixerRouteAvailable", debugUi);
            StringAssert.Contains("ResolveMusicReasonCode(reason)", setter);
            StringAssert.Contains("WriteCompactBudgetEntry(_musicLayerBuffer, index, 'R'", layerSetter);
            StringAssert.Contains("WriteCompactBudgetEntry(_musicLayerBuffer, index, 'B'", layerSetter);
            StringAssert.Contains("WriteCompactBudgetEntry(_musicLayerBuffer, index, 'A'", layerSetter);
            StringAssert.Contains("WriteCompactBudgetEntry(_musicLayerBuffer, index, 'D'", layerSetter);
            StringAssert.Contains("mixerRouteAvailable ? '+' : '-'", layerSetter);
            StringAssert.Contains("value.TryFormat(buffer.AsSpan(index)", compact);
            StringAssert.Contains("MusicActivityReason.Emergency", reason);
            StringAssert.Contains("return \"EMG\"", reason);
            StringAssert.Contains("MusicActivityReason.Combat", reason);
            StringAssert.Contains("return \"CBT\"", reason);
            StringAssert.Contains("MusicActivityReason.Exploration", reason);
            StringAssert.Contains("return \"EXP\"", reason);
            Assert.That(setter.IndexOf("string.Format", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(setter.IndexOf(".ToString(", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(layerSetter.IndexOf("string.Format", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(layerSetter.IndexOf(".ToString(", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void DynamicMusicHostRoutingUsesMusicDirectorAndSettingsOnlyOffAudioThread()
        {
            string synth = Read(DynamicMusicSynthPath);
            string awake = ExtractMethodBody(synth, "private void Awake()");
            string onEnable = ExtractMethodBody(synth, "private void OnEnable()");
            string unregister = ExtractMethodBody(synth, "private void UnregisterRuntime()");
            string clearCached = ExtractMethodBody(synth, "private void ClearCachedRuntimeServices()");
            string coldTick = ExtractMethodBody(synth, "public void ColdTick()");
            string hotSwap = ExtractMethodBody(synth, "public void OnGlobalRegistryServiceReplaced(");
            string route = ExtractMethodBody(synth, "private void ApplyAudioHostMixerRoute()");
            string fallback = ExtractMethodBody(synth, "private float ResolveFallbackMusicHostVolume01()");
            string drain = ExtractMethodBody(synth, "private void DrainSignalInputs()");
            string schedule = ExtractMethodBody(synth, "private void ScheduleSynthJobs(");
            string mockJob = ExtractMethodBody(synth, "private unsafe struct GenerateMockTensionJob");
            string callback = ExtractMethodBody(synth, "private void OnAudioFilterRead(");

            StringAssert.Contains("CacheMusicDirectorCold();", awake);
            StringAssert.Contains("CacheSettingsManagerCold();", awake);
            StringAssert.Contains("CacheMusicDirectorCold();", onEnable);
            StringAssert.Contains("CacheSettingsManagerCold();", onEnable);
            AssertTextBefore(awake, "CacheMusicDirectorCold();", "ConfigureAudioHostCold();");
            AssertTextBefore(awake, "CacheSettingsManagerCold();", "ConfigureAudioHostCold();");
            AssertTextBefore(onEnable, "CacheMusicDirectorCold();", "ConfigureAudioHostCold();");
            AssertTextBefore(onEnable, "CacheSettingsManagerCold();", "ConfigureAudioHostCold();");
            StringAssert.Contains("CacheMusicDirectorCold();", coldTick);
            StringAssert.Contains("CacheSettingsManagerCold();", coldTick);
            StringAssert.Contains("GlobalRegistryServiceSlot.SettingsRuntime", hotSwap);
            StringAssert.Contains("CacheSettingsManager(currentService as SettingsManager)", hotSwap);
            StringAssert.Contains("GlobalRegistryServiceSlot.MusicDirectorRuntime", hotSwap);
            StringAssert.Contains("CacheMusicDirector(currentService as HectonMusicDirector)", hotSwap);
            StringAssert.Contains("ClearCachedRuntimeServices();", unregister);
            StringAssert.Contains("_cachedAudioService = null", clearCached);
            StringAssert.Contains("_cachedMusicDirector = null", clearCached);
            StringAssert.Contains("_cachedSettingsManager = null", clearCached);
            StringAssert.Contains("HectonMusicDirector musicDirector = _cachedMusicDirector", route);
            StringAssert.Contains("musicDirector != null && musicDirector.isActiveAndEnabled ? musicDirector.DedicatedMusicMixerGroup : null", route);
            StringAssert.Contains("musicDirector.DedicatedMusicMixerGroup", route);
            StringAssert.Contains("_hostSource.volume = ResolveFallbackMusicHostVolume01();", route);
            StringAssert.Contains("audioService.AmbientGroup", route);
            StringAssert.Contains("SettingsManager settings = _cachedSettingsManager", fallback);
            StringAssert.Contains("settings.MusicVolume", fallback);
            StringAssert.Contains("bool signalIsMusicDirectorScalar", drain);
            StringAssert.Contains("if (signalIsMusicDirectorScalar || !receivedMusicDirectorScalar)", drain);
            StringAssert.Contains("if (signalIsMusicDirectorScalar)", drain);
            StringAssert.Contains("else if (!receivedMusicDirectorScalar && signal.MusicActivity01 > 0f)", drain);
            StringAssert.Contains("FlagSuppressReactiveImpulses", drain);
            StringAssert.Contains("_externalDamageImpulse01 = 0f", drain);
            StringAssert.Contains("_externalMusicActivity01 = 0f", drain);
            StringAssert.Contains("MusicDirectorScalarGraceTicks", synth);
            StringAssert.Contains("!_allowMockPlaybackWithoutDirector", drain);
            StringAssert.Contains("!receivedMusicDirectorScalar", drain);
            StringAssert.Contains("Volatile.Write(ref _suppressReactiveMusicImpulses", drain);
            StringAssert.Contains("Volatile.Read(ref _suppressReactiveMusicImpulses)", schedule);
            StringAssert.Contains("mockJob.SuppressReactiveImpulses", schedule);
            StringAssert.Contains("public int SuppressReactiveImpulses", mockJob);
            StringAssert.Contains("? 0f", mockJob);
            StringAssert.Contains("scalar.StingerImpulse = SuppressReactiveImpulses != 0", mockJob);
            AssertTextBefore(drain, "if (signalIsMusicDirectorScalar)", "if ((signal.Flags & DynamicMusicScalarSignal.FlagSuppressReactiveImpulses)");
            AssertTextBefore(drain, "if (suppressReactiveImpulses)", "_pendingStingerImpulse = math.saturate(stingerImpulse);");
            AssertTextBefore(drain, "!_allowMockPlaybackWithoutDirector", "_pendingStingerImpulse = math.saturate(stingerImpulse);");
            Assert.That(route.IndexOf("GlobalRegistry.", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(fallback.IndexOf("GlobalRegistry.", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(synth.IndexOf("PlayerStressSignal", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(synth.IndexOf("IVocalWarningSystem", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(synth.IndexOf("IAudioLogRuntime", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(synth.IndexOf("GlobalRegistry.VocalWarnings", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(synth.IndexOf("GlobalRegistry.AudioLogRuntime", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(callback.IndexOf("ResolveFallbackMusicHostVolume01", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(callback.IndexOf("SettingsManager", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(callback.IndexOf("MusicDirector", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void CoreAudioSystemsUseOnlyUsableAudioServiceRuntime()
        {
            string spatial = Read("Assets/_Project/Scripts/SpatialAudioManager.cs");
            string vehicleCockpit = Read("Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs");
            string renderer = Read(RendererPath);
            string musicDirector = Read(MusicDirectorPath);
            string soundscape = Read(SoundscapeSystemPath);
            string acousticZone = Read(AcousticZoneControllerPath);
            string dynamicSynth = Read(DynamicMusicSynthPath);
            string audioLog = Read(AudioLogSystemPath);
            string vocalWarning = Read(VocalWarningSystemPath);
            string prologue = Read(PrologueAudioPath);
            string psychosis = Read(DeepPsychosisControllerPath);
            string footstep = Read(PlayerFootstepAudioPath);
            string stressVfx = Read(PlayerStressVfxPath);

            string spatialRegister = ExtractMethodBody(spatial, "private bool TryRegisterAudioRuntimeServices()");
            string spatialAudioOwnerUsable = ExtractMethodBody(spatial, "private static bool IsAudioServiceOwnerUsable(");
            string spatialVirtualizationOwnerUsable = ExtractMethodBody(spatial, "private static bool IsAudioVirtualizationOwnerUsable(");
            string spatialColdRuntimeServices = ExtractMethodBody(spatial, "private void RefreshCachedAudioRuntimeServicesCold()");
            string spatialReboundRuntimeServices = ExtractMethodBody(spatial, "private void CacheReboundAudioRuntimeService(");
            string spatialPlayerCriticalCache = ExtractMethodBody(spatial, "private void CachePlayerCriticalAudio(");
            string spatialPlayerCriticalResolve = ExtractMethodBody(spatial, "private IPlayerCriticalAudioSignalSink ResolvePlayerCriticalAudioSignalSink()");
            string spatialPlayerCriticalUsable = ExtractMethodBody(spatial, "private static bool IsPlayerCriticalAudioSignalSinkUsable(");
            string spatialPrologueQueue = ExtractMethodBody(spatial, "public bool QueuePrologueAudioTransition(");
            string spatialHighSpeedQueue = ExtractMethodBody(spatial, "public bool QueueHighSpeedImpactSignal(");

            string cockpitHotSwap = ExtractMethodBody(vehicleCockpit, "public void OnGlobalRegistryServiceReplaced(");
            string cockpitColdCache = ExtractMethodBody(vehicleCockpit, "private void CacheRegistryServicesCold()");
            string cockpitPlayerCriticalCache = ExtractMethodBody(vehicleCockpit, "private void CachePlayerCriticalAudio(");
            string cockpitPlayerCriticalResolve = ExtractMethodBody(vehicleCockpit, "private IPlayerCriticalSonarEchoReadModel ResolvePlayerCriticalSonarEchoReadModel()");
            string cockpitPlayerCriticalUsable = ExtractMethodBody(vehicleCockpit, "private static bool IsPlayerCriticalSonarEchoReadModelUsable(");
            string cockpitSonarUpload = ExtractMethodBody(vehicleCockpit, "private void UploadSonarTapsAndDispatchRadar()");

            string rendererRegister = ExtractMethodBody(renderer, "private bool TryRegisterRuntimeService()");
            string rendererRuntimeUsable = ExtractMethodBody(renderer, "private static bool IsPlayerCriticalAudioRuntimeUsable(");

            string musicRegister = ExtractMethodBody(musicDirector, "private bool TryRegisterToGlobalRegistry()");
            string musicRuntimeUsable = ExtractMethodBody(musicDirector, "private static bool IsMusicDirectorRuntimeUsable(");
            string musicCache = ExtractMethodBody(musicDirector, "private void CacheAudioService(");
            string musicResolve = ExtractMethodBody(musicDirector, "private IAudioService ResolveAudioService()");
            string musicUsable = ExtractMethodBody(musicDirector, "private static bool IsAudioServiceUsable(");
            string musicMixer = ExtractMethodBody(musicDirector, "private AudioMixerGroup ResolveMusicMixerGroup()");

            string soundscapeDrain = ExtractMethodBody(soundscape, "private void DrainSignals()");
            string soundscapeImpact = ExtractMethodBody(soundscape, "private void HandleImpactSignal(");
            string soundscapeCache = ExtractMethodBody(soundscape, "private void CacheAudioService(");
            string soundscapeResolve = ExtractMethodBody(soundscape, "private IAudioService ResolveAudioService()");
            string soundscapeUsable = ExtractMethodBody(soundscape, "private static bool IsAudioServiceUsable(");

            string acousticCache = ExtractMethodBody(acousticZone, "private void CacheAudioService(");
            string acousticResolve = ExtractMethodBody(acousticZone, "private IAudioService ResolveAudioService()");
            string acousticSpatial = ExtractMethodBody(acousticZone, "private ISpatialAudioWorldEmitterReadModel ResolveSpatialAudioEmitterReadModel()");
            string acousticUsable = ExtractMethodBody(acousticZone, "private static bool IsAudioServiceUsable(");
            string acousticPhysicsRebind = ExtractMethodBody(acousticZone, "private void RebindPhysicsStateEventService(");
            string acousticPhysicsUsable = ExtractMethodBody(acousticZone, "private static bool IsPhysicsStateEventServiceUsable(");

            string synthCache = ExtractMethodBody(dynamicSynth, "private void CacheAudioService(");
            string synthResolve = ExtractMethodBody(dynamicSynth, "private IAudioService ResolveAudioService()");
            string synthUsable = ExtractMethodBody(dynamicSynth, "private static bool IsAudioServiceUsable(");
            string synthRoute = ExtractMethodBody(dynamicSynth, "private void ApplyAudioHostMixerRoute()");

            string audioLogRegister = ExtractMethodBody(audioLog, "private void TryRegisterService()");
            string audioLogSystemUsable = ExtractMethodBody(audioLog, "private static bool IsAudioLogSystemUsable(");
            string audioLogCache = ExtractMethodBody(audioLog, "private void CacheAudioService(");
            string audioLogResolve = ExtractMethodBody(audioLog, "private IAudioService ResolveAudioService()");
            string audioLogSinkResolve = ExtractMethodBody(audioLog, "private ISpatialAudioNarrativeRadioSink ResolveNarrativeAudioSink()");
            string audioLogUsable = ExtractMethodBody(audioLog, "private static bool IsAudioServiceUsable(");
            string audioLogSinkUsable = ExtractMethodBody(audioLog, "private static bool IsNarrativeAudioSinkUsable(");
            string audioLogQueue = ExtractMethodBody(audioLog, "private bool QueuePlaybackVisualSync(");
            string audioLogFlush = ExtractMethodBody(audioLog, "private void FlushPendingPlaybackVisualSync()");
            string audioLogRefreshGlitch = ExtractMethodBody(audioLog, "private void RefreshActiveNarrativeRadioGlitchVisualSync()");
            string audioLogResetGlitch = ExtractMethodBody(audioLog, "private void FlushPendingNarrativeRadioGlitchReset()");

            string vocalWarningRegister = ExtractMethodBody(vocalWarning, "private bool TryRegisterRuntimeService()");
            string vocalWarningUsable = ExtractMethodBody(vocalWarning, "private static bool IsVocalWarningSystemUsable(");

            string prologuePublish = ExtractMethodBody(prologue, "private void PublishAudioTransition(");
            string prologueNeutral = ExtractMethodBody(prologue, "private void PublishNeutralTransitionOnDisable()");
            string prologueCache = ExtractMethodBody(prologue, "private void CacheAudioService(");
            string prologueResolve = ExtractMethodBody(prologue, "private IAudioService ResolveAudioService()");
            string prologueUsable = ExtractMethodBody(prologue, "private static bool IsAudioServiceUsable(");

            string psychosisCache = ExtractMethodBody(psychosis, "private void CacheAudioService(");
            string psychosisResolve = ExtractMethodBody(psychosis, "private IAudioService ResolveAudioService()");
            string psychosisUsable = ExtractMethodBody(psychosis, "private static bool IsAudioServiceUsable(");
            string psychosisCue = ExtractMethodBody(psychosis, "private void PlayPsychosisCue()");

            string footstepHotSwap = ExtractMethodBody(footstep, "public void OnGlobalRegistryServiceReplaced(");
            string footstepColdCache = ExtractMethodBody(footstep, "private void RefreshColdRegistryReferences()");
            string footstepCache = ExtractMethodBody(footstep, "private void CacheAudioService(");
            string footstepResolve = ExtractMethodBody(footstep, "private IAudioService ResolveAudioService()");
            string footstepUsable = ExtractMethodBody(footstep, "private static bool IsAudioServiceUsable(");
            string footstepHandle = ExtractMethodBody(footstep, "private void HandleFootstep()");

            string stressHotSwap = ExtractMethodBody(stressVfx, "public void OnGlobalRegistryServiceReplaced(");
            string stressColdCache = ExtractMethodBody(stressVfx, "private void CacheRegistryServicesCold()");
            string stressCache = ExtractMethodBody(stressVfx, "private void CacheAudioService(");
            string stressResolve = ExtractMethodBody(stressVfx, "private IAudioService ResolveAudioService()");
            string stressUsable = ExtractMethodBody(stressVfx, "private static bool IsAudioServiceUsable(");
            string stressHeartbeat = ExtractMethodBody(stressVfx, "private void PlayHeartbeat(");

            AssertAudioServiceUsableBody(musicUsable);
            AssertAudioServiceUsableBody(soundscapeUsable);
            AssertAudioServiceUsableBody(acousticUsable);
            AssertAudioServiceUsableBody(synthUsable);
            AssertAudioServiceUsableBody(audioLogUsable);
            AssertAudioServiceUsableBody(prologueUsable);
            AssertAudioServiceUsableBody(psychosisUsable);
            AssertAudioServiceUsableBody(footstepUsable);
            AssertAudioServiceUsableBody(stressUsable);
            StringAssert.Contains("narrativeAudioSink is Behaviour behaviour", audioLogSinkUsable);
            StringAssert.Contains("return behaviour != null && behaviour.isActiveAndEnabled", audioLogSinkUsable);

            StringAssert.Contains("IAudioService registeredAudioService = GlobalRegistry.Audio", spatialRegister);
            StringAssert.Contains("IAudioVirtualizationService registeredVirtualization = GlobalRegistry.AudioVirtualization", spatialRegister);
            StringAssert.Contains("if (IsAudioServiceOwnerUsable(registeredAudioService))", spatialRegister);
            StringAssert.Contains("if (IsAudioVirtualizationOwnerUsable(registeredVirtualization))", spatialRegister);
            StringAssert.Contains("RestoreActiveRuntimeInstanceFromOwner(registeredAudioService)", spatialRegister);
            StringAssert.Contains("RestoreActiveRuntimeInstanceFromOwner(registeredVirtualization)", spatialRegister);
            StringAssert.Contains("GlobalRegistry.UnregisterAudioService(registeredAudioService);", spatialRegister);
            StringAssert.Contains("GlobalRegistry.UnregisterAudioVirtualizationService(registeredVirtualization);", spatialRegister);
            AssertTextBefore(spatialRegister, "GlobalRegistry.UnregisterAudioService(registeredAudioService);", "GlobalRegistry.RegisterAudioService(this);");
            AssertTextBefore(spatialRegister, "GlobalRegistry.UnregisterAudioVirtualizationService(registeredVirtualization);", "GlobalRegistry.RegisterAudioVirtualizationService(this);");
            StringAssert.Contains("return ReferenceEquals(GlobalRegistry.Audio, this) &&", spatialRegister);
            StringAssert.Contains("ReferenceEquals(audioService, null)", spatialAudioOwnerUsable);
            StringAssert.Contains("audioService is Behaviour behaviour", spatialAudioOwnerUsable);
            StringAssert.Contains("return audioService.IsInitialized", spatialAudioOwnerUsable);
            StringAssert.Contains("ReferenceEquals(virtualization, null)", spatialVirtualizationOwnerUsable);
            StringAssert.Contains("virtualization is Behaviour behaviour", spatialVirtualizationOwnerUsable);
            StringAssert.Contains("return virtualization.IsVirtualizationReady", spatialVirtualizationOwnerUsable);
            StringAssert.Contains("CachePlayerCriticalAudio(GlobalRegistry.PlayerCriticalAudioSignals)", spatialColdRuntimeServices);
            StringAssert.Contains("CachePlayerCriticalAudio(currentService as IPlayerCriticalAudioSignalSink)", spatialReboundRuntimeServices);
            StringAssert.Contains("_cachedPlayerCriticalAudio = IsPlayerCriticalAudioSignalSinkUsable(playerCriticalAudio)", spatialPlayerCriticalCache);
            StringAssert.Contains("if (IsPlayerCriticalAudioSignalSinkUsable(playerCriticalAudio))", spatialPlayerCriticalResolve);
            StringAssert.Contains("_cachedPlayerCriticalAudio = null", spatialPlayerCriticalResolve);
            StringAssert.Contains("playerCriticalAudio is Behaviour behaviour", spatialPlayerCriticalUsable);
            StringAssert.Contains("return behaviour != null && behaviour.isActiveAndEnabled", spatialPlayerCriticalUsable);
            StringAssert.Contains("IPlayerCriticalAudioSignalSink playerCriticalAudio = ResolvePlayerCriticalAudioSignalSink()", spatialPrologueQueue);
            StringAssert.Contains("IPlayerCriticalAudioSignalSink renderer = ResolvePlayerCriticalAudioSignalSink()", spatialHighSpeedQueue);
            Assert.That(spatialPrologueQueue.IndexOf("_cachedPlayerCriticalAudio", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(spatialHighSpeedQueue.IndexOf("_cachedPlayerCriticalAudio", StringComparison.Ordinal), Is.LessThan(0));

            StringAssert.Contains("CachePlayerCriticalAudio(currentService as IPlayerCriticalSonarEchoReadModel)", cockpitHotSwap);
            StringAssert.Contains("CachePlayerCriticalAudio(GlobalRegistry.PlayerCriticalSonarEcho)", cockpitColdCache);
            StringAssert.Contains("_cachedPlayerCriticalAudio = IsPlayerCriticalSonarEchoReadModelUsable(playerCriticalAudio)", cockpitPlayerCriticalCache);
            StringAssert.Contains("if (IsPlayerCriticalSonarEchoReadModelUsable(playerCriticalAudio))", cockpitPlayerCriticalResolve);
            StringAssert.Contains("_cachedPlayerCriticalAudio = null", cockpitPlayerCriticalResolve);
            StringAssert.Contains("playerCriticalAudio is Behaviour behaviour", cockpitPlayerCriticalUsable);
            StringAssert.Contains("return behaviour != null && behaviour.isActiveAndEnabled", cockpitPlayerCriticalUsable);
            StringAssert.Contains("IPlayerCriticalSonarEchoReadModel audioRuntime = ResolvePlayerCriticalSonarEchoReadModel()", cockpitSonarUpload);
            Assert.That(cockpitSonarUpload.IndexOf("IPlayerCriticalSonarEchoReadModel audioRuntime = _cachedPlayerCriticalAudio", StringComparison.Ordinal), Is.LessThan(0));

            StringAssert.Contains("PlayerCriticalProceduralAudioRenderer registeredInstance = GlobalRegistry.PlayerCriticalAudio", rendererRegister);
            StringAssert.Contains("!ReferenceEquals(registeredInstance, null)", rendererRegister);
            StringAssert.Contains("!ReferenceEquals(registeredInstance, this)", rendererRegister);
            StringAssert.Contains("if (IsPlayerCriticalAudioRuntimeUsable(registeredInstance))", rendererRegister);
            StringAssert.Contains("Destroy(this);", rendererRegister);
            StringAssert.Contains("GlobalRegistry.UnregisterPlayerCriticalAudioRuntime(registeredInstance);", rendererRegister);
            AssertTextBefore(rendererRegister, "GlobalRegistry.UnregisterPlayerCriticalAudioRuntime(registeredInstance);", "GlobalRegistry.RegisterPlayerCriticalAudioRuntime(this);");
            StringAssert.Contains("return renderer != null && renderer.isActiveAndEnabled", rendererRuntimeUsable);

            StringAssert.Contains("HectonMusicDirector activeDirector = GlobalRegistry.MusicDirector", musicRegister);
            StringAssert.Contains("!ReferenceEquals(activeDirector, null)", musicRegister);
            StringAssert.Contains("!ReferenceEquals(activeDirector, this)", musicRegister);
            StringAssert.Contains("if (IsMusicDirectorRuntimeUsable(activeDirector))", musicRegister);
            StringAssert.Contains("Destroy(gameObject);", musicRegister);
            StringAssert.Contains("GlobalRegistry.UnregisterMusicDirectorRuntime(activeDirector);", musicRegister);
            AssertTextBefore(musicRegister, "GlobalRegistry.UnregisterMusicDirectorRuntime(activeDirector);", "GlobalRegistry.RegisterMusicDirectorRuntime(this);");
            StringAssert.Contains("return director != null && director.isActiveAndEnabled", musicRuntimeUsable);

            StringAssert.Contains("_cachedAudioService = IsAudioServiceUsable(audioService) ? audioService : null", musicCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", musicResolve);
            StringAssert.Contains("_cachedAudioService = null", musicResolve);
            StringAssert.Contains("IAudioService audioService = ResolveAudioService()", musicMixer);

            StringAssert.Contains("_audioService = IsAudioServiceUsable(audioService) ? audioService : null", soundscapeCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", soundscapeResolve);
            StringAssert.Contains("_audioService = null", soundscapeResolve);
            StringAssert.Contains("IAudioService audio = ResolveAudioService()", soundscapeDrain);
            StringAssert.Contains("if (!IsAudioServiceUsable(audio))", soundscapeImpact);

            StringAssert.Contains("if (!IsAudioServiceUsable(audioService))", acousticCache);
            StringAssert.Contains("_cachedSpatialAudioEmitterReadModel = null", acousticCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", acousticResolve);
            StringAssert.Contains("_cachedSpatialAudioEmitterReadModel = null", acousticResolve);
            StringAssert.Contains("if (audioService == null)", acousticSpatial);
            AssertTextBefore(acousticPhysicsRebind, "!IsPhysicsStateEventServiceUsable(_physicsStateEvents)", "_physicsStateEvents.RegisterImpactListener(this);");
            StringAssert.Contains("return physicsStateEvents != null && physicsStateEvents.IsInitialized;", acousticPhysicsUsable);

            StringAssert.Contains("_cachedAudioService = IsAudioServiceUsable(audioService) ? audioService : null", synthCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", synthResolve);
            StringAssert.Contains("_cachedAudioService = null", synthResolve);
            StringAssert.Contains("IAudioService audioService = ResolveAudioService()", synthRoute);

            StringAssert.Contains("AudioLogSystem registeredAudioLogs = GlobalRegistry.AudioLogs", audioLogRegister);
            StringAssert.Contains("!ReferenceEquals(registeredAudioLogs, null)", audioLogRegister);
            StringAssert.Contains("!ReferenceEquals(registeredAudioLogs, this)", audioLogRegister);
            StringAssert.Contains("if (IsAudioLogSystemUsable(registeredAudioLogs))", audioLogRegister);
            StringAssert.Contains("Destroy(gameObject);", audioLogRegister);
            StringAssert.Contains("GlobalRegistry.UnregisterAudioLogRuntime(registeredAudioLogs);", audioLogRegister);
            AssertTextBefore(audioLogRegister, "GlobalRegistry.UnregisterAudioLogRuntime(registeredAudioLogs);", "GlobalRegistry.RegisterAudioLogRuntime(this);");
            StringAssert.Contains("return audioLogSystem != null && audioLogSystem.isActiveAndEnabled", audioLogSystemUsable);
            StringAssert.Contains("if (!IsAudioServiceUsable(audioService))", audioLogCache);
            StringAssert.Contains("_cachedNarrativeAudioSink = null", audioLogCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", audioLogResolve);
            StringAssert.Contains("_cachedNarrativeAudioSink = null", audioLogResolve);
            StringAssert.Contains("IAudioService audioService = ResolveAudioService()", audioLogSinkResolve);
            StringAssert.Contains("ResolveNarrativeAudioSink()", audioLogQueue);
            StringAssert.Contains("ResolveNarrativeAudioSink()", audioLogFlush);
            StringAssert.Contains("ResolveAudioService()", audioLogFlush);
            StringAssert.Contains("ResolveNarrativeAudioSink()", audioLogRefreshGlitch);
            StringAssert.Contains("ResolveNarrativeAudioSink()", audioLogResetGlitch);

            StringAssert.Contains("IVocalWarningSystem registeredVocalWarnings = GlobalRegistry.VocalWarnings", vocalWarningRegister);
            StringAssert.Contains("!ReferenceEquals(registeredVocalWarnings, null)", vocalWarningRegister);
            StringAssert.Contains("!ReferenceEquals(registeredVocalWarnings, this)", vocalWarningRegister);
            StringAssert.Contains("if (IsVocalWarningSystemUsable(registeredVocalWarnings))", vocalWarningRegister);
            StringAssert.Contains("Destroy(this);", vocalWarningRegister);
            StringAssert.Contains("GlobalRegistry.UnregisterVocalWarningRuntime(registeredVocalWarnings);", vocalWarningRegister);
            AssertTextBefore(vocalWarningRegister, "GlobalRegistry.UnregisterVocalWarningRuntime(registeredVocalWarnings);", "GlobalRegistry.RegisterVocalWarningRuntime(this);");
            StringAssert.Contains("ReferenceEquals(vocalWarningSystem, null)", vocalWarningUsable);
            StringAssert.Contains("vocalWarningSystem is Behaviour behaviour", vocalWarningUsable);
            StringAssert.Contains("!behaviour.isActiveAndEnabled", vocalWarningUsable);
            StringAssert.Contains("return vocalWarningSystem.IsInitialized", vocalWarningUsable);

            StringAssert.Contains("_audioService = IsAudioServiceUsable(audioService) ? audioService : null", prologueCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", prologueResolve);
            StringAssert.Contains("_audioService = null", prologueResolve);
            StringAssert.Contains("IAudioService audioService = ResolveAudioService()", prologuePublish);
            StringAssert.Contains("TryQueueNeutralTransition(ResolveAudioService())", prologueNeutral);

            StringAssert.Contains("_audioService = IsAudioServiceUsable(audioService) ? audioService : null", psychosisCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", psychosisResolve);
            StringAssert.Contains("_audioService = null", psychosisResolve);
            StringAssert.Contains("IAudioService audioManager = ResolveAudioService()", psychosisCue);

            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", footstepHotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", footstepColdCache);
            StringAssert.Contains("_audioService = IsAudioServiceUsable(audioService) ? audioService : null", footstepCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", footstepResolve);
            StringAssert.Contains("_audioService = null", footstepResolve);
            StringAssert.Contains("IAudioService sam = ResolveAudioService()", footstepHandle);

            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", stressHotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", stressColdCache);
            StringAssert.Contains("_cachedAudioService = IsAudioServiceUsable(audioService) ? audioService : null", stressCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", stressResolve);
            StringAssert.Contains("_cachedAudioService = null", stressResolve);
            StringAssert.Contains("IAudioService audioManager = ResolveAudioService()", stressHeartbeat);
            Assert.That(stressHeartbeat.IndexOf("_cachedAudioService", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void PhysicalDiegeticPanelAudioUsesOnlyUsableAudioServiceRuntime()
        {
            string dial = Read(PhysicalPanelDialPath);
            string keyboard = Read(PhysicalTerminalKeyboardPath);
            string button = Read(PhysicalPanelButtonPath);

            string dialHotSwap = ExtractMethodBody(dial, "public void OnGlobalRegistryServiceReplaced(");
            string dialOnEnable = ExtractMethodBody(dial, "private void OnEnable()");
            string dialCache = ExtractMethodBody(dial, "private void CacheAudioService(");
            string dialResolve = ExtractMethodBody(dial, "private IAudioService ResolveAudioService()");
            string dialUsable = ExtractMethodBody(dial, "private static bool IsAudioServiceUsable(");
            string dialQueue = ExtractMethodBody(dial, "private void QueueScrollAudio()");

            string keyboardHotSwap = ExtractMethodBody(keyboard, "public void OnGlobalRegistryServiceReplaced(");
            string keyboardOnEnable = ExtractMethodBody(keyboard, "private void OnEnable()");
            string keyboardCache = ExtractMethodBody(keyboard, "private void CacheAudioService(");
            string keyboardResolve = ExtractMethodBody(keyboard, "private IAudioService ResolveAudioService()");
            string keyboardUsable = ExtractMethodBody(keyboard, "private static bool IsAudioServiceUsable(");
            string keyboardQueue = ExtractMethodBody(keyboard, "private void QueuePressAudio()");

            string buttonHotSwap = ExtractMethodBody(button, "public void OnGlobalRegistryServiceReplaced(");
            string buttonColdCache = ExtractMethodBody(button, "private void CacheRegistryServicesCold()");
            string buttonCache = ExtractMethodBody(button, "private void CacheAudioService(");
            string buttonResolve = ExtractMethodBody(button, "private IAudioService ResolveAudioService()");
            string buttonUsable = ExtractMethodBody(button, "private static bool IsAudioServiceUsable(");
            string buttonClick = ExtractMethodBody(button, "private void PlayDiegeticClick(");

            AssertAudioServiceUsableBody(dialUsable);
            AssertAudioServiceUsableBody(keyboardUsable);
            AssertAudioServiceUsableBody(buttonUsable);

            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", dialOnEnable);
            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", dialHotSwap);
            StringAssert.Contains("_cachedAudioService = IsAudioServiceUsable(audioService) ? audioService : null", dialCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", dialResolve);
            StringAssert.Contains("_cachedAudioService = null", dialResolve);
            StringAssert.Contains("IAudioService audio = ResolveAudioService()", dialQueue);
            Assert.That(dialQueue.IndexOf("_cachedAudioService", StringComparison.Ordinal), Is.LessThan(0));

            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", keyboardOnEnable);
            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", keyboardHotSwap);
            StringAssert.Contains("_cachedAudioService = IsAudioServiceUsable(audioService) ? audioService : null", keyboardCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", keyboardResolve);
            StringAssert.Contains("_cachedAudioService = null", keyboardResolve);
            StringAssert.Contains("IAudioService audio = ResolveAudioService()", keyboardQueue);
            Assert.That(keyboardQueue.IndexOf("_cachedAudioService", StringComparison.Ordinal), Is.LessThan(0));

            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", buttonColdCache);
            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", buttonHotSwap);
            StringAssert.Contains("_cachedAudioService = IsAudioServiceUsable(audioService) ? audioService : null", buttonCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", buttonResolve);
            StringAssert.Contains("_cachedAudioService = null", buttonResolve);
            StringAssert.Contains("IAudioService audio = ResolveAudioService()", buttonClick);
            Assert.That(buttonClick.IndexOf("_cachedAudioService", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void UiWarningAndButtonAudioUsesOnlyUsableAudioServiceRuntime()
        {
            string advisory = Read(SuitAdvisoryControllerPath);
            string feedback = Read(UIAudioFeedbackPath);
            string trigger = Read(UIButtonAudioTriggerPath);

            string advisoryAwake = ExtractMethodBody(advisory, "private void Awake()");
            string advisoryOnEnable = ExtractMethodBody(advisory, "private void OnEnable()");
            string advisoryHotSwap = ExtractMethodBody(advisory, "public void OnGlobalRegistryServiceReplaced(");
            string advisoryCache = ExtractMethodBody(advisory, "private void CacheAudioService(");
            string advisoryResolve = ExtractMethodBody(advisory, "private IAudioService ResolveAudioService()");
            string advisoryUsable = ExtractMethodBody(advisory, "private static bool IsAudioServiceUsable(");
            string advisoryPlay = ExtractMethodBody(advisory, "private void PlayUiClip(");

            string feedbackAwake = ExtractMethodBody(feedback, "private void Awake()");
            string feedbackRegister = ExtractMethodBody(feedback, "private bool TryRegisterRuntime()");
            string feedbackExistingRuntime = ExtractMethodBody(feedback, "private bool TryAbortForUsableExistingRuntime()");
            string feedbackRuntimeUsable = ExtractMethodBody(feedback, "private static bool IsUIAudioFeedbackRuntimeUsable(");
            string feedbackRebound = ExtractMethodBody(feedback, "public void OnGlobalRegistryServiceRebound(");
            string feedbackHotSwap = ExtractMethodBody(feedback, "public void OnGlobalRegistryServiceReplaced(");
            string feedbackHotSwapRegister = ExtractMethodBody(feedback, "private void TryRegisterHotSwapListener()");
            string feedbackHotSwapUnregister = ExtractMethodBody(feedback, "private void TryUnregisterHotSwapListener()");
            string feedbackBind = ExtractMethodBody(feedback, "private void BindAudioAndRegisterControls()");
            string feedbackCache = ExtractMethodBody(feedback, "private void CacheAudioService(");
            string feedbackResolve = ExtractMethodBody(feedback, "private IAudioService ResolveAudioService()");
            string feedbackUsable = ExtractMethodBody(feedback, "private static bool IsAudioServiceUsable(");
            string feedbackPlay = ExtractMethodBody(feedback, "private void PlaySound(");

            string triggerAwake = ExtractMethodBody(trigger, "private void Awake()");
            string triggerOnEnable = ExtractMethodBody(trigger, "private void OnEnable()");
            string triggerHotSwap = ExtractMethodBody(trigger, "public void OnGlobalRegistryServiceReplaced(");
            string triggerCache = ExtractMethodBody(trigger, "private void CacheAudioService(");
            string triggerResolve = ExtractMethodBody(trigger, "private IAudioService ResolveAudioService()");
            string triggerUsable = ExtractMethodBody(trigger, "private static bool IsAudioServiceUsable(");
            string triggerClick = ExtractMethodBody(trigger, "private void OnButtonClicked()");

            AssertAudioServiceUsableBody(advisoryUsable);
            AssertAudioServiceUsableBody(feedbackUsable);
            AssertAudioServiceUsableBody(triggerUsable);

            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", advisoryAwake);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", advisoryOnEnable);
            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", advisoryHotSwap);
            StringAssert.Contains("_cachedAudioService = IsAudioServiceUsable(audioService) ? audioService : null", advisoryCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", advisoryResolve);
            StringAssert.Contains("_cachedAudioService = null", advisoryResolve);
            StringAssert.Contains("IAudioService audio = ResolveAudioService()", advisoryPlay);
            Assert.That(advisoryPlay.IndexOf("_cachedAudioService", StringComparison.Ordinal), Is.LessThan(0));

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", feedbackAwake);
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", feedbackRegister);
            AssertTextBefore(feedbackRegister, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterUIAudioFeedbackRuntime(this);");
            StringAssert.Contains("UIAudioFeedback registered = GlobalRegistry.UIAudioFeedback", feedbackExistingRuntime);
            StringAssert.Contains("ReferenceEquals(registered, null)", feedbackExistingRuntime);
            StringAssert.Contains("ReferenceEquals(registered, this)", feedbackExistingRuntime);
            StringAssert.Contains("if (IsUIAudioFeedbackRuntimeUsable(registered))", feedbackExistingRuntime);
            StringAssert.Contains("Destroy(gameObject);", feedbackExistingRuntime);
            StringAssert.Contains("GlobalRegistry.UnregisterUIAudioFeedbackRuntime(registered);", feedbackExistingRuntime);
            StringAssert.Contains("s_activeRuntime = null", feedbackExistingRuntime);
            StringAssert.Contains("feedback != null && feedback._runtimeRegistered && feedback.isActiveAndEnabled", feedbackRuntimeUsable);
            Assert.That(feedbackAwake.IndexOf("registered != null && registered != this", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(feedbackRegister.IndexOf("registered != null && registered != this", StringComparison.Ordinal), Is.LessThan(0));
            StringAssert.Contains("_hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);", feedbackHotSwapRegister);
            StringAssert.Contains("GlobalRegistry.TryUnregisterHotSwapListener(this);", feedbackHotSwapUnregister);
            StringAssert.DoesNotContain("GlobalRegistry.UnregisterHotSwapListener(this);", feedbackHotSwapUnregister);
            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", feedbackRebound);
            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", feedbackHotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", feedbackBind);
            StringAssert.Contains("_audioManager = IsAudioServiceUsable(audioService) ? audioService : null", feedbackCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", feedbackResolve);
            StringAssert.Contains("_audioManager = null", feedbackResolve);
            StringAssert.Contains("IAudioService audioManager = ResolveAudioService()", feedbackPlay);
            Assert.That(feedbackPlay.IndexOf("_audioManager.PlayStatic2D", StringComparison.Ordinal), Is.LessThan(0));

            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", triggerAwake);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", triggerOnEnable);
            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", triggerHotSwap);
            StringAssert.Contains("_audioManager = IsAudioServiceUsable(audioService) ? audioService : null", triggerCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", triggerResolve);
            StringAssert.Contains("_audioManager = null", triggerResolve);
            StringAssert.Contains("IAudioService audioManager = ResolveAudioService()", triggerClick);
            Assert.That(triggerClick.IndexOf("_audioManager", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void SpectrumSystemAudioUsesOnlyUsableAudioServiceRuntime()
        {
            string spectrum = Read(SpectrumSystemPath);

            string hotSwap = ExtractMethodBody(spectrum, "public void OnGlobalRegistryServiceReplaced(");
            string coldCache = ExtractMethodBody(spectrum, "private void CacheRegistryServicesCold()");
            string clearRegistry = ExtractMethodBody(spectrum, "private void ClearCachedRegistryServices()");
            string cache = ExtractMethodBody(spectrum, "private void CacheAudioService(");
            string resolve = ExtractMethodBody(spectrum, "private IAudioService ResolveAudioService()");
            string clearAudio = ExtractMethodBody(spectrum, "private void ClearCachedAudioService()");
            string usable = ExtractMethodBody(spectrum, "private static bool IsAudioServiceUsable(");
            string flush = ExtractMethodBody(spectrum, "private void FlushQueuedSpectrumAudio()");

            AssertAudioServiceUsableBody(usable);
            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", hotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", coldCache);
            StringAssert.Contains("ClearCachedAudioService()", clearRegistry);
            StringAssert.Contains("if (!IsAudioServiceUsable(audioService))", cache);
            StringAssert.Contains("_audioService = audioService", cache);
            StringAssert.Contains("_spatialAudioEmitterReadModel = _audioService as ISpatialAudioWorldEmitterReadModel", cache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", resolve);
            StringAssert.Contains("ClearCachedAudioService()", resolve);
            StringAssert.Contains("_audioService = null", clearAudio);
            StringAssert.Contains("_spatialAudioEmitterReadModel = null", clearAudio);
            StringAssert.Contains("Hecton8.Core.IAudioService audioManager = ResolveAudioService()", flush);
            StringAssert.Contains("audioManager.PlayStatic2D", flush);
            Assert.That(flush.IndexOf("Hecton8.Core.IAudioService audioManager = _audioService", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void SurfaceWeatherThunderAudioUsesOnlyUsableAudioServiceRuntime()
        {
            string weather = Read(SurfaceWeatherDirectorPath);

            string hotSwap = ExtractMethodBody(weather, "public void OnGlobalRegistryServiceReplaced(");
            string coldCache = ExtractMethodBody(weather, "private void CacheAudioRuntimeCold()");
            string cache = ExtractMethodBody(weather, "private void CacheAudioService(");
            string resolve = ExtractMethodBody(weather, "private IAudioService ResolveAudioService()");
            string usable = ExtractMethodBody(weather, "private static bool IsAudioServiceUsable(");
            string playThunder = ExtractMethodBody(weather, "private void PlayThunderNow()");

            AssertAudioServiceUsableBody(usable);
            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", hotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", coldCache);
            StringAssert.Contains("_audioRuntime = IsAudioServiceUsable(audioService) ? audioService : null", cache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", resolve);
            StringAssert.Contains("_audioRuntime = null", resolve);
            StringAssert.Contains("IAudioService audioManager = ResolveAudioService()", playThunder);
            Assert.That(playThunder.IndexOf("_audioRuntime", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(playThunder.IndexOf("GlobalRegistry.Audio", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void PlayerCriticalKineticImpactWaterlineFallbackMatchesRuntimeSeaLevel()
        {
            string renderer = Read(RendererPath);
            string resolveWaterline = ExtractMethodBody(renderer, "private float ResolveKineticImpactWaterlineY()");
            string resolveDepth = ExtractMethodBody(renderer, "private float ResolveAbsoluteDepthMeters()");

            StringAssert.Contains("private const float KineticImpactDefaultWaterlineY = 14.02f;", renderer);
            StringAssert.Contains("playerMovement != null && TryResolveKineticImpactWaterlineY(playerMovement.CurrentWaterSurfaceY, out float playerWaterlineY)", resolveWaterline);
            StringAssert.Contains("return playerWaterlineY;", resolveWaterline);
            StringAssert.Contains("return KineticImpactDefaultWaterlineY;", resolveWaterline);
            StringAssert.Contains("private static bool TryResolveKineticImpactWaterlineY(float candidateWaterlineY, out float waterlineY)", renderer);
            StringAssert.Contains("math.abs(candidateWaterlineY) > 0.0001f", renderer);
            StringAssert.Contains("math.abs(candidateWaterlineY) <= 1000f", renderer);
            StringAssert.Contains("waterlineY = KineticImpactDefaultWaterlineY;", renderer);
            StringAssert.DoesNotContain("math.isfinite(playerMovement.CurrentWaterSurfaceY)", resolveWaterline);
            StringAssert.DoesNotContain("return playerMovement.CurrentWaterSurfaceY;", resolveWaterline);
            StringAssert.Contains("double absoluteSurfaceY = (double)ResolveKineticImpactWaterlineY() + originAup.ToAbsoluteDouble3().y;", resolveDepth);
            StringAssert.DoesNotContain("(double)playerMovement.CurrentWaterSurfaceY", resolveDepth);
            StringAssert.DoesNotContain("private const float KineticImpactDefaultWaterlineY = 0f;", renderer);
        }

        [Test]
        public void UnderwaterVisualsThermoclineAudioUsesOnlyUsableAudioServiceRuntime()
        {
            string visuals = Read(HectonUnderwaterVisualsPath);

            string hotSwap = ExtractMethodBody(visuals, "public void OnGlobalRegistryServiceReplaced(");
            string coldCache = ExtractMethodBody(visuals, "private void CacheRuntimeDependencies()");
            string cache = ExtractMethodBody(visuals, "private void CacheAudioService(");
            string resolve = ExtractMethodBody(visuals, "private IAudioService ResolveAudioService()");
            string usable = ExtractMethodBody(visuals, "private static bool IsAudioServiceUsable(");
            string thermocline = ExtractMethodBody(visuals, "private void TryHandleThermoclineTransition(");

            AssertAudioServiceUsableBody(usable);
            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", hotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", coldCache);
            StringAssert.Contains("_audioRuntime = IsAudioServiceUsable(audioService) ? audioService : null", cache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", resolve);
            StringAssert.Contains("_audioRuntime = null", resolve);
            StringAssert.Contains("IAudioService audioRuntime = ResolveAudioService()", thermocline);
            StringAssert.Contains("audioRuntime.PlayStatic2D", thermocline);
            Assert.That(hotSwap.IndexOf("_audioRuntime = currentService as IAudioService", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(coldCache.IndexOf("_audioRuntime = GlobalRegistry.Audio", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(thermocline.IndexOf("IAudioService audioRuntime = _audioRuntime", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(thermocline.IndexOf("GlobalRegistry.Audio", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void InteractionFeedbackAudioUsesOnlyUsableAudioServiceRuntime()
        {
            string playerInteraction = Read(PlayerInteractionPath);
            string saveStation = Read(SaveStationPath);
            string physicalSwitch = Read(PhysicalSnapSwitchPath);

            string playerHotSwap = ExtractMethodBody(playerInteraction, "public void OnGlobalRegistryServiceReplaced(");
            string playerColdCache = ExtractMethodBody(playerInteraction, "private void RefreshCachedRegistryServices()");
            string playerCache = ExtractMethodBody(playerInteraction, "private void CacheAudioService(");
            string playerResolve = ExtractMethodBody(playerInteraction, "private IAudioService ResolveAudioService()");
            string playerUsable = ExtractMethodBody(playerInteraction, "private static bool IsAudioServiceUsable(");
            string playerQueue = ExtractMethodBody(playerInteraction, "private void QueueStaticAudio(");
            string playerFlush = ExtractMethodBody(playerInteraction, "private void FlushQueuedStaticAudio()");

            string saveHotSwap = ExtractMethodBody(saveStation, "public void OnGlobalRegistryServiceReplaced(");
            string saveColdCache = ExtractMethodBody(saveStation, "private void CacheRegistryServicesCold()");
            string saveCache = ExtractMethodBody(saveStation, "private void CacheAudioService(");
            string saveResolve = ExtractMethodBody(saveStation, "private Hecton8.Core.IAudioService ResolveAudioService()");
            string saveUsable = ExtractMethodBody(saveStation, "private static bool IsAudioServiceUsable(");
            string savePlay = ExtractMethodBody(saveStation, "private void PlayInteractionSound()");

            string switchHotSwap = ExtractMethodBody(physicalSwitch, "public void OnGlobalRegistryServiceReplaced(");
            string switchColdCache = ExtractMethodBody(physicalSwitch, "private void RefreshColdRegistryReferences()");
            string switchCache = ExtractMethodBody(physicalSwitch, "private void CacheAudioService(");
            string switchResolve = ExtractMethodBody(physicalSwitch, "private IAudioService ResolveAudioService()");
            string switchUsable = ExtractMethodBody(physicalSwitch, "private static bool IsAudioServiceUsable(");
            string switchQueue = ExtractMethodBody(physicalSwitch, "private void QueueSnapAudio(");

            AssertAudioServiceUsableBody(playerUsable);
            AssertAudioServiceUsableBody(saveUsable);
            AssertAudioServiceUsableBody(switchUsable);

            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", playerHotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", playerColdCache);
            StringAssert.Contains("_audioService = IsAudioServiceUsable(audioService) ? audioService : null", playerCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", playerResolve);
            StringAssert.Contains("_audioService = null", playerResolve);
            StringAssert.Contains("ResolveAudioService() == null", playerQueue);
            Assert.That(playerQueue.IndexOf("_audioService", StringComparison.Ordinal), Is.LessThan(0));
            StringAssert.Contains("IAudioService audioService = ResolveAudioService()", playerFlush);
            Assert.That(playerFlush.IndexOf("IAudioService audioService = _audioService", StringComparison.Ordinal), Is.LessThan(0));

            StringAssert.Contains("CacheAudioService(currentService as Hecton8.Core.IAudioService)", saveHotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", saveColdCache);
            StringAssert.Contains("_audioService = IsAudioServiceUsable(audioService) ? audioService : null", saveCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", saveResolve);
            StringAssert.Contains("_audioService = null", saveResolve);
            StringAssert.Contains("Hecton8.Core.IAudioService audioManager = ResolveAudioService()", savePlay);
            Assert.That(savePlay.IndexOf("_audioService", StringComparison.Ordinal), Is.LessThan(0));

            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", switchHotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", switchColdCache);
            StringAssert.Contains("_audioService = IsAudioServiceUsable(audioService) ? audioService : null", switchCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", switchResolve);
            StringAssert.Contains("_audioService = null", switchResolve);
            StringAssert.Contains("IAudioService audio = ResolveAudioService()", switchQueue);
            Assert.That(switchQueue.IndexOf("_audioService", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(switchQueue.IndexOf("!audio.IsInitialized", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void GameplayFeedbackAudioUsesOnlyUsableAudioServiceRuntime()
        {
            string oxygenPlant = Read(OxygenPlantPath);
            string oxygenBubble = Read(OxygenBubblePath);
            string storageCrate = Read(StorageCratePath);
            string messageTerminal = Read(MessageTerminalPath);
            string playerAction = Read(PlayerActionControllerPath);

            string plantHotSwap = ExtractMethodBody(oxygenPlant, "public void OnGlobalRegistryServiceReplaced(");
            string plantColdCache = ExtractMethodBody(oxygenPlant, "private void RefreshColdRegistryReferences()");
            string plantCache = ExtractMethodBody(oxygenPlant, "private void CacheAudioService(");
            string plantResolve = ExtractMethodBody(oxygenPlant, "private IAudioService ResolveAudioService()");
            string plantUsable = ExtractMethodBody(oxygenPlant, "private static bool IsAudioServiceUsable(");
            string plantLateFrame = ExtractMethodBody(oxygenPlant, "public void LateFrameTick()");

            string bubbleHotSwap = ExtractMethodBody(oxygenBubble, "public void OnGlobalRegistryServiceReplaced(");
            string bubbleColdCache = ExtractMethodBody(oxygenBubble, "private void CacheRegistryServicesCold()");
            string bubbleCache = ExtractMethodBody(oxygenBubble, "private void CacheAudioService(");
            string bubbleResolve = ExtractMethodBody(oxygenBubble, "private IAudioService ResolveAudioService()");
            string bubbleUsable = ExtractMethodBody(oxygenBubble, "private static bool IsAudioServiceUsable(");
            string bubbleCollect = ExtractMethodBody(oxygenBubble, "private void PlayCollectEffects(");

            string crateHotSwap = ExtractMethodBody(storageCrate, "public void OnGlobalRegistryServiceReplaced(");
            string crateColdCache = ExtractMethodBody(storageCrate, "private void CacheRegistryServicesCold()");
            string crateCache = ExtractMethodBody(storageCrate, "private void CacheAudioService(");
            string crateResolve = ExtractMethodBody(storageCrate, "private IAudioService ResolveAudioService()");
            string crateUsable = ExtractMethodBody(storageCrate, "private static bool IsAudioServiceUsable(");
            string crateOpen = ExtractMethodBody(storageCrate, "public void OpenCrate()");
            string crateClose = ExtractMethodBody(storageCrate, "public void CloseCrate()");

            string terminalHotSwap = ExtractMethodBody(messageTerminal, "public void OnGlobalRegistryServiceReplaced(");
            string terminalColdCache = ExtractMethodBody(messageTerminal, "private void CacheRegistryServicesCold()");
            string terminalCache = ExtractMethodBody(messageTerminal, "private void CacheAudioService(");
            string terminalResolve = ExtractMethodBody(messageTerminal, "private IAudioService ResolveAudioService()");
            string terminalUsable = ExtractMethodBody(messageTerminal, "private static bool IsAudioServiceUsable(");
            string terminalQueue = ExtractMethodBody(messageTerminal, "private void QueueStaticAudio(");
            string terminalFlush = ExtractMethodBody(messageTerminal, "private void FlushQueuedStaticAudio()");

            string actionHotSwap = ExtractMethodBody(playerAction, "public void OnGlobalRegistryServiceReplaced(");
            string actionColdCache = ExtractMethodBody(playerAction, "private void CacheRegistryServicesCold()");
            string actionCache = ExtractMethodBody(playerAction, "private void CacheAudioService(");
            string actionResolve = ExtractMethodBody(playerAction, "private IAudioService ResolveAudioService()");
            string actionUsable = ExtractMethodBody(playerAction, "private static bool IsAudioServiceUsable(");
            string actionCompletion = ExtractMethodBody(playerAction, "private void PlayCompletionSound(");
            string actionCancel = ExtractMethodBody(playerAction, "private void PlayCancelSound()");
            string actionFlush = ExtractMethodBody(playerAction, "private void FlushQueuedActionAudio()");

            AssertAudioServiceUsableBody(plantUsable);
            AssertAudioServiceUsableBody(bubbleUsable);
            AssertAudioServiceUsableBody(crateUsable);
            AssertAudioServiceUsableBody(terminalUsable);
            AssertAudioServiceUsableBody(actionUsable);

            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", plantHotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", plantColdCache);
            StringAssert.Contains("_audioService = IsAudioServiceUsable(audioService) ? audioService : null", plantCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", plantResolve);
            StringAssert.Contains("_audioService = null", plantResolve);
            StringAssert.Contains("IAudioService audio = ResolveAudioService()", plantLateFrame);
            Assert.That(plantLateFrame.IndexOf("IAudioService audio = _audioService", StringComparison.Ordinal), Is.LessThan(0));

            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", bubbleHotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", bubbleColdCache);
            StringAssert.Contains("_audioService = IsAudioServiceUsable(audioService) ? audioService : null", bubbleCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", bubbleResolve);
            StringAssert.Contains("_audioService = null", bubbleResolve);
            StringAssert.Contains("IAudioService audio = ResolveAudioService()", bubbleCollect);
            Assert.That(bubbleCollect.IndexOf("_audioService.PlayAtPoint", StringComparison.Ordinal), Is.LessThan(0));

            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", crateHotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", crateColdCache);
            StringAssert.Contains("_audioService = IsAudioServiceUsable(audioService) ? audioService : null", crateCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", crateResolve);
            StringAssert.Contains("_audioService = null", crateResolve);
            StringAssert.Contains("IAudioService audio = ResolveAudioService()", crateOpen);
            StringAssert.Contains("IAudioService audio = ResolveAudioService()", crateClose);
            Assert.That(crateOpen.IndexOf("_audioService", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(crateClose.IndexOf("_audioService", StringComparison.Ordinal), Is.LessThan(0));

            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", terminalHotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", terminalColdCache);
            StringAssert.Contains("_audioService = IsAudioServiceUsable(audioService) ? audioService : null", terminalCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", terminalResolve);
            StringAssert.Contains("_audioService = null", terminalResolve);
            StringAssert.Contains("ResolveAudioService() == null", terminalQueue);
            Assert.That(terminalQueue.IndexOf("_audioService", StringComparison.Ordinal), Is.LessThan(0));
            StringAssert.Contains("IAudioService audioService = ResolveAudioService()", terminalFlush);
            Assert.That(terminalFlush.IndexOf("IAudioService audioService = _audioService", StringComparison.Ordinal), Is.LessThan(0));

            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", actionHotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", actionColdCache);
            StringAssert.Contains("_audioService = IsAudioServiceUsable(audioService) ? audioService : null", actionCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", actionResolve);
            StringAssert.Contains("_audioService = null", actionResolve);
            StringAssert.Contains("ResolveAudioService() != null", actionCompletion);
            StringAssert.Contains("ResolveAudioService() != null", actionCancel);
            StringAssert.Contains("IAudioService audioService = ResolveAudioService()", actionFlush);
            Assert.That(actionCompletion.IndexOf("_audioService", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(actionCancel.IndexOf("_audioService", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(actionFlush.IndexOf("IAudioService audioService = _audioService", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void GameplayResourceInteractionAudioUsesOnlyUsableAudioServiceRuntime()
        {
            string scannableFragment = Read(ScannableFragmentPath);
            string climbableLadder = Read(ClimbableLadderPath);
            string bioReactor = Read(BioReactorPath);

            string fragmentHotSwap = ExtractMethodBody(scannableFragment, "public void OnGlobalRegistryServiceReplaced(");
            string fragmentColdCache = ExtractMethodBody(scannableFragment, "private void CacheRegistryServicesCold()");
            string fragmentCache = ExtractMethodBody(scannableFragment, "private void CacheAudioService(");
            string fragmentResolve = ExtractMethodBody(scannableFragment, "private IAudioService ResolveAudioService()");
            string fragmentUsable = ExtractMethodBody(scannableFragment, "private static bool IsAudioServiceUsable(");
            string fragmentLateFrame = ExtractMethodBody(scannableFragment, "public void LateFrameTick()");

            string ladderHotSwap = ExtractMethodBody(climbableLadder, "public void OnGlobalRegistryServiceReplaced(");
            string ladderColdCache = ExtractMethodBody(climbableLadder, "private void CacheRegistryServicesCold()");
            string ladderCache = ExtractMethodBody(climbableLadder, "private void CacheAudioService(");
            string ladderResolve = ExtractMethodBody(climbableLadder, "private IAudioService ResolveAudioService()");
            string ladderUsable = ExtractMethodBody(climbableLadder, "private static bool IsAudioServiceUsable(");
            string ladderMove = ExtractMethodBody(climbableLadder, "private bool RequestProceduralClimb(");

            string reactorHotSwap = ExtractMethodBody(bioReactor, "public void OnGlobalRegistryServiceReplaced(");
            string reactorColdCache = ExtractMethodBody(bioReactor, "private void RefreshColdRegistryReferences()");
            string reactorCache = ExtractMethodBody(bioReactor, "private void CacheAudioService(");
            string reactorResolve = ExtractMethodBody(bioReactor, "private IAudioService ResolveAudioService()");
            string reactorUsable = ExtractMethodBody(bioReactor, "private static bool IsAudioServiceUsable(");
            string reactorLateFrame = ExtractMethodBody(bioReactor, "public void LateFrameTick()");
            string reactorUnregisterHotSwap = ExtractMethodBody(bioReactor, "private void TryUnregisterHotSwap()");

            AssertAudioServiceUsableBody(fragmentUsable);
            AssertAudioServiceUsableBody(ladderUsable);
            AssertAudioServiceUsableBody(reactorUsable);

            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", fragmentHotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", fragmentColdCache);
            StringAssert.Contains("_audioService = IsAudioServiceUsable(audioService) ? audioService : null", fragmentCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", fragmentResolve);
            StringAssert.Contains("_audioService = null", fragmentResolve);
            StringAssert.Contains("IAudioService audio = ResolveAudioService()", fragmentLateFrame);
            Assert.That(fragmentHotSwap.IndexOf("_audioService = currentService as IAudioService", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(fragmentColdCache.IndexOf("_audioService = GlobalRegistry.Audio", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(fragmentLateFrame.IndexOf("IAudioService audio = _audioService", StringComparison.Ordinal), Is.LessThan(0));

            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", ladderHotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", ladderColdCache);
            StringAssert.Contains("_audioService = IsAudioServiceUsable(audioService) ? audioService : null", ladderCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", ladderResolve);
            StringAssert.Contains("_audioService = null", ladderResolve);
            StringAssert.Contains("IAudioService audio = ResolveAudioService()", ladderMove);
            Assert.That(ladderHotSwap.IndexOf("_audioService = currentService as IAudioService", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(ladderColdCache.IndexOf("_audioService = GlobalRegistry.Audio", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(ladderMove.IndexOf("IAudioService audio = _audioService", StringComparison.Ordinal), Is.LessThan(0));

            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", reactorHotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", reactorColdCache);
            StringAssert.Contains("_audioService = IsAudioServiceUsable(audioService) ? audioService : null", reactorCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", reactorResolve);
            StringAssert.Contains("_audioService = null", reactorResolve);
            StringAssert.Contains("IAudioService audio = ResolveAudioService()", reactorLateFrame);
            StringAssert.Contains("GlobalRegistry.TryUnregisterHotSwapListener(this);", reactorUnregisterHotSwap);
            StringAssert.DoesNotContain("GlobalRegistry.UnregisterHotSwapListener(this);", reactorUnregisterHotSwap);
            Assert.That(reactorHotSwap.IndexOf("_audioService = currentService as IAudioService", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(reactorColdCache.IndexOf("_audioService = GlobalRegistry.Audio", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(reactorLateFrame.IndexOf("IAudioService audio = _audioService", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void GameplayWorldObjectAudioUsesOnlyUsableAudioServiceRuntime()
        {
            string harvestablePlant = Read(HarvestablePlantPath);
            string harvestableOutcrop = Read(HarvestableOutcropPath);
            string hostileFlora = Read(HostileFloraPath);
            string floater = Read(FloaterPath);
            string deployableBeacon = Read(DeployableBeaconPath);

            string plantHotSwap = ExtractMethodBody(harvestablePlant, "public void OnGlobalRegistryServiceReplaced(");
            string plantColdCache = ExtractMethodBody(harvestablePlant, "private void CacheRegistryServicesCold()");
            string plantCache = ExtractMethodBody(harvestablePlant, "private void CacheAudioService(");
            string plantResolve = ExtractMethodBody(harvestablePlant, "private IAudioService ResolveAudioService()");
            string plantUsable = ExtractMethodBody(harvestablePlant, "private static bool IsAudioServiceUsable(");
            string plantFlush = ExtractMethodBody(harvestablePlant, "private void FlushQueuedSegmentPresentation()");

            string outcropHotSwap = ExtractMethodBody(harvestableOutcrop, "public void OnGlobalRegistryServiceReplaced(");
            string outcropColdCache = ExtractMethodBody(harvestableOutcrop, "private void CacheRegistryServicesCold()");
            string outcropCache = ExtractMethodBody(harvestableOutcrop, "private void CacheAudioService(");
            string outcropResolve = ExtractMethodBody(harvestableOutcrop, "private IAudioService ResolveAudioService()");
            string outcropUsable = ExtractMethodBody(harvestableOutcrop, "private static bool IsAudioServiceUsable(");
            string outcropLateFrame = ExtractMethodBody(harvestableOutcrop, "public void LateFrameTick()");

            string floraHotSwap = ExtractMethodBody(hostileFlora, "public void OnGlobalRegistryServiceReplaced(");
            string floraColdCache = ExtractMethodBody(hostileFlora, "private void CacheRegistryServicesCold()");
            string floraCache = ExtractMethodBody(hostileFlora, "private void CacheAudioService(");
            string floraResolve = ExtractMethodBody(hostileFlora, "private IAudioService ResolveAudioService()");
            string floraUsable = ExtractMethodBody(hostileFlora, "private static bool IsAudioServiceUsable(");
            string floraLateFrame = ExtractMethodBody(hostileFlora, "public void LateFrameTick()");

            string floaterHotSwap = ExtractMethodBody(floater, "public void OnGlobalRegistryServiceReplaced(");
            string floaterColdCache = ExtractMethodBody(floater, "private void CacheRegistryServicesCold()");
            string floaterCache = ExtractMethodBody(floater, "private void CacheAudioService(");
            string floaterResolve = ExtractMethodBody(floater, "private IAudioService ResolveAudioService()");
            string floaterUsable = ExtractMethodBody(floater, "private static bool IsAudioServiceUsable(");
            string floaterLateFrame = ExtractMethodBody(floater, "public void LateFrameTick()");

            string beaconHotSwap = ExtractMethodBody(deployableBeacon, "public void OnGlobalRegistryServiceReplaced(");
            string beaconColdCache = ExtractMethodBody(deployableBeacon, "private void CacheRegistryServicesCold()");
            string beaconCache = ExtractMethodBody(deployableBeacon, "private void CacheAudioService(");
            string beaconResolve = ExtractMethodBody(deployableBeacon, "private IAudioService ResolveAudioService()");
            string beaconUsable = ExtractMethodBody(deployableBeacon, "private static bool IsAudioServiceUsable(");
            string beaconLateFrame = ExtractMethodBody(deployableBeacon, "public void LateFrameTick()");

            AssertAudioServiceUsableBody(plantUsable);
            AssertAudioServiceUsableBody(outcropUsable);
            AssertAudioServiceUsableBody(floraUsable);
            AssertAudioServiceUsableBody(floaterUsable);
            AssertAudioServiceUsableBody(beaconUsable);

            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", plantHotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", plantColdCache);
            StringAssert.Contains("_audioService = IsAudioServiceUsable(audioService) ? audioService : null", plantCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", plantResolve);
            StringAssert.Contains("_audioService = null", plantResolve);
            StringAssert.Contains("IAudioService audio = ResolveAudioService()", plantFlush);
            Assert.That(plantHotSwap.IndexOf("_audioService = currentService as IAudioService", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(plantColdCache.IndexOf("_audioService = GlobalRegistry.Audio", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(plantFlush.IndexOf("IAudioService audio = _audioService", StringComparison.Ordinal), Is.LessThan(0));

            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", outcropHotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", outcropColdCache);
            StringAssert.Contains("_audioService = IsAudioServiceUsable(audioService) ? audioService : null", outcropCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", outcropResolve);
            StringAssert.Contains("_audioService = null", outcropResolve);
            StringAssert.Contains("IAudioService audio = ResolveAudioService()", outcropLateFrame);
            Assert.That(outcropHotSwap.IndexOf("_audioService = currentService as IAudioService", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(outcropColdCache.IndexOf("_audioService = GlobalRegistry.Audio", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(outcropLateFrame.IndexOf("IAudioService audio = _audioService", StringComparison.Ordinal), Is.LessThan(0));

            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", floraHotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", floraColdCache);
            StringAssert.Contains("_audioService = IsAudioServiceUsable(audioService) ? audioService : null", floraCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", floraResolve);
            StringAssert.Contains("_audioService = null", floraResolve);
            StringAssert.Contains("IAudioService audio = ResolveAudioService()", floraLateFrame);
            Assert.That(floraHotSwap.IndexOf("_audioService = currentService as IAudioService", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(floraColdCache.IndexOf("_audioService = GlobalRegistry.Audio", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(floraLateFrame.IndexOf("IAudioService audio = _audioService", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(floraLateFrame.IndexOf("audio.IsInitialized", StringComparison.Ordinal), Is.LessThan(0));

            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", floaterHotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", floaterColdCache);
            StringAssert.Contains("_audioService = IsAudioServiceUsable(audioService) ? audioService : null", floaterCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", floaterResolve);
            StringAssert.Contains("_audioService = null", floaterResolve);
            StringAssert.Contains("IAudioService audio = ResolveAudioService()", floaterLateFrame);
            Assert.That(floaterHotSwap.IndexOf("_audioService = currentService as IAudioService", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(floaterColdCache.IndexOf("_audioService = GlobalRegistry.Audio", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(floaterLateFrame.IndexOf("IAudioService audio = _audioService", StringComparison.Ordinal), Is.LessThan(0));

            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", beaconHotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", beaconColdCache);
            StringAssert.Contains("_audioService = IsAudioServiceUsable(audioService) ? audioService : null", beaconCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", beaconResolve);
            StringAssert.Contains("_audioService = null", beaconResolve);
            StringAssert.Contains("IAudioService audioService = ResolveAudioService()", beaconLateFrame);
            Assert.That(beaconHotSwap.IndexOf("_audioService = currentService as IAudioService", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(beaconColdCache.IndexOf("_audioService = GlobalRegistry.Audio", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(beaconLateFrame.IndexOf("IAudioService audioService = _audioService", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void DestructibleOrganicAudioUsesOnlyUsableAudioServiceRuntime()
        {
            string organic = Read(DestructibleOrganicManagerPath);

            string hotSwap = ExtractMethodBody(organic, "public void OnGlobalRegistryServiceReplaced(");
            string coldCache = ExtractMethodBody(organic, "private void CacheRegistryServicesCold()");
            string clearRegistry = ExtractMethodBody(organic, "private void ClearCachedRegistryServices()");
            string cache = ExtractMethodBody(organic, "private void CacheAudioService(");
            string resolve = ExtractMethodBody(organic, "private IAudioService ResolveAudioService()");
            string sinkResolve = ExtractMethodBody(organic, "private ISpatialAudioHarvestPlaybackSink ResolveHarvestAudioSink()");
            string clearAudio = ExtractMethodBody(organic, "private void ClearCachedAudioService()");
            string usable = ExtractMethodBody(organic, "private static bool IsAudioServiceUsable(");
            string dispatchHarvest = ExtractMethodBody(organic, "private void DispatchHarvestAudioTransition(");
            string dispatchSpore = ExtractMethodBody(organic, "private void DispatchSporeAcousticEvent(");
            string flushHarvest = ExtractMethodBody(organic, "private void FlushPendingHarvestAudioEvents()");

            AssertAudioServiceUsableBody(usable);
            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", hotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", coldCache);
            StringAssert.Contains("ClearCachedAudioService()", clearRegistry);
            StringAssert.Contains("if (!IsAudioServiceUsable(audioService))", cache);
            StringAssert.Contains("_audioService = audioService", cache);
            StringAssert.Contains("_harvestAudioSink = _audioService as ISpatialAudioHarvestPlaybackSink", cache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", resolve);
            StringAssert.Contains("ClearCachedAudioService()", resolve);
            StringAssert.Contains("ResolveAudioService() != null ? _harvestAudioSink : null", sinkResolve);
            StringAssert.Contains("_audioService = null", clearAudio);
            StringAssert.Contains("_harvestAudioSink = null", clearAudio);
            StringAssert.Contains("ResolveHarvestAudioSink() != null", dispatchHarvest);
            StringAssert.Contains("ISpatialAudioHarvestPlaybackSink harvestAudioSink = ResolveHarvestAudioSink()", dispatchSpore);
            StringAssert.Contains("IAudioService audioService = ResolveAudioService()", dispatchSpore);
            StringAssert.Contains("audioService.PlayAtPoint", dispatchSpore);
            StringAssert.Contains("ISpatialAudioHarvestPlaybackSink harvestAudioSink = ResolveHarvestAudioSink()", flushHarvest);
            StringAssert.Contains("IAudioService audioService = ResolveAudioService()", flushHarvest);
            StringAssert.Contains("audioService.PlayAtPoint", flushHarvest);
            Assert.That(dispatchSpore.IndexOf("_audioService?.PlayAtPoint", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(flushHarvest.IndexOf("_audioService?.PlayAtPoint", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(dispatchSpore.IndexOf("ISpatialAudioHarvestPlaybackSink harvestAudioSink = _harvestAudioSink", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(flushHarvest.IndexOf("ISpatialAudioHarvestPlaybackSink harvestAudioSink = _harvestAudioSink", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void EnvironmentFixtureAudioUsesOnlyUsableAudioServiceRuntime()
        {
            string airlock = Read(BaseAirlockPath);
            string charger = Read(BatteryChargerPath);
            string door = Read(SealedDoorPath);

            string airlockHotSwap = ExtractMethodBody(airlock, "public void OnGlobalRegistryServiceReplaced(");
            string airlockColdCache = ExtractMethodBody(airlock, "private void CacheRegistryServicesCold()");
            string airlockClear = ExtractMethodBody(airlock, "private void ClearCachedRegistryServices()");
            string airlockCache = ExtractMethodBody(airlock, "private void CacheAudioService(");
            string airlockResolve = ExtractMethodBody(airlock, "private IAudioService ResolveAudioService()");
            string airlockUsable = ExtractMethodBody(airlock, "private static bool IsAudioServiceUsable(");
            string airlockStart = ExtractMethodBody(airlock, "private void StartCycle(");
            string airlockFlush = ExtractMethodBody(airlock, "private void FlushAirlockAudioPresentation()");

            string chargerHotSwap = ExtractMethodBody(charger, "public void OnGlobalRegistryServiceReplaced(");
            string chargerColdCache = ExtractMethodBody(charger, "private void CacheRegistryServicesCold()");
            string chargerCache = ExtractMethodBody(charger, "private void CacheAudioService(");
            string chargerResolve = ExtractMethodBody(charger, "private IAudioService ResolveAudioService()");
            string chargerUsable = ExtractMethodBody(charger, "private static bool IsAudioServiceUsable(");
            string chargerFlush = ExtractMethodBody(charger, "private void FlushQueuedChargerAudio()");

            string doorHotSwap = ExtractMethodBody(door, "public void OnGlobalRegistryServiceReplaced(");
            string doorColdCache = ExtractMethodBody(door, "private void CacheColdDependencies()");
            string doorCache = ExtractMethodBody(door, "private void CacheAudioService(");
            string doorResolve = ExtractMethodBody(door, "private IAudioService ResolveAudioService()");
            string doorUsable = ExtractMethodBody(door, "private static bool IsAudioServiceUsable(");
            string doorFlush = ExtractMethodBody(door, "private void FlushPresentation()");

            AssertAudioServiceUsableBody(airlockUsable);
            AssertAudioServiceUsableBody(chargerUsable);
            AssertAudioServiceUsableBody(doorUsable);

            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", airlockHotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", airlockColdCache);
            StringAssert.Contains("ClearCachedAudioService()", airlockClear);
            StringAssert.Contains("_cachedAudioService = IsAudioServiceUsable(audioService) ? audioService : null", airlockCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", airlockResolve);
            StringAssert.Contains("ClearCachedAudioService()", airlockResolve);
            StringAssert.Contains("IAudioService audio = ResolveAudioService()", airlockStart);
            StringAssert.Contains("IAudioService audio = ResolveAudioService()", airlockFlush);
            Assert.That(airlockStart.IndexOf("IAudioService audio = _cachedAudioService", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(airlockFlush.IndexOf("IAudioService audio = _cachedAudioService", StringComparison.Ordinal), Is.LessThan(0));

            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", chargerHotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", chargerColdCache);
            StringAssert.Contains("_cachedAudioService = IsAudioServiceUsable(audioService) ? audioService : null", chargerCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", chargerResolve);
            StringAssert.Contains("_cachedAudioService = null", chargerResolve);
            StringAssert.Contains("IAudioService audio = ResolveAudioService()", chargerFlush);
            Assert.That(chargerFlush.IndexOf("IAudioService audio = _cachedAudioService", StringComparison.Ordinal), Is.LessThan(0));

            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", doorHotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", doorColdCache);
            StringAssert.Contains("_cachedAudioService = IsAudioServiceUsable(audioService) ? audioService : null", doorCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", doorResolve);
            StringAssert.Contains("_cachedAudioService = null", doorResolve);
            StringAssert.Contains("IAudioService audio = ResolveAudioService()", doorFlush);
            Assert.That(doorFlush.IndexOf("IAudioService audio = _cachedAudioService", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void ConstructionGraphAudioUsesOnlyUsableAudioServiceRuntime()
        {
            string constructionManager = Read(ConstructionManagerPath);
            string habitatGraph = Read(HabitatGraphManagerPath);

            string hotSwap = ExtractMethodBody(constructionManager, "public void OnGlobalRegistryServiceReplaced(");
            string coldCache = ExtractMethodBody(constructionManager, "private void CacheRegistryServicesCold()");
            string bind = ExtractMethodBody(constructionManager, "private void BindConstructionRuntimeServices()");
            string clearCached = ExtractMethodBody(constructionManager, "private void ClearCachedRegistryServices()");
            string cache = ExtractMethodBody(constructionManager, "private void CacheAudioService(");
            string resolve = ExtractMethodBody(constructionManager, "private IAudioService ResolveAudioService()");
            string clearAudio = ExtractMethodBody(constructionManager, "private void ClearCachedAudioService()");
            string usable = ExtractMethodBody(constructionManager, "private static bool IsAudioServiceUsable(");
            string graphDispose = ExtractMethodBody(habitatGraph, "public void Dispose()");
            string graphSetRuntime = ExtractMethodBody(habitatGraph, "internal void SetRuntimeServices(");
            string graphSetAudio = ExtractMethodBody(habitatGraph, "internal void SetAudioService(");
            string graphPublish = ExtractMethodBody(habitatGraph, "private void PublishHullStressSignal(");
            string graphCache = ExtractMethodBody(habitatGraph, "private void CacheAudioService(");
            string graphResolve = ExtractMethodBody(habitatGraph, "private IAudioService ResolveAudioService()");
            string graphClearAudio = ExtractMethodBody(habitatGraph, "private void ClearCachedAudioService()");
            string graphUsable = ExtractMethodBody(habitatGraph, "private static bool IsAudioServiceUsable(");

            AssertAudioServiceUsableBody(usable);
            AssertAudioServiceUsableBody(graphUsable);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", coldCache);
            StringAssert.Contains("ResolveAudioService(),", bind);
            StringAssert.Contains("ClearCachedAudioService()", clearCached);
            StringAssert.Contains("_cachedAudioService = IsAudioServiceUsable(audioService) ? audioService : null", cache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", resolve);
            StringAssert.Contains("ClearCachedAudioService()", resolve);
            StringAssert.Contains("_cachedAudioService = null", clearAudio);
            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", hotSwap);
            StringAssert.Contains("_habitatGraphManager?.SetAudioService(ResolveAudioService())", hotSwap);
            Assert.That(coldCache.IndexOf("_cachedAudioService = GlobalRegistry.Audio", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(hotSwap.IndexOf("_cachedAudioService = currentService as IAudioService", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(bind.IndexOf("_cachedAudioService,", StringComparison.Ordinal), Is.LessThan(0));
            StringAssert.Contains("ClearCachedAudioService()", graphDispose);
            StringAssert.Contains("CacheAudioService(audioService)", graphSetRuntime);
            StringAssert.Contains("CacheAudioService(audioService)", graphSetAudio);
            StringAssert.Contains("IAudioService audioService = ResolveAudioService()", graphPublish);
            StringAssert.Contains("_audioService = IsAudioServiceUsable(audioService) ? audioService : null", graphCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", graphResolve);
            StringAssert.Contains("ClearCachedAudioService()", graphResolve);
            StringAssert.Contains("_audioService = null", graphClearAudio);
            Assert.That(graphSetRuntime.IndexOf("_audioService = audioService", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(graphSetAudio.IndexOf("_audioService = audioService", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(graphPublish.IndexOf("GetCachedAudioService()", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void CraftingStationAudioUsesOnlyUsableAudioServiceRuntime()
        {
            string fabricator = Read(FabricatorPath);

            string hotSwap = ExtractMethodBody(fabricator, "public void OnGlobalRegistryServiceReplaced(");
            string coldCache = ExtractMethodBody(fabricator, "private void CacheRegistryServicesCold()");
            string cache = ExtractMethodBody(fabricator, "private void CacheAudioService(");
            string resolve = ExtractMethodBody(fabricator, "private IAudioService ResolveAudioService()");
            string clear = ExtractMethodBody(fabricator, "private void ClearCachedAudioService()");
            string usable = ExtractMethodBody(fabricator, "private static bool IsAudioServiceUsable(");
            string onDisable = ExtractMethodBody(fabricator, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(fabricator, "private void OnDestroy()");
            string flush = ExtractMethodBody(fabricator, "private void FlushPendingAudio()");

            AssertAudioServiceUsableBody(usable);
            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", hotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", coldCache);
            StringAssert.Contains("_audioService = IsAudioServiceUsable(audioService) ? audioService : null", cache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", resolve);
            StringAssert.Contains("ClearCachedAudioService()", resolve);
            StringAssert.Contains("_audioService = null", clear);
            StringAssert.Contains("ClearCachedAudioService()", onDisable);
            StringAssert.Contains("ClearCachedAudioService()", onDestroy);
            StringAssert.Contains("IAudioService audioService = ResolveAudioService()", flush);
            Assert.That(hotSwap.IndexOf("_audioService = currentService as IAudioService", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(coldCache.IndexOf("_audioService = GlobalRegistry.Audio", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(flush.IndexOf("_audioService?.PlayAtPoint", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void PlayerEquipmentPdaAudioUsesOnlyUsableAudioServiceRuntime()
        {
            string flashlight = Read(PlayerFlashlightPath);
            string pda = Read(PlayerPdaPath);
            string inventoryTab = Read(PDAInventoryTabPath);
            string mapTab = Read(PDAMapTabPath);

            string flashlightHotSwap = ExtractMethodBody(flashlight, "private void ApplyRegistryServiceRebind(");
            string flashlightColdCache = ExtractMethodBody(flashlight, "private void CachePlayerRuntimeContextCold()");
            string flashlightCache = ExtractMethodBody(flashlight, "private void CacheAudioService(");
            string flashlightResolve = ExtractMethodBody(flashlight, "private IAudioService ResolveAudioService()");
            string flashlightUsable = ExtractMethodBody(flashlight, "private static bool IsAudioServiceUsable(");
            string flashlightFlush = ExtractMethodBody(flashlight, "private void FlushPendingAudio()");

            string pdaHotSwap = ExtractMethodBody(pda, "public void OnGlobalRegistryServiceReplaced(");
            string pdaColdCache = ExtractMethodBody(pda, "private void RefreshColdRegistryReferences()");
            string pdaCache = ExtractMethodBody(pda, "private void CacheAudioService(");
            string pdaResolve = ExtractMethodBody(pda, "private IAudioService ResolveAudioService()");
            string pdaUsable = ExtractMethodBody(pda, "private static bool IsAudioServiceUsable(");
            string pdaFlush = ExtractMethodBody(pda, "private void FlushPendingSounds()");

            string inventoryHotSwap = ExtractMethodBody(inventoryTab, "public void OnGlobalRegistryServiceReplaced(");
            string inventoryColdCache = ExtractMethodBody(inventoryTab, "private void CacheRegistryServicesCold()");
            string inventoryCache = ExtractMethodBody(inventoryTab, "private void CacheAudioService(");
            string inventoryResolve = ExtractMethodBody(inventoryTab, "private IAudioService ResolveAudioService()");
            string inventoryUsable = ExtractMethodBody(inventoryTab, "private static bool IsAudioServiceUsable(");
            string inventoryPlay = ExtractMethodBody(inventoryTab, "private void PlayUISound(");

            string mapHotSwap = ExtractMethodBody(mapTab, "public void OnGlobalRegistryServiceReplaced(");
            string mapColdCache = ExtractMethodBody(mapTab, "private void CacheRegistryServicesCold()");
            string mapCache = ExtractMethodBody(mapTab, "private void CacheAudioService(");
            string mapResolve = ExtractMethodBody(mapTab, "private IAudioService ResolveAudioService()");
            string mapUsable = ExtractMethodBody(mapTab, "private static bool IsAudioServiceUsable(");
            string mapThreatPings = ExtractMethodBody(mapTab, "private void RefreshThreatPings()");

            AssertAudioServiceUsableBody(flashlightUsable);
            AssertAudioServiceUsableBody(pdaUsable);
            AssertAudioServiceUsableBody(inventoryUsable);
            AssertAudioServiceUsableBody(mapUsable);

            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", flashlightHotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", flashlightColdCache);
            StringAssert.Contains("_audioService = IsAudioServiceUsable(audioService) ? audioService : null", flashlightCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", flashlightResolve);
            StringAssert.Contains("_audioService = null", flashlightResolve);
            StringAssert.Contains("IAudioService audioService = ResolveAudioService()", flashlightFlush);
            Assert.That(flashlightFlush.IndexOf("IAudioService audioService = _audioService", StringComparison.Ordinal), Is.LessThan(0));

            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", pdaHotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", pdaColdCache);
            StringAssert.Contains("_audioService = IsAudioServiceUsable(audioService) ? audioService : null", pdaCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", pdaResolve);
            StringAssert.Contains("_audioService = null", pdaResolve);
            StringAssert.Contains("IAudioService audioManager = ResolveAudioService()", pdaFlush);
            Assert.That(pdaFlush.IndexOf("IAudioService audioManager = _audioService", StringComparison.Ordinal), Is.LessThan(0));

            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", inventoryHotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", inventoryColdCache);
            StringAssert.Contains("_audioService = IsAudioServiceUsable(audioService) ? audioService : null", inventoryCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", inventoryResolve);
            StringAssert.Contains("_audioService = null", inventoryResolve);
            StringAssert.Contains("IAudioService audio = ResolveAudioService()", inventoryPlay);
            Assert.That(inventoryPlay.IndexOf("IAudioService audio = _audioService", StringComparison.Ordinal), Is.LessThan(0));

            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", mapHotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", mapColdCache);
            StringAssert.Contains("_audioService = IsAudioServiceUsable(audioService) ? audioService : null", mapCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", mapResolve);
            StringAssert.Contains("_audioService = null", mapResolve);
            StringAssert.Contains("IAudioService audio = ResolveAudioService()", mapThreatPings);
            Assert.That(mapThreatPings.IndexOf("_audioService", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void PlayerInventoryAudioUsesOnlyUsableAudioServiceRuntime()
        {
            string playerInventory = Read(PlayerInventoryPath);

            string hotSwap = ExtractMethodBody(playerInventory, "public void OnGlobalRegistryServiceReplaced(");
            string coldCache = ExtractMethodBody(playerInventory, "private void CacheRegistryServicesCold()");
            string cache = ExtractMethodBody(playerInventory, "private void CacheAudioService(");
            string resolve = ExtractMethodBody(playerInventory, "private IAudioService ResolveAudioService()");
            string usable = ExtractMethodBody(playerInventory, "private static bool IsAudioServiceUsable(");
            string runaway = ExtractMethodBody(playerInventory, "private void DispatchInventoryThermalRunaway(");

            AssertAudioServiceUsableBody(usable);
            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", hotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", coldCache);
            StringAssert.Contains("_cachedAudioService = IsAudioServiceUsable(audioService) ? audioService : null", cache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", resolve);
            StringAssert.Contains("_cachedAudioService = null", resolve);
            StringAssert.Contains("IAudioService audioService = ResolveAudioService()", runaway);
            StringAssert.Contains("audioService is ISpatialAudioInventoryRunawaySink inventoryAudio", runaway);
            Assert.That(hotSwap.IndexOf("_cachedAudioService = currentService as IAudioService", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(coldCache.IndexOf("_cachedAudioService = GlobalRegistry.Audio", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(runaway.IndexOf("_cachedAudioService is ISpatialAudioInventoryRunawaySink", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void PlayerMovementPresentationAudioUsesOnlyUsableAudioServiceRuntime()
        {
            string playerMovement = Read(HectonPlayerMovementPath);

            string inject = ExtractMethodBody(playerMovement, "public void OnDependencyInject()");
            string hotSwap = ExtractMethodBody(playerMovement, "public void OnGlobalRegistryServiceReplaced(");
            string cache = ExtractMethodBody(playerMovement, "private void CacheAudioService(");
            string resolve = ExtractMethodBody(playerMovement, "private IAudioService ResolveAudioService()");
            string usable = ExtractMethodBody(playerMovement, "private static bool IsAudioServiceUsable(");
            string flush = ExtractMethodBody(playerMovement, "private void FlushPresentationAudioEvents()");

            AssertAudioServiceUsableBody(usable);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", inject);
            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", hotSwap);
            StringAssert.Contains("_audioService = IsAudioServiceUsable(audioService) ? audioService : null", cache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", resolve);
            StringAssert.Contains("_audioService = null", resolve);
            StringAssert.Contains("IAudioService audioManager = ResolveAudioService()", flush);
            StringAssert.Contains("audioManager.PlayStatic2D", flush);
            StringAssert.Contains("audioManager.PlayAtPoint", flush);
            Assert.That(inject.IndexOf("_audioService = GlobalRegistry.Audio", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(hotSwap.IndexOf("_audioService = currentService as IAudioService", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(flush.IndexOf("IAudioService audioManager = _audioService", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void MountablePlayerTransportAudioUsesOnlyUsableAudioServiceRuntime()
        {
            string transport = Read(MountablePlayerTransportPath);

            string hotSwap = ExtractMethodBody(transport, "public void OnGlobalRegistryServiceReplaced(");
            string refresh = ExtractMethodBody(transport, "private void RefreshCachedRegistryServices()");
            string cache = ExtractMethodBody(transport, "private void CacheAudioService(");
            string resolve = ExtractMethodBody(transport, "private IAudioService ResolveAudioService()");
            string usable = ExtractMethodBody(transport, "private static bool IsAudioServiceUsable(");
            string flush = ExtractMethodBody(transport, "private void FlushQueuedTransportAudio()");

            AssertAudioServiceUsableBody(usable);
            StringAssert.Contains("serviceSlot != GlobalRegistryServiceSlot.Audio", hotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", refresh);
            StringAssert.Contains("_cachedAudioService = IsAudioServiceUsable(audioService) ? audioService : null", cache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", resolve);
            StringAssert.Contains("_cachedAudioService = null", resolve);
            StringAssert.Contains("IAudioService audioService = ResolveAudioService()", flush);
            StringAssert.Contains("audioService.PlayAtPoint", flush);
            Assert.That(refresh.IndexOf("_cachedAudioService = GlobalRegistry.Audio", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(flush.IndexOf("_cachedAudioService.PlayAtPoint", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void DropPodAudioUsesOnlyUsableAudioServiceRuntime()
        {
            string seat = Read(DropPodSeatControllerPath);
            string toggle = Read(DropPodDashboardToggleSwitchPath);
            string airlock = Read(DropPodAirlockControllerPath);

            string seatHotSwap = ExtractMethodBody(seat, "public void OnGlobalRegistryServiceReplaced(");
            string seatColdCache = ExtractMethodBody(seat, "private void CacheColdReferences()");
            string seatCache = ExtractMethodBody(seat, "private void CacheAudioService(");
            string seatResolve = ExtractMethodBody(seat, "private IAudioService ResolveAudioService()");
            string seatUsable = ExtractMethodBody(seat, "private static bool IsAudioServiceUsable(");
            string seatQueue = ExtractMethodBody(seat, "private void QueueAudio(");

            string toggleHotSwap = ExtractMethodBody(toggle, "public void OnGlobalRegistryServiceReplaced(");
            string toggleColdCache = ExtractMethodBody(toggle, "private void CacheColdReferences()");
            string toggleCache = ExtractMethodBody(toggle, "private void CacheAudioService(");
            string toggleResolve = ExtractMethodBody(toggle, "private IAudioService ResolveAudioService()");
            string toggleUsable = ExtractMethodBody(toggle, "private static bool IsAudioServiceUsable(");
            string toggleQueue = ExtractMethodBody(toggle, "private void QueueAudio(");

            string airlockHotSwap = ExtractMethodBody(airlock, "public void OnGlobalRegistryServiceReplaced(");
            string airlockColdCache = ExtractMethodBody(airlock, "private void CacheColdReferences()");
            string airlockCache = ExtractMethodBody(airlock, "private void CacheAudioService(");
            string airlockResolve = ExtractMethodBody(airlock, "private IAudioService ResolveAudioService()");
            string airlockUsable = ExtractMethodBody(airlock, "private static bool IsAudioServiceUsable(");
            string airlockQueue = ExtractMethodBody(airlock, "private void QueueAudio(");

            AssertAudioServiceUsableBody(seatUsable);
            AssertAudioServiceUsableBody(toggleUsable);
            AssertAudioServiceUsableBody(airlockUsable);

            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", seatHotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", seatColdCache);
            StringAssert.Contains("_audioService = IsAudioServiceUsable(audioService) ? audioService : null", seatCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", seatResolve);
            StringAssert.Contains("_audioService = null", seatResolve);
            StringAssert.Contains("IAudioService audio = ResolveAudioService()", seatQueue);
            StringAssert.Contains("audio.QueueAudioEvent(in audioEvent)", seatQueue);
            Assert.That(seatHotSwap.IndexOf("_audioService = currentService as IAudioService", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(seatColdCache.IndexOf("_audioService = GlobalRegistry.Audio", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(seatQueue.IndexOf("IAudioService audio = _audioService", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(seatQueue.IndexOf("audio.IsInitialized", StringComparison.Ordinal), Is.LessThan(0));

            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", toggleHotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", toggleColdCache);
            StringAssert.Contains("_audioService = IsAudioServiceUsable(audioService) ? audioService : null", toggleCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", toggleResolve);
            StringAssert.Contains("_audioService = null", toggleResolve);
            StringAssert.Contains("IAudioService audio = ResolveAudioService()", toggleQueue);
            StringAssert.Contains("audio.QueueAudioEvent(in audioEvent)", toggleQueue);
            Assert.That(toggleHotSwap.IndexOf("_audioService = currentService as IAudioService", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(toggleColdCache.IndexOf("_audioService = GlobalRegistry.Audio", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(toggleQueue.IndexOf("IAudioService audio = _audioService", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(toggleQueue.IndexOf("audio.IsInitialized", StringComparison.Ordinal), Is.LessThan(0));

            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", airlockHotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", airlockColdCache);
            StringAssert.Contains("_audioService = IsAudioServiceUsable(audioService) ? audioService : null", airlockCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", airlockResolve);
            StringAssert.Contains("_audioService = null", airlockResolve);
            StringAssert.Contains("IAudioService audio = ResolveAudioService()", airlockQueue);
            StringAssert.Contains("audio.QueueAudioEvent(in audioEvent)", airlockQueue);
            Assert.That(airlockHotSwap.IndexOf("_audioService = currentService as IAudioService", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(airlockColdCache.IndexOf("_audioService = GlobalRegistry.Audio", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(airlockQueue.IndexOf("IAudioService audio = _audioService", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(airlockQueue.IndexOf("audio.IsInitialized", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void ModCommandDispatcherAcousticPingUsesOnlyUsableAudioServiceRuntime()
        {
            string dispatcher = Read(ModCommandDispatcherPath);

            string hotSwap = ExtractMethodBody(dispatcher, "internal static void OnGlobalRegistryServiceReplaced(");
            string coldCache = ExtractMethodBody(dispatcher, "internal static void BindRegistryServicesCold()");
            string cache = ExtractMethodBody(dispatcher, "private static void CacheAudioService(");
            string resolve = ExtractMethodBody(dispatcher, "private static IAudioService ResolveAudioService()");
            string usable = ExtractMethodBody(dispatcher, "private static bool IsAudioServiceUsable(");
            string ping = ExtractMethodBody(dispatcher, "private static void ExecuteModAcousticPing(");

            AssertAudioServiceUsableBody(usable);
            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", hotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", coldCache);
            StringAssert.Contains("_audioService = IsAudioServiceUsable(audioService) ? audioService : null", cache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", resolve);
            StringAssert.Contains("_audioService = null", resolve);
            StringAssert.Contains("IAudioService audioManager = ResolveAudioService()", ping);
            StringAssert.Contains("audioManager.TryEmitModAcousticPing", ping);
            Assert.That(hotSwap.IndexOf("_audioService = currentService as IAudioService", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(coldCache.IndexOf("_audioService = GlobalRegistry.Audio", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(ping.IndexOf("IAudioService audioManager = _audioService", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void SharedRuntimeAudioCachesOnlyUsableAudioServiceRuntime()
        {
            string baseModule = Read(BaseModulePath);
            string repairTool = Read(RepairToolPath);
            string suitHud = Read(SuitHudOverlayPath);
            string ballast = Read(SubmarineAutoLevelBallastControllerPath);

            string baseColdCache = ExtractMethodBody(baseModule, "private void CacheRegistryServicesCold()");
            string baseHotSwap = ExtractMethodBody(baseModule, "public void OnGlobalRegistryServiceReplaced(");
            string baseCache = ExtractMethodBody(baseModule, "private void CacheAudioService(");
            string baseResolve = ExtractMethodBody(baseModule, "private Hecton8.Core.IAudioService ResolveAudioService()");
            string baseRouteResolve = ExtractMethodBody(baseModule, "private ISpatialAudioSfxMixerRouteReadModel ResolveSpatialAudioSfxRoute()");
            string baseClear = ExtractMethodBody(baseModule, "private void ClearCachedAudioService()");
            string baseUsable = ExtractMethodBody(baseModule, "private static bool IsAudioServiceUsable(");
            string baseRoute = ExtractMethodBody(baseModule, "private void TryRouteAudioSourceToSfxGroup(");
            string baseFlush = ExtractMethodBody(baseModule, "private void FlushPendingSpatialSfx()");

            string repairHotSwap = ExtractMethodBody(repairTool, "protected override void OnToolRegistryServiceReplaced(");
            string repairColdCache = ExtractMethodBody(repairTool, "private void CacheRepairAudioCold()");
            string repairCache = ExtractMethodBody(repairTool, "private void CacheRepairAudioMixerGroup(");
            string repairUsable = ExtractMethodBody(repairTool, "private static bool IsAudioServiceUsable(");

            string suitColdCache = ExtractMethodBody(suitHud, "private bool CacheRuntimeDependenciesCold()");
            string suitHotSwap = ExtractMethodBody(suitHud, "public void OnGlobalRegistryServiceReplaced(");
            string suitCache = ExtractMethodBody(suitHud, "private void CacheAudioService(");
            string suitResolve = ExtractMethodBody(suitHud, "private Hecton8.Core.IAudioService ResolveAudioService()");
            string suitUsable = ExtractMethodBody(suitHud, "private static bool IsAudioServiceUsable(");
            string suitRadar = ExtractMethodBody(suitHud, "private void RefreshAcousticRadarPayload()");

            string ballastHotSwap = ExtractMethodBody(ballast, "public void OnGlobalRegistryServiceReplaced(");
            string ballastRegister = ExtractMethodBody(ballast, "private void RegisterRuntime()");
            string ballastUnregister = ExtractMethodBody(ballast, "private void UnregisterRuntime()");
            string ballastCache = ExtractMethodBody(ballast, "private void CacheAudioService(");
            string ballastClear = ExtractMethodBody(ballast, "private void ClearCachedAudioService()");
            string ballastUsable = ExtractMethodBody(ballast, "private static bool IsAudioServiceUsable(");

            AssertAudioServiceUsableBody(baseUsable);
            AssertAudioServiceUsableBody(repairUsable);
            AssertAudioServiceUsableBody(suitUsable);
            AssertAudioServiceUsableBody(ballastUsable);

            StringAssert.Contains("CacheAudioService(Hecton8.Core.GlobalRegistry.Audio)", baseColdCache);
            StringAssert.Contains("CacheAudioService(currentService as Hecton8.Core.IAudioService)", baseHotSwap);
            StringAssert.Contains("if (!IsAudioServiceUsable(audioService))", baseCache);
            StringAssert.Contains("ClearCachedAudioService()", baseCache);
            StringAssert.Contains("_cachedAudioService = audioService", baseCache);
            StringAssert.Contains("_cachedSpatialAudioSfxRoute = audioService as ISpatialAudioSfxMixerRouteReadModel", baseCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", baseResolve);
            StringAssert.Contains("ClearCachedAudioService()", baseResolve);
            StringAssert.Contains("ResolveAudioService() != null ? _cachedSpatialAudioSfxRoute : null", baseRouteResolve);
            StringAssert.Contains("_cachedAudioService = null", baseClear);
            StringAssert.Contains("_cachedSpatialAudioSfxRoute = null", baseClear);
            StringAssert.Contains("ISpatialAudioSfxMixerRouteReadModel spatialAudioRoute = ResolveSpatialAudioSfxRoute()", baseRoute);
            StringAssert.Contains("Hecton8.Core.IAudioService sam = ResolveAudioService()", baseFlush);
            Assert.That(baseColdCache.IndexOf("_cachedAudioService = Hecton8.Core.GlobalRegistry.Audio", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(baseHotSwap.IndexOf("_cachedAudioService = currentService as Hecton8.Core.IAudioService", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(baseFlush.IndexOf("Hecton8.Core.IAudioService sam = _cachedAudioService", StringComparison.Ordinal), Is.LessThan(0));

            StringAssert.Contains("CacheRepairAudioMixerGroup(currentService as IAudioService)", repairHotSwap);
            StringAssert.Contains("CacheRepairAudioMixerGroup(GlobalRegistry.Audio)", repairColdCache);
            StringAssert.Contains("_cachedRepairAudioMixerGroup = IsAudioServiceUsable(audioService)", repairCache);
            Assert.That(repairHotSwap.IndexOf("_cachedRepairAudioMixerGroup = currentService is IAudioService audioService", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(repairColdCache.IndexOf("_cachedRepairAudioMixerGroup = audioService != null", StringComparison.Ordinal), Is.LessThan(0));

            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", suitColdCache);
            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", suitHotSwap);
            StringAssert.Contains("_spatialAudioManager = IsAudioServiceUsable(audioService) ? audioService : null", suitCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", suitResolve);
            StringAssert.Contains("_spatialAudioManager = null", suitResolve);
            StringAssert.Contains("Hecton8.Core.IAudioService audioManager = ResolveAudioService()", suitRadar);
            StringAssert.Contains("audioManager.TryGetAcousticRadarPayload", suitRadar);
            StringAssert.Contains("audioManager = ResolveAudioService()", suitRadar);
            StringAssert.Contains("audioManager.TryUploadAcousticRadarPayload", suitRadar);
            Assert.That(suitColdCache.IndexOf("_spatialAudioManager = GlobalRegistry.Audio", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(suitHotSwap.IndexOf("_spatialAudioManager = currentService as IAudioService", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(suitRadar.IndexOf("_spatialAudioManager.TryGetAcousticRadarPayload", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(suitRadar.IndexOf("_spatialAudioManager.TryUploadAcousticRadarPayload", StringComparison.Ordinal), Is.LessThan(0));

            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", ballastHotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", ballastRegister);
            StringAssert.Contains("_audio = IsAudioServiceUsable(audioService) ? audioService : null", ballastCache);
            StringAssert.Contains("_audio = null", ballastClear);
            StringAssert.Contains("GlobalRegistry.TryUnregisterHotSwapListener(this);", ballastUnregister);
            StringAssert.DoesNotContain("GlobalRegistry.UnregisterHotSwapListener(this);", ballastUnregister);
            Assert.That(ballastHotSwap.IndexOf("_audio = currentService as IAudioService", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(ballastRegister.IndexOf("_audio = GlobalRegistry.Audio", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void AudioDerivedRuntimeInterfacesUseUsableObjectResolvers()
        {
            string signalBeacon = Read(SignalBeaconPath);
            string narrativeDirector = Read(HectonNarrativeDirectorPath);
            string trauma = Read(TraumaDispatcherPath);
            string randomEvents = Read(RandomEventSystemPath);
            string eclipse = Read(EclipseGameplaySystemPath);
            string acousticRadar = Read(AcousticRadarSphereRendererPath);
            string sonarCompass = Read(SonarHoloCompassPath);
            string sceneRuntime = Read(SceneRuntimeServicePath);

            string beaconHotSwap = ExtractMethodBody(signalBeacon, "public void OnGlobalRegistryServiceReplaced(");
            string beaconColdCache = ExtractMethodBody(signalBeacon, "private void CacheRegistryServicesCold()");
            string beaconCache = ExtractMethodBody(signalBeacon, "private void CacheSpatialAudio(");
            string beaconResolve = ExtractMethodBody(signalBeacon, "private ISpatialAudioListenerCaveReadModel ResolveSpatialAudio()");
            string beaconUsable = ExtractMethodBody(signalBeacon, "private static bool IsAudioRuntimeObjectUsable(");
            string beaconCave = ExtractMethodBody(signalBeacon, "private float ResolveCaveErrorMultiplier()");

            string narrativeHotSwap = ExtractMethodBody(narrativeDirector, "public void OnGlobalRegistryServiceReplaced(");
            string narrativeColdCache = ExtractMethodBody(narrativeDirector, "private void CacheRegistryReadModelsCold()");
            string narrativeCache = ExtractMethodBody(narrativeDirector, "private void CacheNarrativeAudioSink(");
            string narrativeUsable = ExtractMethodBody(narrativeDirector, "private static bool IsAudioRuntimeObjectUsable(");

            string traumaHotSwap = ExtractMethodBody(trauma, "public void OnGlobalRegistryServiceReplaced(");
            string traumaColdCache = ExtractMethodBody(trauma, "private void CacheRegistryServicesCold()");
            string traumaCache = ExtractMethodBody(trauma, "private void CacheSpatialAudioSink(");
            string traumaResolve = ExtractMethodBody(trauma, "private ISpatialAudioEnvironmentModulationSink ResolveSpatialAudioSink()");
            string traumaUsable = ExtractMethodBody(trauma, "private static bool IsAudioRuntimeObjectUsable(");
            string traumaFlush = ExtractMethodBody(trauma, "private void FlushParasiteAudioLoad()");

            string randomHotSwap = ExtractMethodBody(randomEvents, "public void OnGlobalRegistryServiceReplaced(");
            string randomColdCache = ExtractMethodBody(randomEvents, "private void CacheRegistryServicesCold()");
            string randomCache = ExtractMethodBody(randomEvents, "private void CacheMeteorShowerAudioSink(");
            string randomResolve = ExtractMethodBody(randomEvents, "private IMeteorShowerAudioSink ResolveMeteorShowerAudioSink()");
            string randomUsable = ExtractMethodBody(randomEvents, "private static bool IsAudioRuntimeObjectUsable(");
            string randomMeteorBoom = ExtractMethodBody(randomEvents, "private void TryPublishMeteorBoom(");
            string randomWaterImpact = ExtractMethodBody(randomEvents, "private void TryPublishMeteorWaterImpact(");
            string randomSeaLevel = ExtractMethodBody(randomEvents, "private static float ResolveCurrentSeaLevelY()");
            string randomWaterBoom = ExtractMethodBody(randomEvents, "private void TickMeteorWaterBoomDelay(");

            string eclipseOnEnable = ExtractMethodBody(eclipse, "private void OnEnable()");
            string eclipseHotSwap = ExtractMethodBody(eclipse, "public void OnGlobalRegistryServiceReplaced(");
            string eclipsePublish = ExtractMethodBody(eclipse, "private void PublishEclipseAcousticPitchShift(");
            string eclipseCache = ExtractMethodBody(eclipse, "private void CacheSpatialAudioSink(");
            string eclipseResolve = ExtractMethodBody(eclipse, "private ISpatialAudioEnvironmentModulationSink ResolveSpatialAudioSink()");
            string eclipseUsable = ExtractMethodBody(eclipse, "private static bool IsAudioRuntimeObjectUsable(");

            string radarHotSwap = ExtractMethodBody(acousticRadar, "public void OnGlobalRegistryServiceReplaced(");
            string radarColdCache = ExtractMethodBody(acousticRadar, "private void CacheRegistryServicesCold()");
            string radarCache = ExtractMethodBody(acousticRadar, "private void CacheImpactEmitterReadModel(");
            string radarResolve = ExtractMethodBody(acousticRadar, "private ISpatialAudioImpactEmitterReadModel ResolveImpactEmitterReadModel()");
            string radarUsable = ExtractMethodBody(acousticRadar, "private static bool IsAudioRuntimeObjectUsable(");
            string radarRefresh = ExtractMethodBody(acousticRadar, "private void RefreshMatricesForLateFrame()");

            string compassHotSwap = ExtractMethodBody(sonarCompass, "public void OnGlobalRegistryServiceReplaced(");
            string compassColdCache = ExtractMethodBody(sonarCompass, "private void CacheRegistryServicesCold()");
            string compassCache = ExtractMethodBody(sonarCompass, "private void CacheImpactEmitterReadModel(");
            string compassResolve = ExtractMethodBody(sonarCompass, "private ISpatialAudioImpactEmitterReadModel ResolveImpactEmitterReadModel()");
            string compassUsable = ExtractMethodBody(sonarCompass, "private static bool IsAudioRuntimeObjectUsable(");
            string compassProjection = ExtractMethodBody(sonarCompass, "private void AdvanceCompassProjection(");

            string sceneHotSwap = ExtractMethodBody(sceneRuntime, "public void OnGlobalRegistryServiceReplaced(");
            string sceneColdCache = ExtractMethodBody(sceneRuntime, "private void RefreshTerminalBootServiceHandlesCold()");
            string sceneCache = ExtractMethodBody(sceneRuntime, "private void CacheSceneTransitionAudioBridge(");
            string sceneResolve = ExtractMethodBody(sceneRuntime, "private ISceneTransitionAudioBridge ResolveSceneTransitionAudioBridge()");
            string sceneUsable = ExtractMethodBody(sceneRuntime, "private static bool IsAudioRuntimeObjectUsable(");
            string sceneBegin = ExtractMethodBody(sceneRuntime, "private void BeginWorldDroneCrossfade()");
            string sceneUpdate = ExtractMethodBody(sceneRuntime, "private void UpdateWorldDroneCrossfade(");

            AssertAudioRuntimeObjectUsableBody(beaconUsable);
            AssertAudioRuntimeObjectUsableBody(narrativeUsable);
            AssertAudioRuntimeObjectUsableBody(traumaUsable);
            AssertAudioRuntimeObjectUsableBody(randomUsable);
            AssertAudioRuntimeObjectUsableBody(eclipseUsable);
            AssertAudioRuntimeObjectUsableBody(radarUsable);
            AssertAudioRuntimeObjectUsableBody(compassUsable);
            AssertAudioRuntimeObjectUsableBody(sceneUsable);

            StringAssert.Contains("CacheSpatialAudio(currentService)", beaconHotSwap);
            StringAssert.Contains("CacheSpatialAudio(GlobalRegistry.Audio)", beaconColdCache);
            StringAssert.Contains("_spatialAudio = IsAudioRuntimeObjectUsable(audioRuntime)", beaconCache);
            StringAssert.Contains("if (IsAudioRuntimeObjectUsable(spatialAudio))", beaconResolve);
            StringAssert.Contains("_spatialAudio = null", beaconResolve);
            StringAssert.Contains("ResolveSpatialAudio()", beaconCave);

            StringAssert.Contains("CacheNarrativeAudioSink(currentService)", narrativeHotSwap);
            StringAssert.Contains("CacheNarrativeAudioSink(GlobalRegistry.Audio)", narrativeColdCache);
            StringAssert.Contains("_narrativeAudioSink = IsAudioRuntimeObjectUsable(audioRuntime)", narrativeCache);

            StringAssert.Contains("CacheSpatialAudioSink(currentService)", traumaHotSwap);
            StringAssert.Contains("CacheSpatialAudioSink(GlobalRegistry.Audio)", traumaColdCache);
            StringAssert.Contains("_spatialAudioSink = IsAudioRuntimeObjectUsable(audioRuntime)", traumaCache);
            StringAssert.Contains("if (IsAudioRuntimeObjectUsable(spatialAudioSink))", traumaResolve);
            StringAssert.Contains("_spatialAudioSink = null", traumaResolve);
            StringAssert.Contains("ISpatialAudioEnvironmentModulationSink spatialAudioSink = ResolveSpatialAudioSink()", traumaFlush);

            StringAssert.Contains("CacheMeteorShowerAudioSink(currentService)", randomHotSwap);
            StringAssert.Contains("CacheMeteorShowerAudioSink(GlobalRegistry.Audio)", randomColdCache);
            StringAssert.Contains("_cachedSpatialAudioManager = IsAudioRuntimeObjectUsable(audioRuntime)", randomCache);
            StringAssert.Contains("if (IsAudioRuntimeObjectUsable(spatialAudioManager))", randomResolve);
            StringAssert.Contains("_cachedSpatialAudioManager = null", randomResolve);
            StringAssert.Contains("IMeteorShowerAudioSink spatialAudioManager = ResolveMeteorShowerAudioSink()", randomMeteorBoom);
            StringAssert.Contains("float seaLevelY = ResolveCurrentSeaLevelY();", randomWaterImpact);
            StringAssert.Contains("Vector3 impactPosition = new Vector3(meteorSourcePosition.x, seaLevelY, meteorSourcePosition.z);", randomWaterImpact);
            StringAssert.Contains("private const float MeteorWaterPlaneY = 14.02f;", randomEvents);
            StringAssert.Contains("return MeteorWaterPlaneY;", randomSeaLevel);
            StringAssert.DoesNotContain("private const float MeteorWaterPlaneY = 0f;", randomEvents);
            StringAssert.Contains("IMeteorShowerAudioSink spatialAudioManager = ResolveMeteorShowerAudioSink()", randomWaterBoom);

            StringAssert.Contains("CacheSpatialAudioSink(GlobalRegistry.Audio)", eclipseOnEnable);
            StringAssert.Contains("CacheSpatialAudioSink(currentService)", eclipseHotSwap);
            StringAssert.Contains("_currentAcousticPitchShiftCents = float.NaN", eclipseHotSwap);
            StringAssert.Contains("ISpatialAudioEnvironmentModulationSink spatialAudio = ResolveSpatialAudioSink()", eclipsePublish);
            StringAssert.Contains("_spatialAudioSink = IsAudioRuntimeObjectUsable(audioRuntime)", eclipseCache);
            StringAssert.Contains("if (IsAudioRuntimeObjectUsable(spatialAudioSink))", eclipseResolve);

            StringAssert.Contains("CacheImpactEmitterReadModel(currentService)", radarHotSwap);
            StringAssert.Contains("CacheImpactEmitterReadModel(GlobalRegistry.Audio)", radarColdCache);
            StringAssert.Contains("_cachedAudioManager = IsAudioRuntimeObjectUsable(audioRuntime)", radarCache);
            StringAssert.Contains("if (IsAudioRuntimeObjectUsable(audioManager))", radarResolve);
            StringAssert.Contains("ISpatialAudioImpactEmitterReadModel audioManager = ResolveImpactEmitterReadModel()", radarRefresh);

            StringAssert.Contains("CacheImpactEmitterReadModel(currentService)", compassHotSwap);
            StringAssert.Contains("CacheImpactEmitterReadModel(GlobalRegistry.Audio)", compassColdCache);
            StringAssert.Contains("_cachedAudioManager = IsAudioRuntimeObjectUsable(audioRuntime)", compassCache);
            StringAssert.Contains("if (IsAudioRuntimeObjectUsable(audioManager))", compassResolve);
            StringAssert.Contains("ISpatialAudioImpactEmitterReadModel audioManager = ResolveImpactEmitterReadModel()", compassProjection);

            StringAssert.Contains("CacheSceneTransitionAudioBridge(currentService)", sceneHotSwap);
            StringAssert.Contains("CacheSceneTransitionAudioBridge(GlobalRegistry.Audio)", sceneColdCache);
            StringAssert.Contains("_sceneTransitionAudioBridge = IsAudioRuntimeObjectUsable(audioRuntime)", sceneCache);
            StringAssert.Contains("if (IsAudioRuntimeObjectUsable(sceneTransitionAudioBridge))", sceneResolve);
            StringAssert.Contains("ISceneTransitionAudioBridge spatialAudio = ResolveSceneTransitionAudioBridge()", sceneBegin);
            StringAssert.Contains("ISceneTransitionAudioBridge spatialAudio = ResolveSceneTransitionAudioBridge()", sceneUpdate);

            Assert.That(beaconHotSwap.IndexOf("_spatialAudio = currentService as", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(beaconColdCache.IndexOf("_spatialAudio = GlobalRegistry.Audio as", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(traumaFlush.IndexOf("_spatialAudioSink.SetParasiteRoomAcousticLoad", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(randomMeteorBoom.IndexOf("IMeteorShowerAudioSink spatialAudioManager = _cachedSpatialAudioManager", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(randomWaterBoom.IndexOf("IMeteorShowerAudioSink spatialAudioManager = _cachedSpatialAudioManager", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(eclipsePublish.IndexOf("GlobalRegistry.Audio is ISpatialAudioEnvironmentModulationSink", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(radarRefresh.IndexOf("ISpatialAudioImpactEmitterReadModel audioManager = _cachedAudioManager", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(compassProjection.IndexOf("ISpatialAudioImpactEmitterReadModel audioManager = _cachedAudioManager", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(sceneBegin.IndexOf("ISceneTransitionAudioBridge spatialAudio = _sceneTransitionAudioBridge", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(sceneUpdate.IndexOf("ISceneTransitionAudioBridge spatialAudio = _sceneTransitionAudioBridge", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void ToolAudioCachesOnlyUsableAudioServiceRuntime()
        {
            string laser = Read(LaserCutterPath);
            string playerBuilder = Read(PlayerBuilderPath);

            string hotSwap = ExtractMethodBody(laser, "public void OnGlobalRegistryServiceReplaced(");
            string coldCache = ExtractMethodBody(laser, "private void CacheColdDependencies()");
            string clearCold = ExtractMethodBody(laser, "private void ClearColdDependencies()");
            string cache = ExtractMethodBody(laser, "private void CacheAudioService(");
            string resolve = ExtractMethodBody(laser, "private IAudioService ResolveAudioService()");
            string residencyResolve = ExtractMethodBody(laser, "private IAudioResidencyService ResolveAudioResidencyService()");
            string clearAudio = ExtractMethodBody(laser, "private void ClearCachedAudioService()");
            string usable = ExtractMethodBody(laser, "private static bool IsAudioServiceUsable(");
            string prewarm = ExtractMethodBody(laser, "private void PrewarmEquippedAudio()");
            string release = ExtractMethodBody(laser, "private void ReleaseEquippedAudio()");
            string overheat = ExtractMethodBody(laser, "private void ApplyOverheatLockoutCue()");

            string builderHotSwap = ExtractMethodBody(playerBuilder, "protected override void OnToolRegistryServiceReplaced(");
            string builderBind = ExtractMethodBody(playerBuilder, "private void BindRuntimeReferences()");
            string builderCache = ExtractMethodBody(playerBuilder, "private void CacheAudioService(");
            string builderResolve = ExtractMethodBody(playerBuilder, "private IAudioService ResolveAudioService()");
            string builderClear = ExtractMethodBody(playerBuilder, "private void ClearCachedAudioService()");
            string builderUsable = ExtractMethodBody(playerBuilder, "private static bool IsAudioServiceUsable(");
            string builderFlush = ExtractMethodBody(playerBuilder, "private void FlushPendingBuilderAudio()");

            AssertAudioServiceUsableBody(usable);
            AssertAudioServiceUsableBody(builderUsable);
            StringAssert.Contains("case GlobalRegistryServiceSlot.Audio", hotSwap);
            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", hotSwap);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", coldCache);
            Assert.That(coldCache.IndexOf("_cachedAudioService = GlobalRegistry.Audio", StringComparison.Ordinal), Is.LessThan(0));
            StringAssert.Contains("ClearCachedAudioService()", clearCold);
            StringAssert.Contains("ClearCachedAudioService()", cache);
            StringAssert.Contains("_cachedAudioService = audioService", cache);
            StringAssert.Contains("_cachedAudioResidencyService = audioService as IAudioResidencyService", cache);
            StringAssert.Contains("_cachedCutAudioMixerGroup = audioService.AmbientGroup", cache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", resolve);
            StringAssert.Contains("ClearCachedAudioService()", resolve);
            StringAssert.Contains("ResolveAudioService() != null ? _cachedAudioResidencyService : null", residencyResolve);
            StringAssert.Contains("_cachedAudioService = null", clearAudio);
            StringAssert.Contains("_cachedAudioResidencyService = null", clearAudio);
            StringAssert.Contains("_cachedCutAudioMixerGroup = null", clearAudio);
            StringAssert.Contains("IAudioResidencyService residency = ResolveAudioResidencyService()", prewarm);
            StringAssert.Contains("IAudioResidencyService residency = ResolveAudioResidencyService()", release);
            StringAssert.Contains("IAudioService audioService = ResolveAudioService()", overheat);
            Assert.That(overheat.IndexOf("_cachedAudioService.PlayStatic2D", StringComparison.Ordinal), Is.LessThan(0));

            StringAssert.Contains("case GlobalRegistryServiceSlot.Audio", builderHotSwap);
            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", builderHotSwap);
            StringAssert.Contains("if (!IsAudioServiceUsable(_cachedAudioService))", builderBind);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", builderBind);
            StringAssert.Contains("_cachedAudioService = IsAudioServiceUsable(audioService) ? audioService : null", builderCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", builderResolve);
            StringAssert.Contains("ClearCachedAudioService()", builderResolve);
            StringAssert.Contains("_cachedAudioService = null", builderClear);
            StringAssert.Contains("IAudioService audioService = ResolveAudioService()", builderFlush);
            Assert.That(builderHotSwap.IndexOf("_cachedAudioService = currentService as IAudioService", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(builderBind.IndexOf("_cachedAudioService = GlobalRegistry.Audio", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(builderFlush.IndexOf("IAudioService audioService = _cachedAudioService", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void AudioLogPickupUsesOnlyUsableCachedRuntime()
        {
            string pickup = Read(AudioLogPickupPath);
            string onEnable = ExtractMethodBody(pickup, "private void OnEnable()");
            string onDisable = ExtractMethodBody(pickup, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(pickup, "private void OnDestroy()");
            string hotSwap = ExtractMethodBody(pickup, "public void OnGlobalRegistryServiceReplaced(");
            string interact = ExtractMethodBody(pickup, "public void Interact(");
            string coldCache = ExtractMethodBody(pickup, "private void CacheRegistryServicesCold()");
            string clear = ExtractMethodBody(pickup, "private void ClearCachedRegistryServices()");
            string cache = ExtractMethodBody(pickup, "private void CacheAudioLogSystem(");
            string resolve = ExtractMethodBody(pickup, "private IAudioLogRuntime ResolveAudioLogSystem()");
            string usable = ExtractMethodBody(pickup, "private static bool IsAudioLogRuntimeUsable(");
            string refreshDiscovery = ExtractMethodBody(pickup, "private void RefreshDiscoveryStateFromAudioLogSystem()");
            string configureRecovery = ExtractMethodBody(pickup, "internal void ConfigureRecoveryPickup(");

            StringAssert.Contains("ResolveAudioLogSystem()", onEnable);
            StringAssert.Contains("CacheAudioLogSystem(currentService as IAudioLogRuntime)", hotSwap);
            StringAssert.Contains("ClearCachedRegistryServices();", onDisable);
            StringAssert.Contains("ClearCachedRegistryServices();", onDestroy);
            StringAssert.Contains("IAudioLogRuntime system = ResolveAudioLogSystem()", interact);
            StringAssert.Contains("CacheAudioLogSystem(Hecton8.Core.GlobalRegistry.AudioLogRuntime)", coldCache);
            StringAssert.Contains("_cachedAudioLogSystem = null", clear);
            StringAssert.Contains("_cachedLocalization = null", clear);
            StringAssert.Contains("_cachedAudioLogSystem = IsAudioLogRuntimeUsable(audioLogSystem) ? audioLogSystem : null", cache);
            StringAssert.Contains("if (IsAudioLogRuntimeUsable(audioLogSystem))", resolve);
            StringAssert.Contains("_cachedAudioLogSystem = null", resolve);
            StringAssert.Contains("audioLogSystem is Behaviour behaviour", usable);
            StringAssert.Contains("return behaviour != null && behaviour.isActiveAndEnabled", usable);
            StringAssert.Contains("IAudioLogRuntime audioLogSystem = ResolveAudioLogSystem()", refreshDiscovery);
            StringAssert.Contains("ResolveAudioLogSystem() != null", configureRecovery);
            Assert.That(interact.IndexOf("_cachedAudioLogSystem", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(refreshDiscovery.IndexOf("_cachedAudioLogSystem", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void FirstHourDirectorIgnoresUnusableCachedAudioLogRuntime()
        {
            string firstHour = Read(FirstHourDirectorPath);
            string hotSwap = ExtractMethodBody(firstHour, "public void OnGlobalRegistryServiceReplaced(");
            string coldCache = ExtractMethodBody(firstHour, "private void CacheRuntimeServices()");
            string clear = ExtractMethodBody(firstHour, "private void ClearCachedRuntimeServices()");
            string cache = ExtractMethodBody(firstHour, "private void CacheAudioLogSystem(");
            string resolve = ExtractMethodBody(firstHour, "private IAudioLogRuntime ResolveAudioLogSystem()");
            string usable = ExtractMethodBody(firstHour, "private static bool IsAudioLogRuntimeUsable(");
            string sync = ExtractMethodBody(firstHour, "private void SynchronizeContextFromRuntimeSystems()");

            StringAssert.Contains("CacheAudioLogSystem(currentService as IAudioLogRuntime)", hotSwap);
            StringAssert.Contains("CacheAudioLogSystem(Hecton8.Core.GlobalRegistry.AudioLogRuntime)", coldCache);
            StringAssert.Contains("_cachedAudioLogSystem = null", clear);
            StringAssert.Contains("_cachedAudioLogSystem = IsAudioLogRuntimeUsable(audioLogSystem) ? audioLogSystem : null", cache);
            StringAssert.Contains("if (IsAudioLogRuntimeUsable(audioLogSystem))", resolve);
            StringAssert.Contains("_cachedAudioLogSystem = null", resolve);
            StringAssert.Contains("audioLogSystem is Behaviour behaviour", usable);
            StringAssert.Contains("return behaviour != null && behaviour.isActiveAndEnabled", usable);
            StringAssert.Contains("IAudioLogRuntime audioLogSystem = ResolveAudioLogSystem()", sync);
            Assert.That(sync.IndexOf("_cachedAudioLogSystem", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void EmergencyServiceRelayUsesOnlyUsableAudioLogRuntime()
        {
            string relay = Read(EmergencyServiceRelayPath);
            string onDisable = ExtractMethodBody(relay, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(relay, "private void OnDestroy()");
            string interact = ExtractMethodBody(relay, "public void Interact(");
            string hotSwap = ExtractMethodBody(relay, "public void OnGlobalRegistryServiceReplaced(");
            string coldCache = ExtractMethodBody(relay, "private void CacheRegistryServicesCold()");
            string clear = ExtractMethodBody(relay, "private void ClearCachedRegistryServices()");
            string cache = ExtractMethodBody(relay, "private void CacheAudioLogSystem(");
            string resolve = ExtractMethodBody(relay, "private IAudioLogRuntime ResolveAudioLogSystem()");
            string usable = ExtractMethodBody(relay, "private static bool IsAudioLogRuntimeUsable(");

            StringAssert.Contains("ClearCachedRegistryServices();", onDisable);
            StringAssert.Contains("ClearCachedRegistryServices();", onDestroy);
            StringAssert.Contains("IAudioLogRuntime audioLogSystem = ResolveAudioLogSystem()", interact);
            StringAssert.Contains("CacheAudioLogSystem(currentService as IAudioLogRuntime)", hotSwap);
            StringAssert.Contains("CacheAudioLogSystem(GlobalRegistry.AudioLogRuntime)", coldCache);
            StringAssert.Contains("_cachedNarrativeDiscovery = null", clear);
            StringAssert.Contains("_cachedAudioLogSystem = null", clear);
            StringAssert.Contains("_cachedPlayerContext = null", clear);
            StringAssert.Contains("_cachedLocalization = null", clear);
            StringAssert.Contains("_cachedAudioLogSystem = IsAudioLogRuntimeUsable(audioLogSystem) ? audioLogSystem : null", cache);
            StringAssert.Contains("if (IsAudioLogRuntimeUsable(audioLogSystem))", resolve);
            StringAssert.Contains("_cachedAudioLogSystem = null", resolve);
            StringAssert.Contains("audioLogSystem is Behaviour behaviour", usable);
            StringAssert.Contains("return behaviour != null && behaviour.isActiveAndEnabled", usable);
            Assert.That(interact.IndexOf("_cachedAudioLogSystem", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void NarrativeDiscoveryUsesOnlyUsableAudioLogRuntime()
        {
            string discovery = Read(NarrativeDiscoveryPath);
            string onDisable = ExtractMethodBody(discovery, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(discovery, "private void OnDestroy()");
            string hotSwap = ExtractMethodBody(discovery, "public void OnGlobalRegistryServiceReplaced(");
            string coldCache = ExtractMethodBody(discovery, "private void CacheRegistryServicesCold()");
            string clear = ExtractMethodBody(discovery, "private void ClearCachedRegistryServices()");
            string cache = ExtractMethodBody(discovery, "private void CacheAudioLogSystem(");
            string resolve = ExtractMethodBody(discovery, "private IAudioLogRuntime ResolveAudioLogSystem()");
            string usable = ExtractMethodBody(discovery, "private static bool IsAudioLogRuntimeUsable(");
            string play = ExtractMethodBody(discovery, "private bool TryPlayLinkedAudioLog()");

            StringAssert.Contains("ClearCachedRegistryServices();", onDisable);
            StringAssert.Contains("ClearCachedRegistryServices();", onDestroy);
            StringAssert.Contains("CacheAudioLogSystem(currentService as IAudioLogRuntime)", hotSwap);
            StringAssert.Contains("CacheAudioLogSystem(GlobalRegistry.AudioLogRuntime)", coldCache);
            StringAssert.Contains("_narrativeDiscoveryReadModel = null", clear);
            StringAssert.Contains("_audioLogs = null", clear);
            StringAssert.Contains("_loreUnlockSink = null", clear);
            StringAssert.Contains("_localization = null", clear);
            StringAssert.Contains("_audioLogs = IsAudioLogRuntimeUsable(audioLogSystem) ? audioLogSystem : null", cache);
            StringAssert.Contains("if (IsAudioLogRuntimeUsable(audioLogSystem))", resolve);
            StringAssert.Contains("_audioLogs = null", resolve);
            StringAssert.Contains("audioLogSystem is Behaviour behaviour", usable);
            StringAssert.Contains("return behaviour != null && behaviour.isActiveAndEnabled", usable);
            StringAssert.Contains("IAudioLogRuntime audioLogs = ResolveAudioLogSystem()", play);
            Assert.That(play.IndexOf("_audioLogs", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void PlayerHealthAtmosphericWarningsUseOnlyUsableAudioLogRuntime()
        {
            string health = Read(HectonPlayerHealthPath);
            string advisory = ExtractMethodBody(health, "private void TryIssueRadiationAdvisory(");
            string onDisable = ExtractMethodBody(health, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(health, "private void OnDestroy()");
            string coldCache = ExtractMethodBody(health, "private void CacheRegistryServicesCold()");
            string clear = ExtractMethodBody(health, "private void ClearCachedRegistryServices()");
            string audioCache = ExtractMethodBody(health, "private void CacheAudioService(");
            string audioResolve = ExtractMethodBody(health, "private IAudioService ResolveAudioService()");
            string audioUsable = ExtractMethodBody(health, "private static bool IsAudioServiceUsable(");
            string cache = ExtractMethodBody(health, "private void CacheAudioLogSystem(");
            string resolve = ExtractMethodBody(health, "private IAudioLogRuntime ResolveAudioLogSystem()");
            string usable = ExtractMethodBody(health, "private static bool IsAudioLogRuntimeUsable(");
            string hotSwap = ExtractMethodBody(health, "private void OnRegistryServiceReplaced(");
            string heartbeat = ExtractMethodBody(health, "private void PlaySurvivalGraceHeartbeatPulse()");
            string flush = ExtractMethodBody(health, "private void FlushQueuedPresentationFeedback()");

            StringAssert.Contains("IAudioLogRuntime audioLogs = ResolveAudioLogSystem()", advisory);
            StringAssert.Contains("audioLogs.NotifyAtmosphericWarningStarted(glitchDuration)", advisory);
            StringAssert.Contains("ClearCachedRegistryServices();", onDisable);
            StringAssert.Contains("ClearCachedRegistryServices();", onDestroy);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", coldCache);
            StringAssert.Contains("CacheAudioLogSystem(GlobalRegistry.AudioLogRuntime)", coldCache);
            StringAssert.Contains("_audioService = null", clear);
            StringAssert.Contains("_audioLogs = null", clear);
            StringAssert.Contains("_audioService = IsAudioServiceUsable(audioService) ? audioService : null", audioCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", audioResolve);
            StringAssert.Contains("_audioService = null", audioResolve);
            AssertAudioServiceUsableBody(audioUsable);
            StringAssert.Contains("_audioLogs = IsAudioLogRuntimeUsable(audioLogSystem) ? audioLogSystem : null", cache);
            StringAssert.Contains("if (IsAudioLogRuntimeUsable(audioLogSystem))", resolve);
            StringAssert.Contains("_audioLogs = null", resolve);
            StringAssert.Contains("audioLogSystem is Behaviour behaviour", usable);
            StringAssert.Contains("return behaviour != null && behaviour.isActiveAndEnabled", usable);
            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", hotSwap);
            StringAssert.Contains("CacheAudioLogSystem(currentService as IAudioLogRuntime)", hotSwap);
            StringAssert.Contains("ResolveAudioService() == null", heartbeat);
            StringAssert.Contains("IAudioService audioService = ResolveAudioService()", flush);
            Assert.That(advisory.IndexOf("_audioLogs", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(coldCache.IndexOf("_audioService = GlobalRegistry.Audio", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(hotSwap.IndexOf("_audioService = currentService as IAudioService", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(flush.IndexOf("IAudioService audioService = _audioService", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void PlayerHealthPresentationAndRespawnAupUseRuntimeContextBeforeMovementFallback()
        {
            string health = Read(HectonPlayerHealthPath);
            string capturePresentation = ExtractMethodBody(health, "private Vector3 CapturePlayerRuntimePositionForPresentation()");
            string respawnDeath = ExtractMethodBody(health, "internal bool TryResolveRespawnDeathAup(out double3 deathAup)");
            string activeAup = ExtractMethodBody(health, "private static bool TryResolveActivePlayerAup(");
            string runtimePosition = ExtractMethodBody(health, "private static bool TryResolveRuntimePositionFromAup(");

            StringAssert.Contains("TryResolveActivePlayerAup(out AbsoluteUniversePosition activeAup, out bool hasRuntimeContext)", capturePresentation);
            StringAssert.Contains("TryResolveRuntimePositionFromAup(in activeAup, out Vector3 runtimePosition)", capturePresentation);
            StringAssert.Contains("if (hasRuntimeContext)", capturePresentation);
            AssertTextBefore(capturePresentation, "TryResolveActivePlayerAup", "AbsoluteUniversePosition currentAup = _playerMovement.CurrentAup");
            AssertTextBefore(capturePresentation, "if (hasRuntimeContext)", "AbsoluteUniversePosition currentAup = _playerMovement.CurrentAup");

            StringAssert.Contains("TryResolveActivePlayerAup(out AbsoluteUniversePosition activeAup, out bool hasRuntimeContext)", respawnDeath);
            StringAssert.Contains("deathAup = activeAup.ToAbsoluteDouble3();", respawnDeath);
            StringAssert.Contains("if (hasRuntimeContext)", respawnDeath);
            AssertTextBefore(respawnDeath, "TryResolveActivePlayerAup", "var currentAup = _playerMovement.CurrentAup");
            AssertTextBefore(respawnDeath, "if (hasRuntimeContext)", "var currentAup = _playerMovement.CurrentAup");

            StringAssert.Contains("IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext", activeAup);
            StringAssert.Contains("runtimeContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)", activeAup);
            StringAssert.Contains("(snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", activeAup);
            StringAssert.Contains("snapshot.Aup.IsFinite()", activeAup);
            StringAssert.Contains("runtimeContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", activeAup);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u", activeAup);
            StringAssert.Contains("!movementState.PredictedAup.IsFinite()", activeAup);
            StringAssert.Contains("playerAup = movementState.PredictedAup;", activeAup);
            AssertTextBefore(activeAup, "runtimeContext.TryGetPlayerPoseSnapshot", "runtimeContext.TryGetMovementRuntimeState");
            AssertTextBefore(activeAup, "snapshot.Aup.IsFinite()", "playerAup = snapshot.Aup;");
            AssertTextBefore(activeAup, "movementState.PredictedAup.IsFinite()", "playerAup = movementState.PredictedAup;");
            Assert.That(activeAup.IndexOf("runtimeContext.MovementState", StringComparison.Ordinal), Is.LessThan(0));

            StringAssert.Contains("playerAup.IsFinite()", runtimePosition);
            StringAssert.Contains("float3 runtimePosition3 = playerAup.ToRuntimeFloat3();", runtimePosition);
            StringAssert.Contains("math.all(math.isfinite(runtimePosition3))", runtimePosition);
        }

        [Test]
        public void AtlasSignalAudioLogConsumersUseOnlyUsableRuntime()
        {
            string beacon = Read(SignalBeaconPath);
            string atlas = Read(AtlasSignalSystemPath);
            string beaconOnDisable = ExtractMethodBody(beacon, "private void OnDisable()");
            string beaconOnDestroy = ExtractMethodBody(beacon, "private void OnDestroy()");
            string beaconSolve = ExtractMethodBody(beacon, "private void SolveTelemetry()");
            string beaconRecover = ExtractMethodBody(beacon, "private bool TryRecoverFragment()");
            string beaconHotSwap = ExtractMethodBody(beacon, "public void OnGlobalRegistryServiceReplaced(");
            string beaconColdCache = ExtractMethodBody(beacon, "private void CacheRegistryServicesCold()");
            string beaconClear = ExtractMethodBody(beacon, "private void ClearCachedRegistryServices()");
            string beaconCache = ExtractMethodBody(beacon, "private void CacheAudioLogSystem(");
            string beaconResolve = ExtractMethodBody(beacon, "private IAudioLogRuntime ResolveAudioLogSystem()");
            string beaconUsable = ExtractMethodBody(beacon, "private static bool IsAudioLogRuntimeUsable(");
            string atlasHotSwap = ExtractMethodBody(atlas, "public void OnGlobalRegistryServiceReplaced(");
            string atlasColdCache = ExtractMethodBody(atlas, "private void CacheRuntimeDependencies()");
            string atlasClear = ExtractMethodBody(atlas, "private void ClearRuntimeDependencies()");
            string atlasCache = ExtractMethodBody(atlas, "private void CacheAudioLogSystem(");
            string atlasResolve = ExtractMethodBody(atlas, "private IAudioLogRuntime ResolveAudioLogSystem()");
            string atlasUsable = ExtractMethodBody(atlas, "private static bool IsAudioLogRuntimeUsable(");
            string atlasReveal = ExtractMethodBody(atlas, "private void RevealEncryptedLogForStage(");

            StringAssert.Contains("ClearCachedRegistryServices();", beaconOnDisable);
            StringAssert.Contains("ClearCachedRegistryServices();", beaconOnDestroy);
            StringAssert.Contains("IAudioLogRuntime audioLogs = ResolveAudioLogSystem()", beaconSolve);
            StringAssert.Contains("IAudioLogRuntime audioLogs = ResolveAudioLogSystem()", beaconRecover);
            StringAssert.Contains("CacheAudioLogSystem(currentService as IAudioLogRuntime)", beaconHotSwap);
            StringAssert.Contains("CacheAudioLogSystem(GlobalRegistry.AudioLogRuntime)", beaconColdCache);
            StringAssert.Contains("_audioLogs = null", beaconClear);
            StringAssert.Contains("_spatialAudio = null", beaconClear);
            StringAssert.Contains("_playerRuntimeContext = null", beaconClear);
            StringAssert.Contains("_audioLogs = IsAudioLogRuntimeUsable(audioLogSystem) ? audioLogSystem : null", beaconCache);
            StringAssert.Contains("if (IsAudioLogRuntimeUsable(audioLogSystem))", beaconResolve);
            StringAssert.Contains("_audioLogs = null", beaconResolve);
            StringAssert.Contains("audioLogSystem is Behaviour behaviour", beaconUsable);
            StringAssert.Contains("return behaviour != null && behaviour.isActiveAndEnabled", beaconUsable);
            Assert.That(beaconSolve.IndexOf("_audioLogs", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(beaconRecover.IndexOf("_audioLogs", StringComparison.Ordinal), Is.LessThan(0));

            StringAssert.Contains("CacheAudioLogSystem(currentService as IAudioLogRuntime)", atlasHotSwap);
            StringAssert.Contains("CacheAudioLogSystem(GlobalRegistry.AudioLogRuntime)", atlasColdCache);
            StringAssert.Contains("_audioLogs = null", atlasClear);
            StringAssert.Contains("_audioLogs = IsAudioLogRuntimeUsable(audioLogSystem) ? audioLogSystem : null", atlasCache);
            StringAssert.Contains("if (IsAudioLogRuntimeUsable(audioLogSystem))", atlasResolve);
            StringAssert.Contains("_audioLogs = null", atlasResolve);
            StringAssert.Contains("audioLogSystem is Behaviour behaviour", atlasUsable);
            StringAssert.Contains("return behaviour != null && behaviour.isActiveAndEnabled", atlasUsable);
            StringAssert.Contains("IAudioLogRuntime audioLogs = ResolveAudioLogSystem()", atlasReveal);
            Assert.That(atlasReveal.IndexOf("_audioLogs", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void Atlas6LiabilityEvidenceSyncUsesOnlyUsableAudioLogRuntime()
        {
            string manager = Read(Atlas6CorporateLiabilityManagerPath);
            string onEnable = ExtractMethodBody(manager, "private void OnEnable()");
            string onDisable = ExtractMethodBody(manager, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(manager, "private void OnDestroy()");
            string hotSwap = ExtractMethodBody(manager, "public void OnGlobalRegistryServiceReplaced(");
            string clear = ExtractMethodBody(manager, "private void ClearCachedRuntimeServices()");
            string sync = ExtractMethodBody(manager, "private void TrySyncDisasterEvidenceFromAudioLogRuntime()");
            string cache = ExtractMethodBody(manager, "private void CacheAudioLogSystem(");
            string resolve = ExtractMethodBody(manager, "private IAudioLogRuntime ResolveAudioLogSystem()");
            string usable = ExtractMethodBody(manager, "private static bool IsAudioLogRuntimeUsable(");

            StringAssert.Contains("CacheAudioLogSystem(GlobalRegistry.AudioLogRuntime)", onEnable);
            StringAssert.Contains("TrySyncDisasterEvidenceFromAudioLogRuntime()", onEnable);
            StringAssert.Contains("ClearCachedRuntimeServices();", onDisable);
            StringAssert.Contains("ClearCachedRuntimeServices();", onDestroy);
            StringAssert.Contains("CacheAudioLogSystem(currentService as IAudioLogRuntime)", hotSwap);
            StringAssert.Contains("TrySyncDisasterEvidenceFromAudioLogRuntime()", hotSwap);
            StringAssert.Contains("_audioLogs = null", clear);
            StringAssert.Contains("IAudioLogRuntime audioLogRuntime = ResolveAudioLogSystem()", sync);
            StringAssert.Contains("audioLogRuntime.IsAudioLogDiscovered(Atlas6TerminalSector3AudioLogHash)", sync);
            StringAssert.Contains("_audioLogs = IsAudioLogRuntimeUsable(audioLogSystem) ? audioLogSystem : null", cache);
            StringAssert.Contains("if (IsAudioLogRuntimeUsable(audioLogSystem))", resolve);
            StringAssert.Contains("_audioLogs = null", resolve);
            StringAssert.Contains("audioLogSystem is Behaviour behaviour", usable);
            StringAssert.Contains("return behaviour != null && behaviour.isActiveAndEnabled", usable);
            Assert.That(onEnable.IndexOf("TrySyncDisasterEvidenceFromAudioLogRuntime(" + "GlobalRegistry.AudioLogRuntime)", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(hotSwap.IndexOf("TrySyncDisasterEvidenceFromAudioLogRuntime(" + "currentService as IAudioLogRuntime)", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(sync.IndexOf("GlobalRegistry.AudioLogRuntime", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(sync.IndexOf("_audioLogs", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void ConcreteAudioLogSystemConsumersUseOnlyUsableRuntime()
        {
            string atmosphere = Read(SubmarineAtmosphereSystemPath);
            string proceduralLore = Read(ProceduralLoreDirectorPath);
            string pda = Read(PdaDataLogTabPath);
            string progressionBridge = Read(NarrativeProgressionBridgePath);
            string atmosphereOnDisable = ExtractMethodBody(atmosphere, "private void OnDisable()");
            string atmosphereOnDestroy = ExtractMethodBody(atmosphere, "private void OnDestroy()");
            string atmosphereColdCache = ExtractMethodBody(atmosphere, "private void CacheReferencesCold()");
            string atmosphereClear = ExtractMethodBody(atmosphere, "private void ClearCachedRuntimeServices()");
            string atmosphereAudioCache = ExtractMethodBody(atmosphere, "private void CacheAudioService(");
            string atmosphereAudioResolve = ExtractMethodBody(atmosphere, "private IAudioService ResolveAudioService()");
            string atmosphereAudioUsable = ExtractMethodBody(atmosphere, "private static bool IsAudioServiceUsable(");
            string atmosphereCache = ExtractMethodBody(atmosphere, "private void CacheAudioLogSystem(");
            string atmosphereResolve = ExtractMethodBody(atmosphere, "private AudioLogSystem ResolveAudioLogSystem()");
            string atmosphereUsable = ExtractMethodBody(atmosphere, "private static bool IsAudioLogSystemUsable(");
            string atmosphereQueue = ExtractMethodBody(atmosphere, "private void QueueLowOxygenGaspingAudioLog()");
            string atmosphereFlush = ExtractMethodBody(atmosphere, "private void FlushQueuedAtmosphereAudio()");
            string atmosphereHotSwap = ExtractMethodBody(atmosphere, "public void OnGlobalRegistryServiceReplaced(");
            string loreOnDisable = ExtractMethodBody(proceduralLore, "private void OnDisable()");
            string loreOnDestroy = ExtractMethodBody(proceduralLore, "private void OnDestroy()");
            string loreRefresh = ExtractMethodBody(proceduralLore, "private void RefreshActivePlacements()");
            string loreSpawn = ExtractMethodBody(proceduralLore, "private bool TrySpawnInstance(");
            string loreResolveOwners = ExtractMethodBody(proceduralLore, "private bool ResolveOwners()");
            string loreColdCache = ExtractMethodBody(proceduralLore, "private void RefreshCachedOwners()");
            string loreClear = ExtractMethodBody(proceduralLore, "private void ClearCachedRuntimeServices()");
            string loreCache = ExtractMethodBody(proceduralLore, "private void CacheAudioLogSystem(");
            string loreResolve = ExtractMethodBody(proceduralLore, "private AudioLogSystem ResolveAudioLogSystem()");
            string loreUsable = ExtractMethodBody(proceduralLore, "private static bool IsAudioLogSystemUsable(");
            string loreSelect = ExtractMethodBody(proceduralLore, "private bool TrySelectLoreEntry(");
            string loreHotSwap = ExtractMethodBody(proceduralLore, "public void OnGlobalRegistryServiceReplaced(");
            string pdaReset = ExtractMethodBody(pda, "private static void ResetCatalogRegistry()");
            string pdaColdCache = ExtractMethodBody(pda, "private static void CacheRegistryServicesCold()");
            string pdaClear = ExtractMethodBody(pda, "private static void ClearCachedRuntimeServices()");
            string pdaCache = ExtractMethodBody(pda, "private static void CacheAudioLogSystem(");
            string pdaResolve = ExtractMethodBody(pda, "private static AudioLogSystem ResolveAudioLogSystem()");
            string pdaUsable = ExtractMethodBody(pda, "private static bool IsAudioLogSystemUsable(");
            string pdaPlay = ExtractMethodBody(pda, "public void PlaySelected()");
            string pdaPlaybackStarted = ExtractMethodBody(pda, "private void HandlePlaybackStarted(");
            string pdaRowHighlights = ExtractMethodBody(pda, "private void RefreshRowHighlights()");
            string pdaPlayButton = ExtractMethodBody(pda, "private void RefreshPlayButton()");
            string pdaStressRefresh = ExtractMethodBody(pda, "private void RefreshStressReactiveDetailIfNeeded()");
            string pdaHotSwap = ExtractMethodBody(pda, "public void OnGlobalRegistryServiceReplaced(");
            string progressionOnEnable = ExtractMethodBody(progressionBridge, "private void OnEnable()");
            string progressionOnDisable = ExtractMethodBody(progressionBridge, "private void OnDisable()");
            string progressionOnDestroy = ExtractMethodBody(progressionBridge, "private void OnDestroy()");
            string progressionHotSwap = ExtractMethodBody(progressionBridge, "public void OnGlobalRegistryServiceReplaced(");
            string progressionClear = ExtractMethodBody(progressionBridge, "private void ClearCachedRuntimeServices()");
            string progressionCache = ExtractMethodBody(progressionBridge, "private void CacheAudioLogSystem(");
            string progressionBreach = ExtractMethodBody(progressionBridge, "public void OnBaseIntegrityEvent(");
            string progressionResolve = ExtractMethodBody(progressionBridge, "private AudioLogSystem ResolveAudioLogSystem()");
            string progressionUsable = ExtractMethodBody(progressionBridge, "private static bool IsAudioLogSystemUsable(");

            StringAssert.Contains("ClearCachedRuntimeServices();", atmosphereOnDisable);
            StringAssert.Contains("ClearCachedRuntimeServices();", atmosphereOnDestroy);
            StringAssert.Contains("CacheAudioService(GlobalRegistry.Audio)", atmosphereColdCache);
            StringAssert.Contains("CacheAudioLogSystem(GlobalRegistry.AudioLogs)", atmosphereColdCache);
            StringAssert.Contains("_audioService = null", atmosphereClear);
            StringAssert.Contains("_audioLogs = null", atmosphereClear);
            StringAssert.Contains("_audioService = IsAudioServiceUsable(audioService) ? audioService : null", atmosphereAudioCache);
            StringAssert.Contains("if (IsAudioServiceUsable(audioService))", atmosphereAudioResolve);
            StringAssert.Contains("_audioService = null", atmosphereAudioResolve);
            AssertAudioServiceUsableBody(atmosphereAudioUsable);
            StringAssert.Contains("_audioLogs = IsAudioLogSystemUsable(audioLogs) ? audioLogs : null", atmosphereCache);
            StringAssert.Contains("if (IsAudioLogSystemUsable(audioLogs))", atmosphereResolve);
            StringAssert.Contains("_audioLogs = null", atmosphereResolve);
            StringAssert.Contains("return audioLogs != null && audioLogs.isActiveAndEnabled", atmosphereUsable);
            StringAssert.Contains("AudioLogSystem audioLogs = ResolveAudioLogSystem()", atmosphereQueue);
            StringAssert.Contains("AudioLogSystem audioLogs = ResolveAudioLogSystem()", atmosphereFlush);
            StringAssert.Contains("IAudioService audioService = ResolveAudioService()", atmosphereFlush);
            StringAssert.Contains("CacheAudioService(currentService as IAudioService)", atmosphereHotSwap);
            StringAssert.Contains("CacheAudioLogSystem(currentService as AudioLogSystem)", atmosphereHotSwap);
            Assert.That(atmosphereQueue.IndexOf("_audioLogs", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(atmosphereFlush.IndexOf("_audioLogs", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(atmosphereColdCache.IndexOf("_audioService = GlobalRegistry.Audio", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(atmosphereHotSwap.IndexOf("_audioService = currentService as IAudioService", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(atmosphereFlush.IndexOf("IAudioService audioService = _audioService", StringComparison.Ordinal), Is.LessThan(0));

            StringAssert.Contains("ClearCachedRuntimeServices();", loreOnDisable);
            StringAssert.Contains("ClearCachedRuntimeServices();", loreOnDestroy);
            StringAssert.Contains("AudioLogSystem audioLogSystem = ResolveAudioLogSystem()", loreRefresh);
            StringAssert.Contains("AudioLogSystem audioLogSystem = ResolveAudioLogSystem()", loreSpawn);
            StringAssert.Contains("ResolveAudioLogSystem() != null", loreResolveOwners);
            StringAssert.Contains("CacheAudioLogSystem(Hecton8.Core.GlobalRegistry.AudioLogs)", loreColdCache);
            StringAssert.Contains("_audioLogSystem = null", loreClear);
            StringAssert.Contains("_audioLogSystem = IsAudioLogSystemUsable(audioLogSystem) ? audioLogSystem : null", loreCache);
            StringAssert.Contains("if (IsAudioLogSystemUsable(audioLogSystem))", loreResolve);
            StringAssert.Contains("_audioLogSystem = null", loreResolve);
            StringAssert.Contains("return audioLogSystem != null && audioLogSystem.isActiveAndEnabled", loreUsable);
            StringAssert.Contains("AudioLogSystem audioLogSystem = ResolveAudioLogSystem()", loreSelect);
            StringAssert.Contains("CacheAudioLogSystem(currentService as AudioLogSystem)", loreHotSwap);
            Assert.That(loreRefresh.IndexOf("_audioLogSystem", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(loreSpawn.IndexOf("_audioLogSystem", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(loreSelect.IndexOf("_audioLogSystem", StringComparison.Ordinal), Is.LessThan(0));

            StringAssert.Contains("ClearCachedRuntimeServices();", pdaReset);
            StringAssert.Contains("CacheAudioLogSystem(GlobalRegistry.AudioLogs)", pdaColdCache);
            StringAssert.Contains("s_cachedAudioLogs = null", pdaClear);
            StringAssert.Contains("s_cachedAudioLogs = IsAudioLogSystemUsable(audioLogSystem) ? audioLogSystem : null", pdaCache);
            StringAssert.Contains("if (IsAudioLogSystemUsable(audioLogSystem))", pdaResolve);
            StringAssert.Contains("s_cachedAudioLogs = null", pdaResolve);
            StringAssert.Contains("return audioLogSystem != null && audioLogSystem.isActiveAndEnabled", pdaUsable);
            StringAssert.Contains("AudioLogSystem system = ResolveAudioLogSystem()", pdaPlay);
            StringAssert.Contains("AudioLogSystem system = ResolveAudioLogSystem()", pdaPlaybackStarted);
            StringAssert.Contains("AudioLogSystem system = ResolveAudioLogSystem()", pdaRowHighlights);
            StringAssert.Contains("AudioLogSystem system = ResolveAudioLogSystem()", pdaPlayButton);
            StringAssert.Contains("AudioLogSystem system = ResolveAudioLogSystem()", pdaStressRefresh);
            StringAssert.Contains("CacheAudioLogSystem(currentService as AudioLogSystem)", pdaHotSwap);
            StringAssert.Contains("AudioLogSystem sys = PDADataLogTab.ResolveAudioLogSystem()", pda);

            StringAssert.Contains("AudioLogSystem audioLogs = ResolveAudioLogSystem()", progressionBreach);
            StringAssert.Contains("audioLogs.NotifyAtmosphericWarningStarted(0.7f)", progressionBreach);
            StringAssert.Contains("audioLogs.TryPlayLogByHash(_hullFailureLogHash)", progressionBreach);
            StringAssert.Contains("CacheAudioLogSystem(GlobalRegistry.AudioLogs)", progressionOnEnable);
            StringAssert.Contains("TryRegisterHotSwapListener()", progressionOnEnable);
            StringAssert.Contains("TryUnregisterHotSwapListener()", progressionOnDisable);
            StringAssert.Contains("ClearCachedRuntimeServices();", progressionOnDisable);
            StringAssert.Contains("TryUnregisterHotSwapListener()", progressionOnDestroy);
            StringAssert.Contains("ClearCachedRuntimeServices();", progressionOnDestroy);
            StringAssert.Contains("GlobalRegistryServiceSlot.AudioLogRuntime", progressionHotSwap);
            StringAssert.Contains("CacheAudioLogSystem(currentService as AudioLogSystem)", progressionHotSwap);
            StringAssert.Contains("_audioLogs = null", progressionClear);
            StringAssert.Contains("_audioLogs = IsAudioLogSystemUsable(audioLogs) ? audioLogs : null", progressionCache);
            StringAssert.Contains("if (IsAudioLogSystemUsable(audioLogs))", progressionResolve);
            StringAssert.Contains("_audioLogs = null", progressionResolve);
            StringAssert.Contains("return audioLogs != null && audioLogs.isActiveAndEnabled", progressionUsable);
            Assert.That(progressionBreach.IndexOf("GlobalRegistry.AudioLogs", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(progressionBreach.IndexOf("_audioLogs", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(progressionResolve.IndexOf("GlobalRegistry.AudioLogs", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void AdaptiveStemBridgeFeedsContextButNeverMusicActivityOrReactivePunches()
        {
            string adaptiveStem = Read(AdaptiveStemMixerPath);
            string apply = ExtractMethodBody(adaptiveStem, "private void ApplyMixFrameToUnityAudio(");
            string push = ExtractMethodBody(adaptiveStem, "private void PushDynamicMusicSignal(");
            string ruleWrite = ExtractMethodBody(adaptiveStem, "private bool TryWriteRuleForOwnerRoute(");
            string ruleAcquire = ExtractMethodBody(adaptiveStem, "private bool TryAcquireRuleMutationView(");
            string tick = ExtractMethodBody(adaptiveStem, "public void Tick(float deltaTime)");
            string frameAcquire = ExtractMethodBody(adaptiveStem, "private bool TryAcquireStemFrameMutationView(");
            string ensureVaultStorage = ExtractMethodBody(adaptiveStem, "private void EnsureVaultStorage()");
            string emergencyProfiles = ExtractMethodBody(adaptiveStem, "private void GenerateEmergencyMockAudioProfiles()");

            StringAssert.Contains("ProceduralSynthOwnsStemTransport", apply);
            StringAssert.Contains("PushDynamicMusicSignal(tension, depthMeters, quality);", apply);
            StringAssert.Contains("DynamicMusicScalarSignal.FlagExternalScalars |", push);
            StringAssert.Contains("DynamicMusicScalarSignal.FlagSuppressReactiveImpulses", push);
            StringAssert.Contains("signal.Tension01 = math.saturate", push);
            StringAssert.Contains("signal.DepthMeters = math.max", push);
            StringAssert.Contains("signal.GlobalQualityWeight = math.saturate", push);
            StringAssert.Contains("signal.DamageImpulse01 = 0f", push);
            StringAssert.Contains("signal.MusicActivity01 = 0f", push);
            StringAssert.Contains("DynamicMusicScalarSignal.SourceAdaptiveStemHash", push);
            StringAssert.Contains("AudioStemRulesMutationGuardMask", adaptiveStem);
            StringAssert.Contains("AudioStemFrameMutationGuardMask", adaptiveStem);
            StringAssert.Contains("AudioStemRulesMutationGuardMask |", adaptiveStem);
            StringAssert.Contains("TryAcquireStemFrameMutationView", tick);
            StringAssert.Contains("ReleaseAdaptiveStemMutationGuard(guardVault, AudioStemFrameMutationGuardMask)", tick);
            StringAssert.Contains("TryAcquireMutationGuard(AudioStemFrameMutationGuardMask)", frameAcquire);
            StringAssert.Contains("TryResolveStemOwnerViews(guardVault, out views)", frameAcquire);
            StringAssert.Contains("ReleaseAdaptiveStemMutationGuard(guardVault, AudioStemFrameMutationGuardMask)", frameAcquire);
            StringAssert.Contains("TryAcquireStemFrameMutationView", ensureVaultStorage);
            StringAssert.Contains("ReleaseAdaptiveStemMutationGuard(guardVault, AudioStemFrameMutationGuardMask)", ensureVaultStorage);
            StringAssert.Contains("TryAcquireStemFrameMutationView", emergencyProfiles);
            StringAssert.Contains("ReleaseAdaptiveStemMutationGuard(guardVault, AudioStemFrameMutationGuardMask)", emergencyProfiles);
            StringAssert.Contains("TryAcquireRuleMutationView", ruleWrite);
            StringAssert.Contains("ReleaseAdaptiveStemMutationGuard", ruleWrite);
            StringAssert.Contains("TryAcquireMutationGuard(AudioStemRulesMutationGuardMask)", ruleAcquire);
            StringAssert.Contains("TryResolveHandle(in _rulesHandle", ruleAcquire);
            StringAssert.Contains("ReleaseAdaptiveStemMutationGuard", ruleAcquire);
            StringAssert.Contains("return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);", adaptiveStem);
            Assert.That(apply.IndexOf("frame.IoPressure01", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(push.IndexOf("damageImpulse", StringComparison.OrdinalIgnoreCase), Is.LessThan(0));
            Assert.That(adaptiveStem.IndexOf("TryAcquireWriteLock", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(adaptiveStem.IndexOf("ReleaseWriteLock", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void SurvivalVitalsAudioConsumersRequireFlagsAndFiniteValues()
        {
            string adaptiveStem = Read(AdaptiveStemMixerPath);
            string vocalWarning = Read(VocalWarningSystemPath);
            string drain = ExtractMethodBody(adaptiveStem, "private void DrainSignalInputs()");
            int survivalStart = vocalWarning.IndexOf("for (int i = 0; i < SurvivalSignals.Length && evaluations < MaxEvaluations; i++)", StringComparison.Ordinal);
            int survivalEnd = vocalWarning.IndexOf("private unsafe void DecayCooldowns()", survivalStart, StringComparison.Ordinal);
            Assert.That(survivalStart, Is.GreaterThanOrEqualTo(0));
            Assert.That(survivalEnd, Is.GreaterThan(survivalStart));
            string vocalSurvivalBlock = vocalWarning.Substring(survivalStart, survivalEnd - survivalStart);

            StringAssert.Contains("(signal.Flags & SurvivalVitalsChangedSignalFlags.Oxygen) == 0u", drain);
            StringAssert.Contains("!math.isfinite(signal.Oxygen01)", drain);
            StringAssert.Contains("oxygenDanger01 = 1f - math.saturate(signal.Oxygen01);", drain);
            StringAssert.Contains("uint survivalFlags = signal.Flags;", vocalSurvivalBlock);
            StringAssert.Contains("(survivalFlags & SurvivalVitalsChangedSignalFlags.OxygenCritical) != 0u", vocalSurvivalBlock);
            StringAssert.Contains("(survivalFlags & SurvivalVitalsChangedSignalFlags.Oxygen) != 0u && oxygen01 < 0.22f", vocalSurvivalBlock);
            StringAssert.Contains("math.select(0f, signal.Oxygen01, math.isfinite(signal.Oxygen01))", vocalSurvivalBlock);
            StringAssert.DoesNotContain("|| signal.Oxygen01 < 0.22f", vocalSurvivalBlock);
        }

        [Test]
        public void ManagedAudioCallbacksStayTransferOnlyAndDirectLookupFree()
        {
            string dynamicMusic = ExtractMethodBody(Read(DynamicMusicSynthPath), "private void OnAudioFilterRead(");
            string vocalRuntime = Read(VocalBankRuntimePath);
            string vocalRelease = ExtractMethodBodyAfter(vocalRuntime, "#if !UNITY_EDITOR && !DEVELOPMENT_BUILD", "private void OnAudioFilterRead(");
            string vocalDevelopment = ExtractMethodBodyAfter(vocalRuntime, "#else", "private void OnAudioFilterRead(");

            AssertNoDirectManagedCallbackHazards(dynamicMusic);
            AssertNoDirectManagedCallbackHazards(vocalRelease);
            StringAssert.Contains("ZeroManagedAudioBuffer(data", dynamicMusic);
            StringAssert.Contains("ZeroManagedAudioBuffer(data", vocalRelease);
            StringAssert.Contains("ZeroManagedAudioBuffer(data", vocalDevelopment);
            StringAssert.Contains("Volatile.Read", dynamicMusic);
            StringAssert.Contains("Volatile.Read", vocalDevelopment);
            StringAssert.Contains("fixed (float* destination = data)", dynamicMusic);
            StringAssert.Contains("fixed (float* output = data)", vocalDevelopment);
            StringAssert.Contains("VocalDecodeKernel.DecodeIntoAudioBuffer", vocalDevelopment);
            Assert.That(vocalRelease.IndexOf("VocalDecodeKernel.DecodeIntoAudioBuffer", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(vocalRelease.IndexOf("TryAcquireAudioCallbackViews", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(vocalRelease.IndexOf("TryAcquireLockedView", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(vocalRelease.IndexOf("Stopwatch.GetTimestamp", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void VocalBankEditorCsvScratchClearsPointerBeforeSentinelUnregisterRetry()
        {
            string vocalRuntime = Read(VocalBankRuntimePath);
            string allocateScratch = ExtractMethodBody(vocalRuntime, "private bool TryGetEditorCsvScratch(");
            string releaseScratch = ExtractMethodBody(vocalRuntime, "private void ReleaseEditorCsvScratch()");

            StringAssert.Contains("catch (Exception exception)", allocateScratch);
            StringAssert.Contains("ReleaseEditorCsvScratch();", allocateScratch);
            StringAssert.Contains("catch (Exception releaseException)", allocateScratch);
            StringAssert.Contains("throw new AggregateException", allocateScratch);
            StringAssert.Contains("throw;", allocateScratch);
            AssertTextBefore(allocateScratch, "ReleaseEditorCsvScratch();", "throw;");
            Assert.That(allocateScratch.IndexOf("NativeMemorySentinel.Unregister(sentinelId)", StringComparison.Ordinal), Is.LessThan(0));

            StringAssert.Contains("bool released = _editorCsvScratch == null;", releaseScratch);
            StringAssert.Contains("UnsafeUtility.Free(_editorCsvScratch, Allocator.Persistent);", releaseScratch);
            StringAssert.Contains("_editorCsvScratch = null;", releaseScratch);
            StringAssert.Contains("released = true;", releaseScratch);
            StringAssert.Contains("if (released)", releaseScratch);
            StringAssert.Contains("if (_editorCsvScratchSentinelId > 0)", releaseScratch);
            StringAssert.Contains("NativeMemorySentinel.Unregister(_editorCsvScratchSentinelId);", releaseScratch);
            AssertTextBefore(
                releaseScratch,
                "UnsafeUtility.Free(_editorCsvScratch, Allocator.Persistent);",
                "_editorCsvScratch = null;");
            AssertTextBefore(releaseScratch, "_editorCsvScratch = null;", "NativeMemorySentinel.Unregister(_editorCsvScratchSentinelId);");
            AssertTextBefore(releaseScratch, "NativeMemorySentinel.Unregister(_editorCsvScratchSentinelId);", "_editorCsvScratchSentinelId = 0;");
            Assert.That(releaseScratch.IndexOf("int sentinelId = _editorCsvScratchSentinelId;", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void VocalWarningTuningWritesUseMutationGuardNotWriteLock()
        {
            string vocalWarning = Read(VocalWarningSystemPath);
            string tuningWrite = ExtractMethodBody(vocalWarning, "public unsafe bool EditorTryWriteTuning(");
            string tuningAcquire = ExtractMethodBody(vocalWarning, "private bool TryAcquireTuningMutationView(");

            StringAssert.Contains("VocalWarningTuningMutationGuardMask", vocalWarning);
            StringAssert.Contains("TryAcquireTuningMutationView", tuningWrite);
            StringAssert.Contains("ReleaseVocalWarningMutationGuard", tuningWrite);
            StringAssert.Contains("TryAcquireMutationGuard(VocalWarningTuningMutationGuardMask)", tuningAcquire);
            StringAssert.Contains("TryResolveHandle(in _tuningHandle", tuningAcquire);
            StringAssert.Contains("ReleaseVocalWarningMutationGuard", tuningAcquire);
            Assert.That(vocalWarning.IndexOf("TryAcquireWriteLock", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(vocalWarning.IndexOf("ReleaseWriteLock", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void VocalWarningEditorMockThreatsUseFrameMutationGuard()
        {
            string vocalWarning = Read(VocalWarningSystemPath);
            string mockInject = ExtractMethodBody(vocalWarning, "public bool EditorInjectMockThreats(");

            StringAssert.Contains("private bool TryResolveVwsOwnerViews(IDataVault vault, out VwsVaultViews views)", vocalWarning);
            StringAssert.Contains("TryAcquireVocalWarningFrameGuard", mockInject);
            StringAssert.Contains("TryResolveVwsOwnerViews(guardVault, out VwsVaultViews views)", mockInject);
            StringAssert.Contains("GenerateMockVocalThreatsJob", mockInject);
            StringAssert.Contains("ReleaseVocalWarningFrameGuard", mockInject);
            StringAssert.Contains("finally", mockInject);
            Assert.That(mockInject.IndexOf("TryAcquireWriteLock", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(mockInject.IndexOf("ReleaseWriteLock", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void VocalWarningFrameAndVisualSyncResolveGuardedVaultOnly()
        {
            string vocalWarning = Read(VocalWarningSystemPath);
            string ensureNative = ExtractMethodBody(vocalWarning, "private void EnsureNativeStorage()");
            string scheduleFrame = ExtractMethodBody(vocalWarning, "private JobHandle ScheduleVocalWarningFrame(");
            string visualSync = ExtractMethodBody(vocalWarning, "private void VisualSyncPresentationTick()");

            StringAssert.Contains("TryAcquireVocalWarningFrameGuard", ensureNative);
            StringAssert.Contains("TryResolveVwsOwnerViews(guardVault, out VwsVaultViews views)", ensureNative);
            StringAssert.Contains("ReleaseVocalWarningFrameGuard", ensureNative);
            StringAssert.Contains("TryAcquireVocalWarningFrameGuard", scheduleFrame);
            StringAssert.Contains("TryResolveVwsOwnerViews(guardVault, out VwsVaultViews views)", scheduleFrame);
            StringAssert.Contains("_pendingVocalWarningGuardVault = guardVault", scheduleFrame);
            StringAssert.Contains("TryAcquireVocalWarningFrameGuard", visualSync);
            StringAssert.Contains("TryResolveVwsOwnerViews(guardVault, out VwsVaultViews views)", visualSync);
            Assert.That(scheduleFrame.IndexOf("TryAcquireWriteLock", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(visualSync.IndexOf("TryAcquireWriteLock", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void VocalWarningTeardownQueueClearUsesFrameMutationGuard()
        {
            string vocalWarning = Read(VocalWarningSystemPath);
            string clearQueues = ExtractMethodBody(vocalWarning, "private void CancelRendererPlaybackAndClearQueues()");

            StringAssert.Contains("TryAcquireVocalWarningFrameGuard", clearQueues);
            StringAssert.Contains("TryResolveVwsOwnerViews(guardVault, out VwsVaultViews views)", clearQueues);
            StringAssert.Contains("CancelRendererPlaybackAndClearQueues(ref views, true)", clearQueues);
            StringAssert.Contains("ReleaseVocalWarningFrameGuard", clearQueues);
            StringAssert.Contains("finally", clearQueues);
            Assert.That(clearQueues.IndexOf("TryAcquireWriteLock", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(clearQueues.IndexOf("ReleaseWriteLock", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void SpatialListenerBasisUsesCachedRuntimeContextOnly()
        {
            string spatial = Read("Assets/_Project/Scripts/SpatialAudioManager.cs");
            string basis = ExtractMethodBody(spatial, "private void ResolveListenerBasis(");
            string forward = ExtractMethodBody(spatial, "private static bool TryResolveRuntimeContextForward(");

            StringAssert.Contains("IPlayerRuntimeContext runtimeContext = _cachedPlayerRuntimeContext", basis);
            Assert.That(basis.IndexOf("PlayerRuntimeContextService.TryGetActiveRuntimeContext", StringComparison.Ordinal), Is.LessThan(0));
            AssertNoHotDependencyLookups(basis);
            StringAssert.Contains("runtimeContext.TryGetLookRuntimeState(out PlayerLookState lookState)", forward);
            StringAssert.Contains("runtimeContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", forward);
            Assert.That(forward.IndexOf(".LookState", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(forward.IndexOf(".MovementState", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void ProloguePresentationTransferIsLateFramePhaseSafe()
        {
            string prologueAudio = Read(PrologueAudioPath);
            string lateFrame = ExtractMethodBody(prologueAudio, "public void LateFrameTick()");
            string publish = ExtractMethodBody(prologueAudio, "private void PublishAudioTransition(");
            string stress = ExtractMethodBody(prologueAudio, "private void ConsumeReentryAcousticStressSignals()");
            string arm = ExtractMethodBody(prologueAudio, "private void ArmOceanHandoffAudio()");
            string sweep = ExtractMethodBody(prologueAudio, "private void AdvanceFilterSweep(");
            string haptics = ExtractMethodBody(prologueAudio, "private void PublishSynchronizedHaptics(");

            AssertTextBefore(lateFrame, "ConsumeAtmosphericSignals();", "ConsumeReentryAcousticStressSignals();");
            AssertTextBefore(lateFrame, "ConsumeReentryAcousticStressSignals();", "ConsumePrologueCompleteSignals();");
            AssertTextBefore(lateFrame, "ConsumePrologueCompleteSignals();", "AdvanceFilterSweep(ResolveUnscaledDeltaTime());");
            AssertTextBefore(lateFrame, "AdvanceFilterSweep(ResolveUnscaledDeltaTime());", "PublishAudioTransition(frame);");
            StringAssert.Contains("_currentLowPassCutoffHertz = ClampCutoff(SplashdownLowPassCutoffHertz)", prologueAudio);
            StringAssert.Contains("_sweepStartLowPassCutoffHertz = _currentLowPassCutoffHertz", prologueAudio);
            StringAssert.Contains("_sweepSnapHeldForPublish = true", prologueAudio);
            StringAssert.Contains("signal.Phase == ReentryAcousticStressSignal.PhaseSplashdown", stress);
            StringAssert.Contains("ArmOceanHandoffAudio();", stress);
            int splashdownArmIndex = stress.IndexOf("ArmOceanHandoffAudio();", StringComparison.Ordinal);
            int splashdownReturnIndex = stress.IndexOf("return;", splashdownArmIndex, StringComparison.Ordinal);
            int whiteoutFallbackIndex = stress.IndexOf("if (signal.Phase == ReentryAcousticStressSignal.PhaseWhiteout)", splashdownArmIndex, StringComparison.Ordinal);
            Assert.That(whiteoutFallbackIndex, Is.GreaterThan(splashdownArmIndex));
            Assert.That(splashdownReturnIndex, Is.GreaterThan(splashdownArmIndex));
            Assert.That(splashdownReturnIndex, Is.LessThan(whiteoutFallbackIndex));
            StringAssert.Contains("_splashdownPending = true", arm);
            StringAssert.Contains("_sweepActive = true", arm);
            StringAssert.Contains("_stage = AudioTransitionState.StageOceanHandoff", arm);
            StringAssert.Contains("if (_sweepSnapHeldForPublish)", sweep);
            StringAssert.Contains("ClampCutoff(_sweepStartLowPassCutoffHertz)", sweep);
            Assert.That(sweep.IndexOf("_sweepSnapHeldForPublish = false", StringComparison.Ordinal), Is.LessThan(0));
            AssertTextBefore(sweep, "if (_sweepSnapHeldForPublish)", "float duration = PositiveFiniteOrMinimum(oceanFilterSweepSeconds, 0.001f);");
            AssertTextBefore(publish, "if (!audioService.QueuePrologueAudioTransition(in state))", "PublishSynchronizedHaptics(in state);");
            AssertTextBefore(
                publish,
                "if (!audioService.QueuePrologueAudioTransition(in state))",
                "if (_sweepSnapHeldForPublish &&");
            AssertTextBefore(publish, "_sweepSnapHeldForPublish = false;", "PublishSynchronizedHaptics(in state);");
            Assert.That(CountOccurrences(prologueAudio, "PublishSynchronizedHaptics("), Is.EqualTo(2));
            AssertNoHotDependencyLookups(stress);
            AssertNoForbiddenHotTokens(stress);
            AssertNoHotDependencyLookups(arm);
            AssertNoForbiddenHotTokens(arm);
            AssertNoHotDependencyLookups(haptics);
            AssertNoForbiddenHotTokens(haptics);
        }

        [Test]
        public void DataVaultMutationGuardsAreSingleRouteAndFinallyReleased()
        {
            string renderer = Read(RendererPath);
            string produce = ExtractMethodBody(renderer, "private void ProduceAudioBlock(int frameCount)");
            string canProduce = ExtractMethodBody(renderer, "private bool CanProduceAudioBlock(");
            string enqueue = ExtractMethodBody(renderer, "public bool QueuePrologueAudioTransition(");
            string dequeue = ExtractMethodBody(renderer, "private bool TryDequeuePrologueTransitionState(");
            string director = Read(DirectorPath);
            string publishReentry = ExtractMethodBody(director, "private void PublishReentryStateNoThrow()");
            string recordStage = ExtractMethodBody(director, "private void RecordStage(");

            Assert.That(CountOccurrences(canProduce, "TryAcquirePlayerCriticalMutationGuard("), Is.EqualTo(1));
            StringAssert.Contains("AudioBlockDspMutationGuardMask", canProduce);
            StringAssert.Contains("finally", canProduce);
            StringAssert.Contains("ReleasePlayerCriticalMutationGuard(guardVault, AudioBlockDspMutationGuardMask)", canProduce);
            Assert.That(produce.IndexOf("TryAcquirePlayerCriticalMutationGuard(", StringComparison.Ordinal), Is.LessThan(0));
            StringAssert.Contains("finally", produce);
            StringAssert.Contains("ReleaseSonarDspMutationGuard(ref sonarDspViews)", produce);
            StringAssert.Contains("ReleaseFrameScratchMutationGuard(ref frameViews)", produce);

            Assert.That(CountOccurrences(enqueue, "TryAcquirePlayerCriticalMutationBuffer("), Is.EqualTo(1));
            StringAssert.Contains("finally", enqueue);
            StringAssert.Contains("ReleasePlayerCriticalMutationGuard(guardVault, PrologueTransitionRingMutationGuardMask)", enqueue);
            Assert.That(CountOccurrences(dequeue, "TryAcquirePlayerCriticalMutationBuffer("), Is.EqualTo(1));
            StringAssert.Contains("finally", dequeue);
            StringAssert.Contains("ReleasePlayerCriticalMutationGuard(guardVault, PrologueTransitionRingMutationGuardMask)", dequeue);

            Assert.That(CountOccurrences(publishReentry, "TryAcquireWriteLock("), Is.EqualTo(1));
            Assert.That(CountOccurrences(publishReentry, "ReleaseWriteLock("), Is.EqualTo(1));
            StringAssert.Contains("finally", publishReentry);
            Assert.That(publishReentry.IndexOf("RecordStage(", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(CountOccurrences(recordStage, "TryAcquireWriteLock("), Is.EqualTo(1));
            Assert.That(CountOccurrences(recordStage, "ReleaseWriteLock("), Is.EqualTo(1));
            StringAssert.Contains("finally", recordStage);
            Assert.That(recordStage.IndexOf("PublishReentryStateNoThrow(", StringComparison.Ordinal), Is.LessThan(0));
        }

        [Test]
        public void MockVacuumLowPassBiquadRejectsOneKilohertzByFortyDb()
        {
            const double sampleRate = 48000d;
            const double cutoff = 150d;
            const double q = 0.7071067811865476d;
            BiquadCoefficients coefficients = ComputeLowPass(cutoff, q, sampleRate);

            double lowMagnitude = EvaluateMagnitude(coefficients, 100d, sampleRate);
            double highMagnitude = EvaluateMagnitude(coefficients, 1000d, sampleRate);
            double cascadedRatio = (highMagnitude * highMagnitude) / Math.Max(lowMagnitude * lowMagnitude, 1e-12d);
            double attenuationDb = 20d * Math.Log10(Math.Max(cascadedRatio, 1e-12d));

            Assert.That(attenuationDb, Is.LessThan(-40d));
        }

        private static string Read(string assetPath)
        {
            string absolutePath = Path.Combine(Directory.GetCurrentDirectory(), assetPath.Replace('/', Path.DirectorySeparatorChar));
            Assert.That(File.Exists(absolutePath), Is.True, "Missing asset: " + assetPath);
            return File.ReadAllText(absolutePath);
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.That(signatureIndex, Is.GreaterThanOrEqualTo(0), "Missing method: " + signature);
            int braceStart = source.IndexOf('{', signatureIndex);
            Assert.That(braceStart, Is.GreaterThanOrEqualTo(0), "Missing method brace: " + signature);

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

            Assert.Fail("Unterminated method body: " + signature);
            return string.Empty;
        }

        private static string ExtractMethodBodyAfter(string source, string anchor, string signature)
        {
            int anchorIndex = source.IndexOf(anchor, StringComparison.Ordinal);
            Assert.That(anchorIndex, Is.GreaterThanOrEqualTo(0), "Missing anchor: " + anchor);
            return ExtractMethodBody(source.Substring(anchorIndex), signature);
        }

        private static void AssertNoForbiddenHotTokens(string source)
        {
            Assert.That(source.IndexOf("new ", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(source.IndexOf("lock", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(source.IndexOf("File.", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(source.IndexOf("GlobalRegistry.", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(source.IndexOf("GetComponent", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(source.IndexOf("Thread.Sleep", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(source.IndexOf(".ToString(", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(source.IndexOf("foreach", StringComparison.Ordinal), Is.LessThan(0));
        }

        private static void AssertNoHotDependencyLookups(string source)
        {
            Assert.That(source.IndexOf("GlobalRegistry.Get<", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(source.IndexOf("GlobalRegistry.", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(source.IndexOf("GetComponent(", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(source.IndexOf("TryGetComponent", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(source.IndexOf("File.", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(source.IndexOf("Thread.Sleep", StringComparison.Ordinal), Is.LessThan(0));
        }

        private static void AssertAudioServiceUsableBody(string source)
        {
            StringAssert.Contains("audioService == null || !audioService.IsInitialized", source);
            StringAssert.Contains("audioService is Behaviour behaviour", source);
            StringAssert.Contains("return behaviour != null && behaviour.isActiveAndEnabled", source);
        }

        private static void AssertAudioRuntimeObjectUsableBody(string source)
        {
            StringAssert.Contains("runtime == null", source);
            StringAssert.Contains("runtime is IAudioService audioService && !audioService.IsInitialized", source);
            StringAssert.Contains("runtime is Behaviour behaviour", source);
            StringAssert.Contains("return behaviour != null && behaviour.isActiveAndEnabled", source);
        }

        private static void AssertNoDirectManagedCallbackHazards(string source)
        {
            Assert.That(source.IndexOf("GlobalRegistry.Get<", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(source.IndexOf("GlobalRegistry.", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(source.IndexOf("GetComponent(", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(source.IndexOf("TryGetComponent", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(source.IndexOf("File.", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(source.IndexOf("Thread.Sleep", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(source.IndexOf("lock (", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(source.IndexOf("Monitor.", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(source.IndexOf("new ", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(source.IndexOf(".ToString(", StringComparison.Ordinal), Is.LessThan(0));
        }

        private static void AssertTextBefore(string source, string first, string second)
        {
            int firstIndex = source.IndexOf(first, StringComparison.Ordinal);
            int secondIndex = source.IndexOf(second, StringComparison.Ordinal);
            Assert.That(firstIndex, Is.GreaterThanOrEqualTo(0), "Missing token: " + first);
            Assert.That(secondIndex, Is.GreaterThanOrEqualTo(0), "Missing token: " + second);
            Assert.That(firstIndex, Is.LessThan(secondIndex), first + " must precede " + second);
        }

        private static int CountOccurrences(string source, string token)
        {
            int count = 0;
            int index = source.IndexOf(token, StringComparison.Ordinal);
            while (index >= 0)
            {
                count++;
                index = source.IndexOf(token, index + token.Length, StringComparison.Ordinal);
            }

            return count;
        }

        private static BiquadCoefficients ComputeLowPass(double cutoffHertz, double q, double sampleRate)
        {
            double omega = 2d * Math.PI * cutoffHertz / sampleRate;
            double sine = Math.Sin(omega);
            double cosine = Math.Cos(omega);
            double alpha = sine / (2d * q);
            double inverseA0 = 1d / (1d + alpha);

            BiquadCoefficients coefficients = default;
            coefficients.B0 = ((1d - cosine) * 0.5d) * inverseA0;
            coefficients.B1 = (1d - cosine) * inverseA0;
            coefficients.B2 = coefficients.B0;
            coefficients.A1 = (-2d * cosine) * inverseA0;
            coefficients.A2 = (1d - alpha) * inverseA0;
            return coefficients;
        }

        private static double EvaluateMagnitude(BiquadCoefficients coefficients, double frequencyHertz, double sampleRate)
        {
            double omega = 2d * Math.PI * frequencyHertz / sampleRate;
            double numeratorReal = coefficients.B0 + coefficients.B1 * Math.Cos(omega) + coefficients.B2 * Math.Cos(2d * omega);
            double numeratorImag = -coefficients.B1 * Math.Sin(omega) - coefficients.B2 * Math.Sin(2d * omega);
            double denominatorReal = 1d + coefficients.A1 * Math.Cos(omega) + coefficients.A2 * Math.Cos(2d * omega);
            double denominatorImag = -coefficients.A1 * Math.Sin(omega) - coefficients.A2 * Math.Sin(2d * omega);
            double numerator = numeratorReal * numeratorReal + numeratorImag * numeratorImag;
            double denominator = denominatorReal * denominatorReal + denominatorImag * denominatorImag;
            return Math.Sqrt(numerator / Math.Max(denominator, 1e-12d));
        }

        private struct BiquadCoefficients
        {
            public double B0;
            public double B1;
            public double B2;
            public double A1;
            public double A2;
        }
    }
}
