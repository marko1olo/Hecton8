using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using EntitiesWorld = Unity.Entities.World;

namespace Hecton8.World.Dots
{
    /// <summary>
    /// Minimal Entities-based scatter simulation backend.
    /// This is a shadow-safe prototype backend: owner semantics remain unchanged and live ownership still stays outside DOTS.
    /// </summary>
    internal sealed class ScatterEntitiesSimulationBackend : IScatterSimulationBackend
    {
        private const int HeightSourceEntities = 2;
        private const int MaxCandidatesPerCell = 4;
        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        private EntitiesWorld _world;
        private EntityManager _entityManager;
        private Entity _requestEntity;
        private NativeArray<ScatterSimulationCandidate> _resultCandidates;
        private NativeArray<ScatterSimulationCellState> _jobCellStates;
        private NativeArray<ScatterSimulationCandidate> _jobCandidateSlots;
        private JobHandle _simulationJobHandle;
        private bool _initialized;
        private bool _disposed;
        private bool _jobActive;
        private int _scheduledCellCount;

        public ScatterSimulationBackendKind BackendKind => ScatterSimulationBackendKind.EntitiesDots;
        public bool IsInitialized => _initialized && !_disposed;
        public bool IsJobActive => !_disposed && _jobActive;
        public bool IsJobCompleted
        {
            get
            {
                if (_disposed || !_jobActive || !_entityManager.Exists(_requestEntity))
                    return false;

                return _simulationJobHandle.IsCompleted;
            }
        }

        public void Initialize()
        {
            if (_disposed || _initialized)
                return;

            _world = new EntitiesWorld("Hecton8.ScatterEntitiesSimulationWorld");
            _entityManager = _world.EntityManager;

            EntityArchetype archetype = _entityManager.CreateArchetype(
                typeof(ScatterEntitiesSimulationRequest),
                typeof(ScatterEntitiesSimulationStatus),
                typeof(ScatterEntitiesScopeState),
                typeof(ScatterEntitiesQuotaState),
                typeof(ScatterEntitiesHeightSampleElement),
                typeof(ScatterEntitiesCellStateElement),
                typeof(ScatterEntitiesCandidateElement));

            _requestEntity = _entityManager.CreateEntity(archetype);
            _initialized = true;
        }

        public bool TrySchedule(
            ScatterSimulationConfig config,
            NativeArray<float> heightSamples,
            NativeArray<ScatterSimulationCellState> cellStates)
        {
            if (!IsInitialized || _jobActive || !_entityManager.Exists(_requestEntity) || !heightSamples.IsCreated || !cellStates.IsCreated || heightSamples.Length == 0)
                return false;

            EnsureJobCapacity(heightSamples.Length);

            DynamicBuffer<ScatterEntitiesHeightSampleElement> heightBuffer = _entityManager.GetBuffer<ScatterEntitiesHeightSampleElement>(_requestEntity);
            DynamicBuffer<ScatterEntitiesCellStateElement> cellStateBuffer = _entityManager.GetBuffer<ScatterEntitiesCellStateElement>(_requestEntity);
            DynamicBuffer<ScatterEntitiesCandidateElement> candidateBuffer = _entityManager.GetBuffer<ScatterEntitiesCandidateElement>(_requestEntity);
            heightBuffer.Clear();
            cellStateBuffer.Clear();
            candidateBuffer.Clear();
            heightBuffer.EnsureCapacity(heightSamples.Length);
            cellStateBuffer.EnsureCapacity(heightSamples.Length);

            for (int i = 0; i < heightSamples.Length; i++)
            {
                ScatterSimulationCellState cellState = i < cellStates.Length
                    ? cellStates[i]
                    : BuildCellState(config, i, heightSamples[i]);
                heightBuffer.Add(new ScatterEntitiesHeightSampleElement { Value = heightSamples[i] });
                cellStateBuffer.Add(new ScatterEntitiesCellStateElement
                {
                    Value = cellState
                });

                _jobCellStates[i] = cellState;
            }

            _entityManager.SetComponentData(_requestEntity, new ScatterEntitiesSimulationRequest
            {
                Config = config,
                HeightSampleCount = heightSamples.Length
            });
            _entityManager.SetComponentData(_requestEntity, new ScatterEntitiesScopeState
            {
                EligibilityMask = config.DefaultEligibility,
                DefaultSuppressionState = config.DefaultSuppressionState,
                DirtyFlags = config.DirtyFlags
            });
            _entityManager.SetComponentData(_requestEntity, new ScatterEntitiesQuotaState
            {
                Value = config.QuotaState
            });

            _entityManager.SetComponentData(_requestEntity, new ScatterEntitiesSimulationStatus
            {
                CandidateCount = 0,
                ScheduledCellCount = heightSamples.Length,
                Completed = 0
            });

            _scheduledCellCount = heightSamples.Length;
            ScatterSimulationDirtyFlags activeDirtyFlags = config.DirtyFlags;
            _simulationJobHandle = new ScatterEntitiesCandidateJob
            {
                Cells = _jobCellStates,
                CandidateSlots = _jobCandidateSlots,
                Config = config,
                QuotaState = config.QuotaState,
                ActiveDirtyFlags = activeDirtyFlags,
                CellCount = _scheduledCellCount
            }.Schedule(_scheduledCellCount, 32);
            _jobActive = true;
            return true;
        }

