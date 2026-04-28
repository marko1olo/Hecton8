using System;
using Hecton8.AI;
using Hecton8.Core;
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

        private Transform _cachedTransform;
        private Rigidbody _playerRigidbody;
        private PlayerFlashlight _playerFlashlight;
        private PlayerToolManager _playerToolManager;
        private PlayerTransportCoordinator _playerTransportCoordinator;
        private PlayerTool _observedTool;
        private Action<bool> _cachedToolUsedHandler;
        private bool _registered;
        private float _toolUsePulseTimer;
        private float _toolUsePulseAmplitude;

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
            ResolveReferences();
            RefreshObservedToolSubscription();

            if (_toolUsePulseTimer > 0f)
            {
                _toolUsePulseTimer = Mathf.Max(0f, _toolUsePulseTimer - dt);
            }
            else
            {
                _toolUsePulseAmplitude = 0f;
            }

            float toolUseNoise01 = 0f;
            if (_toolUsePulseTimer > 0f && ToolUsePulseDuration > 0f)
                toolUseNoise01 = _toolUsePulseAmplitude * (_toolUsePulseTimer / ToolUsePulseDuration);

            Vector3 playerPosition = _cachedTransform.position;
            float movementSpeedSqr = _playerRigidbody != null ? _playerRigidbody.linearVelocity.sqrMagnitude : 0f;
            bool flashlightOn = _playerFlashlight != null && _playerFlashlight.IsOn;
            float transportBoost01 = ResolveTransportBoost01();
            float transportSignature = ResolveTransportFaunaSignature();

            NoiseSystem.ReportPlayerSignal(
                playerPosition,
                movementSpeedSqr,
                flashlightOn,
                transportBoost01,
                transportSignature,
                toolUseNoise01);
        }

        private void TryRegister()
        {
            if (_registered)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Player);
            _registered = true;
        }

        private void ResolveReferences()
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;

            if (_playerRigidbody == null)
                _cachedTransform.TryGetComponent(out _playerRigidbody);

            if (_playerFlashlight == null)
                _cachedTransform.TryGetComponent(out _playerFlashlight);

            if (_playerTransportCoordinator == null)
                _cachedTransform.TryGetComponent(out _playerTransportCoordinator);

            if (_playerToolManager == null)
            {
                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                if (playerContext != null)
                    _playerToolManager = playerContext.ToolManager;
            }
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
            return transportSource != null ? Mathf.Clamp01(transportSource.GetTransportBoost01()) : 0f;
        }

        private float ResolveTransportFaunaSignature()
        {
            if (_playerTransportCoordinator != null)
            {
                PlayerTransportPreset transportPreset = _playerTransportCoordinator.ResolveTransportPreset();
                if (transportPreset != null)
                    return Mathf.Max(0f, transportPreset.FaunaDetectionSignature);
            }

            if (_playerToolManager == null || _playerToolManager.IsSwapping)
                return 1f;

            PlayerTransportFeelContract transportFeelContract = _playerToolManager.CurrentToolTransportFeelContract;
            if (transportFeelContract == null || transportFeelContract.Preset == null)
                return 1f;

            return Mathf.Max(0f, transportFeelContract.Preset.FaunaDetectionSignature);
        }
    }
}
