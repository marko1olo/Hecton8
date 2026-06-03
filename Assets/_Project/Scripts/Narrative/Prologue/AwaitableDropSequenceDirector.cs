using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Narrative.Prologue
{
    /// <summary>
    /// Contract-only prologue pacing state machine. All concrete domain work is delegated to the runtime port.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-8550)]
    public sealed class AwaitableDropSequenceDirector : MonoBehaviour, IPrologueSequenceService, IUpdatable, IGlobalRegistryHotSwapListener
    {
        private const int TelemetryCapacity = 300;
        private const double OrbitalSilenceSeconds = 3d;
        private const double Mach10MetersPerSecond = 3430d;
        private const double Mach10MetersPerSecondSq = Mach10MetersPerSecond * Mach10MetersPerSecond;
        private const double DefaultAuthoredReentryDurationSeconds = 30d;
        private const double TraumaPublishIntervalSeconds = 1d / 30d;
        private const float AcousticVacuumLowPassCutoffHertz = 150f;
        private const float AcousticPlasmaLowPassCutoffHertz = 20000f;
        private const float AcousticSplashdownLowPassCutoffHertz = 350f;
        private const float AcousticMaxVelocityMetersPerSecond = 7800f;
        private const float AtmosphereEntryHeatThreshold = 0.001f;
        private const float BurnFailClosedProgress01 = 0.42f;
        private const float ManualFailClosedProgress01 = 0.82f;
        private const uint SourceHash = PrologueSignalSourceHashes.SequenceDirector;
        private const uint AwaitHash = 0x41574149u; // AWAI
        private const uint SilenceHash = 0x53494C45u; // SILE
        private const uint BurnHash = 0x4255524Eu; // BURN
        private const uint ManualHash = 0x4D414E55u; // MANU
        private const uint ImpactHash = 0x494D5041u; // IMPA
        private const uint HydrationHash = 0x48594452u; // HYDR
        private const uint WaterHash = 0x57415452u; // WATR
        private const uint CompleteHash = 0x444F4E45u; // DONE
        private const uint DevSkipHash = 0x534B4950u; // SKIP
        private const uint CancelHash = 0x43414E43u; // CANC
        private const uint FaultHash = 0x46414C54u; // FALT
        private const uint DumpFailedHash = 0x444D5046u; // DMPF
        private const string DumpFileName = "Dump_PROLOGUE_SEQUENCE_DIRECTOR.bin";
        private const SystemID OwnerSystemId = SystemID.PrologueSequence;

        private IDataVault _dataVault;
        private VaultGenerationHandle<PrologueSequenceTelemetryEntry> _blackBoxHandle;
        private VaultGenerationHandle<ReentryStateDTO> _reentryStateHandle;
        private IPrologueSequenceRuntime _runtime;
        private PrologueStage _stage;
        private int _blackBoxCursor;
        private bool _configured;
        private bool _running;
        private bool _cancelRequested;
        private bool _disposed;
        private bool _blackBoxDumped;
        private bool _devSkipHandoffPublished;
        private bool _inputLockAcquired;
        private bool _inputLockReleased;
        private byte _cancelReason;
        private PrologueAtmosphericReentrySnapshot _lastAtmosphericReentry;
        private PrologueCompleteSnapshot _lastComplete;
        private PrologueOrbitalSnapshot _lastOrbital;
        private bool _hasLastOrbitalSnapshot;
        private PrologueStage _lastPublishedStage;
        private uint _lastPublishedStateHash;
        private byte _lastPublishedFlags;
        private bool _hasPublishedTelemetry;
        private bool _registeredHotSwap;
        private bool _registeredUpdateLane;
        private double _runElapsedSeconds;
        private double _stageElapsedSeconds;
        private double _traumaPublishAccumulatorSeconds;
        private CancellationToken _runCancellationToken;
        private ReentryStateDTO _reentryState;
        private ushort _acousticStressSequence;
        private int _signalPushDropCount;

        [Header("Authored Reentry Timeline")]
        [Tooltip("Presentation-only scalar duration used for fail-closed curves when external orbital signals stall.")]
        [SerializeField] private float authoredReentryDurationSeconds = (float)DefaultAuthoredReentryDurationSeconds;

        [Header("Impact Sync")]
        [Tooltip("Near-surface distance gate before hydration and world handoff.")]
        [SerializeField] private float impactSyncDistanceMeters = 120f;
        [Tooltip("Minimum post-release hold before the distance gate can complete.")]
        [SerializeField] private float impactSyncMinimumHoldSeconds = 0.65f;
        [Tooltip("Bounded fallback if no valid distance owner is available.")]
        [SerializeField] private float impactSyncWatchdogSeconds = 8f;

        public bool IsConfigured => _configured;
        public bool IsRunning => _running;
        public PrologueStage CurrentStage => _stage;

        public void Configure(IPrologueSequenceRuntime runtime)
        {
            if (_disposed)
                return;

            _runtime = runtime;
            _configured = runtime != null;
            PrologueReentrySignalLanes.Warm();
            TryRegisterHotSwap();
            EnsureBlackBox();
            EnsureReentryStateBuffer();
            RecordStage(PrologueStage.None, SourceHash, 0);
        }

        public async Awaitable RunPrologueSequenceAsync(CancellationToken cancellationToken)
        {
            if (!TryBeginSequenceRun(cancellationToken))
                return;

            try
            {
                while (_running && !cancellationToken.IsCancellationRequested && !_disposed)
                    await _runtime.NextFrameAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                if (_running)
                {
                    ShouldStopForCancellation(cancellationToken);
                    CompleteSequenceRun();
                    PublishFinalizedReentryStateNoThrow();
                }
            }
            catch (Exception)
            {
                FailSequence(PrologueCancelReasons.NonFinite);
            }
            finally
            {
                if (_running && (cancellationToken.IsCancellationRequested || _disposed))
                {
                    ShouldStopForCancellation(cancellationToken);
                    CompleteSequenceRun();
                    PublishFinalizedReentryStateNoThrow();
                }
            }
        }

        public void CancelSequence(byte reason)
        {
            byte normalizedReason = reason == 0 ? PrologueCancelReasons.ExplicitCancel : reason;
            if (_cancelReason == PrologueCancelReasons.DevSkip &&
                normalizedReason != PrologueCancelReasons.DevSkip)
            {
                _cancelRequested = true;
                return;
            }

            _cancelRequested = true;
            _cancelReason = normalizedReason;
        }

        private void OnEnable()
        {
            TryRegisterHotSwap();
        }

        private void OnDisable()
        {
            CancelActiveSequenceNoThrow(PrologueCancelReasons.ExplicitCancel, publishTelemetry: false);
            TryUnregisterHotSwap();
        }

        private void OnDestroy()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            CancelActiveSequenceNoThrow(PrologueCancelReasons.ExplicitCancel, publishTelemetry: false);

            _disposed = true;
            TryUnregisterHotSwap();
            ReleaseBlackBox();
            ReleaseReentryStateBuffer();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.DataVault:
                    if (ReferenceEquals(previousService, currentService))
                    {
                        _dataVault = currentService as IDataVault;
                        return;
                    }

                    ReleaseBlackBox(previousService as IDataVault ?? _dataVault);
                    ReleaseReentryStateBuffer(previousService as IDataVault ?? _dataVault);
                    _dataVault = currentService as IDataVault;
                    _blackBoxCursor = 0;
                    EnsureBlackBox();
                    EnsureReentryStateBuffer();
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    if (_registeredUpdateLane)
                    {
                        GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                        _registeredUpdateLane = false;
                    }

                    if (_running && !TryRegisterUpdateLane())
                        FailSequence(PrologueCancelReasons.NonFinite);
                    break;
            }
        }

        public void Tick(float deltaTime)
        {
            if (!_running || _runtime == null || _disposed)
                return;

            try
            {
                double deltaSeconds = ResolveTickDeltaSeconds(deltaTime);
                _runElapsedSeconds += deltaSeconds;
                _stageElapsedSeconds += deltaSeconds;
                AdvanceReentryState(deltaSeconds);
                if (!_running)
                    return;

                if (ShouldStopForCancellation(_runCancellationToken))
                {
                    if (_running)
                    {
                        CompleteSequenceRun();
                        PublishFinalizedReentryStateNoThrow();
                    }

                    return;
                }

                _runtime.RefreshFrameState();
                if (TryHandleDevelopmentSkip())
                {
                    if (_running)
                    {
                        CompleteSequenceRun();
                        PublishFinalizedReentryStateNoThrow();
                    }

                    return;
                }

                switch (_stage)
                {
                    case PrologueStage.OrbitalSilence:
                        TickOrbitalSilence();
                        break;
                    case PrologueStage.AwaitingAtmosphericReentry:
                        TickAwaitingAtmosphericReentry();
                        break;
                    case PrologueStage.ReentryBurn:
                        TickReentryBurn();
                        break;
                    case PrologueStage.ManualOverride:
                        TickManualOverride();
                        break;
                    case PrologueStage.ImpactSync:
                        TickImpactSync();
                        break;
                    case PrologueStage.AwaitOceanHydration:
                        TickAwaitOceanHydration();
                        break;
                }

                if (_running)
                    PublishContinuousCameraTrauma(deltaSeconds);
                PublishFinalizedReentryStateNoThrow();
            }
            catch (Exception)
            {
                FailSequence(PrologueCancelReasons.NonFinite);
            }
        }

        private bool TryBeginSequenceRun(CancellationToken cancellationToken)
        {
            if (!_configured || _runtime == null || _running || _disposed)
                return false;

            _running = true;
            _cancelRequested = false;
            _cancelReason = 0;
            _blackBoxDumped = false;
            _devSkipHandoffPublished = false;
            _inputLockAcquired = false;
            _inputLockReleased = false;
            _hasLastOrbitalSnapshot = false;
            _lastAtmosphericReentry = default;
            _lastComplete = default;
            _lastOrbital = default;
            _hasPublishedTelemetry = false;
            _runElapsedSeconds = 0d;
            _stageElapsedSeconds = 0d;
            _traumaPublishAccumulatorSeconds = 0d;
            _runCancellationToken = cancellationToken;
            _reentryState = default;
            _acousticStressSequence = 0;
            EnsureReentryStateBuffer();

            if (!TryRegisterUpdateLane())
            {
                FailSequence(PrologueCancelReasons.NonFinite);
                return false;
            }

            try
            {
                _runtime.PrepareSequenceRun();
                EnterOrbitalSilence();
                return true;
            }
            catch (Exception)
            {
                FailSequence(PrologueCancelReasons.NonFinite);
                return false;
            }
        }

        private void TickOrbitalSilence()
        {
            RecordStage(PrologueStage.OrbitalSilence, SilenceHash, 0);
            if (_stageElapsedSeconds < OrbitalSilenceSeconds)
                return;

            EnterAwaitingAtmosphericReentry();
        }

        private void TickAwaitingAtmosphericReentry()
        {
            uint stateHash;
            byte flags;
            if (TryResolveAtmosphericEntry(out stateHash, out flags))
            {
                RecordStage(PrologueStage.AwaitingAtmosphericReentry, stateHash, flags);
                EnterReentryBurn();
                return;
            }

            if (!_running)
                return;

            RecordStage(PrologueStage.AwaitingAtmosphericReentry, AwaitHash, 0);
        }

        private void TickReentryBurn()
        {
            bool burnComplete;
            uint stateHash;
            byte flags;
            if (!TryResolveReentryBurnComplete(out burnComplete, out stateHash, out flags))
                return;

            RecordStage(PrologueStage.ReentryBurn, stateHash, flags);
            if (burnComplete || _reentryState.Progress01 >= BurnFailClosedProgress01)
                EnterManualOverride();
        }

        private void TickManualOverride()
        {
            if (_runtime.TryConsumePrologueComplete(out _lastComplete))
            {
                if (!ValidateCompleteOrFail(in _lastComplete))
                    return;

                RecordStage(PrologueStage.ManualOverride, HashComplete(in _lastComplete), _lastComplete.Flags);
                EnterImpactSync();
                return;
            }

            RecordStage(PrologueStage.ManualOverride, ManualHash, (byte)PrologueInputLockFlags.Translation);
            if (_reentryState.Progress01 >= ManualFailClosedProgress01)
                EnterImpactSync();
        }

        private void TickImpactSync()
        {
            bool rangeReached;
            uint impactStateHash;
            byte impactFlags;
            if (!TryResolveImpactRangeReached(out rangeReached, out impactStateHash, out impactFlags))
                return;

            RecordStage(PrologueStage.ImpactSync, impactStateHash, impactFlags);

            if (_stageElapsedSeconds >= SanitizedNonNegative(impactSyncMinimumHoldSeconds, 0.65f) && rangeReached)
            {
                EnterAwaitOceanHydration();
                return;
            }

            if (_stageElapsedSeconds >= SanitizedPositive(impactSyncWatchdogSeconds, 8f))
                EnterAwaitOceanHydration();
        }

        private void TickAwaitOceanHydration()
        {
            _runtime.RefreshHydrationState(allowProxy: true);
            if (_runtime.IsOceanSurfaceReady(allowProxy: false))
            {
                RecordStage(
                    PrologueStage.AwaitOceanHydration,
                    HydrationHash,
                    (byte)PrologueHydrationMode.HighResolutionSurface);
                CompleteWaterTransition();
                return;
            }

            byte hydrationMode = ResolveHydrationMode(out bool allowProxy);
            if (allowProxy)
            {
                if (_runtime.IsOceanSurfaceReady(allowProxy: true))
                {
                    RecordStage(PrologueStage.AwaitOceanHydration, HydrationHash, hydrationMode);
                    CompleteWaterTransition();
                    return;
                }
            }

            RecordStage(PrologueStage.AwaitOceanHydration, HydrationHash, hydrationMode);
        }

        private void EnterOrbitalSilence()
        {
            EnterStage(PrologueStage.OrbitalSilence, SilenceHash, 0);
            _runtime.PublishMuffledBreathing(0.65f, (float)OrbitalSilenceSeconds);
        }

        private void EnterAwaitingAtmosphericReentry()
        {
            EnterStage(PrologueStage.AwaitingAtmosphericReentry, AwaitHash, 0);
        }

        private void EnterReentryBurn()
        {
            EnterStage(PrologueStage.ReentryBurn, BurnHash, 0);
            _runtime.PublishHullTempCriticalWarning(1f);
            _runtime.PublishHeavyRumble(1f, 0.8f);
        }

        private void EnterManualOverride()
        {
            EnterStage(PrologueStage.ManualOverride, ManualHash, (byte)PrologueInputLockFlags.Translation);
            PublishSequenceInputLock(PrologueInputLockFlags.Translation, paused: true);
            _runtime.PublishManualReleasePrompt();
        }

        private void EnterImpactSync()
        {
            EnterStage(PrologueStage.ImpactSync, ImpactHash, _lastComplete.Flags);
        }

        private void EnterAwaitOceanHydration()
        {
            EnterStage(PrologueStage.AwaitOceanHydration, HydrationHash, ResolveHydrationMode(out _));
        }

        private void EnterStage(PrologueStage stage, uint stateHash, byte flags)
        {
            _stageElapsedSeconds = 0d;
            RecordStage(stage, stateHash, flags);
        }

        private void CompleteWaterTransition()
        {
            RunWaterTransition();
            RecordStage(PrologueStage.Complete, CompleteHash, 0);
            CompleteSequenceRun();
        }

        private void RunWaterTransition()
        {
            RecordStage(PrologueStage.WaterTransition, WaterHash, 0);
            PublishSplashdownAcousticStressSignal();
            _runtime.ZeroUniverseVelocity();
            _runtime.PublishMassiveImpact();
            _runtime.PublishOceanHandoff();
        }

        private bool TryResolveAtmosphericEntry(out uint stateHash, out byte flags)
        {
            stateHash = AwaitHash;
            flags = 0;

            if (_runtime.TryConsumeAtmosphericReentry(out _lastAtmosphericReentry))
            {
                if (!ValidateAtmosphericOrFail(in _lastAtmosphericReentry))
                    return false;

                stateHash = HashAtmospheric(in _lastAtmosphericReentry);
                flags = _lastAtmosphericReentry.Flags;
                return _lastAtmosphericReentry.Heat01 > AtmosphereEntryHeatThreshold ||
                       _lastAtmosphericReentry.UniverseVelocityMetersPerSecond > 0f;
            }

            if (_runtime.TryGetOrbitalSnapshot(out _lastOrbital))
            {
                _hasLastOrbitalSnapshot = true;
                if (!ValidateOrbitalOrFail(in _lastOrbital))
                    return false;

                stateHash = HashOrbital(in _lastOrbital);
                flags = _lastOrbital.Flags;
                return _lastOrbital.ReentryHeat01 > AtmosphereEntryHeatThreshold;
            }

            return false;
        }

        private bool TryResolveReentryBurnComplete(out bool complete, out uint stateHash, out byte flags)
        {
            complete = false;
            stateHash = BurnHash;
            flags = 0;

            bool hasFreshAtmosphericReentry = _runtime.TryConsumeAtmosphericReentry(out _lastAtmosphericReentry);
            if (hasFreshAtmosphericReentry)
            {
                if (!ValidateAtmosphericOrFail(in _lastAtmosphericReentry))
                    return false;

                stateHash = HashAtmospheric(in _lastAtmosphericReentry);
                flags = _lastAtmosphericReentry.Flags;
                complete = _lastAtmosphericReentry.UniverseVelocityMetersPerSecond >= Mach10MetersPerSecond;
            }

            if (_runtime.TryGetOrbitalSnapshot(out _lastOrbital))
            {
                _hasLastOrbitalSnapshot = true;
                if (!ValidateOrbitalOrFail(in _lastOrbital))
                    return false;

                double speedSq = math.lengthsq(_lastOrbital.UniverseVelocity);
                stateHash = HashOrbital(in _lastOrbital);
                flags = _lastOrbital.Flags;
                complete = math.isfinite(speedSq) && speedSq >= Mach10MetersPerSecondSq;
            }

            return true;
        }

        private byte ResolveHydrationMode(out bool allowProxy)
        {
            float survivalProxyPressure01 = math.saturate(_runtime.SurvivalProxyPressure01);
            bool survivalProxyAllowed = survivalProxyPressure01 >=
                                        PrologueSequenceQualityPolicy.SurvivalProxyActivationThreshold01;
            bool handoffProxyAllowed = _runtime.IsStandaloneOrbitHandoffProxyAllowed;
            allowProxy = survivalProxyAllowed || handoffProxyAllowed;
            return handoffProxyAllowed
                ? (byte)PrologueHydrationMode.StandaloneOrbitHandoffProxy
                : survivalProxyAllowed
                    ? (byte)PrologueHydrationMode.SurvivalProxySurface
                    : (byte)PrologueHydrationMode.HighResolutionSurface;
        }

        private void AdvanceReentryState(double deltaSeconds)
        {
            double duration = SanitizedPositive(authoredReentryDurationSeconds, (float)DefaultAuthoredReentryDurationSeconds);
            double elapsed = math.max(0d, _runElapsedSeconds);
            float progress01 = math.saturate((float)(elapsed / duration));
            float heat01 = ResolveHeatCurve01(progress01);
            if (_hasLastOrbitalSnapshot)
                heat01 = math.max(heat01, math.saturate(_lastOrbital.ReentryHeat01));
            heat01 = math.max(heat01, math.saturate(_lastAtmosphericReentry.Heat01));

            float maxQ01 = 1f - math.saturate(math.abs(progress01 - 0.8f) * 5f);
            float traumaBase01 = SmoothStep01(maxQ01) * heat01;
            float quality01 = ResolveGlobalQualityWeight01();
            float traumaScale01 = math.lerp(0.28f, 1f, quality01);

            _reentryState.ElapsedTime = elapsed;
            _reentryState.Progress01 = progress01;
            _reentryState.HeatIntensity = heat01;
            _reentryState.TraumaScalar = math.saturate(traumaBase01 * traumaScale01);

            if (!math.isfinite(deltaSeconds) ||
                !math.isfinite(_reentryState.Progress01) ||
                !math.isfinite(_reentryState.HeatIntensity) ||
                !math.isfinite(_reentryState.TraumaScalar))
            {
                FailSequence(PrologueCancelReasons.NonFinite);
                return;
            }
        }

        private void PublishFinalizedReentryStateNoThrow()
        {
            _reentryState.CurrentPhaseEnum = (uint)_stage;
            if (!math.isfinite(_reentryState.ElapsedTime) ||
                !math.isfinite(_reentryState.Progress01) ||
                !math.isfinite(_reentryState.HeatIntensity) ||
                !math.isfinite(_reentryState.TraumaScalar))
            {
                FailSequence(PrologueCancelReasons.NonFinite);
                return;
            }

            PublishReentryStateNoThrow();
        }

        private void PublishContinuousCameraTrauma(double deltaSeconds)
        {
            byte stage = (byte)_stage;
            if (stage < (byte)PrologueStage.ReentryBurn || stage > (byte)PrologueStage.ImpactSync)
                return;

            float trauma01 = _reentryState.TraumaScalar;
            PublishReentryAcousticStressSignal(trauma01);

            _traumaPublishAccumulatorSeconds += deltaSeconds;
            if (_traumaPublishAccumulatorSeconds < TraumaPublishIntervalSeconds)
                return;

            _traumaPublishAccumulatorSeconds = 0d;
            if (trauma01 > 0.035f)
                CameraJuiceSignals.TryPublishImpact(trauma01, Vector3.zero, Vector3.down);
        }

        private void PublishReentryAcousticStressSignal(float trauma01)
        {
            float heat01 = math.saturate(_reentryState.HeatIntensity);
            float stress01 = math.saturate(math.max(trauma01, heat01));
            if (stress01 <= 0.001f && _stage != PrologueStage.ImpactSync)
                return;

            float velocityMetersPerSecond = ResolvePresentationVelocityMetersPerSecond();
            float velocity01 = math.saturate(velocityMetersPerSecond * math.rcp(AcousticMaxVelocityMetersPerSecond));
            float lowPassCutoff = math.lerp(AcousticVacuumLowPassCutoffHertz, AcousticPlasmaLowPassCutoffHertz, stress01);
            float lfeGain01 = math.saturate(math.lerp(0.22f, 0.32f, velocity01));
            float audioScale01 = math.lerp(0.35f, 1f, ResolveGlobalQualityWeight01());

            ReentryAcousticStressSignal signal = default;
            signal.Stress01 = stress01;
            signal.Heat01 = heat01;
            signal.UniverseVelocityMetersPerSecond = velocityMetersPerSecond;
            signal.LowPassCutoffHz = lowPassCutoff;
            signal.LfeGain01 = math.saturate(lfeGain01 * audioScale01);
            signal.GranularStress01 = math.saturate(stress01 * velocity01 * audioScale01);
            signal.Frame = SystemDispatcher.CurrentFrameId;
            signal.Sequence = unchecked(++_acousticStressSequence);
            signal.Flags = ReentryAcousticStressSignal.FlagAuthoritativeFilter;
            signal.Phase = _stage == PrologueStage.ImpactSync
                ? ReentryAcousticStressSignal.PhaseWhiteout
                : ReentryAcousticStressSignal.PhasePlasma;

            SignalBus<ReentryAcousticStressSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
        }

        private void PublishSplashdownAcousticStressSignal()
        {
            ReentryAcousticStressSignal signal = default;
            signal.Stress01 = 1f;
            signal.Heat01 = math.saturate(_reentryState.HeatIntensity);
            signal.UniverseVelocityMetersPerSecond = 0f;
            signal.LowPassCutoffHz = AcousticSplashdownLowPassCutoffHertz;
            signal.LfeGain01 = 0f;
            signal.GranularStress01 = 1f;
            signal.Frame = SystemDispatcher.CurrentFrameId;
            signal.Sequence = unchecked(++_acousticStressSequence);
            signal.Flags = ReentryAcousticStressSignal.FlagAuthoritativeFilter |
                           ReentryAcousticStressSignal.FlagSplashdown;
            signal.Phase = ReentryAcousticStressSignal.PhaseSplashdown;
            SignalBus<ReentryAcousticStressSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
        }

        private void CompleteSequenceRun()
        {
            _running = false;
            _runCancellationToken = default;
            TryUnregisterUpdateLane();
            ReleaseInputLockNoThrow();
        }

        private void CancelActiveSequenceNoThrow(byte reason, bool publishTelemetry = true)
        {
            if (!_running)
                return;

            byte normalizedReason = reason == 0 ? PrologueCancelReasons.ExplicitCancel : reason;
            _cancelRequested = true;
            _cancelReason = normalizedReason;
            if (publishTelemetry)
                RecordStage(PrologueStage.Cancelled, CancelHash, normalizedReason);
            CompleteSequenceRun();
            PublishFinalizedReentryStateNoThrow();
        }

        private void FailSequence(byte reason)
        {
            RecordStage(PrologueStage.Faulted, FaultHash, reason);
            DumpBlackBox();
            TryDumpRuntimeBlackBox(_runtime);
            CompleteSequenceRun();
            SanitizeReentryStateForTerminalPublish();
            PublishFinalizedReentryStateNoThrow();
        }

        private void SanitizeReentryStateForTerminalPublish()
        {
            double elapsed = math.isfinite(_reentryState.ElapsedTime)
                ? _reentryState.ElapsedTime
                : math.isfinite(_runElapsedSeconds) ? _runElapsedSeconds : 0d;
            _reentryState.ElapsedTime = math.max(0d, elapsed);
            _reentryState.Progress01 = math.isfinite(_reentryState.Progress01) ? math.saturate(_reentryState.Progress01) : 0f;
            _reentryState.HeatIntensity = math.isfinite(_reentryState.HeatIntensity) ? math.saturate(_reentryState.HeatIntensity) : 0f;
            _reentryState.TraumaScalar = math.isfinite(_reentryState.TraumaScalar) ? math.saturate(_reentryState.TraumaScalar) : 0f;
        }

        private bool ValidateOrbitalOrFail(in PrologueOrbitalSnapshot snapshot)
        {
            if (IsFiniteOrbital(in snapshot))
                return true;

            FailSequence(PrologueCancelReasons.NonFinite);
            return false;
        }

        private bool ValidateAtmosphericOrFail(in PrologueAtmosphericReentrySnapshot snapshot)
        {
            if (IsFiniteAtmospheric(in snapshot))
                return true;

            FailSequence(PrologueCancelReasons.NonFinite);
            return false;
        }

        private bool ValidateCompleteOrFail(in PrologueCompleteSnapshot snapshot)
        {
            if (math.isfinite(snapshot.WhiteoutHoldSeconds))
                return true;

            FailSequence(PrologueCancelReasons.NonFinite);
            return false;
        }

        private bool TryRegisterUpdateLane()
        {
            if (_registeredUpdateLane)
                return true;

            if (!Application.isPlaying || GlobalRegistry.TickDispatcher == null)
                return false;

            _registeredUpdateLane = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            return _registeredUpdateLane;
        }

        private void TryUnregisterUpdateLane()
        {
            if (!_registeredUpdateLane)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registeredUpdateLane = false;
        }

        private bool TryResolveImpactRangeReached(out bool rangeReached, out uint stateHash, out byte flags)
        {
            rangeReached = false;
            stateHash = ImpactHash;
            flags = _lastComplete.Flags;
            double impactDistance = SanitizedPositive(impactSyncDistanceMeters, 120f);
            bool hasDistance = false;
            double nearestDistance = double.MaxValue;

            if (_runtime.TryGetOrbitalSnapshot(out _lastOrbital))
            {
                _hasLastOrbitalSnapshot = true;
                if (!IsFiniteOrbital(in _lastOrbital))
                {
                    FailSequence(PrologueCancelReasons.NonFinite);
                    return false;
                }

                nearestDistance = math.min(nearestDistance, math.max(0d, _lastOrbital.PlanetDistanceMeters));
                hasDistance = true;
                stateHash = HashOrbital(in _lastOrbital);
                flags = _lastOrbital.Flags;
            }

            if (_runtime.TryConsumeAtmosphericReentry(out _lastAtmosphericReentry))
            {
                if (!IsFiniteAtmospheric(in _lastAtmosphericReentry))
                {
                    FailSequence(PrologueCancelReasons.NonFinite);
                    return false;
                }

                nearestDistance = math.min(nearestDistance, math.max(0d, (double)_lastAtmosphericReentry.AltitudeMeters));
                hasDistance = true;
                stateHash = HashAtmospheric(in _lastAtmosphericReentry);
                flags = _lastAtmosphericReentry.Flags;
            }

            rangeReached = hasDistance && nearestDistance <= impactDistance;
            return true;
        }

        private void OnValidate()
        {
            authoredReentryDurationSeconds = SanitizedPositive(
                authoredReentryDurationSeconds,
                (float)DefaultAuthoredReentryDurationSeconds);
            impactSyncDistanceMeters = SanitizedPositive(impactSyncDistanceMeters, 120f);
            impactSyncMinimumHoldSeconds = SanitizedNonNegative(impactSyncMinimumHoldSeconds, 0.65f);
            impactSyncWatchdogSeconds = math.max(
                impactSyncMinimumHoldSeconds,
                SanitizedPositive(impactSyncWatchdogSeconds, 8f));
        }

        private static double ResolveTickDeltaSeconds(float deltaTime)
        {
            if (math.isfinite(deltaTime) && deltaTime > 0f)
                return deltaTime;

            return ResolveFrameDeltaSeconds();
        }

        private static double ResolveFrameDeltaSeconds()
        {
            float delta = SystemDispatcher.CurrentFrameDeltaTime;
            return math.isfinite(delta) && delta > 0f ? delta : 1d / 60d;
        }

        private static float ResolveHeatCurve01(float progress01)
        {
            float rise01 = SmoothStep01(math.saturate((progress01 - 0.18f) * 1.6129032f));
            float fall01 = 1f - SmoothStep01(math.saturate((progress01 - 0.88f) * 10f));
            return math.saturate(rise01 * fall01);
        }

        private static float SmoothStep01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - 2f * t);
        }

        private static float ResolveGlobalQualityWeight01()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1f, quality, math.isfinite(quality)));
        }

        private static float SanitizedPositive(float value, float fallback)
        {
            return math.isfinite(value) && value > 0f ? value : fallback;
        }

        private static float SanitizedNonNegative(float value, float fallback)
        {
            return math.isfinite(value) && value >= 0f ? value : fallback;
        }

        private bool ShouldStopForCancellation(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                if (_cancelReason == PrologueCancelReasons.DevSkip)
                    TryExecuteDevelopmentSkipHandoff();
                else
                    RecordStage(
                        PrologueStage.Cancelled,
                        CancelHash,
                        _cancelReason == 0 ? PrologueCancelReasons.TokenCancelled : _cancelReason);
                return true;
            }

            if (_cancelRequested)
            {
                if (_cancelReason == PrologueCancelReasons.DevSkip)
                    TryExecuteDevelopmentSkipHandoff();
                else
                    RecordStage(PrologueStage.Cancelled, CancelHash, _cancelReason);
                return true;
            }

            return false;
        }

        private bool TryHandleDevelopmentSkip()
        {
            if (!_runtime.IsDevelopmentBuild || !_runtime.ShouldSkipPrologue)
                return false;

            CancelSequence(PrologueCancelReasons.DevSkip);
            TryExecuteDevelopmentSkipHandoff();
            return true;
        }

        private bool TryExecuteDevelopmentSkipHandoff()
        {
            if (_devSkipHandoffPublished)
                return true;

            _devSkipHandoffPublished = true;
            try
            {
                ExecuteDevelopmentSkipHandoff();
                return true;
            }
            catch (Exception)
            {
                FailSequence(PrologueCancelReasons.DevSkip);
                return false;
            }
        }

        private void ExecuteDevelopmentSkipHandoff()
        {
            RecordStage(PrologueStage.DevSkip, DevSkipHash, (byte)PrologueHydrationMode.DevForcedShallowWater);
            _runtime.ForceShallowWaterHydration();
            _runtime.ZeroUniverseVelocity();
            _runtime.PublishMassiveImpact();
            _runtime.PublishOceanHandoff();
            ReleaseInputLockNoThrow();
        }

        private void EnsureBlackBox()
        {
            IDataVault vault = CacheDataVaultCold();
            if (vault == null)
                return;

            if (IsVaultHandleCreated(in _blackBoxHandle) &&
                vault.TryReadOnlyHandle(in _blackBoxHandle, out NativeArray<PrologueSequenceTelemetryEntry>.ReadOnly buffer) &&
                buffer.IsCreated &&
                buffer.Length >= TelemetryCapacity)
            {
                return;
            }

            if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return;

            if (IsVaultHandleCreated(in _blackBoxHandle))
                vault.ReleaseBuffer(in _blackBoxHandle);
            ClearBlackBoxDescriptor();

            if (vault.TryGetGenerationHandle<PrologueSequenceTelemetryEntry>(
                    BufferID.PrologueSequenceTelemetryRing,
                    out VaultGenerationHandle<PrologueSequenceTelemetryEntry> existing) &&
                vault.TryReadOnlyHandle(in existing, out NativeArray<PrologueSequenceTelemetryEntry>.ReadOnly existingBuffer) &&
                existingBuffer.IsCreated &&
                existingBuffer.Length >= TelemetryCapacity)
            {
                _blackBoxHandle = existing;
                return;
            }

            VaultGenerationHandle<PrologueSequenceTelemetryEntry> acquired = vault.EnsureGenerationHandle<PrologueSequenceTelemetryEntry>(
                BufferID.PrologueSequenceTelemetryRing,
                TelemetryCapacity,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            if (!IsVaultHandleCreated(in acquired) ||
                !vault.TryReadOnlyHandle(in acquired, out NativeArray<PrologueSequenceTelemetryEntry>.ReadOnly acquiredBuffer) ||
                !acquiredBuffer.IsCreated ||
                acquiredBuffer.Length < TelemetryCapacity)
            {
                ClearBlackBoxDescriptor();
                _blackBoxCursor = 0;
                return;
            }

            _blackBoxHandle = acquired;
        }

        private void EnsureReentryStateBuffer()
        {
            IDataVault vault = CacheDataVaultCold();
            if (vault == null)
                return;

            if (IsVaultHandleCreated(in _reentryStateHandle) &&
                vault.TryReadOnlyHandle(in _reentryStateHandle, out NativeArray<ReentryStateDTO>.ReadOnly buffer) &&
                buffer.IsCreated &&
                buffer.Length >= 1)
            {
                return;
            }

            if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return;

            if (IsVaultHandleCreated(in _reentryStateHandle))
                vault.ReleaseBuffer(in _reentryStateHandle);
            ClearReentryStateDescriptor();

            if (vault.TryGetGenerationHandle<ReentryStateDTO>(
                    BufferID.PrologueReentryState,
                    out VaultGenerationHandle<ReentryStateDTO> existing) &&
                vault.TryReadOnlyHandle(in existing, out NativeArray<ReentryStateDTO>.ReadOnly existingBuffer) &&
                existingBuffer.IsCreated &&
                existingBuffer.Length >= 1)
            {
                _reentryStateHandle = existing;
                return;
            }

            VaultGenerationHandle<ReentryStateDTO> acquired = vault.EnsureGenerationHandle<ReentryStateDTO>(
                BufferID.PrologueReentryState,
                1,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            if (!IsVaultHandleCreated(in acquired) ||
                !vault.TryReadOnlyHandle(in acquired, out NativeArray<ReentryStateDTO>.ReadOnly acquiredBuffer) ||
                !acquiredBuffer.IsCreated ||
                acquiredBuffer.Length < 1)
            {
                ClearReentryStateDescriptor();
                return;
            }

            _reentryStateHandle = acquired;
        }

        private void PublishReentryStateNoThrow()
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsVaultHandleCreated(in _reentryStateHandle) ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireWriteLock(in _reentryStateHandle, OwnerSystemId, out NativeArray<ReentryStateDTO> stateBuffer))
            {
                return;
            }

            try
            {
                if (stateBuffer.IsCreated && stateBuffer.Length > 0)
                    stateBuffer[0] = _reentryState;
            }
            finally
            {
                vault.ReleaseWriteLock(in _reentryStateHandle, OwnerSystemId);
            }
        }

        private void RecordStage(PrologueStage stage, uint stateHash, byte flags)
        {
            _stage = stage;

            IDataVault vault = _dataVault;
            IPrologueSequenceRuntime runtime = _runtime;
            if (vault != null &&
                IsVaultHandleCreated(in _blackBoxHandle))
            {
                uint telemetryFrame = runtime != null ? runtime.CurrentFrame : 0u;
                ushort telemetrySequence = _lastComplete.Sequence != 0 ? _lastComplete.Sequence : _lastAtmosphericReentry.Sequence;
                bool hasOrbitalSnapshot = _hasLastOrbitalSnapshot;
                double universeSpeedMetersPerSecond = 0d;
                double planetDistanceMeters = 0d;
                if (hasOrbitalSnapshot)
                {
                    universeSpeedMetersPerSecond = ResolveTelemetrySpeedMetersPerSecond();
                    planetDistanceMeters = _lastOrbital.PlanetDistanceMeters;
                }

                int blackBoxIndex = math.clamp(_blackBoxCursor, 0, TelemetryCapacity - 1);
                int nextBlackBoxCursor = (blackBoxIndex + 1) % TelemetryCapacity;

                if (vault.TryAcquireWriteLock(in _blackBoxHandle, OwnerSystemId, out NativeArray<PrologueSequenceTelemetryEntry> blackBox))
                {
                    try
                    {
                        if (!blackBox.IsCreated || blackBox.Length < TelemetryCapacity)
                            return;

                        PrologueSequenceTelemetryEntry entry = default;
                        entry.Frame = telemetryFrame;
                        entry.StateHash = stateHash;
                        entry.Stage = (byte)stage;
                        entry.Flags = flags;
                        entry.Sequence = telemetrySequence;
                        if (hasOrbitalSnapshot)
                        {
                            entry.UniverseSpeedMetersPerSecond = universeSpeedMetersPerSecond;
                            entry.PlanetDistanceMeters = planetDistanceMeters;
                        }

                        blackBox[blackBoxIndex] = entry;
                        _blackBoxCursor = nextBlackBoxCursor;
                    }
                    finally
                    {
                        vault.ReleaseWriteLock(in _blackBoxHandle, OwnerSystemId);
                    }
                }
            }

            if (runtime != null &&
                (!_hasPublishedTelemetry ||
                 _lastPublishedStage != stage ||
                 _lastPublishedStateHash != stateHash ||
                 _lastPublishedFlags != flags))
            {
                TryPushTelemetryNoThrow(stage, stateHash, flags);
                _lastPublishedStage = stage;
                _lastPublishedStateHash = stateHash;
                _lastPublishedFlags = flags;
                _hasPublishedTelemetry = true;
            }
        }

        private void ReleaseInputLockNoThrow()
        {
            if (_inputLockReleased || !_inputLockAcquired)
                return;

            IPrologueSequenceRuntime runtime = _runtime;
            if (runtime == null)
                return;

            try
            {
                runtime.PublishInputLock(PrologueInputLockFlags.None, paused: false);
                _inputLockReleased = true;
                _inputLockAcquired = false;
            }
            catch (Exception)
            {
                DumpBlackBox();
                TryDumpRuntimeBlackBox(runtime);
            }
        }

        private void PublishSequenceInputLock(PrologueInputLockFlags flags, bool paused)
        {
            _runtime.PublishInputLock(flags, paused);
            if (flags == PrologueInputLockFlags.None || !paused)
                return;

            _inputLockAcquired = true;
            _inputLockReleased = false;
        }

        private void TryDumpRuntimeBlackBox(IPrologueSequenceRuntime runtime)
        {
            if (runtime == null)
                return;

            try
            {
                runtime.DumpBlackBox();
            }
            catch (Exception)
            {
            }
        }

        private void TryPushTelemetryNoThrow(PrologueStage stage, uint stateHash, byte flags)
        {
            IPrologueSequenceRuntime runtime = _runtime;
            if (runtime == null)
                return;

            try
            {
                runtime.PushTelemetry(stage, stateHash, flags);
            }
            catch (Exception)
            {
            }
        }

        private double ResolveTelemetrySpeedMetersPerSecond()
        {
            double speedSq = math.lengthsq(_lastOrbital.UniverseVelocity);
            if (!math.isfinite(speedSq) || speedSq <= 0d)
                return 0d;

            float speedSqF = (float)math.min(speedSq, (double)float.MaxValue);
            return speedSqF * math.rsqrt(math.max(speedSqF, 0.000001f));
        }

        private float ResolvePresentationVelocityMetersPerSecond()
        {
            if (math.isfinite(_lastAtmosphericReentry.UniverseVelocityMetersPerSecond) &&
                _lastAtmosphericReentry.UniverseVelocityMetersPerSecond > 0f)
            {
                return _lastAtmosphericReentry.UniverseVelocityMetersPerSecond;
            }

            return (float)math.min(ResolveTelemetrySpeedMetersPerSecond(), (double)float.MaxValue);
        }

        private static bool IsFiniteOrbital(in PrologueOrbitalSnapshot snapshot)
        {
            return math.all(math.isfinite(snapshot.UniverseVelocity)) &&
                   math.isfinite(snapshot.PlanetDistanceMeters) &&
                   math.isfinite(snapshot.ReentryHeat01) &&
                   math.isfinite(snapshot.CloudWhiteout01);
        }

        private static bool IsFiniteAtmospheric(in PrologueAtmosphericReentrySnapshot snapshot)
        {
            return math.isfinite(snapshot.AltitudeMeters) &&
                   math.isfinite(snapshot.UniverseVelocityMetersPerSecond) &&
                   math.isfinite(snapshot.Heat01);
        }

        private void DumpBlackBox()
        {
            IDataVault vault = _dataVault;
            if (_blackBoxDumped ||
                vault == null ||
                !IsVaultHandleCreated(in _blackBoxHandle) ||
                !vault.TryReadOnlyHandle(in _blackBoxHandle, out NativeArray<PrologueSequenceTelemetryEntry>.ReadOnly blackBox) ||
                !blackBox.IsCreated ||
                blackBox.Length <= 0)
            {
                return;
            }

            NativeArray<byte> payload = default;
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                string path = Path.Combine(projectRoot, "Docs", "AgentLogs", DumpFileName);
                const int headerBytes = 12;
                const int rowBytes = 28;
                int length = math.min(TelemetryCapacity, blackBox.Length);
                int byteCount = headerBytes + length * rowBytes;
                payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

                unsafe
                {
                    byte* bytes = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payload);
                    WriteUInt(bytes, 0, SourceHash);
                    WriteInt(bytes, 4, TelemetryCapacity);
                    WriteInt(bytes, 8, _blackBoxCursor);
                    int writeCursor = headerBytes;
                    for (int i = 0; i < length; i++)
                    {
                        int index = (_blackBoxCursor + i) % length;
                        PrologueSequenceTelemetryEntry entry = blackBox[index];
                        WriteUInt(bytes, writeCursor, entry.Frame);
                        WriteUInt(bytes, writeCursor + 4, entry.StateHash);
                        WriteDouble(bytes, writeCursor + 8, entry.UniverseSpeedMetersPerSecond);
                        WriteDouble(bytes, writeCursor + 16, entry.PlanetDistanceMeters);
                        WriteUShort(bytes, writeCursor + 24, entry.Sequence);
                        bytes[writeCursor + 26] = entry.Stage;
                        bytes[writeCursor + 27] = entry.Flags;
                        writeCursor += rowBytes;
                    }
                }

                _blackBoxDumped = NativeFaultDumpWriter.TryWriteAll(path, payload, byteCount);
            }
            catch (Exception)
            {
                TryPushTelemetryNoThrow(PrologueStage.Faulted, DumpFailedHash, 1);
            }
            finally
            {
                if (payload.IsCreated)
                    payload.Dispose();
            }
        }

        private static unsafe void WriteUInt(byte* data, int offset, uint value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
            data[offset + 2] = (byte)(value >> 16);
            data[offset + 3] = (byte)(value >> 24);
        }

        private static unsafe void WriteInt(byte* data, int offset, int value)
        {
            WriteUInt(data, offset, unchecked((uint)value));
        }

        private static unsafe void WriteUShort(byte* data, int offset, ushort value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
        }

        private static unsafe void WriteDouble(byte* data, int offset, double value)
        {
            ulong bits = unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
            data[offset] = (byte)bits;
            data[offset + 1] = (byte)(bits >> 8);
            data[offset + 2] = (byte)(bits >> 16);
            data[offset + 3] = (byte)(bits >> 24);
            data[offset + 4] = (byte)(bits >> 32);
            data[offset + 5] = (byte)(bits >> 40);
            data[offset + 6] = (byte)(bits >> 48);
            data[offset + 7] = (byte)(bits >> 56);
        }

        private IDataVault CacheDataVaultCold()
        {
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;

            return _dataVault;
        }

        private void ReleaseBlackBox()
        {
            ReleaseBlackBox(_dataVault);
        }

        private void ReleaseBlackBox(IDataVault vault)
        {
            if (vault != null && IsVaultHandleCreated(in _blackBoxHandle))
                vault.ReleaseBuffer(in _blackBoxHandle);

            ClearBlackBoxDescriptor();
            _blackBoxCursor = 0;
        }

        private void ClearBlackBoxDescriptor()
        {
            _blackBoxHandle = default;
        }

        private void ReleaseReentryStateBuffer()
        {
            ReleaseReentryStateBuffer(_dataVault);
        }

        private void ReleaseReentryStateBuffer(IDataVault vault)
        {
            if (vault != null && IsVaultHandleCreated(in _reentryStateHandle))
                vault.ReleaseBuffer(in _reentryStateHandle);

            ClearReentryStateDescriptor();
        }

        private void ClearReentryStateDescriptor()
        {
            _reentryStateHandle = default;
        }

        private void TryRegisterHotSwap()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwap()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private static uint HashAtmospheric(in PrologueAtmosphericReentrySnapshot snapshot)
        {
            uint hash = 2166136261u;
            hash = Mix(hash, math.asuint(snapshot.AltitudeMeters));
            hash = Mix(hash, math.asuint(snapshot.UniverseVelocityMetersPerSecond));
            hash = Mix(hash, math.asuint(snapshot.Heat01));
            hash = Mix(hash, snapshot.Sequence);
            hash = Mix(hash, snapshot.Phase);
            hash = Mix(hash, snapshot.Flags);
            return hash;
        }

        private static uint HashComplete(in PrologueCompleteSnapshot snapshot)
        {
            uint hash = 2166136261u;
            hash = Mix(hash, snapshot.Frame);
            hash = Mix(hash, math.asuint(snapshot.WhiteoutHoldSeconds));
            hash = Mix(hash, snapshot.Sequence);
            hash = Mix(hash, snapshot.Phase);
            hash = Mix(hash, snapshot.Flags);
            return hash;
        }

        private static uint HashOrbital(in PrologueOrbitalSnapshot snapshot)
        {
            uint hash = 2166136261u;
            hash = Mix(hash, HashDouble(snapshot.UniverseVelocity.x));
            hash = Mix(hash, HashDouble(snapshot.UniverseVelocity.y));
            hash = Mix(hash, HashDouble(snapshot.UniverseVelocity.z));
            hash = Mix(hash, HashDouble(snapshot.PlanetDistanceMeters));
            hash = Mix(hash, math.asuint(snapshot.ReentryHeat01));
            hash = Mix(hash, math.asuint(snapshot.CloudWhiteout01));
            hash = Mix(hash, snapshot.Sequence);
            hash = Mix(hash, snapshot.MathLod);
            hash = Mix(hash, snapshot.Flags);
            return hash;
        }

        private static uint HashDouble(double value)
        {
            long bits = BitConverter.DoubleToInt64Bits(value);
            return unchecked((uint)bits ^ (uint)(bits >> 32));
        }

        private static uint Mix(uint hash, uint value)
        {
            return unchecked((hash ^ value) * 16777619u);
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 64)]
        private struct PrologueSequenceTelemetryEntry
        {
            [System.Runtime.InteropServices.FieldOffset(0)]
            public double UniverseSpeedMetersPerSecond;
            [System.Runtime.InteropServices.FieldOffset(8)]
            public double PlanetDistanceMeters;
            [System.Runtime.InteropServices.FieldOffset(16)]
            public uint Frame;
            [System.Runtime.InteropServices.FieldOffset(20)]
            public uint StateHash;
            [System.Runtime.InteropServices.FieldOffset(24)]
            public ushort Sequence;
            [System.Runtime.InteropServices.FieldOffset(26)]
            public byte Stage;
            [System.Runtime.InteropServices.FieldOffset(27)]
            public byte Flags;
            [System.Runtime.InteropServices.FieldOffset(28)]
            private byte _pad0;
            [System.Runtime.InteropServices.FieldOffset(29)]
            private byte _pad1;
            [System.Runtime.InteropServices.FieldOffset(30)]
            private byte _pad2;
            [System.Runtime.InteropServices.FieldOffset(31)]
            private byte _pad3;
            [System.Runtime.InteropServices.FieldOffset(32)]
            private byte _pad4;
            [System.Runtime.InteropServices.FieldOffset(33)]
            private byte _pad5;
            [System.Runtime.InteropServices.FieldOffset(34)]
            private byte _pad6;
            [System.Runtime.InteropServices.FieldOffset(35)]
            private byte _pad7;
            [System.Runtime.InteropServices.FieldOffset(36)]
            private byte _pad8;
            [System.Runtime.InteropServices.FieldOffset(37)]
            private byte _pad9;
            [System.Runtime.InteropServices.FieldOffset(38)]
            private byte _pad10;
            [System.Runtime.InteropServices.FieldOffset(39)]
            private byte _pad11;
            [System.Runtime.InteropServices.FieldOffset(40)]
            private byte _pad12;
            [System.Runtime.InteropServices.FieldOffset(41)]
            private byte _pad13;
            [System.Runtime.InteropServices.FieldOffset(42)]
            private byte _pad14;
            [System.Runtime.InteropServices.FieldOffset(43)]
            private byte _pad15;
            [System.Runtime.InteropServices.FieldOffset(44)]
            private byte _pad16;
            [System.Runtime.InteropServices.FieldOffset(45)]
            private byte _pad17;
            [System.Runtime.InteropServices.FieldOffset(46)]
            private byte _pad18;
            [System.Runtime.InteropServices.FieldOffset(47)]
            private byte _pad19;
            [System.Runtime.InteropServices.FieldOffset(48)]
            private byte _pad20;
            [System.Runtime.InteropServices.FieldOffset(49)]
            private byte _pad21;
            [System.Runtime.InteropServices.FieldOffset(50)]
            private byte _pad22;
            [System.Runtime.InteropServices.FieldOffset(51)]
            private byte _pad23;
            [System.Runtime.InteropServices.FieldOffset(52)]
            private byte _pad24;
            [System.Runtime.InteropServices.FieldOffset(53)]
            private byte _pad25;
            [System.Runtime.InteropServices.FieldOffset(54)]
            private byte _pad26;
            [System.Runtime.InteropServices.FieldOffset(55)]
            private byte _pad27;
            [System.Runtime.InteropServices.FieldOffset(56)]
            private byte _pad28;
            [System.Runtime.InteropServices.FieldOffset(57)]
            private byte _pad29;
            [System.Runtime.InteropServices.FieldOffset(58)]
            private byte _pad30;
            [System.Runtime.InteropServices.FieldOffset(59)]
            private byte _pad31;
            [System.Runtime.InteropServices.FieldOffset(60)]
            private byte _pad32;
            [System.Runtime.InteropServices.FieldOffset(61)]
            private byte _pad33;
            [System.Runtime.InteropServices.FieldOffset(62)]
            private byte _pad34;
            [System.Runtime.InteropServices.FieldOffset(63)]
            private byte _pad35;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct ReentryStateDTO
    {
        [FieldOffset(0)]
        public double ElapsedTime;
        [FieldOffset(8)]
        public float Progress01;
        [FieldOffset(12)]
        public float HeatIntensity;
        [FieldOffset(16)]
        public float TraumaScalar;
        [FieldOffset(20)]
        public uint CurrentPhaseEnum;
        [FieldOffset(24)]
        private uint _pad0;
        [FieldOffset(28)]
        private uint _pad1;
    }
}
