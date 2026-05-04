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

    [Flags]
    public enum ScatterSimulationEligibilityFlags : byte
    {
        None = 0,
        Ground = 1 << 0,
        Cluster = 1 << 1,
        Structure = 1 << 2,
        Spawn = 1 << 3,
        All = Ground | Cluster | Structure | Spawn
    }

    [Flags]
    public enum ScatterSimulationDirtyFlags : byte
    {
        None = 0,
        Heights = 1 << 0,
        Eligibility = 1 << 1,
        Quotas = 1 << 2,
        Suppression = 1 << 3,
        Candidates = 1 << 4,
        FullRebuild = 1 << 5
    }

    public enum ScatterSimulationSuppressionState : byte
    {
        None = 0,
        Suppressed = 1,
        Retained = 2
    }

    public struct ScatterSimulationLayerQuota
    {
        public int PlacementsPerCell;
        public int CellStride;
        public int FamilyIndex;
    }

    public struct ScatterSimulationQuotaState
    {
        public ScatterSimulationLayerQuota Ground;
        public ScatterSimulationLayerQuota Cluster;
        public ScatterSimulationLayerQuota Structure;
        public ScatterSimulationLayerQuota Spawn;
    }

    public struct ScatterSimulationCellState
    {
        public long CellKey;
        public int CellX;
        public int CellZ;
        public float Height;
        public int HeightSource;
        public uint BiomeInfluencePacked;
        public ScatterSimulationEligibilityFlags Eligibility;
        public ScatterSimulationSuppressionState Suppression;
        public ScatterSimulationDirtyFlags DirtyFlags;
    }

    public struct ScatterSimulationParitySnapshot
    {
        public int CandidateCount;
        public int GroundCount;
        public int ClusterCount;
        public int StructureCount;
        public int SpawnCount;
        public int EligibleGroundCells;
        public int EligibleClusterCells;
        public int EligibleStructureCells;
        public int EligibleSpawnCells;
        public int DirtyCellCount;
        public int SuppressedCellCount;
        public ulong CandidateChecksum;
        public ulong CellChecksum;
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
        public ScatterSimulationQuotaState QuotaState;
        public ScatterSimulationEligibilityFlags DefaultEligibility;
        public ScatterSimulationSuppressionState DefaultSuppressionState;
        public ScatterSimulationDirtyFlags DirtyFlags;

        // Legacy mirrors kept for classic evaluator compatibility during hybrid transition.
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
    /// Managed refs are resolved later by the owner-driven main-thread apply path.
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
        public ScatterSimulationResult(
            NativeArray<ScatterSimulationCandidate> candidates,
            int candidateCount,
            ScatterSimulationParitySnapshot paritySnapshot)
        {
            Candidates = candidates;
            CandidateCount = candidateCount;
            ParitySnapshot = paritySnapshot;
        }

        public NativeArray<ScatterSimulationCandidate> Candidates { get; }
        public int CandidateCount { get; }
        public ScatterSimulationParitySnapshot ParitySnapshot { get; }
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
        bool TrySchedule(
            ScatterSimulationConfig config,
            NativeArray<float> heightSamples,
            NativeArray<ScatterSimulationCellState> cellStates);
        bool TryComplete(out ScatterSimulationResult result);

        /// <summary>
        /// Attempts to close pending backend work without blocking the main thread.
        /// Implementations must leave incomplete jobs tracked for deferred disposal.
        /// </summary>
        void ForceComplete();
    }

}
