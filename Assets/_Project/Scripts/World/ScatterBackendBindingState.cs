using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using ScatterWorkingMemory = Hecton8.World.WorldProceduralScatterDirector.ScatterWorkingMemory;

namespace Hecton8.World
{
    /// <summary>
    /// Owner-local binding state for the scatter backend seam.
    /// Keeps family indices, prefab cache, and height-sample bridge out of the director's ad-hoc field set.
    /// </summary>
    internal sealed class ScatterBackendBindingState : IDisposable
    {
        private readonly Dictionary<int, WorldPrefabFamilyProfile> _familiesByIndex;
        private readonly Dictionary<int, GameObject> _representativePrefabs;
        private NativeArray<float> _heightSamples;

        public ScatterBackendBindingState()
        {
            // COLD ALLOC: Dictionary<int, WorldPrefabFamilyProfile>[128] — scatter backend family lookup — owner: ScatterBackendBindingState
            _familiesByIndex = new Dictionary<int, WorldPrefabFamilyProfile>(128);
            // COLD ALLOC: Dictionary<int, GameObject>[128] — scatter backend prefab cache — owner: ScatterBackendBindingState
            _representativePrefabs = new Dictionary<int, GameObject>(128);
            ResetLookup();
        }

        public int GroundFamilyIndex { get; private set; }
        public int ClusterFamilyIndex { get; private set; }
        public int StructureFamilyIndex { get; private set; }
        public int SpawnFamilyIndex { get; private set; }

        public NativeArray<float> HeightSamples => _heightSamples;

        public void ResetLookup()
        {
            _familiesByIndex.Clear();
            _representativePrefabs.Clear();
            GroundFamilyIndex = -1;
            ClusterFamilyIndex = -1;
            StructureFamilyIndex = -1;
            SpawnFamilyIndex = -1;
        }

        public bool TryRegisterFamily(
            WorldPrefabFamilyProfile family,
            int familyIndex,
            GameObject representativePrefab)
        {
            if (family == null || familyIndex == 0 || _familiesByIndex.ContainsKey(familyIndex))
                return false;

            _familiesByIndex.Add(familyIndex, family);
            _representativePrefabs.Add(familyIndex, representativePrefab);
            RegisterRepresentativeFamilyIndex(family, familyIndex);
            return true;
        }

        public bool TryResolveCachedPrefab(int familyIndex, int layerIndex, out GameObject prefab)
        {
            prefab = null;
            if (!_familiesByIndex.TryGetValue(familyIndex, out WorldPrefabFamilyProfile family) || family == null)
                return false;

            if ((int)family.scatterLayer != layerIndex)
                return false;

            return _representativePrefabs.TryGetValue(familyIndex, out prefab) && prefab != null;
        }

        public bool TryGetFamily(int familyIndex, out WorldPrefabFamilyProfile family)
        {
            return _familiesByIndex.TryGetValue(familyIndex, out family);
        }

        public void CacheRepresentativePrefab(int familyIndex, GameObject prefab)
        {
            if (familyIndex == 0)
                return;

            _representativePrefabs[familyIndex] = prefab;
        }

        public bool TryPopulateHeightSamples(ScatterWorkingMemory memory, int cellCount)
        {
            if (memory == null || !memory.CellSamplingOutputs.IsCreated || cellCount <= 0)
                return false;

            EnsureHeightSampleCapacity(cellCount);
            if (!_heightSamples.IsCreated)
                return false;

            int copyCount = Mathf.Min(cellCount, memory.CellSamplingOutputs.Length);
            for (int i = 0; i < copyCount; i++)
                _heightSamples[i] = memory.CellSamplingOutputs[i].SeafloorHeight;

            return copyCount > 0;
        }

        public void Dispose()
        {
            if (_heightSamples.IsCreated)
                _heightSamples.Dispose();

            ResetLookup();
        }

        private void EnsureHeightSampleCapacity(int requiredCapacity)
        {
            if (requiredCapacity <= 0)
                return;

            if (_heightSamples.IsCreated && _heightSamples.Length >= requiredCapacity)
                return;

            if (_heightSamples.IsCreated)
                _heightSamples.Dispose();

            // COLD ALLOC: NativeArray<float>[NextPowerOfTwo(requiredCapacity)] — scatter backend height bridge — owner: ScatterBackendBindingState
            _heightSamples = new NativeArray<float>(
                Mathf.NextPowerOfTwo(requiredCapacity),
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
        }

        private void RegisterRepresentativeFamilyIndex(WorldPrefabFamilyProfile family, int familyIndex)
        {
            switch (family.scatterLayer)
            {
                case WorldPrefabFamilyProfile.ScatterLayer.Ground:
                    if (GroundFamilyIndex < 0)
                        GroundFamilyIndex = familyIndex;
                    break;
                case WorldPrefabFamilyProfile.ScatterLayer.Cluster:
                    if (ClusterFamilyIndex < 0)
                        ClusterFamilyIndex = familyIndex;
                    break;
                case WorldPrefabFamilyProfile.ScatterLayer.Structure:
                    if (StructureFamilyIndex < 0)
                        StructureFamilyIndex = familyIndex;
                    break;
                case WorldPrefabFamilyProfile.ScatterLayer.Spawn:
                    if (SpawnFamilyIndex < 0)
                        SpawnFamilyIndex = familyIndex;
                    break;
            }
        }
    }
}
