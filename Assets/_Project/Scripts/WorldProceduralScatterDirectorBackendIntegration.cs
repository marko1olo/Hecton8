using Unity.Profiling;
using UnityEngine;

namespace Hecton8.World
{
    public sealed partial class WorldProceduralScatterDirector
    {
        private static readonly ProfilerMarker _scatterBackendShadowScheduleProfilerMarker = new("WorldScatter.Backend.Shadow.Schedule");
        private static readonly ProfilerMarker _scatterBackendShadowPumpProfilerMarker = new("WorldScatter.Backend.Shadow.Pump");

        private ScatterBackendSupportContext _scatterBackendSupportContext;
        private ScatterBackendRuntimeHost _scatterBackendHost;

        private ScatterBackendExecutionMode ResolveScatterBackendRequestedExecutionMode()
        {
            return ScatterHybridRuntimeEntryPoint.ResolveRequestedExecutionMode(
                scatterBackendRequestedExecutionMode,
                enableScatterBackendShadowPass);
        }

        private ScatterSimulationBackendKind ResolveRequestedScatterBackendKind()
        {
            return scatterBackendRequestedKind;
        }

        private ScatterHybridRuntimePlan RefreshScatterBackendPlan()
        {
            EnsureScatterBackendHost();
            return _scatterBackendHost.RefreshPlan(
                Application.isPlaying,
                ResolveRequestedScatterBackendKind(),
                ResolveScatterBackendRequestedExecutionMode());
        }

        private void EnsureScatterBackendHost()
        {
            if (_scatterBackendHost != null)
                return;

            // COLD ALLOC: ScatterBackendRuntimeHost[1] - scatter hybrid backend runtime host - owner: WorldProceduralScatterDirector
            _scatterBackendHost = new ScatterBackendRuntimeHost();
        }

        private void EnsureScatterBackendSupportContext()
        {
            if (_scatterBackendSupportContext != null)
                return;

            // COLD ALLOC: ScatterBackendSupportContext[1] - scatter backend support bundle - owner: WorldProceduralScatterDirector
            _scatterBackendSupportContext = new ScatterBackendSupportContext(this);
        }

        private void EnsureScatterBackendFacadeInitialized()
        {
            RefreshScatterBackendPlan();
            EnsureScatterBackendSupportContext();
            _scatterBackendHost.SyncFacade();
            ApplyScatterBackendRuntimeStatus(_scatterBackendHost.GetStatus());
        }

        private void DisposeScatterBackendFacade()
        {
            if (_scatterBackendHost != null)
            {
                _scatterBackendHost.Dispose();
                _scatterBackendHost = null;
            }

            _scatterBackendSupportContext = null;
            ResetScatterBackendDebugStatus();
        }

        private void RebuildScatterBackendLookup()
        {
            ScatterHybridRuntimePlan plan = RefreshScatterBackendPlan();
            if (!plan.RequiresFacade)
            {
                _scatterBackendHost?.ResetBindingLookup();
                return;
            }

            EnsureScatterBackendFacadeInitialized();
            _scatterBackendSupportContext.BindingBridge.RebuildLookup(_scatterBackendHost, _runtimeRuleBuffer);
        }

        private void PumpScatterBackendShadowPass()
        {
            using (_scatterBackendShadowPumpProfilerMarker.Auto())
            {
                if (_scatterBackendHost == null || !_scatterBackendHost.HasFacade)
                {
                    _debugScatterBackendShadowPending = false;
                    return;
                }

                _debugScatterBackendShadowPending = _scatterBackendHost.IsFacadeJobActive;
                if (!_scatterBackendHost.IsFacadeJobCompleted)
                    return;

                if (_scatterBackendHost.TryCompleteShadowPass(out ScatterBackendShadowCompletion completion))
                {
                    ApplyScatterBackendShadowCompletion(completion);
                }
            }
        }

        private void TryScheduleScatterBackendShadowPass(
            in ScatterBackendShadowScheduleContext context)
        {
            using (_scatterBackendShadowScheduleProfilerMarker.Auto())
            {
                if (!TryPrepareScatterBackendShadowScheduling())
                    return;

                ScatterBackendScheduleRequest request = _scatterBackendSupportContext.RequestFactory.Create(context);
                if (_scatterBackendHost.TrySchedule(request, _memory))
                {
                    _scatterBackendHost.SetShadowPendingClassicParity(context.ClassicParityReference);
                    _debugScatterBackendKind = _scatterBackendHost.ActiveBackendKindLabel;
                    _debugScatterBackendShadowPassesScheduled++;
                    _debugScatterBackendShadowPending = true;
                }
            }
        }

