using Hecton8.Core.Contracts;

namespace Hecton8.VFX.Sonar
{
    public static class ActiveSonarGeoIlluminationAssemblyMarker
    {
        public const int MaxActivePings = 4;
        public const float SoundSpeedMetersPerSecond = HectonPhysicsContract.SoundSpeedWaterMetersPerSecondConst;
        public const float MaxRangeMeters = 400f;
    }
}
