using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using CandiceAIforGames.AI;
using CandiceAIforGames.AI.Pathfinding;

namespace Tests.CandiceAI
{
    public class CandiceAIManagerIsPointWalkableTests
    {
        private GameObject _managerGo;
        private CandiceAIManager _manager;
        private CandiceGrid _grid;
        private GameObject _obstacleGo;

        [SetUp]
        public void Setup()
        {
            _managerGo = new GameObject("AIManager");
            _manager = _managerGo.AddComponent<CandiceAIManager>();
            _grid = _managerGo.AddComponent<CandiceGrid>();
            _manager.grid = _grid;

            // Setup layer for obstacles
            int obstacleLayer = 8; // Custom layer
            _grid.unwalkableMask = 1 << obstacleLayer;
            _grid.nodeRadius = 1f;

            _obstacleGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _obstacleGo.layer = obstacleLayer;
            _obstacleGo.transform.position = new Vector3(10f, 10f, 10f);
            _obstacleGo.transform.localScale = new Vector3(2f, 2f, 2f);
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            if (_managerGo != null)
            {
                Object.Destroy(_managerGo);
            }
            if (_obstacleGo != null)
            {
                Object.Destroy(_obstacleGo);
            }
            yield return null;
        }

        [Test]
        public void IsPointWalkable_NullGrid_ReturnsFalse()
        {
            // Arrange
            _manager.grid = null;

            // Act
            bool result = _manager.IsPointWalkable(Vector3.zero);

            // Assert
            Assert.That(result, Is.False, "Expected IsPointWalkable to return false when grid is null.");
        }

        [UnityTest]
        public IEnumerator IsPointWalkable_PointInEmptySpace_ReturnsTrue()
        {
            // Physics might need a frame to register the collider
            yield return null;

            // Arrange
            Vector3 emptyPoint = new Vector3(0, 0, 0);

            // Act
            bool result = _manager.IsPointWalkable(emptyPoint);

            // Assert
            Assert.That(result, Is.True, "Expected IsPointWalkable to return true for empty space.");
        }

        [UnityTest]
        public IEnumerator IsPointWalkable_PointInsideObstacle_ReturnsFalse()
        {
            // Physics might need a frame to register the collider
            yield return null;

            // Arrange
            Vector3 obstaclePoint = new Vector3(10f, 10f, 10f);

            // Act
            bool result = _manager.IsPointWalkable(obstaclePoint);

            // Assert
            Assert.That(result, Is.False, "Expected IsPointWalkable to return false when point is inside an obstacle.");
        }
    }
}
