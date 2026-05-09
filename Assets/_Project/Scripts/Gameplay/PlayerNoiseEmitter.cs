using System;
using Hecton8.AI;
using Hecton8.Core;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Centralized player-noise emitter that reports locomotion, transport, light, and tool-use state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerNoiseEmitter : MonoBehaviour, ITickable, IUpdatable
    {
        private const float ToolUsePulseDuration = 0.6f;
        private const float PrimaryToolNoisePulse = 1f;
        private const float SecondaryToolNoisePulse = 0.75f;
        private const float ReferenceRefreshInterval = 0.5f;

        private Transform _cachedTransform;
        private HectonPlayerMovement _playerMovement;
        private Rigidbody _playerRigidbody;
        private PlayerFlashlight _playerFlashlight;
        private PlayerToolManager _playerToolManager;
        private PlayerTransportCoordinator _playerTransportCoordinator;
        private PlayerTool _observedTool;
        private Action<bool> _cachedToolUsedHandler;
        private bool _registered;
        private float _toolUsePulseTimer;
        private float _toolUsePulseAmplitude;
        private float _referenceRefreshTimer;

        /// <summary>
        /// Ensures the centralized player-noise emitter exists on the provided player root.
        /// </summary>
        public static PlayerNoiseEmitter EnsureAttached(Transform playerTransform)
        {
            if (playerTransform == null)
                return null;

            if (!playerTransform.TryGetComponent(out PlayerNoiseEmitter emitter))
                emitter = playerTransform.gameObject.AddComponent<PlayerNoiseEmitter>();

            return emitter;
        }

        private void Awake()
        {
            _cachedTransform = transform;
            _cachedToolUsedHandler = HandleToolUsed;
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            RefreshObservedToolSubscription();
            TryRegister();
        }

        private void Start()
        {
            ResolveReferences();
            RefreshObservedToolSubscription();
            TryRegister();
        }

        private void OnDisable()
        {
            if (_registered)
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);

            _registered = false;
            ClearObservedToolSubscription();
            NoiseSystem.ClearPlayerSignal();
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
            if (_playerToolManager == null ||
                _playerMovement == null ||
                _playerRigidbody == null ||
                _playerFlashlight == null ||
                _playerTransportCoordinator == null)
            {
                _referenceRefreshTimer -= math.max(0f, dt);
                if (_referenceRefreshTimer <= 0f)
                {
                    ResolveReferences();
                    _referenceRefreshTimer = ReferenceRefreshInterval;
                }
            }

            RefreshObservedToolSubscription();

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
                playerPosition = playerAup.ToRuntimeFloat3();
            }
            else
            {
                playerPosition = _cachedTransform.position;
                playerAup = AbsoluteUniversePosition.FromRuntimePosition(playerPosition);
            }

            float movementSpeedSqr = _playerRigidbody != null ? _playerRigidbody.linearVelocity.sqrMagnitude : 0f;
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

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);
        }

        private void ResolveReferences()
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;

            if (_playerMovement == null && playerContext != null)
                _playerMovement = playerContext.PlayerMovement;

            if (_playerMovement == null)
                _cachedTransform.TryGetComponent(out _playerMovement);

            if (_playerRigidbody == null)
                _cachedTransform.TryGetComponent(out _playerRigidbody);

            if (_playerFlashlight == null)
                _cachedTransform.TryGetComponent(out _playerFlashlight);

            if (_playerTransportCoordinator == null)
                _cachedTransform.TryGetComponent(out _playerTransportCoordinator);

            if (_playerToolManager == null)
            {
                if (playerContext != null)
                    _playerToolManager = playerContext.ToolManager;
            }
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            if (_playerMovement != null)
            {
                playerAup = _playerMovement.CurrentAup;
                return true;
            }

            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext))
            {
                PlayerMovementRuntimeState movementState = runtimeContext.MovementState;
                if ((movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
                {
                    playerAup = movementState.PredictedAup;
                    return true;
                }
            }

            playerAup = default;
            return false;
        }

        private void RefreshObservedToolSubscription()
        {
            PlayerTool currentTool = _playerToolManager != null ? _playerToolManager.CurrentTool : null;
            if (ReferenceEquals(currentTool, _observedTool))
                return;

            ClearObservedToolSubscription();
            _observedTool = currentTool;

            if (_observedTool != null)
                _observedTool.OnToolUsed += _cachedToolUsedHandler;
        }

        private void ClearObservedToolSubscription()
        {
            if (_observedTool != null)
                _observedTool.OnToolUsed -= _cachedToolUsedHandler;

            _observedTool = null;
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
