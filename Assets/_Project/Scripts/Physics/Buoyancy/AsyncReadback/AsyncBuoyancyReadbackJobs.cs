using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Hecton8.Physics
{
    public static class AsyncBuoyancyReadbackMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveSampleBudget(int minSampleCount, int maxSampleCount, float globalQualityWeight)
        {
            int safeMin = math.max(1, minSampleCount);
            int safeMax = math.max(safeMin, maxSampleCount);
            float quality = math.saturate(math.select(1f, globalQualityWeight, math.isfinite(globalQualityWeight)));
            float smoothQuality = quality * quality * (3f - (2f * quality));
            float scaled = math.lerp((float)safeMin, (float)safeMax, smoothQuality);
            return math.clamp((int)math.ceil(scaled), safeMin, safeMax);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveSmoothingAlpha()
        {
            return 0.52f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveMockLocalHeight(float2 localXz, uint frameIndex, float timeSeconds)
        {
            float coarse = TriangleSigned((localXz.x * 0.013671875f) + (frameIndex * 0.0078125f));
            float cross = TriangleSigned((localXz.y * 0.0107421875f) - (timeSeconds * 0.041666667f));
            float ripple = TriangleSigned(((localXz.x + localXz.y) * 0.03125f) + (frameIndex * 0.01953125f));
            return (coarse * 0.62f) + (cross * 0.31f) + (ripple * 0.18f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float TriangleSigned(float value)
        {
            float wrapped = value - math.floor(value);
            return (math.abs((wrapped * 2f) - 1f) * 2f) - 1f;
        }
    }
}
