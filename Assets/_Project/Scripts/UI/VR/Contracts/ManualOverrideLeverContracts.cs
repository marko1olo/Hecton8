namespace Hecton8.UI.VR.Contracts
{
    /// <summary>
    /// Allocation-free read model exposed by the OpenXR manual override lever to UI, haptics, and cinematic consumers.
    /// </summary>
    public interface IManualOverrideLeverReadModel
    {
        /// <summary>Current lever angle in local degrees.</summary>
        float AngleDegrees { get; }

        /// <summary>Normalized lever travel in the [0, 1] range.</summary>
        float Normalized01 { get; }

        /// <summary>Current spring velocity in degrees per second.</summary>
        float VelocityDegreesPerSecond { get; }

        /// <summary>True while a physical or fallback input owns the lever target.</summary>
        bool IsGrabbed { get; }

        /// <summary>True after the manual override has crossed its latch threshold.</summary>
        bool IsLatched { get; }

        /// <summary>Dispatcher execution phase for consumers that validate read timing.</summary>
        byte ExecutionPhase { get; }
    }

    /// <summary>
    /// Stable constants shared by the manual override lever runtime and external consumers.
    /// </summary>
    public static class ManualOverrideLeverContractConstants
    {
        /// <summary>Simulation-lane execution phase identifier.</summary>
        public const byte ExecutionPhaseSimulation = 2;
    }
}
