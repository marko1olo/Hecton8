namespace Hecton8.Tests.Editor
{
    using System;
    using System.IO;
    using NUnit.Framework;

    public sealed class AudioEnvironment1618EditTests
    {
        private const string RendererPath = "Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs";
        private const string PrologueAudioPath = "Assets/_Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs";
        private const string DirectorPath = "Assets/_Project/Scripts/Narrative/Prologue/AwaitableDropSequenceDirector.cs";
        private const string MainMenuScenePath = "Assets/_Project/Scenes/01_MAIN_MENU.unity";
        private const string WorldScenePath = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";
        private const string MasterMixerPath = "Assets/_Project/MasterMixer.mixer";
        private const string PrologueSignalWarmupPath = "Assets/_Project/Scripts/Core/Signals/PrologueReentrySignals.cs";
        private const string MusicDirectorPath = "Assets/_Project/Scripts/Audio/HectonMusicDirector.cs";
        private const string MusicDirectorConfigPath = "Assets/_Project/Data/Audio/Music/Configs/MusicDirectorConfig_Global.asset";
        private const string MusicDirectorPrefabPath = "Assets/_Project/Prefabs/Audio/PFB_HectonMusicDirectorRoot.prefab";
        private const string DynamicMusicSignalPath = "Assets/_Project/Scripts/Core/Contracts/Signals/DynamicMusicScalarSignal.cs";
        private const string SignalBusRuntimePath = "Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs";
        private const string DynamicMusicSynthPath = "Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs";
        private const string SystemsDebugUiPath = "Assets/_Project/Scripts/UI/HectonSystemsDebugUI.cs";
        private const string SettingsManagerPath = "Assets/_Project/Scripts/UI/SettingsManager.cs";
        private const string ObjectPoolManagerPath = "Assets/_Project/Scripts/ObjectPoolManager.cs";
        private const string AudioLogPickupPath = "Assets/_Project/Scripts/AudioLog/AudioLogPickup.cs";
        private const string VocalBankRuntimePath = "Assets/_Project/Scripts/Audio/Synthesis/VocalBankPlaybackRuntime.cs";
        private const string VocalWarningSystemPath = "Assets/_Project/Scripts/Audio/VocalWarningSystem.cs";
        private const string SoundscapeSystemPath = "Assets/_Project/Scripts/World/SoundscapeSystem.cs";
        private const string AcousticZoneControllerPath = "Assets/_Project/Scripts/AcousticZoneController.cs";
        private const string AdaptiveStemMixerPath = "Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs";

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

            StringAssert.Contains("TryBindFromCachedRuntimeContext();", tick);
            StringAssert.Contains("TryBindFromCachedRuntimeContext();", slowTick);
            StringAssert.Contains("IPlayerRuntimeContext playerContext = _playerRuntimeContext", binder);
            StringAssert.Contains("BindToPlayerRuntimeContext(playerContext)", binder);
            AssertNoHotDependencyLookups(binder);
            Assert.That(binder.IndexOf("PlayerRuntimeContextService", StringComparison.Ordinal), Is.LessThan(0));
            Assert.That(renderer.IndexOf("PlayerRuntimeContextService.ActiveRuntimeContext", StringComparison.Ordinal), Is.LessThan(0));
            StringAssert.Contains("_playerRuntimeContext = CacheReadyPlayerRuntime(GlobalRegistry.Player)", cold);
            StringAssert.Contains("_playerRuntimeContext = CacheReadyPlayerRuntime(currentService as IPlayerRuntimeContext)", rebound);
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
            StringAssert.Contains("ObjectPoolManager.ActiveRuntimeInstance", resolvePool);
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
            StringAssert.Contains("IAudioLogRuntime audioLogRuntime = ResolveAudioLogRuntime()", audioLogDuck);
            StringAssert.Contains("audioLogRuntime.IsPlaying || audioLogRuntime.IsNarrativeQueueBlocked", audioLogDuck);
            StringAssert.Contains("NarrativeAudioLogMusicDuck01", audioLogDuck);
            StringAssert.Contains("math.max(_vocalWarningMusicDuck01, _narrativeAudioLogMusicDuck01)", speechDuckResolve);
            StringAssert.Contains("return IsAudioLogRuntimeUsable(audioLogRuntime) ? audioLogRuntime : null", audioLogResolve);
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
            string sync = ExtractMethodBody(soundscape, "private void SyncMusicDirectorSoundscapeContext(");
            string syncCached = ExtractMethodBody(soundscape, "private void SyncCachedMusicDirectorSoundscapeContext(");
            string cacheMusic = ExtractMethodBody(soundscape, "private void CacheMusicDirector(");
            string setContext = ExtractMethodBody(musicDirector, "public void SetSoundscapeTierContext(");
            string resolveProfile = ExtractMethodBody(musicDirector, "private HectonMusicBiomeProfile ResolveProfile(");
            string soundscapeProfile = ExtractMethodBody(musicDirector, "private HectonMusicBiomeProfile ResolveSoundscapeTierProfile(");
            string resolveLayerDepth = ExtractMethodBody(musicDirector, "private float ResolveLayerDepthMeters()");
            string tension = ExtractMethodBody(musicDirector, "private float ResolveTension01()");
            string route = ExtractMethodBody(musicDirector, "private void UpdateLayerRouting(");

            StringAssert.Contains("CacheMusicDirector(GlobalRegistry.MusicDirector)", onEnable);
            StringAssert.Contains("SyncMusicDirectorSoundscapeContext(_currentTier", onEnable);
            StringAssert.Contains("_musicDirector = null", onDisable);
            StringAssert.Contains("_musicDirector = null", onDestroy);
            StringAssert.Contains("SyncMusicDirectorSoundscapeContext(newTier, depth)", slowTick);
            AssertTextBefore(slowTick, "SyncMusicDirectorSoundscapeContext(newTier, depth)", "if (newTier == _currentTier)");
            StringAssert.Contains("director.SetSoundscapeTierContext(CalculateTier(depthMeters, _currentTier), depthMeters)", depthTier);
            StringAssert.Contains("GlobalRegistryServiceSlot.MusicDirectorRuntime", rebound);
            StringAssert.Contains("CacheMusicDirector(currentService as HectonMusicDirector)", rebound);
            StringAssert.Contains("SyncCachedMusicDirectorSoundscapeContext(_currentTier, survivalSystem != null ? survivalSystem.Depth : 0f)", rebound);
            StringAssert.Contains("GlobalRegistryServiceSlot.Player", rebound);
            StringAssert.Contains("SyncMusicDirectorSoundscapeContext(_currentTier, survivalSystem != null ? survivalSystem.Depth : 0f)", rebound);
            StringAssert.Contains("GlobalRegistryServiceSlot.MusicDirectorRuntime", hotSwap);
            StringAssert.Contains("CacheMusicDirector(currentService as HectonMusicDirector)", hotSwap);
            StringAssert.Contains("SyncCachedMusicDirectorSoundscapeContext(_currentTier, survivalSystem != null ? survivalSystem.Depth : 0f)", hotSwap);
            StringAssert.Contains("GlobalRegistryServiceSlot.Player", hotSwap);
            StringAssert.Contains("SyncMusicDirectorSoundscapeContext(_currentTier, survivalSystem != null ? survivalSystem.Depth : 0f)", hotSwap);
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
            StringAssert.Contains("return math.max(0f, _soundscapeDepthHintMeters);", resolveLayerDepth);
            StringAssert.Contains("soundscapePressure01 * _soundscapePressureWeight", tension);
            StringAssert.Contains("_debugSoundscapePressure01 = soundscapePressure01", tension);
            StringAssert.Contains("math.max(InverseLerp(20f, 900f, depthMeters), soundscapePressure01)", route);
            StringAssert.Contains("soundscapePressure01 * 0.18f", route);
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
