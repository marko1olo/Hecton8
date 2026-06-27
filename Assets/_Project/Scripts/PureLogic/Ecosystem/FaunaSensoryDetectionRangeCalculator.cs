using System;
using System.Numerics;

namespace Hecton8.PureLogic.Ecosystem
{
    /// <summary>
    /// Pure C# mathematical implementation for FaunaSensoryDetectionRangeCalculator.
    /// Extracted from FaunaDirector.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class FaunaSensoryDetectionRangeCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='predatorPos'>Parameter representing the predatorPos (Vector3).</param>
        /// <param name='preyPos'>Parameter representing the preyPos (Vector3).</param>
        /// <param name='waterTurbidity'>Parameter representing the waterTurbidity (float).</param>
        /// <param name='preyMovementSpeed'>Parameter representing the preyMovementSpeed (float).</param>
        /// <returns>Returns Prey Detected of type bool.</returns>
        public static bool Compute(Vector3 predatorPos, Vector3 preyPos, float waterTurbidity, float preyMovementSpeed, float baseVisualRange = 50f, float maxHearingRange = 100f, float speedHearingScale = 2f)
        {
            // Parameter Validation: guard against NaN and Infinity
            if (float.IsNaN(waterTurbidity) || float.IsInfinity(waterTurbidity))
                waterTurbidity = 0f;
            if (float.IsNaN(preyMovementSpeed) || float.IsInfinity(preyMovementSpeed))
                preyMovementSpeed = 0f;

            if (float.IsNaN(predatorPos.X) || float.IsInfinity(predatorPos.X) ||
                float.IsNaN(predatorPos.Y) || float.IsInfinity(predatorPos.Y) ||
                float.IsNaN(predatorPos.Z) || float.IsInfinity(predatorPos.Z))
                predatorPos = Vector3.Zero;

            if (float.IsNaN(preyPos.X) || float.IsInfinity(preyPos.X) ||
                float.IsNaN(preyPos.Y) || float.IsInfinity(preyPos.Y) ||
                float.IsNaN(preyPos.Z) || float.IsInfinity(preyPos.Z))
                preyPos = Vector3.Zero;

            // Clamp out negative or invalid values
            float safeTurbidity = Math.Max(0f, waterTurbidity);
            float safeSpeed = Math.Max(0f, preyMovementSpeed);

            // Mathematical model: Turbid water drastically reduces visual range.
            // visualRange = baseVisualRange * e^(-safeTurbidity)
            float effectiveVisualRange = baseVisualRange * (float)Math.Exp(-safeTurbidity);

            // Mathematical model: Fast moving prey increases hearing detection range.
            // hearingRange = min(maxHearingRange, safeSpeed * speedHearingScale)
            float effectiveHearingRange = Math.Min(maxHearingRange, safeSpeed * speedHearingScale);

            // Boundary Guarding: clamp final detection range
            float totalDetectionRange = Math.Max(effectiveVisualRange, effectiveHearingRange);

            // Calculate squared distance to avoid sqrt
            Vector3 diff = preyPos - predatorPos;
            float sqrDistance = diff.X * diff.X + diff.Y * diff.Y + diff.Z * diff.Z;

            return sqrDistance <= (totalDetectionRange * totalDetectionRange);
        }
    }
}
