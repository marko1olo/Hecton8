// ============================================================================
// HECTON-8 - AcousticZoneController.cs
// Manages acoustic-zone transitions between open water and dry base interiors.
//
// ARCHITECTURE:
//   - Singleton, ITickable: checks player zone state each tick.
//   - Edge detection: transitions only when wet/dry state changes.
//   - AudioMixerSnapshot.TransitionTo: smooth preset crossfade.
//   - SpatialAudioManager: transition sounds for drain/fill cues.
//
// PILLAR 1 - TECHNICAL COMFORT:
//   Inside base: silence, generator hum, metal footsteps.
//   Outside: low-frequency pressure, bubbling, depth echo.
//   Contrast is authored through AudioMixer snapshots:
//     UnderwaterSnapshot: master low-pass, large reverb, muted highs.
//     BaseInteriorSnapshot: clear mids, small metallic reverb, machine hum.
//
// INTEGRATION:
//   - Reads buoyancy air/dry-zone state through IBuoyancyAirStateReadModel.
//   - IsInAir = true: player is inside a dry module.
//   - IsInAir = false: player is in water.
//   - Lazily resolves the player through GameBootstrapper.
//
// TRANSITION FLOW:
//   FixedTick: IBuoyancyAirStateReadModel state changes
//     -> Tick: AcousticZoneController detects edge
//       -> snapshot.TransitionTo(transitionDuration)
//       -> SpatialAudioManager.PlayStatic2D(transitionClip)
//       -> SignalBus<AcousticZoneChangedEvent>.TryPushTracked(isInterior)
//
// ZERO GC:
//   - Tick: one bool comparison plus edge detection. Zero allocation.
//   - TransitionTo: Unity internal path.
//   - PlayStatic2D: SpatialAudioManager 2D voice pool.
//   - Lazy player resolve through GameBootstrapper.
//   - No Update, coroutines, or LINQ.
//
// CPU COST:
//   ~0.0001ms per Tick (one bool read + comparison).
//   Transition itself is handled by Unity AudioMixer internally.
// ============================================================================

