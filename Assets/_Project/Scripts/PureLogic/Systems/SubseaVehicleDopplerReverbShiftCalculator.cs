using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for SubseaVehicleDopplerReverbShiftCalculator.
    /// Extracted from SpatialAudioManager.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class SubseaVehicleDopplerReverbShiftCalculator
    {
        private const float MinimumDistanceSq = 0.0001f;
        private const float VelocityClampScale = 0.9f;
        private const float MaximumDopplerRatio = 1.2f;
        private const float MaximumDopplerRatioInv = 0.8333333f;

        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="initialFrequency">Parameter representing the initialFrequency (float).</param>
        /// <param name="emitterPos">Parameter representing the emitterPos (Vector3).</param>
        /// <param name="emitterVel">Parameter representing the emitterVel (Vector3).</param>
        /// <param name="listenerPos">Parameter representing the listenerPos (Vector3).</param>
        /// <param name="listenerVel">Parameter representing the listenerVel (Vector3).</param>
        /// <param name="speedOfSoundInWater">Parameter representing the speedOfSoundInWater (float).</param>
        /// <returns>Returns Shifted frequency of type float.</returns>
        public static float Compute(float initialFrequency, Vector3 emitterPos, Vector3 emitterVel, Vector3 listenerPos, Vector3 listenerVel, float speedOfSoundInWater)
        {
            if (float.IsNaN(initialFrequency) || float.IsInfinity(initialFrequency) || initialFrequency <= 0f)
                return 0f;

            if (float.IsNaN(speedOfSoundInWater) || float.IsInfinity(speedOfSoundInWater) || speedOfSoundInWater <= 0.001f)
                return initialFrequency;

            // Check parameter validity for NaN or Infinity
            if (!IsFinite(emitterPos) || !IsFinite(emitterVel) || !IsFinite(listenerPos) || !IsFinite(listenerVel))
                return initialFrequency;

            Vector3 listenerToSource = emitterPos - listenerPos;
            float distanceSq = listenerToSource.LengthSquared();

            if (distanceSq <= MinimumDistanceSq)
                return initialFrequency;

            Vector3 direction = ResolveDominantAxisDirection(listenerToSource);
            float relativeVelocity = Vector3.Dot(listenerVel - emitterVel, direction);

            float speedClamp = speedOfSoundInWater * VelocityClampScale;
            float clampedRelativeVelocity = Math.Clamp(relativeVelocity, -speedClamp, speedClamp);

            float targetRatio = Math.Clamp(1f + (clampedRelativeVelocity / speedOfSoundInWater), MaximumDopplerRatioInv, MaximumDopplerRatio);

            return initialFrequency * targetRatio;
        }

        private static Vector3 ResolveDominantAxisDirection(Vector3 direction)
        {
            Vector3 absDirection = Vector3.Abs(direction);
            float maxAxis = Math.Max(absDirection.X, Math.Max(absDirection.Y, absDirection.Z));
            if (!(maxAxis > 0.0001f))
                return Vector3.Zero;

            if (absDirection.X >= absDirection.Y && absDirection.X >= absDirection.Z)
                return direction.X < 0f ? new Vector3(-1f, 0f, 0f) : new Vector3(1f, 0f, 0f);

            if (absDirection.Y >= absDirection.Z)
                return direction.Y < 0f ? new Vector3(0f, -1f, 0f) : new Vector3(0f, 1f, 0f);

            return direction.Z < 0f ? new Vector3(0f, 0f, -1f) : new Vector3(0f, 0f, 1f);
        }

        private static bool IsFinite(Vector3 v)
        {
            return float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
        }
    }
}
