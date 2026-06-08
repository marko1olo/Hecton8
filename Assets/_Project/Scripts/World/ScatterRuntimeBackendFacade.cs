using System;
using Unity.Collections;
using Unity.Profiling;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Orchestrates the active scatter simulation backend.
    /// Scene ownership remains outside the backend seam.
    /// </summary>
    internal sealed class ScatterRuntimeBackendFacade : IDisposable
    {
        private static readonly ProfilerMarker _backendScheduleProfilerMarker = new("WorldScatter.Backend.Schedule");
        private static readonly ProfilerMarker _backendCompleteProfilerMarker = new("WorldScatter.Backend.Complete");

        private readonly IScatterSimulationBackend _simulationBackend;
        private readonly ScatterSimulationBackendKind _requestedBackendKind;
        private readonly uint _backendProviderVersion;
        private bool _disposed;
        private bool _initialized;
        private int _lastCandidateCount;

        /// <summary>
        /// Creates a backend facade for the requested simulation backend kind.
        /// Unsupported kinds fall back to the classic Jobs path until a real implementation exists.
        /// </summary>
        /// <param name="backendKind">Requested simulation backend kind.</param>
        public ScatterRuntimeBackendFacade(ScatterSimulationBackendKind backendKind)
            : this(backendKind, CreateSimulationBackend(backendKind))
        {
        }

        /// <summary>
        /// Creates a backend facade with explicit dependency injection for simulation.
        /// Intended for controlled runtime composition and test injection.
        /// </summary>
        /// <param name="simulationBackend">Simulation backend implementation.</param>
        public ScatterRuntimeBackendFacade(
            IScatterSimulationBackend simulationBackend)
            : this(
                simulationBackend != null ? simulationBackend.BackendKind : ScatterSimulationBackendKind.ClassicJobs,
                simulationBackend)
        {
        }

        private ScatterRuntimeBackendFacade(
            ScatterSimulationBackendKind requestedBackendKind,
            IScatterSimulationBackend simulationBackend)
        {
            _requestedBackendKind = requestedBackendKind;
            _backendProviderVersion = ScatterSimulationBackendRegistry.Version;
            _simulationBackend = simulationBackend;
        }

        /// <summary>Backend kind requested by rollout plan, distinct from actual provider fallback.</summary>
        public ScatterSimulationBackendKind RequestedBackendKind => _requestedBackendKind;
        public uint BackendProviderVersion => _backendProviderVersion;

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

        /// <summary>
        /// Initializes the active simulation backend. Safe to call multiple times.
        /// </summary>
        public void Initialize()
        {
            if (_disposed || _initialized)
                return;

            if (_simulationBackend == null)
                return;

            _simulationBackend.Initialize();
            _initialized = _simulationBackend.IsInitialized;
        }

        /// <summary>
        /// Schedules a scatter simulation pass on the active backend.
        /// </summary>
        /// <param name="config">Simulation config for the current pass.</param>
        /// <param name="heightSamples">Pre-sampled terrain heights.</param>
        /// <param name="cellStates">Owner-derived per-cell narrow-scope state snapshot.</param>
        /// <returns>True when scheduling succeeded; false when backend is unavailable or already busy.</returns>
        public bool TrySchedule(
            ScatterSimulationConfig config,
            NativeArray<float>.ReadOnly heightSamples,
            NativeArray<ScatterSimulationCellState>.ReadOnly cellStates)
        {
            if (!IsInitialized || _simulationBackend == null)
                return false;

            using (_backendScheduleProfilerMarker.Auto())
            {
                return TryScheduleKnownBackend(_simulationBackend, config, heightSamples, cellStates);
            }
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
        /// Attempts to close pending simulation work without running reconciliation.
        /// Incomplete backend jobs remain tracked for deferred disposal.
        /// </summary>
        public void ForceComplete()
        {
            if (!IsInitialized || _simulationBackend == null)
                return;

            _simulationBackend.ForceComplete();
        }

        /// <summary>
        /// Releases backend state.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _simulationBackend?.Dispose();
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
                    Hecton8.Core.H8Debug.LogWarning("[WorldScatter] Entities DOTS backend provider not registered. Falling back to ClassicJobs.");
#endif
                    return new ScatterClassicSimulationBackend();
                default:
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Hecton8.Core.H8Debug.LogWarning("[WorldScatter] Unknown scatter backend kind. Falling back to ClassicJobs.");
#endif
                    return new ScatterClassicSimulationBackend();
            }
        }

        private static bool TryScheduleKnownBackend(
            IScatterSimulationBackend backend,
            ScatterSimulationConfig config,
            NativeArray<float>.ReadOnly heightSamples,
            NativeArray<ScatterSimulationCellState>.ReadOnly cellStates)
        {
            return backend != null && backend.TrySchedule(config, heightSamples, cellStates);
        }
    }
}
