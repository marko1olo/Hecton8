using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Hecton8.Optimization;

namespace Hecton8.Optimization.Tests
{
    [TestFixture]
    public class AssetLifecycleGovernorErrorPathTests
    {
        private GameObject _go;
        private AssetLifecycleGovernor _governor;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestGovernor");
            _governor = _go.AddComponent<AssetLifecycleGovernor>();
        }

        [TearDown]
        public void TearDown()
        {
            AssetLifecycleGovernor.OnEvaluateAddressableTtlAndQueueReleases = null;
            AssetLifecycleGovernor.OnReportColdTickBudgetIfNeeded = null;
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

            bool reportCalled = false;
            AssetLifecycleGovernor.OnEvaluateAddressableTtlAndQueueReleases = () =>
            {
                throw new InvalidOperationException("Mock exception during TTL evaluation");
            };
            AssetLifecycleGovernor.OnReportColdTickBudgetIfNeeded = (ticks) =>
            {
                reportCalled = true;
            };

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => _governor.SlowTick());
            Assert.That(ex.Message, Is.EqualTo("Mock exception during TTL evaluation"));

            // Verify that the finally block executed
            Assert.That(reportCalled, Is.True, "ReportColdTickBudgetIfNeeded was not called in finally block");
        }
    }
}
