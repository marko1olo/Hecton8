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
        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;
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
            if (!IsInitialized || _evaluator.IsJobActive)
                return false;

            _evaluator.ScheduleEvaluation(config, heightSamples);
            return true;
        }

        public bool TryComplete(out ScatterSimulationResult result)
        {
            result = default;
            if (!IsInitialized || !_evaluator.IsJobActive || !_evaluator.IsJobCompleted)
                return false;

            int candidateCount = _evaluator.CompleteAndGetResults(out NativeArray<ScatterSimulationCandidate> candidates);
            result = new ScatterSimulationResult(candidates, candidateCount, BuildParitySnapshot(candidates, candidateCount));
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

        private static ScatterSimulationParitySnapshot BuildParitySnapshot(
            NativeArray<ScatterSimulationCandidate> candidates,
            int candidateCount)
        {
            ScatterSimulationParitySnapshot snapshot = default;
            ulong hash = FnvOffset;
            for (int i = 0; i < candidateCount; i++)
            {
                ScatterSimulationCandidate candidate = candidates[i];
                switch (candidate.LayerIndex)
                {
                    case 0:
                        snapshot.GroundCount++;
                        break;
                    case 1:
                        snapshot.ClusterCount++;
                        break;
                    case 2:
                        snapshot.StructureCount++;
                        break;
                    case 3:
                        snapshot.SpawnCount++;
                        break;
                }

                hash = (hash ^ (ulong)candidate.CellKey) * FnvPrime;
                hash = (hash ^ (ulong)(uint)candidate.LayerIndex) * FnvPrime;
            }

            snapshot.CandidateCount = candidateCount;
            snapshot.CandidateChecksum = hash;
            return snapshot;
        }
    }
}
