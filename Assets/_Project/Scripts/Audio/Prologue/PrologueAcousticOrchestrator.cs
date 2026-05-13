using System;
using Hecton8.Core;
using Hecton8.Core.Signals;
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
        private const float MinimumLowPassCutoffHertz = 80f;

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
        private bool _lateFrameRegistered;
        private bool _hotSwapRegistered;
        private bool _lowTier;
        private byte _qualityTierByte;
        private int _qualityRefreshFrame = -1024;
        private int _lastLateFrame = -1;
        private int _lastAtmosphericFrame = -1;
        private int _lastCompleteFrame = -1;
        private uint _transitionSequence;
        private ushort _lastAtmosphericSequence;
        private ushort _lastCompleteSequence;
        private byte _stage = AudioTransitionState.StageSpace;
        private float _velocityMetersPerSecond;
        private float _heat01;
        private float _currentLowPassCutoffHertz = 400f;
        private float _sweepElapsedSeconds;
        private bool _sweepActive;
        private bool _splashdownPending;
        private uint _tickCount;

        /// <inheritdoc />
        public int TickCount => unchecked((int)_tickCount);

        private void OnEnable()
        {
            CacheAudioService(GlobalRegistry.Audio);
            RefreshQualityTier(true);
            _currentLowPassCutoffHertz = ClampCutoff(vacuumLowPassCutoffHertz);
            _stage = AudioTransitionState.StageSpace;
            _sweepActive = false;
            _splashdownPending = false;

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

            _audioService = null;
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            int frame = Time.frameCount;
            if (_lastLateFrame == frame)
                return;

            _lastLateFrame = frame;
            _tickCount++;
            RefreshQualityTier(false);
            ConsumeAtmosphericSignals();
            ConsumePrologueCompleteSignals();
            AdvanceFilterSweep(Time.unscaledDeltaTime);
            PublishAudioTransition(frame);
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceRebound(
            GlobalRegistryServiceSlot serviceSlot,
            ref object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Audio)
                CacheAudioService(currentService as IAudioService);
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Audio)
                CacheAudioService(currentService as IAudioService);
        }

        private void ConsumeAtmosphericSignals()
        {
            int frame = Time.frameCount;
            if (_lastAtmosphericFrame == frame)
                return;

            _lastAtmosphericFrame = frame;
            ReadOnlySpan<AtmosphericReentrySignal> signals = SignalBus<AtmosphericReentrySignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                AtmosphericReentrySignal signal = signals[i];
                if (!math.isfinite(signal.UniverseVelocityMetersPerSecond) || !math.isfinite(signal.Heat01))
                    continue;

                _lastAtmosphericSequence = signal.Sequence;
                _velocityMetersPerSecond = math.max(0f, signal.UniverseVelocityMetersPerSecond);
                _heat01 = ResolveHeat01(in signal);

                if (signal.Phase >= AtmosphericReentrySignal.PhaseWhiteout ||
                    (signal.Flags & AtmosphericReentrySignal.FlagWhiteoutRequested) != 0)
                {
                    _stage = AudioTransitionState.StageWhiteout;
                    _currentLowPassCutoffHertz = ClampCutoff(vacuumLowPassCutoffHertz);
                }
                else if (signal.Phase >= AtmosphericReentrySignal.PhasePlasma)
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
            int frame = Time.frameCount;
            if (_lastCompleteFrame == frame)
                return;

            _lastCompleteFrame = frame;
            ReadOnlySpan<PrologueCompleteSignal> signals = SignalBus<PrologueCompleteSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PrologueCompleteSignal signal = signals[i];
                if (!math.isfinite(signal.WhiteoutHoldSeconds))
                    continue;

                if (signal.Sequence != _lastCompleteSequence)
                {
                    _lastCompleteSequence = signal.Sequence;
                    _splashdownPending = true;
                }

                _stage = AudioTransitionState.StageOceanHandoff;
                _sweepElapsedSeconds = 0f;
                _sweepActive = true;
                _currentLowPassCutoffHertz = ClampCutoff(vacuumLowPassCutoffHertz);
            }
        }

        private void AdvanceFilterSweep(float deltaSeconds)
        {
            if (!_sweepActive)
                return;

            float duration = math.max(0.001f, oceanFilterSweepSeconds);
            _sweepElapsedSeconds = math.min(duration, _sweepElapsedSeconds + math.max(0f, deltaSeconds));
            float t = math.saturate(_sweepElapsedSeconds / duration);
            t = t * t * (3f - 2f * t);
            _currentLowPassCutoffHertz = math.lerp(ClampCutoff(vacuumLowPassCutoffHertz), ClampCutoff(oceanLowPassCutoffHertz), t);
            if (_sweepElapsedSeconds >= duration)
                _sweepActive = false;
        }

        private void PublishAudioTransition(int frame)
        {
            IAudioService audioService = _audioService;
            if (audioService == null || !audioService.IsInitialized)
                return;

            float velocity01 = ResolveVelocity01(_velocityMetersPerSecond);
            float heat01 = math.saturate(_heat01);
            bool plasmaStage = _stage == AudioTransitionState.StagePlasma || _stage == AudioTransitionState.StageWhiteout;
            bool portalStage = _stage == AudioTransitionState.StageOceanHandoff;
            bool granularEnabled = plasmaStage && !_lowTier;

            byte flags = 0;
            if (_splashdownPending)
                flags |= AudioTransitionState.FlagSplashdown;
            if (portalStage)
                flags |= AudioTransitionState.FlagPortalActive;
            if (granularEnabled)
                flags |= AudioTransitionState.FlagGranularEnabled;
            if (plasmaStage && _lowTier)
                flags |= AudioTransitionState.FlagLowTierProxy;

            var state = new AudioTransitionState
            {
                UniverseVelocityMetersPerSecond = _velocityMetersPerSecond,
                Heat01 = heat01,
                LowPassCutoffHz = ClampCutoff(_currentLowPassCutoffHertz),
                LfeGain01 = ResolveLfeGain(velocity01, plasmaStage, portalStage),
                GranularStress01 = granularEnabled ? math.saturate(velocity01 * plasmaGranularStressGain) : 0f,
                SplashdownGain01 = _splashdownPending ? math.saturate(splashdownGain) : 0f,
                PortalBlend01 = portalStage ? ResolvePortalBlend01() : 0f,
                Frame = unchecked((uint)frame),
                Sequence = ++_transitionSequence,
                SourceHash = SourceHash,
                Stage = _stage,
                Flags = flags,
                QualityTier = _qualityTierByte,
                AbsoluteTimeSeconds = Time.unscaledTimeAsDouble
            };

            audioService.QueuePrologueAudioTransition(in state);
            _splashdownPending = false;
        }

        private void CacheAudioService(IAudioService audioService)
        {
            _audioService = audioService;
        }

        private void RefreshQualityTier(bool force)
        {
            int frame = Time.frameCount;
            if (!force && frame - _qualityRefreshFrame < 60)
                return;

            _qualityRefreshFrame = frame;
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            _qualityTierByte = GlobalRegistry.ScalabilityTierProfileByte;
            _lowTier = tier == HectonQualityTier.Unknown ||
                       tier == HectonQualityTier.Low ||
                       tier == HectonQualityTier.Mx350 ||
                       GlobalRegistry.H8_LOW_MEMORY_PROFILE;
        }

        private float ResolveHeat01(in AtmosphericReentrySignal signal)
        {
            if ((signal.Flags & AtmosphericReentrySignal.FlagAuthoritativeHeat) != 0)
                return math.saturate(signal.Heat01);

            float velocity01 = ResolveVelocity01(signal.UniverseVelocityMetersPerSecond);
            return math.saturate(math.max(signal.Heat01, velocity01));
        }

        private float ResolveVelocity01(float velocityMetersPerSecond)
        {
            float velocityScale = math.max(1f, plasmaFullStressVelocityMetersPerSecond);
            return math.saturate(math.max(0f, velocityMetersPerSecond) / velocityScale);
        }

        private float ResolveLfeGain(float velocity01, bool plasmaStage, bool portalStage)
        {
            if (portalStage)
                return 0f;

            float baseGain = math.saturate(vacuumLfeGain);
            if (!plasmaStage)
                return baseGain;

            float plasmaGain = math.saturate(plasmaLfeGain);
            return math.saturate(math.lerp(baseGain, plasmaGain, velocity01));
        }

        private float ResolvePortalBlend01()
        {
            if (!_sweepActive)
                return 1f;

            float duration = math.max(0.001f, oceanFilterSweepSeconds);
            return math.saturate(_sweepElapsedSeconds / duration);
        }

        private float ClampCutoff(float cutoffHertz)
        {
            return math.clamp(
                math.isfinite(cutoffHertz) ? cutoffHertz : vacuumLowPassCutoffHertz,
                MinimumLowPassCutoffHertz,
                22000f);
        }

        private void OnValidate()
        {
            vacuumLowPassCutoffHertz = math.clamp(vacuumLowPassCutoffHertz, MinimumLowPassCutoffHertz, 22000f);
            oceanLowPassCutoffHertz = math.clamp(oceanLowPassCutoffHertz, MinimumLowPassCutoffHertz, 22000f);
            oceanFilterSweepSeconds = math.max(0.001f, oceanFilterSweepSeconds);
            plasmaFullStressVelocityMetersPerSecond = math.max(1f, plasmaFullStressVelocityMetersPerSecond);
            plasmaGranularStressGain = math.saturate(plasmaGranularStressGain);
            vacuumLfeGain = math.saturate(vacuumLfeGain);
            plasmaLfeGain = math.saturate(plasmaLfeGain);
            splashdownGain = math.saturate(splashdownGain);
        }
    }
}
