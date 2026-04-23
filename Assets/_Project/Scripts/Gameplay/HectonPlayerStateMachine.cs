using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// State owner for locomotion context and wipeout recovery timing.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Player/Hecton Player State Machine")]
    public sealed class HectonPlayerStateMachine : MonoBehaviour, IHectonPlayerStateMachine
    {
        private PlayerEnvironmentState _currentEnvironmentState = PlayerEnvironmentState.DryExterior;
        private PlayerSupportState _currentSupportState = PlayerSupportState.Grounded;
        private PlayerOverrideState _currentOverrideState = PlayerOverrideState.None;
        private PlayerLocomotionMode _currentLocomotionMode = PlayerLocomotionMode.DryGroundWalk;
        private float _wipeoutTimer;
        private float _wipeoutSeverity;

        /// <inheritdoc />
        public PlayerEnvironmentState CurrentEnvironmentState => _currentEnvironmentState;

        /// <inheritdoc />
        public PlayerSupportState CurrentSupportState => _currentSupportState;

        /// <inheritdoc />
        public PlayerOverrideState CurrentOverrideState => _currentOverrideState;

        /// <inheritdoc />
        public PlayerLocomotionMode CurrentLocomotionMode => _currentLocomotionMode;

        /// <inheritdoc />
        public bool IsInWipeout => _wipeoutTimer > 0f;

        /// <inheritdoc />
        public float WipeoutTimer => _wipeoutTimer;

        /// <inheritdoc />
        public float WipeoutSeverity => _wipeoutSeverity;

        /// <inheritdoc />
        public void SyncLocomotionMode(PlayerLocomotionMode mode)
        {
            _currentLocomotionMode = mode;
        }

        /// <inheritdoc />
        public void SyncContext(
            PlayerEnvironmentState environmentState,
            PlayerSupportState supportState,
            PlayerOverrideState overrideState,
            PlayerLocomotionMode mode)
        {
            _currentEnvironmentState = environmentState;
            _currentSupportState = supportState;
            _currentOverrideState = overrideState;
            _currentLocomotionMode = mode;
        }

        /// <inheritdoc />
        public void BeginWipeout(float severity, float duration)
        {
            _wipeoutSeverity = math.max(_wipeoutSeverity, math.saturate(severity));
            _wipeoutTimer = math.max(_wipeoutTimer, math.max(0f, duration));
            if (_wipeoutTimer > 0f)
                _currentOverrideState = PlayerOverrideState.Wipeout;
        }

        /// <inheritdoc />
        public void AdvanceFixed(float fixedDeltaTime)
        {
            if (_wipeoutTimer <= 0f)
                return;

            _wipeoutTimer -= fixedDeltaTime;
            if (_wipeoutTimer <= 0f)
            {
                _wipeoutTimer = 0f;
                _wipeoutSeverity = 0f;
                if (_currentOverrideState == PlayerOverrideState.Wipeout)
                    _currentOverrideState = PlayerOverrideState.None;
            }
        }

        /// <inheritdoc />
        public void ResetRuntimeState()
        {
            _wipeoutTimer = 0f;
            _wipeoutSeverity = 0f;
            _currentEnvironmentState = PlayerEnvironmentState.DryExterior;
            _currentSupportState = PlayerSupportState.Grounded;
            _currentOverrideState = PlayerOverrideState.None;
            _currentLocomotionMode = PlayerLocomotionMode.DryGroundWalk;
        }
    }
}
