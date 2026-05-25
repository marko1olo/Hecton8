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
        private const float MinimumLowPassCutoffHertz = 80f;
        private const float CutoffPublishEpsilonHertz = 1f;
        private const float GainPublishEpsilon = 0.0005f;
        private const float MaxPresentationDeltaSeconds = 0.25f;

        [Header("Filter")]
        [SerializeField] private float vacuumLowPassCutoffHertz = 400f;
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
        private byte _qualityTierByte;
        private int _lastLateFrame = -1;
        private int _lastAtmosphericFrame = -1;
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
        private float _currentLowPassCutoffHertz = 400f;
        private float _lastPublishedLowPassCutoffHertz = -1f;
        private float _lastPublishedLfeGain = -1f;
        private float _lastPublishedGranularStress = -1f;
        private float _lastPublishedSplashdownGain = -1f;
        private float _lastPublishedPortalBlend = -1f;
        private float _sweepElapsedSeconds;
        private bool _sweepActive;
        private bool _splashdownPending;
        private bool _prologueArmed;
        private bool _hasCompleteSequence;
        private bool _hasWhiteoutCompleteSequence;
        private bool _forcePublishTransition;
        private uint _tickCount;

        /// <inheritdoc />
        public int TickCount => unchecked((int)_tickCount);

        private void OnEnable()
        {
            RefreshRuntimeServicesCold();
            RefreshQualityPolicyCold();
            ResetTransientState();

            if (!_lateFrameRegistered)
            {
                _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            }

            if (!_hotSwapRegistered)
            {
                _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
            }

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
            _currentLowPassCutoffHertz = ClampCutoff(oceanLowPassCutoffHertz);
            _lastPublishedLowPassCutoffHertz = -1f;
            _lastPublishedLfeGain = -1f;
            _lastPublishedGranularStress = -1f;
            _lastPublishedSplashdownGain = -1f;
            _lastPublishedPortalBlend = -1f;
            _sweepElapsedSeconds = 0f;
            _sweepActive = false;
            _splashdownPending = false;
            _prologueArmed = false;
            _hasCompleteSequence = false;
            _hasWhiteoutCompleteSequence = false;
            _forcePublishTransition = false;
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastLateFrame == frame)
                return;

            _lastLateFrame = frame;
            _tickCount++;
            RefreshQualityPolicyCold();
            ConsumeAtmosphericSignals();
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
                    _tickDispatcher = currentService as ITickDispatcher;
                    break;
            }
        }

        private void ConsumeAtmosphericSignals()
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastAtmosphericFrame == frame)
                return;

            _lastAtmosphericFrame = frame;
            ReadOnlySpan<AtmosphericReentrySignal> signals = SignalBus<AtmosphericReentrySignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                AtmosphericReentrySignal signal = signals[i];
                if (!IsValidAtmosphericSignal(in signal))
                    continue;

                _prologueArmed = true;
                _velocityMetersPerSecond = NonNegativeFiniteOrZero(signal.UniverseVelocityMetersPerSecond);
                _heat01 = ResolveHeat01(in signal);

                if (_stage == AudioTransitionState.StageOceanHandoff)
                    continue;

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

                bool newCompleteSequence = !_hasCompleteSequence || signal.Sequence != _lastCompleteSequence;
                _prologueArmed = true;
                _hasCompleteSequence = true;

                if (newCompleteSequence)
                {
                    _lastCompleteSequence = signal.Sequence;
                    _splashdownPending = true;
                    _sweepElapsedSeconds = 0f;
                    _sweepActive = true;
                    _currentLowPassCutoffHertz = ClampCutoff(vacuumLowPassCutoffHertz);
                    _forcePublishTransition = true;
                }

                _stage = AudioTransitionState.StageOceanHandoff;
            }
        }

        private void AdvanceFilterSweep(float deltaSeconds)
        {
            if (!_sweepActive)
                return;

            float duration = PositiveFiniteOrMinimum(oceanFilterSweepSeconds, 0.001f);
            _sweepElapsedSeconds = math.min(duration, _sweepElapsedSeconds + math.max(0f, deltaSeconds));
            float t = math.saturate(_sweepElapsedSeconds * math.rcp(duration));
            t = t * t * (3f - 2f * t);
            _currentLowPassCutoffHertz = math.lerp(ClampCutoff(vacuumLowPassCutoffHertz), ClampCutoff(oceanLowPassCutoffHertz), t);
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
                                  !math.isfinite(_currentLowPassCutoffHertz);
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
            float granularGain = SaturateFiniteOrZero(plasmaGranularStressGain);
            float granularStress = plasmaStage ? math.saturate(velocity01 * granularGain) * qualityCurve : 0f;
            if (granularStress > GainPublishEpsilon)
                flags |= AudioTransitionState.FlagGranularEnabled;
            float splashdownGain01 = _splashdownPending ? SaturateFiniteOrZero(splashdownGain) : 0f;
            float portalBlend01 = portalStage ? ResolvePortalBlend01() : 0f;
            if (!ShouldPublishTransition(lowPassCutoffHertz, lfeGain, granularStress, splashdownGain01, portalBlend01, flags))
                return;

            var state = new AudioTransitionState
            {
                UniverseVelocityMetersPerSecond = velocityMetersPerSecond,
                Heat01 = heat01,
                LowPassCutoffHz = lowPassCutoffHertz,
                LfeGain01 = lfeGain,
                GranularStress01 = granularStress,
                SplashdownGain01 = splashdownGain01,
                PortalBlend01 = portalBlend01,
                Frame = unchecked((uint)frame),
                Sequence = ++_transitionSequence,
                SourceHash = SourceHash,
                Stage = _stage,
                Flags = flags,
                QualityTier = _qualityTierByte,
                AbsoluteTimeSeconds = ResolveAbsoluteTimeSeconds()
            };

            if (!audioService.QueuePrologueAudioTransition(in state))
                return;

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

        private void PublishNeutralTransitionOnDisable()
        {
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

            var state = new AudioTransitionState
            {
                UniverseVelocityMetersPerSecond = 0f,
                Heat01 = 0f,
                LowPassCutoffHz = ClampCutoff(oceanLowPassCutoffHertz),
                LfeGain01 = 0f,
                GranularStress01 = 0f,
                SplashdownGain01 = 0f,
                PortalBlend01 = 0f,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                Sequence = ++_transitionSequence,
                SourceHash = SourceHash,
                Stage = AudioTransitionState.StageSpace,
                Flags = 0,
                QualityTier = _qualityTierByte,
                AbsoluteTimeSeconds = ResolveAbsoluteTimeSeconds()
            };

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

        private void RefreshQualityPolicyCold()
        {
            _qualityTierByte = ResolveQualityTierByte(ResolveGlobalQualityWeight01());
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
            float q = _qualityTierByte * math.rcp(byte.MaxValue);
            q = math.saturate(q);
            return q * q * (3f - 2f * q);
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

        private static bool IsValidCompleteHold(float whiteoutHoldSeconds)
        {
            return math.isfinite(whiteoutHoldSeconds) && whiteoutHoldSeconds >= 0f;
        }

        private static bool IsWhiteoutOnlyComplete(in PrologueCompleteSignal signal)
        {
            if (signal.Phase == PrologueCompleteSignal.PhaseWhiteout)
                return true;

            return signal.Phase == PrologueCompleteSignal.PhaseOceanHandoff &&
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