using System.Collections.Generic;
using System;
using System.Runtime.InteropServices;
using Hecton8.Atmosphere;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Visor;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Audio;
using BuoyancyObject = global::Hecton8.Physics.BuoyancyObject;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace Hecton8.Audio
{
    /// <summary>
    /// Typed SignalBus facade for acoustic-zone transitions.
    /// </summary>
    public static class AcousticZoneEvents
    {
        private static int s_x001DirectSignalPushDropCount_AcousticZoneController;

        private const uint FloodMuffleLaneHash = 0x464C4D46u; // FLMF
        private const int FloodMuffleSignalCapacity = 32;
        private static bool _floodMuffleInitialized;

        /// <summary>Number of acoustic-zone payloads visible in the current typed-lane snapshot.</summary>
        public static int PendingCount => SignalBus<AcousticZoneChangedEvent>.SnapshotCount;

        /// <summary>Number of flood-muffle payloads visible in the current typed-lane snapshot.</summary>
        public static int FloodMufflePendingCount => SignalBus<HabitatFloodAcousticMuffleSignal>.SnapshotCount;

        internal static int DroppedZoneChangeCount => SignalBus<AcousticZoneChangedEvent>.DroppedLastFlush;
        internal static int DroppedListenerRegistrationCount => 0;
        internal static int ListenerExceptionCount => 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStaticState()
        {
            AcousticZoneController.ResetActiveRuntimeInstanceForSubsystemRegistration();
            _floodMuffleInitialized = false;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal static void ResetForSmokeTest()
        {
            ResetStaticState();
            EnsureInitialized();
        }
#endif

        /// <summary>Queues one acoustic-zone transition.</summary>
        /// <param name="payload">Zone transition payload.</param>
        public static bool TryRaise(in AcousticZoneChangedEvent payload)
        {
            EnsureInitialized();
            return SignalBus<AcousticZoneChangedEvent>.TryPushTracked(in payload, ref s_x001DirectSignalPushDropCount_AcousticZoneController);
        }

        [Obsolete("Acoustic zone producers must use TryRaise so bounded SignalBus rejection is visible.", true)]
        public static void Raise(in AcousticZoneChangedEvent payload)
        {
            TryRaise(in payload);
        }

        /// <summary>Queues one habitat-flood acoustic muffle scalar payload.</summary>
        public static bool TryRaiseFloodMuffle(in HabitatFloodAcousticMuffleSignal payload)
        {
            EnsureFloodMuffleInitialized();
            return SignalBus<HabitatFloodAcousticMuffleSignal>.TryPushTracked(in payload, ref s_x001DirectSignalPushDropCount_AcousticZoneController);
        }

        [Obsolete("Flood muffle producers must use TryRaiseFloodMuffle so bounded SignalBus rejection is visible.", true)]
        public static void RaiseFloodMuffle(in HabitatFloodAcousticMuffleSignal payload)
        {
            TryRaiseFloodMuffle(in payload);
        }

        public static void EnsureInitialized()
        {
            SignalCorridorRuntime.EnsureInitialized();
            SignalBus<AcousticZoneChangedEvent>.EnsureInitialized();
            EnsureFloodMuffleInitialized();
        }

        public static void EnsureFloodMuffleInitialized()
        {
            if (_floodMuffleInitialized)
                return;

            SignalBus<HabitatFloodAcousticMuffleSignal>.Configure(
                HabitatFloodAcousticMuffleSignal.ExpectedCapacity,
                maxFrameSignals: HabitatFloodAcousticMuffleSignal.MaxFrameSignals,
                lowTierFrameSignals: HabitatFloodAcousticMuffleSignal.LowTierFrameSignals,
                laneHash: HabitatFloodAcousticMuffleSignal.LaneHash);
            SignalBus<HabitatFloodAcousticMuffleSignal>.EnsureInitialized();
            _floodMuffleInitialized = true;
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4000)] // Posle FluidEngine (-5000), do bolshinstva sistem
    public sealed class AcousticZoneController : MonoBehaviour, ITickable, IUpdatable, ILateFrameTickable, ISoundscapeEventListener, IPhysicsImpactEventListener, ISonarPingEventListener, IAtmosphereStateEventListener, IToolAcousticCueService, IAcousticZoneReadModel, IAcousticZoneMadnessCueSink, IGlobalRegistryHotSwapListener
    {
        private const float TwoPi = 6.28318530718f;
        private const float InvTwoPi = 0.15915494309f;
        private const string SurfaceSoundscapeTierLabel = "Surface";
        private const string ShallowSoundscapeTierLabel = "Shallow";
        private const string TwilightSoundscapeTierLabel = "Twilight";
        private const string DarknessSoundscapeTierLabel = "Darkness";
        private const string AbyssSoundscapeTierLabel = "Abyss";
        private const string DeepAbyssSoundscapeTierLabel = "DeepAbyss";
        private const string ThermalSoundscapeTierLabel = "Thermal";

#if UNITY_EDITOR
        private const string DefaultWaterDrainSoundPath = "Assets/_Project/Audio/Movement/swimming -onwater.wav";
        private const string DefaultWaterFillSoundPath = "Assets/_Project/Audio/Movement/swimming - underwater.ogg";
        private const string DefaultMasterMixerPath = "Assets/_Project/MasterMixer.mixer";
        private const string DefaultStormStaticPrimaryPath = "Assets/_Project/Audio/Music for Game/shelf_6_Decaying Analog Static.ogg";
        private const string DefaultStormStaticSecondaryPath = "Assets/_Project/Audio/Music for Game/shelf_7_Decaying Analog Static.ogg";
#endif
        private const string AcousticLowPassCutoffParameterDefault = "AcousticLowPassCutoffHz";
        private const string AcousticLowPassResonanceParameterDefault = "AcousticLowPassResonanceQ";
        private const string AcousticReverbDecayParameterDefault = "AcousticReverbDecayTime";
        private const string AcousticReflectionsLevelParameterDefault = "AcousticReverbReflectionsLevelDb";
        private const string AcousticReverbLevelParameterDefault = "AcousticReverbLevelDb";
        private const string AcousticRoomHighFrequencyParameterDefault = "AcousticRoomHighFrequencyDb";
        private const string AcousticDryLevelParameterDefault = "AcousticDryLevelDb";
        private const int AcousticEmitterSampleCapacity = 24;
        private const float AcousticEmitterOcclusionMaxDistanceMeters = 48f;
        private const float AcousticEmitterDistanceWeightScale = 0.05f;
        private const float AmbientSourceResolveRetryInterval = 0.5f;
        private const int AudioServiceResolveRetryFrames = 30;

        private enum AcousticZoneState : byte
        {
            Surface = 0,
            Underwater = 1,
            Interior = 2
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct AcousticGraphState
        {
            [FieldOffset(0)] public float LowPassCutoffHz;
            [FieldOffset(4)] public float LowPassResonanceQ;
            [FieldOffset(8)] public float ReverbDecayTime;
            [FieldOffset(12)] public float ReflectionsLevelDb;
            [FieldOffset(16)] public float ReverbLevelDb;
            [FieldOffset(20)] public float RoomHighFrequencyDb;
            [FieldOffset(24)] public float DryLevelDb;
            [FieldOffset(28)] public float Arm64AlignmentPad0;
        }

        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        private static AcousticZoneController s_activeRuntimeInstance;

        public static AcousticZoneController Instance => s_activeRuntimeInstance;

        internal static void ResetActiveRuntimeInstanceForSubsystemRegistration()
        {
            s_activeRuntimeInstance = null;
        }

        // ══════════════════════════════════════════════════════════
        //  GLOBAL EVENT — ACOUSTIC ZONE CHANGE
        // ══════════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — SNAPSHOTS
        // ══════════════════════════════════════════════════════════

        [Header("AudioMixer Snapshots")]
        [Tooltip("Underwater snapshot. Expected tuning: low-pass filter, large-hall reverb, muted highs, reinforced lows.")]
        [SerializeField] private AudioMixerSnapshot underwaterSnapshot;

        [Tooltip("Base interior snapshot. Expected tuning: LPF removed, small-room reverb, clean mids, light mechanical hum.")]
        [SerializeField] private AudioMixerSnapshot baseInteriorSnapshot;

        [Tooltip("Optional MasterMixer asset used to auto-resolve authored snapshot refs by name in cold path/editor.")]
        [SerializeField] private AudioMixer masterMixer;

        [SerializeField] private AudioMixerSnapshot surfaceSnapshot;
        [SerializeField] private AudioMixerSnapshot surfaceRainSnapshot;
        [SerializeField] private AudioMixerSnapshot surfaceStormSnapshot;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — TRANSITION
        // ══════════════════════════════════════════════════════════

        [Header("Transition Settings")]
        [Tooltip("Snapshot transition duration in seconds. 2.0 is a smooth water-drain crossfade; 0.5 is a fast test transition.")]
        [SerializeField] private float transitionDuration = 2.0f;

        [Tooltip("Transition duration into base interior. Controls dry-zone LPF and reverb response separately.")]
        [SerializeField] private float interiorTransitionDuration = 2.0f;

        [Tooltip("Transition duration to the default surface snapshot without weather blending.")]
        [SerializeField] private float surfaceTransitionDuration = 2.0f;

        [Tooltip("Transition duration when entering water. Usually faster than drainage.")]
        [SerializeField] private float underwaterTransitionDuration = 1.5f;

        [Tooltip("Weather blend duration for Surface/Rain/Storm snapshots.")]
        [SerializeField] private float surfaceWeatherTransitionDuration = 1.0f;

        [Tooltip("Rain snapshot weight in the surface weather mix.")]
        [SerializeField, Range(0f, 1f)] private float surfaceRainSnapshotWeight = 0.55f;

        [Tooltip("Storm snapshot weight in the surface weather mix.")]
        [SerializeField, Range(0f, 1f)] private float surfaceStormSnapshotWeight = 0.8f;

        [Header("Exterior State Stability")]
        [Tooltip("Depth threshold for entering the underwater acoustic state. Kept above the visual threshold to prevent surface-chatter.")]
        [SerializeField] private float acousticEnterUnderwaterDepth = SurfaceStateUtility.EnterUnderwaterDepth;

        [Tooltip("Depth threshold for leaving the underwater acoustic state. Must stay below the enter threshold for hysteresis.")]
        [SerializeField] private float acousticExitUnderwaterDepth = SurfaceStateUtility.ExitUnderwaterDepth;
        [SerializeField, Range(0.1f, 1f)] private float acousticEnterImmersionRatio = 0.82f;
        [SerializeField, Range(0.05f, 0.95f)] private float acousticExitImmersionRatio = 0.6f;
        [SerializeField] private float acousticForceUnderwaterDepth = 1.1f;

        [Tooltip("Minimum confirmation time before switching between Surface and Underwater. Interior switches immediately.")]
        [SerializeField] private float exteriorTransitionDebounce = 0.35f;

        [Tooltip("Minimum hold time after an exterior acoustic transition. Prevents Surface/Underwater chatter near the waterline.")]
        [SerializeField] private float exteriorTransitionHoldTime = 1.25f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — TRANSITION SOUNDS
        // ══════════════════════════════════════════════════════════

        [Header("Transition Audio")]
        [Tooltip("Zvuk otkachki vody (vhod v suhuyu zonu).\n" +
                 "Vosproizvoditsya cherez SpatialAudioManager.PlayStatic2D\n" +
                 "(2D, 'vnutri shlema'). Dlitelnost ~2-3 sekundy.")]
        [SerializeField] private AudioClip waterDrainSound;

        [Tooltip("Zvuk zapolneniya vodoy (vyhod v okean).\n" +
                 "Bulkane + davlenie + shipenie.")]
        [SerializeField] private AudioClip waterFillSound;

        [Tooltip("Gromkost perehodnyh zvukov [0..1].")]
        [SerializeField, Range(0f, 1f)] private float transitionVolume = 0.8f;

        [Header("Storm Interference Audio")]
        [Tooltip("Optional 2D helmet-static pulse used during heavy electrical storms.")]
        [SerializeField] private AudioClip stormStaticPrimary;

        [Tooltip("Optional alternate static pulse so repeated storm interference does not sound identical.")]
        [SerializeField] private AudioClip stormStaticSecondary;

        [Tooltip("Electrical activity required before storm audio interference becomes audible.")]
        [SerializeField, Range(0f, 1f)] private float stormStaticElectricalThreshold = 0.52f;

        [Tooltip("Slowest cadence between static pulses when the storm only barely exceeds the threshold.")]
        [SerializeField, Min(0.1f)] private float stormStaticIntervalMax = 5.2f;

        [Tooltip("Fastest cadence between static pulses during peak electrical activity.")]
        [SerializeField, Min(0.1f)] private float stormStaticIntervalMin = 1.6f;

        [Tooltip("Helmet-static pulse volume when the storm first crosses the interference threshold.")]
        [SerializeField, Range(0f, 1f)] private float stormStaticVolumeMin = 0.08f;

        [Tooltip("Helmet-static pulse volume during peak electrical activity.")]
        [SerializeField, Range(0f, 1f)] private float stormStaticVolumeMax = 0.2f;

        [Tooltip("Volume multiplier for storm static pulses while the player remains underwater.")]
        [SerializeField, Range(0f, 1f)] private float stormStaticUnderwaterVolumeScale = 0.72f;

        [Tooltip("Maximum ducking applied to the underwater ambient loop while storms interfere with the suit audio path.")]
        [SerializeField, Range(0f, 0.5f)] private float stormAmbientDuckMax = 0.18f;

        [Tooltip("Maximum downward pitch shift applied to the underwater ambient loop during heavy electrical storms.")]
        [SerializeField, Range(0f, 0.25f)] private float stormAmbientPitchDropMax = 0.08f;

        [Tooltip("Maximum flutter amplitude layered on the underwater ambient loop pitch during heavy electrical storms.")]
        [SerializeField, Range(0f, 0.15f)] private float stormAmbientPitchFlutterMax = 0.035f;

        [Tooltip("Pitch flutter frequency range floor for underwater storm interference.")]
        [SerializeField, Range(0.1f, 5f)] private float stormAmbientFlutterFrequencyMin = 0.6f;

        [Tooltip("Pitch flutter frequency range ceiling for underwater storm interference.")]
        [SerializeField, Range(0.1f, 8f)] private float stormAmbientFlutterFrequencyMax = 2.1f;

        [Header("Sonar Pulse Audio")]
        [Tooltip("Optional 2D sonar ping one-shot used when the player sends an active sonar pulse.")]
        [SerializeField] private AudioClip sonarPingClip;
        [Tooltip("Minimum sonar ping volume for low-intensity pulses.")]
        [SerializeField, Range(0f, 1f)] private float sonarPingVolumeMin = 0.18f;
        [Tooltip("Maximum sonar ping volume for full-strength active pulses.")]
        [SerializeField, Range(0f, 1f)] private float sonarPingVolumeMax = 0.42f;

        [Header("Manta Misfire Audio")]
        [Tooltip("Optional 2D sputter one-shot used when the handheld Manta drive misfires under hull stress.")]
        [SerializeField] private AudioClip mantaMisfireClip;
        [Tooltip("Minimum misfire sputter volume when the hull only barely exceeds the failure threshold.")]
        [SerializeField, Range(0f, 1f)] private float mantaMisfireVolumeMin = 0.14f;
        [Tooltip("Maximum misfire sputter volume when the hull is near catastrophic stress.")]
        [SerializeField, Range(0f, 1f)] private float mantaMisfireVolumeMax = 0.36f;

        [Header("Fatal Pressure Procedural Stress")]
        [Tooltip("Slowest cadence between procedural hull-stress bursts at sequence start.")]
        [SerializeField, Min(0.05f)] private float fatalPressureStressIntervalMax = 0.38f;
        [Tooltip("Fastest cadence between procedural hull-stress bursts right before implosion.")]
        [SerializeField, Min(0.05f)] private float fatalPressureStressIntervalMin = 0.08f;
        [Tooltip("Minimum stress payload sent into the procedural hull-stress renderer during the fatal-pressure loop.")]
        [SerializeField, Range(0f, 1f)] private float fatalPressureStressMin = 0.55f;
        [Tooltip("Maximum stress payload sent into the procedural hull-stress renderer at the end of the fatal-pressure loop.")]
        [SerializeField, Range(0f, 1f)] private float fatalPressureStressMax = 1f;
        [Tooltip("Lowest procedural hull-stress pitch scale during the fatal-pressure loop.")]
        [SerializeField, Range(0.25f, 2f)] private float fatalPressureStressPitchMin = 0.68f;
        [Tooltip("Highest procedural hull-stress pitch scale during the fatal-pressure loop.")]
        [SerializeField, Range(0.25f, 2f)] private float fatalPressureStressPitchMax = 1.22f;

        [Header("Madness Whisper Audio")]
        [Tooltip("Very low 2D whisper/static cue played once when PDA lore is fully replaced by a madness line.")]
        [SerializeField, Range(0f, 1f)] private float madnessWhisperVolume = 0.045f;
        [Tooltip("Minimum cooldown between madness whisper cues so repeated PDA swaps do not stack into noise spam.")]
        [SerializeField, Min(0.1f)] private float madnessWhisperCooldown = 0.9f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — PLAYER REFERENCE
        // ══════════════════════════════════════════════════════════

        [Header("Player")]
        [Tooltip("Player BuoyancyObject. If unassigned, it is resolved from the Player tag during startup.")]

        [SerializeField] private BuoyancyObject playerBuoyancy; // player acoustic owner ref
        private IBuoyancyAirStateReadModel _playerBuoyancyState;
        private IPlayerRuntimeContext _playerRuntimeContext;

        [Header("Underwater Vegetation Overlay")]
        [Tooltip("Optional 2D ambient pulse used when underwater audio moves through dense sargassum fields.")]
        [SerializeField] private AudioClip underwaterSargassumBubblesClip;
        [Tooltip("Optional 2D ambient pulse used when underwater audio moves through dense grass or kelp fields.")]
        [SerializeField] private AudioClip underwaterGrassRustleClip;
        [Tooltip("Minimum global vegetation audio density before underwater vegetation overlays become audible.")]
        [SerializeField, Range(0f, 1f)] private float underwaterVegetationDensityThreshold = 0.16f;
        [Tooltip("Slowest cadence between underwater vegetation overlay pulses.")]
        [SerializeField, Min(0.1f)] private float underwaterVegetationIntervalMax = 2.4f;
        [Tooltip("Fastest cadence between underwater vegetation overlay pulses at peak density.")]
        [SerializeField, Min(0.1f)] private float underwaterVegetationIntervalMin = 0.7f;
        [Tooltip("Minimum overlay volume once underwater vegetation density crosses the threshold.")]
        [SerializeField, Range(0f, 1f)] private float underwaterVegetationVolumeMin = 0.06f;
        [Tooltip("Maximum overlay volume at peak underwater vegetation density.")]
        [SerializeField, Range(0f, 1f)] private float underwaterVegetationVolumeMax = 0.22f;


        [Tooltip("Optional player underwater ambient loop AudioSource. If unassigned, the controller lazily resolves the first 2D loop/playOnAwake source under the player root.")]
        [SerializeField] private AudioSource playerUnderwaterAmbientSource;
        [Tooltip("Explicit AudioMixerGroup for the player underwater loop source. If null, SpatialAudioManager AmbientGroup is used.")]
        [SerializeField] private AudioMixerGroup playerUnderwaterAmbientMixerGroup;
        [Tooltip("AudioMixer exposed parameter name for low-pass cutoff frequency.")]
        [SerializeField] private string acousticLowPassCutoffParameter = AcousticLowPassCutoffParameterDefault;
        [Tooltip("AudioMixer exposed parameter name for low-pass resonance.")]
        [SerializeField] private string acousticLowPassResonanceParameter = AcousticLowPassResonanceParameterDefault;
        [Tooltip("AudioMixer exposed parameter name for reverb decay.")]
        [SerializeField] private string acousticReverbDecayParameter = AcousticReverbDecayParameterDefault;
        [Tooltip("AudioMixer exposed parameter name for reflections level.")]
        [SerializeField] private string acousticReflectionsLevelParameter = AcousticReflectionsLevelParameterDefault;
        [Tooltip("AudioMixer exposed parameter name for reverb level.")]
        [SerializeField] private string acousticReverbLevelParameter = AcousticReverbLevelParameterDefault;
        [Tooltip("AudioMixer exposed parameter name for room high-frequency level.")]
        [SerializeField] private string acousticRoomHighFrequencyParameter = AcousticRoomHighFrequencyParameterDefault;
        [Tooltip("AudioMixer exposed parameter name for dry level.")]
        [SerializeField] private string acousticDryLevelParameter = AcousticDryLevelParameterDefault;

        [Header("Biome Ambient Response")]
        [Tooltip("Optional BiomeMatrixDirector reference. If unassigned, the controller lazily resolves the runtime owner.")]
        [SerializeField] private BiomeMatrixDirector biomeMatrixDirector;

        [Tooltip("Retry interval for resolving BiomeMatrixDirector in the cold/runtime path.")]
        [SerializeField] private float biomeMatrixResolveRetryInterval = 1f;

        [Tooltip("Underwater loop volume multiplier in calm biomes.")]
        [SerializeField, Range(0.25f, 1.5f)] private float calmAmbientVolumeScale = 0.84f;

        [Tooltip("Underwater loop volume multiplier in lively biomes.")]
        [SerializeField, Range(0.25f, 1.5f)] private float livelyAmbientVolumeScale = 1.05f;

        [Tooltip("Underwater loop volume multiplier in mixed or neutral biomes.")]
        [SerializeField, Range(0.25f, 1.5f)] private float mixedAmbientVolumeScale = 0.94f;

        [Tooltip("Underwater loop volume multiplier in hostile biomes.")]
        [SerializeField, Range(0.25f, 1.5f)] private float hostileAmbientVolumeScale = 0.72f;

        [Tooltip("Underwater loop pitch multiplier in calm biomes.")]
        [SerializeField, Range(0.5f, 1.5f)] private float calmAmbientPitchScale = 1.02f;

        [Tooltip("Underwater loop pitch multiplier in lively biomes.")]
        [SerializeField, Range(0.5f, 1.5f)] private float livelyAmbientPitchScale = 1.01f;

        [Tooltip("Underwater loop pitch multiplier in mixed or neutral biomes.")]
        [SerializeField, Range(0.5f, 1.5f)] private float mixedAmbientPitchScale = 0.96f;

        [Tooltip("Underwater loop pitch multiplier in hostile biomes.")]
        [SerializeField, Range(0.5f, 1.5f)] private float hostileAmbientPitchScale = 0.90f;

        [Header("Soundscape Tier Response")]
        // Existing underwater acoustic owner consumes depth-band context directly.
        [Tooltip("Optional soundscape tier read model. If unassigned, the controller lazily resolves the runtime owner.")]
        [SerializeField] private MonoBehaviour soundscapeSystem;

        [Tooltip("Retry interval for resolving SoundscapeSystem in the cold/runtime path.")]
        [SerializeField] private float soundscapeResolveRetryInterval = 1f;

        [Tooltip("Underwater loop volume multiplier in the shallow tier.")]
        [SerializeField, Range(0.25f, 1.5f)] private float shallowTierAmbientVolumeScale = 1f;

        [Tooltip("Underwater loop volume multiplier in the twilight tier.")]
        [SerializeField, Range(0.25f, 1.5f)] private float twilightTierAmbientVolumeScale = 0.94f;

        [Tooltip("Underwater loop volume multiplier in the darkness tier.")]
        [SerializeField, Range(0.25f, 1.5f)] private float darknessTierAmbientVolumeScale = 0.88f;

        [Tooltip("Underwater loop volume multiplier in the abyss tier.")]
        [SerializeField, Range(0.25f, 1.5f)] private float abyssTierAmbientVolumeScale = 0.82f;

        [Tooltip("Underwater loop volume multiplier in the deep abyss tier.")]
        [SerializeField, Range(0.25f, 1.5f)] private float deepAbyssTierAmbientVolumeScale = 0.74f;

        [Tooltip("Underwater loop volume multiplier in the thermal tier.")]
        [SerializeField, Range(0.25f, 1.5f)] private float thermalTierAmbientVolumeScale = 0.86f;

        [Tooltip("Underwater loop pitch multiplier in the shallow tier.")]
        [SerializeField, Range(0.5f, 1.5f)] private float shallowTierAmbientPitchScale = 1f;

        [Tooltip("Underwater loop pitch multiplier in the twilight tier.")]
        [SerializeField, Range(0.5f, 1.5f)] private float twilightTierAmbientPitchScale = 0.97f;

        [Tooltip("Underwater loop pitch multiplier in the darkness tier.")]
        [SerializeField, Range(0.5f, 1.5f)] private float darknessTierAmbientPitchScale = 0.93f;

        [Tooltip("Underwater loop pitch multiplier in the abyss tier.")]
        [SerializeField, Range(0.5f, 1.5f)] private float abyssTierAmbientPitchScale = 0.88f;

        [Tooltip("Underwater loop pitch multiplier in the deep abyss tier.")]
        [SerializeField, Range(0.5f, 1.5f)] private float deepAbyssTierAmbientPitchScale = 0.82f;

        [Tooltip("Underwater loop pitch multiplier in the thermal tier.")]
        [SerializeField, Range(0.5f, 1.5f)] private float thermalTierAmbientPitchScale = 0.9f;

        [Header("Music Ambient Integration")]
        [Tooltip("Maximum underwater ambient-loop ducking applied while the music director owns the emotional foreground.")]
        [SerializeField, Range(0f, 0.4f)] private float musicAmbientDuckMax = 0.16f;

        [Tooltip("How quickly underwater ambient yields when music activity rises.")]
        [SerializeField, Range(0.25f, 20f)] private float musicAmbientDuckAttackSharpness = 6.5f;

        [Tooltip("How quickly underwater ambient returns after music activity releases.")]
        [SerializeField, Range(0.25f, 20f)] private float musicAmbientDuckReleaseSharpness = 2.4f;

        [Tooltip("Duck weight for exploration phrases. Keep subtle so ocean texture remains readable.")]
        [SerializeField, Range(0f, 1f)] private float explorationMusicAmbientDuckWeight = 0.42f;

        [Tooltip("Duck weight for base music beds.")]
        [SerializeField, Range(0f, 1f)] private float baseMusicAmbientDuckWeight = 0.50f;

        [Tooltip("Duck weight for tense music phrases.")]
        [SerializeField, Range(0f, 1f)] private float tenseMusicAmbientDuckWeight = 0.72f;

        [Tooltip("Duck weight for combat and authored override music.")]
        [SerializeField, Range(0f, 1f)] private float foregroundMusicAmbientDuckWeight = 1f;

        [Header("Listener Fallback Processing")]
        [Tooltip("If mixer snapshot authoring is incomplete, apply listener-level low-pass/reverb fallback so underwater/interior contrast still exists.")]
        [SerializeField] private bool enableSourceLevelAcousticFallback = true;

        [Tooltip("Legacy serialized underwater fallback cutoff retained for inspector compatibility.")]
#pragma warning disable CS0414
        [SerializeField, Range(500f, 22000f)] private float underwaterFallbackLowPassCutoff = 1100f;
#pragma warning restore CS0414

        [Tooltip("Fallback low-pass cutoff for interior listener processing.")]
        [SerializeField, Range(5000f, 22000f)] private float interiorFallbackLowPassCutoff = 16000f;

        [Tooltip("Legacy serialized interior reverb preset retained for inspector compatibility.")]
#pragma warning disable CS0414
        [SerializeField] private AudioReverbPreset interiorFallbackReverbPreset = AudioReverbPreset.Room;
#pragma warning restore CS0414

        [Tooltip("Fallback interior reverb dry level. Exposed so sound design can retune dry/wet balance without code changes.")]
        [SerializeField, Range(-10000f, 0f)] private float interiorFallbackReverbDryLevel = 0f;

        [Header("Runtime Acoustic Graph Fallback")]
        [Tooltip("Continuous low-pass/reverb listener graph used when the authored mixer only contains attenuation.")]
        [SerializeField] private bool enableRuntimeAcousticGraph = true;

        [Tooltip("How quickly runtime fallback filter coefficients chase the target acoustic state.")]
        [SerializeField, Range(0.5f, 20f)] private float acousticGraphFollowSharpness = 7.5f;

        [Tooltip("Decay speed for hull-impact energy injected into the acoustic graph.")]
        [SerializeField, Range(0.5f, 20f)] private float acousticImpactImpulseDecay = 3.6f;

        [Tooltip("Decay speed for active-sonar energy injected into the acoustic graph.")]
        [SerializeField, Range(0.5f, 20f)] private float acousticSonarImpulseDecay = 2.2f;

        [Tooltip("Reference depth used to fully close the underwater low-pass curve.")]
        [SerializeField, Min(1f)] private float acousticDeepWaterReferenceDepth = 240f;

        [Tooltip("Maximum listener low-pass cutoff when underwater but still near the surface.")]
        [SerializeField, Range(500f, 22000f)] private float underwaterGraphShallowCutoff = 1800f;

        [Tooltip("Minimum listener low-pass cutoff when the player is fully committed to the abyss.")]
        [SerializeField, Range(500f, 22000f)] private float underwaterGraphDeepCutoff = 650f;

        [Tooltip("Interior listener low-pass cutoff before collision impulses darken the room tone.")]
        [SerializeField, Range(5000f, 22000f)] private float interiorGraphLowPassCutoff = 15800f;

        [Tooltip("Base resonance used by the underwater low-pass contour.")]
        [SerializeField, Range(0.5f, 3f)] private float underwaterGraphResonance = 1.22f;

        [Tooltip("Base resonance used by the interior low-pass contour.")]
        [SerializeField, Range(0.5f, 3f)] private float interiorGraphResonance = 1.05f;

        [Tooltip("Baseline underwater reverb decay in seconds.")]
        [SerializeField, Range(0.05f, 12f)] private float underwaterGraphDecayTime = 1.35f;

        [Tooltip("Baseline interior reverb decay in seconds.")]
        [SerializeField, Range(0.05f, 12f)] private float interiorGraphDecayTime = 0.95f;

        [Tooltip("Additional interior decay time injected by heavy hull impacts.")]
        [SerializeField, Range(0f, 4f)] private float interiorImpactDecayBoost = 0.65f;

        [Tooltip("How strongly sonar pings temporarily open the underwater low-pass window.")]
        [SerializeField, Range(0f, 1f)] private float sonarGraphOpenUpBoost = 0.35f;

        [Tooltip("How strongly local hull impacts bend the active graph toward metallic ringing.")]
        [SerializeField, Range(0f, 1f)] private float impactGraphMetallicBoost = 0.6f;

        [Tooltip("Maximum distance for feeding a physics impact into the listener acoustic graph.")]
        [SerializeField, Min(0.5f)] private float acousticImpactImpulseRadius = 18f;

        [Tooltip("Underwater reflection level in dB.")]
        [SerializeField, Range(-10000f, 1000f)] private float underwaterGraphReflectionsLevel = -4200f;

        [Tooltip("Interior reflection level in dB.")]
        [SerializeField, Range(-10000f, 1000f)] private float interiorGraphReflectionsLevel = -800f;

        [Tooltip("Underwater late-reverb level in dB.")]
        [SerializeField, Range(-10000f, 2000f)] private float underwaterGraphReverbLevel = -2200f;

        [Tooltip("Interior late-reverb level in dB.")]
        [SerializeField, Range(-10000f, 2000f)] private float interiorGraphReverbLevel = -1200f;

        [Tooltip("Underwater high-frequency room loss in dB.")]
        [SerializeField, Range(-10000f, 0f)] private float underwaterGraphRoomHighFrequency = -6500f;

        [Tooltip("Interior high-frequency room loss in dB.")]
        [SerializeField, Range(-10000f, 0f)] private float interiorGraphRoomHighFrequency = -1450f;

        [Tooltip("Underwater dry level in dB.")]
        [SerializeField, Range(-10000f, 0f)] private float underwaterGraphDryLevel = -800f;

        [Tooltip("Interior dry level in dB.")]
        [SerializeField, Range(-10000f, 0f)] private float interiorGraphDryLevel = -120f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        [Header("Diagnostics")]
#pragma warning disable CS0414
        [SerializeField] private bool _debugIsInterior;
        [SerializeField] private bool _debugIsUnderwater;
        [SerializeField] private bool _debugPlayerFound;
        [SerializeField] private int  _debugTransitionCount;
        [SerializeField] private string _debugFaunaMood;
        [SerializeField] private string _debugAmbientSummary;
        [SerializeField] private string _debugSnapshotCoverage;
        [SerializeField] private string _debugMixerCoverage;
        [SerializeField] private float _debugAmbientVolume;
        [SerializeField] private float _debugAmbientPitch;
        [SerializeField] private float _debugStormInterference;
        [SerializeField] private string _debugSoundscapeTier;
        [SerializeField] private float _debugSoundscapeVolumeScale = 1f;
        [SerializeField] private float _debugSoundscapePitchScale = 1f;
        [SerializeField] private float _debugMusicAmbientDuck;
        [SerializeField] private float _debugAcousticLowPassCutoff = 22000f;
        [SerializeField] private float _debugAcousticReverbDecay = 0f;
        [SerializeField] private float _debugImpactImpulse;
        [SerializeField] private float _debugSonarImpulse;
#pragma warning restore CS0414

        // ══════════════════════════════════════════════════════════
        //  CACHED STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Poslednee izvestnoe sostoyanie: true = interer (suhaya zona).
        /// Ispolzuetsya dlya edge detection. -1-like: pervyy kadr
        /// opredelyaet nachalnoe sostoyanie bez zapuska perehoda.
        /// </summary>
        private AcousticZoneState _lastZone;

        /// <summary>
        /// Flag: nachalnoe sostoyanie uzhe opredeleno.
        /// false = pervyy Tick esche ne proshel.
        /// Predotvraschaet lozhnyy perehod pri starte.
        /// </summary>
        private bool _stateInitialized;

        /// <summary>
        /// Registration tracking dlya GameTickManager.
        /// </summary>
        private bool _registeredToTickManager;
        private bool _registeredLateFrame;
        private bool _serviceRegistered;
        private bool _hotSwapListenerRegistered;
        private IAudioService _cachedAudioService;
        private ISpatialAudioWorldEmitterReadModel _cachedSpatialAudioEmitterReadModel;
        private IPhysicsStateEventService _physicsStateEvents;
        private int _nextAudioServiceResolveFrame;
        private float _nextPlayerResolveTime;
        private HectonMusicDirector _cachedMusicDirector;
        private const float PlayerResolveRetryInterval = 1f;
        private const float SurfaceWeatherStateEpsilon = 0.001f;
        private float _nextBiomeMatrixResolveTime;
        private float _nextSoundscapeResolveTime;
        private ISoundscapeTierReadModel _cachedSoundscapeReadModel;
        private IAtmosphereReadModel _atmosphereReadModel;
        private HectonPlayerMovement _playerMovement;
        private bool _physicsImpactRegistered;
        private bool _fallbackUnderwaterState;
        private bool _acousticUnderwaterState;
        private bool _hasPendingExteriorZone;
        private float _pendingExteriorZoneResolveTime;
        private AcousticZoneState _pendingExteriorZone;
        private float _nextExteriorTransitionAllowedTime;
        private bool _hasCachedExteriorZone;
        private AcousticZoneState _cachedExteriorZone;
        private List<AudioSource> _playerAudioSources;
        private AudioSource _cachedAmbientSource;
        private AudioListener _cachedPlayerAudioListener;
        private Transform _lastAmbientSourceSearchRoot;
        private float _nextAmbientSourceHierarchyResolveTime;
        private bool _ambientSourceDefaultsCaptured;
        private bool _listenerFallbackDefaultsCaptured;
        private float _ambientSourceBaseVolume = 1f;
        private float _ambientSourceBasePitch = 1f;
        private float _listenerLowPassBaseCutoff = 22000f;
        private float _listenerLowPassBaseResonance = 1f;
        private float _listenerReverbBaseDryLevel;
        private float _listenerReverbBaseDecayTime = 1f;
        private float _listenerReverbBaseReflectionsLevel = -10000f;
        private float _listenerReverbBaseReverbLevel = -10000f;
        private float _listenerReverbBaseRoomHighFrequency = 0f;
        private bool _acousticMixerBindingsResolved;
        private bool _acousticMixerBindingsValid;
        private string _resolvedAcousticLowPassCutoffParameter;
        private string _resolvedAcousticLowPassResonanceParameter;
        private string _resolvedAcousticReverbDecayParameter;
        private string _resolvedAcousticReflectionsLevelParameter;
        private string _resolvedAcousticReverbLevelParameter;
        private string _resolvedAcousticRoomHighFrequencyParameter;
        private string _resolvedAcousticDryLevelParameter;
        private bool _warnedMissingAcousticMixerParameters;
        private float _snapshotTransitionLockUntilTime;
        private HectonBiomeMatrixProfile _lastBiomeProfileForAmbient;
        private int _currentAmbientSurvivalPressure;
        private int _currentAmbientRewardPull;
        private string _currentAmbientSummary;
        private float _currentAmbientVolumeScale = 1f;
        private float _currentAmbientPitchScale = 1f;
        private SoundscapeTier _currentSoundscapeTier = SoundscapeTier.Shallow;
        private float _currentSoundscapeVolumeScale = 1f;
        private float _currentSoundscapePitchScale = 1f;
        private float _currentMusicAmbientDuck01;
        private float _surfacePrecipitationIntensity;
        private float _surfaceElectricalActivity;
        private float _stormInterferencePulseTimer;
        private float _stormAmbientInterference;
        private float _stormAmbientFlutterPhase;
        private float _stormAmbientFlutter;
        private bool _stormStaticUsePrimaryNext = true;
        private float _underwaterVegetationPulseTimer;
        private float _fatalPressureStressTimer;
        private float _nextMadnessWhisperTime;
        private bool _snapshotBindingsResolved;
        private bool _warnedMissingInteriorSnapshot;
        private bool _warnedMissingUnderwaterSnapshot;
        private bool _warnedMissingSurfaceSnapshotSet;
        private bool _warnedMissingSnapshotCoverage;
        private bool _warnedIncompleteMixerSnapshotAuthoring;
        private int _validatedMixerSnapshotCount;
        private float _acousticImpactImpulse;
        private float _acousticSonarImpulse;
        private float _currentAcousticLowPassCutoffHz = 22000f;
        private float _currentAcousticLowPassResonanceQ = 1f;
        private float _currentAcousticReverbDecayTime = 0f;
        private float _currentAcousticReflectionsLevelDb = -10000f;
        private float _currentAcousticReverbLevelDb = -10000f;
        private float _currentAcousticRoomHighFrequencyDb = 0f;
        private float _currentAcousticDryLevelDb = 0f;
        private float _lastAppliedAcousticLowPassCutoffHz = float.NaN;
        private float _lastAppliedAcousticLowPassResonanceQ = float.NaN;
        private float _lastAppliedAcousticReverbDecayTime = float.NaN;
        private float _lastAppliedAcousticReflectionsLevelDb = float.NaN;
        private float _lastAppliedAcousticReverbLevelDb = float.NaN;
        private float _lastAppliedAcousticRoomHighFrequencyDb = float.NaN;
        private float _lastAppliedAcousticDryLevelDb = float.NaN;
        private bool _acousticGraphStateInitialized;
        private bool _validatedMixerHasNamedCoverage;
        private bool _validatedMixerHasEffectGraph;
        private bool _usingSourceLevelAcousticFallback;
        private int _resolvedEmitterOcclusionLayerMask;
        private float _emitterOcclusionTransmission01 = 1f;
        private float _emitterOcclusionLowPassCutoffHz = AcousticOcclusionUtility.OpenLowPassCutoffHertz;
        private bool _hasPendingSnapshotTransition;
        private AcousticZoneState _pendingSnapshotZone;
        private float _pendingSnapshotDuration;
        private bool _isLateFramePresentationPhase;
        private bool _hasPendingZonePresentationTransition;
        private AcousticZoneState _pendingZonePresentationTransition;
        private bool _pendingAmbientLoopStateDirty;
        private AcousticZoneState _pendingAmbientLoopZone;
        private bool _pendingSourceLevelGraphDirty;
        private AcousticZoneState _pendingSourceLevelGraphZone;
        private float _pendingSourceLevelGraphDeltaTime;
        private bool _pendingAmbientSourceMixerRoutingDirty;
        private AudioSource _pendingAmbientSourceMixerRoutingSource;
        private AudioMixerGroup _pendingAmbientSourceMixerRoutingGroup;
        private bool _pendingTransitionCueDirty;
        private AudioClip _pendingTransitionCueClip;
        private float _pendingTransitionCueVolume;
        private bool _pendingMadnessWhisperCueDirty;
        private AudioClip _pendingMadnessWhisperCueClip;
        private float _pendingMadnessWhisperCueVolume;
        private bool _pendingStormStaticCueDirty;
        private AudioClip _pendingStormStaticCueClip;
        private float _pendingStormStaticCueVolume;
        private bool _pendingVegetationCueDirty;
        private AudioClip _pendingVegetationCueClip;
        private float _pendingVegetationCueVolume;
        private bool _pendingFatalPressureCueDirty;
        private Vector3 _pendingFatalPressureCuePosition;
        private float _pendingFatalPressureCueStress01;
        private float _pendingFatalPressureCuePitch;
        private bool _pendingSonarPingCueDirty;
        private float _pendingSonarPingCueVolume;
        private bool _pendingMantaMisfireCueDirty;
        private float _pendingMantaMisfireCueVolume;
        private const float AcousticCutoffWriteEpsilonHz = 8f;
        private const float AcousticResonanceWriteEpsilon = 0.01f;
        private const float AcousticDecayWriteEpsilonSeconds = 0.01f;
        private const float AcousticDbWriteEpsilon = 0.1f;
        // COLD ALLOC: AudioMixerSnapshot[3] - surface weather snapshot blend targets - owner: AcousticZoneController
        private readonly AudioMixerSnapshot[] _surfaceBlendSnapshots = new AudioMixerSnapshot[3];
        // COLD ALLOC: float[3] - surface weather snapshot blend weights - owner: AcousticZoneController
        private readonly float[] _surfaceBlendWeights = new float[3];
        private bool _hasActiveResolvedSnapshotState;
        private bool _activeSurfaceBlendState;
        private AcousticZoneState _activeResolvedZone;
        private AudioMixerSnapshot _activeResolvedSnapshot;
        private int _activeSurfaceBlendSnapshotCount;
        // COLD ALLOC: AudioMixerSnapshot[3] - last applied surface weather snapshot blend targets - owner: AcousticZoneController
        private readonly AudioMixerSnapshot[] _activeSurfaceBlendSnapshots = new AudioMixerSnapshot[3];
        // COLD ALLOC: float[3] - last applied surface weather snapshot blend weights - owner: AcousticZoneController
        private readonly float[] _activeSurfaceBlendWeights = new float[3];
        // COLD ALLOC: SpatialAudioActiveEmitterSample[24] - pooled world-emitter acoustic occlusion sample buffer - owner: AcousticZoneController
        private static readonly SpatialAudioActiveEmitterSample[] s_emitterOcclusionSamples =
            new SpatialAudioActiveEmitterSample[AcousticEmitterSampleCapacity];

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// true esli igrok seychas v suhoy zone (interer bazy).
        /// false esli v vode.
        /// </summary>
        public bool IsInterior => _lastZone == AcousticZoneState.Interior;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            // ── Singleton ──
            if (TryAbortForUsableExistingRuntime())
                return;

            _stateInitialized = false;
            _registeredToTickManager = false;
            // COLD ALLOC: List<AudioSource>[32] - reused player-local audio scan buffer - owner: AcousticZoneController
            _playerAudioSources = new List<AudioSource>(32);

#if UNITY_EDITOR
            TryAssignEditorAuthoringDefaults();
#endif
        }

        private void OnEnable()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            TryRegisterService();
            TryRegister();
            TryRegisterLateFrameTick();
            AtmosphereEvents.Register(this);
            SoundscapeEvents.Register(this);
            TryRegisterPhysicsImpactListener();
            SpectrumEvents.RegisterSonarPingListener(this);
            _stormInterferencePulseTimer = 0f;
            _stormAmbientInterference = 0f;
            _stormAmbientFlutterPhase = 0f;
            _stormAmbientFlutter = 0f;
            _stormStaticUsePrimaryNext = true;
            _underwaterVegetationPulseTimer = 0f;
            _fatalPressureStressTimer = 0f;
            _nextMadnessWhisperTime = 0f;
            _acousticImpactImpulse = 0f;
            _acousticSonarImpulse = 0f;
            _resolvedEmitterOcclusionLayerMask = 0;
            _emitterOcclusionTransmission01 = 1f;
            _emitterOcclusionLowPassCutoffHz = AcousticOcclusionUtility.OpenLowPassCutoffHertz;
            ResolveBiomeMatrixDirector(true);
            RefreshSoundscapeTierContext(true);
        }

        private void Start()
        {
            CachePlayerBuoyancyState();
            TryBindPlayerBuoyancyFromCachedContext();

            // Lazy player lookup.
            if (_playerBuoyancyState == null)
            {
                FindPlayerBuoyancyCold(true);
            }

            // Deferred registration.
            if (!_registeredToTickManager)
            {
                TryRegister();
            }

            if (!_registeredLateFrame)
            {
                TryRegisterLateFrameTick();
            }

            if (!_registeredToTickManager)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError(
                    "[AcousticZoneController] GameTickManager not found at Start(). " +
                    "Acoustic transitions will NOT work.", this);
#endif
            }

            ResolvePlayerAmbientSourceCold();
            ResolvePlayerListenerFiltersCold();
            ResolveBiomeMatrixDirector(true);
            RefreshBiomeAmbientContext();
            RefreshSoundscapeTierContext(true);
            EnsureSnapshotBindings();
            RefreshAtmosphereZoneCache();

            // Apply initial snapshot without transition.
            ApplyInitialSnapshot();
        }

        private void OnDisable()
        {
            AtmosphereEvents.Unregister(this);
            SoundscapeEvents.Unregister(this);
            TryUnregisterPhysicsImpactListener();
            SpectrumEvents.UnregisterSonarPingListener(this);
            _stormInterferencePulseTimer = 0f;
            _stormAmbientInterference = 0f;
            _stormAmbientFlutterPhase = 0f;
            _stormAmbientFlutter = 0f;
            _underwaterVegetationPulseTimer = 0f;
            _fatalPressureStressTimer = 0f;
            _nextMadnessWhisperTime = 0f;
            _acousticImpactImpulse = 0f;
            _acousticSonarImpulse = 0f;
            ResetSourceLevelAcousticFallback();
            TryUnregister();
            TryUnregisterLateFrameTick();
            TryUnregisterService();
            TryUnregisterHotSwapListener();
            ClearCachedRegistryServices();
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterLateFrameTick();
            TryUnregisterService();
            AtmosphereEvents.Unregister(this);
            SoundscapeEvents.Unregister(this);
            TryUnregisterPhysicsImpactListener();
            SpectrumEvents.UnregisterSonarPingListener(this);
            ResetSourceLevelAcousticFallback();
            TryUnregisterHotSwapListener();
            ClearCachedRegistryServices();

        }

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
                case GlobalRegistryServiceSlot.SoundscapeRuntime:
                    if (ReferenceEquals(soundscapeSystem, previousService))
                        soundscapeSystem = null;
                    CacheSoundscapeReadModel(currentService as ISoundscapeTierReadModel);
                    RefreshSoundscapeTierContext(true);
                    break;
                case GlobalRegistryServiceSlot.MusicDirectorRuntime:
                    CacheMusicDirector(currentService as HectonMusicDirector);
                    break;
                case GlobalRegistryServiceSlot.AtmosphereRuntime:
                    _atmosphereReadModel = currentService as IAtmosphereReadModel;
                    RefreshAtmosphereZoneCache();
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    if (_playerRuntimeContext == null)
                    {
                        ClearCachedPlayerSceneBindings();
                        break;
                    }

                    TryBindPlayerBuoyancyFromCachedContext();
                    ResolvePlayerAmbientSourceCold();
                    ResolvePlayerListenerFiltersCold();
                    break;
                case GlobalRegistryServiceSlot.PhysicsStateManager:
                    RebindPhysicsStateEventService(currentService as IPhysicsStateEventService);
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregister();
                    TryUnregisterLateFrameTick();
                    if (currentService != null && isActiveAndEnabled)
                    {
                        TryRegister();
                        TryRegisterLateFrameTick();
                    }
                    break;
            }
        }

        private void TryRegister()
        {
            if (_registeredToTickManager || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registeredToTickManager = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);
        }

        private void TryUnregister()
        {
            if (!_registeredToTickManager)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
            _registeredToTickManager = false;
        }

        private void TryRegisterLateFrameTick()
        {
            if (_registeredLateFrame || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterLateFrameTick()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _registeredLateFrame = false;
        }

        public void LateFrameTick()
        {
            // L19 hop2: batchmode/headless probes have no audible presentation path.
            // AudioMixerSnapshot.TransitionTo / TransitionToSnapshots arm FMOD DSP graph
            // work that AVs on the mixer thread (DSPFilter::read / OutputWASAPI::mixerUpdate)
            // under -batchmode. Soft-disable all late-frame acoustic presentation here.
            if (Application.isBatchMode)
                return;

            _isLateFramePresentationPhase = true;
            try
            {
                FlushPendingZonePresentationTransition();
                ProcessPendingSnapshotTransition();

                if (_pendingSourceLevelGraphDirty)
                {
                    _pendingSourceLevelGraphDirty = false;
                    UpdateSourceLevelAcousticGraph(_pendingSourceLevelGraphZone, _pendingSourceLevelGraphDeltaTime);
                    _pendingSourceLevelGraphDeltaTime = 0f;
                }

                if (_pendingAmbientLoopStateDirty)
                {
                    _pendingAmbientLoopStateDirty = false;
                    FlushAmbientLoopState(_pendingAmbientLoopZone);
                }

                FlushPendingAmbientSourceMixerRouting();
                FlushQueuedAcousticCues();
            }
            finally
            {
                _isLateFramePresentationPhase = false;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable.Tick — ACOUSTIC ZONE DETECTION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Checks the player acoustic zone every frame.
        /// Edge detection starts a transition only when the zone changes.
        /// CPU cost is one state read and comparison. No allocations, no complex logic.
        /// Kept on ITickable because SlowTick delay is audible at airlock transitions.
        /// </summary>
        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            if (TryAbortForUsableExistingRuntime())
                return;

            GlobalRegistry.RegisterAcousticZoneRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.AcousticZone, this);
            if (_serviceRegistered)
                s_activeRuntimeInstance = this;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            AcousticZoneController active = s_activeRuntimeInstance;
            if (!ReferenceEquals(active, null) && !ReferenceEquals(active, this))
            {
                if (IsAcousticZoneRuntimeUsable(active))
                {
                    Destroy(gameObject);
                    return true;
                }

                if (ReferenceEquals(s_activeRuntimeInstance, active))
                    s_activeRuntimeInstance = null;
                if (ReferenceEquals(GlobalRegistry.AcousticZone, active))
                    GlobalRegistry.UnregisterAcousticZoneRuntime(active);
            }

            AcousticZoneController registered = GlobalRegistry.AcousticZone;
            if (ReferenceEquals(registered, null) || ReferenceEquals(registered, this))
                return false;

            if (IsAcousticZoneRuntimeUsable(registered))
            {
                s_activeRuntimeInstance = registered;
                Destroy(gameObject);
                return true;
            }

            GlobalRegistry.UnregisterAcousticZoneRuntime(registered);
            if (ReferenceEquals(s_activeRuntimeInstance, registered))
                s_activeRuntimeInstance = null;
            return false;
        }

        private static bool IsAcousticZoneRuntimeUsable(AcousticZoneController controller)
        {
            return controller != null &&
                   controller._serviceRegistered &&
                   controller.isActiveAndEnabled;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterAcousticZoneRuntime(this);
            if (ReferenceEquals(s_activeRuntimeInstance, this))
                s_activeRuntimeInstance = null;
            _serviceRegistered = false;
        }

        private void ClearCachedRegistryServices()
        {
            _cachedAudioService = null;
            _cachedSpatialAudioEmitterReadModel = null;
            _physicsStateEvents = null;
            _cachedSoundscapeReadModel = null;
            _cachedMusicDirector = null;
            _atmosphereReadModel = null;
            ClearCachedPlayerRuntimeContext();
            _currentMusicAmbientDuck01 = 0f;
            _debugMusicAmbientDuck = 0f;
            _nextAudioServiceResolveFrame = 0;
        }

        private void ClearCachedPlayerRuntimeContext()
        {
            _playerRuntimeContext = null;
            ClearCachedPlayerSceneBindings();
        }

        private void ClearCachedPlayerSceneBindings()
        {
            _playerMovement = null;
            _playerBuoyancyState = null;
            playerBuoyancy = null;
            _cachedPlayerAudioListener = null;
            _lastAmbientSourceSearchRoot = null;
            _cachedAmbientSource = null;
            if (_playerAudioSources != null)
                _playerAudioSources.Clear();
        }

        private void CacheRegistryServicesCold()
        {
            CacheAudioService(GlobalRegistry.Audio);
            CacheSoundscapeReadModel(GlobalRegistry.SoundscapeTierReadModel);
            CacheMusicDirector(GlobalRegistry.MusicDirector);
            _atmosphereReadModel = GlobalRegistry.AtmosphereReadModel;
            _physicsStateEvents = GlobalRegistry.PhysicsStateEvents;
            _playerRuntimeContext = GlobalRegistry.Player;
            TryBindPlayerBuoyancyFromCachedContext();
        }

        private void CacheAudioService(IAudioService audioService)
        {
            if (!IsAudioServiceUsable(audioService))
            {
                _cachedAudioService = null;
                _cachedSpatialAudioEmitterReadModel = null;
                _nextAudioServiceResolveFrame = 0;
                return;
            }

            _cachedAudioService = audioService;
            _cachedSpatialAudioEmitterReadModel = audioService as ISpatialAudioWorldEmitterReadModel;
            _nextAudioServiceResolveFrame = 0;
        }

        private void CacheSoundscapeReadModel(ISoundscapeTierReadModel runtime)
        {
            _cachedSoundscapeReadModel = runtime;
            _nextSoundscapeResolveTime = 0f;
        }

        private void CacheMusicDirector(HectonMusicDirector musicDirector)
        {
            _cachedMusicDirector = musicDirector != null && musicDirector.isActiveAndEnabled ? musicDirector : null;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private void TryRegisterPhysicsImpactListener()
        {
            if (_physicsImpactRegistered)
                return;

            RebindPhysicsStateEventService(_physicsStateEvents ?? GlobalRegistry.PhysicsStateEvents);
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

            if (_physicsStateEvents == null || !isActiveAndEnabled)
                return;

            _physicsStateEvents.RegisterImpactListener(this);
            _physicsImpactRegistered = true;
        }

        private IAudioService ResolveAudioService()
        {
            int frame = SystemDispatcher.CurrentFrameIndex;
            IAudioService audioService = _cachedAudioService;
            if (IsAudioServiceUsable(audioService))
                return audioService;

            _cachedAudioService = null;
            _cachedSpatialAudioEmitterReadModel = null;

            if (frame < _nextAudioServiceResolveFrame)
                return null;

            _nextAudioServiceResolveFrame = frame + AudioServiceResolveRetryFrames;
            return null;
        }

        private ISpatialAudioWorldEmitterReadModel ResolveSpatialAudioEmitterReadModel()
        {
            IAudioService audioService = ResolveAudioService();
            if (audioService == null)
                return null;

            ISpatialAudioWorldEmitterReadModel spatialAudioReadModel = _cachedSpatialAudioEmitterReadModel;
            if (spatialAudioReadModel != null)
                return spatialAudioReadModel;

            spatialAudioReadModel = audioService as ISpatialAudioWorldEmitterReadModel;
            _cachedSpatialAudioEmitterReadModel = spatialAudioReadModel;
            return spatialAudioReadModel;
        }

        private static bool IsAudioServiceUsable(IAudioService audioService)
        {
            if (audioService == null || !audioService.IsInitialized)
                return false;

            if (audioService is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        public void Tick(float deltaTime)
        {
            if (!HasValidPlayerBuoyancyState())
            {
                TryBindPlayerBuoyancyFromCachedContext();
                if (!HasValidPlayerBuoyancyState())
                {
                    UpdateMusicAmbientDucking(AcousticZoneState.Surface, deltaTime);
                    return;
                }
            }

            // Current acoustic zone.
            AcousticZoneState currentZone = ResolveCurrentZone();
            currentZone = ResolveStableZone(currentZone);
            RefreshBiomeAmbientContext();
            RefreshSoundscapeTierContext(false);
            UpdateMusicAmbientDucking(currentZone, deltaTime);
            UpdateStormInterferenceAudio(currentZone, deltaTime);
            QueueAmbientLoopState(currentZone);
            UpdateUnderwaterVegetationOverlay(currentZone, deltaTime);
            UpdateFatalPressureStressAudio(currentZone, deltaTime);
            QueueSourceLevelAcousticGraph(currentZone, deltaTime);

            // First frame: establish initial state without transition.
            if (!_stateInitialized)
            {
                ApplyInitialSnapshot(currentZone);
                return;
            }

            // Edge detection: transition only when the zone changes.
            if (currentZone == _lastZone)
                return;

            // ══════════════════════════════════════════════
            //  TRANSITION DETECTED!
            // ══════════════════════════════════════════════

            _lastZone = currentZone;
            if (currentZone != AcousticZoneState.Interior)
                _nextExteriorTransitionAllowedTime = ResolvePresentationClockSeconds() + exteriorTransitionHoldTime;

            ApplyZoneTransition(currentZone);

            // Notify external systems.
            AcousticZoneEvents.TryRaise(new AcousticZoneChangedEvent(currentZone == AcousticZoneState.Interior));

            UpdateDiagnostics(currentZone);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — TRANSITIONS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Smooth transition into the base interior.
        ///
        /// AudioMixerSnapshot.TransitionTo(timeToReach):
        ///   Moves AudioMixer toward the snapshot over the requested duration.
        ///   Unity interpolates mixer parameters internally.
        ///   Includes volume, LPF cutoff, reverb wet, and related mixer parameters.
        ///   Zero GC — nativnaya operatsiya.
        ///
        /// Transition sound (waterDrainSound):
        ///   Played through PlayStatic2D as an in-helmet 2D sound.
        ///   Simulates water draining from the airlock.
        ///   Clip length should roughly match transitionDuration.
        /// </summary>
        private void TransitionToInterior()
        {
            ApplyAmbientLoopState(AcousticZoneState.Interior);
            if (!TransitionToResolvedSnapshot(AcousticZoneState.Interior, interiorTransitionDuration))
                return;

            // ── Perehodnyy zvuk ──
            PlayTransitionSound(waterDrainSound);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogDiagnostic("[AcousticZoneController] Interior transition.");
#endif
        }

        /// <summary>
        /// Smooth transition into the underwater environment.
        ///
        /// underwaterTransitionDuration can be shorter than transitionDuration
        /// because water-fill reads faster than drainage.
        /// This gives the audio transition an asymmetric feel:
        ///   Into base: 2.0s slow drain.
        ///   Into water: 1.5s fast fill.
        /// </summary>
        private void TransitionToSurface()
        {
            ApplyAmbientLoopState(AcousticZoneState.Surface);
            if (!TransitionToResolvedSnapshot(AcousticZoneState.Surface, surfaceTransitionDuration))
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogDiagnostic("[AcousticZoneController] Surface transition.");
#endif
        }

        private void TransitionToUnderwater()
        {
            ApplyAmbientLoopState(AcousticZoneState.Underwater);
            if (!TransitionToResolvedSnapshot(AcousticZoneState.Underwater, underwaterTransitionDuration))
                return;

            // ── Perehodnyy zvuk ──
            PlayTransitionSound(waterFillSound);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogDiagnostic("[AcousticZoneController] Underwater transition.");
#endif
        }

        /// <summary>
        /// Applies the initial snapshot immediately.
        /// Called from Start() to establish the correct initial state.
        ///
        /// TransitionTo(0f) — mgnovennoe pereklyuchenie (Unity podderzhivaet 0).
        /// </summary>
        private void ApplyInitialSnapshot()
        {
            if (!HasValidPlayerBuoyancyState()) return;

            ApplyInitialSnapshot(ResolveCurrentZone());
        }

        private void ApplyInitialSnapshot(AcousticZoneState zone)
        {
            _lastZone = zone;
            _stateInitialized = true;
            _hasPendingExteriorZone = false;
            _nextExteriorTransitionAllowedTime = 0f;
            QueueInitialSnapshotPresentation(zone);

            UpdateDiagnostics(zone);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogDiagnostic("[AcousticZoneController] Initial zone resolved.");
#endif
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — TRANSITION SOUND
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Vosproizvodit perehodnyy zvuk cherez SpatialAudioManager.
        /// 2D (PlayStatic2D) — zvuk "vnutri shlema", ne pozitsionnyy.
        ///
        /// Null-safe dlya clip i SpatialAudioManager.
        /// </summary>
        private void PlayTransitionSound(AudioClip clip)
        {
            if (clip == null)
                return;

            QueueTransitionCue(clip, transitionVolume);
        }

        public void PlayMadnessWhisperCue()
        {
            if (ResolvePresentationClockSeconds() < _nextMadnessWhisperTime)
                return;

            AudioClip clip = stormStaticPrimary;
            if (clip == null)
                clip = stormStaticSecondary;

            if (clip == null)
                return;

            QueueMadnessWhisperCue(clip, madnessWhisperVolume);
            _nextMadnessWhisperTime = ResolvePresentationClockSeconds() + math.max(0.1f, madnessWhisperCooldown);
        }

        private AudioMixerSnapshot ResolveSurfaceSnapshot()
        {
            EnsureSnapshotBindings();

            if (!HasAnyResolvedSnapshotCoverage())
                return null;

            if (_surfaceElectricalActivity >= 0.55f && surfaceStormSnapshot != null)
                return surfaceStormSnapshot;

            if (_surfacePrecipitationIntensity >= 0.2f && surfaceRainSnapshot != null)
                return surfaceRainSnapshot;

            if (surfaceSnapshot != null)
                return surfaceSnapshot;

            if (baseInteriorSnapshot != null)
            {
                LogSnapshotFallbackWarningOnce(
                    ref _warnedMissingSurfaceSnapshotSet,
                    "[AcousticZoneController] Surface snapshot set missing. Falling back to BaseInteriorSnapshot. Author Surface/SurfaceRain/SurfaceStorm snapshots in MasterMixer.");
                return baseInteriorSnapshot;
            }

            if (underwaterSnapshot != null)
            {
                LogSnapshotFallbackWarningOnce(
                    ref _warnedMissingSurfaceSnapshotSet,
                    "[AcousticZoneController] Surface snapshot set missing. Falling back to UnderwaterSnapshot because no dry/exterior snapshot is authored.");
                return underwaterSnapshot;
            }

            LogSnapshotFallbackWarningOnce(
                ref _warnedMissingSurfaceSnapshotSet,
                "[AcousticZoneController] Surface snapshot set missing and no fallback snapshot exists. Surface acoustic transitions will keep the previous mixer state.");
            return null;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — PLAYER LOOKUP
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Cold-resolves the current player BuoyancyObject through GameBootstrapper.
        /// Called from startup or explicit player rebinding, never from Tick.
        ///
        /// TryGetComponent is zero GC and restricted to this cold path.
        /// </summary>
        private void FindPlayerBuoyancyCold(bool force)
        {
            if (!force && ResolvePresentationClockSeconds() < _nextPlayerResolveTime)
                return;

            _nextPlayerResolveTime = ResolvePresentationClockSeconds() + PlayerResolveRetryInterval;

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            Transform playerTransform = playerContext != null ? playerContext.PlayerTransform : null;
            if (playerTransform != null)
            {
                playerBuoyancy = playerContext.PlayerBuoyancyAirState as BuoyancyObject;
                _playerMovement = playerContext.PlayerMovement;
                CachePlayerBuoyancyState();
            }
            else if (force)
            {
                ClearCachedPlayerSceneBindings();
            }

            UpdatePlayerFoundDiagnostic();
        }

        private void CachePlayerBuoyancyState()
        {
            _playerBuoyancyState = playerBuoyancy as IBuoyancyAirStateReadModel;
        }

        private bool TryBindPlayerBuoyancyFromCachedContext()
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext == null)
            {
                ClearCachedPlayerSceneBindings();
                return false;
            }

            ClearCachedPlayerSceneBindings();
            _playerMovement = playerContext.PlayerMovement;

            IBuoyancyAirStateReadModel airState = playerContext.PlayerBuoyancyAirState;
            if (!IsValidUnityBackedReadModel(airState))
            {
                UpdatePlayerFoundDiagnostic();
                return false;
            }

            _playerBuoyancyState = airState;
            playerBuoyancy = airState as BuoyancyObject;
            UpdatePlayerFoundDiagnostic();
            return true;
        }

        private bool HasValidPlayerBuoyancyState()
        {
            if (IsValidUnityBackedReadModel(_playerBuoyancyState))
                return true;

            _playerBuoyancyState = null;
            playerBuoyancy = null;
            return false;
        }

        private static bool IsValidUnityBackedReadModel(IBuoyancyAirStateReadModel readModel)
        {
            if (readModel == null)
                return false;

            UnityEngine.Object unityObject = readModel as UnityEngine.Object;
            return unityObject == null ? !(readModel is UnityEngine.Object) : true;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — MANUAL CONTROL
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Prinuditelnyy perehod v ukazannuyu zonu.
        /// Ispolzuetsya iz vneshnih sistem (skriptovye stseny, chity, testy).
        ///
        /// Primer: GlobalRegistry.AcousticZone?.ForceZone(true); // Interior
        /// </summary>
        /// <param name="isInterior">true = interer, false = podvodnaya.</param>
        public void ForceZone(bool isInterior)
        {
            AcousticZoneState forcedZone = isInterior
                ? AcousticZoneState.Interior
                : AcousticZoneState.Underwater;

            if (forcedZone == _lastZone && _stateInitialized)
                return; // Uzhe v nuzhnoy zone

            _lastZone = forcedZone;
            _stateInitialized = true;
            _hasPendingExteriorZone = false;
            _nextExteriorTransitionAllowedTime = forcedZone == AcousticZoneState.Interior
                ? 0f
                : ResolvePresentationClockSeconds() + exteriorTransitionHoldTime;

            ApplyZoneTransition(forcedZone);

            AcousticZoneEvents.TryRaise(new AcousticZoneChangedEvent(isInterior));
            UpdateDiagnostics(forcedZone);
        }

        /// <summary>
        /// Ustanavlivaet BuoyancyObject igroka v rantayme.
        /// Vyzyvaetsya pri respavne igroka ili smene kontrollera.
        /// </summary>
        public void SetPlayerBuoyancy(BuoyancyObject buoyancy)
        {
            playerBuoyancy = buoyancy;
            CachePlayerBuoyancyState();
            _playerMovement = null;
            playerUnderwaterAmbientSource = null;
            _cachedPlayerAudioListener = null;
            _cachedAmbientSource = null;
            _lastAmbientSourceSearchRoot = null;
            _nextAmbientSourceHierarchyResolveTime = 0f;
            _listenerFallbackDefaultsCaptured = false;
            _acousticMixerBindingsResolved = false;
            _acousticMixerBindingsValid = false;
            InvalidateAppliedAcousticMixerStateCache();
            _snapshotTransitionLockUntilTime = 0f;
            _hasPendingSnapshotTransition = false;
            _pendingSnapshotDuration = 0f;
            _hasPendingZonePresentationTransition = false;
            _hasPendingExteriorZone = false;
            if (buoyancy != null)
            {
                buoyancy.TryGetComponent(out _playerMovement);
                ResolvePlayerAmbientSourceCold(buoyancy.transform);
                TryRegisterLateFrameTick();
            }
            _stateInitialized = false; // Pereinitsializatsiya pri sleduyuschem Tick
            UpdatePlayerFoundDiagnostic();
        }

        // ══════════════════════════════════════════════════════════
        //  DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateDiagnostics(AcousticZoneState zone)
        {
            _debugIsInterior = zone == AcousticZoneState.Interior;
            _debugIsUnderwater = zone == AcousticZoneState.Underwater;
            _debugTransitionCount++;
            _debugFaunaMood = ResolveAmbientMoodLabel();
            _debugAmbientSummary = string.IsNullOrWhiteSpace(_currentAmbientSummary) ? "None" : _currentAmbientSummary;
            _debugSnapshotCoverage = BuildSnapshotCoverageSummary();
            _debugMixerCoverage = BuildMixerCoverageSummary();
            float musicAmbientDuckScale = zone == AcousticZoneState.Underwater ? ResolveMusicAmbientDuckVolumeScale() : 1f;
            _debugAmbientVolume = _ambientSourceBaseVolume * _currentAmbientVolumeScale * _currentSoundscapeVolumeScale * musicAmbientDuckScale;
            _debugAmbientPitch = _ambientSourceBasePitch * _currentAmbientPitchScale * _currentSoundscapePitchScale;
            _debugSoundscapeTier = ResolveSoundscapeTierLabel(_currentSoundscapeTier);
            _debugSoundscapeVolumeScale = _currentSoundscapeVolumeScale;
            _debugSoundscapePitchScale = _currentSoundscapePitchScale;
            _debugMusicAmbientDuck = _currentMusicAmbientDuck01;
            _debugAcousticLowPassCutoff = _currentAcousticLowPassCutoffHz;
            _debugAcousticReverbDecay = _currentAcousticReverbDecayTime;
            _debugImpactImpulse = _acousticImpactImpulse;
            _debugSonarImpulse = _acousticSonarImpulse;
            if (_usingSourceLevelAcousticFallback)
                _debugMixerCoverage += " | MixerParamFallback";
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdatePlayerFoundDiagnostic()
        {
            _debugPlayerFound = _playerBuoyancyState != null;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogDiagnostic(string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.Log(message, this);
#endif
        }

        private AcousticZoneState ResolveCurrentZone()
        {
            if (_playerBuoyancyState != null && _playerBuoyancyState.IsInDryZone)
                return AcousticZoneState.Interior;

            bool hasMovementState = TryResolvePlayerMovementRuntimeState(out _);
            if (hasMovementState || HasPlayerRuntimeContext())
            {
                _acousticUnderwaterState = ResolveMovementDrivenExteriorState(null);

                return _acousticUnderwaterState
                    ? AcousticZoneState.Underwater
                    : AcousticZoneState.Surface;
            }

            HectonPlayerMovement movement = ResolvePlayerMovement();
            if (movement != null)
            {
                _acousticUnderwaterState = ResolveMovementDrivenExteriorState(movement);

                return _acousticUnderwaterState
                    ? AcousticZoneState.Underwater
                    : AcousticZoneState.Surface;
            }

            if (_hasCachedExteriorZone)
                return _cachedExteriorZone;

            IAtmosphereReadModel atmosphere = ResolveAtmosphereReadModel();
            if (atmosphere != null)
            {
                AcousticZoneState zone = atmosphere.IsUnderwaterState
                    ? AcousticZoneState.Underwater
                    : AcousticZoneState.Surface;
                _cachedExteriorZone = zone;
                _hasCachedExteriorZone = true;
                return zone;
            }

            _fallbackUnderwaterState =
                SurfaceStateUtility.ResolveUnderwaterFromDepth(
                    ResolvePlayerDepthFallback(),
                    _fallbackUnderwaterState,
                    acousticEnterUnderwaterDepth,
                    acousticExitUnderwaterDepth);

            return _fallbackUnderwaterState
                ? AcousticZoneState.Underwater
                : AcousticZoneState.Surface;
        }

        private bool ResolveMovementDrivenExteriorState(HectonPlayerMovement movement)
        {
            bool hasMovementState = TryResolvePlayerMovementRuntimeState(out PlayerMovementRuntimeState movementState);
            if (!hasMovementState && HasPlayerRuntimeContext())
                return false;

            float depth = hasMovementState
                ? math.max(0f, movementState.DepthMeters)
                : ResolvePlayerDepthFallback();
            float immersion = hasMovementState
                ? ((movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.Underwater) != 0u ? 1f : 0f)
                : (movement != null ? math.saturate(movement.WaterImmersionRatio) : (_acousticUnderwaterState ? 1f : 0f));
            bool headSubmerged = hasMovementState
                ? (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.Underwater) != 0u || depth > 0f
                : movement != null && (movement.IsPlayerSubmerged || depth > 0f);

            if (headSubmerged || depth >= acousticForceUnderwaterDepth)
                return true;

            if (_acousticUnderwaterState)
            {
                if (immersion <= acousticExitImmersionRatio && depth <= acousticExitUnderwaterDepth)
                    return false;

                return depth > acousticExitUnderwaterDepth || immersion > acousticExitImmersionRatio;
            }

            if (depth < acousticEnterUnderwaterDepth)
                return false;

            return immersion >= acousticEnterImmersionRatio;
        }

        private AcousticZoneState ResolveStableZone(AcousticZoneState candidateZone)
        {
            if (!_stateInitialized)
                return candidateZone;

            if (candidateZone == AcousticZoneState.Interior || _lastZone == AcousticZoneState.Interior)
            {
                _hasPendingExteriorZone = false;
                return candidateZone;
            }

            if (candidateZone == _lastZone)
            {
                _hasPendingExteriorZone = false;
                return candidateZone;
            }

            float now = ResolvePresentationClockSeconds();
            if (now < _nextExteriorTransitionAllowedTime)
            {
                _hasPendingExteriorZone = false;
                return _lastZone;
            }

            if (!_hasPendingExteriorZone || _pendingExteriorZone != candidateZone)
            {
                _pendingExteriorZone = candidateZone;
                _pendingExteriorZoneResolveTime = now + exteriorTransitionDebounce;
                _hasPendingExteriorZone = true;
                return _lastZone;
            }

            if (now < _pendingExteriorZoneResolveTime)
                return _lastZone;

            _hasPendingExteriorZone = false;
            return candidateZone;
        }

        private float ResolvePlayerDepthFallback()
        {
            if (TryResolvePlayerMovementRuntimeState(out PlayerMovementRuntimeState movementState))
                return math.max(0f, movementState.DepthMeters);

            if (HasPlayerRuntimeContext())
                return 0f;

            HectonPlayerMovement movement = ResolvePlayerMovement();
            if (movement != null && math.isfinite(movement.CurrentDepth))
                return math.max(0f, movement.CurrentDepth);

            IAtmosphereReadModel atmosphere = ResolveAtmosphereReadModel();
            if (atmosphere != null && atmosphere.IsUnderwaterState)
                return acousticEnterUnderwaterDepth;

            return 0f;
        }

        private void HandleAtmosphereStateChanged(EnvironmentState state)
        {
            _cachedExteriorZone = state == EnvironmentState.UNDERWATER
                ? AcousticZoneState.Underwater
                : AcousticZoneState.Surface;
            _hasCachedExteriorZone = true;
        }

        void IAtmosphereStateEventListener.OnAtmosphereStateChanged(EnvironmentState state)
        {
            HandleAtmosphereStateChanged(state);
        }

        private void ResolveBiomeMatrixDirector(bool force)
        {
            if (biomeMatrixDirector != null)
                return;

            float currentTime = ResolvePresentationClockSeconds();
            if (!force && currentTime < _nextBiomeMatrixResolveTime)
                return;

            WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref biomeMatrixDirector);
            _nextBiomeMatrixResolveTime = currentTime + biomeMatrixResolveRetryInterval;
        }

        private ISoundscapeTierReadModel ResolveSoundscapeReadModel(bool force)
        {
            if (soundscapeSystem is ISoundscapeTierReadModel explicitReadModel)
                return explicitReadModel;

            if (_cachedSoundscapeReadModel != null)
                return _cachedSoundscapeReadModel;

            if (!force)
                return null;

            float currentTime = ResolvePresentationClockSeconds();
            _nextSoundscapeResolveTime = currentTime + soundscapeResolveRetryInterval;
            CacheSoundscapeReadModel(GlobalRegistry.SoundscapeTierReadModel);
            return _cachedSoundscapeReadModel;
        }

        private void RefreshSoundscapeTierContext(bool force)
        {
            ISoundscapeTierReadModel soundscapeReadModel = ResolveSoundscapeReadModel(force);

            SoundscapeTier tier = soundscapeReadModel != null
                ? ResolveSoundscapeTierFromCode(soundscapeReadModel.CurrentTierCode)
                : SoundscapeTier.Shallow;

            ApplySoundscapeTierContext(tier);
        }

        private static SoundscapeTier ResolveSoundscapeTierFromCode(byte tierCode)
        {
            switch (tierCode)
            {
                case 0:
                    return SoundscapeTier.Surface;
                case 1:
                    return SoundscapeTier.Shallow;
                case 2:
                    return SoundscapeTier.Twilight;
                case 3:
                    return SoundscapeTier.Darkness;
                case 4:
                    return SoundscapeTier.Abyss;
                case 5:
                    return SoundscapeTier.DeepAbyss;
                case 6:
                    return SoundscapeTier.Thermal;
                default:
                    return SoundscapeTier.Shallow;
            }
        }

        private void ApplySoundscapeTierContext(SoundscapeTier tier)
        {
            _currentSoundscapeTier = tier;
            _currentSoundscapeVolumeScale = shallowTierAmbientVolumeScale;
            _currentSoundscapePitchScale = shallowTierAmbientPitchScale;

            switch (tier)
            {
                case SoundscapeTier.Twilight:
                    _currentSoundscapeVolumeScale = twilightTierAmbientVolumeScale;
                    _currentSoundscapePitchScale = twilightTierAmbientPitchScale;
                    break;

                case SoundscapeTier.Darkness:
                    _currentSoundscapeVolumeScale = darknessTierAmbientVolumeScale;
                    _currentSoundscapePitchScale = darknessTierAmbientPitchScale;
                    break;

                case SoundscapeTier.Abyss:
                    _currentSoundscapeVolumeScale = abyssTierAmbientVolumeScale;
                    _currentSoundscapePitchScale = abyssTierAmbientPitchScale;
                    break;

                case SoundscapeTier.DeepAbyss:
                    _currentSoundscapeVolumeScale = deepAbyssTierAmbientVolumeScale;
                    _currentSoundscapePitchScale = deepAbyssTierAmbientPitchScale;
                    break;

                case SoundscapeTier.Thermal:
                    _currentSoundscapeVolumeScale = thermalTierAmbientVolumeScale;
                    _currentSoundscapePitchScale = thermalTierAmbientPitchScale;
                    break;

                case SoundscapeTier.Surface:
                case SoundscapeTier.Shallow:
                default:
                    break;
            }
        }

        private void HandleSoundscapeTierChanged(SoundscapeTier oldTier, SoundscapeTier newTier)
        {
            ApplySoundscapeTierContext(newTier);
        }

        void ISoundscapeEventListener.OnSoundscapeTierChanged(SoundscapeTier oldTier, SoundscapeTier newTier)
        {
            HandleSoundscapeTierChanged(oldTier, newTier);
        }

        private void RefreshBiomeAmbientContext()
        {
            ResolveBiomeMatrixDirector(false);

            HectonBiomeMatrixProfile profile = biomeMatrixDirector != null
                ? biomeMatrixDirector.CurrentProfile
                : null;

            if (ReferenceEquals(profile, _lastBiomeProfileForAmbient))
                return;

            _lastBiomeProfileForAmbient = profile;
            _currentAmbientSurvivalPressure = 0;
            _currentAmbientRewardPull = 0;
            _currentAmbientSummary = null;
            _currentAmbientVolumeScale = 1f;
            _currentAmbientPitchScale = 1f;

            if (profile == null)
                return;

            _currentAmbientSurvivalPressure = profile.survivalPressure;
            _currentAmbientRewardPull = profile.rewardPull;

            HectonBiomeFamilyProfile familyProfile = profile.familyProfile;
            if (familyProfile != null)
            {
                HectonFaunaFamilyProfile faunaFamilyProfile = familyProfile.faunaFamilyProfile;
                if (faunaFamilyProfile != null)
                    _currentAmbientSummary = faunaFamilyProfile.ambienceSummary;
            }

            if (_currentAmbientSurvivalPressure >= 4)
            {
                _currentAmbientVolumeScale = hostileAmbientVolumeScale;
                _currentAmbientPitchScale = hostileAmbientPitchScale;
                return;
            }

            if (_currentAmbientRewardPull >= 4 && _currentAmbientSurvivalPressure <= 2)
            {
                _currentAmbientVolumeScale = livelyAmbientVolumeScale;
                _currentAmbientPitchScale = livelyAmbientPitchScale;
                return;
            }

            if (_currentAmbientSurvivalPressure <= 2 && _currentAmbientRewardPull <= 2)
            {
                _currentAmbientVolumeScale = calmAmbientVolumeScale;
                _currentAmbientPitchScale = calmAmbientPitchScale;
                return;
            }

            _currentAmbientVolumeScale = mixedAmbientVolumeScale;
            _currentAmbientPitchScale = mixedAmbientPitchScale;
        }

        private void ApplyZoneTransition(AcousticZoneState zone)
        {
            QueueZonePresentationTransition(zone);
        }

        private void QueueInitialSnapshotPresentation(AcousticZoneState zone)
        {
            QueueAmbientLoopState(zone);
            QueuePendingSnapshotTransition(zone, 0f);
        }

        private void QueueZonePresentationTransition(AcousticZoneState zone)
        {
            _pendingZonePresentationTransition = zone;
            _hasPendingZonePresentationTransition = true;
            _hasPendingSnapshotTransition = false;
            _pendingSnapshotDuration = 0f;
        }

        private void FlushPendingZonePresentationTransition()
        {
            if (!_hasPendingZonePresentationTransition)
                return;

            AcousticZoneState zone = _pendingZonePresentationTransition;
            _hasPendingZonePresentationTransition = false;

            ApplyZonePresentationTransition(zone);
        }

        private void ApplyZonePresentationTransition(AcousticZoneState zone)
        {
            switch (zone)
            {
                case AcousticZoneState.Interior:
                    TransitionToInterior();
                    break;

                case AcousticZoneState.Surface:
                    TransitionToSurface();
                    break;

                default:
                    TransitionToUnderwater();
                    break;
            }
        }

        private IAtmosphereReadModel ResolveAtmosphereReadModel()
        {
            return _atmosphereReadModel;
        }

        private void RefreshAtmosphereZoneCache()
        {
            IAtmosphereReadModel atmosphere = ResolveAtmosphereReadModel();
            if (atmosphere == null)
            {
                _hasCachedExteriorZone = false;
                return;
            }

            HandleAtmosphereStateChanged(atmosphere.IsUnderwaterState ? EnvironmentState.UNDERWATER : EnvironmentState.SURFACE_DAY);
        }

        private HectonPlayerMovement ResolvePlayerMovement()
        {
            if (_playerMovement == null && _playerRuntimeContext != null)
                _playerMovement = _playerRuntimeContext.PlayerMovement;

            return _playerMovement;
        }

        private bool TryResolvePlayerMovementRuntimeState(out PlayerMovementRuntimeState movementState)
        {
            movementState = default;
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext == null ||
                !playerContext.IsInitialized ||
                !playerContext.TryGetMovementRuntimeState(out movementState) ||
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u ||
                !math.isfinite(movementState.DepthMeters))
            {
                movementState = default;
                return false;
            }

            return true;
        }

        private bool HasPlayerRuntimeContext()
        {
            return _playerRuntimeContext != null;
        }

        private bool TryResolvePlayerImpactDistanceSq(in PhysicsImpactSignal impactSignal, out double distanceSq)
        {
            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
            {
                distanceSq = 0d;
                return false;
            }

            AbsoluteUniversePosition impactAup = AbsoluteUniversePosition.FromAbsolutePosition(impactSignal.ResolvePointAupMeters());
            distanceSq = AbsoluteUniversePosition.DistanceSq(in playerAup, in impactAup);
            return true;
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            playerAup = AbsoluteUniversePosition.Invalid();
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null)
            {
                if (playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                    (snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                    snapshot.Aup.IsFinite())
                {
                    playerAup = snapshot.Aup;
                    return true;
                }

                return false;
            }

            HectonPlayerMovement movement = ResolvePlayerMovement();
            if (movement == null)
                return false;

            playerAup = movement.CurrentAup;
            return playerAup.IsFinite();
        }

        private bool TryResolvePlayerAupRuntimePosition(out Vector3 runtimePosition, out AbsoluteUniversePosition playerAup)
        {
            if (!TryResolvePlayerAup(out playerAup))
            {
                runtimePosition = default;
                return false;
            }

            float3 runtime = playerAup.ToRuntimeFloat3();
            runtimePosition = new Vector3(runtime.x, runtime.y, runtime.z);
            return true;
        }

        private static float ApproximateOneMinusExpNegPositive(float x)
        {
            return math.saturate(1f - ApproximateExpNegPositive(x));
        }

        private static float ApproximateExpNegPositive(float x)
        {
            float clamped = math.clamp(x, 0f, 8f);
            float x2 = clamped * clamped;
            float x3 = x2 * clamped;
            float numerator = 120f - (60f * clamped) + (12f * x2) - x3;
            float denominator = 120f + (60f * clamped) + (12f * x2) + x3;
            return math.saturate(numerator / math.max(denominator, 0.0001f));
        }

        private AudioSource ResolvePlayerAmbientSource(bool allowPresentationState = false)
        {
            if ((object)playerUnderwaterAmbientSource != null && playerUnderwaterAmbientSource != null)
            {
                if (allowPresentationState)
                {
                    EnsureAmbientSourceMixerRouting(playerUnderwaterAmbientSource);
                    CacheAmbientSourceDefaults(playerUnderwaterAmbientSource);
                }

                return playerUnderwaterAmbientSource;
            }

            playerUnderwaterAmbientSource = null;
            return null;
        }

        private AudioSource ResolvePlayerAmbientSourceCold(bool allowPresentationState = false)
        {
            AudioSource ambientSource = ResolvePlayerAmbientSource(allowPresentationState);
            if (ambientSource != null)
                return ambientSource;

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            Transform playerTransform = playerContext != null ? playerContext.PlayerTransform : null;

            if (playerTransform != null)
                ResolvePlayerAmbientSourceCold(playerTransform, allowPresentationState);

            return playerUnderwaterAmbientSource;
        }

        private void ResolvePlayerAmbientSourceCold(Transform playerTransform, bool allowPresentationState = false)
        {
            if ((object)playerUnderwaterAmbientSource != null && playerUnderwaterAmbientSource != null)
            {
                if (allowPresentationState)
                {
                    EnsureAmbientSourceMixerRouting(playerUnderwaterAmbientSource);
                    CacheAmbientSourceDefaults(playerUnderwaterAmbientSource);
                }

                return;
            }

            if (playerTransform == null || _playerAudioSources == null)
                return;

            if (_lastAmbientSourceSearchRoot != playerTransform)
            {
                _lastAmbientSourceSearchRoot = playerTransform;
                _nextAmbientSourceHierarchyResolveTime = 0f;
            }

            if (ResolvePresentationClockSeconds() < _nextAmbientSourceHierarchyResolveTime)
                return;

            _playerAudioSources.Clear();
            playerTransform.GetComponentsInChildren(true, _playerAudioSources);

            int count = _playerAudioSources.Count;
            for (int i = 0; i < count; i++)
            {
                AudioSource candidate = _playerAudioSources[i];
                if (candidate == null || candidate.clip == null)
                    continue;

                if (!candidate.loop || candidate.spatialBlend > 0.01f)
                    continue;

                if (!candidate.playOnAwake && !candidate.isPlaying)
                    continue;

                playerUnderwaterAmbientSource = candidate;
                if (allowPresentationState)
                {
                    EnsureAmbientSourceMixerRouting(candidate);
                    CacheAmbientSourceDefaults(candidate);
                }

                return;
            }

            _nextAmbientSourceHierarchyResolveTime = ResolvePresentationClockSeconds() + AmbientSourceResolveRetryInterval;
        }

        private void CacheAmbientSourceDefaults(AudioSource ambientSource)
        {
            if (ambientSource == null)
                return;

            EnsureAmbientSourceMixerRouting(ambientSource);
            if (_cachedAmbientSource == ambientSource && _ambientSourceDefaultsCaptured)
                return;

            _cachedAmbientSource = ambientSource;
            _ambientSourceBaseVolume = ambientSource.volume;
            _ambientSourceBasePitch = ambientSource.pitch;
            _ambientSourceDefaultsCaptured = true;
        }

        private void EnsureAmbientSourceMixerRouting(AudioSource ambientSource)
        {
            if (ambientSource == null)
                return;

            if (playerUnderwaterAmbientMixerGroup != null)
            {
                if (ambientSource.outputAudioMixerGroup != playerUnderwaterAmbientMixerGroup)
                    QueueAmbientSourceMixerRouting(ambientSource, playerUnderwaterAmbientMixerGroup);
                return;
            }

            IAudioService audioService = ResolveAudioService();
            if (ambientSource.outputAudioMixerGroup == null &&
                audioService != null &&
                audioService.AmbientGroup != null)
            {
                QueueAmbientSourceMixerRouting(ambientSource, audioService.AmbientGroup);
            }
        }

        private void QueueAmbientSourceMixerRouting(AudioSource ambientSource, AudioMixerGroup mixerGroup)
        {
            if (ambientSource == null || mixerGroup == null)
                return;

            _pendingAmbientSourceMixerRoutingSource = ambientSource;
            _pendingAmbientSourceMixerRoutingGroup = mixerGroup;
            _pendingAmbientSourceMixerRoutingDirty = true;
        }

        private void FlushPendingAmbientSourceMixerRouting()
        {
            if (!_pendingAmbientSourceMixerRoutingDirty)
                return;

            _pendingAmbientSourceMixerRoutingDirty = false;
            AudioSource ambientSource = _pendingAmbientSourceMixerRoutingSource;
            AudioMixerGroup mixerGroup = _pendingAmbientSourceMixerRoutingGroup;
            if (ambientSource != null && mixerGroup != null && ambientSource.outputAudioMixerGroup != mixerGroup)
                ambientSource.outputAudioMixerGroup = mixerGroup;
        }

        private AudioListener ResolvePlayerListenerFilters()
        {
            AudioListener listener = _cachedPlayerAudioListener;
            if ((object)listener != null && listener != null)
            {
                EnsureAcousticMixerParameterBindings();
                return listener;
            }

            _cachedPlayerAudioListener = null;
            return null;
        }

        private AudioListener ResolvePlayerListenerFiltersCold()
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            Transform playerTransform = playerContext != null ? playerContext.PlayerTransform : null;
            if (playerTransform != null)
                ResolvePlayerListenerFiltersCold(playerTransform);

            return _cachedPlayerAudioListener;
        }

        private void ResolvePlayerListenerFiltersCold(Transform playerTransform)
        {
            if (playerTransform == null)
                return;

            AudioListener listener = _cachedPlayerAudioListener;
            if ((object)listener == null || listener == null)
            {
                if (!playerTransform.TryGetComponent(out listener))
                    listener = Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<AudioListener>(playerTransform);

                _cachedPlayerAudioListener = listener;
            }

            if ((object)listener == null || listener == null)
                return;

            EnsureAcousticMixerParameterBindings();
        }

        private bool EnsureAcousticMixerParameterBindings()
        {
            if (_acousticMixerBindingsResolved)
                return _acousticMixerBindingsValid;

            _acousticMixerBindingsResolved = true;
            _acousticMixerBindingsValid = false;

            if (masterMixer == null)
                return false;

            _resolvedAcousticLowPassCutoffParameter = ResolveAcousticMixerParameterName(acousticLowPassCutoffParameter, AcousticLowPassCutoffParameterDefault);
            _resolvedAcousticLowPassResonanceParameter = ResolveAcousticMixerParameterName(acousticLowPassResonanceParameter, AcousticLowPassResonanceParameterDefault);
            _resolvedAcousticReverbDecayParameter = ResolveAcousticMixerParameterName(acousticReverbDecayParameter, AcousticReverbDecayParameterDefault);
            _resolvedAcousticReflectionsLevelParameter = ResolveAcousticMixerParameterName(acousticReflectionsLevelParameter, AcousticReflectionsLevelParameterDefault);
            _resolvedAcousticReverbLevelParameter = ResolveAcousticMixerParameterName(acousticReverbLevelParameter, AcousticReverbLevelParameterDefault);
            _resolvedAcousticRoomHighFrequencyParameter = ResolveAcousticMixerParameterName(acousticRoomHighFrequencyParameter, AcousticRoomHighFrequencyParameterDefault);
            _resolvedAcousticDryLevelParameter = ResolveAcousticMixerParameterName(acousticDryLevelParameter, AcousticDryLevelParameterDefault);

            if (!masterMixer.GetFloat(_resolvedAcousticLowPassCutoffParameter, out _listenerLowPassBaseCutoff) ||
                !masterMixer.GetFloat(_resolvedAcousticLowPassResonanceParameter, out _listenerLowPassBaseResonance) ||
                !masterMixer.GetFloat(_resolvedAcousticReverbDecayParameter, out _listenerReverbBaseDecayTime) ||
                !masterMixer.GetFloat(_resolvedAcousticReflectionsLevelParameter, out _listenerReverbBaseReflectionsLevel) ||
                !masterMixer.GetFloat(_resolvedAcousticReverbLevelParameter, out _listenerReverbBaseReverbLevel) ||
                !masterMixer.GetFloat(_resolvedAcousticRoomHighFrequencyParameter, out _listenerReverbBaseRoomHighFrequency) ||
                !masterMixer.GetFloat(_resolvedAcousticDryLevelParameter, out _listenerReverbBaseDryLevel))
            {
                LogMissingAcousticMixerParameterWarning();
                return false;
            }

            _currentAcousticLowPassCutoffHz = _listenerLowPassBaseCutoff;
            _currentAcousticLowPassResonanceQ = _listenerLowPassBaseResonance;
            _currentAcousticReverbDecayTime = _listenerReverbBaseDecayTime;
            _currentAcousticReflectionsLevelDb = _listenerReverbBaseReflectionsLevel;
            _currentAcousticReverbLevelDb = _listenerReverbBaseReverbLevel;
            _currentAcousticRoomHighFrequencyDb = _listenerReverbBaseRoomHighFrequency;
            _currentAcousticDryLevelDb = _listenerReverbBaseDryLevel;
            _acousticGraphStateInitialized = false;
            _listenerFallbackDefaultsCaptured = true;
            _acousticMixerBindingsValid = true;
            InvalidateAppliedAcousticMixerStateCache();
            return true;
        }

        private bool ApplyAcousticMixerState(
            float lowPassCutoffHz,
            float lowPassResonanceQ,
            float reverbDecayTime,
            float reflectionsLevelDb,
            float reverbLevelDb,
            float roomHighFrequencyDb,
            float dryLevelDb)
        {
            // L19 hop2: mixer parameter writes still touch FMOD graph state under batchmode.
            if (Application.isBatchMode || masterMixer == null)
                return false;


            if (HasAppliedAcousticMixerState(
                    lowPassCutoffHz,
                    lowPassResonanceQ,
                    reverbDecayTime,
                    reflectionsLevelDb,
                    reverbLevelDb,
                    roomHighFrequencyDb,
                    dryLevelDb))
            {
                return true;
            }

            if (!masterMixer.SetFloat(_resolvedAcousticLowPassCutoffParameter, lowPassCutoffHz) ||
                !masterMixer.SetFloat(_resolvedAcousticLowPassResonanceParameter, lowPassResonanceQ) ||
                !masterMixer.SetFloat(_resolvedAcousticReverbDecayParameter, reverbDecayTime) ||
                !masterMixer.SetFloat(_resolvedAcousticReflectionsLevelParameter, reflectionsLevelDb) ||
                !masterMixer.SetFloat(_resolvedAcousticReverbLevelParameter, reverbLevelDb) ||
                !masterMixer.SetFloat(_resolvedAcousticRoomHighFrequencyParameter, roomHighFrequencyDb) ||
                !masterMixer.SetFloat(_resolvedAcousticDryLevelParameter, dryLevelDb))
            {
                _acousticMixerBindingsValid = false;
                _usingSourceLevelAcousticFallback = false;
                LogMissingAcousticMixerParameterWarning();
                InvalidateAppliedAcousticMixerStateCache();
                return false;
            }

            CacheAppliedAcousticMixerState(
                lowPassCutoffHz,
                lowPassResonanceQ,
                reverbDecayTime,
                reflectionsLevelDb,
                reverbLevelDb,
                roomHighFrequencyDb,
                dryLevelDb);
            return true;
        }

        private static string ResolveAcousticMixerParameterName(string configuredName, string fallbackName)
        {
            return string.IsNullOrWhiteSpace(configuredName) ? fallbackName : configuredName;
        }

        private void LogMissingAcousticMixerParameterWarning()
        {
            LogSnapshotFallbackWarningOnce(
                ref _warnedMissingAcousticMixerParameters,
                "[AcousticZoneController] MasterMixer acoustic exposed parameters are missing. Required params: " +
                AcousticLowPassCutoffParameterDefault + ", " +
                AcousticLowPassResonanceParameterDefault + ", " +
                AcousticReverbDecayParameterDefault + ", " +
                AcousticReflectionsLevelParameterDefault + ", " +
                AcousticReverbLevelParameterDefault + ", " +
                AcousticRoomHighFrequencyParameterDefault + ", " +
                AcousticDryLevelParameterDefault +
                ". Runtime acoustic graph fallback is disabled to avoid direct DSP component mutation.");
        }

        private void UpdateAmbientLoopMix(AcousticZoneState zone)
        {
            AudioSource ambientSource = ResolvePlayerAmbientSource(allowPresentationState: true);
            if (ambientSource == null)
                return;

            CacheAmbientSourceDefaults(ambientSource);

            float targetVolume = _ambientSourceBaseVolume;
            float targetPitch = _ambientSourceBasePitch;

            if (zone == AcousticZoneState.Underwater)
            {
                targetVolume *= _currentAmbientVolumeScale;
                targetPitch *= _currentAmbientPitchScale;
                targetVolume *= _currentSoundscapeVolumeScale;
                targetPitch *= _currentSoundscapePitchScale;
                targetVolume *= ResolveMusicAmbientDuckVolumeScale();
                if (_stormAmbientInterference > 0.001f)
                {
                    targetVolume *= math.lerp(1f, math.max(0.1f, 1f - stormAmbientDuckMax), _stormAmbientInterference);
                    targetPitch *= math.lerp(1f, math.max(0.5f, 1f - stormAmbientPitchDropMax), _stormAmbientInterference);
                    targetPitch += _stormAmbientFlutter;
                }
            }

            if (math.abs(ambientSource.volume - targetVolume) > 0.01f)
                ambientSource.volume = targetVolume;

            if (math.abs(ambientSource.pitch - targetPitch) > 0.01f)
                ambientSource.pitch = targetPitch;
        }

        private void UpdateMusicAmbientDucking(AcousticZoneState zone, float deltaTime)
        {
            float target01 = ResolveMusicAmbientDuckTarget01(zone);
            float sharpness = target01 > _currentMusicAmbientDuck01
                ? musicAmbientDuckAttackSharpness
                : musicAmbientDuckReleaseSharpness;

            float blendT = deltaTime <= 0f
                ? 1f
                : ApproximateOneMinusExpNegPositive(math.max(0.01f, sharpness) * deltaTime);

            _currentMusicAmbientDuck01 = math.saturate(math.lerp(_currentMusicAmbientDuck01, target01, blendT));
            _debugMusicAmbientDuck = _currentMusicAmbientDuck01;
        }

        private float ResolveMusicAmbientDuckTarget01(AcousticZoneState zone)
        {
            if (zone != AcousticZoneState.Underwater)
                return 0f;

            HectonMusicDirector musicDirector = _cachedMusicDirector;
            if (musicDirector == null || !musicDirector.isActiveAndEnabled)
                return 0f;

            HectonMusicDirector.MusicActivityReason reason = musicDirector.CurrentMusicActivityReason;
            if (reason == HectonMusicDirector.MusicActivityReason.Emergency ||
                reason == HectonMusicDirector.MusicActivityReason.Silent ||
                reason == HectonMusicDirector.MusicActivityReason.Rest)
            {
                return 0f;
            }

            float activity01 = math.saturate(musicDirector.CurrentMusicActivity01);
            if (activity01 <= 0.001f)
                return 0f;

            return math.saturate(activity01 * ResolveMusicAmbientDuckReasonWeight01(reason));
        }

        private float ResolveMusicAmbientDuckReasonWeight01(HectonMusicDirector.MusicActivityReason reason)
        {
            switch (reason)
            {
                case HectonMusicDirector.MusicActivityReason.Exploration:
                case HectonMusicDirector.MusicActivityReason.Menu:
                case HectonMusicDirector.MusicActivityReason.Prologue:
                    return explorationMusicAmbientDuckWeight;
                case HectonMusicDirector.MusicActivityReason.Base:
                    return baseMusicAmbientDuckWeight;
                case HectonMusicDirector.MusicActivityReason.Tense:
                    return tenseMusicAmbientDuckWeight;
                case HectonMusicDirector.MusicActivityReason.Combat:
                case HectonMusicDirector.MusicActivityReason.Override:
                    return foregroundMusicAmbientDuckWeight;
                default:
                    return 0f;
            }
        }

        private float ResolveMusicAmbientDuckVolumeScale()
        {
            return math.lerp(1f, math.max(0.1f, 1f - musicAmbientDuckMax), math.saturate(_currentMusicAmbientDuck01));
        }

        private void QueueAmbientLoopState(AcousticZoneState zone)
        {
            _pendingAmbientLoopZone = zone;
            _pendingAmbientLoopStateDirty = true;
        }

        private void UpdateStormInterferenceAudio(AcousticZoneState zone, float deltaTime)
        {
            if (zone == AcousticZoneState.Interior)
            {
                _stormAmbientInterference = 0f;
                _stormAmbientFlutter = 0f;
                _stormInterferencePulseTimer = 0f;
                _debugStormInterference = 0f;
                return;
            }

            if (_surfaceElectricalActivity <= stormStaticElectricalThreshold)
            {
                _stormAmbientInterference = 0f;
                _stormAmbientFlutter = 0f;
                _stormInterferencePulseTimer = 0f;
                _debugStormInterference = 0f;
                return;
            }

            float stormInterference = math.saturate(
                (_surfaceElectricalActivity - stormStaticElectricalThreshold) /
                math.max(0.0001f, 1f - stormStaticElectricalThreshold));
            _stormAmbientInterference = stormInterference;
            _debugStormInterference = stormInterference;

            float flutterFrequency = math.lerp(stormAmbientFlutterFrequencyMin, stormAmbientFlutterFrequencyMax, stormInterference);
            _stormAmbientFlutterPhase += deltaTime * flutterFrequency * TwoPi;
            if (_stormAmbientFlutterPhase >= TwoPi)
                _stormAmbientFlutterPhase -= TwoPi;

            _stormAmbientFlutter = FastSineRadians(_stormAmbientFlutterPhase) * (stormAmbientPitchFlutterMax * stormInterference);

            _stormInterferencePulseTimer -= deltaTime;
            if (_stormInterferencePulseTimer > 0f)
                return;

            QueueStormInterferencePulse(stormInterference, zone);
            _stormInterferencePulseTimer = math.lerp(
                math.max(0.1f, stormStaticIntervalMax),
                math.max(0.1f, stormStaticIntervalMin),
                stormInterference);
        }

        private void UpdateUnderwaterVegetationOverlay(AcousticZoneState zone, float deltaTime)
        {
            if (zone != AcousticZoneState.Underwater)
            {
                _underwaterVegetationPulseTimer = 0f;
                return;
            }

            HectonMapMagicVegetationBridge.VegetationAcousticType acousticType =
                HectonMapMagicVegetationBridge.GlobalVegetationAcousticType;
            float density = math.saturate(HectonMapMagicVegetationBridge.GlobalVegetationAudioDensity);
            if (acousticType == HectonMapMagicVegetationBridge.VegetationAcousticType.Silence ||
                density <= underwaterVegetationDensityThreshold)
            {
                _underwaterVegetationPulseTimer = 0f;
                return;
            }

            _underwaterVegetationPulseTimer -= deltaTime;
            if (_underwaterVegetationPulseTimer > 0f)
                return;

            AudioClip clip = acousticType == HectonMapMagicVegetationBridge.VegetationAcousticType.SargassumBubbles
                ? underwaterSargassumBubblesClip
                : underwaterGrassRustleClip;
            if (clip == null)
                return;

            float densityT = math.saturate(
                (density - underwaterVegetationDensityThreshold) /
                math.max(0.0001f, 1f - underwaterVegetationDensityThreshold));
            float volume = math.lerp(underwaterVegetationVolumeMin, underwaterVegetationVolumeMax, densityT);
            QueueVegetationCue(clip, volume);
            _underwaterVegetationPulseTimer = math.lerp(
                math.max(0.1f, underwaterVegetationIntervalMax),
                math.max(0.1f, underwaterVegetationIntervalMin),
                densityT);
        }

        private void UpdateFatalPressureStressAudio(AcousticZoneState zone, float deltaTime)
        {
            HectonPlayerMovement movement = ResolvePlayerMovement();
            float intensity = movement != null ? math.saturate(movement.CurrentFatalPressureSequence01) : 0f;
            if (zone != AcousticZoneState.Underwater || intensity <= 0.001f)
            {
                _fatalPressureStressTimer = 0f;
                return;
            }

            _fatalPressureStressTimer -= deltaTime;
            if (_fatalPressureStressTimer > 0f)
                return;

            if (!TryResolvePlayerAupRuntimePosition(out Vector3 sourcePosition, out _))
                return;

            float stressLow = math.saturate(fatalPressureStressMin);
            float stressHigh = math.max(stressLow, math.saturate(fatalPressureStressMax));
            float pitchLow = math.max(0.25f, fatalPressureStressPitchMin);
            float pitchHigh = math.max(pitchLow, fatalPressureStressPitchMax);
            float stress01 = math.lerp(stressLow, stressHigh, intensity);
            float pitch = math.lerp(pitchLow, pitchHigh, intensity);
            QueueFatalPressureStressCue(sourcePosition, stress01, pitch);
            _fatalPressureStressTimer = math.lerp(
                math.max(0.05f, fatalPressureStressIntervalMax),
                math.max(0.05f, fatalPressureStressIntervalMin),
                intensity);
        }

        private void HandleSonarPingSent(float intensity)
        {
            float clampedIntensity = math.saturate(intensity);

            if (enableRuntimeAcousticGraph)
                _acousticSonarImpulse = math.max(_acousticSonarImpulse, clampedIntensity);

            if (PlayerCriticalProceduralAudioRenderer.IsRuntimeInstalled)
                return;

            if (sonarPingClip == null)
                return;

            float volume = math.lerp(sonarPingVolumeMin, sonarPingVolumeMax, clampedIntensity);
            QueueSonarPingCue(volume);
        }

        void ISonarPingEventListener.OnSonarPingSent(float intensity)
        {
            HandleSonarPingSent(intensity);
        }

        void IPhysicsImpactEventListener.OnPhysicsImpact(in PhysicsImpactSignal impactSignal)
        {
            HandlePhysicsImpact(in impactSignal);
        }

        private void HandlePhysicsImpact(in PhysicsImpactSignal impactSignal)
        {
            if (!enableRuntimeAcousticGraph)
                return;

            float radius = math.max(0.5f, acousticImpactImpulseRadius);
            double radiusSq = (double)radius * radius;
            if (!TryResolvePlayerImpactDistanceSq(in impactSignal, out double distanceSq))
                return;

            if (distanceSq > radiusSq)
                return;

            float proximity = 1f - math.saturate((float)(distanceSq / math.max(radiusSq, 0.0001d)));
            float impulse = math.saturate(impactSignal.Intensity * math.max(0.15f, proximity));
            if (PhysicsImpactSignal.IsHeavy(in impactSignal))
                impulse = math.max(impulse, 0.35f * math.max(0.35f, proximity));

            _acousticImpactImpulse = math.max(_acousticImpactImpulse, impulse);
        }

        internal void PlayMantaMisfire(float intensity)
        {
            if (mantaMisfireClip == null)
                return;

            float volume = math.lerp(mantaMisfireVolumeMin, mantaMisfireVolumeMax, math.saturate(intensity));
            QueueMantaMisfireCue(volume);
        }

        void IToolAcousticCueService.PlayMantaMisfire(float intensity01)
        {
            PlayMantaMisfire(intensity01);
        }

        private void QueueStormInterferencePulse(float stormInterference, AcousticZoneState zone)
        {
            AudioClip clip = null;
            if (stormStaticPrimary != null && stormStaticSecondary != null)
            {
                clip = _stormStaticUsePrimaryNext ? stormStaticPrimary : stormStaticSecondary;
                _stormStaticUsePrimaryNext = !_stormStaticUsePrimaryNext;
            }
            else if (stormStaticPrimary != null)
            {
                clip = stormStaticPrimary;
            }
            else if (stormStaticSecondary != null)
            {
                clip = stormStaticSecondary;
            }

            if (clip == null)
                return;

            float volume = math.lerp(stormStaticVolumeMin, stormStaticVolumeMax, stormInterference);
            if (zone == AcousticZoneState.Underwater)
                volume *= stormStaticUnderwaterVolumeScale;

            _pendingStormStaticCueClip = clip;
            _pendingStormStaticCueVolume = volume;
            _pendingStormStaticCueDirty = true;
        }

        private void QueueTransitionCue(AudioClip clip, float volume)
        {
            _pendingTransitionCueClip = clip;
            _pendingTransitionCueVolume = volume;
            _pendingTransitionCueDirty = true;
        }

        private void QueueMadnessWhisperCue(AudioClip clip, float volume)
        {
            _pendingMadnessWhisperCueClip = clip;
            _pendingMadnessWhisperCueVolume = volume;
            _pendingMadnessWhisperCueDirty = true;
        }

        private void QueueVegetationCue(AudioClip clip, float volume)
        {
            _pendingVegetationCueClip = clip;
            _pendingVegetationCueVolume = volume;
            _pendingVegetationCueDirty = true;
        }

        private void QueueFatalPressureStressCue(Vector3 sourcePosition, float stress01, float pitch)
        {
            _pendingFatalPressureCuePosition = sourcePosition;
            _pendingFatalPressureCueStress01 = stress01;
            _pendingFatalPressureCuePitch = pitch;
            _pendingFatalPressureCueDirty = true;
        }

        private void QueueMantaMisfireCue(float volume)
        {
            _pendingMantaMisfireCueVolume = volume;
            _pendingMantaMisfireCueDirty = true;
        }

        private void QueueSonarPingCue(float volume)
        {
            _pendingSonarPingCueVolume = volume;
            _pendingSonarPingCueDirty = true;
        }

        private void FlushQueuedAcousticCues()
        {
            IAudioService audioService = null;
            if (_pendingTransitionCueDirty)
            {
                _pendingTransitionCueDirty = false;
                audioService = ResolveAudioService();
                if (audioService != null && _pendingTransitionCueClip != null)
                    audioService.PlayStatic2D(_pendingTransitionCueClip, _pendingTransitionCueVolume);
            }

            if (_pendingMadnessWhisperCueDirty)
            {
                _pendingMadnessWhisperCueDirty = false;
                if (audioService == null)
                    audioService = ResolveAudioService();
                if (audioService != null && _pendingMadnessWhisperCueClip != null)
                    audioService.PlayStatic2D(_pendingMadnessWhisperCueClip, _pendingMadnessWhisperCueVolume, audioService.InterfaceGroup);
            }

            if (_pendingStormStaticCueDirty)
            {
                _pendingStormStaticCueDirty = false;
                if (audioService == null)
                    audioService = ResolveAudioService();
                if (audioService != null && _pendingStormStaticCueClip != null)
                    audioService.PlayStatic2D(_pendingStormStaticCueClip, _pendingStormStaticCueVolume, audioService.InterfaceGroup);
            }

            if (_pendingVegetationCueDirty)
            {
                _pendingVegetationCueDirty = false;
                if (audioService == null)
                    audioService = ResolveAudioService();
                if (audioService != null && _pendingVegetationCueClip != null)
                    audioService.PlayStatic2D(_pendingVegetationCueClip, _pendingVegetationCueVolume, audioService.AmbientGroup);
            }

            if (_pendingMantaMisfireCueDirty)
            {
                _pendingMantaMisfireCueDirty = false;
                if (audioService == null)
                    audioService = ResolveAudioService();
                if (audioService != null && mantaMisfireClip != null)
                    audioService.PlayStatic2D(mantaMisfireClip, _pendingMantaMisfireCueVolume, audioService.InterfaceGroup);
            }

            if (_pendingSonarPingCueDirty)
            {
                _pendingSonarPingCueDirty = false;
                if (audioService == null)
                    audioService = ResolveAudioService();
                if (audioService != null && sonarPingClip != null)
                    audioService.PlayStatic2D(sonarPingClip, _pendingSonarPingCueVolume, audioService.InterfaceGroup);
            }

            if (_pendingFatalPressureCueDirty)
            {
                _pendingFatalPressureCueDirty = false;
                ProceduralAudioEvents.TryRaiseStructuralStressTriggered(
                    _pendingFatalPressureCuePosition,
                    _pendingFatalPressureCueStress01,
                    _pendingFatalPressureCuePitch);
            }
        }

        private static float FastSineRadians(float radians)
        {
            float phase = radians * InvTwoPi;
            int whole = (int)phase;
            phase -= whole;
            if (phase < 0f)
                phase += 1f;
            else if (phase >= 1f)
                phase -= 1f;

            float centered = phase > 0.5f ? phase - 1f : phase;
            float wave = (4f * centered) - (8f * centered * math.abs(centered));
            return wave + 0.225f * ((wave * math.abs(wave)) - wave);
        }

        private string ResolveAmbientMoodLabel()
        {
            if (_currentAmbientSurvivalPressure >= 4)
                return "Hostile";

            if (_currentAmbientRewardPull >= 4 && _currentAmbientSurvivalPressure <= 2)
                return "Lively";

            if (_currentAmbientSurvivalPressure <= 2 && _currentAmbientRewardPull <= 2)
                return "Calm";

            if (_currentAmbientSurvivalPressure <= 0 && _currentAmbientRewardPull <= 0)
                return "None";

            return "Mixed";
        }

        internal void SetSurfaceWeatherMix(float precipitationIntensity, float electricalActivity)
        {
            float clampedPrecipitation = math.saturate(precipitationIntensity);
            float clampedElectrical = math.saturate(electricalActivity);
            if (ApproximatelyEqual(_surfacePrecipitationIntensity, clampedPrecipitation) &&
                ApproximatelyEqual(_surfaceElectricalActivity, clampedElectrical))
            {
                return;
            }

            _surfacePrecipitationIntensity = clampedPrecipitation;
            _surfaceElectricalActivity = clampedElectrical;
            _debugStormInterference = clampedElectrical <= stormStaticElectricalThreshold
                ? 0f
                : math.saturate(
                    (clampedElectrical - stormStaticElectricalThreshold) /
                    math.max(0.0001f, 1f - stormStaticElectricalThreshold));

            if (_stateInitialized && _lastZone == AcousticZoneState.Surface)
                QueuePendingSnapshotTransition(AcousticZoneState.Surface, surfaceWeatherTransitionDuration);
        }

        internal void ClearSurfaceWeatherMix()
        {
            if (ApproximatelyEqual(_surfacePrecipitationIntensity, 0f) &&
                ApproximatelyEqual(_surfaceElectricalActivity, 0f))
            {
                return;
            }

            _surfacePrecipitationIntensity = 0f;
            _surfaceElectricalActivity = 0f;
            _stormInterferencePulseTimer = 0f;
            _stormAmbientInterference = 0f;
            _stormAmbientFlutter = 0f;
            _debugStormInterference = 0f;

            if (_stateInitialized && _lastZone == AcousticZoneState.Surface)
                QueuePendingSnapshotTransition(AcousticZoneState.Surface, surfaceWeatherTransitionDuration);
        }

        private void ApplyAmbientLoopState(AcousticZoneState zone)
        {
            QueueAmbientLoopState(zone);
        }

        private void FlushAmbientLoopState(AcousticZoneState zone)
        {
            AudioSource ambientSource = ResolvePlayerAmbientSource(allowPresentationState: true);
            if (ambientSource == null)
            {
                ApplySourceLevelAcousticFallback(zone);
                return;
            }

            if (PlayerCriticalProceduralAudioRenderer.IsRuntimeInstalled)
            {
                if (ambientSource.isActiveAndEnabled && ambientSource.isPlaying)
                    ambientSource.Stop();

                if (ambientSource.isActiveAndEnabled && !ambientSource.mute)
                    ambientSource.mute = true;

                ApplySourceLevelAcousticFallback(zone);
                return;
            }

            bool shouldBeAudible = zone == AcousticZoneState.Underwater;
            bool shouldMute = !shouldBeAudible;

            if (ambientSource.mute != shouldMute)
                ambientSource.mute = shouldMute;

            // AudioSource.Play throws InvalidOperationException when the component is disabled
            // (menu unload / Step-8 teardown can disable the player ambient source mid-LateFrame).
            if (shouldBeAudible &&
                !ambientSource.isPlaying &&
                ambientSource.clip != null &&
                ambientSource.isActiveAndEnabled &&
                ambientSource.enabled)
            {
                ambientSource.Play();
            }

            UpdateAmbientLoopMix(zone);
            ApplySourceLevelAcousticFallback(zone);
        }

        private void ApplySourceLevelAcousticFallback(AcousticZoneState zone)
        {
            UpdateSourceLevelAcousticGraph(zone, 0f);
        }

        private void QueueSourceLevelAcousticGraph(AcousticZoneState zone, float deltaTime)
        {
            _pendingSourceLevelGraphZone = zone;
            _pendingSourceLevelGraphDeltaTime = math.max(0f, deltaTime);
            _pendingSourceLevelGraphDirty = true;
        }

        private void UpdateSourceLevelAcousticGraph(AcousticZoneState zone, float deltaTime)
        {
            DecayAcousticGraphImpulses(deltaTime);

            if (!ShouldUseSourceLevelAcousticFallback())
            {
                ResetSourceLevelAcousticFallback();
                return;
            }

            ResolvePlayerListenerFilters();
            if (!_listenerFallbackDefaultsCaptured)
            {
                return;
            }

            if (zone == AcousticZoneState.Surface)
            {
                ResetSourceLevelAcousticFallback();
                return;
            }

            AudioListener listener = _cachedPlayerAudioListener;
            UpdateEmitterOcclusionState(listener);

            AcousticGraphState targetState = zone == AcousticZoneState.Interior
                ? ResolveInteriorAcousticGraphState()
                : ResolveUnderwaterAcousticGraphState();

            float blendT = deltaTime <= 0f
                ? 1f
                : ApproximateOneMinusExpNegPositive(math.max(0.01f, acousticGraphFollowSharpness) * deltaTime);

            if (!_acousticGraphStateInitialized)
            {
                _currentAcousticLowPassCutoffHz = targetState.LowPassCutoffHz;
                _currentAcousticLowPassResonanceQ = targetState.LowPassResonanceQ;
                _currentAcousticReverbDecayTime = targetState.ReverbDecayTime;
                _currentAcousticReflectionsLevelDb = targetState.ReflectionsLevelDb;
                _currentAcousticReverbLevelDb = targetState.ReverbLevelDb;
                _currentAcousticRoomHighFrequencyDb = targetState.RoomHighFrequencyDb;
                _currentAcousticDryLevelDb = targetState.DryLevelDb;
                _acousticGraphStateInitialized = true;
            }
            else
            {
                _currentAcousticLowPassCutoffHz = math.lerp(_currentAcousticLowPassCutoffHz, targetState.LowPassCutoffHz, blendT);
                _currentAcousticLowPassResonanceQ = math.lerp(_currentAcousticLowPassResonanceQ, targetState.LowPassResonanceQ, blendT);
                _currentAcousticReverbDecayTime = math.lerp(_currentAcousticReverbDecayTime, targetState.ReverbDecayTime, blendT);
                _currentAcousticReflectionsLevelDb = math.lerp(_currentAcousticReflectionsLevelDb, targetState.ReflectionsLevelDb, blendT);
                _currentAcousticReverbLevelDb = math.lerp(_currentAcousticReverbLevelDb, targetState.ReverbLevelDb, blendT);
                _currentAcousticRoomHighFrequencyDb = math.lerp(_currentAcousticRoomHighFrequencyDb, targetState.RoomHighFrequencyDb, blendT);
                _currentAcousticDryLevelDb = math.lerp(_currentAcousticDryLevelDb, targetState.DryLevelDb, blendT);
            }

            _usingSourceLevelAcousticFallback = ApplyAcousticMixerState(
                _currentAcousticLowPassCutoffHz,
                _currentAcousticLowPassResonanceQ,
                _currentAcousticReverbDecayTime,
                _currentAcousticReflectionsLevelDb,
                _currentAcousticReverbLevelDb,
                _currentAcousticRoomHighFrequencyDb,
                _currentAcousticDryLevelDb);
        }

        private void DecayAcousticGraphImpulses(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            float safeDeltaTime = math.max(0f, deltaTime);
            _acousticImpactImpulse = math.max(
                0f,
                _acousticImpactImpulse - (math.max(0.01f, acousticImpactImpulseDecay) * safeDeltaTime));
            _acousticSonarImpulse = math.max(
                0f,
                _acousticSonarImpulse - (math.max(0.01f, acousticSonarImpulseDecay) * safeDeltaTime));
        }

        private void UpdateEmitterOcclusionState(AudioListener listener)
        {
            _emitterOcclusionTransmission01 = 1f;
            _emitterOcclusionLowPassCutoffHz = AcousticOcclusionUtility.OpenLowPassCutoffHertz;

            if ((object)listener == null || listener == null)
                return;

            ISpatialAudioWorldEmitterReadModel spatialAudioReadModel = ResolveSpatialAudioEmitterReadModel();
            if (spatialAudioReadModel == null)
                return;

            if (_resolvedEmitterOcclusionLayerMask == 0)
                _resolvedEmitterOcclusionLayerMask = AcousticOcclusionUtility.BuildSensoryMask();

            if (_resolvedEmitterOcclusionLayerMask == 0)
                return;

            int emitterCount = spatialAudioReadModel.CopyActiveWorldEmitterSamples(s_emitterOcclusionSamples);
            if (emitterCount <= 0)
                return;

            Transform listenerTransform = listener.transform;
            if (!TryResolvePlayerAupRuntimePosition(out Vector3 listenerPosition, out _))
                return;

            Transform listenerRoot = listenerTransform.root;
            float3 listenerPosition3 = new float3(listenerPosition.x, listenerPosition.y, listenerPosition.z);
            float maxDistanceSqr = AcousticEmitterOcclusionMaxDistanceMeters * AcousticEmitterOcclusionMaxDistanceMeters;
            float weightedTransmission = 0f;
            float weightedCutoff = 0f;
            float totalWeight = 0f;

            for (int i = 0; i < emitterCount; i++)
            {
                SpatialAudioActiveEmitterSample sample = s_emitterOcclusionSamples[i];
                if (!(sample.Amplitude > 0.0001f))
                    continue;

                float3 samplePosition = new float3(sample.Position.x, sample.Position.y, sample.Position.z);
                float distanceSqr = math.lengthsq(samplePosition - listenerPosition3);
                if (distanceSqr > maxDistanceSqr)
                    continue;

                float sampleWeight = sample.Amplitude / (1f + (distanceSqr * AcousticEmitterDistanceWeightScale));
                if (!(sampleWeight > 0.0001f))
                    continue;

                if (!AcousticOcclusionUtility.TryGetCachedOcclusionPath(
                        sample.Position,
                        listenerPosition,
                        _resolvedEmitterOcclusionLayerMask,
                        null,
                        listenerRoot,
                        out AcousticOcclusionResult occlusion))
                {
                    AcousticOcclusionUtility.PrimeOcclusionPath(
                        sample.Position,
                        listenerPosition,
                        _resolvedEmitterOcclusionLayerMask,
                        null,
                        listenerRoot);
                    continue;
                }

                weightedTransmission += occlusion.Transmission01 * sampleWeight;
                weightedCutoff += occlusion.LowPassCutoffHz * sampleWeight;
                totalWeight += sampleWeight;

                AcousticOcclusionUtility.PrimeOcclusionPath(
                    sample.Position,
                    listenerPosition,
                    _resolvedEmitterOcclusionLayerMask,
                    null,
                    listenerRoot);
            }

            if (!(totalWeight > 0.0001f))
                return;

            float invTotalWeight = math.rcp(math.max(totalWeight, 0.0001f));
            _emitterOcclusionTransmission01 = math.saturate(weightedTransmission * invTotalWeight);
            _emitterOcclusionLowPassCutoffHz = math.clamp(
                weightedCutoff * invTotalWeight,
                AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                AcousticOcclusionUtility.OpenLowPassCutoffHertz);
        }

        private AcousticGraphState ResolveInteriorAcousticGraphState()
        {
            float metallicImpulse = math.saturate(_acousticImpactImpulse * math.max(0f, impactGraphMetallicBoost));
            float sonarImpulse = math.saturate(_acousticSonarImpulse);
            AcousticGraphState state;
            state.LowPassCutoffHz = math.lerp(interiorGraphLowPassCutoff, 7200f, metallicImpulse);
            state.LowPassResonanceQ = math.lerp(interiorGraphResonance, interiorGraphResonance + 0.22f, metallicImpulse);
            state.ReverbDecayTime = math.clamp(
                interiorGraphDecayTime +
                (interiorImpactDecayBoost * metallicImpulse) +
                (0.22f * sonarImpulse),
                0.05f,
                12f);
            state.ReflectionsLevelDb = math.clamp(
                interiorGraphReflectionsLevel + (550f * metallicImpulse),
                -10000f,
                1000f);
            state.ReverbLevelDb = math.clamp(
                interiorGraphReverbLevel + (450f * sonarImpulse),
                -10000f,
                2000f);
            state.RoomHighFrequencyDb = math.clamp(
                interiorGraphRoomHighFrequency - (1600f * metallicImpulse),
                -10000f,
                0f);
            state.DryLevelDb = math.clamp(
                interiorGraphDryLevel - (120f * sonarImpulse),
                -10000f,
                0f);
            state.Arm64AlignmentPad0 = 0f;
            ApplyEmitterOcclusionToAcousticState(ref state);
            return state;
        }

        private AcousticGraphState ResolveUnderwaterAcousticGraphState()
        {
            float depth01 = ResolveUnderwaterGraphDepth01();
            float sonarImpulse = math.saturate(_acousticSonarImpulse * math.max(0f, sonarGraphOpenUpBoost));
            float metallicImpulse = math.saturate(_acousticImpactImpulse * math.max(0f, impactGraphMetallicBoost));
            float baseCutoff = math.lerp(underwaterGraphShallowCutoff, underwaterGraphDeepCutoff, depth01);
            float openedCutoff = math.min(interiorFallbackLowPassCutoff, baseCutoff + 2400f);
            AcousticGraphState state;
            state.LowPassCutoffHz = math.clamp(math.lerp(baseCutoff, openedCutoff, sonarImpulse), 500f, 22000f);
            state.LowPassResonanceQ = math.lerp(underwaterGraphResonance, underwaterGraphResonance + 0.18f, metallicImpulse);
            state.ReverbDecayTime = math.clamp(
                math.lerp(0.92f, underwaterGraphDecayTime, depth01) +
                (0.2f * sonarImpulse),
                0.05f,
                12f);
            state.ReflectionsLevelDb = math.clamp(
                underwaterGraphReflectionsLevel + (600f * sonarImpulse),
                -10000f,
                1000f);
            state.ReverbLevelDb = math.clamp(
                underwaterGraphReverbLevel + (300f * sonarImpulse) - (120f * metallicImpulse),
                -10000f,
                2000f);
            state.RoomHighFrequencyDb = math.clamp(
                math.lerp(underwaterGraphRoomHighFrequency, underwaterGraphRoomHighFrequency + 1200f, sonarImpulse),
                -10000f,
                0f);
            state.DryLevelDb = math.clamp(
                underwaterGraphDryLevel - (350f * depth01),
                -10000f,
                0f);
            state.Arm64AlignmentPad0 = 0f;
            ApplyEmitterOcclusionToAcousticState(ref state);
            return state;
        }

        private void ApplyEmitterOcclusionToAcousticState(ref AcousticGraphState state)
        {
            float occlusionShadow01 = math.saturate(1f - _emitterOcclusionTransmission01);
            if (occlusionShadow01 <= 0.0001f)
                return;

            float occludedCutoffHz = math.clamp(
                _emitterOcclusionLowPassCutoffHz,
                AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                AcousticOcclusionUtility.OpenLowPassCutoffHertz);

            state.LowPassCutoffHz = math.clamp(
                math.min(state.LowPassCutoffHz, math.lerp(state.LowPassCutoffHz, occludedCutoffHz, occlusionShadow01)),
                AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                AcousticOcclusionUtility.OpenLowPassCutoffHertz);
            state.LowPassResonanceQ = math.lerp(state.LowPassResonanceQ, state.LowPassResonanceQ + 0.18f, occlusionShadow01);
            state.ReflectionsLevelDb = math.clamp(state.ReflectionsLevelDb + (420f * occlusionShadow01), -10000f, 1000f);
            state.RoomHighFrequencyDb = math.clamp(state.RoomHighFrequencyDb - (2200f * occlusionShadow01), -10000f, 0f);
            state.DryLevelDb = math.clamp(state.DryLevelDb - (260f * occlusionShadow01), -10000f, 0f);
        }

        private float ResolveUnderwaterGraphDepth01()
        {
            bool hasMovementState = TryResolvePlayerMovementRuntimeState(out PlayerMovementRuntimeState movementState);
            bool hasRuntimeContext = HasPlayerRuntimeContext();
            HectonPlayerMovement movement = hasMovementState || hasRuntimeContext ? null : ResolvePlayerMovement();
            float depth = hasMovementState
                ? math.max(0f, movementState.DepthMeters)
                : ResolvePlayerDepthFallback();
            float immersion = hasMovementState
                ? ((movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.Underwater) != 0u ? 1f : 0f)
                : (!hasRuntimeContext && movement != null ? math.saturate(movement.WaterImmersionRatio) : 0f);
            float depth01 = math.saturate(depth / math.max(1f, acousticDeepWaterReferenceDepth));
            float immersion01 = math.saturate(
                (immersion - acousticExitImmersionRatio) /
                math.max(0.0001f, 1f - acousticExitImmersionRatio));
            return math.max(depth01, math.max(immersion01, ResolveSoundscapeTierDepth01()));
        }

        private float ResolveSoundscapeTierDepth01()
        {
            switch (_currentSoundscapeTier)
            {
                case SoundscapeTier.DeepAbyss:
                    return 1f;

                case SoundscapeTier.Abyss:
                    return 0.75f;

                case SoundscapeTier.Darkness:
                    return 0.52f;

                case SoundscapeTier.Thermal:
                    return 0.48f;

                case SoundscapeTier.Twilight:
                    return 0.24f;

                default:
                    return 0f;
            }
        }

        private bool ShouldUseSourceLevelAcousticFallback()
        {
            return enableSourceLevelAcousticFallback &&
                   enableRuntimeAcousticGraph &&
                   masterMixer != null &&
                   EnsureAcousticMixerParameterBindings();
        }

        private void ResetSourceLevelAcousticFallback()
        {
            if (!_listenerFallbackDefaultsCaptured)
            {
                _usingSourceLevelAcousticFallback = false;
                _acousticGraphStateInitialized = false;
                return;
            }

            if (_acousticMixerBindingsValid)
            {
                ApplyAcousticMixerState(
                    _listenerLowPassBaseCutoff,
                    _listenerLowPassBaseResonance,
                    _listenerReverbBaseDecayTime,
                    _listenerReverbBaseReflectionsLevel,
                    _listenerReverbBaseReverbLevel,
                    _listenerReverbBaseRoomHighFrequency,
                    _listenerReverbBaseDryLevel);
            }

            _currentAcousticLowPassCutoffHz = _listenerLowPassBaseCutoff;
            _currentAcousticLowPassResonanceQ = _listenerLowPassBaseResonance;
            _currentAcousticReverbDecayTime = _listenerReverbBaseDecayTime;
            _currentAcousticReflectionsLevelDb = _listenerReverbBaseReflectionsLevel;
            _currentAcousticReverbLevelDb = _listenerReverbBaseReverbLevel;
            _currentAcousticRoomHighFrequencyDb = _listenerReverbBaseRoomHighFrequency;
            _currentAcousticDryLevelDb = _listenerReverbBaseDryLevel;
            _emitterOcclusionTransmission01 = 1f;
            _emitterOcclusionLowPassCutoffHz = AcousticOcclusionUtility.OpenLowPassCutoffHertz;
            _acousticGraphStateInitialized = false;
            _usingSourceLevelAcousticFallback = false;
        }

        // ══════════════════════════════════════════════════════════
        //  SNAPSHOT BINDING / FALLBACKS
        // ══════════════════════════════════════════════════════════

        private void EnsureSnapshotBindings()
        {
            if (_snapshotBindingsResolved)
                return;

            _snapshotBindingsResolved = true;

            if (masterMixer == null)
                return;

            ResolveSnapshotBinding(ref underwaterSnapshot, "Underwater", "UnderwaterSnapshot");
            ResolveSnapshotBinding(ref baseInteriorSnapshot, "BaseInterior", "BaseInteriorSnapshot");
            ResolveSnapshotBinding(ref surfaceSnapshot, "Surface", "SurfaceSnapshot");
            ResolveSnapshotBinding(ref surfaceRainSnapshot, "SurfaceRain", "SurfaceRainSnapshot");
            ResolveSnapshotBinding(ref surfaceStormSnapshot, "SurfaceStorm", "SurfaceStormSnapshot");

            if (underwaterSnapshot == null &&
                baseInteriorSnapshot == null &&
                surfaceSnapshot == null &&
                surfaceRainSnapshot == null &&
                surfaceStormSnapshot == null)
            {
                LogSnapshotFallbackWarningOnce(
                    ref _warnedMissingSnapshotCoverage,
                    "[AcousticZoneController] MasterMixer is assigned but no authored acoustic snapshots were resolved by name. Expected names include Underwater/UnderwaterSnapshot, BaseInterior/BaseInteriorSnapshot, Surface/SurfaceSnapshot, SurfaceRain/SurfaceRainSnapshot, SurfaceStorm/SurfaceStormSnapshot.");
            }

#if UNITY_EDITOR
            ValidateMixerAuthoringCoverage();
#endif
        }

        private void ResolveSnapshotBinding(
            ref AudioMixerSnapshot snapshot,
            string primaryName,
            string alternateName)
        {
            if (snapshot != null || masterMixer == null)
                return;

            snapshot = masterMixer.FindSnapshot(primaryName);
            if (snapshot == null && !string.IsNullOrEmpty(alternateName))
                snapshot = masterMixer.FindSnapshot(alternateName);
        }

        private bool TransitionToResolvedSnapshot(AcousticZoneState zone, float duration)
        {
            // L19 hop2: never arm FMOD snapshot DSP under batchmode/headless.
            if (Application.isBatchMode)
                return false;

            if (!_isLateFramePresentationPhase)
            {
                QueuePendingSnapshotTransition(zone, duration);
                return false;
            }

            EnsureSnapshotBindings();
            bool blendResolved = false;

            if (zone == AcousticZoneState.Surface &&
                TryTransitionSurfaceSnapshotBlend(duration, out blendResolved))
            {
                LogDiagnostic("[AcousticZoneController] Snapshot activated: SurfaceBlend");
                return true;
            }

            if (zone == AcousticZoneState.Surface && blendResolved)
            {
                return false;
            }

            AudioMixerSnapshot snapshot = ResolveSnapshotForZone(zone);
            if (snapshot == null)
            {
                LogSnapshotFallbackWarningOnce(
                    ref _warnedMissingSnapshotCoverage,
                    "[AcousticZoneController] No valid snapshot could be resolved for the requested acoustic zone. Mixer state will remain unchanged.");
                return false;
            }

            if (IsResolvedSnapshotAlreadyActive(zone, snapshot))
                return false;

            if (IsSnapshotTransitionLocked())
            {
                QueuePendingSnapshotTransition(zone, duration);
                return false;
            }

            float transitionTime = math.max(0f, duration);
            snapshot.TransitionTo(transitionTime);
            ArmSnapshotTransitionLock(transitionTime);
            CacheResolvedSnapshotState(zone, snapshot);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogDiagnostic("[AcousticZoneController] Snapshot activated.");
#endif
            return true;
        }

        private bool TryTransitionSurfaceSnapshotBlend(float duration, out bool blendResolved)
        {
            blendResolved = false;

            if (masterMixer == null || surfaceSnapshot == null)
                return false;

            int snapshotCount = 0;
            float totalWeight = 0f;

            _surfaceBlendSnapshots[snapshotCount] = surfaceSnapshot;
            _surfaceBlendWeights[snapshotCount] = 1f;
            totalWeight += 1f;
            snapshotCount++;

            if (surfaceRainSnapshot != null && _surfacePrecipitationIntensity >= 0.2f)
            {
                float rainWeight = math.saturate(_surfacePrecipitationIntensity) * surfaceRainSnapshotWeight;
                if (rainWeight > 0.001f)
                {
                    _surfaceBlendSnapshots[snapshotCount] = surfaceRainSnapshot;
                    _surfaceBlendWeights[snapshotCount] = rainWeight;
                    totalWeight += rainWeight;
                    snapshotCount++;
                }
            }

            if (surfaceStormSnapshot != null && _surfaceElectricalActivity >= 0.55f)
            {
                float stormWeight = math.saturate(_surfaceElectricalActivity) * surfaceStormSnapshotWeight;
                if (stormWeight > 0.001f)
                {
                    _surfaceBlendSnapshots[snapshotCount] = surfaceStormSnapshot;
                    _surfaceBlendWeights[snapshotCount] = stormWeight;
                    totalWeight += stormWeight;
                    snapshotCount++;
                }
            }

            if (snapshotCount <= 1 || totalWeight <= 0.001f)
                return false;

            for (int i = 0; i < snapshotCount; i++)
                _surfaceBlendWeights[i] /= totalWeight;

            ClearBlendTail(_surfaceBlendSnapshots, _surfaceBlendWeights, snapshotCount);

            blendResolved = true;
            if (IsActiveSurfaceBlendEquivalent(snapshotCount))
                return false;

            float transitionTime = math.max(0f, surfaceWeatherTransitionDuration > 0f ? surfaceWeatherTransitionDuration : duration);
            if (IsSnapshotTransitionLocked())
            {
                QueuePendingSnapshotTransition(AcousticZoneState.Surface, transitionTime);
                return false;
            }

            masterMixer.TransitionToSnapshots(_surfaceBlendSnapshots, _surfaceBlendWeights, transitionTime);
            ArmSnapshotTransitionLock(transitionTime);
            CacheSurfaceBlendState(snapshotCount);
            return true;
        }

        private static bool ApproximatelyEqual(float a, float b)
        {
            return math.abs(a - b) <= SurfaceWeatherStateEpsilon;
        }

        private static void ClearBlendTail(AudioMixerSnapshot[] snapshots, float[] weights, int startIndex)
        {
            for (int i = startIndex; i < snapshots.Length; i++)
            {
                snapshots[i] = null;
                weights[i] = 0f;
            }
        }

        private static float ResolvePresentationClockSeconds()
        {
            return (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
        }

        private bool IsSnapshotTransitionLocked()
        {
            return ResolvePresentationClockSeconds() < _snapshotTransitionLockUntilTime;
        }

        private void ArmSnapshotTransitionLock(float duration)
        {
            if (duration <= 0f)
                return;

            float unlockTime = ResolvePresentationClockSeconds() + duration;
            if (unlockTime > _snapshotTransitionLockUntilTime)
                _snapshotTransitionLockUntilTime = unlockTime;
        }

        private void QueuePendingSnapshotTransition(AcousticZoneState zone, float duration)
        {
            _pendingSnapshotZone = zone;
            _pendingSnapshotDuration = math.max(0f, duration);
            _hasPendingSnapshotTransition = true;
        }

        private void ProcessPendingSnapshotTransition()
        {
            if (!_hasPendingSnapshotTransition || IsSnapshotTransitionLocked())
                return;

            AcousticZoneState pendingZone = _pendingSnapshotZone;
            float pendingDuration = _pendingSnapshotDuration;
            _hasPendingSnapshotTransition = false;
            _pendingSnapshotDuration = 0f;
            TransitionToResolvedSnapshot(pendingZone, pendingDuration);
        }

        private bool IsResolvedSnapshotAlreadyActive(AcousticZoneState zone, AudioMixerSnapshot snapshot)
        {
            return _hasActiveResolvedSnapshotState &&
                   !_activeSurfaceBlendState &&
                   _activeResolvedZone == zone &&
                   _activeResolvedSnapshot == snapshot;
        }

        private bool IsActiveSurfaceBlendEquivalent(int snapshotCount)
        {
            if (!_hasActiveResolvedSnapshotState ||
                !_activeSurfaceBlendState ||
                _activeResolvedZone != AcousticZoneState.Surface ||
                _activeSurfaceBlendSnapshotCount != snapshotCount)
            {
                return false;
            }

            for (int i = 0; i < snapshotCount; i++)
            {
                if (_activeSurfaceBlendSnapshots[i] != _surfaceBlendSnapshots[i] ||
                    !ApproximatelyEqual(_activeSurfaceBlendWeights[i], _surfaceBlendWeights[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private void CacheResolvedSnapshotState(AcousticZoneState zone, AudioMixerSnapshot snapshot)
        {
            _hasActiveResolvedSnapshotState = true;
            _activeSurfaceBlendState = false;
            _activeResolvedZone = zone;
            _activeResolvedSnapshot = snapshot;
            _activeSurfaceBlendSnapshotCount = 0;
            ClearBlendTail(_activeSurfaceBlendSnapshots, _activeSurfaceBlendWeights, 0);
        }

        private void CacheSurfaceBlendState(int snapshotCount)
        {
            _hasActiveResolvedSnapshotState = true;
            _activeSurfaceBlendState = true;
            _activeResolvedZone = AcousticZoneState.Surface;
            _activeResolvedSnapshot = null;
            _activeSurfaceBlendSnapshotCount = snapshotCount;

            for (int i = 0; i < snapshotCount; i++)
            {
                _activeSurfaceBlendSnapshots[i] = _surfaceBlendSnapshots[i];
                _activeSurfaceBlendWeights[i] = _surfaceBlendWeights[i];
            }

            ClearBlendTail(_activeSurfaceBlendSnapshots, _activeSurfaceBlendWeights, snapshotCount);
        }

        private bool HasAppliedAcousticMixerState(
            float lowPassCutoffHz,
            float lowPassResonanceQ,
            float reverbDecayTime,
            float reflectionsLevelDb,
            float reverbLevelDb,
            float roomHighFrequencyDb,
            float dryLevelDb)
        {
            return !float.IsNaN(_lastAppliedAcousticLowPassCutoffHz) &&
                   math.abs(_lastAppliedAcousticLowPassCutoffHz - lowPassCutoffHz) <= AcousticCutoffWriteEpsilonHz &&
                   math.abs(_lastAppliedAcousticLowPassResonanceQ - lowPassResonanceQ) <= AcousticResonanceWriteEpsilon &&
                   math.abs(_lastAppliedAcousticReverbDecayTime - reverbDecayTime) <= AcousticDecayWriteEpsilonSeconds &&
                   math.abs(_lastAppliedAcousticReflectionsLevelDb - reflectionsLevelDb) <= AcousticDbWriteEpsilon &&
                   math.abs(_lastAppliedAcousticReverbLevelDb - reverbLevelDb) <= AcousticDbWriteEpsilon &&
                   math.abs(_lastAppliedAcousticRoomHighFrequencyDb - roomHighFrequencyDb) <= AcousticDbWriteEpsilon &&
                   math.abs(_lastAppliedAcousticDryLevelDb - dryLevelDb) <= AcousticDbWriteEpsilon;
        }

        private void CacheAppliedAcousticMixerState(
            float lowPassCutoffHz,
            float lowPassResonanceQ,
            float reverbDecayTime,
            float reflectionsLevelDb,
            float reverbLevelDb,
            float roomHighFrequencyDb,
            float dryLevelDb)
        {
            _lastAppliedAcousticLowPassCutoffHz = lowPassCutoffHz;
            _lastAppliedAcousticLowPassResonanceQ = lowPassResonanceQ;
            _lastAppliedAcousticReverbDecayTime = reverbDecayTime;
            _lastAppliedAcousticReflectionsLevelDb = reflectionsLevelDb;
            _lastAppliedAcousticReverbLevelDb = reverbLevelDb;
            _lastAppliedAcousticRoomHighFrequencyDb = roomHighFrequencyDb;
            _lastAppliedAcousticDryLevelDb = dryLevelDb;
        }

        private void InvalidateAppliedAcousticMixerStateCache()
        {
            _lastAppliedAcousticLowPassCutoffHz = float.NaN;
            _lastAppliedAcousticLowPassResonanceQ = float.NaN;
            _lastAppliedAcousticReverbDecayTime = float.NaN;
            _lastAppliedAcousticReflectionsLevelDb = float.NaN;
            _lastAppliedAcousticReverbLevelDb = float.NaN;
            _lastAppliedAcousticRoomHighFrequencyDb = float.NaN;
            _lastAppliedAcousticDryLevelDb = float.NaN;
        }

        private AudioMixerSnapshot ResolveSnapshotForZone(AcousticZoneState zone)
        {
            switch (zone)
            {
                case AcousticZoneState.Interior:
                    if (baseInteriorSnapshot != null)
                        return baseInteriorSnapshot;

                    if (!HasAnyResolvedSnapshotCoverage())
                        return null;

                    LogSnapshotFallbackWarningOnce(
                        ref _warnedMissingInteriorSnapshot,
                        "[AcousticZoneController] BaseInteriorSnapshot missing. Falling back to exterior snapshot coverage.");
                    return ResolveSurfaceSnapshot() ?? underwaterSnapshot;

                case AcousticZoneState.Surface:
                    return ResolveSurfaceSnapshot();

                default:
                    if (underwaterSnapshot != null)
                        return underwaterSnapshot;

                    if (!HasAnyResolvedSnapshotCoverage())
                        return null;

                    LogSnapshotFallbackWarningOnce(
                        ref _warnedMissingUnderwaterSnapshot,
                        "[AcousticZoneController] UnderwaterSnapshot missing. Falling back to surface/interior snapshot coverage.");
                    return ResolveSurfaceSnapshot() ?? baseInteriorSnapshot;
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogSnapshotFallbackWarningOnce(ref bool warnedFlag, string message)
        {
            if (warnedFlag)
                return;

            warnedFlag = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogWarning(message, this);
#endif
        }

        private bool HasAnyResolvedSnapshotCoverage()
        {
            return underwaterSnapshot != null ||
                   baseInteriorSnapshot != null ||
                   surfaceSnapshot != null ||
                   surfaceRainSnapshot != null ||
                   surfaceStormSnapshot != null;
        }

        private string BuildSnapshotCoverageSummary()
        {
            return HasAnyResolvedSnapshotCoverage() ? "Snapshot: Partial/Ready" : "Snapshot: None";
        }

        private string BuildMixerCoverageSummary()
        {
            if (masterMixer == null)
                return "Mixer: None";

            return _acousticMixerBindingsValid ? "Mixer: Valid" : "Mixer: Incomplete";
        }

        private static string ResolveSmallCountLabel(int value)
        {
            switch (value)
            {
                case 0:
                    return "0";
                case 1:
                    return "1";
                case 2:
                    return "2";
                case 3:
                    return "3";
                case 4:
                    return "4";
                case 5:
                    return "5";
                case 6:
                    return "6";
                case 7:
                    return "7";
                case 8:
                    return "8";
                default:
                    return "9+";
            }
        }

        private static string ResolveSoundscapeTierLabel(SoundscapeTier tier)
        {
            switch (tier)
            {
                case SoundscapeTier.Shallow:
                    return ShallowSoundscapeTierLabel;
                case SoundscapeTier.Twilight:
                    return TwilightSoundscapeTierLabel;
                case SoundscapeTier.Darkness:
                    return DarknessSoundscapeTierLabel;
                case SoundscapeTier.Abyss:
                    return AbyssSoundscapeTierLabel;
                case SoundscapeTier.DeepAbyss:
                    return DeepAbyssSoundscapeTierLabel;
                case SoundscapeTier.Thermal:
                    return ThermalSoundscapeTierLabel;
                default:
                    return SurfaceSoundscapeTierLabel;
            }
        }

#if UNITY_EDITOR
        private void Reset()
        {
            TryAssignEditorAuthoringDefaults();
        }

        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (transitionDuration < 0f) transitionDuration = 0f;
            if (interiorTransitionDuration < 0f) interiorTransitionDuration = 0f;
            if (surfaceTransitionDuration < 0f) surfaceTransitionDuration = 0f;
            if (underwaterTransitionDuration < 0f) underwaterTransitionDuration = 0f;
            if (surfaceWeatherTransitionDuration < 0f) surfaceWeatherTransitionDuration = 0f;
            if (acousticEnterUnderwaterDepth < 0f) acousticEnterUnderwaterDepth = 0f;
            if (acousticExitUnderwaterDepth < 0f) acousticExitUnderwaterDepth = 0f;
            if (acousticExitUnderwaterDepth > acousticEnterUnderwaterDepth) acousticExitUnderwaterDepth = acousticEnterUnderwaterDepth;
            if (acousticEnterImmersionRatio < 0.1f) acousticEnterImmersionRatio = 0.1f;
            if (acousticEnterImmersionRatio > 1f) acousticEnterImmersionRatio = 1f;
            if (acousticExitImmersionRatio < 0.05f) acousticExitImmersionRatio = 0.05f;
            if (acousticExitImmersionRatio > acousticEnterImmersionRatio) acousticExitImmersionRatio = acousticEnterImmersionRatio;
            if (acousticForceUnderwaterDepth < acousticEnterUnderwaterDepth) acousticForceUnderwaterDepth = acousticEnterUnderwaterDepth;
            if (exteriorTransitionDebounce < 0f) exteriorTransitionDebounce = 0f;
            if (exteriorTransitionHoldTime < 0f) exteriorTransitionHoldTime = 0f;
            if (transitionVolume < 0f) transitionVolume = 0f;
            if (transitionVolume > 1f) transitionVolume = 1f;
            if (stormStaticElectricalThreshold < 0f) stormStaticElectricalThreshold = 0f;
            if (stormStaticElectricalThreshold > 1f) stormStaticElectricalThreshold = 1f;
            if (stormStaticIntervalMax < 0.1f) stormStaticIntervalMax = 0.1f;
            if (stormStaticIntervalMin < 0.1f) stormStaticIntervalMin = 0.1f;
            if (stormStaticIntervalMin > stormStaticIntervalMax) stormStaticIntervalMin = stormStaticIntervalMax;
            if (stormStaticVolumeMin < 0f) stormStaticVolumeMin = 0f;
            if (stormStaticVolumeMin > 1f) stormStaticVolumeMin = 1f;
            if (stormStaticVolumeMax < stormStaticVolumeMin) stormStaticVolumeMax = stormStaticVolumeMin;
            if (stormStaticVolumeMax > 1f) stormStaticVolumeMax = 1f;
            if (stormStaticUnderwaterVolumeScale < 0f) stormStaticUnderwaterVolumeScale = 0f;
            if (stormStaticUnderwaterVolumeScale > 1f) stormStaticUnderwaterVolumeScale = 1f;
            if (stormAmbientDuckMax < 0f) stormAmbientDuckMax = 0f;
            if (stormAmbientDuckMax > 0.5f) stormAmbientDuckMax = 0.5f;
            if (musicAmbientDuckMax < 0f) musicAmbientDuckMax = 0f;
            if (musicAmbientDuckMax > 0.4f) musicAmbientDuckMax = 0.4f;
            if (musicAmbientDuckAttackSharpness < 0.25f) musicAmbientDuckAttackSharpness = 0.25f;
            if (musicAmbientDuckAttackSharpness > 20f) musicAmbientDuckAttackSharpness = 20f;
            if (musicAmbientDuckReleaseSharpness < 0.25f) musicAmbientDuckReleaseSharpness = 0.25f;
            if (musicAmbientDuckReleaseSharpness > 20f) musicAmbientDuckReleaseSharpness = 20f;
            if (explorationMusicAmbientDuckWeight < 0f) explorationMusicAmbientDuckWeight = 0f;
            if (explorationMusicAmbientDuckWeight > 1f) explorationMusicAmbientDuckWeight = 1f;
            if (baseMusicAmbientDuckWeight < 0f) baseMusicAmbientDuckWeight = 0f;
            if (baseMusicAmbientDuckWeight > 1f) baseMusicAmbientDuckWeight = 1f;
            if (tenseMusicAmbientDuckWeight < 0f) tenseMusicAmbientDuckWeight = 0f;
            if (tenseMusicAmbientDuckWeight > 1f) tenseMusicAmbientDuckWeight = 1f;
            if (foregroundMusicAmbientDuckWeight < 0f) foregroundMusicAmbientDuckWeight = 0f;
            if (foregroundMusicAmbientDuckWeight > 1f) foregroundMusicAmbientDuckWeight = 1f;
            if (stormAmbientPitchDropMax < 0f) stormAmbientPitchDropMax = 0f;
            if (stormAmbientPitchDropMax > 0.25f) stormAmbientPitchDropMax = 0.25f;
            if (stormAmbientPitchFlutterMax < 0f) stormAmbientPitchFlutterMax = 0f;
            if (stormAmbientPitchFlutterMax > 0.15f) stormAmbientPitchFlutterMax = 0.15f;
            if (stormAmbientFlutterFrequencyMin < 0.1f) stormAmbientFlutterFrequencyMin = 0.1f;
            if (stormAmbientFlutterFrequencyMax < stormAmbientFlutterFrequencyMin) stormAmbientFlutterFrequencyMax = stormAmbientFlutterFrequencyMin;
            if (interiorFallbackReverbDryLevel < -10000f) interiorFallbackReverbDryLevel = -10000f;
            if (interiorFallbackReverbDryLevel > 0f) interiorFallbackReverbDryLevel = 0f;
            _snapshotBindingsResolved = false;
            ResetAuthoringWarnings();
            TryAssignEditorAuthoringDefaults();
            EnsureSnapshotBindings();
        }

        private void TryAssignEditorAuthoringDefaults()
        {
            if (masterMixer == null)
                masterMixer = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioMixer>(DefaultMasterMixerPath);

            if (waterDrainSound == null)
                waterDrainSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(DefaultWaterDrainSoundPath);

            if (waterFillSound == null)
                waterFillSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(DefaultWaterFillSoundPath);

            if (stormStaticPrimary == null)
                stormStaticPrimary = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(DefaultStormStaticPrimaryPath);

            if (stormStaticSecondary == null)
                stormStaticSecondary = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(DefaultStormStaticSecondaryPath);
        }

        private void ResetAuthoringWarnings()
        {
            _warnedMissingInteriorSnapshot = false;
            _warnedMissingUnderwaterSnapshot = false;
            _warnedMissingSurfaceSnapshotSet = false;
            _warnedMissingSnapshotCoverage = false;
            _warnedIncompleteMixerSnapshotAuthoring = false;
            _validatedMixerSnapshotCount = 0;
            _validatedMixerHasNamedCoverage = false;
            _validatedMixerHasEffectGraph = false;
        }

        private void ValidateMixerAuthoringCoverage()
        {
            if (masterMixer == null)
                return;

            string mixerAssetPath = UnityEditor.AssetDatabase.GetAssetPath(masterMixer);
            if (string.IsNullOrEmpty(mixerAssetPath))
                return;

            UnityEngine.Object[] mixerSubAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(mixerAssetPath);
            if (mixerSubAssets == null || mixerSubAssets.Length <= 0)
                return;

            int snapshotCount = 0;
            bool hasNamedCoverage = false;
            bool hasNonAttenuationEffect = false;

            for (int i = 0; i < mixerSubAssets.Length; i++)
            {
                UnityEngine.Object subAsset = mixerSubAssets[i];
                if (subAsset == null)
                    continue;

                global::System.Type subAssetType = subAsset.GetType();
                if (subAssetType == null)
                    continue;

                string typeName = subAssetType.Name;
                if (typeName == "AudioMixerSnapshotController")
                {
                    snapshotCount++;
                    string snapshotName = subAsset.name;
                    if (snapshotName == "Underwater" ||
                        snapshotName == "UnderwaterSnapshot" ||
                        snapshotName == "BaseInterior" ||
                        snapshotName == "BaseInteriorSnapshot" ||
                        snapshotName == "Surface" ||
                        snapshotName == "SurfaceSnapshot" ||
                        snapshotName == "SurfaceRain" ||
                        snapshotName == "SurfaceRainSnapshot" ||
                        snapshotName == "SurfaceStorm" ||
                        snapshotName == "SurfaceStormSnapshot")
                    {
                        hasNamedCoverage = true;
                    }

                    continue;
                }

                if (typeName != "AudioMixerEffectController")
                    continue;

                SerializedObject effectSerializedObject = new SerializedObject(subAsset);
                SerializedProperty effectNameProperty = effectSerializedObject.FindProperty("m_EffectName");
                if (effectNameProperty == null)
                    continue;

                string effectName = effectNameProperty.stringValue;
                if (!string.IsNullOrEmpty(effectName) && effectName != "Attenuation")
                    hasNonAttenuationEffect = true;
            }

            _validatedMixerSnapshotCount = snapshotCount;
            _validatedMixerHasNamedCoverage = hasNamedCoverage;
            _validatedMixerHasEffectGraph = hasNonAttenuationEffect;

            if (snapshotCount <= 1 || !hasNamedCoverage)
            {
                LogSnapshotFallbackWarningOnce(
                    ref _warnedIncompleteMixerSnapshotAuthoring,
                    "[AcousticZoneController] MasterMixer snapshot authoring is incomplete. Expected named coverage includes Underwater, BaseInterior, Surface, SurfaceRain, and SurfaceStorm.");
            }

        }
#endif
    
        #region JulesLink_AcousticZoneReverbDecay
        private static void JulesLink_AcousticZoneReverbDecay() { _ = typeof(Hecton8.PureLogic.Systems.AcousticZoneReverbDecay); }
        #endregion
}
}
