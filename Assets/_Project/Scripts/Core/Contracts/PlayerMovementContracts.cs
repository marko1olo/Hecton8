using UnityEngine;

namespace Hecton8.Core.Contracts
{
    public interface IPlayerMovementPoseReadModel
    {
        Vector2 CurrentLocalWaveSlope { get; }

        bool TryGetRuntimePosition(out Vector3 position);
    }

    public interface IPlayerMovementForceSink
    {
        void QueueExternalAcceleration(Vector3 acceleration);

        void QueueExternalVelocityChange(Vector3 velocityChange);
    }

    public interface IPlayerMovementTraumaSink
    {
        void ApplyPhysicalTrauma(Vector3 impulse, float weight);

        void ForceTransportBailout(Vector3 worldImpulse, float severity);
    }

    public interface IPlayerMovementEnvironmentSink
    {
        void ApplyExternalThermalUpdraft(Vector3 velocityChange);

        void RequestExternalHullStress(float normalizedStress);

        void RequestLocalGravityOverride(Vector3 gravityVector, float holdSeconds);
    }

    public interface IPlayerMovementSonarEmitter
    {
        bool TriggerActiveSonarPing();
    }

    public interface IPlayerMovementContracts :
        IPlayerMovementPoseReadModel,
        IPlayerMovementForceSink,
        IPlayerMovementTraumaSink,
        IPlayerMovementEnvironmentSink,
        IPlayerMovementSonarEmitter
    {
    }
}