        public bool TryComplete(out ScatterSimulationResult result)
        {
            result = default;
            if (!IsInitialized || !_jobActive || !_entityManager.Exists(_requestEntity) || !_simulationJobHandle.IsCompleted)
                return false;

            _simulationJobHandle.Complete();
            ScatterSimulationParitySnapshot paritySnapshot = CompleteScheduledSimulation();
            result = new ScatterSimulationResult(_resultCandidates, paritySnapshot.CandidateCount, paritySnapshot);
            _jobActive = false;
            _scheduledCellCount = 0;
            return true;
        }

        public void ForceComplete()
        {
            if (!IsInitialized || !_jobActive)
                return;

            _simulationJobHandle.Complete();
            _jobActive = false;
            _scheduledCellCount = 0;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            if (_jobActive)
                _simulationJobHandle.Complete();

            if (_resultCandidates.IsCreated)
                _resultCandidates.Dispose();
            if (_jobCellStates.IsCreated)
                _jobCellStates.Dispose();
            if (_jobCandidateSlots.IsCreated)
                _jobCandidateSlots.Dispose();

            if (_world != null && _world.IsCreated)
                _world.Dispose();

            _requestEntity = Entity.Null;
            _disposed = true;
            _initialized = false;
            _jobActive = false;
        }

        private ScatterSimulationParitySnapshot CompleteScheduledSimulation()
        {
            ScatterEntitiesSimulationStatus status = _entityManager.GetComponentData<ScatterEntitiesSimulationStatus>(_requestEntity);
            DynamicBuffer<ScatterEntitiesCandidateElement> candidates = _entityManager.GetBuffer<ScatterEntitiesCandidateElement>(_requestEntity);
            candidates.Clear();
            int totalSlotCount = math.max(0, _scheduledCellCount * MaxCandidatesPerCell);
            candidates.EnsureCapacity(totalSlotCount);
            EnsureResultCapacity(totalSlotCount);

            ScatterSimulationParitySnapshot paritySnapshot = default;
            BuildCellParitySnapshot(_jobCellStates, _scheduledCellCount, ref paritySnapshot);

            int candidateCount = 0;
            for (int i = 0; i < totalSlotCount; i++)
            {
                ScatterSimulationCandidate candidate = _jobCandidateSlots[i];
                if (!candidate.IsValid)
                    continue;

                _resultCandidates[candidateCount++] = candidate;
                candidates.Add(new ScatterEntitiesCandidateElement
                {
                    Position = candidate.Position,
                    Rotation = candidate.Rotation,
                    Scale = candidate.Scale,
                    CellKey = candidate.CellKey,
                    FamilyIndex = candidate.FamilyIndex,
                    LayerIndex = candidate.LayerIndex,
                    Score = candidate.Score,
                    HeightSource = candidate.HeightSource,
                    IsValid = 1
                });
                AccumulateCandidateParity(ref paritySnapshot, candidate);
            }

            paritySnapshot.CandidateCount = candidateCount;
            status.CandidateCount = candidateCount;
            status.Completed = 1;
            _entityManager.SetComponentData(_requestEntity, status);
            return paritySnapshot;
        }

        private static ScatterSimulationCellState BuildCellState(
            ScatterSimulationConfig config,
            int cellIndex,
            float height)
        {
            int diameter = config.RadiusCells > 0 ? (config.RadiusCells * 2) + 1 : 1;
            int localX = config.RadiusCells > 0 ? (cellIndex % diameter) - config.RadiusCells : 0;
            int localZ = config.RadiusCells > 0 ? (cellIndex / diameter) - config.RadiusCells : 0;
            long cellKey = ((long)(uint)(localX & 0xFFFF) << 32) | (uint)(localZ & 0xFFFF);
            return new ScatterSimulationCellState
            {
                CellKey = cellKey,
                CellX = localX,
                CellZ = localZ,
                Height = height,
                HeightSource = HeightSourceEntities,
                Eligibility = config.DefaultEligibility,
                Suppression = config.DefaultSuppressionState,
                DirtyFlags = config.DirtyFlags
            };
        }

