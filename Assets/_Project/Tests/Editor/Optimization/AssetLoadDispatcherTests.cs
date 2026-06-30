using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using Hecton8.Optimization;
using Hecton8.Core;

namespace Hecton8.Tests.Optimization
{
    [TestFixture]
    public class AssetLoadDispatcherTests
    {
        private GameObject _go;
        private AssetLoadDispatcher _dispatcher;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestAssetLoadDispatcher");
            _dispatcher = _go.AddComponent<AssetLoadDispatcher>();

            // Clear any static state
            var instanceField = typeof(AssetLoadDispatcher).GetField("s_registeredInstance", BindingFlags.NonPublic | BindingFlags.Static);
            if (instanceField != null)
            {
                instanceField.SetValue(null, null);
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                UnityEngine.MonoBehaviour.DestroyImmediate(_go);
            }

            var instanceField = typeof(AssetLoadDispatcher).GetField("s_registeredInstance", BindingFlags.NonPublic | BindingFlags.Static);
            if (instanceField != null)
            {
                instanceField.SetValue(null, null);
            }
        }

        [Test]
        public void Tick_AgesQueuedRequests()
        {
            // Arrange
            _dispatcher.Enqueue(12345u, (AssetPriorityTier)1, false, out int requestId);

            var queuedRequestsField = typeof(AssetLoadDispatcher).GetField("_queuedRequests", BindingFlags.NonPublic | BindingFlags.Instance);
            var queuedRequestCountField = typeof(AssetLoadDispatcher).GetField("_queuedRequestCount", BindingFlags.NonPublic | BindingFlags.Instance);

            // To prevent dispatching, set maxReadyTicketCount to 0
            var maxReadyTicketCountField = typeof(AssetLoadDispatcher).GetField("maxReadyTicketCount", BindingFlags.NonPublic | BindingFlags.Instance);
            maxReadyTicketCountField.SetValue(_dispatcher, 0);

            var queuedRequests = (System.Array)queuedRequestsField.GetValue(_dispatcher);
            object initialRequest = queuedRequests.GetValue(0);
            var ageField = initialRequest.GetType().GetField("AgeFrames", BindingFlags.Public | BindingFlags.Instance);
            int initialAge = (int)ageField.GetValue(initialRequest);
            Assert.That(initialAge, Is.EqualTo(0));

            // Act
            _dispatcher.Tick(0.016f);

            // Assert
            queuedRequests = (System.Array)queuedRequestsField.GetValue(_dispatcher);
            object agedRequest = queuedRequests.GetValue(0);
            int newAge = (int)ageField.GetValue(agedRequest);

            int count = (int)queuedRequestCountField.GetValue(_dispatcher);
            Assert.That(count, Is.EqualTo(1), "Request should still be queued");

            Assert.That(newAge, Is.EqualTo(1));
        }

        [Test]
        public void Tick_DispatchesWithinBudget_WhenRequestsQueued()
        {
            // Arrange
            _dispatcher.Enqueue(54321u, (AssetPriorityTier)1, false, out int requestId);

            var queuedRequestCountField = typeof(AssetLoadDispatcher).GetField("_queuedRequestCount", BindingFlags.NonPublic | BindingFlags.Instance);
            var readyTicketCountField = typeof(AssetLoadDispatcher).GetField("_readyTicketCount", BindingFlags.NonPublic | BindingFlags.Instance);

            int initialQueued = (int)queuedRequestCountField.GetValue(_dispatcher);
            int initialReady = (int)readyTicketCountField.GetValue(_dispatcher);

            Assert.That(initialQueued, Is.EqualTo(1));
            Assert.That(initialReady, Is.EqualTo(0));

            // Setup budget variables to allow dispatch
            var uploadBudgetField = typeof(AssetLoadDispatcher).GetField("_frameUploadBudgetBytes", BindingFlags.NonPublic | BindingFlags.Instance);
            uploadBudgetField.SetValue(_dispatcher, 100L * 1024L * 1024L); // 100 MB budget

            // Act
            _dispatcher.Tick(0.016f);

            // Assert
            int newQueued = (int)queuedRequestCountField.GetValue(_dispatcher);
            int newReady = (int)readyTicketCountField.GetValue(_dispatcher);

            Assert.That(newQueued, Is.EqualTo(0), "Request should be removed from queue");
            Assert.That(newReady, Is.EqualTo(1), "Request should be moved to ready tickets");
        }

        [Test]
        public void SlowTick_EvaluatesUiMipBiasGate_WhenQueued()
        {
            // Arrange
            var queuedField = typeof(AssetLoadDispatcher).GetField("_uiMipBiasGateEvaluationQueued", BindingFlags.NonPublic | BindingFlags.Instance);
            queuedField.SetValue(_dispatcher, true);

            bool beforeTick = (bool)queuedField.GetValue(_dispatcher);
            Assert.That(beforeTick, Is.True);

            // Act
            _dispatcher.SlowTick();

            // Assert
            bool afterTick = (bool)queuedField.GetValue(_dispatcher);
            Assert.That(afterTick, Is.False, "SlowTick should consume the queued evaluation flag");
        }
    }
}
