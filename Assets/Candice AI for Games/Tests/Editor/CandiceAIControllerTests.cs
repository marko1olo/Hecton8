using NUnit.Framework;
using UnityEngine;
using CandiceAIforGames.AI;
using System.Reflection;

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

        [Test]
        public void AttackMelee_WithAttackAnimation_SetsIsAttackingTrue()
        {
            var go = new GameObject();
            var controller = go.AddComponent<CandiceAIController>();
            controller.HasAttackAnimation = true;
            controller.IsAttacking = false;

            controller.AttackMelee();

            Assert.IsTrue(controller.IsAttacking);

            var pendingAttackField = typeof(CandiceAIController).GetField("_pendingAttack", BindingFlags.NonPublic | BindingFlags.Instance);
            bool pendingAttack = (bool)pendingAttackField.GetValue(controller);
            Assert.IsFalse(pendingAttack);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void AttackMelee_WithoutAttackAnimation_SchedulesPendingAttack()
        {
            var go = new GameObject();
            var controller = go.AddComponent<CandiceAIController>();
            controller.HasAttackAnimation = false;
            controller.IsAttacking = false;

            controller.AttackMelee();

            Assert.IsTrue(controller.IsAttacking);

            var pendingAttackField = typeof(CandiceAIController).GetField("_pendingAttack", BindingFlags.NonPublic | BindingFlags.Instance);
            bool pendingAttack = (bool)pendingAttackField.GetValue(controller);
            Assert.IsTrue(pendingAttack);

            var pendingAttackRangedField = typeof(CandiceAIController).GetField("_pendingAttackIsRanged", BindingFlags.NonPublic | BindingFlags.Instance);
            bool isRanged = (bool)pendingAttackRangedField.GetValue(controller);
            Assert.IsFalse(isRanged);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void AttackMelee_WhenAlreadyAttacking_DoesNothing()
        {
            var go = new GameObject();
            var controller = go.AddComponent<CandiceAIController>();
            controller.HasAttackAnimation = false;
            controller.IsAttacking = true;

            var pendingAttackField = typeof(CandiceAIController).GetField("_pendingAttack", BindingFlags.NonPublic | BindingFlags.Instance);
            pendingAttackField.SetValue(controller, false);

            controller.AttackMelee();

            Assert.IsTrue(controller.IsAttacking);

            bool pendingAttack = (bool)pendingAttackField.GetValue(controller);
            Assert.IsFalse(pendingAttack);

            Object.DestroyImmediate(go);
        }
    }
}
