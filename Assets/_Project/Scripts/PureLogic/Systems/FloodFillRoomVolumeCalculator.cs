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
        // Static 6-neighbour direction table (reused across all calls; no per-call array).
        private static readonly int[] DirX = { 1, -1, 0, 0, 0, 0 };
        private static readonly int[] DirY = { 0, 0, 1, -1, 0, 0 };
        private static readonly int[] DirZ = { 0, 0, 0, 0, 1, -1 };

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

        /// <summary>
        /// Allocation-free variant of <see cref="Compute"/>. Reuses caller-provided scratch
        /// buffers so repeated room-volume queries on hot paths allocate nothing. The queue is
        /// implemented as an explicit flat int buffer (head/tail indices) instead of a
        /// <see cref="Queue{T}"/>, and the 6-neighbour direction table is a static reused array.
        /// </summary>
        /// <param name="voxelGrid">The (immutable) occupancy grid.</param>
        /// <param name="startX">Start voxel X.</param>
        /// <param name="startY">Start voxel Y.</param>
        /// <param name="startZ">Start voxel Z.</param>
        /// <param name="voxelSizeM">World size of one voxel (metres).</param>
        /// <param name="visited">Scratch visited grid of the same dimensions; contents are fully
        /// overwritten (interior AND boundary) each call, so it may be pooled.</param>
        /// <param name="queueBuffer">Scratch int buffer with capacity &gt;= sizeX*sizeY*sizeZ.</param>
        /// <param name="voxelCountOut">Receives the number of connected voxels.</param>
        public static float ComputeBuffered(
            bool[,,] voxelGrid, int startX, int startY, int startZ, float voxelSizeM,
            bool[,,] visited, int[] queueBuffer, out int voxelCountOut)
        {
            voxelCountOut = 0;
            if (voxelGrid == null || visited == null || queueBuffer == null)
                return 0f;

            if (float.IsNaN(voxelSizeM) || float.IsInfinity(voxelSizeM) || voxelSizeM <= 0f)
                return 0f;

            int sizeX = voxelGrid.GetLength(0);
            int sizeY = voxelGrid.GetLength(1);
            int sizeZ = voxelGrid.GetLength(2);

            if (visited.GetLength(0) != sizeX || visited.GetLength(1) != sizeY ||
                visited.GetLength(2) != sizeZ)
                return 0f;

            int cellCount = sizeX * sizeY * sizeZ;
            if (queueBuffer.Length < cellCount)
                return 0f;

            if (startX < 0 || startX >= sizeX || startY < 0 || startY >= sizeY ||
                startZ < 0 || startZ >= sizeZ)
                return 0f;

            if (!voxelGrid[startX, startY, startZ])
                return 0f;

            // Clear the full visited grid (pooled buffer parity with the fresh `new bool[,,]`).
            for (int x = 0; x < sizeX; x++)
                for (int y = 0; y < sizeY; y++)
                    for (int z = 0; z < sizeZ; z++)
                        visited[x, y, z] = false;

            int head = 0;
            int tail = 0;
            queueBuffer[tail++] = startX;
            queueBuffer[tail++] = startY;
            queueBuffer[tail++] = startZ;
            visited[startX, startY, startZ] = true;

            int count = 0;
            while (head < tail)
            {
                int cx = queueBuffer[head++];
                int cy = queueBuffer[head++];
                int cz = queueBuffer[head++];
                count++;

                for (int i = 0; i < 6; i++)
                {
                    int nx = cx + DirX[i];
                    int ny = cy + DirY[i];
                    int nz = cz + DirZ[i];
                    if (nx >= 0 && nx < sizeX && ny >= 0 && ny < sizeY && nz >= 0 && nz < sizeZ)
                    {
                        if (!visited[nx, ny, nz] && voxelGrid[nx, ny, nz])
                        {
                            visited[nx, ny, nz] = true;
                            queueBuffer[tail++] = nx;
                            queueBuffer[tail++] = ny;
                            queueBuffer[tail++] = nz;
                        }
                    }
                }
            }

            voxelCountOut = count;
            double singleVolume = (double)voxelSizeM * (double)voxelSizeM * (double)voxelSizeM;
            double totalVolume = (double)count * singleVolume;
            if (totalVolume > float.MaxValue)
                return float.MaxValue;
            return (float)totalVolume;
        }
    }
}
