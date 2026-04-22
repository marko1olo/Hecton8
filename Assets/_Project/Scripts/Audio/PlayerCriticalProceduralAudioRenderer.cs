using System;
using System.Threading;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Visor;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Audio
{
    /// <summary>
    /// Player-owned procedural DSP renderer for critical helmet/audio-thread synthesis.
    /// </summary>
    /// <remarks>
    /// Ownership is intentionally centralized here so hull stress, active sonar, and
    /// transport thrust all share one audio-thread renderer and one sample-accurate
    /// synchronization bridge.
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioListener))]
    public sealed class PlayerCriticalProceduralAudioRenderer : MonoBehaviour, ITickable, ISlowTickable
    {
        private const float TwoPi = 6.28318530718f;
        private const float HullNoiseFloor = 0.0001f;
        private const float SonarChirpDurationSeconds = 0.5f;
        private const float SonarTailDurationSeconds = 3.8f;
        private const float SonarTotalDurationSeconds = 4.0f;
        private const float SoundSpeedWaterMetersPerSecond = 1480f;
        private const float SonarEchoReferenceDistanceMeters = 24f;
        private const float SonarEchoMaximumDistanceMeters = 1800f;
        private const float SonarEchoMaximumDelaySeconds = 2.2f;
        private const float AbyssalLowPassStartDepthMeters = 4000f;
        private const float AbyssalLowPassFadeDepthMeters = 800f;
        private const float AbyssalLowPassCutoffHertz = 2000f;
        private const int MaxSafeFrameCapacity = 16384;
        private const int MaxFilterChannels = 8;

        private static PlayerCriticalProceduralAudioRenderer s_activeInstance;

        [Header("References")]
        [Tooltip("Resolved live player movement owner. Bound automatically by the runtime installer.")]
        [SerializeField] private HectonPlayerMovement playerMovement;

        [Tooltip("Resolved player tool manager used for transport-state queries.")]
        [SerializeField] private PlayerToolManager playerToolManager;

        [Tooltip("Resolved transport coordinator used when transport ownership is externalized.")]
        [SerializeField] private PlayerTransportCoordinator playerTransportCoordinator;

        [Header("Helmet Mix")]
        [Tooltip("Master gain for the hull-stress synth layer.")]
        [SerializeField, Range(0f, 1f)] private float hullMasterGain = 0.38f;

        [Tooltip("Master gain for the active sonar ping.")]
        [SerializeField, Range(0f, 1f)] private float sonarMasterGain = 0.85f;

        [Tooltip("Master gain for the thruster / cavitation layer.")]
        [SerializeField, Range(0f, 1f)] private float thrusterMasterGain = 0.42f;

        [Tooltip("Global procedural headroom before the signal is mixed into the listener bus.")]
        [SerializeField, Range(0f, 1f)] private float outputHeadroom = 0.72f;

        [Header("Hull Stress")]
        [Tooltip("How quickly the main-thread hull-stress target chases locomotion truth.")]
        [SerializeField, Range(1f, 30f)] private float hullStressFollowSharpness = 8f;

        [Tooltip("How much sub-pressure from the Deepseek reference is folded into the hull groan bed.")]
        [SerializeField, Range(0f, 1f)] private float hullPressureBedAmount = 0.24f;

        [Tooltip("How much rivet-pop energy is injected at maximum hull stress.")]
        [SerializeField, Range(0f, 1f)] private float hullRivetBurstAmount = 0.36f;

        [Header("Sonar Ping")]
        [Tooltip("How much of the piezo attack from the reference implementation is kept in front of the chirp.")]
        [SerializeField, Range(0f, 1f)] private float sonarAttackBlend = 0.46f;

        [Tooltip("How strong the abyssal tail stays relative to the main chirp.")]
        [SerializeField, Range(0f, 1f)] private float sonarTailBlend = 0.72f;

        [Tooltip("Drive amount for the sonar tanh saturation stage.")]
        [SerializeField, Range(0.5f, 4f)] private float sonarSaturationDrive = 1.8f;

        [Header("Thruster")]
        [Tooltip("How strongly surface locomotion is retained in the procedural thruster mix.")]
        [SerializeField, Range(0f, 1f)] private float surfaceSwimModeBlend = 0.58f;

        [Tooltip("Volume multiplier applied while surface swimming.")]
        [SerializeField, Range(0.1f, 1f)] private float surfaceSwimVolumeMultiplier = 0.72f;

        [Tooltip("Pitch-energy multiplier applied while surface swimming.")]
        [SerializeField, Range(0.1f, 1f)] private float surfaceSwimPitchMultiplier = 0.9f;

        [Tooltip("Depth below the surface where cavitation pressure starts fading back toward clean thrust.")]
        [SerializeField, Range(0.1f, 3f)] private float cavitationFadeStartDepth = 0.9f;

        [Tooltip("Depth below the surface where cavitation pressure fully relaxes.")]
        [SerializeField, Range(0.2f, 4f)] private float cavitationFadeEndDepth = 1.8f;

        [Tooltip("Velocity delta treated as full throttle-attack intensity for cavitation boil-up.")]
        [SerializeField, Range(0.1f, 20f)] private float throttleAttackVelocityDelta = 3.6f;

        [Tooltip("How quickly thruster mix targets converge on live locomotion/transport state.")]
        [SerializeField, Range(1f, 30f)] private float thrusterFollowSharpness = 10f;

        [Tooltip("How much heavy cargo drags the synthetic transport pitch downward.")]
        [SerializeField, Range(0f, 0.5f)] private float heavyCarryPitchDrag = 0.14f;

        [Tooltip("How much heavy cargo boosts transport grind and cavitation energy.")]
        [SerializeField, Range(0f, 0.5f)] private float heavyCarryVolumeBoost = 0.12f;

        [Header("Audio Worklet")]
        [Tooltip("How many mono frames the async producer generates per Burst block.")]
        [SerializeField, Range(256, 4096)] private int synthesisBlockFrames = 1024;

        [Tooltip("Total mono-frame capacity of the lock-free ring buffer. Power-of-two rounded at runtime.")]
        [SerializeField, Range(2048, 262144)] private int ringBufferCapacityFrames = 65536;

        [Tooltip("How far ahead of the audio consumer the producer tries to stay buffered.")]
        [SerializeField, Range(1024, 131072)] private int workerTargetLeadFrames = 16384;

        [Header("Spatial Reverb")]
        [Tooltip("Layers considered valid enclosure geometry for the orthogonal reverb probes.")]
        [SerializeField] private LayerMask ceilingProbeLayers = ~0;

        [Tooltip("Maximum orthogonal probe distance used to classify open water vs. local enclosure coverage.")]
        [SerializeField, Range(5f, 80f)] private float ceilingProbeDistance = 48f;

        [Tooltip("Ceiling distance at or below which cave acoustics are considered fully engaged.")]
        [SerializeField, Range(1f, 20f)] private float caveCeilingThreshold = 10f;

        [Tooltip("How quickly cave/open-water reverb settings chase probe results.")]
        [SerializeField, Range(1f, 20f)] private float caveReverbFollowSharpness = 6f;

        [Tooltip("Decay time used when no cave ceiling is found and the player is in open water.")]
        [SerializeField, Range(0.2f, 20f)] private float openWaterDecayTime = 12f;

        [Tooltip("Decay time used when the player is under a close cave ceiling.")]
        [SerializeField, Range(0.1f, 10f)] private float caveDecayTime = 1.6f;

        [Tooltip("Early reflection level for the open-water reverb profile.")]
        [SerializeField, Range(-10000f, 1000f)] private float openWaterReflectionsLevel = -2200f;

        [Tooltip("Early reflection level for the cave reverb profile.")]
        [SerializeField, Range(-10000f, 1000f)] private float caveReflectionsLevel = 120f;

        [Tooltip("High-frequency room attenuation in open water so the tail stays dull.")]
        [SerializeField, Range(-10000f, 0f)] private float openWaterRoomHighFrequency = -4200f;

        [Tooltip("High-frequency room attenuation under a close cave ceiling.")]
        [SerializeField, Range(-10000f, 0f)] private float caveRoomHighFrequency = -1400f;

        private NativeArray<float> _hullScratch;
        private NativeArray<float> _sonarScratch;
        private NativeArray<float> _thrusterScratch;
        private NativeArray<float> _mixScratch;
        private NativeAudioFrameRingBuffer _sampleRingBuffer;
        private Thread _audioProducerThread;
        private int _frameCapacity;
        private int _sampleRate;
        private bool _buffersInitialized;
        private bool _registered;
        private bool _slowTickRegistered;
        private GameObject _boundPlayerObject;
        private Transform _boundPlayerTransform;
        private Rigidbody _playerRigidbody;
        private AudioReverbFilter _listenerReverbFilter;
        private PlayerTransportFeelContract _transportFeelContractCurrent;
        private float _lastSpeed;
        private float _hullStressTickValue;
        private float _thrusterBlendTickValue;
        private float _thrusterLoadTickValue;
        private float _thrusterPitchTickValue = 1f;
        private float _thrusterPressureTickValue;
        private float _thrusterAccelerationTickValue;
        private float _thrusterHeavyCarryTickValue;
        private float _thrusterDiveTickValue;
        private float _audioHullStressValue;
        private float _audioThrusterBlendValue;
        private float _audioThrusterLoadValue;
        private float _audioThrusterPitchValue = 1f;
        private float _audioThrusterPressureValue;
        private float _audioThrusterAccelerationValue;
        private float _audioThrusterHeavyCarryValue;
        private float _audioThrusterDiveValue;
        private int _pendingSonarIntensityBits;
        private int _pendingSonarEchoDelayBits;
        private int _pendingSonarEchoDopplerBits;
        private int _pendingSonarEchoAttenuationBits;
        private long _pendingSonarStartFrame = long.MinValue;
        private float _audioAbyssalLowPassMix;
        private float _caveReverbBlend;
        private float _smoothedEnclosureVolume;
        private float _targetEnclosureVolume;
        private int _probeAxisIndex;
        private long _workerActiveSonarStartFrame = long.MinValue;
        private float _workerActiveSonarIntensity;
        private float _workerActiveSonarEchoDelaySeconds;
        private float _workerActiveSonarEchoDopplerRatio = 1f;
        private float _workerActiveSonarEchoAttenuation = 1f;
        private int _audioProducerRunning;
        private bool _listenerReverbDefaultsCaptured;
        private bool _listenerReverbWasEnabled;
        private AudioReverbPreset _listenerReverbBasePreset = AudioReverbPreset.Off;
        private float _listenerReverbBaseDecayTime = 1f;
        private float _listenerReverbBaseReflectionsLevel = -10000f;
        private float _listenerReverbBaseRoomHighFrequency = -10000f;

        private volatile float _targetHullStressValue;
        private volatile float _targetThrusterBlendValue;
        private volatile float _targetThrusterLoadValue;
        private volatile float _targetThrusterPitchValue = 1f;
        private volatile float _targetThrusterPressureValue;
        private volatile float _targetThrusterAccelerationValue;
        private volatile float _targetThrusterHeavyCarryValue;
        private volatile float _targetThrusterDiveValue;
        private volatile float _targetAbyssalLowPassMix;
        private volatile float _targetCaveReverbBlend;

        // COLD ALLOC: float[8] - final listener low-pass state x1 - owner: PlayerCriticalProceduralAudioRenderer
        private readonly float[] _lowPassInputHistory1 = new float[MaxFilterChannels];
        // COLD ALLOC: float[8] - final listener low-pass state x2 - owner: PlayerCriticalProceduralAudioRenderer
        private readonly float[] _lowPassInputHistory2 = new float[MaxFilterChannels];
        // COLD ALLOC: float[8] - final listener low-pass state y1 - owner: PlayerCriticalProceduralAudioRenderer
        private readonly float[] _lowPassOutputHistory1 = new float[MaxFilterChannels];
        // COLD ALLOC: float[8] - final listener low-pass state y2 - owner: PlayerCriticalProceduralAudioRenderer
        private readonly float[] _lowPassOutputHistory2 = new float[MaxFilterChannels];
        // COLD ALLOC: float[6] - orthogonal enclosure probe distances - owner: PlayerCriticalProceduralAudioRenderer
        private readonly float[] _orthogonalProbeDistances = new float[6];
        // COLD ALLOC: RaycastHit[8] - orthogonal enclosure probe hits - owner: PlayerCriticalProceduralAudioRenderer
        private readonly RaycastHit[] _ceilingProbeHits = new RaycastHit[8];

        /// <summary>
        /// True while the player-owned procedural critical-audio renderer is active.
        /// </summary>
        public static bool IsRuntimeInstalled => s_activeInstance != null;

        private void Awake()
        {
            if (s_activeInstance != null && s_activeInstance != this)
            {
                Destroy(this);
                return;
            }

            s_activeInstance = this;
            ResetEnclosureProbeState(math.max(1f, ceilingProbeDistance));
            RefreshAudioConfiguration();
            TryBindFromBootstrap();
        }

        private void OnEnable()
        {
            AudioSettings.OnAudioConfigurationChanged += HandleAudioConfigurationChanged;
            SpectrumEvents.OnSonarPingSent += HandleSonarPingSent;
            TryRegister();
            TryBindFromBootstrap();
            StartAudioProducerThread();
        }

        private void OnDisable()
        {
            SpectrumEvents.OnSonarPingSent -= HandleSonarPingSent;
            AudioSettings.OnAudioConfigurationChanged -= HandleAudioConfigurationChanged;
            TryUnregister();
            StopAudioProducerThread();
            RestoreListenerReverbDefaults();
            DisposeBuffers();
            ClearLowPassState();
        }

        private void OnDestroy()
        {
            if (s_activeInstance == this)
                s_activeInstance = null;
        }

        /// <summary>
        /// Binds the renderer to the live player object resolved by bootstrap.
        /// </summary>
        /// <param name="playerObject">Live player root.</param>
        internal void BindToPlayer(GameObject playerObject)
        {
            _boundPlayerObject = playerObject;
            _boundPlayerTransform = playerObject != null ? playerObject.transform : null;
            if (playerObject == null)
                return;

            if (playerMovement == null || !ReferenceEquals(playerMovement.gameObject, playerObject))
                playerObject.TryGetComponent(out playerMovement);

            if (playerToolManager == null || !ReferenceEquals(playerToolManager.gameObject, playerObject))
                playerObject.TryGetComponent(out playerToolManager);

            if (playerTransportCoordinator == null || !ReferenceEquals(playerTransportCoordinator.gameObject, playerObject))
                playerObject.TryGetComponent(out playerTransportCoordinator);

            if (_playerRigidbody == null || !ReferenceEquals(_playerRigidbody.gameObject, playerObject))
                playerObject.TryGetComponent(out _playerRigidbody);

            ResolveListenerReverbFilter();
        }

        /// <summary>
        /// Main-thread state sampling for the audio renderer.
        /// </summary>
        /// <param name="deltaTime">Render-step delta time from the tick manager.</param>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            TryBindFromBootstrap();
            UpdateCaveReverb(deltaTime);

            if (playerMovement == null || _playerRigidbody == null)
            {
                _targetHullStressValue = 0f;
                _targetThrusterBlendValue = 0f;
                _targetThrusterLoadValue = 0f;
                _targetThrusterPitchValue = 1f;
                _targetThrusterPressureValue = 0f;
                _targetThrusterAccelerationValue = 0f;
                _targetThrusterHeavyCarryValue = 0f;
                _targetThrusterDiveValue = 0f;
                _targetAbyssalLowPassMix = 0f;
                _targetCaveReverbBlend = 0f;
                _targetEnclosureVolume = math.max(1f, ceilingProbeDistance);
                _lastSpeed = 0f;
                return;
            }

            float hullBlendT = 1f - math.exp(-math.max(hullStressFollowSharpness, 0.01f) * deltaTime);
            _hullStressTickValue = math.lerp(
                _hullStressTickValue,
                math.saturate(playerMovement.CurrentHullStress01),
                hullBlendT);
            _targetHullStressValue = _hullStressTickValue;
            _targetAbyssalLowPassMix = ResolveAbyssalLowPassTarget(playerMovement.CurrentDepth);

            UpdateThrusterTargets(deltaTime);
        }

        /// <summary>
        /// Slow orthogonal enclosure probing for cave-aware listener reverb.
        /// </summary>
        public void SlowTick()
        {
            TryBindFromBootstrap();

            float defaultDistance = math.max(1f, ceilingProbeDistance);
            if (_boundPlayerTransform == null || playerMovement == null || !playerMovement.IsPlayerSubmerged)
            {
                ResetEnclosureProbeState(defaultDistance);
                return;
            }

            Vector3 probeDirection = ResolveProbeDirection(_probeAxisIndex);
            float sampledDistance = ResolveProbeDistanceMeters(probeDirection, defaultDistance);
            _orthogonalProbeDistances[_probeAxisIndex] = sampledDistance;
            _probeAxisIndex = (_probeAxisIndex + 1) % _orthogonalProbeDistances.Length;

            float distanceSum = 0f;
            for (int i = 0; i < _orthogonalProbeDistances.Length; i++)
                distanceSum += _orthogonalProbeDistances[i];

            _targetEnclosureVolume = distanceSum / _orthogonalProbeDistances.Length;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!_buffersInitialized ||
                _sampleRingBuffer == null ||
                !_sampleRingBuffer.IsCreated ||
                channels <= 0 ||
                data == null ||
                data.Length == 0)
                return;

            int frameCount = data.Length / channels;
            if (frameCount <= 0)
                return;

            _sampleRingBuffer.AddToInterleaved(data, channels, frameCount);
        }

        private void StartAudioProducerThread()
        {
            if (Interlocked.CompareExchange(ref _audioProducerRunning, 1, 0) != 0)
                return;

            _audioProducerThread = new Thread(AudioProducerLoop)
            {
                IsBackground = true,
                Name = "Hecton8ProceduralAudioProducer",
                Priority = System.Threading.ThreadPriority.AboveNormal
            };
            _audioProducerThread.Start();
        }

        private void StopAudioProducerThread()
        {
            if (Interlocked.Exchange(ref _audioProducerRunning, 0) == 0)
                return;

            Thread producerThread = _audioProducerThread;
            if (producerThread != null)
            {
                producerThread.Join(250);
                _audioProducerThread = null;
            }
        }

        private void AudioProducerLoop()
        {
            while (Volatile.Read(ref _audioProducerRunning) != 0)
            {
                if (!_buffersInitialized || _sampleRingBuffer == null || !_sampleRingBuffer.IsCreated)
                {
                    Thread.Sleep(1);
                    continue;
                }

                int blockFrames = math.clamp(synthesisBlockFrames, 256, _frameCapacity);
                int targetLeadFrames = math.clamp(workerTargetLeadFrames, blockFrames, math.max(blockFrames, _sampleRingBuffer.CapacityFrames - blockFrames));
                if (_sampleRingBuffer.BufferedFrames >= targetLeadFrames || _sampleRingBuffer.WritableFrames < blockFrames)
                {
                    Thread.Sleep(1);
                    continue;
                }

                ProduceAudioBlock(blockFrames);
            }
        }

        private void ProduceAudioBlock(int frameCount)
        {
            if (frameCount <= 0 ||
                !_hullScratch.IsCreated ||
                !_sonarScratch.IsCreated ||
                !_thrusterScratch.IsCreated ||
                !_mixScratch.IsCreated ||
                _sampleRingBuffer == null)
            {
                return;
            }

            long blockStartFrame = _sampleRingBuffer.WriteFrameCursor;
            TryConsumePendingSonarTrigger(blockStartFrame, frameCount);

            float hullTarget = math.saturate(_targetHullStressValue);
            float thrusterBlendTarget = math.saturate(_targetThrusterBlendValue);
            float thrusterLoadTarget = math.saturate(_targetThrusterLoadValue);
            float thrusterPitchTarget = math.max(0.1f, _targetThrusterPitchValue);
            float thrusterPressureTarget = math.saturate(_targetThrusterPressureValue);
            float thrusterAccelerationTarget = math.saturate(_targetThrusterAccelerationValue);
            float thrusterHeavyCarryTarget = math.saturate(_targetThrusterHeavyCarryValue);
            float thrusterDiveTarget = math.saturate(_targetThrusterDiveValue);

            HullStressSynthesisJob hullJob = new HullStressSynthesisJob
            {
                Output = _hullScratch,
                FrameCount = frameCount,
                BlockStartFrame = blockStartFrame,
                InvSampleRate = 1d / math.max(1, _sampleRate),
                StressStart = _audioHullStressValue,
                StressEnd = hullTarget,
                PressureBedAmount = hullPressureBedAmount,
                RacketAmount = hullRivetBurstAmount,
                MasterGain = hullMasterGain
            };
            hullJob.Run(frameCount);
            _audioHullStressValue = hullTarget;

            SonarPingSynthesisJob sonarJob = new SonarPingSynthesisJob
            {
                Output = _sonarScratch,
                FrameCount = frameCount,
                BlockStartFrame = blockStartFrame,
                InvSampleRate = 1d / math.max(1, _sampleRate),
                SonarStartFrame = _workerActiveSonarStartFrame,
                SonarIntensity = _workerActiveSonarIntensity,
                EchoDelaySeconds = _workerActiveSonarEchoDelaySeconds,
                EchoDopplerRatio = _workerActiveSonarEchoDopplerRatio,
                EchoAttenuation = _workerActiveSonarEchoAttenuation,
                AttackBlend = sonarAttackBlend,
                TailBlend = sonarTailBlend,
                SaturationDrive = sonarSaturationDrive,
                MasterGain = sonarMasterGain
            };
            sonarJob.Run(frameCount);

            ThrusterSynthesisJob thrusterJob = new ThrusterSynthesisJob
            {
                Output = _thrusterScratch,
                FrameCount = frameCount,
                BlockStartFrame = blockStartFrame,
                InvSampleRate = 1d / math.max(1, _sampleRate),
                BlendStart = _audioThrusterBlendValue,
                BlendEnd = thrusterBlendTarget,
                LoadStart = _audioThrusterLoadValue,
                LoadEnd = thrusterLoadTarget,
                PitchStart = _audioThrusterPitchValue,
                PitchEnd = thrusterPitchTarget,
                PressureStart = _audioThrusterPressureValue,
                PressureEnd = thrusterPressureTarget,
                AccelerationStart = _audioThrusterAccelerationValue,
                AccelerationEnd = thrusterAccelerationTarget,
                HeavyCarryStart = _audioThrusterHeavyCarryValue,
                HeavyCarryEnd = thrusterHeavyCarryTarget,
                DiveStart = _audioThrusterDiveValue,
                DiveEnd = thrusterDiveTarget,
                MasterGain = thrusterMasterGain
            };
            thrusterJob.Run(frameCount);
            _audioThrusterBlendValue = thrusterBlendTarget;
            _audioThrusterLoadValue = thrusterLoadTarget;
            _audioThrusterPitchValue = thrusterPitchTarget;
            _audioThrusterPressureValue = thrusterPressureTarget;
            _audioThrusterAccelerationValue = thrusterAccelerationTarget;
            _audioThrusterHeavyCarryValue = thrusterHeavyCarryTarget;
            _audioThrusterDiveValue = thrusterDiveTarget;

            float headroom = outputHeadroom;
            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                _mixScratch[frameIndex] = (_hullScratch[frameIndex] + _sonarScratch[frameIndex] + _thrusterScratch[frameIndex]) * headroom;

            ApplyGlobalAbyssalLowPass(frameCount);
            _sampleRingBuffer.TryWrite(_mixScratch, frameCount);
        }

        private void TryConsumePendingSonarTrigger(long blockStartFrame, int frameCount)
        {
            long pendingStartFrame = Interlocked.Read(ref _pendingSonarStartFrame);
            if (pendingStartFrame == long.MinValue)
                return;

            long blockEndFrameExclusive = blockStartFrame + frameCount;
            if (pendingStartFrame >= blockEndFrameExclusive)
                return;

            if (Interlocked.CompareExchange(ref _pendingSonarStartFrame, long.MinValue, pendingStartFrame) != pendingStartFrame)
                return;

            _workerActiveSonarStartFrame = pendingStartFrame;
            _workerActiveSonarIntensity = math.clamp(
                BitConverter.Int32BitsToSingle(Volatile.Read(ref _pendingSonarIntensityBits)),
                0f,
                1f);
            _workerActiveSonarEchoDelaySeconds = math.clamp(
                BitConverter.Int32BitsToSingle(Volatile.Read(ref _pendingSonarEchoDelayBits)),
                0f,
                SonarEchoMaximumDelaySeconds);
            _workerActiveSonarEchoDopplerRatio = math.clamp(
                BitConverter.Int32BitsToSingle(Volatile.Read(ref _pendingSonarEchoDopplerBits)),
                0.85f,
                1.2f);
            _workerActiveSonarEchoAttenuation = math.clamp(
                BitConverter.Int32BitsToSingle(Volatile.Read(ref _pendingSonarEchoAttenuationBits)),
                0.12f,
                1f);
        }

        private void UpdateThrusterTargets(float deltaTime)
        {
            PlayerLocomotionMode locomotionMode = playerMovement.CurrentLocomotionMode;
            bool isSwimMode = locomotionMode == PlayerLocomotionMode.SurfaceSwim ||
                              locomotionMode == PlayerLocomotionMode.UnderwaterSwim;

            float targetBlend = 0f;
            float pitchMultiplier = 1f;
            float pressureAmount = 0f;

            switch (locomotionMode)
            {
                case PlayerLocomotionMode.SurfaceSwim:
                    targetBlend = surfaceSwimModeBlend;
                    pitchMultiplier = surfaceSwimPitchMultiplier;
                    pressureAmount = surfaceSwimVolumeMultiplier;
                    break;

                case PlayerLocomotionMode.UnderwaterSwim:
                    targetBlend = 1f;
                    pitchMultiplier = 1f;
                    pressureAmount = 1f;
                    break;
            }

            _transportFeelContractCurrent = isSwimMode ? ResolveTransportFeelContract() : null;
            float transportBoost = isSwimMode ? ResolveTransportBoost01() : 0f;
            float heavyCarry = isSwimMode && playerMovement.IsDraggingHeavyCargo
                ? playerMovement.HeavyCarryLoad
                : 0f;
            float diveAttack = isSwimMode ? ResolveDiveAttack01() : 0f;
            float depth = math.max(0f, playerMovement.CurrentDepth);

            Vector3 velocity = _playerRigidbody.linearVelocity;
            float speed = math.length(velocity);
            float velocityDelta = math.abs(speed - _lastSpeed) / math.max(deltaTime, 0.0001f);
            _lastSpeed = speed;

            float throttleAttack = math.saturate(velocityDelta / math.max(throttleAttackVelocityDelta, 0.01f));
            float shallowPressure = 1f - math.saturate(
                (depth - cavitationFadeStartDepth) /
                math.max(cavitationFadeEndDepth - cavitationFadeStartDepth, 0.01f));

            if (transportBoost > 0f)
                targetBlend = math.max(targetBlend, transportBoost * ResolveTransportModeBlendFloor());

            float loadTarget = math.saturate(math.max(
                transportBoost,
                transportBoost * 0.65f + throttleAttack * 0.55f + shallowPressure * 0.35f + heavyCarry * 0.2f + diveAttack * 0.18f));
            float pitchTarget = math.max(0.1f, pitchMultiplier * (1f - heavyCarry * heavyCarryPitchDrag));
            float pressureTarget = math.saturate(pressureAmount * shallowPressure);
            float heavyCarryTarget = math.saturate(heavyCarry * (1f + heavyCarryVolumeBoost));

            float blendT = 1f - math.exp(-math.max(thrusterFollowSharpness, 0.01f) * deltaTime);
            _thrusterBlendTickValue = math.lerp(_thrusterBlendTickValue, targetBlend, blendT);
            _thrusterLoadTickValue = math.lerp(_thrusterLoadTickValue, loadTarget, blendT);
            _thrusterPitchTickValue = math.lerp(_thrusterPitchTickValue, pitchTarget, blendT);
            _thrusterPressureTickValue = math.lerp(_thrusterPressureTickValue, pressureTarget, blendT);
            _thrusterAccelerationTickValue = math.lerp(_thrusterAccelerationTickValue, throttleAttack, blendT);
            _thrusterHeavyCarryTickValue = math.lerp(_thrusterHeavyCarryTickValue, heavyCarryTarget, blendT);
            _thrusterDiveTickValue = math.lerp(_thrusterDiveTickValue, diveAttack, blendT);

            _targetThrusterBlendValue = _thrusterBlendTickValue;
            _targetThrusterLoadValue = _thrusterLoadTickValue;
            _targetThrusterPitchValue = _thrusterPitchTickValue;
            _targetThrusterPressureValue = _thrusterPressureTickValue;
            _targetThrusterAccelerationValue = _thrusterAccelerationTickValue;
            _targetThrusterHeavyCarryValue = _thrusterHeavyCarryTickValue;
            _targetThrusterDiveValue = _thrusterDiveTickValue;
        }

        private void UpdateCaveReverb(float deltaTime)
        {
            ResolveListenerReverbFilter();
            if (_listenerReverbFilter == null)
                return;

            float defaultDistance = math.max(1f, ceilingProbeDistance);
            bool shouldUseWaterReverb = playerMovement != null && playerMovement.IsPlayerSubmerged;
            if (!shouldUseWaterReverb)
            {
                _targetCaveReverbBlend = 0f;
                _caveReverbBlend = 0f;
                ResetEnclosureProbeState(defaultDistance);
                RestoreListenerReverbDefaults();
                return;
            }

            float reverbBlendT = 1f - math.exp(-math.max(caveReverbFollowSharpness, 0.01f) * deltaTime);
            _targetEnclosureVolume = math.clamp(_targetEnclosureVolume, 0.01f, defaultDistance);
            _smoothedEnclosureVolume = math.lerp(_smoothedEnclosureVolume, _targetEnclosureVolume, reverbBlendT);
            _targetCaveReverbBlend = 1f - math.saturate(_smoothedEnclosureVolume / math.max(caveCeilingThreshold, 0.01f));
            _caveReverbBlend = math.lerp(_caveReverbBlend, _targetCaveReverbBlend, reverbBlendT);
            ApplyListenerReverbProfile(_caveReverbBlend);
        }

        private float ResolveProbeDistanceMeters(Vector3 direction, float fallbackDistance)
        {
            if (_boundPlayerTransform == null)
                return fallbackDistance;

            Vector3 origin = _boundPlayerTransform.position + Vector3.up * 0.5f;
            int hitCount = UnityEngine.Physics.RaycastNonAlloc(
                origin,
                direction,
                _ceilingProbeHits,
                fallbackDistance,
                ceilingProbeLayers,
                QueryTriggerInteraction.Ignore);
            if (hitCount <= 0)
                return fallbackDistance;

            float bestDistance = fallbackDistance;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _ceilingProbeHits[i];
                if (hit.collider == null)
                    continue;

                Transform hitTransform = hit.collider.transform;
                if (_boundPlayerTransform != null && hitTransform != null && hitTransform.IsChildOf(_boundPlayerTransform))
                    continue;

                if (hit.distance < bestDistance)
                    bestDistance = hit.distance;
            }

            return bestDistance;
        }

        private Vector3 ResolveProbeDirection(int axisIndex)
        {
            switch (axisIndex)
            {
                case 0:
                    return Vector3.up;
                case 1:
                    return Vector3.down;
                case 2:
                    return Vector3.left;
                case 3:
                    return Vector3.right;
                case 4:
                    return Vector3.forward;
                default:
                    return Vector3.back;
            }
        }

        private void ResetEnclosureProbeState(float defaultDistance)
        {
            float clampedDistance = math.max(1f, defaultDistance);
            for (int i = 0; i < _orthogonalProbeDistances.Length; i++)
                _orthogonalProbeDistances[i] = clampedDistance;

            _probeAxisIndex = 0;
            _targetEnclosureVolume = clampedDistance;
            _smoothedEnclosureVolume = clampedDistance;
        }

        private void ResolveListenerReverbFilter()
        {
            if (_listenerReverbFilter != null)
                return;

            if (!TryGetComponent(out _listenerReverbFilter))
            {
                _listenerReverbFilter = gameObject.AddComponent<AudioReverbFilter>(); // COLD ALLOC: AudioReverbFilter[1] - procedural cave/open-water reverb fallback - owner: PlayerCriticalProceduralAudioRenderer
                _listenerReverbFilter.enabled = false;
            }

            if (_listenerReverbDefaultsCaptured || _listenerReverbFilter == null)
                return;

            _listenerReverbWasEnabled = _listenerReverbFilter.enabled;
            _listenerReverbBasePreset = _listenerReverbFilter.reverbPreset;
            _listenerReverbBaseDecayTime = _listenerReverbFilter.decayTime;
            _listenerReverbBaseReflectionsLevel = _listenerReverbFilter.reflectionsLevel;
            _listenerReverbBaseRoomHighFrequency = _listenerReverbFilter.roomHF;
            _listenerReverbDefaultsCaptured = true;
        }

        private void ApplyListenerReverbProfile(float caveBlend)
        {
            if (_listenerReverbFilter == null)
                return;

            _listenerReverbFilter.enabled = true;
            _listenerReverbFilter.reverbPreset = AudioReverbPreset.User;
            _listenerReverbFilter.decayTime = math.lerp(openWaterDecayTime, caveDecayTime, caveBlend);
            _listenerReverbFilter.reflectionsLevel = math.lerp(openWaterReflectionsLevel, caveReflectionsLevel, caveBlend);
            _listenerReverbFilter.roomHF = math.lerp(openWaterRoomHighFrequency, caveRoomHighFrequency, caveBlend);
        }

        private void RestoreListenerReverbDefaults()
        {
            if (!_listenerReverbDefaultsCaptured || _listenerReverbFilter == null)
                return;

            _listenerReverbFilter.reverbPreset = _listenerReverbBasePreset;
            _listenerReverbFilter.decayTime = _listenerReverbBaseDecayTime;
            _listenerReverbFilter.reflectionsLevel = _listenerReverbBaseReflectionsLevel;
            _listenerReverbFilter.roomHF = _listenerReverbBaseRoomHighFrequency;
            _listenerReverbFilter.enabled = _listenerReverbWasEnabled;
        }

        private void HandleSonarPingSent(float intensity)
        {
            ResolveSonarEchoModel(
                out float echoDelaySeconds,
                out float echoDopplerRatio,
                out float echoAttenuation);

            long consumerFrame = _sampleRingBuffer != null ? _sampleRingBuffer.ReadFrameCursor : 0L;
            long producerFrame = _sampleRingBuffer != null ? _sampleRingBuffer.WriteFrameCursor : consumerFrame;
            long scheduledStartFrame = math.max(producerFrame, consumerFrame);
            long scheduledLeadFrames = math.max(0L, scheduledStartFrame - consumerFrame);
            double scheduledDspTime = AudioSettings.dspTime + (scheduledLeadFrames / (double)math.max(_sampleRate, 1));

            Volatile.Write(ref _pendingSonarIntensityBits, BitConverter.SingleToInt32Bits(math.saturate(intensity)));
            Volatile.Write(ref _pendingSonarEchoDelayBits, BitConverter.SingleToInt32Bits(echoDelaySeconds));
            Volatile.Write(ref _pendingSonarEchoDopplerBits, BitConverter.SingleToInt32Bits(echoDopplerRatio));
            Volatile.Write(ref _pendingSonarEchoAttenuationBits, BitConverter.SingleToInt32Bits(echoAttenuation));
            Interlocked.Exchange(ref _pendingSonarStartFrame, scheduledStartFrame);

            ProceduralAudioEvents.RaiseAudioPingTriggered(scheduledDspTime, math.saturate(intensity), SonarChirpDurationSeconds);
        }

        private void HandleAudioConfigurationChanged(bool deviceWasChanged)
        {
            RefreshAudioConfiguration();
        }

        private void TryBindFromBootstrap()
        {
            GameObject playerObject = SceneBootstrap.CurrentPlayerObject;
            if (playerObject != null)
            {
                if (!ReferenceEquals(_boundPlayerObject, playerObject))
                    BindToPlayer(playerObject);
                return;
            }

            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) && playerTransform != null)
                BindToPlayer(playerTransform.gameObject);
        }

        private void TryRegister()
        {
            GameTickManager gameTickManager = GameTickManager.Instance;
            if (gameTickManager == null)
                return;

            if (!_registered)
            {
                gameTickManager.Register((ITickable)this);
                _registered = true;
            }

            if (_slowTickRegistered)
                return;

            gameTickManager.Register((ISlowTickable)this);
            _slowTickRegistered = true;
        }

        private void TryUnregister()
        {
            GameTickManager gameTickManager = GameTickManager.Instance;
            if (gameTickManager != null && _registered)
                gameTickManager.Unregister((ITickable)this);

            if (gameTickManager != null && _slowTickRegistered)
                gameTickManager.Unregister((ISlowTickable)this);

            _registered = false;
            _slowTickRegistered = false;
        }

        private void RefreshAudioConfiguration()
        {
            bool shouldRestartWorker = Volatile.Read(ref _audioProducerRunning) != 0;
            if (shouldRestartWorker)
                StopAudioProducerThread();

            _sampleRate = math.max(1, AudioSettings.outputSampleRate);
            ClearLowPassState();
            AudioSettings.GetDSPBufferSize(out int bufferLength, out _);
            int requestedCapacity = math.max(2048, NextPowerOfTwo(math.max(bufferLength, 1024) * 4));
            if (requestedCapacity > MaxSafeFrameCapacity)
                requestedCapacity = MaxSafeFrameCapacity;

            EnsureBuffers(requestedCapacity);

            if (shouldRestartWorker && isActiveAndEnabled)
                StartAudioProducerThread();
        }

        private void EnsureBuffers(int frameCapacity)
        {
            if (_buffersInitialized && _frameCapacity == frameCapacity)
                return;

            DisposeBuffers();

            _frameCapacity = frameCapacity;
            _hullScratch = new NativeArray<float>(_frameCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[frameCapacity] - hull-stress DSP scratch - owner: PlayerCriticalProceduralAudioRenderer
            _sonarScratch = new NativeArray<float>(_frameCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[frameCapacity] - sonar DSP scratch - owner: PlayerCriticalProceduralAudioRenderer
            _thrusterScratch = new NativeArray<float>(_frameCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[frameCapacity] - thruster DSP scratch - owner: PlayerCriticalProceduralAudioRenderer
            _mixScratch = new NativeArray<float>(_frameCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[frameCapacity] - mixed procedural audio worklet scratch - owner: PlayerCriticalProceduralAudioRenderer
            _sampleRingBuffer ??= new NativeAudioFrameRingBuffer();
            _sampleRingBuffer.Initialize(math.max(frameCapacity * 16, ringBufferCapacityFrames));
            _workerActiveSonarStartFrame = long.MinValue;
            _buffersInitialized = true;
        }

        private void DisposeBuffers()
        {
            if (_hullScratch.IsCreated)
                _hullScratch.Dispose();

            if (_sonarScratch.IsCreated)
                _sonarScratch.Dispose();

            if (_thrusterScratch.IsCreated)
                _thrusterScratch.Dispose();

            if (_mixScratch.IsCreated)
                _mixScratch.Dispose();

            _sampleRingBuffer?.Dispose();

            _buffersInitialized = false;
            _frameCapacity = 0;
        }

        private void ClearLowPassState()
        {
            Array.Clear(_lowPassInputHistory1, 0, _lowPassInputHistory1.Length);
            Array.Clear(_lowPassInputHistory2, 0, _lowPassInputHistory2.Length);
            Array.Clear(_lowPassOutputHistory1, 0, _lowPassOutputHistory1.Length);
            Array.Clear(_lowPassOutputHistory2, 0, _lowPassOutputHistory2.Length);
            _audioAbyssalLowPassMix = 0f;
            _workerActiveSonarStartFrame = long.MinValue;
            Interlocked.Exchange(ref _pendingSonarStartFrame, long.MinValue);
        }

        private float ResolveAbyssalLowPassTarget(float depthMeters)
        {
            return math.saturate(
                (math.max(0f, depthMeters) - AbyssalLowPassStartDepthMeters) /
                math.max(AbyssalLowPassFadeDepthMeters, 0.01f));
        }

        private void ResolveSonarEchoModel(
            out float echoDelaySeconds,
            out float echoDopplerRatio,
            out float echoAttenuation)
        {
            echoDelaySeconds = 0.24f;
            echoDopplerRatio = 1f;
            echoAttenuation = 0.42f;

            if (_boundPlayerObject == null || _playerRigidbody == null)
                return;

            if (!TryResolveNearestSonarReflector(_boundPlayerObject.transform.position, out Vector3 reflectorPosition, out float distanceMeters))
                return;

            Vector3 toReflector = reflectorPosition - _boundPlayerObject.transform.position;
            float distance = math.max(0.01f, math.length(toReflector));
            Vector3 reflectorDirection = toReflector / distance;
            float closingSpeedMetersPerSecond = Vector3.Dot(_playerRigidbody.linearVelocity, reflectorDirection);
            float clampedSpeed = math.clamp(closingSpeedMetersPerSecond, -80f, 80f);
            float numerator = SoundSpeedWaterMetersPerSecond + clampedSpeed;
            float denominator = math.max(1f, SoundSpeedWaterMetersPerSecond - clampedSpeed);

            distanceMeters = math.min(distanceMeters, SonarEchoMaximumDistanceMeters);
            echoDelaySeconds = math.min((distanceMeters * 2f) / SoundSpeedWaterMetersPerSecond, SonarEchoMaximumDelaySeconds);
            echoDopplerRatio = math.clamp(numerator / denominator, 0.85f, 1.2f);
            echoAttenuation = math.clamp(
                SonarEchoReferenceDistanceMeters / (SonarEchoReferenceDistanceMeters + distanceMeters),
                0.12f,
                0.95f);
        }

        private bool TryResolveNearestSonarReflector(Vector3 playerPosition, out Vector3 reflectorPosition, out float distanceMeters)
        {
            reflectorPosition = Vector3.zero;
            distanceMeters = float.MaxValue;

            HectonMapMagicVegetationBridge vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (vegetationBridge == null)
                return false;

            if (vegetationBridge.TryGetActiveAbyssalAnchorPayload(out NativeArray<Vector3> anchors, out int anchorCount))
                AccumulateNearestPayloadPoint(playerPosition, anchors, anchorCount, ref reflectorPosition, ref distanceMeters);

            if (vegetationBridge.TryGetActiveAbyssalNavNodePayload(out NativeArray<Vector3> nodes, out int nodeCount))
                AccumulateNearestPayloadPoint(playerPosition, nodes, nodeCount, ref reflectorPosition, ref distanceMeters);

            return distanceMeters < float.MaxValue;
        }

        private static void AccumulateNearestPayloadPoint(
            Vector3 playerPosition,
            NativeArray<Vector3> points,
            int count,
            ref Vector3 nearestPoint,
            ref float nearestDistance)
        {
            int safeCount = math.min(count, points.Length);
            for (int i = 0; i < safeCount; i++)
            {
                Vector3 candidate = points[i];
                float candidateDistance = math.distance(playerPosition, candidate);
                if (candidateDistance >= nearestDistance)
                    continue;

                nearestDistance = candidateDistance;
                nearestPoint = candidate;
            }
        }

        private void ApplyGlobalAbyssalLowPass(int frameCount)
        {
            float targetMix = math.saturate(_targetAbyssalLowPassMix);
            if (targetMix <= 0.0001f && _audioAbyssalLowPassMix <= 0.0001f)
                return;

            float startMix = _audioAbyssalLowPassMix;
            float endMix = targetMix;

            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                float frameT = frameCount > 1 ? frameIndex / (float)(frameCount - 1) : 1f;
                float mix = math.lerp(startMix, endMix, frameT);
                float cutoff = math.lerp(_sampleRate * 0.45f, AbyssalLowPassCutoffHertz, mix);
                ComputeLowPassCoefficients(cutoff, out float b0, out float b1, out float b2, out float a1, out float a2);

                float input = _mixScratch[frameIndex];
                float filtered =
                    b0 * input +
                    b1 * _lowPassInputHistory1[0] +
                    b2 * _lowPassInputHistory2[0] -
                    a1 * _lowPassOutputHistory1[0] -
                    a2 * _lowPassOutputHistory2[0];

                _lowPassInputHistory2[0] = _lowPassInputHistory1[0];
                _lowPassInputHistory1[0] = input;
                _lowPassOutputHistory2[0] = _lowPassOutputHistory1[0];
                _lowPassOutputHistory1[0] = filtered;
                _mixScratch[frameIndex] = math.lerp(input, filtered, mix);
            }

            _audioAbyssalLowPassMix = endMix;
        }

        private void ComputeLowPassCoefficients(
            float cutoffHertz,
            out float b0,
            out float b1,
            out float b2,
            out float a1,
            out float a2)
        {
            float normalizedCutoff = math.clamp(cutoffHertz, 32f, _sampleRate * 0.45f);
            float omega = TwoPi * normalizedCutoff / math.max(_sampleRate, 1f);
            float cosine = math.cos(omega);
            float sine = math.sin(omega);
            float alpha = sine / (2f * 0.70710678f);
            float inverseA0 = 1f / math.max(0.0001f, 1f + alpha);

            b0 = ((1f - cosine) * 0.5f) * inverseA0;
            b1 = (1f - cosine) * inverseA0;
            b2 = ((1f - cosine) * 0.5f) * inverseA0;
            a1 = (-2f * cosine) * inverseA0;
            a2 = (1f - alpha) * inverseA0;
        }

        private float ResolveTransportBoost01()
        {
            if (playerTransportCoordinator == null && _boundPlayerObject != null)
                _boundPlayerObject.TryGetComponent(out playerTransportCoordinator);

            bool coordinatorOwnsTransport = playerTransportCoordinator != null && playerTransportCoordinator.HasActiveTransportSource();
            if (coordinatorOwnsTransport)
                return playerTransportCoordinator.ResolveTransportBoost01();

            if (playerToolManager == null || playerToolManager.IsSwapping)
                return 0f;

            IPlayerTransportSource transportSource = playerToolManager.CurrentToolTransportSource;
            if (transportSource == null)
                return 0f;

            float transportBoost = transportSource.GetTransportBoost01();
            return transportBoost > 0f ? math.saturate(transportBoost) : 0f;
        }

        private PlayerTransportFeelContract ResolveTransportFeelContract()
        {
            if (playerTransportCoordinator == null && _boundPlayerObject != null)
                _boundPlayerObject.TryGetComponent(out playerTransportCoordinator);

            bool coordinatorOwnsTransport = playerTransportCoordinator != null && playerTransportCoordinator.HasActiveTransportSource();
            if (coordinatorOwnsTransport)
                return playerTransportCoordinator.ResolveTransportFeelContract();

            if (playerToolManager == null || playerToolManager.IsSwapping)
                return null;

            return playerToolManager.CurrentToolTransportFeelContract;
        }

        private float ResolveTransportModeBlendFloor()
        {
            return _transportFeelContractCurrent != null
                ? _transportFeelContractCurrent.AudioModeBlendFloor
                : 0.35f;
        }

        private float ResolveDiveAttack01()
        {
            Vector3 velocity = _playerRigidbody.linearVelocity;
            float downwardSpeed = math.max(0f, -velocity.y);
            return math.saturate(downwardSpeed / 2.4f);
        }

        private static int NextPowerOfTwo(int value)
        {
            int power = 1;
            while (power < value && power < MaxSafeFrameCapacity)
                power <<= 1;
            return power;
        }

        [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct HullStressSynthesisJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<float> Output;
            [ReadOnly] public int FrameCount;
            [ReadOnly] public long BlockStartFrame;
            [ReadOnly] public double InvSampleRate;
            [ReadOnly] public float StressStart;
            [ReadOnly] public float StressEnd;
            [ReadOnly] public float PressureBedAmount;
            [ReadOnly] public float RacketAmount;
            [ReadOnly] public float MasterGain;

            public void Execute(int index)
            {
                if (index >= FrameCount)
                {
                    Output[index] = 0f;
                    return;
                }

                float frameT = FrameCount > 1 ? index / (float)(FrameCount - 1) : 0f;
                float stress = math.lerp(StressStart, StressEnd, frameT);
                if (stress <= HullNoiseFloor)
                {
                    Output[index] = 0f;
                    return;
                }

                long sampleFrame = BlockStartFrame + index;
                double time = sampleFrame * InvSampleRate;
                uint sampleIndex = (uint)math.max(0L, sampleFrame);
                float pressureLfo = 0.6f + 0.4f * Oscillator(time, 0.3f);
                float pressureBed =
                    (LayeredBrownLike(sampleIndex) * pressureLfo * PressureBedAmount) * math.sqrt(stress);

                float carrierA = math.lerp(120f, 800f, math.pow(stress, 0.82f));
                float carrierB = carrierA * 1.72f;
                float carrierC = carrierA * 2.43f;
                float stickSlip =
                    0.72f +
                    0.28f * Oscillator(time, math.lerp(22f, 43f, stress)) *
                    (0.7f + 0.3f * HeldNoise(sampleIndex, 5, 0x18273645u));
                float frictionNoiseOperator =
                    HeldNoise(sampleIndex, 3, 0x7124AB11u) * 0.62f +
                    HeldNoise(sampleIndex, 5, 0x31DF19A3u) * 0.38f;
                float groanEnvelope = math.pow(0.5f + 0.5f * Oscillator(time, 0.22f), 4f);
                float modIndex = (1.8f + 6.2f * stress) * stickSlip;
                float modulatorA = Oscillator(time, math.lerp(45f, 97f, stress));
                float modulatorB = Oscillator(time, math.lerp(87f, 133f, stress * 0.8f));
                float lowCarrierFm =
                    math.sin((float)(TwoPi * FractionalCycles(time, 80f)) + frictionNoiseOperator * (0.4f + 3.6f * stress) * stickSlip) *
                    (0.18f + 0.26f * stress);

                float metal =
                    math.sin((float)(TwoPi * FractionalCycles(time, carrierA)) + modIndex * modulatorA) * 0.54f +
                    math.sin((float)(TwoPi * FractionalCycles(time, carrierB)) + modIndex * 0.62f * modulatorB) * 0.29f +
                    math.sin((float)(TwoPi * FractionalCycles(time, carrierC)) + modIndex * 0.35f * modulatorA) * 0.17f;
                metal = (metal + lowCarrierFm) * groanEnvelope * math.lerp(0.25f, 1f, stress);

                float rivetBurst = BuildRivetBurst(sampleIndex, stress, RacketAmount);
                float combined = pressureBed + metal + rivetBurst;
                Output[index] = math.tanh(combined * 1.7f) * MasterGain;
            }

            private static float BuildRivetBurst(uint sampleIndex, float stress, float amount)
            {
                if (amount <= 0f || stress <= 0.02f)
                    return 0f;

                uint blockIndex = sampleIndex >> 7;
                float gate = Hash01(blockIndex ^ 0xA531F91u);
                float threshold = math.lerp(0.9984f, 0.965f, stress);
                if (gate < threshold)
                    return 0f;

                uint blockOffset = sampleIndex & 127u;
                float decay = math.exp(-0.07f * blockOffset);
                float x0 = HashSigned(sampleIndex ^ 0x51AF34Du);
                float x1 = HashSigned((sampleIndex - 1u) ^ 0x51AF34Du);
                float x2 = HashSigned((sampleIndex - 2u) ^ 0x51AF34Du);
                float highPass2 = x0 - 2f * x1 + x2;
                return highPass2 * decay * amount * math.saturate(stress * 1.35f);
            }
        }

        [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct SonarPingSynthesisJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<float> Output;
            [ReadOnly] public int FrameCount;
            [ReadOnly] public long BlockStartFrame;
            [ReadOnly] public double InvSampleRate;
            [ReadOnly] public long SonarStartFrame;
            [ReadOnly] public float SonarIntensity;
            [ReadOnly] public float EchoDelaySeconds;
            [ReadOnly] public float EchoDopplerRatio;
            [ReadOnly] public float EchoAttenuation;
            [ReadOnly] public float AttackBlend;
            [ReadOnly] public float TailBlend;
            [ReadOnly] public float SaturationDrive;
            [ReadOnly] public float MasterGain;

            public void Execute(int index)
            {
                if (index >= FrameCount || SonarIntensity <= 0f || SonarStartFrame == long.MinValue)
                {
                    Output[index] = 0f;
                    return;
                }

                long sampleFrame = BlockStartFrame + index;
                double time = sampleFrame * InvSampleRate;
                float age = (float)((sampleFrame - SonarStartFrame) * InvSampleRate);
                if (age < 0f || age > SonarTotalDurationSeconds)
                {
                    Output[index] = 0f;
                    return;
                }

                uint sampleIndex = (uint)math.max(0L, sampleFrame);
                float attack = 0f;
                if (age < 0.03f)
                {
                    float attackEnv = math.exp(-age * 220f);
                    float attackNoise = HashSigned(sampleIndex ^ 0x3941AA1u);
                    attack = attackEnv * (Oscillator(age, 4500f) + attackNoise * 0.85f) * AttackBlend;
                }

                float chirp = 0f;
                if (age < SonarChirpDurationSeconds)
                {
                    float chirpSlope = (400f - 2000f) / SonarChirpDurationSeconds;
                    float phase = TwoPi * (2000f * age + 0.5f * chirpSlope * age * age);
                    float chirpEnv = math.exp(-age * 5f);
                    chirp = chirpEnv * math.sin(phase);
                }

                float echo = 0f;
                float echoAge = age - EchoDelaySeconds;
                if (echoAge >= 0f && echoAge < SonarChirpDurationSeconds)
                {
                    float echoStartFrequency = 2000f * EchoDopplerRatio;
                    float echoEndFrequency = 400f * EchoDopplerRatio;
                    float echoSlope = (echoEndFrequency - echoStartFrequency) / SonarChirpDurationSeconds;
                    float echoPhase = TwoPi * (echoStartFrequency * echoAge + 0.5f * echoSlope * echoAge * echoAge);
                    float echoEnvelope = math.exp(-echoAge * 4.5f) * EchoAttenuation;
                    echo = math.sin(echoPhase) * echoEnvelope;
                }

                float tail = 0f;
                if (age >= 0.08f)
                {
                    float tailAge = age - 0.08f;
                    float tailEnv = math.saturate(tailAge / 0.24f) * math.exp(-tailAge * 0.95f);
                    float slowLfo = 0.55f + 0.45f * Oscillator(age, 0.38f);
                    float beat =
                        Oscillator(age, 150f) +
                        Oscillator(age, 147f) * 0.6f +
                        Oscillator(age, 300f) * 0.4f;
                    float pinkTail = LayeredPinkLike(sampleIndex) * slowLfo;
                    tail = tailEnv * ((beat * 0.46f) + (pinkTail * 0.54f)) * TailBlend;
                }

                float mixed = (attack + chirp + echo + tail) * SonarIntensity;
                Output[index] = math.tanh(mixed * SaturationDrive) * MasterGain;
            }
        }

        [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ThrusterSynthesisJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<float> Output;
            [ReadOnly] public int FrameCount;
            [ReadOnly] public long BlockStartFrame;
            [ReadOnly] public double InvSampleRate;
            [ReadOnly] public float BlendStart;
            [ReadOnly] public float BlendEnd;
            [ReadOnly] public float LoadStart;
            [ReadOnly] public float LoadEnd;
            [ReadOnly] public float PitchStart;
            [ReadOnly] public float PitchEnd;
            [ReadOnly] public float PressureStart;
            [ReadOnly] public float PressureEnd;
            [ReadOnly] public float AccelerationStart;
            [ReadOnly] public float AccelerationEnd;
            [ReadOnly] public float HeavyCarryStart;
            [ReadOnly] public float HeavyCarryEnd;
            [ReadOnly] public float DiveStart;
            [ReadOnly] public float DiveEnd;
            [ReadOnly] public float MasterGain;

            public void Execute(int index)
            {
                if (index >= FrameCount)
                {
                    Output[index] = 0f;
                    return;
                }

                float frameT = FrameCount > 1 ? index / (float)(FrameCount - 1) : 0f;
                float blend = math.lerp(BlendStart, BlendEnd, frameT);
                if (blend <= 0.0001f)
                {
                    Output[index] = 0f;
                    return;
                }

                float load = math.lerp(LoadStart, LoadEnd, frameT);
                float pitchScale = math.lerp(PitchStart, PitchEnd, frameT);
                float pressure = math.lerp(PressureStart, PressureEnd, frameT);
                float acceleration = math.lerp(AccelerationStart, AccelerationEnd, frameT);
                float heavyCarry = math.lerp(HeavyCarryStart, HeavyCarryEnd, frameT);
                float dive = math.lerp(DiveStart, DiveEnd, frameT);
                long sampleFrame = BlockStartFrame + index;
                double time = sampleFrame * InvSampleRate;
                uint sampleIndex = (uint)math.max(0L, sampleFrame);

                float hum =
                    Oscillator(time, 80f * pitchScale) * 1.00f +
                    Oscillator(time, 160f * pitchScale) * 0.60f +
                    Oscillator(time, 240f * pitchScale) * 0.35f +
                    Oscillator(time, 320f * pitchScale) * 0.15f;
                hum *= 0.42f;

                float flowMod = 0.55f + 0.45f * Oscillator(time, 0.31f);
                float flowNoise = LayeredPinkLike(sampleIndex ^ 0xCAFEBABEu);
                float flow = flowNoise * flowMod * (0.18f + 0.22f * load + 0.08f * heavyCarry);

                float propCycle = 0.5f + 0.5f * Oscillator(time, 20f);
                float envelopeSharpness = math.lerp(5f, 0.5f, math.saturate(load + acceleration * 0.35f));
                float dynamicEnvelope = math.pow(math.saturate(propCycle), envelopeSharpness);
                float highNoise = HighBandNoise(sampleIndex);
                float cavitation = highNoise * highNoise * highNoise;
                cavitation *= dynamicEnvelope * math.saturate(load * 1.2f + pressure * 0.75f + acceleration * 0.55f + dive * 0.2f);

                float mixed = hum + flow + cavitation * 0.78f;
                Output[index] = math.tanh(mixed * 2.0f) * MasterGain * blend;
            }
        }

        private static double FractionalCycles(double time, float frequency)
        {
            double cycles = time * frequency;
            return cycles - math.floor(cycles);
        }

        private static float Oscillator(double time, float frequency)
        {
            return (float)math.sin(TwoPi * FractionalCycles(time, frequency));
        }

        private static float Oscillator(float time, float frequency)
        {
            float cycles = time * frequency;
            float wrapped = cycles - math.floor(cycles);
            return math.sin(TwoPi * wrapped);
        }

        private static float HeldNoise(uint sampleIndex, int shift, uint seed)
        {
            return HashSigned((sampleIndex >> shift) ^ seed);
        }

        private static float LayeredBrownLike(uint sampleIndex)
        {
            float low0 = HeldNoise(sampleIndex, 9, 0x19A21C31u) * 0.46f;
            float low1 = HeldNoise(sampleIndex, 11, 0x6A8B13C7u) * 0.31f;
            float low2 = HeldNoise(sampleIndex, 13, 0x2F3E8B97u) * 0.18f;
            float low3 = HeldNoise(sampleIndex, 15, 0x54D91C51u) * 0.11f;
            return low0 + low1 + low2 + low3;
        }

        private static float LayeredPinkLike(uint sampleIndex)
        {
            float octave0 = HeldNoise(sampleIndex, 0, 0x14583AA1u) * 0.18f;
            float octave1 = HeldNoise(sampleIndex, 2, 0x7A15D913u) * 0.22f;
            float octave2 = HeldNoise(sampleIndex, 4, 0x5E2334B1u) * 0.24f;
            float octave3 = HeldNoise(sampleIndex, 6, 0x312F1C99u) * 0.21f;
            float octave4 = HeldNoise(sampleIndex, 8, 0x9D72A113u) * 0.15f;
            return octave0 + octave1 + octave2 + octave3 + octave4;
        }

        private static float HighBandNoise(uint sampleIndex)
        {
            float x0 = HashSigned(sampleIndex ^ 0x5915AA09u);
            float x1 = HashSigned((sampleIndex - 1u) ^ 0x5915AA09u);
            float x3 = HashSigned((sampleIndex - 3u) ^ 0x31D7A2C3u);
            float x5 = HashSigned((sampleIndex - 5u) ^ 0x41B22F11u);
            return (x0 - x1) * 0.75f + (x0 - x3) * 0.18f + (x0 - x5) * 0.07f;
        }

        private static float HashSigned(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) * (1f / 8388607.5f) - 1f;
        }

        private static float Hash01(uint value)
        {
            return HashSigned(value) * 0.5f + 0.5f;
        }
    }
}
