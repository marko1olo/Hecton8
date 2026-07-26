using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    /// <summary>
    /// Burst thermal slumping pass that relaxes slopes above a talus angle.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ThermalSlumpingJob : IJobParallelFor
    {
        /// <summary>Read-only source heights in normalized 0..1 terrain space.</summary>
        [ReadOnly, NoAlias] public NativeArray<float> InputHeights01;

        /// <summary>Write-only relaxed heights in normalized 0..1 terrain space.</summary>
        [WriteOnly, NoAlias] public NativeArray<float> OutputHeights01;

        /// <summary>Optional wear lane receiving talus displacement intensity.</summary>
        [NoAlias] public NativeArray<float> WearMask;

        /// <summary>Heightmap width.</summary>
        public int Width;

        /// <summary>Heightmap height.</summary>
        public int Height;

        /// <summary>Pixel spacing in meters.</summary>
        public float CellSizeMeters;

        /// <summary>Terrain vertical scale in meters.</summary>
        public float HeightScaleMeters;

        /// <summary>Critical slope angle in degrees.</summary>
        public float TalusAngleDegrees;

        /// <summary>Per-iteration transfer strength.</summary>
        public float Strength;

        /// <summary>Non-zero writes talus intensity into <see cref="WearMask"/>.</summary>
        public byte WriteWearMaskFlag;

        /// <summary>
        /// Executes one mass-conserving slumping iteration.
        /// </summary>
        /// <param name="index">Linear heightmap cell index.</param>
        public void Execute(int index)
        {
            if (Width < 3 || Height < 3)
                return;

            int x = index % Width;
            int z = index / Width;
            float center = math.saturate(InputHeights01[index]);

            if (x <= 0 || z <= 0 || x >= Width - 1 || z >= Height - 1)
            {
                OutputHeights01[index] = center;
                if (WriteWearMaskFlag != 0 && WearMask.IsCreated)
                    WearMask[index] = math.max(WearMask[index], 0f);
                return;
            }

            float safeCellSize = math.max(0.001f, CellSizeMeters);
            float safeHeightScale = math.max(0.001f, HeightScaleMeters);
            float talusNormalized = global::Hecton8.Core.MathLodApproximation.ApproxTanClamped(math.radians(math.clamp(TalusAngleDegrees, 1f, 89f)), 4096f) *
                                    safeCellSize /
                                    safeHeightScale;
            float transferScale = math.saturate(Strength) * 0.25f;
            float delta = 0f;

            if (x - 1 > 0) delta += ResolveNeighborDelta(center, InputHeights01[index - 1], talusNormalized, transferScale);
            if (x + 1 < Width - 1) delta += ResolveNeighborDelta(center, InputHeights01[index + 1], talusNormalized, transferScale);
            if (z - 1 > 0) delta += ResolveNeighborDelta(center, InputHeights01[index - Width], talusNormalized, transferScale);
            if (z + 1 < Height - 1) delta += ResolveNeighborDelta(center, InputHeights01[index + Width], talusNormalized, transferScale);

            float resolved = math.saturate(center + delta);
            OutputHeights01[index] = resolved;

            if (WriteWearMaskFlag != 0 && WearMask.IsCreated)
                WearMask[index] += math.abs(delta);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveNeighborDelta(float center, float neighbor, float talusNormalized, float transferScale)
        {
            float safeNeighbor = math.saturate(neighbor);
            float diff = center - safeNeighbor;
            float excess = math.abs(diff) - talusNormalized;
            if (excess <= 0f)
                return 0f;

            return -math.sign(diff) * excess * transferScale;
        }
    }
}
