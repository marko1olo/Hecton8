using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct WorldProceduralTerrainTerraceJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> InputHeights01;
        [WriteOnly] public NativeArray<float> OutputHeights01;
        public float StepCount;
        public float Sharpness;
        public float Strength;

        public void Execute(int index)
        {
            OutputHeights01[index] = Terrace01(
                InputHeights01[index],
                StepCount,
                Sharpness,
                Strength);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Terrace01(float h, float stepCount, float sharpness, float strength)
        {
            float steps = math.max(1f, stepCount);
            float scaled = math.saturate(h) * steps;
            float baseStep = math.floor(scaled);
            float frac = scaled - baseStep;
            float s = math.saturate(sharpness);
            float eased = math.smoothstep(0.5f - s * 0.5f, 0.5f + s * 0.5f, frac);
            float terraced = (baseStep + eased) / steps;
            return math.lerp(h, terraced, math.saturate(strength));
        }
    }
}
