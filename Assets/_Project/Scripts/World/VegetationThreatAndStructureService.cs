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
        /// Returns the current threat level at the provided world-space position without allocations.
        /// </summary>
        public float GetThreatLevel(Vector3 position)
        {
            if (!_threatGridInitialized || !_nativeMemory.EcosystemThreatGridCurrentNative.IsCreated || _ecosystemThreatGridResolution <= 0)
                return 0f;

            return SampleThreatGridAtPosition(position, _ecosystemThreatGridCenter, threatGridCellSize, _ecosystemThreatGridResolution, _nativeMemory.EcosystemThreatGridCurrentNative);
        }

        internal void ApplyExternalThreatPulse(Vector3 position, float radius, float strength, float holdDuration)
        {
            if (!IsFinite(position) ||
                !math.isfinite(radius) ||
                !math.isfinite(strength) ||
                !math.isfinite(holdDuration))
            {
                return;
            }

            float resolvedRadius = math.max(0f, radius);
            float resolvedStrength = math.max(0f, strength);
            float resolvedHoldDuration = math.max(0.01f, holdDuration);
            if (resolvedRadius <= 0f || resolvedStrength <= 0f)
                return;

            bool overwritePulse =
                _externalThreatPulseHoldTimer <= 0f ||
                resolvedStrength >= _externalThreatPulseStrength;
            if (overwritePulse)
            {
                _externalThreatPulsePosition = position;
                _externalThreatPulseRadius = resolvedRadius;
                _externalThreatPulseStrength = resolvedStrength;
            }
            else
            {
                _externalThreatPulseRadius = math.max(_externalThreatPulseRadius, resolvedRadius);
                _externalThreatPulseStrength = math.max(_externalThreatPulseStrength, resolvedStrength);
            }

            _externalThreatPulseHoldTimer = math.max(_externalThreatPulseHoldTimer, resolvedHoldDuration);
        }

        /// <summary>
        /// Returns the highest stamped canopy obstacle Y at the given world-space XZ coordinate.
        /// </summary>
        public float GetCanopyHeightAt(float worldX, float worldZ)
        {
            if (!_canopyGridInitialized || !_nativeMemory.CanopyHeightGridNative.IsCreated || _canopyGridResolution <= 0)
                return float.NegativeInfinity;

            return SampleCanopyHeightAtPosition(worldX, worldZ);
        }

        /// <summary>
        /// Registers a persistent artificial structure bounds for threat damping and interior-aware navigation.
        /// </summary>
        public void RegisterArtificialStructure(Bounds bounds, StructureType type)
        {
            RegisterArtificialStructureHandle(bounds, type);
        }

        /// <summary>
        /// Registers a persistent artificial structure bounds and returns a stable runtime handle for removal.
        /// </summary>
        public int RegisterArtificialStructureHandle(Bounds bounds, StructureType type)
        {
            Vector3 center = bounds.center;
            Vector3 size = bounds.size;
            if (!IsFinite(center) ||
                !IsFinite(size) ||
                size.x <= 0f ||
                size.y <= 0f ||
                size.z <= 0f ||
                size.sqrMagnitude <= 0.0001f)
            {
                return InvalidArtificialStructureId;
            }

            for (int i = 0; i < _persistentArtificialStructures.Count; i++)
            {
                PersistentArtificialStructureRecord existing = _persistentArtificialStructures[i];
                if (existing.Type != type)
                    continue;

                if ((existing.Bounds.center - center).sqrMagnitude > 0.25f)
                    continue;

                if ((existing.Bounds.size - size).sqrMagnitude > 0.25f)
                    continue;

                Bounds previousBounds = existing.Bounds;
                existing.Bounds = bounds;
                _persistentArtificialStructures[i] = existing;
                InvalidateChunksIntersectingBounds(previousBounds);
                InvalidateChunksIntersectingBounds(bounds);
                RefreshArtificialStructureSnapshotIfIdle();
                RefreshResidency();
                return existing.StructureId;
            }

            int structureId = _nextArtificialStructureId++;
            _persistentArtificialStructures.Add(new PersistentArtificialStructureRecord
            {
                StructureId = structureId,
                Bounds = bounds,
                Type = type
            });

            InvalidateChunksIntersectingBounds(bounds);
            RefreshArtificialStructureSnapshotIfIdle();
            RefreshResidency();
            return structureId;
        }

        /// <summary>
        /// Unregisters a persistent artificial structure by stable runtime handle.
        /// </summary>
        public bool UnregisterArtificialStructure(int structureId)
        {
            if (structureId == InvalidArtificialStructureId || _persistentArtificialStructures.Count <= 0)
                return false;

            for (int i = 0; i < _persistentArtificialStructures.Count; i++)
            {
                PersistentArtificialStructureRecord structure = _persistentArtificialStructures[i];
                if (structure.StructureId != structureId)
                    continue;

                Bounds removedBounds = structure.Bounds;
                _persistentArtificialStructures.RemoveAt(i);
                InvalidateChunksIntersectingBounds(removedBounds);
                RefreshArtificialStructureSnapshotIfIdle();
                RefreshResidency();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns a multiplicative predator spawn-weight modifier derived from the local threat field.
        /// 1.0 = neutral, 4.0 = +300% predator weight at maximum threat.
        /// </summary>
        public float GetSpawnWeightModifier(Vector3 position)
        {
            float threat = GetThreatLevel(position);
            if (threat <= 0f)
                return 1f;

            return 1f + (math.saturate(threat) * predatorSpawnThreatBonusMultiplier);
        }

        /// <summary>
        /// Returns true when the provided world-space position falls inside a permanent threat-echo cell.
        /// </summary>
        public bool HasPermanentThreatEcho(Vector3 position)
        {
            if (!_threatGridInitialized ||
                !_nativeMemory.EcosystemThreatEchoCurrentNative.IsCreated ||
                _ecosystemThreatGridResolution <= 0)
            {
                return false;
            }

            return SampleThreatEchoFlagAtPosition(position, _ecosystemThreatGridCenter, threatGridCellSize, _ecosystemThreatGridResolution, _nativeMemory.EcosystemThreatEchoCurrentNative) != 0;
        }

        /// <summary>
        /// Returns a local techno-jungle regrowth modifier derived from permanent threat echoes.
        /// 0 = no extra regrowth pressure, 1 = full echo-driven bio-cable boost.
        /// </summary>
        public float GetTechnoJungleEchoInfluence(Vector3 position)
        {
            return HasPermanentThreatEcho(position) ? 1f : 0f;
        }

        /// <summary>
        /// Returns the current abyssal flow direction at the provided world-space position without allocations.
        /// </summary>
        public Vector3 GetFlowDirection(Vector3 position)
        {
            if (!IsFinite(position))
                return Vector3.zero;

            if (!_flowFieldInitialized || !_nativeMemory.EcosystemFlowFieldCurrentNative.IsCreated || _ecosystemThreatGridResolution <= 0)
            {
                if (!TryResolvePlayerRuntimePositionFromAup(out Vector3 playerRuntimePosition) ||
                    !IsFinite(playerRuntimePosition))
                {
                    return Vector3.zero;
                }

                float3 toPlayer = new float3(
                    playerRuntimePosition.x - position.x,
                    0f,
                    playerRuntimePosition.z - position.z);
                float distanceSq = math.lengthsq(toPlayer);
                if (distanceSq <= 0.0001f || !math.isfinite(distanceSq))
                    return Vector3.zero;

                float3 direction = toPlayer * math.rsqrt(distanceSq);
                return new Vector3(direction.x, 0f, direction.z);
            }

            float2 flow = SampleFlowFieldAtPosition(position, _ecosystemFlowFieldCenter, threatGridCellSize, _ecosystemThreatGridResolution, _nativeMemory.EcosystemFlowFieldCurrentNative);
            return new Vector3(flow.x, 0f, flow.y);
        }

        /// <summary>
        /// Returns the strongest nearby abyssal conductor vector sampled from the immutable nav-graph snapshot.
        /// </summary>
        public Vector3 GetAbyssalConduitVector(Vector3 position)
        {
            if (_abyssalNavNodeCount <= 0 ||
                !_nativeMemory.AbyssalNavConduitVectorsSnapshotNative.IsCreated ||
                !_nativeMemory.AbyssalNavConduitStrengthSnapshotNative.IsCreated ||
                !IsFinite(position))
            {
                return Vector3.zero;
            }

            int nodeIndex = FindNearestAbyssalNavNodeIndex(position);
            if (nodeIndex < 0 ||
                nodeIndex >= _abyssalNavConduitVectorsSnapshot.Length ||
                nodeIndex >= _abyssalNavConduitStrengthSnapshot.Length ||
                nodeIndex >= _nativeMemory.AbyssalNavConduitVectorsSnapshotNative.Length ||
                nodeIndex >= _nativeMemory.AbyssalNavConduitStrengthSnapshotNative.Length)
            {
                return Vector3.zero;
            }

            Vector3 conduitVector = _abyssalNavConduitVectorsSnapshot[nodeIndex];
            float conduitStrength = _abyssalNavConduitStrengthSnapshot[nodeIndex];
            return conduitStrength > 0f && math.isfinite(conduitStrength) && IsFinite(conduitVector)
                ? conduitVector * conduitStrength
                : Vector3.zero;
        }

        /// <summary>
        /// Returns the resolved water temperature in Celsius at the provided world-space position without allocations.
        /// </summary>
        public bool TryResolveMegaWreckPrefab(int wreckId, out GameObject prefab)
        {
            prefab = null;
            if (megaWreckDefinitions == null || megaWreckDefinitions.Length == 0)
                return false;

            for (int i = 0; i < megaWreckDefinitions.Length; i++)
            {
                if (megaWreckDefinitions[i].WreckId != wreckId)
                    continue;

                prefab = megaWreckDefinitions[i].Prefab;
                return prefab != null;
            }

            return false;
        }

        /// <summary>
        /// Finds the strongest threat hotspot inside the requested distance band around the player.
        /// </summary>
        public bool TryGetThreatHotspot(
            float minimumThreatLevel,
            float minimumDistanceFromPlayer,
            float maximumDistanceFromPlayer,
            out Vector3 hotspotPosition,
            out float hotspotThreatLevel)
        {
            hotspotPosition = _currentThreatHotspotPosition;
            hotspotThreatLevel = 0f;
            if (!_threatGridInitialized ||
                !_nativeMemory.EcosystemThreatGridCurrentNative.IsCreated ||
                _ecosystemThreatGridResolution <= 0 ||
                threatGridCellSize <= 0f ||
                !math.isfinite(threatGridCellSize) ||
                !IsFinite(_ecosystemThreatGridCenter) ||
                !TryResolvePlayerRuntimePositionFromAup(out Vector3 playerPosition))
            {
                return false;
            }

            long expectedThreatGridLength = (long)_ecosystemThreatGridResolution * _ecosystemThreatGridResolution;
            if (expectedThreatGridLength <= 0L ||
                expectedThreatGridLength > int.MaxValue ||
                _nativeMemory.EcosystemThreatGridCurrentNative.Length < expectedThreatGridLength ||
                !IsFinite(playerPosition) ||
                !math.isfinite(minimumThreatLevel) ||
                !math.isfinite(minimumDistanceFromPlayer) ||
                !math.isfinite(maximumDistanceFromPlayer))
            {
                return false;
            }

            float minDistanceSq = math.max(0f, minimumDistanceFromPlayer) * math.max(0f, minimumDistanceFromPlayer);
            float maxDistance = math.max(minimumDistanceFromPlayer, maximumDistanceFromPlayer);
            float maxDistanceSq = maxDistance * maxDistance;
            if (!math.isfinite(minDistanceSq) || !math.isfinite(maxDistanceSq))
                return false;

            int halfExtent = _ecosystemThreatGridResolution >> 1;
            float bestThreat = minimumThreatLevel;
            Vector3 bestPosition = default;

            for (int z = 0; z < _ecosystemThreatGridResolution; z++)
            {
                float localZ = (z - halfExtent) * threatGridCellSize;
                for (int x = 0; x < _ecosystemThreatGridResolution; x++)
                {
                    int index = (z * _ecosystemThreatGridResolution) + x;
                    float threat = _nativeMemory.EcosystemThreatGridCurrentNative[index];
                    if (threat <= bestThreat || !math.isfinite(threat))
                        continue;

                    float localX = (x - halfExtent) * threatGridCellSize;
                    Vector3 candidate = new Vector3(
                        _ecosystemThreatGridCenter.x + localX,
                        playerPosition.y,
                        _ecosystemThreatGridCenter.z + localZ);

                    Vector3 delta = candidate - playerPosition;
                    float distanceSq = (delta.x * delta.x) + (delta.z * delta.z);
                    if (distanceSq < minDistanceSq || distanceSq > maxDistanceSq)
                        continue;

                    bestThreat = threat;
                    bestPosition = candidate;
                }
            }

            if (bestThreat <= minimumThreatLevel)
                return false;

            hotspotPosition = bestPosition;
            hotspotThreatLevel = bestThreat;
            return true;
        }

        /// <summary>
    }
}