        private bool TryPrepareScatterBackendShadowScheduling()
        {
            ScatterHybridRuntimePlan plan = RefreshScatterBackendPlan();
            if (!plan.RunsShadowPass)
                return false;

            EnsureScatterBackendSupportContext();
            _scatterBackendHost.SyncFacade();
            ApplyScatterBackendRuntimeStatus(_scatterBackendHost.GetStatus());

            if (_scatterBackendHost == null || !_scatterBackendHost.HasFacade || _scatterBackendHost.IsFacadeJobActive)
            {
                _debugScatterBackendShadowPending = _scatterBackendHost != null && _scatterBackendHost.IsFacadeJobActive;
                return false;
            }

            return true;
        }

        private void ApplyScatterBackendRuntimeStatus(in ScatterBackendRuntimeStatus status)
        {
            _debugScatterBackendExecutionMode = status.ResolvedExecutionModeLabel;
            _debugScatterBackendKind = status.ActiveBackendKindLabel;
            _debugScatterBackendResolutionReason = status.ResolutionReason;
            _debugScatterBackendShadowPending = status.HasFacade && status.IsJobActive;
        }

        private void ApplyScatterBackendShadowCompletion(in ScatterBackendShadowCompletion completion)
        {
            _debugScatterBackendShadowPassesCompleted++;
            _debugScatterBackendShadowLastCandidateCount = completion.CandidateCount;
            _debugScatterBackendShadowLastClassicQueuedCandidates = completion.ClassicQueuedCandidateCount;
            _debugScatterBackendShadowLastCandidateDelta = completion.CandidateDelta;
            _debugScatterBackendShadowLastGroundDelta = completion.GroundDelta;
            _debugScatterBackendShadowLastClusterDelta = completion.ClusterDelta;
            _debugScatterBackendShadowLastStructureDelta = completion.StructureDelta;
            _debugScatterBackendShadowLastSpawnDelta = completion.SpawnDelta;
            _debugScatterBackendShadowLastChecksumMatch = completion.CandidateChecksumMatch;
            _debugScatterBackendShadowLastParityStatus = completion.ParityStatusLabel;
            if (!completion.HasParityMatch)
                _debugScatterBackendShadowParityMismatchCount++;
            _debugScatterBackendShadowPending = completion.IsJobActive;
        }

        private void ResetScatterBackendDebugStatus()
        {
            _debugScatterBackendExecutionMode = ScatterHybridRuntimeEntryPoint.GetExecutionModeLabel(ResolveScatterBackendRequestedExecutionMode());
            _debugScatterBackendKind = ScatterHybridRuntimeEntryPoint.GetBackendKindLabel(ScatterSimulationBackendKind.ClassicJobs);
            _debugScatterBackendResolutionReason = "backend-facade-disposed";
            _debugScatterBackendShadowPending = false;
        }

        private void ResetScatterBackendDebugTelemetry(in ScatterHybridRuntimePlan plan)
        {
            _debugScatterBackendExecutionMode = ScatterHybridRuntimeEntryPoint.GetExecutionModeLabel(plan.ResolvedExecutionMode);
            _debugScatterBackendKind = ScatterHybridRuntimeEntryPoint.GetBackendKindLabel(plan.ResolvedBackendKind);
            _debugScatterBackendResolutionReason = plan.ResolutionReason;
            _debugScatterBackendShadowPending = false;
            _debugScatterBackendShadowPassesScheduled = 0;
            _debugScatterBackendShadowPassesCompleted = 0;
            _debugScatterBackendShadowLastCandidateCount = 0;
            _debugScatterBackendShadowLastClassicQueuedCandidates = 0;
            _debugScatterBackendShadowLastCandidateDelta = 0;
            _debugScatterBackendShadowLastGroundDelta = 0;
            _debugScatterBackendShadowLastClusterDelta = 0;
            _debugScatterBackendShadowLastStructureDelta = 0;
            _debugScatterBackendShadowLastSpawnDelta = 0;
            _debugScatterBackendShadowLastChecksumMatch = false;
            _debugScatterBackendShadowLastParityStatus = "NotRun";
            _debugScatterBackendShadowParityMismatchCount = 0;
        }

    }
}
