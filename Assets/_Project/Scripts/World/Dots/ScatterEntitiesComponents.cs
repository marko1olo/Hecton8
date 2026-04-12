using Unity.Entities;
using Unity.Mathematics;

namespace Hecton8.World.Dots
{
    internal struct ScatterEntitiesSimulationRequest : IComponentData
    {
        public ScatterSimulationConfig Config;
        public int HeightSampleCount;
    }

    internal struct ScatterEntitiesSimulationStatus : IComponentData
    {
        public int CandidateCount;
        public int ScheduledFrame;
        public byte Completed;
    }

    internal struct ScatterEntitiesHeightSampleElement : IBufferElementData
    {
        public float Value;
    }

    internal struct ScatterEntitiesCandidateElement : IBufferElementData
    {
        public float3 Position;
        public float Rotation;
        public float Scale;
        public long CellKey;
        public int FamilyIndex;
        public int LayerIndex;
        public float Score;
        public int HeightSource;
        public byte IsValid;
    }
}
