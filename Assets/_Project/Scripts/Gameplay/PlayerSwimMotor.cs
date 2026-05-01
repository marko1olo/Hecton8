using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Pure swim locomotion math extracted from the player state machine.
    /// </summary>
    internal static class PlayerSwimMotor
    {
        public static float ResolveDepthDragAdd(float currentDepth, float slowdownStart, float slowdownEnd, float dragIncreaseMax)
        {
            if (currentDepth <= slowdownStart || dragIncreaseMax <= 0f)
                return 0f;

            float depthT = math.saturate((currentDepth - slowdownStart) / math.max(slowdownEnd - slowdownStart, 0.01f));
            return depthT * dragIncreaseMax;
        }

        public static float ResolveDepthSlowdown(float currentDepth, float slowdownStart, float slowdownEnd, float slowdownMax)
        {
            if (currentDepth <= slowdownStart || slowdownMax <= 0f)
                return 1f;

            float slowT = math.saturate((currentDepth - slowdownStart) / math.max(slowdownEnd - slowdownStart, 0.01f));
            return 1f - (slowT * slowdownMax);
        }

        public static Vector3 ApplyAnalyticalDrag(Vector3 velocity, float dragCoefficient, float bodyMass, float fixedDeltaTime)
        {
            float dragAccelerationCoefficient = dragCoefficient / math.max(bodyMass, 0.0001f);
            return HectonPlayerMotor.AnalyticalQuadraticDrag(velocity, dragAccelerationCoefficient, fixedDeltaTime);
        }
    }
}
