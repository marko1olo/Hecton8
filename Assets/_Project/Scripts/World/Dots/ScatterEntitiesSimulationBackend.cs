using System;
using Unity.Collections;
using Unity.Entities;
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

        private EntitiesWorld _world;
        private EntityManager _entityManager;
        private Entity _requestEntity;
        private NativeArray<ScatterSimulationCandidate> _resultCandidates;
        private bool _initialized;
        private bool _disposed;
        private bool _jobActive;

        public ScatterSimulationBackendKind BackendKind => ScatterSimulationBackendKind.EntitiesDots;
        public bool IsInitialized => _initialized && !_disposed;
        public bool IsJobActive => !_disposed && _jobActive;
        public bool IsJobCompleted
        {
            get
            {
                if (_disposed || !_jobActive || !_entityManager.Exists(_requestEntity))
                    return false;

                ScatterEntitiesSimulationStatus status = _entityManager.GetComponentData<ScatterEntitiesSimulationStatus>(_requestEntity);
                return Time.frameCount > status.ScheduledFrame;
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
                typeof(ScatterEntitiesHeightSampleElement),
                typeof(ScatterEntitiesCandidateElement));

            _requestEntity = _entityManager.CreateEntity(archetype);
            _initialized = true;
        }

        public bool TrySchedule(ScatterSimulationConfig config, NativeArray<float> heightSamples)
        {
            if (!IsInitialized || _jobActive || !_entityManager.Exists(_requestEntity) || !heightSamples.IsCreated || heightSamples.Length == 0)
                return false;

            DynamicBuffer<ScatterEntitiesHeightSampleElement> heightBuffer = _entityManager.GetBuffer<ScatterEntitiesHeightSampleElement>(_requestEntity);
            DynamicBuffer<ScatterEntitiesCandidateElement> candidateBuffer = _entityManager.GetBuffer<ScatterEntitiesCandidateElement>(_requestEntity);
            heightBuffer.Clear();
            candidateBuffer.Clear();
            heightBuffer.EnsureCapacity(heightSamples.Length);

            for (int i = 0; i < heightSamples.Length; i++)
                heightBuffer.Add(new ScatterEntitiesHeightSampleElement { Value = heightSamples[i] });

            _entityManager.SetComponentData(_requestEntity, new ScatterEntitiesSimulationRequest
            {
                Config = config,
                HeightSampleCount = heightSamples.Length
            });

            _entityManager.SetComponentData(_requestEntity, new ScatterEntitiesSimulationStatus
            {
                CandidateCount = 0,
                ScheduledFrame = Time.frameCount,
                Completed = 0
            });

            _jobActive = true;
            return true;
        }

        public bool TryComplete(out ScatterSimulationResult result)
        {
            result = default;
            if (!IsInitialized || !_jobActive || !_entityManager.Exists(_requestEntity))
                return false;

            RunSimulation();

            ScatterEntitiesSimulationStatus status = _entityManager.GetComponentData<ScatterEntitiesSimulationStatus>(_requestEntity);
            if (status.Completed == 0)
                return false;

            DynamicBuffer<ScatterEntitiesCandidateElement> candidateBuffer = _entityManager.GetBuffer<ScatterEntitiesCandidateElement>(_requestEntity);
            EnsureResultCapacity(candidateBuffer.Length);

            for (int i = 0; i < candidateBuffer.Length; i++)
            {
                ScatterEntitiesCandidateElement candidate = candidateBuffer[i];
                _resultCandidates[i] = new ScatterSimulationCandidate
                {
                    Position = candidate.Position,
                    Rotation = candidate.Rotation,
                    Scale = candidate.Scale,
                    CellKey = candidate.CellKey,
                    FamilyIndex = candidate.FamilyIndex,
                    LayerIndex = candidate.LayerIndex,
                    Score = candidate.Score,
                    HeightSource = candidate.HeightSource,
                    IsValid = candidate.IsValid != 0
                };
            }

            result = new ScatterSimulationResult(_resultCandidates, candidateBuffer.Length);
            _jobActive = false;
            return true;
        }

        public void ForceComplete()
        {
            if (!IsInitialized || !_jobActive)
                return;

            RunSimulation();
            _jobActive = false;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            if (_resultCandidates.IsCreated)
                _resultCandidates.Dispose();

            if (_world != null && _world.IsCreated)
                _world.Dispose();

            _requestEntity = Entity.Null;
            _disposed = true;
            _initialized = false;
            _jobActive = false;
        }

        private void RunSimulation()
        {
            ScatterEntitiesSimulationRequest request = _entityManager.GetComponentData<ScatterEntitiesSimulationRequest>(_requestEntity);
            ScatterEntitiesSimulationStatus status = _entityManager.GetComponentData<ScatterEntitiesSimulationStatus>(_requestEntity);
            if (status.Completed != 0)
                return;

            DynamicBuffer<ScatterEntitiesHeightSampleElement> heights = _entityManager.GetBuffer<ScatterEntitiesHeightSampleElement>(_requestEntity);
            DynamicBuffer<ScatterEntitiesCandidateElement> candidates = _entityManager.GetBuffer<ScatterEntitiesCandidateElement>(_requestEntity);
            candidates.Clear();

            ScatterSimulationConfig config = request.Config;
            int totalCells = math.min(request.HeightSampleCount, heights.Length);
            for (int i = 0; i < totalCells; i++)
            {
                float height = heights[i].Value;
                int localX = config.RadiusCells > 0 ? (i % ((config.RadiusCells * 2) + 1)) - config.RadiusCells : 0;
                int localZ = config.RadiusCells > 0 ? (i / ((config.RadiusCells * 2) + 1)) - config.RadiusCells : 0;
                float3 basePosition = config.PlayerPosition + new float3(localX * config.CellSize, height + config.SurfaceYOffset, localZ * config.CellSize);
                long cellKey = ((long)(uint)(localX & 0xFFFF) << 32) | (uint)(localZ & 0xFFFF);
                uint cellSeed = config.Seed ^ (uint)(i + 1) * 747796405u;

                TryAddCandidate(candidates, basePosition, cellKey, config.GroundFamilyIndex, 0, config.GroundPlacementsPerCell > 0, ref cellSeed, 1f);
                TryAddCandidate(candidates, basePosition, cellKey, config.ClusterFamilyIndex, 1, config.ClusterPlacementsPerCell > 0 && (i % 2) == 0, ref cellSeed, 0.75f);
                TryAddCandidate(candidates, basePosition, cellKey, config.StructureFamilyIndex, 2, config.StructureCellStride > 0 && (i % math.max(1, config.StructureCellStride)) == 0, ref cellSeed, 0.5f);
                TryAddCandidate(candidates, basePosition, cellKey, config.SpawnFamilyIndex, 3, config.SpawnCellStride > 0 && (i % math.max(1, config.SpawnCellStride)) == 0, ref cellSeed, 0.35f);
            }

            status.CandidateCount = candidates.Length;
            status.Completed = 1;
            _entityManager.SetComponentData(_requestEntity, status);
        }

        private static void TryAddCandidate(
            DynamicBuffer<ScatterEntitiesCandidateElement> candidates,
            float3 basePosition,
            long cellKey,
            int familyIndex,
            int layerIndex,
            bool shouldEmit,
            ref uint seed,
            float scoreScale)
        {
            if (!shouldEmit || familyIndex < 0)
                return;

            float random01 = NextRandom01(ref seed);
            float rotation = random01 * 360f;
            float scale = 0.85f + (NextRandom01(ref seed) * 0.5f);
            float offsetX = (NextRandom01(ref seed) - 0.5f) * 2f;
            float offsetZ = (NextRandom01(ref seed) - 0.5f) * 2f;

            candidates.Add(new ScatterEntitiesCandidateElement
            {
                Position = basePosition + new float3(offsetX, 0f, offsetZ),
                Rotation = rotation,
                Scale = scale,
                CellKey = cellKey,
                FamilyIndex = familyIndex,
                LayerIndex = layerIndex,
                Score = random01 * scoreScale,
                HeightSource = HeightSourceEntities,
                IsValid = 1
            });
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
    }
}
