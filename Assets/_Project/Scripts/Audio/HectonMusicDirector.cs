using System;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Systems.AI;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace Hecton8.Audio
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-3900)] // Consumes zone/acoustic state resolved by earlier managers.
    public sealed class HectonMusicDirector : MonoBehaviour, ITickable, IUpdatable, ISlowTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private static int s_x001HectonMusicDirectorSignalPushDropCount;
        private static HectonMusicDirector s_activeRuntimeInstance;
        private enum PlaybackState : byte
        {
            Silent = 0,
            Waiting = 1,
            Playing = 2,
            Override = 3
        }

        private enum StingerKind : byte
        {
            Discovery = 0,
            Danger = 1,
            Recovery = 2
        }

        public enum MusicActivityReason : byte
        {
            Silent = 0,
            Rest = 1,
            Exploration = 2,
            Base = 3,
            Tense = 4,
            Combat = 5,
            Menu = 6,
            Prologue = 7,
            Override = 8,
            Emergency = 9
        }

        private const int MusicVoiceCount = 2;
        private const int InvalidVoiceIndex = -1;
        private const float MixerFloorDb = -80f;
        private const float MixerCeilingDb = 0f;
        private const float EditorDebugStateIntervalSeconds = 0.25f;
        private const int DependencyRetryFrameInterval = 30;
        private const float StormDepthAttenuationInv = 0.008333333f;
        private const float AuthoredPressureRangeInv = 0.25f;
        private const float Random24ToUnit = 0.000000059604648f;
        private const double AupRuntimeFloatClampMeters = 3.4028234663852886E+38d;
        private const float EmergencyBreathDominatesThreshold = 0.9f;
        private const float CriticalPlayerStressDominatesThreshold = 0.88f;
        private const int PlayerStressSignalHoldFrames = 8;
        private const float VocalWarningMusicDuckDefault01 = 0.38f;
        private const float VocalWarningMusicDuckCritical01 = 0.62f;
        private const float NarrativeAudioLogMusicDuck01 = 0.48f;
        private const int RuntimeDirectorPoolReserveCount = 1;
        private static readonly bool ProceduralSynthOwnsMusicPlayback = true;
        private static readonly int _PredatorThreatLayerMask = HectonLayerMasks.CreatureLayerMask;

        private static readonly string[] MenuSceneTokens = { "main_menu" };
        // "orbit" is load-bearing: the prologue ships as the scene named 01_ORBIT
        // (PrologueOrbitSceneBootstrap lives in it, PrologueSequenceRegistryBridge calls it
        // StandaloneOrbitSceneName). Matching only "prologue" matched no scene that exists, so
        // _prologueSceneActive was permanently false - taking six behaviours in this class with it
        // and leaving the authored MusicProfile_Prologue unreachable. "prologue" is kept so a scene
        // actually named that still matches.
        private static readonly string[] PrologueSceneTokens = { "prologue", "orbit" };
        private static readonly string[] BaseTokens = { "base", "service", "fabric", "power", "construction", "module", "hab" };
        private static readonly string[] CaveTokens =
        {
            "cave", "cavern", "grotto", "tunnel", "labyrinth",
            "chamber", "hollow", "fissure", "catacomb", "vault", "entrance"
        };

        private static readonly string[] ThermalTokens =
        {
            "thermal", "termal", "hydrothermal", "vent", "volcanic", "magma", "lava",
            "basalt", "brine", "chemo", "chemosynthetic", "seam", "spire", "pillow", "flux", "ash"
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeInstance()
        {
            if (!Application.isPlaying || ResolveUsableRuntime() != null)
                return;

            TryInstantiateConfiguredRuntimeDirector(SceneManager.GetActiveScene(), false);
        }

        internal static void EnsureRuntimeInstanceForScene(Scene scene)
        {
            if (!Application.isPlaying || ResolveUsableRuntime() != null)
                return;

            TryInstantiateConfiguredRuntimeDirector(scene, true);
        }

        private static HectonMusicDirector ResolveUsableRuntime()
        {
            HectonMusicDirector registered = GlobalRegistry.MusicDirector;
            if (IsMusicDirectorRuntimeUsable(registered))
            {
                s_activeRuntimeInstance = registered;
                return registered;
            }

            if (!ReferenceEquals(registered, null))
            {
                GlobalRegistry.UnregisterMusicDirectorRuntime(registered);
                if (ReferenceEquals(s_activeRuntimeInstance, registered))
                    s_activeRuntimeInstance = null;
            }

            HectonMusicDirector active = s_activeRuntimeInstance;
            if (IsMusicDirectorRuntimeUsable(active))
            {
                GlobalRegistry.RegisterMusicDirectorRuntime(active);
                s_activeRuntimeInstance = active;
                return active;
            }

            if (!ReferenceEquals(active, null))
            {
                GlobalRegistry.UnregisterMusicDirectorRuntime(active);
                if (ReferenceEquals(s_activeRuntimeInstance, active))
                    s_activeRuntimeInstance = null;
            }

            return null;
        }

        [Header("References")]
        [Tooltip("Optional explicit world zone director. If null, runtime instance is used.")]
        [SerializeField] private WorldZoneDirector _worldZoneDirector;

        [Tooltip("Optional explicit biome matrix director. If null, runtime instance is used.")]
        [SerializeField] private BiomeMatrixDirector _biomeMatrixDirector;

        [Tooltip("Optional explicit depth-zone read model. If null, runtime instance is used when available.")]
        [SerializeField] private MonoBehaviour _depthZoneDirector;

        [Tooltip("Optional explicit AI director reference. If null, runtime instance is used when available.")]
        [SerializeField] private HectonDirectorAI _directorAI;

        [Tooltip("Optional authored runtime voice owner. If null, the director resolves an owned MusicVoicePool component.")]
        [SerializeField] private MusicVoicePool _voicePool;

        [Header("Profiles")]
        [Tooltip("Profile used in the main menu scene.")]
        [SerializeField] private HectonMusicBiomeProfile _mainMenuProfile;

        [Tooltip("Profile used in a prologue scene.")]
        [SerializeField] private HectonMusicBiomeProfile _prologueProfile;

        [Tooltip("Profile used for shallow water.")]
        [SerializeField] private HectonMusicBiomeProfile _shallowProfile;

        [Tooltip("Profile used for shelf and mid-depth water.")]
        [SerializeField] private HectonMusicBiomeProfile _shelfProfile;

        [Tooltip("Profile used for abyssal water.")]
        [SerializeField] private HectonMusicBiomeProfile _abyssProfile;

        [Tooltip("Profile used for cave contexts.")]
        [SerializeField] private HectonMusicBiomeProfile _caveProfile;

        [Tooltip("Profile used for thermal and volcanic contexts.")]
        [SerializeField] private HectonMusicBiomeProfile _thermalProfile;

        [Tooltip("Profile used for base interiors and service-heavy base zones.")]
        [SerializeField] private HectonMusicBiomeProfile _baseProfile;

        [Tooltip("Profile used when combat escalation latches in.")]
        [SerializeField] private HectonMusicBiomeProfile _combatProfile;

        [Tooltip("Fallback profile used when no better route resolves.")]
        [SerializeField] private HectonMusicBiomeProfile _fallbackProfile;

        [Header("Mixer Routing")]
        [Tooltip("Dedicated music mixer group. If null, AmbientGroup from SpatialAudioManager is used when available.")]
        [SerializeField] private AudioMixerGroup _musicMixerGroup;

        [Tooltip("Dedicated stinger mixer group. If null, the music group is reused.")]
        [SerializeField] private AudioMixerGroup _stingerMixerGroup;

        [Tooltip("Exposed mixer parameter controlling the rhythmic music layer in dB.")]
        [SerializeField] private string _rhythmLayerParameter = "MusicLayer_Rhythm_dB";

        [Tooltip("Exposed mixer parameter controlling the low-end bass layer in dB.")]
        [SerializeField] private string _bassLayerParameter = "MusicLayer_Bass_dB";

        [Tooltip("Exposed mixer parameter controlling the ambient texture layer in dB.")]
        [SerializeField] private string _atmosphereLayerParameter = "MusicLayer_Atmosphere_dB";

        [Tooltip("Exposed mixer parameter controlling the danger layer in dB.")]
        [SerializeField] private string _dangerLayerParameter = "MusicLayer_Danger_dB";

        [Tooltip("Attack speed for music-layer mixer routing.")]
        [SerializeField, Min(0.01f)] private float _layerAttackSpeed = 2.8f;

        [Tooltip("Release speed for music-layer mixer routing.")]
        [SerializeField, Min(0.01f)] private float _layerReleaseSpeed = 1.4f;

        [Tooltip("Radius used to sample nearby aggressive fauna for danger-layer routing.")]
        [SerializeField, Min(1f)] private float _predatorSenseRadius = 70f;

        [Header("Thresholds")]
        [Tooltip("Tension threshold that enters combat routing.")]
        [SerializeField, Range(0f, 1f)] private float _combatEnterThreshold = 0.70f;

        [Tooltip("Tension threshold that exits combat routing.")]
        [SerializeField, Range(0f, 1f)] private float _combatExitThreshold = 0.48f;

        [Tooltip("Tension threshold used to pick tense exploration pools outside the combat latch.")]
        [SerializeField, Range(0f, 1f)] private float _tenseExplorationThreshold = 0.50f;

        [Tooltip("Release threshold for tense exploration routing. Keeps calm/tense pool choice from thrashing near the boundary.")]
        [SerializeField, Range(0f, 1f)] private float _tenseExplorationReleaseThreshold = 0.36f;

        [Header("World Tension Model")]
        [Tooltip("Weight of DirectorAI tension inside the final music-tension blend.")]
        [SerializeField, Range(0f, 1.5f)] private float _aiTensionWeight = 0.78f;

        [Tooltip("Weight of authored biome survival/route pressure inside the final music-tension blend.")]
        [SerializeField, Range(0f, 0.8f)] private float _biomePressureWeight = 0.28f;

        [Tooltip("Weight of zone-kind pressure inside the final music-tension blend.")]
        [SerializeField, Range(0f, 0.8f)] private float _zonePressureWeight = 0.24f;

        [Tooltip("Weight of depth-zone danger and deep-environment pressure inside the final music-tension blend.")]
        [SerializeField, Range(0f, 0.8f)] private float _depthZonePressureWeight = 0.26f;

        [Tooltip("Weight of the current soundscape depth tier inside the final music-tension blend.")]
        [SerializeField, Range(0f, 0.5f)] private float _soundscapePressureWeight = 0.10f;

        [Tooltip("Small exploration unease contributed by strong reward-pull biomes.")]
        [SerializeField, Range(0f, 0.5f)] private float _rewardUneaseWeight = 0.12f;

        [Tooltip("Tension suppressed while the context reads as a safe pocket, service node, or other recovery space.")]
        [SerializeField, Range(0f, 0.8f)] private float _safePocketSuppressionWeight = 0.22f;

        [Tooltip("Additional tension used during the early spine so deeper, riskier water reads less emotionally flat before the first module route lands.")]
        [SerializeField, Range(0f, 0.5f)] private float _firstHourPressureBoostWeight = 0.18f;

        [Tooltip("Maximum tension scale applied while music is explicitly in an interior/base context.")]
        [SerializeField, Range(0.1f, 1f)] private float _baseContextTensionScale = 0.42f;

        [Header("Ducking")]
        [Tooltip("Bed attenuation while a stinger is playing.")]
        [SerializeField, Range(0.05f, 1f)] private float _stingerDuckFactor = 0.40f;

        [Tooltip("Attack time for bed ducking.")]
        [SerializeField, Min(0.01f)] private float _stingerDuckAttackSeconds = 0.12f;

        [Tooltip("Release time for bed ducking.")]
        [SerializeField, Min(0.01f)] private float _stingerDuckReleaseSeconds = 0.40f;

        [Header("Stinger Cooldowns")]
        [Tooltip("Cooldown after a discovery stinger before another discovery stinger may fire.")]
        [SerializeField, Min(0f)] private float _discoveryStingerCooldownSeconds = 75f;

        [Tooltip("Cooldown after a danger stinger before another danger stinger may fire.")]
        [SerializeField, Min(0f)] private float _dangerStingerCooldownSeconds = 35f;

        [Tooltip("Cooldown after a recovery stinger before another recovery stinger may fire.")]
        [SerializeField, Min(0f)] private float _recoveryStingerCooldownSeconds = 50f;

        [Header("Depth Blending")]
        [Tooltip("Meters near a major depth boundary where adjacent biome music may bleed in dynamically.")]
        [SerializeField, Min(0f)] private float _depthBlendWindowMeters = 180f;

        [Tooltip("Maximum dynamic weight injected for adjacent-depth music near a boundary.")]
        [SerializeField, Range(0, 30)] private int _depthBlendMaxWeight = 18;

        [Header("Procedural Music Phrasing")]
        [Tooltip("Minimum length of a procedural exploration phrase before the director yields back to world sound.")]
        [SerializeField, Min(1f)] private float _proceduralExplorationPhraseMinSeconds = 18f;

        [Tooltip("Maximum length of a procedural exploration phrase before the director yields back to world sound.")]
        [SerializeField, Min(1f)] private float _proceduralExplorationPhraseMaxSeconds = 42f;

        [Tooltip("Minimum silence after a procedural phrase when the active profile has no authored pause window.")]
        [SerializeField, Min(0f)] private float _proceduralFallbackRestMinSeconds = 45f;

        [Tooltip("Maximum silence after a procedural phrase when the active profile has no authored pause window.")]
        [SerializeField, Min(0f)] private float _proceduralFallbackRestMaxSeconds = 95f;

        [Tooltip("How quickly procedural music enters when the director opens a phrase.")]
        [SerializeField, Min(0.01f)] private float _proceduralActivityAttackSpeed = 0.85f;

        [Tooltip("How quickly procedural music leaves room for world sound after a phrase.")]
        [SerializeField, Min(0.01f)] private float _proceduralActivityReleaseSpeed = 0.38f;

        [Header("Fallbacks")]
        [Tooltip("Fallback pause duration when no profile-specific pause exists.")]
        [SerializeField, Min(0f)] private float _fallbackPauseSeconds = 45f;

        [Tooltip("Default fade-in for forced override clips.")]
        [SerializeField, Min(0.01f)] private float _defaultOverrideFadeInSeconds = 0.75f;

        [Tooltip("Default fade-out when clearing a forced override.")]
        [SerializeField, Min(0.01f)] private float _defaultOverrideFadeOutSeconds = 0.75f;

        [Header("Telemetry")]
        [Tooltip("Development-only event telemetry for cue selection and state transitions.")]
        [SerializeField] private bool _enableTelemetry;

        [Header("Diagnostics")]
        [SerializeField] private string _debugResolvedProfile = "None";
        [SerializeField] private string _debugActiveCueId = "None";
        [SerializeField] private float _debugTension01;
        [SerializeField] private bool _debugCombatLatched;
        [SerializeField] private bool _debugTenseExplorationLatched;
        [SerializeField] private float _debugWaitTimer;
        [SerializeField] private string _debugLastSelectionReason = "None";
        [SerializeField] private float _debugAiTension01;
        [SerializeField] private float _debugBiomePressure01;
        [SerializeField] private float _debugBiomeGradientBlend01;
        [SerializeField] private float _debugZonePressure01;
        [SerializeField] private float _debugDepthZonePressure01;
        [SerializeField] private float _debugRewardUnease01;
        [SerializeField] private float _debugSafePocketSuppression01;
        [SerializeField] private float _debugFirstHourPressureBoost01;
        [SerializeField] private float _debugLayerRhythm01;
        [SerializeField] private float _debugLayerBass01;
        [SerializeField] private float _debugLayerAtmosphere01;
        [SerializeField] private float _debugLayerDanger01;
        [SerializeField] private float _debugMusicActivity01;
        [SerializeField] private bool _debugLayerMixerRouteAvailable;
        [SerializeField] private float _debugPredatorProximity01;
        [SerializeField] private float _debugStormPressure01;
        [SerializeField] private float _debugOxygenDanger01;
        [SerializeField] private float _debugPlayerCriticalStress01;
        [SerializeField] private float _debugEmergencyAudioDominance01;
        [SerializeField] private float _debugVocalWarningMusicDuck01;
        [SerializeField] private byte _debugVocalWarningId;
        [SerializeField] private float _debugNarrativeAudioLogMusicDuck01;
        [SerializeField] private float _debugSoundscapePressure01;

        private AudioSource[] _musicSources;
        private HectonMusicBiomeProfile[] _voiceProfiles;
        private HectonMusicClip[] _voiceClips;
        private bool[] _voiceActive;
        private bool[] _voiceFading;
        private bool[] _voiceEndingFadeTriggered;
        private bool[] _voiceIsOverride;
        private float[] _voiceBaseVolumes;
        private float[] _voiceFadeStartVolumes;
        private float[] _voiceFadeTargetVolumes;
        private float[] _voiceFadeDurations;
        private float[] _voiceFadeElapsedTimes;
        private float[] _stingerCooldownRemainingByKind;
        private AudioClip[] _recentLongClips;
        private AudioClip[] _recentShortClips;

        private AudioSource _stingerSource;
        private bool _registeredTick;
        private bool _registeredSlowTick;
        private bool _registeredLateFrameTick;
        private bool _pendingMusicTickDirty;
        private bool _pendingMusicSlowTickDirty;
        private float _pendingMusicTickDeltaTime;
        private bool _serviceRegistered;
        private bool _runtimeOwnerAborted;
        private bool _hotSwapRegistered;
        private PlaybackState _playbackState = PlaybackState.Silent;
        private HectonMusicBiomeProfile _resolvedProfile;
        private HectonMusicBiomeProfile _manualProfile;
        private HectonMusicBiomeProfile _matrixBiomeProfile;
        private HectonMusicBiomeProfile _waitProfile;
        private int _activeVoiceIndex = InvalidVoiceIndex;
        private float _waitTimerSeconds;
        private float _shortTrackCooldownRemaining;
        private float _resolvedTension01;
        private float _manualTension01;
        private bool _manualTensionOverride;
        private bool _combatLatched;
        private bool _menuSceneActive;
        private bool _prologueSceneActive;
        private bool _overrideActive;
        private bool _overrideLoop;
        private AudioClip _overrideClip;
        private float _overrideVolume = 1f;
        private float _overrideFadeOutSeconds;
        private bool _scheduleWaitWhenSilent;
        private bool _pendingImmediateSelection;
        private AudioClip _lastLongClip;
        private AudioClip _lastShortClip;
        private AudioClip _lastStingerClip;
        private float _duckCurrent = 1f;
        private float _duckStart = 1f;
        private float _duckTarget = 1f;
        private float _duckDuration = 0.01f;
        private float _duckElapsed;
        private bool _duckFading;
        private bool _stingerDuckActive;
        private int _recentLongWriteIndex;
        private int _recentLongCount;
        private int _recentShortWriteIndex;
        private int _recentShortCount;
        private bool _currentBaseContext;
        private bool _pendingDiscoveryStinger;
        private bool _pendingDangerStinger;
        private bool _pendingRecoveryStinger;
        private int _forceCalmSelectionsRemaining;
        private bool _selectionUsedCrossTension;
        private bool _selectionUsedDepthBlend;
        private bool _tenseExplorationLatched;
        private bool _lastAcousticInteriorState;
        private bool _hasLastAcousticInteriorState;
        private int _lastAcousticZoneSignalFrame = -1;
        private HectonBiomeMatrixProfile _observedMatrixProfile;
        private int _observedMatrixDepthTier = int.MinValue;
        private float _observedMatrixDepthMeters;
        private SoundscapeTier _currentSoundscapeTier = SoundscapeTier.Shallow;
        private float _soundscapeDepthHintMeters;
        private DepthZoneProfile _observedDepthZone;
        private bool _hasObservedMatrixState;
        private bool _hasObservedDepthZone;
        private bool _lastDirectorPredatorPressure;
        private bool _hasLastDirectorPredatorPressure;
        private int _lastDirectorAISignalFrame = -1;
        private Transform _playerTransform;
        private Transform _dependencyPlayerTransform;
        private HectonPlayerMovement _playerMovement;
        private HectonSurvivalSystem _survivalSystem;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private IAudioService _cachedAudioService;
        private IAcousticZoneReadModel _cachedAcousticZone;
        private IDepthZoneReadModel _cachedDepthZoneReadModel;
        private IEncounterDirectorService _cachedEncounterDirector;
        private ISurfaceWeatherReadModel _cachedSurfaceWeatherDirector;
        private IFirstHourReadModel _cachedFirstHourDirector;
        private IVocalWarningSystem _cachedVocalWarningSystem;
        private IAudioLogRuntime _cachedAudioLogRuntime;
        private bool _depthZoneDirectorRuntimeCached;
        private int _nextPlayerContextResolveFrame;
        private int _nextAudioServiceResolveFrame;
        private int _nextAcousticZoneResolveFrame;
        private int _nextDepthZoneResolveFrame;
        private int _nextSurfaceWeatherResolveFrame;
        private int _nextFirstHourResolveFrame;
        private int _nextVocalWarningResolveFrame;
        private int _nextAudioLogResolveFrame;
        private AudioMixer _layerMixer;
        private float _layerRhythm01;
        private float _layerBass01;
        private float _layerAtmosphere01;
        private float _layerDanger01;
        private float _predatorProximity01;
        private float _stormPressure01;
        private float _oxygenDanger01;
        private float _playerCriticalStress01;
        private float _vocalWarningMusicDuck01;
        private float _narrativeAudioLogMusicDuck01;
        private byte _vocalWarningId;
        private int _lastForegroundSpeechDuckingRefreshFrame = -1;
        private int _lastPlayerStressSignalSequence;
        private int _lastPlayerStressSignalSeenFrame = int.MinValue;
        private float _biomeGradientBlend01;
        private byte _biomeGradientA;
        private byte _biomeGradientB;
        private float _lastRhythmDb = float.MinValue;
        private float _lastBassDb = float.MinValue;
        private float _lastAtmosphereDb = float.MinValue;
        private float _lastDangerDb = float.MinValue;
        private bool _rhythmLayerParameterUnavailable;
        private bool _bassLayerParameterUnavailable;
        private bool _atmosphereLayerParameterUnavailable;
        private bool _dangerLayerParameterUnavailable;
        private float _nextEditorDebugStateTime;
        private uint _musicRandomState;
        private bool _biomeMatrixDirectorRuntimeOwned;
        private bool _encounterDirectorRuntimeOwned;
        private bool _worldZoneDirectorRuntimeOwned;
        private bool _worldZoneDirectorListenerRegistered;
        private float _proceduralMusicActivity01;
        private float _proceduralPhraseTimerSeconds;
        private MusicActivityReason _musicActivityReason = MusicActivityReason.Silent;

        /// <summary>
        /// Currently resolved runtime profile.
        /// </summary>
        public HectonMusicBiomeProfile ActiveResolvedProfile => _runtimeOwnerAborted ? null : _resolvedProfile;
        public HectonMusicBiomeProfile ActiveMatrixBiomeMusicProfile => _runtimeOwnerAborted ? null : _matrixBiomeProfile;

        /// <summary>
        /// True while a forced override cue is active.
        /// </summary>
        public bool IsOverrideActive => !_runtimeOwnerAborted && _overrideActive;

        /// <summary>
        /// Current normalized tension value used by the director.
        /// </summary>
        public float CurrentTension01 => _runtimeOwnerAborted ? 0f : _resolvedTension01;

        /// <summary>
        /// Current normalized permission for procedural music to be audible.
        /// </summary>
        public float CurrentMusicActivity01 => _runtimeOwnerAborted ? 0f : math.saturate(_proceduralMusicActivity01);

        /// <summary>
        /// Current high-level reason behind procedural music activity.
        /// </summary>
        public MusicActivityReason CurrentMusicActivityReason => _runtimeOwnerAborted ? MusicActivityReason.Silent : _musicActivityReason;

        /// <summary>
        /// Current normalized rhythm-layer intensity.
        /// </summary>
        public float CurrentRhythmLayer01 => _runtimeOwnerAborted ? 0f : math.saturate(_layerRhythm01);

        /// <summary>
        /// Current normalized bass-layer intensity.
        /// </summary>
        public float CurrentBassLayer01 => _runtimeOwnerAborted ? 0f : math.saturate(_layerBass01);

        /// <summary>
        /// Current normalized atmosphere-layer intensity.
        /// </summary>
        public float CurrentAtmosphereLayer01 => _runtimeOwnerAborted ? 0f : math.saturate(_layerAtmosphere01);

        /// <summary>
        /// Current normalized danger-layer intensity.
        /// </summary>
        public float CurrentDangerLayer01 => _runtimeOwnerAborted ? 0f : math.saturate(_layerDanger01);

        /// <summary>
        /// True when at least one optional exposed music-layer mixer parameter is bound.
        /// </summary>
        public bool CurrentLayerMixerRouteAvailable => !_runtimeOwnerAborted && _debugLayerMixerRouteAvailable;

        /// <summary>
        /// Current soundscape depth tier mirrored from the world soundscape runtime.
        /// </summary>
        public SoundscapeTier CurrentSoundscapeTier => _runtimeOwnerAborted ? SoundscapeTier.Surface : _currentSoundscapeTier;

        /// <summary>
        /// Normalized musical pressure derived from the current soundscape tier.
        /// </summary>
        public float CurrentSoundscapePressure01 => _runtimeOwnerAborted ? 0f : ResolveSoundscapePressure01(_currentSoundscapeTier);

        /// <summary>
        /// Mixer route currently used by authored music voices.
        /// </summary>
        public AudioMixerGroup CurrentMusicMixerGroup => _runtimeOwnerAborted ? null : ResolveMusicMixerGroup();

        /// <summary>
        /// Authored dedicated music mixer route, if one is assigned.
        /// </summary>
        public AudioMixerGroup DedicatedMusicMixerGroup => _musicMixerGroup;

        private void Awake()
        {
            if (Application.isPlaying && !TryRegisterToGlobalRegistry())
                return;

            // COLD ALLOC: AudioSource[2] — persistent dual music voices — owner: HectonMusicDirector
            _musicSources = new AudioSource[MusicVoiceCount];
            // COLD ALLOC: HectonMusicBiomeProfile[2] — active voice profile ownership — owner: HectonMusicDirector
            _voiceProfiles = new HectonMusicBiomeProfile[MusicVoiceCount];
            // COLD ALLOC: HectonMusicClip[2] — active cue cache — owner: HectonMusicDirector
            _voiceClips = new HectonMusicClip[MusicVoiceCount];
            // COLD ALLOC: bool[2] — voice activity flags — owner: HectonMusicDirector
            _voiceActive = new bool[MusicVoiceCount];
            // COLD ALLOC: bool[2] — voice fade flags — owner: HectonMusicDirector
            _voiceFading = new bool[MusicVoiceCount];
            // COLD ALLOC: bool[2] — voice end-fade guards — owner: HectonMusicDirector
            _voiceEndingFadeTriggered = new bool[MusicVoiceCount];
            // COLD ALLOC: bool[2] — override ownership flags — owner: HectonMusicDirector
            _voiceIsOverride = new bool[MusicVoiceCount];
            // COLD ALLOC: float[2] — voice base volumes — owner: HectonMusicDirector
            _voiceBaseVolumes = new float[MusicVoiceCount];
            // COLD ALLOC: float[2] — voice fade start volumes — owner: HectonMusicDirector
            _voiceFadeStartVolumes = new float[MusicVoiceCount];
            // COLD ALLOC: float[2] — voice fade target volumes — owner: HectonMusicDirector
            _voiceFadeTargetVolumes = new float[MusicVoiceCount];
            // COLD ALLOC: float[2] — voice fade durations — owner: HectonMusicDirector
            _voiceFadeDurations = new float[MusicVoiceCount];
            // COLD ALLOC: float[2] — voice fade elapsed values — owner: HectonMusicDirector
            _voiceFadeElapsedTimes = new float[MusicVoiceCount];

            // COLD ALLOC: float[3] â€” stinger cooldown timers by kind â€” owner: HectonMusicDirector
            _stingerCooldownRemainingByKind = new float[3];
            // COLD ALLOC: AudioClip[4] â€” recent long-form clip history â€” owner: HectonMusicDirector
            _recentLongClips = new AudioClip[4];
            // COLD ALLOC: AudioClip[3] â€” recent short-form clip history â€” owner: HectonMusicDirector
            _recentShortClips = new AudioClip[3];
            _musicRandomState = unchecked(((uint)EntityId.ToULong(GetEntityId()) * 747796405u) ^ 0xD1B54A32u);
            if (_musicRandomState == 0u)
                _musicRandomState = 0xA341316Cu;

            EnsureProceduralSynthRuntime();
            BindAuthoredVoicePool();
            ResolveDependenciesCold();
        }

        private void OnEnable()
        {
            if (_runtimeOwnerAborted || !TryRegisterToGlobalRegistry())
                return;

            TryRegisterHotSwapListener();
            TryRegisterTickHandlers();
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            _pendingImmediateSelection = true;
        }

        private void Start()
        {
            if (_runtimeOwnerAborted || !TryRegisterToGlobalRegistry())
                return;

            TryRegisterTickHandlers();
            ResolveDependenciesCold();
            ReevaluateContext(true);
        }

        private void OnDisable()
        {
            if (_runtimeOwnerAborted)
                return;

            StopMusicInternal(0f);
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            TryUnregisterHotSwapListener();
            TryUnregisterWorldZoneDirectorListener();
            TryUnregisterTickHandlers();
            TryUnregisterFromGlobalRegistry();
            ClearCachedRuntimeServices();
        }

        private void OnDestroy()
        {
            if (_runtimeOwnerAborted)
                return;

            StopMusicInternal(0f);
            TryUnregisterHotSwapListener();
            TryUnregisterWorldZoneDirectorListener();
            TryUnregisterTickHandlers();
            TryUnregisterFromGlobalRegistry();
            ClearCachedRuntimeServices();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (_runtimeOwnerAborted)
                return;

            CacheReboundRuntimeService(serviceSlot, previousService, currentService);
        }

        /// <summary>
        /// Handles fades, wait timers, and ducking.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_runtimeOwnerAborted)
                return;

            _pendingMusicTickDeltaTime += math.max(0f, deltaTime);
            _pendingMusicTickDirty = true;
        }

        public void LateFrameTick()
        {
            if (_runtimeOwnerAborted)
                return;

            if (_pendingMusicSlowTickDirty)
            {
                _pendingMusicSlowTickDirty = false;
                RunMusicSlowTick();
            }

            if (!_pendingMusicTickDirty)
            {
                if (HasPendingStingers())
                    FlushPendingStingers();
                return;
            }

            float deltaTime = _pendingMusicTickDeltaTime;
            _pendingMusicTickDeltaTime = 0f;
            _pendingMusicTickDirty = false;
            RunMusicTick(deltaTime);
        }

        private void RunMusicTick(float deltaTime)
        {
            if (_runtimeOwnerAborted)
                return;

            DrainAcousticZoneSignal();
            DrainDirectorAISignals();
            RefreshPlayerCriticalStressSignal();
            RefreshForegroundSpeechMusicDucking();
            if (ProceduralSynthOwnsMusicPlayback)
            {
                UpdateStingerCooldowns(deltaTime);
                UpdateLayerRouting(deltaTime);
                FlushPendingStingers();
                if (_pendingImmediateSelection)
                {
                    _pendingImmediateSelection = false;
                    StartProceduralPhrase(true);
                }

                UpdateProceduralMusicActivity(deltaTime);
                PublishDynamicMusicScalars(deltaTime);
                WriteDebugState();
                return;
            }

            if (!AreRuntimeVoicesReady())
            {
                WriteDebugState();
                return;
            }

            UpdateDuck(deltaTime);
            UpdateVoices(deltaTime);
            UpdateStingerState();
            UpdateStingerCooldowns(deltaTime);
            UpdateLayerRouting(deltaTime);

            if (_shortTrackCooldownRemaining > 0f)
            {
                _shortTrackCooldownRemaining -= deltaTime;
                if (_shortTrackCooldownRemaining < 0f)
                    _shortTrackCooldownRemaining = 0f;
            }

            if (_overrideActive)
            {
                UpdateOverrideState();
                WriteDebugState();
                return;
            }

            if (_pendingImmediateSelection)
            {
                FlushPendingStingers();
                _pendingImmediateSelection = false;
                TryStartNextResolvedTrack(true);
                WriteDebugState();
                return;
            }

            if (_playbackState == PlaybackState.Waiting)
            {
                _waitTimerSeconds -= deltaTime;
                if (_waitTimerSeconds <= 0f)
                {
                    _waitTimerSeconds = 0f;
                    FlushPendingStingers();
                    TryStartNextResolvedTrack(false);
                }

                WriteDebugState();
                return;
            }

            if (!HasAnyActiveVoice())
            {
                if (_resolvedProfile != null || _fallbackProfile != null)
                    BeginWait(_resolvedProfile != null ? _resolvedProfile : _fallbackProfile);
                else
                    _playbackState = PlaybackState.Silent;

                WriteDebugState();
                return;
            }

            if (_activeVoiceIndex >= 0 && _activeVoiceIndex < MusicVoiceCount && _voiceActive[_activeVoiceIndex])
            {
                HectonMusicBiomeProfile activeProfile = _voiceProfiles[_activeVoiceIndex];
                float fadeOutSeconds = activeProfile != null ? activeProfile.FadeOutSeconds : _defaultOverrideFadeOutSeconds;

                if (!_voiceIsOverride[_activeVoiceIndex] &&
                    !_voiceEndingFadeTriggered[_activeVoiceIndex] &&
                    ShouldTriggerEndFade(_activeVoiceIndex, fadeOutSeconds))
                {
                    _voiceEndingFadeTriggered[_activeVoiceIndex] = true;
                    _scheduleWaitWhenSilent = true;
                    _waitProfile = _resolvedProfile != null ? _resolvedProfile : activeProfile;
                    StartFade(_activeVoiceIndex, 0f, fadeOutSeconds);
                }
            }

            WriteDebugState();
        }

        /// <summary>
        /// Synchronizes music routing with zone, biome, scene, and tension context.
        /// </summary>
        public void SlowTick()
        {
            if (_runtimeOwnerAborted)
                return;

            _pendingMusicSlowTickDirty = true;
        }

        private void RunMusicSlowTick()
        {
            if (_runtimeOwnerAborted)
                return;

            DrainAcousticZoneSignal();
            DrainDirectorAISignals();
            DrainBiomeGradientSignal();
            RefreshLayerThreatSnapshot();
            RefreshPolledMusicContext();
            ReevaluateContext(false);
        }

        /// <summary>
        /// Forces a manual biome-profile override.
        /// </summary>
        public void SetManualBiomeProfile(HectonMusicBiomeProfile profile)
        {
            if (_runtimeOwnerAborted)
                return;

            _manualProfile = profile;
            ReevaluateContext(true);
        }

        /// <summary>
        /// Sets the biome-matrix driven exploration profile. Combat, menu, base, cave, and thermal contexts keep priority.
        /// </summary>
        public void SetMatrixBiomeProfile(HectonBiomeMatrixProfile matrixProfile)
        {
            if (_runtimeOwnerAborted)
                return;

            HectonMusicBiomeProfile resolvedProfile = ResolveMatrixBiomeMusicProfile(matrixProfile);
            if (_matrixBiomeProfile == resolvedProfile)
                return;

            _matrixBiomeProfile = resolvedProfile;
            ReevaluateContext(true);
        }

        /// <summary>
        /// Mirrors the depth-tier soundscape context so music phrasing can yield to the authored world bed.
        /// </summary>
        public void SetSoundscapeTierContext(SoundscapeTier tier, float depthMeters)
        {
            if (_runtimeOwnerAborted)
                return;

            SoundscapeTier safeTier = SanitizeSoundscapeTier(tier);
            float finiteDepth = math.max(0f, math.select(0f, depthMeters, math.isfinite(depthMeters)));
            float depthHint = math.max(finiteDepth, ResolveSoundscapeDepthHintMeters(safeTier));
            float pressure01 = ResolveSoundscapePressure01(safeTier);

            if (_currentSoundscapeTier == safeTier && math.abs(_soundscapeDepthHintMeters - depthHint) < 0.5f)
            {
                _debugSoundscapePressure01 = pressure01;
                return;
            }

            bool tierChanged = _currentSoundscapeTier != safeTier;
            _currentSoundscapeTier = safeTier;
            _soundscapeDepthHintMeters = depthHint;
            _debugSoundscapePressure01 = pressure01;
            if (tierChanged)
                ReevaluateContext(true);
        }

        /// <summary>
        /// Clears the manual biome-profile override.
        /// </summary>
        public void ClearManualBiomeProfile()
        {
            if (_runtimeOwnerAborted)
                return;

            if (_manualProfile == null)
                return;

            _manualProfile = null;
            ReevaluateContext(true);
        }

        /// <summary>
        /// Forces a manual normalized tension value.
        /// </summary>
        public void SetManualTension01(float tension01)
        {
            if (_runtimeOwnerAborted)
                return;

            _manualTensionOverride = true;
            _manualTension01 = math.saturate(tension01);
            ReevaluateContext(true);
        }

        /// <summary>
        /// Clears the manual tension override.
        /// </summary>
        public void ClearManualTensionOverride()
        {
            if (_runtimeOwnerAborted)
                return;

            if (!_manualTensionOverride)
                return;

            _manualTensionOverride = false;
            _manualTension01 = 0f;
            ReevaluateContext(true);
        }

        /// <summary>
        /// Forces a full-priority override clip until cleared or finished.
        /// </summary>
        public void ForceOverrideTrack(AudioClip clip, float volume = 1f, bool loop = false, float fadeInSeconds = -1f, float fadeOutSeconds = -1f)
        {
            if (_runtimeOwnerAborted)
                return;

            if (clip == null)
                return;

            ForceOverrideTrackInternal(clip, volume, loop, fadeInSeconds, fadeOutSeconds);
        }

        /// <summary>
        /// Clears the forced override and returns control to automatic routing.
        /// </summary>
        public void ClearForcedOverride(bool immediate = false)
        {
            if (_runtimeOwnerAborted)
                return;

            ClearForcedOverrideInternal(immediate);
        }

        /// <summary>
        /// Plays a discovery stinger over the current bed.
        /// </summary>
        public void PlayDiscoveryStinger()
        {
            if (_runtimeOwnerAborted)
                return;

            RefreshForegroundSpeechMusicDucking();
            if (_overrideActive || _combatLatched || _currentBaseContext || IsEmergencyBreathDominant() || IsForegroundSpeechActive())
                return;

            _pendingDiscoveryStinger = true;
            TryRegisterLateFrameTick();
        }

        /// <summary>
        /// Plays a danger stinger over the current bed.
        /// </summary>
        public void PlayDangerStinger()
        {
            if (_runtimeOwnerAborted)
                return;

            RefreshForegroundSpeechMusicDucking();
            if (_overrideActive || _currentBaseContext || IsEmergencyBreathDominant() || IsForegroundSpeechActive())
                return;

            _pendingDangerStinger = true;
            TryRegisterLateFrameTick();
        }

        /// <summary>
        /// Plays a recovery stinger over the current bed.
        /// </summary>
        public void PlayRecoveryStinger()
        {
            if (_runtimeOwnerAborted)
                return;

            RefreshForegroundSpeechMusicDucking();
            if (_overrideActive || _currentBaseContext || IsEmergencyBreathDominant() || IsForegroundSpeechActive())
                return;

            _pendingRecoveryStinger = true;
            TryRegisterLateFrameTick();
        }

        private void UpdateStingerCooldowns(float deltaTime)
        {
            if (_stingerCooldownRemainingByKind == null)
                return;

            for (int i = 0; i < _stingerCooldownRemainingByKind.Length; i++)
            {
                float remaining = _stingerCooldownRemainingByKind[i];
                if (remaining <= 0f)
                    continue;

                remaining -= deltaTime;
                _stingerCooldownRemainingByKind[i] = remaining > 0f ? remaining : 0f;
            }
        }

        private void FlushPendingStingers()
        {
            RefreshForegroundSpeechMusicDucking();
            if (IsEmergencyBreathDominant() || IsForegroundSpeechActive())
            {
                _pendingDiscoveryStinger = false;
                _pendingDangerStinger = false;
                _pendingRecoveryStinger = false;
                return;
            }

            if (_pendingDiscoveryStinger)
            {
                if (TryPlayPendingStinger(StingerKind.Discovery) || _overrideActive || _combatLatched || _currentBaseContext)
                    _pendingDiscoveryStinger = false;
            }

            if (_pendingDangerStinger)
            {
                if (TryPlayPendingStinger(StingerKind.Danger) || _overrideActive || _currentBaseContext)
                    _pendingDangerStinger = false;
            }

            if (_pendingRecoveryStinger)
            {
                if (TryPlayPendingStinger(StingerKind.Recovery) || _overrideActive || _currentBaseContext)
                    _pendingRecoveryStinger = false;
            }
        }

        private bool HasPendingStingers()
        {
            return _pendingDiscoveryStinger || _pendingDangerStinger || _pendingRecoveryStinger;
        }

        /// <summary>
        /// Stops all active bed playback with an optional fade-out.
        /// </summary>
        public void StopMusic(float fadeOutSeconds = 0.75f)
        {
            if (_runtimeOwnerAborted)
                return;

            StopMusicInternal(fadeOutSeconds);
        }

        private void TryRegisterTickHandlers()
        {
            if (_runtimeOwnerAborted || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredTick)
            {
                _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            }

            if (!_registeredSlowTick)
            {
                _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            }

            TryRegisterLateFrameTick();
        }

        private void TryUnregisterTickHandlers()
        {
            if (_registeredTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredTick = false;
            }

            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = false;
            }

            if (_registeredLateFrameTick)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrameTick = false;
            }

            _pendingDiscoveryStinger = false;
            _pendingDangerStinger = false;
            _pendingRecoveryStinger = false;
        }

        private void TryRegisterLateFrameTick()
        {
            if (_runtimeOwnerAborted || _registeredLateFrameTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryRegisterHotSwapListener()
        {
            if (_runtimeOwnerAborted || _hotSwapRegistered || !Application.isPlaying)
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

        private bool TryRegisterToGlobalRegistry()
        {
            if (_runtimeOwnerAborted)
                return false;

            if (_serviceRegistered)
            {
                s_activeRuntimeInstance = this;
                return true;
            }

            if (!Application.isPlaying)
                return true;

            if (TryAbortForUsableExistingRuntime())
                return false;

            HectonMusicDirector activeDirector = GlobalRegistry.MusicDirector;
            if (!ReferenceEquals(activeDirector, null) && !ReferenceEquals(activeDirector, this))
            {
                if (IsMusicDirectorRuntimeUsable(activeDirector))
                {
                    AbortDuplicateRuntimeOwner();
                    return false;
                }

                GlobalRegistry.UnregisterMusicDirectorRuntime(activeDirector);
            }

            GlobalRegistry.RegisterMusicDirectorRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.MusicDirector, this);
            if (_serviceRegistered)
            {
                s_activeRuntimeInstance = this;
                return true;
            }

            AbortDuplicateRuntimeOwner();
            return false;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            if (_runtimeOwnerAborted)
                return true;

            if (!Application.isPlaying)
                return false;

            HectonMusicDirector registered = GlobalRegistry.MusicDirector;
            if (!ReferenceEquals(registered, null) && !ReferenceEquals(registered, this))
            {
                if (IsMusicDirectorRuntimeUsable(registered))
                {
                    s_activeRuntimeInstance = registered;
                    AbortDuplicateRuntimeOwner();
                    return true;
                }

                GlobalRegistry.UnregisterMusicDirectorRuntime(registered);
                if (ReferenceEquals(s_activeRuntimeInstance, registered))
                    s_activeRuntimeInstance = null;
            }

            HectonMusicDirector active = s_activeRuntimeInstance;
            if (ReferenceEquals(active, null) || ReferenceEquals(active, this))
                return false;

            if (IsMusicDirectorRuntimeUsable(active))
            {
                GlobalRegistry.RegisterMusicDirectorRuntime(active);
                s_activeRuntimeInstance = active;
                AbortDuplicateRuntimeOwner();
                return true;
            }

            GlobalRegistry.UnregisterMusicDirectorRuntime(active);
            if (ReferenceEquals(s_activeRuntimeInstance, active))
                s_activeRuntimeInstance = null;

            return false;
        }

        private void AbortDuplicateRuntimeOwner()
        {
            if (_runtimeOwnerAborted)
                return;

            if (_musicSources != null)
                StopMusicInternal(0f);

            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            TryUnregisterHotSwapListener();
            TryUnregisterWorldZoneDirectorListener();
            TryUnregisterTickHandlers();

            if (_serviceRegistered)
            {
                GlobalRegistry.UnregisterMusicDirectorRuntime(this);
                _serviceRegistered = false;
            }

            if (ReferenceEquals(s_activeRuntimeInstance, this))
                s_activeRuntimeInstance = null;

            ClearCachedRuntimeServices();
            _runtimeOwnerAborted = true;
            _registeredTick = false;
            _registeredSlowTick = false;
            _registeredLateFrameTick = false;
            _serviceRegistered = false;
            _hotSwapRegistered = false;
            _pendingMusicTickDirty = false;
            _pendingMusicSlowTickDirty = false;
            _pendingMusicTickDeltaTime = 0f;
            _pendingDiscoveryStinger = false;
            _pendingDangerStinger = false;
            _pendingRecoveryStinger = false;
            enabled = false;
            Destroy(gameObject);
        }

        private static bool IsMusicDirectorRuntimeUsable(HectonMusicDirector director)
        {
            return director != null &&
                   director._serviceRegistered &&
                   director.isActiveAndEnabled &&
                   !director._runtimeOwnerAborted;
        }

        private void TryUnregisterFromGlobalRegistry()
        {
            if (_runtimeOwnerAborted || !_serviceRegistered)
                return;

            GlobalRegistry.UnregisterMusicDirectorRuntime(this);
            if (ReferenceEquals(s_activeRuntimeInstance, this))
                s_activeRuntimeInstance = null;
            _serviceRegistered = false;
        }

        private static bool TryInstantiateConfiguredRuntimeDirector(Scene activeScene, bool reportMissingConfig)
        {
            HectonMusicDirectorConfig sceneConfig;
            if (!HectonMusicDirectorAnchor.TryResolveConfigForScene(activeScene, out sceneConfig))
            {
                HectonMusicDirectorAnchor anchor = null;
                sceneConfig = HectonMusicDirectorAnchor.TryResolveActiveRuntime(ref anchor) ? anchor.Config : null;
            }

            if (sceneConfig == null)
            {
                if (reportMissingConfig)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Hecton8.Core.H8Debug.LogError("[HectonMusicDirector] Missing authored HectonMusicDirectorConfig for active scene.");
#endif
                }

                return false;
            }

            if (sceneConfig.RuntimeDirectorPrefab == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[HectonMusicDirector] Missing authored RuntimeDirectorPrefab on active HectonMusicDirectorConfig.");
#endif
                return false;
            }

            IObjectPoolService pool = ResolveRuntimeObjectPool();
            if (pool == null)
                return false;

            GameObject runtimeDirectorPrefab = sceneConfig.RuntimeDirectorPrefab.gameObject;
            EnsureRuntimeDirectorPoolReserve(pool, runtimeDirectorPrefab);
            if (pool.GetAvailableCount(runtimeDirectorPrefab) <= 0)
                return false;

            GameObject instance = pool.Spawn(runtimeDirectorPrefab, Vector3.zero, Quaternion.identity, false);
            if (instance == null)
                return false;

            return s_activeRuntimeInstance != null;
        }

        private static void EnsureRuntimeDirectorPoolReserve(IObjectPoolService pool, GameObject runtimeDirectorPrefab)
        {
            if (pool == null || runtimeDirectorPrefab == null)
                return;

            if (pool.GetAvailableCount(runtimeDirectorPrefab) >= RuntimeDirectorPoolReserveCount)
                return;

            pool.Warmup(runtimeDirectorPrefab, RuntimeDirectorPoolReserveCount);
        }

        private static IObjectPoolService ResolveRuntimeObjectPool()
        {
            ObjectPoolManager pool = null;
            return ObjectPoolManager.TryResolveActiveRuntime(ref pool)
                ? pool
                : null;
        }

        private void BindAuthoredVoicePool()
        {
            if (_runtimeOwnerAborted)
                return;

            if (_musicSources == null)
                return;

            for (int i = 0; i < _musicSources.Length; i++)
                _musicSources[i] = null;

            _stingerSource = null;
            if (ProceduralSynthOwnsMusicPlayback)
            {
                EnsureProceduralSynthRuntime();
                return;
            }

            ResolveVoicePool();
            if (_voicePool == null)
                return;

            _voicePool.ResetRuntimeAvailability();

            for (int i = 0; i < MusicVoiceCount; i++)
            {
                if (_voicePool.TryGetMusicVoice(i, out AudioSource source))
                    _musicSources[i] = source;
            }

            _stingerSource = _voicePool.StingerSource;
            _voicePool.ApplyRuntimeRouting(ResolveMusicMixerGroup(), ResolveStingerMixerGroup());
        }

        private void ResolveVoicePool()
        {
            if (_runtimeOwnerAborted)
                return;

            if (_voicePool != null)
                return;

            if (TryGetComponent(out _voicePool))
                return;

            _voicePool = ComponentReferenceUtility.ResolveOwnedComponent<MusicVoicePool>(transform);
        }

        private void ClearCachedRuntimeServices()
        {
            _playerRuntimeContext = null;
            _cachedAudioService = null;
            _cachedAcousticZone = null;
            if (_depthZoneDirectorRuntimeCached)
            {
                _cachedDepthZoneReadModel = null;
                _depthZoneDirectorRuntimeCached = false;
            }

            if (_biomeMatrixDirectorRuntimeOwned)
            {
                _biomeMatrixDirector = null;
                _biomeMatrixDirectorRuntimeOwned = false;
            }

            if (_encounterDirectorRuntimeOwned)
            {
                _cachedEncounterDirector = null;
                _encounterDirectorRuntimeOwned = false;
            }

            _cachedSurfaceWeatherDirector = null;
            _cachedFirstHourDirector = null;
            _cachedVocalWarningSystem = null;
            _cachedAudioLogRuntime = null;
            _nextPlayerContextResolveFrame = 0;
            _nextAudioServiceResolveFrame = 0;
            _nextAcousticZoneResolveFrame = 0;
            _nextDepthZoneResolveFrame = 0;
            _nextSurfaceWeatherResolveFrame = 0;
            _nextFirstHourResolveFrame = 0;
            _nextVocalWarningResolveFrame = 0;
            _nextAudioLogResolveFrame = 0;
            _playerCriticalStress01 = 0f;
            _vocalWarningMusicDuck01 = 0f;
            _narrativeAudioLogMusicDuck01 = 0f;
            _vocalWarningId = 0;
            _lastForegroundSpeechDuckingRefreshFrame = -1;
            _lastPlayerStressSignalSequence = 0;
            _lastPlayerStressSignalSeenFrame = int.MinValue;
            _debugPlayerCriticalStress01 = 0f;
            _debugEmergencyAudioDominance01 = ResolveEmergencyAudioDominance01();
            _debugVocalWarningMusicDuck01 = 0f;
            _debugVocalWarningId = 0;
            _debugNarrativeAudioLogMusicDuck01 = 0f;
        }

        private void CacheReboundRuntimeService(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (_runtimeOwnerAborted)
                return;

            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Player:
                    CachePlayerRuntimeContext(currentService as IPlayerRuntimeContext, frame);
                    ResetPlayerDependencyProbe();
                    break;
                case GlobalRegistryServiceSlot.Audio:
                    CacheAudioService(currentService as IAudioService, frame);
                    if (_voicePool != null)
                        _voicePool.ApplyRuntimeRouting(ResolveMusicMixerGroup(), ResolveStingerMixerGroup());
                    break;
                case GlobalRegistryServiceSlot.AcousticZoneRuntime:
                    CacheAcousticZone(currentService as IAcousticZoneReadModel, frame);
                    _hasLastAcousticInteriorState = false;
                    break;
                case GlobalRegistryServiceSlot.BiomeMatrixRuntime:
                    BiomeMatrixDirector currentBiomeMatrix = currentService as BiomeMatrixDirector;
                    WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref currentBiomeMatrix);
                    CacheBiomeMatrixDirector(currentBiomeMatrix);
                    _observedMatrixProfile = null;
                    _hasObservedMatrixState = false;
                    break;
                case GlobalRegistryServiceSlot.EncounterDirector:
                    CacheEncounterDirector(currentService as IEncounterDirectorService);
                    _hasLastDirectorPredatorPressure = false;
                    break;
                case GlobalRegistryServiceSlot.DepthZoneRuntime:
                    if (_depthZoneDirectorRuntimeCached ||
                        _cachedDepthZoneReadModel == null ||
                        ReferenceEquals(previousService, _cachedDepthZoneReadModel))
                    {
                        CacheDepthZoneReadModel(currentService as IDepthZoneReadModel, frame);
                    }
                    break;
                case GlobalRegistryServiceSlot.SurfaceWeatherRuntime:
                    CacheSurfaceWeatherDirector(currentService as ISurfaceWeatherReadModel, frame);
                    break;
                case GlobalRegistryServiceSlot.FirstHourRuntime:
                    CacheFirstHourDirector(currentService as IFirstHourReadModel, frame);
                    break;
                case GlobalRegistryServiceSlot.VocalWarningRuntime:
                    CacheVocalWarningSystem(currentService as IVocalWarningSystem, frame);
                    break;
                case GlobalRegistryServiceSlot.AudioLogRuntime:
                    CacheAudioLogRuntime(currentService as IAudioLogRuntime, frame);
                    break;
            }
        }

        private IPlayerRuntimeContext ResolvePlayerRuntimeContext()
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            return playerContext != null && playerContext.IsInitialized ? playerContext : null;
        }

        private IAudioService ResolveAudioService()
        {
            IAudioService audioService = _cachedAudioService;
            if (IsAudioServiceUsable(audioService))
                return audioService;

            _cachedAudioService = null;
            return null;
        }

        private IAcousticZoneReadModel ResolveAcousticZone()
        {
            return _cachedAcousticZone;
        }

        private IDepthZoneReadModel ResolveDepthZoneReadModel()
        {
            if (_depthZoneDirector is IDepthZoneReadModel explicitReadModel)
                return explicitReadModel;

            if (_cachedDepthZoneReadModel != null)
                return _cachedDepthZoneReadModel;

            return null;
        }

        private IEncounterDirectorService ResolveEncounterDirector()
        {
            if (_directorAI != null)
                return _directorAI;

            IEncounterDirectorService encounterDirector = _cachedEncounterDirector;
            return encounterDirector != null && encounterDirector.IsInitialized ? encounterDirector : null;
        }

        private ISurfaceWeatherReadModel ResolveSurfaceWeatherDirector()
        {
            return _cachedSurfaceWeatherDirector;
        }

        private IFirstHourReadModel ResolveFirstHourDirector()
        {
            return _cachedFirstHourDirector;
        }

        private IVocalWarningSystem ResolveVocalWarningSystem()
        {
            IVocalWarningSystem vocalWarningSystem = _cachedVocalWarningSystem;
            if (IsVocalWarningRuntimeUsable(vocalWarningSystem))
                return vocalWarningSystem;

            _cachedVocalWarningSystem = null;
            return null;
        }

        private IAudioLogRuntime ResolveAudioLogRuntime()
        {
            IAudioLogRuntime audioLogRuntime = _cachedAudioLogRuntime;
            if (IsAudioLogRuntimeUsable(audioLogRuntime))
                return audioLogRuntime;

            _cachedAudioLogRuntime = null;
            return null;
        }

        private void RefreshCachedRuntimeServicesCold()
        {
            if (_runtimeOwnerAborted)
                return;

            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            CachePlayerRuntimeContext(GlobalRegistry.Player, frame);
            CacheAudioService(GlobalRegistry.Audio, frame);
            CacheAcousticZone(GlobalRegistry.AcousticZoneReadModel, frame);
            CacheBiomeMatrixDirector(GlobalRegistry.BiomeMatrix);
            CacheEncounterDirector(GlobalRegistry.EncounterDirector);
            CacheDepthZoneReadModel(GlobalRegistry.DepthZoneReadModel, frame);
            CacheSurfaceWeatherDirector(GlobalRegistry.SurfaceWeatherReadModel, frame);
            CacheFirstHourDirector(GlobalRegistry.FirstHourReadModel, frame);
            CacheVocalWarningSystem(GlobalRegistry.VocalWarnings, frame);
            CacheAudioLogRuntime(GlobalRegistry.AudioLogRuntime, frame);
        }

        private void CachePlayerRuntimeContext(IPlayerRuntimeContext playerContext, int frame)
        {
            _playerRuntimeContext = playerContext != null && playerContext.IsInitialized ? playerContext : null;
            _nextPlayerContextResolveFrame = frame + DependencyRetryFrameInterval;
        }

        private void ResetPlayerDependencyProbe()
        {
            _dependencyPlayerTransform = null;
            _playerMovement = null;
            _survivalSystem = null;
        }

        private void CacheAudioService(IAudioService audioService, int frame)
        {
            _cachedAudioService = IsAudioServiceUsable(audioService) ? audioService : null;
            _nextAudioServiceResolveFrame = frame + DependencyRetryFrameInterval;
        }

        private void CacheVocalWarningSystem(IVocalWarningSystem vocalWarningSystem, int frame)
        {
            _cachedVocalWarningSystem = IsVocalWarningRuntimeUsable(vocalWarningSystem) ? vocalWarningSystem : null;
            _nextVocalWarningResolveFrame = frame + DependencyRetryFrameInterval;
            _lastForegroundSpeechDuckingRefreshFrame = -1;
        }

        private void CacheAudioLogRuntime(IAudioLogRuntime audioLogRuntime, int frame)
        {
            _cachedAudioLogRuntime = IsAudioLogRuntimeUsable(audioLogRuntime) ? audioLogRuntime : null;
            _nextAudioLogResolveFrame = frame + DependencyRetryFrameInterval;
            _lastForegroundSpeechDuckingRefreshFrame = -1;
        }

        private void CacheAcousticZone(IAcousticZoneReadModel acousticZone, int frame)
        {
            _cachedAcousticZone = acousticZone;
            _nextAcousticZoneResolveFrame = frame + DependencyRetryFrameInterval;
        }

        private void CacheDepthZoneReadModel(IDepthZoneReadModel depthZoneReadModel, int frame)
        {
            _cachedDepthZoneReadModel = depthZoneReadModel;
            _depthZoneDirectorRuntimeCached = depthZoneReadModel != null;
            _nextDepthZoneResolveFrame = frame + DependencyRetryFrameInterval;
        }

        private void CacheBiomeMatrixDirector(BiomeMatrixDirector director)
        {
            if (_biomeMatrixDirector != null && !_biomeMatrixDirectorRuntimeOwned && _biomeMatrixDirector.isActiveAndEnabled)
                return;

            if (director != null && !director.isActiveAndEnabled)
                director = null;
            WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref director);
            _biomeMatrixDirector = director;
            _biomeMatrixDirectorRuntimeOwned = director != null;
        }

        private void CacheEncounterDirector(IEncounterDirectorService encounterDirector)
        {
            if (_directorAI != null)
            {
                _cachedEncounterDirector = _directorAI;
                _encounterDirectorRuntimeOwned = false;
                return;
            }

            _cachedEncounterDirector = encounterDirector != null && encounterDirector.IsInitialized
                ? encounterDirector
                : null;
            _encounterDirectorRuntimeOwned = _cachedEncounterDirector != null;
        }

        private void CacheSurfaceWeatherDirector(ISurfaceWeatherReadModel surfaceWeather, int frame)
        {
            _cachedSurfaceWeatherDirector = surfaceWeather;
            _nextSurfaceWeatherResolveFrame = frame + DependencyRetryFrameInterval;
        }

        private void CacheFirstHourDirector(IFirstHourReadModel firstHourDirector, int frame)
        {
            _cachedFirstHourDirector = firstHourDirector;
            _nextFirstHourResolveFrame = frame + DependencyRetryFrameInterval;
        }

        private void ResolveDependenciesCold()
        {
            if (_runtimeOwnerAborted)
                return;

            ResolveDependenciesForSceneCold(SceneManager.GetActiveScene());
        }

        private void TryRegisterWorldZoneDirectorListenerCold()
        {
            if (_runtimeOwnerAborted)
                return;

            if (_worldZoneDirector == null || _worldZoneDirectorRuntimeOwned || !_worldZoneDirector.isActiveAndEnabled)
            {
                WorldZoneDirector runtimeWorldZoneDirector = null;
                WorldRuntimeReferenceUtility.TryResolveWorldZoneDirector(ref runtimeWorldZoneDirector);
                CacheRuntimeWorldZoneDirectorCold(runtimeWorldZoneDirector);
            }

            if (_worldZoneDirectorListenerRegistered || !Application.isPlaying)
                return;

            WorldZoneDirector.ActiveRuntimeInstanceChanged += HandleWorldZoneDirectorChanged;
            _worldZoneDirectorListenerRegistered = true;
        }

        private void TryUnregisterWorldZoneDirectorListener()
        {
            if (!_worldZoneDirectorListenerRegistered)
                return;

            WorldZoneDirector.ActiveRuntimeInstanceChanged -= HandleWorldZoneDirectorChanged;
            _worldZoneDirectorListenerRegistered = false;
        }

        private void HandleWorldZoneDirectorChanged(WorldZoneDirector director)
        {
            if (_runtimeOwnerAborted)
                return;

            CacheRuntimeWorldZoneDirectorCold(director);
        }

        private void CacheRuntimeWorldZoneDirectorCold(WorldZoneDirector director)
        {
            if (_runtimeOwnerAborted)
                return;

            if (_worldZoneDirector != null && !_worldZoneDirectorRuntimeOwned && _worldZoneDirector.isActiveAndEnabled)
                return;

            if (director != null && !director.isActiveAndEnabled)
                director = null;
            WorldRuntimeReferenceUtility.TryResolveWorldZoneDirector(ref director);
            _worldZoneDirector = director;
            _worldZoneDirectorRuntimeOwned = director != null;
        }

        private void ResolveDependenciesForSceneCold(Scene activeScene)
        {
            if (_runtimeOwnerAborted)
                return;

            RefreshCachedRuntimeServicesCold();
            ApplySceneConfigCold(activeScene);
            TryRegisterWorldZoneDirectorListenerCold();
            ResolveDependencies();
            BindRuntimeVoiceRoutingCold();
        }

        private void ApplySceneConfigCold(Scene activeScene)
        {
            HectonMusicDirectorConfig sceneConfig;
            if (HectonMusicDirectorAnchor.TryResolveConfigForScene(activeScene, out sceneConfig))
            {
                ApplyConfig(sceneConfig);
            }
            else
            {
                HectonMusicDirectorAnchor anchor = null;
                if (HectonMusicDirectorAnchor.TryResolveActiveRuntime(ref anchor))
                    ApplyConfig(anchor.Config);
            }

            RefreshSceneFlags(activeScene);
        }

        private void ResolveDependencies()
        {
            if (_runtimeOwnerAborted)
                return;

            RefreshVocalWarningRuntimeIfStale();
            RefreshAudioLogRuntimeIfStale();
            ResolveDepthZoneReadModel();

            IPlayerRuntimeContext playerContext = ResolvePlayerRuntimeContext();
            Transform resolvedPlayerTransform = playerContext != null && playerContext.PlayerTransform != null
                ? playerContext.PlayerTransform
                : _playerTransform;

            if (!ReferenceEquals(_dependencyPlayerTransform, resolvedPlayerTransform))
            {
                _dependencyPlayerTransform = resolvedPlayerTransform;
                _playerTransform = resolvedPlayerTransform;
                _playerMovement = playerContext != null &&
                                  ReferenceEquals(playerContext.PlayerTransform, resolvedPlayerTransform)
                    ? playerContext.PlayerMovement
                    : null;
                _survivalSystem = playerContext != null &&
                                  ReferenceEquals(playerContext.PlayerTransform, resolvedPlayerTransform)
                    ? playerContext.SurvivalSystem
                    : null;
            }

            if (playerContext != null && ReferenceEquals(playerContext.PlayerTransform, resolvedPlayerTransform))
            {
                if (_playerMovement == null)
                    _playerMovement = playerContext.PlayerMovement;

                if (_survivalSystem == null)
                    _survivalSystem = playerContext.SurvivalSystem;
            }
        }

        private void RefreshVocalWarningRuntimeIfStale()
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            IVocalWarningSystem vocalWarningSystem = _cachedVocalWarningSystem;
            if (IsVocalWarningRuntimeUsable(vocalWarningSystem))
                return;

            if (vocalWarningSystem != null)
                _cachedVocalWarningSystem = null;

            if (frame < _nextVocalWarningResolveFrame)
                return;

            CacheVocalWarningSystem(GlobalRegistry.VocalWarnings, frame);
        }

        private void RefreshAudioLogRuntimeIfStale()
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            IAudioLogRuntime audioLogRuntime = _cachedAudioLogRuntime;
            if (IsAudioLogRuntimeUsable(audioLogRuntime))
                return;

            if (audioLogRuntime != null)
                _cachedAudioLogRuntime = null;

            if (frame < _nextAudioLogResolveFrame)
                return;

            CacheAudioLogRuntime(GlobalRegistry.AudioLogRuntime, frame);
        }

        private static bool IsAudioServiceUsable(IAudioService audioService)
        {
            if (audioService == null || !audioService.IsAudioRuntimeReady)
                return false;

            if (audioService is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private static bool IsVocalWarningRuntimeUsable(IVocalWarningSystem vocalWarningSystem)
        {
            if (vocalWarningSystem == null || !vocalWarningSystem.IsVocalWarningRuntimeReady)
                return false;

            if (vocalWarningSystem is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private static bool IsAudioLogRuntimeUsable(IAudioLogRuntime audioLogRuntime)
        {
            if (audioLogRuntime == null || !audioLogRuntime.IsAudioLogRuntimeReady)
                return false;

            if (audioLogRuntime is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private void BindRuntimeVoiceRoutingCold()
        {
            AudioMixerGroup musicGroup = ResolveMusicMixerGroup();
            AudioMixerGroup stingerGroup = ResolveStingerMixerGroup();
            _layerMixer = musicGroup != null ? musicGroup.audioMixer : null;
            ResetLayerMixerStateCache();
            BindAuthoredVoicePool();

            if (_musicSources != null)
            {
                for (int i = 0; i < MusicVoiceCount; i++)
                {
                    if (_musicSources[i] != null)
                        _musicSources[i].outputAudioMixerGroup = musicGroup;
                }
            }

            if (_stingerSource != null)
                _stingerSource.outputAudioMixerGroup = stingerGroup;

            ApplyLayerMixerState(true);
        }

        private bool AreRuntimeVoicesReady()
        {
            if (ProceduralSynthOwnsMusicPlayback)
                return true;

            if (_musicSources == null || _musicSources.Length < MusicVoiceCount || _stingerSource == null)
                return false;

            for (int i = 0; i < MusicVoiceCount; i++)
            {
                if (_musicSources[i] == null)
                    return false;
            }

            return true;
        }

        private void EnsureProceduralSynthRuntime()
        {
            if (!ProceduralSynthOwnsMusicPlayback || !Application.isPlaying)
                return;

            SignalBus<DynamicMusicScalarSignal>.Configure(
                expectedCapacity: DynamicMusicScalarSignal.ExpectedCapacity,
                maxFrameSignals: DynamicMusicScalarSignal.MaxFrameSignals,
                lowTierFrameSignals: DynamicMusicScalarSignal.LowTierFrameSignals,
                laneHash: DynamicMusicScalarSignal.LaneHash);
            SignalBus<DynamicMusicScalarSignal>.EnsureInitialized();
        }

        private void StartProceduralPhrase(bool force)
        {
            HectonMusicBiomeProfile profile = _resolvedProfile != null ? _resolvedProfile : _fallbackProfile;
            if (profile == null && !_overrideActive)
            {
                _playbackState = PlaybackState.Silent;
                _proceduralPhraseTimerSeconds = 0f;
                return;
            }

            if (!force &&
                _playbackState == PlaybackState.Playing &&
                (_combatLatched || _tenseExplorationLatched || _menuSceneActive || _prologueSceneActive || _currentBaseContext))
            {
                return;
            }

            _waitTimerSeconds = 0f;
            _waitProfile = null;
            _playbackState = _overrideActive ? PlaybackState.Override : PlaybackState.Playing;
            _proceduralPhraseTimerSeconds = ResolveProceduralPhraseSeconds(profile);
            TraceEvent("Procedural:Phrase", profile, null);
        }

        private void UpdateProceduralMusicActivity(float deltaTime)
        {
            if (!ProceduralSynthOwnsMusicPlayback)
                return;

            if (ShouldForceProceduralMusicOpen() &&
                _playbackState != PlaybackState.Playing &&
                _playbackState != PlaybackState.Override)
            {
                StartProceduralPhrase(true);
            }

            if (_playbackState == PlaybackState.Silent)
            {
                if (_resolvedProfile != null || _fallbackProfile != null)
                    BeginProceduralWait(_resolvedProfile != null ? _resolvedProfile : _fallbackProfile);
            }
            else if (_playbackState == PlaybackState.Waiting)
            {
                _waitTimerSeconds -= deltaTime;
                if (_waitTimerSeconds <= 0f)
                    StartProceduralPhrase(false);
            }
            else if (_playbackState == PlaybackState.Playing)
            {
                if (_proceduralPhraseTimerSeconds > 0f)
                    _proceduralPhraseTimerSeconds -= deltaTime;

                if (_proceduralPhraseTimerSeconds <= 0f &&
                    !_combatLatched &&
                    !_tenseExplorationLatched &&
                    !_menuSceneActive &&
                    !_prologueSceneActive &&
                    !_currentBaseContext)
                {
                    BeginProceduralWait(_resolvedProfile != null ? _resolvedProfile : _fallbackProfile);
                }
            }

            float targetActivity01 = ResolveProceduralMusicActivityTarget01();
            float speed = targetActivity01 > _proceduralMusicActivity01
                ? _proceduralActivityAttackSpeed
                : _proceduralActivityReleaseSpeed;
            _proceduralMusicActivity01 = MoveTowards(_proceduralMusicActivity01, targetActivity01, deltaTime * math.max(0.01f, speed));
            _debugMusicActivity01 = _proceduralMusicActivity01;
        }

        private bool ShouldForceProceduralMusicOpen()
        {
            return !IsEmergencyBreathDominant() &&
                   (_menuSceneActive ||
                    _prologueSceneActive ||
                    _combatLatched ||
                    _tenseExplorationLatched ||
                    _currentBaseContext);
        }

        private void BeginProceduralWait(HectonMusicBiomeProfile profile)
        {
            HectonMusicBiomeProfile waitProfile = profile != null ? profile : _fallbackProfile;
            _waitProfile = waitProfile;

            float minPause = waitProfile != null ? waitProfile.MinPauseSeconds : _proceduralFallbackRestMinSeconds;
            float maxPause = waitProfile != null ? waitProfile.MaxPauseSeconds : _proceduralFallbackRestMaxSeconds;
            if (minPause <= 0f && maxPause <= 0f)
            {
                minPause = _proceduralFallbackRestMinSeconds;
                maxPause = _proceduralFallbackRestMaxSeconds;
            }

            float restScale = ResolveSoundscapeRestScale(_currentSoundscapeTier);
            minPause = math.max(0f, minPause * restScale);
            maxPause = math.max(minPause, maxPause * restScale);

            if (maxPause <= minPause)
                _waitTimerSeconds = math.max(0f, minPause);
            else
                _waitTimerSeconds = NextRandomRange(math.max(0f, minPause), math.max(0f, maxPause));

            _proceduralPhraseTimerSeconds = 0f;
            _playbackState = PlaybackState.Waiting;
            TraceEvent("Procedural:Rest", waitProfile, null);
        }

        private float ResolveProceduralPhraseSeconds(HectonMusicBiomeProfile profile)
        {
            if (_overrideActive || _menuSceneActive || _prologueSceneActive || _currentBaseContext || _combatLatched || _tenseExplorationLatched)
                return 0f;

            float minSeconds = math.max(1f, _proceduralExplorationPhraseMinSeconds);
            float maxSeconds = math.max(minSeconds, _proceduralExplorationPhraseMaxSeconds);
            if (profile != null)
            {
                float profileWindow = math.max(0f, profile.MinPauseSeconds);
                if (profileWindow > 0f)
                    maxSeconds = math.min(maxSeconds, math.max(minSeconds, profileWindow));
            }

            float phraseScale = ResolveSoundscapePhraseScale(_currentSoundscapeTier);
            minSeconds = math.max(1f, minSeconds * phraseScale);
            maxSeconds = math.max(minSeconds, maxSeconds * phraseScale);

            return maxSeconds <= minSeconds ? minSeconds : NextRandomRange(minSeconds, maxSeconds);
        }

        private float ResolveProceduralMusicActivityTarget01()
        {
            if (IsEmergencyBreathDominant())
            {
                _musicActivityReason = MusicActivityReason.Emergency;
                return 0f;
            }

            if (_overrideActive)
            {
                _musicActivityReason = MusicActivityReason.Override;
                return ApplyForegroundSpeechMusicDuck01(math.saturate(_overrideVolume));
            }

            if (_menuSceneActive)
            {
                _musicActivityReason = MusicActivityReason.Menu;
                return ApplyForegroundSpeechMusicDuck01(1f);
            }

            if (_prologueSceneActive)
            {
                _musicActivityReason = MusicActivityReason.Prologue;
                return ApplyForegroundSpeechMusicDuck01(1f);
            }

            if (_playbackState != PlaybackState.Playing)
            {
                _musicActivityReason = _playbackState == PlaybackState.Silent
                    ? MusicActivityReason.Silent
                    : MusicActivityReason.Rest;
                return 0f;
            }

            float soundscapePressure01 = ResolveSoundscapePressure01(_currentSoundscapeTier);
            float depth01 = math.saturate(math.max(ResolveLayerDepthMeters() * 0.00035f, soundscapePressure01));
            float pressure01 = math.saturate(math.max(math.max(_predatorProximity01, ResolveEmergencyAudioDominance01()), _stormPressure01));
            float tension01 = math.saturate(_resolvedTension01);
            if (_combatLatched)
            {
                _musicActivityReason = MusicActivityReason.Combat;
                return ApplyForegroundSpeechMusicDuck01(math.saturate(0.72f + tension01 * 0.28f + pressure01 * 0.18f + soundscapePressure01 * 0.08f));
            }

            if (_tenseExplorationLatched)
            {
                _musicActivityReason = MusicActivityReason.Tense;
                return ApplyForegroundSpeechMusicDuck01(math.saturate(0.48f + tension01 * 0.34f + pressure01 * 0.24f + depth01 * 0.10f + soundscapePressure01 * 0.08f));
            }

            if (_currentBaseContext)
            {
                _musicActivityReason = MusicActivityReason.Base;
                return ApplyForegroundSpeechMusicDuck01(math.saturate(0.16f + tension01 * 0.16f + soundscapePressure01 * 0.06f));
            }

            _musicActivityReason = MusicActivityReason.Exploration;
            return ApplyForegroundSpeechMusicDuck01(math.saturate(0.12f + depth01 * 0.18f + soundscapePressure01 * 0.12f + tension01 * 0.32f + pressure01 * 0.18f));
        }

        private float ApplyForegroundSpeechMusicDuck01(float activity01)
        {
            float safeActivity01 = math.saturate(math.isfinite(activity01) ? activity01 : 0f);
            float duck01 = ResolveForegroundSpeechMusicDuck01();
            return math.saturate(safeActivity01 * (1f - duck01));
        }

        private void PublishDynamicMusicScalars(float deltaTime)
        {
            _ = deltaTime;
            if (!ProceduralSynthOwnsMusicPlayback || !Application.isPlaying)
                return;

            EnsureProceduralSynthRuntime();

            float rawDepthMeters = ResolveLayerDepthMeters();
            float tension01 = math.saturate(math.isfinite(_resolvedTension01) ? _resolvedTension01 : 0f);
            float depthMeters = math.max(0f, math.isfinite(rawDepthMeters) ? rawDepthMeters : 0f);
            float quality01 = math.saturate(math.isfinite(HomeostasisBrain.GlobalQualityWeight) ? HomeostasisBrain.GlobalQualityWeight : 1f);
            bool emergencyBreathDominates = IsEmergencyBreathDominant();
            bool foregroundSpeechActive = IsForegroundSpeechActive();
            float damageImpulse01 = emergencyBreathDominates || foregroundSpeechActive
                ? 0f
                : math.saturate((_pendingDangerStinger ? 0.35f : 0f) + (_combatLatched ? 0.12f : 0f));
            float activity01 = emergencyBreathDominates ? 0f : math.saturate(_proceduralMusicActivity01);
            uint flags = DynamicMusicScalarSignal.FlagExternalScalars;
            if (emergencyBreathDominates || foregroundSpeechActive)
                flags |= DynamicMusicScalarSignal.FlagSuppressReactiveImpulses;
            PushDynamicMusicSignal(
                tension01,
                depthMeters,
                quality01,
                damageImpulse01,
                0f,
                0f,
                activity01,
                flags);
        }

        private void InjectProceduralStinger(StingerKind kind)
        {
            if (IsEmergencyBreathDominant() || IsForegroundSpeechActive())
                return;

            EnsureProceduralSynthRuntime();

            float kind01 = (float)kind * 0.5f;
            float impulse = math.lerp(0.55f, 1f, math.saturate(kind01));
            float tension01 = math.saturate(math.isfinite(_resolvedTension01) ? _resolvedTension01 : 0f);
            float pitchKick = math.lerp(0.35f, 1f, math.saturate(math.max(kind01, tension01)));
            PushDynamicMusicSignal(
                tension01,
                ResolveLayerDepthMeters(),
                math.saturate(math.isfinite(HomeostasisBrain.GlobalQualityWeight) ? HomeostasisBrain.GlobalQualityWeight : 1f),
                pitchKick,
                impulse,
                pitchKick,
                1f,
                DynamicMusicScalarSignal.FlagExternalScalars | DynamicMusicScalarSignal.FlagStingerImpulse);
            _stingerDuckActive = true;
            StartDuck(_stingerDuckFactor, _stingerDuckAttackSeconds);
        }

        private void PushDynamicMusicSignal(
            float tension01,
            float depthMeters,
            float quality01,
            float damageImpulse01,
            float stingerImpulse01,
            float pitchKick01,
            float activity01,
            uint flags)
        {
            DynamicMusicScalarSignal signal = default;
            signal.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            signal.Flags = flags;
            signal.Tension01 = math.saturate(math.isfinite(tension01) ? tension01 : 0f);
            signal.DepthMeters = math.max(0f, math.isfinite(depthMeters) ? depthMeters : 0f);
            signal.GlobalQualityWeight = math.saturate(math.isfinite(quality01) ? quality01 : 1f);
            signal.DamageImpulse01 = math.saturate(math.isfinite(damageImpulse01) ? damageImpulse01 : 0f);
            signal.StingerImpulse01 = math.saturate(math.isfinite(stingerImpulse01) ? stingerImpulse01 : 0f);
            signal.PitchKick01 = math.saturate(math.isfinite(pitchKick01) ? pitchKick01 : 0f);
            signal.MusicActivity01 = math.saturate(math.isfinite(activity01) ? activity01 : 0f);
            signal.SourceHash = DynamicMusicScalarSignal.SourceMusicDirectorHash;
            SignalBus<DynamicMusicScalarSignal>.TryPushTracked(in signal, ref s_x001HectonMusicDirectorSignalPushDropCount);
        }

        private void PublishProceduralMusicStopSignal()
        {
            if (!ProceduralSynthOwnsMusicPlayback || !Application.isPlaying)
                return;

            EnsureProceduralSynthRuntime();
            PushDynamicMusicSignal(
                math.saturate(math.isfinite(_resolvedTension01) ? _resolvedTension01 : 0f),
                ResolveLayerDepthMeters(),
                math.saturate(math.isfinite(HomeostasisBrain.GlobalQualityWeight) ? HomeostasisBrain.GlobalQualityWeight : 1f),
                0f,
                0f,
                0f,
                0f,
                DynamicMusicScalarSignal.FlagExternalScalars | DynamicMusicScalarSignal.FlagSuppressReactiveImpulses);
        }

        private void RefreshLayerThreatSnapshot()
        {
            ResolveDependencies();

            float depthMeters = ResolveLayerDepthMeters();
            _oxygenDanger01 = ResolveLayerOxygenDanger01();
            RefreshPlayerCriticalStressSignal();
            _stormPressure01 = ResolveStormPressure01(depthMeters);

            if (_playerTransform == null)
            {
                _predatorProximity01 = 0f;
                _debugPredatorProximity01 = 0f;
                _debugStormPressure01 = _stormPressure01;
                _debugOxygenDanger01 = _oxygenDanger01;
                _debugEmergencyAudioDominance01 = ResolveEmergencyAudioDominance01();
                return;
            }

            if (!TryResolvePlayerThreatPose(out AbsoluteUniversePosition playerAup, out Vector3 playerRuntimePosition))
            {
                _predatorProximity01 = 0f;
                _debugPredatorProximity01 = 0f;
                _debugStormPressure01 = _stormPressure01;
                _debugOxygenDanger01 = _oxygenDanger01;
                _debugEmergencyAudioDominance01 = ResolveEmergencyAudioDominance01();
                return;
            }

            if (WorldSpatialHashGrid.TryGetNearestAggressiveBioform(
                playerRuntimePosition,
                in playerAup,
                math.max(1f, _predatorSenseRadius),
                _PredatorThreatLayerMask,
                _playerTransform,
                out SpatialQueryHit predatorHit))
            {
                float senseRadius = math.max(1f, _predatorSenseRadius);
                float senseRadiusSq = senseRadius * senseRadius;
                _predatorProximity01 = 1f - math.saturate(math.max(0f, predatorHit.DistanceSqr) * math.rcp(senseRadiusSq));
            }
            else
            {
                _predatorProximity01 = 0f;
            }

            _debugPredatorProximity01 = _predatorProximity01;
            _debugStormPressure01 = _stormPressure01;
            _debugOxygenDanger01 = _oxygenDanger01;
            _debugEmergencyAudioDominance01 = ResolveEmergencyAudioDominance01();
        }

        private bool TryResolvePlayerThreatPose(out AbsoluteUniversePosition playerAup, out Vector3 runtimePosition)
        {
            playerAup = default;
            runtimePosition = default;
            IPlayerRuntimeContext playerContext = ResolvePlayerRuntimeContext();
            if (playerContext != null)
            {
                if (playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot pose) &&
                    pose.Aup.IsFinite() &&
                    math.all(math.isfinite(pose.RuntimePosition)))
                {
                    playerAup = pose.Aup;
                    runtimePosition = new Vector3(
                        pose.RuntimePosition.x,
                        pose.RuntimePosition.y,
                        pose.RuntimePosition.z);
                    return true;
                }

                return false;
            }

            HectonPlayerMovement movement = _playerMovement;
            if (movement == null)
                return false;

            playerAup = movement.CurrentAup;
            if (!playerAup.IsFinite() ||
                !TryResolveRuntimeOriginRelativeFloat3(in playerAup, out float3 runtime3))
            {
                return false;
            }

            runtimePosition = new Vector3(runtime3.x, runtime3.y, runtime3.z);
            return true;
        }

        private static bool TryResolveRuntimeOriginRelativeFloat3(
            in AbsoluteUniversePosition positionAup,
            out float3 runtimePosition)
        {
            runtimePosition = default;
            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!positionAup.IsFinite() || !originAup.IsFinite())
                return false;

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

        private void UpdateLayerRouting(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            float depthMeters = ResolveLayerDepthMeters();
            float soundscapePressure01 = ResolveSoundscapePressure01(_currentSoundscapeTier);
            float depth01 = math.saturate(math.max(InverseLerp(20f, 900f, depthMeters), soundscapePressure01));
            float emergencyAudio01 = ResolveEmergencyAudioDominance01();
            float rhythmTarget = math.saturate(_resolvedTension01 * 0.65f + _predatorProximity01 * 0.55f + _stormPressure01 * 0.18f + _playerCriticalStress01 * 0.16f);
            float bassTarget = math.saturate(depth01 * 0.54f + soundscapePressure01 * 0.18f + _resolvedTension01 * 0.28f + emergencyAudio01 * 0.26f + _stormPressure01 * 0.12f);
            float atmosphereTarget = math.saturate(0.20f + depth01 * 0.48f + soundscapePressure01 * 0.18f + _stormPressure01 * 0.16f + _biomeGradientBlend01 * 0.22f - (_currentBaseContext ? 0.16f : 0f));
            float dangerTarget = math.saturate(math.max(math.max(_predatorProximity01, emergencyAudio01), _resolvedTension01 * 0.82f) + _stormPressure01 * 0.18f);

            if (_currentBaseContext)
            {
                rhythmTarget *= 0.38f;
                bassTarget *= 0.55f;
                atmosphereTarget *= 0.72f;
                dangerTarget *= 0.3f;
            }

            _layerRhythm01 = MoveLayerValue(_layerRhythm01, rhythmTarget, deltaTime);
            _layerBass01 = MoveLayerValue(_layerBass01, bassTarget, deltaTime);
            _layerAtmosphere01 = MoveLayerValue(_layerAtmosphere01, atmosphereTarget, deltaTime);
            _layerDanger01 = MoveLayerValue(_layerDanger01, dangerTarget, deltaTime);

            _debugLayerRhythm01 = _layerRhythm01;
            _debugLayerBass01 = _layerBass01;
            _debugLayerAtmosphere01 = _layerAtmosphere01;
            _debugLayerDanger01 = _layerDanger01;

            ApplyLayerMixerState(false);
        }

        private void DrainBiomeGradientSignal()
        {
            ReadOnlySpan<BiomeGradientSignal> signals = SignalBus<BiomeGradientSignal>.GetFrameSnapshot();
            if (signals.Length == 0)
                return;

            BiomeGradientSignal signal = signals[signals.Length - 1];
            _biomeGradientBlend01 = math.saturate(signal.BlendFactor01);
            _biomeGradientA = signal.BiomeA;
            _biomeGradientB = signal.BiomeB;
            _debugBiomeGradientBlend01 = _biomeGradientBlend01;
        }

        private void DrainAcousticZoneSignal()
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastAcousticZoneSignalFrame == frame)
                return;

            _lastAcousticZoneSignalFrame = frame;
            ReadOnlySpan<AcousticZoneChangedEvent> signals = SignalBus<AcousticZoneChangedEvent>.GetFrameSnapshot();
            if (signals.Length == 0)
                return;

            AcousticZoneChangedEvent signal = signals[signals.Length - 1];
            HandleAcousticZoneChanged(signal.IsInterior != 0);
        }

        private void DrainDirectorAISignals()
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastDirectorAISignalFrame == frame)
                return;

            _lastDirectorAISignalFrame = frame;
            ReadOnlySpan<DirectorAIMusicSignal> signals = SignalBus<DirectorAIMusicSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                DirectorAIMusicSignal signal = signals[i];
                switch (signal.EventType)
                {
                    case DirectorAIMusicSignal.SpawnHordeEventType:
                        HandleSpawnHordeRequested(signal.Position);
                        break;
                    case DirectorAIMusicSignal.RareDiscoveryEventType:
                        HandleRareDiscoveryRequested(signal.Position);
                        break;
                    case DirectorAIMusicSignal.PredatorPressureEventType:
                        HandlePredatorPressureChanged(signal.BoolValue != 0);
                        break;
                    case DirectorAIMusicSignal.ThreatSpikeEventType:
                        HandleThreatSpike(signal.Position, signal.Value);
                        break;
                }
            }
        }

        private void RefreshPolledMusicContext()
        {
            RefreshObservedBiomeMatrixState();
            RefreshObservedDepthZoneState();
            RefreshObservedDirectorPressureState();
        }

        private void RefreshObservedBiomeMatrixState()
        {
            if (!TryResolveBiomeMatrixContext(
                    out HectonBiomeMatrixProfile currentProfile,
                    out int currentDepthTier,
                    out float currentDepthMeters))
            {
                if (_hasObservedMatrixState)
                {
                    _hasObservedMatrixState = false;
                    _observedMatrixProfile = null;
                    _observedMatrixDepthTier = 0;
                    _observedMatrixDepthMeters = math.max(0f, _soundscapeDepthHintMeters);
                }

                return;
            }

            if (!_hasObservedMatrixState)
            {
                _hasObservedMatrixState = true;
                _observedMatrixProfile = currentProfile;
                _observedMatrixDepthTier = currentDepthTier;
                _observedMatrixDepthMeters = currentDepthMeters;
                HandleMatrixBiomeChanged(currentProfile);
                return;
            }

            bool profileChanged = !ReferenceEquals(_observedMatrixProfile, currentProfile);
            bool depthTierChanged = _observedMatrixDepthTier != currentDepthTier;
            _observedMatrixProfile = currentProfile;
            _observedMatrixDepthTier = currentDepthTier;
            _observedMatrixDepthMeters = currentDepthMeters;

            if (profileChanged)
                HandleMatrixBiomeChanged(currentProfile);

            if (depthTierChanged)
                HandleDepthTierChanged(currentDepthTier, currentDepthMeters);
        }

        private void RefreshObservedDepthZoneState()
        {
            IDepthZoneReadModel depthZoneReadModel = ResolveDepthZoneReadModel();
            if (depthZoneReadModel == null)
                return;

            DepthZoneProfile currentZone = depthZoneReadModel.CurrentZone;
            if (!_hasObservedDepthZone)
            {
                _hasObservedDepthZone = true;
                _observedDepthZone = currentZone;
                return;
            }

            if (ReferenceEquals(_observedDepthZone, currentZone))
                return;

            DepthZoneProfile previousZone = _observedDepthZone;
            _observedDepthZone = currentZone;
            if (previousZone != null)
                HandleDepthZoneExited(previousZone);

            if (currentZone != null)
                HandleDepthZoneEntered(currentZone);
        }

        private void RefreshObservedDirectorPressureState()
        {
            IEncounterDirectorService encounterDirector = ResolveEncounterDirector();
            if (encounterDirector == null)
                return;

            bool pressureEnabled = encounterDirector.IsPredatorPressureEnabled;
            if (!_hasLastDirectorPredatorPressure)
            {
                _hasLastDirectorPredatorPressure = true;
                _lastDirectorPredatorPressure = pressureEnabled;
                if (pressureEnabled)
                    HandlePredatorPressureChanged(true);
                return;
            }

            if (_lastDirectorPredatorPressure == pressureEnabled)
                return;

            _lastDirectorPredatorPressure = pressureEnabled;
            HandlePredatorPressureChanged(pressureEnabled);
        }

        private float ResolveLayerDepthMeters()
        {
            float soundscapeDepthMeters = math.max(0f, _soundscapeDepthHintMeters);
            IPlayerRuntimeContext playerContext = ResolvePlayerRuntimeContext();
            if (TryResolvePlayerMovementDepthMeters(out float playerDepthMeters))
                return math.max(playerDepthMeters, soundscapeDepthMeters);

            if (playerContext != null)
                return 0f;

            if (soundscapeDepthMeters > 0f)
                return soundscapeDepthMeters;

            if (TryResolveBiomeMatrixContext(out _, out _, out float biomeDepthMeters))
                return math.max(0f, math.max(biomeDepthMeters, soundscapeDepthMeters));

            if (_survivalSystem != null && math.isfinite(_survivalSystem.Depth))
                return math.max(0f, _survivalSystem.Depth);

            return soundscapeDepthMeters;
        }

        private bool TryResolvePlayerMovementDepthMeters(out float depthMeters)
        {
            depthMeters = 0f;
            IPlayerRuntimeContext playerContext = ResolvePlayerRuntimeContext();
            if (playerContext == null ||
                !playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) ||
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u ||
                !math.isfinite(movementState.DepthMeters))
            {
                return false;
            }

            depthMeters = math.max(0f, movementState.DepthMeters);
            return true;
        }

        private bool TryResolveBiomeMatrixContext(
            out HectonBiomeMatrixProfile currentProfile,
            out int currentDepthTier,
            out float currentDepthMeters)
        {
            currentProfile = null;
            currentDepthTier = 0;
            currentDepthMeters = math.max(0f, _soundscapeDepthHintMeters);

            BiomeMatrixDirector biomeMatrix = _biomeMatrixDirector;
            if (biomeMatrix == null ||
                !biomeMatrix.isActiveAndEnabled ||
                !math.isfinite(biomeMatrix.CurrentDepthMeters))
            {
                return false;
            }

            currentProfile = biomeMatrix.CurrentProfile;
            currentDepthTier = math.max(0, biomeMatrix.CurrentDepthTier);
            currentDepthMeters = math.max(0f, biomeMatrix.CurrentDepthMeters);
            return true;
        }

        private float ResolveLayerOxygenDanger01()
        {
            if (_survivalSystem == null)
                return 0f;

            return InverseLerp(0.35f, 0.05f, _survivalSystem.OxygenNormalized);
        }

        private void RefreshPlayerCriticalStressSignal()
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (SignalBus<PlayerStressSignal>.TryGetLatest(out PlayerStressSignal signal, out int sequence) &&
                math.isfinite(signal.Stress01))
            {
                if (sequence != _lastPlayerStressSignalSequence ||
                    _lastPlayerStressSignalSeenFrame == int.MinValue ||
                    frame < _lastPlayerStressSignalSeenFrame)
                {
                    _lastPlayerStressSignalSequence = sequence;
                    _lastPlayerStressSignalSeenFrame = frame;
                    _playerCriticalStress01 = math.saturate(signal.Stress01);
                }
                else if (frame - _lastPlayerStressSignalSeenFrame > PlayerStressSignalHoldFrames)
                {
                    _playerCriticalStress01 = 0f;
                }
            }
            else
            {
                _playerCriticalStress01 = 0f;
                _lastPlayerStressSignalSeenFrame = int.MinValue;
            }

            _debugPlayerCriticalStress01 = _playerCriticalStress01;
            _debugEmergencyAudioDominance01 = ResolveEmergencyAudioDominance01();
        }

        private float ResolveEmergencyAudioDominance01()
        {
            return math.saturate(math.max(_oxygenDanger01, _playerCriticalStress01));
        }

        private void RefreshVocalWarningMusicDucking()
        {
            IVocalWarningSystem vocalWarningSystem = ResolveVocalWarningSystem();
            if (vocalWarningSystem == null)
            {
                _vocalWarningMusicDuck01 = 0f;
                _vocalWarningId = 0;
                _debugVocalWarningMusicDuck01 = 0f;
                _debugVocalWarningId = 0;
                return;
            }

            byte warningId = vocalWarningSystem.CurrentWarningId;
            bool active = vocalWarningSystem.IsWarningActive && warningId != 0;
            _vocalWarningId = active ? warningId : (byte)0;
            _vocalWarningMusicDuck01 = active ? ResolveVocalWarningMusicDuck01(warningId) : 0f;
            _debugVocalWarningMusicDuck01 = _vocalWarningMusicDuck01;
            _debugVocalWarningId = _vocalWarningId;
        }

        private void RefreshNarrativeAudioLogMusicDucking()
        {
            IAudioLogRuntime audioLogRuntime = ResolveAudioLogRuntime();
            bool active = audioLogRuntime != null &&
                          (audioLogRuntime.IsPlaying || audioLogRuntime.IsNarrativeQueueBlocked);
            _narrativeAudioLogMusicDuck01 = active ? NarrativeAudioLogMusicDuck01 : 0f;
            _debugNarrativeAudioLogMusicDuck01 = _narrativeAudioLogMusicDuck01;
        }

        private void RefreshForegroundSpeechMusicDucking()
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastForegroundSpeechDuckingRefreshFrame == frame)
                return;

            _lastForegroundSpeechDuckingRefreshFrame = frame;
            RefreshVocalWarningMusicDucking();
            RefreshNarrativeAudioLogMusicDucking();
        }

        private bool IsForegroundSpeechActive()
        {
            return ResolveForegroundSpeechMusicDuck01() > 0.001f;
        }

        private float ResolveForegroundSpeechMusicDuck01()
        {
            return math.saturate(math.max(_vocalWarningMusicDuck01, _narrativeAudioLogMusicDuck01));
        }

        private static float ResolveVocalWarningMusicDuck01(byte warningId)
        {
            switch ((VocalWarningId)warningId)
            {
                case VocalWarningId.CrushDepth:
                case VocalWarningId.HullBreach:
                case VocalWarningId.OxygenLow:
                    return VocalWarningMusicDuckCritical01;
                case VocalWarningId.Radiation:
                case VocalWarningId.PowerLow:
                case VocalWarningId.Toxicity:
                    return VocalWarningMusicDuckDefault01;
                default:
                    return 0f;
            }
        }

        private bool IsEmergencyBreathDominant()
        {
            RefreshPlayerCriticalStressSignal();
            float liveOxygenDanger01 = math.saturate(ResolveLayerOxygenDanger01());
            if (_survivalSystem != null)
            {
                _oxygenDanger01 = liveOxygenDanger01;
                _debugOxygenDanger01 = liveOxygenDanger01;
            }

            float emergencyAudio01 = ResolveEmergencyAudioDominance01();
            _debugEmergencyAudioDominance01 = emergencyAudio01;
            return emergencyAudio01 >= EmergencyBreathDominatesThreshold ||
                   _playerCriticalStress01 >= CriticalPlayerStressDominatesThreshold;
        }

        private float ResolveStormPressure01(float depthMeters)
        {
            ISurfaceWeatherReadModel weatherDirector = ResolveSurfaceWeatherDirector();
            if (weatherDirector == null || depthMeters > 120f)
                return 0f;

            float depthAttenuation = 1f - math.saturate(depthMeters * StormDepthAttenuationInv);
            return math.saturate(weatherDirector.CurrentElectricalActivity * depthAttenuation);
        }

        private float MoveLayerValue(float current, float target, float deltaTime)
        {
            float speed = target > current ? _layerAttackSpeed : _layerReleaseSpeed;
            return MoveTowards(current, target, deltaTime * math.max(0.01f, speed));
        }

        private void ApplyLayerMixerState(bool force)
        {
            if (_layerMixer == null)
            {
                _debugLayerMixerRouteAvailable = false;
                ResetLayerMixerStateCache();
                return;
            }

            bool anyRouteAvailable = false;
            float rhythmDb = NormalizedLayerValueToDb(_layerRhythm01);
            float bassDb = NormalizedLayerValueToDb(_layerBass01);
            float atmosphereDb = NormalizedLayerValueToDb(_layerAtmosphere01);
            float dangerDb = NormalizedLayerValueToDb(_layerDanger01);

            anyRouteAvailable |= TryApplyLayerMixerParameter(
                _rhythmLayerParameter,
                rhythmDb,
                ref _lastRhythmDb,
                ref _rhythmLayerParameterUnavailable,
                force);
            anyRouteAvailable |= TryApplyLayerMixerParameter(
                _bassLayerParameter,
                bassDb,
                ref _lastBassDb,
                ref _bassLayerParameterUnavailable,
                force);
            anyRouteAvailable |= TryApplyLayerMixerParameter(
                _atmosphereLayerParameter,
                atmosphereDb,
                ref _lastAtmosphereDb,
                ref _atmosphereLayerParameterUnavailable,
                force);
            anyRouteAvailable |= TryApplyLayerMixerParameter(
                _dangerLayerParameter,
                dangerDb,
                ref _lastDangerDb,
                ref _dangerLayerParameterUnavailable,
                force);

            _debugLayerMixerRouteAvailable = anyRouteAvailable;
        }

        private static float NormalizedLayerValueToDb(float value01)
        {
            float clamped = Mathf.Clamp01(value01);
            if (clamped <= 0.0001f)
                return MixerFloorDb;

            return Mathf.Clamp(20f * Mathf.Log10(clamped), MixerFloorDb, MixerCeilingDb);
        }

        private bool TryApplyLayerMixerParameter(
            string parameterName,
            float valueDb,
            ref float lastValueDb,
            ref bool unavailable,
            bool force)
        {
            if (string.IsNullOrEmpty(parameterName))
            {
                unavailable = true;
                lastValueDb = float.MinValue;
                return false;
            }

            if (unavailable && !force)
                return false;

            if (!force && lastValueDb > float.MinValue && math.abs(lastValueDb - valueDb) < 0.05f)
                return true;

            if (!_layerMixer.SetFloat(parameterName, valueDb))
            {
                unavailable = true;
                lastValueDb = float.MinValue;
                return false;
            }

            unavailable = false;
            lastValueDb = valueDb;
            return true;
        }

        private void ResetLayerMixerStateCache()
        {
            _lastRhythmDb = float.MinValue;
            _lastBassDb = float.MinValue;
            _lastAtmosphereDb = float.MinValue;
            _lastDangerDb = float.MinValue;
            _rhythmLayerParameterUnavailable = false;
            _bassLayerParameterUnavailable = false;
            _atmosphereLayerParameterUnavailable = false;
            _dangerLayerParameterUnavailable = false;
        }

        private void ApplyConfig(HectonMusicDirectorConfig config)
        {
            if (config == null)
                return;

            _mainMenuProfile = config.MainMenuProfile;
            _prologueProfile = config.PrologueProfile;
            _shallowProfile = config.ShallowProfile;
            _shelfProfile = config.ShelfProfile;
            _abyssProfile = config.AbyssProfile;
            _caveProfile = config.CaveProfile;
            _thermalProfile = config.ThermalProfile;
            _baseProfile = config.BaseProfile;
            _combatProfile = config.CombatProfile;
            _fallbackProfile = config.FallbackProfile;

            if (config.MusicMixerGroup != null)
                _musicMixerGroup = config.MusicMixerGroup;

            if (config.StingerMixerGroup != null)
                _stingerMixerGroup = config.StingerMixerGroup;
        }

        private void ReevaluateContext(bool forceImmediateSelection)
        {
            ResolveDependencies();

            bool previousCombatLatched = _combatLatched;
            bool previousTenseExplorationLatched = _tenseExplorationLatched;
            float nextTension01 = ResolveTension01();
            bool baseContext = ResolveBaseContext();
            _currentBaseContext = baseContext;

            if (_combatLatched)
            {
                if (nextTension01 <= _combatExitThreshold || baseContext)
                    _combatLatched = false;
            }
            else if (nextTension01 >= _combatEnterThreshold && !baseContext)
            {
                _combatLatched = true;
            }

            if (_combatLatched || baseContext)
            {
                _tenseExplorationLatched = false;
            }
            else if (_tenseExplorationLatched)
            {
                if (nextTension01 <= _tenseExplorationReleaseThreshold)
                    _tenseExplorationLatched = false;
            }
            else if (nextTension01 >= _tenseExplorationThreshold)
            {
                _tenseExplorationLatched = true;
            }

            _resolvedTension01 = nextTension01;

            HectonMusicBiomeProfile nextProfile = ResolveProfile(baseContext);
            bool profileChanged = nextProfile != _resolvedProfile;
            bool combatChanged = previousCombatLatched != _combatLatched;
            bool tenseChanged = previousTenseExplorationLatched != _tenseExplorationLatched;
            _resolvedProfile = nextProfile;

            if (!previousCombatLatched && _combatLatched)
                _pendingDangerStinger = true;

            if (previousCombatLatched && !_combatLatched)
            {
                _pendingRecoveryStinger = true;
                _forceCalmSelectionsRemaining = 2;
            }

            if (profileChanged)
                TraceEvent("Context:ProfileChanged", nextProfile, null);

            if (combatChanged)
                TraceEvent(_combatLatched ? "Context:CombatEnter" : "Context:CombatExit", nextProfile, null);

            if (tenseChanged)
                TraceEvent(_tenseExplorationLatched ? "Context:TenseEnter" : "Context:TenseExit", nextProfile, null);

            if (forceImmediateSelection || profileChanged || combatChanged || tenseChanged)
                _pendingImmediateSelection = true;

            WriteDebugState();
        }

        private HectonMusicBiomeProfile ResolveProfile(bool baseContext)
        {
            if (_manualProfile != null)
                return _manualProfile;

            if (_menuSceneActive && _mainMenuProfile != null)
                return _mainMenuProfile;

            if (_prologueSceneActive && _prologueProfile != null)
                return _prologueProfile;

            if (baseContext)
                return _baseProfile != null ? _baseProfile : _fallbackProfile;

            if (_combatLatched && _combatProfile != null)
                return _combatProfile;

            if (ResolveThermalContext())
                return _thermalProfile != null ? _thermalProfile : (_abyssProfile != null ? _abyssProfile : _fallbackProfile);

            if (ResolveCaveContext())
                return _caveProfile != null ? _caveProfile : (_shelfProfile != null ? _shelfProfile : _fallbackProfile);

            if (_matrixBiomeProfile != null)
                return _matrixBiomeProfile;

            if (TryResolveBiomeMatrixContext(out _, out int depthTier, out _))
            {
                if (depthTier <= 3)
                    return _shallowProfile != null ? _shallowProfile : _fallbackProfile;

                if (depthTier <= 9)
                    return _shelfProfile != null ? _shelfProfile : _fallbackProfile;

                return _abyssProfile != null ? _abyssProfile : _fallbackProfile;
            }

            HectonMusicBiomeProfile soundscapeProfile = ResolveSoundscapeTierProfile();
            if (soundscapeProfile != null)
                return soundscapeProfile;

            return _fallbackProfile;
        }

        private HectonMusicBiomeProfile ResolveSoundscapeTierProfile()
        {
            switch (_currentSoundscapeTier)
            {
                case SoundscapeTier.Thermal:
                    return _thermalProfile != null ? _thermalProfile : (_abyssProfile != null ? _abyssProfile : _fallbackProfile);
                case SoundscapeTier.DeepAbyss:
                case SoundscapeTier.Abyss:
                    return _abyssProfile != null ? _abyssProfile : _fallbackProfile;
                case SoundscapeTier.Darkness:
                case SoundscapeTier.Twilight:
                    return _shelfProfile != null ? _shelfProfile : _fallbackProfile;
                case SoundscapeTier.Shallow:
                case SoundscapeTier.Surface:
                    return _shallowProfile != null ? _shallowProfile : _fallbackProfile;
                default:
                    return _fallbackProfile;
            }
        }

        private float ResolveTension01()
        {
            if (_manualTensionOverride)
                return _manualTension01;

            HectonBiomeMatrixProfile matrixProfile = _biomeMatrixDirector != null ? _biomeMatrixDirector.CurrentProfile : null;
            WorldZoneAnchor currentZone = _worldZoneDirector != null ? _worldZoneDirector.CurrentZone : null;
            IDepthZoneReadModel depthZoneReadModel = ResolveDepthZoneReadModel();
            DepthZoneProfile depthZone = depthZoneReadModel != null ? depthZoneReadModel.CurrentZone : null;

            IEncounterDirectorService encounterDirector = ResolveEncounterDirector();
            float aiTension01 = encounterDirector != null
                ? math.saturate(encounterDirector.TensionScore * 0.01f)
                : 0f;
            float biomePressure01 = ResolveBiomePressure01(matrixProfile);
            float zonePressure01 = ResolveZonePressure01(currentZone);
            float depthZonePressure01 = ResolveDepthZonePressure01(depthZone);
            float soundscapePressure01 = ResolveSoundscapePressure01(_currentSoundscapeTier);
            float rewardUnease01 = ResolveRewardUnease01(matrixProfile);
            float safePocketSuppression01 = ResolveSafePocketSuppression01(matrixProfile, currentZone);
            float firstHourPressureBoost01 = ResolveFirstHourPressureBoost01(matrixProfile, currentZone);

            float tension01 = Hecton8.PureLogic.Systems.GameStateTensionScorer.Calculate(
                _predatorProximity01,
                depthZonePressure01,
                1f - _oxygenDanger01,
                1f - _playerCriticalStress01
            );

            if (ResolveBaseContext())
                tension01 *= _baseContextTensionScale;

            _debugAiTension01 = aiTension01;
            _debugBiomePressure01 = biomePressure01;
            _debugZonePressure01 = zonePressure01;
            _debugDepthZonePressure01 = depthZonePressure01;
            _debugSoundscapePressure01 = soundscapePressure01;
            _debugRewardUnease01 = rewardUnease01;
            _debugSafePocketSuppression01 = safePocketSuppression01;
            _debugFirstHourPressureBoost01 = firstHourPressureBoost01;

            return math.saturate(tension01);
        }

        private bool ResolveBaseContext()
        {
            IAcousticZoneReadModel acoustic = ResolveAcousticZone();
            if (acoustic != null && acoustic.IsInterior)
                return true;

            WorldZoneAnchor currentZone = _worldZoneDirector != null ? _worldZoneDirector.CurrentZone : null;
            if (currentZone == null)
                return false;

            switch (currentZone.Kind)
            {
                case WorldZoneAnchor.ZoneKind.Service:
                case WorldZoneAnchor.ZoneKind.Power:
                case WorldZoneAnchor.ZoneKind.Fabrication:
                case WorldZoneAnchor.ZoneKind.Construction:
                    return true;
            }

            return ContainsAnyToken(currentZone.ZoneId, BaseTokens) || ContainsAnyToken(currentZone.ZoneLabel, BaseTokens);
        }

        private bool ResolveCaveContext()
        {
            WorldZoneAnchor currentZone = _worldZoneDirector != null ? _worldZoneDirector.CurrentZone : null;
            if (currentZone != null &&
                (ContainsAnyToken(currentZone.ZoneId, CaveTokens) || ContainsAnyToken(currentZone.ZoneLabel, CaveTokens)))
            {
                return true;
            }

            HectonBiomeMatrixProfile matrixProfile = _biomeMatrixDirector != null ? _biomeMatrixDirector.CurrentProfile : null;
            return matrixProfile != null &&
                   (ContainsAnyToken(matrixProfile.biomeName, CaveTokens) || ContainsAnyToken(matrixProfile.shortDescription, CaveTokens));
        }

        private bool ResolveThermalContext()
        {
            WorldZoneAnchor currentZone = _worldZoneDirector != null ? _worldZoneDirector.CurrentZone : null;
            if (currentZone != null &&
                (ContainsAnyToken(currentZone.ZoneId, ThermalTokens) || ContainsAnyToken(currentZone.ZoneLabel, ThermalTokens)))
            {
                return true;
            }

            HectonBiomeMatrixProfile matrixProfile = _biomeMatrixDirector != null ? _biomeMatrixDirector.CurrentProfile : null;
            return matrixProfile != null &&
                   (ContainsAnyToken(matrixProfile.biomeName, ThermalTokens) || ContainsAnyToken(matrixProfile.shortDescription, ThermalTokens));
        }

        private HectonMusicBiomeProfile ResolveMatrixBiomeMusicProfile(HectonBiomeMatrixProfile matrixProfile)
        {
            if (matrixProfile == null)
                return null;

            if (matrixProfile.musicBiomeProfile != null)
                return matrixProfile.musicBiomeProfile;

            if (ContainsAnyToken(matrixProfile.biomeName, ThermalTokens) ||
                ContainsAnyToken(matrixProfile.shortDescription, ThermalTokens) ||
                ContainsAnyToken(matrixProfile.familyId, ThermalTokens))
            {
                return _thermalProfile != null ? _thermalProfile : (_abyssProfile != null ? _abyssProfile : _fallbackProfile);
            }

            if (ContainsAnyToken(matrixProfile.biomeName, CaveTokens) ||
                ContainsAnyToken(matrixProfile.shortDescription, CaveTokens) ||
                ContainsAnyToken(matrixProfile.familyId, CaveTokens))
            {
                return _caveProfile != null ? _caveProfile : (_shelfProfile != null ? _shelfProfile : _fallbackProfile);
            }

            if (matrixProfile.depthTier <= 3)
                return _shallowProfile != null ? _shallowProfile : _fallbackProfile;

            if (matrixProfile.depthTier <= 9)
                return _shelfProfile != null ? _shelfProfile : _fallbackProfile;

            return _abyssProfile != null ? _abyssProfile : _fallbackProfile;
        }

        private bool TryStartNextResolvedTrack(bool forceCrossfade)
        {
            HectonMusicBiomeProfile rootProfile = _resolvedProfile != null ? _resolvedProfile : _fallbackProfile;
            if (rootProfile == null)
            {
                _playbackState = PlaybackState.Silent;
                return false;
            }

            bool highTension = _combatLatched || _tenseExplorationLatched;
            if (_forceCalmSelectionsRemaining > 0)
            {
                highTension = false;
                _forceCalmSelectionsRemaining--;
            }

            bool preferShort = ShouldSelectShortTrack(rootProfile);
            HectonMusicClip selectedCue;
            HectonMusicBiomeProfile selectedProfile;

            if (!TrySelectCue(rootProfile, highTension, preferShort, out selectedCue, out selectedProfile))
            {
                BeginWait(rootProfile);
                return false;
            }

            HectonMusicBiomeProfile playbackProfile = selectedProfile != null ? selectedProfile : rootProfile;
            if (selectedCue.Role == HectonMusicClipRole.ExplorationShort || selectedCue.Role == HectonMusicClipRole.CombatShort)
                _shortTrackCooldownRemaining = playbackProfile.ShortTrackCooldownSeconds;

            float targetVolume = math.saturate(selectedCue.Volume);
            float fadeSeconds = HasAnyActiveVoice()
                ? playbackProfile.CrossfadeSeconds
                : playbackProfile.FadeInSeconds;

            StartBedCue(playbackProfile, selectedCue, targetVolume, fadeSeconds, forceCrossfade);
            CacheLastBedClip(selectedCue);
            TraceSelection(rootProfile, playbackProfile, selectedCue, highTension, preferShort);
            return true;
        }

        private void StartBedCue(HectonMusicBiomeProfile profile, HectonMusicClip cue, float targetVolume, float fadeSeconds, bool forceCrossfade)
        {
            if (ProceduralSynthOwnsMusicPlayback)
            {
                PublishDynamicMusicScalars(0f);
                _activeVoiceIndex = InvalidVoiceIndex;
                _scheduleWaitWhenSilent = false;
                _waitTimerSeconds = 0f;
                _playbackState = PlaybackState.Playing;
                return;
            }

            int nextVoiceIndex = GetInactiveVoiceIndex();
            if (nextVoiceIndex < 0)
                nextVoiceIndex = _activeVoiceIndex == 0 ? 1 : 0;
            if (nextVoiceIndex < 0)
                nextVoiceIndex = 0;

            StopVoiceImmediate(nextVoiceIndex);
            ConfigureVoice(nextVoiceIndex, profile, cue, cue.Clip, 0f, false, false);
            StartFade(nextVoiceIndex, targetVolume, fadeSeconds);

            if (HasAnyActiveVoice())
            {
                for (int i = 0; i < MusicVoiceCount; i++)
                {
                    if (i == nextVoiceIndex || !_voiceActive[i])
                        continue;

                    StartFade(i, 0f, forceCrossfade ? fadeSeconds : fadeSeconds);
                }
            }

            _activeVoiceIndex = nextVoiceIndex;
            _scheduleWaitWhenSilent = false;
            _waitTimerSeconds = 0f;
            _playbackState = PlaybackState.Playing;
        }

        private void ConfigureVoice(int voiceIndex, HectonMusicBiomeProfile profile, HectonMusicClip cue, AudioClip clip, float initialVolume, bool loop, bool isOverride)
        {
            if (ProceduralSynthOwnsMusicPlayback)
                return;

            AudioSource source = _musicSources[voiceIndex];
            source.Stop();
            AudioResidencyCache.TouchClip(clip, AudioResidencyDomain.Music, false);
            source.clip = clip;
            source.loop = loop;
            source.volume = initialVolume * _duckCurrent;
            source.pitch = 1f;
            source.outputAudioMixerGroup = ResolveMusicMixerGroup();
            source.Play();

            _voiceProfiles[voiceIndex] = profile;
            _voiceClips[voiceIndex] = cue;
            _voiceBaseVolumes[voiceIndex] = initialVolume;
            _voiceFadeStartVolumes[voiceIndex] = initialVolume;
            _voiceFadeTargetVolumes[voiceIndex] = initialVolume;
            _voiceFadeDurations[voiceIndex] = 0f;
            _voiceFadeElapsedTimes[voiceIndex] = 0f;
            _voiceFading[voiceIndex] = false;
            _voiceEndingFadeTriggered[voiceIndex] = false;
            _voiceActive[voiceIndex] = true;
            _voiceIsOverride[voiceIndex] = isOverride;
            if (_voicePool != null)
                _voicePool.MarkVoiceInUse(voiceIndex);
        }

        private void UpdateVoices(float deltaTime)
        {
            for (int i = 0; i < MusicVoiceCount; i++)
            {
                if (!_voiceActive[i])
                    continue;

                AudioSource source = _musicSources[i];
                if (source == null)
                    continue;

                if (_voiceFading[i])
                {
                    _voiceFadeElapsedTimes[i] += deltaTime;
                    float duration = _voiceFadeDurations[i];
                    float elapsedTime = _voiceFadeElapsedTimes[i];
                    float startVolume = _voiceFadeStartVolumes[i];
                    float targetVolume = _voiceFadeTargetVolumes[i];

                    _voiceBaseVolumes[i] = Hecton8.PureLogic.Systems.MusicStemBlendCrossfader.Calculate(
                        startVolume, targetVolume, duration, elapsedTime, 0f);

                    float durationSafe = duration > 0f ? duration : 0.01f;
                    if (elapsedTime >= durationSafe)
                    {
                        _voiceFading[i] = false;
                        if (_voiceFadeTargetVolumes[i] <= 0.0001f)
                        {
                            StopVoiceImmediate(i);
                            continue;
                        }
                    }
                }
                else if (!source.isPlaying)
                {
                    StopVoiceImmediate(i);
                    continue;
                }

                source.volume = _voiceBaseVolumes[i] * _duckCurrent;
            }

            if (_scheduleWaitWhenSilent && !HasAnyActiveVoice())
            {
                _scheduleWaitWhenSilent = false;
                BeginWait(_waitProfile != null ? _waitProfile : (_resolvedProfile != null ? _resolvedProfile : _fallbackProfile));
            }
        }

        private void StopAllVoicesImmediate()
        {
            for (int i = 0; i < MusicVoiceCount; i++)
                StopVoiceImmediate(i);
        }

        private void StopVoiceImmediate(int voiceIndex)
        {
            if (voiceIndex < 0 || voiceIndex >= MusicVoiceCount)
                return;

            AudioSource source = _musicSources[voiceIndex];
            AudioClip releasedClip = source != null ? source.clip : null;
            if (_voicePool != null)
            {
                _voicePool.ReleaseMusicVoice(voiceIndex);
            }
            else if (source != null)
            {
                source.Stop();
                source.clip = null;
                source.volume = 0f;
                source.loop = false;
            }

            if (releasedClip != null)
                AudioResidencyCache.ReleaseClip(releasedClip);

            _voiceProfiles[voiceIndex] = null;
            _voiceClips[voiceIndex] = default;
            _voiceBaseVolumes[voiceIndex] = 0f;
            _voiceFadeStartVolumes[voiceIndex] = 0f;
            _voiceFadeTargetVolumes[voiceIndex] = 0f;
            _voiceFadeDurations[voiceIndex] = 0f;
            _voiceFadeElapsedTimes[voiceIndex] = 0f;
            _voiceFading[voiceIndex] = false;
            _voiceEndingFadeTriggered[voiceIndex] = false;
            _voiceActive[voiceIndex] = false;
            _voiceIsOverride[voiceIndex] = false;

            if (_activeVoiceIndex == voiceIndex)
                _activeVoiceIndex = GetAnyActiveVoiceIndex();
        }

        private void StartFade(int voiceIndex, float targetVolume, float duration)
        {
            if (voiceIndex < 0 || voiceIndex >= MusicVoiceCount || !_voiceActive[voiceIndex])
                return;

            _voiceFadeStartVolumes[voiceIndex] = _voiceBaseVolumes[voiceIndex];
            _voiceFadeTargetVolumes[voiceIndex] = math.saturate(targetVolume);
            _voiceFadeDurations[voiceIndex] = duration > 0.01f ? duration : 0.01f;
            _voiceFadeElapsedTimes[voiceIndex] = 0f;
            _voiceFading[voiceIndex] = true;
        }

        private void BeginWait(HectonMusicBiomeProfile profile)
        {
            HectonMusicBiomeProfile waitProfile = profile != null ? profile : _fallbackProfile;
            _waitProfile = waitProfile;

            float minPause = waitProfile != null ? waitProfile.MinPauseSeconds : _fallbackPauseSeconds;
            float maxPause = waitProfile != null ? waitProfile.MaxPauseSeconds : _fallbackPauseSeconds;

            if (maxPause <= minPause)
                _waitTimerSeconds = minPause;
            else
                _waitTimerSeconds = NextRandomRange(minPause, maxPause);

            _playbackState = PlaybackState.Waiting;
            TraceEvent("Wait", waitProfile, null);
        }

        private bool TrySelectCue(HectonMusicBiomeProfile rootProfile, bool highTension, bool preferShort, out HectonMusicClip selectedCue, out HectonMusicBiomeProfile selectedProfile)
        {
            _selectionUsedCrossTension = false;
            _selectionUsedDepthBlend = false;

            if (rootProfile != null &&
                rootProfile.AllowCrossTensionMix &&
                rootProfile.CrossTensionMixChance > 0f &&
                PoolHasValidClips(GetPool(rootProfile, highTension, preferShort)) &&
                PoolHasValidClips(GetPool(rootProfile, !highTension, preferShort)) &&
                NextRandom01() <= rootProfile.CrossTensionMixChance &&
                TrySelectCueFromMode(rootProfile, !highTension, preferShort, out selectedCue, out selectedProfile))
            {
                _selectionUsedCrossTension = true;
                return true;
            }

            if (TrySelectCueFromMode(rootProfile, highTension, preferShort, out selectedCue, out selectedProfile))
                return true;

            if (preferShort)
                return TrySelectCueFromMode(rootProfile, highTension, false, out selectedCue, out selectedProfile);

            if (TrySelectCueFromMode(rootProfile, !highTension, false, out selectedCue, out selectedProfile))
                return true;

            return TrySelectCueFromMode(rootProfile, !highTension, true, out selectedCue, out selectedProfile);
        }

        private void ResolveDepthBlendProfile(HectonMusicBiomeProfile rootProfile, out HectonMusicBiomeProfile depthBlendProfile, out int depthBlendWeight)
        {
            depthBlendProfile = null;
            depthBlendWeight = 0;

            if (_depthBlendWindowMeters <= 0f ||
                _depthBlendMaxWeight <= 0 ||
                rootProfile == null ||
                !TryResolveBiomeMatrixContext(out _, out _, out float depthMeters))
            {
                return;
            }

            float nearestBoundaryDistance = float.MaxValue;
            HectonMusicBiomeProfile candidate = null;

            if (ReferenceEquals(rootProfile, _shallowProfile) && _shelfProfile != null)
            {
                candidate = _shelfProfile;
                nearestBoundaryDistance = math.abs(600f - depthMeters);
            }
            else if (ReferenceEquals(rootProfile, _shelfProfile))
            {
                float shallowDistance = _shallowProfile != null ? math.abs(600f - depthMeters) : float.MaxValue;
                float abyssDistance = _abyssProfile != null ? math.abs(3500f - depthMeters) : float.MaxValue;

                if (shallowDistance <= abyssDistance)
                {
                    candidate = _shallowProfile;
                    nearestBoundaryDistance = shallowDistance;
                }
                else
                {
                    candidate = _abyssProfile;
                    nearestBoundaryDistance = abyssDistance;
                }
            }
            else if (ReferenceEquals(rootProfile, _abyssProfile) && _shelfProfile != null)
            {
                candidate = _shelfProfile;
                nearestBoundaryDistance = math.abs(3500f - depthMeters);
            }

            if (candidate == null || ReferenceEquals(candidate, rootProfile) || nearestBoundaryDistance > _depthBlendWindowMeters)
                return;

            float normalized = 1f - (nearestBoundaryDistance * math.rcp(math.max(0.0001f, _depthBlendWindowMeters)));
            if (normalized <= 0f)
                return;

            depthBlendProfile = candidate;
            depthBlendWeight = math.clamp((int)(normalized * _depthBlendMaxWeight + 0.5f), 1, _depthBlendMaxWeight);
        }

        private bool TrySelectCueFromMode(HectonMusicBiomeProfile rootProfile, bool highTension, bool preferShort, out HectonMusicClip selectedCue, out HectonMusicBiomeProfile selectedProfile)
        {
            selectedCue = default;
            selectedProfile = null;

            if (rootProfile == null)
                return false;

            HectonMusicProfileBlend[] bleedProfiles = highTension ? rootProfile.TenseBleedProfiles : rootProfile.CalmBleedProfiles;
            int localWeight = highTension ? rootProfile.LocalTenseWeight : rootProfile.LocalCalmWeight;
            int totalWeight = 0;
            HectonMusicBiomeProfile depthBlendProfile;
            int depthBlendWeight;
            ResolveDepthBlendProfile(rootProfile, out depthBlendProfile, out depthBlendWeight);

            if (PoolHasValidClips(GetPool(rootProfile, highTension, preferShort)))
                totalWeight += localWeight;

            bool depthBlendValid = depthBlendProfile != null && depthBlendWeight > 0 && PoolHasValidClips(GetPool(depthBlendProfile, highTension, preferShort));
            if (depthBlendValid)
                totalWeight += depthBlendWeight;

            if (bleedProfiles != null)
            {
                for (int i = 0; i < bleedProfiles.Length; i++)
                {
                    HectonMusicBiomeProfile bleedProfile = bleedProfiles[i].Profile;
                    if (bleedProfile == null)
                        continue;

                    if (PoolHasValidClips(GetPool(bleedProfile, highTension, preferShort)))
                        totalWeight += bleedProfiles[i].Weight;
                }
            }

            if (totalWeight <= 0)
                return false;

            int roll = NextRandomRangeInt(0, totalWeight);
            if (PoolHasValidClips(GetPool(rootProfile, highTension, preferShort)))
            {
                if (roll < localWeight)
                {
                    selectedProfile = rootProfile;
                    return TrySelectCueFromPool(GetPool(rootProfile, highTension, preferShort), preferShort, rootProfile, out selectedCue);
                }

                roll -= localWeight;
            }

            if (depthBlendValid)
            {
                if (roll < depthBlendWeight)
                {
                    selectedProfile = depthBlendProfile;
                    _selectionUsedDepthBlend = true;
                    return TrySelectCueFromPool(GetPool(depthBlendProfile, highTension, preferShort), preferShort, depthBlendProfile, out selectedCue);
                }

                roll -= depthBlendWeight;
            }

            if (bleedProfiles != null)
            {
                for (int i = 0; i < bleedProfiles.Length; i++)
                {
                    HectonMusicBiomeProfile bleedProfile = bleedProfiles[i].Profile;
                    if (bleedProfile == null)
                        continue;

                    if (!PoolHasValidClips(GetPool(bleedProfile, highTension, preferShort)))
                        continue;

                    int weight = bleedProfiles[i].Weight;
                    if (roll < weight)
                    {
                        selectedProfile = bleedProfile;
                        return TrySelectCueFromPool(GetPool(bleedProfile, highTension, preferShort), preferShort, bleedProfile, out selectedCue);
                    }

                    roll -= weight;
                }
            }

            return false;
        }

        private bool TrySelectCueFromPool(HectonMusicClip[] pool, bool shortForm, HectonMusicBiomeProfile sourceProfile, out HectonMusicClip selectedCue)
        {
            selectedCue = default;
            if (pool == null || pool.Length == 0)
                return false;

            int totalWeight = 0;
            int validCount = 0;
            int repeatHorizon = sourceProfile != null
                ? (shortForm ? sourceProfile.ShortRepeatHorizon : sourceProfile.LongRepeatHorizon)
                : 1;

            for (int i = 0; i < pool.Length; i++)
            {
                HectonMusicClip cue = pool[i];
                if (!cue.IsValid)
                    continue;

                validCount++;
                if (!IsClipBlockedByHistory(cue.Clip, shortForm, repeatHorizon))
                    totalWeight += cue.Weight;
            }

            bool bypassHistory = false;
            if (validCount > 0 && totalWeight <= 0)
            {
                bypassHistory = true;
                for (int i = 0; i < pool.Length; i++)
                {
                    HectonMusicClip cue = pool[i];
                    if (!cue.IsValid)
                        continue;

                    totalWeight += cue.Weight;
                }
            }

            if (validCount <= 0 || totalWeight <= 0)
                return false;

            int roll = NextRandomRangeInt(0, totalWeight);

            for (int i = 0; i < pool.Length; i++)
            {
                HectonMusicClip cue = pool[i];
                if (!cue.IsValid)
                    continue;

                if (!bypassHistory && IsClipBlockedByHistory(cue.Clip, shortForm, repeatHorizon))
                    continue;

                int weight = cue.Weight;
                if (roll < weight)
                {
                    selectedCue = cue;
                    return true;
                }

                roll -= weight;
            }

            return false;
        }

        private HectonMusicClip[] GetPool(HectonMusicBiomeProfile profile, bool highTension, bool preferShort)
        {
            if (profile == null)
                return null;

            if (highTension)
                return preferShort ? profile.TenseShortTracks : profile.TenseLongTracks;

            return preferShort ? profile.CalmShortTracks : profile.CalmLongTracks;
        }

        private static bool PoolHasValidClips(HectonMusicClip[] pool)
        {
            if (pool == null)
                return false;

            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i].IsValid)
                    return true;
            }

            return false;
        }

        private bool ShouldSelectShortTrack(HectonMusicBiomeProfile profile)
        {
            if (profile == null || _shortTrackCooldownRemaining > 0f || profile.ShortTrackChance <= 0f)
                return false;

            return NextRandom01() <= profile.ShortTrackChance;
        }

        private bool ShouldTriggerEndFade(int voiceIndex, float fadeOutSeconds)
        {
            if (voiceIndex < 0 || voiceIndex >= MusicVoiceCount)
                return false;

            AudioSource source = _musicSources[voiceIndex];
            if (source == null || source.clip == null || source.loop || !source.isPlaying)
                return false;

            float clipLength = source.clip.length;
            if (clipLength <= 0f)
                return false;

            float remaining = clipLength - source.time;
            return remaining <= fadeOutSeconds + 0.02f;
        }

        private bool IsClipBlockedByHistory(AudioClip clip, bool shortForm, int repeatHorizon)
        {
            if (clip == null || repeatHorizon <= 0)
                return false;

            AudioClip[] history = shortForm ? _recentShortClips : _recentLongClips;
            int count = shortForm ? _recentShortCount : _recentLongCount;
            int writeIndex = shortForm ? _recentShortWriteIndex : _recentLongWriteIndex;
            if (history == null || history.Length == 0 || count <= 0)
                return false;

            int sampleCount = repeatHorizon < count ? repeatHorizon : count;
            for (int i = 0; i < sampleCount; i++)
            {
                int historyIndex = writeIndex - 1 - i;
                if (historyIndex < 0)
                    historyIndex += history.Length;

                if (history[historyIndex] == clip)
                    return true;
            }

            return false;
        }

        private static void PushRecentClip(AudioClip[] history, ref int writeIndex, ref int count, AudioClip clip)
        {
            if (history == null || history.Length == 0 || clip == null)
                return;

            history[writeIndex] = clip;
            writeIndex++;
            if (writeIndex >= history.Length)
                writeIndex = 0;

            if (count < history.Length)
                count++;
        }

        private void CacheLastBedClip(HectonMusicClip cue)
        {
            bool shortForm = cue.Role == HectonMusicClipRole.ExplorationShort || cue.Role == HectonMusicClipRole.CombatShort;
            switch (cue.Role)
            {
                case HectonMusicClipRole.ExplorationShort:
                case HectonMusicClipRole.CombatShort:
                    _lastShortClip = cue.Clip;
                    PushRecentClip(_recentShortClips, ref _recentShortWriteIndex, ref _recentShortCount, cue.Clip);
                    break;

                default:
                    _lastLongClip = cue.Clip;
                    PushRecentClip(_recentLongClips, ref _recentLongWriteIndex, ref _recentLongCount, cue.Clip);
                    break;
            }

#if UNITY_EDITOR
            _debugActiveCueId = string.IsNullOrEmpty(cue.CueId) ? "Unnamed Cue" : cue.CueId;
#endif
        }

        private bool TryPlayStinger(HectonMusicBiomeProfile profile, StingerKind kind)
        {
            if (ProceduralSynthOwnsMusicPlayback)
            {
                InjectProceduralStinger(kind);
                HectonMusicBiomeProfile traceProfile = profile != null ? profile : _fallbackProfile;
                TraceEvent(ResolveStingerTraceLabel(kind), traceProfile, null);
                return true;
            }

            if (_stingerSource == null)
                return false;

            HectonMusicBiomeProfile sourceProfile = profile != null ? profile : _fallbackProfile;
            HectonMusicClip selectedCue;

            if (!TrySelectStingerCue(GetStingerPool(sourceProfile, kind), out selectedCue))
            {
                if (sourceProfile != _fallbackProfile && TrySelectStingerCue(GetStingerPool(_fallbackProfile, kind), out selectedCue))
                    sourceProfile = _fallbackProfile;
                else
                    return false;
            }

            if (_voicePool != null)
                _voicePool.ReleaseStingerVoice();
            else
            {
                _stingerSource.Stop();
                _stingerSource.clip = null;
            }

            _stingerSource.clip = selectedCue.Clip;
            _stingerSource.loop = false;
            _stingerSource.volume = math.saturate(selectedCue.Volume);
            _stingerSource.outputAudioMixerGroup = ResolveStingerMixerGroup();
            _stingerSource.Play();
            if (_voicePool != null)
                _voicePool.MarkStingerInUse();

            _lastStingerClip = selectedCue.Clip;
            if (HasAnyActiveVoice())
            {
                _stingerDuckActive = true;
                StartDuck(_stingerDuckFactor, _stingerDuckAttackSeconds);
            }

            TraceEvent(ResolveStingerTraceLabel(kind), sourceProfile, selectedCue.Clip);

            return true;
        }

        private bool TryPlayPendingStinger(StingerKind kind)
        {
            int cooldownIndex = (int)kind;
            if (_stingerCooldownRemainingByKind == null || cooldownIndex < 0 || cooldownIndex >= _stingerCooldownRemainingByKind.Length)
                return false;

            if (_stingerCooldownRemainingByKind[cooldownIndex] > 0f)
                return false;

            HectonMusicBiomeProfile sourceProfile = kind == StingerKind.Danger
                ? (_combatProfile != null ? _combatProfile : (_resolvedProfile != null ? _resolvedProfile : _fallbackProfile))
                : (_resolvedProfile != null ? _resolvedProfile : _fallbackProfile);
            if (!TryPlayStinger(sourceProfile, kind))
                return false;

            _stingerCooldownRemainingByKind[cooldownIndex] = ResolveStingerCooldownSeconds(kind);
            return true;
        }

        private float ResolveStingerCooldownSeconds(StingerKind kind)
        {
            switch (kind)
            {
                case StingerKind.Discovery:
                    return _discoveryStingerCooldownSeconds;
                case StingerKind.Danger:
                    return _dangerStingerCooldownSeconds;
                case StingerKind.Recovery:
                    return _recoveryStingerCooldownSeconds;
                default:
                    return 0f;
            }
        }

        private HectonMusicClip[] GetStingerPool(HectonMusicBiomeProfile profile, StingerKind kind)
        {
            if (profile == null)
                return null;

            switch (kind)
            {
                case StingerKind.Discovery:
                    return profile.DiscoveryStingers;
                case StingerKind.Danger:
                    return profile.DangerStingers;
                case StingerKind.Recovery:
                    return profile.RecoveryStingers;
                default:
                    return null;
            }
        }

        private bool TrySelectStingerCue(HectonMusicClip[] pool, out HectonMusicClip selectedCue)
        {
            selectedCue = default;
            if (pool == null || pool.Length == 0)
                return false;

            int totalWeight = 0;
            int totalWithoutRepeat = 0;
            int validCount = 0;

            for (int i = 0; i < pool.Length; i++)
            {
                HectonMusicClip cue = pool[i];
                if (!cue.IsValid)
                    continue;

                validCount++;
                totalWeight += cue.Weight;
                if (cue.Clip != _lastStingerClip)
                    totalWithoutRepeat += cue.Weight;
            }

            if (validCount <= 0 || totalWeight <= 0)
                return false;

            bool excludeRepeat = validCount > 1 && _lastStingerClip != null && totalWithoutRepeat > 0;
            int roll = NextRandomRangeInt(0, excludeRepeat ? totalWithoutRepeat : totalWeight);

            for (int i = 0; i < pool.Length; i++)
            {
                HectonMusicClip cue = pool[i];
                if (!cue.IsValid)
                    continue;

                if (excludeRepeat && cue.Clip == _lastStingerClip)
                    continue;

                int weight = cue.Weight;
                if (roll < weight)
                {
                    selectedCue = cue;
                    return true;
                }

                roll -= weight;
            }

            return false;
        }

        private void UpdateStingerState()
        {
            if (_stingerSource == null)
                return;

            if (_stingerDuckActive && !_stingerSource.isPlaying)
            {
                _stingerDuckActive = false;
                StartDuck(1f, _stingerDuckReleaseSeconds);
            }
        }

        private void StartDuck(float target, float duration)
        {
            _duckStart = _duckCurrent;
            _duckTarget = math.saturate(target);
            _duckDuration = duration > 0.01f ? duration : 0.01f;
            _duckElapsed = 0f;
            _duckFading = true;
        }

        private void UpdateDuck(float deltaTime)
        {
            if (!_duckFading)
                return;

            _duckElapsed += deltaTime;
            float t = _duckElapsed * math.rcp(_duckDuration);
            if (t > 1f)
                t = 1f;

            _duckCurrent = math.lerp(_duckStart, _duckTarget, t);
            if (t >= 1f)
                _duckFading = false;
        }

        private void UpdateOverrideState()
        {
            int overrideVoiceIndex = GetOverrideVoiceIndex();
            if (overrideVoiceIndex >= 0)
            {
                _activeVoiceIndex = overrideVoiceIndex;
                _playbackState = PlaybackState.Override;
                return;
            }

            _overrideActive = false;
            _overrideLoop = false;
            _overrideClip = null;
            _overrideVolume = 1f;
            _playbackState = PlaybackState.Silent;
            _pendingImmediateSelection = true;
        }

        private void ForceOverrideTrackInternal(AudioClip clip, float volume, bool loop, float fadeInSeconds, float fadeOutSeconds)
        {
            float inSeconds = fadeInSeconds > 0f ? fadeInSeconds : _defaultOverrideFadeInSeconds;
            _overrideFadeOutSeconds = fadeOutSeconds > 0f ? fadeOutSeconds : _defaultOverrideFadeOutSeconds;
            _overrideActive = true;
            _overrideLoop = loop;
            _overrideClip = clip;
            _overrideVolume = math.saturate(volume);
            _pendingImmediateSelection = false;
            _scheduleWaitWhenSilent = false;
            _waitTimerSeconds = 0f;

            if (ProceduralSynthOwnsMusicPlayback)
            {
                EnsureProceduralSynthRuntime();
                RefreshForegroundSpeechMusicDucking();
                bool emergencyBreathDominates = IsEmergencyBreathDominant();
                bool foregroundSpeechActive = IsForegroundSpeechActive();
                bool suppressReactiveImpulses = emergencyBreathDominates || foregroundSpeechActive;
                float overrideActivity01 = emergencyBreathDominates ? 0f : ApplyForegroundSpeechMusicDuck01(_overrideVolume);
                float overrideImpulse01 = suppressReactiveImpulses ? 0f : _overrideVolume;
                float overridePitchKick01 = suppressReactiveImpulses ? 0f : 1f;
                uint flags = DynamicMusicScalarSignal.FlagExternalScalars;
                if (suppressReactiveImpulses)
                    flags |= DynamicMusicScalarSignal.FlagSuppressReactiveImpulses;

                if (emergencyBreathDominates)
                {
                    _proceduralMusicActivity01 = 0f;
                    _debugMusicActivity01 = 0f;
                    _musicActivityReason = MusicActivityReason.Emergency;
                }
                else if (!foregroundSpeechActive)
                {
                    flags |= DynamicMusicScalarSignal.FlagStingerImpulse | DynamicMusicScalarSignal.FlagOverrideImpulse;
                }

                PushDynamicMusicSignal(
                    math.saturate(math.isfinite(_resolvedTension01) ? _resolvedTension01 : 0f),
                    ResolveLayerDepthMeters(),
                    math.saturate(math.isfinite(HomeostasisBrain.GlobalQualityWeight) ? HomeostasisBrain.GlobalQualityWeight : 1f),
                    overrideImpulse01,
                    overrideImpulse01,
                    overridePitchKick01,
                    overrideActivity01,
                    flags);

                _activeVoiceIndex = InvalidVoiceIndex;
                _playbackState = PlaybackState.Override;
                TraceEvent("Override:Start", null, null);
                return;
            }

            int nextVoiceIndex = GetInactiveVoiceIndex();
            if (nextVoiceIndex < 0)
                nextVoiceIndex = _activeVoiceIndex == 0 ? 1 : 0;
            if (nextVoiceIndex < 0)
                nextVoiceIndex = 0;

            StopVoiceImmediate(nextVoiceIndex);
            ConfigureVoice(nextVoiceIndex, null, default, clip, 0f, loop, true);
            StartFade(nextVoiceIndex, _overrideVolume, inSeconds);

            for (int i = 0; i < MusicVoiceCount; i++)
            {
                if (i == nextVoiceIndex || !_voiceActive[i])
                    continue;

                StartFade(i, 0f, inSeconds);
            }

            _activeVoiceIndex = nextVoiceIndex;
            _playbackState = PlaybackState.Override;
            TraceEvent("Override:Start", null, clip);
        }

        private void ClearForcedOverrideInternal(bool immediate)
        {
            if (!_overrideActive && GetOverrideVoiceIndex() < 0)
                return;

            int overrideVoiceIndex = GetOverrideVoiceIndex();
            _overrideActive = false;
            _overrideLoop = false;
            _overrideClip = null;
            _playbackState = PlaybackState.Silent;

            if (overrideVoiceIndex >= 0)
            {
                if (immediate)
                    StopVoiceImmediate(overrideVoiceIndex);
                else
                    StartFade(overrideVoiceIndex, 0f, _overrideFadeOutSeconds > 0f ? _overrideFadeOutSeconds : _defaultOverrideFadeOutSeconds);
            }

            _pendingImmediateSelection = true;
            TraceEvent(immediate ? "Override:ClearImmediate" : "Override:ClearFade", null, null);
        }

        private void StopMusicInternal(float fadeOutSeconds)
        {
            _overrideActive = false;
            _overrideLoop = false;
            _overrideClip = null;
            _pendingImmediateSelection = false;
            _scheduleWaitWhenSilent = false;
            _waitTimerSeconds = 0f;
            _playbackState = PlaybackState.Silent;

            if (ProceduralSynthOwnsMusicPlayback)
            {
                _proceduralMusicActivity01 = 0f;
                _proceduralPhraseTimerSeconds = 0f;
                _musicActivityReason = MusicActivityReason.Silent;
                _debugMusicActivity01 = 0f;
                _stingerDuckActive = false;
                PublishProceduralMusicStopSignal();
                StartDuck(1f, _stingerDuckReleaseSeconds);
                return;
            }

            if (_stingerSource != null)
            {
                if (_voicePool != null)
                    _voicePool.ReleaseStingerVoice();
                else
                {
                    _stingerSource.Stop();
                    _stingerSource.clip = null;
                }
            }

            _stingerDuckActive = false;
            StartDuck(1f, _stingerDuckReleaseSeconds);

            if (fadeOutSeconds <= 0.01f)
            {
                StopAllVoicesImmediate();
                return;
            }

            for (int i = 0; i < MusicVoiceCount; i++)
            {
                if (_voiceActive[i])
                    StartFade(i, 0f, fadeOutSeconds);
            }

            TraceEvent("StopMusic", _resolvedProfile, null);
        }

        private int GetInactiveVoiceIndex()
        {
            for (int i = 0; i < MusicVoiceCount; i++)
            {
                if (!_voiceActive[i])
                    return i;
            }

            return InvalidVoiceIndex;
        }

        private int GetAnyActiveVoiceIndex()
        {
            for (int i = 0; i < MusicVoiceCount; i++)
            {
                if (_voiceActive[i])
                    return i;
            }

            return InvalidVoiceIndex;
        }

        private int GetOverrideVoiceIndex()
        {
            for (int i = 0; i < MusicVoiceCount; i++)
            {
                if (_voiceActive[i] && _voiceIsOverride[i])
                    return i;
            }

            return InvalidVoiceIndex;
        }

        private bool HasAnyActiveVoice()
        {
            for (int i = 0; i < MusicVoiceCount; i++)
            {
                if (_voiceActive[i])
                    return true;
            }

            return false;
        }

        private AudioMixerGroup ResolveMusicMixerGroup()
        {
            if (_musicMixerGroup != null)
                return _musicMixerGroup;

            IAudioService audioService = ResolveAudioService();
            if (audioService != null)
                return audioService.AmbientGroup;

            return null;
        }

        private AudioMixerGroup ResolveStingerMixerGroup()
        {
            if (_stingerMixerGroup != null)
                return _stingerMixerGroup;

            return ResolveMusicMixerGroup();
        }

        private void HandleAcousticZoneChanged(bool isInterior)
        {
            if (_runtimeOwnerAborted)
                return;

            if (_hasLastAcousticInteriorState && _lastAcousticInteriorState == isInterior)
                return;

            _lastAcousticInteriorState = isInterior;
            _hasLastAcousticInteriorState = true;
            ReevaluateContext(true);
        }

        private void HandleMatrixBiomeChanged(HectonBiomeMatrixProfile profile)
        {
            if (_runtimeOwnerAborted)
                return;

            SetMatrixBiomeProfile(profile);
        }

        private void HandleDepthTierChanged(int depthTier, float depthMeters)
        {
            if (_runtimeOwnerAborted)
                return;

            ReevaluateContext(true);
        }

        private void HandleDepthZoneEntered(DepthZoneProfile zone)
        {
            if (_runtimeOwnerAborted)
                return;

            ReevaluateContext(true);

            if (zone == null || _currentBaseContext)
                return;

            IFirstHourReadModel firstHourDirector = ResolveFirstHourDirector();
            if (firstHourDirector != null &&
                !firstHourDirector.IsFirstHourMilestoneComplete((int)FirstHourMilestone.Orientation))
                return;

            if (ShouldPlayDepthDangerStinger(zone))
            {
                PlayDangerStinger();
                return;
            }

            if (ShouldPlayDepthDiscoveryStinger(zone))
                PlayDiscoveryStinger();
        }

        private void HandleDepthZoneExited(DepthZoneProfile zone)
        {
            if (_runtimeOwnerAborted)
                return;

            ReevaluateContext(true);

            if (zone == null || _currentBaseContext || !ShouldPlayDepthRecoveryStinger(zone))
                return;

            PlayRecoveryStinger();
        }

        private void HandleRareDiscoveryRequested(Vector3 position)
        {
            if (_runtimeOwnerAborted)
                return;

            IFirstHourReadModel firstHourDirector = ResolveFirstHourDirector();
            if (firstHourDirector != null &&
                !firstHourDirector.IsFirstHourMilestoneComplete((int)FirstHourMilestone.FirstCraft))
            {
                return;
            }

            PlayDiscoveryStinger();
        }

        private void HandleSpawnHordeRequested(Vector3 position)
        {
            if (_runtimeOwnerAborted)
                return;

            _combatLatched = true;
            PlayDangerStinger();
            ReevaluateContext(true);
        }

        private void HandlePredatorPressureChanged(bool pressureEnabled)
        {
            if (_runtimeOwnerAborted)
                return;

            if (!pressureEnabled && _combatLatched)
                PlayRecoveryStinger();

            ReevaluateContext(true);
        }

        private void HandleThreatSpike(Vector3 position, float intensity)
        {
            if (_runtimeOwnerAborted)
                return;

            _combatLatched = true;
            PlayDangerStinger();
            ReevaluateContext(true);
        }

        private void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
        {
            if (_runtimeOwnerAborted)
                return;

            ResolveDependenciesForSceneCold(nextScene);
            ReevaluateContext(true);
        }

        private void RefreshSceneFlags(Scene activeScene)
        {
            string sceneName = activeScene.name;
            _menuSceneActive = ContainsAnyToken(sceneName, MenuSceneTokens);
            _prologueSceneActive = ContainsAnyToken(sceneName, PrologueSceneTokens);
        }

        private static bool ContainsAnyToken(string source, string[] tokens)
        {
            if (string.IsNullOrEmpty(source) || tokens == null || tokens.Length == 0)
                return false;

            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (string.IsNullOrEmpty(token))
                    continue;

                if (source.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private bool ShouldPlayDepthDiscoveryStinger(DepthZoneProfile zone)
        {
            if (zone == null || _combatLatched || _overrideActive)
                return false;

            IFirstHourReadModel firstHourDirector = ResolveFirstHourDirector();
            if (firstHourDirector != null &&
                !firstHourDirector.IsFirstHourMilestoneComplete((int)FirstHourMilestone.FirstCraft))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(zone.discoveryId))
                return true;

            if (zone.isThermal || zone.hasCaves)
                return zone.minDepth >= 180f;

            return zone.minDepth >= 600f && zone.dangerLevel < 0.72f;
        }

        private static bool ShouldPlayDepthDangerStinger(DepthZoneProfile zone)
        {
            if (zone == null)
                return false;

            if (zone.dangerLevel >= 0.72f)
                return true;

            if (zone.requiredHullTier >= 2 && zone.minDepth >= 600f)
                return true;

            return zone.isThermal && zone.dangerLevel >= 0.45f;
        }

        private bool ShouldPlayDepthRecoveryStinger(DepthZoneProfile exitedZone)
        {
            if (exitedZone == null || _combatLatched || _overrideActive)
                return false;

            if (!ShouldPlayDepthDangerStinger(exitedZone))
                return false;

            IDepthZoneReadModel depthZoneReadModel = ResolveDepthZoneReadModel();
            DepthZoneProfile currentZone = depthZoneReadModel != null ? depthZoneReadModel.CurrentZone : null;
            if (currentZone == null)
                return true;

            return ResolveDepthZonePressure01(currentZone) + 0.18f < ResolveDepthZonePressure01(exitedZone);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void TraceSelection(HectonMusicBiomeProfile rootProfile, HectonMusicBiomeProfile playbackProfile, HectonMusicClip selectedCue, bool highTension, bool preferShort)
        {
#if UNITY_EDITOR
            if (!_enableTelemetry)
                return;

            _debugLastSelectionReason = "music-selection";
            TraceEvent(
                ResolveSelectionTraceLabel(_selectionUsedDepthBlend, _selectionUsedCrossTension, highTension, preferShort),
                playbackProfile,
                selectedCue.Clip);
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void TraceEvent(string eventLabel, HectonMusicBiomeProfile profile, AudioClip clip)
        {
#if UNITY_EDITOR
            if (!_enableTelemetry)
                return;

            _ = eventLabel;
            _ = profile;
            _ = clip;
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void WriteDebugState()
        {
#if UNITY_EDITOR
            if (!_enableTelemetry)
                return;

            float now = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            if (now < _nextEditorDebugStateTime)
                return;

            _nextEditorDebugStateTime = now + EditorDebugStateIntervalSeconds;

            _debugResolvedProfile = _resolvedProfile != null
                ? (!string.IsNullOrEmpty(_resolvedProfile.ProfileLabel) ? _resolvedProfile.ProfileLabel : _resolvedProfile.name)
                : (_fallbackProfile != null ? _fallbackProfile.ProfileLabel : "None");

            int debugVoiceIndex = _activeVoiceIndex;
            if (debugVoiceIndex >= 0 && debugVoiceIndex < MusicVoiceCount && _voiceActive[debugVoiceIndex] && !string.IsNullOrEmpty(_voiceClips[debugVoiceIndex].CueId))
                _debugActiveCueId = _voiceClips[debugVoiceIndex].CueId;
            else if (_overrideClip != null)
                _debugActiveCueId = "Override Clip";
            else if (_stingerSource != null && _stingerSource.isPlaying && _stingerSource.clip != null)
                _debugActiveCueId = "Stinger Clip";
            else if (!HasAnyActiveVoice())
                _debugActiveCueId = "None";

            _debugTension01 = _resolvedTension01;
            _debugCombatLatched = _combatLatched;
            _debugTenseExplorationLatched = _tenseExplorationLatched;
            _debugWaitTimer = _waitTimerSeconds;
#endif
        }

        private static string ResolveStingerTraceLabel(StingerKind kind)
        {
            switch (kind)
            {
                case StingerKind.Discovery:
                    return "Stinger:Discovery";
                case StingerKind.Danger:
                    return "Stinger:Danger";
                case StingerKind.Recovery:
                    return "Stinger:Recovery";
                default:
                    return "Stinger:Unknown";
            }
        }

        private static string ResolveSelectionTraceLabel(bool depthBlend, bool crossTension, bool highTension, bool preferShort)
        {
            if (depthBlend)
            {
                if (highTension)
                    return preferShort ? "Select:depth-blend:tense:short" : "Select:depth-blend:tense:long";

                return preferShort ? "Select:depth-blend:calm:short" : "Select:depth-blend:calm:long";
            }

            if (crossTension)
            {
                if (highTension)
                    return preferShort ? "Select:cross-tension:tense:short" : "Select:cross-tension:tense:long";

                return preferShort ? "Select:cross-tension:calm:short" : "Select:cross-tension:calm:long";
            }

            if (highTension)
                return preferShort ? "Select:local:tense:short" : "Select:local:tense:long";

            return preferShort ? "Select:local:calm:short" : "Select:local:calm:long";
        }

        private static float ResolvePressure01(int authoredValue)
        {
            return math.saturate((authoredValue - 1f) * AuthoredPressureRangeInv);
        }

        private static bool ReadsAsSafeZoneKind(WorldZoneAnchor.ZoneKind kind)
        {
            switch (kind)
            {
                case WorldZoneAnchor.ZoneKind.Service:
                case WorldZoneAnchor.ZoneKind.Power:
                case WorldZoneAnchor.ZoneKind.Fabrication:
                case WorldZoneAnchor.ZoneKind.Construction:
                    return true;
            }

            return false;
        }

        private static float ResolveZoneKindPressure01(WorldZoneAnchor.ZoneKind kind)
        {
            switch (kind)
            {
                case WorldZoneAnchor.ZoneKind.Trial:
                case WorldZoneAnchor.ZoneKind.Combat:
                    return 1f;
                case WorldZoneAnchor.ZoneKind.Progression:
                    return 0.72f;
                case WorldZoneAnchor.ZoneKind.Navigation:
                    return 0.42f;
                case WorldZoneAnchor.ZoneKind.Resources:
                    return 0.20f;
                case WorldZoneAnchor.ZoneKind.Service:
                case WorldZoneAnchor.ZoneKind.Power:
                case WorldZoneAnchor.ZoneKind.Fabrication:
                case WorldZoneAnchor.ZoneKind.Construction:
                    return 0.08f;
            }

            return 0.18f;
        }

        private static float ResolveBiomePressure01(HectonBiomeMatrixProfile matrixProfile)
        {
            if (matrixProfile == null)
                return 0f;

            float survivalPressure01 = ResolvePressure01(matrixProfile.survivalPressure);
            float routePressure01 = ResolvePressure01(matrixProfile.routePressure);
            float pressure01 = survivalPressure01 * 0.72f + routePressure01 * 0.28f;
            return math.saturate(pressure01);
        }

        private static float ResolveRewardUnease01(HectonBiomeMatrixProfile matrixProfile)
        {
            if (matrixProfile == null)
                return 0f;

            float rewardPull01 = ResolvePressure01(matrixProfile.rewardPull);
            if (rewardPull01 <= 0f)
                return 0f;

            float rareRewardBonus = matrixProfile.rewardPull >= 4 && !string.IsNullOrWhiteSpace(matrixProfile.rareRewardHook)
                ? 0.18f
                : 0f;
            return math.saturate(rewardPull01 * 0.68f + rareRewardBonus);
        }

        private static float ResolveZonePressure01(WorldZoneAnchor currentZone)
        {
            if (currentZone == null)
                return 0f;

            float zonePressure01 = ResolveZoneKindPressure01(currentZone.Kind);
            if (currentZone.RouteCritical)
                zonePressure01 = math.max(zonePressure01, 0.46f);

            if (!string.IsNullOrWhiteSpace(currentZone.GameplayIntent))
            {
                if (ContainsAnyToken(currentZone.GameplayIntent, ThermalTokens))
                    zonePressure01 = math.max(zonePressure01, 0.62f);
                else if (ContainsAnyToken(currentZone.GameplayIntent, CaveTokens))
                    zonePressure01 = math.max(zonePressure01, 0.52f);
            }

            return math.saturate(zonePressure01);
        }

        private static float ResolveDepthZonePressure01(DepthZoneProfile depthZone)
        {
            if (depthZone == null)
                return 0f;

            float pressure01 = math.saturate(depthZone.dangerLevel);

            if (depthZone.requiredHullTier > 0)
                pressure01 = math.max(pressure01, math.saturate(depthZone.requiredHullTier * 0.24f));

            if (depthZone.hasCaves)
                pressure01 = math.max(pressure01, 0.44f);

            if (depthZone.isThermal)
                pressure01 = math.max(pressure01, 0.58f);

            if (depthZone.minDepth >= 600f)
                pressure01 = math.max(pressure01, 0.36f);

            if (depthZone.minDepth >= 3500f)
                pressure01 = math.max(pressure01, 0.62f);

            return math.saturate(pressure01);
        }

        private static SoundscapeTier SanitizeSoundscapeTier(SoundscapeTier tier)
        {
            switch (tier)
            {
                case SoundscapeTier.Surface:
                case SoundscapeTier.Shallow:
                case SoundscapeTier.Twilight:
                case SoundscapeTier.Darkness:
                case SoundscapeTier.Abyss:
                case SoundscapeTier.DeepAbyss:
                case SoundscapeTier.Thermal:
                    return tier;
                default:
                    return SoundscapeTier.Shallow;
            }
        }

        private static float ResolveSoundscapeDepthHintMeters(SoundscapeTier tier)
        {
            switch (tier)
            {
                case SoundscapeTier.Twilight:
                    return 150f;
                case SoundscapeTier.Darkness:
                    return 500f;
                case SoundscapeTier.Abyss:
                    return 1000f;
                case SoundscapeTier.DeepAbyss:
                    return 2000f;
                case SoundscapeTier.Thermal:
                    return 4000f;
                case SoundscapeTier.Surface:
                case SoundscapeTier.Shallow:
                default:
                    return 0f;
            }
        }

        private static float ResolveSoundscapePressure01(SoundscapeTier tier)
        {
            switch (tier)
            {
                case SoundscapeTier.Twilight:
                    return 0.22f;
                case SoundscapeTier.Darkness:
                    return 0.42f;
                case SoundscapeTier.Abyss:
                    return 0.64f;
                case SoundscapeTier.DeepAbyss:
                    return 0.82f;
                case SoundscapeTier.Thermal:
                    return 0.72f;
                case SoundscapeTier.Shallow:
                    return 0.06f;
                case SoundscapeTier.Surface:
                default:
                    return 0f;
            }
        }

        private static float ResolveSoundscapeRestScale(SoundscapeTier tier)
        {
            switch (tier)
            {
                case SoundscapeTier.Twilight:
                    return 1.10f;
                case SoundscapeTier.Darkness:
                    return 1.25f;
                case SoundscapeTier.Abyss:
                    return 1.40f;
                case SoundscapeTier.DeepAbyss:
                    return 1.55f;
                case SoundscapeTier.Thermal:
                    return 1.20f;
                case SoundscapeTier.Surface:
                    return 0.88f;
                case SoundscapeTier.Shallow:
                default:
                    return 1f;
            }
        }

        private static float ResolveSoundscapePhraseScale(SoundscapeTier tier)
        {
            switch (tier)
            {
                case SoundscapeTier.Twilight:
                    return 0.95f;
                case SoundscapeTier.Darkness:
                    return 0.84f;
                case SoundscapeTier.Abyss:
                    return 0.76f;
                case SoundscapeTier.DeepAbyss:
                    return 0.70f;
                case SoundscapeTier.Thermal:
                    return 0.88f;
                case SoundscapeTier.Surface:
                    return 0.95f;
                case SoundscapeTier.Shallow:
                default:
                    return 1f;
            }
        }

        private static float ResolveSafePocketSuppression01(HectonBiomeMatrixProfile matrixProfile, WorldZoneAnchor currentZone)
        {
            float suppression01 = 0f;

            if (currentZone != null)
            {
                if (ReadsAsSafeZoneKind(currentZone.Kind))
                    suppression01 = 1f;
                else if (currentZone.RouteCritical)
                    suppression01 = math.max(suppression01, 0.18f);
            }

            if (matrixProfile != null)
            {
                if (!string.IsNullOrWhiteSpace(matrixProfile.safePocketIdentity))
                    suppression01 = math.max(suppression01, matrixProfile.survivalPressure >= 4 ? 0.35f : 0.55f);

                if (matrixProfile.survivalPressure <= 2 && matrixProfile.rewardPull <= 2)
                    suppression01 = math.max(suppression01, 0.14f);
            }

            return math.saturate(suppression01);
        }

        private float ResolveFirstHourPressureBoost01(HectonBiomeMatrixProfile matrixProfile, WorldZoneAnchor currentZone)
        {
            IFirstHourReadModel firstHourDirector = ResolveFirstHourDirector();
            if (firstHourDirector == null || firstHourDirector.IsFirstHourComplete)
                return 0f;

            float boost01 = 0f;
            if (matrixProfile != null)
            {
                if (matrixProfile.depthTier >= 4)
                    boost01 = math.max(boost01, 0.42f);

                if (matrixProfile.survivalPressure >= 4)
                    boost01 = math.max(boost01, 0.58f);
            }

            if (currentZone != null)
            {
                if (currentZone.Kind == WorldZoneAnchor.ZoneKind.Progression || currentZone.Kind == WorldZoneAnchor.ZoneKind.Navigation)
                    boost01 = math.max(boost01, 0.34f);

                if (currentZone.RouteCritical)
                    boost01 = math.max(boost01, 0.26f);
            }

            if (!firstHourDirector.IsFirstHourMilestoneComplete((int)FirstHourMilestone.FirstCraft))
                boost01 = math.max(boost01, 0.18f);

            if (!firstHourDirector.IsFirstHourMilestoneComplete((int)FirstHourMilestone.FirstModule))
                boost01 = math.max(boost01, 0.32f);

            return math.saturate(boost01);
        }

        private static float InverseLerp(float a, float b, float value)
        {
            float denominator = b - a;
            if (math.abs(denominator) <= 0.000001f)
                return 0f;

            return math.saturate((value - a) * math.rcp(denominator));
        }

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            float delta = target - current;
            float absDelta = math.abs(delta);
            if (absDelta <= maxDelta || absDelta <= 0.000001f)
                return target;

            return current + math.sign(delta) * maxDelta;
        }

        private float NextRandomRange(float minInclusive, float maxInclusive)
        {
            return math.lerp(minInclusive, maxInclusive, NextRandom01());
        }

        private int NextRandomRangeInt(int minInclusive, int maxExclusive)
        {
            int span = math.max(1, maxExclusive - minInclusive);
            return minInclusive + (int)(NextRandomUInt() % (uint)span);
        }

        private float NextRandom01()
        {
            return (NextRandomUInt() & 0x00FFFFFFu) * Random24ToUnit;
        }

        private uint NextRandomUInt()
        {
            uint state = _musicRandomState != 0u ? _musicRandomState : 0xA341316Cu;
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            _musicRandomState = state;
            return state;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_combatExitThreshold > _combatEnterThreshold)
                _combatExitThreshold = _combatEnterThreshold;

            if (_tenseExplorationThreshold < 0f)
                _tenseExplorationThreshold = 0f;
            else if (_tenseExplorationThreshold > 1f)
                _tenseExplorationThreshold = 1f;

            if (_tenseExplorationReleaseThreshold > _tenseExplorationThreshold)
                _tenseExplorationReleaseThreshold = _tenseExplorationThreshold;

            if (_tenseExplorationReleaseThreshold < 0f)
                _tenseExplorationReleaseThreshold = 0f;

            if (_fallbackPauseSeconds < 0f)
                _fallbackPauseSeconds = 0f;

            if (_defaultOverrideFadeInSeconds < 0.01f)
                _defaultOverrideFadeInSeconds = 0.01f;

            if (_defaultOverrideFadeOutSeconds < 0.01f)
                _defaultOverrideFadeOutSeconds = 0.01f;

            if (_stingerDuckAttackSeconds < 0.01f)
                _stingerDuckAttackSeconds = 0.01f;

            if (_stingerDuckReleaseSeconds < 0.01f)
                _stingerDuckReleaseSeconds = 0.01f;

            if (_discoveryStingerCooldownSeconds < 0f)
                _discoveryStingerCooldownSeconds = 0f;

            if (_dangerStingerCooldownSeconds < 0f)
                _dangerStingerCooldownSeconds = 0f;

            if (_recoveryStingerCooldownSeconds < 0f)
                _recoveryStingerCooldownSeconds = 0f;

            if (_depthBlendWindowMeters < 0f)
                _depthBlendWindowMeters = 0f;
        }
#endif
    }
}
