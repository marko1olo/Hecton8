namespace Hecton8.World
{
    /// <summary>
    /// Read-only runtime status snapshot for the scatter hybrid backend host.
    /// Lets the director consume one typed status instead of reading host state field-by-field.
    /// </summary>
    internal readonly struct ScatterBackendRuntimeStatus
    {
        public ScatterBackendRuntimeStatus(
            ScatterSimulationBackendKind activeBackendKind,
            string activeBackendKindLabel,
            ScatterBackendExecutionMode resolvedExecutionMode,
            string resolvedExecutionModeLabel,
            string resolutionReason,
            bool hasFacade,
            bool isJobActive,
            bool isJobCompleted)
        {
            ActiveBackendKind = activeBackendKind;
            ActiveBackendKindLabel = activeBackendKindLabel;
            ResolvedExecutionMode = resolvedExecutionMode;
            ResolvedExecutionModeLabel = resolvedExecutionModeLabel;
            ResolutionReason = resolutionReason;
            HasFacade = hasFacade;
            IsJobActive = isJobActive;
            IsJobCompleted = isJobCompleted;
        }

        public ScatterSimulationBackendKind ActiveBackendKind { get; }
        public string ActiveBackendKindLabel { get; }
        public ScatterBackendExecutionMode ResolvedExecutionMode { get; }
        public string ResolvedExecutionModeLabel { get; }
        public string ResolutionReason { get; }
        public bool HasFacade { get; }
        public bool IsJobActive { get; }
        public bool IsJobCompleted { get; }
    }
}
