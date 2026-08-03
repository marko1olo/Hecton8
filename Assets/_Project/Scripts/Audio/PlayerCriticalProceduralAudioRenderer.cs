using System;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
using Hecton8.Audio.Echolocation;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Visor;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Serialization;
using AudioEvent = Hecton8.Core.Contracts.Signals.AudioEvent;

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
    public sealed class PlayerCriticalProceduralAudioRenderer : MonoBehaviour, ITickable, ISlowTickable, ILateFrameTickable, IUpdatable, IPhysicsImpactEventListener, ISonarPingEventListener, IAcousticEchoEventListener, IGlobalRegistryHotSwapListener, IGlobalRegistryHotSwapRefListener, IPlayerCriticalAudioSignalSink, IPlayerCriticalSonarEchoReadModel
    {
        private static int s_x001PlayerCriticalProceduralAudioRendererSignalPushDropCount;
        private const SystemID VaultOwner = SystemID.AudioPlayerCritical;
        private const float TwoPi = 6.28318530718f;
        private const float InvTwoPi = 0.15915494309f;
        private const float NaturalLogTen = 2.3025851f;
        private const float HullNoiseFloor = 0.0001f;
        private const float SonarChirpDurationSeconds = 0.5f;
        private const float SonarChirpDurationSecondsInv = 2f;
        private const float SonarTailAttackSeconds = 0.24f;
        private const float SonarTailAttackSecondsInv = 4.1666665f;
        private const float SonarTailDurationSeconds = 3.8f;
        private const float SonarTotalDurationSeconds = 4.0f;
        private const float SoundSpeedWaterMetersPerSecond = HectonPhysicsContract.SoundSpeedWaterMetersPerSecondConst;
        private static readonly float SoundSpeedWaterMetersPerSecondInv = HectonPhysicsContract.OneOverSoundSpeedWaterMetersPerSecond;
        private const float PredatorKillAudioRadiusMeters = 90f;
        private const float PredatorKillAudioRadiusMetersInv = 0.011111111f;
        private const float MeteorBoomAudioRadiusMeters = 42f;
        private const float MeteorBoomAudioRadiusMetersInv = 0.023809524f;
        private const float MechanicalWhirrAudioRadiusMeters = 18f;
        private const float MechanicalWhirrAudioRadiusMetersInv = 0.055555556f;
        private const float MechanicalWhirrPitchCutoffInv = 0.00083333335f;
        private const double AupRuntimeFloatClampMeters = 3.4028234663852886E+38d;
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
        private const int KineticImpactSignalScanLimit = 32;
        private const int KineticImpactDuplicateHistoryCapacity = 8;
        private const int KineticImpactDuplicateHistoryMask = KineticImpactDuplicateHistoryCapacity - 1;
        private const int AudioQualityPolicyUninitializedFrame = -4096;
        private const int AudioServiceLookupRetryFrames = 30;
        private const int TransportCoordinatorLookupRetryFrames = 30;
        private const int SonarTriggerFlagKineticImpactEcho = 1 << 0;
        private const uint AcousticImpulseFlagCritical = 1u << 0;
        private const uint AcousticImpulseFlagLeviathan = 1u << 1;
        private const uint AcousticImpulseFlagLarge = 1u << 3;
        private const float KineticImpactMinimumEnergyJoules = 12f;
        private const float KineticImpactReferenceEnergyJoules = 42000f;
        private const float KineticImpactExtremeEnergyJoules = 65000f;
        private const float KineticImpactMaximumSafeEnergyJoules = 120000f;
        private const float KineticImpactThudDurationSeconds = 0.2f;
        private const float KineticImpactPortalEchoLifetimeSeconds = 0.75f;
        private const float KineticImpactThudStartHertz = 150f;
        private const float KineticImpactThudEndHertz = 40f;
        private const float KineticImpactWaterLowPassHertz = 800f;
        private const float KineticImpactThudMinimumExcitation = 0.015f;
        private const float KineticImpactPortalEchoMasterGain = 0.46f;
        private const float KineticImpactDefaultWaterlineY = WorldWaterLevelCalibrationMath.DefaultWaterLevelY;
        private const float PlayerWaterSplashMinimumVerticalSpeed = 0.75f;
        private const float PlayerWaterSplashReferenceVerticalSpeed = 10f;
        private const float PlayerWaterSplashThudDurationSeconds = 0.14f;
        private const float PlayerWaterSplashEntryStartHertz = 112f;
        private const float PlayerWaterSplashExitStartHertz = 138f;
        private const float PlayerWaterSplashEntryEndHertz = 28f;
        private const float PlayerWaterSplashExitEndHertz = 46f;
        private const float PlayerWaterSplashLowPassHertz = 760f;
        private const float PlayerWaterSplashExitLowPassHertz = 1120f;
        private const float PlayerWaterSplashMaximumThudExcitation = 0.58f;
        private const float PlayerWaterSplashImpactStressScale = 0.08f;
        private const float ImpactClangMinimumExcitation = 0.12f;
        private const float ImpactClangFundamentalHertz = 150f;
        private const float ImpactClangPitchSpread = 0.22f;
        private const float ImpactClangFeedbackMinimum = 0.942f;
        private const float ImpactClangFeedbackMaximum = 0.986f;
        private const float ImpactClangEnvelopeDecayPerSecond = 4.8f;
        private const float ImpactClangLowPassBlend = 0.48f;
        private const float ImpactClangNoiseSeedDecay = 0.988f;
        private const float AbyssalLowPassStartDepthMeters = 500f;
        private const float AbyssalLowPassFadeDepthMetersInv = 0.00022222222f;
        private const float AbyssalLowPassCutoffHertz = 380f;
        private const float BrineLowPassMix = 0.92f;
        private const float PsychoacousticPressureReferenceDepthMeters = 500f;
        private const float PsychoacousticPressureMinimumCutoffHertz = 420f;
        private const float MinimumProbeDistanceMeters = 0.001f;
        private const float MaximumProbeDistanceMeters = 200f;
        private const float MinimumMixerWetMixDb = -80f;
        private const float MinimumFilterWetMixDb = -10000f;
        private const float BiquadDenormalBias = 1e-15f;
        private const float PressureCreakDepthReferenceMeters = 4000f;
        private const float PressureCreakDepthReferenceMetersInv = 0.00025f;
        private const float StructuralBreachAreaReferenceSquareMeters = 12f;
        private const float StructuralBreachAreaReferenceSquareMetersInv = 0.083333333f;
        private const float StructuralBreachCellCountInv = 0.041666667f;
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
        private const float PressureSignalImpulseDecayPerSecond = 8f;
        private const float PressureCreakMinimumPlaybackRate = 0.58f;
        private const float PressureCreakMaximumPlaybackRate = 1.95f;
        private const float PressureCreakMinimumBandCenterHertz = 96f;
        private const float PressureCreakMaximumBandCenterHertz = 1840f;
        private const int MetallicGrainSampleRate = 44100;
        private const int MetallicGrainBankCapacity = MetallicGrainSampleRate * 2;
        private const int GranularVoiceCapacity = 64;
        private const int GranularTelemetryCapacity = 300;
        private const int PrologueTransitionTelemetryCapacity = 300;
        private const int AudioSynthesisTelemetryCapacity = 300;
        private const int AudioSynthesisTelemetryFailureDumpThreshold = 3;
        private const int PrologueTransitionQueueCapacity = 32;
        private const int NativeDtoAlignmentBytes = 8;
        private const int AudioTransitionStateSizeBytes = 64;
        private const uint GranularTelemetrySampleStrideMask = 63u;
        private const int GranularDisabledVoiceCapacity = 0;
        private const int GranularMinimumQualityVoiceCapacity = 8;
        private const int GranularImpactClusterVoiceCount = 3;
        private const int GranularImpactStealTailSamples = 96;
        private const float GranularMinimumVoiceDensityOutputScale = 0.5f;
        private const int GranularHighQualityInterpolationVoiceThreshold = 12;
        private const float GranularVoiceUpgradeHysteresisSeconds = 2.5f;
        private const float GranularStressThreshold01 = 0.5f;
        private const float GranularMinimumGrainSeconds = 0.01f;
        private const float GranularMaximumGrainSeconds = 0.05f;
        private const float GranularImpactClusterCooldownSeconds = 0.02f;
        private const float GranularTuningBasePitchMinimum = 0.35f;
        private const float GranularTuningBasePitchMaximum = 2.4f;
        private const float GranularTuningGrainLengthMinimum = 0.25f;
        private const float GranularTuningGrainLengthMaximum = 4f;
        private const float GranularTuningOverlapDensityMinimum = 0f;
        private const float GranularTuningOverlapDensityMaximum = 4f;
        private const float GranularTuningFmModulationMinimum = 0f;
        private const float GranularTuningFmModulationMaximum = 4f;
        private const int HullCreakMinimumGrainSamples = 96;
        private const float HullCreakMetalGroanWindowSeconds = 1.35f;
        private const float PrologueClosedLowPassHertz = 150f;
        private const float PrologueOpenLowPassHertz = 22000f;
        private const float PrologueLfeHertz = 40f;
        private const float PrologueLfeOutputGain = 0.24f;
        private const float ProloguePlasmaOutputGain = 0.28f;
        private const float ProloguePlasmaBandPassMinimumHertz = 180f;
        private const float ProloguePlasmaBandPassMaximumHertz = 2100f;
        private const float ProloguePlasmaBandPassQ = 1.15f;
        private const float ProloguePlasmaLfoMinimumHertz = 0.21f;
        private const float ProloguePlasmaLfoMaximumHertz = 1.37f;
        private const float ProloguePlasmaLfoDepthMinimum = 0.38f;
        private const float ProloguePlasmaLfoDepthMaximum = 0.76f;
        private const float ProloguePlasmaMinimumQualityGain = 0.78f;
        private const float PrologueSplashdownDurationSeconds = 0.1f;
        private const float PrologueSplashdownSweepStartHertz = 40f;
        private const float PrologueSplashdownSweepEndHertz = 56f;
        private const float PrologueSplashdownOutputGain = 0.82f;
        private const float PrologueSplashdownCavitationNoiseGain = 0.46f;
        private const float ProloguePortalFdnSend = 0.28f;
        private static readonly int _audioTransitionStateRuntimeSizeBytes = UnsafeUtility.SizeOf<AudioTransitionState>();
        private static readonly bool _audioTransitionStateLayoutValid =
            _audioTransitionStateRuntimeSizeBytes == AudioTransitionStateSizeBytes &&
            (_audioTransitionStateRuntimeSizeBytes & (NativeDtoAlignmentBytes - 1)) == 0;
        private const float HullCreakGrainSeconds = 0.045f;
        private const float HullSubBassMinimumHertz = 25f;
        private const float HullSubBassMaximumHertz = 40f;
        private const float HullSubBassMaximumGain = 0.22f;
        private const float DepthSubwooferBoostFrequencyHertz = 30f;
        private const float DepthSubwooferBoostStartDepthMeters = 1000f;
        private const float DepthSubwooferBoostDepthRangeInv = 0.0002f;
        private const float DepthSubwooferBoostMaximumGain = 0.16f;
        private const float AbyssalHullDistortionStartDepthMeters = 2200f;
        private const float AbyssalHullDistortionDepthRangeInv = 0.00025f;
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
        private const int PanicGranularJitterShift = 5;
        private const float PanicGranularJitterMaximumGain = 0.018f;
        private const float PanicGranularJitterMaximumNoiseGain = 0.0035f;
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
        private const float BinauralMinimumMicroDelaySeconds = 0.0001f;
        private const float BinauralMaximumMicroDelaySeconds = 0.0007f;
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
        private const int SonarEchoTapCapacity = 32;
        private const int SonarSdfLowProbeCount = 8;
        private const int SonarSdfHighProbeCount = 32;
        private const int SonarEchoCompositeCandidateCapacity = 32;
        private const int SonarEchoCompositeGroupCapacity = 8;
        private const BufferID PlayerCriticalSonarEchoTapUploadRingBufferId = BufferID.PlayerCriticalProceduralAudioRenderer_PlayerCriticalSonarEchoTapUploadRingBufferId;
        private const BufferID PlayerCriticalPrologueTransitionRingBufferId = BufferID.PlayerCriticalProceduralAudioRenderer_PlayerCriticalPrologueTransitionRingBufferId;
        private const BufferID PlayerCriticalAudioSynthesisTelemetryRingBufferId = BufferID.PlayerCriticalProceduralAudioRenderer_PlayerCriticalAudioSynthesisTelemetryRingBufferId;
        private const ulong PrologueTransitionRingMutationGuardMask = 1UL << ((int)PlayerCriticalPrologueTransitionRingBufferId & 31);
        private const ulong PrologueTransitionTelemetryMutationGuardMask = 1UL << ((int)BufferID.PlayerCriticalPrologueTransitionTelemetryRing & 31);
        private const ulong GranularTelemetryMutationGuardMask = 1UL << ((int)BufferID.PlayerCriticalGranularTelemetryRing & 31);
        private const ulong AudioSynthesisTelemetryMutationGuardMask = 1UL << ((int)PlayerCriticalAudioSynthesisTelemetryRingBufferId & 31);
        private const ulong FrameScratchMutationGuardMask =
            (1UL << ((int)BufferID.PlayerCriticalHullScratch & 31)) |
            (1UL << ((int)BufferID.PlayerCriticalSonarScratch & 31)) |
            (1UL << ((int)BufferID.PlayerCriticalImpactEchoScratch & 31)) |
            (1UL << ((int)BufferID.PlayerCriticalThrusterScratch & 31)) |
            (1UL << ((int)BufferID.PlayerCriticalHeartbeatScratch & 31)) |
            (1UL << ((int)BufferID.PlayerCriticalHeartbeatDuckScratch & 31)) |
            (1UL << ((int)BufferID.PlayerCriticalBubbleScratch & 31)) |
            (1UL << ((int)BufferID.PlayerCriticalMixScratch & 31)) |
            (1UL << ((int)BufferID.PlayerCriticalStereoMixScratch & 31));
        private const ulong SonarDspMutationGuardMask =
            (1UL << ((int)BufferID.PlayerCriticalSonarEchoDelay & 31)) |
            (1UL << ((int)BufferID.PlayerCriticalSonarEchoReadCursors & 31)) |
            (1UL << ((int)BufferID.PlayerCriticalSonarEchoFilterInput1 & 31)) |
            (1UL << ((int)BufferID.PlayerCriticalSonarEchoFilterInput2 & 31)) |
            (1UL << ((int)BufferID.PlayerCriticalSonarEchoFilterOutput1 & 31)) |
            (1UL << ((int)BufferID.PlayerCriticalSonarEchoFilterOutput2 & 31));
        private const ulong SonarTapMutationGuardMask =
            (1UL << ((int)BufferID.PlayerCriticalPendingSonarEchoTapsA & 31)) |
            (1UL << ((int)BufferID.PlayerCriticalPendingSonarEchoTapsB & 31)) |
            (1UL << ((int)BufferID.PlayerCriticalWorkerSonarEchoTaps & 31)) |
            (1UL << ((int)PlayerCriticalSonarEchoTapUploadRingBufferId & 31));
        private const ulong SonarSpatialMutationGuardMask =
            (1UL << ((int)BufferID.PlayerCriticalSonarEchoCompositeCandidatesA & 31)) |
            (1UL << ((int)BufferID.PlayerCriticalSonarEchoCompositeCandidatesB & 31)) |
            (1UL << ((int)BufferID.PlayerCriticalSonarEchoCompositeGroups & 31)) |
            (1UL << ((int)BufferID.PlayerCriticalSonarEchoCompositeGroupCount & 31)) |
            (1UL << ((int)BufferID.PlayerCriticalSonarEcholocationHits & 31));
        private const ulong TransientDelayMutationGuardMask =
            (1UL << ((int)BufferID.PlayerCriticalImpactClangDelay & 31)) |
            (1UL << ((int)BufferID.PlayerCriticalThrusterCombDelay & 31));
        private const ulong ReverbMutationGuardMask =
            (1UL << ((int)BufferID.PlayerCriticalSabineReverbDelay & 31)) |
            (1UL << ((int)BufferID.PlayerCriticalCaveConvolutionImpulse & 31)) |
            (1UL << ((int)BufferID.PlayerCriticalCaveConvolutionDelay & 31)) |
            (1UL << ((int)BufferID.PlayerCriticalInteriorFdnDelay & 31));
        private const ulong BinauralFilterMutationGuardMask =
            (1UL << ((int)BufferID.PlayerCriticalBinauralDelayRing & 31)) |
            (1UL << ((int)BufferID.PlayerCriticalBinauralShadowHistory & 31)) |
            (1UL << ((int)BufferID.PlayerCriticalLowPassInputHistory1 & 31)) |
            (1UL << ((int)BufferID.PlayerCriticalLowPassInputHistory2 & 31)) |
            (1UL << ((int)BufferID.PlayerCriticalLowPassOutputHistory1 & 31)) |
            (1UL << ((int)BufferID.PlayerCriticalLowPassOutputHistory2 & 31));
        private const ulong GranularVoiceMutationGuardMask =
            (1UL << ((int)BufferID.PlayerCriticalMetallicGrainBank & 31)) |
            (1UL << ((int)BufferID.PlayerCriticalGranularVoiceActive & 31)) |
            (1UL << ((int)BufferID.PlayerCriticalGranularVoiceElapsed & 31)) |
            (1UL << ((int)BufferID.PlayerCriticalGranularVoiceLength & 31)) |
            (1UL << ((int)BufferID.PlayerCriticalGranularVoiceStart & 31)) |
            (1UL << ((int)BufferID.PlayerCriticalGranularVoiceSeed & 31)) |
            (1UL << ((int)BufferID.PlayerCriticalGranularVoiceCursor & 31)) |
            (1UL << ((int)BufferID.PlayerCriticalGranularVoicePlaybackRate & 31)) |
            (1UL << ((int)BufferID.PlayerCriticalGranularVoiceGain & 31));
        private const ulong AudioBlockDspMutationGuardMask =
            FrameScratchMutationGuardMask |
            SonarDspMutationGuardMask |
            SonarTapMutationGuardMask |
            SonarSpatialMutationGuardMask |
            TransientDelayMutationGuardMask |
            ReverbMutationGuardMask |
            BinauralFilterMutationGuardMask |
            GranularVoiceMutationGuardMask;
        private const uint AudioSynthesisTelemetryFlagSuccess = 0u;
        private const uint AudioSynthesisTelemetryFlagLockContention = 1u << 0;
        private const uint AudioSynthesisTelemetryFlagStaleOrMissingHandle = 1u << 1;
        private const uint AudioSynthesisTelemetryFlagNonFiniteSample = 1u << 2;
        private const uint AudioSynthesisTelemetryFlagOutputUnderrun = 1u << 3;
        private const int AudioSynthesisFailureNone = 0;
        private const int AudioSynthesisFailureVaultResolution = 1;
        private const int AudioSynthesisFailureTelemetryLock = 2;
        private const int AudioSynthesisFailureNonFiniteSample = 3;
        private const int AudioSynthesisFailureOutputRingFull = 4;
        private const double SonarEchoCompositeCellSizeMeters = 10d;
        private const double SonarEchoCompositeCellSizeMetersInv = 0.1d;
        private const float EcholocationReflectivityConstant = 0.000045f;
        private const float EcholocationDensityThreshold01 = 0.5f;
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
        private const int CaveConvolutionImpulseLength = 32;
        private const int CaveConvolutionDelayCapacity = 128;
        private const int CaveConvolutionDelayMask = CaveConvolutionDelayCapacity - 1;
        private const int InteriorFdnDelayCapacity = 8192;
        private const int InteriorFdnDelayMask = InteriorFdnDelayCapacity - 1;
        private const int InteriorFdnLaneLength = 2048;
        private const int InteriorFdnLaneMask = InteriorFdnLaneLength - 1;
        private const int InteriorFdnPrimeDelayA = 431;
        private const int InteriorFdnPrimeDelayB = 653;
        private const int InteriorFdnPrimeDelayC = 947;
        private const int InteriorFdnPrimeDelayD = 1291;
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
        private const float CaveConvolutionMaximumWetGain = 0.38f;
        private const float CaveConvolutionDensityWetBoost = 0.42f;
        private const float CaveConvolutionDampingClosedCutoffHertz = 620f;
        private const float CaveConvolutionDampingOpenCutoffHertz = 2600f;
        private const float CaveConvolutionWetMixLerpCoefficient = 0.00125f;
        private const int CaveConvolutionDensitySampleLimit = 512;
        private const float ReverbProfileDecayApplyStepSeconds = 0.025f;
        private const float ReverbProfileWetApplyStep01 = 0.015f;
        private const float ReverbProfileOpennessApplyStep01 = 0.015f;
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
        private const int ImpactEventQueueCapacity = 64;
        private const int ImpactEventQueueMask = ImpactEventQueueCapacity - 1;
        private const int ImpactEventQueueEnqueueAttemptLimit = 8;
        private const int MetallicGrainBankCapacityGuard =
            1 / (MetallicGrainBankCapacity == MetallicGrainSampleRate * 2 ? 1 : 0);
        private const int BinauralDelayPowerOfTwoGuard =
            1 / ((BinauralDelayCapacity > 0 &&
                  (BinauralDelayCapacity & (BinauralDelayCapacity - 1)) == 0 &&
                  BinauralDelayMask == BinauralDelayCapacity - 1) ? 1 : 0);
        private const int MaxSafeFrameCapacityPowerOfTwoGuard =
            1 / ((MaxSafeFrameCapacity > 0 &&
                  (MaxSafeFrameCapacity & (MaxSafeFrameCapacity - 1)) == 0) ? 1 : 0);
        private const int SonarEchoDelayPowerOfTwoGuard =
            1 / ((SonarEchoDelayCapacity > 0 &&
                  (SonarEchoDelayCapacity & (SonarEchoDelayCapacity - 1)) == 0 &&
                  SonarEchoDelayMask == SonarEchoDelayCapacity - 1) ? 1 : 0);
        private const int ImpactClangDelayPowerOfTwoGuard =
            1 / ((ImpactClangDelayCapacity > 0 &&
                  (ImpactClangDelayCapacity & (ImpactClangDelayCapacity - 1)) == 0 &&
                  ImpactClangDelayMask == ImpactClangDelayCapacity - 1) ? 1 : 0);
        private const int ThrusterCombDelayPowerOfTwoGuard =
            1 / ((ThrusterCombDelayCapacity > 0 &&
                  (ThrusterCombDelayCapacity & (ThrusterCombDelayCapacity - 1)) == 0 &&
                  ThrusterCombDelayMask == ThrusterCombDelayCapacity - 1) ? 1 : 0);
        private const int SabineReverbDelayPowerOfTwoGuard =
            1 / ((SabineReverbDelayLineLength > 0 &&
                  (SabineReverbDelayLineLength & (SabineReverbDelayLineLength - 1)) == 0 &&
                  SabineReverbDelayLineMask == SabineReverbDelayLineLength - 1 &&
                  (SabineReverbDelayCapacity & (SabineReverbDelayCapacity - 1)) == 0) ? 1 : 0);
        private const int CaveConvolutionPowerOfTwoGuard =
            1 / ((CaveConvolutionImpulseLength > 0 &&
                  (CaveConvolutionImpulseLength & (CaveConvolutionImpulseLength - 1)) == 0 &&
                  CaveConvolutionDelayCapacity > 0 &&
                  (CaveConvolutionDelayCapacity & (CaveConvolutionDelayCapacity - 1)) == 0 &&
                  CaveConvolutionDelayMask == CaveConvolutionDelayCapacity - 1 &&
                  CaveConvolutionImpulseLength <= CaveConvolutionDelayCapacity) ? 1 : 0);
        private const int InteriorFdnDelayPowerOfTwoGuard =
            1 / ((InteriorFdnDelayCapacity > 0 &&
                  (InteriorFdnDelayCapacity & (InteriorFdnDelayCapacity - 1)) == 0 &&
                  InteriorFdnDelayMask == InteriorFdnDelayCapacity - 1 &&
                  InteriorFdnLaneLength > 0 &&
                  (InteriorFdnLaneLength & (InteriorFdnLaneLength - 1)) == 0 &&
                  InteriorFdnLaneMask == InteriorFdnLaneLength - 1) ? 1 : 0);
        private const int ImpactEventQueuePowerOfTwoGuard =
            1 / ((ImpactEventQueueCapacity > 0 &&
                  (ImpactEventQueueCapacity & (ImpactEventQueueCapacity - 1)) == 0 &&
                  ImpactEventQueueMask == ImpactEventQueueCapacity - 1) ? 1 : 0);
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
        private const float EardrumRuptureTinnitusHertz = 12000f;
        private const float EardrumRuptureImpactThreshold01 = 0.9f;
        private const float EardrumRuptureDecayPerSecond = 2.2f;
        private const float EardrumRuptureMaximumGain = 0.035f;
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
        private const uint KccVelocityAudioMaxAgeFrames = 12u;
        private const float LeviathanDopplerSmoothingSamples = 128f;
        private const float LeviathanDopplerSmoothingReferenceSampleRate = 48000f;
        private const float LeviathanLfeBypassCutoffHertz = 118f;
        private const float LeviathanLfeBypassGain = 1.35f;
        private const float BinauralNarcosisChorusThreshold01 = 0.5f;
        private const float BinauralNarcosisChorusRateHertz = 0.31f;
        private const float BinauralNarcosisMaximumChorusSamples = 2.5f;
        private const float VehicleCavitationScreechStartMetersPerSecond = 20f;
        private const float VehicleCavitationScreechFullMetersPerSecond = 32f;
        private const float VehicleCavitationScreechGain = 0.075f;
        private const float VehicleCavitationHighPassAlpha = 0.92f;
        private const float PressureScrubberHumFrequencyHertz = 40f;
        private const float PressureScrubberHumOxygenPitchMaximumScale = 1.65f;
        private const float PressureScrubberHumMaximumGain = 0.045f;
        private const float PressureScrubberHumDepthUpdateDeltaMeters = 10f;
        private const byte SonarAudioMaterialIdDefault = 0;
        private const byte SonarAudioMaterialIdMetal = 1;
        private const byte SonarAudioMaterialIdRock = 2;
        private const byte SonarAudioMaterialIdGlass = 3;
        private const byte SonarAudioMaterialIdBiological = 4;
        private const float StructuralSnapMinimumHertz = 4200f;
        private const float StructuralSnapMaximumHertz = 9600f;
        private const float StructuralSnapDecayPerSecond = 14f;
        private const float StructuralSnapMaximumGain = 0.16f;
        private const float StructuralSnapPitchMinimum = 0.8f;
        private const float StructuralSnapPitchMaximum = 1.2f;
        private const float DspProducerSolveBudgetMilliseconds = 0.1f;
        private const double DspProducerSolveBudgetSeconds = 0.0001d;
        private const int DspProducerTelemetryCooldownFrames = 60;
        private static readonly long DspProducerSolveBudgetTicks =
            Math.Max(1L, (long)(System.Diagnostics.Stopwatch.Frequency * DspProducerSolveBudgetSeconds));
        private static readonly uint _dspProducerOverBudgetWarningHash =
            unchecked((uint)Hecton.Localization.LocHash.Compute("Audio.DspProducerOverBudget"));
        private static readonly uint _dspProducerContextHash =
            unchecked((uint)Hecton.Localization.LocHash.Compute("PlayerCriticalProceduralAudioRenderer.DspProducer"));
        private static int s_runtimeInstalled;

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

        [Tooltip("SDF raymarch interval for active sonar echo probes.")]
        [SerializeField, Range(5f, 100f)] private float sonarSdfProbeIntervalMeters = 50f;

        [Tooltip("Maximum one-way SDF probe distance for active sonar echo taps.")]
        [SerializeField, Range(50f, 200f)] private float sonarSdfMaximumProbeDistanceMeters = MaximumProbeDistanceMeters;

        [Tooltip("Fallback synthetic echo taps when no published SDF surface is available.")]
        [SerializeField] private bool sonarSdfFallbackGhostEchoes = true;

        [Tooltip("Maximum range for leviathan bio-echo ping-back response.")]
        [SerializeField, Range(25f, 300f)] private float sonarPredatorPingBackRadiusMeters = 180f;

        [Tooltip("Depth pressure scalar used to dull high frequencies and amplitude on sonar echoes.")]
        [SerializeField, Range(0f, 1f)] private float sonarDepthMufflingScalar = 0.18f;

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
        [SerializeField, Range(2048, 262144)] private int ringBufferCapacityFrames = AudioFrameSpscRingBuffer.AudioBufferCapacity;

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

        private VaultGenerationHandle<float> _hullScratchHandle;
        private VaultGenerationHandle<float> _sonarScratchHandle;
        private VaultGenerationHandle<float> _impactEchoScratchHandle;
        private VaultGenerationHandle<float> _thrusterScratchHandle;
        private VaultGenerationHandle<float> _heartbeatScratchHandle;
        private VaultGenerationHandle<float> _heartbeatDuckScratchHandle;
        private VaultGenerationHandle<float> _bubbleScratchHandle;
        private VaultGenerationHandle<float> _mixScratchHandle;
        private VaultGenerationHandle<float> _stereoMixScratchHandle;
        private VaultGenerationHandle<float> _sonarEchoDelayHandle;
        private VaultGenerationHandle<SonarEchoTap> _pendingSonarEchoTapsAHandle;
        private VaultGenerationHandle<SonarEchoTap> _pendingSonarEchoTapsBHandle;
        private VaultGenerationHandle<SonarEchoTap> _workerSonarEchoTapsHandle;
        private VaultGenerationHandle<float> _sonarEchoReadCursorsHandle;
        private VaultGenerationHandle<float> _sonarEchoFilterInput1Handle;
        private VaultGenerationHandle<float> _sonarEchoFilterInput2Handle;
        private VaultGenerationHandle<float> _sonarEchoFilterOutput1Handle;
        private VaultGenerationHandle<float> _sonarEchoFilterOutput2Handle;
        private VaultGenerationHandle<SonarEchoCompositeGroup> _sonarEchoCompositeCandidatesAHandle;
        private VaultGenerationHandle<SonarEchoCompositeGroup> _sonarEchoCompositeCandidatesBHandle;
        private VaultGenerationHandle<SonarEchoCompositeGroup> _sonarEchoCompositeGroupsHandle;
        private VaultGenerationHandle<int> _sonarEchoCompositeGroupCountNativeHandle;
        private VaultGenerationHandle<AcousticEcholocationRayHit> _sonarEcholocationHitsHandle;
        private VaultGenerationHandle<SonarEchoTap> _sonarEchoTapUploadRingHandle;
        private VaultGenerationHandle<float> _impactClangDelayHandle;
        private VaultGenerationHandle<float> _thrusterCombDelayHandle;
        private VaultGenerationHandle<float> _sabineReverbDelayHandle;
        private VaultGenerationHandle<float> _caveConvolutionImpulseHandle;
        private VaultGenerationHandle<float> _caveConvolutionDelayHandle;
        private VaultGenerationHandle<float> _interiorFdnDelayHandle;
        private VaultGenerationHandle<float> _binauralDelayRingHandle;
        private VaultGenerationHandle<float> _binauralShadowHistoryHandle;
        private VaultGenerationHandle<float> _lowPassInputHistory1Handle;
        private VaultGenerationHandle<float> _lowPassInputHistory2Handle;
        private VaultGenerationHandle<float> _lowPassOutputHistory1Handle;
        private VaultGenerationHandle<float> _lowPassOutputHistory2Handle;
        private VaultGenerationHandle<float> _metallicGrainBankHandle;
        private VaultGenerationHandle<int> _granularVoiceActiveHandle;
        private VaultGenerationHandle<int> _granularVoiceElapsedHandle;
        private VaultGenerationHandle<int> _granularVoiceLengthHandle;
        private VaultGenerationHandle<int> _granularVoiceStartHandle;
        private VaultGenerationHandle<uint> _granularVoiceSeedHandle;
        private VaultGenerationHandle<float> _granularVoiceCursorHandle;
        private VaultGenerationHandle<float> _granularVoicePlaybackRateHandle;
        private VaultGenerationHandle<float> _granularVoiceGainHandle;
        private VaultGenerationHandle<GranularAudioTelemetryEntry> _granularTelemetryRingHandle;
        private VaultGenerationHandle<PrologueAudioTransitionTelemetryEntry> _prologueTransitionTelemetryRingHandle;
        private VaultGenerationHandle<AudioTransitionState> _prologueTransitionRingHandle;
        private VaultGenerationHandle<AudioSynthesisTelemetryEntry> _audioSynthesisTelemetryRingHandle;
        private ref struct GranularVoiceVaultViews
        {
            public IDataVault GuardVault;
            public ulong GuardMask;
            public NativeArray<float> MetallicGrainBank;
            public NativeArray<int> VoiceActive;
            public NativeArray<int> VoiceElapsed;
            public NativeArray<int> VoiceLength;
            public NativeArray<int> VoiceStart;
            public NativeArray<uint> VoiceSeed;
            public NativeArray<float> VoiceCursor;
            public NativeArray<float> VoicePlaybackRate;
            public NativeArray<float> VoiceGain;
        }

        private ref struct BinauralFilterVaultViews
        {
            public IDataVault GuardVault;
            public ulong GuardMask;
            public NativeArray<float> BinauralDelayRing;
            public NativeArray<float> BinauralShadowHistory;
            public NativeArray<float> LowPassInputHistory1;
            public NativeArray<float> LowPassInputHistory2;
            public NativeArray<float> LowPassOutputHistory1;
            public NativeArray<float> LowPassOutputHistory2;
        }

        private ref struct ReverbVaultViews
        {
            public IDataVault GuardVault;
            public ulong GuardMask;
            public NativeArray<float> SabineReverbDelay;
            public NativeArray<float> CaveConvolutionImpulse;
            public NativeArray<float> CaveConvolutionDelay;
            public NativeArray<float> InteriorFdnDelay;
        }

        private ref struct TransientDelayVaultViews
        {
            public IDataVault GuardVault;
            public ulong GuardMask;
            public NativeArray<float> ImpactClangDelay;
            public NativeArray<float> ThrusterCombDelay;
        }

        private ref struct FrameScratchVaultViews
        {
            public IDataVault GuardVault;
            public ulong GuardMask;
            public NativeArray<float> HullScratch;
            public NativeArray<float> SonarScratch;
            public NativeArray<float> ImpactEchoScratch;
            public NativeArray<float> ThrusterScratch;
            public NativeArray<float> HeartbeatScratch;
            public NativeArray<float> HeartbeatDuckScratch;
            public NativeArray<float> BubbleScratch;
            public NativeArray<float> MixScratch;
            public NativeArray<float> StereoMixScratch;
        }

        private ref struct SonarTapVaultViews
        {
            public IDataVault GuardVault;
            public ulong GuardMask;
            public NativeArray<SonarEchoTap> PendingA;
            public NativeArray<SonarEchoTap> PendingB;
            public NativeArray<SonarEchoTap> Worker;
            public NativeArray<SonarEchoTap> UploadRing;
        }

        private ref struct SonarDspVaultViews
        {
            public IDataVault GuardVault;
            public ulong GuardMask;
            public NativeArray<float> EchoDelay;
            public NativeArray<float> ReadCursors;
            public NativeArray<float> FilterInput1;
            public NativeArray<float> FilterInput2;
            public NativeArray<float> FilterOutput1;
            public NativeArray<float> FilterOutput2;
        }

        private ref struct SonarSpatialVaultViews
        {
            public IDataVault GuardVault;
            public ulong GuardMask;
            public NativeArray<SonarEchoCompositeGroup> CandidatesA;
            public NativeArray<SonarEchoCompositeGroup> CandidatesB;
            public NativeArray<SonarEchoCompositeGroup> Groups;
            public NativeArray<int> GroupCount;
            public NativeArray<AcousticEcholocationRayHit> Hits;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct AcousticEcholocationRaymarchJob : IJobParallelFor
        {
            private const byte AudioMaterialMetal = 1;
            private const byte AudioMaterialRock = 2;
            private const byte AudioMaterialGlass = 3;
            private const byte AudioMaterialBiological = 4;

            [ReadOnly, NoAlias] public NativeArray<byte>.ReadOnly EncodedSdf;
            [ReadOnly, NoAlias] public NativeArray<byte>.ReadOnly AudioMaterialIds;
            [NoAlias] public NativeArray<AcousticEcholocationRayHit> Hits;
            public int3 GridDimensions;
            public float3 VolumeOrigin;
            public float3 CellSize;
            public float SdfRange;
            public float3 PingOrigin;
            public float3 ListenerPosition;
            public float3 Forward;
            public float3 Right;
            public float3 Up;
            public float MaxDistanceMeters;
            public float StepMeters;
            public float Intensity01;
            public float ReflectivityConstant;
            public float SoundSpeedInv;
            public float DensityThreshold01;
            public float MinimumLowPassHertz;
            public float OpenLowPassHertz;
            public float AbsorptionCoefficient;
            public float ReferenceDistanceMeters;
            public int RayCount;

            public void Execute(int index)
            {
                if (!Hits.IsCreated || (uint)index >= (uint)Hits.Length)
                    return;

                Hits[index] = default;
                if (index >= RayCount ||
                    !EncodedSdf.IsCreated ||
                    GridDimensions.x <= 1 ||
                    GridDimensions.y <= 1 ||
                    GridDimensions.z <= 1 ||
                    SdfRange <= 0.0001f ||
                    MaxDistanceMeters <= 0.0001f)
                {
                    return;
                }

                float3 direction = ResolveRayDirection(index, math.max(1, RayCount));
                float step = math.clamp(StepMeters, 0.05f, math.max(0.05f, MaxDistanceMeters));
                float previousDensity = 0f;
                float3 previousPosition = PingOrigin;
                float previousDistance = 0f;
                bool hasPrevious = false;

                for (float distance = 0f; distance <= MaxDistanceMeters; distance += step)
                {
                    float3 position = PingOrigin + direction * distance;
                    if (!TrySampleDensity(position, out float density, out float density01, out byte audioMaterialId))
                        continue;

                    bool canReturnEcho = distance > 0f;
                    bool thresholdHit = canReturnEcho && density01 >= math.saturate(DensityThreshold01);
                    bool surfaceHit = hasPrevious && previousDensity < 0f && density >= 0f;
                    bool initialSolidHit = canReturnEcho && !hasPrevious && density >= 0f;
                    if (!thresholdHit && !surfaceHit && !initialSolidHit)
                    {
                        previousDensity = density;
                        previousPosition = position;
                        previousDistance = distance;
                        hasPrevious = true;
                        continue;
                    }

                    // Default to the sampled point. density and audioMaterialId were both read at
                    // `position`, so leaving t at 0 reported the echo at `previousPosition` - one
                    // whole `step` (sonarSdfProbeIntervalMeters, 50 m by default) short of the
                    // surface that produced it, and at PingOrigin itself whenever no in-bounds
                    // sample preceded the hit. Only a sign crossing has a sub-step surface to
                    // interpolate towards.
                    float t = 1f;
                    if (surfaceHit)
                    {
                        float denom = math.max(0.0001f, density - previousDensity);
                        t = math.saturate(-previousDensity * math.rcp(denom));
                    }

                    // previousDistance instead of `distance - step` so the interpolated range stays
                    // affine-consistent with hitPoint even when intermediate samples fell outside
                    // the SDF volume and the real gap was wider than one step.
                    float3 hitPoint = math.lerp(previousPosition, position, t);
                    float rayDistance = math.max(0f, math.lerp(previousDistance, distance, t));
                    float returnDistance = math.length(hitPoint - ListenerPosition);
                    float totalDistance = math.max(0.001f, rayDistance + returnDistance);

                    float soundSpeedMps = math.max(0.001f, math.rcp(math.max(SoundSpeedInv, 0.000001f)));
                    var sonarResult = Hecton8.PureLogic.Systems.SonarPingReturnTimeCalculator.Compute(
                        totalDistance * 0.5f,
                        soundSpeedMps,
                        0f,
                        0f,
                        0f,
                        0f,
                        0.001f,
                        5000f
                    );
                    float delaySeconds = sonarResult.returnTimeSeconds;
                    float totalTimeSq = math.max(delaySeconds * delaySeconds, 0.000001f);
                    float absorption = ApproxExpNeg(totalDistance * math.max(0f, AbsorptionCoefficient));
                    float reference = math.max(0.001f, ReferenceDistanceMeters);
                    float nearFieldLimiter = reference * math.rcp(math.max(reference, totalDistance));
                    float gain = math.saturate(
                        math.saturate(Intensity01) *
                        math.max(0f, ReflectivityConstant) *
                        math.rcp(totalTimeSq) *
                        absorption *
                        nearFieldLimiter *
                        ResolveMaterialReflectivity(audioMaterialId));
                    if (!math.isfinite(gain) || gain <= 0.000001f)
                        return;

                    Hits[index] = new AcousticEcholocationRayHit
                    {
                        Point = hitPoint,
                        Direction = direction,
                        RayDistanceMeters = rayDistance,
                        ReturnDistanceMeters = returnDistance,
                        DelaySeconds = delaySeconds,
                        Gain = gain,
                        LowPassCutoffHertz = ResolveLowPassCutoff(totalDistance, audioMaterialId),
                        AudioMaterialId = audioMaterialId,
                        Hit = 1,
                        StateHash = Hash(index, hitPoint, audioMaterialId)
                    };
                    return;
                }
            }

            private bool TrySampleDensity(float3 worldPosition, out float density, out float density01, out byte audioMaterialId)
            {
                density = 0f;
                density01 = 0f;
                audioMaterialId = AudioMaterialRock;

                float3 safeCell = math.max(CellSize, new float3(0.0001f));
                float3 sample = (worldPosition - VolumeOrigin) * math.rcp(safeCell);
                if (sample.x < 0f || sample.y < 0f || sample.z < 0f ||
                    sample.x > GridDimensions.x - 1.001f ||
                    sample.y > GridDimensions.y - 1.001f ||
                    sample.z > GridDimensions.z - 1.001f)
                {
                    return false;
                }

                int x0 = (int)math.floor(sample.x);
                int y0 = (int)math.floor(sample.y);
                int z0 = (int)math.floor(sample.z);
                int x1 = math.min(x0 + 1, GridDimensions.x - 1);
                int y1 = math.min(y0 + 1, GridDimensions.y - 1);
                int z1 = math.min(z0 + 1, GridDimensions.z - 1);
                float tx = sample.x - x0;
                float ty = sample.y - y0;
                float tz = sample.z - z0;

                float c000 = DecodeAt(x0, y0, z0);
                float c100 = DecodeAt(x1, y0, z0);
                float c010 = DecodeAt(x0, y1, z0);
                float c110 = DecodeAt(x1, y1, z0);
                float c001 = DecodeAt(x0, y0, z1);
                float c101 = DecodeAt(x1, y0, z1);
                float c011 = DecodeAt(x0, y1, z1);
                float c111 = DecodeAt(x1, y1, z1);
                float c00 = math.lerp(c000, c100, tx);
                float c10 = math.lerp(c010, c110, tx);
                float c01 = math.lerp(c001, c101, tx);
                float c11 = math.lerp(c011, c111, tx);
                density = math.lerp(math.lerp(c00, c10, ty), math.lerp(c01, c11, ty), tz);
                density01 = math.saturate(math.max(0f, density) * math.rcp(math.max(0.0001f, SdfRange)));
                audioMaterialId = ResolveAudioMaterialIdNearest(sample);
                return math.isfinite(density);
            }

            private float DecodeAt(int x, int y, int z)
            {
                int index = x + GridDimensions.x * (y + GridDimensions.y * z);
                if ((uint)index >= (uint)EncodedSdf.Length)
                    return -SdfRange;

                return ((EncodedSdf[index] * 0.00392156862f) * 2f - 1f) * SdfRange;
            }

            private byte ResolveAudioMaterialIdNearest(float3 sample)
            {
                if (!AudioMaterialIds.IsCreated || AudioMaterialIds.Length != EncodedSdf.Length)
                    return AudioMaterialRock;

                int x = math.clamp((int)(sample.x + 0.5f), 0, GridDimensions.x - 1);
                int y = math.clamp((int)(sample.y + 0.5f), 0, GridDimensions.y - 1);
                int z = math.clamp((int)(sample.z + 0.5f), 0, GridDimensions.z - 1);
                int index = x + GridDimensions.x * (y + GridDimensions.y * z);
                if ((uint)index >= (uint)AudioMaterialIds.Length)
                    return AudioMaterialRock;

                byte materialId = AudioMaterialIds[index];
                switch (materialId)
                {
                    case AudioMaterialMetal:
                    case AudioMaterialRock:
                    case AudioMaterialGlass:
                    case AudioMaterialBiological:
                        return materialId;
                    default:
                        return AudioMaterialRock;
                }
            }

            private float3 ResolveRayDirection(int index, int rayCount)
            {
                float3 forward = NormalizeSafe(Forward, new float3(0f, 0f, 1f));
                float3 right = NormalizeSafe(Right, new float3(1f, 0f, 0f));
                float3 up = NormalizeSafe(Up, new float3(0f, 1f, 0f));
                if (rayCount <= 8)
                {
                    float sx = (index & 1) == 0 ? 1f : -1f;
                    float sy = (index & 2) == 0 ? 1f : -1f;
                    float sz = (index & 4) == 0 ? 1f : -1f;
                    return NormalizeSafe(right * sx + up * sy + forward * sz, forward);
                }

                int lane = index & 31;
                float laneSx = (lane & 1) == 0 ? 1f : -1f;
                float laneSy = (lane & 2) == 0 ? 1f : -1f;
                float laneSz = (lane & 4) == 0 ? 1f : -1f;
                int weightSet = (lane >> 3) & 3;
                float forwardWeight = weightSet == 0 ? 1f : weightSet == 1 ? 0.55f : weightSet == 2 ? 0.25f : 0.75f;
                float rightWeight = weightSet == 1 ? 1f : weightSet == 2 ? 0.55f : weightSet == 3 ? 0.25f : 0.75f;
                float upWeight = weightSet == 2 ? 1f : weightSet == 3 ? 0.55f : weightSet == 0 ? 0.25f : 0.75f;
                return NormalizeSafe(
                    right * (laneSx * rightWeight) +
                    up * (laneSy * upWeight) +
                    forward * (laneSz * forwardWeight),
                    forward);
            }

            private float ResolveLowPassCutoff(float totalDistance, byte audioMaterialId)
            {
                float distanceT = math.saturate(totalDistance * math.rcp(math.max(1f, MaxDistanceMeters)));
                float cutoff = math.lerp(
                    math.max(MinimumLowPassHertz, 1f),
                    math.max(OpenLowPassHertz, MinimumLowPassHertz + 1f),
                    1f - distanceT);
                switch (audioMaterialId)
                {
                    case AudioMaterialMetal:
                        return math.clamp(math.max(cutoff, 6800f), MinimumLowPassHertz, OpenLowPassHertz);
                    case AudioMaterialGlass:
                        return math.clamp(math.max(cutoff, 5600f), MinimumLowPassHertz, OpenLowPassHertz);
                    case AudioMaterialBiological:
                        return math.clamp(math.min(cutoff, 1150f), MinimumLowPassHertz, OpenLowPassHertz);
                    case AudioMaterialRock:
                        return math.clamp(math.min(cutoff, 2400f), MinimumLowPassHertz, OpenLowPassHertz);
                    default:
                        return math.clamp(cutoff, MinimumLowPassHertz, OpenLowPassHertz);
                }
            }

            private static float ResolveMaterialReflectivity(byte audioMaterialId)
            {
                switch (audioMaterialId)
                {
                    case AudioMaterialMetal:
                        return 1.35f;
                    case AudioMaterialGlass:
                        return 1.12f;
                    case AudioMaterialBiological:
                        return 0.78f;
                    case AudioMaterialRock:
                        return 0.64f;
                    default:
                        return 0.86f;
                }
            }

            private static float3 NormalizeSafe(float3 value, float3 fallback)
            {
                if (!math.all(math.isfinite(value)))
                    return fallback;

                float lengthSq = math.lengthsq(value);
                return lengthSq > 0.000001f ? value * math.rsqrt(lengthSq) : fallback;
            }

            private static float ApproxExpNeg(float value)
            {
                float x = math.max(0f, value);
                return math.rcp(1f + x + x * x * 0.48f + x * x * x * 0.235f);
            }

            private static uint Hash(int index, float3 point, byte materialId)
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)index) * 16777619u;
                hash = (hash ^ (uint)math.asint(point.x)) * 16777619u;
                hash = (hash ^ (uint)math.asint(point.y)) * 16777619u;
                hash = (hash ^ (uint)math.asint(point.z)) * 16777619u;
                hash = (hash ^ materialId) * 16777619u;
                return hash;
            }
        }

        private IDataVault _dataVault;
        private int _granularTelemetryCursor;
        private int _granularTelemetryDumpRequested;
        private int _granularTelemetryDumped;
        private int _prologueTransitionTelemetryCursor;
        private int _prologueTransitionTelemetryDumpRequested;
        private int _prologueTransitionTelemetryDumped;
        private int _audioSynthesisTelemetryCursor;
        private int _audioSynthesisTelemetryDumpRequested;
        private int _audioSynthesisTelemetryDumped;
        private int _audioSynthesisConsecutiveVaultFailures;
        private int _sonarEchoTapUploadReadIndex;
        private int _sonarEchoTapUploadWriteIndex;
        private int _sonarEchoTapUploadCount;
        private int _prologueTransitionReadIndex;
        private int _prologueTransitionWriteIndex;
        private int _prologueTransitionQueueCount;
        private AudioFrameSpscRingBuffer _sampleRingBuffer;
        private Thread _audioProducerThread;
        // COLD ALLOC: ManualResetEventSlim[1] - producer-thread wake fence - owner: PlayerCriticalProceduralAudioRenderer
        private readonly ManualResetEventSlim _audioProducerWakeSignal = new(false);
        private int _frameCapacity;
        private int _sampleRate;
        private int _sonarTotalDurationSamples;
        private int _sonarEcholocationScheduledSequence;
        private int _sonarEcholocationScheduledRayCount;
        private int _sonarEcholocationScheduledSdfVersion;
        private uint _sonarEcholocationScheduledShiftSequence;
        private long _sonarEcholocationScheduledStartFrame;
        private float _sonarEcholocationScheduledIntensity;
        private Vector3 _sonarEcholocationScheduledOrigin;
        private Transform _sonarEcholocationScheduledTransform;
        private int _lastConsumedAcousticPingSignalSequence;
        private uint _lastHighSpeedImpactFrame;
        private uint _lastHighSpeedImpactSignature;
        private int _lastHighSpeedImpactSignalValid;
        // COLD ALLOC: HighSpeedImpactDuplicateEntry[8] - same-frame kinetic packet dedupe ring - owner: PlayerCriticalProceduralAudioRenderer
        private readonly HighSpeedImpactDuplicateEntry[] _recentHighSpeedImpactSignals = new HighSpeedImpactDuplicateEntry[KineticImpactDuplicateHistoryCapacity];
        private int _recentHighSpeedImpactSignalCursor;
        private int _audioQualityPolicyFrame = AudioQualityPolicyUninitializedFrame;
        private float _cachedAudioQualityWeight01 = 1f;
        private IAudioService _cachedAudioService;
        private ISpatialAudioListenerCaveReadModel _spatialAudioListenerCaveReadModel;
        private ISpatialAudioBinauralEmitterReadModel _spatialAudioBinauralEmitterReadModel;
        private int _audioServiceLookupFrame = -4096;
        private int _lastAcousticImpulseSignalFrame = -4096;
        private int _lastLaserCutterSignalFrame = -4096;
        private int _lastDirectSonarPingFrame = -4096;
        private int _lastProceduralAudioSignalFrame = -4096;
        private float _lastDirectSonarPingIntensity;
        private Vector3 _lastDirectSonarPingOrigin;
        private bool _buffersInitialized;
        private bool _runtimeRegistered;
        private bool _registered;
        private bool _slowTickRegistered;
        private bool _lateFrameRegistered;
        private bool _runtimeOwnerAborted;
        private bool _hotSwapRegistered;
#pragma warning disable CS0414
        private int _playerContextLookupFrame = -4096;
        private int _ecosystemDirectorLookupFrame = -4096;
        private int _structuralHullLookupFrame = -4096;
        private int _mapMagicBiomeFrame = -4096;
        private int _transportCoordinatorLookupFrame = -4096;
#pragma warning restore CS0414
        private GameObject _boundPlayerObject;
        private Transform _boundPlayerTransform;
        private int _boundPlayerRootEntityId;
        private HectonSurvivalSystem _playerSurvivalSystem;
        private HectonPlayerHealth _playerHealth;
        private ISubmarineHullBreachReadModel _structuralHullReadModel;
        private IPlayerTransportLifecycleOwner _activeTransportLifecycleOwner;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private IEcosystemDirectorService _ecosystemDirectorService;
        private IHectonOceanKinematicsService _oceanKinematicsService;
        private IPhysicsStateEventService _physicsStateEvents;
        private bool _physicsImpactRegistered;
        private MapMagicBridge _mapMagicBridge;
        private int _cachedBiomeId;
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
        private float _structuralPressureImpulseTickValue;
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
        private float _psychoMetricsStressTickValue;
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
        private float _audioPeakImpactEnergyJoules;
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
        private float _audioEardrumRuptureTinnitusValue;
        private float _audioLeviathanRoarAggroValue;
        private float _audioLeviathanRoarPitchScale = 1f;
        private float _audioVehicleCavitationSpeed01;
        private float _audioBubbleBoilIntensity;
        private float _audioPrologueLowPassCutoffHertz = PrologueOpenLowPassHertz;
        private float _audioPrologueLfeGain;
        private float _audioPrologueGranularStress;
        private float _audioProloguePortalBlend01;
        private uint _audioPrologueSplashdownSequence;
        private int _prologueSplashdownRemainingSamples;
        private int _prologueSplashdownTotalSamples;
        private float _prologueSplashdownGain;
        private double _prologueLfePhase;
        private double _prologueSplashdownPhase;
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
        private float _appliedReverbDecayTime;
        private float _appliedReverbWetMix;
        private float _appliedReverbOpenness = 1f;
        private bool _reverbProfileApplied;
        private bool _pendingListenerReverbProfile;
        private bool _pendingListenerReverbDefaultRestore;
        private float _pendingListenerReverbWetMix;
        private float _pendingListenerReverbDecayTime;
        private float _pendingListenerReverbOpenness;
        private int _pendingProceduralPingTriggerCount;
        private long _pendingProceduralPingStartFrame0;
        private long _pendingProceduralPingStartFrame1;
        private float _pendingProceduralPingIntensity0;
        private float _pendingProceduralPingIntensity1;
        private bool _pendingStructuralStressHapticDirty;
        private StructuralStressHapticRequest _pendingStructuralStressHaptic;
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
        private int _workerConsumedSonarSequence;
        private int _workerConsumedSonarRevision;

        private struct StructuralStressHapticRequest
        {
            public float LowFrequencyIntensity;
            public float HighFrequencyIntensity;
            public float DurationSeconds;
            public float PulseFrequencyHz;
            public byte Priority;
            public byte MotorMask;
            public byte Channel;
        }

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
        private ProloguePlasmaSynthesisState _prologuePlasmaSynthesisState;
        private SabineReverbSynthesisState _sabineReverbSynthesisState;
        private CaveConvolutionReverbSynthesisState _caveConvolutionReverbSynthesisState;
        private InteriorFdnReverbSynthesisState _interiorFdnReverbSynthesisState;
        private TinnitusSynthesisState _tinnitusSynthesisState;
        private LeviathanGranularSynthesisState _leviathanGranularSynthesisState;
        private CriticalSidechainCompressorState _criticalSidechainCompressorState;
        private long _producedSampleCount;
        private bool _nativeOutputRegistered;
        private bool _nativeOutputBridgeFailureLogged;
        private int _binauralDelayWriteIndex;
        private float _binauralNarcosisChorusPhase;
        private int _audioBufferUnderrunCount;
        private int _audioProducerUnderrunWindowActive;
        private int _lastActiveDspVoiceCount;
        private int _lastSdfSampleTimeMicroseconds;
        private int _dspProducerOverBudgetPending;
        private long _dspProducerLastOverBudgetTicks;
        private int _dspProducerTelemetryCooldownFrames;
        private int _sabineDelaySamplesA;
        private int _sabineDelaySamplesB;
        private int _sabineDelaySamplesC;
        private int _sabineDelaySamplesD;
        private bool _apexHeartbeatThreatActive;
        private int _lastPlayerStressSignalSequence;

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
        private volatile float _targetReverbAcousticDensity01;
        private volatile int _targetReverbDspTier;
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
        private volatile float _targetEardrumRuptureTinnitusValue;
        private volatile float _targetNarcosisChorusValue;
        private volatile float _targetLeviathanRoarAggroValue;
        private volatile float _targetLeviathanRoarPitchScale = 1f;
        private volatile float _targetVehicleCavitationSpeed01;
        private volatile float _targetStructuralSnapValue;
        private volatile int _targetGranularMaxVoiceCount = GranularVoiceCapacity;
        private volatile float _targetGranularBasePitchScale = 1f;
        private volatile float _targetGranularGrainLengthScale = 1f;
        private volatile float _targetGranularOverlapDensityScale = 1f;
        private volatile float _targetGranularFmModulationIndex = 1f;
        private volatile float _targetPrologueLowPassCutoffHertz = PrologueOpenLowPassHertz;
        private volatile float _targetPrologueLfeGain;
        private volatile float _targetPrologueGranularStress;
        private volatile float _targetPrologueSplashdownGain;
        private volatile float _targetProloguePortalBlend01;
        private volatile int _targetPrologueSplashdownSequence;
        private volatile int _targetPrologueStage;
        private volatile int _targetPrologueFlags;
        private float _granularVoiceUpgradeHoldSeconds;
        private int _granularVoiceUpgradeRequestedCount;
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

        // COLD ALLOC: ImpactAudioEvent[64] - main-thread physics impact bridge for the audio worker SPSC path - owner: PlayerCriticalProceduralAudioRenderer
        private readonly ImpactAudioEvent[] _impactEventQueue = new ImpactAudioEvent[ImpactEventQueueCapacity];

        private enum ReverbDspTier : byte
        {
            UnityProfileOnly = 0,
            NativeSabine = 1,
            NativeConvolution = 2
        }

        [StructLayout(LayoutKind.Explicit, Size = 128)]
        private struct SonarEchoCompositeGroup
        {
            [FieldOffset(0)]
            public AbsoluteUniversePosition Position;
            [FieldOffset(48)]
            public float DistanceMeters;
            [FieldOffset(52)]
            public float ReturnStrength;
            [FieldOffset(56)]
            public float Resonance;
            [FieldOffset(60)]
            public int HitCount;
            [FieldOffset(64)]
            public byte AudioMaterialId;
            [FieldOffset(65)] private byte _pad0;
            [FieldOffset(66)] private byte _pad1;
            [FieldOffset(67)] private byte _pad2;
            [FieldOffset(68)] private byte _pad3;
            [FieldOffset(69)] private byte _pad4;
            [FieldOffset(70)] private byte _pad5;
            [FieldOffset(71)] private byte _pad6;
            [FieldOffset(72)] private byte _pad7;
            [FieldOffset(73)] private byte _pad8;
            [FieldOffset(74)] private byte _pad9;
            [FieldOffset(75)] private byte _pad10;
            [FieldOffset(76)] private byte _pad11;
            [FieldOffset(77)] private byte _pad12;
            [FieldOffset(78)] private byte _pad13;
            [FieldOffset(79)] private byte _pad14;
            [FieldOffset(80)] private byte _pad15;
            [FieldOffset(81)] private byte _pad16;
            [FieldOffset(82)] private byte _pad17;
            [FieldOffset(83)] private byte _pad18;
            [FieldOffset(84)] private byte _pad19;
            [FieldOffset(85)] private byte _pad20;
            [FieldOffset(86)] private byte _pad21;
            [FieldOffset(87)] private byte _pad22;
            [FieldOffset(88)] private byte _pad23;
            [FieldOffset(89)] private byte _pad24;
            [FieldOffset(90)] private byte _pad25;
            [FieldOffset(91)] private byte _pad26;
            [FieldOffset(92)] private byte _pad27;
            [FieldOffset(93)] private byte _pad28;
            [FieldOffset(94)] private byte _pad29;
            [FieldOffset(95)] private byte _pad30;
            [FieldOffset(96)] private byte _pad31;
            [FieldOffset(97)] private byte _pad32;
            [FieldOffset(98)] private byte _pad33;
            [FieldOffset(99)] private byte _pad34;
            [FieldOffset(100)] private byte _pad35;
            [FieldOffset(101)] private byte _pad36;
            [FieldOffset(102)] private byte _pad37;
            [FieldOffset(103)] private byte _pad38;
            [FieldOffset(104)] private byte _pad39;
            [FieldOffset(105)] private byte _pad40;
            [FieldOffset(106)] private byte _pad41;
            [FieldOffset(107)] private byte _pad42;
            [FieldOffset(108)] private byte _pad43;
            [FieldOffset(109)] private byte _pad44;
            [FieldOffset(110)] private byte _pad45;
            [FieldOffset(111)] private byte _pad46;
            [FieldOffset(112)] private byte _pad47;
            [FieldOffset(113)] private byte _pad48;
            [FieldOffset(114)] private byte _pad49;
            [FieldOffset(115)] private byte _pad50;
            [FieldOffset(116)] private byte _pad51;
            [FieldOffset(117)] private byte _pad52;
            [FieldOffset(118)] private byte _pad53;
            [FieldOffset(119)] private byte _pad54;
            [FieldOffset(120)] private byte _pad55;
            [FieldOffset(121)] private byte _pad56;
            [FieldOffset(122)] private byte _pad57;
            [FieldOffset(123)] private byte _pad58;
            [FieldOffset(124)] private byte _pad59;
            [FieldOffset(125)] private byte _pad60;
            [FieldOffset(126)] private byte _pad61;
            [FieldOffset(127)] private byte _pad62;

            public SonarEchoCompositeGroup(
                AbsoluteUniversePosition position,
                float distanceMeters,
                float returnStrength,
                float resonance,
                int hitCount,
                byte audioMaterialId)
                : this()
            {
                Position = position;
                DistanceMeters = distanceMeters;
                ReturnStrength = returnStrength;
                Resonance = resonance;
                HitCount = hitCount;
                AudioMaterialId = audioMaterialId;
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct HighSpeedImpactDuplicateEntry
        {
            [FieldOffset(0)]
            public uint Frame;
            [FieldOffset(4)]
            public uint Signature;
            [FieldOffset(8)]
            public byte Valid;
            [FieldOffset(9)] private byte _pad0;
            [FieldOffset(10)] private byte _pad1;
            [FieldOffset(11)] private byte _pad2;
            [FieldOffset(12)] private byte _pad3;
            [FieldOffset(13)] private byte _pad4;
            [FieldOffset(14)] private byte _pad5;
            [FieldOffset(15)] private byte _pad6;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct GranularAudioTelemetryEntry
        {
            [FieldOffset(0)]
            public uint SampleIndex;
            [FieldOffset(4)]
            public float Stress01;
            [FieldOffset(8)]
            public float StressDerivative01;
            [FieldOffset(12)]
            public float Depth01;
            [FieldOffset(16)]
            public float Impact01;
            [FieldOffset(20)]
            public float MixedSample;
            [FieldOffset(24)]
            public float PeakImpactEnergyJoules;
            [FieldOffset(28)]
            public int ActiveVoices;
            [FieldOffset(32)]
            public int VoiceLimit;
            [FieldOffset(36)]
            public int ActiveEchoTaps;
            [FieldOffset(40)]
            public uint Flags;
            [FieldOffset(44)] private byte _pad0;
            [FieldOffset(45)] private byte _pad1;
            [FieldOffset(46)] private byte _pad2;
            [FieldOffset(47)] private byte _pad3;
            [FieldOffset(48)] private byte _pad4;
            [FieldOffset(49)] private byte _pad5;
            [FieldOffset(50)] private byte _pad6;
            [FieldOffset(51)] private byte _pad7;
            [FieldOffset(52)] private byte _pad8;
            [FieldOffset(53)] private byte _pad9;
            [FieldOffset(54)] private byte _pad10;
            [FieldOffset(55)] private byte _pad11;
            [FieldOffset(56)] private byte _pad12;
            [FieldOffset(57)] private byte _pad13;
            [FieldOffset(58)] private byte _pad14;
            [FieldOffset(59)] private byte _pad15;
            [FieldOffset(60)] private byte _pad16;
            [FieldOffset(61)] private byte _pad17;
            [FieldOffset(62)] private byte _pad18;
            [FieldOffset(63)] private byte _pad19;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct PrologueAudioTransitionTelemetryEntry
        {
            [FieldOffset(0)]
            public uint Frame;
            [FieldOffset(4)]
            public uint Sequence;
            [FieldOffset(8)]
            public uint DspFlags;
            [FieldOffset(12)]
            public float UniverseVelocityMetersPerSecond;
            [FieldOffset(16)]
            public float Heat01;
            [FieldOffset(20)]
            public float LowPassCutoffHz;
            [FieldOffset(24)]
            public float LfeGain01;
            [FieldOffset(28)]
            public float GranularStress01;
            [FieldOffset(32)]
            public float SplashdownGain01;
            [FieldOffset(36)]
            public float PortalBlend01;
            [FieldOffset(40)]
            public float AudioLowPassCutoffHz;
            [FieldOffset(44)]
            public int SplashdownSamplesRemaining;
            [FieldOffset(48)]
            public byte Stage;
            [FieldOffset(49)]
            public byte Flags;
            [FieldOffset(50)]
            public byte QualityTier;
            [FieldOffset(51)]
            public byte Reserved;
            [FieldOffset(52)] private byte _pad0;
            [FieldOffset(53)] private byte _pad1;
            [FieldOffset(54)] private byte _pad2;
            [FieldOffset(55)] private byte _pad3;
            [FieldOffset(56)] private byte _pad4;
            [FieldOffset(57)] private byte _pad5;
            [FieldOffset(58)] private byte _pad6;
            [FieldOffset(59)] private byte _pad7;
            [FieldOffset(60)] private byte _pad8;
            [FieldOffset(61)] private byte _pad9;
            [FieldOffset(62)] private byte _pad10;
            [FieldOffset(63)] private byte _pad11;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct AudioSynthesisTelemetryEntry
        {
            [FieldOffset(0)]
            public long StopwatchTicks;
            [FieldOffset(8)]
            public uint Frame;
            [FieldOffset(12)]
            public uint BufferId;
            [FieldOffset(16)]
            public uint SystemId;
            [FieldOffset(20)]
            public uint ExpectedGeneration;
            [FieldOffset(24)]
            public uint ActualGeneration;
            [FieldOffset(28)]
            public uint Flags;
            [FieldOffset(32)]
            public int ActivePolyphony;
            [FieldOffset(36)]
            public int VoiceLimit;
            [FieldOffset(40)]
            public float DspMicroseconds;
            [FieldOffset(44)]
            public float GlobalQualityWeight;
            [FieldOffset(48)]
            public int FailureCode;
            [FieldOffset(52)]
            public int UnderrunCount;
            [FieldOffset(56)] private byte _pad0;
            [FieldOffset(57)] private byte _pad1;
            [FieldOffset(58)] private byte _pad2;
            [FieldOffset(59)] private byte _pad3;
            [FieldOffset(60)] private byte _pad4;
            [FieldOffset(61)] private byte _pad5;
            [FieldOffset(62)] private byte _pad6;
            [FieldOffset(63)] private byte _pad7;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct SonarTriggerState
        {
            [FieldOffset(0)]
            public long StartFrame;
            [FieldOffset(8)]
            public int Sequence;
            [FieldOffset(12)]
            public int EchoRevision;
            [FieldOffset(16)]
            public float Intensity;
            [FieldOffset(20)]
            public int EchoTapCount;
            [FieldOffset(24)]
            public int Flags;
            [FieldOffset(28)] private byte _pad0;
            [FieldOffset(29)] private byte _pad1;
            [FieldOffset(30)] private byte _pad2;
            [FieldOffset(31)] private byte _pad3;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        internal struct AudioThreadDiagnostics
        {
            [FieldOffset(0)]
            public long ProducedSampleCount;
            [FieldOffset(8)]
            public int BufferedFrames;
            [FieldOffset(12)]
            public int WritableFrames;
            [FieldOffset(16)]
            public int OverflowDropCount;
            [FieldOffset(20)]
            public int ImpactEventQueueDropCount;
            [FieldOffset(24)]
            public int ProducerRunning;
            [FieldOffset(28)] private byte _pad0;
            [FieldOffset(29)] private byte _pad1;
            [FieldOffset(30)] private byte _pad2;
            [FieldOffset(31)] private byte _pad3;
        }

        /// <summary>
        /// Sixteen-byte live tuning DTO for the structural granular synth.
        /// </summary>
        [StructLayout(LayoutKind.Explicit, Size = 16)]
        public struct GranularSynthTuningSnapshot
        {
            /// <summary>Pitch multiplier applied to newly armed grains.</summary>
            [FieldOffset(0)]
            public float BasePitchScale;
            /// <summary>Duration multiplier applied to newly armed grains.</summary>
            [FieldOffset(4)]
            public float GrainLengthScale;
            /// <summary>Spawn-density multiplier applied to new overlap events.</summary>
            [FieldOffset(8)]
            public float OverlapDensityScale;
            /// <summary>Pitch scatter and harshness scalar used for pressure-metal modulation.</summary>
            [FieldOffset(12)]
            public float FmModulationIndex;
        }

        [StructLayout(LayoutKind.Explicit, Size = 256)]
        private struct AudioParameterSnapshot
        {
            [FieldOffset(0)]
            public float HullStress;
            [FieldOffset(4)]
            public float StructuralHullStress;
            [FieldOffset(8)]
            public float StructuralHullStressVelocity;
            [FieldOffset(12)]
            public float StructuralFatigue;
            [FieldOffset(16)]
            public float StructuralSnap;
            [FieldOffset(20)]
            public float HullPressureDepth;
            [FieldOffset(24)]
            public float AbsoluteDepthMeters;
            [FieldOffset(28)]
            public float EnclosureDensityIndex;
            [FieldOffset(32)]
            public float PressureScrubberHumDrive;
            [FieldOffset(36)]
            public float PressureScrubberHumGain;
            [FieldOffset(40)]
            public float ReverbRt60Seconds;
            [FieldOffset(44)]
            public float ReverbWetMix;
            [FieldOffset(48)]
            public float ReverbOpenness;
            [FieldOffset(52)]
            public float ReverbAcousticDensity01;
            [FieldOffset(56)]
            public int ReverbDspTier;
            [FieldOffset(60)]
            public float BubbleBoilIntensity;
            [FieldOffset(64)]
            public float ThrusterBlend;
            [FieldOffset(68)]
            public float ThrusterLoad;
            [FieldOffset(72)]
            public float ThrusterRpm;
            [FieldOffset(76)]
            public float ThrusterPitch;
            [FieldOffset(80)]
            public float ThrusterPressure;
            [FieldOffset(84)]
            public float ThrusterAcceleration;
            [FieldOffset(88)]
            public float ThrusterHeavyCarry;
            [FieldOffset(92)]
            public float ThrusterDive;
            [FieldOffset(96)]
            public float VehicleCavitationSpeed01;
            [FieldOffset(100)]
            public float AbyssalLowPassMix;
            [FieldOffset(104)]
            public float HeartbeatStress;
            [FieldOffset(108)]
            public float HeartbeatOxygenDanger;
            [FieldOffset(112)]
            public float TinnitusOxygenStress;
            [FieldOffset(116)]
            public float EardrumRuptureTinnitus;
            [FieldOffset(120)]
            public float NarcosisChorus01;
            [FieldOffset(124)]
            public float LeviathanRoarAggro;
            [FieldOffset(128)]
            public float LeviathanRoarPitchScale;
            [FieldOffset(132)]
            public int HeartbeatActive;
            [FieldOffset(136)]
            public int GranularMaxVoiceCount;
            [FieldOffset(140)]
            public float GranularBasePitchScale;
            [FieldOffset(144)]
            public float GranularGrainLengthScale;
            [FieldOffset(148)]
            public float GranularOverlapDensityScale;
            [FieldOffset(152)]
            public float GranularFmModulationIndex;
            [FieldOffset(156)]
            public float BinauralAzimuthRadians;
            [FieldOffset(160)]
            public float BinauralRightDot;
            [FieldOffset(164)]
            public float BinauralItdSeconds;
            [FieldOffset(168)]
            public float BinauralShadowAmount01;
            [FieldOffset(172)]
            public float BinauralShadowCutoffHertz;
            [FieldOffset(176)]
            public float BinauralEnergy01;
            [FieldOffset(180)]
            public float BinauralWaterDensityMul;
            [FieldOffset(184)]
            public int BinauralValid;
            [FieldOffset(188)]
            public float PrologueLowPassCutoffHz;
            [FieldOffset(192)]
            public float PrologueLfeGain;
            [FieldOffset(196)]
            public float PrologueGranularStress;
            [FieldOffset(200)]
            public float PrologueSplashdownGain;
            [FieldOffset(204)]
            public float ProloguePortalBlend01;
            [FieldOffset(208)]
            public uint PrologueSplashdownSequence;
            [FieldOffset(212)]
            public int PrologueStage;
            [FieldOffset(216)]
            public int PrologueFlags;
            [FieldOffset(220)]
            public float GlobalQualityWeight;
            [FieldOffset(224)] private byte _pad4;
            [FieldOffset(225)] private byte _pad5;
            [FieldOffset(226)] private byte _pad6;
            [FieldOffset(227)] private byte _pad7;
            [FieldOffset(228)] private byte _pad8;
            [FieldOffset(229)] private byte _pad9;
            [FieldOffset(230)] private byte _pad10;
            [FieldOffset(231)] private byte _pad11;
            [FieldOffset(232)] private byte _pad12;
            [FieldOffset(233)] private byte _pad13;
            [FieldOffset(234)] private byte _pad14;
            [FieldOffset(235)] private byte _pad15;
            [FieldOffset(236)] private byte _pad16;
            [FieldOffset(237)] private byte _pad17;
            [FieldOffset(238)] private byte _pad18;
            [FieldOffset(239)] private byte _pad19;
            [FieldOffset(240)] private byte _pad20;
            [FieldOffset(241)] private byte _pad21;
            [FieldOffset(242)] private byte _pad22;
            [FieldOffset(243)] private byte _pad23;
            [FieldOffset(244)] private byte _pad24;
            [FieldOffset(245)] private byte _pad25;
            [FieldOffset(246)] private byte _pad26;
            [FieldOffset(247)] private byte _pad27;
            [FieldOffset(248)] private byte _pad28;
            [FieldOffset(249)] private byte _pad29;
            [FieldOffset(250)] private byte _pad30;
            [FieldOffset(251)] private byte _pad31;
            [FieldOffset(252)] private byte _pad32;
            [FieldOffset(253)] private byte _pad33;
            [FieldOffset(254)] private byte _pad34;
            [FieldOffset(255)] private byte _pad35;
        }

        [StructLayout(LayoutKind.Explicit, Size = 320)]
        private struct AudioParameterSnapshotSlot
        {
            [FieldOffset(0)]
            public AudioParameterSnapshot Value;
            [FieldOffset(256)]
            private AudioParameterSnapshotCacheLinePad Padding;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct AudioParameterSnapshotCacheLinePad
        {
            [FieldOffset(0)] private long _frontFence;
            [FieldOffset(8)] private long _rearFence;
            [FieldOffset(16)] private byte _pad0;
            [FieldOffset(17)] private byte _pad1;
            [FieldOffset(18)] private byte _pad2;
            [FieldOffset(19)] private byte _pad3;
            [FieldOffset(20)] private byte _pad4;
            [FieldOffset(21)] private byte _pad5;
            [FieldOffset(22)] private byte _pad6;
            [FieldOffset(23)] private byte _pad7;
            [FieldOffset(24)] private byte _pad8;
            [FieldOffset(25)] private byte _pad9;
            [FieldOffset(26)] private byte _pad10;
            [FieldOffset(27)] private byte _pad11;
            [FieldOffset(28)] private byte _pad12;
            [FieldOffset(29)] private byte _pad13;
            [FieldOffset(30)] private byte _pad14;
            [FieldOffset(31)] private byte _pad15;
            [FieldOffset(32)] private byte _pad16;
            [FieldOffset(33)] private byte _pad17;
            [FieldOffset(34)] private byte _pad18;
            [FieldOffset(35)] private byte _pad19;
            [FieldOffset(36)] private byte _pad20;
            [FieldOffset(37)] private byte _pad21;
            [FieldOffset(38)] private byte _pad22;
            [FieldOffset(39)] private byte _pad23;
            [FieldOffset(40)] private byte _pad24;
            [FieldOffset(41)] private byte _pad25;
            [FieldOffset(42)] private byte _pad26;
            [FieldOffset(43)] private byte _pad27;
            [FieldOffset(44)] private byte _pad28;
            [FieldOffset(45)] private byte _pad29;
            [FieldOffset(46)] private byte _pad30;
            [FieldOffset(47)] private byte _pad31;
            [FieldOffset(48)] private byte _pad32;
            [FieldOffset(49)] private byte _pad33;
            [FieldOffset(50)] private byte _pad34;
            [FieldOffset(51)] private byte _pad35;
            [FieldOffset(52)] private byte _pad36;
            [FieldOffset(53)] private byte _pad37;
            [FieldOffset(54)] private byte _pad38;
            [FieldOffset(55)] private byte _pad39;
            [FieldOffset(56)] private byte _pad40;
            [FieldOffset(57)] private byte _pad41;
            [FieldOffset(58)] private byte _pad42;
            [FieldOffset(59)] private byte _pad43;
            [FieldOffset(60)] private byte _pad44;
            [FieldOffset(61)] private byte _pad45;
            [FieldOffset(62)] private byte _pad46;
            [FieldOffset(63)] private byte _pad47;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct SonarEchoSpatialHashCoalesceJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<SonarEchoCompositeGroup> Candidates;
            [NoAlias] public NativeArray<SonarEchoCompositeGroup> Groups;
            [NoAlias] public NativeArray<int> GroupCount;
            public int CandidateCount;

            public void Execute()
            {
                int safeCandidateCount = math.clamp(CandidateCount, 0, Candidates.Length);
                int safeGroupCapacity = Groups.Length;
                GroupCount[0] = 0;

                for (int candidateIndex = 0; candidateIndex < safeCandidateCount; candidateIndex++)
                {
                    SonarEchoCompositeGroup candidate = Candidates[candidateIndex];
                    if (candidate.HitCount <= 0)
                        continue;

                    int hash = ResolveSonarEchoCompositeHash(in candidate.Position, candidate.AudioMaterialId);
                    int groupCount = GroupCount[0];
                    bool merged = false;
                    for (int groupIndex = 0; groupIndex < groupCount && groupIndex < safeGroupCapacity; groupIndex++)
                    {
                        SonarEchoCompositeGroup group = Groups[groupIndex];
                        if (ResolveSonarEchoCompositeHash(in group.Position, group.AudioMaterialId) != hash)
                            continue;

                        group.ReturnStrength += candidate.ReturnStrength;
                        group.Resonance += candidate.Resonance;
                        group.DistanceMeters += candidate.DistanceMeters;
                        group.HitCount += candidate.HitCount;
                        Groups[groupIndex] = group;
                        merged = true;
                        break;
                    }

                    if (merged)
                        continue;

                    int writeIndex = groupCount;
                    if (writeIndex >= safeGroupCapacity)
                        continue;

                    Groups[writeIndex] = candidate;
                    GroupCount[0] = writeIndex + 1;
                }
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        private struct PrologueSplashdownSineSweepProbeJob : IJob
        {
            [WriteOnly] public NativeArray<float> Output;
            public float NormalizedTime;

            public void Execute()
            {
                float t = math.saturate(NormalizedTime);
                float frequency = math.lerp(PrologueSplashdownSweepStartHertz, PrologueSplashdownSweepEndHertz, t);
                Output[0] = FastSine01(frequency * PrologueSplashdownDurationSeconds * t);
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct ImpactAudioEvent
        {
            [FieldOffset(0)]
            public float Stress;
            [FieldOffset(4)]
            public float Metallic;
            [FieldOffset(8)]
            public float ClangExcitation;
            [FieldOffset(12)]
            public float EchoExcitation;
            [FieldOffset(16)]
            public float EchoDelaySeconds;
            [FieldOffset(20)]
            public float EchoAttenuation;
            [FieldOffset(24)]
            public float EchoLowPassCutoffHz;
            [FieldOffset(28)]
            public float EchoPitchScale;
            [FieldOffset(32)]
            public float ThudExcitation;
            [FieldOffset(36)]
            public float ThudDurationSeconds;
            [FieldOffset(40)]
            public float ThudStartHertz;
            [FieldOffset(44)]
            public float ThudEndHertz;
            [FieldOffset(48)]
            public float ThudDistortion;
            [FieldOffset(52)]
            public float ThudLowPassCutoffHz;
            [FieldOffset(56)]
            public float EnergyJoules;
            [FieldOffset(60)]
            public float Reserved0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 256)]
        private struct HullSynthesisState
        {
            [FieldOffset(0)]
            public double PressureLfoPhase;
            [FieldOffset(8)]
            public double GrainReadCursor;
            [FieldOffset(16)]
            public double SubBassPhase;
            [FieldOffset(24)]
            public double DepthSubwooferPhase;
            [FieldOffset(32)]
            public double PressureScrubberHumPhase;
            [FieldOffset(40)]
            public double PressureScrubberHarmonicPhase;
            [FieldOffset(48)]
            public double PressureScrubberSaturationPhase;
            [FieldOffset(56)]
            public double DreadRumblePhase;
            [FieldOffset(64)]
            public double FatigueRingCarrierPhase;
            [FieldOffset(72)]
            public double FatigueRingModulationPhase;
            [FieldOffset(80)]
            public double StructuralSnapPhase;
            [FieldOffset(88)]
            public double KineticImpactThudPhase;
            [FieldOffset(96)]
            public long LastGranularImpactClusterSampleFrame;
            [FieldOffset(104)]
            public int GrainElapsedSamples;
            [FieldOffset(108)]
            public int GrainTotalSamples;
            [FieldOffset(112)]
            public int GrainAttackSamples;
            [FieldOffset(116)]
            public int GrainDecaySamples;
            [FieldOffset(120)]
            public int GrainSustainSamples;
            [FieldOffset(124)]
            public int GrainReleaseSamples;
            [FieldOffset(128)]
            public float GrainSustainLevel;
            [FieldOffset(132)]
            public float GrainGain;
            [FieldOffset(136)]
            public float GrainDerivative;
            [FieldOffset(140)]
            public uint GrainNoiseSeed;
            [FieldOffset(144)]
            public int GrainLoopLength;
            [FieldOffset(148)]
            public float GrainBandPassInput1;
            [FieldOffset(152)]
            public float GrainBandPassInput2;
            [FieldOffset(156)]
            public float GrainBandPassOutput1;
            [FieldOffset(160)]
            public float GrainBandPassOutput2;
            [FieldOffset(164)]
            public float GrainBandPassB0;
            [FieldOffset(168)]
            public float GrainBandPassB1;
            [FieldOffset(172)]
            public float GrainBandPassB2;
            [FieldOffset(176)]
            public float GrainBandPassA1;
            [FieldOffset(180)]
            public float GrainBandPassA2;
            [FieldOffset(184)]
            public float StructuralSnapEnvelope;
            [FieldOffset(188)]
            public float StructuralSnapPitchScale;
            [FieldOffset(192)]
            public float ImpactClangEnvelope;
            [FieldOffset(196)]
            public float ImpactClangFeedback;
            [FieldOffset(200)]
            public float ImpactClangLowPassState;
            [FieldOffset(204)]
            public int ImpactClangDelaySamples;
            [FieldOffset(208)]
            public int ImpactClangWriteIndex;
            [FieldOffset(212)]
            public float KineticImpactThudAgeSeconds;
            [FieldOffset(216)]
            public float KineticImpactThudDurationSeconds;
            [FieldOffset(220)]
            public float KineticImpactThudStartHertz;
            [FieldOffset(224)]
            public float KineticImpactThudEndHertz;
            [FieldOffset(228)]
            public float KineticImpactThudAmplitude;
            [FieldOffset(232)]
            public float KineticImpactThudDistortion;
            [FieldOffset(236)]
            public float KineticImpactThudLowPassCutoffHz;
            [FieldOffset(240)]
            public float KineticImpactThudLowPassState;
            [FieldOffset(244)] private byte _pad0;
            [FieldOffset(245)] private byte _pad1;
            [FieldOffset(246)] private byte _pad2;
            [FieldOffset(247)] private byte _pad3;
            [FieldOffset(248)] private byte _pad4;
            [FieldOffset(249)] private byte _pad5;
            [FieldOffset(250)] private byte _pad6;
            [FieldOffset(251)] private byte _pad7;
            [FieldOffset(252)] private byte _pad8;
            [FieldOffset(253)] private byte _pad9;
            [FieldOffset(254)] private byte _pad10;
            [FieldOffset(255)] private byte _pad11;
        }

#pragma warning disable 0649 // DSP state fields are intentionally zero-initialized and written by the procedural audio integrator.
        [StructLayout(LayoutKind.Explicit, Size = 128)]
        private struct SonarSynthesisState
        {
            [FieldOffset(0)]
            public double AttackPhase;
            [FieldOffset(8)]
            public double ChirpPhase;
            [FieldOffset(16)]
            public double FmModulatorPhase;
            [FieldOffset(24)]
            public double EchoPhase;
            [FieldOffset(32)]
            public double TailSlowPhase;
            [FieldOffset(40)]
            public double TailBeatAPhase;
            [FieldOffset(48)]
            public double TailBeatBPhase;
            [FieldOffset(56)]
            public double TailBeatCPhase;
            [FieldOffset(64)]
            public float EchoFilterInput1;
            [FieldOffset(68)]
            public float EchoFilterInput2;
            [FieldOffset(72)]
            public float EchoFilterOutput1;
            [FieldOffset(76)]
            public float EchoFilterOutput2;
            [FieldOffset(80)]
            public int ActiveSequence;
            [FieldOffset(84)]
            public int EchoWriteIndex;
            [FieldOffset(88)] private byte _pad0;
            [FieldOffset(89)] private byte _pad1;
            [FieldOffset(90)] private byte _pad2;
            [FieldOffset(91)] private byte _pad3;
            [FieldOffset(92)] private byte _pad4;
            [FieldOffset(93)] private byte _pad5;
            [FieldOffset(94)] private byte _pad6;
            [FieldOffset(95)] private byte _pad7;
            [FieldOffset(96)] private byte _pad8;
            [FieldOffset(97)] private byte _pad9;
            [FieldOffset(98)] private byte _pad10;
            [FieldOffset(99)] private byte _pad11;
            [FieldOffset(100)] private byte _pad12;
            [FieldOffset(101)] private byte _pad13;
            [FieldOffset(102)] private byte _pad14;
            [FieldOffset(103)] private byte _pad15;
            [FieldOffset(104)] private byte _pad16;
            [FieldOffset(105)] private byte _pad17;
            [FieldOffset(106)] private byte _pad18;
            [FieldOffset(107)] private byte _pad19;
            [FieldOffset(108)] private byte _pad20;
            [FieldOffset(109)] private byte _pad21;
            [FieldOffset(110)] private byte _pad22;
            [FieldOffset(111)] private byte _pad23;
            [FieldOffset(112)] private byte _pad24;
            [FieldOffset(113)] private byte _pad25;
            [FieldOffset(114)] private byte _pad26;
            [FieldOffset(115)] private byte _pad27;
            [FieldOffset(116)] private byte _pad28;
            [FieldOffset(117)] private byte _pad29;
            [FieldOffset(118)] private byte _pad30;
            [FieldOffset(119)] private byte _pad31;
            [FieldOffset(120)] private byte _pad32;
            [FieldOffset(121)] private byte _pad33;
            [FieldOffset(122)] private byte _pad34;
            [FieldOffset(123)] private byte _pad35;
            [FieldOffset(124)] private byte _pad36;
            [FieldOffset(125)] private byte _pad37;
            [FieldOffset(126)] private byte _pad38;
            [FieldOffset(127)] private byte _pad39;
        }
#pragma warning restore 0649

        [StructLayout(LayoutKind.Explicit, Size = 128)]
        private struct AmbientCurrentSynthesisState
        {
            [FieldOffset(0)]
            public double CarrierPhase;
            [FieldOffset(8)]
            public double ModulatorPhase;
            [FieldOffset(16)]
            public double SlowPhase;
            [FieldOffset(24)]
            public double NoisePhase;
            [FieldOffset(32)]
            public double PressurePhaserPhase;
            [FieldOffset(40)]
            public float LowPassState;
            [FieldOffset(44)]
            public float BandPassState;
            [FieldOffset(48)]
            public float PressurePhaserFeedbackSample;
            [FieldOffset(52)]
            public float PressurePhaserAllPassA;
            [FieldOffset(56)]
            public float PressurePhaserAllPassB;
            [FieldOffset(60)]
            public float PressurePhaserAllPassC;
            [FieldOffset(64)]
            public float PressurePhaserAllPassD;
            [FieldOffset(68)] private byte _pad0;
            [FieldOffset(69)] private byte _pad1;
            [FieldOffset(70)] private byte _pad2;
            [FieldOffset(71)] private byte _pad3;
            [FieldOffset(72)] private byte _pad4;
            [FieldOffset(73)] private byte _pad5;
            [FieldOffset(74)] private byte _pad6;
            [FieldOffset(75)] private byte _pad7;
            [FieldOffset(76)] private byte _pad8;
            [FieldOffset(77)] private byte _pad9;
            [FieldOffset(78)] private byte _pad10;
            [FieldOffset(79)] private byte _pad11;
            [FieldOffset(80)] private byte _pad12;
            [FieldOffset(81)] private byte _pad13;
            [FieldOffset(82)] private byte _pad14;
            [FieldOffset(83)] private byte _pad15;
            [FieldOffset(84)] private byte _pad16;
            [FieldOffset(85)] private byte _pad17;
            [FieldOffset(86)] private byte _pad18;
            [FieldOffset(87)] private byte _pad19;
            [FieldOffset(88)] private byte _pad20;
            [FieldOffset(89)] private byte _pad21;
            [FieldOffset(90)] private byte _pad22;
            [FieldOffset(91)] private byte _pad23;
            [FieldOffset(92)] private byte _pad24;
            [FieldOffset(93)] private byte _pad25;
            [FieldOffset(94)] private byte _pad26;
            [FieldOffset(95)] private byte _pad27;
            [FieldOffset(96)] private byte _pad28;
            [FieldOffset(97)] private byte _pad29;
            [FieldOffset(98)] private byte _pad30;
            [FieldOffset(99)] private byte _pad31;
            [FieldOffset(100)] private byte _pad32;
            [FieldOffset(101)] private byte _pad33;
            [FieldOffset(102)] private byte _pad34;
            [FieldOffset(103)] private byte _pad35;
            [FieldOffset(104)] private byte _pad36;
            [FieldOffset(105)] private byte _pad37;
            [FieldOffset(106)] private byte _pad38;
            [FieldOffset(107)] private byte _pad39;
            [FieldOffset(108)] private byte _pad40;
            [FieldOffset(109)] private byte _pad41;
            [FieldOffset(110)] private byte _pad42;
            [FieldOffset(111)] private byte _pad43;
            [FieldOffset(112)] private byte _pad44;
            [FieldOffset(113)] private byte _pad45;
            [FieldOffset(114)] private byte _pad46;
            [FieldOffset(115)] private byte _pad47;
            [FieldOffset(116)] private byte _pad48;
            [FieldOffset(117)] private byte _pad49;
            [FieldOffset(118)] private byte _pad50;
            [FieldOffset(119)] private byte _pad51;
            [FieldOffset(120)] private byte _pad52;
            [FieldOffset(121)] private byte _pad53;
            [FieldOffset(122)] private byte _pad54;
            [FieldOffset(123)] private byte _pad55;
            [FieldOffset(124)] private byte _pad56;
            [FieldOffset(125)] private byte _pad57;
            [FieldOffset(126)] private byte _pad58;
            [FieldOffset(127)] private byte _pad59;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct ImpactEchoSynthesisState
        {
            [FieldOffset(0)]
            public double CarrierPhaseA;
            [FieldOffset(8)]
            public double CarrierPhaseB;
            [FieldOffset(16)]
            public float DelayRemainingSeconds;
            [FieldOffset(20)]
            public float Excitation;
            [FieldOffset(24)]
            public float Attenuation;
            [FieldOffset(28)]
            public float LowPassCutoffHz;
            [FieldOffset(32)]
            public float LowPassState;
            [FieldOffset(36)]
            public float ElapsedSeconds;
            [FieldOffset(40)]
            public float PitchScale;
            [FieldOffset(44)] private byte _pad0;
            [FieldOffset(45)] private byte _pad1;
            [FieldOffset(46)] private byte _pad2;
            [FieldOffset(47)] private byte _pad3;
            [FieldOffset(48)] private byte _pad4;
            [FieldOffset(49)] private byte _pad5;
            [FieldOffset(50)] private byte _pad6;
            [FieldOffset(51)] private byte _pad7;
            [FieldOffset(52)] private byte _pad8;
            [FieldOffset(53)] private byte _pad9;
            [FieldOffset(54)] private byte _pad10;
            [FieldOffset(55)] private byte _pad11;
            [FieldOffset(56)] private byte _pad12;
            [FieldOffset(57)] private byte _pad13;
            [FieldOffset(58)] private byte _pad14;
            [FieldOffset(59)] private byte _pad15;
            [FieldOffset(60)] private byte _pad16;
            [FieldOffset(61)] private byte _pad17;
            [FieldOffset(62)] private byte _pad18;
            [FieldOffset(63)] private byte _pad19;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct HeartbeatSynthesisState
        {
            [FieldOffset(0)]
            public float TimeToNextBeatSeconds;
            [FieldOffset(4)]
            public float SecondaryPulseDelaySeconds;
            [FieldOffset(8)]
            public float PrimaryPulseAgeSeconds;
            [FieldOffset(12)]
            public float SecondaryPulseAgeSeconds;
            [FieldOffset(16)]
            public float DuckEnvelope;
            [FieldOffset(20)] private byte _pad0;
            [FieldOffset(21)] private byte _pad1;
            [FieldOffset(22)] private byte _pad2;
            [FieldOffset(23)] private byte _pad3;
            [FieldOffset(24)] private byte _pad4;
            [FieldOffset(25)] private byte _pad5;
            [FieldOffset(26)] private byte _pad6;
            [FieldOffset(27)] private byte _pad7;
            [FieldOffset(28)] private byte _pad8;
            [FieldOffset(29)] private byte _pad9;
            [FieldOffset(30)] private byte _pad10;
            [FieldOffset(31)] private byte _pad11;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct CriticalSidechainCompressorState
        {
            [FieldOffset(0)]
            public float Envelope;
            [FieldOffset(4)]
            public float Gain;
            [FieldOffset(8)] private byte _pad0;
            [FieldOffset(9)] private byte _pad1;
            [FieldOffset(10)] private byte _pad2;
            [FieldOffset(11)] private byte _pad3;
            [FieldOffset(12)] private byte _pad4;
            [FieldOffset(13)] private byte _pad5;
            [FieldOffset(14)] private byte _pad6;
            [FieldOffset(15)] private byte _pad7;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct TinnitusSynthesisState
        {
            [FieldOffset(0)]
            public double Phase;
            [FieldOffset(8)]
            public double RupturePhase;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct LeviathanGranularSynthesisState
        {
            [FieldOffset(0)]
            public float Envelope;
            [FieldOffset(4)]
            public float GrainAgeSeconds;
            [FieldOffset(8)]
            public float GrainDurationSeconds;
            [FieldOffset(12)]
            public float GrainPitchRatio;
            [FieldOffset(16)]
            public float GrainStartIndex;
            [FieldOffset(20)]
            public float LowPassState;
            [FieldOffset(24)]
            public float LfeBypassState;
            [FieldOffset(28)]
            public float SampleRate;
            [FieldOffset(32)]
            public uint Seed;
            [FieldOffset(36)] private byte _pad0;
            [FieldOffset(37)] private byte _pad1;
            [FieldOffset(38)] private byte _pad2;
            [FieldOffset(39)] private byte _pad3;
            [FieldOffset(40)] private byte _pad4;
            [FieldOffset(41)] private byte _pad5;
            [FieldOffset(42)] private byte _pad6;
            [FieldOffset(43)] private byte _pad7;
            [FieldOffset(44)] private byte _pad8;
            [FieldOffset(45)] private byte _pad9;
            [FieldOffset(46)] private byte _pad10;
            [FieldOffset(47)] private byte _pad11;
            [FieldOffset(48)] private byte _pad12;
            [FieldOffset(49)] private byte _pad13;
            [FieldOffset(50)] private byte _pad14;
            [FieldOffset(51)] private byte _pad15;
            [FieldOffset(52)] private byte _pad16;
            [FieldOffset(53)] private byte _pad17;
            [FieldOffset(54)] private byte _pad18;
            [FieldOffset(55)] private byte _pad19;
            [FieldOffset(56)] private byte _pad20;
            [FieldOffset(57)] private byte _pad21;
            [FieldOffset(58)] private byte _pad22;
            [FieldOffset(59)] private byte _pad23;
            [FieldOffset(60)] private byte _pad24;
            [FieldOffset(61)] private byte _pad25;
            [FieldOffset(62)] private byte _pad26;
            [FieldOffset(63)] private byte _pad27;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct InteriorFdnReverbSynthesisState
        {
            [FieldOffset(0)]
            public int WriteA;
            [FieldOffset(4)]
            public int WriteB;
            [FieldOffset(8)]
            public int WriteC;
            [FieldOffset(12)]
            public int WriteD;
            [FieldOffset(16)]
            public float DampingA;
            [FieldOffset(20)]
            public float DampingB;
            [FieldOffset(24)]
            public float DampingC;
            [FieldOffset(28)]
            public float DampingD;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct PendingImpactEchoProbe
        {
            [FieldOffset(0)]
            public float Excitation;
            [FieldOffset(4)]
            public float ExpireAt;
            [FieldOffset(8)]
            public byte Valid;
            [FieldOffset(9)] private byte _pad0;
            [FieldOffset(10)] private byte _pad1;
            [FieldOffset(11)] private byte _pad2;
            [FieldOffset(12)] private byte _pad3;
            [FieldOffset(13)] private byte _pad4;
            [FieldOffset(14)] private byte _pad5;
            [FieldOffset(15)] private byte _pad6;
        }

        [StructLayout(LayoutKind.Explicit, Size = 256)]
        private struct ThrusterSynthesisState
        {
            [FieldOffset(0)]
            public double Hum1Phase;
            [FieldOffset(8)]
            public double Hum2Phase;
            [FieldOffset(16)]
            public double Hum3Phase;
            [FieldOffset(24)]
            public double Hum4Phase;
            [FieldOffset(32)]
            public double FlowPhase;
            [FieldOffset(40)]
            public double PropCyclePhase;
            [FieldOffset(48)]
            public double CavitationCarrierPhase;
            [FieldOffset(56)]
            public double CavitationModulatorPhase;
            [FieldOffset(64)]
            public double VehicleCavitationScreechPhase;
            [FieldOffset(72)]
            public float PinkB0;
            [FieldOffset(76)]
            public float PinkB1;
            [FieldOffset(80)]
            public float PinkB2;
            [FieldOffset(84)]
            public float PinkB3;
            [FieldOffset(88)]
            public float PinkB4;
            [FieldOffset(92)]
            public float PinkB5;
            [FieldOffset(96)]
            public float PinkB6;
            [FieldOffset(100)]
            public float BandPassInput1;
            [FieldOffset(104)]
            public float BandPassInput2;
            [FieldOffset(108)]
            public float BandPassOutput1;
            [FieldOffset(112)]
            public float BandPassOutput2;
            [FieldOffset(116)]
            public float CombFeedbackSample;
            [FieldOffset(120)]
            public int CombWriteIndex;
            [FieldOffset(124)]
            public float VehicleCavitationHighPassInput;
            [FieldOffset(128)]
            public float VehicleCavitationHighPassOutput;
            [FieldOffset(132)] private byte _pad0;
            [FieldOffset(133)] private byte _pad1;
            [FieldOffset(134)] private byte _pad2;
            [FieldOffset(135)] private byte _pad3;
            [FieldOffset(136)] private byte _pad4;
            [FieldOffset(137)] private byte _pad5;
            [FieldOffset(138)] private byte _pad6;
            [FieldOffset(139)] private byte _pad7;
            [FieldOffset(140)] private byte _pad8;
            [FieldOffset(141)] private byte _pad9;
            [FieldOffset(142)] private byte _pad10;
            [FieldOffset(143)] private byte _pad11;
            [FieldOffset(144)] private byte _pad12;
            [FieldOffset(145)] private byte _pad13;
            [FieldOffset(146)] private byte _pad14;
            [FieldOffset(147)] private byte _pad15;
            [FieldOffset(148)] private byte _pad16;
            [FieldOffset(149)] private byte _pad17;
            [FieldOffset(150)] private byte _pad18;
            [FieldOffset(151)] private byte _pad19;
            [FieldOffset(152)] private byte _pad20;
            [FieldOffset(153)] private byte _pad21;
            [FieldOffset(154)] private byte _pad22;
            [FieldOffset(155)] private byte _pad23;
            [FieldOffset(156)] private byte _pad24;
            [FieldOffset(157)] private byte _pad25;
            [FieldOffset(158)] private byte _pad26;
            [FieldOffset(159)] private byte _pad27;
            [FieldOffset(160)] private byte _pad28;
            [FieldOffset(161)] private byte _pad29;
            [FieldOffset(162)] private byte _pad30;
            [FieldOffset(163)] private byte _pad31;
            [FieldOffset(164)] private byte _pad32;
            [FieldOffset(165)] private byte _pad33;
            [FieldOffset(166)] private byte _pad34;
            [FieldOffset(167)] private byte _pad35;
            [FieldOffset(168)] private byte _pad36;
            [FieldOffset(169)] private byte _pad37;
            [FieldOffset(170)] private byte _pad38;
            [FieldOffset(171)] private byte _pad39;
            [FieldOffset(172)] private byte _pad40;
            [FieldOffset(173)] private byte _pad41;
            [FieldOffset(174)] private byte _pad42;
            [FieldOffset(175)] private byte _pad43;
            [FieldOffset(176)] private byte _pad44;
            [FieldOffset(177)] private byte _pad45;
            [FieldOffset(178)] private byte _pad46;
            [FieldOffset(179)] private byte _pad47;
            [FieldOffset(180)] private byte _pad48;
            [FieldOffset(181)] private byte _pad49;
            [FieldOffset(182)] private byte _pad50;
            [FieldOffset(183)] private byte _pad51;
            [FieldOffset(184)] private byte _pad52;
            [FieldOffset(185)] private byte _pad53;
            [FieldOffset(186)] private byte _pad54;
            [FieldOffset(187)] private byte _pad55;
            [FieldOffset(188)] private byte _pad56;
            [FieldOffset(189)] private byte _pad57;
            [FieldOffset(190)] private byte _pad58;
            [FieldOffset(191)] private byte _pad59;
            [FieldOffset(192)] private byte _pad60;
            [FieldOffset(193)] private byte _pad61;
            [FieldOffset(194)] private byte _pad62;
            [FieldOffset(195)] private byte _pad63;
            [FieldOffset(196)] private byte _pad64;
            [FieldOffset(197)] private byte _pad65;
            [FieldOffset(198)] private byte _pad66;
            [FieldOffset(199)] private byte _pad67;
            [FieldOffset(200)] private byte _pad68;
            [FieldOffset(201)] private byte _pad69;
            [FieldOffset(202)] private byte _pad70;
            [FieldOffset(203)] private byte _pad71;
            [FieldOffset(204)] private byte _pad72;
            [FieldOffset(205)] private byte _pad73;
            [FieldOffset(206)] private byte _pad74;
            [FieldOffset(207)] private byte _pad75;
            [FieldOffset(208)] private byte _pad76;
            [FieldOffset(209)] private byte _pad77;
            [FieldOffset(210)] private byte _pad78;
            [FieldOffset(211)] private byte _pad79;
            [FieldOffset(212)] private byte _pad80;
            [FieldOffset(213)] private byte _pad81;
            [FieldOffset(214)] private byte _pad82;
            [FieldOffset(215)] private byte _pad83;
            [FieldOffset(216)] private byte _pad84;
            [FieldOffset(217)] private byte _pad85;
            [FieldOffset(218)] private byte _pad86;
            [FieldOffset(219)] private byte _pad87;
            [FieldOffset(220)] private byte _pad88;
            [FieldOffset(221)] private byte _pad89;
            [FieldOffset(222)] private byte _pad90;
            [FieldOffset(223)] private byte _pad91;
            [FieldOffset(224)] private byte _pad92;
            [FieldOffset(225)] private byte _pad93;
            [FieldOffset(226)] private byte _pad94;
            [FieldOffset(227)] private byte _pad95;
            [FieldOffset(228)] private byte _pad96;
            [FieldOffset(229)] private byte _pad97;
            [FieldOffset(230)] private byte _pad98;
            [FieldOffset(231)] private byte _pad99;
            [FieldOffset(232)] private byte _pad100;
            [FieldOffset(233)] private byte _pad101;
            [FieldOffset(234)] private byte _pad102;
            [FieldOffset(235)] private byte _pad103;
            [FieldOffset(236)] private byte _pad104;
            [FieldOffset(237)] private byte _pad105;
            [FieldOffset(238)] private byte _pad106;
            [FieldOffset(239)] private byte _pad107;
            [FieldOffset(240)] private byte _pad108;
            [FieldOffset(241)] private byte _pad109;
            [FieldOffset(242)] private byte _pad110;
            [FieldOffset(243)] private byte _pad111;
            [FieldOffset(244)] private byte _pad112;
            [FieldOffset(245)] private byte _pad113;
            [FieldOffset(246)] private byte _pad114;
            [FieldOffset(247)] private byte _pad115;
            [FieldOffset(248)] private byte _pad116;
            [FieldOffset(249)] private byte _pad117;
            [FieldOffset(250)] private byte _pad118;
            [FieldOffset(251)] private byte _pad119;
            [FieldOffset(252)] private byte _pad120;
            [FieldOffset(253)] private byte _pad121;
            [FieldOffset(254)] private byte _pad122;
            [FieldOffset(255)] private byte _pad123;
        }

        [StructLayout(LayoutKind.Explicit, Size = 128)]
        private struct ProloguePlasmaSynthesisState
        {
            [FieldOffset(0)]
            public double LfoPhase;
            [FieldOffset(8)]
            public float PinkB0;
            [FieldOffset(12)]
            public float PinkB1;
            [FieldOffset(16)]
            public float PinkB2;
            [FieldOffset(20)]
            public float PinkB3;
            [FieldOffset(24)]
            public float PinkB4;
            [FieldOffset(28)]
            public float PinkB5;
            [FieldOffset(32)]
            public float PinkB6;
            [FieldOffset(36)]
            public float BandPassInput1;
            [FieldOffset(40)]
            public float BandPassInput2;
            [FieldOffset(44)]
            public float BandPassOutput1;
            [FieldOffset(48)]
            public float BandPassOutput2;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct SabineReverbSynthesisState
        {
            [FieldOffset(0)]
            public int CombAWriteIndex;
            [FieldOffset(4)]
            public int CombBWriteIndex;
            [FieldOffset(8)]
            public int CombCWriteIndex;
            [FieldOffset(12)]
            public int CombDWriteIndex;
            [FieldOffset(16)]
            public float CombADampingState;
            [FieldOffset(20)]
            public float CombBDampingState;
            [FieldOffset(24)]
            public float CombCDampingState;
            [FieldOffset(28)]
            public float CombDDampingState;
            [FieldOffset(32)]
            public float WetMix;
            [FieldOffset(36)] private byte _pad0;
            [FieldOffset(37)] private byte _pad1;
            [FieldOffset(38)] private byte _pad2;
            [FieldOffset(39)] private byte _pad3;
            [FieldOffset(40)] private byte _pad4;
            [FieldOffset(41)] private byte _pad5;
            [FieldOffset(42)] private byte _pad6;
            [FieldOffset(43)] private byte _pad7;
            [FieldOffset(44)] private byte _pad8;
            [FieldOffset(45)] private byte _pad9;
            [FieldOffset(46)] private byte _pad10;
            [FieldOffset(47)] private byte _pad11;
            [FieldOffset(48)] private byte _pad12;
            [FieldOffset(49)] private byte _pad13;
            [FieldOffset(50)] private byte _pad14;
            [FieldOffset(51)] private byte _pad15;
            [FieldOffset(52)] private byte _pad16;
            [FieldOffset(53)] private byte _pad17;
            [FieldOffset(54)] private byte _pad18;
            [FieldOffset(55)] private byte _pad19;
            [FieldOffset(56)] private byte _pad20;
            [FieldOffset(57)] private byte _pad21;
            [FieldOffset(58)] private byte _pad22;
            [FieldOffset(59)] private byte _pad23;
            [FieldOffset(60)] private byte _pad24;
            [FieldOffset(61)] private byte _pad25;
            [FieldOffset(62)] private byte _pad26;
            [FieldOffset(63)] private byte _pad27;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct CaveConvolutionReverbSynthesisState
        {
            [FieldOffset(0)]
            public int WriteIndex;
            [FieldOffset(4)]
            public float DampingState;
            [FieldOffset(8)]
            public float WetMix;
            [FieldOffset(12)] private byte _pad0;
            [FieldOffset(13)] private byte _pad1;
            [FieldOffset(14)] private byte _pad2;
            [FieldOffset(15)] private byte _pad3;
        }

        /// <summary>
        /// True while the player-owned procedural critical-audio renderer is active.
        /// </summary>
        public static bool IsRuntimeInstalled => Volatile.Read(ref s_runtimeInstalled) != 0;

        /// <summary>
        /// True when this instance is the registered, enabled player-critical audio owner.
        /// </summary>
        public bool IsPlayerCriticalAudioRuntimeReady => IsPlayerCriticalAudioRuntimeUsable(this);

        /// <summary>
        /// Legacy renderer voice playback is disabled; SHINOBU_260 voice warnings are routed through `VocalCueSignal`.
        /// </summary>
        public bool IsVocalWarningPlaying => false;

        /// <summary>
        /// Legacy renderer voice playback is disabled; SHINOBU_260 owns warning IDs through the vocal bank runtime.
        /// </summary>
        public byte CurrentVocalWarningId => 0;

        /// <summary>
        /// Legacy no-op retained for external callers; SHINOBU_260 vocal playback cancellation is signal/state owned.
        /// </summary>
        public void CancelVocalWarningPlayback()
        {
        }

        /// <summary>
        /// Queues a prologue vacuum-to-ocean transition state into the SPSC DSP parameter lane.
        /// </summary>
        public bool QueuePrologueAudioTransition(in AudioTransitionState state)
        {
            if (!_audioTransitionStateLayoutValid || _prologueTransitionQueueCount >= PrologueTransitionQueueCapacity)
            {
                return false;
            }

            IDataVault guardVault = null;
            NativeArray<AudioTransitionState> prologueTransitionRing = default;
            bool queued = false;
            bool invalid = false;
            try
            {
                if (!TryAcquirePlayerCriticalMutationBuffer(
                        in _prologueTransitionRingHandle,
                        PlayerCriticalPrologueTransitionRingBufferId,
                        PrologueTransitionQueueCapacity,
                        PrologueTransitionRingMutationGuardMask,
                        out prologueTransitionRing,
                        out guardVault))
                {
                    RecordAudioSynthesisTelemetry(
                        (uint)_prologueTransitionRingHandle.BufferID,
                        AudioSynthesisFailureTelemetryLock,
                        AudioSynthesisTelemetryFlagLockContention,
                        Volatile.Read(ref _lastActiveDspVoiceCount),
                        _targetGranularMaxVoiceCount,
                        0f);
                    return false;
                }

                AudioTransitionState sanitized = SanitizePrologueAudioTransition(in state, out invalid);
                if (!TryWriteRing(prologueTransitionRing, ref _prologueTransitionWriteIndex, _prologueTransitionQueueCount, PrologueTransitionQueueCapacity, in sanitized))
                    return false;

                _prologueTransitionQueueCount++;
                queued = true;
            }
            finally
            {
                ReleasePlayerCriticalMutationGuard(guardVault, PrologueTransitionRingMutationGuardMask);
            }

            if (!queued)
                return false;

            PublishAudioParameterSnapshot();
            return !invalid;
        }

        /// <summary>
        /// Exposes the current main-thread sonar tap publish buffer for diegetic cockpit presentation.
        /// The returned array is read-only and double-buffered by the audio owner; consumers must copy it before
        /// the next publish flip if they need persistent data.
        /// </summary>
        public bool TryGetCockpitSonarEchoTaps(
            out NativeArray<SonarEchoTap>.ReadOnly taps,
            out int tapCount,
            out int sequence)
        {
            taps = default;
            tapCount = 0;
            sequence = 0;

            int activeIndex = Volatile.Read(ref _pendingSonarStateReadIndex);
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (activeIndex == 0)
            {
                if (!IsPlayerCriticalVaultHandle(in _pendingSonarEchoTapsAHandle, BufferID.PlayerCriticalPendingSonarEchoTapsA))
                    return false;

                if (!vault.TryReadOnlyHandle(in _pendingSonarEchoTapsAHandle, out taps))
                    return false;
            }
            else if (!IsPlayerCriticalVaultHandle(in _pendingSonarEchoTapsBHandle, BufferID.PlayerCriticalPendingSonarEchoTapsB) ||
                     !vault.TryReadOnlyHandle(in _pendingSonarEchoTapsBHandle, out taps))
            {
                return false;
            }

            SonarTriggerState pendingState = activeIndex == 0
                ? _pendingSonarStateA
                : _pendingSonarStateB;
            int sourceTapCount = activeIndex == 0
                ? _pendingSonarEchoTapCountA
                : _pendingSonarEchoTapCountB;

            if (!taps.IsCreated || pendingState.Sequence == 0)
                return false;

            int safeTapCount = math.clamp(sourceTapCount, 0, math.min(SonarEchoTapCapacity, taps.Length));
            if (safeTapCount <= 0)
                return false;

            tapCount = safeTapCount;
            sequence = pendingState.Sequence;
            return true;
        }

        public bool QueueHighSpeedImpactSignal(in HighSpeedImpactSignal signal)
        {
            return TryHandleHighSpeedImpactSignal(in signal);
        }

        private void Awake()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            RebuildAcousticOcclusionLayerMask();
            ResetReverbModelState();
            RefreshAudioConfiguration();
            CacheColdRegistryReferences();
            ResolveListenerReverbFilterCold();
            TryBindFromCachedRuntimeContext();
        }

        private void OnEnable()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            if (!TryRegisterRuntimeService())
                return;

            AcousticOcclusionUtility.AcquireRuntime();
            AudioSettings.OnAudioConfigurationChanged -= HandleAudioConfigurationChanged;
            AudioSettings.OnAudioConfigurationChanged += HandleAudioConfigurationChanged;
            RefreshAudioQualityPolicyCold();
            CacheColdRegistryReferences();
            ResolveListenerReverbFilterCold();
            TryRegisterHotSwapListener();
            TryRegisterPhysicsImpactListener();
            SpectrumEvents.RegisterSonarPingListener(this);
            SpectrumEvents.RegisterAcousticEchoListener(this);
            TryRegister();
            TryBindFromCachedRuntimeContext();
            StartAudioProducerThread();
        }

        private void OnDisable()
        {
            if (_runtimeOwnerAborted)
                return;

            TryUnregisterHotSwapListener();
            SpectrumEvents.UnregisterAcousticEchoListener(this);
            SpectrumEvents.UnregisterSonarPingListener(this);
            TryUnregisterPhysicsImpactListener();
            AudioSettings.OnAudioConfigurationChanged -= HandleAudioConfigurationChanged;
            UnsubscribeTransportCoordinator();
            TryUnregister();
            TryUnregisterRuntimeService();
            ClearQueuedProceduralPingTriggers();
            _cachedAudioService = null;
            _spatialAudioListenerCaveReadModel = null;
            _spatialAudioBinauralEmitterReadModel = null;
            _audioServiceLookupFrame = -4096;
            _playerRuntimeContext = null;
            _ecosystemDirectorService = null;
            _oceanKinematicsService = null;
            _physicsStateEvents = null;
            _mapMagicBridge = null;
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
            if (_runtimeOwnerAborted)
                return;

            TryUnregisterHotSwapListener();
            SpectrumEvents.UnregisterAcousticEchoListener(this);
            SpectrumEvents.UnregisterSonarPingListener(this);
            TryUnregisterPhysicsImpactListener();
            ClearQueuedProceduralPingTriggers();
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
            _cachedAudioService = null;
            _spatialAudioListenerCaveReadModel = null;
            _spatialAudioBinauralEmitterReadModel = null;
            _audioServiceLookupFrame = -4096;
            _playerRuntimeContext = null;
            _ecosystemDirectorService = null;
            _oceanKinematicsService = null;
            _physicsStateEvents = null;
            _mapMagicBridge = null;
        }

        public void OnGlobalRegistryServiceRebound(
            GlobalRegistryServiceSlot serviceSlot,
            ref object currentService)
        {
            if (_runtimeOwnerAborted)
                return;

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                IDataVault currentVault = currentService is IDataVault vault ? vault : null;
                RebindDataVault(currentVault);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.PhysicsStateManager)
            {
                RebindPhysicsStateEventService(currentService as IPhysicsStateEventService);
                return;
            }

            CacheRegistryServiceReference(serviceSlot, null, currentService);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (_runtimeOwnerAborted)
                return;

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                IDataVault currentVault = currentService is IDataVault vault ? vault : null;
                if (!ReferenceEquals(previousService, currentService))
                    RebindDataVault(currentVault);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.PhysicsStateManager)
            {
                RebindPhysicsStateEventService(currentService as IPhysicsStateEventService);
                return;
            }

            CacheRegistryServiceReference(serviceSlot, previousService, currentService);
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
            _playerContextLookupFrame = -4096;
            _structuralHullLookupFrame = -4096;
            _transportCoordinatorLookupFrame = -4096;
            _boundPlayerRootEntityId = 0;
            if (playerObject == null)
            {
                UnsubscribeTransportCoordinator();
                _structuralHullReadModel = null;
                _activeTransportLifecycleOwner = null;
                _playerRuntimeContext = null;
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

            if (_playerSurvivalSystem == null || !ReferenceEquals(_playerSurvivalSystem.gameObject, playerObject))
                playerObject.TryGetComponent(out _playerSurvivalSystem);

            if (_playerHealth == null || !ReferenceEquals(_playerHealth.gameObject, playerObject))
                playerObject.TryGetComponent(out _playerHealth);

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

        }

        private void BindToPlayerRuntimeContext(IPlayerRuntimeContext playerContext)
        {
            if (playerContext == null || !playerContext.IsInitialized || playerContext.PlayerObject == null)
                return;

            PlayerTransportCoordinator previousCoordinator = playerTransportCoordinator;
            _playerRuntimeContext = playerContext;
            _boundPlayerObject = playerContext.PlayerObject;
            _boundPlayerTransform = playerContext.PlayerTransform;
            _playerContextLookupFrame = -4096;
            _structuralHullLookupFrame = -4096;
            _transportCoordinatorLookupFrame = -4096;
            _boundPlayerRootEntityId = 0;

            Transform playerRoot = _boundPlayerTransform != null ? _boundPlayerTransform.root : null;
            if (playerRoot != null)
                _boundPlayerRootEntityId = unchecked((int)EntityId.ToULong(playerRoot.GetEntityId()));

            playerMovement = playerContext.PlayerMovement;
            playerToolManager = playerContext.ToolManager;
            playerTransportCoordinator = playerContext.PlayerTransportCoordinator;
            _playerSurvivalSystem = playerContext.SurvivalSystem;
            _playerHealth = playerContext.PlayerHealth;

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
            if (!TryResolvePlayerPoseAup(out playerAup))
                return false;

            if (!TryResolveRuntimeAup(runtimeWorldPosition, out targetAup))
                return false;

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
            if (!TryResolvePlayerPoseAup(out playerAup))
                return false;

            if (!TryResolveRuntimeAup(runtimeWorldPosition, out targetAup))
                return false;

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
            if (!TryResolvePlayerPoseAup(out playerAup))
                return false;

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
            // L19 hop2 LIVE: ACCESS_VIOLATION in Tick after WORLDDRIVER begin under -batchmode
            // (FMOD/procedural audio presentation path). Hop probes only need locomotion intent.
            if (Application.isBatchMode)
                return;

            if (deltaTime <= 0f)
                return;

            TryBindFromCachedRuntimeContext();
            EnsureAudioQualityPolicyCached();
            UpdateCaveReverb(deltaTime);
            ConsumeLaserCutterEventSignals();
            ConsumeProceduralAudioSignals();

            if (playerMovement == null)
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
                _targetReverbDspTier = (int)ReverbDspTier.UnityProfileOnly;
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
                _targetEardrumRuptureTinnitusValue = 0f;
                _targetNarcosisChorusValue = 0f;
                _targetLeviathanRoarAggroValue = 0f;
                _targetLeviathanRoarPitchScale = 1f;
                _targetStructuralSnapValue = 0f;
                _targetGranularMaxVoiceCount = ResolveGranularMaxVoiceCount();
                _granularVoiceUpgradeHoldSeconds = 0f;
                _granularVoiceUpgradeRequestedCount = _targetGranularMaxVoiceCount;
                _targetBinauralAzimuthRadians = 0f;
                _targetBinauralRightDot = 0f;
                _targetBinauralItdSeconds = 0f;
                _targetBinauralShadowAmount01 = 0f;
                _targetBinauralShadowCutoffHertz = 22000f;
                _targetBinauralEnergy01 = 0f;
                _targetBinauralWaterDensityMul = 0f;
                _targetBinauralValid = 0;
                _pendingImpactEchoProbe = default;
                _lastSpeed = 0f;
                _vehicleCavitationSpeedTickValue = 0f;
                _hasLeviathanRoarDopplerSample = false;
                _hasPendingLeviathanRoarDistance = false;
                _impactStressImpulseTickValue = 0f;
                _structuralPressureImpulseTickValue = 0f;
                _hullPressureDepthTickValue = 0f;
                _absoluteDepthTickValue = 0f;
                _pressureScrubberHumLastDepthMeters = float.MinValue;
                _heartbeatStressTickValue = 0f;
                _heartbeatOxygenDangerTickValue = 0f;
                _structuralSnapTickValue = 0f;
                _apexHeartbeatThreatActive = false;
                PublishAudioParameterSnapshot();
                return;
            }

            ConsumeLatestAcousticPingSignal();
            ConsumeHighSpeedImpactSignals();

            float impactStress = _impactStressImpulseTickValue;
            _impactStressImpulseTickValue = math.max(0f, impactStress - deltaTime * PhysicsImpactStressDecayPerSecond);
            float structuralPressureImpulse = _structuralPressureImpulseTickValue;
            _structuralPressureImpulseTickValue = math.max(
                0f,
                structuralPressureImpulse - deltaTime * PressureSignalImpulseDecayPerSecond);
            _targetEardrumRuptureTinnitusValue = math.max(
                0f,
                _targetEardrumRuptureTinnitusValue - (deltaTime * EardrumRuptureDecayPerSecond));
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
            _targetGranularMaxVoiceCount = ResolveGranularMaxVoiceCountWithHysteresis(
                ResolveGranularMaxVoiceCount(),
                deltaTime);
            float structuralBlendT = ApproximateOneMinusExpNegPositive(StructuralStressFollowSharpness * deltaTime);
            _structuralHullStressTickValue = math.lerp(_structuralHullStressTickValue, structuralStressTarget, structuralBlendT);
            _targetStructuralHullStressValue = _structuralHullStressTickValue;
            float structuralStressVelocityTarget = math.saturate(
                math.max(
                    structuralPressureImpulse,
                    math.abs(structuralStressTarget - _structuralHullStressTickValue) *
                    math.rcp(math.max(PressureCreakDerivativeReferencePerSecond * deltaTime, 0.0001f))));
            _structuralHullStressVelocityTickValue = math.lerp(
                _structuralHullStressVelocityTickValue,
                structuralStressVelocityTarget,
                structuralBlendT);
            _targetStructuralHullStressVelocityValue = _structuralHullStressVelocityTickValue;
            _targetStructuralFatigueValue = ResolveStructuralFatigue01();
            _structuralSnapTickValue = math.lerp(
                _structuralSnapTickValue,
                math.max(ResolveStructuralDamageTransient01(), structuralPressureImpulse),
                structuralBlendT);
            _targetStructuralSnapValue = _structuralSnapTickValue;
            _hullPressureDepthTickValue = ResolveHullPressureDepth01(ResolvePlayerDepthMeters());
            _targetHullPressureDepthValue = _hullPressureDepthTickValue;
            _absoluteDepthTickValue = ResolveAmbientPressureEquivalentDepthMeters();
            _targetAbsoluteDepthMeters = _absoluteDepthTickValue;
            UpdatePressureScrubberHumCache(
                _absoluteDepthTickValue,
                _hullPressureDepthTickValue,
                _targetEnclosureDensityIndex);
            float brineLowPassMix = playerMovement.IsInsideBrineLayer
                ? math.saturate(BrineLowPassMix * (playerMovement.CurrentBrineDensityMultiplier * 0.33333334f))
                : 0f;
            _targetAbyssalLowPassMix = math.max(
                ResolveAbyssalLowPassTarget(_absoluteDepthTickValue),
                brineLowPassMix);
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
            TryBindFromCachedRuntimeContext();
            UpdatePsychoMetricsHeartbeatCache();
            UpdateApexHeartbeatThreatCache();

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
            // L19 hop2 LIVE: presentation audio flushes not required for hop input validation.
            if (Application.isBatchMode)
                return;

            FlushQueuedListenerReverbProfile();
            FlushQueuedProceduralPingTriggers();
            FlushQueuedStructuralStressHaptic();
            ConsumeAcousticImpulseSignals();
            FlushSonarEchoCompositeGroups();
            PublishPendingDspProducerOverBudgetWarning();
            FlushGranularTelemetryDumpRequest();
            FlushPrologueTransitionTelemetryDumpRequest();
            FlushAudioSynthesisTelemetryDumpRequest();
            PublishAudioSpatializationBlackBoxFrame();
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
                    if (!TryJoinAudioProducerThreadNoThrow(producerThread, 0))
                        return;

                    Interlocked.Exchange(ref _audioProducerRestartRequested, 0);
                    Interlocked.Exchange(ref _audioProducerRunning, 0);
                }

                _audioProducerThread = null;
            }

            if (Interlocked.CompareExchange(ref _audioProducerRunning, 1, 0) != 0)
                return;

            Interlocked.Exchange(ref _audioProducerRestartRequested, 0);
            try
            {
                Thread nextProducerThread = new Thread(AudioProducerLoop)
                {
                    IsBackground = true,
                    Name = "Hecton8ProceduralAudioProducer",
                    Priority = HectonThreadPriorityPolicy.Resolve(HectonThreadRole.AudioProducer)
                };
                _audioProducerThread = nextProducerThread;
                nextProducerThread.Start();
            }
            catch (Exception)
            {
                Interlocked.Exchange(ref _audioProducerRestartRequested, 0);
                Interlocked.Exchange(ref _audioProducerRunning, 0);
                _audioProducerThread = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning("[PlayerCriticalProceduralAudioRenderer] Audio producer thread failed to start. Procedural audio output will retry on the next lifecycle start.");
#endif
                return;
            }

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

            if (TryJoinAudioProducerThreadNoThrow(producerThread, AudioProducerJoinTimeoutMs))
            {
                _audioProducerThread = null;
                return true;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogWarning("[PlayerCriticalProceduralAudioRenderer] Audio producer thread failed to stop within watchdog budget. Native audio buffers remain owned until the worker exits.");
#endif
            return false;
        }

        private static bool TryJoinAudioProducerThreadNoThrow(Thread producerThread, int timeoutMilliseconds)
        {
            if (producerThread == null || !producerThread.IsAlive)
                return true;

            if (ReferenceEquals(Thread.CurrentThread, producerThread))
                return false;

            try
            {
                producerThread.Join(timeoutMilliseconds);
                return !producerThread.IsAlive;
            }
            catch (Exception)
            {
                return false;
            }
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
            long producedFrames = Interlocked.Read(ref _producedSampleCount);
            bool underrunWindowActive = producedFrames > targetLeadFrames && bufferedFrames <= resolvedBlockFrames;
            if (underrunWindowActive)
            {
                if (Interlocked.Exchange(ref _audioProducerUnderrunWindowActive, 1) == 0)
                    Interlocked.Increment(ref _audioBufferUnderrunCount);
            }
            else
            {
                Interlocked.Exchange(ref _audioProducerUnderrunWindowActive, 0);
            }

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
            diagnostics.OverflowDropCount = sampleRingBuffer.OverflowDropCount;
            diagnostics.ImpactEventQueueDropCount = Volatile.Read(ref _impactEventQueueDropCount);
            diagnostics.ProducerRunning = Volatile.Read(ref _audioProducerRunning);
            diagnostics.ProducedSampleCount = Interlocked.Read(ref _producedSampleCount);
            return true;
        }

        /// <summary>
        /// Applies editor/live granular-synth tuning through the existing lock-free parameter snapshot path.
        /// </summary>
        /// <param name="basePitchScale">New-grain pitch multiplier.</param>
        /// <param name="grainLengthScale">New-grain duration multiplier.</param>
        /// <param name="overlapDensityScale">New-grain spawn density multiplier.</param>
        /// <param name="fmModulationIndex">Pitch scatter and harshness scalar.</param>
        /// <remarks>
        /// This method is main-thread/editor facing. The audio producer reads the values only after the
        /// double-buffered AudioParameterSnapshot swap.
        /// </remarks>
        public void ApplyGranularSynthTuning(
            float basePitchScale,
            float grainLengthScale,
            float overlapDensityScale,
            float fmModulationIndex)
        {
            _targetGranularBasePitchScale = math.clamp(
                FiniteOrDefault(basePitchScale, 1f),
                GranularTuningBasePitchMinimum,
                GranularTuningBasePitchMaximum);
            _targetGranularGrainLengthScale = math.clamp(
                FiniteOrDefault(grainLengthScale, 1f),
                GranularTuningGrainLengthMinimum,
                GranularTuningGrainLengthMaximum);
            _targetGranularOverlapDensityScale = math.clamp(
                FiniteOrDefault(overlapDensityScale, 1f),
                GranularTuningOverlapDensityMinimum,
                GranularTuningOverlapDensityMaximum);
            _targetGranularFmModulationIndex = math.clamp(
                FiniteOrDefault(fmModulationIndex, 1f),
                GranularTuningFmModulationMinimum,
                GranularTuningFmModulationMaximum);
            PublishAudioParameterSnapshot();
        }

        /// <summary>
        /// Applies editor-facing structural DSP tuning without opening a managed audio-source route.
        /// </summary>
        public void ApplyAbyssalDspTuning(
            float maxPolyphony,
            float baseGrainLengthMs,
            float distanceAttenuationCurve,
            float globalQualityWeightOverride,
            float basePitchScale,
            float overlapDensityScale,
            float fmModulationIndex)
        {
            float quality = math.saturate(FiniteOrDefault(globalQualityWeightOverride, 1f));
            int qualityLimitedVoices = math.clamp(
                (int)math.round(math.lerp(GranularMinimumQualityVoiceCapacity, GranularVoiceCapacity, quality)),
                GranularMinimumQualityVoiceCapacity,
                GranularVoiceCapacity);
            int requestedVoices = math.clamp(
                (int)math.round(FiniteOrDefault(maxPolyphony, qualityLimitedVoices)),
                GranularMinimumQualityVoiceCapacity,
                GranularVoiceCapacity);
            _targetGranularMaxVoiceCount = math.min(requestedVoices, qualityLimitedVoices);
            _targetGranularBasePitchScale = math.clamp(
                FiniteOrDefault(basePitchScale, 1f),
                GranularTuningBasePitchMinimum,
                GranularTuningBasePitchMaximum);
            _targetGranularGrainLengthScale = math.clamp(
                FiniteOrDefault(baseGrainLengthMs, GranularMaximumGrainSeconds * 1000f) *
                math.rcp(GranularMaximumGrainSeconds * 1000f),
                GranularTuningGrainLengthMinimum,
                GranularTuningGrainLengthMaximum);
            _targetGranularOverlapDensityScale = math.clamp(
                FiniteOrDefault(overlapDensityScale, 1f),
                GranularTuningOverlapDensityMinimum,
                GranularTuningOverlapDensityMaximum);
            _targetGranularFmModulationIndex = math.clamp(
                FiniteOrDefault(fmModulationIndex, 1f),
                GranularTuningFmModulationMinimum,
                GranularTuningFmModulationMaximum);
            _targetBinauralWaterDensityMul = math.saturate(FiniteOrDefault(distanceAttenuationCurve, _targetBinauralWaterDensityMul));
            PublishAudioParameterSnapshot();
        }

        /// <summary>
        /// Reads the last granular-synth tuning values accepted by the main thread.
        /// </summary>
        /// <param name="snapshot">Returned sixteen-byte tuning snapshot.</param>
        /// <returns>True when a snapshot was written.</returns>
        public bool TryGetGranularSynthTuning(out GranularSynthTuningSnapshot snapshot)
        {
            snapshot = new GranularSynthTuningSnapshot
            {
                BasePitchScale = _targetGranularBasePitchScale,
                GrainLengthScale = _targetGranularGrainLengthScale,
                OverlapDensityScale = _targetGranularOverlapDensityScale,
                FmModulationIndex = _targetGranularFmModulationIndex
            };
            return true;
        }

        /// <summary>
        /// Copies recent granular telemetry samples into an editor oscilloscope buffer.
        /// </summary>
        /// <param name="destination">Destination float buffer owned by the editor window.</param>
        /// <param name="destinationOffset">First destination index to write.</param>
        /// <param name="sampleCount">Maximum number of samples to copy.</param>
        /// <returns>True when at least one sample was copied.</returns>
        /// <remarks>
        /// This is a cold editor/debug readback path. Runtime synthesis still writes native frame buffers.
        /// </remarks>
        public bool TryCopyLatestGranularOscilloscope(float[] destination, int destinationOffset, int sampleCount)
        {
            bool hasTelemetryRing = TryReadGranularTelemetryRing(out NativeArray<GranularAudioTelemetryEntry>.ReadOnly granularTelemetryRing);
            if (destination == null ||
                !hasTelemetryRing ||
                destinationOffset < 0 ||
                destinationOffset >= destination.Length ||
                sampleCount <= 0)
            {
                return false;
            }

            int safeCount = math.min(
                sampleCount,
                math.min(destination.Length - destinationOffset, granularTelemetryRing.Length));
            if (safeCount <= 0)
                return false;

            int readCursor = _granularTelemetryCursor - safeCount;
            while (readCursor < 0)
                readCursor += granularTelemetryRing.Length;

            for (int i = 0; i < safeCount; i++)
            {
                destination[destinationOffset + i] = granularTelemetryRing[readCursor].MixedSample;
                readCursor++;
                if (readCursor >= granularTelemetryRing.Length)
                    readCursor = 0;
            }

            return true;
        }

        private void ProduceAudioBlock(int frameCount)
        {
            if (!CanProduceAudioBlock(
                    frameCount,
                    out GranularVoiceVaultViews granularViews,
                    out BinauralFilterVaultViews filterViews,
                    out ReverbVaultViews reverbViews,
                    out TransientDelayVaultViews transientViews,
                    out FrameScratchVaultViews frameViews,
                    out SonarTapVaultViews sonarTapViews,
                    out SonarDspVaultViews sonarDspViews))
            {
                NoteAudioSynthesisConsecutiveFailure();
                RecordAudioSynthesisTelemetry(
                    0u,
                    AudioSynthesisFailureVaultResolution,
                    AudioSynthesisTelemetryFlagStaleOrMissingHandle,
                    Volatile.Read(ref _lastActiveDspVoiceCount),
                    _targetGranularMaxVoiceCount,
                    0f);
                return;
            }

            bool recordBlockTelemetry = false;
            int blockTelemetryFailureCode = AudioSynthesisFailureNone;
            uint blockTelemetryFlags = AudioSynthesisTelemetryFlagSuccess;
            int blockTelemetryActiveVoiceCount = 0;
            int blockTelemetryVoiceLimit = GranularDisabledVoiceCapacity;
            float blockTelemetryDspMicroseconds = 0f;
            try
            {
                long solveStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                long blockStartFrame = Interlocked.Read(ref _producedSampleCount);
                TryConsumePendingSonarTrigger(blockStartFrame, frameCount);
                int parameterReadIndex = Volatile.Read(ref _audioParameterSnapshotReadIndex);
                AudioParameterSnapshot parameters = parameterReadIndex == 0
                    ? _audioParameterSnapshotA.Value
                    : _audioParameterSnapshotB.Value;

                double invSampleRate = math.rcp((double)math.max(1, _sampleRate));
                ConsumePendingImpactAudioEvents(
                    frameCount,
                    invSampleRate,
                    ref transientViews,
                    out float impactStressTarget,
                    out float impactMetallicTarget);
                float hullTarget = math.saturate(math.max(parameters.HullStress, impactStressTarget));
                float structuralHullTarget = math.saturate(parameters.StructuralHullStress);
                float structuralHullVelocityTarget = math.saturate(parameters.StructuralHullStressVelocity);
                float prologueGranularStress = math.saturate(parameters.PrologueGranularStress);
                if (prologueGranularStress > HullNoiseFloor)
                {
                    hullTarget = math.saturate(math.max(hullTarget, prologueGranularStress * 0.35f));
                    structuralHullTarget = math.saturate(math.max(structuralHullTarget, prologueGranularStress));
                    structuralHullVelocityTarget = math.saturate(math.max(structuralHullVelocityTarget, prologueGranularStress * 0.85f));
                }

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
                float pressureHumPitchScaleTarget = math.lerp(1f, PressureScrubberHumOxygenPitchMaximumScale, heartbeatOxygenDangerTarget);
                bool heartbeatActiveTarget = parameters.HeartbeatActive != 0;
                int granularMaxVoiceCount = math.clamp(
                    parameters.GranularMaxVoiceCount,
                    GranularDisabledVoiceCapacity,
                    GranularVoiceCapacity);
                float granularAccelerationPitchWobble = math.lerp(0.96f, 1.08f, thrusterAccelerationTarget);
                float granularBasePitchScale = math.clamp(
                    FiniteOrDefault(parameters.GranularBasePitchScale, 1f),
                    GranularTuningBasePitchMinimum,
                    GranularTuningBasePitchMaximum);
                float granularGrainLengthScale = math.clamp(
                    FiniteOrDefault(parameters.GranularGrainLengthScale, 1f),
                    GranularTuningGrainLengthMinimum,
                    GranularTuningGrainLengthMaximum);
                float granularOverlapDensityScale = math.clamp(
                    FiniteOrDefault(parameters.GranularOverlapDensityScale, 1f),
                    GranularTuningOverlapDensityMinimum,
                    GranularTuningOverlapDensityMaximum);
                float granularFmModulationIndex = math.clamp(
                    FiniteOrDefault(parameters.GranularFmModulationIndex, 1f),
                    GranularTuningFmModulationMinimum,
                    GranularTuningFmModulationMaximum);

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
                    pressureHumPitchScaleTarget,
                    pressureHumGainTarget,
                    impactMetallicTarget,
                    granularMaxVoiceCount,
                    granularAccelerationPitchWobble,
                    granularBasePitchScale,
                    granularGrainLengthScale,
                    granularOverlapDensityScale,
                    granularFmModulationIndex,
                    ref frameViews,
                    ref granularViews,
                    ref transientViews);
                RenderSonarBlock(frameCount, blockStartFrame, invSampleRate, granularFmModulationIndex, ref frameViews, ref sonarTapViews, ref sonarDspViews);
                RenderImpactEchoBlock(frameCount, invSampleRate, frameViews.ImpactEchoScratch);
                RenderThrusterBlock(
                    frameCount,
                    blockStartFrame,
                    invSampleRate,
                    ref frameViews,
                    ref transientViews,
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
                    ref frameViews,
                    heartbeatActiveTarget,
                    heartbeatStressTarget,
                    heartbeatOxygenDangerTarget);
                RenderBubbleBlock(
                    frameCount,
                    blockStartFrame,
                    invSampleRate,
                    ref frameViews,
                    bubbleBoilTarget,
                    absoluteDepthTarget);
                MixAndFilterBlock(
                    frameCount,
                    blockStartFrame,
                    invSampleRate,
                    parameters,
                    granularViews.MetallicGrainBank,
                    ref frameViews,
                    ref filterViews,
                    ref reverbViews);
                ApplyBinauralSpatializationBlock(frameCount, parameters, ref frameViews, ref filterViews);
                int activeVoiceCount = ResolveActiveDspVoiceCount(
                    parameters,
                    hullTarget,
                    structuralHullTarget,
                    structuralSnapTarget,
                    bubbleBoilTarget,
                    heartbeatActiveTarget,
                    _workerActiveSonarState.Sequence != 0,
                    _impactEchoSynthesisState.Excitation > HullNoiseFloor);
                Volatile.Write(ref _lastActiveDspVoiceCount, activeVoiceCount);

                bool wrote = _sampleRingBuffer.TryWriteInterleaved(frameViews.StereoMixScratch, frameCount, BinauralOutputChannels);
                if (wrote)
                {
                    Interlocked.Add(ref _producedSampleCount, frameCount);
                    ResetAudioSynthesisConsecutiveFailures();
                }
                else
                {
                    NoteAudioSynthesisConsecutiveFailure();
                }

                long solveTicks = System.Diagnostics.Stopwatch.GetTimestamp() - solveStartTicks;
                ReportDspProducerSolveTicks(solveTicks);
                recordBlockTelemetry = true;
                blockTelemetryFailureCode = wrote ? AudioSynthesisFailureNone : AudioSynthesisFailureOutputRingFull;
                blockTelemetryFlags = wrote ? AudioSynthesisTelemetryFlagSuccess : AudioSynthesisTelemetryFlagOutputUnderrun;
                blockTelemetryActiveVoiceCount = activeVoiceCount;
                blockTelemetryVoiceLimit = granularMaxVoiceCount;
                blockTelemetryDspMicroseconds = TicksToMicroseconds(solveTicks);
            }
            finally
            {
                ReleaseSonarDspMutationGuard(ref sonarDspViews);
                ReleaseSonarTapMutationGuard(ref sonarTapViews);
                ReleaseGranularVoiceMutationGuard(ref granularViews);
                ReleaseBinauralFilterMutationGuard(ref filterViews);
                ReleaseReverbMutationGuard(ref reverbViews);
                ReleaseTransientDelayMutationGuard(ref transientViews);
                ReleaseFrameScratchMutationGuard(ref frameViews);
            }

            if (recordBlockTelemetry)
            {
                RecordAudioSynthesisTelemetry(
                    0u,
                    blockTelemetryFailureCode,
                    blockTelemetryFlags,
                    blockTelemetryActiveVoiceCount,
                    blockTelemetryVoiceLimit,
                    blockTelemetryDspMicroseconds);
            }
        }

        private bool CanProduceAudioBlock(
            int frameCount,
            out GranularVoiceVaultViews granularViews,
            out BinauralFilterVaultViews filterViews,
            out ReverbVaultViews reverbViews,
            out TransientDelayVaultViews transientViews,
            out FrameScratchVaultViews frameViews,
            out SonarTapVaultViews sonarTapViews,
            out SonarDspVaultViews sonarDspViews)
        {
            granularViews = default;
            filterViews = default;
            reverbViews = default;
            transientViews = default;
            frameViews = default;
            sonarTapViews = default;
            sonarDspViews = default;
            if (frameCount <= 0 || _sampleRingBuffer == null)
                return false;

            if (!TryAcquirePlayerCriticalMutationGuard(AudioBlockDspMutationGuardMask, out IDataVault guardVault))
                return false;

            bool success = false;
            try
            {
                if (!TryResolveFrameScratchViews(guardVault, frameCount, out frameViews) ||
                    !HasFrameScratchBuffers(ref frameViews, frameCount))
                    return false;

                if (!TryResolveSonarTapViews(guardVault, out sonarTapViews))
                    return false;

                if (!TryResolveSonarDspViews(guardVault, out sonarDspViews))
                    return false;

                if (!TryResolveTransientDelayViews(guardVault, out transientViews) ||
                    !HasTransientDelayBuffers(ref transientViews))
                    return false;

                if (!TryResolveReverbViews(guardVault, out reverbViews) ||
                    !HasReverbBuffers(ref reverbViews))
                    return false;

                if (!TryResolveBinauralFilterViews(guardVault, out filterViews) ||
                    !HasBinauralFilterBuffers(ref filterViews))
                    return false;

                if (!TryResolveGranularVoiceViews(guardVault, out granularViews) ||
                    !HasGranularVoiceBuffers(ref granularViews))
                    return false;

                frameViews.GuardVault = guardVault;
                frameViews.GuardMask = AudioBlockDspMutationGuardMask;
                success = true;
                return true;
            }
            finally
            {
                if (!success)
                {
                    ReleasePlayerCriticalMutationGuard(guardVault, AudioBlockDspMutationGuardMask);
                    granularViews = default;
                    filterViews = default;
                    reverbViews = default;
                    transientViews = default;
                    sonarDspViews = default;
                    sonarTapViews = default;
                    frameViews = default;
                }
            }
        }

        private static int ResolveActiveDspVoiceCount(
            AudioParameterSnapshot parameters,
            float hullStress01,
            float structuralHullStress01,
            float structuralSnap01,
            float bubbleBoil01,
            bool heartbeatActive,
            bool sonarActive,
            bool impactEchoActive)
        {
            int count = 0;
            if (hullStress01 > HullNoiseFloor ||
                structuralHullStress01 > HullNoiseFloor ||
                structuralSnap01 > HullNoiseFloor)
            {
                count++;
            }

            if (sonarActive)
                count++;
            if (impactEchoActive)
                count++;
            if (parameters.ThrusterBlend > HullNoiseFloor ||
                parameters.ThrusterLoad > HullNoiseFloor ||
                parameters.VehicleCavitationSpeed01 > HullNoiseFloor)
            {
                count++;
            }

            if (heartbeatActive)
                count++;
            if (bubbleBoil01 > HullNoiseFloor)
                count++;
            if (parameters.TinnitusOxygenStress > HullNoiseFloor)
                count++;
            if (parameters.EardrumRuptureTinnitus > HullNoiseFloor)
                count++;
            if (parameters.LeviathanRoarAggro > HullNoiseFloor)
                count++;
            if (parameters.ReverbDspTier != (int)ReverbDspTier.UnityProfileOnly &&
                parameters.ReverbWetMix > HullNoiseFloor)
            {
                count++;
            }

            if (parameters.BinauralValid != 0)
                count++;
            return count;
        }

        private void PublishAudioSpatializationBlackBoxFrame()
        {
            CrashTelemetryBuffer.ReportAudioDspSpatializationFrame(
                Volatile.Read(ref _lastActiveDspVoiceCount),
                Volatile.Read(ref _lastSdfSampleTimeMicroseconds),
                Volatile.Read(ref _audioBufferUnderrunCount));
        }

        private void ReportDspProducerSolveTicks(long elapsedTicks)
        {
            _sampleRingBuffer?.RecordDspExecutionTicks(elapsedTicks);
            if (elapsedTicks <= DspProducerSolveBudgetTicks)
                return;

            Interlocked.Exchange(ref _dspProducerLastOverBudgetTicks, elapsedTicks);
            Interlocked.Exchange(ref _dspProducerOverBudgetPending, 1);
        }

        private static float TicksToMicroseconds(long elapsedTicks)
        {
            long safeTicks = elapsedTicks > 0L ? elapsedTicks : 0L;
            return (float)(safeTicks * 1000000d * math.rcp((double)System.Diagnostics.Stopwatch.Frequency));
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
            float elapsedMilliseconds = (float)(elapsedTicks * 1000d * (double)math.rcp((float)System.Diagnostics.Stopwatch.Frequency));
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
            if (!TryAcquireSonarTapViews(out SonarTapVaultViews tapViews))
            {
                _workerActiveSonarTapCount = 0;
                return;
            }

            int safeTapCount = 0;
            try
            {
                NativeArray<SonarEchoTap> sourceTapBuffer = activeIndex == 0 ? tapViews.PendingA : tapViews.PendingB;
                int sourceTapCount = activeIndex == 0 ? _pendingSonarEchoTapCountA : _pendingSonarEchoTapCountB;
                safeTapCount = math.clamp(sourceTapCount, 0, SonarEchoTapCapacity);
                if (tapViews.Worker.IsCreated && sourceTapBuffer.IsCreated)
                {
                    safeTapCount = math.min(safeTapCount, tapViews.Worker.Length);
                    for (int tapIndex = 0; tapIndex < safeTapCount; tapIndex++)
                        tapViews.Worker[tapIndex] = sourceTapBuffer[tapIndex];
                }
                else
                {
                    safeTapCount = 0;
                }
            }
            finally
            {
                ReleaseSonarTapMutationGuard(ref tapViews);
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
            float depth = ResolvePlayerDepthMeters();

            Vector3 velocity = CoreDeterminismSignals.TryGetLatestKccVelocityVector(KccVelocityAudioMaxAgeFrames, out Vector3 kccVelocity)
                ? kccVelocity
                : Vector3.zero;
            float speed = ApproximateMagnitudeNoSqrt((float3)velocity);
            float vehicleMotorSpeed = speed;

            float velocityDelta = math.abs(speed - _lastSpeed) * math.rcp(math.max(deltaTime, 0.0001f));
            _lastSpeed = speed;

            float throttleAttack = math.saturate(velocityDelta * math.rcp(math.max(throttleAttackVelocityDelta, 0.01f)));
            float shallowPressure = 1f - math.saturate(
                (depth - cavitationFadeStartDepth) *
                math.rcp(math.max(cavitationFadeEndDepth - cavitationFadeStartDepth, 0.01f)));

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
            return math.saturate((math.max(0f, speedMetersPerSecond) - VehicleCavitationScreechStartMetersPerSecond) * math.rcp(speedRange));
        }

        private void UpdateCaveReverb(float deltaTime)
        {
            ReverbDspTier reverbTier = ResolveReverbDspTier();
            _targetReverbDspTier = (int)reverbTier;

            bool shouldUseWaterReverb = playerMovement != null && playerMovement.IsPlayerSubmerged;
            if (!shouldUseWaterReverb || _boundPlayerTransform == null)
            {
                Volatile.Write(ref _lastSdfSampleTimeMicroseconds, 0);
                ResetReverbModelState();
                QueueListenerReverbDefaults();
                return;
            }

            float reverbBlendT = ApproximateOneMinusExpNegPositive(math.max(caveReverbFollowSharpness, 0.01f) * deltaTime);
            float targetDecayTime = openWaterDecayTime;
            float targetWetMix = FakeOpenWaterReverbMix01;
            float targetOpenness = 1f;
            float targetDensityIndex = 0f;
            float targetAcousticDensity01 = 0f;
            float reverbDistanceScale = math.max(caveCeilingThreshold, openWaterPresetDistance);
            float caveThreshold01 = math.saturate(caveCeilingThreshold * math.rcp(math.max(0.001f, reverbDistanceScale)));

            ISpatialAudioListenerCaveReadModel spatialAudioReadModel = ResolveSpatialAudioListenerCaveReadModel();
            if (spatialAudioReadModel != null)
            {
                float caveInterior01 = math.saturate(spatialAudioReadModel.ListenerCaveInterior01);
                bool insideCaveVolume = spatialAudioReadModel.IsListenerInsideCaveVolume;
                float effectiveCaveInterior01 = insideCaveVolume
                    ? math.saturate(caveInterior01 + caveThreshold01 * (1f - caveInterior01))
                    : 0f;
                float sabineRt60Seconds = spatialAudioReadModel.ListenerSabineRt60Seconds;
                targetDecayTime = insideCaveVolume
                    ? sabineRt60Seconds > 0f
                        ? sabineRt60Seconds
                        : math.lerp(caveDecayTime, caveDecayTime * 1.35f, effectiveCaveInterior01)
                    : openWaterDecayTime;
                targetWetMix = insideCaveVolume ? FakeCaveReverbMix01 : FakeOpenWaterReverbMix01;
                targetOpenness = insideCaveVolume ? math.lerp(0.28f, 0.12f, effectiveCaveInterior01) : 1f;
                targetDensityIndex = insideCaveVolume ? math.lerp(0.7f, 1f, effectiveCaveInterior01) : 0f;
                if (reverbTier == ReverbDspTier.NativeConvolution && insideCaveVolume)
                    targetAcousticDensity01 = ResolveCaveAcousticDensityMap01();
            }

            if (reverbTier != ReverbDspTier.UnityProfileOnly)
            {
                long sdfSampleStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                bool hasSdfEnclosure = AcousticOcclusionUtility.TryGetSdfEnclosureSample(
                    _boundPlayerTransform.position,
                    reverbDistanceScale,
                    out AcousticEnclosureResult sdfEnclosure);
                Volatile.Write(ref _lastSdfSampleTimeMicroseconds, ResolveElapsedMicroseconds(sdfSampleStartTicks));
                if (hasSdfEnclosure && sdfEnclosure.SurfaceHitCount > 0)
                {
                    float sdfClosed01 = 1f - math.saturate(sdfEnclosure.Openness01);
                    targetDecayTime = sdfEnclosure.Rt60Seconds;
                    targetWetMix = math.max(targetWetMix, sdfEnclosure.WetMix01);
                    targetOpenness = math.min(targetOpenness, sdfEnclosure.Openness01);
                    targetDensityIndex = math.max(targetDensityIndex, sdfClosed01);
                    if (reverbTier == ReverbDspTier.NativeConvolution)
                        targetAcousticDensity01 = math.max(targetAcousticDensity01, sdfClosed01);
                }
            }
            else
            {
                Volatile.Write(ref _lastSdfSampleTimeMicroseconds, 0);
            }

            if (reverbTier == ReverbDspTier.UnityProfileOnly)
            {
                targetDecayTime = ResolveMinimumQualityBiomeReverbTailSeconds(targetDecayTime);
                targetWetMix = math.min(targetWetMix, ResolveMinimumQualityBiomeReverbWetMix(targetWetMix));
                targetAcousticDensity01 = 0f;
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
            _targetReverbAcousticDensity01 = targetAcousticDensity01;
            QueueListenerReverbProfile(_smoothedReverbWetMix, _smoothedReverbDecayTime, _smoothedReverbOpenness);
        }

        private void QueueListenerReverbProfile(float wetMix01, float decayTime, float openness01)
        {
            _pendingListenerReverbWetMix = wetMix01;
            _pendingListenerReverbDecayTime = decayTime;
            _pendingListenerReverbOpenness = openness01;
            _pendingListenerReverbProfile = true;
            _pendingListenerReverbDefaultRestore = false;
        }

        private void QueueListenerReverbDefaults()
        {
            _pendingListenerReverbProfile = false;
            _pendingListenerReverbDefaultRestore = true;
        }

        private void FlushQueuedListenerReverbProfile()
        {
            ResolveListenerReverbFilter();

            if (_pendingListenerReverbDefaultRestore)
            {
                _pendingListenerReverbDefaultRestore = false;
                RestoreListenerReverbDefaults();
            }

            if (!_pendingListenerReverbProfile)
                return;

            _pendingListenerReverbProfile = false;
            ApplyListenerReverbProfile(
                _pendingListenerReverbWetMix,
                _pendingListenerReverbDecayTime,
                _pendingListenerReverbOpenness);
        }

        private ReverbDspTier ResolveReverbDspTier()
        {
            EnsureAudioQualityPolicyCached();
            float quality = _cachedAudioQualityWeight01;
            int tier = math.clamp((int)math.round(SmoothQuality01(quality) * 2f), 0, 2);
            return (ReverbDspTier)tier;
        }

        private static int ResolveElapsedMicroseconds(long startTicks)
        {
            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - startTicks;
            double microseconds = elapsedTicks * 1000000d * (double)math.rcp((float)System.Diagnostics.Stopwatch.Frequency);
            return math.clamp((int)math.max(0d, microseconds + 0.5d), 0, int.MaxValue);
        }

        private float ResolveMinimumQualityBiomeReverbTailSeconds(float fallbackSeconds)
        {
            int biomeId = ResolveCachedBiomeId();

            switch (biomeId & 3)
            {
                case 1:
                    return math.clamp(math.max(fallbackSeconds, 0.72f), 0.12f, 2.4f);
                case 2:
                    return math.clamp(math.max(fallbackSeconds, 1.05f), 0.12f, 2.4f);
                case 3:
                    return math.clamp(math.max(fallbackSeconds, 1.45f), 0.12f, 2.4f);
                default:
                    return math.clamp(math.max(fallbackSeconds, 0.48f), 0.12f, 2.4f);
            }
        }

        private static float ResolveMinimumQualityBiomeReverbWetMix(float fallbackWetMix)
        {
            return math.clamp(math.max(fallbackWetMix, 0.12f), 0f, 0.24f);
        }

        private int ResolveCachedBiomeId()
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (frame >= _mapMagicBiomeFrame &&
                frame - _mapMagicBiomeFrame < AudioServiceLookupRetryFrames)
            {
                return _cachedBiomeId;
            }

            _mapMagicBiomeFrame = frame;
            MapMagicBridge mapMagic = _mapMagicBridge;
            _cachedBiomeId = WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagic) ? mapMagic.CurrentBiomeID : 0;
            _mapMagicBridge = mapMagic;
            return _cachedBiomeId;
        }

        private static float ResolveCaveAcousticDensityMap01()
        {
            if (!WorldSpatialHashGrid.TryGetAcousticDensityMap(
                    out NativeArray<float>.ReadOnly densityMap,
                    out Vector3Int dimensions) ||
                densityMap.Length <= 0)
            {
                return 0f;
            }

            _ = dimensions;
            int sampleCount = math.min(densityMap.Length, CaveConvolutionDensitySampleLimit);
            if (sampleCount <= 0)
                return 0f;

            float densitySum = 0f;
            for (int i = 0; i < sampleCount; i++)
                densitySum += math.saturate(densityMap[i]);

            return math.saturate(densitySum * math.rcp((float)sampleCount));
        }

        private void ResetReverbModelState()
        {
            _smoothedReverbDecayTime = openWaterDecayTime;
            _smoothedReverbWetMix = 0f;
            _smoothedReverbOpenness = 1f;
            _smoothedEnclosureDensityIndex = 0f;
            _reverbProfileApplied = false;
            _targetEnclosureDensityIndex = 0f;
            _targetPressureScrubberHumDrive = 0f;
            _targetPressureScrubberHumGain = 0f;
            _targetReverbRt60Seconds = 0f;
            _targetReverbWetMix = 0f;
            _targetReverbOpenness = 1f;
            _targetReverbAcousticDensity01 = 0f;
            _targetReverbDspTier = (int)ReverbDspTier.UnityProfileOnly;
        }

        private void ResetPrologueDspState()
        {
            _targetPrologueLowPassCutoffHertz = PrologueOpenLowPassHertz;
            _targetPrologueLfeGain = 0f;
            _targetPrologueGranularStress = 0f;
            _targetPrologueSplashdownGain = 0f;
            _targetProloguePortalBlend01 = 0f;
            _targetPrologueSplashdownSequence = 0;
            _targetPrologueStage = 0;
            _targetPrologueFlags = 0;
            _audioPrologueLowPassCutoffHertz = PrologueOpenLowPassHertz;
            _audioPrologueLfeGain = 0f;
            _audioPrologueGranularStress = 0f;
            _audioProloguePortalBlend01 = 0f;
            _audioPrologueSplashdownSequence = 0u;
            _prologueSplashdownRemainingSamples = 0;
            _prologueSplashdownTotalSamples = 0;
            _prologueSplashdownGain = 0f;
            _prologueLfePhase = 0d;
            _prologueSplashdownPhase = 0d;
        }

        private void ResolveListenerReverbFilter()
        {
            EnsureReverbMixerBindings();
            if (_reverbMixerBindingsValid || _listenerReverbFilter != null)
                return;
        }

        private void ResolveListenerReverbFilterCold()
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
                    Hecton8.Core.H8Debug.LogWarning("[PlayerCriticalProceduralAudioRenderer] Missing authored AudioReverbFilter. RequireComponent should install it before runtime; reverb fallback is disabled.", this);
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
            _resolvedReverbDecayTimeParameter = ResolveMixerParameterName(reverbDecayTimeParameter);
            _resolvedReverbReflectionsLevelParameter = ResolveMixerParameterName(reverbReflectionsLevelParameter);
            _resolvedReverbRoomHighFrequencyParameter = ResolveMixerParameterName(reverbRoomHighFrequencyParameter);
            _resolvedReverbWetMixParameter = ResolveMixerParameterName(reverbWetMixParameter);

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
                    Hecton8.Core.H8Debug.LogWarning("[PlayerCriticalProceduralAudioRenderer] Reverb control mixer is missing one or more exposed parameters. Falling back to AudioReverbFilter.", this);
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
                Hecton8.Core.H8Debug.LogWarning("[PlayerCriticalProceduralAudioRenderer] Reverb wet-mix parameter missing on AudioMixer. Decay/room parameters stay mixer-driven, wet mix falls back to the default mixer state.", this);
            }
#endif
        }

        private void ApplyListenerReverbProfile(float wetMix01, float decayTime, float openness01)
        {
            float clampedDecay = math.clamp(decayTime, 0.05f, 12f);
            float clampedWetMix = math.saturate(wetMix01);
            float clampedOpenness = math.saturate(openness01);
            if (!ShouldApplyListenerReverbProfile(clampedWetMix, clampedDecay, clampedOpenness))
                return;

            float reflectionsLevel = math.lerp(caveReflectionsLevel, openWaterReflectionsLevel, clampedOpenness);
            float roomHighFrequency = math.lerp(caveRoomHighFrequency, openWaterRoomHighFrequency, clampedOpenness);
            if (_reverbMixerBindingsValid)
            {
                reverbControlMixer.SetFloat(_resolvedReverbDecayTimeParameter, clampedDecay);
                reverbControlMixer.SetFloat(_resolvedReverbReflectionsLevelParameter, reflectionsLevel);
                reverbControlMixer.SetFloat(_resolvedReverbRoomHighFrequencyParameter, roomHighFrequency);
                if (_reverbMixerWetBindingValid)
                    reverbControlMixer.SetFloat(_resolvedReverbWetMixParameter, math.lerp(MinimumMixerWetMixDb, 0f, clampedWetMix));
                CacheAppliedListenerReverbProfile(clampedWetMix, clampedDecay, clampedOpenness);
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
            CacheAppliedListenerReverbProfile(clampedWetMix, clampedDecay, clampedOpenness);
        }

        private bool ShouldApplyListenerReverbProfile(float wetMix01, float decayTime, float openness01)
        {
            if (!_reverbProfileApplied)
                return true;

            return math.abs(decayTime - _appliedReverbDecayTime) >= ReverbProfileDecayApplyStepSeconds ||
                   math.abs(wetMix01 - _appliedReverbWetMix) >= ReverbProfileWetApplyStep01 ||
                   math.abs(openness01 - _appliedReverbOpenness) >= ReverbProfileOpennessApplyStep01;
        }

        private void CacheAppliedListenerReverbProfile(float wetMix01, float decayTime, float openness01)
        {
            _appliedReverbWetMix = wetMix01;
            _appliedReverbDecayTime = decayTime;
            _appliedReverbOpenness = openness01;
            _reverbProfileApplied = true;
        }

        private void RestoreListenerReverbDefaults()
        {
            _reverbProfileApplied = false;
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
            Transform originTransform = ResolveSonarOriginTransform();
            Vector3 origin = originTransform != null ? originTransform.position : transform.position;
            TriggerSonarEchoFromOrigin(origin, originTransform, intensity);
            RecordDirectSonarPing(origin, intensity);
        }

        private void ConsumeLatestAcousticPingSignal()
        {
            if (!SignalBus<AcousticPingSignal>.TryGetLatest(out AcousticPingSignal signal, out int sequence) ||
                sequence == _lastConsumedAcousticPingSignalSequence)
            {
                return;
            }

            _lastConsumedAcousticPingSignalSequence = sequence;
            if (!IsActiveSonarAcousticPing(in signal) || signal.Intensity01 <= 0.0001f)
                return;

            if (!TryResolveRuntimeOriginRelativeVector3(in signal.PositionAup, out Vector3 origin))
                return;

            if (IsDuplicateDirectSonarPing(origin, signal.Intensity01))
                return;

            TriggerSonarEchoFromOrigin(origin, ResolveSonarOriginTransform(), signal.Intensity01);
        }

        private void ConsumeHighSpeedImpactSignals()
        {
            ReadOnlySpan<HighSpeedImpactSignal> impactSignals = SignalBus<HighSpeedImpactSignal>.GetFrameSnapshot();
            int startIndex = math.max(0, impactSignals.Length - KineticImpactSignalScanLimit);
            for (int i = startIndex; i < impactSignals.Length; i++)
                TryHandleHighSpeedImpactSignal(in impactSignals[i]);
        }

        private bool TryHandleHighSpeedImpactSignal(in HighSpeedImpactSignal signal)
        {
            if (!math.isfinite(signal.ImpactSpeed) ||
                !math.isfinite(signal.LostKineticEnergy))
            {
                return false;
            }

            uint signalSignature = ResolveHighSpeedImpactSignature(in signal);
            if (IsDuplicateHighSpeedImpactSignal(signal.Frame, signalSignature))
                return false;

            float speed = math.max(0f, signal.ImpactSpeed);
            float speedSq = speed * speed;
            float lostEnergy = math.max(0f, signal.LostKineticEnergy);
            float effectiveMass = math.isfinite(signal.EffectiveMass) ? math.max(0f, signal.EffectiveMass) : 0f;
            float derivedMass = speedSq > 0.0001f ? (lostEnergy + lostEnergy) * math.rcp(speedSq) : 0f;
            float mass = effectiveMass > 0.0001f ? effectiveMass : derivedMass;
            float kineticEnergy = math.max(0.5f * mass * speedSq, lostEnergy);
            if (!math.isfinite(kineticEnergy) || kineticEnergy < KineticImpactMinimumEnergyJoules)
                return false;

            kineticEnergy = math.clamp(kineticEnergy, KineticImpactMinimumEnergyJoules, KineticImpactMaximumSafeEnergyJoules);
            if (!TryResolveRuntimeOriginRelativeFloat3(in signal.PointAup, out float3 runtime))
                return false;

            Vector3 runtimePosition = new Vector3(runtime.x, runtime.y, runtime.z);

            float energy01 = math.saturate(kineticEnergy * math.rcp(KineticImpactReferenceEnergyJoules));
            float maxDistance = math.lerp(PhysicsImpactStressRadiusMeters, 64f, energy01);
            bool playerOwnedImpact =
                signal.SourceKind == HighSpeedImpactSignal.SourcePlayer ||
                signal.SourceKind == HighSpeedImpactSignal.SourceVehicle;
            float distance = 0f;
            if (!playerOwnedImpact &&
                !TryResolveBoundPlayerDistanceWithin(in signal.PointAup, maxDistance, out distance))
            {
                return false;
            }

            float proximity = playerOwnedImpact
                ? 1f
                : 1f - math.saturate(distance * math.rcp(math.max(maxDistance, 0.001f)));
            if (proximity <= 0.001f)
                return false;

            float waterlineY = ResolveKineticImpactWaterlineY();
            bool underwater = runtime.y < waterlineY;
            ResolveHighSpeedImpactMaterialIds(in signal, out byte primaryMaterialId, out byte secondaryMaterialId);
            ResolveImpactMaterialBlend(
                primaryMaterialId,
                secondaryMaterialId,
                out float clangMaterialMultiplier,
                out float echoMaterialMultiplier,
                out float hollowMaterialMultiplier);
            bool metalImpact = ResolveHighSpeedImpactMetal(primaryMaterialId, secondaryMaterialId);
            float lowPassCutoffHz = underwater
                ? KineticImpactWaterLowPassHertz
                : AcousticOcclusionUtility.OpenLowPassCutoffHertz;
            float thudExcitation = math.saturate((0.16f + energy01 * 0.84f) * math.max(0.18f, proximity));
            float distortion = math.saturate(
                (kineticEnergy - KineticImpactExtremeEnergyJoules) *
                math.rcp(math.max(1f, KineticImpactMaximumSafeEnergyJoules - KineticImpactExtremeEnergyJoules)));
            float impactStress = math.saturate(thudExcitation * math.lerp(0.36f, 0.72f, energy01));
            float metallic = metalImpact
                ? math.saturate(thudExcitation * 0.92f * hollowMaterialMultiplier)
                : math.saturate(thudExcitation * 0.24f);
            float clangExcitation = metalImpact
                ? math.saturate(thudExcitation * math.lerp(0.42f, 0.86f, energy01) * clangMaterialMultiplier)
                : 0f;
            float echoExcitation = math.saturate(thudExcitation * math.lerp(0.35f, 0.78f, energy01) * echoMaterialMultiplier);
            float echoDelaySeconds = math.clamp(distance * SoundSpeedWaterMetersPerSecondInv, 0f, SonarEchoMaximumDelaySeconds);
            float echoAttenuation = math.saturate(math.max(0.12f, proximity) * math.lerp(0.24f, 0.9f, energy01));
            float pitchScale = math.clamp(
                math.lerp(0.82f, 1.18f, energy01) *
                ResolveHighSpeedImpactMaterialPitchScale(primaryMaterialId, secondaryMaterialId),
                0.65f,
                1.45f);

            bool accepted = TryEnqueueImpactAudioEvent(
                impactStress,
                metallic,
                clangExcitation,
                echoExcitation,
                echoDelaySeconds,
                echoAttenuation,
                lowPassCutoffHz,
                pitchScale,
                thudExcitation,
                KineticImpactThudDurationSeconds,
                KineticImpactThudStartHertz,
                KineticImpactThudEndHertz,
                distortion,
                lowPassCutoffHz,
                kineticEnergy);
            if (!accepted)
                return false;

            TryPublishKineticImpactEchoTap(runtimePosition, distance, proximity, thudExcitation, lowPassCutoffHz, energy01);
            if (distortion > 0f)
                _targetEardrumRuptureTinnitusValue = math.max(_targetEardrumRuptureTinnitusValue, distortion);

            _impactStressImpulseTickValue = math.max(_impactStressImpulseTickValue, impactStress);
            RecordHighSpeedImpactSignal(signal.Frame, signalSignature);
            return true;
        }

        private bool IsDuplicateHighSpeedImpactSignal(uint frame, uint signature)
        {
            if (_lastHighSpeedImpactSignalValid != 0 &&
                frame == _lastHighSpeedImpactFrame &&
                signature == _lastHighSpeedImpactSignature)
            {
                return true;
            }

            for (int i = 0; i < KineticImpactDuplicateHistoryCapacity; i++)
            {
                HighSpeedImpactDuplicateEntry entry = _recentHighSpeedImpactSignals[i];
                if (entry.Valid != 0 && entry.Frame == frame && entry.Signature == signature)
                    return true;
            }

            return false;
        }

        private void RecordHighSpeedImpactSignal(uint frame, uint signature)
        {
            _lastHighSpeedImpactFrame = frame;
            _lastHighSpeedImpactSignature = signature;
            _lastHighSpeedImpactSignalValid = 1;

            int slot = _recentHighSpeedImpactSignalCursor & KineticImpactDuplicateHistoryMask;
            _recentHighSpeedImpactSignals[slot] = new HighSpeedImpactDuplicateEntry
            {
                Frame = frame,
                Signature = signature,
                Valid = 1
            };
            _recentHighSpeedImpactSignalCursor = (slot + 1) & KineticImpactDuplicateHistoryMask;
        }

        private static uint ResolveHighSpeedImpactSignature(in HighSpeedImpactSignal signal)
        {
            uint hash = 2166136261u;
            hash = (hash ^ signal.SourceHash) * 16777619u;
            hash = (hash ^ signal.TargetHash) * 16777619u;
            hash = (hash ^ math.asuint(signal.ImpactSpeed)) * 16777619u;
            hash = (hash ^ math.asuint(signal.LostKineticEnergy)) * 16777619u;
            hash = (hash ^ math.asuint(signal.EffectiveMass)) * 16777619u;
            hash = (hash ^ signal.SourceKind) * 16777619u;
            hash = (hash ^ signal.Flags) * 16777619u;
            hash = (hash ^ signal.MaterialHash) * 16777619u;
            hash = (hash ^ signal.PrimaryMaterialId) * 16777619u;
            hash = (hash ^ signal.SecondaryMaterialId) * 16777619u;
            return hash;
        }

        private static void ResolveHighSpeedImpactMaterialIds(
            in HighSpeedImpactSignal signal,
            out byte primaryMaterialId,
            out byte secondaryMaterialId)
        {
            bool hasAuthoredMaterial =
                signal.MaterialHash != 0u ||
                signal.PrimaryMaterialId != 0 ||
                signal.SecondaryMaterialId != 0;
            if (hasAuthoredMaterial)
            {
                primaryMaterialId = NormalizeHighSpeedImpactMaterialId(signal.PrimaryMaterialId);
                secondaryMaterialId = NormalizeHighSpeedImpactMaterialId(signal.SecondaryMaterialId);
                return;
            }

            byte fallback = signal.SourceKind == HighSpeedImpactSignal.SourceLeviathan
                ? (byte)ItemAudioMaterialId.Organic
                : (byte)ItemAudioMaterialId.Metal;
            primaryMaterialId = fallback;
            secondaryMaterialId = fallback;
        }

        private static byte NormalizeHighSpeedImpactMaterialId(byte materialId)
        {
            switch (materialId)
            {
                case ItemPhysicalMetadataUtility.AudioMaterialMetal:
                case ItemPhysicalMetadataUtility.AudioMaterialGlass:
                case ItemPhysicalMetadataUtility.AudioMaterialOrganic:
                    return materialId;

                default:
                    return (byte)ItemAudioMaterialId.Metal;
            }
        }

        private static bool ResolveHighSpeedImpactMetal(byte primaryMaterialId, byte secondaryMaterialId)
        {
            return primaryMaterialId == (byte)ItemAudioMaterialId.Metal ||
                   secondaryMaterialId == (byte)ItemAudioMaterialId.Metal ||
                   primaryMaterialId == (byte)ItemAudioMaterialId.Glass ||
                   secondaryMaterialId == (byte)ItemAudioMaterialId.Glass;
        }

        private static float ResolveHighSpeedImpactMaterialPitchScale(byte primaryMaterialId, byte secondaryMaterialId)
        {
            byte dominantMaterialId = ResolveDominantImpactMaterialId(primaryMaterialId, secondaryMaterialId);
            switch (dominantMaterialId)
            {
                case ItemPhysicalMetadataUtility.AudioMaterialGlass:
                    return 1.14f;

                case ItemPhysicalMetadataUtility.AudioMaterialMetal:
                    return 1.04f;

                default:
                    return 0.88f;
            }
        }

        private void EnsureKineticImpactQualityPolicyCached()
        {
            EnsureAudioQualityPolicyCached();
        }

        private void EnsureAudioQualityPolicyCached()
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_audioQualityPolicyFrame == frame)
                return;

            CacheAudioQualityPolicy(ResolveGlobalAudioQualityWeight01(), frame);
        }

        private static string ResolveMixerParameterName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            ReadOnlySpan<char> span = value.AsSpan();
            bool hasVisible = false;
            for (int i = 0; i < span.Length; i++)
            {
                if (!char.IsWhiteSpace(span[i]))
                {
                    hasVisible = true;
                    break;
                }
            }

            if (!hasVisible)
                return null;

            return HasOuterWhitespace(span) ? null : value;
        }

        private static bool HasOuterWhitespace(ReadOnlySpan<char> value)
        {
            return value.Length > 0 &&
                (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[value.Length - 1]));
        }

        private void RefreshAudioQualityPolicyCold()
        {
            CacheAudioQualityPolicy(ResolveGlobalAudioQualityWeight01(), Hecton8.Core.SystemDispatcher.CurrentFrameIndex);
        }

        private void CacheAudioQualityPolicy(float qualityWeight01, int frame)
        {
            _cachedAudioQualityWeight01 = SanitizeQuality01(qualityWeight01);
            _audioQualityPolicyFrame = frame;
        }

        private static float ResolveGlobalAudioQualityWeight01()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return SanitizeQuality01(quality);
        }

        private float ResolveCachedAudioQualityCurve01()
        {
            EnsureAudioQualityPolicyCached();
            return SmoothQuality01(_cachedAudioQualityWeight01);
        }

        private static float SanitizeQuality01(float quality)
        {
            return math.saturate(math.select(1f, quality, math.isfinite(quality)));
        }

        private static float SmoothQuality01(float quality)
        {
            float q = SanitizeQuality01(quality);
            return q * q * (3f - 2f * q);
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void TryRegisterPhysicsImpactListener()
        {
            if (_physicsImpactRegistered)
                return;

            RebindPhysicsStateEventService(GlobalRegistry.PhysicsStateEvents);
        }

        private void TryUnregisterPhysicsImpactListener()
        {
            if (!_physicsImpactRegistered)
            {
                _physicsStateEvents = null;
                return;
            }

            _physicsStateEvents?.UnregisterImpactListener(this);
            _physicsStateEvents = null;
            _physicsImpactRegistered = false;
        }

        private void RebindPhysicsStateEventService(IPhysicsStateEventService physicsStateEvents)
        {
            if (ReferenceEquals(_physicsStateEvents, physicsStateEvents) && _physicsImpactRegistered)
                return;

            if (_physicsImpactRegistered)
                _physicsStateEvents?.UnregisterImpactListener(this);

            _physicsStateEvents = physicsStateEvents;
            _physicsImpactRegistered = false;

            if (_physicsStateEvents == null ||
                !isActiveAndEnabled ||
                !IsPhysicsStateEventServiceUsable(_physicsStateEvents))
                return;

            _physicsStateEvents.RegisterImpactListener(this);
            _physicsImpactRegistered = true;
        }

        private static bool IsPhysicsStateEventServiceUsable(IPhysicsStateEventService physicsStateEvents)
        {
            return physicsStateEvents != null && physicsStateEvents.IsInitialized;
        }

        private ISpatialAudioListenerCaveReadModel ResolveSpatialAudioListenerCaveReadModel()
        {
            IAudioService audioService = _cachedAudioService;
            if (!IsAudioServiceUsable(audioService))
            {
                _cachedAudioService = null;
                _spatialAudioListenerCaveReadModel = null;
                _spatialAudioBinauralEmitterReadModel = null;
                return null;
            }

            ISpatialAudioListenerCaveReadModel readModel = _spatialAudioListenerCaveReadModel;
            if (ReferenceEquals(readModel, audioService) && IsAudioRuntimeObjectUsable(readModel))
                return readModel;

            readModel = audioService as ISpatialAudioListenerCaveReadModel;
            _spatialAudioListenerCaveReadModel = readModel;
            return IsAudioRuntimeObjectUsable(readModel) ? readModel : null;
        }

        private ISpatialAudioBinauralEmitterReadModel ResolveSpatialAudioBinauralEmitterReadModel()
        {
            IAudioService audioService = _cachedAudioService;
            if (!IsAudioServiceUsable(audioService))
            {
                _cachedAudioService = null;
                _spatialAudioListenerCaveReadModel = null;
                _spatialAudioBinauralEmitterReadModel = null;
                return null;
            }

            ISpatialAudioBinauralEmitterReadModel readModel = _spatialAudioBinauralEmitterReadModel;
            if (ReferenceEquals(readModel, audioService) && IsAudioRuntimeObjectUsable(readModel))
                return readModel;

            readModel = audioService as ISpatialAudioBinauralEmitterReadModel;
            _spatialAudioBinauralEmitterReadModel = readModel;
            return IsAudioRuntimeObjectUsable(readModel) ? readModel : null;
        }

        private void CacheAudioRuntimeService(IAudioService audioService, int frame)
        {
            bool isUsable = IsAudioServiceUsable(audioService);
            _cachedAudioService = isUsable ? audioService : null;
            _spatialAudioListenerCaveReadModel = isUsable ? audioService as ISpatialAudioListenerCaveReadModel : null;
            _spatialAudioBinauralEmitterReadModel = isUsable ? audioService as ISpatialAudioBinauralEmitterReadModel : null;
            _audioServiceLookupFrame = frame;
        }

        private static bool IsAudioServiceUsable(IAudioService audioService)
        {
            if (audioService == null || !audioService.IsAudioRuntimeReady)
                return false;

            return IsAudioRuntimeObjectUsable(audioService);
        }

        private static bool IsAudioRuntimeObjectUsable(object runtime)
        {
            if (runtime == null)
                return false;

            if (runtime is IAudioService audioService && !audioService.IsAudioRuntimeReady)
                return false;

            if (runtime is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private void CacheColdRegistryReferences()
        {
            _mapMagicBridge = null;
            WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref _mapMagicBridge);
            _ecosystemDirectorService = CacheReadyEcosystemDirector(GlobalRegistry.EcosystemDirector);
            _oceanKinematicsService = GlobalRegistry.OceanKinematics;
            _playerRuntimeContext = CacheReadyPlayerRuntime(GlobalRegistry.Player);
            if (_structuralHullReadModel == null)
                _structuralHullReadModel = GlobalRegistry.SubmarineHullBreach;
            CacheAudioRuntimeService(GlobalRegistry.Audio, Hecton8.Core.SystemDispatcher.CurrentFrameIndex);
        }

        private void CacheRegistryServiceReference(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Audio:
                    CacheAudioRuntimeService(currentService as IAudioService, Hecton8.Core.SystemDispatcher.CurrentFrameIndex);
                    break;
                case GlobalRegistryServiceSlot.MapMagicRuntime:
                case GlobalRegistryServiceSlot.TerrainProviderRuntime:
                    if (ReferenceEquals(_mapMagicBridge, previousService))
                        _mapMagicBridge = null;
                    _mapMagicBridge = currentService as MapMagicBridge;
                    WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref _mapMagicBridge);
                    _mapMagicBiomeFrame = -4096;
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntimeContext = CacheReadyPlayerRuntime(currentService as IPlayerRuntimeContext);
                    if (_playerRuntimeContext != null)
                        BindToPlayerRuntimeContext(_playerRuntimeContext);
                    break;
                case GlobalRegistryServiceSlot.EcosystemDirector:
                    _ecosystemDirectorService = CacheReadyEcosystemDirector(currentService as IEcosystemDirectorService);
                    _ecosystemDirectorLookupFrame = -4096;
                    break;
                case GlobalRegistryServiceSlot.OceanKinematics:
                    _oceanKinematicsService = currentService as IHectonOceanKinematicsService;
                    break;
                case GlobalRegistryServiceSlot.SubmarineHullBreach:
                    if (_activeTransportLifecycleOwner == null || _structuralHullReadModel == null)
                        _structuralHullReadModel = currentService as ISubmarineHullBreachReadModel;
                    _structuralHullLookupFrame = -4096;
                    break;
            }
        }

        private static IPlayerRuntimeContext CacheReadyPlayerRuntime(IPlayerRuntimeContext playerContext)
        {
            return playerContext != null && playerContext.IsInitialized ? playerContext : null;
        }

        private static IEcosystemDirectorService CacheReadyEcosystemDirector(IEcosystemDirectorService ecosystemDirector)
        {
            return ecosystemDirector != null && ecosystemDirector.IsInitialized ? ecosystemDirector : null;
        }

        private float ResolveKineticImpactWaterlineY()
        {
            if (playerMovement != null && TryResolveRuntimeKineticImpactWaterlineY(playerMovement.CurrentWaterSurfaceY, out float playerWaterlineY))
                return playerWaterlineY;

            if (TryResolveOceanKineticImpactWaterlineY(out float oceanWaterlineY))
                return oceanWaterlineY;

            return KineticImpactDefaultWaterlineY;
        }

        private bool TryResolveOceanKineticImpactWaterlineY(out float waterlineY)
        {
            IHectonOceanKinematicsService oceanKinematicsService = _oceanKinematicsService;
            IHectonOceanKinematics oceanKinematics = oceanKinematicsService != null && oceanKinematicsService.IsInitialized
                ? oceanKinematicsService.ActiveProvider
                : null;
            if (oceanKinematics != null &&
                oceanKinematics.IsAvailable &&
                TryResolveRuntimeKineticImpactWaterlineY(oceanKinematics.SeaLevel, out waterlineY))
            {
                return true;
            }

            waterlineY = KineticImpactDefaultWaterlineY;
            return false;
        }

        private static bool TryResolveRuntimeKineticImpactWaterlineY(float candidateWaterlineY, out float waterlineY)
        {
            if (math.isfinite(candidateWaterlineY) &&
                math.abs(candidateWaterlineY) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                waterlineY = candidateWaterlineY;
                return true;
            }

            waterlineY = KineticImpactDefaultWaterlineY;
            return false;
        }

        private static bool TryResolveKineticImpactWaterlineY(float candidateWaterlineY, out float waterlineY)
        {
            if (math.isfinite(candidateWaterlineY) &&
                math.abs(candidateWaterlineY) > 0.0001f &&
                math.abs(candidateWaterlineY) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                waterlineY = candidateWaterlineY;
                return true;
            }

            waterlineY = KineticImpactDefaultWaterlineY;
            return false;
        }

        private bool TryPublishKineticImpactEchoTap(
            Vector3 runtimePosition,
            float distanceMeters,
            float proximity,
            float thudExcitation,
            float lowPassCutoffHz,
            float energy01)
        {
            if (thudExcitation <= KineticImpactThudMinimumExcitation)
            {
                return false;
            }

            int inactiveIndex = 1 - Volatile.Read(ref _pendingSonarStateReadIndex);
            if (!TryAcquireSonarTapViews(out SonarTapVaultViews tapViews))
                return false;

            try
            {
                NativeArray<SonarEchoTap> inactiveTapBuffer = inactiveIndex == 0
                    ? tapViews.PendingA
                    : tapViews.PendingB;
                if (!inactiveTapBuffer.IsCreated || inactiveTapBuffer.Length <= 0)
                    return false;

                float panStereo = ResolveKineticImpactPanStereo(runtimePosition);
                float delaySeconds = math.clamp(
                    math.max(0.035f, distanceMeters * SoundSpeedWaterMetersPerSecondInv),
                    0.025f,
                    SonarEchoMaximumDelaySeconds);
                SonarEchoTap tap = BuildSonarEchoTap(
                    delaySeconds,
                    math.lerp(0.82f, 1.08f, energy01),
                    math.saturate(thudExcitation * math.max(0.12f, proximity)),
                    panStereo,
                    lowPassCutoffHz);
                inactiveTapBuffer[0] = tap;
                const int tapCount = 1;

                PublishPendingSonarState(
                    inactiveIndex,
                    new SonarTriggerState
                    {
                        Sequence = Interlocked.Increment(ref _pendingSonarSequence),
                        EchoRevision = 3,
                        StartFrame = Interlocked.Read(ref _producedSampleCount),
                        Intensity = math.saturate(thudExcitation * KineticImpactPortalEchoMasterGain),
                        EchoTapCount = tapCount,
                        Flags = SonarTriggerFlagKineticImpactEcho
                    },
                    tapCount);
                return true;
            }
            finally
            {
                ReleaseSonarTapMutationGuard(ref tapViews);
            }
        }

        private float ResolveKineticImpactPanStereo(Vector3 runtimePosition)
        {
            if (_boundPlayerTransform == null || !IsFiniteVector(runtimePosition))
                return 0f;

            Vector3 toImpact = runtimePosition - _boundPlayerTransform.position;
            float magnitudeSq = toImpact.sqrMagnitude;
            if (magnitudeSq <= 0.0001f || !math.isfinite(magnitudeSq))
                return 0f;

            Vector3 direction = toImpact * math.rsqrt(magnitudeSq);
            return math.clamp(Vector3.Dot(_boundPlayerTransform.right, direction), -1f, 1f);
        }

        private static bool IsActiveSonarAcousticPing(in AcousticPingSignal signal)
        {
            return signal.Channel == AcousticPingSignal.ChannelActiveSonar ||
                   (signal.Flags & AcousticPingSignal.FlagActiveSonar) != 0;
        }

        private Transform ResolveSonarOriginTransform()
        {
            Transform originTransform = _boundPlayerTransform;
            if (originTransform == null)
            {
                TryBindFromCachedRuntimeContext();
                originTransform = _boundPlayerTransform;
            }

            return originTransform;
        }

        private void TriggerSonarEchoFromOrigin(Vector3 origin, Transform originTransform, float intensity)
        {
            if (!IsFiniteVector(origin))
                return;

            long scheduledStartFrame = Interlocked.Read(ref _producedSampleCount);
            int sequence = Interlocked.Increment(ref _pendingSonarSequence);
            int inactiveIndex = 1 - Volatile.Read(ref _pendingSonarStateReadIndex);
            float clampedIntensity = math.saturate(intensity);
            int tapCount = 0;
            int echoRevision = 2;
            bool sdfJobBusy = false;

            if (TryRunSdfSonarEchoPass(
                    origin,
                    originTransform,
                    clampedIntensity,
                    sequence,
                    scheduledStartFrame))
            {
                TryPublishPredatorPingBack(origin, clampedIntensity);
                RaiseProceduralPingTriggered(scheduledStartFrame, clampedIntensity);
                return;
            }

            if (sdfJobBusy)
            {
                PublishPendingSonarState(
                    inactiveIndex,
                    new SonarTriggerState
                    {
                        Sequence = sequence,
                        EchoRevision = 1,
                        StartFrame = scheduledStartFrame,
                        Intensity = clampedIntensity,
                        EchoTapCount = 0
                    },
                    0);
                TryPublishPredatorPingBack(origin, clampedIntensity);
                RaiseProceduralPingTriggered(scheduledStartFrame, clampedIntensity);
                return;
            }

            if (!TryAcquireSonarTapViews(out SonarTapVaultViews tapViews))
                return;

            try
            {
                NativeArray<SonarEchoTap> inactiveTapBuffer = inactiveIndex == 0 ? tapViews.PendingA : tapViews.PendingB;
                if (inactiveTapBuffer.IsCreated)
                {
                    tapCount = BuildSdfSonarEchoTaps(origin, originTransform, clampedIntensity, inactiveTapBuffer);
                    TryAppendPredatorFleshEchoTapToBuffer(origin, originTransform, clampedIntensity, inactiveTapBuffer, ref tapCount);
                    if (tapCount <= 0 && sonarSdfFallbackGhostEchoes)
                    {
                        int tapLimit = math.min(SonarGhostEchoTapCount, inactiveTapBuffer.Length);
                        for (int tapIndex = 0; tapIndex < tapLimit; tapIndex++)
                            inactiveTapBuffer[tapIndex] = BuildGhostSonarEchoTap(sequence, tapIndex, clampedIntensity);
                        tapCount = tapLimit;
                        echoRevision = 1;
                    }

                    TryPublishPredatorPingBack(origin, clampedIntensity);
                }

                PublishPendingSonarState(
                    inactiveIndex,
                    new SonarTriggerState
                    {
                        Sequence = sequence,
                        EchoRevision = echoRevision,
                        StartFrame = scheduledStartFrame,
                        Intensity = clampedIntensity,
                        EchoTapCount = tapCount
                    },
                    tapCount);
            }
            finally
            {
                ReleaseSonarTapMutationGuard(ref tapViews);
            }

            RaiseProceduralPingTriggered(scheduledStartFrame, clampedIntensity);
        }

        private void RaiseProceduralPingTriggered(long scheduledStartFrame, float intensity)
        {
            QueueProceduralPingTrigger(scheduledStartFrame, intensity);
        }

        private void QueueProceduralPingTrigger(long scheduledStartFrame, float intensity)
        {
            if (_pendingProceduralPingTriggerCount == 0)
            {
                _pendingProceduralPingStartFrame0 = scheduledStartFrame;
                _pendingProceduralPingIntensity0 = math.saturate(intensity);
                _pendingProceduralPingTriggerCount = 1;
                return;
            }

            _pendingProceduralPingStartFrame1 = scheduledStartFrame;
            _pendingProceduralPingIntensity1 = math.saturate(intensity);
            _pendingProceduralPingTriggerCount = 2;
        }

        private void FlushQueuedProceduralPingTriggers()
        {
            int count = _pendingProceduralPingTriggerCount;
            if (count <= 0)
                return;

            _pendingProceduralPingTriggerCount = 0;
            int sampleRate = math.max(_sampleRate, 1);
            ProceduralAudioEvents.TryRaiseAudioPingTriggered(
                _pendingProceduralPingStartFrame0,
                sampleRate,
                _pendingProceduralPingIntensity0,
                SonarChirpDurationSeconds);

            if (count > 1)
            {
                ProceduralAudioEvents.TryRaiseAudioPingTriggered(
                    _pendingProceduralPingStartFrame1,
                    sampleRate,
                    _pendingProceduralPingIntensity1,
                    SonarChirpDurationSeconds);
            }

            ClearQueuedProceduralPingTriggers();
        }

        private void ClearQueuedProceduralPingTriggers()
        {
            _pendingProceduralPingTriggerCount = 0;
            _pendingProceduralPingStartFrame0 = 0L;
            _pendingProceduralPingStartFrame1 = 0L;
            _pendingProceduralPingIntensity0 = 0f;
            _pendingProceduralPingIntensity1 = 0f;
        }

        private void QueueStructuralStressHaptic(
            float lowFrequencyIntensity,
            float highFrequencyIntensity,
            float durationSeconds,
            float pulseFrequencyHz,
            byte priority,
            byte motorMask,
            byte channel)
        {
            _pendingStructuralStressHaptic.LowFrequencyIntensity = lowFrequencyIntensity;
            _pendingStructuralStressHaptic.HighFrequencyIntensity = highFrequencyIntensity;
            _pendingStructuralStressHaptic.DurationSeconds = durationSeconds;
            _pendingStructuralStressHaptic.PulseFrequencyHz = pulseFrequencyHz;
            _pendingStructuralStressHaptic.Priority = priority;
            _pendingStructuralStressHaptic.MotorMask = motorMask;
            _pendingStructuralStressHaptic.Channel = channel;
            _pendingStructuralStressHapticDirty = true;
        }

        private void FlushQueuedStructuralStressHaptic()
        {
            if (!_pendingStructuralStressHapticDirty)
                return;

            StructuralStressHapticRequest request = _pendingStructuralStressHaptic;
            _pendingStructuralStressHaptic = default;
            _pendingStructuralStressHapticDirty = false;
            Hecton8.Tools.ToolHapticsRuntime.TryEnqueueCommand(
                request.LowFrequencyIntensity,
                request.HighFrequencyIntensity,
                request.DurationSeconds,
                request.PulseFrequencyHz,
                request.Priority,
                request.MotorMask,
                request.Channel);
        }

        private void RecordDirectSonarPing(Vector3 origin, float intensity)
        {
            _lastDirectSonarPingFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            _lastDirectSonarPingIntensity = math.saturate(intensity);
            _lastDirectSonarPingOrigin = origin;
        }

        private bool IsDuplicateDirectSonarPing(Vector3 origin, float intensity)
        {
            int frameDelta = Hecton8.Core.SystemDispatcher.CurrentFrameIndex - _lastDirectSonarPingFrame;
            if ((uint)frameDelta > 2u)
                return false;

            if (math.abs(math.saturate(intensity) - _lastDirectSonarPingIntensity) > 0.02f)
                return false;

            return (origin - _lastDirectSonarPingOrigin).sqrMagnitude <= 1f;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z);
        }

        private static bool TryResolveRuntimeAup(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!IsFiniteVector(runtimePosition))
                return false;

            if (!TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup))
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return positionAup.IsFinite();
        }

        private static bool TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup)
        {
            originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            return originAup.IsFinite();
        }

        private static bool TryResolveRuntimeOriginRelativeFloat3(
            in AbsoluteUniversePosition positionAup,
            out float3 runtimePosition)
        {
            runtimePosition = default;
            if (!positionAup.IsFinite() ||
                !TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup))
            {
                return false;
            }

            double3 deltaAup = AbsoluteUniversePosition.DeltaMetersClamped(in positionAup, in originAup);
            double3 clampedDelta = math.clamp(
                deltaAup,
                new double3(-AupRuntimeFloatClampMeters),
                new double3(AupRuntimeFloatClampMeters));
            runtimePosition = new float3(
                (float)clampedDelta.x,
                (float)clampedDelta.y,
                (float)clampedDelta.z);
            return math.all(math.isfinite(runtimePosition));
        }

        private static bool TryResolveRuntimeOriginRelativeVector3(
            in AbsoluteUniversePosition positionAup,
            out Vector3 runtimePosition)
        {
            runtimePosition = default;
            if (!TryResolveRuntimeOriginRelativeFloat3(in positionAup, out float3 runtimeFloat))
                return false;

            runtimePosition = new Vector3(runtimeFloat.x, runtimeFloat.y, runtimeFloat.z);
            return true;
        }

        private bool TryRunSdfSonarEchoPass(
            Vector3 origin,
            Transform originTransform,
            float intensity,
            int sequence,
            long scheduledStartFrame)
        {
            if (!HectonVoxelVolume.TryAcquireClosestPublishedSonarSdfPayloadReadLease(
                    origin,
                    out HectonVoxelVolume publishedSdfVolume,
                    out NativeArray<byte>.ReadOnly encodedSdf,
                    out NativeArray<byte>.ReadOnly audioMaterialIds,
                    out Vector3Int gridDimensions,
                    out Vector3 volumeOrigin,
                    out Vector3 voxelCellSize,
                    out float sdfRange,
                    out int version,
                    out HectonVoxelVolume.PublishedSonarSdfReadLease publishedSdfLease))
            {
                return false;
            }

            if (!TryAcquireSonarSpatialViews(out SonarSpatialVaultViews spatialViews))
            {
                publishedSdfVolume.ReleasePublishedSonarSdfPayloadReadLease(in publishedSdfLease);
                return false;
            }

            int rayCount = 0;
            try
            {
                rayCount = math.clamp(ResolveSonarSdfProbeCount(), 1, math.min(SonarEchoTapCapacity, spatialViews.Hits.Length));
                float maxDistance = math.clamp(
                    sonarSdfMaximumProbeDistanceMeters,
                    math.max(1f, sonarSdfProbeIntervalMeters),
                    MaximumProbeDistanceMeters);
                float stepMeters = math.clamp(sonarSdfProbeIntervalMeters, 1f, maxDistance);
                Vector3 forward = NormalizeVector(originTransform != null ? originTransform.forward : Vector3.forward, Vector3.forward);
                Vector3 right = NormalizeVector(originTransform != null ? originTransform.right : Vector3.right, Vector3.right);
                Vector3 up = NormalizeVector(originTransform != null ? originTransform.up : Vector3.up, Vector3.up);
                AcousticEcholocationRaymarchJob job = new AcousticEcholocationRaymarchJob
                {
                    EncodedSdf = encodedSdf,
                    AudioMaterialIds = audioMaterialIds,
                    GridDimensions = new int3(gridDimensions.x, gridDimensions.y, gridDimensions.z),
                    VolumeOrigin = new float3(volumeOrigin.x, volumeOrigin.y, volumeOrigin.z),
                    CellSize = new float3(voxelCellSize.x, voxelCellSize.y, voxelCellSize.z),
                    SdfRange = sdfRange,
                    PingOrigin = new float3(origin.x, origin.y, origin.z),
                    ListenerPosition = new float3(origin.x, origin.y, origin.z),
                    Forward = new float3(forward.x, forward.y, forward.z),
                    Right = new float3(right.x, right.y, right.z),
                    Up = new float3(up.x, up.y, up.z),
                    MaxDistanceMeters = maxDistance,
                    StepMeters = stepMeters,
                    Intensity01 = math.saturate(intensity),
                    ReflectivityConstant = EcholocationReflectivityConstant,
                    SoundSpeedInv = SoundSpeedWaterMetersPerSecondInv,
                    DensityThreshold01 = EcholocationDensityThreshold01,
                    MinimumLowPassHertz = AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                    OpenLowPassHertz = AcousticOcclusionUtility.OpenLowPassCutoffHertz,
                    AbsorptionCoefficient = SonarEchoAbsorptionCoefficient,
                    ReferenceDistanceMeters = SonarEchoReferenceDistanceMeters,
                    RayCount = rayCount,
                    Hits = spatialViews.Hits
                };

                for (int rayIndex = 0; rayIndex < rayCount; rayIndex++)
                    job.Execute(rayIndex);

                _sonarEcholocationScheduledSequence = sequence;
                _sonarEcholocationScheduledRayCount = rayCount;
                _sonarEcholocationScheduledSdfVersion = version;
                _sonarEcholocationScheduledShiftSequence = HectonFloatingOrigin.CurrentShiftSequence;
                _sonarEcholocationScheduledStartFrame = scheduledStartFrame;
                _sonarEcholocationScheduledIntensity = math.saturate(intensity);
                _sonarEcholocationScheduledOrigin = origin;
                _sonarEcholocationScheduledTransform = originTransform;
            }
            finally
            {
                ReleaseSonarSpatialMutationGuard(ref spatialViews);
                publishedSdfVolume.ReleasePublishedSonarSdfPayloadReadLease(in publishedSdfLease);
            }

            PublishSdfSonarEchoPass();
            return true;
        }

        private void PublishSdfSonarEchoPass()
        {
            if (_sonarEcholocationScheduledSequence != Volatile.Read(ref _pendingSonarSequence))
                return;

            int inactiveIndex = 1 - Volatile.Read(ref _pendingSonarStateReadIndex);
            if (!TryAcquireSonarTapViews(out SonarTapVaultViews tapViews) ||
                !TryReadSonarHitView(out NativeArray<AcousticEcholocationRayHit>.ReadOnly sonarHits))
            {
                if (tapViews.PendingA.IsCreated ||
                    tapViews.PendingB.IsCreated ||
                    tapViews.Worker.IsCreated ||
                    tapViews.UploadRing.IsCreated)
                {
                    ReleaseSonarTapMutationGuard(ref tapViews);
                }
                return;
            }

            try
            {
                NativeArray<SonarEchoTap> inactiveTapBuffer = inactiveIndex == 0 ? tapViews.PendingA : tapViews.PendingB;
                if (!inactiveTapBuffer.IsCreated)
                    return;

                ClearSonarEchoTapUploadQueue(ref tapViews);
                int queuedTapCount = 0;
                int rayCount = math.clamp(_sonarEcholocationScheduledRayCount, 0, math.min(SonarEchoTapCapacity, sonarHits.Length));
                Transform originTransform = _sonarEcholocationScheduledTransform;
                Vector3 right = originTransform != null ? originTransform.right : Vector3.right;
                for (int rayIndex = 0; rayIndex < rayCount; rayIndex++)
                {
                    AcousticEcholocationRayHit hit = sonarHits[rayIndex];
                    if (hit.Hit == 0 || hit.Gain <= 0.000001f)
                        continue;

                    Vector3 direction = (Vector3)hit.Direction;
                    byte audioMaterialId = NormalizeSonarAudioMaterialId(hit.AudioMaterialId);
                    float dopplerRatio = ResolveSdfSonarEchoDopplerRatio(direction) *
                                         ResolveSonarMaterialPitchScale(audioMaterialId);
                    float panStereo = originTransform != null
                        ? math.clamp(Vector3.Dot(right, direction), -1f, 1f)
                        : 0f;
                    float lowPassCutoffHz = ResolveDepthMuffledSonarLowPass(
                        ResolveSonarMaterialLowPassCutoffHz(audioMaterialId, hit.LowPassCutoffHertz));
                    if (!TryEnqueueSonarEchoTap(
                            ref tapViews,
                            BuildSonarEchoTap(
                                hit.DelaySeconds,
                                dopplerRatio,
                                hit.Gain * ResolveSonarDepthMufflingGain(),
                                panStereo,
                                lowPassCutoffHz),
                            ref queuedTapCount))
                    {
                        break;
                    }

                    PublishPingReturnSignal((Vector3)hit.Point, hit.RayDistanceMeters, hit.Gain, hit.DelaySeconds, audioMaterialId);
                }

                TryAppendPredatorFleshEchoTapToQueue(
                    ref tapViews,
                    _sonarEcholocationScheduledOrigin,
                    originTransform,
                    _sonarEcholocationScheduledIntensity,
                    ref queuedTapCount);

                int tapCount = DrainSonarEchoTapUploadQueue(ref tapViews, inactiveTapBuffer);
                int echoRevision = 2;
                if (tapCount <= 0 && sonarSdfFallbackGhostEchoes)
                {
                    int tapLimit = math.min(SonarGhostEchoTapCount, inactiveTapBuffer.Length);
                    for (int tapIndex = 0; tapIndex < tapLimit; tapIndex++)
                        inactiveTapBuffer[tapIndex] = BuildGhostSonarEchoTap(_sonarEcholocationScheduledSequence, tapIndex, _sonarEcholocationScheduledIntensity);
                    tapCount = tapLimit;
                }

                PublishPendingSonarState(
                    inactiveIndex,
                    new SonarTriggerState
                    {
                        Sequence = _sonarEcholocationScheduledSequence,
                        EchoRevision = echoRevision,
                        StartFrame = _sonarEcholocationScheduledStartFrame,
                        Intensity = _sonarEcholocationScheduledIntensity,
                        EchoTapCount = tapCount
                    },
                    tapCount);
            }
            finally
            {
                ReleaseSonarTapMutationGuard(ref tapViews);
            }
        }

        private void ClearSonarEchoTapUploadQueue()
        {
            if (!TryAcquireSonarTapViews(out SonarTapVaultViews tapViews))
                return;

            try
            {
                ClearSonarEchoTapUploadQueue(ref tapViews);
            }
            finally
            {
                ReleaseSonarTapMutationGuard(ref tapViews);
            }
        }

        private void ClearSonarEchoTapUploadQueue(ref SonarTapVaultViews tapViews)
        {
            if (!tapViews.UploadRing.IsCreated)
                return;

            ClearRing(tapViews.UploadRing, SonarEchoTapCapacity);
            _sonarEchoTapUploadReadIndex = 0;
            _sonarEchoTapUploadWriteIndex = 0;
            _sonarEchoTapUploadCount = 0;
        }

        private void PrewarmSonarEchoTapUploadQueue()
        {
            ClearSonarEchoTapUploadQueue();
        }

        private bool TryEnqueueSonarEchoTap(SonarEchoTap tap, ref int queuedTapCount)
        {
            if (!TryAcquireSonarTapViews(out SonarTapVaultViews tapViews))
                return false;

            try
            {
                return TryEnqueueSonarEchoTap(ref tapViews, tap, ref queuedTapCount);
            }
            finally
            {
                ReleaseSonarTapMutationGuard(ref tapViews);
            }
        }

        private bool TryEnqueueSonarEchoTap(ref SonarTapVaultViews tapViews, SonarEchoTap tap, ref int queuedTapCount)
        {
            if (!tapViews.UploadRing.IsCreated ||
                queuedTapCount >= SonarEchoTapCapacity ||
                _sonarEchoTapUploadCount >= SonarEchoTapCapacity)
            {
                return false;
            }

            if (!TryWriteRing(tapViews.UploadRing, ref _sonarEchoTapUploadWriteIndex, _sonarEchoTapUploadCount, SonarEchoTapCapacity, in tap))
                return false;

            _sonarEchoTapUploadCount++;
            queuedTapCount++;
            return true;
        }

        private int DrainSonarEchoTapUploadQueue(NativeArray<SonarEchoTap> destination)
        {
            if (!TryAcquireSonarTapViews(out SonarTapVaultViews tapViews))
                return 0;

            try
            {
                return DrainSonarEchoTapUploadQueue(ref tapViews, destination);
            }
            finally
            {
                ReleaseSonarTapMutationGuard(ref tapViews);
            }
        }

        private int DrainSonarEchoTapUploadQueue(ref SonarTapVaultViews tapViews, NativeArray<SonarEchoTap> destination)
        {
            if (!destination.IsCreated ||
                !tapViews.UploadRing.IsCreated)
                return 0;

            int tapCount = 0;
            while (tapCount < destination.Length &&
                   tapCount < SonarEchoTapCapacity &&
                   _sonarEchoTapUploadCount > 0 &&
                   TryReadRing(tapViews.UploadRing, ref _sonarEchoTapUploadReadIndex, _sonarEchoTapUploadCount, SonarEchoTapCapacity, out SonarEchoTap tap))
            {
                _sonarEchoTapUploadCount = math.max(0, _sonarEchoTapUploadCount - 1);
                destination[tapCount++] = tap;
            }

            if (_sonarEchoTapUploadCount <= 0)
            {
                _sonarEchoTapUploadCount = 0;
                _sonarEchoTapUploadReadIndex = 0;
                _sonarEchoTapUploadWriteIndex = 0;
            }

            return tapCount;
        }

        private void HandleAcousticEchoReturned(AcousticEchoEvent echoEvent)
        {
            if (echoEvent.ReturnStrength <= 0.0001f)
                return;

            if (_boundPlayerTransform == null)
                TryBindFromCachedRuntimeContext();

            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_sonarEchoCompositeFrame != frame)
            {
                FlushSonarEchoCompositeGroups();
                _sonarEchoCompositeFrame = frame;
            }

            AbsoluteUniversePosition echoAup = echoEvent.ResolveWorldAup();
            if (!TryAcquireSonarSpatialViews(out SonarSpatialVaultViews spatialViews))
                return;

            try
            {
                NativeArray<SonarEchoCompositeGroup> writeCandidates = GetSonarEchoCompositeCandidateBuffer(ref spatialViews, _sonarEchoCompositeWriteBufferIndex);
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
            finally
            {
                ReleaseSonarSpatialMutationGuard(ref spatialViews);
            }
        }

        private void FlushSonarEchoCompositeGroups()
        {
            int writeBufferIndex = _sonarEchoCompositeWriteBufferIndex;
            int candidateCount = GetSonarEchoCompositeCandidateCount(writeBufferIndex);
            if (candidateCount <= 0)
                return;

            if (!TryAcquireSonarSpatialViews(out SonarSpatialVaultViews spatialViews))
                return;

            try
            {
                NativeArray<SonarEchoCompositeGroup> candidates = GetSonarEchoCompositeCandidateBuffer(ref spatialViews, writeBufferIndex);
                if (!RunSonarEchoCompositeHashPass(ref spatialViews, candidates, candidateCount))
                {
                    SetSonarEchoCompositeCandidateCount(writeBufferIndex, 0);
                    return;
                }

                _sonarEchoCompositeScheduledBufferIndex = writeBufferIndex;
                _sonarEchoCompositeScheduledCandidateCount = math.clamp(candidateCount, 0, SonarEchoCompositeCandidateCapacity);
                _sonarEchoCompositeWriteBufferIndex = writeBufferIndex ^ 1;
                SetSonarEchoCompositeCandidateCount(_sonarEchoCompositeWriteBufferIndex, 0);
                _sonarEchoCompositeFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
                PublishCompletedSonarEchoCompositeGroups(ref spatialViews);
            }
            finally
            {
                ReleaseSonarSpatialMutationGuard(ref spatialViews);
            }
        }

        private void PublishCompletedSonarEchoCompositeGroups(ref SonarSpatialVaultViews spatialViews)
        {
            int groupCount = spatialViews.GroupCount.IsCreated
                ? math.clamp(spatialViews.GroupCount[0], 0, SonarEchoCompositeGroupCapacity)
                : 0;
            for (int i = 0; i < groupCount; i++)
            {
                SonarEchoCompositeGroup group = spatialViews.Groups[i];
                int hitCount = math.max(1, group.HitCount);
                float invHitCount = math.rcp((float)hitCount);
                float hitScale = ResolveSonarCompositeHitScale(hitCount);
                EnqueueCompositeAcousticEcho(
                    group.DistanceMeters * invHitCount,
                    group.ReturnStrength * invHitCount * hitScale,
                    group.Resonance * invHitCount,
                    group.AudioMaterialId);
                spatialViews.Groups[i] = default;
            }

            int candidateCount = _sonarEchoCompositeScheduledCandidateCount;
            NativeArray<SonarEchoCompositeGroup> candidates = GetSonarEchoCompositeCandidateBuffer(ref spatialViews, _sonarEchoCompositeScheduledBufferIndex);
            for (int i = 0; i < candidateCount && i < SonarEchoCompositeCandidateCapacity; i++)
                candidates[i] = default;

            if (spatialViews.GroupCount.IsCreated)
                spatialViews.GroupCount[0] = 0;
            if (_sonarEchoCompositeScheduledBufferIndex >= 0)
                SetSonarEchoCompositeCandidateCount(_sonarEchoCompositeScheduledBufferIndex, 0);
            _sonarEchoCompositeScheduledBufferIndex = -1;
            _sonarEchoCompositeScheduledCandidateCount = 0;
            _sonarEchoCompositeFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
        }

        private bool RunSonarEchoCompositeHashPass(
            ref SonarSpatialVaultViews spatialViews,
            NativeArray<SonarEchoCompositeGroup> candidates,
            int candidateCount)
        {
            if (!candidates.IsCreated ||
                !spatialViews.Groups.IsCreated ||
                !spatialViews.GroupCount.IsCreated)
            {
                return false;
            }

            spatialViews.GroupCount[0] = 0;

            SonarEchoSpatialHashCoalesceJob coalesceJob = new SonarEchoSpatialHashCoalesceJob
            {
                Candidates = candidates,
                Groups = spatialViews.Groups,
                GroupCount = spatialViews.GroupCount,
                CandidateCount = math.clamp(candidateCount, 0, SonarEchoCompositeCandidateCapacity)
            };
            coalesceJob.Execute();
            return true;
        }

        private static NativeArray<SonarEchoCompositeGroup> GetSonarEchoCompositeCandidateBuffer(
            ref SonarSpatialVaultViews spatialViews,
            int bufferIndex)
        {
            return bufferIndex == 0
                ? spatialViews.CandidatesA
                : spatialViews.CandidatesB;
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

        private static int FastFloorToInt(double value)
        {
            int truncated = (int)value;
            return truncated > value ? truncated - 1 : truncated;
        }

        private static int ResolveSonarEchoCompositeHash(in AbsoluteUniversePosition position, byte audioMaterialId)
        {
            const uint primeX = 73856093u;
            const uint primeY = 19349663u;
            const uint primeZ = 83492791u;
            const uint primeMaterial = 2654435761u;
            double sectorSize = HectonPhysicsContract.AupSectorSizeMetersDouble;
            int cellX = FastFloorToInt(((position.GridX * sectorSize) + position.LocalX) * SonarEchoCompositeCellSizeMetersInv);
            int cellY = FastFloorToInt(((position.GridY * sectorSize) + position.LocalY) * SonarEchoCompositeCellSizeMetersInv);
            int cellZ = FastFloorToInt(((position.GridZ * sectorSize) + position.LocalZ) * SonarEchoCompositeCellSizeMetersInv);

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
            float resonance01 = math.saturate((resonanceScale - 0.65f) * 1.25f);
            float roundTripDistance = math.max(0f, distanceMeters) * 2f;
            float echoDelaySeconds = math.clamp(roundTripDistance * SoundSpeedWaterMetersPerSecondInv, 0f, SonarEchoMaximumDelaySeconds);
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

        private int BuildSdfSonarEchoTaps(
            Vector3 origin,
            Transform originTransform,
            float intensity,
            NativeArray<SonarEchoTap> tapBuffer)
        {
            if (!tapBuffer.IsCreated)
                return 0;

            int probeCount = math.min(ResolveSonarSdfProbeCount(), tapBuffer.Length);
            float maxDistance = math.clamp(
                sonarSdfMaximumProbeDistanceMeters,
                math.max(1f, sonarSdfProbeIntervalMeters),
                MaximumProbeDistanceMeters);
            float stepMeters = math.clamp(sonarSdfProbeIntervalMeters, 1f, maxDistance);
            int tapCount = 0;
            for (int probeIndex = 0; probeIndex < probeCount; probeIndex++)
            {
                Vector3 direction = ResolveSdfSonarProbeDirection(probeIndex, probeCount, originTransform);
                if (!HectonVoxelVolume.TryRaymarchAnyPublishedSdf(
                        origin,
                        direction,
                        maxDistance,
                        stepMeters,
                        out HectonVoxelVolume hitVolume,
                        out VoxelSdfRaycastHit hit) ||
                    hit.Hit == 0)
                {
                    continue;
                }

                float distanceMeters = math.clamp(hit.Distance, MinimumProbeDistanceMeters, maxDistance);
                float returnDistanceMeters = math.max(
                    MinimumProbeDistanceMeters,
                    ApproximateDistanceMetersFromSq((hit.Point - origin).sqrMagnitude));
                byte audioMaterialId = ResolveSdfSonarAudioMaterialId(hitVolume, hit.Point);
                float attenuation = ResolveSdfSonarEchoAttenuation(distanceMeters, intensity, audioMaterialId);
                if (attenuation <= 0.0001f)
                    continue;

                float delaySeconds = math.clamp(
                    (distanceMeters + returnDistanceMeters) * SoundSpeedWaterMetersPerSecondInv,
                    0f,
                    SonarEchoMaximumDelaySeconds);
                float dopplerRatio = ResolveSdfSonarEchoDopplerRatio(direction) *
                                     ResolveSonarMaterialPitchScale(audioMaterialId);
                float panStereo = originTransform != null
                    ? math.clamp(Vector3.Dot(originTransform.right, direction), -1f, 1f)
                    : 0f;
                float baseCutoffHz = math.lerp(1250f, AcousticOcclusionUtility.OpenLowPassCutoffHertz, attenuation);
                float lowPassCutoffHz = ResolveDepthMuffledSonarLowPass(
                    ResolveSonarMaterialLowPassCutoffHz(audioMaterialId, baseCutoffHz));

                tapBuffer[tapCount] = BuildSonarEchoTap(
                    delaySeconds,
                    dopplerRatio,
                    attenuation,
                    panStereo,
                    lowPassCutoffHz);

                PublishPingReturnSignal(hit.Point, distanceMeters, attenuation, delaySeconds, audioMaterialId);
                tapCount++;
                if (tapCount >= tapBuffer.Length)
                    break;
            }

            return tapCount;
        }

        private int ResolveSonarSdfProbeCount()
        {
            EnsureAudioQualityPolicyCached();
            float qualityCurve = ResolveCachedAudioQualityCurve01();
            return math.clamp(
                (int)math.round(math.lerp(SonarSdfLowProbeCount, SonarSdfHighProbeCount, qualityCurve)),
                SonarSdfLowProbeCount,
                SonarSdfHighProbeCount);
        }

        private static Vector3 ResolveSdfSonarProbeDirection(int probeIndex, int probeCount, Transform originTransform)
        {
            Vector3 forward = NormalizeVector(originTransform != null ? originTransform.forward : Vector3.forward, Vector3.forward);
            Vector3 right = NormalizeVector(originTransform != null ? originTransform.right : Vector3.right, Vector3.right);
            Vector3 up = NormalizeVector(originTransform != null ? originTransform.up : Vector3.up, Vector3.up);

            if (probeCount <= SonarSdfLowProbeCount)
            {
                switch (probeIndex & 7)
                {
                    case 0: return forward;
                    case 1: return NormalizeVector(forward + right, forward);
                    case 2: return right;
                    case 3: return -forward;
                    case 4: return -right;
                    case 5: return up;
                    case 6: return -up;
                    default: return NormalizeVector(forward - right, forward);
                }
            }

            if (probeCount <= 16)
            {
                switch (probeIndex & 7)
                {
                    case 0: return forward;
                    case 1: return NormalizeVector(forward + right, forward);
                    case 2: return right;
                    case 3: return NormalizeVector(-forward + right, right);
                    case 4: return -forward;
                    case 5: return NormalizeVector(-forward - right, -forward);
                    case 6: return -right;
                    default: return NormalizeVector(forward - right, forward);
                }
            }

            int lane = probeIndex & 31;
            float sx = (lane & 1) == 0 ? 1f : -1f;
            float sy = (lane & 2) == 0 ? 1f : -1f;
            float sz = (lane & 4) == 0 ? 1f : -1f;
            int weightSet = (lane >> 3) & 3;
            float forwardWeight = weightSet == 0 ? 1f : weightSet == 1 ? 0.55f : weightSet == 2 ? 0.25f : 0.75f;
            float rightWeight = weightSet == 1 ? 1f : weightSet == 2 ? 0.55f : weightSet == 3 ? 0.25f : 0.75f;
            float upWeight = weightSet == 2 ? 1f : weightSet == 3 ? 0.55f : weightSet == 0 ? 0.25f : 0.75f;
            return NormalizeVector(
                right * (sx * rightWeight) +
                up * (sy * upWeight) +
                forward * (sz * forwardWeight),
                forward);
        }

        private static Vector3 NormalizeVector(Vector3 value, Vector3 fallback)
        {
            float3 v = (float3)value;
            float lengthSq = math.lengthsq(v);
            if (!math.all(math.isfinite(v)) || lengthSq <= 0.000001f)
                return fallback;

            return (Vector3)(v * math.rsqrt(lengthSq));
        }

        private static byte ResolveSdfSonarAudioMaterialId(HectonVoxelVolume hitVolume, Vector3 hitPoint)
        {
            if (hitVolume != null &&
                hitVolume.TrySamplePublishedSonarAudioMaterialId(hitPoint, out byte audioMaterialId))
            {
                switch (audioMaterialId)
                {
                    case SonarAudioMaterialIdMetal:
                    case SonarAudioMaterialIdRock:
                    case SonarAudioMaterialIdGlass:
                    case SonarAudioMaterialIdBiological:
                        return audioMaterialId;
                }
            }

            return SonarAudioMaterialIdRock;
        }

        private static byte NormalizeSonarAudioMaterialId(byte audioMaterialId)
        {
            switch (audioMaterialId)
            {
                case SonarAudioMaterialIdMetal:
                case SonarAudioMaterialIdRock:
                case SonarAudioMaterialIdGlass:
                case SonarAudioMaterialIdBiological:
                    return audioMaterialId;
                default:
                    return SonarAudioMaterialIdRock;
            }
        }

        private float ResolveSdfSonarEchoAttenuation(float distanceMeters, float intensity, byte audioMaterialId)
        {
            float distanceFalloff = SonarEchoReferenceDistanceMeters * math.rcp(math.max(SonarEchoReferenceDistanceMeters, distanceMeters));
            float absorption = ApproximateExpNegPositive(distanceMeters * SonarEchoAbsorptionCoefficient);
            float materialDecay = ResolveSonarMaterialDecayMultiplier(audioMaterialId);
            return math.saturate(
                math.saturate(intensity) *
                distanceFalloff *
                absorption *
                materialDecay *
                ResolveSonarDepthMufflingGain());
        }

        private float ResolveSdfSonarEchoDopplerRatio(Vector3 echoDirection)
        {
            Vector3 velocity = CoreDeterminismSignals.TryGetLatestKccVelocityVector(KccVelocityAudioMaxAgeFrames, out Vector3 kccVelocity)
                ? kccVelocity
                : Vector3.zero;

            float radialVelocity = Vector3.Dot(velocity, echoDirection);
            float clampedRadialVelocity = math.clamp(
                radialVelocity,
                -SoundSpeedWaterMetersPerSecond * 0.45f,
                SoundSpeedWaterMetersPerSecond * 0.45f);
            return math.clamp(
                1f + (clampedRadialVelocity * SoundSpeedWaterMetersPerSecondInv),
                SonarEchoMinimumDopplerRatio,
                SonarEchoMaximumDopplerRatio);
        }

        private float ResolveSonarDepthMufflingGain()
        {
            float pressureScalar = ResolveSonarAmbientPressureScalar();
            return math.saturate(math.rcp(1f + pressureScalar * math.max(0f, sonarDepthMufflingScalar)));
        }

        private float ResolveDepthMuffledSonarLowPass(float openCutoff)
        {
            float pressureScalar = ResolveSonarAmbientPressureScalar();
            float pressureCutoff = openCutoff * math.rcp(math.max(pressureScalar * (1f + sonarDepthMufflingScalar), 1f));
            return math.clamp(
                pressureCutoff,
                AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                AcousticOcclusionUtility.OpenLowPassCutoffHertz);
        }

        private float ResolveSonarAmbientPressureScalar()
        {
            float depthMeters = math.max(0f, _absoluteDepthTickValue);
            if (depthMeters <= 0f)
                depthMeters = ResolvePlayerDepthMeters();

            return 1f + depthMeters * math.rcp(math.max(1f, PsychoacousticPressureReferenceDepthMeters));
        }

        private static void PublishPingReturnSignal(
            Vector3 position,
            float distanceMeters,
            float returnStrength,
            float echoDelaySeconds,
            byte audioMaterialId)
        {
            if (!TryResolveRuntimeAup(position, out AbsoluteUniversePosition hitAup))
                return;

            PingReturnSignal signal = new PingReturnSignal(
                position,
                in hitAup,
                distanceMeters,
                returnStrength,
                echoDelaySeconds,
                audioMaterialId);
            SpectrumEvents.TryRaisePingReturnSignal(in signal);
        }

        private void TryPublishPredatorPingBack(Vector3 origin, float intensity)
        {
            float radius = math.max(1f, sonarPredatorPingBackRadiusMeters);
            if (!WorldSpatialHashGrid.TryGetNearestAggressiveBioform(
                    origin,
                    radius,
                    ~0,
                    _boundPlayerTransform,
                    out SpatialQueryHit hit) ||
                !(hit.Owner is IFaunaSpatialContact predatorContact) ||
                !predatorContact.IsLeviathanContact)
            {
                return;
            }

            float distanceMeters = ApproximateDistanceMetersFromSq(hit.DistanceSqr);
            float range01 = 1f - math.saturate(distanceMeters * math.rcp(radius));
            if (range01 <= 0f)
                return;

            Vector3 direction = NormalizeVector(origin - hit.Position, Vector3.forward);
            int sourceBodyInstanceId = hit.Rigidbody != null
                ? unchecked((int)EntityId.ToULong(hit.Rigidbody.GetEntityId()))
                : hit.Transform != null ? unchecked((int)EntityId.ToULong(hit.Transform.GetEntityId())) : 0;
            PublishAcousticImpulseSignal(
                hit.Position,
                direction,
                math.max(1f, radius * range01 * math.saturate(intensity) * 120f),
                math.saturate(range01 * math.max(0.2f, intensity)),
                0.62f,
                math.max(12f, radius * 0.45f),
                sourceBodyInstanceId,
                SonarAudioMaterialIdBiological,
                AcousticImpulseFlagLeviathan | AcousticImpulseFlagLarge);
        }

        private void TryAppendPredatorFleshEchoTapToBuffer(
            Vector3 origin,
            Transform originTransform,
            float intensity,
            NativeArray<SonarEchoTap> tapBuffer,
            ref int tapCount)
        {
            if (!tapBuffer.IsCreated || tapCount >= tapBuffer.Length)
                return;

            if (!TryBuildPredatorFleshEchoTap(
                    origin,
                    originTransform,
                    intensity,
                    out SonarEchoTap tap,
                    out Vector3 hitPosition,
                    out float distanceMeters,
                    out float strength))
            {
                return;
            }

            tapBuffer[tapCount++] = tap;
            PublishPingReturnSignal(
                hitPosition,
                distanceMeters,
                strength,
                tap.DelaySeconds,
                SonarAudioMaterialIdBiological);
        }

        private void TryAppendPredatorFleshEchoTapToQueue(
            Vector3 origin,
            Transform originTransform,
            float intensity,
            ref int queuedTapCount)
        {
            if (!TryAcquireSonarTapViews(out SonarTapVaultViews tapViews))
                return;

            try
            {
                TryAppendPredatorFleshEchoTapToQueue(
                    ref tapViews,
                    origin,
                    originTransform,
                    intensity,
                    ref queuedTapCount);
            }
            finally
            {
                ReleaseSonarTapMutationGuard(ref tapViews);
            }
        }

        private void TryAppendPredatorFleshEchoTapToQueue(
            ref SonarTapVaultViews tapViews,
            Vector3 origin,
            Transform originTransform,
            float intensity,
            ref int queuedTapCount)
        {
            if (!TryBuildPredatorFleshEchoTap(
                    origin,
                    originTransform,
                    intensity,
                    out SonarEchoTap tap,
                    out Vector3 hitPosition,
                    out float distanceMeters,
                    out float strength))
            {
                return;
            }

            if (!TryEnqueueSonarEchoTap(ref tapViews, tap, ref queuedTapCount))
                return;

            PublishPingReturnSignal(
                hitPosition,
                distanceMeters,
                strength,
                tap.DelaySeconds,
                SonarAudioMaterialIdBiological);
        }

        private bool TryBuildPredatorFleshEchoTap(
            Vector3 origin,
            Transform originTransform,
            float intensity,
            out SonarEchoTap tap,
            out Vector3 hitPosition,
            out float distanceMeters,
            out float strength)
        {
            tap = default;
            hitPosition = default;
            distanceMeters = 0f;
            strength = 0f;

            float radius = math.max(1f, sonarPredatorPingBackRadiusMeters);
            if (!WorldSpatialHashGrid.TryGetNearestAggressiveBioform(
                    origin,
                    radius,
                    ~0,
                    _boundPlayerTransform,
                    out SpatialQueryHit hit) ||
                !(hit.Owner is IFaunaSpatialContact predatorContact) ||
                !predatorContact.IsLeviathanContact)
            {
                return false;
            }

            distanceMeters = ApproximateDistanceMetersFromSq(hit.DistanceSqr);
            float range01 = 1f - math.saturate(distanceMeters * math.rcp(radius));
            strength = math.saturate(math.saturate(intensity) * range01 * 0.74f);
            if (strength <= 0.0001f)
                return false;

            hitPosition = hit.Position;
            Vector3 direction = NormalizeVector(hitPosition - origin, Vector3.forward);
            float returnDistance = math.max(MinimumProbeDistanceMeters, distanceMeters);
            float delaySeconds = math.clamp(
                (distanceMeters + returnDistance) * SoundSpeedWaterMetersPerSecondInv,
                0f,
                SonarEchoMaximumDelaySeconds);
            float panStereo = originTransform != null
                ? math.clamp(Vector3.Dot(originTransform.right, direction), -1f, 1f)
                : 0f;
            tap = BuildSonarEchoTap(
                delaySeconds,
                ResolveSonarMaterialPitchScale(SonarAudioMaterialIdBiological),
                strength,
                panStereo,
                ResolveDepthMuffledSonarLowPass(ResolveSonarMaterialLowPassCutoffHz(SonarAudioMaterialIdBiological, 900f)));
            return true;
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

        private void ConsumeLaserCutterEventSignals()
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastLaserCutterSignalFrame == frame)
                return;

            _lastLaserCutterSignalFrame = frame;
            ReadOnlySpan<global::Hecton8.Core.Contracts.Signals.LaserCutterEventPayload> payloads =
                SignalBus<global::Hecton8.Core.Contracts.Signals.LaserCutterEventPayload>.GetFrameSnapshot();
            for (int i = 0; i < payloads.Length; i++)
                HandleLaserCutterEvent(in payloads[i]);
        }

        private void HandleLaserCutterEvent(in global::Hecton8.Core.Contracts.Signals.LaserCutterEventPayload payload)
        {
            if (!IsBoundPlayerCutterEvent(in payload))
                return;

            global::Hecton8.Core.Contracts.Signals.LaserCutterEventType eventType = (global::Hecton8.Core.Contracts.Signals.LaserCutterEventType)payload.EventType;
            if (eventType == global::Hecton8.Core.Contracts.Signals.LaserCutterEventType.HeatChanged)
            {
                HandleCutterHeatChanged(payload.Heat01);
                return;
            }

            if (eventType == global::Hecton8.Core.Contracts.Signals.LaserCutterEventType.BeamStateChanged)
                HandleCutterBeamStateChanged(in payload);
        }

        private void HandleCutterHeatChanged(float heat01)
        {
            _laserCutterHeat01 = math.saturate(heat01);
        }

        private void HandleCutterBeamStateChanged(in global::Hecton8.Core.Contracts.Signals.LaserCutterEventPayload payload)
        {
            bool isActive = LaserCutterEvents.IsBeamActive(in payload);
            _laserCutterBeamActive = isActive;
            if (!isActive)
                _laserCutterHeat01 = 0f;
        }

        private bool IsBoundPlayerCutterEvent(in global::Hecton8.Core.Contracts.Signals.LaserCutterEventPayload payload)
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

            bool isPlayerOwnedImpact = IsBoundPlayerImpact(in impactSignal);
            float maxDistance = PhysicsImpactStressRadiusMeters;
            float distance = 0f;
            if (!isPlayerOwnedImpact)
            {
                AbsoluteUniversePosition impactAup = AbsoluteUniversePosition.FromAbsolutePosition(impactSignal.ResolvePointAupMeters());
                if (!TryResolveBoundPlayerDistanceWithin(in impactAup, maxDistance, out distance))
                    return;
            }

            float proximity = isPlayerOwnedImpact
                ? 1f
                : 1f - math.saturate(distance * math.rcp(math.max(maxDistance, 0.001f)));
            bool isHeavyImpact = PhysicsImpactSignal.IsHeavy(in impactSignal);
            if (!isHeavyImpact && impactSignal.MassVelocity < PhysicsImpactMinimumAudibleMassVelocity)
                return;

            ResolveImpactMaterialBlend(
                impactSignal.PrimaryAudioMaterialId,
                impactSignal.SecondaryAudioMaterialId,
                out float clangMaterialMultiplier,
                out float echoMaterialMultiplier,
                out float hollowMaterialMultiplier);
            float impactVolume01 = ResolveImpactVolume01FromMassVelocity(impactSignal.MassVelocity);
            float impactStress = math.saturate(impactVolume01 * PhysicsImpactStressBoost * math.max(0.2f, proximity));
            if (isHeavyImpact)
                impactStress = math.max(impactStress, 0.45f * math.max(0.35f, proximity));

            float metallicImpulse = isHeavyImpact
                ? math.max(impactStress, 0.55f * math.max(0.35f, proximity))
                : impactStress * math.max(0.35f, proximity);
            metallicImpulse = math.saturate(metallicImpulse * clangMaterialMultiplier);
            float clangExcitation = math.saturate(
                metallicImpulse *
                math.lerp(0.55f, 1.15f, impactVolume01) *
                math.max(0.4f, proximity) *
                clangMaterialMultiplier);
            if (isPlayerOwnedImpact && isHeavyImpact)
                clangExcitation = math.max(clangExcitation, 0.48f);
            float ruptureSignal01 = math.max(impactStress, metallicImpulse);
            if (ruptureSignal01 > EardrumRuptureImpactThreshold01)
            {
                float rupture01 = math.saturate(
                    (ruptureSignal01 - EardrumRuptureImpactThreshold01) *
                    math.rcp(math.max(1f - EardrumRuptureImpactThreshold01, 0.001f)));
                _targetEardrumRuptureTinnitusValue = math.max(
                    _targetEardrumRuptureTinnitusValue,
                    rupture01);
            }

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

        private static bool IsBoundPlayerImpact(in PhysicsImpactSignal impactSignal)
        {
            _ = impactSignal;
            return false;
        }

        private void ConsumeAcousticImpulseSignals()
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastAcousticImpulseSignalFrame == frame)
                return;

            _lastAcousticImpulseSignalFrame = frame;
            ReadOnlySpan<PhysicsEventPayload> signals = SignalBus<PhysicsEventPayload>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PhysicsEventPayload payload = signals[i];
                if (payload.EventType != (ushort)PhysicsEventType.AcousticImpulse)
                    continue;

                HandleAcousticImpulse(in payload);
            }
        }

        private static void PublishAcousticImpulseSignal(
            Vector3 runtimePosition,
            Vector3 direction,
            float kineticEnergyJoules,
            float volume01,
            float pitchScale,
            float radiusMeters,
            int sourceBodyInstanceId,
            byte audioMaterialId,
            uint statusBits)
        {
            PhysicsEventPayload payload = new PhysicsEventPayload
            {
                RuntimePosition = runtimePosition,
                Direction = ResolveAcousticImpulseDirection(direction),
                ForceVector = default,
                ImpulseVector = default,
                RadiusMeters = math.max(0f, radiusMeters),
                Scalar0 = math.max(0f, kineticEnergyJoules),
                Scalar1 = math.saturate(volume01),
                Scalar2 = math.clamp(pitchScale, 0.05f, 4f),
                PrimaryId = sourceBodyInstanceId,
                DataHash = audioMaterialId,
                StatusBits = statusBits,
                EventType = (ushort)PhysicsEventType.AcousticImpulse,
                Reserved = 0
            };
            SignalBus<PhysicsEventPayload>.TryPushTracked(in payload, ref s_x001PlayerCriticalProceduralAudioRendererSignalPushDropCount);
        }

        private static Vector3 ResolveAcousticImpulseDirection(Vector3 value)
        {
            float3 vector = math.float3(value.x, value.y, value.z);
            if (!math.all(math.isfinite(vector)))
                return Vector3.forward;

            float ax = math.abs(vector.x);
            float ay = math.abs(vector.y);
            float az = math.abs(vector.z);
            if ((ax + ay + az) <= 0.000001f)
                return Vector3.forward;

            if (ax >= ay && ax >= az)
                return vector.x < 0f ? Vector3.left : Vector3.right;

            if (ay >= az)
                return vector.y < 0f ? Vector3.down : Vector3.up;

            return vector.z < 0f ? Vector3.back : Vector3.forward;
        }

        private void HandleAcousticImpulse(in PhysicsEventPayload impulseEvent)
        {
            if (_boundPlayerTransform == null)
                return;

            float maxDistance = math.max(PhysicsImpactStressRadiusMeters, impulseEvent.RadiusMeters);
            if (!TryResolveBoundPlayerDistanceWithin(impulseEvent.RuntimePosition, maxDistance, out float distance))
                return;

            float proximity = 1f - math.saturate(distance * math.rcp(math.max(maxDistance, 0.001f)));
            float audible01 = math.saturate(impulseEvent.Scalar1 * math.max(0.12f, proximity));
            if (audible01 <= 0.001f)
                return;

            byte audioMaterialId = unchecked((byte)impulseEvent.DataHash);
            bool isCritical = (impulseEvent.StatusBits & AcousticImpulseFlagCritical) != 0u;
            bool isLeviathan = (impulseEvent.StatusBits & AcousticImpulseFlagLeviathan) != 0u;
            float threatScale = isCritical ? 1.25f : 1f;
            if (isLeviathan)
                threatScale = math.max(threatScale, 1.45f);

            float materialDecayMultiplier = ResolveSonarMaterialDecayMultiplier(audioMaterialId);
            float materialPitchScale = ResolveSonarMaterialPitchScale(audioMaterialId);
            float stress = math.saturate(audible01 * 0.45f * threatScale);
            float metallic = audioMaterialId == SonarAudioMaterialIdMetal
                ? math.saturate(audible01 * math.lerp(0.45f, 0.9f, proximity))
                : 0f;
            float clangExcitation = math.saturate(audible01 * materialPitchScale * threatScale);
            float echoExcitation = math.saturate(audible01 * materialDecayMultiplier * 0.72f);
            float echoDelaySeconds = math.clamp(distance * SoundSpeedWaterMetersPerSecondInv, 0f, SonarEchoMaximumDelaySeconds);
            float echoLowPassCutoffHz = ResolveSonarMaterialLowPassCutoffHz(
                audioMaterialId,
                math.lerp(720f, AcousticOcclusionUtility.OpenLowPassCutoffHertz, proximity));

            TryEnqueueImpactAudioEvent(
                stress,
                metallic,
                clangExcitation,
                echoExcitation,
                echoDelaySeconds,
                proximity,
                echoLowPassCutoffHz,
                math.clamp(impulseEvent.Scalar2 * materialPitchScale, 0.05f, 4f));
            _impactStressImpulseTickValue = math.max(_impactStressImpulseTickValue, stress);
        }

        private void ConsumeProceduralAudioSignals()
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastProceduralAudioSignalFrame == frame)
                return;

            _lastProceduralAudioSignalFrame = frame;
            ReadOnlySpan<AudioEvent> signals = SignalBus<AudioEvent>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                AudioEvent audioEvent = signals[i];
                HandleProceduralAudioEvent(in audioEvent);
            }

            ReadOnlySpan<PlayerWaterSplashSignal> waterSplashes = SignalBus<PlayerWaterSplashSignal>.GetFrameSnapshot();
            for (int i = 0; i < waterSplashes.Length; i++)
            {
                PlayerWaterSplashSignal splash = waterSplashes[i];
                HandlePlayerWaterSplashSignal(in splash);
            }

            ReadOnlySpan<BaseStructuralWarningSignal> baseWarnings = SignalBus<BaseStructuralWarningSignal>.GetFrameSnapshot();
            for (int i = 0; i < baseWarnings.Length; i++)
            {
                BaseStructuralWarningSignal warning = baseWarnings[i];
                HandleBaseStructuralWarningSignal(in warning);
            }
        }

        private void HandlePlayerWaterSplashSignal(in PlayerWaterSplashSignal signal)
        {
            float intensity = math.saturate(math.isfinite(signal.Intensity01) ? signal.Intensity01 : 0f);
            float verticalSpeed = math.max(0f, math.isfinite(signal.VerticalSpeed) ? signal.VerticalSpeed : 0f);
            float speed01 = math.saturate(
                (verticalSpeed - PlayerWaterSplashMinimumVerticalSpeed) *
                math.rcp(math.max(0.001f, PlayerWaterSplashReferenceVerticalSpeed - PlayerWaterSplashMinimumVerticalSpeed)));
            float audible01 = math.saturate(intensity * math.lerp(0.35f, 1f, speed01));
            if (audible01 <= KineticImpactThudMinimumExcitation)
                return;

            bool submerged = signal.IsSubmerged != 0;
            float thudExcitation = math.saturate(audible01 * PlayerWaterSplashMaximumThudExcitation);
            float startHertz = math.lerp(
                submerged ? PlayerWaterSplashEntryStartHertz : PlayerWaterSplashExitStartHertz,
                submerged ? PlayerWaterSplashEntryStartHertz * 1.18f : PlayerWaterSplashExitStartHertz * 1.12f,
                speed01);
            float endHertz = submerged ? PlayerWaterSplashEntryEndHertz : PlayerWaterSplashExitEndHertz;
            float lowPassHertz = math.lerp(
                PlayerWaterSplashLowPassHertz,
                submerged ? PlayerWaterSplashLowPassHertz : PlayerWaterSplashExitLowPassHertz,
                speed01);
            float impactStress = math.saturate(audible01 * PlayerWaterSplashImpactStressScale);

            TryEnqueueImpactAudioEvent(
                impactStress,
                0f,
                0f,
                0f,
                0f,
                0f,
                lowPassHertz,
                1f,
                thudExcitation,
                PlayerWaterSplashThudDurationSeconds,
                startHertz,
                endHertz,
                math.lerp(0.08f, 0.2f, speed01),
                lowPassHertz,
                0f);
            _impactStressImpulseTickValue = math.max(_impactStressImpulseTickValue, impactStress);
        }

        private void HandleProceduralAudioEvent(in AudioEvent audioEvent)
        {
            switch (audioEvent.Kind)
            {
                case AudioEventKind.AudioPing:
                    HandleAudioPingTriggered(in audioEvent.AudioPing);
                    break;
                case AudioEventKind.StructuralStress:
                    HandleStructuralStressTriggered(in audioEvent.StructuralStress);
                    break;
            }
        }

        private void HandleAudioPingTriggered(in AudioPingTriggerPayload info)
        {
            ProceduralAudioPingKind kind = (ProceduralAudioPingKind)info.Kind;
            if (kind == ProceduralAudioPingKind.PredatorKill)
                HandlePredatorKillAudioPing(in info);
            else if (kind == ProceduralAudioPingKind.MeteorBoom)
                HandleMeteorBoomAudioPing(in info);
            else if (kind == ProceduralAudioPingKind.MechanicalWhirr ||
                     kind == ProceduralAudioPingKind.AirRelease)
                HandleMechanicalWhirrAudioPing(in info);
            else if (kind == ProceduralAudioPingKind.LeviathanRoar)
                HandleLeviathanRoarAudioPing(in info);
        }

        private void HandlePredatorKillAudioPing(in AudioPingTriggerPayload info)
        {
            if (_boundPlayerTransform == null)
                return;

            if (!TryResolveBoundPlayerDistanceWithin(info.WorldPosition, PredatorKillAudioRadiusMeters, out float distance))
                return;

            float proximity = 1f - math.saturate(distance * PredatorKillAudioRadiusMetersInv);
            float transmission01 = math.saturate(info.AcousticTransmission01);
            float audible01 = math.saturate(info.Intensity * proximity * math.max(0.08f, transmission01));
            if (audible01 <= 0.001f)
                return;

            float echoDelaySeconds = math.clamp(distance * SoundSpeedWaterMetersPerSecondInv, 0f, SonarEchoMaximumDelaySeconds);
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

        private void HandleMeteorBoomAudioPing(in AudioPingTriggerPayload info)
        {
            if (_boundPlayerTransform == null)
                return;

            if (!TryResolveBoundPlayerDistanceWithin(info.WorldPosition, MeteorBoomAudioRadiusMeters, out float distance))
                return;

            float proximity = 1f - math.saturate(distance * MeteorBoomAudioRadiusMetersInv);
            float audible01 = math.saturate(info.Intensity * proximity * math.max(0.2f, info.AcousticTransmission01));
            if (audible01 <= 0.001f)
                return;

            float echoDelaySeconds = math.clamp(distance * SoundSpeedWaterMetersPerSecondInv, 0f, SonarEchoMaximumDelaySeconds);
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

        private void HandleMechanicalWhirrAudioPing(in AudioPingTriggerPayload info)
        {
            if (_boundPlayerTransform == null)
                return;

            if (!TryResolveBoundPlayerDistanceWithin(info.WorldPosition, MechanicalWhirrAudioRadiusMeters, out float distance))
                return;

            float proximity = 1f - math.saturate(distance * MechanicalWhirrAudioRadiusMetersInv);
            float audible01 = math.saturate(info.Intensity * proximity * math.max(0.18f, info.AcousticTransmission01));
            if (audible01 <= 0.001f)
                return;

            float pitchScale = math.clamp(info.LowPassCutoffHz * MechanicalWhirrPitchCutoffInv, 0.75f, 1.45f);
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

        private void HandleLeviathanRoarAudioPing(in AudioPingTriggerPayload info)
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

            float proximity = 1f - math.saturate(distance * math.rcp(math.max(maxDistance, 0.001f)));
            float transmission01 = math.saturate(info.AcousticTransmission01);
            float aggroLevel = math.saturate(math.max(info.Intensity, info.ChirpDurationSeconds) * proximity * math.max(0.1f, transmission01));
            if (aggroLevel <= 0.001f)
                return;

            float3 predatorRelativeMeters = AbsoluteUniversePosition.ToCameraRelativeFloat3(predatorAup, playerAup);
            _pendingLeviathanRoarDistanceMeters = distance;
            _hasPendingLeviathanRoarDistance = true;
            float dopplerPitchScale = math.clamp(
                _targetLeviathanRoarPitchScale,
                LeviathanDopplerMinimumPitchScale,
                LeviathanDopplerMaximumPitchScale);
            _targetLeviathanRoarAggroValue = math.max(_targetLeviathanRoarAggroValue, aggroLevel);
            _impactStressImpulseTickValue = math.max(_impactStressImpulseTickValue, aggroLevel * 0.22f);
            Vector3 directionToPredator = new Vector3(predatorRelativeMeters.x, predatorRelativeMeters.y, predatorRelativeMeters.z);
            PublishAcousticImpulseSignal(
                info.WorldPosition,
                directionToPredator,
                0f,
                aggroLevel,
                dopplerPitchScale,
                PredatorKillAudioRadiusMeters * 2.5f,
                0,
                SonarAudioMaterialIdDefault,
                AcousticImpulseFlagLeviathan);
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

            float now = ResolvePresentationClockSeconds();
            if (!_hasLeviathanRoarDopplerSample)
            {
                _hasLeviathanRoarDopplerSample = true;
                _lastLeviathanRoarDistanceMeters = currentDistance;
                _lastLeviathanRoarSampleTime = now;
                _lastLeviathanRoarRelativeVelocityMetersPerSecond = 0f;
                return 1f;
            }

            float deltaTime = math.max(0.0001f, now - _lastLeviathanRoarSampleTime);
            float radialVelocity = (_lastLeviathanRoarDistanceMeters - currentDistance) * math.rcp(deltaTime);
            radialVelocity = math.clamp(
                radialVelocity,
                -LeviathanDopplerVelocityClampMetersPerSecond,
                LeviathanDopplerVelocityClampMetersPerSecond);
            float rawRatio = 1f + (radialVelocity * SoundSpeedWaterMetersPerSecondInv);
            float clampedRatio = math.clamp(rawRatio, LeviathanDopplerMinimumPitchScale, LeviathanDopplerMaximumPitchScale);
            if (math.abs(radialVelocity - _lastLeviathanRoarRelativeVelocityMetersPerSecond) > LeviathanDopplerVelocityJumpThresholdMetersPerSecond)
            {
                float smoothingWindowSeconds = LeviathanDopplerSmoothingSamples * math.rcp(LeviathanDopplerSmoothingReferenceSampleRate);
                float blend = math.saturate(deltaTime * math.rcp(math.max(0.0001f, smoothingWindowSeconds)));
                clampedRatio = math.lerp(_targetLeviathanRoarPitchScale, clampedRatio, blend);
            }

            _lastLeviathanRoarDistanceMeters = currentDistance;
            _lastLeviathanRoarSampleTime = now;
            _lastLeviathanRoarRelativeVelocityMetersPerSecond = radialVelocity;
            return clampedRatio;
        }

        private void HandleStructuralStressTriggered(in StructuralStressAudioPayload stressInfo)
        {
            if (_boundPlayerTransform == null)
                return;

            float maxDistance = PhysicsImpactStressRadiusMeters;
            Vector3 sourcePosition = ResolveStructuralStressRuntimePosition(in stressInfo);
            if (!TryResolveBoundPlayerDistanceWithin(sourcePosition, maxDistance, out float distance))
                return;

            float proximity = 1f - math.saturate(distance * math.rcp(math.max(maxDistance, 0.001f)));
            float pressureDelta = math.saturate(math.abs(stressInfo.PressureDelta));
            float acousticTransmission = math.saturate(stressInfo.AcousticTransmission01);
            float stress = math.saturate((stressInfo.Stress01 + pressureDelta * 0.65f) * math.max(0.25f, proximity) * math.max(0.12f, acousticTransmission));
            if (stress <= 0f)
                return;

            float metallic = math.saturate(math.max(stress * 0.95f, pressureDelta));
            float clangExcitation = math.saturate(stress * 0.35f + pressureDelta * 0.75f);
            float echoExcitation = math.saturate(stress * 0.45f + pressureDelta * 0.5f);
            float echoLowPassCutoffHz = math.clamp(
                stressInfo.LowPassCutoffHz,
                AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                AcousticOcclusionUtility.OpenLowPassCutoffHertz);
            TryEnqueueImpactAudioEvent(
                stress,
                metallic,
                clangExcitation,
                echoExcitation,
                stressInfo.AcousticDelaySeconds,
                acousticTransmission,
                echoLowPassCutoffHz,
                stressInfo.PitchScale);
            _impactStressImpulseTickValue = math.max(_impactStressImpulseTickValue, stress);
            _structuralHullStressVelocityTickValue = math.max(_structuralHullStressVelocityTickValue, pressureDelta);
            _structuralPressureImpulseTickValue = math.max(_structuralPressureImpulseTickValue, pressureDelta);
            _structuralSnapTickValue = math.max(_structuralSnapTickValue, math.saturate(pressureDelta * 1.15f));
            _targetHullPressureDepthValue = math.max(_targetHullPressureDepthValue, ResolveHullPressureDepth01(stressInfo.DepthMeters));
            if (pressureDelta > 0.08f || stress > 0.65f)
            {
                float hapticDrive = math.saturate(stress * 0.45f + pressureDelta * 0.65f);
                QueueStructuralStressHaptic(
                    hapticDrive * 0.4f,
                    hapticDrive,
                    0.08f + hapticDrive * 0.11f,
                    8f,
                    3,
                    0b0011,
                    2);
            }
        }

        private void HandleBaseStructuralWarningSignal(in BaseStructuralWarningSignal warning)
        {
            if (_boundPlayerTransform == null)
                return;

            float maxDistance = PhysicsImpactStressRadiusMeters;
            AbsoluteUniversePosition sourceAup = ResolveBaseStructuralWarningAup(in warning);
            if (!TryResolveBoundPlayerDistanceWithin(in sourceAup, maxDistance, out float distance))
                return;

            float proximity = 1f - math.saturate(distance * math.rcp(math.max(maxDistance, 0.001f)));
            float stress = math.saturate(warning.HighestStress01 * math.max(0.12f, proximity) * math.max(0.08f, warning.AudioIntensity01));
            if (stress <= 0.001f)
                return;

            bool redAlert = (warning.CriticalFlags & BaseStructuralWarningSignal.FlagRedAlert) != 0u;
            float pressureDelta = redAlert ? math.saturate(stress * 1.15f) : math.saturate(stress * 0.55f);
            float clangExcitation = math.saturate(stress * (redAlert ? 0.62f : 0.38f) + warning.PanicScalar01 * 0.28f);
            float echoExcitation = math.saturate(stress * 0.5f + warning.PanicScalar01 * 0.35f);
            float echoDelaySeconds = math.clamp(distance * SoundSpeedWaterMetersPerSecondInv, 0f, SonarEchoMaximumDelaySeconds);
            float echoLowPassCutoffHz = math.lerp(720f, AcousticOcclusionUtility.OpenLowPassCutoffHertz, math.saturate(proximity));
            TryEnqueueImpactAudioEvent(
                stress,
                math.saturate(stress * 0.95f + pressureDelta * 0.25f),
                clangExcitation,
                echoExcitation,
                echoDelaySeconds,
                math.max(0.12f, proximity),
                echoLowPassCutoffHz,
                math.lerp(0.72f, 1.08f, math.saturate(stress)));
            _impactStressImpulseTickValue = math.max(_impactStressImpulseTickValue, stress);
            _structuralHullStressVelocityTickValue = math.max(_structuralHullStressVelocityTickValue, pressureDelta);
            _structuralPressureImpulseTickValue = math.max(_structuralPressureImpulseTickValue, pressureDelta);
            _structuralSnapTickValue = math.max(_structuralSnapTickValue, math.saturate(pressureDelta * 1.05f));
        }

        private static AbsoluteUniversePosition ResolveBaseStructuralWarningAup(in BaseStructuralWarningSignal warning)
        {
            return new AbsoluteUniversePosition
            {
                GridX = warning.EpicenterAup.GridX,
                GridY = warning.EpicenterAup.GridY,
                GridZ = warning.EpicenterAup.GridZ,
                LocalX = warning.EpicenterAup.Local.x,
                LocalY = warning.EpicenterAup.Local.y,
                LocalZ = warning.EpicenterAup.Local.z
            };
        }

        private static Vector3 ResolveStructuralStressRuntimePosition(in StructuralStressAudioPayload stressInfo)
        {
            if ((stressInfo.Flags & StructuralStressAudioPayload.FlagHasSourceAup) != 0)
            {
                AbsoluteUniversePosition sourceAup = new AbsoluteUniversePosition
                {
                    GridX = stressInfo.SourceAup.GridX,
                    GridY = stressInfo.SourceAup.GridY,
                    GridZ = stressInfo.SourceAup.GridZ,
                    LocalX = stressInfo.SourceAup.Local.x,
                    LocalY = stressInfo.SourceAup.Local.y,
                    LocalZ = stressInfo.SourceAup.Local.z
                };
                if (TryResolveRuntimeOriginRelativeVector3(in sourceAup, out Vector3 runtimePosition))
                    return runtimePosition;
            }

            return stressInfo.WorldPosition;
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
            return math.saturate(math.max(0f, massVelocity) * math.rcp(PhysicsImpactMassVelocityReference));
        }

        private static float ResolveImpactClangMaterialMultiplier(byte materialId)
        {
            switch (materialId)
            {
                case ItemPhysicalMetadataUtility.AudioMaterialMetal:
                    return 1.1f;

                case ItemPhysicalMetadataUtility.AudioMaterialGlass:
                    return 0.85f;

                default:
                    return 0.4f;
            }
        }

        private static float ResolveImpactEchoMaterialMultiplier(byte materialId)
        {
            switch (materialId)
            {
                case ItemPhysicalMetadataUtility.AudioMaterialMetal:
                    return 1f;

                case ItemPhysicalMetadataUtility.AudioMaterialGlass:
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

                case SonarAudioMaterialIdBiological:
                    return 0.72f;

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

                case SonarAudioMaterialIdBiological:
                    return 0.74f;

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

                case SonarAudioMaterialIdBiological:
                    return math.clamp(math.min(cutoff, 1150f), AcousticOcclusionUtility.MinimumLowPassCutoffHertz, AcousticOcclusionUtility.OpenLowPassCutoffHertz);

                default:
                    return cutoff;
            }
        }

        private void HandleActiveTransportLifecycleChanged(IPlayerTransportLifecycleOwner lifecycleOwner)
        {
            _activeTransportLifecycleOwner = lifecycleOwner;
            ResolveStructuralHullReadModelCold(lifecycleOwner);
        }

        private void TryBindFromCachedRuntimeContext()
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext == null || !playerContext.IsInitialized || playerContext.PlayerObject == null)
                return;

            if (!ReferenceEquals(_boundPlayerObject, playerContext.PlayerObject) ||
                _boundPlayerTransform == null ||
                playerMovement == null)
            {
                BindToPlayerRuntimeContext(playerContext);
            }
        }

        private void TryRegister()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registered)
            {
                _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            }

            if (!_lateFrameRegistered)
            {
                _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            }

            if (_slowTickRegistered)
                return;

            _slowTickRegistered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private bool TryRegisterRuntimeService()
        {
            if (_runtimeOwnerAborted)
                return false;

            if (_runtimeRegistered || !Application.isPlaying)
                return true;

            if (TryAbortForUsableExistingRuntime())
                return false;

            PlayerCriticalProceduralAudioRenderer registeredInstance = GlobalRegistry.PlayerCriticalAudio;
            if (!ReferenceEquals(registeredInstance, null) && !ReferenceEquals(registeredInstance, this))
            {
                if (IsPlayerCriticalAudioRuntimeUsable(registeredInstance))
                {
                    Volatile.Write(ref s_runtimeInstalled, 1);
                    AbortDuplicateRuntimeOwner();
                    return false;
                }

                GlobalRegistry.UnregisterPlayerCriticalAudioRuntime(registeredInstance);
            }

            GlobalRegistry.RegisterPlayerCriticalAudioRuntime(this);
            _runtimeRegistered = ReferenceEquals(GlobalRegistry.PlayerCriticalAudio, this);
            Volatile.Write(ref s_runtimeInstalled, _runtimeRegistered ? 1 : 0);
            if (!_runtimeRegistered)
                AbortDuplicateRuntimeOwner();
            return _runtimeRegistered;
        }

        private static bool IsPlayerCriticalAudioRuntimeUsable(PlayerCriticalProceduralAudioRenderer renderer)
        {
            return renderer != null &&
                   renderer._runtimeRegistered &&
                   renderer.isActiveAndEnabled &&
                   !renderer._runtimeOwnerAborted;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            if (_runtimeOwnerAborted)
                return true;

            if (!Application.isPlaying)
                return false;

            PlayerCriticalProceduralAudioRenderer registeredInstance = GlobalRegistry.PlayerCriticalAudio;
            if (ReferenceEquals(registeredInstance, null) || ReferenceEquals(registeredInstance, this))
                return false;

            if (IsPlayerCriticalAudioRuntimeUsable(registeredInstance))
            {
                Volatile.Write(ref s_runtimeInstalled, 1);
                AbortDuplicateRuntimeOwner();
                return true;
            }

            GlobalRegistry.UnregisterPlayerCriticalAudioRuntime(registeredInstance);
            return false;
        }

        private void AbortDuplicateRuntimeOwner()
        {
            _runtimeOwnerAborted = true;
            _runtimeRegistered = false;
            _registered = false;
            _slowTickRegistered = false;
            _lateFrameRegistered = false;
            enabled = false;
            Destroy(this);
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
            _pendingStructuralStressHapticDirty = false;
            _pendingStructuralStressHaptic = default;
        }

        private void TryUnregisterRuntimeService()
        {
            if (!_runtimeRegistered)
                return;

            GlobalRegistry.UnregisterPlayerCriticalAudioRuntime(this);
            _runtimeRegistered = false;
            Volatile.Write(ref s_runtimeInstalled, GlobalRegistry.PlayerCriticalAudio != null ? 1 : 0);
        }

        private void SubscribeTransportCoordinator()
        {
            if (playerTransportCoordinator == null)
            {
                _activeTransportLifecycleOwner = null;
                _structuralHullReadModel = null;
                _structuralHullLookupFrame = -4096;
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
            _structuralHullLookupFrame = -4096;
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
            _structuralHullLookupFrame = -4096;
        }

        private void ResolveStructuralHullReadModel(IPlayerTransportLifecycleOwner lifecycleOwner)
        {
            if (lifecycleOwner is ISubmarineHullBreachReadModel directReadModel)
            {
                _structuralHullReadModel = directReadModel;
                return;
            }

            _structuralHullReadModel = null;
            _structuralHullLookupFrame = -4096;
        }

        private void ResolveStructuralHullReadModelCold(IPlayerTransportLifecycleOwner lifecycleOwner)
        {
            if (lifecycleOwner is ISubmarineHullBreachReadModel directReadModel)
            {
                _structuralHullReadModel = directReadModel;
                return;
            }

            MonoBehaviour lifecycleBehaviour = lifecycleOwner as MonoBehaviour;
            if (lifecycleBehaviour != null && lifecycleBehaviour.TryGetComponent(out ISubmarineHullBreachReadModel readModel))
            {
                _structuralHullReadModel = readModel;
                return;
            }

            _structuralHullReadModel = null;
            _structuralHullLookupFrame = -4096;
        }

        private void RebindDataVault(IDataVault vault)
        {
            if (ReferenceEquals(_dataVault, vault))
                return;

            bool hadBuffers = _buffersInitialized;
            int previousFrameCapacity = _frameCapacity;
            bool shouldRestartWorker = hadBuffers && isActiveAndEnabled;
            bool producerStopped = !IsAudioProducerThreadAlive();
            if (Volatile.Read(ref _audioProducerRunning) != 0 || IsAudioProducerThreadAlive())
                producerStopped = StopAudioProducerThread();

            if (!producerStopped)
            {
                ClearNativeOutputBridge();
                return;
            }

            DisposeBuffers(disposeSabineReverbDelay: true);
            _dataVault = vault;
            if (!hadBuffers || vault == null || previousFrameCapacity <= 0)
                return;

            EnsureBuffers(previousFrameCapacity);
            RefreshNativeOutputBridge();
            if (shouldRestartWorker && _buffersInitialized)
                StartAudioProducerThread();
        }

        private void RefreshAudioConfiguration()
        {
            bool shouldRestartWorker = isActiveAndEnabled;
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
            RefreshSabineDelaySamples();
            ClearLowPassState();
            AudioSettings.GetDSPBufferSize(out int bufferLength, out _);
            int requestedCapacity = math.max(2048, NextPowerOfTwo(math.max(bufferLength, 1024) * 4));
            if (requestedCapacity > MaxSafeFrameCapacity)
                requestedCapacity = MaxSafeFrameCapacity;

            EnsureBuffers(requestedCapacity);
            _nativeOutputBridgeFailureLogged = false;
            RefreshNativeOutputBridge();

            if (shouldRestartWorker && isActiveAndEnabled)
                StartAudioProducerThread();
        }

        private void RefreshSabineDelaySamples()
        {
            float sampleRate = math.max(_sampleRate, 1);
            _sonarTotalDurationSamples = math.max(1, (int)(SonarTotalDurationSeconds * sampleRate + 0.999f));
            _sabineDelaySamplesA = ResolveSabineDelaySamples(SabineReverbDelayASeconds, sampleRate);
            _sabineDelaySamplesB = ResolveSabineDelaySamples(SabineReverbDelayBSeconds, sampleRate);
            _sabineDelaySamplesC = ResolveSabineDelaySamples(SabineReverbDelayCSeconds, sampleRate);
            _sabineDelaySamplesD = ResolveSabineDelaySamples(SabineReverbDelayDSeconds, sampleRate);
        }

        private bool BindVaultBackedAudioBuffers(int frameCapacity)
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            _ = ResolveVaultBuffer(vault, ref _hullScratchHandle, BufferID.PlayerCriticalHullScratch, frameCapacity, NativeArrayOptions.UninitializedMemory);
            _ = ResolveVaultBuffer(vault, ref _sonarScratchHandle, BufferID.PlayerCriticalSonarScratch, frameCapacity, NativeArrayOptions.UninitializedMemory);
            _ = ResolveVaultBuffer(vault, ref _impactEchoScratchHandle, BufferID.PlayerCriticalImpactEchoScratch, frameCapacity, NativeArrayOptions.UninitializedMemory);
            _ = ResolveVaultBuffer(vault, ref _thrusterScratchHandle, BufferID.PlayerCriticalThrusterScratch, frameCapacity, NativeArrayOptions.UninitializedMemory);
            _ = ResolveVaultBuffer(vault, ref _heartbeatScratchHandle, BufferID.PlayerCriticalHeartbeatScratch, frameCapacity, NativeArrayOptions.UninitializedMemory);
            _ = ResolveVaultBuffer(vault, ref _heartbeatDuckScratchHandle, BufferID.PlayerCriticalHeartbeatDuckScratch, frameCapacity, NativeArrayOptions.UninitializedMemory);
            _ = ResolveVaultBuffer(vault, ref _bubbleScratchHandle, BufferID.PlayerCriticalBubbleScratch, frameCapacity, NativeArrayOptions.UninitializedMemory);
            _ = ResolveVaultBuffer(vault, ref _mixScratchHandle, BufferID.PlayerCriticalMixScratch, frameCapacity, NativeArrayOptions.UninitializedMemory);
            _ = ResolveVaultBuffer(vault, ref _stereoMixScratchHandle, BufferID.PlayerCriticalStereoMixScratch, frameCapacity * BinauralOutputChannels, NativeArrayOptions.UninitializedMemory);
            _ = ResolveVaultBuffer(vault, ref _sonarEchoDelayHandle, BufferID.PlayerCriticalSonarEchoDelay, SonarEchoDelayCapacity, NativeArrayOptions.ClearMemory);
            _ = ResolveVaultBuffer(vault, ref _pendingSonarEchoTapsAHandle, BufferID.PlayerCriticalPendingSonarEchoTapsA, SonarEchoTapCapacity, NativeArrayOptions.ClearMemory);
            _ = ResolveVaultBuffer(vault, ref _pendingSonarEchoTapsBHandle, BufferID.PlayerCriticalPendingSonarEchoTapsB, SonarEchoTapCapacity, NativeArrayOptions.ClearMemory);
            _ = ResolveVaultBuffer(vault, ref _workerSonarEchoTapsHandle, BufferID.PlayerCriticalWorkerSonarEchoTaps, SonarEchoTapCapacity, NativeArrayOptions.ClearMemory);
            _ = ResolveVaultBuffer(vault, ref _sonarEchoReadCursorsHandle, BufferID.PlayerCriticalSonarEchoReadCursors, SonarEchoTapCapacity, NativeArrayOptions.ClearMemory);
            _ = ResolveVaultBuffer(vault, ref _sonarEchoFilterInput1Handle, BufferID.PlayerCriticalSonarEchoFilterInput1, SonarEchoTapCapacity, NativeArrayOptions.ClearMemory);
            _ = ResolveVaultBuffer(vault, ref _sonarEchoFilterInput2Handle, BufferID.PlayerCriticalSonarEchoFilterInput2, SonarEchoTapCapacity, NativeArrayOptions.ClearMemory);
            _ = ResolveVaultBuffer(vault, ref _sonarEchoFilterOutput1Handle, BufferID.PlayerCriticalSonarEchoFilterOutput1, SonarEchoTapCapacity, NativeArrayOptions.ClearMemory);
            _ = ResolveVaultBuffer(vault, ref _sonarEchoFilterOutput2Handle, BufferID.PlayerCriticalSonarEchoFilterOutput2, SonarEchoTapCapacity, NativeArrayOptions.ClearMemory);
            _ = ResolveVaultBuffer(vault, ref _sonarEchoCompositeCandidatesAHandle, BufferID.PlayerCriticalSonarEchoCompositeCandidatesA, SonarEchoCompositeCandidateCapacity, NativeArrayOptions.ClearMemory);
            _ = ResolveVaultBuffer(vault, ref _sonarEchoCompositeCandidatesBHandle, BufferID.PlayerCriticalSonarEchoCompositeCandidatesB, SonarEchoCompositeCandidateCapacity, NativeArrayOptions.ClearMemory);
            _ = ResolveVaultBuffer(vault, ref _sonarEchoCompositeGroupsHandle, BufferID.PlayerCriticalSonarEchoCompositeGroups, SonarEchoCompositeGroupCapacity, NativeArrayOptions.ClearMemory);
            _ = ResolveVaultBuffer(vault, ref _sonarEchoCompositeGroupCountNativeHandle, BufferID.PlayerCriticalSonarEchoCompositeGroupCount, 1, NativeArrayOptions.ClearMemory);
            _ = ResolveVaultBuffer(vault, ref _sonarEcholocationHitsHandle, BufferID.PlayerCriticalSonarEcholocationHits, SonarEchoTapCapacity, NativeArrayOptions.ClearMemory);
            _ = ResolveVaultBuffer(vault, ref _sonarEchoTapUploadRingHandle, PlayerCriticalSonarEchoTapUploadRingBufferId, SonarEchoTapCapacity, NativeArrayOptions.ClearMemory);
            _ = ResolveVaultBuffer(vault, ref _impactClangDelayHandle, BufferID.PlayerCriticalImpactClangDelay, ImpactClangDelayCapacity, NativeArrayOptions.ClearMemory);
            _ = ResolveVaultBuffer(vault, ref _thrusterCombDelayHandle, BufferID.PlayerCriticalThrusterCombDelay, ThrusterCombDelayCapacity, NativeArrayOptions.ClearMemory);
            _ = ResolveVaultBuffer(vault, ref _sabineReverbDelayHandle, BufferID.PlayerCriticalSabineReverbDelay, SabineReverbDelayCapacity, NativeArrayOptions.ClearMemory);
            _ = ResolveVaultBuffer(vault, ref _caveConvolutionImpulseHandle, BufferID.PlayerCriticalCaveConvolutionImpulse, CaveConvolutionImpulseLength, NativeArrayOptions.UninitializedMemory);
            _ = ResolveVaultBuffer(vault, ref _caveConvolutionDelayHandle, BufferID.PlayerCriticalCaveConvolutionDelay, CaveConvolutionDelayCapacity, NativeArrayOptions.ClearMemory);
            _ = ResolveVaultBuffer(vault, ref _interiorFdnDelayHandle, BufferID.PlayerCriticalInteriorFdnDelay, InteriorFdnDelayCapacity, NativeArrayOptions.ClearMemory);
            _ = ResolveVaultBuffer(vault, ref _binauralDelayRingHandle, BufferID.PlayerCriticalBinauralDelayRing, BinauralDelayCapacity, NativeArrayOptions.ClearMemory);
            _ = ResolveVaultBuffer(vault, ref _binauralShadowHistoryHandle, BufferID.PlayerCriticalBinauralShadowHistory, BinauralOutputChannels, NativeArrayOptions.ClearMemory);
            _ = ResolveVaultBuffer(vault, ref _lowPassInputHistory1Handle, BufferID.PlayerCriticalLowPassInputHistory1, MaxFilterChannels, NativeArrayOptions.ClearMemory);
            _ = ResolveVaultBuffer(vault, ref _lowPassInputHistory2Handle, BufferID.PlayerCriticalLowPassInputHistory2, MaxFilterChannels, NativeArrayOptions.ClearMemory);
            _ = ResolveVaultBuffer(vault, ref _lowPassOutputHistory1Handle, BufferID.PlayerCriticalLowPassOutputHistory1, MaxFilterChannels, NativeArrayOptions.ClearMemory);
            _ = ResolveVaultBuffer(vault, ref _lowPassOutputHistory2Handle, BufferID.PlayerCriticalLowPassOutputHistory2, MaxFilterChannels, NativeArrayOptions.ClearMemory);
            _ = ResolveVaultBuffer(vault, ref _metallicGrainBankHandle, BufferID.PlayerCriticalMetallicGrainBank, MetallicGrainBankCapacity, NativeArrayOptions.UninitializedMemory);
            _ = ResolveVaultBuffer(vault, ref _granularVoiceActiveHandle, BufferID.PlayerCriticalGranularVoiceActive, GranularVoiceCapacity, NativeArrayOptions.UninitializedMemory);
            _ = ResolveVaultBuffer(vault, ref _granularVoiceElapsedHandle, BufferID.PlayerCriticalGranularVoiceElapsed, GranularVoiceCapacity, NativeArrayOptions.UninitializedMemory);
            _ = ResolveVaultBuffer(vault, ref _granularVoiceLengthHandle, BufferID.PlayerCriticalGranularVoiceLength, GranularVoiceCapacity, NativeArrayOptions.UninitializedMemory);
            _ = ResolveVaultBuffer(vault, ref _granularVoiceStartHandle, BufferID.PlayerCriticalGranularVoiceStart, GranularVoiceCapacity, NativeArrayOptions.UninitializedMemory);
            _ = ResolveVaultBuffer(vault, ref _granularVoiceSeedHandle, BufferID.PlayerCriticalGranularVoiceSeed, GranularVoiceCapacity, NativeArrayOptions.UninitializedMemory);
            _ = ResolveVaultBuffer(vault, ref _granularVoiceCursorHandle, BufferID.PlayerCriticalGranularVoiceCursor, GranularVoiceCapacity, NativeArrayOptions.UninitializedMemory);
            _ = ResolveVaultBuffer(vault, ref _granularVoicePlaybackRateHandle, BufferID.PlayerCriticalGranularVoicePlaybackRate, GranularVoiceCapacity, NativeArrayOptions.UninitializedMemory);
            _ = ResolveVaultBuffer(vault, ref _granularVoiceGainHandle, BufferID.PlayerCriticalGranularVoiceGain, GranularVoiceCapacity, NativeArrayOptions.UninitializedMemory);
            _ = ResolveVaultBuffer(vault, ref _granularTelemetryRingHandle, BufferID.PlayerCriticalGranularTelemetryRing, GranularTelemetryCapacity, NativeArrayOptions.ClearMemory);
            _ = ResolveVaultBuffer(vault, ref _prologueTransitionTelemetryRingHandle, BufferID.PlayerCriticalPrologueTransitionTelemetryRing, PrologueTransitionTelemetryCapacity, NativeArrayOptions.ClearMemory);
            if (_audioTransitionStateLayoutValid)
                _ = ResolveVaultBuffer(vault, ref _prologueTransitionRingHandle, PlayerCriticalPrologueTransitionRingBufferId, PrologueTransitionQueueCapacity, NativeArrayOptions.ClearMemory);
            else
                _prologueTransitionRingHandle = default;
            _ = ResolveVaultBuffer(vault, ref _audioSynthesisTelemetryRingHandle, PlayerCriticalAudioSynthesisTelemetryRingBufferId, AudioSynthesisTelemetryCapacity, NativeArrayOptions.ClearMemory);
            if (!AreVaultBackedAudioBuffersCreated(vault))
            {
                ClearVaultBackedAudioBufferAliases(clearSabine: true);
                return false;
            }

            ClearVaultBackedAudioBuffers();
            return true;
        }

        private void ClearVaultBackedAudioBufferAliases(bool clearSabine)
        {
            _ = clearSabine;
        }

        private static NativeArray<T> ResolveVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int length,
            NativeArrayOptions options) where T : struct
        {
            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                math.max(1, length),
                VaultOwner,
                options);
            if (!IsPlayerCriticalVaultHandle(in handle, bufferId))
            {
                handle = default;
                return default;
            }

            return vault.TryResolveHandle(in handle, out NativeArray<T> buffer) && buffer.IsCreated
                ? buffer
                : default;
        }

        private static void ReleaseVaultBuffer<T>(IDataVault vault, ref VaultGenerationHandle<T> handle, BufferID expectedBufferId)
            where T : struct
        {
            if (vault != null && IsPlayerCriticalVaultHandle(in handle, expectedBufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool IsPlayerCriticalVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId)
            where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)expectedBufferId) &&
                   handle.SystemID == (uint)VaultOwner &&
                   handle.Generation != 0u;
        }

        private bool TryAcquirePlayerCriticalMutationBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            int requiredLength,
            ulong mutationGuardMask,
            out NativeArray<T> buffer,
            out IDataVault guardVault) where T : struct
        {
            buffer = default;
            guardVault = null;
            IDataVault vault = _dataVault;
            if (vault == null ||
                mutationGuardMask == 0UL ||
                !IsPlayerCriticalVaultHandle(in handle, expectedBufferId) ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireMutationGuard(mutationGuardMask))
            {
                NoteAudioSynthesisConsecutiveFailure();
                return false;
            }

            bool acquired = true;
            try
            {
                if (vault.IsCompactionFenceActive ||
                    !vault.TryResolveHandle(in handle, out buffer) ||
                    !buffer.IsCreated ||
                    buffer.Length < math.max(1, requiredLength))
                {
                    NoteAudioSynthesisConsecutiveFailure();
                    return false;
                }

                guardVault = vault;
                acquired = false;
                return true;
            }
            finally
            {
                if (acquired)
                {
                    vault.ReleaseMutationGuard(mutationGuardMask);
                    buffer = default;
                }
            }
        }

        private static void ReleasePlayerCriticalMutationGuard(IDataVault guardVault, ulong mutationGuardMask)
        {
            if (guardVault != null && mutationGuardMask != 0UL)
                guardVault.ReleaseMutationGuard(mutationGuardMask);
        }

        private bool TryAcquirePlayerCriticalMutationGuard(ulong mutationGuardMask, out IDataVault guardVault)
        {
            guardVault = null;
            IDataVault vault = _dataVault;
            if (vault == null ||
                mutationGuardMask == 0UL ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireMutationGuard(mutationGuardMask))
            {
                NoteAudioSynthesisConsecutiveFailure();
                return false;
            }

            guardVault = vault;
            return true;
        }

        private bool TryResolvePlayerCriticalBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsPlayerCriticalVaultHandle(in handle, expectedBufferId) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < math.max(1, requiredLength))
            {
                NoteAudioSynthesisConsecutiveFailure();
                return false;
            }

            return true;
        }

        private bool TryResolveGranularVoiceViews(IDataVault vault, out GranularVoiceVaultViews views)
        {
            views = default;
            return TryResolvePlayerCriticalBuffer(vault, in _metallicGrainBankHandle, BufferID.PlayerCriticalMetallicGrainBank, MetallicGrainBankCapacity, out views.MetallicGrainBank) &&
                   TryResolvePlayerCriticalBuffer(vault, in _granularVoiceActiveHandle, BufferID.PlayerCriticalGranularVoiceActive, GranularVoiceCapacity, out views.VoiceActive) &&
                   TryResolvePlayerCriticalBuffer(vault, in _granularVoiceElapsedHandle, BufferID.PlayerCriticalGranularVoiceElapsed, GranularVoiceCapacity, out views.VoiceElapsed) &&
                   TryResolvePlayerCriticalBuffer(vault, in _granularVoiceLengthHandle, BufferID.PlayerCriticalGranularVoiceLength, GranularVoiceCapacity, out views.VoiceLength) &&
                   TryResolvePlayerCriticalBuffer(vault, in _granularVoiceStartHandle, BufferID.PlayerCriticalGranularVoiceStart, GranularVoiceCapacity, out views.VoiceStart) &&
                   TryResolvePlayerCriticalBuffer(vault, in _granularVoiceSeedHandle, BufferID.PlayerCriticalGranularVoiceSeed, GranularVoiceCapacity, out views.VoiceSeed) &&
                   TryResolvePlayerCriticalBuffer(vault, in _granularVoiceCursorHandle, BufferID.PlayerCriticalGranularVoiceCursor, GranularVoiceCapacity, out views.VoiceCursor) &&
                   TryResolvePlayerCriticalBuffer(vault, in _granularVoicePlaybackRateHandle, BufferID.PlayerCriticalGranularVoicePlaybackRate, GranularVoiceCapacity, out views.VoicePlaybackRate) &&
                   TryResolvePlayerCriticalBuffer(vault, in _granularVoiceGainHandle, BufferID.PlayerCriticalGranularVoiceGain, GranularVoiceCapacity, out views.VoiceGain);
        }

        private bool TryResolveBinauralFilterViews(IDataVault vault, out BinauralFilterVaultViews views)
        {
            views = default;
            return TryResolvePlayerCriticalBuffer(vault, in _binauralDelayRingHandle, BufferID.PlayerCriticalBinauralDelayRing, BinauralDelayCapacity, out views.BinauralDelayRing) &&
                   TryResolvePlayerCriticalBuffer(vault, in _binauralShadowHistoryHandle, BufferID.PlayerCriticalBinauralShadowHistory, BinauralOutputChannels, out views.BinauralShadowHistory) &&
                   TryResolvePlayerCriticalBuffer(vault, in _lowPassInputHistory1Handle, BufferID.PlayerCriticalLowPassInputHistory1, MaxFilterChannels, out views.LowPassInputHistory1) &&
                   TryResolvePlayerCriticalBuffer(vault, in _lowPassInputHistory2Handle, BufferID.PlayerCriticalLowPassInputHistory2, MaxFilterChannels, out views.LowPassInputHistory2) &&
                   TryResolvePlayerCriticalBuffer(vault, in _lowPassOutputHistory1Handle, BufferID.PlayerCriticalLowPassOutputHistory1, MaxFilterChannels, out views.LowPassOutputHistory1) &&
                   TryResolvePlayerCriticalBuffer(vault, in _lowPassOutputHistory2Handle, BufferID.PlayerCriticalLowPassOutputHistory2, MaxFilterChannels, out views.LowPassOutputHistory2);
        }

        private bool TryResolveReverbViews(IDataVault vault, out ReverbVaultViews views)
        {
            views = default;
            return TryResolvePlayerCriticalBuffer(vault, in _sabineReverbDelayHandle, BufferID.PlayerCriticalSabineReverbDelay, SabineReverbDelayCapacity, out views.SabineReverbDelay) &&
                   TryResolvePlayerCriticalBuffer(vault, in _caveConvolutionImpulseHandle, BufferID.PlayerCriticalCaveConvolutionImpulse, CaveConvolutionImpulseLength, out views.CaveConvolutionImpulse) &&
                   TryResolvePlayerCriticalBuffer(vault, in _caveConvolutionDelayHandle, BufferID.PlayerCriticalCaveConvolutionDelay, CaveConvolutionDelayCapacity, out views.CaveConvolutionDelay) &&
                   TryResolvePlayerCriticalBuffer(vault, in _interiorFdnDelayHandle, BufferID.PlayerCriticalInteriorFdnDelay, InteriorFdnDelayCapacity, out views.InteriorFdnDelay);
        }

        private bool TryResolveTransientDelayViews(IDataVault vault, out TransientDelayVaultViews views)
        {
            views = default;
            return TryResolvePlayerCriticalBuffer(vault, in _impactClangDelayHandle, BufferID.PlayerCriticalImpactClangDelay, ImpactClangDelayCapacity, out views.ImpactClangDelay) &&
                   TryResolvePlayerCriticalBuffer(vault, in _thrusterCombDelayHandle, BufferID.PlayerCriticalThrusterCombDelay, ThrusterCombDelayCapacity, out views.ThrusterCombDelay);
        }

        private bool TryResolveFrameScratchViews(IDataVault vault, int frameCount, out FrameScratchVaultViews views)
        {
            int safeFrameCount = math.max(1, frameCount);
            views = default;
            return TryResolvePlayerCriticalBuffer(vault, in _hullScratchHandle, BufferID.PlayerCriticalHullScratch, safeFrameCount, out views.HullScratch) &&
                   TryResolvePlayerCriticalBuffer(vault, in _sonarScratchHandle, BufferID.PlayerCriticalSonarScratch, safeFrameCount, out views.SonarScratch) &&
                   TryResolvePlayerCriticalBuffer(vault, in _impactEchoScratchHandle, BufferID.PlayerCriticalImpactEchoScratch, safeFrameCount, out views.ImpactEchoScratch) &&
                   TryResolvePlayerCriticalBuffer(vault, in _thrusterScratchHandle, BufferID.PlayerCriticalThrusterScratch, safeFrameCount, out views.ThrusterScratch) &&
                   TryResolvePlayerCriticalBuffer(vault, in _heartbeatScratchHandle, BufferID.PlayerCriticalHeartbeatScratch, safeFrameCount, out views.HeartbeatScratch) &&
                   TryResolvePlayerCriticalBuffer(vault, in _heartbeatDuckScratchHandle, BufferID.PlayerCriticalHeartbeatDuckScratch, safeFrameCount, out views.HeartbeatDuckScratch) &&
                   TryResolvePlayerCriticalBuffer(vault, in _bubbleScratchHandle, BufferID.PlayerCriticalBubbleScratch, safeFrameCount, out views.BubbleScratch) &&
                   TryResolvePlayerCriticalBuffer(vault, in _mixScratchHandle, BufferID.PlayerCriticalMixScratch, safeFrameCount, out views.MixScratch) &&
                   TryResolvePlayerCriticalBuffer(vault, in _stereoMixScratchHandle, BufferID.PlayerCriticalStereoMixScratch, safeFrameCount * BinauralOutputChannels, out views.StereoMixScratch);
        }

        private bool TryResolveSonarTapViews(IDataVault vault, out SonarTapVaultViews views)
        {
            views = default;
            return TryResolvePlayerCriticalBuffer(vault, in _pendingSonarEchoTapsAHandle, BufferID.PlayerCriticalPendingSonarEchoTapsA, SonarEchoTapCapacity, out views.PendingA) &&
                   TryResolvePlayerCriticalBuffer(vault, in _pendingSonarEchoTapsBHandle, BufferID.PlayerCriticalPendingSonarEchoTapsB, SonarEchoTapCapacity, out views.PendingB) &&
                   TryResolvePlayerCriticalBuffer(vault, in _workerSonarEchoTapsHandle, BufferID.PlayerCriticalWorkerSonarEchoTaps, SonarEchoTapCapacity, out views.Worker) &&
                   TryResolvePlayerCriticalBuffer(vault, in _sonarEchoTapUploadRingHandle, PlayerCriticalSonarEchoTapUploadRingBufferId, SonarEchoTapCapacity, out views.UploadRing);
        }

        private bool TryResolveSonarDspViews(IDataVault vault, out SonarDspVaultViews views)
        {
            views = default;
            return TryResolvePlayerCriticalBuffer(vault, in _sonarEchoDelayHandle, BufferID.PlayerCriticalSonarEchoDelay, SonarEchoDelayCapacity, out views.EchoDelay) &&
                   TryResolvePlayerCriticalBuffer(vault, in _sonarEchoReadCursorsHandle, BufferID.PlayerCriticalSonarEchoReadCursors, SonarEchoTapCapacity, out views.ReadCursors) &&
                   TryResolvePlayerCriticalBuffer(vault, in _sonarEchoFilterInput1Handle, BufferID.PlayerCriticalSonarEchoFilterInput1, SonarEchoTapCapacity, out views.FilterInput1) &&
                   TryResolvePlayerCriticalBuffer(vault, in _sonarEchoFilterInput2Handle, BufferID.PlayerCriticalSonarEchoFilterInput2, SonarEchoTapCapacity, out views.FilterInput2) &&
                   TryResolvePlayerCriticalBuffer(vault, in _sonarEchoFilterOutput1Handle, BufferID.PlayerCriticalSonarEchoFilterOutput1, SonarEchoTapCapacity, out views.FilterOutput1) &&
                   TryResolvePlayerCriticalBuffer(vault, in _sonarEchoFilterOutput2Handle, BufferID.PlayerCriticalSonarEchoFilterOutput2, SonarEchoTapCapacity, out views.FilterOutput2);
        }

        private bool TryResolveSonarSpatialViews(IDataVault vault, out SonarSpatialVaultViews views)
        {
            views = default;
            return TryResolvePlayerCriticalBuffer(vault, in _sonarEchoCompositeCandidatesAHandle, BufferID.PlayerCriticalSonarEchoCompositeCandidatesA, SonarEchoCompositeCandidateCapacity, out views.CandidatesA) &&
                   TryResolvePlayerCriticalBuffer(vault, in _sonarEchoCompositeCandidatesBHandle, BufferID.PlayerCriticalSonarEchoCompositeCandidatesB, SonarEchoCompositeCandidateCapacity, out views.CandidatesB) &&
                   TryResolvePlayerCriticalBuffer(vault, in _sonarEchoCompositeGroupsHandle, BufferID.PlayerCriticalSonarEchoCompositeGroups, SonarEchoCompositeGroupCapacity, out views.Groups) &&
                   TryResolvePlayerCriticalBuffer(vault, in _sonarEchoCompositeGroupCountNativeHandle, BufferID.PlayerCriticalSonarEchoCompositeGroupCount, 1, out views.GroupCount) &&
                   TryResolvePlayerCriticalBuffer(vault, in _sonarEcholocationHitsHandle, BufferID.PlayerCriticalSonarEcholocationHits, SonarEchoTapCapacity, out views.Hits);
        }

        private bool TryAcquireGranularVoiceViews(out GranularVoiceVaultViews views)
        {
            views = default;
            if (!TryAcquirePlayerCriticalMutationGuard(GranularVoiceMutationGuardMask, out IDataVault guardVault))
                return false;

            bool success = false;
            try
            {
                if (!TryResolveGranularVoiceViews(guardVault, out views))
                    return false;

                if (HasGranularVoiceBuffers(ref views))
                {
                    views.GuardVault = guardVault;
                    views.GuardMask = GranularVoiceMutationGuardMask;
                    success = true;
                    return true;
                }

                NoteAudioSynthesisConsecutiveFailure();
                return false;
            }
            finally
            {
                if (!success)
                    ReleasePlayerCriticalMutationGuard(guardVault, GranularVoiceMutationGuardMask);
            }
        }

        private bool TryAcquireBinauralFilterViews(out BinauralFilterVaultViews views)
        {
            views = default;
            if (!TryAcquirePlayerCriticalMutationGuard(BinauralFilterMutationGuardMask, out IDataVault guardVault))
                return false;

            bool success = false;
            try
            {
                if (!TryResolveBinauralFilterViews(guardVault, out views))
                    return false;

                if (HasBinauralFilterBuffers(ref views))
                {
                    views.GuardVault = guardVault;
                    views.GuardMask = BinauralFilterMutationGuardMask;
                    success = true;
                    return true;
                }

                NoteAudioSynthesisConsecutiveFailure();
                return false;
            }
            finally
            {
                if (!success)
                    ReleasePlayerCriticalMutationGuard(guardVault, BinauralFilterMutationGuardMask);
            }
        }

        private bool TryAcquireReverbViews(out ReverbVaultViews views)
        {
            views = default;
            if (!TryAcquirePlayerCriticalMutationGuard(ReverbMutationGuardMask, out IDataVault guardVault))
                return false;

            bool success = false;
            try
            {
                if (!TryResolveReverbViews(guardVault, out views))
                    return false;

                if (HasReverbBuffers(ref views))
                {
                    views.GuardVault = guardVault;
                    views.GuardMask = ReverbMutationGuardMask;
                    success = true;
                    return true;
                }

                NoteAudioSynthesisConsecutiveFailure();
                return false;
            }
            finally
            {
                if (!success)
                    ReleasePlayerCriticalMutationGuard(guardVault, ReverbMutationGuardMask);
            }
        }

        private bool TryAcquireTransientDelayViews(out TransientDelayVaultViews views)
        {
            views = default;
            if (!TryAcquirePlayerCriticalMutationGuard(TransientDelayMutationGuardMask, out IDataVault guardVault))
                return false;

            bool success = false;
            try
            {
                if (!TryResolveTransientDelayViews(guardVault, out views))
                    return false;

                if (HasTransientDelayBuffers(ref views))
                {
                    views.GuardVault = guardVault;
                    views.GuardMask = TransientDelayMutationGuardMask;
                    success = true;
                    return true;
                }

                NoteAudioSynthesisConsecutiveFailure();
                return false;
            }
            finally
            {
                if (!success)
                    ReleasePlayerCriticalMutationGuard(guardVault, TransientDelayMutationGuardMask);
            }
        }

        private bool TryAcquireFrameScratchViews(int frameCount, out FrameScratchVaultViews views)
        {
            int safeFrameCount = math.max(1, frameCount);
            views = default;
            if (!TryAcquirePlayerCriticalMutationGuard(FrameScratchMutationGuardMask, out IDataVault guardVault))
                return false;

            bool success = false;
            try
            {
                if (!TryResolveFrameScratchViews(guardVault, safeFrameCount, out views))
                    return false;

                if (HasFrameScratchBuffers(ref views, safeFrameCount))
                {
                    views.GuardVault = guardVault;
                    views.GuardMask = FrameScratchMutationGuardMask;
                    success = true;
                    return true;
                }

                NoteAudioSynthesisConsecutiveFailure();
                return false;
            }
            finally
            {
                if (!success)
                    ReleasePlayerCriticalMutationGuard(guardVault, FrameScratchMutationGuardMask);
            }
        }

        private bool TryAcquireSonarTapViews(out SonarTapVaultViews views)
        {
            views = default;
            if (!TryAcquirePlayerCriticalMutationGuard(SonarTapMutationGuardMask, out IDataVault guardVault))
                return false;

            bool success = false;
            try
            {
                if (!TryResolveSonarTapViews(guardVault, out views))
                    return false;

                if (views.PendingA.IsCreated &&
                    views.PendingA.Length >= SonarEchoTapCapacity &&
                    views.PendingB.IsCreated &&
                    views.PendingB.Length >= SonarEchoTapCapacity &&
                    views.Worker.IsCreated &&
                    views.Worker.Length >= SonarEchoTapCapacity &&
                    views.UploadRing.IsCreated &&
                    views.UploadRing.Length >= SonarEchoTapCapacity)
                {
                    views.GuardVault = guardVault;
                    views.GuardMask = SonarTapMutationGuardMask;
                    success = true;
                    return true;
                }

                NoteAudioSynthesisConsecutiveFailure();
                return false;
            }
            finally
            {
                if (!success)
                    ReleasePlayerCriticalMutationGuard(guardVault, SonarTapMutationGuardMask);
            }
        }

        private bool TryAcquireSonarDspViews(out SonarDspVaultViews views)
        {
            views = default;
            if (!TryAcquirePlayerCriticalMutationGuard(SonarDspMutationGuardMask, out IDataVault guardVault))
                return false;

            bool success = false;
            try
            {
                if (!TryResolveSonarDspViews(guardVault, out views))
                    return false;

                if (views.EchoDelay.IsCreated &&
                    views.EchoDelay.Length >= SonarEchoDelayCapacity &&
                    views.ReadCursors.IsCreated &&
                    views.ReadCursors.Length >= SonarEchoTapCapacity &&
                    views.FilterInput1.IsCreated &&
                    views.FilterInput1.Length >= SonarEchoTapCapacity &&
                    views.FilterInput2.IsCreated &&
                    views.FilterInput2.Length >= SonarEchoTapCapacity &&
                    views.FilterOutput1.IsCreated &&
                    views.FilterOutput1.Length >= SonarEchoTapCapacity &&
                    views.FilterOutput2.IsCreated &&
                    views.FilterOutput2.Length >= SonarEchoTapCapacity)
                {
                    views.GuardVault = guardVault;
                    views.GuardMask = SonarDspMutationGuardMask;
                    success = true;
                    return true;
                }

                NoteAudioSynthesisConsecutiveFailure();
                return false;
            }
            finally
            {
                if (!success)
                    ReleasePlayerCriticalMutationGuard(guardVault, SonarDspMutationGuardMask);
            }
        }

        private bool TryAcquireSonarSpatialViews(out SonarSpatialVaultViews views)
        {
            views = default;
            if (!TryAcquirePlayerCriticalMutationGuard(SonarSpatialMutationGuardMask, out IDataVault guardVault))
                return false;

            bool success = false;
            try
            {
                if (!TryResolveSonarSpatialViews(guardVault, out views))
                    return false;

                if (views.CandidatesA.IsCreated &&
                    views.CandidatesA.Length >= SonarEchoCompositeCandidateCapacity &&
                    views.CandidatesB.IsCreated &&
                    views.CandidatesB.Length >= SonarEchoCompositeCandidateCapacity &&
                    views.Groups.IsCreated &&
                    views.Groups.Length >= SonarEchoCompositeGroupCapacity &&
                    views.GroupCount.IsCreated &&
                    views.GroupCount.Length > 0 &&
                    views.Hits.IsCreated &&
                    views.Hits.Length >= SonarEchoTapCapacity)
                {
                    views.GuardVault = guardVault;
                    views.GuardMask = SonarSpatialMutationGuardMask;
                    success = true;
                    return true;
                }

                NoteAudioSynthesisConsecutiveFailure();
                return false;
            }
            finally
            {
                if (!success)
                    ReleasePlayerCriticalMutationGuard(guardVault, SonarSpatialMutationGuardMask);
            }
        }

        private void ReleaseGranularVoiceMutationGuard(ref GranularVoiceVaultViews views)
        {
            ReleasePlayerCriticalMutationGuard(views.GuardVault, views.GuardMask);
            views = default;
        }

        private void ReleaseBinauralFilterMutationGuard(ref BinauralFilterVaultViews views)
        {
            ReleasePlayerCriticalMutationGuard(views.GuardVault, views.GuardMask);
            views = default;
        }

        private void ReleaseReverbMutationGuard(ref ReverbVaultViews views)
        {
            ReleasePlayerCriticalMutationGuard(views.GuardVault, views.GuardMask);
            views = default;
        }

        private void ReleaseTransientDelayMutationGuard(ref TransientDelayVaultViews views)
        {
            ReleasePlayerCriticalMutationGuard(views.GuardVault, views.GuardMask);
            views = default;
        }

        private void ReleaseSonarTapMutationGuard(ref SonarTapVaultViews views)
        {
            ReleasePlayerCriticalMutationGuard(views.GuardVault, views.GuardMask);
            views = default;
        }

        private void ReleaseSonarDspMutationGuard(ref SonarDspVaultViews views)
        {
            ReleasePlayerCriticalMutationGuard(views.GuardVault, views.GuardMask);
            views = default;
        }

        private void ReleaseSonarSpatialMutationGuard(ref SonarSpatialVaultViews views)
        {
            ReleasePlayerCriticalMutationGuard(views.GuardVault, views.GuardMask);
            views = default;
        }

        private void ReleaseFrameScratchMutationGuard(ref FrameScratchVaultViews views)
        {
            ReleasePlayerCriticalMutationGuard(views.GuardVault, views.GuardMask);
            views = default;
        }

        private bool IsReadOnlyVaultBufferCreated<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId, int requiredLength)
            where T : struct
        {
            return IsReadOnlyVaultBufferCreated(_dataVault, in handle, expectedBufferId, requiredLength);
        }

        private static bool IsReadOnlyVaultBufferCreated<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            int requiredLength)
            where T : struct
        {
            return vault != null &&
                   IsPlayerCriticalVaultHandle(in handle, expectedBufferId) &&
                   vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= math.max(1, requiredLength);
        }

        private bool AreGranularVoiceBuffersCreated(IDataVault vault)
        {
            return IsReadOnlyVaultBufferCreated(vault, in _metallicGrainBankHandle, BufferID.PlayerCriticalMetallicGrainBank, MetallicGrainBankCapacity) &&
                   IsReadOnlyVaultBufferCreated(vault, in _granularVoiceActiveHandle, BufferID.PlayerCriticalGranularVoiceActive, GranularVoiceCapacity) &&
                   IsReadOnlyVaultBufferCreated(vault, in _granularVoiceElapsedHandle, BufferID.PlayerCriticalGranularVoiceElapsed, GranularVoiceCapacity) &&
                   IsReadOnlyVaultBufferCreated(vault, in _granularVoiceLengthHandle, BufferID.PlayerCriticalGranularVoiceLength, GranularVoiceCapacity) &&
                   IsReadOnlyVaultBufferCreated(vault, in _granularVoiceStartHandle, BufferID.PlayerCriticalGranularVoiceStart, GranularVoiceCapacity) &&
                   IsReadOnlyVaultBufferCreated(vault, in _granularVoiceSeedHandle, BufferID.PlayerCriticalGranularVoiceSeed, GranularVoiceCapacity) &&
                   IsReadOnlyVaultBufferCreated(vault, in _granularVoiceCursorHandle, BufferID.PlayerCriticalGranularVoiceCursor, GranularVoiceCapacity) &&
                   IsReadOnlyVaultBufferCreated(vault, in _granularVoicePlaybackRateHandle, BufferID.PlayerCriticalGranularVoicePlaybackRate, GranularVoiceCapacity) &&
                   IsReadOnlyVaultBufferCreated(vault, in _granularVoiceGainHandle, BufferID.PlayerCriticalGranularVoiceGain, GranularVoiceCapacity);
        }

        private bool AreBinauralFilterBuffersCreated(IDataVault vault)
        {
            return IsReadOnlyVaultBufferCreated(vault, in _binauralDelayRingHandle, BufferID.PlayerCriticalBinauralDelayRing, BinauralDelayCapacity) &&
                   IsReadOnlyVaultBufferCreated(vault, in _binauralShadowHistoryHandle, BufferID.PlayerCriticalBinauralShadowHistory, BinauralOutputChannels) &&
                   IsReadOnlyVaultBufferCreated(vault, in _lowPassInputHistory1Handle, BufferID.PlayerCriticalLowPassInputHistory1, 1) &&
                   IsReadOnlyVaultBufferCreated(vault, in _lowPassInputHistory2Handle, BufferID.PlayerCriticalLowPassInputHistory2, 1) &&
                   IsReadOnlyVaultBufferCreated(vault, in _lowPassOutputHistory1Handle, BufferID.PlayerCriticalLowPassOutputHistory1, 1) &&
                   IsReadOnlyVaultBufferCreated(vault, in _lowPassOutputHistory2Handle, BufferID.PlayerCriticalLowPassOutputHistory2, 1);
        }

        private static bool HasBinauralFilterBuffers(ref BinauralFilterVaultViews views)
        {
            return views.BinauralDelayRing.IsCreated &&
                   views.BinauralDelayRing.Length == BinauralDelayCapacity &&
                   views.BinauralShadowHistory.IsCreated &&
                   views.BinauralShadowHistory.Length >= BinauralOutputChannels &&
                   views.LowPassInputHistory1.IsCreated &&
                   views.LowPassInputHistory1.Length > 0 &&
                   views.LowPassInputHistory2.IsCreated &&
                   views.LowPassInputHistory2.Length > 0 &&
                   views.LowPassOutputHistory1.IsCreated &&
                   views.LowPassOutputHistory1.Length > 0 &&
                   views.LowPassOutputHistory2.IsCreated &&
                   views.LowPassOutputHistory2.Length > 0;
        }

        private bool AreReverbBuffersCreated(IDataVault vault)
        {
            return IsReadOnlyVaultBufferCreated(vault, in _sabineReverbDelayHandle, BufferID.PlayerCriticalSabineReverbDelay, SabineReverbDelayCapacity) &&
                   IsReadOnlyVaultBufferCreated(vault, in _caveConvolutionImpulseHandle, BufferID.PlayerCriticalCaveConvolutionImpulse, CaveConvolutionImpulseLength) &&
                   IsReadOnlyVaultBufferCreated(vault, in _caveConvolutionDelayHandle, BufferID.PlayerCriticalCaveConvolutionDelay, CaveConvolutionDelayCapacity) &&
                   IsReadOnlyVaultBufferCreated(vault, in _interiorFdnDelayHandle, BufferID.PlayerCriticalInteriorFdnDelay, InteriorFdnDelayCapacity);
        }

        private static bool HasReverbBuffers(ref ReverbVaultViews views)
        {
            return views.SabineReverbDelay.IsCreated &&
                   views.SabineReverbDelay.Length >= SabineReverbDelayCapacity &&
                   views.CaveConvolutionImpulse.IsCreated &&
                   views.CaveConvolutionImpulse.Length >= CaveConvolutionImpulseLength &&
                   views.CaveConvolutionDelay.IsCreated &&
                   views.CaveConvolutionDelay.Length >= CaveConvolutionDelayCapacity &&
                   views.InteriorFdnDelay.IsCreated &&
                   views.InteriorFdnDelay.Length >= InteriorFdnDelayCapacity;
        }

        private bool AreTransientDelayBuffersCreated(IDataVault vault)
        {
            return IsReadOnlyVaultBufferCreated(vault, in _impactClangDelayHandle, BufferID.PlayerCriticalImpactClangDelay, ImpactClangDelayCapacity) &&
                   IsReadOnlyVaultBufferCreated(vault, in _thrusterCombDelayHandle, BufferID.PlayerCriticalThrusterCombDelay, ThrusterCombDelayCapacity);
        }

        private static bool HasTransientDelayBuffers(ref TransientDelayVaultViews views)
        {
            return views.ImpactClangDelay.IsCreated &&
                   views.ImpactClangDelay.Length >= ImpactClangDelayCapacity &&
                   views.ThrusterCombDelay.IsCreated &&
                   views.ThrusterCombDelay.Length >= ThrusterCombDelayCapacity;
        }

        private bool AreFrameScratchBuffersCreated(IDataVault vault, int frameCount)
        {
            int safeFrameCount = math.max(1, frameCount);
            return IsReadOnlyVaultBufferCreated(vault, in _hullScratchHandle, BufferID.PlayerCriticalHullScratch, safeFrameCount) &&
                   IsReadOnlyVaultBufferCreated(vault, in _sonarScratchHandle, BufferID.PlayerCriticalSonarScratch, safeFrameCount) &&
                   IsReadOnlyVaultBufferCreated(vault, in _impactEchoScratchHandle, BufferID.PlayerCriticalImpactEchoScratch, safeFrameCount) &&
                   IsReadOnlyVaultBufferCreated(vault, in _thrusterScratchHandle, BufferID.PlayerCriticalThrusterScratch, safeFrameCount) &&
                   IsReadOnlyVaultBufferCreated(vault, in _heartbeatScratchHandle, BufferID.PlayerCriticalHeartbeatScratch, safeFrameCount) &&
                   IsReadOnlyVaultBufferCreated(vault, in _heartbeatDuckScratchHandle, BufferID.PlayerCriticalHeartbeatDuckScratch, safeFrameCount) &&
                   IsReadOnlyVaultBufferCreated(vault, in _bubbleScratchHandle, BufferID.PlayerCriticalBubbleScratch, safeFrameCount) &&
                   IsReadOnlyVaultBufferCreated(vault, in _mixScratchHandle, BufferID.PlayerCriticalMixScratch, safeFrameCount) &&
                   IsReadOnlyVaultBufferCreated(vault, in _stereoMixScratchHandle, BufferID.PlayerCriticalStereoMixScratch, safeFrameCount * BinauralOutputChannels);
        }

        private static bool HasFrameScratchBuffers(ref FrameScratchVaultViews views, int frameCount)
        {
            int safeFrameCount = math.max(1, frameCount);
            return HasNativeBufferFrames(views.HullScratch, safeFrameCount) &&
                   HasNativeBufferFrames(views.SonarScratch, safeFrameCount) &&
                   HasNativeBufferFrames(views.ImpactEchoScratch, safeFrameCount) &&
                   HasNativeBufferFrames(views.ThrusterScratch, safeFrameCount) &&
                   HasNativeBufferFrames(views.HeartbeatScratch, safeFrameCount) &&
                   HasNativeBufferFrames(views.HeartbeatDuckScratch, safeFrameCount) &&
                   HasNativeBufferFrames(views.BubbleScratch, safeFrameCount) &&
                   HasNativeBufferFrames(views.MixScratch, safeFrameCount) &&
                   views.StereoMixScratch.IsCreated &&
                   views.StereoMixScratch.Length >= safeFrameCount * BinauralOutputChannels;
        }

        private bool AreSonarTapBuffersCreated(IDataVault vault)
        {
            return IsReadOnlyVaultBufferCreated(vault, in _pendingSonarEchoTapsAHandle, BufferID.PlayerCriticalPendingSonarEchoTapsA, SonarEchoTapCapacity) &&
                   IsReadOnlyVaultBufferCreated(vault, in _pendingSonarEchoTapsBHandle, BufferID.PlayerCriticalPendingSonarEchoTapsB, SonarEchoTapCapacity) &&
                   IsReadOnlyVaultBufferCreated(vault, in _workerSonarEchoTapsHandle, BufferID.PlayerCriticalWorkerSonarEchoTaps, SonarEchoTapCapacity) &&
                   IsReadOnlyVaultBufferCreated(vault, in _sonarEchoTapUploadRingHandle, PlayerCriticalSonarEchoTapUploadRingBufferId, SonarEchoTapCapacity);
        }

        private bool AreSonarDspBuffersCreated(IDataVault vault)
        {
            return IsReadOnlyVaultBufferCreated(vault, in _sonarEchoDelayHandle, BufferID.PlayerCriticalSonarEchoDelay, SonarEchoDelayCapacity) &&
                   IsReadOnlyVaultBufferCreated(vault, in _sonarEchoReadCursorsHandle, BufferID.PlayerCriticalSonarEchoReadCursors, SonarEchoTapCapacity) &&
                   IsReadOnlyVaultBufferCreated(vault, in _sonarEchoFilterInput1Handle, BufferID.PlayerCriticalSonarEchoFilterInput1, SonarEchoTapCapacity) &&
                   IsReadOnlyVaultBufferCreated(vault, in _sonarEchoFilterInput2Handle, BufferID.PlayerCriticalSonarEchoFilterInput2, SonarEchoTapCapacity) &&
                   IsReadOnlyVaultBufferCreated(vault, in _sonarEchoFilterOutput1Handle, BufferID.PlayerCriticalSonarEchoFilterOutput1, SonarEchoTapCapacity) &&
                   IsReadOnlyVaultBufferCreated(vault, in _sonarEchoFilterOutput2Handle, BufferID.PlayerCriticalSonarEchoFilterOutput2, SonarEchoTapCapacity);
        }

        private bool AreSonarSpatialBuffersCreated(IDataVault vault)
        {
            return IsReadOnlyVaultBufferCreated(vault, in _sonarEchoCompositeCandidatesAHandle, BufferID.PlayerCriticalSonarEchoCompositeCandidatesA, SonarEchoCompositeCandidateCapacity) &&
                   IsReadOnlyVaultBufferCreated(vault, in _sonarEchoCompositeCandidatesBHandle, BufferID.PlayerCriticalSonarEchoCompositeCandidatesB, SonarEchoCompositeCandidateCapacity) &&
                   IsReadOnlyVaultBufferCreated(vault, in _sonarEchoCompositeGroupsHandle, BufferID.PlayerCriticalSonarEchoCompositeGroups, SonarEchoCompositeGroupCapacity) &&
                   IsReadOnlyVaultBufferCreated(vault, in _sonarEchoCompositeGroupCountNativeHandle, BufferID.PlayerCriticalSonarEchoCompositeGroupCount, 1) &&
                   IsReadOnlyVaultBufferCreated(vault, in _sonarEcholocationHitsHandle, BufferID.PlayerCriticalSonarEcholocationHits, SonarEchoTapCapacity);
        }

        private bool TryReadSonarHitView(out NativeArray<AcousticEcholocationRayHit>.ReadOnly hits)
        {
            hits = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   IsPlayerCriticalVaultHandle(in _sonarEcholocationHitsHandle, BufferID.PlayerCriticalSonarEcholocationHits) &&
                   vault.TryReadOnlyHandle(in _sonarEcholocationHitsHandle, out hits) &&
                   hits.IsCreated &&
                   hits.Length >= SonarEchoTapCapacity;
        }

        private bool AreVaultBackedAudioBuffersCreated(IDataVault vault)
        {
            return AreFrameScratchBuffersCreated(vault, 1) &&
                   AreSonarDspBuffersCreated(vault) &&
                   AreSonarTapBuffersCreated(vault) &&
                   AreSonarSpatialBuffersCreated(vault) &&
                   AreTransientDelayBuffersCreated(vault) &&
                   AreReverbBuffersCreated(vault) &&
                   AreBinauralFilterBuffersCreated(vault) &&
                   AreGranularVoiceBuffersCreated(vault) &&
                   IsReadOnlyVaultBufferCreated(vault, in _granularTelemetryRingHandle, BufferID.PlayerCriticalGranularTelemetryRing, GranularTelemetryCapacity) &&
                   IsReadOnlyVaultBufferCreated(vault, in _prologueTransitionTelemetryRingHandle, BufferID.PlayerCriticalPrologueTransitionTelemetryRing, PrologueTransitionTelemetryCapacity) &&
                   IsReadOnlyVaultBufferCreated(vault, in _prologueTransitionRingHandle, PlayerCriticalPrologueTransitionRingBufferId, PrologueTransitionQueueCapacity) &&
                   IsReadOnlyVaultBufferCreated(vault, in _audioSynthesisTelemetryRingHandle, PlayerCriticalAudioSynthesisTelemetryRingBufferId, AudioSynthesisTelemetryCapacity);
        }

        private void ClearVaultBackedAudioBuffers()
        {
            if (TryAcquireFrameScratchViews(1, out FrameScratchVaultViews frameViews))
            {
                try
                {
                    ClearNativeBuffer(frameViews.HullScratch);
                    ClearNativeBuffer(frameViews.SonarScratch);
                    ClearNativeBuffer(frameViews.ImpactEchoScratch);
                    ClearNativeBuffer(frameViews.ThrusterScratch);
                    ClearNativeBuffer(frameViews.HeartbeatScratch);
                    ClearNativeBuffer(frameViews.HeartbeatDuckScratch);
                    ClearNativeBuffer(frameViews.BubbleScratch);
                    ClearNativeBuffer(frameViews.MixScratch);
                    ClearNativeBuffer(frameViews.StereoMixScratch);
                }
                finally
                {
                    ReleaseFrameScratchMutationGuard(ref frameViews);
                }
            }

            if (TryAcquireSonarDspViews(out SonarDspVaultViews sonarDspViews))
            {
                try
                {
                    ClearNativeBuffer(sonarDspViews.EchoDelay);
                    ClearNativeBuffer(sonarDspViews.ReadCursors);
                    ClearNativeBuffer(sonarDspViews.FilterInput1);
                    ClearNativeBuffer(sonarDspViews.FilterInput2);
                    ClearNativeBuffer(sonarDspViews.FilterOutput1);
                    ClearNativeBuffer(sonarDspViews.FilterOutput2);
                }
                finally
                {
                    ReleaseSonarDspMutationGuard(ref sonarDspViews);
                }
            }

            if (TryAcquireSonarTapViews(out SonarTapVaultViews sonarTapViews))
            {
                try
                {
                    ClearNativeBuffer(sonarTapViews.PendingA);
                    ClearNativeBuffer(sonarTapViews.PendingB);
                    ClearNativeBuffer(sonarTapViews.Worker);
                    ClearNativeBuffer(sonarTapViews.UploadRing);
                }
                finally
                {
                    ReleaseSonarTapMutationGuard(ref sonarTapViews);
                }
            }

            if (TryAcquireSonarSpatialViews(out SonarSpatialVaultViews sonarSpatialViews))
            {
                try
                {
                    ClearNativeBuffer(sonarSpatialViews.CandidatesA);
                    ClearNativeBuffer(sonarSpatialViews.CandidatesB);
                    ClearNativeBuffer(sonarSpatialViews.Groups);
                    ClearNativeBuffer(sonarSpatialViews.GroupCount);
                    ClearNativeBuffer(sonarSpatialViews.Hits);
                }
                finally
                {
                    ReleaseSonarSpatialMutationGuard(ref sonarSpatialViews);
                }
            }
            if (TryAcquireTransientDelayViews(out TransientDelayVaultViews transientViews))
            {
                try
                {
                    ClearNativeBuffer(transientViews.ImpactClangDelay);
                    ClearNativeBuffer(transientViews.ThrusterCombDelay);
                }
                finally
                {
                    ReleaseTransientDelayMutationGuard(ref transientViews);
                }
            }

            if (TryAcquireReverbViews(out ReverbVaultViews reverbViews))
            {
                try
                {
                    ClearNativeBuffer(reverbViews.SabineReverbDelay);
                    ClearNativeBuffer(reverbViews.CaveConvolutionImpulse);
                    ClearNativeBuffer(reverbViews.CaveConvolutionDelay);
                    ClearNativeBuffer(reverbViews.InteriorFdnDelay);
                }
                finally
                {
                    ReleaseReverbMutationGuard(ref reverbViews);
                }
            }

            if (TryAcquireBinauralFilterViews(out BinauralFilterVaultViews filterViews))
            {
                try
                {
                    ClearNativeBuffer(filterViews.BinauralDelayRing);
                    ClearNativeBuffer(filterViews.BinauralShadowHistory);
                    ClearNativeBuffer(filterViews.LowPassInputHistory1);
                    ClearNativeBuffer(filterViews.LowPassInputHistory2);
                    ClearNativeBuffer(filterViews.LowPassOutputHistory1);
                    ClearNativeBuffer(filterViews.LowPassOutputHistory2);
                }
                finally
                {
                    ReleaseBinauralFilterMutationGuard(ref filterViews);
                }
            }

            if (TryAcquireGranularVoiceViews(out GranularVoiceVaultViews granularViews))
            {
                try
                {
                    ClearNativeBuffer(granularViews.VoiceActive);
                    ClearNativeBuffer(granularViews.VoiceElapsed);
                    ClearNativeBuffer(granularViews.VoiceLength);
                    ClearNativeBuffer(granularViews.VoiceStart);
                    ClearNativeBuffer(granularViews.VoiceSeed);
                    ClearNativeBuffer(granularViews.VoiceCursor);
                    ClearNativeBuffer(granularViews.VoicePlaybackRate);
                    ClearNativeBuffer(granularViews.VoiceGain);
                }
                finally
                {
                    ReleaseGranularVoiceMutationGuard(ref granularViews);
                }
            }

            IDataVault telemetryGuardVault = null;
            if (TryAcquirePlayerCriticalMutationBuffer(
                    in _granularTelemetryRingHandle,
                    BufferID.PlayerCriticalGranularTelemetryRing,
                    GranularTelemetryCapacity,
                    GranularTelemetryMutationGuardMask,
                    out NativeArray<GranularAudioTelemetryEntry> granularTelemetryRing,
                    out telemetryGuardVault))
            {
                try
                {
                    ClearNativeBuffer(granularTelemetryRing);
                }
                finally
                {
                    ReleasePlayerCriticalMutationGuard(telemetryGuardVault, GranularTelemetryMutationGuardMask);
                }
            }

            telemetryGuardVault = null;
            if (TryAcquirePlayerCriticalMutationBuffer(
                    in _prologueTransitionTelemetryRingHandle,
                    BufferID.PlayerCriticalPrologueTransitionTelemetryRing,
                    PrologueTransitionTelemetryCapacity,
                    PrologueTransitionTelemetryMutationGuardMask,
                    out NativeArray<PrologueAudioTransitionTelemetryEntry> prologueTelemetryRing,
                    out telemetryGuardVault))
            {
                try
                {
                    ClearNativeBuffer(prologueTelemetryRing);
                }
                finally
                {
                    ReleasePlayerCriticalMutationGuard(telemetryGuardVault, PrologueTransitionTelemetryMutationGuardMask);
                }
            }

            telemetryGuardVault = null;
            if (TryAcquirePlayerCriticalMutationBuffer(
                    in _prologueTransitionRingHandle,
                    PlayerCriticalPrologueTransitionRingBufferId,
                    PrologueTransitionQueueCapacity,
                    PrologueTransitionRingMutationGuardMask,
                    out NativeArray<AudioTransitionState> prologueTransitionRing,
                    out telemetryGuardVault))
            {
                try
                {
                    ClearNativeBuffer(prologueTransitionRing);
                }
                finally
                {
                    ReleasePlayerCriticalMutationGuard(telemetryGuardVault, PrologueTransitionRingMutationGuardMask);
                }
            }

            telemetryGuardVault = null;
            if (TryAcquirePlayerCriticalMutationBuffer(
                    in _audioSynthesisTelemetryRingHandle,
                    PlayerCriticalAudioSynthesisTelemetryRingBufferId,
                    AudioSynthesisTelemetryCapacity,
                    AudioSynthesisTelemetryMutationGuardMask,
                    out NativeArray<AudioSynthesisTelemetryEntry> audioSynthesisTelemetryRing,
                    out telemetryGuardVault))
            {
                try
                {
                    ClearNativeBuffer(audioSynthesisTelemetryRing);
                }
                finally
                {
                    ReleasePlayerCriticalMutationGuard(telemetryGuardVault, AudioSynthesisTelemetryMutationGuardMask);
                }
            }
        }

        private static void ClearNativeBuffer<T>(NativeArray<T> buffer) where T : struct
        {
            if (!buffer.IsCreated)
                return;

            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = default;
        }

        private static void ClearRing<T>(NativeArray<T> ring, int capacity) where T : struct
        {
            if (!ring.IsCreated)
                return;

            int safeCapacity = math.min(math.max(0, capacity), ring.Length);
            for (int i = 0; i < safeCapacity; i++)
                ring[i] = default;
        }

        private static bool TryWriteRing<T>(NativeArray<T> ring, ref int writeIndex, int count, int capacity, in T value) where T : struct
        {
            if (!ring.IsCreated || capacity <= 0 || count >= capacity)
                return false;

            int safeCapacity = math.min(capacity, ring.Length);
            if (safeCapacity <= 0)
                return false;

            int index = writeIndex;
            if ((uint)index >= (uint)safeCapacity)
                index = 0;

            ring[index] = value;
            writeIndex = index + 1;
            if (writeIndex >= safeCapacity)
                writeIndex = 0;
            return true;
        }

        private static bool TryReadRing<T>(NativeArray<T> ring, ref int readIndex, int count, int capacity, out T value) where T : struct
        {
            if (!ring.IsCreated || capacity <= 0 || count <= 0)
            {
                value = default;
                return false;
            }

            int safeCapacity = math.min(capacity, ring.Length);
            if (safeCapacity <= 0)
            {
                value = default;
                return false;
            }

            int index = readIndex;
            if ((uint)index >= (uint)safeCapacity)
                index = 0;

            value = ring[index];
            ring[index] = default;
            readIndex = index + 1;
            if (readIndex >= safeCapacity)
                readIndex = 0;
            return true;
        }

        private void EnsureBuffers(int frameCapacity)
        {
            if (_buffersInitialized && _frameCapacity == frameCapacity)
                return;

            bool retainedSabineReverbDelay =
                IsReadOnlyVaultBufferCreated(in _sabineReverbDelayHandle, BufferID.PlayerCriticalSabineReverbDelay, SabineReverbDelayCapacity);
            DisposeBuffers(disposeSabineReverbDelay: false);

            _frameCapacity = frameCapacity;
            if (!BindVaultBackedAudioBuffers(_frameCapacity))
            {
                _frameCapacity = 0;
                return;
            }

            PrewarmSonarEchoTapUploadQueue();
            PrewarmPrologueTransitionQueue();
            WarmPrologueSplashdownBurstProbeCold();
            RegisterNativeBuffers(registerSabineReverbDelay: !retainedSabineReverbDelay);
            if (TryAcquireReverbViews(out ReverbVaultViews reverbViews))
            {
                try
                {
                    BakeCaveConvolutionImpulseResponse(reverbViews.CaveConvolutionImpulse);
                }
                finally
                {
                    ReleaseReverbMutationGuard(ref reverbViews);
                }
            }
            PopulateMetallicGrainBank();
            _sampleRingBuffer ??= new AudioFrameSpscRingBuffer();
            int audioBufferCapacity = AudioFrameSpscRingBuffer.ResolvePowerOfTwoCapacity(
                math.max(frameCapacity * 16, ringBufferCapacityFrames));
            _sampleRingBuffer.Initialize(audioBufferCapacity, BinauralOutputChannels);
            if (!_sampleRingBuffer.IsCreated ||
                _sampleRingBuffer.CapacityFrames != audioBufferCapacity ||
                _sampleRingBuffer.SourceChannels != BinauralOutputChannels)
            {
                DisposeBuffers(disposeSabineReverbDelay: true);
                return;
            }

            _producedSampleCount = 0L;
            Volatile.Write(ref _audioProducerUnderrunWindowActive, 0);
            _workerActiveSonarState = default;
            _workerConsumedSonarSequence = 0;
            _workerConsumedSonarRevision = 0;
            _workerActiveSonarTapCount = 0;
            _sonarEchoCompositeCandidateCountA = 0;
            _sonarEchoCompositeCandidateCountB = 0;
            _sonarEchoTapUploadReadIndex = 0;
            _sonarEchoTapUploadWriteIndex = 0;
            _sonarEchoTapUploadCount = 0;
            Interlocked.Exchange(ref _impactEventQueueDropCount, 0);
            _sonarEchoCompositeWriteBufferIndex = 0;
            _sonarEchoCompositeScheduledBufferIndex = -1;
            _sonarEchoCompositeScheduledCandidateCount = 0;
            _sonarEcholocationScheduledSequence = 0;
            _sonarEcholocationScheduledRayCount = 0;
            _sonarEcholocationScheduledSdfVersion = 0;
            _sonarEcholocationScheduledShiftSequence = HectonFloatingOrigin.CurrentShiftSequence;
            _sonarEcholocationScheduledStartFrame = 0L;
            _sonarEcholocationScheduledIntensity = 0f;
            _sonarEcholocationScheduledOrigin = Vector3.zero;
            _sonarEcholocationScheduledTransform = null;
            _lastConsumedAcousticPingSignalSequence = 0;
            _lastDirectSonarPingFrame = -4096;
            _lastDirectSonarPingIntensity = 0f;
            _lastDirectSonarPingOrigin = Vector3.zero;
            _pendingSonarEchoTapCountA = 0;
            _pendingSonarEchoTapCountB = 0;
            _pendingSonarStateA = default;
            _pendingSonarStateB = default;
            _granularTelemetryCursor = 0;
            Interlocked.Exchange(ref _granularTelemetryDumpRequested, 0);
            Interlocked.Exchange(ref _granularTelemetryDumped, 0);
            _prologueTransitionTelemetryCursor = 0;
            _prologueTransitionReadIndex = 0;
            _prologueTransitionWriteIndex = 0;
            _prologueTransitionQueueCount = 0;
            Interlocked.Exchange(ref _prologueTransitionTelemetryDumpRequested, 0);
            Interlocked.Exchange(ref _prologueTransitionTelemetryDumped, 0);
            _audioSynthesisTelemetryCursor = 0;
            Interlocked.Exchange(ref _audioSynthesisTelemetryDumpRequested, 0);
            Interlocked.Exchange(ref _audioSynthesisTelemetryDumped, 0);
            Interlocked.Exchange(ref _audioSynthesisConsecutiveVaultFailures, 0);
            ResetPrologueDspState();
            _heartbeatSynthesisState = default;
            _sabineReverbSynthesisState = default;
            _caveConvolutionReverbSynthesisState = default;
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
            _ = registerSabineReverbDelay;
        }

        private void UnregisterNativeBuffers(bool unregisterSabineReverbDelay)
        {
            _ = unregisterSabineReverbDelay;
        }

        private void DisposeBuffers(bool disposeSabineReverbDelay)
        {
            ClearNativeOutputBridge();
            AudioFrameSpscRingBuffer sampleRingBuffer = _sampleRingBuffer;
            if (sampleRingBuffer != null && sampleRingBuffer.TryDispose())
                _sampleRingBuffer = null;

            UnregisterNativeBuffers(disposeSabineReverbDelay);
            if (disposeSabineReverbDelay && _dataVault != null)
                ReleaseVaultBackedAudioBufferHandles(_dataVault);

            _granularTelemetryCursor = 0;
            Interlocked.Exchange(ref _granularTelemetryDumpRequested, 0);
            Interlocked.Exchange(ref _granularTelemetryDumped, 0);
            _prologueTransitionTelemetryCursor = 0;
            _sonarEchoTapUploadReadIndex = 0;
            _sonarEchoTapUploadWriteIndex = 0;
            _sonarEchoTapUploadCount = 0;
            _prologueTransitionReadIndex = 0;
            _prologueTransitionWriteIndex = 0;
            _prologueTransitionQueueCount = 0;
            Interlocked.Exchange(ref _prologueTransitionTelemetryDumpRequested, 0);
            Interlocked.Exchange(ref _prologueTransitionTelemetryDumped, 0);
            _audioSynthesisTelemetryCursor = 0;
            Interlocked.Exchange(ref _audioSynthesisTelemetryDumpRequested, 0);
            Interlocked.Exchange(ref _audioSynthesisTelemetryDumped, 0);
            Interlocked.Exchange(ref _audioSynthesisConsecutiveVaultFailures, 0);
            ResetPrologueDspState();

            _buffersInitialized = false;
            _frameCapacity = 0;
            _producedSampleCount = 0L;
            Volatile.Write(ref _audioProducerUnderrunWindowActive, 0);
            _binauralDelayWriteIndex = 0;
            _sonarEchoCompositeCandidateCountA = 0;
            _sonarEchoCompositeCandidateCountB = 0;
            _sonarEchoCompositeWriteBufferIndex = 0;
            _sonarEchoCompositeScheduledBufferIndex = -1;
            _sonarEchoCompositeScheduledCandidateCount = 0;
            _sabineReverbSynthesisState = default;
            _caveConvolutionReverbSynthesisState = default;
        }

        private void ReleaseVaultBackedAudioBufferHandles(IDataVault vault)
        {
            ReleaseVaultBuffer(vault, ref _hullScratchHandle, BufferID.PlayerCriticalHullScratch);
            ReleaseVaultBuffer(vault, ref _sonarScratchHandle, BufferID.PlayerCriticalSonarScratch);
            ReleaseVaultBuffer(vault, ref _impactEchoScratchHandle, BufferID.PlayerCriticalImpactEchoScratch);
            ReleaseVaultBuffer(vault, ref _thrusterScratchHandle, BufferID.PlayerCriticalThrusterScratch);
            ReleaseVaultBuffer(vault, ref _heartbeatScratchHandle, BufferID.PlayerCriticalHeartbeatScratch);
            ReleaseVaultBuffer(vault, ref _heartbeatDuckScratchHandle, BufferID.PlayerCriticalHeartbeatDuckScratch);
            ReleaseVaultBuffer(vault, ref _bubbleScratchHandle, BufferID.PlayerCriticalBubbleScratch);
            ReleaseVaultBuffer(vault, ref _mixScratchHandle, BufferID.PlayerCriticalMixScratch);
            ReleaseVaultBuffer(vault, ref _stereoMixScratchHandle, BufferID.PlayerCriticalStereoMixScratch);
            ReleaseVaultBuffer(vault, ref _sonarEchoDelayHandle, BufferID.PlayerCriticalSonarEchoDelay);
            ReleaseVaultBuffer(vault, ref _pendingSonarEchoTapsAHandle, BufferID.PlayerCriticalPendingSonarEchoTapsA);
            ReleaseVaultBuffer(vault, ref _pendingSonarEchoTapsBHandle, BufferID.PlayerCriticalPendingSonarEchoTapsB);
            ReleaseVaultBuffer(vault, ref _workerSonarEchoTapsHandle, BufferID.PlayerCriticalWorkerSonarEchoTaps);
            ReleaseVaultBuffer(vault, ref _sonarEchoReadCursorsHandle, BufferID.PlayerCriticalSonarEchoReadCursors);
            ReleaseVaultBuffer(vault, ref _sonarEchoFilterInput1Handle, BufferID.PlayerCriticalSonarEchoFilterInput1);
            ReleaseVaultBuffer(vault, ref _sonarEchoFilterInput2Handle, BufferID.PlayerCriticalSonarEchoFilterInput2);
            ReleaseVaultBuffer(vault, ref _sonarEchoFilterOutput1Handle, BufferID.PlayerCriticalSonarEchoFilterOutput1);
            ReleaseVaultBuffer(vault, ref _sonarEchoFilterOutput2Handle, BufferID.PlayerCriticalSonarEchoFilterOutput2);
            ReleaseVaultBuffer(vault, ref _sonarEchoCompositeCandidatesAHandle, BufferID.PlayerCriticalSonarEchoCompositeCandidatesA);
            ReleaseVaultBuffer(vault, ref _sonarEchoCompositeCandidatesBHandle, BufferID.PlayerCriticalSonarEchoCompositeCandidatesB);
            ReleaseVaultBuffer(vault, ref _sonarEchoCompositeGroupsHandle, BufferID.PlayerCriticalSonarEchoCompositeGroups);
            ReleaseVaultBuffer(vault, ref _sonarEchoCompositeGroupCountNativeHandle, BufferID.PlayerCriticalSonarEchoCompositeGroupCount);
            ReleaseVaultBuffer(vault, ref _sonarEcholocationHitsHandle, BufferID.PlayerCriticalSonarEcholocationHits);
            ReleaseVaultBuffer(vault, ref _sonarEchoTapUploadRingHandle, PlayerCriticalSonarEchoTapUploadRingBufferId);
            ReleaseVaultBuffer(vault, ref _impactClangDelayHandle, BufferID.PlayerCriticalImpactClangDelay);
            ReleaseVaultBuffer(vault, ref _thrusterCombDelayHandle, BufferID.PlayerCriticalThrusterCombDelay);
            ReleaseVaultBuffer(vault, ref _sabineReverbDelayHandle, BufferID.PlayerCriticalSabineReverbDelay);
            ReleaseVaultBuffer(vault, ref _caveConvolutionImpulseHandle, BufferID.PlayerCriticalCaveConvolutionImpulse);
            ReleaseVaultBuffer(vault, ref _caveConvolutionDelayHandle, BufferID.PlayerCriticalCaveConvolutionDelay);
            ReleaseVaultBuffer(vault, ref _interiorFdnDelayHandle, BufferID.PlayerCriticalInteriorFdnDelay);
            ReleaseVaultBuffer(vault, ref _binauralDelayRingHandle, BufferID.PlayerCriticalBinauralDelayRing);
            ReleaseVaultBuffer(vault, ref _binauralShadowHistoryHandle, BufferID.PlayerCriticalBinauralShadowHistory);
            ReleaseVaultBuffer(vault, ref _lowPassInputHistory1Handle, BufferID.PlayerCriticalLowPassInputHistory1);
            ReleaseVaultBuffer(vault, ref _lowPassInputHistory2Handle, BufferID.PlayerCriticalLowPassInputHistory2);
            ReleaseVaultBuffer(vault, ref _lowPassOutputHistory1Handle, BufferID.PlayerCriticalLowPassOutputHistory1);
            ReleaseVaultBuffer(vault, ref _lowPassOutputHistory2Handle, BufferID.PlayerCriticalLowPassOutputHistory2);
            ReleaseVaultBuffer(vault, ref _metallicGrainBankHandle, BufferID.PlayerCriticalMetallicGrainBank);
            ReleaseVaultBuffer(vault, ref _granularVoiceActiveHandle, BufferID.PlayerCriticalGranularVoiceActive);
            ReleaseVaultBuffer(vault, ref _granularVoiceElapsedHandle, BufferID.PlayerCriticalGranularVoiceElapsed);
            ReleaseVaultBuffer(vault, ref _granularVoiceLengthHandle, BufferID.PlayerCriticalGranularVoiceLength);
            ReleaseVaultBuffer(vault, ref _granularVoiceStartHandle, BufferID.PlayerCriticalGranularVoiceStart);
            ReleaseVaultBuffer(vault, ref _granularVoiceSeedHandle, BufferID.PlayerCriticalGranularVoiceSeed);
            ReleaseVaultBuffer(vault, ref _granularVoiceCursorHandle, BufferID.PlayerCriticalGranularVoiceCursor);
            ReleaseVaultBuffer(vault, ref _granularVoicePlaybackRateHandle, BufferID.PlayerCriticalGranularVoicePlaybackRate);
            ReleaseVaultBuffer(vault, ref _granularVoiceGainHandle, BufferID.PlayerCriticalGranularVoiceGain);
            ReleaseVaultBuffer(vault, ref _granularTelemetryRingHandle, BufferID.PlayerCriticalGranularTelemetryRing);
            ReleaseVaultBuffer(vault, ref _prologueTransitionTelemetryRingHandle, BufferID.PlayerCriticalPrologueTransitionTelemetryRing);
            ReleaseVaultBuffer(vault, ref _prologueTransitionRingHandle, PlayerCriticalPrologueTransitionRingBufferId);
            ReleaseVaultBuffer(vault, ref _audioSynthesisTelemetryRingHandle, PlayerCriticalAudioSynthesisTelemetryRingBufferId);
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
                    Hecton8.Core.H8Debug.LogError(
                        "[PlayerCriticalProceduralAudioRenderer] Native HectonAudioKernel descriptor rejected before registration.",
                        this);
                }
#endif
                return;
            }

            bool registered = HectonSensoryKernelNativeBridge.TryRegisterWithRetryGate(ref descriptor, out NativeAudioKernelBridgeStatus bridgeStatus);
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
                Hecton8.Core.H8Debug.LogError(
                    "[PlayerCriticalProceduralAudioRenderer] Native HectonAudioKernel bridge unavailable. Procedural master-bus output is not registered.",
                    this);
            }
#endif
            _sampleRingBuffer.RecordBridgeFailure(bridgeStatus);
        }

        private void ClearNativeOutputBridge()
        {
            if (!_nativeOutputRegistered)
                return;

            if (HectonSensoryKernelNativeBridge.TryClear(out NativeAudioKernelBridgeStatus clearStatus))
            {
                _nativeOutputRegistered = false;
                return;
            }

            _sampleRingBuffer?.RecordBridgeFailure(clearStatus);
            if ((clearStatus & NativeAudioKernelBridgeStatus.PluginUnavailable) != 0)
                _nativeOutputRegistered = false;
        }

        private void ClearLowPassState()
        {
            if (TryAcquireBinauralFilterViews(out BinauralFilterVaultViews filterViews))
            {
                try
                {
                    ClearScratchBuffer(filterViews.LowPassInputHistory1, filterViews.LowPassInputHistory1.Length);
                    ClearScratchBuffer(filterViews.LowPassInputHistory2, filterViews.LowPassInputHistory2.Length);
                    ClearScratchBuffer(filterViews.LowPassOutputHistory1, filterViews.LowPassOutputHistory1.Length);
                    ClearScratchBuffer(filterViews.LowPassOutputHistory2, filterViews.LowPassOutputHistory2.Length);
                    ClearScratchBuffer(filterViews.BinauralDelayRing, filterViews.BinauralDelayRing.Length);
                    ClearScratchBuffer(filterViews.BinauralShadowHistory, filterViews.BinauralShadowHistory.Length);
                }
                finally
                {
                    ReleaseBinauralFilterMutationGuard(ref filterViews);
                }
            }

            if (TryAcquireReverbViews(out ReverbVaultViews reverbViews))
            {
                try
                {
                    ClearScratchBufferCold(reverbViews.InteriorFdnDelay, reverbViews.InteriorFdnDelay.Length);
                }
                finally
                {
                    ReleaseReverbMutationGuard(ref reverbViews);
                }
            }
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
            _lastDirectSonarPingFrame = -4096;
            _lastDirectSonarPingIntensity = 0f;
            _lastDirectSonarPingOrigin = Vector3.zero;
            Interlocked.Exchange(ref _impactEventReadIndex, 0);
            Interlocked.Exchange(ref _impactEventWriteIndex, 0);
            Interlocked.Exchange(ref _impactEventQueueDropCount, 0);
            _hullSynthesisState = default;
            _ambientCurrentSynthesisState = default;
            _impactEchoSynthesisState = default;
            _thrusterSynthesisState = default;
            _sabineReverbSynthesisState = default;
            _caveConvolutionReverbSynthesisState = default;
            _interiorFdnReverbSynthesisState = default;
            _tinnitusSynthesisState = default;
            _leviathanGranularSynthesisState = default;
            _criticalSidechainCompressorState = new CriticalSidechainCompressorState { Gain = 1f };
            _audioImpactStressValue = 0f;
            _audioImpactMetallicValue = 0f;
            _audioPeakImpactEnergyJoules = 0f;
            _audioTinnitusOxygenStressValue = 0f;
            _audioEardrumRuptureTinnitusValue = 0f;
            _targetEardrumRuptureTinnitusValue = 0f;
            _audioLeviathanRoarAggroValue = 0f;
            _audioHullStressValue = 0f;
            _audioStructuralHullStressValue = 0f;
            _audioStructuralHullStressVelocityValue = 0f;
            if (TryAcquireSonarDspViews(out SonarDspVaultViews sonarDspViews))
            {
                try
                {
                    for (int i = 0; i < sonarDspViews.ReadCursors.Length; i++)
                        sonarDspViews.ReadCursors[i] = -1f;
                    ClearScratchBuffer(sonarDspViews.FilterInput1, sonarDspViews.FilterInput1.Length);
                    ClearScratchBuffer(sonarDspViews.FilterInput2, sonarDspViews.FilterInput2.Length);
                    ClearScratchBuffer(sonarDspViews.FilterOutput1, sonarDspViews.FilterOutput1.Length);
                    ClearScratchBuffer(sonarDspViews.FilterOutput2, sonarDspViews.FilterOutput2.Length);
                }
                finally
                {
                    ReleaseSonarDspMutationGuard(ref sonarDspViews);
                }
            }
            _audioHullPressureDepthValue = 0f;
            _audioAbsoluteDepthMeters = 0f;
            _pendingImpactEchoProbe = default;
            _hullStressTickValue = 0f;
            _structuralHullStressTickValue = 0f;
            _structuralHullStressVelocityTickValue = 0f;
            _structuralPressureImpulseTickValue = 0f;
            _absoluteDepthTickValue = 0f;
            _targetAbsoluteDepthMeters = 0f;
            ResetReverbModelState();
            ResetSonarPhaseState(0);
            if (TryAcquireSonarDspViews(out sonarDspViews))
            {
                try
                {
                    ClearScratchBufferCold(sonarDspViews.EchoDelay, sonarDspViews.EchoDelay.Length);
                }
                finally
                {
                    ReleaseSonarDspMutationGuard(ref sonarDspViews);
                }
            }
            if (TryAcquireFrameScratchViews(1, out FrameScratchVaultViews frameViews))
            {
                try
                {
                    ClearScratchBufferCold(frameViews.ImpactEchoScratch, frameViews.ImpactEchoScratch.Length);
                }
                finally
                {
                    ReleaseFrameScratchMutationGuard(ref frameViews);
                }
            }
            if (TryAcquireTransientDelayViews(out TransientDelayVaultViews transientViews))
            {
                try
                {
                    ClearScratchBufferCold(transientViews.ImpactClangDelay, transientViews.ImpactClangDelay.Length);
                    ClearScratchBufferCold(transientViews.ThrusterCombDelay, transientViews.ThrusterCombDelay.Length);
                }
                finally
                {
                    ReleaseTransientDelayMutationGuard(ref transientViews);
                }
            }

            if (TryAcquireReverbViews(out ReverbVaultViews resetReverbViews))
            {
                try
                {
                    ClearScratchBufferCold(resetReverbViews.SabineReverbDelay, resetReverbViews.SabineReverbDelay.Length);
                    ClearScratchBuffer(resetReverbViews.CaveConvolutionDelay, resetReverbViews.CaveConvolutionDelay.Length);
                }
                finally
                {
                    ReleaseReverbMutationGuard(ref resetReverbViews);
                }
            }

            _sabineReverbSynthesisState = default;
            _caveConvolutionReverbSynthesisState = default;
        }

        private float ResolveAbyssalLowPassTarget(float depthMeters)
        {
            return math.saturate(
                (math.max(0f, depthMeters) - AbyssalLowPassStartDepthMeters) *
                AbyssalLowPassFadeDepthMetersInv);
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

            if (!TryResolvePlayerPoseRuntimePosition(out Vector3 playerRuntimePosition))
                return false;

            IPlayerRuntimeContext playerContext = ResolvePlayerRuntimeContext();
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
                Valid = 1,
                Excitation = echoExcitation,
                ExpireAt = ResolvePresentationClockSeconds() + ImpactEchoMaximumLifetimeSeconds
            };
        }

        private void TryResolvePendingImpactEchoProbe()
        {
            if (_pendingImpactEchoProbe.Valid == 0)
                return;

            if (ResolvePresentationClockSeconds() > _pendingImpactEchoProbe.ExpireAt)
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
                forwardEcho.HasHit == 0 ||
                forwardEcho.HitDistanceMeters <= ForwardEchoMinimumDistanceMeters)
            {
                return false;
            }

            float distanceMeters = math.min(forwardEcho.HitDistanceMeters, SonarEchoMaximumDistanceMeters);
            echoDelaySeconds = math.min(distanceMeters * SoundSpeedWaterMetersPerSecondInv, SonarEchoMaximumDelaySeconds);
            echoAttenuation = math.clamp(
                echoExcitation *
                (SonarEchoReferenceDistanceMeters * math.rcp(SonarEchoReferenceDistanceMeters + distanceMeters)) *
                forwardEcho.Transmission01,
                0f,
                0.92f);
            echoLowPassCutoffHz = forwardEcho.LowPassCutoffHz;
            return echoAttenuation > 0.0001f;
        }

        private static float ResolvePresentationClockSeconds()
        {
            return (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
        }

        private void UpdateAcousticThreatPulse()
        {
            if (_boundPlayerTransform == null)
                return;

            HectonMapMagicVegetationBridge vegetationBridge = null;
            if (!WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref vegetationBridge))
                return;

            float lfeThreat01 = math.saturate(math.max(
                _targetStructuralHullStressValue * _targetHullPressureDepthValue,
                _impactStressImpulseTickValue * 0.65f));
            if (lfeThreat01 < HullLfeThreatMinimum01)
                return;

            float radius = math.lerp(36f, HullLfeThreatRadiusMeters, lfeThreat01);
            float strength = math.lerp(0.25f, HullLfeThreatStrength, lfeThreat01);
            if (!TryResolvePlayerPoseRuntimePosition(out Vector3 playerRuntimePosition))
                return;

            vegetationBridge.ApplyExternalThreatPulse(
                playerRuntimePosition,
                radius,
                strength,
                HullLfeThreatHoldSeconds);
        }

        private void RenderHeartbeatBlock(
            int frameCount,
            double invSampleRate,
            ref FrameScratchVaultViews frameViews,
            bool heartbeatActiveTarget,
            float heartbeatStressTarget,
            float heartbeatOxygenDangerTarget)
        {
            NativeArray<float> heartbeatScratch = frameViews.HeartbeatScratch;
            NativeArray<float> heartbeatDuckScratch = frameViews.HeartbeatDuckScratch;
            if (!heartbeatScratch.IsCreated || !heartbeatDuckScratch.IsCreated)
                return;

            HeartbeatSynthesisState state = _heartbeatSynthesisState;
            float stressStart = _audioHeartbeatStressValue;
            float oxygenDangerStart = _audioHeartbeatOxygenDangerValue;
            if (!heartbeatActiveTarget)
            {
                ClearScratchBuffer(heartbeatScratch, frameCount);
                FillScratchBuffer(heartbeatDuckScratch, frameCount, 1f);
                _heartbeatSynthesisState = default;
                _audioHeartbeatStressValue = 0f;
                _audioHeartbeatOxygenDangerValue = 0f;
                return;
            }

            float frameTScale = frameCount > 1 ? math.rcp((float)(frameCount - 1)) : 0f;
            float invSampleRateF = (float)invSampleRate;
            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                float frameT = frameCount > 1 ? frameIndex * frameTScale : 0f;
                float stress = heartbeatActiveTarget ? math.lerp(stressStart, heartbeatStressTarget, frameT) : 0f;
                float oxygenDanger = heartbeatActiveTarget ? math.lerp(oxygenDangerStart, heartbeatOxygenDangerTarget, frameT) : 0f;
                float heartbeatDrive = math.saturate(math.max(stress, oxygenDanger));
                float bpm = math.lerp(HeartbeatBaseBpm, HeartbeatStressBpm, heartbeatDrive);
                float beatIntervalSeconds = 60f * math.rcp(math.max(HeartbeatBaseBpm, bpm));
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
                heartbeatScratch[frameIndex] = 0f;

                float deltaSeconds = invSampleRateF;
                float duckDepth = HeartbeatDuckMaximum * math.lerp(0.18f, 1f, heartbeatDrive);
                float duckTarget = combinedEnvelope * duckDepth;
                float duckSharpness = duckTarget > state.DuckEnvelope
                    ? HeartbeatDuckAttackSharpness
                    : HeartbeatDuckReleaseSharpness;
                float duckBlend = ApproximateOneMinusExpNegPositive(duckSharpness * deltaSeconds);
                state.DuckEnvelope = math.lerp(state.DuckEnvelope, duckTarget, duckBlend);
                heartbeatDuckScratch[frameIndex] = math.clamp(1f - state.DuckEnvelope, 0.35f, 1f);

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
            ref FrameScratchVaultViews frameViews,
            float bubbleBoilTarget,
            float absoluteDepthMeters)
        {
            _ = invSampleRate;
            NativeArray<float> bubbleScratch = frameViews.BubbleScratch;
            if (!bubbleScratch.IsCreated)
                return;

            int safeCount = math.min(frameCount, bubbleScratch.Length);
            if (safeCount <= 0)
                return;

            float startIntensity = math.saturate(_audioBubbleBoilIntensity);
            float endIntensity = math.saturate(bubbleBoilTarget);
            if (startIntensity <= HullNoiseFloor && endIntensity <= HullNoiseFloor)
            {
                ClearScratchBuffer(bubbleScratch, frameCount);
                _audioBubbleBoilIntensity = 0f;
                return;
            }

            float depthDrive = math.lerp(
                0.78f,
                1.18f,
                ResolveAscendingNormalized01(math.max(0f, absoluteDepthMeters), 50f, 750f));
            float frameTScale = safeCount > 1 ? math.rcp((float)(safeCount - 1)) : 0f;
            for (int frameIndex = 0; frameIndex < safeCount; frameIndex++)
            {
                float frameT = safeCount > 1 ? frameIndex * frameTScale : 1f;
                float intensity = math.lerp(startIntensity, endIntensity, frameT);
                uint sampleIndex = (uint)math.max(0L, blockStartFrame + frameIndex);
                uint burstIndex = sampleIndex >> ToolCavitationBurstShift;
                float burstDensity = math.lerp(
                    ToolCavitationBurstDensityMinimum,
                    ToolCavitationBurstDensityMaximum,
                    intensity);
                float burstDensityInv = math.rcp(math.max(burstDensity, 0.0001f));
                float burstThreshold = 1f - burstDensity;
                float burstHash = Hash01(burstIndex ^ 0xB0E1C9A5u);
                float burstGate = math.saturate((burstHash - burstThreshold) * burstDensityInv);
                float burstOffset = sampleIndex & ToolCavitationBurstMask;
                float burstEnvelope = math.saturate(burstOffset * 0.125f) * ApproximateExpNegPositive(0.085f * burstOffset);
                float white = XorShiftSigned(sampleIndex, 0x7E5A3C91u);
                float high = HighBandNoise(sampleIndex ^ 0xA91F37D5u);
                float shapedNoise = (white * 0.34f) + (high * 0.66f);
                float heatEnvelope = intensity * intensity;
                bubbleScratch[frameIndex] =
                    FastSoftClip(shapedNoise * 2.4f) *
                    burstGate *
                    burstEnvelope *
                    heatEnvelope *
                    depthDrive *
                    ToolCavitationMaximumGain;
            }

            _audioBubbleBoilIntensity = endIntensity;
        }

        private void MixAndFilterBlock(
            int frameCount,
            long blockStartFrame,
            double invSampleRate,
            AudioParameterSnapshot parameters,
            NativeArray<float> metallicGrainBank,
            ref FrameScratchVaultViews frameViews,
            ref BinauralFilterVaultViews filterViews,
            ref ReverbVaultViews reverbViews)
        {
            if (!HasFrameScratchBuffers(ref frameViews, frameCount) ||
                !HasBinauralFilterBuffers(ref filterViews) ||
                !HasReverbBuffers(ref reverbViews))
                return;

            NativeArray<float> hullScratch = frameViews.HullScratch;
            NativeArray<float> sonarScratch = frameViews.SonarScratch;
            NativeArray<float> impactEchoScratch = frameViews.ImpactEchoScratch;
            NativeArray<float> thrusterScratch = frameViews.ThrusterScratch;
            NativeArray<float> heartbeatScratch = frameViews.HeartbeatScratch;
            NativeArray<float> heartbeatDuckScratch = frameViews.HeartbeatDuckScratch;
            NativeArray<float> bubbleScratch = frameViews.BubbleScratch;
            NativeArray<float> mixScratch = frameViews.MixScratch;
            NativeArray<float> sabineReverbDelay = reverbViews.SabineReverbDelay;
            NativeArray<float> caveConvolutionImpulse = reverbViews.CaveConvolutionImpulse;
            NativeArray<float> caveConvolutionDelay = reverbViews.CaveConvolutionDelay;
            NativeArray<float> interiorFdnDelay = reverbViews.InteriorFdnDelay;
            NativeArray<float> lowPassInputHistory1 = filterViews.LowPassInputHistory1;
            NativeArray<float> lowPassInputHistory2 = filterViews.LowPassInputHistory2;
            NativeArray<float> lowPassOutputHistory1 = filterViews.LowPassOutputHistory1;
            NativeArray<float> lowPassOutputHistory2 = filterViews.LowPassOutputHistory2;
            float targetMix = math.saturate(parameters.AbyssalLowPassMix);
            float startMix = _audioAbyssalLowPassMix;
            float endMix = targetMix;
            float startAbsoluteDepthMeters = math.max(0f, _audioAbsoluteDepthMeters);
            float endAbsoluteDepthMeters = math.max(0f, parameters.AbsoluteDepthMeters);
            float startPressureCutoff = ResolvePressureHighFrequencyCutoff(startAbsoluteDepthMeters);
            float endPressureCutoff = ResolvePressureHighFrequencyCutoff(endAbsoluteDepthMeters);
            float startTinnitusStress = _audioTinnitusOxygenStressValue;
            float endTinnitusStress = math.saturate(parameters.TinnitusOxygenStress);
            float startEardrumRupture = _audioEardrumRuptureTinnitusValue;
            float endEardrumRupture = math.saturate(parameters.EardrumRuptureTinnitus);
            float startLeviathanAggro = _audioLeviathanRoarAggroValue;
            float endLeviathanAggro = math.saturate(parameters.LeviathanRoarAggro);
            float startLeviathanPitchScale = math.max(0.05f, _audioLeviathanRoarPitchScale);
            float endLeviathanPitchScale = parameters.LeviathanRoarPitchScale > 0f
                ? math.clamp(
                    parameters.LeviathanRoarPitchScale,
                    LeviathanDopplerMinimumPitchScale,
                    LeviathanDopplerMaximumPitchScale)
                : 1f;
            float startPrologueLowPassCutoff = math.clamp(
                _audioPrologueLowPassCutoffHertz,
                AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                PrologueOpenLowPassHertz);
            float endPrologueLowPassCutoff = math.clamp(
                parameters.PrologueLowPassCutoffHz > 0f ? parameters.PrologueLowPassCutoffHz : PrologueOpenLowPassHertz,
                AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                PrologueOpenLowPassHertz);
            float startPrologueLfeGain = math.saturate(_audioPrologueLfeGain);
            float endPrologueLfeGain = math.saturate(parameters.PrologueLfeGain);
            float startPrologueGranularStress = math.saturate(_audioPrologueGranularStress);
            bool prologuePlasmaActive =
                parameters.PrologueStage == AudioTransitionState.StagePlasma ||
                parameters.PrologueStage == AudioTransitionState.StageWhiteout;
            float endPrologueGranularStress = prologuePlasmaActive
                ? math.saturate(FiniteOrZero(parameters.PrologueGranularStress))
                : 0f;
            float startProloguePortalBlend = math.saturate(_audioProloguePortalBlend01);
            float endProloguePortalBlend = math.saturate(parameters.ProloguePortalBlend01);
            uint targetSplashdownSequence = parameters.PrologueSplashdownSequence;
            if (targetSplashdownSequence != 0u && targetSplashdownSequence != _audioPrologueSplashdownSequence)
            {
                _audioPrologueSplashdownSequence = targetSplashdownSequence;
                _prologueSplashdownTotalSamples = math.max(1, (int)(PrologueSplashdownDurationSeconds * math.max(1, _sampleRate) + 0.5f));
                _prologueSplashdownRemainingSamples = _prologueSplashdownTotalSamples;
                _prologueSplashdownGain = math.saturate(parameters.PrologueSplashdownGain);
                _prologueSplashdownPhase = 0d;
            }

            AmbientCurrentSynthesisState ambientState = _ambientCurrentSynthesisState;
            float ambientDepthDrive = math.saturate(math.max(math.max(parameters.HullPressureDepth, parameters.AbyssalLowPassMix), endProloguePortalBlend));
            float panicAmbientDull = math.saturate(parameters.HeartbeatStress);
            float structuralSidechainDrive = math.saturate(math.max(
                math.max(parameters.StructuralHullStress, parameters.StructuralSnap),
                math.max(parameters.PrologueGranularStress, parameters.PrologueSplashdownGain) * 0.85f));
            CriticalSidechainCompressorState sidechainState = _criticalSidechainCompressorState;
            if (sidechainState.Gain <= HullNoiseFloor)
                sidechainState.Gain = 1f;
            bool sabineReverbActive = false;
            float sabineWetGain = 0f;
            float sabineDampingAlpha = 0f;
            int sabineDelayA = 1;
            int sabineDelayB = 1;
            int sabineDelayC = 1;
            int sabineDelayD = 1;
            float sabineFeedbackA = 0f;
            float sabineFeedbackB = 0f;
            float sabineFeedbackC = 0f;
            float sabineFeedbackD = 0f;
            bool caveConvolutionActive = false;
            float caveConvolutionWetGain = 0f;
            float caveConvolutionDampingAlpha = 0f;
            float caveConvolutionDensity01 = 0f;
            if (parameters.ReverbDspTier == (int)ReverbDspTier.NativeSabine)
            {
                ResolveSabineReverbBlock(
                    sabineReverbDelay,
                    parameters.ReverbRt60Seconds,
                    parameters.ReverbWetMix,
                    parameters.ReverbOpenness,
                    out sabineReverbActive,
                    out sabineWetGain,
                    out sabineDampingAlpha,
                    out sabineDelayA,
                    out sabineDelayB,
                    out sabineDelayC,
                    out sabineDelayD,
                    out sabineFeedbackA,
                    out sabineFeedbackB,
                    out sabineFeedbackC,
                    out sabineFeedbackD);
            }
            else if (parameters.ReverbDspTier == (int)ReverbDspTier.NativeConvolution)
            {
                ResolveCaveConvolutionReverbBlock(
                    caveConvolutionImpulse,
                    caveConvolutionDelay,
                    parameters.ReverbRt60Seconds,
                    parameters.ReverbWetMix,
                    parameters.ReverbOpenness,
                    parameters.ReverbAcousticDensity01,
                    out caveConvolutionActive,
                    out caveConvolutionWetGain,
                    out caveConvolutionDampingAlpha,
                    out caveConvolutionDensity01);
            }

            SabineReverbSynthesisState sabineState = _sabineReverbSynthesisState;
            CaveConvolutionReverbSynthesisState caveConvolutionState = _caveConvolutionReverbSynthesisState;
            InteriorFdnReverbSynthesisState interiorFdnState = _interiorFdnReverbSynthesisState;
            TinnitusSynthesisState tinnitusState = _tinnitusSynthesisState;
            LeviathanGranularSynthesisState leviathanState = _leviathanGranularSynthesisState;
            float enclosureDensityTarget = math.saturate(parameters.EnclosureDensityIndex);
            bool nativeReverbActive = parameters.ReverbDspTier != (int)ReverbDspTier.UnityProfileOnly;
            float sabineFdnSend = parameters.ReverbDspTier == (int)ReverbDspTier.NativeSabine
                ? math.saturate(parameters.ReverbWetMix * enclosureDensityTarget)
                : 0f;
            float interiorFdnSend = nativeReverbActive
                ? math.saturate(math.max(
                    (1f - math.saturate(parameters.BinauralWaterDensityMul)) * enclosureDensityTarget,
                    sabineFdnSend))
                : 0f;
            if (nativeReverbActive)
                interiorFdnSend = math.saturate(math.max(interiorFdnSend, endProloguePortalBlend * ProloguePortalFdnSend));
            float frameTScale = frameCount > 1 ? math.rcp((float)(frameCount - 1)) : 0f;
            float invSampleRateF = (float)invSampleRate;
            float sampleRateF = math.max(1f, _sampleRate);
            leviathanState.SampleRate = sampleRateF;
            float sidechainAttackBlend = ResolveOnePoleTimeBlend(CriticalSidechainAttackSeconds, invSampleRate);
            float sidechainReleaseBlend = ResolveOnePoleTimeBlend(CriticalSidechainReleaseSeconds, invSampleRate);
            float openCutoff = _sampleRate * 0.45f;
            float muffledRangeInv = math.rcp(math.max(openCutoff - AcousticOcclusionUtility.MinimumLowPassCutoffHertz, 0.001f));
            float leviathanLfeAlpha = ResolveOnePoleLowPassCoefficient(LeviathanLfeBypassCutoffHertz, _sampleRate);
            ProloguePlasmaSynthesisState prologuePlasmaState = _prologuePlasmaSynthesisState;
            float blockProloguePlasmaDrive = math.saturate(math.max(startPrologueGranularStress, endPrologueGranularStress));
            float prologuePlasmaQuality = SmoothQuality01(parameters.GlobalQualityWeight);
            float plasmaBpB0 = 0f;
            float plasmaBpB1 = 0f;
            float plasmaBpB2 = 0f;
            float plasmaBpA1 = 0f;
            float plasmaBpA2 = 0f;
            if (blockProloguePlasmaDrive > HullNoiseFloor)
            {
                float plasmaBandPassCenter = math.lerp(
                    ProloguePlasmaBandPassMinimumHertz,
                    ProloguePlasmaBandPassMaximumHertz,
                    blockProloguePlasmaDrive);
                ComputeBandPassCoefficients(
                    plasmaBandPassCenter,
                    ProloguePlasmaBandPassQ,
                    _sampleRate,
                    out plasmaBpB0,
                    out plasmaBpB1,
                    out plasmaBpB2,
                    out plasmaBpA1,
                    out plasmaBpA2);
            }

            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                long sampleFrame = blockStartFrame + frameIndex;
                float frameT = frameCount > 1 ? frameIndex * frameTScale : 1f;
                float tinnitusStress = math.lerp(startTinnitusStress, endTinnitusStress, frameT);
                float eardrumRupture = math.lerp(startEardrumRupture, endEardrumRupture, frameT);
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
                    metallicGrainBank,
                    sampleFrame,
                    leviathanAggro,
                    leviathanPitchScale,
                    invSampleRateF);
                float leviathanLfe = ApplyOnePoleLowPass(
                    leviathanRoar,
                    leviathanState.LfeBypassState + BiquadDenormalBias,
                    leviathanLfeAlpha);
                leviathanState.LfeBypassState = leviathanLfe;
                float leviathanLfeBypass = leviathanLfe * LeviathanLfeBypassGain * math.saturate(leviathanAggro);
                float criticalSidechain = math.max(math.abs(hullScratch[frameIndex]), math.abs(impactEchoScratch[frameIndex]));
                criticalSidechain = math.max(criticalSidechain, math.abs(sonarScratch[frameIndex]) * 0.45f);
                criticalSidechain = math.max(criticalSidechain, structuralSidechainDrive);
                float envelopeBlend = criticalSidechain > sidechainState.Envelope
                    ? sidechainAttackBlend
                    : sidechainReleaseBlend;
                sidechainState.Envelope = math.lerp(sidechainState.Envelope, criticalSidechain, envelopeBlend);
                float duckGainTarget = ResolveCriticalSidechainDuckingGain(sidechainState.Envelope);
                float gainBlend = duckGainTarget < sidechainState.Gain
                    ? sidechainAttackBlend
                    : sidechainReleaseBlend;
                sidechainState.Gain = math.lerp(sidechainState.Gain, duckGainTarget, gainBlend);
                float duckedAmbientCurrent = ambientCurrent * sidechainState.Gain;
                float proceduralDry =
                    (hullScratch[frameIndex] +
                     sonarScratch[frameIndex] +
                     impactEchoScratch[frameIndex] +
                     thrusterScratch[frameIndex] +
                     duckedAmbientCurrent +
                     bubbleScratch[frameIndex] +
                     leviathanRoar) * heartbeatDuckScratch[frameIndex];
                float mixedDry = proceduralDry;
                float tinnitus =
                    RenderTinnitusSample(ref tinnitusState, tinnitusStress, panicAmbientDull, invSampleRate) +
                    RenderEardrumRuptureTinnitusSample(ref tinnitusState, eardrumRupture, invSampleRate);
                float mixed = (mixedDry + heartbeatScratch[frameIndex] + tinnitus) * outputHeadroom;
                mixed = ApplyPanicGranularMasterJitter(
                    mixed,
                    (uint)math.max(0L, sampleFrame),
                    panicAmbientDull);
                float prologueLfeGain = math.lerp(startPrologueLfeGain, endPrologueLfeGain, frameT);
                float prologueLfe = prologueLfeGain > HullNoiseFloor
                    ? AdvanceSine(ref _prologueLfePhase, PrologueLfeHertz, invSampleRate) * prologueLfeGain * PrologueLfeOutputGain
                    : 0f;
                float prologuePlasmaDrive = math.saturate(math.lerp(startPrologueGranularStress, endPrologueGranularStress, frameT));
                float prologuePlasma = RenderProloguePlasmaSample(
                    ref prologuePlasmaState,
                    (uint)math.max(0L, sampleFrame),
                    prologuePlasmaDrive,
                    prologuePlasmaQuality,
                    invSampleRate,
                    plasmaBpB0,
                    plasmaBpB1,
                    plasmaBpB2,
                    plasmaBpA1,
                    plasmaBpA2);
                float prologueSplashdown = RenderPrologueSplashdownSample(invSampleRate);
                mixed = FastSoftClip(mixed + prologueLfe + prologuePlasma + prologueSplashdown);

                if (sabineReverbActive)
                {
                    sabineState.WetMix = math.lerp(sabineState.WetMix, sabineWetGain, SabineReverbWetMixLerpCoefficient);
                    mixed = RenderSabineReverbSample(
                        sabineReverbDelay,
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
                else if (caveConvolutionActive)
                {
                    caveConvolutionState.WetMix = math.lerp(
                        caveConvolutionState.WetMix,
                        caveConvolutionWetGain,
                        CaveConvolutionWetMixLerpCoefficient);
                    mixed = RenderCaveConvolutionReverbSample(
                        caveConvolutionImpulse,
                        caveConvolutionDelay,
                        ref caveConvolutionState,
                        mixed,
                        caveConvolutionState.WetMix,
                        caveConvolutionDampingAlpha,
                        caveConvolutionDensity01);
                }

                if (interiorFdnSend > 0.0001f && interiorFdnDelay.IsCreated)
                    mixed = RenderInteriorFdnReverbSample(interiorFdnDelay, ref interiorFdnState, mixed, interiorFdnSend);

                float mix = math.lerp(startMix, endMix, frameT);
                float abyssalCutoff = math.lerp(_sampleRate * 0.45f, AbyssalLowPassCutoffHertz, mix);
                float pressureCutoff = math.lerp(startPressureCutoff, endPressureCutoff, frameT);
                float tinnitusCutoff = math.lerp(_sampleRate * 0.45f, TinnitusLowPassCutoffHertz, tinnitusStress);
                float prologueCutoff = math.lerp(startPrologueLowPassCutoff, endPrologueLowPassCutoff, frameT);
                float cutoff = math.min(math.min(math.min(abyssalCutoff, pressureCutoff), tinnitusCutoff), prologueCutoff);
                float muffled01 = math.saturate((openCutoff - cutoff) * muffledRangeInv);
                float targetAlpha = ResolveOnePoleLowPassCoefficient(cutoff, _sampleRate);
                float alpha = math.lerp(1f, targetAlpha, muffled01);
                float firstPole = ApplyOnePoleLowPass(mixed, lowPassOutputHistory1[0] + BiquadDenormalBias, alpha);
                float secondPole = ApplyOnePoleLowPass(firstPole, lowPassOutputHistory2[0] + BiquadDenormalBias, alpha);

                lowPassInputHistory1[0] = mixed;
                lowPassInputHistory2[0] = firstPole;
                lowPassOutputHistory1[0] = firstPole;
                lowPassOutputHistory2[0] = secondPole;
                mixed = math.lerp(mixed, secondPole, muffled01);
                mixed += leviathanLfeBypass;

                mixScratch[frameIndex] = ApplyMasterSafetyLimiter(mixed);
            }

            _ambientCurrentSynthesisState = ambientState;
            _sabineReverbSynthesisState = sabineState;
            _caveConvolutionReverbSynthesisState = caveConvolutionState;
            _interiorFdnReverbSynthesisState = interiorFdnState;
            _tinnitusSynthesisState = tinnitusState;
            _leviathanGranularSynthesisState = leviathanState;
            _prologuePlasmaSynthesisState = prologuePlasmaState;
            _criticalSidechainCompressorState = sidechainState;
            _audioAbyssalLowPassMix = endMix;
            _audioAbsoluteDepthMeters = endAbsoluteDepthMeters;
            _audioTinnitusOxygenStressValue = endTinnitusStress;
            _audioEardrumRuptureTinnitusValue = endEardrumRupture;
            _audioLeviathanRoarAggroValue = endLeviathanAggro;
            _audioLeviathanRoarPitchScale = endLeviathanPitchScale;
            _audioPrologueLowPassCutoffHertz = endPrologueLowPassCutoff;
            _audioPrologueLfeGain = endPrologueLfeGain;
            _audioPrologueGranularStress = endPrologueGranularStress;
            _audioProloguePortalBlend01 = endProloguePortalBlend;
        }

        private static float RenderProloguePlasmaSample(
            ref ProloguePlasmaSynthesisState state,
            uint sampleIndex,
            float drive01,
            float quality01,
            double invSampleRate,
            float b0,
            float b1,
            float b2,
            float a1,
            float a2)
        {
            float drive = math.saturate(FiniteOrZero(drive01));
            if (drive <= HullNoiseFloor)
                return 0f;

            float whiteNoise = HashSigned(sampleIndex ^ 0xA621F35Bu);
            float pinkNoise = ApplyPaulKelletPink(ref state, whiteNoise);
            if (!math.isfinite(pinkNoise))
            {
                ResetProloguePlasmaState(ref state);
                return 0f;
            }

            float lfoRate = math.lerp(ProloguePlasmaLfoMinimumHertz, ProloguePlasmaLfoMaximumHertz, drive);
            float lfoDepth = math.lerp(
                ProloguePlasmaLfoDepthMinimum,
                ProloguePlasmaLfoDepthMaximum,
                math.saturate(FiniteOrZero(quality01)));
            float lfo = 1f - (lfoDepth * (1f - AdvanceTriangle01(ref state.LfoPhase, lfoRate, invSampleRate)));
            float bandPassed = ProcessBiquad(
                pinkNoise * lfo,
                b0,
                b1,
                b2,
                a1,
                a2,
                ref state.BandPassInput1,
                ref state.BandPassInput2,
                ref state.BandPassOutput1,
                ref state.BandPassOutput2);
            if (!math.isfinite(bandPassed))
            {
                ResetProloguePlasmaState(ref state);
                return 0f;
            }

            float qualityGain = math.lerp(
                ProloguePlasmaMinimumQualityGain,
                1f,
                math.saturate(FiniteOrZero(quality01)));
            float sample = FastSoftClip(bandPassed * math.lerp(1.4f, 2.8f, drive)) * drive * ProloguePlasmaOutputGain * qualityGain;
            return math.isfinite(sample) ? sample : 0f;
        }

        private static void ResetProloguePlasmaState(ref ProloguePlasmaSynthesisState state)
        {
            state.LfoPhase = 0d;
            state.PinkB0 = 0f;
            state.PinkB1 = 0f;
            state.PinkB2 = 0f;
            state.PinkB3 = 0f;
            state.PinkB4 = 0f;
            state.PinkB5 = 0f;
            state.PinkB6 = 0f;
            state.BandPassInput1 = 0f;
            state.BandPassInput2 = 0f;
            state.BandPassOutput1 = 0f;
            state.BandPassOutput2 = 0f;
        }

        private float RenderPrologueSplashdownSample(double invSampleRate)
        {
            if (_prologueSplashdownRemainingSamples <= 0)
                return 0f;

            int totalSamples = math.max(1, _prologueSplashdownTotalSamples);
            int elapsedSamples = math.clamp(totalSamples - _prologueSplashdownRemainingSamples, 0, totalSamples);
            float t = totalSamples > 1
                ? elapsedSamples * math.rcp((float)(totalSamples - 1))
                : 1f;
            float fadeOut = 1f - t;
            float envelope = math.saturate(fadeOut * fadeOut);
            float frequency = math.lerp(PrologueSplashdownSweepStartHertz, PrologueSplashdownSweepEndHertz, t);
            float thud = AdvanceSine(ref _prologueSplashdownPhase, frequency, invSampleRate) *
                         envelope *
                         _prologueSplashdownGain *
                         PrologueSplashdownOutputGain;
            uint sampleIndex = unchecked((uint)elapsedSamples + (_audioPrologueSplashdownSequence * 4099u));
            float cavitationEnvelope = envelope * math.saturate(1f - t * 0.65f);
            float cavitation =
                (LayeredBrownLike(sampleIndex ^ 0x51C0A51Du) * 0.72f +
                 HighBandNoise(sampleIndex ^ 0x7A91D3E5u) * 0.28f) *
                cavitationEnvelope *
                _prologueSplashdownGain *
                PrologueSplashdownCavitationNoiseGain;
            float sample = FastSoftClip(thud + cavitation);
            _prologueSplashdownRemainingSamples--;
            return math.isfinite(sample) ? sample : 0f;
        }

        private bool TryReadGranularTelemetryRing(out NativeArray<GranularAudioTelemetryEntry>.ReadOnly ring)
        {
            ring = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   IsPlayerCriticalVaultHandle(in _granularTelemetryRingHandle, BufferID.PlayerCriticalGranularTelemetryRing) &&
                   vault.TryReadOnlyHandle(in _granularTelemetryRingHandle, out ring) &&
                   ring.IsCreated;
        }

        private bool TryReadPrologueTransitionTelemetryRing(out NativeArray<PrologueAudioTransitionTelemetryEntry>.ReadOnly ring)
        {
            ring = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   IsPlayerCriticalVaultHandle(in _prologueTransitionTelemetryRingHandle, BufferID.PlayerCriticalPrologueTransitionTelemetryRing) &&
                   vault.TryReadOnlyHandle(in _prologueTransitionTelemetryRingHandle, out ring) &&
                   ring.IsCreated;
        }

        private bool TryReadAudioSynthesisTelemetryRing(out NativeArray<AudioSynthesisTelemetryEntry>.ReadOnly ring)
        {
            ring = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   IsPlayerCriticalVaultHandle(in _audioSynthesisTelemetryRingHandle, PlayerCriticalAudioSynthesisTelemetryRingBufferId) &&
                   vault.TryReadOnlyHandle(in _audioSynthesisTelemetryRingHandle, out ring) &&
                   ring.IsCreated;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float RenderEardrumRuptureTinnitusSample(
            ref TinnitusSynthesisState state,
            float rupture01,
            double invSampleRate)
        {
            float rupture = math.saturate(rupture01);
            if (rupture <= 0.0001f)
                return 0f;

            float sine = AdvanceSine(ref state.RupturePhase, EardrumRuptureTinnitusHertz, invSampleRate);
            float shaped = rupture * rupture;
            return sine * shaped * EardrumRuptureMaximumGain;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float ApproximateOneMinusExpNegPositive(float x)
        {
            return math.saturate(1f - ApproximateExpNegPositive(x));
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float ApproximateExpNegPositive(float x)
        {
            float clamped = math.clamp(x, 0f, 8f);
            float x2 = clamped * clamped;
            float x3 = x2 * clamped;
            float numerator = 120f - (60f * clamped) + (12f * x2) - x3;
            float denominator = 120f + (60f * clamped) + (12f * x2) + x3;
            return math.saturate(numerator * math.rcp(math.max(denominator, 0.0001f)));
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float ApproximatePressureScrubberHumDrive01(float pressureDrive)
        {
            float x = math.saturate(pressureDrive);
            float x2 = x * x;
            float numerator = 0.7616f + (1.43f * x) + (0.42f * x2);
            float denominator = 1f + (1.32f * x) + (0.29f * x2);
            return math.saturate(numerator * math.rcp(math.max(denominator, 0.0001f)));
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float ApproximateMagnitudeNoSqrt(float3 value)
        {
            float3 absolute = math.abs(value);
            float max = math.cmax(absolute);
            float min = math.cmin(absolute);
            float mid = absolute.x + absolute.y + absolute.z - max - min;
            return max + (0.375f * mid) + (0.125f * min);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float ApproximateDistanceMetersFromSq(double distanceSq)
        {
            if (double.IsNaN(distanceSq) || double.IsInfinity(distanceSq))
                return float.PositiveInfinity;
            if (distanceSq <= 0d)
                return 0f;

            float clampedSq = (float)math.min(distanceSq, (double)float.MaxValue);
            uint estimateBits = (math.asuint(clampedSq) >> 1) + 0x1FC00000u;
            float estimate = math.asfloat(estimateBits);
            return 0.5f * (estimate + (clampedSq * math.rcp(math.max(estimate, 0.0001f))));
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float ApproximateThrusterEnvelope01(float cycle01, float sharpness)
        {
            float x = math.saturate(cycle01);
            float x2 = x * x;
            float x3 = x2 * x;
            float x5 = x3 * x2;
            float mid = x2 * (3f - (2f * x));
            float broad = x * (2f - x);
            float loadBlend = math.saturate((5f - sharpness) * 0.22222222f);
            return math.lerp(math.lerp(x5, mid, loadBlend), broad, loadBlend * loadBlend);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float RenderLeviathanGranularRoarSample(
            ref LeviathanGranularSynthesisState state,
            NativeArray<float> baseRoarClip,
            long sampleFrame,
            float aggroLevel,
            float pitchScale,
            float invSampleRate)
        {
            float aggro = math.saturate(aggroLevel);
            if (aggro <= 0.0001f || !baseRoarClip.IsCreated || baseRoarClip.Length <= 4)
            {
                state.Envelope = math.max(0f, state.Envelope - (invSampleRate * LeviathanRoarAggroDecayPerSecond));
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
            float sampleRate = math.max(1f, state.SampleRate);
            double cursor = state.GrainStartIndex + (state.GrainAgeSeconds * sampleRate * state.GrainPitchRatio * dopplerPitch);
            float grain = LinearSampleLoopWindow(baseRoarClip, 0, baseRoarClip.Length, cursor);
            float t = math.saturate(state.GrainAgeSeconds * math.rcp(math.max(state.GrainDurationSeconds, 0.0001f)));
            float grainEnvelope = FastSine01(t * 0.5f);
            state.LowPassState = math.lerp(state.LowPassState, grain, math.lerp(0.025f, 0.16f, aggro));
            state.GrainAgeSeconds += invSampleRate;
            state.Envelope = math.max(0f, state.Envelope - (invSampleRate * LeviathanRoarAggroDecayPerSecond * 0.5f));
            float roar = (state.LowPassState * 0.74f + grain * 0.26f) * grainEnvelope * state.Envelope;
            return FastSoftClip(roar * 2.8f) * LeviathanRoarMaximumGain;
        }

        private static float RenderInteriorFdnReverbSample(
            NativeArray<float> interiorFdnDelay,
            ref InteriorFdnReverbSynthesisState state,
            float input,
            float send01)
        {
            if (!interiorFdnDelay.IsCreated)
                return input;

            float send = math.saturate(send01);
            int writeA = state.WriteA & InteriorFdnLaneMask;
            int writeB = state.WriteB & InteriorFdnLaneMask;
            int writeC = state.WriteC & InteriorFdnLaneMask;
            int writeD = state.WriteD & InteriorFdnLaneMask;
            float a = interiorFdnDelay[(0 * InteriorFdnLaneLength) + ((writeA - InteriorFdnPrimeDelayA) & InteriorFdnLaneMask)];
            float b = interiorFdnDelay[(1 * InteriorFdnLaneLength) + ((writeB - InteriorFdnPrimeDelayB) & InteriorFdnLaneMask)];
            float c = interiorFdnDelay[(2 * InteriorFdnLaneLength) + ((writeC - InteriorFdnPrimeDelayC) & InteriorFdnLaneMask)];
            float d = interiorFdnDelay[(3 * InteriorFdnLaneLength) + ((writeD - InteriorFdnPrimeDelayD) & InteriorFdnLaneMask)];

            state.DampingA = math.lerp(state.DampingA, a, 1f - InteriorFdnDamping);
            state.DampingB = math.lerp(state.DampingB, b, 1f - InteriorFdnDamping);
            state.DampingC = math.lerp(state.DampingC, c, 1f - InteriorFdnDamping);
            state.DampingD = math.lerp(state.DampingD, d, 1f - InteriorFdnDamping);

            float fdnInput = input * send;
            interiorFdnDelay[(0 * InteriorFdnLaneLength) + writeA] =
                fdnInput + ((state.DampingB + state.DampingC - state.DampingD) * InteriorFdnFeedback);
            interiorFdnDelay[(1 * InteriorFdnLaneLength) + writeB] =
                fdnInput + ((state.DampingA - state.DampingC + state.DampingD) * InteriorFdnFeedback);
            interiorFdnDelay[(2 * InteriorFdnLaneLength) + writeC] =
                fdnInput + ((-state.DampingA + state.DampingB + state.DampingD) * InteriorFdnFeedback);
            interiorFdnDelay[(3 * InteriorFdnLaneLength) + writeD] =
                fdnInput + ((state.DampingA + state.DampingB - state.DampingC) * InteriorFdnFeedback);

            state.WriteA = (writeA + 1) & InteriorFdnLaneMask;
            state.WriteB = (writeB + 1) & InteriorFdnLaneMask;
            state.WriteC = (writeC + 1) & InteriorFdnLaneMask;
            state.WriteD = (writeD + 1) & InteriorFdnLaneMask;

            float wet = (state.DampingA + state.DampingB + state.DampingC + state.DampingD) * 0.25f;
            return input + (wet * send * InteriorFdnWetGainMaximum);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float ResolveOnePoleTimeBlend(float timeConstantSeconds, double invSampleRate)
        {
            float deltaSeconds = math.max((float)invSampleRate, 0f);
            return ApproximateOneMinusExpNegPositive(deltaSeconds * math.rcp(math.max(timeConstantSeconds, 0.0001f)));
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float ResolveOnePoleLowPassCoefficient(float cutoffHertz, int sampleRate)
        {
            float safeSampleRate = math.max(1f, sampleRate);
            float safeSampleRateInv = math.rcp(safeSampleRate);
            float safeCutoff = math.clamp(cutoffHertz, 20f, safeSampleRate * 0.45f);
            return ApproximateOneMinusExpNegPositive(TwoPi * safeCutoff * safeSampleRateInv);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float ApplyOnePoleLowPass(float input, float previousOutput, float alpha)
        {
            return previousOutput + math.saturate(alpha) * (input - previousOutput);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float ResolveCriticalSidechainDuckingGain(float sidechainEnvelope)
        {
            float overThreshold = math.saturate(
                (math.max(0f, sidechainEnvelope) - CriticalSidechainThreshold) *
                math.rcp(math.max(CriticalSidechainKneeWidth, 0.0001f)));
            float shaped = overThreshold * overThreshold * (3f - 2f * overThreshold);
            return math.lerp(1f, CriticalSidechainDuckedGain, shaped);
        }

        private void ResolveSabineReverbBlock(
            NativeArray<float> sabineReverbDelay,
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
            float safeSampleRateInv = math.rcp(safeSampleRate);
            float safeRt60 = math.clamp(rt60Seconds, 0.05f, 12f);
            float safeWetMix = math.saturate(wetMix01);
            float safeOpenness = math.saturate(openness01);

            active = sabineReverbDelay.IsCreated && safeWetMix > 0.0001f && safeRt60 > 0.05f;
            wetGain = safeWetMix * SabineReverbMaximumWetGain;
            float dampingCutoff = math.lerp(
                SabineReverbDampingClosedCutoffHertz,
                SabineReverbDampingOpenCutoffHertz,
                safeOpenness);
            dampingAlpha = ApproximateExpNegPositive(TwoPi * dampingCutoff * safeSampleRateInv);

            delayA = _sabineDelaySamplesA > 0 ? _sabineDelaySamplesA : ResolveSabineDelaySamples(SabineReverbDelayASeconds, safeSampleRate);
            delayB = _sabineDelaySamplesB > 0 ? _sabineDelaySamplesB : ResolveSabineDelaySamples(SabineReverbDelayBSeconds, safeSampleRate);
            delayC = _sabineDelaySamplesC > 0 ? _sabineDelaySamplesC : ResolveSabineDelaySamples(SabineReverbDelayCSeconds, safeSampleRate);
            delayD = _sabineDelaySamplesD > 0 ? _sabineDelaySamplesD : ResolveSabineDelaySamples(SabineReverbDelayDSeconds, safeSampleRate);

            feedbackA = ResolveSabineFeedback(delayA, safeSampleRate, safeRt60);
            feedbackB = ResolveSabineFeedback(delayB, safeSampleRate, safeRt60);
            feedbackC = ResolveSabineFeedback(delayC, safeSampleRate, safeRt60);
            feedbackD = ResolveSabineFeedback(delayD, safeSampleRate, safeRt60);
        }

        private static int ResolveSabineDelaySamples(float delaySeconds, float sampleRate)
        {
            return math.clamp(
                (int)(delaySeconds * sampleRate + 0.5f),
                1,
                SabineReverbDelayLineLength - 1);
        }

        private static float ResolveSabineFeedback(int delaySamples, float sampleRate, float rt60Seconds)
        {
            float sampleRateInv = math.rcp(math.max(sampleRate, 1f));
            float rt60Inv = math.rcp(math.max(rt60Seconds, 0.05f));
            float delaySeconds = delaySamples * sampleRateInv;
            float decay = (3f * NaturalLogTen * delaySeconds) * rt60Inv;
            float feedback = ApproximateExpNegPositive(decay);
            return math.clamp(feedback, SabineReverbMinimumFeedback, SabineReverbMaximumFeedback);
        }

        private float RenderSabineReverbSample(
            NativeArray<float> sabineReverbDelay,
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
                sabineReverbDelay,
                0,
                ref state.CombAWriteIndex,
                ref state.CombADampingState,
                drySample,
                delayA,
                feedbackA,
                dampingAlpha);
            float combB = ProcessSabineComb(
                sabineReverbDelay,
                SabineReverbDelayLineLength,
                ref state.CombBWriteIndex,
                ref state.CombBDampingState,
                drySample,
                delayB,
                feedbackB,
                dampingAlpha);
            float combC = ProcessSabineComb(
                sabineReverbDelay,
                SabineReverbDelayLineLength * 2,
                ref state.CombCWriteIndex,
                ref state.CombCDampingState,
                drySample,
                delayC,
                feedbackC,
                dampingAlpha);
            float combD = ProcessSabineComb(
                sabineReverbDelay,
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
            NativeArray<float> sabineReverbDelay,
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
            float delayed = sabineReverbDelay[readAddress];
            dampingState = delayed + dampingAlpha * ((dampingState + BiquadDenormalBias) - delayed);
            sabineReverbDelay[writeAddress] = input + dampingState * feedback;
            writeIndex = (clampedWriteIndex + 1) & SabineReverbDelayLineMask;
            return delayed;
        }

        private void ResolveCaveConvolutionReverbBlock(
            NativeArray<float> caveConvolutionImpulse,
            NativeArray<float> caveConvolutionDelay,
            float rt60Seconds,
            float wetMix01,
            float openness01,
            float acousticDensity01,
            out bool active,
            out float wetGain,
            out float dampingAlpha,
            out float density01)
        {
            float safeSampleRate = math.max(_sampleRate, 1f);
            float safeSampleRateInv = math.rcp(safeSampleRate);
            float safeWetMix = math.saturate(wetMix01);
            float safeOpenness = math.saturate(openness01);
            density01 = math.saturate(acousticDensity01);

            active = caveConvolutionImpulse.IsCreated &&
                     caveConvolutionDelay.IsCreated &&
                     safeWetMix > 0.0001f &&
                     rt60Seconds > 0.05f;
            wetGain = safeWetMix * (CaveConvolutionMaximumWetGain + density01 * CaveConvolutionDensityWetBoost);
            float dampingCutoff = math.lerp(
                CaveConvolutionDampingClosedCutoffHertz,
                CaveConvolutionDampingOpenCutoffHertz,
                safeOpenness);
            dampingCutoff *= math.lerp(0.72f, 1f, 1f - density01);
            dampingAlpha = ApproximateExpNegPositive(TwoPi * dampingCutoff * safeSampleRateInv);
        }

        private float RenderCaveConvolutionReverbSample(
            NativeArray<float> caveConvolutionImpulse,
            NativeArray<float> caveConvolutionDelay,
            ref CaveConvolutionReverbSynthesisState state,
            float drySample,
            float wetGain,
            float dampingAlpha,
            float density01)
        {
            if (!caveConvolutionImpulse.IsCreated || !caveConvolutionDelay.IsCreated)
                return drySample;

            int writeIndex = state.WriteIndex & CaveConvolutionDelayMask;
            caveConvolutionDelay[writeIndex] = drySample;

            float wet = 0f;
            int readIndex = writeIndex;
            for (int tapIndex = 0; tapIndex < CaveConvolutionImpulseLength; tapIndex++)
            {
                wet += caveConvolutionDelay[readIndex] * caveConvolutionImpulse[tapIndex];
                readIndex = (readIndex - 1) & CaveConvolutionDelayMask;
            }

            state.DampingState = wet + dampingAlpha * ((state.DampingState + BiquadDenormalBias) - wet);
            state.WriteIndex = (writeIndex + 1) & CaveConvolutionDelayMask;
            float densityGain = math.lerp(0.9f, 1.24f, math.saturate(density01));
            return drySample + state.DampingState * wetGain * densityGain;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float ApplyMasterSafetyLimiter(float sample)
        {
            float magnitude = math.abs(sample);
            if (magnitude <= MasterSafetyLimiterThreshold)
                return sample;

            float sign = math.select(-1f, 1f, sample >= 0f);
            float excess = magnitude - MasterSafetyLimiterThreshold;
            float compressed = MasterSafetyLimiterThreshold + (excess * math.rcp(1f + excess * MasterSafetyLimiterDrive));
            return sign * math.min(1f, compressed);
        }

        private float ResolvePressureHighFrequencyCutoff(float absoluteDepthMeters)
        {
            float pressureDepth = math.max(0f, absoluteDepthMeters);
            float pressureScalar = 1f + (pressureDepth * math.rcp(math.max(PsychoacousticPressureReferenceDepthMeters, 1f)));
            float openCutoff = _sampleRate * 0.45f;
            return math.clamp(
                openCutoff * math.rcp(math.max(pressureScalar, 1f)),
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
                panicAmbientDull01);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float RenderAmbientCurrentFmKernel(
            ref AmbientCurrentSynthesisState state,
            uint sampleIndex,
            float sampleTime,
            double invSampleRate,
            float depthDrive,
            float pressurePhaserDepth01,
            float panicAmbientDull01)
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
                TwoPi * lowPassCutoff * (float)invSampleRate);
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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
            float sampleRateInv = math.rcp(math.max(_sampleRate, 1f));
            float omega = TwoPi * normalizedCutoff * sampleRateInv;
            float cosine = FastCosineRadians(omega);
            float sine = FastSineRadians(omega);
            float alpha = sine * 0.70710678f;
            float inverseA0 = math.rcp(math.max(0.0001f, 1f + alpha));

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

            if (!TryAcquireSonarDspViews(out SonarDspVaultViews sonarDspViews))
                return;

            try
            {
                ClearScratchBuffer(sonarDspViews.EchoDelay, sonarDspViews.EchoDelay.Length);
                for (int i = 0; i < sonarDspViews.ReadCursors.Length; i++)
                    sonarDspViews.ReadCursors[i] = -1f;
                ClearScratchBuffer(sonarDspViews.FilterInput1, sonarDspViews.FilterInput1.Length);
                ClearScratchBuffer(sonarDspViews.FilterInput2, sonarDspViews.FilterInput2.Length);
                ClearScratchBuffer(sonarDspViews.FilterOutput1, sonarDspViews.FilterOutput1.Length);
                ClearScratchBuffer(sonarDspViews.FilterOutput2, sonarDspViews.FilterOutput2.Length);
            }
            finally
            {
                ReleaseSonarDspMutationGuard(ref sonarDspViews);
            }
        }

        private void RebuildAcousticOcclusionLayerMask()
        {
            _resolvedAcousticOcclusionLayerMask = AcousticOcclusionUtility.BuildSensoryMask() & acousticOcclusionLayers.value;
        }

        private static float ResolveHullPressureDepth01(float depthMeters)
        {
            return math.saturate(math.max(0f, depthMeters) * PressureCreakDepthReferenceMetersInv);
        }

        private float ResolvePlayerDepthMeters()
        {
            IPlayerRuntimeContext playerContext = ResolvePlayerRuntimeContext();
            if (playerContext != null &&
                playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                math.isfinite(movementState.DepthMeters))
            {
                return math.max(0f, movementState.DepthMeters);
            }

            if (playerContext != null)
                return 0f;

            HectonPlayerMovement movement = playerMovement;
            return movement != null && math.isfinite(movement.CurrentDepth)
                ? math.max(0f, movement.CurrentDepth)
                : 0f;
        }

        private void PopulateMetallicGrainBank()
        {
            IDataVault guardVault = null;
            if (!TryAcquirePlayerCriticalMutationBuffer(
                    in _metallicGrainBankHandle,
                    BufferID.PlayerCriticalMetallicGrainBank,
                    MetallicGrainBankCapacity,
                    GranularVoiceMutationGuardMask,
                    out NativeArray<float> metallicGrainBank,
                    out guardVault))
            {
                return;
            }

            try
            {
                PlayerCriticalMetallicGrainBank.Generate(metallicGrainBank);
            }
            finally
            {
                ReleasePlayerCriticalMutationGuard(guardVault, GranularVoiceMutationGuardMask);
            }
        }

        private float ResolveAbsoluteDepthMeters()
        {
            if (!TryResolvePlayerPoseAup(out AbsoluteUniversePosition playerAup))
                return 0f;

            double absolutePlayerY = playerAup.ToAbsoluteDouble3().y;
            if (!TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup))
                return 0f;

            double absoluteSurfaceY = (double)ResolveKineticImpactWaterlineY() + originAup.ToAbsoluteDouble3().y;
            return (float)math.max(0d, absoluteSurfaceY - absolutePlayerY);
        }

        private float ResolveAmbientPressureEquivalentDepthMeters()
        {
            HectonSurvivalSystem survivalSystem = _playerSurvivalSystem;
            IPlayerRuntimeContext playerContext = ResolvePlayerRuntimeContext();
            if (playerContext != null &&
                playerContext.IsInitialized &&
                playerContext.SurvivalSystem != null)
            {
                survivalSystem = playerContext.SurvivalSystem;
            }

            if (survivalSystem != null)
            {
                float ambientPressureAtm = math.max(1f, survivalSystem.Pressure);
                return math.max(0f, (ambientPressureAtm - 1f) * 10f);
            }

            return ResolveAbsoluteDepthMeters();
        }

        private void UpdateSurvivalTargets(float deltaTime)
        {
            float oxygenNormalized = _playerSurvivalSystem != null
                ? math.saturate(FiniteOrDefault(_playerSurvivalSystem.OxygenNormalized, 1f))
                : 1f;
            float nitrogenWarningRing = _playerSurvivalSystem != null
                ? math.saturate(FiniteOrZero(_playerSurvivalSystem.NitrogenWarningRinging01))
                : 0f;
            float nitrogenNarcosis = _playerSurvivalSystem != null
                ? math.saturate(FiniteOrZero(_playerSurvivalSystem.NitrogenNarcosis01))
                : 0f;
            float healthStress = _playerHealth != null ? math.saturate(FiniteOrZero(_playerHealth.Stress)) : 0f;
            float healthPanicStress = ResolveAscendingNormalized01(
                healthStress,
                PanicHeartbeatStressThreshold01,
                1f);
            float panicStress = math.max(
                _psychoMetricsStressTickValue,
                math.max(healthPanicStress, _apexHeartbeatThreatActive ? 1f : 0f));
            if (oxygenNormalized > HeartbeatBypassOxygenThreshold &&
                panicStress <= HullNoiseFloor &&
                nitrogenWarningRing <= HullNoiseFloor &&
                nitrogenNarcosis <= HullNoiseFloor)
            {
                _heartbeatStressTickValue = 0f;
                _heartbeatOxygenDangerTickValue = 0f;
                _targetHeartbeatStressValue = 0f;
                _targetHeartbeatOxygenDangerValue = 0f;
                _targetHeartbeatActive = 0;
                _targetTinnitusOxygenStressValue = 0f;
                _targetNarcosisChorusValue = 0f;
                return;
            }

            float oxygenDanger = ResolveDescendingNormalized01(
                oxygenNormalized,
                HeartbeatCriticalOxygenThreshold,
                HeartbeatTerminalOxygenThreshold);
            float pressureStress = _playerSurvivalSystem != null
                ? math.saturate(FiniteOrZero(_playerSurvivalSystem.PressureExposureSeverity01))
                : 0f;
            float thermalStress = _playerSurvivalSystem != null
                ? math.saturate(FiniteOrZero(_playerSurvivalSystem.ThermalStressSeverity01))
                : 0f;
            float underwaterStress = playerMovement != null
                ? math.saturate(FiniteOrZero(playerMovement.CurrentUnderwaterStressIntensity01))
                : 0f;
            float fatalPressure = playerMovement != null ? math.saturate(FiniteOrZero(playerMovement.CurrentFatalPressureSequence01)) : 0f;
            float stressTarget = math.saturate(math.max(
                oxygenDanger,
                math.max(pressureStress, math.max(thermalStress, math.max(underwaterStress, math.max(fatalPressure, panicStress))))));
            float survivalBlendT = ApproximateOneMinusExpNegPositive(6f * math.max(0f, deltaTime));
            _heartbeatStressTickValue = math.saturate(FiniteOrZero(_heartbeatStressTickValue));
            _heartbeatOxygenDangerTickValue = math.saturate(FiniteOrZero(_heartbeatOxygenDangerTickValue));
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
            _targetNarcosisChorusValue = nitrogenNarcosis;
            _targetHeartbeatActive = 1;
        }

        private void UpdatePsychoMetricsHeartbeatCache()
        {
            if (!SignalBus<PlayerStressSignal>.TryGetLatest(out PlayerStressSignal signal, out int sequence) ||
                sequence == _lastPlayerStressSignalSequence)
            {
                return;
            }

            _lastPlayerStressSignalSequence = sequence;
            _psychoMetricsStressTickValue = math.saturate(FiniteOrZero(signal.Stress01));
        }

        private void UpdateApexHeartbeatThreatCache()
        {
            if (playerMovement == null || !playerMovement.IsPlayerSubmerged)
            {
                _apexHeartbeatThreatActive = false;
                return;
            }

            IEcosystemDirectorService ecosystemDirector = ResolveEcosystemDirectorService();
            if (ecosystemDirector == null || !ecosystemDirector.IsInitialized)
            {
                _apexHeartbeatThreatActive = false;
                return;
            }

            if (!TryResolvePlayerPoseRuntimePosition(out Vector3 playerPosition))
            {
                _apexHeartbeatThreatActive = false;
                return;
            }

            _apexHeartbeatThreatActive = ecosystemDirector.IsApexInSector(playerPosition);
        }

        private bool TryResolvePlayerPoseAup(out AbsoluteUniversePosition playerAup)
        {
            playerAup = default;
            IPlayerRuntimeContext playerContext = ResolvePlayerRuntimeContext();
            if (playerContext != null)
            {
                if (playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot pose) &&
                    pose.Aup.IsFinite())
                {
                    playerAup = pose.Aup;
                    return true;
                }

                return false;
            }

            HectonPlayerMovement movement = playerMovement;
            if (movement == null)
                return false;

            playerAup = movement.CurrentAup;
            return playerAup.IsFinite();
        }

        private bool TryResolvePlayerPoseRuntimePosition(out Vector3 runtimePosition)
        {
            runtimePosition = default;
            IPlayerRuntimeContext playerContext = ResolvePlayerRuntimeContext();
            if (playerContext != null)
            {
                if (playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot pose) &&
                    pose.Aup.IsFinite() &&
                    math.all(math.isfinite(pose.RuntimePosition)))
                {
                    runtimePosition = new Vector3(
                        pose.RuntimePosition.x,
                        pose.RuntimePosition.y,
                        pose.RuntimePosition.z);
                    return true;
                }

                return false;
            }

            HectonPlayerMovement movement = playerMovement;
            if (movement == null)
                return false;

            AbsoluteUniversePosition playerAup = movement.CurrentAup;
            return playerAup.IsFinite() &&
                   TryResolveRuntimeOriginRelativeVector3(in playerAup, out runtimePosition);
        }

        private IPlayerRuntimeContext ResolvePlayerRuntimeContext()
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null && playerContext.IsInitialized)
                return playerContext;

            _playerRuntimeContext = null;
            return null;
        }

        private IEcosystemDirectorService ResolveEcosystemDirectorService()
        {
            IEcosystemDirectorService ecosystemDirector = _ecosystemDirectorService;
            if (ecosystemDirector != null && ecosystemDirector.IsInitialized)
                return ecosystemDirector;

            _ecosystemDirectorService = null;
            return null;
        }

        private ISubmarineHullBreachReadModel ResolveSubmarineHullReadModel()
        {
            ISubmarineHullBreachReadModel readModel = _structuralHullReadModel;
            if (readModel != null)
                return readModel;

            return null;
        }

        private float ResolveStructuralHullStress01()
        {
            if (_structuralHullReadModel == null)
                _structuralHullReadModel = ResolveSubmarineHullReadModel();

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

            float breachAreaSeverity = math.saturate(totalBreachArea * StructuralBreachAreaReferenceSquareMetersInv);
            float cellFailureSeverity = math.saturate(breachedCellCount * StructuralBreachCellCountInv);
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
                _structuralHullReadModel = ResolveSubmarineHullReadModel();

            return _structuralHullReadModel != null
                ? math.saturate(_structuralHullReadModel.FatiguePeakNormalized)
                : 0f;
        }

        private float ResolveStructuralDamageTransient01()
        {
            if (_structuralHullReadModel == null)
                _structuralHullReadModel = ResolveSubmarineHullReadModel();

            return _structuralHullReadModel != null
                ? math.saturate(_structuralHullReadModel.RecentImpactSeverityNormalized)
                : 0f;
        }

        private int ResolveGranularMaxVoiceCount()
        {
            float qualityCurve = ResolveCachedAudioQualityCurve01();
            return math.clamp(
                (int)math.round(math.lerp(GranularMinimumQualityVoiceCapacity, GranularVoiceCapacity, qualityCurve)),
                GranularMinimumQualityVoiceCapacity,
                GranularVoiceCapacity);
        }

        private int ResolveGranularMaxVoiceCountWithHysteresis(int requestedVoiceCount, float deltaTime)
        {
            int currentVoiceCount = math.clamp(
                _targetGranularMaxVoiceCount,
                GranularDisabledVoiceCapacity,
                GranularVoiceCapacity);
            int safeRequestedVoiceCount = math.clamp(
                requestedVoiceCount,
                GranularDisabledVoiceCapacity,
                GranularVoiceCapacity);

            if (safeRequestedVoiceCount <= currentVoiceCount)
            {
                _granularVoiceUpgradeHoldSeconds = 0f;
                _granularVoiceUpgradeRequestedCount = safeRequestedVoiceCount;
                return safeRequestedVoiceCount;
            }

            if (_granularVoiceUpgradeRequestedCount != safeRequestedVoiceCount)
            {
                _granularVoiceUpgradeRequestedCount = safeRequestedVoiceCount;
                _granularVoiceUpgradeHoldSeconds = 0f;
            }

            _granularVoiceUpgradeHoldSeconds = math.min(
                GranularVoiceUpgradeHysteresisSeconds,
                _granularVoiceUpgradeHoldSeconds + math.max(0f, deltaTime));
            if (_granularVoiceUpgradeHoldSeconds < GranularVoiceUpgradeHysteresisSeconds)
                return currentVoiceCount;

            _granularVoiceUpgradeHoldSeconds = 0f;
            return safeRequestedVoiceCount;
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

            float proceduralFloor = math.saturate(math.max(_laserCutterHeat01, BubbleBoilMinimumHeatFloor));
            float cavitationDrive = ResolveAscendingNormalized01(_laserCutterHeat01, ToolCavitationHeatStart01, 1f);
            _targetBubbleBoilIntensity = math.saturate(math.max(proceduralFloor, cavitationDrive));
        }

        private void UpdateBinauralTargets()
        {
            ISpatialAudioBinauralEmitterReadModel audioReadModel = ResolveSpatialAudioBinauralEmitterReadModel();
            if (audioReadModel == null ||
                !audioReadModel.TryGetDominantBinauralEmitter(out SpatialAudioBinauralEmitterTelemetry telemetry))
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
            TryResolvePlayerTransportCoordinator();

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
            TryResolvePlayerTransportCoordinator();

            bool coordinatorOwnsTransport = playerTransportCoordinator != null && playerTransportCoordinator.HasActiveTransportSource();
            if (coordinatorOwnsTransport)
                return playerTransportCoordinator.ResolveTransportFeelContract();

            if (playerToolManager == null || playerToolManager.IsSwapping)
                return null;

            return playerToolManager.CurrentToolTransportFeelContract;
        }

        private bool TryResolvePlayerTransportCoordinator()
        {
            if (playerTransportCoordinator != null)
                return true;

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            PlayerTransportCoordinator coordinator = playerContext != null && playerContext.IsInitialized
                ? playerContext.PlayerTransportCoordinator
                : null;
            if (coordinator == null)
                return false;

            playerTransportCoordinator = coordinator;
            SubscribeTransportCoordinator();
            return true;
        }

        private float ResolveTransportModeBlendFloor()
        {
            return _transportFeelContractCurrent != null
                ? _transportFeelContractCurrent.AudioModeBlendFloor
                : 0.35f;
        }

        private float ResolveDiveAttack01()
        {
            Vector3 velocity = CoreDeterminismSignals.TryGetLatestKccVelocityVector(KccVelocityAudioMaxAgeFrames, out Vector3 kccVelocity)
                ? kccVelocity
                : Vector3.zero;
            float downwardSpeed = math.max(0f, -velocity.y);
            return math.saturate(downwardSpeed * 0.41666667f);
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
            return TryEnqueueImpactAudioEvent(
                stress,
                metallic,
                clangExcitation,
                echoExcitation,
                echoDelaySeconds,
                echoAttenuation,
                echoLowPassCutoffHz,
                echoPitchScale,
                0f,
                0f,
                0f,
                0f,
                0f,
                AcousticOcclusionUtility.OpenLowPassCutoffHertz,
                0f);
        }

        private bool TryEnqueueImpactAudioEvent(
            float stress,
            float metallic,
            float clangExcitation,
            float echoExcitation,
            float echoDelaySeconds,
            float echoAttenuation,
            float echoLowPassCutoffHz,
            float echoPitchScale,
            float thudExcitation,
            float thudDurationSeconds,
            float thudStartHertz,
            float thudEndHertz,
            float thudDistortion,
            float thudLowPassCutoffHz,
            float energyJoules)
        {
            float pitchJitter = ResolveImpactPitchJitter(unchecked((uint)Hecton8.Core.SystemDispatcher.CurrentFrameIndex ^ ((uint)(Volatile.Read(ref _impactEventWriteIndex) & ImpactEventQueueMask) * 0x9E3779B9u)));
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
                EchoPitchScale = math.clamp(echoPitchScale * pitchJitter, 0.65f, 1.45f),
                ThudExcitation = math.saturate(thudExcitation),
                ThudDurationSeconds = math.clamp(thudDurationSeconds, 0f, KineticImpactThudDurationSeconds),
                ThudStartHertz = math.clamp(thudStartHertz, 8f, 240f),
                ThudEndHertz = math.clamp(thudEndHertz, 8f, 240f),
                ThudDistortion = math.saturate(thudDistortion),
                ThudLowPassCutoffHz = math.clamp(
                    thudLowPassCutoffHz,
                    AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                    AcousticOcclusionUtility.OpenLowPassCutoffHertz),
                EnergyJoules = math.clamp(
                    math.isfinite(energyJoules) ? energyJoules : 0f,
                    0f,
                    KineticImpactMaximumSafeEnergyJoules)
            };

            for (int attempt = 0; attempt < ImpactEventQueueEnqueueAttemptLimit; attempt++)
            {
                int writeIndex = Volatile.Read(ref _impactEventWriteIndex) & ImpactEventQueueMask;
                int nextWriteIndex = (writeIndex + 1) & ImpactEventQueueMask;
                int observedReadIndex = Volatile.Read(ref _impactEventReadIndex);
                int readIndex = observedReadIndex & ImpactEventQueueMask;
                if (nextWriteIndex == readIndex)
                {
                    int advancedReadIndex = (readIndex + 1) & ImpactEventQueueMask;
                    // Overflow policy: drop the oldest unread event, but only if the consumer
                    // has not already advanced the read pointer since we observed it.
                    if (Interlocked.CompareExchange(ref _impactEventReadIndex, advancedReadIndex, observedReadIndex) != observedReadIndex)
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
            int readIndex = Volatile.Read(ref _impactEventReadIndex) & ImpactEventQueueMask;
            int writeIndex = Volatile.Read(ref _impactEventWriteIndex) & ImpactEventQueueMask;
            if (readIndex == writeIndex)
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
            ref TransientDelayVaultViews transientViews,
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
                    ArmImpactClangInternal(transientViews.ImpactClangDelay, ref hullState, impactAudioEvent.ClangExcitation, clangSeed);
                    clangSeed += 0x9E3779B9u;
                }

                if (impactAudioEvent.ThudExcitation > KineticImpactThudMinimumExcitation)
                    ArmKineticImpactThudInternal(ref hullState, in impactAudioEvent);

                if (impactAudioEvent.EnergyJoules > _audioPeakImpactEnergyJoules)
                    _audioPeakImpactEnergyJoules = impactAudioEvent.EnergyJoules;

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
            float pressureHumPitchScaleTarget,
            float pressureHumGainTarget,
            float impactMetallicTarget,
            int granularMaxVoiceCount,
            float granularAccelerationPitchWobble,
            float granularBasePitchScale,
            float granularGrainLengthScale,
            float granularOverlapDensityScale,
            float granularFmModulationIndex,
            ref FrameScratchVaultViews frameViews,
            ref GranularVoiceVaultViews granularViews,
            ref TransientDelayVaultViews transientViews)
        {
            NativeArray<float> hullScratch = frameViews.HullScratch;
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
            float granularVoiceRatio = math.saturate(granularMaxVoiceCount * math.rcp((float)GranularVoiceCapacity));
            float granularVoiceScale = math.saturate(
                GranularMinimumVoiceDensityOutputScale +
                granularVoiceRatio * (1f - GranularMinimumVoiceDensityOutputScale));
            float previousStructuralSnap = structuralSnapStart;
            float frameTScale = frameCount > 1 ? math.rcp((float)(frameCount - 1)) : 0f;
            int blockSampleRate = math.max(1, _sampleRate);
            TrimGranularVoicesToBudget(granularMaxVoiceCount, ref granularViews);

            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                float frameT = frameCount > 1 ? frameIndex * frameTScale : 0f;
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

                float pressureCreak = RenderPressureCreakSample(ref state, sampleIndex, stress, structuralStressVelocity, depthParam, invSampleRate, blockSampleRate);
                float granularMetal = RenderStructuralGranularVoices(
                    ref state,
                    ref granularViews,
                    granularViews.MetallicGrainBank,
                    sampleIndex,
                    stress,
                    structuralStressVelocity,
                    depthParam,
                    impactMetallicImpulse,
                    granularMaxVoiceCount,
                    granularAccelerationPitchWobble,
                    granularBasePitchScale,
                    granularGrainLengthScale,
                    granularOverlapDensityScale,
                    granularFmModulationIndex,
                    blockSampleRate) * metallicDrive * granularVoiceScale;
                float fatigueRing = RenderStructuralFatigueRingSample(ref state, sampleIndex, structuralFatigue, structuralStress, invSampleRate);
                float structuralSnapTransient = RenderStructuralSnapTransientSample(
                    ref state,
                    sampleIndex,
                    structuralSnap,
                    previousStructuralSnap,
                    structuralStress,
                    invSampleRate);
                float impactClang = RenderImpactClangSampleInternal(transientViews.ImpactClangDelay, ref state, sampleIndex, invSampleRate);
                float kineticThud = RenderKineticImpactThudSampleInternal(ref state, invSampleRate);
                float subBass = RenderHullSubBassSample(ref state, structuralStress, depthParam, absoluteDepthMeters, enclosureDensityIndex, invSampleRate);
                float pressureScrubberHum = RenderPressureScrubberHumSample(
                    ref state,
                    pressureHumDriveTarget,
                    pressureHumPitchScaleTarget,
                    pressureHumGainTarget,
                    invSampleRate);
                float rivetBurst = BuildRivetBurst(sampleIndex, math.max(stress, metallicImpulse), rivetAmount);
                float combined = pressureBed + pressureCreak + granularMetal + fatigueRing + structuralSnapTransient + impactClang + kineticThud + rivetBurst + subBass + pressureScrubberHum;
                combined = ApplyDepthHullDistortion(combined, depthParam, structuralStress);
                hullScratch[frameIndex] = math.max(math.max(stress, structuralSnap), math.abs(kineticThud)) <= HullNoiseFloor
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

        private static bool HasNativeBufferFrames(NativeArray<float> buffer, int frameCount)
        {
            return buffer.IsCreated && frameCount > 0 && buffer.Length >= frameCount;
        }

        private static float FiniteOrZero(float value)
        {
            return math.isfinite(value) ? value : 0f;
        }

        private static float FiniteOrDefault(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
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

        private static void BakeCaveConvolutionImpulseResponse(NativeArray<float> impulse)
        {
            if (!impulse.IsCreated)
                return;

            int safeCount = math.min(impulse.Length, CaveConvolutionImpulseLength);
            for (int i = 0; i < safeCount; i++)
                impulse[i] = ResolvePrebakedCaveImpulseTap(i);
        }

        private static float ResolvePrebakedCaveImpulseTap(int index)
        {
            switch (index)
            {
                case 0: return 0.520f;
                case 1: return 0.312f;
                case 2: return -0.184f;
                case 3: return 0.145f;
                case 4: return 0.096f;
                case 5: return -0.082f;
                case 6: return 0.071f;
                case 7: return -0.061f;
                case 8: return 0.053f;
                case 9: return 0.047f;
                case 10: return -0.040f;
                case 11: return 0.035f;
                case 12: return -0.030f;
                case 13: return 0.026f;
                case 14: return 0.022f;
                case 15: return -0.019f;
                case 16: return 0.016f;
                case 17: return -0.014f;
                case 18: return 0.012f;
                case 19: return 0.010f;
                case 20: return -0.0085f;
                case 21: return 0.0072f;
                case 22: return -0.0061f;
                case 23: return 0.0051f;
                case 24: return 0.0042f;
                case 25: return -0.0035f;
                case 26: return 0.0029f;
                case 27: return -0.0024f;
                case 28: return 0.0019f;
                case 29: return 0.0015f;
                case 30: return -0.0011f;
                case 31: return 0.0008f;
                default: return 0f;
            }
        }

        private static void FillScratchBuffer(NativeArray<float> buffer, int frameCount, float value)
        {
            if (!buffer.IsCreated || frameCount <= 0)
                return;

            int safeCount = math.min(frameCount, buffer.Length);
            for (int i = 0; i < safeCount; i++)
                buffer[i] = value;
        }

        private void ApplyBinauralSpatializationBlock(
            int frameCount,
            AudioParameterSnapshot parameters,
            ref FrameScratchVaultViews frameViews,
            ref BinauralFilterVaultViews filterViews)
        {
            if (!HasFrameScratchBuffers(ref frameViews, frameCount) ||
                !HasBinauralFilterBuffers(ref filterViews))
            {
                return;
            }

            NativeArray<float> mixScratch = frameViews.MixScratch;
            NativeArray<float> stereoMixScratch = frameViews.StereoMixScratch;
            NativeArray<float> binauralDelayRing = filterViews.BinauralDelayRing;
            NativeArray<float> binauralShadowHistory = filterViews.BinauralShadowHistory;
            int safeFrameCount = math.min(frameCount, math.min(mixScratch.Length, stereoMixScratch.Length >> 1));
            if (safeFrameCount <= 0)
                return;

            bool hasDirectionalTarget = parameters.BinauralValid != 0;
            float rightDot = hasDirectionalTarget ? math.clamp(FiniteOrZero(parameters.BinauralRightDot), -1f, 1f) : 0f;
            float shadowAmount = hasDirectionalTarget ? math.saturate(FiniteOrZero(parameters.BinauralShadowAmount01)) : 0f;
            float waterDensityMul = hasDirectionalTarget ? math.saturate(FiniteOrZero(parameters.BinauralWaterDensityMul)) : 0f;
            float shadowCutoffHertz = hasDirectionalTarget
                ? math.clamp(FiniteOrDefault(parameters.BinauralShadowCutoffHertz, _sampleRate * 0.45f), 400f, _sampleRate * 0.45f)
                : _sampleRate * 0.45f;
            shadowCutoffHertz = math.min(
                shadowCutoffHertz,
                math.lerp(_sampleRate * 0.45f, BinauralUnderwaterShadowCutoffHertz, waterDensityMul));
            float spatialEnergy = hasDirectionalTarget ? math.saturate(FiniteOrZero(parameters.BinauralEnergy01)) : 0f;
            float binauralMix = hasDirectionalTarget ? math.lerp(0.18f, 0.85f, spatialEnergy) : 0f;
            float contraFloor = math.lerp(BinauralAirShadowMinimumGain, BinauralWaterShadowMinimumGain, waterDensityMul);
            float contraGain = math.lerp(1f, contraFloor, shadowAmount * binauralMix);
            float sampleRateF = math.max(_sampleRate, 1);
            float minDelaySamples = BinauralMinimumMicroDelaySeconds * sampleRateF;
            float maxDelaySamples = math.min(BinauralMaximumDelaySamples, BinauralMaximumMicroDelaySeconds * sampleRateF);
            float requestedDelaySamples = math.max(0f, FiniteOrZero(parameters.BinauralItdSeconds)) * sampleRateF;
            float lateralItd01 = math.abs(rightDot);
            float resolvedDelaySamples = requestedDelaySamples > 0f ? requestedDelaySamples : lateralItd01 * maxDelaySamples;
            float delaySamples = hasDirectionalTarget && lateralItd01 > 0.001f
                ? math.clamp(math.max(minDelaySamples, resolvedDelaySamples), minDelaySamples, maxDelaySamples)
                : 0f;
            float delayLeftSamples = rightDot > 0f ? delaySamples : 0f;
            float delayRightSamples = rightDot < 0f ? delaySamples : 0f;
            float narcosisChorusDrive = ResolveAscendingNormalized01(
                math.saturate(FiniteOrZero(parameters.NarcosisChorus01)),
                BinauralNarcosisChorusThreshold01,
                1f);
            float chorusPhase = _binauralNarcosisChorusPhase;
            float chorusPhaseStep = (TwoPi * BinauralNarcosisChorusRateHertz) *
                                    math.rcp(math.max(_sampleRate, 1f));
            float chorusBaseDelay = narcosisChorusDrive > 0.0001f
                ? BinauralNarcosisMaximumChorusSamples * 0.5f * narcosisChorusDrive
                : 0f;
            float shadowAlpha = ApproximateExpNegPositive(
                (TwoPi * math.max(400f, shadowCutoffHertz)) *
                math.rcp(math.max(_sampleRate, 1f)));

            for (int frameIndex = 0; frameIndex < safeFrameCount; frameIndex++)
            {
                float mono = FiniteOrZero(mixScratch[frameIndex]);
                int stereoIndex = frameIndex << 1;
                float sonarLeftDelta = FiniteOrZero(stereoMixScratch[stereoIndex]);
                float sonarRightDelta = FiniteOrZero(stereoMixScratch[stereoIndex + 1]);
                binauralDelayRing[_binauralDelayWriteIndex] = mono;

                float chorusOffset = narcosisChorusDrive > 0.0001f
                    ? MathLodApproximation.ApproxSinBhaskara(chorusPhase) * BinauralNarcosisMaximumChorusSamples * narcosisChorusDrive
                    : 0f;
                float leftReadDelay = math.max(delayLeftSamples, chorusBaseDelay) + chorusOffset;
                float rightReadDelay = math.max(delayRightSamples, chorusBaseDelay) - chorusOffset;
                float delayedLeft = leftReadDelay > 0f
                    ? SampleBinauralDelay(binauralDelayRing, leftReadDelay, mono)
                    : mono;
                float delayedRight = rightReadDelay > 0f
                    ? SampleBinauralDelay(binauralDelayRing, rightReadDelay, mono)
                    : mono;

                _binauralDelayWriteIndex = (_binauralDelayWriteIndex + 1) & BinauralDelayMask;
                chorusPhase += chorusPhaseStep;
                if (chorusPhase >= TwoPi)
                    chorusPhase -= TwoPi;

                float leftSpatial = delayedLeft;
                float rightSpatial = delayedRight;
                if (hasDirectionalTarget)
                {
                    if (rightDot > 0f)
                    {
                        leftSpatial = ApplyBinauralShadowEar(binauralShadowHistory, delayedLeft * contraGain, 0, shadowAlpha);
                        rightSpatial = delayedRight;
                    }
                    else if (rightDot < 0f)
                    {
                        leftSpatial = delayedLeft;
                        rightSpatial = ApplyBinauralShadowEar(binauralShadowHistory, delayedRight * contraGain, 1, shadowAlpha);
                    }
                }

                float left = math.lerp(mono, leftSpatial, binauralMix) + sonarLeftDelta;
                float right = math.lerp(mono, rightSpatial, binauralMix) + sonarRightDelta;
                stereoMixScratch[stereoIndex] = math.clamp(left, -1f, 1f);
                stereoMixScratch[stereoIndex + 1] = math.clamp(right, -1f, 1f);
            }

            _binauralNarcosisChorusPhase = chorusPhase;
        }

        private static float ApplyBinauralShadowEar(NativeArray<float> binauralShadowHistory, float sample, int earIndex, float alpha)
        {
            float previous = binauralShadowHistory[earIndex] + BiquadDenormalBias;
            float filtered = sample + alpha * (previous - sample);
            binauralShadowHistory[earIndex] = filtered;
            return filtered;
        }

        private float SampleBinauralDelay(NativeArray<float> binauralDelayRing, float delaySamples, float fallback)
        {
            if (!binauralDelayRing.IsCreated || delaySamples <= 0f)
                return fallback;

            float clampedDelay = math.clamp(delaySamples, 0f, BinauralMaximumDelaySamples);
            int baseDelay = (int)clampedDelay;
            float fraction = clampedDelay - baseDelay;
            float sample0 = binauralDelayRing[(_binauralDelayWriteIndex - baseDelay) & BinauralDelayMask];
            float sample1 = binauralDelayRing[(_binauralDelayWriteIndex - baseDelay - 1) & BinauralDelayMask];
            return math.lerp(sample0, sample1, fraction);
        }

        private void RenderSonarBlock(
            int frameCount,
            long blockStartFrame,
            double invSampleRate,
            float fmModulationIndex,
            ref FrameScratchVaultViews frameViews,
            ref SonarTapVaultViews tapViews,
            ref SonarDspVaultViews dspViews)
        {
            NativeArray<float> sonarScratch = frameViews.SonarScratch;
            NativeArray<float> stereoMixScratch = frameViews.StereoMixScratch;
            if (!tapViews.Worker.IsCreated ||
                !dspViews.EchoDelay.IsCreated ||
                !dspViews.ReadCursors.IsCreated)
            {
                ClearScratchBuffer(sonarScratch, frameCount);
                ClearSonarStereoDelta(stereoMixScratch, frameCount);
                return;
            }

            SonarTriggerState activeState = _workerActiveSonarState;
            if (activeState.Sequence == 0 || activeState.Intensity <= 0f)
            {
                ClearScratchBuffer(sonarScratch, frameCount);
                ClearSonarStereoDelta(stereoMixScratch, frameCount);
                return;
            }

            SonarSynthesisState state = _sonarSynthesisState;
            if (state.ActiveSequence != activeState.Sequence)
            {
                ResetSonarPhaseState(activeState.Sequence);
                state = _sonarSynthesisState;
            }

            NativeArray<SonarEchoTap> activeTapBuffer = tapViews.Worker;
            int activeTapCount = activeTapBuffer.IsCreated
                ? math.clamp(_workerActiveSonarTapCount, 0, math.min(SonarEchoTapCapacity, activeTapBuffer.Length))
                : 0;
            bool kineticImpactEcho = (activeState.Flags & SonarTriggerFlagKineticImpactEcho) != 0;
            float activeDurationSeconds = kineticImpactEcho
                ? KineticImpactPortalEchoLifetimeSeconds
                : SonarTotalDurationSeconds;
            float echoSourceDurationSeconds = kineticImpactEcho
                ? KineticImpactThudDurationSeconds
                : SonarChirpDurationSeconds;
            long maxActiveFrame = activeState.StartFrame +
                math.max(1, (int)(activeDurationSeconds * math.max(_sampleRate, 1) + 0.5f));
            float dopplerFrameDelta = frameCount > 1 ? math.rcp((float)(frameCount - 1)) : 1f;
            float safeFmModulationIndex = math.clamp(
                FiniteOrDefault(fmModulationIndex, 1f),
                GranularTuningFmModulationMinimum,
                GranularTuningFmModulationMaximum);
            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                long sampleFrame = blockStartFrame + frameIndex;
                float dopplerFrameT = math.saturate(frameIndex * dopplerFrameDelta);
                float age = (float)((sampleFrame - activeState.StartFrame) * invSampleRate);
                if (age < 0f || age > activeDurationSeconds)
                {
                    sonarScratch[frameIndex] = 0f;
                    StoreSonarStereoDelta(stereoMixScratch, frameIndex, 0f, 0f);
                    continue;
                }

                uint sampleIndex = (uint)math.max(0L, sampleFrame);
                float drySignal = 0f;
                float tail = 0f;
                if (kineticImpactEcho)
                {
                    if (age < KineticImpactThudDurationSeconds)
                    {
                        float thudT = math.saturate(age * math.rcp(KineticImpactThudDurationSeconds));
                        float thudFrequency = math.lerp(KineticImpactThudStartHertz, KineticImpactThudEndHertz, thudT);
                        float thudEnvelope = math.saturate(age * 200f) * (1f - thudT) * (1f - thudT);
                        drySignal = thudEnvelope * AdvanceSine(ref state.ChirpPhase, thudFrequency, invSampleRate);
                    }
                }
                else
                {
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
                        float chirpT = math.saturate(age * SonarChirpDurationSecondsInv);
                        float chirpBaseFrequency = math.lerp(2000f, 400f, chirpT);
                        float chirpFmDepthHertz = math.lerp(0f, 180f, math.saturate(safeFmModulationIndex * 0.25f));
                        float chirpModulator = AdvanceSine(
                            ref state.FmModulatorPhase,
                            math.lerp(45d, 150d, chirpT),
                            invSampleRate);
                        float chirpFrequency = math.max(40f, chirpBaseFrequency + chirpModulator * chirpFmDepthHertz);
                        float chirpEnv = ApproximateExpNegPositive(age * 5f);
                        float carrier = AdvanceSine(ref state.ChirpPhase, chirpFrequency, invSampleRate);
                        float sideband = AdvanceSine(ref state.EchoPhase, chirpFrequency * 1.997f, invSampleRate) *
                            math.saturate(safeFmModulationIndex * 0.16f);
                        chirp = chirpEnv * (carrier + sideband);
                    }

                    drySignal = attack + chirp;
                    if (age >= 0.08f)
                    {
                        float tailAge = age - 0.08f;
                        float tailEnv = math.saturate(tailAge * SonarTailAttackSecondsInv) * ApproximateExpNegPositive(tailAge * 0.95f);
                        float slowLfo = 0.55f + 0.45f * AdvanceSine(ref state.TailSlowPhase, 0.38d, invSampleRate);
                        float beat =
                            AdvanceSine(ref state.TailBeatAPhase, 150d, invSampleRate) +
                            AdvanceSine(ref state.TailBeatBPhase, 147d, invSampleRate) * 0.6f +
                            AdvanceSine(ref state.TailBeatCPhase, 300d, invSampleRate) * 0.4f;
                        float pinkTail = LayeredPinkLike(sampleIndex) * slowLfo;
                        tail = tailEnv * ((beat * 0.46f) + (pinkTail * 0.54f)) * sonarTailBlend;
                    }
                }

                if (dspViews.EchoDelay.IsCreated)
                {
                    dspViews.EchoDelay[state.EchoWriteIndex] = drySignal;
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
                        if (dspViews.ReadCursors.IsCreated)
                            dspViews.ReadCursors[tapIndex] = -1f;
                        continue;
                    }

                    if (echoAge >= echoSourceDurationSeconds || !dspViews.EchoDelay.IsCreated || !dspViews.ReadCursors.IsCreated)
                        continue;

                    int echoDelaySamples = tap.DelaySamples;
                    if (echoDelaySamples <= 0)
                        continue;

                    float echoReadCursor = dspViews.ReadCursors[tapIndex];
                    if (echoReadCursor < 0f)
                        echoReadCursor = (state.EchoWriteIndex - echoDelaySamples) & SonarEchoDelayMask;

                    float dopplerRatio = math.clamp(
                        math.lerp(tap.PreviousDopplerRatio, tap.DopplerRatio, dopplerFrameT),
                        SonarEchoMinimumDopplerRatio,
                        SonarEchoMaximumDopplerRatio);
                    float tapEcho = LinearSampleRing(dspViews.EchoDelay, echoReadCursor, SonarEchoDelayMask) *
                                    (ApproximateExpNegPositive(echoAge * 4.5f) * tap.Attenuation);
                    echoReadCursor = WrapRingCursor(echoReadCursor + dopplerRatio, SonarEchoDelayCapacity);
                    dspViews.ReadCursors[tapIndex] = echoReadCursor;

                    if (dspViews.FilterInput1.IsCreated &&
                        dspViews.FilterInput2.IsCreated &&
                        dspViews.FilterOutput1.IsCreated &&
                        dspViews.FilterOutput2.IsCreated)
                    {
                        float filteredEcho =
                            tap.LowPassB0 * tapEcho +
                            tap.LowPassB1 * dspViews.FilterInput1[tapIndex] +
                            tap.LowPassB2 * dspViews.FilterInput2[tapIndex] -
                            tap.LowPassA1 * (dspViews.FilterOutput1[tapIndex] + BiquadDenormalBias) -
                            tap.LowPassA2 * (dspViews.FilterOutput2[tapIndex] + BiquadDenormalBias);

                        dspViews.FilterInput2[tapIndex] = dspViews.FilterInput1[tapIndex];
                        dspViews.FilterInput1[tapIndex] = tapEcho;
                        dspViews.FilterOutput2[tapIndex] = dspViews.FilterOutput1[tapIndex];
                        dspViews.FilterOutput1[tapIndex] = filteredEcho;
                        tapEcho = math.lerp(tapEcho, filteredEcho, tap.UseLowPass);
                    }

                    echo += tapEcho;
                    echoLeftDelta += tapEcho * tap.LeftPanDeltaGain;
                    echoRightDelta += tapEcho * tap.RightPanDeltaGain;
                }

                float masterGain = kineticImpactEcho ? 1f : sonarMasterGain;
                float saturationDrive = kineticImpactEcho ? 1.35f : sonarSaturationDrive;
                float mixed = (drySignal + echo + tail) * activeState.Intensity;
                sonarScratch[frameIndex] = FastSoftClip(mixed * saturationDrive) * masterGain;
                StoreSonarStereoDelta(
                    stereoMixScratch,
                    frameIndex,
                    echoLeftDelta * activeState.Intensity * masterGain,
                    echoRightDelta * activeState.Intensity * masterGain);
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

        private static void ClearSonarStereoDelta(NativeArray<float> stereoMixScratch, int frameCount)
        {
            if (!stereoMixScratch.IsCreated)
                return;

            int safeCount = math.min(frameCount * BinauralOutputChannels, stereoMixScratch.Length);
            for (int i = 0; i < safeCount; i++)
                stereoMixScratch[i] = 0f;
        }

        private static void StoreSonarStereoDelta(NativeArray<float> stereoMixScratch, int frameIndex, float leftDelta, float rightDelta)
        {
            if (!stereoMixScratch.IsCreated)
                return;

            int stereoIndex = frameIndex << 1;
            if (stereoIndex + 1 >= stereoMixScratch.Length)
                return;

            stereoMixScratch[stereoIndex] = leftDelta;
            stereoMixScratch[stereoIndex + 1] = rightDelta;
        }

        private void RenderImpactEchoBlock(int frameCount, double invSampleRate, NativeArray<float> impactEchoScratch)
        {
            if (!impactEchoScratch.IsCreated)
                return;

            ImpactEchoSynthesisState state = _impactEchoSynthesisState;
            if (state.Excitation <= ImpactEchoMinimumExcitation || state.Attenuation <= 0.0001f)
            {
                ClearScratchBuffer(impactEchoScratch, frameCount);
                return;
            }

            float decayRate = math.max(ImpactEchoDecayPerSecond, 0.01f);
            float lowPassCutoff = math.clamp(
                state.LowPassCutoffHz,
                AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                AcousticOcclusionUtility.OpenLowPassCutoffHertz);
            float lowPassAlpha = ApproximateExpNegPositive(TwoPi * lowPassCutoff * math.rcp(math.max(_sampleRate, 1f)));
            float pitchScale = math.clamp(state.PitchScale <= 0f ? 1f : state.PitchScale, 0.65f, 1.45f);

            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                if (state.DelayRemainingSeconds > 0f)
                {
                    state.DelayRemainingSeconds = math.max(0f, state.DelayRemainingSeconds - (float)invSampleRate);
                    impactEchoScratch[frameIndex] = 0f;
                    continue;
                }

                float age = state.ElapsedSeconds;
                if (age >= ImpactEchoMaximumLifetimeSeconds)
                {
                    impactEchoScratch[frameIndex] = 0f;
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
                impactEchoScratch[frameIndex] = FastSoftClip(filtered * 2.2f) * 0.35f;
                state.ElapsedSeconds += (float)invSampleRate;
            }

            _impactEchoSynthesisState = state;
        }

        private static void ArmKineticImpactThudInternal(ref HullSynthesisState state, in ImpactAudioEvent impactAudioEvent)
        {
            float excitation = math.saturate(impactAudioEvent.ThudExcitation);
            if (excitation <= KineticImpactThudMinimumExcitation)
                return;

            state.KineticImpactThudAgeSeconds = 0f;
            state.KineticImpactThudDurationSeconds = math.clamp(
                impactAudioEvent.ThudDurationSeconds,
                0.02f,
                KineticImpactThudDurationSeconds);
            state.KineticImpactThudStartHertz = math.clamp(
                impactAudioEvent.ThudStartHertz,
                KineticImpactThudEndHertz,
                KineticImpactThudStartHertz);
            state.KineticImpactThudEndHertz = math.clamp(
                impactAudioEvent.ThudEndHertz,
                8f,
                state.KineticImpactThudStartHertz);
            state.KineticImpactThudAmplitude = math.max(state.KineticImpactThudAmplitude, excitation);
            state.KineticImpactThudDistortion = math.max(state.KineticImpactThudDistortion, impactAudioEvent.ThudDistortion);
            state.KineticImpactThudLowPassCutoffHz = math.clamp(
                impactAudioEvent.ThudLowPassCutoffHz,
                AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                AcousticOcclusionUtility.OpenLowPassCutoffHertz);
            state.KineticImpactThudLowPassState = 0f;
        }

        private float RenderKineticImpactThudSampleInternal(ref HullSynthesisState state, double invSampleRate)
        {
            if (state.KineticImpactThudAmplitude <= KineticImpactThudMinimumExcitation ||
                state.KineticImpactThudDurationSeconds <= 0f)
            {
                return 0f;
            }

            float age = state.KineticImpactThudAgeSeconds;
            float duration = math.max(0.02f, state.KineticImpactThudDurationSeconds);
            if (age >= duration)
            {
                state.KineticImpactThudAmplitude = 0f;
                state.KineticImpactThudDistortion = 0f;
                return 0f;
            }

            float t = math.saturate(age * math.rcp(duration));
            float attack = math.saturate(age * 200f);
            float decay = (1f - t) * (1f - t);
            float envelope = attack * decay;
            float frequency = math.lerp(
                state.KineticImpactThudStartHertz,
                state.KineticImpactThudEndHertz,
                t);
            float raw = AdvanceSine(ref state.KineticImpactThudPhase, frequency, invSampleRate) *
                envelope *
                state.KineticImpactThudAmplitude;
            float cutoff = math.clamp(
                state.KineticImpactThudLowPassCutoffHz,
                AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                AcousticOcclusionUtility.OpenLowPassCutoffHertz);
            float lowPassAlpha = ApproximateExpNegPositive(TwoPi * cutoff * math.rcp(math.max(_sampleRate, 1f)));
            float filtered = raw + lowPassAlpha * ((state.KineticImpactThudLowPassState + BiquadDenormalBias) - raw);
            state.KineticImpactThudLowPassState = filtered;

            float distortion = math.saturate(state.KineticImpactThudDistortion);
            float hardClipped = math.clamp(filtered * math.lerp(1f, 2.85f, distortion), -0.82f, 0.82f);
            float output = math.lerp(filtered, hardClipped, distortion) * 0.72f;
            state.KineticImpactThudAgeSeconds += (float)invSampleRate;
            state.KineticImpactThudAmplitude *= ApproximateExpNegPositive(0.6f * (float)invSampleRate);
            return math.isfinite(output) ? output : 0f;
        }

        private void ArmImpactClangInternal(NativeArray<float> impactClangDelay, ref HullSynthesisState state, float excitation, uint seed)
        {
            if (!impactClangDelay.IsCreated || excitation <= ImpactClangMinimumExcitation)
                return;

            float pitchScale = math.lerp(
                1f - ImpactClangPitchSpread,
                1f + ImpactClangPitchSpread,
                Hash01(seed ^ 0x13A531D7u));
            int delaySamples = math.clamp(
                (int)(_sampleRate * math.rcp(math.max(24f, ImpactClangFundamentalHertz * pitchScale)) + 0.5f),
                2,
                ImpactClangDelayCapacity - 2);
            int writeIndex = state.ImpactClangWriteIndex & ImpactClangDelayMask;
            float seedAmplitude = math.lerp(0.22f, 0.95f, excitation);
            float noiseDecay = 1f;
            for (int i = 0; i < delaySamples; i++)
            {
                int ringIndex = (writeIndex + i) & ImpactClangDelayMask;
                impactClangDelay[ringIndex] = HashSigned(seed + (uint)i * 0x9E3779B9u) * seedAmplitude * noiseDecay;
                noiseDecay *= ImpactClangNoiseSeedDecay;
            }

            state.ImpactClangDelaySamples = delaySamples;
            state.ImpactClangEnvelope = math.max(state.ImpactClangEnvelope, excitation);
            state.ImpactClangFeedback = math.lerp(ImpactClangFeedbackMinimum, ImpactClangFeedbackMaximum, excitation);
            state.ImpactClangLowPassState = 0f;
            state.ImpactClangWriteIndex = (writeIndex + delaySamples) & ImpactClangDelayMask;
        }

        private float RenderImpactClangSampleInternal(
            NativeArray<float> impactClangDelay,
            ref HullSynthesisState state,
            uint sampleIndex,
            double invSampleRate)
        {
            if (!impactClangDelay.IsCreated ||
                state.ImpactClangEnvelope <= 0.0001f ||
                state.ImpactClangDelaySamples <= 1)
            {
                return 0f;
            }

            int writeIndex = state.ImpactClangWriteIndex & ImpactClangDelayMask;
            int delaySamples = math.clamp(state.ImpactClangDelaySamples, 2, ImpactClangDelayCapacity - 2);
            int readIndexA = (writeIndex - delaySamples) & ImpactClangDelayMask;
            int readIndexB = (readIndexA - 1) & ImpactClangDelayMask;
            float delayedA = impactClangDelay[readIndexA];
            float delayedB = impactClangDelay[readIndexB];
            float averaged = (delayedA + delayedB) * 0.5f;
            float filtered = math.lerp(delayedA, averaged, ImpactClangLowPassBlend);
            float noiseEdge = HashSigned(sampleIndex ^ 0x61F0B1C3u) * 0.018f * state.ImpactClangEnvelope;
            state.ImpactClangLowPassState = math.lerp(state.ImpactClangLowPassState + BiquadDenormalBias, filtered + noiseEdge, 0.5f);
            impactClangDelay[writeIndex] = state.ImpactClangLowPassState * state.ImpactClangFeedback;
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
            ref FrameScratchVaultViews frameViews,
            ref TransientDelayVaultViews transientViews,
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
            NativeArray<float> thrusterScratch = frameViews.ThrusterScratch;
            if (!thrusterScratch.IsCreated)
                return;

            NativeArray<float> thrusterCombDelay = transientViews.ThrusterCombDelay;
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
                (int)(_sampleRate * math.rcp(math.max(1f, blockBladePassHz)) + 0.5f),
                1,
                ThrusterCombDelayCapacity - 1);
            float frameTScale = frameCount > 1 ? math.rcp((float)(frameCount - 1)) : 0f;

            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                float frameT = frameCount > 1 ? frameIndex * frameTScale : 0f;
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
                float delayedBladePass = thrusterCombDelay.IsCreated ? thrusterCombDelay[combReadIndex] : 0f;
                state.CombFeedbackSample = math.lerp(delayedBladePass, state.CombFeedbackSample, ThrusterCombDamp);
                float combFeedback = math.lerp(0.18f, 0.62f, math.saturate(load * 0.65f + pressure * 0.35f));
                if (thrusterCombDelay.IsCreated)
                {
                    thrusterCombDelay[combWriteIndex] = bandPassedFlow + state.CombFeedbackSample * combFeedback;
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
                thrusterScratch[frameIndex] = FastSoftClip(mixed * 2.0f) * thrusterMasterGain * blend * rpmGain;
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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
                return ageSeconds * math.rcp(math.max(HeartbeatAttackSeconds, 0.0001f));

            if (ageSeconds < decayEnd)
            {
                float decayT = (ageSeconds - attackEnd) * math.rcp(math.max(HeartbeatDecaySeconds, 0.0001f));
                return math.lerp(1f, 0.58f, decayT);
            }

            if (ageSeconds < sustainEnd)
                return 0.58f;

            float releaseT = (ageSeconds - sustainEnd) * math.rcp(math.max(HeartbeatReleaseSeconds, 0.0001f));
            return math.lerp(0.58f, 0f, releaseT);
        }

        private static float RenderPressureCreakSample(
            ref HullSynthesisState state,
            uint sampleIndex,
            float stress,
            float stressDerivative,
            float depthParam,
            double invSampleRate,
            int sampleRate)
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
                    StartPressureCreakGrain(ref state, sampleIndex, stress, depthParam, stressDerivative, sampleRate);
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
            int sampleRate)
        {
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

        private float RenderStructuralGranularVoices(
            ref HullSynthesisState state,
            ref GranularVoiceVaultViews granularViews,
            NativeArray<float> grainBank,
            uint sampleIndex,
            float stress,
            float stressDerivative,
            float depthParam,
            float impactMetallic,
            int maxVoices,
            float accelerationPitchWobble,
            float basePitchScale,
            float grainLengthScale,
            float overlapDensityScale,
            float fmModulationIndex,
            int sampleRate)
        {
            if (!HasGranularVoiceBuffers(ref granularViews) || !grainBank.IsCreated || grainBank.Length <= 1)
                return 0f;

            int voiceLimit = math.clamp(maxVoices, GranularDisabledVoiceCapacity, GranularVoiceCapacity);
            if (voiceLimit <= 0)
                return 0f;

            NativeArray<int> voiceActive = granularViews.VoiceActive;
            NativeArray<int> voiceElapsed = granularViews.VoiceElapsed;
            NativeArray<int> voiceLength = granularViews.VoiceLength;
            NativeArray<int> voiceStart = granularViews.VoiceStart;
            NativeArray<float> voiceCursor = granularViews.VoiceCursor;
            NativeArray<float> voicePlaybackRate = granularViews.VoicePlaybackRate;
            NativeArray<float> voiceGain = granularViews.VoiceGain;

            float safeStress = math.saturate(FiniteOrZero(stress));
            float safeStressDerivative = math.saturate(FiniteOrZero(stressDerivative));
            float safeDepthParam = math.saturate(FiniteOrZero(depthParam));
            float impactDrive = math.saturate(FiniteOrZero(impactMetallic));
            float safePitchWobble = math.clamp(FiniteOrDefault(accelerationPitchWobble, 1f), 0.92f, 1.12f);
            float safeBasePitchScale = math.clamp(
                FiniteOrDefault(basePitchScale, 1f),
                GranularTuningBasePitchMinimum,
                GranularTuningBasePitchMaximum);
            float safeGrainLengthScale = math.clamp(
                FiniteOrDefault(grainLengthScale, 1f),
                GranularTuningGrainLengthMinimum,
                GranularTuningGrainLengthMaximum);
            float safeOverlapDensityScale = math.clamp(
                FiniteOrDefault(overlapDensityScale, 1f),
                GranularTuningOverlapDensityMinimum,
                GranularTuningOverlapDensityMaximum);
            float safeFmModulationIndex = math.clamp(
                FiniteOrDefault(fmModulationIndex, 1f),
                GranularTuningFmModulationMinimum,
                GranularTuningFmModulationMaximum);
            float structuralDrive = math.saturate((math.max(safeStress, safeStressDerivative) - GranularStressThreshold01) * 2f);
            int safeSampleRate = math.max(1, sampleRate);

            if (impactDrive > HullNoiseFloor)
            {
                int cooldownSamples = math.max(1, (int)(GranularImpactClusterCooldownSeconds * safeSampleRate + 0.5f));
                long currentSampleFrame = sampleIndex;
                if (currentSampleFrame - state.LastGranularImpactClusterSampleFrame >= cooldownSamples)
                {
                    uint clusterSeed = HashUInt(sampleIndex ^ 0x5A84C9E3u);
                    int clusterVoices = math.min(GranularImpactClusterVoiceCount, voiceLimit);
                    for (int i = 0; i < clusterVoices; i++)
                    {
                        clusterSeed = NextLcg(clusterSeed);
                        ArmGranularVoice(
                            ref granularViews,
                            grainBank,
                            voiceLimit,
                            safeSampleRate,
                            clusterSeed,
                            math.max(safeStress, impactDrive),
                            math.max(safeStressDerivative, impactDrive),
                            safeDepthParam,
                            impactDrive,
                            safePitchWobble,
                            safeBasePitchScale,
                            safeGrainLengthScale,
                            safeFmModulationIndex,
                            highPitchCluster: true);
                    }

                    state.LastGranularImpactClusterSampleFrame = currentSampleFrame;
                }
            }

            if (structuralDrive > HullNoiseFloor)
            {
                float eventsPerSecond =
                    math.lerp(0.25f, 24f, structuralDrive) +
                    safeStressDerivative * 18f +
                    impactDrive * 32f;
                eventsPerSecond *= safeOverlapDensityScale;
                float eventThreshold = math.saturate(eventsPerSecond * math.rcp((float)safeSampleRate));
                if (Hash01(sampleIndex ^ 0x2FD6A8BBu) <= eventThreshold)
                {
                    uint seed = HashUInt(sampleIndex ^ 0xB9175A2Du);
                    ArmGranularVoice(
                        ref granularViews,
                        grainBank,
                        voiceLimit,
                        safeSampleRate,
                        seed,
                        safeStress,
                        safeStressDerivative,
                        safeDepthParam,
                        impactDrive,
                        safePitchWobble,
                        safeBasePitchScale,
                        safeGrainLengthScale,
                        safeFmModulationIndex,
                        highPitchCluster: false);
                }
            }

            float mixed = 0f;
            int activeVoiceCount = 0;
            bool linearWindow = voiceLimit <= GranularMinimumQualityVoiceCapacity;
            bool highQualityInterpolation = voiceLimit >= GranularHighQualityInterpolationVoiceThreshold;
            for (int voiceIndex = 0; voiceIndex < voiceLimit; voiceIndex++)
            {
                if (voiceActive[voiceIndex] == 0)
                    continue;

                int elapsed = voiceElapsed[voiceIndex];
                int length = math.clamp(voiceLength[voiceIndex], 1, grainBank.Length);
                if (elapsed >= length)
                {
                    voiceActive[voiceIndex] = 0;
                    continue;
                }

                activeVoiceCount++;
                float cursor = voiceCursor[voiceIndex];
                if (!math.isfinite(cursor))
                    cursor = 0f;
                cursor = math.clamp(cursor, 0f, math.max(0f, length - 1f));
                float currentVoiceGain = math.saturate(FiniteOrZero(voiceGain[voiceIndex]));
                float playbackRate = math.clamp(
                    FiniteOrDefault(voicePlaybackRate[voiceIndex], 1f),
                    0.05f,
                    2.4f);

                float sample = highQualityInterpolation
                    ? HermiteSampleGrainWindow(
                        grainBank,
                        voiceStart[voiceIndex],
                        length,
                        cursor)
                    : LinearSampleGrainWindow(
                        grainBank,
                        voiceStart[voiceIndex],
                        length,
                        cursor);
                float envelope = linearWindow
                    ? ResolveLinearGrainEnvelope(elapsed, length)
                    : ResolveParabolicGrainEnvelope(elapsed, length);
                mixed += FiniteOrZero(sample) * envelope * currentVoiceGain;

                cursor += playbackRate;
                if (cursor >= length)
                    cursor -= length;

                elapsed++;
                if (elapsed >= length)
                {
                    voiceActive[voiceIndex] = 0;
                    continue;
                }

                voiceCursor[voiceIndex] = cursor;
                voiceElapsed[voiceIndex] = elapsed;
            }

            float clipped = FastSoftClip(FiniteOrZero(mixed));
            RecordGranularTelemetry(
                sampleIndex,
                safeStress,
                safeStressDerivative,
                safeDepthParam,
                impactDrive,
                clipped,
                activeVoiceCount,
                voiceLimit);
            return math.isfinite(clipped) ? clipped : 0f;
        }

        private void TrimGranularVoicesToBudget(int voiceLimit, ref GranularVoiceVaultViews granularViews)
        {
            if (!HasGranularVoiceBuffers(ref granularViews))
                return;

            NativeArray<int> voiceActive = granularViews.VoiceActive;
            NativeArray<int> voiceElapsed = granularViews.VoiceElapsed;
            NativeArray<float> voiceCursor = granularViews.VoiceCursor;
            NativeArray<float> voiceGain = granularViews.VoiceGain;
            int safeLimit = math.clamp(voiceLimit, GranularDisabledVoiceCapacity, GranularVoiceCapacity);
            for (int voiceIndex = safeLimit; voiceIndex < GranularVoiceCapacity; voiceIndex++)
            {
                voiceActive[voiceIndex] = 0;
                voiceElapsed[voiceIndex] = 0;
                voiceCursor[voiceIndex] = 0f;
                voiceGain[voiceIndex] = 0f;
            }
        }

        private void NoteAudioSynthesisConsecutiveFailure()
        {
            int failureCount = Interlocked.Increment(ref _audioSynthesisConsecutiveVaultFailures);
            if (failureCount >= AudioSynthesisTelemetryFailureDumpThreshold)
                Interlocked.Exchange(ref _audioSynthesisTelemetryDumpRequested, 1);
        }

        private void ResetAudioSynthesisConsecutiveFailures()
        {
            Interlocked.Exchange(ref _audioSynthesisConsecutiveVaultFailures, 0);
        }

        private bool RecordAudioSynthesisTelemetry(
            uint bufferId,
            int failureCode,
            uint flags,
            int activePolyphony,
            int voiceLimit,
            float dspMicroseconds)
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            IDataVault guardVault = null;
            NativeArray<AudioSynthesisTelemetryEntry> telemetryRing = default;
            try
            {
                if (!TryAcquirePlayerCriticalMutationBuffer(
                        in _audioSynthesisTelemetryRingHandle,
                        PlayerCriticalAudioSynthesisTelemetryRingBufferId,
                        AudioSynthesisTelemetryCapacity,
                        AudioSynthesisTelemetryMutationGuardMask,
                        out telemetryRing,
                        out guardVault))
                {
                    int failureCount = Volatile.Read(ref _audioSynthesisConsecutiveVaultFailures);
                    if (failureCount >= AudioSynthesisTelemetryFailureDumpThreshold)
                        Interlocked.Exchange(ref _audioSynthesisTelemetryDumpRequested, 1);
                    return false;
                }

                int cursor = _audioSynthesisTelemetryCursor;
                if ((uint)cursor >= (uint)AudioSynthesisTelemetryCapacity)
                    cursor = 0;

                telemetryRing[cursor] = new AudioSynthesisTelemetryEntry
                {
                    StopwatchTicks = System.Diagnostics.Stopwatch.GetTimestamp(),
                    Frame = (uint)math.max(0, Hecton8.Core.SystemDispatcher.CurrentFrameIndex),
                    BufferId = bufferId,
                    SystemId = (uint)VaultOwner,
                    ExpectedGeneration = _audioSynthesisTelemetryRingHandle.Generation,
                    ActualGeneration = _audioSynthesisTelemetryRingHandle.Generation,
                    Flags = flags,
                    ActivePolyphony = math.max(0, activePolyphony),
                    VoiceLimit = math.clamp(voiceLimit, GranularDisabledVoiceCapacity, GranularVoiceCapacity),
                    DspMicroseconds = math.max(0f, FiniteOrDefault(dspMicroseconds, 0f)),
                    GlobalQualityWeight = math.saturate(_cachedAudioQualityWeight01),
                    FailureCode = failureCode,
                    UnderrunCount = Volatile.Read(ref _audioBufferUnderrunCount)
                };

                cursor++;
                if (cursor >= AudioSynthesisTelemetryCapacity)
                    cursor = 0;
                _audioSynthesisTelemetryCursor = cursor;
                if (failureCode != AudioSynthesisFailureNone ||
                    (flags & (AudioSynthesisTelemetryFlagNonFiniteSample | AudioSynthesisTelemetryFlagOutputUnderrun)) != 0u)
                {
                    NoteAudioSynthesisConsecutiveFailure();
                }

                return true;
            }
            finally
            {
                ReleasePlayerCriticalMutationGuard(guardVault, AudioSynthesisTelemetryMutationGuardMask);
            }
        }

        private void RecordGranularTelemetry(
            uint sampleIndex,
            float stress,
            float stressDerivative,
            float depthParam,
            float impactDrive,
            float mixedSample,
            int activeVoiceCount,
            int voiceLimit)
        {
            bool invalid =
                !math.isfinite(stress) ||
                !math.isfinite(stressDerivative) ||
                !math.isfinite(depthParam) ||
                !math.isfinite(impactDrive) ||
                !math.isfinite(mixedSample) ||
                !math.isfinite(_audioPeakImpactEnergyJoules);
            uint flags = 0u;
            if (invalid)
                flags |= 1u;
            if (activeVoiceCount >= voiceLimit)
                flags |= 2u;
            if (impactDrive > HullNoiseFloor)
                flags |= 4u;
            if (!invalid && (sampleIndex & GranularTelemetrySampleStrideMask) != 0u)
                return;

            CrashTelemetryBuffer.ReportAudioDspStats(
                activeVoiceCount,
                Volatile.Read(ref _audioBufferUnderrunCount));

            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            IDataVault guardVault = null;
            NativeArray<GranularAudioTelemetryEntry> granularTelemetryRing = default;
            try
            {
                if (!TryAcquirePlayerCriticalMutationBuffer(
                        in _granularTelemetryRingHandle,
                        BufferID.PlayerCriticalGranularTelemetryRing,
                        GranularTelemetryCapacity,
                        GranularTelemetryMutationGuardMask,
                        out granularTelemetryRing,
                        out guardVault))
                {
                    RecordAudioSynthesisTelemetry(
                        (uint)_granularTelemetryRingHandle.BufferID,
                        AudioSynthesisFailureTelemetryLock,
                        AudioSynthesisTelemetryFlagLockContention,
                        activeVoiceCount,
                        voiceLimit,
                        0f);
                    return;
                }

                int cursor = _granularTelemetryCursor;
                if ((uint)cursor >= (uint)GranularTelemetryCapacity)
                    cursor = 0;

                granularTelemetryRing[cursor] = new GranularAudioTelemetryEntry
                {
                    SampleIndex = sampleIndex,
                    Stress01 = math.saturate(stress),
                    StressDerivative01 = math.saturate(stressDerivative),
                    Depth01 = math.saturate(depthParam),
                    Impact01 = math.saturate(impactDrive),
                    MixedSample = mixedSample,
                    PeakImpactEnergyJoules = math.clamp(
                        math.isfinite(_audioPeakImpactEnergyJoules) ? _audioPeakImpactEnergyJoules : 0f,
                        0f,
                        KineticImpactMaximumSafeEnergyJoules),
                    ActiveVoices = activeVoiceCount,
                    VoiceLimit = voiceLimit,
                    ActiveEchoTaps = math.clamp(_workerActiveSonarTapCount, 0, SonarEchoTapCapacity),
                    Flags = flags
                };

                cursor++;
                if (cursor >= GranularTelemetryCapacity)
                    cursor = 0;
                _granularTelemetryCursor = cursor;
            }
            finally
            {
                ReleasePlayerCriticalMutationGuard(guardVault, GranularTelemetryMutationGuardMask);
            }

            if (invalid)
            {
                Interlocked.Exchange(ref _granularTelemetryDumpRequested, 1);
                RecordAudioSynthesisTelemetry(
                    (uint)_granularTelemetryRingHandle.BufferID,
                    AudioSynthesisFailureNonFiniteSample,
                    AudioSynthesisTelemetryFlagNonFiniteSample,
                    activeVoiceCount,
                    voiceLimit,
                    0f);
            }
        }

        private void RecordPrologueTransitionTelemetry(in AudioTransitionState state)
        {
            bool invalid = (state.Flags & AudioTransitionState.FlagNonFiniteGuard) != 0 ||
                           !math.isfinite(state.UniverseVelocityMetersPerSecond) ||
                           !math.isfinite(state.Heat01) ||
                           !math.isfinite(state.LowPassCutoffHz) ||
                           !math.isfinite(state.LfeGain01) ||
                           !math.isfinite(state.GranularStress01) ||
                           !math.isfinite(state.SplashdownGain01) ||
                           !math.isfinite(state.PortalBlend01);
            uint dspFlags = 0u;
            if (invalid)
                dspFlags |= 1u;
            if ((state.Flags & AudioTransitionState.FlagPortalActive) != 0)
                dspFlags |= 4u;
            if ((state.Flags & AudioTransitionState.FlagGranularEnabled) != 0)
                dspFlags |= 8u;
            if ((state.Flags & AudioTransitionState.FlagSplashdown) != 0)
                dspFlags |= 16u;

            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            IDataVault guardVault = null;
            NativeArray<PrologueAudioTransitionTelemetryEntry> prologueTelemetryRing = default;
            try
            {
                if (!TryAcquirePlayerCriticalMutationBuffer(
                        in _prologueTransitionTelemetryRingHandle,
                        BufferID.PlayerCriticalPrologueTransitionTelemetryRing,
                        PrologueTransitionTelemetryCapacity,
                        PrologueTransitionTelemetryMutationGuardMask,
                        out prologueTelemetryRing,
                        out guardVault))
                {
                    RecordAudioSynthesisTelemetry(
                        (uint)_prologueTransitionTelemetryRingHandle.BufferID,
                        AudioSynthesisFailureTelemetryLock,
                        AudioSynthesisTelemetryFlagLockContention,
                        Volatile.Read(ref _lastActiveDspVoiceCount),
                        _targetGranularMaxVoiceCount,
                        0f);
                    return;
                }

                int cursor = _prologueTransitionTelemetryCursor;
                if ((uint)cursor >= (uint)PrologueTransitionTelemetryCapacity)
                    cursor = 0;

                prologueTelemetryRing[cursor] = new PrologueAudioTransitionTelemetryEntry
                {
                    Frame = state.Frame,
                    Sequence = state.Sequence,
                    UniverseVelocityMetersPerSecond = state.UniverseVelocityMetersPerSecond,
                    Heat01 = state.Heat01,
                    LowPassCutoffHz = state.LowPassCutoffHz,
                    LfeGain01 = state.LfeGain01,
                    GranularStress01 = state.GranularStress01,
                    SplashdownGain01 = state.SplashdownGain01,
                    PortalBlend01 = state.PortalBlend01,
                    AudioLowPassCutoffHz = _audioPrologueLowPassCutoffHertz,
                    SplashdownSamplesRemaining = _prologueSplashdownRemainingSamples,
                    Stage = state.Stage,
                    Flags = state.Flags,
                    QualityTier = state.QualityTier,
                    DspFlags = dspFlags
                };

                cursor++;
                if (cursor >= PrologueTransitionTelemetryCapacity)
                    cursor = 0;
                _prologueTransitionTelemetryCursor = cursor;
            }
            finally
            {
                ReleasePlayerCriticalMutationGuard(guardVault, PrologueTransitionTelemetryMutationGuardMask);
            }

            if (invalid)
            {
                Interlocked.Exchange(ref _prologueTransitionTelemetryDumpRequested, 1);
                RecordAudioSynthesisTelemetry(
                    (uint)_prologueTransitionTelemetryRingHandle.BufferID,
                    AudioSynthesisFailureNonFiniteSample,
                    AudioSynthesisTelemetryFlagNonFiniteSample,
                    Volatile.Read(ref _lastActiveDspVoiceCount),
                    _targetGranularMaxVoiceCount,
                    0f);
            }
        }

        private void FlushGranularTelemetryDumpRequest()
        {
            if (Interlocked.Exchange(ref _granularTelemetryDumpRequested, 0) == 0)
                return;

            DumpGranularTelemetryCold();
        }

        private void FlushPrologueTransitionTelemetryDumpRequest()
        {
            if (Interlocked.Exchange(ref _prologueTransitionTelemetryDumpRequested, 0) == 0)
                return;

            DumpPrologueTransitionTelemetryCold();
        }

        private void FlushAudioSynthesisTelemetryDumpRequest()
        {
            if (Interlocked.Exchange(ref _audioSynthesisTelemetryDumpRequested, 0) == 0)
                return;

            DumpAudioSynthesisTelemetryCold();
        }

        private void DumpGranularTelemetryCold()
        {
            if (!TryReadGranularTelemetryRing(out _))
                return;

            if (Volatile.Read(ref _granularTelemetryDumped) != 0)
                return;

            try
            {
                bool wrote =
                    TryWriteGranularTelemetryDumpCold("Docs/AgentLogs/Dump_PROCEDURAL_SYNTH.h8dump") &
                    TryWriteGranularTelemetryDumpCold("Docs/AgentLogs/Dump_PROCEDURAL_SYNTH.bin") &
                    TryWriteGranularTelemetryDumpCold("Docs/AgentLogs/Dump_STRUCTURAL_ACOUSTICS_LEAD.bin") &
                    TryWriteGranularTelemetryDumpCold("Docs/AgentLogs/Dump_ACOUSTIC_REFLECTION_MAPPER.bin") &
                    TryWriteGranularTelemetryDumpCold("Docs/AgentLogs/Dump_KINETIC_IMPACT_ACOUSTICS.bin") &
                    TryWriteGranularTelemetryDumpCold("Docs/AgentLogs/Dump_SHINOBU_351.bin");
                if (wrote)
                    Interlocked.Exchange(ref _granularTelemetryDumped, 1);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private bool TryWriteGranularTelemetryDumpCold(string path)
        {
            const int HeaderBytes = 8;
            const int RowBytes = 44;
            if (!TryReadGranularTelemetryRing(out NativeArray<GranularAudioTelemetryEntry>.ReadOnly granularTelemetryRing) ||
                !granularTelemetryRing.IsCreated ||
                granularTelemetryRing.Length <= 0)
            {
                return false;
            }

            NativeArray<byte> payload = default;
            try
            {
                int byteCount = HeaderBytes + granularTelemetryRing.Length * RowBytes;
                const string dumpPayloadLabel = "GranularAudioTelemetryDumpPayload";
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(PlayerCriticalProceduralAudioRenderer),
                    dumpPayloadLabel,
                    NativeArrayOptions.ClearMemory);
                int offset = 0;

                WriteInt32LittleEndian(payload, ref offset, GranularTelemetryCapacity);
                WriteInt32LittleEndian(payload, ref offset, _granularTelemetryCursor);

                for (int i = 0; i < granularTelemetryRing.Length; i++)
                {
                    GranularAudioTelemetryEntry entry = granularTelemetryRing[i];
                    WriteUInt32LittleEndian(payload, ref offset, entry.SampleIndex);
                    WriteFloatLittleEndian(payload, ref offset, entry.Stress01);
                    WriteFloatLittleEndian(payload, ref offset, entry.StressDerivative01);
                    WriteFloatLittleEndian(payload, ref offset, entry.Depth01);
                    WriteFloatLittleEndian(payload, ref offset, entry.Impact01);
                    WriteFloatLittleEndian(payload, ref offset, entry.MixedSample);
                    WriteFloatLittleEndian(payload, ref offset, entry.PeakImpactEnergyJoules);
                    WriteInt32LittleEndian(payload, ref offset, entry.ActiveVoices);
                    WriteInt32LittleEndian(payload, ref offset, entry.VoiceLimit);
                    WriteInt32LittleEndian(payload, ref offset, entry.ActiveEchoTaps);
                    WriteUInt32LittleEndian(payload, ref offset, entry.Flags);
                }

                return offset == byteCount && NativeFaultDumpWriter.TryWriteAll(path, payload, byteCount);
            }
            finally
            {
                const string dumpPayloadLabel = "GranularAudioTelemetryDumpPayload";
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(PlayerCriticalProceduralAudioRenderer),
                    dumpPayloadLabel);
            }
        }

        private void DumpPrologueTransitionTelemetryCold()
        {
            if (!TryReadPrologueTransitionTelemetryRing(out _))
                return;

            if (Volatile.Read(ref _prologueTransitionTelemetryDumped) != 0)
                return;

            try
            {
                if (TryWritePrologueTransitionTelemetryDumpCold("Docs/AgentLogs/Dump_PROLOGUE_ACOUSTIC_ORCHESTRATOR.bin"))
                    Interlocked.Exchange(ref _prologueTransitionTelemetryDumped, 1);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private bool TryWritePrologueTransitionTelemetryDumpCold(string path)
        {
            const int HeaderBytes = 8;
            const int RowBytes = 52;
            if (!TryReadPrologueTransitionTelemetryRing(out NativeArray<PrologueAudioTransitionTelemetryEntry>.ReadOnly prologueTelemetryRing) ||
                !prologueTelemetryRing.IsCreated ||
                prologueTelemetryRing.Length <= 0)
            {
                return false;
            }

            NativeArray<byte> payload = default;
            try
            {
                int byteCount = HeaderBytes + prologueTelemetryRing.Length * RowBytes;
                const string dumpPayloadLabel = "PrologueTransitionTelemetryDumpPayload";
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(PlayerCriticalProceduralAudioRenderer),
                    dumpPayloadLabel,
                    NativeArrayOptions.ClearMemory);
                int offset = 0;

                WriteInt32LittleEndian(payload, ref offset, PrologueTransitionTelemetryCapacity);
                WriteInt32LittleEndian(payload, ref offset, _prologueTransitionTelemetryCursor);

                for (int i = 0; i < prologueTelemetryRing.Length; i++)
                {
                    PrologueAudioTransitionTelemetryEntry entry = prologueTelemetryRing[i];
                    WriteUInt32LittleEndian(payload, ref offset, entry.Frame);
                    WriteUInt32LittleEndian(payload, ref offset, entry.Sequence);
                    WriteFloatLittleEndian(payload, ref offset, entry.UniverseVelocityMetersPerSecond);
                    WriteFloatLittleEndian(payload, ref offset, entry.Heat01);
                    WriteFloatLittleEndian(payload, ref offset, entry.LowPassCutoffHz);
                    WriteFloatLittleEndian(payload, ref offset, entry.LfeGain01);
                    WriteFloatLittleEndian(payload, ref offset, entry.GranularStress01);
                    WriteFloatLittleEndian(payload, ref offset, entry.SplashdownGain01);
                    WriteFloatLittleEndian(payload, ref offset, entry.PortalBlend01);
                    WriteFloatLittleEndian(payload, ref offset, entry.AudioLowPassCutoffHz);
                    WriteInt32LittleEndian(payload, ref offset, entry.SplashdownSamplesRemaining);
                    WriteByte(payload, ref offset, entry.Stage);
                    WriteByte(payload, ref offset, entry.Flags);
                    WriteByte(payload, ref offset, entry.QualityTier);
                    WriteByte(payload, ref offset, entry.Reserved);
                    WriteUInt32LittleEndian(payload, ref offset, entry.DspFlags);
                }

                return offset == byteCount && NativeFaultDumpWriter.TryWriteAll(path, payload, byteCount);
            }
            finally
            {
                const string dumpPayloadLabel = "PrologueTransitionTelemetryDumpPayload";
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(PlayerCriticalProceduralAudioRenderer),
                    dumpPayloadLabel);
            }
        }

        private void DumpAudioSynthesisTelemetryCold()
        {
            if (!TryReadAudioSynthesisTelemetryRing(out _))
                return;

            if (Volatile.Read(ref _audioSynthesisTelemetryDumped) != 0)
                return;

            try
            {
                if (TryWriteAudioSynthesisTelemetryDumpCold("Docs/AgentLogs/Dump_1320_Synthesis.bin"))
                    Interlocked.Exchange(ref _audioSynthesisTelemetryDumped, 1);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private bool TryWriteAudioSynthesisTelemetryDumpCold(string path)
        {
            const int HeaderBytes = 8;
            const int RowBytes = 56;
            if (!TryReadAudioSynthesisTelemetryRing(out NativeArray<AudioSynthesisTelemetryEntry>.ReadOnly telemetryRing) ||
                !telemetryRing.IsCreated ||
                telemetryRing.Length <= 0)
            {
                return false;
            }

            NativeArray<byte> payload = default;
            try
            {
                int byteCount = HeaderBytes + telemetryRing.Length * RowBytes;
                const string dumpPayloadLabel = "AudioSynthesisTelemetryDumpPayload";
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(PlayerCriticalProceduralAudioRenderer),
                    dumpPayloadLabel,
                    NativeArrayOptions.ClearMemory);
                int offset = 0;

                WriteInt32LittleEndian(payload, ref offset, AudioSynthesisTelemetryCapacity);
                WriteInt32LittleEndian(payload, ref offset, _audioSynthesisTelemetryCursor);

                for (int i = 0; i < telemetryRing.Length; i++)
                {
                    AudioSynthesisTelemetryEntry entry = telemetryRing[i];
                    WriteInt64LittleEndian(payload, ref offset, entry.StopwatchTicks);
                    WriteUInt32LittleEndian(payload, ref offset, entry.Frame);
                    WriteUInt32LittleEndian(payload, ref offset, entry.BufferId);
                    WriteUInt32LittleEndian(payload, ref offset, entry.SystemId);
                    WriteUInt32LittleEndian(payload, ref offset, entry.ExpectedGeneration);
                    WriteUInt32LittleEndian(payload, ref offset, entry.ActualGeneration);
                    WriteUInt32LittleEndian(payload, ref offset, entry.Flags);
                    WriteInt32LittleEndian(payload, ref offset, entry.ActivePolyphony);
                    WriteInt32LittleEndian(payload, ref offset, entry.VoiceLimit);
                    WriteFloatLittleEndian(payload, ref offset, entry.DspMicroseconds);
                    WriteFloatLittleEndian(payload, ref offset, entry.GlobalQualityWeight);
                    WriteInt32LittleEndian(payload, ref offset, entry.FailureCode);
                    WriteInt32LittleEndian(payload, ref offset, entry.UnderrunCount);
                }

                return offset == byteCount && NativeFaultDumpWriter.TryWriteAll(path, payload, byteCount);
            }
            finally
            {
                const string dumpPayloadLabel = "AudioSynthesisTelemetryDumpPayload";
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(PlayerCriticalProceduralAudioRenderer),
                    dumpPayloadLabel);
            }
        }

        private static void WriteByte(NativeArray<byte> payload, ref int offset, byte value)
        {
            payload[offset++] = value;
        }

        private static void WriteInt32LittleEndian(NativeArray<byte> payload, ref int offset, int value)
        {
            WriteUInt32LittleEndian(payload, ref offset, unchecked((uint)value));
        }

        private static void WriteInt64LittleEndian(NativeArray<byte> payload, ref int offset, long value)
        {
            WriteUInt64LittleEndian(payload, ref offset, unchecked((ulong)value));
        }

        private static void WriteUInt32LittleEndian(NativeArray<byte> payload, ref int offset, uint value)
        {
            payload[offset++] = (byte)value;
            payload[offset++] = (byte)(value >> 8);
            payload[offset++] = (byte)(value >> 16);
            payload[offset++] = (byte)(value >> 24);
        }

        private static void WriteUInt64LittleEndian(NativeArray<byte> payload, ref int offset, ulong value)
        {
            payload[offset++] = (byte)value;
            payload[offset++] = (byte)(value >> 8);
            payload[offset++] = (byte)(value >> 16);
            payload[offset++] = (byte)(value >> 24);
            payload[offset++] = (byte)(value >> 32);
            payload[offset++] = (byte)(value >> 40);
            payload[offset++] = (byte)(value >> 48);
            payload[offset++] = (byte)(value >> 56);
        }

        private static void WriteFloatLittleEndian(NativeArray<byte> payload, ref int offset, float value)
        {
            WriteUInt32LittleEndian(payload, ref offset, math.asuint(value));
        }

        private static bool HasGranularVoiceBuffers(ref GranularVoiceVaultViews views)
        {
            return views.MetallicGrainBank.IsCreated &&
                   views.MetallicGrainBank.Length > HullCreakMinimumGrainSamples &&
                   views.VoiceActive.IsCreated &&
                   views.VoiceActive.Length >= GranularVoiceCapacity &&
                   views.VoiceElapsed.IsCreated &&
                   views.VoiceElapsed.Length >= GranularVoiceCapacity &&
                   views.VoiceLength.IsCreated &&
                   views.VoiceLength.Length >= GranularVoiceCapacity &&
                   views.VoiceStart.IsCreated &&
                   views.VoiceStart.Length >= GranularVoiceCapacity &&
                   views.VoiceSeed.IsCreated &&
                   views.VoiceSeed.Length >= GranularVoiceCapacity &&
                   views.VoiceCursor.IsCreated &&
                   views.VoiceCursor.Length >= GranularVoiceCapacity &&
                   views.VoicePlaybackRate.IsCreated &&
                   views.VoicePlaybackRate.Length >= GranularVoiceCapacity &&
                   views.VoiceGain.IsCreated &&
                   views.VoiceGain.Length >= GranularVoiceCapacity;
        }

        private void ArmGranularVoice(
            ref GranularVoiceVaultViews granularViews,
            NativeArray<float> grainBank,
            int voiceLimit,
            int sampleRate,
            uint seed,
            float stress,
            float stressDerivative,
            float depthParam,
            float impactDrive,
            float accelerationPitchWobble,
            float basePitchScale,
            float grainLengthScale,
            float fmModulationIndex,
            bool highPitchCluster)
        {
            if (!HasGranularVoiceBuffers(ref granularViews) ||
                !grainBank.IsCreated ||
                grainBank.Length <= HullCreakMinimumGrainSamples)
                return;

            NativeArray<int> voiceActive = granularViews.VoiceActive;
            NativeArray<int> voiceElapsed = granularViews.VoiceElapsed;
            NativeArray<int> voiceLength = granularViews.VoiceLength;
            NativeArray<int> voiceStart = granularViews.VoiceStart;
            NativeArray<uint> voiceSeed = granularViews.VoiceSeed;
            NativeArray<float> voiceCursor = granularViews.VoiceCursor;
            NativeArray<float> voicePlaybackRate = granularViews.VoicePlaybackRate;
            NativeArray<float> voiceGain = granularViews.VoiceGain;
            int voiceIndex = ResolveGranularVoiceSlot(
                voiceActive,
                voiceElapsed,
                voiceLength,
                voiceLimit,
                highPrioritySteal: highPitchCluster);
            if (voiceIndex < 0)
                return;

            float safeStress = math.saturate(FiniteOrZero(stress));
            float safeStressDerivative = math.saturate(FiniteOrZero(stressDerivative));
            float safeDepthParam = math.saturate(FiniteOrZero(depthParam));
            float safeImpactDrive = math.saturate(FiniteOrZero(impactDrive));
            float safePitchWobble = math.clamp(FiniteOrDefault(accelerationPitchWobble, 1f), 0.92f, 1.12f);
            float safeBasePitchScale = math.clamp(
                FiniteOrDefault(basePitchScale, 1f),
                GranularTuningBasePitchMinimum,
                GranularTuningBasePitchMaximum);
            float safeGrainLengthScale = math.clamp(
                FiniteOrDefault(grainLengthScale, 1f),
                GranularTuningGrainLengthMinimum,
                GranularTuningGrainLengthMaximum);
            float safeFmModulationIndex = math.clamp(
                FiniteOrDefault(fmModulationIndex, 1f),
                GranularTuningFmModulationMinimum,
                GranularTuningFmModulationMaximum);
            uint lcg = seed == 0u ? 0x9E3779B9u : seed;
            uint durationSeed = NextLcg(lcg);
            uint startSeed = NextLcg(durationSeed);
            uint pitchSeed = NextLcg(startSeed);
            uint gainSeed = NextLcg(pitchSeed);
            int minSamples = math.max(1, (int)(GranularMinimumGrainSeconds * safeGrainLengthScale * sampleRate + 0.5f));
            int maxSamples = math.max(
                minSamples,
                (int)(GranularMaximumGrainSeconds * safeGrainLengthScale * sampleRate + 0.5f));
            int durationRange = math.max(1, maxSamples - minSamples + 1);
            int grainSamples = math.min(
                grainBank.Length - 2,
                minSamples + (int)MapUIntToRange(durationSeed, (uint)durationRange));
            int selectableRange = math.max(1, grainBank.Length - grainSamples - 1);
            int startIndex = (int)MapUIntToRange(startSeed, (uint)selectableRange);
            float depthPitch = math.lerp(1f, 0.52f, safeDepthParam);
            float basePitch = highPitchCluster
                ? math.lerp(1.22f, 1.82f, Hash01(pitchSeed ^ 0xA7C15E31u))
                : math.lerp(0.72f, 1.16f, Hash01(pitchSeed ^ 0xC2B2AE35u));
            float derivativePitch = math.lerp(1f, 1.28f, safeStressDerivative);
            float fmScatter = math.lerp(
                1f,
                math.lerp(0.82f, 1.18f, Hash01(pitchSeed ^ 0x64E62D11u)),
                math.saturate(safeFmModulationIndex * 0.25f));
            float playbackRate = math.clamp(
                basePitch * derivativePitch * depthPitch * safePitchWobble * safeBasePitchScale * fmScatter,
                0.35f,
                2.4f);
            float gain =
                math.lerp(0.06f, 0.28f, safeDepthParam) *
                math.lerp(0.42f, 1f, safeStress) *
                math.lerp(0.75f, 1.65f, math.max(safeStressDerivative, safeImpactDrive)) *
                math.lerp(1f, 1.22f, math.saturate(safeFmModulationIndex * 0.25f)) *
                math.lerp(0.88f, 1.12f, Hash01(gainSeed ^ 0x3D4E91B7u));

            voiceActive[voiceIndex] = 1;
            voiceElapsed[voiceIndex] = 0;
            voiceLength[voiceIndex] = math.max(1, grainSamples);
            voiceStart[voiceIndex] = startIndex;
            voiceSeed[voiceIndex] = lcg;
            voiceCursor[voiceIndex] = 0f;
            voicePlaybackRate[voiceIndex] = playbackRate;
            voiceGain[voiceIndex] = highPitchCluster ? gain * 1.55f : gain;
        }

        private static int ResolveGranularVoiceSlot(
            NativeArray<int> voiceActive,
            NativeArray<int> voiceElapsed,
            NativeArray<int> voiceLength,
            int voiceLimit,
            bool highPrioritySteal)
        {
            int safeLimit = math.clamp(voiceLimit, GranularDisabledVoiceCapacity, GranularVoiceCapacity);
            if (safeLimit <= 0 ||
                !voiceActive.IsCreated ||
                !voiceElapsed.IsCreated ||
                !voiceLength.IsCreated)
            {
                return -1;
            }

            int shortestTailVoiceIndex = -1;
            int shortestTailSamples = int.MaxValue;
            for (int i = 0; i < safeLimit; i++)
            {
                if (voiceActive[i] == 0)
                    return i;

                int elapsed = voiceElapsed[i];
                int tailSamples = math.max(0, math.max(1, voiceLength[i]) - elapsed);
                if (tailSamples < shortestTailSamples)
                {
                    shortestTailSamples = tailSamples;
                    shortestTailVoiceIndex = i;
                }
            }

            if (!highPrioritySteal)
                return -1;

            return shortestTailSamples <= GranularImpactStealTailSamples && shortestTailVoiceIndex >= 0
                ? shortestTailVoiceIndex
                : -1;
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
            float triangle = 1f - (4f * math.abs((float)state.SubBassPhase - 0.5f));
            float amplitude = HullSubBassMaximumGain * math.saturate(depthParam * 0.85f + structuralStress * 0.15f);
            float boostedDepth01 = math.saturate(
                (absoluteDepthMeters - DepthSubwooferBoostStartDepthMeters) *
                DepthSubwooferBoostDepthRangeInv);
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float RenderPressureScrubberHumSample(
            ref HullSynthesisState state,
            float cachedHarmonicGain,
            float pitchScale,
            float outputGain,
            double invSampleRate)
        {
            float gain = math.saturate(outputGain);
            float harmonicGain = math.saturate(cachedHarmonicGain);
            if (gain <= HullNoiseFloor || harmonicGain <= HullNoiseFloor)
                return 0f;

            float frequency = PressureScrubberHumFrequencyHertz * math.clamp(pitchScale, 1f, PressureScrubberHumOxygenPitchMaximumScale);
            float fundamental = AdvanceSine(ref state.PressureScrubberHumPhase, frequency, invSampleRate);
            float second = AdvanceSine(ref state.PressureScrubberHarmonicPhase, frequency * 2f, invSampleRate) * (0.18f + harmonicGain * 0.2f);
            float third = AdvanceSine(ref state.PressureScrubberSaturationPhase, frequency * 3f, invSampleRate) * (0.05f + harmonicGain * 0.13f);
            float cachedDrive = math.lerp(0.62f, 1.28f, harmonicGain);
            return FastSoftClip((fundamental + second + third) * cachedDrive) * gain;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float ApplyDepthHullDistortion(float sample, float depthParam, float structuralStress)
        {
            float depthMeters = depthParam * PressureCreakDepthReferenceMeters;
            float depthBlend = math.saturate(
                (depthMeters - AbyssalHullDistortionStartDepthMeters) *
                AbyssalHullDistortionDepthRangeInv);
            if (depthBlend <= HullNoiseFloor)
                return sample;

            float distortionBlend = depthBlend * math.lerp(0.55f, 1f, math.saturate(structuralStress)) * AbyssalHullDistortionMaximumBlend;
            float drive = math.lerp(1f, AbyssalHullDistortionMaximumDrive, depthBlend);
            float distorted = FastSoftClip(sample * drive);
            return math.lerp(sample, distorted, distortionBlend);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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
                envelope = elapsed * math.rcp((float)math.max(1, state.GrainAttackSamples));
            }
            else if (elapsed < decayEnd)
            {
                float t = (elapsed - attackEnd) * math.rcp((float)math.max(1, state.GrainDecaySamples));
                envelope = math.lerp(1f, state.GrainSustainLevel, t);
            }
            else if (elapsed < sustainEnd)
            {
                envelope = state.GrainSustainLevel;
            }
            else
            {
                float t = (elapsed - sustainEnd) * math.rcp((float)math.max(1, state.GrainReleaseSamples));
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
            int whole = (int)phase;
            phase -= whole;
            if (phase < 0d)
                phase += 1d;
            else if (phase >= 1d)
                phase -= 1d;

            return phase;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float ResolveDescendingNormalized01(float value, float start, float end)
        {
            if (value >= start)
                return 0f;

            if (value <= end)
                return 1f;

            return math.saturate((start - value) * math.rcp(math.max(start - end, 0.0001f)));
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float ResolveAscendingNormalized01(float value, float start, float end)
        {
            if (value <= start)
                return 0f;

            if (value >= end)
                return 1f;

            return math.saturate((value - start) * math.rcp(math.max(end - start, 0.0001f)));
        }

        private static float AdvanceSine(ref double phase, double frequencyHz, double invSampleRate)
        {
            return FastSine01((float)AdvancePhase(ref phase, frequencyHz, invSampleRate));
        }

        private static float AdvanceTriangle01(ref double phase, double frequencyHz, double invSampleRate)
        {
            float phase01 = (float)AdvancePhase(ref phase, frequencyHz, invSampleRate);
            return 1f - math.abs((phase01 * 2f) - 1f);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float FastSineRadians(float radians)
        {
            return FastSine01(radians * InvTwoPi);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float FastCosineRadians(float radians)
        {
            return FastSine01(radians * InvTwoPi + 0.25f);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float FastSine01(float phase01)
        {
            int whole = (int)phase01;
            float phase = phase01 - whole;
            if (phase < 0f)
                phase += 1f;
            else if (phase >= 1f)
                phase -= 1f;

            float centered = phase > 0.5f ? phase - 1f : phase;
            float wave = (4f * centered) - (8f * centered * math.abs(centered));
            return wave + 0.225f * ((wave * math.abs(wave)) - wave);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float FastSoftClip(float value)
        {
            float square = value * value;
            return math.clamp(value * (27f + square) * math.rcp(27f + 9f * square), -1f, 1f);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float ApplyPanicGranularMasterJitter(float sample, uint sampleIndex, float panic01)
        {
            float panic = math.saturate(panic01);
            if (panic <= HullNoiseFloor)
                return sample;

            float heldJitter = HeldNoise(sampleIndex, PanicGranularJitterShift, 0x46D3B2A1u);
            float airNoise = HighBandNoise(sampleIndex ^ 0x9F3E57C1u);
            float amplitudeJitter = 1f + heldJitter * PanicGranularJitterMaximumGain * panic;
            return FastSoftClip((sample * amplitudeJitter) + airNoise * PanicGranularJitterMaximumNoiseGain * panic);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float ApplyPaulKelletPink(ref ProloguePlasmaSynthesisState state, float white)
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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
            float sampleRateInv = math.rcp(math.max(sampleRate, 1));
            float omega = TwoPi * normalizedCenter * sampleRateInv;
            float sine = FastSineRadians(omega);
            float cosine = FastCosineRadians(omega);
            float alpha = sine * (0.5f * math.rcp(math.max(0.01f, q)));
            float inverseA0 = math.rcp(math.max(0.0001f, 1f + alpha));

            b0 = alpha * inverseA0;
            b1 = 0f;
            b2 = -alpha * inverseA0;
            a1 = (-2f * cosine) * inverseA0;
            a2 = (1f - alpha) * inverseA0;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float LinearSampleGrainWindow(
            NativeArray<float> buffer,
            int grainStartIndex,
            int grainLength,
            float cursor)
        {
            if (!buffer.IsCreated || buffer.Length <= 0 || grainLength <= 0)
                return 0f;

            int safeLength = math.min(math.max(1, grainLength), buffer.Length);
            if (!math.isfinite(cursor))
                cursor = 0f;

            int baseIndex = (int)cursor;
            float t = cursor - baseIndex;
            if (t < 0f)
            {
                baseIndex--;
                t += 1f;
            }

            if (baseIndex < 0)
            {
                baseIndex = 0;
                t = 0f;
            }
            else if (baseIndex >= safeLength)
            {
                baseIndex = safeLength - 1;
                t = 0f;
            }

            int nextIndex = baseIndex + 1;
            if (nextIndex >= safeLength)
                nextIndex = 0;

            int source0 = WrapGrainSourceIndex(grainStartIndex + baseIndex, buffer.Length);
            int source1 = WrapGrainSourceIndex(grainStartIndex + nextIndex, buffer.Length);
            return math.lerp(buffer[source0], buffer[source1], t);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float HermiteSampleGrainWindow(
            NativeArray<float> buffer,
            int grainStartIndex,
            int grainLength,
            float cursor)
        {
            if (!buffer.IsCreated || buffer.Length <= 0 || grainLength <= 0)
                return 0f;

            int safeLength = math.min(math.max(1, grainLength), buffer.Length);
            if (!math.isfinite(cursor))
                cursor = 0f;

            int baseIndex = (int)cursor;
            float t = cursor - baseIndex;
            if (t < 0f)
            {
                baseIndex--;
                t += 1f;
            }

            if (baseIndex < 0)
            {
                baseIndex = 0;
                t = 0f;
            }
            else if (baseIndex >= safeLength)
            {
                baseIndex = safeLength - 1;
                t = 0f;
            }

            int prevIndex = WrapLocalGrainIndex(baseIndex - 1, safeLength);
            int nextIndex = WrapLocalGrainIndex(baseIndex + 1, safeLength);
            int nextNextIndex = WrapLocalGrainIndex(baseIndex + 2, safeLength);
            float p0 = buffer[WrapGrainSourceIndex(grainStartIndex + prevIndex, buffer.Length)];
            float p1 = buffer[WrapGrainSourceIndex(grainStartIndex + baseIndex, buffer.Length)];
            float p2 = buffer[WrapGrainSourceIndex(grainStartIndex + nextIndex, buffer.Length)];
            float p3 = buffer[WrapGrainSourceIndex(grainStartIndex + nextNextIndex, buffer.Length)];
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (
                (2f * p1) +
                ((p2 - p0) * t) +
                (((2f * p0) - (5f * p1) + (4f * p2) - p3) * t2) +
                (((-p0) + (3f * p1) - (3f * p2) + p3) * t3));
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float ResolveLinearGrainEnvelope(int elapsed, int length)
        {
            if (length <= 1)
                return 1f;

            float phase = math.saturate(elapsed * math.rcp((float)(length - 1)));
            return math.saturate(1f - math.abs((phase * 2f) - 1f));
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float ResolveParabolicGrainEnvelope(int elapsed, int length)
        {
            if (length <= 1)
                return 1f;

            float phase = math.saturate(elapsed * math.rcp((float)(length - 1)));
            float x = (phase * 2f) - 1f;
            return math.saturate(1f - (x * x));
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static uint NextLcg(uint state)
        {
            unchecked
            {
                return (state * 1664525u) + 1013904223u;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static uint MapUIntToRange(uint value, uint range)
        {
            return range == 0u
                ? 0u
                : (uint)(((ulong)value * range) >> 32);
        }

        private static int WrapGrainSourceIndex(int index, int bufferLength)
        {
            if (bufferLength <= 0)
                return 0;

            if (index < 0)
                return 0;
            if (index >= bufferLength)
                return bufferLength - 1;
            return index;
        }

        private static int WrapLocalGrainIndex(int index, int length)
        {
            if (length <= 1)
                return 0;
            if (index < 0)
                return length - 1;
            if (index >= length)
                return math.min(index - length, length - 1);
            return index;
        }

        private static float LinearSampleLoopWindow(
            NativeArray<float> buffer,
            int loopStartIndex,
            int loopLength,
            double cursor)
        {
            if (!buffer.IsCreated || buffer.Length <= 0 || loopLength <= 0)
                return 0f;

            int baseIndex = (int)cursor;
            float t = (float)(cursor - baseIndex);
            if (t < 0f)
            {
                baseIndex--;
                t += 1f;
            }

            int wrappedBase = baseIndex % loopLength;
            if (wrappedBase < 0)
                wrappedBase += loopLength;

            int wrappedNext = wrappedBase + 1;
            if (wrappedNext >= loopLength)
                wrappedNext = 0;

            int source0 = WrapGrainSourceIndex(loopStartIndex + wrappedBase, buffer.Length);
            int source1 = WrapGrainSourceIndex(loopStartIndex + wrappedNext, buffer.Length);
            float x0 = buffer[source0];
            float x1 = buffer[source1];
            return math.lerp(x0, x1, t);
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float SampleSimplex01(float position, uint seed)
        {
            float seedX = (seed & 0xFFFFu) * 0.00006103515625f;
            float seedY = ((seed >> 16) & 0xFFFFu) * 0.00006103515625f;
            float simplex = noise.snoise(new float2(position + seedX * 31.17f, seedY * 17.93f));
            return math.saturate(simplex * 0.5f + 0.5f);
        }

        private static AudioTransitionState SanitizePrologueAudioTransition(
            in AudioTransitionState state,
            out bool invalid)
        {
            invalid =
                !math.isfinite(state.UniverseVelocityMetersPerSecond) ||
                !math.isfinite(state.Heat01) ||
                !math.isfinite(state.LowPassCutoffHz) ||
                !math.isfinite(state.LfeGain01) ||
                !math.isfinite(state.GranularStress01) ||
                !math.isfinite(state.SplashdownGain01) ||
                !math.isfinite(state.PortalBlend01) ||
                !math.isfinite(state.AbsoluteTimeSeconds);

            AudioTransitionState sanitized = state;
            sanitized.UniverseVelocityMetersPerSecond = math.isfinite(state.UniverseVelocityMetersPerSecond)
                ? math.max(0f, state.UniverseVelocityMetersPerSecond)
                : 0f;
            sanitized.Heat01 = math.isfinite(state.Heat01) ? math.saturate(state.Heat01) : 0f;
            sanitized.LowPassCutoffHz = math.clamp(
                math.isfinite(state.LowPassCutoffHz) ? state.LowPassCutoffHz : PrologueClosedLowPassHertz,
                AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                PrologueOpenLowPassHertz);
            sanitized.LfeGain01 = math.isfinite(state.LfeGain01) ? math.saturate(state.LfeGain01) : 0f;
            sanitized.GranularStress01 = math.isfinite(state.GranularStress01) ? math.saturate(state.GranularStress01) : 0f;
            sanitized.SplashdownGain01 = math.isfinite(state.SplashdownGain01) ? math.saturate(state.SplashdownGain01) : 0f;
            sanitized.PortalBlend01 = math.isfinite(state.PortalBlend01) ? math.saturate(state.PortalBlend01) : 0f;
            if (sanitized.Stage < AudioTransitionState.StageSpace || sanitized.Stage > AudioTransitionState.StageOceanHandoff)
            {
                sanitized.Stage = AudioTransitionState.StageSpace;
                invalid = true;
            }

            if (invalid)
                sanitized.Flags = (byte)(sanitized.Flags | AudioTransitionState.FlagNonFiniteGuard);

            return sanitized;
        }

        private void DrainPrologueTransitionQueue()
        {
            int guard = PrologueTransitionQueueCapacity;
            while (guard-- > 0 &&
                   TryDequeuePrologueTransitionState(out AudioTransitionState state))
            {
                ApplyPrologueTransitionState(in state);
                RecordPrologueTransitionTelemetry(in state);
            }
        }

        private bool TryDequeuePrologueTransitionState(out AudioTransitionState state)
        {
            state = default;
            if (_prologueTransitionQueueCount <= 0)
                return false;

            IDataVault guardVault = null;
            if (!TryAcquirePlayerCriticalMutationBuffer(
                    in _prologueTransitionRingHandle,
                    PlayerCriticalPrologueTransitionRingBufferId,
                    PrologueTransitionQueueCapacity,
                    PrologueTransitionRingMutationGuardMask,
                    out NativeArray<AudioTransitionState> prologueTransitionRing,
                    out guardVault))
            {
                return false;
            }

            try
            {
                if (_prologueTransitionQueueCount <= 0)
                    return false;

                if (!TryReadRing(prologueTransitionRing, ref _prologueTransitionReadIndex, _prologueTransitionQueueCount, PrologueTransitionQueueCapacity, out state))
                {
                    _prologueTransitionQueueCount = 0;
                    _prologueTransitionReadIndex = 0;
                    _prologueTransitionWriteIndex = 0;
                    return false;
                }

                _prologueTransitionQueueCount = math.max(0, _prologueTransitionQueueCount - 1);
                if (_prologueTransitionQueueCount <= 0)
                {
                    _prologueTransitionQueueCount = 0;
                    _prologueTransitionReadIndex = 0;
                    _prologueTransitionWriteIndex = 0;
                }

                return true;
            }
            finally
            {
                ReleasePlayerCriticalMutationGuard(guardVault, PrologueTransitionRingMutationGuardMask);
            }
        }

        private void PrewarmPrologueTransitionQueue()
        {
            if (!_audioTransitionStateLayoutValid)
                return;

            IDataVault guardVault = null;
            if (!TryAcquirePlayerCriticalMutationBuffer(
                    in _prologueTransitionRingHandle,
                    PlayerCriticalPrologueTransitionRingBufferId,
                    PrologueTransitionQueueCapacity,
                    PrologueTransitionRingMutationGuardMask,
                    out NativeArray<AudioTransitionState> prologueTransitionRing,
                    out guardVault))
            {
                return;
            }

            try
            {
                ClearRing(prologueTransitionRing, PrologueTransitionQueueCapacity);
                _prologueTransitionReadIndex = 0;
                _prologueTransitionWriteIndex = 0;
                _prologueTransitionQueueCount = 0;
            }
            finally
            {
                ReleasePlayerCriticalMutationGuard(guardVault, PrologueTransitionRingMutationGuardMask);
            }
        }

        private void WarmPrologueSplashdownBurstProbeCold()
        {
            if (!TryAcquireFrameScratchViews(1, out FrameScratchVaultViews frameViews))
                return;

            try
            {
                if (!frameViews.MixScratch.IsCreated ||
                    frameViews.MixScratch.Length <= 0)
                {
                    return;
                }

                var job = new PrologueSplashdownSineSweepProbeJob
                {
                    Output = frameViews.MixScratch,
                    NormalizedTime = 0.5f
                };
                job.Execute();
                frameViews.MixScratch[0] = 0f;
            }
            finally
            {
                ReleaseFrameScratchMutationGuard(ref frameViews);
            }
        }

        private void ApplyPrologueTransitionState(in AudioTransitionState state)
        {
            _targetPrologueLowPassCutoffHertz = math.clamp(
                state.LowPassCutoffHz,
                AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                PrologueOpenLowPassHertz);
            _targetPrologueLfeGain = math.saturate(state.LfeGain01);
            _targetPrologueGranularStress = math.saturate(state.GranularStress01);
            _targetPrologueSplashdownGain = math.saturate(state.SplashdownGain01);
            _targetProloguePortalBlend01 = math.saturate(state.PortalBlend01);
            _targetPrologueStage = state.Stage;
            _targetPrologueFlags = state.Flags;
            if ((state.Flags & AudioTransitionState.FlagSplashdown) != 0)
                _targetPrologueSplashdownSequence = unchecked((int)state.Sequence);
        }

        private void PublishAudioParameterSnapshot()
        {
            DrainPrologueTransitionQueue();

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
                ReverbAcousticDensity01 = _targetReverbAcousticDensity01,
                ReverbDspTier = _targetReverbDspTier,
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
                EardrumRuptureTinnitus = _targetEardrumRuptureTinnitusValue,
                NarcosisChorus01 = _targetNarcosisChorusValue,
                LeviathanRoarAggro = _targetLeviathanRoarAggroValue,
                LeviathanRoarPitchScale = _targetLeviathanRoarPitchScale,
                HeartbeatActive = _targetHeartbeatActive,
                GranularMaxVoiceCount = _targetGranularMaxVoiceCount,
                GranularBasePitchScale = _targetGranularBasePitchScale,
                GranularGrainLengthScale = _targetGranularGrainLengthScale,
                GranularOverlapDensityScale = _targetGranularOverlapDensityScale,
                GranularFmModulationIndex = _targetGranularFmModulationIndex,
                BinauralAzimuthRadians = _targetBinauralAzimuthRadians,
                BinauralRightDot = _targetBinauralRightDot,
                BinauralItdSeconds = _targetBinauralItdSeconds,
                BinauralShadowAmount01 = _targetBinauralShadowAmount01,
                BinauralShadowCutoffHertz = _targetBinauralShadowCutoffHertz,
                BinauralEnergy01 = _targetBinauralEnergy01,
                BinauralWaterDensityMul = _targetBinauralWaterDensityMul,
                BinauralValid = _targetBinauralValid,
                PrologueLowPassCutoffHz = _targetPrologueLowPassCutoffHertz,
                PrologueLfeGain = _targetPrologueLfeGain,
                PrologueGranularStress = _targetPrologueGranularStress,
                PrologueSplashdownGain = _targetPrologueSplashdownGain,
                ProloguePortalBlend01 = _targetProloguePortalBlend01,
                PrologueSplashdownSequence = unchecked((uint)_targetPrologueSplashdownSequence),
                PrologueStage = _targetPrologueStage,
                PrologueFlags = _targetPrologueFlags,
                GlobalQualityWeight = SanitizeQuality01(_cachedAudioQualityWeight01)
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
            return (state & 0x00FFFFFFu) * 0.000000119209296f - 1f;
        }

        private static float ResolveImpactPitchJitter(uint seed)
        {
            uint state = seed == 0u ? 0x6E624EB7u : seed;
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return math.lerp(0.8f, 1.2f, (state & 0x00FFFFFFu) * 0.000000059604648f);
        }

        private static float HashSigned(uint value)
        {
            value = HashUInt(value);
            return (value & 0x00FFFFFFu) * 0.000000119209296f - 1f;
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
            ringBufferCapacityFrames = AudioFrameSpscRingBuffer.ResolvePowerOfTwoCapacity(ringBufferCapacityFrames);
            RebuildAcousticOcclusionLayerMask();
        }
#endif
    }
}
