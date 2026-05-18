using Unity.Mathematics;

namespace Hecton8.Thermodynamics
{
    /// <summary>
    /// Blind local source seeding used when WFC/geology hazard producers are absent.
    /// </summary>
    public static class MockHazardGenerator
    {
        public const uint MockHeatSourceId = 0x54484D48u;
        public const uint MockRadiationSourceId = 0x54484D52u;

        /// <summary>
        /// Seeds one 1000C thermal source and one radiation leak into the caller-owned source array.
        /// </summary>
        public static int GenerateEmergencyMockSources(
            ref HazardSourceDTO heat,
            ref HazardSourceDTO radiation,
            double3 gridOriginAup,
            float cellSizeMeters,
            uint heatHash,
            uint radiationHash)
        {
            double3 center = gridOriginAup + new double3(cellSizeMeters * 2.5, cellSizeMeters * 2.0, cellSizeMeters * 2.5);
            heat = new HazardSourceDTO
            {
                AUP = center,
                Intensity = 1000f,
                Radius = math.max(4f, cellSizeMeters * 2f),
                HazardTypeHash = heatHash,
                _pad0 = MockHeatSourceId
            };

            radiation = new HazardSourceDTO
            {
                AUP = gridOriginAup + new double3(cellSizeMeters * 4.5, cellSizeMeters * 1.5, cellSizeMeters * 4.5),
                Intensity = 1f,
                Radius = math.max(6f, cellSizeMeters * 3f),
                HazardTypeHash = radiationHash,
                _pad0 = MockRadiationSourceId
            };

            return 2;
        }
    }
}
