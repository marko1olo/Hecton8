using System;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for ProjectileDropCalculator.
    /// Extracted from BallisticsRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class ProjectileDropCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="muzzleVelocity">Parameter representing the muzzleVelocity (float).</param>
        /// <param name="launchAngleDeg">Parameter representing the launchAngleDeg (float).</param>
        /// <param name="dragCoeff">Parameter representing the dragCoeff (float).</param>
        /// <param name="gravityMsq">Parameter representing the gravityMsq (float).</param>
        /// <param name="timeOfFlight">Parameter representing the timeOfFlight (float).</param>
        /// <returns>Returns vertical drop in meters at timeOfFlight of type float.</returns>
        public static float Compute(float muzzleVelocity, float launchAngleDeg, float dragCoeff, float gravityMsq, float timeOfFlight)
        {
            if (float.IsNaN(muzzleVelocity) || float.IsInfinity(muzzleVelocity)) muzzleVelocity = 0f;
            if (float.IsNaN(launchAngleDeg) || float.IsInfinity(launchAngleDeg)) launchAngleDeg = 0f;
            if (float.IsNaN(dragCoeff) || float.IsInfinity(dragCoeff)) dragCoeff = 0f;
            if (float.IsNaN(gravityMsq) || float.IsInfinity(gravityMsq)) gravityMsq = 0f;
            if (float.IsNaN(timeOfFlight) || float.IsInfinity(timeOfFlight)) return 0f;

            muzzleVelocity = Math.Max(0f, muzzleVelocity);
            timeOfFlight = Math.Max(0f, timeOfFlight);
            dragCoeff = Math.Max(0f, dragCoeff);
            gravityMsq = Math.Max(0f, gravityMsq);

            if (timeOfFlight <= 0f) return 0f;

            float launchAngleRad = launchAngleDeg * (float)(Math.PI / 180.0);
            float v0y = muzzleVelocity * (float)Math.Sin(launchAngleRad);

            float y;
            if (dragCoeff < 0.0001f)
            {
                y = (v0y * timeOfFlight) - (0.5f * gravityMsq * timeOfFlight * timeOfFlight);
            }
            else
            {
                float terminalVelocity = gravityMsq / dragCoeff;
                float expTerm = (float)Math.Exp(-dragCoeff * timeOfFlight);
                y = ((v0y + terminalVelocity) / dragCoeff) * (1f - expTerm) - (terminalVelocity * timeOfFlight);
            }

            if (float.IsNaN(y) || float.IsInfinity(y))
                return 0f;

            return y;
        }
    }
}
