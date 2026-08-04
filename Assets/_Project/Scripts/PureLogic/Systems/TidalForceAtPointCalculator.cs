using System;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for TidalForceAtPointCalculator.
    /// Extracted from HectonCelestialEngine.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class TidalForceAtPointCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="moonPhaseAngleDeg">Parameter representing the moonPhaseAngleDeg (float).</param>
        /// <param name="latitude">Parameter representing the latitude (float).</param>
        /// <param name="tidalAmplitudeBase">Parameter representing the tidalAmplitudeBase (float).</param>
        /// <param name="moonGravitationalParam">Parameter representing the moonGravitationalParam (float).</param>
        /// <returns>Returns tidalForceNormalized 0-1 of type float.</returns>
        public static float Compute(float moonPhaseAngleDeg, float latitude, float tidalAmplitudeBase, float moonGravitationalParam)
        {
            if (float.IsNaN(moonPhaseAngleDeg) || float.IsInfinity(moonPhaseAngleDeg) ||
                float.IsNaN(latitude) || float.IsInfinity(latitude) ||
                float.IsNaN(tidalAmplitudeBase) || float.IsInfinity(tidalAmplitudeBase) ||
                float.IsNaN(moonGravitationalParam) || float.IsInfinity(moonGravitationalParam))
            {
                return 0f;
            }

            moonPhaseAngleDeg = moonPhaseAngleDeg % 360f;
            if (moonPhaseAngleDeg < 0f)
            {
                moonPhaseAngleDeg += 360f;
            }

            latitude = Math.Clamp(latitude, -90f, 90f);
            tidalAmplitudeBase = Math.Max(0f, tidalAmplitudeBase);
            moonGravitationalParam = Math.Max(0f, moonGravitationalParam);

            float phaseRad = moonPhaseAngleDeg * (float)Math.PI / 180f;
            float cosPhase = (float)Math.Cos(phaseRad);
            float cos2Phase = (float)Math.Cos(2f * phaseRad);

            float phaseMultiplier = 0.25f * cosPhase + 0.375f * cos2Phase + 0.375f;

            float latRad = latitude * (float)Math.PI / 180f;
            float latMultiplier = (float)Math.Cos(latRad);

            float normalized = 0f;
            if (tidalAmplitudeBase > 0f && moonGravitationalParam > 0f)
            {
                normalized = phaseMultiplier * latMultiplier;
            }


            if (float.IsNaN(normalized) || float.IsInfinity(normalized))
            {
                return 0f;
            }

            return Math.Clamp(normalized, 0f, 1f);
        }
    }
}
