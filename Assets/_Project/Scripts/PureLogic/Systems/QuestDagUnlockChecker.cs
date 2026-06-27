using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for QuestDagUnlockChecker.
    /// Extracted from QuestDagResolverRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class QuestDagUnlockChecker
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="questId">Parameter representing the questId (int).</param>
        /// <param name="allCompletedQuestIds">Parameter representing the allCompletedQuestIds (int[]).</param>
        /// <param name="dependencyGraph">Parameter representing the dependencyGraph (int[,]).</param>
        /// <param name="nodeCount">Parameter representing the nodeCount (int).</param>
        /// <returns>Returns isUnlocked of type bool.</returns>
        public static bool Check(int questId, int[] allCompletedQuestIds, int[,] dependencyGraph, int nodeCount)
        {
            if (questId < 0)
                throw new ArgumentOutOfRangeException(nameof(questId), "questId cannot be negative.");

            if (nodeCount < 0)
                nodeCount = 0; // Clamp negative nodeCount

            if (dependencyGraph == null || dependencyGraph.GetLength(0) == 0)
                return true;

            return IsNodeUnlockedRecursively(questId, allCompletedQuestIds, dependencyGraph, nodeCount, 0);
        }

        private static bool IsNodeUnlockedRecursively(
            int currentQuestId,
            int[] allCompletedQuestIds,
            int[,] dependencyGraph,
            int nodeCount,
            int depth)
        {
            if (depth > nodeCount)
            {
                // To avoid infinite loops in a cyclic graph, we stop at nodeCount depth.
                return false;
            }

            int edgeCount = dependencyGraph.GetLength(0);
            if (dependencyGraph.GetLength(1) < 2)
                return true;

            for (int i = 0; i < edgeCount; i++)
            {
                int predecessorId = dependencyGraph[i, 0];
                int successorId = dependencyGraph[i, 1];

                if (successorId == currentQuestId)
                {
                    bool predecessorIsCompleted = false;

                    if (allCompletedQuestIds != null)
                    {
                        for (int j = 0; j < allCompletedQuestIds.Length; j++)
                        {
                            if (allCompletedQuestIds[j] == predecessorId)
                            {
                                predecessorIsCompleted = true;
                                break;
                            }
                        }
                    }

                    if (!predecessorIsCompleted)
                    {
                        return false;
                    }

                    // Recursive graph traversal check to ensure entire predecessor chain is valid
                    if (!IsNodeUnlockedRecursively(predecessorId, allCompletedQuestIds, dependencyGraph, nodeCount, depth + 1))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
