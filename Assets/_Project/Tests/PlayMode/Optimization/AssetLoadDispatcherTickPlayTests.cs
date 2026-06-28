using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Hecton8.Optimization;

namespace Hecton8.Tests.PlayMode.Optimization
{
    public class AssetLoadDispatcherTickPlayTests
    {
        private GameObject _dispatcherGo;
        private AssetLoadDispatcher _dispatcher;

        [SetUp]
        public void SetUp()
        {
            _dispatcherGo = new GameObject("TestAssetLoadDispatcher");
            _dispatcher = _dispatcherGo.AddComponent<AssetLoadDispatcher>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_dispatcherGo != null)
                GameObject.DestroyImmediate(_dispatcherGo);
        }

        [Test]
        public void Tick_AgesQueuedRequests()
        {
            int requestId;
            bool enqueued = _dispatcher.Enqueue(1234u, AssetPriorityTier.Tier1Equipped, false, out requestId);
            Assert.That(enqueued, Is.True, "Failed to enqueue request.");

            // Set dispatch budget to 0 so it won't dispatch during tick
            var budgetField = typeof(AssetLoadDispatcher).GetField("dispatchBudgetMilliseconds", BindingFlags.NonPublic | BindingFlags.Instance);
            budgetField.SetValue(_dispatcher, 0f);

            var queuedRequestsField = typeof(AssetLoadDispatcher).GetField("_queuedRequests", BindingFlags.NonPublic | BindingFlags.Instance);
            var queuedRequests = (Array)queuedRequestsField.GetValue(_dispatcher);

            var elementBefore = queuedRequests.GetValue(0);
            var ageField = elementBefore.GetType().GetField("AgeFrames", BindingFlags.Public | BindingFlags.Instance);
            int ageBefore = (int)ageField.GetValue(elementBefore);
            Assert.That(ageBefore, Is.EqualTo(0));

            _dispatcher.Tick(0.1f);

            var queuedRequestsAfter = (Array)queuedRequestsField.GetValue(_dispatcher);
            var elementAfterValue = queuedRequestsAfter.GetValue(0);
            int ageAfter = (int)ageField.GetValue(elementAfterValue);

            Assert.That(ageAfter, Is.EqualTo(1));

            var queuedCountField = typeof(AssetLoadDispatcher).GetField("_queuedRequestCount", BindingFlags.NonPublic | BindingFlags.Instance);
            int queuedCount = (int)queuedCountField.GetValue(_dispatcher);
            Assert.That(queuedCount, Is.EqualTo(1));
        }

        [Test]
        public void Tick_DispatchesWithinBudget()
        {
            int requestId;
            _dispatcher.Enqueue(5678u, AssetPriorityTier.Tier1Equipped, false, out requestId);

            var queuedCountField = typeof(AssetLoadDispatcher).GetField("_queuedRequestCount", BindingFlags.NonPublic | BindingFlags.Instance);
            var inflightCountField = typeof(AssetLoadDispatcher).GetField("_inflightRequestCount", BindingFlags.NonPublic | BindingFlags.Instance);
            var readyCountField = typeof(AssetLoadDispatcher).GetField("_readyTicketCount", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That((int)queuedCountField.GetValue(_dispatcher), Is.EqualTo(1));
            Assert.That((int)inflightCountField.GetValue(_dispatcher), Is.EqualTo(0));
            Assert.That((int)readyCountField.GetValue(_dispatcher), Is.EqualTo(0));

            _dispatcher.Tick(0.1f);

            Assert.That((int)queuedCountField.GetValue(_dispatcher), Is.EqualTo(0));
            Assert.That((int)inflightCountField.GetValue(_dispatcher), Is.EqualTo(1));
            Assert.That((int)readyCountField.GetValue(_dispatcher), Is.EqualTo(1));
        }
    }
}
