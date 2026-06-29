using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Hecton8.Optimization;

namespace Hecton8.Optimization.Tests
{
    public class MockAssetLifecycleGovernor : AssetLifecycleGovernor
    {
        public bool ReportColdTickBudgetIfNeededCalled { get; private set; }
        public bool ThrowExceptionOnEvaluate { get; set; } = true;

        protected override void EvaluateAddressableTtlAndQueueReleases()
        {
            if (ThrowExceptionOnEvaluate)
            {
                throw new InvalidOperationException("Mock exception during TTL evaluation");
            }
            base.EvaluateAddressableTtlAndQueueReleases();
        }

        protected override void ReportColdTickBudgetIfNeeded(long startTicks)
        {
            ReportColdTickBudgetIfNeededCalled = true;
            base.ReportColdTickBudgetIfNeeded(startTicks);
        }
    }

    [TestFixture]
    public class AssetLifecycleGovernorErrorPathTests
    {
        private GameObject _go;
        private MockAssetLifecycleGovernor _governor;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestGovernor");
            _governor = _go.AddComponent<MockAssetLifecycleGovernor>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                UnityEngine.Object.DestroyImmediate(_go);
            }
        }

        [Test]
        public void SlowTick_WhenEvaluateAddressableTtlThrows_EnsuresFinallyBlockExecutes()
        {
            // Arrange: bypass the cold release time check
            var field = typeof(AssetLifecycleGovernor).GetField("_nextColdReleaseTime", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "Could not find _nextColdReleaseTime field via reflection");

            // Set _nextColdReleaseTime to -1f so now < _nextColdReleaseTime is bypassed
            field.SetValue(_governor, -1f);

            _governor.ThrowExceptionOnEvaluate = true;
            _governor.ReportColdTickBudgetIfNeededCalled = false;

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => _governor.SlowTick());
            Assert.That(ex.Message, Is.EqualTo("Mock exception during TTL evaluation"));

            // Verify that the finally block executed
            Assert.That(_governor.ReportColdTickBudgetIfNeededCalled, Is.True, "ReportColdTickBudgetIfNeeded was not called in finally block");
        }
    }
}
