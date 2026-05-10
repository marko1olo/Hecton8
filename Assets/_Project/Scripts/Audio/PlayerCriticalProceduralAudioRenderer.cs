using System;
using System.Threading;
using System.Runtime.InteropServices;
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
    [RequireComponent(typeof(AudioReverbFilter))]
    public sealed class PlayerCriticalProceduralAudioRenderer : MonoBehaviour, ITickable, ISlowTickable, ILateFrameTickable, IUpdatable, IProceduralAudioEventListener, IPhysicsImpactEventListener, IPhysicsAcousticImpulseEventListener, ISonarPingEventListener, IAcousticEchoEventListener, ILaserCutterEventListener
    {
        private const float TwoPi = 6.28318530718f;
        private const float InvTwoPi = 0.15915494309f;
        private const float NaturalLogTen = 2.3025851f;
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
        private const float HullGroanLoopPitchMinimum = 0.8f;
        private const float HullGroanLoopPitchMaximum = 1.2f;
        private const float HullGroanLoopPitchUpdateIntervalSeconds = 0.18f;
        private const float AbyssalLowPassStartDepthMeters = 500f;
        private const float AbyssalLowPassFadeDepthMeters = 4500f;
        private const float AbyssalLowPassCutoffHertz = 380f;
        private const float PsychoacousticPressureReferenceDepthMeters = 500f;
        private const float PsychoacousticPressureMinimumCutoffHertz = 420f;
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
        private const float PanicHeartbeatStressThreshold01 = 0.8f;
        private const float PanicHeartbeatAmbientHighCutMinimumGain = 0.38f;
        private const float PressurePhaserStartDepthMeters = 2000f;
        private const float PressurePhaserFullDepthMeters = 5000f;
        private const float PressurePhaserSweepMinimumHertz = 0.035f;
        private const float PressurePhaserSweepMaximumHertz = 0.11f;
        private const float PressurePhaserCoefficientMinimum = 0.18f;
        private const float PressurePhaserCoefficientMaximum = 0.78f;
        private const float PressurePhaserFeedback = 0.42f;
        private const float PressurePhaserWetMaximum = 0.38f;
        private const float ThrusterBandPassQ = 0.82f;
        private const float ThrusterBladePassFrequencyMinHertz = 22f;
        private const float ThrusterBladePassFrequencyMaxHertz = 116f;
        private const float ThrusterCombDamp = 0.22f;
        private const int BinauralOutputChannels = 2;
        private const int BinauralDelayCapacity = 128;
        private const int BinauralDelayMask = BinauralDelayCapacity - 1;
        private const int BinauralMaximumDelaySamples = 64;
        private const float BinauralMaximumMicroDelaySeconds = 0.0006f;
        private const float BinauralAirShadowMinimumGain = 0.34f;
        private const float BinauralWaterShadowMinimumGain = 0.58f;
        private const float BinauralUnderwaterShadowCutoffHertz = 3200f;
        private const int MaxSafeFrameCapacity = 16384;
        private const int MaxFilterChannels = 8;
        private const int SonarGhostEchoTapCount = 3;
        private const float FakeOpenWaterReverbMix01 = 0.2f;
        private const float FakeCaveReverbMix01 = 0.8f;
        private const int AudioProducerJoinTimeoutMs = 250;
        private const int AudioProducerIdleWaitTimeoutMs = 8;
        private const int SonarEchoDelayCapacity = 131072;
        private const int SonarEchoDelayMask = SonarEchoDelayCapacity - 1;
        private const int SonarEchoTapCapacity = 12;
        private const int SonarEchoCompositeCandidateCapacity = 32;
        private const int SonarEchoCompositeGroupCapacity = 8;
        private const double SonarEchoCompositeCellSizeMeters = 10d;
        private const float SonarEchoMinimumDopplerRatio = 0.05f;
        private const float SonarEchoMaximumDopplerRatio = 4f;
        private const int ImpactClangDelayCapacity = 1024;
        private const int ImpactClangDelayMask = ImpactClangDelayCapacity - 1;
        private const int ThrusterCombDelayCapacity = 4096;
        private const int ThrusterCombDelayMask = ThrusterCombDelayCapacity - 1;
        private const int SabineReverbCombCount = 4;
        private const int SabineReverbDelayLineLength = 65536;
        private const int SabineReverbDelayLineMask = SabineReverbDelayLineLength - 1;
        private const int SabineReverbDelayCapacity = SabineReverbCombCount * SabineReverbDelayLineLength;
        private const int InteriorFdnDelayCapacity = 8192;
        private const int InteriorFdnDelayMask = InteriorFdnDelayCapacity - 1;
        private const int InteriorFdnLaneLength = 2048;
        private const int InteriorFdnLaneMask = InteriorFdnLaneLength - 1;
        private const float SabineReverbDelayASeconds = 0.34f;
        private const float SabineReverbDelayBSeconds = 0.42f;
        private const float SabineReverbDelayCSeconds = 0.58f;
        private const float SabineReverbDelayDSeconds = 0.71f;
        private const float SabineReverbMaximumFeedback = 0.85f;
        private const float SabineReverbMinimumFeedback = 0.18f;
        private const float SabineReverbMaximumWetGain = 0.32f;
        private const float SabineReverbWetMixLerpCoefficient = 0.001f;
        private const float SabineReverbDampingClosedCutoffHertz = 950f;
        private const float SabineReverbDampingOpenCutoffHertz = 2400f;
        private const float InteriorFdnWetGainMaximum = 0.18f;
        private const float InteriorFdnFeedback = 0.42f;
        private const float InteriorFdnDamping = 0.62f;
        private const float DreadRumbleMinimumHertz = 15f;
        private const float DreadRumbleMaximumHertz = 30f;
        private const float DreadRumbleMaximumGain = 0.18f;
        private const float DreadRumbleCaveBoost = 1.65f;
        private const float EnclosureDensityFollowSharpness = 4.5f;
        private const float BubbleBoilMinimumHeatFloor = 0.08f;
        private const float ToolCavitationHeatStart01 = 0.72f;
        private const float ToolCavitationMaximumGain = 0.075f;
        private const float ToolCavitationBurstDensityMinimum = 0.025f;
        private const float ToolCavitationBurstDensityMaximum = 0.38f;
        private const int ToolCavitationBurstShift = 6;
        private const uint ToolCavitationBurstMask = (1u << ToolCavitationBurstShift) - 1u;
        private const float BoilingWaterLoopPitchMinimum = 0.8f;
        private const float BoilingWaterLoopPitchMaximum = 1.2f;
        private const float BoilingWaterLoopPitchUpdateIntervalSeconds = 0.12f;
        private const int BoilingWaterSamplePoolCapacity = 8;
        private const int ImpactEventQueueCapacity = 64;
        private const int ImpactEventQueueMask = ImpactEventQueueCapacity - 1;
        private const int ImpactEventQueueEnqueueAttemptLimit = 8;
        private const int ColdBurstClearMinimumCount = 1024;
        private const float PhysicsImpactStressRadiusMeters = 18f;
        private const float PhysicsImpactStressDecayPerSecond = 1.65f;
        private const float PhysicsImpactMetallicDecayPerSecond = 2.4f;
        private const float PhysicsImpactStressBoost = 0.55f;
        private const float PhysicsImpactMinimumAudibleMassVelocity = 5f;
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
        private const float HeartbeatDuckMaximum = 0.46f;
        private const float HeartbeatDuckAttackSharpness = 180f;
        private const float HeartbeatDuckReleaseSharpness = 14f;
        private const float TinnitusOxygenThreshold = 0.10f;
        private const float TinnitusCarrierHertz = 8000f;
        private const float TinnitusMaximumGain = 0.045f;
        private const float TinnitusLowPassCutoffHertz = 720f;
        private const float NitrogenWarningTinnitusGainScale = 0.58f;
        private const float TinnitusPlayerStressExponentialSharpness = 3.4f;
        private const float TinnitusPlayerStressMaximumScale = 2.35f;
        private const float CriticalSidechainAttackSeconds = 0.05f;
        private const float CriticalSidechainReleaseSeconds = 0.3f;
        private const float CriticalSidechainThreshold = 0.08f;
        private const float CriticalSidechainKneeWidth = 0.72f;
        private const float CriticalSidechainDuckedGain = 0.25118864f;
        private const float LeviathanRoarAggroDecayPerSecond = 0.42f;
        private const float LeviathanRoarMaximumGain = 0.16f;
        private const float LeviathanRoarMinimumGrainSeconds = 0.038f;
        private const float LeviathanRoarMaximumGrainSeconds = 0.16f;
        private const float LeviathanDopplerMinimumPitchScale = 0.55f;
        private const float LeviathanDopplerMaximumPitchScale = 1.8f;
        private const float LeviathanDopplerVelocityClampMetersPerSecond = SoundSpeedWaterMetersPerSecond * 0.9f;
        private const float LeviathanDopplerVelocityJumpThresholdMetersPerSecond = 10f;
        private const float LeviathanDopplerSmoothingSamples = 128f;
        private const float LeviathanDopplerSmoothingReferenceSampleRate = 48000f;
        private const float VehicleCavitationScreechStartMetersPerSecond = 20f;
        private const float VehicleCavitationScreechFullMetersPerSecond = 32f;
        private const float VehicleCavitationScreechGain = 0.075f;
        private const float VehicleCavitationHighPassAlpha = 0.92f;
        private const float PressureScrubberHumFrequencyHertz = 40f;
        private const float PressureScrubberHumMaximumGain = 0.045f;
        private const float PressureScrubberHumDepthUpdateDeltaMeters = 10f;
        private const byte SonarAudioMaterialIdDefault = 0;
        private const byte SonarAudioMaterialIdMetal = 1;
        private const byte SonarAudioMaterialIdRock = 2;
        private const byte SonarAudioMaterialIdGlass = 3;
        private const float StructuralSnapMinimumHertz = 4200f;
        private const float StructuralSnapMaximumHertz = 9600f;
        private const float StructuralSnapDecayPerSecond = 14f;
        private const float StructuralSnapMaximumGain = 0.16f;
        private const float StructuralSnapPitchMinimum = 0.8f;
        private const float StructuralSnapPitchMaximum = 1.2f;
        // Rescue path: route procedural output through the listener filter until the native mixer effect is proven healthy.
        private const bool EnableNativeMixerKernel = false;
        private const float DspProducerSolveBudgetMilliseconds = 0.1f;
        private const double DspProducerSolveBudgetSeconds = 0.0001d;
        private const int DspProducerTelemetryCooldownFrames = 60;
        private static readonly long DspProducerSolveBudgetTicks =
            Math.Max(1L, (long)(System.Diagnostics.Stopwatch.Frequency * DspProducerSolveBudgetSeconds));
        private static readonly uint _dspProducerOverBudgetWarningHash =
            unchecked((uint)Hecton.Localization.LocHash.Compute("Audio.DspProducerOverBudget"));
        private static readonly uint _dspProducerContextHash =
            unchecked((uint)Hecton.Localization.LocHash.Compute("PlayerCriticalProceduralAudioRenderer.DspProducer"));

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

        [Tooltip("Authored 2D loop used for pressure-driven hull groans. Pitch and gain are stress-modulated on the main thread.")]
        [SerializeField] private AudioSource hullGroanLoopSource;

        [Tooltip("Optional authored hull-groan loop clip assigned to the hull groan source at runtime.")]
        [SerializeField] private AudioClip hullGroanLoopClip;

        [Tooltip("Maximum gain applied to the hull-groan loop at full hull stress.")]
        [SerializeField, Range(0f, 1f)] private float hullGroanLoopMaximumVolume = 0.42f;

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
        [Tooltip("Layer mask retained for acoustic occlusion and trigger-preset compatibility.")]
        [FormerlySerializedAs("ceilingProbeLayers")]
        [FormerlySerializedAs("enclosureProbeLayers")]
        [SerializeField] private LayerMask acousticOcclusionLayers = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;

        [Tooltip("Legacy preset distance used only when open-water fallback needs a stable scale.")]
        [FormerlySerializedAs("ceilingProbeDistance")]
        [SerializeField, Range(5f, 80f)] private float openWaterPresetDistance = 48f;

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

        [Header("Tool Boil Loop")]
        [Tooltip("Looping boiling-water source used for cutter boil without procedural bubble synthesis.")]
        [SerializeField] private AudioSource boilingWaterLoopSource;

        [Tooltip("Optional boiling-water loop clip assigned to the loop source at runtime.")]
        [SerializeField] private AudioClip boilingWaterLoopClip;

        [Tooltip("Maximum gain applied to the boiling-water loop at full cutter heat.")]
        [SerializeField, Range(0f, 1f)] private float boilingWaterLoopMaximumVolume = 0.32f;

        [Tooltip("Optional eight-source boiling sample pool for cutter cavitation. Sources are 2D stereo-panned and heat-modulated.")]
        [SerializeField] private AudioSource[] boilingWaterPoolSources;

        [Tooltip("Optional boiling sample clips assigned to the pool sources in order.")]
        [SerializeField] private AudioClip[] boilingWaterPoolClips;

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
        // COLD ALLOC: NativeArray<float>[131072] - sonar linear echo delay ring - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<float> _sonarEchoDelay;
        // COLD ALLOC: NativeArray<SonarEchoTap>[12] - pending sonar echo tap buffer A - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<SonarEchoTap> _pendingSonarEchoTapsA;
        // COLD ALLOC: NativeArray<SonarEchoTap>[12] - pending sonar echo tap buffer B - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<SonarEchoTap> _pendingSonarEchoTapsB;
        // COLD ALLOC: NativeArray<SonarEchoTap>[12] - worker-owned sonar tap snapshot prevents main-thread tap tearing - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<SonarEchoTap> _workerSonarEchoTaps;
        // COLD ALLOC: NativeArray<float>[12] - sonar echo read cursors per tap - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<float> _sonarEchoReadCursors;
        // COLD ALLOC: NativeArray<float>[12] - sonar echo low-pass x1 state per tap - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<float> _sonarEchoFilterInput1;
        // COLD ALLOC: NativeArray<float>[12] - sonar echo low-pass x2 state per tap - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<float> _sonarEchoFilterInput2;
        // COLD ALLOC: NativeArray<float>[12] - sonar echo low-pass y1 state per tap - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<float> _sonarEchoFilterOutput1;
        // COLD ALLOC: NativeArray<float>[12] - sonar echo low-pass y2 state per tap - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<float> _sonarEchoFilterOutput2;
        // COLD ALLOC: NativeArray<SonarEchoCompositeGroup>[32] - active-sonar echo candidate buffer A before AUP hash coalescing - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<SonarEchoCompositeGroup> _sonarEchoCompositeCandidatesA;
        // COLD ALLOC: NativeArray<SonarEchoCompositeGroup>[32] - active-sonar echo candidate buffer B before AUP hash coalescing - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<SonarEchoCompositeGroup> _sonarEchoCompositeCandidatesB;
        // COLD ALLOC: NativeArray<SonarEchoCompositeGroup>[8] - Burst-coalesced active-sonar echo groups by 10m AUP hash - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<SonarEchoCompositeGroup> _sonarEchoCompositeGroups;
        // COLD ALLOC: NativeArray<int>[1] - sonar echo coalesced group count returned by Burst hash job - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<int> _sonarEchoCompositeGroupCountNative;
        // COLD ALLOC: NativeParallelMultiHashMap<int,int>[32] - sonar echo AUP cell occupancy before DSP tap publish - owner: PlayerCriticalProceduralAudioRenderer
        private NativeParallelMultiHashMap<int, int> _sonarEchoCompositeSpatialHash;
        // COLD ALLOC: NativeParallelHashMap<int,int>[8] - sonar echo hash-to-output group lookup for coalescing - owner: PlayerCriticalProceduralAudioRenderer
        private NativeParallelHashMap<int, int> _sonarEchoCompositeGroupByHash;
        // COLD ALLOC: NativeArray<float>[1024] - Karplus-Strong delay line for metallic impact synthesis - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<float> _impactClangDelay;
        // COLD ALLOC: NativeArray<float>[4096] - thruster comb filter delay ring - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<float> _thrusterCombDelay;
        // COLD ALLOC: NativeArray<float>[262144] - 1,048,576 bytes fixed four-comb Sabine reverb delay field - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<float> _sabineReverbDelay;
        // COLD ALLOC: NativeArray<float>[8192] - dry BaseModule feedback delay network cache - owner: PlayerCriticalProceduralAudioRenderer
        private NativeArray<float> _interiorFdnDelay;
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
        private bool _runtimeRegistered;
        private bool _registered;
        private bool _slowTickRegistered;
        private bool _lateFrameRegistered;
        private GameObject _boundPlayerObject;
        private Transform _boundPlayerTransform;
        private int _boundPlayerRootEntityId;
        private Rigidbody _playerRigidbody;
        private HectonSurvivalSystem _playerSurvivalSystem;
        private HectonPlayerHealth _playerHealth;
        private ISubmarineHullBreachReadModel _structuralHullReadModel;
        private IPlayerTransportLifecycleOwner _activeTransportLifecycleOwner;
        private AudioReverbFilter _listenerReverbFilter;
        private bool _reverbMixerBindingsResolved;
        private bool _reverbMixerBindingsValid;
        private bool _reverbMixerWetBindingValid;
        private bool _warnedMissingReverbMixerParameters;
        private bool _warnedMissingReverbWetMixerParameter;
        private bool _warnedMissingListenerReverbFilter;
        private string _resolvedReverbDecayTimeParameter;
        private string _resolvedReverbReflectionsLevelParameter;
        private string _resolvedReverbRoomHighFrequencyParameter;
        private string _resolvedReverbWetMixParameter;
        private PlayerTransportFeelContract _transportFeelContractCurrent;
        private float _lastSpeed;
        private float _vehicleCavitationSpeedTickValue;
        private float _lastLeviathanRoarDistanceMeters;
        private float _lastLeviathanRoarSampleTime;
        private float _lastLeviathanRoarRelativeVelocityMetersPerSecond;
        private float _pendingLeviathanRoarDistanceMeters;
        private bool _hasLeviathanRoarDopplerSample;
        private bool _hasPendingLeviathanRoarDistance;
        private float _hullStressTickValue;
        private float _structuralHullStressTickValue;
        private float _structuralHullStressVelocityTickValue;
        private float _impactStressImpulseTickValue;
        private float _hullPressureDepthTickValue;
        private float _absoluteDepthTickValue;
        private float _pressureScrubberHumLastDepthMeters = float.MinValue;
        private float _thrusterBlendTickValue;
        private float _thrusterLoadTickValue;
        private float _thrusterRpmTickValue;
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
        private float _audioImpactStressValue;
        private float _audioImpactMetallicValue;
        private float _audioThrusterBlendValue;
        private float _audioThrusterLoadValue;
        private float _audioThrusterRpmValue;
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
        private float _audioTinnitusOxygenStressValue;
        private float _audioLeviathanRoarAggroValue;
        private float _audioLeviathanRoarPitchScale = 1f;
        private float _audioVehicleCavitationSpeed01;
        private float _audioBubbleBoilIntensity;
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
        private int _impactEventQueueDropCount;
        private int _sonarEchoCompositeFrame = -1;
        private int _sonarEchoCompositeCandidateCountA;
        private int _sonarEchoCompositeCandidateCountB;
        private int _sonarEchoCompositeWriteBufferIndex;
        private int _sonarEchoCompositeScheduledBufferIndex = -1;
        private int _sonarEchoCompositeScheduledCandidateCount;
        private JobHandle _sonarEchoCompositeHashHandle;
        private bool _sonarEchoCompositeHashJobScheduled;
        private int _workerConsumedSonarSequence;
        private int _workerConsumedSonarRevision;
        private int _workerActiveSonarTapCount;
        private int _pendingSonarEchoTapCountA;
        private int _pendingSonarEchoTapCountB;
        private SonarTriggerState _pendingSonarStateA;
        private SonarTriggerState _pendingSonarStateB;
        private AudioParameterSnapshotSlot _audioParameterSnapshotA;
        private AudioParameterSnapshotSlot _audioParameterSnapshotB;
        private SonarTriggerState _workerActiveSonarState;
        private HullSynthesisState _hullSynthesisState;
        private SonarSynthesisState _sonarSynthesisState;
        private AmbientCurrentSynthesisState _ambientCurrentSynthesisState;
        private ImpactEchoSynthesisState _impactEchoSynthesisState;
        private HeartbeatSynthesisState _heartbeatSynthesisState;
        private ThrusterSynthesisState _thrusterSynthesisState;
        private SabineReverbSynthesisState _sabineReverbSynthesisState;
        private InteriorFdnReverbSynthesisState _interiorFdnReverbSynthesisState;
        private TinnitusSynthesisState _tinnitusSynthesisState;
        private LeviathanGranularSynthesisState _leviathanGranularSynthesisState;
        private CriticalSidechainCompressorState _criticalSidechainCompressorState;
        private long _producedSampleCount;
        private bool _nativeOutputRegistered;
        private bool _nativeOutputBridgeFailureLogged;
        private int _managedFilterFallbackEnabled;
        private int _binauralDelayWriteIndex;
        private ulong _playerBodyEntityId;
        private int _dspProducerOverBudgetPending;
        private long _dspProducerLastOverBudgetTicks;
        private int _dspProducerTelemetryCooldownFrames;

        private volatile float _targetHullStressValue;
        private volatile float _targetStructuralHullStressValue;
        private volatile float _targetStructuralHullStressVelocityValue;
        private volatile float _targetStructuralFatigueValue;
        private volatile float _targetHullPressureDepthValue;
        private volatile float _targetAbsoluteDepthMeters;
        private volatile float _targetEnclosureDensityIndex;
        private volatile float _targetPressureScrubberHumDrive;
        private volatile float _targetPressureScrubberHumGain;
        private volatile float _targetReverbRt60Seconds;
        private volatile float _targetReverbWetMix;
        private volatile float _targetReverbOpenness;
        private volatile float _targetBubbleBoilIntensity;
        private volatile float _targetThrusterBlendValue;
        private volatile float _targetThrusterLoadValue;
        private volatile float _targetThrusterRpmValue;
        private volatile float _targetThrusterPitchValue = 1f;
        private volatile float _targetThrusterPressureValue;
        private volatile float _targetThrusterAccelerationValue;
        private volatile float _targetThrusterHeavyCarryValue;
        private volatile float _targetThrusterDiveValue;
        private volatile float _targetAbyssalLowPassMix;
        private volatile float _targetHeartbeatStressValue;
        private volatile float _targetHeartbeatOxygenDangerValue;
        private volatile int _targetHeartbeatActive;
        private volatile float _targetTinnitusOxygenStressValue;
        private volatile float _targetLeviathanRoarAggroValue;
        private volatile float _targetLeviathanRoarPitchScale = 1f;
        private volatile float _targetVehicleCavitationSpeed01;
        private volatile float _targetStructuralSnapValue;
        private volatile float _targetBinauralAzimuthRadians;
        private volatile float _targetBinauralRightDot;
        private volatile float _targetBinauralItdSeconds;
        private volatile float _targetBinauralShadowAmount01;
        private volatile float _targetBinauralShadowCutoffHertz;
        private volatile float _targetBinauralEnergy01;
        private volatile float _targetBinauralWaterDensityMul;
        private volatile int _targetBinauralValid;
        private PendingImpactEchoProbe _pendingImpactEchoProbe;
        private bool _laserCutterBeamActive;
        private float _laserCutterHeat01;
        private float _hullGroanLoopNextPitchUpdateTime;
        private float _hullGroanLoopPitch = 1f;
        private uint _hullGroanLoopPitchSeed = 0x53A9D71Fu;
        private float _boilingLoopNextPitchUpdateTime;
        private float _boilingLoopPitch = 1f;
        private uint _boilingLoopPitchSeed = 0xA31C59B3u;

        // COLD ALLOC: ImpactAudioEvent[64] - main-thread physics impact bridge for the audio worker SPSC path - owner: PlayerCriticalProceduralAudioRenderer
        private readonly ImpactAudioEvent[] _impactEventQueue = new ImpactAudioEvent[ImpactEventQueueCapacity];
        private struct SonarEchoTap
        {
            public float DelaySeconds;
            public float PreviousDopplerRatio;
            public float DopplerRatio;
            public float Attenuation;
            public float LeftPanDeltaGain;
            public float RightPanDeltaGain;
            public float LowPassCutoffHz;
            public float LowPassB0;
            public float LowPassB1;
            public float LowPassB2;
            public float LowPassA1;
            public float LowPassA2;
            public int DelaySamples;
            public int UseLowPass;
        }

        private struct SonarEchoCompositeGroup
        {
            public AbsoluteUniversePosition Position;
            public float DistanceMeters;
            public float ReturnStrength;
            public float Resonance;
            public int HitCount;
            public byte AudioMaterialId;

            public SonarEchoCompositeGroup(
                AbsoluteUniversePosition position,
                float distanceMeters,
                float returnStrength,
                float resonance,
                int hitCount,
                byte audioMaterialId)
            {
                Position = position;
                DistanceMeters = distanceMeters;
                ReturnStrength = returnStrength;
                Resonance = resonance;
                HitCount = hitCount;
                AudioMaterialId = audioMaterialId;
            }
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
            public int ImpactEventQueueDropCount;
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
            public float PressureScrubberHumDrive;
            public float PressureScrubberHumGain;
            public float ReverbRt60Seconds;
            public float ReverbWetMix;
            public float ReverbOpenness;
            public float BubbleBoilIntensity;
            public float ThrusterBlend;
            public float ThrusterLoad;
            public float ThrusterRpm;
            public float ThrusterPitch;
            public float ThrusterPressure;
            public float ThrusterAcceleration;
            public float ThrusterHeavyCarry;
            public float ThrusterDive;
            public float VehicleCavitationSpeed01;
            public float AbyssalLowPassMix;
            public float HeartbeatStress;
            public float HeartbeatOxygenDanger;
            public float TinnitusOxygenStress;
            public float LeviathanRoarAggro;
            public float LeviathanRoarPitchScale;
            public int HeartbeatActive;
            public float BinauralAzimuthRadians;
            public float BinauralRightDot;
            public float BinauralItdSeconds;
            public float BinauralShadowAmount01;
            public float BinauralShadowCutoffHertz;
            public float BinauralEnergy01;
            public float BinauralWaterDensityMul;
            public int BinauralValid;
        }

        [StructLayout(LayoutKind.Explicit, Size = 256)]
        private struct AudioParameterSnapshotSlot
        {
            [FieldOffset(0)]
            public AudioParameterSnapshot Value;
            [FieldOffset(192)]
            private AudioParameterSnapshotCacheLinePad Padding;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct AudioParameterSnapshotCacheLinePad
        {
            [FieldOffset(0)] private long _frontFence;
            [FieldOffset(56)] private long _rearFence;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct SonarEchoSpatialHashCoalesceJob : IJob
        {
            [ReadOnly] public NativeArray<SonarEchoCompositeGroup> Candidates;
            public NativeParallelMultiHashMap<int, int> SpatialHash;
            public NativeParallelHashMap<int, int> GroupByHash;
            public NativeArray<SonarEchoCompositeGroup> Groups;
            public NativeArray<int> GroupCount;
            public int CandidateCount;

            public void Execute()
            {
                int safeCandidateCount = math.clamp(CandidateCount, 0, Candidates.Length);
                GroupCount[0] = 0;

                for (int candidateIndex = 0; candidateIndex < safeCandidateCount; candidateIndex++)
                {
                    SonarEchoCompositeGroup candidate = Candidates[candidateIndex];
                    if (candidate.HitCount <= 0)
                        continue;

                    int hash = ResolveSonarEchoCompositeHash(in candidate.Position, candidate.AudioMaterialId);
                    SpatialHash.Add(hash, candidateIndex);

                    if (GroupByHash.TryGetValue(hash, out int groupIndex))
                    {
                        SonarEchoCompositeGroup group = Groups[groupIndex];
                        group.ReturnStrength += candidate.ReturnStrength;
                        group.Resonance += candidate.Resonance;
                        group.DistanceMeters += candidate.DistanceMeters;
                        group.HitCount += candidate.HitCount;
                        Groups[groupIndex] = group;
                        continue;
                    }

                    int writeIndex = GroupCount[0];
                    if (writeIndex >= Groups.Length)
                        continue;

                    Groups[writeIndex] = candidate;
                    GroupByHash.TryAdd(hash, writeIndex);
                    GroupCount[0] = writeIndex + 1;
                }
            }
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
            public double PressureScrubberHumPhase;
            public double PressureScrubberHarmonicPhase;
            public double PressureScrubberSaturationPhase;
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
            public int EchoWriteIndex;
        }
#pragma warning restore 0649

        private struct AmbientCurrentSynthesisState
        {
            public double CarrierPhase;
            public double ModulatorPhase;
            public double SlowPhase;
            public double NoisePhase;
            public double PressurePhaserPhase;
            public float LowPassState;
            public float BandPassState;
            public float PressurePhaserFeedbackSample;
            public float PressurePhaserAllPassA;
            public float PressurePhaserAllPassB;
            public float PressurePhaserAllPassC;
            public float PressurePhaserAllPassD;
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
        }

        private struct CriticalSidechainCompressorState
        {
            public float Envelope;
            public float Gain;
        }

        private struct TinnitusSynthesisState
        {
            public double Phase;
        }

        private struct LeviathanGranularSynthesisState
        {
            public float Envelope;
            public float GrainAgeSeconds;
            public float GrainDurationSeconds;
            public float GrainPitchRatio;
            public float GrainStartIndex;
            public float LowPassState;
            public uint Seed;
        }

        private struct InteriorFdnReverbSynthesisState
        {
            public int WriteA;
            public int WriteB;
            public int WriteC;
            public int WriteD;
            public float DampingA;
            public float DampingB;
            public float DampingC;
            public float DampingD;
        }

        private struct PendingImpactEchoProbe
        {
            public bool Valid;
            public float Excitation;
            public float ExpireAt;
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
            public double VehicleCavitationScreechPhase;
            public float VehicleCavitationHighPassInput;
            public float VehicleCavitationHighPassOutput;
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
            public float WetMix;
        }

        /// <summary>
        /// True while the player-owned procedural critical-audio renderer is active.
        /// </summary>
        public static bool IsRuntimeInstalled => GlobalRegistry.PlayerCriticalAudio != null;

        private void Awake()
        {
            RebuildAcousticOcclusionLayerMask();
            ResetReverbModelState();
            RefreshAudioConfiguration();
            TryBindFromBootstrap();
        }

        private void OnEnable()
        {
            if (!TryRegisterRuntimeService())
                return;

            AcousticOcclusionUtility.AcquireRuntime();
            AudioSettings.OnAudioConfigurationChanged += HandleAudioConfigurationChanged;
            PhysicsEvents.Register(this);
            PhysicsEventBus.Register(this);
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
            PhysicsEventBus.Unregister(this);
            PhysicsEvents.Unregister(this);
            AudioSettings.OnAudioConfigurationChanged -= HandleAudioConfigurationChanged;
            UnsubscribeTransportCoordinator();
            TryUnregister();
            TryUnregisterRuntimeService();
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
            PhysicsEventBus.Unregister(this);
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

            TryUnregisterRuntimeService();
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
                _playerHealth = null;
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

            if (_playerHealth == null || !ReferenceEquals(_playerHealth.gameObject, playerObject))
                playerObject.TryGetComponent(out _playerHealth);

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

        private float ResolveBoundPlayerDistanceMeters(Vector3 runtimeWorldPosition)
        {
            return TryResolveBoundPlayerAupDistance(
                runtimeWorldPosition,
                out _,
                out _,
                out float distanceMeters)
                ? distanceMeters
                : float.PositiveInfinity;
        }

        private bool TryResolveBoundPlayerDistanceWithin(
            Vector3 runtimeWorldPosition,
            float maxDistanceMeters,
            out float distanceMeters)
        {
            return TryResolveBoundPlayerAupDistanceWithin(
                runtimeWorldPosition,
                maxDistanceMeters,
                out _,
                out _,
                out distanceMeters);
        }

        private bool TryResolveBoundPlayerDistanceWithin(
            in AbsoluteUniversePosition targetAup,
            float maxDistanceMeters,
            out float distanceMeters)
        {
            return TryResolveBoundPlayerAupDistanceWithin(
                in targetAup,
                maxDistanceMeters,
                out _,
                out _,
                out distanceMeters);
        }

        private bool TryResolveBoundPlayerAupDistance(
            Vector3 runtimeWorldPosition,
            out AbsoluteUniversePosition playerAup,
            out AbsoluteUniversePosition targetAup,
            out float distanceMeters)
        {
            playerAup = default;
            targetAup = default;
            distanceMeters = float.PositiveInfinity;
            if (playerMovement == null)
                return false;

            playerAup = playerMovement.CurrentAup;
            targetAup = AbsoluteUniversePosition.FromRuntimePosition(runtimeWorldPosition);
            double distanceSq = AbsoluteUniversePosition.DistanceSq(in playerAup, in targetAup);
            distanceMeters = ApproximateDistanceMetersFromSq(distanceSq);
            return math.isfinite(distanceMeters);
        }

        private bool TryResolveBoundPlayerAupDistanceWithin(
            Vector3 runtimeWorldPosition,
            float maxDistanceMeters,
            out AbsoluteUniversePosition playerAup,
            out AbsoluteUniversePosition targetAup,
            out float distanceMeters)
        {
            playerAup = default;
            targetAup = default;
            distanceMeters = float.PositiveInfinity;
            if (playerMovement == null)
                return false;

            playerAup = playerMovement.CurrentAup;
            targetAup = AbsoluteUniversePosition.FromRuntimePosition(runtimeWorldPosition);
            double distanceSq = AbsoluteUniversePosition.DistanceSq(in playerAup, in targetAup);
            double maxDistance = math.max(0f, maxDistanceMeters);
            if (distanceSq > maxDistance * maxDistance)
                return false;

            distanceMeters = ApproximateDistanceMetersFromSq(distanceSq);
            return math.isfinite(distanceMeters);
        }

        private bool TryResolveBoundPlayerAupDistanceWithin(
            in AbsoluteUniversePosition targetAup,
            float maxDistanceMeters,
            out AbsoluteUniversePosition playerAup,
            out AbsoluteUniversePosition resolvedTargetAup,
            out float distanceMeters)
        {
            playerAup = default;
            resolvedTargetAup = targetAup;
            distanceMeters = float.PositiveInfinity;
            if (playerMovement == null)
                return false;

            playerAup = playerMovement.CurrentAup;
            double distanceSq = AbsoluteUniversePosition.DistanceSq(in playerAup, in targetAup);
            double maxDistance = math.max(0f, maxDistanceMeters);
            if (distanceSq > maxDistance * maxDistance)
                return false;

            distanceMeters = ApproximateDistanceMetersFromSq(distanceSq);
            return math.isfinite(distanceMeters);
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
                _targetPressureScrubberHumDrive = 0f;
                _targetPressureScrubberHumGain = 0f;
                _targetReverbRt60Seconds = 0f;
                _targetReverbWetMix = 0f;
                _targetReverbOpenness = 1f;
                _targetBubbleBoilIntensity = 0f;
                _targetThrusterBlendValue = 0f;
                _targetThrusterLoadValue = 0f;
                _targetThrusterRpmValue = 0f;
                _targetThrusterPitchValue = 1f;
                _targetThrusterPressureValue = 0f;
                _targetThrusterAccelerationValue = 0f;
                _targetThrusterHeavyCarryValue = 0f;
                _targetThrusterDiveValue = 0f;
                _targetVehicleCavitationSpeed01 = 0f;
                _targetAbyssalLowPassMix = 0f;
                _targetHeartbeatStressValue = 0f;
                _targetHeartbeatOxygenDangerValue = 0f;
                _targetHeartbeatActive = 0;
                _targetTinnitusOxygenStressValue = 0f;
                _targetLeviathanRoarAggroValue = 0f;
                _targetLeviathanRoarPitchScale = 1f;
                _targetStructuralSnapValue = 0f;
                _targetBinauralAzimuthRadians = 0f;
                _targetBinauralRightDot = 0f;
                _targetBinauralItdSeconds = 0f;
                _targetBinauralShadowAmount01 = 0f;
                _targetBinauralShadowCutoffHertz = 22000f;
                _targetBinauralEnergy01 = 0f;
                _targetBinauralWaterDensityMul = 0f;
                _targetBinauralValid = 0;
                _pendingImpactEchoProbe = default;
                UpdateHullGroanLoop(false, 0f);
                UpdateBoilingWaterLoop(false, 0f);
                _lastSpeed = 0f;
                _vehicleCavitationSpeedTickValue = 0f;
                _hasLeviathanRoarDopplerSample = false;
                _hasPendingLeviathanRoarDistance = false;
                _impactStressImpulseTickValue = 0f;
                _hullPressureDepthTickValue = 0f;
                _absoluteDepthTickValue = 0f;
                _pressureScrubberHumLastDepthMeters = float.MinValue;
                _heartbeatStressTickValue = 0f;
                _heartbeatOxygenDangerTickValue = 0f;
                _structuralSnapTickValue = 0f;
                PublishAudioParameterSnapshot();
                return;
            }

            float impactStress = _impactStressImpulseTickValue;
            _impactStressImpulseTickValue = math.max(0f, impactStress - deltaTime * PhysicsImpactStressDecayPerSecond);
            _targetLeviathanRoarAggroValue = math.max(
                0f,
                _targetLeviathanRoarAggroValue - (deltaTime * LeviathanRoarAggroDecayPerSecond));
            float hullBlendT = ApproximateOneMinusExpNegPositive(math.max(hullStressFollowSharpness, 0.01f) * deltaTime);
            _hullStressTickValue = math.lerp(
                _hullStressTickValue,
                math.saturate(math.max(playerMovement.CurrentHullStress01, impactStress)),
                hullBlendT);
            _targetHullStressValue = _hullStressTickValue;
            float structuralStressTarget = ResolveStructuralHullStress01();
            float structuralBlendT = ApproximateOneMinusExpNegPositive(StructuralStressFollowSharpness * deltaTime);
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
            UpdateHullGroanLoop(true, math.saturate(math.max(_targetHullStressValue, _targetStructuralHullStressValue)));
            _hullPressureDepthTickValue = ResolveHullPressureDepth01(playerMovement.CurrentDepth);
            _targetHullPressureDepthValue = _hullPressureDepthTickValue;
            _absoluteDepthTickValue = ResolveAbsoluteDepthMeters();
            _targetAbsoluteDepthMeters = _absoluteDepthTickValue;
            UpdatePressureScrubberHumCache(
                _absoluteDepthTickValue,
                _hullPressureDepthTickValue,
                _targetEnclosureDensityIndex);
            _targetAbyssalLowPassMix = ResolveAbyssalLowPassTarget(playerMovement.CurrentDepth);
            UpdateBubbleBoilTargets();
            UpdateSurvivalTargets(deltaTime);

            UpdateThrusterTargets(deltaTime);
            UpdateBinauralTargets();
            UpdateAcousticThreatPulse();
            TryResolvePendingImpactEchoProbe();
            PublishAudioParameterSnapshot();
        }

        public void SlowTick()
        {
            TryBindFromBootstrap();

            if (_boundPlayerTransform == null || playerMovement == null || !playerMovement.IsPlayerSubmerged)
            {
                ResetReverbModelState();
                _hasPendingLeviathanRoarDistance = false;
                _targetLeviathanRoarPitchScale = 1f;
                return;
            }

            UpdateLeviathanDopplerCache();
        }

        public void LateFrameTick()
        {
            FlushSonarEchoCompositeGroups(allowJobCompletion: true);
            PublishPendingDspProducerOverBudgetWarning();
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
            diagnostics.ImpactEventQueueDropCount = Volatile.Read(ref _impactEventQueueDropCount);
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

            long solveStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            long blockStartFrame = Interlocked.Read(ref _producedSampleCount);
            TryConsumePendingSonarTrigger(blockStartFrame, frameCount);
            int parameterReadIndex = Volatile.Read(ref _audioParameterSnapshotReadIndex);
            AudioParameterSnapshot parameters = parameterReadIndex == 0
                ? _audioParameterSnapshotA.Value
                : _audioParameterSnapshotB.Value;

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
            float pressureHumDriveTarget = math.saturate(parameters.PressureScrubberHumDrive);
            float pressureHumGainTarget = math.saturate(parameters.PressureScrubberHumGain);
            float bubbleBoilTarget = math.saturate(parameters.BubbleBoilIntensity);
            float thrusterBlendTarget = math.saturate(parameters.ThrusterBlend);
            float thrusterLoadTarget = math.saturate(parameters.ThrusterLoad);
            float thrusterRpmTarget = math.saturate(parameters.ThrusterRpm);
            float thrusterPitchTarget = math.max(0.1f, parameters.ThrusterPitch);
            float thrusterPressureTarget = math.saturate(parameters.ThrusterPressure);
            float thrusterAccelerationTarget = math.saturate(parameters.ThrusterAcceleration);
            float thrusterHeavyCarryTarget = math.saturate(parameters.ThrusterHeavyCarry);
            float thrusterDiveTarget = math.saturate(parameters.ThrusterDive);
            float vehicleCavitationSpeedTarget = math.saturate(parameters.VehicleCavitationSpeed01);
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
                pressureHumDriveTarget,
                pressureHumGainTarget,
                impactMetallicTarget);
            RenderSonarBlock(frameCount, blockStartFrame, invSampleRate);
            RenderImpactEchoBlock(frameCount, invSampleRate);
            RenderThrusterBlock(
                frameCount,
                blockStartFrame,
                invSampleRate,
                thrusterBlendTarget,
                thrusterLoadTarget,
                thrusterRpmTarget,
                thrusterPitchTarget,
                thrusterPressureTarget,
                thrusterAccelerationTarget,
                thrusterHeavyCarryTarget,
                thrusterDiveTarget,
                vehicleCavitationSpeedTarget);
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

            ReportDspProducerSolveTicks(System.Diagnostics.Stopwatch.GetTimestamp() - solveStartTicks);
        }

        private void ReportDspProducerSolveTicks(long elapsedTicks)
        {
            if (elapsedTicks <= DspProducerSolveBudgetTicks)
                return;

            Interlocked.Exchange(ref _dspProducerLastOverBudgetTicks, elapsedTicks);
            Interlocked.Exchange(ref _dspProducerOverBudgetPending, 1);
        }

        private void PublishPendingDspProducerOverBudgetWarning()
        {
            if (_dspProducerTelemetryCooldownFrames > 0)
            {
                _dspProducerTelemetryCooldownFrames--;
                return;
            }

            if (Interlocked.Exchange(ref _dspProducerOverBudgetPending, 0) == 0)
                return;

            long elapsedTicks = Interlocked.Read(ref _dspProducerLastOverBudgetTicks);
            float elapsedMilliseconds = (float)((elapsedTicks * 1000d) / System.Diagnostics.Stopwatch.Frequency);
            GlobalTelemetryBus.PublishPerformanceWarning(
                _dspProducerOverBudgetWarningHash,
                _dspProducerContextHash,
                math.max(elapsedMilliseconds, DspProducerSolveBudgetMilliseconds));
            _dspProducerTelemetryCooldownFrames = DspProducerTelemetryCooldownFrames;
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
            NativeArray<SonarEchoTap> sourceTapBuffer = activeIndex == 0 ? _pendingSonarEchoTapsA : _pendingSonarEchoTapsB;
            int sourceTapCount = activeIndex == 0 ? _pendingSonarEchoTapCountA : _pendingSonarEchoTapCountB;
            int safeTapCount = math.clamp(sourceTapCount, 0, SonarEchoTapCapacity);
            if (_workerSonarEchoTaps.IsCreated && sourceTapBuffer.IsCreated)
            {
                safeTapCount = math.min(safeTapCount, _workerSonarEchoTaps.Length);
                for (int tapIndex = 0; tapIndex < safeTapCount; tapIndex++)
                    _workerSonarEchoTaps[tapIndex] = sourceTapBuffer[tapIndex];
            }
            else
            {
                safeTapCount = 0;
            }

            _workerActiveSonarTapCount = safeTapCount;
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

            float transportBoost = ResolveTransportBoost01();
            _transportFeelContractCurrent = isSwimMode || transportBoost > 0.0001f
                ? ResolveTransportFeelContract()
                : null;
            float heavyCarry = isSwimMode && playerMovement.IsDraggingHeavyCargo
                ? playerMovement.HeavyCarryLoad
                : 0f;
            float diveAttack = isSwimMode ? ResolveDiveAttack01() : 0f;
            float depth = math.max(0f, playerMovement.CurrentDepth);

            Vector3 velocity = _playerRigidbody.linearVelocity;
            float speed = ApproximateMagnitudeNoSqrt((float3)velocity);
            float vehicleMotorSpeed = speed;
            if (VehicleMotor.TryResolveForBody(_playerRigidbody, out VehicleMotor vehicleMotor))
                vehicleMotorSpeed = math.max(vehicleMotorSpeed, ApproximateMagnitudeNoSqrt((float3)vehicleMotor.LinearVelocity));

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
            float rpmTarget = math.saturate(transportBoost * 0.72f + throttleAttack * 0.18f + diveAttack * 0.1f);
            float pitchTarget = math.max(0.1f, pitchMultiplier * (1f - heavyCarry * heavyCarryPitchDrag) * math.lerp(0.94f, 1.18f, rpmTarget));
            float pressureTarget = math.saturate(pressureAmount * shallowPressure);
            float heavyCarryTarget = math.saturate(heavyCarry * (1f + heavyCarryVolumeBoost));

            float blendT = ApproximateOneMinusExpNegPositive(math.max(thrusterFollowSharpness, 0.01f) * deltaTime);
            float rpmBlendT = math.saturate(2.0f * math.max(0f, deltaTime));
            _thrusterBlendTickValue = math.lerp(_thrusterBlendTickValue, targetBlend, blendT);
            _thrusterLoadTickValue = math.lerp(_thrusterLoadTickValue, loadTarget, blendT);
            _thrusterRpmTickValue = math.lerp(_thrusterRpmTickValue, rpmTarget, rpmBlendT);
            _thrusterPitchTickValue = math.lerp(_thrusterPitchTickValue, pitchTarget, blendT);
            _thrusterPressureTickValue = math.lerp(_thrusterPressureTickValue, pressureTarget, blendT);
            _thrusterAccelerationTickValue = math.lerp(_thrusterAccelerationTickValue, throttleAttack, blendT);
            _thrusterHeavyCarryTickValue = math.lerp(_thrusterHeavyCarryTickValue, heavyCarryTarget, blendT);
            _thrusterDiveTickValue = math.lerp(_thrusterDiveTickValue, diveAttack, blendT);
            _vehicleCavitationSpeedTickValue = math.lerp(
                _vehicleCavitationSpeedTickValue,
                ResolveVehicleCavitationSpeed01(vehicleMotorSpeed),
                blendT);

            _targetThrusterBlendValue = _thrusterBlendTickValue;
            _targetThrusterLoadValue = _thrusterLoadTickValue;
            _targetThrusterRpmValue = _thrusterRpmTickValue;
            _targetThrusterPitchValue = _thrusterPitchTickValue;
            _targetThrusterPressureValue = _thrusterPressureTickValue;
            _targetThrusterAccelerationValue = _thrusterAccelerationTickValue;
            _targetThrusterHeavyCarryValue = _thrusterHeavyCarryTickValue;
            _targetThrusterDiveValue = _thrusterDiveTickValue;
            _targetVehicleCavitationSpeed01 = _vehicleCavitationSpeedTickValue;
        }

        private static float ResolveVehicleCavitationSpeed01(float speedMetersPerSecond)
        {
            float speedRange = math.max(
                VehicleCavitationScreechFullMetersPerSecond - VehicleCavitationScreechStartMetersPerSecond,
                0.01f);
            return math.saturate((math.max(0f, speedMetersPerSecond) - VehicleCavitationScreechStartMetersPerSecond) / speedRange);
        }

        private void UpdateCaveReverb(float deltaTime)
        {
            ResolveListenerReverbFilter();
            if (!_reverbMixerBindingsValid && _listenerReverbFilter == null)
                return;

            bool shouldUseWaterReverb = playerMovement != null && playerMovement.IsPlayerSubmerged;
            if (!shouldUseWaterReverb || _boundPlayerTransform == null)
            {
                ResetReverbModelState();
                RestoreListenerReverbDefaults();
                return;
            }

            float reverbBlendT = ApproximateOneMinusExpNegPositive(math.max(caveReverbFollowSharpness, 0.01f) * deltaTime);
            float targetDecayTime = openWaterDecayTime;
            float targetWetMix = FakeOpenWaterReverbMix01;
            float targetOpenness = 1f;
            float targetDensityIndex = 0f;
            float reverbDistanceScale = math.max(caveCeilingThreshold, openWaterPresetDistance);
            float caveThreshold01 = math.saturate(caveCeilingThreshold / math.max(0.001f, reverbDistanceScale));

            if (GlobalRegistry.Audio is SpatialAudioManager spatialAudioManager)
            {
                float caveInterior01 = math.saturate(spatialAudioManager.ListenerCaveInterior01);
                bool insideCaveVolume = spatialAudioManager.IsListenerInsideCaveVolume;
                float effectiveCaveInterior01 = insideCaveVolume
                    ? math.saturate(caveInterior01 + caveThreshold01 * (1f - caveInterior01))
                    : 0f;
                float sabineRt60Seconds = spatialAudioManager.ListenerSabineRt60Seconds;
                targetDecayTime = insideCaveVolume
                    ? sabineRt60Seconds > 0f
                        ? sabineRt60Seconds
                        : math.lerp(caveDecayTime, caveDecayTime * 1.35f, effectiveCaveInterior01)
                    : openWaterDecayTime;
                targetWetMix = insideCaveVolume ? FakeCaveReverbMix01 : FakeOpenWaterReverbMix01;
                targetOpenness = insideCaveVolume ? math.lerp(0.28f, 0.12f, effectiveCaveInterior01) : 1f;
                targetDensityIndex = insideCaveVolume ? math.lerp(0.7f, 1f, effectiveCaveInterior01) : 0f;
            }

            float densityBlendT = ApproximateOneMinusExpNegPositive(math.max(EnclosureDensityFollowSharpness, 0.01f) * deltaTime);
            _smoothedEnclosureDensityIndex = math.lerp(_smoothedEnclosureDensityIndex, targetDensityIndex, densityBlendT);
            _targetEnclosureDensityIndex = _smoothedEnclosureDensityIndex;
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
            _targetPressureScrubberHumDrive = 0f;
            _targetPressureScrubberHumGain = 0f;
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (!_warnedMissingListenerReverbFilter)
                {
                    _warnedMissingListenerReverbFilter = true;
                    Debug.LogWarning("[PlayerCriticalProceduralAudioRenderer] Missing authored AudioReverbFilter. RequireComponent should install it before runtime; reverb fallback is disabled.", this);
                }
#endif
                return;
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (!_warnedMissingReverbMixerParameters)
                {
                    _warnedMissingReverbMixerParameters = true;
                    Debug.LogWarning("[PlayerCriticalProceduralAudioRenderer] Reverb control mixer is missing one or more exposed parameters. Falling back to AudioReverbFilter.", this);
                }
#endif

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

            int inactiveIndex = 1 - Volatile.Read(ref _pendingSonarStateReadIndex);
            NativeArray<SonarEchoTap> inactiveTapBuffer = inactiveIndex == 0 ? _pendingSonarEchoTapsA : _pendingSonarEchoTapsB;
            int ghostTapCount = 0;
            if (inactiveTapBuffer.IsCreated)
            {
                int tapLimit = math.min(SonarGhostEchoTapCount, inactiveTapBuffer.Length);
                for (int tapIndex = 0; tapIndex < tapLimit; tapIndex++)
                    inactiveTapBuffer[tapIndex] = BuildGhostSonarEchoTap(sequence, tapIndex, intensity);
                ghostTapCount = tapLimit;
            }

            SonarTriggerState pendingState = new SonarTriggerState
            {
                Sequence = sequence,
                EchoRevision = echoRevision,
                StartFrame = scheduledStartFrame,
                Intensity = math.saturate(intensity),
                EchoTapCount = ghostTapCount
            };
            PublishPendingSonarState(inactiveIndex, pendingState, ghostTapCount);

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

            int frame = Time.frameCount;
            if (_sonarEchoCompositeFrame != frame)
            {
                FlushSonarEchoCompositeGroups(allowJobCompletion: false);
                _sonarEchoCompositeFrame = frame;
            }

            AbsoluteUniversePosition echoAup = echoEvent.ResolveWorldAup();
            NativeArray<SonarEchoCompositeGroup> writeCandidates = GetSonarEchoCompositeCandidateBuffer(_sonarEchoCompositeWriteBufferIndex);
            int writeCandidateCount = GetSonarEchoCompositeCandidateCount(_sonarEchoCompositeWriteBufferIndex);
            if (!writeCandidates.IsCreated ||
                writeCandidateCount >= SonarEchoCompositeCandidateCapacity)
                return;

            writeCandidates[writeCandidateCount] = new SonarEchoCompositeGroup(
                echoAup,
                echoEvent.DistanceMeters,
                echoEvent.ReturnStrength,
                echoEvent.Resonance,
                1,
                echoEvent.AudioMaterialId);
            SetSonarEchoCompositeCandidateCount(_sonarEchoCompositeWriteBufferIndex, writeCandidateCount + 1);
        }

        private void FlushSonarEchoCompositeGroups(bool allowJobCompletion)
        {
            if (_sonarEchoCompositeHashJobScheduled)
            {
                if (!allowJobCompletion)
                    return;

                if (!CompleteSonarEchoCompositeHashJob(forceComplete: false))
                    return;

                PublishCompletedSonarEchoCompositeGroups();
            }

            int writeBufferIndex = _sonarEchoCompositeWriteBufferIndex;
            int candidateCount = GetSonarEchoCompositeCandidateCount(writeBufferIndex);
            if (candidateCount <= 0)
                return;

            NativeArray<SonarEchoCompositeGroup> candidates = GetSonarEchoCompositeCandidateBuffer(writeBufferIndex);
            if (!ScheduleSonarEchoCompositeHashJob(candidates, candidateCount))
            {
                SetSonarEchoCompositeCandidateCount(writeBufferIndex, 0);
                return;
            }

            _sonarEchoCompositeScheduledBufferIndex = writeBufferIndex;
            _sonarEchoCompositeScheduledCandidateCount = math.clamp(candidateCount, 0, SonarEchoCompositeCandidateCapacity);
            _sonarEchoCompositeWriteBufferIndex = writeBufferIndex ^ 1;
            SetSonarEchoCompositeCandidateCount(_sonarEchoCompositeWriteBufferIndex, 0);
            _sonarEchoCompositeFrame = Time.frameCount;
        }

        private void PublishCompletedSonarEchoCompositeGroups()
        {
            int groupCount = _sonarEchoCompositeGroupCountNative.IsCreated
                ? math.clamp(_sonarEchoCompositeGroupCountNative[0], 0, SonarEchoCompositeGroupCapacity)
                : 0;
            for (int i = 0; i < groupCount; i++)
            {
                SonarEchoCompositeGroup group = _sonarEchoCompositeGroups[i];
                int hitCount = math.max(1, group.HitCount);
                float invHitCount = 1f / hitCount;
                float hitScale = ResolveSonarCompositeHitScale(hitCount);
                EnqueueCompositeAcousticEcho(
                    group.DistanceMeters * invHitCount,
                    group.ReturnStrength * invHitCount * hitScale,
                    group.Resonance * invHitCount,
                    group.AudioMaterialId);
                _sonarEchoCompositeGroups[i] = default;
            }

            int candidateCount = _sonarEchoCompositeScheduledCandidateCount;
            NativeArray<SonarEchoCompositeGroup> candidates = GetSonarEchoCompositeCandidateBuffer(_sonarEchoCompositeScheduledBufferIndex);
            for (int i = 0; i < candidateCount && i < SonarEchoCompositeCandidateCapacity; i++)
                candidates[i] = default;

            if (_sonarEchoCompositeGroupCountNative.IsCreated)
                _sonarEchoCompositeGroupCountNative[0] = 0;
            if (_sonarEchoCompositeScheduledBufferIndex >= 0)
                SetSonarEchoCompositeCandidateCount(_sonarEchoCompositeScheduledBufferIndex, 0);
            _sonarEchoCompositeScheduledBufferIndex = -1;
            _sonarEchoCompositeScheduledCandidateCount = 0;
            _sonarEchoCompositeFrame = Time.frameCount;
        }

        private bool ScheduleSonarEchoCompositeHashJob(NativeArray<SonarEchoCompositeGroup> candidates, int candidateCount)
        {
            if (!candidates.IsCreated ||
                !_sonarEchoCompositeGroups.IsCreated ||
                !_sonarEchoCompositeGroupCountNative.IsCreated ||
                !_sonarEchoCompositeSpatialHash.IsCreated ||
                !_sonarEchoCompositeGroupByHash.IsCreated)
            {
                return false;
            }

            _sonarEchoCompositeSpatialHash.Clear();
            _sonarEchoCompositeGroupByHash.Clear();
            _sonarEchoCompositeGroupCountNative[0] = 0;

            SonarEchoSpatialHashCoalesceJob coalesceJob = new SonarEchoSpatialHashCoalesceJob
            {
                Candidates = candidates,
                SpatialHash = _sonarEchoCompositeSpatialHash,
                GroupByHash = _sonarEchoCompositeGroupByHash,
                Groups = _sonarEchoCompositeGroups,
                GroupCount = _sonarEchoCompositeGroupCountNative,
                CandidateCount = math.clamp(candidateCount, 0, SonarEchoCompositeCandidateCapacity)
            };
            _sonarEchoCompositeHashHandle = coalesceJob.Schedule();
            _sonarEchoCompositeHashJobScheduled = true;
            return true;
        }

        private bool CompleteSonarEchoCompositeHashJob(bool forceComplete)
        {
            if (!_sonarEchoCompositeHashJobScheduled)
                return true;

            if (!DispatcherJobSwap.TryComplete(ref _sonarEchoCompositeHashHandle, forceComplete))
                return false;

            _sonarEchoCompositeHashJobScheduled = false;
            return true;
        }

        private NativeArray<SonarEchoCompositeGroup> GetSonarEchoCompositeCandidateBuffer(int bufferIndex)
        {
            return bufferIndex == 0
                ? _sonarEchoCompositeCandidatesA
                : _sonarEchoCompositeCandidatesB;
        }

        private int GetSonarEchoCompositeCandidateCount(int bufferIndex)
        {
            return bufferIndex == 0
                ? _sonarEchoCompositeCandidateCountA
                : _sonarEchoCompositeCandidateCountB;
        }

        private void SetSonarEchoCompositeCandidateCount(int bufferIndex, int count)
        {
            if (bufferIndex == 0)
                _sonarEchoCompositeCandidateCountA = count;
            else
                _sonarEchoCompositeCandidateCountB = count;
        }

        private static float ResolveSonarCompositeHitScale(int hitCount)
        {
            switch (hitCount)
            {
                case 1: return 1f;
                case 2: return 1.4142135f;
                case 3: return 1.7320508f;
                case 4: return 2f;
                case 5: return 2.236068f;
                case 6: return 2.4494898f;
                case 7: return 2.6457512f;
                case 8: return 2.8284271f;
                case 9: return 3f;
                case 10: return 3.1622777f;
                case 11: return 3.3166249f;
                case 12: return 3.4641016f;
                case 13: return 3.6055512f;
                case 14: return 3.7416575f;
                case 15: return 3.8729835f;
                default: return hitCount <= 0 ? 1f : 4f;
            }
        }

        private static int ResolveSonarEchoCompositeHash(in AbsoluteUniversePosition position, byte audioMaterialId)
        {
            const uint primeX = 73856093u;
            const uint primeY = 19349663u;
            const uint primeZ = 83492791u;
            const uint primeMaterial = 2654435761u;
            double cellSize = SonarEchoCompositeCellSizeMeters;
            double sectorSize = 5000d;
            int cellX = (int)math.floor(((position.GridX * sectorSize) + position.LocalX) / cellSize);
            int cellY = (int)math.floor(((position.GridY * sectorSize) + position.LocalY) / cellSize);
            int cellZ = (int)math.floor(((position.GridZ * sectorSize) + position.LocalZ) / cellSize);

            unchecked
            {
                uint hash = ((uint)cellX * primeX) ^
                            ((uint)cellY * primeY) ^
                            ((uint)cellZ * primeZ) ^
                            ((uint)audioMaterialId * primeMaterial);
                hash ^= hash >> 16;
                return (int)(hash & 0x7FFFFFFFu);
            }
        }

        private void EnqueueCompositeAcousticEcho(float distanceMeters, float returnStrength, float resonance, byte audioMaterialId)
        {
            float resonanceScale = math.clamp(resonance, 0.65f, 1.45f);
            float materialPitchScale = ResolveSonarMaterialPitchScale(audioMaterialId);
            float materialDecayMultiplier = ResolveSonarMaterialDecayMultiplier(audioMaterialId);
            float resonance01 = math.saturate((resonanceScale - 0.65f) / 0.8f);
            float roundTripDistance = math.max(0f, distanceMeters) * 2f;
            float echoDelaySeconds = math.clamp(roundTripDistance / SoundSpeedWaterMetersPerSecond, 0f, SonarEchoMaximumDelaySeconds);
            float echoAttenuation = ApproximateExpNegPositive(roundTripDistance * SonarEchoAbsorptionCoefficient);
            float echoExcitation = math.saturate(
                returnStrength *
                math.lerp(0.65f, 1.2f, resonance01) *
                materialDecayMultiplier *
                math.max(0.2f, echoAttenuation));
            float echoLowPassCutoffHz = ResolveSonarMaterialLowPassCutoffHz(
                audioMaterialId,
                math.lerp(1450f, AcousticOcclusionUtility.OpenLowPassCutoffHertz, resonance01));
            TryEnqueueImpactAudioEvent(
                0f,
                0f,
                0f,
                echoExcitation,
                echoDelaySeconds,
                echoAttenuation,
                echoLowPassCutoffHz,
                math.clamp(resonanceScale * materialPitchScale, 0.05f, 4f));
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

        private SonarEchoTap BuildGhostSonarEchoTap(int sequence, int tapIndex, float intensity)
        {
            uint seed = HashUInt((uint)sequence ^ ((uint)tapIndex * 0x9E3779B9u) ^ 0x5D6E2F91u);
            float baseDelaySeconds = tapIndex == 0 ? 0.18f : tapIndex == 1 ? 0.43f : 0.82f;
            float baseGain = tapIndex == 0 ? 0.58f : tapIndex == 1 ? 0.34f : 0.22f;
            float lowPassCutoffHz = tapIndex == 0 ? 5200f : tapIndex == 1 ? 3100f : 1650f;
            float delayJitter = math.lerp(-0.045f, 0.075f, Hash01(seed ^ 0xA1B2C3D4u));
            float gainJitter = math.lerp(0.58f, 1.12f, Hash01(seed ^ 0x6C8E9CF5u));
            float pitchJitter = math.lerp(0.94f, 1.08f, Hash01(seed ^ 0xB47A1D39u));
            float panStereo = math.lerp(-0.82f, 0.82f, Hash01(seed ^ 0xD1F3A55Bu));
            return BuildSonarEchoTap(
                math.clamp(baseDelaySeconds + delayJitter, 0.05f, SonarEchoMaximumDelaySeconds),
                pitchJitter,
                math.saturate(math.saturate(intensity) * baseGain * gainJitter),
                panStereo,
                lowPassCutoffHz);
        }

        private SonarEchoTap BuildSonarEchoTap(
            float delaySeconds,
            float dopplerRatio,
            float attenuation,
            float panStereo,
            float lowPassCutoffHz)
        {
            float clampedDelaySeconds = math.clamp(delaySeconds, 0f, SonarEchoMaximumDelaySeconds);
            float clampedPanStereo = math.clamp(panStereo, -1f, 1f);
            int sampleRate = math.max(_sampleRate, 1);
            SonarEchoTap tap = new SonarEchoTap
            {
                DelaySeconds = clampedDelaySeconds,
                PreviousDopplerRatio = 1f,
                DopplerRatio = math.clamp(dopplerRatio, SonarEchoMinimumDopplerRatio, SonarEchoMaximumDopplerRatio),
                Attenuation = math.saturate(attenuation),
                LowPassCutoffHz = math.clamp(
                    lowPassCutoffHz,
                    AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                    AcousticOcclusionUtility.OpenLowPassCutoffHertz),
                DelaySamples = math.clamp(
                    (int)(clampedDelaySeconds * sampleRate + 0.5f),
                    1,
                    SonarEchoDelayCapacity - 4)
            };
            float linearPan = 0.5f * clampedPanStereo;
            tap.LeftPanDeltaGain = -0.5f - linearPan;
            tap.RightPanDeltaGain = -0.5f + linearPan;

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
                tap.LowPassB0 = 1f;
                tap.LowPassB1 = 0f;
                tap.LowPassB2 = 0f;
                tap.LowPassA1 = 0f;
                tap.LowPassA2 = 0f;
                tap.UseLowPass = 0;
            }

            return tap;
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

        void IPhysicsAcousticImpulseEventListener.OnAcousticImpulse(in global::Hecton8.Physics.AcousticImpulseEvent impulseEvent)
        {
            HandleAcousticImpulse(in impulseEvent);
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
            float distance = 0f;
            if (!isPlayerOwnedImpact)
            {
                AbsoluteUniversePosition impactAup = impactSignal.ResolvePointAup();
                if (!TryResolveBoundPlayerDistanceWithin(in impactAup, maxDistance, out distance))
                    return;
            }

            float proximity = isPlayerOwnedImpact
                ? 1f
                : 1f - math.saturate(distance / maxDistance);
            if (!impactSignal.IsHeavy && impactSignal.MassVelocity < PhysicsImpactMinimumAudibleMassVelocity)
                return;

            ResolveImpactMaterialBlend(
                impactSignal.PrimaryAudioMaterialId,
                impactSignal.SecondaryAudioMaterialId,
                out float clangMaterialMultiplier,
                out float echoMaterialMultiplier,
                out float hollowMaterialMultiplier);
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
            float echoExcitation = math.saturate(
                metallicImpulse *
                math.lerp(0.45f, 1f, impactVolume01) *
                echoMaterialMultiplier *
                hollowMaterialMultiplier);
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

        private void HandleAcousticImpulse(in global::Hecton8.Physics.AcousticImpulseEvent impulseEvent)
        {
            if (_boundPlayerTransform == null)
                return;

            float maxDistance = math.max(PhysicsImpactStressRadiusMeters, impulseEvent.RadiusMeters);
            if (!TryResolveBoundPlayerDistanceWithin(impulseEvent.RuntimePosition, maxDistance, out float distance))
                return;

            float proximity = 1f - math.saturate(distance / math.max(maxDistance, 0.001f));
            float audible01 = math.saturate(impulseEvent.Volume01 * math.max(0.12f, proximity));
            if (audible01 <= 0.001f)
                return;

            bool isCritical = (impulseEvent.Flags & AcousticImpulseFlags.Critical) != 0;
            bool isLeviathan = (impulseEvent.Flags & AcousticImpulseFlags.Leviathan) != 0;
            float threatScale = isCritical ? 1.25f : 1f;
            if (isLeviathan)
                threatScale = math.max(threatScale, 1.45f);

            float materialDecayMultiplier = ResolveSonarMaterialDecayMultiplier(impulseEvent.AudioMaterialId);
            float materialPitchScale = ResolveSonarMaterialPitchScale(impulseEvent.AudioMaterialId);
            float stress = math.saturate(audible01 * 0.45f * threatScale);
            float metallic = impulseEvent.AudioMaterialId == SonarAudioMaterialIdMetal
                ? math.saturate(audible01 * math.lerp(0.45f, 0.9f, proximity))
                : 0f;
            float clangExcitation = math.saturate(audible01 * materialPitchScale * threatScale);
            float echoExcitation = math.saturate(audible01 * materialDecayMultiplier * 0.72f);
            float echoDelaySeconds = math.clamp(distance / SoundSpeedWaterMetersPerSecond, 0f, SonarEchoMaximumDelaySeconds);
            float echoLowPassCutoffHz = ResolveSonarMaterialLowPassCutoffHz(
                impulseEvent.AudioMaterialId,
                math.lerp(720f, AcousticOcclusionUtility.OpenLowPassCutoffHertz, proximity));

            TryEnqueueImpactAudioEvent(
                stress,
                metallic,
                clangExcitation,
                echoExcitation,
                echoDelaySeconds,
                proximity,
                echoLowPassCutoffHz,
                math.clamp(impulseEvent.PitchScale * materialPitchScale, 0.05f, 4f));
            _impactStressImpulseTickValue = math.max(_impactStressImpulseTickValue, stress);
        }

        void IProceduralAudioEventListener.OnAudioPingTriggered(in AudioPingTriggerInfo info)
        {
            if (info.Kind == ProceduralAudioPingKind.PredatorKill)
                HandlePredatorKillAudioPing(in info);
            else if (info.Kind == ProceduralAudioPingKind.MeteorBoom)
                HandleMeteorBoomAudioPing(in info);
            else if (info.Kind == ProceduralAudioPingKind.MechanicalWhirr)
                HandleMechanicalWhirrAudioPing(in info);
            else if (info.Kind == ProceduralAudioPingKind.LeviathanRoar)
                HandleLeviathanRoarAudioPing(in info);
        }

        void IProceduralAudioEventListener.OnStructuralStressTriggered(in StructuralStressAudioInfo info)
        {
            HandleStructuralStressTriggered(in info);
        }

        private void HandlePredatorKillAudioPing(in AudioPingTriggerInfo info)
        {
            if (_boundPlayerTransform == null)
                return;

            if (!TryResolveBoundPlayerDistanceWithin(info.WorldPosition, PredatorKillAudioRadiusMeters, out float distance))
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

            if (!TryResolveBoundPlayerDistanceWithin(info.WorldPosition, MeteorBoomAudioRadiusMeters, out float distance))
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

            if (!TryResolveBoundPlayerDistanceWithin(info.WorldPosition, MechanicalWhirrAudioRadiusMeters, out float distance))
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

        private void HandleLeviathanRoarAudioPing(in AudioPingTriggerInfo info)
        {
            if (_boundPlayerTransform == null)
                return;

            float maxDistance = PredatorKillAudioRadiusMeters * 2.5f;
            if (!TryResolveBoundPlayerAupDistanceWithin(
                    info.WorldPosition,
                    maxDistance,
                    out AbsoluteUniversePosition playerAup,
                    out AbsoluteUniversePosition predatorAup,
                    out float distance))
            {
                return;
            }

            float proximity = 1f - math.saturate(distance / maxDistance);
            float transmission01 = math.saturate(info.AcousticTransmission01);
            float aggroLevel = math.saturate(math.max(info.Intensity, info.ChirpDurationSeconds) * proximity * math.max(0.1f, transmission01));
            if (aggroLevel <= 0.001f)
                return;

            float3 predatorDeltaAup = AbsoluteUniversePosition.ToCameraRelativeFloat3(predatorAup, playerAup);
            _pendingLeviathanRoarDistanceMeters = distance;
            _hasPendingLeviathanRoarDistance = true;
            float dopplerPitchScale = math.clamp(
                _targetLeviathanRoarPitchScale,
                LeviathanDopplerMinimumPitchScale,
                LeviathanDopplerMaximumPitchScale);
            _targetLeviathanRoarAggroValue = math.max(_targetLeviathanRoarAggroValue, aggroLevel);
            _impactStressImpulseTickValue = math.max(_impactStressImpulseTickValue, aggroLevel * 0.22f);
            Vector3 directionToPredator = new Vector3(predatorDeltaAup.x, predatorDeltaAup.y, predatorDeltaAup.z);
            PhysicsEventBus.NotifyAcousticImpulse(new AcousticImpulseEvent(
                info.WorldPosition,
                directionToPredator,
                0f,
                aggroLevel,
                dopplerPitchScale,
                PredatorKillAudioRadiusMeters * 2.5f,
                0,
                SonarAudioMaterialIdDefault,
                AcousticImpulseFlags.Leviathan));
        }

        private void UpdateLeviathanDopplerCache()
        {
            if (!_hasPendingLeviathanRoarDistance)
                return;

            _hasPendingLeviathanRoarDistance = false;
            _targetLeviathanRoarPitchScale = ResolveLeviathanDopplerPitchScale(_pendingLeviathanRoarDistanceMeters);
        }

        private float ResolveLeviathanDopplerPitchScale(float currentDistance)
        {
            if (currentDistance <= 0.001f || !math.isfinite(currentDistance))
                return 1f;

            float now = Time.unscaledTime;
            if (!_hasLeviathanRoarDopplerSample)
            {
                _hasLeviathanRoarDopplerSample = true;
                _lastLeviathanRoarDistanceMeters = currentDistance;
                _lastLeviathanRoarSampleTime = now;
                _lastLeviathanRoarRelativeVelocityMetersPerSecond = 0f;
                return 1f;
            }

            float deltaTime = math.max(0.0001f, now - _lastLeviathanRoarSampleTime);
            float radialVelocity = (_lastLeviathanRoarDistanceMeters - currentDistance) / deltaTime;
            radialVelocity = math.clamp(
                radialVelocity,
                -LeviathanDopplerVelocityClampMetersPerSecond,
                LeviathanDopplerVelocityClampMetersPerSecond);
            float rawRatio = (SoundSpeedWaterMetersPerSecond + radialVelocity) /
                             math.max(1f, SoundSpeedWaterMetersPerSecond - radialVelocity);
            float clampedRatio = math.clamp(rawRatio, LeviathanDopplerMinimumPitchScale, LeviathanDopplerMaximumPitchScale);
            if (math.abs(radialVelocity - _lastLeviathanRoarRelativeVelocityMetersPerSecond) > LeviathanDopplerVelocityJumpThresholdMetersPerSecond)
            {
                float smoothingWindowSeconds = LeviathanDopplerSmoothingSamples / LeviathanDopplerSmoothingReferenceSampleRate;
                float blend = math.saturate(deltaTime / math.max(0.0001f, smoothingWindowSeconds));
                clampedRatio = math.lerp(_targetLeviathanRoarPitchScale, clampedRatio, blend);
            }

            _lastLeviathanRoarDistanceMeters = currentDistance;
            _lastLeviathanRoarSampleTime = now;
            _lastLeviathanRoarRelativeVelocityMetersPerSecond = radialVelocity;
            return clampedRatio;
        }

        private void HandleStructuralStressTriggered(in StructuralStressAudioInfo stressInfo)
        {
            if (_boundPlayerTransform == null)
                return;

            float maxDistance = PhysicsImpactStressRadiusMeters;
            if (!TryResolveBoundPlayerDistanceWithin(stressInfo.WorldPosition, maxDistance, out float distance))
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

        private static void ResolveImpactMaterialBlend(
            byte primaryMaterialId,
            byte secondaryMaterialId,
            out float clangMaterialMultiplier,
            out float echoMaterialMultiplier,
            out float hollowMaterialMultiplier)
        {
            float primaryClang = ResolveImpactClangMaterialMultiplier(primaryMaterialId);
            float secondaryClang = ResolveImpactClangMaterialMultiplier(secondaryMaterialId);
            float primaryEcho = ResolveImpactEchoMaterialMultiplier(primaryMaterialId);
            float secondaryEcho = ResolveImpactEchoMaterialMultiplier(secondaryMaterialId);
            clangMaterialMultiplier = math.max(0.1f, (primaryClang + secondaryClang) * 0.5f);
            echoMaterialMultiplier = math.max(0.1f, (primaryEcho + secondaryEcho) * 0.5f);
            byte dominantMaterialId = ResolveDominantImpactMaterialId(primaryMaterialId, secondaryMaterialId);
            hollowMaterialMultiplier = (dominantMaterialId == (byte)ItemAudioMaterialId.Metal ||
                                        dominantMaterialId == (byte)ItemAudioMaterialId.Glass)
                ? 0.86f
                : 1.18f;
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

        private static float ResolveSonarMaterialPitchScale(byte audioMaterialId)
        {
            switch (audioMaterialId)
            {
                case SonarAudioMaterialIdMetal:
                    return 1.18f;

                case SonarAudioMaterialIdRock:
                    return 0.82f;

                case SonarAudioMaterialIdGlass:
                    return 1.34f;

                default:
                    return 1f;
            }
        }

        private static float ResolveSonarMaterialDecayMultiplier(byte audioMaterialId)
        {
            switch (audioMaterialId)
            {
                case SonarAudioMaterialIdMetal:
                    return 1.35f;

                case SonarAudioMaterialIdRock:
                    return 0.62f;

                case SonarAudioMaterialIdGlass:
                    return 1.18f;

                default:
                    return 0.86f;
            }
        }

        private static float ResolveSonarMaterialLowPassCutoffHz(byte audioMaterialId, float baseCutoffHz)
        {
            float cutoff = math.clamp(
                baseCutoffHz,
                AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                AcousticOcclusionUtility.OpenLowPassCutoffHertz);
            switch (audioMaterialId)
            {
                case SonarAudioMaterialIdMetal:
                    return math.clamp(math.max(cutoff, 8200f), AcousticOcclusionUtility.MinimumLowPassCutoffHertz, AcousticOcclusionUtility.OpenLowPassCutoffHertz);

                case SonarAudioMaterialIdRock:
                    return math.min(cutoff, 2400f);

                case SonarAudioMaterialIdGlass:
                    return math.clamp(math.max(cutoff, 6800f), AcousticOcclusionUtility.MinimumLowPassCutoffHertz, AcousticOcclusionUtility.OpenLowPassCutoffHertz);

                default:
                    return cutoff;
            }
        }

        private void HandleActiveTransportLifecycleChanged(IPlayerTransportLifecycleOwner lifecycleOwner)
        {
            _activeTransportLifecycleOwner = lifecycleOwner;
            ResolveStructuralHullReadModel(lifecycleOwner);
        }

        private void TryBindFromBootstrap()
        {
            GameObject playerObject = GameBootstrapper.CurrentPlayerObject;
            if (playerObject != null)
            {
                if (!ReferenceEquals(_boundPlayerObject, playerObject))
                    BindToPlayer(playerObject);
                return;
            }

            if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) && playerTransform != null)
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

        private bool TryRegisterRuntimeService()
        {
            if (_runtimeRegistered || !Application.isPlaying)
                return true;

            PlayerCriticalProceduralAudioRenderer registeredInstance = GlobalRegistry.PlayerCriticalAudio;
            if (registeredInstance != null && registeredInstance != this)
            {
                Destroy(this);
                return false;
            }

            GlobalRegistry.RegisterPlayerCriticalAudioRuntime(this);
            _runtimeRegistered = ReferenceEquals(GlobalRegistry.PlayerCriticalAudio, this);
            return _runtimeRegistered;
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

        private void TryUnregisterRuntimeService()
        {
            if (!_runtimeRegistered)
                return;

            GlobalRegistry.UnregisterPlayerCriticalAudioRuntime(this);
            _runtimeRegistered = false;
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

            bool retainedSabineReverbDelay = _sabineReverbDelay.IsCreated;
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
            _sonarEchoDelay = new NativeArray<float>(SonarEchoDelayCapacity, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[131072] - sonar linear echo delay ring - owner: PlayerCriticalProceduralAudioRenderer
            _pendingSonarEchoTapsA = new NativeArray<SonarEchoTap>(SonarEchoTapCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<SonarEchoTap>[12] - pending sonar echo taps A - owner: PlayerCriticalProceduralAudioRenderer
            _pendingSonarEchoTapsB = new NativeArray<SonarEchoTap>(SonarEchoTapCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<SonarEchoTap>[12] - pending sonar echo taps B - owner: PlayerCriticalProceduralAudioRenderer
            _workerSonarEchoTaps = new NativeArray<SonarEchoTap>(SonarEchoTapCapacity, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<SonarEchoTap>[12] - worker-owned sonar tap snapshot prevents main-thread tap tearing - owner: PlayerCriticalProceduralAudioRenderer
            _sonarEchoReadCursors = new NativeArray<float>(SonarEchoTapCapacity, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[12] - sonar echo read cursors per tap - owner: PlayerCriticalProceduralAudioRenderer
            _sonarEchoFilterInput1 = new NativeArray<float>(SonarEchoTapCapacity, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[12] - sonar echo low-pass x1 state per tap - owner: PlayerCriticalProceduralAudioRenderer
            _sonarEchoFilterInput2 = new NativeArray<float>(SonarEchoTapCapacity, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[12] - sonar echo low-pass x2 state per tap - owner: PlayerCriticalProceduralAudioRenderer
            _sonarEchoFilterOutput1 = new NativeArray<float>(SonarEchoTapCapacity, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[12] - sonar echo low-pass y1 state per tap - owner: PlayerCriticalProceduralAudioRenderer
            _sonarEchoFilterOutput2 = new NativeArray<float>(SonarEchoTapCapacity, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[12] - sonar echo low-pass y2 state per tap - owner: PlayerCriticalProceduralAudioRenderer
            _sonarEchoCompositeCandidatesA = new NativeArray<SonarEchoCompositeGroup>(SonarEchoCompositeCandidateCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<SonarEchoCompositeGroup>[32] - active-sonar echo candidates A before Burst AUP hash coalescing - owner: PlayerCriticalProceduralAudioRenderer
            _sonarEchoCompositeCandidatesB = new NativeArray<SonarEchoCompositeGroup>(SonarEchoCompositeCandidateCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<SonarEchoCompositeGroup>[32] - active-sonar echo candidates B before Burst AUP hash coalescing - owner: PlayerCriticalProceduralAudioRenderer
            _sonarEchoCompositeGroups = new NativeArray<SonarEchoCompositeGroup>(SonarEchoCompositeGroupCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<SonarEchoCompositeGroup>[8] - coalesced active-sonar echo groups by 10m AUP hash - owner: PlayerCriticalProceduralAudioRenderer
            _sonarEchoCompositeGroupCountNative = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[1] - sonar echo coalesced group count from Burst hash job - owner: PlayerCriticalProceduralAudioRenderer
            _sonarEchoCompositeSpatialHash = new NativeParallelMultiHashMap<int, int>(SonarEchoCompositeCandidateCapacity, Allocator.Persistent); // COLD ALLOC: NativeParallelMultiHashMap<int,int>[32] - sonar echo AUP cell occupancy before DSP tap publish - owner: PlayerCriticalProceduralAudioRenderer
            _sonarEchoCompositeGroupByHash = new NativeParallelHashMap<int, int>(SonarEchoCompositeGroupCapacity, Allocator.Persistent); // COLD ALLOC: NativeParallelHashMap<int,int>[8] - sonar echo hash-to-output group lookup - owner: PlayerCriticalProceduralAudioRenderer
            _impactClangDelay = new NativeArray<float>(ImpactClangDelayCapacity, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[1024] - Karplus-Strong impact delay line - owner: PlayerCriticalProceduralAudioRenderer
            _thrusterCombDelay = new NativeArray<float>(ThrusterCombDelayCapacity, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[4096] - thruster comb filter delay ring - owner: PlayerCriticalProceduralAudioRenderer
            if (!_sabineReverbDelay.IsCreated)
                _sabineReverbDelay = new NativeArray<float>(SabineReverbDelayCapacity, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[262144] - 1,048,576 bytes fixed four-comb Sabine reverb delay field - owner: PlayerCriticalProceduralAudioRenderer
            else
                ClearScratchBufferCold(_sabineReverbDelay, _sabineReverbDelay.Length);
            _interiorFdnDelay = new NativeArray<float>(InteriorFdnDelayCapacity, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[8192] - dry BaseModule feedback delay network cache - owner: PlayerCriticalProceduralAudioRenderer
            _binauralDelayRing = new NativeArray<float>(BinauralDelayCapacity, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[128] - binaural ITD mono delay ring - owner: PlayerCriticalProceduralAudioRenderer
            _binauralShadowHistory = new NativeArray<float>(BinauralOutputChannels, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[2] - binaural shadow low-pass history per ear - owner: PlayerCriticalProceduralAudioRenderer
            _lowPassInputHistory1 = new NativeArray<float>(MaxFilterChannels, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[8] - final listener low-pass state x1 - owner: PlayerCriticalProceduralAudioRenderer
            _lowPassInputHistory2 = new NativeArray<float>(MaxFilterChannels, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[8] - final listener low-pass state x2 - owner: PlayerCriticalProceduralAudioRenderer
            _lowPassOutputHistory1 = new NativeArray<float>(MaxFilterChannels, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[8] - final listener low-pass state y1 - owner: PlayerCriticalProceduralAudioRenderer
            _lowPassOutputHistory2 = new NativeArray<float>(MaxFilterChannels, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[8] - final listener low-pass state y2 - owner: PlayerCriticalProceduralAudioRenderer
            _metallicGrainBank = new NativeArray<float>(MetallicGrainBankCapacity, Allocator.AudioKernel, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<float>[8192] - pre-baked metallic screech grain bank for hull granular synthesis - owner: PlayerCriticalProceduralAudioRenderer
            RegisterNativeBuffers(registerSabineReverbDelay: !retainedSabineReverbDelay);
            PlayerCriticalMetallicGrainBank.Generate(_metallicGrainBank);
            _sampleRingBuffer ??= new AudioFrameSpscRingBuffer();
            _sampleRingBuffer.Initialize(math.max(frameCapacity * 16, ringBufferCapacityFrames), BinauralOutputChannels);
            _producedSampleCount = 0L;
            _workerActiveSonarState = default;
            _workerConsumedSonarSequence = 0;
            _workerConsumedSonarRevision = 0;
            _workerActiveSonarTapCount = 0;
            _sonarEchoCompositeCandidateCountA = 0;
            _sonarEchoCompositeCandidateCountB = 0;
            Interlocked.Exchange(ref _impactEventQueueDropCount, 0);
            _sonarEchoCompositeWriteBufferIndex = 0;
            _sonarEchoCompositeScheduledBufferIndex = -1;
            _sonarEchoCompositeScheduledCandidateCount = 0;
            _sonarEchoCompositeHashJobScheduled = false;
            _pendingSonarEchoTapCountA = 0;
            _pendingSonarEchoTapCountB = 0;
            _pendingSonarStateA = default;
            _pendingSonarStateB = default;
            _heartbeatSynthesisState = default;
            _sabineReverbSynthesisState = default;
            _interiorFdnReverbSynthesisState = default;
            _tinnitusSynthesisState = default;
            _leviathanGranularSynthesisState = default;
            _criticalSidechainCompressorState = new CriticalSidechainCompressorState { Gain = 1f };
            _binauralDelayWriteIndex = 0;
            ResetSonarPhaseState(0);
            _buffersInitialized = true;
            SignalAudioProducerThread();
        }

        private void RegisterNativeBuffers(bool registerSabineReverbDelay)
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
            NativeMemorySentinel.RegisterNativeArray(_pendingSonarEchoTapsA, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_pendingSonarEchoTapsA), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_pendingSonarEchoTapsB, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_pendingSonarEchoTapsB), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_workerSonarEchoTaps, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_workerSonarEchoTaps), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_sonarEchoReadCursors, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_sonarEchoReadCursors), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_sonarEchoFilterInput1, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_sonarEchoFilterInput1), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_sonarEchoFilterInput2, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_sonarEchoFilterInput2), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_sonarEchoFilterOutput1, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_sonarEchoFilterOutput1), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_sonarEchoFilterOutput2, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_sonarEchoFilterOutput2), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_sonarEchoCompositeCandidatesA, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_sonarEchoCompositeCandidatesA), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_sonarEchoCompositeCandidatesB, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_sonarEchoCompositeCandidatesB), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_sonarEchoCompositeGroups, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_sonarEchoCompositeGroups), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_sonarEchoCompositeGroupCountNative, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_sonarEchoCompositeGroupCountNative), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeParallelMultiHashMap(_sonarEchoCompositeSpatialHash, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_sonarEchoCompositeSpatialHash), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeParallelHashMap(_sonarEchoCompositeGroupByHash, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_sonarEchoCompositeGroupByHash), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_impactClangDelay, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_impactClangDelay), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_thrusterCombDelay, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_thrusterCombDelay), NativeAllocationLifetime.Session);
            if (registerSabineReverbDelay)
                NativeMemorySentinel.RegisterNativeArray(_sabineReverbDelay, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_sabineReverbDelay), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_interiorFdnDelay, nameof(PlayerCriticalProceduralAudioRenderer), nameof(_interiorFdnDelay), NativeAllocationLifetime.Session);
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
            NativeMemorySentinel.UnregisterNativeArray(_pendingSonarEchoTapsA);
            NativeMemorySentinel.UnregisterNativeArray(_pendingSonarEchoTapsB);
            NativeMemorySentinel.UnregisterNativeArray(_workerSonarEchoTaps);
            NativeMemorySentinel.UnregisterNativeArray(_sonarEchoReadCursors);
            NativeMemorySentinel.UnregisterNativeArray(_sonarEchoFilterInput1);
            NativeMemorySentinel.UnregisterNativeArray(_sonarEchoFilterInput2);
            NativeMemorySentinel.UnregisterNativeArray(_sonarEchoFilterOutput1);
            NativeMemorySentinel.UnregisterNativeArray(_sonarEchoFilterOutput2);
            NativeMemorySentinel.UnregisterNativeArray(_sonarEchoCompositeCandidatesA);
            NativeMemorySentinel.UnregisterNativeArray(_sonarEchoCompositeCandidatesB);
            NativeMemorySentinel.UnregisterNativeArray(_sonarEchoCompositeGroups);
            NativeMemorySentinel.UnregisterNativeArray(_sonarEchoCompositeGroupCountNative);
            if (_sonarEchoCompositeSpatialHash.IsCreated)
                NativeMemorySentinel.UnregisterNativeParallelMultiHashMap(nameof(PlayerCriticalProceduralAudioRenderer), nameof(_sonarEchoCompositeSpatialHash));
            if (_sonarEchoCompositeGroupByHash.IsCreated)
                NativeMemorySentinel.UnregisterNativeParallelHashMap(nameof(PlayerCriticalProceduralAudioRenderer), nameof(_sonarEchoCompositeGroupByHash));
            NativeMemorySentinel.UnregisterNativeArray(_impactClangDelay);
            NativeMemorySentinel.UnregisterNativeArray(_thrusterCombDelay);
            if (unregisterSabineReverbDelay)
                NativeMemorySentinel.UnregisterNativeArray(_sabineReverbDelay);
            NativeMemorySentinel.UnregisterNativeArray(_interiorFdnDelay);
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
            CompleteSonarEchoCompositeHashJob(forceComplete: true);
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
            if (_pendingSonarEchoTapsA.IsCreated)
                _pendingSonarEchoTapsA.Dispose();
            if (_pendingSonarEchoTapsB.IsCreated)
                _pendingSonarEchoTapsB.Dispose();
            if (_workerSonarEchoTaps.IsCreated)
                _workerSonarEchoTaps.Dispose();
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
            if (_sonarEchoCompositeCandidatesA.IsCreated)
                _sonarEchoCompositeCandidatesA.Dispose();
            if (_sonarEchoCompositeCandidatesB.IsCreated)
                _sonarEchoCompositeCandidatesB.Dispose();
            if (_sonarEchoCompositeGroups.IsCreated)
                _sonarEchoCompositeGroups.Dispose();
            if (_sonarEchoCompositeGroupCountNative.IsCreated)
                _sonarEchoCompositeGroupCountNative.Dispose();
            if (_sonarEchoCompositeSpatialHash.IsCreated)
                _sonarEchoCompositeSpatialHash.Dispose();
            if (_sonarEchoCompositeGroupByHash.IsCreated)
                _sonarEchoCompositeGroupByHash.Dispose();
            if (_impactClangDelay.IsCreated)
                _impactClangDelay.Dispose();
            if (_thrusterCombDelay.IsCreated)
                _thrusterCombDelay.Dispose();
            if (disposeSabineReverbDelay && _sabineReverbDelay.IsCreated)
                _sabineReverbDelay.Dispose();
            if (_interiorFdnDelay.IsCreated)
                _interiorFdnDelay.Dispose();
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
            _pendingSonarEchoTapsA = default;
            _pendingSonarEchoTapsB = default;
            _workerSonarEchoTaps = default;
            _sonarEchoReadCursors = default;
            _sonarEchoFilterInput1 = default;
            _sonarEchoFilterInput2 = default;
            _sonarEchoFilterOutput1 = default;
            _sonarEchoFilterOutput2 = default;
            _sonarEchoCompositeCandidatesA = default;
            _sonarEchoCompositeCandidatesB = default;
            _sonarEchoCompositeGroups = default;
            _sonarEchoCompositeGroupCountNative = default;
            _sonarEchoCompositeSpatialHash = default;
            _sonarEchoCompositeGroupByHash = default;
            _impactClangDelay = default;
            _thrusterCombDelay = default;
            if (disposeSabineReverbDelay)
                _sabineReverbDelay = default;
            _interiorFdnDelay = default;
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
            _sonarEchoCompositeCandidateCountA = 0;
            _sonarEchoCompositeCandidateCountB = 0;
            _sonarEchoCompositeWriteBufferIndex = 0;
            _sonarEchoCompositeScheduledBufferIndex = -1;
            _sonarEchoCompositeScheduledCandidateCount = 0;
            _sonarEchoCompositeHashJobScheduled = false;
            _sabineReverbSynthesisState = default;
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
            ClearScratchBufferCold(_interiorFdnDelay, _interiorFdnDelay.Length);
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
            _workerActiveSonarTapCount = 0;
            Interlocked.Exchange(ref _impactEventReadIndex, 0);
            Interlocked.Exchange(ref _impactEventWriteIndex, 0);
            Interlocked.Exchange(ref _impactEventQueueDropCount, 0);
            _hullSynthesisState = default;
            _ambientCurrentSynthesisState = default;
            _impactEchoSynthesisState = default;
            _thrusterSynthesisState = default;
            _interiorFdnReverbSynthesisState = default;
            _tinnitusSynthesisState = default;
            _leviathanGranularSynthesisState = default;
            _criticalSidechainCompressorState = new CriticalSidechainCompressorState { Gain = 1f };
            _audioImpactStressValue = 0f;
            _audioImpactMetallicValue = 0f;
            _audioTinnitusOxygenStressValue = 0f;
            _audioLeviathanRoarAggroValue = 0f;
            _audioHullStressValue = 0f;
            _audioStructuralHullStressValue = 0f;
            _audioStructuralHullStressVelocityValue = 0f;
            if (_sonarEchoReadCursors.IsCreated)
            {
                for (int i = 0; i < _sonarEchoReadCursors.Length; i++)
                    _sonarEchoReadCursors[i] = -1f;
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
                ClearScratchBufferCold(_sonarEchoDelay, _sonarEchoDelay.Length);
            if (_impactClangDelay.IsCreated)
                ClearScratchBufferCold(_impactClangDelay, _impactClangDelay.Length);
            if (_impactEchoScratch.IsCreated)
                ClearScratchBufferCold(_impactEchoScratch, _impactEchoScratch.Length);
            if (_thrusterCombDelay.IsCreated)
                ClearScratchBufferCold(_thrusterCombDelay, _thrusterCombDelay.Length);
            if (_sabineReverbDelay.IsCreated)
                ClearScratchBufferCold(_sabineReverbDelay, _sabineReverbDelay.Length);
            _sabineReverbSynthesisState = default;
        }

        private float ResolveAbyssalLowPassTarget(float depthMeters)
        {
            return math.saturate(
                (math.max(0f, depthMeters) - AbyssalLowPassStartDepthMeters) /
                math.max(AbyssalLowPassFadeDepthMeters, 0.01f));
        }

        private void UpdatePressureScrubberHumCache(
            float absoluteDepthMeters,
            float depthParam,
            float enclosureDensityIndex)
        {
            float safeDepth = math.max(0f, absoluteDepthMeters);
            if (_pressureScrubberHumLastDepthMeters > float.MinValue &&
                math.abs(safeDepth - _pressureScrubberHumLastDepthMeters) <= PressureScrubberHumDepthUpdateDeltaMeters)
            {
                return;
            }

            _pressureScrubberHumLastDepthMeters = safeDepth;
            float pressureDrive = math.saturate(math.max(depthParam, enclosureDensityIndex));
            if (pressureDrive <= HullNoiseFloor)
            {
                _targetPressureScrubberHumDrive = 0f;
                _targetPressureScrubberHumGain = 0f;
                return;
            }

            _targetPressureScrubberHumDrive = ApproximatePressureScrubberHumDrive01(pressureDrive);
            _targetPressureScrubberHumGain = PressureScrubberHumMaximumGain * pressureDrive;
        }

        private bool TryResolveForwardEchoProbe(out Vector3 origin, out Vector3 forward, out Transform ignoreRoot)
        {
            origin = default;
            forward = Vector3.forward;
            ignoreRoot = _boundPlayerTransform != null ? _boundPlayerTransform.root : null;
            if (playerMovement == null)
                return false;

            float3 playerRuntime3 = playerMovement.CurrentAup.ToRuntimeFloat3();
            Vector3 playerRuntimePosition = new Vector3(playerRuntime3.x, playerRuntime3.y, playerRuntime3.z);

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null && playerContext.PlayerCamera != null)
            {
                Transform cameraTransform = playerContext.PlayerCamera.transform;
                if (cameraTransform != null)
                {
                    origin = playerRuntimePosition;
                    forward = cameraTransform.forward;
                    ignoreRoot = cameraTransform.root;
                    return forward.sqrMagnitude > 0.0001f;
                }
            }

            if (_boundPlayerTransform == null)
                return false;

            origin = playerRuntimePosition;
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

        private void UpdateAcousticThreatPulse()
        {
            if (_boundPlayerTransform == null || playerMovement == null)
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
            float3 playerRuntime3 = playerMovement.CurrentAup.ToRuntimeFloat3();
            Vector3 playerRuntimePosition = new Vector3(playerRuntime3.x, playerRuntime3.y, playerRuntime3.z);
            vegetationBridge.ApplyExternalThreatPulse(
                playerRuntimePosition,
                radius,
                strength,
                HullLfeThreatHoldSeconds);
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
                _heartbeatScratch[frameIndex] = 0f;

                float deltaSeconds = (float)invSampleRate;
                float duckDepth = HeartbeatDuckMaximum * math.lerp(0.18f, 1f, heartbeatDrive);
                float duckTarget = combinedEnvelope * duckDepth;
                float duckSharpness = duckTarget > state.DuckEnvelope
                    ? HeartbeatDuckAttackSharpness
                    : HeartbeatDuckReleaseSharpness;
                float duckBlend = ApproximateOneMinusExpNegPositive(duckSharpness * deltaSeconds);
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
            _ = invSampleRate;
            if (!_bubbleScratch.IsCreated)
                return;

            int safeCount = math.min(frameCount, _bubbleScratch.Length);
            if (safeCount <= 0)
                return;

            float startIntensity = math.saturate(_audioBubbleBoilIntensity);
            float endIntensity = math.saturate(bubbleBoilTarget);
            if (startIntensity <= HullNoiseFloor && endIntensity <= HullNoiseFloor)
            {
                ClearScratchBuffer(_bubbleScratch, frameCount);
                _audioBubbleBoilIntensity = 0f;
                return;
            }

            float depthDrive = math.lerp(
                0.78f,
                1.18f,
                ResolveAscendingNormalized01(math.max(0f, absoluteDepthMeters), 50f, 750f));
            for (int frameIndex = 0; frameIndex < safeCount; frameIndex++)
            {
                float frameT = safeCount > 1 ? frameIndex / (float)(safeCount - 1) : 1f;
                float intensity = math.lerp(startIntensity, endIntensity, frameT);
                uint sampleIndex = (uint)math.max(0L, blockStartFrame + frameIndex);
                uint burstIndex = sampleIndex >> ToolCavitationBurstShift;
                float burstDensity = math.lerp(
                    ToolCavitationBurstDensityMinimum,
                    ToolCavitationBurstDensityMaximum,
                    intensity);
                float burstThreshold = 1f - burstDensity;
                float burstHash = Hash01(burstIndex ^ 0xB0E1C9A5u);
                float burstGate = math.saturate((burstHash - burstThreshold) / math.max(burstDensity, 0.0001f));
                float burstOffset = sampleIndex & ToolCavitationBurstMask;
                float burstEnvelope = math.saturate(burstOffset / 8f) * ApproximateExpNegPositive(0.085f * burstOffset);
                float white = XorShiftSigned(sampleIndex, 0x7E5A3C91u);
                float high = HighBandNoise(sampleIndex ^ 0xA91F37D5u);
                float shapedNoise = (white * 0.34f) + (high * 0.66f);
                float heatEnvelope = intensity * intensity;
                _bubbleScratch[frameIndex] =
                    FastSoftClip(shapedNoise * 2.4f) *
                    burstGate *
                    burstEnvelope *
                    heatEnvelope *
                    depthDrive *
                    ToolCavitationMaximumGain;
            }

            _audioBubbleBoilIntensity = endIntensity;
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
            float startTinnitusStress = _audioTinnitusOxygenStressValue;
            float endTinnitusStress = math.saturate(parameters.TinnitusOxygenStress);
            float startLeviathanAggro = _audioLeviathanRoarAggroValue;
            float endLeviathanAggro = math.saturate(parameters.LeviathanRoarAggro);
            float startLeviathanPitchScale = math.max(0.05f, _audioLeviathanRoarPitchScale);
            float endLeviathanPitchScale = parameters.LeviathanRoarPitchScale > 0f
                ? math.clamp(
                    parameters.LeviathanRoarPitchScale,
                    LeviathanDopplerMinimumPitchScale,
                    LeviathanDopplerMaximumPitchScale)
                : 1f;
            AmbientCurrentSynthesisState ambientState = _ambientCurrentSynthesisState;
            float ambientDepthDrive = math.saturate(math.max(parameters.HullPressureDepth, parameters.AbyssalLowPassMix));
            float panicAmbientDull = math.saturate(parameters.HeartbeatStress);
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
            InteriorFdnReverbSynthesisState interiorFdnState = _interiorFdnReverbSynthesisState;
            TinnitusSynthesisState tinnitusState = _tinnitusSynthesisState;
            LeviathanGranularSynthesisState leviathanState = _leviathanGranularSynthesisState;
            float enclosureDensityTarget = math.saturate(parameters.EnclosureDensityIndex);
            float interiorFdnSend = math.saturate((1f - math.saturate(parameters.BinauralWaterDensityMul)) * enclosureDensityTarget);

            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                long sampleFrame = blockStartFrame + frameIndex;
                float frameT = frameCount > 1 ? frameIndex / (float)(frameCount - 1) : 1f;
                float tinnitusStress = math.lerp(startTinnitusStress, endTinnitusStress, frameT);
                float leviathanAggro = math.lerp(startLeviathanAggro, endLeviathanAggro, frameT);
                float leviathanPitchScale = math.lerp(startLeviathanPitchScale, endLeviathanPitchScale, frameT);
                float absoluteDepthMeters = math.lerp(startAbsoluteDepthMeters, endAbsoluteDepthMeters, frameT);
                float pressurePhaserDepth01 = ResolveAscendingNormalized01(
                    absoluteDepthMeters,
                    PressurePhaserStartDepthMeters,
                    PressurePhaserFullDepthMeters);
                float ambientCurrent = RenderAmbientCurrentSample(
                    ref ambientState,
                    sampleFrame,
                    invSampleRate,
                    ambientDepthDrive,
                    pressurePhaserDepth01,
                    panicAmbientDull);
                float leviathanRoar = RenderLeviathanGranularRoarSample(
                    ref leviathanState,
                    _metallicGrainBank,
                    sampleFrame,
                    leviathanAggro,
                    leviathanPitchScale,
                    invSampleRate);
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
                     _bubbleScratch[frameIndex] +
                     leviathanRoar) * _heartbeatDuckScratch[frameIndex];
                float tinnitus = RenderTinnitusSample(ref tinnitusState, tinnitusStress, panicAmbientDull, invSampleRate);
                float mixed = (mixedDry + _heartbeatScratch[frameIndex] + tinnitus) * outputHeadroom;

                if (sabineReverbActive)
                {
                    sabineState.WetMix = math.lerp(sabineState.WetMix, sabineWetGain, SabineReverbWetMixLerpCoefficient);
                    mixed = RenderSabineReverbSample(
                        ref sabineState,
                        mixed,
                        sabineState.WetMix,
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

                if (interiorFdnSend > 0.0001f && _interiorFdnDelay.IsCreated)
                    mixed = RenderInteriorFdnReverbSample(ref interiorFdnState, mixed, interiorFdnSend);

                float mix = math.lerp(startMix, endMix, frameT);
                float abyssalCutoff = math.lerp(_sampleRate * 0.45f, AbyssalLowPassCutoffHertz, mix);
                float pressureCutoff = math.lerp(startPressureCutoff, endPressureCutoff, frameT);
                float tinnitusCutoff = math.lerp(_sampleRate * 0.45f, TinnitusLowPassCutoffHertz, tinnitusStress);
                float cutoff = math.min(math.min(abyssalCutoff, pressureCutoff), tinnitusCutoff);
                float openCutoff = _sampleRate * 0.45f;
                float muffled01 = math.saturate((openCutoff - cutoff) / math.max(openCutoff - AcousticOcclusionUtility.MinimumLowPassCutoffHertz, 0.001f));
                float targetAlpha = ResolveOnePoleLowPassCoefficient(cutoff, _sampleRate);
                float alpha = math.lerp(1f, targetAlpha, muffled01);
                float filtered = ApplyOnePoleLowPass(mixed, _lowPassOutputHistory1[0] + BiquadDenormalBias, alpha);

                _lowPassInputHistory1[0] = mixed;
                _lowPassOutputHistory1[0] = filtered;
                mixed = math.lerp(mixed, filtered, muffled01);

                _mixScratch[frameIndex] = ApplyMasterSafetyLimiter(mixed);
            }

            _ambientCurrentSynthesisState = ambientState;
            _sabineReverbSynthesisState = sabineState;
            _interiorFdnReverbSynthesisState = interiorFdnState;
            _tinnitusSynthesisState = tinnitusState;
            _leviathanGranularSynthesisState = leviathanState;
            _criticalSidechainCompressorState = sidechainState;
            _audioAbyssalLowPassMix = endMix;
            _audioAbsoluteDepthMeters = endAbsoluteDepthMeters;
            _audioTinnitusOxygenStressValue = endTinnitusStress;
            _audioLeviathanRoarAggroValue = endLeviathanAggro;
            _audioLeviathanRoarPitchScale = endLeviathanPitchScale;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float RenderTinnitusSample(
            ref TinnitusSynthesisState state,
            float oxygenStress01,
            float playerStress01,
            double invSampleRate)
        {
            float stress = math.saturate(oxygenStress01);
            if (stress <= 0.0001f)
                return 0f;

            float sine = AdvanceSine(ref state.Phase, TinnitusCarrierHertz, invSampleRate);
            float playerStress = math.saturate(playerStress01);
            float exponentialStress = ApproximateOneMinusExpNegPositive(TinnitusPlayerStressExponentialSharpness * playerStress);
            float shaped = math.saturate(
                stress *
                stress *
                math.lerp(1f, TinnitusPlayerStressMaximumScale, exponentialStress));
            return sine * shaped * TinnitusMaximumGain;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float ApproximateOneMinusExpNegPositive(float x)
        {
            return math.saturate(1f - ApproximateExpNegPositive(x));
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float ApproximateExpNegPositive(float x)
        {
            float clamped = math.clamp(x, 0f, 8f);
            float x2 = clamped * clamped;
            float x3 = x2 * clamped;
            float numerator = 120f - (60f * clamped) + (12f * x2) - x3;
            float denominator = 120f + (60f * clamped) + (12f * x2) + x3;
            return math.saturate(numerator / math.max(denominator, 0.0001f));
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float ApproximatePressureScrubberHumDrive01(float pressureDrive)
        {
            float x = math.saturate(pressureDrive);
            float x2 = x * x;
            float numerator = 0.7616f + (1.43f * x) + (0.42f * x2);
            float denominator = 1f + (1.32f * x) + (0.29f * x2);
            return math.saturate(numerator / math.max(denominator, 0.0001f));
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float ApproximateMagnitudeNoSqrt(float3 value)
        {
            float3 absolute = math.abs(value);
            float max = math.cmax(absolute);
            float min = math.cmin(absolute);
            float mid = absolute.x + absolute.y + absolute.z - max - min;
            return max + (0.375f * mid) + (0.125f * min);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float ApproximateDistanceMetersFromSq(double distanceSq)
        {
            if (double.IsNaN(distanceSq) || double.IsInfinity(distanceSq))
                return float.PositiveInfinity;
            if (distanceSq <= 0d)
                return 0f;

            float clampedSq = (float)math.min(distanceSq, (double)float.MaxValue);
            uint estimateBits = (math.asuint(clampedSq) >> 1) + 0x1FC00000u;
            float estimate = math.asfloat(estimateBits);
            return 0.5f * (estimate + (clampedSq / math.max(estimate, 0.0001f)));
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float ApproximateThrusterEnvelope01(float cycle01, float sharpness)
        {
            float x = math.saturate(cycle01);
            float x2 = x * x;
            float x3 = x2 * x;
            float x5 = x3 * x2;
            float mid = x2 * (3f - (2f * x));
            float broad = x * (2f - x);
            float loadBlend = math.saturate((5f - sharpness) * (1f / 4.5f));
            return math.lerp(math.lerp(x5, mid, loadBlend), broad, loadBlend * loadBlend);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float RenderLeviathanGranularRoarSample(
            ref LeviathanGranularSynthesisState state,
            NativeArray<float> baseRoarClip,
            long sampleFrame,
            float aggroLevel,
            float pitchScale,
            double invSampleRate)
        {
            float aggro = math.saturate(aggroLevel);
            if (aggro <= 0.0001f || !baseRoarClip.IsCreated || baseRoarClip.Length <= 4)
            {
                state.Envelope = math.max(0f, state.Envelope - ((float)invSampleRate * LeviathanRoarAggroDecayPerSecond));
                return 0f;
            }

            state.Envelope = math.max(state.Envelope, aggro);
            if (state.Seed == 0u)
                state.Seed = 0xA615D351u;

            if (state.GrainDurationSeconds <= 0f || state.GrainAgeSeconds >= state.GrainDurationSeconds)
            {
                uint sampleFrameHash = sampleFrame > 0L ? (uint)sampleFrame : 0u;
                state.Seed = HashUInt(state.Seed ^ sampleFrameHash);
                float randA = Hash01(state.Seed ^ 0x28B7C91Du);
                float randB = Hash01(state.Seed ^ 0xC31E49B5u);
                float randC = Hash01(state.Seed ^ 0x71A2F935u);
                state.GrainDurationSeconds = math.lerp(
                    LeviathanRoarMaximumGrainSeconds,
                    LeviathanRoarMinimumGrainSeconds,
                    aggro) * math.lerp(0.75f, 1.25f, randA);
                state.GrainPitchRatio = math.lerp(0.42f, 1.12f, aggro) * math.lerp(0.88f, 1.18f, randB);
                state.GrainStartIndex = randC * (baseRoarClip.Length - 4);
                state.GrainAgeSeconds = 0f;
            }

            float dopplerPitch = math.clamp(pitchScale, LeviathanDopplerMinimumPitchScale, LeviathanDopplerMaximumPitchScale);
            double cursor = state.GrainStartIndex + (state.GrainAgeSeconds / math.max((float)invSampleRate, 0.000001f) * state.GrainPitchRatio * dopplerPitch);
            float grain = LinearSampleLoopWindow(baseRoarClip, 0, baseRoarClip.Length, cursor);
            float t = math.saturate(state.GrainAgeSeconds / math.max(state.GrainDurationSeconds, 0.0001f));
            float grainEnvelope = FastSine01(t * 0.5f);
            state.LowPassState = math.lerp(state.LowPassState, grain, math.lerp(0.025f, 0.16f, aggro));
            state.GrainAgeSeconds += (float)invSampleRate;
            state.Envelope = math.max(0f, state.Envelope - ((float)invSampleRate * LeviathanRoarAggroDecayPerSecond * 0.5f));
            float roar = (state.LowPassState * 0.74f + grain * 0.26f) * grainEnvelope * state.Envelope;
            return math.tanh(roar * 2.8f) * LeviathanRoarMaximumGain;
        }

        private float RenderInteriorFdnReverbSample(ref InteriorFdnReverbSynthesisState state, float input, float send01)
        {
            float send = math.saturate(send01);
            int writeA = state.WriteA & InteriorFdnLaneMask;
            int writeB = state.WriteB & InteriorFdnLaneMask;
            int writeC = state.WriteC & InteriorFdnLaneMask;
            int writeD = state.WriteD & InteriorFdnLaneMask;
            float a = _interiorFdnDelay[(0 * InteriorFdnLaneLength) + ((writeA - 431) & InteriorFdnLaneMask)];
            float b = _interiorFdnDelay[(1 * InteriorFdnLaneLength) + ((writeB - 653) & InteriorFdnLaneMask)];
            float c = _interiorFdnDelay[(2 * InteriorFdnLaneLength) + ((writeC - 947) & InteriorFdnLaneMask)];
            float d = _interiorFdnDelay[(3 * InteriorFdnLaneLength) + ((writeD - 1291) & InteriorFdnLaneMask)];

            state.DampingA = math.lerp(state.DampingA, a, 1f - InteriorFdnDamping);
            state.DampingB = math.lerp(state.DampingB, b, 1f - InteriorFdnDamping);
            state.DampingC = math.lerp(state.DampingC, c, 1f - InteriorFdnDamping);
            state.DampingD = math.lerp(state.DampingD, d, 1f - InteriorFdnDamping);

            float fdnInput = input * send;
            _interiorFdnDelay[(0 * InteriorFdnLaneLength) + writeA] =
                fdnInput + ((state.DampingB + state.DampingC - state.DampingD) * InteriorFdnFeedback);
            _interiorFdnDelay[(1 * InteriorFdnLaneLength) + writeB] =
                fdnInput + ((state.DampingA - state.DampingC + state.DampingD) * InteriorFdnFeedback);
            _interiorFdnDelay[(2 * InteriorFdnLaneLength) + writeC] =
                fdnInput + ((-state.DampingA + state.DampingB + state.DampingD) * InteriorFdnFeedback);
            _interiorFdnDelay[(3 * InteriorFdnLaneLength) + writeD] =
                fdnInput + ((state.DampingA + state.DampingB - state.DampingC) * InteriorFdnFeedback);

            state.WriteA = (writeA + 1) & InteriorFdnLaneMask;
            state.WriteB = (writeB + 1) & InteriorFdnLaneMask;
            state.WriteC = (writeC + 1) & InteriorFdnLaneMask;
            state.WriteD = (writeD + 1) & InteriorFdnLaneMask;

            float wet = (state.DampingA + state.DampingB + state.DampingC + state.DampingD) * 0.25f;
            return input + (wet * send * InteriorFdnWetGainMaximum);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float ResolveOnePoleTimeBlend(float timeConstantSeconds, double invSampleRate)
        {
            float deltaSeconds = math.max((float)invSampleRate, 0f);
            return ApproximateOneMinusExpNegPositive(deltaSeconds / math.max(timeConstantSeconds, 0.0001f));
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float ResolveOnePoleLowPassCoefficient(float cutoffHertz, int sampleRate)
        {
            float safeSampleRate = math.max(1f, sampleRate);
            float safeCutoff = math.clamp(cutoffHertz, 20f, safeSampleRate * 0.45f);
            return ApproximateOneMinusExpNegPositive(TwoPi * safeCutoff / safeSampleRate);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float ApplyOnePoleLowPass(float input, float previousOutput, float alpha)
        {
            return previousOutput + math.saturate(alpha) * (input - previousOutput);
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
            dampingAlpha = ApproximateExpNegPositive((TwoPi * dampingCutoff) / safeSampleRate);

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
            float decay = (3f * NaturalLogTen * delaySeconds) / math.max(rt60Seconds, 0.05f);
            float feedback = ApproximateExpNegPositive(decay);
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
            float depthDrive,
            float pressurePhaserDepth01,
            float panicAmbientDull01)
        {
            uint sampleIndex = (uint)math.max(0L, sampleFrame);
            float sampleTime = (float)(sampleFrame * invSampleRate);
            return RenderAmbientCurrentFmKernel(
                ref state,
                sampleIndex,
                sampleTime,
                invSampleRate,
                depthDrive,
                pressurePhaserDepth01,
                panicAmbientDull01,
                _sampleRate);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float RenderAmbientCurrentFmKernel(
            ref AmbientCurrentSynthesisState state,
            uint sampleIndex,
            float sampleTime,
            double invSampleRate,
            float depthDrive,
            float pressurePhaserDepth01,
            float panicAmbientDull01,
            int sampleRate)
        {
            float panicDull = math.saturate(panicAmbientDull01);
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
            float lowPassMinimum = math.lerp(AmbientCurrentLowPassMinimumHertz, 55f, panicDull);
            float lowPassMaximum = math.lerp(AmbientCurrentLowPassMaximumHertz, 145f, panicDull);
            float lowPassCutoff = math.clamp(
                math.lerp(lowPassMinimum, lowPassMaximum, cascade) +
                carrier * math.lerp(18f, 64f, cascade) * math.lerp(1f, 0.25f, panicDull),
                lowPassMinimum,
                lowPassMaximum);
            float filterCoefficient = math.min(
                0.99f,
                TwoPi * lowPassCutoff / math.max(sampleRate, 1f));
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
            float fmBed = carrier *
                          math.lerp(0.16f, 0.34f, cascade) *
                          math.lerp(1f, PanicHeartbeatAmbientHighCutMinimumGain, panicDull);
            float ambient = (filteredNoise * 0.78f + fmBed) *
                            math.lerp(0.3f, 1f, depthDrive) *
                            slowLfo *
                            AmbientCurrentMasterGain;
            return RenderDepthPressurePhaserSample(ref state, ambient, pressurePhaserDepth01, invSampleRate);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float RenderDepthPressurePhaserSample(
            ref AmbientCurrentSynthesisState state,
            float input,
            float depth01,
            double invSampleRate)
        {
            float pressureDrive = math.saturate(depth01);
            if (pressureDrive <= 0.0001f)
                return input;

            float lfo = 0.5f + 0.5f * AdvanceSine(
                ref state.PressurePhaserPhase,
                math.lerp(PressurePhaserSweepMinimumHertz, PressurePhaserSweepMaximumHertz, pressureDrive),
                invSampleRate);
            float coefficient = math.lerp(
                PressurePhaserCoefficientMinimum,
                PressurePhaserCoefficientMaximum,
                lfo);
            float feedbackInput = input + state.PressurePhaserFeedbackSample * PressurePhaserFeedback * pressureDrive;
            float stageA = ProcessPressureAllPass(feedbackInput, coefficient, ref state.PressurePhaserAllPassA);
            float stageB = ProcessPressureAllPass(stageA, coefficient, ref state.PressurePhaserAllPassB);
            float stageC = ProcessPressureAllPass(stageB, coefficient, ref state.PressurePhaserAllPassC);
            float stageD = ProcessPressureAllPass(stageC, coefficient, ref state.PressurePhaserAllPassD);
            state.PressurePhaserFeedbackSample = stageD;
            float wet = PressurePhaserWetMaximum * pressureDrive;
            return math.lerp(input, input * 0.72f + stageD * 1.15f, wet);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float ProcessPressureAllPass(float input, float coefficient, ref float state)
        {
            float delayed = state + BiquadDenormalBias;
            float output = -coefficient * input + delayed;
            state = input + coefficient * output;
            return output;
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
            float cosine = FastCosineRadians(omega);
            float sine = FastSineRadians(omega);
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

            if (_sonarEchoDelay.IsCreated)
                ClearScratchBuffer(_sonarEchoDelay, _sonarEchoDelay.Length);

            if (_sonarEchoReadCursors.IsCreated)
            {
                for (int i = 0; i < _sonarEchoReadCursors.Length; i++)
                    _sonarEchoReadCursors[i] = -1f;
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

        private void RebuildAcousticOcclusionLayerMask()
        {
            _resolvedAcousticOcclusionLayerMask = AcousticOcclusionUtility.BuildSensoryMask() & acousticOcclusionLayers.value;
        }

        private static float ResolveHullPressureDepth01(float depthMeters)
        {
            return math.saturate(math.max(0f, depthMeters) / PressureCreakDepthReferenceMeters);
        }

        private float ResolveAbsoluteDepthMeters()
        {
            if (playerMovement == null)
                return 0f;

            AbsoluteUniversePosition playerAup = playerMovement.CurrentAup;
            double absolutePlayerY = playerAup.ToAbsoluteDouble3().y;
            double absoluteSurfaceY = (double)playerMovement.CurrentWaterSurfaceY + HectonFloatingOrigin.CurrentTotalOffset.y;
            return (float)math.max(0d, absoluteSurfaceY - absolutePlayerY);
        }

        private void UpdateSurvivalTargets(float deltaTime)
        {
            float oxygenNormalized = _playerSurvivalSystem != null
                ? math.saturate(_playerSurvivalSystem.OxygenNormalized)
                : 1f;
            float nitrogenWarningRing = _playerSurvivalSystem != null
                ? math.saturate(_playerSurvivalSystem.NitrogenWarningRinging01)
                : 0f;
            float healthStress = _playerHealth != null ? math.saturate(_playerHealth.Stress) : 0f;
            float panicStress = ResolveAscendingNormalized01(
                healthStress,
                PanicHeartbeatStressThreshold01,
                1f);
            if (oxygenNormalized > HeartbeatBypassOxygenThreshold &&
                panicStress <= HullNoiseFloor &&
                nitrogenWarningRing <= HullNoiseFloor)
            {
                _heartbeatStressTickValue = 0f;
                _heartbeatOxygenDangerTickValue = 0f;
                _targetHeartbeatStressValue = 0f;
                _targetHeartbeatOxygenDangerValue = 0f;
                _targetHeartbeatActive = 0;
                _targetTinnitusOxygenStressValue = 0f;
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
                math.max(pressureStress, math.max(thermalStress, math.max(underwaterStress, math.max(fatalPressure, panicStress))))));
            float survivalBlendT = ApproximateOneMinusExpNegPositive(6f * math.max(0f, deltaTime));
            _heartbeatStressTickValue = math.lerp(_heartbeatStressTickValue, stressTarget, survivalBlendT);
            _heartbeatOxygenDangerTickValue = math.lerp(_heartbeatOxygenDangerTickValue, oxygenDanger, survivalBlendT);
            _targetHeartbeatStressValue = _heartbeatStressTickValue;
            _targetHeartbeatOxygenDangerValue = _heartbeatOxygenDangerTickValue;
            float oxygenTinnitus = ResolveDescendingNormalized01(
                oxygenNormalized,
                TinnitusOxygenThreshold,
                0f);
            _targetTinnitusOxygenStressValue = math.saturate(math.max(
                oxygenTinnitus,
                nitrogenWarningRing * NitrogenWarningTinnitusGainScale));
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
                UpdateBoilingWaterLoop(false, 0f);
                _targetBubbleBoilIntensity = 0f;
                return;
            }

            float loopIntensity = math.saturate(math.max(_laserCutterHeat01, BubbleBoilMinimumHeatFloor));
            UpdateBoilingWaterLoop(true, loopIntensity);
            _targetBubbleBoilIntensity = ResolveAscendingNormalized01(
                _laserCutterHeat01,
                ToolCavitationHeatStart01,
                1f);
        }

        private void UpdateHullGroanLoop(bool shouldPlay, float stress01)
        {
            AudioSource source = hullGroanLoopSource;
            if (source == null)
                return;

            if (hullGroanLoopClip != null && source.clip != hullGroanLoopClip)
                source.clip = hullGroanLoopClip;

            source.loop = true;
            source.spatialBlend = 0f;
            float stress = math.saturate(stress01);
            if (!shouldPlay || stress <= HullNoiseFloor)
            {
                source.volume = 0f;
                if (source.isPlaying)
                    source.Stop();
                return;
            }

            if (!source.isPlaying)
                source.Play();

            float shapedStress = stress * stress * (3f - 2f * stress);
            source.volume = shapedStress * math.saturate(hullGroanLoopMaximumVolume);
            float now = Time.unscaledTime;
            if (now >= _hullGroanLoopNextPitchUpdateTime)
            {
                _hullGroanLoopPitchSeed = HashUInt(_hullGroanLoopPitchSeed ^ (uint)(now * 2048f) ^ 0x4E7B9A11u);
                float randomTilt = math.lerp(0.96f, 1.04f, Hash01(_hullGroanLoopPitchSeed));
                _hullGroanLoopPitch = math.clamp(
                    math.lerp(HullGroanLoopPitchMinimum, HullGroanLoopPitchMaximum, stress) * randomTilt,
                    HullGroanLoopPitchMinimum,
                    HullGroanLoopPitchMaximum);
                _hullGroanLoopNextPitchUpdateTime = now + HullGroanLoopPitchUpdateIntervalSeconds;
            }

            source.pitch = _hullGroanLoopPitch;
        }

        private void UpdateBoilingWaterLoop(bool shouldPlay, float intensity)
        {
            if (UpdateBoilingWaterSamplePool(shouldPlay, intensity))
            {
                AudioSource fallback = boilingWaterLoopSource;
                if (fallback != null)
                {
                    fallback.volume = 0f;
                    if (fallback.isPlaying)
                        fallback.Stop();
                }

                return;
            }

            AudioSource source = boilingWaterLoopSource;
            if (source == null)
                return;

            if (boilingWaterLoopClip != null && source.clip != boilingWaterLoopClip)
                source.clip = boilingWaterLoopClip;

            source.loop = true;
            if (!shouldPlay || intensity <= HullNoiseFloor)
            {
                source.volume = 0f;
                if (source.isPlaying)
                    source.Stop();
                return;
            }

            if (!source.isPlaying)
                source.Play();

            source.volume = math.saturate(intensity) * math.saturate(boilingWaterLoopMaximumVolume);
            float now = Time.unscaledTime;
            if (now >= _boilingLoopNextPitchUpdateTime)
            {
                _boilingLoopPitchSeed = HashUInt(_boilingLoopPitchSeed ^ (uint)(now * 4096f) ^ 0x6B4F1D2Du);
                float randomPitch = math.lerp(
                    BoilingWaterLoopPitchMinimum,
                    BoilingWaterLoopPitchMaximum,
                    Hash01(_boilingLoopPitchSeed));
                float heatTilt = math.lerp(0.92f, 1.08f, math.saturate(intensity));
                _boilingLoopPitch = math.clamp(
                    randomPitch * heatTilt,
                    BoilingWaterLoopPitchMinimum,
                    BoilingWaterLoopPitchMaximum);
                _boilingLoopNextPitchUpdateTime = now + BoilingWaterLoopPitchUpdateIntervalSeconds;
            }

            source.pitch = _boilingLoopPitch;
        }

        private bool UpdateBoilingWaterSamplePool(bool shouldPlay, float intensity)
        {
            if (boilingWaterPoolSources == null || boilingWaterPoolSources.Length <= 0)
                return false;

            int sourceLimit = math.min(BoilingWaterSamplePoolCapacity, boilingWaterPoolSources.Length);
            int activeSourceCount = 0;
            for (int i = 0; i < sourceLimit; i++)
            {
                if (boilingWaterPoolSources[i] != null)
                    activeSourceCount++;
            }

            if (activeSourceCount <= 0)
                return false;

            float clampedIntensity = math.saturate(intensity);
            int clipCount = boilingWaterPoolClips != null ? boilingWaterPoolClips.Length : 0;
            float perSourceMaximumVolume = math.saturate(boilingWaterLoopMaximumVolume) / activeSourceCount;
            float now = Time.unscaledTime;
            bool shouldRefreshPitch = now >= _boilingLoopNextPitchUpdateTime;
            if (shouldRefreshPitch)
            {
                _boilingLoopPitchSeed = HashUInt(_boilingLoopPitchSeed ^ (uint)(now * 4096f) ^ 0x6B4F1D2Du);
                _boilingLoopNextPitchUpdateTime = now + BoilingWaterLoopPitchUpdateIntervalSeconds;
            }

            for (int i = 0; i < sourceLimit; i++)
            {
                AudioSource source = boilingWaterPoolSources[i];
                if (source == null)
                    continue;

                if (clipCount > 0)
                {
                    AudioClip clip = boilingWaterPoolClips[i < clipCount ? i : i % clipCount];
                    if (clip != null && source.clip != clip)
                        source.clip = clip;
                }

                source.loop = true;
                source.spatialBlend = 0f;
                if (!shouldPlay || clampedIntensity <= HullNoiseFloor)
                {
                    source.volume = 0f;
                    if (source.isPlaying)
                        source.Stop();
                    continue;
                }

                if (!source.isPlaying)
                    source.Play();

                uint seed = HashUInt(_boilingLoopPitchSeed ^ ((uint)i * 0x85EBCA6Bu));
                float randomPitch = math.lerp(
                    BoilingWaterLoopPitchMinimum,
                    BoilingWaterLoopPitchMaximum,
                    Hash01(seed ^ 0xC2B2AE35u));
                float heatTilt = math.lerp(0.92f, 1.08f, clampedIntensity);
                source.pitch = math.clamp(
                    randomPitch * heatTilt,
                    BoilingWaterLoopPitchMinimum,
                    BoilingWaterLoopPitchMaximum);
                source.panStereo = math.lerp(-0.85f, 0.85f, Hash01(seed ^ 0x27D4EB2Fu));
                source.volume = clampedIntensity *
                                perSourceMaximumVolume *
                                math.lerp(0.72f, 1.08f, Hash01(seed ^ 0x165667B1u));
            }

            return true;
        }

        private void UpdateBinauralTargets()
        {
            if (!(Hecton8.Core.GlobalRegistry.Audio is SpatialAudioManager audioManager) ||
                !audioManager.TryGetDominantBinauralEmitter(out SpatialAudioManager.BinauralEmitterTelemetry telemetry))
            {
                _targetBinauralAzimuthRadians = 0f;
                _targetBinauralRightDot = 0f;
                _targetBinauralItdSeconds = 0f;
                _targetBinauralShadowAmount01 = 0f;
                _targetBinauralShadowCutoffHertz = _sampleRate * 0.45f;
                _targetBinauralEnergy01 = 0f;
                _targetBinauralWaterDensityMul = 0f;
                _targetBinauralValid = 0;
                return;
            }

            _targetBinauralAzimuthRadians = telemetry.AzimuthRadians;
            _targetBinauralRightDot = telemetry.RightDot;
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
            float pitchJitter = ResolveImpactPitchJitter(unchecked((uint)Time.frameCount ^ ((uint)Volatile.Read(ref _impactEventWriteIndex) * 0x9E3779B9u)));
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
                EchoPitchScale = math.clamp(echoPitchScale * pitchJitter, 0.65f, 1.45f)
            };

            for (int attempt = 0; attempt < ImpactEventQueueEnqueueAttemptLimit; attempt++)
            {
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

                    Interlocked.Increment(ref _impactEventQueueDropCount);
                }

                _impactEventQueue[writeIndex] = impactAudioEvent;
                Interlocked.Exchange(ref _impactEventWriteIndex, nextWriteIndex);
                SignalAudioProducerThread();
                return true;
            }

            Interlocked.Increment(ref _impactEventQueueDropCount);
            return false;
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
            float pressureHumDriveTarget,
            float pressureHumGainTarget,
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
                float metallicDrive = math.lerp(1f, 1.65f, metallicImpulse);
                float rivetAmount = hullRivetBurstAmount * math.lerp(1f, 2.35f, metallicImpulse);
                long sampleFrame = blockStartFrame + frameIndex;
                uint sampleIndex = (uint)math.max(0L, sampleFrame);

                float pressureLfo = 0.6f + 0.4f * AdvanceSine(ref state.PressureLfoPhase, 0.3d, invSampleRate);
                float stress01 = math.saturate(stress);
                float pressureStressDrive = stress01 * (2f - stress01);
                float pressureBed =
                    (LayeredBrownLike(sampleIndex) * pressureLfo * hullPressureBedAmount) * pressureStressDrive;

                float pressureCreak = RenderPressureCreakSample(ref state, sampleIndex, stress, structuralStressVelocity, depthParam, invSampleRate);
                float granularMetal = RenderStructuralGranularSample(
                    ref state,
                    _metallicGrainBank,
                    sampleIndex) * metallicDrive;
                float fatigueRing = RenderStructuralFatigueRingSample(ref state, sampleIndex, structuralFatigue, structuralStress, invSampleRate);
                float structuralSnapTransient = RenderStructuralSnapTransientSample(
                    ref state,
                    sampleIndex,
                    structuralSnap,
                    previousStructuralSnap,
                    structuralStress,
                    invSampleRate);
                float impactClang = RenderImpactClangSampleInternal(ref state, sampleIndex, invSampleRate);
                float subBass = RenderHullSubBassSample(ref state, structuralStress, depthParam, absoluteDepthMeters, enclosureDensityIndex, invSampleRate);
                float pressureScrubberHum = RenderPressureScrubberHumSample(ref state, pressureHumDriveTarget, pressureHumGainTarget, invSampleRate);
                float rivetBurst = BuildRivetBurst(sampleIndex, math.max(stress, metallicImpulse), rivetAmount);
                float combined = pressureBed + pressureCreak + granularMetal + fatigueRing + structuralSnapTransient + impactClang + rivetBurst + subBass + pressureScrubberHum;
                combined = ApplyDepthHullDistortion(combined, depthParam, structuralStress);
                _hullScratch[frameIndex] = math.max(stress, structuralSnap) <= HullNoiseFloor
                    ? 0f
                    : FastSoftClip(combined * math.lerp(1.7f, 2.8f, metallicImpulse)) * hullMasterGain;
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

        private static void ClearScratchBufferCold(NativeArray<float> buffer, int frameCount)
        {
            if (!buffer.IsCreated || frameCount <= 0)
                return;

            int safeCount = math.min(frameCount, buffer.Length);
            if (safeCount >= ColdBurstClearMinimumCount)
            {
                PlayerCriticalBufferJobs.Clear(buffer, safeCount);
                return;
            }

            ClearScratchBuffer(buffer, safeCount);
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
            float rightDot = hasDirectionalTarget ? math.clamp(parameters.BinauralRightDot, -1f, 1f) : 0f;
            float shadowAmount = hasDirectionalTarget ? math.saturate(parameters.BinauralShadowAmount01) : 0f;
            float waterDensityMul = hasDirectionalTarget ? math.saturate(parameters.BinauralWaterDensityMul) : 0f;
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
            float maxDelaySamples = math.min(BinauralMaximumDelaySamples, BinauralMaximumMicroDelaySeconds * math.max(_sampleRate, 1));
            int delaySamples = hasDirectionalTarget
                ? math.clamp((int)(math.abs(rightDot) * maxDelaySamples + 0.5f), 0, BinauralMaximumDelaySamples)
                : 0;
            int delayLeftSamples = rightDot > 0f ? delaySamples : 0;
            int delayRightSamples = rightDot < 0f ? delaySamples : 0;
            float shadowAlpha = ApproximateExpNegPositive(
                (TwoPi * math.max(400f, shadowCutoffHertz)) /
                math.max(_sampleRate, 1f));

            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                float mono = _mixScratch[frameIndex];
                int stereoIndex = frameIndex << 1;
                float sonarLeftDelta = _stereoMixScratch[stereoIndex];
                float sonarRightDelta = _stereoMixScratch[stereoIndex + 1];
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
                    if (rightDot > 0f)
                    {
                        leftSpatial = ApplyBinauralShadowEar(delayedLeft * contraGain, 0, shadowAlpha);
                        rightSpatial = delayedRight;
                    }
                    else if (rightDot < 0f)
                    {
                        leftSpatial = delayedLeft;
                        rightSpatial = ApplyBinauralShadowEar(delayedRight * contraGain, 1, shadowAlpha);
                    }
                }

                float left = math.lerp(mono, leftSpatial, binauralMix) + sonarLeftDelta;
                float right = math.lerp(mono, rightSpatial, binauralMix) + sonarRightDelta;
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
                ClearSonarStereoDelta(frameCount);
                return;
            }

            SonarSynthesisState state = _sonarSynthesisState;
            if (state.ActiveSequence != activeState.Sequence)
            {
                ResetSonarPhaseState(activeState.Sequence);
                state = _sonarSynthesisState;
            }

            NativeArray<SonarEchoTap> activeTapBuffer = _workerSonarEchoTaps;
            int activeTapCount = activeTapBuffer.IsCreated
                ? math.clamp(_workerActiveSonarTapCount, 0, math.min(SonarEchoTapCapacity, activeTapBuffer.Length))
                : 0;
            long maxActiveFrame = activeState.StartFrame + (long)math.ceil(SonarTotalDurationSeconds * math.max(_sampleRate, 1));
            float dopplerFrameDelta = frameCount > 1 ? 1f / (frameCount - 1) : 1f;
            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                long sampleFrame = blockStartFrame + frameIndex;
                float dopplerFrameT = math.saturate(frameIndex * dopplerFrameDelta);
                float age = (float)((sampleFrame - activeState.StartFrame) * invSampleRate);
                if (age < 0f || age > SonarTotalDurationSeconds)
                {
                    _sonarScratch[frameIndex] = 0f;
                    StoreSonarStereoDelta(frameIndex, 0f, 0f);
                    continue;
                }

                uint sampleIndex = (uint)math.max(0L, sampleFrame);
                float attack = 0f;
                if (age < 0.03f)
                {
                    float attackEnv = ApproximateExpNegPositive(age * 220f);
                    float attackNoise = HashSigned(sampleIndex ^ 0x3941AA1u);
                    attack = attackEnv * (AdvanceSine(ref state.AttackPhase, 4500d, invSampleRate) + attackNoise * 0.85f) * sonarAttackBlend;
                }

                float chirp = 0f;
                if (age < SonarChirpDurationSeconds)
                {
                    float chirpT = math.saturate(age / SonarChirpDurationSeconds);
                    float chirpFrequency = math.lerp(2000f, 400f, chirpT);
                    float chirpEnv = ApproximateExpNegPositive(age * 5f);
                    chirp = chirpEnv * AdvanceSine(ref state.ChirpPhase, chirpFrequency, invSampleRate);
                }

                float drySignal = attack + chirp;
                if (_sonarEchoDelay.IsCreated)
                {
                    _sonarEchoDelay[state.EchoWriteIndex] = drySignal;
                    state.EchoWriteIndex = (state.EchoWriteIndex + 1) & SonarEchoDelayMask;
                }

                float echo = 0f;
                float echoLeftDelta = 0f;
                float echoRightDelta = 0f;
                for (int tapIndex = 0; tapIndex < activeTapCount; tapIndex++)
                {
                    SonarEchoTap tap = activeTapBuffer[tapIndex];
                    float echoAge = age - tap.DelaySeconds;
                    if (echoAge < 0f)
                    {
                        if (_sonarEchoReadCursors.IsCreated)
                            _sonarEchoReadCursors[tapIndex] = -1f;
                        continue;
                    }

                    if (echoAge >= SonarChirpDurationSeconds || !_sonarEchoDelay.IsCreated || !_sonarEchoReadCursors.IsCreated)
                        continue;

                    int echoDelaySamples = tap.DelaySamples;
                    if (echoDelaySamples <= 0)
                        continue;

                    float echoReadCursor = _sonarEchoReadCursors[tapIndex];
                    if (echoReadCursor < 0f)
                        echoReadCursor = (state.EchoWriteIndex - echoDelaySamples) & SonarEchoDelayMask;

                    float dopplerRatio = math.clamp(
                        math.lerp(tap.PreviousDopplerRatio, tap.DopplerRatio, dopplerFrameT),
                        SonarEchoMinimumDopplerRatio,
                        SonarEchoMaximumDopplerRatio);
                    float tapEcho = LinearSampleRing(_sonarEchoDelay, echoReadCursor, SonarEchoDelayMask) *
                                    (ApproximateExpNegPositive(echoAge * 4.5f) * tap.Attenuation);
                    echoReadCursor = WrapRingCursor(echoReadCursor + dopplerRatio, SonarEchoDelayCapacity);
                    _sonarEchoReadCursors[tapIndex] = echoReadCursor;

                    if (_sonarEchoFilterInput1.IsCreated &&
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
                        tapEcho = math.lerp(tapEcho, filteredEcho, tap.UseLowPass);
                    }

                    echo += tapEcho;
                    echoLeftDelta += tapEcho * tap.LeftPanDeltaGain;
                    echoRightDelta += tapEcho * tap.RightPanDeltaGain;
                }

                float tail = 0f;
                if (age >= 0.08f)
                {
                    float tailAge = age - 0.08f;
                    float tailEnv = math.saturate(tailAge / 0.24f) * ApproximateExpNegPositive(tailAge * 0.95f);
                    float slowLfo = 0.55f + 0.45f * AdvanceSine(ref state.TailSlowPhase, 0.38d, invSampleRate);
                    float beat =
                        AdvanceSine(ref state.TailBeatAPhase, 150d, invSampleRate) +
                        AdvanceSine(ref state.TailBeatBPhase, 147d, invSampleRate) * 0.6f +
                        AdvanceSine(ref state.TailBeatCPhase, 300d, invSampleRate) * 0.4f;
                    float pinkTail = LayeredPinkLike(sampleIndex) * slowLfo;
                    tail = tailEnv * ((beat * 0.46f) + (pinkTail * 0.54f)) * sonarTailBlend;
                }

                float mixed = (attack + chirp + echo + tail) * activeState.Intensity;
                _sonarScratch[frameIndex] = FastSoftClip(mixed * sonarSaturationDrive) * sonarMasterGain;
                StoreSonarStereoDelta(
                    frameIndex,
                    echoLeftDelta * activeState.Intensity * sonarMasterGain,
                    echoRightDelta * activeState.Intensity * sonarMasterGain);
            }

            if (blockStartFrame >= maxActiveFrame)
                _workerActiveSonarState = default;

            if (activeTapBuffer.IsCreated)
            {
                for (int tapIndex = 0; tapIndex < activeTapCount; tapIndex++)
                {
                    SonarEchoTap tap = activeTapBuffer[tapIndex];
                    tap.PreviousDopplerRatio = tap.DopplerRatio;
                    activeTapBuffer[tapIndex] = tap;
                }
            }

            _sonarSynthesisState = state;
        }

        private void ClearSonarStereoDelta(int frameCount)
        {
            if (!_stereoMixScratch.IsCreated)
                return;

            int safeCount = math.min(frameCount * BinauralOutputChannels, _stereoMixScratch.Length);
            for (int i = 0; i < safeCount; i++)
                _stereoMixScratch[i] = 0f;
        }

        private void StoreSonarStereoDelta(int frameIndex, float leftDelta, float rightDelta)
        {
            if (!_stereoMixScratch.IsCreated)
                return;

            int stereoIndex = frameIndex << 1;
            if (stereoIndex + 1 >= _stereoMixScratch.Length)
                return;

            _stereoMixScratch[stereoIndex] = leftDelta;
            _stereoMixScratch[stereoIndex + 1] = rightDelta;
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
            float lowPassAlpha = ApproximateExpNegPositive((TwoPi * lowPassCutoff) / math.max(_sampleRate, 1f));
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
                float envelope = ApproximateExpNegPositive(age * decayRate);
                float tonal =
                    AdvanceSine(ref state.CarrierPhaseA, ImpactEchoCarrierPrimaryHertz * pitchScale, invSampleRate) * 0.62f +
                    AdvanceSine(ref state.CarrierPhaseB, ImpactEchoCarrierSecondaryHertz * pitchScale, invSampleRate) * 0.38f;
                float noise = LayeredPinkLike(sampleIndex) * ImpactEchoNoiseBlend;
                float raw = (tonal + noise) * envelope * state.Excitation * state.Attenuation;
                float filtered = raw + lowPassAlpha * ((state.LowPassState + BiquadDenormalBias) - raw);
                state.LowPassState = filtered;
                _impactEchoScratch[frameIndex] = FastSoftClip(filtered * 2.2f) * 0.35f;
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
                (int)(_sampleRate / math.max(24f, ImpactClangFundamentalHertz * pitchScale) + 0.5f),
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

            float output = FastSoftClip(
                state.ImpactClangLowPassState *
                (1.55f + state.ImpactClangEnvelope * 2.4f)) *
                state.ImpactClangEnvelope *
                0.42f;
            state.ImpactClangEnvelope *= ApproximateExpNegPositive(ImpactClangEnvelopeDecayPerSecond * (float)invSampleRate);
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
            float thrusterRpmTarget,
            float thrusterPitchTarget,
            float thrusterPressureTarget,
            float thrusterAccelerationTarget,
            float thrusterHeavyCarryTarget,
            float thrusterDiveTarget,
            float vehicleCavitationSpeedTarget)
        {
            ThrusterSynthesisState state = _thrusterSynthesisState;
            float blendStart = _audioThrusterBlendValue;
            float loadStart = _audioThrusterLoadValue;
            float rpmStart = _audioThrusterRpmValue;
            float pitchStart = _audioThrusterPitchValue;
            float pressureStart = _audioThrusterPressureValue;
            float accelerationStart = _audioThrusterAccelerationValue;
            float heavyCarryStart = _audioThrusterHeavyCarryValue;
            float diveStart = _audioThrusterDiveValue;
            float vehicleCavitationStart = _audioVehicleCavitationSpeed01;
            float blockLoad = (loadStart + thrusterLoadTarget) * 0.5f;
            float blockRpm = (rpmStart + thrusterRpmTarget) * 0.5f;
            float blockAcceleration = (accelerationStart + thrusterAccelerationTarget) * 0.5f;
            float blockThrottle = math.saturate(blockLoad * 0.62f + blockRpm * 0.28f + blockAcceleration * 0.1f);
            float blockBandPassCenter = math.lerp(200f, 1200f, blockThrottle);
            ComputeBandPassCoefficients(
                blockBandPassCenter,
                ThrusterBandPassQ,
                _sampleRate,
                out float bpB0,
                out float bpB1,
                out float bpB2,
                out float bpA1,
                out float bpA2);
            float blockBladePassHz = math.lerp(
                ThrusterBladePassFrequencyMinHertz,
                ThrusterBladePassFrequencyMaxHertz,
                math.saturate(blockRpm * 0.86f + blockThrottle * 0.14f));
            int bladeDelaySamples = math.clamp(
                (int)(_sampleRate / math.max(1f, blockBladePassHz) + 0.5f),
                1,
                ThrusterCombDelayCapacity - 1);

            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                float frameT = frameCount > 1 ? frameIndex / (float)(frameCount - 1) : 0f;
                float blend = math.lerp(blendStart, thrusterBlendTarget, frameT);
                float load = math.lerp(loadStart, thrusterLoadTarget, frameT);
                float rpm = math.lerp(rpmStart, thrusterRpmTarget, frameT);
                float pitchScale = math.lerp(pitchStart, thrusterPitchTarget, frameT);
                float pressure = math.lerp(pressureStart, thrusterPressureTarget, frameT);
                float acceleration = math.lerp(accelerationStart, thrusterAccelerationTarget, frameT);
                float heavyCarry = math.lerp(heavyCarryStart, thrusterHeavyCarryTarget, frameT);
                float dive = math.lerp(diveStart, thrusterDiveTarget, frameT);
                float vehicleCavitationSpeed = math.lerp(vehicleCavitationStart, vehicleCavitationSpeedTarget, frameT);
                float throttle = math.saturate(load * 0.62f + rpm * 0.28f + acceleration * 0.1f);
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
                float dynamicEnvelope = ApproximateThrusterEnvelope01(propCycle, envelopeSharpness);
                float highNoise = HighBandNoise(sampleIndex);
                float cavitation = math.saturate(highNoise * highNoise * highNoise);
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
                FastSineRadians((float)(TwoPi * state.CavitationCarrierPhase) + highNoise * 0.6f) *
                dynamicEnvelope *
                math.saturate(acceleration * 0.82f + pressure * 0.35f + dive * 0.12f);
                float rawScreechNoise = HighBandNoise(sampleIndex ^ 0xDA7A51C3u);
                float highPassScreech =
                    VehicleCavitationHighPassAlpha *
                    (state.VehicleCavitationHighPassOutput + rawScreechNoise - state.VehicleCavitationHighPassInput);
                state.VehicleCavitationHighPassInput = rawScreechNoise;
                state.VehicleCavitationHighPassOutput = highPassScreech;
                AdvancePhase(
                    ref state.VehicleCavitationScreechPhase,
                    math.lerp(2600f, 7200f, vehicleCavitationSpeed),
                    invSampleRate);
                float cavitationScreech =
                    (highPassScreech * 0.62f +
                     FastSineRadians((float)(TwoPi * state.VehicleCavitationScreechPhase) + highPassScreech * 0.8f) * 0.38f) *
                    vehicleCavitationSpeed *
                    VehicleCavitationScreechGain;

                float mixed = hum + flow + (cavitation * 0.56f + cavitationFm * 0.44f) * 0.78f + cavitationScreech;
                float rpmGain = math.lerp(0.82f, 1.18f, math.saturate(rpm));
                _thrusterScratch[frameIndex] = FastSoftClip(mixed * 2.0f) * thrusterMasterGain * blend * rpmGain;
            }

            _thrusterSynthesisState = state;
            _audioThrusterBlendValue = thrusterBlendTarget;
            _audioThrusterLoadValue = thrusterLoadTarget;
            _audioThrusterRpmValue = thrusterRpmTarget;
            _audioThrusterPitchValue = thrusterPitchTarget;
            _audioThrusterPressureValue = thrusterPressureTarget;
            _audioThrusterAccelerationValue = thrusterAccelerationTarget;
            _audioThrusterHeavyCarryValue = thrusterHeavyCarryTarget;
            _audioThrusterDiveValue = thrusterDiveTarget;
            _audioVehicleCavitationSpeed01 = vehicleCavitationSpeedTarget;
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
            float decay = ApproximateExpNegPositive(0.07f * blockOffset);
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
            return FastSoftClip(filtered * envelope * state.GrainGain * 2.1f);
        }

        private static void StartPressureCreakGrain(
            ref HullSynthesisState state,
            uint sampleIndex,
            float stress,
            float depthParam,
            float stressDerivative,
            double invSampleRate)
        {
            int sampleRate = math.max(1, (int)(1d / invSampleRate + 0.5d));
            int attackSamples = math.max(1, (int)(PressureCreakAttackSeconds * sampleRate + 0.5f));
            int decaySamples = math.max(1, (int)(PressureCreakDecaySeconds * sampleRate + 0.5f));
            int sustainSamples = math.max(1, (int)(math.lerp(PressureCreakSustainSeconds, PressureCreakSustainSeconds * 1.65f, stress) * sampleRate + 0.5f));
            int releaseSamples = math.max(1, (int)(PressureCreakReleaseSeconds * sampleRate + 0.5f));
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
                sampleRate,
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
            float sample = LinearSampleLoopWindow(grainBank, state.GrainLoopStartIndex, state.GrainLoopLength, state.GrainReadCursor);
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
            return FastSoftClip(filtered * envelope * state.GrainGain * math.lerp(1.6f, 3.1f, math.saturate(state.GrainDerivative)));
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
            state.GrainLoopLength = math.max(96, (int)(math.lerp(112f, 640f, lengthHash) + 0.5f));
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
        private static float RenderPressureScrubberHumSample(
            ref HullSynthesisState state,
            float cachedHarmonicGain,
            float outputGain,
            double invSampleRate)
        {
            float gain = math.saturate(outputGain);
            float harmonicGain = math.saturate(cachedHarmonicGain);
            if (gain <= HullNoiseFloor || harmonicGain <= HullNoiseFloor)
                return 0f;

            float fundamental = AdvanceSine(ref state.PressureScrubberHumPhase, PressureScrubberHumFrequencyHertz, invSampleRate);
            float second = AdvanceSine(ref state.PressureScrubberHarmonicPhase, PressureScrubberHumFrequencyHertz * 2f, invSampleRate) * (0.18f + harmonicGain * 0.2f);
            float third = AdvanceSine(ref state.PressureScrubberSaturationPhase, PressureScrubberHumFrequencyHertz * 3f, invSampleRate) * (0.05f + harmonicGain * 0.13f);
            float cachedDrive = math.lerp(0.62f, 1.28f, harmonicGain);
            return (fundamental + second + third) * cachedDrive * gain;
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
            float sine = FastSine01((float)state.StructuralSnapPhase);
            float harmonic = FastSine01((float)(state.StructuralSnapPhase * 1.82d)) * 0.34f;
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
            float ring = FastSine01((float)state.FatigueRingCarrierPhase);
            float harmonic = FastSine01((float)(state.FatigueRingCarrierPhase * 1.97d)) * 0.38f;
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
            float distorted = FastSoftClip(sample * drive);
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

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float ResolveAscendingNormalized01(float value, float start, float end)
        {
            if (value <= start)
                return 0f;

            if (value >= end)
                return 1f;

            return math.saturate((value - start) / math.max(end - start, 0.0001f));
        }

        private static float AdvanceSine(ref double phase, double frequencyHz, double invSampleRate)
        {
            return FastSine01((float)AdvancePhase(ref phase, frequencyHz, invSampleRate));
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float FastSineRadians(float radians)
        {
            return FastSine01(radians * InvTwoPi);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float FastCosineRadians(float radians)
        {
            return FastSine01(radians * InvTwoPi + 0.25f);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float FastSine01(float phase01)
        {
            float phase = phase01 - math.floor(phase01);
            float centered = phase > 0.5f ? phase - 1f : phase;
            float wave = (4f * centered) - (8f * centered * math.abs(centered));
            return wave + 0.225f * ((wave * math.abs(wave)) - wave);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float FastSoftClip(float value)
        {
            float square = value * value;
            return math.clamp(value * (27f + square) / (27f + 9f * square), -1f, 1f);
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
            float sine = FastSineRadians(omega);
            float cosine = FastCosineRadians(omega);
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

        private static float LinearSampleLoopWindow(
            NativeArray<float> buffer,
            int loopStartIndex,
            int loopLength,
            double cursor)
        {
            if (!buffer.IsCreated || buffer.Length <= 0 || loopLength <= 0)
                return 0f;

            double wrapped = cursor - math.floor(cursor / loopLength) * loopLength;
            if (wrapped < 0d)
                wrapped += loopLength;

            int baseIndex = (int)wrapped;
            float t = (float)(wrapped - baseIndex);
            float x0 = buffer[WrapLoopIndex(loopStartIndex, loopLength, baseIndex)];
            float x1 = buffer[WrapLoopIndex(loopStartIndex, loopLength, baseIndex + 1)];
            return math.lerp(x0, x1, t);
        }

        private static int WrapLoopIndex(int loopStartIndex, int loopLength, int index)
        {
            int wrapped = index % loopLength;
            if (wrapped < 0)
                wrapped += loopLength;

            return (loopStartIndex + wrapped) & MetallicGrainBankMask;
        }

        private static float LinearSampleRing(NativeArray<float> buffer, float cursor, int mask)
        {
            int capacity = mask + 1;
            if (!buffer.IsCreated || capacity <= 0)
                return 0f;

            if (cursor < 0f)
                cursor = 0f;
            else if (cursor >= capacity)
                cursor -= capacity;

            int baseIndex = (int)cursor;
            float t = cursor - baseIndex;
            float x0 = buffer[baseIndex & mask];
            float x1 = buffer[(baseIndex + 1) & mask];
            return math.lerp(x0, x1, t);
        }

        private static float WrapRingCursor(float cursor, int capacity)
        {
            if (capacity <= 0 || float.IsNaN(cursor) || float.IsInfinity(cursor))
                return 0f;

            if (cursor >= capacity)
                cursor -= capacity;
            else if (cursor < 0f)
                cursor += capacity;
            return cursor;
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
                PressureScrubberHumDrive = _targetPressureScrubberHumDrive,
                PressureScrubberHumGain = _targetPressureScrubberHumGain,
                ReverbRt60Seconds = _targetReverbRt60Seconds,
                ReverbWetMix = _targetReverbWetMix,
                ReverbOpenness = _targetReverbOpenness,
                BubbleBoilIntensity = _targetBubbleBoilIntensity,
                ThrusterBlend = _targetThrusterBlendValue,
                ThrusterLoad = _targetThrusterLoadValue,
                ThrusterRpm = _targetThrusterRpmValue,
                ThrusterPitch = _targetThrusterPitchValue,
                ThrusterPressure = _targetThrusterPressureValue,
                ThrusterAcceleration = _targetThrusterAccelerationValue,
                ThrusterHeavyCarry = _targetThrusterHeavyCarryValue,
                ThrusterDive = _targetThrusterDiveValue,
                VehicleCavitationSpeed01 = _targetVehicleCavitationSpeed01,
                AbyssalLowPassMix = _targetAbyssalLowPassMix,
                HeartbeatStress = _targetHeartbeatStressValue,
                HeartbeatOxygenDanger = _targetHeartbeatOxygenDangerValue,
                TinnitusOxygenStress = _targetTinnitusOxygenStressValue,
                LeviathanRoarAggro = _targetLeviathanRoarAggroValue,
                LeviathanRoarPitchScale = _targetLeviathanRoarPitchScale,
                HeartbeatActive = _targetHeartbeatActive,
                BinauralAzimuthRadians = _targetBinauralAzimuthRadians,
                BinauralRightDot = _targetBinauralRightDot,
                BinauralItdSeconds = _targetBinauralItdSeconds,
                BinauralShadowAmount01 = _targetBinauralShadowAmount01,
                BinauralShadowCutoffHertz = _targetBinauralShadowCutoffHertz,
                BinauralEnergy01 = _targetBinauralEnergy01,
                BinauralWaterDensityMul = _targetBinauralWaterDensityMul,
                BinauralValid = _targetBinauralValid
            };

            int inactiveIndex = Volatile.Read(ref _audioParameterSnapshotReadIndex) ^ 1;
            if (inactiveIndex == 0)
                _audioParameterSnapshotA.Value = snapshot;
            else
                _audioParameterSnapshotB.Value = snapshot;

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

        private static float XorShiftSigned(uint sampleIndex, uint seed)
        {
            uint state = sampleIndex ^ seed;
            if (state == 0u)
                state = seed | 1u;
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0x00FFFFFFu) * (1f / 8388607.5f) - 1f;
        }

        private static float ResolveImpactPitchJitter(uint seed)
        {
            uint state = seed == 0u ? 0x6E624EB7u : seed;
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return math.lerp(0.8f, 1.2f, (state & 0x00FFFFFFu) * (1f / 16777215f));
        }

        private static float HashSigned(uint value)
        {
            value = HashUInt(value);
            return (value & 0x00FFFFFFu) * (1f / 8388607.5f) - 1f;
        }

        private static uint HashUInt(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }

        private static float Hash01(uint value)
        {
            return HashSigned(value) * 0.5f + 0.5f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RebuildAcousticOcclusionLayerMask();
        }
#endif
    }
}
