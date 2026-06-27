using System;
using System.Numerics;

namespace Hecton8.PureLogic.Ecosystem
{
    /// <summary>
    /// Pure C# mathematical implementation for EcosystemLogicalLodTieringCalculator.
    /// Extracted from EcosystemDirector.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class EcosystemLogicalLodTieringCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="distanceSq">Parameter representing the distanceSq (float).</param>
        /// <param name="zone1RadiusSq">Parameter representing the zone1RadiusSq (float).</param>
        /// <param name="zone2RadiusSq">Parameter representing the zone2RadiusSq (float).</param>
        /// <param name="qualityWeight">Parameter representing the qualityWeight (float).</param>
        /// <returns>Returns Tier Index: 0=Full, 1=Medium, 2=Suspended of type int.</returns>
        public static int Compute(float distanceSq, float zone1RadiusSq, float zone2RadiusSq, float qualityWeight)
        {
            if (float.IsNaN(distanceSq) || distanceSq < 0f) distanceSq = 0f;
            if (float.IsInfinity(distanceSq)) return 2; // Hibernating for infinite distance

            if (float.IsNaN(zone1RadiusSq) || float.IsInfinity(zone1RadiusSq) || zone1RadiusSq < 0f) zone1RadiusSq = 0f;
            if (float.IsNaN(zone2RadiusSq) || float.IsInfinity(zone2RadiusSq) || zone2RadiusSq < 0f) zone2RadiusSq = 0f;
            if (float.IsNaN(qualityWeight) || float.IsInfinity(qualityWeight) || qualityWeight < 0f) qualityWeight = 0f;

            // expand Zone 1 based on quality weight as requested by: "Ensure high quality weight expands the Zone 1 radius"
            float expandedZone1RadiusSq = zone1RadiusSq * (1f + qualityWeight);

            if (distanceSq < expandedZone1RadiusSq)
                return 0; // FullSim

            if (distanceSq <= zone2RadiusSq)
                return 1; // DataOnly

            return 2; // Hibernating
        }
    }
}
