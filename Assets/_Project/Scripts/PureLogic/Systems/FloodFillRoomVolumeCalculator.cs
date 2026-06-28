using System;
using System.Collections.Generic;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for FloodFillRoomVolumeCalculator.
    /// Extracted from HabitatGraphManager.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class FloodFillRoomVolumeCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="voxelGrid">Parameter representing the voxelGrid (bool[,,]).</param>
        /// <param name="startX">Parameter representing the startX (int).</param>
        /// <param name="startY">Parameter representing the startY (int).</param>
        /// <param name="startZ">Parameter representing the startZ (int).</param>
        /// <param name="voxelSizeM">Parameter representing the voxelSizeM (float).</param>
        /// <returns>Returns connectedVolumeM3, int (voxelCount) of type float.</returns>
        public static float Compute(bool[,,] voxelGrid, int startX, int startY, int startZ, float voxelSizeM)
        {
            if (voxelGrid == null)
                return 0f;

            if (float.IsNaN(voxelSizeM) || float.IsInfinity(voxelSizeM) || voxelSizeM <= 0f)
                return 0f;

            int sizeX = voxelGrid.GetLength(0);
            int sizeY = voxelGrid.GetLength(1);
            int sizeZ = voxelGrid.GetLength(2);

            if (startX < 0 || startX >= sizeX ||
                startY < 0 || startY >= sizeY ||
                startZ < 0 || startZ >= sizeZ)
            {
                return 0f;
            }

            if (!voxelGrid[startX, startY, startZ])
            {
                return 0f;
            }

            // We must not mutate the original voxelGrid.
            // Using a visited array ensures we only visit each voxel once.
            bool[,,] visited = new bool[sizeX, sizeY, sizeZ];
            Queue<(int x, int y, int z)> queue = new Queue<(int, int, int)>();

            queue.Enqueue((startX, startY, startZ));
            visited[startX, startY, startZ] = true;

            int count = 0;

            int[] dx = { 1, -1, 0, 0, 0, 0 };
            int[] dy = { 0, 0, 1, -1, 0, 0 };
            int[] dz = { 0, 0, 0, 0, 1, -1 };

            while (queue.Count > 0)
            {
                var curr = queue.Dequeue();
                count++;

                for (int i = 0; i < 6; i++)
                {
                    int nx = curr.x + dx[i];
                    int ny = curr.y + dy[i];
                    int nz = curr.z + dz[i];

                    if (nx >= 0 && nx < sizeX &&
                        ny >= 0 && ny < sizeY &&
                        nz >= 0 && nz < sizeZ)
                    {
                        if (!visited[nx, ny, nz] && voxelGrid[nx, ny, nz])
                        {
                            visited[nx, ny, nz] = true;
                            queue.Enqueue((nx, ny, nz));
                        }
                    }
                }
            }

            double singleVolume = (double)voxelSizeM * (double)voxelSizeM * (double)voxelSizeM;
            double totalVolume = (double)count * singleVolume;

            if (totalVolume > float.MaxValue)
                return float.MaxValue;

            return (float)totalVolume;
        }
    }
}
