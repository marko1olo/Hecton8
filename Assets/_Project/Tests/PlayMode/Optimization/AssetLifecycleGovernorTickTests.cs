using NUnit.Framework;
using UnityEngine;
using Hecton8.Optimization;

namespace Hecton8.Tests.Optimization
{
    [TestFixture]
    public class AssetLifecycleGovernorTickTests
    {
        private GameObject _go;
        private AssetLifecycleGovernor _governor;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("TestGovernor");
            _governor = _go.AddComponent<AssetLifecycleGovernor>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
            }
        }

        [Test]
        public void Tick_IncrementsFrameSequence()
        {
            // Arrange
            long initialFrameSequence = _governor.Test_GetFrameSequence();

            // Act
            _governor.Tick(0.016f);

            // Assert
            long frameSequence = _governor.Test_GetFrameSequence();
            Assert.That(frameSequence, Is.EqualTo(initialFrameSequence + 1L), "Tick should increment _frameSequence by 1.");
        }

        [Test]
        public void Tick_MultipleCalls_IncrementsSequenceCorrectly()
        {
            // Arrange
            long initialFrameSequence = _governor.Test_GetFrameSequence();

            // Act
            _governor.Tick(0.016f);
            _governor.Tick(0.016f);
            _governor.Tick(0.016f);

            // Assert
            long frameSequence = _governor.Test_GetFrameSequence();
            Assert.That(frameSequence, Is.EqualTo(initialFrameSequence + 3L), "Multiple Tick calls should increment _frameSequence correctly.");
        }
    }
}
