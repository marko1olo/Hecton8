using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for BeaconNetworkSignalAttenuationCalculator.
    /// Extracted from BeaconNetworkSystem.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class BeaconNetworkSignalAttenuationCalculator
    {
        private const float MinDistance = 0.001f;
        private const float SpreadingLossCoefficient = 20f;
        private const float BaseAbsorption = 0.1f;
        private const float SalinityAbsorptionMultiplier = 0.05f;

        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="transmitPowerDb">Parameter representing the transmitPowerDb (float).</param>
        /// <param name="distance">Parameter representing the distance (float).</param>
        /// <param name="salinityPpt">Parameter representing the salinityPpt (float).</param>
        /// <returns>Returns Received signal strength Db of type float.</returns>
        public static float Compute(float transmitPowerDb, float distance, float salinityPpt)
        {
            if (float.IsNaN(transmitPowerDb) || float.IsInfinity(transmitPowerDb))
                transmitPowerDb = 0f;

            if (float.IsNaN(distance) || float.IsInfinity(distance))
                distance = 0f;

            if (float.IsNaN(salinityPpt) || float.IsInfinity(salinityPpt))
                salinityPpt = 0f;

            if (distance < 0f) distance = 0f;
            if (salinityPpt < 0f) salinityPpt = 0f;

            if (distance <= MinDistance) return transmitPowerDb;

            float spreadingLossDb = SpreadingLossCoefficient * (float)Math.Log10(distance);
            if (spreadingLossDb < 0f) spreadingLossDb = 0f;

            float alpha = BaseAbsorption + (SalinityAbsorptionMultiplier * salinityPpt);
            float absorptionLossDb = alpha * distance;

            float totalLossDb = spreadingLossDb + absorptionLossDb;

            float receivedStrengthDb = transmitPowerDb - totalLossDb;

            return receivedStrengthDb;
        }
    }
}
