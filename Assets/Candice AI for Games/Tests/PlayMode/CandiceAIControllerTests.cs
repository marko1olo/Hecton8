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

        [UnityTest]
        public IEnumerator AvoidObstacles_WithoutCollider_SetsHalfHeightToLocalScaleXTimesTwo()
        {
            _controllerObj.transform.localScale = new Vector3(3f, 1f, 1f);

            var mainTargetObj = new GameObject("MainTarget");
            _controller.MainTarget = mainTargetObj;

            yield return null;

            _controller.AvoidObstacles();

            Assert.That(_controller.HalfHeight, Is.EqualTo(6f).Within(0.001f));

            Object.DestroyImmediate(mainTargetObj);
        }

        [UnityTest]
        public IEnumerator AvoidObstacles_With3DCollider_SetsHalfHeightToColliderBoundsExtentsXTimesTwo()
        {
            var col = _controllerObj.AddComponent<BoxCollider>();
            col.size = new Vector3(2f, 1f, 1f); // extents.x = 1

            _controller.Is3D = true;

            var mainTargetObj = new GameObject("MainTarget");
            _controller.MainTarget = mainTargetObj;

            yield return null;

            _controller.AvoidObstacles();

            Assert.That(_controller.HalfHeight, Is.EqualTo(2f).Within(0.001f));

            Object.DestroyImmediate(mainTargetObj);
        }

        [UnityTest]
        public IEnumerator AvoidObstacles_With2DCollider_SetsHalfHeightToCollider2DBoundsExtentsXTimesTwo()
        {
            var col = _controllerObj.AddComponent<BoxCollider2D>();
            col.size = new Vector2(4f, 1f); // extents.x = 2

            _controller.Is3D = false;

            var mainTargetObj = new GameObject("MainTarget");
            _controller.MainTarget = mainTargetObj;

            yield return null;

            _controller.AvoidObstacles();

            Assert.That(_controller.HalfHeight, Is.EqualTo(4f).Within(0.001f));

            Object.DestroyImmediate(mainTargetObj);
        }
    }
}
