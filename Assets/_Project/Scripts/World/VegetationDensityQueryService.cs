using System;
using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Environment;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    public sealed partial class HectonMapMagicVegetationBridge
    {

        private void RebuildDensityQuerySnapshot()
        {
            if (_selectedChunkCount <= 0)
            {
                _densityQueryChunkCount = 0;
                return;
            }

            EnsureDensityQueryCapacity(_selectedChunkCount);
            _densityQueryChunkLookup.Clear();
            for (int i = 0; i < _densityQueryChunkCount; i++)
                _densityQueryChunkLookup[_densityQueryChunkKeys[i]] = i;

            int nextChunkCount = 0;
            for (int i = 0; i < _selectedChunkCount; i++)
            {
                ChunkKey key = _selectedChunkKeys[i];
                if (!_chunkPayloads.TryGetValue(key, out ChunkPayload payload))
                    continue;

                int gridOffset = nextChunkCount * DensityGridCellCount;
                ClearDensityGridCells(_nativeMemory.DensityQueryGridScratchNative, gridOffset, DensityGridCellCount);
                ClearThreatAttractorGridCells(_nativeMemory.ThreatAttractorGridScratchNative, gridOffset, DensityGridCellCount);
                AccumulateChunkDensityGrid(payload, ref _nativeMemory.DensityQueryGridScratchNative, gridOffset);
                AccumulateChunkThreatAttractorGrid(payload, ref _nativeMemory.ThreatAttractorGridScratchNative, gridOffset);

                VegetationDensityChunkRecord record = new VegetationDensityChunkRecord
                {
                    MinX = payload.MinX,
                    MaxX = payload.MaxX,
                    MinZ = payload.MinZ,
                    MaxZ = payload.MaxZ,
                    GridOffset = gridOffset,
                    GrassLodTier = payload.GrassLodTier
                };

                if (_densityQueryChunkLookup.TryGetValue(key, out int previousIndex))
                {
                    VegetationDensityChunkRecord previousRecord = _nativeMemory.DensityQueryChunksNative[previousIndex];
                    if (previousRecord.GrassLodTier != payload.GrassLodTier)
                        BlendDensityGrid(_nativeMemory.DensityQueryGridNative, previousRecord.GridOffset, _nativeMemory.DensityQueryGridScratchNative, gridOffset, DensityGridCellCount, 0.35f);
                }

                _nativeMemory.DensityQueryChunksScratchNative[nextChunkCount] = record;
                _densityQueryChunkKeys[nextChunkCount] = key;
                nextChunkCount++;
            }

            SwapDensityQueryBuffers();
            for (int i = nextChunkCount; i < _densityQueryChunkCount; i++)
                _densityQueryChunkKeys[i] = default;

            _densityQueryChunkCount = nextChunkCount;
        }

        private void RebuildAbyssalAnchorSnapshot()
        {
            int anchorCount = 0;
            for (int i = 0; i < _selectedChunkCount; i++)
            {
                if (!_chunkPayloads.TryGetValue(_selectedChunkKeys[i], out ChunkPayload payload) || !payload.HasUnderwater)
                    continue;

                anchorCount += CountSemanticType(ResolveChunkPool(isSurface: false, payload), payload.UnderwaterOffset, payload.UnderwaterCount, (int)VegetationSemanticType.DeadZoneMassiveStructure);
            }

            _abyssalAnchorCount = anchorCount;
            if (anchorCount <= 0)
                return;

            EnsureVector3Capacity(ref _abyssalAnchorPositions, anchorCount);
            EnsureVector3NativeCapacity(ref _nativeMemory.AbyssalAnchorPositionsNative, anchorCount);
            EnsureAupNativeCapacity(ref _nativeMemory.AbyssalAnchorAupPositionsNative, anchorCount);
            int writeIndex = 0;
            for (int i = 0; i < _selectedChunkCount; i++)
            {
                if (!_chunkPayloads.TryGetValue(_selectedChunkKeys[i], out ChunkPayload payload) || !payload.HasUnderwater)
                    continue;

                CopySemanticAnchorPositions(
                    ResolveChunkPool(isSurface: false, payload),
                    payload.UnderwaterOffset,
                    payload.UnderwaterCount,
                    (int)VegetationSemanticType.DeadZoneMassiveStructure,
                    _abyssalAnchorPositions,
                    _nativeMemory.AbyssalAnchorPositionsNative,
                    _nativeMemory.AbyssalAnchorAupPositionsNative,
                    _totalUniverseOffsetDouble,
                    ref writeIndex);
            }
        }

        private void RebuildAbyssalNavNodeSnapshot()
        {
            InvalidateAbyssalPathState();
            int nodeCount = 0;
            for (int i = 0; i < _selectedChunkCount; i++)
            {
                ChunkKey key = _selectedChunkKeys[i];
                if (!_chunkAbyssalNavPayloads.TryGetValue(key, out ChunkAbyssalNavPayload payload) || payload.Count <= 0 || !payload.Nodes.IsCreated)
                    continue;

                nodeCount += payload.Count;
            }

            int fixedNodeCapacity = ResolveMaxAbyssalNavNodeCapacity();
            if (nodeCount > fixedNodeCapacity)
                nodeCount = fixedNodeCapacity;

            _abyssalNavNodeCount = nodeCount;
            if (nodeCount <= 0)
            {
                if (_nativeMemory.AbyssalNavNodes.IsCreated)
                    _nativeMemory.AbyssalNavNodes.Clear();

                _abyssalNavGraphOrigin = Vector3.zero;
                if (_nativeMemory.AbyssalNavGraphHashNative.IsCreated)
                    _nativeMemory.AbyssalNavGraphHashNative.Clear();
                return;
            }

            if (_nativeMemory.AbyssalNavNodes.IsCreated)
                _nativeMemory.AbyssalNavNodes.Clear();
            if (_nativeMemory.AbyssalNavGraphHashNative.IsCreated)
                _nativeMemory.AbyssalNavGraphHashNative.Clear();
            if (!EnsureAbyssalNavNodeListCapacity(nodeCount))
            {
                _abyssalNavNodeCount = 0;
                return;
            }

            EnsureVector3Capacity(ref _abyssalNavNodeSnapshot, fixedNodeCapacity);
            EnsureVector3Capacity(ref _abyssalNavConduitVectorsSnapshot, fixedNodeCapacity);
            EnsureFloatCapacity(ref _abyssalNavConduitStrengthSnapshot, fixedNodeCapacity);
            EnsureByteCapacity(ref _abyssalNavNodeTypesSnapshot, fixedNodeCapacity);
            EnsureVector3NativeCapacity(ref _nativeMemory.AbyssalNavNodeSnapshotNative, fixedNodeCapacity);
            EnsureVector3NativeCapacity(ref _nativeMemory.AbyssalNavConduitVectorsSnapshotNative, fixedNodeCapacity);
            EnsureFloatNativeCapacity(ref _nativeMemory.AbyssalNavConduitStrengthSnapshotNative, fixedNodeCapacity);
            EnsureByteNativeCapacity(ref _nativeMemory.AbyssalNavNodeTypesSnapshotNative, fixedNodeCapacity);
            if (!EnsureAbyssalNavGraphHashCapacity(nodeCount * 4))
            {
                _abyssalNavNodeCount = 0;
                return;
            }

            bool hasOrigin = false;
            Vector3 minNode = default;

            int writeIndex = 0;
            for (int i = 0; i < _selectedChunkCount && writeIndex < nodeCount; i++)
            {
                ChunkKey key = _selectedChunkKeys[i];
                if (!_chunkAbyssalNavPayloads.TryGetValue(key, out ChunkAbyssalNavPayload payload) || payload.Count <= 0 || !payload.Nodes.IsCreated)
                    continue;

                for (int nodeIndex = 0; nodeIndex < payload.Count && writeIndex < nodeCount; nodeIndex++)
                {
                    Vector3 node = payload.Nodes[nodeIndex];
                    Vector3 conduitVector = payload.ConduitVectors.IsCreated && nodeIndex < payload.ConduitVectors.Length
                        ? payload.ConduitVectors[nodeIndex]
                        : Vector3.zero;
                    float conduitStrength = payload.ConduitStrengths.IsCreated && nodeIndex < payload.ConduitStrengths.Length
                        ? payload.ConduitStrengths[nodeIndex]
                        : 0f;
                    byte nodeType = payload.NodeTypes.IsCreated && nodeIndex < payload.NodeTypes.Length
                        ? payload.NodeTypes[nodeIndex]
                        : (byte)NavNodeType.Water;
                    _nativeMemory.AbyssalNavNodes.AddNoResize(node);
                    _abyssalNavNodeSnapshot[writeIndex] = node;
                    _abyssalNavConduitVectorsSnapshot[writeIndex] = conduitVector;
                    _abyssalNavConduitStrengthSnapshot[writeIndex] = conduitStrength;
                    _abyssalNavNodeTypesSnapshot[writeIndex] = nodeType;
                    _nativeMemory.AbyssalNavNodeSnapshotNative[writeIndex] = node;
                    _nativeMemory.AbyssalNavConduitVectorsSnapshotNative[writeIndex] = conduitVector;
                    _nativeMemory.AbyssalNavConduitStrengthSnapshotNative[writeIndex] = conduitStrength;
                    _nativeMemory.AbyssalNavNodeTypesSnapshotNative[writeIndex] = nodeType;
                    if (!hasOrigin)
                    {
                        minNode = node;
                        hasOrigin = true;
                    }
                    else
                    {
                        minNode.x = Mathf.Min(minNode.x, node.x);
                        minNode.y = Mathf.Min(minNode.y, node.y);
                        minNode.z = Mathf.Min(minNode.z, node.z);
                    }
                    writeIndex++;
                }
            }

            _abyssalNavNodeCount = writeIndex;
            _abyssalNavGraphOrigin = hasOrigin ? minNode : Vector3.zero;
            if (_nativeMemory.AbyssalNavGraphHashNative.IsCreated)
            {
                _nativeMemory.AbyssalNavGraphHashNative.Clear();
                for (int i = 0; i < _abyssalNavNodeCount; i++)
                {
                    int key = ComputeAbyssalNavGraphHashKey(_abyssalNavNodeSnapshot[i], _abyssalNavGraphOrigin, abyssalNavGraphCellSize);
                    _nativeMemory.AbyssalNavGraphHashNative.Add(key, i);
                }
            }
        }

        private void RebuildMegaWreckStreamSnapshot()
        {
            int sectionCount = 0;
            for (int i = 0; i < _selectedChunkCount; i++)
            {
                ChunkKey key = _selectedChunkKeys[i];
                if (!_chunkMegaWreckPayloads.TryGetValue(key, out ChunkMegaWreckPayload payload) || payload.Count <= 0 || payload.Sections == null)
                    continue;

                sectionCount += payload.Count;
            }

            _megaWreckStreamCount = sectionCount;
            if (sectionCount <= 0)
                return;

            EnsureMegaWreckSectionCapacity(ref _megaWreckStreamSnapshot, sectionCount);
            EnsureNativeCapacity(ref _nativeMemory.MegaWreckStreamSnapshotNative, sectionCount);
            int writeIndex = 0;
            for (int i = 0; i < _selectedChunkCount; i++)
            {
                ChunkKey key = _selectedChunkKeys[i];
                if (!_chunkMegaWreckPayloads.TryGetValue(key, out ChunkMegaWreckPayload payload) || payload.Count <= 0 || payload.Sections == null)
                    continue;

                for (int sectionIndex = 0; sectionIndex < payload.Count; sectionIndex++)
                {
                    MegaWreckStreamSection section = payload.Sections[sectionIndex];
                    _megaWreckStreamSnapshot[writeIndex] = section;
                    _nativeMemory.MegaWreckStreamSnapshotNative[writeIndex] = section;
                    writeIndex++;
                }
            }
        }

        private void RebuildCanopyHeightGrid()
        {
            EnsureCanopyGridBuffer();
            _canopyGridCenter = playerTransform != null ? playerTransform.position : _ecosystemThreatGridCenter;
            if (!_nativeMemory.CanopyHeightGridNative.IsCreated || _canopyGridResolution <= 0)
            {
                _canopyGridInitialized = false;
                return;
            }

            for (int i = 0; i < _canopyGridCellCount; i++)
                _nativeMemory.CanopyHeightGridNative[i] = float.NegativeInfinity;

            for (int i = 0; i < _megaWreckStreamCount; i++)
            {
                MegaWreckStreamSection section = _megaWreckStreamSnapshot[i];
                Bounds bounds = GetMegaWreckSectionBounds(section);
                StampCanopyBounds(bounds.min.x, bounds.max.x, bounds.min.z, bounds.max.z, bounds.max.y);
            }

            StampCanopyFromChunkPool(useStructuralThickness: false);
            StampCanopyFromChunkPool(useStructuralThickness: true);
            _canopyGridInitialized = true;
        }

        private void StampCanopyFromChunkPool(bool useStructuralThickness)
        {
            if (_selectedChunkCount <= 0)
            {
                return;
            }

            for (int i = 0; i < _selectedChunkCount; i++)
            {
                if (!_chunkPayloads.TryGetValue(_selectedChunkKeys[i], out ChunkPayload payload))
                    continue;

                int offset = useStructuralThickness ? payload.UnderwaterOffset : payload.SurfaceOffset;
                int count = useStructuralThickness ? payload.UnderwaterCount : payload.SurfaceCount;
                if (count <= 0)
                    continue;

                NativeChunkPool pool = ResolveChunkPool(isSurface: !useStructuralThickness, payload);
                if (!pool.Matrices.IsCreated || !pool.SemanticTypes.IsCreated || !pool.Metadata.IsCreated)
                    continue;

                int end = Mathf.Min(pool.Matrices.Length, offset + count);
                for (int poolIndex = Mathf.Max(0, offset); poolIndex < end; poolIndex++)
                {
                    int semanticType = pool.SemanticTypes[poolIndex];
                    if (useStructuralThickness)
                    {
                        if (semanticType != (int)VegetationSemanticType.ColonyHullPlating &&
                            semanticType != (int)VegetationSemanticType.ColonySupportBeam &&
                            semanticType != (int)VegetationSemanticType.DeadZoneMassiveStructure)
                        {
                            continue;
                        }
                    }
                    else if (semanticType != (int)VegetationSemanticType.FloatingSargassum)
                    {
                        continue;
                    }

                    Vector3 position = ResolveRuntimePosition(pool.Matrices[poolIndex]);
                    HectonVegetationInstanceData metadata = pool.Metadata[poolIndex];
                    float halfExtent = Mathf.Max(2f, metadata.WidthScale * (useStructuralThickness ? canopyStructureThickness : canopySargassumThickness));
                    float canopyTopY = position.y + Mathf.Max(metadata.HeightScale, useStructuralThickness ? canopyStructureThickness : canopySargassumThickness);
                    StampCanopyBounds(
                        position.x - halfExtent,
                        position.x + halfExtent,
                        position.z - halfExtent,
                        position.z + halfExtent,
                        canopyTopY);
                }
            }
        }

        private void StampCanopyBounds(float minX, float maxX, float minZ, float maxZ, float canopyY)
        {
            if (!_nativeMemory.CanopyHeightGridNative.IsCreated || _canopyGridResolution <= 0)
                return;

            int halfExtent = _canopyGridResolution >> 1;
            int minCellX = Mathf.Clamp(Mathf.FloorToInt((minX - _canopyGridCenter.x) / canopyGridCellSize) + halfExtent, 0, _canopyGridResolution - 1);
            int maxCellX = Mathf.Clamp(Mathf.CeilToInt((maxX - _canopyGridCenter.x) / canopyGridCellSize) + halfExtent, 0, _canopyGridResolution - 1);
            int minCellZ = Mathf.Clamp(Mathf.FloorToInt((minZ - _canopyGridCenter.z) / canopyGridCellSize) + halfExtent, 0, _canopyGridResolution - 1);
            int maxCellZ = Mathf.Clamp(Mathf.CeilToInt((maxZ - _canopyGridCenter.z) / canopyGridCellSize) + halfExtent, 0, _canopyGridResolution - 1);
            for (int cellZ = minCellZ; cellZ <= maxCellZ; cellZ++)
            {
                int rowOffset = cellZ * _canopyGridResolution;
                for (int cellX = minCellX; cellX <= maxCellX; cellX++)
                {
                    int index = rowOffset + cellX;
                    if (canopyY > _nativeMemory.CanopyHeightGridNative[index])
                        _nativeMemory.CanopyHeightGridNative[index] = canopyY;
                }
            }
        }

        private void DistortAggregateFlowVectorsByThreat(ActiveAggregateNativeBufferSet buffers, int count)
        {
            if (!_threatGridInitialized ||
                !_nativeMemory.EcosystemThreatGridCurrentNative.IsCreated ||
                _ecosystemThreatGridResolution <= 0 ||
                count <= 0 ||
                threatWhirlpoolStrength <= 0f ||
                _currentThreatHotspotLevel < threatWhirlpoolThreshold)
            {
                return;
            }

            float radiusSq = threatWhirlpoolRadius * threatWhirlpoolRadius;
            for (int i = 0; i < count; i++)
            {
                Vector3 position = ResolveRuntimePosition(buffers.Matrices[i]);
                float localThreat = GetThreatLevel(position);
                if (localThreat < threatWhirlpoolThreshold)
                    continue;

                Vector3 radial = position - _currentThreatHotspotPosition;
                float radialSq = (radial.x * radial.x) + (radial.z * radial.z);
                if (radialSq <= 0.0001f || radialSq > radiusSq)
                    continue;

                float swirl01 = Mathf.Clamp01((localThreat - threatWhirlpoolThreshold) / Mathf.Max(0.01f, 1f - threatWhirlpoolThreshold));
                swirl01 *= 1f - Mathf.Clamp01(radialSq / radiusSq);
                Vector3 tangent = NormalizeVector3Fast(new Vector3(-radial.z, 0f, radial.x), Vector3.forward);
                Vector3 baseFlow = buffers.FlowVectors[i];
                float fakeMagnitude = Mathf.Max(EstimateLength3D(baseFlow), 1f);
                float blend = Mathf.Clamp01(swirl01 * threatWhirlpoolStrength);
                Vector3 distortedFlow = baseFlow + ((tangent * fakeMagnitude) - baseFlow) * blend;
                Vector2 distortedDirection = NormalizeFlowDirection(new Vector2(distortedFlow.x, distortedFlow.z));
                buffers.FlowVectors[i] = distortedFlow;
                buffers.FlowDirections[i] = distortedDirection;
            }
        }

        private void AccumulateChunkDensityGrid(ChunkPayload payload, ref NativeArray<float3> destination, int gridOffset)
        {
            float chunkWidth = Mathf.Max(0.01f, payload.MaxX - payload.MinX);
            float chunkDepth = Mathf.Max(0.01f, payload.MaxZ - payload.MinZ);
            float cellArea = (chunkWidth / DensityGridResolution) * (chunkDepth / DensityGridResolution);
            float safeCellArea = Mathf.Max(0.0001f, cellArea);

            if (payload.SurfaceCount > 0)
            {
                float grassArea = GetGrassStepForTier(payload.GrassLodTier);
                grassArea *= grassArea;
                AccumulateChunkDensityGridFromSlice(
                    ResolveChunkPool(isSurface: true, payload),
                    payload.SurfaceOffset,
                    payload.SurfaceCount,
                    payload.MinX,
                    payload.MaxX,
                    payload.MinZ,
                    payload.MaxZ,
                    safeCellArea,
                    grassArea,
                    kelpStepMeters * kelpStepMeters,
                    floatingStepMeters * floatingStepMeters,
                    ref destination,
                    gridOffset);
            }

            if (payload.UnderwaterCount > 0)
            {
                float kelpArea = kelpStepMeters * kelpStepMeters;
                AccumulateChunkDensityGridFromSlice(
                    ResolveChunkPool(isSurface: false, payload),
                    payload.UnderwaterOffset,
                    payload.UnderwaterCount,
                    payload.MinX,
                    payload.MaxX,
                    payload.MinZ,
                    payload.MaxZ,
                    safeCellArea,
                    grassStepMeters * grassStepMeters,
                    kelpArea,
                    floatingStepMeters * floatingStepMeters,
                    ref destination,
                    gridOffset);
            }
        }

        private void AccumulateChunkThreatAttractorGrid(ChunkPayload payload, ref NativeArray<float2> destination, int gridOffset)
        {
            float chunkWidth = Mathf.Max(0.01f, payload.MaxX - payload.MinX);
            float chunkDepth = Mathf.Max(0.01f, payload.MaxZ - payload.MinZ);
            float cellArea = (chunkWidth / DensityGridResolution) * (chunkDepth / DensityGridResolution);
            float safeCellArea = Mathf.Max(0.0001f, cellArea);

            if (payload.HasSurface)
            {
                float grassArea = GetGrassStepForTier(payload.GrassLodTier);
                grassArea *= grassArea;
                AccumulateChunkThreatAttractorGridFromSlice(
                    ResolveChunkPool(isSurface: true, payload),
                    payload.SurfaceOffset,
                    payload.SurfaceCount,
                    payload.MinX,
                    payload.MaxX,
                    payload.MinZ,
                    payload.MaxZ,
                    safeCellArea,
                    grassArea,
                    kelpStepMeters * kelpStepMeters,
                    floatingStepMeters * floatingStepMeters,
                    ref destination,
                    gridOffset);
            }

            if (payload.HasUnderwater)
            {
                float kelpArea = kelpStepMeters * kelpStepMeters;
                AccumulateChunkThreatAttractorGridFromSlice(
                    ResolveChunkPool(isSurface: false, payload),
                    payload.UnderwaterOffset,
                    payload.UnderwaterCount,
                    payload.MinX,
                    payload.MaxX,
                    payload.MinZ,
                    payload.MaxZ,
                    safeCellArea,
                    grassStepMeters * grassStepMeters,
                    kelpArea,
                    floatingStepMeters * floatingStepMeters,
                    ref destination,
                    gridOffset);
            }
        }

        private void AccumulateChunkDensityGridFromSlice(
            NativeChunkPool pool,
            int offset,
            int count,
            float minX,
            float maxX,
            float minZ,
            float maxZ,
            float cellArea,
            float grassRepresentedArea,
            float kelpRepresentedArea,
            float sargassumRepresentedArea,
            ref NativeArray<float3> destination,
            int gridOffset)
        {
            float width = Mathf.Max(0.01f, maxX - minX);
            float depth = Mathf.Max(0.01f, maxZ - minZ);
            float inverseWidth = 1f / width;
            float inverseDepth = 1f / depth;
            for (int i = 0; i < count; i++)
            {
                int poolIndex = offset + i;
                double xDouble = pool.Matrices[poolIndex].m03 + _totalUniverseOffsetDouble.x;
                double zDouble = pool.Matrices[poolIndex].m23 + _totalUniverseOffsetDouble.z;
                if (xDouble < minX || xDouble > maxX || zDouble < minZ || zDouble > maxZ)
                    continue;

                float x = (float)xDouble;
                float z = (float)zDouble;
                int type = pool.Types[poolIndex];
                float normalizedX = Mathf.Clamp01((x - minX) * inverseWidth) * (DensityGridResolution - 1);
                float normalizedZ = Mathf.Clamp01((z - minZ) * inverseDepth) * (DensityGridResolution - 1);
                int cellX = Mathf.Clamp(Mathf.FloorToInt(normalizedX), 0, DensityGridResolution - 1);
                int cellZ = Mathf.Clamp(Mathf.FloorToInt(normalizedZ), 0, DensityGridResolution - 1);
                int nextCellX = Mathf.Min(cellX + 1, DensityGridResolution - 1);
                int nextCellZ = Mathf.Min(cellZ + 1, DensityGridResolution - 1);
                float fracX = normalizedX - cellX;
                float fracZ = normalizedZ - cellZ;

                float representedArea = ResolveRepresentedArea(type, grassRepresentedArea, kelpRepresentedArea, sargassumRepresentedArea);
                float edgeCompensation = ResolveEdgeCompensation(pool.EdgeDistances[poolIndex]);
                float densityWeight = (representedArea / cellArea) * edgeCompensation;
                float3 channel = ResolveDensityChannel(type, densityWeight);
                AddDensityCell(ref destination, gridOffset, cellX, cellZ, channel * ((1f - fracX) * (1f - fracZ)));
                AddDensityCell(ref destination, gridOffset, nextCellX, cellZ, channel * (fracX * (1f - fracZ)));
                AddDensityCell(ref destination, gridOffset, cellX, nextCellZ, channel * ((1f - fracX) * fracZ));
                AddDensityCell(ref destination, gridOffset, nextCellX, nextCellZ, channel * (fracX * fracZ));
            }
        }

        private void AccumulateChunkThreatAttractorGridFromSlice(
            NativeChunkPool pool,
            int offset,
            int count,
            float minX,
            float maxX,
            float minZ,
            float maxZ,
            float cellArea,
            float grassRepresentedArea,
            float kelpRepresentedArea,
            float sargassumRepresentedArea,
            ref NativeArray<float2> destination,
            int gridOffset)
        {
            float width = Mathf.Max(0.01f, maxX - minX);
            float depth = Mathf.Max(0.01f, maxZ - minZ);
            float inverseWidth = 1f / width;
            float inverseDepth = 1f / depth;
            for (int i = 0; i < count; i++)
            {
                int poolIndex = offset + i;
                double xDouble = pool.Matrices[poolIndex].m03 + _totalUniverseOffsetDouble.x;
                double zDouble = pool.Matrices[poolIndex].m23 + _totalUniverseOffsetDouble.z;
                if (xDouble < minX || xDouble > maxX || zDouble < minZ || zDouble > maxZ)
                    continue;

                float x = (float)xDouble;
                float z = (float)zDouble;
                float normalizedX = Mathf.Clamp01((x - minX) * inverseWidth) * (DensityGridResolution - 1);
                float normalizedZ = Mathf.Clamp01((z - minZ) * inverseDepth) * (DensityGridResolution - 1);
                int cellX = Mathf.Clamp(Mathf.FloorToInt(normalizedX), 0, DensityGridResolution - 1);
                int cellZ = Mathf.Clamp(Mathf.FloorToInt(normalizedZ), 0, DensityGridResolution - 1);
                int nextCellX = Mathf.Min(cellX + 1, DensityGridResolution - 1);
                int nextCellZ = Mathf.Min(cellZ + 1, DensityGridResolution - 1);
                float fracX = normalizedX - cellX;
                float fracZ = normalizedZ - cellZ;

                int type = pool.Types[poolIndex];
                int semanticType = pool.SemanticTypes[poolIndex];
                float representedArea = ResolveRepresentedArea(type, grassRepresentedArea, kelpRepresentedArea, sargassumRepresentedArea);
                float edgeCompensation = ResolveEdgeCompensation(pool.EdgeDistances[poolIndex]);
                float densityWeight = (representedArea / cellArea) * edgeCompensation;
                float2 channel = ResolveThreatAttractorChannel(semanticType, densityWeight);
                if (math.lengthsq(channel) <= 0.000001f)
                    continue;

                AddThreatAttractorCell(ref destination, gridOffset, cellX, cellZ, channel * ((1f - fracX) * (1f - fracZ)));
                AddThreatAttractorCell(ref destination, gridOffset, nextCellX, cellZ, channel * (fracX * (1f - fracZ)));
                AddThreatAttractorCell(ref destination, gridOffset, cellX, nextCellZ, channel * ((1f - fracX) * fracZ));
                AddThreatAttractorCell(ref destination, gridOffset, nextCellX, nextCellZ, channel * (fracX * fracZ));
            }
        }

        private static float ResolveRepresentedArea(int type, float grassArea, float kelpArea, float sargassumArea)
        {
            switch ((HectonVegetationInstanceType)type)
            {
                case HectonVegetationInstanceType.Grass:
                    return grassArea;
                case HectonVegetationInstanceType.GiantKelp:
                    return kelpArea;
                case HectonVegetationInstanceType.Sargassum:
                    return sargassumArea;
                default:
                    return grassArea;
            }
        }

        private float ResolveEdgeCompensation(float edgeDistance)
        {
            if (edgeDitherDistance <= 0f || edgeDistance >= edgeDitherDistance)
                return 1f;

            float normalized = Mathf.Clamp01(edgeDistance / Mathf.Max(0.01f, edgeDitherDistance));
            return 1f / Mathf.Max(0.35f, normalized);
        }

        private static float3 ResolveDensityChannel(int type, float densityWeight)
        {
            return VegetationMath.ResolveDensityChannel(type, densityWeight);
        }

        private static float2 ResolveThreatAttractorChannel(int semanticType, float densityWeight)
        {
            return VegetationMath.ResolveThreatAttractorChannel(semanticType, densityWeight);
        }

        private float EvaluateVisibilityModifier(Vector3 position, float3 densityChannels)
        {
            return EvaluateVisibilityModifierStatic(
                position.y,
                densityChannels,
                grassVisibilityWeight,
                kelpVisibilityWeight,
                sargassumVisibilityWeight,
                waterLevel,
                floatingSurfaceOffset,
                sargassumVisibilityBand);
        }

        private float3 ResolveFallbackVisibilityChannels(Vector3 position, HectonVegetationInstanceType type)
        {
            switch (type)
            {
                case HectonVegetationInstanceType.Grass:
                    return new float3(0.18f, 0f, 0f);
                case HectonVegetationInstanceType.GiantKelp:
                    return new float3(0f, 0.24f, 0f);
                case HectonVegetationInstanceType.Sargassum:
                    return new float3(0f, 0f, 0.28f * EvaluateSargassumVerticalConcealment(position.y));
                default:
                    return float3.zero;
            }
        }

        private float EvaluateSargassumVerticalConcealment(float worldY)
        {
            return EvaluateSargassumVerticalConcealmentStatic(worldY, waterLevel, floatingSurfaceOffset, sargassumVisibilityBand);
        }

        private static float EvaluateVisibilityModifierStatic(
            float worldY,
            float3 densityChannels,
            float grassWeight,
            float kelpWeight,
            float sargassumWeight,
            float localWaterLevel,
            float localFloatingSurfaceOffset,
            float localSargassumVisibilityBand)
        {
            return VegetationMath.EvaluateVisibilityModifier(
                worldY,
                densityChannels,
                grassWeight,
                kelpWeight,
                sargassumWeight,
                localWaterLevel,
                localFloatingSurfaceOffset,
                localSargassumVisibilityBand);
        }

        private static float EvaluateSargassumVerticalConcealmentStatic(
            float worldY,
            float localWaterLevel,
            float localFloatingSurfaceOffset,
            float localSargassumVisibilityBand)
        {
            return VegetationMath.EvaluateSargassumVerticalConcealment(
                worldY,
                localWaterLevel,
                localFloatingSurfaceOffset,
                localSargassumVisibilityBand);
        }

        private static void AddDensityCell(ref NativeArray<float3> destination, int gridOffset, int cellX, int cellZ, float3 value)
        {
            int index = gridOffset + (cellZ * DensityGridResolution) + cellX;
            destination[index] = destination[index] + value;
        }

        private static void AddThreatAttractorCell(ref NativeArray<float2> destination, int gridOffset, int cellX, int cellZ, float2 value)
        {
            int index = gridOffset + (cellZ * DensityGridResolution) + cellX;
            destination[index] = destination[index] + value;
        }

        private static void ClearDensityGridCells(NativeArray<float3> destination, int startIndex, int count)
        {
            for (int i = 0; i < count; i++)
                destination[startIndex + i] = float3.zero;
        }

        private static void ClearThreatAttractorGridCells(NativeArray<float2> destination, int startIndex, int count)
        {
            for (int i = 0; i < count; i++)
                destination[startIndex + i] = float2.zero;
        }

        private static void BlendDensityGrid(
            NativeArray<float3> previous,
            int previousOffset,
            NativeArray<float3> current,
            int currentOffset,
            int count,
            float previousWeight)
        {
            float currentWeight = 1f - previousWeight;
            for (int i = 0; i < count; i++)
                current[currentOffset + i] = (previous[previousOffset + i] * previousWeight) + (current[currentOffset + i] * currentWeight);
        }

        private void SwapDensityQueryBuffers()
        {
            NativeArray<VegetationDensityChunkRecord> chunkSwap = _nativeMemory.DensityQueryChunksNative;
            _nativeMemory.DensityQueryChunksNative = _nativeMemory.DensityQueryChunksScratchNative;
            _nativeMemory.DensityQueryChunksScratchNative = chunkSwap;

            NativeArray<float3> gridSwap = _nativeMemory.DensityQueryGridNative;
            _nativeMemory.DensityQueryGridNative = _nativeMemory.DensityQueryGridScratchNative;
            _nativeMemory.DensityQueryGridScratchNative = gridSwap;

            NativeArray<float2> attractorSwap = _nativeMemory.ThreatAttractorGridNative;
            _nativeMemory.ThreatAttractorGridNative = _nativeMemory.ThreatAttractorGridScratchNative;
            _nativeMemory.ThreatAttractorGridScratchNative = attractorSwap;
        }

        private static float SampleDensityAtPosition(
            float3 position,
            int typeMask,
            NativeArray<VegetationDensityChunkRecord> chunks,
            NativeArray<float3> densityGrid,
            int chunkCount)
        {
            return VegetationMath.SampleDensityAtPosition(position, typeMask, chunks, densityGrid, chunkCount);
        }

        private static float3 SampleDensityChannelsAtPosition(
            float3 position,
            NativeArray<VegetationDensityChunkRecord> chunks,
            NativeArray<float3> densityGrid,
            int chunkCount)
        {
            return VegetationMath.SampleDensityChannelsAtPosition(position, chunks, densityGrid, chunkCount);
        }

        /// <summary>
        /// Samples only macro-flora biomass density (kelp plus sargassum) from the current resident chunk-density snapshot.
        /// </summary>
        public float SampleMacroFloraDensityImmediate(Vector3 positionWS)
        {
            return SampleBiomassDensityImmediate(positionWS, DensityTypeMaskKelp | DensityTypeMaskSargassum);
        }

        private static float3 SampleDensityChannelsAtPositionHashed(
            float3 position,
            NativeArray<VegetationDensityChunkRecord> chunks,
            NativeArray<float3> densityGrid,
            NativeParallelMultiHashMap<int, int> chunkHash,
            float3 gridCenter,
            float cellSize,
            int gridResolution,
            int chunkCount)
        {
            return VegetationMath.SampleDensityChannelsAtPositionHashed(
                position,
                chunks,
                densityGrid,
                chunkHash,
                gridCenter,
                cellSize,
                gridResolution,
                chunkCount);
        }

        private static float2 SampleThreatAttractorAtPosition(
            float3 position,
            NativeArray<VegetationDensityChunkRecord> chunks,
            NativeArray<float2> attractorGrid,
            int chunkCount)
        {
            return VegetationMath.SampleThreatAttractorAtPosition(position, chunks, attractorGrid, chunkCount);
        }

        private static float2 SampleThreatAttractorAtPositionHashed(
            float3 position,
            NativeArray<VegetationDensityChunkRecord> chunks,
            NativeArray<float2> attractorGrid,
            NativeParallelMultiHashMap<int, int> chunkHash,
            float3 gridCenter,
            float cellSize,
            int gridResolution,
            int chunkCount)
        {
            return VegetationMath.SampleThreatAttractorAtPositionHashed(
                position,
                chunks,
                attractorGrid,
                chunkHash,
                gridCenter,
                cellSize,
                gridResolution,
                chunkCount);
        }

        private static float3 SampleChunkDensityChannels(
            float worldX,
            float worldZ,
            VegetationDensityChunkRecord chunk,
            NativeArray<float3> densityGrid)
        {
            return VegetationMath.SampleChunkDensityChannels(worldX, worldZ, chunk, densityGrid);
        }

        private static float ApplyDensityTypeMask(float3 sample, int typeMask)
        {
            return VegetationMath.ApplyDensityTypeMask(sample, typeMask);
        }

        private bool TryBuildDensitySample(
            Vector3 positionWS,
            float3 densityChannels,
            out VegetationDensitySample sample)
        {
            if (IsInsideRegisteredTerrainHole(positionWS.x, positionWS.z))
            {
                sample = default;
                return false;
            }

            if (TryResolveDominantDensitySample(densityChannels, out HectonVegetationInstanceType type, out float density))
            {
                uint seed = ResolveWorldQuerySeed(positionWS);
                VegetationBiomeLayer biomeLayer = ResolveBiomeLayer(positionWS.y, seed);
                sample = new VegetationDensitySample(
                    true,
                    type,
                    ResolveSemanticType(type, biomeLayer, seed),
                    biomeLayer,
                    ResolveAcousticType(type, density),
                    density);
                return true;
            }

            sample = default;
            return false;
        }

        private bool TryResolveDominantDensitySample(
            float3 densityChannels,
            out HectonVegetationInstanceType type,
            out float density)
        {
            density = math.max(densityChannels.x, math.max(densityChannels.y, densityChannels.z));
            if (density <= 0f)
            {
                type = HectonVegetationInstanceType.Grass;
                return false;
            }

            if (densityChannels.z >= densityChannels.x && densityChannels.z >= densityChannels.y)
            {
                type = HectonVegetationInstanceType.Sargassum;
            }
            else if (densityChannels.y >= densityChannels.x)
            {
                type = HectonVegetationInstanceType.GiantKelp;
            }
            else
            {
                type = HectonVegetationInstanceType.Grass;
            }

            return true;
        }

        private static VegetationAcousticType ResolveAcousticType(HectonVegetationInstanceType type, float density)
        {
            if (density <= 0f)
                return VegetationAcousticType.Silence;

            return type == HectonVegetationInstanceType.Sargassum
                ? VegetationAcousticType.SargassumBubbles
                : VegetationAcousticType.VegetationRustle;
        }

        private uint ResolveWorldQuerySeed(Vector3 positionWS)
        {
            if (TryFindTileStateAtPosition(positionWS, out TileRuntimeState state) && state != null)
                return BuildDensityQuerySeed(state.TileX, state.TileZ, positionWS.x, positionWS.z);

            return BuildArbitraryWorldSeed(positionWS.x, positionWS.y, positionWS.z);
        }

        private VegetationBiomeLayer ResolveBiomeLayer(float worldY, uint seed)
        {
            float depth = math.max(0f, waterLevel - worldY);
            float halfBand = math.max(1f, verticalBiomeBlendBand * 0.5f);
            float firstBlendStart = colonyBiomeStartDepth - halfBand;
            float firstBlendEnd = colonyBiomeStartDepth + halfBand;
            if (depth <= firstBlendStart)
                return VegetationBiomeLayer.OrganicShelf;

            if (depth < firstBlendEnd)
            {
                float transition = math.saturate((depth - firstBlendStart) / math.max(0.01f, verticalBiomeBlendBand));
                return Hash01(seed ^ 0x6E624EB7u) < transition
                    ? VegetationBiomeLayer.ColonyGraveyard
                    : VegetationBiomeLayer.OrganicShelf;
            }

            float secondBlendStart = deadZoneStartDepth - halfBand;
            float secondBlendEnd = deadZoneStartDepth + halfBand;
            if (depth <= secondBlendStart)
                return VegetationBiomeLayer.ColonyGraveyard;

            if (depth < secondBlendEnd)
            {
                float transition = math.saturate((depth - secondBlendStart) / math.max(0.01f, verticalBiomeBlendBand));
                return Hash01(seed ^ 0xB5297A4Du) < transition
                    ? VegetationBiomeLayer.DeadZone
                    : VegetationBiomeLayer.ColonyGraveyard;
            }

            return VegetationBiomeLayer.DeadZone;
        }

        private static VegetationSemanticType ResolveSemanticType(
            HectonVegetationInstanceType renderType,
            VegetationBiomeLayer biomeLayer,
            uint seed)
        {
            switch (renderType)
            {
                case HectonVegetationInstanceType.Grass:
                    return VegetationSemanticType.OrganicGrass;
                case HectonVegetationInstanceType.Sargassum:
                    return VegetationSemanticType.FloatingSargassum;
                case HectonVegetationInstanceType.GiantKelp:
                    switch (biomeLayer)
                    {
                        case VegetationBiomeLayer.ColonyGraveyard:
                        {
                            float selector = Hash01(seed ^ 0x165667B1u);
                            if (selector < 0.34f)
                                return VegetationSemanticType.ColonyCable;
                            if (selector < 0.67f)
                                return VegetationSemanticType.ColonyHullPlating;

                            return VegetationSemanticType.ColonySupportBeam;
                        }
                        case VegetationBiomeLayer.DeadZone:
                            return VegetationSemanticType.DeadZoneMassiveStructure;
                        default:
                            return VegetationSemanticType.OrganicKelp;
                    }
                default:
                    return VegetationSemanticType.OrganicGrass;
            }
        }

        private void UpdateVegetationAudioHandoff()
        {
            if (playerTransform == null)
            {
                PublishVegetationAudioHandoff(0f, VegetationAcousticType.Silence, force: false);
                return;
            }

            float3 averagedChannels = SampleVegetationAudioDensity(playerTransform.position);
            float totalDensity = math.saturate(averagedChannels.x + averagedChannels.y + averagedChannels.z);
            VegetationAcousticType acousticType = VegetationAcousticType.Silence;

            if (TryResolveDominantDensitySample(averagedChannels, out HectonVegetationInstanceType dominantType, out float dominantDensity))
                acousticType = ResolveAcousticType(dominantType, dominantDensity);

            PublishVegetationAudioHandoff(totalDensity, acousticType, force: false);
        }

        private float3 SampleVegetationAudioDensity(Vector3 origin)
        {
            if (!_nativeMemory.DensityQueryChunksNative.IsCreated || !_nativeMemory.DensityQueryGridNative.IsCreated || _densityQueryChunkCount <= 0)
                return float3.zero;

            Vector3 forward = playerTransform != null ? playerTransform.forward : Vector3.forward;
            if (forward.sqrMagnitude <= 0.0001f)
                forward = Vector3.forward;

            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
                forward = Vector3.forward;

            forward.Normalize();
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);
            float3 sum = float3.zero;

            sum += SampleDensityChannelsAtPosition(new float3(origin.x, origin.y, origin.z), _nativeMemory.DensityQueryChunksNative, _nativeMemory.DensityQueryGridNative, _densityQueryChunkCount);
            Vector3 offset = forward * vegetationAudioProbeRadius;
            sum += SampleDensityChannelsAtPosition(new float3(origin.x + offset.x, origin.y + offset.y, origin.z + offset.z), _nativeMemory.DensityQueryChunksNative, _nativeMemory.DensityQueryGridNative, _densityQueryChunkCount);
            sum += SampleDensityChannelsAtPosition(new float3(origin.x - offset.x, origin.y - offset.y, origin.z - offset.z), _nativeMemory.DensityQueryChunksNative, _nativeMemory.DensityQueryGridNative, _densityQueryChunkCount);
            offset = right * vegetationAudioProbeRadius;
            sum += SampleDensityChannelsAtPosition(new float3(origin.x + offset.x, origin.y + offset.y, origin.z + offset.z), _nativeMemory.DensityQueryChunksNative, _nativeMemory.DensityQueryGridNative, _densityQueryChunkCount);
            sum += SampleDensityChannelsAtPosition(new float3(origin.x - offset.x, origin.y - offset.y, origin.z - offset.z), _nativeMemory.DensityQueryChunksNative, _nativeMemory.DensityQueryGridNative, _densityQueryChunkCount);

            return sum / (float)VegetationAudioProbeCount;
        }

        private void PublishVegetationAudioHandoff(float density, VegetationAcousticType acousticType, bool force)
        {
            _vegetationAudioDensity = Mathf.Clamp01(density);
            _vegetationAudioAcousticType = acousticType;
            GlobalVegetationAudioDensity = _vegetationAudioDensity;
            GlobalVegetationAcousticType = acousticType;

            Shader.SetGlobalFloat(_ShaderVegetationAudioDensityId, _vegetationAudioDensity);
            Shader.SetGlobalFloat(_ShaderVegetationAudioAcousticTypeId, (float)acousticType);

            if (!force &&
                Mathf.Abs(_lastPublishedVegetationAudioDensity - _vegetationAudioDensity) <= 0.01f &&
                _lastPublishedVegetationAudioAcousticType == acousticType)
            {
                return;
            }

            _lastPublishedVegetationAudioDensity = _vegetationAudioDensity;
            _lastPublishedVegetationAudioAcousticType = acousticType;

            if (vegetationAudioMixer == null)
                return;

            if (!string.IsNullOrEmpty(vegetationDensityMixerParameter))
                vegetationAudioMixer.SetFloat(vegetationDensityMixerParameter, _vegetationAudioDensity);

            if (!string.IsNullOrEmpty(vegetationAcousticTypeMixerParameter))
                vegetationAudioMixer.SetFloat(vegetationAcousticTypeMixerParameter, (float)acousticType);
        }

        private void ClearVegetationAudioHandoff()
        {
            PublishVegetationAudioHandoff(0f, VegetationAcousticType.Silence, force: true);
        }
    }
}
