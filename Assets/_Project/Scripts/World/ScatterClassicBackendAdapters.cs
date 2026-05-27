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

        public bool TrySchedule(
            ScatterSimulationConfig config,
            NativeArray<float>.ReadOnly heightSamples,
            NativeArray<ScatterSimulationCellState>.ReadOnly cellStates)
        {
            return false;
        }

        public bool TrySchedule(
            ScatterSimulationConfig config,
            NativeArray<float> heightSamples,
            NativeArray<ScatterSimulationCellState> cellStates)
        {
            return TrySchedule(config, heightSamples.AsReadOnly(), cellStates.AsReadOnly());
        }

        public bool TryComplete(out ScatterSimulationResult result)
        {
            result = default;
            if (!IsInitialized || !_evaluator.IsJobActive || !_evaluator.IsJobCompleted)
                return false;

            return _evaluator.TryComplete(out result);
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
}
