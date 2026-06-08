using System;
using UnityEngine;
using ScatterWorkingMemory = Hecton8.World.WorldProceduralScatterDirector.ScatterWorkingMemory;

namespace Hecton8.World
{
    /// <summary>
    /// Owner-local runtime host for the scatter hybrid backend seam.
    /// Holds plan, facade, binding state, and shadow-pass bookkeeping without changing scene ownership.
    /// </summary>
    internal sealed class ScatterBackendRuntimeHost : IDisposable
    {
        private ScatterRuntimeBackendFacade _facade;
        private ScatterBackendBindingState _bindingState;
        private ScatterHybridRuntimePlan _plan;
        private ScatterSimulationBackendKind _resolvedBackendKind = ScatterSimulationBackendKind.ClassicJobs;
        private ScatterBackendParityReference _shadowPendingClassicParity;
        private string _lastScheduleFailureReason;
        private string _lastCompletionFailureReason;
        private int _interruptedShadowPassCount;

        public ScatterBackendBindingState BindingState => _bindingState;
        public bool HasFacade => _facade != null;
        public ScatterSimulationBackendKind ActiveBackendKind => _facade != null ? _facade.BackendKind : _resolvedBackendKind;
        public string ActiveBackendKindLabel => ScatterHybridRuntimeEntryPoint.GetBackendKindLabel(ActiveBackendKind);
        public string ResolvedExecutionModeLabel => ScatterHybridRuntimeEntryPoint.GetExecutionModeLabel(_plan.ResolvedExecutionMode);
        public string ResolutionReason => string.IsNullOrWhiteSpace(_plan.ResolutionReason) ? "backend-host-uninitialized" : _plan.ResolutionReason;
        public bool IsFacadeJobActive => _facade != null && _facade.IsJobActive;
        public bool IsFacadeJobCompleted => _facade != null && _facade.IsJobCompleted;

        public ScatterBackendRuntimeStatus GetStatus()
        {
            string resolutionReason = ResolutionReason;
            if (_facade != null && _facade.BackendKind != _plan.ResolvedBackendKind)
                resolutionReason = $"{resolutionReason}|provider-fallback-{ScatterHybridRuntimeEntryPoint.GetBackendKindLabel(_facade.BackendKind)}";

            resolutionReason = AppendScheduleFailureReason(resolutionReason);
            resolutionReason = AppendCompletionFailureReason(resolutionReason);

            return new ScatterBackendRuntimeStatus(
                ActiveBackendKind,
                ActiveBackendKindLabel,
                _plan.ResolvedExecutionMode,
                ResolvedExecutionModeLabel,
                resolutionReason,
                HasFacade,
                IsFacadeJobActive,
                IsFacadeJobCompleted,
                _interruptedShadowPassCount);
        }

        public ScatterHybridRuntimePlan RefreshPlan(
            bool isPlaying,
            ScatterSimulationBackendKind requestedBackendKind,
            ScatterBackendExecutionMode requestedExecutionMode)
        {
            _plan = ScatterHybridRuntimeEntryPoint.Resolve(isPlaying, requestedBackendKind, requestedExecutionMode);
            _resolvedBackendKind = _plan.ResolvedBackendKind;
            return _plan;
        }

        public bool SyncFacade()
        {
            bool hadActiveFacadeJob = IsFacadeJobActive;
            _facade = ScatterHybridRuntimeEntryPoint.SyncFacadeForPlan(
                _facade,
                _plan,
                out bool resetPendingState);

            if (_facade == null)
            {
                DisposeBindingState();
                if (!_plan.RequiresFacade)
                {
                    ClearScheduleFailureReason();
                    ClearCompletionFailureReason();
                }
            }

            if (resetPendingState)
            {
                _shadowPendingClassicParity = default;
                _bindingState?.ClearCellDataViews();
                ClearScheduleFailureReason();
                ClearCompletionFailureReason();
                if (hadActiveFacadeJob)
                    _interruptedShadowPassCount++;
            }

            return resetPendingState;
        }

        public ScatterBackendBindingState EnsureBindingState()
        {
            if (_bindingState != null)
                return _bindingState;

            // COLD ALLOC: ScatterBackendBindingState[1] - scatter backend binding cache and height bridge - owner: ScatterBackendRuntimeHost
            _bindingState = new ScatterBackendBindingState();
            return _bindingState;
        }

        public void ResetBindingLookup()
        {
            _bindingState?.ResetLookup();
        }

        public void ResetTelemetry()
        {
            _interruptedShadowPassCount = 0;
            ClearScheduleFailureReason();
            ClearCompletionFailureReason();
        }

        public bool TrySchedule(in ScatterBackendScheduleRequest request, ScatterWorkingMemory memory)
        {
            if (_facade == null)
            {
                MarkScheduleFailure("facade-unavailable");
                return false;
            }

            if (!_facade.IsInitialized)
            {
                MarkScheduleFailure("backend-not-initialized");
                return false;
            }

            if (_bindingState == null)
            {
                MarkScheduleFailure("binding-state-unavailable");
                return false;
            }

            if (!_bindingState.TryPopulateCellData(memory, request.TotalCells))
            {
                MarkScheduleFailure("cell-data-unavailable");
                return false;
            }

            if (!_facade.TrySchedule(BuildConfig(request), _bindingState.HeightSamples, _bindingState.CellStates))
            {
                _bindingState.ClearCellDataViews();
                MarkScheduleFailure(_facade.IsJobActive ? "facade-busy" : "backend-schedule-rejected");
                return false;
            }

            _bindingState.ClearCellDataViews();
            _shadowPendingClassicParity = request.ParityReference;
            ClearScheduleFailureReason();
            ClearCompletionFailureReason();
            return true;
        }

