using System;
using System.Numerics;

namespace Hecton8.PureLogic.Kinematics
{
    /// <summary>
    /// Pure C# mathematical implementation for ServoTorqueLoadCalculator.
    /// Extracted from ExosuitKinematicsRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class ServoTorqueLoadCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="loadKg">Parameter representing the loadKg (float).</param>
        /// <param name="armAngleDeg">Parameter representing the armAngleDeg (float).</param>
        /// <param name="armLengthM">Parameter representing the armLengthM (float).</param>
        /// <param name="gravity">Parameter representing the gravity (float).</param>
        /// <param name="servoEfficiency">Parameter representing the servoEfficiency (float).</param>
        /// <returns>Returns requiredTorqueNm, float (powerConsumptionWatts) of type float.</returns>
        public static float Compute(float loadKg, float armAngleDeg, float armLengthM, float gravity, float servoEfficiency)
        {
            if (float.IsNaN(loadKg) || float.IsInfinity(loadKg)) loadKg = 0f;
            if (float.IsNaN(armAngleDeg) || float.IsInfinity(armAngleDeg)) armAngleDeg = 0f;
            if (float.IsNaN(armLengthM) || float.IsInfinity(armLengthM)) armLengthM = 0f;
            if (float.IsNaN(gravity) || float.IsInfinity(gravity)) gravity = 9.81f;
            if (float.IsNaN(servoEfficiency) || float.IsInfinity(servoEfficiency)) servoEfficiency = 1f;

            loadKg = Math.Max(0f, loadKg);
            armLengthM = Math.Max(0f, armLengthM);
            servoEfficiency = Math.Clamp(servoEfficiency, 0.01f, 1f);

            // Torque calculation: Torque = Force * Distance * sin(theta) where theta is angle to gravity
            // Force = Mass * Gravity
            // Horizontal arm (angle 0 or 180 to gravity perpendicular) has max torque
            // Vertical arm (angle 90 or 270) has zero torque against gravity

            float angleRad = armAngleDeg * (float)Math.PI / 180f;
            float force = loadKg * gravity;
            // Abs(Cos) because angle 0 means horizontal in this context for "max torque" based on requirement
            // Requirement says: "Heavy load horizontal: max. Vertical down: gravity-assisted."
            float horizontalFactor = Math.Abs((float)Math.Cos(angleRad));

            float torque = force * armLengthM * horizontalFactor;
            float requiredTorqueNm = torque / servoEfficiency;

            return requiredTorqueNm;
        }
    }
}
