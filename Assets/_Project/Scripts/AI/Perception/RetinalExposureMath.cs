using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Hecton8.AI.Perception
{
    internal static class RetinalExposureMath
    {
        internal const float LookingAtLightDotThreshold = 0.9f;
        internal const float GlareHoldDotThreshold = 0.5f;
        private const float DirectDotInvWidth = 10f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float ResolvePredatorToLightDot(float3 predatorForward, float3 lightToPredatorDirection)
        {
            return math.dot(predatorForward, -lightToPredatorDirection);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsLookingAtLight(float predatorToLightDot)
        {
            return predatorToLightDot > LookingAtLightDotThreshold;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsHoldingGlare(float predatorToLightDot)
        {
            return predatorToLightDot > GlareHoldDotThreshold;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float ResolveDirectGlare01(float predatorToLightDot)
        {
            return math.saturate((predatorToLightDot - LookingAtLightDotThreshold) * DirectDotInvWidth);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float SignedTriangle(float phase)
        {
            return 1f - (math.abs((math.frac(phase) * 2f) - 1f) * 2f);
        }
    }
}
