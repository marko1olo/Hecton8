using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Unity.Collections;
using Hecton8.Quest;

namespace Hecton8.Quest.Tests
{
    public class QuestGraphEvaluatorTests
    {
        [Test]
        public void DisposePendingSignals_OnNativeMemorySentinelException_SetsAndThrowsFirstException()
        {
            var stateManager = new QuestStateManager();
            Action onResultsAvailable = () => {};
            var evaluator = new QuestGraphEvaluator(stateManager, onResultsAvailable);

            // Ensure the sentinel ID is set to a positive value so the catch block gets executed
            var sentinelIdField = typeof(QuestGraphEvaluator).GetField("_pendingSignalsSentinelId", BindingFlags.NonPublic | BindingFlags.Instance);
            int currentSentinelId = (int)sentinelIdField.GetValue(evaluator);
            if (currentSentinelId <= 0)
            {
                sentinelIdField.SetValue(evaluator, 9999);
            }

            // To cause NativeMemorySentinel.Unregister(int id) to throw, we can modify the static _count field
            // to be out of bounds for the _records array.
            var countField = typeof(Hecton8.Core.NativeMemorySentinel).GetField("_count", BindingFlags.NonPublic | BindingFlags.Static);
            var recordsField = typeof(Hecton8.Core.NativeMemorySentinel).GetField("_records", BindingFlags.NonPublic | BindingFlags.Static);

            int originalCount = (int)countField.GetValue(null);
            var records = (Array)recordsField.GetValue(null);

            // Set _count to a huge number so iterating over it throws IndexOutOfRangeException
            countField.SetValue(null, records.Length + 1);

            try
            {
                var ex = Assert.Throws<IndexOutOfRangeException>(() => evaluator.Dispose());
                Assert.That(ex, Is.Not.Null);
            }
            finally
            {
                // Restore _count to prevent side effects in other tests
                countField.SetValue(null, originalCount);
                stateManager.Dispose();
            }
        }
    }
}
