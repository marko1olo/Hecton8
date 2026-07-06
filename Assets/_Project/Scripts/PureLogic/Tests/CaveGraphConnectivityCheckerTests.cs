using NUnit.Framework;
using System;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class CaveGraphConnectivityCheckerTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs (Linear chain)
            int nodeCount = 3;
            bool[,] matrix = new bool[3, 3] {
                { false, true, false },
                { true, false, true },
                { false, true, false }
            };

            // Act
            bool result = CaveGraphConnectivityChecker.Check(nodeCount, matrix, out int[] disconnected);

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(0, disconnected.Length);
        }

        [Test]
        public void Test_HappyPath_Disconnected_Case01()
        {
            // Arrange: Isolated node 2
            int nodeCount = 3;
            bool[,] matrix = new bool[3, 3] {
                { false, true, false },
                { true, false, false },
                { false, false, false }
            };

            // Act
            bool result = CaveGraphConnectivityChecker.Check(nodeCount, matrix, out int[] disconnected);

            // Assert
            Assert.IsFalse(result);
            Assert.AreEqual(1, disconnected.Length);
            Assert.AreEqual(2, disconnected[0]);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (Single node)
            int nodeCount = 1;
            bool[,] matrix = new bool[1, 1] { { false } };

            // Act
            bool result = CaveGraphConnectivityChecker.Check(nodeCount, matrix, out int[] disconnected);

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(0, disconnected.Length);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (Zero nodes)
            int nodeCount = 0;
            bool[,] matrix = new bool[0, 0];

            // Act
            bool result = CaveGraphConnectivityChecker.Check(nodeCount, matrix, out int[] disconnected);

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(0, disconnected.Length);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            int nodeCount = -1;
            bool[,] matrix = new bool[0, 0];

            // Act
            bool result = CaveGraphConnectivityChecker.Check(nodeCount, matrix, out int[] disconnected);

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(0, disconnected.Length);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup exception case (mismatched matrix size)
            int nodeCount = 3;
            bool[,] matrix = new bool[2, 2];

            // Act & Assert
            Assert.Throws<ArgumentException>(() => CaveGraphConnectivityChecker.Check(nodeCount, matrix, out int[] disconnected));
        }

        [Test]
        public void Test_ExtremeInputs_NullMatrix_Case06()
        {
            // Arrange: Setup null matrix
            int nodeCount = 3;
            bool[,] matrix = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => CaveGraphConnectivityChecker.Check(nodeCount, matrix, out int[] disconnected));
        }
    }
}
