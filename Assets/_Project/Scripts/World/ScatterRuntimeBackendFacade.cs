using System;
using Unity.Collections;
using Unity.Profiling;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Orchestrates the active scatter simulation backend and main-thread placement reconciler.
    /// This is the seam that future DOTS backends plug into without changing scene ownership.
    /// </summary>
    internal sealed class ScatterRuntimeBackendFacade : IDisposable
    {
        private static readonly ProfilerMarker _backendScheduleProfilerMarker = new("WorldScatter.Backend.Schedule");
        private static readonly ProfilerMarker _backendCompleteProfilerMarker = new("WorldScatter.Backend.Complete");
        private static readonly ProfilerMarker _backendReconcileProfilerMarker = new("WorldScatter.Backend.Reconcile");

        private readonly IScatterSimulationBackend _simulationBackend;
        private readonly IScatterPlacementReconciler _placementReconciler;
        private bool _disposed;
        private bool _initialized;
        private int _lastCandidateCount;

        /// <summary>
        /// Creates a backend facade for the requested simulation backend kind.
        /// Unsupported kinds fall back to the classic Jobs path until a real implementation exists.
        /// </summary>
        /// <param name="backendKind">Requested simulation backend kind.</param>
        public ScatterRuntimeBackendFacade(ScatterSimulationBackendKind backendKind)
            : this(CreateSimulationBackend(backendKind), CreatePlacementReconciler())
        {
        }

        /// <summary>
        /// Creates a backend facade with explicit dependencies.
        /// Intended for controlled runtime composition and test injection.
        /// </summary>
        /// <param name="simulationBackend">Simulation backend implementation.</param>
        /// <param name="placementReconciler">Main-thread placement reconciler implementation.</param>
        public ScatterRuntimeBackendFacade(
            IScatterSimulationBackend simulationBackend,
            IScatterPlacementReconciler placementReconciler)
        {
            _simulationBackend = simulationBackend;
            _placementReconciler = placementReconciler;
        }

        /// <summary>Active simulation backend kind.</summary>
        public ScatterSimulationBackendKind BackendKind => _simulationBackend != null
            ? _simulationBackend.BackendKind
            : ScatterSimulationBackendKind.ClassicJobs;

        /// <summary>Whether the facade and underlying backends are ready for use.</summary>
        public bool IsInitialized => _initialized && !_disposed;

        /// <summary>Whether the active simulation backend currently has scheduled work.</summary>
        public bool IsJobActive => !_disposed && _simulationBackend != null && _simulationBackend.IsJobActive;
        public bool IsJobCompleted => !_disposed && _simulationBackend != null && _simulationBackend.IsJobCompleted;

        /// <summary>Candidate count returned by the last completed simulation pass.</summary>
        public int LastCandidateCount => _lastCandidateCount;

        /// <summary>Live placement count reported by the active reconciler.</summary>
        public int ActivePlacementCount => _placementReconciler != null ? _placementReconciler.ActivePlacementCount : 0;

        /// <summary>Spawn count from the last reconcile pass.</summary>
        public int LastSpawnCount => _placementReconciler != null ? _placementReconciler.LastSpawnCount : 0;

        /// <summary>Despawn count from the last reconcile pass.</summary>
        public int LastDespawnCount => _placementReconciler != null ? _placementReconciler.LastDespawnCount : 0;

        /// <summary>Retained placement count from the last reconcile pass.</summary>
        public int LastRetainedCount => _placementReconciler != null ? _placementReconciler.LastRetainedCount : 0;

        /// <summary>
        /// Initializes the active backend and reconciler. Safe to call multiple times.
        /// </summary>
        public void Initialize()
        {
            if (_disposed || _initialized)
                return;

            _simulationBackend?.Initialize();
            _initialized = true;
        }

        /// <summary>
        /// Schedules a scatter simulation pass on the active backend.
        /// </summary>
        /// <param name="config">Simulation config for the current pass.</param>
        /// <param name="heightSamples">Pre-sampled terrain heights.</param>
        /// <returns>True when scheduling succeeded; false when backend is unavailable or already busy.</returns>
        public bool TrySchedule(ScatterSimulationConfig config, NativeArray<float> heightSamples)
        {
            if (!IsInitialized || _simulationBackend == null)
                return false;

            using (_backendScheduleProfilerMarker.Auto())
            {
                return _simulationBackend.TrySchedule(config, heightSamples);
            }
        }

        /// <summary>
        /// Completes the active simulation pass and immediately reconciles live placements on the main thread.
        /// </summary>
        /// <param name="prefabResolver">Prefab resolver for candidate family/layer pairs.</param>
        /// <returns>True when a pending pass was completed and reconciled.</returns>
        public bool TryCompleteAndReconcile(IScatterPrefabResolver prefabResolver)
        {
            if (!IsInitialized || _simulationBackend == null || _placementReconciler == null)
                return false;

            if (!TryCompleteSimulation(out ScatterSimulationResult result))
                return false;

            using (_backendReconcileProfilerMarker.Auto())
            {
                _placementReconciler.Reconcile(result.Candidates, result.CandidateCount, prefabResolver);
            }

            return true;
        }

        /// <summary>
        /// Completes the active simulation pass without running placement reconciliation.
        /// Intended for shadow execution and backend parity instrumentation.
        /// </summary>
        public bool TryCompleteSimulation(out ScatterSimulationResult result)
        {
            result = default;
            if (!IsInitialized || _simulationBackend == null || !_simulationBackend.IsJobCompleted)
                return false;

            using (_backendCompleteProfilerMarker.Auto())
            {
                if (!_simulationBackend.TryComplete(out result))
                    return false;
            }

            _lastCandidateCount = result.CandidateCount;
            return true;
        }

        /// <summary>
        /// Force-completes any pending simulation work without running reconciliation.
        /// </summary>
        public void ForceComplete()
        {
            if (!IsInitialized || _simulationBackend == null)
                return;

            _simulationBackend.ForceComplete();
        }

        /// <summary>
        /// Despawns all placements owned by the active reconciler.
        /// </summary>
        public void DespawnAll()
        {
            if (_placementReconciler == null)
                return;

            _placementReconciler.DespawnAll();
        }

        /// <summary>
        /// Releases backend state and any live placements owned by the reconciler.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _simulationBackend?.Dispose();
            _placementReconciler?.Dispose();
            _disposed = true;
            _initialized = false;
            _lastCandidateCount = 0;
        }

        private static IScatterSimulationBackend CreateSimulationBackend(ScatterSimulationBackendKind backendKind)
        {
            switch (backendKind)
            {
                case ScatterSimulationBackendKind.ClassicJobs:
                    return new ScatterClassicSimulationBackend();
                case ScatterSimulationBackendKind.EntitiesDots:
                    if (ScatterSimulationBackendRegistry.TryCreateBackend(backendKind, out IScatterSimulationBackend dotsBackend))
                        return dotsBackend;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning("[WorldScatter] Entities DOTS backend provider not registered. Falling back to ClassicJobs.");
#endif
                    return new ScatterClassicSimulationBackend();
                default:
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning("[WorldScatter] Unknown scatter backend kind. Falling back to ClassicJobs.");
#endif
                    return new ScatterClassicSimulationBackend();
            }
        }

        private static IScatterPlacementReconciler CreatePlacementReconciler()
        {
            return new ScatterClassicPlacementReconciler();
        }
    }
}
