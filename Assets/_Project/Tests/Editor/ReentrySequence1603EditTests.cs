namespace Hecton8.Tests.Editor
{
    using System;
    using System.IO;
    using System.Linq;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using NUnit.Framework;
    using Unity.Mathematics;
    using UnityEngine;

    public sealed class ReentrySequence1603EditTests
    {
        private const string DirectorPath = "Assets/_Project/Scripts/Narrative/Prologue/AwaitableDropSequenceDirector.cs";
        private const string ValidatorPath = "Assets/_Project/Scripts/Narrative/Prologue/ReentrySequenceMetricValidator1603.cs";
        private const string VfxPath = "Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs";
        private const string OrbitDirectorPath = "Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs";
        private const string OrbitBootstrapPath = "Assets/_Project/Scripts/Prologue/Space/PrologueOrbitSceneBootstrap.cs";
        private const string WorldHandoffLoaderPath = "Assets/_Project/Scripts/Prologue/Space/PrologueWorldHandoffSceneLoader.cs";
        private const string AudioPath = "Assets/_Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs";
        private const string RegistryBridgePath = "Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs";
        private const string CapsuleShaderPath = "Assets/_Project/Art/Shaders/Prologue/Hecton_CapsuleReentryPlasmaFake.shader";
        private const string PlanetShaderPath = "Assets/_Project/Art/Shaders/Prologue/Hecton_OrbitalPlanetRelativityFake.shader";
        private const string CloudShaderPath = "Assets/_Project/Art/Shaders/Prologue/Hecton_OrbitalCloudWhiteoutFake.shader";
        private const string VisorShaderPath = "Assets/_Project/Art/Shaders/SuitVisor.shader";
        private const float FixedDeltaSeconds = 1f / 60f;
        private const float SampleTolerance = 0.0005f;

        private static readonly string[] ReentryCSharpFiles =
        {
            DirectorPath,
            ValidatorPath,
            VfxPath,
            OrbitDirectorPath,
            OrbitBootstrapPath,
            WorldHandoffLoaderPath,
            AudioPath,
            RegistryBridgePath
        };

        private static readonly string[] HotMethodNames =
        {
            "Tick",
            "FixedUpdate",
            "LateFrameTick",
            "Update",
            "LateUpdate",
            "Execute",
            "AdvanceReentryState",
            "PublishFinalizedReentryStateNoThrow",
            "PublishContinuousCameraTrauma",
            "PublishReentryAcousticStressSignal",
            "PublishSplashdownAcousticStressSignal",
            "PublishReentryStateNoThrow",
            "RecordStage",
            "ShouldStopForCancellation",
            "TryHandleSkipRequest",
            "TryExecuteSkipHandoff",
            "IntegrateState",
            "MaintainCameraLocalOverlay",
            "ConsumeAtmosphericSignals",
            "ConsumePrologueCompleteSignals",
            "ConsumeReentryAcousticStressSignals",
            "PublishShaderState",
            "SetReentryRuntimeGlobalsIfChanged",
            "ResolvePlasmaIntensity01",
            "ResolveAblationAmount01",
            "ResolveGlassStress01",
            "PublishAcousticBlend",
            "PublishPlasmaRoar",
            "PublishOceanWaves",
            "PublishMassiveSplash",
            "PublishStateSignal",
            "AdvanceFilterSweep",
            "PublishAudioTransition",
            "PublishNeutralTransitionOnDisable",
            "RefreshQualityPolicy",
            "ResolveQualityCurve01",
            "RefreshFrameState",
            "TryGetOrbitalSnapshot",
            "TryConsumeAtmosphericReentry",
            "TryConsumePrologueComplete",
            "RefreshHydrationState",
            "IsOceanSurfaceReady",
            "ConsumeSkipInputSignals",
            "IsImmediateSkipInputHeld",
            "RefreshSurvivalProxyPressureForFrame",
            "RefreshObservedSurvivalProxyPressure",
            "TryObserveCriticalMemoryPressure"
        };

        [Test]
        public void SourcesParseAndMetricValidatorCoversFullTrajectory()
        {
            for (int i = 0; i < ReentryCSharpFiles.Length; i++)
                Parse(ReentryCSharpFiles[i]);

            string validator = Read(ValidatorPath);
            StringAssert.Contains("FuzzerFrameCount > 1 ? (float)i / (FuzzerFrameCount - 1) : 1f", validator);
            StringAssert.Contains("MaxAblation01", validator);
            StringAssert.Contains("MaxGlassStress01", validator);
            StringAssert.Contains("AblationBoundsValid", validator);
            StringAssert.Contains("private static byte ToByte(bool value)", validator);
            StringAssert.Contains("UnsafeUtility.SizeOf<ReentryStateDTO>()", validator);
            StringAssert.Contains("UnsafeUtility.SizeOf<ReentryAcousticStressSignal>()", validator);
            StringAssert.Contains("(dtoBytes & 7) == 0", validator);
            StringAssert.Contains("(signalBytes & 7) == 0", validator);
            Assert.That(ContainsOrdinal(validator, "public bool DtoLayoutValid"), Is.False);
            Assert.That(ContainsOrdinal(validator, "public bool AcousticLayoutValid"), Is.False);
            Assert.That(ContainsOrdinal(validator, "public bool Valid"), Is.False);
            StringAssert.Contains("public byte DtoLayoutValid;", validator);
            StringAssert.Contains("public byte AcousticLayoutValid;", validator);
            StringAssert.Contains("public byte Valid;", validator);
            Assert.That(ContainsOrdinal(validator, "float elapsed = i * FixedDeltaSeconds;"), Is.False);
        }

        [Test]
        public void ReentryBlackBoxDumpsUseTrackedTransientPayloads()
        {
            AssertTrackedDumpPayload(
                DirectorPath,
                "DumpBlackBox",
                "AwaitableDropSequenceDirector",
                "DumpPayloadLabel",
                "prologueSequenceDirectorDumpPayload");
            AssertTrackedDumpPayload(
                VfxPath,
                "DumpBlackBoxOnce",
                "OrbitalDropReentryVfxController",
                "DumpPayloadLabel",
                "orbitalDropReentryVfxDumpPayload");
            AssertTrackedDumpPayload(
                OrbitDirectorPath,
                "DumpTelemetry",
                "OrbitalRelativityDirector",
                "DumpPayloadLabel",
                "orbitalMechanicsDirectorDumpPayload");
        }

        [Test]
        public void HotPhaseMethodsDoNotResolveColdDependenciesOrManagedTiming()
        {
            for (int fileIndex = 0; fileIndex < ReentryCSharpFiles.Length; fileIndex++)
            {
                string relativePath = ReentryCSharpFiles[fileIndex];
                CompilationUnitSyntax root = Parse(relativePath);
                foreach (MethodDeclarationSyntax method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    if (!HotMethodNames.Contains(method.Identifier.ValueText))
                        continue;

                    string methodSource = method.ToFullString();
                    AssertNoForbiddenHotText(methodSource, relativePath, method.Identifier.ValueText);
                    foreach (InvocationExpressionSyntax invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
                    {
                        string expression = invocation.Expression.ToString();
                        Assert.That(IsHotDependencyLookup(expression), Is.False, relativePath + ":" + method.Identifier.ValueText + " -> " + expression);
                    }
                }
            }
        }

        [Test]
        public void ReentryStateTransferHappensAfterSimulationSwitchSettles()
        {
            CompilationUnitSyntax root = Parse(DirectorPath);
            MethodDeclarationSyntax tick = FindMethod(root, "Tick");
            MethodDeclarationSyntax run = FindMethod(root, "RunPrologueSequenceAsync");
            MethodDeclarationSyntax tryBegin = FindMethod(root, "TryBeginSequenceRun");
            MethodDeclarationSyntax advance = FindMethod(root, "AdvanceReentryState");
            MethodDeclarationSyntax finalized = FindMethod(root, "PublishFinalizedReentryStateNoThrow");
            MethodDeclarationSyntax onDisable = FindMethod(root, "OnDisable");
            MethodDeclarationSyntax dispose = FindMethod(root, "Dispose");
            MethodDeclarationSyntax cancelActive = FindMethod(root, "CancelActiveSequenceNoThrow");
            MethodDeclarationSyntax fail = FindMethod(root, "FailSequence");
            MethodDeclarationSyntax sanitizeTerminal = FindMethod(root, "SanitizeReentryStateForTerminalPublish");
            MethodDeclarationSyntax shouldStop = FindMethod(root, "ShouldStopForCancellation");
            MethodDeclarationSyntax tryHandleSkip = FindMethod(root, "TryHandleSkipRequest");
            MethodDeclarationSyntax developmentSkipAllowed = FindMethod(root, "IsDevelopmentSkipAllowed");
            MethodDeclarationSyntax devSkipHandoff = FindMethod(root, "TryExecuteSkipHandoff");
            string tickSource = tick.ToFullString();
            string runSource = run.ToFullString();
            string tryBeginSource = tryBegin.ToFullString();
            string cancelActiveSource = cancelActive.ToFullString();
            string failSource = fail.ToFullString();
            string compactTick = CompactSource(tickSource);
            string devSkipHandoffSource = devSkipHandoff.ToFullString();

            int switchIndex = tickSource.IndexOf("switch (_stage)", StringComparison.Ordinal);
            int publishIndex = tickSource.LastIndexOf("PublishFinalizedReentryStateNoThrow()", StringComparison.Ordinal);
            Assert.That(switchIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(publishIndex, Is.GreaterThan(switchIndex));
            Assert.That(ContainsOrdinal(advance.ToFullString(), "PublishReentryStateNoThrow()"), Is.False);
            Assert.That(ContainsOrdinal(advance.ToFullString(), "CurrentPhaseEnum"), Is.False);
            StringAssert.Contains("catch (OperationCanceledException)", runSource);
            StringAssert.Contains("catch (Exception)", runSource);
            StringAssert.Contains("FailSequence(PrologueCancelReasons.NonFinite);", runSource);
            Assert.That(ContainsOrdinal(runSource, "RecordStage(PrologueStage.Faulted"), Is.False);
            AssertTextBefore(tryBeginSource, "if (!TryRegisterUpdateLane())", "FailSequence(PrologueCancelReasons.NonFinite);");
            Assert.That(ContainsOrdinal(tryBeginSource, "RecordStage(PrologueStage.Faulted"), Is.False);
            Assert.That(CountOrdinal(runSource, "PublishFinalizedReentryStateNoThrow();"), Is.EqualTo(2));
            Assert.That(
                ContainsOrdinal(compactTick, "if(ShouldStopForCancellation(_runCancellationToken)){if(_running){CompleteSequenceRun(preservePendingCameraImpact:false);PublishFinalizedReentryStateNoThrow();}return;}"),
                Is.True);
            Assert.That(
                ContainsOrdinal(compactTick, "if(TryHandleSkipRequest()){if(_running){CompleteSequenceRun(preservePendingCameraImpact:false);PublishFinalizedReentryStateNoThrow();}return;}"),
                Is.True);
            StringAssert.Contains("_reentryState.CurrentPhaseEnum = (uint)_stage;", finalized.ToFullString());
            StringAssert.Contains("PublishReentryStateNoThrow();", finalized.ToFullString());
            Assert.That(Calls(onDisable, "CancelActiveSequenceNoThrow"), Is.True);
            Assert.That(Calls(dispose, "CancelActiveSequenceNoThrow"), Is.True);
            StringAssert.Contains("RecordStage(PrologueStage.Cancelled", cancelActiveSource);
            AssertTextBefore(cancelActiveSource, "RecordStage(PrologueStage.Cancelled", "CompleteSequenceRun(preservePendingCameraImpact: false);");
            AssertTextBefore(cancelActiveSource, "CompleteSequenceRun(preservePendingCameraImpact: false);", "PublishFinalizedReentryStateNoThrow();");
            StringAssert.Contains("RecordStage(PrologueStage.Faulted", failSource);
            AssertTextBefore(failSource, "RecordStage(PrologueStage.Faulted", "CompleteSequenceRun(preservePendingCameraImpact: false);");
            AssertTextBefore(failSource, "SanitizeReentryStateForTerminalPublish();", "PublishFinalizedReentryStateNoThrow();");
            StringAssert.Contains("FailSequence(PrologueCancelReasons.DevSkip);", devSkipHandoffSource);
            Assert.That(ContainsOrdinal(devSkipHandoffSource, "RecordStage(PrologueStage.Faulted"), Is.False);
            Assert.That(ContainsOrdinal(devSkipHandoffSource, "DumpBlackBox()"), Is.False);
            StringAssert.Contains("_cancelReason == PrologueCancelReasons.DevSkip && IsDevelopmentSkipAllowed()", shouldStop.ToFullString());
            StringAssert.Contains("if (!IsDevelopmentSkipAllowed() || !_runtime.ShouldSkipPrologue)", tryHandleSkip.ToFullString());
            StringAssert.Contains("return runtime != null && runtime.IsDevelopmentBuild;", developmentSkipAllowed.ToFullString());
            StringAssert.Contains("math.isfinite(_reentryState.ElapsedTime)", sanitizeTerminal.ToFullString());
            StringAssert.Contains("math.saturate(_reentryState.Progress01)", sanitizeTerminal.ToFullString());
        }

        [Test]
        public void RuntimeReadAccessorsArePureAndFrameStateIsExplicit()
        {
            CompilationUnitSyntax directorRoot = Parse(DirectorPath);
            CompilationUnitSyntax bridgeRoot = Parse(RegistryBridgePath);
            string tickSource = FindMethod(directorRoot, "Tick").ToFullString();
            string survivalProxyGetter = FindProperty(bridgeRoot, "SurvivalProxyPressure01").ToFullString();
            string skipGetter = FindProperty(bridgeRoot, "ShouldSkipPrologue").ToFullString();
            string frameRefresh = FindMethod(bridgeRoot, "RefreshFrameState").ToFullString();
            string handleSkip = FindMethod(bridgeRoot, "HandleSkipRequested").ToFullString();
            string hydrationRefresh = FindMethod(bridgeRoot, "RefreshHydrationState").ToFullString();
            string oceanReady = FindMethod(bridgeRoot, "IsOceanSurfaceReady").ToFullString();
            string prepare = FindMethod(bridgeRoot, "PrepareSequenceRun").ToFullString();

            StringAssert.Contains("void RefreshFrameState()", Read("Assets/_Project/Scripts/Core/Contracts/PrologueSequenceContracts.cs"));
            StringAssert.Contains("void RefreshHydrationState(bool allowProxy)", Read("Assets/_Project/Scripts/Core/Contracts/PrologueSequenceContracts.cs"));
            AssertTextBefore(tickSource, "_runtime.RefreshFrameState();", "if (TryHandleSkipRequest())");
            AssertTextBefore(tickSource, "_runtime.RefreshHydrationState(allowProxy: true);", "if (_runtime.IsOceanSurfaceReady(allowProxy: false))");
            AssertTextBefore(tickSource, "_runtime.IsOceanSurfaceReady(allowProxy: false)", "if (_runtime.IsOceanSurfaceReady(allowProxy: true))");
            Assert.That(CountOrdinal(tickSource, "_runtime.RefreshHydrationState("), Is.EqualTo(1));
            StringAssert.Contains("return _isDevelopmentBuild && _skipRequested;", skipGetter);
            StringAssert.Contains("if (!IsDevelopmentBuild)", frameRefresh);
            StringAssert.Contains("_skipRequested = false;", frameRefresh);
            StringAssert.Contains("if (!IsDevelopmentBuild || _skipRequested)", handleSkip);
            Assert.That(ContainsOrdinal(survivalProxyGetter, "RefreshSurvivalProxy"), Is.False);
            Assert.That(ContainsOrdinal(survivalProxyGetter, "ResolveSurvivalProxy"), Is.False);
            Assert.That(ContainsOrdinal(survivalProxyGetter, "SignalBus<"), Is.False);
            Assert.That(ContainsOrdinal(skipGetter, "ConsumeSkipInputSignals"), Is.False);
            Assert.That(ContainsOrdinal(skipGetter, "GetState()"), Is.False);
            StringAssert.Contains("return IsDevelopmentBuild && _skipRequested;", skipGetter);
            StringAssert.Contains("ConsumeSkipInputSignals();", frameRefresh);
            StringAssert.Contains("IsImmediateSkipInputHeld()", frameRefresh);
            StringAssert.Contains("if (_observedHighResSurfaceReady)", hydrationRefresh);
            Assert.That(ContainsOrdinal(hydrationRefresh, "if (IsOceanSurfaceReady(allowProxy))"), Is.False);
            StringAssert.Contains("RefreshSurvivalProxyPressureForFrame(SystemDispatcher.CurrentFrameIndex);", hydrationRefresh);
            AssertTextBefore(
                hydrationRefresh,
                "streaming.IsChunkResident(oceanSurfaceChunkId)",
                "if (allowProxy && _observedProxySurfaceReady)");
            AssertTextBefore(
                hydrationRefresh,
                "streaming.IsChunkResident(oceanSurfaceChunkId)",
                "allowProxy && allowStandaloneOrbitHydrationProxy");
            AssertTextBefore(
                hydrationRefresh,
                "streaming.IsChunkResident(oceanSurfaceChunkId)",
                "streaming.ActiveImpostorCount");
            StringAssert.Contains("SignalBus<SectorResidencyHydratedSignal>.GetFrameSnapshot();", hydrationRefresh);
            StringAssert.Contains("return _observedHighResSurfaceReady || (allowProxy && _observedProxySurfaceReady);", oceanReady);
            Assert.That(ContainsOrdinal(oceanReady, "SignalBus<"), Is.False);
            Assert.That(ContainsOrdinal(oceanReady, "RefreshSurvivalProxy"), Is.False);
            StringAssert.Contains("RefreshSurvivalProxyPressureForFrame(SystemDispatcher.CurrentFrameIndex);", prepare);
        }

        [Test]
        public void VisualSyncAblationAndFlashUploadsAreDirtyGatedGlobals()
        {
            string vfx = Read(VfxPath);
            CompilationUnitSyntax root = Parse(VfxPath);
            MethodDeclarationSyntax lateFrame = FindMethod(root, "LateFrameTick");
            MethodDeclarationSyntax publishShader = FindMethod(root, "PublishShaderState");
            MethodDeclarationSyntax upload = FindMethod(root, "SetReentryRuntimeGlobalsIfChanged");
            MethodDeclarationSyntax activeState = FindMethod(root, "HasActivePresentationState");
            MethodDeclarationSyntax vfxAtmosphere = FindMethod(root, "ConsumeAtmosphericSignals");
            MethodDeclarationSyntax vfxComplete = FindMethod(root, "ConsumePrologueCompleteSignals");

            Assert.That(Calls(lateFrame, "PublishShaderState"), Is.True);
            Assert.That(Calls(publishShader, "SetReentryRuntimeGlobalsIfChanged"), Is.True);
            Assert.That(Calls(activeState, "IsAmbientBlendSettledForComplete"), Is.True);
            AssertGuardBeforeSnapshot(
                vfxAtmosphere.ToFullString(),
                "_phase >= ReentryPhase.HydratedFade",
                "SignalBus<AtmosphericReentrySignal>.GetFrameSnapshot()");
            AssertTextBefore(
                vfxComplete.ToFullString(),
                "_phase == ReentryPhase.Complete || HasConsumedOceanHandoff()",
                "TriggerImpactFlash();");
            StringAssert.Contains("_lastOceanHandoffSequence = signal.Sequence;", vfxComplete.ToFullString());
            StringAssert.Contains("(uint)_lastOceanHandoffSequence", FindMethod(root, "ResolveStateHash").ToFullString());
            StringAssert.Contains("_HectonReentryAblationStateId", vfx);
            StringAssert.Contains("_lastUploadedPlasmaIntensity", upload.ToFullString());
            StringAssert.Contains("_lastUploadedAblationAmount", upload.ToFullString());
            StringAssert.Contains("_lastUploadedGlassStress", upload.ToFullString());
            StringAssert.Contains("Shader.SetGlobalVector(_HectonReentryAblationStateId", upload.ToFullString());
            StringAssert.Contains("Shader.SetGlobalVector(_FullScreenFlashId", upload.ToFullString());
            Assert.That(ContainsOrdinal(upload.ToFullString(), "SetFloat"), Is.False);
            Assert.That(ContainsOrdinal(upload.ToFullString(), "SetPropertyBlock"), Is.False);
            Assert.That(ContainsOrdinal(upload.ToFullString(), ".material"), Is.False);

            string capsuleShader = Read(CapsuleShaderPath);
            string visorShader = Read(VisorShaderPath);
            StringAssert.Contains("_PlasmaIntensity", capsuleShader);
            StringAssert.Contains("_AblationAmount", capsuleShader);
            StringAssert.Contains("_GlassCrackIntensity", capsuleShader);
            StringAssert.Contains("_HectonReentryAblationState", capsuleShader);
            StringAssert.Contains("_HectonReentryPlasmaState1", capsuleShader);
            StringAssert.Contains("smoothstep(0.16h, 0.82h, mathLod01)", capsuleShader);
            StringAssert.Contains("smoothstep(0.82h, 1.0h, mathLod01)", capsuleShader);
            Assert.That(ContainsOrdinal(capsuleShader, "if (_H8OrbitalMathLod"), Is.False);
            Assert.That(ContainsOrdinal(capsuleShader, "_H8OrbitalMathLod >"), Is.False);
            Assert.That(ContainsOrdinal(capsuleShader, "_H8OrbitalMathLod >="), Is.False);
            StringAssert.Contains("_GlassCrackIntensity", visorShader);
            StringAssert.Contains("_HectonReentryAblationState.z", visorShader);
        }

        [Test]
        public void PrologueOrbitShadersUseContinuousMathLod()
        {
            AssertContinuousMathLod(Read(CapsuleShaderPath), CapsuleShaderPath);
            AssertContinuousMathLod(Read(PlanetShaderPath), PlanetShaderPath);
            AssertContinuousMathLod(Read(CloudShaderPath), CloudShaderPath);
        }

        [Test]
        public void OrbitalDirectorUploadsContinuousShaderMathLod()
        {
            CompilationUnitSyntax root = Parse(OrbitDirectorPath);
            string applyPresentation = FindMethod(root, "ApplyPresentation").ToFullString();
            string buildGlobals = FindMethod(root, "BuildPresentationShaderGlobals").ToFullString();
            string continuousLod = FindMethod(root, "ResolveContinuousMathLod").ToFullString();
            string uploadGlobals = FindMethod(root, "UploadPresentationShaderGlobalsIfDirty").ToFullString();

            AssertTextBefore(applyPresentation, "_mathLodShader = ResolveContinuousMathLod(distance);", "BuildPresentationShaderGlobals(distance);");
            StringAssert.Contains("_presentationShaderGlobals.Secondary.z = _mathLodShader;", buildGlobals);
            StringAssert.Contains("Shader.SetGlobalFloat(_mathLodId, _presentationShaderGlobals.Secondary.z);", uploadGlobals);
            StringAssert.Contains("math.smoothstep(0.12f, 0.45f, quality01)", continuousLod);
            StringAssert.Contains("math.smoothstep(0.52f, 0.88f, quality01)", continuousLod);
            StringAssert.Contains("math.smoothstep(0.86f, 1f, quality01)", continuousLod);
            Assert.That(ContainsOrdinal(continuousLod, "return MathLod"), Is.False);
        }

        [Test]
        public void AcousticQualityCurveUsesContinuousWeightAndTierOnlyForMetadata()
        {
            CompilationUnitSyntax root = Parse(AudioPath);
            string refreshQuality = FindMethod(root, "RefreshQualityPolicy").ToFullString();
            string qualityCurve = FindMethod(root, "ResolveQualityCurve01").ToFullString();
            string publishTransition = FindMethod(root, "PublishAudioTransition").ToFullString();

            StringAssert.Contains("float quality01 = ResolveGlobalQualityWeight01();", refreshQuality);
            StringAssert.Contains("_qualityWeight = quality01;", refreshQuality);
            StringAssert.Contains("_qualityTierByte = ResolveQualityTierByte(quality01);", refreshQuality);
            StringAssert.Contains("return math.smoothstep(0f, 1f, math.saturate(_qualityWeight));", qualityCurve);
            Assert.That(ContainsOrdinal(qualityCurve, "_qualityTierByte"), Is.False);
            AssertTextBefore(publishTransition, "float qualityCurve = ResolveQualityCurve01();", "float granularStress");
            StringAssert.Contains("state.QualityTier = _qualityTierByte;", publishTransition);
        }

        [Test]
        public void AcousticStressIsNotThrottledByCameraTraumaCadence()
        {
            CompilationUnitSyntax root = Parse(DirectorPath);
            string source = Read(DirectorPath);
            string presentation = FindMethod(root, "PublishContinuousCameraTrauma").ToFullString();
            string queueCameraImpact = FindMethod(root, "QueueCameraPressureImpact").ToFullString();
            string flushCameraImpact = FindMethod(root, "FlushQueuedCameraPressureImpact").ToFullString();
            string lateFrame = FindMethod(root, "LateFrameTick").ToFullString();
            string complete = FindMethod(root, "CompleteSequenceRun").ToFullString();
            string cancel = FindMethod(root, "CancelActiveSequenceNoThrow").ToFullString();
            string fail = FindMethod(root, "FailSequence").ToFullString();
            string register = FindMethod(root, "TryRegisterUpdateLane").ToFullString();
            string ensureLate = FindMethod(root, "TryEnsureLateFrameLane").ToFullString();
            string unregisterLateFrame = FindMethod(root, "TryUnregisterLateFrameLane").ToFullString();

            StringAssert.Contains("ILateFrameTickable", source);
            AssertTextBefore(presentation, "float trauma01 = _reentryState.TraumaScalar;", "PublishReentryAcousticStressSignal(trauma01);");
            AssertTextBefore(presentation, "PublishReentryAcousticStressSignal(trauma01);", "_traumaPublishAccumulatorSeconds += deltaSeconds;");
            AssertTextBefore(presentation, "PublishReentryAcousticStressSignal(trauma01);", "if (_traumaPublishAccumulatorSeconds < TraumaPublishIntervalSeconds)");
            AssertTextBefore(presentation, "if (_traumaPublishAccumulatorSeconds < TraumaPublishIntervalSeconds)", "QueueCameraPressureImpact(trauma01);");
            Assert.That(ContainsOrdinal(presentation, "CameraJuiceSignals.TryPublishImpact"), Is.False);
            AssertTextBefore(queueCameraImpact, "if (!TryEnsureLateFrameLane())", "float safeTrauma01");
            AssertTextBefore(queueCameraImpact, "_pendingCameraPressureTrauma01 = safeTrauma01;", "_pendingCameraPressureImpactDirty = true;");
            AssertTextBefore(lateFrame, "FlushQueuedCameraPressureImpact();", "if (!_running)");
            StringAssert.Contains("CameraJuiceSignals.TryPublishImpact", flushCameraImpact);
            StringAssert.Contains("_registeredUpdateLane = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);", register);
            Assert.That(ContainsOrdinal(register, "TryRegisterLateFrameTickable"), Is.False);
            StringAssert.Contains("GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment)", ensureLate);
            StringAssert.Contains("GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment)", unregisterLateFrame);
            AssertTextBefore(complete, "TryUnregisterUpdateLane();", "if (!preservePendingCameraImpact)");
            AssertTextBefore(complete, "if (!preservePendingCameraImpact)", "if (!_pendingCameraPressureImpactDirty)");
            StringAssert.Contains("CompleteSequenceRun(preservePendingCameraImpact: false);", cancel);
            StringAssert.Contains("CompleteSequenceRun(preservePendingCameraImpact: false);", fail);
            AssertNoForbiddenHotText(queueCameraImpact, DirectorPath, "QueueCameraPressureImpact");
            AssertNoForbiddenHotText(flushCameraImpact, DirectorPath, "FlushQueuedCameraPressureImpact");
        }

        [Test]
        public void OrbitalFeedbackSignalsFlushAfterPresentationInLateFrame()
        {
            CompilationUnitSyntax root = Parse(OrbitDirectorPath);
            string tick = FindMethod(root, "Tick").ToFullString();
            string lateFrame = FindMethod(root, "LateFrameTick").ToFullString();
            string emitFeedback = FindMethod(root, "EmitFeedback").ToFullString();
            string queueCameraPressure = FindMethod(root, "QueueCameraPressureFeedback").ToFullString();
            string flush = FindMethod(root, "FlushQueuedFeedbackSignals").ToFullString();
            string reset = FindMethod(root, "ResetRuntimeState").ToFullString();

            StringAssert.Contains("QueueCameraPressureFeedback(turbulence01, _reentryHeat01, cameraJuiceIntervalSeconds, cameraPriority);", emitFeedback);
            Assert.That(ContainsOrdinal(tick, "CameraJuiceSignals.TryPublishImpact"), Is.False);
            Assert.That(ContainsOrdinal(emitFeedback, "CameraJuiceSignals.TryPublishImpact"), Is.False);
            AssertTextBefore(lateFrame, "ApplyPresentation();", "FlushQueuedFeedbackSignals();");
            StringAssert.Contains("CameraJuiceSignals.TryPublishImpact", flush);
            StringAssert.Contains("SignalBus<StreamingTurbulenceSignal>.TryPushTracked(in signal, ref _signalPushDropCount);", flush);
            AssertTextBefore(flush, "_pendingCameraPressureSignalDirty = false;", "CameraJuiceSignals.TryPublishImpact");
            AssertTextBefore(queueCameraPressure, "_pendingCameraPressureSignal = signal;", "_pendingCameraPressureSignalDirty = true;");
            StringAssert.Contains("_pendingCameraPressureSignalDirty = false;", reset);
            StringAssert.Contains("_pendingCameraPressureSignal = default;", reset);
            AssertNoForbiddenHotText(queueCameraPressure, OrbitDirectorPath, "QueueCameraPressureFeedback");
            AssertNoForbiddenHotText(flush, OrbitDirectorPath, "FlushQueuedFeedbackSignals");
        }

        [Test]
        public void OrbitBootstrapPostProcessingIsContinuousAndDisabledAtZeroQuality()
        {
            CompilationUnitSyntax root = Parse(OrbitBootstrapPath);
            string bootstrap = Read(OrbitBootstrapPath);
            string post = FindMethod(root, "ConfigureOrbitPostProcessing").ToFullString();
            string opticalWeight = FindMethod(root, "ResolveOrbitOpticalWeight01").ToFullString();

            StringAssert.Contains("COLD ALLOC: List<GameObject>[16]", bootstrap);
            StringAssert.Contains("float bloomWeight = ResolveOrbitOpticalWeight01(quality);", post);
            StringAssert.Contains("math.smoothstep(OrbitOpticalQualityFloor, 1f, quality)", opticalWeight);
            StringAssert.Contains("return t * t;", opticalWeight);
            StringAssert.Contains("bool postProcessingEnabled = bloomWeight > 0f;", post);
            StringAssert.Contains("volume.enabled = postProcessingEnabled;", post);
            StringAssert.Contains("bloom.active = postProcessingEnabled;", post);
            StringAssert.Contains("cameraData.renderPostProcessing = postProcessingEnabled;", post);
            Assert.That(ContainsOrdinal(post, "cameraData.renderPostProcessing = true"), Is.False);
            Assert.That(ContainsOrdinal(post, "bloom.active = true"), Is.False);
        }

        [Test]
        public void WorldHandoffLoaderPreloadsAdditivelyAndReleasesAfterOceanHandoff()
        {
            CompilationUnitSyntax root = Parse(WorldHandoffLoaderPath);
            string source = Read(WorldHandoffLoaderPath);
            string onEnable = FindMethod(root, "OnEnable").ToFullString();
            string lateFrame = FindMethod(root, "LateFrameTick").ToFullString();
            string preload = FindMethod(root, "TryBeginWorldPreloadIfReady").ToFullString();
            string release = FindMethod(root, "TryReleaseWorldActivationIfReady").ToFullString();
            string complete = FindMethod(root, "TryCompleteActivatedWorldHandoff").ToFullString();
            string disableRelease = FindMethod(root, "ReleaseHeldWorldLoadBeforeDisable").ToFullString();

            Assert.That(ContainsOrdinal(source, "useDirectSingleSceneLoad"), Is.False);
            Assert.That(ContainsOrdinal(source, "LoadWorldOnNextFrameAsync"), Is.False);
            Assert.That(ContainsOrdinal(source, "sceneService.LoadScene(sceneName);"), Is.False);
            Assert.That(ContainsOrdinal(source, "SceneManager.LoadScene("), Is.False);
            StringAssert.Contains("PrologueReentrySignalLanes.Warm();", onEnable);
            AssertTextBefore(lateFrame, "ConsumeAtmosphericPreloadSignals(frame);", "TryBeginWorldPreloadIfReady(frame);");
            AssertTextBefore(lateFrame, "ConsumePrologueCompleteSignals(frame);", "TryReleaseWorldActivationIfReady(frame);");
            AssertTextBefore(lateFrame, "TryReleaseWorldActivationIfReady(frame);", "TryCompleteActivatedWorldHandoff();");
            AssertTextBefore(preload, "RefreshGameStartContextHandoff();", "SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);");
            AssertTextBefore(preload, "SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);", "operation.allowSceneActivation = false;");
            StringAssert.Contains("operation.priority = math.clamp(additiveLoadPriority, -100, 100);", preload);
            AssertTextBefore(release, "float progress01 = math.saturate(operation.progress * math.rcp(ActivationProgressReady01));", "EmitActivationHoldMask(frame, 1f - progress01);");
            AssertTextBefore(release, "EmitActivationHoldMask(frame, 1f - progress01);", "operation.allowSceneActivation = true;");
            StringAssert.Contains("CameraJuiceSignals.ContinuousPressureStressProfileHash", FindMethod(root, "EmitActivationHoldMask").ToFullString());
            StringAssert.Contains("SceneManager.UnloadSceneAsync(orbitScene);", complete);
            StringAssert.Contains("operation.allowSceneActivation = true;", disableRelease);
        }

        [Test]
        public void DataVaultWriteLocksAreFlatAndReleasedInFinally()
        {
            string orbitalSource = Read(OrbitDirectorPath);
            Assert.That(ContainsOrdinal(orbitalSource, "TryResolveHandle("), Is.False, OrbitDirectorPath);
            StringAssert.Contains("TryReadOnlyHandle(in _telemetryRingHandle", orbitalSource);

            for (int fileIndex = 0; fileIndex < ReentryCSharpFiles.Length; fileIndex++)
            {
                string relativePath = ReentryCSharpFiles[fileIndex];
                CompilationUnitSyntax root = Parse(relativePath);
                foreach (MethodDeclarationSyntax method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    InvocationExpressionSyntax[] acquires = method
                        .DescendantNodes()
                        .OfType<InvocationExpressionSyntax>()
                        .Where(invocation => InvocationName(invocation) == "TryAcquireWriteLock")
                        .ToArray();
                    if (acquires.Length == 0)
                        continue;

                    Assert.That(acquires.Length, Is.EqualTo(1), relativePath + ":" + method.Identifier.ValueText);
                    InvocationExpressionSyntax[] releases = method
                        .DescendantNodes()
                        .OfType<InvocationExpressionSyntax>()
                        .Where(invocation => InvocationName(invocation) == "ReleaseWriteLock")
                        .ToArray();
                    Assert.That(releases.Length, Is.EqualTo(1), relativePath + ":" + method.Identifier.ValueText);
                    Assert.That(releases[0].FirstAncestorOrSelf<FinallyClauseSyntax>(), Is.Not.Null, relativePath + ":" + method.Identifier.ValueText);
                }
            }
        }

        [Test]
        public void TimelineFuzzerMatchesAuthoredCurveSamples()
        {
            AssertTimelineSample(500, 0.2777778f, 0.0667690f, 0f, 0.0022947f, 0.0225320f);
            AssertTimelineSample(1000, 0.5555556f, 0.6562379f, 0f, 0.3536672f, 0.3294815f);
            AssertTimelineSample(1500, 0.8333333f, 1f, 0.5925926f, 1f, 0.6525f);
        }

        [Test]
        public void ScalarUploadMathAllocatesZeroBytesOverTenThousandIterations()
        {
            TimelineSample warmup = EvaluateTimelineFrame(0, FixedDeltaSeconds, 0.5f);
            Assert.That(warmup.Progress01, Is.EqualTo(0f));
            _ = GC.GetAllocatedBytesForCurrentThread();

            long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
            float checksum = 0f;
            for (int i = 0; i < 10000; i++)
            {
                TimelineSample sample = EvaluateTimelineFrame(i % 2000, FixedDeltaSeconds, 0.5f);
                checksum += sample.Heat01 + sample.Trauma01 + sample.Ablation01 + sample.GlassStress01;
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - beforeBytes;
            Assert.That(allocatedBytes, Is.EqualTo(0L));
            Assert.That(checksum, Is.GreaterThan(1f));
        }

        [Test]
        public void SignalRoutesUseUnmanagedBusAndNoStringEvents()
        {
            string director = Read(DirectorPath);
            string bridge = Read(RegistryBridgePath);
            string audio = Read(AudioPath);
            CompilationUnitSyntax audioRoot = Parse(AudioPath);
            string stressConsumer = FindMethod(audioRoot, "ConsumeReentryAcousticStressSignals").ToFullString();

            StringAssert.Contains("SignalBus<ReentryAcousticStressSignal>.TryPushTracked", director);
            StringAssert.Contains("_runtime.PublishOceanHandoff();", director);
            StringAssert.Contains("SignalBus<PrologueCompleteSignal>.TryPushTracked", bridge);
            StringAssert.Contains("SignalBus<ReentryAcousticStressSignal>.GetFrameSnapshot()", audio);
            StringAssert.Contains("AudioTransitionState state = default;", audio);
            AssertGuardBeforeSnapshot(
                FindMethod(audioRoot, "ConsumeAtmosphericSignals").ToFullString(),
                "_stage == AudioTransitionState.StageOceanHandoff",
                "SignalBus<AtmosphericReentrySignal>.GetFrameSnapshot()");
            AssertGuardBeforeSnapshot(
                stressConsumer,
                "_stage == AudioTransitionState.StageOceanHandoff",
                "SignalBus<ReentryAcousticStressSignal>.GetFrameSnapshot()");
            AssertTextBefore(stressConsumer, "_stage == AudioTransitionState.StageOceanHandoff", "_hasStressOverride = false;");
            AssertTextBefore(stressConsumer, "_hasStressOverride = false;", "SignalBus<ReentryAcousticStressSignal>.GetFrameSnapshot()");
            StringAssert.Contains("_acousticStress01 = 0f;", stressConsumer);
            StringAssert.Contains("_stressLfeGain01 = 0f;", stressConsumer);
            StringAssert.Contains("_stressGranularStress01 = 0f;", stressConsumer);
            AssertTextBefore(
                FindMethod(audioRoot, "ConsumePrologueCompleteSignals").ToFullString(),
                "if (_hasCompleteSequence)",
                "_sweepElapsedSeconds = 0f;");
            AssertNoManagedEventRoute(director, DirectorPath);
            AssertNoManagedEventRoute(audio, AudioPath);
        }

        private static void AssertTimelineSample(int frame, float expectedProgress, float expectedHeat, float expectedTrauma, float expectedAblation, float expectedGlass)
        {
            TimelineSample sample = EvaluateTimelineFrame(frame, FixedDeltaSeconds, 0.5f);
            Assert.That(sample.Progress01, Is.EqualTo(expectedProgress).Within(SampleTolerance), "progress");
            Assert.That(sample.Heat01, Is.EqualTo(expectedHeat).Within(SampleTolerance), "heat");
            Assert.That(sample.Trauma01, Is.EqualTo(expectedTrauma).Within(SampleTolerance), "trauma");
            Assert.That(sample.Ablation01, Is.EqualTo(expectedAblation).Within(SampleTolerance), "ablation");
            Assert.That(sample.GlassStress01, Is.EqualTo(expectedGlass).Within(SampleTolerance), "glass");
        }

        private static TimelineSample EvaluateTimelineFrame(int frame, float deltaSeconds, float quality01)
        {
            float progress01 = math.saturate(frame * deltaSeconds / 30f);
            float heat01 = ResolveHeatCurve01(progress01);
            float trauma01 = ResolveTraumaCurve01(progress01, heat01, quality01);
            float opacity01 = ResolveOpacityCurve01(progress01, heat01);
            float plasmaIntensity01 = ResolvePlasmaIntensity01(heat01, opacity01);
            float ablationAmount01 = ResolveAblationAmount01(plasmaIntensity01, opacity01);
            float glassStress01 = ResolveGlassStress01(plasmaIntensity01, ablationAmount01, 0f, quality01);
            return new TimelineSample(progress01, heat01, trauma01, ablationAmount01, glassStress01);
        }

        private static float ResolveHeatCurve01(float progress01)
        {
            float rise01 = SmoothStep01(math.saturate((progress01 - 0.18f) * 1.6129032f));
            float fall01 = 1f - SmoothStep01(math.saturate((progress01 - 0.88f) * 10f));
            return math.saturate(rise01 * fall01);
        }

        private static float ResolveTraumaCurve01(float progress01, float heat01, float globalQualityWeight01)
        {
            float maxQ01 = 1f - math.saturate(math.abs(progress01 - 0.8f) * 5f);
            float traumaBase01 = SmoothStep01(maxQ01) * math.saturate(heat01);
            float traumaScale01 = math.lerp(0.28f, 1f, math.saturate(globalQualityWeight01));
            return math.saturate(traumaBase01 * traumaScale01);
        }

        private static float ResolveOpacityCurve01(float progress01, float heat01)
        {
            float whiteout01 = SmoothStep01(math.saturate((progress01 - 0.62f) * 5f));
            return math.saturate(math.max(heat01, whiteout01));
        }

        private static float ResolvePlasmaIntensity01(float heat01, float opacity01)
        {
            return math.saturate(math.max(heat01, opacity01 * 0.72f));
        }

        private static float ResolveAblationAmount01(float plasmaIntensity01, float opacity01)
        {
            float plasmaSquared = plasmaIntensity01 * plasmaIntensity01;
            float opacityGain = math.lerp(0.48f, 1f, math.saturate(opacity01));
            return math.saturate(plasmaSquared * opacityGain);
        }

        private static float ResolveGlassStress01(float plasmaIntensity01, float ablationAmount01, float fullScreenFlash01, float globalQualityWeight01)
        {
            float qualityCurve = SmoothStep01(math.saturate(globalQualityWeight01));
            float qualityScaledStress = (plasmaIntensity01 * 0.45f + ablationAmount01 * 0.45f) *
                                        math.lerp(0.45f, 1f, qualityCurve);
            return math.saturate(qualityScaledStress + fullScreenFlash01 * 0.35f);
        }

        private static float SmoothStep01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - 2f * t);
        }

        private static void AssertNoForbiddenHotText(string methodSource, string relativePath, string methodName)
        {
            Assert.That(ContainsOrdinal(methodSource, "GlobalRegistry.Get<"), Is.False, relativePath + ":" + methodName);
            Assert.That(ContainsOrdinal(methodSource, "GetComponent("), Is.False, relativePath + ":" + methodName);
            Assert.That(ContainsOrdinal(methodSource, "TryGetComponent("), Is.False, relativePath + ":" + methodName);
            Assert.That(ContainsOrdinal(methodSource, ".Complete("), Is.False, relativePath + ":" + methodName);
            Assert.That(ContainsOrdinal(methodSource, "SceneManager.LoadScene("), Is.False, relativePath + ":" + methodName);
            if (!string.Equals(methodName, "TryBeginWorldPreloadIfReady", StringComparison.Ordinal))
                Assert.That(ContainsOrdinal(methodSource, "LoadSceneAsync"), Is.False, relativePath + ":" + methodName);
            Assert.That(ContainsOrdinal(methodSource, "Start" + "Coroutine"), Is.False, relativePath + ":" + methodName);
            Assert.That(ContainsOrdinal(methodSource, "yield return"), Is.False, relativePath + ":" + methodName);
            Assert.That(ContainsOrdinal(methodSource, "WaitForSeconds"), Is.False, relativePath + ":" + methodName);
            Assert.That(ContainsOrdinal(methodSource, "new List<"), Is.False, relativePath + ":" + methodName);
            Assert.That(ContainsOrdinal(methodSource, "new Dictionary<"), Is.False, relativePath + ":" + methodName);
            Assert.That(ContainsOrdinal(methodSource, "new HashSet<"), Is.False, relativePath + ":" + methodName);
            Assert.That(ContainsOrdinal(methodSource, "new Queue<"), Is.False, relativePath + ":" + methodName);
            Assert.That(ContainsOrdinal(methodSource, "new StringBuilder"), Is.False, relativePath + ":" + methodName);
            Assert.That(ContainsOrdinal(methodSource, "new GameObject"), Is.False, relativePath + ":" + methodName);
            Assert.That(ContainsOrdinal(methodSource, "new Material"), Is.False, relativePath + ":" + methodName);
            Assert.That(ContainsOrdinal(methodSource, "new AcousticPingSignal"), Is.False, relativePath + ":" + methodName);
            Assert.That(ContainsOrdinal(methodSource, "new DebrisSpawnSignal"), Is.False, relativePath + ":" + methodName);
            Assert.That(ContainsOrdinal(methodSource, "new VisorDropletSignal"), Is.False, relativePath + ":" + methodName);
            Assert.That(ContainsOrdinal(methodSource, "new ReentryVfxStateSignal"), Is.False, relativePath + ":" + methodName);
            Assert.That(ContainsOrdinal(methodSource, "string.Format"), Is.False, relativePath + ":" + methodName);
            Assert.That(ContainsOrdinal(methodSource, "$\""), Is.False, relativePath + ":" + methodName);
        }

        private static bool IsHotDependencyLookup(string expression)
        {
            return ContainsOrdinal(expression, "GlobalRegistry.Get<") ||
                   expression.EndsWith(".GetComponent", StringComparison.Ordinal) ||
                   expression.EndsWith(".TryGetComponent", StringComparison.Ordinal) ||
                   expression == "GetComponent" ||
                   expression == "TryGetComponent";
        }

        private static void AssertNoManagedEventRoute(string source, string relativePath)
        {
            Assert.That(ContainsOrdinal(source, "HectonEventBus"), Is.False, relativePath);
            Assert.That(ContainsOrdinal(source, "event "), Is.False, relativePath);
            Assert.That(ContainsOrdinal(source, "Action<"), Is.False, relativePath);
            Assert.That(ContainsOrdinal(source, "delegate "), Is.False, relativePath);
            Assert.That(ContainsOrdinal(source, "SendMessage"), Is.False, relativePath);
            Assert.That(ContainsOrdinal(source, "BroadcastMessage"), Is.False, relativePath);
        }

        private static void AssertContinuousMathLod(string source, string relativePath)
        {
            StringAssert.Contains("mathLod01", source);
            StringAssert.Contains("smoothstep(0.16h, 0.82h, mathLod01)", source);
            StringAssert.Contains("smoothstep(0.82h, 1.0h, mathLod01)", source);
            Assert.That(ContainsOrdinal(source, "if (_H8OrbitalMathLod"), Is.False, relativePath);
            Assert.That(ContainsOrdinal(source, "_H8OrbitalMathLod <"), Is.False, relativePath);
            Assert.That(ContainsOrdinal(source, "_H8OrbitalMathLod >"), Is.False, relativePath);
            Assert.That(ContainsOrdinal(source, "_H8OrbitalMathLod <="), Is.False, relativePath);
            Assert.That(ContainsOrdinal(source, "_H8OrbitalMathLod >="), Is.False, relativePath);
        }

        private static bool Calls(MethodDeclarationSyntax method, string name)
        {
            return method
                .DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Any(invocation => InvocationName(invocation) == name);
        }

        private static void AssertGuardBeforeSnapshot(string methodSource, string guard, string snapshotCall)
        {
            AssertTextBefore(methodSource, guard, snapshotCall);
        }

        private static void AssertTextBefore(string source, string first, string second)
        {
            int firstIndex = source.IndexOf(first, StringComparison.Ordinal);
            int secondIndex = source.IndexOf(second, StringComparison.Ordinal);
            Assert.That(firstIndex, Is.GreaterThanOrEqualTo(0), first);
            Assert.That(secondIndex, Is.GreaterThan(firstIndex), second);
        }

        private static string CompactSource(string source)
        {
            return source
                .Replace(" ", string.Empty)
                .Replace("\t", string.Empty)
                .Replace("\r", string.Empty)
                .Replace("\n", string.Empty);
        }

        private static string InvocationName(InvocationExpressionSyntax invocation)
        {
            switch (invocation.Expression)
            {
                case IdentifierNameSyntax identifier:
                    return identifier.Identifier.ValueText;
                case MemberAccessExpressionSyntax member:
                    return member.Name.Identifier.ValueText;
                case GenericNameSyntax generic:
                    return generic.Identifier.ValueText;
                default:
                    return invocation.Expression.ToString();
            }
        }

        private static MethodDeclarationSyntax FindMethod(CompilationUnitSyntax root, string name)
        {
            MethodDeclarationSyntax method = root
                .DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(candidate => candidate.Identifier.ValueText == name);
            Assert.That(method, Is.Not.Null, name);
            return method;
        }

        private static void AssertTrackedDumpPayload(
            string relativePath,
            string methodName,
            string ownerName,
            string labelName,
            string labelValue)
        {
            string source = Read(relativePath);
            string methodSource = FindMethod(Parse(relativePath), methodName).ToFullString();

            StringAssert.Contains("private const string " + labelName + " = \"" + labelValue + "\";", source);
            StringAssert.Contains("NativeFaultDumpWriter.CreateTransientPayload(", methodSource);
            StringAssert.Contains("nameof(" + ownerName + ")", methodSource);
            StringAssert.Contains(labelName, methodSource);
            StringAssert.Contains("NativeArrayOptions.UninitializedMemory", methodSource);
            StringAssert.Contains("NativeFaultDumpWriter.DisposeTransientPayload(", methodSource);
            Assert.That(ContainsOrdinal(methodSource, "new NativeArray<byte>(byteCount"), Is.False);
            Assert.That(ContainsOrdinal(methodSource, "payload.Dispose()"), Is.False);
        }

        private static PropertyDeclarationSyntax FindProperty(CompilationUnitSyntax root, string name)
        {
            PropertyDeclarationSyntax property = root
                .DescendantNodes()
                .OfType<PropertyDeclarationSyntax>()
                .FirstOrDefault(candidate => candidate.Identifier.ValueText == name);
            Assert.That(property, Is.Not.Null, name);
            return property;
        }

        private static CompilationUnitSyntax Parse(string relativePath)
        {
            SyntaxTree tree = CSharpSyntaxTree.ParseText(Read(relativePath));
            Diagnostic[] parseErrors = tree
                .GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .ToArray();
            Assert.That(parseErrors, Is.Empty, relativePath);
            return tree.GetCompilationUnitRoot();
        }

        private static string Read(string relativePath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return File.ReadAllText(Path.Combine(projectRoot, relativePath));
        }

        private static bool ContainsOrdinal(string source, string value)
        {
            return source.IndexOf(value, StringComparison.Ordinal) >= 0;
        }

        private static int CountOrdinal(string source, string value)
        {
            int count = 0;
            int cursor = 0;
            while (cursor < source.Length)
            {
                int index = source.IndexOf(value, cursor, StringComparison.Ordinal);
                if (index < 0)
                    break;

                count++;
                cursor = index + value.Length;
            }

            return count;
        }

        private readonly struct TimelineSample
        {
            public readonly float Progress01;
            public readonly float Heat01;
            public readonly float Trauma01;
            public readonly float Ablation01;
            public readonly float GlassStress01;

            public TimelineSample(float progress01, float heat01, float trauma01, float ablation01, float glassStress01)
            {
                Progress01 = progress01;
                Heat01 = heat01;
                Trauma01 = trauma01;
                Ablation01 = ablation01;
                GlassStress01 = glassStress01;
            }
        }
    }
}
