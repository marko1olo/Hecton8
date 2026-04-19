using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Runtime lifecycle contract for transports that can be damaged and recharged in the world.
    /// </summary>
    /// <remarks>
    /// This owner complements propulsion/feel contracts.
    /// It exists so collision, docking, charging, and break-state logic can target a real transport runtime owner.
    /// </remarks>
    public interface IPlayerTransportLifecycleOwner
    {
        /// <summary>True when this transport is currently allowed to receive station charge.</summary>
        bool CanReceiveTransportCharge { get; }

        /// <summary>True when this transport has failed and can no longer provide propulsion.</summary>
        bool IsTransportBroken { get; }

        /// <summary>Current normalized transport charge state.</summary>
        float TransportChargeNormalized { get; }

        /// <summary>Current normalized transport integrity state.</summary>
        float TransportIntegrityNormalized { get; }

        /// <summary>
        /// Recharges the transport by a normalized amount.
        /// </summary>
        /// <param name="normalizedChargeDelta">Normalized charge delta to add.</param>
        void RechargeTransport(float normalizedChargeDelta);

        /// <summary>
        /// Applies collision impact damage to the transport.
        /// </summary>
        /// <param name="impactSpeed">Collision speed in meters per second.</param>
        /// <param name="hitPoint">World hit point.</param>
        /// <param name="hitNormal">World hit normal.</param>
        void ApplyTransportCollisionImpact(float impactSpeed, Vector3 hitPoint, Vector3 hitNormal);
    }
}
