using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for SoundObstructionLowpassCutoffCalculator.
    /// Extracted from SpatialAudioManager.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class SoundObstructionLowpassCutoffCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="baseCutoffHz">Parameter representing the baseCutoffHz (float).</param>
        /// <param name="obstructionThicknessCm">Parameter representing the obstructionThicknessCm (float).</param>
        /// <param name="materialDensity">Parameter representing the materialDensity (float).</param>
        /// <returns>Returns Target lowpass cutoff frequency Hz of type float.</returns>
        public static float Compute(float baseCutoffHz, float obstructionThicknessCm, float materialDensity, float attenuationFactor = 0.01f)
        {

            if (float.IsNaN(baseCutoffHz)) return 0f;
            if (float.IsNaN(obstructionThicknessCm)) obstructionThicknessCm = 0f;
            if (float.IsNaN(materialDensity)) materialDensity = 0f;

            if (float.IsInfinity(baseCutoffHz)) return 0f;
            if (float.IsInfinity(obstructionThicknessCm) || float.IsInfinity(materialDensity)) return 0f;


            if (baseCutoffHz < 0f) baseCutoffHz = 0f;
            if (obstructionThicknessCm < 0f) obstructionThicknessCm = 0f;
            if (materialDensity < 0f) materialDensity = 0f;

            if (obstructionThicknessCm == 0f || materialDensity == 0f) return baseCutoffHz;

            float massArea = obstructionThicknessCm * materialDensity;
            float attenuation = (float)Math.Exp(-attenuationFactor * massArea);
            float cutoff = baseCutoffHz * attenuation;

            if (cutoff < 0f) cutoff = 0f;
            return cutoff;
        }
    }
}
