using System;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Audio.Prologue
{
    /// <summary>
    /// Visual-sync bridge from orbital prologue stage signals into procedural helmet DSP.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PrologueAcousticOrchestrator : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener, IGlobalRegistryHotSwapRefListener
    {
        private const uint SourceHash = 0xAC0571C5u;
        private const uint PrologueSequenceSourceHash = PrologueSignalSourceHashes.SequenceDirector;
        private const uint OrbitalRelativitySourceHash = PrologueSignalSourceHashes.OrbitalRelativityDirector;
        private const float MinimumLowPassCutoffHertz = 80f;
        private const float CutoffPublishEpsilonHertz = 1f;
        private const float GainPublishEpsilon = 0.0005f;
        private const float MaxPresentationDeltaSeconds = 0.25f;
        private const float SplashdownLowPassCutoffHertz = 350f;
        private const float PlasmaHapticThreshold01 = 0.64f;
        private const float PlasmaHapticDurationSeconds = 0.055f;
        private const float SplashdownHapticDurationSeconds = 0.18f;
        private const int PlasmaHapticCooldownFrames = 3;

        [Header("Filter")]
        [SerializeField] private float vacuumLowPassCutoffHertz = 150f;
        [SerializeField] private float oceanLowPassCutoffHertz = 22000f;
        [SerializeField] private float oceanFilterSweepSeconds = 3f;

        [Header("Plasma")]
        [SerializeField] private float plasmaFullStressVelocityMetersPerSecond = 7800f;
        [SerializeField] private float plasmaGranularStressGain = 0.85f;
        [SerializeField] private float vacuumLfeGain = 0.22f;
        [SerializeField] private float plasmaLfeGain = 0.32f;

        [Header("Splashdown")]
        [SerializeField] private float splashdownGain = 1f;

        private IAudioService _audioService;
        private ITickDispatcher _tickDispatcher;
        private bool _lateFrameRegistered;
        private bool _hotSwapRegistered;
        private float _qualityWeight = 1f;
        private byte _qualityTierByte;
        private int _lastLateFrame = -1;
        private int _lastAtmosphericFrame = -1;
        private int _lastAcousticStressFrame = -1;
        private int _lastCompleteFrame = -1;
        private uint _transitionSequence;
        private ushort _lastCompleteSequence;
        private ushort _lastWhiteoutCompleteSequence;
        private uint _lastWhiteoutCompleteSourceHash;
        private byte _stage = AudioTransitionState.StageSpace;
        private byte _lastPublishedStage;
        private byte _lastPublishedFlags;
        private byte _lastPublishedQualityTierByte = byte.MaxValue;
        private float _velocityMetersPerSecond;
        private float _heat01;
        private float _acousticStress01;
        private float _stressLfeGain01;
        private float _stressGranularStress01;
        private float _currentLowPassCutoffHertz = 150f;
        private float _lastPublishedLowPassCutoffHertz = -1f;
        private float _lastPublishedLfeGain = -1f;
        private float _lastPublishedGranularStress = -1f;
        private float _lastPublishedSplashdownGain = -1f;
        private float _lastPublishedPortalBlend = -1f;
        private float _sweepStartLowPassCutoffHertz = 150f;
        private float _sweepElapsedSeconds;
        private bool _sweepActive;
        private bool _sweepSnapHeldForPublish;
        private bool _splashdownPending;
        private bool _prologueArmed;
        private bool _hasCompleteSequence;
        private bool _hasWhiteoutCompleteSequence;
        private bool _forcePublishTransition;
        private bool _hasStressOverride;
        private uint _tickCount;
        private uint _lastSplashdownHapticSequence;
        private uint _lastPlasmaHapticFrame = uint.MaxValue;
        private int _hapticSignalDropCount;

        /// <inheritdoc />
        public int TickCount => unchecked((int)_tickCount);

        private void OnEnable()
        {
            PrologueReentrySignalLanes.Warm();
            RefreshRuntimeServicesCold();
            RefreshQualityPolicy();
            ResetTransientState();

            RegisterLateFrame();

            RegisterHotSwap();

        }

        private void OnDisable()
        {
            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _lateFrameRegistered = false;
            }

            if (_hotSwapRegistered)
            {
                GlobalRegistry.TryUnregisterHotSwapListener(this);
                _hotSwapRegistered = false;
            }

            PublishNeutralTransitionOnDisable();
            _audioService = null;
            _tickDispatcher = null;
        }

        private void ResetTransientState()
        {
            _lastLateFrame = -1;
            _lastAtmosphericFrame = -1;
            _lastAcousticStressFrame = -1;
            _lastCompleteFrame = -1;
            _lastCompleteSequence = 0;
            _lastWhiteoutCompleteSequence = 0;
            _lastWhiteoutCompleteSourceHash = 0u;
            _stage = AudioTransitionState.StageSpace;
            _lastPublishedStage = 0;
            _lastPublishedFlags = 0;
            _lastPublishedQualityTierByte = byte.MaxValue;
            _velocityMetersPerSecond = 0f;
            _heat01 = 0f;
            _acousticStress01 = 0f;
            _stressLfeGain01 = 0f;
            _stressGranularStress01 = 0f;
            _currentLowPassCutoffHertz = ClampCutoff(oceanLowPassCutoffHertz);
            _lastPublishedLowPassCutoffHertz = -1f;
            _lastPublishedLfeGain = -1f;
            _lastPublishedGranularStress = -1f;
            _lastPublishedSplashdownGain = -1f;
            _lastPublishedPortalBlend = -1f;
            _sweepStartLowPassCutoffHertz = ClampCutoff(vacuumLowPassCutoffHertz);
            _sweepElapsedSeconds = 0f;
            _sweepActive = false;
            _sweepSnapHeldForPublish = false;
            _splashdownPending = false;
            _prologueArmed = false;
            _hasCompleteSequence = false;
            _hasWhiteoutCompleteSequence = false;
            _forcePublishTransition = false;
            _hasStressOverride = false;
            _lastSplashdownHapticSequence = 0u;
            _lastPlasmaHapticFrame = uint.MaxValue;
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastLateFrame == frame)
                return;

            _lastLateFrame = frame;
            _tickCount++;
            RefreshQualityPolicy();
            ConsumeAtmosphericSignals();
            ConsumeReentryAcousticStressSignals();
            ConsumePrologueCompleteSignals();
            AdvanceFilterSweep(ResolveUnscaledDeltaTime());
            PublishAudioTransition(frame);
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceRebound(
            GlobalRegistryServiceSlot serviceSlot,
            ref object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Audio:
                    CacheAudioService(currentService as IAudioService);
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    _tickDispatcher = currentService as ITickDispatcher;
                    break;
            }
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Audio:
                    CacheAudioService(currentService as IAudioService);
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    if (ReferenceEquals(previousService, currentService))
                    {
                        _tickDispatcher = currentService as ITickDispatcher;
                        break;
                    }

                    RebindDispatcher(currentService as ITickDispatcher);
                    break;
            }
        }

        private void RebindDispatcher(ITickDispatcher dispatcher)
        {
            _tickDispatcher = dispatcher;
            _lateFrameRegistered = false;
            if (_tickDispatcher != null && isActiveAndEnabled)
                RegisterLateFrame();
        }

        private void RegisterLateFrame()
        {
            if (_lateFrameRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void RegisterHotSwap()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void ConsumeAtmosphericSignals()
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastAtmosphericFrame == frame)
                return;

            _lastAtmosphericFrame = frame;
            if (_stage == AudioTransitionState.StageOceanHandoff)
                return;

            ReadOnlySpan<AtmosphericReentrySignal> signals = SignalBus<AtmosphericReentrySignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                AtmosphericReentrySignal signal = signals[i];
                if (!IsValidAtmosphericSignal(in signal))
                    continue;

                _prologueArmed = true;
                _velocityMetersPerSecond = NonNegativeFiniteOrZero(signal.UniverseVelocityMetersPerSecond);
                _heat01 = ResolveHeat01(in signal);

                if (signal.Phase == AtmosphericReentrySignal.PhaseWhiteout ||
                    (signal.Flags & AtmosphericReentrySignal.FlagWhiteoutRequested) != 0)
                {
                    _stage = AudioTransitionState.StageWhiteout;
                    _currentLowPassCutoffHertz = ClampCutoff(vacuumLowPassCutoffHertz);
                }
                else if (signal.Phase == AtmosphericReentrySignal.PhasePlasma)
                {
                    _stage = AudioTransitionState.StagePlasma;
                    _currentLowPassCutoffHertz = ClampCutoff(vacuumLowPassCutoffHertz);
                }
                else
                {
                    _stage = AudioTransitionState.StageSpace;
                    _currentLowPassCutoffHertz = ClampCutoff(vacuumLowPassCutoffHertz);
                }
            }
        }

        private void ConsumeReentryAcousticStressSignals()
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastAcousticStressFrame == frame)
                return;

            _lastAcousticStressFrame = frame;
            if (_stage == AudioTransitionState.StageOceanHandoff)
                return;

            _hasStressOverride = false;
            _acousticStress01 = 0f;
            _stressLfeGain01 = 0f;
            _stressGranularStress01 = 0f;

            ReadOnlySpan<ReentryAcousticStressSignal> signals = SignalBus<ReentryAcousticStressSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                ReentryAcousticStressSignal signal = signals[i];
                if (!IsValidStressSignal(in signal))
                    continue;

                _prologueArmed = true;
                _hasStressOverride = true;
                _velocityMetersPerSecond = NonNegativeFiniteOrZero(signal.UniverseVelocityMetersPerSecond);
                _heat01 = math.max(SaturateFiniteOrZero(signal.Heat01), SaturateFiniteOrZero(signal.Stress01));
                _acousticStress01 = SaturateFiniteOrZero(signal.Stress01);
                _stressLfeGain01 = SaturateFiniteOrZero(signal.LfeGain01);
                _stressGranularStress01 = SaturateFiniteOrZero(signal.GranularStress01);

                if ((signal.Flags & ReentryAcousticStressSignal.FlagAuthoritativeFilter) != 0)
                    _currentLowPassCutoffHertz = ClampCutoff(signal.LowPassCutoffHz);

                if ((signal.Flags & ReentryAcousticStressSignal.FlagSplashdown) != 0)
                {
                    _splashdownPending = true;
                    _forcePublishTransition = true;
                }

                if (signal.Phase == ReentryAcousticStressSignal.PhaseWhiteout ||
                    signal.Phase == ReentryAcousticStressSignal.PhaseSplashdown)
                {
                    _stage = AudioTransitionState.StageWhiteout;
                }
                else if (signal.Phase == ReentryAcousticStressSignal.PhasePlasma)
                {
                    _stage = AudioTransitionState.StagePlasma;
                }
                else
                {
                    _stage = AudioTransitionState.StageSpace;
                }
            }
        }

        private void ConsumePrologueCompleteSignals()
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastCompleteFrame == frame)
                return;

            _lastCompleteFrame = frame;
            ReadOnlySpan<PrologueCompleteSignal> signals = SignalBus<PrologueCompleteSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PrologueCompleteSignal signal = signals[i];
                if (!IsValidCompleteHold(signal.WhiteoutHoldSeconds))
                    continue;

                bool sequenceOceanHandoff = signal.Phase == PrologueCompleteSignal.PhaseOceanHandoff &&
                                             signal.SourceHash == PrologueSequenceSourceHash;
                if (!sequenceOceanHandoff && !IsWhiteoutOnlyComplete(in signal))
                    continue;

                if (!sequenceOceanHandoff)
                {
                    if (_stage == AudioTransitionState.StageOceanHandoff)
                        continue;

                    bool newWhiteoutSequence = !_hasWhiteoutCompleteSequence ||
                                                signal.Sequence != _lastWhiteoutCompleteSequence ||
                                                signal.SourceHash != _lastWhiteoutCompleteSourceHash;
                    _prologueArmed = true;
                    _stage = AudioTransitionState.StageWhiteout;
                    _currentLowPassCutoffHertz = ClampCutoff(vacuumLowPassCutoffHertz);
                    if (newWhiteoutSequence)
                    {
                        _lastWhiteoutCompleteSequence = signal.Sequence;
                        _lastWhiteoutCompleteSourceHash = signal.SourceHash;
                        _hasWhiteoutCompleteSequence = true;
                        _forcePublishTransition = true;
                    }

                    continue;
                }

                if (_hasCompleteSequence)
                    continue;

                _prologueArmed = true;
                _hasCompleteSequence = true;
                _lastCompleteSequence = signal.Sequence;
                _splashdownPending = true;
                _sweepElapsedSeconds = 0f;
                _sweepActive = true;
                _currentLowPassCutoffHertz = ClampCutoff(SplashdownLowPassCutoffHertz);
                _sweepStartLowPassCutoffHertz = _currentLowPassCutoffHertz;
                _sweepSnapHeldForPublish = true;
                _forcePublishTransition = true;

                _stage = AudioTransitionState.StageOceanHandoff;
            }
        }

        private void AdvanceFilterSweep(float deltaSeconds)
        {
            if (!_sweepActive)
                return;

            if (_sweepSnapHeldForPublish)
            {
                _currentLowPassCutoffHertz = ClampCutoff(_sweepStartLowPassCutoffHertz);
                return;
            }

            float duration = PositiveFiniteOrMinimum(oceanFilterSweepSeconds, 0.001f);
            _sweepElapsedSeconds = math.min(duration, _sweepElapsedSeconds + math.max(0f, deltaSeconds));
            float t = math.saturate(_sweepElapsedSeconds * math.rcp(duration));
            t = t * t * (3f - 2f * t);
            _currentLowPassCutoffHertz = math.lerp(
                ClampCutoff(_sweepStartLowPassCutoffHertz),
                ClampCutoff(oceanLowPassCutoffHertz),
                t);
            if (_sweepElapsedSeconds >= duration)
            {
                _sweepActive = false;
                _forcePublishTransition = true;
            }
        }

        private void PublishAudioTransition(int frame)
        {
            if (!_prologueArmed)
                return;

            IAudioService audioService = _audioService;
            if (audioService == null || !audioService.IsInitialized)
                return;

            bool nonFiniteGuard = !math.isfinite(_velocityMetersPerSecond) ||
                                  !math.isfinite(_heat01) ||
                                  !math.isfinite(_currentLowPassCutoffHertz) ||
                                  (_hasStressOverride &&
                                   (!math.isfinite(_acousticStress01) ||
                                    !math.isfinite(_stressLfeGain01) ||
                                    !math.isfinite(_stressGranularStress01)));
            float velocityMetersPerSecond = NonNegativeFiniteOrZero(_velocityMetersPerSecond);
            float velocity01 = ResolveVelocity01(velocityMetersPerSecond);
            float heat01 = SaturateFiniteOrZero(_heat01);
            bool plasmaStage = _stage == AudioTransitionState.StagePlasma || _stage == AudioTransitionState.StageWhiteout;
            bool portalStage = _stage == AudioTransitionState.StageOceanHandoff;
            float qualityCurve = ResolveQualityCurve01();

            byte flags = 0;
            if (_splashdownPending)
                flags |= AudioTransitionState.FlagSplashdown;
            if (portalStage)
                flags |= AudioTransitionState.FlagPortalActive;
            if (nonFiniteGuard)
                flags |= AudioTransitionState.FlagNonFiniteGuard;

            float lowPassCutoffHertz = ClampCutoff(_currentLowPassCutoffHertz);
            float lfeGain = ResolveLfeGain(velocity01, plasmaStage, portalStage);
            if (!portalStage && _hasStressOverride)
                lfeGain = math.max(lfeGain, SaturateFiniteOrZero(_stressLfeGain01));

            float granularGain = SaturateFiniteOrZero(plasmaGranularStressGain);
            float granularStress = plasmaStage ? math.saturate(velocity01 * granularGain) * qualityCurve : 0f;
            if (plasmaStage && _hasStressOverride)
            {
                float acousticStress = SaturateFiniteOrZero(_acousticStress01) * qualityCurve;
                granularStress = math.max(granularStress, math.max(SaturateFiniteOrZero(_stressGranularStress01), acousticStress));
            }

            if (granularStress > GainPublishEpsilon)
                flags |= AudioTransitionState.FlagGranularEnabled;
            float splashdownGain01 = _splashdownPending ? SaturateFiniteOrZero(splashdownGain) : 0f;
            float portalBlend01 = portalStage ? ResolvePortalBlend01() : 0f;
            if (!ShouldPublishTransition(lowPassCutoffHertz, lfeGain, granularStress, splashdownGain01, portalBlend01, flags))
                return;

            AudioTransitionState state = default;
            state.UniverseVelocityMetersPerSecond = velocityMetersPerSecond;
            state.Heat01 = heat01;
            state.LowPassCutoffHz = lowPassCutoffHertz;
            state.LfeGain01 = lfeGain;
            state.GranularStress01 = granularStress;
            state.SplashdownGain01 = splashdownGain01;
            state.PortalBlend01 = portalBlend01;
            state.Frame = unchecked((uint)frame);
            state.Sequence = ++_transitionSequence;
            state.SourceHash = SourceHash;
            state.Stage = _stage;
            state.Flags = flags;
            state.QualityTier = _qualityTierByte;
            state.AbsoluteTimeSeconds = ResolveAbsoluteTimeSeconds();

            if (!audioService.QueuePrologueAudioTransition(in state))
                return;

            if (_sweepSnapHeldForPublish &&
                (state.Flags & AudioTransitionState.FlagSplashdown) != 0)
            {
                _sweepSnapHeldForPublish = false;
            }

            PublishSynchronizedHaptics(in state);

            _lastPublishedStage = _stage;
            _lastPublishedFlags = flags;
            _lastPublishedQualityTierByte = _qualityTierByte;
            _lastPublishedLowPassCutoffHertz = lowPassCutoffHertz;
            _lastPublishedLfeGain = lfeGain;
            _lastPublishedGranularStress = granularStress;
            _lastPublishedSplashdownGain = splashdownGain01;
            _lastPublishedPortalBlend = portalBlend01;
            _forcePublishTransition = false;
            _splashdownPending = false;
        }

        private void PublishSynchronizedHaptics(in AudioTransitionState state)
        {
            if ((state.Flags & AudioTransitionState.FlagSplashdown) != 0 &&
                state.SplashdownGain01 > GainPublishEpsilon &&
                state.Sequence != _lastSplashdownHapticSequence)
            {
                HapticRequest request = default;
                request.Intensity01 = math.saturate(state.SplashdownGain01);
                request.DurationSeconds = SplashdownHapticDurationSeconds;
                request.Frequency01 = 0.22f;
                request.SourceHash = SourceHash;
                request.Frame = state.Frame;
                request.Channel = HapticRequest.ChannelVehicleCritical;
                request.Flags = HapticRequest.FlagCrush;
                SignalBus<HapticRequest>.TryPushTracked(in request, ref _hapticSignalDropCount);
                _lastSplashdownHapticSequence = state.Sequence;
                return;
            }

            bool plasmaStage = state.Stage == AudioTransitionState.StagePlasma ||
                               state.Stage == AudioTransitionState.StageWhiteout;
            if (!plasmaStage || state.GranularStress01 < PlasmaHapticThreshold01)
                return;

            if (_lastPlasmaHapticFrame != uint.MaxValue &&
                unchecked(state.Frame - _lastPlasmaHapticFrame) < (uint)PlasmaHapticCooldownFrames)
                return;

            HapticRequest plasma = default;
            plasma.Intensity01 = math.saturate(state.GranularStress01 * 0.72f);
            plasma.DurationSeconds = PlasmaHapticDurationSeconds;
            plasma.Frequency01 = math.saturate(0.35f + state.Heat01 * 0.45f);
            plasma.SourceHash = SourceHash;
            plasma.Frame = state.Frame;
            plasma.Channel = HapticRequest.ChannelMicroVibration;
            plasma.Flags = HapticRequest.FlagMicroVibration;
            SignalBus<HapticRequest>.TryPushTracked(in plasma, ref _hapticSignalDropCount);
            _lastPlasmaHapticFrame = state.Frame;
        }

        private void PublishNeutralTransitionOnDisable()
        {
            if (!Application.isPlaying)
                return;

            bool activeTransition = _prologueArmed ||
                                    _sweepActive ||
                                    _splashdownPending ||
                                    _stage != AudioTransitionState.StageSpace ||
                                    (_lastPublishedStage != 0 && _lastPublishedStage != AudioTransitionState.StageSpace);
            if (!activeTransition)
                return;

            IAudioService audioService = _audioService;
            if (audioService == null || !audioService.IsInitialized)
                return;

            AudioTransitionState state = default;
            state.UniverseVelocityMetersPerSecond = 0f;
            state.Heat01 = 0f;
            state.LowPassCutoffHz = ClampCutoff(oceanLowPassCutoffHertz);
            state.LfeGain01 = 0f;
            state.GranularStress01 = 0f;
            state.SplashdownGain01 = 0f;
            state.PortalBlend01 = 0f;
            state.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            state.Sequence = ++_transitionSequence;
            state.SourceHash = SourceHash;
            state.Stage = AudioTransitionState.StageSpace;
            state.Flags = 0;
            state.QualityTier = _qualityTierByte;
            state.AbsoluteTimeSeconds = ResolveAbsoluteTimeSeconds();

            audioService.QueuePrologueAudioTransition(in state);
        }

        private void CacheAudioService(IAudioService audioService)
        {
            _audioService = audioService;
        }

        private void RefreshRuntimeServicesCold()
        {
            CacheAudioService(GlobalRegistry.Audio);
            _tickDispatcher = GlobalRegistry.TickDispatcher;
        }

        private void RefreshQualityPolicy()
        {
            float quality01 = ResolveGlobalQualityWeight01();
            _qualityWeight = quality01;
            _qualityTierByte = ResolveQualityTierByte(quality01);
        }

        private static float ResolveGlobalQualityWeight01()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1f, quality, math.isfinite(quality)));
        }

        private static byte ResolveQualityTierByte(float quality)
        {
            float q = math.saturate(math.select(1f, quality, math.isfinite(quality)));
            return (byte)math.clamp((int)math.round(q * byte.MaxValue), 0, byte.MaxValue);
        }

        private float ResolveQualityCurve01()
        {
            return math.smoothstep(0f, 1f, math.saturate(_qualityWeight));
        }

        private float ResolveHeat01(in AtmosphericReentrySignal signal)
        {
            if ((signal.Flags & AtmosphericReentrySignal.FlagAuthoritativeHeat) != 0)
                return SaturateFiniteOrZero(signal.Heat01);

            float velocity01 = ResolveVelocity01(signal.UniverseVelocityMetersPerSecond);
            return math.saturate(math.max(signal.Heat01, velocity01));
        }

        private float ResolveVelocity01(float velocityMetersPerSecond)
        {
            float velocityScale = PositiveFiniteOrMinimum(plasmaFullStressVelocityMetersPerSecond, 1f);
            float safeVelocity = NonNegativeFiniteOrZero(velocityMetersPerSecond);
            return math.saturate(safeVelocity * math.rcp(velocityScale));
        }

        private static bool IsValidAtmosphericSignal(in AtmosphericReentrySignal signal)
        {
            return math.isfinite(signal.UniverseVelocityMetersPerSecond) &&
                   math.isfinite(signal.Heat01) &&
                   (signal.Phase == AtmosphericReentrySignal.PhaseApproach ||
                    signal.Phase == AtmosphericReentrySignal.PhasePlasma ||
                    signal.Phase == AtmosphericReentrySignal.PhaseWhiteout);
        }

        private static bool IsValidStressSignal(in ReentryAcousticStressSignal signal)
        {
            return math.isfinite(signal.Stress01) &&
                   math.isfinite(signal.Heat01) &&
                   math.isfinite(signal.UniverseVelocityMetersPerSecond) &&
                   math.isfinite(signal.LowPassCutoffHz) &&
                   math.isfinite(signal.LfeGain01) &&
                   math.isfinite(signal.GranularStress01) &&
                   signal.Phase >= ReentryAcousticStressSignal.PhaseSpace &&
                   signal.Phase <= ReentryAcousticStressSignal.PhaseSplashdown;
        }

        private static bool IsValidCompleteHold(float whiteoutHoldSeconds)
        {
            return math.isfinite(whiteoutHoldSeconds) && whiteoutHoldSeconds >= 0f;
        }

        private static bool IsWhiteoutOnlyComplete(in PrologueCompleteSignal signal)
        {
            return signal.SourceHash == OrbitalRelativitySourceHash &&
                   signal.Sequence != 0 &&
                   signal.Phase == PrologueCompleteSignal.PhaseWhiteout &&
                   (signal.Flags & PrologueCompleteSignal.FlagForceWhiteout) != 0;
        }

        private float ResolveLfeGain(float velocity01, bool plasmaStage, bool portalStage)
        {
            if (portalStage)
                return 0f;

            float baseGain = SaturateFiniteOrZero(vacuumLfeGain);
            if (!plasmaStage)
                return baseGain;

            float plasmaGain = SaturateFiniteOrZero(plasmaLfeGain);
            return math.saturate(math.lerp(baseGain, plasmaGain, velocity01));
        }

        private float ResolvePortalBlend01()
        {
            if (!_sweepActive)
                return 1f;

            float duration = PositiveFiniteOrMinimum(oceanFilterSweepSeconds, 0.001f);
            return math.saturate(_sweepElapsedSeconds * math.rcp(duration));
        }

        private bool ShouldPublishTransition(
            float lowPassCutoffHertz,
            float lfeGain,
            float granularStress,
            float splashdownGain01,
            float portalBlend01,
            byte flags)
        {
            return _forcePublishTransition ||
                   _splashdownPending ||
                   _stage != _lastPublishedStage ||
                   flags != _lastPublishedFlags ||
                   _qualityTierByte != _lastPublishedQualityTierByte ||
                   math.abs(lowPassCutoffHertz - _lastPublishedLowPassCutoffHertz) > CutoffPublishEpsilonHertz ||
                   math.abs(lfeGain - _lastPublishedLfeGain) > GainPublishEpsilon ||
                   math.abs(granularStress - _lastPublishedGranularStress) > GainPublishEpsilon ||
                   math.abs(splashdownGain01 - _lastPublishedSplashdownGain) > GainPublishEpsilon ||
                   math.abs(portalBlend01 - _lastPublishedPortalBlend) > GainPublishEpsilon;
        }

        private float ClampCutoff(float cutoffHertz)
        {
            float resolvedCutoff = math.isfinite(cutoffHertz) ? cutoffHertz : vacuumLowPassCutoffHertz;
            if (!math.isfinite(resolvedCutoff))
                resolvedCutoff = MinimumLowPassCutoffHertz;

            return math.clamp(
                resolvedCutoff,
                MinimumLowPassCutoffHertz,
                22000f);
        }

        private float ResolveUnscaledDeltaTime()
        {
            ITickDispatcher dispatcher = _tickDispatcher;
            if (dispatcher != null)
            {
                double dispatcherDelta = dispatcher.TimeSnapshot.UnscaledDeltaTime;
                if (dispatcherDelta > 0d && double.IsFinite(dispatcherDelta))
                    return dispatcherDelta > MaxPresentationDeltaSeconds ? MaxPresentationDeltaSeconds : (float)dispatcherDelta;
            }

            float fallback = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            return math.isfinite(fallback) && fallback > 0f ? math.min(fallback, MaxPresentationDeltaSeconds) : 0f;
        }

        private double ResolveAbsoluteTimeSeconds()
        {
            ITickDispatcher dispatcher = _tickDispatcher;
            if (dispatcher != null)
            {
                double dispatcherTime = dispatcher.TimeSnapshot.UnscaledTime;
                if (dispatcherTime >= 0d && double.IsFinite(dispatcherTime))
                    return dispatcherTime;
            }

            double fallback = SystemDispatcher.CurrentUnscaledTimeSeconds;
            if (fallback >= 0d && double.IsFinite(fallback))
                return fallback;

            return 0d;
        }

        private static float PositiveFiniteOrMinimum(float value, float minimum)
        {
            return math.isfinite(value) && value > minimum ? value : minimum;
        }

        private static float SaturateFiniteOrZero(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static float NonNegativeFiniteOrZero(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        private void OnValidate()
        {
            vacuumLowPassCutoffHertz = ClampCutoff(vacuumLowPassCutoffHertz);
            oceanLowPassCutoffHertz = ClampCutoff(oceanLowPassCutoffHertz);
            oceanFilterSweepSeconds = PositiveFiniteOrMinimum(oceanFilterSweepSeconds, 0.001f);
            plasmaFullStressVelocityMetersPerSecond = PositiveFiniteOrMinimum(plasmaFullStressVelocityMetersPerSecond, 1f);
            plasmaGranularStressGain = SaturateFiniteOrZero(plasmaGranularStressGain);
            vacuumLfeGain = SaturateFiniteOrZero(vacuumLfeGain);
            plasmaLfeGain = SaturateFiniteOrZero(plasmaLfeGain);
            splashdownGain = SaturateFiniteOrZero(splashdownGain);
        }
    }
}
