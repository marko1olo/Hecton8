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
        private const string PrologueSignalWarmupPath = "Assets/_Project/Scripts/Core/Signals/PrologueReentrySignals.cs";
        private const string MusicDirectorPath = "Assets/_Project/Scripts/Audio/HectonMusicDirector.cs";
        private const string DynamicMusicSynthPath = "Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs";
        private const string VocalBankRuntimePath = "Assets/_Project/Scripts/Audio/Synthesis/VocalBankPlaybackRuntime.cs";

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
        public void ManagedAudioCallbacksStayTransferOnlyAndDirectLookupFree()
        {
            string dynamicMusic = ExtractMethodBody(Read(DynamicMusicSynthPath), "private void OnAudioFilterRead(");
            string vocalBank = ExtractMethodBody(Read(VocalBankRuntimePath), "private void OnAudioFilterRead(");

            AssertNoDirectManagedCallbackHazards(dynamicMusic);
            AssertNoDirectManagedCallbackHazards(vocalBank);
            StringAssert.Contains("ZeroManagedAudioBuffer(data", dynamicMusic);
            StringAssert.Contains("ZeroManagedAudioBuffer(data", vocalBank);
            StringAssert.Contains("Volatile.Read", dynamicMusic);
            StringAssert.Contains("Volatile.Read", vocalBank);
            StringAssert.Contains("fixed (float* destination = data)", dynamicMusic);
            StringAssert.Contains("fixed (float* output = data)", vocalBank);
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
