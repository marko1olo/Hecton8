using Hecton8.Core;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Systems.AI;
using Hecton8.Atmosphere;
using Hecton8.Bootstrap;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace Hecton8.Audio
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-3900)] // Consumes zone/acoustic state resolved by earlier managers.
    public sealed class HectonMusicDirector : MonoBehaviour, ITickable, IUpdatable, ISlowTickable
    {
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

        private const int MusicVoiceCount = 2;
        private const int InvalidVoiceIndex = -1;
        private const float MixerFloorDb = -80f;
        private const float MixerCeilingDb = 0f;
        private static readonly int _PredatorThreatLayerMask = HectonLayerMasks.CreatureLayerMask;

        private static readonly string[] MenuSceneTokens = { "main_menu" };
        private static readonly string[] PrologueSceneTokens = { "prologue" };
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

        private static HectonMusicDirector _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeInstance()
        {
            if (!Application.isPlaying || _instance != null)
                return;

            TryInstantiateConfiguredRuntimeDirector();
        }

        [Header("References")]
        [Tooltip("Optional explicit world zone director. If null, runtime instance is used.")]
        [SerializeField] private WorldZoneDirector _worldZoneDirector;

        [Tooltip("Optional explicit biome matrix director. If null, runtime instance is used.")]
        [SerializeField] private BiomeMatrixDirector _biomeMatrixDirector;

        [Tooltip("Optional explicit depth-zone director. If null, runtime instance is used when available.")]
        [SerializeField] private DepthZoneDirector _depthZoneDirector;

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
        [SerializeField] private float _debugZonePressure01;
        [SerializeField] private float _debugDepthZonePressure01;
        [SerializeField] private float _debugRewardUnease01;
        [SerializeField] private float _debugSafePocketSuppression01;
        [SerializeField] private float _debugFirstHourPressureBoost01;
        [SerializeField] private float _debugLayerRhythm01;
        [SerializeField] private float _debugLayerBass01;
        [SerializeField] private float _debugLayerAtmosphere01;
        [SerializeField] private float _debugLayerDanger01;
        [SerializeField] private float _debugPredatorProximity01;
        [SerializeField] private float _debugStormPressure01;
        [SerializeField] private float _debugOxygenDanger01;

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
        private PlaybackState _playbackState = PlaybackState.Silent;
        private HectonMusicBiomeProfile _resolvedProfile;
        private HectonMusicBiomeProfile _manualProfile;
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
        private bool _pendingDangerStinger;
        private bool _pendingRecoveryStinger;
        private int _forceCalmSelectionsRemaining;
        private bool _selectionUsedCrossTension;
        private bool _selectionUsedDepthBlend;
        private bool _tenseExplorationLatched;
        private bool _lastAcousticInteriorState;
        private bool _hasLastAcousticInteriorState;
        private Transform _playerTransform;
        private HectonSurvivalSystem _survivalSystem;
        private AudioMixer _layerMixer;
        private float _layerRhythm01;
        private float _layerBass01;
        private float _layerAtmosphere01;
        private float _layerDanger01;
        private float _predatorProximity01;
        private float _stormPressure01;
        private float _oxygenDanger01;
        private float _lastRhythmDb = float.MinValue;
        private float _lastBassDb = float.MinValue;
        private float _lastAtmosphereDb = float.MinValue;
        private float _lastDangerDb = float.MinValue;

        /// <summary>
        /// Global access to the music director.
        /// </summary>
        public static HectonMusicDirector Instance => _instance;

        /// <summary>
        /// Silent singleton probe for optional callers.
        /// </summary>
        public static bool TryGetInstance(out HectonMusicDirector instance)
        {
            instance = _instance;
            return instance != null;
        }

        /// <summary>
        /// Currently resolved runtime profile.
        /// </summary>
        public HectonMusicBiomeProfile ActiveResolvedProfile => _resolvedProfile;

        /// <summary>
        /// True while a forced override cue is active.
        /// </summary>
        public bool IsOverrideActive => _overrideActive;

        /// <summary>
        /// Current normalized tension value used by the director.
        /// </summary>
        public float CurrentTension01 => _resolvedTension01;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

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

            BindAuthoredVoicePool();
            ResolveDependencies();
            RefreshSceneFlags(SceneManager.GetActiveScene());
        }

        private void OnEnable()
        {
            TryRegisterTickHandlers();
            AcousticZoneController.OnAcousticZoneChanged += HandleAcousticZoneChanged;
            BiomeMatrixDirector.OnMatrixBiomeChanged += HandleMatrixBiomeChanged;
            BiomeMatrixDirector.OnDepthTierChanged += HandleDepthTierChanged;
            DepthZoneEvents.RegisterZoneEntered(HandleDepthZoneEntered);
            DepthZoneEvents.RegisterZoneExited(HandleDepthZoneExited);
            HectonDirectorAI.OnRequestRareDiscovery += HandleRareDiscoveryRequested;
            HectonDirectorAI.OnRequestSpawnHorde += HandleSpawnHordeRequested;
            HectonDirectorAI.OnPredatorPressureChanged += HandlePredatorPressureChanged;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            _pendingImmediateSelection = true;
        }

        private void Start()
        {
            TryRegisterTickHandlers();
            ResolveDependencies();
            ReevaluateContext(true);
        }

        private void OnDisable()
        {
            StopMusicInternal(0f);
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            HectonDirectorAI.OnPredatorPressureChanged -= HandlePredatorPressureChanged;
            HectonDirectorAI.OnRequestSpawnHorde -= HandleSpawnHordeRequested;
            HectonDirectorAI.OnRequestRareDiscovery -= HandleRareDiscoveryRequested;
            DepthZoneEvents.UnregisterZoneExited(HandleDepthZoneExited);
            DepthZoneEvents.UnregisterZoneEntered(HandleDepthZoneEntered);
            BiomeMatrixDirector.OnDepthTierChanged -= HandleDepthTierChanged;
            BiomeMatrixDirector.OnMatrixBiomeChanged -= HandleMatrixBiomeChanged;
            AcousticZoneController.OnAcousticZoneChanged -= HandleAcousticZoneChanged;
            TryUnregisterTickHandlers();
        }

        private void OnDestroy()
        {
            StopMusicInternal(0f);
            TryUnregisterTickHandlers();

            if (_instance == this)
                _instance = null;
        }

        /// <summary>
        /// Handles fades, wait timers, and ducking.
        /// </summary>
        public void Tick(float deltaTime)
        {
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
            RefreshLayerThreatSnapshot();
            ReevaluateContext(false);
        }

        /// <summary>
        /// Forces a manual biome-profile override.
        /// </summary>
        public void SetManualBiomeProfile(HectonMusicBiomeProfile profile)
        {
            _manualProfile = profile;
            ReevaluateContext(true);
        }

        /// <summary>
        /// Clears the manual biome-profile override.
        /// </summary>
        public void ClearManualBiomeProfile()
        {
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
            _manualTensionOverride = true;
            _manualTension01 = math.saturate(tension01);
            ReevaluateContext(true);
        }

        /// <summary>
        /// Clears the manual tension override.
        /// </summary>
        public void ClearManualTensionOverride()
        {
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
            if (clip == null)
                return;

            ForceOverrideTrackInternal(clip, volume, loop, fadeInSeconds, fadeOutSeconds);
        }

        /// <summary>
        /// Clears the forced override and returns control to automatic routing.
        /// </summary>
        public void ClearForcedOverride(bool immediate = false)
        {
            ClearForcedOverrideInternal(immediate);
        }

        /// <summary>
        /// Plays a discovery stinger over the current bed.
        /// </summary>
        public void PlayDiscoveryStinger()
        {
            if (_overrideActive || _combatLatched || _currentBaseContext)
                return;

            TryPlayPendingStinger(StingerKind.Discovery);
        }

        /// <summary>
        /// Plays a danger stinger over the current bed.
        /// </summary>
        public void PlayDangerStinger()
        {
            if (_overrideActive || _currentBaseContext)
                return;

            TryPlayPendingStinger(StingerKind.Danger);
        }

        /// <summary>
        /// Plays a recovery stinger over the current bed.
        /// </summary>
        public void PlayRecoveryStinger()
        {
            if (_overrideActive || _currentBaseContext)
                return;

            TryPlayPendingStinger(StingerKind.Recovery);
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

        /// <summary>
        /// Stops all active bed playback with an optional fade-out.
        /// </summary>
        public void StopMusic(float fadeOutSeconds = 0.75f)
        {
            StopMusicInternal(fadeOutSeconds);
        }

        private void TryRegisterTickHandlers()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredTick)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
                _registeredTick = true;
            }

            if (!_registeredSlowTick)
            {
                GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = true;
            }
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
        }

        private static bool TryInstantiateConfiguredRuntimeDirector()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            HectonMusicDirectorConfig sceneConfig;
            if (!HectonMusicDirectorAnchor.TryResolveConfigForScene(activeScene, out sceneConfig))
            {
                HectonMusicDirectorAnchor anchor = HectonMusicDirectorAnchor.ActiveRuntimeInstance;
                sceneConfig = anchor != null ? anchor.Config : null;
            }

            if (sceneConfig == null || sceneConfig.RuntimeDirectorPrefab == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[HectonMusicDirector] Missing authored RuntimeDirectorPrefab on active HectonMusicDirectorConfig.");
#endif
                return false;
            }

            ObjectPoolManager pool = Hecton8.Core.ObjectPoolManager.Instance;
            if (pool == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[HectonMusicDirector] ObjectPoolManager is unavailable. Runtime director spawn aborted.");
#endif
                return false;
            }

            GameObject runtimeDirectorPrefab = sceneConfig.RuntimeDirectorPrefab.gameObject;
            if (pool.GetAvailableCount(runtimeDirectorPrefab) <= 0)
                pool.Warmup(runtimeDirectorPrefab, 1);

            pool.Spawn(runtimeDirectorPrefab, Vector3.zero, Quaternion.identity);
            return _instance != null;
        }

        private void BindAuthoredVoicePool()
        {
            if (_musicSources == null)
                return;

            for (int i = 0; i < _musicSources.Length; i++)
                _musicSources[i] = null;

            _stingerSource = null;
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
            if (_voicePool != null)
                return;

            if (TryGetComponent(out _voicePool))
                return;

            _voicePool = ComponentReferenceUtility.ResolveOwnedComponent<MusicVoicePool>(transform);
        }

        private void ResolveDependencies()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            HectonMusicDirectorConfig sceneConfig;
            if (HectonMusicDirectorAnchor.TryResolveConfigForScene(activeScene, out sceneConfig))
            {
                ApplyConfig(sceneConfig);
            }
            else
            {
                HectonMusicDirectorAnchor anchor = HectonMusicDirectorAnchor.ActiveRuntimeInstance;
                if (anchor != null)
                    ApplyConfig(anchor.Config);
            }

            if (_worldZoneDirector == null)
                _worldZoneDirector = WorldZoneDirector.ActiveRuntimeInstance;

            if (_biomeMatrixDirector == null)
                _biomeMatrixDirector = BiomeMatrixDirector.ActiveRuntimeInstance;

            if (_depthZoneDirector == null)
                _depthZoneDirector = DepthZoneDirector.Instance;

            if (_directorAI == null)
                _directorAI = HectonDirectorAI.ActiveRuntimeInstance;

            if ((_playerTransform == null || _survivalSystem == null) &&
                SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null)
            {
                _playerTransform = playerTransform;

                if (_survivalSystem == null)
                    _playerTransform.TryGetComponent(out _survivalSystem);
            }

            AudioMixerGroup musicGroup = ResolveMusicMixerGroup();
            AudioMixerGroup stingerGroup = ResolveStingerMixerGroup();
            _layerMixer = musicGroup != null ? musicGroup.audioMixer : null;
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

            ApplyLayerMixerState(false);
        }

        private bool AreRuntimeVoicesReady()
        {
            if (_musicSources == null || _musicSources.Length < MusicVoiceCount || _stingerSource == null)
                return false;

            for (int i = 0; i < MusicVoiceCount; i++)
            {
                if (_musicSources[i] == null)
                    return false;
            }

            return true;
        }

        private void RefreshLayerThreatSnapshot()
        {
            ResolveDependencies();

            float depthMeters = ResolveLayerDepthMeters();
            _oxygenDanger01 = ResolveLayerOxygenDanger01();
            _stormPressure01 = ResolveStormPressure01(depthMeters);

            if (_playerTransform == null)
            {
                _predatorProximity01 = 0f;
                _debugPredatorProximity01 = 0f;
                _debugStormPressure01 = _stormPressure01;
                _debugOxygenDanger01 = _oxygenDanger01;
                return;
            }

            if (WorldSpatialHashGrid.TryGetNearestAggressiveBioform(
                _playerTransform.position,
                math.max(1f, _predatorSenseRadius),
                _PredatorThreatLayerMask,
                _playerTransform,
                out SpatialQueryHit predatorHit))
            {
                float distance = math.sqrt(predatorHit.DistanceSqr);
                _predatorProximity01 = 1f - math.saturate(distance / math.max(1f, _predatorSenseRadius));
            }
            else
            {
                _predatorProximity01 = 0f;
            }

            _debugPredatorProximity01 = _predatorProximity01;
            _debugStormPressure01 = _stormPressure01;
            _debugOxygenDanger01 = _oxygenDanger01;
        }

        private void UpdateLayerRouting(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            float depthMeters = ResolveLayerDepthMeters();
            float depth01 = InverseLerp(20f, 900f, depthMeters);
            float rhythmTarget = math.saturate(_resolvedTension01 * 0.65f + _predatorProximity01 * 0.55f + _stormPressure01 * 0.18f);
            float bassTarget = math.saturate(depth01 * 0.62f + _resolvedTension01 * 0.28f + _oxygenDanger01 * 0.26f + _stormPressure01 * 0.12f);
            float atmosphereTarget = math.saturate(0.24f + depth01 * 0.58f + _stormPressure01 * 0.16f - (_currentBaseContext ? 0.16f : 0f));
            float dangerTarget = math.saturate(math.max(math.max(_predatorProximity01, _oxygenDanger01), _resolvedTension01 * 0.82f) + _stormPressure01 * 0.18f);

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

        private float ResolveLayerDepthMeters()
        {
            if (_survivalSystem != null)
                return math.max(0f, _survivalSystem.Depth);

            if (_biomeMatrixDirector != null)
                return math.max(0f, _biomeMatrixDirector.CurrentDepthMeters);

            return 0f;
        }

        private float ResolveLayerOxygenDanger01()
        {
            if (_survivalSystem == null)
                return 0f;

            return InverseLerp(0.35f, 0.05f, _survivalSystem.OxygenNormalized);
        }

        private float ResolveStormPressure01(float depthMeters)
        {
            HectonSurfaceWeatherDirector weatherDirector = HectonSurfaceWeatherDirector.Instance;
            if (weatherDirector == null || depthMeters > 120f)
                return 0f;

            float depthAttenuation = 1f - math.saturate(depthMeters / 120f);
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
                return;

            SetMixerFloatIfChanged(_rhythmLayerParameter, _layerRhythm01, ref _lastRhythmDb, force);
            SetMixerFloatIfChanged(_bassLayerParameter, _layerBass01, ref _lastBassDb, force);
            SetMixerFloatIfChanged(_atmosphereLayerParameter, _layerAtmosphere01, ref _lastAtmosphereDb, force);
            SetMixerFloatIfChanged(_dangerLayerParameter, _layerDanger01, ref _lastDangerDb, force);
        }

        private void SetMixerFloatIfChanged(string parameterName, float normalizedValue, ref float cachedDb, bool force)
        {
            if (string.IsNullOrEmpty(parameterName))
                return;

            float db = math.lerp(MixerFloorDb, MixerCeilingDb, math.saturate(normalizedValue));
            if (!force && math.abs(db - cachedDb) < 0.1f)
                return;

            _layerMixer.SetFloat(parameterName, db);
            cachedDb = db;
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

            int depthTier = _biomeMatrixDirector != null ? _biomeMatrixDirector.CurrentDepthTier : 0;
            if (depthTier > 0)
            {
                if (depthTier <= 3)
                    return _shallowProfile != null ? _shallowProfile : _fallbackProfile;

                if (depthTier <= 9)
                    return _shelfProfile != null ? _shelfProfile : _fallbackProfile;

                return _abyssProfile != null ? _abyssProfile : _fallbackProfile;
            }

            return _fallbackProfile;
        }

        private float ResolveTension01()
        {
            if (_manualTensionOverride)
                return _manualTension01;

            HectonBiomeMatrixProfile matrixProfile = _biomeMatrixDirector != null ? _biomeMatrixDirector.CurrentProfile : null;
            WorldZoneAnchor currentZone = _worldZoneDirector != null ? _worldZoneDirector.CurrentZone : null;
            DepthZoneProfile depthZone = _depthZoneDirector != null ? _depthZoneDirector.CurrentZone : null;

            float aiTension01 = _directorAI != null
                ? math.saturate(_directorAI.TensionScore * 0.01f)
                : 0f;
            float biomePressure01 = ResolveBiomePressure01(matrixProfile);
            float zonePressure01 = ResolveZonePressure01(currentZone);
            float depthZonePressure01 = ResolveDepthZonePressure01(depthZone);
            float rewardUnease01 = ResolveRewardUnease01(matrixProfile);
            float safePocketSuppression01 = ResolveSafePocketSuppression01(matrixProfile, currentZone);
            float firstHourPressureBoost01 = ResolveFirstHourPressureBoost01(matrixProfile, currentZone);

            float tension01 =
                aiTension01 * _aiTensionWeight +
                biomePressure01 * _biomePressureWeight +
                zonePressure01 * _zonePressureWeight +
                depthZonePressure01 * _depthZonePressureWeight +
                rewardUnease01 * _rewardUneaseWeight +
                firstHourPressureBoost01 * _firstHourPressureBoostWeight -
                safePocketSuppression01 * _safePocketSuppressionWeight;

            if (ResolveBaseContext())
                tension01 *= _baseContextTensionScale;

            _debugAiTension01 = aiTension01;
            _debugBiomePressure01 = biomePressure01;
            _debugZonePressure01 = zonePressure01;
            _debugDepthZonePressure01 = depthZonePressure01;
            _debugRewardUnease01 = rewardUnease01;
            _debugSafePocketSuppression01 = safePocketSuppression01;
            _debugFirstHourPressureBoost01 = firstHourPressureBoost01;

            return math.saturate(tension01);
        }

        private bool ResolveBaseContext()
        {
            AcousticZoneController acoustic = AcousticZoneController.Instance;
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
            AudioSource source = _musicSources[voiceIndex];
            source.Stop();
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
                    float duration = _voiceFadeDurations[i] > 0f ? _voiceFadeDurations[i] : 0.01f;
                    float t = _voiceFadeElapsedTimes[i] / duration;
                    if (t > 1f)
                        t = 1f;

                    float startVolume = _voiceFadeStartVolumes[i];
                    float targetVolume = _voiceFadeTargetVolumes[i];
                    float fadeAngle = t * (math.PI * 0.5f);
                    if (targetVolume <= 0.0001f)
                    {
                        _voiceBaseVolumes[i] = startVolume * math.cos(fadeAngle);
                    }
                    else if (startVolume <= 0.0001f)
                    {
                        _voiceBaseVolumes[i] = targetVolume * math.sin(fadeAngle);
                    }
                    else
                    {
                        _voiceBaseVolumes[i] = math.sqrt(math.lerp(startVolume * startVolume, targetVolume * targetVolume, t));
                    }
                    if (t >= 1f)
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
                _waitTimerSeconds = UnityEngine.Random.Range(minPause, maxPause);

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
                UnityEngine.Random.value <= rootProfile.CrossTensionMixChance &&
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

            if (_biomeMatrixDirector == null || _depthBlendWindowMeters <= 0f || _depthBlendMaxWeight <= 0 || rootProfile == null)
                return;

            float depthMeters = _biomeMatrixDirector.CurrentDepthMeters;
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

            float normalized = 1f - (nearestBoundaryDistance / _depthBlendWindowMeters);
            if (normalized <= 0f)
                return;

            depthBlendProfile = candidate;
            depthBlendWeight = math.clamp((int)math.round(normalized * _depthBlendMaxWeight), 1, _depthBlendMaxWeight);
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

            int roll = UnityEngine.Random.Range(0, totalWeight);
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

            int roll = UnityEngine.Random.Range(0, totalWeight);

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

            return UnityEngine.Random.value <= profile.ShortTrackChance;
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

            TraceEvent("Stinger:" + kind, sourceProfile, selectedCue.Clip);

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
            int roll = UnityEngine.Random.Range(0, excludeRepeat ? totalWithoutRepeat : totalWeight);

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
            float t = _duckElapsed / _duckDuration;
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

            if (Hecton8.Core.GlobalRegistry.Audio is Hecton8.Core.IAudioService audioService)
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
            if (_hasLastAcousticInteriorState && _lastAcousticInteriorState == isInterior)
                return;

            _lastAcousticInteriorState = isInterior;
            _hasLastAcousticInteriorState = true;
            ReevaluateContext(true);
        }

        private void HandleMatrixBiomeChanged(HectonBiomeMatrixProfile profile)
        {
            ReevaluateContext(true);
        }

        private void HandleDepthTierChanged(int depthTier, float depthMeters)
        {
            ReevaluateContext(true);
        }

        private void HandleDepthZoneEntered(DepthZoneProfile zone)
        {
            ReevaluateContext(true);

            if (zone == null || _currentBaseContext)
                return;

            FirstHourDirector firstHourDirector = FirstHourDirector.Instance;
            if (firstHourDirector != null && !firstHourDirector.IsMilestoneComplete(FirstHourMilestone.Orientation))
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
            ReevaluateContext(true);

            if (zone == null || _currentBaseContext || !ShouldPlayDepthRecoveryStinger(zone))
                return;

            PlayRecoveryStinger();
        }

        private void HandleRareDiscoveryRequested(Vector3 position)
        {
            FirstHourDirector firstHourDirector = FirstHourDirector.Instance;
            if (firstHourDirector != null &&
                !firstHourDirector.IsMilestoneComplete(FirstHourMilestone.FirstCraft))
            {
                return;
            }

            PlayDiscoveryStinger();
        }

        private void HandleSpawnHordeRequested(Vector3 position)
        {
            _combatLatched = true;
            PlayDangerStinger();
            ReevaluateContext(true);
        }

        private void HandlePredatorPressureChanged(bool pressureEnabled)
        {
            if (!pressureEnabled && _combatLatched)
                PlayRecoveryStinger();

            ReevaluateContext(true);
        }

        private void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
        {
            RefreshSceneFlags(nextScene);
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

            FirstHourDirector firstHourDirector = FirstHourDirector.Instance;
            if (firstHourDirector != null &&
                !firstHourDirector.IsMilestoneComplete(FirstHourMilestone.FirstCraft))
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

            DepthZoneProfile currentZone = _depthZoneDirector != null ? _depthZoneDirector.CurrentZone : null;
            if (currentZone == null)
                return true;

            return ResolveDepthZonePressure01(currentZone) + 0.18f < ResolveDepthZonePressure01(exitedZone);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void TraceSelection(HectonMusicBiomeProfile rootProfile, HectonMusicBiomeProfile playbackProfile, HectonMusicClip selectedCue, bool highTension, bool preferShort)
        {
#if UNITY_EDITOR
            string rootLabel = rootProfile != null && !string.IsNullOrEmpty(rootProfile.ProfileLabel) ? rootProfile.ProfileLabel : "None";
            string playbackLabel = playbackProfile != null && !string.IsNullOrEmpty(playbackProfile.ProfileLabel) ? playbackProfile.ProfileLabel : "None";
            string cueId = !string.IsNullOrEmpty(selectedCue.CueId) ? selectedCue.CueId : (selectedCue.Clip != null ? selectedCue.Clip.name : "None");
            string tensionLabel = highTension ? "tense" : "calm";
            string formLabel = preferShort ? "short" : "long";
            string routeLabel = _selectionUsedDepthBlend ? "depth-blend" : (_selectionUsedCrossTension ? "cross-tension" : "local");
            _debugLastSelectionReason = rootLabel + " -> " + playbackLabel + " | " + tensionLabel + " | " + formLabel + " | " + routeLabel + " | " + cueId;
            TraceEvent("Select:" + routeLabel + ":" + tensionLabel + ":" + formLabel, playbackProfile, selectedCue.Clip);
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void TraceEvent(string eventLabel, HectonMusicBiomeProfile profile, AudioClip clip)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_enableTelemetry)
                return;

            string profileLabel = profile != null && !string.IsNullOrEmpty(profile.ProfileLabel) ? profile.ProfileLabel : "None";
            string clipLabel = clip != null ? clip.name : "None";
            Debug.Log("[MusicDirector] " + eventLabel + " | profile=" + profileLabel + " | clip=" + clipLabel + " | tension=" + _resolvedTension01.ToString("F2"));
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void WriteDebugState()
        {
#if UNITY_EDITOR
            _debugResolvedProfile = _resolvedProfile != null
                ? (!string.IsNullOrEmpty(_resolvedProfile.ProfileLabel) ? _resolvedProfile.ProfileLabel : _resolvedProfile.name)
                : (_fallbackProfile != null ? _fallbackProfile.ProfileLabel : "None");

            int debugVoiceIndex = _activeVoiceIndex;
            if (debugVoiceIndex >= 0 && debugVoiceIndex < MusicVoiceCount && _voiceActive[debugVoiceIndex] && !string.IsNullOrEmpty(_voiceClips[debugVoiceIndex].CueId))
                _debugActiveCueId = _voiceClips[debugVoiceIndex].CueId;
            else if (_overrideClip != null)
                _debugActiveCueId = _overrideClip.name;
            else if (_stingerSource != null && _stingerSource.isPlaying && _stingerSource.clip != null)
                _debugActiveCueId = _stingerSource.clip.name;
            else if (!HasAnyActiveVoice())
                _debugActiveCueId = "None";

            _debugTension01 = _resolvedTension01;
            _debugCombatLatched = _combatLatched;
            _debugTenseExplorationLatched = _tenseExplorationLatched;
            _debugWaitTimer = _waitTimerSeconds;
#endif
        }

        private static float ResolvePressure01(int authoredValue)
        {
            return math.saturate((authoredValue - 1f) / 4f);
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

        private static float ResolveFirstHourPressureBoost01(HectonBiomeMatrixProfile matrixProfile, WorldZoneAnchor currentZone)
        {
            FirstHourDirector firstHourDirector = FirstHourDirector.Instance;
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

            if (!firstHourDirector.IsMilestoneComplete(FirstHourMilestone.FirstCraft))
                boost01 = math.max(boost01, 0.18f);

            if (!firstHourDirector.IsMilestoneComplete(FirstHourMilestone.FirstModule))
                boost01 = math.max(boost01, 0.32f);

            return math.saturate(boost01);
        }

        private static float InverseLerp(float a, float b, float value)
        {
            float denominator = b - a;
            if (math.abs(denominator) <= 0.000001f)
                return 0f;

            return math.saturate((value - a) / denominator);
        }

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            float delta = target - current;
            float absDelta = math.abs(delta);
            if (absDelta <= maxDelta || absDelta <= 0.000001f)
                return target;

            return current + math.sign(delta) * maxDelta;
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
