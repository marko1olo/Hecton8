namespace Hecton8.World
{
    /// <summary>
    /// Resolved cold-path runtime plan for the scatter hybrid backend seam.
    /// Keeps rollout decisions explicit so the owner does not spread mode/fallback logic.
    /// </summary>
    internal readonly struct ScatterHybridRuntimePlan
    {
        public ScatterHybridRuntimePlan(
            ScatterSimulationBackendKind requestedBackendKind,
            ScatterSimulationBackendKind resolvedBackendKind,
            ScatterBackendExecutionMode requestedExecutionMode,
            ScatterBackendExecutionMode resolvedExecutionMode,
            string resolutionReason)
        {
            RequestedBackendKind = requestedBackendKind;
            ResolvedBackendKind = resolvedBackendKind;
            RequestedExecutionMode = requestedExecutionMode;
            ResolvedExecutionMode = resolvedExecutionMode;
            ResolutionReason = string.IsNullOrWhiteSpace(resolutionReason) ? "unspecified" : resolutionReason;
        }

        public ScatterSimulationBackendKind RequestedBackendKind { get; }
        public ScatterSimulationBackendKind ResolvedBackendKind { get; }
        public ScatterBackendExecutionMode RequestedExecutionMode { get; }
        public ScatterBackendExecutionMode ResolvedExecutionMode { get; }
        public string ResolutionReason { get; }

        public bool RequiresFacade => ResolvedExecutionMode != ScatterBackendExecutionMode.Disabled;
        public bool RunsShadowPass => ResolvedExecutionMode == ScatterBackendExecutionMode.Shadow;
        public bool OwnsLivePlacements => ResolvedExecutionMode == ScatterBackendExecutionMode.ReservedLiveOwnership;
        public bool HasBackendFallback => RequestedBackendKind != ResolvedBackendKind;
        public bool HasExecutionModeFallback => RequestedExecutionMode != ResolvedExecutionMode;
    }

    /// <summary>
    /// Hybrid entry point for scatter backend rollout.
    /// The runtime owner remains the director; this resolver only decides how the backend seam is allowed to run.
    /// </summary>
    internal static class ScatterHybridRuntimeEntryPoint
    {
        public static ScatterBackendExecutionMode ResolveRequestedExecutionMode(
            ScatterBackendExecutionMode requestedExecutionMode,
            bool enableLegacyShadowPass)
        {
            if (requestedExecutionMode != ScatterBackendExecutionMode.Disabled)
                return requestedExecutionMode;

            return enableLegacyShadowPass
                ? ScatterBackendExecutionMode.Shadow
                : ScatterBackendExecutionMode.Disabled;
        }

        public static ScatterHybridRuntimePlan Resolve(
            bool isPlaying,
            ScatterSimulationBackendKind requestedBackendKind,
            ScatterBackendExecutionMode requestedExecutionMode)
        {
            if (!isPlaying)
            {
                return new ScatterHybridRuntimePlan(
                    requestedBackendKind,
                    ScatterSimulationBackendKind.ClassicJobs,
                    requestedExecutionMode,
                    ScatterBackendExecutionMode.Disabled,
                    "editor-preview-classic-owner");
            }

            switch (requestedExecutionMode)
            {
                case ScatterBackendExecutionMode.Disabled:
                    return new ScatterHybridRuntimePlan(
                        requestedBackendKind,
                        ResolveBackendKind(requestedBackendKind),
                        requestedExecutionMode,
                        ScatterBackendExecutionMode.Disabled,
                        "backend-rollout-disabled");

                case ScatterBackendExecutionMode.Shadow:
                    return new ScatterHybridRuntimePlan(
                        requestedBackendKind,
                        ResolveBackendKind(requestedBackendKind),
                        requestedExecutionMode,
                        ScatterBackendExecutionMode.Shadow,
                        ResolveShadowReason(requestedBackendKind));

                case ScatterBackendExecutionMode.ReservedLiveOwnership:
                    return new ScatterHybridRuntimePlan(
                        requestedBackendKind,
                        ResolveBackendKind(requestedBackendKind),
                        requestedExecutionMode,
                        ScatterBackendExecutionMode.Shadow,
                        "live-ownership-not-enabled-fallback-shadow");

                default:
                    return new ScatterHybridRuntimePlan(
                        requestedBackendKind,
                        ScatterSimulationBackendKind.ClassicJobs,
                        requestedExecutionMode,
                        ScatterBackendExecutionMode.Disabled,
                        "unknown-rollout-mode-disabled");
            }
        }

        public static ScatterRuntimeBackendFacade CreateFacade(in ScatterHybridRuntimePlan plan)
        {
            if (!plan.RequiresFacade)
                return null;

            // COLD ALLOC: ScatterRuntimeBackendFacade[1] - hybrid scatter backend seam owner - owner: ScatterHybridRuntimeEntryPoint
            return new ScatterRuntimeBackendFacade(plan.ResolvedBackendKind);
        }

        public static ScatterRuntimeBackendFacade SyncFacadeForPlan(
            ScatterRuntimeBackendFacade currentFacade,
            in ScatterHybridRuntimePlan plan,
            out bool resetPendingState)
        {
            resetPendingState = false;

            if (!plan.RequiresFacade)
            {
                if (currentFacade != null)
                {
                    currentFacade.Dispose();
                    resetPendingState = true;
                }

                return null;
            }

            if (currentFacade != null && ShouldReplaceFacadeForPlan(currentFacade, in plan))
            {
                currentFacade.Dispose();
                currentFacade = null;
                resetPendingState = true;
            }

            if (currentFacade == null)
            {
                currentFacade = CreateFacade(plan);
                resetPendingState = true;
            }

            currentFacade?.Initialize();
            return currentFacade;
        }

        private static bool ShouldReplaceFacadeForPlan(
            ScatterRuntimeBackendFacade currentFacade,
            in ScatterHybridRuntimePlan plan)
        {
            if (currentFacade.RequestedBackendKind != plan.ResolvedBackendKind)
                return true;

            return plan.ResolvedBackendKind == ScatterSimulationBackendKind.EntitiesDots &&
                currentFacade.BackendProviderVersion != ScatterSimulationBackendRegistry.Version;
        }

        public static string GetBackendKindLabel(ScatterSimulationBackendKind backendKind)
        {
            switch (backendKind)
            {
                case ScatterSimulationBackendKind.EntitiesDots:
                    return "EntitiesDots";
                case ScatterSimulationBackendKind.ClassicJobs:
                default:
                    return "ClassicJobs";
            }
        }

        public static string GetExecutionModeLabel(ScatterBackendExecutionMode executionMode)
        {
            switch (executionMode)
            {
                case ScatterBackendExecutionMode.Shadow:
                    return "Shadow";
                case ScatterBackendExecutionMode.ReservedLiveOwnership:
                    return "ReservedLiveOwnership";
                case ScatterBackendExecutionMode.Disabled:
                default:
                    return "Disabled";
            }
        }

        private static ScatterSimulationBackendKind ResolveBackendKind(ScatterSimulationBackendKind requestedBackendKind)
        {
            switch (requestedBackendKind)
            {
                case ScatterSimulationBackendKind.EntitiesDots:
                    return ScatterSimulationBackendKind.EntitiesDots;
                case ScatterSimulationBackendKind.ClassicJobs:
                default:
                    return ScatterSimulationBackendKind.ClassicJobs;
            }
        }

        private static string ResolveShadowReason(ScatterSimulationBackendKind requestedBackendKind)
        {
            switch (requestedBackendKind)
            {
                case ScatterSimulationBackendKind.EntitiesDots:
                    return "entities-shadow-rollout";
                case ScatterSimulationBackendKind.ClassicJobs:
                default:
                    return "classic-shadow-rollout";
            }
        }
    }
}
