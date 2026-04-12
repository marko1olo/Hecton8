using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    internal enum ScatterSimulationBackendKind
    {
        ClassicJobs = 0,
        EntitiesDots = 1
    }

    internal enum ScatterBackendExecutionMode
    {
        Disabled = 0,
        Shadow = 1,
        ReservedLiveOwnership = 2
    }

    /// <summary>
    /// Immutable config for one scatter simulation pass.
    /// Shared by classic Jobs and future DOTS backends.
    /// </summary>
    internal struct ScatterSimulationConfig
    {
        public float CellSize;
        public int RadiusCells;
        public float3 PlayerPosition;
        public int GroundPlacementsPerCell;
        public int ClusterPlacementsPerCell;
        public int StructureCellStride;
        public int SpawnCellStride;
        public int GroundFamilyIndex;
        public int ClusterFamilyIndex;
        public int StructureFamilyIndex;
        public int SpawnFamilyIndex;
        public float SurfaceYOffset;
        public uint Seed;
    }

    /// <summary>
    /// Blittable scatter candidate contract shared by classic Jobs and future DOTS backends.
    /// Managed refs are resolved later by the main-thread reconciler.
    /// </summary>
    internal struct ScatterSimulationCandidate
    {
        public float3 Position;
        public float Rotation;
        public float Scale;
        public long CellKey;
        public int FamilyIndex;
        public int LayerIndex;
        public float Score;
        public int HeightSource;
        public bool IsValid;
    }

    /// <summary>
    /// Completed simulation output for one scatter pass.
    /// NativeArray ownership stays with the backend that produced it.
    /// </summary>
    internal readonly struct ScatterSimulationResult
    {
        public ScatterSimulationResult(NativeArray<ScatterSimulationCandidate> candidates, int candidateCount)
        {
            Candidates = candidates;
            CandidateCount = candidateCount;
        }

        public NativeArray<ScatterSimulationCandidate> Candidates { get; }
        public int CandidateCount { get; }
    }

    /// <summary>
    /// Backend seam for scatter candidate simulation.
    /// Current implementation is classic Jobs; DOTS backend plugs into the same contract.
    /// </summary>
    internal interface IScatterSimulationBackend : IDisposable
    {
        ScatterSimulationBackendKind BackendKind { get; }
        bool IsInitialized { get; }
        bool IsJobActive { get; }
        bool IsJobCompleted { get; }

        void Initialize();
        bool TrySchedule(ScatterSimulationConfig config, NativeArray<float> heightSamples);
        bool TryComplete(out ScatterSimulationResult result);
        void ForceComplete();
    }

    /// <summary>
    /// Resolves prefabs for scatter candidates on the main thread.
    /// This avoids wiring managed delegates through hot-path reconciliation calls.
    /// </summary>
    internal interface IScatterPrefabResolver
    {
        bool TryResolvePrefab(int familyIndex, int layerIndex, out GameObject prefab);
    }

    /// <summary>
    /// Main-thread owner for live placement reconciliation.
    /// Simulation backend may change; scene ownership must not.
    /// </summary>
    internal interface IScatterPlacementReconciler : IDisposable
    {
        int ActivePlacementCount { get; }
        int LastSpawnCount { get; }
        int LastDespawnCount { get; }
        int LastRetainedCount { get; }

        void Reconcile(
            NativeArray<ScatterSimulationCandidate> candidates,
            int candidateCount,
            IScatterPrefabResolver prefabResolver);

        void DespawnAll();
    }
}
