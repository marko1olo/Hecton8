using System;
using System.Threading;
using Hecton8.AI;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Physics;
using Hecton8.Visor;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
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
    public sealed class PlayerCriticalProceduralAudioRenderer : MonoBehaviour, ITickable, ISlowTickable, ILateFrameTickable, IUpdatable, IProceduralAudioEventListener, IPhysicsImpactEventListener, ISonarPingEventListener, IAcousticEchoEventListener, ILaserCutterEventListener
    {
        private const float TwoPi = 6.28318530718f;
        private const float HullNoiseFloor = 0.0001f;
        private const float SonarChirpDurationSeconds = 0.5f;
        private const float SonarTailDurationSeconds = 3.8f;
        private const float SonarTotalDurationSeconds = 4.0f;
        private const float SoundSpeedWaterMetersPerSecond = 1480f;
        private const float PredatorKillAudioRadiusMeters = 90f;
        private const float MeteorBoomAudioRadiusMeters = 42f;
        private const float MechanicalWhirrAudioRadiusMeters = 18f;
        private const float SonarEchoReferenceDistanceMeters = 24f;
        private const float SonarEchoMaximumDistanceMeters = 1800f;
        private const float SonarEchoMaximumDelaySeconds = 2.2f;
        private const float SonarEchoAbsorptionCoefficient = 0.0035f;
        private const float ForwardEchoMinimumDistanceMeters = 50f;
        private const float ImpactEchoMinimumExcitation = 0.16f;
        private const float ImpactEchoMaximumLifetimeSeconds = 0.75f;
        private const float ImpactEchoDecayPerSecond = 6.5f;
        private const float ImpactEchoCarrierPrimaryHertz = 420f;
        private const float ImpactEchoCarrierSecondaryHertz = 860f;
        private const float ImpactEchoNoiseBlend = 0.34f;
        private const float ImpactClangMinimumExcitation = 0.12f;
        private const float ImpactClangFundamentalHertz = 150f;
        private const float ImpactClangPitchSpread = 0.22f;
        private const float ImpactClangFeedbackMinimum = 0.942f;
        private const float ImpactClangFeedbackMaximum = 0.986f;
        private const float ImpactClangEnvelopeDecayPerSecond = 4.8f;
        private const float ImpactClangLowPassBlend = 0.48f;
        private const float ImpactClangNoiseSeedDecay = 0.988f;
        private const float HullStressFmBaseCarrierHertz = 80f;
        private const float HullStressFmModulationMinimumHertz = 5f;
        private const float HullStressFmModulationMaximumHertz = 80f;
        private const float HullStressFmModulationIndexMinimum = 0.1f;
        private const float HullStressFmModulationIndexMaximum = 12f;
        private const float HullStressFmOversampleIndexThreshold = 8f;
        private const float HullStressFmOversampleLowPassQ = 0.70710677f;
        private const float HullStressFmDcBlockPole = 0.9975f;
        private const float HullStressFmMasterGain = 0.24f;
        private const float AbyssalLowPassStartDepthMeters = 4000f;
        private const float AbyssalLowPassFadeDepthMeters = 800f;
        private const float AbyssalLowPassCutoffHertz = 2000f;
        private const float PsychoacousticPressureReferenceDepthMeters = 500f;
        private const float PsychoacousticPressureMinimumCutoffHertz = 1200f;
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
        private const float DepthSubwooferBoostFrequencyHertz = 30f;
        private const float DepthSubwooferBoostStartDepthMeters = 1000f;
        private const float DepthSubwooferBoostFullDepthMeters = 6000f;
        private const float DepthSubwooferBoostMaximumGain = 0.16f;
        private const float AbyssalHullDistortionStartDepthMeters = 2200f;
        private const float AbyssalHullDistortionFullDepthMeters = 6200f;
        private const float AbyssalHullDistortionMaximumBlend = 0.24f;
        private const float AbyssalHullDistortionMaximumDrive = 2.35f;
        private const float HullLfeThreatMinimum01 = 0.12f;
        private const float HullLfeThreatRadiusMeters = 135f;
        private const float HullLfeThreatHoldSeconds = 1.35f;
        private const float HullLfeThreatStrength = 1.1f;
        private const float StructuralFatigueRingMinimumHertz = 3400f;
        private const float StructuralFatigueRingMaximumHertz = 7600f;
        private const float StructuralFatigueRingMaximumGain = 0.05f;
        private const float StructuralFatigueRingModulationHertz = 0.83f;
        private const float MasterSafetyLimiterThreshold = 0.78f;
        private const float MasterSafetyLimiterDrive = 1.95f;
        private const float AmbientCurrentBaseFrequencyHertz = 22f;
        private const float AmbientCurrentFmDepthHertz = 14f;
        private const float AmbientCurrentLowPassMinimumHertz = 90f;
        private const float AmbientCurrentLowPassMaximumHertz = 380f;
        private const float AmbientCurrentSlowLfoMinimumHertz = 0.05f;
        private const float AmbientCurrentSlowLfoMaximumHertz = 0.13f;
        private const float AmbientCurrentMasterGain = 0.12f;
        private const float ThrusterBandPassQ = 0.82f;
        private const float ThrusterBladePassFrequencyMinHertz = 22f;
        private const float ThrusterBladePassFrequencyMaxHertz = 116f;
        private const float ThrusterCombDamp = 0.22f;
        private const int BinauralOutputChannels = 2;
        private const int BinauralDelayCapacity = 128;
        private const int BinauralDelayMask = BinauralDelayCapacity - 1;
        private const int BinauralMaximumDelaySamples = 96;
        private const float BinauralAirShadowMinimumGain = 0.34f;
        private const float BinauralWaterShadowMinimumGain = 0.58f;
        private const float BinauralWaterItdDelayRatio = 0.2326f;
        private const float BinauralUnderwaterShadowCutoffHertz = 3200f;
        private const int MaxSafeFrameCapacity = 16384;
        private const int MaxFilterChannels = 8;
        private const int MaxDynamicSonarReflectorCount = 24;
        private const int AudioProducerJoinTimeoutMs = 250;
        private const int AudioProducerIdleWaitTimeoutMs = 8;
        private const int SonarEchoDelayCapacity = 131072;
        private const int SonarEchoDelayMask = SonarEchoDelayCapacity - 1;
        private const int SonarEchoTapCapacity = 12;
        private const float SonarEchoMinimumDopplerRatio = 0.05f;
        private const float SonarEchoMaximumDopplerRatio = 4f;
        private const float HermiteFractionMaximum = 0.99999994f;
        private const float SonarConeInnerRingAngleDegrees = 10f;
        private const float SonarConeOuterRingAngleDegrees = 22f;
        private const int ImpactClangDelayCapacity = 1024;
        private const int ImpactClangDelayMask = ImpactClangDelayCapacity - 1;
        private const int ThrusterCombDelayCapacity = 4096;
        private const int ThrusterCombDelayMask = ThrusterCombDelayCapacity - 1;
        private const int SabineReverbCombCount = 4;
        private const int SabineReverbDelayLineLength = 65536;
        private const int SabineReverbDelayLineMask = SabineReverbDelayLineLength - 1;
        private const int SabineReverbDelayCapacity = SabineReverbCombCount * SabineReverbDelayLineLength;
        private const float SabineReverbDelayASeconds = 0.34f;
        private const float SabineReverbDelayBSeconds = 0.42f;
        private const float SabineReverbDelayCSeconds = 0.58f;
        private const float SabineReverbDelayDSeconds = 0.71f;
        private const float SabineReverbMaximumFeedback = 0.85f;
        private const float SabineReverbMinimumFeedback = 0.18f;
        private const float SabineReverbMaximumWetGain = 0.32f;
        private const float SabineReverbDampingClosedCutoffHertz = 950f;
        private const float SabineReverbDampingOpenCutoffHertz = 2400f;
        private const float DreadRumbleMinimumHertz = 15f;
        private const float DreadRumbleMaximumHertz = 30f;
        private const float DreadRumbleMaximumGain = 0.18f;
        private const float DreadRumbleCaveBoost = 1.65f;
        private const float EnclosureDensityFollowSharpness = 4.5f;
        private const float BubbleBoilMinimumHeatFloor = 0.08f;
        private const float BubbleBoilSpawnRateMinimum = 2f;
        private const float BubbleBoilSpawnRateMaximum = 14f;
        private const float WaterHeatRatioGamma = 1.4f;
        private const float WaterDensityKilogramsPerCubicMeter = 1025f;
        private const float WaterGravityMetersPerSecondSquared = 9.81f;
        private const float WaterAmbientPressureSeaLevelPascals = 101325f;
        private const float BubbleRadiusMinimumMeters = 0.0018f;
        private const float BubbleRadiusMaximumMeters = 0.008f;
        private const float BubbleDecayMinimumPerSecond = 10f;
        private const float BubbleDecayMaximumPerSecond = 26f;
        private const float BubbleMaximumGain = 0.09f;
        private const int ImpactEventQueueCapacity = 64;
        private const int ImpactEventQueueMask = ImpactEventQueueCapacity - 1;
        private const int ImpactEventQueueSpinWatchdog = 50000;
        private const float PhysicsImpactStressRadiusMeters = 18f;
        private const float PhysicsImpactStressDecayPerSecond = 1.65f;
        private const float PhysicsImpactMetallicDecayPerSecond = 2.4f;
        private const float PhysicsImpactStressBoost = 0.55f;
        private const float PhysicsImpactMassVelocityReference = 24f;
        private const float HeartbeatBypassOxygenThreshold = 0.90f;
        private const float HeartbeatCriticalOxygenThreshold = 0.30f;
        private const float HeartbeatTerminalOxygenThreshold = 0.05f;
        private const float HeartbeatBaseBpm = 54f;
        private const float HeartbeatStressBpm = 124f;
        private const float HeartbeatSecondaryPulseDelaySeconds = 0.14f;
        private const float HeartbeatAttackSeconds = 0.008f;
        private const float HeartbeatDecaySeconds = 0.05f;
        private const float HeartbeatSustainSeconds = 0.035f;
        private const float HeartbeatReleaseSeconds = 0.11f;
        private const float HeartbeatPrimaryCarrierHertz = 56f;
        private const float HeartbeatSecondaryCarrierHertz = 92f;
        private const float HeartbeatMaximumGain = 0.22f;
        private const float HeartbeatDuckMaximum = 0.46f;
        private const float HeartbeatDuckAttackSharpness = 180f;
        private const float HeartbeatDuckReleaseSharpness = 14f;
        private const float CriticalSidechainAttackSeconds = 0.05f;
        private const float CriticalSidechainReleaseSeconds = 0.3f;
        private const float CriticalSidechainThreshold = 0.08f;
        private const float CriticalSidechainKneeWidth = 0.72f;
        private const float CriticalSidechainDuckedGain = 0.25118864f;
        private const float StructuralSnapMinimumHertz = 4200f;
        private const float StructuralSnapMaximumHertz = 9600f;
        private const float StructuralSnapDecayPerSecond = 14f;
        private const float StructuralSnapMaximumGain = 0.16f;
        private const float StructuralSnapPitchMinimum = 0.8f;
        private const float StructuralSnapPitchMaximum = 1.2f;
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
        [SerializeField] private LayerMask enclosureProbeLayers = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;

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
        // COLD ALLOC: NativeArray<float>[frameCapacity] - transient forward-echo DSP scratch - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<float> _impactEchoScratch;
        // COLD ALLOC: NativeArray<float>[frameCapacity] - thruster DSP scratch - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<float> _thrusterScratch;
        // COLD ALLOC: NativeArray<float>[frameCapacity] - psychoacoustic heartbeat DSP scratch - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<float> _heartbeatScratch;
        // COLD ALLOC: NativeArray<float>[frameCapacity] - sample-domain sidechain duck coefficients - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<float> _heartbeatDuckScratch;
        // COLD ALLOC: NativeArray<float>[frameCapacity] - procedural bubble burst scratch - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<float> _bubbleScratch;
        // COLD ALLOC: NativeArray<float>[frameCapacity] - mixed procedural audio worklet scratch - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<float> _mixScratch;
        // COLD ALLOC: NativeArray<float>[frameCapacity*2] - stereo binaural output scratch - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<float> _stereoMixScratch;
        // COLD ALLOC: NativeArray<float>[131072] - sonar Hermite echo delay ring - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<float> _sonarEchoDelay;
        // COLD ALLOC: NativeArray<RaycastCommand>[12] - active-sonar cone probe commands - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<RaycastCommand> _sonarConeCommands;
        // COLD ALLOC: NativeArray<RaycastHit>[12] - active-sonar cone probe results - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<RaycastHit> _sonarConeResults;
        // COLD ALLOC: NativeArray<SonarEchoTap>[12] - pending sonar echo tap buffer A - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<SonarEchoTap> _pendingSonarEchoTapsA;
        // COLD ALLOC: NativeArray<SonarEchoTap>[12] - pending sonar echo tap buffer B - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<SonarEchoTap> _pendingSonarEchoTapsB;
        // COLD ALLOC: NativeArray<double>[12] - sonar echo Hermite cursors per tap - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<double> _sonarEchoReadCursors;
        // COLD ALLOC: NativeArray<float>[12] - sonar echo low-pass x1 state per tap - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<float> _sonarEchoFilterInput1;
        // COLD ALLOC: NativeArray<float>[12] - sonar echo low-pass x2 state per tap - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<float> _sonarEchoFilterInput2;
        // COLD ALLOC: NativeArray<float>[12] - sonar echo low-pass y1 state per tap - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<float> _sonarEchoFilterOutput1;
        // COLD ALLOC: NativeArray<float>[12] - sonar echo low-pass y2 state per tap - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<float> _sonarEchoFilterOutput2;
        // COLD ALLOC: NativeArray<float>[1024] - Karplus-Strong delay line for metallic impact synthesis - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<float> _impactClangDelay;
        // COLD ALLOC: NativeArray<float>[4096] - thruster comb filter delay ring - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<float> _thrusterCombDelay;
        // COLD ALLOC: NativeArray<float>[262144] - 1,048,576 bytes fixed four-comb Sabine reverb delay field - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<float> _sabineReverbDelay;
        // COLD ALLOC: NativeArray<float>[128] - binaural ITD mono delay ring - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<float> _binauralDelayRing;
        // COLD ALLOC: NativeArray<float>[2] - binaural shadow low-pass history per ear - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<float> _binauralShadowHistory;
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
        // COLD ALLOC: ManualResetEventSlim[1] - producer-thread wake fence - owner: PlayerCriticalProceduralAudioRenderer
        private readonly ManualResetEventSlim _audioProducerWakeSignal = new(false);
        private int _frameCapacity;
        private int _sampleRate;
        private bool _buffersInitialized;
        private bool _registered;
        private bool _slowTickRegistered;
        private bool _lateFrameRegistered;
        private GameObject _boundPlayerObject;
        private Transform _boundPlayerTransform;
        private int _boundPlayerRootEntityId;
        private Rigidbody _playerRigidbody;
        private HectonSurvivalSystem _playerSurvivalSystem;
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
        private float _absoluteDepthTickValue;
        private float _thrusterBlendTickValue;
        private float _thrusterLoadTickValue;
        private float _thrusterPitchTickValue = 1f;
        private float _thrusterPressureTickValue;
        private float _thrusterAccelerationTickValue;
        private float _thrusterHeavyCarryTickValue;
        private float _thrusterDiveTickValue;
        private float _heartbeatStressTickValue;
        private float _heartbeatOxygenDangerTickValue;
        private float _structuralSnapTickValue;
        private float _smoothedEnclosureDensityIndex;
        private float _audioHullStressValue;
        private float _audioStructuralHullStressValue;
        private float _audioStructuralHullStressVelocityValue;
        private float _audioHullPressureDepthValue;
        private float _audioAbsoluteDepthMeters;
        private float _audioEnclosureDensityIndex;
        private float _audioBubbleBoilIntensity;
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
        private float _audioStructuralFatigueValue;
        private float _audioHeartbeatStressValue;
        private float _audioHeartbeatOxygenDangerValue;
        private float _audioStructuralSnapValue;
        private float _smoothedReverbDecayTime;
        private float _smoothedReverbWetMix;
        private float _smoothedReverbOpenness = 1f;
        private int _audioProducerRunning;
        private int _audioProducerRestartRequested;
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
        private int _audioParameterSnapshotReadIndex;
        private int _pendingSonarSequence;
        private int _impactEventReadIndex;
        private int _impactEventWriteIndex;
        private int _workerConsumedSonarSequence;
        private int _workerConsumedSonarRevision;
        private int _workerActiveSonarTapBufferIndex;
        private int _workerActiveSonarTapCount;
        private int _pendingSonarEchoTapCountA;
        private int _pendingSonarEchoTapCountB;
        private SonarTriggerState _pendingSonarStateA;
        private SonarTriggerState _pendingSonarStateB;
        private AudioParameterSnapshot _audioParameterSnapshotA;
        private AudioParameterSnapshot _audioParameterSnapshotB;
        private SonarTriggerState _workerActiveSonarState;
        private HullSynthesisState _hullSynthesisState;
        private SonarSynthesisState _sonarSynthesisState;
        private AmbientCurrentSynthesisState _ambientCurrentSynthesisState;
        private ImpactEchoSynthesisState _impactEchoSynthesisState;
        private HeartbeatSynthesisState _heartbeatSynthesisState;
        private BubbleSynthesisState _bubbleSynthesisState;
        private ThrusterSynthesisState _thrusterSynthesisState;
        private SabineReverbSynthesisState _sabineReverbSynthesisState;
        private CriticalSidechainCompressorState _criticalSidechainCompressorState;
        private long _producedSampleCount;
        private bool _nativeOutputRegistered;
        private bool _nativeOutputBridgeFailureLogged;
        private int _managedFilterFallbackEnabled;
        private int _binauralDelayWriteIndex;
        private ulong _playerBodyEntityId;
        private JobHandle _pendingSonarConeHandle;
        private bool _sonarConeQueryScheduled;
        private PendingSonarConeQuery _queuedSonarConeQuery;
        private PendingSonarConeQuery _scheduledSonarConeQuery;

        private volatile float _targetHullStressValue;
        private volatile float _targetStructuralHullStressValue;
        private volatile float _targetStructuralHullStressVelocityValue;
        private volatile float _targetStructuralFatigueValue;
        private volatile float _targetHullPressureDepthValue;
        private volatile float _targetAbsoluteDepthMeters;
        private volatile float _targetEnclosureDensityIndex;
        private volatile float _targetReverbRt60Seconds;
        private volatile float _targetReverbWetMix;
        private volatile float _targetReverbOpenness;
        private volatile float _targetBubbleBoilIntensity;
        private volatile float _targetThrusterBlendValue;
        private volatile float _targetThrusterLoadValue;
        private volatile float _targetThrusterPitchValue = 1f;
        private volatile float _targetThrusterPressureValue;
        private volatile float _targetThrusterAccelerationValue;
        private volatile float _targetThrusterHeavyCarryValue;
        private volatile float _targetThrusterDiveValue;
        private volatile float _targetAbyssalLowPassMix;
        private volatile float _targetHeartbeatStressValue;
        private volatile float _targetHeartbeatOxygenDangerValue;
        private volatile int _targetHeartbeatActive;
        private volatile float _targetStructuralSnapValue;
        private volatile float _targetBinauralAzimuthRadians;
        private volatile float _targetBinauralItdSeconds;
        private volatile float _targetBinauralShadowAmount01;
        private volatile float _targetBinauralShadowCutoffHertz;
        private volatile float _targetBinauralEnergy01;
        private volatile float _targetBinauralWaterDensityMul;
        private volatile int _targetBinauralValid;
        private PendingImpactEchoProbe _pendingImpactEchoProbe;
        private bool _laserCutterBeamActive;
        private float _laserCutterHeat01;

        // COLD ALLOC: ImpactAudioEvent[64] - main-thread physics impact bridge for the audio worker SPSC path - owner: PlayerCriticalProceduralAudioRenderer
        private readonly ImpactAudioEvent[] _impactEventQueue = new ImpactAudioEvent[ImpactEventQueueCapacity];
        // COLD ALLOC: SpatialQueryHit[24] - moving sonar reflector candidates - owner: PlayerCriticalProceduralAudioRenderer
        private readonly SpatialQueryHit[] _dynamicSonarReflectorBuffer = new SpatialQueryHit[MaxDynamicSonarReflectorCount];

        private struct SonarEchoTap
        {
            public float DelaySeconds;
            public float DopplerRatio;
            public float Attenuation;
            public float LowPassCutoffHz;
            public float LowPassB0;
            public float LowPassB1;
            public float LowPassB2;
            public float LowPassA1;
            public float LowPassA2;
            public int UseLowPass;
        }

        private struct PendingSonarConeQuery
        {
            public bool Valid;
            public int Sequence;
            public int EchoRevision;
            public long StartFrame;
            public float Intensity;
            public Vector3 Origin;
            public Vector3 Forward;
            public Vector3 Right;
            public Vector3 Up;
            public Transform IgnoreRoot;
        }

        private struct SonarTriggerState
        {
            public int Sequence;
            public int EchoRevision;
            public long StartFrame;
            public float Intensity;
            public int EchoTapCount;
        }

        internal struct AudioThreadDiagnostics
        {
            public int BufferedFrames;
            public int WritableFrames;
            public int UnderrunCount;
            public int OverflowDropCount;
            public int ProducerRunning;
            public long ProducedSampleCount;
        }

        private struct AudioParameterSnapshot
        {
            public float HullStress;
            public float StructuralHullStress;
            public float StructuralHullStressVelocity;
            public float StructuralFatigue;
            public float StructuralSnap;
            public float HullPressureDepth;
            public float AbsoluteDepthMeters;
            public float EnclosureDensityIndex;
            public float ReverbRt60Seconds;
            public float ReverbWetMix;
            public float ReverbOpenness;
            public float BubbleBoilIntensity;
            public float ThrusterBlend;
            public float ThrusterLoad;
            public float ThrusterPitch;
            public float ThrusterPressure;
            public float ThrusterAcceleration;
            public float ThrusterHeavyCarry;
            public float ThrusterDive;
            public float AbyssalLowPassMix;
            public float HeartbeatStress;
            public float HeartbeatOxygenDanger;
            public int HeartbeatActive;
            public float BinauralAzimuthRadians;
            public float BinauralItdSeconds;
            public float BinauralShadowAmount01;
            public float BinauralShadowCutoffHertz;
            public float BinauralEnergy01;
            public float BinauralWaterDensityMul;
            public int BinauralValid;
        }

        private struct ImpactAudioEvent
        {
            public float Stress;
            public float Metallic;
            public float ClangExcitation;
            public float EchoExcitation;
            public float EchoDelaySeconds;
            public float EchoAttenuation;
            public float EchoLowPassCutoffHz;
            public float EchoPitchScale;
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
            public double StressFmModulatorPhase;
            public double StressFmCarrierPhase;
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
            public double DepthSubwooferPhase;
            public double DreadRumblePhase;
            public double FatigueRingCarrierPhase;
            public double FatigueRingModulationPhase;
            public float StructuralSnapEnvelope;
            public double StructuralSnapPhase;
            public float StructuralSnapPitchScale;
            public float ImpactClangEnvelope;
            public float ImpactClangFeedback;
            public float ImpactClangLowPassState;
            public int ImpactClangDelaySamples;
            public int ImpactClangWriteIndex;
            public float StressFmDcBlockInput;
            public float StressFmDcBlockOutput;
            public float StressFmOversampleInput1;
            public float StressFmOversampleInput2;
            public float StressFmOversampleOutput1;
            public float StressFmOversampleOutput2;
        }

#pragma warning disable 0649 // DSP state fields are intentionally zero-initialized and written by the procedural audio integrator.
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
#pragma warning restore 0649

        private struct AmbientCurrentSynthesisState
        {
            public double CarrierPhase;
            public double ModulatorPhase;
            public double SlowPhase;
            public double NoisePhase;
            public float LowPassState;
            public float BandPassState;
        }

        private struct ImpactEchoSynthesisState
        {
            public float DelayRemainingSeconds;
            public float Excitation;
            public float Attenuation;
            public float LowPassCutoffHz;
            public float LowPassState;
            public float ElapsedSeconds;
            public float PitchScale;
            public double CarrierPhaseA;
            public double CarrierPhaseB;
        }

        private struct HeartbeatSynthesisState
        {
            public float TimeToNextBeatSeconds;
            public float SecondaryPulseDelaySeconds;
            public float PrimaryPulseAgeSeconds;
            public float SecondaryPulseAgeSeconds;
            public float DuckEnvelope;
            public double CarrierPhase;
            public double HarmonicPhase;
        }

        private struct BubbleSynthesisState
        {
            public float TimeToNextSpawnSeconds;
            public float Envelope;
            public float FrequencyHertz;
            public float DecayPerSecond;
            public double Phase;
            public uint SpawnSeed;
        }

        private struct CriticalSidechainCompressorState
        {
            public float Envelope;
            public float Gain;
        }

        private struct PendingImpactEchoProbe
        {
            public bool Valid;
            public float Excitation;
            public float ExpireAt;
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
            public double CavitationCarrierPhase;
            public double CavitationModulatorPhase;
        }

        private struct SabineReverbSynthesisState
        {
            public int CombAWriteIndex;
            public int CombBWriteIndex;
            public int CombCWriteIndex;
            public int CombDWriteIndex;
            public float CombADampingState;
            public float CombBDampingState;
            public float CombCDampingState;
            public float CombDDampingState;
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
            PhysicsEvents.Register(this);
            ProceduralAudioEvents.Register(this);
            SpectrumEvents.RegisterSonarPingListener(this);
            SpectrumEvents.RegisterAcousticEchoListener(this);
            LaserCutterEvents.Register(this);
            Volatile.Write(ref _managedFilterFallbackEnabled, 1);
            TryRegister();
            TryBindFromBootstrap();
            StartAudioProducerThread();
        }

        private void OnDisable()
        {
            Volatile.Write(ref _managedFilterFallbackEnabled, 0);
            LaserCutterEvents.Unregister(this);
            SpectrumEvents.UnregisterAcousticEchoListener(this);
            SpectrumEvents.UnregisterSonarPingListener(this);
            ProceduralAudioEvents.Unregister(this);
            PhysicsEvents.Unregister(this);
            AudioSettings.OnAudioConfigurationChanged -= HandleAudioConfigurationChanged;
            UnsubscribeTransportCoordinator();
            TryUnregister();
            bool producerStopped = StopAudioProducerThread();
            RestoreListenerReverbDefaults();
            if (producerStopped)
            {
                ClearNativeOutputBridge();
                _sampleRingBuffer?.Clear();
            }
            else
            {
                ClearNativeOutputBridge();
            }

            AcousticOcclusionUtility.ReleaseRuntime();
            if (producerStopped)
                ClearLowPassState();
        }

        private void OnDestroy()
        {
            LaserCutterEvents.Unregister(this);
            SpectrumEvents.UnregisterAcousticEchoListener(this);
            SpectrumEvents.UnregisterSonarPingListener(this);
            bool producerStopped = StopAudioProducerThread();
            _audioProducerWakeSignal.Set();
            Thread producerThread = _audioProducerThread;
            if (producerStopped || producerThread == null || !producerThread.IsAlive)
                _audioProducerWakeSignal.Dispose();

            if (producerStopped)
            {
                DisposeBuffers(disposeSabineReverbDelay: true);
            }
            else
            {
                ClearNativeOutputBridge();
            }

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

            // Stereo ITD/ILD is rendered into interleaved left/right frames on the worker thread.
            // The managed filter path only transfers that channel ordering into Unity's output buffer.
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
            _boundPlayerRootEntityId = 0;
            _playerBodyEntityId = 0ul;
            if (playerObject == null)
            {
                UnsubscribeTransportCoordinator();
                _structuralHullReadModel = null;
                _activeTransportLifecycleOwner = null;
                _playerSurvivalSystem = null;
                return;
            }

            Transform playerRoot = _boundPlayerTransform != null ? _boundPlayerTransform.root : null;
            if (playerRoot != null)
                _boundPlayerRootEntityId = unchecked((int)EntityId.ToULong(playerRoot.GetEntityId()));

            if (playerMovement == null || !ReferenceEquals(playerMovement.gameObject, playerObject))
                playerObject.TryGetComponent(out playerMovement);

            if (playerToolManager == null || !ReferenceEquals(playerToolManager.gameObject, playerObject))
                playerObject.TryGetComponent(out playerToolManager);

            if (playerTransportCoordinator == null || !ReferenceEquals(playerTransportCoordinator.gameObject, playerObject))
                playerObject.TryGetComponent(out playerTransportCoordinator);

            if (_playerRigidbody == null || !ReferenceEquals(_playerRigidbody.gameObject, playerObject))
                playerObject.TryGetComponent(out _playerRigidbody);

            if (_playerSurvivalSystem == null || !ReferenceEquals(_playerSurvivalSystem.gameObject, playerObject))
                playerObject.TryGetComponent(out _playerSurvivalSystem);

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
                _targetStructuralFatigueValue = 0f;
                _targetHullPressureDepthValue = 0f;
                _targetAbsoluteDepthMeters = 0f;
                _targetEnclosureDensityIndex = 0f;
                _targetReverbRt60Seconds = 0f;
                _targetReverbWetMix = 0f;
                _targetReverbOpenness = 1f;
                _targetBubbleBoilIntensity = 0f;
                _targetThrusterBlendValue = 0f;
                _targetThrusterLoadValue = 0f;
                _targetThrusterPitchValue = 1f;
                _targetThrusterPressureValue = 0f;
                _targetThrusterAccelerationValue = 0f;
                _targetThrusterHeavyCarryValue = 0f;
                _targetThrusterDiveValue = 0f;
                _targetAbyssalLowPassMix = 0f;
                _targetHeartbeatStressValue = 0f;
                _targetHeartbeatOxygenDangerValue = 0f;
                _targetHeartbeatActive = 0;
                _targetStructuralSnapValue = 0f;
                _targetBinauralAzimuthRadians = 0f;
                _targetBinauralItdSeconds = 0f;
                _targetBinauralShadowAmount01 = 0f;
                _targetBinauralShadowCutoffHertz = 22000f;
                _targetBinauralEnergy01 = 0f;
                _targetBinauralWaterDensityMul = 0f;
                _targetBinauralValid = 0;
                _pendingImpactEchoProbe = default;
                _lastSpeed = 0f;
                _impactStressImpulseTickValue = 0f;
                _hullPressureDepthTickValue = 0f;
                _absoluteDepthTickValue = 0f;
                _heartbeatStressTickValue = 0f;
                _heartbeatOxygenDangerTickValue = 0f;
                _structuralSnapTickValue = 0f;
                PublishAudioParameterSnapshot();
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
            _targetStructuralFatigueValue = ResolveStructuralFatigue01();
            _structuralSnapTickValue = math.lerp(
                _structuralSnapTickValue,
                ResolveStructuralDamageTransient01(),
                structuralBlendT);
            _targetStructuralSnapValue = _structuralSnapTickValue;
            _hullPressureDepthTickValue = ResolveHullPressureDepth01(playerMovement.CurrentDepth);
            _targetHullPressureDepthValue = _hullPressureDepthTickValue;
            _absoluteDepthTickValue = ResolveAbsoluteDepthMeters();
            _targetAbsoluteDepthMeters = _absoluteDepthTickValue;
            _targetAbyssalLowPassMix = ResolveAbyssalLowPassTarget(playerMovement.CurrentDepth);
            UpdateBubbleBoilTargets();
            UpdateSurvivalTargets(deltaTime);

            UpdateThrusterTargets(deltaTime);
            UpdateBinauralTargets();
            UpdateAcousticThreatPulse();
            TryResolvePendingImpactEchoProbe();
            PublishAudioParameterSnapshot();
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

        /// <summary>
        /// Recovers active sonar raycast batches in the dispatcher-owned late-frame window.
        /// </summary>
        public void LateFrameTick()
        {
            TryConsumeCompletedSonarConeQuery();
            if (!_sonarConeQueryScheduled && _queuedSonarConeQuery.Valid)
                ScheduleQueuedSonarConeQuery();
        }

        private void StartAudioProducerThread()
        {
            Thread producerThread = _audioProducerThread;
            if (producerThread != null)
            {
                if (producerThread.IsAlive)
                {
                    Interlocked.Exchange(ref _audioProducerRestartRequested, 1);
                    Interlocked.Exchange(ref _audioProducerRunning, 1);
                    SignalAudioProducerThread();
                    if (!producerThread.Join(0))
                        return;

                    Interlocked.Exchange(ref _audioProducerRestartRequested, 0);
                    Interlocked.Exchange(ref _audioProducerRunning, 0);
                }

                _audioProducerThread = null;
            }

            if (Interlocked.CompareExchange(ref _audioProducerRunning, 1, 0) != 0)
                return;

            Interlocked.Exchange(ref _audioProducerRestartRequested, 0);
            _audioProducerThread = new Thread(AudioProducerLoop)
            {
                IsBackground = true,
                Name = "Hecton8ProceduralAudioProducer",
                Priority = System.Threading.ThreadPriority.AboveNormal
            };
            _audioProducerThread.Start();
            SignalAudioProducerThread();
        }

        private bool StopAudioProducerThread()
        {
            Interlocked.Exchange(ref _audioProducerRestartRequested, 0);
            if (Interlocked.Exchange(ref _audioProducerRunning, 0) == 0)
                return !IsAudioProducerThreadAlive();

            SignalAudioProducerThread();
            Thread producerThread = _audioProducerThread;
            if (producerThread == null)
                return true;

            if (producerThread.Join(AudioProducerJoinTimeoutMs))
            {
                _audioProducerThread = null;
                return true;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[PlayerCriticalProceduralAudioRenderer] Audio producer thread failed to stop within watchdog budget. Native audio buffers remain owned until the worker exits.");
#endif
            return false;
        }

        private bool IsAudioProducerThreadAlive()
        {
            Thread producerThread = _audioProducerThread;
            return producerThread != null && producerThread.IsAlive;
        }

        private void AudioProducerLoop()
        {
            while (true)
            {
                while (Volatile.Read(ref _audioProducerRunning) != 0)
                {
                    if (TryResolveAudioProducerWork(out int blockFrames))
                    {
                        ProduceAudioBlock(blockFrames);
                        continue;
                    }

                    _audioProducerWakeSignal.Reset();
                    if (TryResolveAudioProducerWork(out blockFrames))
                    {
                        ProduceAudioBlock(blockFrames);
                        continue;
                    }

                    _audioProducerWakeSignal.Wait(AudioProducerIdleWaitTimeoutMs);
                }

                if (Interlocked.Exchange(ref _audioProducerRestartRequested, 0) == 0)
                    return;

                Interlocked.Exchange(ref _audioProducerRunning, 1);
            }
        }

        private bool TryResolveAudioProducerWork(out int blockFrames)
        {
            blockFrames = 0;
            AudioFrameSpscRingBuffer sampleRingBuffer = _sampleRingBuffer;
            if (!_buffersInitialized || sampleRingBuffer == null || !sampleRingBuffer.IsCreated || _frameCapacity <= 0)
                return false;

            int resolvedBlockFrames = math.clamp(synthesisBlockFrames, 256, _frameCapacity);
            int targetLeadFrames = math.clamp(
                workerTargetLeadFrames,
                resolvedBlockFrames,
                math.max(resolvedBlockFrames, sampleRingBuffer.CapacityFrames - resolvedBlockFrames));

            sampleRingBuffer.GetState(out int bufferedFrames, out int writableFrames);
            if (bufferedFrames >= targetLeadFrames || writableFrames < resolvedBlockFrames)
                return false;

            blockFrames = resolvedBlockFrames;
            return true;
        }

        private void SignalAudioProducerThread()
        {
            _audioProducerWakeSignal.Set();
        }

        internal bool TryGetAudioThreadDiagnostics(out AudioThreadDiagnostics diagnostics)
        {
            diagnostics = default;
            AudioFrameSpscRingBuffer sampleRingBuffer = _sampleRingBuffer;
            if (sampleRingBuffer == null || !sampleRingBuffer.IsCreated)
                return false;

            sampleRingBuffer.GetState(out diagnostics.BufferedFrames, out diagnostics.WritableFrames);
            diagnostics.UnderrunCount = sampleRingBuffer.UnderrunCount;
            diagnostics.OverflowDropCount = sampleRingBuffer.OverflowDropCount;
            diagnostics.ProducerRunning = Volatile.Read(ref _audioProducerRunning);
            diagnostics.ProducedSampleCount = Interlocked.Read(ref _producedSampleCount);
            return true;
        }

        private void ProduceAudioBlock(int frameCount)
        {
            if (frameCount <= 0 ||
                !_hullScratch.IsCreated ||
                !_sonarScratch.IsCreated ||
                !_impactEchoScratch.IsCreated ||
                !_thrusterScratch.IsCreated ||
                !_heartbeatScratch.IsCreated ||
                !_heartbeatDuckScratch.IsCreated ||
                !_bubbleScratch.IsCreated ||
                !_mixScratch.IsCreated ||
                _sampleRingBuffer == null)
            {
                return;
            }

            long blockStartFrame = Interlocked.Read(ref _producedSampleCount);
            TryConsumePendingSonarTrigger(blockStartFrame, frameCount);
            int parameterReadIndex = Volatile.Read(ref _audioParameterSnapshotReadIndex);
            AudioParameterSnapshot parameters = parameterReadIndex == 0 ? _audioParameterSnapshotA : _audioParameterSnapshotB;

            double invSampleRate = 1d / math.max(1, _sampleRate);
            ConsumePendingImpactAudioEvents(frameCount, invSampleRate, out float impactStressTarget, out float impactMetallicTarget);
            float hullTarget = math.saturate(math.max(parameters.HullStress, impactStressTarget));
            float structuralHullTarget = math.saturate(parameters.StructuralHullStress);
            float structuralHullVelocityTarget = math.saturate(parameters.StructuralHullStressVelocity);
            float structuralFatigueTarget = math.saturate(parameters.StructuralFatigue);
            float structuralSnapTarget = math.saturate(parameters.StructuralSnap);
            float hullDepthTarget = math.saturate(parameters.HullPressureDepth);
            float absoluteDepthTarget = math.max(0f, parameters.AbsoluteDepthMeters);
            float enclosureDensityTarget = math.saturate(parameters.EnclosureDensityIndex);
            float bubbleBoilTarget = math.saturate(parameters.BubbleBoilIntensity);
            float thrusterBlendTarget = math.saturate(parameters.ThrusterBlend);
            float thrusterLoadTarget = math.saturate(parameters.ThrusterLoad);
            float thrusterPitchTarget = math.max(0.1f, parameters.ThrusterPitch);
            float thrusterPressureTarget = math.saturate(parameters.ThrusterPressure);
            float thrusterAccelerationTarget = math.saturate(parameters.ThrusterAcceleration);
            float thrusterHeavyCarryTarget = math.saturate(parameters.ThrusterHeavyCarry);
            float thrusterDiveTarget = math.saturate(parameters.ThrusterDive);
            float heartbeatStressTarget = math.saturate(parameters.HeartbeatStress);
            float heartbeatOxygenDangerTarget = math.saturate(parameters.HeartbeatOxygenDanger);
            bool heartbeatActiveTarget = parameters.HeartbeatActive != 0;

            RenderHullStressBlock(
                frameCount,
                blockStartFrame,
                invSampleRate,
                hullTarget,
                structuralHullTarget,
                structuralHullVelocityTarget,
                structuralFatigueTarget,
                structuralSnapTarget,
                hullDepthTarget,
                absoluteDepthTarget,
                enclosureDensityTarget,
                impactMetallicTarget);
            RenderSonarBlock(frameCount, blockStartFrame, invSampleRate);
            RenderImpactEchoBlock(frameCount, invSampleRate);
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
            RenderHeartbeatBlock(
                frameCount,
                invSampleRate,
                heartbeatActiveTarget,
                heartbeatStressTarget,
                heartbeatOxygenDangerTarget);
            RenderBubbleBlock(
                frameCount,
                blockStartFrame,
                invSampleRate,
                bubbleBoilTarget,
                absoluteDepthTarget);
            MixAndFilterBlock(frameCount, blockStartFrame, invSampleRate, parameters);
            ApplyBinauralSpatializationBlock(frameCount, parameters);

            if (_sampleRingBuffer.TryWriteInterleaved(_stereoMixScratch, frameCount, BinauralOutputChannels))
                Interlocked.Add(ref _producedSampleCount, frameCount);
        }

        private void TryConsumePendingSonarTrigger(long blockStartFrame, int frameCount)
        {
            int activeIndex = Volatile.Read(ref _pendingSonarStateReadIndex);
            SonarTriggerState pendingState = activeIndex == 0 ? _pendingSonarStateA : _pendingSonarStateB;
            if (pendingState.Sequence == 0)
                return;

            bool isNewSequence = pendingState.Sequence != _workerConsumedSonarSequence;
            bool isNewRevision = pendingState.Sequence == _workerConsumedSonarSequence &&
                                 pendingState.EchoRevision != _workerConsumedSonarRevision;
            if (!isNewSequence && !isNewRevision)
                return;

            long blockEndFrameExclusive = blockStartFrame + frameCount;
            if (isNewSequence && pendingState.StartFrame >= blockEndFrameExclusive)
                return;

            _workerConsumedSonarSequence = pendingState.Sequence;
            _workerConsumedSonarRevision = pendingState.EchoRevision;
            _workerActiveSonarState = pendingState;
            _workerActiveSonarTapBufferIndex = activeIndex;
            _workerActiveSonarTapCount = activeIndex == 0 ? _pendingSonarEchoTapCountA : _pendingSonarEchoTapCountB;
            if (isNewSequence)
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
                float targetDensityIndex = ResolveEnclosureDensityIndex(in enclosure);
                float densityBlendT = 1f - math.exp(-math.max(EnclosureDensityFollowSharpness, 0.01f) * deltaTime);
                _smoothedEnclosureDensityIndex = math.lerp(_smoothedEnclosureDensityIndex, targetDensityIndex, densityBlendT);
                _targetEnclosureDensityIndex = _smoothedEnclosureDensityIndex;
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
            _targetReverbRt60Seconds = _smoothedReverbDecayTime;
            _targetReverbWetMix = _smoothedReverbWetMix;
            _targetReverbOpenness = _smoothedReverbOpenness;
            ApplyListenerReverbProfile(_smoothedReverbWetMix, _smoothedReverbDecayTime, _smoothedReverbOpenness);
        }

        private void ResetReverbModelState()
        {
            _smoothedReverbDecayTime = openWaterDecayTime;
            _smoothedReverbWetMix = 0f;
            _smoothedReverbOpenness = 1f;
            _smoothedEnclosureDensityIndex = 0f;
            _targetEnclosureDensityIndex = 0f;
            _targetReverbRt60Seconds = 0f;
            _targetReverbWetMix = 0f;
            _targetReverbOpenness = 1f;
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
            long producerFrame = Interlocked.Read(ref _producedSampleCount);
            long scheduledStartFrame = producerFrame;
            int sequence = Interlocked.Increment(ref _pendingSonarSequence);
            int echoRevision = 1;

            ResolveSonarEchoModel(
                out float echoDelaySeconds,
                out float echoDopplerRatio,
                out float echoAttenuation,
                out float echoLowPassCutoffHz);

            int inactiveIndex = 1 - Volatile.Read(ref _pendingSonarStateReadIndex);
            NativeArray<SonarEchoTap> inactiveTapBuffer = inactiveIndex == 0 ? _pendingSonarEchoTapsA : _pendingSonarEchoTapsB;
            int fallbackTapCount = 0;
            if (inactiveTapBuffer.IsCreated)
            {
                SonarEchoTap fallbackTap = BuildSonarEchoTap(
                    math.clamp(echoDelaySeconds, 0f, SonarEchoMaximumDelaySeconds),
                    math.clamp(echoDopplerRatio, SonarEchoMinimumDopplerRatio, SonarEchoMaximumDopplerRatio),
                    math.clamp(echoAttenuation, 0f, 1f),
                    math.clamp(
                        echoLowPassCutoffHz,
                        AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                        AcousticOcclusionUtility.OpenLowPassCutoffHertz));
                inactiveTapBuffer[0] = fallbackTap;
                fallbackTapCount = 1;
            }

            SonarTriggerState pendingState = new SonarTriggerState
            {
                Sequence = sequence,
                EchoRevision = echoRevision,
                StartFrame = scheduledStartFrame,
                Intensity = math.saturate(intensity),
                EchoTapCount = fallbackTapCount
            };
            PublishPendingSonarState(inactiveIndex, pendingState, fallbackTapCount);
            TryQueueActiveSonarConeQuery(sequence, echoRevision + 1, scheduledStartFrame, math.saturate(intensity));

            ProceduralAudioEvents.RaiseAudioPingTriggered(
                scheduledStartFrame,
                math.max(_sampleRate, 1),
                math.saturate(intensity),
                SonarChirpDurationSeconds);
        }

        private void HandleAcousticEchoReturned(AcousticEchoEvent echoEvent)
        {
            if (echoEvent.ReturnStrength <= 0.0001f)
                return;

            if (_boundPlayerTransform == null)
                TryBindFromBootstrap();

            float resonanceScale = math.clamp(echoEvent.Resonance, 0.65f, 1.45f);
            float resonance01 = math.saturate((resonanceScale - 0.65f) / 0.8f);
            float roundTripDistance = math.max(0f, echoEvent.DistanceMeters) * 2f;
            float echoDelaySeconds = math.clamp(roundTripDistance / SoundSpeedWaterMetersPerSecond, 0f, SonarEchoMaximumDelaySeconds);
            float echoAttenuation = math.saturate(math.exp(-roundTripDistance * SonarEchoAbsorptionCoefficient));
            float echoExcitation = math.saturate(
                echoEvent.ReturnStrength *
                math.lerp(0.65f, 1.2f, resonance01) *
                math.max(0.2f, echoAttenuation));
            float echoLowPassCutoffHz = math.lerp(1450f, AcousticOcclusionUtility.OpenLowPassCutoffHertz, resonance01);
            TryEnqueueImpactAudioEvent(
                0f,
                0f,
                0f,
                echoExcitation,
                echoDelaySeconds,
                echoAttenuation,
                echoLowPassCutoffHz,
                resonanceScale);
        }

        void ISonarPingEventListener.OnSonarPingSent(float intensity)
        {
            HandleSonarPingSent(intensity);
        }

        void IAcousticEchoEventListener.OnAcousticEchoReturned(in AcousticEchoEvent echoEvent)
        {
            HandleAcousticEchoReturned(echoEvent);
        }

        private void HandleAudioConfigurationChanged(bool deviceWasChanged)
        {
            RefreshAudioConfiguration();
        }

        private void PublishPendingSonarState(int inactiveIndex, SonarTriggerState pendingState, int tapCount)
        {
            if (inactiveIndex == 0)
            {
                _pendingSonarEchoTapCountA = tapCount;
                _pendingSonarStateA = pendingState;
            }
            else
            {
                _pendingSonarEchoTapCountB = tapCount;
                _pendingSonarStateB = pendingState;
            }

            Interlocked.Exchange(ref _pendingSonarStateReadIndex, inactiveIndex);
            SignalAudioProducerThread();
        }

        private void TryQueueActiveSonarConeQuery(int sequence, int echoRevision, long startFrame, float intensity)
        {
            if (_sonarConeQueryScheduled || _queuedSonarConeQuery.Valid)
                return;

            if (!TryResolveForwardEchoProbe(out Vector3 probeOrigin, out Vector3 probeDirection, out Transform probeIgnoreRoot))
                return;

            Transform orientationTransform = probeIgnoreRoot;
            Vector3 right = orientationTransform != null ? orientationTransform.right : Vector3.right;
            Vector3 up = orientationTransform != null ? orientationTransform.up : Vector3.up;
            if (right.sqrMagnitude <= 0.0001f || up.sqrMagnitude <= 0.0001f)
            {
                right = Vector3.right;
                up = Vector3.up;
            }

            _queuedSonarConeQuery = new PendingSonarConeQuery
            {
                Valid = true,
                Sequence = sequence,
                EchoRevision = echoRevision,
                StartFrame = startFrame,
                Intensity = intensity,
                Origin = probeOrigin,
                Forward = probeDirection.normalized,
                Right = right.normalized,
                Up = up.normalized,
                IgnoreRoot = probeIgnoreRoot
            };
        }

        private void TryConsumeCompletedSonarConeQuery()
        {
            if (!_sonarConeQueryScheduled || !_scheduledSonarConeQuery.Valid)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _pendingSonarConeHandle, forceComplete: false))
                return;

            _sonarConeQueryScheduled = false;

            int inactiveIndex = 1 - Volatile.Read(ref _pendingSonarStateReadIndex);
            NativeArray<SonarEchoTap> inactiveTapBuffer = inactiveIndex == 0 ? _pendingSonarEchoTapsA : _pendingSonarEchoTapsB;
            int tapCount = 0;

            if (inactiveTapBuffer.IsCreated)
            {
                Vector3 listenerVelocity = _playerRigidbody != null ? _playerRigidbody.linearVelocity : Vector3.zero;
                for (int rayIndex = 0; rayIndex < SonarEchoTapCapacity; rayIndex++)
                {
                    if (!TryBuildSonarEchoTapFromRayResult(
                            _scheduledSonarConeQuery,
                            listenerVelocity,
                            rayIndex,
                            out SonarEchoTap tap))
                    {
                        continue;
                    }

                    inactiveTapBuffer[tapCount] = tap;
                    tapCount++;
                }

                SortSonarEchoTapsByDelay(inactiveTapBuffer, tapCount);
            }

            if (tapCount > 0)
            {
                SonarTriggerState resolvedState = new SonarTriggerState
                {
                    Sequence = _scheduledSonarConeQuery.Sequence,
                    EchoRevision = _scheduledSonarConeQuery.EchoRevision,
                    StartFrame = _scheduledSonarConeQuery.StartFrame,
                    Intensity = _scheduledSonarConeQuery.Intensity,
                    EchoTapCount = tapCount
                };
                PublishPendingSonarState(inactiveIndex, resolvedState, tapCount);
            }

            _scheduledSonarConeQuery = default;
        }

        private void ScheduleQueuedSonarConeQuery()
        {
            if (!_queuedSonarConeQuery.Valid || !_sonarConeCommands.IsCreated || !_sonarConeResults.IsCreated)
                return;

            QueryParameters parameters = new QueryParameters(_resolvedAcousticOcclusionLayerMask, false, QueryTriggerInteraction.Ignore);
            for (int rayIndex = 0; rayIndex < SonarEchoTapCapacity; rayIndex++)
            {
                Vector3 direction = ResolveSonarConeDirection(
                    rayIndex,
                    _queuedSonarConeQuery.Forward,
                    _queuedSonarConeQuery.Right,
                    _queuedSonarConeQuery.Up);
                _sonarConeCommands[rayIndex] = new RaycastCommand(
                    _queuedSonarConeQuery.Origin,
                    direction,
                    parameters,
                    SonarEchoMaximumDistanceMeters);
                _sonarConeResults[rayIndex] = default;
            }

            _scheduledSonarConeQuery = _queuedSonarConeQuery;
            _queuedSonarConeQuery = default;
            _pendingSonarConeHandle = RaycastCommand.ScheduleBatch(_sonarConeCommands, _sonarConeResults, 1, default);
            _sonarConeQueryScheduled = true;
        }

        private bool TryBuildSonarEchoTapFromRayResult(
            in PendingSonarConeQuery query,
            Vector3 listenerVelocity,
            int rayIndex,
            out SonarEchoTap tap)
        {
            tap = default;
            if ((uint)rayIndex >= SonarEchoTapCapacity || !_sonarConeResults.IsCreated)
                return false;

            RaycastHit hit = _sonarConeResults[rayIndex];
            Collider collider = hit.collider;
            if (collider == null)
                return false;

            Transform hitRoot = collider.transform != null ? collider.transform.root : null;
            if (query.IgnoreRoot != null && hitRoot == query.IgnoreRoot)
                return false;

            float hitDistanceMeters = math.clamp(hit.distance, ForwardEchoMinimumDistanceMeters, SonarEchoMaximumDistanceMeters);
            AcousticSurfaceResponse response = AcousticOcclusionUtility.ResolveSurfaceResponse(collider);
            float travelDistanceMeters = hitDistanceMeters * 2f;
            float transmissionLossDb =
                (20f * math.log10(math.max(travelDistanceMeters, MinimumProbeDistanceMeters))) +
                (SonarEchoAbsorptionCoefficient * travelDistanceMeters);
            float attenuation = math.clamp(
                math.pow(10f, -transmissionLossDb / 20f) * response.Transmission01,
                0f,
                0.95f);
            if (attenuation <= 0.0001f)
                return false;

            Vector3 direction = ResolveSonarConeDirection(rayIndex, query.Forward, query.Right, query.Up);
            float radialVelocity = Vector3.Dot(listenerVelocity, direction);
            float clampedRadialVelocity = math.clamp(
                radialVelocity,
                -SoundSpeedWaterMetersPerSecond * 0.9f,
                SoundSpeedWaterMetersPerSecond * 0.9f);
            float dopplerDenominator = math.max(
                MinimumProbeDistanceMeters,
                SoundSpeedWaterMetersPerSecond - clampedRadialVelocity);
            float dopplerRatio = math.clamp(
                (SoundSpeedWaterMetersPerSecond + clampedRadialVelocity) / dopplerDenominator,
                SonarEchoMinimumDopplerRatio,
                SonarEchoMaximumDopplerRatio);
            float delaySeconds = math.min(travelDistanceMeters / SoundSpeedWaterMetersPerSecond, SonarEchoMaximumDelaySeconds);
            tap = BuildSonarEchoTap(delaySeconds, dopplerRatio, attenuation, response.LowPassCutoffHz);
            return true;
        }

        private SonarEchoTap BuildSonarEchoTap(
            float delaySeconds,
            float dopplerRatio,
            float attenuation,
            float lowPassCutoffHz)
        {
            SonarEchoTap tap = new SonarEchoTap
            {
                DelaySeconds = math.clamp(delaySeconds, 0f, SonarEchoMaximumDelaySeconds),
                DopplerRatio = math.clamp(dopplerRatio, SonarEchoMinimumDopplerRatio, SonarEchoMaximumDopplerRatio),
                Attenuation = math.saturate(attenuation),
                LowPassCutoffHz = math.clamp(
                    lowPassCutoffHz,
                    AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                    AcousticOcclusionUtility.OpenLowPassCutoffHertz)
            };

            if (tap.LowPassCutoffHz < math.min(AcousticOcclusionUtility.OpenLowPassCutoffHertz, _sampleRate * 0.45f) - 1f)
            {
                ComputeLowPassCoefficients(
                    tap.LowPassCutoffHz,
                    out tap.LowPassB0,
                    out tap.LowPassB1,
                    out tap.LowPassB2,
                    out tap.LowPassA1,
                    out tap.LowPassA2);
                tap.UseLowPass = 1;
            }
            else
            {
                tap.LowPassB0 = 0f;
                tap.LowPassB1 = 0f;
                tap.LowPassB2 = 0f;
                tap.LowPassA1 = 0f;
                tap.LowPassA2 = 0f;
                tap.UseLowPass = 0;
            }

            return tap;
        }

        private static void SortSonarEchoTapsByDelay(NativeArray<SonarEchoTap> taps, int tapCount)
        {
            for (int i = 1; i < tapCount; i++)
            {
                SonarEchoTap value = taps[i];
                int insertIndex = i - 1;
                while (insertIndex >= 0 && taps[insertIndex].DelaySeconds > value.DelaySeconds)
                {
                    taps[insertIndex + 1] = taps[insertIndex];
                    insertIndex--;
                }

                taps[insertIndex + 1] = value;
            }
        }

        private static Vector3 ResolveSonarConeDirection(int rayIndex, Vector3 forward, Vector3 right, Vector3 up)
        {
            switch (rayIndex)
            {
                case 0: return NormalizeConeDirection(forward, right, up, 0f, 0f);
                case 1: return NormalizeConeDirection(forward, right, up, SonarConeInnerRingAngleDegrees, 0f);
                case 2: return NormalizeConeDirection(forward, right, up, 0f, SonarConeInnerRingAngleDegrees);
                case 3: return NormalizeConeDirection(forward, right, up, -SonarConeInnerRingAngleDegrees, 0f);
                case 4: return NormalizeConeDirection(forward, right, up, 0f, -SonarConeInnerRingAngleDegrees);
                case 5: return NormalizeConeDirection(forward, right, up, SonarConeOuterRingAngleDegrees, SonarConeOuterRingAngleDegrees * 0.5f);
                case 6: return NormalizeConeDirection(forward, right, up, SonarConeOuterRingAngleDegrees, -SonarConeOuterRingAngleDegrees * 0.5f);
                case 7: return NormalizeConeDirection(forward, right, up, -SonarConeOuterRingAngleDegrees, SonarConeOuterRingAngleDegrees * 0.5f);
                case 8: return NormalizeConeDirection(forward, right, up, -SonarConeOuterRingAngleDegrees, -SonarConeOuterRingAngleDegrees * 0.5f);
                case 9: return NormalizeConeDirection(forward, right, up, SonarConeOuterRingAngleDegrees * 0.5f, SonarConeOuterRingAngleDegrees);
                case 10: return NormalizeConeDirection(forward, right, up, -SonarConeOuterRingAngleDegrees * 0.5f, SonarConeOuterRingAngleDegrees);
                default: return NormalizeConeDirection(forward, right, up, 0f, -SonarConeOuterRingAngleDegrees);
            }
        }

        private static Vector3 NormalizeConeDirection(Vector3 forward, Vector3 right, Vector3 up, float yawDegrees, float pitchDegrees)
        {
            float yawRadians = math.radians(yawDegrees);
            float pitchRadians = math.radians(pitchDegrees);
            float cosPitch = math.cos(pitchRadians);
            Vector3 localDirection = new Vector3(
                math.sin(yawRadians) * cosPitch,
                math.sin(pitchRadians),
                math.cos(yawRadians) * cosPitch);
            Vector3 worldDirection =
                right * localDirection.x +
                up * localDirection.y +
                forward * localDirection.z;
            return worldDirection.sqrMagnitude > 0.0001f ? worldDirection.normalized : forward;
        }

        /// <summary>
        /// Receives deferred laser cutter heat and beam-state events.
        /// </summary>
        /// <param name="payload">Blittable cutter event payload.</param>
        public void OnLaserCutterEvent(in LaserCutterEventPayload payload)
        {
            if (!IsBoundPlayerCutterEvent(in payload))
                return;

            LaserCutterEventType eventType = (LaserCutterEventType)payload.EventType;
            if (eventType == LaserCutterEventType.HeatChanged)
            {
                HandleCutterHeatChanged(payload.Heat01);
                return;
            }

            if (eventType == LaserCutterEventType.BeamStateChanged)
                HandleCutterBeamStateChanged(in payload);
        }

        private void HandleCutterHeatChanged(float heat01)
        {
            _laserCutterHeat01 = math.saturate(heat01);
        }

        private void HandleCutterBeamStateChanged(in LaserCutterEventPayload payload)
        {
            bool isActive = LaserCutterEvents.IsBeamActive(in payload);
            _laserCutterBeamActive = isActive;
            if (!isActive)
                _laserCutterHeat01 = 0f;
        }

        private bool IsBoundPlayerCutterEvent(in LaserCutterEventPayload payload)
        {
            return _boundPlayerRootEntityId != 0 &&
                   payload.CutterRootInstanceId == _boundPlayerRootEntityId;
        }

        void IPhysicsImpactEventListener.OnPhysicsImpact(in PhysicsImpactSignal impactSignal)
        {
            HandlePhysicsImpact(in impactSignal);
        }

        private void HandlePhysicsImpact(in PhysicsImpactSignal impactSignal)
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
            byte dominantMaterialId = ResolveDominantImpactMaterialId(impactSignal.PrimaryAudioMaterialId, impactSignal.SecondaryAudioMaterialId);
            float clangMaterialMultiplier = ResolveImpactClangMaterialMultiplier(dominantMaterialId);
            float echoMaterialMultiplier = ResolveImpactEchoMaterialMultiplier(dominantMaterialId);
            float impactVolume01 = ResolveImpactVolume01FromMassVelocity(impactSignal.MassVelocity);
            float impactStress = math.saturate(impactVolume01 * PhysicsImpactStressBoost * math.max(0.2f, proximity));
            if (impactSignal.IsHeavy)
                impactStress = math.max(impactStress, 0.45f * math.max(0.35f, proximity));

            float metallicImpulse = impactSignal.IsHeavy
                ? math.max(impactStress, 0.55f * math.max(0.35f, proximity))
                : impactStress * math.max(0.35f, proximity);
            metallicImpulse = math.saturate(metallicImpulse * clangMaterialMultiplier);
            float clangExcitation = math.saturate(
                metallicImpulse *
                math.lerp(0.55f, 1.15f, impactVolume01) *
                math.max(0.4f, proximity) *
                clangMaterialMultiplier);
            if (isPlayerOwnedImpact && impactSignal.IsHeavy)
                clangExcitation = math.max(clangExcitation, 0.48f);
            float echoExcitation = math.saturate(metallicImpulse * math.lerp(0.45f, 1f, impactVolume01) * echoMaterialMultiplier);
            float echoDelaySeconds = 0f;
            float echoAttenuation = 0f;
            float echoLowPassCutoffHz = AcousticOcclusionUtility.OpenLowPassCutoffHertz;
            if (echoExcitation >= ImpactEchoMinimumExcitation)
            {
                if (!TryResolveForwardImpactEcho(
                        echoExcitation,
                        out echoDelaySeconds,
                        out echoAttenuation,
                        out echoLowPassCutoffHz))
                {
                    PrimeForwardImpactEchoProbe(echoExcitation);
                }
            }

            TryEnqueueImpactAudioEvent(
                impactStress,
                metallicImpulse,
                clangExcitation,
                echoExcitation,
                echoDelaySeconds,
                echoAttenuation,
                echoLowPassCutoffHz,
                1f);
            _impactStressImpulseTickValue = math.max(_impactStressImpulseTickValue, impactStress);
        }

        void IProceduralAudioEventListener.OnAudioPingTriggered(in AudioPingTriggerInfo info)
        {
            if (info.Kind == ProceduralAudioPingKind.PredatorKill)
                HandlePredatorKillAudioPing(in info);
            else if (info.Kind == ProceduralAudioPingKind.MeteorBoom)
                HandleMeteorBoomAudioPing(in info);
            else if (info.Kind == ProceduralAudioPingKind.MechanicalWhirr)
                HandleMechanicalWhirrAudioPing(in info);
        }

        void IProceduralAudioEventListener.OnStructuralStressTriggered(in StructuralStressAudioInfo info)
        {
            HandleStructuralStressTriggered(in info);
        }

        private void HandlePredatorKillAudioPing(in AudioPingTriggerInfo info)
        {
            if (_boundPlayerTransform == null)
                return;

            float distance = Vector3.Distance(_boundPlayerTransform.position, info.WorldPosition);
            if (distance > PredatorKillAudioRadiusMeters)
                return;

            float proximity = 1f - math.saturate(distance / PredatorKillAudioRadiusMeters);
            float transmission01 = math.saturate(info.AcousticTransmission01);
            float audible01 = math.saturate(info.Intensity * proximity * math.max(0.08f, transmission01));
            if (audible01 <= 0.001f)
                return;

            float echoDelaySeconds = math.clamp(distance / SoundSpeedWaterMetersPerSecond, 0f, SonarEchoMaximumDelaySeconds);
            float echoLowPassCutoffHz = math.clamp(
                math.min(info.LowPassCutoffHz, math.lerp(420f, AcousticOcclusionUtility.OpenLowPassCutoffHertz, transmission01)),
                AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                AcousticOcclusionUtility.OpenLowPassCutoffHertz);
            float snapExcitation = math.saturate(audible01 * 0.42f);
            float echoExcitation = math.saturate(audible01 * 0.55f);
            TryEnqueueImpactAudioEvent(
                audible01 * 0.18f,
                0f,
                snapExcitation,
                echoExcitation,
                echoDelaySeconds,
                transmission01,
                echoLowPassCutoffHz,
                0.78f);
        }

        private void HandleMeteorBoomAudioPing(in AudioPingTriggerInfo info)
        {
            if (_boundPlayerTransform == null)
                return;

            float distance = Vector3.Distance(_boundPlayerTransform.position, info.WorldPosition);
            if (distance > MeteorBoomAudioRadiusMeters)
                return;

            float proximity = 1f - math.saturate(distance / MeteorBoomAudioRadiusMeters);
            float audible01 = math.saturate(info.Intensity * proximity * math.max(0.2f, info.AcousticTransmission01));
            if (audible01 <= 0.001f)
                return;

            float echoDelaySeconds = math.clamp(distance / SoundSpeedWaterMetersPerSecond, 0f, SonarEchoMaximumDelaySeconds);
            float lowPassCutoffHz = math.clamp(
                info.LowPassCutoffHz,
                AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                800f);
            TryEnqueueImpactAudioEvent(
                audible01 * 0.62f,
                0f,
                0f,
                audible01 * 0.9f,
                echoDelaySeconds,
                math.saturate(info.AcousticTransmission01),
                lowPassCutoffHz,
                0.65f);
            _impactStressImpulseTickValue = math.max(_impactStressImpulseTickValue, audible01 * 0.28f);
        }

        private void HandleMechanicalWhirrAudioPing(in AudioPingTriggerInfo info)
        {
            if (_boundPlayerTransform == null)
                return;

            float distance = Vector3.Distance(_boundPlayerTransform.position, info.WorldPosition);
            if (distance > MechanicalWhirrAudioRadiusMeters)
                return;

            float proximity = 1f - math.saturate(distance / MechanicalWhirrAudioRadiusMeters);
            float audible01 = math.saturate(info.Intensity * proximity * math.max(0.18f, info.AcousticTransmission01));
            if (audible01 <= 0.001f)
                return;

            float pitchScale = math.clamp(info.LowPassCutoffHz / 1200f, 0.75f, 1.45f);
            TryEnqueueImpactAudioEvent(
                audible01 * 0.1f,
                audible01 * 0.55f,
                audible01 * 0.08f,
                audible01 * 0.22f,
                0f,
                math.saturate(info.AcousticTransmission01),
                math.clamp(info.LowPassCutoffHz, 900f, AcousticOcclusionUtility.OpenLowPassCutoffHertz),
                pitchScale);
        }

        private void HandleStructuralStressTriggered(in StructuralStressAudioInfo stressInfo)
        {
            if (_boundPlayerTransform == null)
                return;

            float maxDistance = PhysicsImpactStressRadiusMeters;
            float distance = Vector3.Distance(_boundPlayerTransform.position, stressInfo.WorldPosition);
            if (distance > maxDistance)
                return;

            float proximity = 1f - math.saturate(distance / maxDistance);
            float stress = math.saturate(stressInfo.Stress01 * math.max(0.25f, proximity));
            if (stress <= 0f)
                return;

            float metallic = math.saturate(stress * 0.95f);
            float clangExcitation = math.saturate(stress * 0.35f);
            float echoExcitation = math.saturate(stress * 0.45f);
            TryEnqueueImpactAudioEvent(
                stress,
                metallic,
                clangExcitation,
                echoExcitation,
                0f,
                0f,
                AcousticOcclusionUtility.OpenLowPassCutoffHertz,
                stressInfo.PitchScale);
            _impactStressImpulseTickValue = math.max(_impactStressImpulseTickValue, stress);
        }

        private static byte ResolveDominantImpactMaterialId(byte primaryMaterialId, byte secondaryMaterialId)
        {
            if (primaryMaterialId == (byte)ItemAudioMaterialId.Metal || primaryMaterialId == (byte)ItemAudioMaterialId.Glass)
                return primaryMaterialId;

            if (secondaryMaterialId == (byte)ItemAudioMaterialId.Metal || secondaryMaterialId == (byte)ItemAudioMaterialId.Glass)
                return secondaryMaterialId;

            return primaryMaterialId;
        }

        private static float ResolveImpactVolume01FromMassVelocity(float massVelocity)
        {
            return math.saturate(math.max(0f, massVelocity) / PhysicsImpactMassVelocityReference);
        }

        private static float ResolveImpactClangMaterialMultiplier(byte materialId)
        {
            switch ((ItemAudioMaterialId)materialId)
            {
                case ItemAudioMaterialId.Metal:
                    return 1.1f;

                case ItemAudioMaterialId.Glass:
                    return 0.85f;

                default:
                    return 0.4f;
            }
        }

        private static float ResolveImpactEchoMaterialMultiplier(byte materialId)
        {
            switch ((ItemAudioMaterialId)materialId)
            {
                case ItemAudioMaterialId.Metal:
                    return 1f;

                case ItemAudioMaterialId.Glass:
                    return 1.15f;

                default:
                    return 0.55f;
            }
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
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registered)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
                _registered = GlobalRegistry.Updatables.Contains(this);
            }

            if (!_lateFrameRegistered)
            {
                GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Environment);
                _lateFrameRegistered = SystemDispatcher.GetLateFrameLane(PriorityLayer.Environment).Contains(this);
            }

            if (_slowTickRegistered)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _slowTickRegistered = GlobalRegistry.SlowTickables.Contains(this);
        }

        private void TryUnregister()
        {
            if (_registered)
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);

            if (_slowTickRegistered)
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);

            if (_lateFrameRegistered)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);

            _registered = false;
            _slowTickRegistered = false;
            _lateFrameRegistered = false;
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
            bool shouldRestartWorker = Volatile.Read(ref _managedFilterFallbackEnabled) != 0;
            bool hasProducerThread = Volatile.Read(ref _audioProducerRunning) != 0 || IsAudioProducerThreadAlive();
            bool producerStopped = !IsAudioProducerThreadAlive();
            if (hasProducerThread)
                producerStopped = StopAudioProducerThread();

            if (!producerStopped)
            {
                ClearNativeOutputBridge();
                return;
            }

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

            DisposeBuffers(disposeSabineReverbDelay: false);

            _frameCapacity = frameCapacity;
            _hullScratch = new NativeArray<float>(_frameCapacity, Allocator.AudioKernel, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<float>[frameCapacity] - hull-stress DSP scratch - owner: PlayerCriticalProceduralAudioRenderer
            _sonarScratch = new NativeArray<float>(_frameCapacity, Allocator.AudioKernel, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<float>[frameCapacity] - sonar DSP scratch - owner: PlayerCriticalProceduralAudioRenderer
            _impactEchoScratch = new NativeArray<float>(_frameCapacity, Allocator.AudioKernel, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<float>[frameCapacity] - transient forward-echo DSP scratch - owner: PlayerCriticalProceduralAudioRenderer
            _thrusterScratch = new NativeArray<float>(_frameCapacity, Allocator.AudioKernel, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<float>[frameCapacity] - thruster DSP scratch - owner: PlayerCriticalProceduralAudioRenderer
            _heartbeatScratch = new NativeArray<float>(_frameCapacity, Allocator.AudioKernel, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<float>[frameCapacity] - psychoacoustic heartbeat DSP scratch - owner: PlayerCriticalProceduralAudioRenderer
            _heartbeatDuckScratch = new NativeArray<float>(_frameCapacity, Allocator.AudioKernel, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<float>[frameCapacity] - sample-domain sidechain duck coefficients - owner: PlayerCriticalProceduralAudioRenderer
            _bubbleScratch = new NativeArray<float>(_frameCapacity, Allocator.AudioKernel, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<float>[frameCapacity] - procedural bubble burst scratch - owner: PlayerCriticalProceduralAudioRenderer
            _mixScratch = new NativeArray<float>(_frameCapacity, Allocator.AudioKernel, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<float>[frameCapacity] - mixed procedural audio worklet scratch - owner: PlayerCriticalProceduralAudioRenderer
            _stereoMixScratch = new NativeArray<float>(_frameCapacity * BinauralOutputChannels, Allocator.AudioKernel, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<float>[frameCapacity*2] - stereo binaural output scratch - owner: PlayerCriticalProceduralAudioRenderer
            _sonarEchoDelay = new NativeArray<float>(SonarEchoDelayCapacity, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[131072] - sonar Hermite echo delay ring - owner: PlayerCriticalProceduralAudioRenderer
            _sonarConeCommands = new NativeArray<RaycastCommand>(SonarEchoTapCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<RaycastCommand>[12] - active-sonar cone probe commands - owner: PlayerCriticalProceduralAudioRenderer
            _sonarConeResults = new NativeArray<RaycastHit>(SonarEchoTapCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<RaycastHit>[12] - active-sonar cone probe results - owner: PlayerCriticalProceduralAudioRenderer
            _pendingSonarEchoTapsA = new NativeArray<SonarEchoTap>(SonarEchoTapCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<SonarEchoTap>[12] - pending sonar echo taps A - owner: PlayerCriticalProceduralAudioRenderer
            _pendingSonarEchoTapsB = new NativeArray<SonarEchoTap>(SonarEchoTapCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<SonarEchoTap>[12] - pending sonar echo taps B - owner: PlayerCriticalProceduralAudioRenderer
            _sonarEchoReadCursors = new NativeArray<double>(SonarEchoTapCapacity, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<double>[12] - sonar echo read cursors per tap - owner: PlayerCriticalProceduralAudioRenderer
            _sonarEchoFilterInput1 = new NativeArray<float>(SonarEchoTapCapacity, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[12] - sonar echo low-pass x1 state per tap - owner: PlayerCriticalProceduralAudioRenderer
            _sonarEchoFilterInput2 = new NativeArray<float>(SonarEchoTapCapacity, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[12] - sonar echo low-pass x2 state per tap - owner: PlayerCriticalProceduralAudioRenderer
            _sonarEchoFilterOutput1 = new NativeArray<float>(SonarEchoTapCapacity, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[12] - sonar echo low-pass y1 state per tap - owner: PlayerCriticalProceduralAudioRenderer
            _sonarEchoFilterOutput2 = new NativeArray<float>(SonarEchoTapCapacity, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[12] - sonar echo low-pass y2 state per tap - owner: PlayerCriticalProceduralAudioRenderer
            _impactClangDelay = new NativeArray<float>(ImpactClangDelayCapacity, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[1024] - Karplus-Strong impact delay line - owner: PlayerCriticalProceduralAudioRenderer
            _thrusterCombDelay = new NativeArray<float>(ThrusterCombDelayCapacity, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[4096] - thruster comb filter delay ring - owner: PlayerCriticalProceduralAudioRenderer
            if (!_sabineReverbDelay.IsCreated)
                _sabineReverbDelay = new NativeArray<float>(SabineReverbDelayCapacity, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[262144] - 1,048,576 bytes fixed four-comb Sabine reverb delay field - owner: PlayerCriticalProceduralAudioRenderer
            else
                ClearScratchBuffer(_sabineReverbDelay, _sabineReverbDelay.Length);
            _binauralDelayRing = new NativeArray<float>(BinauralDelayCapacity, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[128] - binaural ITD mono delay ring - owner: PlayerCriticalProceduralAudioRenderer
            _binauralShadowHistory = new NativeArray<float>(BinauralOutputChannels, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[2] - binaural shadow low-pass history per ear - owner: PlayerCriticalProceduralAudioRenderer
            _lowPassInputHistory1 = new NativeArray<float>(MaxFilterChannels, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[8] - final listener low-pass state x1 - owner: PlayerCriticalProceduralAudioRenderer
            _lowPassInputHistory2 = new NativeArray<float>(MaxFilterChannels, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[8] - final listener low-pass state x2 - owner: PlayerCriticalProceduralAudioRenderer
            _lowPassOutputHistory1 = new NativeArray<float>(MaxFilterChannels, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[8] - final listener low-pass state y1 - owner: PlayerCriticalProceduralAudioRenderer
            _lowPassOutputHistory2 = new NativeArray<float>(MaxFilterChannels, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[8] - final listener low-pass state y2 - owner: PlayerCriticalProceduralAudioRenderer
            _metallicGrainBank = new NativeArray<float>(MetallicGrainBankCapacity, Allocator.AudioKernel, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<float>[8192] - pre-baked metallic screech grain bank for hull granular synthesis - owner: PlayerCriticalProceduralAudioRenderer
            RegisterNativeBuffers();
            GenerateMetallicGrainBank(_metallicGrainBank);
            _sampleRingBuffer ??= new AudioFrameSpscRingBuffer();
            _sampleRingBuffer.Initialize(math.max(frameCapacity * 16, ringBufferCapacityFrames), BinauralOutputChannels);
            _producedSampleCount = 0L;
            _workerActiveSonarState = default;
            _workerConsumedSonarSequence = 0;
            _workerConsumedSonarRevision = 0;
            _workerActiveSonarTapBufferIndex = 0;
            _workerActiveSonarTapCount = 0;
            _pendingSonarEchoTapCountA = 0;
            _pendingSonarEchoTapCountB = 0;
            _pendingSonarStateA = default;
            _pendingSonarStateB = default;
            _queuedSonarConeQuery = default;
            _scheduledSonarConeQuery = default;
            _sonarConeQueryScheduled = false;
            _pendingSonarConeHandle = default;
            _heartbeatSynthesisState = default;
            _bubbleSynthesisState = default;
            _sabineReverbSynthesisState = default;
            _criticalSidechainCompressorState = new CriticalSidechainCompressorState { Gain = 1f };
            _binauralDelayWriteIndex = 0;
            ResetSonarPhaseState(0);
            _buffersInitialized = true;
            SignalAudioProducerThread();
        }

        private void RegisterNativeBuffers()
        {
            NativeMemorySentinel.RegisterNativeArray(_hullScratch, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_hullScratch), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_sonarScratch, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_sonarScratch), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_impactEchoScratch, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_impactEchoScratch), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_thrusterScratch, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_thrusterScratch), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_heartbeatScratch, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_heartbeatScratch), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_heartbeatDuckScratch, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_heartbeatDuckScratch), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_bubbleScratch, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_bubbleScratch), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_mixScratch, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_mixScratch), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_stereoMixScratch, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_stereoMixScratch), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_sonarEchoDelay, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_sonarEchoDelay), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_sonarConeCommands, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_sonarConeCommands), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_sonarConeResults, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_sonarConeResults), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_pendingSonarEchoTapsA, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_pendingSonarEchoTapsA), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_pendingSonarEchoTapsB, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_pendingSonarEchoTapsB), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_sonarEchoReadCursors, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_sonarEchoReadCursors), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_sonarEchoFilterInput1, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_sonarEchoFilterInput1), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_sonarEchoFilterInput2, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_sonarEchoFilterInput2), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_sonarEchoFilterOutput1, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_sonarEchoFilterOutput1), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_sonarEchoFilterOutput2, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_sonarEchoFilterOutput2), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_impactClangDelay, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_impactClangDelay), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_thrusterCombDelay, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_thrusterCombDelay), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_sabineReverbDelay, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_sabineReverbDelay), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_binauralDelayRing, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_binauralDelayRing), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_binauralShadowHistory, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_binauralShadowHistory), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_lowPassInputHistory1, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_lowPassInputHistory1), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_lowPassInputHistory2, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_lowPassInputHistory2), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_lowPassOutputHistory1, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_lowPassOutputHistory1), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_lowPassOutputHistory2, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_lowPassOutputHistory2), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_metallicGrainBank, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_metallicGrainBank), NativeAllocationLifetime.Session);
        }

        private void UnregisterNativeBuffers(bool unregisterSabineReverbDelay)
        {
            NativeMemorySentinel.UnregisterNativeArray(_hullScratch);
            NativeMemorySentinel.UnregisterNativeArray(_sonarScratch);
            NativeMemorySentinel.UnregisterNativeArray(_impactEchoScratch);
            NativeMemorySentinel.UnregisterNativeArray(_thrusterScratch);
            NativeMemorySentinel.UnregisterNativeArray(_heartbeatScratch);
            NativeMemorySentinel.UnregisterNativeArray(_heartbeatDuckScratch);
            NativeMemorySentinel.UnregisterNativeArray(_bubbleScratch);
            NativeMemorySentinel.UnregisterNativeArray(_mixScratch);
            NativeMemorySentinel.UnregisterNativeArray(_stereoMixScratch);
            NativeMemorySentinel.UnregisterNativeArray(_sonarEchoDelay);
            NativeMemorySentinel.UnregisterNativeArray(_sonarConeCommands);
            NativeMemorySentinel.UnregisterNativeArray(_sonarConeResults);
            NativeMemorySentinel.UnregisterNativeArray(_pendingSonarEchoTapsA);
            NativeMemorySentinel.UnregisterNativeArray(_pendingSonarEchoTapsB);
            NativeMemorySentinel.UnregisterNativeArray(_sonarEchoReadCursors);
            NativeMemorySentinel.UnregisterNativeArray(_sonarEchoFilterInput1);
            NativeMemorySentinel.UnregisterNativeArray(_sonarEchoFilterInput2);
            NativeMemorySentinel.UnregisterNativeArray(_sonarEchoFilterOutput1);
            NativeMemorySentinel.UnregisterNativeArray(_sonarEchoFilterOutput2);
            NativeMemorySentinel.UnregisterNativeArray(_impactClangDelay);
            NativeMemorySentinel.UnregisterNativeArray(_thrusterCombDelay);
            if (unregisterSabineReverbDelay)
                NativeMemorySentinel.UnregisterNativeArray(_sabineReverbDelay);
            NativeMemorySentinel.UnregisterNativeArray(_binauralDelayRing);
            NativeMemorySentinel.UnregisterNativeArray(_binauralShadowHistory);
            NativeMemorySentinel.UnregisterNativeArray(_lowPassInputHistory1);
            NativeMemorySentinel.UnregisterNativeArray(_lowPassInputHistory2);
            NativeMemorySentinel.UnregisterNativeArray(_lowPassOutputHistory1);
            NativeMemorySentinel.UnregisterNativeArray(_lowPassOutputHistory2);
            NativeMemorySentinel.UnregisterNativeArray(_metallicGrainBank);
        }

        private void DisposeBuffers(bool disposeSabineReverbDelay)
        {
            ClearNativeOutputBridge();
            _sampleRingBuffer?.Dispose();
            _sampleRingBuffer = null;
            UnregisterNativeBuffers(disposeSabineReverbDelay);
            if (_hullScratch.IsCreated)
                _hullScratch.Dispose();
            if (_sonarScratch.IsCreated)
                _sonarScratch.Dispose();
            if (_impactEchoScratch.IsCreated)
                _impactEchoScratch.Dispose();
            if (_thrusterScratch.IsCreated)
                _thrusterScratch.Dispose();
            if (_heartbeatScratch.IsCreated)
                _heartbeatScratch.Dispose();
            if (_heartbeatDuckScratch.IsCreated)
                _heartbeatDuckScratch.Dispose();
            if (_bubbleScratch.IsCreated)
                _bubbleScratch.Dispose();
            if (_mixScratch.IsCreated)
                _mixScratch.Dispose();
            if (_stereoMixScratch.IsCreated)
                _stereoMixScratch.Dispose();
            if (_sonarEchoDelay.IsCreated)
                _sonarEchoDelay.Dispose();
            if (_sonarConeCommands.IsCreated)
            {
                if (_sonarConeQueryScheduled && !_pendingSonarConeHandle.Equals(default(JobHandle)))
                    _sonarConeCommands.Dispose(_pendingSonarConeHandle);
                else
                    _sonarConeCommands.Dispose();
            }
            if (_sonarConeResults.IsCreated)
            {
                if (_sonarConeQueryScheduled && !_pendingSonarConeHandle.Equals(default(JobHandle)))
                    _sonarConeResults.Dispose(_pendingSonarConeHandle);
                else
                    _sonarConeResults.Dispose();
            }
            if (_pendingSonarEchoTapsA.IsCreated)
                _pendingSonarEchoTapsA.Dispose();
            if (_pendingSonarEchoTapsB.IsCreated)
                _pendingSonarEchoTapsB.Dispose();
            if (_sonarEchoReadCursors.IsCreated)
                _sonarEchoReadCursors.Dispose();
            if (_sonarEchoFilterInput1.IsCreated)
                _sonarEchoFilterInput1.Dispose();
            if (_sonarEchoFilterInput2.IsCreated)
                _sonarEchoFilterInput2.Dispose();
            if (_sonarEchoFilterOutput1.IsCreated)
                _sonarEchoFilterOutput1.Dispose();
            if (_sonarEchoFilterOutput2.IsCreated)
                _sonarEchoFilterOutput2.Dispose();
            if (_impactClangDelay.IsCreated)
                _impactClangDelay.Dispose();
            if (_thrusterCombDelay.IsCreated)
                _thrusterCombDelay.Dispose();
            if (disposeSabineReverbDelay && _sabineReverbDelay.IsCreated)
                _sabineReverbDelay.Dispose();
            if (_binauralDelayRing.IsCreated)
                _binauralDelayRing.Dispose();
            if (_binauralShadowHistory.IsCreated)
                _binauralShadowHistory.Dispose();
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
            _impactEchoScratch = default;
            _thrusterScratch = default;
            _heartbeatScratch = default;
            _heartbeatDuckScratch = default;
            _bubbleScratch = default;
            _mixScratch = default;
            _stereoMixScratch = default;
            _sonarEchoDelay = default;
            _sonarConeCommands = default;
            _sonarConeResults = default;
            _pendingSonarEchoTapsA = default;
            _pendingSonarEchoTapsB = default;
            _sonarEchoReadCursors = default;
            _sonarEchoFilterInput1 = default;
            _sonarEchoFilterInput2 = default;
            _sonarEchoFilterOutput1 = default;
            _sonarEchoFilterOutput2 = default;
            _impactClangDelay = default;
            _thrusterCombDelay = default;
            if (disposeSabineReverbDelay)
                _sabineReverbDelay = default;
            _binauralDelayRing = default;
            _binauralShadowHistory = default;
            _lowPassInputHistory1 = default;
            _lowPassInputHistory2 = default;
            _lowPassOutputHistory1 = default;
            _lowPassOutputHistory2 = default;
            _metallicGrainBank = default;

            _buffersInitialized = false;
            _frameCapacity = 0;
            _producedSampleCount = 0L;
            _binauralDelayWriteIndex = 0;
            _sabineReverbSynthesisState = default;
            _sonarConeQueryScheduled = false;
            _queuedSonarConeQuery = default;
            _scheduledSonarConeQuery = default;
            _pendingSonarConeHandle = default;
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
            ClearScratchBuffer(_binauralDelayRing, _binauralDelayRing.Length);
            ClearScratchBuffer(_binauralShadowHistory, _binauralShadowHistory.Length);
            _audioAbyssalLowPassMix = 0f;
            _audioStructuralFatigueValue = 0f;
            _pendingSonarSequence = 0;
            _pendingSonarStateReadIndex = 0;
            _pendingSonarEchoTapCountA = 0;
            _pendingSonarEchoTapCountB = 0;
            _pendingSonarStateA = default;
            _pendingSonarStateB = default;
            _workerActiveSonarState = default;
            _workerConsumedSonarSequence = 0;
            _workerConsumedSonarRevision = 0;
            _workerActiveSonarTapBufferIndex = 0;
            _workerActiveSonarTapCount = 0;
            _queuedSonarConeQuery = default;
            _scheduledSonarConeQuery = default;
            _sonarConeQueryScheduled = false;
            _pendingSonarConeHandle = default;
            _impactEventReadIndex = 0;
            _impactEventWriteIndex = 0;
            _hullSynthesisState = default;
            _ambientCurrentSynthesisState = default;
            _impactEchoSynthesisState = default;
            _thrusterSynthesisState = default;
            _criticalSidechainCompressorState = new CriticalSidechainCompressorState { Gain = 1f };
            _audioImpactStressValue = 0f;
            _audioImpactMetallicValue = 0f;
            _audioHullStressValue = 0f;
            _audioStructuralHullStressValue = 0f;
            _audioStructuralHullStressVelocityValue = 0f;
            if (_sonarEchoReadCursors.IsCreated)
            {
                for (int i = 0; i < _sonarEchoReadCursors.Length; i++)
                    _sonarEchoReadCursors[i] = -1d;
            }
            if (_sonarEchoFilterInput1.IsCreated)
                ClearScratchBuffer(_sonarEchoFilterInput1, _sonarEchoFilterInput1.Length);
            if (_sonarEchoFilterInput2.IsCreated)
                ClearScratchBuffer(_sonarEchoFilterInput2, _sonarEchoFilterInput2.Length);
            if (_sonarEchoFilterOutput1.IsCreated)
                ClearScratchBuffer(_sonarEchoFilterOutput1, _sonarEchoFilterOutput1.Length);
            if (_sonarEchoFilterOutput2.IsCreated)
                ClearScratchBuffer(_sonarEchoFilterOutput2, _sonarEchoFilterOutput2.Length);
            _audioHullPressureDepthValue = 0f;
            _audioAbsoluteDepthMeters = 0f;
            _pendingImpactEchoProbe = default;
            _hullStressTickValue = 0f;
            _structuralHullStressTickValue = 0f;
            _structuralHullStressVelocityTickValue = 0f;
            _absoluteDepthTickValue = 0f;
            _targetAbsoluteDepthMeters = 0f;
            ResetReverbModelState();
            ResetSonarPhaseState(0);
            if (_sonarEchoDelay.IsCreated)
                ClearScratchBuffer(_sonarEchoDelay, _sonarEchoDelay.Length);
            if (_impactClangDelay.IsCreated)
                ClearScratchBuffer(_impactClangDelay, _impactClangDelay.Length);
            if (_impactEchoScratch.IsCreated)
                ClearScratchBuffer(_impactEchoScratch, _impactEchoScratch.Length);
            if (_thrusterCombDelay.IsCreated)
                ClearScratchBuffer(_thrusterCombDelay, _thrusterCombDelay.Length);
            if (_sabineReverbDelay.IsCreated)
                ClearScratchBuffer(_sabineReverbDelay, _sabineReverbDelay.Length);
            _sabineReverbSynthesisState = default;
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

            if (TryResolveForwardEchoProbe(out Vector3 probeOrigin, out Vector3 probeDirection, out Transform probeIgnoreRoot))
            {
                if (AcousticOcclusionUtility.TryGetCachedForwardEchoSample(
                        probeOrigin,
                        probeDirection,
                        SonarEchoMaximumDistanceMeters,
                        _resolvedAcousticOcclusionLayerMask,
                        probeIgnoreRoot,
                        out AcousticForwardEchoResult forwardEcho) &&
                    forwardEcho.HasHit &&
                    forwardEcho.HitDistanceMeters > ForwardEchoMinimumDistanceMeters)
                {
                    float forwardEchoDistanceMeters = math.min(forwardEcho.HitDistanceMeters, SonarEchoMaximumDistanceMeters);
                    echoDelaySeconds = math.min(forwardEchoDistanceMeters / SoundSpeedWaterMetersPerSecond, SonarEchoMaximumDelaySeconds);
                    float radialVelocity = Vector3.Dot(_playerRigidbody.linearVelocity, probeDirection);
                    float clampedRadialVelocity = math.clamp(
                        radialVelocity,
                        -SoundSpeedWaterMetersPerSecond * 0.9f,
                        SoundSpeedWaterMetersPerSecond * 0.9f);
                    float stationaryDenominator = math.max(MinimumProbeDistanceMeters, SoundSpeedWaterMetersPerSecond - clampedRadialVelocity);
                    echoDopplerRatio = math.clamp(
                        (SoundSpeedWaterMetersPerSecond + clampedRadialVelocity) / stationaryDenominator,
                        SonarEchoMinimumDopplerRatio,
                        SonarEchoMaximumDopplerRatio);
                    float forwardTransmissionLossDb =
                        (20f * math.log10(math.max(forwardEchoDistanceMeters, MinimumProbeDistanceMeters))) +
                        (SonarEchoAbsorptionCoefficient * forwardEchoDistanceMeters);
                    echoAttenuation = math.clamp(
                        math.pow(10f, -forwardTransmissionLossDb / 20f) *
                        (SonarEchoReferenceDistanceMeters / (SonarEchoReferenceDistanceMeters + forwardEchoDistanceMeters)) *
                        forwardEcho.Transmission01,
                        0f,
                        0.95f);
                    echoLowPassCutoffHz = forwardEcho.LowPassCutoffHz;
                    return;
                }

                AcousticOcclusionUtility.PrimeForwardEchoSample(
                    probeOrigin,
                    probeDirection,
                    SonarEchoMaximumDistanceMeters,
                    _resolvedAcousticOcclusionLayerMask,
                    probeIgnoreRoot);
            }

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
                    SonarEchoMinimumDopplerRatio,
                    SonarEchoMaximumDopplerRatio);
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
                    SonarEchoMinimumDopplerRatio,
                    SonarEchoMaximumDopplerRatio);
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

        private bool TryResolveForwardEchoProbe(out Vector3 origin, out Vector3 forward, out Transform ignoreRoot)
        {
            origin = default;
            forward = Vector3.forward;
            ignoreRoot = _boundPlayerTransform != null ? _boundPlayerTransform.root : null;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null && playerContext.PlayerCamera != null)
            {
                Transform cameraTransform = playerContext.PlayerCamera.transform;
                if (cameraTransform != null)
                {
                    origin = cameraTransform.position;
                    forward = cameraTransform.forward;
                    ignoreRoot = cameraTransform.root;
                    return forward.sqrMagnitude > 0.0001f;
                }
            }

            if (_boundPlayerTransform == null)
                return false;

            origin = _boundPlayerTransform.position;
            forward = _boundPlayerTransform.forward;
            return forward.sqrMagnitude > 0.0001f;
        }

        private void PrimeForwardImpactEchoProbe(float echoExcitation)
        {
            if (!TryResolveForwardEchoProbe(out Vector3 probeOrigin, out Vector3 probeDirection, out Transform probeIgnoreRoot))
                return;

            AcousticOcclusionUtility.PrimeForwardEchoSample(
                probeOrigin,
                probeDirection,
                SonarEchoMaximumDistanceMeters,
                _resolvedAcousticOcclusionLayerMask,
                probeIgnoreRoot);

            _pendingImpactEchoProbe = new PendingImpactEchoProbe
            {
                Valid = true,
                Excitation = echoExcitation,
                ExpireAt = Time.unscaledTime + ImpactEchoMaximumLifetimeSeconds
            };
        }

        private void TryResolvePendingImpactEchoProbe()
        {
            if (!_pendingImpactEchoProbe.Valid)
                return;

            if (Time.unscaledTime > _pendingImpactEchoProbe.ExpireAt)
            {
                _pendingImpactEchoProbe = default;
                return;
            }

            if (!TryResolveForwardImpactEcho(
                    _pendingImpactEchoProbe.Excitation,
                    out float echoDelaySeconds,
                    out float echoAttenuation,
                    out float echoLowPassCutoffHz))
            {
                return;
            }

            TryEnqueueImpactAudioEvent(
                0f,
                0f,
                0f,
                _pendingImpactEchoProbe.Excitation,
                echoDelaySeconds,
                echoAttenuation,
                echoLowPassCutoffHz,
                1f);
            _pendingImpactEchoProbe = default;
        }

        private bool TryResolveForwardImpactEcho(
            float echoExcitation,
            out float echoDelaySeconds,
            out float echoAttenuation,
            out float echoLowPassCutoffHz)
        {
            echoDelaySeconds = 0f;
            echoAttenuation = 0f;
            echoLowPassCutoffHz = AcousticOcclusionUtility.OpenLowPassCutoffHertz;

            if (echoExcitation < ImpactEchoMinimumExcitation ||
                !TryResolveForwardEchoProbe(out Vector3 probeOrigin, out Vector3 probeDirection, out Transform probeIgnoreRoot))
            {
                return false;
            }

            if (!AcousticOcclusionUtility.TryGetCachedForwardEchoSample(
                    probeOrigin,
                    probeDirection,
                    SonarEchoMaximumDistanceMeters,
                    _resolvedAcousticOcclusionLayerMask,
                    probeIgnoreRoot,
                    out AcousticForwardEchoResult forwardEcho) ||
                !forwardEcho.HasHit ||
                forwardEcho.HitDistanceMeters <= ForwardEchoMinimumDistanceMeters)
            {
                return false;
            }

            float distanceMeters = math.min(forwardEcho.HitDistanceMeters, SonarEchoMaximumDistanceMeters);
            echoDelaySeconds = math.min(distanceMeters / SoundSpeedWaterMetersPerSecond, SonarEchoMaximumDelaySeconds);
            echoAttenuation = math.clamp(
                echoExcitation *
                (SonarEchoReferenceDistanceMeters / (SonarEchoReferenceDistanceMeters + distanceMeters)) *
                forwardEcho.Transmission01,
                0f,
                0.92f);
            echoLowPassCutoffHz = forwardEcho.LowPassCutoffHz;
            return echoAttenuation > 0.0001f;
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

        private void UpdateAcousticThreatPulse()
        {
            if (_boundPlayerTransform == null)
                return;

            HectonMapMagicVegetationBridge vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (vegetationBridge == null)
                return;

            float lfeThreat01 = math.saturate(math.max(
                _targetStructuralHullStressValue * _targetHullPressureDepthValue,
                _impactStressImpulseTickValue * 0.65f));
            if (lfeThreat01 < HullLfeThreatMinimum01)
                return;

            float radius = math.lerp(36f, HullLfeThreatRadiusMeters, lfeThreat01);
            float strength = math.lerp(0.25f, HullLfeThreatStrength, lfeThreat01);
            vegetationBridge.ApplyExternalThreatPulse(
                _boundPlayerTransform.position,
                radius,
                strength,
                HullLfeThreatHoldSeconds);
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

        private void RenderHeartbeatBlock(
            int frameCount,
            double invSampleRate,
            bool heartbeatActiveTarget,
            float heartbeatStressTarget,
            float heartbeatOxygenDangerTarget)
        {
            if (!_heartbeatScratch.IsCreated || !_heartbeatDuckScratch.IsCreated)
                return;

            HeartbeatSynthesisState state = _heartbeatSynthesisState;
            float stressStart = _audioHeartbeatStressValue;
            float oxygenDangerStart = _audioHeartbeatOxygenDangerValue;
            if (!heartbeatActiveTarget)
            {
                ClearScratchBuffer(_heartbeatScratch, frameCount);
                FillScratchBuffer(_heartbeatDuckScratch, frameCount, 1f);
                _heartbeatSynthesisState = default;
                _audioHeartbeatStressValue = 0f;
                _audioHeartbeatOxygenDangerValue = 0f;
                return;
            }

            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                float frameT = frameCount > 1 ? frameIndex / (float)(frameCount - 1) : 0f;
                float stress = heartbeatActiveTarget ? math.lerp(stressStart, heartbeatStressTarget, frameT) : 0f;
                float oxygenDanger = heartbeatActiveTarget ? math.lerp(oxygenDangerStart, heartbeatOxygenDangerTarget, frameT) : 0f;
                float heartbeatDrive = math.saturate(math.max(stress, oxygenDanger));
                float bpm = math.lerp(HeartbeatBaseBpm, HeartbeatStressBpm, heartbeatDrive);
                float beatIntervalSeconds = 60f / math.max(HeartbeatBaseBpm, bpm);
                if (state.TimeToNextBeatSeconds <= 0f)
                {
                    state.TimeToNextBeatSeconds = beatIntervalSeconds;
                    state.SecondaryPulseDelaySeconds = HeartbeatSecondaryPulseDelaySeconds;
                    state.PrimaryPulseAgeSeconds = 0f;
                    state.SecondaryPulseAgeSeconds = -1f;
                }

                float primaryEnvelope = state.PrimaryPulseAgeSeconds >= 0f
                    ? RenderHeartbeatEnvelope(state.PrimaryPulseAgeSeconds)
                    : 0f;
                float secondaryEnvelope = state.SecondaryPulseAgeSeconds >= 0f
                    ? RenderHeartbeatEnvelope(state.SecondaryPulseAgeSeconds) * 0.82f
                    : 0f;
                float combinedEnvelope = math.max(primaryEnvelope, secondaryEnvelope);
                float carrier =
                    AdvanceSine(ref state.CarrierPhase, HeartbeatPrimaryCarrierHertz, invSampleRate) * 0.78f +
                    AdvanceSine(ref state.HarmonicPhase, HeartbeatSecondaryCarrierHertz, invSampleRate) * 0.22f;
                float amplitude = HeartbeatMaximumGain * math.lerp(0.2f, 1f, heartbeatDrive);
                _heartbeatScratch[frameIndex] = carrier * combinedEnvelope * amplitude;

                float deltaSeconds = (float)invSampleRate;
                float duckDepth = HeartbeatDuckMaximum * math.lerp(0.18f, 1f, heartbeatDrive);
                float duckTarget = combinedEnvelope * duckDepth;
                float duckSharpness = duckTarget > state.DuckEnvelope
                    ? HeartbeatDuckAttackSharpness
                    : HeartbeatDuckReleaseSharpness;
                float duckBlend = 1f - math.exp(-duckSharpness * deltaSeconds);
                state.DuckEnvelope = math.lerp(state.DuckEnvelope, duckTarget, duckBlend);
                _heartbeatDuckScratch[frameIndex] = math.clamp(1f - state.DuckEnvelope, 0.35f, 1f);

                state.TimeToNextBeatSeconds -= deltaSeconds;
                if (state.PrimaryPulseAgeSeconds >= 0f)
                {
                    state.PrimaryPulseAgeSeconds += deltaSeconds;
                    if (state.PrimaryPulseAgeSeconds > HeartbeatAttackSeconds + HeartbeatDecaySeconds + HeartbeatSustainSeconds + HeartbeatReleaseSeconds)
                        state.PrimaryPulseAgeSeconds = -1f;
                }

                if (state.SecondaryPulseDelaySeconds > 0f)
                {
                    state.SecondaryPulseDelaySeconds -= deltaSeconds;
                    if (state.SecondaryPulseDelaySeconds <= 0f)
                        state.SecondaryPulseAgeSeconds = 0f;
                }
                else if (state.SecondaryPulseAgeSeconds >= 0f)
                {
                    state.SecondaryPulseAgeSeconds += deltaSeconds;
                    if (state.SecondaryPulseAgeSeconds > HeartbeatAttackSeconds + HeartbeatDecaySeconds + HeartbeatSustainSeconds + HeartbeatReleaseSeconds)
                        state.SecondaryPulseAgeSeconds = -1f;
                }
            }

            _heartbeatSynthesisState = state;
            _audioHeartbeatStressValue = heartbeatStressTarget;
            _audioHeartbeatOxygenDangerValue = heartbeatOxygenDangerTarget;
        }

        private void RenderBubbleBlock(
            int frameCount,
            long blockStartFrame,
            double invSampleRate,
            float bubbleBoilTarget,
            float absoluteDepthMeters)
        {
            if (!_bubbleScratch.IsCreated)
                return;

            BubbleSynthesisState state = _bubbleSynthesisState;
            float startIntensity = _audioBubbleBoilIntensity;
            if (!(bubbleBoilTarget > HullNoiseFloor) && !(startIntensity > HullNoiseFloor) && state.Envelope <= HullNoiseFloor)
            {
                ClearScratchBuffer(_bubbleScratch, frameCount);
                _bubbleSynthesisState = default;
                _audioBubbleBoilIntensity = 0f;
                return;
            }

            RenderMinnaertBubbleBurstKernel(
                _bubbleScratch,
                frameCount,
                blockStartFrame,
                invSampleRate,
                startIntensity,
                bubbleBoilTarget,
                absoluteDepthMeters,
                ref state);

            _bubbleSynthesisState = state;
            _audioBubbleBoilIntensity = bubbleBoilTarget;
        }

        private void MixAndFilterBlock(int frameCount, long blockStartFrame, double invSampleRate, AudioParameterSnapshot parameters)
        {
            float targetMix = math.saturate(parameters.AbyssalLowPassMix);
            float startMix = _audioAbyssalLowPassMix;
            float endMix = targetMix;
            float startAbsoluteDepthMeters = math.max(0f, _audioAbsoluteDepthMeters);
            float endAbsoluteDepthMeters = math.max(0f, parameters.AbsoluteDepthMeters);
            float startPressureCutoff = ResolvePressureHighFrequencyCutoff(startAbsoluteDepthMeters);
            float endPressureCutoff = ResolvePressureHighFrequencyCutoff(endAbsoluteDepthMeters);
            bool shouldFilter =
                targetMix > 0.0001f ||
                startMix > 0.0001f ||
                startPressureCutoff < (_sampleRate * 0.45f) - 0.001f ||
                endPressureCutoff < (_sampleRate * 0.45f) - 0.001f;
            AmbientCurrentSynthesisState ambientState = _ambientCurrentSynthesisState;
            float ambientDepthDrive = math.saturate(math.max(parameters.HullPressureDepth, parameters.AbyssalLowPassMix));
            float structuralSidechainDrive = math.saturate(math.max(parameters.StructuralHullStress, parameters.StructuralSnap));
            CriticalSidechainCompressorState sidechainState = _criticalSidechainCompressorState;
            if (sidechainState.Gain <= HullNoiseFloor)
                sidechainState.Gain = 1f;
            ResolveSabineReverbBlock(
                parameters.ReverbRt60Seconds,
                parameters.ReverbWetMix,
                parameters.ReverbOpenness,
                out bool sabineReverbActive,
                out float sabineWetGain,
                out float sabineDampingAlpha,
                out int sabineDelayA,
                out int sabineDelayB,
                out int sabineDelayC,
                out int sabineDelayD,
                out float sabineFeedbackA,
                out float sabineFeedbackB,
                out float sabineFeedbackC,
                out float sabineFeedbackD);
            SabineReverbSynthesisState sabineState = _sabineReverbSynthesisState;

            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                long sampleFrame = blockStartFrame + frameIndex;
                float ambientCurrent = RenderAmbientCurrentSample(ref ambientState, sampleFrame, invSampleRate, ambientDepthDrive);
                float criticalSidechain = math.max(math.abs(_hullScratch[frameIndex]), math.abs(_impactEchoScratch[frameIndex]));
                criticalSidechain = math.max(criticalSidechain, math.abs(_sonarScratch[frameIndex]) * 0.45f);
                criticalSidechain = math.max(criticalSidechain, structuralSidechainDrive);
                float envelopeBlend = ResolveOnePoleTimeBlend(
                    criticalSidechain > sidechainState.Envelope
                        ? CriticalSidechainAttackSeconds
                        : CriticalSidechainReleaseSeconds,
                    invSampleRate);
                sidechainState.Envelope = math.lerp(sidechainState.Envelope, criticalSidechain, envelopeBlend);
                float duckGainTarget = ResolveCriticalSidechainDuckingGain(sidechainState.Envelope);
                float gainBlend = ResolveOnePoleTimeBlend(
                    duckGainTarget < sidechainState.Gain
                        ? CriticalSidechainAttackSeconds
                        : CriticalSidechainReleaseSeconds,
                    invSampleRate);
                sidechainState.Gain = math.lerp(sidechainState.Gain, duckGainTarget, gainBlend);
                float duckedAmbientCurrent = ambientCurrent * sidechainState.Gain;
                float mixedDry =
                    (_hullScratch[frameIndex] +
                     _sonarScratch[frameIndex] +
                     _impactEchoScratch[frameIndex] +
                     _thrusterScratch[frameIndex] +
                     duckedAmbientCurrent +
                     _bubbleScratch[frameIndex]) * _heartbeatDuckScratch[frameIndex];
                float mixed = (mixedDry + _heartbeatScratch[frameIndex]) * outputHeadroom;

                if (sabineReverbActive)
                {
                    mixed = RenderSabineReverbSample(
                        ref sabineState,
                        mixed,
                        sabineWetGain,
                        sabineDampingAlpha,
                        sabineDelayA,
                        sabineDelayB,
                        sabineDelayC,
                        sabineDelayD,
                        sabineFeedbackA,
                        sabineFeedbackB,
                        sabineFeedbackC,
                        sabineFeedbackD);
                }

                if (shouldFilter)
                {
                    float frameT = frameCount > 1 ? frameIndex / (float)(frameCount - 1) : 1f;
                    float mix = math.lerp(startMix, endMix, frameT);
                    float abyssalCutoff = math.lerp(_sampleRate * 0.45f, AbyssalLowPassCutoffHertz, mix);
                    float pressureCutoff = math.lerp(startPressureCutoff, endPressureCutoff, frameT);
                    float cutoff = math.min(abyssalCutoff, pressureCutoff);
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

                _mixScratch[frameIndex] = ApplyMasterSafetyLimiter(mixed);
            }

            _ambientCurrentSynthesisState = ambientState;
            _sabineReverbSynthesisState = sabineState;
            _criticalSidechainCompressorState = sidechainState;
            _audioAbyssalLowPassMix = endMix;
            _audioAbsoluteDepthMeters = endAbsoluteDepthMeters;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float ResolveOnePoleTimeBlend(float timeConstantSeconds, double invSampleRate)
        {
            float deltaSeconds = math.max((float)invSampleRate, 0f);
            return 1f - math.exp(-deltaSeconds / math.max(timeConstantSeconds, 0.0001f));
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float ResolveCriticalSidechainDuckingGain(float sidechainEnvelope)
        {
            float overThreshold = math.saturate(
                (math.max(0f, sidechainEnvelope) - CriticalSidechainThreshold) /
                math.max(CriticalSidechainKneeWidth, 0.0001f));
            float shaped = overThreshold * overThreshold * (3f - 2f * overThreshold);
            return math.lerp(1f, CriticalSidechainDuckedGain, shaped);
        }

        private void ResolveSabineReverbBlock(
            float rt60Seconds,
            float wetMix01,
            float openness01,
            out bool active,
            out float wetGain,
            out float dampingAlpha,
            out int delayA,
            out int delayB,
            out int delayC,
            out int delayD,
            out float feedbackA,
            out float feedbackB,
            out float feedbackC,
            out float feedbackD)
        {
            float safeSampleRate = math.max(_sampleRate, 1f);
            float safeRt60 = math.clamp(rt60Seconds, 0.05f, 12f);
            float safeWetMix = math.saturate(wetMix01);
            float safeOpenness = math.saturate(openness01);

            active = _sabineReverbDelay.IsCreated && safeWetMix > 0.0001f && safeRt60 > 0.05f;
            wetGain = safeWetMix * SabineReverbMaximumWetGain;
            float dampingCutoff = math.lerp(
                SabineReverbDampingClosedCutoffHertz,
                SabineReverbDampingOpenCutoffHertz,
                safeOpenness);
            dampingAlpha = math.exp((-TwoPi * dampingCutoff) / safeSampleRate);

            delayA = ResolveSabineDelaySamples(SabineReverbDelayASeconds, safeSampleRate);
            delayB = ResolveSabineDelaySamples(SabineReverbDelayBSeconds, safeSampleRate);
            delayC = ResolveSabineDelaySamples(SabineReverbDelayCSeconds, safeSampleRate);
            delayD = ResolveSabineDelaySamples(SabineReverbDelayDSeconds, safeSampleRate);

            feedbackA = ResolveSabineFeedback(delayA, safeSampleRate, safeRt60);
            feedbackB = ResolveSabineFeedback(delayB, safeSampleRate, safeRt60);
            feedbackC = ResolveSabineFeedback(delayC, safeSampleRate, safeRt60);
            feedbackD = ResolveSabineFeedback(delayD, safeSampleRate, safeRt60);
        }

        private static int ResolveSabineDelaySamples(float delaySeconds, float sampleRate)
        {
            return math.clamp(
                (int)math.round(delaySeconds * sampleRate),
                1,
                SabineReverbDelayLineLength - 1);
        }

        private static float ResolveSabineFeedback(int delaySamples, float sampleRate, float rt60Seconds)
        {
            float delaySeconds = delaySamples / math.max(sampleRate, 1f);
            float feedback = math.pow(10f, (-3f * delaySeconds) / math.max(rt60Seconds, 0.05f));
            return math.clamp(feedback, SabineReverbMinimumFeedback, SabineReverbMaximumFeedback);
        }

        private float RenderSabineReverbSample(
            ref SabineReverbSynthesisState state,
            float drySample,
            float wetGain,
            float dampingAlpha,
            int delayA,
            int delayB,
            int delayC,
            int delayD,
            float feedbackA,
            float feedbackB,
            float feedbackC,
            float feedbackD)
        {
            float combA = ProcessSabineComb(
                0,
                ref state.CombAWriteIndex,
                ref state.CombADampingState,
                drySample,
                delayA,
                feedbackA,
                dampingAlpha);
            float combB = ProcessSabineComb(
                SabineReverbDelayLineLength,
                ref state.CombBWriteIndex,
                ref state.CombBDampingState,
                drySample,
                delayB,
                feedbackB,
                dampingAlpha);
            float combC = ProcessSabineComb(
                SabineReverbDelayLineLength * 2,
                ref state.CombCWriteIndex,
                ref state.CombCDampingState,
                drySample,
                delayC,
                feedbackC,
                dampingAlpha);
            float combD = ProcessSabineComb(
                SabineReverbDelayLineLength * 3,
                ref state.CombDWriteIndex,
                ref state.CombDDampingState,
                drySample,
                delayD,
                feedbackD,
                dampingAlpha);

            float wet = (combA + combB + combC + combD) * 0.25f;
            return drySample + wet * wetGain;
        }

        private float ProcessSabineComb(
            int offset,
            ref int writeIndex,
            ref float dampingState,
            float input,
            int delaySamples,
            float feedback,
            float dampingAlpha)
        {
            int clampedWriteIndex = writeIndex & SabineReverbDelayLineMask;
            int readIndex = (clampedWriteIndex - delaySamples) & SabineReverbDelayLineMask;
            int readAddress = offset + readIndex;
            int writeAddress = offset + clampedWriteIndex;
            float delayed = _sabineReverbDelay[readAddress];
            dampingState = delayed + dampingAlpha * ((dampingState + BiquadDenormalBias) - delayed);
            _sabineReverbDelay[writeAddress] = input + dampingState * feedback;
            writeIndex = (clampedWriteIndex + 1) & SabineReverbDelayLineMask;
            return delayed;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float ApplyMasterSafetyLimiter(float sample)
        {
            float magnitude = math.abs(sample);
            if (magnitude <= MasterSafetyLimiterThreshold)
                return sample;

            float sign = math.select(-1f, 1f, sample >= 0f);
            float excess = magnitude - MasterSafetyLimiterThreshold;
            float compressed = MasterSafetyLimiterThreshold + (excess / (1f + excess * MasterSafetyLimiterDrive));
            return sign * math.min(1f, compressed);
        }

        private float ResolvePressureHighFrequencyCutoff(float absoluteDepthMeters)
        {
            float pressureDepth = math.max(0f, absoluteDepthMeters);
            float pressureScalar = 1f + (pressureDepth / math.max(PsychoacousticPressureReferenceDepthMeters, 1f));
            float openCutoff = _sampleRate * 0.45f;
            return math.clamp(
                openCutoff / math.max(pressureScalar, 1f),
                PsychoacousticPressureMinimumCutoffHertz,
                openCutoff);
        }

        private float RenderAmbientCurrentSample(
            ref AmbientCurrentSynthesisState state,
            long sampleFrame,
            double invSampleRate,
            float depthDrive)
        {
            uint sampleIndex = (uint)math.max(0L, sampleFrame);
            float sampleTime = (float)(sampleFrame * invSampleRate);
            return RenderAmbientCurrentFmKernel(
                ref state,
                sampleIndex,
                sampleTime,
                invSampleRate,
                depthDrive,
                _sampleRate);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float RenderAmbientCurrentFmKernel(
            ref AmbientCurrentSynthesisState state,
            uint sampleIndex,
            float sampleTime,
            double invSampleRate,
            float depthDrive,
            int sampleRate)
        {
            float simplex0 = SampleSimplex01(sampleTime * 0.0215f + 11.17f, 0x14583AA1u);
            float simplex1 = SampleSimplex01(sampleTime * 0.0585f + 23.91f, 0x7A15D913u);
            float simplex2 = SampleSimplex01(sampleTime * 0.109f + 41.33f, 0x5E2334B1u);
            float simplex3 = SampleSimplex01(sampleTime * 0.181f + 57.63f, 0x1D42B7C5u);
            float cascade = math.saturate(simplex0 * 0.36f + simplex1 * 0.29f + simplex2 * 0.21f + simplex3 * 0.14f);
            float modulatorRate = math.lerp(0.06f, 0.32f, cascade);
            float fmDepth = math.lerp(4f, AmbientCurrentFmDepthHertz * 1.75f, cascade) * math.lerp(0.45f, 1f, depthDrive);
            float modulator = AdvanceSine(ref state.ModulatorPhase, modulatorRate, invSampleRate) * fmDepth;
            float noiseDrift = AdvanceSine(ref state.NoisePhase, math.lerp(0.11f, 0.47f, simplex3), invSampleRate) * math.lerp(1.5f, 6.5f, cascade);
            float carrierFrequency = math.max(6f, AmbientCurrentBaseFrequencyHertz + modulator + noiseDrift);
            float carrier = AdvanceSine(ref state.CarrierPhase, carrierFrequency, invSampleRate);
            float whiteNoise = HashSigned(sampleIndex ^ 0x6A1F0D3Bu);
            float lowPassCutoff = math.clamp(
                math.lerp(AmbientCurrentLowPassMinimumHertz, AmbientCurrentLowPassMaximumHertz, cascade) +
                carrier * math.lerp(18f, 64f, cascade),
                AmbientCurrentLowPassMinimumHertz,
                AmbientCurrentLowPassMaximumHertz);
            float filterCoefficient = math.min(
                0.99f,
                2f * math.sin(math.PI * lowPassCutoff / math.max(sampleRate, 1f)));
            float resonance = math.lerp(1.38f, 0.42f, math.saturate(cascade * 0.7f + depthDrive * 0.3f));
            float lowPassState = state.LowPassState + BiquadDenormalBias;
            float bandPassState = state.BandPassState + BiquadDenormalBias;
            state.LowPassState = lowPassState + filterCoefficient * bandPassState;
            float highPass = whiteNoise - state.LowPassState - resonance * bandPassState;
            state.BandPassState = bandPassState + filterCoefficient * highPass;
            float filteredNoise = state.LowPassState;
            float slowLfo = 0.55f + 0.45f * AdvanceSine(
                ref state.SlowPhase,
                math.lerp(AmbientCurrentSlowLfoMinimumHertz, AmbientCurrentSlowLfoMaximumHertz, cascade),
                invSampleRate);
            float fmBed = carrier * math.lerp(0.16f, 0.34f, cascade);
            return (filteredNoise * 0.78f + fmBed) *
                   math.lerp(0.3f, 1f, depthDrive) *
                   slowLfo *
                   AmbientCurrentMasterGain;
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

            if (_sonarEchoReadCursors.IsCreated)
            {
                for (int i = 0; i < _sonarEchoReadCursors.Length; i++)
                    _sonarEchoReadCursors[i] = -1d;
            }

            if (_sonarEchoFilterInput1.IsCreated)
                ClearScratchBuffer(_sonarEchoFilterInput1, _sonarEchoFilterInput1.Length);
            if (_sonarEchoFilterInput2.IsCreated)
                ClearScratchBuffer(_sonarEchoFilterInput2, _sonarEchoFilterInput2.Length);
            if (_sonarEchoFilterOutput1.IsCreated)
                ClearScratchBuffer(_sonarEchoFilterOutput1, _sonarEchoFilterOutput1.Length);
            if (_sonarEchoFilterOutput2.IsCreated)
                ClearScratchBuffer(_sonarEchoFilterOutput2, _sonarEchoFilterOutput2.Length);
        }

        private void RebuildEnclosureProbeLayerMask()
        {
            _resolvedAcousticOcclusionLayerMask = AcousticOcclusionUtility.BuildSensoryMask() & enclosureProbeLayers.value;
        }

        private static float ResolveHullPressureDepth01(float depthMeters)
        {
            return math.saturate(math.max(0f, depthMeters) / PressureCreakDepthReferenceMeters);
        }

        private float ResolveAbsoluteDepthMeters()
        {
            if (_boundPlayerTransform == null || playerMovement == null)
                return 0f;

            Vector3 runtimePlayerPosition = _boundPlayerTransform.position;
            Vector3 runtimeSurfacePosition = new Vector3(
                runtimePlayerPosition.x,
                playerMovement.CurrentWaterSurfaceY,
                runtimePlayerPosition.z);
            Vector3 absolutePlayerPosition = HectonFloatingOrigin.ToAbsoluteUniversePosition(runtimePlayerPosition);
            Vector3 absoluteSurfacePosition = HectonFloatingOrigin.ToAbsoluteUniversePosition(runtimeSurfacePosition);
            return math.max(0f, absoluteSurfacePosition.y - absolutePlayerPosition.y);
        }

        private void UpdateSurvivalTargets(float deltaTime)
        {
            float oxygenNormalized = _playerSurvivalSystem != null
                ? math.saturate(_playerSurvivalSystem.OxygenNormalized)
                : 1f;
            if (oxygenNormalized > HeartbeatBypassOxygenThreshold)
            {
                _heartbeatStressTickValue = 0f;
                _heartbeatOxygenDangerTickValue = 0f;
                _targetHeartbeatStressValue = 0f;
                _targetHeartbeatOxygenDangerValue = 0f;
                _targetHeartbeatActive = 0;
                return;
            }

            float oxygenDanger = ResolveDescendingNormalized01(
                oxygenNormalized,
                HeartbeatCriticalOxygenThreshold,
                HeartbeatTerminalOxygenThreshold);
            float pressureStress = _playerSurvivalSystem != null
                ? math.saturate(_playerSurvivalSystem.PressureExposureSeverity01)
                : 0f;
            float thermalStress = _playerSurvivalSystem != null
                ? math.saturate(_playerSurvivalSystem.ThermalStressSeverity01)
                : 0f;
            float underwaterStress = playerMovement != null
                ? math.saturate(playerMovement.CurrentUnderwaterStressIntensity01)
                : 0f;
            float fatalPressure = playerMovement != null ? math.saturate(playerMovement.CurrentFatalPressureSequence01) : 0f;
            float stressTarget = math.saturate(math.max(
                oxygenDanger,
                math.max(pressureStress, math.max(thermalStress, math.max(underwaterStress, fatalPressure)))));
            float survivalBlendT = 1f - math.exp(-math.max(6f, 0.01f) * math.max(0f, deltaTime));
            _heartbeatStressTickValue = math.lerp(_heartbeatStressTickValue, stressTarget, survivalBlendT);
            _heartbeatOxygenDangerTickValue = math.lerp(_heartbeatOxygenDangerTickValue, oxygenDanger, survivalBlendT);
            _targetHeartbeatStressValue = _heartbeatStressTickValue;
            _targetHeartbeatOxygenDangerValue = _heartbeatOxygenDangerTickValue;
            _targetHeartbeatActive = 1;
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

        private float ResolveStructuralFatigue01()
        {
            if (_structuralHullReadModel == null)
                _structuralHullReadModel = GlobalRegistry.SubmarineHullBreach;

            if (_structuralHullReadModel is SubmarineStructuralGrid structuralGrid)
                return math.saturate(structuralGrid.FatiguePeakNormalized);

            return 0f;
        }

        private float ResolveStructuralDamageTransient01()
        {
            if (_structuralHullReadModel == null)
                _structuralHullReadModel = GlobalRegistry.SubmarineHullBreach;

            if (_structuralHullReadModel is SubmarineStructuralGrid structuralGrid)
                return math.saturate(structuralGrid.RecentImpactSeverityNormalized);

            return 0f;
        }

        private void UpdateBubbleBoilTargets()
        {
            bool shouldEmitBubbles =
                _laserCutterBeamActive &&
                playerMovement != null &&
                playerMovement.IsPlayerSubmerged;
            if (!shouldEmitBubbles)
            {
                _targetBubbleBoilIntensity = 0f;
                return;
            }

            _targetBubbleBoilIntensity = math.saturate(math.max(_laserCutterHeat01, BubbleBoilMinimumHeatFloor));
        }

        private static float ResolveEnclosureDensityIndex(in AcousticEnclosureResult enclosure)
        {
            float surfaceDensity01 = math.saturate(enclosure.SurfaceHitCount / 6f);
            return math.saturate(((1f - enclosure.Openness01) * 0.65f) + (surfaceDensity01 * 0.35f));
        }

        private void UpdateBinauralTargets()
        {
            if (!(Hecton8.Core.GlobalRegistry.Audio is SpatialAudioManager audioManager) ||
                !audioManager.TryGetDominantBinauralEmitter(out SpatialAudioManager.BinauralEmitterTelemetry telemetry))
            {
                _targetBinauralAzimuthRadians = 0f;
                _targetBinauralItdSeconds = 0f;
                _targetBinauralShadowAmount01 = 0f;
                _targetBinauralShadowCutoffHertz = _sampleRate * 0.45f;
                _targetBinauralEnergy01 = 0f;
                _targetBinauralWaterDensityMul = 0f;
                _targetBinauralValid = 0;
                return;
            }

            _targetBinauralAzimuthRadians = telemetry.AzimuthRadians;
            _targetBinauralItdSeconds = telemetry.ItdSeconds;
            _targetBinauralShadowAmount01 = telemetry.ShadowAmount01;
            _targetBinauralShadowCutoffHertz = telemetry.ShadowCutoffHertz;
            _targetBinauralEnergy01 = math.saturate(telemetry.Energy);
            _targetBinauralWaterDensityMul = math.saturate(telemetry.WaterDensityMul);
            _targetBinauralValid = telemetry.Valid;
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

        private bool TryEnqueueImpactAudioEvent(
            float stress,
            float metallic,
            float clangExcitation,
            float echoExcitation,
            float echoDelaySeconds,
            float echoAttenuation,
            float echoLowPassCutoffHz,
            float echoPitchScale)
        {
            ImpactAudioEvent impactAudioEvent = new ImpactAudioEvent
            {
                Stress = math.saturate(stress),
                Metallic = math.saturate(metallic),
                ClangExcitation = math.saturate(clangExcitation),
                EchoExcitation = math.saturate(echoExcitation),
                EchoDelaySeconds = math.max(0f, echoDelaySeconds),
                EchoAttenuation = math.saturate(echoAttenuation),
                EchoLowPassCutoffHz = math.clamp(
                    echoLowPassCutoffHz,
                    AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                    AcousticOcclusionUtility.OpenLowPassCutoffHertz),
                EchoPitchScale = math.clamp(echoPitchScale, 0.65f, 1.45f)
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

                int writeIndex = Volatile.Read(ref _impactEventWriteIndex);
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
                Interlocked.Exchange(ref _impactEventWriteIndex, nextWriteIndex);
                SignalAudioProducerThread();
                return true;
            }
        }

        private bool TryDequeueImpactAudioEvent(out ImpactAudioEvent impactAudioEvent)
        {
            int readIndex = Volatile.Read(ref _impactEventReadIndex);
            if (readIndex == Volatile.Read(ref _impactEventWriteIndex))
            {
                impactAudioEvent = default;
                return false;
            }

            impactAudioEvent = _impactEventQueue[readIndex];
            Interlocked.Exchange(ref _impactEventReadIndex, (readIndex + 1) & ImpactEventQueueMask);
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
            HullSynthesisState hullState = _hullSynthesisState;
            uint clangSeed = (uint)_producedSampleCount ^ 0x51F2A8B3u;
            float strongestEchoEnergy = _impactEchoSynthesisState.Excitation * _impactEchoSynthesisState.Attenuation;

            while (TryDequeueImpactAudioEvent(out ImpactAudioEvent impactAudioEvent))
            {
                impactStressTarget = math.max(impactStressTarget, impactAudioEvent.Stress);
                impactMetallicTarget = math.max(impactMetallicTarget, impactAudioEvent.Metallic);
                if (impactAudioEvent.ClangExcitation > ImpactClangMinimumExcitation)
                {
                    ArmImpactClangInternal(ref hullState, impactAudioEvent.ClangExcitation, clangSeed);
                    clangSeed += 0x9E3779B9u;
                }

                if (impactAudioEvent.EchoExcitation > ImpactEchoMinimumExcitation &&
                    impactAudioEvent.EchoAttenuation > 0.0001f)
                {
                    float eventEchoEnergy = impactAudioEvent.EchoExcitation * impactAudioEvent.EchoAttenuation;
                    if (eventEchoEnergy >= strongestEchoEnergy)
                    {
                        _impactEchoSynthesisState.DelayRemainingSeconds = impactAudioEvent.EchoDelaySeconds;
                        _impactEchoSynthesisState.Excitation = impactAudioEvent.EchoExcitation;
                        _impactEchoSynthesisState.Attenuation = impactAudioEvent.EchoAttenuation;
                        _impactEchoSynthesisState.LowPassCutoffHz = impactAudioEvent.EchoLowPassCutoffHz;
                        _impactEchoSynthesisState.LowPassState = 0f;
                        _impactEchoSynthesisState.ElapsedSeconds = 0f;
                        _impactEchoSynthesisState.PitchScale = math.clamp(impactAudioEvent.EchoPitchScale, 0.65f, 1.45f);
                        strongestEchoEnergy = eventEchoEnergy;
                    }
                }
            }

            _hullSynthesisState = hullState;

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
            float structuralFatigueTarget,
            float structuralSnapTarget,
            float depthParamTarget,
            float absoluteDepthMetersTarget,
            float enclosureDensityTarget,
            float impactMetallicTarget)
        {
            HullSynthesisState state = _hullSynthesisState;
            float stressStart = _audioHullStressValue;
            float structuralStressStart = _audioStructuralHullStressValue;
            float structuralStressVelocityStart = _audioStructuralHullStressVelocityValue;
            float structuralFatigueStart = _audioStructuralFatigueValue;
            float structuralSnapStart = _audioStructuralSnapValue;
            float depthParamStart = _audioHullPressureDepthValue;
            float absoluteDepthStart = _audioAbsoluteDepthMeters;
            float enclosureDensityStart = _audioEnclosureDensityIndex;
            float impactMetallicImpulse = math.saturate(impactMetallicTarget);
            float previousStructuralSnap = structuralSnapStart;
            ComputeBandPassCoefficients(
                math.max(32f, _sampleRate * 0.25f),
                HullStressFmOversampleLowPassQ,
                math.max(1, _sampleRate << 1),
                out float stressFmOversampleB0,
                out float stressFmOversampleB1,
                out float stressFmOversampleB2,
                out float stressFmOversampleA1,
                out float stressFmOversampleA2);

            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                float frameT = frameCount > 1 ? frameIndex / (float)(frameCount - 1) : 0f;
                float stress = math.lerp(stressStart, hullTarget, frameT);
                float structuralStress = math.lerp(structuralStressStart, structuralHullTarget, frameT);
                float structuralStressVelocity = math.lerp(structuralStressVelocityStart, structuralHullVelocityTarget, frameT);
                float structuralFatigue = math.lerp(structuralFatigueStart, structuralFatigueTarget, frameT);
                float structuralSnap = math.lerp(structuralSnapStart, structuralSnapTarget, frameT);
                float depthParam = math.lerp(depthParamStart, depthParamTarget, frameT);
                float absoluteDepthMeters = math.lerp(absoluteDepthStart, absoluteDepthMetersTarget, frameT);
                float enclosureDensityIndex = math.lerp(enclosureDensityStart, enclosureDensityTarget, frameT);
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
                float fatigueRing = RenderStructuralFatigueRingSample(ref state, sampleIndex, structuralFatigue, structuralStress, invSampleRate);
                float stressFm = RenderHullStressFmSample(
                    ref state,
                    sampleIndex,
                    stress,
                    invSampleRate,
                    stressFmOversampleB0,
                    stressFmOversampleB1,
                    stressFmOversampleB2,
                    stressFmOversampleA1,
                    stressFmOversampleA2);
                float structuralSnapTransient = RenderStructuralSnapTransientSample(
                    ref state,
                    sampleIndex,
                    structuralSnap,
                    previousStructuralSnap,
                    structuralStress,
                    invSampleRate);
                float impactClang = RenderImpactClangSampleInternal(ref state, sampleIndex, invSampleRate);
                float subBass = RenderHullSubBassSample(ref state, structuralStress, depthParam, absoluteDepthMeters, enclosureDensityIndex, invSampleRate);
                float rivetBurst = BuildRivetBurst(sampleIndex, math.max(stress, metallicImpulse), rivetAmount);
                float combined = pressureBed + metal + pressureCreak + granularMetal + fatigueRing + stressFm + structuralSnapTransient + impactClang + rivetBurst + subBass;
                combined = ApplyDepthHullDistortion(combined, depthParam, structuralStress);
                _hullScratch[frameIndex] = math.max(stress, structuralSnap) <= HullNoiseFloor
                    ? 0f
                    : math.tanh(combined * math.lerp(1.7f, 2.8f, metallicImpulse)) * hullMasterGain;
                previousStructuralSnap = structuralSnap;
                AdvancePressureCreakEnvelope(ref state);
            }

            _hullSynthesisState = state;
            _audioHullStressValue = hullTarget;
            _audioStructuralHullStressValue = structuralHullTarget;
            _audioStructuralHullStressVelocityValue = structuralHullVelocityTarget;
            _audioStructuralFatigueValue = structuralFatigueTarget;
            _audioStructuralSnapValue = structuralSnapTarget;
            _audioHullPressureDepthValue = depthParamTarget;
            _audioAbsoluteDepthMeters = absoluteDepthMetersTarget;
            _audioEnclosureDensityIndex = enclosureDensityTarget;
        }

        private static void ClearScratchBuffer(NativeArray<float> buffer, int frameCount)
        {
            if (!buffer.IsCreated || frameCount <= 0)
                return;

            int safeCount = math.min(frameCount, buffer.Length);
            for (int i = 0; i < safeCount; i++)
                buffer[i] = 0f;
        }

        private static void FillScratchBuffer(NativeArray<float> buffer, int frameCount, float value)
        {
            if (!buffer.IsCreated || frameCount <= 0)
                return;

            int safeCount = math.min(frameCount, buffer.Length);
            for (int i = 0; i < safeCount; i++)
                buffer[i] = value;
        }

        private void ApplyBinauralSpatializationBlock(int frameCount, AudioParameterSnapshot parameters)
        {
            if (!_stereoMixScratch.IsCreated || !_binauralDelayRing.IsCreated || !_binauralShadowHistory.IsCreated)
                return;

            bool hasDirectionalTarget = parameters.BinauralValid != 0;
            float azimuth = hasDirectionalTarget ? parameters.BinauralAzimuthRadians : 0f;
            float airItdSeconds = hasDirectionalTarget ? math.max(0f, parameters.BinauralItdSeconds) : 0f;
            float shadowAmount = hasDirectionalTarget ? math.saturate(parameters.BinauralShadowAmount01) : 0f;
            float waterDensityMul = hasDirectionalTarget ? math.saturate(parameters.BinauralWaterDensityMul) : 0f;
            float itdSeconds = math.lerp(airItdSeconds, airItdSeconds * BinauralWaterItdDelayRatio, waterDensityMul);
            float shadowCutoffHertz = hasDirectionalTarget
                ? math.clamp(parameters.BinauralShadowCutoffHertz, 400f, _sampleRate * 0.45f)
                : _sampleRate * 0.45f;
            shadowCutoffHertz = math.min(
                shadowCutoffHertz,
                math.lerp(_sampleRate * 0.45f, BinauralUnderwaterShadowCutoffHertz, waterDensityMul));
            float spatialEnergy = hasDirectionalTarget ? math.saturate(parameters.BinauralEnergy01) : 0f;
            float binauralMix = hasDirectionalTarget ? math.lerp(0.18f, 0.85f, spatialEnergy) : 0f;
            float contraFloor = math.lerp(BinauralAirShadowMinimumGain, BinauralWaterShadowMinimumGain, waterDensityMul);
            float contraGain = math.lerp(1f, contraFloor, shadowAmount * binauralMix);
            int delaySamples = hasDirectionalTarget
                ? math.clamp((int)math.round(itdSeconds * math.max(_sampleRate, 1)), 0, BinauralMaximumDelaySamples)
                : 0;
            int delayLeftSamples = azimuth > 0f ? delaySamples : 0;
            int delayRightSamples = azimuth < 0f ? delaySamples : 0;
            float shadowAlpha = math.exp(
                (-TwoPi * math.max(400f, shadowCutoffHertz)) /
                math.max(_sampleRate, 1f));

            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                float mono = _mixScratch[frameIndex];
                _binauralDelayRing[_binauralDelayWriteIndex] = mono;

                float delayedLeft = delayLeftSamples > 0
                    ? _binauralDelayRing[(_binauralDelayWriteIndex - delayLeftSamples) & BinauralDelayMask]
                    : mono;
                float delayedRight = delayRightSamples > 0
                    ? _binauralDelayRing[(_binauralDelayWriteIndex - delayRightSamples) & BinauralDelayMask]
                    : mono;

                _binauralDelayWriteIndex = (_binauralDelayWriteIndex + 1) & BinauralDelayMask;

                float leftSpatial = delayedLeft;
                float rightSpatial = delayedRight;
                if (hasDirectionalTarget)
                {
                    if (azimuth > 0f)
                    {
                        leftSpatial = ApplyBinauralShadowEar(delayedLeft * contraGain, 0, shadowAlpha);
                        rightSpatial = delayedRight;
                    }
                    else if (azimuth < 0f)
                    {
                        leftSpatial = delayedLeft;
                        rightSpatial = ApplyBinauralShadowEar(delayedRight * contraGain, 1, shadowAlpha);
                    }
                }

                float left = math.lerp(mono, leftSpatial, binauralMix);
                float right = math.lerp(mono, rightSpatial, binauralMix);
                int stereoIndex = frameIndex << 1;
                _stereoMixScratch[stereoIndex] = math.clamp(left, -1f, 1f);
                _stereoMixScratch[stereoIndex + 1] = math.clamp(right, -1f, 1f);
            }
        }

        private float ApplyBinauralShadowEar(float sample, int earIndex, float alpha)
        {
            float previous = _binauralShadowHistory[earIndex] + BiquadDenormalBias;
            float filtered = sample + alpha * (previous - sample);
            _binauralShadowHistory[earIndex] = filtered;
            return filtered;
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

            NativeArray<SonarEchoTap> activeTapBuffer = _workerActiveSonarTapBufferIndex == 0 ? _pendingSonarEchoTapsA : _pendingSonarEchoTapsB;
            int activeTapCount = math.clamp(_workerActiveSonarTapCount, 0, SonarEchoTapCapacity);
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
                for (int tapIndex = 0; tapIndex < activeTapCount; tapIndex++)
                {
                    SonarEchoTap tap = activeTapBuffer[tapIndex];
                    float echoAge = age - tap.DelaySeconds;
                    if (echoAge < 0f)
                    {
                        if (_sonarEchoReadCursors.IsCreated)
                            _sonarEchoReadCursors[tapIndex] = -1d;
                        continue;
                    }

                    if (echoAge >= SonarChirpDurationSeconds || !_sonarEchoDelay.IsCreated || !_sonarEchoReadCursors.IsCreated)
                        continue;

                    int echoDelaySamples = math.clamp(
                        (int)math.round(tap.DelaySeconds * math.max(_sampleRate, 1)),
                        1,
                        SonarEchoDelayCapacity - 4);

                    double echoReadCursor = _sonarEchoReadCursors[tapIndex];
                    if (echoReadCursor < 0d)
                        echoReadCursor = (state.EchoWriteIndex - echoDelaySamples) & SonarEchoDelayMask;

                    float dopplerRatio = math.clamp(
                        tap.DopplerRatio,
                        SonarEchoMinimumDopplerRatio,
                        SonarEchoMaximumDopplerRatio);
                    float tapEcho = HermiteSampleRing(_sonarEchoDelay, echoReadCursor, SonarEchoDelayMask) *
                                    (math.exp(-echoAge * 4.5f) * tap.Attenuation);
                    echoReadCursor = WrapRingCursor(echoReadCursor + dopplerRatio, SonarEchoDelayCapacity);
                    _sonarEchoReadCursors[tapIndex] = echoReadCursor;

                    if (tap.UseLowPass != 0 &&
                        _sonarEchoFilterInput1.IsCreated &&
                        _sonarEchoFilterInput2.IsCreated &&
                        _sonarEchoFilterOutput1.IsCreated &&
                        _sonarEchoFilterOutput2.IsCreated)
                    {
                        float filteredEcho =
                            tap.LowPassB0 * tapEcho +
                            tap.LowPassB1 * _sonarEchoFilterInput1[tapIndex] +
                            tap.LowPassB2 * _sonarEchoFilterInput2[tapIndex] -
                            tap.LowPassA1 * (_sonarEchoFilterOutput1[tapIndex] + BiquadDenormalBias) -
                            tap.LowPassA2 * (_sonarEchoFilterOutput2[tapIndex] + BiquadDenormalBias);

                        _sonarEchoFilterInput2[tapIndex] = _sonarEchoFilterInput1[tapIndex];
                        _sonarEchoFilterInput1[tapIndex] = tapEcho;
                        _sonarEchoFilterOutput2[tapIndex] = _sonarEchoFilterOutput1[tapIndex];
                        _sonarEchoFilterOutput1[tapIndex] = filteredEcho;
                        tapEcho = filteredEcho;
                    }

                    echo += tapEcho;
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

        private void RenderImpactEchoBlock(int frameCount, double invSampleRate)
        {
            if (!_impactEchoScratch.IsCreated)
                return;

            ImpactEchoSynthesisState state = _impactEchoSynthesisState;
            if (state.Excitation <= ImpactEchoMinimumExcitation || state.Attenuation <= 0.0001f)
            {
                ClearScratchBuffer(_impactEchoScratch, frameCount);
                return;
            }

            float decayRate = math.max(ImpactEchoDecayPerSecond, 0.01f);
            float lowPassCutoff = math.clamp(
                state.LowPassCutoffHz,
                AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                AcousticOcclusionUtility.OpenLowPassCutoffHertz);
            float lowPassAlpha = math.exp((-TwoPi * lowPassCutoff) / math.max(_sampleRate, 1f));
            float pitchScale = math.clamp(state.PitchScale <= 0f ? 1f : state.PitchScale, 0.65f, 1.45f);

            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                if (state.DelayRemainingSeconds > 0f)
                {
                    state.DelayRemainingSeconds = math.max(0f, state.DelayRemainingSeconds - (float)invSampleRate);
                    _impactEchoScratch[frameIndex] = 0f;
                    continue;
                }

                float age = state.ElapsedSeconds;
                if (age >= ImpactEchoMaximumLifetimeSeconds)
                {
                    _impactEchoScratch[frameIndex] = 0f;
                    state.Excitation = 0f;
                    state.Attenuation = 0f;
                    continue;
                }

                uint sampleIndex = (uint)math.max(0f, age * _sampleRate);
                float envelope = math.exp(-age * decayRate);
                float tonal =
                    AdvanceSine(ref state.CarrierPhaseA, ImpactEchoCarrierPrimaryHertz * pitchScale, invSampleRate) * 0.62f +
                    AdvanceSine(ref state.CarrierPhaseB, ImpactEchoCarrierSecondaryHertz * pitchScale, invSampleRate) * 0.38f;
                float noise = LayeredPinkLike(sampleIndex) * ImpactEchoNoiseBlend;
                float raw = (tonal + noise) * envelope * state.Excitation * state.Attenuation;
                float filtered = raw + lowPassAlpha * ((state.LowPassState + BiquadDenormalBias) - raw);
                state.LowPassState = filtered;
                _impactEchoScratch[frameIndex] = math.tanh(filtered * 2.2f) * 0.35f;
                state.ElapsedSeconds += (float)invSampleRate;
            }

            _impactEchoSynthesisState = state;
        }

        private void ArmImpactClangInternal(ref HullSynthesisState state, float excitation, uint seed)
        {
            if (!_impactClangDelay.IsCreated || excitation <= ImpactClangMinimumExcitation)
                return;

            float pitchScale = math.lerp(
                1f - ImpactClangPitchSpread,
                1f + ImpactClangPitchSpread,
                Hash01(seed ^ 0x13A531D7u));
            int delaySamples = math.clamp(
                (int)math.round(_sampleRate / math.max(24f, ImpactClangFundamentalHertz * pitchScale)),
                2,
                ImpactClangDelayCapacity - 2);
            int writeIndex = state.ImpactClangWriteIndex & ImpactClangDelayMask;
            float seedAmplitude = math.lerp(0.22f, 0.95f, excitation);
            float noiseDecay = 1f;
            for (int i = 0; i < delaySamples; i++)
            {
                int ringIndex = (writeIndex + i) & ImpactClangDelayMask;
                _impactClangDelay[ringIndex] = HashSigned(seed + (uint)i * 0x9E3779B9u) * seedAmplitude * noiseDecay;
                noiseDecay *= ImpactClangNoiseSeedDecay;
            }

            state.ImpactClangDelaySamples = delaySamples;
            state.ImpactClangEnvelope = math.max(state.ImpactClangEnvelope, excitation);
            state.ImpactClangFeedback = math.lerp(ImpactClangFeedbackMinimum, ImpactClangFeedbackMaximum, excitation);
            state.ImpactClangLowPassState = 0f;
            state.ImpactClangWriteIndex = (writeIndex + delaySamples) & ImpactClangDelayMask;
        }

        private float RenderImpactClangSampleInternal(ref HullSynthesisState state, uint sampleIndex, double invSampleRate)
        {
            if (!_impactClangDelay.IsCreated ||
                state.ImpactClangEnvelope <= 0.0001f ||
                state.ImpactClangDelaySamples <= 1)
            {
                return 0f;
            }

            int writeIndex = state.ImpactClangWriteIndex & ImpactClangDelayMask;
            int delaySamples = math.clamp(state.ImpactClangDelaySamples, 2, ImpactClangDelayCapacity - 2);
            int readIndexA = (writeIndex - delaySamples) & ImpactClangDelayMask;
            int readIndexB = (readIndexA - 1) & ImpactClangDelayMask;
            float delayedA = _impactClangDelay[readIndexA];
            float delayedB = _impactClangDelay[readIndexB];
            float averaged = (delayedA + delayedB) * 0.5f;
            float filtered = math.lerp(delayedA, averaged, ImpactClangLowPassBlend);
            float noiseEdge = HashSigned(sampleIndex ^ 0x61F0B1C3u) * 0.018f * state.ImpactClangEnvelope;
            state.ImpactClangLowPassState = math.lerp(state.ImpactClangLowPassState + BiquadDenormalBias, filtered + noiseEdge, 0.5f);
            _impactClangDelay[writeIndex] = state.ImpactClangLowPassState * state.ImpactClangFeedback;
            state.ImpactClangWriteIndex = (writeIndex + 1) & ImpactClangDelayMask;

            float output = math.tanh(
                state.ImpactClangLowPassState *
                (1.55f + state.ImpactClangEnvelope * 2.4f)) *
                state.ImpactClangEnvelope *
                0.42f;
            state.ImpactClangEnvelope *= math.exp(-ImpactClangEnvelopeDecayPerSecond * (float)invSampleRate);
            if (state.ImpactClangEnvelope <= 0.0001f)
            {
                state.ImpactClangEnvelope = 0f;
                state.ImpactClangFeedback = 0f;
            }

            return output;
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
                float cavitationModulator =
                    AdvanceSine(
                        ref state.CavitationModulatorPhase,
                        math.lerp(26f, 112f, math.saturate(acceleration + pressure * 0.25f)),
                        invSampleRate) *
                    math.lerp(40f, 420f, math.saturate(acceleration * 0.85f + load * 0.15f));
                double cavitationCarrierFrequency = math.max(
                    420d,
                    math.lerp(1200f, 4200f, math.saturate(acceleration * 0.9f + load * 0.1f)) + cavitationModulator);
                AdvancePhase(ref state.CavitationCarrierPhase, cavitationCarrierFrequency, invSampleRate);
                float cavitationFm =
                    math.sin((float)(TwoPi * state.CavitationCarrierPhase) + highNoise * 0.6f) *
                    dynamicEnvelope *
                    math.saturate(acceleration * 0.82f + pressure * 0.35f + dive * 0.12f);

                float mixed = hum + flow + (cavitation * 0.56f + cavitationFm * 0.44f) * 0.78f;
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

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float RenderHeartbeatEnvelope(float ageSeconds)
        {
            if (ageSeconds < 0f)
                return 0f;

            float attackEnd = HeartbeatAttackSeconds;
            float decayEnd = attackEnd + HeartbeatDecaySeconds;
            float sustainEnd = decayEnd + HeartbeatSustainSeconds;
            float releaseEnd = sustainEnd + HeartbeatReleaseSeconds;
            if (ageSeconds >= releaseEnd)
                return 0f;

            if (ageSeconds < attackEnd)
                return ageSeconds / math.max(HeartbeatAttackSeconds, 0.0001f);

            if (ageSeconds < decayEnd)
            {
                float decayT = (ageSeconds - attackEnd) / math.max(HeartbeatDecaySeconds, 0.0001f);
                return math.lerp(1f, 0.58f, decayT);
            }

            if (ageSeconds < sustainEnd)
                return 0.58f;

            float releaseT = (ageSeconds - sustainEnd) / math.max(HeartbeatReleaseSeconds, 0.0001f);
            return math.lerp(0.58f, 0f, releaseT);
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

        private static float RenderHullStressFmSample(
            ref HullSynthesisState state,
            uint sampleIndex,
            float stressParam,
            double invSampleRate,
            float oversampleB0,
            float oversampleB1,
            float oversampleB2,
            float oversampleA1,
            float oversampleA2)
        {
            if (stressParam <= HullNoiseFloor)
                return 0f;

            float stressSquared = stressParam * stressParam;
            float modulationFrequency = math.lerp(
                HullStressFmModulationMinimumHertz,
                HullStressFmModulationMaximumHertz,
                stressSquared);
            float modulationIndex = math.lerp(
                HullStressFmModulationIndexMinimum,
                HullStressFmModulationIndexMaximum,
                stressParam);
            int oversampleFactor = modulationIndex > HullStressFmOversampleIndexThreshold ? 2 : 1;
            double oversampleInvSampleRate = invSampleRate / oversampleFactor;
            float accumulated = 0f;

            for (int oversampleIndex = 0; oversampleIndex < oversampleFactor; oversampleIndex++)
            {
                uint oversampleSampleIndex = sampleIndex * (uint)oversampleFactor + (uint)oversampleIndex;
                float filteredNoise = HighBandNoise(oversampleSampleIndex ^ 0x1F27C4B3u);
                float modulator =
                    AdvanceSine(ref state.StressFmModulatorPhase, modulationFrequency, oversampleInvSampleRate) *
                    modulationIndex *
                    filteredNoise;
                float carrierFrequency = HullStressFmBaseCarrierHertz + modulator;
                AdvancePhase(ref state.StressFmCarrierPhase, carrierFrequency, oversampleInvSampleRate);
                float raw = math.sin((float)(TwoPi * state.StressFmCarrierPhase));
                float shaped = math.tanh(raw * (1f + stressParam * 3f));

                if (oversampleFactor > 1)
                {
                    shaped = ProcessBiquad(
                        shaped,
                        oversampleB0,
                        oversampleB1,
                        oversampleB2,
                        oversampleA1,
                        oversampleA2,
                        ref state.StressFmOversampleInput1,
                        ref state.StressFmOversampleInput2,
                        ref state.StressFmOversampleOutput1,
                        ref state.StressFmOversampleOutput2);
                }

                accumulated += shaped;
            }

            float averaged = accumulated / oversampleFactor;
            float dcBlocked =
                averaged -
                state.StressFmDcBlockInput +
                HullStressFmDcBlockPole * (state.StressFmDcBlockOutput + BiquadDenormalBias);
            state.StressFmDcBlockInput = averaged;
            state.StressFmDcBlockOutput = dcBlocked;
            return dcBlocked * math.lerp(0.08f, HullStressFmMasterGain, stressParam);
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
            float absoluteDepthMeters,
            float enclosureDensityIndex,
            double invSampleRate)
        {
            if (depthParam <= HullNoiseFloor)
                return 0f;

            float frequency = math.lerp(HullSubBassMaximumHertz, HullSubBassMinimumHertz, depthParam);
            float sine = AdvanceSine(ref state.SubBassPhase, frequency, invSampleRate);
            double trianglePhase = state.SubBassPhase;
            float triangle = (float)(2.0 * math.abs((float)(2.0 * (trianglePhase - math.floor(trianglePhase + 0.5)))) - 1.0);
            float amplitude = HullSubBassMaximumGain * math.saturate(depthParam * 0.85f + structuralStress * 0.15f);
            float boostedDepth01 = math.saturate(
                (absoluteDepthMeters - DepthSubwooferBoostStartDepthMeters) /
                math.max(DepthSubwooferBoostFullDepthMeters - DepthSubwooferBoostStartDepthMeters, 1f));
            float depthSine = AdvanceSine(ref state.DepthSubwooferPhase, DepthSubwooferBoostFrequencyHertz, invSampleRate);
            float depthSineAmplitude =
                DepthSubwooferBoostMaximumGain *
                boostedDepth01 *
                math.lerp(0.45f, 1f, structuralStress);
            float enclosureDensity = math.saturate(enclosureDensityIndex);
            float dreadFrequency = math.lerp(DreadRumbleMaximumHertz, DreadRumbleMinimumHertz, enclosureDensity);
            float dreadSine = AdvanceSine(ref state.DreadRumblePhase, dreadFrequency, invSampleRate);
            float dreadAmplitude =
                DreadRumbleMaximumGain *
                boostedDepth01 *
                math.lerp(0.35f, DreadRumbleCaveBoost, enclosureDensity);
            return ((sine * 0.76f + triangle * 0.24f) * amplitude) +
                   (depthSine * depthSineAmplitude) +
                   (dreadSine * dreadAmplitude);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float RenderStructuralSnapTransientSample(
            ref HullSynthesisState state,
            uint sampleIndex,
            float structuralSnap,
            float previousStructuralSnap,
            float structuralStress,
            double invSampleRate)
        {
            float risingEdge = math.max(0f, structuralSnap - previousStructuralSnap);
            if (risingEdge > 0.0005f)
            {
                state.StructuralSnapEnvelope = math.max(state.StructuralSnapEnvelope, math.saturate(risingEdge * 9f));
                state.StructuralSnapPitchScale = math.lerp(
                    StructuralSnapPitchMinimum,
                    StructuralSnapPitchMaximum,
                    Hash01(sampleIndex ^ 0x19C4F5B3u));
            }

            if (state.StructuralSnapEnvelope <= HullNoiseFloor)
                return 0f;

            float frequency = math.lerp(
                StructuralSnapMinimumHertz,
                StructuralSnapMaximumHertz,
                math.saturate(structuralSnap * 0.75f + structuralStress * 0.25f)) *
                (state.StructuralSnapPitchScale > 0f ? state.StructuralSnapPitchScale : 1f);
            AdvancePhase(ref state.StructuralSnapPhase, frequency, invSampleRate);
            float sine = math.sin((float)(TwoPi * state.StructuralSnapPhase));
            float harmonic = math.sin((float)(TwoPi * state.StructuralSnapPhase * 1.82d)) * 0.34f;
            float noise = HighBandNoise(sampleIndex ^ 0x51BA1D3u) * 0.26f;
            float amplitude =
                StructuralSnapMaximumGain *
                state.StructuralSnapEnvelope *
                math.lerp(0.45f, 1f, structuralStress);
            state.StructuralSnapEnvelope = math.max(
                0f,
                state.StructuralSnapEnvelope - (float)invSampleRate * StructuralSnapDecayPerSecond);
            return (sine + harmonic + noise) * amplitude;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float RenderStructuralFatigueRingSample(
            ref HullSynthesisState state,
            uint sampleIndex,
            float structuralFatigue,
            float structuralStress,
            double invSampleRate)
        {
            if (structuralFatigue <= HullNoiseFloor)
                return 0f;

            float fatigue = math.saturate(structuralFatigue);
            float frequency =
                math.lerp(StructuralFatigueRingMinimumHertz, StructuralFatigueRingMaximumHertz, fatigue) *
                math.lerp(0.94f, 1.08f, 0.5f + 0.5f * HeldNoise(sampleIndex, 5, 0x3E91F4A1u));
            float amplitude =
                StructuralFatigueRingMaximumGain *
                fatigue *
                math.lerp(0.35f, 1f, structuralStress);
            float modulation =
                0.55f +
                0.45f * AdvanceSine(ref state.FatigueRingModulationPhase, StructuralFatigueRingModulationHertz, invSampleRate);
            AdvancePhase(ref state.FatigueRingCarrierPhase, frequency, invSampleRate);
            float ring = math.sin((float)(TwoPi * state.FatigueRingCarrierPhase));
            float harmonic = math.sin((float)(TwoPi * state.FatigueRingCarrierPhase * 1.97d)) * 0.38f;
            return (ring + harmonic) * amplitude * modulation;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float ApplyDepthHullDistortion(float sample, float depthParam, float structuralStress)
        {
            float depthMeters = depthParam * PressureCreakDepthReferenceMeters;
            float depthBlend = math.saturate(
                (depthMeters - AbyssalHullDistortionStartDepthMeters) /
                math.max(1f, AbyssalHullDistortionFullDepthMeters - AbyssalHullDistortionStartDepthMeters));
            if (depthBlend <= HullNoiseFloor)
                return sample;

            float distortionBlend = depthBlend * math.lerp(0.55f, 1f, math.saturate(structuralStress)) * AbyssalHullDistortionMaximumBlend;
            float drive = math.lerp(1f, AbyssalHullDistortionMaximumDrive, depthBlend);
            float distorted = math.tanh(sample * drive);
            return math.lerp(sample, distorted, distortionBlend);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float ResolveDescendingNormalized01(float value, float start, float end)
        {
            if (value >= start)
                return 0f;

            if (value <= end)
                return 1f;

            return math.saturate((start - value) / math.max(start - end, 0.0001f));
        }

        private static float AdvanceSine(ref double phase, double frequencyHz, double invSampleRate)
        {
            return math.sin((float)(TwoPi * AdvancePhase(ref phase, frequencyHz, invSampleRate)));
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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
            int capacity = mask + 1;
            if (!buffer.IsCreated || capacity <= 0)
                return 0f;

            double wrappedCursor = WrapRingCursor(cursor, capacity);
            int baseIndex = (int)math.floor(wrappedCursor);
            float t = math.clamp((float)(wrappedCursor - baseIndex), 0f, HermiteFractionMaximum);
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

        private static double WrapRingCursor(double cursor, int capacity)
        {
            if (capacity <= 0 || double.IsNaN(cursor) || double.IsInfinity(cursor))
                return 0d;

            double wrapped = cursor - math.floor(cursor / capacity) * capacity;
            if (wrapped < 0d)
                wrapped += capacity;
            if (wrapped >= capacity)
                wrapped -= capacity;
            return wrapped;
        }

        private static float HeldNoise(uint sampleIndex, int shift, uint seed)
        {
            return HashSigned((sampleIndex >> shift) ^ seed);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float SampleSimplex01(float position, uint seed)
        {
            float seedX = (seed & 0xFFFFu) * 0.00006103515625f;
            float seedY = ((seed >> 16) & 0xFFFFu) * 0.00006103515625f;
            float simplex = noise.snoise(new float2(position + seedX * 31.17f, seedY * 17.93f));
            return math.saturate(simplex * 0.5f + 0.5f);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float ResolveMinnaertFrequency(float radiusMeters, float ambientPressurePascals)
        {
            float safeRadius = math.max(radiusMeters, 0.0001f);
            float pressure = math.max(ambientPressurePascals, 1f);
            float numerator = 3f * WaterHeatRatioGamma * pressure;
            float root = math.sqrt(numerator / WaterDensityKilogramsPerCubicMeter);
            return math.rcp(2f * math.PI * safeRadius) * root;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static void RenderMinnaertBubbleBurstKernel(
            NativeArray<float> output,
            int frameCount,
            long blockStartFrame,
            double invSampleRate,
            float startIntensity,
            float targetIntensity,
            float absoluteDepthMeters,
            ref BubbleSynthesisState state)
        {
            float ambientPressure =
                WaterAmbientPressureSeaLevelPascals +
                WaterDensityKilogramsPerCubicMeter * WaterGravityMetersPerSecondSquared * math.max(0f, absoluteDepthMeters);
            float deltaSeconds = math.max((float)invSampleRate, 0f);

            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                float frameT = frameCount > 1 ? frameIndex / (float)(frameCount - 1) : 1f;
                float boilIntensity = math.lerp(startIntensity, targetIntensity, frameT);
                state.TimeToNextSpawnSeconds -= deltaSeconds;
                if (boilIntensity > HullNoiseFloor && state.TimeToNextSpawnSeconds <= 0f)
                {
                    long sampleFrame = blockStartFrame + frameIndex;
                    uint sampleIndex = sampleFrame > 0L ? (uint)sampleFrame : 0u;
                    state.SpawnSeed = sampleIndex ^ 0x5E17A4C3u;
                    float radius = math.lerp(BubbleRadiusMinimumMeters, BubbleRadiusMaximumMeters, Hash01(state.SpawnSeed));
                    state.FrequencyHertz = math.clamp(
                        ResolveMinnaertFrequency(radius, ambientPressure),
                        120f,
                        3800f);
                    state.DecayPerSecond = math.lerp(BubbleDecayMinimumPerSecond, BubbleDecayMaximumPerSecond, boilIntensity);
                    state.Envelope = math.lerp(0.15f, 1f, boilIntensity);
                    state.TimeToNextSpawnSeconds = math.rcp(math.lerp(BubbleBoilSpawnRateMinimum, BubbleBoilSpawnRateMaximum, boilIntensity));
                }

                output[frameIndex] = state.Envelope > HullNoiseFloor
                    ? RenderMinnaertBubbleSample(ref state, invSampleRate)
                    : 0f;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float RenderMinnaertBubbleSample(ref BubbleSynthesisState state, double invSampleRate)
        {
            float sine = AdvanceSine(ref state.Phase, state.FrequencyHertz, invSampleRate);
            float burst = sine * state.Envelope * BubbleMaximumGain;
            state.Envelope = math.max(0f, state.Envelope - (state.DecayPerSecond * math.max((float)invSampleRate, 0f)));
            return burst;
        }

        private void PublishAudioParameterSnapshot()
        {
            AudioParameterSnapshot snapshot = new AudioParameterSnapshot
            {
                HullStress = _targetHullStressValue,
                StructuralHullStress = _targetStructuralHullStressValue,
                StructuralHullStressVelocity = _targetStructuralHullStressVelocityValue,
                StructuralFatigue = _targetStructuralFatigueValue,
                StructuralSnap = _targetStructuralSnapValue,
                HullPressureDepth = _targetHullPressureDepthValue,
                AbsoluteDepthMeters = _targetAbsoluteDepthMeters,
                EnclosureDensityIndex = _targetEnclosureDensityIndex,
                ReverbRt60Seconds = _targetReverbRt60Seconds,
                ReverbWetMix = _targetReverbWetMix,
                ReverbOpenness = _targetReverbOpenness,
                BubbleBoilIntensity = _targetBubbleBoilIntensity,
                ThrusterBlend = _targetThrusterBlendValue,
                ThrusterLoad = _targetThrusterLoadValue,
                ThrusterPitch = _targetThrusterPitchValue,
                ThrusterPressure = _targetThrusterPressureValue,
                ThrusterAcceleration = _targetThrusterAccelerationValue,
                ThrusterHeavyCarry = _targetThrusterHeavyCarryValue,
                ThrusterDive = _targetThrusterDiveValue,
                AbyssalLowPassMix = _targetAbyssalLowPassMix,
                HeartbeatStress = _targetHeartbeatStressValue,
                HeartbeatOxygenDanger = _targetHeartbeatOxygenDangerValue,
                HeartbeatActive = _targetHeartbeatActive,
                BinauralAzimuthRadians = _targetBinauralAzimuthRadians,
                BinauralItdSeconds = _targetBinauralItdSeconds,
                BinauralShadowAmount01 = _targetBinauralShadowAmount01,
                BinauralShadowCutoffHertz = _targetBinauralShadowCutoffHertz,
                BinauralEnergy01 = _targetBinauralEnergy01,
                BinauralWaterDensityMul = _targetBinauralWaterDensityMul,
                BinauralValid = _targetBinauralValid
            };

            int inactiveIndex = Volatile.Read(ref _audioParameterSnapshotReadIndex) ^ 1;
            if (inactiveIndex == 0)
                _audioParameterSnapshotA = snapshot;
            else
                _audioParameterSnapshotB = snapshot;

            Interlocked.Exchange(ref _audioParameterSnapshotReadIndex, inactiveIndex);
            SignalAudioProducerThread();
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
