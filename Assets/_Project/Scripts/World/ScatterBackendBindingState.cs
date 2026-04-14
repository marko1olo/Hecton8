using System;
using Unity.Collections;
using UnityEngine;
using ScatterWorkingMemory = Hecton8.World.WorldProceduralScatterDirector.ScatterWorkingMemory;

namespace Hecton8.World
{
    /// <summary>
    /// Owner-local binding state for the scatter backend seam.
    /// Keeps representative layer-family indices and height-sample bridge out of the director's ad-hoc field set.
    /// </summary>
    internal sealed class ScatterBackendBindingState : IDisposable
    {
        private NativeArray<float> _heightSamples;
        private NativeArray<ScatterSimulationCellState> _cellStates;

        public ScatterBackendBindingState()
        {
            ResetLookup();
        }

        public int GroundFamilyIndex { get; private set; }
        public int ClusterFamilyIndex { get; private set; }
        public int StructureFamilyIndex { get; private set; }
        public int SpawnFamilyIndex { get; private set; }

        public NativeArray<float> HeightSamples => _heightSamples;
        public NativeArray<ScatterSimulationCellState> CellStates => _cellStates;

        public void ResetLookup()
        {
            GroundFamilyIndex = -1;
            ClusterFamilyIndex = -1;
            StructureFamilyIndex = -1;
            SpawnFamilyIndex = -1;
        }

        public bool TryRegisterRepresentativeFamilyIndex(
            WorldPrefabFamilyProfile family,
            int familyIndex)
        {
            if (family == null || familyIndex == 0)
                return false;

            return RegisterRepresentativeFamilyIndex(family, familyIndex);
        }

        public bool TryPopulateCellData(ScatterWorkingMemory memory, int cellCount)
        {
            if (memory == null || !memory.CellSamplingOutputs.IsCreated || !memory.ScatterBackendCellStates.IsCreated || cellCount <= 0)
                return false;

            EnsureHeightSampleCapacity(cellCount);
            EnsureCellStateCapacity(cellCount);
            if (!_heightSamples.IsCreated || !_cellStates.IsCreated)
                return false;

            int copyCount = Mathf.Min(cellCount, Mathf.Min(memory.CellSamplingOutputs.Length, memory.ScatterBackendCellStates.Length));
            for (int i = 0; i < copyCount; i++)
            {
                _heightSamples[i] = memory.CellSamplingOutputs[i].SeafloorHeight;
                _cellStates[i] = memory.ScatterBackendCellStates[i];
            }

            return copyCount > 0;
        }

        public void Dispose()
        {
            if (_heightSamples.IsCreated)
                _heightSamples.Dispose();
            if (_cellStates.IsCreated)
                _cellStates.Dispose();

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

        private void EnsureCellStateCapacity(int requiredCapacity)
        {
            if (requiredCapacity <= 0)
                return;

            if (_cellStates.IsCreated && _cellStates.Length >= requiredCapacity)
                return;

            if (_cellStates.IsCreated)
                _cellStates.Dispose();

            // COLD ALLOC: NativeArray<ScatterSimulationCellState>[NextPowerOfTwo(requiredCapacity)] - scatter backend cell-state bridge - owner: ScatterBackendBindingState
            _cellStates = new NativeArray<ScatterSimulationCellState>(
                Mathf.NextPowerOfTwo(requiredCapacity),
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
        }

        private bool RegisterRepresentativeFamilyIndex(WorldPrefabFamilyProfile family, int familyIndex)
        {
            switch (family.scatterLayer)
            {
                case WorldPrefabFamilyProfile.ScatterLayer.Ground:
                    if (GroundFamilyIndex < 0)
                    {
                        GroundFamilyIndex = familyIndex;
                        return true;
                    }

                    return false;
                case WorldPrefabFamilyProfile.ScatterLayer.Cluster:
                    if (ClusterFamilyIndex < 0)
                    {
                        ClusterFamilyIndex = familyIndex;
                        return true;
                    }

                    return false;
                case WorldPrefabFamilyProfile.ScatterLayer.Structure:
                    if (StructureFamilyIndex < 0)
                    {
                        StructureFamilyIndex = familyIndex;
                        return true;
                    }

                    return false;
                case WorldPrefabFamilyProfile.ScatterLayer.Spawn:
                    if (SpawnFamilyIndex < 0)
                    {
                        SpawnFamilyIndex = familyIndex;
                        return true;
                    }

                    return false;
            }

            return false;
        }
    }
}
