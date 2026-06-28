using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CandiceAI.Tests
{
    public class CandiceAIControllerWanderTests
    {
        private GameObject _aiManagerObj;
        private CandiceAIManager _aiManager;
        private GameObject _controllerObj;
        private CandiceAIController _controller;
        private GameObject _wanderTargetObj;

        [SetUp]
        public void SetUp()
        {
            _aiManagerObj = new GameObject("CandiceAIManager");
            _aiManager = _aiManagerObj.AddComponent<CandiceAIManager>();
            _aiManagerObj.AddComponent<CandiceGrid>(); // Required by FindTarget

            _controllerObj = new GameObject("CandiceAIController");
            _controller = _controllerObj.AddComponent<CandiceAIController>();

            // Set required dependencies for FindTarget to execute correctly
            _controller.candice = _aiManager;

            _wanderTargetObj = new GameObject("WanderTarget");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_aiManagerObj);
            Object.DestroyImmediate(_controllerObj);
            Object.DestroyImmediate(_wanderTargetObj);
        }

        [UnityTest]
        public IEnumerator Wander_WithNullTarget_DoesNothing()
        {
            // Act
            _controller.Wander();

            yield return null;

            // Assert
            // State should be unchanged, testing via reflection or public getters where available
            // Since `switchWanderTarget` is private, we verify no exceptions are thrown.
            Assert.DoesNotThrow(() => _controller.Wander());
        }

        [UnityTest]
        public IEnumerator Wander_WithTargetFarAway_DoesNotSwitchTarget()
        {
            // Arrange
            _controller.wanderTarget = _wanderTargetObj;
            _controllerObj.transform.position = Vector3.zero;
            _wanderTargetObj.transform.position = new Vector3(10f, 0, 0); // sqrMagnitude = 100 > 25

            // Make sure switchWanderTarget is initially false to test it doesn't change
            // we will need to verify the outcome using mainTarget since switchWanderTarget is private
            _controller.MainTarget = null;

            // Act
            _controller.Wander();

            yield return null;

            // Assert
            Assert.That(_controller.MainTarget, Is.Null, "Target is far away, so it shouldn't switch wander target yet.");
        }

        [UnityTest]
        public IEnumerator Wander_WithTargetClose_SwitchesTargetAndCallsFindTarget()
        {
            // Arrange
            _controller.wanderTarget = _wanderTargetObj;
            _controllerObj.transform.position = Vector3.zero;
            _wanderTargetObj.transform.position = new Vector3(4f, 0, 0); // sqrMagnitude = 16 < 25

            // Act
            _controller.Wander();

            yield return null;

            // Assert
            Assert.That(_controller.MainTarget, Is.EqualTo(_wanderTargetObj));

            Vector3 expectedMovePoint = _wanderTargetObj.transform.position;
            expectedMovePoint.y = 1;
            Assert.That(_controller.MovePoint, Is.EqualTo(expectedMovePoint));
        }

        [UnityTest]
        public IEnumerator Wander_SwitchWanderTargetTrue_CallsFindTargetWithoutDistanceCheck()
        {
            // Arrange
            _controller.wanderTarget = _wanderTargetObj;
            _controllerObj.transform.position = Vector3.zero;
            _wanderTargetObj.transform.position = new Vector3(10f, 0, 0); // Far away

            // Hack to set switchWanderTarget to true without reflection:
            // We temporarily move the target close to trigger switchWanderTarget = true,
            // then move it away in the same frame before Wander gets called again
            _wanderTargetObj.transform.position = new Vector3(4f, 0, 0);
            _controller.Wander(); // Sets switchWanderTarget to true, and MainTarget

            // We reset MainTarget and MovePoint to simulate a clean state but switchWanderTarget is now likely false
            // Wait, FindTarget resets switchWanderTarget to false. We need reflection to set it directly.

            var fieldInfo = typeof(CandiceAIController).GetField("switchWanderTarget", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            fieldInfo.SetValue(_controller, true);

            _wanderTargetObj.transform.position = new Vector3(10f, 0, 0);
            _controller.MainTarget = null;

            // Act
            _controller.Wander();

            yield return null;

            // Assert
            Assert.That(_controller.MainTarget, Is.EqualTo(_wanderTargetObj));

            Vector3 expectedMovePoint = _wanderTargetObj.transform.position;
            expectedMovePoint.y = 1;
            Assert.That(_controller.MovePoint, Is.EqualTo(expectedMovePoint));
        }
    }
}
