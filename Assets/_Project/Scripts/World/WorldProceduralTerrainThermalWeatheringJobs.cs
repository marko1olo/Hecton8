using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct WorldProceduralTerrainThermalWeatheringJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<float> InputHeights01;
        [WriteOnly, NoAlias] public NativeArray<float> OutputHeights01;
        public int Width;
        public int Height;
        public float CellSizeMeters;
        public float HeightScaleMeters;
        public float TalusAngleDegrees;
        public float Strength;

        public void Execute(int index)
        {
            int x = index % Width;
            int z = index / Width;
            float center = math.saturate(InputHeights01[index]);

            if (x <= 0 || z <= 0 || x >= Width - 1 || z >= Height - 1)
            {
                OutputHeights01[index] = center;
                return;
            }

            float safeCellSize = math.max(0.001f, CellSizeMeters);
            float safeHeightScale = math.max(0.001f, HeightScaleMeters);
            float talusNormalized = math.tan(math.radians(math.clamp(TalusAngleDegrees, 1f, 89f))) *
                                    safeCellSize /
                                    safeHeightScale;
            float transferScale = math.saturate(Strength) * 0.25f;
            float delta = 0f;

            delta += ResolveTransfer(center, InputHeights01[index - 1], talusNormalized, transferScale);
            delta += ResolveTransfer(center, InputHeights01[index + 1], talusNormalized, transferScale);
            delta += ResolveTransfer(center, InputHeights01[index - Width], talusNormalized, transferScale);
            delta += ResolveTransfer(center, InputHeights01[index + Width], talusNormalized, transferScale);

            OutputHeights01[index] = math.saturate(center + delta);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveTransfer(float center, float neighbor, float talusNormalized, float transferScale)
        {
            float diff = center - math.saturate(neighbor);
            float excess = math.abs(diff) - talusNormalized;
            if (excess <= 0f)
                return 0f;

            return -math.sign(diff) * excess * transferScale;
        }
    }
}
