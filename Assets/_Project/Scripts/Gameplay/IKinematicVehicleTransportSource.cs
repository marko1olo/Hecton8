namespace Hecton8.Gameplay
{
    /// <summary>
    /// Marker contract for external transports that move themselves kinematically instead of injecting propulsion into player swim physics.
    /// </summary>
    internal interface IKinematicVehicleTransportSource : IPlayerTransportSource
    {
        /// <summary>
        /// True while this transport owns authoritative vehicle motion and the rider should be treated as a passenger.
        /// </summary>
        bool IsVehicleMotionAuthoritative { get; }
    }
}
