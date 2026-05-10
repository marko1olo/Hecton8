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

            float depthT = math.saturate((currentDepth - slowdownStart) * math.rcp(math.max(slowdownEnd - slowdownStart, 0.01f)));
            return depthT * dragIncreaseMax;
        }

        public static float ResolveDepthSlowdown(float currentDepth, float slowdownStart, float slowdownEnd, float slowdownMax)
        {
            if (currentDepth <= slowdownStart || slowdownMax <= 0f)
                return 1f;

            float slowT = math.saturate((currentDepth - slowdownStart) * math.rcp(math.max(slowdownEnd - slowdownStart, 0.01f)));
            return 1f - (slowT * slowdownMax);
        }

        public static Vector3 ApplyAnalyticalDrag(Vector3 velocity, float dragCoefficient, float fixedDeltaTime)
        {
            return HectonPlayerMotor.AnalyticalQuadraticDrag(velocity, dragCoefficient, fixedDeltaTime);
        }

        public static float ResolveBrineViscosityDragMultiplier(bool isSubmergedInBrine, float brineMultiplier)
        {
            return isSubmergedInBrine ? math.max(1f, brineMultiplier) : 1f;
        }

        public static Vector3 ApplyAnalyticalDrag(
            Vector3 velocity,
            float dragCoefficient,
            float fixedDeltaTime,
            Vector3 forward,
            float crossSectionalAreaScale)
        {
            return HectonPlayerMotor.AnalyticalQuadraticDrag(
                velocity,
                dragCoefficient,
                forward,
                crossSectionalAreaScale,
                fixedDeltaTime);
        }
    }
}
