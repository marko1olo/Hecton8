using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Habitat.Deformation.Contracts
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public readonly struct HabitatModuleDeformationSample
    {
        [FieldOffset(0)] public readonly uint NodeId;
        [FieldOffset(4)] public readonly uint ModuleHash;
        [FieldOffset(8)] public readonly float3 RuntimeCenter;
        [FieldOffset(20)] public readonly float Stress01;
        [FieldOffset(24)] public readonly float PeakStress01;
        [FieldOffset(28)] public readonly byte QualityTier;
        [FieldOffset(29)] private readonly byte _padding0;
        [FieldOffset(30)] private readonly byte _padding1;
        [FieldOffset(31)] private readonly byte _padding2;

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
            _padding0 = 0;
            _padding1 = 0;
            _padding2 = 0;
        }
    }

    public interface IHabitatModuleDeformationReadModel
    {
        int ModuleStressCount { get; }
        bool TryGetModuleStress(int stressIndex, out HabitatModuleDeformationSample sample);
    }
}
