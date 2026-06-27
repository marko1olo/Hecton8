using System;
using System.Numerics;

namespace Hecton8.PureLogic.Kinematics
{
    /// <summary>
    /// Pure C# mathematical implementation for NitrogenNarcosisInputDrifter.
    /// Extracted from HectonPlayerMovement.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class NitrogenNarcosisInputDrifter
    {
        private const float RuntimeNarcosisInputNoiseScale = 0.22f;
        private const float RuntimeNarcosisInputNoiseFrequency = 1.37f;
        private const uint RuntimeNarcosisLcgMultiplier = 1664525u;
        private const uint RuntimeNarcosisLcgIncrement = 1013904223u;

        private const float TwoPi = 6.283185307179586f;
        private const float InvTwoPi = 0.15915494309189535f;

        private const float PhaseScale = 0.000015259022f;
        private const float PhaseYMultiplier = 1.618f;
        private const float DriftYScale = 0.5f;

        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='rawInput'>Parameter representing the rawInput (Vector2).</param>
        /// <param name='narcosisDepth01'>Parameter representing the narcosisDepth01 (float).</param>
        /// <param name='timeSeconds'>Parameter representing the timeSeconds (float).</param>
        /// <param name='seed'>Parameter representing the seed (int).</param>
        /// <returns>Returns Drifted input vector of type Vector2.</returns>
        public static Vector2 Calculate(Vector2 rawInput, float narcosisDepth01, float timeSeconds, int seed)
        {
            if (!IsFinite(rawInput.X) || !IsFinite(rawInput.Y) || !IsFinite(narcosisDepth01) || !IsFinite(timeSeconds))
            {
                return new Vector2(
                    ClampFinite(rawInput.X),
                    ClampFinite(rawInput.Y)
                );
            }

            float severity01 = Math.Max(0f, Math.Min(1f, narcosisDepth01));

            if (severity01 <= 0f)
            {
                return new Vector2(
                    Math.Max(-1f, Math.Min(1f, rawInput.X)),
                    Math.Max(-1f, Math.Min(1f, rawInput.Y))
                );
            }

            uint narcosisSeed = unchecked((uint)seed);

            float phase = timeSeconds * RuntimeNarcosisInputNoiseFrequency +
                ((narcosisSeed & 0xFFFFu) * PhaseScale) * TwoPi;

            float outX = Math.Max(-1f, Math.Min(1f,
                rawInput.X + SignedTriangleRadians(phase) * RuntimeNarcosisInputNoiseScale * severity01));

            narcosisSeed = AdvanceRuntimeNarcosisLcg(narcosisSeed);

            float outY = Math.Max(-1f, Math.Min(1f,
                rawInput.Y + SignedTriangleRadians(phase * PhaseYMultiplier + ((narcosisSeed & 0xFFFFu) * PhaseScale) * TwoPi) * RuntimeNarcosisInputNoiseScale * DriftYScale * severity01));

            return new Vector2(outX, outY);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float ClampFinite(float value)
        {
            if (float.IsNaN(value)) return 0f;
            if (float.IsPositiveInfinity(value)) return 1f;
            if (float.IsNegativeInfinity(value)) return -1f;
            return Math.Max(-1f, Math.Min(1f, value));
        }

        private static uint AdvanceRuntimeNarcosisLcg(uint state)
        {
            return state * RuntimeNarcosisLcgMultiplier + RuntimeNarcosisLcgIncrement;
        }

        private static float SignedTriangleRadians(float radians)
        {
            return SignedTriangle01(radians * InvTwoPi + 0.25f);
        }

        private static float SignedTriangle01(float phase)
        {
            float wrapped = phase - (float)Math.Floor(phase);
            return (1f - Math.Abs(wrapped * 2f - 1f)) * 2f - 1f;
        }
    }
}