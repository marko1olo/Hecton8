// ============================================================================
// HECTON-8 — ScatterEvaluator.cs
// Scatter candidate evaluation backend contract.
//
// ARCHITECTURE:
//   Extracted from WorldProceduralScatterDirector (11,845-line monolith).
//   Current implementation is a disabled backend shell; owner path remains the source of truth.
//
// OWNERSHIP: WorldProceduralScatterDirector owns and drives this evaluator.
// LIFETIME:  Created in Awake, disposed in OnDisable/OnDestroy.
// ============================================================================

using System;
using Hecton8.Core;
using Unity.Collections;
using Unity.Jobs;

namespace Hecton8.World
{
    /// <summary>
    /// Disabled scatter backend shell kept for owner-route compatibility.
    /// </summary>
    internal sealed class ScatterEvaluator : IDisposable
    {
        // ══════════════════════════════════════════════════════════
        //  DATA STRUCTURES (Blittable for Jobs)
        // ══════════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════════
        //  CONSTANTS
        // ══════════════════════════════════════════════════════════

        private const int MaxCandidatesPerEvaluation = 4096; // COLD ALLOC budget
        private const string NativeMemoryOwner = nameof(ScatterEvaluator);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Session;
        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        // ══════════════════════════════════════════════════════════
        //  NATIVE CONTAINERS (Persistent lifetime)
        // ══════════════════════════════════════════════════════════

        private bool _disposed;
        private bool _initialized;
        private JobHandle _activeHandle;
        private bool _hasActiveJob;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        /// <summary>Whether an evaluation job is currently scheduled.</summary>
        public bool IsJobActive => _hasActiveJob;
        public bool IsJobCompleted => _hasActiveJob && _activeHandle.IsCompleted;

        /// <summary>Number of valid candidates from the last completed evaluation.</summary>
        public int LastCandidateCount => 0;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Allocates persistent native containers. Call once in Awake.
        /// </summary>
        /// <remarks>
        /// COLD ALLOC: 4096 × ScatterSimulationCandidate (256 KiB) + 4096 × float (~16 KiB) + 64 B counter.
        /// Persistent alloc justified: reused every scatter evaluation cycle for entire scene lifetime.
        /// </remarks>
        public void Initialize()
        {
            if (_initialized) return;

            _disposed = false;
            _initialized = true;
        }

        /// <summary>
        /// Schedules a scatter evaluation job. Call at start of frame or SlowTick.
        /// </summary>
        /// <param name="config">Evaluation configuration.</param>
        /// <param name="heightSamples">Pre-sampled terrain heights (must be filled before calling).</param>
        /// <returns>JobHandle for the scheduled work.</returns>
        /// <remarks>
        /// [REQ] Schedule() at start of frame. Complete() at end or next frame.
        /// [FORBID] Schedule()+Complete() in same method.
        /// </remarks>
        public JobHandle ScheduleEvaluation(ScatterSimulationConfig config, NativeArray<float>.ReadOnly heightSamples)
        {
            if (!_initialized)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[ScatterEvaluator] Not initialized. Call Initialize() first.");
#endif
                return default;
            }

            return _activeHandle;
        }

        /// <summary>
        /// Completes the active evaluation job and returns candidate data.
        /// Call on main thread at end of frame or next frame.
        /// </summary>
        /// <param name="result">Completed candidate result owned by the evaluator backend.</param>
        /// <returns>True when the completed result was produced.</returns>
        public bool TryComplete(out ScatterSimulationResult result)
        {
            result = default;

            _hasActiveJob = false;
            return false;
        }

        /// <summary>
        /// Attempts to complete pending work without blocking. Safe to call multiple times.
        /// </summary>
        public void ForceComplete()
        {
            if (!_hasActiveJob) return;

            if (!DispatcherJobSwap.TryComplete(ref _activeHandle, forceComplete: false))
                return;

            _hasActiveJob = false;
        }

        /// <summary>
        /// Disposes all native containers. Call in OnDisable or OnDestroy.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            bool hasActiveJob = _hasActiveJob;
            JobHandle activeHandle = hasActiveJob ? _activeHandle : default;
            _disposed = true;
            _initialized = false;
            _activeHandle = default;
            _hasActiveJob = false;
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array, JobHandle dependency, bool hasDependency)
            where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            if (hasDependency)
            {
                JobHandle disposeHandle = array.Dispose(dependency);
                DispatcherJobFence.TryComplete(ref disposeHandle, forceComplete: true);
            }
            else
            {
                array.Dispose();
            }

            array = default;
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

        // ══════════════════════════════════════════════════════════
        // ══════════════════════════════════════════════════════════

    }
}
