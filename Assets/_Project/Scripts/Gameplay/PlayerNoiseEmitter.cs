using Hecton8.AI;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Centralized player-noise emitter that reports locomotion, transport, light, and tool-use state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerNoiseEmitter : MonoBehaviour, ITickable, IUpdatable, IGlobalRegistryHotSwapListener
    {
        private const float ToolUsePulseDuration = 0.6f;
        private const float PrimaryToolNoisePulse = 1f;
        private const float SecondaryToolNoisePulse = 0.75f;
        private const float ReferenceRefreshInterval = 0.5f;
        private const uint KccVelocityNoiseMaxAgeFrames = 12u;

        private Transform _cachedTransform;
        private HectonPlayerMovement _playerMovement;
        private PlayerFlashlight _playerFlashlight;
        private PlayerToolManager _playerToolManager;
        private PlayerTransportCoordinator _playerTransportCoordinator;
        private PlayerTool _observedTool;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private bool _registered;
        private bool _hotSwapListenerRegistered;
        private bool _localReferenceProbeCompleted;
        private float _toolUsePulseTimer;
        private float _toolUsePulseAmplitude;
        private float _referenceRefreshTimer;
        private float _lastObservedToolUseTime = float.NegativeInfinity;

        /// <summary>
        /// Ensures the centralized player-noise emitter exists on the provided player root.
        /// </summary>
        public static PlayerNoiseEmitter EnsureAttached(Transform playerTransform)
        {
            if (playerTransform == null)
                return null;

            if (!playerTransform.TryGetComponent(out PlayerNoiseEmitter emitter))
            {
                // Player-build construction path: no authored/bootstrap instance reachable.
                // Must construct in player builds when bootstrap reorders or skips registration.
                emitter = playerTransform.gameObject.AddComponent<PlayerNoiseEmitter>();
            }

            return emitter;
        }

        private void Awake()
        {
            _cachedTransform = transform;
            CacheRegistryServicesCold();
            ResolveReferencesCold();
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            ResolveReferencesCold();
            RefreshObservedToolReference();
            ConsumeObservedToolUsePulse();
            TryRegister();
        }

        private void Start()
        {
            CacheRegistryServicesCold();
            ResolveReferencesCold();
            RefreshObservedToolReference();
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
            ClearObservedToolReference();
            NoiseSystem.ClearPlayerSignal();
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
            if (_playerToolManager == null ||
                _playerMovement == null ||
                _playerFlashlight == null ||
                _playerTransportCoordinator == null)
            {
                _referenceRefreshTimer -= math.max(0f, dt);
                if (_referenceRefreshTimer <= 0f)
                {
                    ResolveReferencesFromRuntimeContext();
                    _referenceRefreshTimer = ReferenceRefreshInterval;
                }
            }

            RefreshObservedToolReference();
            ConsumeObservedToolUsePulse();

            if (_toolUsePulseTimer > 0f)
            {
                _toolUsePulseTimer = math.max(0f, _toolUsePulseTimer - dt);
            }
            else
            {
                _toolUsePulseAmplitude = 0f;
            }

            float toolUseNoise01 = 0f;
            if (_toolUsePulseTimer > 0f && ToolUsePulseDuration > 0f)
                toolUseNoise01 = _toolUsePulseAmplitude * (_toolUsePulseTimer / ToolUsePulseDuration);

            AbsoluteUniversePosition playerAup;
            Vector3 playerPosition;
            if (TryResolvePlayerAup(out playerAup))
            {
                float3 runtimePosition = playerAup.ToRuntimeFloat3();
                playerPosition = new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            }
            else
            {
                if (HasPlayerRuntimeContext())
                    return;

                playerPosition = ResolveCachedRuntimePosition();
                if (!TryResolveRuntimeAup(playerPosition, out playerAup))
                    return;
            }

            float movementSpeedSqr = TryResolveKccVelocity(out Vector3 kccVelocity) ? kccVelocity.sqrMagnitude : 0f;
            bool flashlightOn = _playerFlashlight != null && _playerFlashlight.IsOn;
            float transportBoost01 = ResolveTransportBoost01();
            float transportSignature = ResolveTransportFaunaSignature();

            NoiseSystem.ReportPlayerSignal(
                playerPosition,
                in playerAup,
                movementSpeedSqr,
                flashlightOn,
                transportBoost01,
                transportSignature,
                toolUseNoise01);
        }

        private Vector3 ResolveCachedRuntimePosition()
        {
            return _cachedTransform != null ? _cachedTransform.position : Vector3.zero;
        }

        private static bool TryResolveKccVelocity(out Vector3 velocity)
        {
            velocity = Vector3.zero;
            if (!CoreDeterminismSignals.TryGetLatestKccVelocity(out KccVelocitySignal signal) || signal.Sequence == 0u)
                return false;

            uint currentFrame = SystemDispatcher.CurrentFrameId;
            uint signalFrame = signal.Frame != 0u ? signal.Frame : signal.Sequence;
            if (currentFrame != 0u &&
                signalFrame != 0u &&
                (signalFrame > currentFrame || currentFrame - signalFrame > KccVelocityNoiseMaxAgeFrames))
            {
                return false;
            }

            float3 value = signal.Velocity;
            if (!math.all(math.isfinite(value)))
                return false;

            velocity = new Vector3(value.x, value.y, value.z);
            return true;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
            _registered = false;
        }

        private void ResolveReferencesFromRuntimeContext(bool replaceExisting = false)
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;

            IPlayerRuntimeContext playerContext = _cachedPlayerContext;

            if ((_playerMovement == null || replaceExisting) && playerContext != null)
                _playerMovement = playerContext.PlayerMovement;

            if ((_playerToolManager == null || replaceExisting) && playerContext != null)
                _playerToolManager = playerContext.ToolManager;

            if ((_playerFlashlight == null || replaceExisting) && playerContext != null)
                _playerFlashlight = playerContext.Flashlight;

            if ((_playerTransportCoordinator == null || replaceExisting) && playerContext != null)
                _playerTransportCoordinator = playerContext.PlayerTransportCoordinator;

            if (replaceExisting && playerContext == null)
            {
                _playerMovement = null;
                _playerToolManager = null;
                _playerFlashlight = null;
                _playerTransportCoordinator = null;
            }
        }

        private void ResolveReferencesCold()
        {
            ResolveReferencesFromRuntimeContext();

            bool canProbeLocalComponents = !_localReferenceProbeCompleted;
            if (_playerMovement == null && canProbeLocalComponents)
                _cachedTransform.TryGetComponent(out _playerMovement);

            if (_playerFlashlight == null && canProbeLocalComponents)
                _cachedTransform.TryGetComponent(out _playerFlashlight);

            if (_playerTransportCoordinator == null && canProbeLocalComponents)
                _cachedTransform.TryGetComponent(out _playerTransportCoordinator);

            if (canProbeLocalComponents)
                _localReferenceProbeCompleted = true;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregister();
                    if (currentService != null && isActiveAndEnabled)
                        TryRegister();
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _cachedPlayerContext = currentService as IPlayerRuntimeContext;
                    ResolveReferencesFromRuntimeContext(replaceExisting: true);
                    RefreshObservedToolReference();
                    break;
            }
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

        private void CacheRegistryServicesCold()
        {
            _cachedPlayerContext = GlobalRegistry.Player;
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            playerAup = default;

            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext != null)
            {
                return TryResolvePlayerAupFromRuntimeContext(playerContext, out playerAup);
            }

            IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;
            if (runtimeContext != null)
            {
                return TryResolvePlayerAupFromRuntimeContext(runtimeContext, out playerAup);
            }

            if (_playerMovement != null)
            {
                AbsoluteUniversePosition currentAup = _playerMovement.CurrentAup;
                if (currentAup.IsFinite())
                {
                    playerAup = currentAup;
                    return true;
                }
            }

            return false;
        }

        private bool HasPlayerRuntimeContext()
        {
            return _cachedPlayerContext != null ||
                   PlayerRuntimeContextService.ActiveRuntimeContext != null;
        }

        private static bool TryResolvePlayerAupFromRuntimeContext(
            IPlayerRuntimeContext playerContext,
            out AbsoluteUniversePosition playerAup)
        {
            playerAup = default;
            if (playerContext == null)
                return false;

            if (playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                (snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                snapshot.Aup.IsFinite())
            {
                playerAup = snapshot.Aup;
                return true;
            }

            return playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                   TryResolvePlayerAupFromMovementState(in movementState, out playerAup);
        }

        private static bool TryResolvePlayerAupFromMovementState(
            in PlayerMovementRuntimeState movementState,
            out AbsoluteUniversePosition playerAup)
        {
            playerAup = default;
            if ((movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u ||
                !movementState.PredictedAup.IsFinite())
            {
                return false;
            }

            playerAup = movementState.PredictedAup;
            return true;
        }

        private static bool TryResolveRuntimeAup(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            float3 localRuntime = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(localRuntime)))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return positionAup.IsFinite();
        }

        private void RefreshObservedToolReference()
        {
            PlayerTool currentTool = _playerToolManager != null ? _playerToolManager.CurrentTool : null;
            if (ReferenceEquals(currentTool, _observedTool))
                return;

            _observedTool = currentTool;
            _lastObservedToolUseTime = _observedTool != null
                ? _observedTool.LastUseTime
                : float.NegativeInfinity;
        }

        private void ClearObservedToolReference()
        {
            _observedTool = null;
            _lastObservedToolUseTime = float.NegativeInfinity;
        }

        private void ConsumeObservedToolUsePulse()
        {
            if (_observedTool == null)
                return;

            float lastUseTime = _observedTool.LastUseTime;
            if (!math.isfinite(lastUseTime) || lastUseTime <= _lastObservedToolUseTime)
                return;

            _lastObservedToolUseTime = lastUseTime;
            HandleToolUsed(_observedTool.LastUseWasPrimary);
        }

        private void HandleToolUsed(bool isPrimary)
        {
            _toolUsePulseTimer = ToolUsePulseDuration;
            _toolUsePulseAmplitude = isPrimary ? PrimaryToolNoisePulse : SecondaryToolNoisePulse;
        }

        private float ResolveTransportBoost01()
        {
            if (_playerTransportCoordinator != null)
                return _playerTransportCoordinator.ResolveTransportBoost01();

            if (_playerToolManager == null || _playerToolManager.IsSwapping)
                return 0f;

            IPlayerTransportSource transportSource = _playerToolManager.CurrentToolTransportSource;
            return transportSource != null ? math.saturate(transportSource.GetTransportBoost01()) : 0f;
        }

        private float ResolveTransportFaunaSignature()
        {
            if (_playerTransportCoordinator != null)
            {
                PlayerTransportPreset transportPreset = _playerTransportCoordinator.ResolveTransportPreset();
                if (transportPreset != null)
                    return math.max(0f, transportPreset.FaunaDetectionSignature);
            }

            if (_playerToolManager == null || _playerToolManager.IsSwapping)
                return 1f;

            PlayerTransportFeelContract transportFeelContract = _playerToolManager.CurrentToolTransportFeelContract;
            if (transportFeelContract == null || transportFeelContract.Preset == null)
                return 1f;

            return math.max(0f, transportFeelContract.Preset.FaunaDetectionSignature);
        }
    }
}