        private void EnsureJobCapacity(int cellCount)
        {
            int requiredCells = math.max(1, cellCount);
            int requiredCandidates = math.max(1, requiredCells * MaxCandidatesPerCell);
            EnsureCellStateCapacity(requiredCells);
            EnsureCandidateSlotCapacity(requiredCandidates);
        }

        private void EnsureCellStateCapacity(int requiredLength)
        {
            if (_jobCellStates.IsCreated && _jobCellStates.Length >= requiredLength)
                return;

            if (_jobCellStates.IsCreated)
                _jobCellStates.Dispose();

            // COLD ALLOC: NativeArray<ScatterSimulationCellState>[NextPowerOfTwo(requiredLength)] - entities scatter cell-state job input - owner: ScatterEntitiesSimulationBackend
            _jobCellStates = new NativeArray<ScatterSimulationCellState>(
                math.max(1, math.ceilpow2(requiredLength)),
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
        }

        private void EnsureCandidateSlotCapacity(int requiredLength)
        {
            if (_jobCandidateSlots.IsCreated && _jobCandidateSlots.Length >= requiredLength)
                return;

            if (_jobCandidateSlots.IsCreated)
                _jobCandidateSlots.Dispose();

            // COLD ALLOC: NativeArray<ScatterSimulationCandidate>[NextPowerOfTwo(requiredLength)] - entities scatter candidate job output slots - owner: ScatterEntitiesSimulationBackend
            _jobCandidateSlots = new NativeArray<ScatterSimulationCandidate>(
                math.max(1, math.ceilpow2(requiredLength)),
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
        }

        private static void BuildCellParitySnapshot(
            NativeArray<ScatterSimulationCellState> cells,
            int cellCount,
            ref ScatterSimulationParitySnapshot snapshot)
        {
            ulong hash = FnvOffset;
            for (int i = 0; i < cellCount; i++)
            {
                ScatterSimulationCellState cell = cells[i];
                if ((cell.Eligibility & ScatterSimulationEligibilityFlags.Ground) != 0)
                    snapshot.EligibleGroundCells++;
                if ((cell.Eligibility & ScatterSimulationEligibilityFlags.Cluster) != 0)
                    snapshot.EligibleClusterCells++;
                if ((cell.Eligibility & ScatterSimulationEligibilityFlags.Structure) != 0)
                    snapshot.EligibleStructureCells++;
                if ((cell.Eligibility & ScatterSimulationEligibilityFlags.Spawn) != 0)
                    snapshot.EligibleSpawnCells++;
                if (cell.DirtyFlags != ScatterSimulationDirtyFlags.None)
                    snapshot.DirtyCellCount++;
                if (cell.Suppression == ScatterSimulationSuppressionState.Suppressed)
                    snapshot.SuppressedCellCount++;

                hash = HashCombine(hash, (ulong)cell.CellKey);
                hash = HashCombine(hash, (ulong)(uint)cell.HeightSource);
                hash = HashCombine(hash, math.asuint(cell.Height));
                hash = HashCombine(hash, (ulong)(uint)cell.Eligibility);
                hash = HashCombine(hash, (ulong)(uint)cell.Suppression);
                hash = HashCombine(hash, (ulong)(uint)cell.DirtyFlags);
            }

            snapshot.CellChecksum = hash;
        }

        private static void AccumulateCandidateParity(
            ref ScatterSimulationParitySnapshot snapshot,
            ScatterSimulationCandidate candidate)
        {
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

            ulong hash = snapshot.CandidateChecksum == 0UL ? FnvOffset : snapshot.CandidateChecksum;
            hash = HashCombine(hash, (ulong)candidate.CellKey);
            hash = HashCombine(hash, (ulong)(uint)candidate.LayerIndex);
            snapshot.CandidateChecksum = hash;
        }

        private static ulong HashCombine(ulong hash, ulong value)
        {
            return (hash ^ value) * FnvPrime;
        }

        private static void TryWriteLayerCandidate(
            NativeArray<ScatterSimulationCandidate> candidates,
            int slotIndex,
            float3 basePosition,
            long cellKey,
            ScatterSimulationLayerQuota quota,
            ScatterSimulationEligibilityFlags requiredEligibility,
            ScatterSimulationCellState cellState,
            int cellIndex,
            int layerIndex,
            ref uint seed,
            float scoreScale)
        {
            candidates[slotIndex] = default;
            if ((cellState.Eligibility & requiredEligibility) == 0)
                return;

            int familyIndex = quota.FamilyIndex;
            if (familyIndex < 0)
                return;

            bool shouldEmit = layerIndex switch
            {
                0 => quota.PlacementsPerCell > 0,
                1 => quota.PlacementsPerCell > 0 && (cellIndex % 2) == 0,
                _ => quota.CellStride > 0 && (cellIndex % math.max(1, quota.CellStride)) == 0
            };
            if (!shouldEmit)
                return;

            float random01 = NextRandom01(ref seed);
            float rotation = random01 * 360f;
            float scale = 0.85f + (NextRandom01(ref seed) * 0.5f);
            float offsetX = (NextRandom01(ref seed) - 0.5f) * 2f;
            float offsetZ = (NextRandom01(ref seed) - 0.5f) * 2f;

            candidates[slotIndex] = new ScatterSimulationCandidate
            {
                Position = basePosition + new float3(offsetX, 0f, offsetZ),
                Rotation = rotation,
                Scale = scale,
                CellKey = cellKey,
                FamilyIndex = familyIndex,
                LayerIndex = layerIndex,
                Score = random01 * scoreScale,
                HeightSource = cellState.HeightSource,
                IsValid = true
            };
        }

        private void EnsureResultCapacity(int requiredLength)
        {
            if (requiredLength <= 0)
                requiredLength = 1;

            if (_resultCandidates.IsCreated && _resultCandidates.Length >= requiredLength)
                return;

            if (_resultCandidates.IsCreated)
                _resultCandidates.Dispose();

            // COLD ALLOC: NativeArray<ScatterSimulationCandidate>[NextPowerOfTwo(requiredLength)] - entities scatter simulation results - owner: ScatterEntitiesSimulationBackend
            _resultCandidates = new NativeArray<ScatterSimulationCandidate>(
                math.max(1, math.ceilpow2(requiredLength)),
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
        }

        private static float NextRandom01(ref uint state)
        {
            state = (state * 1664525u) + 1013904223u;
            return (state & 0x00FFFFFFu) / 16777215f;
        }

        private struct ScatterEntitiesCandidateJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<ScatterSimulationCellState> Cells;
            [NativeDisableParallelForRestriction] public NativeArray<ScatterSimulationCandidate> CandidateSlots;
            public ScatterSimulationConfig Config;
            public ScatterSimulationQuotaState QuotaState;
            public ScatterSimulationDirtyFlags ActiveDirtyFlags;
            public int CellCount;

            public void Execute(int index)
            {
                int baseSlot = index * MaxCandidatesPerCell;
                CandidateSlots[baseSlot + 0] = default;
                CandidateSlots[baseSlot + 1] = default;
                CandidateSlots[baseSlot + 2] = default;
                CandidateSlots[baseSlot + 3] = default;

                if (index < 0 || index >= CellCount)
                    return;

                ScatterSimulationDirtyFlags refreshMask = ScatterSimulationDirtyFlags.FullRebuild
                    | ScatterSimulationDirtyFlags.Candidates
                    | ScatterSimulationDirtyFlags.Heights
                    | ScatterSimulationDirtyFlags.Eligibility
                    | ScatterSimulationDirtyFlags.Quotas
                    | ScatterSimulationDirtyFlags.Suppression;
                if ((ActiveDirtyFlags & refreshMask) == 0)
                    return;

                ScatterSimulationCellState cellState = Cells[index];
                if ((cellState.DirtyFlags & ActiveDirtyFlags) == 0 && (ActiveDirtyFlags & ScatterSimulationDirtyFlags.FullRebuild) == 0)
                    return;

                if (cellState.Suppression == ScatterSimulationSuppressionState.Suppressed)
                    return;

                float3 basePosition = Config.PlayerPosition + new float3(
                    cellState.CellX * Config.CellSize,
                    cellState.Height + Config.SurfaceYOffset,
                    cellState.CellZ * Config.CellSize);
                long cellKey = cellState.CellKey;
                uint cellSeed = Config.Seed ^ ((uint)(index + 1) * 747796405u);

                TryWriteLayerCandidate(CandidateSlots, baseSlot + 0, basePosition, cellKey, QuotaState.Ground, ScatterSimulationEligibilityFlags.Ground, cellState, index, 0, ref cellSeed, 1f);
                TryWriteLayerCandidate(CandidateSlots, baseSlot + 1, basePosition, cellKey, QuotaState.Cluster, ScatterSimulationEligibilityFlags.Cluster, cellState, index, 1, ref cellSeed, 0.75f);
                TryWriteLayerCandidate(CandidateSlots, baseSlot + 2, basePosition, cellKey, QuotaState.Structure, ScatterSimulationEligibilityFlags.Structure, cellState, index, 2, ref cellSeed, 0.5f);
                TryWriteLayerCandidate(CandidateSlots, baseSlot + 3, basePosition, cellKey, QuotaState.Spawn, ScatterSimulationEligibilityFlags.Spawn, cellState, index, 3, ref cellSeed, 0.35f);
            }
        }
    }
}