        public bool TryCompleteShadowPass(out ScatterBackendShadowCompletion completion)
        {
            completion = default;
            if (_facade == null || !_facade.IsJobCompleted)
                return false;

            if (!_facade.TryCompleteSimulation(out ScatterSimulationResult result))
            {
                _facade.Dispose();
                _facade = null;
                _shadowPendingClassicParity = default;
                _bindingState?.ClearCellDataViews();
                MarkCompletionFailure("backend-complete-failed");
                return false;
            }

            completion = new ScatterBackendShadowCompletion(
                result.ParitySnapshot,
                _shadowPendingClassicParity,
                _facade.IsJobActive);

            _shadowPendingClassicParity = default;
            _bindingState?.ClearCellDataViews();
            ClearScheduleFailureReason();
            ClearCompletionFailureReason();
            return true;
        }

        public void Dispose()
        {
            if (_facade != null)
            {
                _facade.Dispose();
                _facade = null;
            }

            DisposeBindingState();
            _plan = default;
            _resolvedBackendKind = ScatterSimulationBackendKind.ClassicJobs;
            _shadowPendingClassicParity = default;
            _interruptedShadowPassCount = 0;
            ClearScheduleFailureReason();
            ClearCompletionFailureReason();
        }

        private void DisposeBindingState()
        {
            if (_bindingState == null)
                return;

            _bindingState.Dispose();
            _bindingState = null;
        }

        private void MarkScheduleFailure(string reason)
        {
            _lastScheduleFailureReason = string.IsNullOrWhiteSpace(reason) ? "unknown" : reason;
            ClearCompletionFailureReason();
        }

        private void ClearScheduleFailureReason()
        {
            _lastScheduleFailureReason = null;
        }

        private void MarkCompletionFailure(string reason)
        {
            _lastCompletionFailureReason = string.IsNullOrWhiteSpace(reason) ? "unknown" : reason;
        }

        private void ClearCompletionFailureReason()
        {
            _lastCompletionFailureReason = null;
        }

        private string AppendScheduleFailureReason(string resolutionReason)
        {
            if (string.IsNullOrWhiteSpace(_lastScheduleFailureReason))
                return resolutionReason;

            return $"{resolutionReason}|schedule-failed-{_lastScheduleFailureReason}";
        }

        private string AppendCompletionFailureReason(string resolutionReason)
        {
            if (string.IsNullOrWhiteSpace(_lastCompletionFailureReason))
                return resolutionReason;

            return $"{resolutionReason}|completion-failed-{_lastCompletionFailureReason}";
        }

        private ScatterSimulationConfig BuildConfig(in ScatterBackendScheduleRequest request)
        {
            ScatterSimulationQuotaState quotaState = new ScatterSimulationQuotaState
            {
                Ground = new ScatterSimulationLayerQuota
                {
                    PlacementsPerCell = Mathf.Max(0, request.GroundBudget),
                    CellStride = 1,
                    FamilyIndex = _bindingState != null ? _bindingState.GroundFamilyIndex : -1
                },
                Cluster = new ScatterSimulationLayerQuota
                {
                    PlacementsPerCell = Mathf.Max(0, request.ClusterBudget),
                    CellStride = 1,
                    FamilyIndex = _bindingState != null ? _bindingState.ClusterFamilyIndex : -1
                },
                Structure = new ScatterSimulationLayerQuota
                {
                    PlacementsPerCell = 1,
                    CellStride = Mathf.Max(1, request.StructureStride),
                    FamilyIndex = _bindingState != null ? _bindingState.StructureFamilyIndex : -1
                },
                Spawn = new ScatterSimulationLayerQuota
                {
                    PlacementsPerCell = 1,
                    CellStride = Mathf.Max(1, request.SpawnStride),
                    FamilyIndex = _bindingState != null ? _bindingState.SpawnFamilyIndex : -1
                }
            };

            return new ScatterSimulationConfig
            {
                CellSize = Mathf.Max(6f, request.CellSize),
                RadiusCells = Mathf.Max(2, request.RadiusCells),
                PlayerPosition = request.ObserverPosition,
                QuotaState = quotaState,
                DefaultEligibility = request.EligibilityMask,
                DefaultSuppressionState = request.DefaultSuppressionState,
                DirtyFlags = request.DirtyFlags,
                GroundPlacementsPerCell = quotaState.Ground.PlacementsPerCell,
                ClusterPlacementsPerCell = quotaState.Cluster.PlacementsPerCell,
                StructureCellStride = quotaState.Structure.CellStride,
                SpawnCellStride = quotaState.Spawn.CellStride,
                GroundFamilyIndex = quotaState.Ground.FamilyIndex,
                ClusterFamilyIndex = quotaState.Cluster.FamilyIndex,
                StructureFamilyIndex = quotaState.Structure.FamilyIndex,
                SpawnFamilyIndex = quotaState.Spawn.FamilyIndex,
                SurfaceYOffset = request.SurfaceYOffset,
                Seed = request.Seed
            };
        }
    }
}
