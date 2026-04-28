using System;
using System.Threading;
using Hecton8.AI;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Physics;
using Hecton8.Visor;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Audio;
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
    public sealed class PlayerCriticalProceduralAudioRenderer : MonoBehaviour, ITickable, ISlowTickable, IUpdatable
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
        private const float MinimumMixerWetMixDb = -80f;
        private const float MinimumFilterWetMixDb = -10000f;
        private const float BiquadDenormalBias = 1e-15f;
        private const float PressureCreakDepthReferenceMeters = 4000f;
        private const float StructuralBreachAreaReferenceSquareMeters = 12f;
        private const float StructuralStressFollowSharpness = 4.5f;
        private const float PressureCreakMinimumEventsPerSecond = 0.2f;
        private const float PressureCreakMaximumEventsPerSecond = 1.0f;
        private const float PressureCreakAttackSeconds = 0.004f;
        private const float PressureCreakDecaySeconds = 0.018f;
        private const float PressureCreakSustainSeconds = 0.024f;
        private const float PressureCreakReleaseSeconds = 0.052f;
        private const float PressureCreakBandPassQ = 1.35f;
        private const float PressureCreakDerivativeReferencePerSecond = 1.6f;
        private const float PressureCreakDerivativeDensityBoostPerSecond = 2.25f;
        private const float PressureCreakDerivativePitchBoost = 1.35f;
        private const float PressureCreakMinimumPlaybackRate = 0.58f;
        private const float PressureCreakMaximumPlaybackRate = 1.95f;
        private const float PressureCreakMinimumBandCenterHertz = 96f;
        private const float PressureCreakMaximumBandCenterHertz = 1840f;
        private const int MetallicGrainBankCapacity = 8192;
        private const int MetallicGrainBankMask = MetallicGrainBankCapacity - 1;
        private const float HullSubBassMinimumHertz = 25f;
        private const float HullSubBassMaximumHertz = 40f;
        private const float HullSubBassMaximumGain = 0.22f;
        private const float ThrusterBandPassQ = 0.82f;
        private const float ThrusterBladePassFrequencyMinHertz = 22f;
        private const float ThrusterBladePassFrequencyMaxHertz = 116f;
        private const float ThrusterCombDamp = 0.22f;
        private const int MaxSafeFrameCapacity = 16384;
        private const int MaxFilterChannels = 8;
        private const int MaxDynamicSonarReflectorCount = 24;
        private const int AudioProducerJoinTimeoutMs = 250;
        private const int SonarEchoDelayCapacity = 131072;
        private const int SonarEchoDelayMask = SonarEchoDelayCapacity - 1;
        private const int ThrusterCombDelayCapacity = 4096;
        private const int ThrusterCombDelayMask = ThrusterCombDelayCapacity - 1;
        private const int ImpactEventQueueCapacity = 64;
        private const int ImpactEventQueueMask = ImpactEventQueueCapacity - 1;
        private const int ImpactEventQueueSpinWatchdog = 50000;
        private const float PhysicsImpactStressRadiusMeters = 18f;
        private const float PhysicsImpactStressDecayPerSecond = 1.65f;
        private const float PhysicsImpactMetallicDecayPerSecond = 2.4f;
        private const float PhysicsImpactStressBoost = 0.55f;
        // Rescue path: route procedural output through the listener filter until the native mixer effect is proven healthy.
        private const bool EnableNativeMixerKernel = false;

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
        [SerializeField, Range(0.2f, 20f)] private float openWaterDecayTime = 10f;

        [Tooltip("Decay time used when the player is under a close cave ceiling.")]
        [SerializeField, Range(0.1f, 10f)] private float caveDecayTime = 1.6f;

        [Tooltip("Early reflection level for the open-water reverb profile.")]
        [SerializeField, Range(-10000f, 1000f)] private float openWaterReflectionsLevel = -2200f;

        [Tooltip("Early reflection level for the cave reverb profile.")]
        [SerializeField, Range(-10000f, 1000f)] private float caveReflectionsLevel = 120f;

        [Tooltip("High-frequency room attenuation in open water so the tail stays present instead of cave-muffled.")]
        [SerializeField, Range(-10000f, 0f)] private float openWaterRoomHighFrequency = -1800f;

        [Tooltip("High-frequency room attenuation under a close cave ceiling.")]
        [SerializeField, Range(-10000f, 0f)] private float caveRoomHighFrequency = -5200f;

        [Header("Spatial Reverb Mixer Routing")]
        [Tooltip("Optional AudioMixer used to drive cave/open-water reverb without mutating AudioReverbFilter every frame.")]
        [SerializeField] private AudioMixer reverbControlMixer;

        [Tooltip("Exposed AudioMixer parameter for reverb decay time.")]
        [SerializeField] private string reverbDecayTimeParameter = "PlayerCriticalReverbDecayTime";

        [Tooltip("Exposed AudioMixer parameter for reflections level.")]
        [SerializeField] private string reverbReflectionsLevelParameter = "PlayerCriticalReverbReflectionsLevelDb";

        [Tooltip("Exposed AudioMixer parameter for room high-frequency attenuation.")]
        [SerializeField] private string reverbRoomHighFrequencyParameter = "PlayerCriticalRoomHighFrequencyDb";

        [Tooltip("Optional exposed AudioMixer parameter for Sabine-driven wet mix in decibels.")]
        [SerializeField] private string reverbWetMixParameter = "PlayerCriticalReverbWetMixDb";

        // COLD ALLOC: NativeArray<float>[frameCapacity] - hull-stress DSP scratch - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<float> _hullScratch;
        // COLD ALLOC: NativeArray<float>[frameCapacity] - sonar DSP scratch - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<float> _sonarScratch;
        // COLD ALLOC: NativeArray<float>[frameCapacity] - thruster DSP scratch - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<float> _thrusterScratch;
        // COLD ALLOC: NativeArray<float>[frameCapacity] - mixed procedural audio worklet scratch - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<float> _mixScratch;
        // COLD ALLOC: NativeArray<float>[131072] - sonar Hermite echo delay ring - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<float> _sonarEchoDelay;
        // COLD ALLOC: NativeArray<float>[4096] - thruster comb filter delay ring - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<float> _thrusterCombDelay;
        // COLD ALLOC: NativeArray<float>[8] - final listener low-pass state x1 - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<float> _lowPassInputHistory1;
        // COLD ALLOC: NativeArray<float>[8] - final listener low-pass state x2 - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<float> _lowPassInputHistory2;
        // COLD ALLOC: NativeArray<float>[8] - final listener low-pass state y1 - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<float> _lowPassOutputHistory1;
        // COLD ALLOC: NativeArray<float>[8] - final listener low-pass state y2 - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<float> _lowPassOutputHistory2;
        // COLD ALLOC: NativeArray<float>[8192] - pre-baked metallic screech grain bank for hull granular synthesis - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<float> _metallicGrainBank;
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
        private ISubmarineHullBreachReadModel _structuralHullReadModel;
        private IPlayerTransportLifecycleOwner _activeTransportLifecycleOwner;
        private AudioReverbFilter _listenerReverbFilter;
        private bool _reverbMixerBindingsResolved;
        private bool _reverbMixerBindingsValid;
        private bool _reverbMixerWetBindingValid;
        private bool _warnedMissingReverbMixerParameters;
        private bool _warnedMissingReverbWetMixerParameter;
        private string _resolvedReverbDecayTimeParameter;
        private string _resolvedReverbReflectionsLevelParameter;
        private string _resolvedReverbRoomHighFrequencyParameter;
        private string _resolvedReverbWetMixParameter;
        private PlayerTransportFeelContract _transportFeelContractCurrent;
        private float _lastSpeed;
        private float _hullStressTickValue;
        private float _structuralHullStressTickValue;
        private float _structuralHullStressVelocityTickValue;
        private float _impactStressImpulseTickValue;
        private float _hullPressureDepthTickValue;
        private float _thrusterBlendTickValue;
        private float _thrusterLoadTickValue;
        private float _thrusterPitchTickValue = 1f;
        private float _thrusterPressureTickValue;
        private float _thrusterAccelerationTickValue;
        private float _thrusterHeavyCarryTickValue;
        private float _thrusterDiveTickValue;
        private float _audioHullStressValue;
        private float _audioStructuralHullStressValue;
        private float _audioStructuralHullStressVelocityValue;
        private float _audioHullPressureDepthValue;
        private float _audioImpactStressValue;
        private float _audioImpactMetallicValue;
        private float _audioThrusterBlendValue;
        private float _audioThrusterLoadValue;
        private float _audioThrusterPitchValue = 1f;
        private float _audioThrusterPressureValue;
        private float _audioThrusterAccelerationValue;
        private float _audioThrusterHeavyCarryValue;
        private float _audioThrusterDiveValue;
        private float _audioAbyssalLowPassMix;
        private float _smoothedReverbDecayTime;
        private float _smoothedReverbWetMix;
        private float _smoothedReverbOpenness = 1f;
        private int _audioProducerRunning;
        private int _resolvedAcousticOcclusionLayerMask;
        private bool _listenerReverbDefaultsCaptured;
        private bool _listenerReverbWasEnabled;
        private AudioReverbPreset _listenerReverbBasePreset = AudioReverbPreset.Off;
        private float _listenerReverbBaseDecayTime = 1f;
        private float _listenerReverbBaseReflectionsLevel = -10000f;
        private float _listenerReverbBaseRoomHighFrequency = -10000f;
        private float _listenerReverbBaseReverbLevel = MinimumFilterWetMixDb;
        private float _mixerReverbBaseDecayTime;
        private float _mixerReverbBaseReflectionsLevel;
        private float _mixerReverbBaseRoomHighFrequency;
        private float _mixerReverbBaseWetMixDb = MinimumMixerWetMixDb;
        private bool _mixerReverbDefaultsCaptured;
        private int _pendingSonarStateReadIndex;
        private int _pendingSonarSequence;
        private int _impactEventReadIndex;
        private int _impactEventWriteIndex;
        private int _workerConsumedSonarSequence;
        private SonarTriggerState _pendingSonarStateA;
        private SonarTriggerState _pendingSonarStateB;
        private SonarTriggerState _workerActiveSonarState;
        private HullSynthesisState _hullSynthesisState;
        private SonarSynthesisState _sonarSynthesisState;
        private ThrusterSynthesisState _thrusterSynthesisState;
        private long _producedSampleCount;
        private bool _nativeOutputRegistered;
        private bool _nativeOutputBridgeFailureLogged;
        private int _managedFilterFallbackEnabled;
        private ulong _playerBodyEntityId;

        private volatile float _targetHullStressValue;
        private volatile float _targetStructuralHullStressValue;
        private volatile float _targetStructuralHullStressVelocityValue;
        private volatile float _targetHullPressureDepthValue;
        private volatile float _targetThrusterBlendValue;
        private volatile float _targetThrusterLoadValue;
        private volatile float _targetThrusterPitchValue = 1f;
        private volatile float _targetThrusterPressureValue;
        private volatile float _targetThrusterAccelerationValue;
        private volatile float _targetThrusterHeavyCarryValue;
        private volatile float _targetThrusterDiveValue;
        private volatile float _targetAbyssalLowPassMix;

        // COLD ALLOC: ImpactAudioEvent[64] - main-thread physics impact bridge for the audio worker SPSC path - owner: PlayerCriticalProceduralAudioRenderer
        private readonly ImpactAudioEvent[] _impactEventQueue = new ImpactAudioEvent[ImpactEventQueueCapacity];
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

        private struct ImpactAudioEvent
        {
            public float Stress;
            public float Metallic;
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
            public int GrainElapsedSamples;
            public int GrainTotalSamples;
            public int GrainAttackSamples;
            public int GrainDecaySamples;
            public int GrainSustainSamples;
            public int GrainReleaseSamples;
            public float GrainSustainLevel;
            public float GrainGain;
            public float GrainPlaybackRate;
            public float GrainDerivative;
            public uint GrainNoiseSeed;
            public int GrainLoopStartIndex;
            public int GrainLoopLength;
            public double GrainReadCursor;
            public float GrainBandPassInput1;
            public float GrainBandPassInput2;
            public float GrainBandPassOutput1;
            public float GrainBandPassOutput2;
            public float GrainBandPassB0;
            public float GrainBandPassB1;
            public float GrainBandPassB2;
            public float GrainBandPassA1;
            public float GrainBandPassA2;
            public double SubBassPhase;
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
            public double EchoReadCursor;
            public int EchoWriteIndex;
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
            public float PinkB0;
            public float PinkB1;
            public float PinkB2;
            public float PinkB3;
            public float PinkB4;
            public float PinkB5;
            public float PinkB6;
            public float BandPassInput1;
            public float BandPassInput2;
            public float BandPassOutput1;
            public float BandPassOutput2;
            public float CombFeedbackSample;
            public int CombWriteIndex;
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
            RebuildEnclosureProbeLayerMask();
            ResetReverbModelState();
            RefreshAudioConfiguration();
            TryBindFromBootstrap();
        }

        private void OnEnable()
        {
            AcousticOcclusionUtility.AcquireRuntime();
            AudioSettings.OnAudioConfigurationChanged += HandleAudioConfigurationChanged;
            PhysicsEvents.OnImpact += HandlePhysicsImpact;
            SpectrumEvents.OnSonarPingSent += HandleSonarPingSent;
            Volatile.Write(ref _managedFilterFallbackEnabled, 1);
            TryRegister();
            TryBindFromBootstrap();
            StartAudioProducerThread();
        }

        private void OnDisable()
        {
            Volatile.Write(ref _managedFilterFallbackEnabled, 0);
            SpectrumEvents.OnSonarPingSent -= HandleSonarPingSent;
            PhysicsEvents.OnImpact -= HandlePhysicsImpact;
            AudioSettings.OnAudioConfigurationChanged -= HandleAudioConfigurationChanged;
            UnsubscribeTransportCoordinator();
            TryUnregister();
            StopAudioProducerThread();
            RestoreListenerReverbDefaults();
            DisposeBuffers();
            AcousticOcclusionUtility.ReleaseRuntime();
            ClearLowPassState();
        }

        private void OnDestroy()
        {
            if (s_activeInstance == this)
                s_activeInstance = null;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (EnableNativeMixerKernel ||
                Volatile.Read(ref _managedFilterFallbackEnabled) == 0 ||
                data == null ||
                channels <= 0)
            {
                return;
            }

            AudioFrameSpscRingBuffer sampleRingBuffer = _sampleRingBuffer;
            if (!_buffersInitialized || sampleRingBuffer == null || !sampleRingBuffer.IsCreated)
                return;

            sampleRingBuffer.MixInterleavedInto(data, channels);
        }

        /// <summary>
        /// Binds the renderer to the live player object resolved by bootstrap.
        /// </summary>
        /// <param name="playerObject">Live player root.</param>
        internal void BindToPlayer(GameObject playerObject)
        {
            PlayerTransportCoordinator previousCoordinator = playerTransportCoordinator;
            _boundPlayerObject = playerObject;
            _boundPlayerTransform = playerObject != null ? playerObject.transform : null;
            _playerBodyEntityId = 0ul;
            if (playerObject == null)
            {
                UnsubscribeTransportCoordinator();
                _structuralHullReadModel = null;
                _activeTransportLifecycleOwner = null;
                return;
            }

            if (playerMovement == null || !ReferenceEquals(playerMovement.gameObject, playerObject))
                playerObject.TryGetComponent(out playerMovement);

            if (playerToolManager == null || !ReferenceEquals(playerToolManager.gameObject, playerObject))
                playerObject.TryGetComponent(out playerToolManager);

            if (playerTransportCoordinator == null || !ReferenceEquals(playerTransportCoordinator.gameObject, playerObject))
                playerObject.TryGetComponent(out playerTransportCoordinator);

            if (_playerRigidbody == null || !ReferenceEquals(_playerRigidbody.gameObject, playerObject))
                playerObject.TryGetComponent(out _playerRigidbody);

            if (_playerRigidbody != null)
                _playerBodyEntityId = EntityId.ToULong(_playerRigidbody.GetEntityId());

            if (!ReferenceEquals(previousCoordinator, playerTransportCoordinator))
            {
                if (previousCoordinator != null)
                    previousCoordinator.ActiveTransportLifecycleChanged -= HandleActiveTransportLifecycleChanged;

                SubscribeTransportCoordinator();
            }
            else
            {
                RefreshStructuralHullBinding();
            }

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
                _targetStructuralHullStressValue = 0f;
                _targetStructuralHullStressVelocityValue = 0f;
                _targetHullPressureDepthValue = 0f;
                _targetThrusterBlendValue = 0f;
                _targetThrusterLoadValue = 0f;
                _targetThrusterPitchValue = 1f;
                _targetThrusterPressureValue = 0f;
                _targetThrusterAccelerationValue = 0f;
                _targetThrusterHeavyCarryValue = 0f;
                _targetThrusterDiveValue = 0f;
                _targetAbyssalLowPassMix = 0f;
                _lastSpeed = 0f;
                _impactStressImpulseTickValue = 0f;
                _hullPressureDepthTickValue = 0f;
                return;
            }

            float impactStress = _impactStressImpulseTickValue;
            _impactStressImpulseTickValue = math.max(0f, impactStress - deltaTime * PhysicsImpactStressDecayPerSecond);
            float hullBlendT = 1f - math.exp(-math.max(hullStressFollowSharpness, 0.01f) * deltaTime);
            _hullStressTickValue = math.lerp(
                _hullStressTickValue,
                math.saturate(math.max(playerMovement.CurrentHullStress01, impactStress)),
                hullBlendT);
            _targetHullStressValue = _hullStressTickValue;
            float structuralStressTarget = ResolveStructuralHullStress01();
            float structuralBlendT = 1f - math.exp(-StructuralStressFollowSharpness * deltaTime);
            _structuralHullStressTickValue = math.lerp(_structuralHullStressTickValue, structuralStressTarget, structuralBlendT);
            _targetStructuralHullStressValue = _structuralHullStressTickValue;
            float structuralStressVelocityTarget = math.saturate(
                math.abs(structuralStressTarget - _structuralHullStressTickValue) /
                math.max(PressureCreakDerivativeReferencePerSecond * deltaTime, 0.0001f));
            _structuralHullStressVelocityTickValue = math.lerp(
                _structuralHullStressVelocityTickValue,
                structuralStressVelocityTarget,
                structuralBlendT);
            _targetStructuralHullStressVelocityValue = _structuralHullStressVelocityTickValue;
            _hullPressureDepthTickValue = ResolveHullPressureDepth01(playerMovement.CurrentDepth);
            _targetHullPressureDepthValue = _hullPressureDepthTickValue;
            _targetAbyssalLowPassMix = ResolveAbyssalLowPassTarget(playerMovement.CurrentDepth);

            UpdateThrusterTargets(deltaTime);
        }

        /// <summary>
        /// Slow orthogonal enclosure probing for cave-aware listener reverb.
        /// </summary>
        public void SlowTick()
        {
            TryBindFromBootstrap();

            float defaultDistance = math.clamp(math.max(ceilingProbeDistance, caveCeilingThreshold), 1f, MaximumProbeDistanceMeters);
            if (_boundPlayerTransform == null || playerMovement == null || !playerMovement.IsPlayerSubmerged)
            {
                ResetReverbModelState();
                return;
            }

            AcousticOcclusionUtility.PrimeEnclosureSample(
                _boundPlayerTransform.position + Vector3.up * 0.5f,
                defaultDistance,
                _resolvedAcousticOcclusionLayerMask,
                _boundPlayerTransform.root);
            PrimeNearestSonarOcclusionSample();
        }

        private void StartAudioProducerThread()
        {
            if (_audioProducerThread != null && _audioProducerThread.IsAlive)
                return;

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
                if (producerThread.Join(AudioProducerJoinTimeoutMs))
                {
                    _audioProducerThread = null;
                }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                else
                {
                    Debug.LogWarning("[PlayerCriticalProceduralAudioRenderer] Audio producer thread failed to stop within watchdog budget.");
                }
#endif
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
                _sampleRingBuffer.GetState(out int bufferedFrames, out int writableFrames);
                if (bufferedFrames >= targetLeadFrames || writableFrames < blockFrames)
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

            long blockStartFrame = Interlocked.Read(ref _producedSampleCount);
            TryConsumePendingSonarTrigger(blockStartFrame, frameCount);

            double invSampleRate = 1d / math.max(1, _sampleRate);
            ConsumePendingImpactAudioEvents(frameCount, invSampleRate, out float impactStressTarget, out float impactMetallicTarget);
            float hullTarget = math.saturate(math.max(_targetHullStressValue, impactStressTarget));
            float structuralHullTarget = math.saturate(_targetStructuralHullStressValue);
            float structuralHullVelocityTarget = math.saturate(_targetStructuralHullStressVelocityValue);
            float hullDepthTarget = math.saturate(_targetHullPressureDepthValue);
            float thrusterBlendTarget = math.saturate(_targetThrusterBlendValue);
            float thrusterLoadTarget = math.saturate(_targetThrusterLoadValue);
            float thrusterPitchTarget = math.max(0.1f, _targetThrusterPitchValue);
            float thrusterPressureTarget = math.saturate(_targetThrusterPressureValue);
            float thrusterAccelerationTarget = math.saturate(_targetThrusterAccelerationValue);
            float thrusterHeavyCarryTarget = math.saturate(_targetThrusterHeavyCarryValue);
            float thrusterDiveTarget = math.saturate(_targetThrusterDiveValue);

            RenderHullStressBlock(
                frameCount,
                blockStartFrame,
                invSampleRate,
                hullTarget,
                structuralHullTarget,
                structuralHullVelocityTarget,
                hullDepthTarget,
                impactMetallicTarget);
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

            if (_sampleRingBuffer.TryWrite(_mixScratch, frameCount))
                Interlocked.Add(ref _producedSampleCount, frameCount);
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
            if (!_reverbMixerBindingsValid && _listenerReverbFilter == null)
                return;

            float defaultDistance = math.clamp(math.max(ceilingProbeDistance, caveCeilingThreshold), 1f, MaximumProbeDistanceMeters);
            bool shouldUseWaterReverb = playerMovement != null && playerMovement.IsPlayerSubmerged;
            if (!shouldUseWaterReverb || _boundPlayerTransform == null || _resolvedAcousticOcclusionLayerMask == 0)
            {
                ResetReverbModelState();
                RestoreListenerReverbDefaults();
                return;
            }

            float reverbBlendT = 1f - math.exp(-math.max(caveReverbFollowSharpness, 0.01f) * deltaTime);
            float targetDecayTime = openWaterDecayTime;
            float targetWetMix = 0f;
            float targetOpenness = 1f;
            Vector3 probeOrigin = _boundPlayerTransform.position + Vector3.up * 0.5f;
            Transform playerRoot = _boundPlayerTransform.root;

            if (AcousticOcclusionUtility.TryGetCachedEnclosureSample(
                    probeOrigin,
                    defaultDistance,
                    _resolvedAcousticOcclusionLayerMask,
                    playerRoot,
                    out AcousticEnclosureResult enclosure))
            {
                targetDecayTime = math.clamp(enclosure.Rt60Seconds, caveDecayTime, openWaterDecayTime);
                targetWetMix = enclosure.WetMix01;
                targetOpenness = enclosure.Openness01;
            }
            else
            {
                AcousticOcclusionUtility.PrimeEnclosureSample(
                    probeOrigin,
                    defaultDistance,
                    _resolvedAcousticOcclusionLayerMask,
                    playerRoot);
            }

            _smoothedReverbDecayTime = math.lerp(_smoothedReverbDecayTime, targetDecayTime, reverbBlendT);
            _smoothedReverbWetMix = math.lerp(_smoothedReverbWetMix, targetWetMix, reverbBlendT);
            _smoothedReverbOpenness = math.lerp(_smoothedReverbOpenness, targetOpenness, reverbBlendT);
            ApplyListenerReverbProfile(_smoothedReverbWetMix, _smoothedReverbDecayTime, _smoothedReverbOpenness);
        }

        private void ResetReverbModelState()
        {
            _smoothedReverbDecayTime = openWaterDecayTime;
            _smoothedReverbWetMix = 0f;
            _smoothedReverbOpenness = 1f;
        }

        private void ResolveListenerReverbFilter()
        {
            EnsureReverbMixerBindings();
            if (_reverbMixerBindingsValid || _listenerReverbFilter != null)
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
            _listenerReverbBaseReverbLevel = _listenerReverbFilter.reverbLevel;
            _listenerReverbDefaultsCaptured = true;
        }

        private void EnsureReverbMixerBindings()
        {
            if (_reverbMixerBindingsResolved)
                return;

            _reverbMixerBindingsResolved = true;
            _reverbMixerBindingsValid = false;
            _reverbMixerWetBindingValid = false;
            _resolvedReverbDecayTimeParameter = string.IsNullOrWhiteSpace(reverbDecayTimeParameter) ? null : reverbDecayTimeParameter.Trim();
            _resolvedReverbReflectionsLevelParameter = string.IsNullOrWhiteSpace(reverbReflectionsLevelParameter) ? null : reverbReflectionsLevelParameter.Trim();
            _resolvedReverbRoomHighFrequencyParameter = string.IsNullOrWhiteSpace(reverbRoomHighFrequencyParameter) ? null : reverbRoomHighFrequencyParameter.Trim();
            _resolvedReverbWetMixParameter = string.IsNullOrWhiteSpace(reverbWetMixParameter) ? null : reverbWetMixParameter.Trim();

            if (reverbControlMixer == null ||
                string.IsNullOrEmpty(_resolvedReverbDecayTimeParameter) ||
                string.IsNullOrEmpty(_resolvedReverbReflectionsLevelParameter) ||
                string.IsNullOrEmpty(_resolvedReverbRoomHighFrequencyParameter))
            {
                return;
            }

            if (!reverbControlMixer.GetFloat(_resolvedReverbDecayTimeParameter, out _mixerReverbBaseDecayTime) ||
                !reverbControlMixer.GetFloat(_resolvedReverbReflectionsLevelParameter, out _mixerReverbBaseReflectionsLevel) ||
                !reverbControlMixer.GetFloat(_resolvedReverbRoomHighFrequencyParameter, out _mixerReverbBaseRoomHighFrequency))
            {
                if (!_warnedMissingReverbMixerParameters)
                {
                    _warnedMissingReverbMixerParameters = true;
                    Debug.LogWarning("[PlayerCriticalProceduralAudioRenderer] Reverb control mixer is missing one or more exposed parameters. Falling back to AudioReverbFilter.", this);
                }

                return;
            }

            _mixerReverbDefaultsCaptured = true;
            _reverbMixerBindingsValid = true;

            if (!string.IsNullOrEmpty(_resolvedReverbWetMixParameter) &&
                reverbControlMixer.GetFloat(_resolvedReverbWetMixParameter, out _mixerReverbBaseWetMixDb))
            {
                _reverbMixerWetBindingValid = true;
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_warnedMissingReverbWetMixerParameter)
            {
                _warnedMissingReverbWetMixerParameter = true;
                Debug.LogWarning("[PlayerCriticalProceduralAudioRenderer] Reverb wet-mix parameter missing on AudioMixer. Decay/room parameters stay mixer-driven, wet mix falls back to the default mixer state.", this);
            }
#endif
        }

        private void ApplyListenerReverbProfile(float wetMix01, float decayTime, float openness01)
        {
            float clampedDecay = math.clamp(decayTime, 0.05f, 12f);
            float clampedWetMix = math.saturate(wetMix01);
            float clampedOpenness = math.saturate(openness01);
            float reflectionsLevel = math.lerp(caveReflectionsLevel, openWaterReflectionsLevel, clampedOpenness);
            float roomHighFrequency = math.lerp(caveRoomHighFrequency, openWaterRoomHighFrequency, clampedOpenness);
            if (_reverbMixerBindingsValid)
            {
                reverbControlMixer.SetFloat(_resolvedReverbDecayTimeParameter, clampedDecay);
                reverbControlMixer.SetFloat(_resolvedReverbReflectionsLevelParameter, reflectionsLevel);
                reverbControlMixer.SetFloat(_resolvedReverbRoomHighFrequencyParameter, roomHighFrequency);
                if (_reverbMixerWetBindingValid)
                    reverbControlMixer.SetFloat(_resolvedReverbWetMixParameter, math.lerp(MinimumMixerWetMixDb, 0f, clampedWetMix));
                return;
            }

            if (_listenerReverbFilter == null)
                return;

            _listenerReverbFilter.enabled = true;
            _listenerReverbFilter.reverbPreset = AudioReverbPreset.User;
            _listenerReverbFilter.decayTime = clampedDecay;
            _listenerReverbFilter.reflectionsLevel = reflectionsLevel;
            _listenerReverbFilter.roomHF = roomHighFrequency;
            _listenerReverbFilter.reverbLevel = math.lerp(MinimumFilterWetMixDb, 0f, clampedWetMix);
        }

        private void RestoreListenerReverbDefaults()
        {
            if (_reverbMixerBindingsValid && _mixerReverbDefaultsCaptured)
            {
                reverbControlMixer.SetFloat(_resolvedReverbDecayTimeParameter, _mixerReverbBaseDecayTime);
                reverbControlMixer.SetFloat(_resolvedReverbReflectionsLevelParameter, _mixerReverbBaseReflectionsLevel);
                reverbControlMixer.SetFloat(_resolvedReverbRoomHighFrequencyParameter, _mixerReverbBaseRoomHighFrequency);
                if (_reverbMixerWetBindingValid)
                    reverbControlMixer.SetFloat(_resolvedReverbWetMixParameter, _mixerReverbBaseWetMixDb);
                return;
            }

            if (!_listenerReverbDefaultsCaptured || _listenerReverbFilter == null)
                return;

            _listenerReverbFilter.reverbPreset = _listenerReverbBasePreset;
            _listenerReverbFilter.decayTime = _listenerReverbBaseDecayTime;
            _listenerReverbFilter.reflectionsLevel = _listenerReverbBaseReflectionsLevel;
            _listenerReverbFilter.roomHF = _listenerReverbBaseRoomHighFrequency;
            _listenerReverbFilter.reverbLevel = _listenerReverbBaseReverbLevel;
            _listenerReverbFilter.enabled = _listenerReverbWasEnabled;
        }

        private void HandleSonarPingSent(float intensity)
        {
            ResolveSonarEchoModel(
                out float echoDelaySeconds,
                out float echoDopplerRatio,
                out float echoAttenuation,
                out float echoLowPassCutoffHz);

            long producerFrame = Interlocked.Read(ref _producedSampleCount);
            long scheduledStartFrame = producerFrame;

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

            ProceduralAudioEvents.RaiseAudioPingTriggered(
                scheduledStartFrame,
                math.max(_sampleRate, 1),
                math.saturate(intensity),
                SonarChirpDurationSeconds);
        }

        private void HandleAudioConfigurationChanged(bool deviceWasChanged)
        {
            RefreshAudioConfiguration();
        }

        private void HandlePhysicsImpact(PhysicsImpactSignal impactSignal)
        {
            if (_boundPlayerTransform == null)
                return;

            bool isPlayerOwnedImpact =
                _playerBodyEntityId != 0ul &&
                (impactSignal.PrimaryBodyId == _playerBodyEntityId ||
                 impactSignal.SecondaryBodyId == _playerBodyEntityId);
            float maxDistance = PhysicsImpactStressRadiusMeters;
            float distance = Vector3.Distance(_boundPlayerTransform.position, impactSignal.Point);
            if (!isPlayerOwnedImpact && distance > maxDistance)
                return;

            float proximity = isPlayerOwnedImpact
                ? 1f
                : 1f - math.saturate(distance / maxDistance);
            float impactStress = math.saturate(impactSignal.Intensity * PhysicsImpactStressBoost * math.max(0.2f, proximity));
            if (impactSignal.IsHeavy)
                impactStress = math.max(impactStress, 0.45f * math.max(0.35f, proximity));

            float metallicImpulse = impactSignal.IsHeavy
                ? math.max(impactStress, 0.55f * math.max(0.35f, proximity))
                : impactStress * math.max(0.35f, proximity);
            TryEnqueueImpactAudioEvent(impactStress, metallicImpulse);
            _impactStressImpulseTickValue = math.max(_impactStressImpulseTickValue, impactStress);
        }

        private void HandleActiveTransportLifecycleChanged(IPlayerTransportLifecycleOwner lifecycleOwner)
        {
            _activeTransportLifecycleOwner = lifecycleOwner;
            ResolveStructuralHullReadModel(lifecycleOwner);
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
            SystemDispatcher.EnsureRuntimeInstance();

            if (!_registered)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
                _registered = true;
            }

            if (_slowTickRegistered)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _slowTickRegistered = true;
        }

        private void TryUnregister()
        {
            if (_registered)
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);

            if (_slowTickRegistered)
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);

            _registered = false;
            _slowTickRegistered = false;
        }

        private void SubscribeTransportCoordinator()
        {
            if (playerTransportCoordinator == null)
            {
                _activeTransportLifecycleOwner = null;
                _structuralHullReadModel = null;
                return;
            }

            playerTransportCoordinator.ActiveTransportLifecycleChanged -= HandleActiveTransportLifecycleChanged;
            playerTransportCoordinator.ActiveTransportLifecycleChanged += HandleActiveTransportLifecycleChanged;
            RefreshStructuralHullBinding();
        }

        private void UnsubscribeTransportCoordinator()
        {
            if (playerTransportCoordinator != null)
                playerTransportCoordinator.ActiveTransportLifecycleChanged -= HandleActiveTransportLifecycleChanged;

            _activeTransportLifecycleOwner = null;
            _structuralHullReadModel = null;
        }

        private void RefreshStructuralHullBinding()
        {
            if (playerTransportCoordinator != null &&
                playerTransportCoordinator.TryResolveTransportLifecycleOwner(out IPlayerTransportLifecycleOwner lifecycleOwner))
            {
                _activeTransportLifecycleOwner = lifecycleOwner;
                ResolveStructuralHullReadModel(lifecycleOwner);
                return;
            }

            _activeTransportLifecycleOwner = null;
            _structuralHullReadModel = null;
        }

        private void ResolveStructuralHullReadModel(IPlayerTransportLifecycleOwner lifecycleOwner)
        {
            MonoBehaviour lifecycleBehaviour = lifecycleOwner as MonoBehaviour;
            if (lifecycleBehaviour != null && lifecycleBehaviour.TryGetComponent(out SubmarineStructuralGrid structuralGrid))
            {
                _structuralHullReadModel = structuralGrid;
                return;
            }

            _structuralHullReadModel = null;
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
            _nativeOutputBridgeFailureLogged = false;
            ClearNativeOutputBridge();

            if (shouldRestartWorker && isActiveAndEnabled)
                StartAudioProducerThread();
        }

        private void EnsureBuffers(int frameCapacity)
        {
            if (_buffersInitialized && _frameCapacity == frameCapacity)
                return;

            DisposeBuffers();

            _frameCapacity = frameCapacity;
            _hullScratch = new NativeArray<float>(_frameCapacity, Allocator.AudioKernel, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<float>[frameCapacity] - hull-stress DSP scratch - owner: PlayerCriticalProceduralAudioRenderer
            _sonarScratch = new NativeArray<float>(_frameCapacity, Allocator.AudioKernel, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<float>[frameCapacity] - sonar DSP scratch - owner: PlayerCriticalProceduralAudioRenderer
            _thrusterScratch = new NativeArray<float>(_frameCapacity, Allocator.AudioKernel, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<float>[frameCapacity] - thruster DSP scratch - owner: PlayerCriticalProceduralAudioRenderer
            _mixScratch = new NativeArray<float>(_frameCapacity, Allocator.AudioKernel, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<float>[frameCapacity] - mixed procedural audio worklet scratch - owner: PlayerCriticalProceduralAudioRenderer
            _sonarEchoDelay = new NativeArray<float>(SonarEchoDelayCapacity, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[131072] - sonar Hermite echo delay ring - owner: PlayerCriticalProceduralAudioRenderer
            _thrusterCombDelay = new NativeArray<float>(ThrusterCombDelayCapacity, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[4096] - thruster comb filter delay ring - owner: PlayerCriticalProceduralAudioRenderer
            _lowPassInputHistory1 = new NativeArray<float>(MaxFilterChannels, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[8] - final listener low-pass state x1 - owner: PlayerCriticalProceduralAudioRenderer
            _lowPassInputHistory2 = new NativeArray<float>(MaxFilterChannels, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[8] - final listener low-pass state x2 - owner: PlayerCriticalProceduralAudioRenderer
            _lowPassOutputHistory1 = new NativeArray<float>(MaxFilterChannels, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[8] - final listener low-pass state y1 - owner: PlayerCriticalProceduralAudioRenderer
            _lowPassOutputHistory2 = new NativeArray<float>(MaxFilterChannels, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[8] - final listener low-pass state y2 - owner: PlayerCriticalProceduralAudioRenderer
            _metallicGrainBank = new NativeArray<float>(MetallicGrainBankCapacity, Allocator.AudioKernel, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<float>[8192] - pre-baked metallic screech grain bank for hull granular synthesis - owner: PlayerCriticalProceduralAudioRenderer
            GenerateMetallicGrainBank(_metallicGrainBank);
            _sampleRingBuffer ??= new AudioFrameSpscRingBuffer();
            _sampleRingBuffer.Initialize(math.max(frameCapacity * 16, ringBufferCapacityFrames));
            _producedSampleCount = 0L;
            _workerActiveSonarState = default;
            _workerConsumedSonarSequence = 0;
            ResetSonarPhaseState(0);
            _buffersInitialized = true;
        }

        private void DisposeBuffers()
        {
            ClearNativeOutputBridge();
            _sampleRingBuffer?.Dispose();
            _sampleRingBuffer = null;
            if (_hullScratch.IsCreated)
                _hullScratch.Dispose();
            if (_sonarScratch.IsCreated)
                _sonarScratch.Dispose();
            if (_thrusterScratch.IsCreated)
                _thrusterScratch.Dispose();
            if (_mixScratch.IsCreated)
                _mixScratch.Dispose();
            if (_sonarEchoDelay.IsCreated)
                _sonarEchoDelay.Dispose();
            if (_thrusterCombDelay.IsCreated)
                _thrusterCombDelay.Dispose();
            if (_lowPassInputHistory1.IsCreated)
                _lowPassInputHistory1.Dispose();
            if (_lowPassInputHistory2.IsCreated)
                _lowPassInputHistory2.Dispose();
            if (_lowPassOutputHistory1.IsCreated)
                _lowPassOutputHistory1.Dispose();
            if (_lowPassOutputHistory2.IsCreated)
                _lowPassOutputHistory2.Dispose();
            if (_metallicGrainBank.IsCreated)
                _metallicGrainBank.Dispose();

            _hullScratch = default;
            _sonarScratch = default;
            _thrusterScratch = default;
            _mixScratch = default;
            _sonarEchoDelay = default;
            _thrusterCombDelay = default;
            _lowPassInputHistory1 = default;
            _lowPassInputHistory2 = default;
            _lowPassOutputHistory1 = default;
            _lowPassOutputHistory2 = default;
            _metallicGrainBank = default;

            _buffersInitialized = false;
            _frameCapacity = 0;
            _producedSampleCount = 0L;
        }

        private void RefreshNativeOutputBridge()
        {
            if (_sampleRingBuffer == null || !_sampleRingBuffer.IsCreated)
            {
                ClearNativeOutputBridge();
                return;
            }

            if (!_sampleRingBuffer.TryCreateNativeDescriptor(
                    out NativeAudioKernelRingBufferDescriptor descriptor,
                    out NativeAudioKernelBridgeStatus descriptorStatus))
            {
                ClearNativeOutputBridge();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (!_nativeOutputBridgeFailureLogged)
                {
                    _nativeOutputBridgeFailureLogged = true;
                    Debug.LogError(
                        "[PlayerCriticalProceduralAudioRenderer] Native HectonAudioKernel descriptor rejected before registration. Status=" +
                        descriptorStatus,
                        this);
                }
#endif
                return;
            }

            bool registered = HectonSensoryKernelNativeBridge.TryRegister(ref descriptor, out NativeAudioKernelBridgeStatus bridgeStatus);
            _nativeOutputRegistered = registered;
            if (registered)
            {
                _nativeOutputBridgeFailureLogged = false;
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_nativeOutputBridgeFailureLogged)
            {
                _nativeOutputBridgeFailureLogged = true;
                Debug.LogError(
                    "[PlayerCriticalProceduralAudioRenderer] Native HectonAudioKernel bridge unavailable. Procedural master-bus output is not registered. Status=" +
                    bridgeStatus,
                    this);
            }
#endif
        }

        private void ClearNativeOutputBridge()
        {
            if (_nativeOutputRegistered)
                HectonSensoryKernelNativeBridge.TryClear();

            _nativeOutputRegistered = false;
        }

        private void ClearLowPassState()
        {
            ClearScratchBuffer(_lowPassInputHistory1, _lowPassInputHistory1.Length);
            ClearScratchBuffer(_lowPassInputHistory2, _lowPassInputHistory2.Length);
            ClearScratchBuffer(_lowPassOutputHistory1, _lowPassOutputHistory1.Length);
            ClearScratchBuffer(_lowPassOutputHistory2, _lowPassOutputHistory2.Length);
            ClearScratchBuffer(_metallicGrainBank, _metallicGrainBank.Length);
            _audioAbyssalLowPassMix = 0f;
            _pendingSonarSequence = 0;
            _pendingSonarStateReadIndex = 0;
            _pendingSonarStateA = default;
            _pendingSonarStateB = default;
            _workerActiveSonarState = default;
            _workerConsumedSonarSequence = 0;
            _impactEventReadIndex = 0;
            _impactEventWriteIndex = 0;
            _hullSynthesisState = default;
            _thrusterSynthesisState = default;
            _audioImpactStressValue = 0f;
            _audioImpactMetallicValue = 0f;
            _audioHullStressValue = 0f;
            _audioStructuralHullStressValue = 0f;
            _audioStructuralHullStressVelocityValue = 0f;
            _audioHullPressureDepthValue = 0f;
            _hullStressTickValue = 0f;
            _structuralHullStressTickValue = 0f;
            _structuralHullStressVelocityTickValue = 0f;
            ResetReverbModelState();
            ResetSonarPhaseState(0);
            if (_sonarEchoDelay.IsCreated)
                ClearScratchBuffer(_sonarEchoDelay, _sonarEchoDelay.Length);
            if (_thrusterCombDelay.IsCreated)
                ClearScratchBuffer(_thrusterCombDelay, _thrusterCombDelay.Length);
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

            Transform playerRoot = _boundPlayerTransform != null ? _boundPlayerTransform.root : null;
            if (AcousticOcclusionUtility.TryGetCachedOcclusionPath(
                    playerPosition,
                    reflector.Position,
                    _resolvedAcousticOcclusionLayerMask,
                    playerRoot,
                    reflector.RootTransform,
                    out AcousticOcclusionResult occlusion))
            {
                echoAttenuation = math.clamp(echoAttenuation * occlusion.Transmission01, 0f, 0.95f);
                echoLowPassCutoffHz = occlusion.LowPassCutoffHz;
            }
            else
            {
                AcousticOcclusionUtility.PrimeOcclusionPath(
                    playerPosition,
                    reflector.Position,
                    _resolvedAcousticOcclusionLayerMask,
                    playerRoot,
                    reflector.RootTransform);
            }
        }

        private void PrimeNearestSonarOcclusionSample()
        {
            if (_boundPlayerObject == null || _resolvedAcousticOcclusionLayerMask == 0)
                return;

            Vector3 playerPosition = _boundPlayerObject.transform.position;
            if (!TryResolveNearestSonarReflector(playerPosition, out SonarReflectorDescriptor reflector))
                return;

            AcousticOcclusionUtility.PrimeOcclusionPath(
                playerPosition,
                reflector.Position,
                _resolvedAcousticOcclusionLayerMask,
                _boundPlayerTransform != null ? _boundPlayerTransform.root : null,
                reflector.RootTransform);
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
                ActiveSequence = activeSequence,
                EchoReadCursor = -1d
            };

            if (_sonarEchoDelay.IsCreated)
                ClearScratchBuffer(_sonarEchoDelay, _sonarEchoDelay.Length);
        }

        private void RebuildEnclosureProbeLayerMask()
        {
            _resolvedAcousticOcclusionLayerMask = AcousticOcclusionUtility.BuildSensoryMask() & enclosureProbeLayers.value;
        }

        private static float ResolveHullPressureDepth01(float depthMeters)
        {
            return math.saturate(math.max(0f, depthMeters) / PressureCreakDepthReferenceMeters);
        }

        private float ResolveStructuralHullStress01()
        {
            if (_structuralHullReadModel == null)
                _structuralHullReadModel = GlobalRegistry.SubmarineHullBreach;

            ISubmarineHullBreachReadModel readModel = _structuralHullReadModel;
            if (readModel == null || !readModel.IsReady)
                return playerMovement != null ? math.saturate(playerMovement.CurrentHullStress01) : 0f;

            float totalBreachArea = 0f;
            for (int compartmentIndex = 0; compartmentIndex < 8; compartmentIndex++)
                totalBreachArea += math.max(0f, readModel.GetCompartmentBreachAreaSquareMeters(compartmentIndex));

            int breachedCellCount = 0;
            int breachWordCount = readModel.BreachMaskWordCount;
            for (int wordIndex = 0; wordIndex < breachWordCount; wordIndex++)
                breachedCellCount += CountBits(readModel.GetHullBreachMaskWord(wordIndex));

            float breachAreaSeverity = math.saturate(totalBreachArea / StructuralBreachAreaReferenceSquareMeters);
            float cellFailureSeverity = math.saturate(breachedCellCount / 24f);
            float structuralSeverity = math.saturate(math.max(
                breachAreaSeverity,
                breachAreaSeverity * 0.65f + cellFailureSeverity * 0.35f));

            if (playerMovement == null)
                return structuralSeverity;

            return math.saturate(math.max(playerMovement.CurrentHullStress01, structuralSeverity));
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
            if (value <= 1)
                return 1;

            int power = 1;
            int growthWatchdog = 31;
            while (power < value && power < MaxSafeFrameCapacity && growthWatchdog-- > 0)
            {
                if (power > (MaxSafeFrameCapacity >> 1))
                    return MaxSafeFrameCapacity;

                power <<= 1;
            }

            return power < value ? MaxSafeFrameCapacity : power;
        }

        private static int CountBits(ulong value)
        {
            int count = 0;
            while (value != 0UL)
            {
                value &= value - 1UL;
                count++;
            }

            return count;
        }

        private bool TryEnqueueImpactAudioEvent(float stress, float metallic)
        {
            ImpactAudioEvent impactAudioEvent = new ImpactAudioEvent
            {
                Stress = math.saturate(stress),
                Metallic = math.saturate(metallic)
            };

            int watchdog = 0;
            while (true)
            {
                if (watchdog++ > ImpactEventQueueSpinWatchdog)
                {
                    Debug.LogError(
                        $"[PlayerCriticalProceduralAudioRenderer] TryEnqueueImpactAudioEvent exceeded {ImpactEventQueueSpinWatchdog} iterations.");
                    return false;
                }

                int writeIndex = _impactEventWriteIndex;
                int nextWriteIndex = (writeIndex + 1) & ImpactEventQueueMask;
                int readIndex = Volatile.Read(ref _impactEventReadIndex);
                if (nextWriteIndex == readIndex)
                {
                    int advancedReadIndex = (readIndex + 1) & ImpactEventQueueMask;
                    // Overflow policy: drop the oldest unread event, but only if the consumer
                    // has not already advanced the read pointer since we observed it.
                    if (Interlocked.CompareExchange(ref _impactEventReadIndex, advancedReadIndex, readIndex) != readIndex)
                        continue;
                }

                _impactEventQueue[writeIndex] = impactAudioEvent;
                Volatile.Write(ref _impactEventWriteIndex, nextWriteIndex);
                return true;
            }
        }

        private bool TryDequeueImpactAudioEvent(out ImpactAudioEvent impactAudioEvent)
        {
            int readIndex = _impactEventReadIndex;
            if (readIndex == Volatile.Read(ref _impactEventWriteIndex))
            {
                impactAudioEvent = default;
                return false;
            }

            impactAudioEvent = _impactEventQueue[readIndex];
            Volatile.Write(ref _impactEventReadIndex, (readIndex + 1) & ImpactEventQueueMask);
            return true;
        }

        private void ConsumePendingImpactAudioEvents(
            int frameCount,
            double invSampleRate,
            out float impactStressTarget,
            out float impactMetallicTarget)
        {
            impactStressTarget = _audioImpactStressValue;
            impactMetallicTarget = _audioImpactMetallicValue;

            while (TryDequeueImpactAudioEvent(out ImpactAudioEvent impactAudioEvent))
            {
                impactStressTarget = math.max(impactStressTarget, impactAudioEvent.Stress);
                impactMetallicTarget = math.max(impactMetallicTarget, impactAudioEvent.Metallic);
            }

            float blockDurationSeconds = frameCount > 0 ? (float)(frameCount * invSampleRate) : 0f;
            _audioImpactStressValue = math.max(
                0f,
                impactStressTarget - (blockDurationSeconds * PhysicsImpactStressDecayPerSecond));
            _audioImpactMetallicValue = math.max(
                0f,
                impactMetallicTarget - (blockDurationSeconds * PhysicsImpactMetallicDecayPerSecond));
        }

        private void RenderHullStressBlock(
            int frameCount,
            long blockStartFrame,
            double invSampleRate,
            float hullTarget,
            float structuralHullTarget,
            float structuralHullVelocityTarget,
            float depthParamTarget,
            float impactMetallicTarget)
        {
            HullSynthesisState state = _hullSynthesisState;
            float stressStart = _audioHullStressValue;
            float structuralStressStart = _audioStructuralHullStressValue;
            float structuralStressVelocityStart = _audioStructuralHullStressVelocityValue;
            float depthParamStart = _audioHullPressureDepthValue;
            float impactMetallicImpulse = math.saturate(impactMetallicTarget);

            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                float frameT = frameCount > 1 ? frameIndex / (float)(frameCount - 1) : 0f;
                float stress = math.lerp(stressStart, hullTarget, frameT);
                float structuralStress = math.lerp(structuralStressStart, structuralHullTarget, frameT);
                float structuralStressVelocity = math.lerp(structuralStressVelocityStart, structuralHullVelocityTarget, frameT);
                float depthParam = math.lerp(depthParamStart, depthParamTarget, frameT);
                float metallicImpulse = math.max(impactMetallicImpulse, structuralStress);
                float metallicDrive = math.lerp(1f, 2.15f, metallicImpulse);
                float rivetAmount = hullRivetBurstAmount * math.lerp(1f, 2.35f, metallicImpulse);
                long sampleFrame = blockStartFrame + frameIndex;
                uint sampleIndex = (uint)math.max(0L, sampleFrame);

                float pressureLfo = 0.6f + 0.4f * AdvanceSine(ref state.PressureLfoPhase, 0.3d, invSampleRate);
                float pressureBed =
                    (LayeredBrownLike(sampleIndex) * pressureLfo * hullPressureBedAmount) * math.sqrt(math.max(stress, 0f));

                float structuralSag = math.lerp(1f, 0.58f, structuralStress);
                float carrierA = math.lerp(120f, 800f, math.pow(stress, 0.82f)) * structuralSag;
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
                float modIndex = (1.8f + 6.2f * stress + 4.8f * structuralStress) * stickSlip;
                float modulatorA = AdvanceSine(ref state.ModulatorAPhase, math.lerp(45f, 97f, stress) + (structuralStress * 31f), invSampleRate);
                float modulatorB = AdvanceSine(ref state.ModulatorBPhase, math.lerp(87f, 133f, stress * 0.8f) + (structuralStress * 47f), invSampleRate);

                AdvancePhase(ref state.LowCarrierPhase, 80d, invSampleRate);
                float lowCarrierFm =
                    math.sin((float)(TwoPi * state.LowCarrierPhase) + frictionNoiseOperator * (0.4f + 3.6f * stress) * stickSlip) *
                    (0.18f + 0.26f * stress + 0.18f * structuralStress);

                AdvancePhase(ref state.CarrierAPhase, carrierA, invSampleRate);
                AdvancePhase(ref state.CarrierBPhase, carrierB, invSampleRate);
                AdvancePhase(ref state.CarrierCPhase, carrierC, invSampleRate);
                float metal =
                    math.sin((float)(TwoPi * state.CarrierAPhase) + modIndex * modulatorA) * 0.54f +
                    math.sin((float)(TwoPi * state.CarrierBPhase) + modIndex * 0.62f * modulatorB) * 0.29f +
                    math.sin((float)(TwoPi * state.CarrierCPhase) + modIndex * 0.35f * modulatorA) * 0.17f;
                metal = ((metal + lowCarrierFm) * metallicDrive) * groanEnvelope * math.lerp(0.25f, 1f, math.max(stress, structuralStress));

                float pressureCreak = RenderPressureCreakSample(ref state, sampleIndex, stress, structuralStressVelocity, depthParam, invSampleRate);
                float granularMetal = RenderStructuralGranularSample(
                    ref state,
                    _metallicGrainBank,
                    sampleIndex);
                float subBass = RenderHullSubBassSample(ref state, structuralStress, depthParam, invSampleRate);
                float rivetBurst = BuildRivetBurst(sampleIndex, math.max(stress, metallicImpulse), rivetAmount);
                float combined = pressureBed + metal + pressureCreak + granularMetal + rivetBurst + subBass;
                _hullScratch[frameIndex] = stress <= HullNoiseFloor
                    ? 0f
                    : math.tanh(combined * math.lerp(1.7f, 2.8f, metallicImpulse)) * hullMasterGain;
                AdvancePressureCreakEnvelope(ref state);
            }

            _hullSynthesisState = state;
            _audioHullStressValue = hullTarget;
            _audioStructuralHullStressValue = structuralHullTarget;
            _audioStructuralHullStressVelocityValue = structuralHullVelocityTarget;
            _audioHullPressureDepthValue = depthParamTarget;
        }

        private static void ClearScratchBuffer(NativeArray<float> buffer, int frameCount)
        {
            if (!buffer.IsCreated || frameCount <= 0)
                return;

            int safeCount = math.min(frameCount, buffer.Length);
            for (int i = 0; i < safeCount; i++)
                buffer[i] = 0f;
        }

        private void RenderSonarBlock(int frameCount, long blockStartFrame, double invSampleRate)
        {
            SonarTriggerState activeState = _workerActiveSonarState;
            if (activeState.Sequence == 0 || activeState.Intensity <= 0f)
            {
                ClearScratchBuffer(_sonarScratch, frameCount);
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

                float drySignal = attack + chirp;
                if (_sonarEchoDelay.IsCreated)
                {
                    _sonarEchoDelay[state.EchoWriteIndex] = drySignal;
                    state.EchoWriteIndex = (state.EchoWriteIndex + 1) & SonarEchoDelayMask;
                }

                float echo = 0f;
                float echoAge = age - activeState.EchoDelaySeconds;
                if (echoAge >= 0f && echoAge < SonarChirpDurationSeconds && _sonarEchoDelay.IsCreated)
                {
                    int echoDelaySamples = math.clamp(
                        (int)math.round(activeState.EchoDelaySeconds * math.max(_sampleRate, 1)),
                        1,
                        SonarEchoDelayCapacity - 4);

                    if (state.EchoReadCursor < 0d)
                        state.EchoReadCursor = (state.EchoWriteIndex - echoDelaySamples) & SonarEchoDelayMask;

                    float echoEnvelope = math.exp(-echoAge * 4.5f) * activeState.EchoAttenuation;
                    echo = HermiteSampleRing(_sonarEchoDelay, state.EchoReadCursor, SonarEchoDelayMask) * echoEnvelope;
                    state.EchoReadCursor += activeState.EchoDopplerRatio;
                    if (state.EchoReadCursor >= SonarEchoDelayCapacity)
                        state.EchoReadCursor -= SonarEchoDelayCapacity;
                }
                else if (echoAge < 0f)
                {
                    state.EchoReadCursor = -1d;
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
                float throttle = math.saturate(load * 0.76f + acceleration * 0.24f);
                long sampleFrame = blockStartFrame + frameIndex;
                uint sampleIndex = (uint)math.max(0L, sampleFrame);

                float hum =
                    AdvanceSine(ref state.Hum1Phase, 80d * pitchScale, invSampleRate) * 1.00f +
                    AdvanceSine(ref state.Hum2Phase, 160d * pitchScale, invSampleRate) * 0.60f +
                    AdvanceSine(ref state.Hum3Phase, 240d * pitchScale, invSampleRate) * 0.35f +
                    AdvanceSine(ref state.Hum4Phase, 320d * pitchScale, invSampleRate) * 0.15f;
                hum *= 0.42f;

                float flowMod = 0.55f + 0.45f * AdvanceSine(ref state.FlowPhase, 0.31d, invSampleRate);
                float whiteNoise = HashSigned(sampleIndex ^ 0xCAFEBABEu);
                float pinkNoise = ApplyPaulKelletPink(ref state, whiteNoise) * flowMod;
                float bandPassCenter = math.lerp(200f, 1200f, throttle);
                ComputeBandPassCoefficients(
                    bandPassCenter,
                    ThrusterBandPassQ,
                    _sampleRate,
                    out float bpB0,
                    out float bpB1,
                    out float bpB2,
                    out float bpA1,
                    out float bpA2);
                float bandPassedFlow = ProcessBiquad(
                    pinkNoise,
                    bpB0,
                    bpB1,
                    bpB2,
                    bpA1,
                    bpA2,
                    ref state.BandPassInput1,
                    ref state.BandPassInput2,
                    ref state.BandPassOutput1,
                    ref state.BandPassOutput2);

                float bladePassHz = math.lerp(
                    ThrusterBladePassFrequencyMinHertz,
                    ThrusterBladePassFrequencyMaxHertz,
                    math.saturate(throttle * 0.82f + pitchScale * 0.18f - 0.1f));
                int bladeDelaySamples = math.clamp(
                    (int)math.round(_sampleRate / math.max(1f, bladePassHz)),
                    1,
                    ThrusterCombDelayCapacity - 1);
                int combWriteIndex = state.CombWriteIndex & ThrusterCombDelayMask;
                int combReadIndex = (combWriteIndex - bladeDelaySamples) & ThrusterCombDelayMask;
                float delayedBladePass = _thrusterCombDelay.IsCreated ? _thrusterCombDelay[combReadIndex] : 0f;
                state.CombFeedbackSample = math.lerp(delayedBladePass, state.CombFeedbackSample, ThrusterCombDamp);
                float combFeedback = math.lerp(0.18f, 0.62f, math.saturate(load * 0.65f + pressure * 0.35f));
                if (_thrusterCombDelay.IsCreated)
                {
                    _thrusterCombDelay[combWriteIndex] = bandPassedFlow + state.CombFeedbackSample * combFeedback;
                    state.CombWriteIndex = (combWriteIndex + 1) & ThrusterCombDelayMask;
                }

                float flow =
                    bandPassedFlow * (0.34f + 0.31f * load + 0.11f * heavyCarry) +
                    delayedBladePass * (0.22f + 0.28f * math.saturate(load + acceleration * 0.4f));

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

        private static float RenderPressureCreakSample(
            ref HullSynthesisState state,
            uint sampleIndex,
            float stress,
            float stressDerivative,
            float depthParam,
            double invSampleRate)
        {
            if (stress <= HullNoiseFloor || depthParam <= HullNoiseFloor)
                return 0f;

            if (state.GrainTotalSamples <= 0)
            {
                float derivativeBoost = stressDerivative * PressureCreakDerivativeDensityBoostPerSecond;
                float lambda =
                    PressureCreakMinimumEventsPerSecond +
                    depthParam * (PressureCreakMaximumEventsPerSecond - PressureCreakMinimumEventsPerSecond) +
                    derivativeBoost;
                float eventThreshold = math.saturate(lambda * (float)invSampleRate);
                if (Hash01(sampleIndex ^ 0x2C9D4F31u) <= eventThreshold)
                {
                    StartPressureCreakGrain(ref state, sampleIndex, stress, depthParam, stressDerivative, invSampleRate);
                    StartStructuralGranularLoop(ref state, sampleIndex, stress, stressDerivative, depthParam);
                }
            }

            if (state.GrainTotalSamples <= 0)
                return 0f;

            float envelope = PeekPressureCreakEnvelope(state);
            float grainNoise = HighBandNoise(sampleIndex ^ state.GrainNoiseSeed);
            float filtered = ProcessBiquad(
                grainNoise,
                state.GrainBandPassB0,
                state.GrainBandPassB1,
                state.GrainBandPassB2,
                state.GrainBandPassA1,
                state.GrainBandPassA2,
                ref state.GrainBandPassInput1,
                ref state.GrainBandPassInput2,
                ref state.GrainBandPassOutput1,
                ref state.GrainBandPassOutput2);
            return math.tanh(filtered * envelope * state.GrainGain * 2.1f);
        }

        private static void StartPressureCreakGrain(
            ref HullSynthesisState state,
            uint sampleIndex,
            float stress,
            float depthParam,
            float stressDerivative,
            double invSampleRate)
        {
            int attackSamples = math.max(1, (int)math.round(PressureCreakAttackSeconds / invSampleRate));
            int decaySamples = math.max(1, (int)math.round(PressureCreakDecaySeconds / invSampleRate));
            int sustainSamples = math.max(1, (int)math.round(math.lerp(PressureCreakSustainSeconds, PressureCreakSustainSeconds * 1.65f, stress) / invSampleRate));
            int releaseSamples = math.max(1, (int)math.round(PressureCreakReleaseSeconds / invSampleRate));
            float derivativePitch = math.saturate(stressDerivative * PressureCreakDerivativePitchBoost);
            float bandPassCenter = math.lerp(
                PressureCreakMinimumBandCenterHertz,
                PressureCreakMaximumBandCenterHertz,
                math.saturate(Hash01(sampleIndex ^ 0x42E98A77u) * 0.45f + derivativePitch * 0.55f));

            state.GrainElapsedSamples = 0;
            state.GrainAttackSamples = attackSamples;
            state.GrainDecaySamples = decaySamples;
            state.GrainSustainSamples = sustainSamples;
            state.GrainReleaseSamples = releaseSamples;
            state.GrainTotalSamples = attackSamples + decaySamples + sustainSamples + releaseSamples;
            state.GrainSustainLevel = math.lerp(0.22f, 0.64f, stress);
            state.GrainGain =
                math.lerp(0.12f, 0.48f, depthParam) *
                math.lerp(0.35f, 1f, stress) *
                math.lerp(1f, 1.65f, derivativePitch);
            state.GrainDerivative = stressDerivative;
            state.GrainNoiseSeed = sampleIndex ^ 0x8A51DD13u;
            state.GrainBandPassInput1 = 0f;
            state.GrainBandPassInput2 = 0f;
            state.GrainBandPassOutput1 = 0f;
            state.GrainBandPassOutput2 = 0f;
            ComputeBandPassCoefficients(
                bandPassCenter,
                PressureCreakBandPassQ,
                (int)math.round(1d / invSampleRate),
                out state.GrainBandPassB0,
                out state.GrainBandPassB1,
                out state.GrainBandPassB2,
                out state.GrainBandPassA1,
                out state.GrainBandPassA2);
        }

        private static float RenderStructuralGranularSample(
            ref HullSynthesisState state,
            NativeArray<float> grainBank,
            uint sampleIndex)
        {
            if (!grainBank.IsCreated || grainBank.Length <= 0 || state.GrainLoopLength <= 0 || state.GrainTotalSamples <= 0)
                return 0f;

            float envelope = PeekPressureCreakEnvelope(state);
            float sample = HermiteSampleLoopWindow(grainBank, state.GrainLoopStartIndex, state.GrainLoopLength, state.GrainReadCursor);
            state.GrainReadCursor += state.GrainPlaybackRate;
            while (state.GrainReadCursor >= state.GrainLoopLength)
                state.GrainReadCursor -= state.GrainLoopLength;

            float filtered = ProcessBiquad(
                sample,
                state.GrainBandPassB0,
                state.GrainBandPassB1,
                state.GrainBandPassB2,
                state.GrainBandPassA1,
                state.GrainBandPassA2,
                ref state.GrainBandPassInput1,
                ref state.GrainBandPassInput2,
                ref state.GrainBandPassOutput1,
                ref state.GrainBandPassOutput2);
            return math.tanh(filtered * envelope * state.GrainGain * math.lerp(1.6f, 3.1f, math.saturate(state.GrainDerivative)));
        }

        private static void StartStructuralGranularLoop(
            ref HullSynthesisState state,
            uint sampleIndex,
            float stress,
            float stressDerivative,
            float depthParam)
        {
            float startHash = Hash01(sampleIndex ^ 0xB913E51u);
            float lengthHash = Hash01(sampleIndex ^ 0x6F124C31u);
            state.GrainLoopLength = math.max(96, (int)math.round(math.lerp(112f, 640f, lengthHash)));
            state.GrainLoopStartIndex = ((int)math.floor(startHash * (MetallicGrainBankCapacity - state.GrainLoopLength))) & MetallicGrainBankMask;
            state.GrainReadCursor = 0d;
            state.GrainPlaybackRate = math.lerp(
                PressureCreakMinimumPlaybackRate,
                PressureCreakMaximumPlaybackRate,
                math.saturate(stressDerivative * 0.7f + stress * 0.3f));
            state.GrainDerivative = stressDerivative;
            state.GrainGain =
                math.lerp(0.08f, 0.28f, depthParam) *
                math.lerp(0.45f, 1f, stress) *
                math.lerp(0.65f, 1.45f, stressDerivative);
        }

        private static float RenderHullSubBassSample(
            ref HullSynthesisState state,
            float structuralStress,
            float depthParam,
            double invSampleRate)
        {
            if (depthParam <= HullNoiseFloor)
                return 0f;

            float frequency = math.lerp(HullSubBassMaximumHertz, HullSubBassMinimumHertz, depthParam);
            float sine = AdvanceSine(ref state.SubBassPhase, frequency, invSampleRate);
            double trianglePhase = state.SubBassPhase;
            float triangle = (float)(2.0 * math.abs((float)(2.0 * (trianglePhase - math.floor(trianglePhase + 0.5)))) - 1.0);
            float amplitude = HullSubBassMaximumGain * math.saturate(depthParam * 0.85f + structuralStress * 0.15f);
            return (sine * 0.76f + triangle * 0.24f) * amplitude;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
        private static float PeekPressureCreakEnvelope(in HullSynthesisState state)
        {
            if (state.GrainTotalSamples <= 0)
                return 0f;

            int attackEnd = state.GrainAttackSamples;
            int decayEnd = attackEnd + state.GrainDecaySamples;
            int sustainEnd = decayEnd + state.GrainSustainSamples;
            int elapsed = state.GrainElapsedSamples;
            float envelope;

            if (elapsed < attackEnd)
            {
                envelope = elapsed / (float)math.max(1, state.GrainAttackSamples);
            }
            else if (elapsed < decayEnd)
            {
                float t = (elapsed - attackEnd) / (float)math.max(1, state.GrainDecaySamples);
                envelope = math.lerp(1f, state.GrainSustainLevel, t);
            }
            else if (elapsed < sustainEnd)
            {
                envelope = state.GrainSustainLevel;
            }
            else
            {
                float t = (elapsed - sustainEnd) / (float)math.max(1, state.GrainReleaseSamples);
                envelope = math.lerp(state.GrainSustainLevel, 0f, t);
            }

            return envelope;
        }

        private static void AdvancePressureCreakEnvelope(ref HullSynthesisState state)
        {
            if (state.GrainTotalSamples <= 0)
                return;

            state.GrainElapsedSamples++;
            if (state.GrainElapsedSamples < state.GrainTotalSamples)
                return;

            state.GrainTotalSamples = 0;
            state.GrainLoopLength = 0;
            state.GrainReadCursor = 0d;
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

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
        private static float ApplyPaulKelletPink(ref ThrusterSynthesisState state, float white)
        {
            state.PinkB0 = 0.99886f * state.PinkB0 + white * 0.0555179f;
            state.PinkB1 = 0.99332f * state.PinkB1 + white * 0.0750759f;
            state.PinkB2 = 0.96900f * state.PinkB2 + white * 0.1538520f;
            state.PinkB3 = 0.86650f * state.PinkB3 + white * 0.3104856f;
            state.PinkB4 = 0.55000f * state.PinkB4 + white * 0.5329522f;
            state.PinkB5 = -0.7616f * state.PinkB5 - white * 0.0168980f;
            float pink = state.PinkB0 + state.PinkB1 + state.PinkB2 + state.PinkB3 + state.PinkB4 + state.PinkB5 + state.PinkB6 + white * 0.5362f;
            state.PinkB6 = white * 0.115926f;
            return pink * 0.11f;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
        private static void ComputeBandPassCoefficients(
            float centerHertz,
            float q,
            int sampleRate,
            out float b0,
            out float b1,
            out float b2,
            out float a1,
            out float a2)
        {
            float normalizedCenter = math.clamp(centerHertz, 32f, math.max(64f, sampleRate * 0.45f));
            float omega = TwoPi * normalizedCenter / math.max(sampleRate, 1);
            float sine = math.sin(omega);
            float cosine = math.cos(omega);
            float alpha = sine / (2f * math.max(0.01f, q));
            float inverseA0 = 1f / math.max(0.0001f, 1f + alpha);

            b0 = alpha * inverseA0;
            b1 = 0f;
            b2 = -alpha * inverseA0;
            a1 = (-2f * cosine) * inverseA0;
            a2 = (1f - alpha) * inverseA0;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
        private static float ProcessBiquad(
            float sample,
            float b0,
            float b1,
            float b2,
            float a1,
            float a2,
            ref float inputHistory1,
            ref float inputHistory2,
            ref float outputHistory1,
            ref float outputHistory2)
        {
            float filtered =
                b0 * sample +
                b1 * inputHistory1 +
                b2 * inputHistory2 -
                a1 * (outputHistory1 + BiquadDenormalBias) -
                a2 * (outputHistory2 + BiquadDenormalBias);

            inputHistory2 = inputHistory1;
            inputHistory1 = sample;
            outputHistory2 = outputHistory1;
            outputHistory1 = filtered;
            return filtered;
        }

        private static void GenerateMetallicGrainBank(NativeArray<float> grainBank)
        {
            if (!grainBank.IsCreated)
                return;

            double carrierPhaseA = 0d;
            double carrierPhaseB = 0d;
            double carrierPhaseC = 0d;
            double modPhase = 0d;
            float envelope = 0f;
            for (int i = 0; i < grainBank.Length; i++)
            {
                float t = grainBank.Length > 1 ? i / (float)(grainBank.Length - 1) : 0f;
                float strike = HashSigned((uint)i ^ 0xA91C52B1u);
                float friction = HeldNoise((uint)i, 2, 0x2D1A44C7u) * 0.62f + HeldNoise((uint)i, 4, 0x6B9342D1u) * 0.38f;
                envelope = math.max(envelope * 0.9986f, math.saturate(strike * strike * strike * 0.52f));
                float sweep = math.lerp(0.18f, 1f, t);
                float modulator = AdvanceSine(ref modPhase, math.lerp(31f, 187f, sweep), 1d / 48000d);
                float sample =
                    AdvanceSine(ref carrierPhaseA, math.lerp(122f, 640f, sweep), 1d / 48000d) * 0.48f +
                    AdvanceSine(ref carrierPhaseB, math.lerp(244f, 1180f, sweep), 1d / 48000d) * 0.31f +
                    AdvanceSine(ref carrierPhaseC, math.lerp(508f, 2330f, sweep), 1d / 48000d) * 0.21f;
                sample = (sample + modulator * friction * 0.45f) * (0.42f + envelope * 0.58f);
                grainBank[i] = math.tanh(sample * 2.6f);
            }
        }

        private static float HermiteSampleLoopWindow(
            NativeArray<float> buffer,
            int loopStartIndex,
            int loopLength,
            double cursor)
        {
            if (!buffer.IsCreated || buffer.Length <= 0 || loopLength <= 0)
                return 0f;

            int baseIndex = (int)math.floor(cursor);
            float t = (float)(cursor - baseIndex);
            int xm1Index = WrapLoopIndex(loopStartIndex, loopLength, baseIndex - 1);
            int x0Index = WrapLoopIndex(loopStartIndex, loopLength, baseIndex);
            int x1Index = WrapLoopIndex(loopStartIndex, loopLength, baseIndex + 1);
            int x2Index = WrapLoopIndex(loopStartIndex, loopLength, baseIndex + 2);

            float xm1 = buffer[xm1Index];
            float x0 = buffer[x0Index];
            float x1 = buffer[x1Index];
            float x2 = buffer[x2Index];

            float c0 = x0;
            float c1 = 0.5f * (x1 - xm1);
            float c2 = xm1 - 2.5f * x0 + 2f * x1 - 0.5f * x2;
            float c3 = 0.5f * (x2 - xm1) + 1.5f * (x0 - x1);
            return ((c3 * t + c2) * t + c1) * t + c0;
        }

        private static int WrapLoopIndex(int loopStartIndex, int loopLength, int index)
        {
            int wrapped = index % loopLength;
            if (wrapped < 0)
                wrapped += loopLength;

            return (loopStartIndex + wrapped) & MetallicGrainBankMask;
        }

        private static float HermiteSampleRing(NativeArray<float> buffer, double cursor, int mask)
        {
            int baseIndex = (int)math.floor(cursor);
            float t = (float)(cursor - baseIndex);
            float xm1 = buffer[(baseIndex - 1) & mask];
            float x0 = buffer[baseIndex & mask];
            float x1 = buffer[(baseIndex + 1) & mask];
            float x2 = buffer[(baseIndex + 2) & mask];

            float c0 = x0;
            float c1 = 0.5f * (x1 - xm1);
            float c2 = xm1 - 2.5f * x0 + 2f * x1 - 0.5f * x2;
            float c3 = 0.5f * (x2 - xm1) + 1.5f * (x0 - x1);
            return ((c3 * t + c2) * t + c1) * t + c0;
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
