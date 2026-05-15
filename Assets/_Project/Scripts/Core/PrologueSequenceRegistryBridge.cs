using System;
using System.Threading;
using Hecton8.Core.Contracts;
using Hecton8.Core.Signals;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Core-domain adapter between contract-only prologue pacing and concrete registry/signal services.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-8540)]
    public sealed class PrologueSequenceRegistryBridge : MonoBehaviour, IPrologueSequenceRuntime, IGlobalRegistryHotSwapListener
    {
        private const uint SourceHash = PrologueSignalSourceHashes.SequenceDirector;
        private const uint ManualOverrideSourceHash = PrologueSignalSourceHashes.ManualOverrideLever;
        private const uint PlayerInputSignalSourceHash = 0x504C494Eu;
        private const uint MissingServiceHash = 0x50524D49u; // PRMI
        private const uint RegistrationRejectedHash = 0x5052524Au; // PRRJ
        private const uint CancellationFaultHash = 0x50524346u; // PRCF
        private const uint MuffledBreathingHash = 0x4D425254u; // MBRT
        private const uint HullTempCriticalHash = 0x4854454Du; // HTEM
        private const uint ManualReleaseHash = 0x4D52454Cu; // MREL
        private const uint ManualReleaseContextHash = 0x434F434Bu; // COCK
        private const uint ShallowWaterChunkHash = 0x53484C57u; // SHLW
        private const int LowTierHysteresisFrames = 150;
        private const int LowTierProbeIntervalFrames = 30;
        private const byte CriticalMemoryPressureSeverity = 2;
        private const float MassiveImpactSeverity = 1f;
        private const float ReentryHeatStartThreshold01 = 0.001f;

        [SerializeField] private MonoBehaviour sequenceComponent;
        [SerializeField] private bool autoRunOnEnable = true;
        [SerializeField] private long oceanSurfaceChunkId;
        [SerializeField] private bool allowAnyHydratedChunkFallback = true;

        private IPrologueSequenceService _service;
        private IInputService _inputService;
        private IOrbitalDirector _orbitalDirector;
        private IStreamingBackpressureService _streamingBackpressure;
        private ITickDispatcher _tickDispatcher;
        private CancellationTokenSource _runCancellationSource;
        private bool _registeredService;
        private bool _registeredHotSwap;
        private bool _isDevelopmentBuild;
        private bool _cachedLowTier;
        private bool _pendingLowTier;
        private bool _hasLowTierCache;
        private bool _lastObservedLowTierPolicy;
        private bool _lastObservedForcedLowMemory;
        private bool _skipRequested;
        private bool _observedHighResSurfaceReady;
        private bool _observedProxySurfaceReady;
        private int _lowTierCandidateFrame;
        private int _lowTierPolicyProbeFrame = -1;
        private int _memoryPressureSnapshotFrame = -1;
        private int _memoryPressureSnapshotCursor;
        private int _atmosphereSnapshotFrame = -1;
        private int _completeSnapshotFrame = -1;
        private int _residencySnapshotFrame = -1;
        private int _atmosphereSnapshotCursor;
        private int _completeSnapshotCursor;
        private int _residencySnapshotCursor;
        private uint _lastPlayerInputSignalSequence;
        private ushort _sequence;

        public bool IsDevelopmentBuild => _isDevelopmentBuild;

        public bool IsLowTier
        {
            get
            {
                return ResolveLowTierWithHysteresis();
            }
        }

        public uint CurrentFrame => unchecked((uint)math.max(0, Time.frameCount));

        public bool ShouldSkipPrologue
        {
            get
            {
                if (!IsDevelopmentBuild)
                    return false;

                if (!_skipRequested)
                    ConsumeSkipInputSignals();

                if (_skipRequested)
                    return true;

                IInputService input = _inputService;
                if (input == null)
                    return false;

                if (!input.IsInitialized)
                    return false;

                PlayerInputState state = input.GetState();
                return state.HasAction(PlayerInputAction.Dash) &&
                       state.HasAction(PlayerInputAction.PrimaryFire) &&
                       state.HasAction(PlayerInputAction.SecondaryFire);
            }
        }

        private void OnEnable()
        {
            ResetTransientSequenceState();
            ResolveService();

            if (_service == null)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(MissingServiceHash, SourceHash, 1f);
                return;
            }

            IPrologueSequenceService registeredService = GlobalRegistry.PrologueSequence;
            if (registeredService != null && !ReferenceEquals(registeredService, _service))
            {
                GlobalTelemetryBus.PublishPerformanceWarning(RegistrationRejectedHash, SourceHash, 1f);
                return;
            }

            _service.Configure(this);
            GlobalRegistry.RegisterPrologueSequenceRuntime(_service);
            _registeredService = ReferenceEquals(GlobalRegistry.PrologueSequence, _service);
            if (!_registeredService)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(RegistrationRejectedHash, SourceHash, 1f);
                return;
            }

            CacheRuntimeServices();
            BindInputIfAvailable();
            if (Application.isPlaying)
                BaselineSkipInputSignalSequence();
            RegisterHotSwap();

            if (autoRunOnEnable && Application.isPlaying)
            {
                DisposeRunCancellationSource();
                // COLD ALLOC: CancellationTokenSource[1] - auto-run cancellation bridge for disable/dev-skip Awaitable interruption - owner: PrologueSequenceRegistryBridge
                _runCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
                _ = RunAutoSequenceAsync(_service, _runCancellationSource);
            }
        }

        private void OnDisable()
        {
            RequestRunCancellation(PrologueCancelReasons.ExplicitCancel);

            if (_registeredService && _service != null)
            {
                GlobalRegistry.UnregisterPrologueSequenceRuntime(_service);
                _registeredService = false;
            }

            UnbindInput();
            UnregisterHotSwap();
            ClearRuntimeServiceCache();
            _skipRequested = false;
        }

        private void OnDestroy()
        {
            DisposeRunCancellationSource();
        }

        public bool TryGetOrbitalSnapshot(out PrologueOrbitalSnapshot snapshot)
        {
            IOrbitalDirector orbital = _orbitalDirector;
            if (orbital != null && orbital.TryGetSnapshot(out OrbitalDirectorSnapshot source))
            {
                snapshot = new PrologueOrbitalSnapshot(
                    source.UniverseVelocity,
                    source.PlanetDistanceMeters,
                    source.ReentryHeat01,
                    source.CloudWhiteout01,
                    source.Sequence,
                    source.MathLod,
                    source.Flags);
                return true;
            }

            snapshot = default;
            return false;
        }

        public bool TryConsumeAtmosphericReentry(out PrologueAtmosphericReentrySnapshot snapshot)
        {
            ReadOnlySpan<AtmosphericReentrySignal> signals = SignalBus<AtmosphericReentrySignal>.GetFrameSnapshot();
            int frame = Time.frameCount;
            if (_atmosphereSnapshotFrame != frame)
            {
                _atmosphereSnapshotFrame = frame;
                _atmosphereSnapshotCursor = 0;
            }

            while (_atmosphereSnapshotCursor < signals.Length)
            {
                AtmosphericReentrySignal signal = signals[_atmosphereSnapshotCursor++];
                if (!IsValidAtmosphericReentrySignal(in signal))
                    continue;

                snapshot = new PrologueAtmosphericReentrySnapshot(
                    signal.AltitudeMeters,
                    signal.UniverseVelocityMetersPerSecond,
                    signal.Heat01,
                    signal.Sequence,
                    signal.Phase,
                    signal.Flags);
                return true;
            }

            snapshot = default;
            return false;
        }

        public bool TryConsumePrologueComplete(out PrologueCompleteSnapshot snapshot)
        {
            ReadOnlySpan<PrologueCompleteSignal> signals = SignalBus<PrologueCompleteSignal>.GetFrameSnapshot();
            int frame = Time.frameCount;
            if (_completeSnapshotFrame != frame)
            {
                _completeSnapshotFrame = frame;
                _completeSnapshotCursor = 0;
            }

            while (_completeSnapshotCursor < signals.Length)
            {
                PrologueCompleteSignal signal = signals[_completeSnapshotCursor++];
                if (!IsValidManualCompleteSignal(in signal))
                    continue;

                snapshot = new PrologueCompleteSnapshot(
                    signal.Frame,
                    signal.WhiteoutHoldSeconds,
                    signal.Sequence,
                    signal.Phase,
                    signal.Flags);
                return true;
            }

            snapshot = default;
            return false;
        }

        public bool IsOceanSurfaceReady(bool allowProxy)
        {
            if (_observedHighResSurfaceReady || (allowProxy && _observedProxySurfaceReady))
                return true;

            IStreamingBackpressureService streaming = _streamingBackpressure;
            if (oceanSurfaceChunkId != 0 && streaming != null && streaming.IsChunkResident(oceanSurfaceChunkId))
            {
                _observedHighResSurfaceReady = true;
                return true;
            }

            if (allowProxy && streaming != null && streaming.ActiveImpostorCount > 0)
            {
                _observedProxySurfaceReady = true;
                return true;
            }

            ReadOnlySpan<SectorResidencyHydratedSignal> signals = SignalBus<SectorResidencyHydratedSignal>.GetFrameSnapshot();
            int frame = Time.frameCount;
            if (_residencySnapshotFrame != frame)
            {
                _residencySnapshotFrame = frame;
                _residencySnapshotCursor = 0;
            }

            while (_residencySnapshotCursor < signals.Length)
            {
                SectorResidencyHydratedSignal signal = signals[_residencySnapshotCursor++];
                bool proxy = (signal.Flags & SectorResidencyHydratedSignal.FlagProxyFallback) != 0;
                if (!MatchesOceanChunk(signal.ChunkId, allowProxy, proxy))
                    continue;

                if (proxy)
                    _observedProxySurfaceReady = true;
                else
                    _observedHighResSurfaceReady = true;

                if (!proxy || allowProxy)
                    return true;
            }

            return false;
        }

        public void PrepareSequenceRun()
        {
            ResetTransientSequenceState();
        }

        public Awaitable DelayDilatedAsync(float seconds, CancellationToken cancellationToken)
        {
            if (!IsDevelopmentBuild)
                return AwaitableExtension.DelayDilated(seconds, cancellationToken);

            return DelayDilatedDevelopmentInterruptibleAsync(seconds, cancellationToken);
        }

        public Awaitable NextFrameAsync(CancellationToken cancellationToken)
        {
            return AwaitableDebtMonitor.NextFrameAsync(cancellationToken);
        }

        public void PublishInputLock(PrologueInputLockFlags flags, bool paused)
        {
            SystemPauseSignal signal = default;
            signal.SourceHash = SourceHash;
            signal.Frame = CurrentFrame;
            signal.Sequence = unchecked(++_sequence);
            signal.Paused = paused ? (byte)1 : (byte)0;
            signal.Flags = (byte)flags;
            signal.RestoreScalar = 1f;
            GlobalSignals.Publish(in signal);
        }

        public void PublishMuffledBreathing(float intensity01, float durationSeconds)
        {
            float safeIntensity = math.saturate(intensity01);
            MixerStateSignal mixer = default;
            mixer.MixerStateHash = MuffledBreathingHash;
            mixer.SourceHash = SourceHash;
            mixer.Intensity01 = safeIntensity;
            mixer.DuckingDb = -18f * safeIntensity;
            mixer.Frame = CurrentFrame;
            mixer.Flags = 1;
            GlobalSignals.Publish(in mixer);

            AcousticPingSignal ping = default;
            ping.PositionAup = Hecton8.World.AbsoluteUniversePosition.FromRuntimePosition(Vector3.zero);
            ping.RadiusMeters = math.max(8f, durationSeconds * 12f);
            ping.Intensity01 = safeIntensity;
            ping.SourceId = MuffledBreathingHash;
            ping.Channel = AcousticPingSignal.ChannelFabricScrape;
            ping.Flags = AcousticPingSignal.FlagFabricScrape;
            GlobalSignals.Publish(in ping);
        }

        public void PublishHullTempCriticalWarning(float severity01)
        {
            VocalWarningSignal signal = default;
            signal.WarningHash = HullTempCriticalHash;
            signal.SourceId = SourceHash;
            signal.Severity01 = math.saturate(severity01);
            signal.CooldownSeconds = 2f;
            signal.Priority = 1;
            GlobalSignals.Publish(in signal);
        }

        public void PublishHeavyRumble(float intensity01, float durationSeconds)
        {
            HapticRequest signal = default;
            signal.Intensity01 = math.saturate(intensity01);
            signal.DurationSeconds = math.max(0.05f, durationSeconds);
            signal.Frequency01 = 0.92f;
            signal.SourceHash = SourceHash;
            signal.Frame = CurrentFrame;
            signal.Channel = HapticRequest.ChannelVehicleCritical;
            GlobalSignals.Publish(in signal);
        }

        public void PublishManualReleasePrompt()
        {
            DiegeticHudSignal diegetic = default;
            diegetic.MessageHash = ManualReleaseHash;
            diegetic.ContextHash = ManualReleaseContextHash;
            diegetic.SourceHash = SourceHash;
            diegetic.Frame = CurrentFrame;
            diegetic.PromptKind = DiegeticHudSignal.PromptManualRelease;
            diegetic.Priority = 3;
            diegetic.Flags = DiegeticHudSignal.FlagPersistent;
            GlobalSignals.Publish(in diegetic);

            HUDNotificationSignal hud = default;
            hud.MessageHash = ManualReleaseHash;
            hud.ContextHash = ManualReleaseContextHash;
            hud.SourceId = SourceHash;
            hud.Frame = CurrentFrame;
            hud.Severity = 2;
            hud.Flags = 1;
            GlobalSignals.Publish(in hud);
        }

        public void PublishMassiveImpact()
        {
            CameraJuiceSignals.PublishImpact(MassiveImpactSeverity, Vector3.zero, Vector3.down);
        }

        public void PublishOceanHandoff()
        {
            PrologueCompleteSignal signal = default;
            signal.CapsuleAup = Hecton8.World.AbsoluteUniversePosition.FromRuntimePosition(Vector3.zero);
            signal.Frame = CurrentFrame;
            signal.SourceHash = SourceHash;
            signal.Sequence = unchecked(++_sequence);
            signal.WhiteoutHoldSeconds = 0.12f;
            signal.Flags = PrologueCompleteSignal.FlagForceWhiteout;
            signal.Phase = PrologueCompleteSignal.PhaseOceanHandoff;
            GlobalSignals.Publish(in signal);
        }

        public void ZeroUniverseVelocity()
        {
            IOrbitalDirector orbital = _orbitalDirector;
            orbital?.ForceZeroUniverseVelocity(0);
        }

        public void ForceShallowWaterHydration()
        {
            _observedHighResSurfaceReady = true;
            _observedProxySurfaceReady = true;

            SectorResidencyHydratedSignal signal = default;
            signal.CenterAup = Hecton8.World.AbsoluteUniversePosition.FromRuntimePosition(Vector3.zero);
            signal.ChunkId = oceanSurfaceChunkId != 0 ? oceanSurfaceChunkId : ShallowWaterChunkHash;
            signal.Frame = CurrentFrame;
            signal.RadiusMetersQ = 64;
            signal.Flags = SectorResidencyHydratedSignal.FlagPinned | SectorResidencyHydratedSignal.FlagProxyFallback;
            signal.ResidencyState = 1;
            SignalBus<SectorResidencyHydratedSignal>.Push(in signal);
        }

        public void PushTelemetry(PrologueStage stage, uint stateHash, byte flags)
        {
            uint stageHash = unchecked(SourceHash ^ ((uint)stage << 24) ^ flags);
            GlobalTelemetryBus.PublishPrologueStage(stageHash, stateHash, flags);
        }

        public void DumpBlackBox()
        {
            GlobalTelemetryBus.RequestEmergencyFlushAsync();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Input:
                    UnbindInput();
                    BindInputIfAvailable(currentService as IInputService);
                    break;
                case GlobalRegistryServiceSlot.OrbitalDirectorRuntime:
                    _orbitalDirector = currentService as IOrbitalDirector;
                    break;
                case GlobalRegistryServiceSlot.StreamingBackpressureRuntime:
                    _streamingBackpressure = currentService as IStreamingBackpressureService;
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    _tickDispatcher = currentService as ITickDispatcher;
                    break;
            }
        }

        private void ResolveService()
        {
            _service = null;

            if (sequenceComponent is IPrologueSequenceService serializedService)
            {
                _service = serializedService;
                return;
            }

            MonoBehaviour[] components = GetComponents<MonoBehaviour>(); // COLD ALLOC: MonoBehaviour[] - one-shot interface discovery for inspector-free bridge setup - owner: PrologueSequenceRegistryBridge
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] is IPrologueSequenceService discovered)
                {
                    sequenceComponent = components[i];
                    _service = discovered;
                    return;
                }
            }
        }

        private void RegisterHotSwap()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void UnregisterHotSwap()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private void CacheRuntimeServices()
        {
            _isDevelopmentBuild = GlobalRegistry.IsDevelopmentBuild;
            _orbitalDirector = GlobalRegistry.OrbitalDirector;
            _streamingBackpressure = GlobalRegistry.StreamingBackpressure;
            _tickDispatcher = GlobalRegistry.TickDispatcher;
        }

        private void ClearRuntimeServiceCache()
        {
            _isDevelopmentBuild = false;
            _inputService = null;
            _orbitalDirector = null;
            _streamingBackpressure = null;
            _tickDispatcher = null;
            ResetLowTierCache();
        }

        private void ResetLowTierCache()
        {
            _cachedLowTier = false;
            _pendingLowTier = false;
            _hasLowTierCache = false;
            _lastObservedLowTierPolicy = false;
            _lastObservedForcedLowMemory = false;
            _lowTierCandidateFrame = 0;
            _lowTierPolicyProbeFrame = -1;
            _memoryPressureSnapshotFrame = -1;
            _memoryPressureSnapshotCursor = 0;
        }

        private void BindInputIfAvailable()
        {
            BindInputIfAvailable(GlobalRegistry.Input);
        }

        private void BindInputIfAvailable(IInputService input)
        {
            if (input == null)
                return;

            _inputService = input;
        }

        private void UnbindInput()
        {
            _inputService = null;
        }

        private void HandleSkipRequested()
        {
            if (!IsDevelopmentBuild)
                return;

            _skipRequested = true;
            RequestRunCancellation(PrologueCancelReasons.DevSkip);
        }

        private void ConsumeSkipInputSignals()
        {
            ReadOnlySpan<PlayerInputSignal> signals = SignalBus<PlayerInputSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PlayerInputSignal signal = signals[i];
                if (signal.SourceHash != PlayerInputSignalSourceHash ||
                    signal.Command != PlayerInputSignalCommands.Cancel ||
                    !IsNewerInputSequence(signal.Sequence, _lastPlayerInputSignalSequence))
                    continue;

                _lastPlayerInputSignalSequence = signal.Sequence;
                HandleSkipRequested();
                return;
            }
        }

        private void BaselineSkipInputSignalSequence()
        {
            ReadOnlySpan<PlayerInputSignal> signals = SignalBus<PlayerInputSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PlayerInputSignal signal = signals[i];
                if (signal.SourceHash == PlayerInputSignalSourceHash &&
                    IsNewerInputSequence(signal.Sequence, _lastPlayerInputSignalSequence))
                    _lastPlayerInputSignalSequence = signal.Sequence;
            }
        }

        private static bool IsNewerInputSequence(uint candidate, uint current)
        {
            return candidate != 0u && candidate != current && unchecked(candidate - current) < 0x80000000u;
        }

        private bool MatchesOceanChunk(long chunkId, bool allowProxy, bool proxy)
        {
            if (oceanSurfaceChunkId != 0)
                return chunkId == oceanSurfaceChunkId;

            if (chunkId == ShallowWaterChunkHash)
                return true;

            return allowAnyHydratedChunkFallback && allowProxy && proxy;
        }

        private static bool IsValidAtmosphericReentrySignal(in AtmosphericReentrySignal signal)
        {
            return math.isfinite(signal.AltitudeMeters) &&
                   math.isfinite(signal.UniverseVelocityMetersPerSecond) &&
                   math.isfinite(signal.Heat01) &&
                   (signal.Phase == AtmosphericReentrySignal.PhasePlasma ||
                    signal.Phase == AtmosphericReentrySignal.PhaseWhiteout) &&
                   signal.Heat01 > ReentryHeatStartThreshold01;
        }

        private static bool IsValidManualCompleteSignal(in PrologueCompleteSignal signal)
        {
            return signal.SourceHash == ManualOverrideSourceHash &&
                   signal.Sequence != 0 &&
                   signal.Phase == PrologueCompleteSignal.PhaseOceanHandoff &&
                   (signal.Flags & PrologueCompleteSignal.FlagForceWhiteout) != 0 &&
                   math.isfinite(signal.WhiteoutHoldSeconds) &&
                   signal.WhiteoutHoldSeconds >= 0f;
        }

        private bool ResolveLowTierWithHysteresis()
        {
            int frame = Time.frameCount;
            bool forcedLowMemory;
            bool requestedLowTier = ResolveObservedLowTierPolicy(frame, out forcedLowMemory);

            if (!_hasLowTierCache)
            {
                _cachedLowTier = requestedLowTier;
                _pendingLowTier = requestedLowTier;
                _lowTierCandidateFrame = frame;
                _hasLowTierCache = true;
                return _cachedLowTier;
            }

            if (forcedLowMemory && !_cachedLowTier)
            {
                _cachedLowTier = true;
                _pendingLowTier = true;
                _lowTierCandidateFrame = frame;
                return true;
            }

            if (requestedLowTier == _cachedLowTier)
            {
                _pendingLowTier = requestedLowTier;
                _lowTierCandidateFrame = frame;
                return _cachedLowTier;
            }

            if (requestedLowTier != _pendingLowTier)
            {
                _pendingLowTier = requestedLowTier;
                _lowTierCandidateFrame = frame;
                return _cachedLowTier;
            }

            if (frame - _lowTierCandidateFrame >= LowTierHysteresisFrames)
            {
                _cachedLowTier = requestedLowTier;
                _pendingLowTier = requestedLowTier;
                _lowTierCandidateFrame = frame;
            }

            return _cachedLowTier;
        }

        private bool ResolveObservedLowTierPolicy(int frame, out bool forcedLowMemory)
        {
            if (TryObserveCriticalMemoryPressure(frame))
            {
                forcedLowMemory = true;
                _lastObservedForcedLowMemory = true;
                _lastObservedLowTierPolicy = true;
                _lowTierPolicyProbeFrame = frame;
                return true;
            }

            if (_lowTierPolicyProbeFrame < 0 ||
                frame - _lowTierPolicyProbeFrame >= LowTierProbeIntervalFrames)
            {
                _lastObservedLowTierPolicy = ReadLowTierPolicy(out forcedLowMemory);
                _lastObservedForcedLowMemory = forcedLowMemory;
                _lowTierPolicyProbeFrame = frame;
                return _lastObservedLowTierPolicy;
            }

            forcedLowMemory = _lastObservedForcedLowMemory;
            return _lastObservedLowTierPolicy;
        }

        private bool TryObserveCriticalMemoryPressure(int frame)
        {
            ReadOnlySpan<MemoryPressureSignal> signals = SignalBus<MemoryPressureSignal>.GetFrameSnapshot();
            if (_memoryPressureSnapshotFrame != frame)
            {
                _memoryPressureSnapshotFrame = frame;
                _memoryPressureSnapshotCursor = 0;
            }

            while (_memoryPressureSnapshotCursor < signals.Length)
            {
                MemoryPressureSignal signal = signals[_memoryPressureSnapshotCursor++];
                if (signal.Severity >= CriticalMemoryPressureSeverity)
                    return true;
            }

            return false;
        }

        private static bool ReadLowTierPolicy(out bool forcedLowMemory)
        {
            forcedLowMemory = GlobalRegistry.H8_LOW_MEMORY_PROFILE;
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            return forcedLowMemory ||
                   tier == HectonQualityTier.Unknown ||
                   tier == HectonQualityTier.Low ||
                   tier == HectonQualityTier.Mx350;
        }

        private async Awaitable RunAutoSequenceAsync(IPrologueSequenceService service, CancellationTokenSource source)
        {
            try
            {
                await service.RunPrologueSequenceAsync(source.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(CancellationFaultHash, SourceHash, 1f);
            }
            finally
            {
                if (ReferenceEquals(_runCancellationSource, source))
                {
                    _runCancellationSource = null;
                    source.Dispose();
                }
            }
        }

        private async Awaitable DelayDilatedDevelopmentInterruptibleAsync(float seconds, CancellationToken cancellationToken)
        {
            if (!math.isfinite(seconds) || seconds <= 0f)
                return;

            double remainingSeconds = seconds;
            ITickDispatcher dispatcher = _tickDispatcher;
            while (remainingSeconds > 0d)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (ShouldSkipPrologue)
                {
                    RequestRunCancellation(PrologueCancelReasons.DevSkip);
                    throw new OperationCanceledException(cancellationToken);
                }

                await AwaitableDebtMonitor.NextFrameAsync(cancellationToken);
                dispatcher = _tickDispatcher;

                H8TimeSnapshot snapshot = dispatcher != null
                    ? dispatcher.TimeSnapshot
                    : new H8TimeSnapshot(0d, SystemDispatcher.CurrentFrameDeltaTime, 0d, SystemDispatcher.CurrentFrameUnscaledDeltaTime);
                double deltaTime = snapshot.DeltaTime;
                if (deltaTime > 0d && math.isfinite(deltaTime))
                    remainingSeconds -= deltaTime;
            }
        }

        private void ResetTransientSequenceState()
        {
            _skipRequested = false;
            _observedHighResSurfaceReady = false;
            _observedProxySurfaceReady = false;
            _atmosphereSnapshotFrame = -1;
            _completeSnapshotFrame = -1;
            _residencySnapshotFrame = -1;
            _atmosphereSnapshotCursor = 0;
            _completeSnapshotCursor = 0;
            _residencySnapshotCursor = 0;
            ResetLowTierCache();
        }

        private void RequestRunCancellation(byte reason)
        {
            try
            {
                _service?.CancelSequence(reason);
            }
            catch (Exception)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(CancellationFaultHash, SourceHash, 1f);
            }

            CancellationTokenSource source = _runCancellationSource;
            CancelRunSourceNoThrow(source);
        }

        private void CancelRunSourceNoThrow(CancellationTokenSource source)
        {
            if (source == null || source.IsCancellationRequested)
                return;

            try
            {
                source.Cancel();
            }
            catch (Exception)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(CancellationFaultHash, SourceHash, 1f);
            }
        }

        private void DisposeRunCancellationSource()
        {
            CancellationTokenSource source = _runCancellationSource;
            if (source == null)
                return;

            _runCancellationSource = null;
            CancelRunSourceNoThrow(source);
            source.Dispose();
        }
    }
}
