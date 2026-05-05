using Unity.Collections;
using Unity.Mathematics;
using Hecton8.Core;

namespace Hecton8.World
{
    /// <summary>
    /// Cold-path bridge from anomaly feature records to runtime resource binding.
    /// </summary>
    public static class HectonAnomalyResourceBinding
    {
        /// <summary>
        /// Registers valid chthonic pillar records with <see cref="ResourceDistributionDirector"/> for deterministic resource spawning.
        /// </summary>
        public static int TryBindChthonicPillarResources(
            NativeArray<AnomalyFeatureRecord> featureRecords,
            int maxPillars = 64,
            float pillarRadiusMeters = 50f,
            float pillarHeightMeters = 1000f)
        {
            ResourceDistributionDirector director = GlobalRegistry.ResourceDistribution;
            if (director == null || !featureRecords.IsCreated || maxPillars <= 0)
                return 0;

            int safeMaxPillars = math.min(maxPillars, featureRecords.Length);
            int visitedPillars = 0;
            int spawnedResources = 0;
            for (int i = 0; i < featureRecords.Length && visitedPillars < safeMaxPillars; i++)
            {
                AnomalyFeatureRecord record = featureRecords[i];
                if (record.Valid == 0 || record.Kind != (byte)AnomalyFeatureKind.ChthonicPillar)
                    continue;

                visitedPillars++;
                spawnedResources += director.TryBindChthonicPillarResourcesAtAup(
                    new double3(record.AupX, record.AupY, record.AupZ),
                    pillarRadiusMeters,
                    pillarHeightMeters,
                    unchecked((uint)record.Index));
            }

            return spawnedResources;
        }
    }
}
