using System;
using Unity.Collections;
using ScatterWorkingMemory = Hecton8.World.WorldProceduralScatterDirector.ScatterWorkingMemory;

namespace Hecton8.World
{
    /// <summary>
    /// Owner-local binding state for the scatter backend seam.
    /// Retains representative family lookup and read-only views into director-owned sampling buffers.
    /// </summary>
    internal sealed class ScatterBackendBindingState : IDisposable
    {
        private NativeArray<float>.ReadOnly _heightSamples;
        private NativeArray<ScatterSimulationCellState>.ReadOnly _cellStates;

        public ScatterBackendBindingState()
        {
            ResetLookup();
        }

        public int GroundFamilyIndex { get; private set; }
        public int ClusterFamilyIndex { get; private set; }
        public int StructureFamilyIndex { get; private set; }
        public int SpawnFamilyIndex { get; private set; }

        public NativeArray<float>.ReadOnly HeightSamples => _heightSamples;
        public NativeArray<ScatterSimulationCellState>.ReadOnly CellStates => _cellStates;

        public void ResetLookup()
        {
            GroundFamilyIndex = -1;
            ClusterFamilyIndex = -1;
            StructureFamilyIndex = -1;
            SpawnFamilyIndex = -1;
            ClearCellDataViews();
        }

        public bool TryRegisterRepresentativeFamilyIndex(
            WorldPrefabFamilyProfile family,
            int familyIndex)
        {
            if (family == null || familyIndex <= 0)
                return false;

            return RegisterRepresentativeFamilyIndex(family, familyIndex);
        }

        public bool TryPopulateCellData(ScatterWorkingMemory memory, int cellCount)
        {
            ClearCellDataViews();

            if (memory == null ||
                !memory.CellSamplingOutputs.IsCreated ||
                !memory.ScatterBackendHeightSamples.IsCreated ||
                !memory.ScatterBackendCellStates.IsCreated ||
                cellCount <= 0)
            {
                return false;
            }

            if (memory.CellSamplingOutputs.Length < cellCount ||
                memory.ScatterBackendHeightSamples.Length < cellCount ||
                memory.ScatterBackendCellStates.Length < cellCount)
            {
                return false;
            }

            _heightSamples = memory.ScatterBackendHeightSamples.GetSubArray(0, cellCount).AsReadOnly();
            _cellStates = memory.ScatterBackendCellStates.GetSubArray(0, cellCount).AsReadOnly();
            return true;
        }

        public void Dispose()
        {
            ResetLookup();
        }

        public void ClearCellDataViews()
        {
            _heightSamples = default;
            _cellStates = default;
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
