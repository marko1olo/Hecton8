using System.Runtime.InteropServices;

namespace Hecton8.World
{
    internal static class EcosystemEnvelopeLayout
    {
        public const int EcosystemEnvelopeStrideBytes = 32;
    }

    /// <summary>
    /// O(1) environmental sample used by fauna spawn and logical-LOD decisions.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = EcosystemEnvelopeLayout.EcosystemEnvelopeStrideBytes)]
    internal readonly struct EcosystemEnvelope
    {
        [FieldOffset(0)] public readonly float TemperatureCelsius;
        [FieldOffset(4)] public readonly float DepthMeters;
        [FieldOffset(8)] public readonly float LightExposure01;
        [FieldOffset(12)] public readonly float BloodScent01;
        [FieldOffset(16)] public readonly float ExhaustScent01;
        [FieldOffset(20)] public readonly float FearScent01;
        [FieldOffset(24)] public readonly float Hostility01;
        [FieldOffset(28)] private readonly uint _pad0;

        public EcosystemEnvelope(
            float temperatureCelsius,
            float depthMeters,
            float lightExposure01,
            float bloodScent01,
            float exhaustScent01,
            float fearScent01,
            float hostility01)
        {
            TemperatureCelsius = temperatureCelsius;
            DepthMeters = depthMeters;
            LightExposure01 = lightExposure01;
            BloodScent01 = bloodScent01;
            ExhaustScent01 = exhaustScent01;
            FearScent01 = fearScent01;
            Hostility01 = hostility01;
            _pad0 = 0u;
        }
    }
}
