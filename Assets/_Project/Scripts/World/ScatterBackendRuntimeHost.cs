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

            return new ScatterBackendRuntimeStatus(
                ActiveBackendKind,
                ActiveBackendKindLabel,
                _plan.ResolvedExecutionMode,
                ResolvedExecutionModeLabel,
                resolutionReason,
                HasFacade,
                IsFacadeJobActive,
                IsFacadeJobCompleted);
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
            _facade = ScatterHybridRuntimeEntryPoint.SyncFacadeForPlan(
                _facade,
                _plan,
                out bool resetPendingState);

            if (_facade == null)
            {
                DisposeBindingState();
            }

            if (resetPendingState)
                _shadowPendingClassicParity = default;

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

        public void SetShadowPendingClassicParity(in ScatterBackendParityReference parityReference)
        {
            _shadowPendingClassicParity = parityReference;
        }

        public bool TrySchedule(in ScatterBackendScheduleRequest request, ScatterWorkingMemory memory)
        {
            if (_facade == null || _bindingState == null || !_bindingState.TryPopulateCellData(memory, request.TotalCells))
                return false;

            return _facade.TrySchedule(BuildConfig(request), _bindingState.HeightSamples, _bindingState.CellStates);
        }

        public bool TryCompleteShadowPass(out ScatterBackendShadowCompletion completion)
        {
            completion = default;
            if (_facade == null || !_facade.IsJobCompleted || !_facade.TryCompleteSimulation(out ScatterSimulationResult result))
                return false;

            completion = new ScatterBackendShadowCompletion(
                result.ParitySnapshot,
                _shadowPendingClassicParity,
                _facade.IsJobActive);

            _shadowPendingClassicParity = default;
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
        }

        private void DisposeBindingState()
        {
            if (_bindingState == null)
                return;

            _bindingState.Dispose();
            _bindingState = null;
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
