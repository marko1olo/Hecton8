using System;
using System.Threading;
using Hecton8.AI;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Visor;
using Hecton8.World;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

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
        private const float SonarEchoAbsorptionCoefficient = 0.0035f;
        private const float AbyssalLowPassStartDepthMeters = 4000f;
        private const float AbyssalLowPassFadeDepthMeters = 800f;
        private const float AbyssalLowPassCutoffHertz = 2000f;
        private const float MinimumProbeDistanceMeters = 0.001f;
        private const float MaximumProbeDistanceMeters = 200f;
        private const float BiquadDenormalBias = 1e-15f;
        private const int MaxSafeFrameCapacity = 16384;
        private const int MaxFilterChannels = 8;
        private const int MaxDynamicSonarReflectorCount = 24;

        private static readonly int PlayerLayer = LayerMask.NameToLayer("Player");
        private static readonly int VehicleLayer = LayerMask.NameToLayer("Vehicle");
        private static readonly int BaseModuleLayer = LayerMask.NameToLayer("BaseModule");
        private static readonly int TriggerZoneLayer = LayerMask.NameToLayer("TriggerZone");
        private static readonly int TransparentFxLayer = LayerMask.NameToLayer("TransparentFX");
        private static readonly int FirstPersonToolsLayer = LayerMask.NameToLayer("FirstPersonTools");

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
        [FormerlySerializedAs("ceilingProbeLayers")]
        [SerializeField] private LayerMask enclosureProbeLayers = ~0;

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

        // COLD ALLOC: float[frameCapacity] - hull-stress DSP scratch - owner: PlayerCriticalProceduralAudioRenderer
        private float[] _hullScratch;
        // COLD ALLOC: float[frameCapacity] - sonar DSP scratch - owner: PlayerCriticalProceduralAudioRenderer
        private float[] _sonarScratch;
        // COLD ALLOC: float[frameCapacity] - thruster DSP scratch - owner: PlayerCriticalProceduralAudioRenderer
        private float[] _thrusterScratch;
        // COLD ALLOC: float[frameCapacity] - mixed procedural audio worklet scratch - owner: PlayerCriticalProceduralAudioRenderer
        private float[] _mixScratch;
        private AudioFrameSpscRingBuffer _sampleRingBuffer;
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
        private float _audioAbyssalLowPassMix;
        private float _caveReverbBlend;
        private float _smoothedReverbDecayTime;
        private float _smoothedEnclosureVolume;
        private float _targetEnclosureVolume;
        private int _probeAxisIndex;
        private int _audioProducerRunning;
        private int _resolvedEnclosureProbeLayerMask;
        private int _resolvedAcousticOcclusionLayerMask;
        private bool _listenerReverbDefaultsCaptured;
        private bool _listenerReverbWasEnabled;
        private bool _slowProbeScheduled;
        private AudioReverbPreset _listenerReverbBasePreset = AudioReverbPreset.Off;
        private float _listenerReverbBaseDecayTime = 1f;
        private float _listenerReverbBaseReflectionsLevel = -10000f;
        private float _listenerReverbBaseRoomHighFrequency = -10000f;
        private int _scheduledProbeAxisIndex;
        private int _pendingSonarStateReadIndex;
        private int _pendingSonarSequence;
        private int _workerConsumedSonarSequence;
        private JobHandle _slowProbeHandle;
        // COLD ALLOC: NativeArray<RaycastCommand>[1] - staggered enclosure probe command buffer - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<RaycastCommand> _slowProbeCommands;
        // COLD ALLOC: NativeArray<RaycastHit>[1] - staggered enclosure probe result buffer - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<RaycastHit> _slowProbeResults;
        private SonarTriggerState _pendingSonarStateA;
        private SonarTriggerState _pendingSonarStateB;
        private SonarTriggerState _workerActiveSonarState;
        private HullSynthesisState _hullSynthesisState;
        private SonarSynthesisState _sonarSynthesisState;
        private ThrusterSynthesisState _thrusterSynthesisState;

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
        // COLD ALLOC: RaycastHit[8] - sonar occlusion chain hits - owner: PlayerCriticalProceduralAudioRenderer
        private readonly RaycastHit[] _sonarOcclusionHits = new RaycastHit[8];
        // COLD ALLOC: SpatialQueryHit[24] - moving sonar reflector candidates - owner: PlayerCriticalProceduralAudioRenderer
        private readonly SpatialQueryHit[] _dynamicSonarReflectorBuffer = new SpatialQueryHit[MaxDynamicSonarReflectorCount];

        private struct SonarTriggerState
        {
            public int Sequence;
            public long StartFrame;
            public float Intensity;
            public float EchoDelaySeconds;
            public float EchoDopplerRatio;
            public float EchoAttenuation;
            public float EchoLowPassCutoffHz;
        }

        private struct HullSynthesisState
        {
            public double PressureLfoPhase;
            public double StickSlipPhase;
            public double GroanEnvelopePhase;
            public double ModulatorAPhase;
            public double ModulatorBPhase;
            public double LowCarrierPhase;
            public double CarrierAPhase;
            public double CarrierBPhase;
            public double CarrierCPhase;
        }

        private struct SonarSynthesisState
        {
            public int ActiveSequence;
            public double AttackPhase;
            public double ChirpPhase;
            public double EchoPhase;
            public double TailSlowPhase;
            public double TailBeatAPhase;
            public double TailBeatBPhase;
            public double TailBeatCPhase;
            public float EchoFilterInput1;
            public float EchoFilterInput2;
            public float EchoFilterOutput1;
            public float EchoFilterOutput2;
        }

        private struct SonarReflectorDescriptor
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public Transform RootTransform;
            public float DistanceMeters;
            public bool IsDynamic;
        }

        private struct ThrusterSynthesisState
        {
            public double Hum1Phase;
            public double Hum2Phase;
            public double Hum3Phase;
            public double Hum4Phase;
            public double FlowPhase;
            public double PropCyclePhase;
        }

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
            EnsureSlowProbeBuffersAllocated();
            RebuildEnclosureProbeLayerMask();
            ResetEnclosureProbeState(math.max(1f, ceilingProbeDistance));
            RefreshAudioConfiguration();
            TryBindFromBootstrap();
        }

        private void OnEnable()
        {
            EnsureSlowProbeBuffersAllocated();
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
            ReleaseSlowProbeBuffers();
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
                _targetEnclosureVolume = ResolveEnclosureVolume(math.max(1f, ceilingProbeDistance));
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

            float defaultDistance = math.clamp(ceilingProbeDistance, 1f, MaximumProbeDistanceMeters);
            if (_boundPlayerTransform == null || playerMovement == null || !playerMovement.IsPlayerSubmerged)
            {
                ResetEnclosureProbeState(defaultDistance);
                return;
            }

            TryConsumeCompletedProbeSample(defaultDistance);
            ScheduleEnclosureProbe(defaultDistance);
            _probeAxisIndex = (_probeAxisIndex + 1) % _orthogonalProbeDistances.Length;
            UpdateApproximateEnclosureVolume();
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
                producerThread.Join();
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
                _hullScratch == null ||
                _sonarScratch == null ||
                _thrusterScratch == null ||
                _mixScratch == null ||
                _sampleRingBuffer == null)
            {
                return;
            }

            long blockStartFrame = _sampleRingBuffer.WriteFrameCursor;
            TryConsumePendingSonarTrigger(blockStartFrame, frameCount);

            double invSampleRate = 1d / math.max(1, _sampleRate);
            float hullTarget = math.saturate(_targetHullStressValue);
            float thrusterBlendTarget = math.saturate(_targetThrusterBlendValue);
            float thrusterLoadTarget = math.saturate(_targetThrusterLoadValue);
            float thrusterPitchTarget = math.max(0.1f, _targetThrusterPitchValue);
            float thrusterPressureTarget = math.saturate(_targetThrusterPressureValue);
            float thrusterAccelerationTarget = math.saturate(_targetThrusterAccelerationValue);
            float thrusterHeavyCarryTarget = math.saturate(_targetThrusterHeavyCarryValue);
            float thrusterDiveTarget = math.saturate(_targetThrusterDiveValue);

            RenderHullStressBlock(frameCount, blockStartFrame, invSampleRate, hullTarget);
            RenderSonarBlock(frameCount, blockStartFrame, invSampleRate);
            RenderThrusterBlock(
                frameCount,
                blockStartFrame,
                invSampleRate,
                thrusterBlendTarget,
                thrusterLoadTarget,
                thrusterPitchTarget,
                thrusterPressureTarget,
                thrusterAccelerationTarget,
                thrusterHeavyCarryTarget,
                thrusterDiveTarget);
            MixAndFilterBlock(frameCount);

            _sampleRingBuffer.TryWrite(_mixScratch, frameCount);
        }

        private void TryConsumePendingSonarTrigger(long blockStartFrame, int frameCount)
        {
            int activeIndex = Volatile.Read(ref _pendingSonarStateReadIndex);
            SonarTriggerState pendingState = activeIndex == 0 ? _pendingSonarStateA : _pendingSonarStateB;
            if (pendingState.Sequence == 0 || pendingState.Sequence == _workerConsumedSonarSequence)
                return;

            long blockEndFrameExclusive = blockStartFrame + frameCount;
            if (pendingState.StartFrame >= blockEndFrameExclusive)
                return;

            _workerConsumedSonarSequence = pendingState.Sequence;
            _workerActiveSonarState = pendingState;
            ResetSonarPhaseState(pendingState.Sequence);
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

            float defaultDistance = math.clamp(ceilingProbeDistance, 1f, MaximumProbeDistanceMeters);
            bool shouldUseWaterReverb = playerMovement != null && playerMovement.IsPlayerSubmerged;
            if (!shouldUseWaterReverb)
            {
                _targetCaveReverbBlend = 0f;
                _caveReverbBlend = 0f;
                _smoothedReverbDecayTime = openWaterDecayTime;
                ResetEnclosureProbeState(defaultDistance);
                RestoreListenerReverbDefaults();
                return;
            }

            float caveReferenceVolume = ResolveEnclosureVolume(math.max(1f, caveCeilingThreshold));
            float openWaterReferenceVolume = ResolveEnclosureVolume(defaultDistance);
            float reverbBlendT = 1f - math.exp(-math.max(caveReverbFollowSharpness, 0.01f) * deltaTime);
            _targetEnclosureVolume = math.clamp(_targetEnclosureVolume, 0.01f, openWaterReferenceVolume);
            _smoothedEnclosureVolume = math.lerp(_smoothedEnclosureVolume, _targetEnclosureVolume, reverbBlendT);
            float logVolume = math.log10(_smoothedEnclosureVolume + 1f);
            float logCave = math.log10(caveReferenceVolume + 1f);
            float logOpen = math.log10(openWaterReferenceVolume + 1f);
            float opennessT = math.saturate((logVolume - logCave) / math.max(logOpen - logCave, 0.0001f));
            float targetDecayTime = math.lerp(caveDecayTime, openWaterDecayTime, opennessT);
            _targetCaveReverbBlend = 1f - opennessT;
            _caveReverbBlend = math.lerp(_caveReverbBlend, _targetCaveReverbBlend, reverbBlendT);
            _smoothedReverbDecayTime = math.lerp(_smoothedReverbDecayTime, targetDecayTime, reverbBlendT);
            ApplyListenerReverbProfile(_caveReverbBlend, _smoothedReverbDecayTime);
        }

        private void TryConsumeCompletedProbeSample(float fallbackDistance)
        {
            if (!_slowProbeScheduled || !_slowProbeHandle.IsCompleted)
                return;

            _slowProbeHandle.Complete();
            RaycastHit hit = _slowProbeResults[0];
            float sampledDistance = hit.collider != null
                ? math.clamp(hit.distance, MinimumProbeDistanceMeters, fallbackDistance)
                : fallbackDistance;

            _orthogonalProbeDistances[_scheduledProbeAxisIndex] = sampledDistance;
            _slowProbeScheduled = false;
        }

        private void ScheduleEnclosureProbe(float fallbackDistance)
        {
            if (_boundPlayerTransform == null || !_slowProbeCommands.IsCreated || !_slowProbeResults.IsCreated || _slowProbeScheduled)
                return;

            Vector3 origin = _boundPlayerTransform.position + Vector3.up * 0.5f;
            Vector3 direction = ResolveProbeDirection(_probeAxisIndex);
            _slowProbeCommands[0] = new RaycastCommand(
                origin,
                direction,
                new QueryParameters(_resolvedEnclosureProbeLayerMask, false, QueryTriggerInteraction.Ignore),
                fallbackDistance);

            _slowProbeHandle = RaycastCommand.ScheduleBatch(_slowProbeCommands, _slowProbeResults, 1, default);
            _scheduledProbeAxisIndex = _probeAxisIndex;
            _slowProbeScheduled = true;
        }

        private void UpdateApproximateEnclosureVolume()
        {
            float dUp = math.max(_orthogonalProbeDistances[0], MinimumProbeDistanceMeters);
            float dDown = math.max(_orthogonalProbeDistances[1], MinimumProbeDistanceMeters);
            float dLeft = math.max(_orthogonalProbeDistances[2], MinimumProbeDistanceMeters);
            float dRight = math.max(_orthogonalProbeDistances[3], MinimumProbeDistanceMeters);
            float dForward = math.max(_orthogonalProbeDistances[4], MinimumProbeDistanceMeters);
            float dBack = math.max(_orthogonalProbeDistances[5], MinimumProbeDistanceMeters);

            _targetEnclosureVolume = math.max(
                MinimumProbeDistanceMeters,
                (dUp + dDown) * (dLeft + dRight) * (dForward + dBack));
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
            float defaultVolume = ResolveEnclosureVolume(clampedDistance);
            _targetEnclosureVolume = defaultVolume;
            _smoothedEnclosureVolume = defaultVolume;
            _smoothedReverbDecayTime = openWaterDecayTime;
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

        private void ApplyListenerReverbProfile(float caveBlend, float decayTime)
        {
            if (_listenerReverbFilter == null)
                return;

            _listenerReverbFilter.enabled = true;
            _listenerReverbFilter.reverbPreset = AudioReverbPreset.User;
            _listenerReverbFilter.decayTime = math.clamp(decayTime, 0.05f, 12f);
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
                out float echoAttenuation,
                out float echoLowPassCutoffHz);

            long consumerFrame = _sampleRingBuffer != null ? _sampleRingBuffer.ReadFrameCursor : 0L;
            long producerFrame = _sampleRingBuffer != null ? _sampleRingBuffer.WriteFrameCursor : consumerFrame;
            long scheduledStartFrame = math.max(producerFrame, consumerFrame);
            long scheduledLeadFrames = math.max(0L, scheduledStartFrame - consumerFrame);
            double scheduledDspTime = AudioSettings.dspTime + (scheduledLeadFrames / (double)math.max(_sampleRate, 1));

            SonarTriggerState pendingState = new SonarTriggerState
            {
                Sequence = Interlocked.Increment(ref _pendingSonarSequence),
                StartFrame = scheduledStartFrame,
                Intensity = math.saturate(intensity),
                EchoDelaySeconds = math.clamp(echoDelaySeconds, 0f, SonarEchoMaximumDelaySeconds),
                EchoDopplerRatio = math.clamp(echoDopplerRatio, 0.85f, 1.2f),
                EchoAttenuation = math.clamp(echoAttenuation, 0f, 1f),
                EchoLowPassCutoffHz = math.clamp(
                    echoLowPassCutoffHz,
                    AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                    AcousticOcclusionUtility.OpenLowPassCutoffHertz)
            };
            int inactiveIndex = 1 - Volatile.Read(ref _pendingSonarStateReadIndex);
            if (inactiveIndex == 0)
                _pendingSonarStateA = pendingState;
            else
                _pendingSonarStateB = pendingState;

            Interlocked.Exchange(ref _pendingSonarStateReadIndex, inactiveIndex);

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
            _hullScratch = new float[_frameCapacity]; // COLD ALLOC: float[frameCapacity] - hull-stress DSP scratch - owner: PlayerCriticalProceduralAudioRenderer
            _sonarScratch = new float[_frameCapacity]; // COLD ALLOC: float[frameCapacity] - sonar DSP scratch - owner: PlayerCriticalProceduralAudioRenderer
            _thrusterScratch = new float[_frameCapacity]; // COLD ALLOC: float[frameCapacity] - thruster DSP scratch - owner: PlayerCriticalProceduralAudioRenderer
            _mixScratch = new float[_frameCapacity]; // COLD ALLOC: float[frameCapacity] - mixed procedural audio worklet scratch - owner: PlayerCriticalProceduralAudioRenderer
            _sampleRingBuffer ??= new AudioFrameSpscRingBuffer();
            _sampleRingBuffer.Initialize(math.max(frameCapacity * 16, ringBufferCapacityFrames));
            _workerActiveSonarState = default;
            _workerConsumedSonarSequence = 0;
            ResetSonarPhaseState(0);
            _buffersInitialized = true;
        }

        private void DisposeBuffers()
        {
            _sampleRingBuffer?.Dispose();
            _sampleRingBuffer = null;
            _hullScratch = null;
            _sonarScratch = null;
            _thrusterScratch = null;
            _mixScratch = null;

            _buffersInitialized = false;
            _frameCapacity = 0;
        }

        private void EnsureSlowProbeBuffersAllocated()
        {
            if (!_slowProbeCommands.IsCreated)
            {
                _slowProbeCommands = new NativeArray<RaycastCommand>(1, Allocator.Persistent);
            }

            if (!_slowProbeResults.IsCreated)
            {
                _slowProbeResults = new NativeArray<RaycastHit>(1, Allocator.Persistent);
            }
        }

        private void ReleaseSlowProbeBuffers()
        {
            if (_slowProbeCommands.IsCreated)
            {
                if (_slowProbeScheduled)
                    _slowProbeCommands.Dispose(_slowProbeHandle);
                else
                    _slowProbeCommands.Dispose();
            }

            if (_slowProbeResults.IsCreated)
            {
                if (_slowProbeScheduled)
                    _slowProbeResults.Dispose(_slowProbeHandle);
                else
                    _slowProbeResults.Dispose();
            }

            _slowProbeCommands = default;
            _slowProbeResults = default;
            _slowProbeHandle = default;
            _slowProbeScheduled = false;
        }

        private void ClearLowPassState()
        {
            Array.Clear(_lowPassInputHistory1, 0, _lowPassInputHistory1.Length);
            Array.Clear(_lowPassInputHistory2, 0, _lowPassInputHistory2.Length);
            Array.Clear(_lowPassOutputHistory1, 0, _lowPassOutputHistory1.Length);
            Array.Clear(_lowPassOutputHistory2, 0, _lowPassOutputHistory2.Length);
            _audioAbyssalLowPassMix = 0f;
            _pendingSonarSequence = 0;
            _pendingSonarStateReadIndex = 0;
            _pendingSonarStateA = default;
            _pendingSonarStateB = default;
            _workerActiveSonarState = default;
            _workerConsumedSonarSequence = 0;
            _hullSynthesisState = default;
            _thrusterSynthesisState = default;
            _scheduledProbeAxisIndex = 0;
            _smoothedReverbDecayTime = openWaterDecayTime;
            ResetSonarPhaseState(0);
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
            out float echoAttenuation,
            out float echoLowPassCutoffHz)
        {
            echoDelaySeconds = 0.24f;
            echoDopplerRatio = 1f;
            echoAttenuation = 0.42f;
            echoLowPassCutoffHz = AcousticOcclusionUtility.OpenLowPassCutoffHertz;

            if (_boundPlayerObject == null || _playerRigidbody == null)
                return;

            Vector3 playerPosition = _boundPlayerObject.transform.position;
            if (!TryResolveNearestSonarReflector(playerPosition, out SonarReflectorDescriptor reflector))
                return;

            Vector3 toReflector = reflector.Position - playerPosition;
            float distance = math.max(MinimumProbeDistanceMeters, math.length(toReflector));
            Vector3 reflectorDirection = toReflector / distance;

            float distanceMeters = math.min(reflector.DistanceMeters, SonarEchoMaximumDistanceMeters);
            echoDelaySeconds = math.min((distanceMeters * 2f) / SoundSpeedWaterMetersPerSecond, SonarEchoMaximumDelaySeconds);

            if (reflector.IsDynamic)
            {
                Vector3 sourceToListener = playerPosition - reflector.Position;
                float sourceDistance = math.max(MinimumProbeDistanceMeters, math.length(sourceToListener));
                Vector3 listenerDirection = sourceToListener / sourceDistance;
                float sourceRadialVelocity = Vector3.Dot(reflector.Velocity, listenerDirection);
                float listenerRadialVelocity = Vector3.Dot(_playerRigidbody.linearVelocity, -listenerDirection);
                float dopplerDenominator = SoundSpeedWaterMetersPerSecond + sourceRadialVelocity;
                if (math.abs(dopplerDenominator) < MinimumProbeDistanceMeters)
                    dopplerDenominator = dopplerDenominator >= 0f ? MinimumProbeDistanceMeters : -MinimumProbeDistanceMeters;

                echoDopplerRatio = math.clamp(
                    (SoundSpeedWaterMetersPerSecond + listenerRadialVelocity) / dopplerDenominator,
                    0.85f,
                    1.2f);
            }
            else
            {
                float radialVelocity = Vector3.Dot(_playerRigidbody.linearVelocity, reflectorDirection);
                float clampedRadialVelocity = math.clamp(
                    radialVelocity,
                    -SoundSpeedWaterMetersPerSecond * 0.9f,
                    SoundSpeedWaterMetersPerSecond * 0.9f);
                float stationaryDenominator = math.max(MinimumProbeDistanceMeters, SoundSpeedWaterMetersPerSecond - clampedRadialVelocity);
                echoDopplerRatio = math.clamp(
                    (SoundSpeedWaterMetersPerSecond + clampedRadialVelocity) / stationaryDenominator,
                    0.85f,
                    1.2f);
            }

            float transmissionLossDb =
                (20f * math.log10(math.max(distanceMeters, MinimumProbeDistanceMeters))) +
                (SonarEchoAbsorptionCoefficient * distanceMeters);
            echoAttenuation = math.clamp(
                math.pow(10f, -transmissionLossDb / 20f) *
                (SonarEchoReferenceDistanceMeters / (SonarEchoReferenceDistanceMeters + distanceMeters)),
                0f,
                0.95f);

            AcousticOcclusionResult occlusion = AcousticOcclusionUtility.EvaluateOcclusionPath(
                playerPosition,
                reflector.Position,
                _resolvedAcousticOcclusionLayerMask,
                _sonarOcclusionHits,
                _boundPlayerTransform != null ? _boundPlayerTransform.root : null,
                reflector.RootTransform);
            echoAttenuation = math.clamp(echoAttenuation * occlusion.Transmission01, 0f, 0.95f);
            echoLowPassCutoffHz = occlusion.LowPassCutoffHz;
        }

        private bool TryResolveNearestSonarReflector(Vector3 playerPosition, out SonarReflectorDescriptor reflector)
        {
            reflector = default;
            reflector.DistanceMeters = float.MaxValue;

            AccumulateNearestDynamicSonarReflector(playerPosition, ref reflector);

            HectonMapMagicVegetationBridge vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (vegetationBridge == null)
                return reflector.DistanceMeters < float.MaxValue;

            if (vegetationBridge.TryGetActiveAbyssalAnchorPayload(out NativeArray<Vector3> anchors, out int anchorCount))
                AccumulateNearestPayloadPoint(playerPosition, anchors, anchorCount, ref reflector);

            if (vegetationBridge.TryGetActiveAbyssalNavNodePayload(out NativeArray<Vector3> nodes, out int nodeCount))
                AccumulateNearestPayloadPoint(playerPosition, nodes, nodeCount, ref reflector);

            return reflector.DistanceMeters < float.MaxValue;
        }

        private void AccumulateNearestDynamicSonarReflector(Vector3 playerPosition, ref SonarReflectorDescriptor nearestReflector)
        {
            int count = WorldSpatialHashGrid.CollectContactsNonAlloc(
                playerPosition,
                80f,
                SpatialTargetKind.Bioform,
                _dynamicSonarReflectorBuffer);

            for (int i = 0; i < count; i++)
            {
                SpatialQueryHit candidate = _dynamicSonarReflectorBuffer[i];
                if (!(candidate.Owner is FaunaBrain brain) || candidate.Transform == null)
                    continue;

                if (!brain.TryGetComponent(out Rigidbody targetBody))
                    continue;

                float candidateDistance = math.sqrt(candidate.DistanceSqr);
                if (candidateDistance >= nearestReflector.DistanceMeters)
                    continue;

                nearestReflector = new SonarReflectorDescriptor
                {
                    Position = candidate.Position,
                    Velocity = targetBody.linearVelocity,
                    RootTransform = candidate.Transform.root,
                    DistanceMeters = candidateDistance,
                    IsDynamic = true
                };
            }
        }

        private static void AccumulateNearestPayloadPoint(
            Vector3 playerPosition,
            NativeArray<Vector3> points,
            int count,
            ref SonarReflectorDescriptor nearestReflector)
        {
            int safeCount = math.min(count, points.Length);
            for (int i = 0; i < safeCount; i++)
            {
                Vector3 candidate = points[i];
                float candidateDistance = math.distance(playerPosition, candidate);
                if (candidateDistance >= nearestReflector.DistanceMeters)
                    continue;

                nearestReflector = new SonarReflectorDescriptor
                {
                    Position = candidate,
                    Velocity = Vector3.zero,
                    RootTransform = null,
                    DistanceMeters = candidateDistance,
                    IsDynamic = false
                };
            }
        }

        private void MixAndFilterBlock(int frameCount)
        {
            float targetMix = math.saturate(_targetAbyssalLowPassMix);
            float startMix = _audioAbyssalLowPassMix;
            float endMix = targetMix;
            bool shouldFilter = targetMix > 0.0001f || startMix > 0.0001f;

            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                float mixed =
                    (_hullScratch[frameIndex] +
                     _sonarScratch[frameIndex] +
                     _thrusterScratch[frameIndex]) * outputHeadroom;

                if (shouldFilter)
                {
                    float frameT = frameCount > 1 ? frameIndex / (float)(frameCount - 1) : 1f;
                    float mix = math.lerp(startMix, endMix, frameT);
                    float cutoff = math.lerp(_sampleRate * 0.45f, AbyssalLowPassCutoffHertz, mix);
                    ComputeLowPassCoefficients(cutoff, out float b0, out float b1, out float b2, out float a1, out float a2);

                    float outputHistory1 = _lowPassOutputHistory1[0] + BiquadDenormalBias;
                    float outputHistory2 = _lowPassOutputHistory2[0] + BiquadDenormalBias;
                    float filtered =
                        b0 * mixed +
                        b1 * _lowPassInputHistory1[0] +
                        b2 * _lowPassInputHistory2[0] -
                        a1 * outputHistory1 -
                        a2 * outputHistory2;

                    _lowPassInputHistory2[0] = _lowPassInputHistory1[0];
                    _lowPassInputHistory1[0] = mixed;
                    _lowPassOutputHistory2[0] = _lowPassOutputHistory1[0];
                    _lowPassOutputHistory1[0] = filtered;
                    mixed = math.lerp(mixed, filtered, mix);
                }

                _mixScratch[frameIndex] = mixed;
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

        private void ResetSonarPhaseState(int activeSequence)
        {
            _sonarSynthesisState = new SonarSynthesisState
            {
                ActiveSequence = activeSequence
            };
        }

        private void RebuildEnclosureProbeLayerMask()
        {
            int mask = enclosureProbeLayers.value;
            mask &= ~LayerBit(PlayerLayer);
            mask &= ~LayerBit(VehicleLayer);
            mask &= ~LayerBit(BaseModuleLayer);
            mask &= ~LayerBit(TriggerZoneLayer);
            mask &= ~LayerBit(TransparentFxLayer);
            mask &= ~LayerBit(FirstPersonToolsLayer);
            _resolvedEnclosureProbeLayerMask = mask;
            _resolvedAcousticOcclusionLayerMask = AcousticOcclusionUtility.BuildSensoryMask() & enclosureProbeLayers.value;
        }

        private static int LayerBit(int layer)
        {
            return layer >= 0 ? 1 << layer : 0;
        }

        private static float ResolveEnclosureVolume(float probeDistance)
        {
            float span = math.max(1f, probeDistance) * 2f;
            return math.max(0.01f, span * span * span);
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

        private void RenderHullStressBlock(int frameCount, long blockStartFrame, double invSampleRate, float hullTarget)
        {
            HullSynthesisState state = _hullSynthesisState;
            float stressStart = _audioHullStressValue;

            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                float frameT = frameCount > 1 ? frameIndex / (float)(frameCount - 1) : 0f;
                float stress = math.lerp(stressStart, hullTarget, frameT);
                long sampleFrame = blockStartFrame + frameIndex;
                uint sampleIndex = (uint)math.max(0L, sampleFrame);

                float pressureLfo = 0.6f + 0.4f * AdvanceSine(ref state.PressureLfoPhase, 0.3d, invSampleRate);
                float pressureBed =
                    (LayeredBrownLike(sampleIndex) * pressureLfo * hullPressureBedAmount) * math.sqrt(math.max(stress, 0f));

                float carrierA = math.lerp(120f, 800f, math.pow(stress, 0.82f));
                float carrierB = carrierA * 1.72f;
                float carrierC = carrierA * 2.43f;
                float stickSlip =
                    0.72f +
                    0.28f * AdvanceSine(ref state.StickSlipPhase, math.lerp(22f, 43f, stress), invSampleRate) *
                    (0.7f + 0.3f * HeldNoise(sampleIndex, 5, 0x18273645u));
                float frictionNoiseOperator =
                    HeldNoise(sampleIndex, 3, 0x7124AB11u) * 0.62f +
                    HeldNoise(sampleIndex, 5, 0x31DF19A3u) * 0.38f;
                float groanEnvelope = math.pow(0.5f + 0.5f * AdvanceSine(ref state.GroanEnvelopePhase, 0.22d, invSampleRate), 4f);
                float modIndex = (1.8f + 6.2f * stress) * stickSlip;
                float modulatorA = AdvanceSine(ref state.ModulatorAPhase, math.lerp(45f, 97f, stress), invSampleRate);
                float modulatorB = AdvanceSine(ref state.ModulatorBPhase, math.lerp(87f, 133f, stress * 0.8f), invSampleRate);

                AdvancePhase(ref state.LowCarrierPhase, 80d, invSampleRate);
                float lowCarrierFm =
                    math.sin((float)(TwoPi * state.LowCarrierPhase) + frictionNoiseOperator * (0.4f + 3.6f * stress) * stickSlip) *
                    (0.18f + 0.26f * stress);

                AdvancePhase(ref state.CarrierAPhase, carrierA, invSampleRate);
                AdvancePhase(ref state.CarrierBPhase, carrierB, invSampleRate);
                AdvancePhase(ref state.CarrierCPhase, carrierC, invSampleRate);
                float metal =
                    math.sin((float)(TwoPi * state.CarrierAPhase) + modIndex * modulatorA) * 0.54f +
                    math.sin((float)(TwoPi * state.CarrierBPhase) + modIndex * 0.62f * modulatorB) * 0.29f +
                    math.sin((float)(TwoPi * state.CarrierCPhase) + modIndex * 0.35f * modulatorA) * 0.17f;
                metal = (metal + lowCarrierFm) * groanEnvelope * math.lerp(0.25f, 1f, stress);

                float rivetBurst = BuildRivetBurst(sampleIndex, stress, hullRivetBurstAmount);
                float combined = pressureBed + metal + rivetBurst;
                _hullScratch[frameIndex] = stress <= HullNoiseFloor
                    ? 0f
                    : math.tanh(combined * 1.7f) * hullMasterGain;
            }

            _hullSynthesisState = state;
            _audioHullStressValue = hullTarget;
        }

        private void RenderSonarBlock(int frameCount, long blockStartFrame, double invSampleRate)
        {
            SonarTriggerState activeState = _workerActiveSonarState;
            if (activeState.Sequence == 0 || activeState.Intensity <= 0f)
            {
                Array.Clear(_sonarScratch, 0, frameCount);
                return;
            }

            SonarSynthesisState state = _sonarSynthesisState;
            if (state.ActiveSequence != activeState.Sequence)
            {
                ResetSonarPhaseState(activeState.Sequence);
                state = _sonarSynthesisState;
            }

            bool shouldLowPassEcho =
                activeState.EchoLowPassCutoffHz <
                math.min(AcousticOcclusionUtility.OpenLowPassCutoffHertz, _sampleRate * 0.45f) - 1f;
            float echoB0 = 0f;
            float echoB1 = 0f;
            float echoB2 = 0f;
            float echoA1 = 0f;
            float echoA2 = 0f;
            if (shouldLowPassEcho)
            {
                ComputeLowPassCoefficients(
                    activeState.EchoLowPassCutoffHz,
                    out echoB0,
                    out echoB1,
                    out echoB2,
                    out echoA1,
                    out echoA2);
            }

            long maxActiveFrame = activeState.StartFrame + (long)math.ceil(SonarTotalDurationSeconds * math.max(_sampleRate, 1));
            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                long sampleFrame = blockStartFrame + frameIndex;
                float age = (float)((sampleFrame - activeState.StartFrame) * invSampleRate);
                if (age < 0f || age > SonarTotalDurationSeconds)
                {
                    _sonarScratch[frameIndex] = 0f;
                    continue;
                }

                uint sampleIndex = (uint)math.max(0L, sampleFrame);
                float attack = 0f;
                if (age < 0.03f)
                {
                    float attackEnv = math.exp(-age * 220f);
                    float attackNoise = HashSigned(sampleIndex ^ 0x3941AA1u);
                    attack = attackEnv * (AdvanceSine(ref state.AttackPhase, 4500d, invSampleRate) + attackNoise * 0.85f) * sonarAttackBlend;
                }

                float chirp = 0f;
                if (age < SonarChirpDurationSeconds)
                {
                    float chirpT = math.saturate(age / SonarChirpDurationSeconds);
                    float chirpFrequency = math.lerp(2000f, 400f, chirpT);
                    float chirpEnv = math.exp(-age * 5f);
                    chirp = chirpEnv * AdvanceSine(ref state.ChirpPhase, chirpFrequency, invSampleRate);
                }

                float echo = 0f;
                float echoAge = age - activeState.EchoDelaySeconds;
                if (echoAge >= 0f && echoAge < SonarChirpDurationSeconds)
                {
                    float echoT = math.saturate(echoAge / SonarChirpDurationSeconds);
                    float echoFrequency = math.lerp(
                        2000f * activeState.EchoDopplerRatio,
                        400f * activeState.EchoDopplerRatio,
                        echoT);
                    float echoEnvelope = math.exp(-echoAge * 4.5f) * activeState.EchoAttenuation;
                    echo = AdvanceSine(ref state.EchoPhase, echoFrequency, invSampleRate) * echoEnvelope;
                }

                if (shouldLowPassEcho)
                {
                    float filteredEcho =
                        echoB0 * echo +
                        echoB1 * state.EchoFilterInput1 +
                        echoB2 * state.EchoFilterInput2 -
                        echoA1 * (state.EchoFilterOutput1 + BiquadDenormalBias) -
                        echoA2 * (state.EchoFilterOutput2 + BiquadDenormalBias);

                    state.EchoFilterInput2 = state.EchoFilterInput1;
                    state.EchoFilterInput1 = echo;
                    state.EchoFilterOutput2 = state.EchoFilterOutput1;
                    state.EchoFilterOutput1 = filteredEcho;
                    echo = filteredEcho;
                }

                float tail = 0f;
                if (age >= 0.08f)
                {
                    float tailAge = age - 0.08f;
                    float tailEnv = math.saturate(tailAge / 0.24f) * math.exp(-tailAge * 0.95f);
                    float slowLfo = 0.55f + 0.45f * AdvanceSine(ref state.TailSlowPhase, 0.38d, invSampleRate);
                    float beat =
                        AdvanceSine(ref state.TailBeatAPhase, 150d, invSampleRate) +
                        AdvanceSine(ref state.TailBeatBPhase, 147d, invSampleRate) * 0.6f +
                        AdvanceSine(ref state.TailBeatCPhase, 300d, invSampleRate) * 0.4f;
                    float pinkTail = LayeredPinkLike(sampleIndex) * slowLfo;
                    tail = tailEnv * ((beat * 0.46f) + (pinkTail * 0.54f)) * sonarTailBlend;
                }

                float mixed = (attack + chirp + echo + tail) * activeState.Intensity;
                _sonarScratch[frameIndex] = math.tanh(mixed * sonarSaturationDrive) * sonarMasterGain;
            }

            if (blockStartFrame >= maxActiveFrame)
                _workerActiveSonarState = default;

            _sonarSynthesisState = state;
        }

        private void RenderThrusterBlock(
            int frameCount,
            long blockStartFrame,
            double invSampleRate,
            float thrusterBlendTarget,
            float thrusterLoadTarget,
            float thrusterPitchTarget,
            float thrusterPressureTarget,
            float thrusterAccelerationTarget,
            float thrusterHeavyCarryTarget,
            float thrusterDiveTarget)
        {
            ThrusterSynthesisState state = _thrusterSynthesisState;
            float blendStart = _audioThrusterBlendValue;
            float loadStart = _audioThrusterLoadValue;
            float pitchStart = _audioThrusterPitchValue;
            float pressureStart = _audioThrusterPressureValue;
            float accelerationStart = _audioThrusterAccelerationValue;
            float heavyCarryStart = _audioThrusterHeavyCarryValue;
            float diveStart = _audioThrusterDiveValue;

            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                float frameT = frameCount > 1 ? frameIndex / (float)(frameCount - 1) : 0f;
                float blend = math.lerp(blendStart, thrusterBlendTarget, frameT);
                float load = math.lerp(loadStart, thrusterLoadTarget, frameT);
                float pitchScale = math.lerp(pitchStart, thrusterPitchTarget, frameT);
                float pressure = math.lerp(pressureStart, thrusterPressureTarget, frameT);
                float acceleration = math.lerp(accelerationStart, thrusterAccelerationTarget, frameT);
                float heavyCarry = math.lerp(heavyCarryStart, thrusterHeavyCarryTarget, frameT);
                float dive = math.lerp(diveStart, thrusterDiveTarget, frameT);
                long sampleFrame = blockStartFrame + frameIndex;
                uint sampleIndex = (uint)math.max(0L, sampleFrame);

                float hum =
                    AdvanceSine(ref state.Hum1Phase, 80d * pitchScale, invSampleRate) * 1.00f +
                    AdvanceSine(ref state.Hum2Phase, 160d * pitchScale, invSampleRate) * 0.60f +
                    AdvanceSine(ref state.Hum3Phase, 240d * pitchScale, invSampleRate) * 0.35f +
                    AdvanceSine(ref state.Hum4Phase, 320d * pitchScale, invSampleRate) * 0.15f;
                hum *= 0.42f;

                float flowMod = 0.55f + 0.45f * AdvanceSine(ref state.FlowPhase, 0.31d, invSampleRate);
                float flowNoise = LayeredPinkLike(sampleIndex ^ 0xCAFEBABEu);
                float flow = flowNoise * flowMod * (0.18f + 0.22f * load + 0.08f * heavyCarry);

                float propCycle = 0.5f + 0.5f * AdvanceSine(ref state.PropCyclePhase, 20d, invSampleRate);
                float envelopeSharpness = math.lerp(5f, 0.5f, math.saturate(load + acceleration * 0.35f));
                float dynamicEnvelope = math.pow(math.saturate(propCycle), envelopeSharpness);
                float highNoise = HighBandNoise(sampleIndex);
                float cavitation = highNoise * highNoise * highNoise;
                cavitation *= dynamicEnvelope * math.saturate(load * 1.2f + pressure * 0.75f + acceleration * 0.55f + dive * 0.2f);

                float mixed = hum + flow + cavitation * 0.78f;
                _thrusterScratch[frameIndex] = math.tanh(mixed * 2.0f) * thrusterMasterGain * blend;
            }

            _thrusterSynthesisState = state;
            _audioThrusterBlendValue = thrusterBlendTarget;
            _audioThrusterLoadValue = thrusterLoadTarget;
            _audioThrusterPitchValue = thrusterPitchTarget;
            _audioThrusterPressureValue = thrusterPressureTarget;
            _audioThrusterAccelerationValue = thrusterAccelerationTarget;
            _audioThrusterHeavyCarryValue = thrusterHeavyCarryTarget;
            _audioThrusterDiveValue = thrusterDiveTarget;
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

        private static double AdvancePhase(ref double phase, double frequencyHz, double invSampleRate)
        {
            phase += frequencyHz * invSampleRate;
            phase -= math.floor(phase);
            return phase;
        }

        private static float AdvanceSine(ref double phase, double frequencyHz, double invSampleRate)
        {
            return math.sin((float)(TwoPi * AdvancePhase(ref phase, frequencyHz, invSampleRate)));
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

#if UNITY_EDITOR
        private void OnValidate()
        {
            RebuildEnclosureProbeLayerMask();
        }
#endif
    }
}
