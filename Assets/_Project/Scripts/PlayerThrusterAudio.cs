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
    public sealed class PlayerThrusterAudio : MonoBehaviour, ITickable
    {
        [Header("References")]
        [SerializeField] private HectonPlayerMovement playerMovement;
        [SerializeField] private PlayerToolManager playerToolManager;
        [SerializeField] private PlayerTransportCoordinator playerTransportCoordinator;

        [Header("Audio Clips")]
        [Tooltip("Looping thruster / servo sound. Should be a seamless loop.")]
        [SerializeField] private AudioClip thrusterLoopClip;

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

        private AudioSource _audioSource;
        private Rigidbody _playerRb;
        private float _currentVolume;
        private float _currentPitch;
        private float _modeBlend;
        private bool _registered;
        private PlayerTransportFeelContract _transportFeelContractCurrent;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();

            _audioSource.clip = thrusterLoopClip;
            _audioSource.loop = true;
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 1f;
            _audioSource.volume = 0f;
            _audioSource.pitch = idlePitch;
            _audioSource.priority = 200;

            if (playerMovement != null)
            {
                _playerRb = playerMovement.GetComponent<Rigidbody>();
                if (playerToolManager == null)
                    playerMovement.TryGetComponent(out playerToolManager);
                if (playerTransportCoordinator == null)
                    playerMovement.TryGetComponent(out playerTransportCoordinator);
            }

            _currentVolume = 0f;
            _currentPitch = idlePitch;
            _modeBlend = 0f;
        }

        private void OnEnable()
        {
            if (PlayerCriticalProceduralAudioRenderer.IsRuntimeInstalled)
            {
                if (_audioSource != null && _audioSource.isPlaying)
                    _audioSource.Stop();

                enabled = false;
                return;
            }

            TryRegister();

            if (thrusterLoopClip != null && _audioSource != null)
                _audioSource.Play();
        }

        private void OnDisable()
        {
            TryUnregister();

            if (_audioSource != null && _audioSource.isPlaying)
                _audioSource.Stop();
        }

        private void OnDestroy()
        {
            TryUnregister();
        }

        public void Tick(float deltaTime)
        {
            if (playerMovement == null || _playerRb == null)
                return;

            if (thrusterLoopClip == null)
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

            float modeT = 1f - math.exp(-modeFadeSpeed * dt);
            _modeBlend = math.lerp(_modeBlend, targetModeBlend, modeT);

            float speedFactor = 0f;
            if (_modeBlend > 0.01f)
            {
                Vector3 velocity = _playerRb.linearVelocity;
                float speed = math.sqrt(velocity.x * velocity.x + velocity.y * velocity.y + velocity.z * velocity.z);
                float maxSpeed = playerMovement.CurrentSuit != null
                    ? playerMovement.CurrentSuit.maxSwimSpeed
                    : 12f;

                if (locomotionMode == PlayerLocomotionMode.SurfaceSwim)
                    speed = math.sqrt(velocity.x * velocity.x + velocity.z * velocity.z);

                speedFactor = maxSpeed > 0f
                    ? math.clamp(speed / maxSpeed, 0f, 1f)
                    : 0f;

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

            float volumeT = 1f - math.exp(-volumeResponseSpeed * dt);
            float pitchT = 1f - math.exp(-pitchResponseSpeed * dt);

            _currentVolume = math.lerp(_currentVolume, targetVolume, volumeT);
            _currentPitch = math.lerp(_currentPitch, targetPitch, pitchT);

            _audioSource.volume = _currentVolume;
            _audioSource.pitch = _currentPitch;
        }

        private float ResolveTransportBoost01()
        {
            if (playerTransportCoordinator == null)
                gameObject.TryGetComponent(out playerTransportCoordinator);

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
            if (playerTransportCoordinator == null)
                gameObject.TryGetComponent(out playerTransportCoordinator);

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

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Player);
            _registered = true;
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
