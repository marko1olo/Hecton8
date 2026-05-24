// ============================================================================
// HECTON-8 - PlayerThrusterAudio.cs
// Dynamic servo / thruster loop for swim locomotion and powered transport.
// ============================================================================

using Hecton8.Core;
using Hecton8.Gameplay;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Audio
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class PlayerThrusterAudio : MonoBehaviour, ITickable, IGlobalRegistryHotSwapListener, IGlobalRegistryHotSwapRefListener
    {
        [Header("References")]
        [SerializeField] private HectonPlayerMovement playerMovement;
        [SerializeField] private PlayerToolManager playerToolManager;
        [SerializeField] private PlayerTransportCoordinator playerTransportCoordinator;

        [Header("Volume")]
        [Tooltip("Volume when completely idle.")]
        [SerializeField, Range(0f, 0.5f)] private float idleVolume = 0.05f;

        [Tooltip("Volume at maximum swim speed.")]
        [SerializeField, Range(0f, 1f)] private float maxVolume = 0.6f;

        [Tooltip("How quickly volume responds to speed changes.")]
        [SerializeField, Range(1f, 20f)] private float volumeResponseSpeed = 5f;

        [Header("Pitch")]
        [Tooltip("Base pitch when idle.")]
        [SerializeField, Range(0.3f, 1.5f)] private float idlePitch = 0.7f;

        [Tooltip("Pitch at maximum swim speed.")]
        [SerializeField, Range(0.5f, 3f)] private float maxPitch = 1.4f;

        [Tooltip("How quickly pitch responds to speed changes.")]
        [SerializeField, Range(1f, 20f)] private float pitchResponseSpeed = 4f;

        [Header("Mode Transition")]
        [Tooltip("How quickly thruster audio fades in or out as locomotion changes.")]
        [SerializeField, Range(1f, 15f)] private float modeFadeSpeed = 4f;

        [Header("Surface Swim Mix")]
        [Tooltip("How strong the thruster loop remains while surface swimming.")]
        [SerializeField, Range(0f, 1f)] private float surfaceSwimModeBlend = 0.58f;

        [Tooltip("Volume multiplier applied while surface swimming.")]
        [SerializeField, Range(0.1f, 1f)] private float surfaceSwimVolumeMultiplier = 0.72f;

        [Tooltip("Pitch multiplier applied while surface swimming.")]
        [SerializeField, Range(0.5f, 1.2f)] private float surfaceSwimPitchMultiplier = 0.9f;

        [Header("Transport Feel")]
        [Tooltip("Reference propulsion force treated as full Manta drive for audio shaping.")]
        [SerializeField, Range(50f, 2000f)] private float mantaPropulsionReference = 800f;

        [Tooltip("Minimum speed-floor kept alive while Manta propulsion is active.")]
        [SerializeField, Range(0f, 1f)] private float mantaIdleSpeedFloor = 0.42f;

        [Tooltip("Extra volume added by active Manta propulsion.")]
        [SerializeField, Range(0f, 0.6f)] private float mantaVolumeBoost = 0.18f;

        [Tooltip("Extra pitch added by active Manta propulsion.")]
        [SerializeField, Range(0f, 0.8f)] private float mantaPitchBoost = 0.22f;

        [Tooltip("Minimum swim-mode blend kept alive while Manta propulsion is active.")]
        [SerializeField, Range(0f, 1f)] private float mantaModeBlendFloor = 0.35f;

        [Header("Load / Dive Feel")]
        [Tooltip("How much heavy cargo load increases motor strain volume.")]
        [SerializeField, Range(0f, 0.4f)] private float heavyCarryVolumeBoost = 0.12f;

        [Tooltip("How much heavy cargo load drags motor pitch downward.")]
        [SerializeField, Range(0f, 0.4f)] private float heavyCarryPitchDrag = 0.14f;

        [Tooltip("Extra volume added during aggressive downward swim entry.")]
        [SerializeField, Range(0f, 0.4f)] private float diveVolumeBoost = 0.06f;

        [Tooltip("Extra pitch added during aggressive downward swim entry.")]
        [SerializeField, Range(0f, 0.4f)] private float divePitchBoost = 0.08f;

        [Tooltip("Downward velocity treated as full dive-attack intensity.")]
        [SerializeField, Range(0.1f, 6f)] private float diveVelocityReference = 2.4f;

        private const int ProceduralThrusterSampleRate = 22050;
        private const int ProceduralThrusterFrameCount = 1024;
        private const float ProceduralBaseFrequencyHertz = 42f;
        private const float ProceduralWhineFrequencyHertz = 137f;
        private const float ProceduralThrusterSampleRateInv = 0.000045351474f;
        private const float ProceduralTwoPi = 6.28318530718f;

        private AudioSource _audioSource;
        private Rigidbody _playerRb;
        private AudioClip _proceduralThrusterClip;
        private float _currentVolume;
        private float _currentPitch;
        private float _modeBlend;
        private float _proceduralPhase;
        private float _proceduralWhinePhase;
        private float _proceduralNoiseLowPass;
        private uint _proceduralNoiseState = 0xA341316Cu;
        private bool _registered;
        private bool _hotSwapRegistered;
        private bool _transportCoordinatorLookupAttempted;
        private PlayerTransportFeelContract _transportFeelContractCurrent;
        private SpatialAudioManager _cachedSpatialAudioManager;

        private void Awake()
        {
            TryGetComponent(out _audioSource);
            if (_audioSource == null)
            {
                enabled = false;
                return;
            }

            EnsureProceduralThrusterClip();
            _audioSource.clip = _proceduralThrusterClip;
            _audioSource.loop = true;
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 1f;
            _audioSource.volume = 0f;
            _audioSource.pitch = idlePitch;
            RefreshRuntimeAudioServicesCold();
            TryAssignMixerRoute();

            if (playerMovement != null)
            {
                playerMovement.TryGetComponent(out _playerRb);
                if (playerToolManager == null)
                    playerMovement.TryGetComponent(out playerToolManager);
                if (playerTransportCoordinator == null)
                    playerMovement.TryGetComponent(out playerTransportCoordinator);
            }

            TryCacheTransportCoordinatorOnce();

            _currentVolume = 0f;
            _currentPitch = idlePitch;
            _modeBlend = 0f;
        }

        private void OnEnable()
        {
            if (playerTransportCoordinator == null)
                _transportCoordinatorLookupAttempted = false;
            TryCacheTransportCoordinatorOnce();
            RefreshRuntimeAudioServicesCold();
            TryRegisterHotSwapListener();
            TryAssignMixerRoute();

            if (PlayerCriticalProceduralAudioRenderer.IsRuntimeInstalled)
            {
                if (_audioSource != null && _audioSource.isPlaying)
                    _audioSource.Stop();

                enabled = false;
                return;
            }

            TryRegister();

            EnsureProceduralThrusterClip();
            if (_proceduralThrusterClip != null && _audioSource != null)
            {
                if (_audioSource.clip != _proceduralThrusterClip)
                    _audioSource.clip = _proceduralThrusterClip;

                _audioSource.Play();
            }
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            TryUnregister();

            if (_audioSource != null && _audioSource.isPlaying)
                _audioSource.Stop();
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            TryUnregister();
            if (_proceduralThrusterClip != null)
            {
                Destroy(_proceduralThrusterClip);
                _proceduralThrusterClip = null;
            }
        }

        public void OnGlobalRegistryServiceRebound(
            GlobalRegistryServiceSlot serviceSlot,
            ref object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Audio)
                return;

            CacheSpatialAudioManager(currentService as IAudioService);
            TryAssignMixerRoute(force: true);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Audio)
                return;

            CacheSpatialAudioManager(currentService as IAudioService);
            TryAssignMixerRoute(force: true);
        }

        public void Tick(float deltaTime)
        {
            if (playerMovement == null || _playerRb == null || _audioSource == null)
                return;

            if (_proceduralThrusterClip == null)
                EnsureProceduralThrusterClip();

            if (_proceduralThrusterClip == null)
                return;

            float dt = deltaTime;
            if (dt <= 0f)
                return;

            PlayerLocomotionMode locomotionMode = playerMovement.CurrentLocomotionMode;
            bool isSwimMode = locomotionMode == PlayerLocomotionMode.SurfaceSwim ||
                              locomotionMode == PlayerLocomotionMode.UnderwaterSwim;
            float targetModeBlend;
            float modeVolumeMultiplier;
            float modePitchMultiplier;

            switch (locomotionMode)
            {
                case PlayerLocomotionMode.SurfaceSwim:
                    targetModeBlend = surfaceSwimModeBlend;
                    modeVolumeMultiplier = surfaceSwimVolumeMultiplier;
                    modePitchMultiplier = surfaceSwimPitchMultiplier;
                    break;

                case PlayerLocomotionMode.UnderwaterSwim:
                    targetModeBlend = 1f;
                    modeVolumeMultiplier = 1f;
                    modePitchMultiplier = 1f;
                    break;

                default:
                    targetModeBlend = 0f;
                    modeVolumeMultiplier = 1f;
                    modePitchMultiplier = 1f;
                    break;
            }

            _transportFeelContractCurrent = isSwimMode ? ResolveTransportFeelContract() : null;
            float transportBoost01 = isSwimMode ? ResolveTransportBoost01() : 0f;
            float heavyCarryLoad = isSwimMode && playerMovement.IsDraggingHeavyCargo
                ? playerMovement.HeavyCarryLoad
                : 0f;
            float diveAttack01 = isSwimMode ? ResolveDiveAttack01() : 0f;

            if (transportBoost01 > 0f)
                targetModeBlend = math.max(targetModeBlend, transportBoost01 * ResolveTransportModeBlendFloor());

            float modeT = ResolveDecayBlend(modeFadeSpeed, dt);
            _modeBlend = math.lerp(_modeBlend, targetModeBlend, modeT);

            float speedFactor = 0f;
            if (_modeBlend > 0.01f)
            {
                Vector3 velocity = _playerRb.linearVelocity;
                float maxSpeed = playerMovement.CurrentSuit != null
                    ? playerMovement.CurrentSuit.maxSwimSpeed
                    : 12f;

                speedFactor = ResolveSquaredSpeedFactor(
                    velocity,
                    maxSpeed,
                    locomotionMode == PlayerLocomotionMode.SurfaceSwim);

                if (transportBoost01 > 0f)
                    speedFactor = math.max(speedFactor, transportBoost01 * ResolveTransportIdleSpeedFloor());
            }

            float driveVolumeBoostValue = transportBoost01 * ResolveTransportVolumeBoost() + diveAttack01 * diveVolumeBoost;
            float drivePitchBoostValue = transportBoost01 * ResolveTransportPitchBoost() + diveAttack01 * divePitchBoost;
            float loadVolumeMultiplier = 1f + heavyCarryLoad * heavyCarryVolumeBoost;
            float loadPitchMultiplier = 1f - heavyCarryLoad * heavyCarryPitchDrag;
            float transportAudioScale = ResolveTransportAudioScale();

            float targetVolume = math.lerp(idleVolume, maxVolume, speedFactor) * _modeBlend * modeVolumeMultiplier;
            targetVolume = math.clamp((targetVolume + driveVolumeBoostValue * _modeBlend) * loadVolumeMultiplier * transportAudioScale, 0f, 1f);

            float targetPitch = math.lerp(idlePitch, maxPitch, speedFactor) * modePitchMultiplier * loadPitchMultiplier;
            targetPitch = math.clamp(targetPitch + drivePitchBoostValue, 0.1f, 3f);
            targetPitch = math.lerp(1f, targetPitch, transportAudioScale);

            float volumeT = ResolveDecayBlend(volumeResponseSpeed, dt);
            float pitchT = ResolveDecayBlend(pitchResponseSpeed, dt);

            _currentVolume = math.lerp(_currentVolume, targetVolume, volumeT);
            _currentPitch = math.lerp(_currentPitch, targetPitch, pitchT);

            _audioSource.volume = _currentVolume;
            _audioSource.pitch = _currentPitch;
        }

        private void EnsureProceduralThrusterClip()
        {
            if (_proceduralThrusterClip != null)
                return;

            _proceduralThrusterClip = AudioClip.Create(
                "H8_Procedural_SubmarineEngine",
                ProceduralThrusterFrameCount,
                1,
                ProceduralThrusterSampleRate,
                true,
                OnProceduralAudioRead,
                OnProceduralAudioSetPosition);
        }

        private void OnProceduralAudioRead(float[] data)
        {
            if (data == null)
                return;

            float rawGain = _currentVolume;
            float rawPitch = _currentPitch;
            float gain = math.isfinite(rawGain) ? math.saturate(rawGain) : 0f;
            float pitch = math.isfinite(rawPitch) ? math.clamp(rawPitch, 0.1f, 3f) : 1f;
            float baseStep = ProceduralBaseFrequencyHertz * pitch * ProceduralThrusterSampleRateInv;
            float whineStep = ProceduralWhineFrequencyHertz * pitch * ProceduralThrusterSampleRateInv;
            float phase = _proceduralPhase;
            float whinePhase = _proceduralWhinePhase;
            float noiseLowPass = _proceduralNoiseLowPass;
            uint noiseState = _proceduralNoiseState;

            for (int i = 0; i < data.Length; i++)
            {
                noiseState = noiseState * 1664525u + 1013904223u;
                float white = ((noiseState >> 8) & 0xFFFF) * 0.000030518044f - 1f;
                noiseLowPass = math.lerp(noiseLowPass, white, 0.08f);

                float motor = math.sin(phase * ProceduralTwoPi) * 0.52f;
                float whine = math.sin(whinePhase * ProceduralTwoPi) * 0.18f;
                data[i] = (motor + whine + noiseLowPass * 0.24f) * gain;

                phase += baseStep;
                whinePhase += whineStep;
                phase -= math.floor(phase);
                whinePhase -= math.floor(whinePhase);
            }

            _proceduralPhase = phase;
            _proceduralWhinePhase = whinePhase;
            _proceduralNoiseLowPass = noiseLowPass;
            _proceduralNoiseState = noiseState;
        }

        private void OnProceduralAudioSetPosition(int position)
        {
            if (position == 0)
            {
                _proceduralPhase = 0f;
                _proceduralWhinePhase = 0f;
                _proceduralNoiseLowPass = 0f;
            }
        }

        private float ResolveTransportBoost01()
        {
            TryCacheTransportCoordinatorOnce();

            bool coordinatorOwnsTransport = playerTransportCoordinator != null && playerTransportCoordinator.HasActiveTransportSource();
            if (coordinatorOwnsTransport)
                return playerTransportCoordinator.ResolveTransportBoost01();

            if (playerToolManager == null || playerToolManager.IsSwapping)
                return 0f;

            IPlayerTransportSource transportSource = playerToolManager.CurrentToolTransportSource;
            if (transportSource == null)
                return 0f;

            float transportBoost = transportSource.GetTransportBoost01();
            if (transportBoost > 0f)
                return math.saturate(transportBoost);

            float reference = math.max(ResolveTransportPropulsionReference(), 0.01f);
            return math.saturate(transportSource.GetTransportPropulsionForce() / reference);
        }

        private PlayerTransportFeelContract ResolveTransportFeelContract()
        {
            TryCacheTransportCoordinatorOnce();

            bool coordinatorOwnsTransport = playerTransportCoordinator != null && playerTransportCoordinator.HasActiveTransportSource();
            if (coordinatorOwnsTransport)
                return playerTransportCoordinator.ResolveTransportFeelContract();

            if (playerToolManager == null || playerToolManager.IsSwapping)
                return null;

            return playerToolManager.CurrentToolTransportFeelContract;
        }

        private float ResolveTransportPropulsionReference()
        {
            return _transportFeelContractCurrent != null
                ? _transportFeelContractCurrent.PropulsionForceReference
                : mantaPropulsionReference;
        }

        private float ResolveTransportIdleSpeedFloor()
        {
            return _transportFeelContractCurrent != null
                ? _transportFeelContractCurrent.AudioIdleSpeedFloor
                : mantaIdleSpeedFloor;
        }

        private float ResolveTransportVolumeBoost()
        {
            return _transportFeelContractCurrent != null
                ? _transportFeelContractCurrent.AudioVolumeBoost
                : mantaVolumeBoost;
        }

        private float ResolveTransportPitchBoost()
        {
            return _transportFeelContractCurrent != null
                ? _transportFeelContractCurrent.AudioPitchBoost
                : mantaPitchBoost;
        }

        private float ResolveTransportModeBlendFloor()
        {
            return _transportFeelContractCurrent != null
                ? _transportFeelContractCurrent.AudioModeBlendFloor
                : mantaModeBlendFloor;
        }

        private float ResolveTransportAudioScale()
        {
            return _transportFeelContractCurrent != null
                ? _transportFeelContractCurrent.ThrusterAudioScale
                : 1f;
        }

        private float ResolveDiveAttack01()
        {
            Vector3 velocity = _playerRb.linearVelocity;
            float downwardSpeed = math.max(0f, -velocity.y);
            float reference = math.max(diveVelocityReference, 0.01f);
            return math.saturate(downwardSpeed / reference);
        }

        private static float ResolveDecayBlend(float speed, float deltaTime)
        {
            float x = math.max(0f, speed) * math.max(0f, deltaTime);
            return math.saturate(x / (1f + x));
        }

        private static float ResolveSquaredSpeedFactor(Vector3 velocity, float maxSpeed, bool surfaceOnly)
        {
            if (!(maxSpeed > 0f))
                return 0f;

            float speedSq = surfaceOnly
                ? velocity.x * velocity.x + velocity.z * velocity.z
                : velocity.x * velocity.x + velocity.y * velocity.y + velocity.z * velocity.z;
            float maxSpeedSq = math.max(maxSpeed * maxSpeed, 0.0001f);
            return math.saturate(speedSq / maxSpeedSq);
        }

        private void TryCacheTransportCoordinatorOnce()
        {
            if (playerTransportCoordinator != null || _transportCoordinatorLookupAttempted)
                return;

            _transportCoordinatorLookupAttempted = true;
            if (playerMovement != null && playerMovement.TryGetComponent(out playerTransportCoordinator))
                return;

            gameObject.TryGetComponent(out playerTransportCoordinator);
        }

        private void RefreshRuntimeAudioServicesCold()
        {
            CacheSpatialAudioManager(Hecton8.Audio.SpatialAudioManager.ActiveRuntimeInstance);
        }

        private void CacheSpatialAudioManager(IAudioService audioService)
        {
            _cachedSpatialAudioManager = audioService as SpatialAudioManager;
        }

        private void TryAssignMixerRoute(bool force = false)
        {
            if (_audioSource == null || (!force && _audioSource.outputAudioMixerGroup != null))
                return;

            SpatialAudioManager spatialAudioManager = _cachedSpatialAudioManager;
            if (spatialAudioManager != null)
                _audioSource.outputAudioMixerGroup = spatialAudioManager.SfxGroup;
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

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Player);
            _registered = GlobalRegistry.Updatables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);

            _registered = false;
        }
    }
}
