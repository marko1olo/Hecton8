using System;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    public enum WaterTransitionKind : byte
    {
        None = 0,
        SurfaceEnter = 1,
        SurfaceExit = 2,
        Splash = 3,
        SubmergeChanged = 4
    }

    [DisallowMultipleComponent]
    public sealed class WaterTransitionHandler : MonoBehaviour
    {
        private HectonPlayerMovement _owner;
        private uint _ownerSignalSourceId;
        private uint _lastConsumedWaterTransitionFrame;
        private int _lastConsumedSignalSnapshotDispatcherFrame = -1;
        private bool _hasConsumedWaterTransitionFrame;
        private float _surfaceExitGravityDelaySeconds;
        private float _surfaceExitGravityAcceleration;
        private float _surfaceExitGravityDurationSeconds;
        private float _surfaceExitGravityDelayTimer;
        private float _surfaceExitGravitySpikeTimer;

        public bool HasPendingSurfaceBreachGravity =>
            _surfaceExitGravityDelayTimer > 0f || _surfaceExitGravitySpikeTimer > 0f;

        public void Bind(HectonPlayerMovement owner)
        {
            _owner = owner;
            _ownerSignalSourceId = owner != null ? unchecked((uint)EntityId.ToULong(owner.GetEntityId())) : 0u;
        }

        public void ConfigureSurfaceBreachGravity(float delaySeconds, float acceleration, float durationSeconds)
        {
            _surfaceExitGravityDelaySeconds = math.max(0f, delaySeconds);
            _surfaceExitGravityAcceleration = math.max(0f, acceleration);
            _surfaceExitGravityDurationSeconds = math.max(0f, durationSeconds);
        }

        public void ResetRuntimeState()
        {
            _surfaceExitGravityDelayTimer = 0f;
            _surfaceExitGravitySpikeTimer = 0f;
            _lastConsumedWaterTransitionFrame = 0u;
            _lastConsumedSignalSnapshotDispatcherFrame = -1;
            _hasConsumedWaterTransitionFrame = false;
        }

        public void ConsumeWaterTransitionSignals()
        {
            if (_owner == null || _ownerSignalSourceId == 0u)
                return;

            int dispatcherFrame = SystemDispatcher.CurrentFrameIndex;
            if (_lastConsumedSignalSnapshotDispatcherFrame == dispatcherFrame)
                return;

            _lastConsumedSignalSnapshotDispatcherFrame = dispatcherFrame;
            ReadOnlySpan<WaterTransitionSignal> signals = SignalBus<WaterTransitionSignal>.GetFrameSnapshot();
            if (signals.Length == 0)
                return;

            uint newestFrame = _lastConsumedWaterTransitionFrame;
            for (int i = 0; i < signals.Length; i++)
            {
                WaterTransitionSignal signal = signals[i];
                newestFrame = math.max(newestFrame, signal.Frame);
                if (_hasConsumedWaterTransitionFrame && signal.Frame <= _lastConsumedWaterTransitionFrame)
                    continue;

                if (signal.SourceId != _ownerSignalSourceId)
                    continue;

                if ((WaterTransitionKind)signal.Kind != WaterTransitionKind.SurfaceExit)
                    continue;

                StartSurfaceExitGravityArc();
            }

            _lastConsumedWaterTransitionFrame = newestFrame;
            _hasConsumedWaterTransitionFrame = true;
        }

        public void AdvanceSurfaceBreachGravity(float fixedDeltaTime, Vector3 gravityVector, float gravityMagnitude)
        {
            ConsumeWaterTransitionSignals();

            float safeDeltaTime = math.max(0f, fixedDeltaTime);
            if (safeDeltaTime <= 0f || _owner == null)
                return;

            if (_surfaceExitGravityDelayTimer > 0f)
            {
                _surfaceExitGravityDelayTimer -= safeDeltaTime;
                if (_surfaceExitGravityDelayTimer <= 0f)
                {
                    _surfaceExitGravityDelayTimer = 0f;
                    _surfaceExitGravitySpikeTimer = math.max(_surfaceExitGravitySpikeTimer, _surfaceExitGravityDurationSeconds);
                }
            }

            if (_surfaceExitGravitySpikeTimer <= 0f || _surfaceExitGravityAcceleration <= 0f)
                return;

            _surfaceExitGravitySpikeTimer -= safeDeltaTime;
            if (_surfaceExitGravitySpikeTimer < 0f)
                _surfaceExitGravitySpikeTimer = 0f;

            Vector3 gravityDirection = gravityMagnitude > 0.0001f
                ? gravityVector / gravityMagnitude
                : Vector3.down;
            _owner.QueueSubsystemExternalAcceleration(gravityDirection * _surfaceExitGravityAcceleration);
        }

        private void OnDisable()
        {
            ResetRuntimeState();
        }

        private void StartSurfaceExitGravityArc()
        {
            if (_surfaceExitGravityAcceleration <= 0f || _surfaceExitGravityDurationSeconds <= 0f)
                return;

            if (_surfaceExitGravityDelaySeconds > 0f)
            {
                _surfaceExitGravityDelayTimer = math.max(_surfaceExitGravityDelayTimer, _surfaceExitGravityDelaySeconds);
                _surfaceExitGravitySpikeTimer = 0f;
                return;
            }

            _surfaceExitGravityDelayTimer = 0f;
            _surfaceExitGravitySpikeTimer = math.max(_surfaceExitGravitySpikeTimer, _surfaceExitGravityDurationSeconds);
        }
    }
}
