using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Immutable payload delivered to objects that want custom reactions to a heavy-tow cable snap.
    /// </summary>
    public readonly struct TowSnapEventData
    {
        /// <summary>
        /// Creates a new tow snap event payload.
        /// </summary>
        public TowSnapEventData(Rigidbody payloadBody, Vector3 lineDirection, Vector3 velocityChange, Vector3 torqueVelocityChange, float severity)
        {
            PayloadBody = payloadBody;
            LineDirection = lineDirection;
            VelocityChange = velocityChange;
            TorqueVelocityChange = torqueVelocityChange;
            Severity = severity;
        }

        /// <summary>
        /// Payload rigidbody that was attached when the cable snapped.
        /// </summary>
        public Rigidbody PayloadBody { get; }

        /// <summary>
        /// Direction from tow origin toward the payload at the instant of snap.
        /// </summary>
        public Vector3 LineDirection { get; }

        /// <summary>
        /// Velocity-change impulse already applied to the payload.
        /// </summary>
        public Vector3 VelocityChange { get; }

        /// <summary>
        /// Angular velocity-change already applied to the payload.
        /// </summary>
        public Vector3 TorqueVelocityChange { get; }

        /// <summary>
        /// Normalized authored severity of the cable snap.
        /// </summary>
        public float Severity { get; }
    }

    /// <summary>
    /// Optional payload reaction hook for heavy-tow cable snaps.
    /// Explosive salvage can detonate here after the snap jerk is applied.
    /// </summary>
    public interface ITowSnapReceiver
    {
        /// <summary>
        /// Invoked when a heavy-tow cable snaps while this object is the payload.
        /// </summary>
        void HandleTowCableSnap(TowSnapEventData eventData);
    }
}
