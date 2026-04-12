using System;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Classic Jobs-based scatter simulation backend.
    /// Thin adapter over <see cref="ScatterEvaluator"/> so runtime owners can depend on a stable seam.
    /// </summary>
    internal sealed class ScatterClassicSimulationBackend : IScatterSimulationBackend
    {
        private readonly ScatterEvaluator _evaluator;
        private bool _disposed;
        private bool _initialized;

        public ScatterClassicSimulationBackend()
        {
            // COLD ALLOC: ScatterEvaluator[1] - classic scatter simulation backend wrapper - owner: ScatterClassicSimulationBackend
            _evaluator = new ScatterEvaluator();
        }

        public ScatterSimulationBackendKind BackendKind => ScatterSimulationBackendKind.ClassicJobs;
        public bool IsInitialized => _initialized && !_disposed;
        public bool IsJobActive => !_disposed && _evaluator.IsJobActive;
        public bool IsJobCompleted => !_disposed && _evaluator.IsJobCompleted;

        public void Initialize()
        {
            if (_disposed || _initialized)
                return;

            _evaluator.Initialize();
            _initialized = true;
        }

        public bool TrySchedule(ScatterSimulationConfig config, NativeArray<float> heightSamples)
        {
            if (!IsInitialized || _evaluator.IsJobActive)
                return false;

            _evaluator.ScheduleEvaluation(config, heightSamples);
            return true;
        }

        public bool TryComplete(out ScatterSimulationResult result)
        {
            result = default;
            if (!IsInitialized || !_evaluator.IsJobActive)
                return false;

            int candidateCount = _evaluator.CompleteAndGetResults(out NativeArray<ScatterSimulationCandidate> candidates);
            result = new ScatterSimulationResult(candidates, candidateCount);
            return true;
        }

        public void ForceComplete()
        {
            if (!IsInitialized)
                return;

            _evaluator.ForceComplete();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _evaluator.Dispose();
            _disposed = true;
            _initialized = false;
        }
    }

    /// <summary>
    /// Classic main-thread placement reconciler adapter.
    /// Keeps spawn/despawn ownership outside any simulation backend.
    /// </summary>
    internal sealed class ScatterClassicPlacementReconciler : IScatterPlacementReconciler
    {
        private readonly ScatterReconciler _reconciler;
        private bool _disposed;

        public ScatterClassicPlacementReconciler()
        {
            // COLD ALLOC: ScatterReconciler[1] - classic scatter placement reconciler wrapper - owner: ScatterClassicPlacementReconciler
            _reconciler = new ScatterReconciler();
        }

        public int ActivePlacementCount => _disposed ? 0 : _reconciler.ActivePlacementCount;
        public int LastSpawnCount => _disposed ? 0 : _reconciler.LastSpawnCount;
        public int LastDespawnCount => _disposed ? 0 : _reconciler.LastDespawnCount;
        public int LastRetainedCount => _disposed ? 0 : _reconciler.LastRetainedCount;

        public void Reconcile(
            NativeArray<ScatterSimulationCandidate> candidates,
            int candidateCount,
            IScatterPrefabResolver prefabResolver)
        {
            if (_disposed)
                return;

            _reconciler.Reconcile(candidates, candidateCount, prefabResolver);
        }

        public void DespawnAll()
        {
            if (_disposed)
                return;

            _reconciler.DespawnAll();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _reconciler.Dispose();
            _disposed = true;
        }
    }
}
