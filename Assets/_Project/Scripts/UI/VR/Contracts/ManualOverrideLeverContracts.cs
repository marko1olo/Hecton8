namespace Hecton8.UI.VR.Contracts
{
    public interface IManualOverrideLeverReadModel
    {
        float AngleDegrees { get; }
        float Normalized01 { get; }
        float VelocityDegreesPerSecond { get; }
        bool IsGrabbed { get; }
        bool IsLatched { get; }
        byte ExecutionPhase { get; }
    }

    public static class ManualOverrideLeverContractConstants
    {
        public const byte ExecutionPhaseSimulation = 2;
    }
}
