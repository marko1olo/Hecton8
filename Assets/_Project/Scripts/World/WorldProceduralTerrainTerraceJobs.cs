using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct WorldProceduralTerrainTerraceJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<float> InputHeights01;
        [WriteOnly, NoAlias] public NativeArray<float> OutputHeights01;
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
            float normalized = math.saturate(h);
            float steps = math.max(1f, stepCount);
            float scaled = normalized * steps;
            float baseStep = math.floor(scaled);
            float frac = scaled - baseStep;
            float halfWidth = math.max(0.0001f, math.saturate(sharpness) * 0.5f);
            float eased = math.smoothstep(0.5f - halfWidth, 0.5f + halfWidth, frac);
            float terraced = (baseStep + eased) / steps;
            return math.saturate(math.lerp(normalized, terraced, math.saturate(strength)));
        }
    }
}
