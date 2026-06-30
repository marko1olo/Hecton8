using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CandiceAIforGames.AI;

namespace CandiceAIforGames.Tests.PlayMode
{
    public class CandiceAIControllerTests
    {
        private GameObject _managerObj;
        private GameObject _controllerObj;
        private CandiceAIController _controller;

        [SetUp]
        public void SetUp()
        {
            _managerObj = new GameObject("Manager");
            _managerObj.AddComponent<CandiceGrid>();
            _managerObj.AddComponent<CandiceAIManager>();

            _controllerObj = new GameObject("Controller");
            _controller = _controllerObj.AddComponent<CandiceAIController>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_controllerObj);
            Object.DestroyImmediate(_managerObj);
        }

        [UnityTest]
        public IEnumerator WithinAttackRange_ReturnsFalse_WhenTargetIsNull()
        {
            _controller.AttackTarget = null;
            bool result = _controller.WithinAttackRange();
            Assert.That(result, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator WithinAttackRange_ReturnsTrue_WhenTargetIsInRange()
        {
            GameObject targetObj = new GameObject("Target");
            targetObj.transform.position = new Vector3(5, 0, 0);

            _controllerObj.transform.position = new Vector3(0, 0, 0);
            _controller.AttackTarget = targetObj;
            _controller.AttackRange = 6f;

            bool result = _controller.WithinAttackRange();

            Assert.That(result, Is.True);
            Assert.That(_controller.LookPoint, Is.EqualTo(targetObj.transform.position));

            Object.DestroyImmediate(targetObj);
            yield return null;
        }

        [UnityTest]
        public IEnumerator WithinAttackRange_ReturnsFalse_WhenTargetIsOutOfRange()
        {
            GameObject targetObj = new GameObject("Target");
            targetObj.transform.position = new Vector3(10, 0, 0);

            _controllerObj.transform.position = new Vector3(0, 0, 0);
            _controller.AttackTarget = targetObj;
            _controller.AttackRange = 5f;

            bool result = _controller.WithinAttackRange();

            Assert.That(result, Is.False);

            Object.DestroyImmediate(targetObj);
            yield return null;
        }
    }
}
