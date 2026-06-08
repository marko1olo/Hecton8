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
            ScatterHybridRuntimePlan plan = RefreshScatterBackendPlan();
            if (!plan.RequiresFacade)
            {
                _scatterBackendHost.SyncFacade();
                ApplyScatterBackendRuntimeStatus(_scatterBackendHost.GetStatus());
                return;
            }

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
                _scatterBackendHost.SyncFacade();
                ApplyScatterBackendRuntimeStatus(_scatterBackendHost.GetStatus());
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
                    return;
                }

                ApplyScatterBackendRuntimeStatus(_scatterBackendHost.GetStatus());
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
                if (!_scatterBackendHost.TrySchedule(request, _memory))
                {
                    ApplyScatterBackendRuntimeStatus(_scatterBackendHost.GetStatus());
                    return;
                }

                ApplyScatterBackendRuntimeStatus(_scatterBackendHost.GetStatus());
                _debugScatterBackendShadowPassesScheduled++;
                _debugScatterBackendShadowPending = true;
            }
        }

        private bool TryPrepareScatterBackendShadowScheduling()
        {
            ScatterHybridRuntimePlan plan = RefreshScatterBackendPlan();
            if (!plan.RequiresFacade)
            {
                _scatterBackendHost.SyncFacade();
                ApplyScatterBackendRuntimeStatus(_scatterBackendHost.GetStatus());
                return false;
            }

            if (!plan.RunsShadowPass)
            {
                ApplyScatterBackendRuntimeStatus(_scatterBackendHost.GetStatus());
                return false;
            }

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
            _debugScatterBackendShadowPending = status.HasFacade != 0 && status.IsJobActive != 0;
            _debugScatterBackendShadowInterruptedCount = status.InterruptedShadowPassCount;
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
            _debugScatterBackendShadowLastChecksumMatch = ScatterBackendShadowCompletion.CandidateChecksumMatches(in completion);
            _debugScatterBackendShadowLastParityStatus = ScatterBackendShadowCompletion.GetParityStatusLabel(completion.ParityStatusCode);
            if (!ScatterBackendShadowCompletion.HasParityMatch(in completion))
                _debugScatterBackendShadowParityMismatchCount++;
            _debugScatterBackendShadowPending = ScatterBackendShadowCompletion.IsJobActive(in completion);
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
            _debugScatterBackendShadowInterruptedCount = 0;
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
