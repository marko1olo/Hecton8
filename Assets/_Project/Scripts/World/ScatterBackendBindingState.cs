using System;
using Unity.Collections;
using UnityEngine;
using ScatterWorkingMemory = Hecton8.World.WorldProceduralScatterDirector.ScatterWorkingMemory;

namespace Hecton8.World
{
    /// <summary>
    /// Owner-local binding state for the scatter backend seam.
    /// The current shadow backend is fail-closed, so this class retains only lookup metadata.
    /// </summary>
    internal sealed class ScatterBackendBindingState : IDisposable
    {
        public ScatterBackendBindingState()
        {
            ResetLookup();
        }

        public int GroundFamilyIndex { get; private set; }
        public int ClusterFamilyIndex { get; private set; }
        public int StructureFamilyIndex { get; private set; }
        public int SpawnFamilyIndex { get; private set; }

        public NativeArray<float>.ReadOnly HeightSamples => default;
        public NativeArray<ScatterSimulationCellState>.ReadOnly CellStates => default;

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

            return Mathf.Min(cellCount, Mathf.Min(memory.CellSamplingOutputs.Length, memory.ScatterBackendCellStates.Length)) > 0;
        }

        public void Dispose()
        {
            ResetLookup();
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
