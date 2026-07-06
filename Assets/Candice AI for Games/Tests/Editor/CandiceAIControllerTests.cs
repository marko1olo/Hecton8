using NUnit.Framework;
using UnityEngine;
using CandiceAIforGames.AI;

namespace CandiceAIforGames.AI.Tests
{
    public class CandiceAIControllerTests
    {
        [Test]
        public void GetPickaxe_DoesNotThrow()
        {
            var go = new GameObject("CandiceAIControllerTest");
            var controller = go.AddComponent<CandiceAIController>();

            Assert.DoesNotThrow(() => controller.GetPickaxe());

            Object.DestroyImmediate(go);
        }

        [Test]
        public void AddRegistrationListener_AddsItemToList()
        {
            var go = new GameObject("CandiceAIControllerTest");
            var controller = go.AddComponent<CandiceAIController>();

            bool listenerCalled = false;
            System.Action<bool, int> mockListener = (isRegistered, agentId) => { listenerCalled = true; };

            controller.AddRegistrationListener(mockListener);

            var fieldInfo = typeof(CandiceAIController).GetField("readyStateListeners", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var readyStateListeners = (System.Collections.Generic.List<System.Action<bool, int>>)fieldInfo.GetValue(controller);

            Assert.That(readyStateListeners, Is.Not.Null);
            Assert.That(readyStateListeners.Count, Is.EqualTo(1));
            Assert.That(readyStateListeners[0], Is.EqualTo(mockListener));

            Object.DestroyImmediate(go);
        }
    }
}
