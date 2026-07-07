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
        public void AttackRanged_WithAnimationAndNotAttacking_SetsIsAttackingAndDoesNotSchedulePendingAttack()
        public void WithinAttackRange_NoTarget_ReturnsFalse()
        {
            var go = new GameObject("CandiceAIControllerTest");
            var controller = go.AddComponent<CandiceAIController>();

            controller.HasAttackAnimation = true;
            controller.IsAttacking = false;

            controller.AttackRanged();

            Assert.That(controller.IsAttacking, Is.True);

            var pendingAttackField = typeof(CandiceAIController).GetField("_pendingAttack", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            bool pendingAttack = (bool)pendingAttackField.GetValue(controller);
            Assert.That(pendingAttack, Is.False);
            controller.AttackTarget = null;

            Assert.IsFalse(controller.WithinAttackRange());
        public void AttackMelee_WithAttackAnimation_SetsIsAttackingTrue()
            var go = new GameObject();

            controller.AttackMelee();

            Assert.IsTrue(controller.IsAttacking);

            var pendingAttackField = typeof(CandiceAIController).GetField("_pendingAttack", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsFalse(pendingAttack);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void AttackRanged_WithoutAnimationAndNotAttacking_SetsIsAttackingAndSchedulesPendingAttack()
        public void WithinAttackRange_TargetOutOfRange_ReturnsFalse()
        {
            var go = new GameObject("CandiceAIControllerTest");
            var controller = go.AddComponent<CandiceAIController>();

            controller.HasAttackAnimation = false;
            controller.IsAttacking = false;

            controller.AttackRanged();

            Assert.That(controller.IsAttacking, Is.True);

            var pendingAttackField = typeof(CandiceAIController).GetField("_pendingAttack", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            bool pendingAttack = (bool)pendingAttackField.GetValue(controller);
            Assert.That(pendingAttack, Is.True);

            var pendingAttackIsRangedField = typeof(CandiceAIController).GetField("_pendingAttackIsRanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            bool pendingAttackIsRanged = (bool)pendingAttackIsRangedField.GetValue(controller);
            Assert.That(pendingAttackIsRanged, Is.True);

            Object.DestroyImmediate(go);
            var targetGo = new GameObject("Target");
            controller.AttackTarget = targetGo;
            controller.AttackRange = 5f;

            go.transform.position = Vector3.zero;
            targetGo.transform.position = new Vector3(10f, 0f, 0f);

            Assert.IsFalse(controller.WithinAttackRange());

            Object.DestroyImmediate(targetGo);
        }

        [Test]
        public void WithinAttackRange_TargetInRange_ReturnsTrueAndSetsLookPoint()
        {
            var go = new GameObject("CandiceAIControllerTest");
            var controller = go.AddComponent<CandiceAIController>();

            var targetGo = new GameObject("Target");
            controller.AttackTarget = targetGo;
            controller.AttackRange = 5f;

            go.transform.position = Vector3.zero;
            targetGo.transform.position = new Vector3(3f, 0f, 0f);

            Assert.IsTrue(controller.WithinAttackRange());
            Assert.AreEqual(targetGo.transform.position, controller.LookPoint);

            Object.DestroyImmediate(targetGo);
        public void AttackMelee_WithoutAttackAnimation_SchedulesPendingAttack()
            var go = new GameObject();

            controller.AttackMelee();

            Assert.IsTrue(controller.IsAttacking);

            var pendingAttackField = typeof(CandiceAIController).GetField("_pendingAttack", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsTrue(pendingAttack);

            var pendingAttackRangedField = typeof(CandiceAIController).GetField("_pendingAttackIsRanged", BindingFlags.NonPublic | BindingFlags.Instance);
            bool isRanged = (bool)pendingAttackRangedField.GetValue(controller);
            Assert.IsFalse(isRanged);


        public void AttackMelee_WhenAlreadyAttacking_DoesNothing()
            var go = new GameObject();
            controller.IsAttacking = true;

            var pendingAttackField = typeof(CandiceAIController).GetField("_pendingAttack", BindingFlags.NonPublic | BindingFlags.Instance);
            pendingAttackField.SetValue(controller, false);

            controller.AttackMelee();

            Assert.IsTrue(controller.IsAttacking);

            Assert.IsFalse(pendingAttack);

        }
    }
}
