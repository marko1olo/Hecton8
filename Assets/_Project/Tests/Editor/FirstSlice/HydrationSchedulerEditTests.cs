using System.Diagnostics;
using NUnit.Framework;
using Hecton8.SaveSystem;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public class HydrationSchedulerEditTests
    {
        [Test]
        public void CreateDeadlineTicks_ReturnsFutureTickCountBasedOnStopwatchAndFrameBudget()
        {
            // Arrange
            long currentTicks = Stopwatch.GetTimestamp();
            long expectedMin = currentTicks + HydrationScheduler.FrameBudgetTicks;

            // Act
            long deadlineTicks = HydrationScheduler.CreateDeadlineTicks();

            // Assert
            Assert.That(deadlineTicks, Is.GreaterThan(currentTicks));
            // Due to execution time, deadlineTicks will be slightly larger than expectedMin
            Assert.That(deadlineTicks, Is.GreaterThanOrEqualTo(expectedMin));
        }
    }
}