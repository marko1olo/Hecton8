using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class FixedCapacityRingBufferTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            int head = 0;
            int tail = 0;
            int capacity = 5;

            // Act: Push
            int newHead = FixedCapacityRingBuffer.Calculate(head, tail, capacity, true);

            // Assert: Verify expected output behaviour
            Assert.AreEqual(1, newHead, "First push should move head to 1.");

            // Act: Pop
            int newTail = FixedCapacityRingBuffer.Calculate(newHead, tail, capacity, false);

            // Assert
            Assert.AreEqual(1, newTail, "First pop should move tail to 1.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            int capacity = 5;

            // Fill to capacity: next push fails.
            // When tail=0, max head is 4 before nextHead(0) == tail(0)
            int result = FixedCapacityRingBuffer.Calculate(4, 0, capacity, true);

            // Assert
            Assert.AreEqual(-1, result, "Pushing when full (nextHead == tail) must fail with -1.");

            // Pop from empty: failure.
            result = FixedCapacityRingBuffer.Calculate(0, 0, capacity, false);
            Assert.AreEqual(-1, result, "Popping when empty (head == tail) must fail with -1.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values
            // Act
            int resultPush = FixedCapacityRingBuffer.Calculate(0, 0, 0, true);
            int resultPop = FixedCapacityRingBuffer.Calculate(0, 0, 0, false);

            // Assert
            Assert.AreEqual(-1, resultPush, "Pushing to 0 capacity must fail without division by zero.");
            Assert.AreEqual(-1, resultPop, "Popping from 0 capacity must fail without division by zero.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            // Act
            int resultPush = FixedCapacityRingBuffer.Calculate(-5, 0, 5, true);
            int resultPop = FixedCapacityRingBuffer.Calculate(0, -2, 5, false);

            // Assert
            // Expected clamp: head=-5 -> 0, tail=0. Push -> nextHead=1
            Assert.AreEqual(1, resultPush, "Negative head must clamp gracefully.");

            // Expected clamp: head=0, tail=-2 -> 0. Pop -> empty
            Assert.AreEqual(-1, resultPop, "Negative tail must clamp gracefully.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            // Act
            // Extreme capacity -> push
            // nextHead = (int.MaxValue - 1 + 1) % int.MaxValue = 0
            // Since tail = 0, this is full
            int resultPush = FixedCapacityRingBuffer.Calculate(int.MaxValue - 1, 0, int.MaxValue, true);

            // Extreme capacity -> pop
            int resultPop = FixedCapacityRingBuffer.Calculate(int.MaxValue - 1, int.MaxValue - 2, int.MaxValue, false);

            // Assert
            Assert.AreEqual(-1, resultPush, "Large parameters should not overflow and evaluate full correctly.");
            Assert.AreEqual(int.MaxValue - 1, resultPop, "Large parameters should pop correctly.");
        }
    }
}
