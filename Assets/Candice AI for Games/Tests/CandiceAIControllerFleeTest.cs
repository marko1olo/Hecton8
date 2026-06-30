using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CandiceAIforGames.AI;
using CandiceAIforGames.AI.Pathfinding;

namespace CandiceAIforGames.Tests
{
    public class CandiceAIControllerFleeTest
    {
        private GameObject aiObject;
        private CandiceAIController aiController;
        private GameObject targetObject;
        private GameObject managerObject;
        private CandiceAIManager aiManager;

        [SetUp]
        public void SetUp()
        {
            managerObject = new GameObject("CandiceAIManager");
            aiManager = managerObject.AddComponent<CandiceAIManager>();
            managerObject.AddComponent<CandiceGrid>();

            aiObject = new GameObject("CandiceAI");
            aiController = aiObject.AddComponent<CandiceAIController>();

            targetObject = new GameObject("Target");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(aiObject);
            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(managerObject);
        }

        [Test]
        public void Flee_SetsLookPointAndMovePointAwayFromMainTarget()
        {
            // Arrange
            aiController.MainTarget = targetObject;
            aiObject.transform.position = new Vector3(5, 0, 5);
            targetObject.transform.position = new Vector3(10, 0, 10);

            // Expected direction is away from the target
            // moveDirection = transform.position - MainTarget.transform.position;
            // moveDirection = (5, 0, 5) - (10, 0, 10) = (-5, 0, -5)
            Vector3 expectedDirection = new Vector3(-5, 0, -5);

            // Act
            aiController.Flee();

            // Assert
            Assert.That(aiController.LookPoint, Is.EqualTo(expectedDirection));
            Assert.That(aiController.MovePoint, Is.EqualTo(expectedDirection));
        }

        [Test]
        public void Flee_WhenTargetAtSamePosition_SetsPointsToZero()
        {
            // Arrange
            aiController.MainTarget = targetObject;
            aiObject.transform.position = new Vector3(5, 0, 5);
            targetObject.transform.position = new Vector3(5, 0, 5);

            // Act
            aiController.Flee();

            // Assert
            Assert.That(aiController.LookPoint, Is.EqualTo(Vector3.zero));
            Assert.That(aiController.MovePoint, Is.EqualTo(Vector3.zero));
        }
    }
}
