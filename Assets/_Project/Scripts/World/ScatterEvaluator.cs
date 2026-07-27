// ============================================================================
// HECTON-8 — ScatterEvaluator.cs
// Scatter candidate evaluation backend contract.
//
// ARCHITECTURE:
//   Extracted from WorldProceduralScatterDirector (11,845-line monolith).
//   Current implementation is a shadow backend evaluator; owner placement remains the source of truth.
//
// OWNERSHIP: WorldProceduralScatterDirector owns and drives this evaluator.
// LIFETIME:  Created in Awake, disposed in OnDisable/OnDestroy.
// ============================================================================

using System;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    /// <summary>
    /// Shadow scatter backend evaluator kept behind the owner-route compatibility seam.
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
        private const SystemID NativeArrayOwner = SystemID.WorldProceduralFieldSampler;
        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        // ══════════════════════════════════════════════════════════
        //  NATIVE CONTAINERS (Persistent lifetime)
        // ══════════════════════════════════════════════════════════

        private bool _disposed;
        private bool _initialized;
        private JobHandle _activeHandle;
        private bool _hasActiveJob;
        private NativeArray<float> _heightSamples;
        private NativeArray<ScatterSimulationCellState> _cellStates;
        private NativeArray<ScatterSimulationCandidate> _candidates;
        private NativeArray<ScatterSimulationParitySnapshot> _paritySnapshots;
        private int _lastCandidateCount;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        /// <summary>Whether an evaluation job is currently scheduled.</summary>
        public bool IsInitialized => _initialized && !_disposed;
        public bool IsJobActive => _hasActiveJob;
        public bool IsJobCompleted => _hasActiveJob && _activeHandle.IsCompleted;

        /// <summary>Number of valid candidates from the last completed evaluation.</summary>
        public int LastCandidateCount => _lastCandidateCount;

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
            EnsureNativeBuffers();
            _initialized = _candidates.IsCreated && _paritySnapshots.IsCreated;
        }

        public bool TryScheduleEvaluation(
            ScatterSimulationConfig config,
            NativeArray<float>.ReadOnly heightSamples,
            NativeArray<ScatterSimulationCellState>.ReadOnly cellStates)
        {
            if (!_initialized)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[ScatterEvaluator] Not initialized. Call Initialize() first.");
#endif
                return false;
            }

            int cellCount = cellStates.Length;
            if (_disposed ||
                _hasActiveJob ||
                !heightSamples.IsCreated ||
                !cellStates.IsCreated ||
                !_candidates.IsCreated ||
                !_paritySnapshots.IsCreated ||
                cellCount <= 0 ||
                heightSamples.Length < cellCount)
            {
                return false;
            }

            if (!EnsureInputBuffers(cellCount))
                return false;

            CopyInputSnapshot(heightSamples, cellStates, cellCount);
            _lastCandidateCount = 0;
            _paritySnapshots[0] = default;
            _activeHandle = new ScatterEvaluationJob
            {
                Config = config,
                HeightSamples = _heightSamples.GetSubArray(0, cellCount).AsReadOnly(),
                CellStates = _cellStates.GetSubArray(0, cellCount).AsReadOnly(),
                Candidates = _candidates,
                ParitySnapshots = _paritySnapshots
            }.Schedule();
            _hasActiveJob = true;
            return true;
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
            if (!_initialized || !_hasActiveJob || !_activeHandle.IsCompleted)
                return false;

            if (!DispatcherJobSwap.TryComplete(ref _activeHandle, forceComplete: false))
                return false;

            ScatterSimulationParitySnapshot snapshot = _paritySnapshots.IsCreated
                ? _paritySnapshots[0]
                : default;
            _lastCandidateCount = math.clamp(snapshot.CandidateCount, 0, _candidates.IsCreated ? _candidates.Length : 0);
            _hasActiveJob = false;
            result = new ScatterSimulationResult(_candidates, _lastCandidateCount, snapshot);
            return true;
        }

        /// <summary>
        /// Attempts to complete pending work without blocking. Safe to call multiple times.
        /// </summary>
        public void ForceComplete()
        {
            if (!_hasActiveJob) return;

            if (!DispatcherJobSwap.TryComplete(ref _activeHandle, forceComplete: false))
                return;

            if (_paritySnapshots.IsCreated)
                _lastCandidateCount = math.clamp(_paritySnapshots[0].CandidateCount, 0, _candidates.IsCreated ? _candidates.Length : 0);

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
            _lastCandidateCount = 0;
            DisposeNativeArray(ref _heightSamples, activeHandle, hasActiveJob);
            DisposeNativeArray(ref _cellStates, activeHandle, hasActiveJob);
            DisposeNativeArray(ref _candidates, activeHandle, hasActiveJob);
            DisposeNativeArray(ref _paritySnapshots, activeHandle, hasActiveJob);
        }

        private void EnsureNativeBuffers()
        {
            if (_candidates.IsCreated && _paritySnapshots.IsCreated)
                return;

            ReleaseNativeBuffers(default, hasDependency: false);

            _candidates = H8Memory.Allocate<ScatterSimulationCandidate>(
                MaxCandidatesPerEvaluation,
                NativeArrayOwner,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            _paritySnapshots = H8Memory.Allocate<ScatterSimulationParitySnapshot>(
                1,
                NativeArrayOwner,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory);

            if (!_candidates.IsCreated || !_paritySnapshots.IsCreated)
            {
                ReleaseNativeBuffers(default, hasDependency: false);
                return;
            }

            try
            {
                RegisterNativeArray(_candidates, nameof(_candidates));
                RegisterNativeArray(_paritySnapshots, nameof(_paritySnapshots));
            }
            catch
            {
                ReleaseNativeBuffers(default, hasDependency: false);
                throw;
            }
        }

        private void ReleaseNativeBuffers(JobHandle dependency, bool hasDependency)
        {
            DisposeNativeArray(ref _heightSamples, dependency, hasDependency);
            DisposeNativeArray(ref _cellStates, dependency, hasDependency);
            DisposeNativeArray(ref _candidates, dependency, hasDependency);
            DisposeNativeArray(ref _paritySnapshots, dependency, hasDependency);
        }

        private bool EnsureInputBuffers(int cellCount)
        {
            if (_heightSamples.IsCreated &&
                _cellStates.IsCreated &&
                _heightSamples.Length >= cellCount &&
                _cellStates.Length >= cellCount)
            {
                return true;
            }

            DisposeNativeArray(ref _heightSamples, default, hasDependency: false);
            DisposeNativeArray(ref _cellStates, default, hasDependency: false);

            _heightSamples = H8Memory.Allocate<float>(
                cellCount,
                NativeArrayOwner,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            _cellStates = H8Memory.Allocate<ScatterSimulationCellState>(
                cellCount,
                NativeArrayOwner,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);

            if (!_heightSamples.IsCreated || !_cellStates.IsCreated)
            {
                DisposeNativeArray(ref _heightSamples, default, hasDependency: false);
                DisposeNativeArray(ref _cellStates, default, hasDependency: false);
                return false;
            }

            try
            {
                RegisterNativeArray(_heightSamples, nameof(_heightSamples));
                RegisterNativeArray(_cellStates, nameof(_cellStates));
            }
            catch
            {
                DisposeNativeArray(ref _heightSamples, default, hasDependency: false);
                DisposeNativeArray(ref _cellStates, default, hasDependency: false);
                throw;
            }

            return true;
        }

        private static void CopyInputSnapshot(
            NativeArray<float>.ReadOnly sourceHeightSamples,
            NativeArray<ScatterSimulationCellState>.ReadOnly sourceCellStates,
            int cellCount,
            NativeArray<float> destinationHeightSamples,
            NativeArray<ScatterSimulationCellState> destinationCellStates)
        {
            for (int i = 0; i < cellCount; i++)
            {
                destinationHeightSamples[i] = sourceHeightSamples[i];
                destinationCellStates[i] = sourceCellStates[i];
            }
        }

        private void CopyInputSnapshot(
            NativeArray<float>.ReadOnly sourceHeightSamples,
            NativeArray<ScatterSimulationCellState>.ReadOnly sourceCellStates,
            int cellCount)
        {
            CopyInputSnapshot(sourceHeightSamples, sourceCellStates, cellCount, _heightSamples, _cellStates);
        }

        private static void RegisterNativeArray<T>(NativeArray<T> array, string label) where T : struct
        {
            int sentinelId = NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeMemoryLifetime);
            if (sentinelId <= 0)
                throw new InvalidOperationException($"NativeMemorySentinel rejected scatter evaluator array registration for {label}.");
        }

        // The only IJob under Assets/_Project/Scripts that was missing [BurstCompile] (1391 of
        // 1392 had it), so this scatter evaluation ran as managed IL over every simulation cell.
        // Deterministic rather than Fast is required here, not preferred: the job folds its own
        // results into FNV candidate/cell hashes stored in ScatterSimulationParitySnapshot, which
        // exists to detect desync. Fast float would let those parity hashes differ per ISA and
        // report false desyncs on identical input.
        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct ScatterEvaluationJob : IJob
        {
            public ScatterSimulationConfig Config;
            [ReadOnly, NoAlias] public NativeArray<float>.ReadOnly HeightSamples;
            [ReadOnly, NoAlias] public NativeArray<ScatterSimulationCellState>.ReadOnly CellStates;
            [NoAlias] public NativeArray<ScatterSimulationCandidate> Candidates;
            [NoAlias] public NativeArray<ScatterSimulationParitySnapshot> ParitySnapshots;

            public void Execute()
            {
                ScatterSimulationParitySnapshot snapshot = default;
                ulong candidateHash = FnvOffset;
                ulong cellHash = FnvOffset;
                int candidateCount = 0;
                int cellCount = CellStates.Length;

                for (int i = 0; i < cellCount; i++)
                {
                    ScatterSimulationCellState cellState = CellStates[i];
                    float height = ResolveHeight(i, in cellState);
                    AccumulateCellParity(ref snapshot, ref cellHash, in cellState, height);

                    if (cellState.Suppression == ScatterSimulationSuppressionState.Suppressed)
                        continue;

                    TryEmitLayerCandidates(
                        ref snapshot,
                        ref candidateCount,
                        ref candidateHash,
                        in cellState,
                        height,
                        ScatterSimulationEligibilityFlags.Ground,
                        layerIndex: 0,
                        familyIndex: Config.QuotaState.Ground.FamilyIndex,
                        placementsPerCell: Config.QuotaState.Ground.PlacementsPerCell,
                        cellStride: Config.QuotaState.Ground.CellStride);
                    TryEmitLayerCandidates(
                        ref snapshot,
                        ref candidateCount,
                        ref candidateHash,
                        in cellState,
                        height,
                        ScatterSimulationEligibilityFlags.Cluster,
                        layerIndex: 1,
                        familyIndex: Config.QuotaState.Cluster.FamilyIndex,
                        placementsPerCell: Config.QuotaState.Cluster.PlacementsPerCell,
                        cellStride: Config.QuotaState.Cluster.CellStride);
                    TryEmitLayerCandidates(
                        ref snapshot,
                        ref candidateCount,
                        ref candidateHash,
                        in cellState,
                        height,
                        ScatterSimulationEligibilityFlags.Structure,
                        layerIndex: 2,
                        familyIndex: Config.QuotaState.Structure.FamilyIndex,
                        placementsPerCell: Config.QuotaState.Structure.PlacementsPerCell,
                        cellStride: Config.QuotaState.Structure.CellStride);
                    TryEmitLayerCandidates(
                        ref snapshot,
                        ref candidateCount,
                        ref candidateHash,
                        in cellState,
                        height,
                        ScatterSimulationEligibilityFlags.Spawn,
                        layerIndex: 3,
                        familyIndex: Config.QuotaState.Spawn.FamilyIndex,
                        placementsPerCell: Config.QuotaState.Spawn.PlacementsPerCell,
                        cellStride: Config.QuotaState.Spawn.CellStride);
                }

                snapshot.CandidateCount = candidateCount;
                snapshot.CandidateChecksum = candidateHash;
                snapshot.CellChecksum = cellHash;
                ParitySnapshots[0] = snapshot;
            }

            private float ResolveHeight(int index, in ScatterSimulationCellState cellState)
            {
                float sampleHeight = index >= 0 && index < HeightSamples.Length ? HeightSamples[index] : cellState.Height;
                if (math.isfinite(sampleHeight))
                    return sampleHeight;

                return math.isfinite(cellState.Height) ? cellState.Height : 0f;
            }

            private static void AccumulateCellParity(
                ref ScatterSimulationParitySnapshot snapshot,
                ref ulong cellHash,
                in ScatterSimulationCellState cellState,
                float height)
            {
                if ((cellState.Eligibility & ScatterSimulationEligibilityFlags.Ground) != 0)
                    snapshot.EligibleGroundCells++;
                if ((cellState.Eligibility & ScatterSimulationEligibilityFlags.Cluster) != 0)
                    snapshot.EligibleClusterCells++;
                if ((cellState.Eligibility & ScatterSimulationEligibilityFlags.Structure) != 0)
                    snapshot.EligibleStructureCells++;
                if ((cellState.Eligibility & ScatterSimulationEligibilityFlags.Spawn) != 0)
                    snapshot.EligibleSpawnCells++;
                if (cellState.DirtyFlags != ScatterSimulationDirtyFlags.None)
                    snapshot.DirtyCellCount++;
                if (cellState.Suppression == ScatterSimulationSuppressionState.Suppressed)
                    snapshot.SuppressedCellCount++;

                cellHash = Hash(cellHash, unchecked((ulong)cellState.CellKey));
                cellHash = Hash(cellHash, unchecked((ulong)(uint)cellState.CellX));
                cellHash = Hash(cellHash, unchecked((ulong)(uint)cellState.CellZ));
                cellHash = Hash(cellHash, math.asuint(height));
                cellHash = Hash(cellHash, unchecked((ulong)(uint)cellState.HeightSource));
                cellHash = Hash(cellHash, cellState.BiomeInfluencePacked);
                cellHash = Hash(cellHash, (byte)cellState.Eligibility);
                cellHash = Hash(cellHash, (byte)cellState.Suppression);
                cellHash = Hash(cellHash, (byte)cellState.DirtyFlags);
            }

            private void TryEmitLayerCandidates(
                ref ScatterSimulationParitySnapshot snapshot,
                ref int candidateCount,
                ref ulong candidateHash,
                in ScatterSimulationCellState cellState,
                float height,
                ScatterSimulationEligibilityFlags eligibility,
                int layerIndex,
                int familyIndex,
                int placementsPerCell,
                int cellStride)
            {
                if ((cellState.Eligibility & eligibility) == 0 ||
                    familyIndex < 0 ||
                    placementsPerCell <= 0)
                {
                    return;
                }

                if (candidateCount >= Candidates.Length)
                {
                    MarkCandidateCapacitySaturated(ref snapshot);
                    return;
                }

                int safeStride = math.max(1, cellStride);
                int cellOrdinal = unchecked(cellState.CellX * 73856093 ^ cellState.CellZ * 19349663);
                cellOrdinal = cellOrdinal == int.MinValue ? 0 : math.abs(cellOrdinal);
                if (cellOrdinal % safeStride != 0)
                    return;

                int emitCount = math.min(placementsPerCell, Candidates.Length - candidateCount);
                if (emitCount < placementsPerCell)
                    MarkCandidateCapacitySaturated(ref snapshot);

                for (int placementIndex = 0; placementIndex < emitCount; placementIndex++)
                {
                    ScatterSimulationCandidate candidate = BuildCandidate(
                        in cellState,
                        height,
                        layerIndex,
                        familyIndex,
                        placementIndex);
                    Candidates[candidateCount++] = candidate;
                    IncrementLayerCount(ref snapshot, layerIndex);
                    candidateHash = AccumulateCandidateHash(candidateHash, in cellState, layerIndex);
                }
            }

            private static void MarkCandidateCapacitySaturated(ref ScatterSimulationParitySnapshot snapshot)
            {
                snapshot.EvaluationFlags |= ScatterSimulationParitySnapshot.CandidateCapacitySaturatedFlag;
            }

            private static void IncrementLayerCount(ref ScatterSimulationParitySnapshot snapshot, int layerIndex)
            {
                switch (layerIndex)
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
            }

            private ScatterSimulationCandidate BuildCandidate(
                in ScatterSimulationCellState cellState,
                float height,
                int layerIndex,
                int familyIndex,
                int placementIndex)
            {
                uint hash = HashToUInt(cellState.CellKey, layerIndex, placementIndex, Config.Seed);
                float jitterX = (((hash >> 8) & 0xFFu) * (1f / 255f) - 0.5f) * Config.CellSize;
                float jitterZ = (((hash >> 16) & 0xFFu) * (1f / 255f) - 0.5f) * Config.CellSize;
                float cellSize = math.max(0.001f, Config.CellSize);

                return new ScatterSimulationCandidate
                {
                    CellKey = cellState.CellKey,
                    Position = new float3(
                        Config.PlayerPosition.x + cellState.CellX * cellSize + jitterX,
                        height + Config.SurfaceYOffset,
                        Config.PlayerPosition.z + cellState.CellZ * cellSize + jitterZ),
                    Rotation = (hash & 0xFFFFu) * (360f / 65535f),
                    Scale = 1f,
                    Score = 1f,
                    FamilyIndex = familyIndex,
                    LayerIndex = layerIndex,
                    HeightSource = cellState.HeightSource,
                    IsValid = 1
                };
            }
        }

        private static ulong AccumulateCandidateHash(
            ulong hash,
            in ScatterSimulationCellState cellState,
            int layerIndex)
        {
            hash = Hash(hash, unchecked((ulong)(uint)layerIndex));
            long classicCellKey = ((long)(uint)(cellState.CellX & 0xFFFF) << 32) | (uint)(cellState.CellZ & 0xFFFF);
            return Hash(hash, unchecked((ulong)classicCellKey));
        }

        private static uint HashToUInt(long cellKey, int layerIndex, int placementIndex, uint seed)
        {
            ulong hash = FnvOffset;
            hash = Hash(hash, unchecked((ulong)cellKey));
            hash = Hash(hash, unchecked((ulong)(uint)layerIndex));
            hash = Hash(hash, unchecked((ulong)(uint)placementIndex));
            hash = Hash(hash, seed);
            return (uint)(hash ^ (hash >> 32));
        }

        private static ulong Hash(ulong hash, ulong value)
        {
            return (hash ^ value) * FnvPrime;
        }

        private static unsafe void DisposeNativeArray<T>(ref NativeArray<T> array, JobHandle dependency, bool hasDependency)
            where T : struct
        {
            if (!array.IsCreated)
                return;

            void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);
            Exception firstException = null;

            if (hasDependency)
            {
                JobHandle disposeHandle = H8Memory.Release(ref array, dependency, NativeArrayOwner);
                if (array.IsCreated)
                    return;
                if (!DispatcherJobFence.TryComplete(ref disposeHandle, forceComplete: true))
                    throw new InvalidOperationException("ScatterEvaluator native array disposal did not complete before sentinel unregister.");

                try
                {
                    NativeMemorySentinel.UnregisterPointer(trackedPointer);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }
            }
            else
            {
                try
                {
                    H8Memory.Release(ref array, NativeArrayOwner);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }

                if (array.IsCreated)
                    return;
                try
                {
                    NativeMemorySentinel.UnregisterPointer(trackedPointer);
                }
                catch (Exception exception)
                {
                    if (firstException == null)
                        firstException = exception;
                }
            }

            array = default;

            if (firstException != null)
                throw firstException;
        }

        // ══════════════════════════════════════════════════════════
        // ══════════════════════════════════════════════════════════

    }
}
