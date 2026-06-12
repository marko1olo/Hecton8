using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Audio
{
    /// <summary>
    /// Drives low-frequency hallucination cues when the player is deep, oxygen-starved, or psychologically overloaded by pollution pressure.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeepPsychosisController : MonoBehaviour, ITickable, IUpdatable, ISlowTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener, IGlobalRegistryHotSwapRefListener
    {
        private const int DependencyRetryFrameInterval = 30;
        private const float DiagonalCueAxis = 0.70710678f;
        private const float Random24ToUnit = 0.000000059604648f;
        private const double AupRuntimeFloatClampMeters = 3.4028234663852886E+38d;

        [Header("── Clip Pools ──────────────────")]
        [Tooltip("3D whisper cues emitted around the player during deep psychosis windows.")]
        [SerializeField] private AudioClip[] whisperClips;

        [Header("── Thresholds ──────────────────")]
        [Tooltip("Depth threshold where the deep psychosis layer starts evaluating oxygen stress.")]
        [SerializeField, Min(0f)] private float depthThreshold = 500f;

        [Tooltip("Oxygen threshold below which deep psychosis cues become eligible.")]
        [SerializeField, Range(0.01f, 1f)] private float oxygenThreshold = 0.30f;

        [Tooltip("Combined pollution threshold above which psychosis can trigger even when oxygen is not yet critical.")]
        [SerializeField, Min(0f)] private float pollutionThreshold = 140f;

        [Header("── Playback ──────────────────")]
        [Tooltip("Shortest cadence between psychosis cues at peak intensity.")]
        [SerializeField, Min(0.1f)] private float cueIntervalMinSeconds = 5.5f;

        [Tooltip("Longest cadence between psychosis cues when the effect just barely activates.")]
        [SerializeField, Min(0.1f)] private float cueIntervalMaxSeconds = 16f;

        [Tooltip("Minimum radial distance for 3D psychosis cues around the player.")]
        [SerializeField, Min(0.1f)] private float cueRadiusMin = 7f;

        [Tooltip("Maximum radial distance for 3D psychosis cues around the player.")]
        [SerializeField, Min(0.1f)] private float cueRadiusMax = 18f;

        [Tooltip("Base volume range for hallucination cues.")]
        [SerializeField, Range(0f, 1f)] private float cueVolumeMin = 0.18f;

        [Tooltip("Peak volume range for hallucination cues.")]
        [SerializeField, Range(0f, 1f)] private float cueVolumeMax = 0.42f;

        [Tooltip("Chance to layer a helmet whisper cue through the existing acoustic controller.")]
        [SerializeField, Range(0f, 1f)] private float helmetWhisperChance = 0.34f;

        [Header("── Diagnostics ──────────────────")]
        [SerializeField] private float _debugPsychosisIntensity01;
        [SerializeField] private float _debugDepthMeters;
        [SerializeField] private float _debugOxygenNormalized = 1f;
        [SerializeField] private float _debugPollution01;

        private bool _registeredTick;
        private bool _registeredSlowTick;
        private bool _registeredLateFrame;
        private bool _hotSwapRegistered;
        private HectonSurvivalSystem _survivalSystem;
        private Transform _playerTransform;
        private Transform _dependencyPlayerTransform;
        private HectonPlayerMovement _playerMovement;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private IEnvironmentalStrainReadModel _environmentalStrainReadModel;
        private IAudioService _audioService;
        private IAcousticZoneMadnessCueSink _acousticZone;
        private int _nextPlayerContextRetryFrame;
        private int _nextEnvironmentalStrainRetryFrame;
        private int _nextAudioServiceRetryFrame;
        private int _nextAcousticZoneRetryFrame;
        private float _psychosisIntensity01;
        private float _cueTimerSeconds;
        private uint _psychosisRandomState;
        private bool _pendingPsychosisCue;

        private void Awake()
        {
            _playerTransform = transform;
            RefreshCachedRuntimeServicesCold();
            TryResolveDependencies();
            _cueTimerSeconds = cueIntervalMaxSeconds;
            _psychosisRandomState = unchecked(((uint)EntityId.ToULong(GetEntityId()) * 747796405u) ^ 0x9E3779B9u);
            if (_psychosisRandomState == 0u)
                _psychosisRandomState = 0xA341316Cu;
        }

        private void OnEnable()
        {
            RefreshCachedRuntimeServicesCold();
            TryRegisterHotSwapListener();
            TryResolveDependencies();
            TryRegisterTickHandlers();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            TryUnregisterTickHandlers();
            _cueTimerSeconds = cueIntervalMaxSeconds;
            _psychosisIntensity01 = 0f;
            _pendingPsychosisCue = false;
            _debugPsychosisIntensity01 = 0f;
            ClearCachedRuntimeServices();
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            TryUnregisterTickHandlers();
            ClearCachedRuntimeServices();
        }

        public void OnGlobalRegistryServiceRebound(
            GlobalRegistryServiceSlot serviceSlot,
            ref object currentService)
        {
            CacheReboundRuntimeService(serviceSlot, currentService);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            CacheReboundRuntimeService(serviceSlot, currentService);
        }

        /// <summary>
        /// Advances playback cadence for stress cues without allocating.
        /// </summary>
        /// <param name="deltaTime">Tick delta supplied by <see cref="GameTickManager"/>.</param>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            if (_psychosisIntensity01 <= 0.001f)
            {
                _cueTimerSeconds = cueIntervalMaxSeconds;
                return;
            }

            _cueTimerSeconds -= deltaTime;
            if (_cueTimerSeconds > 0f)
                return;

            _pendingPsychosisCue = true;
            _cueTimerSeconds = math.lerp(cueIntervalMaxSeconds, cueIntervalMinSeconds, _psychosisIntensity01);
        }

        public void LateFrameTick()
        {
            if (!_pendingPsychosisCue)
                return;

            _pendingPsychosisCue = false;
            PlayPsychosisCue();
        }

        /// <summary>
        /// Refreshes deep-stress inputs at slow-tick cadence.
        /// </summary>
        public void SlowTick()
        {
            TryResolveDependencies();

            float depthMeters = ResolvePlayerDepthMeters();
            float oxygenNormalized = _survivalSystem != null ? math.saturate(_survivalSystem.OxygenNormalized) : 1f;
            float deepPressure01 = math.saturate(math.unlerp(depthThreshold, depthThreshold + 350f, depthMeters));
            float oxygenDanger01 = oxygenNormalized <= oxygenThreshold
                ? math.saturate(math.unlerp(oxygenThreshold, 0.05f, oxygenNormalized))
                : 0f;

            IEnvironmentalStrainReadModel strainReadModel = ResolveEnvironmentalStrainReadModel();
            float pollutionLoad = strainReadModel != null
                ? math.max(0f, strainReadModel.MicroplasticStrain + strainReadModel.GeneralPollution)
                : 0f;
            float pollutionPressure01 = pollutionLoad <= pollutionThreshold
                ? 0f
                : math.saturate((pollutionLoad - pollutionThreshold) * math.rcp(math.max(1f, pollutionThreshold)));

            float depthStress01 = deepPressure01 * oxygenDanger01;
            _psychosisIntensity01 = math.saturate(math.max(depthStress01, pollutionPressure01 * 0.65f));

            if (_psychosisIntensity01 > 0.001f)
            {
                TryRegisterUpdateHandlers();
            }
            else
            {
                TryUnregisterUpdateHandlers();
            }

            _debugDepthMeters = depthMeters;
            _debugOxygenNormalized = oxygenNormalized;
            _debugPollution01 = pollutionPressure01;
            _debugPsychosisIntensity01 = _psychosisIntensity01;
        }

        private void TryResolveDependencies()
        {
            IPlayerRuntimeContext playerContext = ResolvePlayerRuntimeContext();
            Transform resolvedPlayerTransform = playerContext != null && playerContext.PlayerTransform != null
                ? playerContext.PlayerTransform
                : _playerTransform;

            if (!ReferenceEquals(_dependencyPlayerTransform, resolvedPlayerTransform))
            {
                _dependencyPlayerTransform = resolvedPlayerTransform;
                _playerTransform = resolvedPlayerTransform;
            }

            bool contextMatchesPlayer = playerContext != null &&
                                        playerContext.PlayerTransform != null &&
                                        ReferenceEquals(playerContext.PlayerTransform, resolvedPlayerTransform);
            _playerMovement = contextMatchesPlayer ? playerContext.PlayerMovement : null;
            _survivalSystem = contextMatchesPlayer ? playerContext.SurvivalSystem : null;
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

            HectonPlayerMovement movement = _playerMovement;
            if (movement != null && math.isfinite(movement.CurrentDepth))
                return math.max(0f, movement.CurrentDepth);

            HectonSurvivalSystem survival = _survivalSystem;
            if (survival != null && math.isfinite(survival.Depth))
                return math.max(0f, survival.Depth);

            return 0f;
        }

        private void TryRegisterTickHandlers()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredSlowTick)
            {
                _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Player);
            }
        }

        private void TryUnregisterTickHandlers()
        {
            TryUnregisterUpdateHandlers();

            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Player);
                _registeredSlowTick = false;
            }
        }

        private void TryRegisterUpdateHandlers()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredTick)
            {
                _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);
            }

            if (!_registeredLateFrame)
            {
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
            }
        }

        private void TryUnregisterUpdateHandlers()
        {
            if (_registeredTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
                _registeredTick = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
                _registeredLateFrame = false;
            }
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

        private void ClearCachedRuntimeServices()
        {
            _playerRuntimeContext = null;
            _environmentalStrainReadModel = null;
            _audioService = null;
            _acousticZone = null;
            _nextPlayerContextRetryFrame = 0;
            _nextEnvironmentalStrainRetryFrame = 0;
            _nextAudioServiceRetryFrame = 0;
            _nextAcousticZoneRetryFrame = 0;
        }

        private void RefreshCachedRuntimeServicesCold()
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            CachePlayerRuntimeContext(GlobalRegistry.Player, frame);
            CacheEnvironmentalStrainReadModel(GlobalRegistry.EnvironmentalStrainReadModel, frame);
            CacheAudioService(GlobalRegistry.Audio, frame);
            CacheAcousticZone(GlobalRegistry.AcousticZoneMadnessCueSink, frame);
        }

        private void CacheReboundRuntimeService(GlobalRegistryServiceSlot serviceSlot, object currentService)
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Player:
                    CachePlayerRuntimeContext(currentService as IPlayerRuntimeContext, frame);
                    _dependencyPlayerTransform = null;
                    _playerMovement = null;
                    _survivalSystem = null;
                    break;
                case GlobalRegistryServiceSlot.EnvironmentalStrainRuntime:
                    CacheEnvironmentalStrainReadModel(currentService as IEnvironmentalStrainReadModel, frame);
                    break;
                case GlobalRegistryServiceSlot.Audio:
                    CacheAudioService(currentService as IAudioService, frame);
                    break;
                case GlobalRegistryServiceSlot.AcousticZoneRuntime:
                    CacheAcousticZone(currentService as IAcousticZoneMadnessCueSink, frame);
                    break;
            }
        }

        private IPlayerRuntimeContext ResolvePlayerRuntimeContext()
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            return playerContext != null && playerContext.IsInitialized ? playerContext : null;
        }

        private IEnvironmentalStrainReadModel ResolveEnvironmentalStrainReadModel()
        {
            return _environmentalStrainReadModel;
        }

        private IAudioService ResolveAudioService()
        {
            IAudioService audioService = _audioService;
            if (IsAudioServiceUsable(audioService))
                return audioService;

            _audioService = null;
            return null;
        }

        private IAcousticZoneMadnessCueSink ResolveAcousticZone()
        {
            return _acousticZone;
        }

        private void CachePlayerRuntimeContext(IPlayerRuntimeContext playerContext, int frame)
        {
            _playerRuntimeContext = playerContext != null && playerContext.IsInitialized ? playerContext : null;
            _nextPlayerContextRetryFrame = frame + DependencyRetryFrameInterval;
        }

        private void CacheEnvironmentalStrainReadModel(IEnvironmentalStrainReadModel strainReadModel, int frame)
        {
            _environmentalStrainReadModel = strainReadModel;
            _nextEnvironmentalStrainRetryFrame = frame + DependencyRetryFrameInterval;
        }

        private void CacheAudioService(IAudioService audioService, int frame)
        {
            _audioService = IsAudioServiceUsable(audioService) ? audioService : null;
            _nextAudioServiceRetryFrame = frame + DependencyRetryFrameInterval;
        }

        private static bool IsAudioServiceUsable(IAudioService audioService)
        {
            if (audioService == null || !audioService.IsAudioRuntimeReady)
                return false;

            if (audioService is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private void CacheAcousticZone(IAcousticZoneMadnessCueSink acousticZone, int frame)
        {
            _acousticZone = acousticZone;
            _nextAcousticZoneRetryFrame = frame + DependencyRetryFrameInterval;
        }

        private void PlayHelmetWhisperCue()
        {
            IAcousticZoneMadnessCueSink acousticZone = ResolveAcousticZone();
            if (acousticZone != null)
                acousticZone.PlayMadnessWhisperCue();
        }

        private void PlayPsychosisCue()
        {
            if (_playerTransform == null || !TryResolvePlayerAupRuntimePosition(out Vector3 origin))
                return;

            float radius = math.lerp(cueRadiusMin, cueRadiusMax, _psychosisIntensity01);
            float3 offset = ResolveCinematicCueOffset(
                NextRandomRange(-1f, 1f),
                NextRandomRange(-0.35f, 0.35f),
                NextRandomRange(-1f, 1f));

            Vector3 cuePosition = origin + new Vector3(offset.x, offset.y, offset.z) * radius;
            float volume = math.lerp(cueVolumeMin, cueVolumeMax, _psychosisIntensity01);
            float pitch = NextRandomRange(0.88f, 1.08f);
            if (_psychosisIntensity01 >= 0.6f)
            {
                AudioClip clip = SelectRandomClip(whisperClips);
                IAudioService audioManager = ResolveAudioService();
                if (clip != null && audioManager != null)
                    audioManager.PlayAtPoint(clip, cuePosition, volume, pitch, audioManager.AmbientGroup);
                else
                    PlayHelmetWhisperCue();
            }
            else
            {
                float stress01 = math.saturate(math.lerp(0.30f, 0.78f, _psychosisIntensity01));
                float stressPitch = math.lerp(0.74f, 0.52f, stress01) * math.clamp(pitch, 0.85f, 1.12f);
                ProceduralAudioEvents.TryRaiseStructuralStressTriggered(cuePosition, stress01, stressPitch);
            }

            if (_psychosisIntensity01 >= 0.55f && NextRandom01() <= helmetWhisperChance)
                PlayHelmetWhisperCue();
        }

        private bool TryResolvePlayerAupRuntimePosition(out Vector3 runtimePosition)
        {
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

                runtimePosition = default;
                return false;
            }

            HectonPlayerMovement movement = _playerMovement;
            if (movement == null)
            {
                TryResolveDependencies();
                movement = _playerMovement;
            }

            if (movement == null)
            {
                runtimePosition = default;
                return false;
            }

            AbsoluteUniversePosition movementAup = movement.CurrentAup;
            if (!TryResolveRuntimeOriginRelativeFloat3(in movementAup, out float3 runtime))
            {
                runtimePosition = default;
                return false;
            }

            runtimePosition = new Vector3(runtime.x, runtime.y, runtime.z);
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

        private static float3 ResolveCinematicCueOffset(float x, float y, float z)
        {
            float ax = math.abs(x);
            float az = math.abs(z);
            if (ax < 0.01f && az < 0.01f)
                return new float3(0f, y, 1f);

            float sx = x < 0f ? -1f : 1f;
            float sz = z < 0f ? -1f : 1f;
            if (ax > az * 2f)
                return new float3(sx, y, 0f);

            if (az > ax * 2f)
                return new float3(0f, y, sz);

            return new float3(sx * DiagonalCueAxis, y, sz * DiagonalCueAxis);
        }

        private AudioClip SelectRandomClip(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0)
                return null;

            int clipCount = clips.Length;
            int startIndex = NextRandomRangeInt(0, clipCount);
            for (int i = 0; i < clipCount; i++)
            {
                AudioClip clip = clips[(startIndex + i) % clipCount];
                if (clip != null)
                    return clip;
            }

            return null;
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
            uint state = _psychosisRandomState != 0u ? _psychosisRandomState : 0xA341316Cu;
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            _psychosisRandomState = state;
            return state;
        }
    }
}


