using System.Runtime.InteropServices;

namespace Hecton8.World
{
    /// <summary>
    /// O(1) environmental sample used by fauna spawn and logical-LOD decisions.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct EcosystemEnvelope
    {
        public readonly float TemperatureCelsius;
        public readonly float DepthMeters;
        public readonly float LightExposure01;
        public readonly float BloodScent01;
        public readonly float ExhaustScent01;
        public readonly float FearScent01;
        public readonly float Hostility01;

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
        }
    }
}
