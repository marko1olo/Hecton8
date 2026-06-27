using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class QuestDagUnlockCheckerTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            // No dependencies: always unlocked
            bool resultNoDeps = QuestDagUnlockChecker.Check(5, new int[] { 1, 2 }, new int[0, 2], 10);
            Assert.IsTrue(resultNoDeps, "No dependencies should always be unlocked.");

            // One incomplete predecessor: locked
            int[,] graphIncomplete = new int[,] { { 1, 5 }, { 2, 5 } };
            bool resultIncomplete = QuestDagUnlockChecker.Check(5, new int[] { 1 }, graphIncomplete, 10);
            Assert.IsFalse(resultIncomplete, "One incomplete predecessor should return false.");

            // All complete: unlocked
            int[,] graphComplete = new int[,] { { 1, 5 }, { 2, 5 } };
            bool resultComplete = QuestDagUnlockChecker.Check(5, new int[] { 1, 2 }, graphComplete, 10);
            Assert.IsTrue(resultComplete, "All predecessors complete should return true.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange
            int[,] graph = new int[,] { { 0, 1 } };

            // Act: questId is 0, which has no dependencies in the graph (it is a predecessor, not a successor)
            bool resultRootNode = QuestDagUnlockChecker.Check(0, new int[0], graph, 2);

            // Assert
            Assert.IsTrue(resultRootNode, "Root node with no dependencies mapping to it should be unlocked.");

            // Test boundary nodeCount
            bool resultNodeCountZero = QuestDagUnlockChecker.Check(1, new int[] { 0 }, graph, 0);
            // With depth > nodeCount (0 > 0 is false on first, but depth + 1 > 0 is true on recursive check of predecessor)
            Assert.IsFalse(resultNodeCountZero, "nodeCount 0 prevents traversal and marks locked.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange
            int[,] graphEmpty = new int[0, 0]; // Empty graph

            // Act
            bool resultEmptyGraph = QuestDagUnlockChecker.Check(1, new int[0], graphEmpty, 5);
            bool resultNullGraph = QuestDagUnlockChecker.Check(1, new int[0], null, 5);

            // Assert
            Assert.IsTrue(resultEmptyGraph, "Empty dependency graph should mean unlocked.");
            Assert.IsTrue(resultNullGraph, "Null dependency graph should mean unlocked.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange
            int[,] graph = new int[,] { { 1, 2 } };

            // Act & Assert
            // negative questId should throw ArgumentOutOfRangeException
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                QuestDagUnlockChecker.Check(-1, new int[] { 1 }, graph, 10));

            // negative nodeCount should be clamped to 0
            bool resultNegativeNodeCount = QuestDagUnlockChecker.Check(2, new int[] { 1 }, graph, -5);
            Assert.IsFalse(resultNegativeNodeCount, "Negative nodeCount clamped to 0 should fail traversal.");

            // However, a node with no dependencies should pass even if negative nodeCount
            bool resultNoDepsNegativeNodeCount = QuestDagUnlockChecker.Check(3, new int[0], graph, -5);
            Assert.IsTrue(resultNoDepsNegativeNodeCount, "Negative nodeCount but no dependencies should pass.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange
            // Create a deep dependency chain 0 -> 1 -> 2 -> ... -> 99 -> 100
            int chainLength = 100;
            int[,] graph = new int[chainLength, 2];
            int[] allCompleted = new int[chainLength];

            for (int i = 0; i < chainLength; i++)
            {
                graph[i, 0] = i;
                graph[i, 1] = i + 1;
                allCompleted[i] = i;
            }

            // Act
            // Node 100 depends on 99, 99 on 98... all are in allCompleted
            bool resultChain = QuestDagUnlockChecker.Check(100, allCompleted, graph, 200);

            // Assert
            Assert.IsTrue(resultChain, "Deep valid chain should correctly traverse and return true.");

            // Act: cyclic graph 0 -> 1 -> 2 -> 0
            int[,] cyclicGraph = new int[,] { { 0, 1 }, { 1, 2 }, { 2, 0 } };
            // Request 0, which requires 2. 2 requires 1. 1 requires 0.
            bool resultCycle = QuestDagUnlockChecker.Check(0, new int[] { 1, 2 }, cyclicGraph, 5);

            // Assert
            Assert.IsFalse(resultCycle, "Cyclic dependency that requires itself while NOT completed should return false without infinite loop.");
        }
    }
}
