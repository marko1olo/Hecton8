using Unity.Mathematics;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// State owner for locomotion-mode mirrors and wipeout recovery timing.
    /// </summary>
    [DefaultExecutionOrder(30)] // Explicit helper registration ordering: owner -> environment -> motor -> state.
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Player/Hecton Player State Machine")]
    public sealed class HectonPlayerStateMachine : MonoBehaviour, IHectonPlayerStateMachine, IFixedTickable
    {
        private PlayerLocomotionMode _currentLocomotionMode = PlayerLocomotionMode.DryGroundWalk;
        private float _wipeoutTimer;
        private float _wipeoutSeverity;
        private bool _registered;

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
        public void BeginWipeout(float severity, float duration)
        {
            _wipeoutSeverity = math.max(_wipeoutSeverity, math.saturate(severity));
            _wipeoutTimer = math.max(_wipeoutTimer, math.max(0f, duration));
        }

        private void OnEnable()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
            ResetRuntimeState();
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
            }
        }

        /// <inheritdoc />
        public void FixedTick(float fixedDeltaTime)
        {
            AdvanceFixed(fixedDeltaTime);
        }

        /// <inheritdoc />
        public void ResetRuntimeState()
        {
            _wipeoutTimer = 0f;
            _wipeoutSeverity = 0f;
            _currentLocomotionMode = PlayerLocomotionMode.DryGroundWalk;
        }

        private void TryRegister()
        {
            if (_registered || GameTickManager.Instance == null)
                return;

            GameTickManager.Instance.Register((IFixedTickable)this);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered || GameTickManager.Instance == null)
                return;

            GameTickManager.Instance.Unregister((IFixedTickable)this);
            _registered = false;
        }
    }
}
