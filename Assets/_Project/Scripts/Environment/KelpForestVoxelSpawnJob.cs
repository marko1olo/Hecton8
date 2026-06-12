using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Environment
{
    /// <summary>
    /// Evaluates the voxel grid and identifies surface placement points for Kelp Forest flora and fauna.
    /// Operates on the Voxel SDF and writes spawn points back to be instantiated by the WorldProceduralScatterDirector.
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct KelpForestVoxelSpawnJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> SdfBuffer;
        public int3 GridSize;
        public float VoxelStep;
        public float3 GridOrigin;

        // Settings
        public float SurfaceThreshold;
        public float MinDepth;
        public float MaxDepth;
        public float SpawnProbability;
        public uint Seed;

        // Output
        [WriteOnly] public NativeQueue<float3>.ParallelWriter KelpSpawnPoints;
        [WriteOnly] public NativeQueue<float3>.ParallelWriter CreatureSpawnPoints;

        public void Execute(int index)
        {
            // Reconstruct 3D coordinate from 1D index
            int x = index % GridSize.x;
            int y = (index / GridSize.x) % GridSize.y;
            int z = index / (GridSize.x * GridSize.y);

            // Avoid bounds
            if (x == 0 || y == 0 || z == 0 || x == GridSize.x - 1 || y == GridSize.y - 1 || z == GridSize.z - 1)
                return;

            float sdf = SdfBuffer[index];

            // Only spawn near surface
            if (math.abs(sdf) > SurfaceThreshold)
                return;

            float3 localPos = new float3(x, y, z) * VoxelStep;
            float3 worldPos = GridOrigin + localPos;

            // Enforce depth bands
            if (worldPos.y > MinDepth || worldPos.y < MaxDepth)
                return;

            // Calculate basic up-normal by sampling neighbors
            int upIndex = index + GridSize.x; // Simplified assuming Y is stride GridSize.x, actually Y is stride GridSize.x * 1
            // Real stride: x=1, y=GridSize.x, z=GridSize.x*GridSize.y
            int strideY = GridSize.x;
            float sdfUp = SdfBuffer[index + strideY];
            
            // Check if surface is floor-like (SDF goes positive when moving UP into empty water)
            bool isFloor = sdfUp > sdf;
            if (!isFloor)
                return;

            // Stable pseudo-random
            uint hash = Hash(new int3(x, y, z), Seed);
            float rand = (hash % 1000) / 1000f;

            if (rand < SpawnProbability)
            {
                // Add Kelp
                KelpSpawnPoints.Enqueue(worldPos);
            }
            else if (rand < SpawnProbability * 1.1f)
            {
                // Add minor creatures
                CreatureSpawnPoints.Enqueue(worldPos + new float3(0, 1f, 0));
            }
        }

        private static uint Hash(int3 pos, uint seed)
        {
            uint h = seed + (uint)pos.x * 374761393u + (uint)pos.y * 668265263u + (uint)pos.z * 109861619u;
            h = (h ^ (h >> 13)) * 2654435769u;
            h = (h ^ (h >> 16));
            return h;
        }
    }
}
