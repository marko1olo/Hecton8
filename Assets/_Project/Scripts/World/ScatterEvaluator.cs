// ============================================================================
// HECTON-8 — ScatterEvaluator.cs
// Scatter candidate evaluation via Unity Job System + Burst.
//
// ARCHITECTURE:
//   Extracted from WorldProceduralScatterDirector (11,845-line monolith).
//   Handles grid-cell iteration, height sampling, and candidate scoring
//   on worker threads. Results consumed by ScatterReconciler on main thread.
//
// OWNERSHIP: WorldProceduralScatterDirector owns and drives this evaluator.
// LIFETIME:  Created in Awake, disposed in OnDisable/OnDestroy.
// ============================================================================

using System;
using Hecton8.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Evaluates scatter grid cells and produces candidate placement data
    /// using Burst-compiled jobs. Zero managed allocations in hot path.
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

        // ══════════════════════════════════════════════════════════
        //  NATIVE CONTAINERS (Persistent lifetime)
        // ══════════════════════════════════════════════════════════

        private NativeArray<ScatterSimulationCandidate> _candidates;
        private NativeArray<float> _heightSamples;
        private NativeArray<int> _candidateCount;
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
        public int LastCandidateCount => _initialized && _candidateCount.IsCreated ? _candidateCount[0] : 0;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Allocates persistent native containers. Call once in Awake.
        /// </summary>
        /// <remarks>
        /// COLD ALLOC: 4096 × ScatterSimulationCandidate (~200 KB) + 4096 × float (~16 KB) + 4 B counter.
        /// Persistent alloc justified: reused every scatter evaluation cycle for entire scene lifetime.
        /// </remarks>
        public void Initialize()
        {
            if (_initialized) return;

            // COLD ALLOC: MaxCandidatesPerEvaluation entries, persistent lifetime.
            _candidates = new NativeArray<ScatterSimulationCandidate>(
                MaxCandidatesPerEvaluation, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);

            // COLD ALLOC: Height sample buffer for pre-sampled terrain heights.
            _heightSamples = new NativeArray<float>(
                MaxCandidatesPerEvaluation, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);

            // COLD ALLOC: Atomic counter for candidate output.
            _candidateCount = new NativeArray<int>(1, Allocator.Persistent,
                NativeArrayOptions.ClearMemory);

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
        public JobHandle ScheduleEvaluation(ScatterSimulationConfig config, NativeArray<float> heightSamples)
        {
            if (!_initialized)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[ScatterEvaluator] Not initialized. Call Initialize() first.");
#endif
                return default;
            }

            if (_hasActiveJob)
            {
                // Force-complete stale job before scheduling new one.
                _activeHandle.Complete();
                _hasActiveJob = false;
            }

            // Reset counter.
            _candidateCount[0] = 0;

            // Copy height data if provided externally.
            if (heightSamples.IsCreated && heightSamples.Length > 0)
            {
                int copyLength = math.min(heightSamples.Length, _heightSamples.Length);
                NativeArray<float>.Copy(heightSamples, _heightSamples, copyLength);
            }

            int diameter = config.RadiusCells * 2 + 1;
            int totalCells = diameter * diameter;

            var job = new ScatterCellEvaluationJob
            {
                Config = config,
                HeightSamples = _heightSamples,
                Candidates = _candidates,
                CandidateCount = _candidateCount,
                MaxCandidates = MaxCandidatesPerEvaluation,
                TotalCells = totalCells,
                Diameter = diameter
            };

            _activeHandle = job.Schedule(totalCells, 32);
            _hasActiveJob = true;
            return _activeHandle;
        }

        /// <summary>
        /// Completes the active evaluation job and returns candidate data.
        /// Call on main thread at end of frame or next frame.
        /// </summary>
        /// <param name="results">Output slice of valid candidates.</param>
        /// <returns>Number of valid candidates.</returns>
        public int CompleteAndGetResults(out NativeArray<ScatterSimulationCandidate> results)
        {
            results = _candidates;

            if (!_hasActiveJob || !_initialized)
                return 0;

            _activeHandle.Complete();
            _hasActiveJob = false;

            return _candidateCount[0];
        }

        /// <summary>
        /// Force-completes any pending job. Safe to call multiple times.
        /// </summary>
        public void ForceComplete()
        {
            if (!_hasActiveJob) return;
            _activeHandle.Complete();
            _hasActiveJob = false;
        }

        /// <summary>
        /// Disposes all native containers. Call in OnDisable or OnDestroy.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            ForceComplete();

            if (_candidates.IsCreated) _candidates.Dispose();
            if (_heightSamples.IsCreated) _heightSamples.Dispose();
            if (_candidateCount.IsCreated) _candidateCount.Dispose();

            _disposed = true;
            _initialized = false;
        }

        // ══════════════════════════════════════════════════════════
        //  BURST JOB
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Burst-compiled job that evaluates scatter grid cells in parallel.
        /// Produces blittable ScatterSimulationCandidate for main-thread reconciliation.
        /// </summary>
        /// <remarks>
        /// No managed refs. No GC. No string ops.
        /// Height sampling uses pre-filled NativeArray (main thread fills via
        /// Physics.RaycastNonAlloc or MapMagic queries before scheduling).
        /// </remarks>
        [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast)]
        private unsafe struct ScatterCellEvaluationJob : IJobParallelFor
        {
            [ReadOnly] public ScatterSimulationConfig Config;
            [ReadOnly] public NativeArray<float> HeightSamples;
            [NativeDisableParallelForRestriction]
            public NativeArray<ScatterSimulationCandidate> Candidates;
            [NativeDisableParallelForRestriction]
            public NativeArray<int> CandidateCount;
            public int MaxCandidates;
            public int TotalCells;
            public int Diameter;

            public void Execute(int index)
            {
                int cellX = (index % Diameter) - Config.RadiusCells;
                int cellZ = (index / Diameter) - Config.RadiusCells;

                // Player-relative grid position.
                int playerCellX = (int)math.floor(Config.PlayerPosition.x / Config.CellSize);
                int playerCellZ = (int)math.floor(Config.PlayerPosition.z / Config.CellSize);
                int worldCellX = playerCellX + cellX;
                int worldCellZ = playerCellZ + cellZ;

                // Deterministic cell key for residency tracking.
                long cellKey = ((long)worldCellX << 32) | (uint)worldCellZ;

                // Deterministic RNG seeded from cell coordinates + global seed.
                uint seed = Config.Seed ^ (uint)(worldCellX * 73856093) ^ (uint)(worldCellZ * 19349663);
                seed = math.max(seed, 1u); // Prevent zero-seed.
                var rng = new Unity.Mathematics.Random(seed);

                // Cell center in world space.
                float worldX = (worldCellX + 0.5f) * Config.CellSize;
                float worldZ = (worldCellZ + 0.5f) * Config.CellSize;

                // Distance from player for priority scoring.
                float dx = worldX - Config.PlayerPosition.x;
                float dz = worldZ - Config.PlayerPosition.z;
                float distSq = dx * dx + dz * dz;
                float maxDistSq = Config.RadiusCells * Config.CellSize * Config.RadiusCells * Config.CellSize;
                float distanceFactor = 1.0f - math.saturate(distSq / math.max(maxDistSq, 0.001f));

                // Height from pre-sampled terrain data.
                float terrainHeight = index < HeightSamples.Length ? HeightSamples[index] : 0f;

                // Ground placement candidates.
                for (int g = 0; g < Config.GroundPlacementsPerCell; g++)
                {
                    float offsetX = rng.NextFloat(-0.45f, 0.45f) * Config.CellSize;
                    float offsetZ = rng.NextFloat(-0.45f, 0.45f) * Config.CellSize;
                    float yRotation = rng.NextFloat(0f, 360f);
                    float scale = rng.NextFloat(0.75f, 1.25f);

                    // Atomic increment for output slot.
                    int slot = System.Threading.Interlocked.Increment(ref ((int*)CandidateCount.GetUnsafePtr())[0]) - 1;
                    if (slot >= MaxCandidates) return;

                    Candidates[slot] = new ScatterSimulationCandidate
                    {
                        Position = new float3(worldX + offsetX, terrainHeight + Config.SurfaceYOffset, worldZ + offsetZ),
                        Rotation = yRotation,
                        Scale = scale,
                        CellKey = cellKey + g,
                        FamilyIndex = Config.GroundFamilyIndex,
                        LayerIndex = 0, // Ground layer.
                        Score = distanceFactor * (1.0f + rng.NextFloat(0f, 0.15f)),
                        HeightSource = terrainHeight > -9999f ? 0 : 2,
                        IsValid = true
                    };
                }

                // Cluster placement candidates.
                for (int c = 0; c < Config.ClusterPlacementsPerCell; c++)
                {
                    float offsetX = rng.NextFloat(-0.35f, 0.35f) * Config.CellSize;
                    float offsetZ = rng.NextFloat(-0.35f, 0.35f) * Config.CellSize;
                    float yRotation = rng.NextFloat(0f, 360f);
                    float scale = rng.NextFloat(0.65f, 1.35f);

                    int slot = System.Threading.Interlocked.Increment(ref ((int*)CandidateCount.GetUnsafePtr())[0]) - 1;
                    if (slot >= MaxCandidates) return;

                    Candidates[slot] = new ScatterSimulationCandidate
                    {
                        Position = new float3(worldX + offsetX, terrainHeight + Config.SurfaceYOffset, worldZ + offsetZ),
                        Rotation = yRotation,
                        Scale = scale,
                        CellKey = cellKey + 10000 + c,
                        FamilyIndex = Config.ClusterFamilyIndex,
                        LayerIndex = 1, // Flora layer.
                        Score = distanceFactor * 0.85f * (1.0f + rng.NextFloat(0f, 0.1f)),
                        HeightSource = terrainHeight > -9999f ? 0 : 2,
                        IsValid = true
                    };
                }

                // Structure placement candidates (strided).
                bool isStructureCell = (worldCellX % Config.StructureCellStride == 0) &&
                                       (worldCellZ % Config.StructureCellStride == 0);
                if (isStructureCell)
                {
                    float yRotation = rng.NextFloat(0f, 360f);
                    float scale = rng.NextFloat(0.8f, 1.2f);

                    int slot = System.Threading.Interlocked.Increment(ref ((int*)CandidateCount.GetUnsafePtr())[0]) - 1;
                    if (slot >= MaxCandidates) return;

                    Candidates[slot] = new ScatterSimulationCandidate
                    {
                        Position = new float3(worldX, terrainHeight + Config.SurfaceYOffset, worldZ),
                        Rotation = yRotation,
                        Scale = scale,
                        CellKey = cellKey + 20000,
                        FamilyIndex = Config.StructureFamilyIndex,
                        LayerIndex = 2, // Debris/Structure layer.
                        Score = distanceFactor * 0.7f,
                        HeightSource = terrainHeight > -9999f ? 0 : 2,
                        IsValid = true
                    };
                }

                // Spawn placement candidates (wider stride).
                bool isSpawnCell = (worldCellX % Config.SpawnCellStride == 0) &&
                                   (worldCellZ % Config.SpawnCellStride == 0);
                if (isSpawnCell)
                {
                    float yRotation = rng.NextFloat(0f, 360f);

                    int slot = System.Threading.Interlocked.Increment(ref ((int*)CandidateCount.GetUnsafePtr())[0]) - 1;
                    if (slot >= MaxCandidates) return;

                    Candidates[slot] = new ScatterSimulationCandidate
                    {
                        Position = new float3(worldX, terrainHeight + Config.SurfaceYOffset + 0.5f, worldZ),
                        Rotation = yRotation,
                        Scale = 1.0f,
                        CellKey = cellKey + 30000,
                        FamilyIndex = Config.SpawnFamilyIndex,
                        LayerIndex = 3, // Fauna/Resource layer.
                        Score = distanceFactor * 0.6f,
                        HeightSource = terrainHeight > -9999f ? 0 : 2,
                        IsValid = true
                    };
                }
            }
        }
    }
}
