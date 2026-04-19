namespace Hecton8.Gameplay
{
    /// <summary>
    /// Runtime transport source contract consumed by locomotion, presentation, audio, and AI.
    /// </summary>
    /// <remarks>
    /// Implement on any active tool or mounted transport that can drive the player with propulsion.
    /// Keep methods allocation-free and deterministic.
    /// </remarks>
    public interface IPlayerTransportSource
    {
        /// <summary>
        /// True while the transport is actively engaged and should influence gameplay feel.
        /// </summary>
        bool IsTransportActive { get; }

        /// <summary>
        /// Current propulsion force contributed by this transport.
        /// </summary>
        float GetTransportPropulsionForce();

        /// <summary>
        /// Current swim-speed multiplier contributed by this transport.
        /// </summary>
        float GetTransportSpeedMultiplier();

        /// <summary>
        /// Current normalized 0-1 feel boost used by presentation, audio, and AI.
        /// </summary>
        float GetTransportBoost01();
    }
}
