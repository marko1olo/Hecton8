using System;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for CaveGraphConnectivityChecker.
    /// Extracted from CaveGraphGenerator.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class CaveGraphConnectivityChecker
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="nodeCount">Parameter representing the nodeCount (int).</param>
        /// <param name="adjacencyMatrix">Parameter representing the adjacencyMatrix (bool[,]).</param>
        /// <returns>Returns isFullyConnected, int[] (disconnectedNodeIds) of type bool.</returns>
        public static bool Check(int nodeCount, bool[,] adjacencyMatrix, out int[] disconnectedNodeIds)
        {
            if (nodeCount <= 0)
            {
                disconnectedNodeIds = Array.Empty<int>();
                return true;
            }

            if (adjacencyMatrix == null)
            {
                throw new ArgumentNullException(nameof(adjacencyMatrix));
            }

            if (adjacencyMatrix.GetLength(0) != nodeCount || adjacencyMatrix.GetLength(1) != nodeCount)
            {
                throw new ArgumentException("Adjacency matrix must be square and match node count.");
            }

            bool[] visited = new bool[nodeCount];
            visited[0] = true;

            int[] queue = new int[nodeCount];
            int head = 0;
            int tail = 0;

            queue[tail++] = 0;

            while(head < tail)
            {
                int current = queue[head++];

                for(int i = 0; i < nodeCount; i++)
                {
                    if (adjacencyMatrix[current, i] && !visited[i])
                    {
                        visited[i] = true;
                        queue[tail++] = i;
                    }
                }
            }

            int unvisitedCount = 0;
            for(int i = 0; i < nodeCount; i++)
            {
                if (!visited[i])
                {
                    unvisitedCount++;
                }
            }

            if (unvisitedCount == 0)
            {
                disconnectedNodeIds = Array.Empty<int>();
                return true;
            }

            disconnectedNodeIds = new int[unvisitedCount];
            int idx = 0;
            for(int i = 0; i < nodeCount; i++)
            {
                if (!visited[i])
                {
                    disconnectedNodeIds[idx++] = i;
                }
            }

            return false;
        }
    }
}
