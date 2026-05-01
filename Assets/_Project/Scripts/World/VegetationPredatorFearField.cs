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
        /// <summary>
        /// Records a temporary species-scoped predator fear sector at the snapped AUP ecosystem cell center.
        /// </summary>
        public void RegisterPredatorFearNode(int speciesId, Vector3 worldPosition, float normalizedDamage)
        {
            if (speciesId == 0 || normalizedDamage < 0.3f)
                return;

            EnsurePredatorFearMemoryBuffers();
            float currentTime = _predatorFearSimulationTime;
            CompactPredatorFearNodes(currentTime);

            float normalizedWeight = Mathf.Clamp01((normalizedDamage - 0.3f) / 0.7f);
            if (normalizedWeight <= 0f)
                return;

            float3 sectorCenter = ResolvePredatorFearSectorCenter(worldPosition);
            float sectorRadius = Mathf.Max(1f, predatorFearNodeRadiusMeters);
            float expireTime = currentTime + Mathf.Max(120f, predatorFearLifetimeSeconds);

            for (int i = 0; i < _predatorFearNodeCount; i++)
            {
                PredatorFearNodeState node = _predatorFearNodes[i];
                if (node.SpeciesId != speciesId)
                    continue;

                float2 delta = new float2(node.Position.x - sectorCenter.x, node.Position.z - sectorCenter.z);
                if (math.lengthsq(delta) > 1f)
                    continue;

                node.Position = sectorCenter;
                node.Radius = Mathf.Max(node.Radius, sectorRadius);
                node.Weight = Mathf.Max(node.Weight, normalizedWeight);
                node.ExpireTime = Mathf.Max(node.ExpireTime, expireTime);
                _predatorFearNodes[i] = node;
                if (!_abyssalPathScheduled)
                    SyncPredatorFearNodeSnapshot(currentTime);
                return;
            }

            int writeIndex = _predatorFearNodeCount < _predatorFearNodes.Length
                ? _predatorFearNodeCount
                : FindWeakestPredatorFearNodeIndex(currentTime);

            if (writeIndex < 0)
                writeIndex = 0;

            _predatorFearNodes[writeIndex] = new PredatorFearNodeState
            {
                Position = sectorCenter,
                Radius = sectorRadius,
                Weight = normalizedWeight,
                ExpireTime = expireTime,
                SpeciesId = speciesId
            };

            _predatorFearNodeCount = Mathf.Min(_predatorFearNodes.Length, Mathf.Max(_predatorFearNodeCount, writeIndex + 1));
            if (!_abyssalPathScheduled)
                SyncPredatorFearNodeSnapshot(currentTime);
        }

        /// <summary>
        /// Samples the current species-scoped predator fear pressure at a world position.
        /// </summary>
        public float SamplePredatorFearPressure(Vector3 worldPosition, int speciesId)
        {
            if (speciesId == 0 || _predatorFearNodeCount <= 0)
                return 0f;

            float currentTime = _predatorFearSimulationTime;
            float pressure = 0f;
            float lifetime = Mathf.Max(120f, predatorFearLifetimeSeconds);
            float3 position = new float3(worldPosition.x, worldPosition.y, worldPosition.z);
            for (int i = 0; i < _predatorFearNodeCount; i++)
            {
                PredatorFearNodeState node = _predatorFearNodes[i];
                if (node.SpeciesId != speciesId || node.ExpireTime <= currentTime)
                    continue;

                float2 delta = new float2(position.x - node.Position.x, position.z - node.Position.z);
                float radius = math.max(node.Radius, 1f);
                float gate = 1f - math.saturate(math.length(delta) / radius);
                if (gate <= 0f)
                    continue;

                float freshness = math.saturate((node.ExpireTime - currentTime) / lifetime);
                pressure = math.max(pressure, node.Weight * freshness * gate);
            }

            return Mathf.Clamp01(pressure * predatorFearCognitionPressureScale);
        }

        private void EnsurePredatorFearMemoryBuffers()
        {
            int safeCapacity = Mathf.Clamp(predatorFearNodeCapacity, 4, 128);
            if (_predatorFearNodes == null || _predatorFearNodes.Length != safeCapacity)
            {
                // COLD ALLOC: PredatorFearNodeState[safeCapacity] - bounded predator fear-sector memory aligned to ecosystem threat routing - owner: HectonMapMagicVegetationBridge
                PredatorFearNodeState[] resized = new PredatorFearNodeState[safeCapacity];
                int copyCount = Mathf.Min(_predatorFearNodeCount, resized.Length);
                if (_predatorFearNodes != null && copyCount > 0)
                    Array.Copy(_predatorFearNodes, resized, copyCount);

                _predatorFearNodes = resized;
                _predatorFearNodeCount = copyCount;
            }

            if (!_nativeMemory.PredatorFearNodesSnapshotNative.IsCreated || _nativeMemory.PredatorFearNodesSnapshotNative.Length != safeCapacity)
            {
                DisposeNativeArray(ref _nativeMemory.PredatorFearNodesSnapshotNative);
                // COLD ALLOC: NativeArray<PredatorFearNodeSnapshot>[safeCapacity] - path-job snapshot of predator fear memory - owner: HectonMapMagicVegetationBridge
                _nativeMemory.PredatorFearNodesSnapshotNative = new NativeArray<PredatorFearNodeSnapshot>(safeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            }
        }

        private void CompactPredatorFearNodes(float currentTime)
        {
            if (_predatorFearNodeCount <= 0 || _predatorFearNodes == null)
            {
                _predatorFearNodeCount = 0;
                return;
            }

            int writeIndex = 0;
            for (int i = 0; i < _predatorFearNodeCount; i++)
            {
                PredatorFearNodeState node = _predatorFearNodes[i];
                if (node.SpeciesId == 0 || node.ExpireTime <= currentTime || node.Weight <= 0f)
                    continue;

                if (writeIndex != i)
                    _predatorFearNodes[writeIndex] = node;

                writeIndex++;
            }

            _predatorFearNodeCount = writeIndex;
        }

        private int FindWeakestPredatorFearNodeIndex(float currentTime)
        {
            if (_predatorFearNodes == null || _predatorFearNodes.Length == 0)
                return -1;

            int weakestIndex = 0;
            float weakestScore = float.MaxValue;
            float lifetime = Mathf.Max(120f, predatorFearLifetimeSeconds);
            int count = Mathf.Min(_predatorFearNodes.Length, Mathf.Max(_predatorFearNodeCount, 1));
            for (int i = 0; i < count; i++)
            {
                PredatorFearNodeState node = _predatorFearNodes[i];
                float freshness = Mathf.Clamp01((node.ExpireTime - currentTime) / lifetime);
                float score = node.SpeciesId == 0 ? -1f : node.Weight * freshness;
                if (score < weakestScore)
                {
                    weakestScore = score;
                    weakestIndex = i;
                }
            }

            return weakestIndex;
        }

        private float3 ResolvePredatorFearSectorCenter(Vector3 worldPosition)
        {
            float sectorSize = Mathf.Max(100f, predatorFearSectorSizeMeters);
            return new float3(
                Mathf.Round(worldPosition.x / sectorSize) * sectorSize,
                worldPosition.y,
                Mathf.Round(worldPosition.z / sectorSize) * sectorSize);
        }

        private void SyncPredatorFearNodeSnapshot(float currentTime)
        {
            if (!_nativeMemory.PredatorFearNodesSnapshotNative.IsCreated)
                return;

            CompactPredatorFearNodes(currentTime);
            float lifetime = Mathf.Max(120f, predatorFearLifetimeSeconds);
            int safeLength = _nativeMemory.PredatorFearNodesSnapshotNative.Length;
            int activeCount = Mathf.Min(_predatorFearNodeCount, safeLength);
            for (int i = 0; i < safeLength; i++)
            {
                PredatorFearNodeSnapshot snapshot = default;
                if (i < activeCount)
                {
                    PredatorFearNodeState node = _predatorFearNodes[i];
                    float freshness = Mathf.Clamp01((node.ExpireTime - currentTime) / lifetime);
                    snapshot.Position = node.Position;
                    snapshot.Radius = node.Radius;
                    snapshot.Weight = node.Weight * freshness;
                    snapshot.SpeciesId = node.SpeciesId;
                    snapshot.Padding = 0f;
                }

                _nativeMemory.PredatorFearNodesSnapshotNative[i] = snapshot;
            }

            UploadPredatorFearShaderPayload(activeCount);
        }

        private void UploadPredatorFearShaderPayload(int activeCount)
        {
            int safeCount = Mathf.Max(1, activeCount);
            EnsurePredatorFearShaderBuffer(safeCount);
            if (_predatorFearNodeBuffer == null || !_nativeMemory.PredatorFearNodesSnapshotNative.IsCreated)
                return;

            GraphicsBufferUploadUtility.UploadNativeArray(_predatorFearNodeBuffer, _nativeMemory.PredatorFearNodesSnapshotNative, safeCount);
            Shader.SetGlobalBuffer(_PredatorFearNodeBufferId, _predatorFearNodeBuffer);
            Shader.SetGlobalInt(_PredatorFearNodeCountId, activeCount);
        }

        private void EnsurePredatorFearShaderBuffer(int requiredCount)
        {
            if (_predatorFearNodeBuffer != null && _predatorFearNodeBuffer.count >= requiredCount)
                return;

            ReleaseBuffer(ref _predatorFearNodeBuffer);
            _predatorFearNodeBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<PredatorFearNodeSnapshot>(requiredCount); // COLD ALLOC: GraphicsBuffer[requiredCount] - global predator-fear StructuredBuffer for flora stealth dimming - owner: HectonMapMagicVegetationBridge
        }
    }
}
