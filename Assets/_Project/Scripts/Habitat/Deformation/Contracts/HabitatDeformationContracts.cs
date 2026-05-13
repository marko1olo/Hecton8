using Unity.Mathematics;

namespace Hecton8.Habitat.Deformation.Contracts
{
    public readonly struct HabitatModuleDeformationSample
    {
        public readonly uint NodeId;
        public readonly uint ModuleHash;
        public readonly float3 RuntimeCenter;
        public readonly float Stress01;
        public readonly float PeakStress01;
        public readonly byte QualityTier;

        public HabitatModuleDeformationSample(
            uint nodeId,
            uint moduleHash,
            float3 runtimeCenter,
            float stress01,
            float peakStress01,
            byte qualityTier)
        {
            NodeId = nodeId;
            ModuleHash = moduleHash;
            RuntimeCenter = runtimeCenter;
            Stress01 = math.saturate(stress01);
            PeakStress01 = math.saturate(peakStress01);
            QualityTier = qualityTier;
        }
    }

    public interface IHabitatModuleDeformationReadModel
    {
        int ModuleStressCount { get; }
        bool TryGetModuleStress(int stressIndex, out HabitatModuleDeformationSample sample);
    }
}
