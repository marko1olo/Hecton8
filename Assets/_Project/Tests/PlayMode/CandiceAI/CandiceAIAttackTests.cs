using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CandiceAIforGames.AI;

namespace Hecton8.Tests.PlayMode.CandiceAI
{
    public class CandiceAIAttackTests
    {
        private GameObject _aiObject;
        private CandiceAIController _aiController;
        private GameObject _candiceManagerObj;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _candiceManagerObj = new GameObject("CandiceAIManager");
            _candiceManagerObj.AddComponent<CandiceAIManager>();

            _aiObject = new GameObject("CandiceAIController");
            _aiController = _aiObject.AddComponent<CandiceAIController>();

            // Wait for Awake and Start to complete
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_aiObject != null)
            {
                Object.Destroy(_aiObject);
            }
            if (_candiceManagerObj != null)
            {
                Object.Destroy(_candiceManagerObj);
            }
            yield return null; // Important per memory context for Object.Destroy in PlayMode tests
        }

        [UnityTest]
        public IEnumerator AttackRanged_WhenHasAnimationAndNotAttacking_SetsIsAttackingAndDoesNotSchedule()
        {
            _aiController.HasAttackAnimation = true;
            _aiController.IsAttacking = false;

            _aiController.AttackRanged();

            Assert.That(_aiController.IsAttacking, Is.True);

            bool pendingAttack = (bool)typeof(CandiceAIController).GetField("_pendingAttack", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_aiController);
            Assert.That(pendingAttack, Is.False, "Pending attack should not be scheduled when HasAttackAnimation is true");

            yield return null;
        }

        [UnityTest]
        public IEnumerator AttackRanged_WhenNoAnimationAndNotAttacking_SetsIsAttackingAndSchedulesRangedAttack()
        {
            _aiController.HasAttackAnimation = false;
            _aiController.IsAttacking = false;

            _aiController.AttackRanged();

            Assert.That(_aiController.IsAttacking, Is.True);

            bool pendingAttack = (bool)typeof(CandiceAIController).GetField("_pendingAttack", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_aiController);
            bool pendingAttackIsRanged = (bool)typeof(CandiceAIController).GetField("_pendingAttackIsRanged", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_aiController);

            Assert.That(pendingAttack, Is.True, "Pending attack should be scheduled when HasAttackAnimation is false");
            Assert.That(pendingAttackIsRanged, Is.True, "Pending attack should be ranged");

            yield return null;
        }

        [UnityTest]
        public IEnumerator AttackRanged_WhenIsAttacking_DoesNothing()
        {
            _aiController.IsAttacking = true;
            _aiController.HasAttackAnimation = false;

            _aiController.AttackRanged();

            Assert.That(_aiController.IsAttacking, Is.True);

            bool pendingAttack = (bool)typeof(CandiceAIController).GetField("_pendingAttack", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_aiController);
            Assert.That(pendingAttack, Is.False, "Pending attack should not be scheduled if already attacking");

            yield return null;
        }
    }
}
