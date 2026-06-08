namespace Hecton8.World
{
    /// <summary>
    /// Read-only runtime status snapshot for the scatter hybrid backend host.
    /// Lets the director consume one typed status instead of reading host state field-by-field.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal readonly struct ScatterBackendRuntimeStatus
    {
        public readonly ScatterSimulationBackendKind ActiveBackendKind;
        public readonly string ActiveBackendKindLabel;
        public readonly ScatterBackendExecutionMode ResolvedExecutionMode;
        public readonly string ResolvedExecutionModeLabel;
        public readonly string ResolutionReason;
        public readonly byte HasFacade;
        public readonly byte IsJobActive;
        public readonly byte IsJobCompleted;
        public readonly int InterruptedShadowPassCount;

        public ScatterBackendRuntimeStatus(
            ScatterSimulationBackendKind activeBackendKind,
            string activeBackendKindLabel,
            ScatterBackendExecutionMode resolvedExecutionMode,
            string resolvedExecutionModeLabel,
            string resolutionReason,
            bool hasFacade,
            bool isJobActive,
            bool isJobCompleted,
            int interruptedShadowPassCount)
        {
            ActiveBackendKind = activeBackendKind;
            ActiveBackendKindLabel = activeBackendKindLabel;
            ResolvedExecutionMode = resolvedExecutionMode;
            ResolvedExecutionModeLabel = resolvedExecutionModeLabel;
            ResolutionReason = resolutionReason;
            HasFacade = hasFacade ? (byte)1 : (byte)0;
            IsJobActive = isJobActive ? (byte)1 : (byte)0;
            IsJobCompleted = isJobCompleted ? (byte)1 : (byte)0;
            InterruptedShadowPassCount = interruptedShadowPassCount < 0 ? 0 : interruptedShadowPassCount;
        }
    }
}
