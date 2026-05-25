using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    internal static class ScatterSimulationContractLayout
    {
        public const int ScatterSimulationLayerQuotaStrideBytes = 16;
        public const int ScatterSimulationQuotaStateStrideBytes = 64;
        public const int ScatterSimulationCellStateStrideBytes = 32;
        public const int ScatterSimulationParitySnapshotStrideBytes = 64;
        public const int ScatterSimulationConfigStrideBytes = 128;
        public const int ScatterSimulationCandidateStrideBytes = 64;
    }

    public enum ScatterSimulationBackendKind
    {
        ClassicJobs = 0,
        EntitiesDots = 1
    }

    public enum ScatterBackendExecutionMode
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

    [StructLayout(LayoutKind.Explicit, Size = ScatterSimulationContractLayout.ScatterSimulationLayerQuotaStrideBytes)]
    public struct ScatterSimulationLayerQuota
    {
        [FieldOffset(0)]
        public int PlacementsPerCell;

        [FieldOffset(4)]
        public int CellStride;

        [FieldOffset(8)]
        public int FamilyIndex;

        [FieldOffset(12)]
        private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = ScatterSimulationContractLayout.ScatterSimulationQuotaStateStrideBytes)]
    public struct ScatterSimulationQuotaState
    {
        [FieldOffset(0)]
        public ScatterSimulationLayerQuota Ground;

        [FieldOffset(16)]
        public ScatterSimulationLayerQuota Cluster;

        [FieldOffset(32)]
        public ScatterSimulationLayerQuota Structure;

        [FieldOffset(48)]
        public ScatterSimulationLayerQuota Spawn;
    }

    [StructLayout(LayoutKind.Explicit, Size = ScatterSimulationContractLayout.ScatterSimulationCellStateStrideBytes)]
    public struct ScatterSimulationCellState
    {
        [FieldOffset(0)]
        public long CellKey;

        [FieldOffset(8)]
        public int CellX;

        [FieldOffset(12)]
        public int CellZ;

        [FieldOffset(16)]
        public float Height;

        [FieldOffset(20)]
        public int HeightSource;

        [FieldOffset(24)]
        public uint BiomeInfluencePacked;

        [FieldOffset(28)]
        public ScatterSimulationEligibilityFlags Eligibility;

        [FieldOffset(29)]
        public ScatterSimulationSuppressionState Suppression;

        [FieldOffset(30)]
        public ScatterSimulationDirtyFlags DirtyFlags;

        [FieldOffset(31)]
        private byte _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = ScatterSimulationContractLayout.ScatterSimulationParitySnapshotStrideBytes)]
    public struct ScatterSimulationParitySnapshot
    {
        [FieldOffset(0)]
        public ulong CandidateChecksum;

        [FieldOffset(8)]
        public ulong CellChecksum;

        [FieldOffset(16)]
        public int CandidateCount;

        [FieldOffset(20)]
        public int GroundCount;

        [FieldOffset(24)]
        public int ClusterCount;

        [FieldOffset(28)]
        public int StructureCount;

        [FieldOffset(32)]
        public int SpawnCount;

        [FieldOffset(36)]
        public int EligibleGroundCells;

        [FieldOffset(40)]
        public int EligibleClusterCells;

        [FieldOffset(44)]
        public int EligibleStructureCells;

        [FieldOffset(48)]
        public int EligibleSpawnCells;

        [FieldOffset(52)]
        public int DirtyCellCount;

        [FieldOffset(56)]
        public int SuppressedCellCount;

        [FieldOffset(60)]
        private uint _pad0;
    }

    /// <summary>
    /// Immutable config for one scatter simulation pass.
    /// Shared by classic Jobs and future DOTS backends.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = ScatterSimulationContractLayout.ScatterSimulationConfigStrideBytes)]
    public struct ScatterSimulationConfig
    {
        [FieldOffset(0)]
        public ScatterSimulationQuotaState QuotaState;

        [FieldOffset(64)]
        public float3 PlayerPosition;

        [FieldOffset(76)]
        public float CellSize;

        [FieldOffset(80)]
        public float SurfaceYOffset;

        [FieldOffset(84)]
        public uint Seed;

        [FieldOffset(88)]
        public int RadiusCells;

        // Legacy mirrors kept for classic evaluator compatibility during hybrid transition.
        [FieldOffset(92)]
        public int GroundPlacementsPerCell;

        [FieldOffset(96)]
        public int ClusterPlacementsPerCell;

        [FieldOffset(100)]
        public int StructureCellStride;

        [FieldOffset(104)]
        public int SpawnCellStride;

        [FieldOffset(108)]
        public int GroundFamilyIndex;

        [FieldOffset(112)]
        public int ClusterFamilyIndex;

        [FieldOffset(116)]
        public int StructureFamilyIndex;

        [FieldOffset(120)]
        public int SpawnFamilyIndex;

        [FieldOffset(124)]
        public ScatterSimulationEligibilityFlags DefaultEligibility;

        [FieldOffset(125)]
        public ScatterSimulationSuppressionState DefaultSuppressionState;

        [FieldOffset(126)]
        public ScatterSimulationDirtyFlags DirtyFlags;

        [FieldOffset(127)]
        private byte _pad0;
    }

    /// <summary>
    /// Blittable scatter candidate contract shared by classic Jobs and future DOTS backends.
    /// Managed refs are resolved later by the owner-driven main-thread apply path.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = ScatterSimulationContractLayout.ScatterSimulationCandidateStrideBytes)]
    public struct ScatterSimulationCandidate
    {
        [FieldOffset(0)]
        public long CellKey;

        [FieldOffset(8)]
        public float3 Position;

        [FieldOffset(20)]
        public float Rotation;

        [FieldOffset(24)]
        public float Scale;

        [FieldOffset(28)]
        public float Score;

        [FieldOffset(32)]
        public int FamilyIndex;

        [FieldOffset(36)]
        public int LayerIndex;

        [FieldOffset(40)]
        public int HeightSource;

        [FieldOffset(44)]
        public byte IsValid;

        [FieldOffset(45)]
        private byte _pad0;

        [FieldOffset(46)]
        private ushort _pad1;

        [FieldOffset(48)]
        private ulong _pad2;

        [FieldOffset(56)]
        private ulong _pad3;
    }

    /// <summary>
    /// Completed simulation output for one scatter pass.
    /// NativeArray ownership stays with the backend that produced it.
    /// </summary>
    public readonly struct ScatterSimulationResult
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

        public readonly NativeArray<ScatterSimulationCandidate> Candidates;
        public readonly int CandidateCount;
        public readonly ScatterSimulationParitySnapshot ParitySnapshot;
    }

    /// <summary>
    /// Backend seam for scatter candidate simulation.
    /// Current implementation is classic Jobs; DOTS backend plugs into the same contract.
    /// </summary>
    public interface IScatterSimulationBackend : IDisposable
    {
        ScatterSimulationBackendKind BackendKind { get; }
        bool IsInitialized { get; }
        bool IsJobActive { get; }
        bool IsJobCompleted { get; }

        void Initialize();
        bool TrySchedule(
            ScatterSimulationConfig config,
            NativeArray<float>.ReadOnly heightSamples,
            NativeArray<ScatterSimulationCellState>.ReadOnly cellStates);
        bool TryComplete(out ScatterSimulationResult result);

        /// <summary>
        /// Attempts to close pending backend work without blocking the main thread.
        /// Implementations must leave incomplete jobs tracked for deferred disposal.
        /// </summary>
        void ForceComplete();
    }

}
