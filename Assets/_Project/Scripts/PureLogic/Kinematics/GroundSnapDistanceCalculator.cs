using System;
using System.Numerics;

namespace Hecton8.PureLogic.Kinematics
{
    /// <summary>
    /// Pure C# mathematical implementation for GroundSnapDistanceCalculator.
    /// Extracted from PlayerKinematicsRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class GroundSnapDistanceCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='distanceToGround'>Parameter representing the distanceToGround (float).</param>
        /// <param name='maxStepHeight'>Parameter representing the maxStepHeight (float).</param>
        /// <param name='slopeAngleDeg'>Parameter representing the slopeAngleDeg (float).</param>
        /// <param name='maxWalkableSlopeDeg'>Parameter representing the maxWalkableSlopeDeg (float).</param>
        /// <returns>Returns bool shouldSnap, float snapDistance of type bool.</returns>
        public static bool Compute(float distanceToGround, float maxStepHeight, float slopeAngleDeg, float maxWalkableSlopeDeg)
        {
            if (float.IsNaN(distanceToGround) || float.IsInfinity(distanceToGround) ||
                float.IsNaN(maxStepHeight) || float.IsInfinity(maxStepHeight) ||
                float.IsNaN(slopeAngleDeg) || float.IsInfinity(slopeAngleDeg) ||
                float.IsNaN(maxWalkableSlopeDeg) || float.IsInfinity(maxWalkableSlopeDeg))
            {
                return false;
            }

            if (distanceToGround < 0f || maxStepHeight < 0f || slopeAngleDeg < 0f || maxWalkableSlopeDeg < 0f)
            {
                return false;
            }

            return distanceToGround <= maxStepHeight && slopeAngleDeg <= maxWalkableSlopeDeg;
        }
    }
}
