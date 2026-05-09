using Hecton8.Bootstrap;
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
    public sealed class DeepPsychosisController : MonoBehaviour, ITickable, IUpdatable, ISlowTickable
    {
        private const int DependencyRetryFrameInterval = 30;

        [Header("── Clip Pools ──────────────────")]
        [Tooltip("3D whisper cues emitted around the player during deep psychosis windows.")]
        [SerializeField] private AudioClip[] whisperClips;

        [Tooltip("3D hull groans and metallic scrapes emitted around the player during deep psychosis windows.")]
        [SerializeField] private AudioClip[] hullStressClips;

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
        private HectonSurvivalSystem _survivalSystem;
        private Transform _playerTransform;
        private Transform _dependencyPlayerTransform;
        private HectonPlayerMovement _playerMovement;
        private bool _movementLookupAttempted;
        private bool _survivalLookupAttempted;
        private int _nextDependencyRetryFrame;
        private float _psychosisIntensity01;
        private float _cueTimerSeconds;
        private uint _psychosisRandomState;

        private void Awake()
        {
            _playerTransform = transform;
            TryResolveDependencies();
            _cueTimerSeconds = cueIntervalMaxSeconds;
            _psychosisRandomState = unchecked(((uint)EntityId.ToULong(GetEntityId()) * 747796405u) ^ 0x9E3779B9u);
            if (_psychosisRandomState == 0u)
                _psychosisRandomState = 0xA341316Cu;
        }

        private void OnEnable()
        {
            TryRegisterTickHandlers();
        }

        private void OnDisable()
        {
            TryUnregisterTickHandlers();
            _cueTimerSeconds = cueIntervalMaxSeconds;
            _psychosisIntensity01 = 0f;
            _debugPsychosisIntensity01 = 0f;
        }

        private void OnDestroy()
        {
            TryUnregisterTickHandlers();
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

            PlayPsychosisCue();
            _cueTimerSeconds = math.lerp(cueIntervalMaxSeconds, cueIntervalMinSeconds, _psychosisIntensity01);
        }

        /// <summary>
        /// Refreshes deep-stress inputs at slow-tick cadence.
        /// </summary>
        public void SlowTick()
        {
            TryResolveDependencies();

            float depthMeters = _survivalSystem != null ? math.max(0f, _survivalSystem.Depth) : 0f;
            float oxygenNormalized = _survivalSystem != null ? math.saturate(_survivalSystem.OxygenNormalized) : 1f;
            float deepPressure01 = math.saturate(math.unlerp(depthThreshold, depthThreshold + 350f, depthMeters));
            float oxygenDanger01 = oxygenNormalized <= oxygenThreshold
                ? math.saturate(math.unlerp(oxygenThreshold, 0.05f, oxygenNormalized))
                : 0f;

            EnvironmentalStrainManager strainManager = GlobalRegistry.EnvironmentalStrain;
            float pollutionLoad = strainManager != null
                ? math.max(0f, strainManager.MicroplasticStrain + strainManager.GeneralPollution)
                : 0f;
            float pollutionPressure01 = pollutionLoad <= pollutionThreshold
                ? 0f
                : math.saturate((pollutionLoad - pollutionThreshold) / math.max(1f, pollutionThreshold));

            float depthStress01 = deepPressure01 * oxygenDanger01;
            _psychosisIntensity01 = math.saturate(math.max(depthStress01, pollutionPressure01 * 0.65f));

            _debugDepthMeters = depthMeters;
            _debugOxygenNormalized = oxygenNormalized;
            _debugPollution01 = pollutionPressure01;
            _debugPsychosisIntensity01 = _psychosisIntensity01;
        }

        private void TryResolveDependencies()
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            Transform resolvedPlayerTransform = playerContext != null && playerContext.PlayerTransform != null
                ? playerContext.PlayerTransform
                : _playerTransform;

            if (resolvedPlayerTransform == null &&
                SceneBootstrap.TryGetCurrentPlayerTransform(out Transform bootstrapPlayerTransform))
            {
                resolvedPlayerTransform = bootstrapPlayerTransform;
            }

            if (!ReferenceEquals(_dependencyPlayerTransform, resolvedPlayerTransform))
            {
                _dependencyPlayerTransform = resolvedPlayerTransform;
                _playerTransform = resolvedPlayerTransform;
                _playerMovement = playerContext != null &&
                                  ReferenceEquals(playerContext.PlayerTransform, resolvedPlayerTransform)
                    ? playerContext.PlayerMovement
                    : null;
                _survivalSystem = null;
                _movementLookupAttempted = _playerMovement != null;
                _survivalLookupAttempted = false;
                _nextDependencyRetryFrame = 0;
            }

            if ((_playerMovement == null && _movementLookupAttempted) ||
                (_survivalSystem == null && _survivalLookupAttempted))
            {
                if (Time.frameCount >= _nextDependencyRetryFrame)
                {
                    _movementLookupAttempted = _playerMovement != null;
                    _survivalLookupAttempted = _survivalSystem != null;
                }
            }

            if (_playerMovement == null &&
                playerContext != null &&
                ReferenceEquals(playerContext.PlayerTransform, resolvedPlayerTransform) &&
                playerContext.PlayerMovement != null)
            {
                _playerMovement = playerContext.PlayerMovement;
                _movementLookupAttempted = true;
            }

            if (_playerMovement == null && !_movementLookupAttempted && _playerTransform != null)
            {
                _movementLookupAttempted = true;
                _playerTransform.TryGetComponent(out _playerMovement);
                if (_playerMovement == null)
                    _nextDependencyRetryFrame = Time.frameCount + DependencyRetryFrameInterval;
            }

            if (_survivalSystem == null && !_survivalLookupAttempted)
            {
                _survivalLookupAttempted = true;
                if (_playerTransform != null)
                    _playerTransform.TryGetComponent(out _survivalSystem);

                if (_survivalSystem == null)
                    TryGetComponent(out _survivalSystem);

                if (_survivalSystem == null)
                    _nextDependencyRetryFrame = Time.frameCount + DependencyRetryFrameInterval;
            }
        }

        private void TryRegisterTickHandlers()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredTick)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Player);
                _registeredTick = GlobalRegistry.Updatables.Contains(this);
            }

            if (!_registeredSlowTick)
            {
                GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Player);
                _registeredSlowTick = GlobalRegistry.SlowTickables.Contains(this);
            }
        }

        private void TryUnregisterTickHandlers()
        {
            if (_registeredTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
                _registeredTick = false;
            }

            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Player);
                _registeredSlowTick = false;
            }
        }

        private void PlayPsychosisCue()
        {
            Hecton8.Core.IAudioService audioManager = Hecton8.Core.GlobalRegistry.Audio;
            if (_playerTransform == null || audioManager == null)
                return;

            AudioClip clip = SelectCueClip();
            if (clip == null)
            {
                GlobalRegistry.AcousticZone?.PlayMadnessWhisperCue();
                return;
            }

            if (!TryResolvePlayerAupRuntimePosition(out Vector3 origin))
                return;

            float radius = math.lerp(cueRadiusMin, cueRadiusMax, _psychosisIntensity01);
            float3 offset = new float3(
                NextRandomRange(-1f, 1f),
                NextRandomRange(-0.35f, 0.35f),
                NextRandomRange(-1f, 1f));

            float offsetLengthSq = math.lengthsq(offset);
            if (offsetLengthSq < 0.01f)
            {
                offset = new float3(0f, 0f, 1f);
            }
            else
            {
                offset *= math.rsqrt(offsetLengthSq);
            }

            Vector3 cuePosition = origin + new Vector3(offset.x, offset.y, offset.z) * radius;
            float volume = math.lerp(cueVolumeMin, cueVolumeMax, _psychosisIntensity01);
            float pitch = NextRandomRange(0.88f, 1.08f);
            audioManager.PlayAtPoint(clip, cuePosition, volume, pitch, audioManager.AmbientGroup);

            if (_psychosisIntensity01 >= 0.55f && NextRandom01() <= helmetWhisperChance)
                GlobalRegistry.AcousticZone?.PlayMadnessWhisperCue();
        }

        private bool TryResolvePlayerAupRuntimePosition(out Vector3 runtimePosition)
        {
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

            float3 runtime = movement.CurrentAup.ToRuntimeFloat3();
            runtimePosition = new Vector3(runtime.x, runtime.y, runtime.z);
            return true;
        }

        private AudioClip SelectCueClip()
        {
            AudioClip[] primaryPool = _psychosisIntensity01 >= 0.6f ? whisperClips : hullStressClips;
            AudioClip clip = SelectRandomClip(primaryPool);
            if (clip != null)
                return clip;

            AudioClip[] fallbackPool = ReferenceEquals(primaryPool, whisperClips) ? hullStressClips : whisperClips;
            return SelectRandomClip(fallbackPool);
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
            return (NextRandomUInt() & 0x00FFFFFFu) * (1f / 16777215f);
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


