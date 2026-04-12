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

        private bool ShouldRunScatterBackendShadowPass()
        {
            return RefreshScatterBackendPlan().RunsShadowPass;
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
            ScatterBackendRuntimeStatus status = _scatterBackendHost.GetStatus();
            _debugScatterBackendExecutionMode = status.ResolvedExecutionModeLabel;
            _debugScatterBackendResolutionReason = status.ResolutionReason;

            if (!status.HasFacade)
            {
                _debugScatterBackendKind = status.ActiveBackendKindLabel;
                _debugScatterBackendShadowPending = false;
                return;
            }

            _debugScatterBackendKind = status.ActiveBackendKindLabel;
            _debugScatterBackendShadowPending = status.IsJobActive;
        }

        private void DisposeScatterBackendFacade()
        {
            if (_scatterBackendHost != null)
            {
                _scatterBackendHost.Dispose();
                _scatterBackendHost = null;
            }

            _scatterBackendSupportContext = null;

            _debugScatterBackendExecutionMode = ScatterHybridRuntimeEntryPoint.GetExecutionModeLabel(ResolveScatterBackendRequestedExecutionMode());
            _debugScatterBackendKind = ScatterHybridRuntimeEntryPoint.GetBackendKindLabel(ScatterSimulationBackendKind.ClassicJobs);
            _debugScatterBackendResolutionReason = "backend-facade-disposed";
            _debugScatterBackendShadowPending = false;
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

        private bool TryResolveScatterBackendPrefab(int familyIndex, int layerIndex, out GameObject prefab)
        {
            prefab = null;
            EnsureScatterBackendSupportContext();
            return _scatterBackendSupportContext.BindingBridge.TryResolvePrefab(_scatterBackendHost, familyIndex, layerIndex, out prefab);
        }

        private bool TryScheduleScatterBackendFacadePass(
            Vector3 observerPosition,
            int totalCells,
            int groundBudget,
            int clusterBudget,
            int structureStride,
            int spawnStride)
        {
            EnsureScatterBackendFacadeInitialized();
            if (_scatterBackendHost == null)
                return false;

            ScatterBackendScheduleRequest request = _scatterBackendSupportContext.RequestFactory.Create(
                observerPosition,
                totalCells,
                groundBudget,
                clusterBudget,
                structureStride,
                spawnStride);

            return _scatterBackendHost.TrySchedule(request, _memory);
        }

        private bool TryCompleteScatterBackendFacadePass()
        {
            EnsureScatterBackendSupportContext();
            return _scatterBackendHost != null
                && _scatterBackendHost.TryCompleteAndReconcile(_scatterBackendSupportContext.PrefabResolver);
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
                    _debugScatterBackendShadowPassesCompleted++;
                    _debugScatterBackendShadowLastCandidateCount = completion.CandidateCount;
                    _debugScatterBackendShadowLastClassicQueuedCandidates = completion.ClassicQueuedCandidateCount;
                    _debugScatterBackendShadowLastCandidateDelta = completion.CandidateDelta;
                    _debugScatterBackendShadowPending = completion.IsJobActive;
                }
            }
        }

        private void TryScheduleScatterBackendShadowPass(
            Vector3 observerPosition,
            int totalCells,
            int groundBudget,
            int clusterBudget,
            int structureStride,
            int spawnStride)
        {
            if (!ShouldRunScatterBackendShadowPass())
                return;

            using (_scatterBackendShadowScheduleProfilerMarker.Auto())
            {
                EnsureScatterBackendFacadeInitialized();
                if (_scatterBackendHost == null || !_scatterBackendHost.HasFacade || _scatterBackendHost.IsFacadeJobActive)
                {
                    _debugScatterBackendShadowPending = _scatterBackendHost != null && _scatterBackendHost.IsFacadeJobActive;
                    return;
                }

                if (TryScheduleScatterBackendFacadePass(
                    observerPosition,
                    totalCells,
                    groundBudget,
                    clusterBudget,
                    structureStride,
                    spawnStride))
                {
                    _scatterBackendHost.SetShadowPendingClassicQueuedCandidates(_debugQueuedCandidates);
                    _debugScatterBackendKind = _scatterBackendHost.ActiveBackendKindLabel;
                    _debugScatterBackendShadowPassesScheduled++;
                    _debugScatterBackendShadowPending = true;
                }
            }
        }

        private bool HasScatterBackendFacadePendingWork()
        {
            return _scatterBackendHost != null && _scatterBackendHost.HasPendingFacadeWork();
        }

    }
}
