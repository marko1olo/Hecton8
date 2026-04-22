using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Minimal motor contract used by external force producers.
    /// </summary>
    public interface IMotorForces
    {
        /// <summary>Authoritative rigidbody driven by the locomotion motor.</summary>
        Rigidbody Body { get; }

        /// <summary>Authoritative grounding capsule used by the locomotion motor.</summary>
        CapsuleCollider Capsule { get; }

        /// <summary>True while the motor currently considers itself grounded.</summary>
        bool IsGrounded { get; }

        /// <summary>Queues a world-space acceleration for the next motor force flush.</summary>
        /// <param name="acceleration">World-space acceleration in m/s^2.</param>
        void AddExternalAcceleration(Vector3 acceleration);

        /// <summary>Queues a world-space velocity change for the next motor force flush.</summary>
        /// <param name="velocityChange">World-space velocity delta in m/s.</param>
        void AddExternalVelocityChange(Vector3 velocityChange);
    }
}
