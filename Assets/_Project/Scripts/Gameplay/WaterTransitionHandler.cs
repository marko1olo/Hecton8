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

    public readonly struct WaterTransitionEvent
    {
        public readonly int SourceInstanceId;
        public readonly WaterTransitionKind Kind;
        public readonly bool IsSubmerged;
        public readonly float Intensity;
        public readonly float SurfaceY;
        public readonly float VerticalSpeed;
        public readonly Vector3 RuntimePosition;
        public readonly AbsoluteUniversePosition AbsolutePosition;

        public WaterTransitionEvent(
            int sourceInstanceId,
            WaterTransitionKind kind,
            bool isSubmerged,
            float intensity,
            float surfaceY,
            float verticalSpeed,
            Vector3 runtimePosition)
        {
            SourceInstanceId = sourceInstanceId;
            Kind = kind;
            IsSubmerged = isSubmerged;
            Intensity = math.saturate(intensity);
            SurfaceY = surfaceY;
            VerticalSpeed = math.max(0f, verticalSpeed);
            RuntimePosition = HectonPlayerMotor.SafeVelocity(runtimePosition);
            AbsolutePosition = AbsoluteUniversePosition.FromRuntimePosition(RuntimePosition);
        }
    }

    public interface IWaterTransitionEventListener
    {
        void OnWaterTransition(in WaterTransitionEvent transitionEvent);
    }

    public static class WaterTransitionEvents
    {
        private const int MaxListeners = 16;
        private static readonly IWaterTransitionEventListener[] _listeners = new IWaterTransitionEventListener[MaxListeners]; // COLD ALLOC: IWaterTransitionEventListener[16] - fixed-capacity event listener registry - owner: WaterTransitionEvents
        private static int _listenerCount;

        public static void Register(IWaterTransitionEventListener listener)
        {
            if (listener == null)
                return;

            for (int i = 0; i < _listenerCount; i++)
            {
                if (ReferenceEquals(_listeners[i], listener))
                    return;
            }

            if (_listenerCount >= MaxListeners)
                return;

            _listeners[_listenerCount++] = listener;
        }

        public static void Unregister(IWaterTransitionEventListener listener)
        {
            if (listener == null)
                return;

            for (int i = 0; i < _listenerCount; i++)
            {
                if (!ReferenceEquals(_listeners[i], listener))
                    continue;

                int lastIndex = --_listenerCount;
                _listeners[i] = _listeners[lastIndex];
                _listeners[lastIndex] = null;
                return;
            }
        }

        public static void Publish(in WaterTransitionEvent transitionEvent)
        {
            for (int i = 0; i < _listenerCount; i++)
            {
                _listeners[i]?.OnWaterTransition(in transitionEvent);
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class WaterTransitionHandler : MonoBehaviour, IWaterTransitionEventListener
    {
        private HectonPlayerMovement _owner;
        private int _ownerInstanceId;
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
            _ownerInstanceId = owner != null ? unchecked((int)EntityId.ToULong(owner.GetEntityId())) : 0;
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
        }

        public void OnWaterTransition(in WaterTransitionEvent transitionEvent)
        {
            if (_owner == null || transitionEvent.SourceInstanceId != _ownerInstanceId)
                return;

            if (transitionEvent.Kind != WaterTransitionKind.SurfaceExit)
                return;

            StartSurfaceExitGravityArc();
        }

        public void AdvanceSurfaceBreachGravity(float fixedDeltaTime, Vector3 gravityVector, float gravityMagnitude)
        {
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

        private void OnEnable()
        {
            WaterTransitionEvents.Register(this);
        }

        private void OnDisable()
        {
            WaterTransitionEvents.Unregister(this);
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
